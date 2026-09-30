<script setup lang="ts">
/**
 * 診斷回報：App 自動回報的崩潰、異常退出、連線錯誤、啟動耗時與使用者回報。
 * 🔴 回報內容不含個資（自由文字中的 Email 與 8 位以上數字入庫前已遮蔽），保存 90 天；清單只給摘要，技術細節要點進去看。
 * 🔴 「無崩潰裝置比例」是近似值（只算有帶裝置識別的回報）；連線錯誤只彙總次數，正式錯誤率由伺服器監控提供。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { getDiagnostic, getDiagnosticSummary, listDiagnostics, setDiagnosticStatus, type DiagnosticDto, type DiagnosticSummaryDto } from '@/api/adminApp'
import { formatDateTime } from '@/utils/dateTime'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canUpdate = usePermission('app.diagnostic.update')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const TYPES = [
  { value: 'crash', label: '崩潰' }, { value: 'abnormal_exit', label: '異常退出' }, { value: 'api_error', label: '連線錯誤' },
  { value: 'startup_time', label: '啟動耗時' }, { value: 'user_report', label: '使用者回報' },
]
const STATUSES = [
  { value: 'new', label: '新回報' }, { value: 'reviewing', label: '處理中' }, { value: 'resolved', label: '已解決' }, { value: 'ignored', label: '略過' },
]
const statusTag = (s: string) => (s === 'new' ? 'danger' : s === 'reviewing' ? 'warning' : s === 'resolved' ? 'success' : 'info')

// ── 摘要 ──
const days = ref(7)
const summary = ref<DiagnosticSummaryDto | null>(null)
const summaryError = ref<string | null>(null)
async function loadSummary() {
  summaryError.value = null
  try {
    summary.value = await getDiagnosticSummary(days.value)
  } catch (e) {
    summary.value = null
    summaryError.value = errText(e, '摘要載入失敗')
  }
}

// ── 清單 ──
const filters = reactive({ type: '', status: '', platform: '', appVersion: '' })
const rows = ref<DiagnosticDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    const r = await listDiagnostics({ type: filters.type || undefined, status: filters.status || undefined, platform: filters.platform || undefined, appVersion: filters.appVersion.trim() || undefined, page: page.value, pageSize: pageSize.value })
    rows.value = r.items
    total.value = r.totalCount
  } catch (e) {
    rows.value = []
    total.value = 0
    loadError.value = errText(e, '診斷回報載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  load()
}
function clearFilter() {
  Object.assign(filters, { type: '', status: '', platform: '', appVersion: '' })
  applyFilter()
}
onMounted(() => {
  load()
  loadSummary()
})

// ── 詳情與處理狀態 ──
const visible = ref(false)
const current = ref<DiagnosticDto | null>(null)
const detailText = ref<string | null>(null)
const detailLoading = ref(false)
const detailError = ref<string | null>(null)
const newStatus = ref('new')
const updating = ref(false)
async function openDetail(row: DiagnosticDto) {
  visible.value = true
  current.value = row
  newStatus.value = row.status
  detailText.value = null
  detailError.value = null
  detailLoading.value = true
  try {
    const r = await getDiagnostic(row.id)
    current.value = r.report
    detailText.value = r.detail
  } catch (e) {
    detailError.value = errText(e, '技術細節載入失敗')
  } finally {
    detailLoading.value = false
  }
}
async function updateStatus() {
  if (!current.value) return
  updating.value = true
  try {
    await setDiagnosticStatus(current.value.id, newStatus.value)
    ElMessage.success('已更新處理狀態')
    visible.value = false
    load()
    loadSummary()
  } catch (e) {
    ElMessage.error(errText(e, '更新失敗，請稍後再試'))
  } finally {
    updating.value = false
  }
}
</script>

<template>
  <div>
    <el-card shadow="never" class="dg__block">
      <div class="dg__row">
        <strong>彙總</strong>
        <el-select v-model="days" class="dg__sel" @change="loadSummary"><el-option label="近 7 天" :value="7" /><el-option label="近 30 天" :value="30" /><el-option label="近 90 天" :value="90" /></el-select>
        <span class="dg__muted">啟動耗時中位數 {{ summary?.startupMedianMs ?? '—' }} 毫秒・第 90 百分位 {{ summary?.startupP90Ms ?? '—' }} 毫秒</span>
      </div>
      <el-alert v-if="summaryError" type="warning" show-icon :closable="false" :title="summaryError" class="dg__mt" />
      <template v-if="summary">
        <el-table v-if="summary.byVersion.length > 0" :data="summary.byVersion" size="small" class="dg__mt" :max-height="260">
          <el-table-column label="平台" width="100"><template #default="{ row }">{{ row.platform === 'ios' ? 'iPhone' : 'Android' }}</template></el-table-column>
          <el-table-column prop="appVersion" label="版本" width="100" />
          <el-table-column prop="crashes" label="崩潰次數" width="100" />
          <el-table-column prop="activeDevices" label="活躍裝置" width="100" />
          <el-table-column prop="devicesWithCrash" label="崩潰過的裝置" width="120" />
          <el-table-column label="無崩潰裝置比例（近似）" min-width="160"><template #default="{ row }">{{ row.crashFreeDevicePercent != null ? `${row.crashFreeDevicePercent}%` : '—' }}</template></el-table-column>
        </el-table>
        <p v-else class="dg__muted dg__mt">這段期間沒有崩潰回報。</p>
        <p v-if="summary.apiErrorNote" class="dg__muted">{{ summary.apiErrorNote }}</p>
      </template>
    </el-card>

    <el-card shadow="never" class="dg__block">
      <div class="dg__row">
        <el-select v-model="filters.type" placeholder="類型" clearable class="dg__sel" @change="applyFilter"><el-option v-for="t in TYPES" :key="t.value" :label="t.label" :value="t.value" /></el-select>
        <el-select v-model="filters.status" placeholder="處理狀態" clearable class="dg__sel" @change="applyFilter"><el-option v-for="s in STATUSES" :key="s.value" :label="s.label" :value="s.value" /></el-select>
        <el-select v-model="filters.platform" placeholder="平台" clearable class="dg__sel" @change="applyFilter"><el-option label="iPhone（iOS）" value="ios" /><el-option label="Android" value="android" /></el-select>
        <el-input v-model="filters.appVersion" placeholder="App 版本" clearable class="dg__sel" @keyup.enter="applyFilter" @clear="applyFilter" />
        <el-button type="primary" @click="applyFilter">篩選</el-button>
        <el-button @click="clearFilter">清除</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="沒有符合條件的診斷回報" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id" @row-click="openDetail">
          <el-table-column label="時間（台灣時間）" width="160"><template #default="{ row }">{{ formatDateTime(row.occurredAt) }}</template></el-table-column>
          <el-table-column label="類型" width="110" prop="reportTypeLabel" />
          <el-table-column label="平台與版本" width="170"><template #default="{ row }">{{ row.platformLabel }}・{{ row.appVersion || '—' }}<div class="dg__muted">系統 {{ row.osVersion || '—' }}</div></template></el-table-column>
          <el-table-column label="摘要" min-width="240"><template #default="{ row }">{{ row.summary || '（沒有摘要）' }}<span v-if="row.metricValue != null" class="dg__muted">&emsp;數值 {{ row.metricValue }}</span></template></el-table-column>
          <el-table-column label="處理狀態" width="110"><template #default="{ row }"><el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.summary || row.reportTypeLabel }}</template>
          <template #meta="{ row }"><el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag><span>{{ row.reportTypeLabel }}</span><span>{{ row.platformLabel }} {{ row.appVersion }}</span><span>{{ formatDateTime(row.occurredAt) }}</span></template>
          <template #actions="{ row }"><el-button size="small" text type="primary" @click="openDetail(row)">查看</el-button></template>
        </MobileCardList>
        <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="dg__pager" @current-change="load" @size-change="applyFilter" />
      </template>
    </el-card>

    <el-dialog v-model="visible" title="診斷回報詳情" width="640px" :fullscreen="isMobile">
      <template v-if="current">
        <el-descriptions :column="1" border>
          <el-descriptions-item label="時間（台灣時間）">{{ formatDateTime(current.occurredAt) }}</el-descriptions-item>
          <el-descriptions-item label="類型">{{ current.reportTypeLabel }}</el-descriptions-item>
          <el-descriptions-item label="平台與版本">{{ current.platformLabel }}・{{ current.appVersion || '—' }}<template v-if="current.buildNumber">（建置 {{ current.buildNumber }}）</template>・系統 {{ current.osVersion || '—' }}</el-descriptions-item>
          <el-descriptions-item label="摘要">{{ current.summary || '（沒有摘要）' }}</el-descriptions-item>
        </el-descriptions>
        <h4 class="dg__h">技術細節</h4>
        <el-skeleton v-if="detailLoading" :rows="3" animated />
        <el-alert v-else-if="detailError" type="warning" show-icon :closable="false" :title="detailError" />
        <pre v-else class="dg__pre">{{ detailText || '（沒有技術細節）' }}</pre>
        <div v-if="canUpdate" class="dg__status">
          <span>處理狀態</span>
          <el-select v-model="newStatus" class="dg__sel"><el-option v-for="s in STATUSES" :key="s.value" :label="s.label" :value="s.value" /></el-select>
          <el-button type="primary" :loading="updating" :disabled="newStatus === current.status" @click="updateStatus">更新</el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.dg__block { margin-bottom: 12px; }
.dg__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.dg__sel { width: 150px; max-width: 100%; }
.dg__mt { margin-top: 10px; }
.dg__muted { font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.dg__pager { margin-top: 12px; justify-content: flex-end; }
.dg__h { margin: 16px 0 8px; }
.dg__pre { margin: 0; padding: 10px; max-height: 260px; overflow: auto; font-size: 12px; line-height: 1.6; white-space: pre-wrap; word-break: break-all; background: var(--admin-bg-surface-2); border: 1px solid var(--admin-border); border-radius: 6px; }
.dg__status { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-top: 14px; }
:deep(.el-table__row) { cursor: pointer; }
</style>
