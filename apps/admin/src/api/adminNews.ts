/**
 * `apps/api` 後台新聞端點（`Features/AdminNews`）的型別與呼叫函式，
 * 對照 apps/api/README.md「端點清單」「後台新聞（B2）寫入垂直切片」。
 */
import { apiRequest, apiUploadRequest } from './http'
import type { NewsArticle, NewsCategory } from '@/types/news'
import type { ContentStatus } from '@/types/common'

// ── 後端 DTO（逐欄位對照 Features/AdminNews/AdminArticleDtos.cs，不自行增減欄位）──────────

export interface AdminArticleLocaleContentDto {
  title?: string | null
  summary?: string | null
  body?: string | null
  seoTitle?: string | null
  seoDescription?: string | null
}

export interface AdminArticleContentInputDto {
  zh: AdminArticleLocaleContentDto
  en?: AdminArticleLocaleContentDto | null
}

export interface AdminArticleListItemDto {
  id: string
  slug: string
  categoryCode: string
  coverKey?: string | null
  isFeatured: boolean
  /** `draft`／`published`／`scheduled`，⛔ 沒有 `disabled`（articles.status 的 CHECK 約束只有三態） */
  status: 'draft' | 'published' | 'scheduled'
  publishedAt?: string | null
  isShared: boolean
  updatedAt: string
  titleZh?: string | null
  titleEn?: string | null
}

export interface AdminArticleDetailDto {
  id: string
  slug: string
  categoryCode: string
  coverKey?: string | null
  isFeatured: boolean
  status: 'draft' | 'published' | 'scheduled'
  publishedAt?: string | null
  isShared: boolean
  updatedAt: string
  zh: AdminArticleLocaleContentDto
  en?: AdminArticleLocaleContentDto | null
}

