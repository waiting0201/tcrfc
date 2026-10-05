// shared/utils/club-copy-en-core.ts — 主站（tcrfc）英文版文案：共用項與 core 群組（首頁／關於／FAQ／搜尋）
//
// 約定（英文版文案檔共通，群組代號 core）：
// - 既有 `FOO_EN`／`getFooEn` 只回 tcrfc 的英文值（簽名與行為不變）。
// - 🔵 藍鯨英文版（B-5，2026-10-05）：**另開獨立匯出**，命名 `FOO_EN_BW`（常數）／`getFooEnBw(facts)`（函式），
//   不給原函式加 club 參數。藍鯨簡稱 `BW_NAME_EN`、全名 `BW_FULL_NAME_EN`；只翻譯 club-copy.ts bw 分支的繁中原文，
//   不新增事實。其他群組取俱樂部識別請用 `getClubIdentityEnBw()`（藍鯨）或 `getClubIdentityEn()`（磐石），
//   或一次判斷：`getClubIdentityEnFor(club)`。
// - 原匯出 `FOO`（常數）→ `FOO_EN`；原函式 `getFoo(club, facts)` → `getFooEn(facts)`。
// - 型別沿用 club-copy.ts 的 interface，**欄位名稱不變但值是英文**（例如 `h1Zh` 欄位放英文 H1、
//   `lede` 放英文導言），頁面端只要把來源換成 `_EN` 即可，不必改渲染。
// - 檔內字串不得含中文字元（scripts 有檢查）；英文用詞一律照 docs/06-conventions.md §1.1 對照表。
// - 事實（成立年、聯賽、梯隊代碼）一律由呼叫端傳入的 `facts` 取值，不在此寫死字面值
//   （check-fact-single-source.mjs）。

import type { ClubIdentity, HeroCopy, SeoCopy, HomeHeroCopy, PillarCopy, CtaCardCopy, EcoNode, VisionItem } from './club-copy'
import type { SiteFacts } from './site-facts'
import { BW_NAME_EN, BW_FULL_NAME_EN } from './club-copy'

/** 英文版的俱樂部名稱（對照表：台中磐石 → Taichung Rock FC）。 */
export const CLUB_NAME_EN = 'Taichung Rock FC'

/** 「U15, U14 and U12」式的英文列舉（梯隊代碼取自 facts.squadCodes，不寫死）。 */
function listAnd(items: readonly string[]): string {
  if (items.length <= 1) return items.join('')
  return `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`
}

/** 聯賽英文名稱：優先用 facts.league.nameEn（API 的 en 回應），沒有時用保守的 `the league`。 */
function leagueEn(facts: SiteFacts): string {
  return facts.league.nameEn ? `the ${facts.league.nameEn}` : 'the league'
}

/** 成立當年頭銜（API 目前只提供中文，此處對應已核實的既有事實）。成立年份取自 facts。 */
const FOUNDING_TITLE_EN = 'National Second Division champions'

// ---------------------------------------------------------------------------
// 共用：俱樂部識別（SiteHeader／SiteFooter／各頁麵包屑與 SEO 共用）
// ---------------------------------------------------------------------------

const CLUB_IDENTITY_EN: ClubIdentity = {
  aboutLabelZh: 'About TCRFC',
  cultureLabelZh: 'TCRFC Culture',
  academyLabelZh: 'TCRFC Academy',
  academyLabelEn: 'ACADEMY',
  academyShortLabelZh: 'Academy',
  brandTagEn: 'TCRFC',
  slogan: { zh: 'LOCAL ROOTS. GLOBAL PATHWAYS.', en: 'LOCAL ROOTS. GLOBAL PATHWAYS.' },
  footerBlurb: 'Taichung Rock FC develops players who pursue excellence through a professional model, so the world can see Taiwan football.',
  copyrightZh: '© 2026 TAICHUNG ROCK FOOTBALL CLUB. All rights reserved.',
  social: {
    facebook: 'https://www.facebook.com/TCRFC2024',
    instagram: 'https://www.instagram.com/tcr_fc_2024',
    youtube: 'https://www.youtube.com/@TCRFC-2024',
    line: null,
    email: null,
  },
}

