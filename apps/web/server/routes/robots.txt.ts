// server/routes/robots.txt.ts — 自組 /robots.txt（S1-12 驗收退回後補做，2026-09-25；
// S1-12b 擴充 GEO-02 AI 爬蟲授權，2026-09-25）。
//
// 取代原本 nuxt.config.ts 用 @nuxtjs/robots 的 `disallow: ['/']` 固定輸出——那個模組是
// 建置期固定產生一份規則，沒有辦法依 runtime 才知道的環境旗標切換輸出內容，跟
// docs/18-work-errors.md E-18（sitemap.xml 那次）完全同一種衝突。本檔改用 Nitro 自組路由，
// 資料來源是兩支公開端點：GET /api/v1/{club}/seo/settings（robotsCustomRules 欄位）與
// GET /api/v1/{club}/seo/crawler-settings（GEO-02，S1-12b 新增），不另開一份判斷。
//
// 🔴🔴🔴 環境旗標是白名單而不是黑名單：只有 NUXT_PUBLIC_SITE_ENV 精確等於 'production' 時
// 才輸出「允許索引＋後台自訂規則＋GEO-02 AI 爬蟲清單」這一支；任何其他值（'prelaunch'、
// 未設定、拼錯、未來新增的過渡值……）一律落在封鎖那一側，回傳 `Disallow: /`。這是任務指示
// 明文要求的方向——「漏設變數時要落在封鎖那一側」——用白名單而不是「!== 'prelaunch'」這種
// 黑名單寫法，才能保證新增或忘記設定的環境值不會意外被解讀成「可以索引」。
//
// ⚠️ 這支路由目前**只處理 robots.txt 檔案內容本身**，不影響、也不依賴既有的
// `X-Robots-Tag: noindex` 標頭（nuxt.config.ts 的 routeRules，全站上線前的第 1 層防護，
// CLAUDE.md 全域規定第 5 條）——那個標頭目前是無條件套用，不看 NUXT_PUBLIC_SITE_ENV，
// 這是任務指示明文要求本輪不要去動的部分，見 apps/web/README.md「S1-12」段的完整說明。
// 換句話說：即使這裡在 production 模式下輸出「允許索引」，回應本身仍然會帶著強制 noindex
// 的標頭——兩者要在真正上線時一起由 docs/17-deployment.md §10.4 的「上線前三層防護」機制
// 完整切換，不是這支檔案的職責範圍。
//
// 🔴 S1-12b（GEO-02）設計：排除路徑（會員中心、七類表單、訂單查詢、/m/<token>、未成年學員
// 照片路徑……）套用到 `User-agent: *`（**全站**、對所有爬蟲一視同仁，不是只告訴 AI 爬蟲
// 不要看）——這是 docs/14-invariants.md 講的「個資防線」的精神：排除的理由是個資與肖像同意，
// 不是「只想省 AI 的爬取額度」，因此沒有理由只對命名的 AI 代理生效、放一般爬蟲進去。命名的
// AI 使用者代理（GPTBot／ClaudeBot／…）在此之上**明列允許**（`GEO-02`「robots.txt 明列允許的
// AI 使用者代理」的字面要求），套用同一份排除清單；後台若把某個代理設為拒絕，則該代理拿到
// 專屬的 `Disallow: /`（robots.txt 規格本身「較具體的 User-agent 區塊覆蓋 `*`」的既有語意）。
// 排除清單本身（強制 ∪ 後台自加）已經由 apps/api 的 SeoRepository.GetCrawlerSettingsAsync
// 合併好，這裡直接使用，不在前台重算一次強制清單（避免兩處各自維護、日久漂移）。
interface CrawlerAgent {
  userAgent: string
  allowed: boolean
}

interface PublicCrawlerSettings {
  userAgents: CrawlerAgent[]
  excludePaths: string[]
}

function buildAgentBlock(userAgent: string, excludePaths: string[]): string[] {
  const block = [`User-agent: ${userAgent}`, 'Allow: /']
  for (const path of excludePaths) {
    block.push(`Disallow: ${path}`)
  }
  return block
}

export default defineEventHandler(async (event) => {
  const siteEnv = useRuntimeConfig(event).public.siteEnv
  const club = useRuntimeConfig(event).public.club

  setHeader(event, 'Content-Type', 'text/plain; charset=utf-8')

  if (siteEnv !== 'production') {
    return 'User-agent: *\nDisallow: /\n'
  }

  const siteUrl = (getSiteConfig(event).url ?? '').replace(/\/$/, '')

  let excludePaths: string[] = []
  let userAgents: CrawlerAgent[] = []
  try {
    const crawlerSettings = await $fetch<PublicCrawlerSettings>(`/api/v1/${club}/seo/crawler-settings`, {
      baseURL: backendApiBase(),
    })
    excludePaths = crawlerSettings.excludePaths ?? []
    userAgents = crawlerSettings.userAgents ?? []
  }
  catch {
    // apps/api 暫時連不上：不輸出任何排除路徑或 AI 代理區塊，只保留最基本的 `User-agent: *`
    // 允許索引規則——跟既有 seo.robots_custom_rules 的防禦性寫法一致（fail-open 到「最基本可用」
    // 而不是整支路由 500，見上方 catch 同一種既定模式）。⚠️ 這代表 apps/api 斷線時 GEO-02 的
    // 排除路徑會暫時消失，是這個 try/catch 既有設計取捨的延伸，不是本輪新增的風險。
  }

  const lines = ['User-agent: *', 'Allow: /']
  for (const path of excludePaths) {
    lines.push(`Disallow: ${path}`)
  }

  try {
    const settings = await $fetch<{ robotsCustomRules?: string | null }>(`/api/v1/${club}/seo/settings`, {
      baseURL: backendApiBase(),
    })
    if (settings.robotsCustomRules && settings.robotsCustomRules.trim().length > 0) {
      lines.push('', settings.robotsCustomRules.trim())
    }
  }
  catch {
    // apps/api 暫時連不上：不附加自訂規則，仍然輸出基本的允許索引規則
    // （跟既有 sitemap-urls.ts／llms.txt.ts 同一種防禦性寫法）。
  }

  for (const agent of userAgents) {
    lines.push('')
    lines.push(...(agent.allowed ? buildAgentBlock(agent.userAgent, excludePaths) : [`User-agent: ${agent.userAgent}`, 'Disallow: /']))
  }

  if (siteUrl) {
    lines.push('', `Sitemap: ${siteUrl}/sitemap.xml`)
  }

  return `${lines.join('\n')}\n`
})