export interface AdminArticlePage {
  items: AdminArticleListItemDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ListAdminNewsParams {
  status?: ContentStatus | ''
  category?: NewsCategory | ''
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

export function listAdminNews(club: string, params: ListAdminNewsParams = {}): Promise<AdminArticlePage> {
  const query = buildQuery({
    status: params.status,
    category: params.category,
    keyword: params.keyword,
    page: params.page,
    pageSize: params.pageSize,
  })
  return apiRequest<AdminArticlePage>(`/api/v1/admin/${club}/news${query}`)
}

export function getAdminNewsById(club: string, id: string): Promise<AdminArticleDetailDto> {
  return apiRequest<AdminArticleDetailDto>(`/api/v1/admin/${club}/news/${id}`)
}

/**
 * 🔴🔴🔴 S0-8 修正（2026-09-22，規劃書「選檔不上傳、儲存才上傳」）：**不含封面圖片鍵**——
 * 封面圖片改成建立／更新時跟這份 payload 一起放進同一個 `multipart/form-data` 請求的 `file`
 * 欄位，由伺服器端處理上傳並把結果寫進 `coverKey`，呼叫端不會、也不能自己指定物件鍵字串
 * （見 apps/api/README.md「圖片上傳共用元件」整節的新契約、`AdminArticleDtos.cs` 的
 * `CreateArticleRequest`／`UpdateArticleRequest`）。
 */
export interface SaveArticlePayload {
  slug: string
  categoryCode: string
  isFeatured: boolean
  content: AdminArticleContentInputDto
}

/** PUT 專用：多了並行權杖與封面圖片三態裡「清空」那一態的旗標（`UpdateArticleRequest.RemoveCover`）。
 * 「換新」與「維持不變」不需要欄位表達——換新看這次請求有沒有夾 `file`，維持不變是兩者都沒有時的
 * 預設值。`removeCover` 與 `file` 同時出現時後端回 400（請求矛盾），呼叫端不應該讓兩者同時發生。 */
export interface UpdateArticlePayload extends SaveArticlePayload {
  expectedUpdatedAt: string
  removeCover: boolean
}

/** 組出建立／更新文章共用的 `multipart/form-data`：固定 `payload`（JSON 文字）欄位，`coverFile`
 * 非 `null` 時才附上 `file` 欄位——這正是「選檔不上傳、儲存才上傳」在請求層級的落地：呼叫這支函式
 * 之前，圖片只存在瀏覽器記憶體（`ImageUploader.vue` 的本機預覽），沒有任何 HTTP 請求送出過。 */
function buildArticleFormData(payload: SaveArticlePayload | UpdateArticlePayload, coverFile: File | null): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (coverFile) form.append('file', coverFile)
  return form
}

export function createAdminNews(club: string, payload: SaveArticlePayload, coverFile: File | null): Promise<AdminArticleDetailDto> {
  return apiUploadRequest<AdminArticleDetailDto>(
    `/api/v1/admin/${club}/news`,
    buildArticleFormData(payload, coverFile),
    { method: 'POST' },
  )
}

export function updateAdminNews(
  club: string,
  id: string,
  payload: UpdateArticlePayload,
  coverFile: File | null,
): Promise<AdminArticleDetailDto> {
  return apiUploadRequest<AdminArticleDetailDto>(
    `/api/v1/admin/${club}/news/${id}`,
    buildArticleFormData(payload, coverFile),
    { method: 'PUT' },
  )
}

export function publishAdminNews(club: string, id: string, expectedUpdatedAt: string): Promise<AdminArticleDetailDto> {
  return apiRequest<AdminArticleDetailDto>(`/api/v1/admin/${club}/news/${id}/publish`, {
    method: 'POST',
    body: { expectedUpdatedAt },
  })
}

export function scheduleAdminNews(
  club: string,
  id: string,
  expectedUpdatedAt: string,
  publishAt: string,
): Promise<AdminArticleDetailDto> {
  return apiRequest<AdminArticleDetailDto>(`/api/v1/admin/${club}/news/${id}/schedule`, {
    method: 'POST',
    body: { expectedUpdatedAt, publishAt },
  })
}

export function deleteAdminNews(club: string, id: string, expectedUpdatedAt: string): Promise<void> {
  const query = buildQuery({ expectedUpdatedAt })
  return apiRequest<void>(`/api/v1/admin/${club}/news/${id}${query}`, { method: 'DELETE' })
}

// ── DTO ↔ 畫面型別 對照 ─────────────────────────────────────────────────────────

function localeToBilingualPair(zh: AdminArticleLocaleContentDto, en: AdminArticleLocaleContentDto | null | undefined) {
  return {
    title: { zh: zh.title ?? '', en: en?.title ?? '' },
    content: { zh: zh.body ?? '', en: en?.body ?? '' },
    summary: { zh: zh.summary ?? '', en: en?.summary ?? '' },
    seoTitle: { zh: zh.seoTitle ?? '', en: en?.seoTitle ?? '' },
    seoDescription: { zh: zh.seoDescription ?? '', en: en?.seoDescription ?? '' },
  }
}

export function detailDtoToArticle(dto: AdminArticleDetailDto): NewsArticle {
  const pair = localeToBilingualPair(dto.zh, dto.en)
  return {
    id: dto.id,
    title: pair.title,
    urlName: dto.slug,
    category: dto.categoryCode as NewsCategory,
    // coverKey 不是可直接顯示的網址（物件儲存容器是私有的，也還沒有任何「用 key 換可顯示網址」
    // 的端點），這裡先固定給 null；等這條路徑真的補上時，把這行換成真正算出來的網址即可，
    // ImageUploader.vue 的 existingPreviewUrl 已經接好這個欄位，不需要再改呼叫端。見
    // apps/admin/README.md「圖片上傳共用元件的前端接線」已知限制。
    coverImageUrl: null,
    coverKey: dto.coverKey ?? null,
    isFeatured: dto.isFeatured,
    status: dto.status,
    statusAt: dto.publishedAt ?? undefined,
    isSharedContent: dto.isShared,
    updatedAt: dto.updatedAt,
    content: pair.content,
    summary: pair.summary,
    seoTitle: pair.seoTitle,
    seoDescription: pair.seoDescription,
  }
}

export function listItemDtoToArticle(dto: AdminArticleListItemDto): NewsArticle {
  return {
    id: dto.id,
    title: { zh: dto.titleZh ?? '', en: dto.titleEn ?? '' },
    urlName: dto.slug,
    category: dto.categoryCode as NewsCategory,
    coverImageUrl: null,
    coverKey: dto.coverKey ?? null,
    isFeatured: dto.isFeatured,
    status: dto.status,
    statusAt: dto.publishedAt ?? undefined,
    isSharedContent: dto.isShared,
    updatedAt: dto.updatedAt,
    content: { zh: '', en: '' },
    summary: { zh: '', en: '' },
    seoTitle: { zh: '', en: '' },
    seoDescription: { zh: '', en: '' },
  }
}

/**
 * 畫面表單 → 寫入 API 的 payload。
 * 🔴 PUT／POST 對雙語內容是整份取代語意：省略 `en` ＝ 清掉既有英文列。這裡的規則是
 * 「中文以外的四個英文欄位全部留白，才省略 `en`」——只清空其中一個欄位不會誤刪整份英文版，
 * 呼叫端（NewsEditView）在「原本有英文、現在四個欄位都清空了」這個情境要另外跳出確認，
 * 不能讓這個函式默默決定使用者的意圖。
 */
export function articleToSavePayload(article: NewsArticle): SaveArticlePayload {
  const isEnEmpty =
    !article.title.en.trim()
    && !article.content.en.trim()
    && !article.summary.en.trim()
    && !article.seoTitle.en.trim()
    && !article.seoDescription.en.trim()

  return {
    slug: article.urlName,
    categoryCode: article.category,
    isFeatured: article.isFeatured,
    content: {
      zh: {
        title: article.title.zh,
        summary: article.summary.zh || null,
        body: article.content.zh || null,
        seoTitle: article.seoTitle.zh || null,
        seoDescription: article.seoDescription.zh || null,
      },
      en: isEnEmpty
        ? undefined
        : {
            title: article.title.en || null,
            summary: article.summary.en || null,
            body: article.content.en || null,
            seoTitle: article.seoTitle.en || null,
            seoDescription: article.seoDescription.en || null,
          },
    },
  }
}
