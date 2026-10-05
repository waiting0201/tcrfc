// shared/utils/site-units.ts — 前台單元清單（單一真實來源的「資料」半，函式半見 units.ts）
//
// ⚠️ 這裡刻意明確 import isUnitEnabledForClub，不依賴 Nuxt 的 auto-import：
// nuxt.config.ts 的 sitemap.urls 會在 Nitro 的 sitemap 虛擬模組（獨立打包）裡執行，
// 那個情境不會套用 unimport 轉換，auto-import 在那裡會是「執行期 undefined」的
// 靜默錯誤（build 曾因此失敗：isUnitEnabledForClub is not defined，已記入
// docs/18-work-errors.md）。app／server 目錄內的其他呼叫點不受影響，
// 明確 import 對它們來說只是多一行、行為不變。
// 🔵 骨架階段的資料表（STATUS.md S0-9 完整搬遷前，先用設定檔頂著）。
// 單元代號對照 header.html 的 mega menu 編號（2.x 關於／3.x 俱樂部／4.x 學院／5.x 課程／
// 7.x 新聞／8.x 文化／9.x 夥伴），06＝女子足球、11＝慈善，兩者皆無 mega menu 故無法從
// 現有 HTML 反推子項，代號依 docs/14-invariants.md 與 docs/13-blue-whale-site.md §3 直接認定。
// 10（加入與聯絡）、12（FAQ）、13（賽事行事曆）同樣不在 mega menu（CTA 按鈕或另行連結），
// 代號依各自頁面 definePageMeta 的 unit 值直接認定。
// ⚠️ 這份清單只到「單元」層級（對應主要導覽項），不含每個單元底下的子頁——
// 子頁清單留給 S0-9 完整搬遷、sitemap 需要逐頁列出時再補。
// 🔴 docs/18-work-errors.md E-72：13（賽事行事曆）自 S1-15 建置完成起漏列於此清單、
// 10（加入與聯絡，S1-17 建置完成）也同樣漏列，兩者已於 S1-18 本輪一併補上並跑過
// sitemap.xml／llms.txt 收錄驗證（見 apps/web/README.md「S1-18」節）。**新增任何
// 走 `definePageMeta({ unit: 'XX' })` 的頂層單元頁面時，同一次交付要一併檢查這份
// 清單是否已收錄該代碼**——`npm run lint` 的 `lint:site-units-coverage` 防呆
// 會在忘記時擋下（見 scripts/check-site-units-coverage.mjs）。

import { isUnitEnabledForClub } from './units'
import { getClubIdentity } from './club-copy'

export interface SiteUnit {
  /** 單元代號，對照規劃書與 docs/14-invariants.md */
  code: string
  /** 對應 <a data-nav="…"> 的值，供選單標記目前分頁使用 */
  navKey: string
  path: string
  labelZh: string
}

export const SITE_UNITS: readonly SiteUnit[] = [
  { code: '01', navKey: 'home', path: '/zh/', labelZh: '首頁' },
  { code: '02', navKey: 'about', path: '/zh/about/', labelZh: '關於台中磐石' },
  { code: '03', navKey: 'club', path: '/zh/club/', labelZh: '俱樂部' },
  { code: '04', navKey: 'academy', path: '/zh/academy/', labelZh: '足球學院' },
  { code: '05', navKey: 'programs', path: '/zh/programs/', labelZh: '課程' },
  { code: '06', navKey: 'womens', path: '/zh/womens/', labelZh: '女子足球' },
  { code: '07', navKey: 'news', path: '/zh/news/', labelZh: '新聞' },
  { code: '08', navKey: 'culture', path: '/zh/culture/', labelZh: '台中磐石文化' },
  { code: '09', navKey: 'partners', path: '/zh/partners/', labelZh: '夥伴' },
  { code: '10', navKey: 'join', path: '/zh/join/', labelZh: '加入與聯絡' },
  { code: '11', navKey: 'charity', path: '/zh/charity/', labelZh: '慈善' },
  { code: '12', navKey: 'faq', path: '/zh/faq/', labelZh: '常見問題' },
  { code: '13', navKey: 'schedule', path: '/zh/schedule/', labelZh: '賽事行事曆' },
] as const

export function getEnabledSiteUnits(club: string): SiteUnit[] {
  return SITE_UNITS.filter((unit) => isUnitEnabledForClub(unit.code, club))
}

/**
 * 單元在「某個俱樂部」的顯示名稱（`llms.txt` 代表頁清單用）。`SITE_UNITS[].labelZh` 是磐石版名稱，
 * 其中三個單元名稱本身含俱樂部詞彙（02 關於台中磐石／04 足球學院／08 台中磐石文化），直接輸出給藍鯨
 * 會讓藍鯨的 `llms.txt` 出現磐石事實（BW-7 驗收發現，2026-10-05）。這三個改走 `getClubIdentity(club)`
 * 既有欄位（SiteHeader／SiteFooter 同一份），其餘單元名稱兩站相同。
 */
export function getUnitLabelZh(unit: SiteUnit, club: string): string {
  const identity = getClubIdentity(club)
  switch (unit.code) {
    case '02': return identity.aboutLabelZh
    case '04': return identity.academyLabelZh
    case '08': return identity.cultureLabelZh
    default: return unit.labelZh
  }
}
