<script setup lang="ts">
/**
 * 捐款報表（規劃書 §7）：五張固定報表共用同一組篩選條件，數字永遠對得上。
 * 所有報表都不含捐款人的個人資料（要含個資的明細請到「捐款紀錄」匯出，那裡需要額外授權並留下用途備註）。
 * 能看報表不代表能匯出：匯出按鈕只在持有匯出授權時顯示。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import InvoiceStatusTag from '@/components/InvoiceStatusTag.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ErrorState from '@/components/ErrorState.vue'
import DonationStatusTag from '@/components/DonationStatusTag.vue'
import {
  exportReport,
  getByProject,
  getByStore,
  getDetails,
  getInvoiceStatus,
  getOverview,
  type DetailRow,
  type InvoiceStatusSummary,
  type ProjectRow,
  type ReportFilter,
  type ReportName,
  type ReportOverview,
  type StoreRow,
} from '@/api/reports'
import { listProjects, type ProjectListItem } from '@/api/projects'
import { listStores, type StoreListItem } from '@/api/stores'
import { AdminApiError } from '@/api/http'
import { formatMoney, formatRatio, formatTaipei } from '@/utils/format'
import { hasPermission } from '@/auth/session'
import { useBreakpoint } from '@/composables/useBreakpoint'
import type { InvoiceIssueStatus, InvoiceVoidStatus } from '@/types/invoice'
import type { DonationStatus } from '@/api/donations'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const isDesktop = computed(() => breakpoint.value === 'desktop')
const canExport = computed(() => hasPermission('n6.report.export'))

const NO_STORE = '__none__'
const filters = reactive({
  range: null as [string, string] | null,
  storeId: '',
  projectId: '',
  paymentStatus: 'paid',
  invoiceType: '',
  amountMin: null as number | null,
  amountMax: null as number | null,
})
const storeOptions = ref<StoreListItem[]>([])
const projectOptions = ref<ProjectListItem[]>([])

function currentFilter(): ReportFilter {
  return {
    from: filters.range?.[0],
    to: filters.range?.[1],
    storeId: filters.storeId && filters.storeId !== NO_STORE ? filters.storeId : undefined,
    noStore: filters.storeId === NO_STORE,
    projectId: filters.projectId || undefined,
    paymentStatus: filters.paymentStatus,
    invoiceType: filters.invoiceType || undefined,
    amountMin: filters.amountMin ?? undefined,
    amountMax: filters.amountMax ?? undefined,
  }
}

type TabName = 'overview' | 'by-store' | 'by-project' | 'invoice-status' | 'details'
const TAB_REPORT: Record<TabName, ReportName> = {
  overview: 'overview',
  'by-store': 'by-store',
  'by-project': 'by-project',
  'invoice-status': 'invoice-status',
  details: 'details',
}

const activeTab = ref<TabName>('overview')
const granularity = ref<'day' | 'month'>('day')

const overview = ref<ReportOverview | null>(null)
const byStore = ref<StoreRow[] | null>(null)
const byProject = ref<ProjectRow[] | null>(null)
const invoiceStatus = ref<InvoiceStatusSummary | null>(null)
const details = ref<DetailRow[]>([])
const detailsTotal = ref(0)
const detailsPage = ref(1)
const detailsPageSize = 50

const loading = ref(false)
const errors = reactive<Record<TabName, string>>({ overview: '', 'by-store': '', 'by-project': '', 'invoice-status': '', details: '' })
/** 篩選條件改了之後，其他頁籤的資料就過期了：切過去時要重查。 */
const stale = reactive<Record<TabName, boolean>>({ overview: true, 'by-store': true, 'by-project': true, 'invoice-status': true, details: true })

async function loadTab(tab: TabName) {
  loading.value = true
  errors[tab] = ''
  const f = currentFilter()
  try {
    if (tab === 'overview') overview.value = await getOverview(f, granularity.value)
    else if (tab === 'by-store') byStore.value = await getByStore(f)
    else if (tab === 'by-project') byProject.value = await getByProject(f)
    else if (tab === 'invoice-status') invoiceStatus.value = await getInvoiceStatus(f)
    else {
      const r = await getDetails(f, detailsPage.value, detailsPageSize)
      details.value = r.items
      detailsTotal.value = r.totalCount
    }
    stale[tab] = false
  } catch (error) {
    errors[tab] = error instanceof AdminApiError ? error.detail : '查詢時發生問題，請稍後再試一次'
  } finally {
    loading.value = false
  }
}