/**
 * 英文版俱樂部識別（只回 tcrfc）。欄位名稱沿用 `ClubIdentity`（`aboutLabelZh` 等欄位放英文值）。
 * 其他群組：`const identity = computed(() => isEn.value ? getClubIdentityEn() : getClubIdentity(clubKey.value))`。
 */
export function getClubIdentityEn(): ClubIdentity {
  return CLUB_IDENTITY_EN
}

/**
 * 藍鯨英文版俱樂部識別（B-5）。欄位名稱沿用 `ClubIdentity`（`aboutLabelZh` 等欄位放英文值）。
 * - `academyLabelZh`／`academyShortLabelZh`：04 單元藍鯨叫 Youth Teams／Youth（不得出現 Academy）。
 * - `brandTagEn` 與繁中版一致為 null（eyebrow 只輸出編號）。
 * - 版權列最後一項（足球學校）舊站只有中文名，英文名無來源，採保守描述寫法 `Taichung Blue Whale Football School`（待客戶確認）。
 * - 臺中市女子足球協會用 `Taichung Women's Football Association`（初稿，待確認，見 docs/06）。
 */
const CLUB_IDENTITY_EN_BW: ClubIdentity = {
  aboutLabelZh: `About ${BW_NAME_EN}`,
  cultureLabelZh: `${BW_NAME_EN} Culture`,
  academyLabelZh: 'Youth Teams',
  academyLabelEn: 'YOUTH',
  academyShortLabelZh: 'Youth',
  brandTagEn: null,
  slogan: { zh: `${BW_NAME_EN} rides the waves towards the open ocean`, en: `${BW_NAME_EN} rides the waves towards the open ocean` },
  footerBlurb: `${BW_FULL_NAME_EN}, part of the Taichung Women's Football Association, is one of the teams of the Taiwan Mulan Football League. Taking the blue whale as its symbol, it stands for a faster, stronger and more modern style of football, and hopes to lift the grassroots football culture in Taichung and drive the development of women's football in central Taiwan.`,
  copyrightZh: `© 2014 | Taichung Women's Football Association | ${BW_FULL_NAME_EN} | ${BW_NAME_EN} Football School`,
  social: {
    facebook: 'https://www.facebook.com/tbwfc',
    instagram: 'https://instagram.com/tcbw2014',
    youtube: 'https://www.youtube.com/@user-xu1wm3xx1w',
    line: 'https://lin.ee/CS65qCR',
    email: 'fbbh2014@gmail.com',
  },
}

export function getClubIdentityEnBw(): ClubIdentity {
  return CLUB_IDENTITY_EN_BW
}

/** 依俱樂部取英文識別（`club` 為 `bw` 回藍鯨，其餘回磐石）。 */
export function getClubIdentityEnFor(club: string): ClubIdentity {
  return club === 'bw' ? CLUB_IDENTITY_EN_BW : CLUB_IDENTITY_EN
}

/** 「About TCRFC」類 eyebrow 的英文版：`aboutEyebrowEn('2.1')` → `2.1 About TCRFC`。 */
export function aboutEyebrowEn(num: string, club: string = 'tcrfc'): string {
  // 藍鯨沒有英文縮寫（brandTagEn 為 null），與繁中版一致只輸出編號。
  return club === 'bw' ? num : `${num} About ${CLUB_IDENTITY_EN.brandTagEn}`
}

// ---------------------------------------------------------------------------
// 01 首頁
// ---------------------------------------------------------------------------

export function getHomeSeoEn(facts: SiteFacts): SeoCopy {
  const title = facts.foundingTitleZh ? `, ${facts.foundedYear} ${FOUNDING_TITLE_EN}` : ''
  return {
    title: 'Taichung Rock FC (TCRFC) | LOCAL ROOTS. GLOBAL PATHWAYS.',
    description: `Official website of Taichung Rock FC (TCRFC). Founded in ${facts.foundedYear}${title}. Four pillars: the First Team, the TCRFC Academy, programs and camps, and women's football.`,
  }
}

/** 英文版首頁 Hero：標語本身就是 H1，故 `kickerEn` 為 null（避免同一句話出現兩次）。連結沿用中文版（頁面端套 lp()）。 */
export function getHomeHeroEn(facts: SiteFacts): HomeHeroCopy {
  const titlePart = facts.foundingTitleZh ? ` · <b>${facts.foundedYear} ${FOUNDING_TITLE_EN}</b>` : ''
  return {
    kickerEn: null,
    headlineZh: 'LOCAL ROOTS.<br>GLOBAL PATHWAYS.',
    factLineZh: `Taichung Rock FC · <b>Founded in ${facts.foundedYear}</b>${titlePart}`,
    ctaPrimaryHref: '/zh/charity/',
    ctaSecondaryLabelZh: 'Get to know TCRFC',
    ctaSecondaryHref: '/zh/culture/',
  }
}

