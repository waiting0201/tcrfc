# 12b — 資料表明細、權限模型與索引

> 本檔是 [`12-database-schema.md`](12-database-schema.md) 的一部分，**章節編號沿用原檔**。
> 🔴 **v3.0 同步尚未完成**——動工前必讀主檔的「v3.0 落差」段落，遇衝突一律以規劃書為準。
> **DBMS 未定案**：不寫 DDL、不用廠商專屬型別。

> 其餘部分：型別詞彙與資料表總覽見主檔，關聯圖見 [`12a-database-erd.md`](12a-database-erd.md)。

## 6. 關鍵資料表明細

ER 圖已給欄位與型別，本節只補**值域、唯一鍵與約束**——這 12 張表是最容易做錯的。

### 6.1 `Team`

| 項目 | 規則 |
|---|---|
| `code` | **UNIQUE**。值域**只有** `D1`／`U15`／`U14`／`U12`。**全站沒有 `D2`** |
| `type` | `first_team`（⚠️ **v3.0 改為「每俱樂部至多一筆」**：磐石是 `D1`、藍鯨是 `BW1`）／`academy`（U15／U14／U12）。⚠️ **`women` 值已廢除**——性別改用獨立的 `gender` 欄位（`men`／`women`／`mixed`） |
| 用途 | 行事曆第一層分類、篩選標籤、訂閱網址 `/schedule/d1/`。新增梯隊（U18／U10）只需 C1 新增一筆，前台分類自動出現 |
| 對外顯示 | `D1` 是代號，前台一律顯示 `First Team / 一線隊` |

### 6.2 `Member`

| 欄位 | 值域與約束 |
|---|---|
| `tier` | `registered`（免費）／`fan_club`（付費＝球迷會員）。**不是兩種身分，是同一個 `Member` 上的層級標記** |
| `member_no` | UNIQUE。格式建議 `TCR-<球季>-<流水號>`（待確認事項第 7 點） |
| `email` | UNIQUE，🔒 受限。**前台登入識別**（與後台 `AdminUser.username` 無關） |
| `signup_source` | `web`／`line`／`admin`／`app`。**不含 `google`**——不採用 Google 登入 |
| `line_user_id_encrypted` | 🔒 加密儲存，**不得匯出** |
| `status` | `active`／`suspended`／`deleted` |
| 會籍計期 | **球季制**，全體同時到期；`membership_start_on`／`membership_end_on` 由 `MembershipPayment` 開通時寫入 |
| 抽獎資格 | **算出來的布林值**：`tier = 'fan_club'` AND `membership_end_on >= snapshot_at` AND `status = 'active'`。**沒有欄位、沒有表** |
| 刪帳號 | **欄位清除不是刪列**：保留 `member_no` 與遮罩姓名，其餘個資清除。稅法要求保留的訂單與發票**優先於刪除請求** |

### 6.3 `MemberCard`

| 項目 | 規則 |
|---|---|
| 列數 | **一張卡一列**，數量上限為 `MembershipPlan.card_quota`（家庭方案可為 3） |
| `token` | UNIQUE，**不可由 `member_no` 推導**。公開驗證頁 `/m/<token>` 使用 |
| 唯一性 | **一張卡只有一組 token**，官網驗證頁與 App 內卡片共用。發兩組＝兩份可撤銷狀態，撤銷必漏一邊 |
| 折扣使用 | 到店**出示卡片目視即可**，QR 指向公開唯讀驗證頁。**不核銷、不計次、店家不需系統**——所以沒有 `redemption` 任何表 |

### 6.4 `MemberDraw` / `DrawRoster`

| 項目 | 規則 |
|---|---|
| `MemberDraw.status` | `draft`／`roster_locked`／`drawn`／`announced`／`closed`／`voided` |
| `snapshot_at` | 資格基準時間。名單於此刻**一次性寫入**，會員無法自行建立 |
| `roster_version` | 名單版本。有誤只能**整份作廢重產**（版本 +1），**舊版保留不刪** |
| `roster_hash` | 名單雜湊，供開獎當下的完整性佐證 |
| `DrawRoster.serial_no` | **依 `member_no` 升冪連號配發，一人一號**。`(member_draw_id, serial_no)` UNIQUE。鎖定後不得重排 |
| 快照欄位 | `member_no_snapshot`／`name_snapshot`／`tier_snapshot`／`membership_end_on_snapshot` 皆**值複製**，母表變動不追溯 |
| `is_winner` | **後台人工回填**——系統不抽出，實體開獎在現場或直播進行 |
| `withholding_data_encrypted` | 🔒 **僅達扣繳門檻時蒐集**，加密、預設遮罩 |
| 通知 | **不發中獎通知信、不推播**。中獎只以最新消息公布（7.1 ＋`球迷會員抽獎` 標籤，遮罩） |

