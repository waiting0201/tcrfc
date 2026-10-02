/**
 * `apps/api` 全站共用場地主檔的唯讀清單端點（`Features/AdminVenues`，S1-12d 後續補完），對照
 * apps/api/README.md「S1-12d」節「場地清單端點」與 `Features/AdminVenues/AdminVenuesDtos.cs`。
 *
 * 🔴 **回應內容與路由的 `{club}` 段無關**：`Venue` 本身刻意不帶 `club_id`（docs/12 §4.7：場地是
 * 地理實體，兩俱樂部可能共用同一座球場），這支端點回傳的是**全站**場地清單，不是「這個俱樂部的
 * 場地」——任何俱樂部呼叫都會拿到同一份清單，路由段只是借用既有俱樂部授權管線。
 *
 * 權限：`site.fact.view`（`I` 網站設定挑主場）或 `team.match.view`（`C4` 賽程挑比賽地點）任一
 * 通過即可，未新增權限碼。
 */
import { apiRequest, apiUploadRequest } from './http'
import { buildMultipart } from './adminCommon'

/** 對照 `AdminVenueListItemDto`。 */
export interface AdminVenueListItemDto {
  id: string
  nameZh: string
  nameEn?: string | null
  address?: string | null
}

export function listAdminVenues(club: string): Promise<AdminVenueListItemDto[]> {
  return apiRequest<AdminVenueListItemDto[]>(`/api/v1/admin/${club}/venues`)
}

// ───────────── H 批：場地管理（apps/api/README.md「H 批」§5） ─────────────

/** 清單新增 `lat`／`lng`／`photoUrl`（原欄位不變）。 */
export interface AdminVenueRow extends AdminVenueListItemDto {
  lat?: number | null
  lng?: number | null
  photoUrl?: string | null
}

export interface AdminVenueLocale {
  name: string
  address?: string | null
  directions?: string | null
  photoAlt?: string | null
}

export interface AdminVenueDetail {
  id: string
  lat?: number | null
  lng?: number | null
  photoUrl?: string | null
  photoWidth?: number | null
  photoHeight?: number | null
  sortOrder: number
  zh: AdminVenueLocale
  en?: AdminVenueLocale | null
  usageCount: number
  isHomeVenue: boolean
  updatedAt: string
}

export interface SaveVenuePayload {
  zh: AdminVenueLocale
  en: AdminVenueLocale | null
  lat: number | null
  lng: number | null
  sortOrder: number
  removePhoto: boolean
}

const venues = (club: string) => `/api/v1/admin/${club}/venues`

export function listAdminVenueRows(club: string): Promise<AdminVenueRow[]> {
  return apiRequest<AdminVenueRow[]>(venues(club))
}

export function getAdminVenue(club: string, id: string): Promise<AdminVenueDetail> {
  return apiRequest<AdminVenueDetail>(`${venues(club)}/${id}`)
}

export function createAdminVenue(club: string, payload: SaveVenuePayload, photo: File | null): Promise<AdminVenueDetail> {
  return apiUploadRequest<AdminVenueDetail>(venues(club), buildMultipart(payload, { photo }), { method: 'POST' })
}

export function updateAdminVenue(club: string, id: string, payload: SaveVenuePayload, photo: File | null): Promise<AdminVenueDetail> {
  return apiUploadRequest<AdminVenueDetail>(`${venues(club)}/${id}`, buildMultipart(payload, { photo }), { method: 'PUT' })
}

/** 被賽事／梯次／試訓／行事曆事件／球迷會活動引用或登記為主場 → 409，訊息說明原因。 */
export function deleteAdminVenue(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${venues(club)}/${id}`, { method: 'DELETE' })
}

/** 「由地址定位」預覽：404 查無、503 服務未啟用或故障（`body.code === 'geocoder_unavailable'` 為暫時故障）；不寫入任何資料。 */
export function locateAdminVenueAddress(club: string, address: string): Promise<{ lat: number; lng: number }> {
  return apiRequest(`${venues(club)}/locate`, { method: 'POST', body: { address } })
}