/** 首頁「加入球隊」主按鈕預設文字（後台輪播沒有 CTA 時使用）。 */
export const HOME_JOIN_LABEL_EN = 'Join as a Player'

/** 首頁四大支柱。圖片敘述（imgAlt）為英文；`zhLabel` 欄位放英文顯示名稱。 */
export const HOME_PILLARS_EN: PillarCopy[] = [
  { enLabel: 'FOOTBALL CLUB', zhLabel: 'Taichung Rock FC', linkLabelZh: 'Meet the First Team', href: '/zh/schedule/', imgAlt: 'Taichung Rock FC first team taking the field in a night match', imgWidth: 1280, imgHeight: 855 },
  { id: 'academy', enLabel: 'ACADEMY', zhLabel: 'TCRFC Academy', linkLabelZh: 'Discover the Academy', href: '/zh/academy/', imgAlt: 'Young players from the TCRFC Academy at an exchange match in Slovakia', imgWidth: 1920, imgHeight: 1279 },
  { id: 'programs', enLabel: 'PROGRAMS', zhLabel: 'Programs & Activities', linkLabelZh: 'View the timetable', href: '/zh/programs/', imgAlt: 'A football training session in which a coach guides players through a dribbling drill around cones', imgWidth: 1920, imgHeight: 1279 },
  { id: 'womens', enLabel: "WOMEN'S FOOTBALL", zhLabel: "Women's Football", linkLabelZh: 'Meet ' + BW_NAME_EN, href: '/zh/womens/', imgAlt: "A women's football match", imgWidth: 1280, imgHeight: 853 },
]

/** 首頁與一線隊頁共用的「加入我們」CTA 三卡（英文版，欄位名稱沿用 `CtaCardCopy`）。 */
export function getHomeCtaTrioEn(): CtaCardCopy[] {
  return [
    {
      num: '10.1',
      titleZh: 'Join as a Player',
      descZh: 'Have the ability and the drive to prove yourself in the league? We are continuously recruiting players for the First Team and our age-group squads.',
      ctaLabelZh: 'Complete the Application',
      href: '/zh/join/player/',
    },
    {
      num: '10.2',
      titleZh: "Academy & Children's Training",
      descZh: "From basic technique to understanding the game, the TCRFC Academy and our children's training programs offer systematic football training for every age group.",
      ctaLabelZh: 'Book a Trial',
      href: '/zh/join/academy/',
    },
    {
      num: '10.5',
      titleZh: 'Become a Partner',
      descZh: 'Partner with Taichung Rock FC to reach the local community through a professional football platform and create shared value for your brand and the community.',
      ctaLabelZh: 'Discuss a Partnership',
      href: '/zh/join/partnership/',
    },
  ]
}

/** 五大核心價值說明文字（對應 shared/utils/core-values.ts 的 DESCRIPTIONS，依 code 取用）。 */
export const CORE_VALUE_DESCRIPTIONS_EN: Readonly<Record<string, string>> = {
  players_first: "All training plans and resource allocation put players' long-term development and wellbeing first.",
  excellence: 'We build a professional training and coaching system to help players develop the ability and mindset needed to reach the professional stage.',
  global_pathways: 'Starting from Taichung and looking to the world, we build pathways to the professional stage through international exchange.',
  community: 'Rooted in Taichung, we aim to be a source of local pride and identity, growing together with our fans.',
  integrity: "Honest governance and professional systems support the club's long-term, steady development.",
}

// ---------------------------------------------------------------------------
// 02 ABOUT 系列（欄位沿用 `HeroCopy`／`SeoCopy`：`h1Zh` 放英文 H1，`h1En` 恆為 null）
// ---------------------------------------------------------------------------

export const ABOUT_INDEX_SEO_EN: SeoCopy = {
  title: 'About TCRFC | Taichung Rock FC',
  description: 'Get to know Taichung Rock FC: our story, vision and mission, football philosophy, people, governance, ecosystem, club history and key milestones.',
}

