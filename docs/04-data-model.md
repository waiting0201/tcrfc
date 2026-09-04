# 04 — 資料模型與內容型別

> 來源：規劃書 §5（**行 1160–1212**）。
> **慈善捐款平台的新增型別**（`DonationStore`／`DonationProject`／`DonationPayment`／`DonationInvoice`／`Settlement`／`SettlementLine`）不在本檔，見 [`10-charity-donation-site.md`](10-charity-donation-site.md)。
> **行動 App 的新增型別**（`AdSlot`／`Advertiser`／`AdCampaign`／`AdCreative`／`AdEvent`／`AdDailyStat`／`AppDevice`／`PushTopicSubscription`／`PushMessage`／`AppRelease`）不在本檔，見 [`11-mobile-app.md`](11-mobile-app.md)。

---

## 1. 全部型別

| 型別 | 說明 | 主要關聯 |
|---|---|---|
| `Page` | 靜態頁面（含區塊）；**女足介紹頁亦屬此型別** | SEO、多語系 |
| `Article` | 新聞與故事 | Category、Tag、Player、Team、Match、Program |
| `Team` | 球隊，含隊別代號 `D1`／`U15`／`U14`／`U12`；女足暫不使用 | Player、Coach、Match、Season、CalendarEvent |
| `Player` | 球員 | Team、Article、Stats、Pathway |
| `Staff` | 教練與團隊成員 | Team、Program |
| `Match` | 賽事。**v2.5 補** `opponent_en`／`venue_en`；`competition`／`status` 升格為正式欄位 | Team、Season、Article（賽後報導） |
| `Standing` | 積分榜 | Season、Team |
| `Achievement` | 榮譽 | Team、Season |
| `Milestone` | 里程碑 | — |
| `Program` | 課程／營隊／專項 | Session、Staff、Venue、Partner |
| `Session` | 梯次／場次 | Program、Venue、Registration |
| `Registration` | 報名。**v2.5 新增 `member_id`（可為空）**——非會員仍可報名 | Session、Contact、Member |
| `Trial` | 試訓場次 | Team、Venue、Registration |
| `Partner` | 合作夥伴：名稱（中／英）、Logo（深底／淺底兩版）、類型、國家、合作內容與期間、官網連結、排序、是否於 Footer／首頁曝光 | Article、Program |
| `Sponsor` | 贊助商：名稱（中／英）、Logo（兩版）、**等級**（主贊助／官方／支持）、合約期間、贊助內容、聯絡窗口、到期提醒、排序 | SponsorPackage、Article、**Advertiser** |
| `SponsorPackage` | 贊助方案（9 種）：名稱（中／英）、內容、權益清單、適合對象、價格區間（可不公開）、排序、上下架 | Enquiry |
| `ProductShowcase` | 商品櫥窗（**僅展示 + Shopify 連結，非電商實體**） | Collection |
| `ComicEpisode` / `ComicCharacter` | 漫畫集數／角色 | Player（原型） |
| `FanEvent` | 球迷活動 | Member |
| `Enquiry` | 表單詢問（7 類表單 + 提案下載 + 捐助洽詢） | Form、Assignee |
| `Venue` | 場地。**v2.5 新增 `lat` / `lng`**（App 場地導航與課程地點） | Program、Match、Trial |
| `MediaAsset` | 媒體資產 | 全域 |
| `Faq` / `FaqCategory` | 常見問題／主題分類 | Page（嵌入位置） |
| `CharityProgram` | 慈善計畫 | Charity、Partner、Article、ImpactRecord |
| `ImpactRecord` | 慈善事蹟紀錄 | Charity、CharityProgram |
| `Charity` | 受贈公益團體 | CharityProgram、ImpactRecord |
| `Donation` | 捐款紀錄。**主檔定義在慈善捐款平台規劃書 §9**，主站不新增捐款紀錄 | CharityProgram、DonationProject |
| `ImpactMetric` | 影響力統計項目 | CharityProgram |
| `Member` | 會員帳號：層級（`registered`／`fan_club`）、會員編號、**會員卡 token**、會籍起訖、球衣尺寸與發放狀態、註冊來源、**LINE 綁定識別碼**（加密） | MembershipPlan、MembershipPayment、FanEvent、DrawRoster |
| `MembershipPlan` | 會籍方案：費用、球季、期間、`card_quota` 發卡數、`jersey_quota` 球衣件數、季中計價 | Member、MembershipPayment |
| `MembershipPayment` | 會籍付款與開通紀錄：方式、金額、日期、交易備註、經辦人、開通起訖 | Member、MembershipPlan |
| `MembershipBenefit` | 權益對照條目：分組、免費層值、付費層值、排序（3.14／8.2／升級頁共用） | MembershipPlan |
| `PartnerStore` | **特約店家**：類別、地址、電話、營業時間、地圖、優惠內容、適用層級、合作起訖。**v2.5 新增 `lat` / `lng`**（後台 K4 人工確認後儲存，供 App 附近店家距離排序） | — |
| `MemberDraw` | **球迷會員抽獎活動**：名稱／獎品與名額（中英）、**資格基準時間 `snapshot_at`**、開獎時間與場合、領獎期限、活動辦法、狀態（草稿／名單已鎖定／已抽出／已公布／已結案／作廢）、`roster_version`、`total_count`、`roster_hash`、關聯公布新聞 | Member、DrawRoster、Article |
| `DrawRoster` | **合格名單快照，一列一位合格會員**：`serial_no` 抽獎序號、會員編號、**姓名快照**、層級快照、會籍到期日快照、是否中獎、獎項、領獎方式、發放狀態、扣繳所需資料（僅達門檻時蒐集，加密遮罩）。**系統一次性寫入，鎖定後不可增刪** | MemberDraw、Member |
| `EmailLog` | 五封系統信的寄送紀錄 | Member |
| `CalendarEvent` | **行事曆事件（彙整視圖）** | Match、Team、Venue |
| `EventType` | 賽事／活動類型（圖示、色彩、顯示規則） | CalendarEvent |