### 6.5 `Order`

| 欄位 | 值域與約束 |
|---|---|
| `order_no` | UNIQUE。**若商店與 App 會籍付款共用 LINE Pay 商店號，須以訂單前綴區分以利對帳**（待確認事項第 22 點） |
| `member_id` | **可為空**——非會員可結帳。**這條不得更動** |
| `lookup_token` | UNIQUE。非會員訂單查詢用（`/zh/order/lookup/`） |
| `payment_status` | `pending`／`paid`／`failed`／`expired`／`refunded`。**逾時未付款自動取消並釋回庫存** |
| `order_status` | `待付款`／`已付款`／`備貨中`／`已出貨`／`已完成`／`已取消`／`退貨處理中`／`已退款` |
| `delivery_method` | `home_delivery`／`cvs_pickup`／`onsite_pickup`（實際開哪幾種待確認第 24 點） |
| `shipping_fee` | **單一固定運費**，免運門檻另存 `Setting`。**沒有級距、沒有重量計費** |
| `is_manual` | 現場銷售補登（S3），退款人工執行並記 `handled_by` |
| 收件人三欄 | 🔒 **視同會員個資**：完整值僅系統管理員、客服／行政與出貨角色可見 |
| 不存在的欄位 | `discount_code`、`member_price`、`points_used`、`card_no`、`shipping_tier` ——**一律沒有** |

### 6.6 `OrderItem`（📸 快照）

| 項目 | 規則 |
|---|---|
| 快照四欄 | `product_name_snapshot`／`variant_label_snapshot`／`sku_snapshot`／`unit_price_snapshot`，建單當下**值複製** |
| `product_variant_id` | **僅供追溯**。讀取訂單時**不得回頭 join 取名稱與價格** |
| 理由 | 商品改名、改價、下架**都不得改動歷史訂單**，否則對帳與客訴舉證失去依據 |
| 同類 | `DrawRoster`、`SettlementLine`、`StoreInvoice`、`DonationInvoice` 適用同一原則 |

### 6.7 `StoreInvoice`

| 項目 | 規則 |
|---|---|
| 抬頭 | **俱樂部**。與協會發票**分屬不同字軌**，不得共用 |
| 三選一 | `carrier_type`＋`carrier_id_encrypted`（載具）／`tax_id`（統編）／`donation_code`（捐贈碼）——**恰有一組非空** |
| `issue_status` | `pending`／`issued`／`failed`，失敗可重試（`retry_count`） |
| `void_status` | `none`／`voided`（作廢）／`allowance`（折讓）。**退貨必須作廢或折讓** |
| 前提 | **LINE Pay 本身不開發票**，須另接發票服務（待確認第 23 點） |

### 6.8 `PaymentChannel`

| 項目 | 規則 |
|---|---|
| `subject` | **`club`（俱樂部）／`association`（協會）二選一** |
| 唯一鍵 | `(subject, channel_type, environment)` UNIQUE |
| `channel_type` | `linepay`／`einvoice` |
| `environment` | `sandbox`／`production` |
| 憑證 | 🔒 加密儲存，**僅系統管理員可見**（S6／N7）。輪替記 `rotated_at` |
| 風險 | **填錯商店號＝款項進錯法人**，同時踩稅務與《公益勸募條例》 |

### 6.9 `Donation`

