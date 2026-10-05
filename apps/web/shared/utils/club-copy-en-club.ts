// shared/utils/club-copy-en-club.ts — 主站（tcrfc）英文版文案：club 群組
// （俱樂部 03 單元、女子足球 06、特約店家／文化 08 的 SEO、Hero、導覽卡文字與資料標籤）
//
// 約定（英文版文案檔共通，群組代號 club）：
// - 預設匯出（`FOO_EN`／`getFooEn`）只提供 tcrfc 的英文值。
// - 藍鯨（bw）英文版變體（B-5，2026-10-05）：常數命名 `FOO_EN_BW`、函式命名 `getFooEnBw(facts)`
//   （不加參數、不動既有簽名的預設行為）。藍鯨英文只翻譯 `club-copy.ts` 的 bw 繁中原文，不新增事實；
//   藍鯨名稱一律取 `BW_NAME_EN`；聯賽、場地、成立日期取自呼叫端傳入的 `facts`（單一來源）。
//   人名沒有英文來源者維持中文，放在資料層（`INTL_PATHWAY_*_NOTES_BW`），不寫在本檔字串裡。
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
import { BW_NAME_EN } from './club-copy'

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
    "An introduction to the " + BW_NAME_EN + " women's team and the entry point to its official website. For the full squad, coaching staff, fixtures and results, please visit the " + BW_NAME_EN + " official website.",
}

/** `OUR_STORY_BODY_BW`（藍鯨定位敘述）的英文翻譯，不增加原文沒有的資訊。協會名稱採對照表初稿寫法（待客戶確認）。 */
export const WOMENS_STORY_BODY_EN =
  "The " + BW_NAME_EN + " women's football team belongs to the Taichung Women's Football Association, is known for short as " + BW_NAME_EN + ", and is one of the teams in the Taiwan Mulan Football League. The blue whale is its symbol of a faster, stronger and more modern way of playing football, with an emphasis on teamwork, and the whale's fin is an emblem of Taiwan, leading Taiwan football forward. " + BW_NAME_EN + " hopes to lift the culture of grassroots football in Taichung and drive the development of women's football in central Taiwan."

/**
 * 06 女子足球頁面中含「Taichung Blue Whale」（對照表的描述性寫法，B-5 前不選正式全名）的英文句子，
 * 集中在本檔，頁面不自己寫死藍鯨英文名（check-bw-en-name.mjs 只需對 club-copy-en-*.ts 開例外）。
 */
export const CLUB_WOMENS_PAGE_EN = {
  lede: "The " + BW_NAME_EN + " women's team is a women's football team supported by Taichung Rock. For the squad list, fixtures, results and other details, please visit the women's football official website.",
  introHeading: BW_NAME_EN + " Women's Team",
  crestAlt: BW_NAME_EN + ' crest',
  teamName: BW_NAME_EN + " women's football team",
  fullInfo: 'For the squad list, coaching staff, fixtures and results, please visit the ' + BW_NAME_EN + ' official website',
  officialHeading: 'For the full squad, fixtures and results, visit the ' + BW_NAME_EN + ' website',
  officialLede: 'The squad list, coaching staff, fixtures and match results are all presented on the ' + BW_NAME_EN + ' official website.',
  officialCta: 'Go to the ' + BW_NAME_EN + ' official website',
} as const

/** 球員故事頁空狀態說明（英文版，含藍鯨官網連結文字；HTML 結構由頁面負責）。 */
export const CLUB_PLAYER_STORIES_BW_LINK_TEXT_EN = BW_NAME_EN + " women's team website"
export const CLUB_PLAYER_STORIES_BW_PREFIX_EN = 'For the ' + BW_NAME_EN + ' squad and fixtures, see the '

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


// ===========================================================================
// 藍鯨（bw）英文版變體：`FOO_EN_BW`／`getFooEnBw(facts)`
// ===========================================================================

/** 藍鯨場地清單的英文行文（「A and B」）；場地英文名缺漏時退回資料層原名稱。 */
function bwVenuesEn(facts: SiteFacts): string {
  const names = facts.venues.map((v) => v.nameEn ?? v.nameZh)
  if (names.length <= 1) return names.join('')
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`
}

/** 「Founded on 12 April 2014」→ 句中用的「founded on 12 April 2014」；缺英文值時只寫年份。 */
function bwFoundedClauseEn(facts: SiteFacts): string {
  const base = facts.foundedDisplayEn ?? `Founded in ${facts.foundedYear}`
  return base.charAt(0).toLowerCase() + base.slice(1)
}

/** 俱樂部英文名稱後綴：`X | Taichung Blue Whale`（比照磐石的 `X | Taichung Rock FC`）。 */
const BW_SUFFIX_EN = ' | ' + BW_NAME_EN

// ---- 03 單元 hub ----

export function getClubHubSeoEnBw(facts: SiteFacts): SeoCopy {
  return {
    title: 'Football Club' + BW_SUFFIX_EN,
    description: `Overview of the ${BW_NAME_EN} Football Club section: the First Team, Player Development, Player Opportunities, International Pathways and Player Stories. The club was ${bwFoundedClauseEn(facts)} and competes in ${leagueEn(facts)}.`,
  }
}

export function getClubHubHeroEnBw(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: BW_NAME_EN + ' First Team',
    h1En: null,
    lede: `The club's first team, representing it in ${leagueEn(facts)}; the club was ${bwFoundedClauseEn(facts)}. Here you will find the squad list, the focus areas of player development, ways to join, and real cases of players who have gone abroad.`,
  }
}

