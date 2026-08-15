# 04 — 資料模型與內容型別

> 來源：規劃書 §5（行 956–1000）

---

## 1. 全部型別

| 型別 | 說明 | 主要關聯 |
|---|---|---|
| `Page` | 靜態頁面（含區塊）；**女足介紹頁亦屬此型別** | SEO、多語系 |
| `Article` | 新聞與故事 | Category、Tag、Player、Team、Match、Program |
| `Team` | 球隊，含隊別代號 `D1`／`U15`／`U14`／`U12`；女足暫不使用 | Player、Coach、Match、Season、CalendarEvent |
| `Player` | 球員 | Team、Article、Stats、Pathway |
| `Staff` | 教練與團隊成員 | Team、Program |
| `Match` | 賽事 | Team、Season、Article（賽後報導） |
| `Standing` | 積分榜 | Season、Team |
| `Achievement` | 榮譽 | Team、Season |
| `Milestone` | 里程碑 | — |
| `Program` | 課程／營隊／專項 | Session、Staff、Venue、Partner |
| `Session` | 梯次／場次 | Program、Venue、Registration |
| `Registration` | 報名 | Session、Contact |
| `Trial` | 試訓場次 | Team、Venue、Registration |
| `Partner` | 合作夥伴 | Article、Program |
| `Sponsor` | 贊助商 | SponsorPackage、Article |
| `SponsorPackage` | 贊助方案（9 種） | Enquiry |
| `ProductShowcase` | 商品櫥窗（**僅展示 + Shopify 連結，非電商實體**） | Collection |
| `ComicEpisode` / `ComicCharacter` | 漫畫集數／角色 | Player（原型） |
| `FanEvent` | 球迷活動 | Member |
| `Enquiry` | 表單詢問（10 類 + 志工／捐助） | Form、Assignee |
| `Venue` | 場地 | Program、Match、Trial |
| `MediaAsset` | 媒體資產 | 全域 |
| `Faq` / `FaqCategory` | 常見問題／主題分類 | Page（嵌入位置） |
| `CharityProgram` | 慈善計畫 | Charity、Partner、Article、ImpactRecord |
| `ImpactRecord` | 慈善事蹟紀錄 | Charity、CharityProgram |
| `Charity` | 受贈公益團體 | CharityProgram、ImpactRecord |
| `Donation` | 捐款紀錄 | Member、CharityProgram |
| `ImpactMetric` | 影響力統計項目 | CharityProgram |
| `Member` | 會員帳號，含註冊來源、**LINE 綁定識別碼**、通知偏好 | Registration、FanEvent、StudentLink、LineEntryCode |
| `StudentLink` | 家長 ↔ 學員綁定關係 | Member、Registration |
| `Notification` | 通知／推播紀錄 | Member |
| `LineEntryCode` | LINE 入會 QR Code（帶參數，用於來源追蹤） | Member |
| `CalendarEvent` | **行事曆事件（彙整視圖）** | Match、Team、Venue |
| `EventType` | 賽事／活動類型（圖示、色彩、顯示規則） | CalendarEvent |
| `EventInterest` | 會員「我要參加」／提醒設定 | Member、CalendarEvent |

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
| 球迷會員 | **不是獨立名單**，是 `Member` 上的 `fan_club` 層級標記 |
| 慈善報導 | 即時報導發布於 `Article`（7.7 社區活動），慈善單元用 `CharityProgram`／`ImpactRecord` 長期陳列，兩者互連**不重複建置** |
| 商品 | `ProductShowcase` 只有展示欄位 + Shopify 連結，**沒有庫存、價格同步、訂單** |
| 課程時段 | 屬 `Session`，**不進 `CalendarEvent`** |
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

匯出需求：報名名單 Excel、簽到表、Lead 名單 CSV、詢問 CSV、會員 CSV（需權限 + 稽核）、賽事 CSV/.ics。
