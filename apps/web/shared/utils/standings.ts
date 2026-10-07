// shared/utils/standings.ts — 賽事積分榜與球員數據（S3-9）的型別與純函式
//
// 對應 apps/api README「F 批」：`GET /{club}/standings`、`GET /{club}/stats/players`、`GET /{club}/players/{id}/stats`
// （Features/Standings/StandingsDtos.cs）。純資料型別與純函式，供頁面與 `scripts/check-shop-lib.mjs` 使用。

export interface StandingRow {
  rank: number | null
  teamName: string
  played: number | null
  points: number | null
  /** 後端補上後：該列英文名稱缺漏、回退繁中（可選欄位，舊版 API 不回）。 */
  isFallbackLocale?: boolean
}

export interface SeasonRef {
  code: string
  startOn: string
  endOn: string
}

export interface StandingsResponse {
  season: SeasonRef | null
  seasons: string[]
  items: StandingRow[]
  updatedAt: string | null
  /** 後端補上後：整份回應是否含繁中備援（可選欄位）。 */
  isFallbackLocale?: boolean
}

export interface PlayerSeasonStat {
  playerId: string
  name: string | null
  teamCode: string
  shirtNo: number | null
  position: string | null
  photoUrl: string | null
  appearances: number
  goals: number
  /** 自動彙總時為 `null`（賽事紀錄沒有助攻資料）；只有後台手動輸入的球季數據才有值 */
  assists: number | null
  yellowCards: number
  redCards: number
  /** `auto`＝由已結束賽事彙總；`manual`＝後台手動輸入（有手動數據時以手動為準） */
  source: 'auto' | 'manual' | string
}

export interface PlayerStatsResponse {
  season: SeasonRef | null
  seasons: string[]
  items: PlayerSeasonStat[]
}

export interface PlayerCareerSeason {
  seasonCode: string
  appearances: number
  goals: number
  assists: number | null
  yellowCards: number
  redCards: number
  source: 'auto' | 'manual' | string
}

export interface PlayerCareerStatsResponse {
  playerId: string
  seasons: PlayerCareerSeason[]
}

/** 數據格：`null`／`undefined`（沒有資料來源，例如自動彙總的助攻）顯示「—」，**不是 0**；0 是真實的 0。 */
export function statCell(value: number | null | undefined): string {
  return typeof value === 'number' && Number.isFinite(value) ? String(value) : '—'
}

/** 積分榜名次：沒有名次者顯示「—」（後端把無名次者排在最後）。 */
export function rankCell(rank: number | null | undefined): string {
  return statCell(rank)
}

/** 球季代碼格式容錯檢查（`2026-27`、`2026/27` 等），不合法視為沒指定。 */
export function safeSeasonParam(value: unknown): string {
  return typeof value === 'string' && /^[A-Za-z0-9._/-]{1,20}$/.test(value) ? value : ''
}
