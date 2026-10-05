/**
 * K5 抽獎名單管理後台端點，對照 apps/api/README.md「C1」節「K5 抽獎名單」。
 * 🔴 系統不抽出：實體抽獎於現場或直播由人工進行，中獎人以序號回填。
 */
import { apiRequest, apiUploadRequest, buildMultipart, buildQuery, downloadExport, type BilingualContentInput, type PagedResult } from './adminCommon'

const base = (club: string) => `/api/v1/admin/${club}/draws`

export type DrawStatus = 'draft' | 'roster_locked' | 'drawn' | 'announced' | 'closed' | 'voided'
export const DRAW_STATUS_OPTIONS: { value: DrawStatus; label: string }[] = [
  { value: 'draft', label: '草稿' },
  { value: 'roster_locked', label: '名單已鎖定' },
  { value: 'drawn', label: '已抽出' },
  { value: 'announced', label: '已公布' },
  { value: 'closed', label: '已結案' },
  { value: 'voided', label: '已作廢' },
]
export const DRAW_OCCASION_OPTIONS = [
  { value: 'home_match', label: '主場賽事日' },
  { value: 'livestream', label: '直播' },
  { value: 'other', label: '其他' },
]

export interface DrawListItemDto {
  id: string
  drawCode: string
  nameZh?: string | null
  nameEn?: string | null
  snapshotAt?: string | null
  drawnAt?: string | null
  drawOccasion?: string | null
  drawOccasionLabel?: string | null
  claimDeadlineOn?: string | null
  status: DrawStatus
  statusLabel: string
  rosterVersion: number
  /** 尚未試算／產生名單時可能為 null，畫面不得顯示成「null 人」。 */
  totalCount?: number | null
  winnerCount: number
  backupCount: number
  fulfilledCount: number
  announcementArticleId?: string | null
  announcementStatus?: string | null
  announcementStatusLabel?: string | null
  createdByName?: string | null
  createdAt: string
  updatedAt: string
}

export type DrawAction =
  | 'edit'
  | 'generate_roster'
  | 'regenerate_roster'
  | 'record_winners'
  | 'announce'
  | 'mark_announced'
  | 'close'
  | 'void'
  | 'delete'

export interface DrawLocale {
  name: string
  prizeDescription?: string | null
  rules?: string | null
  notes?: string | null
}

export interface DrawDetailDto extends DrawListItemDto {
  coverKey?: string | null
  coverUrl?: string | null
  coverThumbUrl?: string | null
  internalNote?: string | null
  rosterHash?: string | null
  lockedAt?: string | null
  lockedByName?: string | null
  zh: DrawLocale
  en?: DrawLocale | null
  versions: {
    version: number
    snapshotAt?: string | null
    totalCount?: number | null
    rosterHash?: string | null
    generatedAt?: string | null
    generatedByName?: string | null
    voidedAt?: string | null
    voidReason?: string | null
    isCurrent: boolean
  }[]
  availableActions: DrawAction[]
}

export interface SaveDrawPayload {
  drawCode?: string
  snapshotAt?: string | null
  drawnAt?: string | null
  drawOccasion?: string | null
  claimDeadlineOn?: string | null
  internalNote?: string | null
  removeCover?: boolean
  content: BilingualContentInput<DrawLocale>
}

export interface RosterRowDto {
  serialNo: number
  memberNo: string
  name?: string | null
  tier: string
  tierLabel: string
  membershipEndOn?: string | null
  isWinner: boolean
  isBackup: boolean
  prizeName?: string | null
  isMasked: boolean
}

export type FulfilmentStatus = 'pending' | 'shipped' | 'claimed'
export interface FulfilmentRowDto {
  serialNo: number
  memberNo: string
  memberName?: string | null
  isBackup: boolean
  prizeName?: string | null
  claimMethod?: 'ship' | 'pickup' | null
  claimMethodLabel?: string | null
  recipientName?: string | null
  recipientPhone?: string | null
  recipientAddress?: string | null
  fulfilmentStatus: FulfilmentStatus
  effectiveStatus: string
  effectiveStatusLabel: string
  shippedAt?: string | null
  claimedAt?: string | null
  note?: string | null
  isMasked: boolean
}

export interface AnnouncementPreviewDto {
  drawName: string
  prizeDescription?: string | null
  snapshotAt?: string | null
  eligibleCount: number
  winners: { serialNo: number; memberNo: string; maskedName: string; prizeName: string }[]
}

