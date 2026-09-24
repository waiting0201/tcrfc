import type { Bilingual } from './common'

/**
 * C1–C3 球隊／球員／教練與團隊成員的畫面型別。逐欄位對照
 * `apps/api` 的 `Features/AdminTeams`／`AdminPlayers`／`AdminStaff`（見 apps/api/README.md「S1-7」
 * 「S1-7a」）。中文標籤對照 docs/06-conventions.md §1 與主站規劃書 §4.3。
 */

// ── C1 球隊 ──────────────────────────────────────────────────────────────────────

export type TeamType = 'first_team' | 'academy'
export type TeamGender = 'men' | 'women' | 'mixed'

export const TEAM_TYPE_LABEL: Record<TeamType, string> = {
  first_team: '一線隊',
  academy: '學院梯隊',
}

export const TEAM_GENDER_LABEL: Record<TeamGender, string> = {
  men: '男子',
  women: '女子',
  mixed: '男女混合',
}

export interface Team {
  id: string
  code: string
  type: TeamType
  gender: TeamGender
  ageBand: string
  teamColor: string
  heroKey: string | null
  sortOrder: number
  name: Bilingual
  intro: Bilingual
  updatedAt: string
}

// ── C2 球員 ──────────────────────────────────────────────────────────────────────

export type PlayerStatus = 'active' | 'departed' | 'loan' | 'overseas'

export const PLAYER_STATUS_LABEL: Record<PlayerStatus, string> = {
  active: '現役',
  departed: '離隊',
  loan: '外借',
  overseas: '海外發展',
}

export const PLAYER_STATUS_ORDER: PlayerStatus[] = ['active', 'departed', 'loan', 'overseas']

/**
 * 肖像同意狀態（S1-7a，藍鯨規劃書行 193／314、docs/14-invariants.md「肖像同意」段）：
 * 三態，**新建預設 `not_consented`（fail-closed）**，公開端點只有 `consented`／
 * `consented_by_guardian` 才會輸出照片。畫面上一律用中文顯示，不顯示英文列舉原值。
 */
export type PortraitConsentStatus = 'not_consented' | 'consented' | 'consented_by_guardian'

export const PORTRAIT_CONSENT_STATUS_LABEL: Record<PortraitConsentStatus, string> = {
  not_consented: '未同意',
  consented: '本人同意',
  consented_by_guardian: '監護人代簽',
}

export const PORTRAIT_CONSENT_STATUS_ORDER: PortraitConsentStatus[] = [
  'not_consented',
  'consented',
  'consented_by_guardian',
]

export interface Player {
  id: string
  teamId: string
  teamCode: string
  shirtNo: number | null
  position: string
  birthOn: string | null
  heightCm: number | null
  weightKg: number | null
  nationality: string
  preferredFoot: string
  joinedOn: string | null
  status: PlayerStatus
  photoKey: string | null
  portraitConsentStatus: PortraitConsentStatus
  name: Bilingual
  bio: Bilingual
  updatedAt: string
}

// ── C3 教練與團隊成員 ─────────────────────────────────────────────────────────────

/** 值域是資料庫直接存的中文字串（`db/club-schema.sql` 的 CHECK 約束本身就是中文），
 * 不是英文代碼轉譯——「顧問」職稱歸入「管理層」是 S0-3c 已拍板的執行層決定，屬於使用者
 * 填值時的選擇，不是系統另外的列舉值。 */
export const STAFF_GROUP_OPTIONS = ['管理層', '行政', '醫療', '後勤'] as const
export type StaffGroup = (typeof STAFF_GROUP_OPTIONS)[number]

export interface StaffTeamAssignment {
  teamId: string
  teamCode: string
  roleCode: string
}

export interface Staff {
  id: string
  isShared: boolean
  staffGroup: string
  licence: string
  photoKey: string | null
  portraitConsentStatus: PortraitConsentStatus
  name: Bilingual
  title: Bilingual
  bio: Bilingual
  teams: StaffTeamAssignment[]
  updatedAt: string
}
