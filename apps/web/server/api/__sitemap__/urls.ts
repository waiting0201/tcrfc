// server/api/__sitemap__/urls.ts — sitemap 網址清單的資料端點（保留作為可獨立驗證的
// 資料來源；docs/18-work-errors.md E-18 曾用 curl 直接呼叫這支驗證 club 過濾正確）。
//
// 🔴 2026-09-22 更新：這支端點原本是給 @nuxtjs/sitemap 的「零設定自動探索」慣例用的
// 動態來源，但追查發現 /sitemap.xml 恆為空的真正原因不是這支端點（它一直是對的），
// 而是 @nuxtjs/sitemap 內建的 /sitemap.xml 產生邏輯會把「路徑命中 X-Robots-Tag: noindex
// 的 route rule」的網址整批排除——本站上線前 nuxt.config.ts 對 `/**` 全站蓋一條 noindex
// 標頭（CLAUDE.md 全域規定第 5 條），所以每一筆都被排除，且這個排除沒有設定能繞過。
// 已在 nuxt.config.ts 把 `sitemap.enabled` 設為 false，改由 server/routes/sitemap.xml.ts
// 自組 XML 接手最終輸出；這支端點降級為單純的資料來源（維持既有相對路徑陣列的回應
// 格式，方便單獨 curl 核對 club 過濾結果），實際邏輯已抽到 server/utils/sitemap-urls.ts
// 共用，兩處不再各自判斷一次。詳見 docs/18-work-errors.md E-18。
export default defineEventHandler(async (event) => {
  const club = useRuntimeConfig(event).public.club
  return getSitemapUrls(club)
})
