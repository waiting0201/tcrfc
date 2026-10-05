// shared/utils/club-copy.ts — 前台文案依俱樂部切換的單一真實來源
// （docs/13-blue-whale-site.md §6 紀律 11，新增於本次任務）
//
// 🔴 這支檔案只放「俱樂部自己的事實與敘事」（隊名、標語、成立年、沿革、願景、
// 聯絡方式、社群連結、useSeoMeta 的 title/description）。不放：
//   (a) 版型／導覽／功能性 UI 文字（「更多消息」「送出」這種兩站一模一樣的字）
//       ——那些留在各頁 <template> 原地，不必進資料層。
//   (c) 動態內容（新聞、球員、賽程、商品）——那是後台 API 依 club_id 供給的
//       （見 app/pages/zh/about/our-people.vue 打 /api/backend/${club}/staff 的既有做法），
//       不在這裡重複做一套靜態假資料。
//
// 🔴 型別設計說明（給新增文案鍵的人看）：
// 1. 每個內容鍵都用 `Record<ClubCode, ...>`（`ClubText<T>` 或 `ClubBlock<T>`，
//    見下方兩個型別定義）——兩個俱樂部的欄位都必須存在，只填 tcrfc 漏填 bw
//    會是 TypeScript 編譯錯誤，不是執行期靜默回退成磐石文案。這條刻意跟
//    club.ts 的 getClubAssets() 分開處理：資產一定兩份都有，靜默正規化輸入
//    字串合理；文案不一定兩份都寫得出來，所以「兩份都要填」這件事必須是型別
//    層級的強制，不能用同一種「找不到就回退 tcrfc」的寫法。
// 2. 兩種內容鍵型別：
//    - `ClubText<T>`：兩家保證都有值，不接受 null（本檔絕大多數 SEO／Hero
//      文案都是這種——即使某頁對某俱樂部內容單薄，也還是有一段可以顯示的
//      文字，不是「這頁對這家俱樂部整個不存在」）。
//    - `ClubBlock<T>`：值可能是 `T | null`，`null` 專指「本站明文決定不顯示
//      這個區塊」（比照 docs/13 踩雷點 8 對標誌的處理：缺資產不放假圖，缺
//      文案不放佔位假文，直接不顯示），不得用 null 代表「文案還沒寫」——
//      還沒寫的內容，代表這個鍵目前根本不該存在，等文案生產出來後才新增
//      這個鍵。本次交付的「隱藏」大多是動態內容區塊（球員／新聞／賽程等，
//      依規則 (c) 本來就不進本檔），改用頁面層級的 `isTcrfc`／`clubKey`
//      判斷隱藏；`ClubBlock<T>` 目前只用在 `ClubIdentity` 內的個別欄位
//      （例如 `social.email`），留給日後真的出現「這個文案鍵某俱樂部明確
//      沒有」時使用。
// 3. 雙語：`Bilingual.en` 型別是 `string | null`，null 代表「英文尚未生產」
//    （全域規定第 4 條：欄位必須存在，值可空）。舊站盤點顯示藍鯨舊站 0 個英文字，
//    英文全部是新生產、不是翻譯既有——目前 apps/web 只有 zh 頁面上線（en 留給
//    S0-9 之後的雙語工作），這裡先把欄位開好，等雙語頁面動工時就不會漏接。
//
// 🔴 內容紀律（任務交付時的既有限制，之後新增文案務必延續）：
// - 藍鯨文案一律只能引用、節錄、重排 content/blue-whale/ 的舊站原文，不得自行
//   創作藍鯨沒說過的話。需要新文案的地方一律標成待補（英文正式全名同理，
//   舊站三種寫法並存、尚待客戶確認，brandTagEn 對藍鯨一律是 null，不得自行選一個）。
// - 未成年球員名單／照片不進本檔（U15／U12 肖像同意未知）。

import type { ClubCode } from './club'
// GEO-03／GEO-04（S1-12d）事實單一來源：成立年份、主場、聯賽三類事實不在本檔重複寫一份
// 字面值，一律引用後端 `GET /api/v1/{club}/site-facts`（`app/composables/useSiteFacts.ts`）。
//
// 🔴 S1-12d 收尾第二輪（2026-09-29）：本檔原本直接讀 site-facts.ts 的靜態 `SITE_FACTS`
// 快照組文案，改事實不會反映到這裡。現在改為「吃事實參數的工廠函式」——凡是內容依賴
// GEO-03 五類事實（成立年份、主場、聯賽、梯隊代碼、聯絡方式）的鍵，一律是
// `getXxx(club, facts): T` 函式而不是 `ClubText<T>` 常數，`facts` 由呼叫端的
// `useSiteFacts(club)` 取得（API 打不到時該 composable 自動退回 site-facts.ts 的
// 靜態快照，本檔不需要知道這個降級細節）。**呼叫端必須確保傳入的 `facts` 對應同一個
// `club`**（一律用同一次 `useSiteFacts(club)` 呼叫取得兩者），本檔不做交叉核對。
// 不依賴事實的內容鍵（版型固定的 SEO／Hero／CTA 文案等）維持原本的 `ClubText<T>`
// 靜態物件寫法，不要無謂改成函式。
import type { SiteFacts } from './site-facts'

function normalizeClub(club: string): ClubCode {
  return club === 'bw' ? 'bw' : 'tcrfc'
}

/** 主場（`isHomeGround` 第一筆）。等價於 site-facts.ts 的 `getPrimaryVenue(club)`，
 * 差別是這裡直接吃已經取得的 `facts`，不用再認識 `club` 字串去查表。 */
function primaryVenueOf(facts: SiteFacts) {
  return facts.venues.find((v) => v.isHomeGround) ?? facts.venues[0]!
}

/** 「U15／U14／U12」這種梯隊代碼組字，等價於 site-facts.ts 的 `academyTeamCodesLabel(club, sep)`。 */
function squadCodesLabel(facts: SiteFacts, separator = '／'): string {
  return facts.squadCodes.join(separator)
}

/** T | null 的 null＝本俱樂部明文不顯示這個區塊（見檔頭說明 2）。目前沒有任何內容鍵用到
 * 這個型別——本次交付的「隱藏」一律是動態內容區塊（球員／新聞／賽程等，不進本檔），
 * 用頁面層級的 `isTcrfc`／`clubKey` 判斷隱藏，不是文案本身缺漏；型別留著給日後真的
 * 出現「這個文案鍵某俱樂部明確沒有」的情況使用，不必為了用而用。 */
export type ClubBlock<T> = Record<ClubCode, T | null>

/** 兩家俱樂部都保證會填、不會是 null 的內容鍵用這個型別（SEO、Hero 文案等）——
 * 一樣是 Record，兩個 key 都是 TypeScript 強制必填，只是值本身不接受 null，
 * 讀取端不需要额外處理「可能是 null」的分支。 */
export type ClubText<T> = Record<ClubCode, T>

export interface Bilingual {
  zh: string
  /** null＝英文尚未生產（不是遺漏，全域規定第 4 條要求欄位存在）。 */
  en: string | null
}

export interface SeoCopy {
  title: string
  description: string
}

// ---------------------------------------------------------------------------
// 俱樂部識別——跨頁重複用到的最小共同事實集合，SiteHeader／SiteFooter／各頁
// 的麵包屑與 SEO 都從這裡取值，不得各自硬編碼一份。
// ---------------------------------------------------------------------------

export interface ClubIdentity {
  /** about 系列共用的「關於＿＿」文字（麵包屑、mega menu、頁首 eyebrow 共用） */
  aboutLabelZh: string
  /** 08 單元的「＿＿文化」文字（mega menu／footer 共用） */
  cultureLabelZh: string
  /** 04 單元導覽標籤的**全名**——磐石是「足球學院」，藍鯨依 docs/13-blue-whale-site.md §3 改為「青年隊」 */
  academyLabelZh: string
  /** 04 單元的行動選單英文標籤——磐石 ACADEMY／藍鯨 YOUTH，同一條 §3 改名依據 */
  academyLabelEn: string
  /**
   * 04 單元導覽標籤的**短名**，用於各種「＿＿ ＋ 後綴／前綴」的複合句
   * （SiteFooter／SiteHeader 的「加入＿＿」「＿＿總覽」「＿＿隊伍」「＿＿發展路徑」
   * 「＿＿教練團」「＿＿生活」「＿＿新聞」，以及 `pages/zh/join/index.vue` 的
   * 「…與＿＿場地…」）。
   *
   * 🔴 跟 `academyLabelZh`（全名）的分界只有「全名／短名」，不是「join／非 join」——
   * 名稱裡不再帶 `Join` 就是為了不讓下一個人誤以為它只管「加入＿＿」那一句
   * （S0-9n（2026-09-23）把這個欄位從只用在 SiteFooter「加入＿＿」擴大到 SiteHeader
   * 另外 8 處非 join 的複合句時，欄位名稱與這段 JSDoc 都沒跟著改，變成名字叫
   * `academyJoinLabelZh` 卻承載六種非 join 語意——這正是 `E-42`「一個欄位身兼兩種
   * 語境」同一個根因的第三次現形，發現後當場改名而不是留著錯的名字加註解了事）。
   *
   * 🔴 S0-9m（2026-09-23）修的是這個欄位跟 `academyLabelZh` 被誤當同一個欄位用的
   * bug（`E-42` 第二次現形）：mockup 的 footer.html 逐字是「加入學院」，不是
   * 「加入足球學院」——「學院」是這一句自己的慣用縮寫，不是 `academyLabelZh`
   * （「足球學院」）去掉「足球」兩個字算出來的。磐石值 `學院` 逐字對應 mockup；
   * 藍鯨值沿用已核准的 `academyLabelZh`（`青年隊`）本身，不是新文案——藍鯨沒有
   * 「加入＿＿」原文可以引用，用同一個已核准的單元短名組句是唯一不需要自行創作
   * 的作法（紀律 11）；藍鯨恰好全名＝短名（都是「青年隊」），這是巧合不是規則，
   * 不代表兩個欄位可以合併。
   */
  academyShortLabelZh: string
  /**
   * 頁首 kicker／SEO 用的英文品牌縮寫。
   * 🔴 藍鯨一律 null——英文正式全名舊站有三種寫法並存，待客戶確認
   * （docs/13-blue-whale-site.md §5 第 2 項），不得自行選一個顯示在頁面上。
   */
  brandTagEn: string | null
  /** 官方標語（沿用舊站已公開發布的中英文字句，非新譯）。null＝無對外標語。 */
  slogan: Bilingual | null
  /** SiteFooter 品牌欄一句話介紹 */
  footerBlurb: string
  /** SiteFooter 版權列 */
  copyrightZh: string
  social: {
    facebook: string | null
    instagram: string | null
    youtube: string | null
    line: string | null
    email: string | null
  }
}

// 🔴 S1-12d 收尾第二輪（2026-09-29）：`ClubIdentity` 原本有一個 `foundedZh` 欄位
// （直接讀 SITE_FACTS 靜態快照），但全站沒有任何頁面實際讀取
// `identity.foundedZh`（grep 全 `app/` 目錄零命中）——是已停用的死欄位，不是遺漏。
// 移除它比照本輪「site-facts.ts 只剩降級備援角色」的目標：與其把整個
// `CLUB_IDENTITY`／`getClubIdentity()`（SiteHeader／SiteFooter／十餘頁共用）改成
// 吃 facts 參數的工廠函式去養一個沒人用的欄位，不如直接刪掉，需要「＿＿年創立」
// 這句文字的頁面一律呼叫 `useSiteFacts(club).facts.value.foundedDisplayZh`
// （見下方各 `getXxx()` 工廠函式的既有用法）。
/**
 * 🔴 藍鯨「英文文案裡的俱樂部名稱」唯一來源（STATUS B-5，2026-10-05 客戶定案）：
 * 簡稱 `Taichung Blue Whale`（導覽、內文、標題）；全名 `Taichung Blue Whale Women's Football Club`
 * （Schema、llms、頁尾、首次提及）。舊站的 `Taichung Bluewhale`／`…Women's Football Team`／
 * `Taichung blue whale` 變體一律不用。英文句子需要藍鯨名稱時**用這兩個常數，不得寫死**，
 * `scripts/check-bw-en-name.mjs` 只允許這兩種寫法、擋其他變體（寫死也擋，常數定義處除外）。
 */
export const BW_NAME_EN = 'Taichung Blue Whale'
export const BW_FULL_NAME_EN = "Taichung Blue Whale Women's Football Club"

export const CLUB_IDENTITY: Record<ClubCode, ClubIdentity> = {
  tcrfc: {
    aboutLabelZh: '關於台中磐石',
    cultureLabelZh: '台中磐石文化',
    academyLabelZh: '足球學院',
    academyLabelEn: 'ACADEMY',
    academyShortLabelZh: '學院',
    brandTagEn: 'TCRFC',
    slogan: { zh: '在地扎根．放眼世界', en: 'LOCAL ROOTS. GLOBAL PATHWAYS.' },
    footerBlurb: '台中磐石足球俱樂部致力於透過專業模式，培育選手追求卓越，讓世界看見台灣足球。',
    copyrightZh: '© 2026 台中磐石足球俱樂部 Taichung Rock FC. All rights reserved.',
    social: {
      facebook: 'https://www.facebook.com/TCRFC2024',
      instagram: 'https://www.instagram.com/tcr_fc_2024',
      youtube: 'https://www.youtube.com/@TCRFC-2024',
      line: null,
      email: null,
    },
  },
  bw: {
    aboutLabelZh: '關於台中藍鯨',
    cultureLabelZh: '台中藍鯨文化',
    academyLabelZh: '青年隊',
    academyLabelEn: 'YOUTH',
    academyShortLabelZh: '青年隊',
    // 🔴 不得自行選定英文正式全名（B-5，見 BW_NAME_EN）。
    brandTagEn: null,
    // 沿用舊站首頁已公開發布的中英文標語原文（content/blue-whale/club-profile.md §3），
    // 不是新譯。⚠️ 舊站英文句首是英文名，B-5 未定前改放
    // `BW_NAME_EN`（中文名），定案後句子即與舊站一致。
    slogan: { zh: '航向世界的藍鯨', en: `${BW_NAME_EN} rides the waves towards the open ocean` },
    // 直接節錄 content/blue-whale/club-profile.md §1「定位敘述（原文）」兩句，未新增文字。
    footerBlurb:
      '隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。',
    // 逐字引用舊站頁尾版權列（content/blue-whale/club-profile.md §9）。
    copyrightZh: '© 2014 ｜臺中市女子足球協會｜台中藍鯨女子足球隊｜台中藍鯨足球學校',
    social: {
      facebook: 'https://www.facebook.com/tbwfc',
      instagram: 'https://instagram.com/tcbw2014',
      youtube: 'https://www.youtube.com/@user-xu1wm3xx1w',
      line: 'https://lin.ee/CS65qCR',
      email: 'fbbh2014@gmail.com',
    },
  },
}

