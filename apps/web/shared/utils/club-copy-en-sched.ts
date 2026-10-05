// shared/utils/club-copy-en-sched.ts — 主站（tcrfc）英文版文案：賽事行事曆（13）與新聞中心（07）
//
// 約定（英文版文案檔共通，群組代號 sched）：
// - 只提供 tcrfc 的英文值；藍鯨站英文版不在範圍（isEn 在藍鯨一律 false）。
// - 原函式 `getFoo(club, facts)` → `getFooEn(...)`；型別沿用 club-copy.ts 的 interface，
//   **欄位名稱不變但值是英文**（例如 `HeroCopy.h1Zh` 欄位放英文 H1，`h1En` 一律 null）。
// - 檔內字串不得含中文字元（scripts 有檢查）；英文用詞一律照 docs/06-conventions.md §1.1 對照表。
// - 事實（聯賽名稱、賽季）一律由呼叫端傳入，不在此寫死字面值（check-fact-single-source.mjs）。
// - 頁面端的短字串與屬性用 `tx(zh, en)`；本檔只放「SEO、Hero、分類文案」等較長或多頁共用的英文。

import type { HeroCopy, SeoCopy } from './club-copy'
import { CLUB_NAME_EN } from './club-copy-en-core'

// ---------------------------------------------------------------------------
// 13 賽事行事曆
// ---------------------------------------------------------------------------

export interface ScheduleSeoArgsEn {
  /** `facts.league.nameEn`；沒有英文正式名稱時傳 null（不得自創全名），文案改用 "the league"。 */
  leagueNameEn: string | null
  /** 預設賽季代碼（來自賽程回應的 `seasonCode`）；沒有時傳 null。 */
  seasonCode: string | null
  /** 賽程總場數 */
  matchCount: number
  /** 梯隊代碼組字（例如 "U15 / U14 / U12"），由呼叫端以 `academyLabel(' / ')` 取得 */
  squadLabel: string
}

export function getScheduleSeoEn(args: ScheduleSeoArgsEn): SeoCopy {
  const season = args.seasonCode ? `${args.seasonCode} ` : ''
  const league = args.leagueNameEn ?? 'the league'
  return {
    title: `Schedule | ${CLUB_NAME_EN}`,
    description: `Full fixtures and results for ${CLUB_NAME_EN}: ${args.matchCount} ${season}fixtures in ${league}, organised by team (First Team / Academy ${args.squadLabel}), with fixtures and results views, a calendar view and add-to-calendar for each match.`,
  }
}

// ---------------------------------------------------------------------------
// 07 新聞中心
// ---------------------------------------------------------------------------

export function getNewsIndexSeoEn(totalCount: number): SeoCopy {
  return {
    title: `News & Stories | ${CLUB_NAME_EN}`,
    description: `The ${CLUB_NAME_EN} news centre: club news, match reports, international exchange, camps and events, and community work. Browse ${totalCount} reports by category, month and keyword.`,
  }
}

export function getNewsIndexHeroEn(totalCount: number): HeroCopy {
  return {
    h1Zh: 'News & Stories',
    h1En: null,
    lede: `Club announcements, match reports, international exchange, camps and events, and community work are all published in the news centre. ${totalCount} reports so far.`,
  }
}

export interface NewsCategoryCopyEn {
  /** 規劃書編號（與 zh 版分類 Tab 一致，例 "7.1"） */
  num: string
  /** 分類名稱（英文） */
  label: string
  /** Hero 導言 */
  lede: string
  /** SEO 描述的主體；`count` 為 null 時不帶篇數 */
  description: (count: number | null) => string
}

function countSuffix(count: number | null): string {
  return count === null ? '' : ` ${count} ${count === 1 ? 'report' : 'reports'} in total.`
}

