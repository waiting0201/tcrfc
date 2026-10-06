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

// ── 進球類型（match_goals.goal_type，apps/api/README.md 稽核 A-1）──────────────────────────
// 後端值域固定：空值＝一般進球，其餘五個代碼。寫入端也接受常見同義詞，但畫面一律用下拉。

export type MatchGoalType = '' | 'header' | 'penalty' | 'free_kick' | 'own_goal' | 'other'

export const MATCH_GOAL_TYPE_LABEL: Record<MatchGoalType, string> = {
  '': '一般進球',
  header: '頭槌',
  penalty: '點球',
  free_kick: '自由球',
  own_goal: '烏龍球',
  other: '其他',
}

export const MATCH_GOAL_TYPE_ORDER: MatchGoalType[] = ['', 'header', 'penalty', 'free_kick', 'own_goal', 'other']

/** 對照後端 `MatchGoalTypes.TryNormalize` 的同義詞表（小寫比對）。 */
const GOAL_TYPE_SYNONYMS: Record<string, MatchGoalType> = {
  頭槌: 'header',
  頭球: 'header',
  點球: 'penalty',
  十二碼: 'penalty',
  罰球: 'penalty',
  pk: 'penalty',
  自由球: 'free_kick',
  直接自由球: 'free_kick',
  任意球: 'free_kick',
  freekick: 'free_kick',
  'free-kick': 'free_kick',
  烏龍球: 'own_goal',
  烏龍: 'own_goal',
  owngoal: 'own_goal',
  'own-goal': 'own_goal',
  og: 'own_goal',
  其他: 'other',
}

/**
 * 載入舊資料：後端回的 `goalType` 可能是新代碼、舊的中文自由文字或空值。
 * 對得上就換成選項；對不上（例如「遠射」）選「其他」，並把原文字帶出來讓畫面提示——
 * 後端寫入端不收未知文字（400），無法原樣保留，儲存時會歸為「其他」。
 * 烏龍球的判斷與後端 `IsOwnGoal` 一致（含「烏龍」「own goal」字樣一律視為烏龍球），避免舊資料被誤算成進球。
 */
export function parseGoalType(raw: string | null | undefined): { value: MatchGoalType; legacyText: string } {
  const text = (raw ?? '').trim()
  if (!text) return { value: '', legacyText: '' }
  const lower = text.toLowerCase()
  if (lower in MATCH_GOAL_TYPE_LABEL && lower !== '') return { value: lower as MatchGoalType, legacyText: '' }
  const hit = GOAL_TYPE_SYNONYMS[lower] ?? GOAL_TYPE_SYNONYMS[text]
  if (hit) return { value: hit, legacyText: '' }
  const squeezed = lower.replace(/[\s_-]/g, '')
  if (text.includes('烏龍') || squeezed.includes('owngoal')) return { value: 'own_goal', legacyText: '' }
  return { value: 'other', legacyText: text }
}