export function getClubIdentity(club: string): ClubIdentity {
  return CLUB_IDENTITY[normalizeClub(club)]
}

/** 取值輔助——回傳 null 時代表呼叫端該隱藏對應區塊，型別會強迫呼叫端處理這個分支。 */
export function getClubBlock<T>(block: ClubBlock<T>, club: string): T | null {
  return block[normalizeClub(club)]
}

/**
 * about 系列頁首 eyebrow 的「編號 + About + 英文縮寫」組字——藍鯨沒有英文縮寫
 * 時只輸出編號，不得自創英文字樣頂替（見 ClubIdentity.brandTagEn 註解）。
 */
export function aboutEyebrow(num: string, club: string): string {
  const brandTagEn = getClubIdentity(club).brandTagEn
  return brandTagEn ? `${num} About ${brandTagEn}` : num
}

// ---------------------------------------------------------------------------
// 01 首頁
// ---------------------------------------------------------------------------

export interface HomeHeroCopy {
  kickerEn: string | null
  headlineZh: string
  /** 「俱樂部名 · 成立年 · 頭銜」這行，只放已核實的既有事實，不臆測戰績 */
  factLineZh: string
  /**
   * 「加入球隊」CTA 的連結目標。🔴 磐石逐字沿用 mockup 既有連結 `/zh/charity/`
   * （site/src/pages/zh/index.html 原文如此，S0-9k 查證：這是既有 mockup 的既定行為，
   * 不是本次搬遷造成的新錯，依 docs/14-invariants.md「前台改 Nuxt 後必須與現有 mockup
   * 一模一樣」不得順手改成看起來更合理的 `/zh/join/player/`）。藍鯨沒有 11 慈善單元
   * （docs/13-blue-whale-site.md §3 已移除），沿用同一個路徑會是真的死連結，
   * 改指向本站實際存在、語意對得上的加入球員頁。
   */
  ctaPrimaryHref: string
  ctaSecondaryLabelZh: string
  /** 「認識＿＿」CTA 的連結目標，同上一併沿用 mockup 既有值（`/zh/culture/`），藍鯨改指向 about。 */
  ctaSecondaryHref: string
}

/** 01 首頁 SEO——`description` 含成立年份／首季頭銜／聯賽事實，改為工廠函式（S1-12d 收尾第二輪）。 */
export function getHomeSeo(club: string, facts: SiteFacts): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '台中藍鯨女子足球隊｜航向世界的藍鯨',
      description: `台中藍鯨女子足球隊官方網站。隸屬臺中市女子足球協會，${facts.foundedDisplayZh}，${facts.league.nameZh}球隊，隊史五度奪得木蘭聯賽冠軍。一線隊、青年隊、推廣活動三大體系。`,
    }
  }
  return {
    title: '台中磐石足球俱樂部 TCRFC｜在地扎根．放眼世界',
    description: `台中磐石足球俱樂部（TCRFC）官方網站。${facts.foundedDisplayZh}，${facts.foundedYear} ${facts.foundingTitleZh}。一線隊、台中磐石足球學院、課程與活動、女子足球四大體系。`,
  }
}

/** 01 首頁 Hero——`factLineZh` 含成立年份／首季頭銜事實，改為工廠函式（S1-12d 收尾第二輪）。 */
export function getHomeHero(club: string, facts: SiteFacts): HomeHeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      kickerEn: `${BW_NAME_EN} rides the waves towards the open ocean`,
      headlineZh: '航向世界<br>的藍鯨',
      // 「五度」是對 content/blue-whale/club-profile.md §4 沿革逐條「隊史第 X 座台灣
      // 木蘭聯賽冠軍」明文出現次數的計數（2017／2018／2019／2021／2023 共五次），
      // 是核算既有原文，不是新臆測的戰績。
      factLineZh: `台中藍鯨女子足球隊 · <b>${facts.foundedDisplayZh}</b> · <b>隊史五度奪得木蘭聯賽冠軍</b>`,
      ctaPrimaryHref: '/zh/join/player/',
      ctaSecondaryLabelZh: '認識台中藍鯨',
      ctaSecondaryHref: '/zh/about/',
    }
  }
  return {
    kickerEn: 'LOCAL ROOTS. GLOBAL PATHWAYS.',
    headlineZh: '在地扎根<br>放眼世界',
    factLineZh: `台中磐石足球俱樂部 · <b>${facts.foundedDisplayZh}</b> · <b>${facts.foundedYear} ${facts.foundingTitleZh}</b>`,
    ctaPrimaryHref: '/zh/charity/',
    ctaSecondaryLabelZh: '認識台中磐石',
    ctaSecondaryHref: '/zh/culture/',
  }
}

export interface PillarCopy {
  /** 頁內錨點 id（供頁尾／導覽的 #academy 等連結使用）。不是每個支柱都有。 */
  id?: string
  enLabel: string
  zhLabel: string
  linkLabelZh: string
  href: string
  /**
   * 卡片圖片的無障礙敘述。🔴 磐石逐字沿用 mockup 原文（描述真實照片內容）；
   * 藍鯨目前重用同一組通用足球場景照（不是藍鯨自己的照片，見樣板檔頭註解），
   * 依 WCAG 對「純裝飾、與旁邊文字敘述無關的重用圖片」的建議留空字串，
   * 不得沿用磐石照片的敘述文字（那會誤植成藍鯨的真人真事）。
   */
  imgAlt: string
  /** 卡片圖片的原始尺寸（縮圖後主檔尺寸），須與實際圖檔一致以避免 CLS。 */
  imgWidth: number
  imgHeight: number
}

/** 首頁四大支柱／三大體系。藍鯨移除自我指涉節點（女子足球），04 依 docs/13 §3 改為青年隊。 */
export const HOME_PILLARS: ClubText<PillarCopy[]> = {
  tcrfc: [
    { enLabel: 'FOOTBALL CLUB', zhLabel: '台中磐石足球俱樂部', linkLabelZh: '了解一線隊', href: '/zh/schedule/', imgAlt: '台中磐石一線隊夜間比賽出戰畫面', imgWidth: 1280, imgHeight: 855 },
    { id: 'academy', enLabel: 'ACADEMY', zhLabel: '台中磐石足球學院', linkLabelZh: '認識學院', href: '/zh/academy/', imgAlt: '台中磐石足球學院青少年球員於斯洛伐克進行交流賽', imgWidth: 1920, imgHeight: 1279 },
    { id: 'programs', enLabel: 'PROGRAMS', zhLabel: '課程與活動', linkLabelZh: '查看課表', href: '/zh/programs/', imgAlt: '足球課程訓練現場，教練以障礙錐引導球員進行帶球練習', imgWidth: 1920, imgHeight: 1279 },
    { id: 'womens', enLabel: "WOMEN'S FOOTBALL", zhLabel: '女子足球', linkLabelZh: '認識藍鯨', href: '/zh/womens/', imgAlt: '女子足球比賽畫面', imgWidth: 1280, imgHeight: 853 },
  ],
  bw: [
    { enLabel: 'FIRST TEAM', zhLabel: '台中藍鯨一線隊', linkLabelZh: '了解一線隊', href: '/zh/club/first-team/', imgAlt: '', imgWidth: 1280, imgHeight: 855 },
    { id: 'academy', enLabel: 'YOUTH', zhLabel: '青年隊', linkLabelZh: '認識青年隊', href: '/zh/academy/', imgAlt: '', imgWidth: 1920, imgHeight: 1279 },
    { id: 'programs', enLabel: 'PROGRAMS', zhLabel: '推廣活動', linkLabelZh: '查看活動', href: '/zh/programs/', imgAlt: '', imgWidth: 1920, imgHeight: 1279 },
  ],
}

export interface CtaCardCopy {
  num: string
  titleZh: string
  descZh: string
  ctaLabelZh: string
  href: string
}

/** 首頁與一線隊頁共用的「加入我們」CTA 三卡——磐石版逐字沿用既有 mockup 文案；藍鯨版改「企甲聯賽」為「木蘭聯賽」、學院為青年隊、俱樂部名稱代入，其餘句型不變。
 * 只有 bw 版「加入青年隊」卡的 `descZh` 含梯隊代碼事實，改為工廠函式（S1-12d 收尾第二輪）。 */
export function getHomeCtaTrio(club: string, facts: SiteFacts): CtaCardCopy[] {
  if (normalizeClub(club) === 'bw') {
    return [
      {
        num: '10.1',
        titleZh: '加入球隊',
        descZh: '具備競技實力、渴望在木蘭聯賽舞台證明自己？我們持續招募一線隊球員。',
        ctaLabelZh: '填寫報名表',
        href: '/zh/join/player/',
      },
      {
        num: '10.2',
        titleZh: '加入青年隊',
        descZh: `${squadCodesLabel(facts)} 青少年女子足球隊，提供系統化的足球訓練。`,
        ctaLabelZh: '洽詢報名',
        href: '/zh/join/academy/',
      },
      {
        num: '10.5',
        titleZh: '成為合作夥伴',
        descZh: '攜手台中藍鯨，透過女子足球平台觸及在地社群，共創品牌與社區的雙贏價值。',
        ctaLabelZh: '洽談合作',
        href: '/zh/join/partnership/',
      },
    ]
  }
  return [
    {
      num: '10.1',
      titleZh: '加入球隊',
      descZh: '具備競技實力、渴望在企甲聯賽舞台證明自己？我們持續招募一線隊與各梯隊球員。',
      ctaLabelZh: '填寫報名表',
      href: '/zh/join/player/',
    },
    {
      num: '10.2',
      titleZh: '加入學院／兒童訓練',
      descZh: '從基礎技術到比賽觀念，台中磐石足球學院與兒童訓練課程提供各年齡層系統化的足球訓練。',
      ctaLabelZh: '預約試訓',
      href: '/zh/join/academy/',
    },
    {
      num: '10.5',
      titleZh: '成為合作夥伴',
      descZh: '攜手台中磐石，透過職業足球平台觸及在地社群，共創品牌與社區的雙贏價值。',
      ctaLabelZh: '洽談合作',
      href: '/zh/join/partnership/',
    },
  ]
}

// ---------------------------------------------------------------------------
// 02 ABOUT 系列
// ---------------------------------------------------------------------------

export interface HeroCopy {
  h1Zh: string
  h1En: string | null
  /**
   * 一般用 `{{ hero.lede }}` 文字插值渲染。少數頁（history.vue／
   * join/contact/index.vue 的 tcrfc 版）延續 mockup 既有的內嵌連結寫法，
   * 那幾頁改用 `v-html` 渲染——內容全部來自本檔案的靜態字串，非使用者輸入，
   * 沒有 XSS 疑慮。新增內容時預設寫純文字，只有需要沿用既有內嵌連結時才加 HTML。
   */
  lede: string
}

export const ABOUT_INDEX_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '關於台中磐石 About TCRFC｜台中磐石足球俱樂部',
    description: '認識台中磐石足球俱樂部：我們的故事、願景與使命、足球理念、團隊成員、治理與管理、生態系、俱樂部歷程與重要里程碑。',
  },
  bw: {
    title: '關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '認識台中藍鯨女子足球隊：我們的故事、發展願景、俱樂部口號與培訓精神、團隊成員、治理與管理、生態系、俱樂部歷程與重要里程碑。',
  },
}

/** about/index.vue 頁首——含成立年份／聯賽事實，改為工廠函式（S1-12d 收尾第二輪）。 */
export function getAboutIndexHero(club: string, facts: SiteFacts): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '關於台中藍鯨',
      h1En: null,
      lede: `台中藍鯨女子足球隊隸屬臺中市女子足球協會，${facts.foundedDisplayZh}，是${facts.league.nameZh}的球隊之一。以下篇章帶你認識這支球隊從理念到組織的全貌。`,
    }
  }
  return {
    h1Zh: '關於台中磐石',
    h1En: 'About TCRFC',
    lede: `LOCAL ROOTS. GLOBAL PATHWAYS.｜在地扎根 · 放眼世界。台中磐石足球俱樂部 ${facts.foundedYear} 年於台中成立，以下八個篇章，帶你認識這支球隊從理念到組織的全貌。`,
  }
}

/** about/index.vue 8 張導覽卡的描述，只有內容涉及俱樂部專屬架構（願景框架、五大核心價值、生態系）的 3 張需要藍鯨版本。 */
export const ABOUT_NAV_DESC: ClubText<{
  ourStory: string
  visionMission: string
  philosophy: string
  ourPeople: string
  ecosystem: string
  history: string
}> = {
  tcrfc: {
    ourStory: '認識台中磐石從創立至今的發展沿革。',
    visionMission: '台中磐石的核心願景與俱樂部使命。',
    philosophy: '足球理念與五大核心價值。',
    ourPeople: '認識台中磐石的教練團與行政團隊。',
    ecosystem: '一線隊、學院、課程、女足四大體系總覽。',
    history: '圖文紀錄台中磐石的發展歷程。',
  },
  bw: {
    ourStory: '認識台中藍鯨從創立至今的發展沿革。',
    visionMission: '台中藍鯨的發展願景。',
    philosophy: '俱樂部口號與培訓精神。',
    ourPeople: '認識台中藍鯨的教練團隊。',
    ecosystem: '一線隊、青年隊、推廣活動三大體系總覽。',
    history: '圖文紀錄台中藍鯨的發展歷程。',
  },
}

// 2.1 我們的故事——SEO／Hero 含成立年份事實，改為工廠函式（S1-12d 收尾第二輪）。
export function getOurStorySeo(club: string, facts: SiteFacts): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '我們的故事｜關於台中藍鯨｜台中藍鯨女子足球隊',
      description: `台中藍鯨女子足球隊 ${facts.foundedYear} 年成立於台中，隸屬臺中市女子足球協會。認識這支球隊的定位與成立宗旨。`,
    }
  }
  return {
    title: '我們的故事 Our Story｜關於台中磐石｜台中磐石足球俱樂部',
    description: `台中磐石足球俱樂部（TCRFC）於 ${facts.foundedYear} 年在台中成立。這裡收錄俱樂部從創立至今的沿革故事，完整內文正在整理中。`,
  }
}

