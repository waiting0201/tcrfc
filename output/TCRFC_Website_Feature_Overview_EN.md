# Taichung Rock FC — Official Website Feature Overview (Client Edition)

> **Document version**: v1.4
> **Date**: 2026-09-14 (v1.4 revision: 2026-09-18)
> **Corresponds to**: *TCRFC Website Functional Specification* v3.9
> **Brand promise**: LOCAL ROOTS. GLOBAL PATHWAYS.

> **How to read this document**
> This is a plain-language **feature overview** of the official website, written so that anyone who does not read technical documents can still confirm what the site will contain and who maintains it.
> The full specification is the *TCRFC Website Functional Specification*; where the two differ, the specification governs.
> Screens and layouts described here are **functional arrangements**, not final visual design.

---

## Contents

1. [What the site is](#1-what-the-site-is)
2. [The public site](#2-the-public-site)
3. [The official shop](#3-the-official-shop)
4. [Membership and the membership card](#4-membership-and-the-membership-card)
5. [The admin: every module](#5-the-admin-every-module)
6. [Who can do what](#6-who-can-do-what)
7. [What has to be maintained after launch](#7-what-has-to-be-maintained-after-launch)
8. [Two languages](#8-two-languages)

---

## 1. What the site is

The official website of Taichung Rock Football Club, in **Traditional Chinese and English**, with one admin managing all of it.

It serves four audiences, each arriving for a different reason:

| Visitor | Wants to know | What the site gives them |
|---|---|---|
| **Supporters** | When the next match is and how the last one went | Fixtures, results, squad lists, news |
| **Parents** | Whether their child can train here, how to sign up, what it costs | Academy pages, programmes and camps, trial sign-up |
| **Companies** | What sponsoring this club is worth | Partners and sponsors section, downloadable proposal |
| **Media** | Assets and a contact | News and stories, media area, contact forms |

The site is also one half of a **two-club system**: Taichung Rock's own site is this one, and Taichung Blue Whale Women's Team has a separate site of its own (see *Taichung Blue Whale Website Feature Overview (Client Edition)*). **The content is kept apart; the admin is one shared entrance.**

---

## 2. The public site

The site is organised into **13 sections**, plus the shop and the member area.

| # | Section | What is in it | Where the content comes from |
|---|---|---|---|
| **01** | **Home** | Nine arrangeable blocks: hero, next match, latest news, academy intake, shop picks, sponsors | Pulled from each module; block order set in the admin |
| **02** | **About Taichung Rock** | Founding story, vision and mission, five core values, organisation, honours, facilities | Written once, rarely changes |
| **03** | **Football Club** | First team, squad, coaching staff, season reviews | Teams module; **must follow squad changes** |
| **04** | **Academy** | Philosophy, age groups (U15 / U14 / U12), coaches, grounds, intake and trials | Part fixed copy, part follows each age group |
| **05** | **Programmes** | Regular courses, short camps, taster sessions, school and corporate work | Programmes module; **updated every intake** |
| **06** | **Women's Football** | A single page plus **the entrance to the Blue Whale site** | Squad and fixtures live on the Blue Whale site; not duplicated here |
| **07** | **News & Stories** | Latest news, match reports, features, video, media area | **The most frequently updated section** |
| **08** | **Culture** | Brand story, the comic, merchandise, supporters' club | Comic episodes and products need regular updates |
| **09** | **Partners & Sponsors** | Partner wall, sponsorship tiers, downloadable proposal, enquiry form | Updated by commercial staff as deals are signed |
| **10** | **Join / Contact** | Player recruitment, academy sign-up, supporters' club, volunteering, corporate enquiries, contact and map | Form submissions land in the admin inbox |
| **11** | **Charity & Impact** | Programme descriptions, impact figures, partner organisations | Descriptive only; **actual fundraising runs on the Association's separate platform** |
| **12** | **FAQ** | Searchable questions gathered across topics | Added to by support as real questions come in |
| **13** | **Schedule** | Fixtures, results and tables per team, filterable, subscribable to a phone calendar | **Maintained by hand; the heaviest load in season** |

Two further areas:

- **The official shop**: product list, product page, cart, checkout, order lookup (section 3)
- **The member area**: visible once signed in — orders, membership status, membership card (section 4)

### On every page

Search, breadcrumbs, language switch, sharing, related content, a contact call to action, cookie consent, and accessibility support (keyboard operation and screen readers).

---

## 3. The official shop

The shop runs **inside the site**, not on a third-party platform. The club is the collecting entity.

**What a customer does**: browse → choose a size or colour → add to cart → check out → **pay with LINE Pay** → receive a confirmation email → receive a dispatch notice → receive the goods.

| Item | How it works in this release |
|---|---|
| **Payment** | **LINE Pay only** |
| **Invoice** | **An e-invoice is always issued**; carrier, tax ID or donation code can be entered |
| **Account needed?** | **No.** Non-members can check out and later look up an order with the order number and email |
| **Shipping** | One flat rate plus a free-shipping threshold |
| **What is sold** | **Physical goods only.** Memberships, programme fees and donations do not go through the shop |
| **Returns** | Requested on the site; the invoice is voided or credited on return |

> **The old Wix shop** stops selling once the new site goes live, and its addresses redirect here.

Blue Whale merchandise is sold through the same system. **The collecting entity and the invoice title remain the club**, with the proceeds settled to Blue Whale afterwards by product. A cart **may only contain one club's goods at a time**.

---

## 4. Membership and the membership card

Membership covers **one thing only — belonging**, at two tiers:

| | Free member | Paid member (supporters' club) |
|---|---|---|
| Partner store discounts | ✔ | ✔ (better rates) |
| Shirt | — | ✔ one |
| Prize draw entry | — | ✔ |
| Digital membership card | ✔ | ✔ |

**The rules that matter**:

- **One person, one account — but one membership per club.** Joining Rock only, Blue Whale only, or both are all normal.
- **Each membership carries its own digital card**, in that club's crest and colours.
- When a store scans the card's QR code, the page it opens shows only the **first character of the name, the member number, the tier, and whether it is valid or expired** — no personal data is exposed.
- Fees run **by season**, paid through a LINE Pay payment link or in person, not through the shop checkout.
- **The two clubs' seasons are not aligned**; expiry dates and renewal reminders are calculated separately.

**The prize draw**: any paid member whose membership is valid at the cut-off time is **entered automatically, with nothing to do**, one number each. **Spending more, checking in or sharing does not improve anyone's odds.** The draw itself is made by hand, live or on stream, and winners are announced through the news section.

---

## 5. The admin: every module

**Every admin module corresponds to somewhere on the website.** Module names match what visitors see, and the interface is written in everyday words — no codes, no technical terms.

**You do not need to compress images before uploading them.** Choose a picture, press Save, and the system resizes it to a web-friendly size, converts it to a lighter format, and prepares smaller copies for mobile screens and list views; any location data embedded in the photo is stripped at the same time. Each page then loads the size it actually needs, so phones never download the large version — pages open faster and storage stays small.

**One entrance, two clubs kept apart.** After signing in, a switcher at the top of the screen selects which club is being worked on; every list and report then shows only that club's data. Anyone authorised for a single club never sees the switcher.

| Module | Submodule | What it does |
|---|---|---|
| **Dashboard** | — | Tasks, latest submissions, key figures |
| **Content** | Pages | Static pages and editable blocks (including the Blue Whale entrance page) |
| | News & Stories | Writing, scheduled publishing, categories and tags |
| | Home layout | Order of the home page blocks and the hero |
| | FAQ | Questions and answers, including reports of failed searches |
| | Charity & Impact | The descriptive content and impact figures for section 11 |
| | Press & Media | Press releases, brand identity packs and high-resolution images for the media section |
| **Teams** | Teams | First team, academy age groups, Blue Whale first team |
| | Players | Squad, numbers, positions, photographs, biographies |
| | Coaches and staff | Coaching and administrative staff |
| | Fixtures & Results | **Fixtures, results, tables** (bulk import supported) |
| | Honours and milestones | Titles and notable records |
| **Programmes** | Courses and camps | Regular courses and short camps |
| | Intakes and sessions | Dates and places available |
| | Registrations | Intake, review, exports |
| | Trial sessions | Trial dates and sign-ups |
| **Business** | Partners | The B2B partner wall |
| | Sponsors and packages | Tiers and entitlements |
| | Proposal and download tracking | Who downloaded the sponsorship proposal |
| | Advertisers and slots | **Directly sold advertising in the mobile app** |
| | Flights and creatives | Scheduling and creative management |
| | Performance reports | Impressions and clicks |
| **Culture** | Comic | Characters, episodes, reading settings |
| | Supporters' club events | Event scheduling (the roster lives in module K) |
| **Enquiries** | Form designer | Building form fields |
| | Inbox | Seven form types plus proposal downloads and donation enquiries |
| | Newsletter list | Subscriptions and unsubscribes |
| **Search & AI visibility** | — | Titles and descriptions, sitemap, structured data, **settings that help AI understand the site** |
| **Site settings** | — | Menus, footer, languages, contact details, venues, external services |
| **System** | Accounts | Admin accounts |
| | Roles and permissions | Who can do what |
| | Audit and backup | Operation records and backups |
| | Clubs and authorisation | **Club branding and legal details; each account's club authorisations** |
| **Members** | Member list | Member records (personal data masked by role) |
| | Memberships and plans | Activation, renewal, expiry |
| | Shirt fulfilment | Sizes and issue records |
| | Partner stores and benefits | Store list and discount terms |
| | Prize draw rosters | Building rosters, issuing numbers, exporting (**no random selection**) |
| **Calendar** | Overview calendar | A combined view across modules |
| | Own events | Club events |
| | Categories and display | Colours and grouping |
| | Subscription and export | Lets supporters subscribe from their phones |
| **Mobile app** | Versions and releases | App versions and forced updates |
| | Content and deep links | Arranging the app's content |
| | Push notifications | Sending pushes (**two-person approval required**) |
| | Push devices | Device management |
| | App settings & connection check | App configuration and troubleshooting |
| **Shop** | Products & options | Products, sizes and colours, prices |
| | Stock | Movements and safety levels |
| | Orders | Order status and support handling |
| | Dispatch and delivery | Dispatch notes and tracking |
| | Returns and refunds | Return requests and refunds |
| | Shop settings and reports | Shipping, payment and invoice settings, sales reports |

> The charity donation platform is run by the Taiwan Football Strategic Development Association and is **an entirely separate system with its own admin**; it is not part of this one. What "Charity & Impact" maintains here is the **descriptive page** on the website — a different thing.

---

## 6. Who can do what

The admin is organised by role, so **each person sees only what they need**. Six roles are used in practice:

| Role | Responsible for | Typically held by |
|---|---|---|
| **System administrator** | Everything, including accounts, payment settings and refunds | One or two people, no more |
| **Content and press** | News, pages, FAQ, media assets, product copy | The marketing or content contact |
| **Teams and programmes** | Teams, players, fixtures, results, course intakes, registrations | Coaching staff or administration |
| **Commercial / sponsorship** | Partners, sponsorship packages, advertising | The commercial contact |
| **Support / administration** | Members, memberships, orders, dispatch, returns, registrations | The day-to-day operational core |
| **Partner club manager** | Blue Whale's own content, teams, memberships and orders | Blue Whale's own maintainers |

Two further roles are available if the division of work calls for them: **translator** (English fields only, cannot alter the Chinese original) and **viewer** (read-only, cannot see amounts).

**Three safeguards**:

- **Refunds, payment settings and app releases are restricted to the system administrator.** These are irreversible actions, so the permission is deliberately kept as narrow as possible.
- **Push notifications take two people.** One composes, the administrator approves before anything is sent — a push cannot be recalled.
- **Content goes through submit-then-publish**; not everyone can put something live.

**Three rules about personal data**:

1. Members' email addresses, phone numbers, dates of birth and delivery addresses are **restricted**: only the system administrator and support see them in full, everyone else sees a masked version such as `a***@gmail.com`.
2. **Exporting a list requires a separate authorisation**, and every export is recorded: who, when, how many records, and why.
3. **Prize draw rosters count as member personal data.** Press staff writing the announcement receive a masked roster.

---

## 7. What has to be maintained after launch

A website is not finished when it launches. The table below is a realistic staffing expectation; it is worth naming the people before launch.

| What | How often | Who | If it slips |
|---|---|---|---|
| **Fixtures and results** | **Weekly in season** | Teams and programmes | The schedule is the most-read content on the site; a delay is felt immediately |
| **News and stories** | One to two a week | Content and press | The home page starts to look abandoned |
| **Squad and staff** | Each season, and on any transfer | Teams and programmes | An out-of-date squad is worse than no squad |
| **Courses and intakes** | Every intake | Teams and programmes | Parents are looking at last term's dates |
| **Form submissions** | **Daily** | Support | Parents who wait for a reply go elsewhere |
| **Products and stock** | On new lines and restocks | Commercial / support | Overselling has to be cleaned up by hand |
| **Orders and dispatch** | **Daily** | Support | Late dispatch turns straight into complaints |
| **Membership activation and renewals** | Daily to weekly | Support | **The two seasons are not aligned, so expiries occur all year** |
| **FAQ** | Monthly | Support | The same question keeps arriving in the inbox |
| **The English edition** | Alongside the Chinese | Translator | The English site quietly degrades into a half-site |

> **The first three are the ones to assign now.** Fixtures, news and squads are why supporters come back, and all three are **continuous** work rather than a one-off task.

---

## 8. Two languages

- **Traditional Chinese leads, English follows**, separated in the address as `/zh/` and `/en/`.
- Every piece of public content has both a Chinese and an English field. **English may be left empty, but the field always exists** — adding English later never requires a code change.
- A third language is already allowed for in the structure; adding one later does not mean rebuilding.
- Where English lags, the page shows the Chinese text with the language marked rather than appearing blank.

---

> Taichung Rock Football Club　·　Official Website Feature Overview (Client Edition) v1.3
> Based on the *TCRFC Website Functional Specification* v3.7　·　13 sections plus shop and member area　·　Chinese and English
