# Taichung Blue Whale — Official Website Functional Specification

> **Document version**: v1.3
> **Date**: 2026-09-10 (v1.3 revision: 2026-09-12)
> **Content principal**: Taichung Blue Whale Women's Football Team
> **System principal**: **shares the admin and database** of the Taichung Rock FC official website
> **Note**: This is the English edition of *TCRFC 台中藍鯨官網功能規劃書 v1.3*. Section numbering matches the Traditional Chinese edition 1:1.

> **v1.3 revision summary — advertising module identifiers**
> **No functional changes; identifiers only.** The advertising module is **`E4–E6`**, in step with website specification v3.3.

> **v1.2 revision summary — the document describes the current specification only**
> **No functional specification changes.** Revision summaries and body text state **what is to be built now**; content that was adjusted away is not recorded.

> **v1.1 revision summary — document scope focused on building the website**
> 1. **This document specifies website functionality only**: everything that **determines how the system is built** is retained — a single collecting entity, the invoice title, orders recording the beneficiary club, and data partitioned by `club_id`.
> 2. **§1.2 is a function-oriented table of principals**; **§10 covers items that bear directly on building the site**.
> 3. Functional scope, section structure, data model and admin design are **unchanged**.

> **What this document covers**
> 1. This document specifies the **Taichung Blue Whale official website**: a bilingual club site on its own domain, as its own front-end project, **sharing one admin and one database with the Taichung Rock FC website**, with data partitioned by `club_id`.
> 2. Its relationship to [`TCRFC_Website_Functional_Specification_EN.md`](TCRFC_Website_Functional_Specification_EN.md) (v3.0 onwards) is **subordinate, not parallel**: the main-site specification defines the multi-club architecture, the data model, the admin modules and permissions. **This document does not restate them; it covers only what is specific to the Blue Whale site.** Where the two conflict, the main-site specification prevails.
> 3. Its relationship to [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) (v3.0 onwards): the app is a shared handheld interface for both clubs; this site is Blue Whale's content and SEO home. **Deep links to Blue Whale content in the app fall back to this site.**
> 4. This document **does not cover** the Charity Donation Platform — that is organised by the Taiwan Football Strategic Development Association and, from its specification v2.0, is a fully independent system with no bearing on Blue Whale.

---

## 1. Objectives and Scope

### 1.1 Why build it

Taichung Blue Whale's existing site is built on **Google Sites** ([www.tcbw2014.com](https://www.tcbw2014.com/)): ten sections, **no second language, no structured data, no functional modules**. For a team holding an **AFC club licence** and playing in the Taiwan Mulan Football League and international invitationals, that site falls short on search visibility, international reach and sponsor presentation.

The goal is to bring Blue Whale's content up to the same level as the Taichung Rock site, and to maintain both clubs' data in **one admin**.

### 1.2 Principals

| Layer | Principal | What it means for the system |
|---|---|---|
| **Content and brand** | **Taichung Blue Whale** | This site's logo, brand colours, bylines and social links are all Blue Whale's |
| **Membership beneficiary** | **Taichung Blue Whale** | A Blue Whale membership is its own record: `Membership.club_id = TCBW` |
| **Collection and invoicing** | **Taichung Rock FC** | **One LINE Pay merchant account, one invoice title.** Orders and membership payments additionally record the **beneficiary club** for settlement (`selling_club_id` / `collecting_club_id`) |
| **System and database** | Shared with the main site | One admin login; data partitioned by `club_id` |

> **The public site must state the collecting party plainly**: checkout and membership payment on this site must say both "you are buying Taichung Blue Whale merchandise / membership" and "payment and the invoice are handled by Taichung Rock FC". Leaving it out invites a "who am I paying?" dispute and makes the invoice title look wrong.

### 1.3 System scope

