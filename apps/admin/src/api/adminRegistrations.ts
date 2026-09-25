/**
 * `apps/api` P3「報名管理」後台端點（`Features/AdminRegistrations`），對照 apps/api/README.md「S1-9」。
 * 只服務課程報名（`sessionId` 非空），不含 P4 試訓（`trialId`，留給 `S2-4`）。
 */
import { apiRequest, AdminApiError, API_BASE_URL } from './http'
import { getAccessToken } from '@/auth/session'

export interface AdminRegistrationListItemDto {
  id: string
  registrationNo: string
  sessionId?: string | null
  programNameZh?: string | null
  trialId?: string | null
  memberId?: string | null
  isMember: boolean
  applicantName: string
  phone?: string | null
  email?: string | null
  status: string
  createdAt: string
}

/**
 * 🔴 健康聲明（`healthDeclaration`）依後端現況原樣顯示與可編輯（整份覆寫語意，見
 * `apps/api` `UpdateAdminRegistrationRequest` 檔頭）——**不新增蒐集欄位、不新增同意書上傳**，
 * 依任務指示不擴大蒐集範圍。CSV 匯出（`downloadAdminRegistrationsCsv`）由後端刻意排除這欄
 * （資料最小化，見 apps/api/README.md「S1-9」「名額控管」上方段落），前端不需要另外處理。
 */
export interface AdminRegistrationDetailDto {
  id: string
  registrationNo: string
  sessionId?: string | null
  trialId?: string | null
  memberId?: string | null
  applicantName: string
  phone?: string | null
  email?: string | null
  birthOn?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  healthDeclaration?: string | null
  note?: string | null
  status: string
  createdAt: string
  updatedAt: string
}

export interface ListAdminRegistrationsParams {
  sessionId?: string
  status?: string
}

/** 後台代填報名（電話／現場報名）。P4 不在本次範圍，故不接受 `trialId`。 */
export interface CreateRegistrationPayload {
  sessionId: string
  memberId?: string | null
  applicantName: string
  phone?: string | null
  email?: string | null
  birthOn?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  healthDeclaration?: string | null
  note?: string | null
  status?: string | null
}

/** 報名處理：確認／取消／轉梯次（換 `sessionId`）／加入候補／備註／學員資料整份覆寫。 */
export interface UpdateRegistrationPayload {
  sessionId: string
  memberId?: string | null
  applicantName: string
  phone?: string | null
  email?: string | null
  birthOn?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  healthDeclaration?: string | null
  note?: string | null
  status: string
}

export function listAdminRegistrations(
  club: string,
  params: ListAdminRegistrationsParams = {},
): Promise<AdminRegistrationListItemDto[]> {
  const search = new URLSearchParams()
  if (params.sessionId) search.set('sessionId', params.sessionId)
  if (params.status) search.set('status', params.status)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminRegistrationListItemDto[]>(`/api/v1/admin/${club}/registrations${query}`)
}

export function getAdminRegistration(club: string, id: string): Promise<AdminRegistrationDetailDto> {
  return apiRequest<AdminRegistrationDetailDto>(`/api/v1/admin/${club}/registrations/${id}`)
}

export function createAdminRegistration(club: string, payload: CreateRegistrationPayload): Promise<AdminRegistrationDetailDto> {
  return apiRequest<AdminRegistrationDetailDto>(`/api/v1/admin/${club}/registrations`, { method: 'POST', body: payload })
}

export function updateAdminRegistration(
  club: string,
  id: string,
  payload: UpdateRegistrationPayload,
): Promise<AdminRegistrationDetailDto> {
  return apiRequest<AdminRegistrationDetailDto>(`/api/v1/admin/${club}/registrations/${id}`, { method: 'PUT', body: payload })
}

/** CSV 匯出（比照 `adminFaq.ts` 的 `downloadAdminFaqsCsv` 既有做法：直接 fetch 拿 blob 觸發下載，
 * 不透過 `apiRequest`——那支預期回應是 JSON）。`sessionId`／`status` 篩選條件同列表。 */
export async function downloadAdminRegistrationsCsv(
  club: string,
  params: ListAdminRegistrationsParams = {},
): Promise<void> {
  const search = new URLSearchParams()
  if (params.sessionId) search.set('sessionId', params.sessionId)
  if (params.status) search.set('status', params.status)
  const query = search.toString() ? `?${search.toString()}` : ''

  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const response = await fetch(`${API_BASE_URL}/api/v1/admin/${club}/registrations/export${query}`, {
    headers,
    credentials: 'include',
  })
  if (!response.ok) {
    if (response.status === 403) {
      throw new AdminApiError('forbidden', '你沒有權限執行這個操作。', { status: 403 })
    }
    throw new AdminApiError('server', '匯出失敗，請稍後再試。', { status: response.status })
  }
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `registrations-${club}.csv`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
