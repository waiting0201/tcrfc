// shared/utils/club-copy-en-acad.ts - Taichung Rock FC (tcrfc) English copy: group "acad"
// (04 Academy pages and 05 Programs pages).
//
// Conventions (shared by all club-copy-en-* files):
// - tcrfc English is the default. Blue Whale (bw) English was added on 2026-10-05 (B-5): every function that
//   differs by club takes an OPTIONAL trailing `club: 'tcrfc' | 'bw' = 'tcrfc'` argument (existing call
//   signatures and default behaviour are unchanged). Blue Whale-only constants use the `_BW` suffix.
//   Blue Whale uses "Youth" / "Youth Teams" for unit 04 (never "Academy") and the short name BW_NAME_EN.
//   Blue Whale English is a translation of the bw zh branches of club-copy.ts only; no new facts.
//   Proper names without an English source are described conservatively and are listed in the hand-over
//   report as "to be confirmed by the client".
// - Original constant `FOO` -> `FOO_EN`; original function `getFoo(club, facts)` -> `getFooEn(facts)`
//   (functions that took only `club` become `getFooEn()`).
// - Types are reused from club-copy.ts. Field names are unchanged but the values are English
//   (for example `h1Zh` holds the English H1 and `h1En` is null, `titleZh` holds the English title).
// - No Chinese characters may appear in this file (a script checks it). Terms follow the
//   docs/06-conventions.md section 1.1 English glossary.
// - Facts (founding year, squad codes) always come from the `facts` argument supplied by the caller.

import type { HeroCopy, SeoCopy, AcademyTeamTab, AcademyHubCard, ProgramsHubCard, EnrolFlowStep, CtaCardCopy, ChildrensClassRow, SchoolPartnerRow } from './club-copy'
import type { SiteFacts } from './site-facts'
import { BW_NAME_EN } from './club-copy'

const SITE = 'Taichung Rock FC'
const BW = BW_NAME_EN

export type AcadClub = 'tcrfc' | 'bw'

/** "U15 and U12" / "U15, U14 and U12" (codes come from facts, never hard-coded). */
function squadCodesAnd(facts: SiteFacts): string {
  const c = facts.squadCodes
  if (c.length <= 1) return c.join('')
  return `${c.slice(0, -1).join(', ')} and ${c[c.length - 1]}`
}

/** Unit 04 label in breadcrumbs (English): tcrfc "TCRFC Academy", Blue Whale "Youth Teams". */
export function getAcademyUnitLabelEn(club: AcadClub = 'tcrfc'): string {
  return club === 'bw' ? 'Youth Teams' : 'TCRFC Academy'
}

function squadCodes(facts: SiteFacts, separator = ', '): string {
  return facts.squadCodes.join(separator)
}

// ---------------------------------------------------------------------------
// 04 Academy hub
// ---------------------------------------------------------------------------

