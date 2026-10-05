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

interface CharityProgramsResponse {
  items?: Array<{ slug: string }>
}

/**
 * S2-9：11.2 慈善計畫詳情（`/zh/charity/programs/{slug}/`）是第二種有真實動態路由的內容型別。
 * 只有開放 11 單元的俱樂部才列（藍鯨不設 11，列了會是 404 網址）；公開端點只回已發布計畫。
 * `seo/sitemap-entries` 目前只涵蓋 Article，這裡直接讀 `charity/programs`（單次最多 50 筆，慈善計畫數量
 * 遠低於此；超過時只收最前 50 筆——見 apps/web/README.md「S2-9」節範圍縮減）。失敗一律回空陣列，不影響其餘網址。
 */
async function getCharityProgramUrls(club: string): Promise<SitemapUrlEntry[]> {
  if (!isUnitEnabledForClub('11', club)) return []
  try {
    const res = await $fetch<CharityProgramsResponse>(`/api/v1/${club}/charity/programs`, {
      baseURL: backendApiBase(),
      query: { pageSize: 50 },
    })
    return (res?.items ?? []).map((p) => ({ loc: `/zh/charity/programs/${p.slug}/` }))
  } catch {
    return []
  }
}

interface ShopProductsResponse {
  items?: Array<{ slug: string }>
}

/**
 * S3-5：8.3 商店商品詳情（`/zh/shop/{slug}/`）是第三種有真實動態路由的內容型別。商品列表端點只回已上架商品
 * （草稿、別隊商品不會出現）；單次最多 60 筆（後端 maxPageSize），超過只收最前 60 筆。
 * 列網址是公開資訊（不是庫存），所以讀一次直連後端、不經 BFF；失敗一律回空陣列，不影響其餘網址。
 * 購物車、結帳、訂單查詢頁**不列入**（本來就不在 SITE_UNITS，且都是 noindex 的個人化頁面）。
 */
async function getShopProductUrls(club: string): Promise<SitemapUrlEntry[]> {
  if (!isUnitEnabledForClub('8.3', club)) return []
  try {
    const res = await $fetch<ShopProductsResponse>(`/api/v1/${club}/shop/products`, {
      baseURL: backendApiBase(),
      query: { pageSize: 60 },
    })
    return (res?.items ?? []).map((p) => ({ loc: `/zh/shop/${p.slug}/` }))
  } catch {
    return []
  }
}

interface PlayersResponse {
  items?: Array<{ slug?: string | null, teamCode: string, birthOn?: string | null }>
}

/**
 * 3.1 球員詳情（`/zh/club/first-team/player/{slug}/`）：正規網址是 slug（id 網址會 301，不列）。
 * 只列一線隊（詳情頁只接一線隊球員，其餘會 302 回名單）、且排除出生日期已知未滿 18 歲者（未成年個資不主動送進索引，
 * 與 Person Schema 的閘門同一條規則）。公開端點單次最多 100 筆，一線隊名單遠低於此；失敗回空陣列。
 */
async function getPlayerUrls(club: string): Promise<SitemapUrlEntry[]> {
  if (!isUnitEnabledForClub('3.1', club)) return []
  const teamCode = club === 'bw' ? 'BW1' : 'D1'
  try {
    const res = await $fetch<PlayersResponse>(`/api/v1/${club}/players`, {
      baseURL: backendApiBase(),
      query: { team: teamCode, pageSize: 100 },
    })
    const now = new Date()
    const isMinor = (birthOn?: string | null) => {
      if (!birthOn) return false
      const [y, m, d] = birthOn.split('-').map(Number)
      if (!y || !m || !d) return false
      let age = now.getUTCFullYear() - y
      if (now.getUTCMonth() + 1 < m || (now.getUTCMonth() + 1 === m && now.getUTCDate() < d)) age -= 1
      return age < 18
    }
    return (res?.items ?? [])
      .filter((p) => p.slug && p.teamCode === teamCode && !isMinor(p.birthOn))
      .map((p) => ({ loc: `/zh/club/first-team/player/${p.slug}/` }))
  } catch {
    return []
  }
}

/** 不屬於 13 個單元、但是公開且該被收錄的站務頁（兩站相同）。目前只有 App 下載頁（App 規劃書 §2.3）。
 * 會員中心（含 `/member/upgrade/`）、隱私權與 Cookie 政策不在此列（GEO-02／站務法遵頁，維持既有排除）。 */
const EXTRA_PUBLIC_PATHS: readonly string[] = ['/zh/app/']

export async function getSitemapUrls(club: string): Promise<SitemapUrlEntry[]> {
  const unitUrls: SitemapUrlEntry[] = [
    ...getEnabledSiteUnits(club).map((unit) => ({ loc: unit.path })),
    ...EXTRA_PUBLIC_PATHS.map((loc) => ({ loc })),
    ...await getCharityProgramUrls(club),
    ...await getShopProductUrls(club),
    ...await getPlayerUrls(club),
  ]

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
