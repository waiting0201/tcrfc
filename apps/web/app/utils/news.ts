// app/utils/news.ts — 新聞中心（07 單元）共用工具（S0-9 資料驅動頁搬遷）
//
// 由 site/src/pages/zh/news/*/index.html 搬到 Nuxt 時，10 頁裡有 6 頁共用同一份
// 59 行 client script（docs/13-blue-whale-site.md §6），另外 8 頁共用同一個
// 「分類 Tab（<a> 連結）」區塊。這裡集中兩件事：分類中繼資料（純靜態文案，不是
// 從 API 拿的）、把 ArticleListItemDto 轉成卡片需要的顯示值（日期格式、封面圖）。
//
// 封面圖規則（N1／E-152）：API 有 coverUrl 就用；沒有時，只有 scripts/site-images.txt 列出的
// 舊文章才用本地 mockup 圖（NEWS_LOCAL_COVER_SLUGS 白名單），其餘畫佔位標誌，不發圖片請求。

import siteImagesManifest from '~~/scripts/site-images.txt?raw'

export interface NewsCategoryMeta {
  code: string
  /** 分類 Tab 顯示文字，含規劃書編號（例："7.1 俱樂部新聞"） */
  label: string
  /** 英文分類名（不含編號），逐字取自各分類頁 <h1><span class="en"> 的既有文字
   *  （例如 app/pages/zh/news/club.vue 的 "Club News"）。S0-9e 新增：文章詳情頁
   *  的 page-hero__eyebrow（"7.2 Match Reports"）與「分類」meta 欄（"比賽報導 Match
   *  Reports"）都要用到英文分類名，這裡集中一份，不在文章頁另開一份新的對照表。 */
  enLabel: string
}

/** 07 單元的 8 個新聞分類（規劃書 v1.8 §3.7，靜態文案，來源見 site/src/partials/header.html mega menu） */
export const NEWS_CATEGORIES: readonly NewsCategoryMeta[] = [
  { code: 'club', label: '7.1 俱樂部新聞', enLabel: 'Club News' },
  { code: 'match', label: '7.2 比賽報導', enLabel: 'Match Reports' },
  { code: 'academy', label: '7.3 學院新聞', enLabel: 'Academy News' },
  { code: 'player-stories', label: '7.4 球員故事', enLabel: 'Player Stories' },
  { code: 'international', label: '7.5 國際動態', enLabel: 'International' },
  { code: 'camps-events', label: '7.6 營隊與活動', enLabel: 'Camps & Events' },
  { code: 'community', label: '7.7 社區活動', enLabel: 'Community' },
  { code: 'media', label: '7.8 媒體專區', enLabel: 'Media' },
] as const

/**
 * 分類 Tab 顯示文字——單一來源，取代 NewsCategoryTabs.vue／news/index.vue 各自算一次
 * （BW-C1 品牌外洩全站盤點發現：news/index.vue 的篩選按鈕直接讀 `cat.label`，繞過了
 * NewsCategoryTabs.vue 那份已修正的俱樂部分支，本輪合併成這支函式，兩處都呼叫它）。
 * `academy` 分類固定寫死「學院」，藍鯨依 docs/13-blue-whale-site.md §3 一律稱「青年隊」，
 * 其餘分類兩俱樂部共用同一份靜態文案。
 */
export function newsCategoryTabLabel(categoryCode: string, club: string): string {
  const meta = NEWS_CATEGORIES.find((c) => c.code === categoryCode)
  if (!meta) return ''
  if (meta.code === 'academy') {
    return `7.3 ${getClubIdentity(club).academyShortLabelZh}新聞`
  }
  return meta.label
}

/** 文章詳情頁 page-hero__eyebrow 用："{規劃書編號} {英文分類名}"（例："7.2 Match Reports"），
 *  逐字比對各分類頁 <p class="page-hero__eyebrow"> 的既有文字。查無分類（理論上不會發生，
 *  公開 API 的 categoryCode 一律來自 article_categories 主檔）時回傳空字串，交由呼叫端決定
 *  要不要顯示這一行，不在這裡塞假文字。 */
export function newsEyebrowText(categoryCode: string): string {
  const meta = NEWS_CATEGORIES.find((c) => c.code === categoryCode)
  if (!meta) return ''
  const num = meta.label.split(' ')[0]
  return `${num} ${meta.enLabel}`
}

/** 文章詳情頁「分類」meta 欄用："{中文分類名} {英文分類名}"（例："比賽報導 Match Reports"）。
 *  中文分類名取自 API 回傳的 categoryName（已依語系挑好），不是這份靜態表——避免兩份中文
 *  分類名不同步；這裡只補英文半邊。 */
