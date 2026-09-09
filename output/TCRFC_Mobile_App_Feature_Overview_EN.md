# TCRFC Taichung Rock FC — Mobile App Feature Overview (Client Edition)

> **Document version**: v1.4
> **Date**: 2026-09-09
> **Brand promise**: LOCAL ROOTS. GLOBAL PATHWAYS.

> **Who this document is for**
> This is a **feature overview**, not a technical document. It answers one question: **which features can go into the mobile app, and what each one is worth to fans and to the club**.
> Colleagues who need the data structures, integrations, and development rules should read the *TCRFC Mobile App Functional Specification* instead — that is the document development follows, and it prevails wherever the two differ.

---

## Contents

1. [What this app is for](#1-what-this-app-is-for)
2. [What you see when you open it](#2-what-you-see-when-you-open-it)
3. [The feature list](#3-the-feature-list)
4. [Who can see what](#4-who-can-see-what)
5. [What the club needs to decide or supply](#5-what-the-club-needs-to-decide-or-supply)

---

## 1. What this app is for

### 1.1 The position, in one line

**This app is not a shrunken website, and it is not a web page wrapped in a shell.**

Squeezing the website into an app would produce a website that is slower than the website and costs a second maintenance effort. So every feature has to clear a bar — **to go into the app, a feature must meet at least one of these two tests**:

| Test | In plain terms | Examples |
|---|---|---|
| **Only a phone can do it** | It needs location, offline storage, push, the calendar, the camera — things only a phone has | Partner stores near you, a membership card that works with no signal, a reminder before kick-off |
| **The content is already in the website's admin** | It reuses content the club already maintains, so nothing new has to be produced before launch | Fixtures, news, squad lists, program sessions |

Anything that meets neither test stays out of the app.

### 1.2 Three assumptions that shape the whole app

| Assumption | What follows from it |
|---|---|
| **The app and the website share one admin** | Content is maintained once. Change a fixture, publish news, or adjust a store offer in the website admin, and the app follows immediately. **There is no second admin to maintain** |
| **Members will use it at the ground and at store counters** | Signal is routinely absent in those places. So the **membership card, downloaded fixtures, and articles already read must work with no connection** |
| **Advertising is sold by the club, not by an ad network** | Slots are sold to local businesses and performance is measured by the club's own system. No user tracking, no targeting by browsing history — the numbers are honest, and they can be handed straight to the advertiser |

### 1.3 How it relates to the website and the charity platform

| | What it carries | Who collects the money |
|---|---|---|
| **Official website** | Public content and search presence, the online shop, the full admin | The club |
| **Charity donation platform** | Scan-to-donate at partner stores, invoices and receipts, revenue-share settlement | **Taiwan Football Strategic Development Association** |
| **Mobile app** (this document) | The member's pocket interface, fixture information, nearby stores, owned advertising | The club |

All three share one admin and one set of data, but **the legal entities and the payment accounts are entirely separate**: the app collects the club's membership fees, the charity platform collects the Association's donations. They share no merchant account and are never conflated.

---

## 2. What you see when you open it

Five fixed tabs along the bottom; nothing is more than three taps deep.

| Tab | What is in it |
|---|---|
| **Home** | Countdown to the next match, latest news, nearby stores, advertising slots, sponsor logo wall |
| **Fixtures** | Squad tabs, fixtures/results toggle, match detail |
| **Member** | Digital membership card, membership status, my bookings, jersey registration, prize-draw eligibility |
| **News** | Category list, article, offline reading |
| **More** | Squads, partner stores, program booking, partners and sponsors, charity (outbound), shop (to the website shop), FAQ, settings |

**One important design decision**: **people who are not signed in must still be able to browse fixtures, news, squads, partner stores, and programs in full.**

The sign-in wall stands in exactly four places: **the membership card, my bookings, renewal payment, and prize-draw eligibility.**

The partner-store directory itself is public — it is the single strongest reason to join, and locking it away would close the door on conversion. The listings simply carry a label, "all members" or "paid members only", so people can see which offers require a paid membership.

---

## 3. The feature list

### 3.1 Fixtures and results

| Feature | Description |
|---|---|
| Fixtures by squad | Tabs for First Team, U15, U14, U12. Adding a U18 or U10 later means adding one squad in the admin — **no app update or resubmission** |
| Fixtures / results toggle | Fixtures before kick-off, scores afterwards |
| Filters | Competition type (league / cup / friendly), home or away |
| Match detail | Date, time, opponent, venue, round; after the match, the score, scorers and times, cards, line-up, and a link to the report |
| **Add to the phone's calendar** | One match at a time, or the whole season imported at once into the phone's native calendar |
| **Venue navigation** | One tap opens the phone's maps app and navigates to the ground |
| **Kick-off reminder** | A push notification two hours before kick-off, sent only to people subscribed to that squad, and switchable off |
| **Venue confirmed notification** | When a venue changes from "to be confirmed" to an actual ground, everyone following that squad is told automatically |
| Works offline | Fixtures and results, once downloaded, can be consulted with no connection |

**A few notes**

- The public label is always **First Team**; there is only one such squad club-wide.
- **No automatic live scores.** Fixture data is maintained by hand, so genuine "live" is not achievable — building it anyway would only break trust.
- **The app deliberately has one filter layer fewer than the website.** The website's fixtures page shows five filters at once; a phone screen cannot carry that, and forcing it produces an interface nobody uses.

### 3.2 Digital membership card

**This is the feature a paying member feels most directly, standing at a store counter.**

| Feature | Description |
|---|---|
| Card in the app | Member number, QR code, name, membership tier, expiry date |
| **Works offline** | **The card can be shown with no signal** — basements at grounds and store counters routinely have none, and this is something a web page cannot do. The card face carries a "last synced" time and warns if it has been too long |
| Updates itself | After a renewal or a tier upgrade the card face updates on its own; the member does nothing |
| Card invalidation | If the phone is lost, the member can regenerate the QR and the old one stops working immediately |
| Quick access | The card can be opened with fingerprint or face recognition |

**How a store checks the card**: staff **look at it**, or scan the QR with a phone to open a public verification page showing only the first character of the name, the member number, the tier, and whether it is valid or expired.

**No scan-to-redeem**: no counting, no store reports, no store accounts, nothing for the store to install. This is a deliberate trade-off — the moment redemption is built, every store needs an account, a report, and reconciliation, which amounts to a second system.

### 3.3 Partner stores and the nearby map

**This is where the app is worth the most relative to the website.**

A member is out and about and wants to know "is there a shop nearby that gives me a discount?" — a web page does this badly and a phone does it well.

| Feature | Description |
|---|---|
| **Sorted by distance** | Straight-line distance from the phone's location, nearest first, shown as "400 m away" |
| **Map view** | Toggle between list and map; the map marks each store with its category icon |
| **One-tap navigation** | Opens the phone's maps app and navigates there |
| **One-tap calling** | Dials the store directly |
| Category and area filters | Food and drink / sportswear / health / education / everyday services, plus area |
| Store detail | Name, photo, address, phone, opening hours, the offer, website or social links |
| Applicable tier label | "All members" and "paid members only" are clearly distinguished; the latter shows an upgrade prompt to free members |

**How we handle location permission**

1. **Location is used only while the feature is in use — never tracked in the background.**
2. The purpose is **explained before permission is requested**, the first time the feature is used — not with a permission dialog the moment the app opens.
3. If permission is refused, **the feature does not break** — it falls back to manual area filtering, and an address can be typed in instead.
4. **Coordinates are never uploaded and never stored**; the distance is calculated on the phone and discarded.

**Something to sort out first**: partner-store records currently hold an address in text only, with **no latitude and longitude**, and distance cannot be calculated without them. The admin will gain coordinate fields and a "locate from this address" helper button, but **the result must be confirmed by a person before it is saved**.

### 3.4 Member centre and membership

| Feature | Description |
|---|---|
| Member profile | Name, mobile, email, date of birth, language preference; change password, delete account |
| Membership status | Tier, member number, expiry date, current state (pending / active / expired) |
| **Renewal reminder** | A banner in the Member tab 30 days before expiry, plus a push notification |
| Jersey registration | Size, collection method (posted / collected in person), delivery details; status shown as pending / dispatched / collected |
| Benefits table | Free and paid membership benefits compared line by line, visible without signing in |
| Plan comparison | Price, season period, how many cards, how many jerseys, family plans |

**Joining and paying inside the app**: a paid membership can be completed in the app with **LINE Pay**, and **the system activates the membership automatically** once payment succeeds — no waiting for someone to confirm it by hand.

Two boundaries to state up front:

- **Payment in the app is only ever for membership fees.** Course fees stay offline (bank transfer or in person), merchandise is checked out in the website shop, and donations go through the charity platform.
- **The app handles no donation payments at all.** The charity section offers an outbound link at most, and it states clearly that the recipient is the Taiwan Football Strategic Development Association, so nobody can mistake it for a donation to the club.

### 3.5 News and stories

| Feature | Description |
|---|---|
| Browse by category | The website's existing eight categories, with none added |
| List | Cover image, headline, category, date; pull to refresh, infinite scroll |
| Article | Language switch, adjustable text size, one-tap share (what gets shared is the website URL, so people without the app can still open it) |
| **Offline reading** | Articles already opened can be read offline and are kept for 30 days |
| Category push subscriptions | Subscribe only to the categories you care about |

**News push is off by default** — pushing every article at the club's publishing pace would be intrusive, so users turn it on themselves.

### 3.6 Squads and players

| Feature | Description |
|---|---|
| Squad lists | Grouped by position (goalkeeper / defender / midfielder / forward), ordered by shirt number within each group |
| Player card | Number, name in Chinese and English, position, photograph |
| Player detail | Date of birth, height and weight, nationality, preferred foot, date joined, previous club, career, season statistics (appearances, goals, assists, cards), related news |
| Coaches and staff | Name, role, qualifications, specialism, squads covered |

**Something to sort out first**: photographs and biographies for all 28 players are still missing; this section opens once the assets arrive, and **the list will not use placeholder images** — a text card with number and name only is better than a fake photograph. Whether the player image-rights consents cover the app also needs confirming.

### 3.7 Programs and camp bookings

**One thing to clear up first**: the website already takes bookings online, and the admin's booking management already exists in full. **Booking in the app is not a new feature; it is a phone-shaped interface onto the existing mechanism.**

Only two things are genuinely new, and both are a member acting on their own data:

| New | Description |
|---|---|
| **Form pre-filling** | For a signed-in member, name, mobile, and email are filled in automatically and remain editable. Typing on a phone is painful, so this is worth an order of magnitude more in the app than on the web |
| **My bookings** | A list of the member's own bookings: program, session, time, place, payment status |

Rules carried over from the website, unchanged:

- Booking status flow: pending → confirmed → paid → completed / cancelled / waitlisted
- Session status: open / full / waitlist / closed, with the front end showing "book now / join waitlist / closed" automatically
- **Payment stays offline** (marked in the admin after a transfer or in-person payment)
- **Non-members can still book** — joining is never forced

**Parent–student linking is explicitly not built** (not on the website, not in the app, not in the admin). It would create a lasting link between an adult and a minor, which is a step up in sensitivity; the family membership was deliberately designed to avoid it. Student details continue to live in the booking record — there is simply no parent-to-student link.

### 3.8 Prize-draw information for paid members (display only, no actions)

Paid members (Fan Club members) hold prize-draw eligibility. The app's role here is **only to let a member know whether they are eligible**; it performs no draw actions at all.

**The app shows four things and no more**:

| # | What is shown |
|---|---|
| 1 | **Your eligibility**: "your membership is valid at the qualifying date and you will be entered automatically", or "your membership does not cover the qualifying date" |
| 2 | The rules, prizes and quantities, and the notes (in Chinese and English) |
| 3 | The draw date and where it takes place (in person or on a livestream) |
| 4 | Once results are published, a link to that news article |

**Deliberately not built** (identical to the website's rules): no serial numbers displayed, no lookup screen, no winners list inside the app, no online draw animation, no live "how many people qualify" counter, and **push is never used for individual winner notifications**.

Eligibility is **automatic and requires nothing from the member**: a membership valid at the qualifying date is entered, one entry per person, and **it cannot be increased by spending, checking in, sharing, or attending**. The draw itself happens physically, in person or on a livestream, and the result is announced only through a news article.

### 3.9 Partners and sponsors

| Section | Content |
|---|---|
| Our Partners | A logo wall grouped by type (strategic / international / training / education / brand), with partnership details, period, and website link |
| Our Sponsors | Grouped by level (principal / official / supporting), linking to sponsorship stories |
| Enquiries | Points to the partnership and sponsorship enquiry form on the website |

**A sponsor logo wall and an advertising slot are two different things** — this is the easiest point to confuse:

| | Sponsor logo wall | Advertising slot |
|---|---|---|
| Position | The fixed partners section and the lower part of Home | A designated slot, rotating between advertisers |
| **Impressions counted** | **No** | Yes |
| **Appears in advertising reports** | **No** | Yes |
| Labelled "advertising" | No | **Required** |

If a sponsor's contract happens to include "N flights in the app's advertising slots", the correct approach is to create a separate advertiser record for them, link it back to the existing sponsor record, and then open an advertising flight — that way performance lands in the advertising report. **Page views on the logo wall are never counted as performance**; that is a number nobody can reconcile.

**An asset red line**: no partner or sponsor names and logos have been received yet. Until they arrive, **neither the app's screens nor the store screenshots may carry placeholder logos** — doing so would publicly assert a commercial relationship that does not exist. Gaps are handled with a "partnership enquiries welcome" placeholder.

### 3.10 Advertising slots (the part that earns money)

The app carries **advertising slots sold directly by the club** to local businesses, with performance measured by the club's own system.

**Slot positions**: top of Home, mid-Home, the fixtures list, the news list.

| For fans | For the club and the advertiser |
|---|---|
| Every slot is **clearly labelled "advertising" or "sponsored content"** and never disguised as editorial | The admin manages advertisers, flights, rotation weights, and daily impression caps |
| **No splash advertising** (the kind that blocks the screen the moment the app opens), no interstitials, no video pre-rolls, no floating advertising you cannot dismiss | Reports cover impressions, clicks, click-through rate, and daily trend, exportable as CSV and PDF for the advertiser |
| Slot height is fixed, so **the layout never jumps as an advert loads** | Impressions follow the accepted industry standard (at least half the advert visible continuously for one second counts as one) |
| When no flight is running, the slot shows the club's own content — **a slot is never blank** | "Guaranteed impressions" flights are supported, with the guarantee spread evenly across the flight rather than burnt through in two days |

**Three decisions that are actually a selling point to advertisers**

| Decision | What it buys |
|---|---|
| **No third-party ad networks; direct sales only** | No irritating "allow tracking?" prompt; the app can honestly declare that it does not track users |
| **No advertising identifiers** | Nothing is collected that could track an individual across apps |
| **No behavioural targeting** | Adverts are chosen by slot, language, and platform only — never by an individual's browsing history |

In one line, for advertisers: **"our numbers are honest figures measured directly by our own system, not estimates passed along by a network."**

**Two restrictions**

1. **No advertising on screens children will see**: no slots on academy or program screens. Their audience includes minors and they sit alongside photographs of minors; commercial messaging does not belong there.
2. **Categories never accepted**: tobacco and alcohol, gambling, adult content, medical efficacy claims, and financial investment solicitation. Creatives may also not use the club's marks or imply official endorsement (unless the advertiser genuinely is a sponsor and the contract says so). Any single flight can be taken down immediately if a breach is found.

**No advertiser self-service admin in the first release** — advertisers do not get an account to log in and read reports; the sales team exports and sends them.

### 3.11 Push notifications and the notification centre

| Type | When it is sent | Default |
|---|---|---|
| Kick-off reminder | Two hours before kick-off | On |
| Venue confirmed | When a venue changes from "to be confirmed" to an actual ground | On |
| Fixture change | Date, time, or venue changed, or the match postponed | On |
| Result published | When the score is entered and published in the admin | On |
| News published | When an article is published, by subscribed category | **Off** |
| Membership expiry reminder | 30 days and 7 days before expiry | On |
| Membership activated | When payment activates the membership | On |
| Jersey status change | When the status becomes "dispatched" | On |
| Booking status change | When a booking becomes "confirmed" or "paid" | On |
| General announcement | Sent manually from the admin | On |

**The in-app notification centre**: notifications received stay in an inbox inside the app, kept for 90 days and readable offline.

**How much control the user has**

- Category switches (fixtures / results / news / membership and jersey), each independently
- Subscribe to the squads you follow; kick-off reminders go only to those
- A daily quiet period (suggested default 22:00–08:00)
- Everything can be switched off at once — but **the membership expiry reminder still arrives by email**, so an important message never depends on push alone

**Three boundaries**

1. **Push is segmented by membership tier, squad subscription, and language only** — never personalised by browsing history, click behaviour, location, or purchase history.
2. **Push is never used for individual winner notifications**, and the five system emails stay as they are.
3. The website **still does not build** on-site messaging or a notification centre — this notification centre exists only inside the app.

### 3.12 Settings and account

Language switch, push permission and categories, an explanation of the location permission, clear cache, manage offline content, privacy policy, terms of service, a "why am I seeing advertising?" page, version information and update check, contact us, sign out, and **delete account**.

**Account deletion must be completable inside the app** (Apple and Google both require this); "please write to us" is not acceptable. On deletion the member's personal data is erased, and a locked prize-draw roster retains only the member number and a masked name.

### 3.13 Other app-wide features

| Feature | How the app does it |
|---|---|
| Chinese / English switch | Follows the phone's language on first launch; follows the account setting once signed in; switching keeps you on the same screen |
| Search | Covers news, players, partner stores, and the FAQ |
| Share | Uses the phone's own share sheet, and **what gets shared is the website URL**, so recipients without the app can still open it |
| Social links | Instagram, Facebook, YouTube |
| FAQ | Reuses the website's content, embedded by topic on the program and academy screens |
| Opening the app straight from a link | Someone with the app installed who taps a website link lands on the matching app screen; someone without it simply gets the web page, and **never an error** |

---

## 4. Who can see what

| Feature | Signed out | Free member | Paid member (Fan Club) |
|---|---|---|---|
| Fixtures, results, match detail | ✅ | ✅ | ✅ |
| Kick-off reminders and fixture push | ✅ | ✅ | ✅ |
| Add to calendar, venue navigation | ✅ | ✅ | ✅ |
| News and stories, offline reading | ✅ | ✅ | ✅ |
| Squads and players | ✅ | ✅ | ✅ |
| Partner store directory, nearby map, navigation and calling | ✅ | ✅ | ✅ |
| **Redeeming** a store offer | ✗ | Some (those labelled "all members") | ✅ All |
| Browsing and booking programs | ✅ | ✅ | ✅ |
| Form pre-filling, my bookings | ✗ | ✅ | ✅ |
| Partners and sponsors | ✅ | ✅ | ✅ |
| Membership plans and the benefits table | ✅ | ✅ | ✅ |
| **Digital membership card** (works offline) | ✗ | ✅ | ✅ |
| Jersey registration | ✗ | ✗ | ✅ |
| **Prize-draw eligibility** | ✗ | ✗ | ✅ |
| Notification centre and preferences | ✅ | ✅ | ✅ |

**The row worth noticing**: the partner-store **directory is entirely public** and complete without signing in — because it is the most effective reason anyone has to join. The real threshold is the act of showing a card in the shop.

---

## 5. What the club needs to decide or supply

Every item below directly affects whether a feature can be built, and when. They fall into four groups.

### 5.1 Blocking — nothing can be built without these

| # | Item | What happens without it |
|---|---|---|
| 1 | **How the app stores treat a paid membership**: may joining be paid via LINE Pay rather than Apple's or Google's in-app purchase? (The benefits are predominantly physical — jersey, store discounts, in-person events — which gives grounds to argue it, but the stores make the judgement) | **The single largest launch risk.** A fallback has to be prepared in parallel |
| 2 | **The club's own LINE Pay merchant account** (the Association's cannot be shared) | Joining and paying inside the app cannot be built at all |
| 3 | **Whose name the Apple and Google developer accounts are in**: the club's or the Association's? What is the display name in the stores? | Also determines item 1, who applies for the push certificates, and who is named as data controller in the privacy policy |
| 4 | **Geographic coordinates for partner stores and grounds** (only text addresses exist today) | Sorting nearby stores by distance cannot be built |
| 5 | **Partner and sponsor names and logos** (none received so far) | That section cannot be signed off, and **placeholder logos will not be used** |
| 6 | **Whether the first advertisers are in place** | No advertisers means nothing to show. The slots themselves can still launch — an empty flight shows the club's own content |

### 5.2 Commercial decisions

| # | Item |
|---|---|
| 7 | **How advertising is sold and priced**: by impressions, by buying out a whole flight, or bundled into a sponsorship package? Pricing it separately brings an invoicing and tax process with it |
| 8 | **The actual slot list and its prices** |
| 9 | **Roughly how many people will use the app each day**: this drives the volume of performance data and the cost of reporting |
| 10 | **Membership fees and plan design** (the website is waiting on this too): it affects how prices appear in the app, and the argument in item 1 |

### 5.3 Compliance and personal data

| # | Item |
|---|---|
| 11 | **App Store age rating**: the app contains U12 / U14 / U15 content and photographs of minors — will it be classed as a children's app? If so, advertising restrictions tighten considerably (the specification is already aligned to them, but this needs confirming) |
| 12 | **Three additions to the membership terms** (push, booking attribution, use of location), to be approved by legal |
| 13 | **Guardian consent for photographs of minors** — does it cover "app store screenshots", a new public surface? |
| 14 | **Player image-rights consents** — do they cover the app and the store pages? |
| 15 | **The wording of the location permission notice**, stating its purpose and scope specifically |

### 5.4 Content assets still awaited

| # | Item | What it blocks |
|---|---|---|
| 16 | Player photographs and biographies (all 28 currently empty) | The player detail screen |
| 17 | Progress unlocking the draft articles | Article text and offline reading |
| 18 | English fields for fixtures (opponent and venue names in English) | The English fixture list |
| 19 | Member number format, season start and end dates (the website is waiting too) | The card face, and when renewal reminders fire |

---

> **The last word**
> This app is planned against one test: **do what the website cannot.**
> Pulling out a membership card at a ground with no signal, finding a shop nearby that gives you a discount while you are out, a phone buzzing two hours before kick-off — a website will never do those three things, and they happen to be the three moments a paid membership feels most worth having.
