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
import { apiRequest } from './http'

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