export function getAcademyHubSeoEn(facts: SiteFacts, club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Youth Teams | ${BW}`,
      description: `The ${BW} Youth Teams are made up of ${squadCodesAnd(facts)} girls' football teams. They link to the First Team's competitive system, with age-group training and the support of a coaching team.`,
    }
  }
  return {
    title: `TCRFC Academy | ${SITE}`,
    description: `TCRFC Academy is the youth development system of ${SITE}. It offers age-group training (${squadCodes(facts)}), a clear development pathway and the support of a coaching team, linking players to the First Team and to opportunities overseas.`,
  }
}

export function getAcademyHubHeroEn(club: AcadClub = 'tcrfc', facts?: SiteFacts): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Youth Teams',
      h1En: null,
      lede: `The ${BW} Youth Teams are made up of ${facts ? squadCodesAnd(facts) : 'age-group'} girls' football teams, the squads that link to the First Team's competitive system. Through age-group training and the long-term support of the coaching team, we help players grow step by step.`,
    }
  }
  return {
    h1Zh: 'TCRFC Academy',
    h1En: null,
    lede:
      'TCRFC Academy carries forward the club vision of LOCAL ROOTS. GLOBAL PATHWAYS. Through age-group training, a clear development pathway ' +
      'and the long-term support of our coaches, we help players grow from core technique towards the First Team or opportunities overseas.',
  }
}

export function getAcademyHubCardsEn(facts: SiteFacts, club: AcadClub = 'tcrfc'): AcademyHubCard[] {
  if (club === 'bw') {
    const bwCard = (num: string, title: string, desc: string, href: string): AcademyHubCard => ({ num, titleZh: title, titleEn: title, descZh: desc, href })
    return [
      bwCard('4.1', 'Youth Teams Overview', 'The positioning and the big picture of the Youth Teams', '/zh/academy/overview/'),
      bwCard('4.2', 'Our Teams', `Rosters, coaches and fixtures for the ${squadCodes(facts, ' / ')} squads`, '/zh/academy/teams/'),
      bwCard('4.3', 'Youth Pathway', 'The pathway from U12 to U15 and the First Team', '/zh/academy/pathway/'),
      bwCard('4.4', 'Training & Curriculum', 'Five pillars: technical, tactical, physical, game reading and character', '/zh/academy/curriculum/'),
      bwCard('4.5', 'Youth Coaches', 'Meet the coaches who lead each squad', '/zh/academy/coaches/'),
      bwCard('4.6', 'Youth Life', 'A day-to-day record of training, matches and activities', '/zh/academy/life/'),
    ]
  }
  const card = (num: string, title: string, desc: string, href: string): AcademyHubCard => ({
    num,
    titleZh: title,
    titleEn: title,
    descZh: desc,
    href,
  })
  return [
    card('4.1', 'Academy Overview', 'The Academy\'s positioning, training base and the big picture', '/zh/academy/overview/'),
    card('4.2', 'Our Teams', `Rosters, coaches and fixtures for the ${squadCodes(facts, ' / ')} squads`, '/zh/academy/teams/'),
    card('4.3', 'Academy Pathway', 'The pathway from U12 to the First Team and overseas', '/zh/academy/pathway/'),
    card('4.4', 'Training & Curriculum', 'Five pillars: technical, tactical, physical, game reading and character', '/zh/academy/curriculum/'),
    card('4.5', 'Coaches', 'Meet the coaches who lead each squad', '/zh/academy/coaches/'),
    card('4.6', 'Academy Life', 'A day-to-day record of training, matches and activities', '/zh/academy/life/'),
    card('4.7', 'Join the Academy', 'Who we recruit, the selection process and online application', '/zh/academy/join/'),
  ]
}

export const ACADEMY_HUB_CTA_TITLE_EN = 'Ready to join TCRFC Academy?'
/** Blue Whale: no admissions button (4.7 is closed), title only. */
export const ACADEMY_HUB_CTA_TITLE_EN_BW = `Ready to join the ${BW} Youth Teams?`

// ---------------------------------------------------------------------------
// 4.1 Overview
// ---------------------------------------------------------------------------

export function getAcademyOverviewSeoEn(club: AcadClub = 'tcrfc', facts?: SiteFacts): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Youth Teams Overview | ${BW}`,
      description: `The ${BW} Youth Teams are made up of ${facts ? squadCodesAnd(facts) : 'age-group'} girls' football teams and link to the First Team's competitive system. Admissions and timetables are still to be confirmed; please contact the club for details.`,
    }
  }
  return {
    title: `Academy Overview | TCRFC Academy | ${SITE}`,
    description:
      'Get to know the positioning and training base of TCRFC Academy: a youth development system that carries forward the club\'s brand vision, ' +
      'plus highlights such as player numbers, coach numbers and school-progression rates (data being collected).',
  }
}

export function getAcademyOverviewHeroEn(club: AcadClub = 'tcrfc', facts?: SiteFacts): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Youth Teams Overview',
      h1En: null,
      lede: `The ${BW} Youth Teams are made up of ${facts ? squadCodesAnd(facts) : 'age-group'} girls' football teams, the squads that link to the First Team's competitive system.`,
    }
  }
  return {
    h1Zh: 'Academy Overview',
    h1En: null,
    lede:
      'TCRFC Academy is the youth development system of Taichung Rock FC. It carries forward the club vision of LOCAL ROOTS. GLOBAL PATHWAYS. ' +
      'and the five core values of Players First, Excellence, Global Pathways, Community and Integrity, supporting players\' growth through age-group training.',
  }
}

