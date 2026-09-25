/**
 * `apps/api` P2「梯次與場次」後台端點（`Features/AdminSessions`），對照 apps/api/README.md「S1-9」。
 * 沒有圖片欄位，一般 JSON 請求即可，不需要 multipart。
 */
import { apiRequest } from './http'

export interface AdminSessionListItemDto {
  id: string
  programId: string
  programNameZh?: string | null
  venueId?: string | null
  startOn?: string | null
  endOn?: string | null
  capacity?: number | null
  enrolledCount: number
  price?: number | null
  earlyBirdPrice?: number | null
  earlyBirdUntil?: string | null
  signupOpensAt?: string | null
  signupClosesAt?: string | null
  status: string
  updatedAt: string
}

export interface AdminSessionDetailDto {
  id: string
  programId: string
  venueId?: string | null
  startOn?: string | null
  endOn?: string | null
  /** `sessions.weekly_schedule`（JSON 文字，週期時段表）。⚠️ 後端目前沒有像 `programs.content`
   * 那樣做語法驗證（見 apps/admin/README.md「P1–P3 已知缺口」），前端仍在送出前檢查一次語法，
   * 避免使用者直接看到伺服器端的原始例外。 */
  weeklySchedule?: string | null
  capacity?: number | null
  enrolledCount: number
  price?: number | null
  earlyBirdPrice?: number | null
  earlyBirdUntil?: string | null
  signupOpensAt?: string | null
  signupClosesAt?: string | null
  status: string
  createdAt: string
  updatedAt: string
}

export interface ListAdminSessionsParams {
  programId?: string
}

export interface CreateSessionPayload {
  programId: string
  venueId?: string | null
  startOn?: string | null
  endOn?: string | null
  weeklySchedule?: string | null
  capacity?: number | null
  price?: number | null
  earlyBirdPrice?: number | null
  earlyBirdUntil?: string | null
  signupOpensAt?: string | null
  signupClosesAt?: string | null
  status?: string | null
}

/** `programId` 不能改（建立後歸屬固定），`UpdateAdminSessionRequest` 本來就沒有這個欄位。 */
export type UpdateSessionPayload = Omit<CreateSessionPayload, 'programId'>

export function listAdminProgramSessions(club: string, params: ListAdminSessionsParams = {}): Promise<AdminSessionListItemDto[]> {
  const search = new URLSearchParams()
  if (params.programId) search.set('programId', params.programId)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminSessionListItemDto[]>(`/api/v1/admin/${club}/program-sessions${query}`)
}

export function getAdminProgramSession(club: string, id: string): Promise<AdminSessionDetailDto> {
  return apiRequest<AdminSessionDetailDto>(`/api/v1/admin/${club}/program-sessions/${id}`)
}

export function createAdminProgramSession(club: string, payload: CreateSessionPayload): Promise<AdminSessionDetailDto> {
  return apiRequest<AdminSessionDetailDto>(`/api/v1/admin/${club}/program-sessions`, { method: 'POST', body: payload })
}

export function updateAdminProgramSession(club: string, id: string, payload: UpdateSessionPayload): Promise<AdminSessionDetailDto> {
  return apiRequest<AdminSessionDetailDto>(`/api/v1/admin/${club}/program-sessions/${id}`, { method: 'PUT', body: payload })
}
