// app/utils/team-view.ts — 公開球隊（`GET /{club}/teams`，C1）的前台檢視型別與小工具（B-10，2026-10-06）

export interface PublicTeamView {
  id: string
  code: string
  clubCode: string
  /** `first_team`（一線隊）或 `academy`（學院／青年梯隊），見 shared/enums.json。 */
  type: string
  gender: string
  ageBand: string | null
  teamColor: string | null
  name: string | null
  intro: string | null
  heroUrl: string | null
  isFallbackLocale?: boolean
}

/** 後台「代表色」是自由文字；只接受 `#RGB`／`#RRGGBB`（要放進 inline style，不合格式一律視為沒有）。 */
export function normalizeTeamColor(raw: string | null | undefined): string | null {
  const v = (raw ?? '').trim()
  if (/^#[0-9a-f]{6}$/i.test(v)) return v.toLowerCase()
  if (/^#[0-9a-f]{3}$/i.test(v)) return `#${v[1]}${v[1]}${v[2]}${v[2]}${v[3]}${v[3]}`.toLowerCase()
  return null
}

function relLuminance(hex: string): number {
  const ch = [1, 3, 5].map((i) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * ch[0]! + 0.7152 * ch[1]! + 0.0722 * ch[2]!
}

/** 代表色色塊與其上文字：依相對亮度在近黑／白擇一，確保文字對比（WCAG ≥ 4.5 的方向，取對比較高者）。 */
export function teamColorChip(raw: string | null | undefined): { bg: string, fg: string } | null {
  const bg = normalizeTeamColor(raw)
  if (!bg) return null
  const L = relLuminance(bg)
  // 與白色對比：1.05 / (L + 0.05)；與近黑 #111 對比：(L + 0.05) / 0.0056
  const withWhite = 1.05 / (L + 0.05)
  const withInk = (L + 0.05) / (relLuminance('#111111') + 0.05)
  return { bg, fg: withInk >= withWhite ? '#111111' : '#ffffff' }
}

/** 學院梯隊分頁標籤：藍鯨球隊代碼帶 `BW-` 前綴（`BW-U15`），分頁沿用既有「U15」寫法。 */
export function academyTabLabel(code: string): string {
  return code.replace(/^BW-/i, '')
}
