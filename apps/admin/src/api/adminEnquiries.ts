/**
 * G2「詢問收件匣」後台端點（`Features/AdminEnquiries`），對照 apps/api/README.md「S1-10」。
 * 依表單類別的列級授權由後端計算，前端只需要照常呼叫、依回應內容呈現——**看不到的類別後端會
 * 回傳空集合（清單）或 404（單筆），不是前端要另外判斷的邊界**（見 `useFormsPermissions.ts`）。
 */
import { apiRequest, AdminApiError, API_BASE_URL } from './http'
import { getAccessToken } from '@/auth/session'

export interface AdminEnquiryListItemDto {
  id: string
  formCode: string
  formNameZh: string
  applicantName?: string | null
  contactInfo?: string | null
  contentSummary?: string | null
  sourcePath?: string | null
  utmSource?: string | null
  utmCampaign?: string | null
  status?: string | null
  assigneeAdminUserId?: string | null
  createdAt: string
}

export interface AdminEnquiryAnswerDto {
  fieldKey: string
  fieldType: string
  value?: string | null
}

export interface AdminEnquiryDetailDto {
  id: string
  formCode: string
  formNameZh: string
  sourcePath?: string | null
  utmSource?: string | null
  utmCampaign?: string | null
  status?: string | null
  internalNote?: string | null
  tags?: string | null
  assigneeAdminUserId?: string | null
  answers: AdminEnquiryAnswerDto[]
  createdAt: string
  updatedAt: string
}

/** G2「指派負責人」姓名選單的候選人（S1-10 修正新增）——只回傳必要欄位（`id`／`displayName`），
 * 不是 `system.account.view` 專屬的完整帳號清單，持有 `enquiry.*.update` 的角色都能查詢。 */
export interface AssignableAdminUserDto {
  id: string
  displayName: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ListAdminEnquiriesParams {
  formCode?: string
  status?: string
  keyword?: string
  /** `YYYY-MM-DD` */
  dateFrom?: string
  /** `YYYY-MM-DD` */
  dateTo?: string
  page?: number
  pageSize?: number
}

/** 後台只能改這四項——來源表單與逐筆回答內容是訪客送出的原始資料，不開放竄改。 */
export interface UpdateEnquiryPayload {
  status: string
  assigneeAdminUserId?: string | null
  internalNote?: string | null
  tags?: string | null
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === '') continue
    search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export function listAdminEnquiries(club: string, params: ListAdminEnquiriesParams = {}): Promise<PagedResult<AdminEnquiryListItemDto>> {
  const query = buildQuery({
    formCode: params.formCode,
    status: params.status,
    keyword: params.keyword,
    dateFrom: params.dateFrom,
    dateTo: params.dateTo,
    page: params.page,
    pageSize: params.pageSize,
  })
  return apiRequest<PagedResult<AdminEnquiryListItemDto>>(`/api/v1/admin/${club}/enquiries${query}`)
}

export function getAdminEnquiry(club: string, id: string): Promise<AdminEnquiryDetailDto> {
  return apiRequest<AdminEnquiryDetailDto>(`/api/v1/admin/${club}/enquiries/${id}`)
}

export function updateAdminEnquiry(club: string, id: string, payload: UpdateEnquiryPayload): Promise<AdminEnquiryDetailDto> {
  return apiRequest<AdminEnquiryDetailDto>(`/api/v1/admin/${club}/enquiries/${id}`, { method: 'PUT', body: payload })
}

/** `formCode` 依這筆詢問實際所屬的表單類別查詢——權限碼與 `PUT .../enquiries/{id}` 同一組
 * （能處理詢問的人才能查「能指派給誰」），越權查詢別的類別後端回 404（不洩漏存在與否），呼叫端
 * 不需要另外判斷，見 `EnquiryEditView.vue`。 */
export function listAssignableEnquiryUsers(club: string, formCode: string): Promise<AssignableAdminUserDto[]> {
  return apiRequest<AssignableAdminUserDto[]>(`/api/v1/admin/${club}/enquiries/assignable-users?formCode=${encodeURIComponent(formCode)}`)
}

/** CSV 匯出（比照 `adminRegistrations.ts` 的 `downloadAdminRegistrationsCsv` 既有做法：直接 fetch
 * 拿 blob 觸發下載）。`enquiry.inbox.export` 是 `is_restricted`，僅系統管理員看得到匯出按鈕
 * （見 `useFormsPermissions.ts` 的 `canExportInbox`），但這裡仍然處理 403 以防萬一。 */
export async function downloadAdminEnquiriesCsv(club: string, params: ListAdminEnquiriesParams = {}): Promise<void> {
  const query = buildQuery({
    formCode: params.formCode,
    status: params.status,
    keyword: params.keyword,
    dateFrom: params.dateFrom,
    dateTo: params.dateTo,
  })

  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const response = await fetch(`${API_BASE_URL}/api/v1/admin/${club}/enquiries/export${query}`, {
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
  link.download = `enquiries-${club}.csv`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
