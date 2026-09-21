// server/routes/llms-en.txt.ts — GEO-01 英文版，與 llms.txt.ts（繁中版）內容對應但不逐句翻譯，
// 見 docs/05-i18n-seo.md §3「llms.txt：繁中英文各一份」。骨架階段同樣只到事實摘要骨架。
export default defineEventHandler((event) => {
  const club = useRuntimeConfig(event).public.club
  const assets = getClubAssets(club)
  const units = getEnabledSiteUnits(club)

  const lines = [
    `# ${assets.code === 'bw' ? 'Taichung Blue Whale' : 'Taichung Rock FC'}`,
    '',
    'This file helps AI systems understand this site\'s purpose and key pages (GEO-01).',
    '',
    '## Key pages',
    ...units.map((u) => `- ${u.labelZh}: ${u.path.replace(/^\/zh\//, '/en/')}`),
    '',
    '## License and citation',
    'Content may be summarized with attribution linking back to the source page.',
    '',
    '## Contact',
    'Please use the contact form on the official site for fact-checking or citation questions.',
  ]

  setHeader(event, 'Content-Type', 'text/plain; charset=utf-8')
  return lines.join('\n')
})
