# TCRFC — Official Website Functional Specification (Public Site & Admin CMS)

> **Document version**: v2.6
> **Date**: 2026-08-14 (v2.6 revision: 2026-09-04)
> **Brand promise**: LOCAL ROOTS. GLOBAL PATHWAYS.
> **Note**: This is the English edition of *TCRFC 前後台功能規劃書 v2.6*. Section numbering matches the Traditional Chinese edition 1:1.

> **v2.6 revision summary — e-commerce is brought in-house; "no payment gateway" is scoped a third time**
> 1. **This site now runs its own shop, at the same functional scale as the old site's existing store**: 8.3 is rewritten from "Shopify referral showcase" into an **on-site official store** — product catalogue (with categories and size/colour filters), product detail (size/colour SKUs, list and sale price, size chart), cart, checkout, **payment by LINE Pay**, **e-invoicing**, fulfilment, order lookup, and returns, **collected for by this club**. **Deliberately nothing more elaborate than the old store** (see point 7). The "e-commerce and payments" bullet is **removed** from the out-of-scope list in 1.3 and replaced by a statement of the shop's boundaries.
> 2. **A new `S. Shop` admin module (S1–S6)**: S1 products & SKUs / S2 inventory / S3 orders / S4 fulfilment & shipping / S5 returns & refunds / S6 shop settings & reports, defined in 4.13. **The letter `S` (Shop) is used** — `D` was retired because it collides with the team code `D1`, `N` belongs to the Charity Donation Platform, and `M` belongs to the Mobile App.
> 3. **`E4 Product Showcase` is folded into `S1`** and the `ProductShowcase` type is retired (display fields and an outbound link cannot carry SKUs, inventory, or orders). **The `E4` identifier is retired and will not be reused**, so that products have exactly one place of maintenance.
> 4. **"No payment gateway" is scoped a third time; the boundary now divides four ways**: ① **shop checkout on this website takes payments, and the only payment method is LINE Pay** (collected by this club); ② **membership fees still do not go through on-site checkout** (the website keeps payment links and in-person collection; the app uses LINE Pay), with a possible move to shop checkout listed as an open item in section 10; ③ the Charity Donation Platform integrates LINE Pay (collected for by the **Association**); ④ the Mobile App's membership payment (collected by **this club**). **"No card data" remains globally applicable** — payment always completes on LINE Pay's side and this site renders no card fields. **All four payment flows use LINE Pay, but the collecting entities differ, so merchant accounts and invoice tracks must never be shared.**
> 5. **Every checkout issues an e-invoice**: **carrier, tax ID, or donation code**, made out to **this club**, on a **different invoice track from the Association's** charity invoices; voided or credit-noted on a return. **LINE Pay does not issue invoices itself**, so a separate invoicing service is required (an open item in section 10).
> 6. **New data types**: `Product`, `ProductVariant`, `InventoryMovement`, `Cart`, `Order`, `OrderItem`, `Shipment`, `RefundRequest`, `StoreInvoice`. `Order.member_id` **may be empty** — **guest checkout is supported**, mirroring `Registration`; `OrderItem` is a **value-copied snapshot**, so renaming or repricing a product must never alter historical orders. The Member Centre gains **"My orders"**; the v2.0 premise "no SSO with Shopify" **lapses with the external store**.
> 7. **Deliberately not built (keeping to the old store's scale rather than building a large shop in version one)**: **multiple payment methods** (no card, convenience-store code, ATM transfer, or cash on delivery — **LINE Pay only**), **member pricing and discount codes** (products carry only a list price and a sale price; paid membership keeps partner-store discounts and the jersey and **does not include merchandise discounts**), a **shipping-rate engine** (a single flat rate plus a free-shipping threshold only), subscriptions and recurring billing, cross-border sales and multi-currency, marketplace/multi-vendor, resale or consignment, points redemption and e-wallet, **ticketing and match packages**, tying merchandise to membership or draw eligibility, carrier API integration (tracking numbers entered manually or by CSV), and product reviews or ratings.
> 8. **The old site's Wix store becomes a transitional channel**: it keeps selling until the on-site shop launches, then closes, with `/product-page/…` and `/category/…` 301-redirected to their on-site equivalents. **Shopify is no longer the referral target.**

> **v2.5 revision summary — a Mobile App specification is added; four assumptions are scoped accordingly**
> 1. **A third specification is added**, [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) (v1.0): the official iOS/Android app, **sharing this site's admin and database**, adding an `M. Mobile App` admin module and extending the `E` commercial module with E5–E7 (advertising). The three specifications divide as follows — this site is the content and SEO body, the Charity Donation Platform is the Association's separate collection site, and the Mobile App is the pocket interface for membership and fixtures.
> 2. **"No separate fixed slots" is scoped to this site's public web front end**: the Mobile App sells **its own advertising slots** (slots, flights, rotation weights, impression and click measurement, advertiser reporting). This is app scope and **does not change this site's homepage composition**. One exception stands unchanged: **there is no fixed charity slot on the homepage, and the app does not carry one either** — the Charity Donation Platform is run and collected for by the Association, so this club's app must not sell or grant charity-related placements.
> 3. **"No payment gateway" is scoped a second time**: section 3.14 previously stated that LINE Pay API checkout was out of scope and that adopting it "would require amending the assumption in section 10". **This revision performs exactly that amendment** — **in-app membership payment via LINE Pay is now confirmed** (membership fees only; not course fees, merchandise, or donations), with **this club** as the collecting entity, using **the club's own LINE Pay merchant account, never the Association's**. This site's public web front end keeps payment links and in-person collection, and **"no card data" remains globally applicable**. `POST /api/membership/activate` is promoted from "reserved, not enabled" to "triggered by the app's successful-payment callback" — delivering exactly what its original design promised.
> 4. **"No push notifications or notification centre" is scoped to LINE push and on-site web messaging**: the Mobile App provides **native push (APNs/FCM) and an in-app notification centre**. This site's public web front end still has no notification centre, no notification preferences, and no LINE push. **The five system emails are unchanged.** **The prize-draw commitments are untouched**: winners are still announced through masked News posts only, and **app push must never be used for individual winner notifications**.
> 5. **"No booking attribution or form pre-filling" is partially lifted, for the app only**: the Mobile App provides **form pre-filling** and **"my bookings"** (the member's own data only). `Registration` gains a nullable `member_id`, and the admin P3 booking list gains a "member" column. This site's public web front end still does neither, and **parent–student linking remains excluded across the website, the app, and the admin**. This is the only substantive functional change in v2.5; the other three are scope qualifications.
> 6. **Data-model completion and extensions**: `Sponsor` / `SponsorPackage` / `Partner` are promoted from one-line descriptions to field tables (tidying only, no new functionality); `PartnerStore` and `Venue` gain geographic coordinates (for the app's nearby-store and venue navigation); `Match` gains `opponent_en` / `venue_en`, and `competition` / `status` are promoted to formal fields. App-specific types are defined in the Mobile App specification, section 10.

> **v2.4 revision summary — the Charity Donation Platform is run by the Association, not this club**
> 1. From v1.4 of its own specification, the Charity Donation Platform is **organised, fundraised and collected for by 台灣足球策略發展協會** (the Association), not by Taichung Rock FC. This site is only a **traffic source** and the **admin host**.
> 2. **The section 11 "fan donation" CTA must say where it leads**: it goes to the Association's donation platform, and the copy must make clear who receives the money. **It must not read as donating to this club.**
> 3. This site's scope, page architecture and data model are **unchanged**; 11.1–11.4 and the `CharityProgram` linkage stay exactly as they are.
> 4. In section 10, "donation receipts and fundraising eligibility" now turns on the Association rather than the club.

> **v2.3 revision summary — prize draw entitlement for paying members**
> 1. **The "Fan Club Prize Draw" is added as a paid-membership benefit**: at a draw's **eligibility snapshot time**, every paying member (`fan_club`) with a valid membership is **automatically included in the eligible roster — the member does nothing at all**: no entry, no sign-up, no points, no accumulation. One entry is added to the benefits comparison table under the existing "events" group (**no new group**), maintained as before in K4.
> 2. **The system does not draw winners**: the physical draw is **performed by people, on site or on a live stream**, and what is drawn is the **draw serial number** the system issued. The system only **freezes the roster into a snapshot** at the snapshot time, issues consecutive serial numbers, and exports a CSV for use at the event; winners are then **ticked in manually** in the admin.
> 3. **New admin module `K5 Prize draw rosters`**, placed under K Members (the roster comes from membership, it is maintained by the same support/administration staff, and prize fulfilment mirrors K3). **This `K5` is a recycled identifier**: v2.0 narrowed module K from K1–K5 to K1–K4, freeing it; it has no relationship to the former K5.
> 4. **Two new data types: `MemberDraw` (draw) and `DrawRoster` (eligible roster snapshot)**. `DrawRoster` is an **immutable snapshot**: it copies the name, member number, tier, and membership expiry as values at snapshot time, cannot be added to or deleted from once locked, and can only be voided and regenerated in full if wrong; a **roster hash** is stored for audit. It is deliberately not named `Ticket` (which would blur into the excluded ticketing scope) or `Entry` (which would imply members must enter).
> 5. **Winners are announced through News only** (category 7.1 Club News plus a new `Fan Club Prize Draw` tag — **no ninth news category**), always masked to "serial number + member number + masked name". **No winner notification email** (the five system emails are unchanged), no on-site notification, no LINE push. The public site has **no** draw page, no "my draws", no serial-number lookup, no winners list page, and no online draw animation.
> 6. **Tax and personal data follow data minimisation**: a winner's national ID number is collected only where a single prize reaches the withholding threshold (encrypted, masked, audited, destroyed at end of retention), and **the first draws' prize values are recommended to sit below the threshold so nothing need be collected at all**; the member terms must add a collection notice for the draw. Section 10 gains open items 16–20.

> **v2.2 revision summary — English club name corrected**
> 1. The club's English name is corrected from `Taichung Rocks FC` to **`Taichung Rock FC`** (long form `TAICHUNG ROCK FOOTBALL CLUB`).
> 2. This revision changes wording only — scope, data model and page architecture are unchanged. All documents, the mockup and the public-site skeleton have been updated; any remaining `Rocks` spelling is an error.

> **v2.1 revision summary — donations move to a separate platform**
> 1. **A separate Charity Donation Platform is introduced**, specified in its own document, [`TCRFC_Charity_Donation_Platform_Specification_EN.md`](TCRFC_Charity_Donation_Platform_Specification_EN.md): its own domain and its own public-site project, **sharing this site's admin and database**. Partner venues' QR codes are the entry point; it integrates LINE Pay and issues e-invoices or donation receipts.
> 2. **Section 11 on this site narrows to editorial content and referral**: 11.1 commitment, 11.2 programmes, 11.3 impact stories and 11.4 impact metrics are unchanged; **all on-site donation mechanisms (Shopify donation item, bank transfer, in-kind donation, the transfer report form) are removed**, and fan donation links out to the Charity Donation Platform.
> 3. **Volunteer signup is out of scope**: the section 11 CTA narrows from three routes to two (corporate partnership / fan donation), the G2 inbox no longer has a volunteer tab, and the `Enquiry` type drops the volunteer category.
> 4. **"No payment gateway" now applies to this site only**: the Charity Donation Platform integrates the LINE Pay Online API properly, within that project's scope. This site still builds no cart, integrates no gateway, and manages no orders or inventory.
> 5. **The donation receipt question is answered**: the former open item "whether donation receipts are required" is now specified in Charity Donation Platform §5 (both e-invoices and donation receipts, chosen per project); fundraising eligibility is elevated to a **launch precondition** for that platform.
> 6. The `Donation` type's definition moves to Charity Donation Platform §9; this site no longer creates donation records.

> **v2.0 revision summary — member system scope narrowed**
> 1. **The member system now carries membership alone**: a free tier and a paid tier, with **partner-store discounts** for members and a **jersey** for paying members. The earlier positioning — connecting fan club, program registration, event signup, and newsletter — is withdrawn.
> 2. **New section 8.4 Partner Perks** (a public page) plus admin module K4. This is an entirely new content type, `PartnerStore`, which shares no data with 9.1 Partners (the B2B logo wall).
> 3. **A paying member is a fan club member**, not a separate identity; page 8.2 becomes the introduction and join page for paid membership. The former "Parent" tier is removed.
> 4. **The benefits comparison table is now a stated requirement**: structured data, maintained in the admin, visible without signing in, and **never delivered as images**; one dataset shared by the join page, 8.2, and the upgrade page.
> 5. **Membership runs by season**, with everyone expiring together; individual and family plans are supported via `card_quota` / `jersey_quota`.
> 6. **Fees are collected via LINE Pay payment links and in person**; the website takes no payments, and the admin activates membership after reconciliation. An automation hook is reserved.
> 7. **Moved out of scope**: loyalty points, e-wallet, ticketing and match packages, **store scan-to-redeem and redemption reporting**, Shopify SSO, booking attribution and form pre-filling (**from v2.5 provided in the Mobile App; this site still does neither**), parent–student linking (**still excluded everywhere**), on-site notification centre, LINE push and parameterised QR source tracking, Google sign-in, member calendar and "I'm attending".
> 8. Admin module K goes from K1–K5 to **K1 Member list / K2 Membership and plans / K3 Jersey fulfilment / K4 Partner stores and benefits**. The data model gains `MembershipPlan`, `MembershipPayment`, `MembershipBenefit`, `PartnerStore`, and `EmailLog`, and drops `StudentLink`, `LineEntryCode`, `Notification`, and `EventInterest`.

> **v1.9 revision summary**
> 1. The club mark must always be one of the three lockups extracted from `reference/TCR_logo_CMYK.ai` (mark / mark + TCRFC "English" lockup / mark + TCRFC + 台中磐石足球俱樂部 "Chinese" lockup). **Never set the club name in type alongside it, and never add "SINCE" or a founding year.** The public-site header carries the mark alone; the footer carries the full Chinese lockup.
> 2. The Chinese short name is always **台中磐石**, never 磐石 on its own.
> 3. The first-team squad list is **ordered by squad number, with no position grouping or filter**.
> 4. **The homepage stat strip is removed entirely** (the strips on the 03 Football Club landing page and 11.4 Our Impact remain).
> 5. Section 10 forms merged from nine to seven (10.2 + 10.3, and 10.6 + 10.7); numbering closes up.
> 6. The club's English name is `Taichung Rock FC` throughout (the Brand Deck footer's `TAICHUNG ROCK FOOTBALL CLUB` is the long form of the same name). Every previous use of `Taichung Cornerstone RFC` has been replaced.

---

## Table of Contents

1. [Objectives & Scope](#1-objectives--scope)
2. [Site Architecture Overview](#2-site-architecture-overview)
3. [Public Site Functional Specification](#3-public-site-functional-specification)
4. [Admin CMS Functional Specification](#4-admin-cms-functional-specification)
5. [Data Model & Content Types](#5-data-model--content-types)
6. [Roles & Permissions Matrix](#6-roles--permissions-matrix)
7. [SEO / GEO & Multilingual Plan](#7-seo--geo--multilingual-plan)
8. [Non-Functional Requirements](#8-non-functional-requirements)
9. [Delivery Phases & Priorities](#9-delivery-phases--priorities)
10. [Open Items](#10-open-items)

---

## 1. Objectives & Scope

### 1.1 Business objectives

| Objective | Corresponding website capability |
|---|---|
| Build a world-class football-ecosystem website | Full content architecture across the four pillars: First Team / Academy / Women's / Programs |
| Grow brand influence | News & stories system, TCRFC Culture (manga), Fan Club, official merchandise |
| Attract talent (players, coaches, students) | Recruitment / trial / registration conversion funnels, International Pathways |
| Expand international partnerships and sponsorship | Partner zone, sponsorship packages, deck download, B2B enquiry forms |

### 1.2 Five core values (threaded through site-wide visuals and content tagging)

`Players First`, `Excellence`, `Global Pathways`, `Community`, `Integrity`

> Recommendation: implement the core values as **content tags (Value Tags)** in the admin, applicable to any article / player story / event, so the public site can aggregate content by value.

### 1.3 System scope

- **Public site**: 13 top-level sections, ~60+ sub-pages, 7 CTA conversion forms, a **Member Centre and partner store directory**, an **on-site official store (catalogue / cart / checkout / order lookup)**, and **two languages (Traditional Chinese / English)**.
- **Admin CMS**: content management, teams & fixtures, registrations & rosters, **member management**, **shop and order management**, FAQ management, charity impact records, enquiry inbox, partners & sponsorship, SEO & site settings, permissions & audit.
- **Out of scope for this engagement**:
  - **Boundaries of the shop (from v2.6 e-commerce is in scope, so this is no longer an exclusion)** — this site runs its own **official store**: products, cart, checkout, **payment by LINE Pay**, **e-invoicing**, shipping, orders, and returns are all handled here, **collected for by this club** (see 3.8 and 4.13). **Kept to the same scale as the old site's store; excluded**: multiple payment methods (**LINE Pay only**), member pricing and discount codes, a shipping-rate engine, subscriptions and recurring billing, cross-border sales and multi-currency, marketplace/multi-vendor, resale or consignment, points redemption and e-wallet, and tying merchandise to membership or draw eligibility; **ticketing and match packages remain excluded**. **"No card data" applies globally** — payment completes on LINE Pay's side and this site never renders its own card fields. The other collecting entities stay separate: charity donation payments are handled by the separate Charity Donation Platform (collected for by the **Association**), see [`TCRFC_Charity_Donation_Platform_Specification_EN.md`](TCRFC_Charity_Donation_Platform_Specification_EN.md); **in-app membership payment uses LINE Pay** (collected by this club, membership fees only), see [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) section 5. **Membership fees do not go through shop checkout** and keep payment links and in-person collection.
  - **Technology selection** — this document defines functional requirements only; it does not decide framework, CMS, or hosting.
  - **Fixed advertising slots** — **this site's public web front end** has no separate fixed slots; seasonal content is surfaced through the hero carousel or latest news (see 3.1 / 4.2 B4). **The Mobile App's own advertising slots are app scope**, see [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) section 7.
  - **Native push and notification centre** — this site builds no notification centre, no notification preferences, and no LINE push; **the Mobile App's native push and in-app notification centre are app scope**, see the app specification, section 6.
  - **Booking attribution and form pre-filling** — not built on this site's public web front end; **the Mobile App provides form pre-filling and "my bookings"** (the member's own data only), see the app specification, section 3.9. **Parent–student linking is excluded everywhere.**
  - **Ticketing and match packages**, **loyalty points**, **e-wallet**, and **partner-store scan-to-redeem with redemption reporting** (recommended for later evaluation). The paying-member prize draw (3.14 / admin K5) is **none of these**: eligibility is a boolean derived from membership, the draw serial number is not a ticket, and prizes exclude tickets — see 3.14.
  - **On-site donations and volunteer signup** — fan donations move to the Charity Donation Platform (separate domain, see above); **volunteer signup is not built**, and any such need is handled by the general contact form (10.7).

### 1.4 Existing digital assets

The following assets must be inventoried before launch to determine migration scope and post-launch traffic routing:

| Asset | URL | Treatment |
|---|---|---|
| Current official website | [www.tcrfc.tw](https://www.tcrfc.tw/) | **Content migration source**: existing copy, news and images to be inventoried and imported; set 301 redirects from old URLs to their new counterparts at launch to preserve SEO equity |
| Instagram | [@tcr_fc_2024](https://www.instagram.com/tcr_fc_2024) | Linked in footer and contact page; recent posts may be embedded on the homepage or news pages |
| Facebook | [TCRFC2024](https://www.facebook.com/TCRFC2024) | Linked in footer and contact page; events and news cross-posted |
| YouTube | [@TCRFC-2024](https://www.youtube.com/@TCRFC-2024) | Linked in footer; videos embedded in team pages, match highlights, player stories, manga animations |
| Women's team official website | [Taichung Blue Whale](https://www.tcbw2014.com/) | Outbound target for section 06 Women's Football |
| The old site's Wix store | www.tcrfc.tw `/product-page/…`, `/category/…` | **Transitional channel**: keeps selling until the on-site shop (8.3) launches, then closes; the five product and category URLs are 301-redirected to their on-site equivalents. **Shopify is no longer the referral target from v2.6** |

**Migration principle**: inventory and classify existing content first (keep / rewrite / discard), and migrate only what is still current and of sufficient quality. For news, keeping the last 1–2 years is recommended; older articles are not migrated item by item, but their URLs are still redirected.

---

## 2. Site Architecture Overview

```
01 HOME
├── 02 ABOUT TCRFC                    (2.1 ~ 2.8)
├── 03 FOOTBALL CLUB                  (3.1 ~ 3.5)
├── 04 TCRFC ACADEMY                  (4.1 ~ 4.7)
├── 05 PROGRAMS                       (5.1 ~ 5.5)
├── 06 WOMEN'S FOOTBALL               single introductory page (no roster / fixtures / results)
├── 07 NEWS & STORIES                 (7.1 ~ 7.8)
├── 08 TCRFC CULTURE                  (8.1 ~ 8.4)
├── 09 PARTNERS & SPONSORS            (9.1 ~ 9.4 + 2 CTAs)
├── 10 JOIN / CONTACT                 (10.1 ~ 10.7 + map / contact info)
├── 11 CHARITY & IMPACT               (11.1 ~ 11.4)
├── 12 FAQ                            (standalone section, cross-topic)
├── 13 SCHEDULE                       (fixtures and results by team)
├── SHOP                              (the 8.3 transaction flow, added in v2.6)
│   ├── Product list / product detail
│   ├── Cart /cart, checkout /checkout, confirmation
│   └── Order lookup /order/lookup     (guests: order number + email)
└── MEMBER CENTRE                     (authenticated area, entry point at the right of the header)
    ├── My orders                      (added in v2.6)
    └── Card verification /m/<token>   (public, read-only, for stores to check validity)
```

### 2.1 Page-type taxonomy

| Type | Description | Admin handling |
|---|---|---|
| **Main page** | Homepage | Modular slot composition (page builder) |
| **Main category** | Landing page of each top-level section | Fixed template + editable blocks |
| **Sub-page** | 2.1, 3.1, etc. | Fixed template + editable blocks |
| **Content / item page** | Players, coaches, news, matches, products, manga episodes | Data-driven CRUD (list + detail); **products additionally carry SKUs, inventory, and the transaction flow, see 4.13** |
| **CTA page** | The 10.x forms, sponsorship deck download | Form designer + submission inbox |

### 2.2 Global navigation

- **Primary menu (desktop)**: ABOUT / CLUB / ACADEMY / PROGRAMS / WOMEN'S / SCHEDULE / NEWS / CULTURE / PARTNERS / CHARITY, with a persistent right-hand group: `JOIN` (accent button), **`Cart` (with item count, added in v2.6)**, `Sign in / Member Centre`, and the language switch `繁中 / EN`.
- **Mega menu**: each top-level category expands to show its second level plus one key visual and one primary CTA for that category. Women's Football and Charity are single pages and link directly without expanding.
- **Mobile**: full-screen hamburger drawer (including language switch, member entry, and **the cart**), plus a sticky bottom CTA bar (`Join the Club` / `Contact Us`); **on product detail pages the CTA bar becomes "Add to cart"**.
- **Footer**: four-column sitemap (including FAQ and Charity), social links, sponsor logo carousel, **official store entry with links to the shopping guide and returns policy**, contact details, privacy / cookie policy, language switch.

---

## 3. Public Site Functional Specification

### 3.0 Site-wide functionality

| ID | Feature | Description |
|---|---|---|
| G-01 | Language switching | **Traditional Chinese / English**, separated by URL prefix `/zh/`, `/en/`. Default is Traditional Chinese; untranslated content falls back to Chinese with a notice ("This page is not yet available in this language"). Switching language keeps the user on the corresponding version of the current page. **The architecture must accommodate a third language** (Japanese is deferred for later evaluation) without code changes |
| G-02 | Site-wide search | Across news, players, coaches, programs, FAQ, and charity records; supports keyword highlighting and category filtering |
| G-03 | Breadcrumbs | Mirrors the site hierarchy and emits BreadcrumbList schema |
| G-04 | Responsive layout | Mobile-first; breakpoints at 360 / 768 / 1024 / 1440 |
| G-05 | CTA component library | Reusable conversion blocks (register / enquire / download) that can be appended to any page |
| G-06 | Social sharing | Facebook / Instagram / LINE / X / copy link |
| G-07 | Cookie consent & privacy policy | GDPR-friendly; tracking scripts load only after consent |
| G-08 | Accessibility | WCAG 2.1 AA, keyboard operation, image alt text, contrast checks |
| G-09 | Newsletter signup | Persistent in the footer, integrated with an EDM platform |
| G-10 | 404 / maintenance pages | Branded error pages with links to popular destinations |
| G-11 | Member status bar | Header shows sign-in / register or a member menu (card / membership / sign out); supports one-tap LINE sign-in. No form pre-filling on this site (**the Mobile App provides it**, see the app specification, section 3.9) |
| G-12 | FAQ quick block | Attachable to the bottom of any page, automatically pulling the FAQs for the relevant topic (see 3.12) |
| G-13 | Cart status bar (**added in v2.6**) | The header shows the cart item count and a quick view; guests can add to cart (held under a browser-side token) and the cart is merged into the account on sign-in. Checkout flow: see 8.3 |

---

### 3.1 【01】HOME

**Purpose**: communicate the brand position within three seconds, then route visitors to the four pillars (First Team / Academy / Programs / Women's) and the two commercial entry points (Join / Sponsor).

| Block | Description | Data source |
|---|---|---|
| Hero | Video or image carousel (max 5 slides), supporting headline / subhead / dual CTA | Admin banner management |
| Five core values | Icon + name, linking to 2.3 Our Philosophy | Admin settings |
| Four-pillar navigation cards | Football Club / Academy / Programs / Women's Football | Static module with editable text and imagery |
| Latest match block | Next fixture (with countdown) + most recent result | Fixtures module |
| Upcoming fixtures | Summary of the next 30 days, with `D1 / U15 / U14 / U12` team quick-filter chips, linking to 13 Schedule | Calendar module |
| Latest news | Pulls the newest 3–6 items from 7.x; featured items can be pinned | News module |
| Sponsor logo wall | Carousel ordered by tier, clicking through to 9.1 | Partners module |
| Official store entry | Featured products and collection entry points, linking to the **on-site shop** (8.3; from v2.6 on-site, no longer a new tab) | Shop module S1 |
| Bottom CTA strip | Join the Club / Join the Academy / Become a Partner | CTA component |

> The homepage follows a single line of argument: brand position → match activity → latest news → conversion. Seasonal content (a new manga chapter, a major charity event, an enrolment window) is surfaced through the hero carousel or the latest-news block rather than through dedicated fixed slots — **this applies to this site's public web front end**. The Mobile App's own advertising slots are app scope, see [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) section 7, and do not affect this homepage.

---

### 3.2 【02】ABOUT TCRFC

| No. | Page | Public-site functionality |
|---|---|---|
| 2.1 | Our Story | Long-form editor (rich text + mixed image/text + pull quotes) |
| 2.2 | Vision & Mission | Two-column vision/mission layout supporting icon lists |
| 2.3 | Our Philosophy | Philosophy narrative + expansion of the five core values |
| 2.4 | Our People | Person cards (photo / title / bio), groupable (management / coaching / administration), with modal detail |
| 2.5 | Governance | Org chart, governance principles, downloadable documents (articles of association, annual report PDFs) |
| 2.6 | TCRFC Ecosystem | Interactive ecosystem diagram; clicking a node jumps to the corresponding section |
| 2.7 | Club History | Illustrated historical narrative |
| 2.8 | Key Milestones | **Timeline component** (year anchors, images, event descriptions) with year filtering |

---

### 3.3 【03】FOOTBALL CLUB

#### 3.1 First Team (team code **D1**)

> Throughout the Schedule (13), admin team records, and all filters, this team is identified by the team code **D1**; its public-facing display name remains `First Team`.

| Sub-item | Public-site functionality |
|---|---|
| Team Overview | Team introduction + key visual + headline stats for the season |
| Players | Player card list (squad number / position / nationality / photo), **ordered by squad number, with no position grouping or filter**; clicking opens a **player detail page** (profile, career stats, appearances this season, related news, video) |
| Coaches | Coach card list + detail (coaching history, licences) |
| Fixtures | Grouped by season and competition, showing date, opponent, venue, home/away; sourced from the same data as calendar category `D1`, with "View full schedule" and "Subscribe to D1 fixtures (.ics)" |
| Results & Standings | Results list (score, scorers) + league table (maintained automatically or manually) |
| Achievements | Trophy and honours timeline |

#### 3.2 Player Development

Eight module pages: technical & tactical analysis, physical conditioning, game reading, mental resilience, video analysis, IDP (individual development plan), nutrition & lifestyle, education & languages.

- Presented as a **3×3 card grid with expandable detail**; each module contains an overview, training methods, responsible coach, and links to outcome case studies.

#### 3.3 Player Opportunities

- Join TCRFC: eligibility criteria + routing to form 10.1
- Trials: **trial session list** (date, venue, target group, capacity, registration deadline) + online registration
- Foreign Players: English-first version + routing to form 10.4

#### 3.4 International Pathways

- Pathway overview diagram (local → domestic professional → overseas training → overseas club)
- Regional pages: Europe / Japan / Hong Kong (each with partner organisations, success stories, application process)
- International Partners: logo + country + nature of collaboration
- Trials & Scouting: process explanation + application entry point
- Finding Clubs Abroad: service description + consultation form

#### 3.5 Player Stories / Case Studies

- Case list (filters: Academy / First Team / Overseas / Women's); detail pages use a "before-and-after + timeline + video" narrative template.

---

### 3.4 【04】TCRFC ACADEMY

| No. | Page | Public-site functionality |
|---|---|---|
| 4.1 | Academy Overview | Positioning, training base, headline metrics (students, coaches, progression rate) |
| 4.2 | Our Teams | Tabs for **U15 / U14 / U12 / other age groups**; each team shows roster, coaches, fixtures, results. The "fixtures" block embeds that team's data from the Schedule (13) directly, with a "Subscribe to this team's calendar" button |
| 4.3 | Academy Pathway | Step-ladder pathway diagram (U12 → U15 → First Team / overseas); each stage is clickable for detail |
| 4.4 | Training & Curriculum | Five dimensions (technical / tactical / physical / game reading / character), each with a syllabus and periodisation table |
| 4.5 | Coaches | Coach list + detail (licences, specialisms, assigned age group) |
| 4.6 | Academy Life | Daily training, matches, competitions, educational support, player welfare; image gallery + video |
| 4.7 | Join the Academy | Who it's for, selection process (step bar), trial information, **fees and information table**, online application (routes to 10.2). This page uses the FAQ quick block (G-12) to embed the "Academy admissions" topic automatically, with a "See all FAQs" link (full content in section 12) |

---

### 3.5 【05】PROGRAMS

**Shared mechanism**: every course and camp is a **Program Item** record with sessions, venue, schedule, capacity, fees, and registration status.

| No. | Program | Public-site functionality |
|---|---|---|
| 5.1 | Children's Training | Level descriptions (mixed-age / beginner / skill development), **training venue map**, weekly timetable, online registration |
| 5.2 | Summer Camp | Who it's for, curriculum, coaching staff, activities, partners, **dates and venue**, registration (with early-bird pricing and remaining-place countdown) |
| 5.3 | Winter Camp | Same structure as 5.2 (shared template and data model) |
| 5.4 | Specialist Training | Six specialisms (goalkeeping / forwards / defenders / midfielders / speed & conditioning / advanced), each with objectives, target audience, registration |
| 5.5 | School & Community | School partnership packages, community programmes, coach education; includes a **partner school list** and enquiry form |

**Registration flow (public site)**: choose program → choose session → enter student details (multiple students supported) → parent / emergency contact → health declaration and consent → submit → registration number issued → email / SMS confirmation → (optional) online payment or bank transfer instructions.

---

### 3.6 【06】WOMEN'S FOOTBALL

> **Section positioning**: this section is a **single introductory page**. No roster, fixture list, results, or league table functionality is built here.

| Item | Public-site functionality |
|---|---|
| Page type | Single content page, composed with the block editor |
| Suggested blocks | ① Key visual and headline ② Introduction to the Taichung Blue Whale women's team (history, positioning, significance) ③ Image gallery / video embed ④ A prominent **"Visit the women's team website"** button (opens in a new tab) ⑤ Bottom CTA |
| **Outbound link** | The page's primary action is to route visitors to the **women's team official website** (a separate site), which owns the roster, fixtures, and results. This site provides introduction and referral only |
| Not included | ✗ Roster ✗ Coaching staff page ✗ Fixtures and results ✗ League table ✗ Online scholarship application |
| Admin handling | Managed under **B1 Page Management**, identical to the 2.x static pages; the women's site URL is maintained in the admin |

> Consequently, the `Team / Player / Match` data models serve only the First Team and academy age groups. Responsibility for women's team information rests with the women's website, so this site does not mirror its roster or fixtures — avoiding inconsistency between the two sites.
>
> **Women's team official website**: [Taichung Blue Whale](https://www.tcbw2014.com/) (provided by the club, 2026-09-01).

---

### 3.7 【07】NEWS & STORIES

A **unified newsroom** segmented into eight categories:

| Category | Description | Special fields |
|---|---|---|
| 7.1 Club News | General announcements | — |
| 7.2 Match Reports | Post-match reporting | Linked match, score, line-up |
| 7.3 Academy News | Academy activity | Linked age group |
| 7.4 Player Stories | Personal features | Linked player |
| 7.5 International | Overseas partnerships / player news | Linked country / partner |
| 7.6 Camps & Events | Event previews and recaps | Linked program item |
| 7.7 Community | Community initiatives | Linked partner organisation |
| 7.8 Media | Media resource centre | **Press release downloads, brand identity pack (logo / CIS), high-resolution image library, media contact (routes to 10.6)** |

**Public-site functionality**:
- List page: category tabs, tag filter, year/month filter, keyword search, infinite scroll or pagination
- Detail page: cover image, publish date, author, body (images / video / pull quotes / galleries), tags, social sharing, related-article recommendations
- Featured pinning (max 3) and a popular-articles sidebar

---

### 3.8 【08】TCRFC CULTURE

#### 8.1 TCRFC Manga / Comics

- **About the Project**: origin of the initiative and its world-building
- **Characters**: character card wall + character detail (profile, link to the real player who inspired them)
- **Episodes / Stories**: episode list (cover, episode number, publish date) + an **online reader** (paged and scroll modes, previous/next navigation, mobile gestures); **entirely free, no sign-in required**
- **Latest Episode**: pinned block, also surfaced on the homepage

#### 8.2 Fan Club (the introduction and join page for **paid membership**)

| Block | Content |
|---|---|
| Membership Plans | Individual and family plan cards: price, season term, cards included, jerseys included, benefit summary |
| Fan Benefits | The **free versus paid benefits comparison table**, sourced from the same data as 3.14 (maintained in admin K4), visible without signing in |
| Partner Perks | Current number of partner stores and a selection of them, linking through to the full list in 8.4 |
| Join / Upgrade | Routes to registration or the upgrade flow in the Member Centre; signed-in visitors go straight to the upgrade page |
| Fan Events | Event list + registration (can be restricted to paying members) + event recaps |
| Member Draw | **An explanatory block, not an interactive one**: states that a valid membership **confers prize-draw eligibility automatically, with no entry or sign-up**, and that the prizes, quantities, **eligibility snapshot time**, draw time, and collection method for each draw are announced in News. This page has **no** draw entry, serial-number lookup, or winners list (see 3.14) |

> Fan club members are members flagged `fan_club` in the member system — **no separate list is maintained** (see 3.14 and 4.11 K).

#### 8.3 Merchandise & Online Store (**built on this site from v2.6; no external store**)

| Item | Description |
|---|---|
| Positioning | The website is both a **brand showcase** and an **online store**: collection stories and imagery keep their editorial quality, while every purchase action (add to cart, checkout, payment, invoicing, fulfilment, returns) is **handled here**, **collected for by this club** |
| Categories | Club Collection / Academy Collection / Fan Collection, each with its own brand narrative block; products may carry further tags (jersey / accessory / memorabilia) |
| Product list | Card wall (hero image, name, price, **sale price and "new" flag**, out-of-stock flag), filterable and sortable by category, price, size, and colour (**the same filters the old store already has**) |
| Product detail | Gallery, product narrative, **variant selection (size / colour, mapped to SKUs)**, size chart, **list price and sale price**, stock status, shipping and returns notes, add to cart. Emits **Product schema** (see section 7) |
| Cart | Persists across pages (account-bound when signed in, browser token otherwise, merged on sign-in), quantity editing, removal, subtotal and shipping estimate |
| Checkout | Delivery details → shipping method → **invoice option** → confirm → hand off to LINE Pay. **Guest checkout is supported** (email only; `Order.member_id` may be empty); signed-in members get pre-filled delivery details. **There is no payment-method step** — LINE Pay is the only option |
| Payment method | **LINE Pay only** (`Request → Confirm`, collected by **this club**). **No** card, convenience-store code, ATM transfer, or cash on delivery. Payment completes on LINE Pay's side; **this site renders no card fields and stores no card data** (global premise). Callbacks must be **signature-verified and idempotent**, and unpaid orders are cancelled on timeout with stock released |
| Invoicing | An **e-invoice** (B2C) is issued at checkout — **carrier, tax ID, or donation code** — made out to **this club**, notified by email, and voided or credit-noted on a return. It uses a **different invoice track from the Association's charity invoices and must never be shared**. **LINE Pay does not issue invoices**, so a separate invoicing service is used (see section 10) |
| Shipping | Home delivery, convenience-store pickup (**pickup only, no pay-on-collection**), and **collection at a home match or the club**; **a single flat rate plus a free-shipping threshold** (configured in admin S6 — **no weight or banded rates**). **No carrier API integration** — tracking numbers are entered manually or by CSV |
| Order lookup | Members use **"My orders"** in the Member Centre; **guests look up an order with the order number and email at `/order/lookup`**, or follow the time-limited link in the order confirmation email |
| Returns | The statutory seven-day cooling-off period applies (**a legal duty, not trimmed away with the rest of the scope**). The public site provides a **returns policy page** and a request channel (form or support email — **no dedicated returns wizard**); admin S5 handles review, inspection, refund, and **invoice voiding or credit notes**. Refunds go **back through LINE Pay**. **Exclusions (e.g. customised goods) must be stated on the product page** |
| Members and the shop | The shop is on this site, so the **member account is the shop account** and no SSO is needed; members see orders under "My orders". **No member pricing and no discount codes this time round** — paid membership keeps **partner-store discounts and the jersey** (see 3.14 / K4) and **does not include merchandise discounts**; adding them later requires amending the assumption in section 10 first |
| Transactional email | Shop emails are **a separate set**: order placed, payment received, shipped, refunded. **They are counted separately from the member system's five system emails, which remain five** |
| Not included | ✗ Payment methods other than LINE Pay ✗ Member pricing and discount codes ✗ Weight-based or banded shipping rates ✗ Subscriptions / recurring billing ✗ Cross-border sales and multi-currency ✗ Marketplace / multi-vendor ✗ Resale or consignment ✗ Points redemption and e-wallet ✗ Ticketing and match packages ✗ Tying merchandise to membership or draw eligibility ✗ Live chat support ✗ Product reviews and ratings |

> **Boundary with membership**: the shop sells **physical goods only**. **Membership fees do not go through shop checkout** — they keep payment links and in-person collection (and LINE Pay inside the app). Whether membership should move into the shop is an open item in section 10.
> **Boundary with the Charity Donation Platform**: both use LINE Pay, but the shop collects for the **club** and the platform for the **Association**; **merchant accounts, credentials, and invoice tracks must never be shared**. The shop must not sell donation items or take donations under the guise of merchandise.
> **Where the scope line sits**: the goal this time is to **move the old Wix store into this site as it stands and put our own payment and invoicing behind it** — not to build a bigger shop. Anything beyond what the old store already does (member pricing, discount codes, extra payment methods, banded shipping rates, subscriptions) requires amending the assumption in section 10 first.
> **Retirement of the old store**: the old site's Wix store is a transitional channel and closes once the on-site shop launches, with 301 redirects (see 1.4). The former "evaluate Shopify Storefront API product sync" option **is cancelled along with the external store**.

#### 8.4 Partner Perks (member discounts)

| Item | Description |
|---|---|
| Positioning | The content source for the discount benefit and the primary incentive to join. **A public page, browsable without signing in** |
| Store list | Card wall, filterable by **category** (dining, sportswear, health, education, services, etc.) and **area** |
| Store detail | Name, photo / logo, category, address, phone, opening hours, map link, website or social link |
| Offer | Each store states its offer and the **applicable tier** (all members / paying members only) |
| How to use | Show the digital membership card in store. The page sets out usage notes and caveats (e.g. not combinable with other offers, store terms prevail) |
| Not included | ✗ Scan-to-redeem ✗ Redemption counts ✗ Store-side accounts or admin ✗ Loyalty points ✗ E-wallet |

> This is a **different content type** from 9.1 Partners (the B2B logo wall): different audience, fields, and maintenance cadence, with no shared data (see `PartnerStore` and `Partner` in section 5).

> **Launch prerequisite**: the value proposition of paid membership rests on partner discounts and the jersey (this site does not do ticketing or match packages), so **the size and quality of the initial partner store roster directly determines whether the paid tier is viable**. It must be secured before launch (see section 10).

---

### 3.9 【09】PARTNERS & SPONSORS

**Purpose**: B2B conversion — commercially the highest-value area of the site.

| No. | Page | Public-site functionality |
|---|---|---|
| 9.1 | Our Partners | Grouped by type: strategic / international / training / education / brand. Logo wall + partner detail (nature of collaboration, period, link) |
| 9.2 | Our Sponsors | Current sponsors by tier (title / official / supporting), sponsorship stories (case articles), sponsorship activation records |
| 9.3 | Become a Partner | Six value arguments: why partner, audience analysis (**data visualisation**: followers, reach, student numbers), brand exposure, social impact, intermediary influence, international reach |
| 9.4 | Sponsorship Opportunities | **Nine sponsorship package cards**: club / academy / team / camp / international programme / manga content / merchandise / fan club / stadium naming. Each covers what it includes, the rights schedule, who it suits, and an enquiry CTA |
| CTA | Sponsorship Deck download | **Gated download form**: company / name / email → download link issued (creates a lead record; supports A/B versions) |
| CTA | Contact Us | Routes to the 10.5 partnership & sponsorship enquiry form |

---

### 3.10 【10】JOIN / CONTACT

A **forms hub** with seven forms, each with its own fields and recipients:

| No. | Form | Key fields | Recipient team |
|---|---|---|---|
| 10.1 | Join as a Player | Name, date of birth, position, experience, video link, contact details | Football operations |
| 10.2 | Academy & Children's Training | **Programme selected (Academy U12 / U14 / U15 age groups, Children's Training mixed-age / beginner / skill-development classes, or the six Specialist Training disciplines)**, student details, preferred venue, parent contact, football background, health notes | Academy / Programs (routed by programme selected) |
| 10.3 | Camp Registration | Camp session, student details, health declaration, emergency contact | Programs |
| 10.4 | International Player Enquiries | Name (English), nationality, passport, experience, video, visa status | International |
| 10.5 | Partnership & Sponsorship | **Enquiry type (partnership / sponsorship / both)**, company, industry, budget range, proposed collaboration, packages of interest, contact person | Commercial |
| 10.6 | Media Enquiries | Outlet, journalist name, topic, deadline | PR |
| 10.7 | General Contact | Name, email, subject, message | Administration |

> **v1.9 change**: former 10.2 (Join the Academy) and 10.3 (Children's Training) are merged into a single form, routed to the Academy or Programs department by the "programme selected" field; former 10.6 (Partner with TCRFC) and 10.7 (Sponsorship Enquiries) are merged into a single form, both already handled by Commercial and separated by the "enquiry type" field. The forms hub drops from nine to seven and the numbering closes up.

**Shared mechanisms**:
- Bot protection (reCAPTCHA / Turnstile), required-field validation, file upload (CV / video link)
- On submission: auto-reply to the sender + notification to the relevant team + record written to the admin enquiry inbox
- Personal-data consent checkbox (with privacy policy link) and stated retention period

**Supporting pages**:
- **Location & Map**: multi-venue list (training base, home ground, academy pitches) + embedded map + travel directions + navigation links
- **Contact Information**: phone, email, address, opening hours, departmental extensions, social links

---

### 3.11 【11】CHARITY & IMPACT

**Purpose**: demonstrate the club's `Community` core value in practice, reinforcing brand trust while serving as supporting material for CSR-driven corporate sponsorship (cross-linked with the "social impact" argument in 9.3).

| No. | Page | Public-site functionality |
|---|---|---|
| 11.1 | Our Commitment | Philosophy and focus areas (youth support, rural football, disadvantaged families, charity matches), composed with the block editor |
| 11.2 | Charity Programs | **Program list** (cover, name, beneficiaries, period, status: ongoing / completed); detail pages cover the programme's origin, **recipient charity name**, **what was donated**, delivery narrative, **event photo gallery**, and related coverage |
| 11.3 | Impact Stories | **Timeline / card list**; every record presents three core data points: **① charity organisation name ② what was donated ③ event photography**, plus date, location, and a short description; filterable by year |
| 11.4 | Our Impact | Cumulative statistics: number of partner charities, total donation instances, areas served; presented as a **logo wall / list of organisations** with representative imagery. Monetary figures are hidden by default; visibility is decided per item in the admin |
| CTA | Get Involved | Two participation routes: **corporate charity partnership** (routes to 10.5) / **fan donation** (links out to the Charity Donation Platform, see below) |

#### Fan donation: routing to the Charity Donation Platform

Fan donations are **not handled on this site**. They link out to the **Charity Donation Platform** on its own domain (see [`TCRFC_Charity_Donation_Platform_Specification_EN.md`](TCRFC_Charity_Donation_Platform_Specification_EN.md)). That platform's main entry point is a QR code displayed by partner venues; it integrates LINE Pay properly and issues an e-invoice or a donation receipt depending on the project.

> **That platform is organised and collected for by 台灣足球策略發展協會 (the Association), not by this club** (Charity Donation Platform specification v1.4). The CTA copy must therefore name where it leads and who receives the donation, and **must never read as donating to Taichung Rock FC**.

This site has three responsibilities and no more:

| Item | Approach |
|---|---|
| Referral | Every "fan donation" CTA in section 11 links to the Charity Donation Platform; the URL is configured in admin B6 and **never hard-coded into a template**. **The copy must state that the Association receives the donation** |
| Impact feedback | Donation projects on that platform can link to this site's `CharityProgram` records; how funds were used continues to be presented as structured content in 11.2 / 11.3 |
| Consistency | This site shows no amount options, hosts no donation form, and publishes no bank account details |

> **This site takes no donation payments — unchanged by v2.6.** The shop's payment integration covers **physical merchandise only, collected for by the club**; donation payments, invoicing, revenue share and reporting all belong to the Charity Donation Platform, collected for by the **Association**. The two share an admin and a database, but their public sites, merchant accounts, and invoice tracks are entirely separate, and **the shop must never sell donation items**.
> The three channels from v2.0 (Shopify donation item, bank transfer, in-kind donation) and the on-site transfer report form have **all been removed** and are no longer part of this specification.

**Content cross-linking**:
- Shares the news library with **7.7 Community**: timely reporting of charity activity is published in 7.7, while the Charity section presents programmes and impact records in structured, long-lived form. The two cross-link rather than duplicating content.
- Cross-linked with **5.5 School & Community**: rural and school football outreach is both a programme and a charitable activity, so it appears in both places.
- Charity programmes can be tagged with sponsoring partners (linked to E1/E2) so that partner pages can show "charity programmes we've supported".

---

### 3.12 【12】FAQ

**Section positioning**: trials, program registration, fees, international development, and sponsorship all attract high-frequency questions, so FAQs are handled as a **standalone, centrally managed section**, with individual pages embedding the relevant topic automatically.

| Item | Public-site functionality |
|---|---|
| FAQ home | Topic navigation cards: `Joining the club`, `Academy admissions`, `Program & camp registration`, `Fees & refunds`, `Trials`, `International pathways & foreign players`, `Women's football`, `Fan club & merchandise`, `Partnership & sponsorship`, `Other` |
| Search | Live keyword search (across question and answer text); no-result searches route to the 10.7 general contact form |
| Presentation | Accordion, with **deep links to individual questions** (`/faq/#q-123`) so support staff can send a link to a single answer |
| Category pages | Each topic has its own page with independent SEO settings |
| Helpfulness feedback | "Was this helpful? 👍 / 👎" under each answer, with results written back to the admin for optimisation |
| Unresolved routing | A persistent CTA at the bottom of each page: contact us (10.7) or the form specific to that topic |
| Embeddable block (G-12) | 4.7 Academy admissions, the 5.x programs, 3.3 trials, and 9.4 sponsorship automatically embed the top five FAQs for their topic plus a "see all" link |
| SEO | Emits **FAQPage schema** to compete for FAQ rich results and citation by AI engines |

---

### 3.13 【13】SCHEDULE

**Purpose**: bring **every team's fixtures and results** together on one page so fans, parents, and media can follow the club's competitive calendar, and subscribe to it in their own calendar app.

> **Scope**: this section is **match-centric**. Academy courses, children's training, summer/winter camps, and specialist training are **not** in the calendar; their timings and sessions remain on the 05 program pages (see 3.5).

#### Data sources

| Event type | Source module | Displayed | Click behaviour |
|---|---|---|---|
| 🏆 Match (primary) | C4 Fixtures | Both teams, kick-off, venue, home/away, score | Go to match detail / match report |
| 📣 Club Event (secondary) | Calendar-native event | Press conference, signing session, fan meet-up, open training, etc. | Show detail or go to the event page |

**Excluded from the calendar**:

| Content | Where it appears instead |
|---|---|
| Children's classes, summer/winter camps, specialist training sessions | Timetable and registration blocks on the 05 program pages |
| Academy day-to-day training timetables | The 04 team pages; or the Member Centre for enrolled parents |
| Trial sessions | The trial blocks on 3.3 Player Opportunities and 4.7 Join the Academy (the admin can optionally sync these to the calendar) |

#### Team categories (the calendar's first-level filter)

The calendar is organised primarily **by team**:

| Code | Team | Covers | Source data |
|---|---|---|---|
| **D1** | **First Team** | League and cup fixtures, results, first-team public events | C1 Team (`first_team`), C4 Matches |
| **U15** | U15 squad | That squad's fixtures and results | C1 Team (`academy` / U15), C4 Matches |
| **U14** | U14 squad | As above | C1 Team (`academy` / U14), C4 Matches |
| **U12** | U12 squad | As above | C1 Team (`academy` / U12), C4 Matches |

**Categorisation rules**:

1. Every match must be assigned to a **team** (cross-squad friendlies may select more than one).
2. Events not belonging to a specific team are classified as `Club Event` and appear in the "All" view.
3. Once a team is selected, the page header shows the team name and a countdown to its next fixture, and offers that team's **dedicated calendar subscription URL** (so a parent subscribing to U12 does not receive D1 fixtures).
4. The team list is generated automatically from the C1 team records, so **any new squad added later (U18, U10, etc.) appears as a category automatically** with no code change (matching the "other age groups" provision in 4.2).

> **Terminology**: **D1 is the First Team described in 3.1** — the same team, not an additional one. `D1` is the site-wide **team code** used for calendar categories, filter chips, and subscription URLs; `First Team` is the public-facing display name used in headings and body copy across 03 FOOTBALL CLUB.
>
> The team list (D1 / U15 / U14 / U12) is therefore the First Team plus the three academy squads from 4.2 Our Teams — covering every competitive team at the club.

#### Fixture list layout (reference: Manchester United fixtures page)

**Reference**: [Men's Team – Fixture Listing | Manchester United](https://www.manutd.com/en/matches/mens-team/fixtures)

> Note: that site returns 403 to programmatic fetching. The **confirmed** points below are: ① the page is men's-team-specific, i.e. team is the first-level split; ② all kick-off times are shown in the **viewer's local time zone**, with a note that times may change; ③ the site operates a Match Centre. The remaining specifications follow common practice among professional club fixture pages.
>
> Supporting evidence: Man Utd splits fixtures across Men's / Women's / U21 / U18 tabs, consistent with the `D1 / U15 / U14 / U12` team split proposed here.

##### Layout structure

```
┌──────────────────────────────────────────────────────────┐
│  SCHEDULE                                                 │
│  [ D1 ] [ U15 ] [ U14 ] [ U12 ]              ← team tabs  │
├──────────────────────────────────────────────────────────┤
│  [ Fixtures ] [ Results ]                    ← main tabs  │
│  Season 2026/27 ▾   Competition All ▾   Venue All ▾  [Cal]│
├──────────────────────────────────────────────────────────┤
│  MARCH 2026                              ← month grouping │
│  ┌────────────────────────────────────────────────────┐  │
│  │ Sat 15   │ League │ TCRFC [crest] vs [crest] Rival │  │
│  │ 19:00    │        │ 📍 Taichung Football Stadium · H│  │
│  │          │        │ [Match info] [Tickets] [+ iCal] │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

##### Fixture card fields

| Area | Content |
|---|---|
| Left time column | Weekday + date (large) and kick-off time; **displayed in the viewer's local time zone**, with a page-level note: "All times are local and subject to change" |
| Competition tag | League / cup / friendly name with an identifying colour |
| Match-up | Home crest + name `vs` away crest + name; TCRFC's side highlighted in brand colour |
| Venue | Venue name, home/away marker, map link |
| Status | Upcoming / live / finished / postponed / cancelled (postponed fixtures show the original date) |
| Actions | `Match info`, `Tickets` (if applicable), `Add to calendar (.ics)`, `Broadcast info` (if applicable) |
| Results mode adds | Score (large), scorers, `Match report`, `Highlights` |

##### Tabs and filters

| Control | Options |
|---|---|
| Team tabs (first level) | `D1` / `U15` / `U14` / `U12` (plus `All` and `Club Events`) |
| Fixtures / Results toggle | `Fixtures` (future, nearest first) / `Results` (past, most recent first) |
| Season | Dropdown, defaulting to the current season, with past seasons available |
| Competition type | All / League / Cup / Friendly / Other |
| Venue | All / Home / Away |
| Month grouping | The list is separated by month headings, with quick month jumps |
| View toggle | `List` (default) / `Calendar` |

#### Public-site functionality

| Feature | Description |
|---|---|
| **List view (default)** | Uses the fixture card layout above, grouped by month; fixtures nearest-first, results most-recent-first |
| **Calendar view** | Month grid with a tag per event; clicking a date expands that day's fixtures |
| **Multilingual** | Calendar content supports **Chinese / English**: team names, competition names, venue names, and event titles must all be maintained bilingually, falling back to Chinese where English is absent |
| **Time zones** | Kick-off times are converted to the viewer's time zone with a "subject to change" note, so overseas fans and partners are not misled |
| **Tabs and filters** | See the table above; on mobile, secondary filters collapse behind a "Filter" button |
| **Event detail** | Side drawer or modal: match-up, time, location (with map link), status, primary CTA |
| **Add to my calendar** | `.ics` download for a single fixture; or **subscribe by team** (one webcal URL per team plus a site-wide feed; Google Calendar and Apple Calendar stay in sync automatically) |
| **Sharing** | Per-fixture share link with an OG preview image (date + match-up) |
| **Member integration** | Signed-in users can mark "I'm attending"; marked fixtures are collected in the Member Centre |
| **Reminders** | Members can set reminders 1 day / 1 hour before kick-off (LINE / email / on-site — see K4) |
| **Embeddable block** | An "upcoming fixtures" block can be placed on the homepage, first-team page, and each squad page, filtering by team automatically based on its host page |

#### Usage scenarios

- **Fan**: switch to `D1` → subscribe → every fixture change syncs to their phone calendar automatically
- **U12 parent**: switch to `U12` → subscribe to that squad → receives only their child's matches, undisturbed by D1 fixtures
- **Coach / team manager**: switch to their squad → full view of the season
- **Media**: browse fixtures and results, retrieve match report links

#### SEO and URL rules

- Every public fixture emits **SportsEvent schema** (name, start/end time, location, competing teams, status) to compete for Google's match result features
- Team categories have their own indexable URLs: `/schedule/d1/`, `/schedule/u15/`, `/schedule/u14/`, `/schedule/u12/`
- Significant fixtures can have dedicated URLs: `/schedule/2026-03-15-tcrfc-vs-xxx`

---

### 3.14 MEMBER CENTRE

**Positioning**: the member system carries **one thing — membership**. Anyone who joins gets discounts at partner stores; paying members additionally receive a jersey. The member system does not handle student records or newsletter campaigns; **this site's public web front end** does not handle booking attribution (the Mobile App provides "my bookings", see the app specification, section 3.9).

> **Explicitly out of scope**: loyalty points, e-wallet, ticketing and match packages, and partner-store scan-to-redeem and redemption reporting.
> **From v2.6 "no SSO with Shopify" lapses** — the external store is gone and the shop is on this site (8.3), so the member account *is* the shop account and no second account exists.

#### Membership tiers (two)

| Code | Display name | How it's obtained | Benefits |
|---|---|---|---|
| `registered` | Member | Free registration + email verification | Digital membership card; discounts at partner stores marked "all members" |
| `fan_club` | Fan Club Member (paid) | Annual fee, activated by the admin after payment | The above + discounts marked "paying members only" + **a jersey** (quantity per plan) + priority booking for fan events + **prize draw eligibility** (automatic while the membership is valid, with no sign-up) |

> A paying member **is** a fan club member (8.2) — not a separate identity, and no separate list is maintained. The former "Parent" tier has been removed.

#### Membership term and plans

- **Season-based term**: membership runs by season (e.g. 2026/27); `paid_until` comes from the plan rather than being calculated from the join date. Everyone expires together and renewals are handled in one batch at season end.
- **Plan settings** (admin K2): plan name, price, season code, start and end dates, `card_quota` (cards issued per membership), `jersey_quota` (jerseys included), mid-season pricing rule, benefit description, sort order, publish state.
- **Family plans** are simply different records of the same entity (e.g. 1 adult + 2 children = `card_quota` 3, `jersey_quota` 3). Names and jersey sizes for additional cardholders are captured during jersey registration — **no parent–student linking mechanism is required**.
- **Mid-season pricing** (pro-rata, full price, or otherwise) is defined per plan.

#### Membership fees (no payment processing on this site)

| Item | Approach |
|---|---|
| Payment method | **LINE Pay** (payment link / official account invoice), plus in-person payment (matchdays, recruitment events) |
| Role of the website | Presents plans and payment instructions and accepts upgrade requests; **membership fees do not go through shop checkout** — v2.6 opens on-site payments, but **for physical merchandise only**, while membership keeps payment links and in-person collection. **"No card data" remains globally applicable** — the Mobile App stores no card data either |
| Mobile App | **Membership payment is completed in-app via LINE Pay** (membership fees only); a successful-payment callback activates the membership automatically, see the app specification, section 5. The collecting entity is **this club**, using **the club's own merchant account — never the Association's** |
| Activation | Customer service activates the membership in the admin after reconciling payment, recording payment method, amount, date, transaction note, and handler; the system writes a payment record for reconciliation and audit |
| Automation hook | An internal endpoint `POST /api/membership/activate` (credential-protected, and required to be idempotent). **From v2.5 it is triggered by the Mobile App's successful-payment callback**, with manual activation by support staff retained; the original promise — "only the trigger changes, the member module does not" — is delivered here. Calling conventions are in the app specification, section 9.7 |

> **Web checkout for membership is still not built (v2.6).** The payment assumption has now been amended twice — v2.5 for the Mobile App, v2.6 for the on-site shop — both following the rule of amending section 10 before implementing. **Whether membership moves to shop checkout is a third amendment that has not been made**; it is listed as an open item in section 10 and must not be implemented before it is settled.

#### Digital membership card and how discounts are used

- **Card contents**: member number, QR code, name, tier, expiry date.
- **In store**: the member shows the digital card and staff check it visually. The QR code points to a public verification page `/m/<token>` that displays **only** the first character of the name, the member number, the tier, and valid / expired status — no other personal data.
- **No scan-to-redeem**: the system does not count redemptions, does not produce store performance reports, and stores need no account and no software. The verification page is read-only.
- **Token security**: the token cannot be derived from the member number, and members can regenerate it from the Member Centre if a card is leaked.

#### Benefits comparison table (the core of signup conversion)

- A **line-by-line comparison** of free versus paid benefits, maintained as structured content in the admin (K4).
- **One dataset, three placements (from v2.5, three plus the Mobile App)**: ① the join page, ② Fan Benefits on the fan club page (8.2), ③ the upgrade page in the Member Centre, plus ④ the app's upgrade screen. All share one component and one dataset; **the app must not hard-code any benefit text**.
- **Visible without signing in.** Benefits and the partner store list are the incentive to join; requiring registration to see them defeats the purpose.
- **Must not be delivered as images.** Benefits require zh/en fields, must be indexable by search engines, and must be readable by screen readers.

#### Fan Club Prize Draw (eligibility is automatic)

- **Eligibility**: at a draw's **eligibility snapshot time**, every paying member (`fan_club`) with a valid membership is **automatically included in the eligible roster**. The member **does nothing at all** — no entry, no sign-up, no check-in, and nothing to accumulate.
- **How the draw runs**: the physical draw is **performed by people, on site or on a live stream**, and what is drawn is the **draw serial number** issued by the system. The system only freezes the roster into a **snapshot** at the snapshot time, issues consecutive serial numbers, and exports the list for use at the event. It runs **no random-selection algorithm** and produces no random result.
- **Announcement**: winners are announced **through News only** (category 7.1 Club News, tagged `Fan Club Prize Draw`), always masked (serial number + member number + masked name). No system email, no on-site notification, no LINE push.
- **Prizes**: physical items supplied by the club, shipped or collected in person, fulfilled the same way as jerseys (admin K3). No tickets, no partner-store redemption, no cash or cash equivalent.
- **Admin workflow**: see 4.11 K5 Prize draw rosters.
- **Not built on the public site**: a draw page or entry button, "my draws / my serial number" in the Member Centre, any lookup of a serial number or win status, an on-site winners list page, an online draw animation or wheel, a live counter of eligible members, and any share-or-invite mechanism that would improve someone's chances.
- **In the Mobile App**: only a read-only display of the member's own **eligibility boolean**, the rules and prizes, and the draw time and setting; results always link out to the News post. See [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) section 3.11. **Every exclusion above applies equally in the app — not one is relaxed**, and the app must not read `DrawRoster`.

##### How the draw stays inside the decisions already made

| Already ruled out | Why this design does not breach it |
|---|---|
| ✗ **Loyalty points** | Eligibility is a **boolean**: valid membership at the snapshot time, or not. **One person, one serial number** — spending, checking in, sharing, inviting, or taking part more often never improves the odds. No points, balance, accrual, or weighting field exists, and the roster snapshot holds no count column |
| ✗ **Ticketing and match packages** | The draw serial number is only the snapshot's running number: **no face value, non-transferable, non-tradable, not scanned for entry, no QR code, never shown on the public site**. Prizes explicitly exclude tickets and packages |
| ✗ **Store scan-to-redeem and redemption reports** | Collection happens at the club and is recorded by **ticking a status in the admin** (pending / shipped / collected, exactly as K3). Nothing is scanned or counted, no store report is produced, and partner stores are not involved at all; the response from the card verification page `/m/<token>` is **unchanged**, with no new fields |
| ✗ **On-site notification centre and LINE push** | Winners are announced **through News only**. The five system emails stand, with **no sixth added**; there is no on-site messaging, no notification preferences, and no LINE push. Contacting an individual winner is done by support staff by phone or a written message, outside any system template and never recorded in `EmailLog`. **This commitment stands in v2.5: the Mobile App's native push must never be used for individual winner notifications**, and admin M3 must block this at system level |
| ✗ **The draw itself involves no payments** | The draw **sells nothing, charges nothing, and has no paid entry**. What is paid for is the membership itself, still collected by LINE Pay payment link or in person and activated after reconciliation. Prizes are the club's own physical goods; the system handles no prize payments, refunds, or invoices. **Opening shop payments in v2.6 changes nothing here**: the shop sells physical goods only and **must not sell draw entries, must not let spend increase the chance of winning, and must not list or discount prizes** — eligibility remains a boolean derived from membership |
| ✗ **Booking attribution and form pre-filling** | The draw has **no form**. Eligibility is automatic and nothing is filled in, so neither applies |
| ✗ **System-run random selection** (added by this revision) | The draw is performed by people, on site or on a live stream, where it can be witnessed. The system only freezes the roster, issues serial numbers, and exports them — it **produces no random result**, which keeps the club clear of disputes over the fairness of an electronic draw and of the burden of proving it |

#### Jersey fulfilment

- Once a paid membership is activated, the member enters a **size** and **collection method** (shipping / in-person) in the Member Centre; shipping requires recipient name, phone, and address.
- Fulfilment status: `pending / shipped / collected`, visible to the member and maintained by the admin in K3.
- Family plans capture a size for each jersey per `jersey_quota`.

#### Signup channels (two)

| Channel | Description |
|---|---|
| **A. Email registration** | Email + password, activated by a verification email |
| **B. One-tap LINE sign-in / registration** | Authorise with LINE to create and link the account |

> Both channels create records in **the same member database**; when the same person arrives via a different channel, accounts are merged by matching on email or mobile number. Google sign-in is not used.

#### Public-site functionality

| Feature | Description |
|---|---|
| Registration | ① Email + password; ② one-tap LINE registration |
| Email verification | Verification email activates the account |
| Sign in / out | Email + password, one-tap LINE sign-in; remember me; failed-attempt limits |
| Forgotten password | Time-limited reset link by email |
| LINE link management | Shows link status and allows linking and unlinking; at least one sign-in method must remain |
| Profile | Name, mobile, email, date of birth, language preference; change password, delete account (personal-data deletion request workflow) |
| **Digital membership card** | Member number, QR code, tier, expiry date; QR can be regenerated |
| **Benefits comparison** | Free versus paid, line by line, visible without signing in |
| **Partner store list** | Browse by category and area, each store marked with the applicable tier and offer (see 8.4) |
| **Upgrade to paid membership** | Plan comparison (individual / family), payment instructions, upgrade request, status display (pending / active / expired) |
| **Jersey registration** | Enter size and collection method; view fulfilment status |
| Renewal | Renewal prompt and payment instructions ahead of expiry |
| **My orders** (added in v2.6) | Shop order list and detail: items, amount breakdown, payment and fulfilment status, tracking number, invoice number, and the entry point for return requests (see 8.3 and 4.13) |

> The following are **not included on this site's public web front end** after review: form pre-filling, my bookings, my donations, my students (parent linking), my calendar (.ics), on-site notification centre, notification preference centre, the members-only content area, **"my draws" (serial-number and win lookup)**, **an on-site winners list page**, and **an online draw or random-selection tool**.
>
> **The v2.5 exceptions**: the Mobile App provides **form pre-filling** and **my bookings** (see the app specification, section 3.9), plus an **in-app notification centre and preferences** (section 6.4). **My donations, my students (parent linking), my draws, the winners list page, and any online draw tool remain excluded in the app as well.**

#### Email notifications (five only, all with zh/en templates)

Registration verification, password reset, membership activation confirmation, 30-day expiry reminder, and expiry notice.
**Not included**: on-site messaging, LINE push, newsletter campaigns. Marketing emails must carry an unsubscribe link.
**The five system emails stand unchanged and are neither increased nor reduced by the Mobile App's native push (APNs/FCM)** — push is a second channel for existing notifications, not a new class of notification. See [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) section 6.

#### Integration with existing modules

- **Fan club (8.2 / F2)**: fan club members are members flagged `fan_club`; no separate list is maintained, and page 8.2 is the introduction and join page for paid membership.
- **Partner stores (8.4)**: the content source for the discount benefit, publicly browsable.
- **Official store (8.3 / admin S)**: the shop is on this site, so the **member account is the shop account** — there is no second account and no SSO to build, and orders appear under "My orders". **Guest checkout remains supported**, so `Order.member_id` may be empty. **Paid membership does not include a merchandise discount** — no member pricing and no discount codes this time round; the benefits stay partner-store discounts and the jersey (see the benefits table, K4).
- **LINE Official Account**: used only for sign-in and account linking — not as a separate contact database, and with no parameterised QR source tracking.

---

## 4. Admin CMS Functional Specification

### 4.0 Admin structure overview

```
TCRFC Admin
├── A. Dashboard
├── B. Content
│   ├── B1 Pages (static pages / blocks, incl. the women's football page)
│   ├── B2 News & Stories
│   ├── B3 Media Library
│   ├── B4 Homepage slots / Banners
│   ├── B5 FAQ Management
│   └── B6 Charity & Impact Records
├── C. Teams
│   ├── C1 Teams (first team / academy squads)
│   ├── C2 Players
│   ├── C3 Coaches & Staff
│   ├── C4 Matches (fixtures / results / standings)
│   └── C5 Honours & Milestones
├── P. Programs
│   ├── P1 Programs / Camps
│   ├── P2 Sessions
│   ├── P3 Registrations
│   └── P4 Trial Sessions
├── E. Business
│   ├── E1 Partners
│   ├── E2 Sponsors & Packages
│   ├── E3 Sponsorship Deck & Download Tracking
│   └── E4 Product Showcase (**folded into S1 in v2.6; identifier retired**)
├── F. Culture
│   ├── F1 Manga project / characters / episodes
│   └── F2 Fan Club events (member lists live in module K)
├── G. Forms & Enquiries
│   ├── G1 Form Designer
│   ├── G2 Inbox (7 form types + deck downloads + donation enquiries)
│   └── G3 Newsletter Subscribers
├── H. SEO & Marketing
├── I. Site Settings (menus / footer / languages / contact info / venues / external services)
├── J. System (accounts / roles / audit / backup)
├── K. Members
│   ├── K1 Member list and detail
│   ├── K2 Membership and plans
│   ├── K3 Jersey fulfilment
│   ├── K4 Partner stores and benefits
│   └── K5 Prize draw rosters (roster and export only; no random selection)
├── L. Schedule
│   ├── L1 Master calendar (cross-module aggregate view)
│   ├── L2 Custom events (club events)
│   ├── L3 Categories & display settings
│   └── L4 Subscription & export (iCal / .ics)
├── M. Mobile App (added in v2.5; defined in the Mobile App specification, section 8)
│   ├── M1 App releases & version management
│   ├── M2 App composition & deep links
│   ├── M3 Push notification management
│   ├── M4 Devices & push tokens
│   └── M5 App settings, certificates & diagnostics
└── S. Shop (added in v2.6; defined in 4.13)
    ├── S1 Products & variants (SKUs)
    ├── S2 Inventory
    ├── S3 Orders
    ├── S4 Fulfilment & shipping
    ├── S5 Returns & refunds
    └── S6 Shop settings & reports
```

> **Numbering note**: the programs module was originally numbered `D1–D4`, which clashed confusingly with the **team code `D1`** (First Team). It has been renumbered **`P1–P4` (Programs)**, and all references throughout this document have been updated.
> **Why the shop module takes `S` (Shop)**: `D` is retired because of that clash, `N` belongs to the Charity Donation Platform, and `M` belongs to the Mobile App, so a new letter is used rather than a recycled one. **`E4` is retired once folded into `S1` and will not be reused** — recycling it would give "where are products maintained?" two answers.

---

### 4.1 A. Dashboard

| Feature | Description |
|---|---|
| Action items | Unhandled enquiries, registrations awaiting approval, camps closing soon, sponsorship contracts nearing expiry |
| Traffic overview | GA4 integration: weekly page views, top pages, traffic sources |
| Conversion overview | Form submissions, registrations, deck downloads, **new member registrations (split by website / LINE source)** — weekly and monthly trend charts |
| Content overview | Articles published this month, drafts, **untranslated content count (English / Japanese listed separately)** |
| FAQ overview | Top 10 questions, alerts on questions with negative (👎) feedback |
| Upcoming schedule | Matches / camps / trials / events in the next 14 days, with exception alerts (no coach assigned, places unfilled, incomplete information) |
| Quick actions | Publish news, add a match, add a program session, add an FAQ, add a calendar event |

---

### 4.2 B. Content

#### B1 Pages
- Covers every static page (2.x, 3.2–3.4, 4.x, 5.x, **06 Women's Football**, 9.3, 11.1, etc.)
- **Block editor**: text, image-with-text, gallery, video embed, pull quote, CTA, FAQ accordion, timeline, step bar, stat cards, tables, file downloads
- Each page carries: status (draft / published / scheduled), SEO settings, language versions, revision history with rollback, and a preview link (shareable before publishing)

#### B2 News & Stories
- Article CRUD, categories (mapping to 7.1–7.8), tags, core-value tags
- Cover image, summary, block-based body, relationships (player / team / match / program / partner)
- Scheduled publishing, featured pinning (max 3), view counts
- Bulk actions: recategorise, bulk publish / unpublish

#### B3 Media Library
- Image / video / PDF upload with automatic compression, WebP conversion, and multi-size cropping
- Folders, tags, alt text (multilingual), usage tracking (warning before deletion)
- **For the media centre (7.8)**: assets flagged "publicly downloadable" — press releases, brand identity packs, high-resolution images

#### B4 Homepage slots / Banners
- Hero carousel management (order, image/video, headline, CTA, display period); **seasonal content (new manga chapters, charity campaigns, enrolment windows) is surfaced here** instead of via dedicated fixed slots (**this applies to this site's public web front end**; the Mobile App's own advertising slots are covered by the app specification, section 7, and admin E5–E7)
- Toggles and ordering for each homepage block, plus featured-content selection (current blocks: hero, core values, pillar cards, latest match, upcoming fixtures, latest news, sponsor logo wall, store entry, bottom CTA)

#### B5 FAQ Management
- **Topic categories**: create / reorder / disable categories (joining the club, academy admissions, program registration, fees & refunds, trials, international pathways, women's football, fan club & merchandise, partnership & sponsorship, other)
- **Question CRUD**: question, answer (rich text with links / images / files), categories (multi-select), sort weight, status (visible / hidden), bilingual versions
- **Embedding**: specify which pages' FAQ quick blocks (G-12) a question may appear in, or map automatically by category
- **Performance data**: per-question views, 👍 / 👎 counts and ratio; a "low-rated questions" list for rewriting
- **Zero-result search terms**: a ranking of searches that returned nothing, used to decide which FAQs to add
- Bulk actions: recategorise, show / hide, CSV import and export

#### B6 Charity & Impact Records
- **Charity Program**: name, cover, beneficiaries, period, status (ongoing / completed), background and content (block editor), **recipient charity** (linked to the organisation records below), **what was donated**, gallery, related coverage (7.7)
- **Impact Record**: three required fields — **charity organisation name**, **what was donated** (free text, e.g. "50 footballs, 100 training bibs" or "N scholarships"), and **event photography** (multiple images); plus date, location, short description, and optionally the parent programme
- **Charity organisation records**: name, description, logo or representative image, website, contact person, collaboration history; reusable across multiple impact records to avoid re-entry
- **Impact metrics**: custom statistics (name, unit, value, public visibility); **monetary metrics are private by default**
- **Donation referral settings**: the Charity Donation Platform URL (**never hard-coded into a template**) and the CTA copy. Donation records, amount options, invoicing and the donor roll are all maintained in that platform's `N` module and are not duplicated here
- **Get Involved settings**: copy and destinations for **two** CTAs (corporate partnership / fan donation); volunteer signup is out of scope
- Display control: ordering and pinning within the charity section (**no fixed charity slot on the homepage**; for temporary exposure, use the hero carousel or publish a news article). **The Mobile App carries no charity slot either** — the Charity Donation Platform is run and collected for by the Association, so this club's app must not sell or grant charity-related placements

---

### 4.3 C. Teams

#### C1 Teams
- Team record: name, **team code (D1 / U15 / U14 / U12 …)**, type (`first_team` / `academy`), age group, season, description, key visual, brand colour, display order
  - **`D1` = `first_team` (First Team)**, of which there is exactly one site-wide; U15 / U14 / U12 and any future squads are `academy`
  - Team codes must be unique — they are the identifier for calendar categories and subscription URLs (e.g. `/schedule/d1/`)
- **Team codes drive calendar categorisation**: teams created here automatically become filter options and subscription sources in the public Schedule (13)
- Supports adding new age groups (matching "other age groups" in 4.2 — adding U18 or U10 later requires only a new record here)
- **Women's football (06) has no team record**; it is managed as a single page (B1). The `women` type is reserved so it can be enabled later

#### C2 Players
- Profile: name (Chinese and English), squad number, position, date of birth, height and weight, nationality, preferred foot, join date, photo
- Career: history, previous clubs, achievements
- Season stats: appearances, goals, assists, cards (entered manually or aggregated from match data)
- Status: active / departed / on loan / developing overseas (mapping to the Pathways markers in 3.4)
- Relationships: team (across seasons), related news, player stories

#### C3 Coaches & Staff
- Coaches: name, title, licences (AFC A/B/C etc.), specialisms, history, assigned squad, photo
- Staff (2.4 Our People): group (management / administration / medical / operations), title, bio

#### C4 Matches
- Match record: season, competition (league / cup), date and time, home/away, opponent, venue, status (upcoming / live / finished / postponed)
- Result: score, scorers with timings, cards, line-up, link to the match report (7.2)
- **League table**: maintained manually or imported from CSV
- **Maintenance approach: entirely manual** (no external league API integration). Both **CSV bulk import** of a full season and single-match entry are provided to reduce data-entry effort

#### C5 Honours & Milestones
- Honours: year, competition, placing, associated team
- Milestones (2.8): date, title, description, image, whether shown on the timeline

---

### 4.4 P. Programs

#### P1 Programs / Camps
- Types: children's training / summer camp / winter camp / specialist training / school & community
- Fields: name, description, who it's for, age range, curriculum (block editor), coaching staff (linked to C3), partners (linked to E1), images, FAQs

#### P2 Sessions
- Session: period, weekly timetable, venue (linked), capacity, current registrations, fees (standard / early-bird / early-bird deadline), registration open and close times, status (open / full / waitlist / closed)
- The public site automatically shows "Register now / Join waitlist / Closed" based on status

#### P3 Registrations
- Registration list: filters (program, session, status, date, **member or not**), keyword search, and a **"member" column** (added in v2.5; `Registration.member_id` is nullable — non-members may still register)
- Registration detail: student details, parent contact, health declaration, notes
- Status workflow: `Pending → Confirmed → Paid → Completed / Cancelled / Waitlisted`
  - Payment is handled **offline** (imported transfer records or on-site collection, then marked by staff). **Course fees do not go through shop checkout** — the v2.6 payment scope covers physical merchandise only; **the Mobile App does not take course fees either**, its LINE Pay flow being limited to membership, see the app specification, section 3.9
- Actions: confirm / cancel, move to another session, add to waitlist, add notes, send templated notification emails
- **Excel export**: roster export (with grouping columns) and printable attendance sheets
- Capacity control: automatic closure when full, waitlist promotion alerts

#### P4 Trial Sessions
- Backs the trial information in 3.3, 4.7, and 6.3: date, venue, target group, capacity, registration deadline, registrant management

---

### 4.5 E. Business

#### E1 Partners
- Partner record: name (Chinese / English), logo (light and dark variants), type (strategic / international / training / education / brand), country, nature of collaboration, period, website, display order, whether shown in the footer / homepage

#### E2 Sponsors & Packages
- Sponsors: name, logo, tier (title / official / supporting), contract period, what the sponsorship covers, contact person, **expiry reminders**
- Sponsorship stories: linked articles (7.x)
- Activations: name, date, gallery, results summary
- **Package management (9.4)**: content, rights schedule, price range (can be hidden), and ordering for the nine packages

#### E3 Sponsorship Deck & Download Tracking
- Upload the deck PDF (multiple versions / languages), configure the download form fields
- **Lead list**: who downloaded, company, timestamp, source page; exportable to CSV with follow-up status flags

#### E4 Product Showcase (**folded into S1 in v2.6; identifier retired**)

From v2.6 products are maintained in the **`S. Shop` admin module** (products and variants in S1, collection copy in S1, store entry and policies in S6); this module no longer holds product data.
The `ProductShowcase` type is retired with it — display fields and an outbound link cannot carry SKUs, inventory, or orders. **The `E4` identifier is retired and not recycled**; anything describing "E4 Product Showcase (Shopify referral)" is pre-v2.6 material.

#### E5–E7 Mobile App Advertising (added in v2.5)

- **E5 Advertisers & slots**: maintenance of `Advertiser` (which may link to an existing `Sponsor`) and `AdSlot`
- **E6 Advertising flights & creatives**: the `AdCampaign` state machine, creative review, flight-conflict view, emergency pause
- **E7 Advertising performance reports**: impressions, clicks, CTR, unique devices; exports must be written to the audit log

> Full fields, state machine, impression definitions, and privacy rules are defined in [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) sections 7 and 8.6–8.8.
> **These sit in E rather than M for permission minimisation**: selling advertising should not also grant app release and push-sending rights.
> **The sponsor logo wall (E1/E2) counts no impressions and never enters advertising reports**; if a sponsorship contract includes app placements, create an `Advertiser` in E5 linked back via `sponsor_id` and open an `AdCampaign`.

---

### 4.6 F. Culture

#### F1 Manga Management
- Project settings (8.1 About): world-building page
- Characters: name, profile, artwork, linked real player (optional), order
- Episodes: episode number, title, cover, **bulk upload and ordering of interior pages**, publish date, status, latest-episode flag (determined automatically)
- **All episodes are free to read**, with no paywall and no sign-in requirement
- Readership statistics

#### F2 Fan Club (events)
- **Member lists, membership plans, and the benefits table are all maintained in module K** (fan club members = paying members with the `fan_club` tier); no separate list and no separate plans are kept here
- Fan events: event CRUD, registrant list (can be restricted to paying members), event recaps (linked galleries and articles)
- The plan cards and benefits table required by page 8.2 are sourced from K2 (plans) and K4 (benefit entries)

---

### 4.7 G. Forms & Enquiries

#### G1 Form Designer
- Create and edit form fields (text, dropdown, multi-select, date, file upload, consent checkbox)
- Per form: notification recipients (multiple allowed), auto-reply template, CAPTCHA toggle, post-submission redirect

#### G2 Enquiry Inbox
- A unified inbox with tabs by form type (10.1–10.7 + deck downloads + **donation enquiries**)
- Fields: source form, name, contact details, message summary, source page, UTM source, submission time
- Status workflow: `New → In progress → Replied → Closed / Invalid`
- Assignee, internal notes, tags
- CSV export, keyword and date filtering
- (Optional) CRM / Google Sheets / Slack integration

#### G3 Newsletter Subscribers
- Subscriber list, source, status (subscribed / unsubscribed), export, EDM platform integration

---

### 4.8 H. SEO & Marketing

| Feature | Description |
|---|---|
| Site-wide SEO defaults | Title template, default description, default OG image, site name |
| Per-page SEO | Meta title / description / keywords, OG image and text, canonical, noindex toggle |
| Structured data | Automatic output of Organization / SportsTeam / **Event, SportsEvent (schedule)** / Person / Article / Course / BreadcrumbList / **FAQPage** schema |
| sitemap.xml | Generated automatically (including zh / en hreflang), with the ability to exclude pages |
| robots.txt | Editable online |
| 301 redirect management | Old-to-new URL mapping with bulk import |
| Tracking tags | GA4, GTM, Meta Pixel, LINE Tag placement configuration |
| Internal linking suggestions | Flags orphan pages that nothing links to, based on the site hierarchy |

---

### 4.9 I. Site Settings

- **Menu management**: primary menu / mega menu / footer menu, drag-and-drop ordering, multiple levels, external links, bilingual labels
- **Language management**:
  - Enabled languages: Traditional Chinese (default) / English; **retain the ability to add languages** (Japanese deferred for evaluation)
  - **Translation status overview**: a matrix of every content item's zh / en completion status, filterable by "missing English"
  - **UI string table**: bilingual maintenance of buttons, form labels, hints, and error messages
  - Fallback rules: show Chinese or hide the page when a translation is missing
  - Date / number formatting and font settings
- **Contact information**: phone, email, address, opening hours, departmental contacts, social links
- **Venue management**: venue name, address, coordinates, travel notes, photos (used by 5.1 training locations and Location & Map)
- **Shop settings**: whether the store entry is shown, and links to the shopping-guide and returns-policy pages (content maintained in S6). **LINE Pay and invoicing credentials are not here; they live in S6 and are visible to system administrators only**
- **External service links**:
  - **Social platforms**: Instagram [`@tcr_fc_2024`](https://www.instagram.com/tcr_fc_2024), Facebook [`TCRFC2024`](https://www.facebook.com/TCRFC2024), YouTube [`@TCRFC-2024`](https://www.youtube.com/@TCRFC-2024)
  - **Women's team official website** URL (for the 06 outbound link): [`https://www.tcbw2014.com/`](https://www.tcbw2014.com/)
  - EDM platform configuration
- **Global settings**: logo, brand colours, favicon, cookie policy, privacy policy, **membership terms**, maintenance-mode toggle

---

### 4.10 J. System

- **Account management**: create / disable accounts, password policy, two-factor authentication (2FA)
- **Roles and permissions**: role creation with per-feature permission checkboxes (see section 6)
- **Audit log**: who changed what, when (create / update / delete / publish), retained for ≥ 12 months
- **Sign-in log and anomaly alerts**
- **Backups**: daily automatic backups with manual restore points

---

### 4.11 K. Members

#### K1 Member list and detail
- List columns: member number, name, email, tier (free / paid), **membership expiry**, **jersey status**, registration source (website / LINE / in person), LINE link status, registration date, last sign-in, status (active / disabled / unverified)
- Filters: tier, status, registration source, LINE linked or not, registration period, **membership season**, **expiring soon**, jersey status, language preference
- **Duplicate detection**: identifies likely duplicate accounts by email or mobile number and offers merging (membership and payment records transfer with the merge)
- **Member detail**: profile, membership and payment history, card status, jersey registration and fulfilment history, sign-in history, internal notes
- Actions: disable / enable, resend verification email, send a password reset on their behalf, **regenerate the membership card QR token**, add internal notes
- CSV export (**requires additional authorisation** and is written to the audit log)

#### K2 Membership and plans
- **Plan settings**: plan name (zh / en), price, season code, start and end dates, `card_quota` (cards issued), `jersey_quota` (jerseys included), mid-season pricing rule, benefit description, sort order, publish state
- **Activation and renewal**: activate a membership manually, recording payment method (LINE Pay / in person), amount, payment date, transaction note, and handler; the system writes a payment record for reconciliation and audit
- **Automation hook**: an internal endpoint `POST /api/membership/activate` (credential-protected) is reserved for future automated collection, not enabled in this phase
- Expiry reminder list, **end-of-season batch expiry**, and renewal list export
- Manual tier adjustment (with a reason recorded)
- Member number generation rules

#### K3 Jersey fulfilment
- Pending list: member and additional cardholder names, size, collection method (shipping / in person), delivery details
- Status marking: pending / shipped / collected, reflected back to the member
- **Quantity by size** for procurement planning
- Fulfilment list CSV export (permission-gated, written to the audit log)

#### K4 Partner stores and benefits
- **Partner store CRUD**: name (zh / en), photo / logo, category, address (zh / en), phone, opening hours, map link, website or social link, offer (zh / en), **applicable tier** (all members / paying members only), partnership dates, sort order, publish state
- Maintains the category and area filters used by the public 8.4 list
- **Benefits comparison entries**: entry name (zh / en), description (zh / en), group (card / store discounts / jersey / events), value for the free tier, value for the paid tier (tick / cross / text such as "10% off", "one"), sort order, publish state
- The benefits data is **shared by three public placements** — the join page (3.14), the fan club page (8.2), and the upgrade page — plus the Mobile App's upgrade screen from v2.5 (four in total), and this is the single point of maintenance
- **Geographic coordinates (added in v2.5)**: `PartnerStore` gains `lat` / `lng` plus a "locate from address" helper, **saved after human confirmation** (no run-time geocoding). Used by the app's nearby-store distance sorting, see the app specification, section 3.8

#### K5 Prize draw rosters (Fan Club Prize Draw)

> **Positioning**: one of the benefits of paid membership. The system **only builds the eligible roster, freezes it, issues serial numbers, and exports the list**; the **physical draw is performed by people, on site or on a live stream**, and winners are ticked in afterwards in the admin. The system runs **no random-selection algorithm** and produces no random result.
> This `K5` is a **recycled identifier**: v2.0 narrowed module K from K1–K5 to K1–K4, freeing it; it has no relationship to the former K5.

**① Create a draw**
- Fields: draw name (zh / en), prizes and quantities (zh / en, itemised), **eligibility snapshot time** (date + time, defaulting to 00:00 on the day of the draw), draw time, setting (home match day / live stream / other), **collection deadline and how unclaimed prizes are handled**, rules and notices (zh / en), cover image, internal notes
- Status flow: `draft → roster locked → drawn → announced → closed`, plus `voided`
- The rules are **mandatory** and must state: prizes, quantities, eligibility, snapshot time, draw time and setting, collection deadline, and the extent to which the organiser reserves the right to make changes

**② Build the eligible roster (snapshot)**
- The condition is fixed: **tier is `fan_club`, the membership is valid (`paid_until` covers the snapshot time), and the account is active at the snapshot time**. It is **not configurable in the admin**, so that no one can adjust who qualifies
- **The member does nothing**: no entry, no sign-up, no check-in, no points threshold
- On execution the system writes the snapshot in one pass: **consecutive serial numbers 1…N issued in ascending member-number order**, one per person, copying the name, member number, tier, and membership expiry as they stand
- The roster **locks** immediately: **rows cannot be added or removed**. A roster that is wrong can only be **voided and regenerated in full** (`roster_version` +1, with the old version retained for audit)
- The snapshot time, eligible count, roster hash, and operator are recorded, and the action is written to the audit log
- A **dry run** is available beforehand: it returns the eligible count only, writing no data and issuing no serial numbers

**③ Export the draw lists (two CSVs, different permissions)**

| Purpose | Filename | Columns | Permission |
|---|---|---|---|
| **For the draw itself / safe to project** | `draw-<code>-v<version>-public.csv` | Serial number, member number, **masked name**, draw code, roster version, snapshot time | Same level as K1 viewing |
| **Contacting winners (restricted)** | `draw-<code>-v<version>-winners.csv` | Serial number, member number, name, mobile, email, collection method, delivery details, prize | **Separate authorisation**, written to the audit log |

- The restricted export covers **only winners already ticked in**; the full personal data of the entire eligible roster is never exported (data minimisation)
- Both exports record who, when, how many records, and the stated purpose

**④ Drawing the winners (outside the system)**
- The **serial number** drawn physically decides the winner (balls, a ballot box, a wheel, and so on); recording or streaming the draw is recommended as evidence
- The system provides **no** online draw tool, no draw animation, and never decides a winner on the club's behalf

**⑤ Tick in the winners**
- Search the roster by **serial number** and tick the winner, entering the prize name; several can be ticked at once
- The system checks in real time whether the serial number exists, has already won, and belongs to this roster version, and refuses anything that fails
- Ticking in is audited (who, when, whom, what changed); after announcement, any change requires a stated reason
- **Reserves** can be marked, to step in when a winner does not claim in time; reserves are drawn physically and ticked in the same way

**⑥ Prize fulfilment (mirrors K3)**
- Pending list: winner name, prize, collection method (shipping / in person), delivery details
- Status marking: `pending / shipped / collected`, the same states and handling as K3 jersey fulfilment
- **Unclaimed**: automatically marked `overdue` past the draw's collection deadline and handled as the rules state
- Fulfilment list CSV export (permission-gated, written to the audit log)
- **Prizes are always the club's own physical goods**, shipped or collected in person; never tickets, partner-store redemptions, cash, or cash equivalents

**⑦ Announce the winners (handed over to B2)**
- Announcement goes **through B2 News only**: category 7.1 Club News, tagged `Fan Club Prize Draw`. **No new news category is added**
- K5 offers "generate announcement draft", carrying the draw name, prizes, snapshot time, eligible count, and the **masked winners list** (serial number + member number + masked name) into B2 as a draft for communications staff to polish and publish
- The published article is linked back, and the K5 list shows announcement status and a link
- **Announcements are always masked**: never a phone number, email, address, date of birth, or full name
- **No winner notification email, no on-site message, no LINE push** (the five system emails stand). Contacting an individual winner is done by support staff by phone or a written message, outside any system template and never recorded in `EmailLog`

**List and audit**
- Draw list columns: draw name, snapshot time, eligible count, roster version, status, number of winners, number fulfilled, announcement link, creator
- Every roster keeps its full version history; voided versions cannot be deleted

#### Email notifications
- Five system emails only, all with Chinese and English templates: **registration verification, password reset, membership activation confirmation, 30-day expiry reminder, expiry notice**
- Delivery log (recipient, type, time, outcome)
- **Not included**: on-site notification centre, LINE push, newsletter campaigns, notification preference centre. Marketing emails must carry an unsubscribe link

> Parent–student linking, the three-channel messaging centre, and parameterised LINE QR codes with channel performance reporting have been moved out of scope (see 3.14). LINE is retained for sign-in and account linking only.

#### Member data security requirements
- Passwords stored hashed (irreversible), strength rules, lockout after failed attempts
- Viewing member personal data in the admin requires the corresponding permission, and every view and export is written to the audit log
- A defined process and timeframe for member-initiated account deletion (in line with personal-data legislation)
- **Registration by anyone under 18 requires guardian consent**; where an additional cardholder on a family plan is a minor, their name and size are entered by, and are the responsibility of, the primary account holder
- The **LINE link identifier** (a user's account-specific ID) is treated as personal data: stored encrypted, never displayed publicly, never included in general exports
- The **membership card QR token** must not be derivable from the member number; the public verification page `/m/<token>` returns only the first character of the name, the member number, the tier, and validity status, and no other personal data
- Registration and signup forms must state the purpose of data collection and consent terms

#### Personal data and tax requirements for the draw (K5)

- **Collection notice**: the member terms and registration consent (maintained in admin I) must add a notice — "while your membership is valid you will automatically be included in the Fan Club Prize Draw roster; if you win, your name will be published in News in masked form". **No draw may be held until this notice is in place**
- **Masked announcements**: announcements carry only the **serial number, member number, and masked name**. Never a phone number, email, address, date of birth, or full name
- **Prize value and withholding**: prizes won by chance are taxable income. The specification follows **data minimisation** —
  1. where a single prize is **below the withholding threshold**, the system **does not collect the winner's national ID number**; only the name and delivery details are needed;
  2. only where a single prize **reaches the withholding or reporting threshold** are the details needed for a withholding certificate (national ID number, registered address) collected in **restricted fields** on the roster snapshot — **encrypted, masked by default, never merged into the `Member` record, and never present in a general export** — with every view and export written to the audit log and a retention period set to the statutory life of the certificate, after which the data is destroyed;
  3. **non-residents** are subject to different withholding rules with no starting threshold, which must be stated in the rules.
  > The actual threshold, withholding rate, and filing method **must be confirmed by an accountant** (see open item 16 in section 10). **Keeping the first draws' prize values below the threshold is recommended**, as it removes the need to collect a national ID number at all — the cheapest and lowest-risk option
- **Minors**: registration by anyone under 18 continues to require guardian consent; where an additional cardholder on a family plan is a minor, the **prize and any tax certificate go to the adult primary account holder**
- **Image rights**: photographs of a prize being collected or presented that are used in News or on social media require **separate consent for that use**; where the subject is a minor, written guardian consent is required. This extends the club's existing handling of material featuring minors rather than relaxing it
- **Retention of the rules**: each draw's rules, snapshot time, roster snapshot, roster hash, and announcement are retained together, for the same period as the audit log

---

### 4.12 L. Schedule

#### L1 Master calendar
- Displays all matches (C4) and custom club events (L2) in a month view, colour-coded by team
- **Per-team swimlanes**: D1 / U15 / U14 / U12 side by side, making each squad's month and any clashes visible at a glance
- Drag-and-drop date changes that **write back to the match record** (triggering notifications on rescheduling)
- **Clash detection**: warns when the same venue or the same squad is double-booked in a time slot
- Filters: team, competition type, venue, status
- View toggle: month / list

> Programs, camps, specialist training, and academy day-to-day timetables **do not enter the calendar module**; they are maintained separately in P1 / P2.

#### L2 Custom events (club events)
- For non-match public activity: press conferences, signing sessions, fan meet-ups, open training, closure notices
- Fields: title (Chinese / English), team (multi-select, or "Club Event"), start and end (all-day and multi-day supported), venue (linked), description, cover image, external link or CTA, public visibility
- **Recurrence rules**: weekly / fortnightly / monthly, with an end date and exception dates

#### L3 Categories & display settings
- **Team categories**: populated automatically from C1 teams (D1 / U15 / U14 / U12 …); this screen maintains the public display name (Chinese / English), order, colour, and visibility
- Competition types: names, identifying colours, order for league / cup / friendly etc.
- Public defaults: default view (list or calendar), default date range, default selected team
- Default filters for each embeddable block (which teams show on the homepage, D1 fixed on the first-team page, each squad's own page showing that squad)
- **Whether trials sync to the calendar**: a toggle (off by default, keeping trial information on the recruitment pages)

#### L4 Subscription & export
- **iCal subscription URLs**: separate webcal feeds for `all` and **each team (D1 / U15 / U14 / U12)**
- Export: matches within a date range to CSV / .ics
- **Bulk import**: a full season of fixtures via CSV (sharing the mechanism with the C4 import)
- Subscription statistics (subscriber count per team feed)

#### Data consistency principle
- The calendar is an **aggregation layer, not a source of truth**: matches remain owned by the C4 module, and the calendar provides a unified view and editing entry point
- Only L2 custom events are calendar-native data
- When a match time or status changes, members who marked attendance or subscribed are notified automatically (see K4)

---

### 4.13 S. Shop (added in v2.6)

> The letter `S` (Shop) is used. **The former `E4 Product Showcase` is folded into S1** and the `ProductShowcase` type is retired.

#### S1 Products & variants (SKUs)
- **Product**: name (zh/en), collection (club / academy / fan), tags, gallery, product narrative (zh/en), size chart, publication state, ordering, SEO fields (title / description / structured data)
- **Variants (SKUs)**: size × colour combinations, each with its own **SKU code, price, sale price (optional), and stock level**; the cost field is visible to authorised roles only. **No member-price field** (no member discounts this time round)
- Product states: `draft / published / out of stock (derived from inventory) / unpublished`; out-of-stock items can be set to "visible but not purchasable" or hidden automatically; items can be flagged "new"
- Collection copy: the brand narrative block for each collection (this is where 8.3's showcase quality is maintained)

#### S2 Inventory
- Tracked **per SKU**; **stock is reserved and deducted at order placement and released automatically if payment fails or times out**, and restored on cancellation or return
- Receipts, stocktakes, write-offs, and adjustments are all written to `InventoryMovement`, with quantity, reason, and operator traceable for every change
- Low-stock thresholds and replenishment alerts
- **Not included**: multiple warehouses, batch/expiry tracking, pre-orders and backorder scheduling

#### S3 Orders
- List: order number, placed at, amount, payment status, fulfilment status, shipping method, **member or guest**; filterable by status and period
- Detail: items and SKUs, amount breakdown (subtotal / shipping), **LINE Pay transaction ID and payment time**, invoice number, tracking number, customer notes and internal notes
- State machine: `awaiting payment → paid → picking → shipped → completed`, with branches `cancelled` / `return in progress` / `refunded`
- Supports **manual order creation and back-entry** (in-person sales, match-day stalls), flagged as in-person payment
- CSV export **requires separate authorisation and is written to the audit log** (who, when, how many records, stated purpose), mirroring the member-list export rule

#### S4 Fulfilment & shipping
- Printing of picking and packing slips, bulk "mark as shipped", and **tracking numbers entered manually or imported by CSV**
- Convenience-store pickup codes and arrival notices; collection status for in-person pickup (`awaiting collection / collected / expired`)
- **No carrier API integration in v2.6**; whether to integrate is deferred for evaluation

#### S5 Returns & refunds
- Handles return requests raised on the public site (**arriving by form or support email — there is no dedicated returns wizard**): case list, review, goods inspection, refund execution
- **Invoice voiding or credit notes** are processed alongside (a tax requirement, never skipped for policy reasons)
- Refunds always go **back through LINE Pay** (full or partial); in-person orders are refunded manually and logged with the operator
- **Not included**: automated refund scheduling, unconditional full shipping refunds

#### S6 Shop settings & reports
- **LINE Pay configuration and credentials** (Channel ID / Secret, **visible to system administrators only**), sandbox toggle, key-rotation log. **The collecting entity is the club; the Association's merchant account must never be entered here**
- **E-invoicing service configuration and credentials**: invoice track, donation-code list, issue/void retry settings
- **Shipping settings**: a single flat rate plus a free-shipping threshold, outlying islands and non-serviced areas. **No weight-based or banded rates**
- Bilingual maintenance of the store entry and policy content: shopping guide, shipping information, returns policy, terms of sale
- Reports: revenue, order count, average order value, best-selling SKUs, stock, return rate; **exports are written to the audit log**
- **Division of labour with the A dashboard**: A shows summary figures only; detail always lives in S6
- **Not in this module**: discount-code and member-price settings (not built this time), payment-method settings (LINE Pay only)

> **Payments and personal data**: payment always completes on **LINE Pay's** side; **this site renders no card fields and stores no card data** (global premise). Callbacks must be signature-verified and idempotent, and orders whose `Confirm` fails or times out are cancelled with stock released.
> Orders contain the **recipient's name, phone number, and address** and are **treated as member personal data**: full values are visible only to system administrators, support/administration, and fulfilment roles; other roles see masked values, and exports require separate authorisation and are audited.
> **Transaction records and invoices are retained under tax law** (the retention period is an open item in section 10), and that duty **is not extinguished by a data-subject deletion request** — on account deletion an order keeps only the legally required fields and the rest of the personal data is cleared, mirroring the `DrawRoster` approach.
> **Shared admin with the charity platform's `N` module, but fully separated books**: both use LINE Pay, but the collecting entity, **merchant account**, credentials, invoice track, and statements are all separate. **Wrong credentials mean money reaching the wrong legal entity**, so S6 must label this module as the club's.

---

## 5. Data Model & Content Types

| Type | Description | Key relationships |
|---|---|---|
| `Page` | Static page (with blocks); **the women's football page is also this type** | SEO, languages |
| `Article` | News and stories | Category, Tag, Player, Team, Match, Program |
| `Team` | Team, carrying the **team code**: `D1` (= First Team) / `U15` / `U14` / `U12`; unused by the women's team for now | Player, Coach, Match, Season, CalendarEvent |
| `Player` | Player | Team, Article, Stats, Pathway |
| `Staff` | Coaches and staff | Team, Program |
| `Match` | Match. **v2.5 adds** `opponent_en` / `venue_en`, and promotes `competition` / `status` to formal fields | Team, Season, Article (match report) |
| `Standing` | League table | Season, Team |
| `Achievement` | Honour | Team, Season |
| `Milestone` | Milestone | — |
| `Program` | Course / camp / specialist training | Session, Staff, Venue, Partner |
| `Session` | Session / intake | Program, Venue, Registration |
| `Registration` | Registration. **v2.5 adds a nullable `member_id`** — non-members may still register | Session, Contact, Member |
| `Trial` | Trial session | Team, Venue, Registration |
| `Partner` | Partner: name (zh/en), logo (**dark and light variants**), type, country, partnership content and dates, website link, display order, whether shown in the footer / homepage | Article, Program |
| `Sponsor` | Sponsor: name (zh/en), logo (both variants), **tier** (title / official / supporting), contract dates, sponsorship content, contact, expiry reminder, display order | SponsorPackage, Article, Advertiser |
| `SponsorPackage` | Sponsorship package (9 types): name (zh/en), content, benefit list, target audience, price band (**may be withheld**), order, publication state | Enquiry |
| `Product` | **Product**: name (zh/en), collection, tags, gallery, narrative, size chart, state, ordering, SEO fields. **Replaces the retired `ProductShowcase` in v2.6** | ProductVariant, OrderItem |
| `ProductVariant` | **Variant (SKU)**: size / colour, SKU code, price, **sale price (optional)**, cost (restricted), stock level | Product, InventoryMovement, OrderItem |
| `InventoryMovement` | Stock movement: type (receipt / sale / return restock / stocktake / write-off / adjustment), quantity, reason, operator, timestamp | ProductVariant, Order |
| `Cart` | Cart: owner (member or anonymous token), line items and quantities, updated at; merged into the account cart on sign-in | Member, ProductVariant |
| `Order` | **Order**: order number, `member_id` (**may be empty — guest checkout is supported**), recipient name / phone / address (**restricted fields**), amount breakdown (subtotal / shipping), **LINE Pay transaction ID and payment status**, shipping method and fulfilment status, invoice number, lookup token | Member, OrderItem, Shipment, StoreInvoice |
| `OrderItem` | Order line: **SKU snapshot** (product name, variant, unit price, all **value-copied**), quantity, line total | Order, ProductVariant |
| `Shipment` | Shipment: method, tracking number, shipped and delivered timestamps, pickup store code, collection status | Order |
| `RefundRequest` | Return/refund request: items, reason, status (requested / under review / approved / refunded / rejected), refund amount and method, invoice voiding or credit-note record | Order |
| `StoreInvoice` | **E-invoice**: number, issued at, **carrier / tax ID / donation code**, issue result and retries, voiding and credit-note status. **Made out to the club**, on a different invoice track from the Association's charity invoices | Order |
| `ComicEpisode` / `ComicCharacter` | Manga episode / character | Player (inspiration) |
| `FanEvent` | Fan event | Member |
| `Enquiry` | Form submission (7 form types + deck downloads + donation enquiries) | Form, Assignee |
| `Venue` | Venue. **v2.5 adds `lat` / `lng`**, for the app's venue navigation and course locations | Program, Match, Trial |
| `MediaAsset` | Media asset | Global |
| `Faq` / `FaqCategory` | FAQ / topic category | Page (embed location) |
| `CharityProgram` | Charity programme | Charity, Partner, Article, ImpactRecord |
| `ImpactRecord` | Impact record (organisation, donation, imagery) | Charity, CharityProgram |
| `Charity` | Recipient organisation (name, description, logo, website) | CharityProgram, ImpactRecord |
| `Donation` | Donation record. **Defined in the [Charity Donation Platform Specification](TCRFC_Charity_Donation_Platform_Specification_EN.md) §9**; this site creates no donation records and only aggregates them for 11.4 impact metrics | CharityProgram, DonationProject |
| `ImpactMetric` | Impact statistic | CharityProgram |
| `Member` | Member account: tier (`registered` / `fan_club`), member number, **card token**, membership dates, jersey size and fulfilment status, registration source, **LINE link identifier** (encrypted) | MembershipPlan, MembershipPayment, FanEvent, DrawRoster |
| `MembershipPlan` | Membership plan: price, season, term, `card_quota`, `jersey_quota`, mid-season pricing rule | Member, MembershipPayment |
| `MembershipPayment` | Membership payment and activation record: method, amount, date, transaction note, handler, activation dates | Member, MembershipPlan |
| `MembershipBenefit` | Benefits comparison entry: group, free-tier value, paid-tier value, sort order (shared by 3.14 / 8.2 / upgrade page) | MembershipPlan |
| `PartnerStore` | **Partner store**: category, address, phone, opening hours, map link, offer, applicable tier, partnership dates. **v2.5 adds `lat` / `lng`** (saved in admin K4 after human confirmation) for the app's nearby-store distance sorting | — |
| `MemberDraw` | **Fan Club Prize Draw**: name (zh / en), prizes and quantities (zh / en), **eligibility snapshot time `snapshot_at`**, draw time and setting (on site / live stream), collection deadline and unclaimed handling, rules and notices (zh / en), status (draft / roster locked / drawn / announced / closed / voided), `roster_version`, `total_count` eligible members, `roster_hash`, the linked announcement article, creator and locker | Member, DrawRoster, Article |
| `DrawRoster` | **Eligible roster snapshot (one row per eligible member)**: `serial_no` draw serial number (issued consecutively in ascending member-number order at snapshot time, one per person), member number, **name snapshot**, tier snapshot, membership expiry snapshot, won or not, prize name, collection method (shipping / in person), fulfilment status (pending / shipped / collected / overdue), **withholding details (collected only above the threshold; encrypted, masked by default)**, notes. **Written by the system in one pass at the snapshot time — members cannot create rows; once locked, rows cannot be added or removed and only the win and fulfilment fields may be filled in** | MemberDraw, Member |
| `EmailLog` | Delivery record for the five system emails | Member |

> **Mobile App types** (`AdSlot` / `Advertiser` / `AdCampaign` / `AdCreative` / `AdEvent` / `AdDailyStat` / `AppDevice` / `PushTopicSubscription` / `PushMessage` / `AppRelease`) are **not in this table**; see [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) section 10.
>
> **Five "commercial counterparties" must not be conflated**: `Partner` (B2B logo wall) / `Sponsor` / `PartnerStore` (member discounts, no payments, no revenue share) / `DonationStore` (charity site scan-in, **payments and revenue share**) / `Advertiser` (**app advertiser, impressions counted**). One real company may be several of these at once — **create a separate record for each, never share one**. The only exception is `Advertiser.sponsor_id`, which links back to a `Sponsor` to avoid maintaining duplicate contacts; it is **a link, not a merge**.
| `CalendarEvent` | **Calendar event (aggregate view)**: points at a Match via `source_type` + `source_id`, or is a `custom` club event; carries **`team_codes[]` (D1 / U15 / U14 / U12)** as its first-level category | Match, Team, Venue |
| `EventType` | Match / event type (icon, colour, display rules) | CalendarEvent |

> Every type with a public-facing presentation must support **zh / en bilingual fields**, with room to add a third language.
> `CalendarEvent` should be implemented as a **view or index table** rather than duplicated data, keeping it in sync with its source module and avoiding two sources of truth.
> `DrawRoster` is the opposite — **immutable snapshot data**, deliberately copying values from `Member` rather than resolving a foreign key: a member who later changes their name, merges accounts, or asks for deletion must not alter a locked historical roster, so that the audit trail of who was eligible at the draw survives. On account deletion the snapshot keeps only the member number and a masked name; everything else is erased.

---

## 6. Roles & Permissions Matrix

| Role | Content | FAQ | Charity | Teams / Matches | Programs / Registrations | Schedule | Members | Business / Sponsors | Shop | Advertising | Mobile App | Enquiries | SEO / Settings | System |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| System administrator | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full |
| Content editor | ✔ Edit / publish | ✔ Full | ✔ Edit | Read-only | Read-only | Custom events | — | Read-only | S1 copy / imagery | — | — | M2 composition | Per-page SEO | — |
| Football / team manager | Draft | Relevant topics | — | ✔ Full | Read-only | Match events | — | — | — | — | — | — | — | — |
| Academy / programs manager | Draft | Relevant topics | — | Academy squads | ✔ Full | Squad matches | — | — | — | Program enquiries | — | — | — | — |
| Commercial / sponsorship | Draft | Relevant topics | Read-only | Read-only | Read-only | Read-only | — | ✔ Full | S1 / S6 (**order PII masked**) | Partnership / sponsorship enquiries | **✔ Full** | — | — | — |
| PR / media | ✔ Edit | Read-only | ✔ Edit | Read-only | — | Custom events | — | Read-only | Read-only | Media enquiries | Reports (read) | M3 draft (**needs approval**) | — | — |
| Support / administration | — | ✔ Edit | — | — | Registration handling | Read-only | ✔ View / handle | — | **✔ S2–S5** | ✔ Full | — | M4 view (masked) | — | — |
| Translator ※ | Translation fields only | Translation fields only | Translation fields only | Translation fields only | Translation fields only | Translation fields only | — | Translation fields only | Translation fields only | — | Creative translation fields | Push copy translation | UI string table | — |
| Viewer | Read-only | Read-only | Read-only | Read-only | Read-only | Read-only | — | Read-only | Read-only (no amounts) | Read-only | Read-only (no amounts) | Read-only | — | — |

**Additional rules**:
- **App releases and forced updates (M1) are restricted to system administrators** — a wrong minimum supported version locks every user out.
- **Push sending (M3) requires two-person approval**: PR/media may draft and preview; a system administrator must approve the send. Push is an irreversible, audience-wide action.
- **Push tokens and device identifiers (M4) count as personal data**; only system administrators see full values, other roles see masked ones.
- **Advertising contract amounts** are visible only to commercial/sponsorship and finance; advertising report exports must be written to the audit log (who, when, which flight, stated purpose), mirroring the member-list export rule.
- **Order recipient data (name / phone / address) is treated as member personal data**: full values are visible only to system administrators, support/administration, and fulfilment roles; other roles see masked values, and order exports require separate authorisation and are audited.
- **Refund execution (S5) and shop credentials (S6: LINE Pay and invoicing) are restricted to system administrators** — a refund is an irreversible movement of money, leaked credentials amount to losing the shop, and a wrong merchant account sends money to the wrong legal entity. Commercial/sponsorship may work on products and reports but **gains no access to order personal data and no refund rights**.
- **Member module (K) permissions are not widened by the Mobile App or the shop**: still restricted to system administrators and support/administration.
- Content editing follows a **submit-for-review → publish** workflow; only designated roles may publish.
- **Member personal data (email / phone / date of birth / minors' details) is restricted**: only system administrators and support/administration staff see it in full; other roles see masked values (e.g. `a***@gmail.com`).
- **Exporting** member lists requires separate authorisation, and every export is written to the audit log (who, when, how many records, stated purpose).
- **Draw rosters (K5) are treated as member personal data**: building and locking a roster is limited to system administrators and support/administration staff, and the restricted winners export requires separate authorisation and is audited. Communications staff writing the announcement receive **the masked roster only**, and gain no access to the member module by doing so.
- ※ The translator role may edit `en` fields only, and may not modify the Chinese source or publication status.
- **Calendar permissions follow the source module**: which events a user can edit on the calendar depends on their permissions over the underlying match data (e.g. an academy manager may reschedule their own squad's fixtures but not the first team's).

---

## 7. SEO / GEO & Multilingual Plan

Implementing each of the nine "GEO & SEO FOUNDATION" fundamentals:

| Fundamental | Implementation |
|---|---|
| Clear site architecture and hierarchy | URLs mirror the site hierarchy, e.g. `/en/academy/join/` |
| Keyword-led content strategy | Each page can be assigned a primary keyword in the admin, with usage checks (title / H1 / opening paragraph) |
| Structured data / schema markup | Automatic output of Organization, SportsTeam, SportsEvent, Event, Person, Article, Course, FAQPage, BreadcrumbList, **plus Product and Offer (v2.6: the shop is on this site, so Product schema — price, currency, availability — is now emitted here)** |
| Internal linking strategy | Articles can be related to players / teams / programs, generating cross-link blocks automatically; orphan-page detection |
| Mobile-friendly design | Mobile-first, touch targets ≥ 44px, mobile CTA bar |
| Fast loading | WebP images with lazy loading, CDN, inlined critical CSS; targets of LCP < 2.5s, CLS < 0.1, INP < 200ms |
| High-quality original content | Player stories, match reports, manga content, and **charity impact records** are the differentiating assets |
| Regularly updated news and stories | Scheduled publishing plus a content calendar view in the admin |
| Multilingual support | **Traditional Chinese / English**: `hreflang="zh-Hant" / "en"` + `x-default`, distinct URLs, and language switching that stays on the equivalent page |

**Suggested priority scope for the English edition**: homepage, 02 About, 03.4 International Pathways, 04 Academy, 09 Partners & Sponsors, 10 contact forms, 12 FAQ, 13 Schedule.

> A Japanese edition is **deferred for later evaluation** and is not implemented here; the data structure and URL rules must nonetheless leave room for it, so enabling it later requires no re-architecture.

**GEO (generative engine optimisation) recommendations**:
- **The standalone FAQ section (12) is the core GEO asset**: the Q&A format is the easiest for AI engines to extract and cite, so FAQPage schema must be emitted completely
- Give every page a clear H2/H3 structure and an FAQ block, making it easy for AI to summarise
- Present key facts (founding year, squads, venues, contact details, charity impact figures) both as structured data and as explicit text
- Maintain an `llms.txt` describing the site's key content and licensing terms

---

## 8. Non-Functional Requirements

| Aspect | Requirement |
|---|---|
| Performance | Homepage LCP < 2.5s (4G), Lighthouse Performance ≥ 85 |
| Compatibility | Latest two versions of Chrome / Safari / Edge / Firefox; iOS 15+, Android 10+ |
| Accessibility | WCAG 2.1 AA |
| Security | Forced HTTPS, 2FA on the admin, CSRF / XSS / SQL injection protection, upload type and size limits, optional admin IP allowlist; **member system**: password hashing, session timeout, lockout after failed sign-ins, brute-force protection; **shop**: checkout redirected to the payment provider's hosted page or SDK, **no card data stored**, payment callbacks signature-verified and **idempotent**, order-lookup tokens non-derivable and time-limited, dual authorisation for refunds and exports |
| Personal data | Registrations, enquiries, **member data** (including LINE link identifiers), and **order recipient data** stored encrypted, with a retention policy and a data-subject deletion process; **minors' data requires guardian consent**. **Transaction records and invoices carry a statutory retention duty that takes precedence over deletion requests**: on account deletion an order keeps only the legally required fields and the rest is cleared |
| Availability | 99.5% uptime target; daily backups retained off-site for 30 days |
| Extensibility | Content types must be extensible (new age groups / seasons / program types / languages without code changes); if the women's team is later upgraded to a full team area, the existing team module can be reused directly; **the shop's payment provider, invoice provider, and shipping methods must be swappable — no provider's specifics may be hard-wired into the order flow** |
| Monitoring | Error tracking (Sentry-class), uptime monitoring, alerts on form submission failures; **the shop additionally needs alerts on payment failure rates, failed payment callbacks, and negative stock** |

---

## 9. Delivery Phases & Priorities

### Phase 1 — Brand foundation and conversion (MVP, approx. 8–10 weeks)
- Homepage, 02 About, 03.1 First Team (basic), 04 Academy (4.1 / 4.2 / 4.7), 05 Programs (5.1 / 5.2)
- **06 Women's Football page** (single page, low cost, delivered alongside)
- 07 Newsroom (all categories), 10 Forms hub (all 9), Location & Map
- **12 Standalone FAQ section** (starting with 3–4 high-frequency topics)
- **13 Schedule**: **team-first categorisation by D1 / U15 / U14 / U12**, fixtures/results toggle, list and calendar views, per-match .ics download
- Admin: content management, news, media library, teams / players / coaches, programs and registrations, FAQ management, master calendar and custom events, enquiry inbox, SEO basics, permissions
- **Multilingual framework** (Chinese content launches first, with English fields and URL structure in place, and room for a third language)

### Phase 2 — Commercial, membership, and deeper content (approx. 6–8 weeks)
- The full 09 Partners & Sponsors area (including deck downloads and lead tracking)
- 03.2–03.5 Player Development, International Pathways, Player Stories
- **The full 11 Charity & Impact area**
- 05.3–05.5 Winter Camp, Specialist Training, School & Community
- **Member system (module K)**: email registration and sign-in, one-tap LINE sign-in and linking, two membership tiers, **digital card and public verification page**, **benefits comparison table**, **partner store directory (8.4)**, upgrade and renewal flows, jersey registration
- **Advanced schedule**: per-team subscription URLs (webcal), member "I'm attending" and reminders, admin swimlane view / clash detection / drag-to-reschedule
- Admin: business modules, charity module, trial management, advanced registration (waitlists / exports / attendance sheets), **member management K1–K4 (list / membership and plans / jersey fulfilment / partner stores and benefits)**, advanced calendar
- **English content goes live**

### Phase 3 — Culture and community (approx. 6 weeks)
- 08 TCRFC Culture: manga reader, fan club 8.2 (paid membership introduction and join page, integrated with the member system)
- **The on-site official store (8.3 + admin module S, added in v2.6)**: products and SKUs, inventory, cart, checkout, **LINE Pay integration**, **e-invoicing**, fulfilment and shipping, order lookup, returns and refunds, shop reports. **Scoped to match the old site's existing store** — no member pricing, discount codes, or extra payment methods, so the effort is far below a full e-commerce build. **Depends on the member system (Phase 2) being live**; **the club's LINE Pay merchant account and the invoicing service must be onboarded from Phase 2 onwards** or they will block this phase
- **Retirement of the old store**: the old site's Wix store closes when the on-site shop launches, and its product and category URLs are 301-redirected (bulk import in module H)
- **Member management K5 Prize draw rosters**: roster snapshots and serial numbers, the two CSV exports, ticking in winners, prize fulfilment, and handing the announcement to B2. (Eligibility itself is a K4 benefits entry and ships with the Phase 2 benefits table)
- League tables, automatic aggregation of player statistics
- Newsletter integration

### Phase 4 — Optimisation and expansion (ongoing)
- FAQ performance optimisation, zero-result search feedback loop
- Deeper analytics dashboards, A/B testing, personalised recommendations
- Optional evaluations: ticketing, loyalty points, **partner-store scan-to-redeem with performance reporting**, **carrier API integration**, **moving membership fees into shop checkout**, expanding women's football into a full team area
- **Cancelled**: `Shopify Storefront API product sync` falls away with the external store in v2.6
- **Removed from optional**: `LINE Pay API checkout` was confirmed in v2.5 — **in-app membership payment uses LINE Pay** (see the app specification, section 5); this site's public web front end still does not
- **Mobile App**: a separate project with its own phasing, see [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) section 15

> Note: **from v2.6 e-commerce and payments are part of Phase 3** (the earlier plan excluded them because all shopping went to Shopify — that premise has lapsed). This is **the largest single increase in Phase 3 effort** relative to the previous version, and it introduces this site's first outbound collection of money. The member system's scope was narrowed in v2.0 to "membership × partner discounts × jersey", reducing its effort relative to v1.9; v2.3 added draw eligibility, but the system only builds and exports rosters, so the increase is small.

---

## 10. Open Items

### Decisions already made

| Item | Decision |
|---|---|
| Technology selection | Deferred; this document defines functional requirements only (the app specification follows the same premise, expressed as "platform capability requirements") |
| Payments and shopping | **From v2.6 this site runs its own shop** (8.3 / admin module S); the former premise that "all shopping is routed to Shopify" **has lapsed**. Payments now divide **four ways**: ① **shop checkout on this website takes payments, by LINE Pay only** (collected by **this club**); ② **membership fees still do not go through on-site checkout** (payment links and in-person collection); ③ the **Charity Donation Platform** integrates LINE Pay, collected for by the **Association**; ④ the **Mobile App** integrates LINE Pay for **membership fees only**, collected by **this club**. **All four flows use LINE Pay, but merchant accounts, credentials, and invoice tracks are never shared between entities**; **"no card data" applies globally**, with payment always completing on LINE Pay's side |
| On-site shop (added in v2.6) | **Yes, at the same functional scale as the old site's store.** Products and SKUs, inventory, cart, checkout, **LINE Pay payment**, **e-invoicing**, fulfilment and shipping, order lookup, and returns are all handled here, under the `S. Shop` admin module (S1–S6); `E4` is folded into `S1` and `ProductShowcase` is retired. **Guest checkout is supported.** ✗ **Payment methods other than LINE Pay** ✗ **Member pricing and discount codes** (paid membership includes no merchandise discount) ✗ **Weight-based or banded shipping rates** (flat rate plus free-shipping threshold only) ✗ Subscriptions / recurring billing ✗ Cross-border and multi-currency ✗ Marketplace / multi-vendor ✗ Resale or consignment ✗ Points redemption and e-wallet ✗ Ticketing and match packages ✗ Tying merchandise to membership or draw eligibility ✗ Carrier API integration ✗ Product reviews and ratings |
| Advertising slots | **This site's public web front end** has no separate fixed slots; seasonal content uses the hero carousel or news. **The Mobile App sells its own advertising slots with performance measurement** (admin E5–E7): **no third-party ad networks, no advertising identifiers, no behavioural targeting, no splash ads**. **Neither the website nor the app carries charity slots** |
| Push notifications | **This site's public web front end** has no notification centre, no notification preferences, and no LINE push. **The Mobile App provides native push (APNs/FCM) and an in-app notification centre.** **The five system emails are unchanged**; **push must never be used for individual winner notifications** |
| Mobile App | **Yes**, with its own specification, [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) (v1.0): **shares this site's admin and database**, adding admin module `M` and `E5–E7`. Both the operating entity and the collecting entity are **this club**, entirely separate from the Association's charity platform |
| Languages | **Traditional Chinese (default) / English**; Japanese is out of scope, with the architecture left extensible |
| Member system | **Required**, delivered in Phase 2. Scope narrowed to **membership** alone: a free tier and a paid tier, with partner-store discounts for members and a jersey for paying members. Signup channels are **email registration + one-tap LINE sign-in** (no Google) |
| Out of scope for the member system | ✗ Loyalty points ✗ E-wallet ✗ Ticketing and match packages ✗ Store scan-to-redeem and redemption reports ✗ **Parent–student linking (website, app, and admin alike)** ✗ Web notification centre and LINE push<br>**The two v2.5 exceptions (Mobile App only)**: ✔ Form pre-filling ✔ My bookings (`Registration.member_id`). This site's web front end still does neither<br>**One item removed in v2.6**: "✗ Shopify SSO" lapses with the external store — the shop is on this site, the **member account is the shop account**, and the Member Centre gains "My orders" |
| Paying-member prize draw | **Built**. Eligibility follows membership **automatically, with no sign-up** (every paying member valid at the snapshot time is included); the system only **freezes the eligible roster, issues serial numbers, and exports a CSV**, while the **physical draw is performed by people, on site or on a live stream** and winners are ticked in afterwards. Winners are announced **through News only** (masked); prizes are **physical items**, shipped or collected in person, fulfilled as jerseys are. ✗ System-run random selection ✗ Public draw page or "my draws" ✗ Winner notification emails and push ✗ Paid entries ✗ Ticket or store-redemption prizes |
| Membership term | **Season-based** (e.g. 2026/27); everyone expires together and renewals are handled at season end |
| Membership fees | **LINE Pay** (payment link / official account invoice) and in-person payment; **membership does not go through shop checkout**, and the admin activates it after reconciliation |
| How discounts are used | Members **show the digital card** in store and staff check it visually; the QR points to a public read-only verification page. **No scan-to-redeem, no redemption counts, no store-side account or software** |
| LINE Official Account | **Already exists** — integrate with the current account; no new application needed. Used here for sign-in and linking only |
| Member offers | Partner-store discounts are maintained on this site (8.4). **The on-site shop gives members no discount this time round** — no member pricing, no codes; paid membership keeps **partner-store discounts and the jersey**. The former "issue Shopify discount codes" approach lapses with the external store. Adding a merchandise discount later requires amending this assumption |
| Fan donations | **Moved to a separate Charity Donation Platform** (**organised and collected for by 台灣足球策略發展協會**, on its own domain, sharing this site's admin and database), entered through partner venues' QR codes, integrating LINE Pay and issuing e-invoices or donation receipts. Section 11 here is editorial and referral only, and the CTA must name the recipient — see [`TCRFC_Charity_Donation_Platform_Specification_EN.md`](TCRFC_Charity_Donation_Platform_Specification_EN.md) |
| Volunteer signup | **Not built**. The section 11 CTA narrows from three routes to two; any volunteering need is handled by the general contact form (10.7) |
| FAQ | Standalone section 12, centrally managed and embedded across pages |
| Charity records | Section 11; every record carries three core data points — **charity organisation name, what was donated, event photography** |
| Women's football | A single introductory page **routing to the women's team website**; this site does not maintain their roster or fixtures |
| Match data | **Entirely manual**, no external API integration; CSV bulk import provided |
| Schedule | **Match-centric**; academy courses, camps, and specialist training are excluded. Calendar content must be **bilingual** |
| TCRFC manga | **Entirely free and public**, with no paywall and no sign-in |

### Still to confirm

1. **Retiring the old Wix store, and the redirects**: v2.6 confirms an on-site shop, so **Shopify is no longer the referral target** and that open item is closed. What remains open: when does the old Wix store stop selling, how are its outstanding orders and stock handed over before the on-site shop launches, and who signs off the 301 mapping for the five `/product-page/…` and `/category/…` URLs?
2. **Initial partner store roster**: this site does not do ticketing or match packages, so the value of paid membership rests on **store discounts and the jersey**. The **number and quality of the launch roster directly determines whether the paid tier is viable** and must be secured before launch (see 8.4).
3. **Annual fee and plan design**: price of the individual plan? Is there a family plan (1 adult + N children)? How many jerseys does each include?
4. **Season dates and mid-season pricing**: membership runs by season, so the season start and end dates need confirming, along with whether mid-season joiners are charged pro rata.
5. **The club's own LINE Pay merchant account**: settled in v2.5 — the website keeps payment links and in-person collection, while **the Mobile App uses LINE Pay API checkout**. What remains open is **onboarding the club's own merchant account** (the charity platform uses the Association's, which **must not be shared**) and the App Store in-app-purchase determination; see [`TCRFC_Mobile_App_Specification_EN.md`](TCRFC_Mobile_App_Specification_EN.md) section 16.2.
   Note: the Charity Donation Platform is **already committed to a proper LINE Pay API integration**; once merchant onboarding is done, whether membership fees reuse it can be assessed separately.
6. **Member number format**: `TCR-<season>-<serial>` suggested; to be confirmed.
7. **Jersey size chart and stock levels**: the size range offered to members (adult / youth) and the stocking strategy by size.
8. **Donation receipts and fundraising eligibility**: now specified in the [Charity Donation Platform Specification](TCRFC_Charity_Donation_Platform_Specification_EN.md) §5 (both e-invoices and donation receipts, chosen per project). **Fundraising eligibility and legal entity are a launch precondition for that platform**, and now turn on **台灣足球策略發展協會**, to be confirmed by the Association with its accountant and legal advisers — see §11 and §13 of that document.
9. **Migration scope for the existing site**: which content on [www.tcrfc.tw](https://www.tcrfc.tw/) should be kept, rewritten, or discarded? For news, keeping the last 1–2 years is recommended.
10. **How English content will be produced**: supplied by the club, outsourced for translation, or launched for priority pages first? This affects the Phase 2 timeline.
11. **Whether charity amounts are published**: organisation name, donation content, and imagery are confirmed; monetary amounts are private by default — are there specific cases that should be public?
12. **Whether trials appear in the calendar**: off by default (kept on the recruitment pages); can be enabled in the admin if desired.
13. **Ticketing and broadcast information**: are there ticketing channels or broadcast platforms to display on fixture cards?
14. **Personal data retention period**: §3.10 and §9 both require forms to display a retention notice and for a retention policy to be defined, but **the actual duration is unspecified**. All seven forms need it; it is a personal-data compliance item and must be confirmed by the club and its legal advisers.
15. **Manga release cadence**: the publishing rhythm of episodes, which shapes the "latest episode" block and homepage exposure.
16. **Prize value ceiling and the withholding threshold**: should a single prize be kept deliberately below the withholding threshold, removing the need to collect a winner's national ID number? The actual threshold, the withholding rate, any obligation to report even where withholding does not apply, and the rules for non-residents must be confirmed by an **accountant** and written into the draw rules (see 4.11 K5).
17. **Draw frequency and setting**: once a quarter, once a season, or alongside particular home matches? Will draws be streamed and the recording kept as evidence?
18. **Collection deadline and unclaimed prizes**: how many days? Once the deadline passes, does a reserve step in, is the prize redrawn, voided, or rolled into the next draw? This must be stated in the rules.
19. **Collection and tax certificates for minors who win**: where an additional cardholder on a family plan wins, the prize and any tax certificate go to the adult primary account holder (this specification's default) — does that match the club's practice?
20. **Approval of the draw rules and member terms**: the template rules and the collection notice to be added to the member terms must be approved by **legal advisers** before the first draw is held.

**Added in v2.6 (the on-site shop)**

21. **The club's LINE Pay merchant account**: the payment method is settled — **LINE Pay only**. What remains open is **the merchant account itself**: the collecting entity is **the club** and it **must never share the Association's charity account**; the shop and the Mobile App's membership payments both belong to the club, so it must be decided whether they share one merchant account (if so, orders need a prefix or product naming that keeps reconciliation readable), along with fees and the settlement cycle. **Onboarding has a lead time and must start during Phase 2**, or it will block Phase 3.
22. **How e-invoices are issued**: **LINE Pay does not issue invoices**, so it must be decided whether to register directly with the Ministry of Finance e-invoice platform or use an invoicing service provider. Invoice tracks, the donation-code list, and the voiding/credit-note process need confirming, and **the track must not be shared with the Association**.
23. **Shipping methods and the flat rate**: which of home delivery, convenience-store pickup, and in-person collection are offered? Which carrier, what **flat shipping fee**, and what **free-shipping threshold**? Are outlying islands served? (No weight-based or banded rates this time round.)
24. **The actual returns policy**: the seven-day cooling-off period is statutory, but the **exclusions** (customised goods; whether socks and similar items are excluded), who pays return shipping, and whether exchanges are offered must be decided by the club and cleared by legal advisers before the policy page is written.
25. **Fulfilment staffing and space**: who picks, packs, and ships? How often (daily, or a few times a week)? Where is stock held? **This determines the S4 workflow and the delivery-time promise on the public site.**
26. **Launch assortment and stock levels**: only a jersey and performance socks are on sale today. How many SKUs at launch, how much stock per size and colour, and when do the Academy and Fan collections get products?
27. **Statutory retention period for transaction records**: orders, invoices, and refund records must be retained for several years under tax law; the actual period and "which fields survive an account deletion" must be confirmed by the accountant and legal advisers and written into the retention policy (handle together with item 15).
28. **Whether membership fees should move to shop checkout**: now that payments exist on the site, should membership be sold online and activated automatically? **Deferred in v2.6** (payment links and in-person collection stand). Adopting it requires **amending the assumption in this section first**, following the established procedure, and assessing the overlap and reconciliation against the Mobile App's LINE Pay flow.

---

*The functionality in this specification can be adjusted and extended to suit actual needs, ensuring the best possible user experience and search visibility.*
