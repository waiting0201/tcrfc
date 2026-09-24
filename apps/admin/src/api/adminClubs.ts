/**
 * J4「俱樂部與法人資料」，對照 `Features/AdminClubs/AdminClubDtos.cs`。全域端點，需要系統管理員。
 * ⚠️ 標誌／favicon／OG 圖三組欄位本輪唯讀（見 apps/api/README.md「執行層判斷」第 5 點），
 * 這裡的型別因此只讀不寫這三組欄位。
 */
import { apiRequest } from './http'

export interface AdminClubLocaleContent {
  name: string
  description?: string | null
}

export interface AdminClubContentInput {
  zh: AdminClubLocaleContent
  en?: AdminClubLocaleContent | null
}

export interface AdminClubListItemDto {
  id: string
  code: string
  domain: string
  nameZh?: string | null
  nameEn?: string | null
  defaultLocale: string
  isCollectingSubject: boolean
  sortOrder: number
  status?: string | null
}

export interface AdminClubDetailDto {
  id: string
  code: string
  domain: string
  logoLightKey?: string | null
  logoDarkKey?: string | null
  faviconKey?: string | null
  ogImageKey?: string | null
  brandColor?: string | null
  brandSecondaryColor?: string | null
  invoiceTitle?: string | null
  taxId?: string | null
  isCollectingSubject: boolean
  defaultLocale: string
  sortOrder: number
  status?: string | null
  zh: AdminClubLocaleContent
  en?: AdminClubLocaleContent | null
  createdAt: string
  updatedAt: string
}

export function listAdminClubs(): Promise<AdminClubListItemDto[]> {
  return apiRequest<AdminClubListItemDto[]>('/api/v1/admin/clubs')
}

export function getAdminClub(id: string): Promise<AdminClubDetailDto> {
  return apiRequest<AdminClubDetailDto>(`/api/v1/admin/clubs/${id}`)
}

export interface CreateAdminClubPayload {
  code: string
  domain: string
  content: AdminClubContentInput
  brandColor?: string | null
  brandSecondaryColor?: string | null
  invoiceTitle?: string | null
  taxId?: string | null
  isCollectingSubject: boolean
  defaultLocale: string
  sortOrder: number
}

/** 更新不接受改 `code`（後端 `UpdateAdminClubRequest` 沒有這個欄位——代碼是行事曆訂閱網址等處的
 * 識別鍵，見 docs/14-invariants.md，建立後不可改）。 */
export interface UpdateAdminClubPayload {
  domain: string
  content: AdminClubContentInput
  brandColor?: string | null
  brandSecondaryColor?: string | null
  invoiceTitle?: string | null
  taxId?: string | null
  isCollectingSubject: boolean
  defaultLocale: string
  sortOrder: number
  status?: string | null
}

export function createAdminClub(payload: CreateAdminClubPayload): Promise<AdminClubDetailDto> {
  return apiRequest<AdminClubDetailDto>('/api/v1/admin/clubs', { method: 'POST', body: payload })
}

export function updateAdminClub(id: string, payload: UpdateAdminClubPayload): Promise<AdminClubDetailDto> {
  return apiRequest<AdminClubDetailDto>(`/api/v1/admin/clubs/${id}`, { method: 'PUT', body: payload })
}