| 項目 | 規則 |
|---|---|
| 收款主體 | **協會**。發票與收據抬頭、系統信署名、對帳單一律為協會 |
| `store_id` | **可為空**（非掃碼進入時）。為空時 `store_share_pct_snapshot = 0`，仍能結算 |
| 分潤五欄 | `store_share_pct_snapshot`／`project_share_pct_snapshot`（`decimal(5,2)`）→ `store_amount`／`project_amount`／`association_amount`（`int`，元）。**無條件捨去至整數元**，三者相加須等於 `amount` |
| 約束 | `store_share_pct + project_share_pct <= 100`（N2 驗證） |
| 捐款人 | 只有 `donor_name`／`donor_email`／`is_anonymous`。**不建帳號、不歸戶、不得有 `member_id`** |
| `invoice_mode` | `b2c_invoice`／`donation_receipt`，**由 `DonationProject` 決定**，建單時快照 |
| 退款 | **對外不受理**，後台保留人工退款供誤捐個案，記 `refund_reason` 與 `refunded_by` |

### 6.10 `Settlement` / `SettlementLine`

| 項目 | 規則 |
|---|---|
| `payee_type` | **`store`／`project` 二選一，兩份不同的對帳單** |
| 計算基準 | **只計 `status = 'paid'` 的捐款**，用**捐款當下的快照百分比**，改設定不追溯 |
| 退款沖回 | 以**負項** `SettlementLine`（`is_clawback = true`）進入下一期，**不改原列、不重算已付款期間** |
| `status` | `待結算`／`已結算`／`已付款`。**實際匯款人工執行**，系統只登記 `remitted_on` 與 `remit_note` |
| 職責分離 | 標記「已付款」的人與執行匯款的人**不得為同一人**（慈善站 §10）——以權限碼分離，不落資料表 |

### 6.11 `CalendarEvent`（視圖）

| 項目 | 規則 |
|---|---|
| 實作 | **視圖或索引表**，以 `source_type` + `source_id` 指向來源，**不複製資料** |
| `source_type` | `match`（來源 C4）／`trial`（**L3 開關，預設關閉**）／`custom`（來源 `CalendarCustomEvent`） |
| 隊別分類 | `CalendarEventTeam` 關聯表（`team_codes[]` 的實作），第一層分類 |
| **永不進入** | **`Session` 課程時段**——行事曆以比賽為核心，學院課程、營隊、專項訓練不納入 |
| 權限 | **跟隨來源模組**：能編輯哪些事件取決於對賽事資料的權限（學院管理者可改梯隊賽程、不能改一線隊） |

### 6.12 `AdminUser`