export function getAcademyPositioningEn(facts: SiteFacts, club: AcadClub = 'tcrfc'): string {
  if (club === 'bw') {
    return `${BW} is Taiwan's leading women's football club, and over the years the team has produced as many as 21 players for the Chinese Taipei women's national team. The Youth Teams hope to build on that record and widen the football future open to their players (excerpted from the Youth Teams recruitment text on the official ${BW} website).`
  }
  return `Since the club was founded in ${facts.foundedYear}, TCRFC Academy has served as a bridge between community football and the competitive system. Our aim is for every player to develop technique, tactical understanding and character at their own pace in a solid training environment, and, for players ready to step up to the First Team or an overseas stage, to offer a clear pathway for growth.`
}

// ---------------------------------------------------------------------------
// 4.2 Our Teams
// ---------------------------------------------------------------------------

export function getAcademyTeamsSeoEn(facts: SiteFacts, club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Youth Teams ${squadCodes(facts)} | ${BW}`,
      description: `${BW} Youth Teams ${squadCodes(facts)} girls' football teams: rosters, coaches and fixtures by squad (data being collected).`,
    }
  }
  return {
    title: `Our Teams | TCRFC Academy | ${SITE}`,
    description: `TCRFC Academy squads (${squadCodes(facts)} and other age groups): rosters, coaches, fixtures and results by squad (data being collected), with a subscription to each squad's calendar.`,
  }
}

export function getAcademyTeamsHeroEn(facts: SiteFacts, club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Youth Teams',
      h1En: null,
      lede: `The ${BW} Youth Teams are organised by age into ${squadCodesAnd(facts)} girls' football teams. Rosters, coaches and fixtures for each squad are shown below.`,
    }
  }
  return {
    h1Zh: 'Our Teams',
    h1En: null,
    lede: `TCRFC Academy is organised by age into ${squadCodes(facts)} and other age-group squads. Each squad has its own roster, coaches, fixtures and results, and you can subscribe to a squad's calendar to keep up with every training session and match.`,
  }
}

/** English tab labels. `id` and `teamCode` are identical to getAcademyTeamTabs(); only the label of the "other" tab is English. */
export function getAcademyTeamTabsEn(facts: SiteFacts, club: AcadClub = 'tcrfc'): AcademyTeamTab[] {
  if (club === 'bw') {
    return facts.squadCodes.map((code) => ({ id: code.toLowerCase(), labelZh: code, teamCode: `BW-${code}` }))
  }
  return [
    ...facts.squadCodes.map((code) => ({ id: code.toLowerCase(), labelZh: code, teamCode: code })),
    { id: 'other', labelZh: 'Other age groups', teamCode: null },
  ]
}

// ---------------------------------------------------------------------------
// 4.3 Pathway / 4.4 Curriculum / 4.5 Coaches / 4.6 Life
// ---------------------------------------------------------------------------

export function getAcademyPathwaySeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Youth Pathway | ${BW}`,
      description: `The step-by-step development pathway of the ${BW} Youth Teams: U12 to U15 to the First Team. Select each stage to see what it takes to move up (details being collected).`,
    }
  }
  return {
    title: `Academy Pathway | TCRFC Academy | ${SITE}`,
    description: 'The step-by-step development pathway at TCRFC Academy: U12 to U15 to the First Team or overseas. Select each stage to see what it takes to move up (details being collected).',
  }
}

export function getAcademyPathwayHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Youth Pathway',
      h1En: null,
      lede: `Start at U12, deepen your game at U15, then move up to the First Team: ${BW} offers a step-by-step development pathway. Select a stage below to see how players progress to the next level.`,
    }
  }
  return {
    h1Zh: 'Academy Pathway',
    h1En: null,
    lede: 'Start at U12, deepen your game at U15, then step up to the First Team or an overseas stage. TCRFC Academy offers a step-by-step development pathway. Select a stage below to see how players progress to the next level.',
  }
}

export function getAcademyCurriculumSeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Training & Curriculum | ${BW}`,
      description: `Training at the ${BW} Youth Teams covers five pillars: technical, tactical, physical, game reading and character. The syllabus and training-cycle plan for each pillar are being collected.`,
    }
  }
  return {
    title: `Training & Curriculum | TCRFC Academy | ${SITE}`,
    description: 'Training at TCRFC Academy covers five pillars: technical, tactical, physical, game reading and character. The syllabus and training-cycle plan for each pillar are being collected.',
  }
}

