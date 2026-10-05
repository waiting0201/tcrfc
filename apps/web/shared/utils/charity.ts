// shared/utils/charity.ts — 11 慈善與社會影響（S2-9）的公開 API 型別與純函式
//
// 對應 apps/api Features/CharityImpact（apps/api/README.md「E1a」節「公開讀取端點」表，後台 B5）：
//   GET /api/v1/{club}/charity/cta｜programs｜programs/{slug}｜records｜records/years｜impact
//
// 🔴 本站**不承接捐款**（主站規劃書 §3.11「球迷捐款：導向慈善捐款平台」）：只做介紹與導流。捐款網址與
// 導流文案一律來自 `charity/cta`（後台 B5 設定），前台不得寫死網址、不得放金額選項／捐款表單／匯款帳戶。
// 🔴 收受者是「台灣足球策略發展協會」，不是俱樂部——後端在設定捐款網址時強制導流文案含該名稱；前台
// 凡是自己寫的固定說明文字也必須點明（見 CHARITY_RECIPIENT）。

/** 捐款收受者名稱（慈善規劃書 v1.4／主站規劃書 §3.11）；前台固定文案點明收受者時使用的唯一字面值。 */
export const CHARITY_RECIPIENT = '台灣足球策略發展協會'

/** 英文版固定文案點明收受者時使用（docs/06 §1.1 英文用詞對照表，待客戶確認的直譯）。 */
export const CHARITY_RECIPIENT_EN = 'Taiwan Football Strategic Development Association'

export interface CharityCta {
  donationUrl: string | null
  donationCta: string | null
  fanCta: string | null
  corporateCta: string | null
  corporateUrl: string | null
}

export interface PublicCharityOrg {
  slug: string
  name: string | null
  intro: string | null
  logoUrl: string | null
  websiteUrl: string | null
}

export interface PublicCharityImage {
  imageUrl: string
  thumbUrl: string | null
}

export interface CharityProgramListItem {
  id: string
  slug: string
  name: string | null
  targetAudience: string | null
  startOn: string | null
  endOn: string | null
  /** `ongoing`（進行中）／`completed`（已完成）。 */
  progress: 'ongoing' | 'completed' | string
  isPinned: boolean
  coverUrl: string | null
  charityName: string | null
}

export interface CharityLinkedItem {
  slug: string
  name: string | null
  logoDarkUrl: string | null
  logoLightUrl: string | null
}

export interface CharityArticleLink {
  slug: string
  title: string | null
}

export interface CharityProgramDetail {
  id: string
  slug: string
  name: string | null
  targetAudience: string | null
  startOn: string | null
  endOn: string | null
  progress: string
  coverUrl: string | null
  content: string | null
  donationContent: string | null
  charity: PublicCharityOrg | null
  images: PublicCharityImage[]
  partners: CharityLinkedItem[]
  sponsors: CharityLinkedItem[]
  articles: CharityArticleLink[]
}

export interface ImpactRecord {
  id: string
  happenedOn: string | null
  charityName: string | null
  charityLogoUrl: string | null
  donationContent: string | null
  location: string | null
  briefDescription: string | null
  imageUrl: string | null
  imageWidth: number | null
  imageHeight: number | null
  images: PublicCharityImage[]
  programSlug: string | null
  programName: string | null
}

export interface ImpactMetric {
  name: string | null
  unit: string | null
  value: number | null
  programSlug: string | null
}

export interface ImpactSummary {
  metrics: ImpactMetric[]
  charityCount: number
  donationItemCount: number
  regions: string[]
  charities: PublicCharityOrg[]
}

export function progressLabel(progress: string, en = false): string {
  if (en) return progress === 'completed' ? 'Completed' : 'Ongoing'
  return progress === 'completed' ? '已完成' : '進行中'
}

/** `2026-01-12` → `2026/01/12`；沒有日期回傳 null。 */
export function slashDate(iso: string | null | undefined): string | null {
  return iso ? iso.slice(0, 10).replaceAll('-', '/') : null
}

/** 計畫期間文字：起訖都有「2026/01/12 – 2026/06/30」；只有起日「2026/01/12 起」；都沒有 null。 */
export function programPeriod(startOn: string | null, endOn: string | null, en = false): string | null {
  const s = slashDate(startOn)
  const e = slashDate(endOn)
  if (s && e) return `${s} – ${e}`
  if (s) return en ? `From ${s}` : `${s} 起`
  if (e) return en ? `Until ${e}` : `至 ${e}`
  return null
}

/** 事蹟所屬年份（依 `happenedOn`）；沒有日期的事蹟歸「未標日期」，回傳 null。 */
export function recordYear(r: { happenedOn: string | null }): number | null {
  return r.happenedOn ? Number(r.happenedOn.slice(0, 4)) : null
}
