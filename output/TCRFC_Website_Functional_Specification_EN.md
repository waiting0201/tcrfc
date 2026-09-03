# TCRFC — Official Website Functional Specification (Public Site & Admin CMS)

> **Document version**: v1.9
> **Date**: 2026-08-14 (v1.9 revision: 2026-09-03)
> **Brand promise**: LOCAL ROOTS. GLOBAL PATHWAYS.
> **Note**: This is the English edition of *TCRFC 前後台功能規劃書 v1.9*. Section numbering matches the Traditional Chinese edition 1:1.

> **v1.9 revision summary**
> 1. The club mark must always be one of the three lockups extracted from `reference/TCR_logo_CMYK.ai` (mark / mark + TCRFC "English" lockup / mark + TCRFC + 台中磐石足球俱樂部 "Chinese" lockup). **Never set the club name in type alongside it, and never add "SINCE" or a founding year.**
> 2. The Chinese short name is always **台中磐石**, never 磐石 on its own.
> 3. The first-team squad list is **ordered by squad number, with no position grouping or filter**.
> 4. The homepage stat strip drops the two 2024 figures (founding year, national Division 2 title).
> 5. Section 10 forms merged from nine to seven (10.2 + 10.3, and 10.6 + 10.7); numbering closes up.
> 6. `TAICHUNG ROCKS FOOTBALL CLUB` is a brand slogan, **not the club's formal English name**, which remains `Taichung Cornerstone RFC`.

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

- **Public site**: 13 top-level sections, ~60+ sub-pages, 7 CTA conversion forms, a **Member Centre**, and **two languages (Traditional Chinese / English)**.
- **Admin CMS**: content management, teams & fixtures, registrations & rosters, **member management**, FAQ management, charity impact records, enquiry inbox, merchandise & partners, SEO & site settings, permissions & audit.
- **Out of scope for this engagement**:
  - **E-commerce and payments** — all shopping is routed to **Shopify**. This site builds no cart, integrates no payment gateway, and manages no orders or inventory; it maintains a product showcase and outbound links only (see 3.8 / 4.6).
  - **Technology selection** — this document defines functional requirements only; it does not decide framework, CMS, or hosting.
  - Ticketing, member points, and e-wallet (recommended for later evaluation).

### 1.4 Existing digital assets

The following assets must be inventoried before launch to determine migration scope and post-launch traffic routing:

