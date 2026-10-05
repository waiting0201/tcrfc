/**
 * E4 廣告主與版位、E5 投放檔期與素材、E6 成效報表，對照 apps/api/README.md「D 批」節。
 * 🔴 廣告不分俱樂部（App 是兩隊共用平台）：路徑 `/api/v1/admin/ads/…`，沒有俱樂部路由段。
 */
import { apiRequest, apiUploadRequest, buildMultipart, buildQuery, downloadExport } from './adminCommon'

const ads = '/api/v1/admin/ads'

// ── E4 版位 ──
export interface AdSlotDto {
  id: string
  slotCode: string
  surface: string
  screenCode: string | null
  blockOrder: number | null
  aspectRatio: string | null
  minWidth: number | null
  minHeight: number | null
  maxFileKb: number | null
  allowedFormats: string | null
  allowVideo: boolean
  sessionImpressionCap: number | null
  rotationCap: number
  fallbackImageKey: string | null
  fallbackImageUrl: string | null
  fallbackImageThumbUrl: string | null
  fallbackLink: string | null
  isActive: boolean
  nameZh: string | null
  nameEn: string | null
  fallbackAltZh: string | null
  fallbackAltEn: string | null
  campaignCount: number
  updatedAt: string
}

export interface SaveAdSlotPayload {
  slotCode?: string
  screenCode?: string | null
  blockOrder?: number | null
  aspectRatio?: string | null
  minWidth?: number | null
  minHeight?: number | null
  maxFileKb?: number | null
  allowedFormats?: string | null
  allowVideo: boolean
  sessionImpressionCap?: number | null
  rotationCap: number
  fallbackLink?: string | null
  isActive: boolean
  removeFallbackImage?: boolean
  content: { zh: { name: string; fallbackAlt?: string | null }; en?: { name: string; fallbackAlt?: string | null } }
}

export const listAdSlots = () => apiRequest<AdSlotDto[]>(`${ads}/slots`)
export const createAdSlot = (payload: SaveAdSlotPayload, image: File | null) =>
  apiUploadRequest<AdSlotDto>(`${ads}/slots`, buildMultipart(payload, { fallbackImage: image }))
export const updateAdSlot = (id: string, payload: SaveAdSlotPayload, image: File | null) =>
  apiUploadRequest<AdSlotDto>(`${ads}/slots/${id}`, buildMultipart(payload, { fallbackImage: image }), { method: 'PUT' })
export const deleteAdSlot = (id: string) => apiRequest<void>(`${ads}/slots/${id}`, { method: 'DELETE' })

// ── E4 廣告主 ──
export type AdvertiserStatus = 'negotiating' | 'active' | 'ended'

export interface AdvertiserDto {
  id: string
  nameZh: string | null
  nameEn: string | null
  taxId: string | null
  contactName: string | null
  contactPhone: string | null
  contactEmail: string | null
  contractNote: string | null
  cooperationStartOn: string | null
  cooperationEndOn: string | null
  sponsorId: string | null
  sponsorName: string | null
  status: AdvertiserStatus
  statusLabel: string
  campaignCount: number
  updatedAt: string
}

export interface SaveAdvertiserPayload {
  taxId?: string | null
  contactName?: string | null
  contactPhone?: string | null
  contactEmail?: string | null
  contractNote?: string | null
  cooperationStartOn?: string | null
  cooperationEndOn?: string | null
  sponsorId?: string | null
  status?: AdvertiserStatus
  content: { zh: { name: string }; en?: { name: string } }
}

export interface SponsorOptionDto {
  id: string
  name: string
  clubCode: string | null
}

export const listAdvertisers = (params: { status?: string; keyword?: string } = {}) =>
  apiRequest<AdvertiserDto[]>(`${ads}/advertisers${buildQuery({ ...params })}`)
