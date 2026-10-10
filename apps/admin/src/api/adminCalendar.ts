/**
 * `L1` 行事曆總覽／`L2` 自建事件後台端點（`Features/AdminCalendar`），對照
 * apps/api/README.md「S1-11」。上傳形狀比照 `adminPrograms.ts`：`multipart/form-data`，
 * 固定欄位 `payload`（JSON 文字）＋選填的 `file`（封面圖）。
 */
import { apiBlobRequest, apiRequest, apiUploadRequest, API_BASE_URL, AdminApiError } from './http'
import { getAccessToken } from '@/auth/session'
import { buildQuery } from './adminCommon'
import type { MatchCsvImportResultDto } from './adminMatches'

// ── L1 總覽（合併讀取，唯讀）────────────────────────────────────────────────────────

export interface AdminCalendarEventDto {
  sourceType: 'match' | 'custom'
  sourceId: string
  startsAt: string
  endsAt?: string | null
  isAllDay: boolean
  title: string
  teamCodes: string[]
  venueName?: string | null
  /** 僅 `custom` 有值。 */
  eventTypeCode?: string | null
  /** 僅 `match` 有值：`scheduled`／`live`／`played`／`postponed`／`cancelled`。 */
  status?: string | null
  /** 僅 `match` 有值：`主場`／`客場`。 */
  homeAway?: string | null
  /** 僅 `custom` 有值。 */
  isPublic?: boolean | null
  /** 場地 id（沒有指定場地時為空），用於「依場地篩選」與衝突偵測。 */
  venueId?: string | null
  /** 僅 `match` 有值：開賽時間 `HH:mm`（賽事的 `startsAt` 只有日期）。 */
  kickoff?: string | null
  /** 僅 `match` 有值：賽事類型標籤（聯賽／盃賽…的代碼）。 */
  competitionTag?: string | null
}

export interface ListAdminCalendarEventsParams {
  /** 省略時後端預設本月 1 日～下月 1 日。上限 366 天（含）。 */
  from?: string
  to?: string
  /** 球隊代碼、`club`（俱樂部活動）或省略（不限）。 */
  team?: string
  /** `trial` 只有「試訓同步到行事曆」開關開啟時才有資料。 */
  sourceType?: 'match' | 'custom' | 'trial'
  venueId?: string
  /** 賽事狀態；依狀態篩選時自建活動不會出現。 */
  status?: string
  /** 賽事類型（聯賽／盃賽／友誼賽／其他）或自建活動類型代碼。 */
  type?: string
}

export function listAdminCalendarEvents(club: string, params: ListAdminCalendarEventsParams = {}): Promise<AdminCalendarEventDto[]> {
  return apiRequest<AdminCalendarEventDto[]>(`/api/v1/admin/${club}/calendar/events${buildQuery({ ...params })}`)
}

// ── L1 進階：分軌、衝突偵測、拖曳改期 ───────────────────────────────────────────────

export interface AdminCalendarTrackDto {
  teamId: string
  teamCode: string
  name: string
  colour?: string | null
  sortOrder: number
  events: AdminCalendarEventDto[]
}

export interface AdminCalendarConflictDto {
  reasons: ('venue' | 'team')[]
  /** 日常中文，可直接顯示。 */
  description: string
  venueName?: string | null
  sharedTeamCodes: string[]
  first: AdminCalendarEventDto
  second: AdminCalendarEventDto
}

export interface AdminCalendarTracksDto {
  from: string
  toExclusive: string
  tracks: AdminCalendarTrackDto[]
  clubEvents: AdminCalendarEventDto[]
  conflicts: AdminCalendarConflictDto[]
}

export function getAdminCalendarTracks(club: string, params: { from?: string; to?: string } = {}): Promise<AdminCalendarTracksDto> {
  return apiRequest<AdminCalendarTracksDto>(`/api/v1/admin/${club}/calendar/tracks${buildQuery(params)}`)
}

export function listAdminCalendarConflicts(club: string, params: { from?: string; to?: string } = {}): Promise<AdminCalendarConflictDto[]> {
  return apiRequest<AdminCalendarConflictDto[]>(`/api/v1/admin/${club}/calendar/conflicts${buildQuery(params)}`)
}

export interface RescheduleMatchPayload {
  matchOn: string
  /** 省略＝維持、空字串＝清除、`HH:mm`＝新時間。 */
  kickoff?: string
  markAsPostponed?: boolean
  acknowledgeConflicts?: boolean
}

