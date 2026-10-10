/**
 * P4 試訓場次與試訓報名後台端點（`Features/AdminTrials`），對照 apps/api/README.md「B1」節「P4 試訓場次」。
 * 錯誤格式與分頁通則見「E1a」通則：`detail` 為日常中文，`AdminApiError.message` 可直接顯示。
 */
import { apiRequest, buildQuery, downloadExport, type BilingualContentInput } from './adminCommon'

/** 試訓場次狀態（中文字面，照傳照顯示）。 */
export type TrialStatus = '開放' | '額滿' | '候補' | '已結束'
export const TRIAL_STATUS_ORDER: TrialStatus[] = ['開放', '額滿', '候補', '已結束']

// ── 場次 ────────────────────────────────────────────────────────────────────────

export interface TrialListItemDto {
  id: string
  teamId?: string | null
  teamCode?: string | null
  teamName?: string | null
  venueId?: string | null
  venueName?: string | null
  trialOn: string
  capacity?: number | null
  enrolledCount: number
  deadlineOn?: string | null
  status: string
  isSignupOpen: boolean
  syncToCalendar: boolean
  audienceZh?: string | null
  audienceEn?: string | null
  waitlistCount: number
  updatedAt: string
}

export interface TrialAudienceContent {
  audience?: string | null
}

export interface TrialDetailDto extends TrialListItemDto {
  zh: TrialAudienceContent
  en?: TrialAudienceContent | null
  createdAt: string
}

export interface ListTrialsParams {
  teamId?: string
  status?: string
  from?: string
  to?: string
}

export interface SaveTrialPayload {
  teamId?: string | null
  venueId?: string | null
  trialOn: string
  capacity?: number | null
  deadlineOn?: string | null
  /** 新增省略＝開放、更新省略＝不變。 */
  status?: TrialStatus
  content: BilingualContentInput<{ audience: string }>
}

const trialsBase = (club: string) => `/api/v1/admin/${club}/trials`

export function listTrials(club: string, params: ListTrialsParams = {}): Promise<TrialListItemDto[]> {
  return apiRequest<TrialListItemDto[]>(`${trialsBase(club)}${buildQuery({ ...params })}`)
}

export function getTrial(club: string, id: string): Promise<TrialDetailDto> {
  return apiRequest<TrialDetailDto>(`${trialsBase(club)}/${id}`)
}

export function createTrial(club: string, payload: SaveTrialPayload): Promise<TrialDetailDto> {
  return apiRequest<TrialDetailDto>(trialsBase(club), { method: 'POST', body: payload })
}

export function updateTrial(club: string, id: string, payload: SaveTrialPayload): Promise<TrialDetailDto> {
  return apiRequest<TrialDetailDto>(`${trialsBase(club)}/${id}`, { method: 'PUT', body: payload })
}

export function deleteTrial(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${trialsBase(club)}/${id}`, { method: 'DELETE' })
}

// ── 報名 ────────────────────────────────────────────────────────────────────────

export interface TrialRegistrationListItemDto {
  id: string
  registrationNo: string
  memberId?: string | null
  isMember: boolean
  applicantName: string
  phone?: string | null
  email?: string | null
  birthOn?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  note?: string | null
  status: string
  createdAt: string
}

export interface TrialRegistrationDetailDto extends TrialRegistrationListItemDto {
  /** 後端算好的「這筆儲存後是否超過名額」；缺值時畫面退回自己計算。 */
  isOverCapacity?: boolean
  trialId: string
  healthDeclaration?: string | null
  /** 訪客送出時同意隱私權政策的時間（UTC，唯讀）；後台代填或舊資料為空。 */
  privacyConsentedAt?: string | null
  /** 同意當下的隱私權政策版本（唯讀）。 */
  privacyPolicyVersion?: string | null
  updatedAt: string
}

export interface ListTrialRegistrationsParams {
  status?: string
  keyword?: string
  isMember?: boolean
}

/** 新增時 `status` 省略＝待確認；更新為整份覆寫，`status` 必填。 */
export interface SaveTrialRegistrationPayload {
  applicantName: string
  phone?: string | null
  email?: string | null
  birthOn?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  healthDeclaration?: string | null
  note?: string | null
  memberId?: string | null
  status: string
}

const regsBase = (club: string, trialId: string) => `${trialsBase(club)}/${trialId}/registrations`

export function listTrialRegistrations(
  club: string,
  trialId: string,
  params: ListTrialRegistrationsParams = {},
): Promise<TrialRegistrationListItemDto[]> {
  return apiRequest<TrialRegistrationListItemDto[]>(`${regsBase(club, trialId)}${buildQuery({ ...params })}`)
}

export function getTrialRegistration(club: string, trialId: string, regId: string): Promise<TrialRegistrationDetailDto> {
  return apiRequest<TrialRegistrationDetailDto>(`${regsBase(club, trialId)}/${regId}`)
}

export function createTrialRegistration(
  club: string,
  trialId: string,
  payload: SaveTrialRegistrationPayload,
): Promise<TrialRegistrationDetailDto> {
  return apiRequest<TrialRegistrationDetailDto>(regsBase(club, trialId), { method: 'POST', body: payload })
}

export function updateTrialRegistration(
  club: string,
  trialId: string,
  regId: string,
  payload: SaveTrialRegistrationPayload,
): Promise<TrialRegistrationDetailDto> {
  return apiRequest<TrialRegistrationDetailDto>(`${regsBase(club, trialId)}/${regId}`, { method: 'PUT', body: payload })
}

/** 候補 → 已確認（佔名額）；非候補列後端回 400。 */
export function promoteTrialRegistration(club: string, trialId: string, regId: string): Promise<TrialRegistrationDetailDto> {
  return apiRequest<TrialRegistrationDetailDto>(`${regsBase(club, trialId)}/${regId}/promote`, { method: 'POST' })
}

/** 名單 CSV（不含健康聲明）。`purpose` 必填，檔名由伺服器提供。 */
export function exportTrialRegistrations(
  club: string,
  trialId: string,
  params: { status?: string },
  purpose: string,
  trialOn?: string | null,
): Promise<void> {
  // 檔名以伺服器 Content-Disposition 為準（downloadExport 已優先採用）；退路檔名不含識別碼，只用場次日期
  return downloadExport(
    `${regsBase(club, trialId)}/export${buildQuery({ status: params.status, purpose })}`,
    `試訓報名名單${trialOn ? `-${trialOn}` : ''}.csv`,
  )
}

// ── 簽到表 ──────────────────────────────────────────────────────────────────────

export interface SignInRowDto {
  no: number
  registrationNo: string
  applicantName: string
  phone?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  status: string
}

export interface TrialSignInSheetDto {
  trialId: string
  trialOn: string
  teamName?: string | null
  venueName?: string | null
  audienceZh?: string | null
  generatedAt: string
  rows: SignInRowDto[]
}

export function getTrialSignInSheet(club: string, trialId: string): Promise<TrialSignInSheetDto> {
  return apiRequest<TrialSignInSheetDto>(`${trialsBase(club)}/${trialId}/sign-in-sheet`)
}
