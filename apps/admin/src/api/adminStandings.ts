/**
 * `apps/api` C4「積分榜」後台端點（`Features/AdminStandings`），對照 apps/api/README.md「S1-8」。
 * ⚠️ **這個模組不套用球隊列級授權**——`standings` 沒有 `team_id` 欄位，`academy_program`
 * 角色（如 `academy.manager@tcrfc.test`）目前完全沒有 `team.standing.*` 權限碼，打這組端點
 * 一律 403「沒有權限」（不是列級授權的細節訊息），見 apps/api/README.md「為什麼積分榜不套列級
 * 授權」。
 */
import { apiRequest } from './http'
import { API_BASE_URL, AdminApiError } from './http'
import { getAccessToken } from '@/auth/session'

export interface AdminStandingListItemDto {
  id: string
  seasonId: string
  seasonCode: string
  teamName: string
  rank?: number | null
  played?: number | null
  points?: number | null
  updatedAt: string
}

export interface AdminStandingDetailDto {
  id: string
  seasonId: string
  seasonCode: string
  teamName: string
  rank?: number | null
  played?: number | null
  points?: number | null
  createdAt: string
  updatedAt: string
}

export interface SaveStandingPayload {
  seasonId: string
  teamName: string
  rank?: number | null
  played?: number | null
  points?: number | null
}

export function listAdminStandings(club: string, seasonId?: string): Promise<AdminStandingListItemDto[]> {
  const query = seasonId ? `?seasonId=${encodeURIComponent(seasonId)}` : ''
  return apiRequest<AdminStandingListItemDto[]>(`/api/v1/admin/${club}/standings${query}`)
}

export function getAdminStanding(club: string, id: string): Promise<AdminStandingDetailDto> {
  return apiRequest<AdminStandingDetailDto>(`/api/v1/admin/${club}/standings/${id}`)
}

export function createAdminStanding(club: string, payload: SaveStandingPayload): Promise<AdminStandingDetailDto> {
  return apiRequest<AdminStandingDetailDto>(`/api/v1/admin/${club}/standings`, { method: 'POST', body: payload })
}

export function updateAdminStanding(club: string, id: string, payload: SaveStandingPayload): Promise<AdminStandingDetailDto> {
  return apiRequest<AdminStandingDetailDto>(`/api/v1/admin/${club}/standings/${id}`, { method: 'PUT', body: payload })
}

export function deleteAdminStanding(club: string, id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/standings/${id}`, { method: 'DELETE' })
}

// ── CSV 匯入（整季替換：匯入前先刪除這個賽季的全部既有列，再整批寫入新內容）───────────────────

export interface StandingCsvImportRowErrorDto {
  rowNumber: number
  reason: string
}

/**
 * `replacedCount`：匯入後這個賽季的總列數（＝新檔案的資料列數）。
 * `deletedCount`：匯入前先清掉的既有列數——畫面用這兩個數字組出
 * 「原本 N 筆已被清除，換成 M 筆新資料」的提示，不是另外算出來的。
 */
export interface StandingCsvImportResultDto {
  replacedCount: number
  deletedCount: number
  errors: StandingCsvImportRowErrorDto[]
}

export async function importAdminStandingsCsv(club: string, file: File): Promise<StandingCsvImportResultDto> {
  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const text = await file.text()
  const response = await fetch(`${API_BASE_URL}/api/v1/admin/${club}/standings/import`, {
    method: 'POST',
    headers,
    credentials: 'include',
    body: text,
  })

  const raw = await response.text()
  let parsed: StandingCsvImportResultDto | null = null
  try {
    parsed = raw ? (JSON.parse(raw) as StandingCsvImportResultDto) : null
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