export function getDrawNotice(club: string): Promise<{ confirmed: boolean; confirmedAt?: string | null }> {
  return apiRequest(`${base(club)}/notice`)
}
export function saveDrawNotice(club: string, confirmed: boolean): Promise<{ confirmed: boolean; confirmedAt?: string | null }> {
  return apiRequest(`${base(club)}/notice`, { method: 'PUT', body: { confirmed } })
}
export function listDraws(club: string, params: { status?: string; keyword?: string; page?: number; pageSize?: number }): Promise<PagedResult<DrawListItemDto>> {
  return apiRequest<PagedResult<DrawListItemDto>>(`${base(club)}${buildQuery(params)}`)
}
export function getDraw(club: string, id: string): Promise<DrawDetailDto> {
  return apiRequest<DrawDetailDto>(`${base(club)}/${id}`)
}
export function createDraw(club: string, payload: SaveDrawPayload, cover: File | null): Promise<DrawDetailDto> {
  return apiUploadRequest<DrawDetailDto>(base(club), buildMultipart(payload, { cover }), { method: 'POST' })
}
export function updateDraw(club: string, id: string, payload: SaveDrawPayload, cover: File | null): Promise<DrawDetailDto> {
  return apiUploadRequest<DrawDetailDto>(`${base(club)}/${id}`, buildMultipart(payload, { cover }), { method: 'PUT' })
}
export function deleteDraw(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}`, { method: 'DELETE' })
}
export function previewRoster(club: string, id: string): Promise<{ asOf: string; eligibleCount: number }> {
  return apiRequest(`${base(club)}/${id}/roster/preview`, { method: 'POST' })
}
/** 產生並鎖定名單；名單已鎖定時再送＝作廢重產，必須帶 `voidReason`。 */
export function generateRoster(club: string, id: string, voidReason?: string): Promise<DrawDetailDto> {
  return apiRequest<DrawDetailDto>(`${base(club)}/${id}/roster`, { method: 'POST', body: voidReason ? { voidReason } : {} })
}
export function listRoster(
  club: string,
  id: string,
  params: { version?: number; keyword?: string; winnersOnly?: boolean; page?: number; pageSize?: number; reveal?: boolean },
): Promise<PagedResult<RosterRowDto>> {
  // reveal 預設不送＝後端回遮罩；true 需要「檢視完整個資」權限，否則 403
  return apiRequest<PagedResult<RosterRowDto>>(`${base(club)}/${id}/roster${buildQuery({ ...params, reveal: params.reveal || undefined })}`)
}
export function saveWinners(
  club: string,
  id: string,
  body: { winners: { serialNo: number; prizeName: string; isBackup?: boolean }[]; reason?: string },
): Promise<{ updatedCount: number; draw: DrawDetailDto }> {
  return apiRequest(`${base(club)}/${id}/winners`, { method: 'PUT', body })
}
export function removeWinners(club: string, id: string, body: { serialNos: number[]; reason?: string }): Promise<DrawDetailDto> {
  return apiRequest<DrawDetailDto>(`${base(club)}/${id}/winners/remove`, { method: 'POST', body })
}
export function listFulfilment(club: string, id: string, params: { status?: string; claimMethod?: string; page?: number; pageSize?: number }): Promise<PagedResult<FulfilmentRowDto>> {
  return apiRequest<PagedResult<FulfilmentRowDto>>(`${base(club)}/${id}/fulfilment${buildQuery(params)}`)
}
export function updateFulfilment(
  club: string,
  id: string,
  serialNo: number,
  body: { claimMethod?: string; recipientName?: string; recipientPhone?: string; recipientAddress?: string; status?: FulfilmentStatus; note?: string },
): Promise<FulfilmentRowDto> {
  return apiRequest<FulfilmentRowDto>(`${base(club)}/${id}/fulfilment/${serialNo}`, { method: 'PUT', body })
}
export function batchFulfilmentStatus(
  club: string,
  id: string,
  body: { serialNos: number[]; status: FulfilmentStatus },
): Promise<{ updatedCount: number; skipped: { serialNo: number; reason: string }[] }> {
  return apiRequest(`${base(club)}/${id}/fulfilment/batch/status`, { method: 'POST', body })
}
export function exportDrawPublic(club: string, id: string, purpose: string): Promise<void> {
  return downloadExport(`${base(club)}/${id}/export/public${buildQuery({ purpose })}`, '抽獎名單（公開版）.csv')
}
export function exportDrawWinners(club: string, id: string, purpose: string): Promise<void> {
  return downloadExport(`${base(club)}/${id}/export/winners${buildQuery({ purpose })}`, '中獎人名單.csv')
}
export function exportDrawShipping(club: string, id: string, purpose: string): Promise<void> {
  return downloadExport(`${base(club)}/${id}/export/shipping${buildQuery({ purpose })}`, '獎品出貨清單.csv')
}
export function getAnnouncementPreview(club: string, id: string): Promise<AnnouncementPreviewDto> {
  return apiRequest<AnnouncementPreviewDto>(`${base(club)}/${id}/announcement-preview`)
}
export function createAnnouncementDraft(club: string, id: string): Promise<{ articleId: string; articleSlug: string; draw: DrawDetailDto }> {
  return apiRequest(`${base(club)}/${id}/announcement-draft`, { method: 'POST' })
}
export function linkAnnouncementArticle(club: string, id: string, articleId: string): Promise<DrawDetailDto> {
  return apiRequest<DrawDetailDto>(`${base(club)}/${id}/announcement-article`, { method: 'PUT', body: { articleId } })
}
export function markAnnounced(club: string, id: string): Promise<DrawDetailDto> {
  return apiRequest<DrawDetailDto>(`${base(club)}/${id}/mark-announced`, { method: 'POST' })
}
export function closeDraw(club: string, id: string): Promise<DrawDetailDto> {
  return apiRequest<DrawDetailDto>(`${base(club)}/${id}/close`, { method: 'POST' })
}
export function voidDraw(club: string, id: string, reason: string): Promise<DrawDetailDto> {
  return apiRequest<DrawDetailDto>(`${base(club)}/${id}/void`, { method: 'POST', body: { reason } })
}

/**
 * 合格人數的顯示文字：有數字顯示「N 人」；`totalCount` 為 null 一律顯示「尚未試算」。
 * 後端語意：`rosterVersion=1` ＋ `totalCount=null` 代表尚無名單（版本預設就是 1），所以不能用版本號分流。
 */
export function eligibleCountText(totalCount: number | null | undefined): string {
  return totalCount != null ? `${totalCount} 人` : '尚未試算'
}
