/**
 * `apps/api` C4「對手球隊」後台端點（`Features/AdminOpponentTeams`，主站規劃書 v3.31）。
 * 各俱樂部各自維護；權限碼沿用 `team.match.*`。
 * 寫入是 multipart：`payload`（JSON）＋選填檔案 `crest`（隊徽）。
 * 被賽事或積分榜引用時刪除回 409（訊息已含引用數量），重名回 409，畫面直接顯示 `AdminApiError.message`。
 */
import { apiRequest, apiUploadRequest } from './http'
import { buildMultipart } from './adminCommon'

export interface AdminOpponentTeamListItem {
  id: string
  name: string
  nameEn?: string | null
  crestUrl?: string | null
  crestThumbUrl?: string | null
  sortOrder: number
  usageCount: number
  updatedAt: string
}

export interface AdminOpponentTeamDetail extends AdminOpponentTeamListItem {
  crestWidth?: number | null
  crestHeight?: number | null
  matchCount: number
  standingCount: number
  createdAt: string
}

export interface SaveOpponentTeamPayload {
  name: string
  nameEn: string | null
  sortOrder: number
  removeCrest: boolean
}

const base = (club: string) => `/api/v1/admin/${club}/opponent-teams`

export function listAdminOpponentTeams(club: string): Promise<AdminOpponentTeamListItem[]> {
  return apiRequest<AdminOpponentTeamListItem[]>(base(club))
}

export function getAdminOpponentTeam(club: string, id: string): Promise<AdminOpponentTeamDetail> {
  return apiRequest<AdminOpponentTeamDetail>(`${base(club)}/${id}`)
}

export function createAdminOpponentTeam(club: string, payload: SaveOpponentTeamPayload, crest: File | null): Promise<AdminOpponentTeamDetail> {
  return apiUploadRequest<AdminOpponentTeamDetail>(base(club), buildMultipart(payload, { crest }), { method: 'POST' })
}

export function updateAdminOpponentTeam(club: string, id: string, payload: SaveOpponentTeamPayload, crest: File | null): Promise<AdminOpponentTeamDetail> {
  return apiUploadRequest<AdminOpponentTeamDetail>(`${base(club)}/${id}`, buildMultipart(payload, { crest }), { method: 'PUT' })
}

/** 被賽事或積分榜引用 → 409，訊息已說明引用數量。 */
export function deleteAdminOpponentTeam(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}`, { method: 'DELETE' })
}
