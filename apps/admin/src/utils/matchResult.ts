/**
 * C4 賽事編輯的純函式（可單元測試，見 scripts/test-match-result.mjs）。
 */

export interface MatchResultState {
  status: string
  matchOn: string
  scoreHome: number | null
  scoreAway: number | null
}

/** 本機日期 YYYY-MM-DD（與日期選擇器的 value-format 同格式，可直接字串比較）。 */
export function localDateString(now: Date = new Date()): string {
  const y = now.getFullYear()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const d = String(now.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/**
 * 已填完整比分、狀態還是「未開始」、而且比賽日期不晚於今天時，應詢問是否改為「已結束」
 * （前台賽果只顯示已結束的比賽，漏改會讓比分一直不出現）。
 */
export function shouldOfferMarkPlayed(state: MatchResultState, today: string = localDateString()): boolean {
  return (
    state.status === 'scheduled' &&
    state.scoreHome !== null &&
    state.scoreHome !== undefined &&
    state.scoreAway !== null &&
    state.scoreAway !== undefined &&
    !!state.matchOn &&
    state.matchOn <= today
  )
}

/** 替補是否有上場的送出值：先發恆為 false；替補有進球或吃牌時一律 true（與後端規則一致）。 */
export function effectiveCameOn(row: { playerId: string; isStarter: boolean; cameOn: boolean }, eventPlayerIds: ReadonlySet<string>): boolean {
  if (row.isStarter) return false
  return row.cameOn || eventPlayerIds.has(row.playerId)
}