export const createAdvertiser = (body: SaveAdvertiserPayload) => apiRequest<AdvertiserDto>(`${ads}/advertisers`, { method: 'POST', body })
export const updateAdvertiser = (id: string, body: SaveAdvertiserPayload) => apiRequest<AdvertiserDto>(`${ads}/advertisers/${id}`, { method: 'PUT', body })
export const deleteAdvertiser = (id: string) => apiRequest<void>(`${ads}/advertisers/${id}`, { method: 'DELETE' })
export const listSponsorOptions = (keyword?: string) => apiRequest<SponsorOptionDto[]>(`${ads}/advertisers/sponsor-options${buildQuery({ keyword })}`)

// ── E5 檔期 ──
export type CampaignStatus = 'draft' | 'pending_review' | 'scheduled' | 'running' | 'paused' | 'ended' | 'closed' | 'voided'
export type CampaignGoalType = 'guaranteed' | 'traffic'
export type CampaignAction = 'submit' | 'approve' | 'return' | 'pause' | 'resume' | 'close' | 'void'

export interface CampaignListItemDto {
  id: string
  name: string
  advertiserId: string
  advertiserName: string | null
  slotId: string
  slotCode: string
  slotName: string | null
  startsAt: string
  endsAt: string
  weight: number
  goalType: CampaignGoalType
  goalTypeLabel: string
  goalImpressions: number | null
  deliveredTotal: number
  status: CampaignStatus
  statusLabel: string
  creativeCount: number
  approvedCreativeCount: number
  updatedAt: string
}

export interface CampaignPacingDto {
  goalImpressions: number
  delivered: number
  expectedByNow: number
  dailyTarget: number
  status: 'ahead' | 'on_track' | 'behind'
  statusLabel: string
}

export interface CreativeDto {
  id: string
  campaignId: string
  locale: 'zh' | 'en'
  imageKey: string | null
  imageUrl: string | null
  imageThumbUrl: string | null
  imageWidth: number | null
  imageHeight: number | null
  videoKey: string | null
  videoUrl: string | null
  altText: string
  title: string | null
  ctaText: string | null
  clickUrl: string | null
  theme: string | null
  themeLabel?: string | null
  variantTag: string | null
  reviewStatus: 'pending' | 'approved' | 'rejected'
  reviewStatusLabel?: string | null
  rejectReason: string | null
  reviewedAt: string | null
  isPaused: boolean
  updatedAt: string
}

export interface CampaignDetailDto extends CampaignListItemDto {
  dailyImpressionCap: number | null
  perDeviceDailyCap: number | null
  deliveredToday: number
  /** 沒有合約金額檢視權限時為 null。 */
  contractAmount: number | null
  /** 沒權限＝「不公開」；有權限＝「NT$ 30,000」或「尚未填寫」。 */
  contractAmountLabel: string
  isAmountHidden: boolean | null
  pauseReason: string | null
  reviewedBy: string | null
  /** 後端若提供顯示名稱就用；沒有時畫面只顯示「已審核」，不顯示識別碼。 */
  reviewedByName?: string | null
  reviewedAt: string | null
  availableActions: CampaignAction[]
  pacing: CampaignPacingDto | null
  creatives: CreativeDto[]
}

export interface SaveCampaignPayload {
  advertiserId: string
  slotId: string
  name: string
  startsAt: string
  endsAt: string
  weight?: number
  dailyImpressionCap?: number | null
  perDeviceDailyCap?: number | null
  goalType?: CampaignGoalType
  goalImpressions?: number | null
  contractAmount?: number | null
  isAmountHidden?: boolean
}

export interface SlotScheduleDto {
  slotId: string
  slotCode: string
  rotationCap: number
  maxConcurrent: number
  exceedsRotationCap: boolean
  items: {
    campaignId: string
    name: string
    advertiserName: string | null
    startsAt: string
    endsAt: string
    weight: number
    status: string
    statusLabel: string
    weightSharePercent: number
  }[]
}

