/**
 * C4「賽程與賽果」／「積分榜」的畫面型別。逐欄位對照
 * `apps/api` 的 `Features/AdminMatches`／`Features/AdminStandings`（見 apps/api/README.md「S1-8」）。
 * 中文標籤逐字對照 `AdminMatchesRepository` 的 `StatusZhLabels`／`HomeAwayZhLabels`／
 * `CompetitionTagZhLabels`（不是自己另外翻譯一套，避免畫面顯示的中文跟 CSV 匯入接受的中文對不上）。
 */

// ── 狀態值域（後端 API 層定案，見 apps/api/README.md「S1-8」〈matches.status 值域定案〉）───────

export type MatchStatus = 'scheduled' | 'live' | 'played' | 'postponed' | 'cancelled'

export const MATCH_STATUS_LABEL: Record<MatchStatus, string> = {
  scheduled: '未開始',
  live: '進行中',
  played: '已結束',
  postponed: '延賽',
  cancelled: '取消',
}

export const MATCH_STATUS_ORDER: MatchStatus[] = ['scheduled', 'live', 'played', 'postponed', 'cancelled']

// ── 主客場 ───────────────────────────────────────────────────────────────────────

export type MatchHomeAway = 'HOME' | 'AWAY'

export const MATCH_HOME_AWAY_LABEL: Record<MatchHomeAway, string> = {
  HOME: '主場',
  AWAY: '客場',
}

export const MATCH_HOME_AWAY_ORDER: MatchHomeAway[] = ['HOME', 'AWAY']

// ── 賽事類型（自由文字標籤，沿用既有種子資料與公開端點既有用字）───────────────────────────

export type MatchCompetitionTag = 'league' | 'cup' | 'friendly' | 'other'

export const MATCH_COMPETITION_TAG_LABEL: Record<MatchCompetitionTag, string> = {
  league: '聯賽',
  cup: '盃賽',
  friendly: '友誼賽',
  other: '其他',
}

export const MATCH_COMPETITION_TAG_ORDER: MatchCompetitionTag[] = ['league', 'cup', 'friendly', 'other']

// ── 卡牌類型（match_cards，規劃書原文只講「黃紅牌」，值域由 apps/api 定案）───────────────────

export type MatchCardType = 'yellow' | 'red'

export const MATCH_CARD_TYPE_LABEL: Record<MatchCardType, string> = {
  yellow: '黃牌',
  red: '紅牌',
}

export const MATCH_CARD_TYPE_ORDER: MatchCardType[] = ['yellow', 'red']

export function matchStatusLabel(value: string): string {
  return MATCH_STATUS_LABEL[value as MatchStatus] ?? value
}

export function matchHomeAwayLabel(value: string | null | undefined): string {
  if (!value) return '—'
  return MATCH_HOME_AWAY_LABEL[value as MatchHomeAway] ?? value
}

export function matchCompetitionTagLabel(value: string | null | undefined): string {
  if (!value) return '—'
  return MATCH_COMPETITION_TAG_LABEL[value as MatchCompetitionTag] ?? value
}

export function matchCardTypeLabel(value: string): string {
  return MATCH_CARD_TYPE_LABEL[value as MatchCardType] ?? value
}
