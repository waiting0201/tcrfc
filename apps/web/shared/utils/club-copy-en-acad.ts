// shared/utils/club-copy-en-acad.ts - Taichung Rock FC (tcrfc) English copy: group "acad"
// (04 Academy pages and 05 Programs pages).
//
// Conventions (shared by all club-copy-en-* files):
// - Only tcrfc English values are provided. The Blue Whale site never reads these (isEn is always false there).
// - Original constant `FOO` -> `FOO_EN`; original function `getFoo(club, facts)` -> `getFooEn(facts)`
//   (functions that took only `club` become `getFooEn()`).
// - Types are reused from club-copy.ts. Field names are unchanged but the values are English
//   (for example `h1Zh` holds the English H1 and `h1En` is null, `titleZh` holds the English title).
// - No Chinese characters may appear in this file (a script checks it). Terms follow the
//   docs/06-conventions.md section 1.1 English glossary.
// - Facts (founding year, squad codes) always come from the `facts` argument supplied by the caller.

import type { HeroCopy, SeoCopy, AcademyTeamTab, AcademyHubCard, ProgramsHubCard, EnrolFlowStep, CtaCardCopy } from './club-copy'
import type { SiteFacts } from './site-facts'

const SITE = 'Taichung Rock FC'

function squadCodes(facts: SiteFacts, separator = ', '): string {
  return facts.squadCodes.join(separator)
}

// ---------------------------------------------------------------------------
// 04 Academy hub
// ---------------------------------------------------------------------------

export function getAcademyHubSeoEn(facts: SiteFacts): SeoCopy {
  return {
    title: `TCRFC Academy | ${SITE}`,
    description: `TCRFC Academy is the youth development system of ${SITE}. It offers age-group training (${squadCodes(facts)}), a clear development pathway and the support of a coaching team, linking players to the First Team and to opportunities overseas.`,
  }
}

export function getAcademyHubHeroEn(): HeroCopy {
  return {
    h1Zh: 'TCRFC Academy',
    h1En: null,
    lede:
      'TCRFC Academy carries forward the club vision of LOCAL ROOTS. GLOBAL PATHWAYS. Through age-group training, a clear development pathway ' +
      'and the long-term support of our coaches, we help players grow from core technique towards the First Team or opportunities overseas.',
  }
}

