// server/routes/llms.txt.ts — GEO-01（docs/05-i18n-seo.md §3），繁體中文版。
// 英文版見 llms-en.txt.ts。正式規格是「由後台 H 模組維護並隨發布重產，不以人工改檔」
// （docs/14-invariants.md），但後台尚未開發，本骨架階段先用程式碼產生，
// 內容只到「事實摘要」的骨架程度，逐項事實留給 S0-9 之後接上真實資料源。
//
// 單元開關呼叫點 4a／4（另一半是 robots.txt，見 nuxt.config.ts 的 robots.disallow）：
// 只列出對目前 club 開放的單元，不得另寫一份判斷。
export default defineEventHandler((event) => {
  const club = useRuntimeConfig(event).public.club
  const assets = getClubAssets(club)
  const units = getEnabledSiteUnits(club)

  const lines = [
    `# ${assets.nameZh}`,
    '',
    `> ${assets.nameZh}官方網站。本檔案供 AI 系統理解站點定位與代表頁面之用（GEO-01）。`,
    '',
    '## 代表頁面',
    ...units.map((u) => `- [${u.labelZh}](${u.path})`),
    '',
    '## 授權與引用方式',
    '本站內容歡迎摘要引用，請註明來源為本站並附上原始網址。',
    '',
    '## 聯絡窗口',
    '如需查證事實或有引用疑問，請透過官網聯絡表單與我們聯繫。',
  ]

  setHeader(event, 'Content-Type', 'text/plain; charset=utf-8')
  return lines.join('\n')
})