export function getAcademyCurriculumHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Training & Curriculum',
      h1En: null,
      lede: `Technical, tactical, physical, game reading and character: training at the ${BW} Youth Teams is built around five pillars, each with its own syllabus and training-cycle plan.`,
    }
  }
  return {
    h1Zh: 'Training & Curriculum',
    h1En: null,
    lede: 'Technical, tactical, physical, game reading and character: training at TCRFC Academy is built around five pillars, each with its own syllabus and training-cycle plan.',
  }
}

export function getYouthCoachesSeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Youth Coaches | ${BW}`,
      description: `The coaching list of the ${BW} Youth Teams is being compiled and will be published on this page once verified information is ready.`,
    }
  }
  return {
    title: `Coaches | TCRFC Academy | ${SITE}`,
    description: 'Meet the TCRFC Academy coaching team: the Director of Youth Development and two youth coaches. Licences, specialisms and squad responsibilities are being collected.',
  }
}

export function getYouthCoachesHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Youth Coaches',
      h1En: null,
      lede: `The coaching list of the ${BW} Youth Teams is being compiled and will be published on this page once verified, up-to-date information is ready.`,
    }
  }
  return {
    h1Zh: 'Academy Coaches',
    h1En: null,
    lede: 'The TCRFC Academy coaching team consists of one Director of Youth Development and two youth coaches, who accompany players in every squad from core technique to reading the game.',
  }
}

export function getYouthLifeSeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Youth Life | ${BW}`,
      description: `Everyday training and match images of the ${BW} Youth Teams will be published on this page progressively once image consent is in place.`,
    }
  }
  return {
    title: `Academy Life | TCRFC Academy | ${SITE}`,
    description: 'Everyday training, matches and activities at TCRFC Academy, with real photos that record players\' growth.',
  }
}

export function getYouthLifeHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Youth Life',
      h1En: null,
      lede: `Everyday training, match days and team activities: once image consent is in place, we will progressively record the growth of ${BW} Youth Team players in photos and video.`,
    }
  }
  return {
    h1Zh: 'Academy Life',
    h1En: null,
    lede: 'Everyday training, match days and team activities: photos that capture every run and every breakthrough by our Academy players.',
  }
}

/** 4.7 Join the Academy has inline copy on the page; its SEO is provided here for symmetry. */
export function getAcademyJoinSeoEn(): SeoCopy {
  return {
    title: `Join the Academy | TCRFC Academy | ${SITE}`,
    description: 'Who TCRFC Academy recruits, how selection works and trial information. Details such as fees and trial dates are still being collected; get in touch through our online application.',
  }
}

// ---------------------------------------------------------------------------
// 05 Programs hub
// ---------------------------------------------------------------------------

