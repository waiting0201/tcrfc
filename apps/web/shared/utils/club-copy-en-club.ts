// shared/utils/club-copy-en-club.ts — 主站（tcrfc）英文版文案：club 群組
// （俱樂部 03 單元、女子足球 06、特約店家／文化 08 的 SEO、Hero、導覽卡文字與資料標籤）
//
// 約定（英文版文案檔共通，群組代號 club）：
// - 只提供 tcrfc 的英文值；藍鯨站英文版不在範圍（isEn 在藍鯨一律 false）。
// - 原匯出 `FOO`（常數）→ `FOO_EN`；原函式 `getFoo(club, facts)` → `getFooEn(facts)`。
// - 型別沿用 club-copy.ts 的 interface，**欄位名稱不變但值是英文**（例如 `h1Zh` 欄位放英文 H1、
//   `lede` 放英文導言、`titleZh`／`descZh` 放英文標題與敘述），頁面端只要把來源換成 `_EN` 即可，不必改渲染。
// - 檔內字串不得含中文字元；英文用詞一律照 docs/06-conventions.md §1.1 對照表。
// - 事實（成立年、聯賽、場地、頭銜）一律由呼叫端傳入的 `facts` 取值，不在此寫死字面值
//   （check-fact-single-source.mjs）；`facts` 的英文欄位為 null 時改用保守的描述性寫法（the league 等），
//   不自創正式名稱。
// - 本檔另含英文版專用的資料標籤小工具（`club*Label*En`），名稱一律以 `club` 開頭避免與其他群組撞名。

import type { HeroCopy, SeoCopy, ClubHubStat, CtaCardCopy } from './club-copy'
import type { SiteFacts } from './site-facts'
import { BW_NAME_EN_PENDING } from './club-copy'

// ---------------------------------------------------------------------------
// 共用小工具（本檔內部使用，不匯出）
// ---------------------------------------------------------------------------

/** 聯賽的英文行文寫法：有正式英文名用「the <名稱>」，否則保守寫「the league」。 */
function leagueEn(facts: SiteFacts): string {
  return facts.league.nameEn ? `the ${facts.league.nameEn}` : 'the league'
}

/** 主場英文名；資料沒有英文名時退回原中文名稱（不自創譯名）。 */
function primaryVenueEn(facts: SiteFacts): string {
  const v = facts.venues.find((x) => x.isHomeGround) ?? facts.venues[0]
  return v ? (v.nameEn ?? v.nameZh) : ''
}

// ---------------------------------------------------------------------------
// 03 單元 hub（club/index.vue）
// ---------------------------------------------------------------------------

export function getClubHubSeoEn(): SeoCopy {
  return {
    title: 'Football Club | Taichung Rock FC',
    description:
      'Overview of the Taichung Rock Football Club (TCRFC) section: the First Team, Player Development, Player Opportunities, International Pathways and Player Stories, showing how the First Team develops players for the professional and international stage.',
  }
}

export function getClubHubHeroEn(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'Football Club',
    h1En: null,
    lede: `The First Team, based at ${primaryVenueEn(facts)}, is the stage that every part of the Taichung Rock youth system leads to. Here you will find the squad, Player Development, ways to join, and the pathways players take overseas.`,
  }
}

/** 統計卡——「28」「21」兩個數字沿用 `getClubHubStats` 既有字面值（本來就不在 SiteFacts 內）。 */
export function getClubHubStatsEn(facts: SiteFacts): ClubHubStat[] {
  return [
    { num: facts.foundedYear, labelZh: 'Year founded' },
    { num: facts.foundedYear, labelZh: facts.foundingTitleEn ?? '' },
    { num: '28', labelZh: 'First Team registered players' },
    { num: '21', labelZh: '2026/27 regular-season league matches' },
  ]
}

export const CLUB_HUB_OPPORTUNITIES_DESC_EN =
  'Joining Taichung Rock, trial dates with online registration, and the recruitment channel for international players.'