/** 統計卡——「5」沿用 `getClubHubStats` 既有字面值（隊史奪冠次數，club-profile.md §4 沿革逐條計數）。 */
export function getClubHubStatsEnBw(facts: SiteFacts): ClubHubStat[] {
  return [
    { num: facts.foundedYear, labelZh: 'Year founded' },
    { num: '5', labelZh: 'League titles won in the club\'s history' },
    { num: String(facts.squadCodes.length), labelZh: 'Youth team age groups' },
    { num: facts.league.nameEn ?? '', labelZh: 'League' },
  ]
}

export const CLUB_HUB_OPPORTUNITIES_DESC_EN_BW =
  'Joining ' + BW_NAME_EN + ', trial dates with online registration, and the recruitment channel for foreign players.'

export const CLUB_HUB_PLAYER_STORIES_DESC_EN_BW =
  'Real cases from the First Team and from players abroad, showing how each player got to where they are.'

export const CLUB_HUB_INTL_DESC_EN_BW =
  'Real cases of players who have gone abroad, regional information for Japan and China, and the overseas club-matching enquiry channel.'

export const CLUB_HUB_INTL_CTA_DESC_EN_BW =
  'Want to learn about cases of players going abroad to Japan and China? The International Pathways page explains the full route.'

export const CLUB_HUB_CTA_TITLE_EN_BW = 'Join the ' + BW_NAME_EN + ' First Team'

/** 底部 CTA 第一張卡「加入球隊」——對應 `getHomeCtaTrio('bw')` 第一筆。 */
export function getClubHubJoinPlayerCardEnBw(facts: SiteFacts): CtaCardCopy {
  return {
    num: '10.1',
    titleZh: 'Join the Squad',
    descZh: `Have the competitive ability and want to prove yourself in ${leagueEn(facts)}? We are continuously recruiting First Team players.`,
    ctaLabelZh: 'Fill in the registration form',
    href: '/zh/join/player/',
  }
}

// ---- 03.1 一線隊 ----

export function getFirstTeamSeoEnBw(facts: SiteFacts): SeoCopy {
  return {
    title: 'First Team' + BW_SUFFIX_EN,
    description: `The ${BW_NAME_EN} First Team: competing in ${leagueEn(facts)}, with five league titles in the club's history. The squad and fixtures are maintained in the back office and the content is being updated.`,
  }
}

export function getFirstTeamHeroEnBw(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'First Team',
    h1En: null,
    lede: `The ${BW_NAME_EN} First Team represents the club in ${leagueEn(facts)}. The club was ${bwFoundedClauseEn(facts)}, and the team has won the league title five times. Its home grounds are ${bwVenuesEn(facts)}.`,
  }
}

export function getFirstTeamIntroEnBw(facts: SiteFacts): string {
  return `The ${BW_NAME_EN} First Team was formed with the club in ${facts.foundedYear} and competes in ${leagueEn(facts)}, having won the league title five times in its history (2017, 2018, 2019, 2021 and 2023). The team's home grounds are ${bwVenuesEn(facts)}. The squad and the latest fixtures are maintained in the back office, and this page is being updated.`
}

/** 一線隊頁底部 CTA 卡 3.2 的英文（藍鯨：球員培育重點）。 */
export const CLUB_FIRST_TEAM_PD_CARD_EN_BW = {
  title: 'Player Development Priorities',
  desc: 'See how the First Team keeps developing players through eight areas.',
  cta: 'View Player Development Priorities',
} as const

// ---- 03.2 球員培育重點／03.3 球員機會／03.4 國際發展通道／03.5 球員故事 ----

export function getPlayerDevelopmentSeoEnBw(): SeoCopy {
  return {
    title: 'Player Development Priorities' + BW_SUFFIX_EN,
    description: `Player development priorities at ${BW_NAME_EN}: technical and tactical analysis, physical fitness training, match reading, mental resilience, video analysis, Individual Development Plans, nutrition and lifestyle, and education and language. The details of each area are being prepared.`,
  }
}