export function getAboutIndexHeroEn(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'About TCRFC',
    h1En: null,
    lede: `LOCAL ROOTS. GLOBAL PATHWAYS. Taichung Rock FC was founded in Taichung in ${facts.foundedYear}. These eight chapters take you through the club, from its ideas to its organisation.`,
  }
}

/** about/index.vue 導覽卡描述（英文版）。 */
export const ABOUT_NAV_DESC_EN = {
  ourStory: "How Taichung Rock FC has developed since its founding.",
  visionMission: "Taichung Rock FC's core vision and the club's mission.",
  philosophy: 'Our football philosophy and five core values.',
  ourPeople: "Meet Taichung Rock FC's coaching staff and administrative team.",
  ecosystem: "An overview of our four systems: the First Team, the Academy, programs and women's football.",
  history: "A visual record of Taichung Rock FC's journey.",
  governance: 'Organisational structure, governance principles and public documents.',
  milestones: "A year-by-year view of the club's key events.",
} as const

export function getOurStorySeoEn(facts: SiteFacts): SeoCopy {
  return {
    title: 'Our Story | About TCRFC | Taichung Rock FC',
    description: `Taichung Rock FC (TCRFC) was founded in Taichung in ${facts.foundedYear}. This page will tell the club's story from its founding to today; the full text is still being prepared.`,
  }
}

export function getOurStoryHeroEn(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'Our Story',
    h1En: null,
    lede: `LOCAL ROOTS. GLOBAL PATHWAYS. Taichung Rock FC was founded in Taichung in ${facts.foundedYear}. This is the chapter on our history; the full text is being confirmed with the club.`,
  }
}

export const VISION_MISSION_SEO_EN: SeoCopy = {
  title: 'Vision & Mission | About TCRFC | Taichung Rock FC',
  description: 'The vision and mission of Taichung Rock FC: through a professional development system, we help local players in Taichung reach the professional stage and let the world see Taiwan football.',
}

export const VISION_MISSION_HERO_EN: HeroCopy = {
  h1Zh: 'Vision & Mission',
  h1En: null,
  lede: 'Starting from Taichung: developing local players for the professional game, becoming a source of local pride, and letting the world see Taiwan football through the sport.',
}

export const VISION_ITEMS_EN: VisionItem[] = [
  { kicker: 'VISION', titleZh: 'Vision', textZh: 'Starting from Taichung, we develop local players for the professional stage and become a source of local pride.' },
  { kicker: 'MISSION', titleZh: 'Mission', textZh: 'Through a solid training system and international connections, we let the world see Taiwan football.' },
]

export const PHILOSOPHY_SEO_EN: SeoCopy = {
  title: 'Our Philosophy | About TCRFC | Taichung Rock FC',
  description: 'The football philosophy and five core values of Taichung Rock FC: Players First, Excellence, Global Pathways, Community and Integrity.',
}

export const PHILOSOPHY_HERO_EN: HeroCopy = {
  h1Zh: 'Our Philosophy',
  h1En: null,
  lede: 'Developing players who pursue excellence through a professional model, so the world can see Taiwan football.',
}

export const OUR_PEOPLE_SEO_EN: SeoCopy = {
  title: 'Our People | About TCRFC | Taichung Rock FC',
  description: "Meet the coaching staff and advisers of Taichung Rock FC: the head coach, coaches, goalkeeper coach, fitness coach, youth development director, youth coaches and technical adviser.",
}

export const OUR_PEOPLE_HERO_EN: HeroCopy = {
  h1Zh: 'Our People',
  h1En: null,
  lede: "The coaching staff and advisers who keep Taichung Rock FC's First Team running. Select any member to see more details.",
}

export const GOVERNANCE_SEO_EN: SeoCopy = {
  title: 'Governance | About TCRFC | Taichung Rock FC',
  description: 'Governance information and a download area for the public documents of Taichung Rock FC. Content is updated regularly.',
}

export const GOVERNANCE_HERO_EN: HeroCopy = {
  h1Zh: 'Governance',
  h1En: null,
  lede: 'Governance information will be published progressively. Below are the public documents currently available.',
}

