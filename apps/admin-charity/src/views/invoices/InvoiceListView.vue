<script setup lang="ts">
/**
 * 發票與收據管理（規劃書 §6.5）：接真 API。
 * 查詢、匯出給會計申報、手動補登號碼、作廢與折讓。列表與匯出都不含捐款人的個人資料。
 * 作廢只能在「開立當期」進行；跨期（已經申報過）的憑證改走「折讓」。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import InvoiceStatusTag from '@/components/InvoiceStatusTag.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import DangerConfirmDialog from '@/components/DangerConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { allowInvoice, exportInvoices, listInvoices, setManualInvoiceNumber, voidInvoice, type InvoiceFilter, type InvoiceItem } from '@/api/invoices'
import { reissueInvoice } from '@/api/donations'
import { listProjects, type ProjectListItem } from '@/api/projects'
import { AdminApiError } from '@/api/http'
import { formatMoney, formatTaipei, todayTaipei } from '@/utils/format'
import { hasPermission } from '@/auth/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const isDesktop = computed(() => breakpoint.value === 'desktop')

const canIssue = computed(() => hasPermission('n5.donation_invoice.issue'))
const canVoid = computed(() => hasPermission('n5.donation_invoice.void'))

const filters = reactive({
  range: null as [string, string] | null,
  issueStatus: '',
  voidStatus: '',
  invoiceType: '',
  projectId: '',
  keyword: '',
})
const projectOptions = ref<ProjectListItem[]>([])
const items = ref<InvoiceItem[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const loadError = ref('')

function currentFilter(): InvoiceFilter {
  return {
    from: filters.range?.[0],
    to: filters.range?.[1],
    issueStatus: filters.issueStatus || undefined,
    voidStatus: filters.voidStatus || undefined,
    invoiceType: filters.invoiceType || undefined,
    projectId: filters.projectId || undefined,
    keyword: filters.keyword.trim() || undefined,
  }
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const result = await listInvoices(currentFilter(), page.value, pageSize)
    items.value = result.items
    totalCount.value = result.totalCount
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '查詢時發生問題，請稍後再試一次'
  } finally {
    loading.value = false
  }
}

function search() {
  page.value = 1
  void load()
}

function resetFilters() {
  Object.assign(filters, { range: null, issueStatus: '', voidStatus: '', invoiceType: '', projectId: '', keyword: '' })
  search()
}

watch(page, () => { void load() })

onMounted(async () => {
  void load()
  try { projectOptions.value = await listProjects() } catch { /* 沒有項目檢視權限時不給篩選選項 */ }
})

const typeLabel = (t: InvoiceItem['invoiceType']) => (t === 'donation_receipt' ? '捐贈收據' : '電子發票')

// 哪些操作適用於哪一列
const canSetNumber = (r: InvoiceItem) => canIssue.value && r.voidStatus === 'none' && (r.issueStatus === 'pending' || r.issueStatus === 'failed')
const canReissue = (r: InvoiceItem) => canIssue.value && r.issueStatus === 'failed' && r.voidStatus === 'none'
const canVoidRow = (r: InvoiceItem) => canVoid.value && r.voidStatus === 'none'
const canAllowRow = (r: InvoiceItem) => canVoid.value && r.voidStatus === 'none' && r.issueStatus === 'issued'
const hasActions = (r: InvoiceItem) => canSetNumber(r) || canReissue(r) || canVoidRow(r) || canAllowRow(r)

