/**
 * M1–M5 行動 App 後台端點，對照 apps/api/README.md「D 批」節。
 * 🔴 App 是兩隊共用平台：路徑 `/api/v1/admin/app/…`，沒有俱樂部路由段。
 */
import { apiRequest, apiUploadRequest, buildMultipart, buildQuery, type PagedResult } from './adminCommon'

const app = '/api/v1/admin/app'

/** 版本／維護模式／功能開關存檔後，是否已同步到靜態設定（目前恆為「尚未串接」，存檔本身仍成功）。 */
export interface EdgePublishDto {
  published: boolean
  message: string
}
export interface WithEdge<T> {
  value: T
  edgePublish: EdgePublishDto
}

// ── M1 版本 ──
export type ReleasePlatform = 'ios' | 'android'
export type ReleaseStatus = 'testing' | 'live' | 'withdrawn'

export interface ReleaseLocaleText {
  whatsNew?: string | null
  forceMessage?: string | null
  recommendMessage?: string | null
}
export interface ReleaseDto {
  id: string
  platform: ReleasePlatform
  platformLabel: string
  version: string
  buildNumber: string | number | null
  releasedOn: string | null
  status: ReleaseStatus
  statusLabel: string
  isMinSupported: boolean
  isRecommended: boolean
  zh?: ReleaseLocaleText | null
  en?: ReleaseLocaleText | null
  updatedAt: string
}
export interface SaveReleasePayload {
  platform: ReleasePlatform
  version: string
  buildNumber?: number | null
  releasedOn?: string | null
  status?: ReleaseStatus
  content: { zh: ReleaseLocaleText; en?: ReleaseLocaleText }
}
export interface MaintenanceDto {
  scope: 'all' | 'ios' | 'android'
  scopeLabel: string
  enabled: boolean
  messageZh: string | null
  messageEn: string | null
}

export const listReleases = (platform?: string) => apiRequest<ReleaseDto[]>(`${app}/releases${buildQuery({ platform })}`)
export const createRelease = (body: SaveReleasePayload) => apiRequest<ReleaseDto>(`${app}/releases`, { method: 'POST', body })
export const updateRelease = (id: string, body: SaveReleasePayload) => apiRequest<WithEdge<ReleaseDto>>(`${app}/releases/${id}`, { method: 'PUT', body })
export const deleteRelease = (id: string) => apiRequest<void>(`${app}/releases/${id}`, { method: 'DELETE' })
export const setReleaseFlags = (id: string, body: { isMinSupported: boolean; isRecommended: boolean; confirmForceUpdate: boolean }) =>
  apiRequest<WithEdge<ReleaseDto>>(`${app}/releases/${id}/flags`, { method: 'PUT', body })
export const listMaintenance = () => apiRequest<MaintenanceDto[]>(`${app}/maintenance`)
export const saveMaintenance = (scope: string, body: { enabled: boolean; messageZh?: string | null; messageEn?: string | null }) =>
  apiRequest<WithEdge<MaintenanceDto>>(`${app}/maintenance/${scope}`, { method: 'PUT', body })

// ── M2 內容編排 ──
export type LayoutKind = 'home_section' | 'quick_entry' | 'more_item'
export interface LayoutItemDto {
  id: string
  kind: LayoutKind
  kindLabel: string
  itemKey: string
  deepLinkId: string | null
  deepLinkCode: string | null
  iconKey: string | null
  sortOrder: number
  isEnabled: boolean
  isFixed: boolean
  labelZh: string | null
  labelEn: string | null
}
export interface SaveLayoutItemPayload {
  kind?: LayoutKind
  itemKey?: string
  deepLinkId?: string | null
  iconKey?: string | null
  isEnabled: boolean
  label: { zh: string; en?: string }
}
export interface DeepLinkDto {
  id: string
  code: string
  appLink: string
  webUrl: string | null
  requiresLogin: boolean
  isActive: boolean
  sortOrder: number
  labelZh: string | null
  labelEn: string | null
  usedByCount: number
}
export interface SaveDeepLinkPayload {
  code: string
  appLink: string
  webUrl?: string | null
  requiresLogin: boolean
  isActive: boolean
  label: { zh: string; en?: string }
}
export type AudienceTier = 'all' | 'fan_club' | 'registered' | 'anonymous'
export interface AnnouncementDto {
  id: string
  messageZh: string | null
  messageEn: string | null
  linkUrl: string | null
  startsAt: string | null
  endsAt: string | null
  audienceTier: AudienceTier
  audienceTierLabel?: string | null
  audienceClubCode: string | null
  isEnabled: boolean
  isActiveNow: boolean
}
export interface SaveAnnouncementPayload {
  message: { zh: string; en?: string }
  linkUrl?: string | null
  startsAt?: string | null
  endsAt?: string | null
  audienceTier?: AudienceTier
  audienceClubCode?: string | null
  isEnabled: boolean
}

