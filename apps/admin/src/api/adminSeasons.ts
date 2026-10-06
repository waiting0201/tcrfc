/**
 * 俱樂部範圍的「賽季」維護，對照 `Features/AdminSeasons/AdminSeasonDtos.cs`
 * （apps/api/README.md「後台欄位串接稽核的後端修正」A-12）。
 * 權限碼 `team.match.view|create|update|delete`；寫入另要求球隊列級授權為整個俱樂部。
 * 清單網址與先前 `adminCompetitions.listAdminSeasons` 相同（`GET /admin/{club}/seasons`），
 * 那邊的下拉選單取用方式不變。
 */
import { apiRequest } from './http'

export interface AdminSeasonUsageDto {
  /** 日常中文（賽事系列、賽事、積分榜、榮譽、球員賽季數據、會籍、會籍方案）。 */
  label: string
  count: number
}

export interface AdminSeasonDto {
  id: string
  code: string
  startOn: string
  endOn: string
  inUse: boolean
  usage: AdminSeasonUsageDto[]
  updatedAt: string
}

export interface SaveSeasonPayload {
  code: string
  startOn: string
  endOn: string
}

export function listSeasonsForManage(club: string): Promise<AdminSeasonDto[]> {
  return apiRequest<AdminSeasonDto[]>(`/api/v1/admin/${club}/seasons`)
}

export function createAdminSeason(club: string, payload: SaveSeasonPayload): Promise<AdminSeasonDto> {
  return apiRequest<AdminSeasonDto>(`/api/v1/admin/${club}/seasons`, { method: 'POST', body: payload })
}

export function updateAdminSeason(club: string, id: string, payload: SaveSeasonPayload): Promise<AdminSeasonDto> {
  return apiRequest<AdminSeasonDto>(`/api/v1/admin/${club}/seasons/${id}`, { method: 'PUT', body: payload })
}

export function deleteAdminSeason(club: string, id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/seasons/${id}`, { method: 'DELETE' })
}
