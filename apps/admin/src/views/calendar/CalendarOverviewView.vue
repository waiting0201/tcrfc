<script setup lang="ts">
/**
 * `L1` 行事曆總覽（對應主站規劃書 §4.12 L1，行 1368–1376；apps/api/README.md「S1-11」＋「B1」L1 進階）。
 *
 * S2-6 補上三件進階功能（原本 S1-11 刻意縮減掉的部分）：
 * - **隊別分軌檢視**：每支球隊一條軌道並排（俱樂部活動另成一條），一場跨隊賽事會同時出現在涉及的每條軌道。
 * - **衝突偵測提示**：同一時段、同一場地或同一梯隊的兩件事，在頁面上方提示並在月曆上標警示。
 * - **拖曳改期**：月曆檢視把賽事／自建活動拖到別的日期；手機與不方便拖曳時用「調整日期」按鈕。
 *   改期只做日期（賽事另可改開賽時間），是否同時標為延賽由使用者勾選；衝突時要再次確認才寫入。
 *
 * 賽事點進去連到「賽程與賽果」編輯頁（`/teams/matches/:id/edit`），自建活動連到 L2 編輯頁。
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
import { listAdminVenues, type AdminVenueListItemDto } from '@/api/adminVenues'
import {
  getAdminCalendarTracks,
  listAdminCalendarConflicts,
  listAdminCalendarEvents,
  listAdminCalendarEventTypes,
  type AdminCalendarConflictDto,
  type AdminCalendarEventDto,
  type AdminCalendarTracksDto,
  type AdminEventTypeDto,
} from '@/api/adminCalendar'
import { AdminApiError } from '@/api/http'
import { useCalendarPermissions } from '@/composables/useCalendarPermissions'
import { usePermission } from '@/composables/useCrudPermissions'
import { formatDateTime } from '@/utils/formatDateTime'
import { MATCH_COMPETITION_TAG_LABEL, MATCH_STATUS_ORDER, matchStatusLabel } from '@/types/match'
import { CALENDAR_CLUB_TEAM_VALUE, calendarSourceTagType, calendarSourceTypeLabel } from '@/types/calendar'
import CalendarRescheduleDialog from './CalendarRescheduleDialog.vue'

const router = useRouter()
const club = computed(() => activeClubId.value)
const { canViewCustomEvents } = useCalendarPermissions()

type ViewMode = 'calendar' | 'list' | 'tracks'
const viewMode = ref<ViewMode>('calendar')

const filters = reactive({ team: '', sourceType: '' as '' | 'match' | 'custom' | 'trial', venueId: '', status: '', type: '' })

const venues = ref<AdminVenueListItemDto[]>([])
const eventTypes = ref<AdminEventTypeDto[]>([])

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
    venueId: filters.venueId,
    status: filters.status,
    type: filters.type,
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

/** 場地與活動類型只是篩選用的下拉來源，載入失敗不影響總覽本身。 */
async function loadFilterSources() {
  const [v, t] = await Promise.allSettled([listAdminVenues(club.value), listAdminCalendarEventTypes(club.value)])
  venues.value = v.status === 'fulfilled' ? v.value : []
  eventTypes.value = t.status === 'fulfilled' ? t.value : []
}

const tracks = ref<AdminCalendarTracksDto | null>(null)
const conflicts = ref<AdminCalendarConflictDto[]>([])
const showConflictList = ref(false)

