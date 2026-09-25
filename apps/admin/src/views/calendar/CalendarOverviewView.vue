<script setup lang="ts">
/**
 * `L1` 行事曆總覽（對應主站規劃書 §4.12 L1，行 1368–1376；apps/api/README.md「S1-11」）。
 *
 * 🔴 **本輪範圍縮減**（沿用 `STATUS.md` 既定切法，非本輪判斷）：規劃書 L1 原文的「隊別分軌並排
 * 檢視」「拖曳調整日期回寫賽事」「衝突偵測」與 L3／L4 一起排進 `S2-6`。本輪只做**合併讀取的
 * 呈現**——列表與月曆兩種檢視，賽事在這裡是唯讀資訊，點進去連到既有的「賽程與賽果」編輯頁
 * （`/teams/matches/:id/edit`，C4，S1-8），不在這裡直接改賽事；自建活動點進去連到 L2 編輯頁。
 *
 * 月曆檢視只把重複事件展開後的每一次發生標在對應日期（後端 `RecurrenceExpander` 已經在查詢
 * 範圍內展開好，這裡不用自己算重複規則）；多日活動（有 `endsAt` 橫跨數天）目前只標在起始日，
 * 跟後端「俱樂部活動列表分頁只用原始 `starts_at` 排序」的既有簡化方向一致，沒有另外畫跨日色塊。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { listAdminCalendarEvents, type AdminCalendarEventDto } from '@/api/adminCalendar'
import { AdminApiError } from '@/api/http'
import { useCalendarPermissions } from '@/composables/useCalendarPermissions'
import { formatDateTime } from '@/utils/formatDateTime'
import { matchStatusLabel } from '@/types/match'
import { CALENDAR_CLUB_TEAM_VALUE, calendarSourceTagType, calendarSourceTypeLabel } from '@/types/calendar'

const router = useRouter()
const club = computed(() => activeClubId.value)
const { canViewCustomEvents } = useCalendarPermissions()

type ViewMode = 'calendar' | 'list'
const viewMode = ref<ViewMode>('calendar')

const filters = reactive({ team: '', sourceType: '' as '' | 'match' | 'custom' })

const teams = ref<AdminTeamAdminListItemDto[]>([])
const teamLabelByCode = computed(() => {
  const map = new Map<string, string>()
  for (const t of teams.value) map.set(t.code, t.nameZh || t.code)
  return map
})

function teamCodesLabel(codes: string[]): string {
  if (codes.length === 0) return '俱樂部活動'
  return codes.map((code) => teamLabelByCode.value.get(code) ?? code).join('、')
}

// ── 檢視範圍：月曆檢視跟著目前顯示的月份走，列表檢視用日期區間選擇器（預設本月）───────────────

function toDateOnlyString(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

function defaultMonthRange(): [Date, Date] {
  const now = new Date()
  const from = new Date(now.getFullYear(), now.getMonth(), 1)
  const to = new Date(now.getFullYear(), now.getMonth() + 1, 1)
  return [from, to]
}

const calendarValue = ref(new Date())
const listRange = ref<[Date, Date]>(defaultMonthRange())

const queryRange = computed<[Date, Date]>(() => {
  if (viewMode.value === 'calendar') {
    const y = calendarValue.value.getFullYear()
    const m = calendarValue.value.getMonth()
    return [new Date(y, m, 1), new Date(y, m + 1, 1)]
  }
  return listRange.value
})

const queryKey = computed(() =>
  JSON.stringify({
    club: club.value,
    view: viewMode.value,
    from: toDateOnlyString(queryRange.value[0]),
    to: toDateOnlyString(queryRange.value[1]),
    team: filters.team,
    sourceType: filters.sourceType,
  }),
)

const events = ref<AdminCalendarEventDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadTeams() {
  try {
    teams.value = await listAdminClubTeams(club.value)
  } catch {
    teams.value = []
  }
}

async function loadEvents() {
  loading.value = true
  loadError.value = null
  try {
    const [from, to] = queryRange.value
    events.value = await listAdminCalendarEvents(club.value, {
      from: toDateOnlyString(from),
      to: toDateOnlyString(to),
      team: filters.team || undefined,
      sourceType: filters.sourceType || undefined,
    })
  } catch (error) {
    events.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '行事曆載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

watch(queryKey, loadEvents)

async function bootstrap() {
  filters.team = ''
  filters.sourceType = ''
  calendarValue.value = new Date()
  listRange.value = defaultMonthRange()
  await loadTeams()
  await loadEvents()
}

onMounted(bootstrap)
watch(club, bootstrap)

function clearFilters() {
  filters.team = ''
  filters.sourceType = ''
}

// ── 依日期分桶（月曆檢視用）──────────────────────────────────────────────────────────

const eventsByDay = computed(() => {
  const map = new Map<string, AdminCalendarEventDto[]>()
  for (const e of events.value) {
    const day = e.startsAt.slice(0, 10)
    if (!map.has(day)) map.set(day, [])
    map.get(day)!.push(e)
  }
  for (const list of map.values()) list.sort((a, b) => a.startsAt.localeCompare(b.startsAt))
  return map
})

const sortedListEvents = computed(() => [...events.value].sort((a, b) => a.startsAt.localeCompare(b.startsAt)))

const MAX_CHIPS_PER_DAY = 3

const selectedDay = ref<string | null>(null)
const selectedDayEvents = computed(() => (selectedDay.value ? eventsByDay.value.get(selectedDay.value) ?? [] : []))
const dayDialogVisible = computed({
  get: () => selectedDay.value !== null,
  set: (v: boolean) => {
    if (!v) selectedDay.value = null
  },
})

function openDay(day: string) {
  if ((eventsByDay.value.get(day)?.length ?? 0) === 0) return
  selectedDay.value = day
}

function statusTagType(status: string | null | undefined): 'success' | 'warning' | 'info' | 'danger' {
  if (status === 'played') return 'success'
  if (status === 'live') return 'warning'
  if (status === 'postponed' || status === 'cancelled') return 'danger'
  return 'info'
}

/** 賽事一律可以點進去（唯讀連結，見檔頭）；自建活動要看得到 L2 才給連結，否則點進去只會被
 * 後端 403（`team_competition`／`academy_program` 只拿到 `calendar.view`，見
 * `useCalendarPermissions` 檔頭）。 */