export function getProgramsHubSeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Programs | ${BW}`,
      description: `An overview of ${BW} programs: the community football school, Hot Zone classes, specialist training and school and community partnerships. In-person individual registration, with no trial class or assessment required and no membership fee.`,
    }
  }
  return {
    title: `Programs | ${SITE}`,
    description:
      'An overview of Taichung Rock FC programs: Children\'s Training, Summer Camp, Winter Camp, Specialist Training and School & Community projects. ' +
      'Every session lists its venue, schedule, places and fee, with online registration.',
  }
}

export function getProgramsHubHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Programs',
      h1En: null,
      lede: `From the community football school and Hot Zone classes to school and community partnerships, ${BW} meets more people through the ball with its programs. Register in person on site, with no trial class or assessment required and no membership fee.`,
    }
  }
  return {
    h1Zh: 'Programs',
    h1En: null,
    lede:
      'From Children\'s Training, Summer and Winter Camps and Specialist Training to School & Community projects, Taichung Rock FC runs every way of meeting the ball as a clearly defined program, ' +
      'with the venue, schedule, places and fee of each session shown up front.',
  }
}

export function getProgramsHubIntroEn(club: AcadClub = 'tcrfc'): string {
  if (club === 'bw') {
    return `${BW} programs are mainly registered in person on site, with no trial class, no assessment and no membership fee. The "Little Blue Whale" community football school runs the Taichung City Government's "Sport i Taiwan 2.0" Sports Hot Zone program, and we also offer a Goalkeeper Foundation Class and school and community partnerships. The five areas below serve different ages and needs. Select a card to see the details.`
  }
  return 'Programs at Taichung Rock FC are managed by session: every program has a clear level or category, training venue, schedule, capacity and fee, and can be booked online. The five areas below serve players of different ages and needs. Select a card to see the details.'
}

export function getProgramsHubCardsEn(club: AcadClub = 'tcrfc'): ProgramsHubCard[] {
  if (club === 'bw') {
    return [
      { num: '5.1', titleZh: 'Children\'s Training', titleEn: 'Children\'s Training', descZh: 'The "Little Blue Whale" community football school and Sport i Taiwan 2.0 classes for ages 3 to 15, with in-person individual registration.', href: '/zh/programs/childrens-training/', hasPhoto: false },
      { num: '5.2', titleZh: 'Summer Camp', titleEn: 'Summer Camp', descZh: 'No stand-alone Summer Camp has been launched yet. Please follow our official social channels for summer activities.', href: '/zh/programs/summer-camp/', hasPhoto: false },
      { num: '5.3', titleZh: 'Winter Camp', titleEn: 'Winter Camp', descZh: 'No stand-alone Winter Camp has been launched yet. Please follow our official social channels for winter holiday activities.', href: '/zh/programs/winter-camp/', hasPhoto: false },
      { num: '5.4', titleZh: 'Specialist Training', titleEn: 'Specialist Training', descZh: 'Currently the Goalkeeper Foundation Class (ages 7 to 12); a registration form must be completed first. Other specialist programs have not been launched yet.', href: '/zh/programs/specialist/', hasPhoto: false },
      { num: '5.5', titleZh: 'School & Community', titleEn: 'School & Community', descZh: 'A list of industry-academia partner schools, community football outreach and coach education. Schools and community organisations are welcome to get in touch.', href: '/zh/programs/school-community/', hasPhoto: false },
    ]
  }
  return [
    { num: '5.1', titleZh: 'Children\'s Training', titleEn: 'Children\'s Training', descZh: 'Levels (mixed-age, beginner, skill development), a training venue map, a weekly timetable and online registration.', href: '/zh/programs/childrens-training/', hasPhoto: true },
    { num: '5.2', titleZh: 'Summer Camp', titleEn: 'Summer Camp', descZh: 'Who it is for, what we cover, the coaching team, partners, dates, venue and registration details.', href: '/zh/programs/summer-camp/', hasPhoto: true },
    { num: '5.3', titleZh: 'Winter Camp', titleEn: 'Winter Camp', descZh: 'Shares its layout and data with Summer Camp. Session details will be announced before registration opens.', href: '/zh/programs/winter-camp/', hasPhoto: false },
    { num: '5.4', titleZh: 'Specialist Training', titleEn: 'Specialist Training', descZh: 'Six specialist programs: goalkeeper, forward, defender, midfield, fitness and speed, and advanced training.', href: '/zh/programs/specialist/', hasPhoto: true },
    { num: '5.5', titleZh: 'School & Community', titleEn: 'School & Community', descZh: 'School programs, community projects, coach education, a list of partner schools and an enquiry form.', href: '/zh/programs/school-community/', hasPhoto: false },
  ]
}

