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
  // 主站規劃書 v3.20：標誌與品牌色由前台靜態資產（`getClubAssets(code)`）定義，API 不再提供。
  // 舊版後端若仍回這四個欄位，前台一律忽略，所以型別不收。
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
  /** 封面圖片替代文字（API 依請求語系回傳，無封面為 null） */
  coverAlt?: string | null
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

// ── 註冊年齡閘門與監護人同意（主站規劃書「會員資料安全要求」：未滿 18 歲須經監護人同意方得註冊）──
export type GuardianRelationship = 'parent' | 'legal_guardian'
export const GUARDIAN_RELATIONSHIP_LABEL: Record<GuardianRelationship, string> = { parent: '父母', legal_guardian: '法定監護人' }
export const GUARDIAN_RELATIONSHIP_LABEL_EN: Record<GuardianRelationship, string> = { parent: 'Parent', legal_guardian: 'Legal guardian' }

/** 🔴 同意文案待法務定稿（B-9）。送出的版本號固定標成 `pending-legal`，後台 K1 看得到「文案未定稿」；
 * 法務定稿後換成正式版本號（並改掉 `MemberAgeGuardian.vue` 的佔位說明），不得在此之前自擬法律條文。 */
export const GUARDIAN_CONSENT_VERSION_PENDING = 'pending-legal'
export const ADULT_AGE = 18

export interface GuardianConsentBody {
  consented: boolean
  guardianName: string
  relationship: GuardianRelationship
  consentTextVersion: string
}

/** 台北當地日期（`YYYY-MM-DD`）。與後端 `TaiwanClock.ToDate` 同一時區（UTC+8，無日光節約），不依賴使用者裝置時區。 */
export function taipeiToday(now: Date = new Date()): string {
  return new Date(now.getTime() + 8 * 3600_000).toISOString().slice(0, 10)
}

/** 依台北當地日期算足歲；生日格式不合或晚於今天回 `null`（交給後端的範圍檢查擋）。 */
export function taipeiAge(birthOn: string, now: Date = new Date()): number | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(birthOn)
  if (!m) return null
  const [by, bm, bd] = [Number(m[1]), Number(m[2]), Number(m[3])]
  const [ty, tm, td] = taipeiToday(now).split('-').map(Number) as [number, number, number]
  let age = ty - by
  if (tm < bm || (tm === bm && td < bd)) age -= 1
  return age < 0 ? null : age
}

export function isMinorBirth(birthOn: string, now?: Date): boolean {
  const age = taipeiAge(birthOn, now)
  return age !== null && age < ADULT_AGE
}

/**
 * 從 `$fetch` 拋出的例外取出 ProblemDetails。代理一律原樣轉回後端的狀態碼與本文，所以 `err.data` 就是 ProblemDetails。
 * `en`＝主站英文版（`useLocale().isEn`）：優先用 ProblemDetails 的 `messageEn`；沒有就用呼叫端給的（英文）`fallback`，
 * 不把繁中 `detail` 顯示在英文介面上。預設 `en = false`，zh 行為與過去完全相同。
 */
export function toMemberApiError(err: unknown, fallback = '操作失敗，請稍後再試一次。', en = false): MemberApiError {
  const e = err as { status?: number, statusCode?: number, data?: unknown } | null
  const status = e?.status ?? e?.statusCode ?? 0
  const data = e?.data
  let code = ''
  let detail = ''
  let lockedUntil: string | null = null
  if (data && typeof data === 'object') {
    const d = data as Record<string, unknown>
    if (typeof d.code === 'string') code = d.code
    if (en) {
      if (typeof d.messageEn === 'string' && d.messageEn.trim()) detail = d.messageEn
    }
    else if (typeof d.detail === 'string') detail = d.detail
    else if (typeof d.message === 'string' && status > 0 && status < 500) detail = d.message
    if (typeof d.lockedUntil === 'string') lockedUntil = d.lockedUntil
  }
  if (!detail) {
    if (status === 429) detail = en ? 'Too many attempts. Please try again later.' : '操作太頻繁，請稍後再試。'
    else if (status === 0 || status >= 500) detail = en ? 'The service is temporarily unavailable. Please try again later.' : '服務暫時無法使用，請稍後再試。'
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
  // F3（2026-10-03）：不直接用 `.format()` 的字串。Node（SSR）與瀏覽器的 ICU 版本不同，日期與時間之間的
  // 分隔字元不一樣（Node 輸出 U+2009 細空格、瀏覽器輸出一般空格），肉眼完全相同、字元不同，
  // Vue 水合時報「Hydration text mismatch」。改用 formatToParts 取出各欄位、自己以固定字元組字。
  // `hourCycle: 'h23'` 避免部分 ICU 把午夜顯示成 24:xx。
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat(locale === 'zh' ? 'zh-TW' : 'en-GB', {
      timeZone: 'Asia/Taipei',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    }).formatToParts(d).map((p) => [p.type, p.value]),
  ) as Record<string, string>
  return locale === 'zh'
    ? `${parts.year}/${parts.month}/${parts.day} ${parts.hour}:${parts.minute}`
    : `${parts.day}/${parts.month}/${parts.year}, ${parts.hour}:${parts.minute}`
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
export function passwordProblem(pw: string, en = false): string | null {
  if (pw.length < 8) return en ? 'Your password must be at least 8 characters.' : '密碼至少需要 8 個字元。'
  if (pw.length > 128) return en ? 'Your password must not exceed 128 characters.' : '密碼不可超過 128 個字元。'
  if (!/[A-Za-z]/.test(pw) || !/\d/.test(pw)) return en ? 'Your password must include both letters and numbers.' : '密碼需同時包含英文字母與數字。'
  return null
}

export const MEMBER_PASSWORD_HINT = '8–128 個字元，需同時包含英文字母與數字。'
export const MEMBER_PASSWORD_HINT_EN = '8–128 characters, including both letters and numbers.'
