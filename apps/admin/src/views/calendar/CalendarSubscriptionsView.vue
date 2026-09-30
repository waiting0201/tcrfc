<script setup lang="ts">
/**
 * `L4` 訂閱與匯出（主站規劃書 §4.12 L4；apps/api/README.md「B1」L4 一節）。
 * - 訂閱：全站與每支球隊各一條行事曆訂閱網址，附估計的訂閱數（來源辨識靠網路來源，只能估計，畫面照後端說明顯示）。
 * - 匯出：指定期間匯出成表格檔（CSV）或行事曆檔（ics）。行事曆是公開資料，不需要填用途。
 * - 匯入：整季賽程 CSV，與「賽程與賽果」的匯入是同一套規則。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  exportCalendar,
  getCalendarSubscriptions,
  importCalendarMatchesCsv,
  listAdminCalendarEventTypes,
  type AdminEventTypeDto,
  type CalendarSubscriptionsDto,
} from '@/api/adminCalendar'
import type { MatchCsvImportResultDto } from '@/api/adminMatches'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { listAdminVenues, type AdminVenueListItemDto } from '@/api/adminVenues'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { saveBlob } from '@/utils/downloadFile'
import { daysBetweenDates, dateOnlyToPickerDate, pickerDateToDateOnly, taipeiToday } from '@/utils/dateTime'
import { MATCH_COMPETITION_TAG_LABEL, MATCH_STATUS_ORDER, matchStatusLabel } from '@/types/match'
import { CALENDAR_CLUB_TEAM_VALUE } from '@/types/calendar'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const club = computed(() => activeClubId.value)
const canViewSubs = usePermission('calendar.subscription.view')
const canExport = usePermission('calendar.export')
const canImport = usePermission('team.match.create')

const data = ref<CalendarSubscriptionsDto | null>(null)
const loading = ref(true)
const loadError = ref<string | null>(null)

const teams = ref<AdminTeamAdminListItemDto[]>([])
const venues = ref<AdminVenueListItemDto[]>([])
const eventTypes = ref<AdminEventTypeDto[]>([])

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const [subs, t, v, et] = await Promise.allSettled([
      canViewSubs.value ? getCalendarSubscriptions(club.value) : Promise.resolve(null),
      listAdminClubTeams(club.value),
      listAdminVenues(club.value),
      listAdminCalendarEventTypes(club.value),
    ])
    if (subs.status === 'rejected') throw subs.reason
    data.value = subs.value
    teams.value = t.status === 'fulfilled' ? t.value : []
    venues.value = v.status === 'fulfilled' ? v.value : []
    eventTypes.value = et.status === 'fulfilled' ? et.value : []
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, () => {
  resetExport()
  load()
})

// ── 訂閱網址 ─────────────────────────────────────────────────────────────────────────
async function copy(text: string) {
  try {
    await navigator.clipboard.writeText(text)
    ElMessage.success('已複製')
  } catch {
    ElMessage.warning('無法自動複製，請手動選取網址複製')
  }
}

// ── 匯出 ─────────────────────────────────────────────────────────────────────────────
const toDateOnly = pickerDateToDateOnly
function monthRange(): [string, string] {
  const now = dateOnlyToPickerDate(taipeiToday())!
  return [toDateOnly(new Date(now.getFullYear(), now.getMonth(), 1)), toDateOnly(new Date(now.getFullYear(), now.getMonth() + 1, 1))]
}

const exportForm = reactive({
  format: 'csv' as 'csv' | 'ics',
  range: monthRange() as [string, string],
  team: '',
  sourceType: '',
  venueId: '',
  status: '',
  type: '',
})
const exporting = ref(false)
const exportError = ref<string | null>(null)

function resetExport() {
  exportForm.range = monthRange()
  exportForm.team = ''
  exportForm.sourceType = ''
  exportForm.venueId = ''
  exportForm.status = ''
  exportForm.type = ''
  exportError.value = null
}

const typeOptions = computed(() => [
  ...Object.entries(MATCH_COMPETITION_TAG_LABEL).map(([value, label]) => ({ value, label: `賽事：${label}` })),
  ...eventTypes.value.map((t) => ({ value: t.code, label: `活動：${t.nameZh || t.code}` })),
])

async function handleExport() {
  exportError.value = null
  const [from, to] = exportForm.range ?? []
  if (!from || !to) return void (exportError.value = '請選擇匯出的期間')
  if (daysBetweenDates(from, to) > 366) return void (exportError.value = '匯出期間最長 366 天')
  exporting.value = true
  try {
    const result = await exportCalendar(club.value, {
      format: exportForm.format,
      from,
      to,
      team: exportForm.team || undefined,
      sourceType: exportForm.sourceType || undefined,
      venueId: exportForm.venueId || undefined,
      status: exportForm.status || undefined,
      type: exportForm.type || undefined,
    })
    saveBlob(result.blob, result.filename ?? `calendar-${from}.${exportForm.format}`)
  } catch (error) {
    exportError.value = error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試'
  } finally {
    exporting.value = false
  }
}

// ── 整季賽程匯入 ─────────────────────────────────────────────────────────────────────
const fileInput = ref<HTMLInputElement | null>(null)
const importing = ref(false)
const importResult = ref<MatchCsvImportResultDto | null>(null)
const importDialog = ref(false)

async function handleFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  importing.value = true
  try {
    const result = await importCalendarMatchesCsv(club.value, file)
    importResult.value = result
    importDialog.value = true
    if (result.errors.length === 0) ElMessage.success(`已匯入 ${result.importedCount} 場賽事`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯入失敗，請稍後再試')
  } finally {
    importing.value = false
  }
}
</script>

<template>
  <div class="calendar-subs">
    <PageHeader title="訂閱與匯出">
      <template #meta><FrontendUnitBanner module-code="L4" /></template>
    </PageHeader>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>

    <template v-else>
      <!-- 訂閱 -->
      <el-card v-if="canViewSubs && data" shadow="never" header="行事曆訂閱網址" class="calendar-subs__section">
        <p class="calendar-subs__hint">
          球迷把網址加入手機或電腦的行事曆後，賽程異動會自動同步。「訂閱行事曆」連結可直接在手機上開啟；電腦或其他軟體請複製「一般網址」。
        </p>
        <el-alert v-if="data.statsNote" :title="data.statsNote" type="info" show-icon :closable="false" class="calendar-subs__alert" />

        <el-table v-if="!isMobile" :data="data.feeds" row-key="feedKey">
          <el-table-column label="範圍" min-width="140">
            <template #default="{ row }">
              {{ row.label }}
              <el-tag v-if="!row.isPublic" type="info" size="small">不公開（連結目前無效）</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="訂閱網址" min-width="260">
            <template #default="{ row }">
              <div class="calendar-subs__url">
                <span class="calendar-subs__url-text">{{ row.httpsUrl }}</span>
                <el-button size="small" text type="primary" @click="copy(row.httpsUrl)">複製一般網址</el-button>
                <el-button size="small" text type="primary" @click="copy(row.webcalUrl)">複製訂閱連結</el-button>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="近 30 天訂閱數（估計）" width="130">
            <template #default="{ row }">{{ row.subscribers30d }}</template>
          </el-table-column>
          <el-table-column label="近 7 天（估計）" width="110">
            <template #default="{ row }">{{ row.subscribers7d }}</template>
          </el-table-column>
          <el-table-column label="最近一次被讀取" width="130">
            <template #default="{ row }">{{ row.lastFetchedOn || '尚無' }}</template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="data.feeds" row-key="feedKey">
          <template #title="{ row }">{{ row.label }}</template>
          <template #meta="{ row }">
            <el-tag v-if="!row.isPublic" type="info" size="small">不公開</el-tag>
            <span>30 天約 {{ row.subscribers30d }} 人</span>
            <span>7 天約 {{ row.subscribers7d }} 人</span>
            <span>最近讀取：{{ row.lastFetchedOn || '尚無' }}</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="copy(row.httpsUrl)">複製一般網址</el-button>
            <el-button size="small" text type="primary" @click="copy(row.webcalUrl)">複製訂閱連結</el-button>
          </template>
        </MobileCardList>
      </el-card>

      <!-- 匯出 -->
      <el-card v-if="canExport" shadow="never" header="匯出行事曆" class="calendar-subs__section">
        <p class="calendar-subs__hint">
          表格檔（CSV）可用 Excel 開啟；行事曆檔（ics）可匯入手機或電腦的行事曆。期間最長 366 天。未公開的自建活動只有能管理自建事件的人匯得出來。
        </p>
        <el-alert v-if="exportError" :title="exportError" type="warning" show-icon :closable="false" class="calendar-subs__alert" />
        <el-form label-position="top">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12" :md="8">
              <el-form-item label="檔案格式">
                <el-radio-group v-model="exportForm.format">
                  <el-radio-button value="csv">表格檔（CSV）</el-radio-button>
                  <el-radio-button value="ics">行事曆檔（ics）</el-radio-button>
                </el-radio-group>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12" :md="16">
              <el-form-item label="期間">
                <el-date-picker
                  v-model="exportForm.range"
                  type="daterange"
                  value-format="YYYY-MM-DD"
                  range-separator="至"
                  start-placeholder="開始日期"
                  end-placeholder="結束日期"
                  :clearable="false"
                  style="width: 100%; max-width: 360px"
                />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12" :md="8">
              <el-form-item label="球隊">
                <el-select v-model="exportForm.team" clearable placeholder="全部" filterable style="width: 100%">
                  <el-option label="俱樂部活動" :value="CALENDAR_CLUB_TEAM_VALUE" />
                  <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.code" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12" :md="8">
              <el-form-item label="來源">
                <el-select v-model="exportForm.sourceType" clearable placeholder="全部" style="width: 100%">
                  <el-option label="賽事" value="match" />
                  <el-option label="自建活動" value="custom" />
                  <el-option label="試訓（需先開啟同步）" value="trial" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12" :md="8">
              <el-form-item label="場地">
                <el-select v-model="exportForm.venueId" clearable filterable placeholder="全部" style="width: 100%">
                  <el-option v-for="v in venues" :key="v.id" :label="v.nameZh" :value="v.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12" :md="8">
              <el-form-item label="賽事狀態">
                <el-select v-model="exportForm.status" clearable placeholder="全部" style="width: 100%">
                  <el-option v-for="s in MATCH_STATUS_ORDER" :key="s" :label="matchStatusLabel(s)" :value="s" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12" :md="8">
              <el-form-item label="類型">
                <el-select v-model="exportForm.type" clearable filterable placeholder="全部" style="width: 100%">
                  <el-option v-for="o in typeOptions" :key="o.value" :label="o.label" :value="o.value" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <el-button type="primary" :loading="exporting" @click="handleExport">匯出</el-button>
        </el-form>
      </el-card>

      <!-- 匯入 -->
      <el-card v-if="canImport" shadow="never" header="匯入整季賽程" class="calendar-subs__section">
        <p class="calendar-subs__hint">
          上傳整季賽程的 CSV，格式與「賽程與賽果」的匯入完全相同。匯入是整批新增，不是逐列更新；只要有任何一列有錯，整份檔案都不會寫入。
        </p>
        <input ref="fileInput" type="file" accept=".csv,text/csv" class="calendar-subs__file" @change="handleFile">
        <el-button :loading="importing" @click="fileInput?.click()">選擇 CSV 檔案並匯入</el-button>
      </el-card>

      <el-card v-if="!canViewSubs && !canExport && !canImport" shadow="never">
        <el-empty description="你的帳號沒有使用這個頁面功能的權限" />
      </el-card>
    </template>

    <el-dialog v-model="importDialog" title="匯入結果" width="640px">
      <template v-if="importResult">
        <el-result v-if="importResult.errors.length === 0" icon="success" :title="`已匯入 ${importResult.importedCount} 場賽事`" />
        <template v-else>
          <el-alert title="整份檔案有錯誤列，本次沒有任何一列被寫入，請修正後重新上傳。" type="error" show-icon :closable="false" class="calendar-subs__alert" />
          <el-table :data="importResult.errors" max-height="360">
            <el-table-column label="行號" width="80"><template #default="{ row }">{{ row.rowNumber }}</template></el-table-column>
            <el-table-column label="錯誤原因"><template #default="{ row }">{{ row.reason }}</template></el-table-column>
          </el-table>
        </template>
      </template>
      <template #footer><el-button type="primary" @click="importDialog = false">關閉</el-button></template>
    </el-dialog>
  </div>
</template>

<style scoped>
.calendar-subs__section { margin-bottom: 16px; }
.calendar-subs__hint { margin: 0 0 12px; font-size: 13px; line-height: 1.7; color: var(--admin-text-secondary); }
.calendar-subs__alert { margin-bottom: 12px; }
.calendar-subs__file { display: none; }
.calendar-subs__url { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 8px; }
.calendar-subs__url-text { word-break: break-all; font-size: 12px; color: var(--admin-text-tertiary); }
</style>