export function getPlayerDevelopmentHeroEnBw(): HeroCopy {
  return {
    h1Zh: 'Player Development Priorities',
    h1En: null,
    lede: `From tactics to education and language, these eight areas are the focus of ${BW_NAME_EN} player development, supporting First Team and Youth team players as they keep growing. Select a card to read more.`,
  }
}

/** 球員培育重點頁其餘藍鯨專屬英文句。 */
export const CLUB_PLAYER_DEVELOPMENT_PAGE_EN_BW = {
  summaryTitle: 'Eight areas, continued growth',
  summaryDesc: 'The eight areas together support the growth of First Team and Youth team players, linking Youth team training with International Pathways to help players keep improving.',
  ctaHeading: 'Join Player Development',
  firstTeamDesc: 'See the First Team squad and season performances.',
  joinDesc: 'Want to be part of the team? Fill in the registration form and start your journey.',
} as const

export function getPlayerOpportunitiesSeoEnBw(): SeoCopy {
  return {
    title: 'Player Opportunities' + BW_SUFFIX_EN,
    description: `Player opportunities at ${BW_NAME_EN}: how to join the First Team, trial dates with online registration, and the recruitment channel for foreign players.`,
  }
}

export function getPlayerOpportunitiesHeroEnBw(): HeroCopy {
  return {
    h1Zh: 'Player Opportunities',
    h1En: null,
    lede: `From joining the First Team and attending trials to the recruitment channel for foreign players, this page brings together every way into ${BW_NAME_EN} player opportunities.`,
  }
}

export const CLUB_JOIN_HEADING_EN_BW = 'Join ' + BW_NAME_EN

export function getJoinFirstTeamBodyEnBw(facts: SiteFacts): string {
  return `The ${BW_NAME_EN} First Team represents the club in ${leagueEn(facts)} and continues to recruit players with the competitive ability to join the squad. After you submit the registration form, the club will contact you about the next steps of the assessment.`
}

export function getForeignPlayerBodyEnBw(facts: SiteFacts): string {
  return `${BW_NAME_EN} First Team competes in ${leagueEn(facts)}. We welcome enquiries from foreign players interested in trialling or joining the squad. Please use the international enquiry form below and our club will follow up.`
}

/** 球員機會頁底部「國際發展通道」卡（藍鯨）。 */
export const CLUB_OPPORTUNITIES_INTL_CARD_DESC_EN_BW =
  'Learn how players have gone abroad to Japan and China through ' + BW_NAME_EN + '.'

export function getInternationalPathwaysSeoEnBw(): SeoCopy {
  return {
    title: 'International Pathways' + BW_SUFFIX_EN,
    description: `${BW_NAME_EN} International Pathways: real cases of players going abroad to Japan and China, and the channels for overseas trials and club-matching enquiries.`,
  }
}

export function getInternationalPathwaysHeroEnBw(): HeroCopy {
  return {
    h1Zh: 'International Pathways',
    h1En: null,
    lede: `Starting from Taichung, ${BW_NAME_EN} players have gone abroad successfully to Japan and China, and the club keeps building routes for players to the international stage.`,
  }
}

/** 旅外日本案例英文敘述（順序對應資料層 `INTL_PATHWAY_JAPAN_NOTES_BW`；名字由資料層取用）。 */
export const INTL_PATHWAY_JAPAN_NOTES_DESC_EN_BW: readonly string[] = [
  'Goalkeeper. Played for FC Fujizakura Yamanashi in Japan from 2019 to 2022 and returned to ' + BW_NAME_EN + ' in 2022.',
  'Goalkeeper. Went abroad to Japan successfully in 2020 (the old website records only the year, not the club played for).',
  'Went abroad to Japan successfully in 2019 (the old website records only the year, not the club played for).',
]

/** 旅外中國案例英文敘述（對應資料層 `INTL_PATHWAY_CHINA_NOTE_BW`）。 */
export const INTL_PATHWAY_CHINA_NOTE_DESC_EN_BW =
  'Went abroad to China successfully in 2023 (the old website records only the year, not the club played for).'

/** 分區頁籤說明（藍鯨：日本／中國）。 */
export const CLUB_INTL_REGION_COPY_EN_BW = {
  selectRegion: 'Select a region',
  japanHasTiles: 'Partner clubs in Japan, and real cases of players going abroad:',
  japanNoTiles: 'There are no formal partner clubs or agreements in Japan that can be made public yet, but players have already gone abroad:',
  chinaHasTiles: 'Partner clubs in China, and real cases of players going abroad:',
  chinaNoTiles: 'There are no formal partner clubs or agreements in China that can be made public yet, but a player has already gone abroad:',
  abroadBadge: 'Abroad',
} as const

