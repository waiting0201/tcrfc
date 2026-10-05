<script setup lang="ts">
/**
 * 商店報表（現金基礎口徑）：
 * - 營收＝付款成立時間落在期間內的訂單總額（含運費，不論之後是否取消）
 * - 退款＝退款執行時間落在期間內的金額；淨營收＝營收－退款
 * - 客單價＝營收÷訂單數；退貨率＝期間內有退款的訂單數÷訂單數；庫存是即時值
 * 兩隊分帳表只列出你有授權的俱樂部，供線下分帳參考——系統不計算應付金額、不產生結算單。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { exportReport, getReportBySellingClub, getReportSummary, type ReportBySellingClubDto, type ReportSummaryDto } from '@/api/adminShop'
import { formatMoney } from '@/utils/formatMoney'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canExport = usePermission('shop.report.export')
const club = computed(() => activeClubId.value)

const range = ref<[string, string] | null>(null)
const summary = ref<ReportSummaryDto | null>(null)
const byClub = ref<ReportBySellingClubDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const [from, to] = range.value ?? [undefined, undefined]
    const [s, b] = await Promise.all([getReportSummary(club.value, from, to), getReportBySellingClub(club.value, from, to)])
    summary.value = s
    byClub.value = b
    if (!range.value) range.value = [s.from, s.to]
  } catch (error) {
    summary.value = null
    byClub.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '報表載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, () => {
  range.value = null
  load()
})

const cards = computed(() => {
  const s = summary.value
  if (!s) return []
  return [
    { label: '營收', value: formatMoney(s.grossRevenue) },
    { label: '退款', value: formatMoney(s.refundedAmount) },
    { label: '淨營收', value: formatMoney(s.netRevenue) },
    { label: '訂單數', value: `${s.orderCount} 張` },
    { label: '客單價', value: formatMoney(s.averageOrderValue) },
    { label: '退貨率', value: `${s.returnRatePercent}%（${s.returnOrderCount} 張）` },
    { label: '庫存偏低的規格', value: `${s.lowStockCount} 個` },
    { label: '已缺貨的規格', value: `${s.outOfStockCount} 個` },
    { label: '目前可售總量', value: `${s.totalAvailableQuantity} 件` },
  ]
})

const exportOpen = ref(false)
const exportKind = ref<'summary' | 'by-selling-club'>('summary')
const exporting = ref(false)
async function doExport(purpose: string) {
  exporting.value = true
  try {
    await exportReport(club.value, exportKind.value, range.value?.[0], range.value?.[1], purpose)
    exportOpen.value = false
    ElMessage.success('已匯出，檔案已下載')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}
</script>

<template>
  <div class="report">
    <el-card shadow="never" class="report__block">
      <div class="report__row">
        <el-date-picker v-model="range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="起日" end-placeholder="迄日" :clearable="false" class="report__range" />
        <el-button type="primary" @click="load">查詢</el-button>
        <span class="report__spacer" />
        <el-dropdown v-if="canExport" trigger="click" @command="(c: 'summary' | 'by-selling-club') => { exportKind = c; exportOpen = true }">
          <el-button>匯出報表<el-icon class="el-icon--right"><ArrowDown /></el-icon></el-button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="summary">營收與商品摘要</el-dropdown-item>
              <el-dropdown-item command="by-selling-club">兩隊分帳加總</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>
      <p class="report__hint">預設最近 30 天，期間最長 800 天。營收以付款成立時間計算（含運費，之後取消的訂單仍計入，直到退款執行）；退款以退款執行時間計算；庫存為即時值。</p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <template v-else-if="summary">
      <div class="report__cards">
        <el-card v-for="c in cards" :key="c.label" shadow="never" class="report__card">
          <div class="report__card-label">{{ c.label }}</div>
          <div class="report__card-value">{{ c.value }}</div>
        </el-card>
      </div>

      <el-card shadow="never" header="熱銷規格（前 10 名，依數量）" class="report__block">
        <el-empty v-if="summary.topSkus.length === 0" description="這段期間沒有銷售" :image-size="56" />
        <el-table v-else-if="!isMobile" :data="summary.topSkus" row-key="sku">
          <el-table-column label="商品規格編號" min-width="120" prop="sku" />
          <el-table-column label="商品" min-width="160" prop="productName" />
          <el-table-column label="尺寸／顏色" min-width="100"><template #default="{ row }">{{ row.variantLabel || '—' }}</template></el-table-column>
          <el-table-column label="數量" width="80" prop="quantity" />
          <el-table-column label="營收" width="120"><template #default="{ row }">{{ formatMoney(row.revenue) }}</template></el-table-column>
        </el-table>
        <MobileCardList v-else :rows="summary.topSkus" row-key="sku">
          <template #title="{ row }">{{ row.productName }} {{ row.variantLabel }}</template>
          <template #meta="{ row }"><span>{{ row.sku }}</span><span>{{ row.quantity }} 件</span><span>{{ formatMoney(row.revenue) }}</span></template>
          <template #actions><span /></template>
        </MobileCardList>
      </el-card>

      <el-card shadow="never" header="兩隊分帳加總（供線下分帳參考）" class="report__block">
        <p class="report__hint">只列出你有授權的俱樂部。系統不計算應付金額、不產生結算單，兩隊分潤走線下合約。</p>
        <el-empty v-if="byClub.length === 0" description="沒有資料" :image-size="56" />
        <el-table v-else-if="!isMobile" :data="byClub" row-key="sellingClubId">
          <el-table-column label="販售俱樂部" min-width="140" prop="sellingClubName" />
          <el-table-column label="訂單數" width="80" prop="orderCount" />
          <el-table-column label="營收" width="120"><template #default="{ row }">{{ formatMoney(row.grossRevenue) }}</template></el-table-column>
          <el-table-column label="退款" width="120"><template #default="{ row }">{{ formatMoney(row.refundedAmount) }}</template></el-table-column>
          <el-table-column label="淨營收" width="120"><template #default="{ row }">{{ formatMoney(row.netRevenue) }}</template></el-table-column>
          <el-table-column label="已結算" width="120"><template #default="{ row }">{{ formatMoney(row.settledAmount) }}</template></el-table-column>
          <el-table-column label="待結算" width="120"><template #default="{ row }">{{ formatMoney(row.pendingSettlementAmount) }}</template></el-table-column>
        </el-table>
        <MobileCardList v-else :rows="byClub" row-key="sellingClubId">
          <template #title="{ row }">{{ row.sellingClubName }}</template>
          <template #meta="{ row }"><span>{{ row.orderCount }} 張</span><span>營收 {{ formatMoney(row.grossRevenue) }}</span><span>淨營收 {{ formatMoney(row.netRevenue) }}</span><span>待結算 {{ formatMoney(row.pendingSettlementAmount) }}</span></template>
          <template #actions><span /></template>
        </MobileCardList>
      </el-card>
    </template>

    <ExportPurposeDialog v-model="exportOpen" title="匯出商店報表" description="報表含營收與訂單彙總資料，範圍為目前選擇的期間。" :loading="exporting" @confirm="doExport" />
  </div>
</template>

<style scoped>
.report__block { margin-bottom: 16px; }
.report__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.report__range { max-width: 100%; }
.report__spacer { flex: 1; }
.report__hint { margin: 8px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.report__cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 12px; margin-bottom: 16px; }
.report__card { --el-card-padding: 14px; min-width: 0; }
.report__card-label { font-size: 12px; color: var(--admin-text-secondary); margin-bottom: 4px; }
.report__card-value { font-size: 18px; font-weight: 600; word-break: break-word; }
</style>
