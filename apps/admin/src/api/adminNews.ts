/**
 * `apps/api` 後台新聞端點（`Features/AdminNews`）的型別與呼叫函式，
 * 對照 apps/api/README.md「端點清單」「後台新聞（B2）寫入垂直切片」。
 */
import { apiRequest } from './http'
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

export interface SaveArticlePayload {
  slug: string
  categoryCode: string
  coverKey?: string | null
  isFeatured: boolean
  content: AdminArticleContentInputDto
}

export function createAdminNews(club: string, payload: SaveArticlePayload): Promise<AdminArticleDetailDto> {
  return apiRequest<AdminArticleDetailDto>(`/api/v1/admin/${club}/news`, { method: 'POST', body: payload })
}

export function updateAdminNews(
  club: string,
  id: string,
  payload: SaveArticlePayload & { expectedUpdatedAt: string },
): Promise<AdminArticleDetailDto> {
  return apiRequest<AdminArticleDetailDto>(`/api/v1/admin/${club}/news/${id}`, { method: 'PUT', body: payload })
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
    coverImageUrl: null, // 本輪不做上傳管線，coverKey 不是可直接顯示的網址，見 apps/api/README.md
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
    coverKey: article.coverKey,
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
