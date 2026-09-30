<script setup lang="ts">
/**
 * 推播裝置：安裝了 App 的裝置概況與版本分布，用來判斷「最低支援版本」該設多少、有多少裝置能收到推播。
 * 🔴 兩隊共用 App，不分俱樂部。
 * 🔴 裝置識別碼與推播識別碼視同個資：清單只顯示遮罩後的識別碼；只有「檢視完整識別碼」權限（僅系統管理員）能查看，查看會留下紀錄。
 * 🔴 清單只說裝置有沒有綁定會員，不顯示是哪一位會員；系統也不記錄個人層級的推播開啟資料。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { cleanupInvalidTokens, getDevice, getDeviceStats, listDevices, type DeviceDetailDto, type DeviceDto, type DeviceStatsDto } from '@/api/adminApp'
import { formatDateTime } from '@/utils/dateTime'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canView = usePermission('app.device.view')
const canReveal = usePermission('app.device.reveal')
const canCleanup = usePermission('app.device.update')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

// ── 統計 ──
const stats = ref<DeviceStatsDto | null>(null)
const statsError = ref<string | null>(null)
const below = reactive({ platform: '', version: '' })
const belowCount = ref<number | null>(null)
async function loadStats() {
  statsError.value = null
  try {
    stats.value = await getDeviceStats()
  } catch (e) {
    stats.value = null
    statsError.value = errText(e, '統計載入失敗')
  }
}
async function calcBelow() {
  if (!below.platform || !/^\d+\.\d+\.\d+$/.test(below.version.trim())) return void ElMessage.warning('請選擇平台並填入「主.次.修」格式的版本號，例如 1.2.0')
  try {
    const r = await getDeviceStats({ platform: below.platform, belowVersion: below.version.trim() })
    belowCount.value = r.devicesBelowVersion ?? 0
  } catch (e) {
    ElMessage.error(errText(e, '試算失敗，請稍後再試'))
  }
}

// ── 清單 ──
const filters = reactive({ platform: '', appVersion: '', permission: '', tokenStatus: '' })
const rows = ref<DeviceDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
async function load() {
  if (!canView.value) return void (loading.value = false)
  loading.value = true
  loadError.value = null
  try {
    const r = await listDevices({
      platform: filters.platform || undefined, appVersion: filters.appVersion.trim() || undefined,
      permission: filters.permission || undefined, tokenStatus: filters.tokenStatus || undefined, page: page.value, pageSize: pageSize.value,
    })
    rows.value = r.items
    total.value = r.totalCount
  } catch (e) {
    rows.value = []
    total.value = 0
    loadError.value = errText(e, '裝置清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  load()
}
function clearFilter() {
  Object.assign(filters, { platform: '', appVersion: '', permission: '', tokenStatus: '' })
  applyFilter()
}
onMounted(() => {
  if (!canView.value) return void (loading.value = false)
  load()
  loadStats()
})

// ── 詳情 ──
const detailVisible = ref(false)
const detail = ref<DeviceDetailDto | null>(null)
const detailLoading = ref(false)
const detailError = ref<string | null>(null)
async function openDetail(row: DeviceDto, reveal = false) {
  detailVisible.value = true
  detailLoading.value = true
  detailError.value = null
  try {
    detail.value = await getDevice(row.id, reveal)
  } catch (e) {
    detail.value = null
    detailError.value = errText(e, '裝置資料載入失敗')
  } finally {
    detailLoading.value = false
  }
}
async function reveal() {
  const d = detail.value?.device
  if (!d) return
  try {
    await ElMessageBox.confirm('完整的裝置識別碼與推播識別碼視同個資，查看會留下系統紀錄。確定要查看嗎？', '查看完整識別碼', { confirmButtonText: '查看', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  await openDetail(d, true)
}

// ── 失效識別碼清理 ──
const cleaning = ref(false)
async function cleanup() {
  try {
    await ElMessageBox.confirm(`會把 ${stats.value?.invalidTokenCount ?? ''} 台「已失效」裝置的推播識別碼資料清空（裝置與追蹤偏好都保留），這些裝置重新開啟 App 後會自動重新登記。確定要清理嗎？`, '清理失效的推播識別碼', { confirmButtonText: '清理', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  cleaning.value = true
  try {
    const r = await cleanupInvalidTokens()
    ElMessage.success(`已清理 ${r.tokensCleared} 台裝置`)
    load()
    loadStats()
  } catch (e) {
    ElMessage.error(errText(e, '清理失敗，請稍後再試'))
  } finally {
    cleaning.value = false
  }
}
const tokenTag = (s: string) => (s === 'valid' ? 'success' : s === 'invalid' ? 'danger' : 'info')
</script>

<template>
  <div class="dev">
    <PageHeader title="推播裝置">
      <template #meta><FrontendUnitBanner module-code="M4" /></template>
    </PageHeader>
    <el-alert class="dev__block" type="info" show-icon :closable="false" title="兩隊共用：這是安裝了行動 App 的裝置清單，不分俱樂部。裝置識別碼與推播識別碼視同個資，清單只顯示遮罩後的內容；清單只說明裝置有沒有綁定會員，不會顯示是哪一位會員。" />

    <el-card v-if="!canView" shadow="never"><el-empty description="你的帳號沒有檢視推播裝置的權限" /></el-card>
    <template v-else>
      <el-alert v-if="statsError" class="dev__block" type="warning" show-icon :closable="false" :title="statsError" />
      <template v-if="stats">
        <div class="dev__stats dev__block">
          <el-card shadow="never"><div class="dev__num">{{ stats.totalDevices.toLocaleString() }}</div><div class="dev__muted">已登記的裝置</div></el-card>
          <el-card shadow="never"><div class="dev__num">{{ stats.activeLast7Days.toLocaleString() }}</div><div class="dev__muted">近 7 天有開啟過</div></el-card>
          <el-card shadow="never"><div class="dev__num">{{ stats.activeLast30Days.toLocaleString() }}</div><div class="dev__muted">近 30 天有開啟過</div></el-card>
          <el-card shadow="never"><div class="dev__num">{{ stats.invalidTokenCount.toLocaleString() }}</div><div class="dev__muted">推播識別碼已失效</div><el-button v-if="canCleanup && stats.invalidTokenCount > 0" size="small" class="dev__clean" :loading="cleaning" @click="cleanup">清理</el-button></el-card>
        </div>
        <el-row :gutter="12" class="dev__block">
          <el-col :xs="24" :md="12">
            <el-card shadow="never" header="各版本裝置數">
              <el-empty v-if="stats.byVersion.length === 0" description="沒有資料" :image-size="48" />
              <el-table v-else :data="stats.byVersion" size="small" max-height="260"><el-table-column prop="platformLabel" label="平台" width="100" /><el-table-column prop="appVersion" label="版本" /><el-table-column prop="count" label="裝置數" width="90" /></el-table>
            </el-card>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-card shadow="never" header="推播權限狀態">
              <el-empty v-if="stats.byPermission.length === 0" description="沒有資料" :image-size="48" />
              <el-table v-else :data="stats.byPermission" size="small"><el-table-column prop="label" label="狀態" /><el-table-column prop="count" label="裝置數" width="90" /></el-table>
              <div class="dev__below">
                <div class="dev__muted">想設定「最低支援版本」前，先算算有多少裝置會被強制更新：</div>
                <div class="dev__below-row">
                  <el-select v-model="below.platform" placeholder="平台" class="dev__sel"><el-option label="iPhone（iOS）" value="ios" /><el-option label="Android" value="android" /></el-select>
                  <el-input v-model="below.version" placeholder="版本，例如 1.2.0" class="dev__sel" />
                  <el-button @click="calcBelow">試算</el-button>
                </div>
                <div v-if="belowCount !== null" class="dev__result">低於 {{ below.version }} 的 {{ below.platform === 'ios' ? 'iPhone' : 'Android' }} 裝置共 {{ belowCount.toLocaleString() }} 台</div>
              </div>
            </el-card>
          </el-col>
        </el-row>
      </template>

      <el-card shadow="never" class="dev__block">
        <div class="dev__row">
          <el-select v-model="filters.platform" placeholder="平台" clearable class="dev__sel" @change="applyFilter"><el-option label="iPhone（iOS）" value="ios" /><el-option label="Android" value="android" /></el-select>
          <el-input v-model="filters.appVersion" placeholder="App 版本" clearable class="dev__sel" @keyup.enter="applyFilter" @clear="applyFilter" />
          <el-select v-model="filters.permission" placeholder="推播權限" clearable class="dev__sel" @change="applyFilter">
            <el-option label="尚未詢問" value="not_determined" /><el-option label="已允許" value="granted" /><el-option label="已拒絕" value="denied" /><el-option label="暫時允許" value="provisional" />
          </el-select>
          <el-select v-model="filters.tokenStatus" placeholder="推播識別碼" clearable class="dev__sel" @change="applyFilter"><el-option label="有效" value="valid" /><el-option label="已失效" value="invalid" /><el-option label="沒有" value="none" /></el-select>
          <el-button type="primary" @click="applyFilter">篩選</el-button>
          <el-button @click="clearFilter">清除</el-button>
        </div>
      </el-card>

      <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
      <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
      <el-card v-else shadow="never">
        <el-empty v-if="rows.length === 0" description="沒有符合條件的裝置" />
        <template v-else>
          <el-table v-if="!isMobile" :data="rows" row-key="id">
            <el-table-column label="裝置識別碼（已遮罩）" min-width="180" prop="deviceInstallIdMasked" />
            <el-table-column label="平台" width="150"><template #default="{ row }">{{ row.platformLabel }}<div class="dev__muted">系統 {{ row.osVersion || '—' }}</div></template></el-table-column>
            <el-table-column label="App 版本" width="100"><template #default="{ row }">{{ row.appVersion || '—' }}</template></el-table-column>
            <el-table-column label="語言" width="80" prop="localeLabel" />
            <el-table-column label="推播權限" width="110" prop="pushPermissionLabel" />
            <el-table-column label="推播識別碼" width="110"><template #default="{ row }"><el-tag :type="tokenTag(row.pushTokenStatus)" size="small">{{ row.pushTokenStatusLabel }}</el-tag></template></el-table-column>
            <el-table-column label="已綁定會員" width="100"><template #default="{ row }">{{ row.isMemberBound ? '是' : '否' }}</template></el-table-column>
            <el-table-column label="最近開啟（台灣時間）" width="160"><template #default="{ row }">{{ formatDateTime(row.lastActiveAt) }}</template></el-table-column>
            <el-table-column label="操作" width="80" fixed="right"><template #default="{ row }"><el-button size="small" text type="primary" @click="openDetail(row)">詳情</el-button></template></el-table-column>
          </el-table>
          <MobileCardList v-else :rows="rows" row-key="id">
            <template #title="{ row }">{{ row.deviceInstallIdMasked }}</template>
            <template #meta="{ row }"><span>{{ row.platformLabel }} {{ row.appVersion || '' }}</span><el-tag :type="tokenTag(row.pushTokenStatus)" size="small">{{ row.pushTokenStatusLabel }}</el-tag><span>{{ row.pushPermissionLabel }}</span></template>
            <template #actions="{ row }"><el-button size="small" text type="primary" @click="openDetail(row)">詳情</el-button></template>
          </MobileCardList>
          <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="dev__pager" @current-change="load" @size-change="applyFilter" />
        </template>
      </el-card>
    </template>

    <el-dialog v-model="detailVisible" title="裝置詳情" width="560px" :fullscreen="isMobile">
      <el-skeleton v-if="detailLoading" :rows="5" animated />
      <el-alert v-else-if="detailError" type="error" show-icon :closable="false" :title="detailError" />
      <template v-else-if="detail">
        <el-descriptions :column="1" border>
          <el-descriptions-item label="裝置識別碼">{{ detail.revealed ? detail.deviceInstallId : detail.device.deviceInstallIdMasked }}</el-descriptions-item>
          <el-descriptions-item label="推播識別碼">{{ detail.revealed ? (detail.pushToken || '沒有') : detail.device.pushTokenStatusLabel }}</el-descriptions-item>
          <el-descriptions-item label="平台與系統">{{ detail.device.platformLabel }}・{{ detail.device.osVersion || '—' }}</el-descriptions-item>
          <el-descriptions-item label="App 版本">{{ detail.device.appVersion || '—' }}</el-descriptions-item>
          <el-descriptions-item label="語言">{{ detail.device.localeLabel }}</el-descriptions-item>
          <el-descriptions-item label="推播權限">{{ detail.device.pushPermissionLabel }}</el-descriptions-item>
          <el-descriptions-item label="追蹤項目數">{{ detail.subscriptionCount }}</el-descriptions-item>
          <el-descriptions-item label="首次出現">{{ formatDateTime(detail.device.firstSeenAt) }}</el-descriptions-item>
          <el-descriptions-item label="最近開啟">{{ formatDateTime(detail.device.lastActiveAt) }}</el-descriptions-item>
        </el-descriptions>
        <el-button v-if="canReveal && !detail.revealed" class="dev__reveal" @click="reveal">查看完整識別碼</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.dev { min-width: 0; }
.dev__block { margin-bottom: 12px; }
.dev__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.dev__sel { width: 150px; max-width: 100%; }
.dev__stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; }
.dev__num { font-size: 26px; font-weight: 600; color: var(--admin-text-primary); }
.dev__muted { font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.dev__clean { margin-top: 8px; }
.dev__below { margin-top: 14px; }
.dev__below-row { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 8px; }
.dev__result { margin-top: 8px; font-weight: 600; font-size: 13px; }
.dev__pager { margin-top: 12px; justify-content: flex-end; }
.dev__reveal { margin-top: 12px; }
</style>