export interface RescheduleResultDto {
  saved: boolean
  sourceType: string
  sourceId: string
  startsAt: string
  kickoff?: string | null
  status?: string | null
  originalMatchOn?: string | null
  conflicts: AdminCalendarConflictDto[]
  /** 恆為 false：全系統目前沒有寄信通路。 */
  notificationSent: boolean
}

export function rescheduleAdminMatch(club: string, id: string, payload: RescheduleMatchPayload): Promise<RescheduleResultDto> {
  return apiRequest<RescheduleResultDto>(`/api/v1/admin/${club}/calendar/matches/${id}/reschedule`, { method: 'POST', body: payload })
}

export interface MoveCustomEventPayload {
  /** UTC ISO 8601。 */
  startsAt: string
  endsAt?: string | null
  isAllDay?: boolean
  acknowledgeConflicts?: boolean
}

export function moveAdminCustomEvent(club: string, id: string, payload: MoveCustomEventPayload): Promise<RescheduleResultDto> {
  return apiRequest<RescheduleResultDto>(`/api/v1/admin/${club}/calendar/custom-events/${id}/move`, { method: 'POST', body: payload })
}

/** 改期遇到衝突時（409），從錯誤本文取出衝突清單；不是衝突錯誤就回 `null`。 */
export function conflictsFromError(error: unknown): AdminCalendarConflictDto[] | null {
  if (!(error instanceof AdminApiError) || error.kind !== 'schedule-conflict') return null
  const body = error.body as { conflicts?: AdminCalendarConflictDto[] } | null | undefined
  return body?.conflicts ?? []
}

// ── L3 起始字典（唯讀，正式管理畫面留給 S2-6）───────────────────────────────────────

export interface AdminEventTypeDto {
  id: string
  code: string
  colour?: string | null
  icon?: string | null
  nameZh?: string | null
  nameEn?: string | null
  isPublic?: boolean
  sortOrder?: number
  /** 有幾個自建活動使用這個類型（大於 0 時不能刪除）。 */
  usageCount?: number
}

export function listAdminCalendarEventTypes(club: string): Promise<AdminEventTypeDto[]> {
  return apiRequest<AdminEventTypeDto[]>(`/api/v1/admin/${club}/calendar/event-types`)
}

// ── L2 自建事件 CRUD ────────────────────────────────────────────────────────────────

export interface AdminCalendarEventLocaleContent {
  title: string
  description?: string | null
  /** 圖片說明（替代文字，≤200 字）。 */
  coverAlt?: string | null
}

export interface AdminCalendarEventContentInput {
  zh: AdminCalendarEventLocaleContent
  en?: AdminCalendarEventLocaleContent | null
}

export interface AdminCalendarCustomEventListItemDto {
  id: string
  startsAt: string
  endsAt?: string | null
  isAllDay: boolean
  repeatRule?: string | null
  isPublic: boolean
  coverKey?: string | null
  /** 後端依 coverKey 附帶回傳的可顯示網址（大圖／160px 方形縮圖）。 */
  coverUrl?: string | null
  coverThumbUrl?: string | null
  coverWidth?: number | null
  coverHeight?: number | null
  teamCodes: string[]
  eventTypeCode?: string | null
  titleZh?: string | null
  titleEn?: string | null
  updatedAt: string
}

export interface AdminCalendarCustomEventDetailDto {
  id: string
  eventTypeId?: string | null
  venueId?: string | null
  startsAt: string
  endsAt?: string | null
  isAllDay: boolean
  repeatRule?: string | null
  /** `date`（`YYYY-MM-DD`）。 */
  repeatUntil?: string | null
  exceptionDates: string[]
  isPublic: boolean
  coverKey?: string | null
  /** 後端依 coverKey 附帶回傳的可顯示網址（大圖／160px 方形縮圖）。 */
  coverUrl?: string | null
  coverThumbUrl?: string | null
  coverWidth?: number | null
  coverHeight?: number | null
  ctaUrl?: string | null
  teamIds: string[]
  teamCodes: string[]
  zh: AdminCalendarEventLocaleContent
  en?: AdminCalendarEventLocaleContent | null
  createdAt: string
  updatedAt: string
}

export interface ListAdminCalendarCustomEventsParams {
  team?: string
}

