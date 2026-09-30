/**
 * E1 夥伴後台端點，對照 apps/api/README.md「E1a」節「E1 夥伴」。
 */
import { apiRequest, apiUploadRequest, buildMultipart, buildQuery, putOrder, type BilingualContentInput } from './adminCommon'

export interface PartnerLocaleContent {
  name: string
  content?: string | null
}

export interface PartnerListItemDto {
  id: string
  slug: string
  partnerType?: string | null
  country?: string | null
  startOn?: string | null
  endOn?: string | null
  websiteUrl?: string | null
  showInFooter: boolean
  showOnHome: boolean
  sortOrder: number
  logoDarkUrl?: string | null
  logoDarkThumbUrl?: string | null
  logoLightUrl?: string | null
  logoLightThumbUrl?: string | null
  nameZh?: string | null
  nameEn?: string | null
  isActive: boolean
  updatedAt: string
}

export interface PartnerDetailDto {
  id: string
  slug: string
  partnerType?: string | null
  country?: string | null
  startOn?: string | null
  endOn?: string | null
  websiteUrl?: string | null
  showInFooter: boolean
  showOnHome: boolean
  sortOrder: number
  logoDarkKey?: string | null
  logoDarkUrl?: string | null
  logoLightKey?: string | null
  logoLightUrl?: string | null
  zh: PartnerLocaleContent
  en?: PartnerLocaleContent | null
  createdAt: string
  updatedAt: string
}

export interface PartnerTypesDto {
  standardTypes: string[]
  usedTypes: string[]
}

export interface SavePartnerPayload {
  slug?: string
  partnerType: string
  country?: string | null
  startOn?: string | null
  endOn?: string | null
  websiteUrl?: string | null
  showInFooter: boolean
  showOnHome: boolean
  sortOrder: number
  content: BilingualContentInput<PartnerLocaleContent>
  removeLogoDark?: boolean
  removeLogoLight?: boolean
}

export interface PartnerFiles {
  logoDark: File | null
  logoLight: File | null
}

const base = (club: string) => `/api/v1/admin/${club}/partners`

export function listPartners(club: string, params: { partnerType?: string; keyword?: string } = {}): Promise<PartnerListItemDto[]> {
  return apiRequest<PartnerListItemDto[]>(`${base(club)}${buildQuery(params)}`)
}

export function listPartnerTypes(club: string): Promise<PartnerTypesDto> {
  return apiRequest<PartnerTypesDto>(`${base(club)}/types`)
}

export function getPartner(club: string, id: string): Promise<PartnerDetailDto> {
  return apiRequest<PartnerDetailDto>(`${base(club)}/${id}`)
}

export function createPartner(club: string, payload: SavePartnerPayload, files: PartnerFiles): Promise<PartnerDetailDto> {
  return apiUploadRequest<PartnerDetailDto>(base(club), buildMultipart(payload, { ...files }), { method: 'POST' })
}

export function updatePartner(club: string, id: string, payload: SavePartnerPayload, files: PartnerFiles): Promise<PartnerDetailDto> {
  return apiUploadRequest<PartnerDetailDto>(`${base(club)}/${id}`, buildMultipart(payload, { ...files }), { method: 'PUT' })
}

export function deletePartner(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}`, { method: 'DELETE' })
}

export function reorderPartners(club: string, ids: string[]): Promise<void> {
  return putOrder(`${base(club)}/order`, ids)
}
