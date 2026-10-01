// shared/utils/partners.ts — 09 合作夥伴與贊助（S2-7）／首頁贊助夥伴 Logo 牆（S1-14）共用的型別與純函式
//
// 對應 apps/api 公開端點（Features/Partners、Features/Sponsors、Features/Proposals，
// apps/api/README.md「E1a」節「公開讀取端點」表）：
//   GET /api/v1/{club}/partners?type=&home=&footer=&lang=
//   GET /api/v1/{club}/sponsors?lang=
//   GET /api/v1/{club}/sponsor-packages?lang=
//   GET /api/v1/{club}/proposals
//
// 🔴 「藍鯨與磐石分區、不得混列」（藍鯨規劃書 §2.1／docs/14 五種商業對象）：端點本身以
// `club_id` 分區（`partners.club_id`、贊助商與方案亦同，各自簽約各自建一筆），前台一律用
// 目前容器的 `config.public.club` 組網址，**不得**跨俱樂部讀取、也不得在前台合併兩隊清單。
// 本檔的分組函式只處理「同一俱樂部內」的排版，不涉及跨俱樂部。

export interface PartnerCharityProgramLink {
  slug: string
  name: string | null
}

export interface PublicPartner {
  id: string
  slug: string
  /** 夥伴類型顯示文字：標準五類或該俱樂部自訂（例如藍鯨的「指導單位」），儲存的是中文字面值，不隨語系翻譯。 */
  partnerType: string | null
  country: string | null
  startOn: string | null
  endOn: string | null
  websiteUrl: string | null
  showInFooter: boolean
  showOnHome: boolean
  sortOrder: number
  name: string | null
  content: string | null
  logoDarkUrl: string | null
  logoLightUrl: string | null
  charityPrograms: PartnerCharityProgramLink[]
}

export interface SponsorActivationImage {
  imageUrl: string
  thumbUrl: string | null
  imageWidth: number | null
  imageHeight: number | null
}

export interface SponsorActivation {
  id: string
  title: string | null
  happenedOn: string | null
  resultSummary: string | null
  images: SponsorActivationImage[]
}

export interface SponsorStory {
  slug: string
  title: string | null
  summary: string | null
}

export interface PublicSponsor {
  id: string
  slug: string
  tier: string | null
  sortOrder: number
  name: string | null
  content: string | null
  logoDarkUrl: string | null
  logoLightUrl: string | null
  stories: SponsorStory[]
  activations: SponsorActivation[]
  charityPrograms: PartnerCharityProgramLink[]
}

export interface PublicSponsorPackage {
  id: string
  slug: string
  sortOrder: number
  name: string | null
  content: string | null
  benefitList: string | null
  audience: string | null
  priceMin: number | null
  priceMax: number | null
}

export interface PublicProposal {
  id: string
  title: string
  versionNo: number
  locales: string[]
}

/** 9.1 五大標準類型（規劃書 §3.9）；`key` 同時是頁面區塊錨點 id 與 `band` 樣式輪替的依據。 */
export const PARTNER_TYPE_SECTIONS = [
  { key: 'strategic', type: '策略夥伴', en: 'Strategic Partners' },
  { key: 'international', type: '國際夥伴', en: 'International Partners' },
  { key: 'training', type: '訓練夥伴', en: 'Training Partners' },
  { key: 'education', type: '教育夥伴', en: 'Education Partners' },
  { key: 'brand', type: '品牌夥伴', en: 'Brand Partners' },
] as const

/** 9.2 三個贊助等級（規劃書 §3.9，`SponsorDto.Tier` 的中文字面值）。 */
export const SPONSOR_TIER_SECTIONS = [
  { key: 'title-sponsors', tier: '主贊助', en: 'Title Sponsors', heading: '主贊助' },
  { key: 'official-sponsors', tier: '官方贊助', en: 'Official Sponsors', heading: '官方贊助' },
  { key: 'supporting-sponsors', tier: '支持夥伴', en: 'Supporting Sponsors', heading: '支持贊助' },
] as const

export interface GroupedSection<T> {
  /** 錨點 id；自訂類型用 `custom-N`。 */
  key: string
  /** 區塊標題（中文）。 */
  title: string
  /** 英文副標；自訂類型沒有。 */
  en: string | null
  items: T[]
}

