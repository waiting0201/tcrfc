# TCRFC Taichung Rock FC — Charity Donation Platform Functional Specification

> **Document version**: v1.2
> **Date**: 2026-09-03 (v1.2 revision: 2026-09-04)
> **Brand promise**: LOCAL ROOTS. GLOBAL PATHWAYS.

> **What this document covers**
> 1. This document specifies the **Charity Donation Platform**: a donation site on its own domain, entered by scanning a QR code in physical venues, which **shares the official website's admin and database**.
> 2. It is **complementary to, not overlapping with**, the [Website Functional Specification](TCRFC_Website_Functional_Specification_EN.md) (v2.1 onwards). The main site no longer handles any donation payments; section 11 "Charity & Impact" retains only the **editorial** content — commitment, programmes, impact stories and impact metrics — and routes "fan donation" to this platform.
> 3. Main specification v2.1 revises three premises accordingly: **donations no longer run through Shopify items or bank transfer**, **volunteer signup is out of scope**, and **"no payment gateway" now applies to the main site only**.
> 4. This is a functional specification and **does not cover framework, CMS, or deployment decisions** (per the standing premise in main specification §1.3).

> **v1.2 revision summary — fundraising progress removed**
> 1. **The public site does not show fundraising progress**: the project card wall (3.1) and the project page (3.2) **drop** the progress bar, the raised-to-date and target amounts, and the donation count. Donors see what the project does and where the money goes, not a running total.
> 2. **Removed from the admin too**: the "fundraising target" field and the "progress is public" display control in N2 are **both gone**, and `DonationProject` no longer carries a target amount.
> 3. **The numbers are still available internally**: donation counts and totals per project come from **N6 donation reporting** (§7); they are simply not published. What was removed is the **public display**, not the ability to measure.
> 4. Open item 14 in section 10, "whether fundraising progress is public", is **decided as not doing it** — removed from the open list and recorded in the decided-premises table.

> **v1.1 revision summary — no SEO / GEO optimisation**
> 1. **This platform does no SEO or GEO optimisation**: no keyword research or content programme, no structured data (`Schema.org` markup), no sitemap submission or indexing monitoring, no AI-search (GEO) optimisation, and **no search-ranking acceptance criteria of any kind**. Section 7 of the main specification (SEO / GEO) **does not apply to this platform**.
> 2. **Pages remain indexable**: landing and project pages are not marked `noindex`, all three `hreflang` values are still required (so the two language versions are not treated as duplicates), and the `noindex` rules for `/result/` and the donor roll are unchanged.
> 3. This revision changes only the **expected outcomes and the scope of work**. The page list, URL structure, bilingual field rules and every other feature are **unchanged**.

---

## Table of contents