async function loadEvents() {
  loading.value = true
  loadError.value = null
  try {
    const [from, to] = queryRange.value
    const range = { from: toDateOnlyString(from), to: toDateOnlyString(to) }
    if (viewMode.value === 'tracks') {
      const result = await getAdminCalendarTracks(club.value, range)
      tracks.value = result
      conflicts.value = result.conflicts
      events.value = []
    } else {
      tracks.value = null
      const [list, found] = await Promise.all([
        listAdminCalendarEvents(club.value, {
          ...range,
          team: filters.team || undefined,
          sourceType: filters.sourceType || undefined,
          venueId: filters.venueId || undefined,
          status: filters.status || undefined,
          type: filters.type || undefined,
        }),
        // 衝突提示只是輔助資訊：失敗時不擋住總覽本身
        listAdminCalendarConflicts(club.value, range).catch(() => [] as AdminCalendarConflictDto[]),
      ])
      events.value = list
      conflicts.value = found
    }
  } catch (error) {
    events.value = []
    tracks.value = null
    conflicts.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '行事曆載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

watch(queryKey, loadEvents)

async function bootstrap() {
  filters.team = ''
  filters.sourceType = ''
  filters.venueId = ''
  filters.status = ''
  filters.type = ''
  calendarValue.value = new Date()
  listRange.value = defaultMonthRange()
  await Promise.all([loadTeams(), loadFilterSources()])
  await loadEvents()
}

onMounted(bootstrap)
watch(club, bootstrap)

function clearFilters() {
  filters.team = ''
  filters.sourceType = ''
  filters.venueId = ''
  filters.status = ''
  filters.type = ''
}

const typeOptions = computed(() => [
  ...Object.entries(MATCH_COMPETITION_TAG_LABEL).map(([value, label]) => ({ value, label: `賽事：${label}` })),
  ...eventTypes.value.map((t) => ({ value: t.code, label: `活動：${t.nameZh || t.code}` })),
])

// ── 衝突標示 ─────────────────────────────────────────────────────────────────────────

function eventKey(e: AdminCalendarEventDto): string {
  return `${e.sourceType}-${e.sourceId}`
}

const conflictKeys = computed(() => {
  const set = new Set<string>()
  for (const c of conflicts.value) {
    set.add(eventKey(c.first))
    set.add(eventKey(c.second))
  }
  return set
})

function isConflicting(e: AdminCalendarEventDto): boolean {
  return conflictKeys.value.has(eventKey(e))
}

/** 賽事只有日期＋開賽時間（不能拿時間戳轉本地時區）；自建活動是 UTC 時間點。 */
function displayWhen(e: AdminCalendarEventDto): string {
  if (e.sourceType === 'match') return `${e.startsAt.slice(0, 10)}${e.kickoff ? ` ${e.kickoff}` : ''}`
  return e.isAllDay ? `${formatDateTime(e.startsAt).slice(0, 10)}（全天）` : formatDateTime(e.startsAt)
}

// ── 改期（拖曳＋按鈕）─────────────────────────────────────────────────────────────────

const canMoveMatch = usePermission('team.match.update')
const canMoveCustom = usePermission('calendar.custom_event.update')

/** 只有「未開始」與「延賽」的賽事能改期（後端規則）；權限依來源不同。真正的範圍檢查仍在後端。 */
function canReschedule(e: AdminCalendarEventDto): boolean {
  if (e.sourceType === 'match') return canMoveMatch.value && (e.status === 'scheduled' || e.status === 'postponed')
  if (e.sourceType === 'custom') return canMoveCustom.value
  return false
}

const rescheduleVisible = ref(false)
const rescheduleEvent = ref<AdminCalendarEventDto | null>(null)
const rescheduleDay = ref<string | null>(null)

function openReschedule(event: AdminCalendarEventDto, day: string | null = null) {
  rescheduleEvent.value = event
  rescheduleDay.value = day
  rescheduleVisible.value = true
}

const draggedEvent = ref<AdminCalendarEventDto | null>(null)

function onDragStart(event: AdminCalendarEventDto, e: DragEvent) {
  if (!canReschedule(event)) {
    e.preventDefault()
    return
  }
  draggedEvent.value = event
  e.dataTransfer?.setData('text/plain', eventKey(event))
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move'
}

function onDragOver(e: DragEvent) {
  if (draggedEvent.value) e.preventDefault()
}

function onDrop(day: string) {
  const event = draggedEvent.value
  draggedEvent.value = null
  if (!event || event.startsAt.slice(0, 10) === day) return
  openReschedule(event, day)
}

function onRescheduled() {
  selectedDay.value = null
  loadEvents()
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

function sortEvents(list: AdminCalendarEventDto[]): AdminCalendarEventDto[] {
  return [...list].sort((a, b) => a.startsAt.localeCompare(b.startsAt))
}

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
          <el-radio-button value="tracks">分軌檢視</el-radio-button>
        </el-radio-group>

        <template v-if="viewMode !== 'tracks'">
          <el-select v-model="filters.team" placeholder="球隊" clearable filterable class="calendar-overview__filter-select">
            <el-option label="俱樂部活動" :value="CALENDAR_CLUB_TEAM_VALUE" />
            <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.code" />
          </el-select>
          <el-select v-model="filters.sourceType" placeholder="來源" clearable class="calendar-overview__filter-select">
            <el-option label="賽事" value="match" />
            <el-option label="自建活動" value="custom" />
            <el-option label="試訓（需先在分類設定開啟同步）" value="trial" />
          </el-select>
          <el-select v-model="filters.venueId" placeholder="場地" clearable filterable class="calendar-overview__filter-select">
            <el-option v-for="v in venues" :key="v.id" :label="v.nameZh" :value="v.id" />
          </el-select>
          <el-select v-model="filters.status" placeholder="賽事狀態" clearable class="calendar-overview__filter-select">
            <el-option v-for="st in MATCH_STATUS_ORDER" :key="st" :label="matchStatusLabel(st)" :value="st" />
          </el-select>
          <el-select v-model="filters.type" placeholder="類型" clearable filterable class="calendar-overview__filter-select">
            <el-option v-for="o in typeOptions" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
          <el-button @click="clearFilters">清除</el-button>
        </template>

        <div class="calendar-overview__filter-placeholder" />

        <el-date-picker
          v-if="viewMode !== 'calendar'"
          v-model="listRange"
          type="daterange"
          range-separator="至"
          start-placeholder="開始日期"
          end-placeholder="結束日期"
          class="calendar-overview__range"
        />
      </div>
      <p v-if="filters.status && viewMode !== 'tracks'" class="calendar-overview__hint">依賽事狀態篩選時，不會列出自建活動。</p>
      <p v-if="viewMode === 'tracks'" class="calendar-overview__hint">
        每支球隊一條軌道，跨隊的賽事會同時出現在涉及的每條軌道；沒有指定球隊的活動放在「俱樂部活動」。分軌檢視顯示所選期間的全部內容，不套用上方篩選。
      </p>
      <p v-else-if="viewMode === 'calendar'" class="calendar-overview__hint">
        可以把賽事或活動拖曳到別的日期改期；手機或不方便拖曳時，點日期後按「調整日期」。
      </p>
    </el-card>

    <el-alert
      v-if="!loading && !loadError && conflicts.length > 0"
      type="warning"
      show-icon
      :closable="false"
      class="calendar-overview__conflict"
    >
      <template #title>
        這段期間有 {{ conflicts.length }} 組行程衝突（同一時段使用同一場地或同一支球隊）
        <el-button link type="primary" @click="showConflictList = !showConflictList">{{ showConflictList ? '收合' : '查看明細' }}</el-button>
      </template>
      <ul v-if="showConflictList" class="calendar-overview__conflict-list">
        <li v-for="(c, i) in conflicts" :key="i">
          <div>{{ c.description }}</div>
          <div class="calendar-overview__muted">
            {{ c.first.title }}（{{ displayWhen(c.first) }}）與 {{ c.second.title }}（{{ displayWhen(c.second) }}）
          </div>
        </li>
      </ul>
    </el-alert>

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
            @dragover="onDragOver"
            @drop.prevent="onDrop(data.day)"
          >
            <span class="calendar-overview__cell-date">{{ data.day.split('-').at(-1) }}</span>
            <div class="calendar-overview__cell-chips">
              <span
                v-for="e in (eventsByDay.get(data.day) ?? []).slice(0, MAX_CHIPS_PER_DAY)"
                :key="eventKey(e)"
                class="calendar-overview__chip"
                :class="[`calendar-overview__chip--${e.sourceType}`, { 'calendar-overview__chip--conflict': isConflicting(e), 'calendar-overview__chip--draggable': canReschedule(e) }]"
                :title="isConflicting(e) ? `${e.title}（與其他行程衝突）` : e.title"
                :draggable="canReschedule(e)"
                @dragstart="onDragStart(e, $event)"
                @dragend="draggedEvent = null"
              >
                <el-icon v-if="isConflicting(e)"><Warning /></el-icon>{{ e.title }}
              </span>
              <span v-if="(eventsByDay.get(data.day)?.length ?? 0) > MAX_CHIPS_PER_DAY" class="calendar-overview__chip-more">
                +{{ (eventsByDay.get(data.day)?.length ?? 0) - MAX_CHIPS_PER_DAY }} 更多
              </span>
            </div>
          </div>
        </template>
      </el-calendar>
    </el-card>

    <!-- 分軌檢視 -->
    <template v-else-if="viewMode === 'tracks'">
      <div v-if="tracks && (tracks.tracks.length > 0 || tracks.clubEvents.length > 0)" class="calendar-overview__tracks">
        <el-card
          v-for="track in tracks.tracks"
          :key="track.teamId"
          shadow="never"
          class="calendar-overview__track"
          :style="{ borderTopColor: track.colour || 'var(--admin-border)' }"
        >
          <template #header>
            <div class="calendar-overview__track-head">
              <span class="calendar-overview__track-dot" :style="{ background: track.colour || 'var(--admin-text-tertiary)' }" />
              {{ track.name }}
              <span class="calendar-overview__muted">{{ track.events.length }} 件</span>
            </div>
          </template>
          <p v-if="track.events.length === 0" class="calendar-overview__muted">這段期間沒有安排</p>
          <ul v-else class="calendar-overview__track-list">
            <li v-for="e in sortEvents(track.events)" :key="eventKey(e)" :class="{ 'calendar-overview__track-item--conflict': isConflicting(e) }">
              <div class="calendar-overview__track-item-when">
                {{ displayWhen(e) }}
                <el-tag v-if="isConflicting(e)" type="warning" size="small">衝突</el-tag>
              </div>
              <div class="calendar-overview__track-item-title">
                <el-tag :type="calendarSourceTagType(e.sourceType)" size="small">{{ calendarSourceTypeLabel(e.sourceType) }}</el-tag>
                {{ e.title }}
              </div>
              <div v-if="e.venueName" class="calendar-overview__muted">{{ e.venueName }}</div>
              <div class="calendar-overview__track-item-actions">
                <el-button v-if="canOpen(e)" size="small" text type="primary" @click="openEvent(e)">查看</el-button>
                <el-button v-if="canReschedule(e)" size="small" text type="primary" @click="openReschedule(e)">調整日期</el-button>
              </div>
            </li>
          </ul>
        </el-card>

        <el-card v-if="tracks.clubEvents.length > 0" shadow="never" class="calendar-overview__track">
          <template #header>
            <div class="calendar-overview__track-head">
              俱樂部活動
              <span class="calendar-overview__muted">{{ tracks.clubEvents.length }} 件</span>
            </div>
          </template>
          <ul class="calendar-overview__track-list">
            <li v-for="e in sortEvents(tracks.clubEvents)" :key="eventKey(e)" :class="{ 'calendar-overview__track-item--conflict': isConflicting(e) }">
              <div class="calendar-overview__track-item-when">
                {{ displayWhen(e) }}
                <el-tag v-if="isConflicting(e)" type="warning" size="small">衝突</el-tag>
              </div>
              <div class="calendar-overview__track-item-title">
                <el-tag :type="calendarSourceTagType(e.sourceType)" size="small">{{ calendarSourceTypeLabel(e.sourceType) }}</el-tag>
                {{ e.title }}
              </div>
              <div v-if="e.venueName" class="calendar-overview__muted">{{ e.venueName }}</div>
              <div class="calendar-overview__track-item-actions">
                <el-button v-if="canOpen(e)" size="small" text type="primary" @click="openEvent(e)">查看</el-button>
                <el-button v-if="canReschedule(e)" size="small" text type="primary" @click="openReschedule(e)">調整日期</el-button>
              </div>
            </li>
          </ul>
        </el-card>
      </div>
      <el-card v-else shadow="never"><el-empty description="這段期間沒有任何球隊軌道可顯示" /></el-card>
    </template>

    <!-- 列表檢視 -->
    <el-card v-else shadow="never">
      <el-table v-if="sortedListEvents.length > 0" :data="sortedListEvents" row-key="sourceId" class="calendar-overview__table">
        <el-table-column label="日期／時間" width="170">
          <template #default="{ row }">
            <div>{{ displayWhen(row) }}</div>
            <el-tag v-if="isConflicting(row)" type="warning" size="small">衝突</el-tag>
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
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <el-button v-if="canOpen(row)" size="small" text type="primary" @click="openEvent(row)">
              {{ row.sourceType === 'match' ? '查看賽事' : '查看活動' }}
            </el-button>
            <span v-else class="calendar-overview__muted">無檢視權限</span>
            <el-button v-if="canReschedule(row)" size="small" text type="primary" @click="openReschedule(row)">調整日期</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="這段期間找不到符合條件的賽事或活動" />
    </el-card>

    <el-dialog v-model="dayDialogVisible" :title="selectedDay ?? ''" width="560px">
      <el-empty v-if="selectedDayEvents.length === 0" description="這天沒有任何賽事或活動" />
      <ul v-else class="calendar-overview__day-list">
        <li v-for="e in selectedDayEvents" :key="eventKey(e)" class="calendar-overview__day-item">
          <div class="calendar-overview__day-item-main">
            <el-tag :type="calendarSourceTagType(e.sourceType)" size="small">{{ calendarSourceTypeLabel(e.sourceType) }}</el-tag>
            <span class="calendar-overview__day-item-title">{{ e.title }}</span>
            <el-tag v-if="isConflicting(e)" type="warning" size="small">衝突</el-tag>
          </div>
          <div class="calendar-overview__day-item-meta">
            {{ displayWhen(e) }}
            <span v-if="e.teamCodes.length || e.sourceType === 'custom'"> ・ {{ teamCodesLabel(e.teamCodes) }}</span>
            <span v-if="e.venueName"> ・ {{ e.venueName }}</span>
          </div>
          <div>
            <el-button v-if="canOpen(e)" size="small" text type="primary" @click="openEvent(e)">
              {{ e.sourceType === 'match' ? '查看賽事' : '查看活動' }}
            </el-button>
            <el-button v-if="canReschedule(e)" size="small" text type="primary" @click="openReschedule(e)">調整日期</el-button>
          </div>
        </li>
      </ul>
    </el-dialog>

    <CalendarRescheduleDialog
      v-model="rescheduleVisible"
      :event="rescheduleEvent"
      :initial-day="rescheduleDay"
      :team-label="(code: string) => teamLabelByCode.get(code) ?? code"
      @done="onRescheduled"
    />
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

.calendar-overview__hint {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.calendar-overview__conflict {
  margin-bottom: 12px;
}

.calendar-overview__conflict-list {
  margin: 8px 0 0;
  padding-left: 18px;
  font-size: 13px;
  line-height: 1.7;
}

.calendar-overview__table {
  width: 100%;
}

.calendar-overview__chip--conflict {
  outline: 2px solid var(--el-color-danger);
  outline-offset: -1px;
}

.calendar-overview__chip--draggable {
  cursor: grab;
}

.calendar-overview__tracks {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 260px), 1fr));
  gap: 12px;
  align-items: start;
}

.calendar-overview__track {
  border-top: 4px solid var(--admin-border);
  min-width: 0;
}

.calendar-overview__track-head {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
}

.calendar-overview__track-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex: none;
}

.calendar-overview__track-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.calendar-overview__track-list li {
  border-left: 3px solid var(--admin-border);
  padding-left: 8px;
  min-width: 0;
  word-break: break-word;
}

.calendar-overview__track-item--conflict {
  border-left-color: var(--el-color-danger) !important;
}

.calendar-overview__track-item-when {
  font-size: 12px;
  color: var(--admin-text-secondary);
  display: flex;
  align-items: center;
  gap: 6px;
}

.calendar-overview__track-item-title {
  font-size: 13px;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
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
