# 12d — 全表欄位盤點（S0-3c）

> **用途**：對 [`docs/12-database-schema.md`](12-database-schema.md) §4「資料表總覽」列出的 **104 張實體表**，
> 逐張核對規劃書明文要求的欄位是否已落在 ERD（[`12a`](12a-database-erd.md)）、關鍵表明細（[`12b`](12b-database-tables.md#6-關鍵資料表明細)）
> 或 i18n 側表（[`12c`](12c-i18n-tables.md)）三者之一。**這是盤點，不是修正**——本檔不改任何既有檔案，
> 也不提出新欄位名（若標「建議」則必須能指回規劃書原文）。
>
> **核對基準**：
> - `output/TCRFC_前後台功能規劃書.md`（**v3.11，1826 行**，2026-09-20）——**唯一真實來源**
> - `docs/12a-database-erd.md`（v3.0 同步後版本，17 張 ERD／2026-09-20 更新）
> - `docs/12b-database-tables.md`（12 張關鍵表明細、權限模型、索引，2026-09-20 更新）
> - `docs/12c-i18n-tables.md`（39 張 i18n 側表欄位起草，2026-09-20 產出，S0-3b）
>
> **日期**：2026-09-20
>
> ⚠️ **這是盤點不是規格**。任何要補的欄位，一律先確認規劃書原文是否真的要求、再回頭改 `docs/12a`／`docs/12b`／`docs/12c`
> ——不得直接把本檔的「建議」當成定案抄進 ERD。
>
> 🔵 **與 `docs/12c` 的分工**：`12c` 已對「哪些欄位因語系而不同、該不該進 i18n 側表」做過一輪詳盡核對，
> 並在其 §5「發現的問題」中**已經獨立發現** `Club.description` 缺失與 `ImpactRecord` 三個文字欄位整個沒有落點兩項。
> 本檔**重用並引用 `12c` 的既有結論**，範圍擴大到：非文字欄位（數值／日期／布林／座標／狀態）、多圖子表、
> 值域是否定義、ERD 反向多出的欄位，以及**整張表在 ERD 中完全沒有屬性方塊**（比缺個別欄位更嚴重的一種缺漏）。

---

## §1 核對方法與三種情形的判準

**核對方式**：以規劃書 §4 後台功能規劃（行 832–1452，每模組的「欄位：…」條列）與 §5.1 型別總表（行 1455–1518）為主，
§3 前台功能規劃（行 248–831）補充前台需顯示但後台條列漏寫的欄位；逐條與 `docs/12a` 的 ERD 屬性方塊、`docs/12b` 的
12 張關鍵表明細、`docs/12c` 的 i18n 側表欄位清單比對。

**三種情形的判準**：

| 標記 | 判準 |
|---|---|
| 🔴 **真的缺** | 規劃書有明文（可指出行號與原文），ERD 主表沒有對應欄位，`docs/12c` 的側表也沒有承接 |
| 🟡 **落點存疑** | 規劃書有寫，但語意可能已由其他既有欄位／關聯間接滿足（如經緯度可推算地圖連結），或規劃書字面與既有資料模型設計慣例衝突，需要人工判斷是否要新增欄位 |
| ✅ **不算缺** | 圖片衍生欄位（`_width`／`_height`／`_alt`）依 ERD 圖例刻意省略；i18n 側表欄位（走 `docs/12c`）；共通欄位（`created_at`／`updated_at`／`created_by`／`updated_by`，`docs/12` §1.3 全表通用不入圖）；由父表推導的欄位 |

**新增一種情形（本檔擴大範圍後才會遇到）**：

| 標記 | 判準 |
|---|---|
| ⬛ **整表無欄位定義** | 該表在 `docs/12` §4 資料表總覽有一列，`docs/12a` 的 ER 圖裡卻**只以關聯線出現、從未有自己的屬性方塊**（不是 GHOST 引用其他圖的表，是本圖也沒有畫出它自己的欄位）。這比「缺一兩個欄位」嚴重，因為代表**整張表目前無法轉 DDL**。本檔仍歸入 🔴，但獨立標注以免被誤讀成單一欄位缺漏 |

---

## §2 總結表

依 `docs/12` §4 的模組分節；張數不含 i18n 側表（`docs/12c` 已覆蓋）。

「核對張數」「有缺漏張數」以**表**為單位（同一張表無論缺幾個欄位都只算一張）；括號內另附該分節實際缺漏的**欄位／子功能筆數**，供對照 §3 的逐筆明細。

| 模組 | 核對張數 | 有缺漏張數（🔴，含⬛整表） | 缺漏欄位／子功能筆數 | 🟡 落點存疑張數 | 無從核對張數 |
|---|---|---|---|---|---|
| 4.0 共通機制 | 7 | 0 | 0 | 0 | 4（`Locale`／`UiString`／`UiStringTranslation`／`ValueTagLink`，見 §8） |
| 4.1 B 內容管理＋H | 16 | 2（`PressResource`／`Faq`） | 2 | 3（`Banner`／`HomeSection`／`Faq` 嵌入設定） | 0 |
| 4.2 C 球隊管理 | 15 | 7（`Competition`／`Team`／`Player`／⬛`PlayerSeasonStat`／⬛`MatchGoal`／⬛`MatchCard`／⬛`MatchLineup`） | 10 | 1（`Team.賽季`） | 1（`Season`，`docs/12c` 已認定） |
| 4.3 P 課程與活動 | 6 | 2（`Session`／`Registration`） | 5 | 1（`Trial.對象`，沿用 `12c`） | 0 |
| 4.4 E 商業模組 | 5 | 2（`Sponsor`＋新發現的**遺漏型別**「贊助活動 Activations」） | 2（含 1 個整型別缺席） | 0 | 0 |
| 4.5 F 文化模組 | 5 | 1（`FanEvent`） | 1 | 0 | 0 |
| 4.6 G 表單與詢問 | 5 | 1（`Enquiry`） | 1 | 1（`Enquiry.姓名`） | 0 |
| 4.7 I 網站設定 | 2 | 1（⬛**`Venue` 整表無欄位定義**） | 1（整表） | 0 | 0 |
| 4.8 J 系統管理 | 8 | 1（`Club`：`description`／`favicon`／`OG圖`／`輔色`／`預設語系`） | 5 | 1（`Club.官網連結`） | 0 |
| 4.9 K 會員管理 | 10 | 4（`Member`／`MembershipPlan`／`JerseyIssue`／`PartnerStore`） | 8（`PartnerStore` 一表即佔 5 項） | 2（`Member.球衣尺寸`／`MembershipPlan.期間起訖`） | 0 |
| 4.10 L 行事曆管理 | 5 | 1（`CalendarCustomEvent`） | 1 | 0 | 0 |
| 4.11 S 商店 | 15 | 2（`Product`／⬛`InvoiceDonationCode`） | 3 | 0 | 0 |
| 4.12 B6 慈善內容 | 5 | 2（`Charity`／`ImpactRecord`） | 4（`ImpactRecord` 已由 `12c` 發現三項） | 0 | 0 |
| **合計** | **104** | **26 張表有缺漏（含 6 張整表無欄位定義）** | **約 43 筆** | **9 張** | **5 張** |

> 26 張有缺漏＋9 張存疑＋5 張無從核對＋既有已知 i18n 缺漏（`docs/12c` 涵蓋，不重複計入）＝本次盤點實際觸及約 40 張表，其餘約 64 張核對後**沒有發現落差**。逐筆明細見 §3。

---

## §3 🔴 真的缺

### 4.1 B 內容管理

#### `PressResource`
- **缺欄位**：`sort_order`（排序）
- **規劃書行 1034**：「資源 CRUD：標題與說明（雙語）、類別、檔案、封面縮圖、發布日期、狀態（顯示／隱藏）、**排序**」
- **目前哪裡都沒有**：`docs/12a` §5.1b 的 `press_resource` 屬性方塊只有 `id`／`club_id`／`slug`／`resource_type`／`file_key`／`file_bytes`／`cover_key`／`cover_width`／`cover_height`／`published_on`／`download_count`／`status`，沒有 `sort_order`。`docs/12c` 未收錄（非文字欄位，不在其範圍）

#### `Faq`
- **缺欄位**：`view_count`（瀏覽數）
- **規劃書行 1019**：「成效數據：**各題瀏覽數**、👍／👎 數與比率」
- **目前哪裡都沒有**：`docs/12a` §5.1b 的 `faq` 屬性方塊只有 `helpful_count`／`unhelpful_count`／`sort_order`／`status`，沒有瀏覽數欄位（`helpful_count`／`unhelpful_count` 只對應 👍／👎，不含「瀏覽數」本身）

### 4.2 C 球隊管理

#### `Competition`
- **缺欄位**：`organizer`（主辦單位）
- **規劃書行 1463**：「代號、名稱（中／英）、類型（對應 `Match.competition` 四值）、所屬球季、**主辦單位**、排序、啟用狀態」
- **目前哪裡都沒有**：`docs/12a` §5.2 的 `competition` 屬性方塊為 `id`／`club_id`／`season_id`／`code`／`name_zh`／`name_en`／`comp_type`／`sort_order`／`status`，沒有主辦單位欄位

#### `Team`（兩項）
- **缺欄位 1**：主視覺（Hero 圖片，如 `hero_key`）
- **缺欄位 2**：代表色（如 `team_color`）
- **規劃書行 1043**：「球隊資料：所屬俱樂部、名稱、隊別代號…類型…性別…年齡層、賽季、簡介、**主視覺**、**代表色**、顯示排序」
- **目前哪裡都沒有**：`docs/12a` §5.2 的 `team` 屬性方塊為 `id`／`club_id`／`code`／`type`／`gender`／`age_band`／`sort_order`，**沒有任何圖片欄位、也沒有色彩欄位**。這不是「省略 `_width`／`_height`」的情形——`team` 連 `_key` 本身都不存在
- 附註：`name`／`intro` 已由 `docs/12c` 的 `team_i18n` 承接，不重複列入本節

#### `Player`（三項）
- **缺欄位 1**：身高體重（如 `height_cm`／`weight_kg`）
- **缺欄位 2**：慣用腳（如 `preferred_foot`）
- **缺欄位 3**：加入日期（如 `joined_on`）
- **規劃書行 1053**：「基本資料：中英姓名、背號、位置、生日、**身高體重**、國籍、**慣用腳**、**加入日期**、照片」
- **目前哪裡都沒有**：`docs/12a` §5.2 的 `player` 屬性方塊為 `id`／`club_id`／`team_id`／`shirt_no`／`position`／`birth_on`／`nationality`／`status`／`photo_key`，缺上述三項；也不在 `docs/12b`（`Player` 不屬於 12 張關鍵表）

#### ⬛ `PlayerSeasonStat`（整表無欄位定義）
- **規劃書行 1055**：「賽季數據：出賽、進球、助攻、黃紅牌（可手動輸入或由賽事自動彙總）」
- **目前哪裡都沒有**：`docs/12a` §5.2 只有 `season ||--o{ player_season_stat` 與 `player ||--o{ player_season_stat` 兩條關聯線，**從未出現 `player_season_stat { ... }` 屬性方塊**。`docs/12` §4.2 表總覽也只寫「逐季數據 `(player_id, season_id)`。由 `Player` 推導」，同樣沒有欄位。出賽數、進球數、助攻數、黃牌數、紅牌數目前**完全沒有落點**

#### ⬛ `MatchGoal`（整表無欄位定義）
- **規劃書行 1065**：「結果：比分、**進球者與時間**、卡牌、出賽名單、賽後報導連結」
- **目前哪裡都沒有**：`docs/12a` §5.2 只有 `match ||--o{ match_goal` 與 `player ||--o{ match_goal` 兩條關聯線，無屬性方塊。球員（進球者）與時間目前無欄位承載

#### ⬛ `MatchCard`（整表無欄位定義）
- **規劃書行 1065**：「結果：…**卡牌**…」
- **目前哪裡都沒有**：`docs/12a` §5.2 只有 `match ||--o{ match_card` 關聯線，無屬性方塊。卡別（黃／紅）、球員、時間均無欄位

#### ⬛ `MatchLineup`（整表無欄位定義）
- **規劃書行 1065**：「結果：…**出賽名單**…」
- **目前哪裡都沒有**：`docs/12a` §5.2 只有 `match ||--o{ match_lineup` 與 `player ||--o{ match_lineup` 兩條關聯線，無屬性方塊。先發／替補、上下場時間均無欄位

### 4.3 P 課程與活動

#### `Session`
- **缺欄位**：上課時間表（週期時段，如 `weekly_schedule`）
- **規劃書行 1082**：「梯次：期間、**上課時間表**、地點（關聯場地）、名額上限、目前報名數、費用…」；前台 3.5／5.1 亦呼應「課表（**週期時段表**）」（行 367）
- **目前哪裡都沒有**：`docs/12a` §5.4 的 `session` 屬性方塊只有 `start_on`／`end_on`（期間的起訖日期），沒有任何欄位表達每週上課的星期與時段（如「每週六 10:00–12:00」）

#### `Registration`（四項）
- **缺欄位 1**：家長／緊急聯絡人（如 `guardian_name`／`guardian_phone`）
- **缺欄位 2**：健康聲明與同意條款（如 `health_declaration`）
- **缺欄位 3**：備註（如 `note`）
- **缺欄位 4**：報名編號（人類可讀的編號，如 `registration_no`）
- **規劃書行 1087**：「報名詳情：學員資料、**家長聯絡**、**健康聲明**、**備註**」；前台報名流程（行 373）：「填寫學員資料（可多名） → **家長／緊急聯絡人** → **健康聲明與同意條款** → 送出 → **產生報名編號** → Email／簡訊通知」
- **目前哪裡都沒有**：`docs/12a` §5.4 的 `registration` 屬性方塊為 `id`／`club_id`／`session_id`／`trial_id`／`member_id`／`applicant_name`／`phone`／`email`／`birth_on`／`status`／`created_at`。只有單一申請人聯絡方式，沒有家長／緊急聯絡人、健康聲明、備註；也沒有比照 `Order.order_no`／`Member.member_no` 模式的人類可讀報名編號（僅有 `id` UUID）

### 4.4 E 商業模組

#### `Sponsor`
- **缺欄位**：贊助內容（description／content）
- **規劃書行 1105**：「贊助商：名稱、Logo、等級（主贊助／官方贊助／支持夥伴）、合約期間、**贊助內容**、聯絡窗口、到期提醒」
- **目前哪裡都沒有**：`docs/12a` §5.5 的 `sponsor` 屬性方塊沒有內容／說明欄位；`docs/12c` 起草的 `sponsor_i18n` 也只推定 `name`（見 `12c` §2），未包含贊助內容文字

#### ⬛ 贊助活動（Activations）——**整個型別在 ERD 與 `docs/12` §4 表總覽都不存在**
- **規劃書行 486**（前台 9.2）：「現有贊助商（依等級：主贊助／官方／支持）、贊助故事（案例文章）、**贊助活動紀錄**」
- **規劃書行 1107**（後台 E2）：「**贊助活動（Activations）**：活動名稱、日期、圖集、成效摘要」
- **目前哪裡都沒有**：`docs/12` §4.4「E 商業模組（5）」只列 `Partner`／`Sponsor`／`SponsorPackage`／`Proposal`／`ProposalFile` 五張表，**沒有對應「贊助活動」的型別**；`docs/12a` §5.5 的 ERD 同樣沒有任何 `sponsor_activation` 或類似實體。這不是欄位漏了，是**規劃書明文要求的一個完整子功能（含活動名稱、日期、圖集、成效摘要四個欄位與一張圖集子表）從資料模型裡整個不見了**

### 4.5 F 文化模組

#### `FanEvent`
- **缺欄位**：是否限付費會員報名（如 `paid_members_only`）
- **規劃書行 1135**：「球迷活動：活動 CRUD、**報名名單（可限定僅付費會員報名）**、活動回顧（關聯圖集與文章）」
- **目前哪裡都沒有**：`docs/12a` §5.6 的 `fan_event` 屬性方塊為 `id`／`club_id`／`slug`／`starts_at`／`capacity`，沒有任何欄位表達「僅付費會員可報名」這項限制

### 4.6 G 表單與詢問

#### `Enquiry`
- **缺欄位**：標籤（tags）
- **規劃書行 1150**：「指派負責人、內部備註、**標籤**」
- **目前哪裡都沒有**：`docs/12a` §5.4b 的 `enquiry` 屬性方塊為 `id`／`club_id`／`form_id`／`assignee_admin_user_id`／`source_path`／`utm_source`／`utm_campaign`／`status`／`internal_note`／`created_at`，沒有標籤欄位，也沒有對應的多對多標籤關聯表

### 4.7 I 網站設定

#### ⬛ `Venue`——**整表在 ERD 中沒有自己的屬性方塊**
- **規劃書行 1183**（I 模組）：「場地管理：**場地名稱**、**地址**、**經緯度**、**交通說明**、**照片**（供 5.1 訓練地點與 Location & Map 使用）」
- **規劃書行 1492**（§5.1 型別總表）：「`Venue` | 場地。**v2.5 新增 `lat` / `lng` 座標**，供行動 App 的賽事場地導航與課程地點使用」
- **目前哪裡都沒有**：`docs/12a` 全檔搜尋不到任何 `venue { ... }` 屬性方塊——每一處出現都是 `match }o--o| venue : "GHOST"`／`session }o--o| venue : "GHOST"`／`trial }o--o| venue : "GHOST"` 這種**只畫關聯不畫欄位**的引用寫法。`docs/12c` 起草了 `venue_i18n`（`name`／`address`／`directions`，見 `12c` 3.7），但**非語系欄位（`lat`／`lng`／`photo_key`）完全沒有落點，連主表本身的存在都沒有被畫出來**。`PartnerStore` 已有 `lat`／`lng` 可對照（`docs/12a` §5.6），`Venue` 卻連這組座標欄位都沒有，是本次盤點中**唯一一張規劃書明文要求、ERD 卻連表的輪廓都沒畫出來**的表

### 4.8 J 系統管理

#### `Club`（四項）
- **缺欄位 1**：`description_zh`／`description_en`（簡介）——**`docs/12c` §3.8／§5 已獨立發現此項**，本檔重複引用以求完整
- **缺欄位 2**：favicon（如 `favicon_key`）
- **缺欄位 3**：OG 圖（如 `og_image_key`）
- **缺欄位 4**：品牌輔色（如 `brand_secondary_color`）——目前只有 `brand_color` 一色
- **缺欄位 5**：預設語系（如 `default_locale`）
- **規劃書行 1216**（J4）：「俱樂部資料：新增與維護 `Club`：代號、**名稱與簡介（中／英）**、**標誌（淺底／深底）**、**favicon**、**OG 圖**、**品牌主色與輔色**、前台網域、官網連結、**預設語系**、排序、啟用狀態」
- **規劃書行 1462**（§5.1）：同一份欄位清單再次出現，用字相同
- **目前哪裡都沒有**：`docs/12a` §5.11 的 `club` 屬性方塊為 `id`／`code`／`domain`／`name_zh`／`name_en`／`logo_light_key`／`logo_dark_key`／`brand_color`（單色）／`invoice_title`／`tax_id`／`is_collecting_subject`／`sort_order`／`status`。標誌淺底／深底、代號、名稱、網域、排序、啟用狀態、抬頭與統編都已落地，**但簡介、favicon、OG 圖、輔色、預設語系五項規劃書明文要求的欄位完全找不到**。`docs/12c` 只補了 `description`，其餘四項不在 `12c` 範圍（非語系欄位），本檔補齊

### 4.9 K 會員管理

#### `Member`——**整表沒有姓名欄位**
- **缺欄位**：姓名（如 `name`）
- **規劃書行 1233**（K1）：「列表欄位：會員編號、**姓名**、Email、各俱樂部的會籍層級與到期日…」
- **規劃書行 1500**（§5.1）：「`Member` | 會員帳號（一人一組，不分俱樂部）：會員編號、**姓名**、Email（登入鍵，全站唯一）、電話、生日、球衣尺寸、註冊來源、LINE 綁定識別碼（加密）、帳號狀態」
- **目前哪裡都沒有**：`docs/12a` §5.6 的 `member` 屬性方塊為 `id`／`member_no`／`email`／`password_hash`／`phone`／`birth_on`／`line_user_id_encrypted`／`signup_source`／`status`／`created_at`——**完全沒有姓名欄位**，也沒有對應的 `member_i18n`（姓名本來就不該走語系側表，人名不因語系而不同）。這代表以目前的綱要，後台 K1 會員列表要顯示的「姓名」這一欄**沒有任何資料來源**，是本次盤點裡**影響最直接的一項**——不是次要展示欄位，是規劃書明文列在會員清單第二欄的核心資料

#### `MembershipPlan`
- **缺欄位**：排序（`sort_order`）
- **規劃書行 1242**：「方案設定：所屬俱樂部、方案名稱（中／英）、費用、球季代碼、期間起訖、`card_quota`…`jersey_quota`…季中入會計價規則、權益說明、**排序**、上下架」
- **目前哪裡都沒有**：`docs/12a` §5.6 的 `membership_plan` 屬性方塊為 `id`／`club_id`／`season_id`／`fee`／`card_quota`／`jersey_quota`／`mid_season_rule`／`status`，沒有 `sort_order`

#### `JerseyIssue`
- **缺欄位**：電話（`phone`）
- **規劃書行 782**：「付費會籍開通後，會員於會員中心填寫尺寸與領取方式（寄送／到場領取）；選寄送須填**收件人、電話與地址**」
- **目前哪裡都沒有**：`docs/12a` §5.6 的 `jersey_issue` 屬性方塊為 `id`／`club_id`／`member_id`／`recipient_name`／`size`／`delivery_method`／`address`／`status`／`shipped_on`。有收件人姓名與地址，**沒有電話**

#### `PartnerStore`（五項）
- **缺欄位 1**：照片／Logo（如 `image_key`）
- **缺欄位 2**：營業時間（如 `business_hours`）
- **缺欄位 3**：官網或社群連結（如 `website_url`）
- **缺欄位 4**：排序（`sort_order`）
- **缺欄位 5**：上下架狀態（`status`）
- **規劃書行 1257**：「特約店家 CRUD：名稱（中／英）、**照片／Logo**、類別、地址（中／英）、電話、**營業時間**、地圖連結、**官網或社群連結**、優惠內容（中／英）、適用層級、合作起訖、**排序**、**上下架**」
- **目前哪裡都沒有**：`docs/12a` §5.6 的 `partner_store` 屬性方塊為 `id`／`club_id`／`slug`／`category`／`address`／`lat`／`lng`／`phone`／`applicable_tier`／`start_on`／`end_on`。類別、地址、電話、經緯度、適用層級、合作起訖都有，但**照片／Logo、營業時間、官網或社群連結、排序、上下架狀態五項完全沒有欄位**——這是本次盤點中單一一張表缺最多欄位的案例

### 4.10 L 行事曆管理

#### `CalendarCustomEvent`
- **缺欄位**：外部連結或 CTA（如 `cta_url`）
- **規劃書行 1364**：「欄位：標題（中／英）、所屬隊別…起訖時間…地點…說明、封面圖、**外部連結或 CTA**、是否公開於前台」
- **目前哪裡都沒有**：`docs/12a` §5.3 的 `calendar_custom_event` 屬性方塊為 `id`／`club_id`／`event_type_id`／`venue_id`／`starts_at`／`ends_at`／`is_all_day`／`repeat_rule`／`is_public`／`cover_key`，沒有外部連結／CTA 欄位

### 4.11 S 商店

#### `Product`（兩項）
- **缺欄位 1**：標籤（tags）
- **缺欄位 2**：尺碼表（size chart，非語系欄位本身也缺，`docs/12c` 只討論其是否需要 i18n，未論及主表完全沒有此欄位）
- **規劃書行 1396**：「商品：名稱（中／英）、Collection 分類（俱樂部／學院／球迷）、**標籤**、圖集、商品敘事（中／英）、**尺碼表**、上下架、排序、SEO 欄位」
- **目前哪裡都沒有**：`docs/12a` §5.8 的 `product` 屬性方塊為 `id`／`club_id`／`slug`／`collection_id`／`is_new_arrival`／`sort_order`／`status`，沒有標籤關聯、也沒有任何尺碼表欄位（不論是圖片型或結構化型）

#### ⬛ `InvoiceDonationCode`（整表無欄位定義）
- **規劃書行 1429**：「電子發票服務設定與憑證：字軌、**捐贈碼名單**、開立與作廢的重試設定」
- **目前哪裡都沒有**：`docs/12` §4.11 表總覽只寫「捐贈碼名單（S6 維護）。全系統共用」，`docs/12a` 全檔搜尋不到 `invoice_donation_code { ... }` 屬性方塊。至少需要「捐贈碼本身」一欄，規劃書未進一步展開，無法得知是否還需要機構名稱等欄位（見 §8）

### 4.12 B6 慈善內容（主站）

#### `Charity`
- **缺欄位**：聯絡窗口（contact，如 `contact_name`／`contact_phone`）
- **規劃書行 1026**：「公益團體資料：團體名稱、簡介、Logo 或代表圖、官網連結、**聯絡窗口**、合作紀錄」
- **目前哪裡都沒有**：`docs/12a` §5.9 的 `charity` 屬性方塊為 `id`／`club_id`／`slug`／`logo_key`／`website_url`，沒有聯絡窗口欄位；`docs/12c` 起草的 `charity_i18n` 只有 `name`／`intro`，同樣沒有涵蓋

#### `ImpactRecord`（三項，`docs/12c` 已發現，本檔引用並標注模組位置）
- **缺欄位**：`donation_content`（捐助內容）、`location`（地點）、`brief_description`（簡述）
- **規劃書行 1025**：「事蹟紀錄（Impact Record）：三項必填 —— 公益團體名稱、**捐助內容**（文字描述）、活動圖片（可多張）；另含日期、**地點**、**簡述**、所屬計畫（選填）」
- **目前哪裡都沒有**：`docs/12a` §5.9 的 `impact_record` 屬性方塊只有 `id`／`club_id`／`charity_program_id`／`charity_id`／`image_key`／`image_width`／`image_height`／`happened_on`——三項必填之一的「捐助內容」與另含的「地點」「簡述」**在主表與 `docs/12c` 的 `impact_record_i18n` 都找不到**（`12c` 已將這三欄列為草稿建議，但明文標注「目前完全沒有落點」，見 `12c` §5 第 3 點）

---

## §4 🟡 落點存疑

| 表 | 規劃書字面 | 存疑原因 |
|---|---|---|
| `Banner` | 行 282：「Hero 主視覺：**影片或圖片輪播**（最多 5 則）」 | ERD `banner` 只有 `image_key`，沒有影片欄位（`video_url` 或 `video_key`）。但 v3.5–v3.9「後台圖片上傳通則」全篇只談圖片重新編碼，從未再提影片，不確定「影片」選項是否已被後續版本悄悄取消，還是純粹遺漏欄位 |
| `HomeSection` | 行 1013：「首頁各區塊開關與排序、**精選內容指定**」 | `home_section` 只有 `is_enabled`／`sort_order`，沒有欄位表達「這個區塊要精選哪幾筆內容」。可能已由各內容型別自己的 `is_featured`（如 `Article.is_featured`）滿足，也可能需要獨立的精選清單，規劃書字面不夠具體以判斷 |
| `Faq` | 行 1018：「嵌入設定：指定該題可出現於哪些頁面的 FAQ 快捷區塊（G-12），**或由分類自動對應**」 | 條文本身給了兩種可能（手動指定頁面 vs. 分類自動對應），現有欄位（`faq_category_link`）只支援後者；「手動指定頁面」是否仍要做不確定 |
| `Team` | 行 1043：「球隊資料：…年齡層、**賽季**、簡介…」 | `Team` 是跨季存在的實體（一支球隊打很多季），`team` 主表加 `season_id` 在架構上說不通；比較合理的解讀是筆誤或指「目前所屬賽季」的顯示邏輯，而非儲存欄位。列為存疑而非確定缺漏 |
| `Trial` | 行 1095：「對應 3.3、4.7、6.3 試訓資訊：日期、地點、**對象**、名額、報名截止…」 | `docs/12c` §4 已列為低信心度候選欄位（`audience`），`trial` 主表確實沒有對應欄位。本檔沿用 `12c` 判斷，不重複升級信心度 |
| `Enquiry` | 行 1148：「欄位：來源表單、**姓名**、聯絡方式、內容摘要、來源頁面、UTM 來源、送出時間」 | `enquiry` 主表沒有姓名／聯絡方式欄位，但 `Enquiry` 設計上是靠 `EnquiryAnswer` 承載動態表單填答內容，姓名很可能只是「動態欄位之一」而非固定欄位。若列表要能直接排序／篩選姓名，才需要在主表加冗餘欄位；規劃書字面不足以判斷是否需要 |
| `Club` | 行 1216：「…前台網域、**官網連結**、預設語系…」 | `club` 已有 `domain`（前台網域），「官網連結」是否為獨立欄位或只是同一件事的重複敘述不確定 |
| `Member` | 行 1500：「…電話、生日、**球衣尺寸**、註冊來源…」 | `member` 主表沒有球衣尺寸欄位，但 `JerseyIssue.size` 已逐件記錄實際尺寸（含家庭方案多件不同尺寸）。`Member` 層級的「球衣尺寸」可能是註冊時的預設偏好值，也可能只是規劃書把 `JerseyIssue` 的欄位錯記到 `Member` 身上 |
| `MembershipPlan` | 行 1242：「…球季代碼、**期間起訖**…」 | `membership_plan` 已有 `season_id`，而 `Season` 本身帶 `start_on`／`end_on`。方案的販售期間是否可能短於或不同於球季期間（因而需要獨立欄位）不確定 |

---

## §5 「可多張」缺子表的

全文搜尋規劃書「圖集」「可多張」「批次上傳」「藝廊」關鍵字後逐一核對：

| 型別 | 規劃書字面與行號 | 現狀 |
|---|---|---|
| `ImpactRecord` | 行 1025：「活動圖片（**可多張**）」 | ⬛ **缺子表**。`impact_record` 主表只有單一 `image_key`／`image_width`／`image_height`，沒有 `impact_record_image` 之類的子表。與 `CharityProgramImage`（同模組、已有子表）待遇不一致 |
| 贊助活動 Activations | 行 1107：「活動名稱、日期、**圖集**、成效摘要」 | ⬛ **整個型別缺席**，圖集子表自然也不存在（見 §3 4.4 節） |
| `FanEvent` | 行 1135：「活動回顧（**關聯圖集**與文章）」 | ⬛ **缺子表**。`fan_event` 沒有任何圖片欄位或圖集子表，與文章的關聯也未建模（可能靠通用的 `article_relation` 多型表覆蓋，但圖集本身仍無落點） |

**已確認有子表、不算缺漏的對照組**（供交叉核對用，非本節缺漏）：`Product`→`ProductImage`、`CharityProgram`→`CharityProgramImage`、`ComicEpisode`→`ComicPage`、`Proposal`→`ProposalFile`（多語 PDF 而非圖集，性質不同但同屬子表模式）。

---

## §6 值域未定義的欄位清單

規劃書提到「狀態」「類別」等 enum 型欄位，但沒有給出完整值域，或不同章節給出的值域彼此不一致。**只列出，不推值域**：

| 表.欄位 | 問題 |
|---|---|
| `MemberCard.status`（卡片狀態） | 規劃書多處提到「狀態」但從未列舉具體值（如 active／lost／revoked），`docs/12b` §6.5 也只講規則不給值域 |
| `PressResource.resource_type`（媒體資源類別） | 規劃書 7.8／B6（行 408、1033）只用中文描述「新聞稿、品牌識別包（Logo／CIS）、高解析圖」三類，沒有給出對應的 enum 代碼值 |
| `Member.status` 與 K1 用語不一致 | `docs/12a` §5.6 的 `member.status` 暗示值域為 `active`／`suspended`／`deleted`（`docs/12b` §6.3），但規劃書 K1（行 1233）列表欄位寫的是「狀態（**啟用／停用／未驗證**）」——兩處的值域用詞對不上（`未驗證` 在 `active/suspended/deleted` 三值裡沒有對應項），需要人工確認是否為同一組值的不同措辭 |
| `Match.status` 兩處行文不一致 | C4（規劃書行 1064）給「未開始／進行中／已結束／延期」四值，前台 3.13（規劃書行 653）給「未開始／進行中／已結束／延期／取消」五值（多一個「取消」）。DDL 若只依 C4 會少一個狀態值 |
| `Competition.comp_type` | 規劃書行 1463 只說「類型（對應 `Match.competition` 四值）」，四值本身要去 `Match.competition` 的定義處對照，`Competition` 自己的欄位描述沒有重複列出 |
| `CalendarCustomEvent.repeat_rule` | 規劃書行 1365 給「每週／每兩週／每月」三種頻率，但沒有給出對應的 enum 代碼或是否採 RRULE 格式的技術決定 |

---

## §7 ERD 有但規劃書沒有的

以下欄位存在於 ERD，但在規劃書逐欄核對時找不到明確對應文字——多數是合理的技術性補充（如排序或輪次編號），**列出是為了讓人工確認來源，不代表建議刪除**：

| 表.欄位 | 說明 |
|---|---|
| `Match.round_no`（輪次編號） | 規劃書 C4（行 1063–1068）沒有提到「輪次」，`round_no` 可能是聯賽賽制的合理技術性補充（第幾輪），但無法指出規劃書原文依據 |
| `Standing.rank`／`played`／`points` | 規劃書只寫「積分榜：手動維護表格」（行 1066），沒有逐欄列出「名次」「出賽場次」「積分」三個具體欄位名，但這是積分榜的標準構成，判斷為合理的技術性展開而非無中生有 |
| `Team.age_band` | 規劃書沒有把「年齡層」與「隊別代號」分開成兩個獨立欄位描述（行 1043 只寫「年齡層」一詞，未明言與 `code` 是否為同一件事），`age_band` 作為獨立欄位是否必要待確認 |

---

## §8 規劃書無欄位級描述、無從核對的模組

以下表在 `docs/12` §4 資料表總覽有一列，但**規劃書全文找不到任何欄位級文字描述**（不同於「有描述但 ERD 沒接住」的 §3／§5，這裡是規劃書本身就沒寫到這個細節）：

| 表 | 說明 |
|---|---|
| `Locale` | 語系主檔（代碼、名稱、是否預設、fallback、排序）完全是 `docs/12` 自行導入的技術性型別，用於支撐「架構須預留第三語系擴充」（規劃書 G-01，行 260）這句話，但規劃書從未逐欄描述 `Locale` 本身要存什麼 |
| `UiString` / `UiStringTranslation` | 介面字串鍵與翻譯，用於支撐規劃書「字串翻譯表」（行 1183）這句話，但同樣是 `docs/12` 的技術性展開，規劃書沒有逐欄描述 |
| `ValueTagLink` | 支撐規劃書 1.2「核心價值作為內容標籤（Value Tag）存在後台，可標記於任何文章／球員故事／活動」（行 175）這句建議性文字，但規劃書用詞是「建議」，沒有給出資料結構，欄位形式（多型關聯 `entity_type`／`entity_id`／`value_tag`）是 `docs/12a` 自行設計 |
| `Season` | `docs/12c` §3.2 與 `docs/12` §12 踩雷點 17 都已確認：規劃書全文找不到 `Season` 的任何文字型欄位描述，`Season` 本身是 `docs/12` 新增的型別（規劃書只在其他型別的關聯欄提到「球季」二字），非欄位級遺漏，是型別本身就不是規劃書逐欄定義的對象 |
| `InvoiceDonationCode` | 規劃書只有「捐贈碼名單」四字（行 1429），連捐贈碼本身要存什麼格式、是否需要機構名稱等輔助欄位都沒有展開，見 §3 已列為整表無欄位定義，此處補充說明其「無從核對」的原因是規劃書描述本來就極簡 |

> 上述五張表**不是「漏寫」**，是規劃書行文本來就停在功能敘述或建議層級，沒有進到欄位。列在此處是為了誠實標注「這裡沒有更多可核對的依據」，而不是含糊地算進 §3 或跳過不提。

---

## §9 S0-6c 灌種子資料時新發現的落差（2026-09-21）

> 這一節的核對基準與 §1–§8 不同：不是逐條核對規劃書文字，而是**實際把 `site/src/data/*.json`（mockup 用的
> 球員／新聞／賽程等六個 JSON）灌進 `docs/12` 轉出的 DDL 時，發現 JSON 有欄位、資料表卻沒有對應落點**。
> 這種落差 §1–§8 的核對方法查不到——規劃書文字本身沒錯，是**既有內容裡有一個具體資料點，
> 剛好落在規劃書與 ERD 都沒展開到的顆粒度**。詳細操作見 [`../db/seed/README.md`](../db/seed/README.md)。

| 表.欄位（JSON 來源） | 問題 |
|---|---|
| `Match`（`content/schedule/2026-27_企甲賽程.csv`／`site/src/data/schedule.json` 的 `match_no`） | ✅ **已解決（2026-09-21）**。每筆賽程都有聯賽官方配發的「場次編號」（如 `match_no: 3`），與 `round_no`（第幾輪）是兩個不同的東西——同一輪可能對應多場比賽，各有自己的官方編號。已跑完規格異動同步鏈：規劃書 C4（中英雙版）補上「場次編號」欄位並升版 v3.12、`docs/12`／`docs/12a`／`docs/12b` 補上欄位說明與 ERD 屬性、`db/club-schema.sql` 的 `matches` 表加 `match_no int NULL`（可為空，因盃賽等非聯賽賽事可能沒有官方編號）。**未新增唯一鍵**——`match_no` 只在同賽季同聯賽內唯一，且會有 NULL 並存，SQL Server 唯一索引把 NULL 當成相等會誤擋，留待實際需要時再設計篩選式唯一索引 |
| `Article.article_category_id`（`site/src/data/news.json` 的 `category` 值 `intcup`） | 🟡 **落點存疑，非欄位缺漏**。News mockup 用的 6 個 `category` 值裡，`intcup`（台中磐石國際足球盃）沒有任何一個規劃書 7.1–7.8 的分類字面對得上。本次逐篇標題人工確認內容皆為賽事報導，**歸類到 7.2 Match Reports**——這是 seed 時的人工判斷不是規劃書規則，正式資料應由後台人工複核分類是否需要獨立看待「盃賽」與「聯賽」報導 |
| `StaffTeam`（`site/src/data/coaches-academy.json`，青訓教練／青訓總監） | 🟡 **資料缺口，非欄位缺漏**。來源 JSON 沒有標明青訓教練是帶 `U15`／`U14`／`U12` 哪一隊，本次 seed **刻意不連結任何 `Team`**，避免臆測。連帶地本次也沒有建立這三支學院球隊（`Team.type = 'academy'`）——沒有球員名單可以佐證需要建隊，建了也是空殼 |
| `Article.cover_key`／`Player.photo_key`（`news.json` 的 `cover`／`cover_web`，`players.json` 的 `photo`） | ✅ **不算缺，是已知的 pipeline 落差**。JSON 的 `cover_web` 是 mockup 靜態資源相對路徑，`players.json` 的 `photo` 全部是 `null`——兩者都不是走過「上傳即縮圖」pipeline（`docs/14`）後產生的 Blob object key，本次 seed 一律留 `NULL`，不把 mockup 路徑硬塞進 `_key` 欄位誤導未來開發者 |

---

## 檔案版本

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-09-20 | 首版，S0-3c 全表欄位盤點 |
| v1.1 | 2026-09-21 | 新增 §9：S0-6c 灌種子資料時從實際 JSON 內容發現的落差（`Match.match_no` 真的缺；`intcup` 分類、學院教練隊別歸屬為資料缺口非欄位缺漏） |
| v1.2 | 2026-09-21 | §9 `Match.match_no` 落差已解決：規格異動同步鏈跑完（規劃書 v3.12、`docs/12`／`12a`／`12b`、`db/club-schema.sql`），欄位補上 |