export const ECOSYSTEM_SEO_EN: SeoCopy = {
  title: 'TCRFC Ecosystem | About TCRFC | Taichung Rock FC',
  description: "The four systems of Taichung Rock FC: the First Team, the TCRFC Academy, programs and activities, and women's football. Select an icon to go to each page.",
}

export const ECOSYSTEM_HERO_EN: HeroCopy = {
  h1Zh: 'TCRFC Ecosystem',
  h1En: null,
  lede: "With the First Team at its core, Taichung Rock FC extends downward into the Academy, programs and women's football, forming a complete football development ecosystem. Select any part to go to its page.",
}

/** 生態系四個節點（英文版）。`zhLabel` 欄位放英文顯示名稱，頁面端英文版不再顯示 `enLabel` 小標。 */
export function getEcosystemNodesEn(facts: SiteFacts): EcoNode[] {
  return [
    { num: '3', slug: 'club', enLabel: 'Football Club', zhLabel: 'First Team', descZh: `The club's core squad, competing in ${leagueEn(facts)}.`, href: '/zh/club/' },
    { num: '4', slug: 'academy', enLabel: 'Academy', zhLabel: 'TCRFC Academy', descZh: `The ${listAnd(facts.squadCodes)} squads, forming the youth pathway into the First Team.`, href: '/zh/academy/' },
    { num: '5', slug: 'programs', enLabel: 'Programs', zhLabel: 'Programs & Activities', descZh: "Children's training, summer and winter camps, specialist training, and school and community programs.", href: '/zh/programs/' },
    { num: '6', slug: 'womens', enLabel: "Women's Football", zhLabel: "Women's Football", descZh: BW_NAME_EN + "'s women's team, which has its own official website.", badgeZh: 'Official website', href: '/zh/womens/' },
  ]
}

/** 生態系頁視覺隱藏標題（對應 ECOSYSTEM_TITLE.tcrfc）。 */
export const ECOSYSTEM_TITLE_EN = 'Overview diagram of the four systems'

export function getHistorySeoEn(facts: SiteFacts): SeoCopy {
  return {
    title: 'Club History | About TCRFC | Taichung Rock FC',
    description: `A visual narrative of how Taichung Rock FC has developed since its founding in ${facts.foundedYear}. The full text is still being prepared.`,
  }
}

/** `lede` 含內嵌連結（頁面端用 v-html，並以 localizeHtmlLinks 換成目前語系）。 */
export const HISTORY_HERO_EN: HeroCopy = {
  h1Zh: 'Club History',
  h1En: null,
  lede: 'A visual record of how Taichung Rock FC has developed. For the key events year by year, start with the <a href="/zh/about/milestones/" style="color:#fff;text-decoration:underline;">2.8 Key Milestones</a> timeline.',
}

export const MILESTONES_SEO_EN: SeoCopy = {
  title: 'Key Milestones | About TCRFC | Taichung Rock FC',
  description: 'A timeline of key events at Taichung Rock FC from 2024 to 2026, including the founding, the title win, international cooperation agreements and new signings, with year filtering.',
}

export function getMilestonesHeroEn(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'Key Milestones',
    h1En: null,
    lede: `Since our founding in ${facts.foundedYear}, Taichung Rock FC has steadily built First Team results, an international cooperation network and a local community presence. The timeline below is compiled from published club news and can be filtered by year.`,
  }
}

// ===========================================================================
// 藍鯨英文版（B-5，2026-10-05）——命名 `FOO_EN_BW`／`getFooEnBw(facts)`
//
// 來源一律是 club-copy.ts 的 bw 分支繁中原文（節錄自 content/blue-whale/ 舊站原文），只翻譯、不新增事實。
// 名稱：簡稱 `BW_NAME_EN`、全名 `BW_FULL_NAME_EN`；聯賽名走 facts（`leagueEn`）。
// 🔵 待客戶確認：臺中市女子足球協會英文名採 `Taichung Women's Football Association`（初稿，docs/06）；
//   足球學校英文名無來源，採保守描述寫法 `Taichung Blue Whale Football School`。
// ===========================================================================

/** 藍鯨首頁 SEO（對應 getHomeSeo('bw')）。 */
export function getHomeSeoEnBw(facts: SiteFacts): SeoCopy {
  return {
    title: `${BW_FULL_NAME_EN} | ${BW_NAME_EN} rides the waves towards the open ocean`,
    description: `Official website of ${BW_FULL_NAME_EN}. Part of the Taichung Women's Football Association, founded in ${facts.foundedYear}, one of the teams of ${leagueEn(facts)} and five-time Mulan League champions. Three systems: the First Team, the Youth Teams and community programs.`,
  }
}

