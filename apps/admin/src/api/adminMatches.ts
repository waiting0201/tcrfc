/**
 * `apps/api` C4「賽程與賽果」後台端點（`Features/AdminMatches`），對照
 * apps/api/README.md「S1-8」。權限碼 `team.match.*`，寫入端點套用球隊列級授權
 * （`academy_only`／`own_teams`）——後端擋下時回傳的 403 訊息已經是完整中文句子
 * （例如「你的球隊授權範圍不允許為這些球隊建立賽事。」），畫面直接顯示 `AdminApiError.message`
 * 即可，不需要另外轉譯或補充權限碼說明。
 */
import { apiRequest } from './http'
import { API_BASE_URL, AdminApiError } from './http'
import { getAccessToken } from '@/auth/session'

// ── 進球／卡牌／出賽名單（比分結果，內嵌在賽事本身，不另開 CRUD）─────────────────────────────

export interface AdminMatchGoalInput {
  playerId: string
  minute?: number | null
  goalType?: string | null
}

export interface AdminMatchGoalDto {
  id: string
  playerId: string
  playerName?: string | null
  minute?: number | null
  goalType?: string | null
}

export interface AdminMatchCardInput {
  playerId: string
  cardType: string
  minute?: number | null
}

export interface AdminMatchCardDto {
  id: string
  playerId: string
  playerName?: string | null
  cardType: string
  minute?: number | null
}

export interface AdminMatchLineupInput {
  playerId: string
  isStarter: boolean
}

export interface AdminMatchLineupDto {
  id: string
  playerId: string
  playerName?: string | null
  isStarter: boolean
}

// ── 賽事本身 ─────────────────────────────────────────────────────────────────────

export interface AdminMatchListItemDto {
  id: string
  seasonId: string
  seasonCode: string
  competitionId?: string | null
  competitionCode?: string | null
  teamIds: string[]
  teamCodes: string[]
  matchOn: string
  kickoff?: string | null
  homeAway?: string | null
  opponent?: string | null
  competitionTag?: string | null
  status?: string | null
  scoreHome?: number | null
  scoreAway?: number | null
  roundNo?: number | null
  matchNo?: number | null
  updatedAt: string
}

export interface AdminMatchDetailDto {
  id: string
  seasonId: string
  seasonCode: string
  competitionId?: string | null
  competitionCode?: string | null
  venueId?: string | null
  teamIds: string[]
  teamCodes: string[]
  matchOn: string
  kickoff?: string | null
  homeAway?: string | null
  opponent?: string | null
  opponentEn?: string | null
  venue?: string | null
  venueEn?: string | null
  goals: AdminMatchGoalDto[]
  cards: AdminMatchCardDto[]
  lineups: AdminMatchLineupDto[]
  competitionTag?: string | null
  status: string
  scoreHome?: number | null
  scoreAway?: number | null
  roundNo?: number | null
  matchNo?: number | null
  originalMatchOn?: string | null
  originalKickoff?: string | null
  createdAt: string
  updatedAt: string
}

/**
 * 🔴 `originalMatchOn`／`originalKickoff` 只有狀態為「延賽」時才可以有值，非延賽時後端會
 * 擋下（`AdminMatchValidationException`）——畫面在送出前先做同一個檢查，避免使用者填完整份
 * 表單才在儲存那一刻被拒絕。
 */
export interface SaveMatchPayload {
  seasonId: string
  competitionId?: string | null
  venueId?: string | null
  teamIds: string[]
  matchOn: string
  kickoff?: string | null
  homeAway?: string | null
  opponent: string
  opponentEn?: string | null
  venue?: string | null
  venueEn?: string | null
  competitionTag?: string | null
  status: string
  scoreHome?: number | null
  scoreAway?: number | null
  roundNo?: number | null
  matchNo?: number | null
  originalMatchOn?: string | null
  originalKickoff?: string | null
  /** 省略（undefined）＝維持既有內容不變；提供陣列（含空陣列）＝整份取代。 */
  goals?: AdminMatchGoalInput[]
  cards?: AdminMatchCardInput[]
  lineups?: AdminMatchLineupInput[]
}

export interface ListAdminMatchesParams {
  seasonId?: string
  teamId?: string
  status?: string
}

export function listAdminMatches(club: string, params: ListAdminMatchesParams = {}): Promise<AdminMatchListItemDto[]> {
  const search = new URLSearchParams()
  if (params.seasonId) search.set('seasonId', params.seasonId)
  if (params.teamId) search.set('teamId', params.teamId)
  if (params.status) search.set('status', params.status)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminMatchListItemDto[]>(`/api/v1/admin/${club}/matches${query}`)
}

export function getAdminMatch(club: string, id: string): Promise<AdminMatchDetailDto> {
  return apiRequest<AdminMatchDetailDto>(`/api/v1/admin/${club}/matches/${id}`)
}

export function createAdminMatch(club: string, payload: SaveMatchPayload): Promise<AdminMatchDetailDto> {
  return apiRequest<AdminMatchDetailDto>(`/api/v1/admin/${club}/matches`, { method: 'POST', body: payload })
}

export function updateAdminMatch(club: string, id: string, payload: SaveMatchPayload): Promise<AdminMatchDetailDto> {
  return apiRequest<AdminMatchDetailDto>(`/api/v1/admin/${club}/matches/${id}`, { method: 'PUT', body: payload })
}

/** 🔴 硬刪除，不是狀態轉換——賽事是純資料紀錄，畫面上一律要求二次確認再呼叫這支。 */
export function deleteAdminMatch(club: string, id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/matches/${id}`, { method: 'DELETE' })
}

// ── CSV 匯入（整季賽程，整批新建、不是 upsert）───────────────────────────────────────────
// 逐字比照 `adminFaq.ts` 的既有做法：CSV 原始位元組直接當 request body，不是 JSON 也不是
// multipart；不做 401 的 refresh-retry（低頻手動操作，見既有註解）。

export interface MatchCsvImportRowErrorDto {
  rowNumber: number
  reason: string
}

export interface MatchCsvImportResultDto {
  importedCount: number
  errors: MatchCsvImportRowErrorDto[]
}

export async function importAdminMatchesCsv(club: string, file: File): Promise<MatchCsvImportResultDto> {
  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const text = await file.text()
  const response = await fetch(`${API_BASE_URL}/api/v1/admin/${club}/matches/import`, {
    method: 'POST',
    headers,
    credentials: 'include',
    body: text,
  })

  const raw = await response.text()
  let parsed: MatchCsvImportResultDto | null = null
  try {
    parsed = raw ? (JSON.parse(raw) as MatchCsvImportResultDto) : null
  } catch {
    parsed = null
  }

  if (response.status === 200 || response.status === 400) {
    if (parsed) return parsed
  }
  if (!response.ok) {
    throw new AdminApiError('server', '匯入失敗，請稍後再試。', { status: response.status })
  }
  throw new AdminApiError('server', '匯入結果無法解析，請稍後再試。')
}