const layout = `${app}/layout`
export const listLayoutItems = (kind?: string) => apiRequest<LayoutItemDto[]>(`${layout}/items${buildQuery({ kind })}`)
export const createLayoutItem = (body: SaveLayoutItemPayload) => apiRequest<LayoutItemDto>(`${layout}/items`, { method: 'POST', body })
export const updateLayoutItem = (id: string, body: SaveLayoutItemPayload) => apiRequest<LayoutItemDto>(`${layout}/items/${id}`, { method: 'PUT', body })
export const deleteLayoutItem = (id: string) => apiRequest<void>(`${layout}/items/${id}`, { method: 'DELETE' })
export const reorderLayoutItems = (kind: LayoutKind, ids: string[]) => apiRequest<LayoutItemDto[]>(`${layout}/items/reorder`, { method: 'POST', body: { kind, ids } })
export const listDeepLinks = () => apiRequest<DeepLinkDto[]>(`${layout}/deep-links`)
export const createDeepLink = (body: SaveDeepLinkPayload) => apiRequest<DeepLinkDto>(`${layout}/deep-links`, { method: 'POST', body })
export const updateDeepLink = (id: string, body: SaveDeepLinkPayload) => apiRequest<DeepLinkDto>(`${layout}/deep-links/${id}`, { method: 'PUT', body })
export const deleteDeepLink = (id: string) => apiRequest<void>(`${layout}/deep-links/${id}`, { method: 'DELETE' })
export const listAnnouncements = () => apiRequest<AnnouncementDto[]>(`${layout}/announcements`)
export const createAnnouncement = (body: SaveAnnouncementPayload) => apiRequest<AnnouncementDto>(`${layout}/announcements`, { method: 'POST', body })
export const updateAnnouncement = (id: string, body: SaveAnnouncementPayload) => apiRequest<AnnouncementDto>(`${layout}/announcements/${id}`, { method: 'PUT', body })
export const deleteAnnouncement = (id: string) => apiRequest<void>(`${layout}/announcements/${id}`, { method: 'DELETE' })

// ── M3 推播 ──
export type PushStatus = 'draft' | 'pending_review' | 'scheduled' | 'sending' | 'sent' | 'partial' | 'failed' | 'cancelled'
export type PushAction = 'submit' | 'approve' | 'return' | 'cancel' | 'retry'

export interface PushListItemDto {
  id: string
  titleZh: string | null
  kind: string
  status: PushStatus
  statusLabel: string
  scheduledAt: string | null
  sentAt: string | null
  sentCount: number
  deliveredCount: number
  openedCount: number
  createdBy: string | null
  /** 後端新增的顯示名稱；缺值時畫面顯示「—」，絕不顯示 createdBy（那是識別碼）。 */
  createdByName?: string | null
  createdAt: string
}
export interface PushStatDto {
  platform: string
  platformLabel: string
  locale: string
  localeLabel: string
  sent: number
  delivered: number
  opened: number
}
export interface PushLocaleContent {
  title: string
  body: string
  imageAlt?: string | null
}
export interface PushDetailDto {
  id: string
  kind: string
  status: PushStatus
  statusLabel: string
  content: { zh: PushLocaleContent; en?: PushLocaleContent | null }
  imageKey: string | null
  imageUrl: string | null
  deepLink: string | null
  audienceTier: AudienceTier
  audienceTierLabel?: string | null
  audienceClubCode: string | null
  audienceTeamCodes: string[]
  scheduledAt: string | null
  audienceEstimate: number | null
  createdBy: string | null
  createdByName?: string | null
  reviewedBy: string | null
  reviewedByName?: string | null
  reviewedAt: string | null
  rejectNote: string | null
  sentAt: string | null
  sentCount: number
  deliveredCount: number
  failedCount: number
  openedCount: number
  failureMessage: string | null
  stats: PushStatDto[]
  statsNote: string
  availableActions: PushAction[]
  updatedAt: string
}
export interface SavePushPayload {
  kind?: string
  deepLink?: string | null
  audienceTier?: AudienceTier
  audienceClubCode?: string | null
  audienceTeamCodes?: string[]
  scheduledAt?: string | null
  removeImage?: boolean
  content: { zh: PushLocaleContent; en?: PushLocaleContent }
}
export interface AudienceInput {
  audienceTier?: AudienceTier
  audienceClubCode?: string | null
  audienceTeamCodes?: string[]
}
export interface PushEstimateDto {
  total: number
  breakdown: PushStatDto[]
}
export interface PushPreviewDto {
  zh: PushLocaleContent
  en: PushLocaleContent | null
  enEffective: PushLocaleContent
  deepLink: string | null
  imageUrl: string | null
  audienceSummary: string
}
export interface PushTestSendDto {
  configured: boolean
  sent: number
  failed: number
  unknownDevices: string[]
  message: string
}
export interface PushRulesDto {
  matchReminderHours: number
  membershipExpiryDays: number[]
  toggles: { key: string; label: string; enabled: boolean }[]
}

