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

interface PublicSiteFactsEn {
  foundedDisplay?: string | null
  foundingTitle?: string | null
  league?: { name?: string | null } | null
  venues?: Array<{ name?: string | null, isHomeGround?: boolean }> | null
  squadStructureSummary?: string | null
}

const CJK_RE = /[\u3000-\u303f\u3400-\u9fff\uff00-\uffef]/

/** 英文版只收不含中日文字元的值；後端 `?lang=en` 缺英文時會回退中文，那種值一律丟掉。 */
function enOnly(value: string | null | undefined): string | null {
  const v = value?.trim()
  return v && !CJK_RE.test(v) ? v : null
}

/**
 * 開發端預設英文段落（僅在後台 `geo.llms_*` 的 en／zh 欄位皆空時使用，見檔頭 fallback 說明）。
 * 只用規劃書已定案的事實：品牌主張、站台事實（先讀 `site-facts?lang=en`，缺值退回
 * `shared/utils/site-facts.ts` 的英文快照）與單元清單；不編造任何數字、獎項或聯絡方式。
 * 兩個俱樂部都提供（藍鯨 B-5 已於 2026-10-05 定案：全名 `BW_FULL_NAME_EN`，事實取自藍鯨規劃書英文版 §3，
 * 不編造任何數字、獎項或聯絡方式；藍鯨沒有實體地址與電話，聯絡段維持通用句）。
 */
async function buildDefaultsEn(club: string): Promise<{ positioning: string, factsSummary: string } | null> {
  const snapshot = SITE_FACTS[club === 'bw' ? 'bw' : 'tcrfc']
  let api: PublicSiteFactsEn | null = null
  try {
    api = await $fetch<PublicSiteFactsEn>(`/api/v1/${club}/site-facts`, {
      baseURL: backendApiBase(),
      query: { lang: 'en' },
    })
  }
  catch {
    // 打不到後端：只用靜態英文快照。
  }
  const founded = enOnly(api?.foundedDisplay) ?? snapshot.foundedDisplayEn
  const title = enOnly(api?.foundingTitle) ?? snapshot.foundingTitleEn
  const league = enOnly(api?.league?.name) ?? snapshot.league.nameEn
  // 主場可能不只一個（藍鯨：太原足球場、豐原體育場），全部列出；API 沒有英文值時退回快照。
  const apiHomes = (api?.venues ?? []).filter((v) => v.isHomeGround).map((v) => enOnly(v.name)).filter((v): v is string => !!v)
  const snapHomes = snapshot.venues.filter((v) => v.isHomeGround).map((v) => v.nameEn).filter((v): v is string => !!v)
  const venue = (apiHomes.length > 0 ? apiHomes : snapHomes).join(' and ') || null
  const squads = enOnly(api?.squadStructureSummary) ?? snapshot.squadStructureEn

  const positioning = club === 'bw'
    ? `${BW_FULL_NAME_EN} is a women's football club in Taichung, Taiwan, with a First Team and Youth teams (U15 and U12 girls' teams). `
      + 'This file helps AI systems understand the club and its key pages (GEO-01).'
    : 'Taichung Rock FC is a football club in Taichung, Taiwan, with an Academy and a First Team. '
      + 'Its brand promise is LOCAL ROOTS. GLOBAL PATHWAYS. This file helps AI systems understand the club and its key pages (GEO-01).'
  const facts = [
    founded && `- ${founded}${title ? ` (${title})` : ''}.`,
    league && `- League: ${league}.`,
    venue && `- Home venue: ${venue}.`,
    squads && `- Squads: ${squads}.`,
    '- Details and the latest information are on the pages listed above; where this summary and a page differ, the page is authoritative.',
  ].filter(Boolean).join('\n')
  return { positioning, factsSummary: facts }
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

  // 藍鯨英文名 B-5 已於 2026-10-05 定案：`llms-en.txt` 是 AI 爬蟲直接讀的檔，標題與定位用全名
  // `BW_FULL_NAME_EN`（舊站變體 Bluewhale／…Women's Football Team／大小寫不一一律不用，check-bw-en-name 擋）。
  const siteName = assets.code === 'bw' ? BW_FULL_NAME_EN : 'Taichung Rock FC'
  // 單元名稱：`getUnitLabelEn(unit, club)`（units-en.ts，主站與藍鯨各有英文對照；藍鯨 04 叫 Youth）。
  const defaultKeyPages = units.map((u) => `- ${getUnitLabelEn(u, club)}: ${u.path.replace(/^\/zh\//, '/en/')}`).join('\n')
  const defaults = await buildDefaultsEn(club)

  // 🔴 回退順序：後台英文欄位 → 開發端預設英文（兩個俱樂部皆有）→ 後台中文欄位 → 內建英文通用句。
  // 原本「英文空就退中文」會讓爬蟲讀到的英文版混中文；後台沒填英文時寧可用有事實依據的預設英文。
  const positioning = content.positioningEn?.trim() || defaults?.positioning || content.positioningZh?.trim()
    || `This file helps AI systems understand ${siteName}'s purpose and key pages (GEO-01).`
  const keyPages = content.keyPagesEn?.trim() || (defaults ? defaultKeyPages : content.keyPagesZh?.trim() || defaultKeyPages)
  const factsSummary = content.factsSummaryEn?.trim() || defaults?.factsSummary || content.factsSummaryZh?.trim()
    || 'Key facts (founding year, home venue, squads, league, contact) follow the plain text and structured data on this site\'s own pages.'
  const license = content.licenseEn?.trim() || (defaults ? '' : content.licenseZh?.trim())
    || 'Content may be summarized with attribution linking back to the source page.'
  const contact = content.contactEn?.trim() || (defaults ? '' : content.contactZh?.trim())
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
