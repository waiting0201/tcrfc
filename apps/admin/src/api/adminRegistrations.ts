/**
 * `apps/api` P3「報名管理」後台端點（`Features/AdminRegistrations`），對照 apps/api/README.md「S1-9」。
 * 只服務課程報名（`sessionId` 非空），不含 P4 試訓（`trialId`，留給 `S2-4`）。
 */
import { apiRequest } from './http'
import { buildQuery, downloadExport, postBatch, type BatchResultDto } from './adminCommon'
import type { SignInRowDto } from './adminTrials'

export interface AdminRegistrationListItemDto {
  id: string
  registrationNo: string
  sessionId?: string | null
  programNameZh?: string | null
  trialId?: string | null
  memberId?: string | null
  isMember: boolean
  applicantName: string
  phone?: string | null
  email?: string | null
  status: string
  createdAt: string
}

/**
 * 🔴 健康聲明（`healthDeclaration`）依後端現況原樣顯示與可編輯（整份覆寫語意，見
 * `apps/api` `UpdateAdminRegistrationRequest` 檔頭）——**不新增蒐集欄位、不新增同意書上傳**，
 * 依任務指示不擴大蒐集範圍。CSV 匯出（`downloadAdminRegistrationsCsv`）由後端刻意排除這欄
 * （資料最小化，見 apps/api/README.md「S1-9」「名額控管」上方段落），前端不需要另外處理。
 */
export interface AdminRegistrationDetailDto {
  id: string
  /** 後端算好的「這筆所屬梯次是否已超過名額」；缺值時不顯示警示。 */
  isOverCapacity?: boolean
  registrationNo: string
  sessionId?: string | null
  trialId?: string | null
  memberId?: string | null
  applicantName: string
  phone?: string | null
  email?: string | null
  birthOn?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  healthDeclaration?: string | null
  note?: string | null
  /** 訪客送出時同意隱私權政策的時間（UTC，唯讀）；後台代填或舊資料為空。 */
  privacyConsentedAt?: string | null
  /** 同意當下的隱私權政策版本（唯讀）。 */
  privacyPolicyVersion?: string | null
  status: string
  createdAt: string
  updatedAt: string
}

/** 清單與匯出共用的篩選（P3 進階）。`dateFrom`／`dateTo` 是報名建立日（含當天）。 */
export interface ListAdminRegistrationsParams {
  sessionId?: string
  status?: string
  programId?: string
  keyword?: string
  isMember?: boolean
  dateFrom?: string
  dateTo?: string
}

/** 後台代填報名（電話／現場報名）。P4 不在本次範圍，故不接受 `trialId`。 */
export interface CreateRegistrationPayload {
  sessionId: string
  memberId?: string | null
  applicantName: string
  phone?: string | null
  email?: string | null
  birthOn?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  healthDeclaration?: string | null
  note?: string | null
  status?: string | null
}

/** 報名處理：確認／取消／轉梯次（換 `sessionId`）／加入候補／備註／學員資料整份覆寫。 */
export interface UpdateRegistrationPayload {
  sessionId: string
  memberId?: string | null
  applicantName: string
  phone?: string | null
  email?: string | null
  birthOn?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  healthDeclaration?: string | null
  note?: string | null
  status: string
}

export function listAdminRegistrations(
  club: string,
  params: ListAdminRegistrationsParams = {},
): Promise<AdminRegistrationListItemDto[]> {
  return apiRequest<AdminRegistrationListItemDto[]>(`/api/v1/admin/${club}/registrations${buildQuery({ ...params })}`)
}

export function getAdminRegistration(club: string, id: string): Promise<AdminRegistrationDetailDto> {
  return apiRequest<AdminRegistrationDetailDto>(`/api/v1/admin/${club}/registrations/${id}`)
}

export function createAdminRegistration(club: string, payload: CreateRegistrationPayload): Promise<AdminRegistrationDetailDto> {
  return apiRequest<AdminRegistrationDetailDto>(`/api/v1/admin/${club}/registrations`, { method: 'POST', body: payload })
}

export function updateAdminRegistration(
  club: string,
  id: string,
  payload: UpdateRegistrationPayload,
): Promise<AdminRegistrationDetailDto> {
  return apiRequest<AdminRegistrationDetailDto>(`/api/v1/admin/${club}/registrations/${id}`, { method: 'PUT', body: payload })
}

/** CSV 匯出，吃與清單同一組篩選；檔名由伺服器提供（沒給才用備用檔名）。 */
export function downloadAdminRegistrationsCsv(club: string, params: ListAdminRegistrationsParams = {}, purpose?: string): Promise<void> {
  // 匯出用途與 P4／K3／G3 同名參數 `purpose`（後端記錄匯出人員、筆數與用途）
  return downloadExport(`/api/v1/admin/${club}/registrations/export${buildQuery({ ...params, purpose })}`, `registrations-${club}.csv`)
}

/** 批次改狀態（1–200 筆）；已是該狀態或找不到的列進 `skipped`。 */
export function batchUpdateRegistrationStatus(club: string, ids: string[], status: string): Promise<BatchResultDto> {
  return postBatch(`/api/v1/admin/${club}/registrations/batch/status`, { ids, status })
}

/** 候補 → 已確認（佔名額）；非候補列後端回 400。 */
export function promoteAdminRegistration(club: string, id: string): Promise<AdminRegistrationDetailDto> {
  return apiRequest<AdminRegistrationDetailDto>(`/api/v1/admin/${club}/registrations/${id}/promote`, { method: 'POST' })
}

// ── 候補遞補提醒 ────────────────────────────────────────────────────────────────

export interface WaitlistEntryDto {
  order: number
  registrationId: string
  registrationNo: string
  applicantName: string
  phone?: string | null
  guardianName?: string | null
  guardianPhone?: string | null
  queuedAt: string
}

export interface WaitlistReminderDto {
  sessionId: string
  programNameZh?: string | null
  startOn?: string | null
  endOn?: string | null
  capacity?: number | null
  enrolledCount: number
  vacancy: number
  waiting: WaitlistEntryDto[]
}

export function listWaitlistReminders(club: string): Promise<WaitlistReminderDto[]> {
  return apiRequest<WaitlistReminderDto[]>(`/api/v1/admin/${club}/registrations/waitlist-reminders`)
}

// ── 課程簽到表 ──────────────────────────────────────────────────────────────────

export interface RegistrationSignInSheetDto {
  sessionId: string
  programNameZh?: string | null
  startOn?: string | null
  endOn?: string | null
  venueName?: string | null
  generatedAt: string
  rows: SignInRowDto[]
}

export function getRegistrationSignInSheet(club: string, sessionId: string): Promise<RegistrationSignInSheetDto> {
  return apiRequest<RegistrationSignInSheetDto>(`/api/v1/admin/${club}/registrations/sign-in-sheet${buildQuery({ sessionId })}`)
}
