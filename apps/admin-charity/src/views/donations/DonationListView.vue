<script setup lang="ts">
/**
 * N3 捐款紀錄（docs/22-charity-ui.md §3.7.3／§3.8）：接真 API。
 * 主列表（伺服器篩選與分頁）＋ 異常處理（四類佇列）＋ 側拉詳情（個資預設遮罩、有權限才能看完整、退款、
 * 重新確認付款、重寄感謝信、補開發票）＋ 含個資的明細匯出。
 * 個資遮罩是 API 層做的（不是前端隱藏）；看完整個資會留下稽核紀錄，所以每次都要使用者明確按下。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import DonationStatusTag from '@/components/DonationStatusTag.vue'
import InvoiceStatusTag from '@/components/InvoiceStatusTag.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import DangerConfirmDialog from '@/components/DangerConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ReconciliationPanel from '@/components/ReconciliationPanel.vue'
import DiscrepancyBadge from '@/components/DiscrepancyBadge.vue'
import {
  countAnomalies,
  exportDonations,
  getDonation,
  listAnomalies,
  listDonations,
  recheckPayment,
  refundDonation,
  setCreditVisibility,
  reissueInvoice,
  resendThanks,
  type Anomaly,
  type AnomalyCounts,
  type AnomalyKind,
  type DonationDetail,
  type DonationFilter,
  type DonationListItem,
} from '@/api/donations'
import { resolveDiscrepancy, type DiscrepancyType } from '@/api/reconciliation'
import { listProjects, type ProjectListItem } from '@/api/projects'
import { listStores, type StoreListItem } from '@/api/stores'
import { AdminApiError } from '@/api/http'
import { formatMoney, formatTaipei } from '@/utils/format'
import { hasPermission } from '@/auth/session'
import { useBreakpoint } from '@/composables/useBreakpoint'
import type { InvoiceIssueStatus, InvoiceVoidStatus } from '@/types/invoice'

const route = useRoute()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度的次要欄位用 v-if 整欄不渲染（el-table 欄位總寬不看 CSS 隱藏，見 README「響應式」）。
const isDesktop = computed(() => breakpoint.value === 'desktop')

const canReveal = computed(() => hasPermission('n3.donation.reveal'))
const canRefund = computed(() => hasPermission('n3.donation.refund'))
const canExport = computed(() => hasPermission('n3.donation.export'))
const canRecheck = computed(() => hasPermission('n3.donation.recheck_payment'))
const canReissue = computed(() => hasPermission('n5.donation_invoice.issue'))
const canHideCredit = computed(() => hasPermission('n3.donation.hide_credit'))

const activeTab = ref(route.query.tab === 'anomaly' ? 'anomaly' : route.query.tab === 'recon' ? 'recon' : 'list')

// ── 篩選與列表 ─────────────────────────────────────────────────────────
const filters = reactive({
  range: null as [string, string] | null,
  status: '',
  projectId: '',
  storeId: '',
  invoiceStatus: '',
  keyword: '',
  amountMin: null as number | null,
  amountMax: null as number | null,
})

const NO_STORE = '__none__'
const projectOptions = ref<ProjectListItem[]>([])
const storeOptions = ref<StoreListItem[]>([])

const items = ref<DonationListItem[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const loadError = ref('')

function currentFilter(): DonationFilter {
  return {
    from: filters.range?.[0],
    to: filters.range?.[1],
    status: filters.status || undefined,
    projectId: filters.projectId || undefined,
    storeId: filters.storeId && filters.storeId !== NO_STORE ? filters.storeId : undefined,
    noStore: filters.storeId === NO_STORE,
    invoiceStatus: filters.invoiceStatus || undefined,
    keyword: filters.keyword.trim() || undefined,
    amountMin: filters.amountMin ?? undefined,
    amountMax: filters.amountMax ?? undefined,
  }
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const result = await listDonations(currentFilter(), page.value, pageSize)
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
  Object.assign(filters, { range: null, status: '', projectId: '', storeId: '', invoiceStatus: '', keyword: '', amountMin: null, amountMax: null })
  search()
}

watch(page, () => { void load() })

onMounted(async () => {
  void load()
  void loadCounts()
  if (activeTab.value === 'anomaly') void loadAnomalies()
  // 篩選用的項目與店家清單：沒有對應檢視權限時（403）就不給選項，不影響主列表。
  try { projectOptions.value = await listProjects() } catch { /* 沒有權限或暫時查不到 */ }
  try { storeOptions.value = (await listStores({ page: 1, pageSize: 100 })).items } catch { /* 同上 */ }
})

