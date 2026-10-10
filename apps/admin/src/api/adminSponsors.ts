/**
 * E2 贊助商／贊助方案／贊助活動後台端點，對照 apps/api/README.md「E1a」節「E2」。
 */
import {
  apiRequest,
  apiUploadRequest,
  buildMultipart,
  buildQuery,
  buildSingleFile,
  putOrder,
  type BilingualContentInput,
} from './adminCommon'

// ───────────── 贊助商 ─────────────

export type SponsorContractStatus = 'none' | 'active' | 'alert' | 'expired'

export interface SponsorLocaleContent {
  name: string
  content?: string | null
  /** 圖片說明（替代文字，≤200 字）。 */
  logoAlt?: string | null
}

export interface SponsorListItemDto {
  id: string
  slug: string
  tier?: string | null
  contractStartOn?: string | null
  contractEndOn?: string | null
  expiryAlertOn?: string | null
  contractStatus: SponsorContractStatus
  contactName?: string | null
  contactPhone?: string | null
  contactEmail?: string | null
  sortOrder: number
  logoDarkThumbUrl?: string | null
  logoLightThumbUrl?: string | null
  logoDarkWidth?: number | null
  logoDarkHeight?: number | null
  logoLightWidth?: number | null
  logoLightHeight?: number | null
  nameZh?: string | null
  nameEn?: string | null
  packageCount: number
  activationCount: number
  updatedAt: string
}

export interface SponsorDetailDto {
  id: string
  slug: string
  tier?: string | null
  contractStartOn?: string | null
  contractEndOn?: string | null
  expiryAlertOn?: string | null
  contractStatus: SponsorContractStatus
  contactName?: string | null
  contactPhone?: string | null
  contactEmail?: string | null
  sortOrder: number
  logoDarkKey?: string | null
  logoDarkUrl?: string | null
  logoLightKey?: string | null
  logoLightUrl?: string | null
  logoDarkWidth?: number | null
  logoDarkHeight?: number | null
  logoLightWidth?: number | null
  logoLightHeight?: number | null
  zh: SponsorLocaleContent
  en?: SponsorLocaleContent | null
  packages: { id: string; slug: string; nameZh?: string | null }[]
  articles: { id: string; slug: string; titleZh?: string | null; status: string }[]
  createdAt: string
  updatedAt: string
}

export interface SaveSponsorPayload {
  slug?: string
  tier: string
  contractStartOn?: string | null
  contractEndOn?: string | null
  expiryAlertOn?: string | null
  contactName?: string | null
  contactPhone?: string | null
  contactEmail?: string | null
  sortOrder: number
  content: BilingualContentInput<SponsorLocaleContent>
  packageIds?: string[]
  articleIds?: string[]
  removeLogoDark?: boolean
  removeLogoLight?: boolean
}

export interface SponsorFiles {
  logoDark: File | null
  logoLight: File | null
}

const sponsorBase = (club: string) => `/api/v1/admin/${club}/sponsors`

export function listSponsors(
  club: string,
  params: { tier?: string; contractStatus?: string; keyword?: string } = {},
): Promise<SponsorListItemDto[]> {
  return apiRequest<SponsorListItemDto[]>(`${sponsorBase(club)}${buildQuery(params)}`)
}

export function getSponsor(club: string, id: string): Promise<SponsorDetailDto> {
  return apiRequest<SponsorDetailDto>(`${sponsorBase(club)}/${id}`)
}

export function createSponsor(club: string, payload: SaveSponsorPayload, files: SponsorFiles): Promise<SponsorDetailDto> {
  return apiUploadRequest<SponsorDetailDto>(sponsorBase(club), buildMultipart(payload, { ...files }), { method: 'POST' })
}

export function updateSponsor(club: string, id: string, payload: SaveSponsorPayload, files: SponsorFiles): Promise<SponsorDetailDto> {
  return apiUploadRequest<SponsorDetailDto>(`${sponsorBase(club)}/${id}`, buildMultipart(payload, { ...files }), { method: 'PUT' })
}

