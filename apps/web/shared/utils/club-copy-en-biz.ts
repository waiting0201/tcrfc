// shared/utils/club-copy-en-biz.ts — main site (tcrfc) English copy: biz group (Join / Contact, Partners, Charity)
//
// Conventions (shared by every club-copy-en-* file, group code `biz`):
// - Main-site (tcrfc) values are the original exports. Blue Whale (bw) variants (B-5, 2026-10-05) are separate
//   exports named `FOO_EN_BW` (constants) / `getFooEnBw(...)` (functions); existing exports and their
//   signatures are unchanged. bw wording is a translation of the bw zh branch in club-copy.ts: no new facts,
//   no Academy / Taichung Rock wording (the bw unit 04 is the Youth team). Short name = BW_NAME_EN.
// - Original export `FOO` (constant) -> `FOO_EN`; original function `getFoo(club, facts)` -> `getFooEn(facts)`.
// - Types are reused from club-copy.ts and the field names are unchanged, but the VALUES are English
//   (for example `h1Zh` holds the English H1 and `lede` the English lede), so pages only swap the source.
// - No Chinese characters inside string literals in this file (scripts/check-en-copy.mjs checks this).
//   English wording follows the English terminology table in docs/06-conventions.md section 1.1.
// - Facts (founding year, league, squad codes) always come from the caller's `facts`, never hard-coded here
//   (check-fact-single-source.mjs).

import type { HeroCopy, SeoCopy } from './club-copy'
import { BW_NAME_EN } from './club-copy'
import type { SiteFacts } from './site-facts'

/** "U15 / U14 / U12" style list built from facts.squadCodes. */
function squadCodesEn(facts: SiteFacts): string {
  return facts.squadCodes.join(' / ')
}

/**
 * English club name used inside shared biz page templates (SEO titles, ledes, notices):
 * tcrfc -> Taichung Rock FC, bw -> BW_NAME_EN (short name; the full name is used only in Schema / llms / footer).
 */
export function bizClubNameEn(club: string): string {
  return club === 'bw' ? BW_NAME_EN : 'Taichung Rock FC'
}

// ---------------------------------------------------------------------------
// 10 Join / Contact
// ---------------------------------------------------------------------------

export const JOIN_INDEX_SEO_EN: SeoCopy = {
  title: 'Join / Contact | Taichung Rock FC',
  description:
    'Join / Contact at Taichung Rock FC: seven forms covering Join as a Player, Academy and children\'s training, camp registration, international player enquiries, partnership and sponsorship, media enquiries and general contact, plus venue locations and contact details.',
}

export const JOIN_INDEX_HERO_EN: HeroCopy = {
  h1Zh: 'Join / Contact',
  h1En: null,
  lede: 'Whether you are a player who wants to join the team, a parent looking for structured training for your child, or a company or media outlet that wants to work with Taichung Rock FC, you will find the right form here. Each of the seven forms goes to a different department, and we will get back to you as soon as we can.',
}

/** 10.2 card: title and description (squad codes come from facts). */
export function getJoinAcademyCardEn(facts: SiteFacts): { titleZh: string; descZh: string } {
  return {
    titleZh: 'Academy & Children\'s Training',
    descZh: `Academy ${squadCodesEn(facts)} squads, or children's training (mixed-age, beginner and skills development classes), all through one form.`,
  }
}

export const JOIN_CONTACT_SEO_EN: SeoCopy = {
  title: 'Contact Information | Join / Contact | Taichung Rock FC',
  description: 'Taichung Rock FC contact information: phone, email, address, opening hours, department extensions and social media links.',
}

export const JOIN_CONTACT_HERO_EN: HeroCopy = {
  h1Zh: 'Contact Information',
  h1En: null,
  // Contains an inline link marker; contact/index.vue renders it with v-html (same as the original copy).
  lede: 'Phone, email, address, opening hours and department extensions, so you can find the right contact for your needs. For a specific application or enquiry, we recommend using the <a href="/zh/join/">relevant form</a>, which is processed faster.',
}

/** 10.4 International Player Enquiries: SEO (page body is already English-first). */
export function getInternationalPlayerSeoEn(): SeoCopy {
  return {
    title: 'International Player Enquiries | Join / Contact | Taichung Rock FC',
    description:
      'Interested in playing for Taichung Rock FC (TCRFC) in Taiwan? Submit your football background, video highlights and visa status. Our International department will get back to you.',
  }
}

// ---------------------------------------------------------------------------
// Blue Whale (bw) variants, group biz
// ---------------------------------------------------------------------------

export const JOIN_INDEX_SEO_EN_BW: SeoCopy = {
  title: `Join / Contact | ${BW_NAME_EN}`,
  description: `Join / Contact at ${BW_NAME_EN}: Join as a Player, join the Youth team, partnership enquiries, media enquiries and general contact, plus contact information.`,
}

export const JOIN_INDEX_HERO_EN_BW: HeroCopy = {
  h1Zh: 'Join / Contact',
  h1En: null,
  lede: `Whether you are a player who wants to join the team, a parent who wants your child to join the Youth team, or a company or media outlet that wants to work with ${BW_NAME_EN}, you will find the right form here.`,
}

