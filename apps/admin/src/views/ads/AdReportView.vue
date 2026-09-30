<script setup lang="ts">
/**
 * 廣告成效報表：曝光、點擊、點擊率與不重複裝置數。
 * 🔴 廣告不分俱樂部：兩隊共用 App，報表涵蓋全部檔期。
 * 🔴 沒有任何個人層級資料（不列出裝置或會員）。曝光與點擊由 App 量測（可見面積達一半且連續 1 秒才算曝光；備援素材不計；贊助商標誌牆不計）。
 * 🔴 「不重複裝置數」跨多日彙總時，是「每天不重複裝置數的加總」（裝置日），不是期間內真正的不重複人數。
 * 🔴 日期一律是台灣日期。匯出須填用途；沒有 PDF 匯出，需要紙本請用「列印」。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import {
  exportAdReport,
  getAdReport,
  listAdSlots,
  listCampaigns,
  runAdMaintenance,
  type AdMaintenanceResultDto,
  type AdReportDto,
  type AdSlotDto,
  type CampaignListItemDto,
  type ReportGroupBy,
} from '@/api/adminAds'
import { dateOnlyToPickerDate, pickerDateToDateOnly, taipeiToday } from '@/utils/dateTime'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canView = usePermission('ad.report.view')
const canExport = usePermission('ad.report.export')
const canMaintain = usePermission('ad.maintenance.run')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const GROUP_OPTIONS: { value: ReportGroupBy; label: string; column: string }[] = [
  { value: 'campaign', label: '依檔期', column: '檔期' },
  { value: 'slot', label: '依版位', column: '版位' },
  { value: 'creative', label: '依素材', column: '素材' },
  { value: 'platform', label: '依手機平台', column: '手機平台' },
  { value: 'locale', label: '依語言', column: '語言' },
  { value: 'date', label: '依日期', column: '日期' },
]

function defaultRange(): [string, string] {
  const today = taipeiToday()
  const start = dateOnlyToPickerDate(today)!
  start.setDate(start.getDate() - 29)
  return [pickerDateToDateOnly(start), today]
}
const filters = reactive({
  range: defaultRange() as [string, string] | null,
  groupBy: 'campaign' as ReportGroupBy,
  campaignId: '', slotId: '', platform: '', locale: '',
})
const campaigns = ref<CampaignListItemDto[]>([])
const slots = ref<AdSlotDto[]>([])
const report = ref<AdReportDto | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const params = () => ({
  from: filters.range?.[0], to: filters.range?.[1], groupBy: filters.groupBy,
  campaignId: filters.campaignId || undefined, slotId: filters.slotId || undefined,
  platform: filters.platform || undefined, locale: filters.locale || undefined,
})
async function load() {
  if (!canView.value) return void (loading.value = false)
  if (!filters.range) return void ElMessage.warning('請選擇報表期間')
  loading.value = true
  error.value = null
  try {
    report.value = await getAdReport(params())
  } catch (e) {
    report.value = null
    error.value = errText(e, '報表載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(async () => {
  load()
  campaigns.value = await listCampaigns().catch(() => [])
  slots.value = await listAdSlots().catch(() => [])
})
const groupColumn = computed(() => GROUP_OPTIONS.find((o) => o.value === (report.value?.groupBy ?? filters.groupBy))?.column ?? '項目')
const printPage = () => window.print()
const fmt = (n: number) => n.toLocaleString()

// ── 匯出 ──
const exportVisible = ref(false)
const exporting = ref(false)
async function doExport(purpose: string) {
  exporting.value = true
  try {
    await exportAdReport({ ...params(), purpose })
    exportVisible.value = false
    ElMessage.success('已匯出')
  } catch (e) {
    ElMessage.error(errText(e, '匯出失敗，請稍後再試'))
  } finally {
    exporting.value = false
  }
}

// ── 維護作業（僅系統管理員）──
const maintaining = ref(false)
const maintenance = ref<AdMaintenanceResultDto | null>(null)
async function doMaintenance() {
  maintaining.value = true
  try {
    maintenance.value = await runAdMaintenance()
    ElMessage.success('已執行完成')
    load()
  } catch (e) {
    ElMessage.error(errText(e, '執行失敗，請稍後再試'))
  } finally {
    maintaining.value = false
  }
}
</script>

<template>
  <div class="rep">
    <PageHeader title="成效報表">
      <template #meta><FrontendUnitBanner module-code="E6" /></template>
    </PageHeader>
    <el-alert class="rep__block" type="info" show-icon :closable="false" title="兩隊共用：這份報表涵蓋 App 的全部廣告檔期，不分俱樂部。日期為台灣日期。報表沒有任何個人層級資料，看不到是哪一位使用者看了或點了廣告。" />

    <el-card v-if="!canView" shadow="never"><el-empty description="你的帳號沒有檢視成效報表的權限" /></el-card>
    <template v-else>
      <el-card shadow="never" class="rep__block rep__no-print">
        <div class="rep__row">
          <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="開始日" end-placeholder="結束日" :clearable="false" class="rep__range" />
          <el-select v-model="filters.groupBy" class="rep__select"><el-option v-for="o in GROUP_OPTIONS" :key="o.value" :label="o.label" :value="o.value" /></el-select>
          <el-select v-model="filters.campaignId" placeholder="全部檔期" clearable filterable class="rep__select"><el-option v-for="c in campaigns" :key="c.id" :label="c.name" :value="c.id" /></el-select>
          <el-select v-model="filters.slotId" placeholder="全部版位" clearable class="rep__select"><el-option v-for="s in slots" :key="s.id" :label="s.nameZh || s.slotCode" :value="s.id" /></el-select>
          <el-select v-model="filters.platform" placeholder="全部平台" clearable class="rep__select"><el-option label="iPhone" value="ios" /><el-option label="Android" value="android" /></el-select>
          <el-select v-model="filters.locale" placeholder="全部語言" clearable class="rep__select"><el-option label="中文" value="zh" /><el-option label="英文" value="en" /></el-select>
          <el-button type="primary" @click="load">查詢</el-button>
          <span class="rep__spacer" />
          <el-button @click="printPage">列印</el-button>
          <el-button v-if="canExport" @click="exportVisible = true">匯出報表</el-button>
          <el-button v-if="canMaintain" :loading="maintaining" @click="doMaintenance">立即整理廣告資料</el-button>
        </div>
        <p class="rep__hint">期間最長 366 天。「立即整理廣告資料」會推進檔期狀態、彙整尚未計入的原始事件並清除舊資料；平常系統每分鐘自動處理，只有報表顯示還有未計入的事件時才需要手動執行。</p>
      </el-card>

      <el-alert v-if="maintenance" class="rep__block rep__no-print" :type="maintenance.overdueUnaggregated > 0 ? 'warning' : 'success'" show-icon :closable="false" :title="`已整理：開始 ${maintenance.campaignsStarted} 個檔期、結束 ${maintenance.campaignsEnded} 個、彙整 ${maintenance.eventsAggregated} 筆事件、清除舊事件 ${maintenance.eventsPurged} 筆`">
        <template v-if="maintenance.overdueUnaggregated > 0">仍有 {{ maintenance.overdueUnaggregated }} 筆事件超過 2 天沒有彙整成功（或已超過 90 天無法重算），請通知維運人員檢查。</template>
      </el-alert>

      <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
      <el-card v-else-if="error" shadow="never"><el-empty :description="error"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
      <template v-else-if="report">
        <el-alert v-if="report.pendingEvents > 0" class="rep__block" type="warning" show-icon :closable="false" :title="`期間內還有 ${fmt(report.pendingEvents)} 筆事件尚未彙整，下面的數字還沒有包含它們。`">
          系統管理員可以按「立即整理廣告資料」處理。
        </el-alert>

        <div class="rep__stats rep__block">
          <el-card shadow="never"><div class="rep__num">{{ fmt(report.total.impressions) }}</div><div class="rep__muted">曝光數</div></el-card>
          <el-card shadow="never"><div class="rep__num">{{ fmt(report.total.clicks) }}</div><div class="rep__muted">點擊數</div></el-card>
          <el-card shadow="never"><div class="rep__num">{{ report.total.ctr }}%</div><div class="rep__muted">點擊率</div></el-card>
          <el-card shadow="never"><div class="rep__num">{{ fmt(report.total.uniqueDevices) }}</div><div class="rep__muted">不重複裝置數（裝置日）</div></el-card>
        </div>

        <el-card shadow="never" class="rep__block" :header="`${report.from} ～ ${report.to}（台灣日期）`">
          <el-empty v-if="report.rows.length === 0" description="這段期間沒有廣告成效資料" :image-size="64" />
          <template v-else>
            <el-table v-if="!isMobile" :data="report.rows" row-key="label" show-summary :summary-method="() => ['合計', fmt(report!.total.impressions), fmt(report!.total.clicks), `${report!.total.ctr}%`, fmt(report!.total.uniqueDevices)]">
              <el-table-column :label="groupColumn" prop="label" min-width="200" />
              <el-table-column label="曝光數" align="right" width="120"><template #default="{ row }">{{ fmt(row.impressions) }}</template></el-table-column>
              <el-table-column label="點擊數" align="right" width="120"><template #default="{ row }">{{ fmt(row.clicks) }}</template></el-table-column>
              <el-table-column label="點擊率" align="right" width="110"><template #default="{ row }">{{ row.ctr }}%</template></el-table-column>
              <el-table-column label="不重複裝置數" align="right" width="140"><template #default="{ row }">{{ fmt(row.uniqueDevices) }}</template></el-table-column>
            </el-table>
            <MobileCardList v-else :rows="report.rows" row-key="label">
              <template #title="{ row }">{{ row.label }}</template>
              <template #meta="{ row }"><span>曝光 {{ fmt(row.impressions) }}</span><span>點擊 {{ fmt(row.clicks) }}</span><span>點擊率 {{ row.ctr }}%</span><span>裝置 {{ fmt(row.uniqueDevices) }}</span></template>
              <template #actions><span /></template>
            </MobileCardList>
          </template>
          <p class="rep__hint">「不重複裝置數」跨多天彙總時，是每一天不重複裝置數的加總（同一支手機連看三天算三），不是期間內真正的不重複人數。</p>
        </el-card>

        <el-card v-if="report.pacing.length > 0" shadow="never" class="rep__block" header="曝光保證檔期的投放進度">
          <ul class="rep__pacing">
            <li v-for="p in report.pacing" :key="p.campaignId">
              <strong>{{ p.campaignName }}</strong>
              <span class="rep__muted">已曝光 {{ fmt(p.pacing.delivered) }} ／ 目標 {{ fmt(p.pacing.goalImpressions) }}，依進度應達 {{ fmt(p.pacing.expectedByNow) }}</span>
              <el-tag :type="p.pacing.status === 'behind' ? 'danger' : p.pacing.status === 'ahead' ? 'success' : 'primary'" size="small">{{ p.pacing.statusLabel }}</el-tag>
            </li>
          </ul>
        </el-card>
      </template>
    </template>

    <ExportPurposeDialog v-model="exportVisible" title="匯出廣告成效報表" description="匯出內容為目前查詢條件下的統計數字（CSV），最後一列是合計。" :loading="exporting" @confirm="doExport" />
  </div>
</template>

<style scoped>
.rep { min-width: 0; }
.rep__block { margin-bottom: 12px; }
.rep__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.rep__range { max-width: 100%; }
.rep__select { width: 140px; max-width: 100%; }
.rep__spacer { flex: 1; }
.rep__stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; }
.rep__num { font-size: 26px; font-weight: 600; color: var(--admin-text-primary); }
.rep__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.rep__hint { margin: 8px 0 0; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.rep__pacing { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
.rep__pacing li { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
@media print { .rep__no-print { display: none; } }
</style>