/** 藍鯨英文版首頁 Hero：標語即 H1，故 `kickerEn` 為 null。連結沿用中文版（頁面端套 lp()）。 */
export function getHomeHeroEnBw(facts: SiteFacts): HomeHeroCopy {
  const league = facts.league.nameEn ? `the ${facts.league.nameEn}` : 'the league'
  return {
    kickerEn: null,
    headlineZh: `${BW_NAME_EN}<br>rides the waves towards<br>the open ocean`,
    factLineZh: `${BW_FULL_NAME_EN} · <b>Founded in ${facts.foundedYear}</b> · <b>Five-time champions of ${league}</b>`,
    ctaPrimaryHref: '/zh/join/player/',
    ctaSecondaryLabelZh: `Get to know ${BW_NAME_EN}`,
    ctaSecondaryHref: '/zh/about/',
  }
}

/** 藍鯨首頁支柱（三項；`zhLabel` 欄位放英文顯示名稱；圖片敘述與中文版一致為空字串）。 */
export const HOME_PILLARS_EN_BW: PillarCopy[] = [
  { enLabel: 'FIRST TEAM', zhLabel: `${BW_NAME_EN} First Team`, linkLabelZh: 'Meet the First Team', href: '/zh/club/first-team/', imgAlt: '', imgWidth: 1280, imgHeight: 855 },
  { id: 'academy', enLabel: 'YOUTH', zhLabel: 'Youth Teams', linkLabelZh: 'Discover the Youth Teams', href: '/zh/academy/', imgAlt: '', imgWidth: 1920, imgHeight: 1279 },
  { id: 'programs', enLabel: 'PROGRAMS', zhLabel: 'Community Programs', linkLabelZh: 'View the activities', href: '/zh/programs/', imgAlt: '', imgWidth: 1920, imgHeight: 1279 },
]

/** 藍鯨首頁「加入我們」CTA 三卡。 */
export function getHomeCtaTrioEnBw(facts: SiteFacts): CtaCardCopy[] {
  return [
    {
      num: '10.1',
      titleZh: 'Join as a Player',
      descZh: `Have the ability and the drive to prove yourself in ${leagueEn(facts)}? We are continuously recruiting First Team players.`,
      ctaLabelZh: 'Complete the Application',
      href: '/zh/join/player/',
    },
    {
      num: '10.2',
      titleZh: 'Join the Youth Teams',
      descZh: `${listAnd(facts.squadCodes)} girls' youth football teams, offering systematic football training.`,
      ctaLabelZh: 'Enquire to Register',
      href: '/zh/join/academy/',
    },
    {
      num: '10.5',
      titleZh: 'Become a Partner',
      descZh: `Partner with ${BW_NAME_EN} to reach the local community through a women's football platform and create shared value for your brand and the community.`,
      ctaLabelZh: 'Discuss a Partnership',
      href: '/zh/join/partnership/',
    },
  ]
}

// --- 02 ABOUT 系列（藍鯨）---------------------------------------------------

export const ABOUT_INDEX_SEO_EN_BW: SeoCopy = {
  title: `About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
  description: `Get to know ${BW_FULL_NAME_EN}: our story, development vision, club slogan and training spirit, people, governance, ecosystem, club history and key milestones.`,
}

export function getAboutIndexHeroEnBw(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: `About ${BW_NAME_EN}`,
    h1En: null,
    lede: `${BW_FULL_NAME_EN} is part of the Taichung Women's Football Association, was founded in ${facts.foundedYear}, and is one of the teams of ${leagueEn(facts)}. These chapters take you through the team from its ideas to its organisation.`,
  }
}

/** about/index.vue 導覽卡描述（藍鯨英文版；未改的兩張沿用主站通用句）。 */
export const ABOUT_NAV_DESC_EN_BW = {
  ...ABOUT_NAV_DESC_EN,
  ourStory: `How ${BW_NAME_EN} has developed since its founding.`,
  visionMission: `The development vision of ${BW_NAME_EN}.`,
  philosophy: 'Our club slogan and training spirit.',
  ourPeople: `Meet the coaching staff of ${BW_NAME_EN}.`,
  ecosystem: 'An overview of our three systems: the First Team, the Youth Teams and community programs.',
  history: `A visual record of the ${BW_NAME_EN} journey.`,
} as const

