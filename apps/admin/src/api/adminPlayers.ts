/**
 * `apps/api` C2「球員」後台端點（`Features/AdminPlayers`），對照 apps/api/README.md「S1-7」「S1-7a」。
 */
import { apiRequest, apiUploadRequest } from './http'

export interface AdminPlayerLocaleContentDto {
  name?: string | null
  bio?: string | null
}

export interface AdminPlayerContentInputDto {
  zh: AdminPlayerLocaleContentDto
  en?: AdminPlayerLocaleContentDto | null
}

export interface AdminPlayerListItemDto {
  id: string
  /** 網址代稱（後台稱「網址代稱」，前台球員頁網址與 App 深連結用）。 */
  slug?: string | null
  teamId: string
  teamCode: string
  shirtNo?: number | null
  position?: string | null
  birthOn?: string | null
  status?: string | null
  photoKey?: string | null
  /** 後端依 photoKey 附帶回傳的可顯示網址（大圖／160px 方形縮圖）。 */
  photoUrl?: string | null
  photoThumbUrl?: string | null
  /** 肖像同意狀態（S1-7a），必填三態字串，見 apps/admin/src/types/team.ts。 */
  portraitConsentStatus: string
  nameZh?: string | null
  nameEn?: string | null
  updatedAt: string
}

export interface AdminPlayerDetailDto {
  id: string
  slug?: string | null
  teamId: string
  teamCode: string
  shirtNo?: number | null
  position?: string | null
  birthOn?: string | null
  heightCm?: number | null
  weightKg?: number | null
  nationality?: string | null
  preferredFoot?: string | null
  joinedOn?: string | null
  status?: string | null
  photoKey?: string | null
  /** 後端依 photoKey 附帶回傳的可顯示網址（大圖／160px 方形縮圖）。 */
  photoUrl?: string | null
  photoThumbUrl?: string | null
  portraitConsentStatus: string
  zh: AdminPlayerLocaleContentDto
  en?: AdminPlayerLocaleContentDto | null
  createdAt: string
  updatedAt: string
}

export interface ListAdminPlayersParams {
  teamId?: string
  status?: string
}

/**
 * 🔴 `portraitConsentStatus` 一律明確帶出目前畫面上的值——後端的「省略」語意是「回退到最安全的
 * `not_consented`」（fail-closed，不是「維持不變」，見 `apps/api` `UpdateAdminPlayerRequest` 檔頭
 * 說明），跟新聞標籤／關聯那種「省略＝維持不變」完全不同，這裡絕對不能為了偷懶而省略這個欄位，
 * 否則會把使用者原本已經填好的同意狀態悄悄重置。
 */
export interface SavePlayerPayload {
  teamId: string
  /**
   * 網址代稱，選填：建立時省略＝後端自動產生；更新時省略＝維持原值（不重新產生）。
   * 格式 `[a-z0-9]+(-[a-z0-9]+)*`（錯 400）；同俱樂部重複 409「網址代稱重複」，後端訊息原樣顯示。
   */
  slug?: string
  shirtNo?: number | null
  position?: string | null
  birthOn?: string | null
  heightCm?: number | null
  weightKg?: number | null
  nationality?: string | null
  preferredFoot?: string | null
  joinedOn?: string | null
  status?: string | null
  portraitConsentStatus: string
  content: AdminPlayerContentInputDto
}

export interface UpdatePlayerPayload extends SavePlayerPayload {
  removePhoto: boolean
}

export function listAdminPlayers(club: string, params: ListAdminPlayersParams = {}): Promise<AdminPlayerListItemDto[]> {
  const search = new URLSearchParams()
  if (params.teamId) search.set('teamId', params.teamId)
  if (params.status) search.set('status', params.status)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminPlayerListItemDto[]>(`/api/v1/admin/${club}/players${query}`)
}

export function getAdminPlayer(club: string, id: string): Promise<AdminPlayerDetailDto> {
  return apiRequest<AdminPlayerDetailDto>(`/api/v1/admin/${club}/players/${id}`)
}

function buildPlayerFormData(payload: SavePlayerPayload | UpdatePlayerPayload, file: File | null): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (file) form.append('file', file)
  return form
}

export function createAdminPlayer(club: string, payload: SavePlayerPayload, photoFile: File | null): Promise<AdminPlayerDetailDto> {
  return apiUploadRequest<AdminPlayerDetailDto>(`/api/v1/admin/${club}/players`, buildPlayerFormData(payload, photoFile), {
    method: 'POST',
  })
}

export function updateAdminPlayer(
  club: string,
  id: string,
  payload: UpdatePlayerPayload,
  photoFile: File | null,
): Promise<AdminPlayerDetailDto> {
  return apiUploadRequest<AdminPlayerDetailDto>(`/api/v1/admin/${club}/players/${id}`, buildPlayerFormData(payload, photoFile), {
    method: 'PUT',
  })
}

// ── 賽季數據（apps/api/README.md「後台欄位串接稽核的後端修正」B-13）─────────────────────────
// 權限：檢視 `team.player.view`、寫入 `team.player.update`＋球員的球隊列級授權。
// 「這一列存在」就是手動（`player_season_stats` 沒有 source 欄位），所以清除＝刪除該列，
// 公開端自動回到賽事彙總。

export type PlayerSeasonStatSource = 'manual' | 'auto' | 'none'

export interface PlayerSeasonStatValues {
  appearances: number
  goals: number
  /** 自動彙總沒有助攻資料來源，恆為 null。 */
  assists: number | null
  yellowCards: number
  redCards: number
}

export interface AdminPlayerSeasonStatDto {
  seasonId: string
  seasonCode: string
  startOn: string
  endOn: string
  source: PlayerSeasonStatSource
  manual?: PlayerSeasonStatValues | null
  auto?: PlayerSeasonStatValues | null
}

export interface AdminPlayerSeasonStatsDto {
  playerId: string
  items: AdminPlayerSeasonStatDto[]
}

export interface SetPlayerSeasonStatPayload {
  appearances: number
  goals: number
  assists: number
  yellowCards: number
  redCards: number
}

export function getAdminPlayerSeasonStats(club: string, playerId: string): Promise<AdminPlayerSeasonStatsDto> {
  return apiRequest<AdminPlayerSeasonStatsDto>(`/api/v1/admin/${club}/players/${playerId}/season-stats`)
}

export function setAdminPlayerSeasonStat(
  club: string,
  playerId: string,
  seasonId: string,
  payload: SetPlayerSeasonStatPayload,
): Promise<AdminPlayerSeasonStatDto> {
  return apiRequest<AdminPlayerSeasonStatDto>(`/api/v1/admin/${club}/players/${playerId}/season-stats/${seasonId}`, {
    method: 'PUT',
    body: payload,
  })
}

export function clearAdminPlayerSeasonStat(club: string, playerId: string, seasonId: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/players/${playerId}/season-stats/${seasonId}`, { method: 'DELETE' })
}