export const listCampaigns = (params: { status?: string; slotId?: string; advertiserId?: string; keyword?: string } = {}) =>
  apiRequest<CampaignListItemDto[]>(`${ads}/campaigns${buildQuery({ ...params })}`)
export const getCampaign = (id: string) => apiRequest<CampaignDetailDto>(`${ads}/campaigns/${id}`)
export const createCampaign = (body: SaveCampaignPayload) => apiRequest<CampaignDetailDto>(`${ads}/campaigns`, { method: 'POST', body })
export const updateCampaign = (id: string, body: SaveCampaignPayload) => apiRequest<CampaignDetailDto>(`${ads}/campaigns/${id}`, { method: 'PUT', body })
export const deleteCampaign = (id: string) => apiRequest<void>(`${ads}/campaigns/${id}`, { method: 'DELETE' })
export const runCampaignAction = (id: string, action: CampaignAction, body?: { reason: string }) =>
  apiRequest<CampaignDetailDto>(`${ads}/campaigns/${id}/${action}`, { method: 'POST', body })
export const getSlotSchedule = (slotId: string, params: { from?: string; to?: string } = {}) =>
  apiRequest<SlotScheduleDto>(`${ads}/slots/${slotId}/schedule${buildQuery({ ...params })}`)

// ── E5 素材 ──
export interface SaveCreativePayload {
  locale: 'zh' | 'en'
  altText: string
  title?: string | null
  ctaText?: string | null
  clickUrl?: string | null
  theme?: string | null
  variantTag?: string | null
  removeVideo?: boolean
}

export const createCreative = (campaignId: string, payload: SaveCreativePayload, image: File | null, video: File | null) =>
  apiUploadRequest<CreativeDto>(`${ads}/campaigns/${campaignId}/creatives`, buildMultipart(payload, { image, video }))
export const updateCreative = (id: string, payload: SaveCreativePayload, image: File | null, video: File | null) =>
  apiUploadRequest<CreativeDto>(`${ads}/creatives/${id}`, buildMultipart(payload, { image, video }), { method: 'PUT' })
export const deleteCreative = (id: string) => apiRequest<void>(`${ads}/creatives/${id}`, { method: 'DELETE' })
export const runCreativeAction = (id: string, action: 'approve' | 'reject' | 'pause' | 'resume', body?: { reason: string }) =>
  apiRequest<CreativeDto>(`${ads}/creatives/${id}/${action}`, { method: 'POST', body })

// ── E6 報表 ──
export type ReportGroupBy = 'campaign' | 'slot' | 'creative' | 'platform' | 'locale' | 'date'

export interface ReportFilters {
  from?: string
  to?: string
  campaignId?: string
  slotId?: string
  creativeId?: string
  platform?: string
  locale?: string
  groupBy?: ReportGroupBy
}

export interface ReportRowDto {
  label: string
  id: string | null
  impressions: number
  clicks: number
  ctr: number
  uniqueDevices: number
}

export interface AdReportDto {
  from: string
  to: string
  groupBy: ReportGroupBy
  rows: ReportRowDto[]
  total: ReportRowDto
  pendingEvents: number
  pacing: { campaignId: string; campaignName: string; pacing: CampaignPacingDto }[]
}

export interface AdMaintenanceResultDto {
  campaignsStarted: number
  campaignsEnded: number
  eventsAggregated: number
  daysRebuilt: number
  eventsPurged: number
  diagnosticsPurged: number
  overdueUnaggregated: number
}

export const getAdReport = (params: ReportFilters) => apiRequest<AdReportDto>(`${ads}/reports${buildQuery({ ...params })}`)
export const exportAdReport = (params: ReportFilters & { purpose: string }) => downloadExport(`${ads}/reports/export${buildQuery({ ...params })}`, '廣告成效報表.csv')
export const runAdMaintenance = () => apiRequest<AdMaintenanceResultDto>(`${ads}/maintenance/run`, { method: 'POST' })