// ── 匯出 ───────────────────────────────────────────────────────────────
const exporting = ref(false)
async function doExport() {
  exporting.value = true
  try {
    await exportInvoices(currentFilter())
    ElMessage.success('已匯出，匯出內容不含捐款人個資；這次匯出已記入操作紀錄')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

// ── 操作 ───────────────────────────────────────────────────────────────
const target = ref<InvoiceItem | null>(null)
const busy = ref(false)

async function run(action: () => Promise<unknown>, okMessage: string): Promise<boolean> {
  if (busy.value) return false
  busy.value = true
  try {
    await action()
    ElMessage.success(okMessage)
    await load()
    return true
  } catch (error) {
    ElMessage.error({ message: error instanceof AdminApiError ? error.detail : '操作失敗，請稍後再試', duration: 6000, showClose: true })
    return false
  } finally {
    busy.value = false
  }
}

// 手動補登號碼
const numberVisible = ref(false)
const numberForm = reactive({ invoiceNo: '', issuedOn: '', reason: '' })
const disableFuture = (d: Date) => d.getTime() > Date.now()

function askNumber(row: InvoiceItem) {
  target.value = row
  Object.assign(numberForm, { invoiceNo: '', issuedOn: todayTaipei(), reason: '' })
  numberVisible.value = true
}

async function doNumber() {
  if (!target.value) return
  const no = numberForm.invoiceNo.trim().toUpperCase()
  if (!/^[A-Z0-9-]{4,32}$/.test(no)) {
    ElMessage.warning('號碼請填 4 到 32 個英文字母、數字或連字號')
    return
  }
  if (numberForm.reason.trim().length < 2) {
    ElMessage.warning('請寫下補登原因（例如：已在加值中心後台手動開立）')
    return
  }
  const ok = await run(
    () => setManualInvoiceNumber(target.value!.invoiceId, { invoiceNo: no, issuedOn: numberForm.issuedOn || null, reason: numberForm.reason.trim() }),
    '已補登號碼，並寄出憑證通知信給捐款人',
  )
  if (ok) numberVisible.value = false
}

// 作廢與折讓
const voidVisible = ref(false)
const allowVisible = ref(false)

function askVoid(row: InvoiceItem) {
  target.value = row
  voidVisible.value = true
}

function askAllow(row: InvoiceItem) {
  target.value = row
  allowVisible.value = true
}

function confirmVoid(reason: string) {
  if (!target.value) return
  if (reason.length < 2) {
    ElMessage.warning('作廢原因至少 2 個字')
    return
  }
  void run(() => voidInvoice(target.value!.invoiceId, reason), '已作廢')
}

function confirmAllow(reason: string) {
  if (!target.value) return
  if (reason.length < 2) {
    ElMessage.warning('折讓原因至少 2 個字')
    return
  }
  void run(() => allowInvoice(target.value!.invoiceId, reason), '已折讓')
}

const doReissue = (row: InvoiceItem) => run(() => reissueInvoice(row.donationId), '已重新開立')
</script>

<template>
  <div>
    <PageHeader title="發票與收據管理" frontend-unit="結果頁（發票／收據）">
      <template #actions>
        <el-button :loading="exporting" @click="doExport">匯出明細</el-button>
      </template>
    </PageHeader>

    <div class="invoice-list__filters">
      <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="付款日起" end-placeholder="付款日迄" style="max-width: 280px" />
      <el-select v-model="filters.invoiceType" placeholder="類型" clearable style="width: 130px">
        <el-option value="b2c_invoice" label="電子發票" />
        <el-option value="donation_receipt" label="捐贈收據" />
      </el-select>
      <el-select v-model="filters.issueStatus" placeholder="開立狀態" clearable style="width: 130px">
        <el-option value="pending" label="待開立" />
        <el-option value="issued" label="已開立" />
        <el-option value="failed" label="開立失敗" />
      </el-select>
      <el-select v-model="filters.voidStatus" placeholder="作廢或折讓" clearable style="width: 130px">
        <el-option value="none" label="正常" />
        <el-option value="voided" label="已作廢" />
        <el-option value="allowance" label="已折讓" />
      </el-select>
      <el-select v-model="filters.projectId" placeholder="項目" clearable style="width: 160px">
        <el-option v-for="p in projectOptions" :key="p.id" :value="p.id" :label="p.nameZh ?? ''" />
      </el-select>
      <el-input v-model="filters.keyword" placeholder="捐款單號或憑證號碼" clearable style="width: 190px" @keyup.enter="search" />
      <el-button type="primary" @click="search">查詢</el-button>
      <el-button @click="resetFilters">清除條件</el-button>
    </div>
    <p class="invoice-list__hint">這裡不顯示捐款人的姓名與聯絡資料；匯出的明細同樣不含個資，可直接交給會計。作廢只能在開立當期進行，跨期的憑證請改用折讓。</p>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <EmptyState v-else-if="!loading && items.length === 0" text="沒有符合條件的發票或收據" />
    <template v-else>
      <el-table v-if="!isMobile" v-loading="loading" :data="items" style="width: 100%">
        <el-table-column label="捐款單號" min-width="150" prop="orderNo" />
        <el-table-column label="類型" width="90">
          <template #default="{ row }: { row: InvoiceItem }">
            {{ typeLabel(row.invoiceType) }}
            <SemanticTag v-if="row.isAnnualSummary" variant="info">年度彙總</SemanticTag>
          </template>
        </el-table-column>
        <el-table-column label="憑證號碼" min-width="130">
          <template #default="{ row }: { row: InvoiceItem }">{{ row.invoiceNo ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="100">
          <template #default="{ row }: { row: InvoiceItem }">
            <InvoiceStatusTag :issue-status="row.issueStatus" :void-status="row.voidStatus" />
          </template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="金額" width="100" align="right">
          <template #default="{ row }: { row: InvoiceItem }">{{ formatMoney(row.amount) }}</template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="項目" min-width="140">
          <template #default="{ row }: { row: InvoiceItem }">{{ row.projectName ?? '—' }}</template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="開立時間" width="150">
          <template #default="{ row }: { row: InvoiceItem }">{{ formatTaipei(row.issuedAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="220" fixed="right">
          <template #default="{ row }: { row: InvoiceItem }">
            <el-button v-if="canSetNumber(row)" size="small" text type="primary" @click="askNumber(row)">補登號碼</el-button>
            <el-button v-if="canReissue(row)" size="small" text type="primary" :loading="busy" @click="doReissue(row)">重新開立</el-button>
            <el-button v-if="canAllowRow(row)" size="small" text type="primary" @click="askAllow(row)">折讓</el-button>
            <el-button v-if="canVoidRow(row)" size="small" text type="danger" @click="askVoid(row)">作廢</el-button>
            <span v-if="!hasActions(row)" class="invoice-list__muted">—</span>
          </template>
        </el-table-column>
      </el-table>

      <MobileCardList v-else :items="items">
        <template #default="{ item }: { item: InvoiceItem }">
          <div class="invoice-list__card-head">
            <strong>{{ item.orderNo }}</strong>
            <InvoiceStatusTag :issue-status="item.issueStatus" :void-status="item.voidStatus" />
          </div>
          <p class="invoice-list__card-meta">
            {{ typeLabel(item.invoiceType) }}・{{ formatMoney(item.amount) }}・{{ item.invoiceNo ?? '尚未取得號碼' }}<br>
            {{ item.projectName ?? '—' }}
          </p>
          <div v-if="hasActions(item)" class="invoice-list__card-actions">
            <el-button v-if="canSetNumber(item)" size="small" text type="primary" @click="askNumber(item)">補登號碼</el-button>
            <el-button v-if="canReissue(item)" size="small" text type="primary" :loading="busy" @click="doReissue(item)">重新開立</el-button>
            <el-button v-if="canAllowRow(item)" size="small" text type="primary" @click="askAllow(item)">折讓</el-button>
            <el-button v-if="canVoidRow(item)" size="small" text type="danger" @click="askVoid(item)">作廢</el-button>
          </div>
        </template>
      </MobileCardList>
      <el-pagination v-if="totalCount > pageSize" v-model:current-page="page" class="invoice-list__pager" layout="prev, pager, next" :page-size="pageSize" :total="totalCount" />
    </template>

    <el-dialog v-model="numberVisible" title="手動補登憑證號碼" width="460px">
      <p class="invoice-list__hint">用於系統開立失敗、或已經在加值中心後台手動開立的憑證。補登後狀態會變成已開立，並寄出憑證通知信給捐款人。</p>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="憑證號碼" required>
          <el-input v-model="numberForm.invoiceNo" maxlength="32" placeholder="英文字母、數字與連字號，4 到 32 個字" />
        </el-form-item>
        <el-form-item label="開立日期（選填）">
          <el-date-picker v-model="numberForm.issuedOn" type="date" value-format="YYYY-MM-DD" :disabled-date="disableFuture" />
        </el-form-item>
        <el-form-item label="補登原因" required>
          <el-input v-model="numberForm.reason" type="textarea" :rows="3" maxlength="255" show-word-limit />
        </el-form-item>
      </el-form>
      <p class="invoice-list__notice">這個操作會記入操作紀錄。</p>
      <template #footer>
        <el-button @click="numberVisible = false">取消</el-button>
        <el-button type="primary" :loading="busy" @click="doNumber">補登</el-button>
      </template>
    </el-dialog>

    <DangerConfirmDialog
      v-model="voidVisible"
      title="作廢憑證"
      reason-label="作廢原因"
      confirm-text="確認作廢"
      audit-notice="只能作廢開立當期的憑證，跨期的會被擋下，請改用折讓。作廢後不能復原，這個操作會記入操作紀錄。"
      @confirm="confirmVoid"
    >
      確定要作廢「{{ target?.orderNo }}」的{{ target ? typeLabel(target.invoiceType) : '' }}嗎？
    </DangerConfirmDialog>

    <DangerConfirmDialog
      v-model="allowVisible"
      title="折讓憑證"
      reason-label="折讓原因"
      confirm-text="確認折讓"
      audit-notice="折讓是全額折讓，用於已經跨期、不能作廢的憑證。部分金額折讓目前不提供。這個操作會記入操作紀錄。"
      @confirm="confirmAllow"
    >
      確定要為「{{ target?.orderNo }}」的{{ target ? typeLabel(target.invoiceType) : '' }}（{{ target ? formatMoney(target.amount) : '' }}）辦理折讓嗎？
    </DangerConfirmDialog>
  </div>
</template>

<style scoped>
.invoice-list__filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
}

.invoice-list__hint {
  margin: var(--charity-admin-space-2) 0 var(--charity-admin-space-4);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.invoice-list__notice {
  margin: 0;
  font-size: 13px;
  color: var(--charity-warning-text);
}

.invoice-list__muted {
  color: var(--charity-admin-text-tertiary);
}

.invoice-list__pager {
  margin-top: var(--charity-admin-space-4);
  justify-content: center;
}

.invoice-list__card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--charity-admin-space-2);
}

.invoice-list__card-meta {
  margin: var(--charity-admin-space-2) 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.8;
}

.invoice-list__card-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-1);
  margin-top: var(--charity-admin-space-2);
}
</style>