- **Public site**: 11 top-level sections, two languages (Traditional Chinese / English), Member Centre, on-site shop.
- **Admin**: **no new modules**. Blue Whale's content, teams, programmes, members and orders are all maintained in the main site's existing modules, partitioned by `club_id` and by data-scope permissions (§4).
- **Out of scope**:
  - **Charity referral** — the Charity Donation Platform is organised and collected for by the Taiwan Football Strategic Development Association and has no bearing on Blue Whale. This site has no charity section and no donation referral.
  - **Independent collection** — this site does not set up its own LINE Pay merchant account or invoice track; it uses the main site's single payment configuration. Separating them later would be a major scope change requiring an amendment to the main-site specification first.
  - **Revenue splitting and settlement between the clubs** — handled by offline contract; the system only records the beneficiary and offers aggregation and export.
  - **Cross-club mixed carts** — this site's cart and the main site's cart are independent and must not be mixed (rationale in main-site specification 4.13).
  - **Technology selection** — as with the main-site specification, this document defines functionality only.

---

## 2. Site Architecture

### 2.1 Sections

Mirrors the main site's 13 sections, **omitting two and adapting two**:

```
Taichung Blue Whale official website (its own domain)
├── 01 HOME
├── 02 ABOUT              History (from 2014), AFC licence, organisation, manager's message
├── 03 FIRST TEAM         Squad, coaching staff, player stories        ← BW1
├── 04 YOUTH              U15 / U12 girls' teams                       ← existing at Blue Whale
├── 05 PROGRAMS           Community and school outreach, football festival, Blue Whale Cup
├── 07 NEWS
├── 08 CULTURE + SHOP
├── 09 PARTNERS           ← must be zoned separately from Taichung Rock, never mixed
├── 10 JOIN / CONTACT
├── 12 FAQ
├── 13 SCHEDULE           Mulan League, cups, international invitationals   ← the core
└── MEMBER Member Centre
```

| Main-site section | Blue Whale site | Notes |
|---|---|---|
| **06 WOMEN'S FOOTBALL** | **Not built** | **Self-referential** — this entire site is women's football. The main site's 06 is the entry point to it; this site does not need one of its own |
| **11 CHARITY & IMPACT** | **Not built** | Organised and collected for by the Taiwan Football Strategic Development Association; no bearing on Blue Whale |
| 04 ACADEMY | **Becomes YOUTH** | Blue Whale's existing structure is U15 and U12 girls' teams, not Taichung Rock's academy system. **The admissions and programme-registration architecture is not carried over** |
| 09 PARTNERS | **Must be zoned** | The two clubs' sponsorship contracts are signed separately and **must never be mixed** (main-site specification §5.1) |

> **The section numbers deliberately preserve the main site's mapping** (skipping 06 and 11) rather than renumbering to 01–11. Because the two sites share one admin, **consistent section numbers substantially reduce mistakes** when staff switch between sites in the same interface.

### 2.2 URL rules

Follows the main site: URLs mirror the site hierarchy, language prefixes `/zh/` and `/en/`, `hreflang` including `x-default`.

| Path | Notes |
|---|---|
| `/{lang}/team/` | First-team squad |
| `/{lang}/team/players/{slug}/` | Player page |
| `/{lang}/schedule/` | Fixtures |
| **`/{lang}/schedule/bw1/`** | **Blue Whale first-team fixture category and ICS subscription source** |
| `/{lang}/news/{slug}/` | News |
| `/{lang}/shop/` | Shop |
| `/{lang}/member/` | Member Centre |

> ⚠️ **The `bw1` code comes from `Team.code`, which is globally unique** (not a "club × code" composite). This is an existing hard constraint — calendar subscription URLs are already in public circulation; see main-site specification 4.3 C1.

### 2.3 Links between this site, the main site and the app

| Direction | Approach |
|---|---|
| Main site → here | Main-site section 06 is the entry page; the main navigation and footer keep a "Women's Football" link |
| Here → main site | The footer notes that Taichung Rock FC is this site's system and collection partner; checkout names the collecting party |
| **App → here** | **Deep links to Blue Whale content in the app fall back to this site.** `tcrfc://schedule/bw1`, `tcrfc://match/{Blue Whale match}` and `tcrfc://player/{Blue Whale player}` all need a corresponding page |