const push = `${app}/push`
export const listPushMessages = (status?: string) => apiRequest<PushListItemDto[]>(`${push}/messages${buildQuery({ status })}`)
export const getPushMessage = (id: string) => apiRequest<PushDetailDto>(`${push}/messages/${id}`)
export const createPushMessage = (payload: SavePushPayload, image: File | null) =>
  apiUploadRequest<PushDetailDto>(`${push}/messages`, buildMultipart(payload, { image }))
export const updatePushMessage = (id: string, payload: SavePushPayload, image: File | null) =>
  apiUploadRequest<PushDetailDto>(`${push}/messages/${id}`, buildMultipart(payload, { image }), { method: 'PUT' })
export const deletePushMessage = (id: string) => apiRequest<void>(`${push}/messages/${id}`, { method: 'DELETE' })
export const estimatePush = (body: AudienceInput) => apiRequest<PushEstimateDto>(`${push}/estimate`, { method: 'POST', body })
export const previewPush = (id: string) => apiRequest<PushPreviewDto>(`${push}/messages/${id}/preview`)
export const testSendPush = (id: string, deviceInstallIds: string[]) => apiRequest<PushTestSendDto>(`${push}/messages/${id}/test-send`, { method: 'POST', body: { deviceInstallIds } })
export const submitPush = (id: string) => apiRequest<PushDetailDto>(`${push}/messages/${id}/submit`, { method: 'POST' })
export const approvePush = (id: string, expectedAudience: number) => apiRequest<PushDetailDto>(`${push}/messages/${id}/approve`, { method: 'POST', body: { expectedAudience } })
export const returnPush = (id: string, note: string) => apiRequest<PushDetailDto>(`${push}/messages/${id}/return`, { method: 'POST', body: { note } })
export const cancelPush = (id: string) => apiRequest<PushDetailDto>(`${push}/messages/${id}/cancel`, { method: 'POST' })
export const retryPush = (id: string) => apiRequest<PushDetailDto>(`${push}/messages/${id}/retry`, { method: 'POST' })
export const dispatchDuePush = () => apiRequest<{ dispatched: number }>(`${push}/dispatch-due`, { method: 'POST' })
export const getPushRules = () => apiRequest<PushRulesDto>(`${push}/rules`)
export const savePushRules = (body: { matchReminderHours?: number; membershipExpiryDays?: number[]; toggles?: Record<string, boolean> }) =>
  apiRequest<PushRulesDto>(`${push}/rules`, { method: 'PUT', body })

// ── M4 裝置 ──
export interface DeviceDto {
  id: string
  deviceInstallIdMasked: string
  platform: string
  platformLabel: string
  osVersion: string | null
  appVersion: string | null
  locale: string
  localeLabel: string
  pushPermission: string
  pushPermissionLabel: string
  pushTokenStatus: string
  pushTokenStatusLabel: string
  isMemberBound: boolean
  firstSeenAt: string
  lastActiveAt: string
}
export interface DeviceDetailDto {
  device: DeviceDto
  deviceInstallId: string | null
  pushToken: string | null
  subscriptionCount: number
  revealed: boolean
}
export interface DeviceStatsDto {
  totalDevices: number
  activeLast7Days: number
  activeLast30Days: number
  invalidTokenCount: number
  byVersion: { platform: string; platformLabel: string; appVersion: string; count: number }[]
  byPermission: { key: string; label: string; count: number }[]
  devicesBelowVersion?: number | null
}
const devices = `${app}/devices`
export const listDevices = (params: { platform?: string; appVersion?: string; permission?: string; tokenStatus?: string; page: number; pageSize: number }) =>
  apiRequest<PagedResult<DeviceDto>>(`${devices}${buildQuery({ ...params })}`)
export const getDevice = (id: string, reveal = false) => apiRequest<DeviceDetailDto>(`${devices}/${id}${buildQuery({ reveal: reveal || undefined })}`)
export const getDeviceStats = (params: { platform?: string; belowVersion?: string } = {}) => apiRequest<DeviceStatsDto>(`${devices}/stats${buildQuery({ ...params })}`)
export const cleanupInvalidTokens = () => apiRequest<{ tokensCleared: number }>(`${devices}/cleanup-invalid-tokens`, { method: 'POST' })