// ── 異常處理 ───────────────────────────────────────────────────────────
const counts = ref<AnomalyCounts | null>(null)
const anomalyKind = ref<AnomalyKind>('confirm_failed')
const anomalies = ref<Anomaly[]>([])
const anomalyLoading = ref(false)
const anomalyError = ref('')

const ANOMALY_LABELS: Record<AnomalyKind, string> = {
  confirm_failed: '已扣款但確認失敗',
  invoice_failed: '發票或收據開立失敗',
  invoice_void_pending: '已退款但發票未作廢',
  reconciliation: '對帳差異',
}

function countOf(kind: AnomalyKind): number {
  const c = counts.value
  if (!c) return 0
  return { confirm_failed: c.confirmFailed, invoice_failed: c.invoiceFailed, invoice_void_pending: c.invoiceVoidPending, reconciliation: c.reconciliation }[kind]
}

async function loadCounts() {
  try { counts.value = await countAnomalies() } catch { counts.value = null }
}

async function loadAnomalies() {
  anomalyLoading.value = true
  anomalyError.value = ''
  try {
    anomalies.value = await listAnomalies(anomalyKind.value)
  } catch (error) {
    anomalyError.value = error instanceof AdminApiError ? error.detail : '查詢時發生問題，請稍後再試一次'
  } finally {
    anomalyLoading.value = false
  }
}

watch(anomalyKind, () => { void loadAnomalies() })

// ── 詳情 ───────────────────────────────────────────────────────────────
const detailVisible = ref(false)
const detail = ref<DonationDetail | null>(null)
const detailLoading = ref(false)
const detailError = ref('')
const actionBusy = ref(false)

let lastDetailId = ''
async function openDetail(id: string) {
  lastDetailId = id
  detailVisible.value = true
  detail.value = null
  detailError.value = ''
  detailLoading.value = true
  try {
    detail.value = await getDonation(id)
  } catch (error) {
    detailError.value = error instanceof AdminApiError ? error.detail : '讀取詳情時發生問題'
  } finally {
    detailLoading.value = false
  }
}

async function reveal() {
  if (!detail.value) return
  try {
    detail.value = await getDonation(detail.value.id, true)
    ElMessage.success('已顯示完整個資，這次檢視已記入稽核紀錄')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '無法顯示完整個資')
  }
}

async function refreshAfterAction(updated: DonationDetail | null, message: string) {
  if (updated) detail.value = updated
  ElMessage.success(message)
  await Promise.all([load(), loadCounts(), activeTab.value === 'anomaly' ? loadAnomalies() : Promise.resolve()])
}

async function runAction(action: () => Promise<DonationDetail | null>, message: string) {
  if (actionBusy.value) return
  actionBusy.value = true
  try {
    await refreshAfterAction(await action(), message)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '操作失敗，請稍後再試')
  } finally {
    actionBusy.value = false
  }
}

const doRecheck = () => detail.value && runAction(() => recheckPayment(detail.value!.id), '已重新向金流確認付款結果')
const doReissue = () => detail.value && runAction(() => reissueInvoice(detail.value!.id), '已重新開立')
const doResend = () => detail.value && runAction(async () => {
  const result = await resendThanks(detail.value!.id)
  if (!result.sent) throw new AdminApiError(0, '感謝信沒有寄出，請稍後再試')
  return null
}, '已補寄感謝信')