export function getAcademyHubCardsEn(facts: SiteFacts): AcademyHubCard[] {
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

// ---------------------------------------------------------------------------
// 4.1 Overview
// ---------------------------------------------------------------------------

export function getAcademyOverviewSeoEn(): SeoCopy {
  return {
    title: `Academy Overview | TCRFC Academy | ${SITE}`,
    description:
      'Get to know the positioning and training base of TCRFC Academy: a youth development system that carries forward the club\'s brand vision, ' +
      'plus highlights such as player numbers, coach numbers and school-progression rates (data being collected).',
  }
}

export function getAcademyOverviewHeroEn(): HeroCopy {
  return {
    h1Zh: 'Academy Overview',
    h1En: null,
    lede:
      'TCRFC Academy is the youth development system of Taichung Rock FC. It carries forward the club vision of LOCAL ROOTS. GLOBAL PATHWAYS. ' +
      'and the five core values of Players First, Excellence, Global Pathways, Community and Integrity, supporting players\' growth through age-group training.',
  }
}

export function getAcademyPositioningEn(facts: SiteFacts): string {
  return `Since the club was founded in ${facts.foundedYear}, TCRFC Academy has served as a bridge between community football and the competitive system. Our aim is for every player to develop technique, tactical understanding and character at their own pace in a solid training environment, and, for players ready to step up to the First Team or an overseas stage, to offer a clear pathway for growth.`
}

// ---------------------------------------------------------------------------
// 4.2 Our Teams
// ---------------------------------------------------------------------------

export function getAcademyTeamsSeoEn(facts: SiteFacts): SeoCopy {
  return {
    title: `Our Teams | TCRFC Academy | ${SITE}`,
    description: `TCRFC Academy squads (${squadCodes(facts)} and other age groups): rosters, coaches, fixtures and results by squad (data being collected), with a subscription to each squad's calendar.`,
  }
}

export function getAcademyTeamsHeroEn(facts: SiteFacts): HeroCopy {
  return {
    h1Zh: 'Our Teams',
    h1En: null,
    lede: `TCRFC Academy is organised by age into ${squadCodes(facts)} and other age-group squads. Each squad has its own roster, coaches, fixtures and results, and you can subscribe to a squad's calendar to keep up with every training session and match.`,
  }
}

/** English tab labels. `id` and `teamCode` are identical to getAcademyTeamTabs(); only the label of the "other" tab is English. */
export function getAcademyTeamTabsEn(facts: SiteFacts): AcademyTeamTab[] {
  return [
    ...facts.squadCodes.map((code) => ({ id: code.toLowerCase(), labelZh: code, teamCode: code })),
    { id: 'other', labelZh: 'Other age groups', teamCode: null },
  ]
}

// ---------------------------------------------------------------------------
// 4.3 Pathway / 4.4 Curriculum / 4.5 Coaches / 4.6 Life
// ---------------------------------------------------------------------------

export function getAcademyPathwaySeoEn(): SeoCopy {
  return {
    title: `Academy Pathway | TCRFC Academy | ${SITE}`,
    description: 'The step-by-step development pathway at TCRFC Academy: U12 to U15 to the First Team or overseas. Select each stage to see what it takes to move up (details being collected).',
  }
}

export function getAcademyPathwayHeroEn(): HeroCopy {
  return {
    h1Zh: 'Academy Pathway',
    h1En: null,
    lede: 'Start at U12, deepen your game at U15, then step up to the First Team or an overseas stage. TCRFC Academy offers a step-by-step development pathway. Select a stage below to see how players progress to the next level.',
  }
}

export function getAcademyCurriculumSeoEn(): SeoCopy {
  return {
    title: `Training & Curriculum | TCRFC Academy | ${SITE}`,
    description: 'Training at TCRFC Academy covers five pillars: technical, tactical, physical, game reading and character. The syllabus and training-cycle plan for each pillar are being collected.',
  }
}

export function getAcademyCurriculumHeroEn(): HeroCopy {
  return {
    h1Zh: 'Training & Curriculum',
    h1En: null,
    lede: 'Technical, tactical, physical, game reading and character: training at TCRFC Academy is built around five pillars, each with its own syllabus and training-cycle plan.',
  }
}

export function getYouthCoachesSeoEn(): SeoCopy {
  return {
    title: `Coaches | TCRFC Academy | ${SITE}`,
    description: 'Meet the TCRFC Academy coaching team: the Director of Youth Development and two youth coaches. Licences, specialisms and squad responsibilities are being collected.',
  }
}

export function getYouthCoachesHeroEn(): HeroCopy {
  return {
    h1Zh: 'Academy Coaches',
    h1En: null,
    lede: 'The TCRFC Academy coaching team consists of one Director of Youth Development and two youth coaches, who accompany players in every squad from core technique to reading the game.',
  }
}

export function getYouthLifeSeoEn(): SeoCopy {
  return {
    title: `Academy Life | TCRFC Academy | ${SITE}`,
    description: 'Everyday training, matches and activities at TCRFC Academy, with real photos that record players\' growth.',
  }
}

export function getYouthLifeHeroEn(): HeroCopy {
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

export function getProgramsHubSeoEn(): SeoCopy {
  return {
    title: `Programs | ${SITE}`,
    description:
      'An overview of Taichung Rock FC programs: Children\'s Training, Summer Camp, Winter Camp, Specialist Training and School & Community projects. ' +
      'Every session lists its venue, schedule, places and fee, with online registration.',
  }
}

export function getProgramsHubHeroEn(): HeroCopy {
  return {
    h1Zh: 'Programs',
    h1En: null,
    lede:
      'From Children\'s Training, Summer and Winter Camps and Specialist Training to School & Community projects, Taichung Rock FC runs every way of meeting the ball as a clearly defined program, ' +
      'with the venue, schedule, places and fee of each session shown up front.',
  }
}

export function getProgramsHubIntroEn(): string {
  return 'Programs at Taichung Rock FC are managed by session: every program has a clear level or category, training venue, schedule, capacity and fee, and can be booked online. The five areas below serve players of different ages and needs. Select a card to see the details.'
}

export function getProgramsHubCardsEn(): ProgramsHubCard[] {
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

export function getProgramsHubCtaCardsEn(): CtaCardCopy[] {
  return [
    { num: 'Children & youth', titleZh: 'Children\'s Training', descZh: 'Grouped by age and level, from mixed-age introduction to skill development.', ctaLabelZh: 'Learn more', href: '/zh/programs/childrens-training/' },
    { num: 'Schools & organisations', titleZh: 'School & Community Partnerships', descZh: 'Talk to us about school programs, community projects and coach education.', ctaLabelZh: 'Get in touch', href: '/zh/programs/school-community/' },
    { num: 'Other questions', titleZh: 'Contact Taichung Rock FC', descZh: 'Questions about any program are welcome.', ctaLabelZh: 'Contact us', href: '/zh/join/general/' },
  ]
}

// ---------------------------------------------------------------------------
// 5.1 - 5.5
// ---------------------------------------------------------------------------

export function getChildrensTrainingSeoEn(): SeoCopy {
  return {
    title: `Children's Training | Programs | ${SITE}`,
    description: 'Taichung Rock FC Children\'s Training is organised into mixed-age introduction, beginner and skill-development levels, held at the club\'s home ground and other venues, with a weekly timetable and online registration.',
  }
}

export function getChildrensTrainingHeroEn(): HeroCopy {
  return {
    h1Zh: 'Children\'s Training',
    h1En: null,
    lede: 'From the first touch of a ball to building real skills, Taichung Rock FC plans its programs by age and ability so every child can grow at the right pace.',
  }
}

export function getSummerCampSeoEn(): SeoCopy {
  return {
    title: `Summer Camp | Programs | ${SITE}`,
    description: 'Taichung Rock FC Summer Camp offers intensive football training and activities. Session dates, venue and fees will be announced when registration opens. Online registration does not take payment.',
  }
}

export function getSummerCampHeroEn(): HeroCopy {
  return {
    h1Zh: 'Summer Camp',
    h1En: null,
    lede: 'Spend the summer holiday immersed in football, building ball feel, fitness and teamwork under professional coaches.',
  }
}

export function getWinterCampSeoEn(): SeoCopy {
  return {
    title: `Winter Camp | Programs | ${SITE}`,
    description: 'Taichung Rock FC Winter Camp shares its layout and data with Summer Camp. Session dates, venue, fees and coaching team will be announced when registration opens.',
  }
}

export function getWinterCampHeroEn(): HeroCopy {
  return {
    h1Zh: 'Winter Camp',
    h1En: null,
    lede: 'An intensive football camp during the winter holiday, with the same layout and data as Summer Camp. Session dates and fees will be announced before registration opens.',
  }
}

export function getSpecialistTrainingSeoEn(): SeoCopy {
  return {
    title: `Specialist Training | Programs | ${SITE}`,
    description: 'Taichung Rock FC Specialist Training covers six categories: goalkeeper, forward, defender, midfield, fitness and speed, and advanced training. Planned and delivered by the club\'s coaching team, with online registration.',
  }
}

export function getSpecialistTrainingHeroEn(): HeroCopy {
  return {
    h1Zh: 'Specialist Training',
    h1En: null,
    lede: 'Focused training built around specific positions and abilities, with objectives and target players set by the Taichung Rock FC coaching team according to player needs.',
  }
}

export function getSchoolCommunitySeoEn(): SeoCopy {
  return {
    title: `School & Community | Programs | ${SITE}`,
    description: 'Taichung Rock FC school programs, community projects and coach education. Schools and community organisations are welcome to get in touch through the form and our team will follow up.',
  }
}

export function getSchoolCommunityHeroEn(): HeroCopy {
  return {
    h1Zh: 'School & Community',
    h1En: null,
    lede: 'Taichung Rock FC works with schools and community organisations to promote football, offering school programs, community projects and coach education, and helping to develop local coaches.',
  }
}