export function deleteSponsor(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${sponsorBase(club)}/${id}`, { method: 'DELETE' })
}

export function reorderSponsors(club: string, ids: string[]): Promise<void> {
  return putOrder(`${sponsorBase(club)}/order`, ids)
}

// ───────────── 贊助方案 ─────────────

export interface PackageLocaleContent {
  name: string
  content?: string | null
  benefitList?: string | null
  audience?: string | null
}

export interface SponsorPackageListItemDto {
  id: string
  slug: string
  priceMin?: number | null
  priceMax?: number | null
  isPricePublic: boolean
  sortOrder: number
  status: 'draft' | 'published'
  nameZh?: string | null
  nameEn?: string | null
  sponsorCount: number
  updatedAt: string
}

export interface SponsorPackageDetailDto {
  id: string
  slug: string
  priceMin?: number | null
  priceMax?: number | null
  isPricePublic: boolean
  sortOrder: number
  status: 'draft' | 'published'
  zh: PackageLocaleContent
  en?: PackageLocaleContent | null
  createdAt: string
  updatedAt: string
}

export interface SaveSponsorPackagePayload {
  slug?: string
  priceMin?: number | null
  priceMax?: number | null
  isPricePublic: boolean
  sortOrder: number
  status: 'draft' | 'published'
  content: BilingualContentInput<PackageLocaleContent>
}

const packageBase = (club: string) => `/api/v1/admin/${club}/sponsor-packages`

export function listSponsorPackages(club: string, params: { status?: string } = {}): Promise<SponsorPackageListItemDto[]> {
  return apiRequest<SponsorPackageListItemDto[]>(`${packageBase(club)}${buildQuery(params)}`)
}

export function getSponsorPackage(club: string, id: string): Promise<SponsorPackageDetailDto> {
  return apiRequest<SponsorPackageDetailDto>(`${packageBase(club)}/${id}`)
}

export function createSponsorPackage(club: string, payload: SaveSponsorPackagePayload): Promise<SponsorPackageDetailDto> {
  return apiRequest<SponsorPackageDetailDto>(packageBase(club), { method: 'POST', body: payload })
}

export function updateSponsorPackage(club: string, id: string, payload: SaveSponsorPackagePayload): Promise<SponsorPackageDetailDto> {
  return apiRequest<SponsorPackageDetailDto>(`${packageBase(club)}/${id}`, { method: 'PUT', body: payload })
}

export function deleteSponsorPackage(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${packageBase(club)}/${id}`, { method: 'DELETE' })
}

export function reorderSponsorPackages(club: string, ids: string[]): Promise<void> {
  return putOrder(`${packageBase(club)}/order`, ids)
}

// ───────────── 贊助活動（含圖集） ─────────────

export interface ActivationLocaleContent {
  title: string
  resultSummary?: string | null
}

export interface ActivationImageDto {
  id: string
  imageKey: string
  imageUrl?: string | null
  thumbUrl?: string | null
  imageWidth?: number | null
  imageHeight?: number | null
  altZh?: string | null
  altEn?: string | null
  sortOrder: number
}

export interface ActivationDto {
  id: string
  sponsorId: string
  happenedOn?: string | null
  sortOrder: number
  zh: ActivationLocaleContent
  en?: ActivationLocaleContent | null
  images: ActivationImageDto[]
  updatedAt: string
}

export interface SaveActivationPayload {
  happenedOn?: string | null
  sortOrder: number
  content: BilingualContentInput<ActivationLocaleContent>
}

const activationBase = (club: string, sponsorId: string) => `${sponsorBase(club)}/${sponsorId}/activations`

export function listActivations(club: string, sponsorId: string): Promise<ActivationDto[]> {
  return apiRequest<ActivationDto[]>(activationBase(club, sponsorId))
}

export function createActivation(club: string, sponsorId: string, payload: SaveActivationPayload): Promise<ActivationDto> {
  return apiRequest<ActivationDto>(activationBase(club, sponsorId), { method: 'POST', body: payload })
}

export function updateActivation(club: string, sponsorId: string, id: string, payload: SaveActivationPayload): Promise<ActivationDto> {
  return apiRequest<ActivationDto>(`${activationBase(club, sponsorId)}/${id}`, { method: 'PUT', body: payload })
}

export function deleteActivation(club: string, sponsorId: string, id: string): Promise<void> {
  return apiRequest<void>(`${activationBase(club, sponsorId)}/${id}`, { method: 'DELETE' })
}

export function addActivationImage(club: string, sponsorId: string, id: string, file: File): Promise<ActivationDto> {
  return apiUploadRequest<ActivationDto>(`${activationBase(club, sponsorId)}/${id}/images`, buildSingleFile(file), { method: 'POST' })
}

export function deleteActivationImage(club: string, sponsorId: string, id: string, imageId: string): Promise<void> {
  return apiRequest<void>(`${activationBase(club, sponsorId)}/${id}/images/${imageId}`, { method: 'DELETE' })
}

/** 活動圖集圖片說明（替代文字），各 ≤200 字、空白清為無；回傳更新後的活動。 */
export function updateActivationImageAlt(club: string, sponsorId: string, id: string, imageId: string, altZh: string | null, altEn: string | null): Promise<ActivationDto> {
  return apiRequest<ActivationDto>(`${activationBase(club, sponsorId)}/${id}/images/${imageId}`, { method: 'PUT', body: { altZh, altEn } })
}
export function reorderActivationImages(club: string, sponsorId: string, id: string, ids: string[]): Promise<ActivationDto> {
  return apiRequest<ActivationDto>(`${activationBase(club, sponsorId)}/${id}/images/order`, { method: 'PUT', body: { ids } })
}