export function getOurStoryHero(club: string, facts: SiteFacts): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '我們的故事',
      h1En: null,
      lede: `台中藍鯨女子足球隊 ${facts.foundedDisplayZh}，隸屬臺中市女子足球協會，是${facts.league.nameZh}的球隊之一。`,
    }
  }
  return {
    h1Zh: '我們的故事',
    h1En: 'Our Story',
    lede: `LOCAL ROOTS. GLOBAL PATHWAYS.｜台中磐石足球俱樂部 ${facts.foundedYear} 年於台中成立。這裡是我們沿革故事的篇章，完整內文正在與俱樂部確認中。`,
  }
}

/** 直接節錄 content/blue-whale/club-profile.md §1「定位敘述（原文）」，未增刪文字。 */
export const OUR_STORY_BODY_BW =
  '隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，簡稱為台中藍鯨，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。台中藍鯨希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。'

// 2.2 願景與使命
export const VISION_MISSION_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '願景與使命 Vision & Mission｜關於台中磐石｜台中磐石足球俱樂部',
    description: '台中磐石足球俱樂部的願景與使命：透過專業化培育體系，讓台中在地選手邁向職業舞台，並以足球讓世界看見台灣。',
  },
  bw: {
    title: '發展願景｜關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊的發展願景：無止盡的探索、不怕難的堅韌、更細膩的態度、最真實的影響、更深遠之目的。',
  },
}

export const VISION_MISSION_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '願景與使命',
    h1En: 'Vision & Mission',
    lede: '從台中出發：培育本土選手邁向職業、成為在地榮耀的來源，並以足球讓世界看見台灣。',
  },
  bw: {
    h1Zh: '發展願景',
    h1En: null,
    lede: '「追尋卓越 止於至善（Pursuit of Brilliance）」的精神驅動台中藍鯨不斷向前邁進。',
  },
}

export interface VisionItem {
  kicker: string
  titleZh: string
  textZh: string
}

/** tcrfc 維持既有雙欄版；bw 逐字節錄 club-profile.md §5 五節「行動目標」句（未改寫內文）。 */
export const VISION_ITEMS: ClubText<VisionItem[]> = {
  tcrfc: [
    { kicker: 'VISION', titleZh: '願景', textZh: '從台中出發，培育本土選手邁向職業舞台，成為在地榮耀的來源。' },
    { kicker: 'MISSION', titleZh: '使命', textZh: '以扎實的訓練體系與國際連結，讓世界看見台灣足球。' },
  ],
  bw: [
    {
      kicker: '01',
      titleZh: '無止盡的探索',
      textZh: '提昇及普及大台中足球水準，吸收更專業精進足球技術，追上亞洲足球技術水平迎接世界潮流。',
    },
    {
      kicker: '02',
      titleZh: '不怕難的堅韌',
      textZh: '創造足球運動文化與風氣，以球迷為本讓足球比賽呈現更有水準，場內技術提升，場外足球比賽氛圍更加提升。',
    },
    {
      kicker: '03',
      titleZh: '更細膩的態度',
      textZh: '增加足球選手發展管道，讓選手有更好的發展空間，家長支持，學校支持，政府支持，產業支持，民眾支持。',
    },
    {
      kicker: '04',
      titleZh: '最真實的影響',
      textZh: '建立台中為台灣足球之都的美名與榮耀，健康正向的足球風氣，連結喜愛足球運動的球迷及選手所追求的足球夢想。',
    },
    {
      kicker: '05',
      titleZh: '更深遠之目的',
      textZh: '持續回饋社會，致力為人們創造更美好的生活，深信我們能帶來改變，幫助人們以全新的方式彼此分享與連結，讓世界更加和諧。',
    },
  ],
}

// 2.3 足球理念 / 俱樂部口號與培訓精神
export const PHILOSOPHY_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '足球理念 Our Philosophy｜關於台中磐石｜台中磐石足球俱樂部',
    description: '台中磐石足球俱樂部的足球理念與五大核心價值：以球員為本、追求卓越、國際發展、社區共好、誠信專業。',
  },
  bw: {
    title: '俱樂部口號與培訓精神｜關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊的俱樂部口號與培訓精神，以及隊徽「藍鯨」象徵的設計理念。',
  },
}

export const PHILOSOPHY_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '足球理念',
    h1En: 'Our Philosophy',
    lede: '透過專業模式，培育選手追求卓越，讓世界看見台灣足球。',
  },
  bw: {
    h1Zh: '俱樂部口號與培訓精神',
    h1En: null,
    lede: '以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，重視團隊合作。',
  },
}

/** 逐字節錄 content/blue-whale/club-profile.md §3，舊站以圖片呈現、同時附可選取文字版。 */
export const PHILOSOPHY_QUOTES_BW = {
  crestZh:
    '以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態、重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。',
  sloganZh:
    '藍色的天空是我們心中夢想的方向，閃爍的陽光是走向夢想的力量，草地上揮灑汗水是成長過往 堅定信仰，有你在身旁 就不再徬徨，此時此刻，我們與我們的球迷站在一起。一起迎向世界。',
  spiritZh:
    '別害怕 勇敢去闖，邁開步伐乘風破浪，就算遍體鱗傷 也要逆風飛翔，抬起頭 夢在前方，越過那重重的高牆 沒有誰能阻擋，眼神越是發光，世界都是我的舞台。',
}

// 2.4 團隊成員（教練團與顧問——僅 hero／SEO 進資料層，名單本身走既有 API，見 our-people.vue）
export const OUR_PEOPLE_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '團隊成員 Our People｜關於台中磐石｜台中磐石足球俱樂部',
    description: '認識台中磐石足球俱樂部的教練團與顧問團隊：總教練、教練、守門員教練、體能教練、青訓總監、青訓教練與技術顧問。',
  },
  bw: {
    title: '團隊成員｜關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '認識台中藍鯨女子足球隊的教練團隊。',
  },
}

export const OUR_PEOPLE_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '團隊成員',
    h1En: 'Our People',
    lede: '支撐台中磐石一線隊運作的教練團與顧問團隊。點選任一成員可查看詳細資料。',
  },
  bw: {
    h1Zh: '團隊成員',
    h1En: null,
    lede: '支撐台中藍鯨一線隊運作的教練團隊。名單由後台維護，內容更新中。',
  },
}

// 2.5 治理與管理（現行雙方皆無公開文件，只有 hero eyebrow／SEO 需要 club 名稱）
export const GOVERNANCE_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '治理與管理 Governance｜關於台中磐石｜台中磐石足球俱樂部',
    description: '台中磐石足球俱樂部治理相關資訊與公開文件下載區，內容持續更新中。',
  },
  bw: {
    title: '治理與管理｜關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊治理相關資訊與公開文件下載區，內容持續更新中。',
  },
}

export const GOVERNANCE_HERO: ClubText<HeroCopy> = {
  tcrfc: { h1Zh: '治理與管理', h1En: 'Governance', lede: '俱樂部治理相關資訊將陸續公布，以下為目前可提供的公開文件。' },
  bw: { h1Zh: '治理與管理', h1En: null, lede: '俱樂部治理相關資訊將陸續公布，以下為目前可提供的公開文件。' },
}

// 2.6 生態系
export const ECOSYSTEM_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '生態系 TCRFC Ecosystem｜關於台中磐石｜台中磐石足球俱樂部',
    description: '台中磐石足球俱樂部的四大體系：一線隊、台中磐石足球學院、課程與活動、女子足球。點選圖示前往各體系頁面。',
  },
  bw: {
    title: '生態系｜關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '台中藍鯨的三大體系：一線隊、青年隊、推廣活動。點選圖示前往各體系頁面。',
  },
}

export const ECOSYSTEM_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '生態系',
    h1En: 'TCRFC Ecosystem',
    lede: '台中磐石以一線隊為核心，向下延伸學院、課程與女子足球，構成完整的足球培育生態系。點選任一體系前往該頁。',
  },
  bw: {
    h1Zh: '生態系',
    h1En: null,
    lede: '台中藍鯨以一線隊為核心，向下延伸青年隊與推廣活動，構成完整的足球培育生態系。點選任一體系前往該頁。',
  },
}

export interface EcoNode {
  num: string
  /**
   * 語意化的 CSS 修飾字（class 會拼成 `eco-node--${slug}`）。🔴 S0-9k 修正：舊版直接用
   * `num`（3／4／5／6）拼 class，跟 mockup 的 `eco-node--club`／`--academy`／`--programs`／
   * `--womens` 對不上——純粹是 DOM 命名忠實度問題（`tcrfc.css` 與頁內 `<style>` 都沒有任何
   * 選擇器吃這幾個 class，grep 零命中，不影響視覺），不是文案，不受紀律 11 的內容規範約束。
   */
  slug: string
  enLabel: string
  zhLabel: string
  descZh: string
  /**
   * 只有磐石「女子足球」節點有的官網入口徽章（`<span class="badge">`，嵌在
   * `eco-node__desc` 內、緊接在 descZh 文字後面）。藍鯨沒有這個節點，其餘節點皆無徽章。
   */
  badgeZh?: string
  href: string
}

/** 藍鯨拿掉自我指涉節點（女子足球→本站），04 依 docs/13 §3 改為青年隊。
 * 「一線隊」「學院」兩個節點的 `descZh` 含聯賽名稱／梯隊代碼事實，改為工廠函式（S1-12d 收尾第二輪）。 */
export function getEcosystemNodes(club: string, facts: SiteFacts): EcoNode[] {
  if (normalizeClub(club) === 'bw') {
    return [
      { num: '3', slug: 'club', enLabel: 'First Team', zhLabel: '一線隊', descZh: `出戰${facts.league.nameZh}的球隊本體。`, href: '/zh/club/' },
      { num: '4', slug: 'youth', enLabel: 'Youth', zhLabel: '青年隊', descZh: `${squadCodesLabel(facts)} 青少年女子足球隊，銜接一線隊的青訓體系。`, href: '/zh/academy/' },
      { num: '5', slug: 'programs', enLabel: 'Programs', zhLabel: '推廣活動', descZh: '社區足球學校、運動熱區課程、教練講習與足球節。', href: '/zh/programs/' },
    ]
  }
  return [
    { num: '3', slug: 'club', enLabel: 'Football Club', zhLabel: '一線隊', descZh: `征戰${facts.league.nameZh}的球隊本體，代號 First Team / 一線隊。`, href: '/zh/club/' },
    { num: '4', slug: 'academy', enLabel: 'Academy', zhLabel: '台中磐石足球學院', descZh: `${squadCodesLabel(facts)} 三個梯隊，銜接一線隊的青訓體系。`, href: '/zh/academy/' },
    { num: '5', slug: 'programs', enLabel: 'Programs', zhLabel: '課程與活動', descZh: '兒童足球訓練、夏／冬令營、專項訓練與校園社區計畫。', href: '/zh/programs/' },
    { num: '6', slug: 'womens', enLabel: "Women's Football", zhLabel: '女子足球', descZh: '台中藍鯨女子隊，設有獨立的官方網站。', badgeZh: '官網入口', href: '/zh/womens/' },
  ]
}

/**
 * 2.6 生態系頁 `<h2 id="eco-title">` 視覺隱藏標題——文字裡的「N 大」對應 ECOSYSTEM_NODES
 * 節點數（磐石 4 個、藍鯨 3 個），是對既有資料的計數描述，不是新臆測的俱樂部事實，
 * 不受紀律 11「藍鯨文案只能引用舊站原文」的限制（那條管的是俱樂部自己的敘事與事實，
 * 不是這種依資料筆數而定的功能性標題）。🔴 S0-9k 修正：磐石那份原值應為「四大體系圖解」，
 * 舊版被改寫成通用的「體系圖解」（回歸，見 docs/18-work-errors.md）。
 */
export const ECOSYSTEM_TITLE: ClubText<string> = {
  tcrfc: '四大體系圖解',
  bw: '三大體系圖解',
}

// 2.7 俱樂部歷程
/** tcrfc 版 `description` 含成立年份事實，改為工廠函式（S1-12d 收尾第二輪）；bw 版全靜態，
 * 但保持同一個函式簽章（`(club, facts)`），呼叫端不必判斷「這個鍵到底要不要傳 facts」。 */
export function getHistorySeo(club: string, facts: SiteFacts): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '俱樂部歷程｜關於台中藍鯨｜台中藍鯨女子足球隊',
      description: '台中藍鯨女子足球隊 2014～2025 年逐年沿革，整理自舊官網公開內容。',
    }
  }
  return {
    title: '俱樂部歷程 Club History｜關於台中磐石｜台中磐石足球俱樂部',
    description: `台中磐石足球俱樂部的圖文歷史敘事，記錄俱樂部自 ${facts.foundedYear} 年成立以來的發展歷程。完整內文正在整理中。`,
  }
}

export const HISTORY_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '俱樂部歷程',
    h1En: 'Club History',
    // 含既有 mockup 的內嵌連結標記，history.vue 用 v-html 渲染（沿用既有做法，非新增風險）。
    lede: '以圖文方式記錄台中磐石足球俱樂部的發展歷程。逐年重要大事，可先參考 <a href="/zh/about/milestones/" style="color:#fff;text-decoration:underline;">2.8 重要里程碑</a> 時間軸。',
  },
  bw: {
    h1Zh: '俱樂部歷程',
    h1En: null,
    lede: '台中藍鯨女子足球隊 2014 年成立至今的逐年沿革，整理自舊官網公開內容。',
  },
}

export interface HistoryYear {
  year: string
  itemsZh: string[]
}

/**
 * 逐字節錄 content/blue-whale/club-profile.md §4「沿革 HISTORY（2014–2025，原文照錄）」，
 * 未增刪、未改寫任一條目。原文已知的屆數矛盾（2017 寫「第三屆」應為第四屆、2021 寫
 * 「第八屆」跳過第七屆）依 content/blue-whale/README.md 紀律 1「照抄不改寫」原樣保留，
 * 於頁面上以一句來源說明（非本檔內容）提示待客戶確認，不在此處另加註解文字。
 */
