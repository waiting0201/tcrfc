/**
 * `GET /admin/teams`（S1-4 續作補上的端點，見 apps/api/README.md「前端回報缺口②之二」）：
 * J4「球隊授權」畫面的跨俱樂部球隊下拉選單，取代先前「沒有清單、只能自己貼識別碼」的暫時作法。
 * 全域端點（無 `{club}` 路由段——指派球隊授權的操作者是系統管理員，球隊本身可能來自任何俱樂部），
 * 權限碼比照既有的 `system.team_grant.view`，不是新的權限碼。
 */
import { apiRequest } from './http'

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