const refundVisible = ref(false)
function confirmRefund(reason: string) {
  if (!detail.value) return
  if (reason.length < 2) {
    ElMessage.warning('退款原因至少 2 個字')
    return
  }
  void runAction(() => refundDonation(detail.value!.id, reason), '已完成退款')
}

const canRefundThis = computed(() => canRefund.value && detail.value?.status === 'paid')
const canRecheckThis = computed(() => canRecheck.value && detail.value?.needsManualReview)
const canReissueThis = computed(() => canReissue.value && detail.value?.invoice?.issueStatus === 'failed')
const canResendThis = computed(() => canReveal.value && detail.value?.status === 'paid')

// ── 徵信名單：逐筆「不列入」 ───────────────────────────────────────────
// 只有「具名」且「已完成」的捐款才可能出現在公開的徵信名單；匿名的本來就不會列入，所以不提供切換。
const creditBusy = ref(false)
const creditApplicable = computed(() => detail.value !== null && !detail.value.donor.isAnonymous)

async function toggleCreditHidden(hidden: boolean) {
  if (!detail.value || creditBusy.value) return
  creditBusy.value = true
  try {
    const result = await setCreditVisibility(detail.value.id, hidden)
    detail.value.isCreditHidden = result.isCreditHidden
    const row = items.value.find((x) => x.id === result.donationId)
    if (row) row.isCreditHidden = result.isCreditHidden
    ElMessage.success(result.isCreditHidden ? '已設為不列入徵信名單' : '已恢復列入徵信名單')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '設定失敗，請稍後再試')
  } finally {
    creditBusy.value = false
  }
}

// ── 異常處理裡的對帳差異：標記已處理 ──────────────────────────────────
const resolveVisible = ref(false)
const resolveTarget = ref<Anomaly | null>(null)

function askResolve(row: Anomaly) {
  resolveTarget.value = row
  resolveVisible.value = true
}

async function confirmResolve(note: string) {
  const target = resolveTarget.value
  if (!target?.discrepancyId) return
  if (note.length < 2) {
    ElMessage.warning('處理備註至少 2 個字')
    return
  }
  try {
    await resolveDiscrepancy(target.discrepancyId, note)
    ElMessage.success('已標記為已處理')
    await Promise.all([loadAnomalies(), loadCounts()])
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '標記失敗，請稍後再試')
  }
}

// ── 匯出 ───────────────────────────────────────────────────────────────
const exportVisible = ref(false)
const exportPurpose = ref('')
const exporting = ref(false)

