/**
 * B5 慈善與社會影響後台端點，對照 apps/api/README.md「E1a」節「B5」。
 */
import {
  apiRequest,
  apiUploadRequest,
  buildMultipart,
  buildQuery,
  buildSingleFile,
  type BilingualContentInput,
  type PagedResult,
} from './adminCommon'

const base = (club: string) => `/api/v1/admin/${club}/charity`

// ───────────── 公益團體 ─────────────

export interface OrgLocaleContent {
  name: string
  intro?: string | null
}

export interface OrgListItemDto {
  id: string
  slug: string
  isShared: boolean
  websiteUrl?: string | null
  contactName?: string | null
  contactPhone?: string | null
  logoUrl?: string | null
  logoThumbUrl?: string | null
  nameZh?: string | null
  nameEn?: string | null
  programCount: number
  recordCount: number
  updatedAt: string
}

export interface OrgDetailDto {
  id: string
  slug: string
  isShared: boolean
  websiteUrl?: string | null
  contactName?: string | null
  contactPhone?: string | null
  logoKey?: string | null
  logoUrl?: string | null
  zh: OrgLocaleContent
  en?: OrgLocaleContent | null
  programs: { id: string; nameZh?: string | null; status: string }[]
  records: { id: string; happenedOn?: string | null; donationContentZh?: string | null }[]
  createdAt: string
  updatedAt: string
}

export interface SaveOrgPayload {
  slug?: string
  websiteUrl?: string | null
  contactName?: string | null
  contactPhone?: string | null
  content: BilingualContentInput<OrgLocaleContent>
  removeLogo?: boolean
}

export function listOrgs(club: string, keyword?: string): Promise<OrgListItemDto[]> {
  return apiRequest<OrgListItemDto[]>(`${base(club)}/organizations${buildQuery({ keyword })}`)
}

export function getOrg(club: string, id: string): Promise<OrgDetailDto> {
  return apiRequest<OrgDetailDto>(`${base(club)}/organizations/${id}`)
}

export function createOrg(club: string, payload: SaveOrgPayload, logo: File | null): Promise<OrgDetailDto> {
  return apiUploadRequest<OrgDetailDto>(`${base(club)}/organizations`, buildMultipart(payload, { logo }), { method: 'POST' })
}

export function updateOrg(club: string, id: string, payload: SaveOrgPayload, logo: File | null): Promise<OrgDetailDto> {
  return apiUploadRequest<OrgDetailDto>(`${base(club)}/organizations/${id}`, buildMultipart(payload, { logo }), { method: 'PUT' })
}