export const HISTORY_YEARS_BW: HistoryYear[] = [
  { year: '2014', itemsZh: ['籌組台中藍鯨女子足球隊參加木蘭聯賽', '參加第一屆台灣木蘭足球聯賽', 'FB 粉絲人數 1200 人', '台灣體育運動大學小型人工草足球場完成'] },
  { year: '2015', itemsZh: ['參加第二屆台灣木蘭足球聯賽', '協助台中市五權國中女足隊成立', '承接辦理 D 級教練講習'] },
  { year: '2016', itemsZh: ['參加第三屆台灣木蘭足球聯賽', '成立中部菁英女子訓練站', '首次舉辦 AFC 女子足球節'] },
  {
    year: '2017',
    itemsZh: [
      '參加第三屆台灣木蘭足球聯賽',
      '舉辦第一屆藍鯨盃足球賽',
      '菁英女子足球訓練站改名中部訓練站',
      '聘請 JFA S 級教練 堀野博幸擔任一線隊總教練',
      '舉辦地區性教練講習',
      '台中北屯太原足球場啟用',
      '隊史第一座台灣木蘭聯賽冠軍',
      'FB 粉絲人數達 6500 人',
    ],
  },
  {
    year: '2018',
    itemsZh: [
      '參加第四屆台灣木蘭足球聯賽',
      '成立台中藍鯨足球學校',
      '中部訓練站更名為藍鯨中部足球訓練站',
      '第一位職業選手包欣玄加入台中藍鯨',
      '協助台中市惠文高中成立女足隊成立',
      '隊史第二座台灣木蘭聯賽冠軍',
    ],
  },
  {
    year: '2019',
    itemsZh: [
      '參加第五屆台灣木蘭足球聯賽',
      '守門員蔡明容輸出旅外日本成功',
      '第一位日本選手田中麻帆加入',
      '爭取台中足球園區興建',
      '成立電競小隊參加 PES2020 世界盃',
      '隊史第三座台灣木蘭聯賽冠軍',
      '聯賽第一次藍鯨主場售票',
      '通過 AFC CLUB License 俱樂部認證',
      '總教練呂桂花獲得 AFC2019 草根領袖獎',
      '首次全年度主場舉辦主題日',
      '成立藍鯨女孩啦啦隊',
      '承接運動 i 台灣-運動熱區推廣計畫',
    ],
  },
  {
    year: '2020',
    itemsZh: [
      '參加第六屆台灣木蘭足球聯賽',
      '第一位香港籍選手吳卓蔚加入',
      '第一位美國籍選手瑪芮兒加入',
      '守門員程思瑜輸出旅外日本成功',
      '選手蘇育萱輸出旅外日本成功',
      '聯賽藍鯨主場售票',
      '台中藍鯨 U15 女子足球隊參加第一屆台灣青年聯賽',
      '隊史第一座台灣木蘭聯賽亞軍',
      '承接運動 i 台灣-運動熱區推廣計畫',
    ],
  },
  {
    year: '2021',
    itemsZh: [
      '參加第八屆台灣木蘭足球聯賽',
      '第二位日本籍選手日高偉織加入',
      '第一位泰國籍選手皮薩邁頌賽加入',
      '第一位泰國籍守門員納塔魯亞牧塔納維奇加入',
      '隊史第四座台灣木蘭聯賽冠軍',
      '隊史第一座台灣木蘭聯賽盃 MLC 冠軍',
      '台中藍鯨 U15 女子足球隊參加第二屆台灣青年聯賽',
      '台中藍鯨 U18 女子足球隊參加第二屆台灣青年聯賽',
      '承接運動 i 台灣-運動熱區推廣計畫',
    ],
  },
  {
    year: '2022',
    itemsZh: [
      '參加第九屆台灣木蘭足球聯賽',
      '代表台灣參加 AFC 女子足球俱樂部錦標賽(泰國)',
      '史上第三位泰國籍選手席拉萬茵樂敏加入',
      '疫情有成舉辦首場頂級足球開門賽',
      '台中藍鯨 U15 女子足球隊參加第三屆台灣青年聯賽',
      '台中藍鯨 U18 女子足球隊參加第三屆台灣青年聯賽',
      '台中藍鯨 U15 獲得第一座台灣青年聯賽 U15 女子組冠軍',
      '隊史第二座台灣木蘭聯賽亞軍',
      '承接運動 i 台灣-運動熱區推廣計畫',
    ],
  },
  {
    year: '2023',
    itemsZh: [
      '參加第十屆台灣木蘭足球聯賽',
      '台中足球園區動土並獲邀參加動土典禮',
      '選手蘇育萱輸出旅外中國成功',
      '史上第四位泰國籍選手席菲拉萬茵樂敏加入',
      '史上第五位泰國籍選手薩瓦拉克彭甘加入',
      'FB 粉絲人數達 16500 人',
      '隊史第五座台灣木蘭聯賽冠軍',
      '承接運動 i 台灣-運動熱區推廣計畫',
    ],
  },
  {
    year: '2024',
    itemsZh: [
      '參加第十一屆台灣木蘭足球聯賽',
      '成立台中藍鯨 U10 女子隊',
      '台中藍鯨 U10 女子隊首次參加臺中市市長盃',
      '獲邀參加陽信盃國際邀請賽並獲得冠軍',
      '代表台灣參加 24/25 年亞足聯女子冠軍聯賽並順利小組晉級',
      '隊史第三座台灣木蘭聯賽亞軍',
      '史上第六位泰國籍選手第二位守門員瓦拉邦汶廷加入',
      '隊史第二位外籍選手薩瓦拉克彭甘獲得台灣木蘭足球聯賽年度金靴獎',
      '承接運動 i 台灣-運動熱區推廣計畫',
    ],
  },
  {
    year: '2025',
    itemsZh: [
      '參加第十二屆台灣木蘭足球聯賽',
      '代表台灣參加 2024 年至 2025 年亞足聯女子冠軍聯賽 8 強賽獲得亞洲前 8 名成績',
      '2025 台灣總統盃足球錦標賽亞軍',
      '史上第七位泰國籍選手第三位守門員邱瑪尼-通蒙戈加入',
      '史上第二位泰國籍選手冼仲意加入',
      '總教練呂桂花獲得 AFC 亞足聯亞洲最佳女足隊教練提名',
      '球衣首次放上公益團體機關台中惠明盲校',
      '承接運動 i 台灣-運動熱區推廣計畫',
      '首次接受英國世界足球雜誌專訪',
    ],
  },
]

// 2.8 重要里程碑（藍鯨這一輪不重建時間軸元件，改指向 2.7 俱樂部歷程；只換 hero／SEO）
export const MILESTONES_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '重要里程碑 Key Milestones｜關於台中磐石｜台中磐石足球俱樂部',
    description: '台中磐石足球俱樂部 2024～2026 年重要大事記時間軸，包含成軍、奪冠、國際合作備忘錄簽署與新血加盟等紀錄，支援年份篩選。',
  },
  bw: {
    title: '重要里程碑｜關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊的年度大事記，請見「俱樂部歷程」頁的 2014～2025 逐年沿革。',
  },
}

export const MILESTONES_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '重要里程碑',
    h1En: 'Key Milestones',
    lede: '2024 年成立至今，台中磐石一步步建立起一線隊戰績、國際合作網絡與在地公益足跡。以下時間軸整理自俱樂部已發布消息，可依年份篩選查看。',
  },
  bw: {
    h1Zh: '重要里程碑',
    h1En: null,
    lede: '台中藍鯨的年度大事記目前收錄在「俱樂部歷程」頁的 2014～2025 逐年沿革，本頁的年份篩選時間軸尚未依藍鯨資料重建。',
  },
}

// ---------------------------------------------------------------------------
// 03 一線隊（只做靜態敘事文案；球員／教練／賽程名單屬動態內容，不進本檔）
// ---------------------------------------------------------------------------

/** 03 一線隊 SEO／Hero／球隊介紹——皆含聯賽名稱／成立年份／主場事實，改為工廠函式（S1-12d 收尾第二輪）。 */
export function getFirstTeamSeo(club: string, facts: SiteFacts): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '一線隊｜台中藍鯨女子足球隊',
      description: `台中藍鯨女子足球隊一線隊：出戰${facts.league.nameZh}，隊史五度奪冠。名單與賽程由後台維護，內容更新中。`,
    }
  }
  return {
    title: '一線隊 First Team｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC',
    description: `台中磐石足球俱樂部一線隊（First Team）：28 名註冊球員名單依背號排序、教練團陣容、2026/27 ${facts.league.nameZh}完整賽程與 .ics 訂閱、榮譽紀錄時間軸。`,
  }
}

export function getFirstTeamHero(club: string, facts: SiteFacts): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '一線隊',
      h1En: null,
      lede: `台中藍鯨一線隊代表俱樂部出戰${facts.league.nameZh}，${facts.foundedDisplayZh}，隊史五度奪得聯賽冠軍。主場為${facts.venues.map((v) => v.nameZh).join('、')}。`,
    }
  }
  return {
    h1Zh: '一線隊',
    h1En: 'First Team',
    lede: `台中磐石一線隊代表俱樂部出戰${facts.league.nameZh}，是所有青訓與學院球員最終銜接的競技舞台。球隊 ${facts.foundedDisplayZh}，同年即拿下${facts.foundingTitleZh}，主場為${primaryVenueOf(facts).nameZh}。`,
  }
}

/** 對應 mockup「球隊介紹」段落——藍鯨版逐句改寫自 club-profile.md §1 已核實事實（成立年、聯賽名、主場），不臆測名次或賽季戰績。 */
export function getFirstTeamIntro(club: string, facts: SiteFacts): string {
  if (normalizeClub(club) === 'bw') {
    return `台中藍鯨一線隊於 ${facts.foundedYear} 年隨俱樂部創立成軍，出戰${facts.league.nameZh}，隊史累計五度奪得聯賽冠軍（2017、2018、2019、2021、2023）。球隊主場為${facts.venues.map((v) => v.nameZh).join('、')}。名單與最新賽程由後台維護，本頁內容更新中。`
  }
  return `台中磐石足球俱樂部一線隊於 ${facts.foundedYear} 年隨俱樂部創立成軍，同年奪下${facts.foundingTitleZh}，現於${facts.league.nameZh}出賽。球隊主場設於${primaryVenueOf(facts).nameZh}，2026/27 賽季共排定 21 場企甲例行賽。`
}

// ---------------------------------------------------------------------------
// 04 學院／青年隊（S1-15 新增）
//
// 🔴 藍鯨規劃書 §3.4：「04 青年隊沿用主站 04 的梯隊版型，但不沿用招生與課程報名
// 架構」——本節只涵蓋 4.1 總覽與 4.2 隊伍兩頁，4.7 加入學院對藍鯨整頁關閉
// （見 shared/utils/units.ts BLUE_WHALE_DISABLED_UNITS），不需要 bw 版文案。
// 藍鯨文案逐句節錄、改寫自 content/blue-whale/squad/youth-teams.md 舊站原文
// （紀律 11：藍鯨文案只能引用既有舊站內容，不得自行創作）。
// ---------------------------------------------------------------------------

/** bw 版含梯隊代碼事實，tcrfc 版全靜態，改為工廠函式（S1-12d 收尾第二輪，理由同 getHistorySeo）。 */
export function getAcademyOverviewSeo(club: string, facts: SiteFacts): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '青年隊總覽｜台中藍鯨女子足球隊',
      description: `台中藍鯨青年隊由 ${squadCodesLabel(facts, '、')} 女子足球隊組成，銜接一線隊的競技體系。招生方式與課表待確認，詳情請洽俱樂部。`,
    }
  }
  return {
    title: '學院總覽 Academy Overview｜台中磐石足球學院｜台中磐石足球俱樂部',
    description: '認識台中磐石足球學院的定位與訓練基地：銜接俱樂部品牌主張的青訓體系，以及學員數、教練數、升學率等數據亮點（資料收集中）。',
  }
}

export function getAcademyOverviewHero(club: string, facts: SiteFacts): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '青年隊總覽',
      h1En: null,
      lede: `台中藍鯨青年隊由 ${squadCodesLabel(facts, '、')} 女子足球隊組成，是銜接一線隊競技體系的梯隊。`,
    }
  }
  return {
    h1Zh: '學院總覽',
    h1En: 'Academy Overview',
    lede: '台中磐石足球學院是台中磐石足球俱樂部的青訓體系，銜接俱樂部「在地扎根．放眼世界」的品牌主張與 Players First、Excellence、Global Pathways、Community、Integrity 五大核心價值，以分齡訓練陪伴學員成長。',
  }
}

/** 學院／青年隊定位段落。藍鯨版直接節錄 youth-teams.md「隊伍定位（原文）」——
 * 該段原文寫在 U15 隊頁面下，但敘述對象是整個藍鯨女足梯隊體系，用於總覽頁定位段落
 * 語意相符，未新增文字。tcrfc 版含成立年份事實，改為工廠函式（S1-12d 收尾第二輪）。 */
export function getAcademyPositioning(club: string, facts: SiteFacts): string {
  if (normalizeClub(club) === 'bw') {
    return '台中藍鯨是台灣最頂尖的女子足球俱樂部，球隊歷年造就了高達 21 位中華女足代表隊國手。青年隊希望青出於藍、更勝於藍，擴大學員足球未來的可能性（節錄自台中藍鯨官方網站青年隊招募原文）。'
  }
  return `台中磐石足球學院自俱樂部 ${facts.foundedYear} 年成立起，作為銜接社區足球與競技體系的橋樑，目標是讓每一位學員都能在扎實的訓練環境中，依照自身節奏發展技術、戰術理解與品格，並為有能力銜接一線隊或海外舞台的球員，提供清晰可循的成長路徑。`
}

/** 4.2 學院隊伍／青年隊 SEO／Hero——皆含梯隊代碼事實，改為工廠函式（S1-12d 收尾第二輪）。 */
export function getAcademyTeamsSeo(club: string, facts: SiteFacts): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: `青年隊 ${squadCodesLabel(facts)}｜台中藍鯨女子足球隊`,
      description: `台中藍鯨青年隊 ${squadCodesLabel(facts, '、')} 女子足球隊——各隊名單、教練與賽程（資料收集中）。`,
    }
  }
  return {
    title: '學院隊伍 Our Teams｜台中磐石足球學院｜台中磐石足球俱樂部',
    description: `台中磐石足球學院 ${squadCodesLabel(facts)} 及其他年齡層隊伍——各梯隊名單、教練、賽程與成績（資料收集中），並提供訂閱本隊行事曆功能。`,
  }
}

export function getAcademyTeamsHero(club: string, facts: SiteFacts): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '青年隊',
      h1En: null,
      lede: `台中藍鯨青年隊依年齡分為 ${squadCodesLabel(facts, '、')} 女子足球隊，各隊名單、教練與賽程如下。`,
    }
  }
  return {
    h1Zh: '學院隊伍',
    h1En: 'Our Teams',
    lede: `台中磐石足球學院依年齡分為 ${squadCodesLabel(facts, '、')} 及其他年齡層梯隊，各隊皆設有專屬名單、教練、賽程與成績頁面，並可訂閱該隊行事曆，掌握每一場訓練與比賽。`,
  }
}

