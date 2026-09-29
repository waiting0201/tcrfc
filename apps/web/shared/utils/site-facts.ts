// shared/utils/site-facts.ts — GEO-03／GEO-04 事實單一來源（S1-12d，2026-09-29；
// 2026-09-29 收尾更新角色定位，見下方「本檔現在的角色」）
//
// 主站規劃書 §7 GEO-03：「成立年份、主場與場地、梯隊組成、所屬聯賽、聯絡方式…全站只有
// 一個維護處」；GEO-04：「結構化資料與明確文字同時呈現」。
//
// 🔴 本檔現在的角色（S1-12d 收尾，2026-09-29）：後端已補上
// `GET /api/v1/{club}/site-facts?lang=zh|en` 公開端點（apps/api/README.md「S1-12d」節），
// **本檔下方的 `SITE_FACTS` 靜態物件不再是主要來源**，改由
// `app/composables/useSiteFacts.ts` 的 `useSiteFacts(club)` 讀 API 取值，頁面文字與
// `useSchemaOrgClub.ts` 的 JSON-LD 一律改讀那支 composable。`SITE_FACTS` 保留下來只做
// 兩件事：① 該 composable 在 API 失敗時的降級備援快照（不出 500）；
// ② `shared/utils/club-copy.ts` 仍直接讀取（見該檔案，一個已知、有紀錄的例外——
// club-copy.ts 是模組層級在 import 當下同步組出近 40 處引用的 SEO／Hero 文案物件，
// 沒有 Nuxt 元件的請求生命週期可以掛非同步抓取，接上 API 是遠超本輪邊界的重構，
// 留給下一輪）。**新增頁面請一律呼叫 `useSiteFacts(club)`，不要再直接讀本檔的
// `SITE_FACTS`／`getPrimaryVenue`／`academyTeamCodesLabel`**（這三者仍保留給
// club-copy.ts 與降級路徑使用，不要刪除）。
//
// 🔴 內容紀律比照 club-copy.ts 檔頭：藍鯨的每一個值都要能對應到
// content/blue-whale/club-profile.md 或既有頁面已核實的既有事實，不得自行臆測。
// 這條紀律現在主要約束「降級快照要不要更新」，實際顯示值以後端 API 回傳為準
// （例如藍鯨豐原體育場的官方全名與地址，後端已用既有 `Venue` 資料回傳，可能與本檔
// 下方寫死的快照不同——這是預期中的行為，不是本檔的錯，前台不得覆寫 API 回傳值）。
//
// 用法：`useOrganizationSchema()`／`useSportsTeamSchema()`（見 useSchemaOrgClub.ts）與
// 已改接 API 的頁面都必須從 `useSiteFacts(club)` 取值，不得在頁面或 club-copy.ts 裡
// 重新寫一份字面值（見 apps/web/scripts/check-fact-single-source.mjs）。

import type { ClubCode } from './club'

export interface SiteFactVenue {
  nameZh: string
  nameEn: string | null
  /** null＝地址尚未公開／未核實（比照 club-copy.ts 的 ClubBlock 慣例，不放佔位假地址）。 */
  address: string | null
  /** 是否為主場（用於 JSON-LD 與明文皆需要「主場」單一定義時的判斷，梯隊/學院場地不算主場）。 */
  isHomeGround: boolean
}

export interface SiteFactLeague {
  /** 官方全名，明文與 JSON-LD `memberOf.name` 皆用這個值。 */
  nameZh: string
  nameEn: string | null
  /** 常用簡稱（如「木蘭聯賽」），僅供明文行文使用，JSON-LD 一律用 `nameZh` 全名。 */
  shortNameZh: string | null
}