1. [Objectives and scope](#1-objectives-and-scope)
2. [Site architecture and URLs](#2-site-architecture-and-urls)
3. [Public site functionality](#3-public-site-functionality)
4. [Payment integration (LINE Pay)](#4-payment-integration-line-pay)
5. [E-invoices and donation receipts](#5-e-invoices-and-donation-receipts)
6. [Admin functionality](#6-admin-functionality)
7. [Donation reporting](#7-donation-reporting)
8. [Revenue share and accounting rules](#8-revenue-share-and-accounting-rules)
9. [Data model and content types](#9-data-model-and-content-types)
10. [Roles and permissions](#10-roles-and-permissions)
11. [Personal data, security and regulation](#11-personal-data-security-and-regulation)
12. [Delivery phases](#12-delivery-phases)
13. [Open items](#13-open-items)

---

## 1. Objectives and scope

### 1.1 Positioning

A small-donation platform entered through **QR codes in physical venues**. Partner restaurants, drink shops and similar businesses display their own QR code in store; a customer scans it, lands on this platform, sees **that venue's name**, picks a cause to support, reads the project description, and completes a LINE Pay donation at the foot of the page. An e-invoice or donation receipt is issued automatically.

Three premises shape the whole site:

| Premise | Design consequence |
|---|---|
| **Scanning is 100% mobile** | Mobile-first is not an option but the only consideration; desktop needs to be readable, not optimised |
| **Customers donate in spare moments — waiting for food, paying the bill** | The path from scan to completed payment must be as short as possible; **no registration, no login** |
| **Venues are referrers, not system users** | Venues need no account, no login, nothing installed; their only action is displaying the QR code |

### 1.2 Relationship to the main website

| Item | Approach |
|---|---|
| **Domain** | **Its own domain** (name TBC, see §13), separate from `www.tcrfc.tw` |
| **Public site** | A **fully separate project**; it does not share the main site's 73-page build pipeline |
| **Admin** | **Shares the official website admin**, via a new `N. Charity Donations` module (see §6) |
| **Database** | **Shares the same database**, so it can relate to existing types such as `CharityProgram`, `Charity` and `Member` |
| **Visual design** | Uses TCRFC brand assets and design tokens (magenta `#E0218A` / brand black `#231916`), but its own layout and navigation |
| **The main site's role** | Section 11 becomes editorial and referral only: 11.1 commitment, 11.2 programmes, 11.3 impact stories and 11.4 impact metrics are unchanged; the CTA narrows from three routes to two (corporate partnership / fan donation), and **fan donation always links out to this platform** |

> **Why a separate domain rather than a section of the main site**: the landing page must be minimal with the shortest possible conversion path, and should not carry the main site's mega menu and 13-unit navigation. The platform also handles payments and invoicing, so a separate security boundary is simpler.

### 1.3 Scope

**In scope**:

- Scan landing page (showing the venue name), project list and detail pages, donation form, LINE Pay payment, result page
- Issuing e-invoices and donation receipts (one of the two, set per project)
- Admin: venue management with QR generation, project management, donation records, rebate settlement, invoice management, donation reporting, site settings
- Revenue-share percentages for venues and projects, and their settlement
- Traditional Chinese (default) and English

**Out of scope**:

- **Venue accounts and a venue login area** — venues do not register, do not log in, do not see the admin. Rebate statements are supplied by the club
- **Scan-based redemption, redemption counts, live venue dashboards** (consistent with the standing premise for 8.4 Partner Perks on the main site)
- **Recurring donations** (monthly auto-debit) — deferred for later assessment
- **Posting physical receipts**, printing paper invoices
- **Regulatory filing for public fundraising** (the system does not file on the club's behalf; it provides reports for manual filing)
- **Donor accounts and a donation history area** — donors do not log in; records are emailed
- **Technology selection** — framework, CMS, hosting and deployment are all outside this document

### 1.4 Terminology

Two pairs of concepts in this project are easy to confuse and **must be kept apart before implementation**:

| This platform's type | Existing main-site type | Difference |
|---|---|---|
| **`DonationStore` donation partner venue** | **`PartnerStore` perks partner** (main site 8.4) | The former is a **referrer**: it displays a QR code, drives donations, and **earns a revenue share**. The latter provides **member discounts**: members show their digital card for a deal, with **no payment flow and no revenue share**.<br>**They are separate tables. Even the same physical shop is recorded twice and the records are not shared.** |
| **`DonationProject` donation project** | **`CharityProgram` charity programme** (main site 11.2) | The former is what people **donate to**: amount options, revenue-share settings, invoice mode. The latter is **charitable work already delivered**: recipient organisation and what was donated.<br>A project **may link to** a programme, to show donors what the money eventually did. |

Other terms:

- **Venue share (`store_share_pct`)**: the proportion of a donation returned to the referring venue. This is real income for the venue.
- **Project share (`project_share_pct`)**: the proportion paid out to the project's designated recipient (a charity organisation or delivery partner).
- **Club retention**: what remains after both of the above.
- **Donation (`Donation`)**: the record of one donation, reusing and extending the existing `Donation` type from the main specification.

---

## 2. Site architecture and URLs

### 2.1 Pages

Languages are separated by `/zh/` and `/en/`, with the structure reserving room for a third language (consistent with the main site).

| Page | URL | Notes |
|---|---|---|
| **Scan landing page** | `/{lang}/s/<store_slug>` | The QR code target. **The venue name appears at the top**, followed by project cards |
| General entry | `/{lang}/` | Direct donation with no venue attribution (e.g. arriving from main site section 11) |
| **Project detail** | `/{lang}/p/<project_slug>` | Full description; **the donation form sits at the very bottom**. `?s=<store_slug>` carries venue attribution |
| Result | `/{lang}/result/<order_no>` | Success / failure / processing |
| Donor roll | `/{lang}/donors/` | Named donors (names only, no amounts). Can be disabled site-wide in the admin |
| Impact | `/{lang}/impact/` | Summary of linked charity programmes, routing back to main site 11.3 |
| Privacy policy | `/{lang}/privacy/` | A separate domain needs its own, covering donor data and invoice details |
| Donation terms | `/{lang}/terms/` | Refund rules, invoice rules, how funds are used |

### 2.2 How venue attribution is carried

Whether a venue earns its share depends on whether "which venue this scan came from" survives all the way to order creation. The rules:

1. On landing at `/{lang}/s/<store_slug>`, write `store_slug` to a **short-lived cookie** (24 hours suggested).
2. When moving from the landing page to a project detail page, also pass `?s=<store_slug>` in the URL.
3. **Precedence** when creating a donation: `URL parameter` > `cookie` > `no venue`.
4. If no venue can be resolved, the donation **still proceeds**: `store_id` is null, it is classified as unattributed, the venue share is 0, and the project share is calculated as normal.
5. If `store_slug` resolves to no valid venue (partnership ended, record deleted), treat it as unattributed — **never fail the donation flow**.

> `store_slug` is **only an attribution marker**. The URL must **never** carry an amount, a share percentage, or anything else that affects accounting.

### 2.3 QR code specification

| Item | Requirement |
|---|---|
| Target URL | `https://<charity domain>/zh/s/<store_slug>`. Always the Chinese version; visitors can switch language on site |
| `store_slug` | Generated in the admin, **not derivable from a venue ID** (so other venues' URLs cannot be guessed), alphanumeric with hyphens, globally unique |
| Error correction | **Level Q (25%)** suggested, since printed material gets soiled and partly covered |
| Minimum size | The code area on printed output should be no smaller than **3×3 cm**, with a quiet zone of at least 4 modules |
| Output formats | **PNG** (general use), **SVG** (lossless scaling), **print-ready PDF including the venue name** |
| Print template | TCRFC logo, venue name, a short call to action, and the code itself. Only the three lockups in `brand/svg/` may be used — **no custom typesetting, no SINCE or year** |
| Regeneration | The admin can reissue a venue's `store_slug`. **Existing printed QR codes stop working immediately**, so this needs an explicit warning and an audit record of who did it |

### 2.4 Languages and search engines

- `hreflang` must provide all three of `zh-Hant`, `en` and `x-default`
- Every publicly visible type (venue name, project name and description, site copy) needs **`zh` / `en` fields**; English may be empty but the field must exist
- When English is empty, fall back to Chinese and label the page as having no version in that language
- Switching language stays on the equivalent page rather than returning to the home page
- Landing and project pages **remain indexable**; `/result/` pages must be `noindex`

> **This platform does no SEO / GEO optimisation.**
> Traffic comes from **QR codes scanned in physical venues** and from unit 11 of the main site; search visibility is not a success metric here.
> So there is **no** keyword research or content programme, **no** structured data (`Schema.org` markup), **no** sitemap submission or
> indexing monitoring, **no** AI-search (GEO) optimisation, and **no** search-ranking acceptance criteria.
> What remains is basic legibility only: a correct `<title>` on every page, basic `og:title` / `og:description` / `og:image` tags
> (**sharing on LINE is a primary distribution path** — a broken card costs donations directly), the `hreflang` set above, and the `noindex` rules where required.
> Section 7 of the main specification (SEO / GEO) **does not apply to this platform**.

---

## 3. Public site functionality

### 3.1 Scan landing page `/{lang}/s/<store_slug>`

| Block | Content |
|---|---|
| Venue identity | **Venue name (required)**, venue logo or photo (optional), a line of thanks (e.g. "Thank you for doing good with Taichung Rock at ○○○"). **With no logo, degrade to a text-only venue block — never an empty frame** |
| Introduction | A short editable explanation of what the platform does and how donations are used |
| **Project list** | Card wall: cover image, project name, one-line description, and a "read more and donate" button. **No fundraising progress** |
| Trust block | Club introduction, registration details, and a link to the impact records in main site section 11 |
| Footer | Privacy policy, donation terms, contact |

- If the partnership has ended, the URL still opens but **the venue block is omitted** — it behaves as the general entry
- The page must open quickly on mobile networks; the first screen must not depend on a large image

### 3.2 Project detail page `/{lang}/p/<project_slug>`

Top to bottom:

1. **Cover and title**: project name and a one-line appeal
2. **Project description**: block editor layout supporting rich text, images, pull quotes and lists
3. **Use of funds**: an explicit statement of where the money goes
4. **Linked charity programme** (optional): links to the corresponding `CharityProgram` in main site 11.2, so donors can see past results
5. **Donation form** (see 3.3) — **at the very bottom of the page, as required**
6. **FAQ**: invoices, refunds, use of funds

> **No fundraising progress on the public site**: no progress bar, no raised-to-date, no target, no donation count. Per-project totals live in N6 donation reporting (§7) and are not shown to donors.

> When the visitor arrives with venue attribution, the page should state "you are donating via ○○○" — clearly, but without overshadowing the project itself.

### 3.3 Donation form

| Field | Rule |
|---|---|
| **Amount** | Preset amount chips, configured per project in the admin (e.g. NT$100 / 300 / 500 / 1,000), plus a free-entry "other amount" |
| Amount limits | Per-donation **minimum** and **maximum** are configurable; values outside the range are flagged inline |
| **Name** | Required. Used for the invoice or receipt and for thanks |
| **Email** | Required. Used for the thank-you message and invoice notification. **Validated inline** |
| Named / anonymous | Single choice. Named donors appear on the donor roll (**name only, never the amount**) |
| **Invoice fields** | **Switch dynamically** on the project's `invoice_mode`, see §5.2 |
| Consent | Required checkbox, linking to this site's privacy policy, stating the purpose and retention period |
| Anti-abuse | Turnstile or equivalent; rate-limit bursts of orders from one IP |
| Submit | "Donate NT$ ○○○ with LINE Pay" — **the button shows the actual amount**, to prevent mis-taps |

- Donors **do not register and do not log in**
- Form state must be preserved before redirecting to LINE Pay, so that **returning after a failed payment never requires re-entry**

### 3.4 Payment flow as the donor sees it

```
Fill in form → Submit → Donation created (created)
             → Redirect to LINE Pay
             → Payment completed → return → result page (success)
             → Cancelled / timed out → return → result page (incomplete, with retry)
```

- If there is a delay between returning and final confirmation, the result page shows a **"processing"** state and polls automatically; **the timeout limit and its message must be defined** — never an endless spinner
- The processing message must not read as a failure, or donors will pay twice

### 3.5 Result page and system emails

**Result page** (`/{lang}/result/<order_no>`):

- Success: thanks, order number, amount, project, the status of the invoice or receipt, share buttons, a way back
- Incomplete: likely reasons, a **retry button** (which reuses the original donation rather than creating a new one), contact details
- The page must be `noindex` and **must not display full personal data** (email masked)

**Four system emails**:

| # | Email | Trigger |
|---|---|---|
| 1 | Donation thank-you | Immediately on successful payment; includes order number, amount, project and use of funds |
| 2 | E-invoice / receipt notification | Once the invoice is issued; includes the number and a link to view it |
| 3 | Issuing failure alert (to the club) | Invoice issuing failed; admin staff need to issue it manually |
| 4 | Refund notification | Sent to the donor after the admin completes a refund |

Every send is written to `EmailLog` (the existing main-site type).

### 3.6 Donor roll `/{lang}/donors/`

- Lists only donors who chose to be named — **no amounts, no email addresses, no venue**
- Filterable by project and period
- Can be disabled site-wide, and individual entries can be hidden

### 3.7 Accessibility and performance

- Scanning is 100% mobile, so mobile-first; touch targets no smaller than 44×44 px
- Form fields need labels and the correct `inputmode` (numeric keypad for amounts, email keypad for email)
- Contrast meets WCAG AA; small text uses the safe magenta `#D61E83`
- Every image carries `alt` plus `width` and `height`

---

## 4. Payment integration (LINE Pay)

> **This is a scope change relative to the main specification.** The v2.0 premise "the site carries no payment gateway" is **narrowed to the main site only**. This platform **integrates the LINE Pay Online API properly**, so payment results, invoice issuing and reporting are all automatic. The already-decided table in main specification v2.1 has been amended to match.

### 4.1 Donation state machine

| State | Meaning | Can move to |
|---|---|---|
| `created` | Created, not yet sent to payment | `pending`, `expired` |
| `pending` | Sent to LINE Pay, awaiting the result | `paid`, `failed`, `expired` |
| `paid` | **Paid**; invoice can be issued, counts towards reports and revenue share | `refunded` |
| `failed` | Payment failed or was cancelled | `pending` (retry) |
| `expired` | Timed out | `pending` (retry) |
| `refunded` | Refunded | — |

- **Only `paid` counts towards reporting and revenue share**; `created` and `pending` must never appear in donation totals
- Each transition records its time and what caused it (donor / payment callback / admin user)

### 4.2 Two-step flow and idempotency

LINE Pay uses a two-step Request (create) / Confirm (capture) flow:

1. Create the donation and obtain an internal `order_no`
2. Call Request to obtain the payment URL, recording the gateway transaction ID
3. Redirect the donor to pay
4. On return, call Confirm to capture the payment
5. On success the donation moves to `paid`, triggering invoice issuing and the thank-you email

**Idempotency is a hard requirement**: repeating Confirm for the same `order_no` must not double-count the payment, issue a second invoice, or send a second thank-you. Implement with a lock on the order number or a state check.

### 4.3 Exception handling

| Situation | Handling |
|---|---|
| Donor abandons midway | The donation stays `pending` and moves to `expired` after the timeout |
| Timeout | Anything not completed within the configured window (30 minutes suggested) becomes `expired` |
| **Duplicate payment** | Only one successful capture per donation; a retry **reuses the original order** rather than creating a new one |
| **Captured but Confirm failed** | The most serious case. The donation must be flagged for **manual handling**, placed in the admin's exception queue with an active alert — **never silently discarded** |
| Gateway outage | Show a clear error and contact details; never leave an ambiguous half-finished state |

### 4.4 Refunds

- Initiated in the admin (see §6 N3), requiring a reason and the responsible staff member
- A refund cascades to three things: **voiding or crediting the invoice** (§5.4), **reversing the rebate** (§8.5), and **sending the refund notification**
- Partial refunds are out of scope; full refunds only

### 4.5 Reconciliation

- Daily comparison of `paid` donations against the LINE Pay transaction list
- Discrepancies (present here but not at the gateway, present at the gateway but not here, amount mismatch) are listed for manual handling
- Reconciliation results are retained for audit

---

## 5. E-invoices and donation receipts

### 5.1 One of two, set per project

Each project carries an `invoice_mode` that determines which document all its donations produce:

| Mode | Meaning | Suits |
|---|---|---|
| **`b2c_invoice` e-invoice** | A cloud B2C invoice; treated as sales revenue for tax purposes | General fundraising projects |
| **`donation_receipt` donation receipt** | A donation receipt the donor can use for itemised deduction | Projects run under an eligible recipient status |

> **Precondition**: whether receipts can be issued at all depends on the club's legal status and recipient eligibility. This mode must not be enabled until the client and their accountant confirm it (see §11 and §13).

### 5.2 Fields switch on the mode

**For `b2c_invoice`**:

| Field | Rule |
|---|---|
| Invoice type | One of: **mobile barcode carrier**, **donate the invoice**, **company tax ID** |
| Mobile barcode | Required for the carrier option; `/` plus 7 alphanumerics, format-validated |
| Donation code | Required when donating the invoice; a common default can be configured in the admin |
| Tax ID | Required for the company option; 8 digits with **checksum validation** |
| Invoice title | Required for the company option |

**For `donation_receipt`**:

| Field | Rule |
|---|---|
| Receipt name | Pre-filled from the donor's name, editable |
| National ID | **Optional**, for itemised deduction filing. **Must be stored encrypted** and masked by default in the admin |
| Postal address | Optional. No physical posting in this scope; used for receipt content and follow-up |
| Annual summary | The donor can choose a per-donation receipt or an annual consolidated one issued at year end |

### 5.3 Timing and failure handling

- **Timing**: issued **automatically** once the donation reaches `paid`, with no human step
- **Retry**: automatic retries with backoff; persistent failure marks the record `failed` and alerts the admin
- **Manual issuing**: the admin can retrigger a failed record, or record a number issued externally
- Statuses: `pending` / `issued` / `failed` / `voided` / `allowance`

### 5.4 Voiding and credit notes

- Triggered by refunds: void within the same period, issue a credit note across periods
- Both record a reason and the responsible staff member
- An already-voided invoice cannot be voided again

### 5.5 Integrating the invoicing provider

- The provider is **not specified in this document** and remains an open item
- Requirement at specification level: the integration must be abstracted behind a **single interface** (issue / void / credit / query). Changing provider then changes only that implementation, leaving donations and reporting untouched
- Key custody requirements are in §11

---

## 6. Admin functionality

Delivered inside the **existing official website admin** as one new top-level module.

> **The module letter is `N`**: `D` is avoided because the course modules D1–D4 were renamed P1–P4, and `M` is too easily confused with the Member module.

```
N. Charity Donation Platform
├── N1 Venues and QR codes
├── N2 Donation projects
├── N3 Donation records
├── N4 Rebate settlement
├── N5 Invoices and receipts
├── N6 Donation reporting
└── N7 Site settings
```

### 6.1 N1 Venues and QR codes

| Feature | Notes |
|---|---|
| Venue fields | Name (**zh / en**), logo or photo, category, address, contact and phone, partnership dates, status (active / ended) |
| **Revenue share** | `store_share_pct`, a percentage, may be 0 |
| `store_slug` | System-generated, globally unique, not derivable from an ID; can be reissued (**existing QR codes stop working immediately**, requiring confirmation and an audit record) |
| **QR generation** | One-click generate and download: **PNG / SVG / print-ready PDF with the venue name** |
| Batch operations | Export every venue's QR code as a zip; import venues from CSV |
| Overview | Per venue: cumulative donations, amount, rebate payable, linking through to N4 |

> This module is **entirely separate from K4 Partner Perks (`PartnerStore`)** in the member module: different table, different fields, different maintenance rhythm. The admin UI must name the two distinctly so staff do not confuse them.

### 6.2 N2 Donation projects

| Feature | Notes |
|---|---|
| Project fields | Name (**zh / en**), cover image, one-line description, body content (block editor), use of funds, `project_slug` |
| Amounts | Preset amount chips, per-donation minimum and maximum. **No fundraising target** |
| **Revenue share** | `project_share_pct` and the **recipient** (which may link to a `Charity` organisation) |
| **Invoice mode** | `invoice_mode`: `b2c_invoice` or `donation_receipt` |
| Links | May link to a main-site `CharityProgram`, shown publicly as delivered impact |
| Display | Publish state, ordering |
| Validation | On save, verify that **`store_share_pct` + `project_share_pct` ≤ 100%**, blocking the save if not |

### 6.3 N3 Donation records

| Feature | Notes |
|---|---|
| List | Order number, time, amount, project, **originating venue**, state, invoice state, named / anonymous |
| Filters | Period, state, project, venue, invoice state, amount band |
| Detail | The full donation, gateway transaction ID, state history, invoice details, **revenue-share snapshot** |
| **Member matching** | Emails are **softly matched** against the member list; a match is labelled "this donor is also a member" and links to the member record. **This is a label only — it creates no linkage and writes nothing to member data** |
| Actions | Refund, resend the thank-you, reissue the invoice, hide from the donor roll |
| **Exception queue** | A separate tab listing the three classes needing human attention: captured-but-unconfirmed, invoice issuing failures, reconciliation discrepancies |
| Data protection | Email and national ID are **masked by default**; full visibility requires permission, and export requires separate authorisation with an audit entry |

### 6.4 N4 Rebate settlement

| Feature | Notes |
|---|---|
| Payees | **Venues** (`store_share_pct`) and **project recipients** (`project_share_pct`) settle **separately**, as two distinct runs |
| Cycle | Settled by period (monthly suggested), with a custom range available |
| Basis | Only donations in `paid` state within the period, using the **snapshot** percentages taken when the donation was made (see §8.4) |
| Output | A printable statement plus CSV: period, count, gross donations, share rate, amount payable |
| States | `to settle` → `settled` → `paid`, each transition recording the staff member and time |
| Payment record | **Transfers happen outside the system.** The admin only records the transfer date, method and notes for reconciliation and audit |
| Refund reversal | See §8.5 |

### 6.5 N5 Invoices and receipts

- List and filter by issuing state, period, document type and project
- Actions: reissue after failure, record an externally issued number, void, issue a credit note
- Export: a detail CSV for accounting
- Every action records the staff member and reason

### 6.6 N6 Donation reporting

See §7.

### 6.7 N7 Site settings

- Site copy (home page introduction, thanks template, donation terms, privacy policy) with **`zh` / `en` fields**
- The four email templates
- Site-wide default amount limits
- Whether the donor roll is public, and its default display rules
- Environment switch (test / production) for LINE Pay and the invoicing provider; keys are never displayed in clear text

---

## 7. Donation reporting

### 7.1 Shared dimensions

Every report filters on: **period**, **venue**, **project**, **payment state**, **document type**, **amount band**.

Only `paid` donations are counted by default; a toggle includes refunded donations to show gross figures.

### 7.2 Standard reports

| Report | Content |
|---|---|
| **Overview** | Donation count, total amount, average donation, **conversion rate** (orders created → paid), trend over the period |
| **By venue** | Per venue: referred donations, amount, share of total, **share rate and rebate payable**, settled vs unsettled. Sortable to find the strongest venues |
| **By project** | Per project: count, amount, **progress against target**, share rate, **amount payable to the recipient**, average donation |
| **Invoice status** | Counts by state (issued / pending / failed / voided / credited), with failures linking straight through to N5 |
| **Line-by-line detail** | The full donation list, for export |

### 7.3 Export and audit

- Every report exports to **CSV** (UTF-8 with BOM, so Excel opens it directly)
- **Exporting detail containing personal data (name, email, national ID) requires separate authorisation**, and each export writes an audit entry: who, when, how many rows, and a stated purpose (mirroring the member-export rule in main specification §6)
- Aggregate reports without personal data (by venue, by project) are not restricted

---

## 8. Revenue share and accounting rules

### 8.1 The calculation

**The venue share and the project share are added together and paid out separately** — they are **two payments to two different parties**, not a redistribution of one:

```
Venue rebate    = donation amount × store_share_pct
Project payout  = donation amount × project_share_pct
Club retention  = donation amount − venue rebate − project payout

Constraint: store_share_pct + project_share_pct ≤ 100%
```

- The two are **settled separately in N4**, each with its own statement and its own payment status
- For unattributed donations, `store_share_pct` is treated as 0 and the project share is calculated as normal

### 8.2 Basis of calculation

- Shares are calculated on the **gross donation**, not on the net after payment fees
- **Payment fees come out of the club's retention.** If the two share rates are set so high that retention cannot cover the fees, the admin warns at save time but does not hard-block
- The final decision on fee allocation is an open item (§13)

### 8.3 Rounding

- Both share amounts are **rounded down to whole dollars**
- The rounding remainder falls to club retention
- Report totals must equal the sum of the individual rows — rounding must never make a statement fail to add up

### 8.4 Percentage changes are not retroactive

**When a donation reaches `paid`, the current `store_share_pct` and `project_share_pct` are snapshotted onto the donation record.**

- Changing a venue's or project's percentage later **affects only new donations**; history is untouched
- Settled periods are never recalculated because a setting changed
- This is a hard requirement for accounting integrity: once a statement has gone to a venue, the amount cannot move

### 8.5 Reversing refunds

| Situation | Handling |
|---|---|
| **Not yet settled** | The donation is simply excluded from settlement; no rebate arises |
| **Settled but not paid** | Deducted from that period's statement, which is reissued |
| **Settled and paid** | **Not clawed back.** It is carried as a **negative line** into the next settlement, with the reversal reason and original order number stated on the statement |

### 8.6 Settlement and transfer

1. At period end, run settlement in N4, producing the venue statement and the project statement
2. Finance checks them and executes the transfers outside the system
3. Back in N4, record the transfer date, method and notes; the state moves to `paid`
4. The system never moves money outward

---

## 9. Data model and content types

### 9.1 New types

| Type | Description | Key relations |
|---|---|---|
| `DonationStore` | **Donation partner venue**: name (zh/en), logo, category, address, contact, `store_slug`, **`store_share_pct`**, partnership dates, status | Donation, Settlement |
| `DonationProject` | **Donation project**: name (zh/en), cover, description, use of funds, `project_slug`, amount options, min/max, **`project_share_pct`**, recipient, **`invoice_mode`**, publish state and ordering | Charity, CharityProgram, Donation |
| `DonationPayment` | **Gateway transaction**: gateway transaction ID, request and confirm timestamps, amount, state, response summary | Donation |
| `DonationInvoice` | **Invoice / receipt**: type, number, issue time, carrier or tax ID or receipt details, state, void and credit records | Donation |
| `Settlement` | **Settlement run**: period, payee type (`store` / `project`), payee, count, gross donations, amount payable, state, transfer record | DonationStore, DonationProject, SettlementLine |
| `SettlementLine` | **Settlement line**: the share amount for one donation, including negative reversal lines | Settlement, Donation |

### 9.2 Extending existing types

**`Donation`** (the existing type at main specification line 1039) is extended to become this platform's donation record. Since the main site's donation channels have been retired, reusing it avoids two sources of truth:

| Field group | Content |
|---|---|
| Core | `order_no`, amount, state, created and paid timestamps |
| Relations | `project_id`, `store_id` (**nullable**), `charity_program_id` |
| Donor | Name, email, named / anonymous. **No account, no attribution to a member record** |
| **Share snapshot** | `store_share_pct_snapshot`, `project_share_pct_snapshot`, `store_amount`, `project_amount`, `club_amount` |
| Invoice | `invoice_mode`, the mode's fields, and the related `DonationInvoice` |
| Audit | State history, refund reason and responsible staff member |

**`EmailLog`** (existing): add the four email types from this platform.

### 9.3 Boundaries against existing types

| Situation | Correct approach |
|---|---|
| A venue referring donations via QR | `DonationStore`. **Not** `PartnerStore` |
| A venue offering member discounts | `PartnerStore` (main site 8.4). **Not** `DonationStore` |
| One shop doing both | **Create a record in each**, sharing nothing and with no foreign key between them |
| What people donate to | `DonationProject` |
| Charitable work already delivered | `CharityProgram` (main site 11.2). A `DonationProject` may link to it; the reverse does not hold |
| The recipient organisation | `Charity` (existing on the main site); a project's payout recipient links directly to it |
| Whether a donor is a member | **A soft email match, shown as a label only.** Nothing is written to `Member`, and no relation field is created |

### 9.4 Languages

Every publicly visible type (`DonationStore`, `DonationProject`, site copy) needs **`zh` / `en` fields**; English may be empty but the field must exist, and the structure must allow a third language later.

---

## 10. Roles and permissions

The nine roles from main specification §6 gain one more column:

| Role | Charity donations |
|---|---|
| System administrator | ✔ Full |
| Content editor | Project content and site copy (**no share settings, no donation records**) |
| Football / team management | — |
| Academy / programme management | — |
| Commercial / sponsorship | Venue management and QR generation, read-only reporting (**no refunds**) |
| PR / media | Read-only reporting (aggregate, no personal data) |
| Customer service / admin | View and handle donations, resend emails, reissue invoices (**no refunds, no share settings**) |
| Translator | `en` fields only |
| Viewer | Read-only aggregate reporting |

**Additional rules**:

- **Donor personal data (name / email / national ID / address) is restricted**: only system administrators and customer service / admin see it in full; everyone else sees it masked
- **Refunds** and **share percentage settings** require separate authorisation, and every action is written to the audit log
- Exporting detail containing personal data requires separate authorisation and a stated purpose
- Marking a settlement "paid" should be separated from executing the transfer, so no one approves their own work (the client may adjust this to their actual staffing)

---

## 11. Personal data, security and regulation

### 11.1 Personal data

- Collected: name, email, and — in receipt mode — national ID and address
- **National ID must be stored encrypted**, masked by default in the admin, and used only as far as issuing the receipt requires
- The privacy policy must state the purpose, the scope of use, and the **retention period** (the actual term is to be confirmed with the client and their legal advisers, see §13)
- The donor roll publishes names only, and only with the donor's explicit consent

### 11.2 Security

- **No card numbers or payment credentials are stored**
- LINE Pay merchant credentials and invoicing provider keys are held as secrets — never in version control, never shown in clear text in the admin
- Payment callbacks must be verified for origin and signature
- `store_slug` is an attribution marker only; **the URL must never carry an amount or a share percentage**
- Donation creation needs anti-abuse protection (Turnstile, IP rate limiting)
- Refunds, share settings and exports all leave an audit trail

### 11.3 Regulation

- **Soliciting donations from the general public may fall under the Charity Donations Act**; whether a permit is needed, and under what legal entity, directly determines whether this platform can go live
- **Eligibility to issue donation receipts** depends on the club's legal status and recipient status
- Both must be confirmed by the client with their accountant and legal advisers. **These are launch preconditions, not ordinary open items** (see §13)

---

## 12. Delivery phases

### Phase A: the donation path

Scan landing page → project list and detail → donation form → LINE Pay integration → result page → invoice issuing → thank-you email; admin N1 venues and QR generation, N2 projects, N3 donation records.

**Once this runs end to end, the platform can go live and take donations.**

### Phase B: accounting and reporting

N4 rebate settlement, N5 invoice management, N6 donation reporting, reconciliation and the exception queue.

> Phase B may follow Phase A, but **must not slip past the first settlement cycle**, or settlement has to be done by hand.

### Phase C: extensions

Donor roll, impact page, **the English version**, and the finer parts of N7 site settings.

---

## 13. Open items

### Already decided

| Item | Decision |
|---|---|
| Domain | **Its own domain**, separate from the main website |
| Admin and database | **Shares the official website admin and database**, via a new `N` module |
| Public site | A **fully separate project**; does not share the main site's 73-page build |
| Languages | **Traditional Chinese (default) and English**, with room for a third |
| SEO | **No SEO / GEO optimisation** (no keyword programme, structured data, sitemap submission or GEO). Pages **remain indexable**; `hreflang` and the required `noindex` rules still apply |
| Fundraising progress | **Not shown on the public site** (no progress bar, raised-to-date, target or donation count). Totals live in N6 donation reporting and are not published |
| Payments | **Proper LINE Pay Online API integration**, so payment results arrive automatically. The main specification's "no payment gateway" premise is **narrowed to the main site only** |
| Documents | **Both e-invoices and donation receipts**, chosen **per donation project** |
| Donor identity | **No login, no registration**; name and email only. The admin **softly matches** emails against members as a label, with **no attribution** |
| Revenue share | The venue share is a **rebate to the referring venue**; venue % and project % are **added together and paid separately**, settled as two statements |
| Rebate payment | The system calculates and produces statements and CSV; **transfers are made manually**, with the system recording status only |
| Venue side | **No venue accounts, no venue login area, no scan-based redemption** |
| The main site's role | Section 11 keeps the editorial content only (commitment / programmes / impact stories / metrics); **fan donation routes to this platform** and **volunteer signup is out of scope** |
| Technology | Not discussed here; this document defines functional requirements only |

### To be confirmed

1. **The charity site's domain name**: it determines the QR codes and the main site's outbound links, and **a printed QR code is hard to change**, so this is the most urgent item.
2. **Fundraising eligibility and legal entity**: whether the Charity Donations Act applies, whether a permit is required, and under what entity. **This is a launch precondition** — QR codes should not go to print before it is settled.
3. **Eligibility to issue donation receipts**: determines whether `donation_receipt` mode can be enabled at all; if not, every project runs on `b2c_invoice`.
4. **Invoicing provider**: affects integration effort and cost; the specification abstracts it behind one interface, so switching provider affects nothing else.
5. **LINE Pay merchant account**: application progress, fee rate, settlement cycle.
6. **The first wave of partner venues**: how many, which categories and areas — this drives the first print run and the print template.
7. **The first donation projects**: names, descriptions, and the amount chip values.
8. **The actual share percentages**: venue rate, project rate, and whether every venue gets the same rate or each is negotiated.
9. **Who absorbs payment fees**: club retention by default (§8.2) — to be confirmed.
10. **How the public site is built**: its relationship to the main `site/` scaffold, hosting, and domain arrangements.
11. **Donor data retention period**: to be confirmed alongside the form retention periods in main specification §3.10.
12. **Whether receipts need a national ID**: it brings encrypted storage and retention duties; if it is not required, we recommend not collecting it.
13. **Recurring donations**: out of scope now — should they be assessed later?

---

*The functionality in this specification can be adjusted and extended to suit actual needs, ensuring the best possible user experience and search visibility.*