/** 4.2 梯隊分頁定義：磐石四個分頁（U15／U14／U12／其他年齡層），藍鯨只有兩個實際隊伍
 * （docs/12 §2b 種子資料：`BW-U15`／`BW-U12`，沒有 U14、沒有「其他年齡層」）。
 * `teamCode: null` 代表「其他年齡層」這種沒有對應 `Team.code` 的靜態說明分頁，
 * 目前只有磐石版本用得到。 */
export interface AcademyTeamTab {
  id: string
  labelZh: string
  teamCode: string | null
}
// 🔴 GEO-03（S1-12d）：年齡層代碼本身（'U15'／'U14'／'U12'）的單一來源是後端
// site-facts 端點的 `squadCodes`，這裡只疊加 API 篩選用的俱樂部代碼前綴與
// 「其他年齡層」這個沒有對應 `Team.code` 的靜態分頁，不重新打一份年齡層清單。
// 改為工廠函式（S1-12d 收尾第二輪）——`facts` 由呼叫端 `useSiteFacts(club)` 取得。
export function getAcademyTeamTabs(club: string, facts: SiteFacts): AcademyTeamTab[] {
  if (normalizeClub(club) === 'bw') {
    return facts.squadCodes.map((code) => ({ id: code.toLowerCase(), labelZh: code, teamCode: `BW-${code}` }))
  }
  return [
    ...facts.squadCodes.map((code) => ({ id: code.toLowerCase(), labelZh: code, teamCode: code })),
    { id: 'other', labelZh: '其他年齡層', teamCode: null },
  ]
}

// ---------------------------------------------------------------------------
// 04 單元 hub（academy/index.vue）——BW-C1 品牌外洩全站盤點新增（整頁固定磐石內容，
// 是本輪全站掃描才發現的既有缺口，不在先前任何一輪記錄範圍內）。版型不變，
// SEO／Hero／七張導覽卡改依俱樂部切換；4.7（加入學院）對藍鯨仍是 units.ts 明文關閉
// 的單元（藍鯨規劃書 §3.4「04 不沿用招生與課程報名架構」），bw 版導覽卡與底部 CTA
// 對應移除，不連到會 404 的頁面。
// ---------------------------------------------------------------------------

export function getAcademyHubSeo(club: string, facts: SiteFacts): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '青年隊｜台中藍鯨女子足球隊',
      description: `台中藍鯨青年隊由 ${squadCodesLabel(facts, '、')} 女子足球隊組成，銜接一線隊的競技體系，提供分齡訓練與教練團陪伴。`,
    }
  }
  return {
    title: '台中磐石足球學院 TCRFC Academy｜台中磐石足球俱樂部',
    description: '台中磐石足球學院是台中磐石足球俱樂部的青訓體系，提供 U12 至 U15 分齡訓練、清晰的發展路徑與教練團陪伴，銜接一線隊與海外舞台。',
  }
}

export function getAcademyHubHero(club: string, facts: SiteFacts): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '青年隊',
      h1En: null,
      lede: `台中藍鯨青年隊由 ${squadCodesLabel(facts, '、')} 女子足球隊組成，是銜接一線隊競技體系的梯隊，透過分齡訓練與教練團的長期陪伴，協助學員逐步成長。`,
    }
  }
  return {
    h1Zh: '台中磐石足球學院',
    h1En: 'TCRFC Academy',
    lede:
      '台中磐石足球學院承接俱樂部「在地扎根．放眼世界」的品牌主張，透過分齡訓練、清晰的發展路徑與教練團的長期陪伴，' +
      '協助學員從基礎技術一路成長，銜接一線隊或海外舞台。',
  }
}

export interface AcademyHubCard {
  num: string
  titleZh: string
  titleEn: string
  descZh: string
  href: string
}

/** 04 hub 導覽卡——tcrfc 逐字沿用既有 7 張卡（4.2 描述含梯隊代碼事實，等價於改動前
 * `{{ tcrfcAcademyLabel() }} 各梯隊名單、教練與賽程`，這裡改讀 `squadCodesLabel(facts)`
 * 單一來源，不重複寫一份）；bw 版只有 6 張（4.7 對藍鯨關閉，見 units.ts），標題一律
 * 用「青年隊」不用「學院」，4.3 描述拿掉「／海外」（bw 的 4.3 頁面本身只承諾
 * U12 → U15 → 一線隊，見 getAcademyPathwayHero bw 分支，不重複宣稱海外路徑）。 */
export function getAcademyHubCards(club: string, facts: SiteFacts): AcademyHubCard[] {
  if (normalizeClub(club) === 'bw') {
    return [
      { num: '4.1', titleZh: '青年隊總覽', titleEn: 'Academy Overview', descZh: '認識青年隊定位與整體樣貌', href: '/zh/academy/overview/' },
      { num: '4.2', titleZh: '青年隊隊伍', titleEn: 'Our Teams', descZh: `${squadCodesLabel(facts)} 各梯隊名單、教練與賽程`, href: '/zh/academy/teams/' },
      { num: '4.3', titleZh: '青年隊發展路徑', titleEn: 'Academy Pathway', descZh: '從 U12 到 U15、一線隊的成長路徑', href: '/zh/academy/pathway/' },
      { num: '4.4', titleZh: '訓練課程與課綱', titleEn: 'Training & Curriculum', descZh: '技術、戰術、體能、比賽判讀與品格五大面向', href: '/zh/academy/curriculum/' },
      { num: '4.5', titleZh: '青年隊教練團', titleEn: 'Coaches', descZh: '認識帶領各梯隊的教練團隊', href: '/zh/academy/coaches/' },
      { num: '4.6', titleZh: '青年隊生活', titleEn: 'Academy Life', descZh: '訓練、比賽與活動的日常紀錄', href: '/zh/academy/life/' },
    ]
  }
  return [
    { num: '4.1', titleZh: '學院總覽', titleEn: 'Academy Overview', descZh: '認識學院定位、訓練基地與整體樣貌', href: '/zh/academy/overview/' },
    { num: '4.2', titleZh: '學院隊伍', titleEn: 'Our Teams', descZh: `${squadCodesLabel(facts)} 各梯隊名單、教練與賽程`, href: '/zh/academy/teams/' },
    { num: '4.3', titleZh: '學院發展路徑', titleEn: 'Academy Pathway', descZh: '從 U12 到一線隊／海外的成長路徑', href: '/zh/academy/pathway/' },
    { num: '4.4', titleZh: '訓練課程與課綱', titleEn: 'Training & Curriculum', descZh: '技術、戰術、體能、比賽判讀與品格五大面向', href: '/zh/academy/curriculum/' },
    { num: '4.5', titleZh: '學院教練團', titleEn: 'Coaches', descZh: '認識帶領各梯隊的教練團隊', href: '/zh/academy/coaches/' },
    { num: '4.6', titleZh: '學院生活', titleEn: 'Academy Life', descZh: '訓練、比賽與活動的日常紀錄', href: '/zh/academy/life/' },
    { num: '4.7', titleZh: '加入學院', titleEn: 'Join the Academy', descZh: '招生對象、遴選流程與線上申請', href: '/zh/academy/join/' },
  ]
}

/** 底部 CTA 標題——bw 版不提「查看招生資訊」按鈕（4.7 對藍鯨關閉），呼叫端據此
 * 決定要不要渲染第二顆按鈕。 */
export function getAcademyHubCtaTitle(club: string): string {
  if (normalizeClub(club) === 'bw') {
    return '準備好加入台中藍鯨青年隊了嗎？'
  }
  return '準備好加入台中磐石足球學院了嗎？'
}

// ---------------------------------------------------------------------------
// 10 加入與聯絡
// ---------------------------------------------------------------------------

export const JOIN_INDEX_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '加入與聯絡 Join / Contact｜台中磐石足球俱樂部',
    description:
      '台中磐石足球俱樂部加入與聯絡總覽：加入球隊、加入學院／兒童訓練、營隊報名、國際球員詢問、合作夥伴與贊助洽詢、媒體詢問、一般聯絡七種表單，以及場地位置與聯絡資訊。',
  },
  bw: {
    title: '加入與聯絡｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊加入與聯絡：加入球隊、加入青年隊、合作夥伴洽詢、媒體詢問、一般聯絡，以及聯絡資訊。',
  },
}

export const JOIN_INDEX_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '加入與聯絡',
    h1En: 'Join / Contact',
    lede: '不論你是想加入球隊的球員、想讓孩子接受系統化訓練的家長，還是想與台中磐石合作的企業與媒體，都可以在這裡找到對應的表單。七種表單各自送達不同部門，我們會盡快與你聯繫。',
  },
  bw: {
    h1Zh: '加入與聯絡',
    h1En: null,
    lede: '不論你是想加入球隊的球員、想讓孩子加入青年隊的家長，還是想與台中藍鯨合作的企業與媒體，都可以在這裡找到對應的表單。',
  },
}

// ---------------------------------------------------------------------------
// 04 學院／青年隊 4.3／4.4（S2-8 新增，派工指示要求一併處理的既有品牌外洩）
//
// 🔴 兩頁改動前全部既有內容都是「準備中」通用佔位文字，沒有任何磐石專屬真實事實
// （人名／照片／具名系統），只需換抬頭字樣與 CTA 連結，不需要臆造新內容——與
// 4.5／4.6（真實教練／真實照片，關閉不開放）不同，見 shared/utils/units.ts 說明。
// ---------------------------------------------------------------------------

export function getAcademyPathwaySeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '青年隊發展路徑｜台中藍鯨女子足球隊',
      description: '台中藍鯨青年隊的階梯式發展路徑：U12 → U15 → 一線隊，點擊各階段了解升上一階的方向（詳細內容資料收集中）。',
    }
  }
  return {
    title: '學院發展路徑 Academy Pathway｜台中磐石足球學院｜台中磐石足球俱樂部',
    description: '台中磐石足球學院的階梯式發展路徑：U12 → U15 → 一線隊／海外，點擊各階段了解升上一階的方向（詳細內容資料收集中）。',
  }
}

export function getAcademyPathwayHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '青年隊發展路徑',
      h1En: null,
      lede: '從 U12 起步，經 U15 深化，到一線隊——台中藍鯨提供階梯式的發展路徑。點擊下方各階段，了解學員如何逐步銜接下一個層級。',
    }
  }
  return {
    h1Zh: '學院發展路徑',
    h1En: 'Academy Pathway',
    lede: '從 U12 起步，經 U15 深化，到一線隊或海外舞台——台中磐石足球學院提供階梯式的發展路徑。點擊下方各階段，了解學員如何逐步銜接下一個層級。',
  }
}

export function getAcademyCurriculumSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '訓練課程與課綱｜台中藍鯨女子足球隊',
      description: '台中藍鯨青年隊訓練涵蓋技術、戰術、體能、比賽判讀與品格五大面向，各面向課綱與週期規劃表資料收集中。',
    }
  }
  return {
    title: '訓練課程與課綱 Training & Curriculum｜台中磐石足球學院｜台中磐石足球俱樂部',
    description: '台中磐石足球學院訓練涵蓋技術、戰術、體能、比賽判讀與品格五大面向，各面向課綱與週期規劃表資料收集中。',
  }
}

export function getAcademyCurriculumHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '訓練課程與課綱',
      h1En: null,
      lede: '技術、戰術、體能、比賽判讀、品格——台中藍鯨青年隊的訓練圍繞五大面向展開，每個面向都有對應的課綱與週期規劃表。',
    }
  }
  return {
    h1Zh: '訓練課程與課綱',
    h1En: 'Training & Curriculum',
    lede: '技術、戰術、體能、比賽判讀、品格——台中磐石足球學院的訓練圍繞五大面向展開，每個面向都有對應的課綱與週期規劃表。',
  }
}

/** 10.2 卡片標籤——藍鯨依 docs/13 §3 用「青年隊」，不沿用磐石學院的招生用詞。 */
export function getJoinAcademyCard(club: string, facts: SiteFacts): { titleZh: string; descZh: string } {
  if (normalizeClub(club) === 'bw') {
    return { titleZh: '加入青年隊', descZh: `${squadCodesLabel(facts)} 青少年女子足球隊招募，報名資格與費用請洽俱樂部。` }
  }
  return { titleZh: '加入學院／兒童訓練', descZh: `學院 ${squadCodesLabel(facts)} 梯隊，或兒童訓練的混齡、初學、技巧發展班，同一份表單完成報名。` }
}

/**
 * 10.4 國際球員詢問卡片英文說明——磐石有確認的英文縮寫可用（TCRFC），藍鯨的英文正式全名
 * 尚待客戶確認（`identity.brandTagEn` 對藍鯨一律 null，docs/13-blue-whale-site.md §5
 * 第 2 項、踩雷點 17），不得自行挑一個填進句子裡，改沿用原句已有的中性寫法「for us」。
 * 🔴 S0-9k 修正：舊版把這句英文寫死成「for us」套用給兩家，磐石那份因此遺失 mockup
 * 原文裡的「TCRFC」（回歸，見 docs/18-work-errors.md）。
 */
export const JOIN_INTL_DESC: ClubText<string> = {
  tcrfc: 'Interested in playing for TCRFC in Taiwan? Tell us about yourself and your football background.',
  bw: 'Interested in playing for us in Taiwan? Tell us about yourself and your football background.',
}

export const JOIN_CONTACT_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '聯絡資訊 Contact Information｜加入與聯絡｜台中磐石足球俱樂部',
    description: '台中磐石足球俱樂部聯絡資訊：電話、Email、地址、營業時間、各部門分機與社群連結。',
  },
  bw: {
    title: '聯絡資訊｜加入與聯絡｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊聯絡資訊：Email、LINE 官方帳號與社群連結。',
  },
}

export const JOIN_CONTACT_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '聯絡資訊',
    h1En: 'Contact Information',
    // 含既有 mockup 的內嵌連結標記，contact/index.vue 用 v-html 渲染（比照 history.vue 的做法）。
    lede: '電話、Email、地址、營業時間與各部門分機，方便你依需求找到對應窗口。若是特定申請或洽詢，建議直接使用<a href="/zh/join/">對應的表單</a>，處理速度會更快。',
  },
  bw: {
    h1Zh: '聯絡資訊',
    h1En: null,
    lede: 'Email、LINE 官方帳號與社群連結如下。若是特定申請或洽詢，建議直接使用<a href="/zh/join/">對應的表單</a>，處理速度會更快。',
  },
}

