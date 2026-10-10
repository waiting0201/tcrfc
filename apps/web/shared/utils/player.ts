// shared/utils/player.ts — 球員公開資料型別與網址（對應 apps/api `PlayerDto`，shared/openapi.json）。
//
// 球員詳情網址以 `slug`（同俱樂部唯一，`[a-z0-9]+(-[a-z0-9]+)*`）為正規形式：
// `/zh/club/first-team/player/{slug}/`（App 深連結 `tcrfc://player/{slug}` 的回退網址，App 規劃書 §2.3）。
// id（UUID）網址仍可解析，但詳情頁會 301 到 slug 網址，避免同一球員兩個網址的重複內容。

export interface PlayerDto {
  id: string
  /** 網址代稱；後端保證非空（資料表 NOT NULL）。 */
  slug: string
  teamCode: string
  shirtNo: number | null
  position: string | null
  birthOn?: string | null
  heightCm: number | null
  weightKg: number | null
  nationality: string | null
  preferredFoot: string | null
  photoKey?: string | null
  name: string | null
  bio: string | null
  photoUrl: string | null
  photoWidth?: number | null
  photoHeight?: number | null
  photoAlt?: string | null
  schemaEligible?: boolean
  /** 球員狀態（稽核 A-2）；公開端只回現役，目前恆為 `active`。前台不依此過濾，僅型別容許。 */
  status?: string
}

/** 球員詳情的站內路徑（不含語系前綴，交給 `lp()`）。`slugOrId` 優先給 slug；只有 id 時網址仍可用（頁面會 301 到 slug）。 */
export function playerPath(slugOrId: string): string {
  return `/zh/club/first-team/player/${slugOrId}/`
}
