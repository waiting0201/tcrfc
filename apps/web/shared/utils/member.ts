// shared/utils/member.ts — 會員中心（S2-11）、08 文化（S3-2）共用的型別與純函式
//
// 型別對應 apps/api README「E 批：會員前台（S2-11）與文化公開端點（S3-2）」的契約
// （Features/MemberAuth／MemberCenter／MembershipPayments／MembershipPublic／Comics／FanEvents 的 DTO）。
// 全部是純資料型別與純函式，沒有任何瀏覽器或 Nitro 依賴，app／server 兩側都可 auto-import。

// ── 會員帳號與工作階段 ────────────────────────────────────────────────────────
export interface MemberBrief {
  memberNo: string
  name: string
  emailVerified: boolean
  hasPassword: boolean
  lineBound: boolean
}

/** 瀏覽器拿到的工作階段：**沒有更新權杖**（它只存在 Nuxt 伺服器代理設定的 HttpOnly Cookie）。 */
export interface MemberBrowserSession {
  accessToken: string
  accessTokenExpiresAt: string
  member: MemberBrief
}

export interface MemberProfile {
  memberNo: string
  name: string
  email: string
  phone: string | null
  birthOn: string | null
  locale: 'zh' | 'en' | null
  emailVerified: boolean
  hasPassword: boolean
  lineBound: boolean
  signupSource: string
  signupSourceLabel: string
  createdAt: string
}

export interface MemberRegistered {
  memberNo: string
  emailVerificationRequired: boolean
  emailSent: boolean
}

export type LineCallbackResult =
  | { status: 'logged_in', session: MemberBrowserSession }
  | { status: 'bound' }
  | { status: 'signup_required', ticket: string, displayName: string | null, suggestedEmail: string | null }

// ── 會籍／會員卡／球衣／訂單 ──────────────────────────────────────────────────
export interface MemberClubBrand {
  code: string
  name: string
  logoLightUrl: string | null
  logoDarkUrl: string | null
  brandColor: string | null
  brandSecondaryColor: string | null
}

export interface MemberCard {
  id: string
  membershipId: string
  club: MemberClubBrand
  memberNo: string
  holderName: string
  tier: string
  tierLabel: string
  validUntil: string | null
  status: 'valid' | 'expired' | 'revoked'
  statusLabel: string
  isValid: boolean
  token: string
  reissueCount: number
  issuedAt: string | null
}

export interface MyMembership {
  id: string
  club: MemberClubBrand
  seasonCode: string
  tier: string
  tierLabel: string
  status: string
  statusLabel: string
  startOn: string | null
  endOn: string | null
  planCode: string | null
  planName: string | null
  cardQuota: number
  jerseyQuota: number
  isCurrentSeason: boolean
  renewalDue: boolean
  pendingOrder: { orderNo: string, status: string, statusLabel: string } | null
  cards: MemberCard[]
}

export interface MyMemberships {
  memberships: MyMembership[]
  joinableClubs: Array<{ code: string, name: string }>
}

export interface MemberJerseyItem {
  id: string
  clubCode: string
  membershipId: string
  recipientName: string
  phone: string | null
  size: string | null
  deliveryMethod: 'ship' | 'pickup' | null
  deliveryMethodLabel: string | null
  address: string | null
  status: string
  statusLabel: string
  shippedOn: string | null
  receivedOn: string | null
  editable: boolean
}

export interface MemberJerseyGroup {
  membershipId: string
  clubCode: string
  clubName: string
  seasonCode: string
  quota: number
  used: number
  canRegister: boolean
  items: MemberJerseyItem[]
}

export interface MembershipOrder {
  orderNo: string
  clubCode: string
  planCode: string
  planName: string | null
  seasonCode: string
  amount: number
  status: string
  statusLabel: string
  createdAt: string
  canPayOnline: boolean
  canCancel: boolean
}

// ── 公開讀取：方案、權益、特約店家 ────────────────────────────────────────────
export interface MembershipPlan {
  code: string
  name: string
  benefitNote: string | null
  fee: number
  cardQuota: number
  jerseyQuota: number
  midSeasonRule: string | null
  seasonCode: string
  startsOn: string | null
  endsOn: string | null
}

export interface MembershipBenefitItem {
  name: string
  description: string | null
  freeValue: string | null
  paidValue: string | null
}

export interface MembershipBenefitGroup {
  group: string
  groupLabel: string
  items: MembershipBenefitItem[]
}

export interface MembershipBenefits {
  planCode: string | null
  planName: string | null
  groups: MembershipBenefitGroup[]
}

export interface PartnerStore {
  slug: string
  name: string
  category: string | null
  region: string | null
  address: string | null
  lat: number | null
  lng: number | null
  phone: string | null
  businessHours: string | null
  offerContent: string | null
  applicableTier: 'all' | 'fan_club'
  applicableTierLabel: string | null
  mapUrl: string | null
  websiteUrl: string | null
  imageUrl: string | null
  isShared: boolean
}

export interface PartnerStoreFilters {
  categories: string[]
  regions: string[]
}

// ── 08 文化 ──────────────────────────────────────────────────────────────────
export interface ComicAbout { title: string | null, body: string | null }

