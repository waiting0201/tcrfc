// shared/utils/units-en.ts — 單元名稱的英文版（導覽、麵包屑用）
//
// 對照 docs/06-conventions.md §1.1 英文用詞對照表與規劃書 §2／§2.2 單元名稱。
// 只提供主站（tcrfc）英文；藍鯨站（isEn 恆為 false）不會呼叫，若仍傳入 bw 則回傳繁中原名，
// 不得讓藍鯨出現磐石英文名稱。檔內字串不得含中文字元。
import type { SiteUnit } from './site-units'
import { getUnitLabelZh } from './site-units'

const UNIT_LABEL_EN: Readonly<Record<string, string>> = {
  '01': 'Home',
  '02': 'About TCRFC',
  '03': 'Football Club',
  '04': 'TCRFC Academy',
  '05': 'Programs',
  '06': "Women's Football",
  '07': 'News & Stories',
  '08': 'TCRFC Culture',
  '09': 'Partners & Sponsors',
  '10': 'Join / Contact',
  '11': 'Charity & Impact',
  '12': 'FAQ',
  '13': 'Schedule',
}

/** 單元在英文版的顯示名稱（導覽、麵包屑用）。`club` 為 `bw` 時回傳繁中原名（藍鯨不適用英文對照表）。 */
export function getUnitLabelEn(unit: SiteUnit, club: string): string {
  if (club === 'bw') return getUnitLabelZh(unit, club)
  return UNIT_LABEL_EN[unit.code] ?? unit.navKey
}