export function listAdminCalendarCustomEvents(
  club: string,
  params: ListAdminCalendarCustomEventsParams = {},
): Promise<AdminCalendarCustomEventListItemDto[]> {
  const search = new URLSearchParams()
  if (params.team) search.set('team', params.team)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminCalendarCustomEventListItemDto[]>(`/api/v1/admin/${club}/calendar/custom-events${query}`)
}

export function getAdminCalendarCustomEvent(club: string, id: string): Promise<AdminCalendarCustomEventDetailDto> {
  return apiRequest<AdminCalendarCustomEventDetailDto>(`/api/v1/admin/${club}/calendar/custom-events/${id}`)
}

/** `repeatRule` 省略／`null`＝不重複；非空時值域為 `weekly`／`biweekly`／`monthly`。
 * `teamIds` 省略或空陣列＝「俱樂部活動」（不強制至少一支，跟 C4 賽事語意不同）。 */
export interface SaveCalendarCustomEventPayload {
  eventTypeId?: string | null
  venueId?: string | null
  startsAt: string
  endsAt?: string | null
  isAllDay: boolean
  repeatRule?: string | null
  repeatUntil?: string | null
  exceptionDates?: string[]
  isPublic: boolean
  ctaUrl?: string | null
  teamIds?: string[]
  content: AdminCalendarEventContentInput
}

export interface UpdateCalendarCustomEventPayload extends SaveCalendarCustomEventPayload {
  /** true＝移除目前的封面圖，不接受同時夾帶新檔案。 */
  removeCover: boolean
}

function buildCalendarEventFormData(payload: SaveCalendarCustomEventPayload | UpdateCalendarCustomEventPayload, file: File | null): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (file) form.append('file', file)
  return form
}

export function createAdminCalendarCustomEvent(
  club: string,
  payload: SaveCalendarCustomEventPayload,
  coverFile: File | null,
): Promise<AdminCalendarCustomEventDetailDto> {
  return apiUploadRequest<AdminCalendarCustomEventDetailDto>(
    `/api/v1/admin/${club}/calendar/custom-events`,
    buildCalendarEventFormData(payload, coverFile),
    { method: 'POST' },
  )
}

export function updateAdminCalendarCustomEvent(
  club: string,
  id: string,
  payload: UpdateCalendarCustomEventPayload,
  coverFile: File | null,
): Promise<AdminCalendarCustomEventDetailDto> {
  return apiUploadRequest<AdminCalendarCustomEventDetailDto>(
    `/api/v1/admin/${club}/calendar/custom-events/${id}`,
    buildCalendarEventFormData(payload, coverFile),
    { method: 'PUT' },
  )
}

export function deleteAdminCalendarCustomEvent(club: string, id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/calendar/custom-events/${id}`, { method: 'DELETE' })
}

// ── L3 分類與顯示設定 ────────────────────────────────────────────────────────────────

export interface CalendarTeamSettingDto {
  teamId: string
  code: string
  type: string
  teamNameZh: string
  displayNameZh?: string | null
  displayNameEn?: string | null
  /** 覆寫色（`#RRGGBB`），空＝沿用球隊本身。 */
  colour?: string | null
  effectiveColour?: string | null
  sortOrder?: number | null
  effectiveSortOrder: number
  isPublic: boolean
}

export interface CalendarSettingsDto {
  defaultView: 'list' | 'month'
  defaultRange: 'upcoming' | 'this_month' | 'next_30_days' | 'season'
  /** `all` 或隊別代碼。 */
  defaultTeamCode: string
  homeTeamCodes: string[]
  firstTeamCode?: string | null
  syncTrials: boolean
  teams: CalendarTeamSettingDto[]
  eventTypes: AdminEventTypeDto[]
}

export interface SaveCalendarSettingsPayload {
  defaultView: string
  defaultRange: string
  defaultTeamCode?: string | null
  homeTeamCodes?: string[]
  firstTeamCode?: string | null
  syncTrials: boolean
}

export interface SaveCalendarTeamSettingPayload {
  teamId: string
  displayNameZh?: string | null
  displayNameEn?: string | null
  colour?: string | null
  sortOrder?: number | null
  isPublic: boolean
}

export interface CalendarEventTypeIconDto {
  code: string
  label: string
}

export interface SaveCalendarEventTypePayload {
  code: string
  nameZh: string
  nameEn?: string | null
  colour?: string | null
  icon?: string | null
  isPublic: boolean
  sortOrder: number
}

