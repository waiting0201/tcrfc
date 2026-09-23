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
  foundedZh: string
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

export const CLUB_IDENTITY: Record<ClubCode, ClubIdentity> = {
  tcrfc: {
    aboutLabelZh: '關於台中磐石',
    cultureLabelZh: '台中磐石文化',
    academyLabelZh: '足球學院',
    academyLabelEn: 'ACADEMY',
    academyShortLabelZh: '學院',
    brandTagEn: 'TCRFC',
    foundedZh: '2024 年創立',
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
    // 🔴 不得自行選定英文正式全名（舊站並存 Taichung Bluewhale／Taichung Blue
    // Whale Women's Football Team／Taichung blue whale 三種寫法，待客戶確認）。
    brandTagEn: null,
    foundedZh: '2014 年 4 月 12 日成立',
    // 沿用舊站首頁已公開發布的中英文標語原文（content/blue-whale/club-profile.md §3），
    // 不是新譯——首頁一句英文說明「Taichung Blue Whale rides the waves towards
    // the open ocean」是舊站自己配的英文文案，不是我方新生產翻譯。
    slogan: { zh: '航向世界的藍鯨', en: 'Taichung Blue Whale rides the waves towards the open ocean' },
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

function normalizeClub(club: string): ClubCode {
  return club === 'bw' ? 'bw' : 'tcrfc'
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

export const HOME_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '台中磐石足球俱樂部 TCRFC｜在地扎根．放眼世界',
    description:
      '台中磐石足球俱樂部（TCRFC）官方網站。2024 年創立，2024 全國乙級聯賽冠軍。一線隊、台中磐石足球學院、課程與活動、女子足球四大體系。',
  },
  bw: {
    title: '台中藍鯨女子足球隊｜航向世界的藍鯨',
    description:
      '台中藍鯨女子足球隊官方網站。隸屬臺中市女子足球協會，2014 年成立，台灣木蘭足球聯賽球隊，隊史五度奪得木蘭聯賽冠軍。一線隊、青年隊、推廣活動三大體系。',
  },
}

export const HOME_HERO: ClubText<HomeHeroCopy> = {
  tcrfc: {
    kickerEn: 'LOCAL ROOTS. GLOBAL PATHWAYS.',
    headlineZh: '在地扎根<br>放眼世界',
    factLineZh: '台中磐石足球俱樂部 · <b>2024 年創立</b> · <b>2024 全國乙級聯賽冠軍</b>',
    ctaPrimaryHref: '/zh/charity/',
    ctaSecondaryLabelZh: '認識台中磐石',
    ctaSecondaryHref: '/zh/culture/',
  },
  bw: {
    kickerEn: 'Taichung Blue Whale rides the waves towards the open ocean',
    headlineZh: '航向世界<br>的藍鯨',
    // 「五度」是對 content/blue-whale/club-profile.md §4 沿革逐條「隊史第 X 座台灣
    // 木蘭聯賽冠軍」明文出現次數的計數（2017／2018／2019／2021／2023 共五次），
    // 是核算既有原文，不是新臆測的戰績。
    factLineZh: '台中藍鯨女子足球隊 · <b>2014 年成立</b> · <b>隊史五度奪得木蘭聯賽冠軍</b>',
    ctaPrimaryHref: '/zh/join/player/',
    ctaSecondaryLabelZh: '認識台中藍鯨',
    ctaSecondaryHref: '/zh/about/',
  },
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

/** 首頁與一線隊頁共用的「加入我們」CTA 三卡——磐石版逐字沿用既有 mockup 文案；藍鯨版改「企甲聯賽」為「木蘭聯賽」、學院為青年隊、俱樂部名稱代入，其餘句型不變。 */
export const HOME_CTA_TRIO: ClubText<CtaCardCopy[]> = {
  tcrfc: [
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
  ],
  bw: [
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
      descZh: 'U15／U12 青少年女子足球隊，提供系統化的足球訓練。',
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
  ],
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

export const ABOUT_INDEX_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '關於台中磐石',
    h1En: 'About TCRFC',
    lede: 'LOCAL ROOTS. GLOBAL PATHWAYS.｜在地扎根 · 放眼世界。台中磐石足球俱樂部 2024 年於台中成立，以下八個篇章，帶你認識這支球隊從理念到組織的全貌。',
  },
  bw: {
    h1Zh: '關於台中藍鯨',
    h1En: null,
    lede: '台中藍鯨女子足球隊隸屬臺中市女子足球協會，2014 年成立，是台灣木蘭足球聯賽的球隊之一。以下篇章帶你認識這支球隊從理念到組織的全貌。',
  },
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

// 2.1 我們的故事
export const OUR_STORY_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '我們的故事 Our Story｜關於台中磐石｜台中磐石足球俱樂部',
    description: '台中磐石足球俱樂部（TCRFC）於 2024 年在台中成立。這裡收錄俱樂部從創立至今的沿革故事，完整內文正在整理中。',
  },
  bw: {
    title: '我們的故事｜關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊 2014 年成立於台中，隸屬臺中市女子足球協會。認識這支球隊的定位與成立宗旨。',
  },
}

export const OUR_STORY_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '我們的故事',
    h1En: 'Our Story',
    lede: 'LOCAL ROOTS. GLOBAL PATHWAYS.｜台中磐石足球俱樂部 2024 年於台中成立。這裡是我們沿革故事的篇章，完整內文正在與俱樂部確認中。',
  },
  bw: {
    h1Zh: '我們的故事',
    h1En: null,
    lede: '台中藍鯨女子足球隊 2014 年 4 月 12 日成立，隸屬臺中市女子足球協會，是台灣木蘭足球聯賽的球隊之一。',
  },
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

