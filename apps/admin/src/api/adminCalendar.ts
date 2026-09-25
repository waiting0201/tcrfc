/**
 * `L1` 行事曆總覽／`L2` 自建事件後台端點（`Features/AdminCalendar`），對照
 * apps/api/README.md「S1-11」。上傳形狀比照 `adminPrograms.ts`：`multipart/form-data`，
 * 固定欄位 `payload`（JSON 文字）＋選填的 `file`（封面圖）。
 */
import { apiRequest, apiUploadRequest } from './http'

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
}

export interface ListAdminCalendarEventsParams {
  /** 省略時後端預設本月 1 日～下月 1 日。上限 366 天（含）。 */
  from?: string
  to?: string
  /** 球隊代碼、`club`（俱樂部活動）或省略（不限）。 */
  team?: string
  sourceType?: 'match' | 'custom'
}

export function listAdminCalendarEvents(club: string, params: ListAdminCalendarEventsParams = {}): Promise<AdminCalendarEventDto[]> {
  const search = new URLSearchParams()
  if (params.from) search.set('from', params.from)
  if (params.to) search.set('to', params.to)
  if (params.team) search.set('team', params.team)
  if (params.sourceType) search.set('sourceType', params.sourceType)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminCalendarEventDto[]>(`/api/v1/admin/${club}/calendar/events${query}`)
}

// ── L3 起始字典（唯讀，正式管理畫面留給 S2-6）───────────────────────────────────────

export interface AdminEventTypeDto {
  id: string
  code: string
  colour?: string | null
  icon?: string | null
  nameZh?: string | null
  nameEn?: string | null
}

export function listAdminCalendarEventTypes(club: string): Promise<AdminEventTypeDto[]> {
  return apiRequest<AdminEventTypeDto[]>(`/api/v1/admin/${club}/calendar/event-types`)
}

// ── L2 自建事件 CRUD ────────────────────────────────────────────────────────────────

export interface AdminCalendarEventLocaleContent {
  title: string
  description?: string | null
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
