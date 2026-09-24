/**
 * `apps/api` 後台常見問題端點（`Features/AdminFaqs`），對照 apps/api/README.md
 * 「S1-6：B3 首頁編排／B4 常見問題」「S1-6 續作：批次操作與 CSV 匯入／匯出」「S1-7a」。
 */
import { apiRequest } from './http'
import { API_BASE_URL, AdminApiError } from './http'
import { getAccessToken } from '@/auth/session'

// ── 主題分類（faq-categories，全域端點）─────────────────────────────────────────────

export interface AdminFaqCategoryLocaleContentDto {
  name?: string | null
}

export interface AdminFaqCategoryContentInputDto {
  zh: AdminFaqCategoryLocaleContentDto
  en?: AdminFaqCategoryLocaleContentDto | null
}

export interface AdminFaqCategoryListItemDto {
  id: string
  slug: string
  sortOrder: number
  isEnabled: boolean
  nameZh?: string | null
  nameEn?: string | null
  faqCount: number
  updatedAt: string
}

export interface AdminFaqCategoryDetailDto {
  id: string
  slug: string
  sortOrder: number
  isEnabled: boolean
  zh: AdminFaqCategoryLocaleContentDto
  en?: AdminFaqCategoryLocaleContentDto | null
  createdAt: string
  updatedAt: string
}

export interface CreateFaqCategoryPayload {
  slug: string
  sortOrder: number
  isEnabled?: boolean
  content: AdminFaqCategoryContentInputDto
}

export interface UpdateFaqCategoryPayload {
  slug: string
  sortOrder: number
  isEnabled: boolean
  content: AdminFaqCategoryContentInputDto
}

export function listAdminFaqCategories(): Promise<AdminFaqCategoryListItemDto[]> {
  return apiRequest<AdminFaqCategoryListItemDto[]>('/api/v1/admin/faq-categories')
}

export function createAdminFaqCategory(payload: CreateFaqCategoryPayload): Promise<AdminFaqCategoryDetailDto> {
  return apiRequest<AdminFaqCategoryDetailDto>('/api/v1/admin/faq-categories', { method: 'POST', body: payload })
}

export function updateAdminFaqCategory(id: string, payload: UpdateFaqCategoryPayload): Promise<AdminFaqCategoryDetailDto> {
  return apiRequest<AdminFaqCategoryDetailDto>(`/api/v1/admin/faq-categories/${id}`, { method: 'PUT', body: payload })
}

/** 真刪除（不可逆）——「停用」請改用 `updateAdminFaqCategory` 的 `isEnabled: false`，
 * 不要再用刪除頂替停用（S1-7a 之前的舊做法，已改為真正的刪除，見 apps/api/README.md）。 */
export function deleteAdminFaqCategory(id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/faq-categories/${id}`, { method: 'DELETE' })
}

// ── G-12 嵌入掛載點字典（唯讀，4 筆固定值）───────────────────────────────────────────

export interface AdminFaqEmbedSlotDto {
  id: string
  code: string
  name: string
}

export function listAdminFaqEmbedSlots(): Promise<AdminFaqEmbedSlotDto[]> {
  return apiRequest<AdminFaqEmbedSlotDto[]>('/api/v1/admin/faq-embed-slots')
}

// ── 題目（faqs，俱樂部範圍）──────────────────────────────────────────────────────

export interface AdminFaqLocaleContentDto {
  question?: string | null
  answer?: string | null
}

export interface AdminFaqContentInputDto {
  zh: AdminFaqLocaleContentDto
  en?: AdminFaqLocaleContentDto | null
}

export interface AdminFaqCategoryRefDto {
  id: string
  slug: string
  nameZh?: string | null
  nameEn?: string | null
}

export interface AdminFaqEmbedSlotRefDto {
  id: string
  code: string
  name: string
}

export interface AdminFaqListItemDto {
  id: string
  slug: string
  sortOrder: number
  status: 'draft' | 'published'
  isShared: boolean
  viewCount: number
  helpfulCount: number
  unhelpfulCount: number
  updatedAt: string
  questionZh?: string | null
  questionEn?: string | null
  categories: AdminFaqCategoryRefDto[]
  embedSlots: AdminFaqEmbedSlotRefDto[]
}

export interface AdminFaqDetailDto {
  id: string
  slug: string
  sortOrder: number
  status: 'draft' | 'published'
  isShared: boolean
  viewCount: number
  helpfulCount: number
  unhelpfulCount: number
  updatedAt: string
  zh: AdminFaqLocaleContentDto
  en?: AdminFaqLocaleContentDto | null
  categories: AdminFaqCategoryRefDto[]
  embedSlots: AdminFaqEmbedSlotRefDto[]
}

export interface AdminFaqPage {
  items: AdminFaqListItemDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ListAdminFaqsParams {
  status?: 'draft' | 'published' | ''
  categoryId?: string
  keyword?: string
  /** `low_rating`＝低評價題目清單（規劃書 B4「成效數據」），省略＝依排序值排序 */
  sort?: 'low_rating' | ''
  page?: number
  pageSize?: number
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

export function listAdminFaqs(club: string, params: ListAdminFaqsParams = {}): Promise<AdminFaqPage> {
  const query = buildQuery({
    status: params.status,
    categoryId: params.categoryId,
    keyword: params.keyword,
    sort: params.sort,
    page: params.page,
    pageSize: params.pageSize,
  })
  return apiRequest<AdminFaqPage>(`/api/v1/admin/${club}/faqs${query}`)
}

export function getAdminFaq(club: string, id: string): Promise<AdminFaqDetailDto> {
  return apiRequest<AdminFaqDetailDto>(`/api/v1/admin/${club}/faqs/${id}`)
}

export interface SaveFaqPayload {
  slug: string
  categoryIds: string[]
  sortOrder: number
  status: 'draft' | 'published'
  /** 省略＝維持不變（更新時）；空陣列＝清空。建立時省略＝這題沒有額外指定任何掛載點。 */
  embedSlotIds?: string[]
  content: AdminFaqContentInputDto
}

export function createAdminFaq(club: string, payload: SaveFaqPayload): Promise<AdminFaqDetailDto> {
  return apiRequest<AdminFaqDetailDto>(`/api/v1/admin/${club}/faqs`, { method: 'POST', body: payload })
}

export function updateAdminFaq(club: string, id: string, payload: SaveFaqPayload): Promise<AdminFaqDetailDto> {
  return apiRequest<AdminFaqDetailDto>(`/api/v1/admin/${club}/faqs/${id}`, { method: 'PUT', body: payload })
}

export function deleteAdminFaq(club: string, id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/faqs/${id}`, { method: 'DELETE' })
}

