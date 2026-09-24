/**
 * `apps/api` 後台頁面管理端點（`Features/AdminPages`，B1），對照
 * apps/api/README.md「S1-4：B1 頁面管理」與 `Features/AdminPages/AdminPageDtos.cs`（camelCase）。
 */
import { apiRequest, apiUploadRequest } from './http'

export interface AdminPageSeoLocaleContentDto {
  seoTitle?: string | null
  seoDescription?: string | null
}

export interface AdminPageSeoInputDto {
  zh: AdminPageSeoLocaleContentDto
  en?: AdminPageSeoLocaleContentDto | null
}

/** 單一區塊的輸入。`content` 是該區塊型別對應的 JSON 物件，見 `@/types/pageBlocks.ts`
 * 與 `@/utils/pageBlockSerializer.ts`（畫面狀態 ↔ 這個形狀的互轉）。 */
export interface AdminPageBlockInputDto {
  blockType: string
  content: unknown
}

export interface AdminPageBlockDto {
  id: string
  blockType: string
  content: unknown
  sortOrder: number
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** 清單一列不含標題（`pages_i18n` 沒有這個欄位），用 SEO 標題作為清單上唯一可辨識的雙語文字。 */
export interface AdminPageListItemDto {
  id: string
  slug: string
  status: 'draft' | 'published' | 'scheduled'
  publishedAt?: string | null
  updatedAt: string
  seoTitleZh?: string | null
  seoTitleEn?: string | null
}

export interface AdminPageDetailDto {
  id: string
  slug: string
  status: 'draft' | 'published' | 'scheduled'
  publishedAt?: string | null
  updatedAt: string
  zh: AdminPageSeoLocaleContentDto
  en?: AdminPageSeoLocaleContentDto | null
  blocks: AdminPageBlockDto[]
  latestVersionNo: number
  /** 最新版本的預覽權杖，前端組 `/{locale}/preview/{token}` 即可分享（未發布也可以）。 */
  previewToken?: string | null
}

export interface AdminPageVersionListItemDto {
  versionNo: number
  createdAt: string
  createdBy?: string | null
  previewToken?: string | null
}

export interface AdminPageVersionDetailDto extends AdminPageVersionListItemDto {
  zh: AdminPageSeoLocaleContentDto
  en?: AdminPageSeoLocaleContentDto | null
  blocks: AdminPageBlockDto[]
}

export interface ListAdminPagesParams {
  status?: 'draft' | 'scheduled' | 'published' | ''
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

export function listAdminPages(club: string, params: ListAdminPagesParams = {}): Promise<PagedResult<AdminPageListItemDto>> {
  const query = buildQuery({
    status: params.status,
    keyword: params.keyword,
    page: params.page,
    pageSize: params.pageSize,
  })
  return apiRequest<PagedResult<AdminPageListItemDto>>(`/api/v1/admin/${club}/pages${query}`)
}

export function getAdminPage(club: string, id: string): Promise<AdminPageDetailDto> {
  return apiRequest<AdminPageDetailDto>(`/api/v1/admin/${club}/pages/${id}`)
}

export interface SavePagePayload {
  slug: string
  seo: AdminPageSeoInputDto
  blocks: AdminPageBlockInputDto[]
}

export interface UpdatePagePayload extends SavePagePayload {
  expectedUpdatedAt: string
}

/** 組出建立／更新頁面共用的 `multipart/form-data`：固定 `payload`（JSON 文字）欄位，
 * 加上每一個待上傳圖片各自的 `file:{區塊索引}:{圖片路徑}` 欄位（見
 * `@/utils/pageBlockSerializer.ts` 的 `serializeBlocksForSubmit`）。呼叫這支函式之前，
 * 圖片只存在瀏覽器記憶體，沒有任何 HTTP 請求送出過（規劃書「選檔不上傳、儲存才上傳」）。 */
function buildPageFormData(payload: SavePagePayload | UpdatePagePayload, files: Record<string, File>): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  for (const [fieldName, file] of Object.entries(files)) {
    form.append(fieldName, file)
  }
  return form
}

export function createAdminPage(club: string, payload: SavePagePayload, files: Record<string, File>): Promise<AdminPageDetailDto> {
  return apiUploadRequest<AdminPageDetailDto>(`/api/v1/admin/${club}/pages`, buildPageFormData(payload, files), { method: 'POST' })
}

export function updateAdminPage(club: string, id: string, payload: UpdatePagePayload, files: Record<string, File>): Promise<AdminPageDetailDto> {
  return apiUploadRequest<AdminPageDetailDto>(`/api/v1/admin/${club}/pages/${id}`, buildPageFormData(payload, files), { method: 'PUT' })
}

export function publishAdminPage(club: string, id: string, expectedUpdatedAt: string): Promise<AdminPageDetailDto> {
  return apiRequest<AdminPageDetailDto>(`/api/v1/admin/${club}/pages/${id}/publish`, { method: 'POST', body: { expectedUpdatedAt } })
}

export function scheduleAdminPage(club: string, id: string, expectedUpdatedAt: string, publishAt: string): Promise<AdminPageDetailDto> {
  return apiRequest<AdminPageDetailDto>(`/api/v1/admin/${club}/pages/${id}/schedule`, { method: 'POST', body: { expectedUpdatedAt, publishAt } })
}

export function deleteAdminPage(club: string, id: string, expectedUpdatedAt: string): Promise<void> {
  const query = buildQuery({ expectedUpdatedAt })
  return apiRequest<void>(`/api/v1/admin/${club}/pages/${id}${query}`, { method: 'DELETE' })
}

export function listAdminPageVersions(club: string, id: string, page?: number, pageSize?: number): Promise<PagedResult<AdminPageVersionListItemDto>> {
  const query = buildQuery({ page, pageSize })
  return apiRequest<PagedResult<AdminPageVersionListItemDto>>(`/api/v1/admin/${club}/pages/${id}/versions${query}`)
}

export function getAdminPageVersion(club: string, id: string, versionNo: number): Promise<AdminPageVersionDetailDto> {
  return apiRequest<AdminPageVersionDetailDto>(`/api/v1/admin/${club}/pages/${id}/versions/${versionNo}`)
}

/** 還原＝以舊版內容產生一個新版本，不改變目前的發布狀態（apps/api 的執行層判斷，見 README）。 */
export function restoreAdminPageVersion(club: string, id: string, versionNo: number, expectedUpdatedAt: string): Promise<AdminPageDetailDto> {
  return apiRequest<AdminPageDetailDto>(`/api/v1/admin/${club}/pages/${id}/versions/${versionNo}/restore`, {
    method: 'POST',
    body: { expectedUpdatedAt },
  })
}
