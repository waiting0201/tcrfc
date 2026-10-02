<script setup lang="ts">
/**
 * 回饋金結算（規劃書 §6.4、§8）：接真 API。
 * 流程：產生結算單（草稿）→ 核對明細（必要時重算）→ 確認結算（金額鎖定）→ 匯款後登記已付款。
 * 店家與捐款項目分開結算，一份結算單只有一個對象，各自一份對帳單。
 * 🔴 實際匯款在系統外進行，這裡只登記「日期、方式、備註」。
 * 登記已付款是獨立授權，用意是讓「核對的人」和「登記付款的人」可以是不同人。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import SettlementStatusTag from '@/components/SettlementStatusTag.vue'
import DangerConfirmDialog from '@/components/DangerConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import {
  deleteSettlementDraft,
  exportSettlement,
  getSettlement,
  listSettlements,
  markSettlementPaid,
  recalculateSettlement,
  runSettlements,
  settleSettlement,
  type PayeeType,
  type RecalculationResult,
  type RunSettlementResult,
  type SettlementDetail,
  type SettlementItem,
} from '@/api/settlements'
import { listProjects, type ProjectListItem } from '@/api/projects'
import { listStores, type StoreListItem } from '@/api/stores'
import { AdminApiError } from '@/api/http'
import { formatMoney, formatMoneySigned, formatTaipei, todayTaipei } from '@/utils/format'
import { hasPermission } from '@/auth/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const isDesktop = computed(() => breakpoint.value === 'desktop')

const canExecute = computed(() => hasPermission('n4.settlement.execute'))
const canMarkPaid = computed(() => hasPermission('n4.settlement.mark_paid'))
const canExport = computed(() => hasPermission('n4.settlement.export'))

const PAYEE_LABEL: Record<PayeeType, string> = { store: '店家', project: '捐款項目' }
const payeeLabel = (t: PayeeType) => PAYEE_LABEL[t]

// ── 列表 ───────────────────────────────────────────────────────────────
const filters = reactive({ status: '', payeeType: '', range: null as [string, string] | null })
const items = ref<SettlementItem[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const result = await listSettlements(
      { status: filters.status || undefined, payeeType: filters.payeeType || undefined, from: filters.range?.[0], to: filters.range?.[1] },
      page.value,
      pageSize,
    )
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
  Object.assign(filters, { status: '', payeeType: '', range: null })
  search()
}

watch(page, () => { void load() })
onMounted(load)

// ── 產生結算單 ─────────────────────────────────────────────────────────
const runVisible = ref(false)
const runForm = reactive({ range: null as [string, string] | null, payeeType: '' as '' | PayeeType, payeeId: '' })
const runBusy = ref(false)
const runResult = ref<RunSettlementResult | null>(null)
const runError = ref('')
const storeOptions = ref<StoreListItem[]>([])
const projectOptions = ref<ProjectListItem[]>([])

// 結算期間的結束日必須早於今天（當天的捐款還在進來）。
const disableRangeDate = (d: Date) => d.getTime() >= Date.parse(`${todayTaipei()}T00:00:00+08:00`)

const payeeChoices = computed(() => {
  if (runForm.payeeType === 'store') return storeOptions.value.map((s) => ({ id: s.id, name: s.nameZh ?? '' }))
  if (runForm.payeeType === 'project') return projectOptions.value.map((p) => ({ id: p.id, name: p.nameZh ?? '' }))
  return []
})

async function openRun() {
  Object.assign(runForm, { range: null, payeeType: '', payeeId: '' })
  runResult.value = null
  runError.value = ''
  runVisible.value = true
  // 指定單一對象用的清單；沒有檢視權限（403）就只能一次產生全部，不影響主流程。
  if (storeOptions.value.length === 0) {
    try { storeOptions.value = (await listStores({ page: 1, pageSize: 100 })).items } catch { /* 沒有權限或暫時查不到 */ }
  }
  if (projectOptions.value.length === 0) {
    try { projectOptions.value = await listProjects() } catch { /* 同上 */ }
  }
}

watch(() => runForm.payeeType, () => { runForm.payeeId = '' })