export const CLUB_HUB_PLAYER_STORIES_DESC_EN =
  "Real stories from the Academy, the First Team, players abroad and women's football, showing how each player got to where they are."

export const CLUB_HUB_CTA_TITLE_EN = 'Join the Taichung Rock First Team'

/** 底部 CTA 第一張卡「加入球隊」——對應首頁 `getHomeCtaTrio()` 第一筆（titleZh／descZh 放英文）。 */
export function getClubHubJoinPlayerCardEn(facts: SiteFacts): CtaCardCopy {
  return {
    num: '10.1',
    titleZh: 'Join the Squad',
    descZh: `Have the competitive ability and want to prove yourself in ${leagueEn(facts)}? We are continuously recruiting First Team and age-group players.`,
    ctaLabelZh: 'Fill in the registration form',
    href: '/zh/join/player/',
  }
}

// ---------------------------------------------------------------------------
// 03.1 一線隊
// ---------------------------------------------------------------------------

export function getFirstTeamSeoEn(facts: SiteFacts): SeoCopy {
  const league = facts.league.nameEn ? `2026/27 ${facts.league.nameEn}` : '2026/27 league'
  return {
    title: 'First Team | Taichung Rock FC',
    description: `Taichung Rock Football Club First Team: the 28-player registered squad ordered by shirt number, the coaching staff, the full ${league} fixture list with .ics subscription, and the honours timeline.`,
  }
}

/** 「球隊創立、同年即拿下首季頭銜」的英文句（頭銜缺英文時只留創立年份）。 */
function foundingClauseEn(facts: SiteFacts): string {
  return facts.foundingTitleEn
    ? `The team was founded in ${facts.foundedYear} and won the ${facts.foundingTitleEn} title that same year`
    : `The team was founded in ${facts.foundedYear}`
}

export function getFirstTeamHeroEn(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'First Team',
    h1En: null,
    lede: `The Taichung Rock First Team represents the club in ${leagueEn(facts)} and is the competitive stage that every youth and Academy player ultimately works towards. ${foundingClauseEn(facts)}, and its home ground is ${primaryVenueEn(facts)}.`,
  }
}

export function getFirstTeamIntroEn(facts: SiteFacts): string {
  const title = facts.foundingTitleEn ? `, won the ${facts.foundingTitleEn} title that same year,` : ''
  return `The Taichung Rock Football Club First Team was formed with the club in ${facts.foundedYear}${title} and now competes in ${leagueEn(facts)}. The team's home ground is ${primaryVenueEn(facts)}, and 21 regular-season league matches are scheduled for the 2026/27 season.`
}

/** 賽程列「主客」欄。 */
export function clubHomeAwayLabelEn(homeAway: string | null): string {
  if (homeAway === 'home') return 'Home'
  if (homeAway === 'away') return 'Away'
  return '\u2014'
}

/** 星期（`matchWeekday().en` 是全大寫縮寫 SAT）→ 首字大寫的 Sat。 */
export function clubWeekdayShortEn(upper: string): string {
  return upper.charAt(0) + upper.slice(1).toLowerCase()
}

/** 場上位置代碼（GK／DF／MF／FW）→ 英文全名；其他值原樣顯示。 */
export function clubPositionLabelEn(position: string | null | undefined): string {
  switch ((position ?? '').toUpperCase()) {
    case 'GK': return 'Goalkeeper'
    case 'DF': return 'Defender'
    case 'MF': return 'Midfielder'
    case 'FW': return 'Forward'
    default: return position ?? '\u2014'
  }
}

/** 慣用腳。 */
export function clubFootLabelEn(foot: string): string {
  const f = foot.toLowerCase()
  if (f === 'right') return 'Right foot'
  if (f === 'left') return 'Left foot'
  if (f === 'both') return 'Both feet'
  return foot
}

