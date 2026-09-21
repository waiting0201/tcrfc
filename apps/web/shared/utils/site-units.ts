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
// ⚠️ 這份清單只到「單元」層級（對應主要導覽項），不含每個單元底下的子頁——
// 子頁清單留給 S0-9 完整搬遷、sitemap 需要逐頁列出時再補。

import { isUnitEnabledForClub } from './units'

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
  { code: '11', navKey: 'charity', path: '/zh/charity/', labelZh: '慈善' },
] as const

export function getEnabledSiteUnits(club: string): SiteUnit[] {
  return SITE_UNITS.filter((unit) => isUnitEnabledForClub(unit.code, club))
}
