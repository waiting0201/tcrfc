/**
 * `apps/api` 後台首頁編排端點（`Features/AdminBanners`／`Features/AdminHomeSections`），
 * 對照 apps/api/README.md「S1-6：B3 首頁編排／B4 常見問題」「S1-7a」。
 */
import { apiRequest, apiUploadRequest } from './http'

// ── Hero 輪播（banners）───────────────────────────────────────────────────────────

export interface AdminBannerLocaleContentDto {
  title?: string | null
  subtitle?: string | null
  imageAlt?: string | null
  cta1Label?: string | null
  cta1Url?: string | null
  cta2Label?: string | null
  cta2Url?: string | null
}

export interface AdminBannerContentInputDto {
  zh: AdminBannerLocaleContentDto
  en?: AdminBannerLocaleContentDto | null
}

/** 草稿／發布（v3.14）。新增一律是 `draft`；`published` 且落在上架期間內才會出現在公開端點
 * ——期間判斷在後端，畫面只用它做「目前是否在前台顯示」的提示。 */
export type AdminBannerStatus = 'draft' | 'published'

export interface AdminBannerListItemDto {
  id: string
  mediaType: 'image' | 'video'
  imageKey: string
  imageWidth?: number | null
  imageHeight?: number | null
  videoKey?: string | null
  status: AdminBannerStatus
  startAt?: string | null
  endAt?: string | null
  sortOrder: number
  updatedAt: string
  titleZh?: string | null
  titleEn?: string | null
}

export interface AdminBannerDetailDto {
  id: string
  mediaType: 'image' | 'video'
  imageKey: string
  imageWidth?: number | null
  imageHeight?: number | null
  videoKey?: string | null
  status: AdminBannerStatus
  startAt?: string | null
  endAt?: string | null
  sortOrder: number
  updatedAt: string
  zh: AdminBannerLocaleContentDto
  en?: AdminBannerLocaleContentDto | null
}

export interface SaveBannerPayload {
  /** 省略回退 `image`。`video`：`file` 欄位仍是必填（作為海報格），另需搭配 `video` 檔案
   * （見 `createAdminBanner`／`updateAdminBanner` 的 `videoFile` 參數）。 */
  mediaType?: 'image' | 'video'
  startAt?: string | null
  endAt?: string | null
  sortOrder: number
  content: AdminBannerContentInputDto
}

export function listAdminBanners(club: string): Promise<AdminBannerListItemDto[]> {
  return apiRequest<AdminBannerListItemDto[]>(`/api/v1/admin/${club}/banners`)
}

export function getAdminBanner(club: string, id: string): Promise<AdminBannerDetailDto> {
  return apiRequest<AdminBannerDetailDto>(`/api/v1/admin/${club}/banners/${id}`)
}

function buildBannerFormData(payload: SaveBannerPayload, file: File | null, videoFile: File | null): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (file) form.append('file', file)
  if (videoFile) form.append('video', videoFile)
  return form
}

/** 建立輪播：`file`（圖片，影片模式下作為海報格）必填（`banners.image_key` 是 `NOT NULL`，
 * 沒有「不放圖片」這個選項）。`videoFile`：`mediaType='video'` 時必填，否則不可帶。 */
export function createAdminBanner(
  club: string,
  payload: SaveBannerPayload,
  file: File,
  videoFile: File | null = null,
): Promise<AdminBannerDetailDto> {
  return apiUploadRequest<AdminBannerDetailDto>(
    `/api/v1/admin/${club}/banners`,
    buildBannerFormData(payload, file, videoFile),
    { method: 'POST' },
  )
}

/** 更新：`file` 省略＝維持原圖（沒有「移除」選項，只有「換一張」或「維持原圖」兩態）。
 * `videoFile` 省略＝維持既有影片（前提是原本就是影片模式且已有影片，否則後端回 400）。 */
export function updateAdminBanner(
  club: string,
  id: string,
  payload: SaveBannerPayload,
  file: File | null,
  videoFile: File | null = null,
): Promise<AdminBannerDetailDto> {
  return apiUploadRequest<AdminBannerDetailDto>(
    `/api/v1/admin/${club}/banners/${id}`,
    buildBannerFormData(payload, file, videoFile),
    { method: 'PUT' },
  )
}

export function deleteAdminBanner(club: string, id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/banners/${id}`, { method: 'DELETE' })
}

/** 發布：`draft` → `published`。沿用既有 `content.banner.update` 權限碼，冪等（已發布再打一次不報錯）。 */
export function publishAdminBanner(club: string, id: string): Promise<AdminBannerDetailDto> {
  return apiRequest<AdminBannerDetailDto>(`/api/v1/admin/${club}/banners/${id}/publish`, { method: 'POST' })
}

/** 改回草稿：`published` → `draft`。同上，冪等。 */
export function unpublishAdminBanner(club: string, id: string): Promise<AdminBannerDetailDto> {
  return apiRequest<AdminBannerDetailDto>(`/api/v1/admin/${club}/banners/${id}/unpublish`, { method: 'POST' })
}

// ── 首頁九大區塊（home-sections）──────────────────────────────────────────────────

export interface AdminHomeSectionDto {
  id: string
  sectionCode: string
  nameZh: string
  nameEn: string
  isEnabled: boolean
  sortOrder: number
  featuredBannerId?: string | null
  updatedAt: string
}

export interface UpdateHomeSectionPayload {
  isEnabled: boolean
  sortOrder: number
  /** 🔴 只有 `hero` 區塊可以填——其餘八個區塊送非 `null` 一律 400
   * （`home_sections.featured_banner_id` 是唯一一欄承載「精選指定」，見 apps/api/README.md「綱要缺口」第 2 點）。 */
  featuredBannerId?: string | null
}

export function listAdminHomeSections(club: string): Promise<AdminHomeSectionDto[]> {
  return apiRequest<AdminHomeSectionDto[]>(`/api/v1/admin/${club}/home-sections`)
}

export function updateAdminHomeSection(
  club: string,
  sectionCode: string,
  payload: UpdateHomeSectionPayload,
): Promise<AdminHomeSectionDto> {
  return apiRequest<AdminHomeSectionDto>(`/api/v1/admin/${club}/home-sections/${sectionCode}`, {
    method: 'PUT',
    body: payload,
  })
}