export function getOurStorySeoEnBw(facts: SiteFacts): SeoCopy {
  return {
    title: `Our Story | About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
    description: `${BW_FULL_NAME_EN} was founded in Taichung in ${facts.foundedYear} under the Taichung Women's Football Association. Learn about the team's positioning and founding purpose.`,
  }
}

export function getOurStoryHeroEnBw(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'Our Story',
    h1En: null,
    lede: `${BW_FULL_NAME_EN} was founded in ${facts.foundedYear} under the Taichung Women's Football Association, and is one of the teams of ${leagueEn(facts)}.`,
  }
}

/** 對應 OUR_STORY_BODY_BW（舊站定位敘述的英文翻譯，未增刪）。 */
export const OUR_STORY_BODY_EN_BW =
  `${BW_FULL_NAME_EN}, part of the Taichung Women's Football Association and known for short as ${BW_NAME_EN}, is one of the teams of the Taiwan Mulan Football League. Taking the blue whale as its symbol, it stands for a faster, stronger and more modern style of football with an emphasis on teamwork; the whale's fin is an emblem of Taiwan, leading Taiwan football forward. ${BW_NAME_EN} hopes to lift the grassroots football culture in Taichung and drive the development of women's football in central Taiwan.`

export const VISION_MISSION_SEO_EN_BW: SeoCopy = {
  title: `Development Vision | About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
  description: `The development vision of ${BW_FULL_NAME_EN}: endless exploration, resilience that fears no difficulty, a more refined attitude, the most genuine impact and a deeper purpose.`,
}

export const VISION_MISSION_HERO_EN_BW: HeroCopy = {
  h1Zh: 'Development Vision',
  h1En: null,
  lede: `The spirit of "Pursuit of Brilliance" drives ${BW_NAME_EN} to keep moving forward.`,
}

export const VISION_ITEMS_EN_BW: VisionItem[] = [
  { kicker: '01', titleZh: 'Endless Exploration', textZh: 'Raise and popularise the standard of football in greater Taichung, absorb more professional and refined football skills, catch up with the technical level of Asian football and meet the global trend.' },
  { kicker: '02', titleZh: 'Resilience That Fears No Difficulty', textZh: 'Create a football culture and atmosphere with fans at the centre, so that matches reach a higher standard: better technique on the pitch and a livelier matchday atmosphere off it.' },
  { kicker: '03', titleZh: 'A More Refined Attitude', textZh: 'Widen the pathways for football players so they have more room to develop, with the support of parents, schools, government, industry and the public.' },
  { kicker: '04', titleZh: 'The Most Genuine Impact', textZh: "Build Taichung's reputation and pride as the capital of Taiwan football, with a healthy and positive football culture that connects fans who love the sport with the football dreams that players pursue." },
  { kicker: '05', titleZh: 'A Deeper Purpose', textZh: 'Keep giving back to society and strive to create a better life for people. We believe we can bring change, helping people share and connect with one another in new ways and making the world more harmonious.' },
]

export const VISION_FOOTNOTE_EN_BW = 'For our club slogan and training spirit, see <a href="/zh/about/philosophy/">2.3 Club Slogan and Training Spirit</a>.'

export const PHILOSOPHY_SEO_EN_BW: SeoCopy = {
  title: `Club Slogan and Training Spirit | About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
  description: `The club slogan and training spirit of ${BW_FULL_NAME_EN}, and the design concept behind the blue whale on the club crest.`,
}

export const PHILOSOPHY_HERO_EN_BW: HeroCopy = {
  h1Zh: 'Club Slogan and Training Spirit',
  h1En: null,
  lede: 'The blue whale is our symbol, representing a faster, stronger and more modern style of football with an emphasis on teamwork.',
}

