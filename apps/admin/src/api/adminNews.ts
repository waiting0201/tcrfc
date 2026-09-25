/**
 * `apps/api` 後台新聞端點（`Features/AdminNews`）的型別與呼叫函式，
 * 對照 apps/api/README.md「端點清單」「後台新聞（B2）寫入垂直切片」。
 */
import { apiRequest, apiUploadRequest } from './http'
import type { CoreValueTag, NewsArticle, NewsCategory, NewsTag, RelationTargetType } from '@/types/news'
import type { ContentStatus } from '@/types/common'

// ── 後端 DTO（逐欄位對照 Features/AdminNews/AdminArticleDtos.cs，不自行增減欄位）──────────

/** 標籤（S1-5）。對照 `AdminArticleTagDto`。 */
export interface AdminArticleTagDto {
  slug: string
  nameZh?: string | null
  nameEn?: string | null
}

/** 關聯（S1-5）。對照 `AdminArticleRelationInput`——輸入與輸出共用同一個形狀。 */
export interface AdminArticleRelationDto {
  targetType: string
  targetId: string
}

export interface AdminArticleLocaleContentDto {
  title?: string | null
  summary?: string | null
  body?: string | null
  seoTitle?: string | null
  seoDescription?: string | null
  /** 關鍵字（S1-12 新增），逐語系。對應 `articles_i18n.seo_keywords`。 */
  seoKeywords?: string | null
  /** 分享圖片替代文字（S1-12 驗收退回後補做），逐語系。對應 `articles_i18n.og_image_alt`。 */
  ogImageAlt?: string | null
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
  /** 標籤（S1-5，列表頁沿用同一筆查詢附帶回傳） */
  tags: AdminArticleTagDto[]
  /** 瀏覽數（S1-5） */
  viewCount: number
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
  /** 手動覆寫正規網址（S1-12 新增）。`null`／空字串＝不覆寫。 */
  canonicalPath?: string | null
  /** 不讓搜尋引擎收錄這篇文章（S1-12 新增）。 */
  isNoindex: boolean
  /** 不列入網站地圖（S1-12 新增）。 */
  isExcludedFromSitemap: boolean
  /** 分享圖片完整網址（S1-12 驗收退回後補做），`null`＝沒有專屬分享圖片
   * （前台會依優先序回退到全站預設圖片，再回退到封面圖片）。 */
  ogImageUrl?: string | null
  ogImageWidth?: number | null
  ogImageHeight?: number | null
  zh: AdminArticleLocaleContentDto
  en?: AdminArticleLocaleContentDto | null
  /** 標籤（S1-5） */
  tags: AdminArticleTagDto[]
  /** 瀏覽數（S1-5） */
  viewCount: number
  /** 核心價值標籤（S1-5） */
  coreValueTags: string[]
  /** 關聯（S1-5） */
  relations: AdminArticleRelationDto[]
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
  /**
   * 🔴 標籤／核心價值標籤／關聯三欄（S1-5）：後端的語意是「省略＝維持不變、空陣列＝清空」
   * （更新時），但這裡的呼叫端（`articleToSavePayload`）**一律明確帶出目前畫面上的完整陣列**，
   * 不管有沒有變動、也不管是建立還是更新——因為畫面上這三個欄位現在都有完整的編輯介面，
   * `form` 裡存的本來就是「使用者現在想要的最終狀態」，直接把這個狀態當成明確值送出，
   * 效果上等同於「沒變就送回原值＝維持不變、清空了就送空陣列＝清空」，不需要額外去偵測
   * 「使用者到底有沒有碰過這個欄位」這種容易漏判的邏輯。**這裡永遠不會是 `undefined`**，
   * 型別上仍標成可省略是為了跟後端的 DTO 定義（`IReadOnlyList<...>?`）逐欄位對照。
   */
  tags?: AdminArticleTagDto[]
  coreValueTags?: string[]
  relations?: AdminArticleRelationDto[]
  /** 手動覆寫正規網址（S1-12 新增）。省略或空字串＝不覆寫。 */
  canonicalPath?: string | null
  isNoindex?: boolean
  isExcludedFromSitemap?: boolean
}

