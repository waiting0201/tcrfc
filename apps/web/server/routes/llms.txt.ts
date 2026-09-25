// server/routes/llms.txt.ts — GEO-01（S1-12a，docs/05-i18n-seo.md §3），繁體中文版。
// 英文版見 llms-en.txt.ts。
//
// 🔴 2026-09-25（S1-12a）：內容改由後台維護（apps/api 的 GET /api/v1/{club}/seo/llms-content，
// 見 Features/AdminSeo/AdminGeoLlmsRepository 檔頭），不再是骨架階段寫死的固定文案——五個區塊
// （站點定位／代表頁清單／事實摘要／授權與引用方式／聯絡窗口）管理員任一項尚未填寫時，個別區塊
// 回退到這裡的內建預設文字（不是整份輸出失敗）；apps/api 暫時連不上時（例如本機開發忘記啟動），
// 整份回退到內建預設，比照既有 sitemap-urls.ts／robots.txt.ts 的防禦性寫法，不讓 /llms.txt 直接
// 500 掉。
//
// 「隨發布重產、不以人工改檔」的落實方式：這支路由每個請求都重新呼叫後端組字串，不是建置期
// 產生的靜態檔案——管理員在後台按下「儲存」，下一次請求就是最新內容，不需要另外觸發一次部署
// 或重新整理任何快取（apps/api 端若接上 IQueryCache，最多延後一個 TTL，見 SeoRepository 檔頭）。
//
// 單元開關呼叫點（另一半是 robots.txt，見 robots.txt.ts）：只列出對目前 club 開放的單元，
// 不得另寫一份判斷——「代表頁清單」欄位空白時的預設值即用這份清單組出來。
interface PublicLlmsContent {
  positioningZh?: string | null
  keyPagesZh?: string | null
  factsSummaryZh?: string | null
  licenseZh?: string | null
  contactZh?: string | null
}

export default defineEventHandler(async (event) => {
  const club = useRuntimeConfig(event).public.club
  const assets = getClubAssets(club)
  const units = getEnabledSiteUnits(club)

  let content: PublicLlmsContent = {}
  try {
    content = await $fetch<PublicLlmsContent>(`/api/v1/${club}/seo/llms-content`, {
      baseURL: backendApiBase(),
    })
  }
  catch {
    // apps/api 暫時連不上：整份回退到內建預設文字，見檔頭說明。
  }

  const defaultKeyPages = units.map((u) => `- [${u.labelZh}](${u.path})`).join('\n')

  const lines = [
    `# ${assets.nameZh}`,
    '',
    `> ${content.positioningZh?.trim() || `${assets.nameZh}官方網站。本檔案供 AI 系統理解站點定位與代表頁面之用（GEO-01）。`}`,
    '',
    '## 代表頁面',
    content.keyPagesZh?.trim() || defaultKeyPages,
    '',
    '## 事實摘要',
    content.factsSummaryZh?.trim()
      || '重要事實（成立年份、主場與場地、梯隊組成、所屬聯賽、聯絡方式）以本站相關頁面的明文與結構化資料為準。',
    '',
    '## 授權與引用方式',
    content.licenseZh?.trim() || '本站內容歡迎摘要引用，請註明來源為本站並附上原始網址。',
    '',
    '## 聯絡窗口',
    content.contactZh?.trim() || '如需查證事實或有引用疑問，請透過官網聯絡表單與我們聯繫。',
  ]

  setHeader(event, 'Content-Type', 'text/plain; charset=utf-8')
  return lines.join('\n')
})