| Asset | URL | Treatment |
|---|---|---|
| Current official website | [www.tcrfc.tw](https://www.tcrfc.tw/) | **Content migration source**: existing copy, news and images to be inventoried and imported; set 301 redirects from old URLs to their new counterparts at launch to preserve SEO equity |
| Instagram | [@tcr_fc_2024](https://www.instagram.com/tcr_fc_2024) | Linked in footer and contact page; recent posts may be embedded on the homepage or news pages |
| Facebook | [TCRFC2024](https://www.facebook.com/TCRFC2024) | Linked in footer and contact page; events and news cross-posted |
| YouTube | [@TCRFC-2024](https://www.youtube.com/@TCRFC-2024) | Linked in footer; videos embedded in team pages, match highlights, player stories, manga animations |
| Women's team official website | [Taichung Blue Whale](https://www.tcbw2014.com/) | Outbound target for section 06 Women's Football |
| Shopify store | TBC | Outbound target for the 08.3 product showcase |

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
├── 08 TCRFC CULTURE                  (8.1 ~ 8.3)
├── 09 PARTNERS & SPONSORS            (9.1 ~ 9.4 + 2 CTAs)
├── 10 JOIN / CONTACT                 (10.1 ~ 10.7 + map / contact info)
├── 11 CHARITY & IMPACT               (11.1 ~ 11.4)
├── 12 FAQ                            (standalone section, cross-topic)
├── 13 SCHEDULE                       (fixtures and results by team)
└── MEMBER CENTRE                     (authenticated area, entry point at the right of the header)
```

### 2.1 Page-type taxonomy

| Type | Description | Admin handling |
|---|---|---|
| **Main page** | Homepage | Modular slot composition (page builder) |
| **Main category** | Landing page of each top-level section | Fixed template + editable blocks |
| **Sub-page** | 2.1, 3.1, etc. | Fixed template + editable blocks |
| **Content / item page** | Players, coaches, news, matches, products, manga episodes | Data-driven CRUD (list + detail) |
| **CTA page** | The 10.x forms, sponsorship deck download | Form designer + submission inbox |

### 2.2 Global navigation

- **Primary menu (desktop)**: ABOUT / CLUB / ACADEMY / PROGRAMS / WOMEN'S / SCHEDULE / NEWS / CULTURE / PARTNERS / CHARITY, with a persistent right-hand group: `JOIN` (accent button), `Sign in / Member Centre`, and the language switch `繁中 / EN`.
- **Mega menu**: each top-level category expands to show its second level plus one key visual and one primary CTA for that category. Women's Football and Charity are single pages and link directly without expanding.
- **Mobile**: full-screen hamburger drawer (including language switch and member entry), plus a sticky bottom CTA bar (`Join the Club` / `Contact Us`).
- **Footer**: four-column sitemap (including FAQ and Charity), social links, sponsor logo carousel, Shopify store link, contact details, privacy / cookie policy, language switch.

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
| G-11 | Member status bar | Header shows sign-in / register or an avatar menu; when signed in, forms pre-fill basic details; supports one-tap login with LINE and Google |
| G-12 | FAQ quick block | Attachable to the bottom of any page, automatically pulling the FAQs for the relevant topic (see 3.12) |

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
| Official store entry | Links to the Shopify store (new tab) | Settings |
| Bottom CTA strip | Join the Club / Join the Academy / Become a Partner | CTA component |

> The homepage follows a single line of argument: brand position → match activity → latest news → conversion. Seasonal content (a new manga chapter, a major charity event, an enrolment window) is surfaced through the hero carousel or the latest-news block rather than through dedicated fixed slots.

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

#### 8.2 Fan Club

- Join the Fan Club: membership form (member details, plan selection)
- Fan Benefits: tiered benefits comparison table
- Fan Events: event list + registration + event recaps

#### 8.3 Merchandise (**Shopify referral — no shop functionality on this site**)

| Item | Description |
|---|---|
| Positioning | The website acts purely as a **brand showcase**, presenting product imagery and collection stories; every purchase action (add to cart, checkout, payment, shipping, returns, inventory) is **handled entirely by Shopify** |
| Category display | Club Collection / Academy Collection / Fan Collection |
| Product showcase | Featured product cards (image, name, optional price) + collection story; "Shop now" opens that product's Shopify page in a new tab |
| Online Store | Primary CTA linking to the Shopify storefront; entry points also placed in the header, footer, and homepage |
| Data maintenance | Showcase items are created manually in the admin with a Shopify product link (see 4.6 E4). **Inventory and price are not synchronised**, to avoid inconsistency; the price field should be optional or marked "see store page for current pricing" |
| Not included | ✗ On-site cart ✗ Payment integration ✗ Order management ✗ Inventory ✗ Shipping and returns |

> If automatic product synchronisation becomes necessary later, the Shopify Storefront API could be evaluated to pull the product list — a Phase 4 option, outside the current scope.

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
| 10.2 | Academy & Children's Training | **Programme selected (Academy U12 / U14 / U15 age groups, or Children's Training mixed-age / beginner / skill-development classes)**, student details, preferred venue, parent contact, football background, health notes | Academy / Programs (routed by programme selected) |
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
| CTA | Get Involved | Three participation routes: **corporate charity partnership** (routes to 10.5) / **fan donation** (see below) / **volunteer signup** (form) |

#### Fan donation mechanism

Fans can support the club's charitable programmes directly. Because the site carries no payment gateway, donations are handled through existing channels:

| Channel | How it works | Best for |
|---|---|---|
| **Shopify donation item** (recommended primary channel) | List a "support our charity work" item on Shopify with several fixed amounts (e.g. NT$300 / 500 / 1,000 / 3,000); fans pay exactly as they would for a purchase | General fans, small donations, immediate online completion |
| **Bank transfer** | Publish the donation account details on the site with a follow-up form (name, amount, last five digits of the transfer, designated programme, named or anonymous) | Large donations, corporate giving, those who cannot pay online |
| **In-kind donation** | A form to register the items and quantities offered; the club follows up | Equipment, kit, supplies |

**Donation page functionality**:
- Explains how funds are used (donors may designate a specific charity programme or leave it undesignated)
- Amount option cards + "other amount"
- **Acknowledgement**: donors choose to be **named or anonymous**; named donors appear on a **public donor roll** (name only, no amounts)
- A thank-you message is sent after donating (email / LINE), and the use of funds is published annually
- Donations by fan members are attributed to their member account and visible in the Member Centre

> Note: given no payment gateway is being built, a Shopify item is the fastest route to accepting donations. If formal receipts or tax-deductible status are required later (which involves public fundraising eligibility), the legal and procedural implications need separate assessment — not included in this scope.

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

**Positioning**: use membership to connect four existing scenarios — fan club, program registration, event signup, and newsletter — reducing repeat form-filling and accumulating a first-party contact database.

#### Three parallel signup channels

| Channel | Use case | Description |
|---|---|---|
| **A. Email registration on the website** | Users registering or joining the fan club on their own initiative | Email + password, activated by a verification email |
| **B. LINE Official Account QR code** | On-site recruitment, matchdays, camps, school outreach, printed collateral | Scan the QR code to add the official account, then follow the prompts to complete signup and linking |
| **C. Google account** | Google-ecosystem users and international visitors | One-tap authorisation creates the account, no password required |

> All three channels create records in **the same member database**. If someone registers by email and later signs in via QR code or Google, the accounts are **merged into one** by matching on email or mobile number, preventing duplicate records.

#### LINE Official Account signup flow

```
① Scan the QR code
   (on-site signage / print collateral / website / registration confirmation page / email signature)
        ↓
② Add the official account as a friend
        ↓
③ Automatic welcome message
   "Welcome to TCRFC! Tap here to complete your registration and access your bookings and fixture alerts."
        ↓
④ Tap through to the signup form (opens inside LINE)
   Fields: name, mobile, email, role (fan / parent), teams of interest
        ↓
⑤ The system creates the member record and links the LINE account
        ↓
⑥ Done: the member can self-serve from the LINE rich menu, and the club can push notifications
```

**QR code placement**: signage at training grounds and the home stadium, camps and trial events, recruitment posters and flyers, the website footer and contact page, registration confirmation pages (encouraging signup to receive follow-ups), the fan club signup page, and matchday programmes.

**Suggested LINE rich menu items**: `My bookings`, `Upcoming fixtures`, `Program registration`, `Latest news`, `Contact us`, `Official store`.

> **Design principle**: the LINE signup form is **deliberately minimal** (name, mobile, email, role only) to reduce abandonment when scanning on site; the remaining details are collected when the person actually registers for a program.

#### Membership tiers

| Tier | How it's obtained | Benefits |
|---|---|---|
| Registered | Free registration | Pre-filled registration forms, booking history, newsletter preferences |
| Fan Club | Joining the fan club (see 8.2) | All of the above + priority booking for fan events, member benefits, exclusive content area |
| Parent | Linked after completing a program or academy registration | All Registered benefits + student attendance and course information, payment status, notifications |

#### Public-site functionality

| Feature | Description |
|---|---|
| Registration | ① Email + password; ② **LINE Official Account QR code signup** (flow above); ③ **quick registration with Google**. All three create records in the same member database |
| Email verification | Account activated by a verification email after registration (also sent for LINE signups that include an email) |
| Sign in / out | Email + password; **one-tap LINE sign-in** and **Google sign-in** (no password to remember); remember me; failed-attempt limits |
| Forgotten password | Time-limited reset link by email; LINE-linked members can verify via LINE instead |
| Linked-account management | The Member Centre shows LINE / Google link status and allows linking and unlinking; unlinking preserves the account and booking history (at least one sign-in method must remain) |
| Profile | Name, contact details, date of birth, area, avatar, language preference, notification preferences |
| My bookings | Program / camp / trial / event booking history and status (pending / confirmed / paid / completed / cancelled), with downloadable confirmations |
| My donations | Donation history and thank-you messages (see the fan donation mechanism in 3.11) |
| My students | Parents can link multiple students and pre-fill their details when registering |
| Fan club area | Membership card (member number / QR code), benefits list, exclusive event booking, exclusive content |
| My calendar | Booked programs and fixtures marked "attending" in one view, downloadable as .ics or subscribable (see 3.13) |
| Notification centre | On-site notifications: booking status changes, event reminders, club announcements |
| Subscription management | Toggles for newsletter and notification types; each notification type can be routed to **email / LINE / on-site** |
| Account management | Change password, delete account (personal-data deletion request workflow) |

#### Integration with existing modules

- **Forms (10.1–10.7)**: when signed in, name / email / phone are pre-filled, reducing the fields to complete.
- **Program registration (P3)**: bookings are automatically attributed to the member account, so the admin can view a member's full booking history.
- **Fan club (F2)**: fan club members are simply members flagged `fan_club` in the member system — no separate list is maintained.
- **Shopify**: website accounts and Shopify accounts remain **independent**; no single sign-on is implemented. **Member-exclusive offers are handled by issuing Shopify discount codes**, distributed via LINE push or email, with the admin setting the target audience (tier / list) and validity period.
- **LINE Official Account**: serves as a signup channel and notification channel, not as a separate contact database — every LINE friend who completes signup is written into the member database (see 4.11 K5).

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
│   └── E4 Product Showcase (Shopify link maintenance)
├── F. Culture
│   ├── F1 Manga project / characters / episodes
│   └── F2 Fan Club events (member lists live in module K)
├── G. Forms & Enquiries
│   ├── G1 Form Designer
│   ├── G2 Inbox (9 enquiry types + volunteer / donation)
│   └── G3 Newsletter Subscribers
├── H. SEO & Marketing
├── I. Site Settings (menus / footer / languages / contact info / venues / Shopify links)
├── J. System (accounts / roles / audit / backup)
├── K. Members
│   ├── K1 Member list and detail
│   ├── K2 Tiers & fan club
│   ├── K3 Parent–student links
│   ├── K4 Notifications & messaging (on-site / email / LINE)
│   └── K5 LINE Official Account integration
└── L. Schedule
    ├── L1 Master calendar (cross-module aggregate view)
    ├── L2 Custom events (club events)
    ├── L3 Categories & display settings
    └── L4 Subscription & export (iCal / .ics)
```

> **Numbering note**: the programs module was originally numbered `D1–D4`, which clashed confusingly with the **team code `D1`** (First Team). It has been renumbered **`P1–P4` (Programs)**, and all references throughout this document have been updated.

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
- Hero carousel management (order, image/video, headline, CTA, display period); **seasonal content (new manga chapters, charity campaigns, enrolment windows) is surfaced here** instead of via dedicated fixed slots
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
- **Donation settings**: Shopify donation item link, amount options, bank account details, in-kind donation form, donor roll management (named / anonymous, display toggle)
- **Get Involved settings**: copy and destinations for the three CTAs (corporate partnership / fan donation / volunteering)
- Display control: ordering and pinning within the charity section (**no fixed charity slot on the homepage**; for temporary exposure, use the hero carousel or publish a news article)

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
- Registration list: filters (program, session, status, date), keyword search
- Registration detail: student details, parent contact, health declaration, notes
- Status workflow: `Pending → Confirmed → Paid → Completed / Cancelled / Waitlisted`
  - Payment is handled **offline** (imported transfer records or on-site collection, then marked by staff); the site accepts no payments
- **Member attribution**: registrations submitted while signed in are linked to that member account (K1 can look up all of a member's registrations); guest registrations can be merged later by email matching
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

#### E4 Product Showcase (Shopify referral)
- Showcase item: name, category (club / academy / fan), gallery, description, price (**optional**, or marked "see store page"), **Shopify product link**, order, published status
- Collection copy: brand narrative block for each collection
- Site-wide Shopify store URL (shared by header, footer, and homepage)
- Link health checks: periodic detection of broken links with alerts
- **Not included**: inventory, orders, payments, shipping, returns, coupons (all handled in Shopify's own admin)

---

### 4.6 F. Culture

#### F1 Manga Management
- Project settings (8.1 About): world-building page
- Characters: name, profile, artwork, linked real player (optional), order
- Episodes: episode number, title, cover, **bulk upload and ordering of interior pages**, publish date, status, latest-episode flag (determined automatically)
- **All episodes are free to read**, with no paywall and no sign-in requirement
- Readership statistics

#### F2 Fan Club (events and benefits)
- **Member lists are maintained centrally in module K** (fan club members = members with the `fan_club` tier); no separate list is kept here
- Benefits configuration: tiered benefits table (used by 8.2 Fan Benefits on the public site)
- Fan events: event CRUD, registrant list (can be restricted to fan club members), event recaps (linked galleries and articles)
- Membership plan settings: plan name, period, fee description (payment handled offline or via a Shopify membership product)

---

### 4.7 G. Forms & Enquiries

#### G1 Form Designer
- Create and edit form fields (text, dropdown, multi-select, date, file upload, consent checkbox)
- Per form: notification recipients (multiple allowed), auto-reply template, CAPTCHA toggle, post-submission redirect

#### G2 Enquiry Inbox
- A unified inbox with tabs by form type (10.1–10.7 + deck downloads + **volunteer signups / donation enquiries**)
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
- **External service links**:
  - Shopify store URL
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
- List columns: member number, name, email, tier, **registration source (website / LINE / Google / fan club / program registration)**, **LINE / Google link status**, registration date, last sign-in, status (active / disabled / unverified)
- Filters: tier, status, registration source, **LINE linked or not**, registration period, language preference, newsletter subscription
- **Duplicate detection**: identifies likely duplicate accounts by email or mobile number and offers merging (bookings and links transfer with the merge)
- **Member detail**: profile, linked students, booking history, fan club information, notification history, sign-in history
- Actions: disable / enable, resend verification email, send a password reset on their behalf, add internal notes
- CSV export (permission-gated and written to the audit log)

#### K2 Tiers & fan club
- Tier definitions and benefit configuration (Registered / Fan Club / Parent)
- Fan club members: join date, plan, expiry, **expiry reminders and renewal notices**
- Member number and QR code generation rules
- Manual tier adjustment (with a reason recorded)

#### K3 Parent–student links
- Manages parent ↔ student relationships; one parent may link several students, and one student may have several parents
- Link approval (preventing arbitrary linking to someone else's child): matched against registration records or approved manually
- Provides a view of each student's attendance and registration history

#### K4 Notifications & messaging
- **Three channels**: on-site notification, email, and **LINE push**; each message can specify a channel (or follow the member's preference automatically)
- Audiences: everyone / a tier / parents of a given squad / a given program session / a custom list
- Template management (Chinese / English): registration verification, password reset, booking status change, event reminder, cancelled session, fan club expiry
- **LINE push considerations**: an official account's monthly message allowance depends on its plan, with charges above the quota. Recommended approach:
  - **Routine, personalised** notifications (booking confirmation, cancellations, fixture reminders) go via LINE
  - **Mass broadcasts** (newsletters, campaign promotion) go via email first, to avoid burning through the quota
  - The admin shows **messages used this month and quota remaining**, and estimates consumption before a broadcast
- Delivery records and statistics: delivered count, open rate (email), read and click counts (LINE)

#### K5 LINE Official Account integration
- **QR code management**: generate signup QR codes, with **multiple parameterised links** per use case (e.g. `summer camp on site`, `school recruitment`, `home matchday`, `website footer`) to track channel performance
- **Channel performance report**: scans, completed signups, and conversion rate per QR code
- **Welcome message settings**: the auto-reply and signup link shown after a friend is added (Chinese / English)
- **Rich menu settings**: menu items and destinations, with different menus by member role (general / parent)
- **Keyword auto-replies**: e.g. "fixtures" returns upcoming matches, "register" returns the program list
- **Link management**: view members' LINE link status, unlink manually, resolve linking anomalies
- **Friends who never completed signup**: people who added the account but never submitted the form are tracked separately with optional reminder messages (within quota); this list **does not count towards the member total**

#### Member data security requirements
- Passwords stored hashed (irreversible), strength rules, lockout after failed attempts
- Viewing member personal data in the admin requires the corresponding permission, and every view and export is written to the audit log
- A defined process and timeframe for member-initiated account deletion (in line with personal-data legislation)
- Data for minors must be held under a parent account, with guardian consent obtained
- The **LINE link identifier** (a user's account-specific ID) is treated as personal data: never displayed publicly and never included in general exports
- The signup form must state the purpose of data collection and consent terms, to the same standard as website registration

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

## 5. Data Model & Content Types

| Type | Description | Key relationships |
|---|---|---|
| `Page` | Static page (with blocks); **the women's football page is also this type** | SEO, languages |
| `Article` | News and stories | Category, Tag, Player, Team, Match, Program |
| `Team` | Team, carrying the **team code**: `D1` (= First Team) / `U15` / `U14` / `U12`; unused by the women's team for now | Player, Coach, Match, Season, CalendarEvent |
| `Player` | Player | Team, Article, Stats, Pathway |
| `Staff` | Coaches and staff | Team, Program |
| `Match` | Match | Team, Season, Article (match report) |
| `Standing` | League table | Season, Team |
| `Achievement` | Honour | Team, Season |
| `Milestone` | Milestone | — |
| `Program` | Course / camp / specialist training | Session, Staff, Venue, Partner |
| `Session` | Session / intake | Program, Venue, Registration |
| `Registration` | Registration | Session, Contact |
| `Trial` | Trial session | Team, Venue, Registration |
| `Partner` | Partner | Article, Program |
| `Sponsor` | Sponsor | SponsorPackage, Article |
| `SponsorPackage` | Sponsorship package (9 types) | Enquiry |
| `ProductShowcase` | Showcase item (display + Shopify link only, **not an e-commerce entity**) | Collection |
| `ComicEpisode` / `ComicCharacter` | Manga episode / character | Player (inspiration) |
| `FanEvent` | Fan event | Member |
| `Enquiry` | Form submission (10 types + volunteer / donation) | Form, Assignee |
| `Venue` | Venue | Program, Match, Trial |
| `MediaAsset` | Media asset | Global |
| `Faq` / `FaqCategory` | FAQ / topic category | Page (embed location) |
| `CharityProgram` | Charity programme | Charity, Partner, Article, ImpactRecord |
| `ImpactRecord` | Impact record (organisation, donation, imagery) | Charity, CharityProgram |
| `Charity` | Recipient organisation (name, description, logo, website) | CharityProgram, ImpactRecord |
| `Donation` | Donation record (source, amount, designated programme, named/anonymous, status) | Member, CharityProgram |
| `ImpactMetric` | Impact statistic | CharityProgram |
| `Member` | Member account, incl. registration source, **LINE link identifier**, notification preferences | Registration, FanEvent, StudentLink, LineEntryCode |
| `StudentLink` | Parent ↔ student relationship | Member, Registration |
| `Notification` | Notification / push record (on-site / email / LINE) | Member |
| `LineEntryCode` | LINE signup QR code (parameterised, for source tracking and reporting) | Member |
| `CalendarEvent` | **Calendar event (aggregate view)**: points at a Match via `source_type` + `source_id`, or is a `custom` club event; carries **`team_codes[]` (D1 / U15 / U14 / U12)** as its first-level category | Match, Team, Venue |
| `EventType` | Match / event type (icon, colour, display rules) | CalendarEvent |
| `EventInterest` | Member "I'm attending" marker and reminder settings | Member, CalendarEvent |

> Every type with a public-facing presentation must support **zh / en bilingual fields**, with room to add a third language.
> `CalendarEvent` should be implemented as a **view or index table** rather than duplicated data, keeping it in sync with its source module and avoiding two sources of truth.

---

## 6. Roles & Permissions Matrix

| Role | Content | FAQ | Charity | Teams / Matches | Programs / Registrations | Schedule | Members | Business / Sponsors | Enquiries | SEO / Settings | System |
|---|---|---|---|---|---|---|---|---|---|---|---|
| System administrator | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full | ✔ Full |
| Content editor | ✔ Edit / publish | ✔ Full | ✔ Edit | Read-only | Read-only | Custom events | — | Read-only | — | Per-page SEO | — |
| Football / team manager | Draft | Relevant topics | — | ✔ Full | Read-only | Match events | — | — | — | — | — |
| Academy / programs manager | Draft | Relevant topics | — | Academy squads | ✔ Full | Squad matches | Parent view | — | Program enquiries | — | — |
| Commercial / sponsorship | Draft | Relevant topics | Read-only | Read-only | Read-only | Read-only | — | ✔ Full | Partnership / sponsorship enquiries | — | — |
| PR / media | ✔ Edit | Read-only | ✔ Edit | Read-only | — | Custom events | — | Read-only | Media enquiries | — | — |
| Support / administration | — | ✔ Edit | — | — | Registration handling | Read-only | ✔ View / handle | — | ✔ Full | — | — |
| Translator ※ | Translation fields only | Translation fields only | Translation fields only | Translation fields only | Translation fields only | Translation fields only | — | Translation fields only | — | UI string table | — |
| Viewer | Read-only | Read-only | Read-only | Read-only | Read-only | Read-only | — | Read-only | Read-only | — | — |

**Additional rules**:
- Content editing follows a **submit-for-review → publish** workflow; only designated roles may publish.
- **Member personal data (email / phone / date of birth / minors' details) is restricted**: only system administrators and support/administration staff see it in full; other roles see masked values (e.g. `a***@gmail.com`).
- **Exporting** member lists requires separate authorisation, and every export is written to the audit log (who, when, how many records, stated purpose).
- ※ The translator role may edit `en` fields only, and may not modify the Chinese source or publication status.
- **Calendar permissions follow the source module**: which events a user can edit on the calendar depends on their permissions over the underlying match data (e.g. an academy manager may reschedule their own squad's fixtures but not the first team's).

---

## 7. SEO / GEO & Multilingual Plan

Implementing each of the nine "GEO & SEO FOUNDATION" fundamentals:

| Fundamental | Implementation |
|---|---|
| Clear site architecture and hierarchy | URLs mirror the site hierarchy, e.g. `/en/academy/join/` |
| Keyword-led content strategy | Each page can be assigned a primary keyword in the admin, with usage checks (title / H1 / opening paragraph) |
| Structured data / schema markup | Automatic output of Organization, SportsTeam, SportsEvent, Event, Person, Article, Course, FAQPage, BreadcrumbList (Product schema is Shopify's responsibility and is not emitted here) |
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
| Security | Forced HTTPS, 2FA on the admin, CSRF / XSS / SQL injection protection, upload type and size limits, optional admin IP allowlist; **member system**: password hashing, session timeout, lockout after failed sign-ins, brute-force protection |
| Personal data | Registrations, enquiries, and **member data** (including LINE link identifiers) stored encrypted, with a retention policy and a data-subject deletion process; **minors' data requires guardian consent** |
| Availability | 99.5% uptime target; daily backups retained off-site for 30 days |
| Extensibility | Content types must be extensible (new age groups / seasons / program types / languages without code changes); if the women's team is later upgraded to a full team area, the existing team module can be reused directly |
| Monitoring | Error tracking (Sentry-class), uptime monitoring, alerts on form submission failures |

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

### Phase 2 — Commercial, membership, and deeper content (approx. 8–10 weeks)
- The full 09 Partners & Sponsors area (including deck downloads and lead tracking)
- 03.2–03.5 Player Development, International Pathways, Player Stories
- **The full 11 Charity & Impact area**
- 05.3–05.5 Winter Camp, Specialist Training, School & Community
- **Member system (module K)**: email registration and sign-in, **LINE Official Account QR signup and linking**, one-tap LINE sign-in, Member Centre, booking attribution, student linking
- **Advanced schedule**: per-team subscription URLs (webcal), member "I'm attending" and reminders, admin swimlane view / clash detection / drag-to-reschedule
- Admin: business modules, charity module, trial management, advanced registration (waitlists / exports / attendance sheets), member management, **LINE integration (QR management / rich menu / push)**, advanced calendar
- **English content goes live**

### Phase 3 — Culture and community (approx. 6 weeks)
- 08 TCRFC Culture: manga reader, fan club (integrated with the member system), **product showcase + Shopify referral**
- League tables, automatic aggregation of player statistics
- Notification centre, newsletter integration

### Phase 4 — Optimisation and expansion (ongoing)
- FAQ performance optimisation, zero-result search feedback loop
- Deeper analytics dashboards, A/B testing, personalised recommendations
- Optional evaluations: Shopify Storefront API product sync, ticketing, member points, expanding women's football into a full team area

> Note: the phase plan excludes e-commerce and payment development (all shopping is handled by Shopify); that effort has been reallocated to the member system and the charity section.

---

## 10. Open Items

### Decisions already made

| Item | Decision |
|---|---|
| Technology selection | Deferred; this document defines functional requirements only |
| Payments and shopping | No on-site e-commerce; all shopping is routed to **Shopify** |
| Languages | **Traditional Chinese (default) / English**; Japanese is out of scope, with the architecture left extensible |
| Member system | **Required**, delivered in Phase 2; signup channels are **email registration + LINE Official Account QR code + Google sign-in** |
| LINE Official Account | **Already exists** — integrate with the current account; no new application needed |
| Member offers | Handled exclusively by **issuing Shopify discount codes**; no account integration |
| Fan donations | **Accepted**, via a Shopify donation item and bank transfer (see 3.11) |
| FAQ | Standalone section 12, centrally managed and embedded across pages |
| Charity records | Section 11; every record carries three core data points — **charity organisation name, what was donated, event photography** |
| Women's football | A single introductory page **routing to the women's team website**; this site does not maintain their roster or fixtures |
| Match data | **Entirely manual**, no external API integration; CSV bulk import provided |
| Schedule | **Match-centric**; academy courses, camps, and specialist training are excluded. Calendar content must be **bilingual** |
| TCRFC manga | **Entirely free and public**, with no paywall and no sign-in |

### Still to confirm

1. **Shopify store URL**: the target for the 08.3 showcase and the donation item; if the store is not yet open, confirm the expected launch date.
2. **LINE Official Account plan and message quota**: the monthly push allowance determines which notifications go via LINE and which via email (see 4.11 K4).
3. **Whether donation receipts are required**: issuing formal receipts or supporting tax deductions involves public-fundraising eligibility and regulatory process, and needs separate assessment; this plan covers thank-you messages and annual reporting of fund usage only.
4. **Whether fan club membership is paid**: if so, is payment handled as a Shopify membership product or by offline transfer?
5. **Migration scope for the existing site**: which content on [www.tcrfc.tw](https://www.tcrfc.tw/) should be kept, rewritten, or discarded? For news, keeping the last 1–2 years is recommended.
6. **How English content will be produced**: supplied by the club, outsourced for translation, or launched for priority pages first? This affects the Phase 2 timeline.
7. **Whether charity amounts are published**: organisation name, donation content, and imagery are confirmed; monetary amounts are private by default — are there specific cases that should be public?
8. **Whether trials appear in the calendar**: off by default (kept on the recruitment pages); can be enabled in the admin if desired.
9. **Ticketing and broadcast information**: are there ticketing channels or broadcast platforms to display on fixture cards?
10. **Personal data retention period**: §3.10 and §9 both require forms to display a retention notice and for a retention policy to be defined, but **the actual duration is unspecified**. All seven forms need it; it is a personal-data compliance item and must be confirmed by the club and its legal advisers.
11. **Manga release cadence**: the publishing rhythm of episodes, which shapes the "latest episode" block and homepage exposure.

---

*The functionality in this specification can be adjusted and extended to suit actual needs, ensuring the best possible user experience and search visibility.*