---

## 2. 三條必守的結構原則

**① 所有前台可見型別都要有 `zh` / `en` 雙語欄位**
英文可以留空（fallback 繁中並標示），但**欄位必須存在**，且架構要能再加第三語系而不改程式。

**② `CalendarEvent` 用視圖或索引表實作，不要複製資料**
以 `source_type` + `source_id` 指向 `Match`，或標為 `custom`（俱樂部自建活動）；
含 `team_codes[]`（`D1`／`U15`／`U14`／`U12`）作為第一層分類。
複製一份賽事資料到行事曆＝製造兩個真實來源，必然不同步。

**③ 隊別代號是識別鍵**
`Team.code` 需唯一，直接用於行事曆分類、篩選標籤與訂閱網址（`/schedule/d1/`）。
新增梯隊（U18／U10）只需在 C1 新增一筆，前台分類自動出現。

---

## 3. 容易搞錯的關係

| 情況 | 正確做法 |
|---|---|
| 女子足球 | `Page` 型別，**不建 `Team`／`Player`／`Match`**。型別預留 `women` 但不啟用 |
| 一線隊 | `Team.code = D1`、`type = first_team`，**全站僅一筆** |
| 學院梯隊 | `Team.type = academy`，U15／U14／U12 各一筆 |
| 球迷會員 | **不是獨立名單**，是 `Member` 上的 `fan_club` 層級標記；**付費會員即球迷會員**，不是兩種身分 |
| 特約店家 | `PartnerStore` 是**獨立型別**，與 B2B 的 `Partner`（Logo 牆）不共用資料：受眾、欄位、維護節奏都不同 |
| **五種「商業對象」** | `Partner`（B2B Logo 牆）／`Sponsor`（贊助商）／`PartnerStore`（特約店家，會員折扣，**無金流無分潤**）／`DonationStore`（慈善站掃碼，**有金流有分潤**）／`Advertiser`（**App 廣告主，計曝光**）。**五個不同表**，同一家實體公司可能同時是數種，**各建一筆、不共用紀錄**。唯一例外：`Advertiser.sponsor_id` 可關聯回 `Sponsor`（避免重複維護聯絡窗口，**不是合併成一筆**） |
| 廣告曝光 vs 贊助曝光 | `AdEvent` 只記錄 `AdCreative` 的曝光。**贊助商 Logo 牆不產生任何 `AdEvent`、不進廣告報表** |
| 裝置 vs 會員 | `AppDevice` 可獨立存在（未登入亦註冊），`member_id` 為**可空的弱關聯**，登出即解除 |
| **兩種「項目／計畫」** | `DonationProject`＝募款標的（慈善站）；`CharityProgram`＝已執行的公益計畫（主站 11.2）。前者可關聯後者，反向不成立 |
| 捐款人與會員 | **只用 Email 軟性比對後標示**，不寫入 `Member`、不建關聯欄位、不做歸戶 |
| 家庭會籍 | 只是 `MembershipPlan` 的 `card_quota`／`jersey_quota` 不同，**不需要學員綁定關係**。**學員家長綁定於網頁、App、後台三方皆不做** |
| **抽獎名單** | `DrawRoster` 是**不可變快照**，不是即時 query 也不是外鍵解析：值複製當下的姓名與會籍狀態，會員日後改名、合併或刪帳號都不得改動已鎖定的名單。**不要「優化」成關聯查詢**——那會讓開獎失去稽核能力，也無法穩定配發序號 |
| **抽獎資格** | 是 `Member` 層級的**布林判定**（基準時間 `fan_club` 且會籍有效且帳號啟用），**不存在「抽獎報名」「抽獎券」「點數」型別**。一人一號，無次數或加權欄位 |
| 慈善報導 | 即時報導發布於 `Article`（7.7 社區活動），慈善單元用 `CharityProgram`／`ImpactRecord` 長期陳列，兩者互連**不重複建置** |
| 商品 | `ProductShowcase` 只有展示欄位 + Shopify 連結，**沒有庫存、價格同步、訂單** |
| 課程時段 | 屬 `Session`，**不進 `CalendarEvent`**。App 的「我的報名」可顯示梯次時間，但**不得寫入 `CalendarEvent`**，也不得出現在賽程分頁 |
| 試訓 | `Trial` 型別，預設**不同步**至行事曆（後台可開啟） |