/** PUT 專用：多了並行權杖與封面圖片三態裡「清空」那一態的旗標（`UpdateArticleRequest.RemoveCover`）。
 * 「換新」與「維持不變」不需要欄位表達——換新看這次請求有沒有夾 `file`，維持不變是兩者都沒有時的
 * 預設值。`removeCover` 與 `file` 同時出現時後端回 400（請求矛盾），呼叫端不應該讓兩者同時發生。 */
export interface UpdateArticlePayload extends SaveArticlePayload {
  expectedUpdatedAt: string
  removeCover: boolean
  /** 勾選「移除分享圖片」（S1-12 驗收退回後補做）。跟這次請求的 `ogImage` 檔案欄位互斥。 */
  removeOgImage?: boolean
}

// ── 批次操作（S1-5）─────────────────────────────────────────────────────────────

export interface BatchOperationSkippedItemDto {
  id: string
  reason: string
}

export interface BatchOperationResultDto {
  updatedCount: number
  skipped: BatchOperationSkippedItemDto[]
}

export function batchChangeNewsCategory(club: string, ids: string[], categoryCode: string): Promise<BatchOperationResultDto> {
  return apiRequest<BatchOperationResultDto>(`/api/v1/admin/${club}/news/batch/category`, {
    method: 'POST',
    body: { ids, categoryCode },
  })
}

export function batchPublishNews(club: string, ids: string[]): Promise<BatchOperationResultDto> {
  return apiRequest<BatchOperationResultDto>(`/api/v1/admin/${club}/news/batch/publish`, {
    method: 'POST',
    body: { ids },
  })
}

/** 「下架」＝轉回草稿（`articles.status` 沒有獨立的下架值，見 apps/api/README.md「我的判斷」第 1 點：
 * 這是後端已回報、待業務確認的既有假設，前端沿用同一個判斷，畫面文字仍顯示「下架」。 */
export function batchUnpublishNews(club: string, ids: string[]): Promise<BatchOperationResultDto> {
  return apiRequest<BatchOperationResultDto>(`/api/v1/admin/${club}/news/batch/unpublish`, {
    method: 'POST',
    body: { ids },
  })
}

/** 組出建立／更新文章共用的 `multipart/form-data`：固定 `payload`（JSON 文字）欄位，`coverFile`
 * 非 `null` 時才附上 `file` 欄位，`ogImageFile`（S1-12 新增，分享圖片，獨立於封面圖片之外）非
 * `null` 時附上 `ogImage` 欄位——這正是「選檔不上傳、儲存才上傳」在請求層級的落地：呼叫這支函式
 * 之前，圖片只存在瀏覽器記憶體（`ImageUploader.vue` 的本機預覽），沒有任何 HTTP 請求送出過。 */
function buildArticleFormData(
  payload: SaveArticlePayload | UpdateArticlePayload,
  coverFile: File | null,
  ogImageFile: File | null,
): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (coverFile) form.append('file', coverFile)
  if (ogImageFile) form.append('ogImage', ogImageFile)
  return form
}

export function createAdminNews(
  club: string,
  payload: SaveArticlePayload,
  coverFile: File | null,
  ogImageFile: File | null = null,
): Promise<AdminArticleDetailDto> {
  return apiUploadRequest<AdminArticleDetailDto>(
    `/api/v1/admin/${club}/news`,
    buildArticleFormData(payload, coverFile, ogImageFile),
    { method: 'POST' },
  )
}

