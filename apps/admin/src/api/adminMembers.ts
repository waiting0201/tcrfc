/**
 * K1 會員名單（apps/api/README.md「B1」節「K1 會員名單」）。
 * 個資遮罩是預設：名單一律遮罩；完整值只有詳情頁 `?reveal=true`（需要解除遮罩權限）。
 * LINE 識別碼、密碼、QR 憑證字串永遠不會出現在回應裡。
 */
import { apiRequest, buildQuery, downloadExport, type PagedResult } from './adminCommon'

export type MemberTier = 'registered' | 'fan_club'
export type MembershipStatusCode = 'pending' | 'active' | 'expired' | 'cancelled'
export type MemberAccountStatus = 'active' | 'unverified' | 'suspended' | 'deleted'
export type MemberSignupSource = 'web' | 'line' | 'admin' | 'app'
export type JerseyStatusCode = 'pending' | 'shipped' | 'received'
export type MemberLocale = 'zh-Hant' | 'en'

export const MEMBER_TIER_OPTIONS: { value: MemberTier; label: string }[] = [
  { value: 'registered', label: '一般會員' },
  { value: 'fan_club', label: '球迷會員' },
]
export const MEMBERSHIP_STATUS_OPTIONS: { value: MembershipStatusCode; label: string }[] = [
  { value: 'pending', label: '待確認' },
  { value: 'active', label: '有效' },
  { value: 'expired', label: '已到期' },
  { value: 'cancelled', label: '已取消' },
]
export const MEMBER_ACCOUNT_STATUS_OPTIONS: { value: MemberAccountStatus; label: string }[] = [
  { value: 'active', label: '正常' },
  { value: 'unverified', label: '尚未驗證信箱' },
  { value: 'suspended', label: '已停用' },
  { value: 'deleted', label: '已刪除或已合併' },
]
export const MEMBER_SIGNUP_SOURCE_OPTIONS: { value: MemberSignupSource; label: string }[] = [
  { value: 'web', label: '官網註冊' },
  { value: 'line', label: 'LINE 註冊' },
  { value: 'admin', label: '現場入會' },
  { value: 'app', label: 'App 註冊' },
]
export const JERSEY_STATUS_OPTIONS: { value: JerseyStatusCode; label: string }[] = [
  { value: 'pending', label: '待處理' },
  { value: 'shipped', label: '已寄出' },
  { value: 'received', label: '已領取' },
]
export const MEMBER_LOCALE_OPTIONS: { value: MemberLocale; label: string }[] = [
  { value: 'zh-Hant', label: '繁體中文' },
  { value: 'en', label: '英文' },
]

export interface MembershipSummaryDto {
  membershipId: string
  clubId: string
  clubCode: string
  clubName: string
  seasonId: string
  seasonCode: string
  tier: MemberTier
  tierLabel: string
  status: MembershipStatusCode
  effectiveStatus: MembershipStatusCode
  effectiveStatusLabel: string
  startOn: string | null
  endOn: string | null
  daysToExpire: number | null
  planId: string | null
  planName: string | null
}

export interface MemberListItemDto {
  id: string
  memberNo: string
  name: string | null
  email: string | null
  phone: string | null
  signupSource: MemberSignupSource
  signupSourceLabel: string
  lineBound: boolean
  status: string
  displayStatus: MemberAccountStatus
  displayStatusLabel: string
  locale: MemberLocale
  localeLabel: string
  createdAt: string
  lastLoginAt: string | null
  jerseyStatus: JerseyStatusCode | null
  jerseyStatusLabel: string | null
  memberships: MembershipSummaryDto[]
  isMasked: boolean
}

export interface MemberPaymentDto {
  id: string
  planName: string | null
  method: string
  methodLabel: string
  amount: number
  paidOn: string
  collectingClubCode: string | null
  beneficiaryClubCode: string | null
  note: string | null
  handledByName: string | null
  activatedStartOn: string | null
  activatedEndOn: string | null
  createdAt: string
}

export interface MemberCardDto {
  id: string
  membershipId: string
  holderName: string | null
  status: string
  statusLabel: string
  issuedAt: string
  revokedAt: string | null
  reissueCount: number
}

export interface MemberMembershipDetailDto {
  membership: MembershipSummaryDto
  lastAdjustReason: string | null
  lastAdjustedAt: string | null
  payments: MemberPaymentDto[]
  cards: MemberCardDto[]
}

