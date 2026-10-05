// shared/utils/units-en.ts — 單元名稱的英文版（導覽、麵包屑用）
//
// 對照 docs/06-conventions.md §1.1 英文用詞對照表與規劃書 §2／§2.2 單元名稱。
// 主站（tcrfc）與藍鯨（bw，B-5 2026-10-05）各一份；藍鯨不得出現磐石英文名稱，04 叫 Youth Teams、不得出現 Academy。
// 檔內字串不得含中文字元。
import type { SiteUnit } from './site-units'
import { BW_NAME_EN } from './club-copy'

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

/** 藍鯨站覆蓋項：02／04／08 名稱含俱樂部詞彙；其餘單元兩站相同（06、11 在藍鯨關閉，不會被呼叫）。 */
const UNIT_LABEL_EN_BW: Readonly<Record<string, string>> = {
  '02': `About ${BW_NAME_EN}`,
  '04': 'Youth Teams',
  '08': `${BW_NAME_EN} Culture`,
}

/** 單元在英文版的顯示名稱（導覽、麵包屑用）。 */
export function getUnitLabelEn(unit: SiteUnit, club: string): string {
  if (club === 'bw') return UNIT_LABEL_EN_BW[unit.code] ?? UNIT_LABEL_EN[unit.code] ?? unit.navKey
  return UNIT_LABEL_EN[unit.code] ?? unit.navKey
}
