/**
 * `GET /admin/teams`（S1-4 續作補上的端點，見 apps/api/README.md「前端回報缺口②之二」）：
 * J4「球隊授權」畫面的跨俱樂部球隊下拉選單，取代先前「沒有清單、只能自己貼識別碼」的暫時作法。
 * 全域端點（無 `{club}` 路由段——指派球隊授權的操作者是系統管理員，球隊本身可能來自任何俱樂部），
 * 權限碼比照既有的 `system.team_grant.view`，不是新的權限碼。
 *
 * 🔴 本檔案下半段（S1-7 新增）是**另一組完全不同的端點**——C1「球隊」俱樂部範圍 CRUD
 * （`/api/v1/admin/{club}/teams`，權限碼 `team.team.*`），跟上面這個全域下拉選單端點只是恰好
 * 同名資源、不同用途：上面那組是「系統管理員指派球隊授權時要選哪支球隊」，下面這組是「這個
 * 俱樂部自己維護球隊主檔資料」。兩者刻意放在同一個檔案（都是 `Features/AdminTeams`），
 * 不建議合併成同一組型別——欄位需求本來就不同（見 apps/api/README.md「S1-7」對兩份 DTO 的說明）。
 */
import { apiRequest, apiUploadRequest } from './http'

export interface AdminTeamListItemDto {
  id: string
  clubId: string
  clubCode: string
  clubNameZh?: string | null
  code: string
  type: string
  gender: string
  ageBand?: string | null
  nameZh?: string | null
  nameEn?: string | null
}

export function listAdminTeams(clubCode?: string): Promise<AdminTeamListItemDto[]> {
  const query = clubCode ? `?clubCode=${encodeURIComponent(clubCode)}` : ''
  return apiRequest<AdminTeamListItemDto[]>(`/api/v1/admin/teams${query}`)
}

// ══════════════════════════════════════════════════════════════════════════════
// C1 球隊管理（俱樂部範圍 CRUD，S1-7 新增）。對照 `Features/AdminTeams/AdminTeamDtos.cs`。
// ══════════════════════════════════════════════════════════════════════════════

export interface AdminTeamLocaleContentDto {
  name?: string | null
  intro?: string | null
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