// ── 批次操作 ─────────────────────────────────────────────────────────────────────

export interface BatchOperationSkippedItemDto {
  id: string
  reason: string
}

export interface BatchOperationResultDto {
  updatedCount: number
  skipped: BatchOperationSkippedItemDto[]
}

export function batchChangeFaqCategory(club: string, ids: string[], categoryIds: string[]): Promise<BatchOperationResultDto> {
  return apiRequest<BatchOperationResultDto>(`/api/v1/admin/${club}/faqs/batch/category`, {
    method: 'POST',
    body: { ids, categoryIds },
  })
}

export function batchShowFaqs(club: string, ids: string[]): Promise<BatchOperationResultDto> {
  return apiRequest<BatchOperationResultDto>(`/api/v1/admin/${club}/faqs/batch/show`, { method: 'POST', body: { ids } })
}

export function batchHideFaqs(club: string, ids: string[]): Promise<BatchOperationResultDto> {
  return apiRequest<BatchOperationResultDto>(`/api/v1/admin/${club}/faqs/batch/hide`, { method: 'POST', body: { ids } })
}

// ── CSV 匯入／匯出 ───────────────────────────────────────────────────────────────
// 🔴 這兩支不是 JSON 也不是 multipart：匯出直接回檔案位元組，匯入直接把 CSV 原始位元組當
// request body 送出（apps/api/README.md「CSV 匯入／匯出」：Content-Type 不拘，一律以 UTF-8
// 解碼）。`apiRequest`／`apiUploadRequest` 兩個既有共用函式都假設 body 是 JSON 或 FormData，
// 這裡直接用 fetch，但沿用同一套權杖／401 處理慣例（不做 refresh-retry：CSV 匯入匯出是低頻的
// 手動操作，使用者重新按一次的成本很低，不值得為這兩支端點複製一份 refresh-retry 邏輯）。

export interface FaqCsvImportRowErrorDto {
  rowNumber: number
  reason: string
}

export interface FaqCsvImportResultDto {
  importedCount: number
  errors: FaqCsvImportRowErrorDto[]
}

/** 下載常見問題 CSV（這個俱樂部自己的題目，不含共用內容，見 apps/api/README.md 說明）。
 * 直接觸發瀏覽器下載，不回傳內容給呼叫端。 */
export async function downloadAdminFaqsCsv(club: string): Promise<void> {
  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const response = await fetch(`${API_BASE_URL}/api/v1/admin/${club}/faqs/export`, {
    headers,
    credentials: 'include',
  })
  if (!response.ok) {
    throw new AdminApiError('server', '匯出失敗，請稍後再試。', { status: response.status })
  }
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `faqs-${club}.csv`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

/** 匯入 CSV：任一列有錯就整批不寫入（後端回 400，body 仍是完整的 `FaqCsvImportResultDto`，
 * 裡面列出逐列錯誤）——這裡刻意不把 400 當成 `AdminApiError` 丟出，因為呼叫端需要的是完整的
 * 逐列錯誤清單，不是一句話的錯誤訊息。 */
export async function importAdminFaqsCsv(club: string, file: File): Promise<FaqCsvImportResultDto> {
  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const text = await file.text()
  const response = await fetch(`${API_BASE_URL}/api/v1/admin/${club}/faqs/import`, {
    method: 'POST',
    headers,
    credentials: 'include',
    body: text,
  })

  const raw = await response.text()
  let parsed: FaqCsvImportResultDto | null = null
  try {
    parsed = raw ? (JSON.parse(raw) as FaqCsvImportResultDto) : null
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

// ── 搜尋無結果關鍵字排行（S1-8，`GET /admin/{club}/faqs/search-misses`）─────────────────────
// 🔴 語意：`faq_search_misses` 是彙總列不是逐次搜尋的日誌，`count` 是這個關鍵字「有史以來」被
// 搜尋不到的累計總次數，`days` 只篩「最後一次被搜尋到，落在最近幾天內」，不是「近 N 天的次數」
// ——畫面文案要避免暗示成後者（例如不要寫「近 30 天搜尋次數」，要寫「累計搜尋次數」）。
// 關鍵字本身已經是後端寫入時正規化過的字串（全形轉半形、去頭尾空白、英文轉小寫），畫面照顯示
// 即可，不需要再處理一次。

export interface AdminFaqSearchMissDto {
  keyword: string
  count: number
  lastSearchedAt: string
}

export function listAdminFaqSearchMisses(club: string, days?: number, top?: number): Promise<AdminFaqSearchMissDto[]> {
  const search = new URLSearchParams()
  if (days) search.set('days', String(days))
  if (top) search.set('top', String(top))
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminFaqSearchMissDto[]>(`/api/v1/admin/${club}/faqs/search-misses${query}`)
}
