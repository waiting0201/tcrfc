/**
 * C1 球隊管理（俱樂部範圍 CRUD，`/api/v1/admin/{club}/teams`，權限碼 `team.team.*`）與
 * 可寫入球隊清單（`/admin/{club}/teams/writable`）。
 */
import { apiRequest, apiUploadRequest } from './http'

// ══════════════════════════════════════════════════════════════════════════════
// C1 球隊管理（俱樂部範圍 CRUD，S1-7 新增）。對照 `Features/AdminTeams/AdminTeamDtos.cs`。
// ══════════════════════════════════════════════════════════════════════════════

export interface AdminTeamLocaleContentDto {
  name?: string | null
  intro?: string | null
  /** 主視覺圖片說明（替代文字，≤200 字）。 */
  heroAlt?: string | null
}

export interface AdminTeamContentInputDto {
  zh: AdminTeamLocaleContentDto
  en?: AdminTeamLocaleContentDto | null
}

export interface AdminTeamAdminListItemDto {
  id: string
  code: string
  type: string
  gender: string
  ageBand?: string | null
  teamColor?: string | null
  heroKey?: string | null
  /** 後端依 heroKey 附帶回傳的可顯示網址（大圖／160px 方形縮圖）。 */
  heroUrl?: string | null
  heroThumbUrl?: string | null
  heroWidth?: number | null
  heroHeight?: number | null
  sortOrder: number
  nameZh?: string | null
  nameEn?: string | null
  updatedAt: string
}

export interface AdminTeamDetailDto {
  id: string
  code: string
  type: string
  gender: string
  ageBand?: string | null
  teamColor?: string | null
  heroKey?: string | null
  /** 後端依 heroKey 附帶回傳的可顯示網址（大圖／160px 方形縮圖）。 */
  heroUrl?: string | null
  heroThumbUrl?: string | null
  heroWidth?: number | null
  heroHeight?: number | null
  sortOrder: number
  zh: AdminTeamLocaleContentDto
  en?: AdminTeamLocaleContentDto | null
  createdAt: string
  updatedAt: string
}

/** 🔴 `code`：規劃書明文「全站唯一，不得改成 (club_id, code) 複合鍵」——建立時檢查的是全站範圍。
 * `type`／`gender` 值域見 `apps/api/README.md`：`first_team`／`academy`；`men`／`women`／`mixed`。
 * `type = first_team` 每俱樂部至多一筆，由後端檢查（409）。 */
export interface SaveTeamPayload {
  code: string
  type: 'first_team' | 'academy'
  gender: 'men' | 'women' | 'mixed'
  ageBand?: string | null
  teamColor?: string | null
  sortOrder: number
  content: AdminTeamContentInputDto
}

export interface UpdateTeamPayload extends SaveTeamPayload {
  /** true＝移除目前的主視覺圖片，不接受同時夾帶新檔案。 */
  removeHero: boolean
}

export function listAdminClubTeams(club: string): Promise<AdminTeamAdminListItemDto[]> {
  return apiRequest<AdminTeamAdminListItemDto[]>(`/api/v1/admin/${club}/teams`)
}

export function getAdminClubTeam(club: string, id: string): Promise<AdminTeamDetailDto> {
  return apiRequest<AdminTeamDetailDto>(`/api/v1/admin/${club}/teams/${id}`)
}

function buildTeamFormData(payload: SaveTeamPayload | UpdateTeamPayload, file: File | null): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (file) form.append('file', file)
  return form
}

export function createAdminClubTeam(club: string, payload: SaveTeamPayload, heroFile: File | null): Promise<AdminTeamDetailDto> {
  return apiUploadRequest<AdminTeamDetailDto>(`/api/v1/admin/${club}/teams`, buildTeamFormData(payload, heroFile), {
    method: 'POST',
  })
}

export function updateAdminClubTeam(
  club: string,
  id: string,
  payload: UpdateTeamPayload,
  heroFile: File | null,
): Promise<AdminTeamDetailDto> {
  return apiUploadRequest<AdminTeamDetailDto>(`/api/v1/admin/${club}/teams/${id}`, buildTeamFormData(payload, heroFile), {
    method: 'PUT',
  })
}

// ══════════════════════════════════════════════════════════════════════════════
// 「我能寫哪些球隊」（S1-8 續作新增）。對照 apps/api/README.md「S1-8 續作」第 3 節。
// 已依角色資料範圍（`academy_only`）收斂——回應本身就是完整的可寫選項清單，
// 不是「全部球隊 + canWrite 旗標」，畫面直接把回應綁進下拉選單即可，不需要再自行過濾一次。
// ══════════════════════════════════════════════════════════════════════════════

export type AdminWritableTeamModule = 'team' | 'player' | 'staff' | 'match'

export interface AdminWritableTeamDto {
  id: string
  code: string
  type: string
  nameZh?: string | null
  nameEn?: string | null
}

export function listAdminWritableTeams(club: string, module: AdminWritableTeamModule): Promise<AdminWritableTeamDto[]> {
  return apiRequest<AdminWritableTeamDto[]>(`/api/v1/admin/${club}/teams/writable?module=${module}`)
}
