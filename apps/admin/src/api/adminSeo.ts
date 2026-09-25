/**
 * `apps/api` 後台「搜尋與 AI 能見度」端點（`Features/AdminSeo`），對照
 * apps/api/README.md「S1-12：`H` 搜尋與 AI 能見度」。涵蓋五段：
 * 全站 SEO 預設＋追蹤碼（`seo.setting.*`）、301 轉址（`seo.redirect.*`）、孤立頁面偵測
 * （`seo.report.view`）、`llms.txt`（AI 摘要資料，`seo.llms.*`，apps/api/README.md「S1-12a」）、
 * AI 爬蟲授權（`seo.crawler.*`，apps/api/README.md「S1-12b」）。五段權限碼皆為 `sysadmin_only`
 * （見 apps/api/README.md 各節「權限碼」）。
 */
import { apiRequest, apiUploadRequest, API_BASE_URL, AdminApiError } from './http'
import { getAccessToken } from '@/auth/session'

// ── 全站 SEO 預設＋追蹤碼（H1）────────────────────────────────────────────────────

/** 對照 `Features/AdminSeo/AdminSeoSettingsDtos.cs` 的 `AdminSeoSettingsDto`。 */
export interface AdminSeoSettingsDto {
  titleTemplateZh?: string | null
  titleTemplateEn?: string | null
  defaultDescriptionZh?: string | null
  defaultDescriptionEn?: string | null
  /** robots.txt 自訂規則（線上編輯）。⚠️ 只有正式環境旗標為真時才會真的輸出到 `/robots.txt`，
   * 見 `apps/web/server/routes/robots.txt.ts`——本機／預備環境這裡填了什麼都不會生效。 */
  robotsCustomRules?: string | null
  ga4MeasurementId?: string | null
  gtmContainerId?: string | null
  metaPixelId?: string | null
  lineTagId?: string | null
  /** 全站預設分享圖片完整網址，`null`＝尚未設定。 */
  ogImageUrl?: string | null
  ogImageWidth?: number | null
  ogImageHeight?: number | null
}

/** 對照 `UpdateSeoSettingsRequest`（`payload` 這個 multipart 欄位的 JSON 內容）。
 * 🔴 文字欄位一律整份送出，不是「省略＝維持不變」——跟 `AdminSeoSettingsDto` 後端註解一致。 */
export interface UpdateSeoSettingsPayload {
  titleTemplateZh?: string | null
  titleTemplateEn?: string | null
  defaultDescriptionZh?: string | null
  defaultDescriptionEn?: string | null
  robotsCustomRules?: string | null
  ga4MeasurementId?: string | null
  gtmContainerId?: string | null
  metaPixelId?: string | null
  lineTagId?: string | null
  /** 勾選「移除全站預設分享圖片」。跟這次請求的 `ogImage` 檔案互斥，兩者都有視為請求矛盾（400）。 */
  removeOgImage?: boolean
}

export function getAdminSeoSettings(club: string): Promise<AdminSeoSettingsDto> {
  return apiRequest<AdminSeoSettingsDto>(`/api/v1/admin/${club}/seo/settings`)
}

/** `ogImageFile` 非 `null`＝選了新圖片（規劃書「選檔不上傳、儲存才上傳」，本函式呼叫的當下才是
 * 真正送出上傳請求的時間點）。 */
export function updateAdminSeoSettings(
  club: string,
  payload: UpdateSeoSettingsPayload,
  ogImageFile: File | null,
): Promise<AdminSeoSettingsDto> {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (ogImageFile) form.append('ogImage', ogImageFile)
  return apiUploadRequest<AdminSeoSettingsDto>(`/api/v1/admin/${club}/seo/settings`, form, { method: 'PUT' })
}

// ── 301 轉址（H2）────────────────────────────────────────────────────────────────

/** 對照 `Features/AdminSeo/AdminRedirectDtos.cs` 的 `AdminRedirectDto`。 */
export interface AdminRedirectDto {
  id: string
  fromPath: string
  toPath: string
  isActive: boolean
  updatedAt: string
}

export interface CreateRedirectPayload {
  fromPath: string
  toPath: string
  isActive: boolean
}

