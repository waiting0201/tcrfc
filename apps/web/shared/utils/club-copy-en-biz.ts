// shared/utils/club-copy-en-biz.ts — main site (tcrfc) English copy: biz group (Join / Contact, Partners, Charity)
//
// Conventions (shared by every club-copy-en-* file, group code `biz`):
// - Only tcrfc English values are provided. The Blue Whale site never shows English UI (isEn is false there).
// - Original export `FOO` (constant) -> `FOO_EN`; original function `getFoo(club, facts)` -> `getFooEn(facts)`.
// - Types are reused from club-copy.ts and the field names are unchanged, but the VALUES are English
//   (for example `h1Zh` holds the English H1 and `lede` the English lede), so pages only swap the source.
// - No Chinese characters inside string literals in this file (scripts/check-en-copy.mjs checks this).
//   English wording follows the English terminology table in docs/06-conventions.md section 1.1.
// - Facts (founding year, league, squad codes) always come from the caller's `facts`, never hard-coded here
//   (check-fact-single-source.mjs).

import type { HeroCopy, SeoCopy } from './club-copy'
import type { SiteFacts } from './site-facts'

/** "U15 / U14 / U12" style list built from facts.squadCodes. */
function squadCodesEn(facts: SiteFacts): string {
  return facts.squadCodes.join(' / ')
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