export function deleteOrg(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/organizations/${id}`, { method: 'DELETE' })
}

// ───────────── 慈善計畫 ─────────────

export type ProgramStatus = 'draft' | 'published'

export interface ProgramLocaleContent {
  name: string
  targetAudience?: string | null
  content?: string | null
  donationContent?: string | null
}

export interface GalleryImageDto {
  id: string
  imageKey: string
  imageUrl?: string | null
  thumbUrl?: string | null
  sortOrder: number
}

export interface LinkRefDto {
  id: string
  slug: string
  title?: string | null
}

export interface ProgramListItemDto {
  id: string
  slug: string
  isShared: boolean
  status: ProgramStatus
  progress: 'ongoing' | 'completed'
  startOn?: string | null
  endOn?: string | null
  sortOrder: number
  isPinned: boolean
  charityId: string
  charityNameZh?: string | null
  coverUrl?: string | null
  coverThumbUrl?: string | null
  nameZh?: string | null
  nameEn?: string | null
  updatedAt: string
}

export interface ProgramDetailDto {
  id: string
  slug: string
  isShared: boolean
  status: ProgramStatus
  progress: 'ongoing' | 'completed'
  startOn?: string | null
  endOn?: string | null
  sortOrder: number
  isPinned: boolean
  charityId: string
  charityNameZh?: string | null
  coverKey?: string | null
  coverUrl?: string | null
  zh: ProgramLocaleContent
  en?: ProgramLocaleContent | null
  partners: LinkRefDto[]
  sponsors: LinkRefDto[]
  articles: LinkRefDto[]
  images: GalleryImageDto[]
  createdAt: string
  updatedAt: string
}

export interface SaveProgramPayload {
  slug?: string
  charityId: string
  startOn?: string | null
  endOn?: string | null
  status: ProgramStatus
  sortOrder: number
  isPinned: boolean
  content: BilingualContentInput<ProgramLocaleContent>
  partnerIds?: string[]
  sponsorIds?: string[]
  articleIds?: string[]
  removeCover?: boolean
}

export interface ProgramFilter {
  status?: string
  keyword?: string
  page?: number
  pageSize?: number
}

export function listPrograms(club: string, filter: ProgramFilter): Promise<PagedResult<ProgramListItemDto>> {
  return apiRequest<PagedResult<ProgramListItemDto>>(`${base(club)}/programs${buildQuery({ ...filter })}`)
}

export function getProgram(club: string, id: string): Promise<ProgramDetailDto> {
  return apiRequest<ProgramDetailDto>(`${base(club)}/programs/${id}`)
}

export function createProgram(club: string, payload: SaveProgramPayload, cover: File | null): Promise<ProgramDetailDto> {
  return apiUploadRequest<ProgramDetailDto>(`${base(club)}/programs`, buildMultipart(payload, { cover }), { method: 'POST' })
}

export function updateProgram(club: string, id: string, payload: SaveProgramPayload, cover: File | null): Promise<ProgramDetailDto> {
  return apiUploadRequest<ProgramDetailDto>(`${base(club)}/programs/${id}`, buildMultipart(payload, { cover }), { method: 'PUT' })
}

export function deleteProgram(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/programs/${id}`, { method: 'DELETE' })
}

export function addProgramImage(club: string, id: string, file: File): Promise<ProgramDetailDto> {
  return apiUploadRequest<ProgramDetailDto>(`${base(club)}/programs/${id}/images`, buildSingleFile(file), { method: 'POST' })
}

export function deleteProgramImage(club: string, id: string, imageId: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/programs/${id}/images/${imageId}`, { method: 'DELETE' })
}

export function reorderProgramImages(club: string, id: string, ids: string[]): Promise<ProgramDetailDto> {
  return apiRequest<ProgramDetailDto>(`${base(club)}/programs/${id}/images/order`, { method: 'PUT', body: { ids } })
}

// ───────────── 事蹟紀錄 ─────────────

export interface RecordLocaleContent {
  donationContent: string
  location?: string | null
  briefDescription?: string | null
}

export interface RecordListItemDto {
  id: string
  isShared: boolean
  charityId: string
  charityNameZh?: string | null
  charityProgramId?: string | null
  programNameZh?: string | null
  happenedOn?: string | null
  sortOrder: number
  isPinned: boolean
  imageUrl?: string | null
  imageThumbUrl?: string | null
  donationContentZh?: string | null
  locationZh?: string | null
  updatedAt: string
}

export interface RecordDetailDto {
  id: string
  isShared: boolean
  charityId: string
  charityNameZh?: string | null
  charityProgramId?: string | null
  programNameZh?: string | null
  happenedOn?: string | null
  sortOrder: number
  isPinned: boolean
  imageKey?: string | null
  imageUrl?: string | null
  imageWidth?: number | null
  imageHeight?: number | null
  zh: RecordLocaleContent
  en?: RecordLocaleContent | null
  images: GalleryImageDto[]
  createdAt: string
  updatedAt: string
}

export interface SaveRecordPayload {
  charityId: string
  charityProgramId?: string | null
  happenedOn?: string | null
  sortOrder: number
  isPinned: boolean
  content: BilingualContentInput<RecordLocaleContent>
}

export interface RecordFilter {
  charityId?: string
  programId?: string
  year?: number
  keyword?: string
  page?: number
  pageSize?: number
}

export function listRecords(club: string, filter: RecordFilter): Promise<PagedResult<RecordListItemDto>> {
  return apiRequest<PagedResult<RecordListItemDto>>(`${base(club)}/records${buildQuery({ ...filter })}`)
}

export function getRecord(club: string, id: string): Promise<RecordDetailDto> {
  return apiRequest<RecordDetailDto>(`${base(club)}/records/${id}`)
}

export function createRecord(club: string, payload: SaveRecordPayload, image: File): Promise<RecordDetailDto> {
  return apiUploadRequest<RecordDetailDto>(`${base(club)}/records`, buildMultipart(payload, { image }), { method: 'POST' })
}

export function updateRecord(club: string, id: string, payload: SaveRecordPayload, image: File | null): Promise<RecordDetailDto> {
  return apiUploadRequest<RecordDetailDto>(`${base(club)}/records/${id}`, buildMultipart(payload, { image }), { method: 'PUT' })
}

export function deleteRecord(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/records/${id}`, { method: 'DELETE' })
}