async function doRun() {
  if (!runForm.range || runBusy.value) return
  runBusy.value = true
  runError.value = ''
  try {
    runResult.value = await runSettlements({
      periodStart: runForm.range[0],
      periodEnd: runForm.range[1],
      payeeType: runForm.payeeType || undefined,
      payeeId: runForm.payeeId || undefined,
    })
    await load()
  } catch (error) {
    runError.value = error instanceof AdminApiError ? error.detail : '產生結算單失敗，請稍後再試'
  } finally {
    runBusy.value = false
  }
}

// ── 詳情 ───────────────────────────────────────────────────────────────
const detailVisible = ref(false)
const detail = ref<SettlementDetail | null>(null)
const detailLoading = ref(false)
const detailError = ref('')
const actionBusy = ref(false)
const recalcNotice = ref<RecalculationResult | null>(null)
let lastId = ''

async function openDetail(id: string) {
  lastId = id
  detailVisible.value = true
  detail.value = null
  detailError.value = ''
  recalcNotice.value = null
  detailLoading.value = true
  try {
    detail.value = await getSettlement(id)
  } catch (error) {
    detailError.value = error instanceof AdminApiError ? error.detail : '讀取結算單時發生問題'
  } finally {
    detailLoading.value = false
  }
}

const current = computed(() => detail.value?.settlement ?? null)
const positiveLines = computed(() => detail.value?.lines.filter((l) => !l.isClawback) ?? [])
const clawbackLines = computed(() => detail.value?.lines.filter((l) => l.isClawback) ?? [])
const isPending = computed(() => current.value?.status === 'pending')
const isSettled = computed(() => current.value?.status === 'settled')
const isPaid = computed(() => current.value?.status === 'paid')

async function runAction(action: () => Promise<void>) {
  if (actionBusy.value) return
  actionBusy.value = true
  try {
    await action()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '操作失敗，請稍後再試')
  } finally {
    actionBusy.value = false
  }
}

const doRecalculate = () =>
  runAction(async () => {
    if (!current.value) return
    const result = await recalculateSettlement(current.value.id)
    detail.value = result.detail
    recalcNotice.value = result
    ElMessage.success('已重新計算')
    await load()
  })

async function doSettle() {
  if (!current.value) return
  try {
    await ElMessageBox.confirm(
      `確認後金額就會鎖定，不能再新增明細（之後若有捐款退款，只會從這份扣除）。系統會先重算一次再確認。確定要確認結算「${current.value.payeeName ?? ''}」嗎？`,
      '確認結算',
      { confirmButtonText: '確認結算', cancelButtonText: '先不要', type: 'warning' },
    )
  } catch {
    return
  }
  await runAction(async () => {
    detail.value = await settleSettlement(current.value!.id)
    ElMessage.success('已確認結算')
    await load()
  })
}

const deleteVisible = ref(false)
const doDelete = () =>
  runAction(async () => {
    if (!current.value) return
    await deleteSettlementDraft(current.value.id)
    ElMessage.success('已刪除草稿，裡面的捐款可以重新結算')
    detailVisible.value = false
    await load()
  })

const doExport = () =>
  runAction(async () => {
    if (!current.value) return
    const s = current.value
    await exportSettlement(s.id, `settlement-${s.periodStart}-${s.periodEnd}`)
  })

// ── 登記已付款 ─────────────────────────────────────────────────────────
const paidVisible = ref(false)
const paidForm = reactive({ remittedOn: '', remitMethod: '', remitNote: '' })
const disableFuture = (d: Date) => d.getTime() > Date.now()

function openPaid() {
  Object.assign(paidForm, { remittedOn: todayTaipei(), remitMethod: '', remitNote: '' })
  paidVisible.value = true
}

async function doMarkPaid() {
  if (!current.value) return
  if (!paidForm.remittedOn || !paidForm.remitMethod.trim()) {
    ElMessage.warning('請填寫匯款日期與匯款方式')
    return
  }
  await runAction(async () => {
    detail.value = await markSettlementPaid(current.value!.id, {
      remittedOn: paidForm.remittedOn,
      remitMethod: paidForm.remitMethod.trim(),
      remitNote: paidForm.remitNote.trim() || null,
    })
    paidVisible.value = false
    ElMessage.success('已登記為已付款')
    await load()
  })
}

function periodText(s: SettlementItem): string {
  return `${s.periodStart} ～ ${s.periodEnd}`
}
</script>

