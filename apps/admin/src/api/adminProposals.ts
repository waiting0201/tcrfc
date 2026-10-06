/**
 * E3 提案簡介與下載名單後台端點，對照 apps/api/README.md「E1a」節「E3 提案與 Lead」。
 * 「下載名單」在後端叫 Lead（有意下載提案的訪客），介面一律寫「提案下載名單」。
 */
import { apiBlobRequest, type BlobResult } from './http'
import {
  apiRequest,
  apiUploadRequest,
  buildMultipart,
  buildQuery,
  type PagedResult,
} from './adminCommon'

export type ProposalStatus = 'draft' | 'published'
export type ProposalLocale = 'zh' | 'en'

export interface ProposalFileDto {
  id: string
  locale: ProposalLocale
  versionNo: number
  fileBytes?: number | null
  createdAt: string
}

export interface ProposalListItemDto {
  id: string
  title: string
  titleEn?: string | null
  versionNo: number
  status: ProposalStatus
  locales: ProposalLocale[]
  fileCount: number
  leadCount: number
  updatedAt: string
}

export interface ProposalDetailDto {
  id: string
  title: string
  titleEn?: string | null
  versionNo: number
  status: ProposalStatus
  files: ProposalFileDto[]
  leadCount: number
  createdAt: string
  updatedAt: string
}

export interface SaveProposalPayload {
  title: string
  /** 選填（≤128）；空白＝清除英文。 */
  titleEn?: string | null
  versionNo: number
  status: ProposalStatus
}

const proposalBase = (club: string) => `/api/v1/admin/${club}/proposals`

export function listProposals(club: string): Promise<ProposalListItemDto[]> {
  return apiRequest<ProposalListItemDto[]>(proposalBase(club))
}

export function getProposal(club: string, id: string): Promise<ProposalDetailDto> {
  return apiRequest<ProposalDetailDto>(`${proposalBase(club)}/${id}`)
}

export function createProposal(club: string, payload: SaveProposalPayload): Promise<ProposalDetailDto> {
  return apiRequest<ProposalDetailDto>(proposalBase(club), { method: 'POST', body: payload })
}

export function updateProposal(club: string, id: string, payload: SaveProposalPayload): Promise<ProposalDetailDto> {
  return apiRequest<ProposalDetailDto>(`${proposalBase(club)}/${id}`, { method: 'PUT', body: payload })
}

export function deleteProposal(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${proposalBase(club)}/${id}`, { method: 'DELETE' })
}

export function addProposalFile(
  club: string,
  id: string,
  payload: { locale: ProposalLocale; versionNo?: number },
  file: File,
): Promise<ProposalDetailDto> {
  return apiUploadRequest<ProposalDetailDto>(`${proposalBase(club)}/${id}/files`, buildMultipart(payload, { file }), { method: 'POST' })
}

export function deleteProposalFile(club: string, id: string, fileId: string): Promise<ProposalDetailDto> {
  return apiRequest<ProposalDetailDto>(`${proposalBase(club)}/${id}/files/${fileId}`, { method: 'DELETE' })
}

/** 後台預覽／下載提案檔案（串流，不受前台表單關卡限制）。 */
export function downloadProposalFile(club: string, id: string, fileId: string): Promise<BlobResult> {
  return apiBlobRequest(`${proposalBase(club)}/${id}/files/${fileId}/download`)
}

// ───────────── 提案下載名單（含個資） ─────────────

/** 跟進狀態：後端接受的中文字面值（規格：新進／處理中／已回覆／已結案／無效）。 */
export const LEAD_STATUS_OPTIONS = ['新進', '處理中', '已回覆', '已結案', '無效'] as const
export type LeadStatus = (typeof LEAD_STATUS_OPTIONS)[number]

export interface LeadListItemDto {
  id: string
  company?: string | null
  name?: string | null
  email?: string | null
  proposalId?: string | null
  proposalTitle?: string | null
  sourcePath?: string | null
  utmSource?: string | null
  utmCampaign?: string | null
  status?: string | null
  assigneeAdminUserId?: string | null
  tags?: string | null
  createdAt: string
}

export interface LeadDetailDto extends LeadListItemDto {
  proposalVersionNo?: number | null
  internalNote?: string | null
  updatedAt: string
}

export interface LeadFilter {
  proposalId?: string
  status?: string
  keyword?: string
  dateFrom?: string
  dateTo?: string
}

export interface UpdateLeadPayload {
  status: string
  assigneeAdminUserId?: string | null
  internalNote?: string | null
  tags?: string | null
}

export interface LeadAssigneeDto {
  id: string
  displayName: string
}

const leadBase = (club: string) => `/api/v1/admin/${club}/proposal-leads`

export function listLeads(club: string, filter: LeadFilter & { page?: number; pageSize?: number }): Promise<PagedResult<LeadListItemDto>> {
  return apiRequest<PagedResult<LeadListItemDto>>(`${leadBase(club)}${buildQuery({ ...filter })}`)
}

export function getLead(club: string, id: string): Promise<LeadDetailDto> {
  return apiRequest<LeadDetailDto>(`${leadBase(club)}/${id}`)
}

export function updateLead(club: string, id: string, payload: UpdateLeadPayload): Promise<LeadDetailDto> {
  return apiRequest<LeadDetailDto>(`${leadBase(club)}/${id}`, { method: 'PUT', body: payload })
}

export function listLeadAssignees(club: string): Promise<LeadAssigneeDto[]> {
  return apiRequest<LeadAssigneeDto[]>(`${leadBase(club)}/assignable-users`)
}

/** 匯出名單（含個資的受限操作），篩選條件與清單相同，UTF-8 BOM、欄位標題中文。 */
export function exportLeads(club: string, filter: LeadFilter): Promise<BlobResult> {
  return apiBlobRequest(`${leadBase(club)}/export${buildQuery({ ...filter })}`)
}
