/**
 * K4 特約店家與權益對照表後台端點，對照 apps/api/README.md「B1」節「K4 特約店家與權益」。
 */
import {
  apiRequest,
  apiUploadRequest,
  buildMultipart,
  buildQuery,
  putOrder,
  type BilingualContentInput,
} from './adminCommon'

const club$ = (club: string) => `/api/v1/admin/${club}`

export type StoreStatus = 'published' | 'draft'
export type StoreTier = 'all' | 'fan_club'

export interface PartnerStoreListItemDto {
  id: string
  slug: string
  /** 兩隊共同的店家：所有俱樂部看得到，只有系統管理員能編輯。 */
  isShared: boolean
  category?: string | null
  region?: string | null
  address?: string | null
  lat?: number | null
  lng?: number | null
  phone?: string | null
  applicableTier: StoreTier
  applicableTierLabel?: string | null
  startOn?: string | null
  endOn?: string | null
  isActive: boolean
  sortOrder: number
  status: StoreStatus
  statusLabel?: string | null
  imageKey?: string | null
  imageUrl?: string | null
  imageThumbUrl?: string | null
  nameZh?: string | null
  nameEn?: string | null
  offerZh?: string | null
  updatedAt: string
}

export interface StoreLocaleContent {
  name: string
  address?: string | null
  offerContent?: string | null
}

export interface PartnerStoreDetailDto extends PartnerStoreListItemDto {
  businessHours?: string | null
  mapUrl?: string | null
  websiteUrl?: string | null
  zh: StoreLocaleContent
  en?: StoreLocaleContent | null
  createdAt: string
}

export interface PartnerStoreFiltersDto {
  categories: string[]
  regions: string[]
}

export interface SavePartnerStorePayload {
  slug?: string
  category?: string | null
  region?: string | null
  lat?: number | null
  lng?: number | null
  phone?: string | null
  businessHours?: string | null
  mapUrl?: string | null
  websiteUrl?: string | null
  applicableTier: StoreTier
  startOn?: string | null
  endOn?: string | null
  sortOrder: number
  status: StoreStatus
  isShared?: boolean
  removeImage?: boolean
  content: BilingualContentInput<StoreLocaleContent>
}

const stores = (club: string) => `${club$(club)}/partner-stores`

export function listPartnerStores(
  club: string,
  params: { category?: string; region?: string; status?: string; tier?: string; keyword?: string } = {},
): Promise<PartnerStoreListItemDto[]> {
  return apiRequest<PartnerStoreListItemDto[]>(`${stores(club)}${buildQuery(params)}`)
}

export function getPartnerStoreFilters(club: string): Promise<PartnerStoreFiltersDto> {
  return apiRequest<PartnerStoreFiltersDto>(`${stores(club)}/filters`)
}

export function getPartnerStore(club: string, id: string): Promise<PartnerStoreDetailDto> {
  return apiRequest<PartnerStoreDetailDto>(`${stores(club)}/${id}`)
}

export function createPartnerStore(club: string, payload: SavePartnerStorePayload, image: File | null): Promise<PartnerStoreDetailDto> {
  return apiUploadRequest<PartnerStoreDetailDto>(stores(club), buildMultipart(payload, { image }), { method: 'POST' })
}

export function updatePartnerStore(club: string, id: string, payload: SavePartnerStorePayload, image: File | null): Promise<PartnerStoreDetailDto> {
  return apiUploadRequest<PartnerStoreDetailDto>(`${stores(club)}/${id}`, buildMultipart(payload, { image }), { method: 'PUT' })
}

export function deletePartnerStore(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${stores(club)}/${id}`, { method: 'DELETE' })
}

/** 排序只送本俱樂部的店家 id；共同店家不參與（含共同店家後端回 400）。 */
export function reorderPartnerStores(club: string, ids: string[]): Promise<void> {
  return putOrder(`${stores(club)}/order`, ids)
}

// ── 權益對照表 ────────────────────────────────────────

export type BenefitGroup = 'member_card' | 'store_discount' | 'jersey' | 'event'

export interface MembershipBenefitListItemDto {
  id: string
  planId: string
  planCode: string
  planName?: string | null
  seasonCode: string
  group: BenefitGroup
  groupLabel: string
  sortOrder: number
  status: StoreStatus
  statusLabel?: string | null
  nameZh?: string | null
  nameEn?: string | null
  freeValueZh?: string | null
  paidValueZh?: string | null
  updatedAt: string
}

export interface BenefitLocaleContent {
  name: string
  description?: string | null
  freeValue?: string | null
  paidValue?: string | null
}

export interface MembershipBenefitDetailDto extends MembershipBenefitListItemDto {
  zh: BenefitLocaleContent
  en?: BenefitLocaleContent | null
}

export interface BenefitGroupOptionDto {
  code: BenefitGroup
  label: string
}

export interface SaveBenefitPayload {
  planId: string
  group: BenefitGroup
  sortOrder?: number
  status: StoreStatus
  content: BilingualContentInput<BenefitLocaleContent>
}

const benefits = (club: string) => `${club$(club)}/membership-benefits`

export function listMembershipBenefits(club: string, params: { planId?: string; group?: string; status?: string } = {}): Promise<MembershipBenefitListItemDto[]> {
  return apiRequest<MembershipBenefitListItemDto[]>(`${benefits(club)}${buildQuery(params)}`)
}

export function listBenefitGroups(club: string): Promise<BenefitGroupOptionDto[]> {
  return apiRequest<BenefitGroupOptionDto[]>(`${benefits(club)}/groups`)
}

export function getMembershipBenefit(club: string, id: string): Promise<MembershipBenefitDetailDto> {
  return apiRequest<MembershipBenefitDetailDto>(`${benefits(club)}/${id}`)
}

export function createMembershipBenefit(club: string, payload: SaveBenefitPayload): Promise<MembershipBenefitDetailDto> {
  return apiRequest<MembershipBenefitDetailDto>(benefits(club), { method: 'POST', body: payload })
}

export function updateMembershipBenefit(club: string, id: string, payload: SaveBenefitPayload): Promise<MembershipBenefitDetailDto> {
  return apiRequest<MembershipBenefitDetailDto>(`${benefits(club)}/${id}`, { method: 'PUT', body: payload })
}

export function deleteMembershipBenefit(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${benefits(club)}/${id}`, { method: 'DELETE' })
}

/** 同一方案內重排：`{ planId, ids }`。 */
export function reorderMembershipBenefits(club: string, planId: string, ids: string[]): Promise<void> {
  return apiRequest<void>(`${benefits(club)}/order`, { method: 'PUT', body: { planId, ids } })
}
