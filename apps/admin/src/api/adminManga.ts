/**
 * F1 漫畫後台端點，對照 apps/api/README.md「C1」節「F1 漫畫」。
 * 🔴 台中藍鯨不設漫畫：這組端點在藍鯨站台一律回 403「此功能不適用」，畫面要先擋下不呼叫。
 */
import { apiRequest, apiUploadRequest, buildMultipart, buildQuery, putOrder, type BilingualContentInput } from './adminCommon'

const base = (club: string) => `/api/v1/admin/${club}/comic`

// ── 企劃介紹 ──
export interface MangaAboutLocale {
  title: string
  body: string
}
export interface MangaAboutDto {
  zh: MangaAboutLocale
  en?: MangaAboutLocale | null
  updatedAt?: string | null
}
export function getMangaAbout(club: string): Promise<MangaAboutDto> {
  return apiRequest<MangaAboutDto>(`${base(club)}/about`)
}
export function saveMangaAbout(club: string, body: { zh: MangaAboutLocale; en?: MangaAboutLocale }): Promise<MangaAboutDto> {
  return apiRequest<MangaAboutDto>(`${base(club)}/about`, { method: 'PUT', body })
}

// ── 角色 ──
export interface MangaCharacterDto {
  id: string
  playerId?: string | null
  playerName?: string | null
  imageKey?: string | null
  imageUrl?: string | null
  imageThumbUrl?: string | null
  imageWidth?: number | null
  imageHeight?: number | null
  sortOrder: number
  zh: { name: string; description?: string | null; imageAlt?: string | null }
  en?: { name: string; description?: string | null; imageAlt?: string | null } | null
  updatedAt: string
}
export interface SaveMangaCharacterPayload {
  playerId?: string | null
  sortOrder?: number
  removeImage?: boolean
  content: BilingualContentInput<{ name: string; description?: string | null; imageAlt?: string | null }>
}
export function listMangaCharacters(club: string): Promise<MangaCharacterDto[]> {
  return apiRequest<MangaCharacterDto[]>(`${base(club)}/characters`)
}
export function createMangaCharacter(club: string, payload: SaveMangaCharacterPayload, image: File | null): Promise<MangaCharacterDto> {
  return apiUploadRequest<MangaCharacterDto>(`${base(club)}/characters`, buildMultipart(payload, { image }), { method: 'POST' })
}
export function updateMangaCharacter(club: string, id: string, payload: SaveMangaCharacterPayload, image: File | null): Promise<MangaCharacterDto> {
  return apiUploadRequest<MangaCharacterDto>(`${base(club)}/characters/${id}`, buildMultipart(payload, { image }), { method: 'PUT' })
}
export function deleteMangaCharacter(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/characters/${id}`, { method: 'DELETE' })
}
export function reorderMangaCharacters(club: string, ids: string[]): Promise<void> {
  return putOrder(`${base(club)}/characters/order`, ids)
}

// ── 集數 ──
export type MangaEpisodeStatus = 'draft' | 'published'
export interface MangaEpisodeListItemDto {
  id: string
  episodeNo: number
  coverKey?: string | null
  coverUrl?: string | null
  coverThumbUrl?: string | null
  coverWidth?: number | null
  coverHeight?: number | null
  publishedOn?: string | null
  status: MangaEpisodeStatus
  statusLabel: string
  isLatest: boolean
  viewCount: number
  pageCount: number
  titleZh?: string | null
  titleEn?: string | null
  updatedAt: string
}
export interface MangaPageDto {
  id: string
  imageKey?: string | null
  imageUrl?: string | null
  imageThumbUrl?: string | null
  imageWidth?: number | null
  imageHeight?: number | null
  altZh?: string | null
  altEn?: string | null
  sortOrder: number
}
export interface MangaEpisodeDetailDto extends MangaEpisodeListItemDto {
  zh: { title: string; coverAlt?: string | null }
  en?: { title: string; coverAlt?: string | null } | null
  pages: MangaPageDto[]
  createdAt: string
}
export interface SaveMangaEpisodePayload {
  episodeNo: number
  publishedOn?: string | null
  status: MangaEpisodeStatus
  removeCover?: boolean
  content: BilingualContentInput<{ title: string; coverAlt?: string | null }>
}
export function listMangaEpisodes(club: string, params: { status?: string } = {}): Promise<MangaEpisodeListItemDto[]> {
  return apiRequest<MangaEpisodeListItemDto[]>(`${base(club)}/episodes${buildQuery(params)}`)
}
export function getMangaEpisode(club: string, id: string): Promise<MangaEpisodeDetailDto> {
  return apiRequest<MangaEpisodeDetailDto>(`${base(club)}/episodes/${id}`)
}
export function createMangaEpisode(club: string, payload: SaveMangaEpisodePayload, cover: File | null): Promise<MangaEpisodeDetailDto> {
  return apiUploadRequest<MangaEpisodeDetailDto>(`${base(club)}/episodes`, buildMultipart(payload, { cover }), { method: 'POST' })
}
export function updateMangaEpisode(club: string, id: string, payload: SaveMangaEpisodePayload, cover: File | null): Promise<MangaEpisodeDetailDto> {
  return apiUploadRequest<MangaEpisodeDetailDto>(`${base(club)}/episodes/${id}`, buildMultipart(payload, { cover }), { method: 'PUT' })
}
export function deleteMangaEpisode(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/episodes/${id}`, { method: 'DELETE' })
}
/** 內頁上傳：檔案欄位 `files`，可多張（≤60），整批全有或全無。 */
export function addMangaPages(club: string, id: string, files: File[]): Promise<MangaEpisodeDetailDto> {
  const form = new FormData()
  for (const f of files) form.append('files', f)
  return apiUploadRequest<MangaEpisodeDetailDto>(`${base(club)}/episodes/${id}/pages`, form, { method: 'POST' })
}
export function deleteMangaPage(club: string, id: string, pageId: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/episodes/${id}/pages/${pageId}`, { method: 'DELETE' })
}
/** 內頁圖片說明（替代文字），各 ≤200 字、空白清為無；回傳更新後的集數詳情。 */
export function updateMangaPageAlt(club: string, id: string, pageId: string, altZh: string | null, altEn: string | null): Promise<MangaEpisodeDetailDto> {
  return apiRequest<MangaEpisodeDetailDto>(`${base(club)}/episodes/${id}/pages/${pageId}`, { method: 'PUT', body: { altZh, altEn } })
}
export function reorderMangaPages(club: string, id: string, ids: string[]): Promise<MangaEpisodeDetailDto> {
  return apiRequest<MangaEpisodeDetailDto>(`${base(club)}/episodes/${id}/pages/order`, { method: 'PUT', body: { ids } })
}
