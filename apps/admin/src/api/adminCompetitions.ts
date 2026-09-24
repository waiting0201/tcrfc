/**
 * 俱樂部範圍的「賽事系列」（Competition）維護，對照 `Features/AdminCompetitions/AdminCompetitionDtos.cs`。
 * 權限碼歸在 `team.competition.*`（module=C／submodule=C4），需要俱樂部授權（不是系統管理員限定）。
 */
import { apiRequest } from './http'

export interface AdminCompetitionLocaleContent {
  name: string
  organizer?: string | null
}

export interface AdminCompetitionContentInput {
  zh: AdminCompetitionLocaleContent
  en?: AdminCompetitionLocaleContent | null
}

export interface AdminCompetitionListItemDto {
  id: string
  seasonId: string
  seasonCode: string
  code: string
  compType?: string | null
  sortOrder: number
  status: 'draft' | 'published'
  nameZh?: string | null
  nameEn?: string | null
  updatedAt: string
}

export interface AdminCompetitionDetailDto {
  id: string
  seasonId: string
  seasonCode: string
  code: string
  compType?: string | null
  sortOrder: number
  status: 'draft' | 'published'
  zh: AdminCompetitionLocaleContent
  en?: AdminCompetitionLocaleContent | null
  createdAt: string
  updatedAt: string
}

export function listAdminCompetitions(club: string, seasonId?: string, status?: string): Promise<AdminCompetitionListItemDto[]> {
  const search = new URLSearchParams()
  if (seasonId) search.set('seasonId', seasonId)
  if (status) search.set('status', status)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminCompetitionListItemDto[]>(`/api/v1/admin/${club}/competitions${query}`)
}

export function getAdminCompetition(club: string, id: string): Promise<AdminCompetitionDetailDto> {
  return apiRequest<AdminCompetitionDetailDto>(`/api/v1/admin/${club}/competitions/${id}`)
}

export interface SaveCompetitionPayload {
  seasonId: string
  code: string
  compType?: string | null
  sortOrder: number
  status: 'draft' | 'published'
  content: AdminCompetitionContentInput
}

export function createAdminCompetition(club: string, payload: SaveCompetitionPayload): Promise<AdminCompetitionDetailDto> {
  return apiRequest<AdminCompetitionDetailDto>(`/api/v1/admin/${club}/competitions`, { method: 'POST', body: payload })
}

export function updateAdminCompetition(club: string, id: string, payload: SaveCompetitionPayload): Promise<AdminCompetitionDetailDto> {
  return apiRequest<AdminCompetitionDetailDto>(`/api/v1/admin/${club}/competitions/${id}`, { method: 'PUT', body: payload })
}

/**
 * `GET /admin/{club}/seasons`（S1-4 續作補上的端點，見 apps/api/README.md「前端回報缺口②之一」）：
 * 賽事系列表單的球季下拉選單，取代先前「沒有清單、只能自己貼識別碼」的暫時作法。
 * 權限碼比照既有的 `team.competition.view`，不是新的權限碼。
 */
export interface AdminSeasonListItemDto {
  id: string
  code: string
  startOn: string
  endOn: string
}

export function listAdminSeasons(club: string): Promise<AdminSeasonListItemDto[]> {
  return apiRequest<AdminSeasonListItemDto[]>(`/api/v1/admin/${club}/seasons`)
}