export const ENROL_FLOW_STEPS_TCRFC_EN: EnrolFlowStep[] = [
  { titleZh: 'Choose a program', descZh: 'Pick a program from 5.1 to 5.5 that suits the age and needs' },
  { titleZh: 'Choose a session', descZh: 'Select an open session by schedule, venue and dates' },
  { titleZh: 'Player details', descZh: 'Enter the player\'s basic details; several players can be registered in turn' },
  { titleZh: 'Parent / emergency contact', descZh: 'Leave the details of a parent or emergency contact' },
  { titleZh: 'Health declaration', descZh: 'Confirm health status and agree to the related terms' },
  { titleZh: 'Submit', descZh: 'You receive a registration number; payment details are provided by the club depending on the session' },
]

/** Blue Whale replaces the six-step online registration block with this plain note (in-person registration). */
export function getProgramsHubEnrolNoteEnBw(): string {
  return `${BW} programs are registered in person on site, with no trial class, no assessment and no membership fee. The Goalkeeper Foundation Class requires an online registration form to be completed first. See each program page for registration details, or contact the official ${BW} LINE account.`
}

export function getProgramsHubCtaCardsEn(club: AcadClub = 'tcrfc'): CtaCardCopy[] {
  if (club === 'bw') {
    return [
      { num: 'Children & youth', titleZh: 'Children\'s Training', descZh: 'The "Little Blue Whale" community football school, with in-person individual registration.', ctaLabelZh: 'Learn more', href: '/zh/programs/childrens-training/' },
      { num: 'Schools & organisations', titleZh: 'School & Community Partnerships', descZh: 'Talk to us about school programs, community projects and coach education.', ctaLabelZh: 'Get in touch', href: '/zh/programs/school-community/' },
      { num: 'Other questions', titleZh: `Contact ${BW}`, descZh: 'Questions about any program are welcome.', ctaLabelZh: 'Contact us', href: '/zh/join/general/' },
    ]
  }
  return [
    { num: 'Children & youth', titleZh: 'Children\'s Training', descZh: 'Grouped by age and level, from mixed-age introduction to skill development.', ctaLabelZh: 'Learn more', href: '/zh/programs/childrens-training/' },
    { num: 'Schools & organisations', titleZh: 'School & Community Partnerships', descZh: 'Talk to us about school programs, community projects and coach education.', ctaLabelZh: 'Get in touch', href: '/zh/programs/school-community/' },
    { num: 'Other questions', titleZh: 'Contact Taichung Rock FC', descZh: 'Questions about any program are welcome.', ctaLabelZh: 'Contact us', href: '/zh/join/general/' },
  ]
}

// ---------------------------------------------------------------------------
// 5.1 - 5.5
// ---------------------------------------------------------------------------

export function getChildrensTrainingSeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Children's Training | Programs | ${BW}`,
      description: `${BW} Children's Training: the "Little Blue Whale" community football school and Sport i Taiwan 2.0 classes for ages 3 to 15, with in-person individual registration at Taichung Beitun Taiyuan Football Field.`,
    }
  }
  return {
    title: `Children's Training | Programs | ${SITE}`,
    description: 'Taichung Rock FC Children\'s Training is organised into mixed-age introduction, beginner and skill-development levels, held at the club\'s home ground and other venues, with a weekly timetable and online registration.',
  }
}

export function getChildrensTrainingHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Children\'s Training',
      h1En: null,
      lede: `${BW} runs the Taichung City Government's "Sport i Taiwan 2.0" Sports Hot Zone program. The "Little Blue Whale" community football school and a range of age-group classes are held at Taichung Beitun Taiyuan Football Field, with no trial class or assessment required, no membership fee and in-person individual registration.`,
    }
  }
  return {
    h1Zh: 'Children\'s Training',
    h1En: null,
    lede: 'From the first touch of a ball to building real skills, Taichung Rock FC plans its programs by age and ability so every child can grow at the right pace.',
  }
}