// ---------------------------------------------------------------------------
// 03.2 球員發展系統／03.3 球員機會／03.4 國際發展通道／03.5 球員故事
// ---------------------------------------------------------------------------

export function getPlayerDevelopmentSeoEn(): SeoCopy {
  return {
    title: 'Player Development | Taichung Rock FC',
    description:
      'Taichung Rock Football Club Player Development: technical and tactical analysis, physical fitness training, match reading, mental resilience, video analysis, Individual Development Plans, nutrition and lifestyle, and education and language, with all eight modules explained.',
  }
}

export function getPlayerDevelopmentHeroEn(): HeroCopy {
  return {
    h1Zh: 'Player Development',
    h1En: null,
    lede: 'From tactics to education and language, the eight modules form the complete framework of Taichung Rock Player Development, supporting First Team and age-group players on their way to the professional and international stage. Select a module card to read more.',
  }
}

export function getPlayerOpportunitiesSeoEn(): SeoCopy {
  return {
    title: 'Player Opportunities | Taichung Rock FC',
    description:
      'Player opportunities at Taichung Rock Football Club: how to apply to join the First Team, trial dates with online registration, and the recruitment channel for international players (English first).',
  }
}

export function getPlayerOpportunitiesHeroEn(): HeroCopy {
  return {
    h1Zh: 'Player Opportunities',
    h1En: null,
    lede: 'From joining the squad and attending trials to the recruitment channel for international players, this page brings together every way into the Taichung Rock First Team.',
  }
}

export function getJoinFirstTeamBodyEn(facts: SiteFacts): string {
  return `The Taichung Rock First Team represents the club in ${leagueEn(facts)} and continues to recruit players with the competitive ability to join the squad. After you submit the registration form, our football department, which receives these forms, will contact you about the next steps of the assessment.`
}

/** Foreign Player Recruitment 段落——聯賽英文名缺漏時只寫 the league（不印 null、不混中文）。 */
export function getForeignPlayerBodyEn(facts: SiteFacts): string {
  const league = facts.league.nameEn ? `Taiwan's ${facts.league.nameEn}` : 'the top-tier corporate league in Taiwan'
  return `Taichung Rock FC (TCRFC) First Team competes in ${league}. We welcome enquiries from foreign players interested in trialling or joining the squad. Please use the international enquiry form below and our International Department will follow up.`
}

export function getInternationalPathwaysSeoEn(): SeoCopy {
  return {
    title: 'International Pathways | Taichung Rock FC',
    description:
      'Taichung Rock Football Club International Pathways: the full route from local football to clubs abroad, regional information for Europe, Japan and Hong Kong, a wall of partner clubs, and channels for trial scouting and overseas club matching.',
  }
}

export function getInternationalPathwaysHeroEn(): HeroCopy {
  return {
    h1Zh: 'International Pathways',
    h1En: null,
    lede: 'LOCAL ROOTS. GLOBAL PATHWAYS. Starting from Taichung, the First Team and overseas exchanges build an international route for players towards the professional stage.',
  }
}

/** 國際夥伴「國家」欄的英文顯示：已含英文者取英文部分，純中文者查常見國家／地區，查不到維持原值。 */
export function clubCountryLabelEn(country: string | null): string | null {
  if (!country) return null
  const latin = country.replace(/[^ -\u024f]+/g, ' ').replace(/\s+/g, ' ').trim()
  if (latin) return latin
  const map: Record<string, string> = {
    '\u65e5\u672c': 'Japan',
    '\u9999\u6e2f': 'Hong Kong',
    '\u4e2d\u570b': 'China',
    '\u4e2d\u56fd': 'China',
    '\u7fa9\u5927\u5229': 'Italy',
    '\u897f\u73ed\u7259': 'Spain',
    '\u5fb7\u570b': 'Germany',
    '\u53f0\u7063': 'Taiwan',
  }
  return map[country.trim()] ?? country
}