export interface MemberJerseyIssueDto {
  id: string
  clubCode: string
  recipientName: string | null
  size: string | null
  deliveryMethod: string
  deliveryMethodLabel: string
  status: string
  statusLabel: string
  shippedOn: string | null
  receivedOn: string | null
}

export interface MemberDetailDto extends Omit<MemberListItemDto, 'memberships'> {
  birthOn: string | null
  emailVerifiedAt: string | null
  internalNote: string | null
  mergedIntoMemberNo: string | null
  canReveal: boolean
  memberships: MemberMembershipDetailDto[]
  jerseyIssues: MemberJerseyIssueDto[]
}

export interface MemberListFilter {
  crossClub?: boolean
  keyword?: string
  tier?: string
  membershipStatus?: string
  status?: string
  signupSource?: string
  lineBound?: boolean
  registeredFrom?: string
  registeredTo?: string
  seasonId?: string
  expiringWithinDays?: number
  jerseyStatus?: string
  locale?: string
}

function filterQuery(filter: MemberListFilter) {
  return {
    crossClub: filter.crossClub ? true : undefined,
    keyword: filter.keyword,
    tier: filter.tier,
    membershipStatus: filter.membershipStatus,
    status: filter.status,
    signupSource: filter.signupSource,
    lineBound: filter.lineBound,
    registeredFrom: filter.registeredFrom,
    registeredTo: filter.registeredTo,
    seasonId: filter.seasonId,
    expiringWithinDays: filter.expiringWithinDays,
    jerseyStatus: filter.jerseyStatus,
    locale: filter.locale,
  }
}

const base = (club: string) => `/api/v1/admin/${club}/members`

export function listMembers(club: string, filter: MemberListFilter, page: number, pageSize: number): Promise<PagedResult<MemberListItemDto>> {
  return apiRequest<PagedResult<MemberListItemDto>>(`${base(club)}${buildQuery({ ...filterQuery(filter), page, pageSize })}`)
}

/** `reveal=true` 需要解除遮罩權限（沒有 → 403）。 */
export function getMember(club: string, id: string, reveal = false): Promise<MemberDetailDto> {
  return apiRequest<MemberDetailDto>(`${base(club)}/${id}${reveal ? '?reveal=true' : ''}`)
}

export interface CreateMemberPayload {
  name: string
  email: string
  phone?: string | null
  birthOn?: string | null
  locale?: MemberLocale
  internalNote?: string | null
}

export function createMember(club: string, payload: CreateMemberPayload): Promise<MemberDetailDto> {
  return apiRequest<MemberDetailDto>(base(club), { method: 'POST', body: payload })
}

export function updateMemberStatus(club: string, id: string, status: 'active' | 'suspended', reason?: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}/status`, { method: 'PUT', body: { status, reason: reason || undefined } })
}

export function updateMemberNote(club: string, id: string, internalNote: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}/note`, { method: 'PUT', body: { internalNote } })
}

export function reissueMemberCard(club: string, id: string, cardId: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}/cards/${cardId}/reissue`, { method: 'POST' })
}

export interface DuplicateMemberDto {
  id: string
  memberNo: string
  name: string | null
  email: string | null
  phone: string | null
  createdAt: string
  membershipCount: number
}

export interface DuplicateGroupDto {
  matchKind: 'phone' | 'email'
  matchKindLabel: string
  members: DuplicateMemberDto[]
}

export function listMemberDuplicates(club: string, crossClub: boolean): Promise<DuplicateGroupDto[]> {
  return apiRequest<DuplicateGroupDto[]>(`${base(club)}/duplicates${buildQuery({ crossClub: crossClub ? true : undefined })}`)
}

export interface MergeMembersResultDto {
  targetMemberId: string
  targetMemberNo: string
  sourceMemberNo: string
  movedMemberships: number
  movedRegistrations: number
  movedOrders: number
  movedJerseys: number
}

export function mergeMembers(club: string, targetMemberId: string, sourceMemberId: string): Promise<MergeMembersResultDto> {
  return apiRequest<MergeMembersResultDto>(`${base(club)}/merge`, { method: 'POST', body: { targetMemberId, sourceMemberId } })
}

/** CSV 匯出（每份會籍一列）：範圍與名單完全相同，`purpose` 必填。 */
export async function exportMembers(club: string, filter: MemberListFilter, purpose: string): Promise<void> {
  await downloadExport(`${base(club)}/export${buildQuery({ ...filterQuery(filter), purpose })}`, '會員名單.csv')
}
