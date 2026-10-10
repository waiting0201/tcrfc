/**
 * F2 球迷會活動後台端點，對照 apps/api/README.md「C1」節「F2 球迷會活動」。
 * 時間戳是 UTC（JSON 不帶時區記號）；送出時一律用帶 `Z` 的 ISO 字串。
 */
import { apiRequest, apiUploadRequest, buildMultipart, buildQuery, putOrder, type BilingualContentInput } from './adminCommon'

const base = (club: string) => `/api/v1/admin/${club}/fan-events`

export type FanEventStatus = 'draft' | 'published'
export type FanEventRegistrationStatus = 'registered' | 'waitlist' | 'cancelled' | 'attended'

export const FAN_EVENT_REGISTRATION_STATUS_OPTIONS: { value: FanEventRegistrationStatus; label: string }[] = [
  { value: 'registered', label: '已報名' },
  { value: 'waitlist', label: '候補' },
  { value: 'attended', label: '已到場' },
  { value: 'cancelled', label: '已取消' },
]

export interface FanEventListItemDto {
  id: string
  slug: string
  startsAt?: string | null
  endsAt?: string | null
  registrationDeadlineAt?: string | null
  capacity?: number | null
  isPaidMembersOnly: boolean
  status: FanEventStatus
  statusLabel: string
  coverKey?: string | null
  coverThumbUrl?: string | null
  coverWidth?: number | null
  coverHeight?: number | null
  venueId?: string | null
  nameZh?: string | null
  nameEn?: string | null
  registeredCount: number
  waitlistCount: number
  isRegistrationOpen: boolean
  updatedAt: string
}

export interface FanEventLocale {
  name: string
  description?: string | null
  location?: string | null
  /** 封面圖片替代文字（選填，上限 200 字；錯誤鍵 coverAltZh／coverAltEn） */
  coverAlt?: string | null
}

export interface FanEventImageDto {
  id: string
  imageKey?: string | null
  imageUrl?: string | null
  imageThumbUrl?: string | null
  imageWidth?: number | null
  imageHeight?: number | null
  altZh?: string | null
  altEn?: string | null
  sortOrder: number
}

export interface FanEventDetailDto extends FanEventListItemDto {
  coverUrl?: string | null
  venueName?: string | null
  zh: FanEventLocale
  en?: FanEventLocale | null
  images: FanEventImageDto[]
  articles: { id: string; slug: string; titleZh?: string | null; status: string }[]
  createdAt: string
}

export interface SaveFanEventPayload {
  slug?: string
  startsAt?: string | null
  endsAt?: string | null
  registrationDeadlineAt?: string | null
  capacity?: number | null
  isPaidMembersOnly: boolean
  venueId?: string | null
  status: FanEventStatus
  removeCover?: boolean
  articleIds?: string[]
  content: BilingualContentInput<FanEventLocale>
}

export interface FanEventRegistrationDto {
  id: string
  memberId?: string | null
  memberNo?: string | null
  isMember: boolean
  applicantName?: string | null
  phone?: string | null
  email?: string | null
  status: FanEventRegistrationStatus
  statusLabel: string
  note?: string | null
  createdAt: string
  isMasked: boolean
}

export function listFanEvents(club: string, params: { status?: string; from?: string; to?: string; keyword?: string } = {}): Promise<FanEventListItemDto[]> {
  return apiRequest<FanEventListItemDto[]>(`${base(club)}${buildQuery(params)}`)
}
export function getFanEvent(club: string, id: string): Promise<FanEventDetailDto> {
  return apiRequest<FanEventDetailDto>(`${base(club)}/${id}`)
}
export function createFanEvent(club: string, payload: SaveFanEventPayload, cover: File | null): Promise<FanEventDetailDto> {
  return apiUploadRequest<FanEventDetailDto>(base(club), buildMultipart(payload, { cover }), { method: 'POST' })
}
export function updateFanEvent(club: string, id: string, payload: SaveFanEventPayload, cover: File | null): Promise<FanEventDetailDto> {
  return apiUploadRequest<FanEventDetailDto>(`${base(club)}/${id}`, buildMultipart(payload, { cover }), { method: 'PUT' })
}
export function deleteFanEvent(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}`, { method: 'DELETE' })
}
export function addFanEventImages(club: string, id: string, files: File[]): Promise<FanEventDetailDto> {
  const form = new FormData()
  for (const f of files) form.append('files', f)
  return apiUploadRequest<FanEventDetailDto>(`${base(club)}/${id}/images`, form, { method: 'POST' })
}
export function deleteFanEventImage(club: string, id: string, imageId: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}/images/${imageId}`, { method: 'DELETE' })
}
/** 圖集圖片說明（替代文字），各 ≤200 字、空白清為無；回傳更新後的活動詳情。 */
export function updateFanEventImageAlt(club: string, id: string, imageId: string, altZh: string | null, altEn: string | null): Promise<FanEventDetailDto> {
  return apiRequest<FanEventDetailDto>(`${base(club)}/${id}/images/${imageId}`, { method: 'PUT', body: { altZh, altEn } })
}
export function reorderFanEventImages(club: string, id: string, ids: string[]): Promise<void> {
  return putOrder(`${base(club)}/${id}/images/order`, ids)
}
export function listFanEventRegistrations(club: string, id: string, params: { status?: string; keyword?: string } = {}): Promise<FanEventRegistrationDto[]> {
  return apiRequest<FanEventRegistrationDto[]>(`${base(club)}/${id}/registrations${buildQuery(params)}`)
}
export function createFanEventRegistration(
  club: string,
  id: string,
  body: { memberId?: string; applicantName?: string; phone?: string; email?: string; note?: string },
): Promise<FanEventRegistrationDto> {
  return apiRequest<FanEventRegistrationDto>(`${base(club)}/${id}/registrations`, { method: 'POST', body })
}
export function updateFanEventRegistration(
  club: string,
  id: string,
  registrationId: string,
  body: { status: FanEventRegistrationStatus; note?: string },
): Promise<FanEventRegistrationDto> {
  return apiRequest<FanEventRegistrationDto>(`${base(club)}/${id}/registrations/${registrationId}`, { method: 'PUT', body })
}
