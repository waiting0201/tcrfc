/**
 * 球員「位置」選項：資料庫與 API 存代碼 GK／DF／MF／FW（官網英文版與 App 依代碼轉文字），畫面只顯示中文。
 * 2026-11-13 前這欄是自由文字，舊資料可能存中文（門將、守門員、後衛、中場、前鋒），載入時轉回代碼（API 讀出時也會正規化）。
 * 代碼值域與官網 apps/web/shared/utils/player.ts 的 `normalizePlayerPosition` 要同步。
 */
export const PLAYER_POSITION_OPTIONS = [
  { value: 'GK', label: '門將' },
  { value: 'DF', label: '後衛' },
  { value: 'MF', label: '中場' },
  { value: 'FW', label: '前鋒' },
] as const

const ALIASES: Record<string, string> = { GK: 'GK', 門將: 'GK', 守門員: 'GK', DF: 'DF', 後衛: 'DF', MF: 'MF', 中場: 'MF', FW: 'FW', 前鋒: 'FW' }

/** 認得的值（代碼不分大小寫、中文舊值）轉成代碼；認不得的原樣回傳；空值回空字串。 */
export function normalizePlayerPosition(value: string | null | undefined): string {
  const raw = value ?? ''
  return ALIASES[raw.trim().toUpperCase()] ?? raw
}

export function isPlayerPositionCode(value: string): boolean {
  return PLAYER_POSITION_OPTIONS.some((o) => o.value === value)
}

/** 代碼 → 中文顯示文字（列表用）；認不得的舊值原樣顯示，空值回 `fallback`。 */
export function playerPositionLabel(value: string | null | undefined, fallback = '—'): string {
  if (!value || !value.trim()) return fallback
  const code = normalizePlayerPosition(value)
  return PLAYER_POSITION_OPTIONS.find((o) => o.value === code)?.label ?? code
}