const calendarBase = (club: string) => `/api/v1/admin/${club}/calendar`

export function getCalendarSettings(club: string): Promise<CalendarSettingsDto> {
  return apiRequest<CalendarSettingsDto>(`${calendarBase(club)}/settings`)
}

export function updateCalendarSettings(club: string, payload: SaveCalendarSettingsPayload): Promise<CalendarSettingsDto> {
  return apiRequest<CalendarSettingsDto>(`${calendarBase(club)}/settings`, { method: 'PUT', body: payload })
}

export function updateCalendarTeamSettings(club: string, teams: SaveCalendarTeamSettingPayload[]): Promise<CalendarSettingsDto> {
  return apiRequest<CalendarSettingsDto>(`${calendarBase(club)}/settings/teams`, { method: 'PUT', body: { teams } })
}

export function listCalendarEventTypeIcons(club: string): Promise<CalendarEventTypeIconDto[]> {
  return apiRequest<CalendarEventTypeIconDto[]>(`${calendarBase(club)}/event-types/icons`)
}

export function createCalendarEventType(club: string, payload: SaveCalendarEventTypePayload): Promise<AdminEventTypeDto> {
  return apiRequest<AdminEventTypeDto>(`${calendarBase(club)}/event-types`, { method: 'POST', body: payload })
}

export function updateCalendarEventType(club: string, id: string, payload: SaveCalendarEventTypePayload): Promise<AdminEventTypeDto> {
  return apiRequest<AdminEventTypeDto>(`${calendarBase(club)}/event-types/${id}`, { method: 'PUT', body: payload })
}

export function deleteCalendarEventType(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${calendarBase(club)}/event-types/${id}`, { method: 'DELETE' })
}

export function reorderCalendarEventTypes(club: string, ids: string[]): Promise<void> {
  return apiRequest<void>(`${calendarBase(club)}/event-types/order`, { method: 'PUT', body: { ids } })
}

// ── L4 訂閱與匯出 ────────────────────────────────────────────────────────────────────

export interface CalendarFeedDto {
  /** `all` 或隊別代碼。 */
  feedKey: string
  label: string
  httpsUrl: string
  webcalUrl: string
  subscribers30d: number
  subscribers7d: number
  lastFetchedOn?: string | null
  isPublic: boolean
}

export interface CalendarSubscriptionsDto {
  feeds: CalendarFeedDto[]
  /** 訂閱數是估計值的說明，畫面照顯示。 */
  statsNote: string
}

export function getCalendarSubscriptions(club: string): Promise<CalendarSubscriptionsDto> {
  return apiRequest<CalendarSubscriptionsDto>(`${calendarBase(club)}/subscriptions`)
}

export interface ExportCalendarParams {
  format: 'csv' | 'ics'
  from?: string
  to?: string
  team?: string
  sourceType?: string
  venueId?: string
  status?: string
  type?: string
}

export function exportCalendar(club: string, params: ExportCalendarParams): Promise<{ blob: Blob; filename: string | null }> {
  return apiBlobRequest(`${calendarBase(club)}/export${buildQuery({ ...params })}`)
}

/** 整季賽程 CSV 匯入（與賽程與賽果的匯入同一套規則）：CSV 原始內容直接當本文，不是 JSON 也不是 multipart。 */
export async function importCalendarMatchesCsv(club: string, file: File): Promise<MatchCsvImportResultDto> {
  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`
  const text = await file.text()
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${calendarBase(club)}/matches/import`, { method: 'POST', headers, credentials: 'include', body: text })
  } catch {
    throw new AdminApiError('network', '無法連線到後台服務，請確認網路是否正常。')
  }
  const raw = await response.text()
  let parsed: MatchCsvImportResultDto | null = null
  try {
    parsed = raw ? (JSON.parse(raw) as MatchCsvImportResultDto) : null
  } catch {
    parsed = null
  }
  if ((response.status === 200 || response.status === 400) && parsed && Array.isArray(parsed.errors)) return parsed
  if (response.status === 403) throw new AdminApiError('forbidden', '你沒有權限匯入賽程。', { status: 403 })
  if (response.status === 400) throw new AdminApiError('validation', '檔案格式錯誤，請確認是賽程匯入用的 CSV。', { status: 400 })
  throw new AdminApiError('server', '匯入失敗，請稍後再試。', { status: response.status })
}