/** 10.2 card for bw (Youth team; squad codes come from facts). */
export function getJoinAcademyCardEnBw(facts: SiteFacts): { titleZh: string; descZh: string } {
  return {
    titleZh: 'Join the Youth Team',
    descZh: `Recruitment for the ${squadCodesEn(facts)} girls' youth football teams. For eligibility and fees, please contact the club.`,
  }
}

/** 10.2 button label on the Join / Contact index for bw (the tcrfc label contains "Academy"). */
export const JOIN_ACADEMY_BUTTON_EN_BW = 'Join the Youth Team'

/** 10.6 card description on the Join / Contact index for bw (venues; the tcrfc text says Academy venues). */
export const JOIN_LOCATION_DESC_EN_BW = 'Locations and directions for our training base, home ground and Youth team venues.'

export const JOIN_CONTACT_SEO_EN_BW: SeoCopy = {
  title: `Contact Information | Join / Contact | ${BW_NAME_EN}`,
  description: `${BW_NAME_EN} contact information: email, LINE official account and social media links.`,
}

export const JOIN_CONTACT_HERO_EN_BW: HeroCopy = {
  h1Zh: 'Contact Information',
  h1En: null,
  // Contains an inline link marker; contact/index.vue renders it with v-html (same as the original copy).
  lede: 'Email, LINE official account and social media links are listed below. For a specific application or enquiry, we recommend using the <a href="/zh/join/">relevant form</a>, which is processed faster.',
}

/** 10.4 International Player Enquiries: SEO for bw. */
export function getInternationalPlayerSeoEnBw(): SeoCopy {
  return {
    title: `International Player Enquiries | Join / Contact | ${BW_NAME_EN}`,
    description: `Interested in playing for ${BW_NAME_EN} in Taiwan? Submit your football background, video highlights and visa status and our club will follow up.`,
  }
}

/** 10.4 follow-up sentence fragments for bw (the tcrfc page names the International department). */
export const JOIN_INTL_FOLLOWUP_EN_BW = {
  success: 'Enquiry received! A confirmation email has been sent to you. Our club will follow up with you directly.',
  note: 'You will receive an automatic confirmation email immediately. Our club will also receive a notification and follow up with you directly regarding next steps.',
}

/** 10.4 follow-up sentences by club. 🔴 The form block is always English (also on the zh page), so this must NOT be gated on the page locale:
 * bw has no "International department" (E-233), tcrfc keeps its verbatim wording. */
export function getJoinIntlFollowUpEn(club: string): { success: string, note: string } {
  if (club === 'bw') return JOIN_INTL_FOLLOWUP_EN_BW
  return {
    success: 'Enquiry received! A confirmation email has been sent to you. Our International department will follow up with you directly.',
    note: 'You will receive an automatic confirmation email immediately. Our International department will also receive a notification and follow up with you directly regarding next steps.',
  }
}

/** 10.2 button label on the 10.0 hub: tcrfc "Academy & Children's Training", bw "Join the Youth Team" (never "Academy"). Used on both zh and en pages. */
export function getJoinAcademyButtonEn(club: string): string {
  return club === 'bw' ? JOIN_ACADEMY_BUTTON_EN_BW : "Academy & Children's Training"
}

/** 09 partners: unit-04 sponsorship plan title in English (zh page decoration and en page). */
export function getPartnersPlanYouthTitleEn(club: string): string {
  return club === 'bw' ? PARTNERS_PLAN_YOUTH_EN_BW : 'Academy Sponsorship'
}

// 09 Partners & Sponsors (bw) ------------------------------------------------

export const PARTNERS_INDEX_LEDE_EN_BW = `Join forces with ${BW_NAME_EN} to reach the local community and the international football network through a women's football platform, creating value for your brand and the community alike.`

export const PARTNERS_BECOME_HERO_EN_BW = `Through the ${BW_NAME_EN} women's football platform, reach the local community, youth-development families and the international football network, and let your brand grow together with the club.`

export const PARTNERS_BECOME_VALUES_EN_BW = {
  why: `${BW_NAME_EN} is one of the teams in the Taiwan Mulan Football League, giving brands a direct route into women's football and the local community.`,
  audience: 'Covers First Team fans, Youth team student families and matchday crowds. Detailed figures are below.',
  social: 'Community football promotion activities let your brand take part in meaningful local social action.',
  community: 'Through the Youth team and our community promotion activities, build long-term trust with families and schools and widen your brand\'s word-of-mouth reach.',
  international: `Through international exchange and overseas expansion programs, let your brand travel the world with ${BW_NAME_EN}'s players.`,
}

export const PARTNERS_PLAN_YOUTH_EN_BW = 'Youth Team Sponsorship'
export const PARTNERS_PLAN_YOUTH_LABEL_EN_BW = 'Youth team sponsorship'

export function getPartnershipFormSeoEnBw(): SeoCopy {
  return {
    title: `Partnership & Sponsorship | Join / Contact | ${BW_NAME_EN}`,
    description: `Talk to ${BW_NAME_EN} about partnership or sponsorship. Complete one form with your company details, partnership direction or the sponsorship packages you are interested in, and your budget range, and the club will be in touch.`,
  }
}

export const PARTNERSHIP_FORM_LEDE_EN_BW = `Join forces with ${BW_NAME_EN} to reach the local community through a women's football platform and create value for your brand and the community alike. Whether you want to discuss a long-term partnership or a specific sponsorship package, this one form covers it. Fill in the details below and the club will get in touch to discuss the details.`