export const NEWS_CATEGORY_EN: Record<string, NewsCategoryCopyEn> = {
  club: {
    num: '7.1',
    label: 'Club News',
    lede: 'Team announcements, squad changes, certifications and honours, and partnerships: first-hand news from the club itself.',
    description: (c) => `${CLUB_NAME_EN} club news: squad changes, certification milestones, honours and partnerships.${countSuffix(c)}`,
  },
  match: {
    num: '7.2',
    label: 'Match Reports',
    lede: 'League, second-division, President\'s Cup and friendly matches: post-match reports for every fixture played by the First Team and the reserves.',
    description: (c) => `${CLUB_NAME_EN} match reports for every team, covering league, second-division, President's Cup and friendly fixtures.${countSuffix(c)}`,
  },
  academy: {
    num: '7.3',
    label: 'Academy News',
    lede: 'Training updates and growth stories from every Academy squad. More content is on the way.',
    description: () => `News and updates from the ${CLUB_NAME_EN} Academy squads. Content is still being prepared.`,
  },
  'player-stories': {
    num: '7.4',
    label: 'Player Stories',
    lede: 'Interviews about players\' growth and personal journeys. More content is on the way.',
    description: () => `Player stories and interviews from ${CLUB_NAME_EN}. Content is still being prepared.`,
  },
  international: {
    num: '7.5',
    label: 'International',
    lede: 'Partnerships with overseas clubs, players training abroad and transfers: a first-hand record of Taichung Rock FC players looking to the world, in line with Global Pathways.',
    description: (c) => `${CLUB_NAME_EN} international news: partnership agreements with overseas clubs, players training abroad and transfers.${countSuffix(c)}`,
  },
  'camps-events': {
    num: '7.6',
    label: 'Camps & Events',
    lede: 'Invitational tournaments, the international football cup and other events: highlights from the tournaments and events Taichung Rock FC hosts and takes part in.',
    description: (c) => `Highlights and records from ${CLUB_NAME_EN} camps, invitational tournaments and the international football cup.${countSuffix(c)}`,
  },
  community: {
    num: '7.7',
    label: 'Community',
    lede: 'Charity donations, school exchanges and partnerships with local government: Taichung Rock FC\'s community work beyond the pitch.',
    description: (c) => `Records of ${CLUB_NAME_EN} charity and local partnership activities, reflecting the Community core value.${countSuffix(c)}`,
  },
  media: {
    num: '7.8',
    label: 'Press & Media',
    lede: 'Press releases, the brand kit (logo / CIS), a high-resolution image library and media contact details for our media partners.',
    description: () => `${CLUB_NAME_EN} press and media: brand kit downloads (crest in SVG and PNG, social share image). Press releases, the high-resolution image library and media contact are being set up.`,
  },
}

/** 分類 Tab 顯示字，例 "7.1 Club News"（與 zh 版 "7.1 俱樂部新聞" 同一編號格式）。 */
export function newsCategoryTabLabelEn(code: string): string {
  const c = NEWS_CATEGORY_EN[code]
  return c ? `${c.num} ${c.label}` : ''
}

export function getNewsCategorySeoEn(code: string, count: number | null): SeoCopy {
  const c = NEWS_CATEGORY_EN[code]
  const label = c?.label ?? 'News'
  return {
    title: `${label} | News | ${CLUB_NAME_EN}`,
    description: c ? c.description(count) : '',
  }
}

export function getNewsCategoryHeroEn(code: string): HeroCopy {
  const c = NEWS_CATEGORY_EN[code]
  return { h1Zh: c?.label ?? 'News', h1En: null, lede: c?.lede ?? '' }
}

/** 媒體專區檔案類型標籤：`PDF`／`ZIP`／`Image (WEBP)`；認不得的副檔名回傳 null（與 zh 版同規則）。 */
export function fileKindLabelEn(ext: string | null): string | null {
  const e = (ext ?? '').replace('.', '').toLowerCase()
  if (!e) return null
  if (e === 'pdf' || e === 'zip') return e.toUpperCase()
  if (['webp', 'jpg', 'jpeg', 'png'].includes(e)) return `Image (${e.toUpperCase()})`
  return null
}