<template>
  <div>
    <PageHeader title="回饋金結算" frontend-unit="（無對應前台頁面，內部帳務作業）">
      <template #actions>
        <el-button v-if="canExecute" type="primary" @click="openRun">產生結算單</el-button>
      </template>
    </PageHeader>

    <div class="settlement__filters">
      <el-select v-model="filters.status" placeholder="狀態" clearable style="width: 130px">
        <el-option value="pending" label="待結算" />
        <el-option value="settled" label="已結算" />
        <el-option value="paid" label="已付款" />
      </el-select>
      <el-select v-model="filters.payeeType" placeholder="對象類型" clearable style="width: 140px">
        <el-option value="store" label="店家" />
        <el-option value="project" label="捐款項目" />
      </el-select>
      <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="期間起" end-placeholder="期間迄" style="max-width: 280px" />
      <el-button type="primary" @click="search">查詢</el-button>
      <el-button @click="resetFilters">清除條件</el-button>
    </div>
    <p class="settlement__hint">店家回饋金與項目撥付金分開結算，每個對象各一份對帳單。待結算是草稿，確認後金額鎖定；匯款在系統外進行，這裡只登記匯款日期與方式。</p>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <EmptyState v-else-if="!loading && items.length === 0" text="沒有符合條件的結算單" />
    <template v-else>
      <el-table v-if="!isMobile" v-loading="loading" :data="items" style="width: 100%" @row-click="(row: SettlementItem) => openDetail(row.id)">
        <el-table-column label="對象" min-width="180">
          <template #default="{ row }: { row: SettlementItem }">
            <el-link type="primary" :underline="false">{{ row.payeeName ?? '（已刪除）' }}</el-link>
            <span class="settlement__muted">&nbsp;&nbsp;{{ payeeLabel(row.payeeType) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="結算期間" min-width="190">
          <template #default="{ row }: { row: SettlementItem }">{{ periodText(row) }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="100">
          <template #default="{ row }: { row: SettlementItem }"><SettlementStatusTag :status="row.status" /></template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="捐款" width="150" align="right">
          <template #default="{ row }: { row: SettlementItem }">{{ row.donationCount }} 筆・{{ formatMoney(row.donationTotal) }}</template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="退款沖回" width="140" align="right">
          <template #default="{ row }: { row: SettlementItem }">
            <span v-if="row.clawbackCount > 0">{{ row.clawbackCount }} 筆・{{ formatMoneySigned(row.clawbackAmount) }}</span>
            <span v-else class="settlement__muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="應付金額" width="130" align="right">
          <template #default="{ row }: { row: SettlementItem }">
            <strong :class="{ 'settlement__negative': row.payableAmount < 0 }">{{ formatMoneySigned(row.payableAmount) }}</strong>
          </template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="匯款日期" width="120">
          <template #default="{ row }: { row: SettlementItem }">{{ row.remittedOn ?? '—' }}</template>
        </el-table-column>
      </el-table>

      <MobileCardList v-else :items="items">
        <template #default="{ item }: { item: SettlementItem }">
          <div class="settlement__card" @click="openDetail(item.id)">
            <div class="settlement__card-head">
              <strong>{{ item.payeeName ?? '（已刪除）' }}</strong>
              <SettlementStatusTag :status="item.status" />
            </div>
            <p class="settlement__card-meta">
              {{ payeeLabel(item.payeeType) }}・{{ periodText(item) }}<br>
              應付 {{ formatMoneySigned(item.payableAmount) }}（{{ item.donationCount }} 筆捐款）
            </p>
          </div>
        </template>
      </MobileCardList>
      <el-pagination v-if="totalCount > pageSize" v-model:current-page="page" class="settlement__pager" layout="prev, pager, next" :page-size="pageSize" :total="totalCount" />
    </template>

    <!-- 詳情 -->
    <el-drawer v-model="detailVisible" title="結算單" :size="isMobile ? '100%' : '720px'">
      <ErrorState v-if="detailError" :text="detailError" @retry="openDetail(lastId)" />
      <div v-else-if="detailLoading || !current" class="settlement__loading"><el-icon class="is-loading" :size="24"><Loading /></el-icon></div>
      <div v-else>
        <div class="settlement__detail-head">
          <div>
            <strong>{{ current.payeeName ?? '（已刪除）' }}</strong>
            <span class="settlement__muted">&nbsp;&nbsp;{{ payeeLabel(current.payeeType) }}</span>
          </div>
          <SettlementStatusTag :status="current.status" />
        </div>

        <el-alert
          v-if="current.payableAmount < 0"
          type="warning"
          :closable="false"
          show-icon
          title="這份結算單的應付金額是負數：先前已付款的捐款後來退款，沖回的金額大於這一期的應付。請與對象確認後續如何處理，再決定是否登記付款。"
          class="settlement__alert"
        />
        <el-alert
          v-if="recalcNotice"
          type="success"
          :closable="true"
          show-icon
          class="settlement__alert"
          :title="`重算完成：剔除 ${recalcNotice.removedOrderNos.length} 筆已退款的捐款，新增 ${recalcNotice.addedCount} 筆捐款、${recalcNotice.addedClawbackCount} 筆沖回`"
        >
          <p v-if="recalcNotice.removedOrderNos.length > 0" class="settlement__notice-list">剔除的捐款單：{{ recalcNotice.removedOrderNos.join('、') }}</p>
        </el-alert>

        <el-descriptions :column="2" border size="small">
          <el-descriptions-item label="結算期間" :span="2">{{ periodText(current) }}</el-descriptions-item>
          <el-descriptions-item label="捐款筆數">{{ current.donationCount }} 筆</el-descriptions-item>
          <el-descriptions-item label="捐款總額">{{ formatMoney(current.donationTotal) }}</el-descriptions-item>
          <el-descriptions-item label="退款沖回">{{ current.clawbackCount }} 筆・{{ formatMoneySigned(current.clawbackAmount) }}</el-descriptions-item>
          <el-descriptions-item label="應付金額"><strong>{{ formatMoneySigned(current.payableAmount) }}</strong></el-descriptions-item>
          <el-descriptions-item label="產生時間" :span="2">{{ formatTaipei(current.createdAt) }}</el-descriptions-item>
          <el-descriptions-item v-if="current.settledAt" label="確認結算" :span="2">{{ formatTaipei(current.settledAt) }}（{{ current.settledByName ?? '—' }}）</el-descriptions-item>
          <template v-if="isPaid">
            <el-descriptions-item label="匯款日期">{{ current.remittedOn ?? '—' }}</el-descriptions-item>
            <el-descriptions-item label="匯款方式">{{ current.remitMethod ?? '—' }}</el-descriptions-item>
            <el-descriptions-item label="備註" :span="2">{{ current.remitNote ?? '—' }}</el-descriptions-item>
            <el-descriptions-item label="登記人" :span="2">{{ current.paidRegisteredByName ?? '—' }}（{{ formatTaipei(current.paidRegisteredAt) }}）</el-descriptions-item>
          </template>
        </el-descriptions>

        <h3 class="settlement__title">捐款明細（{{ positiveLines.length }} 筆）</h3>
        <p class="settlement__hint">分潤比例是捐款成立當時記下來的，之後調整設定不會影響已經產生的結算單。</p>
        <el-table v-if="!isMobile" :data="positiveLines" size="small" max-height="320" empty-text="沒有明細">
          <el-table-column label="捐款單" min-width="160" prop="orderNo" />
          <el-table-column label="付款時間" min-width="130">
            <template #default="{ row }">{{ formatTaipei(row.paidAt) }}</template>
          </el-table-column>
          <el-table-column label="捐款金額" width="100" align="right">
            <template #default="{ row }">{{ formatMoney(row.donationAmount) }}</template>
          </el-table-column>
          <el-table-column label="比例" width="70" align="right">
            <template #default="{ row }">{{ row.sharePct }}%</template>
          </el-table-column>
          <el-table-column label="應付" width="100" align="right">
            <template #default="{ row }">{{ formatMoney(row.shareAmount) }}</template>
          </el-table-column>
        </el-table>
        <ul v-else class="settlement__lines">
          <li v-for="l in positiveLines" :key="l.lineId">
            <strong>{{ l.orderNo }}</strong>
            <span class="settlement__muted">{{ formatTaipei(l.paidAt) }}</span>
            <span>{{ formatMoney(l.donationAmount) }} × {{ l.sharePct }}% ＝ {{ formatMoney(l.shareAmount) }}</span>
          </li>
        </ul>

        <template v-if="clawbackLines.length > 0">
          <h3 class="settlement__title">退款沖回（{{ clawbackLines.length }} 筆）</h3>
          <p class="settlement__hint">上一期已經付款的捐款後來退款了，這一期從應付金額中扣回；同一筆只會沖回一次。</p>
          <el-table v-if="!isMobile" :data="clawbackLines" size="small" max-height="280">
            <el-table-column label="捐款單" min-width="160" prop="orderNo" />
            <el-table-column label="沖回金額" width="110" align="right">
              <template #default="{ row }"><span class="settlement__negative">{{ formatMoneySigned(row.shareAmount) }}</span></template>
            </el-table-column>
            <el-table-column label="原因" min-width="220">
              <template #default="{ row }">{{ row.clawbackReason ?? '—' }}</template>
            </el-table-column>
          </el-table>
          <ul v-else class="settlement__lines">
            <li v-for="l in clawbackLines" :key="l.lineId">
              <strong>{{ l.orderNo }}</strong>
              <span class="settlement__negative">{{ formatMoneySigned(l.shareAmount) }}</span>
              <span class="settlement__muted">{{ l.clawbackReason ?? '—' }}</span>
            </li>
          </ul>
        </template>

        <div class="settlement__actions">
          <el-button v-if="canExecute && (isPending || isSettled)" :loading="actionBusy" @click="doRecalculate">重新計算</el-button>
          <el-button v-if="canExecute && isPending" type="primary" :loading="actionBusy" @click="doSettle">確認結算</el-button>
          <el-button v-if="canMarkPaid && isSettled" type="primary" :loading="actionBusy" @click="openPaid">登記已付款</el-button>
          <el-button v-if="canExport" :loading="actionBusy" @click="doExport">匯出對帳單</el-button>
          <el-button v-if="canExecute && isPending" type="danger" plain :loading="actionBusy" @click="deleteVisible = true">刪除草稿</el-button>
        </div>
        <p v-if="isSettled" class="settlement__hint">
          已結算：之後如果有捐款退款，「重新計算」只會從這份扣除，不會新增明細。已付款的結算單是帳務紀錄，不能修改或刪除。
        </p>
        <p v-if="isSettled && !canMarkPaid" class="settlement__hint">登記已付款需要另外的授權，你的角色沒有這項權限。</p>
      </div>
    </el-drawer>

    <!-- 產生結算單 -->
    <el-dialog v-model="runVisible" title="產生結算單" width="520px">
      <template v-if="!runResult">
        <p class="settlement__hint">選擇結算期間（結束日必須早於今天）。系統會為期間內「有應付金額」的每個對象各產生一份待結算的草稿；已經進過結算單的捐款不會重複計入。</p>
        <el-form label-position="top">
          <el-form-item label="結算期間" required>
            <el-date-picker v-model="runForm.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="開始日期" end-placeholder="結束日期" :disabled-date="disableRangeDate" style="width: 100%" />
          </el-form-item>
          <el-form-item label="結算對象">
            <el-select v-model="runForm.payeeType" style="width: 100%">
              <el-option value="" label="店家與捐款項目都結算" />
              <el-option value="store" label="只結算店家" />
              <el-option value="project" label="只結算捐款項目" />
            </el-select>
          </el-form-item>
          <el-form-item v-if="runForm.payeeType" label="指定單一對象（不選＝全部）">
            <el-select v-model="runForm.payeeId" clearable filterable style="width: 100%" placeholder="全部">
              <el-option v-for="c in payeeChoices" :key="c.id" :value="c.id" :label="c.name" />
            </el-select>
          </el-form-item>
        </el-form>
        <el-alert v-if="runError" type="error" :closable="false" show-icon :title="runError" />
      </template>
      <template v-else>
        <el-alert
          :type="runResult.created.length > 0 ? 'success' : 'info'"
          :closable="false"
          show-icon
          :title="runResult.created.length > 0 ? `已產生 ${runResult.created.length} 份結算單草稿` : '這次沒有產生任何結算單'"
        />
        <ul v-if="runResult.created.length > 0" class="settlement__result-list">
          <li v-for="c in runResult.created" :key="c.id">
            <el-link type="primary" :underline="false" @click="runVisible = false; openDetail(c.id)">{{ c.payeeName }}</el-link>
            <span class="settlement__muted">&nbsp;&nbsp;{{ payeeLabel(c.payeeType) }}・應付 {{ formatMoneySigned(c.payableAmount) }}</span>
          </li>
        </ul>
        <template v-if="runResult.skipped.length > 0">
          <p class="settlement__title">略過的對象（{{ runResult.skipped.length }}）</p>
          <el-table :data="runResult.skipped" size="small" max-height="220">
            <el-table-column label="對象" min-width="140">
              <template #default="{ row }">{{ row.payeeName ?? '—' }}&nbsp;&nbsp;<span class="settlement__muted">{{ payeeLabel(row.payeeType as PayeeType) }}</span></template>
            </el-table-column>
            <el-table-column label="原因" min-width="220" prop="reason" />
          </el-table>
        </template>
      </template>
      <template #footer>
        <el-button @click="runVisible = false">{{ runResult ? '關閉' : '取消' }}</el-button>
        <el-button v-if="!runResult" type="primary" :loading="runBusy" :disabled="!runForm.range" @click="doRun">產生</el-button>
      </template>
    </el-dialog>

    <!-- 登記已付款 -->
    <el-dialog v-model="paidVisible" title="登記已付款" width="460px">
      <p class="settlement__hint">實際匯款請在系統外完成，這裡只登記匯款資訊。登記後這份結算單就鎖定，不能再修改。</p>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="匯款日期" required>
          <el-date-picker v-model="paidForm.remittedOn" type="date" value-format="YYYY-MM-DD" :disabled-date="disableFuture" :clearable="false" />
        </el-form-item>
        <el-form-item label="匯款方式" required>
          <el-input v-model="paidForm.remitMethod" maxlength="32" show-word-limit placeholder="例如：銀行轉帳" />
        </el-form-item>
        <el-form-item label="備註（選填）">
          <el-input v-model="paidForm.remitNote" type="textarea" :rows="3" maxlength="255" show-word-limit />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="paidVisible = false">取消</el-button>
        <el-button type="primary" :loading="actionBusy" @click="doMarkPaid">登記</el-button>
      </template>
    </el-dialog>

    <DangerConfirmDialog
      v-model="deleteVisible"
      title="刪除結算草稿"
      :require-reason="false"
      confirm-text="確認刪除"
      audit-notice="這個操作會記入操作紀錄。"
      @confirm="doDelete"
    >
      只有待結算的草稿可以刪除，刪除後裡面的捐款可以重新結算。已結算與已付款的結算單是帳務紀錄，不能刪除。確定要刪除這份草稿嗎？
    </DangerConfirmDialog>
  </div>
</template>

<style scoped>
.settlement__filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
}

.settlement__hint {
  margin: var(--charity-admin-space-2) 0 var(--charity-admin-space-4);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.settlement__muted {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.settlement__negative {
  color: var(--charity-danger-text);
}

.settlement__pager {
  margin-top: var(--charity-admin-space-4);
  justify-content: center;
}

.settlement__loading {
  display: flex;
  justify-content: center;
  padding: var(--charity-admin-space-8);
}

.settlement__detail-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--charity-admin-space-3);
}

.settlement__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.settlement__notice-list {
  margin: var(--charity-admin-space-1) 0 0;
  font-size: 13px;
  overflow-wrap: anywhere;
}

.settlement__title {
  font-size: 14px;
  margin: var(--charity-admin-space-5) 0 var(--charity-admin-space-1);
}

.settlement__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  margin-top: var(--charity-admin-space-5);
  padding-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
}

.settlement__card {
  cursor: pointer;
}

.settlement__card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--charity-admin-space-2);
}

.settlement__card-meta {
  margin: var(--charity-admin-space-2) 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.8;
}

.settlement__lines {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-2);
  font-size: 13px;
}

.settlement__lines li {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding-bottom: var(--charity-admin-space-2);
  border-bottom: 1px solid var(--charity-admin-border);
  overflow-wrap: anywhere;
}

.settlement__result-list {
  margin: var(--charity-admin-space-3) 0 0;
  padding-left: 20px;
  line-height: 2;
}
</style>