export function updateAdminNews(
  club: string,
  id: string,
  payload: UpdateArticlePayload,
  coverFile: File | null,
  ogImageFile: File | null = null,
): Promise<AdminArticleDetailDto> {
  return apiUploadRequest<AdminArticleDetailDto>(
    `/api/v1/admin/${club}/news/${id}`,
    buildArticleFormData(payload, coverFile, ogImageFile),
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
    seoKeywords: { zh: zh.seoKeywords ?? '', en: en?.seoKeywords ?? '' },
    ogImageAlt: { zh: zh.ogImageAlt ?? '', en: en?.ogImageAlt ?? '' },
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
    canonicalPath: dto.canonicalPath ?? '',
    isNoindex: dto.isNoindex,
    isExcludedFromSitemap: dto.isExcludedFromSitemap,
    ogImageUrl: dto.ogImageUrl ?? null,
    ogImageWidth: dto.ogImageWidth ?? null,
    ogImageHeight: dto.ogImageHeight ?? null,
    content: pair.content,
    summary: pair.summary,
    seoTitle: pair.seoTitle,
    seoDescription: pair.seoDescription,
    seoKeywords: pair.seoKeywords,
    ogImageAlt: pair.ogImageAlt,
    tags: dto.tags.map((t) => ({ slug: t.slug, nameZh: t.nameZh, nameEn: t.nameEn })),
    coreValueTags: dto.coreValueTags as CoreValueTag[],
    relations: dto.relations.map((r) => ({ targetType: r.targetType as RelationTargetType, targetId: r.targetId })),
    viewCount: dto.viewCount,
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
    // 列表查詢不回傳這些單頁 SEO 欄位（列表頁沒有畫面需要顯示），維持空值即可，
    // 不影響列表頁渲染；編輯頁一律走 detailDtoToArticle 才有真正的值。
    canonicalPath: '',
    isNoindex: false,
    isExcludedFromSitemap: false,
    ogImageUrl: null,
    ogImageWidth: null,
    ogImageHeight: null,
    content: { zh: '', en: '' },
    summary: { zh: '', en: '' },
    seoTitle: { zh: '', en: '' },
    seoDescription: { zh: '', en: '' },
    seoKeywords: { zh: '', en: '' },
    ogImageAlt: { zh: '', en: '' },
    tags: dto.tags.map((t) => ({ slug: t.slug, nameZh: t.nameZh, nameEn: t.nameEn })),
    // 列表查詢不回傳核心價值標籤與關聯（沒有畫面需要在列表頁顯示這兩者），維持空陣列即可，
    // 不影響列表頁渲染；編輯頁一律走 detailDtoToArticle 才有真正的值。
    coreValueTags: [],
    relations: [],
    viewCount: dto.viewCount,
  }
}

/**
 * 標籤自動完成建議清單（S1-5）。刻意不新增端點——直接沿用既有的後台列表查詢
 * （`content.article.view` 權限，寫新聞的角色本來就有），把目前這個俱樂部所有文章已經在用的
 * 標籤去重彙整起來，當作「輸入時可以選的既有標籤」。`pageSize=200` 一次抓滿（目前全系統
 * 83 篇文章，遠低於這個數字），避免另外處理分頁。
 */
export async function fetchTagSuggestions(club: string): Promise<NewsTag[]> {
  const page = await listAdminNews(club, { pageSize: 200 })
  const bySlug = new Map<string, NewsTag>()
  for (const item of page.items) {
    for (const tag of item.tags) {
      if (!bySlug.has(tag.slug)) {
        bySlug.set(tag.slug, { slug: tag.slug, nameZh: tag.nameZh, nameEn: tag.nameEn })
      }
    }
  }
  return Array.from(bySlug.values())
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
    && !article.seoKeywords.en.trim()
    && !article.ogImageAlt.en.trim()

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
        seoKeywords: article.seoKeywords.zh || null,
        ogImageAlt: article.ogImageAlt.zh || null,
      },
      en: isEnEmpty
        ? undefined
        : {
            title: article.title.en || null,
            summary: article.summary.en || null,
            body: article.content.en || null,
            seoTitle: article.seoTitle.en || null,
            seoDescription: article.seoDescription.en || null,
            seoKeywords: article.seoKeywords.en || null,
            ogImageAlt: article.ogImageAlt.en || null,
          },
    },
    // 一律明確帶出畫面目前的完整陣列，理由見 SaveArticlePayload 型別定義上的說明。
    tags: article.tags.map((t) => ({ slug: t.slug, nameZh: t.nameZh ?? undefined, nameEn: t.nameEn ?? undefined })),
    coreValueTags: article.coreValueTags,
    relations: article.relations.map((r) => ({ targetType: r.targetType, targetId: r.targetId })),
    canonicalPath: article.canonicalPath || null,
    isNoindex: article.isNoindex,
    isExcludedFromSitemap: article.isExcludedFromSitemap,
  }
}
