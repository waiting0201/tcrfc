/**
 * K2 會籍與方案後台端點，對照 apps/api/README.md「B1」節「K2 會籍與方案」。
 * 會籍清單的姓名一律是後端遮罩後的值，前端不做任何還原。
 */
import {
  apiRequest,
  buildQuery,
  downloadExport,
  putOrder,
  type BilingualContentInput,
  type PagedResult,
} from './adminCommon'

const club$ = (club: string) => `/api/v1/admin/${club}`

// ── 方案 ──────────────────────────────────────────────

export type PlanStatus = 'published' | 'draft'

export interface MembershipPlanListItemDto {
  id: string
  seasonId: string
  seasonCode: string
  code: string
  fee: number
  cardQuota: number
  jerseyQuota: number
  startsOn?: string | null
  endsOn?: string | null
  sortOrder: number
  status: PlanStatus
  statusLabel: string
  nameZh?: string | null
  nameEn?: string | null
  membershipCount: number
  updatedAt: string
}

export interface PlanLocaleContent {
  name: string
  benefitNote?: string | null
}

export interface MembershipPlanDetailDto extends MembershipPlanListItemDto {
  midSeasonRule?: string | null
  midSeasonRuleEn?: string | null
  zh: PlanLocaleContent
  en?: PlanLocaleContent | null
  createdAt: string
}

export interface SavePlanPayload {
  seasonId: string
  code: string
  fee: number
  cardQuota: number
  jerseyQuota: number
  midSeasonRule?: string | null
  /** 選填（≤255）；空白＝清除英文。PUT 整份取代。 */
  midSeasonRuleEn?: string | null
  startsOn?: string | null
  endsOn?: string | null
  sortOrder: number
  status: PlanStatus
  content: BilingualContentInput<PlanLocaleContent>
}

const plans = (club: string) => `${club$(club)}/membership-plans`

/**
 * 會籍畫面專用的球季下拉（`member.membership.view`，C1 補）：只含目前俱樂部的球季，新→舊。
 * 客服／行政沒有球隊管理的檢視權限，不可再借用 `GET …/seasons`（會 403）。
 */
export interface MembershipSeasonDto {
  id: string
  code: string
  startOn: string
  endOn: string
}

export function listMembershipSeasons(club: string): Promise<MembershipSeasonDto[]> {
  return apiRequest<MembershipSeasonDto[]>(`${club$(club)}/membership-seasons`)
}

export function listMembershipPlans(club: string, params: { seasonId?: string; status?: string } = {}): Promise<MembershipPlanListItemDto[]> {
  return apiRequest<MembershipPlanListItemDto[]>(`${plans(club)}${buildQuery(params)}`)
}

export function getMembershipPlan(club: string, id: string): Promise<MembershipPlanDetailDto> {
  return apiRequest<MembershipPlanDetailDto>(`${plans(club)}/${id}`)
}

export function createMembershipPlan(club: string, payload: SavePlanPayload): Promise<MembershipPlanDetailDto> {
  return apiRequest<MembershipPlanDetailDto>(plans(club), { method: 'POST', body: payload })
}

export function updateMembershipPlan(club: string, id: string, payload: SavePlanPayload): Promise<MembershipPlanDetailDto> {
  return apiRequest<MembershipPlanDetailDto>(`${plans(club)}/${id}`, { method: 'PUT', body: payload })
}

