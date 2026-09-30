/**
 * K3 球衣發放（apps/api/README.md「B1」節「K3 球衣發放」）。
 * 收件資訊（領用人／電話／地址）依「檢視完整個資」權限遮罩；沒有權限的人也不能修改這三欄（後端 403）。
 */
import { apiRequest, buildQuery, downloadExport, postBatch, type BatchResultDto, type PagedResult } from './adminCommon'
import type { JerseyStatusCode } from './adminMembers'

export type JerseyDeliveryMethod = 'ship' | 'pickup'

export const JERSEY_DELIVERY_OPTIONS: { value: JerseyDeliveryMethod; label: string }[] = [
  { value: 'ship', label: '寄送' },
  { value: 'pickup', label: '到場領取' },
]

/** 常見尺寸（後端會自動轉大寫，也接受其他尺寸字樣）。 */
export const JERSEY_SIZE_OPTIONS = ['XS', 'S', 'M', 'L', 'XL', '2XL', '3XL', '4XL']

export interface JerseyDto {
  id: string
  memberId: string
  memberNo: string | null
  membershipId: string | null
  recipientName: string | null
  phone: string | null
  size: string | null
  deliveryMethod: JerseyDeliveryMethod
  deliveryMethodLabel: string
  address: string | null
  status: JerseyStatusCode
  statusLabel: string
  shippedOn: string | null
  receivedOn: string | null
  createdAt: string
  updatedAt: string
  isMasked: boolean
}

export interface JerseyFilter {
  status?: string
  size?: string
  deliveryMethod?: string
  keyword?: string
}

export interface JerseySizeSummaryDto {
  size: string | null
  sizeLabel: string
  total: number
  ship: number
  pickup: number
}

const base = (club: string) => `/api/v1/admin/${club}/jerseys`

export function listJerseys(club: string, filter: JerseyFilter, page: number, pageSize: number): Promise<PagedResult<JerseyDto>> {
  return apiRequest<PagedResult<JerseyDto>>(`${base(club)}${buildQuery({ ...filter, page, pageSize })}`)
}

export function getJerseySizeSummary(club: string, status: string): Promise<JerseySizeSummaryDto[]> {
  return apiRequest<JerseySizeSummaryDto[]>(`${base(club)}/size-summary${buildQuery({ status })}`)
}

export interface CreateJerseyPayload {
  membershipId: string
  recipientName: string
  phone?: string | null
  size: string
  deliveryMethod: JerseyDeliveryMethod
  address?: string | null
}

export function createJersey(club: string, payload: CreateJerseyPayload): Promise<JerseyDto> {
  return apiRequest<JerseyDto>(base(club), { method: 'POST', body: payload })
}

/** 只送有帶的欄位（省略＝不變）。 */
export interface UpdateJerseyPayload {
  recipientName?: string
  phone?: string
  address?: string
  size?: string
  deliveryMethod?: JerseyDeliveryMethod
  status?: JerseyStatusCode
}

export function updateJersey(club: string, id: string, payload: UpdateJerseyPayload): Promise<JerseyDto> {
  return apiRequest<JerseyDto>(`${base(club)}/${id}`, { method: 'PUT', body: payload })
}

export function batchJerseyStatus(club: string, ids: string[], status: JerseyStatusCode): Promise<BatchResultDto> {
  return postBatch(`${base(club)}/batch/status`, { ids, status })
}

export function exportJerseys(club: string, status: string, purpose: string): Promise<void> {
  return downloadExport(`${base(club)}/export${buildQuery({ status, purpose })}`, '球衣出貨清單.csv')
}

/** 代填時找會籍用的精簡查詢（K2 會籍清單端點；只宣告本畫面用到的欄位）。 */
export interface MembershipLookupDto {
  membershipId: string
  memberId: string
  memberNo: string
  memberName: string | null
  effectiveStatusLabel: string
  seasonCode: string
  planName: string | null
}

export function lookupMemberships(club: string, keyword: string): Promise<PagedResult<MembershipLookupDto>> {
  return apiRequest<PagedResult<MembershipLookupDto>>(`/api/v1/admin/${club}/memberships${buildQuery({ keyword, page: 1, pageSize: 20 })}`)
}