/** 球員名顯示：資料層的名字含拉丁字母英文名者（如「蔡明容 Tsai Ming-Jung」）取英文部分，其餘維持原樣（不音譯）。 */
export function clubPlayerNameEn(name: string): string {
  const latin = name.replace(/[^ -~]+/g, ' ').replace(/\s+/g, ' ').trim()
  return latin || name
}

export function getPlayerStoriesSeoEnBw(): SeoCopy {
  return {
    title: 'Player Stories' + BW_SUFFIX_EN,
    description: `Player story cases from ${BW_NAME_EN}, recording each player's real journey from joining ${BW_NAME_EN} to a bigger stage. Cases are being added.`,
  }
}

export function getPlayerStoriesHeroEnBw(): HeroCopy {
  return {
    h1Zh: 'Player Stories',
    h1En: null,
    lede: `Every player has a ${BW_NAME_EN} journey of their own. Verified player stories, with image consent obtained, will be added to this page.`,
  }
}

export const CLUB_PLAYER_STORIES_EMPTY_NOTE_EN_BW =
  'There are no verified player stories with image consent yet. Cases will be added as the squad list and image consents progress.'

export const CLUB_PLAYER_STORIES_INTL_CARD_DESC_EN_BW =
  'Learn about real cases of players going abroad to Japan and China.'

// ---- 08 文化／特約店家／球迷會／商品（藍鯨不設漫畫） ----

export const CLUB_CULTURE_LABEL_EN_BW = BW_NAME_EN + ' Culture'

export function getCultureHubSeoEnBw(): SeoCopy {
  return {
    title: `${CLUB_CULTURE_LABEL_EN_BW} | Fan Club and Merchandise${BW_SUFFIX_EN}`,
    description: `Explore the ${BW_NAME_EN} culture section: the ${BW_NAME_EN} Fan Club, official merchandise with the online store, and partner perks.`,
  }
}

export const CLUB_CULTURE_PAGE_EN_BW = {
  heroLede: `From the Fan Club and official merchandise to partner perks, ${CLUB_CULTURE_LABEL_EN_BW} is the most direct emotional link between ${BW_NAME_EN}, its fans and the community.`,
  merchCardDesc: 'Official merchandise and the online store, with product content provided through the back office.',
  fanClubCardTitle: BW_NAME_EN + ' Fan Club',
} as const

export const CLUB_PERKS_SEO_EN_BW: SeoCopy = {
  title: 'Partner Perks' + BW_SUFFIX_EN,
  description:
    `Partner stores offering discounts to ${BW_NAME_EN} members. Show your digital membership card in store to enjoy the offer; each store states whether it applies to registered members or Paid Fan Club members.`,
}

export const CLUB_PERKS_HERO_LEDE_EN_BW =
  `Local stores working with ${BW_NAME_EN}. Members can enjoy offers by showing their digital membership card in store. This page is public and can be viewed without logging in.`

export function getPerksDetailSeoEnBw(storeName: string | null | undefined, offer: string | null | undefined): SeoCopy {
  const name = storeName || 'Partner Perks'
  return {
    title: `${name} | Partner Perks${BW_SUFFIX_EN}`,
    description: offer || `${storeName ?? ''} is a partner store for ${BW_NAME_EN} members.`,
  }
}

export const CLUB_FAN_CLUB_SEO_EN_BW: SeoCopy = {
  title: `Fan Club | ${CLUB_CULTURE_LABEL_EN_BW} | ${BW_NAME_EN}`,
  description:
    `Join the ${BW_NAME_EN} Fan Club: membership plans, a tier-by-tier comparison of member benefits, and fan event registration and reviews.`,
}

export const CLUB_FAN_CLUB_HERO_LEDE_EN_BW =
  `Cheer with ${BW_NAME_EN} from the touchline. The Fan Club is the club's paid membership: besides a jersey, you get more discounts at partner stores and priority access to fan events.`

export function getFanEventSeoEnBw(name: string, when: string, location: string | null | undefined): SeoCopy {
  return {
    title: `${name} | Fan Club Events${BW_SUFFIX_EN}`,
    description: `${BW_NAME_EN} Fan Club event "${name}"${when ? `: ${when}` : ''}${location ? `, ${location}` : ''}.`,
  }
}

export function getMerchandiseSeoEnBw(hasProducts: boolean): SeoCopy {
  return {
    title: `Merchandise | ${CLUB_CULTURE_LABEL_EN_BW} | ${BW_NAME_EN}`,
    description: hasProducts
      ? `${BW_NAME_EN} official merchandise: browse by collection, then choose a size and colour in the official store, pay with LINE Pay and receive an e-invoice.`
      : `${BW_NAME_EN} official merchandise. Product content is provided through the back office and there is nothing to display yet.`,
  }
}
