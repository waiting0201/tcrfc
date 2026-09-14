# Taichung Blue Whale — Official Website Feature Overview (Client Edition)

> **Document version**: v1.0
> **Date**: 2026-09-14
> **Corresponds to**: *TCRFC Taichung Blue Whale Website Functional Specification* v1.4
> **Content principal**: Taichung Blue Whale Women's Football Team

> **How to read this document**
> This is a plain-language **feature overview** of the Taichung Blue Whale website, written so that anyone who does not read technical documents can still confirm what the site will contain and who maintains it.
> The full specification is the *TCRFC Taichung Blue Whale Website Functional Specification*; where the two differ, the specification governs.
> Screens and layouts described here are **functional arrangements**, not final visual design.

---

## Contents

1. [How this site relates to the Taichung Rock site](#1-how-this-site-relates-to-the-taichung-rock-site)
2. [The public site](#2-the-public-site)
3. [Membership and the shop](#3-membership-and-the-shop)
4. [The admin: every module](#4-the-admin-every-module)
5. [Who can do what](#5-who-can-do-what)
6. [Brand and visual identity](#6-brand-and-visual-identity)
7. [What has to be maintained after launch](#7-what-has-to-be-maintained-after-launch)
8. [Two languages](#8-two-languages)

---

## 1. How this site relates to the Taichung Rock site

Taichung Blue Whale has **its own address, its own front-end site, and its own colours and crest**, in Chinese and English.

It nonetheless **shares one admin and one database with the Taichung Rock site** — deliberately:

| | Kept separate | Shared |
|---|---|---|
| Web address | ✔ each its own | |
| Front-end site | ✔ each its own | |
| Brand (crest, colours, bylines) | ✔ each its own | |
| Teams, players, fixtures, news | ✔ data kept apart | |
| Sponsors and partners | ✔ **shown separately, never mixed** | |
| Admin entrance and accounts | | ✔ one |
| Member accounts | | ✔ one account per person |
| Shop and collection | | ✔ one system (section 3) |

**Why not build two of everything?** Two admins would mean two sets of accounts, two maintenance routines and two learning curves — and Blue Whale is currently run by Taichung Rock's own staff, who would be switching between them. One admin with the data kept apart is the cheapest arrangement to maintain.

**Switching**: a switcher at the top of the admin selects whether you are working on Rock or Blue Whale; every list then shows only that club's data.

---

## 2. The public site

The structure follows the Taichung Rock site, with **11 sections**. The numbering deliberately preserves the correspondence (06 and 11 are skipped) so that staff switching between the two sites in one admin do not confuse them.

| # | Section | What is in it |
|---|---|---|
| **01** | **Home** | Hero, next match, latest news, featured players, sponsors |
| **02** | **About Taichung Blue Whale** | The story since 2014, the AFC club licence, organisation, a message from the manager |
| **03** | **First Team** | Squad, coaching staff, player features |
| **04** | **Youth** | U15 and U12 girls' teams |
| **05** | **Programmes** | Community and school work, football festivals, the Blue Whale Cup |
| **07** | **News** | Latest news, match reports, features |
| **08** | **Culture & Shop** | Brand story, merchandise |
| **09** | **Partners & Sponsors** | Blue Whale's own partners and sponsors, enquiry form |
| **10** | **Join / Contact** | Player recruitment, joining as a member, contact and map |
| **12** | **FAQ** | Searchable questions and answers |
| **13** | **Schedule** | **Taiwan Mulan League, cups, international invitationals**, subscribable to a phone calendar |

Plus the **member area**, visible once signed in.

**Two sections are deliberately absent**:

| On the Rock site, not here | Why |
|---|---|
| **Women's Football** | This entire site is women's football; a section introducing it would point at itself. The Rock site's section is the entrance to this site |
| **Charity & Impact** | Charitable fundraising is run by the Taiwan Football Strategic Development Association and is unrelated to Blue Whale |

**One section is renamed**: Rock's "Academy" becomes "**Youth**" here — Blue Whale's existing structure is U15 and U12 girls' teams, not Rock's academy intake system, and the academy's course and admission framework is not carried over.

> ⚠️ **Partners and sponsors must be shown separately from Rock's and never mixed.** The two clubs sign their own agreements; a company sponsoring both appears on each site in its own right.

---

## 3. Membership and the shop

### Membership

**One person, one account — but one membership per club.** A Blue Whale supporter can **join Blue Whale alone** without joining Rock; that is the expected case, not an exception.

- Each membership carries **its own digital card**, in Blue Whale's crest and colours, so Blue Whale's partner stores see a Blue Whale card.
- There are still only two tiers, free and paid. **A second club does not create a third kind of member.**
- **The two clubs' seasons are not aligned**; expiry dates and renewal reminders are calculated separately.

### The shop and who collects the money

Blue Whale merchandise is sold through the same shop, with one point that has to be stated plainly:

> **The collecting entity is Taichung Rock Football Club.** Payment runs through the club's LINE Pay account and the invoice is issued in the club's name, with the proceeds settled to Blue Whale afterwards by product.

This is a collect-and-remit arrangement. It changes nothing for the customer — checkout is identical — but it does affect the accounts, which is why it is set out here. A cart **may only contain one club's goods at a time**.

---

## 4. The admin: every module

**It is the same admin as the Taichung Rock site.** The table lists every module; those marked as shared hold data that is not divided by club, while every other module shows only Blue Whale's data when the Blue Whale site is selected.

| Module | Submodule | What it does | Under Blue Whale |
|---|---|---|---|
| **A Dashboard** | — | Tasks, latest submissions, key figures | Blue Whale only |
| **B Content** | B1 Pages | Static pages and editable blocks | Blue Whale's pages |
| | B2 News & Stories | Writing, scheduled publishing, categories | Blue Whale's news |
| | B3 Media library | Images and video | Blue Whale's assets |
| | B4 Home blocks / banners | Home page block order and hero | Blue Whale's home |
| | B5 FAQ | Questions and answers | Blue Whale's entries |
| | B6 Charity records | The website's charity section content | **Not used on this site** |
| **C Teams** | C1 Teams | Blue Whale first team, U15, U12 | Blue Whale's teams |
| | C2 Players | Squad, numbers, positions, photographs, biographies | Blue Whale's players |
| | C3 Coaches and staff | Coaching and administrative staff | Blue Whale's staff |
| | C4 Fixtures | **Fixtures, results, tables** (bulk import supported) | Blue Whale's fixtures |
| | C5 Honours and milestones | Titles and notable records | Blue Whale's records |
| **P Programmes** | P1–P4 | Courses, intakes, registrations, trials | Used if Blue Whale runs outreach |
| **E Business** | E1 Partners | The partner wall | **Blue Whale's own partners** |
| | E2 Sponsors and packages | Tiers and entitlements | **Blue Whale's own sponsors** |
| | E3 Proposal and download tracking | Who downloaded the proposal | Blue Whale's proposal |
| | E4–E6 Advertising | App advertising, flights, performance | **Shared; no Blue Whale access** |
| **F Culture** | F1 Comic | Characters, episodes | Used if Blue Whale has one |
| | F2 Supporters' club events | Event scheduling | Blue Whale's events |
| **G Enquiries** | G1 Form designer | Building form fields | Blue Whale's forms |
| | G2 Inbox | Form submissions | Blue Whale's submissions |
| | G3 Newsletter list | Subscriptions and unsubscribes | Blue Whale's list |
| **H SEO & marketing** | — | Titles and descriptions, sitemap, structured data | Blue Whale's pages |
| **I Site settings** | — | Menus, footer, languages, contact details, venues | This site's settings |
| **J System** | J1 Accounts | Admin accounts | **Shared; administrators only** |
| | J2 Roles and permissions | Who can do what | **Shared; administrators only** |
| | J3 Audit and backup | Operation records and backups | **Shared; administrators only** |
| | J4 Clubs and authorisation | Club branding, legal details, account authorisations | **Shared; administrators only** |
| **K Members** | K1 Member list | Member records (personal data masked by role) | **Blue Whale memberships only** |
| | K2 Memberships and plans | Activation, renewal, expiry | Blue Whale's memberships |
| | K3 Shirt fulfilment | Sizes and issue records | Blue Whale's fulfilment |
| | K4 Partner stores and benefits | Store list and discount terms | Blue Whale's stores |
| | K5 Prize draw rosters | Rosters, numbers, exports | Blue Whale runs its own draw |
| **L Calendar** | L1–L4 | Overview, own events, categories, subscription | Blue Whale's events |
| **M Mobile app** | M1–M5 | Versions, content, push, devices, settings | **Shared; no Blue Whale push rights** |
| **S Shop** | S1 Products and variants | Products, sizes and colours, prices | Blue Whale's products |
| | S2 Stock | Movements and safety levels | Blue Whale's stock |
| | S3 Orders | Order status and support handling | Blue Whale's orders |
| | S4 Dispatch and delivery | Dispatch notes and tracking | Blue Whale's dispatch |
| | S5 Returns and refunds | Returns and refunds | **Refunds: administrators only** |
| | S6 Shop settings and reports | Shipping, payment and invoice settings, reports | **Payment settings shared; administrators only** |

---

## 5. Who can do what

The admin has ten roles in total (described in full in the Taichung Rock feature overview). The one that concerns Blue Whale directly is the tenth:

### Partner club manager

A role designed for Blue Whale's own staff:

| Can | Cannot |
|---|---|
| Their own news, pages and FAQ | Edit any Taichung Rock content |
| Their own teams, players, fixtures and results | Touch Rock's teams |
| Their own partners and sponsors | See Rock's sponsorship agreements |
| Their own products and orders | Issue refunds or change payment settings |
| **Their own membership records** | **See any Taichung Rock membership at all** |
| Their own forms and per-page SEO | Send pushes, manage advertising, or administer the system |

**Member personal data has a further layer of protection**: even for their own memberships, **the member record's fields — email, phone, date of birth, address — are always masked**. A member account is one person's account across both clubs, so buying a Blue Whale membership must not expose that person's details to the other side.

> **No one has to be assigned to this role in the first phase.** Blue Whale is currently maintained by Taichung Rock's staff using their existing accounts. Defining the role without assigning it does not hold up launch.

---

## 6. Brand and visual identity

The Blue Whale site reuses the Taichung Rock site's **layout system** — cards, clipped corners, oversized numerals and colour bands are all brand-agnostic — and **replaces only the brand colours and the crest**.

**The colours are taken from the crest**, not chosen freely:

| | Value | Used for |
|---|---|---|
| **Crest blue** | `#2196D5` | Colour blocks, large headings |
| **Small-text variant (one step darker)** | `#1A78AA` | Small text on white, white text on solid buttons |
| **Crest black** | `#040000` | Body text and dark grounds |

> ⚠️ **Small text cannot use the crest blue directly.** Against white it does not reach the contrast that accessibility standards require at small sizes, so small text always uses the darker variant. The difference is barely visible, but the line has to hold.

**The crest**: the high-resolution transparent original of the crest is used, which **covers the website, the app icon and ordinary documents**.
**The existing crest file is always used as supplied** — never redrawn, never traced from a screenshot, and never scaled up from a small image.

---

## 7. What has to be maintained after launch

| What | How often | If it slips |
|---|---|---|
| **Fixtures and results** | **Weekly in season** | The schedule is the core of this site; a delay is felt immediately |
| **News** | One to two a week | The home page starts to look abandoned |
| **Squad and staff** | Each season, and on any transfer | An out-of-date squad is worse than no squad |
| **Enquiries and sign-ups** | Daily | Enquirers who wait for a reply go elsewhere |
| **Membership activation and renewals** | Daily to weekly | **The two seasons are not aligned, so expiries occur all year** |
| **The English edition** | Alongside the Chinese | The English site quietly falls behind |

> **The main cost is producing content, not building the site.** The front end can be copied from the Rock site; the club history, player biographies, fixture data and English translations cannot — those have to be written.

---

## 8. Two languages

- **Traditional Chinese leads, English follows**, separated in the address as `/zh/` and `/en/`, on the same rules as the Taichung Rock site.
- Every piece of public content has both a Chinese and an English field. English may be left empty, but the field always exists.
- A third language is already allowed for in the structure.

---

> Taichung Blue Whale　·　Official Website Feature Overview (Client Edition) v1.0
> Based on the *TCRFC Taichung Blue Whale Website Functional Specification* v1.4　·　11 sections plus the member area　·　Chinese and English
> **Shares the admin and database of the Taichung Rock FC website; content and brand remain separate.**
