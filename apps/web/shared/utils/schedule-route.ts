// shared/utils/schedule-route.ts — `/schedule/{參數}` 的解析（App 深連結回退網址，App 規劃書 §2.3）
//
// 規劃書對照表有兩條共用 `/schedule/` 前綴的網址：
//   - `tcrfc://schedule/d1`、`/bw1` → `/zh/schedule/d1/`（指定隊別的賽程）
//   - `tcrfc://match/{id}`          → `/zh/schedule/{slug}`（單場賽事詳情）
// 規劃書沒寫兩者怎麼區分（docs/19 §2 缺口 6），執行層決定如下（純函式，無 Nuxt 相依，可單獨測）：
//   1. 參數（不分大小寫）等於某個隊別分頁的 `Team.code`（`d1`／`bw1`／`u15`／`bw-u15`）或分頁 id
//      （`first-team`／`club`）→ 隊別。`all` 不算（那就是 `/schedule/` 本身）。
//   2. 否則參數是 UUID 且在賽事清單裡 → 賽事（目前賽事沒有 slug 欄位，`MatchDto.id` 就是識別碼，
//      App 的 `tcrfc://match/{id}` 用的也是 id）。
//   3. 其餘（含空字串以外的任何未知值、別隊的隊別代號、不存在的賽事 id）→ unknown，頁面 302 回 `/schedule/`。
//   空字串（沒有參數）→ none，就是一般的賽事行事曆首頁。
// 隊別代號全站唯一（docs/14）：磐石站不認得 `bw1`、藍鯨站不認得 `d1`，因為分頁清單本來就依俱樂部產生。

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export interface ScheduleTabRef {
  id: string
  filter: string
}

export type ScheduleTarget =
  | { kind: 'none' }
  | { kind: 'team', filter: string }
  | { kind: 'match', matchId: string }
  | { kind: 'unknown' }

export function resolveScheduleSlug(
  slug: string,
  tabs: readonly ScheduleTabRef[],
  matches: readonly { id: string }[],
): ScheduleTarget {
  const s = slug.trim()
  if (!s) return { kind: 'none' }
  const lower = s.toLowerCase()
  const tab = tabs.find((t) => t.filter !== 'all' && (t.filter.toLowerCase() === lower || t.id.toLowerCase() === lower))
  if (tab) return { kind: 'team', filter: tab.filter }
  if (UUID_RE.test(s)) {
    const m = matches.find((x) => x.id.toLowerCase() === lower)
    if (m) return { kind: 'match', matchId: m.id }
  }
  return { kind: 'unknown' }
}