async function doExport() {
  const purpose = exportPurpose.value.trim()
  if (purpose.length < 4) {
    ElMessage.warning('請填寫用途備註（至少 4 個字），這會記入稽核紀錄')
    return
  }
  exporting.value = true
  try {
    await exportDonations(currentFilter(), purpose)
    ElMessage.success('已匯出，這次匯出已記入稽核紀錄')
    exportVisible.value = false
    exportPurpose.value = ''
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

function payStatusLabel(s: string): string {
  return ({ requested: '已發起', pending: '處理中', confirmed: '已確認', paid: '已確認', failed: '失敗', refunded: '已退款' } as Record<string, string>)[s] ?? s
}

function invoiceTypeLabel(d: DonationDetail['invoice']): string {
  if (!d) return '—'
  if (d.invoiceType === 'donation_receipt') return '捐贈收據'
  return ({ mobile_carrier: '電子發票（手機條碼載具）', love_code: '電子發票（捐贈）', tax_id: '電子發票（統一編號）' } as Record<string, string>)[d.carrierType ?? ''] ?? '電子發票'
}
</script>

<template>
  <div>
    <PageHeader title="捐款紀錄" frontend-unit="捐款表單／結果頁">
      <template #actions>
        <el-button v-if="canExport && activeTab === 'list'" @click="exportVisible = true">匯出明細</el-button>
      </template>
    </PageHeader>

    <el-tabs v-model="activeTab" @tab-change="(name: string | number) => { if (name === 'anomaly') { void loadAnomalies(); void loadCounts() } }">
      <el-tab-pane label="全部捐款" name="list">
        <div class="donation-list__filters">
          <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="開始日期" end-placeholder="結束日期" style="max-width: 280px" />
          <el-select v-model="filters.status" placeholder="狀態" clearable style="width: 130px">
            <el-option value="paid" label="已完成" />
            <el-option value="pending" label="處理中" />
            <el-option value="created" label="已建立" />
            <el-option value="failed" label="付款失敗" />
            <el-option value="expired" label="已逾時" />
            <el-option value="refunded" label="已退款" />
          </el-select>
          <el-select v-model="filters.projectId" placeholder="項目" clearable style="width: 160px">
            <el-option v-for="p in projectOptions" :key="p.id" :value="p.id" :label="p.nameZh ?? ''" />
          </el-select>
          <el-select v-model="filters.storeId" placeholder="店家" clearable style="width: 160px">
            <el-option :value="NO_STORE" label="沒有店家歸屬" />
            <el-option v-for="s in storeOptions" :key="s.id" :value="s.id" :label="s.nameZh ?? ''" />
          </el-select>
          <el-select v-model="filters.invoiceStatus" placeholder="發票或收據" clearable style="width: 140px">
            <el-option value="pending" label="待開立" />
            <el-option value="issued" label="已開立" />
            <el-option value="failed" label="開立失敗" />
          </el-select>
          <el-input v-model="filters.keyword" placeholder="訂單編號" clearable style="width: 180px" @keyup.enter="search" />
          <el-input-number v-model="filters.amountMin" :min="0" :controls="false" placeholder="最低金額" :value-on-clear="null" style="width: 110px" />
          <el-input-number v-model="filters.amountMax" :min="0" :controls="false" placeholder="最高金額" :value-on-clear="null" style="width: 110px" />
          <el-button type="primary" @click="search">查詢</el-button>
          <el-button @click="resetFilters">清除條件</el-button>
        </div>

        <ErrorState v-if="loadError" :text="loadError" @retry="load" />
        <EmptyState v-else-if="!loading && items.length === 0" text="沒有符合條件的捐款紀錄" />
        <template v-else>
          <el-table v-if="!isMobile" v-loading="loading" :data="items" style="width: 100%" @row-click="(row: DonationListItem) => openDetail(row.id)">
            <el-table-column label="訂單編號" min-width="170">
              <template #default="{ row }: { row: DonationListItem }">
                <el-link type="primary" :underline="false">{{ row.orderNo }}</el-link>
                <SemanticTag v-if="row.needsManualReview" variant="warning" class="donation-list__flag">待人工處理</SemanticTag>
              </template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="建立時間" width="150">
              <template #default="{ row }: { row: DonationListItem }">{{ formatTaipei(row.createdAt) }}</template>
            </el-table-column>
            <el-table-column label="項目" min-width="140" prop="projectName" />
            <el-table-column v-if="isDesktop" label="店家" min-width="120">
              <template #default="{ row }: { row: DonationListItem }">{{ row.storeName ?? '無店家歸屬' }}</template>
            </el-table-column>
            <el-table-column label="金額" width="110" align="right">
              <template #default="{ row }: { row: DonationListItem }">{{ formatMoney(row.amount) }}</template>
            </el-table-column>
            <el-table-column label="狀態" width="100">
              <template #default="{ row }: { row: DonationListItem }"><DonationStatusTag :status="row.status" /></template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="徵信名單" width="110">
              <template #default="{ row }: { row: DonationListItem }">
                <SemanticTag v-if="row.isCreditHidden" variant="neutral">不列入</SemanticTag>
                <span v-else-if="row.isAnonymous" class="donation-list__muted">匿名</span>
                <span v-else class="donation-list__muted">—</span>
              </template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="發票或收據" width="110">
              <template #default="{ row }: { row: DonationListItem }">
                <InvoiceStatusTag
                  v-if="row.invoiceStatus"
                  :issue-status="row.invoiceStatus as InvoiceIssueStatus"
                  :void-status="(row.invoiceVoidStatus ?? 'none') as InvoiceVoidStatus"
                />
                <span v-else>—</span>
              </template>
            </el-table-column>
          </el-table>

          <MobileCardList v-else :items="items">
            <template #default="{ item }: { item: DonationListItem }">
              <div class="donation-list__card" @click="openDetail(item.id)">
                <div class="donation-list__card-head">
                  <strong>{{ item.orderNo }}</strong>
                  <DonationStatusTag :status="item.status" />
                </div>
                <p class="donation-list__card-meta">
                  {{ formatMoney(item.amount) }}・{{ item.projectName }}・{{ item.storeName ?? '無店家歸屬' }}<br>
                  {{ formatTaipei(item.createdAt) }}
                  <SemanticTag v-if="item.needsManualReview" variant="warning">待人工處理</SemanticTag>
                  <SemanticTag v-if="item.isCreditHidden" variant="neutral">不列入徵信名單</SemanticTag>
                </p>
              </div>
            </template>
          </MobileCardList>

          <el-pagination v-if="totalCount > pageSize" v-model:current-page="page" class="donation-list__pager" layout="prev, pager, next" :page-size="pageSize" :total="totalCount" />
        </template>
      </el-tab-pane>

      <el-tab-pane name="anomaly">
        <template #label>
          異常處理
          <el-badge v-if="counts && (counts.confirmFailed + counts.invoiceFailed + counts.invoiceVoidPending + counts.reconciliation) > 0" :value="counts.confirmFailed + counts.invoiceFailed + counts.invoiceVoidPending + counts.reconciliation" class="donation-list__badge" />
        </template>
        <el-radio-group v-model="anomalyKind" class="donation-list__kinds">
          <el-radio-button v-for="(label, kind) in ANOMALY_LABELS" :key="kind" :value="kind">
            {{ label }}（{{ countOf(kind as AnomalyKind) }}）
          </el-radio-button>
        </el-radio-group>
        <p v-if="anomalyKind === 'confirm_failed'" class="donation-list__hint">
          付款可能已成功但本站沒有確認到。請不要讓捐款人重複付款；開啟詳情後按「重新確認付款」向金流確認，成功後會自動走完開票與寄信。
        </p>

        <ErrorState v-if="anomalyError" :text="anomalyError" @retry="loadAnomalies" />
        <EmptyState v-else-if="!anomalyLoading && anomalies.length === 0" text="目前沒有這類異常" />
        <el-table v-else-if="!isMobile" v-loading="anomalyLoading" :data="anomalies" style="width: 100%" @row-click="(row: Anomaly) => row.donationId && openDetail(row.donationId)">
          <el-table-column label="訂單編號" min-width="170">
            <template #default="{ row }: { row: Anomaly }">{{ row.orderNo ?? '—' }}</template>
          </el-table-column>
          <el-table-column label="金額" width="110" align="right">
            <template #default="{ row }: { row: Anomaly }">{{ row.amount !== null ? formatMoney(row.amount) : '—' }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="發生時間" width="150">
            <template #default="{ row }: { row: Anomaly }">{{ formatTaipei(row.occurredAt) }}</template>
          </el-table-column>
          <el-table-column v-if="anomalyKind === 'reconciliation'" label="差異類型" min-width="160">
            <template #default="{ row }: { row: Anomaly }">
              <DiscrepancyBadge v-if="row.discrepancyType" :type="row.discrepancyType as DiscrepancyType" />
              <span v-else>—</span>
            </template>
          </el-table-column>
          <el-table-column v-if="anomalyKind === 'reconciliation'" label="處理狀態" width="110">
            <template #default="{ row }: { row: Anomaly }">{{ row.resolutionStatus === 'resolved' ? '已處理' : '待處理' }}</template>
          </el-table-column>
          <el-table-column v-if="anomalyKind === 'reconciliation' && canRecheck" label="操作" width="130">
            <template #default="{ row }: { row: Anomaly }">
              <el-button v-if="row.resolutionStatus !== 'resolved' && row.discrepancyId" size="small" @click.stop="askResolve(row)">標記已處理</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :items="anomalies">
          <template #default="{ item }: { item: Anomaly }">
            <div class="donation-list__card" @click="item.donationId && openDetail(item.donationId)">
              <div class="donation-list__card-head">
                <strong>{{ item.orderNo ?? '（本站查無對應捐款單）' }}</strong>
                <span v-if="anomalyKind === 'reconciliation'">{{ item.resolutionStatus === 'resolved' ? '已處理' : '待處理' }}</span>
              </div>
              <p class="donation-list__card-meta">
                {{ item.amount !== null ? formatMoney(item.amount) : '—' }}・{{ formatTaipei(item.occurredAt) }}
              </p>
              <DiscrepancyBadge v-if="anomalyKind === 'reconciliation' && item.discrepancyType" :type="item.discrepancyType as DiscrepancyType" />
              <el-button
                v-if="anomalyKind === 'reconciliation' && canRecheck && item.resolutionStatus !== 'resolved' && item.discrepancyId"
                size="small"
                class="donation-list__card-action"
                @click.stop="askResolve(item)"
              >
                標記已處理
              </el-button>
            </div>
          </template>
        </MobileCardList>
        <p v-if="anomalyKind === 'reconciliation'" class="donation-list__hint">
          這些是每日對帳發現的差異。處理完請標記為已處理並留下備註；想看整天的對帳結果、或手動重新對帳，請到「每日對帳」頁籤。
        </p>
      </el-tab-pane>
      <el-tab-pane label="每日對帳" name="recon" lazy>
        <ReconciliationPanel @open-donation="openDetail" @changed="loadCounts" />
      </el-tab-pane>
    </el-tabs>

    <el-drawer v-model="detailVisible" title="捐款詳情" :size="isMobile ? '100%' : '520px'">
      <ErrorState v-if="detailError" :text="detailError" @retry="openDetail(lastDetailId)" />
      <div v-else-if="detailLoading || !detail" class="donation-detail__loading"><el-icon class="is-loading" :size="24"><Loading /></el-icon></div>
      <div v-else class="donation-detail">
        <div class="donation-detail__head">
          <strong>{{ detail.orderNo }}</strong>
          <DonationStatusTag :status="detail.status" />
        </div>
        <el-alert v-if="detail.needsManualReview" type="warning" :closable="false" show-icon title="待人工處理：付款確認結果不明，捐款人可能已扣款。請先重新確認付款，不要讓捐款人重複付款。" class="donation-detail__alert" />

        <el-descriptions :column="1" border size="small">
          <el-descriptions-item label="金額">{{ formatMoney(detail.amount) }}</el-descriptions-item>
          <el-descriptions-item label="項目">{{ detail.project.name }}</el-descriptions-item>
          <el-descriptions-item label="來源店家">{{ detail.store?.name ?? '無店家歸屬' }}</el-descriptions-item>
          <el-descriptions-item label="建立時間">{{ formatTaipei(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="付款時間">{{ formatTaipei(detail.paidAt) }}</el-descriptions-item>
        </el-descriptions>

        <h3 class="donation-detail__title">捐款人</h3>
        <el-descriptions :column="1" border size="small">
          <el-descriptions-item label="姓名">{{ detail.donor.name }}</el-descriptions-item>
          <el-descriptions-item label="Email">{{ detail.donor.email }}</el-descriptions-item>
          <el-descriptions-item label="具名或匿名">{{ detail.donor.isAnonymous ? '匿名' : '具名' }}</el-descriptions-item>
          <el-descriptions-item label="徵信名單">
            <template v-if="!creditApplicable">匿名捐款，不會列入徵信名單</template>
            <template v-else>
              <el-switch
                :model-value="detail.isCreditHidden"
                :disabled="!canHideCredit"
                :loading="creditBusy"
                inline-prompt
                active-text="不列入"
                inactive-text="列入"
                aria-label="不列入公開的徵信名單"
                @change="(v: string | number | boolean) => toggleCreditHidden(Boolean(v))"
              />
              <span class="donation-detail__credit-note">
                {{ detail.isCreditHidden ? '這筆捐款不會出現在公開的徵信名單。' : '捐款完成後會依整站設定列入公開的徵信名單（只顯示姓名）。' }}
              </span>
            </template>
          </el-descriptions-item>
        </el-descriptions>
        <p v-if="creditApplicable && !canHideCredit" class="donation-detail__mask">你的角色沒有調整徵信名單的權限。</p>
        <p v-if="!detail.donor.revealed" class="donation-detail__mask">
          個資已遮罩。
          <el-button v-if="canReveal" size="small" type="primary" link @click="reveal">顯示完整個資（會記入稽核紀錄）</el-button>
          <span v-else>你的角色沒有檢視完整個資的權限。</span>
        </p>

        <h3 class="donation-detail__title">發票或收據</h3>
        <el-descriptions v-if="detail.invoice" :column="1" border size="small">
          <el-descriptions-item label="類型">{{ invoiceTypeLabel(detail.invoice) }}</el-descriptions-item>
          <el-descriptions-item label="狀態">
            <InvoiceStatusTag :issue-status="detail.invoice.issueStatus as InvoiceIssueStatus" :void-status="detail.invoice.voidStatus as InvoiceVoidStatus" />
          </el-descriptions-item>
          <el-descriptions-item v-if="detail.invoice.invoiceNo" label="編號">{{ detail.invoice.invoiceNo }}</el-descriptions-item>
          <el-descriptions-item v-if="detail.invoice.carrierId" label="載具或捐贈碼">{{ detail.invoice.carrierId }}</el-descriptions-item>
          <el-descriptions-item v-if="detail.invoice.taxId" label="統一編號">{{ detail.invoice.taxId }}</el-descriptions-item>
          <el-descriptions-item v-if="detail.invoice.invoiceTitle || detail.invoice.receiptTitle" label="抬頭">{{ detail.invoice.invoiceTitle ?? detail.invoice.receiptTitle }}</el-descriptions-item>
          <el-descriptions-item v-if="detail.invoice.nationalId" label="身分證字號">{{ detail.invoice.nationalId }}</el-descriptions-item>
          <el-descriptions-item v-if="detail.invoice.receiptAddress" label="通訊地址">{{ detail.invoice.receiptAddress }}</el-descriptions-item>
          <el-descriptions-item v-if="detail.invoice.voidReason" label="作廢或折讓原因">{{ detail.invoice.voidReason }}</el-descriptions-item>
        </el-descriptions>
        <p v-else class="donation-detail__mask">尚無發票或收據資料。</p>

        <h3 class="donation-detail__title">分潤（付款成立時的快照）</h3>
        <el-descriptions :column="1" border size="small">
          <el-descriptions-item :label="`店家（${detail.split.storeSharePct}%）`">{{ formatMoney(detail.split.storeAmount) }}</el-descriptions-item>
          <el-descriptions-item :label="`撥付對象（${detail.split.projectSharePct}%）`">{{ formatMoney(detail.split.projectAmount) }}</el-descriptions-item>
          <el-descriptions-item label="協會留存">{{ formatMoney(detail.split.associationAmount) }}</el-descriptions-item>
        </el-descriptions>

        <h3 class="donation-detail__title">付款紀錄</h3>
        <el-table :data="detail.payments" size="small" :show-header="true">
          <el-table-column label="發起時間" min-width="130"><template #default="{ row }">{{ formatTaipei(row.requestedAt) }}</template></el-table-column>
          <el-table-column label="狀態" width="90"><template #default="{ row }">{{ payStatusLabel(row.status) }}</template></el-table-column>
          <el-table-column label="確認時間" min-width="130"><template #default="{ row }">{{ formatTaipei(row.confirmedAt) }}</template></el-table-column>
        </el-table>

        <h3 class="donation-detail__title">處理經過</h3>
        <el-timeline>
          <el-timeline-item v-for="(entry, i) in detail.timeline" :key="i" :timestamp="formatTaipei(entry.at)" placement="top">
            {{ entry.text }}<span v-if="entry.byName">（{{ entry.byName }}）</span>
          </el-timeline-item>
        </el-timeline>

        <div v-if="detail.refund" class="donation-detail__mask">已退款。原因：{{ detail.refund.reason ?? '—' }}（{{ detail.refund.refundedByName ?? '—' }}）</div>

        <div class="donation-detail__actions">
          <el-button v-if="canRecheckThis" type="primary" :loading="actionBusy" @click="doRecheck">重新確認付款</el-button>
          <el-button v-if="canReissueThis" :loading="actionBusy" @click="doReissue">補開發票或收據</el-button>
          <el-button v-if="canResendThis" :loading="actionBusy" @click="doResend">重寄感謝信</el-button>
          <el-button v-if="canRefundThis" type="danger" plain :loading="actionBusy" @click="refundVisible = true">退款</el-button>
        </div>
      </div>
    </el-drawer>

    <DangerConfirmDialog
      v-model="refundVisible"
      title="退款"
      reason-label="退款原因"
      confirm-text="確認退款"
      audit-notice="只能全額退款。會先向金流退款，成功後才改本站狀態，並連動發票或收據作廢與退款通知信；這個操作會記入稽核紀錄。"
      @confirm="confirmRefund"
    >
      確定要退還「{{ detail?.orderNo }}」的 {{ detail ? formatMoney(detail.amount) : '' }} 嗎？
    </DangerConfirmDialog>

    <DangerConfirmDialog
      v-model="resolveVisible"
      title="標記差異已處理"
      reason-label="處理備註"
      confirm-text="標記為已處理"
      audit-notice="請寫下處理結果。標記後無法改回待處理，這個操作會記入操作紀錄。"
      @confirm="confirmResolve"
    >
      這筆對帳差異處理完了嗎？
    </DangerConfirmDialog>

    <el-dialog v-model="exportVisible" title="匯出捐款明細" width="440px">
      <p class="donation-detail__mask">匯出內容包含捐款人個資，依目前篩選條件匯出，單次上限 5 萬筆。這次匯出會記入稽核紀錄。</p>
      <el-form-item label="用途備註（至少 4 個字）" required>
        <el-input v-model="exportPurpose" type="textarea" :rows="2" />
      </el-form-item>
      <template #footer>
        <el-button @click="exportVisible = false">取消</el-button>
        <el-button type="primary" :loading="exporting" @click="doExport">匯出</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.donation-list__filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  margin-bottom: var(--charity-admin-space-4);
}

.donation-list__flag {
  margin-left: var(--charity-admin-space-2);
}

.donation-list__pager {
  margin-top: var(--charity-admin-space-4);
  justify-content: center;
}

.donation-list__kinds {
  margin-bottom: var(--charity-admin-space-3);
  flex-wrap: wrap;
}

.donation-list__hint {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.donation-list__badge {
  margin-left: 6px;
}

.donation-list__card-action {
  margin-top: var(--charity-admin-space-2);
}

.donation-list__muted {
  color: var(--charity-admin-text-tertiary);
}

.donation-detail__credit-note {
  display: block;
  margin-top: var(--charity-admin-space-1);
  font-size: 12px;
  color: var(--charity-admin-text-secondary);
}

.donation-list__card {
  cursor: pointer;
}

.donation-list__card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--charity-admin-space-2);
}

.donation-list__card-meta {
  margin: var(--charity-admin-space-2) 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.7;
}

.donation-detail__loading {
  display: flex;
  justify-content: center;
  padding: var(--charity-admin-space-8);
}

.donation-detail__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--charity-admin-space-3);
}

.donation-detail__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.donation-detail__title {
  font-size: 14px;
  margin: var(--charity-admin-space-5) 0 var(--charity-admin-space-2);
  color: var(--charity-admin-text-primary);
}

.donation-detail__mask {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  margin: var(--charity-admin-space-2) 0;
}

.donation-detail__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  margin-top: var(--charity-admin-space-5);
  padding-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
}
</style>