// ---------------------------------------------------------------------------
// 03.3 球員機會 Player Opportunities（S2-8 新增）
//
// 🔴 藍鯨規劃書 §3.3「一線隊」只明文「沿用主站 03 的球員卡、球員頁與球員故事版型」，
// 沒有點名球員機會（試訓／外籍球員招募）——但總則明文「主站有的功能，本站就有；
// 主站沒有的，本站也不做」「總則的例外只有四項單元取捨，四項以外不得另行設計」
// （§1.3 行 24、92），本頁不在四項例外之列，故維持開放，內容改為藍鯨版。
// 「歡迎外籍球員」的邀請文字不是臆造：club-profile.md §4 沿革明確記載藍鯨歷年
// 招募過日本、泰國、香港、美國籍球員（真實事實，只是本頁刻意不逐一列名，維持
// 原句「歡迎詢問」的邀請語氣，不是把沿革內容搬進來）。試訓場次表格兩俱樂部皆是
// 既有的通用空白狀態文字，不含任何俱樂部專屬事實，不需要分支。
// ---------------------------------------------------------------------------

export function getPlayerOpportunitiesSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '球員機會｜台中藍鯨女子足球隊',
      description: '台中藍鯨女子足球隊球員機會：加入一線隊的資格與報名方式、試訓場次列表與線上報名、外籍球員招募管道。',
    }
  }
  return {
    title: '球員機會 Player Opportunities｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC',
    description: '台中磐石足球俱樂部球員機會：加入台中磐石一線隊的資格與報名方式、試訓場次列表與線上報名、外籍球員招募管道（英文優先）。',
  }
}

export function getPlayerOpportunitiesHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '球員機會',
      h1En: null,
      lede: '從加入一線隊、參加試訓，到外籍球員的招募管道，這裡整理台中藍鯨球員機會的完整入口。',
    }
  }
  return {
    h1Zh: '球員機會',
    h1En: 'Player Opportunities',
    lede: '從加入球隊、參加試訓，到外籍球員的招募管道，這裡整理台中磐石一線隊球員機會的完整入口。',
  }
}

/** 「加入台中磐石／台中藍鯨」段落正文——依 `facts.league` 動態帶入聯賽名稱（GEO-03）。 */
export function getJoinFirstTeamBody(club: string, facts: SiteFacts): string {
  if (normalizeClub(club) === 'bw') {
    return `台中藍鯨一線隊代表俱樂部出戰${facts.league.nameZh}，持續招募具備競技實力的球員加入陣容。填寫報名表後，將由俱樂部與您聯繫後續評估流程。`
  }
  return `台中磐石一線隊代表俱樂部出戰${facts.league.nameZh}，持續招募具備競技實力的球員加入陣容。填寫報名表後，將由競技部（10.1 表單收件單位）與您聯繫後續評估流程。`
}

/** Foreign Player Recruitment 英文段落——依 `facts.league` 動態帶入聯賽名稱（GEO-03）。
 * 藍鯨版不寫「TCRFC」縮寫（`identity.brandTagEn` 對藍鯨一律 null，理由同 `JOIN_INTL_DESC`）。
 * `league.nameEn` 藍鯨現況為 `null`（木蘭聯賽尚無確認英文譯名，site-facts.ts 既有值），
 * `null` 時只用 `nameZh` 帶過，不印出字面 "null"。 */
export function getForeignPlayerBody(club: string, facts: SiteFacts): string {
  const leagueLabel = facts.league.nameEn ? `${facts.league.nameEn} (${facts.league.nameZh})` : facts.league.nameZh
  if (normalizeClub(club) === 'bw') {
    // 藍鯨 zh 頁的這段英文句維持中文聯賽名（與 B-5 定案前輸出逐字相同）；site-facts bw 現已補英文名，
    // 英文版頁面另走 club-copy-en-club.ts 的 getForeignPlayerBodyEnBw，不經過這裡。
    return `${BW_NAME_EN} First Team competes in Taiwan's ${facts.league.nameZh}. We welcome enquiries from foreign players interested in trialling or joining the squad. Please use the international enquiry form below and our club will follow up.`
  }
  return `Taichung Rock FC (TCRFC) First Team competes in Taiwan's ${leagueLabel}. We welcome enquiries from foreign players interested in trialling or joining the squad. Please use the international enquiry form below and our International Department will follow up.`
}

// ---------------------------------------------------------------------------
// 03.5 球員故事 Player Stories（S2-8 新增）
//
// 🔴 藍鯨規劃書 §3.3 明文「沿用主站 03 的球員卡、球員頁與球員故事版型」，本頁對
// 藍鯨維持開放（版型承諾沿用）。但目前沒有任何已核實、已取得肖像同意的藍鯨球員
// 故事案例可用（`docs/15-out-of-scope-record.md`／STATUS.md：球員名單與肖像同意
// 尚未到位）——藍鯨版故事清單刻意留空，不得挪用磐石球員（孫恩祈／山內大空／
// 楊朝景）充數，也不得自行編造藍鯨案例。空狀態文字不重複「台中磐石」自我指涉的
// 站外連結（tcrfc 版指向藍鯨官網的既有句子，bw 版若照搬會變成「藍鯨站告訴藍鯨
// 訪客去藍鯨官網」的自我循環，故 bw 版改寫為單純的「案例陸續建立中」）。
// ---------------------------------------------------------------------------

export function getPlayerStoriesSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '球員故事｜台中藍鯨女子足球隊',
      description: '台中藍鯨女子足球隊球員故事案例，記錄選手從加入台中藍鯨到邁向更大舞台的真實歷程。案例陸續建立中。',
    }
  }
  return {
    title: '球員故事 Player Stories｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC',
    description: '台中磐石足球俱樂部球員故事案例，可依學院、一線隊、海外、女足篩選，記錄選手從加入台中磐石到邁向更大舞台的真實歷程。',
  }
}

export function getPlayerStoriesHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '球員故事',
      h1En: null,
      lede: '每一位選手都有自己的台中藍鯨歷程。已核實、取得肖像同意的球員故事案例將陸續建立於本頁。',
    }
  }
  return {
    h1Zh: '球員故事',
    h1En: 'Player Stories',
    lede: '每一位選手都有自己的台中磐石歷程。這裡收錄學院、一線隊、海外與女足球員的真實案例，目前已建立 3 個案例，內容陸續補齊中。',
  }
}

/** 空狀態說明——藍鯨版不含「查看台中藍鯨官網」這種自我指涉連結（見上方節頭說明）。 */
export function getPlayerStoriesEmptyNote(club: string): string {
  if (normalizeClub(club) === 'bw') {
    return '目前尚無已核實、取得肖像同意的球員故事案例，內容將依球員名單與肖像同意進度陸續建立。'
  }
  return '學院與女足類別目前尚無已建立的球員故事案例。'
}

// ---------------------------------------------------------------------------
// 05.5 校園與社區 School & Community（S2-10 新增）
//
// 🔴 藍鯨版「合作學校列表」逐字節錄自 `content/blue-whale/club-profile.md` §1
// 俱樂部資訊表「建教合作」欄（舊站原文，紀律 11：只能引用既有內容，不得自行創作）。
// 「合作年度」欄只在 `content/blue-whale/club-profile.md` §4 沿革能找到明確年份時才
// 填（目前只有臺中市立五權國中對應 2015 年「協助台中市五權國中女足隊成立」一條
// 可查證），其餘四校沿革沒有逐校標明年份，維持空白（不臆測年份）。
// 「社區計畫」／「教練培訓」段落節錄自 `content/blue-whale/programs.md` 第 2、4 節。
// ---------------------------------------------------------------------------

export function getSchoolCommunitySeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '校園與社區｜台中藍鯨女子足球隊',
      description: '台中藍鯨校園合作方案與社區推廣：建教合作學校列表、運動 i 台灣社區足球推廣計畫、教練培訓，歡迎學校與社區單位洽談合作。',
    }
  }
  return {
    title: '校園與社區 School & Community｜課程與活動｜台中磐石足球俱樂部',
    description: '台中磐石校園合作方案、社區計畫與教練培訓，歡迎學校與社區單位洽談合作，填寫表單由專人聯繫。',
  }
}

export function getSchoolCommunityHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '校園與社區',
      h1En: null,
      lede: '台中藍鯨與學校、社區單位合作推廣女子足球運動，提供建教合作、社區足球推廣與教練培訓，協助培育在地師資與基層足球人口。',
    }
  }
  return {
    h1Zh: '校園與社區',
    h1En: 'School & Community',
    lede: '台中磐石與學校、社區單位合作推廣足球運動，提供校園方案、社區計畫與教練培訓，並協助培育在地師資。',
  }
}

export interface SchoolPartnerRow {
  nameZh: string
  contentZh: string
  yearZh: string | null
}

/** 藍鯨建教合作學校列表（5 校，逐字節錄自 club-profile.md §1「建教合作」欄）。 */
export const SCHOOL_PARTNERS_BW: SchoolPartnerRow[] = [
  { nameZh: '國立臺灣體育運動大學女子足球隊', contentZh: '建教合作（女子足球隊）', yearZh: null },
  { nameZh: '臺中市立五權國民中學女子足球隊', contentZh: '建教合作（女子足球隊）', yearZh: '2015 年' },
  { nameZh: '南投縣立水里國民中學女子足球隊', contentZh: '建教合作（女子足球隊）', yearZh: null },
  { nameZh: '彰化縣立永靖國民中學女子足球隊', contentZh: '建教合作（女子足球隊）', yearZh: null },
  { nameZh: '臺中市篤行國小女子足球隊', contentZh: '建教合作（女子足球隊）', yearZh: null },
]

/** 藍鯨社區計畫簡介，節錄自 programs.md §2「運動 i 台灣 2.0－運動熱區」（太原足球場）。 */
export const COMMUNITY_PROGRAM_BODY_BW =
  '台中藍鯨承接臺中市政府「運動 i 台灣 2.0」運動熱區推廣計畫（太原足球場），提供社區足球學校（暱稱「小藍鯨」）等分齡足球課程，推廣全民規律運動。'

/** 藍鯨教練培訓簡介，節錄自 programs.md §4「教練講習」（2025 年足球人才教練暨 TDS 守門員人才培訓）。 */
export const COACH_TRAINING_BODY_BW =
  '台中藍鯨曾與國立臺灣體育運動大學、臺中市政府運動局等單位合辦教練講習，培養足球專業教練人才、更新訓練觀念與知識。'

// ---------------------------------------------------------------------------
// 03.2 球員發展系統 Player Development（BW-C1 重開）
//
// 🔴 S1-15／S2-8 兩輪曾以「藍鯨沒有對應的具名『系統』框架」為由整頁 404（見
// units.ts 舊版檔頭）——這個判斷本身沒有錯（不得對藍鯨宣稱一套磐石自己的機構性
// 框架），但錯的是拿它當關閉整頁的理由：藍鯨規劃書 §1.3 總則「例外只有四項單元
// 取捨」不含 3.2，本頁應重開。八大主題（技術戰術分析、體能訓練、比賽判讀、心理
// 韌性、影片分析、IDP 個人發展計畫、營養與生活、教育與語言）是通用足球培訓詞彙，
// 不是磐石專屬機構事實，兩俱樂部可共用；每個模組的詳細內容本來就是「準備中」
// 佔位文字（磐石版也是），不需要臆造新內容。唯一改的是 SEO／Hero 文案：避免對
// 藍鯨使用「系統」這個暗示已建制完成的機構性框架用詞，改用「培育重點」。
// ---------------------------------------------------------------------------

export function getPlayerDevelopmentSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '球員培育重點｜台中藍鯨女子足球隊',
      description: '台中藍鯨球員培育重點：技術戰術分析、體能訓練、比賽判讀、心理韌性、影片分析、個人發展計畫、營養與生活、教育與語言，各面向詳細內容整理中。',
    }
  }
  return {
    title: '球員發展系統 Player Development｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC',
    description:
      '台中磐石足球俱樂部球員發展系統：技術戰術分析、體能訓練、比賽判讀、心理韌性、影片分析、IDP 個人發展計畫、營養與生活、教育與語言，八大模組完整說明。',
  }
}

export function getPlayerDevelopmentHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '球員培育重點',
      h1En: null,
      lede: '從技戰術到教育語言，八大面向是台中藍鯨球員培育關注的重點，支持一線隊與青年隊球員持續成長。點擊模組卡片展開詳細說明。',
    }
  }
  return {
    h1Zh: '球員發展系統',
    h1En: 'Player Development',
    lede: '從技戰術到教育語言，八大模組構成台中磐石球員發展系統的完整框架，支持一線隊與各梯隊球員朝職業與國際舞台邁進。點擊模組卡片展開詳細說明。',
  }
}

// ---------------------------------------------------------------------------
// 03.4 國際發展通道 International Pathways（BW-C1 重開）
//
// 🔴 S2-8 曾以「藍鯨沒有海外合作俱樂部或旅外案例」為由整頁 404——但只查了「有沒有
// 合作俱樂部 Logo」，沒有查沿革。`content/blue-whale/club-profile.md` §4 沿革其實有
// 三筆真實旅外事實（2019 守門員蔡明容輸出旅外日本、2020 守門員程思瑜輸出旅外日本、
// 2019 選手蘇育萱輸出旅外日本、2023 選手蘇育萱輸出旅外中國），其中蔡明容還有
// `squad/player-tsai-ming-jung.md` 記錄的完整俱樂部經歷（2019–2022 效力日本
// FC ふじざくら山梨／FC FUJIZAKURA YAMANASHI，2022 年返回台中藍鯨）——這是舊站
// 已公開的球員經歷事實（該檔案自己註明「皆為舊站已公開的資訊」），不是未核實的
// 臆測，可以文字引用（不含照片，肖像同意狀態未知）。
// 藍鯨沒有可公開的海外「合作俱樂部」Logo 或協議（不同於「旅外球員」，兩者是不同
// 事實），故地區頁籤改為球員實際旅外的日本／中國，合作俱樂部 Logo 牆對藍鯨維持
// 空狀態，不得挪用磐石的三個海外俱樂部 Logo（Hellas Verona／Rayo Ciudad
// Alcobendas／Rot-Weiss Ahlen）充數。
// ---------------------------------------------------------------------------

