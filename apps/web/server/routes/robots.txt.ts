// server/routes/robots.txt.ts — 自組 /robots.txt（S1-12 驗收退回後補做，2026-09-25）。
//
// 取代原本 nuxt.config.ts 用 @nuxtjs/robots 的 `disallow: ['/']` 固定輸出——那個模組是
// 建置期固定產生一份規則，沒有辦法依 runtime 才知道的環境旗標切換輸出內容，跟
// docs/18-work-errors.md E-18（sitemap.xml 那次）完全同一種衝突。本檔改用 Nitro 自組路由，
// 資料來源仍是同一支公開端點（GET /api/v1/{club}/seo/settings 的 robotsCustomRules 欄位），
// 不另開一份判斷。
//
// 🔴🔴🔴 環境旗標是白名單而不是黑名單：只有 NUXT_PUBLIC_SITE_ENV 精確等於 'production' 時
// 才輸出「允許索引＋後台自訂規則」這一支；任何其他值（'prelaunch'、未設定、拼錯、未來新增的
// 過渡值……）一律落在封鎖那一側，回傳 `Disallow: /`。這是任務指示明文要求的方向——
// 「漏設變數時要落在封鎖那一側」——用白名單而不是「!== 'prelaunch'」這種黑名單寫法，
// 才能保證新增或忘記設定的環境值不會意外被解讀成「可以索引」。
//
// ⚠️ 這支路由目前**只處理 robots.txt 檔案內容本身**，不影響、也不依賴既有的
// `X-Robots-Tag: noindex` 標頭（nuxt.config.ts 的 routeRules，全站上線前的第 1 層防護，
// CLAUDE.md 全域規定第 5 條）——那個標頭目前是無條件套用，不看 NUXT_PUBLIC_SITE_ENV，
// 這是任務指示明文要求本輪不要去動的部分，見 apps/web/README.md「S1-12」段的完整說明。
// 換句話說：即使這裡在 production 模式下輸出「允許索引」，回應本身仍然會帶著強制 noindex
// 的標頭——兩者要在真正上線時一起由 docs/17-deployment.md §10.4 的「上線前三層防護」機制
// 完整切換，不是這支檔案的職責範圍。
//
// production 模式下的內容目前只到「基本允許索引＋後台自訂規則」，**不含 GEO-02 逐一 AI
// 爬蟲的允許清單與排除路徑**——那屬於 S1-12b（依 STATUS.md 排程，本輪不做），等那張票做完
// 之後再擴充這支路由的 production 分支，不需要改動這裡的環境旗標判斷邏輯。
export default defineEventHandler(async (event) => {
  const siteEnv = useRuntimeConfig(event).public.siteEnv
  const club = useRuntimeConfig(event).public.club

  setHeader(event, 'Content-Type', 'text/plain; charset=utf-8')

  if (siteEnv !== 'production') {
    return 'User-agent: *\nDisallow: /\n'
  }

  const siteUrl = (getSiteConfig(event).url ?? '').replace(/\/$/, '')
  const lines = ['User-agent: *', 'Allow: /']

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

  if (siteUrl) {
    lines.push('', `Sitemap: ${siteUrl}/sitemap.xml`)
  }

  return `${lines.join('\n')}\n`
})