function search() {
  for (const key of Object.keys(stale) as TabName[]) stale[key] = true
  detailsPage.value = 1
  void loadTab(activeTab.value)
}

function resetFilters() {
  Object.assign(filters, { range: null, storeId: '', projectId: '', paymentStatus: 'paid', invoiceType: '', amountMin: null, amountMax: null })
  search()
}

function onTabChange(name: string | number) {
  const tab = name as TabName
  if (stale[tab]) void loadTab(tab)
}

watch(granularity, () => { void loadTab('overview') })
watch(detailsPage, () => { void loadTab('details') })

onMounted(async () => {
  void loadTab('overview')
  try { storeOptions.value = (await listStores({ page: 1, pageSize: 100 })).items } catch { /* 沒有權限或暫時查不到 */ }
  try { projectOptions.value = await listProjects() } catch { /* 同上 */ }
})

const exporting = ref(false)
async function doExport() {
  exporting.value = true
  try {
    await exportReport(TAB_REPORT[activeTab.value], currentFilter(), activeTab.value === 'overview' ? granularity.value : undefined)
    ElMessage.success('已匯出（內容不含捐款人個資）')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

const trendMax = computed(() => Math.max(1, ...(overview.value?.trend.map((t) => t.amount) ?? [1])))
const barWidth = (amount: number) => `${Math.max(2, Math.round((amount / trendMax.value) * 100))}%`

const INVOICE_TILES: { key: keyof InvoiceStatusSummary; label: string; hint?: string }[] = [
  { key: 'issued', label: '已開立' },
  { key: 'pending', label: '待開立' },
  { key: 'failed', label: '開立失敗', hint: '需要人工處理，請到「發票與收據」重新開立或補登號碼' },
  { key: 'voided', label: '已作廢' },
  { key: 'allowance', label: '已折讓' },
  { key: 'noInvoice', label: '沒有憑證資料' },
]

const SCOPE_TEXT: Record<string, string> = { paid: '只計算付款完成的捐款', refunded: '只計算已退款的捐款', all: '含已退款的捐款（毛額）' }
const statusScope = computed(() => SCOPE_TEXT[filters.paymentStatus] ?? '')
</script>

<template>
  <div>
    <PageHeader title="捐款報表" frontend-unit="（無對應前台頁面，內部報表）">
      <template #actions>
        <el-button v-if="canExport" :loading="exporting" @click="doExport">匯出目前這張報表</el-button>
      </template>
    </PageHeader>

    <div class="report__filters">
      <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="付款日起" end-placeholder="付款日迄" style="max-width: 280px" />
      <el-select v-model="filters.storeId" placeholder="店家" clearable style="width: 160px">
        <el-option :value="NO_STORE" label="沒有店家歸屬" />
        <el-option v-for="s in storeOptions" :key="s.id" :value="s.id" :label="s.nameZh ?? ''" />
      </el-select>
      <el-select v-model="filters.projectId" placeholder="項目" clearable style="width: 160px">
        <el-option v-for="p in projectOptions" :key="p.id" :value="p.id" :label="p.nameZh ?? ''" />
      </el-select>
      <el-select v-model="filters.paymentStatus" style="width: 170px" aria-label="捐款狀態範圍">
        <el-option value="paid" label="付款完成的捐款" />
        <el-option value="refunded" label="已退款的捐款" />
        <el-option value="all" label="含已退款（毛額）" />
      </el-select>
      <el-select v-model="filters.invoiceType" placeholder="發票或收據" clearable style="width: 140px">
        <el-option value="b2c_invoice" label="電子發票" />
        <el-option value="donation_receipt" label="捐贈收據" />
      </el-select>
      <el-input-number v-model="filters.amountMin" :min="0" :controls="false" placeholder="最低金額" :value-on-clear="null" style="width: 110px" />
      <el-input-number v-model="filters.amountMax" :min="0" :controls="false" placeholder="最高金額" :value-on-clear="null" style="width: 110px" />
      <el-button type="primary" @click="search">查詢</el-button>
      <el-button @click="resetFilters">清除條件</el-button>
    </div>
    <p class="report__hint">篩選條件在五張報表之間共用。期間是依「付款時間」計算；報表都不含捐款人的個人資料。目前範圍：{{ statusScope }}。</p>

    <el-tabs v-model="activeTab" @tab-change="onTabChange">
      <!-- 總覽 -->
      <el-tab-pane label="捐款總覽" name="overview">
        <ErrorState v-if="errors.overview" :text="errors.overview" @retry="loadTab('overview')" />
        <div v-else v-loading="loading && activeTab === 'overview'">
          <template v-if="overview">
            <div class="report__tiles">
              <div class="report__tile"><div class="report__tile-label">捐款筆數</div><div class="report__tile-value">{{ overview.donationCount.toLocaleString('zh-Hant-TW') }}</div></div>
              <div class="report__tile"><div class="report__tile-label">捐款總額</div><div class="report__tile-value">{{ formatMoney(overview.totalAmount) }}</div></div>
              <div class="report__tile"><div class="report__tile-label">平均每筆</div><div class="report__tile-value">{{ formatMoney(overview.averageAmount) }}</div></div>
              <div class="report__tile">
                <div class="report__tile-label">付款轉換率</div>
                <div class="report__tile-value">{{ formatRatio(overview.conversionRate) }}</div>
                <div class="report__tile-sub">建立 {{ overview.createdCount }} 筆，其中 {{ overview.convertedCount }} 筆付款成功</div>
              </div>
            </div>

            <div class="report__section-head">
              <h3 class="report__title">捐款趨勢</h3>
              <el-radio-group v-model="granularity" size="small" aria-label="趨勢單位">
                <el-radio-button value="day">依日</el-radio-button>
                <el-radio-button value="month">依月</el-radio-button>
              </el-radio-group>
            </div>
            <EmptyState v-if="overview.trend.length === 0" text="這個條件下沒有捐款" />
            <el-table v-else :data="overview.trend" size="small" max-height="460">
              <el-table-column :label="granularity === 'day' ? '日期' : '月份'" prop="period" width="120" />
              <el-table-column label="筆數" prop="count" width="80" align="right" />
              <el-table-column label="金額" width="120" align="right">
                <template #default="{ row }">{{ formatMoney(row.amount) }}</template>
              </el-table-column>
              <el-table-column v-if="!isMobile" label="與最高的比較" min-width="160">
                <template #default="{ row }"><div class="report__bar" :style="{ width: barWidth(row.amount) }" /></template>
              </el-table-column>
            </el-table>
          </template>
        </div>
      </el-tab-pane>

      <!-- 依店家 -->
      <el-tab-pane label="依店家" name="by-store" lazy>
        <ErrorState v-if="errors['by-store']" :text="errors['by-store']" @retry="loadTab('by-store')" />
        <EmptyState v-else-if="!loading && byStore && byStore.length === 0" text="這個條件下沒有捐款" />
        <el-table v-else-if="!isMobile" v-loading="loading && activeTab === 'by-store'" :data="byStore ?? []" style="width: 100%">
          <el-table-column label="店家" min-width="160">
            <template #default="{ row }: { row: StoreRow }">{{ row.storeName ?? '沒有店家歸屬' }}</template>
          </el-table-column>
          <el-table-column label="筆數" prop="donationCount" width="80" align="right" />
          <el-table-column label="金額" width="120" align="right">
            <template #default="{ row }: { row: StoreRow }">{{ formatMoney(row.totalAmount) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="佔比" width="80" align="right">
            <template #default="{ row }: { row: StoreRow }">{{ formatRatio(row.amountRatio) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="目前分潤比例" width="110" align="right">
            <template #default="{ row }: { row: StoreRow }">{{ row.storeId ? `${row.currentSharePct}%` : '—' }}</template>
          </el-table-column>
          <el-table-column label="應付回饋金" width="120" align="right">
            <template #default="{ row }: { row: StoreRow }">{{ formatMoney(row.payableAmount) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="已結算" width="110" align="right">
            <template #default="{ row }: { row: StoreRow }">{{ formatMoney(row.settledAmount) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="未結算" width="110" align="right">
            <template #default="{ row }: { row: StoreRow }">{{ formatMoney(row.unsettledAmount) }}</template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :items="byStore ?? []">
          <template #default="{ item }: { item: StoreRow }">
            <strong>{{ item.storeName ?? '沒有店家歸屬' }}</strong>
            <p class="report__card-meta">
              {{ item.donationCount }} 筆・{{ formatMoney(item.totalAmount) }}（佔 {{ formatRatio(item.amountRatio) }}）<br>
              應付回饋金 {{ formatMoney(item.payableAmount) }}（已結算 {{ formatMoney(item.settledAmount) }}・未結算 {{ formatMoney(item.unsettledAmount) }}）
            </p>
          </template>
        </MobileCardList>
        <p class="report__hint">「已結算」是指已進入「已結算」或「已付款」結算單的金額；分潤比例是目前的設定，每筆捐款實際用的是捐款當時記下來的比例。</p>
      </el-tab-pane>

      <!-- 依項目 -->
      <el-tab-pane label="依項目" name="by-project" lazy>
        <ErrorState v-if="errors['by-project']" :text="errors['by-project']" @retry="loadTab('by-project')" />
        <EmptyState v-else-if="!loading && byProject && byProject.length === 0" text="這個條件下沒有捐款" />
        <el-table v-else-if="!isMobile" v-loading="loading && activeTab === 'by-project'" :data="byProject ?? []" style="width: 100%">
          <el-table-column label="項目" min-width="180" prop="projectName" />
          <el-table-column label="筆數" prop="donationCount" width="80" align="right" />
          <el-table-column label="金額" width="120" align="right">
            <template #default="{ row }: { row: ProjectRow }">{{ formatMoney(row.totalAmount) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="佔比" width="80" align="right">
            <template #default="{ row }: { row: ProjectRow }">{{ formatRatio(row.amountRatio) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="平均每筆" width="110" align="right">
            <template #default="{ row }: { row: ProjectRow }">{{ formatMoney(row.averageAmount) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="目前分潤比例" width="110" align="right">
            <template #default="{ row }: { row: ProjectRow }">{{ row.currentSharePct }}%</template>
          </el-table-column>
          <el-table-column label="應撥付" width="120" align="right">
            <template #default="{ row }: { row: ProjectRow }">{{ formatMoney(row.payableAmount) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="已結算" width="110" align="right">
            <template #default="{ row }: { row: ProjectRow }">{{ formatMoney(row.settledAmount) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="未結算" width="110" align="right">
            <template #default="{ row }: { row: ProjectRow }">{{ formatMoney(row.unsettledAmount) }}</template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :items="byProject ?? []">
          <template #default="{ item }: { item: ProjectRow }">
            <strong>{{ item.projectName }}</strong>
            <p class="report__card-meta">
              {{ item.donationCount }} 筆・{{ formatMoney(item.totalAmount) }}（佔 {{ formatRatio(item.amountRatio) }}）<br>
              應撥付 {{ formatMoney(item.payableAmount) }}（已結算 {{ formatMoney(item.settledAmount) }}・未結算 {{ formatMoney(item.unsettledAmount) }}）
            </p>
          </template>
        </MobileCardList>
      </el-tab-pane>

      <!-- 憑證狀況 -->
      <el-tab-pane label="發票與收據狀況" name="invoice-status" lazy>
        <ErrorState v-if="errors['invoice-status']" :text="errors['invoice-status']" @retry="loadTab('invoice-status')" />
        <div v-else v-loading="loading && activeTab === 'invoice-status'">
          <div v-if="invoiceStatus" class="report__tiles">
            <div v-for="tile in INVOICE_TILES" :key="tile.key" class="report__tile">
              <div class="report__tile-label">{{ tile.label }}</div>
              <div class="report__tile-value">{{ invoiceStatus[tile.key].toLocaleString('zh-Hant-TW') }}</div>
              <div v-if="tile.hint && invoiceStatus[tile.key] > 0" class="report__tile-sub report__tile-sub--warn">{{ tile.hint }}</div>
            </div>
          </div>
        </div>
      </el-tab-pane>

      <!-- 逐筆 -->
      <el-tab-pane label="逐筆明細" name="details" lazy>
        <ErrorState v-if="errors.details" :text="errors.details" @retry="loadTab('details')" />
        <EmptyState v-else-if="!loading && details.length === 0" text="這個條件下沒有捐款" />
        <template v-else>
          <el-table v-if="!isMobile" v-loading="loading && activeTab === 'details'" :data="details" style="width: 100%">
            <el-table-column label="捐款單號" min-width="150" prop="orderNo" />
            <el-table-column label="付款時間" width="140">
              <template #default="{ row }: { row: DetailRow }">{{ formatTaipei(row.paidAt) }}</template>
            </el-table-column>
            <el-table-column label="金額" width="100" align="right">
              <template #default="{ row }: { row: DetailRow }">{{ formatMoney(row.amount) }}</template>
            </el-table-column>
            <el-table-column label="狀態" width="90">
              <template #default="{ row }: { row: DetailRow }"><DonationStatusTag :status="row.status as DonationStatus" /></template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="項目" min-width="120" prop="projectName" />
            <el-table-column v-if="isDesktop" label="來源店家" min-width="100">
              <template #default="{ row }: { row: DetailRow }">{{ row.storeName ?? '沒有店家歸屬' }}</template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="具名或匿名" width="90">
              <template #default="{ row }: { row: DetailRow }">{{ row.isAnonymous ? '匿名' : '具名' }}</template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="店家" width="80" align="right">
              <template #default="{ row }: { row: DetailRow }">{{ formatMoney(row.storeAmount) }}</template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="撥付對象" width="90" align="right">
              <template #default="{ row }: { row: DetailRow }">{{ formatMoney(row.projectAmount) }}</template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="協會留存" width="90" align="right">
              <template #default="{ row }: { row: DetailRow }">{{ formatMoney(row.associationAmount) }}</template>
            </el-table-column>
            <el-table-column v-if="isDesktop" label="發票或收據" width="110">
              <template #default="{ row }: { row: DetailRow }">
                <InvoiceStatusTag
                  v-if="row.invoiceStatus"
                  :issue-status="row.invoiceStatus as InvoiceIssueStatus"
                  :void-status="(row.invoiceVoidStatus ?? 'none') as InvoiceVoidStatus"
                />
                <SemanticTag v-else variant="neutral">無</SemanticTag>
              </template>
            </el-table-column>
          </el-table>
          <MobileCardList v-else :items="details">
            <template #default="{ item }: { item: DetailRow }">
              <div class="report__card-head">
                <strong>{{ item.orderNo }}</strong>
                <DonationStatusTag :status="item.status as DonationStatus" />
              </div>
              <p class="report__card-meta">
                {{ formatMoney(item.amount) }}・{{ item.projectName }}・{{ item.storeName ?? '沒有店家歸屬' }}<br>
                {{ formatTaipei(item.paidAt) }}・{{ item.isAnonymous ? '匿名' : '具名' }}
              </p>
            </template>
          </MobileCardList>
          <el-pagination v-if="detailsTotal > detailsPageSize" v-model:current-page="detailsPage" class="report__pager" layout="prev, pager, next" :page-size="detailsPageSize" :total="detailsTotal" />
          <p class="report__hint">這份明細不含捐款人姓名與聯絡資料。</p>
        </template>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.report__filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
}

.report__hint {
  margin: var(--charity-admin-space-2) 0 var(--charity-admin-space-3);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.report__tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: var(--charity-admin-space-3);
  margin-bottom: var(--charity-admin-space-4);
}

.report__tile {
  background: var(--charity-admin-bg-surface);
  border: 1px solid var(--charity-admin-border);
  border-radius: 6px;
  padding: var(--charity-admin-space-3) var(--charity-admin-space-4);
}

.report__tile-label {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.report__tile-value {
  font-size: 22px;
  font-weight: 600;
  margin-top: var(--charity-admin-space-1);
}

.report__tile-sub {
  margin-top: var(--charity-admin-space-1);
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.6;
}

.report__tile-sub--warn {
  color: var(--charity-warning-text);
}

.report__section-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin: var(--charity-admin-space-4) 0 var(--charity-admin-space-2);
}

.report__title {
  margin: 0;
  font-size: 15px;
}

.report__bar {
  height: 10px;
  background: var(--charity-info-text);
  border-radius: 2px;
}

.report__card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--charity-admin-space-2);
}

.report__card-meta {
  margin: var(--charity-admin-space-2) 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.8;
}

.report__pager {
  margin-top: var(--charity-admin-space-4);
  justify-content: center;
}
</style>