export interface ComicCharacter {
  id: string
  name: string
  description: string | null
  imageUrl: string | null
  imageThumbUrl: string | null
  playerId: string | null
}

export interface ComicEpisode {
  episodeNo: number
  title: string
  coverUrl: string | null
  coverThumbUrl: string | null
  publishedOn: string | null
  isLatest: boolean
  pageCount: number
}

export interface ComicPage {
  pageNo: number
  imageUrl: string
  imageThumbUrl: string | null
  width: number | null
  height: number | null
}

export interface ComicEpisodeDetail {
  episodeNo: number
  title: string
  coverUrl: string | null
  publishedOn: string | null
  isLatest: boolean
  pages: ComicPage[]
  previousEpisodeNo: number | null
  nextEpisodeNo: number | null
}

export interface FanEvent {
  slug: string
  name: string
  location: string | null
  startsAt: string | null
  endsAt: string | null
  registrationDeadlineAt: string | null
  capacity: number | null
  spotsLeft: number | null
  isPaidMembersOnly: boolean
  isRegistrationOpen: boolean
  isFull: boolean
  phase: 'upcoming' | 'past'
  coverUrl: string | null
  coverThumbUrl: string | null
}

export interface FanEventDetail {
  event: FanEvent
  description: string | null
  venueName: string | null
  images: Array<{ imageUrl: string | null, imageThumbUrl: string | null, width: number | null, height: number | null }>
  articles: Array<{ slug: string, title: string }>
  myRegistration: { status: string, statusLabel: string } | null
}

export interface CardVerification {
  nameInitial: string
  memberNo: string
  tier: string
  tierLabel: string
  status: 'valid' | 'expired'
  statusLabel: string
}

// ── 錯誤正規化 ────────────────────────────────────────────────────────────────
/** 會員端點的 ProblemDetails 摘要（`code` 是機器可讀、`detail` 是日常中文可直接顯示）。 */
export interface MemberApiError {
  status: number
  code: string
  detail: string
  lockedUntil: string | null
}

/** 從 `$fetch` 拋出的例外取出 ProblemDetails。代理一律原樣轉回後端的狀態碼與本文，所以 `err.data` 就是 ProblemDetails。 */
export function toMemberApiError(err: unknown, fallback = '操作失敗，請稍後再試一次。'): MemberApiError {
  const e = err as { status?: number, statusCode?: number, data?: unknown } | null
  const status = e?.status ?? e?.statusCode ?? 0
  const data = e?.data
  let code = ''
  let detail = ''
  let lockedUntil: string | null = null
  if (data && typeof data === 'object') {
    const d = data as Record<string, unknown>
    if (typeof d.code === 'string') code = d.code
    if (typeof d.detail === 'string') detail = d.detail
    else if (typeof d.message === 'string' && status > 0 && status < 500) detail = d.message
    if (typeof d.lockedUntil === 'string') lockedUntil = d.lockedUntil
  }
  if (!detail) {
    if (status === 429) detail = '操作太頻繁，請稍後再試。'
    else if (status === 0 || status >= 500) detail = '服務暫時無法使用，請稍後再試。'
    else detail = fallback
  }
  return { status, code, detail, lockedUntil }
}

// ── 時間與日期 ────────────────────────────────────────────────────────────────
/** 後端時間戳是 UTC 帶 Z；一律以台灣時間顯示（D 批通則）。 */
export function formatTaipeiDateTime(iso: string | null | undefined, locale: 'zh' | 'en' = 'zh'): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  return new Intl.DateTimeFormat(locale === 'zh' ? 'zh-TW' : 'en-GB', {
    timeZone: 'Asia/Taipei',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(d)
}

/** `yyyy-MM-dd` 是台灣當地日期（不是時間戳），不得丟給 `new Date()`（會被當 UTC 午夜、在負時區少一天）；直接改成斜線分隔顯示。 */
export function formatPlainDate(date: string | null | undefined): string {
  if (!date) return ''
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(date)
  return m ? `${m[1]}/${m[2]}/${m[3]}` : date
}

// ── 導覽與安全 ────────────────────────────────────────────────────────────────
/** 登入後要回去的站內路徑：只接受 `/zh/…`、`/en/…`（擋 open redirect：`//evil`、`https://…`、`/\evil`）。 */
export function safeNextPath(raw: unknown): string | null {
  if (typeof raw !== 'string') return null
  if (!/^\/(zh|en)\//.test(raw)) return null
  if (raw.includes('//') || raw.includes('\\')) return null
  return raw
}

/** 密碼規則（與後端一致：8–128 字元、含英文字母與數字；是否為常見弱密碼由後端判斷）。 */
export function passwordProblem(pw: string): string | null {
  if (pw.length < 8) return '密碼至少需要 8 個字元。'
  if (pw.length > 128) return '密碼不可超過 128 個字元。'
  if (!/[A-Za-z]/.test(pw) || !/\d/.test(pw)) return '密碼需同時包含英文字母與數字。'
  return null
}

export const MEMBER_PASSWORD_HINT = '8–128 個字元，需同時包含英文字母與數字。'