// ── M5 設定 ──
export type FlagPlatform = 'all' | 'ios' | 'android'
export interface FlagDto {
  id: string
  flagKey: string
  isEnabled: boolean
  stringValue: string | null
  platform: FlagPlatform
  platformLabel: string
  description: string | null
  updatedAt: string
}
export interface SaveFlagPayload {
  flagKey?: string
  isEnabled: boolean
  stringValue?: string | null
  platform?: FlagPlatform
  description?: string | null
}
export type CredentialHealth = 'ok' | 'due_soon' | 'overdue' | 'untracked'
export interface CredentialDto {
  id: string
  kind: string
  kindLabel: string
  label: string
  externalRef: string | null
  createdOn: string | null
  lastRotatedOn: string | null
  expiresOn: string | null
  rotationPeriodDays: number | null
  nextDueOn: string | null
  daysUntilDue: number | null
  health: CredentialHealth
  healthLabel: string
  note: string | null
}
export interface SaveCredentialPayload {
  kind: string
  label: string
  externalRef?: string | null
  createdOn?: string | null
  lastRotatedOn?: string | null
  expiresOn?: string | null
  rotationPeriodDays?: number | null
  note?: string | null
}
export interface ConnectionCheckDto {
  checkedAt: string
  items: { key: string; label: string; status: 'ok' | 'warning' | 'not_configured' | 'error'; statusLabel: string; message: string }[]
}
export interface DiagnosticDto {
  id: string
  platform: string
  platformLabel: string
  appVersion: string | null
  buildNumber: string | number | null
  osVersion: string | null
  occurredAt: string
  reportType: string
  reportTypeLabel: string
  metricValue: number | null
  summary: string | null
  status: 'new' | 'reviewing' | 'resolved' | 'ignored'
  statusLabel: string
}
export interface DiagnosticSummaryDto {
  days: number
  byVersion: { platform: string; appVersion: string; crashes: number; activeDevices: number; devicesWithCrash: number; crashFreeDevicePercent: number | null }[]
  byType: { key?: string; type?: string; label?: string; count: number }[]
  startupMedianMs: number | null
  startupP90Ms: number | null
  apiErrorNote: string | null
}

const cfg = `${app}/config`
export const listFlags = () => apiRequest<FlagDto[]>(`${cfg}/flags`)
export const createFlag = (body: SaveFlagPayload) => apiRequest<WithEdge<FlagDto>>(`${cfg}/flags`, { method: 'POST', body })
export const updateFlag = (id: string, body: SaveFlagPayload) => apiRequest<WithEdge<FlagDto>>(`${cfg}/flags/${id}`, { method: 'PUT', body })
export const deleteFlag = (id: string) => apiRequest<WithEdge<boolean>>(`${cfg}/flags/${id}`, { method: 'DELETE' })
export const listCredentials = () => apiRequest<CredentialDto[]>(`${cfg}/credentials`)
export const createCredential = (body: SaveCredentialPayload) => apiRequest<CredentialDto>(`${cfg}/credentials`, { method: 'POST', body })
export const updateCredential = (id: string, body: SaveCredentialPayload) => apiRequest<CredentialDto>(`${cfg}/credentials/${id}`, { method: 'PUT', body })
export const deleteCredential = (id: string) => apiRequest<void>(`${cfg}/credentials/${id}`, { method: 'DELETE' })
export const rotateCredential = (id: string, newExpiresOn?: string) => apiRequest<CredentialDto>(`${cfg}/credentials/${id}/rotate${buildQuery({ newExpiresOn })}`, { method: 'POST' })
export const getConnectionCheck = () => apiRequest<ConnectionCheckDto>(`${cfg}/connection-check`)
export const listDiagnostics = (params: { type?: string; status?: string; platform?: string; appVersion?: string; page: number; pageSize: number }) =>
  apiRequest<PagedResult<DiagnosticDto>>(`${app}/diagnostics${buildQuery({ ...params })}`)
export const getDiagnostic = (id: string) => apiRequest<{ report: DiagnosticDto; detail: string | null }>(`${app}/diagnostics/${id}`)
export const setDiagnosticStatus = (id: string, status: string) => apiRequest<DiagnosticDto>(`${app}/diagnostics/${id}/status`, { method: 'PUT', body: { status } })
export const getDiagnosticSummary = (days = 7) => apiRequest<DiagnosticSummaryDto>(`${app}/diagnostics/summary${buildQuery({ days })}`)