export function getInternationalPathwaysSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '國際發展通道｜台中藍鯨女子足球隊',
      description: '台中藍鯨國際發展通道：球員旅外日本、中國的真實案例，以及海外試訓與媒合諮詢管道。',
    }
  }
  return {
    title: '國際發展通道 International Pathways｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC',
    description:
      '台中磐石足球俱樂部國際發展通道：從在地到海外俱樂部的完整路徑、歐洲／日本／香港分區資訊、合作俱樂部 Logo 牆，以及試訓球探與海外媒合諮詢管道。',
  }
}

export function getInternationalPathwaysHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '國際發展通道',
      h1En: null,
      lede: '從台中出發，台中藍鯨已有球員成功旅外日本與中國，持續為選手建立通往國際舞台的路徑。',
    }
  }
  return {
    h1Zh: '國際發展通道',
    h1En: 'International Pathways',
    lede: 'LOCAL ROOTS. GLOBAL PATHWAYS. 從台中出發，透過一線隊與海外交流，為球員建立通往職業舞台的國際路徑。',
  }
}

export interface IntlPathwayPlayerNote {
  nameZh: string
  descZh: string
}

/** 藍鯨旅外日本球員案例，逐字節錄自 club-profile.md §4 沿革與 player-tsai-ming-jung.md
 * （唯一有完整俱樂部經歷紀錄的案例），不含照片（肖像同意狀態未知，比照 3.5 球員故事）。 */
export const INTL_PATHWAY_JAPAN_NOTES_BW: IntlPathwayPlayerNote[] = [
  { nameZh: '蔡明容 Tsai Ming-Jung', descZh: '守門員，2019–2022 年效力日本 FC ふじざくら山梨（FC Fujizakura Yamanashi），2022 年返回台中藍鯨。' },
  { nameZh: '程思瑜', descZh: '守門員，2020 年輸出旅外日本成功（舊站沿革僅記錄年份，效力俱樂部名稱未提供）。' },
  { nameZh: '蘇育萱', descZh: '2019 年輸出旅外日本成功（舊站沿革僅記錄年份，效力俱樂部名稱未提供）。' },
]

/** 藍鯨旅外中國球員案例，逐字節錄自 club-profile.md §4 沿革 2023 年第 3 點。 */
export const INTL_PATHWAY_CHINA_NOTE_BW: IntlPathwayPlayerNote = {
  nameZh: '蘇育萱',
  descZh: '2023 年輸出旅外中國成功（舊站沿革僅記錄年份，效力俱樂部名稱未提供）。',
}

// ---------------------------------------------------------------------------
// 04.5／04.6 青年隊教練團／青年隊生活 Youth Coaches／Youth Life（BW-C1 重開）
//
// 🔴 S2-8 曾以「沒有已核實、非過期的藍鯨青年隊教練名單／沒有已取得肖像同意的青年隊
// 影像」為由整頁 404——這是「此頁此區塊內容缺漏」，不是「整頁不存在」，藍鯨規劃書
// §1.3 總則例外不含 4.5／4.6，應重開頁面、缺漏區塊顯示既有空狀態（比照 3.5 球員
// 故事）。標題刻意不用「學院」字樣（check-club-brand-leak.mjs 詞表禁詞，04 對藍鯨
// 依 docs/13 §3 一律稱「青年隊」）。
// ---------------------------------------------------------------------------

export function getYouthCoachesSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '青年隊教練團｜台中藍鯨女子足球隊',
      description: '台中藍鯨青年隊教練團：名單整理中，將於已核實資料到位後公布於本頁。',
    }
  }
  return {
    title: '學院教練團 Coaches｜台中磐石足球學院｜台中磐石足球俱樂部',
    description: '認識台中磐石足球學院教練團：青訓總監徐翊、青訓教練許志傑與黃聖傑。證照、專長與負責梯隊等詳細資料收集中。',
  }
}

export function getYouthCoachesHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '青年隊教練團',
      h1En: null,
      lede: '台中藍鯨青年隊教練團名單整理中，將於已核實、非過期的資料到位後公布於本頁。',
    }
  }
  return {
    h1Zh: '學院教練團',
    h1En: 'Academy Coaches',
    lede: '台中磐石足球學院教練團由 1 位青訓總監與 2 位青訓教練組成，陪伴各梯隊學員從基礎技術到比賽判讀逐步成長。',
  }
}

export function getYouthLifeSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '青年隊生活｜台中藍鯨女子足球隊',
      description: '台中藍鯨青年隊訓練與比賽日常影像，待肖像同意到位後將陸續公布於本頁。',
    }
  }
  return {
    title: '學院生活 Academy Life｜台中磐石足球學院｜台中磐石足球俱樂部',
    description: '台中磐石足球學院的訓練日常、比賽與活動剪影，透過真實影像紀錄學員的成長點滴。',
  }
}

export function getYouthLifeHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '青年隊生活',
      h1En: null,
      lede: '訓練日常、比賽現場與隊上活動——待肖像同意到位後，將陸續以影像記錄台中藍鯨青年隊學員的成長點滴。',
    }
  }
  return {
    h1Zh: '學院生活',
    h1En: 'Academy Life',
    lede: '訓練日常、比賽現場與隊上活動——用影像記錄台中磐石學院學員每一次奔跑與突破的瞬間。',
  }
}

// ---------------------------------------------------------------------------
// 05.1–05.4 課程與活動四頁 Programs（BW-C1 重開）
//
// 🔴 藍鯨規劃書 §3.5（行 201）「05 推廣活動沿用主站 05 的活動版型；是否開放線上
// 報名與收費，待確認」——這句話本身就是「頁面開放、報名／收費功能待確認（暫不
// 開放）」的明文依據。S1-15／S2-10 兩輪把這句話誤讀成整頁關閉的理由，本輪重開。
// ---------------------------------------------------------------------------

/** 藍鯨真實課程班別，逐字節錄自 programs.md §1（社區足球學校）與 §2（運動 i 台灣
 * 2.0－運動熱區 10 種課程規格），只取與「兒童足球訓練」對應的班別（3–15 歲），
 * 不含守門員基礎班（另見 5.4 GOALKEEPER_CLASS_BW，規模與定位不同不合併）與成人
 * 性質的野團足球賽。 */
export interface ChildrensClassRow {
  nameZh: string
  ageZh: string
  feeZh: string
}
export const CHILDRENS_TRAINING_CLASSES_BW: ChildrensClassRow[] = [
  { nameZh: '社區幼幼足球班', ageZh: '3–4 歲', feeZh: '200 元／堂' },
  { nameZh: '幼兒社區足球班', ageZh: '6–8 歲', feeZh: '200 元／堂' },
  { nameZh: '藍鯨 U8／U10 足球教室', ageZh: '9–10 歲', feeZh: '200 元／堂' },
  { nameZh: '藍鯨 U12 女子足球班', ageZh: '10–12 歲（限女性）', feeZh: '200 元／堂' },
  { nameZh: '藍鯨 U15 女子足球班', ageZh: '13 歲以上（限女性）', feeZh: '200 元／堂' },
]

export function getChildrensTrainingSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '兒童足球訓練｜課程與活動｜台中藍鯨女子足球隊',
      description: '台中藍鯨兒童足球訓練：社區足球學校「小藍鯨」與運動 i 台灣 2.0 課程，3–15 歲多種班別，太原足球場現場個人報名。',
    }
  }
  return {
    title: '兒童足球訓練 Children\'s Training｜課程與活動｜台中磐石足球俱樂部',
    description: '台中磐石兒童足球訓練依混齡體驗、初學、技巧發展分級規劃，於台中磐石主場等場地授課，提供週期課表與線上報名。',
  }
}

export function getChildrensTrainingHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '兒童足球訓練',
      h1En: null,
      lede: '台中藍鯨承接「運動 i 台灣 2.0」運動熱區推廣計畫，社區足球學校「小藍鯨」與多種分齡班別於太原足球場開課，免試上、免入會費，現場個人報名。',
    }
  }
  return {
    h1Zh: '兒童足球訓練',
    h1En: 'Children\'s Training',
    lede: '從第一次接觸足球到養成技巧，台中磐石依年齡與能力分級規劃課程，讓每個孩子都能在合適的節奏中成長。',
  }
}

export function getSummerCampSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '夏令營｜課程與活動｜台中藍鯨女子足球隊',
      description: '台中藍鯨目前尚未推出獨立的夏令營活動，暑期相關活動請關注官方社群與推廣活動頁面。',
    }
  }
  return {
    title: '夏令營 Summer Camp｜課程與活動｜台中磐石足球俱樂部',
    description: '台中磐石足球夏令營，提供密集足球訓練與活動內容，梯次日期、地點與費用將於報名開放時公告，線上報名不接受金流付款。',
  }
}

export function getSummerCampHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '夏令營',
      h1En: null,
      lede: '台中藍鯨目前尚未推出獨立的夏令營活動，是否開放另行規劃中。暑期期間可留意「兒童足球訓練」既有班別與官方社群公告。',
    }
  }
  return {
    h1Zh: '夏令營',
    h1En: 'Summer Camp',
    lede: '利用暑假密集接觸足球訓練，在專業教練帶領下累積球感、體能與團隊合作經驗。',
  }
}

export function getWinterCampSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '冬令營｜課程與活動｜台中藍鯨女子足球隊',
      description: '台中藍鯨目前尚未推出獨立的冬令營活動，寒假相關活動請關注官方社群與推廣活動頁面。',
    }
  }
  return {
    title: '冬令營 Winter Camp｜課程與活動｜台中磐石足球俱樂部',
    description: '台中磐石足球冬令營，與夏令營共用版型與資料模型。梯次日期、地點、費用與教練團資訊將於報名開放時公告。',
  }
}

export function getWinterCampHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '冬令營',
      h1En: null,
      lede: '台中藍鯨目前尚未推出獨立的冬令營活動，是否開放另行規劃中。寒假期間可留意「兒童足球訓練」既有班別與官方社群公告。',
    }
  }
  return {
    h1Zh: '冬令營',
    h1En: 'Winter Camp',
    lede: '寒假期間的密集足球訓練營，版型與資料模型與夏令營相同。梯次日期與費用將於報名開放前公告。',
  }
}

/** 藍鯨守門員基礎班，逐字節錄自 programs.md §2 第 10 項——規模（7–12 歲、限額 10 位）
 * 與定位（社區推廣性質）都不是磐石「六大專項競技訓練」同一種產品，不得套用六大專項
 * 框架，維持它自己的真實樣貌單獨呈現。 */
export const GOALKEEPER_CLASS_BW = {
  nameZh: '藍鯨守門員基礎班',
  ageZh: '7–12 歲（男女不拘，女生保留錄取名額，限額 10 位）',
  feeZh: '200 元／堂',
  scheduleZh: '每週 1.5 小時／堂',
  signupZh: '須先填寫報名表單',
  signupUrl: 'https://forms.gle/ffQTf1uaZinZd2mt5',
}

export function getSpecialistTrainingSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '專項訓練｜課程與活動｜台中藍鯨女子足球隊',
      description: '台中藍鯨專項訓練目前提供守門員基礎班（7–12 歲），其餘專項尚未推出，須先填寫報名表單。',
    }
  }
  return {
    title: '專項訓練 Specialist Training｜課程與活動｜台中磐石足球俱樂部',
    description: '台中磐石專項訓練涵蓋守門員、前鋒、後衛、中場、體能與速度、高階訓練六大類別，由台中磐石教練團規劃執行，線上報名。',
  }
}

export function getSpecialistTrainingHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '專項訓練',
      h1En: null,
      lede: '台中藍鯨目前提供守門員基礎班，針對守門位置設計專項訓練，其餘專項訓練尚未推出，後續開放將公布於本頁。',
    }
  }
  return {
    h1Zh: '專項訓練',
    h1En: 'Specialist Training',
    lede: '針對特定位置與能力設計的分科訓練，由台中磐石教練團依學員需求規劃課程目標與適合對象。',
  }
}

// ---------------------------------------------------------------------------
// 03 單元 hub（club/index.vue）——BW-C1 品牌外洩全站盤點新增（原本是 S1-12d／S2-8
// 已記錄的既有缺口：整頁固定磐石內容，見 apps/web/README.md「S1-12d」節「刻意不動
// 的範圍」）。版型不變，統計卡／單元卡描述／CTA 標題依俱樂部切換；tcrfc 分支逐字
// 沿用既有內容，不改變既有輸出。
// ---------------------------------------------------------------------------

export function getClubHubSeo(club: string, facts: SiteFacts): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '台中藍鯨一線隊｜台中藍鯨女子足球隊',
      description: `台中藍鯨俱樂部單元總覽：一線隊、球員培育重點、球員機會、國際發展通道與球員故事，${facts.foundedDisplayZh}，出戰${facts.league.nameZh}。`,
    }
  }
  return {
    title: '台中磐石足球俱樂部 Football Club｜台中磐石足球俱樂部 TCRFC',
    description:
      '台中磐石足球俱樂部（TCRFC）足球俱樂部單元總覽：一線隊、球員發展系統、球員機會、國際發展通道與球員故事，帶你認識一線隊如何培育選手邁向職業與國際舞台。',
  }
}

export function getClubHubHero(club: string, facts: SiteFacts): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '台中藍鯨一線隊',
      h1En: null,
      lede: `代表俱樂部出戰${facts.league.nameZh}的第一支隊伍，${facts.foundedDisplayZh}。這裡整理球隊名單、球員培育重點、加入管道，以及選手旅外的真實案例。`,
    }
  }
  return {
    h1Zh: '台中磐石足球俱樂部',
    h1En: 'Football Club',
    lede: `從${primaryVenueOf(facts).nameZh}出發的一線隊，是台中磐石所有青訓體系最終要銜接的舞台。這裡整理球隊陣容、球員發展系統、加入管道，以及選手通往海外的路徑。`,
  }
}

export interface ClubHubStat {
  num: string
  labelZh: string
}

/** 03 hub 統計卡——tcrfc 逐字沿用既有 4 張卡（含既有頁面既定的字面數字：28 名一線隊
 * 註冊球員、21 場 2026/27 賽季賽事，這兩個數字本來就不在 SiteFacts 之列，是本頁改動前
 * 既有的既定文字，非本輪新增臆測）。bw 版只用已核實事實組卡，不比照臆造「註冊球員數」
 * 「賽季場次」這類磐石才有核實數字的統計項目——「隊史五度奪冠」是 club-profile.md §4
 * 沿革逐條計數的既有核實事實（同 getFirstTeamHero 既有用法）。 */