function canOpen(event: AdminCalendarEventDto): boolean {
  return event.sourceType === 'match' || canViewCustomEvents.value
}

function openEvent(event: AdminCalendarEventDto) {
  if (!canOpen(event)) return
  if (event.sourceType === 'match') {
    router.push(`/teams/matches/${event.sourceId}/edit`)
  } else {
    router.push(`/calendar/events/${event.sourceId}/edit`)
  }
}
</script>

<template>
  <div class="calendar-overview">
    <PageHeader title="行事曆總覽">
      <template #meta>
        <FrontendUnitBanner module-code="L1" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="calendar-overview__filters">
      <div class="calendar-overview__filter-row">
        <el-radio-group v-model="viewMode">
          <el-radio-button value="calendar">月曆檢視</el-radio-button>
          <el-radio-button value="list">列表檢視</el-radio-button>
        </el-radio-group>

        <el-select v-model="filters.team" placeholder="球隊" clearable filterable class="calendar-overview__filter-select">
          <el-option label="俱樂部活動" :value="CALENDAR_CLUB_TEAM_VALUE" />
          <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.code" />
        </el-select>
        <el-select v-model="filters.sourceType" placeholder="來源" clearable class="calendar-overview__filter-select">
          <el-option label="賽事" value="match" />
          <el-option label="自建活動" value="custom" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>

        <div class="calendar-overview__filter-placeholder" />

        <el-date-picker
          v-if="viewMode === 'list'"
          v-model="listRange"
          type="daterange"
          range-separator="至"
          start-placeholder="開始日期"
          end-placeholder="結束日期"
          class="calendar-overview__range"
        />
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadEvents">重新載入</el-button>
      </el-empty>
    </el-card>

    <!-- 月曆檢視 -->
    <el-card v-else-if="viewMode === 'calendar'" shadow="never" class="calendar-overview__calendar-card">
      <el-calendar v-model="calendarValue">
        <template #date-cell="{ data }">
          <div
            class="calendar-overview__cell"
            :class="{ 'calendar-overview__cell--other-month': data.type !== 'current-month' }"
            @click="openDay(data.day)"
          >
            <span class="calendar-overview__cell-date">{{ data.day.split('-').at(-1) }}</span>
            <div class="calendar-overview__cell-chips">
              <span
                v-for="e in (eventsByDay.get(data.day) ?? []).slice(0, MAX_CHIPS_PER_DAY)"
                :key="`${e.sourceType}-${e.sourceId}`"
                class="calendar-overview__chip"
                :class="`calendar-overview__chip--${e.sourceType}`"
                :title="e.title"
              >
                {{ e.title }}
              </span>
              <span v-if="(eventsByDay.get(data.day)?.length ?? 0) > MAX_CHIPS_PER_DAY" class="calendar-overview__chip-more">
                +{{ (eventsByDay.get(data.day)?.length ?? 0) - MAX_CHIPS_PER_DAY }} 更多
              </span>
            </div>
          </div>
        </template>
      </el-calendar>
    </el-card>

    <!-- 列表檢視 -->
    <el-card v-else shadow="never">
      <el-table v-if="sortedListEvents.length > 0" :data="sortedListEvents" row-key="sourceId">
        <el-table-column label="日期／時間" width="160">
          <template #default="{ row }">
            <div>{{ formatDateTime(row.startsAt) }}</div>
            <div v-if="row.isAllDay" class="calendar-overview__muted">全天</div>
          </template>
        </el-table-column>
        <el-table-column label="來源" width="90">
          <template #default="{ row }">
            <el-tag :type="calendarSourceTagType(row.sourceType)" size="small">{{ calendarSourceTypeLabel(row.sourceType) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="標題" min-width="200">
          <template #default="{ row }">{{ row.title }}</template>
        </el-table-column>
        <el-table-column label="球隊" min-width="140">
          <template #default="{ row }">{{ teamCodesLabel(row.teamCodes) }}</template>
        </el-table-column>
        <el-table-column label="地點" width="140">
          <template #default="{ row }">{{ row.venueName || '—' }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="90">
          <template #default="{ row }">
            <el-tag v-if="row.sourceType === 'match'" :type="statusTagType(row.status)" size="small">
              {{ matchStatusLabel(row.status ?? '') }}
            </el-tag>
            <span v-else>—</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="110" fixed="right">
          <template #default="{ row }">
            <el-button v-if="canOpen(row)" size="small" text type="primary" @click="openEvent(row)">
              {{ row.sourceType === 'match' ? '查看賽事' : '查看活動' }}
            </el-button>
            <span v-else class="calendar-overview__muted">無檢視權限</span>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="這段期間找不到符合條件的賽事或活動" />
    </el-card>

    <el-dialog v-model="dayDialogVisible" :title="selectedDay ?? ''" width="560px">
      <el-empty v-if="selectedDayEvents.length === 0" description="這天沒有任何賽事或活動" />
      <ul v-else class="calendar-overview__day-list">
        <li v-for="e in selectedDayEvents" :key="`${e.sourceType}-${e.sourceId}`" class="calendar-overview__day-item">
          <div class="calendar-overview__day-item-main">
            <el-tag :type="calendarSourceTagType(e.sourceType)" size="small">{{ calendarSourceTypeLabel(e.sourceType) }}</el-tag>
            <span class="calendar-overview__day-item-title">{{ e.title }}</span>
          </div>
          <div class="calendar-overview__day-item-meta">
            {{ formatDateTime(e.startsAt) }}<span v-if="e.isAllDay">（全天）</span>
            <span v-if="e.teamCodes.length || e.sourceType === 'custom'"> ・ {{ teamCodesLabel(e.teamCodes) }}</span>
            <span v-if="e.venueName"> ・ {{ e.venueName }}</span>
          </div>
          <el-button v-if="canOpen(e)" size="small" text type="primary" @click="openEvent(e)">
            {{ e.sourceType === 'match' ? '查看賽事' : '查看活動' }}
          </el-button>
        </li>
      </ul>
    </el-dialog>
  </div>
</template>

<style scoped>
.calendar-overview__filters {
  margin-bottom: 12px;
}

.calendar-overview__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.calendar-overview__filter-select {
  width: 160px;
  max-width: 100%;
}

.calendar-overview__filter-placeholder {
  flex: 1;
}

.calendar-overview__range {
  width: 260px;
  max-width: 100%;
}

.calendar-overview__calendar-card :deep(.el-calendar-table .el-calendar-day) {
  height: auto;
  min-height: 88px;
  padding: 4px;
}

.calendar-overview__cell {
  height: 100%;
  min-height: 80px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.calendar-overview__cell--other-month {
  opacity: 0.4;
}

.calendar-overview__cell-date {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.calendar-overview__cell-chips {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.calendar-overview__chip {
  font-size: 11px;
  line-height: 1.4;
  padding: 0 4px;
  border-radius: 3px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #ffffff;
}

.calendar-overview__chip--match {
  background: var(--el-color-success);
}

.calendar-overview__chip--custom {
  background: var(--el-color-warning);
}

.calendar-overview__chip-more {
  font-size: 11px;
  color: var(--admin-text-tertiary);
}

.calendar-overview__muted {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.calendar-overview__day-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.calendar-overview__day-item {
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.calendar-overview__day-item-main {
  display: flex;
  align-items: center;
  gap: 8px;
}

.calendar-overview__day-item-title {
  font-weight: 600;
}

.calendar-overview__day-item-meta {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}
</style>