/** 對應 PHILOSOPHY_QUOTES_BW（欄位名稱沿用，值為英文翻譯）。 */
export const PHILOSOPHY_QUOTES_EN_BW = {
  crestZh: "The blue whale is our symbol, representing a faster, stronger and more modern style of football with an emphasis on teamwork; the whale's fin is an emblem of Taiwan, leading Taiwan football forward.",
  sloganZh: 'The blue sky is the direction of the dreams in our hearts, the glittering sunlight is the strength that carries us towards them, and the sweat shed on the grass is our growth and our steadfast belief. With you by our side we are no longer lost. Right now, we stand together with our fans, and face the world together.',
  spiritZh: 'Do not be afraid; be brave and go for it. Stride forward and ride the waves; even if you are covered in scars, fly into the wind. Lift your head, the dream is ahead. Cross those high walls, and no one can stop us. The brighter our eyes shine, the more the whole world is our stage.',
} as const

export const OUR_PEOPLE_SEO_EN_BW: SeoCopy = {
  title: `Our People | About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
  description: `Meet the coaching staff of ${BW_FULL_NAME_EN}.`,
}

export const OUR_PEOPLE_HERO_EN_BW: HeroCopy = {
  h1Zh: 'Our People',
  h1En: null,
  lede: `The coaching staff who keep the ${BW_NAME_EN} First Team running. The list is maintained in the back office and the content is being updated.`,
}

export const GOVERNANCE_SEO_EN_BW: SeoCopy = {
  title: `Governance | About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
  description: `Governance information and a download area for the public documents of ${BW_FULL_NAME_EN}. Content is updated regularly.`,
}

export const ECOSYSTEM_SEO_EN_BW: SeoCopy = {
  title: `Ecosystem | About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
  description: `The three systems of ${BW_NAME_EN}: the First Team, the Youth Teams and community programs. Select an icon to go to each page.`,
}

export const ECOSYSTEM_HERO_EN_BW: HeroCopy = {
  h1Zh: 'Ecosystem',
  h1En: null,
  lede: `With the First Team at its core, ${BW_NAME_EN} extends downward into the Youth Teams and community programs, forming a complete football development ecosystem. Select any part to go to its page.`,
}

export function getEcosystemNodesEnBw(facts: SiteFacts): EcoNode[] {
  return [
    { num: '3', slug: 'club', enLabel: 'First Team', zhLabel: 'First Team', descZh: `The team that competes in ${leagueEn(facts)}.`, href: '/zh/club/' },
    { num: '4', slug: 'youth', enLabel: 'Youth', zhLabel: 'Youth Teams', descZh: `${listAnd(facts.squadCodes)} girls' youth football teams, forming the youth pathway into the First Team.`, href: '/zh/academy/' },
    { num: '5', slug: 'programs', enLabel: 'Programs', zhLabel: 'Community Programs', descZh: 'Community football schools, sports hotspot programs, coach workshops and the football festival.', href: '/zh/programs/' },
  ]
}

export const ECOSYSTEM_TITLE_EN_BW = 'Overview diagram of the three systems'

export function getHistorySeoEnBw(): SeoCopy {
  return {
    title: `Club History | About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
    description: `A year-by-year history of ${BW_FULL_NAME_EN} from 2014 to 2025, compiled from content published on the former official website.`,
  }
}

export function getHistoryHeroEnBw(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'Club History',
    h1En: null,
    lede: `A year-by-year history of ${BW_FULL_NAME_EN} since its founding in ${facts.foundedYear}, compiled from content published on the former official website.`,
  }
}

export const HISTORY_YEAR_SUFFIX_EN_BW = ''
export const HISTORY_NOTE_EN_BW = "The history above is compiled from content published on the former official website. The edition numbers of some seasons differ from other sources and will be updated once the club has confirmed them."

export const MILESTONES_SEO_EN_BW: SeoCopy = {
  title: `Key Milestones | About ${BW_NAME_EN} | ${BW_FULL_NAME_EN}`,
  description: `The key events of ${BW_FULL_NAME_EN} by year. See the Club History page for the year-by-year record from 2014 to 2025.`,
}

export const MILESTONES_HERO_EN_BW: HeroCopy = {
  h1Zh: 'Key Milestones',
  h1En: null,
  lede: `The key events of ${BW_NAME_EN} are currently collected in the year-by-year record from 2014 to 2025 on the Club History page. The year-filter timeline on this page has not yet been rebuilt from ${BW_NAME_EN} data.`,
}

/** 後台有里程碑資料時的中性導言（藍鯨）。 */
export const MILESTONES_API_LEDE_EN_BW = `Key milestones of ${BW_NAME_EN}, which can be filtered by year.`
