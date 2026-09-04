# TCRFC Taichung Rock FC — Mobile App Functional Specification

> **Document version**: v1.0
> **Date**: 2026-09-04
> **Brand promise**: LOCAL ROOTS. GLOBAL PATHWAYS.

> **Purpose of this document**
> 1. This document specifies the **Taichung Rock FC mobile app** (iOS / Android): the official application built around membership, fixtures, partner stores, and owned advertising slots, **sharing the official website's admin and database**.
> 2. Together with [`TCRFC_Website_Functional_Specification_EN.md`](TCRFC_Website_Functional_Specification_EN.md) (from v2.5) and [`TCRFC_Charity_Donation_Platform_Specification_EN.md`](TCRFC_Charity_Donation_Platform_Specification_EN.md), these are **three complementary specifications, not overlapping ones**: the website is the content and SEO body, the Charity Donation Platform is the Association's separate collection site, and this app is the pocket interface for membership and fixture information.
> 3. The website specification v2.5 has, on the basis of this document, amended **four assumptions**: **"no separate fixed slots" scoped to the website**, **"no payment gateway" scoped a second time** (in-app membership payment excepted), **"no push or notification centre" scoped to LINE push and on-site web messaging**, and **"no booking attribution or form pre-filling" partially lifted for the app** (parent–student linking remains excluded).
> 4. This document defines functional requirements only; it **does not decide framework, language, or hosting** (continuing the premise set in website specification 1.3). Section 1.5 replaces technology selection with a statement of required platform capabilities.

---

## Table of contents

