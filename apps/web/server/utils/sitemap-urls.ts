// server/utils/sitemap-urls.ts — sitemap 網址清單的共用邏輯（docs/18-work-errors.md E-18）。
//
// 抽出成獨立檔案是因為 server/api/__sitemap__/urls.ts（既有的資料端點，S0-9a／S0-9e
// 已驗證依 club 過濾正確）與 server/routes/sitemap.xml.ts（本次新增，自組 XML）
// 兩處都要用同一份資料，不得各自寫一份判斷（單元開關只有一個真實來源，
// docs/13-blue-whale-site.md §6 紀律 3）。
//
// 呼叫鏈：isUnitEnabledForClub()（shared/utils/units.ts）→ getEnabledSiteUnits()
// （shared/utils/site-units.ts）→ 這裡；新聞逐篇網址呼叫既有公開讀取端點
// （GET /api/v1/{club}/news，同一套 server/utils/backend-api.ts 基底網址），
// 不是另開一條路。try/catch 是防禦性寫法：apps/api 若暫時連不上，退回只有單元清單，
// 不讓整支路由連 200 都回不了（沿用 urls.ts 原本的設計）。
export interface SitemapUrlEntry {
  loc: string
}

export async function getSitemapUrls(club: string): Promise<SitemapUrlEntry[]> {
  const unitUrls = getEnabledSiteUnits(club).map((unit) => ({ loc: unit.path }))

  try {
    const result = await $fetch<{ items: { slug: string }[] }>(`/api/v1/${club}/news`, {
      baseURL: backendApiBase(),
      query: { pageSize: 200, lang: 'zh' },
    })
    const articleUrls = (result.items ?? []).map((a) => ({ loc: `/zh/news/${a.slug}/` }))
    return [...unitUrls, ...articleUrls]
  } catch {
    return unitUrls
  }
}