export interface SiteFacts {
  /** 成立年（西元，字串形式，供明文組句）。 */
  foundedYear: string
  /** ISO 8601 日期，供 JSON-LD `foundingDate` 使用；exact 日期不明時為 `null`
   * （schema.org 允許只填年份字串，但這裡選擇「不確定就不輸出」而非塞入臆測的月日，
   * 比照 GEO-05「資料不足時不輸出該型別／欄位」的一貫原則）。 */
  foundingDateIso: string | null
  /** 「＿＿年創立」／「＿＿年成立」這句完整顯示文字（沿用既有 club-copy.ts `foundedZh`
   * 的既有核實文字，這裡是唯一定義處，club-copy.ts 改為引用本檔）。 */
  foundedDisplayZh: string
  /** 成立當年拿下的頭銜（僅 tcrfc 有這筆核實事實；bw 沒有「成立當年奪冠」這筆事實，
   * 隊史頭銜是逐年累積的，見 club-copy.ts `TIMELINE_BW`，不放進這裡）。 */
  foundingTitleZh: string | null
  /** 目前所屬聯賽（現役聯賽本身，不含歷年賽季或盃賽名稱——那些是 club-copy.ts
   * `HISTORY_YEARS_BW`／`TIMELINE_BW` 的既有歷史記錄陣列，本檔不重複收錄逐年事件）。 */
  league: SiteFactLeague
  /** 主場與場地清單，見 `SiteFactVenue.isHomeGround`。 */
  venues: SiteFactVenue[]
  /** 梯隊組成的簡短描述（體系層級敘述，供 Organization／SportsTeam 以外找不到更精準
   * 型別時的明文使用）。 */
  squadStructureZh: string
  /** 梯隊年齡層代碼清單（不含「一線隊」本身、不含 API 篩選用的俱樂部代碼前綴——那是
   * club-copy.ts `ACADEMY_TEAM_TABS` 的職責，這裡只放「有哪些年齡層」這個事實本身，
   * `ACADEMY_TEAM_TABS` 的 `labelZh`／`id` 由這裡衍生，避免兩處各寫一份年齡層清單）。 */
  squadCodes: string[]
  /** 聯絡方式：目前只有辦公室地址（與主場地址相同，見下方 `officeAddress` 註解）；
   * 電話與營業時間尚未有可公開的核實資料，維持 `null`（不放佔位假資料，比照
   * join/contact/index.vue 既有的「電話」「營業時間」欄位有標籤無內容的既有做法）。 */
  contact: {
    /** tcrfc：本俱樂部辦公室地址與主場地址目前是同一個值（見既有
     * join/contact/index.vue／join/location/index.vue 既有內容），直接取
     * `venues[0].address`，不在這裡重複一份；bw 沒有實體地址（舊站盤點：
     * content/blue-whale/gap-analysis.md §2 單元 10「沒有任何實體地址」），為 `null`。 */
    address: string | null
    phone: string | null
    hours: string | null
  }
}

export const SITE_FACTS: Record<ClubCode, SiteFacts> = {
  tcrfc: {
    foundedYear: '2024',
    // 確切成立月日未核實（現有素材只提供年份），不臆測，JSON-LD foundingDate 因此為 null。
    foundingDateIso: null,
    foundedDisplayZh: '2024 年創立',
    foundingTitleZh: '全國乙級聯賽冠軍',
    league: {
      nameZh: '企業甲級聯賽',
      nameEn: 'Enterprise Premier League',
      shortNameZh: null,
    },
    venues: [
      {
        nameZh: '西屯足球場',
        nameEn: 'Xitun Football Field',
        address: '台中市北屯區崇平路二段景谷巷 11 弄 41 號',
        isHomeGround: true,
      },
    ],
    squadStructureZh: '一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系',
    squadCodes: ['U15', 'U14', 'U12'],
    contact: {
      address: '台中市北屯區崇平路二段景谷巷 11 弄 41 號',
      phone: null,
      hours: null,
    },
  },
  bw: {
    foundedYear: '2014',
    foundingDateIso: '2014-04-12',
    foundedDisplayZh: '2014 年 4 月 12 日成立',
    // bw 沒有「成立當年奪冠」這筆核實事實，隊史第一座冠軍是 2017 年（見 club-copy.ts TIMELINE_BW）。
    foundingTitleZh: null,
    league: {
      nameZh: '台灣木蘭足球聯賽',
      nameEn: null,
      shortNameZh: '木蘭聯賽',
    },
    venues: [
      { nameZh: '台中北屯太原足球場', nameEn: null, address: null, isHomeGround: true },
      { nameZh: '台中豐原體育場', nameEn: null, address: null, isHomeGround: true },
    ],
    squadStructureZh: '一線隊與青年隊（U15／U12）兩個梯隊並行的發展體系',
    squadCodes: ['U15', 'U12'],
    // 舊站盤點：content/blue-whale/gap-analysis.md §2 單元 10「沒有任何實體地址、電話
    // 或聯絡表單」，不得自行臆測一個地址。
    contact: {
      address: null,
      phone: null,
      hours: null,
    },
  },
}

function normalizeClub(club: string): ClubCode {
  return club === 'bw' ? 'bw' : 'tcrfc'
}

export function getSiteFacts(club: string): SiteFacts {
  return SITE_FACTS[normalizeClub(club)]
}

/** 主場（`isHomeGround` 第一筆）。bw 有两个主場，回傳第一筆（太原足球場）供只需要單一
 * 場地名稱的句子使用；需要完整清單（例如場地位置頁）請直接讀 `venues`。 */
export function getPrimaryVenue(club: string): SiteFactVenue {
  const facts = getSiteFacts(club)
  return facts.venues.find((v) => v.isHomeGround) ?? facts.venues[0]
}

/** 「U15／U14／U12」這種梯隊代碼組字——單一來源是 `squadCodes`，頁面與 club-copy.ts
 * 不得各自重新打一份代碼清單（GEO-03）。`separator` 預設全形斜線，比照既有頁面既定寫法；
 * 部分頁面用頓號（見 academy/join.vue），呼叫端自行傳入。 */
export function academyTeamCodesLabel(club: string, separator = '／'): string {
  return getSiteFacts(club).squadCodes.join(separator)
}