1. [Objectives and scope](#1-objectives-and-scope)
2. [App architecture and navigation](#2-app-architecture-and-navigation)
3. [App front-end functionality](#3-app-front-end-functionality)
4. [Membership and authentication](#4-membership-and-authentication)
5. [In-app payment (LINE Pay)](#5-in-app-payment-line-pay)
6. [Push notifications](#6-push-notifications)
7. [Owned advertising slots and performance measurement](#7-owned-advertising-slots-and-performance-measurement)
8. [Admin functionality](#8-admin-functionality)
9. [API and integration requirements](#9-api-and-integration-requirements)
10. [Data model and content types](#10-data-model-and-content-types)
11. [Roles and permissions](#11-roles-and-permissions)
12. [Personal data, security and regulation](#12-personal-data-security-and-regulation)
13. [Non-functional requirements](#13-non-functional-requirements)
14. [Release and distribution](#14-release-and-distribution)
15. [Delivery phases](#15-delivery-phases)
16. [Open items](#16-open-items)

---

## 1. Objectives and scope

### 1.1 Positioning

This app is not a shrunken website, nor a website wrapped in a shell. It exists to **do what a website cannot**.

Whether a feature belongs in the app is decided on two criteria; meeting either one is sufficient:

| Criterion | Description |
|---|---|
| **Only a phone can do it** | Requires location, offline storage, push, a mobile wallet, the lock screen, the native calendar, or the camera |
| **The data already exists in the specification** | Reuses an existing content type from the website admin, so nothing new has to be produced by the client before launch |

Anything meeting neither is excluded — building it would only produce something slower than the website and harder to maintain.

Three design premises shape the whole app:

| Premise | Consequence for the design |
|---|---|
| **The app shares one dataset with the website** | The app creates no parallel content-editing interface. Everything is still maintained in the website admin; the app is a second presentation surface, not a second source of truth |
| **Members use it at the ground and at store counters** | Signal is often absent. The digital membership card, cached fixtures, and already-read news **must work offline** |
| **Advertising is sold directly, not through a network** | Slots are sold to local businesses and measured by our own system. No ad SDK, no tracking permission prompt, no behavioural targeting |

### 1.2 Relationship to the website and the Charity Donation Platform

| Item | Approach |
|---|---|
| **Admin** | **Shares the website admin**, adding an `M. Mobile App` module and extending the existing `E` commercial module (E5–E7 advertising) |
| **Database** | **Shares one database**; the app reads the same `Match`, `Article`, `Member`, and `PartnerStore` records maintained in the website admin |
| **Entity** | The operating entity is **Taichung Rock FC**. The collecting entity is likewise the club, **entirely separate from the Association that runs the Charity Donation Platform** |
| **Role of the website** | The website remains the body for SEO and outward content. The app carries no SEO responsibility; the website must additionally host an app download page and the Universal Link verification files (see 2.3) |
| **Role of the Charity Donation Platform** | **The app carries no donation payments whatsoever.** At most it links out, and must state plainly that the recipient is the Taiwan Football Strategic Development Association, never implying a donation to the club |

How the three specifications divide:

| Document | What it carries | Entity |
|---|---|---|
| `TCRFC_Website_Functional_Specification_EN.md` | Website public site (13 sections), admin modules A–L, definitions of every content type | Club |
| `TCRFC_Charity_Donation_Platform_Specification_EN.md` | Scan-to-donate site, LINE Pay, e-invoices, settlement, admin module N | **Association** |
| `TCRFC_Mobile_App_Specification_EN.md` (this document) | App front end, admin module M and E5–E7, app-specific types, API conventions | Club |

**Conflict rule**: definitions of content types and existing admin modules always defer to the website specification; this document only defines app-specific additions and extensions.

### 1.3 System scope

**In scope**

| # | Feature | Description |
|---|---|---|
| 1 | Membership | Registration and sign-in, member profile, digital card and **mobile wallet**, jersey registration, renewal, benefits comparison table |
| 2 | In-app payment | Paid membership completed via LINE Pay with automatic activation (**membership fees only**) |
| 3 | Fixtures and results | Squad tabs, filters, match detail, add to calendar, kick-off reminders, venue navigation |
| 4 | Partner stores | Nearby stores by distance, map, one-tap navigation and calling, category and area filters |
| 5 | Programs and camps | Reuses the website's existing booking mechanism, adding form pre-filling and "my bookings" |
| 6 | News and stories | List, article, offline reading, category push subscriptions, sharing |
| 7 | Squads and players | First team and academy squads, player detail |
| 8 | Manga reader | Vertical scrolling, per-episode offline download, reading position |
| 9 | Push notifications | Fixture reminders, results, news, membership expiry, jersey status |
| 10 | **Owned advertising slots** | Slot management, flights, rotation weights, impression and click measurement, advertiser reporting |
| 11 | Partners and sponsors | Logo wall and detail pages, outbound links |
| 12 | Prize draw information | Read-only display of personal eligibility and the rules (see the strict boundary in 3.11) |

**Out of scope**

| Item | Reason |
|---|---|
| **Store scan-to-redeem, loyalty stamps, redemption reports** | Already excluded in website 8.4. Building it would require store accounts and redemption reporting — a second system |
| **Loyalty points, stored-value wallet, ticketing and match packages** | Already excluded on the website; unchanged by the app |
| **Fan photo walls, user-generated uploads, comment boards** | Assets include minors; opening uploads would place the moderation burden on the club |
| **In-app donations** | The collecting entity is the Association, not the club. A club app collecting charitable funds runs straight into the public-fundraising eligibility question. Outbound links only |
| **Course fees paid online** | Website P3 states course payment is offline. Refund rules for courses are far more complex than for membership; including them would turn section 5 into two problems |
| **Parent–student linking** | Creates a durable adult-to-minor association, a step up in sensitivity. Family memberships deliberately avoided this design |
| **Splash advertising** | The placement most likely to generate negative reviews, and it harms both launch experience and review impressions |
| **Live score updates** | Fixture data is maintained entirely by hand; "live" is not achievable and claiming it would only break trust |
| **Third-party ad networks** | See 7.8. Selling directly is a deliberate choice, not a limitation |
| **On-site commerce, cart, orders, inventory** | Merchandise is routed to Shopify, as on the website |
| **Women's football squads and fixtures** | Routed to the Taichung Blue Whale website, as on the website |
| **SEO / GEO** | App content is not indexed. The website's app download page still follows the website SEO rules |
| **Technology selection** | This document defines functional requirements only |

### 1.4 Terminology

| Term | Definition |
|---|---|
| **Slot** | A fixed advertising position on an app screen. Long-lived, identified by `slot_code` |
| **Flight (campaign)** | One advertiser's booking on one slot over a period. Several flights may rotate on the same slot at the same time |
| **Creative** | The actual image or video under a flight, with headline, CTA, and click destination. One flight may hold several creatives for A/B testing and both languages |
| **Impression** | The advertising area **at least 50% visible for at least one continuous second**. Each item in a rotation counts separately |
| **Device** | One app installation, identified by `device_install_id`. Invalidated on uninstall; never shared across apps or devices |
| **Deep link** | A link that opens a specific screen inside the app, either via the custom scheme or a Universal Link |
| **Forced update** | An older app version is taken out of service for compatibility or security reasons and prompts the user to update on launch |
| **Mobile wallet** | Apple Wallet and Google Wallet. The digital membership card can be written there and shown from the lock screen |
| **Eligibility cut-off** | The moment a prize-draw roster is frozen; reuses the website's `MemberDraw.snapshot_at` |

### 1.5 Platform capability requirements (in place of technology selection)

Technology selection is out of scope, but functional feasibility has a floor. The following are **required platform capabilities**; any technical approach that satisfies them is acceptable:

| # | Capability | Why it is required |
|---|---|---|
| 1 | **Native push (APNs / FCM)** | The premise of everything in section 6 |
| 2 | **Reading from a local offline cache** | The membership card must be presentable at the ground with no signal; cached fixtures and read articles must be readable offline |
| 3 | **Deep links and Universal Links** | Push landing, sharing back from the web, advertising creatives pointing to in-app screens |
| 4 | **Foreground visibility measurement** | The premise of the impression definition in 7.5. Without accurate visible-area and duration measurement, advertising performance cannot be reconciled |
| 5 | **Device location permission (foreground only)** | Distance sorting for nearby partner stores (3.8) |
| 6 | **Wallet pass writing and remote update** | The digital membership card (3.6) |
| 7 | **Writing to the system calendar** | Adding fixtures to the calendar (3.2) |
| 8 | **A compliant path for in-app purchase or an external payment link** | Section 5.5 |
| 9 | **Biometric unlock** (optional) | Fast access to the membership card |

> This list effectively rules out a pure WebView shell (items 4 and 6 are hard to achieve accurately), but it does so **as a consequence of functional needs**, not as a technical preference.

---

## 2. App architecture and navigation

### 2.1 Tab structure

Five fixed bottom tabs, never more than three levels deep:

| Tab | Contents | Sign-in required |
|---|---|---|
| **Home** | Next fixture, latest news, advertising slots, shortcuts, sponsor logo wall | No |
| **Fixtures** | Squad tabs, fixtures/results toggle, match detail | No |
| **Member** | Signed out: how to join. Signed in: card, membership, my bookings, jersey, prize-draw information | Partly |
| **News** | Category list, article, offline reading | No |
| **More** | Squads, partner stores, program booking, manga, partners and sponsors, charity (outbound), store (outbound), FAQ, settings | No |

**Design principle**: signed-out users must be able to browse fixtures, news, squads, partner stores, and program information in full. **The sign-in wall stands in exactly four places**: the membership card, my bookings, renewal, and prize-draw eligibility. The partner-store directory itself stays public — it is the main driver of paid conversion — with the applicable-tier label telling users which offers require a paid membership.

### 2.2 Screen inventory

| Code | Screen | Data source | Sign-in | Offline |
|---|---|---|---|---|
| S01 | Home | `Match` / `Article` / `AdCampaign` / `Sponsor` | No | Partial |
| S02 | Fixture list | `Match` / `Team` | No | Yes |
| S03 | Match detail | `Match` / `Venue` | No | Yes |
| S04 | Squad | `Team` / `Player` / `Staff` | No | Partial |
| S05 | Player detail | `Player` | No | Partial |
| S06 | News list | `Article` | No | Yes |
| S07 | Article | `Article` | No | Yes (read) |
| S08 | Member centre | `Member` | Yes | Partial |
| S09 | Digital membership card | `Member` | Yes | Yes |
| S10 | Plans and upgrade | `MembershipPlan` / `MembershipBenefit` | No | No |
| S11 | Payment flow | LINE Pay | Yes | No |
| S12 | Jersey registration | `Member` | Yes | No |
| S13 | Partner store list / map | `PartnerStore` | No | Partial |
| S14 | Partner store detail | `PartnerStore` | No | Partial |
| S15 | Program list | `Program` / `Session` | No | No |
| S16 | Booking form | `Registration` | No | No |
| S17 | My bookings | `Registration` | Yes | No |
| S18 | Manga shelf / reader | `ComicEpisode` | No | Yes (downloaded) |
| S19 | Prize-draw information | `MemberDraw` | Yes | No |
| S20 | Partners and sponsors | `Partner` / `Sponsor` | No | Partial |
| S21 | Notification centre | `PushMessage` | No | Yes |
| S22 | Settings | Local | No | Yes |
| S23 | FAQ | `Faq` | No | Partial |

### 2.3 Deep links and Universal Links

**Two mechanisms in parallel**: the custom scheme `tcrfc://` for internal and push use, and Universal Links / App Links so that website URLs open the app directly on devices where it is installed.

**One-to-one mapping between screens and URLs** (the same URL must render the equivalent content on the website when the app is not installed):

| App screen | Deep link | Website URL |
|---|---|---|
| Fixtures (a squad) | `tcrfc://schedule/d1` | `/zh/schedule/d1/` |
| Match detail | `tcrfc://match/{id}` | `/zh/schedule/{slug}` |
| Article | `tcrfc://news/{slug}` | `/zh/news/{slug}` |
| Player detail | `tcrfc://player/{slug}` | `/zh/club/first-team/player/{slug}` |
| Partner store detail | `tcrfc://store/{id}` | `/zh/perks/{slug}` |
| Program detail | `tcrfc://program/{slug}` | `/zh/programs/{slug}` |
| Membership card | `tcrfc://membercard` | (no public equivalent) |
| Membership upgrade | `tcrfc://upgrade` | `/zh/member/upgrade/` |

**Dependencies on the website (to be delivered by the website project)**:

1. `.well-known/apple-app-site-association` and `.well-known/assetlinks.json` **must be served from the website domain**; their contents require the Apple Team ID and the Android package name, which must be obtained first.
2. The website must add app download pages (`/zh/app/`, `/en/app/`) and a footer link.
3. On devices without the app, every deep link must fall back to its website URL and **must never show an error page**.

### 2.4 Offline and caching strategy

| Content | Strategy | Lifetime | Offline |
|---|---|---|---|
| **Digital membership card** | Cached on sign-in, refreshed on every launch | Retained indefinitely, with a sync timestamp shown | Yes — **mandatory** |
| Fixtures and results | Full season refreshed on launch | 6 hours | Yes |
| News list | Pull to refresh | 30 minutes | Yes |
| Article | Cached once opened | 30 days | Yes |
| Partner stores | Refreshed on launch | 24 hours | Yes (without live distance) |
| Squads and players | Refreshed on launch | 24 hours | Yes |
| Manga episodes | Downloaded on request | Until the user deletes them | Yes |
| Membership status and draw eligibility | Refreshed on entering the screen | Never presented from cache as "valid" | No |
| Advertising creatives | Pre-fetched for the day's flights | Cleared when the flight ends | No (no impressions offline) |
| Program sessions and places | Not cached | — | No |

**Two hard rules**:

1. **Impressions must never be counted offline.** Impression events may be queued offline and sent on reconnection, but **must carry the original occurrence time**, and the server must reject events older than 24 hours — otherwise advertising performance can be inflated by hand.
2. **Membership status must never be declared valid from a cached value.** The card is presentable offline, but must show "Last synced: YYYY-MM-DD HH:mm", with a warning after seven days without a sync. Otherwise an expired member could present a stale card in airplane mode.

### 2.5 Language handling

The website's bilingual principles carry over unchanged (Traditional Chinese default, English as translation, fall back to Chinese with a marker where untranslated, architecture ready for a third language). Three rules are specific to the app:

1. **On first launch, follow the device language.** Where the device language is neither Traditional Chinese nor English, default to Traditional Chinese.
2. **Once signed in, follow the account's language preference** (an existing `Member` field). Changing the language in the app writes back to the account.
3. **Push language follows the language recorded when the device registered** (`AppDevice.lang`); where this differs from the account preference, the account wins.

**Every content type visible in the app must carry both `zh` and `en` fields**; English may be empty but the field must exist — identical to the website rule.

---

## 3. App front-end functionality

### 3.0 Site-wide features

Each of the website's G-01 to G-12 features, and how the app handles it:

| Code | Website feature | App handling |
|---|---|---|
| G-01 | Language switching | **App version**: see 2.5; switching stays on the current screen |
| G-02 | Site search | **App version**: limited to news, players, partner stores, and FAQ; static pages excluded |
| G-03 | Breadcrumbs | **Not built**: replaced by the navigation back stack |
| G-04 | Footer | **Not built**: functions move into the More tab |
| G-05 | Site-wide CTAs | **App version**: join, upgrade, book a program |
| G-06 | Social links | Carried over (IG / FB / YouTube) |
| G-07 | Cookie consent | **Not applicable**: the app uses no cookies; handled instead by the disclosure in 12.1 |
| G-08 | Accessibility | Carried over and strengthened: see section 13 |
| G-09 | Sharing | **App version**: uses the system share sheet, sharing the **website URL** rather than a deep link |
| G-10 | Breadcrumb structured data | **Not applicable**: the app does no SEO |
| G-11 | Member status bar | **App version**: the Member tab is itself the status. **Form pre-filling is provided in the app** (see 3.9) |
| G-12 | FAQ shortcut blocks | Carried over: program and academy screens embed the matching FAQ topic |

### 3.1 Home

Block order from top to bottom (order and visibility controlled by admin M2):

| # | Block | Data source | Notes |
|---|---|---|---|
| 1 | Next fixture | `Match` | Countdown, opponent, time, venue; one tap to add to calendar or navigate |
| 2 | Advertising slot `home_top` | `AdCampaign` | See 3.13 |
| 3 | Latest news (3) | `Article` | Horizontally scrolling cards |
| 4 | Membership card shortcut | `Member` | Shown when signed in with a valid membership; otherwise "Join" |
| 5 | Upcoming fixtures (3) | `Match` | Filtered by subscribed squads |
| 6 | Advertising slot `home_mid` | `AdCampaign` | |
| 7 | Partner stores (3 nearby) | `PartnerStore` | By distance where location is granted; otherwise by admin order |
| 8 | Shortcuts | — | Program booking, manga, store (outbound to Shopify) |
| 9 | Sponsor logo wall | `Sponsor` / `Partner` | **No impressions counted, never in advertising reports** (see 3.12) |

**No fixed charity slot on the home screen** — consistent with the website rule. The charity entry point sits in the More tab and must state that the recipient is the Association.

### 3.2 Fixtures and results

**Squad tabs** are generated at run time from `Team.code` and **must not be hard-coded**. Today these are `D1` / `U15` / `U14` / `U12`; adding U18 or U10 later requires only a new record in admin C1, with no app release.

**The public label is always `First Team`**; `D1` serves only as an internal code and deep-link path segment, and never appears in user-visible text.

**Filter hierarchy (deliberately leaner than the website)**:

| Level | Content | Always visible |
|---|---|---|
| First | Squad tabs | Yes |
| Second | Fixtures / results toggle | Yes |
| Secondary | Competition (league / cup / friendly / other), home or away | Inside a filter sheet |
| Calendar view | **Not built**; replaced by export to the system calendar | — |

> The website fixture page carries five simultaneously visible filter states, which a phone width cannot hold. This reduction is deliberate and **the website layout must not simply be copied across** during implementation.

**Fixture card fields**: date, weekday, kick-off time, home or away, opponent, venue, round, status. Where the venue is `TBC`, show "Venue to be confirmed" in grey and fire a push once it is set (see 6.2).

**Match detail**: the above plus results (score, scorers and minutes, cards, line-up) and a link to the match report.

**Match-day bundle** (the three things only a phone can do):

| Feature | Description |
|---|---|
| **Add to system calendar** | A single fixture, or the whole season at once. Written to the device's native calendar, not an in-app calendar |
| **Venue navigation** | Opens system maps using the `Venue` coordinates. Falls back to an address search where coordinates are absent |
| **Kick-off reminder** | Push two hours before kick-off, segmented by subscribed squad. Can be turned off in settings |

**Time handling**: stored as UTC, displayed in the device time zone, with "fixture times may change; official announcements prevail" shown on the detail screen.

**Not built**: live score updates, external league API integration — fixture data is maintained entirely by hand, consistent with the website rule.

### 3.3 Squads and players

| Item | Content |
|---|---|
| Squad selection | The same dynamic squad list as 3.2 |
| Player list | Grouped by position (GK / DF / MF / FW), ordered by shirt number within each group |
| Player card | Number, name (zh/en), position, photo |
| Player detail | Date of birth, height and weight, nationality, preferred foot, joining date, previous clubs, career, season statistics (appearances, goals, assists, cards), related news |
| Coaches and staff | Name, role, licences, specialisms, squads covered |

**Launch prerequisite**: player photographs and biographies have not yet been supplied (`photo` and `bio` fields are empty). Until they arrive this section sits in Phase D, and the list must not be padded with placeholder images — **a text-only card showing number and name is preferable to a fake photo**.

Player likeness consent must be confirmed first (see 16.2).

### 3.4 News and stories

| Item | Content |
|---|---|
| Categories | The website's existing eight categories, **no new ones** |
| List | Cover image, headline, category, date; pull to refresh, infinite scroll |
| Article | Language toggle, text size control, system share (sharing the website URL) |
| Offline | Opened articles remain readable offline for 30 days |
| Push | Subscribable by category (see 6.3) |

**One hard rule**: the winners announcement post (category 7.1 tagged `Fan Club Prize Draw`) **must never be included in any push category**. The website has committed to no individual winner notification, and a category push covering it would circumvent that commitment. This must be enforced at system level in admin M3, not left to human vigilance.

**Launch prerequisite**: article bodies for the 83 news items have not yet been produced; the complete feature sits in Phase D.

### 3.5 Member centre

All fields and rules carry over from website 3.14; the app adds no member data fields of its own.

| Feature | Description |
|---|---|
| Member profile | Name, mobile, email, date of birth, language preference; change password, delete account |
| Membership status | Tier, member number, expiry date, state (pending / active / expired) |
| Renewal prompt | A banner in the Member tab from 30 days before expiry, plus a push (see 6.2) |
| Jersey registration | Size, collection method (post or in person), delivery details; status shown as pending / dispatched / collected |
| My bookings | See 3.9 |
| Prize-draw information | See 3.11 |
| Benefits table | See 3.7 |

**Explicitly not built** (matching the website 3.14 exclusion list): my donations, my students, my calendar, on-site messaging beyond notification preferences, a members-only content area, my draw serial number, and win lookup.

### 3.6 Digital membership card and mobile wallet

#### In-app card

Card face: member number, QR code, name, tier, expiry date, **last synced time**.

| Rule | Description |
|---|---|
| QR contents | The public verification URL `/m/<token>`, identical to the website |
| Verification response | **Unchanged**: first character of the name, member number, tier, valid or expired. **The app must not request any additional field** |
| Offline | The card face and QR are presentable offline, but validity reflects the last sync; a warning is required after seven days without one |
| Token regeneration | Members may regenerate it; the old token is invalidated immediately and **the wallet pass must be updated in step** |
| Fast access | Biometric unlock or a shortcut may be offered (optional) |

**No scan-to-redeem**: stores still verify by eye; the app provides no store-side scanner, no counting, no store reports, and stores need no account — identical to website 8.4.

#### Wallet pass

The membership card can be written to Apple Wallet and Google Wallet and shown from the lock screen. This is the single largest experience difference for a paying member standing at a store counter.

| Item | Rule |
|---|---|
| Pass fields | Identical to the card face: member number, name, tier, expiry, QR |
| **QR token** | **Must be the same token as the in-app card.** Issuing two is forbidden — two tokens mean two revocable states, and one will inevitably be missed on revocation |
| Updates | On renewal, tier change, or token regeneration, the server pushes a pass update; **the user need not re-add it** |
| Revocation | The pass must be invalidated on account deletion or membership termination |
| Certificates | The Apple Pass Type ID and Google Wallet Issuer certificates are held in admin M5, with expiry tracked and rotation written to the audit log |
| **Not built** | No coupons in the pass, no points balance, **no location-triggered reminders** (which would reintroduce the excluded points and redemption concepts) |

### 3.7 Joining and upgrading

| Block | Content |
|---|---|
| Tier explanation | The difference between a registered member (free) and a fan club member (paid) |
| Plan comparison | Fee, season period, `card_quota`, `jersey_quota`, family plans |
| **Benefits table** | Grouping (card / store discounts / jersey / events), free-tier value, paid-tier value |
| Payment | See section 5 |

**The benefits table is the fourth use of one dataset** (the first three being the website join page, the fan club page 8.2, and the Member Centre upgrade page). **Admin K4 remains the single point of maintenance**; the app **must not hard-code any benefit text**, and must not render it as an image — it needs both languages and must be readable by screen readers.

**Visible without signing in**: the benefits table and plan comparison are the key to conversion and carry no sign-in wall.

### 3.8 Partner stores and the nearby map

This is where the app differs most sharply in value from the website. A member out and about wondering "is there somewhere nearby that gives me a discount?" is a moment where a paid membership pays for itself — and it is a question a website answers badly.

| Feature | Description |
|---|---|
| **Distance sorting** | Straight-line distance from the device location, nearest first, with the distance shown |
| **Map view** | List and map toggle, with category icons on the map |
| **One-tap navigation** | Opens system maps directed at the store |
| **One-tap calling** | Dials the store directly |
| Filters | The existing dimensions: category (food and drink / sports equipment / health / education / lifestyle services) and area |
| Detail | Name, photo or logo, category, address, phone, opening hours, map link, website or social link, offer, **applicable tier** |
| Tier labelling | "All members" and "Paid members only" must be clearly distinguished, with an upgrade CTA shown to non-paying users on the latter |

**Public, with no sign-in wall** — as on website 8.4, this is the main driver of conversion.

**Location permission rules**:

1. Location is used **in the foreground only**; **no background tracking**.
2. Permission is requested only after explaining the purpose in context, never in a prompt on launch.
3. Where the user declines, **the feature must not break**: fall back to manual area filtering plus the admin ordering, and keep an entry point for looking up an address by hand.
4. **Coordinates are never uploaded and never stored**; distance is calculated on the device.

**Data prerequisite**: `PartnerStore` today holds only an address string and **no geographic coordinates**, so distance sorting cannot be built. The data model adds `lat` / `lng` (see 10.2), and admin K4 gains coordinate fields plus a "locate from address" helper, **saved after human confirmation** — no run-time geocoding, which would be slow, costly on every app launch, and would send user positions outward.

**Still not built**: scan-to-redeem, redemption counts, store accounts, loyalty points, stored-value wallet.

### 3.9 Programs and camps

**Scope, first**: the website **already has online booking** (listed on the children's training, summer camp, winter camp, and specialist training pages), admin P3 booking management exists in full, and the types `Program` / `Session` / `Registration` / `Trial` are all defined. **Booking in the app is therefore not a new feature; it is a mobile interface onto an existing mechanism.**

Only three small items were moved out of scope in website v2.0, and only two of them return here:

| Item | Built | Reason |
|---|---|---|
| **Form pre-filling** | Yes | An operation on the member's own data; low sensitivity. Typing on a phone is painful, so the value here is an order of magnitude above the website |
| **My bookings (attribution)** | Yes | Shows the member's own bookings: program, session, time, place, payment state. Again the member's own data only |
| **Parent–student linking** | **No** | Creates a durable adult-to-minor association, a step up in sensitivity. Family memberships deliberately avoided it ("a family membership is only a different `card_quota` / `jersey_quota`; **no student linking relationship is required**"). Student details still live in `Registration` as before; only the `Member`-to-student association table is not created |

**Implementation**: `Registration` gains a `member_id` (**nullable** — non-members may still book, and this rule must not change), and the admin P3 booking list gains a "member" column. **No new type is introduced.**

**Existing rules carried over unchanged**:

| Rule | Source |
|---|---|
| Booking states: pending → confirmed → paid → completed / cancelled / waitlisted | Website P3 |
| Session states: open / full / waitlist / closed, with the front end showing "Book now / Join waitlist / Closed" | Website P2 |
| **Payment is offline** (bank transfer or in person, marked by admin staff) | Website P3 |
| Booking fields: student details, parent contact, health declaration, notes | Website P3 |
| **Program sessions do not enter the calendar** | Website data-consistency principle |

**Two boundaries**:

1. **In-app LINE Pay covers membership fees only and does not extend to course fees.** Otherwise section 5 becomes two problems rather than one, and course refund rules are far more complex than membership ones.
2. **"My bookings" may display session times but must not write to `CalendarEvent`**, nor appear in the fixtures calendar. A single export to the device calendar may be offered.

**Trials**: `Trial` shares `Registration` and is covered by the same mechanism; no separate flow.

### 3.10 Manga reader

Vertical scrolling on a phone is the natural medium for manga — one of the few places where the app is plainly better than the website.

| Feature | Description |
|---|---|
| Shelf | Episode list, covers, new-episode markers |
| Reader | Vertical scroll, pinch zoom, immersive mode hiding the chrome |
| Offline | Per-episode download on request, deletable by the user |
| Progress | Reading position remembered locally; no cross-device sync required |
| Languages | Switching per the rules in 2.5 |
| Characters | Character pages, linked to the players they are based on |

**Launch prerequisite**: the state of the manga assets and their digital distribution rights is unconfirmed (see 16.2). This section sits in Phase D.

### 3.11 Prize-draw information (read-only)

The website has explicitly excluded an entire list of public prize-draw features. **The design goal here is to let a paying member know they are eligible without breaching any item on that list.**

**The app does exactly four things**:

| # | Displayed | Source |
|---|---|---|
| 1 | **Personal eligibility (boolean)**: "your membership is valid at the eligibility cut-off and you will be entered automatically", or "your membership does not cover the cut-off" | Existing `Member` membership status |
| 2 | Rules, prizes and quantities, notes (both languages) | Existing public `MemberDraw` fields |
| 3 | Draw time and setting (in person or livestream) | `MemberDraw` |
| 4 | Once results are published, **a link to the News post** | `Article` |

**Point-by-point against the website's exclusions**:

| Website exclusion | Breached | Explanation |
|---|---|---|
| ✗ Draw page and entry button | No | There is no entry action. Eligibility is automatic and requires nothing from the member |
| ✗ My draws / my serial number | No | **Serial numbers are never shown.** They exist only in the admin snapshot and the on-site CSV |
| ✗ Serial-number or win lookup | No | There is no lookup field of any kind |
| ✗ On-site winners list page | No | Results always link to the News post; the app builds no list page |
| ✗ Online draw animation | No | The draw happens in person or on a livestream; the app renders nothing of it |
| ✗ Live counter of eligible members | No | **See the grey area below** |
| ✗ Share-to-improve-chances mechanism | No | One number per person, unaffected by any behaviour |
| ✗ Winner notification email | No | The five system emails stand. **App push must never be used for individual winner notifications** (see 6.8) |

> **The grey area, stated plainly**: a **personal boolean** answering "am I eligible?" is not the same as a **live counter of eligible members**. The former simply presents existing membership status and creates no new data; the latter discloses a total that invites "join now to dilute the odds" or "join while numbers are low" dynamics. **The app shows the former and explicitly does not build the latter.**

**Data restriction**: the app reads live membership status from `Member` and the public fields of `MemberDraw`. It **must not read `DrawRoster`** — the roster snapshot is an admin audit asset and is not exposed to any front end.

### 3.12 Partners and sponsors

| Block | Content |
|---|---|
| Our Partners | Logo wall grouped by type (strategic / international / training / education / brand), with detail (partnership content, dates, website link) |
| Our Sponsors | Grouped by tier (title / official / supporting), with links to sponsorship stories |
| Enquiries | Routed to the website's partnership and sponsorship enquiry form |

**Sponsor exposure is not advertising** — the boundary most easily confused in implementation:

| | Sponsor logo wall (this section) | Advertising slots (3.13) |
|---|---|---|
| Source | `Sponsor` / `Partner` | `AdCampaign` / `AdCreative` |
| Position | Fixed partner area and lower home screen | Designated slots, rotating |
| **Impressions counted** | **No** | Yes |
| **In advertising reports** | **No** | Yes |
| Labelled "advertisement" | No | **Required** |

Where a sponsorship contract includes "N app placements", the correct approach is to create an `Advertiser` in admin E5 linked back to the existing `Sponsor` via `sponsor_id`, then open an `AdCampaign` — only then does performance enter the reports. **Never report page views for the logo wall**; that number reconciles with nothing.

**Asset red line**: no partner or sponsor names and logos have been supplied. Until they are, **neither the app nor the store screenshots may carry placeholder logos** — doing so would assert commercial relationships that do not exist. Empty positions use a "partnership enquiries welcome" placeholder instead.

### 3.13 How advertising appears

| Item | Rule |
|---|---|
| Slot positions | Home `home_top` and `home_mid`; fixture list `schedule_list`; news list `news_list`. **The definitive list is maintained in admin E5** |
| **Advertising label** | Every slot **must** carry a clear "Advertisement" or "Sponsored" label and must never be mistaken for editorial content |
| Rotation | Weighted random, constrained by the slot's per-session cap and the per-person frequency cap |
| Clicks | Open the external browser or a designated in-app screen; external links must show a leaving-the-app notice first |
| Load failure | Show the slot's **fallback creative** (club content), so **a slot is never blank** |
| Sizing | Fixed height; **advertising loading must never shift the layout** |
| User control | Settings include a "why am I seeing advertising?" explanation |
| **Not built** | Splash advertising, interstitials, video pre-roll, dismissible floating ads |

**Restriction on child-facing content**: **no advertising slots are placed** on manga, academy, or program screens. These have audiences that include minors and sit alongside photographs of minors; commercial messaging does not belong there.

### 3.14 Notification centre and preferences

| Feature | Description |
|---|---|
| Notification centre | An in-app inbox listing received pushes, retained 90 days, readable offline |
| Category toggles | Fixture reminders / results / news / membership and jersey, each independently switchable |
| Squad subscriptions | Subscribe to the squads of interest; fixture reminders are segmented accordingly |
| Quiet hours | A daily do-not-disturb window |
| Permission denied | The notification settings screen must explain the state clearly and offer a shortcut into system settings |

**This is the app's notification centre, not the website's** — the website still has no on-site messaging or notification preferences, and that rule is unchanged.

### 3.15 Settings and account

Language, push permission and categories, location permission explanation, clear cache, offline content management, privacy policy, terms of service, "why am I seeing advertising?", version and update check, contact us, sign out, and **delete account**.

**Account deletion must be completable inside the app** (a hard store requirement); "please write to us" is not acceptable. Deletion follows the website rules: member personal data is erased, but a locked prize-draw snapshot retains only the member number and masked name.

---

## 4. Membership and authentication

### 4.1 One account

**An app account is a website `Member`** — no separate roster, no separate numbering, no separate tiers.

| Principle | Description |
|---|---|
| One dataset | A member who registers in the app appears in admin K1 exactly as a web registrant does, distinguished only by a `registration source` value of `App` |
| One number | Member numbering rules are unchanged by the app |
| One card | One membership card token, shared by the app, the mobile wallet, and the website |
| One set of benefits | Admin K4 remains the single point of maintenance for the benefits table |

**A paying member is a fan club member**, not a second identity, and the fan club keeps no separate roster — as on the website.

### 4.2 Sign-in methods

| Method | Description |
|---|---|
| Email and password | Activated by a verification email; password reset supported |
| **One-tap LINE sign-in** | Reuses the website mechanism. One member dataset, merged by matching email or mobile number |
| Sign in with Apple | **Open item**: Apple has historically required it alongside third-party sign-in. See 16.2 |
| Google sign-in | **Not adopted** — as on the website |

**At least one sign-in method must remain**: before unlinking LINE, the account must be confirmed to have a password set.

Failed-attempt limits and "remember me" follow the website rules.

### 4.3 Sessions and tokens

| Item | Rule |
|---|---|
| Access token | Short-lived, renewed automatically by a refresh token without user involvement |
| Refresh token | Long-lived, but **must be revocable server-side** |
| Multiple devices | One account may be signed in on several devices |
| Sign out everywhere | Members can sign out of all devices from settings; a password change forces it |
| Storage | Tokens must be held in the device's secure storage, **never in plain files or ordinary preferences** |
| Idle | Prolonged inactivity (90 days suggested) requires signing in again |

### 4.4 Device registration

`AppDevice` is created **on first launch**, without sign-in — because unsigned users may still subscribe to fixture reminders.

| Field | Description |
|---|---|
| `device_install_id` | A random value generated at install. **Invalidated on uninstall, never shared across apps or devices, and never derived from an advertising identifier** |
| `member_id` | **Nullable.** Bound on sign-in, unbound on sign-out while the device record is retained |
| Push token | The APNs / FCM token, **treated as personal data** (see 12.1) |
| Other | Platform, OS version, app version, language, push permission state, first and last active times |

**One member may hold several devices; one device is bound to at most one member at a time.**

### 4.5 Minors and guardian consent

The website rules carry over, implemented in the app:

1. Registration must include an **age gate** (date of birth).
2. **Under-18s require guardian consent**, and the app must present the consent flow and explanation.
3. Personal data handling for underage members is identical to other members; all of it is restricted data.
4. Student details in program bookings are entered by the parent and **create no account association between parent and student** (see 3.9).

### 4.6 Account deletion and data erasure

| Item | Rule |
|---|---|
| Path | Completable directly in the app; a write-to-us-only route is not acceptable |
| Confirmation | Two-step confirmation explaining the consequences for membership, jersey records, and bookings |
| Erased | `Member` personal data, `AppDevice` binding and push token, wallet pass invalidation, membership card token revocation |
| **Retained** | A locked `DrawRoster` snapshot **retains only the member number and masked name**, with everything else erased; `MembershipPayment` is retained per accounting requirements |
| Irreversible | **Locked historical draw rosters must never be altered** — doing so would destroy the auditability of the draw |

---

## 5. In-app payment (LINE Pay)

### 5.1 Scope-change statement

The website specification originally stated "no cart, no payment gateway, no card data" and noted in 3.14 that LINE Pay API checkout, refunds, and reconciliation were out of scope and that **adopting them "would require amending the assumption in section 10"**.

**This section is that amendment.** Website specification v2.5 has scoped "no payment gateway" **for the second time**:

| Subject | Payments | Collecting entity |
|---|---|---|
| Website public web front end | **Still no gateway**; payment links and in-person collection retained | Club |
| Charity Donation Platform | Integrates the LINE Pay Online API | **Association** |
| **Mobile App (this document)** | **Integrates LINE Pay, membership fees only** | **Club** |

**"No card data" remains globally applicable** — the app lands no card data either.

**This section covers membership fees only.** Course fees, merchandise, and donations are all out of scope.

### 5.2 Collecting entity and merchant account

> **This is the easiest thing in this section to get wrong, and the most serious.**

The Charity Donation Platform uses the **Taiwan Football Strategic Development Association's** LINE Pay merchant account. This app's membership fees are collected by **Taichung Rock FC**, which **must apply for and use the club's own merchant account — never a shared one**.

Getting the collecting entity wrong breaches two things at once: tax attribution of revenue, and the Charity Donation Act — the Association's fundraising merchant account must not be used to collect the club's commercial membership fees.

**Launch prerequisite**: the club's LINE Pay merchant account has not yet been applied for (see 16.2). Until it exists this section cannot be implemented.

Invoicing responsibility rests with the club, in the club's name, entirely separate from the Association's invoicing on the charity platform.

### 5.3 Payment state machine

```
Order created → Awaiting payment → Paid → Activated
                       ↓             ↓
                   Timed out   Activation failed (manual handling)
                       ↓
                   Cancelled

Also: Refunded (initiated only from the admin)
```

| State | Description |
|---|---|
| `Order created` | Order number and idempotency key issued; not yet redirected to payment |
| `Awaiting payment` | Redirected to LINE Pay, waiting for the user |
| `Paid` | LINE Pay confirmation received; funds collected |
| `Activated` | Membership is live and `paid_until` has been written |
| `Timed out` | Payment not completed in time (15 minutes suggested); the order closes automatically |
| `Activation failed` | Funds collected but activation failed. **Must raise an alert and be handled manually by support**; never fail silently |
| `Refunded` | Initiated only from the admin; see 5.6 |

### 5.4 Two-stage flow and idempotency

| Step | Action |
|---|---|
| 1 | The app creates an order carrying the plan code and an **idempotency key** (the same key repeated creates only one order) |
| 2 | The server requests payment from LINE Pay and returns the payment page |
| 3 | The user completes payment; LINE Pay calls back to the server |
| 4 | The server **verifies the callback origin and the amount** and marks the order paid |
| 5 | The server calls `POST /api/membership/activate` to activate the membership |
| 6 | The "membership activated" email is sent (one of the existing five system emails) and a push is delivered |
| 7 | A wallet pass update is pushed (see 3.6) |

**Three hard rules**:

1. **Activation is driven by the server-side callback only; a payment result reported by the app must never activate a membership.**
2. **The amount must be recalculated and compared server-side**; an amount sent by the app must never be trusted.
3. **`POST /api/membership/activate` must be idempotent** — a repeated callback must never extend a membership twice.

> When the website specified this endpoint it noted that "should automated collection be adopted later, only the trigger changes and the member module does not". This section delivers exactly that: **the member module's activation logic is unchanged; only the trigger moves from a support agent to a payment callback.**

### 5.5 In-app purchase determination

> **This is the single largest launch risk for this app.**

Apple requires in-app purchase for **digital content and services** sold inside an app. The benefits of a paid membership are **predominantly physical** — a joining jersey (a physical item), partner-store discounts (spending in physical premises), physical prizes in the draw, and priority booking for physical events — which gives a substantial basis for arguing they are physical goods and services, permitting external payment.

**But this is a review judgement, not a written rule, and two paths must be prepared in advance:**

| Option | Approach | Cost |
|---|---|---|
| **A (preferred)** | Payment completed in-app via LINE Pay, arguing the benefits are physical goods and services | A failed review requires resubmission |
| **B (fallback)** | The app shows plans and payment instructions only and **opens an external browser** to complete payment (matching current website practice), returning to the app afterwards | A broken flow and lower conversion |
| **C** | Apply for an External Purchase Link Entitlement (subject to regional rules) | The application process and eligible regions need separate confirmation |

**Specification requirement**: the payment flow must be built to be **switchable** — options A and B share one order and activation path, differing only in whether the payment page opens in-app or in an external browser. **The flow must not be hard-wired to only one of them**, or a failed review means a rewrite.

Should in-app purchase be required, the store commission would directly erode the membership margin, and in-app purchase cannot connect to LINE Pay or the existing activation flow — in that case option B should be preferred.

### 5.6 Refunds, reconciliation and invoicing

| Item | Rule |
|---|---|
| Front end | **No refund requests in the app.** Membership is a time-based service, matching current website practice |
| Admin | Mistaken or duplicate charges are refunded manually by support after review, requiring **separate authorisation** and an audit-log entry |
| Reconciliation | The admin provides a report reconciling LINE Pay transactions against `MembershipPayment`, with discrepancies flaggable and annotatable |
| Invoicing | Issued by the club in the club's name. **Entirely separate from the Association's invoicing on the charity platform; settings are not shared** |
| Membership handling | After a refund the membership state must be reversed, `paid_until` rolled back, and the reason recorded |

---

## 6. Push notifications

### 6.1 Scope-change statement

The website specification excludes notification features in four places, and what it excludes is **LINE push** and an **on-site web notification centre**. This app uses operating-system native push (APNs / FCM), which is a different thing — but that difference is **not used to slip past the rule**. Website specification v2.5 states the qualification explicitly:

| Subject | Notifications |
|---|---|
| Website public web front end | **Still none**: no notification centre, no notification preferences, no LINE push, no newsletter campaigns |
| **Mobile App (this document)** | **Native push and an in-app notification centre** |

**The five system emails stand** (registration verification, password reset, membership activated, 30-day expiry reminder, expiry notice) and are **neither increased nor reduced by push**. Push is a second channel for existing notifications, not a new class of notification.

### 6.2 Notification types and triggers

| Type | Trigger | Segment | Default |
|---|---|---|---|
| Fixture reminder | Two hours before kick-off | Devices subscribed to that squad | On |
| Venue confirmed | A fixture's `venue` changes from `TBC` to an actual venue | Devices subscribed to that squad | On |
| Fixture change | Date, time, or venue changed; fixture postponed | Devices subscribed to that squad | On |
| Result published | A score is entered and published in admin C4 | Devices subscribed to that squad | On |
| Article published | An article is published in admin B2 | Devices subscribed to that category | Off |
| Membership expiry | 30 days and 7 days before expiry | That member's devices | On |
| Membership activated | Payment activation succeeds | That member's devices | On |
| Jersey status | Status changes to "dispatched" | That member's devices | On |
| Booking status | Status changes to "confirmed" or "paid" | That member's devices | On |
| General announcement | Sent by hand from admin M3 | Segmentable | On |

**Article push defaults to off** — pushing all 83 articles at their publishing cadence would be intrusive; users opt in.

### 6.3 Segmentation

Three dimensions only, with **behavioural targeting deliberately excluded**:

| Dimension | Description |
|---|---|
| Membership tier | All / `fan_club` only / `registered` only / signed-out devices |
| Subscribed squad | `PushTopicSubscription` (device × `team_code`) |
| Language | `zh` / `en`; push copy must exist in both, sent per the device language |

**Not built**: personalised push based on browsing history, click behaviour, geography, or purchase history.

### 6.4 Preferences and quiet hours

| Feature | Description |
|---|---|
| Category toggles | Independently switchable (fixtures / results / news / membership and jersey) |
| Squad subscriptions | Multiple selection, first team subscribed by default |
| Quiet hours | A daily do-not-disturb window, 22:00–08:00 suggested as the default |
| Permission denied | The app must show the state and offer a shortcut to system settings, and **must not nag with repeated prompts** |
| All off | Users may disable all push; **membership expiry reminders still arrive by the existing system email** (important notices must never depend on push alone) |

### 6.5 Sending (admin M3)

Scheduled sending, pre-send preview (one per language), test sends to designated devices, **an audience-size estimate before sending**, sending, cancelling undelivered batches, resending failures, and a per-batch record with the operator.

**Large sends require two-step confirmation** and must display the estimated device reach.

### 6.6 Delivery statistics

Three aggregate figures only: **sent, delivered, opened**, by batch × platform × language.

**No individual-level push behaviour tracking** — the system does not record whether a given member opened a given push, and push-open behaviour is never used for subsequent segmentation.

### 6.7 Personal data and disclosure

1. **Push tokens count as personal data**: encrypted at rest, never displayed externally, never included in ordinary exports.
2. **The member terms must add a disclosure covering push token collection**; push must not be enabled before this is done (mirroring how the prize-draw disclosure was handled).
3. Where a third-party push service is used, **cross-border data transfer** must be disclosed in the privacy policy.
4. Opt-out paths must be clear: in-app category toggles, system-level permission, and account deletion all work.

### 6.8 Explicitly not built

| Item | Reason |
|---|---|
| **Individual winner notifications** | The website commits to announcing winners through News only and to keeping five system emails. **Push must never become a back door around that commitment.** Admin M3 must block this at system level, not rely on human vigilance |
| A sixth marketing email | The five system emails stand |
| LINE push | Already excluded on the website and excluded here — the LINE official account serves only as a sign-in and linking channel |
| Cross-app tracking | No advertising identifiers, no cross-app behavioural joining |
| Location-triggered push | Location is foreground-only; no background geofencing |

---

## 7. Owned advertising slots and performance measurement

### 7.1 Scope-change statement

The website specification states in two places that there are "no separate fixed slots" (homepage composition and admin B4 banner management). Its subject is **the composition of seasonal content on the website homepage**, and its intent is to stop the homepage being sliced up by placements.

Website specification v2.5 has scoped this to "**this site's public web front end** has no separate fixed slots", noting that the app's owned advertising slots are app scope and **do not affect the website homepage**.

**One exception stands unchanged**: **there is no fixed charity slot on the homepage, and the app carries none either.** The Charity Donation Platform is run and collected for by the Association, so this club's app must not sell or grant charity-related placements, which would blur into the Association's as-yet-unconfirmed fundraising eligibility.

### 7.2 Slot definitions

A slot is a long-lived asset, rarely changed once created.

| Field | Description |
|---|---|
| `slot_code` | Unique identifier, named `<screen>_<position>`, e.g. `home_top` |
| Name (zh / en) | The name shown to sales staff and advertisers |
| `surface` | `app` (with `web` reserved, so opening website slots later needs no new table) |
| Screen position | Maps to the screen codes and block order in 2.2 |
| Creative spec | Aspect ratio, minimum pixels, file size limit, permitted formats |
| Video permitted | Boolean |
| Per-session impression cap | Prevents repeated exposure within one usage session |
| Rotation size cap | How many flights may share the rotation at once |
| **Fallback creative** | Club content shown when there is no flight or a load fails, so **a slot is never blank** |
| Enabled | Published or withdrawn |

**No slots on child-facing screens**: no `AdSlot` is created for manga, academy, or program screens (see 3.13).

### 7.3 Advertisers and contracts

| Field | Description |
|---|---|
| Name (zh / en), tax ID, contact, phone, email | Basic details |
| **`sponsor_id`** | **Nullable**, pointing at an existing `Sponsor` |
| Contract notes, partnership dates | |
| Status | In discussion / active / ended |

**`sponsor_id` is the key design decision**: where one company both sponsors and buys advertising, it avoids a duplicate record, keeps the performance attribution of the two distinct, and removes the need to maintain two sets of contact details.

**No advertiser self-service accounts in v1** — mirroring the Charity Donation Platform's existing decision not to build store accounts; reports are exported and sent by the sales team.

### 7.4 Flights and rotation weights

| Field | Description |
|---|---|
| `advertiser_id`, `slot_id` | One flight binds one slot. Multi-slot requirements are expressed as several flights; **no many-to-many** |
| Name, start and end times | |
| `weight` | Rotation weight; flights on one slot at one time are chosen by weighted random selection |
| Daily impression cap | |
| Per-person frequency cap | How many times one device may see it per day |
| `goal_type` | `guaranteed impressions` / `traffic` |
| `goal_impressions` | The target for guaranteed flights |
| `delivered_today` | Today's delivery counter, used for pacing |
| Contract amount | **May be withheld** |
| Status, approver, creator | |

**State machine**:

```
Draft → Pending review → Scheduled → Live → Ended → Closed
                                       ↓
                                    Paused → (may return to Live)

Also: Voided (enterable from any state, irreversible)
```

`Scheduled → Live → Ended` advances automatically from the start and end times; the rest are manual.

**One rule written into the system**: **a flight whose creatives have not passed review must not enter "Live".** Otherwise unreviewed images reach production.

**Pacing (mandatory for guaranteed flights)**: the target impressions must be spread evenly across the flight's days. Without pacing the guaranteed volume burns out in the first two days and the slot sits empty for the rest — which is precisely why the `delivered_today` field exists. **It is not an optional optimisation.**

### 7.5 Definitions of impressions and clicks

> Without this section, advertiser reports cannot be reconciled and slots cannot be sold.

| Event | Definition |
|---|---|
| **Valid impression** | The advertising area **at least 50% visible for at least one continuous second** (per the prevailing IAB/MRC standard). Each item in a rotation counts separately |
| **Valid click** | A deliberate tap on a creative. **Repeated taps on the same creative from the same device within five seconds count once** |
| Invalid traffic | Events queued offline for more than 24 hours, and abnormally high-frequency events from one device, are excluded and never billed |
| Not counted | While a fallback creative is shown, while the app is backgrounded, and when a creative fails to load |

**Time basis**: events are recorded at their **time of occurrence**, not their time of upload. Events queued offline must carry the original occurrence time, and the server must reject any older than 24 hours.

### 7.6 Event collection and aggregation

| Layer | Contents | Retention |
|---|---|---|
| **Raw events** `AdEvent` | Individual impressions and clicks | **90 days** (for dispute audit and deduplication) |
| **Daily aggregate** `AdDailyStat` | Date × flight × creative × slot × platform × language | Long-term |

**Why two layers** (volume estimates, with the assumptions stated):

| Assumed daily active devices | Impressions per person per day | Events/day | Events/year | Raw data |
|---|---|---|---|---|
| 500 | 15 | 7,500 | ~2.7 M | ~270 MB |
| 2,000 | 20 | 40,000 | ~14.6 M | ~1.5 GB |
| 5,000 | 25 | 125,000 | ~45.6 M | ~4.6 GB |

Keeping raw events indefinitely would grow the database to several gigabytes within a year, while reports need only the aggregates. **The 90-day retention is simultaneously a personal-data retention policy**, satisfying the website's non-functional requirement to define retention periods.

**Aggregation job**: runs daily and stamps `aggregated_at` on completion; a failure must raise an alert and must never be skipped silently.

### 7.7 Advertiser reporting (admin E7)

| Item | Contents |
|---|---|
| Dimensions | Flight / slot / creative / platform / language / date |
| Metrics | Impressions, clicks, CTR, unique devices, daily trend |
| Comparison | Guaranteed flights show "target vs delivered" and pacing progress |
| Export | CSV and PDF, **written to the audit log** (who, when, which flight, stated purpose) |
| **Never provided** | Any individual-level data, device lists, or links to member identity |

### 7.8 Privacy and measurement integrity

Three decisions that between them settle three classes of problem:

| Decision | Consequence |
|---|---|
| **No third-party ad networks; direct sales only** | No tracking permission prompt required; the store privacy label can declare no tracking; no ad-SDK child-safety compliance problem exists |
| **No advertising identifiers (IDFA / AAID)** | Deduplication uses `device_install_id` alone (invalidated on uninstall, never shared across apps); unique-device counts use a daily hash of it |
| **No behavioural targeting** | Segmentation reaches only "slot × language × platform"; advertising is never selected from an individual's browsing history |

**These three are also the pitch to advertisers, and they are a selling point**: "our numbers are measured directly by our own system — honest first-party data, not a network's resold estimate."

`AdEvent` **explicitly does not store**: `member_id`, full IP addresses, precise location coordinates, or advertising identifiers.

### 7.9 Disclosure and compliance

1. **Every slot must carry an "Advertisement" or "Sponsored" label** and must never be mistaken for editorial content.
2. Creatives must not use club marks or imply official endorsement unless the advertiser is genuinely a sponsor and the contract says so.
3. Creatives must pass admin review before going live; there must be an **immediate takedown mechanism** for violations or complaints (any single flight can be paused at once).
4. **Not accepted**: tobacco and alcohol, gambling, adult content, medical efficacy claims, financial investment solicitation.
5. No advertising is placed on screens with underage audiences (see 3.13 and 7.2).

---

## 8. Admin functionality

### 8.0 Module overview

App-related admin functionality is split in two, and **deliberately not placed in one module**:

```
M. Mobile App                        (new top-level module)
├─ M1 App releases and version management
├─ M2 App composition and deep links
├─ M3 Push notification management
├─ M4 Devices and push tokens
└─ M5 App settings, certificates and diagnostics

E. Commercial                        (extends the existing module)
├─ E1 Partners                        (existing)
├─ E2 Sponsors and packages           (existing)
├─ E3 Deck downloads and leads        (existing)
├─ E4 Product showcase                (existing)
├─ E5 Advertisers and slots           (new)
├─ E6 Advertising flights and creatives (new)
└─ E7 Advertising performance reports  (new)
```

**Why advertising sits in E rather than M**: permission minimisation. A salesperson selling advertising should not thereby gain app release rights and push-sending rights — **a wrong push is an audience-wide incident; a wrong release locks every user out**. Moreover, one company may be both a `Sponsor` and an `Advertiser`, and keeping them in one module avoids maintaining two sets of contact details.

> **Naming note**: the admin module code `M` and the membership card's public verification path `/m/<token>` are **different namespaces** and are unrelated.

### 8.1 M1 App releases and version management

- Release list: platform, version, build, release date, state (testing / live / withdrawn)
- **Minimum supported version**: apps below it prompt for an update on launch
- **Forced update** and **recommended update** modes, each with its own copy (zh / en)
- Maintenance mode: global or per-platform, with a custom message
- Release notes ("What's New") in both languages

### 8.2 M2 App composition and deep links

- **Home block visibility and order** (the nine blocks in 3.1)
- Shortcut items and order
- More-tab items and order
- Deep-link mapping table: app screen to website URL, for use by push and advertising creatives
- App-only announcement bar: copy (zh / en), link, display period, audience

**Not built**: content CRUD inside the app. All content (news, fixtures, players, stores) is still maintained in its existing module; M2 controls **order and visibility only**.

### 8.3 M3 Push notification management

- Push batch: title and body (zh / en), image, deep-link destination, segmentation, scheduled time
- **Audience-size estimate before sending** (returns a count only, writes nothing)
- Preview (one per language) and test sends to designated devices
- Send, cancel undelivered batches, resend failures
- Send history: batch, time, operator, devices reached, sent / delivered / opened counts
- Rule settings for automated pushes (reminder lead time, expiry reminder days, and so on)

**System-level blocking**: M3 must block any push targeting a winners list, and must block automatic pushes for articles tagged `Fan Club Prize Draw` (see 6.8).

### 8.4 M4 Devices and push tokens

- Device list: platform, OS version, app version, language, push permission state, bound member, last active
- Statistics: device distribution by platform and version (to inform the minimum supported version)
- Cleanup of invalid tokens
- **Permissions**: push tokens and device identifiers count as personal data; only system administrators see full values, other roles see masked ones

### 8.5 M5 App settings, certificates and diagnostics

- App feature flags: remotely disable a single feature without shipping a new release
- **Certificate management**: APNs certificates, FCM configuration, **Apple Pass Type ID**, **Google Wallet Issuer** — expiry tracked with an alert 60 days ahead, and rotation written to the audit log
- Diagnostics: aggregate views of crash rate, API error rate, and launch duration
- Receipt and review of app-side error reports

### 8.6 E5 Advertisers and slots

- Advertiser CRUD (fields per 7.3), linkable to an existing `Sponsor`
- Slot CRUD (fields per 7.2), including fallback creative upload
- Slot preview: an illustration of where the slot sits in the app

### 8.7 E6 Advertising flights and creatives

- Flight CRUD (fields per 7.4) and state-machine operations
- Creative upload and management: language, dark and light variants, A/B markers, alt text, click destination
- **Creative review**: pending / approved / rejected (with a reason)
- Flight conflict view: the flights sharing one slot at one time, with a weight-share preview
- **Emergency pause**: any single flight or creative can be stopped immediately

### 8.8 E7 Advertising performance reports

See 7.7. Dimensions, metrics, export, and audit rules are as stated there.

### 8.9 Extensions to existing modules

The app requires four small extensions to existing modules, **none of which changes existing behaviour**:

| Module | Extension | Section |
|---|---|---|
| **K4 Partner stores** | Adds `lat` / `lng` and a "locate from address" helper; the benefits table's placements change from three to "three plus the app" | 3.8, 3.7 |
| **P3 Booking management** | Adds `member_id`; the booking list gains a "member" column and filter | 3.9 |
| **C4 Fixtures** | Adds `opponent_en` / `venue_en`; `competition` / `status` are promoted from front-end attributes to formal fields | 3.2 |
| **I Site settings / venues** | `Venue` gains coordinates, for fixture navigation and course locations | 3.2, 3.9 |

---

## 9. API and integration requirements

### 9.1 Purpose

This section defines **what** the app needs from a backend, not **how** it is implemented — no framework, ORM, or hosting — and therefore does not breach the technology-selection exclusion in website specification 1.3.

This is the project's first API specification. It sits here rather than in a fourth document because only the app needs an API today; extracting it early would create another surface to maintain. Should the website need one later, it can be split out then.

### 9.2 Endpoint inventory

| Resource | Actions | Caller | Notes |
|---|---|---|---|
| Device | Register / update | Anonymous | `AppDevice` creation and push token updates |
| Settings | Read | Anonymous | Minimum supported version, feature flags, maintenance mode |
| Fixtures | List / single | Anonymous | Filterable by squad and period |
| Squads and players | List / single | Anonymous | |
| News | List / single | Anonymous | Category filtering and pagination |
| Partner stores | List / single | Anonymous | Includes coordinates; **distance is computed on the device** |
| Programs and sessions | List / single | Anonymous | Includes live places and status |
| Program booking | Create | Anonymous or member | Writes `member_id` when signed in |
| My bookings | List | Member | |
| Partners and sponsors | List | Anonymous | |
| Manga | List / episode | Anonymous | |
| FAQ | List | Anonymous | |
| Register / sign in / refresh | Create | Anonymous | |
| Member profile | Read / update | Member | |
| Membership card | Read / regenerate token | Member | |
| Wallet pass | Issue / update | Member | |
| Plans and benefits | List | Anonymous | Sourced from admin K4 |
| Payment order | Create / query | Member | See 5.4 |
| **Membership activation** | Create | **Server internal** | `POST /api/membership/activate`, credential-protected |
| Jersey registration | Read / update | Member | |
| Prize-draw information | Read | Member | Public fields and the personal eligibility boolean only; **must never return `DrawRoster`** |
| Advertising delivery | Read | Anonymous | Returns the creative to display for a slot |
| Advertising events | Batch report | Anonymous | Impressions and clicks, carrying the original occurrence time |
| Push subscriptions | Update | Anonymous or member | Categories and squads |
| Notification centre | List | Anonymous or member | |

### 9.3 Authentication boundaries

| Level | Scope | Method |
|---|---|---|
| **Anonymous read** | Fixtures, news, squads, stores, programs, partners, manga, FAQ, advertising delivery, settings | No sign-in, but a device identifier is required |
| **Member token** | Member profile, card, wallet, payment, jersey, my bookings, prize-draw information | Access token (4.3) |
| **Admin** | All administrative write operations | The admin's existing role-based permissions |
| **Server internal** | `POST /api/membership/activate` | Credential-protected, **never publicly exposed** |

**One hard rule**: any endpoint returning member personal data **may return only the caller's own data**. There is no public endpoint that looks up another person's data by member number — the public verification page `/m/<token>` is the sole exception, and its response fields are strictly limited.

### 9.4 Versioning

- A version prefix in the path
- Breaking changes require a **parallel period**, with the previous version maintained for at least six months
- When an older app encounters a removed endpoint it must present a clear "please update the app" message and **must never show a technical error or a blank screen**
- The minimum supported version is controlled from admin M1 and read from the settings endpoint on launch

### 9.5 Error format and retries

- **A single error structure**: error code, a user-presentable message (both languages), and whether it is retryable
- Network errors and server 5xx may be retried automatically with exponential backoff; 4xx is not retried
- **Offline queue**: advertising events and push subscription changes may be queued offline and sent later; **payment operations must never be queued offline**
- Error messages must never leak internal implementation details

### 9.6 Rate limiting and idempotency

| Subject | Requirement |
|---|---|
| General read endpoints | A sensible per-device limit |
| Sign-in and password reset | Strictly limited against brute force |
| **Payment order creation** | **Must support an idempotency key**; the same key repeated creates only one order |
| **Membership activation** | **Must be idempotent**; a repeated callback must never extend a membership twice |
| Advertising event reporting | Batched, with server-side deduplication (same device, same creative, same second) |

### 9.7 Bringing the existing endpoint under these rules

`POST /api/membership/activate` was reserved by the website specification and not enabled in that phase. This document brings it formally under management:

| Item | Rule |
|---|---|
| Triggers | The app's successful LINE Pay callback, and manual activation by support in the admin (existing) |
| Authentication | Credential-protected, callable only from within the server |
| Idempotency | Required, keyed on the order number |
| Side effects | Writes `MembershipPayment`, updates `Member.paid_until`, sends the activation email, pushes a wallet pass update, sends a push notification |
| Audit | Records the trigger source, the operator or order number, and the membership state before and after |

> The note made when the website designed this endpoint — "should automated collection be adopted later, only the trigger changes and the member module does not" — is delivered here in full. **Not one line of the member module's activation logic needs to change.**

---

## 10. Data model and content types

### 10.1 New types

Ten app-specific types: six for advertising, four for app operations.

**Advertising**

| Type | Description | Key fields | Relations |
|---|---|---|---|
| `AdSlot` | Advertising slot | `slot_code`, name (zh/en), `surface`, screen position, creative spec, video permitted, per-session impression cap, rotation size cap, fallback creative, enabled | AdCampaign |
| `Advertiser` | Advertiser | Name (zh/en), tax ID, contact, phone, email, contract notes, partnership dates, **`sponsor_id` (nullable)**, status | Sponsor, AdCampaign |
| `AdCampaign` | Flight | `advertiser_id`, `slot_id`, name, start and end, `weight`, daily impression cap, per-person frequency cap, `goal_type`, `goal_impressions`, `delivered_today`, contract amount (may be withheld), status, approver, creator | Advertiser, AdSlot, AdCreative |
| `AdCreative` | Creative | `campaign_id`, language, image or video, alt text, headline, CTA text, click destination, dark and light variants, A/B marker, review status, rejection reason | AdCampaign |
| `AdEvent` | Raw event | `type` (`impression` / `click`), `creative_id`, `campaign_id`, `slot_id`, `occurred_at`, `device_install_id`, platform, app version, language, `aggregated_at`. **Stores no `member_id`, no full IP, no location coordinates, no advertising identifier** | AdCreative, AdCampaign, AdSlot |
| `AdDailyStat` | Daily aggregate | Date × `campaign_id` × `creative_id` × `slot_id` × platform × language → `impressions`, `clicks`, `unique_devices`, `ctr` | AdCampaign, AdCreative, AdSlot |

**App operations**

| Type | Description | Key fields | Relations |
|---|---|---|---|
| `AppDevice` | Device | `device_install_id`, platform, OS version, app version, language, push token (encrypted), push permission state, `member_id` (nullable), first and last active times | Member, PushTopicSubscription |
| `PushTopicSubscription` | Push subscription | `device_install_id`, topic type (squad / news category), topic value, subscription state | AppDevice, Team |
| `PushMessage` | Push batch | Title and body (zh/en), image, deep link, segmentation, scheduled time, status, operator, sent / delivered / opened counts | — |
| `AppRelease` | App release | Platform, version, build, release date, state, minimum-supported flag, release notes (zh/en), forced or recommended | — |

### 10.2 Extensions to existing types

**All are field additions; no existing field changes meaning, and no current website functionality is affected.**

| Type | New fields | Purpose | Section |
|---|---|---|---|
| `PartnerStore` | `lat`, `lng` | Nearby-store distance sorting and map | 3.8 |
| `Venue` | `lat`, `lng` | Fixture venue navigation, course locations | 3.2, 3.9 |
| `Registration` | `member_id` (**nullable**) | My bookings, form pre-filling | 3.9 |
| `Match` | `opponent_en`, `venue_en`, `competition`, `status` | English display; front-end attributes promoted to formal fields | 3.2 |
| `Member` | `registration source` gains an `App` value | Identification in admin K1 | 4.1 |
| `Sponsor` / `Partner` | High-resolution assets (@2x / @3x), dark-mode logo variant, in-app ordering | Logo rendering in the app | 3.12 |

> The **base field definitions** for `Sponsor` / `SponsorPackage` / `Partner` are completed **in the website specification, section 5** (v2.5 promoted the admin E1/E2 lists to field tables). This document defines only the app-specific extensions and **does not redefine website types here** — that would create a second source of truth.

### 10.3 Boundaries against existing types

**Five "commercial counterparties" must not be conflated** — the most easily confused set of concepts in this project:

| Type | Who | Where | Payments | Revenue share | Impressions |
|---|---|---|---|---|---|
| `Partner` | B2B partner | Website 9.1 logo wall | No | No | No |
| `Sponsor` | Sponsor | Website 9.2 | No (offline contract) | No | No |
| `PartnerStore` | Partner store (member discounts) | Website 8.4, app 3.8 | No | No | No |
| `DonationStore` | Charity site scan-in store | Charity Donation Platform | **Yes** | **Yes** | No |
| **`Advertiser`** | **Advertiser** | **App advertising slots** | Offline contract | No | **Yes** |

One real company may be several of these at once; **create a separate record for each and never share one**. The sole exception is `Advertiser.sponsor_id`, which links back to a `Sponsor` to avoid duplicate contact maintenance — **it is a link, not a merge**.

**Other boundaries**:

| Case | Correct handling |
|---|---|
| Prize-draw roster | `DrawRoster` is an **immutable snapshot** and **the app must not read it**. The app reads only live membership status from `Member` and the public fields of `MemberDraw` |
| Draw eligibility | A **boolean** derived at `Member` level; there is no "draw entry", "draw ticket", or "points" type |
| Program sessions | Belong to `Session` and **do not enter `CalendarEvent`**. "My bookings" may display times but must not write to any calendar type |
| Fan club members | Not a separate roster; a `fan_club` tier marker on `Member` |
| Family memberships | Only a different `card_quota` / `jersey_quota` on `MembershipPlan`; **no student linking relationship is created** |
| Advertising vs sponsor exposure | `AdEvent` records impressions of `AdCreative` only. The sponsor logo wall **generates no `AdEvent` whatsoever** |
| Device vs member | `AppDevice` exists independently (registered even when signed out); `member_id` is a **nullable weak link** released on sign-out |

### 10.4 Bilingual field rules

The website rules carry over without exception:

1. **Every content type visible in the app must carry both `zh` and `en` fields**; English may be empty but the field must exist.
2. Where English is missing, fall back to Traditional Chinese with a marker.
3. The architecture must accommodate a third language without code changes.
4. **This applies equally to advertising creatives and push copy** — `AdCreative` is uploaded per language, and `PushMessage` titles and bodies must exist in both.

---

## 11. Roles and permissions

The website's nine roles carry over, with two new columns — **Mobile App** and **Advertising**:

| Role | Mobile App (M) | Advertising (E5–E7) |
|---|---|---|
| System administrator | Full | Full |
| Content editor | M2 composition | — |
| Football / team manager | — | — |
| Academy / programs manager | — | — |
| Commercial / sponsorship | — | **Full** |
| PR / media | M3 push (**needs approval**) | Reports (read) |
| Support / administration | M4 view (masked) | — |
| Finance | — | Contract amounts (read) |
| Viewer | Read-only | Read-only (no amounts) |

**Five additional rules**:

1. **App releases and forced updates (M1) are restricted to system administrators** — a wrong minimum supported version locks every user out of the app.
2. **Push sending (M3) requires two-person approval**: PR/media may draft and preview; a system administrator must approve the send. Push is an irreversible, audience-wide action.
3. **Push tokens and device identifiers (M4) count as personal data**; only system administrators see full values, other roles see masked ones.
4. **Advertising contract amounts** are visible only to commercial/sponsorship and finance; other roles see "withheld".
5. **Advertising report exports must be written to the audit log** (who, when, which flight, stated purpose), mirroring the existing member-list export rule.

**Unchanged**: member data (module K) permissions are **not widened by the app** — still restricted to system administrators and support/administration, with masked values for other roles and exports requiring separate authorisation and an audit entry.

---

## 12. Personal data, security and regulation

### 12.1 Personal data

| Data | Classification | Handling |
|---|---|---|
| Member profile data | Personal data | Website rules carry over; restricted data |
| **Push token** | **Counts as personal data** | Encrypted at rest, never displayed externally, never in ordinary exports |
| **Device identifier** | Quasi-personal data | Generated at install, invalidated on uninstall, never shared across apps; used only for deduplication and diagnostics |
| **Advertising impression events** | De-identified | No `member_id`, no full IP, no location coordinates, no advertising identifier; raw events deleted after 90 days |
| **Location coordinates** | Personal data | **Never uploaded, never stored**; distance is computed on the device |
| LINE link identifier | Personal data | Website rules carry over; encrypted at rest |
| Payment data | Personal data | **No card data stored**; only transaction reference, amount, time, and status |

**Three disclosures must be added to the member terms** (the corresponding feature must not be enabled before each is in place):

1. Push token collection and any third-party service provider (with cross-border transfer disclosed where applicable).
2. Program booking attribution (the association between `Registration` and `Member`).
3. The purpose and scope of location permission.

### 12.2 Minors and likeness rights

The app introduces an exposure surface **the website's `noindex` cannot protect**: **App Store and Google Play listing pages are public and indexed by search engines.**

| Restriction | Description |
|---|---|
| Photographs of underage students | Until guardian consent is obtained, **must not be used** in public app screens, **store screenshots**, or any advertising creative |
| Player likenesses | Consent must be confirmed to cover the app and the store listing |
| Alt text | Describes the scene only and **never names an individual** — the existing website rule |
| Underage members | Registration requires guardian consent (4.5) |
| Child-facing screens | No advertising on manga, academy, or program screens (3.13, 7.2) |

**The consent status of every store screenshot asset must be confirmed individually** before launch; this is a mandatory check and cannot be taken lightly.

### 12.3 Security and certificate management

| Item | Requirement |
|---|---|
| Transport | Encrypted end to end; downgrades refused |
| Token storage | Held in the device's secure storage, never landed in plain text |
| Membership card token | **Must not be derivable from the member number**; members may regenerate it, and the old token and old wallet pass are invalidated immediately |
| Wallet pass | The pass and the in-app card **share one token** (see 3.6) |
| Certificate rotation | APNs, FCM, Pass Type ID, and Wallet Issuer certificates are managed in admin M5, alerted 60 days before expiry, with rotation written to the audit log |
| Jailbreak / root detection | Optional. Warn of the risk on detection; **do not block outright** |
| Error messages | Must never leak internal implementation details |
| Audit retention | Website rules carry over; audit logs retained at least 12 months |

### 12.4 Store compliance

| Item | Requirement |
|---|---|
| Age rating | Must be confirmed whether content involving minors places the app in a children's category (see 16.2). This specification's "no ad networks, no behavioural targeting, no advertising identifiers" position already aligns with those restrictions |
| Privacy labels | Declared accurately. Since no advertising identifier is used and no cross-app tracking occurs, "no tracking" may be declared |
| **Account deletion** | **Must be completable inside the app** (3.15); this is a hard requirement |
| External payment | See 5.5 |
| Third-party sign-in | Where LINE sign-in is offered, confirm whether Sign in with Apple is required alongside |
| Privacy policy and terms | App-specific versions must be written and linked from the store listing |

---

## 13. Non-functional requirements

| Item | Requirement |
|---|---|
| **Launch time** | Cold launch to an interactive home screen within 3 seconds (mid-range device, normal network) |
| **Responsiveness** | Tab switching within 300 ms; list scrolling without dropped frames |
| **Offline availability** | The membership card, cached fixtures, read articles, and downloaded manga are fully usable offline (2.4) |
| **Install size** | 60 MB or less recommended; assets are loaded remotely rather than bundled |
| **Data usage** | Typical use under 50 MB per month (excluding manga and video downloads); list images compressed and served per device resolution |
| **Battery** | No background location, no background polling; background work is limited to receiving push |
| **Crash rate** | Crash-free sessions at or above 99.5% |
| **Supported versions** | iOS 15+, Android 10+ (matching the website's existing compatibility statement) |
| **Accessibility** | System text-size support, screen reader support, AA contrast, readable labels on every tappable element; **the benefits table and fixture information must never be delivered as images** |
| **Monitoring** | Aggregate views of crashes, API error rate, launch duration, and push delivery (admin M5) |
| **Data retention** | Advertising raw events 90 days; notification centre 90 days; audit logs at least 12 months |
| **Brand consistency** | Brand pink `#E0218A` and brand black `#231916`; the AA-safe `#D61E83` for small text; only the existing lockups from `brand/svg/` — **no hand-set type, no added year** |

---

## 14. Release and distribution

### 14.1 Developer accounts and entity

| Item | Description |
|---|---|
| Apple Developer Program | A corporate account requires a D-U-N-S number; allow time for the application |
| Google Play Console | Corporate account |
| **Account holder** | **Must be confirmed as the club or the Association** (see 16.2). This decision also determines the payment entity, the contents of the `.well-known` files, the data controller named in the privacy policy, and the entity applying for the wallet and push certificates |
| Store display name | "Taichung Rock FC" recommended |

### 14.2 Store assets

App icon (all sizes), launch screen, store screenshots (all device sizes, both languages), app name and subtitle, short and full descriptions (both languages), keywords, privacy policy URL, support URL, marketing URL.

**Every screenshot asset must first clear the likeness-rights check in 12.2.**

### 14.3 Review preparation and known risks

| Risk | Preparation |
|---|---|
| **In-app purchase determination** (5.5) | Prepare documentation arguing the benefits are physical goods and services; have a build for fallback option B ready |
| Account deletion | Confirm the in-app path completes the deletion |
| Location permission | The purpose string must be specific; vague wording such as "to provide a better service" is not acceptable |
| Push permission | A pre-permission explanation screen is required before the first request |
| Age rating | Declare the nature of the content accurately |
| Empty content | **If partners, sponsors, manga, or player photographs are still absent at review time, empty-state screens must be complete and show no broken images**, or the app is easily judged unfinished |

### 14.4 Release cadence and forced-update policy

- Regular releases every four to six weeks, carrying fixes and small features
- Forced updates are used only for: security issues, breaking API changes past their parallel period, and major feature failures
- A forced update must be preceded by **at least one recommended-update release** as a buffer
- Changes to the minimum supported version are controlled from admin M1, and **the version distribution in M4 must be reviewed first** to confirm the share of affected devices is acceptable

---

## 15. Delivery phases

| Phase | Contents | Prerequisites |
|---|---|---|
| **A — Foundation** | Fixtures and results, news list, squads (read-only), partners and sponsors, FAQ, deep links and Universal Links, both languages, `AppDevice` registration, settings | None |
| **B — Membership** | Registration and sign-in, member centre, digital card and mobile wallet, **partner stores and the nearby map**, plans and benefits, in-app LINE Pay payment, read-only prize-draw information | IAP determination (R1), club merchant account (R2), wallet certificates (R23), store coordinates (R24) |
| **C — Engagement and revenue** | Push notifications, advertising slots and measurement, match-day reminders, **program booking (pre-filling and my bookings)** | Website v2.5 finalised, member terms updated, first advertisers (R4) |
| **D — Content-dependent** | Manga reader, player photographs and biographies, full article text and offline reading | 113 draft articles unblocked, manga assets and rights (R26), player assets (R27) |

**Two notes**:

1. **Partner stores sit in Phase B rather than later** because the feature is inexpensive, has no content blocker, and directly supports the value proposition of a paid membership — it belongs to the same experience as the digital card, and separating them would halve Phase B's persuasiveness.
2. **Content-dependent features form their own Phase D** so that the delivery schedule for the 113 draft articles and the manga assets does not hold up the app's launch.

---

## 16. Open items

### 16.1 Confirmed assumptions

| Item | Decision |
|---|---|
| Squads | `D1` (First Team, the only one club-wide) / `U15` / `U14` / `U12`. **No `D2` is added.** The list is generated from `Team.code`, and the public label is always `First Team` |
| Admin and database | **Shares the website admin and database**, adding module `M` and extending `E` (E5–E7) |
| Entity and collection | Both are **Taichung Rock FC**, entirely separate from the Association that runs the charity platform |
| Payments | **Membership fees only**; not course fees, merchandise, or donations |
| Advertising | **Sold directly, no third-party networks**; no advertising identifiers; no behavioural targeting; no splash advertising |
| Advertiser accounts | **Not built in v1**; reports are exported and sent by the sales team |
| Parent–student linking | **Not built** (website, app, and admin alike) |
| Scan-to-redeem | **Not built** (the existing website decision carries over) |
| Donations | The app **carries no donation payments**; outbound links only, stating that the recipient is the Association |
| Prize draw | The front end shows only the personal eligibility boolean and the rules — **no serial numbers, no lookup, no list page, no counter** |
| Winner notification | **Push must never be used for individual winner notifications**; the five system emails stand |
| Technology selection | Out of scope; replaced by the platform capability requirements in 1.5 |

### 16.2 Still to be confirmed

**Blocking — the corresponding feature cannot be built or launched until resolved**

| # | Item | Impact |
|---|---|---|
| 1 | **In-app purchase determination**: may paid membership use external LINE Pay? The predominantly physical benefits give grounds to argue so, but it is a review judgement | **The single largest launch risk.** A fallback must be prepared in parallel (5.5) |
| 2 | **The club's own LINE Pay merchant account** (the Association's must not be shared) | Without it, none of section 5 can be implemented |
| 3 | **Developer account holder**: are the Apple and Google accounts held by the club or the Association? What is the store display name? | Determines items 1 and 4, and the data controller in the privacy policy |
| 4 | **Wallet certificates**: Apple Pass Type ID and Google Wallet Issuer (dependent on item 3) | Without them the wallet feature in 3.6 cannot be built |
| 5 | **Do first advertisers exist?** | No advertisers means no advertising. The fallback-creative mechanism is already specified so the feature can launch regardless |
| 6 | **Partner and sponsor assets**: no names, logos, or partnership descriptions have been supplied | Section 3.12 cannot be signed off. **Placeholder logos must not be used** |
| 7 | **Geographic coordinates for partner stores and venues**: current data holds addresses only | Distance sorting in 3.8 cannot be built. A "locate from address" helper with human confirmation is recommended |

**Commercial decisions**

| # | Item |
|---|---|
| 8 | **Advertising pricing**: CPM, flight buyout, or bundled into sponsorship packages? This determines whether `AdCampaign` needs an amount field; priced separately, it also creates an invoicing and tax workflow |
| 9 | **Expected daily active devices**: needed to size the event volume and reporting cost (the assumptions in 7.6 must be replaced with real estimates) |
| 10 | **The definitive slot list and rate card** |
| 11 | **Membership fee amounts and plan design** (a website open item): affects the amounts shown in section 5 and the argument in item 1 |

**Regulatory and personal data**

| # | Item |
|---|---|
| 12 | **App Store age rating**: with U12/U14/U15 content and photographs of minors, does the app fall into a children's category? If so, advertising restrictions tighten considerably (this specification is already aligned) |
| 13 | **The three additions to the member terms** (push token, booking attribution, location purpose) require legal sign-off |
| 14 | **Whether guardian consent for photographs of minors covers "store screenshots"**, a newly public exposure surface |
| 15 | **Whether player likeness consent covers the app and the store listing** |
| 16 | **Whether Sign in with Apple is required** alongside LINE sign-in under current rules |
| 17 | **The wording of the location permission purpose string**, which must state the purpose and scope specifically |

**Technical prerequisites (not technology selection, but client decisions or provisions)**

| # | Item |
|---|---|
| 18 | **Push services**: APNs certificates (dependent on item 3), FCM project ownership, whether a third-party provider is used (with cross-border transfer disclosure) |
| 19 | **Deployment rights for `.well-known` on the website domain**: a hard dependency for Universal Links |
| 20 | **Whether the app's API is hosted by the website admin**: the existing premise is that the charity platform shares the website admin and database, and the app should follow the same model, but this must be confirmed |

**Existing data gaps**

| # | Item |
|---|---|
| 21 | **English fixture fields**: `opponent_en` / `venue_en` have not been supplied; the display and notification rules for round 7's `TBC` venue need confirming |
| 22 | **Member number format** (a website open item): required by the app card and the wallet pass |
| 23 | **Season start and end dates** (a website open item): affects membership status display and renewal prompt timing |
| 24 | **Manga assets and digital distribution rights** |
| 25 | **Player photographs and biographies**: all 28 players currently have empty photo and biography fields |
| 26 | **Progress on unblocking the 113 draft articles**: affects article bodies and the Phase D schedule |