> 🔴 **Prerequisite for Universal Links**
> Universal Links and App Links **can only be bound to a domain you own**. This site's domain must ① be held by a controllable party who can manage its DNS and ② serve `.well-known/apple-app-site-association` and `.well-known/assetlinks.json` from its root (with the same Team ID and package name as the main site — it is one app).
> **If control of the domain rests elsewhere, Universal Links for Blue Whale content must not ship** and the custom scheme with a web fallback must be used instead. See app specification §2.3.

---

## 3. Public Site

> **This section covers only what differs from the main site.** Anything not mentioned follows the identically named section of the main-site specification, chapter 3.

### 3.1 【01】Home

Follows the main site's block structure and templates, with Blue Whale content. The four pillars become **First Team / Youth / Outreach / Fixtures**.

**Excluded**: the main site's women's-football referral card (this site *is* women's football) and the charity block.

### 3.2 【02】About

| Block | Content |
|---|---|
| History | **Founded 12 April 2014**, under the Taichung City Women's Football Association |
| Licences | **CTFA domestic league licence in 2019**, **AFC club licence in 2023** |
| Name and identity | What "Blue Whale" signifies, crest history (the second-generation logo has been in use since 2016), the mascot |
| Organisation | The manager's message, staff |
| Home grounds | Taichung Beitun Taiyuan Football Field, Taichung Fengyuan Stadium |

### 3.3 【03】First Team

Maps to `Team.code = BW1`. Follows the main site's player cards, player pages and player-story templates.

**Player photographs require likeness consent** (with a guardian's consent for minors, as on the main site). **Players without consent are shown without a photograph**, using a default graphic or a text-only card — **never a substitute image**.

### 3.4 【04】Youth

U15 and U12 girls' teams. **Follows the main site's squad templates but not its admissions and programme-registration architecture** — Blue Whale's youth teams and Taichung Rock's academy are different organisational forms, and how admissions work is to be confirmed (§10).

### 3.5 【05】Programs

Maps to Blue Whale's existing outreach section: community and school outreach, the football festival, the Blue Whale Cup. Follows the main site's event templates; **whether online registration and payment are offered is to be confirmed** (§10).

### 3.6 【09】Partners and Sponsors

Follows the main site's logo wall and sponsorship-package templates.

> 🔴 **The two clubs' sponsors must be shown in separate zones and never mixed.** The contracts are signed separately — showing a Taichung Rock sponsor on the Blue Whale site is unauthorised use of that company's mark and undermines the exclusivity the sponsorship was sold on.
> Where one company sponsors both clubs, it is **two `Sponsor` records** in the system (following the existing "one record each for each commercial counterparty" rule).

### 3.7 【13】Schedule

**The single most important section of this site.** Follows the main site's section 13 in full (fixtures/results toggle, list and calendar views, per-match `.ics` download, per-team subscription URLs).

| Item | Notes |
|---|---|
| Competitions | Taiwan Mulan Football League, President's Cup, international invitationals, AFC competitions |
| Team categories | `BW1` (first team), U15, U12 |
| Subscription | `/{lang}/schedule/bw1/` offers a webcal subscription |
| Competition series | Expressed with the `Competition` type (main-site specification §5.1) — a four-value enum cannot name the Mulan League and each individual cup |

**All fixture data is maintained by hand**, with no external API integration and CSV bulk import available (as on the main site).

### 3.8 Remaining sections

`07 News`, `08 Culture`, `10 Join / Contact` and `12 FAQ` follow the identically named main-site sections in function and template, with Blue Whale content.

**The privacy notice on 10 Join / Contact must name this site's actual collecting entity** (maintained in the admin under `I` Site Settings, never hard-coded into a template), with its own inbox rather than the main site's.

---

## 4. Admin and Data Scope

### 4.1 No new admin modules

Blue Whale maintains existing types (`Page` / `Article` / `Team` / `Player` / `Staff` / `Match` / `Program` / `Product` / `Order`…) through the main site's existing modules. **What is needed is not a new module but data scope** — one module, different accounts seeing different subsets.

A new module would create a second place to maintain players and products — two points of maintenance.

### 4.2 Site switcher

Staff sign in with **one account through one entry point** and pick the club from a site switcher at the top of the admin. See main-site specification 4.0.

⚠️ **The switcher is a convenience, not a security boundary.** Data scope must be enforced at the data-access layer.

### 4.3 Boundaries of Blue Whale's admin accounts

Uses the main site's tenth role, **Partner club manager** (main-site specification §6). In summary:

| May | May not |
|---|---|
| Create, edit and publish their own articles and pages | Reach **any** data belonging to the other club |
| Maintain their own teams, players, staff and fixtures | See unmasked `Member` master records (**always masked**) |
| Maintain their own programmes, sessions and registrations | Send pushes (M3) or touch advertising (E4–E6) |
| Maintain their own partners and sponsors | App releases and certificates (M1 / M5) |
| View and handle their own memberships, payments, jerseys and orders | System administration and authorisation (J, including J4) — otherwise they could escalate their own privileges |
| — | Execute refunds (S5) or hold shop credentials (S6) — the collecting entity is the club |

**Shared content (`club_id` null) is read-only to this role.** Authorisations carry expiry dates and lapse automatically.

> **This role need not have any users in the first phase**: if Blue Whale's content is maintained by the existing team under their own accounts, no partner-club account is needed.
> **But data scope still has to be built** — `club_id` reaches roughly 40 tables and every admin list query has to decide whether to filter. It is **foundation work**; adding it later means rewriting the query layer (see main-site specification §9). **Define the role now, assign nobody to it — it does not block launch.**

---

## 5. Membership and Shop

### 5.1 A Blue Whale membership is its own membership

Follows main-site specification 3.14 in full (two tiers, season-based terms, the benefits comparison table, the digital card), except:

| Item | Notes |
|---|---|
| Membership | **Its own record** (`Membership`, `club_id = TCBW`). A member may buy only Blue Whale, only Taichung Rock, or both |
| Account | **Shared with the main site** — one account per person, email as the login key. An existing Taichung Rock member does not register again to add a Blue Whale membership |
| Card | **A Blue Whale membership has its own card**, carrying Blue Whale's logo and brand colours. "One card, one token" and "no applicable-team field on the verification page" both **stand unchanged** |
| Season | **Not aligned with Taichung Rock's** (the Mulan League and the TFPL run to different calendars). Expiry and renewal reminders are calculated separately |
| Plans and pricing | Set by Blue Whale (`MembershipPlan.club_id = TCBW`), independent of Taichung Rock's |
| Draws | Run by Blue Whale (`MemberDraw.club_id = TCBW`). ⚠️ The rules must state that **holding both clubs' memberships allows entry to both draws** |
| Partner stores | Per the store's configured applicability — Blue Whale only, Taichung Rock only, or both |

### 5.2 Collection is collect-and-remit

| Item | Approach |
|---|---|
| Collecting party | **Taichung Rock FC** (one LINE Pay merchant account) |
| Invoicing | **Issued under Taichung Rock FC's title** (one invoice track) |
| Basis for settlement | An order's `selling_club_id`; a membership payment's `club_id` (beneficiary) and `collecting_club_id` (collecting entity) |
| Revenue split | **Offline contract.** The system only aggregates and exports by beneficiary; **it builds no settlement statements and calculates no payables** |

> **The public site must state it plainly**: checkout and membership payment on the Blue Whale site must say both "you are buying Taichung Blue Whale merchandise / membership" and "payment and the invoice are handled by Taichung Rock FC". Leaving it out invites a "who am I paying?" dispute and makes the invoice title look wrong.

> ⚠️ **Whether orders must be split by club at checkout is not yet settled** (§10). The field design here (`selling_club_id` value-copied onto `OrderItem`) **can carry either answer**, but **it must be settled before development** — splitting or not changes the checkout flow, the shipment documents and how return credit notes are handled.

### 5.3 Shop

Follows main-site section 8.3 and the `S` admin module in full. Products, inventory, carts, orders, fulfilment and returns on the Blue Whale site are **all partitioned by `club_id`**.

**Carts must not mix clubs** — switching site switches cart. The four reasons are in main-site specification 4.13 (shipping has no defined answer, fulfilment is separate, returns and credit notes must be split, cross-domain sessions). The cost is that a member wanting both clubs' merchandise places two orders and pays shipping twice; this must be explained wherever the two sites link to each other.

---

## 6. Data Model

**This site introduces no new data types.** It uses the types in main-site specification §5, partitioned by `club_id = TCBW`.

Records that must be created:

| Type | Content |
|---|---|
| `Club` | One record: `code = TCBW`, `is_payment_subject = false` (collection uses the main site's single configuration), this site's domain, Blue Whale's brand colours and logos. Invoice-title and tax-ID fields are filled with the actual values |
| `Team` | `BW1` (first team, `gender = women`, `type = first_team`), U15, U12 |
| `Season` | Blue Whale's own seasons (not aligned with Taichung Rock's) |
| `Competition` | Taiwan Mulan Football League, President's Cup, international invitationals, AFC competitions |
| `Player` / `Staff` | First-team and youth squads (**likeness consent required**) |
| `MembershipPlan` | Blue Whale's membership plans |

> **The criteria for `club_id`, the meaning of a null value, and the affected unique keys** are all in main-site specification **5.4** and are not repeated here.

---

## 7. SEO and Languages

Follows main-site specification chapter 7. Three points specific to this site:

1. **Both languages launch together** (`/zh/` and `/en/`). The existing Blue Whale site is Chinese only, and an English edition materially helps visibility for an AFC-licensed club playing international invitationals. **Blue Whale must supply the official English name and the full English copy.**
2. **Canonical attribution for shared content**: articles with a null `club_id` appear on both sites. **One site must own the canonical URL** (the recommendation is the main site, with this site linking across), or two canonicals amount to duplicate content. To be confirmed (§10).
3. **301 redirects from the old site**: the existing Google Sites sections must be mapped one by one to preserve search equity. Its URLs contain Chinese-language paths, so the mapping has to be compiled by hand.

---

## 8. Visual Identity and Brand

### 8.1 Reuse the main site's templates; replace only the brand variables

The main site's front end concentrates all colour in one set of design tokens, and its layout components (cards, clipped corners, oversized numerals, colour bands) are brand-agnostic. **This site reuses the same templates and components and replaces only the brand variables**: the primary colour, an AA-safe variant, a hover-brightened variant, a solid-button hover variant, and three dark neutrals.

> **Contrast is a hard requirement**: the AA-safe variant and the solid-button hover variant must be verified at **4.5:1**. Blue Whale's primary colour is in the blue range and its lightness distribution differs from Taichung Rock's magenta, so **the same tonal steps must not simply be reused** — they have to be recalculated.

### 8.2 Brand assets

| Asset | Status |
|---|---|
| Vector logo (with a dark variant) | 🔴 **Not supplied** |
| High-density raster @2x / @3x | 🔴 **Not supplied** (needed for the app's membership card face) |
| Brand colours (colour values) | 🔴 **Not supplied** |
| Official English name | 🔴 **Unconfirmed** (the existing site says `Taichung Blue Whale`; whether that is the formal full name needs confirming) |
| favicon / OG image | 🔴 **Not supplied** |

> 🔴 **The logo must be a vector master** (`.ai` / `.svg` / `.eps`). **Never draw a substitute mark, never trace one from a website screenshot, never set the name in type yourself, and never scale up a raster and pass it off as vector.**
> If only a low-resolution raster is available, **go back for the original design file or have it remade** rather than making do — the second-generation logo has been in use since 2016, so an original should exist.
> Until it arrives the corresponding areas **are not rendered** — **no placeholder imagery and no empty logo box.**
> This matches the main site's existing rule (its `brand/` marks are all extracted from the master `.ai` file and never set by hand).

---

## 9. Delivery Phases

> **This site depends on the main site's Phase 1 multi-club foundation** (the `Club` type, the `club_id` dimension, the site switcher, `AdminUserClub` data scope). **Work cannot begin until that foundation is complete.**

| Phase | Content |
|---|---|
| **Prerequisites** | The domain, brand vector masters, squad and coaching-staff data (with likeness consent), and twelve months of fixtures |
| **Phase B1** | Copy the front-end skeleton and swap the brand variables; 01 Home, 02 About, 03 First Team, 07 News, 13 Schedule (**these five are precisely the app's deep-link fallback targets, so they come first**) |
| **Phase B2** | 04 Youth, 05 Programs, 09 Partners, 10 Join / Contact, 12 FAQ; English content |
| **Phase B3** | MEMBER Member Centre and Blue Whale membership; 08 Culture |
| **Phase B4** | The on-site shop (**depends on the tax determination in §5.2**) |

> **On effort**: the front end can copy the main site's skeleton and styling system, so **development effort is far lower than building from scratch**; **the main cost is content production** — history, player biographies, fixture data, translation. Those are not engineering problems.

---

## 10. Open Items

> **This document covers only what bears directly on building the site.** Legal-entity ownership, authorisation documents, data-processing agreements and accounting treatment are the client's administrative and legal matters and are **out of scope here**.

**Blocking — the corresponding scope must not be built until resolved**

| # | Item | Blocks |
|---|---|---|
| 1 | **The domain**: name, who owns it, who manages DNS. ⚠️ **App deep links require a controllable domain** (§2.3) | Launch and app deep links |
| 2 | **Blue Whale brand assets**: vector logo master (with a dark variant), @2x / @3x raster, primary and secondary brand colour values, favicon, OG image, **official English name** | Visual sign-off and every page (§8.2) |
| 3 | **Player and coaching-staff data**: names (zh/en), numbers, positions, photographs, biographies, **likeness consent** | 03 First Team, 04 Youth (§3.3) |
| 4 | **Twelve months of fixture data**: Mulan League, cups and international invitationals — dates, opponents, venues, home or away | 13 Schedule (§3.7) |
| 5 | **Whether orders must be split by club at checkout** | The shop's checkout flow, shipment documents and return credit notes (§5.2) |

**Functionality and scope**

| # | Item |
|---|---|
| 6 | **Full English copy**: the existing site is Chinese only. Who translates? Who supplies English player biographies, history and event descriptions? |
| 7 | **Do the youth teams recruit?** Should U15 / U12 accept online applications, and how does that differ from the Taichung Rock academy? |
| 8 | **Do outreach programmes take online registration and payment?** The Blue Whale Cup, the football festival, and so on |
| 9 | **Blue Whale's membership plans and season dates**: price, `card_quota`, `jersey_quota`, mid-season pricing, season code and dates |
| 10 | **Fulfilment staffing and stock location for the Blue Whale shop**: who picks and packs, and where is the stock held? **This decides whether the shop opens at all, and what delivery estimate the site can promise** |
| 11 | **First product range and stock levels** |
| 12 | **Canonical attribution for shared content**: articles with a null `club_id` appear on both sites — which owns the canonical URL? The recommendation is the main site, with this site linking across |
| 13 | **Does this site need draws (K5), the comic (F) or partner stores (K4)?** This affects scope estimation and pricing |
| 14 | **Do the five core values apply to Blue Whale?** The main site's `ValueTagLink` is Taichung Rock's brand vocabulary; does Blue Whale adopt it or define its own? |
| 15 | **The 301 redirect map from the old Google Sites site**: ten sections with Chinese-language URLs, to be compiled by hand |
| 16 | **Which social accounts to present**: Facebook, Instagram `tcbw2014`, YouTube, the LINE official account and the existing email address — are they all retained and shown in the footer? |
| 17 | **The actual invoice title and tax ID**: the `Club` record's invoicing fields need the correct values |

---

*This specification may be adjusted and extended as requirements evolve. Where it conflicts with [`TCRFC_Website_Functional_Specification_EN.md`](TCRFC_Website_Functional_Specification_EN.md), the main-site specification prevails.*
