// app/utils/news.ts — 新聞中心（07 單元）共用工具（S0-9 資料驅動頁搬遷）
//
// 由 site/src/pages/zh/news/*/index.html 搬到 Nuxt 時，10 頁裡有 6 頁共用同一份
// 59 行 client script（docs/13-blue-whale-site.md §6），另外 8 頁共用同一個
// 「分類 Tab（<a> 連結）」區塊。這裡集中兩件事：分類中繼資料（純靜態文案，不是
// 從 API 拿的）、把 ArticleListItemDto 轉成卡片需要的顯示值（日期格式、封面圖）。
//
// 🔴 已知資料落差（回報用，不在此檔修補）：
//   `apps/api` 的 ArticleListItemDto.coverKey 目前種子資料全為 NULL
//   （apps/api/README.md「已知落差」／docs/12d-field-audit.md）。封面圖改用
//   slug 對應 site/src/assets/img/news/{slug}.jpg 的既有檔名慣例（8 位隊別新聞圖
//   全部依 slug 命名，已用 83 篇種子資料逐一核對）。NEWS_NO_COVER_SLUGS 是用同一批
//   種子資料實際比對 public/assets/img/news/ 目錄得出的唯一例外（"2025-05-17-match-051"
//   ＝「總統盃八強賽」，mockup 原本就用隊徽 mark 取代照片），不是憑印象猜的。

export interface NewsCategoryMeta {
  code: string
  /** 分類 Tab 顯示文字，含規劃書編號（例："7.1 俱樂部新聞"） */
  label: string
}

/** 07 單元的 8 個新聞分類（規劃書 v1.8 §3.7，靜態文案，來源見 site/src/partials/header.html mega menu） */
export const NEWS_CATEGORIES: readonly NewsCategoryMeta[] = [
  { code: 'club', label: '7.1 俱樂部新聞' },
  { code: 'match', label: '7.2 比賽報導' },
  { code: 'academy', label: '7.3 學院新聞' },
  { code: 'player-stories', label: '7.4 球員故事' },
  { code: 'international', label: '7.5 國際動態' },
  { code: 'camps-events', label: '7.6 營隊與活動' },
  { code: 'community', label: '7.7 社區活動' },
  { code: 'media', label: '7.8 媒體專區' },
] as const

/** 唯一已知缺封面圖的文章 slug（見檔頭說明），其餘一律用 newsCoverSrc() 推導路徑 */
export const NEWS_NO_COVER_SLUGS: ReadonlySet<string> = new Set(['2025-05-17-match-051'])

export function hasNewsCover(slug: string): boolean {
  return !NEWS_NO_COVER_SLUGS.has(slug)
}

export function newsCoverSrc(slug: string): string {
  return `/assets/img/news/${slug}.jpg`
}

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
