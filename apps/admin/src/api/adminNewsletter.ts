/**
 * G3 電子報訂閱名單後台端點，對照 apps/api/README.md「D 批」節「G3 電子報」。
 * 名單依俱樂部分開（同一個人可以只退訂其中一站）；沒有公開訂閱／退訂端點、不做群發。
 */
import { apiRequest, buildQuery, downloadExport, type PagedResult } from './adminCommon'

const base = (club: string) => `/api/v1/admin/${club}/newsletter`

export type SubscriberStatus = 'subscribed' | 'unsubscribed'

export interface SubscriberDto {
  id: string
  email: string
  source: string | null
  sourceLabel: string
  status: SubscriberStatus
  statusLabel: string
  subscribedAt: string | null
  unsubscribedAt: string | null
  updatedAt: string
}

export interface NewsletterSummaryDto {
  subscribedCount: number
  unsubscribedCount: number
  sources: { source: string | null; sourceLabel: string; count: number }[]
}

export interface EdmStatusDto {
  configured: boolean
  provider: string | null
  message: string
}

export interface EdmSyncResultDto {
  configured: boolean
  subscribedCount: number
  unsubscribedCount: number
  syncedCount: number
  message: string
}

export interface SubscriberFilters {
  status?: string
  source?: string
  keyword?: string
}

export function listSubscribers(club: string, params: SubscriberFilters & { page: number; pageSize: number }) {
  return apiRequest<PagedResult<SubscriberDto>>(`${base(club)}/subscribers${buildQuery({ ...params })}`)
}

export function getNewsletterSummary(club: string) {
  return apiRequest<NewsletterSummaryDto>(`${base(club)}/summary`)
}

export function addSubscriber(club: string, body: { email: string; source?: string }) {
  return apiRequest<SubscriberDto>(`${base(club)}/subscribers`, { method: 'POST', body })
}

export function changeSubscriberStatus(club: string, id: string, body: { status: SubscriberStatus; reason?: string }) {
  return apiRequest<SubscriberDto>(`${base(club)}/subscribers/${id}/status`, { method: 'PUT', body })
}

export function deleteSubscriber(club: string, id: string) {
  return apiRequest<void>(`${base(club)}/subscribers/${id}`, { method: 'DELETE' })
}

export function exportSubscribers(club: string, params: SubscriberFilters & { purpose: string }) {
  return downloadExport(`${base(club)}/export${buildQuery({ ...params })}`, '電子報訂閱名單.csv')
}

export function getEdmStatus(club: string) {
  return apiRequest<EdmStatusDto>(`${base(club)}/edm`)
}

export function syncEdm(club: string) {
  return apiRequest<EdmSyncResultDto>(`${base(club)}/edm/sync`, { method: 'POST' })
}