/** 更新不接受改 `fromPath`——來源網址是識別鍵，要換視同刪除重建，不是編輯（見後端註解）。 */
export interface UpdateRedirectPayload {
  toPath: string
  isActive: boolean
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ListAdminRedirectsParams {
  keyword?: string
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

export function listAdminRedirects(club: string, params: ListAdminRedirectsParams = {}): Promise<PagedResult<AdminRedirectDto>> {
  const query = buildQuery({ keyword: params.keyword, page: params.page, pageSize: params.pageSize })
  return apiRequest<PagedResult<AdminRedirectDto>>(`/api/v1/admin/${club}/seo/redirects${query}`)
}

export function createAdminRedirect(club: string, payload: CreateRedirectPayload): Promise<AdminRedirectDto> {
  return apiRequest<AdminRedirectDto>(`/api/v1/admin/${club}/seo/redirects`, { method: 'POST', body: payload })
}

export function updateAdminRedirect(club: string, id: string, payload: UpdateRedirectPayload): Promise<AdminRedirectDto> {
  return apiRequest<AdminRedirectDto>(`/api/v1/admin/${club}/seo/redirects/${id}`, { method: 'PUT', body: payload })
}

export function deleteAdminRedirect(club: string, id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/seo/redirects/${id}`, { method: 'DELETE' })
}

// ── 301 轉址 CSV 匯入／匯出 ───────────────────────────────────────────────────────
// 🔴 這兩支不是 JSON 也不是 multipart：匯出直接回檔案位元組，匯入直接把 CSV 原始位元組當
// request body 送出（比照 `@/api/adminFaq.ts` 既有的 `downloadAdminFaqsCsv`／`importAdminFaqsCsv`
// 寫法）。表頭固定「來源網址、目的網址、啟用狀態」，啟用狀態欄位值是「啟用」／「停用」
// （對照 `Features/AdminSeo/AdminRedirectsRepository.CsvHeader`，不是英文 true/false）。

export interface RedirectCsvImportRowErrorDto {
  rowNumber: number
  reason: string
}

export interface RedirectCsvImportResultDto {
  importedCount: number
  errors: RedirectCsvImportRowErrorDto[]
}

/** 下載這個俱樂部目前全部的轉址規則（含停用的）CSV，直接觸發瀏覽器下載。 */
export async function downloadAdminRedirectsCsv(club: string): Promise<void> {
  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const response = await fetch(`${API_BASE_URL}/api/v1/admin/${club}/seo/redirects/export`, {
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
  link.download = `redirects-${club}.csv`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

/** 匯入 CSV：**upsert**（依來源網址，已存在就整列覆寫，不存在就新增），任一列有錯就整批不寫入
 * （後端回 400，body 仍是完整的 `RedirectCsvImportResultDto`，列出逐列錯誤）。 */
export async function importAdminRedirectsCsv(club: string, file: File): Promise<RedirectCsvImportResultDto> {
  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const text = await file.text()
  const response = await fetch(`${API_BASE_URL}/api/v1/admin/${club}/seo/redirects/import`, {
    method: 'POST',
    headers,
    credentials: 'include',
    body: text,
  })

  const raw = await response.text()
  let parsed: RedirectCsvImportResultDto | null = null
  try {
    parsed = raw ? (JSON.parse(raw) as RedirectCsvImportResultDto) : null
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

// ── 孤立頁面偵測（H3）────────────────────────────────────────────────────────────

/** 對照 `Features/AdminSeo/AdminSeoReportDtos.cs` 的 `OrphanPageDto`。`entityType` 值域
 * `page`／`article`，只用來查表決定要顯示哪一種說明文字，不直接顯示在畫面上。 */
export interface OrphanPageDto {
  entityType: 'page' | 'article'
  id: string
  path: string
  titleZh?: string | null
}

export interface OrphanPageReportDto {
  items: OrphanPageDto[]
}

export function getAdminOrphanPages(club: string): Promise<OrphanPageReportDto> {
  return apiRequest<OrphanPageReportDto>(`/api/v1/admin/${club}/seo/orphan-pages`)
}

// ── `llms.txt`／AI 摘要資料（H4，S1-12a）──────────────────────────────────────────
// 對照 `Features/AdminSeo/AdminGeoLlmsDtos.cs` 的 `AdminLlmsContentDto`／`UpdateLlmsContentRequest`。
// 五個區塊皆可為空（後端檔頭：留白時前台有內建預設文字可回退，不會讓 `/llms.txt` 輸出壞掉）。

export interface AdminLlmsContentDto {
  positioningZh?: string | null
  positioningEn?: string | null
  keyPagesZh?: string | null
  keyPagesEn?: string | null
  factsSummaryZh?: string | null
  factsSummaryEn?: string | null
  licenseZh?: string | null
  licenseEn?: string | null
  contactZh?: string | null
  contactEn?: string | null
}

/** 🔴 整份送出語意：省略某個欄位＝清空既有值，不是「維持不變」（同 `AdminSeoSettingsDto` 慣例）。 */
export type UpdateLlmsContentPayload = AdminLlmsContentDto

export function getAdminLlmsContent(club: string): Promise<AdminLlmsContentDto> {
  return apiRequest<AdminLlmsContentDto>(`/api/v1/admin/${club}/seo/llms-content`)
}

export function updateAdminLlmsContent(club: string, payload: UpdateLlmsContentPayload): Promise<AdminLlmsContentDto> {
  return apiRequest<AdminLlmsContentDto>(`/api/v1/admin/${club}/seo/llms-content`, { method: 'PUT', body: payload })
}

// ── AI 爬蟲授權（H5，S1-12b）───────────────────────────────────────────────────────
// 對照 `Features/AdminSeo/AdminGeoCrawlerDtos.cs`。`mandatoryExcludePaths` 唯讀（後端 DTO 本身
// 沒有對應的可寫入欄位，`UpdateCrawlerSettingsRequest` 結構上就不存在「移除強制路徑」這個操作）。

export interface CrawlerAgentDto {
  userAgent: string
  allowed: boolean
}

export interface AdminCrawlerSettingsDto {
  userAgents: CrawlerAgentDto[]
  additionalExcludePaths: string[]
  mandatoryExcludePaths: string[]
}

export interface UpdateCrawlerSettingsPayload {
  userAgents: CrawlerAgentDto[]
  additionalExcludePaths: string[]
}

export function getAdminCrawlerSettings(club: string): Promise<AdminCrawlerSettingsDto> {
  return apiRequest<AdminCrawlerSettingsDto>(`/api/v1/admin/${club}/seo/crawler-settings`)
}

export function updateAdminCrawlerSettings(club: string, payload: UpdateCrawlerSettingsPayload): Promise<AdminCrawlerSettingsDto> {
  return apiRequest<AdminCrawlerSettingsDto>(`/api/v1/admin/${club}/seo/crawler-settings`, { method: 'PUT', body: payload })
}