export function newsCategoryBilingualLabel(categoryCode: string, categoryNameZh: string | null | undefined): string {
  const meta = NEWS_CATEGORIES.find((c) => c.code === categoryCode)
  const zh = categoryNameZh ?? ''
  return meta ? `${zh} ${meta.enLabel}`.trim() : zh
}

// 本地（mockup 時代）封面圖的「有圖」白名單。來源是已納版控的 scripts/site-images.txt
// （與 Azure Blob 上 site/news/{slug}.webp 一一對應，check-site-images.mjs 守一致性），
// 建置期以 ?raw 讀入。不看 public/assets/img 實體檔：該目錄不納版控、不進映像檔，
// 乾淨 checkout 與正式環境都不存在，照實體檔會讓全部文章退回佔位。
// 清單裡沒有的 slug（含新建、沒上傳封面的文章）＝沒有本地圖，一律畫佔位，不發圖片請求。

export const NEWS_LOCAL_COVER_SLUGS: ReadonlySet<string> = new Set(
  siteImagesManifest
    .split(/\r?\n/)
    .map((l: string) => /^news\/(.+)\.jpg$/.exec(l.trim())?.[1])
    .filter((s: string | undefined): s is string => !!s),
)

/**
 * 本地封面圖（news 目錄下的 slug.jpg）全部是**磐石**文章的照片（含未成年學員）。
 * 藍鯨站一律視為沒有本地封面（E-83：藍鯨站不得輸出任何磐石圖片），改走無圖佔位。
 * 磐石站只有清單內的舊文章才有本地圖（白名單），其餘回 false（E-152）。
 */
export function hasNewsCover(slug: string, club: 'tcrfc' | 'bw' = 'tcrfc'): boolean {
  if (club === 'bw') return false
  return NEWS_LOCAL_COVER_SLUGS.has(slug)
}

/** 封面圖相關欄位（公開 API `ArticleListItemDto`／`ArticleDetailDto`，S0-7h G 批；無封面時三者皆 null）。 */
export interface NewsCoverFields {
  slug: string
  title?: string | null
  coverUrl?: string | null
  coverWidth?: number | null
  coverHeight?: number | null
  coverAlt?: string | null
}

export interface NewsCoverImg {
  src: string
  /** 來自 API 的封面寬高；null 時模板不帶 width／height（不發明尺寸）。 */
  width: number | null
  height: number | null
  /** 後台「圖片說明」（已依語系回退）；空值回退文章標題。 */
  alt: string
  /** 後台上傳封面的 1280／640／320 衍生檔 srcset（規劃書 §4.0）；本地過渡圖或寬高未知時為 undefined。 */
  srcset?: string
}

/**
 * 後台上傳的封面 → srcset（w 描述子為衍生檔實際寬度）。規則與站台照片共用 derivativeSrcset
 * （app/utils/siteImage.ts）；封面的 width／height 是主檔真實尺寸，故 capToSource=true。
 */
export function coverSrcset(url: string, width: number | null | undefined, height: number | null | undefined): string | undefined {
  return derivativeSrcset(url, width, height, true)
}

/**
 * 卡片／詳情封面圖的單一來源（S0-7h 前台）。
 * 1. API 有 `coverUrl`（後台上傳的真封面）：用它，寬高取 `coverWidth`／`coverHeight`，null 就不帶。
 * 2. 沒有 `coverUrl`、但是 mockup 時代的本地照片仍存在（`hasNewsCover`）：沿用本地圖與其實際尺寸 1600×1067
 *    （過渡用，後台上傳封面後自然被 1. 取代）。
 * 3. 兩者都沒有：回 null，呼叫端改畫佔位標誌。
 * alt 一律「coverAlt（trim 後非空）→ 文章標題 → 空字串」。
 */
export function newsCoverImg(a: NewsCoverFields, club: 'tcrfc' | 'bw' = 'tcrfc'): NewsCoverImg | null {
  const alt = a.coverAlt?.trim() || a.title?.trim() || ''
  if (a.coverUrl) {
    return {
      src: a.coverUrl,
      width: a.coverWidth ?? null,
      height: a.coverHeight ?? null,
      alt,
      srcset: coverSrcset(a.coverUrl, a.coverWidth, a.coverHeight),
    }
  }
  if (hasNewsCover(a.slug, club)) {
    return { src: newsCoverSrc(a.slug), width: 1600, height: 1067, alt, srcset: siteImgSrcset(`/assets/img/news/${a.slug}.jpg`, 1600, 1067) }
  }
  return null
}

/** 無封面圖時的佔位標誌（磐石＝磐石標誌、藍鯨＝藍鯨隊徽，藍鯨站不得出現磐石標誌）。 */
export function newsFallbackMarkSrc(club: 'tcrfc' | 'bw' = 'tcrfc'): string {
  return club === 'bw' ? '/assets/brand/bw/bw-crest-512.png' : '/assets/brand/svg/tcrfc-mark-black.svg'
}