---

## 4. 建議的共通欄位

規劃書未逐欄定義，以下為實作時的建議基線（屬執行層決定，可調整）：

| 欄位 | 用途 |
|---|---|
| `id` / `slug` | 唯一識別與 URL；客戶素材匯入時作為對應鍵 |
| `status` | `draft` / `published` / `scheduled` |
| `published_at` | 排程發布 |
| `zh_*` / `en_*` | 雙語欄位對 |
| `seo_title` / `seo_description` / `og_image` | 單頁 SEO（後台 H 模組） |
| `value_tags[]` | 五大核心價值標籤，可掛任何內容型別 |
| `created_by` / `updated_by` / `updated_at` | 稽核（J 模組要求保存 ≥ 12 個月） |

---

## 5. 批次匯入格式（需求已明確）

規劃書指定必須支援 CSV 匯入的項目：

| 項目 | 出處 |
|---|---|
| 整季賽程（`Match`） | C4 賽事管理 / L4 行事曆 — 共用機制 |
| 積分榜（`Standing`） | C4 |
| FAQ 題目 | B5（匯入 + 匯出） |
| 301 轉址對照 | H SEO（舊站遷移用） |

匯出需求：報名名單 Excel、簽到表、Lead 名單 CSV、詢問 CSV、會員 CSV（**需額外授權** + 稽核）、球衣出貨清單 CSV、續會名單 CSV、賽事 CSV/.ics。