export function getClubHubStats(club: string, facts: SiteFacts): ClubHubStat[] {
  if (normalizeClub(club) === 'bw') {
    return [
      { num: facts.foundedYear, labelZh: '創立年份' },
      { num: '5', labelZh: '隊史奪得聯賽冠軍次數' },
      { num: String(facts.squadCodes.length), labelZh: '青年隊梯隊數' },
      { num: facts.league.shortNameZh ?? facts.league.nameZh, labelZh: '參與聯賽' },
    ]
  }
  return [
    { num: facts.foundedYear, labelZh: '創立年份' },
    { num: facts.foundedYear, labelZh: facts.foundingTitleZh ?? '' },
    { num: '28', labelZh: '一線隊註冊球員' },
    { num: '21', labelZh: '2026/27 企甲例行賽場次' },
  ]
}

/** 3.3 球員機會單元卡描述——tcrfc 沿用既有文字（含「加入台中磐石」字面），bw 版換俱樂部名稱。 */
export function getClubHubOpportunitiesDesc(club: string): string {
  if (normalizeClub(club) === 'bw') {
    return '加入台中藍鯨、試訓場次列表與線上報名、外籍球員招募管道。'
  }
  return '加入台中磐石、試訓場次列表與線上報名、外籍球員招募管道。'
}

/** 3.5 球員故事單元卡描述——bw 版拿掉「學院」「女足」字樣（04 對藍鯨不稱學院；本站
 * 本身即為女足官網，「女足球員」在此語境是自我指涉，見本檔檔頭紀律 11）。 */
export function getClubHubPlayerStoriesDesc(club: string): string {
  if (normalizeClub(club) === 'bw') {
    return '一線隊與旅外球員的真實案例，看見選手如何一步步走到現在。'
  }
  return '學院、一線隊、海外與女足球員的真實案例，看見選手如何一步步走到現在。'
}

/** 03 hub 底部 CTA 區塊視覺隱藏標題。 */
export function getClubHubCtaTitle(club: string): string {
  if (normalizeClub(club) === 'bw') {
    return '加入台中藍鯨一線隊'
  }
  return '加入台中磐石一線隊'
}

// ---------------------------------------------------------------------------
// 05 單元 hub（programs/index.vue）——BW-C1 品牌外洩全站盤點新增（原本是既有缺口，
// 整頁固定磐石內容）。藍鯨規劃書 §2.1：05 單元名稱是「PROGRAMS 推廣活動」，不是
// 磐石的「課程與活動」；§3.5（行 201）「05 沿用主站 05 的活動版型；是否開放線上
// 報名與收費，待確認」——藍鯨 5.1–5.4 各頁現況一律是現場個人報名，不接站內線上
// 報名／金流流程，故本頁「線上報名流程」六步驟區塊（假定站內線上流程存在）對藍鯨
// 隱藏，改顯示如實的報名說明。
// ---------------------------------------------------------------------------

export function getProgramsHubSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: '推廣活動 Programs｜台中藍鯨女子足球隊',
      description: '台中藍鯨推廣活動總覽：社區足球學校、運動熱區課程、專項訓練、校園與社區合作計畫，現場個人報名，免試上、免入會費。',
    }
  }
  return {
    title: '課程與活動 Programs｜台中磐石足球俱樂部',
    description:
      '台中磐石足球俱樂部課程與活動總覽：兒童足球訓練、夏令營、冬令營、專項訓練、校園與社區計畫。所有梯次含地點、時間、名額與費用資訊，線上報名。',
  }
}

export function getProgramsHubHero(club: string): HeroCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      h1Zh: '推廣活動',
      h1En: 'Programs',
      lede: '從社區足球學校、運動熱區課程，到校園與社區合作計畫——台中藍鯨以推廣活動接觸更多與球的相遇，現場個人報名，免試上、免入會費。',
    }
  }
  return {
    h1Zh: '課程與活動',
    h1En: 'Programs',
    lede: '從兒童足球訓練、夏／冬令營、專項訓練，到校園與社區合作計畫——台中磐石以「課程項目」的方式經營每一種與球的相遇，每個梯次都清楚標示地點、時間、名額與費用。',
  }
}

export function getProgramsHubIntro(club: string): string {
  if (normalizeClub(club) === 'bw') {
    return '台中藍鯨的推廣活動以現場個人報名為主，免試上、免測試、免入會費：社區足球學校「小藍鯨」承接臺中市政府「運動 i 台灣 2.0」運動熱區推廣計畫，另提供守門員基礎班與校園、社區合作方案。以下五個單元分別對應不同年齡與需求，點選卡片查看各單元詳情。'
  }
  return '台中磐石足球俱樂部的課程與活動皆採梯次（Session）方式管理：每個課程項目具備明確的分級或分類、訓練地點、時間安排、名額上限與費用資訊，並提供線上報名。以下五個單元分別對應不同年齡與需求的學員，點選卡片查看各單元詳情。'
}

export interface ProgramsHubCard {
  num: string
  titleZh: string
  titleEn: string
  descZh: string
  href: string
  /** false＝沒有藍鯨自己的照片可用，頁面改用既有的漸層佔位（不挪用磐石照片）。 */
  hasPhoto: boolean
}

/** 05 hub 五張導覽卡——tcrfc 逐字沿用既有文字與照片；bw 版描述改為真實對應內容（5.1／
 * 5.4／5.5）或誠實的「尚未推出」空狀態（5.2／5.3，見 getSummerCampSeo／
 * getWinterCampSeo 既有 bw 分支同一組事實）。 */
export function getProgramsHubCards(club: string): ProgramsHubCard[] {
  if (normalizeClub(club) === 'bw') {
    return [
      { num: '5.1', titleZh: '兒童足球訓練', titleEn: "Children's Training", descZh: '社區足球學校「小藍鯨」與運動 i 台灣 2.0 課程，3–15 歲多種班別，現場個人報名。', href: '/zh/programs/childrens-training/', hasPhoto: false },
      { num: '5.2', titleZh: '夏令營', titleEn: 'Summer Camp', descZh: '目前尚未推出獨立的夏令營活動，暑期活動請關注官方社群公告。', href: '/zh/programs/summer-camp/', hasPhoto: false },
      { num: '5.3', titleZh: '冬令營', titleEn: 'Winter Camp', descZh: '目前尚未推出獨立的冬令營活動，寒假活動請關注官方社群公告。', href: '/zh/programs/winter-camp/', hasPhoto: false },
      { num: '5.4', titleZh: '專項訓練', titleEn: 'Specialist Training', descZh: '目前提供守門員基礎班（7–12 歲），須先填寫報名表單，其餘專項尚未推出。', href: '/zh/programs/specialist/', hasPhoto: false },
      { num: '5.5', titleZh: '校園與社區', titleEn: 'School & Community', descZh: '建教合作學校列表、社區足球推廣計畫、教練培訓，歡迎學校與社區單位洽談合作。', href: '/zh/programs/school-community/', hasPhoto: false },
    ]
  }
  return [
    { num: '5.1', titleZh: '兒童足球訓練', titleEn: "Children's Training", descZh: '分級（混齡／初學／技巧發展）、訓練地點地圖、週期課表、線上報名。', href: '/zh/programs/childrens-training/', hasPhoto: true },
    { num: '5.2', titleZh: '夏令營', titleEn: 'Summer Camp', descZh: '適合對象、課程內容、教練團、合作夥伴、日期地點與報名資訊。', href: '/zh/programs/summer-camp/', hasPhoto: true },
    { num: '5.3', titleZh: '冬令營', titleEn: 'Winter Camp', descZh: '與夏令營共用版型與資料模型，梯次資訊將於開放報名前公告。', href: '/zh/programs/winter-camp/', hasPhoto: false },
    { num: '5.4', titleZh: '專項訓練', titleEn: 'Specialist Training', descZh: '守門員／前鋒／後衛／中場／體能與速度／高階訓練，六大專項課程。', href: '/zh/programs/specialist/', hasPhoto: true },
    { num: '5.5', titleZh: '校園與社區', titleEn: 'School & Community', descZh: '校園合作方案、社區計畫、教練培訓、合作學校列表與洽談表單。', href: '/zh/programs/school-community/', hasPhoto: false },
  ]
}

export interface EnrolFlowStep {
  titleZh: string
  descZh: string
}

/** 「線上報名流程」六步驟——僅 tcrfc 適用（藍鯨現場個人報名，不接這套站內線上流程，
 * 見本節檔頭說明），呼叫端對 bw 應整段隱藏，改用 `getProgramsHubEnrolNoteBw()`，
 * 不得輸出「尚未開放」版本的六步驟（那會誤導成「藍鯨其實也有這套流程，只是還沒開」，
 * 而是根本不採這套線上流程）。 */
export const ENROL_FLOW_STEPS_TCRFC: EnrolFlowStep[] = [
  { titleZh: '選課程', descZh: '依年齡與需求選擇 5.1–5.5 課程項目' },
  { titleZh: '選梯次', descZh: '選擇開放報名中的時段、地點與梯次' },
  { titleZh: '學員資料', descZh: '填寫學員基本資料，可一次登記多名' },
  { titleZh: '家長／緊急聯絡人', descZh: '留下家長或緊急聯絡人資訊' },
  { titleZh: '健康聲明', descZh: '確認健康狀況並同意相關條款' },
  { titleZh: '送出', descZh: '產生報名編號，Email／簡訊通知，視梯次提供匯款資訊' },
]

/** bw 版報名說明，取代 tcrfc 六步驟區塊，如實描述現場個人報名。 */
export function getProgramsHubEnrolNoteBw(): string {
  return '台中藍鯨推廣活動現場個人報名，免試上、免測試、免入會費；守門員基礎班須先填寫線上報名表單。詳細報名方式請見各單元頁面，或洽詢台中藍鯨官方 LINE。'
}

// ---------------------------------------------------------------------------
// 10.4 國際球員詢問表單頁（join/international-player/index.vue）——BW-C1 品牌外洩
// 全站盤點新發現的既有缺口：本頁 SEO／Hero／同意聲明／收件單位標籤原本全部固定
// 寫死「Taichung Rock FC」「TCRFC」「台中磐石足球俱樂部」「International
// Department」，完全沒有俱樂部分支——不在先前任何一輪記錄範圍內（10.4 本來就沒有
// 被關閉，`units.ts` 不含 '10.4'，藍鯨訪客一直看得到這頁）。英文為主、中文輔助的
// 版型不變（規劃書行 320 明訂 10.4 英文優先）。
// ---------------------------------------------------------------------------

export function getInternationalPlayerSeo(club: string): SeoCopy {
  if (normalizeClub(club) === 'bw') {
    return {
      title: `International Player Enquiries 國際球員詢問｜Join / Contact｜${BW_NAME_EN}`,
      description:
        `Interested in playing for ${BW_NAME_EN} in Taiwan? Submit your football background, video highlights and visa status and our club will follow up.`,
    }
  }
  return {
    title: 'International Player Enquiries 國際球員詢問｜Join / Contact｜Taichung Rock FC',
    description:
      'Interested in playing for Taichung Rock FC (TCRFC) in Taiwan? Submit your football background, video highlights and visa status. Our International department will get back to you.',
  }
}

export function getInternationalPlayerHero(club: string): { leadEn: string; leadZh: string } {
  if (normalizeClub(club) === 'bw') {
    return {
      leadEn: 'Interested in playing for us in Taiwan? Tell us about your football background, highlight videos and visa status, and our club will follow up with you.',
      leadZh: '有意加入台中藍鯨的國際球員，請填寫以下表單，我們將盡快與你聯繫。',
    }
  }
  return {
    leadEn: 'Interested in playing for TCRFC in Taiwan? Tell us about your football background, highlight videos and visa status, and our International department will follow up with you.',
    leadZh: '有意加入台中磐石足球俱樂部的國際球員，請填寫以下表單，國際部將盡快與你聯繫。',
  }
}

/** 同意聲明——連結（Privacy Policy）本身是通用 UI 文字留在頁面模板，這裡只提供
 * 連結「之後」含俱樂部名稱的句尾，避免整句重複、模板端用字串插入組出完整句子。 */
export function getInternationalPlayerConsentAfterLink(club: string): { en: string; zh: string } {
  if (normalizeClub(club) === 'bw') {
    return {
      en: `, and consent to ${BW_NAME_EN} collecting the personal data submitted in this form for the purpose of processing this player enquiry.`,
      zh: '本人已閱讀並同意隱私權政策，並同意台中藍鯨依本表單蒐集之個人資料，用於處理本次國際球員詢問。',
    }
  }
  return {
    en: ', and consent to Taichung Rock FC collecting the personal data submitted in this form for the purpose of processing this player enquiry.',
    zh: '本人已閱讀並同意隱私權政策，並同意台中磐石足球俱樂部依本表單蒐集之個人資料，用於處理本次國際球員詢問。',
  }
}

/** 側欄「收件單位」標籤——bw 沒有已核實的「國際部」這個組織單位，不得沿用磐石的
 * 部門名稱（比照 getJoinFirstTeamBody 對 bw 不具名部門的既有做法）。 */
export function getInternationalPlayerDeptLabel(club: string): string {
  if (normalizeClub(club) === 'bw') {
    return 'Our Club'
  }
  return 'International Department'
}

export function getProgramsHubCtaCards(club: string): CtaCardCopy[] {
  if (normalizeClub(club) === 'bw') {
    return [
      { num: '兒童與青少年', titleZh: '兒童足球訓練', descZh: '社區足球學校「小藍鯨」，現場個人報名。', ctaLabelZh: '立即了解', href: '/zh/programs/childrens-training/' },
      { num: '學校與機構', titleZh: '校園與社區合作', descZh: '洽談校園方案、社區計畫與教練培訓。', ctaLabelZh: '前往洽談', href: '/zh/programs/school-community/' },
      { num: '其他問題', titleZh: '聯絡台中藍鯨', descZh: '課程相關問題歡迎直接與我們聯繫。', ctaLabelZh: '聯絡我們', href: '/zh/join/general/' },
    ]
  }
  return [
    { num: '兒童與青少年', titleZh: '兒童足球訓練', descZh: '分齡分級，從混齡體驗到技巧發展。', ctaLabelZh: '立即了解', href: '/zh/programs/childrens-training/' },
    { num: '學校與機構', titleZh: '校園與社區合作', descZh: '洽談校園方案、社區計畫與教練培訓。', ctaLabelZh: '前往洽談', href: '/zh/programs/school-community/' },
    { num: '其他問題', titleZh: '聯絡台中磐石', descZh: '課程相關問題歡迎直接與我們聯繫。', ctaLabelZh: '聯絡我們', href: '/zh/join/general/' },
  ]
}