見 [§7.6](#76-管理員用-username-不用-email)。

---

## 7. 權限模型（J 模組）

### 7.1 七張表（v3.0：由五張增為七張）

**能做什麼**：`AdminUser` → `AdminUserRole` → `AdminRole` → `RolePermission` → `Permission`
**對誰做**：`AdminUser` → **`AdminUserClub`** ／ **`AdminUserTeam`**

有效權限 ＝ 使用者所有角色的權限**聯集**；`is_super_admin = true` 者**跳過整個查詢**。

```
AdminUserClub(admin_user_id PK, club_id PK, granted_on, expires_on NULL, granted_by, is_active)
AdminUserTeam(admin_user_id PK, team_id PK, expires_on NULL, is_active)
AdminRole   + scope_mode enum(all_clubs, own_clubs) NOT NULL DEFAULT 'all_clubs'
AdminUser   + primary_club_id uuid NULL FK → Club     -- 站台切換器預設值，不是 club_id
Permission  + is_club_scoped bool
RolePermission: scope_type 加值 own_clubs；scope_value json ❌ 刪除
```

**為什麼授權掛在「人」不是「角色」**：把俱樂部放在 `AdminRole` 上，每多一個俱樂部就要複製整組九個角色，第三個俱樂部進來就是 27 個。**角色定義「能做什麼」，`AdminUserClub` 定義「對誰做」。**

**為什麼 `scope_value` 直接刪而不是改成關聯表**：「哪些具體對象」現在全由上面兩張關聯表承載（在人身上），角色只需宣告「這個權限受不受範圍限制」。這符合 §1.4 第 1 條「陣列一律以關聯表表達」。

**`AdminUserTeam` 順帶補掉一個既有的坑**：§12 踩雷點 27 自承「學院管理者不能改一線隊這條，資料模型上沒有欄位可擋」——現在有了。

### 7.1b 資料範圍的執行期規則（v3.0 新增）

- **有效範圍 ＝ `AdminUserClub` 中 `is_active` 且 `expires_on` 未到期的 `club_id` 集合。**
- 所有清單查詢一律 `WHERE resource.club_id IN (:allowed)`，且**必須在資料存取層強制**——介面隱藏不算數，擋不住直接呼叫端點與匯出。
- **`club_id IS NULL` 的列對 `scope_mode = 'own_clubs'` 一律唯讀**，只有 `is_super_admin` 能建立與修改。
- **匯出先套資料範圍，再套 `is_restricted` 的二次授權**——兩道關卡，不可互相取代。
- `is_super_admin = true` 跳過整個範圍查詢。
- **`expires_on` 到期自動失效**，不需人工回收。這兌現了 App 規劃書「授權有起訖日」的承諾——舊版綱要沒有欄位可以落實。

### 7.2 九個角色是資料不是列舉

規劃書 §4.10 明寫「**角色建立與功能權限勾選**」——所以角色必須是**資料列**。
§6 的九個角色只是 `is_system = true` 的**種子資料**：**不可刪除，但權限可調**；客戶可自建第十個角色。

| `code` | `name_zh` | 備註 |
|---|---|---|
| `super_admin` | 系統管理員 | 全模組全權限 |
| `content_editor` | 內容編輯 | 送審→發布流程中具發布權 |
| `team_manager` | 競技／球隊管理 | |
| `academy_manager` | 學院／課程管理 | 賽事權限限 `scope_type = academy_only` |
| `business` | 商務／贊助 | 商店限 S1／S6 且**訂單個資遮罩** |
| `pr_media` | 公關／媒體 | |
| `support` | 客服／行政 | 會員與商店 S2–S5 |
| `translator` | 翻譯人員 | `scope_type = translate_only` |
| `viewer` | 檢視者 | 唯讀，商店不含金額 |
| **`partner_club_manager`** | **合作球隊管理**（v3.0 新增） | **`scope_mode = own_clubs`**。可維護自家內容、球隊、課程、夥伴贊助與商品訂單；**可存取自家會籍但 `Member` 主檔遮罩**；**無推播、無廣告、無版本憑證、無 `J` 系統管理**。⚠️ **開通前提：資料範圍已落地 ＋ 兩法人間的個資委託處理約定已簽署** |

### 7.3 權限碼命名

`<domain>.<object>.<action>`，例：`shop.order.export`、`member.pii.reveal`、`shop.refund.execute`。

| `Permission` 欄位 | 值域 |
|---|---|
| `module_code` | `A` `B` `C` `E` `F` `G` `H` `I` `J` `K` `L` `P` `S`。⚠️ **v3.0 移除 `N`**（慈善已獨立）。**禁用 `D`（撞 `D1`）／`U`（撞 `U15`）／`O`（形近 `0`）／`M`（App，本檔不含）** |
| `submodule_code` | `B1`–`B6`、`C1`–`C5`、`P1`–`P4`、`E1`–`E6`（**E4–E6 為 App 廣告**）、`F1`–`F2`、`G1`–`G3`、**`J1`–`J4`（v3.0：`J4` 為俱樂部與授權管理）**、`K1`–`K5`、`L1`–`L4`、`S1`–`S6`。⚠️ **v3.0 移除 `N1`–`N7`** |
| `domain` | `content` `faq` `charity` `team` `program` `calendar` `member` `business` `shop` `enquiry` `seo` `system`。⚠️ **v3.0 移除 `donation`**（慈善已獨立） |
| `action` | `view` `create` `update` `delete` `publish` `export` `translate` `execute` `reveal` |
| `is_restricted` | **須額外授權**：會員名單匯出、訂單匯出、K5 winners 匯出、慈善明細匯出、分潤設定 |
| `sysadmin_only` | **僅系統管理員**：`shop.refund.execute`、`shop.credential.*`、**`system.club.*`（v3.0：`J4` 俱樂部與授權管理）**。⚠️ **v3.0 移除 `donation.*`** |

### 7.4 §6 權限矩陣 → 權限碼對照

`RolePermission.scope_type` 用來表達矩陣裡**不是布林**的格子：

| `scope_type` | 意思 | 出現在 |
|---|---|---|
| `all` | 全部 | 預設 |
| `own_teams` | 只有自己負責的隊伍 | 「賽事事件」「梯隊賽事」——**行事曆權限跟隨來源模組** |
| `academy_only` | 只有 `team.type = 'academy'` | 學院／課程管理的球隊欄 |
| `masked` | 可見但個資遮罩 | 商務／贊助的商店欄「訂單個資遮罩」 |
| `translate_only` | 只能寫 `*_i18n` 且 `locale <> 'zh-Hant'` | 翻譯人員全列 |

逐格對照（節錄關鍵格；其餘依同一規則展開）：

| 角色 | 矩陣格 | 權限碼集合 |
|---|---|---|
| 客服／行政 | 商店 **✔ S2–S5** | `shop.inventory.*`、`shop.order.view/update`、`shop.order.recipient.reveal`、`shop.shipment.*`、`shop.refund.view/update`（**不含 `shop.refund.execute`**）、`shop.order.export`（`is_restricted`） |
| 客服／行政 | 會員 **✔ 檢視／處理** | `member.*.view/update`、`member.pii.reveal`、`member.export`（`is_restricted`）、`draw.roster.create/lock`、`draw.winner.update` |
| 商務／贊助 | 商店 **S1／S6（訂單個資遮罩）** | `shop.product.*`、`shop.report.view`（**不含** `shop.order.recipient.reveal`、`shop.refund.execute`、`shop.credential.*`） |
| 學院／課程管理 | 球隊 **學院梯隊** | `team.*.update` with `scope_type = academy_only` |
| 內容編輯 | 行事曆 **自建事件** | `calendar.custom_event.*`（**不含** `calendar.match.update`） |
| 內容編輯 | 商店 **S1 文案／圖** | `shop.product.update`（**不含** `shop.variant.price.update`、`shop.variant.cost.view`） |
| 翻譯人員 | 全列 **僅翻譯欄位** | `*.translate` with `scope_type = translate_only` |
| 檢視者 | 商店 **唯讀（不含金額）** | `shop.product.view`（**不含** `shop.variant.cost.view`、`shop.report.view`） |

### 7.5 遮罩與「額外授權」怎麼表達

**不另建表。**

- **遮罩** ＝ 缺少對應的 `*.reveal` 權限碼：`member.pii.reveal`、`order.recipient.reveal`、`draw.pii.reveal`、`donor.pii.reveal`。無此碼者，API 層一律回傳遮罩值（`a***@gmail.com`）。
- **額外授權** ＝ `is_restricted = true` 的權限碼 ＋ **執行當下的二次驗證**（重輸密碼或 2FA）。二次驗證屬應用層流程，**不落資料表**。

> ⚠️ 規劃書多處承諾「匯出須寫入稽核日誌（誰、何時、幾筆、用途）」。**本版無稽核表故無法兌現**，見 [§13.1](12-database-schema.md#131-沒有稽核與登入日誌表)。

### 7.6 管理員用 `username` 不用 Email

| 項目 | 規則 |
|---|---|
| **登入識別** | **`AdminUser.username`，UNIQUE。這是唯一的登入查詢鍵** |
| `AdminUser.email` | 🔒 **僅供系統通知與密碼重設。不設唯一索引、不得作為登入查詢鍵、可為空** |
| 種子超級管理員 | `username` = **`sa@system.local`**（**它長得像 Email，但存在 `username` 欄，不是 Email**）；`display_name` = `Super Admin`；`is_super_admin = true` |
| 種子密碼 | **`Admin@123`**，以**雜湊儲存**（演算法待選型，優先 Argon2id，次選 bcrypt）。`must_change_password = true`，**首次登入強制更換** |
| 密碼政策 | J 模組要求，以 `password_changed_at` 支援到期強制更換 |
| 2FA | `two_factor_enabled`／`two_factor_secret_encrypted`／`two_factor_confirmed_at`。§8 非功能性需求明訂**後台強制 2FA** |
| 登入失敗鎖定 | `failed_attempt_count` ＋ `locked_until`。**這是狀態欄位不是日誌表** |
| 最後登入 | `last_login_at` **單一欄位**，取代規劃書 J 的「登入紀錄」表 |
| 系統保護 | 至少保留一筆 `is_super_admin = true` 且 `status = 'active'` 的帳號，**不可全數停用** |

> ⚠️ **前台 `Member.email` 是另一件事**，會員維持 Email ＋ LINE 一鍵登入不變（**不用 Google**）。兩套帳號系統**完全獨立，不共用表、不共用登入**。

---

## 8. 受限與加密欄位盤點

分三級。**分級決定的是儲存方式，可見範圍由 [§7.5](#75-遮罩與額外授權怎麼表達) 的 `*.reveal` 權限碼決定。**

| 級 | 意思 | 儲存 |
|---|---|---|
| 🔒 遮罩 | 明文存，讀取時依權限遮罩 | 一般欄位 |
| 🔐 加密 | 加密存，解密須權限 | 應用層加密，金鑰不入庫 |
| ⚖️ 法定保存 | 稅法／個資法要求保留，**優先於刪除帳號請求** | 刪帳號時只清其餘欄位，**列不刪** |

| 表 | 欄位 | 級 | 完整值可見角色 | 出處 |
|---|---|---|---|---|
| `Member` | `email`、`phone`、`birth_on` | 🔒 | 系統管理員、客服／行政 | 行 1320 |
| `Member` | `line_user_id_encrypted` | 🔐 | 系統管理員 | 行 1282 |
| `JerseyIssue` | `recipient_name`、`address` | 🔒 | 系統管理員、客服／行政、出貨角色 | K3 |
| `Order` | `recipient_name`、`recipient_phone`、`recipient_address` | 🔒 ⚖️ | 系統管理員、客服／行政、出貨角色 | 行 1327 |
| `OrderItem`／`StoreInvoice` | 全表 | ⚖️ | — | 稅法保存，年限待確認第 28 點 |
| `StoreInvoice` | `carrier_id_encrypted` | 🔐 | 系統管理員 | S6 |
| `ProductVariant` | `cost` | 🔒 | 系統管理員、商務／贊助 | S1 |
| `PaymentChannel` | `credential_encrypted` | 🔐 | **僅系統管理員** | S6／N7 |
| `DrawRoster` | `name_snapshot` | 🔒 | 系統管理員、客服／行政（公關只拿遮罩版） | 行 1322 |
| `DrawRoster` | `withholding_data_encrypted` | 🔐 ⚖️ | **僅系統管理員**，達扣繳門檻才蒐集 | 行 1142–1155 |
| `Donation` | `donor_name`、`donor_email` | 🔒 ⚖️ | 系統管理員、客服／行政 | 慈善站 N3 |
| `DonationInvoice` | `carrier_id_encrypted`、`tax_id` | 🔐 ⚖️ | 系統管理員 | 慈善站 §5 |
| `Registration`／`Enquiry`／`EnquiryAnswer` | 姓名、電話、Email、生日 | 🔒 | 依模組權限 | 行 1320 |
| `NewsletterSubscriber` | `email` | 🔒 | 系統管理員、公關／媒體 | G3 |
| `AdminUser` | `password_hash`、`two_factor_secret_encrypted` | 🔐 | **不可讀取，僅比對** | J |
| `AdminUser` | `email` | 🔒 | **不作登入鍵**，僅通知用 | 本檔決定 |

**個資保存期限未定**：規劃書要求七類表單顯示保存期限說明，但實際年限未定（待確認第 15 點，法遵項目，**擋表單上線**）。交易紀錄的年限待會計師確認（第 28 點）。

---

## 9. 快照、視圖與不可變資料

三條結構原則的完整論證。**違反其中任何一條，錯誤都不會立刻顯現，而是在對帳或客訴時才爆。**

### 9.1 值複製快照：讀取不得回頭 join

適用：`OrderItem`、`DrawRoster`、`SettlementLine`、`StoreInvoice`、`DonationInvoice`。

| 情境 | 若做成外鍵解析會怎樣 |
|---|---|
| 商品調價 | 半年前的訂單顯示今天的價格，**對帳永遠對不起來** |
| 商品改名或下架 | 客訴時舉不出當初賣的是什麼 |
| 會員改名 | 已鎖定的抽獎名單被改動，**開獎失去稽核能力** |
| 會員合併或刪帳號 | 名單少一列或序號斷號，**無法證明開獎當下的名單** |
| 分潤百分比調整 | 已結算期間的金額被回頭改寫 |

**實作規則**：快照表可保留來源外鍵（如 `OrderItem.product_variant_id`）**僅供追溯**，但**所有讀取路徑都必須用快照欄位**。程式碼審查時看到 `orderItem.variant.name` 一律視為錯誤。

### 9.2 `CalendarEvent` 是彙整層不是資料源

| 情境 | 正確做法 |
|---|---|
| 賽事改期 | 改 `Match`，行事曆自動反映 |
| 行事曆上拖曳改期（L1） | **寫回 `Match`**，不寫行事曆 |
| 俱樂部自建活動 | 寫 `CalendarCustomEvent`——這是行事曆**唯一的自有資料** |
| 課程梯次 | **永不進入**。App 的「我的報名」可顯示梯次時間，但不得寫入 `CalendarEvent`，也不得出現在賽程分頁 |
| 試訓 | `source_type = 'trial'`，由 L3 開關決定，**預設關閉** |

複製一份賽事資料到行事曆 ＝ **製造兩個真實來源，必然不同步**。

### 9.3 不可變名單：`DrawRoster` 的鎖定語意

| 階段 | 允許的操作 |
|---|---|
| `draft` | 尚未產生名單 |
| `roster_locked` | **一次性寫入完成**。此後**不得新增、不得刪除任何列** |
| `drawn` | **只有 `is_winner`／`prize_name`／`claim_method` 可回填** |
| `announced` | 加上 `fulfilment_status` 可更新；公布後的修改須附理由 |
| `closed` / `voided` | 唯讀。作廢版本**保留不刪**，重產時 `roster_version` +1 |

**有誤只能整份作廢重產**，不能局部修補——局部修補會讓序號與 `roster_hash` 對不上。

---

## 10. 批次匯入與匯出對應

### 10.1 CSV 匯入（規劃書明確要求）

| 項目 | 落到哪張表 | 出處 |
|---|---|---|
| 整季賽程 | `Match`（＋`MatchTeam`、`match_i18n`） | C4／L4 共用機制 |
| 積分榜 | `Standing` | C4 |
| FAQ 題目 | `Faq`＋`FaqCategoryLink`＋`faq_i18n`（匯入 ＋ 匯出） | B4 |
| 301 轉址對照 | `Redirect`。**含舊 Wix 商店的 5 個商品與分類網址** | H |
| 物流單號 | `Shipment.tracking_no`。**v2.6 不串物流商 API，以 CSV 回填** | S4 |
| 店家名單 | `DonationStore`（＋`donation_store_i18n`） | 慈善站 N1 |

> ⚠️ 目前 [`../content/migration/舊官網URL盤點.csv`](../content/migration/舊官網URL盤點.csv) 的「客戶決定」與「新站對應頁面」兩欄**全空**，301 對照表尚無法產生。

### 10.2 匯出

| 項目 | 來源 | 額外授權 |
|---|---|---|
| 報名名單 Excel、簽到表 | `Registration` + `Session` | |
| Lead 名單 CSV | `Enquiry`（`form_code = 'proposal_download'`） | |
| 詢問 CSV | `Enquiry` + `EnquiryAnswer` | |
| **會員 CSV** | `Member` | ✔ `is_restricted` |
| 球衣出貨清單 CSV | `JerseyIssue` | ✔ |
| 續會名單 CSV | `Member` + `MembershipPayment` | ✔ |
| 賽事 CSV／.ics | `Match` + `CalendarEvent` | |
| **抽獎名單 CSV（兩種）** | `DrawRoster`：公開遮罩版／受限中獎人版 | 受限版 ✔ |
| **訂單 CSV** | `Order` + `OrderItem` | ✔ |
| 商店報表 | `Order` 彙總 | ✔ |
| 發票會計 CSV | `StoreInvoice`／`DonationInvoice` | ✔ |
| 對帳單 ＋ CSV | `Settlement` + `SettlementLine` | ✔ |
| **QR 批次 zip** | `DonationStore`（PNG／SVG／印刷版 PDF） | |

---

## 11. 索引與唯一鍵建議

邏輯層寫法。**DBMS 已定為 Azure SQL**（見 [`17-deployment.md`](17-deployment.md)），以下為隨之而來的兩條實作規則：

- 🔴 **每張表另加一欄不對外的 `bigint IDENTITY` 當叢集鍵，主鍵 `id uniqueidentifier` 設為非叢集。**
  SQL Server 的 `uniqueidentifier` 比較位元組的順序是反的，連 UUIDv7 也拿不到索引區域性。
  理由與替代方案的取捨見 [`12` §1.2](12-database-schema.md#12-主鍵外鍵與命名慣例)。
- **可為空 `club_id` 的複合唯一鍵直接用 `UNIQUE (club_id, slug)` 即可**——SQL Server 的唯一索引把 NULL 當成相等，**不需要篩選唯一索引**。
  `(NULL,'about')`、`(1,'about')`、`(2,'about')` 允許併存；網址對應哪一筆由**路由優先順序**（俱樂部專屬優先、回退共同）解決，另加索引 `(slug, club_id)`。見 [`12` §1.4](12-database-schema.md#14-dbms-相依的五件事已定案) 第 5 件。

覆蓋索引與篩選索引的細部調校仍屬實作階段，不在此指定。

### 11.1 唯一鍵（違反即資料錯誤）

| 表 | 唯一鍵 |
|---|---|
| `Team` | `code` |
| `Member` | `member_no`、`email` |
| `MemberCard` | `token` |
| `AdminUser` | **`username`**（**`email` 不設唯一**） |
| `AdminRole` / `Permission` | `code` |
| `ProductVariant` | `sku` |
| `Order` | `order_no`、`lookup_token` |
| `DonationStore` / `DonationProject` | `store_slug` / `project_slug` |
| `Donation` | `order_no` |
| `PaymentChannel` | `(subject, channel_type, environment)` |
| `DrawRoster` | `(member_draw_id, serial_no)`、`(member_draw_id, member_no_snapshot)` |
| `Locale` | `code` |
| `Redirect` | `from_path` |
| `PressResource` | `slug` |
| 所有 `*_i18n` | `(<entity>_id, locale)` |
| 所有內容表 | `slug`（表內唯一） |

### 11.2 查詢索引

| 表 | 索引 | 用途 |
|---|---|---|
| `Article` | `(status, published_at desc)`、`(article_category_id, published_at desc)` | 新聞列表與分類頁 |
| `Match` | `(season_id, match_on)`、`(status, match_on)` | 賽程／賽果切換 |
| `CalendarEventTeam` | `(team_id, source_type)` | 行事曆隊別篩選 |
| `Registration` | `(session_id, status)`、`(member_id)` | 報名管理與我的報名 |
| `Order` | `(member_id, created_at desc)`、`(order_status)`、`(payment_status, created_at)` | 我的訂單、出貨佇列、逾時清理 |
| `OrderItem` | `(order_id)` | |
| `InventoryMovement` | `(product_variant_id, occurred_at desc)` | 庫存異動查詢 |
| `Donation` | `(status, paid_at)`、`(donation_store_id, paid_at)`、`(donation_project_id, paid_at)` | N4 結算與 N6 報表 |
| `SettlementLine` | `(settlement_id)`、`(donation_id)` | 沖回對應 |
| `DrawRoster` | `(member_draw_id, serial_no)` | 名單匯出 |
| `EmailLog` | `(member_id, sent_at desc)`、`(type, sent_at)` | |
| `Enquiry` | `(form_id, status, created_at desc)`、`(assignee_admin_user_id)` | 收件匣 |
| 所有 `*_i18n` | `(locale)` | 翻譯狀態矩陣 |

### 11.3 外鍵刪除行為

| 關係 | 行為 |
|---|---|
| 母表 → `*_i18n` | `CASCADE` |
| `Order` → `OrderItem`／`Shipment`／`StoreInvoice` | **`RESTRICT`**（⚖️ 法定保存，不得刪） |
| `MemberDraw` → `DrawRoster` | **`RESTRICT`**（不可變名單） |
| `Member` → `Order`／`Registration` | **`SET NULL`**（刪帳號後訂單與報名仍在） |
| `Product` → `OrderItem` | **無外鍵約束的刪除行為**——`OrderItem` 是快照，商品下架不影響歷史訂單 |
| `Settlement` → `SettlementLine` | `RESTRICT` |
| `Cart` → `CartItem` | `CASCADE` |

---
