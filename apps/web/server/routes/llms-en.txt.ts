// server/routes/llms-en.txt.ts — GEO-01 英文版，與 llms.txt.ts（繁中版）內容對應但不逐句翻譯，
// 見 docs/05-i18n-seo.md §3「llms.txt：繁中英文各一份」。
//
// 🔴 2026-09-25（S1-12a）：同繁中版，內容改由後台維護的英文欄位（*En）供應，見 llms.txt.ts
// 檔頭「隨發布重產」的完整說明。管理員尚未填寫英文版時，個別區塊依序回退：**英文欄位 →
// 中文欄位（總比空白好，前台既有慣例是「未翻譯 fallback 繁中並標示」，見 docs/01 G-01）→
// 這裡的內建英文預設文字**。
interface PublicLlmsContent {
  positioningZh?: string | null
  positioningEn?: string | null
  keyPagesZh?: string | null
  keyPagesEn?: string | null
  factsSummaryZh?: string | null
  factsSummaryEn?: string | null
  licenseZh?: string | null
  licenseEn?: string | null
  contactZh?: string | null
  contactEn?: string | null
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

  const siteName = assets.code === 'bw' ? 'Taichung Blue Whale' : 'Taichung Rock FC'
  const defaultKeyPages = units.map((u) => `- ${u.labelZh}: ${u.path.replace(/^\/zh\//, '/en/')}`).join('\n')

  const positioning = content.positioningEn?.trim() || content.positioningZh?.trim()
    || `This file helps AI systems understand ${siteName}'s purpose and key pages (GEO-01).`
  const keyPages = content.keyPagesEn?.trim() || content.keyPagesZh?.trim() || defaultKeyPages
  const factsSummary = content.factsSummaryEn?.trim() || content.factsSummaryZh?.trim()
    || 'Key facts (founding year, home venue, squads, league, contact) follow the plain text and structured data on this site\'s own pages.'
  const license = content.licenseEn?.trim() || content.licenseZh?.trim()
    || 'Content may be summarized with attribution linking back to the source page.'
  const contact = content.contactEn?.trim() || content.contactZh?.trim()
    || 'Please use the contact form on the official site for fact-checking or citation questions.'

  const lines = [
    `# ${siteName}`,
    '',
    `> ${positioning}`,
    '',
    '## Key pages',
    keyPages,
    '',
    '## Facts summary',
    factsSummary,
    '',
    '## License and citation',
    license,
    '',
    '## Contact',
    contact,
  ]

  setHeader(event, 'Content-Type', 'text/plain; charset=utf-8')
  return lines.join('\n')
})