export function newsCoverSrc(slug: string): string {
  return siteImg(`/assets/img/news/${slug}.jpg`)
}

const MONTH_NAMES_EN = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

/** ISO 時間字串（API publishedAt）→ <time datetime> 用的日期部分，例："2026-08-10" */
export function newsIsoDate(publishedAt: string | null | undefined): string {
  if (!publishedAt) return ''
  return publishedAt.slice(0, 10)
}

/** ISO 時間字串 → 顯示用斜線日期，例："2026/08/10" */
export function newsSlashDate(publishedAt: string | null | undefined): string {
  const iso = newsIsoDate(publishedAt)
  return iso.replaceAll('-', '/')
}

/** 英文版日期（主站 /en/）：ISO 日期前 10 碼 → "10 Aug 2026"；格式不合回空字串。 */
export function newsDateEn(publishedAt: string | null | undefined): string {
  const iso = newsIsoDate(publishedAt)
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso)
  if (!m) return ''
  const mon = MONTH_NAMES_EN[Number(m[2]) - 1]?.slice(0, 3)
  return mon ? `${Number(m[3])} ${mon} ${m[1]}` : ''
}

/** data-year 篩選用：四碼年份 */
export function newsYearAttr(publishedAt: string | null | undefined): string {
  return newsIsoDate(publishedAt).slice(0, 4)
}

/** data-month 篩選用：兩碼月份 */
export function newsMonthAttr(publishedAt: string | null | undefined): string {
  return newsIsoDate(publishedAt).slice(5, 7)
}

/** data-title 篩選用：小寫標題（比照原 mockup 的關鍵字搜尋比對邏輯） */
export function newsTitleAttr(title: string | null | undefined): string {
  return (title ?? '').toLowerCase()
}

/** 從一批文章計算年份選項（新到舊），供年月篩選表單使用 */
export function newsDistinctYears(items: { publishedAt: string | null }[]): string[] {
  const years = new Set(items.map((i) => newsYearAttr(i.publishedAt)).filter(Boolean))
  return Array.from(years).sort((a, b) => b.localeCompare(a))
}

/** 從一批文章計算月份選項（1 月到 12 月），供年月篩選表單使用 */
export function newsDistinctMonths(items: { publishedAt: string | null }[]): string[] {
  const months = new Set(items.map((i) => newsMonthAttr(i.publishedAt)).filter(Boolean))
  return Array.from(months).sort((a, b) => a.localeCompare(b))
}

export function newsMonthLabel(month: string): string {
  return `${Number.parseInt(month, 10)}月`
}

/** 英文版月份篩選選項文字（主站 /en/）：`'03'` → "March"。 */
export function newsMonthLabelEn(month: string): string {
  return MONTH_NAMES_EN[Number.parseInt(month, 10) - 1] ?? month
}

/**
 * 英文版分類名稱（主站 /en/）：已知分類取 `NEWS_CATEGORY_EN`（docs/06 §1.1 對照表，例 Press & Media），
 * 未知分類退回 API 回傳的分類名稱（後端已依語系挑好）。藍鯨（B-5）傳 `club='bw'`：04 為 Youth News。
 */
export function newsCategoryLabelEn(categoryCode: string, fallbackName: string | null | undefined, club: 'tcrfc' | 'bw' = 'tcrfc'): string {
  return newsCategoryBareLabelEn(categoryCode, club) ?? fallbackName ?? ''
}

export interface NewsTagOption {
  slug: string
  name: string
}

/** 從一批文章計算標籤篩選選項（S1-17 新增，規劃書 3.7「標籤篩選」）。ArticleListItemDto.tags
 * 是 S1-5 就已經回傳的既有欄位，前台一直沒有消費——這裡補上單一來源的「這批文章有哪些標籤」
 * 計算，依名稱字母序排列（標籤沒有既定的顯示順序可循，比照年月篩選「新到舊」這類穩定排序的
 * 精神，選一個可預期的排序而不是依資料庫回傳順序）。同一個 slug 只取第一次出現的 name
 * （不同文章的同一個標籤，name 理論上一致）。 */
export function newsDistinctTags(items: { tags: { slug: string, name: string | null }[] }[]): NewsTagOption[] {
  const bySlug = new Map<string, string>()
  for (const item of items) {
    for (const tag of item.tags ?? []) {
      if (!bySlug.has(tag.slug)) bySlug.set(tag.slug, tag.name ?? tag.slug)
    }
  }
  return Array.from(bySlug, ([slug, name]) => ({ slug, name })).sort((a, b) => a.name.localeCompare(b.name, 'zh-Hant'))
}