/** 藍鯨拿掉自我指涉節點（女子足球→本站），04 依 docs/13 §3 改為青年隊。 */
export const ECOSYSTEM_NODES: ClubText<EcoNode[]> = {
  tcrfc: [
    { num: '3', slug: 'club', enLabel: 'Football Club', zhLabel: '一線隊', descZh: '征戰企業甲級聯賽的球隊本體，代號 First Team / 一線隊。', href: '/zh/club/' },
    { num: '4', slug: 'academy', enLabel: 'Academy', zhLabel: '台中磐石足球學院', descZh: 'U15／U14／U12 三個梯隊，銜接一線隊的青訓體系。', href: '/zh/academy/' },
    { num: '5', slug: 'programs', enLabel: 'Programs', zhLabel: '課程與活動', descZh: '兒童足球訓練、夏／冬令營、專項訓練與校園社區計畫。', href: '/zh/programs/' },
    { num: '6', slug: 'womens', enLabel: "Women's Football", zhLabel: '女子足球', descZh: '台中藍鯨女子隊，設有獨立的官方網站。', badgeZh: '官網入口', href: '/zh/womens/' },
  ],
  bw: [
    { num: '3', slug: 'club', enLabel: 'First Team', zhLabel: '一線隊', descZh: '出戰台灣木蘭足球聯賽的球隊本體。', href: '/zh/club/' },
    { num: '4', slug: 'youth', enLabel: 'Youth', zhLabel: '青年隊', descZh: 'U15／U12 青少年女子足球隊，銜接一線隊的青訓體系。', href: '/zh/academy/' },
    { num: '5', slug: 'programs', enLabel: 'Programs', zhLabel: '推廣活動', descZh: '社區足球學校、運動熱區課程、教練講習與足球節。', href: '/zh/programs/' },
  ],
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
export const HISTORY_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '俱樂部歷程 Club History｜關於台中磐石｜台中磐石足球俱樂部',
    description: '台中磐石足球俱樂部的圖文歷史敘事，記錄俱樂部自 2024 年成立以來的發展歷程。完整內文正在整理中。',
  },
  bw: {
    title: '俱樂部歷程｜關於台中藍鯨｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊 2014～2025 年逐年沿革，整理自舊官網公開內容。',
  },
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

export const FIRST_TEAM_SEO: ClubText<SeoCopy> = {
  tcrfc: {
    title: '一線隊 First Team｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC',
    description:
      '台中磐石足球俱樂部一線隊（First Team）：28 名註冊球員名單依背號排序、教練團陣容、2026/27 企業甲級聯賽完整賽程與 .ics 訂閱、榮譽紀錄時間軸。',
  },
  bw: {
    title: '一線隊｜台中藍鯨女子足球隊',
    description: '台中藍鯨女子足球隊一線隊：出戰台灣木蘭足球聯賽，隊史五度奪冠。名單與賽程由後台維護，內容更新中。',
  },
}

export const FIRST_TEAM_HERO: ClubText<HeroCopy> = {
  tcrfc: {
    h1Zh: '一線隊',
    h1En: 'First Team',
    lede: '台中磐石一線隊代表俱樂部出戰企業甲級聯賽，是所有青訓與學院球員最終銜接的競技舞台。球隊 2024 年創立，同年即拿下全國乙級聯賽冠軍，主場為西屯足球場。',
  },
  bw: {
    h1Zh: '一線隊',
    h1En: null,
    lede: '台中藍鯨一線隊代表俱樂部出戰台灣木蘭足球聯賽，2014 年成立，隊史五度奪得聯賽冠軍。主場為台中北屯太原足球場、台中豐原體育場。',
  },
}

/** 對應 mockup「球隊介紹」段落——藍鯨版逐句改寫自 club-profile.md §1 已核實事實（成立年、聯賽名、主場），不臆測名次或賽季戰績。 */
export const FIRST_TEAM_INTRO: ClubText<string> = {
  tcrfc: '台中磐石足球俱樂部一線隊於 2024 年隨俱樂部創立成軍，同年奪下全國乙級聯賽冠軍，現於企業甲級聯賽出賽。球隊主場設於西屯足球場，2026/27 賽季共排定 21 場企甲例行賽。',
  bw: '台中藍鯨一線隊於 2014 年隨俱樂部創立成軍，出戰台灣木蘭足球聯賽，隊史累計五度奪得聯賽冠軍（2017、2018、2019、2021、2023）。球隊主場為台中北屯太原足球場、台中豐原體育場。名單與最新賽程由後台維護，本頁內容更新中。',
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

/** 10.2 卡片標籤——藍鯨依 docs/13 §3 用「青年隊」，不沿用磐石學院的招生用詞。 */
export const JOIN_ACADEMY_CARD: ClubText<{ titleZh: string; descZh: string }> = {
  tcrfc: { titleZh: '加入學院／兒童訓練', descZh: '學院 U12／U14／U15 梯隊，或兒童訓練的混齡、初學、技巧發展班，同一份表單完成報名。' },
  bw: { titleZh: '加入青年隊', descZh: 'U15／U12 青少年女子足球隊招募，報名資格與費用請洽俱樂部。' },
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
