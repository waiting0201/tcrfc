// server/utils/sitemap-urls.ts — sitemap 網址清單的共用邏輯（docs/18-work-errors.md E-18）。
//
// 抽出成獨立檔案是因為 server/api/__sitemap__/urls.ts（既有的資料端點，S0-9a／S0-9e
// 已驗證依 club 過濾正確）與 server/routes/sitemap.xml.ts（本次新增，自組 XML）
// 兩處都要用同一份資料，不得各自寫一份判斷（單元開關只有一個真實來源，
// docs/13-blue-whale-site.md §6 紀律 3）。
//
// 呼叫鏈：isUnitEnabledForClub()（shared/utils/units.ts）→ getEnabledSiteUnits()
// （shared/utils/site-units.ts）→ 這裡；新聞逐篇網址改呼叫 S1-12 新增的
// GET /api/v1/{club}/seo/sitemap-entries（apps/api，Features/Seo/SeoRepository.cs），
// 取代原本直接打 /news 列表端點的寫法——後者不知道 is_noindex／is_excluded_from_sitemap
// 這兩個 S1-12 新增欄位，會把管理員刻意排除的文章也列進 sitemap。這支後端端點目前只涵蓋
// Article（新聞逐篇頁是唯一有真實動態路由的內容型別，見該檔案的檔頭說明），其餘 79 頁單元
// 仍由 getEnabledSiteUnits 提供，兩者互不重疊。try/catch 是防禦性寫法：apps/api 若暫時連不上，
// 退回只有單元清單，不讓整支路由連 200 都回不了（沿用既有設計）。
export interface SitemapUrlEntry {
  loc: string
  lastmod?: string
}

interface SitemapEntryResponse {
  path: string
  lastModifiedAt?: string | null
}

export async function getSitemapUrls(club: string): Promise<SitemapUrlEntry[]> {
  const unitUrls: SitemapUrlEntry[] = getEnabledSiteUnits(club).map((unit) => ({ loc: unit.path }))

  try {
    const entries = await $fetch<SitemapEntryResponse[]>(`/api/v1/${club}/seo/sitemap-entries`, {
      baseURL: backendApiBase(),
    })
    const articleUrls: SitemapUrlEntry[] = (entries ?? []).map((e) => ({
      loc: e.path,
      lastmod: e.lastModifiedAt ?? undefined,
    }))
    return [...unitUrls, ...articleUrls]
  } catch {
    return unitUrls
  }
}