/**
 * 依「已知類型清單」分組：清單內的類型即使沒有項目也保留一個空區塊（頁面據此顯示既有的
 * 「尚未公開」佔位），清單外的類型（俱樂部自訂）接在後面、只在有項目時出現，順序依首次出現。
 * 同一組內保留 API 回傳的排序（`sortOrder`，後端已排好，這裡不重排）。
 */
export function groupByKnownType<T>(
  items: readonly T[],
  getType: (item: T) => string | null,
  known: ReadonlyArray<{ key: string, /** 後端儲存的類型／等級字面值（比對用）。 */ match: string, /** 頁面區塊標題。 */ title: string, en: string }>,
): GroupedSection<T>[] {
  const sections: GroupedSection<T>[] = known.map((k) => ({ key: k.key, title: k.title, en: k.en, items: [] }))
  const byTitle = new Map(known.map((k, i) => [k.match, sections[i]!]))
  let custom = 0
  for (const item of items) {
    const type = (getType(item) ?? '').trim()
    let section = byTitle.get(type)
    if (!section) {
      // 沒填類型的項目歸入「其他」，避免整筆消失（後端 partnerType 必填，理論上不會發生）。
      const title = type || '其他'
      section = byTitle.get(title)
      if (!section) {
        custom += 1
        section = { key: `custom-${custom}`, title, en: null, items: [] }
        byTitle.set(title, section)
        sections.push(section)
      }
    }
    section.items.push(item)
  }
  return sections
}

/** Logo 牆格子底色一律是淺色（`.sponsor-tile{background:var(--paper)}`），所以淺底版優先，沒有才回退深底版。 */
export function pickLogoUrl(item: { logoLightUrl: string | null, logoDarkUrl: string | null }): string | null {
  return item.logoLightUrl ?? item.logoDarkUrl ?? null
}

/** 只放行 http／https 網址（後端 `websiteUrl` 已驗證，這裡是輸出前的最後一道，不信任資料）。 */
export function safeExternalUrl(url: string | null | undefined): string | null {
  if (!url) return null
  return /^https?:\/\//i.test(url) ? url : null
}

/** 合作期間顯示：`2025-01-01 – 2026-12-31`；只有起日「2025-01-01 起」；只有迄日「至 2026-12-31」；都沒有回傳 null。 */
export function formatPartnerPeriod(startOn: string | null, endOn: string | null): string | null {
  if (startOn && endOn) return `${startOn.replaceAll('-', '/')} – ${endOn.replaceAll('-', '/')}`
  if (startOn) return `${startOn.replaceAll('-', '/')} 起`
  if (endOn) return `至 ${endOn.replaceAll('-', '/')}`
  return null
}

/** 贊助方案價格區間文字；兩欄皆 null（後台未公開價格）回傳 null，頁面不得顯示任何價格。 */
export function formatPackagePrice(min: number | null, max: number | null): string | null {
  const fmt = (n: number) => `NT$${n.toLocaleString('en-US')}`
  if (min != null && max != null) return min === max ? fmt(min) : `${fmt(min)} – ${fmt(max)}`
  if (min != null) return `${fmt(min)} 起`
  if (max != null) return `最高 ${fmt(max)}`
  return null
}

/** 權益清單是「純文字，一行一項」。 */
export function splitBenefitList(text: string | null): string[] {
  return (text ?? '').split(/\r?\n/).map((l) => l.trim()).filter(Boolean)
}

/** 提案下載連結：API 回的是 `/api/v1/{club}/proposals/downloads/{token}`，轉成同源代理路徑；形狀不符一律拒絕。 */
export function proposalDownloadHref(downloadPath: string | null | undefined, club: string): string | null {
  if (!downloadPath) return null
  const m = /^\/api\/v1\/([a-z][a-z0-9-]*)\/proposals\/downloads\/([A-Za-z0-9_.~-]+)$/.exec(downloadPath)
  if (!m || m[1] !== club) return null
  return `/api/backend/${club}/proposals/downloads/${m[2]}`
}

/** 依目前語系挑要下載的提案：優先「有該語系檔案」的，其次版本號最大的。沒有提案回傳 null。 */
export function pickProposal(proposals: readonly PublicProposal[], locale: string): PublicProposal | null {
  if (proposals.length === 0) return null
  const sorted = proposals.slice().sort((a, b) => {
    const la = a.locales.includes(locale) ? 1 : 0
    const lb = b.locales.includes(locale) ? 1 : 0
    return lb - la || b.versionNo - a.versionNo
  })
  return sorted[0] ?? null
}