export function getPlayerStoriesSeoEn(): SeoCopy {
  return {
    title: 'Player Stories | Taichung Rock FC',
    description:
      "Player stories from Taichung Rock Football Club, filterable by Academy, First Team, Overseas and Women's football, recording each player's real journey from joining Taichung Rock to a bigger stage.",
  }
}

export function getPlayerStoriesHeroEn(): HeroCopy {
  return {
    h1Zh: 'Player Stories',
    h1En: null,
    lede: "Every player has a Taichung Rock journey of their own. This page collects real cases from the Academy, the First Team, players abroad and women's football. Three stories are published so far, with more being added.",
  }
}

// ---------------------------------------------------------------------------
// 06 女子足球（womens/index.vue）——介紹文為 club-copy.ts `OUR_STORY_BODY_BW` 的英文版
// ---------------------------------------------------------------------------

export const CLUB_WOMENS_SEO_EN: SeoCopy = {
  title: "Women's Football | Taichung Rock FC",
  description:
    "An introduction to the " + BW_NAME_EN_PENDING + " women's team and the entry point to its official website. For the full squad, coaching staff, fixtures and results, please visit the " + BW_NAME_EN_PENDING + " official website.",
}

/** `OUR_STORY_BODY_BW`（藍鯨定位敘述）的英文翻譯，不增加原文沒有的資訊。協會名稱採對照表初稿寫法（待客戶確認）。 */
export const WOMENS_STORY_BODY_EN =
  "The " + BW_NAME_EN_PENDING + " women's football team belongs to the Taichung Women's Football Association, is known for short as " + BW_NAME_EN_PENDING + ", and is one of the teams in the Taiwan Mulan Football League. The blue whale is its symbol of a faster, stronger and more modern way of playing football, with an emphasis on teamwork, and the whale's fin is an emblem of Taiwan, leading Taiwan football forward. " + BW_NAME_EN_PENDING + " hopes to lift the culture of grassroots football in Taichung and drive the development of women's football in central Taiwan."

/**
 * 06 女子足球頁面中含「Taichung Blue Whale」（對照表的描述性寫法，B-5 前不選正式全名）的英文句子，
 * 集中在本檔，頁面不自己寫死藍鯨英文名（check-bw-en-name.mjs 只需對 club-copy-en-*.ts 開例外）。
 */
export const CLUB_WOMENS_PAGE_EN = {
  lede: "The " + BW_NAME_EN_PENDING + " women's team is a women's football team supported by Taichung Rock. For the squad list, fixtures, results and other details, please visit the women's football official website.",
  introHeading: BW_NAME_EN_PENDING + " Women's Team",
  crestAlt: BW_NAME_EN_PENDING + ' crest',
  teamName: BW_NAME_EN_PENDING + " women's football team",
  fullInfo: 'For the squad list, coaching staff, fixtures and results, please visit the ' + BW_NAME_EN_PENDING + ' official website',
  officialHeading: 'For the full squad, fixtures and results, visit the ' + BW_NAME_EN_PENDING + ' website',
  officialLede: 'The squad list, coaching staff, fixtures and match results are all presented on the ' + BW_NAME_EN_PENDING + ' official website.',
  officialCta: 'Go to the ' + BW_NAME_EN_PENDING + ' official website',
} as const

/** 球員故事頁空狀態說明（英文版，含藍鯨官網連結文字；HTML 結構由頁面負責）。 */
export const CLUB_PLAYER_STORIES_BW_LINK_TEXT_EN = BW_NAME_EN_PENDING + " women's team website"
export const CLUB_PLAYER_STORIES_BW_PREFIX_EN = 'For the ' + BW_NAME_EN_PENDING + ' squad and fixtures, see the '

/** 台中藍鯨女子足球隊的所屬聯賽英文名（docs/06 §1.1 對照表）。 */
export const CLUB_WOMENS_LEAGUE_EN = 'Taiwan Mulan Football League'