export function getSummerCampSeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Summer Camp | Programs | ${BW}`,
      description: `${BW} has not yet launched a stand-alone Summer Camp. For summer activities, please follow our official social channels and the Programs page.`,
    }
  }
  return {
    title: `Summer Camp | Programs | ${SITE}`,
    description: 'Taichung Rock FC Summer Camp offers intensive football training and activities. Session dates, venue and fees will be announced when registration opens. Online registration does not take payment.',
  }
}

export function getSummerCampHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Summer Camp',
      h1En: null,
      lede: `${BW} has not yet launched a stand-alone Summer Camp, and whether to offer one is still being planned. During the summer holiday, please keep an eye on the existing classes under Children's Training and announcements on our official social channels.`,
    }
  }
  return {
    h1Zh: 'Summer Camp',
    h1En: null,
    lede: 'Spend the summer holiday immersed in football, building ball feel, fitness and teamwork under professional coaches.',
  }
}

export function getWinterCampSeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Winter Camp | Programs | ${BW}`,
      description: `${BW} has not yet launched a stand-alone Winter Camp. For winter holiday activities, please follow our official social channels and the Programs page.`,
    }
  }
  return {
    title: `Winter Camp | Programs | ${SITE}`,
    description: 'Taichung Rock FC Winter Camp shares its layout and data with Summer Camp. Session dates, venue, fees and coaching team will be announced when registration opens.',
  }
}

export function getWinterCampHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Winter Camp',
      h1En: null,
      lede: `${BW} has not yet launched a stand-alone Winter Camp, and whether to offer one is still being planned. During the winter holiday, please keep an eye on the existing classes under Children's Training and announcements on our official social channels.`,
    }
  }
  return {
    h1Zh: 'Winter Camp',
    h1En: null,
    lede: 'An intensive football camp during the winter holiday, with the same layout and data as Summer Camp. Session dates and fees will be announced before registration opens.',
  }
}

export function getSpecialistTrainingSeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `Specialist Training | Programs | ${BW}`,
      description: `${BW} Specialist Training currently offers the Goalkeeper Foundation Class (ages 7 to 12). Other specialist programs have not been launched yet, and a registration form must be completed first.`,
    }
  }
  return {
    title: `Specialist Training | Programs | ${SITE}`,
    description: 'Taichung Rock FC Specialist Training covers six categories: goalkeeper, forward, defender, midfield, fitness and speed, and advanced training. Planned and delivered by the club\'s coaching team, with online registration.',
  }
}

export function getSpecialistTrainingHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'Specialist Training',
      h1En: null,
      lede: `${BW} currently offers a Goalkeeper Foundation Class, a specialist program designed for the goalkeeping position. Other specialist training has not been launched yet; any future opening will be announced on this page.`,
    }
  }
  return {
    h1Zh: 'Specialist Training',
    h1En: null,
    lede: 'Focused training built around specific positions and abilities, with objectives and target players set by the Taichung Rock FC coaching team according to player needs.',
  }
}

export function getSchoolCommunitySeoEn(club: AcadClub = 'tcrfc'): SeoCopy {
  if (club === 'bw') {
    return {
      title: `School & Community | Programs | ${BW}`,
      description: `${BW} school partnerships and community outreach: the list of industry-academia partner schools, the Sport i Taiwan community football program and coach education. Schools and community organisations are welcome to get in touch.`,
    }
  }
  return {
    title: `School & Community | Programs | ${SITE}`,
    description: 'Taichung Rock FC school programs, community projects and coach education. Schools and community organisations are welcome to get in touch through the form and our team will follow up.',
  }
}

export function getSchoolCommunityHeroEn(club: AcadClub = 'tcrfc'): HeroCopy {
  if (club === 'bw') {
    return {
      h1Zh: 'School & Community',
      h1En: null,
      lede: `${BW} works with schools and community organisations to promote women's football, offering industry-academia cooperation, community football outreach and coach education, and helping to develop local coaches and grassroots football participation.`,
    }
  }
  return {
    h1Zh: 'School & Community',
    h1En: null,
    lede: 'Taichung Rock FC works with schools and community organisations to promote football, offering school programs, community projects and coach education, and helping to develop local coaches.',
  }
}