export function deleteMembershipPlan(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${plans(club)}/${id}`, { method: 'DELETE' })
}

export function reorderMembershipPlans(club: string, ids: string[]): Promise<void> {
  return putOrder(`${plans(club)}/order`, ids)
}

// ── 會籍 ──────────────────────────────────────────────

export type MembershipTier = 'registered' | 'fan_club'
export type MembershipStatus = 'pending' | 'active' | 'expired' | 'cancelled'

export interface MembershipListItemDto {
  membershipId: string
  memberId: string
  memberNo?: string | null
  /** 後端已遮罩的姓名（例如 王○明），照顯示。 */
  memberName?: string | null
  tier: MembershipTier
  tierLabel?: string | null
  status: MembershipStatus
  statusLabel?: string | null
  effectiveStatus: MembershipStatus
  effectiveStatusLabel?: string | null
  seasonId: string
  seasonCode: string
  startOn?: string | null
  endOn?: string | null
  /** 正數＝幾天後到期，負數＝已過期幾天；沒有到期日為 null。 */
  daysToExpire?: number | null
  planId?: string | null
  planName?: string | null
  cardCount?: number
  paidTotal?: number
  updatedAt?: string
}

export interface MembershipPaymentDto {
  id: string
  planName?: string | null
  method: string
  methodLabel?: string | null
  amount: number
  paidOn: string
  collectingClubCode?: string | null
  beneficiaryClubCode?: string | null
  note?: string | null
  handledByName?: string | null
  activatedStartOn?: string | null
  activatedEndOn?: string | null
  createdAt?: string
}

export interface MembershipCardDto {
  id: string
  membershipId: string
  holderName?: string | null
  status: string
  statusLabel?: string | null
  issuedAt?: string | null
  revokedAt?: string | null
  reissueCount?: number
}

export interface MembershipDetailDto {
  membership: MembershipListItemDto
  lastAdjustReason?: string | null
  lastAdjustedAt?: string | null
  payments: MembershipPaymentDto[]
  cards: MembershipCardDto[]
  cardQuota: number
  jerseyQuota: number
}

export interface MembershipListParams {
  memberId?: string
  keyword?: string
  tier?: string
  status?: string
  seasonId?: string
  planId?: string
  expiringWithinDays?: number
  page?: number
  pageSize?: number
}

const memberships = (club: string) => `${club$(club)}/memberships`

export function listMemberships(club: string, params: MembershipListParams = {}): Promise<PagedResult<MembershipListItemDto>> {
  return apiRequest<PagedResult<MembershipListItemDto>>(`${memberships(club)}${buildQuery({ ...params })}`)
}

export function getMembership(club: string, id: string): Promise<MembershipDetailDto> {
  return apiRequest<MembershipDetailDto>(`${memberships(club)}/${id}`)
}

export interface ActivateMembershipPayload {
  memberId: string
  planId: string
  beneficiaryClubId?: string
  paymentMethod: 'linepay' | 'onsite'
  amount: number
  paidOn: string
  note?: string | null
  startOn?: string | null
  endOn?: string | null
}

export function activateMembership(club: string, payload: ActivateMembershipPayload): Promise<MembershipDetailDto> {
  return apiRequest<MembershipDetailDto>(`${memberships(club)}/activate`, { method: 'POST', body: payload })
}

export function createFreeMembership(club: string, payload: { memberId: string; seasonId: string }): Promise<MembershipDetailDto> {
  return apiRequest<MembershipDetailDto>(memberships(club), { method: 'POST', body: payload })
}

export interface AdjustMembershipPayload {
  tier?: MembershipTier
  status?: MembershipStatus
  startOn?: string
  endOn?: string
  reason: string
}

export function adjustMembership(club: string, id: string, payload: AdjustMembershipPayload): Promise<MembershipDetailDto> {
  return apiRequest<MembershipDetailDto>(`${memberships(club)}/${id}/adjust`, { method: 'PUT', body: payload })
}

export function addMembershipCard(club: string, id: string, holderName: string): Promise<MembershipDetailDto> {
  return apiRequest<MembershipDetailDto>(`${memberships(club)}/${id}/cards`, { method: 'POST', body: { holderName } })
}

export function revokeMembershipCard(club: string, id: string, cardId: string): Promise<MembershipDetailDto> {
  return apiRequest<MembershipDetailDto>(`${memberships(club)}/${id}/cards/${cardId}/revoke`, { method: 'POST' })
}

export interface ExpireBatchResultDto {
  asOf: string
  count: number
  dryRun: boolean
}

export function expireBatch(club: string, body: { asOf?: string; seasonId?: string; dryRun: boolean }): Promise<ExpireBatchResultDto> {
  return apiRequest<ExpireBatchResultDto>(`${memberships(club)}/expire-batch`, { method: 'POST', body })
}

export type RenewalExportKind = 'expiring' | 'expired'

/** 續會名單 CSV（含個資，必填用途）。 */
export function downloadRenewalExport(
  club: string,
  params: { kind: RenewalExportKind; days?: number; seasonId?: string; purpose: string },
): Promise<void> {
  const query = buildQuery({
    kind: params.kind,
    days: params.kind === 'expiring' ? params.days : undefined,
    seasonId: params.seasonId,
    purpose: params.purpose,
  })
  return downloadExport(`${memberships(club)}/renewal-export${query}`, '續會名單.csv')
}

// ── 待確認申請（網頁會員送出的升級／續會申請） ──────────

export type ApplicationStatus = 'created' | 'pending_payment' | 'paid' | 'activated' | 'expired' | 'activation_failed' | 'cancelled' | 'refunded'

export interface MembershipApplicationDto {
  orderNo: string
  memberId: string
  memberNo?: string | null
  /** 姓名／Email／電話依「完整個資」權限由後端遮罩，前端不還原。 */
  memberName?: string | null
  memberEmail?: string | null
  memberPhone?: string | null
  planId: string
  planCode?: string | null
  planName?: string | null
  seasonId: string
  seasonCode?: string | null
  /** 伺服器依方案算出的應收金額。 */
  amount: number
  status: ApplicationStatus
  statusLabel?: string | null
  expiresAt?: string | null
  activatedAt?: string | null
  createdAt: string
  updatedAt?: string | null
}

/** 從「待確認申請」開通時帶入開通視窗的會員、方案與應收金額；手動開通不帶。 */
export interface ActivatePrefill {
  member: MemberSearchItemDto
  planId: string
  amount: number
  /** 申請編號，只用來在視窗上提示是在處理哪一筆申請。 */
  orderNo?: string
}

export function listMembershipApplications(
  club: string,
  params: { status?: ApplicationStatus; keyword?: string; page?: number; pageSize?: number } = {},
): Promise<PagedResult<MembershipApplicationDto>> {
  return apiRequest<PagedResult<MembershipApplicationDto>>(`${club$(club)}/membership-applications${buildQuery({ ...params })}`)
}

// ── 付款紀錄 ──────────────────────────────────────────

export interface MembershipPaymentRowDto {
  payment: MembershipPaymentDto
  membershipId: string
  memberId: string
  memberNo?: string | null
  seasonCode?: string | null
}

export async function listMembershipPayments(
  club: string,
  params: { membershipId?: string; from?: string; to?: string; page?: number; pageSize?: number } = {},
): Promise<PagedResult<MembershipPaymentRowDto>> {
  const res = await apiRequest<PagedResult<MembershipPaymentRowDto> | MembershipPaymentRowDto[]>(
    `${club$(club)}/membership-payments${buildQuery({ ...params })}`,
  )
  if (Array.isArray(res)) {
    return { items: res, page: 1, pageSize: res.length, totalCount: res.length, totalPages: 1 }
  }
  return res
}

// ── 會員編號規則 ──────────────────────────────────────

export interface MemberSettingsDto {
  memberNoPrefix: string
  memberNoDigits: number
  nextMemberNoPreview?: string | null
}

export function getMemberSettings(club: string): Promise<MemberSettingsDto> {
  return apiRequest<MemberSettingsDto>(`${club$(club)}/member-settings`)
}

export function updateMemberSettings(club: string, payload: { memberNoPrefix: string; memberNoDigits: number }): Promise<MemberSettingsDto | void> {
  return apiRequest<MemberSettingsDto | void>(`${club$(club)}/member-settings`, { method: 'PUT', body: payload })
}

// ── 選會員（K1 名單端點，結果為遮罩） ────────────────────

export interface MemberSearchItemDto {
  id: string
  memberNo: string
  name: string
  email?: string | null
  phone?: string | null
}

/** 精簡版會員搜尋：只用來挑會員，回傳值一律遮罩。沒有解除遮罩權限時，關鍵字只比對會員編號。 */
export async function searchMembers(club: string, keyword: string): Promise<MemberSearchItemDto[]> {
  // includeNoMembership=true：本俱樂部的會籍持有人，加上「任何俱樂部都還沒有會籍」的人（現場建立、尚未開通），
  // 不會洩漏他隊會員。
  const res = await apiRequest<PagedResult<MemberSearchItemDto>>(
    `${club$(club)}/members${buildQuery({ keyword, pageSize: 10, includeNoMembership: true })}`,
  )
  return res.items
}