/** 「成立」欄：有精確 ISO 日期時組「12 April 2014」，否則只寫年份。藍鯨英文欄位一律 null，故自行組字。 */
export function clubFoundedLabelEn(foundingDateIso: string | null, foundedYear: string): string {
  const m = foundingDateIso ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(foundingDateIso) : null
  if (!m) return `Founded in ${foundedYear}`
  const months = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
  const name = months[Number(m[2]) - 1]
  return name ? `Founded on ${Number(m[3])} ${name} ${m[1]}` : `Founded in ${foundedYear}`
}

/** 梯隊體系（藍鯨：一線隊與青年隊兩個梯隊並行）——由梯隊代碼清單組字。 */
export function clubWomensSquadStructureEn(squadCodes: string[]): string {
  return `A development system in which the First Team and the youth team (${squadCodes.join('/')}) run in parallel`
}

// ---------------------------------------------------------------------------
// 08 文化／特約店家／球迷會／商品／漫畫
// ---------------------------------------------------------------------------

export function getCultureHubSeoEn(showManga: boolean): SeoCopy {
  return {
    title: showManga
      ? 'TCRFC Culture | Manga, Fan Club and Merchandise | Taichung Rock FC'
      : 'TCRFC Culture | Fan Club and Merchandise | Taichung Rock FC',
    description: showManga
      ? 'Explore the Taichung Rock Football Club culture section: free online TCRFC Manga, Fan Club membership and benefits, and official merchandise with the online store.'
      : 'Explore the Taichung Rock Football Club culture section: the TCRFC Fan Club, official merchandise with the online store, and partner perks.',
  }
}

export const CLUB_PERKS_SEO_EN: SeoCopy = {
  title: 'Partner Perks | Taichung Rock FC',
  description:
    'Partner stores offering discounts to Taichung Rock members. Show your digital membership card in store to enjoy the offer; each store states whether it applies to registered members or Paid Fan Club members.',
}

export function getPerksDetailSeoEn(storeName: string | null | undefined, offer: string | null | undefined): SeoCopy {
  const name = storeName || 'Partner Perks'
  return {
    title: `${name} | Partner Perks | Taichung Rock FC`,
    description: offer || `${storeName ?? ''} is a partner store for Taichung Rock members.`,
  }
}

export const CLUB_FAN_CLUB_SEO_EN: SeoCopy = {
  title: 'TCRFC Fan Club | TCRFC Culture | Taichung Rock FC',
  description:
    'Join the Taichung Rock Fan Club: membership plans, a tier-by-tier comparison of member benefits, and fan event registration and reviews.',
}

export function getFanEventSeoEn(name: string, when: string, location: string | null | undefined): SeoCopy {
  return {
    title: `${name} | Fan Club Events | Taichung Rock FC`,
    description: `Taichung Rock Fan Club event "${name}"${when ? `: ${when}` : ''}${location ? `, ${location}` : ''}.`,
  }
}

export function getMerchandiseSeoEn(hasProducts: boolean): SeoCopy {
  return {
    title: 'Merchandise | TCRFC Culture | Taichung Rock FC',
    description: hasProducts
      ? 'Taichung Rock official merchandise: browse by collection, then choose a size and colour in the official store, pay with LINE Pay and receive an e-invoice.'
      : 'Taichung Rock official merchandise. Product content is provided through the back office and there is nothing to display yet.',
  }
}

export const CLUB_MANGA_SEO_EN: SeoCopy = {
  title: 'TCRFC Manga | TCRFC Culture | Taichung Rock FC',
  description:
    'TCRFC Manga is an original comic project of Taichung Rock Football Club: world-building, a character wall and an online episode reader, all free and with no login required.',
}

export function getMangaEpisodeSeoEn(episodeNo: number, title: string): SeoCopy {
  return {
    title: `Episode ${episodeNo}: ${title} | TCRFC Manga | Taichung Rock FC`,
    description: `Read TCRFC Manga episode ${episodeNo}, "${title}", online for free with no login required.`,
  }
}
