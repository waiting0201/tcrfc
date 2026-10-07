/**
 * B6 媒體專區後台端點，對照 apps/api/README.md「E1a」節「B6」。
 */
import {
  apiRequest,
  apiUploadRequest,
  buildMultipart,
  buildQuery,
  postBatch,
  type BatchResultDto,
  type BilingualContentInput,
  type PagedResult,
} from './adminCommon'

export type PressResourceType = 'press_release' | 'brand_kit' | 'hires_image'
export type PressStatus = 'draft' | 'published'

export interface PressLocaleContent {
  title: string
  description?: string | null
  /** 封面圖片替代文字（選填，上限 200 字；錯誤鍵 coverAltZh／coverAltEn） */
  coverAlt?: string | null
}

export interface PressListItemDto {
  id: string
  slug: string
  isShared: boolean
  resourceType: PressResourceType
  status: PressStatus
  publishedOn?: string | null
  sortOrder: number
  downloadCount: number
  fileBytes?: number | null
  coverThumbUrl?: string | null
  titleZh?: string | null
  titleEn?: string | null
  updatedAt: string
}

export interface PressDetailDto {
  id: string
  slug: string
  isShared: boolean
  resourceType: PressResourceType
  status: PressStatus
  publishedOn?: string | null
  sortOrder: number
  downloadCount: number
  fileKey: string
  fileUrl?: string | null
  fileBytes?: number | null
  coverKey?: string | null
  coverUrl?: string | null
  coverWidth?: number | null
  coverHeight?: number | null
  zh: PressLocaleContent
  en?: PressLocaleContent | null
  createdAt: string
  updatedAt: string
}

export interface SavePressPayload {
  slug?: string
  resourceType: PressResourceType
  status: PressStatus
  publishedOn?: string | null
  sortOrder: number
  content: BilingualContentInput<PressLocaleContent>
  removeCover?: boolean
}

export interface PressFilter {
  resourceType?: string
  status?: string
  keyword?: string
  page?: number
  pageSize?: number
}

const base = (club: string) => `/api/v1/admin/${club}/press-resources`

export function listPress(club: string, filter: PressFilter): Promise<PagedResult<PressListItemDto>> {
  return apiRequest<PagedResult<PressListItemDto>>(`${base(club)}${buildQuery({ ...filter })}`)
}

export function getPress(club: string, id: string): Promise<PressDetailDto> {
  return apiRequest<PressDetailDto>(`${base(club)}/${id}`)
}

export function createPress(club: string, payload: SavePressPayload, file: File, cover: File | null): Promise<PressDetailDto> {
  return apiUploadRequest<PressDetailDto>(base(club), buildMultipart(payload, { file, cover }), { method: 'POST' })
}

export function updatePress(
  club: string,
  id: string,
  payload: SavePressPayload,
  file: File | null,
  cover: File | null,
): Promise<PressDetailDto> {
  return apiUploadRequest<PressDetailDto>(`${base(club)}/${id}`, buildMultipart(payload, { file, cover }), { method: 'PUT' })
}

export function deletePress(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/${id}`, { method: 'DELETE' })
}

export function batchShowPress(club: string, ids: string[]): Promise<BatchResultDto> {
  return postBatch(`${base(club)}/batch/show`, { ids })
}

export function batchHidePress(club: string, ids: string[]): Promise<BatchResultDto> {
  return postBatch(`${base(club)}/batch/hide`, { ids })
}

export function batchChangePressType(club: string, ids: string[], resourceType: PressResourceType): Promise<BatchResultDto> {
  return postBatch(`${base(club)}/batch/type`, { ids, resourceType })
}