// ---------------------------------------------------------------------------
// Blue Whale (bw) page data, English (translations of the bw data tables in club-copy.ts;
// same row order as the zh arrays, so a page can index them in parallel).
// Organisation / class names have no official English source: they are conservative descriptions
// and are listed as "to be confirmed by the client" in the hand-over report.
// ---------------------------------------------------------------------------

/** 5.1 class list (zh twin: CHILDRENS_TRAINING_CLASSES_BW; fields hold English values). */
export const CHILDRENS_TRAINING_CLASSES_BW_EN: ChildrensClassRow[] = [
  { nameZh: 'Community toddler football class', ageZh: 'Ages 3-4', feeZh: 'NT$ 200 / session' },
  { nameZh: 'Young children\'s community football class', ageZh: 'Ages 6-8', feeZh: 'NT$ 200 / session' },
  { nameZh: 'Blue Whale U8/U10 football class', ageZh: 'Ages 9-10', feeZh: 'NT$ 200 / session' },
  { nameZh: 'Blue Whale U12 girls\' football class', ageZh: 'Ages 10-12 (girls only)', feeZh: 'NT$ 200 / session' },
  { nameZh: 'Blue Whale U15 girls\' football class', ageZh: 'Ages 13 and over (girls only)', feeZh: 'NT$ 200 / session' },
]

/** 5.4 Goalkeeper Foundation Class (zh twin: GOALKEEPER_CLASS_BW; `signupUrl` is language-neutral and stays on the zh constant). */
export const GOALKEEPER_CLASS_BW_EN = {
  nameZh: 'Blue Whale Goalkeeper Foundation Class',
  ageZh: 'Ages 7-12 (boys and girls welcome, places reserved for girls, limited to 10)',
  feeZh: 'NT$ 200 / session',
  scheduleZh: '1.5 hours per week',
  signupZh: 'Registration form must be completed first',
}

/** 5.5 partner school list (zh twin: SCHOOL_PARTNERS_BW). */
export const SCHOOL_PARTNERS_BW_EN: SchoolPartnerRow[] = [
  { nameZh: 'National Taiwan Sport University women\'s football team', contentZh: 'Industry-academia cooperation (women\'s football team)', yearZh: null },
  { nameZh: 'Taichung Municipal Wuquan Junior High School women\'s football team', contentZh: 'Industry-academia cooperation (women\'s football team)', yearZh: '2015' },
  { nameZh: 'Nantou County Shuili Junior High School women\'s football team', contentZh: 'Industry-academia cooperation (women\'s football team)', yearZh: null },
  { nameZh: 'Changhua County Yongjing Junior High School women\'s football team', contentZh: 'Industry-academia cooperation (women\'s football team)', yearZh: null },
  { nameZh: 'Taichung Duxing Elementary School women\'s football team', contentZh: 'Industry-academia cooperation (women\'s football team)', yearZh: null },
]

/** Label shown when a partner row has no year (zh: the "year not stated" text). */
export const SCHOOL_PARTNER_NO_YEAR_EN = 'Year not stated'

/** zh twin: COMMUNITY_PROGRAM_BODY_BW. */
export const COMMUNITY_PROGRAM_BODY_BW_EN = `${BW} runs the Taichung City Government's "Sport i Taiwan 2.0" Sports Hot Zone program (Taiyuan Football Field), offering age-group football classes such as the community football school (nicknamed "Little Blue Whale") to promote regular exercise for everyone.`

/** zh twin: COACH_TRAINING_BODY_BW. */
export const COACH_TRAINING_BODY_BW_EN = `${BW} has co-hosted coaching workshops with National Taiwan Sport University and the Taichung City Government Sports Bureau, to develop professional football coaches and refresh training ideas and knowledge.`

/** Blue Whale primary venue, English (Taichung Beitun Taiyuan Football Field; wording from the Blue Whale specification).
 * Only a fallback for when the backend `en` response has no venue English name. */
export const BW_PRIMARY_VENUE_EN_FALLBACK = 'Taichung Beitun Taiyuan Football Field'
