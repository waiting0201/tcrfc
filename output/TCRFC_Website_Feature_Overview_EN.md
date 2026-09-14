# Taichung Rock FC — Official Website Feature Overview (Client Edition)

> **Document version**: v1.0
> **Date**: 2026-09-14
> **Corresponds to**: *TCRFC Website Functional Specification* v3.4
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

**One entrance, two clubs kept apart.** After signing in, a switcher at the top of the screen selects which club is being worked on; every list and report then shows only that club's data. Anyone authorised for a single club never sees the switcher.

| Module | Submodule | What it does |
|---|---|---|
| **A Dashboard** | — | Tasks, latest submissions, key figures |
| **B Content** | B1 Pages | Static pages and editable blocks (including the Blue Whale entrance page) |
| | B2 News & Stories | Writing, scheduled publishing, categories and tags |
| | B3 Media library | Images and video, shared by every module |
| | B4 Home blocks / banners | Order of the home page blocks and the hero |
| | B5 FAQ | Questions and answers, including reports of failed searches |
| | B6 Charity records | The descriptive content and impact figures for section 11 |
| **C Teams** | C1 Teams | First team, academy age groups, Blue Whale first team |
| | C2 Players | Squad, numbers, positions, photographs, biographies |
| | C3 Coaches and staff | Coaching and administrative staff |
| | C4 Fixtures | **Fixtures, results, tables** (bulk import supported) |
| | C5 Honours and milestones | Titles and notable records |
| **P Programmes** | P1 Courses and camps | Regular courses and short camps |
| | P2 Intakes and sessions | Dates and places available |
| | P3 Registrations | Intake, review, exports |
| | P4 Trial sessions | Trial dates and sign-ups |
| **E Business** | E1 Partners | The B2B partner wall |
| | E2 Sponsors and packages | Tiers and entitlements |
| | E3 Proposal and download tracking | Who downloaded the sponsorship proposal |
| | E4 Advertisers and slots | **Directly sold advertising in the mobile app** |
| | E5 Flights and creatives | Scheduling and creative management |
| | E6 Performance reports | Impressions and clicks |
| **F Culture** | F1 Comic | Characters, episodes, reading settings |
| | F2 Supporters' club events | Event scheduling (the roster lives in module K) |
| **G Enquiries** | G1 Form designer | Building form fields |
| | G2 Inbox | Seven form types plus proposal downloads and donation enquiries |
| | G3 Newsletter list | Subscriptions and unsubscribes |
| **H SEO & marketing** | — | Titles and descriptions, sitemap, structured data |
| **I Site settings** | — | Menus, footer, languages, contact details, venues, external services |
| **J System** | J1 Accounts | Admin accounts |
| | J2 Roles and permissions | Who can do what |
| | J3 Audit and backup | Operation records and backups |
| | J4 Clubs and authorisation | **Club branding and legal details; each account's club authorisations** |
| **K Members** | K1 Member list | Member records (personal data masked by role) |
| | K2 Memberships and plans | Activation, renewal, expiry |
| | K3 Shirt fulfilment | Sizes and issue records |
| | K4 Partner stores and benefits | Store list and discount terms |
| | K5 Prize draw rosters | Building rosters, issuing numbers, exporting (**no random selection**) |
| **L Calendar** | L1 Overview calendar | A combined view across modules |
| | L2 Own events | Club events |
| | L3 Categories and display | Colours and grouping |
| | L4 Subscription and export | Lets supporters subscribe from their phones |
| **M Mobile app** | M1 Versions and releases | App versions and forced updates |
| | M2 Content and deep links | Arranging the app's content |
| | M3 Push notifications | Sending pushes (**two-person approval required**) |
| | M4 Devices and push tokens | Device management |
| | M5 Settings, certificates, diagnostics | App configuration and troubleshooting |
| **S Shop** | S1 Products and variants | Products, sizes and colours, prices |
| | S2 Stock | Movements and safety levels |
| | S3 Orders | Order status and support handling |
| | S4 Dispatch and delivery | Dispatch notes and tracking |
| | S5 Returns and refunds | Return requests and refunds |
| | S6 Shop settings and reports | Shipping, payment and invoice settings, sales reports |

> The charity donation platform is run by the Taiwan Football Strategic Development Association and is **an entirely separate system with its own admin**; it is not part of this one. What module `B6` maintains here is the **descriptive page** on the website — a different thing.

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

> Taichung Rock Football Club　·　Official Website Feature Overview (Client Edition) v1.0
> Based on the *TCRFC Website Functional Specification* v3.4　·　13 sections plus shop and member area　·　Chinese and English
