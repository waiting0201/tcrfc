/**
 * `apps/api` 後台「網站設定」（`I` 模組，S1-12d）端點，對照
 * apps/api/README.md「S1-12d：`I` 網站設定——`GEO-03`／`GEO-04` 站台事實承載與公開端點」與
 * `Features/AdminSiteFacts/AdminSiteFactsDtos.cs`。承載成立年份與日期、首季頭銜、所屬聯賽、
 * 梯隊組成、主場場地、聯絡方式——**全站事實只有這一處維護**，前台頁面與搜尋引擎讀到的摘要資料
 * 都出自同一份值。權限碼 `site.fact.view`／`site.fact.update`，皆為 `sysadmin_only`。
 *
 * 🔴 **整份取代語意**（比照既有 `AdminSeoSettingsDto`）：`updateAdminSiteFacts` 一律送出完整表單
 * 內容，省略欄位＝清空該欄位，不是「維持不變」。`homeVenues` 亦同。
 */
import { apiRequest } from './http'

/** 對照 `Features/AdminSiteFacts/AdminSiteFactsDtos.cs` 的 `AdminSiteFactVenueDto`。 */
export interface AdminSiteFactVenueDto {
  id: string
  nameZh: string
  nameEn?: string | null
  address?: string | null
}

/** 對照 `AdminSiteFactsDto`。 */
export interface AdminSiteFactsDto {
  foundedYear?: string | null
  foundingDateIso?: string | null
  foundingDateDisplayZh?: string | null
  foundingDateDisplayEn?: string | null
  foundingTitleZh?: string | null
  foundingTitleEn?: string | null
  leagueNameZh?: string | null
  leagueNameEn?: string | null
  leagueShortNameZh?: string | null
  leagueShortNameEn?: string | null
  squadStructureZh?: string | null
  squadStructureEn?: string | null
  squadCodes: string[]
  homeVenues: AdminSiteFactVenueDto[]
  contactPhone?: string | null
  contactHoursZh?: string | null
  contactHoursEn?: string | null
  /** 台中藍鯨官方網站網址（主站規劃書 §3.6「06 女子足球」入口頁「前往台中藍鯨官網」按鈕的連結
   * 目標）。**概念上只屬於台中磐石（`tcrfc`）**——藍鯨官網本身沒有 06 單元，`bw` 俱樂部下這個鍵
   * 預期恆為 `null`。`null`＝尚未設定；有值時必為 `https://` 開頭的絕對網址。 */
  blueWhaleSiteUrl?: string | null
}

/** 對照 `UpdateSiteFactVenueRequest`。`id` 有值＝更新既有場地（找不到回 400，不會被誤當成新增），
 * `id` 省略＝新增一筆場地。 */
export interface UpdateSiteFactVenueRequest {
  id?: string | null
  nameZh: string
  nameEn?: string | null
  address?: string | null
}

/** 對照 `UpdateSiteFactsRequest`。 */
export interface UpdateSiteFactsRequest {
  foundedYear?: string | null
  foundingDateIso?: string | null
  foundingDateDisplayZh?: string | null
  foundingDateDisplayEn?: string | null
  foundingTitleZh?: string | null
  foundingTitleEn?: string | null
  leagueNameZh?: string | null
  leagueNameEn?: string | null
  leagueShortNameZh?: string | null
  leagueShortNameEn?: string | null
  squadStructureZh?: string | null
  squadStructureEn?: string | null
  squadCodes?: string[] | null
  homeVenues?: UpdateSiteFactVenueRequest[] | null
  contactPhone?: string | null
  contactHoursZh?: string | null
  contactHoursEn?: string | null
  blueWhaleSiteUrl?: string | null
}

export function getAdminSiteFacts(club: string): Promise<AdminSiteFactsDto> {
  return apiRequest<AdminSiteFactsDto>(`/api/v1/admin/${club}/site-facts`)
}

export function updateAdminSiteFacts(club: string, payload: UpdateSiteFactsRequest): Promise<AdminSiteFactsDto> {
  return apiRequest<AdminSiteFactsDto>(`/api/v1/admin/${club}/site-facts`, { method: 'PUT', body: payload })
}