export function addRecordImage(club: string, id: string, file: File): Promise<RecordDetailDto> {
  return apiUploadRequest<RecordDetailDto>(`${base(club)}/records/${id}/images`, buildSingleFile(file), { method: 'POST' })
}

export function deleteRecordImage(club: string, id: string, imageId: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/records/${id}/images/${imageId}`, { method: 'DELETE' })
}

export function reorderRecordImages(club: string, id: string, ids: string[]): Promise<RecordDetailDto> {
  return apiRequest<RecordDetailDto>(`${base(club)}/records/${id}/images/order`, { method: 'PUT', body: { ids } })
}

// ───────────── 影響力數據 ─────────────

export interface MetricLocaleContent {
  name: string
  unit?: string | null
}

export interface MetricDto {
  id: string
  isShared: boolean
  charityProgramId?: string | null
  programNameZh?: string | null
  value?: number | null
  isPublic: boolean
  sortOrder: number
  zh: MetricLocaleContent
  en?: MetricLocaleContent | null
  updatedAt: string
}

export interface SaveMetricPayload {
  charityProgramId?: string | null
  value?: number | null
  isPublic: boolean
  sortOrder: number
  content: BilingualContentInput<MetricLocaleContent>
}

export function listMetrics(club: string, programId?: string): Promise<MetricDto[]> {
  return apiRequest<MetricDto[]>(`${base(club)}/metrics${buildQuery({ programId })}`)
}

export function createMetric(club: string, payload: SaveMetricPayload): Promise<MetricDto> {
  return apiRequest<MetricDto>(`${base(club)}/metrics`, { method: 'POST', body: payload })
}

export function updateMetric(club: string, id: string, payload: SaveMetricPayload): Promise<MetricDto> {
  return apiRequest<MetricDto>(`${base(club)}/metrics/${id}`, { method: 'PUT', body: payload })
}

export function deleteMetric(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/metrics/${id}`, { method: 'DELETE' })
}

// ───────────── 捐款導流與參與方式設定 ─────────────

export interface BilingualTextDto {
  zh?: string | null
  en?: string | null
}

export interface CharitySettingsDto {
  donationUrl?: string | null
  donationCta?: BilingualTextDto | null
  corporateCta?: BilingualTextDto | null
  corporateUrl?: string | null
  fanCta?: BilingualTextDto | null
  updatedAt?: string | null
}

export type SaveCharitySettingsPayload = Omit<CharitySettingsDto, 'updatedAt'>

export function getCharitySettings(club: string): Promise<CharitySettingsDto> {
  return apiRequest<CharitySettingsDto>(`${base(club)}/settings`)
}

export function saveCharitySettings(club: string, payload: SaveCharitySettingsPayload): Promise<CharitySettingsDto> {
  return apiRequest<CharitySettingsDto>(`${base(club)}/settings`, { method: 'PUT', body: payload })
}
