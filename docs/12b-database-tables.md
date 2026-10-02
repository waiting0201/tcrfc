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
| `club_id` | **必填**。磐石與藍鯨各自的隊伍 |
| `code` | **UNIQUE（全站唯一，不得改成 `(club_id, code)`）**。值域 `D1`／**`BW1`**／`U15`／`U14`／`U12`。**沒有 `D2`** |
| 為什麼不加複合鍵 | `code` 是行事曆訂閱網址與 `/schedule/d1/` 的識別鍵，**已在外流通**。藍鯨一線隊是 `BW1` 不是第二個 `D1` |
| `type` | `first_team`（**每俱樂部至多一筆**）／`academy`。⚠️ **`women` 值已廢除** |
| `gender` | **`men`／`women`／`mixed`**（v3.0 新增，取代 `type` 的 `women`） |
| 對外顯示 | `D1` 是代號，前台一律顯示 `First Team / 一線隊`。🔴 **兩隊都有「一線隊」，任何同時呈現兩隊的畫面每張卡片都必須標球隊** |

### 6.2 `Club`

| 項目 | 規則 |
|---|---|
| 定位 | **俱樂部主檔**（後台 `J4`）。**它自己不帶 `club_id`** |
| `code` | UNIQUE |
| `domain` | UNIQUE。每個俱樂部一個獨立網域，前台站台以此對應 |
| 標誌 | 需**點陣高倍圖（@2x／@3x）與深色版**——網頁用 SVG 即可，但 App 需要點陣資源 |
| 邊界 | 🔴 **`Club` 不是第六種商業對象**。它是內容主體，不計曝光、無金流、無分潤，**不與 `Partner`／`Sponsor` 合併** |
| 刪除 | **`RESTRICT`**——有任何帶 `club_id` 的資料就不得刪 |

### 6.3 `Member`

| 欄位 | 值域與約束 |
|---|---|
| `club_id` | 🔴 **刻意不加**。Email 是登入鍵（加了等於一個人要記兩組密碼、做兩次 LINE 綁定、走兩次刪帳號流程）；`line_user_id` 與 LINE 帳號 1:1 複製不了；**個資法上的當事人是「人」不是「會籍」**。俱樂部維度在 `Membership` |
| `member_no` | UNIQUE。格式待確認（主站待確認事項） |
| `email` | UNIQUE，🔒 受限。**前台登入識別**（與後台 `AdminUser.username` 無關） |
| `signup_source` | `web`／`line`／`admin`／`app`。**不含 `google`**——不採用 Google 登入 |
| `line_user_id_encrypted` | 🔒 加密儲存，**不得匯出** |
| `line_user_id_hash`（E 批） | LINE userId 的 SHA-256 小寫十六進位（`char(64)`）。**篩選唯一索引** `WHERE line_user_id_hash IS NOT NULL`（一個 LINE 帳號只能綁一個會員）；只供「用 LINE 帳號找會員」，不得匯出 |
| `failed_attempt_count`／`locked_until`（E 批） | 登入失敗次數與鎖定到期時間（規劃書 §3.14「登入失敗次數限制」）；連續 5 次失敗鎖 15 分鐘，成功登入歸零。與 `admin_users` 同一套機制 |
| `password_hash` 的 `!` 前綴（E 批） | 以 `!` 開頭＝**尚未設定密碼**（LINE 註冊者；被合併的帳號也用 `!merged-…`），`PasswordHasher.Verify` 一律回 false。「至少保留一種登入方式」（規劃書 §3.14）據此判斷：解除 LINE 綁定前須已設定密碼 |
| `status` | `active`／`suspended`／`deleted`。**「未驗證」不是狀態值**：`status = 'active'` 且 `email_verified_at IS NULL`（K1 畫面推得） |
| `email_verified_at`／`last_login_at`／`internal_note`／`locale`（B1） | Email 驗證時間（空＝未驗證）／最後登入（取代登入紀錄表）／後台內部備註／語系偏好（`zh-Hant`／`en`，可空） |
| `merged_into_member_id`（B1） | 合併帳號的去向。被合併的帳號 `status = 'deleted'`、個資清除（Email 換成 `merged-<編號>@merged.invalid`）、指向保留帳號；**不建日誌表** |
| 🔴 **已移出的三欄** | **`tier`／`membership_start_on`／`membership_end_on` 已移入 `Membership`**。看到還寫在 `Member` 上的是舊規格 |
| 刪帳號 | **欄位清除不是刪列**：保留 `member_no` 與遮罩姓名，其餘個資清除。稅法要求保留的訂單與發票**優先於刪除請求** |

### 6.4 `Membership`（v3.0 新增）

| 項目 | 規則 |
|---|---|
| 唯一鍵 | **`(member_id, club_id, season_id)`**——**一人每俱樂部每季一份** |
| `club_id` | **必填**（承載個資與金流歸屬，絕對不可為空） |
| `tier` | `registered`（免費）／`fan_club`（付費＝球迷會員） |
| 計期 | **球季制**，同一俱樂部的會籍全體同時到期。**兩隊球季不同步**（`Season` 也帶 `club_id`） |
| `membership_start_on`／`membership_end_on` | 由 `MembershipPayment` 開通時寫入 |
| `status`（B1） | `pending`（升級申請待確認）／`active`／`expired`／`cancelled`。**有效與否以到期日為準**：`active` 但 `membership_end_on` 已過的，後台一律顯示為已到期（`effectiveStatus`），`status` 欄是人工覆寫（取消、批次到期） |
| `membership_plan_id`（B1） | 目前方案（可空：免費會籍沒有方案）；歷次付款仍各自記在 `membership_payments.membership_plan_id` |
| `last_adjust_reason`／`last_adjusted_at`（B1） | K2「手動調整層級」的異動原因，**只留最近一次**（不建異動歷程表） |
| 抽獎資格 | **算出來的布林值**：該俱樂部的 `tier = 'fan_club'` AND `membership_end_on >= snapshot_at` AND `Member.status = 'active'`。**沒有欄位、沒有表** |
| 雙會籍 | 同時持有兩隊付費會籍者**在兩份抽獎名單各佔一號**，活動辦法須明示可分別參加 |
| 刪除 | `Member` → `Membership` 是 **`RESTRICT`**——會籍涉金流與發票，刪帳號不得連帶刪會籍 |

### 6.5 `MemberCard`

| 項目 | 規則 |
|---|---|
| `membership_id` | 🔴 **必填——每份會籍一張卡**。持兩隊會籍者有兩張卡，各帶該俱樂部標誌與品牌色 |
| `club_id` | **必填**（值複製自 `Membership`，供後台過濾與卡面品牌） |
| 列數 | **一張卡一列**，數量上限為 `MembershipPlan.card_quota`（家庭方案可為 3） |
| `token` | UNIQUE，**不可由 `member_no` 推導**。公開驗證頁 `/m/<token>` 使用 |
| 唯一性 | **一張卡只有一組 token**，官網驗證頁與 App 內卡片共用。發兩組＝兩份可撤銷狀態，撤銷必漏一邊 |
| 驗證頁欄位 | 🔴 **不得新增「適用球隊」欄位**——token 已隱含俱樂部。「一張卡標示兩種會籍」不可採 |
| 折扣使用 | 到店**出示卡片目視即可**。**不核銷、不計次、店家不需系統**——所以沒有 `redemption` 任何表 |
| `status`／`revoked_at`（B1） | `active`／`revoked`。會籍被取消時其會員卡一併停用；「重新產生 QR」是**換 `token`、`reissue_count` +1**（同一張卡、舊憑證立即失效），後台永不回傳 `token` 本身 |

### 6.5b K 模組其餘表的 B1 補充

| 表 | 規則 |
|---|---|
| `MembershipPlan` | `(club_id, season_id, code)` 唯一；`status`：`draft`（下架）／`published`（上架）；有會籍或付款紀錄使用時不可刪除、不可換球季 |
| `MembershipPayment` | 手動開通時寫入：`club_id`＝**受益俱樂部**（目前操作的俱樂部）、`collecting_club_id`＝**收款主體俱樂部**（`clubs.is_collecting_subject = 1`，代收代付）；`method`：`linepay`／`onsite`；`handled_by`＝經辦人 |
| `MembershipBenefit` | **不帶 `club_id`**，靠父表方案；`benefit_group`：`member_card`／`store_discount`／`jersey`／`event`；`status`：`draft`／`published`；側表 `name`／`description`／`group_label`（由分組代碼自動帶入雙語）／`free_value`／`paid_value` |
| `JerseyIssue` | `membership_id`（可空，舊資料）；`status`：`pending`／`shipped`／`received`；`delivery_method`：`ship`（寄送，須有電話與地址）／`pickup`（到場領取，不能標「已寄出」）；件數不得超過方案 `jersey_quota` |
| `PartnerStore` | `club_id` 可為空＝兩隊共同（**只有系統管理員能編輯**）；`applicable_tier`：`all`／`fan_club`；`status`：`draft`／`published`；`address` 存中文地址、`partner_stores_i18n.address` 只存英文；`business_hours` 是 `json` 欄位，後台把自由文字存成 **JSON 物件 `{"text":"…"}`**（⛔ 不是字串純量——原生 `json` 型別只收物件與陣列，`docs/18` `E-111`；讀取端相容舊的字串純量，對外 API 仍是純文字）；`lat`／`lng` 成對、人工確認後儲存（不做即時 geocoding） |
| `Trial`（P4） | `status`：`開放`／`額滿`／`候補`／`已結束`；`enrolled_count` 由報名狀態原子調整（待確認／已確認／已繳費／完成佔名額，取消與候補不佔）；達 `capacity` 時「開放」單向轉「額滿」 |
| `CalendarTeamSetting`（L3） | `team_id` UNIQUE；`colour`／`sort_order` 空＝沿用 `teams.team_color`／`teams.sort_order`；`is_public = 0` 的隊別不出現在前台選單，其訂閱 feed 回 404 |

### 6.5c 會員前台新增的兩張表與一個欄位（E 批，2026-10-01）

| 表／欄位 | 規則 |
|---|---|
| `member_refresh_tokens` | 欄位同 §7.7（`token_hash` 唯一、`issued_at`／`expires_at`／`revoked_at`／`replaced_by_id`），另加 **`is_persistent`**（「記住我」：Cookie 帶到期日、較長效期；否則為工作階段 Cookie）。**輪替與重放偵測規則同 §7.7**：每次續期撤銷舊的、發新的；已撤銷的權杖再被使用 → 撤銷該會員全部有效權杖。**變更密碼、重設密碼、「登出全部裝置」都撤銷全部**（App 規劃書 §4.3）。⚠️ 與 App 的更新權杖鏈（`app_devices` 四欄）互不相干 |
| `membership_orders` | **唯一鍵**：`order_no`；`(member_id, idempotency_key)`。**`status`**：`created`／`pending_payment`／`paid`／`activated`／`expired`／`activation_failed`／`cancelled`／`refunded`（App 規劃書 §5.3 七個狀態＋「建立訂單」）。`created` 也是網頁的「升級申請待確認」（主站 §3.14：客服核對款項後在 K2 開通）；`pending_payment` 才有 `expires_at`（請款後 15 分鐘）、`payment_transaction_id`、`payment_url`。**`amount` 只由伺服器依 `membership_plan_id` 的 `fee` 計算**，用戶端傳的金額一律忽略。`activation_source`：`payment`（付款確認後）／`internal`（內部端點）／`admin`（客服在 K2 開通了同一份申請）。**同一會員同一方案同時只有一張未完成（`created`／`pending_payment`）的訂單**（應用層強制） |
| `membership_payments.membership_order_id` | 可空；有值時**篩選唯一**——一張訂單最多一筆付款紀錄，重複開通在資料庫層就寫不進去（冪等的最後一道保險）。手動開通（K2）仍為空 |

### 6.6 `MemberDraw` / `DrawRoster`

| 項目 | 規則 |
|---|---|
| `club_id` | **兩張都必填**。**各俱樂部各自舉辦抽獎** |
| `MemberDraw.status` | `draft`／`roster_locked`／`drawn`／`announced`／`closed`／`voided` |
| `snapshot_at` | 資格基準時間。名單於此刻**一次性寫入**，會員無法自行建立 |
| `roster_version` | 名單版本。有誤只能**整份作廢重產**（版本 +1），**舊版保留不刪**。**C1：`draw_rosters.roster_version` 標明每列所屬版本，唯一鍵含版本；版本歷程（基準時間、合格人數、雜湊、產生人、作廢時間與原因）在新表 `draw_roster_versions`，作廢版本不得刪除** |
| `roster_hash` | 名單雜湊，供開獎當下的完整性佐證 |
| `DrawRoster.serial_no` | **依 `member_no` 升冪連號配發，一人一號**。`(member_draw_id, roster_version, serial_no)` UNIQUE（C1：含版本）。鎖定後不得重排 |
| 快照欄位 | `member_no_snapshot`／`name_snapshot`／`tier_snapshot`／`membership_end_on_snapshot` 皆**值複製**，母表變動不追溯 |
| `is_winner`／`is_backup` | **後台人工回填**（以序號為準）——系統不抽出，實體開獎在現場或直播進行；`is_backup`＝備取，遞補＝同序號改為中獎（C1） |
| 獎品發放（C1） | `claim_method`（`ship`／`pickup`）、`recipient_name`／`recipient_phone`／`recipient_address`（🔒 遮罩）、`fulfilment_status`（`pending`／`shipped`／`claimed`，**逾期＝待處理且已過 `claim_deadline_on`，讀取時換算**）、`shipped_at`／`claimed_at`，比照 K3 球衣 |
| `withholding_data_encrypted` | 🔒 **僅達扣繳門檻時蒐集**，加密、預設遮罩 |
| 通知 | **不發中獎通知信、不推播**。中獎只以最新消息公布（7.1 ＋`球迷會員抽獎` 標籤，遮罩） |

### 6.7 `Order`

| 欄位 | 值域與約束 |
|---|---|
| `club_id` | **必填** |
| **`selling_club_id`** | **受益方**（v3.0）——這筆訂單的營收算誰的 |
| **`collecting_club_id`** | **收款法人**（v3.0）。藍鯨採**代收代付**，收款方仍是俱樂部；**系統不做分潤計算**，只記歸屬並提供加總匯出 |
| `order_no` | UNIQUE（**維持全站唯一**）。加俱樂部前綴以利對帳，**不改鍵** |
| `member_id` | **可為空**——非會員可結帳。**這條不得更動** |
| `lookup_token` | UNIQUE。非會員訂單查詢用（`/zh/order/lookup/`） |
| `payment_status` | `pending`／`paid`／`failed`／`expired`／`refunded`。**逾時未付款自動取消並釋回庫存** |
| `order_status` | `待付款`／`已付款`／`備貨中`／`已出貨`／`已完成`／`已取消`／`退貨處理中`／`已退款` |
| `delivery_method` | `home_delivery`／`cvs_pickup`／`onsite_pickup` |
| `shipping_fee` | **單一固定運費**，免運門檻另存 `Setting`。**沒有級距、沒有重量計費** |
| `is_manual` | 現場銷售補登（S3），退款人工執行並記 `handled_by` |
| `payment_method`（C1） | `linepay`／`onsite`（現場收款）。現場收款訂單建立即已付款、直接售出扣庫存 |
| `settlement_status`（C1） | `pending`／`settled`，附 `settled_on`／`settlement_note`——**代收代付的人工分帳旗標，不是狀態機**；系統不計算應付金額、不產生結算單 |
| `customer_note`／`internal_note`／`completed_at`／`cancelled_at`／`cancel_reason`（C1） | 顧客備註、內部註記、完成與取消時間與原因 |
| **`buyer_email`**（F 批） | 非會員結帳的買家 Email（規劃書「填 Email 即可」）。訂單成立信、`/order/lookup` 的「訂單編號＋Email」比對依據；後台人工建單可為空。🔒 視同會員個資 |
| **`idempotency_key`／`request_fingerprint`**（F 批） | 前台結帳冪等：`(club_id, idempotency_key)` **篩選唯一**。指紋＝擁有者（會員 id 或購物車權杖雜湊）＋請求內容的 SHA-256；**不含購物車內容**（成功後購物車已清空，重送仍須回原訂單）。後台人工建單兩欄為空 |
| **`payment_url`**（F 批） | LINE Pay 請款後的付款網址，只在待付款期間有值；請款期間 `linepay_transaction_id` 暫存 `RESERVING` 作為並行搶佔旗標 |
| 收件人三欄 | 🔒 **視同會員個資**：完整值僅系統管理員、客服／行政與出貨角色可見 |
| 不存在的欄位 | `discount_code`、`member_price`、`points_used`、`card_no`、`shipping_tier` ——**一律沒有** |

> 🔴 **`Cart.club_id` 必填，不得跨俱樂部混買**，切換站台即切換購物車。
> **購物車（F 批）**：`(club_id, member_id)` 篩選唯一（一個會員在一個俱樂部最多一台）；訪客購物車以 `anonymous_token` ＝**權杖的 SHA-256**（篩選唯一）定位，權杖本身不落庫、遺失無法補發；`cart_items` 只存「規格＋數量」，**不存價格快照**（價格與可售量每次顯示都重新計算）。超過 30 天沒動過的訪客購物車由維護作業清除。
> ⚠️ **「訂單是否於結帳時依俱樂部拆單」尚未定案**（`STATUS.md` B-8，繫於代收代付的稅務認定）。
> 現行禁止混買故不會發生，**開放混買前必須先答**。

### 6.8 `OrderItem`（📸 快照）

| 項目 | 規則 |
|---|---|
| `club_id` | **必填，值複製自 `Order`**。🔴 **快照表的 `club_id` 絕對不可為空**——快照的意義是凍結歸屬，NULL 是「未知」不是「共同」 |
| 快照四欄 | `product_name_snapshot`／`variant_label_snapshot`／`sku_snapshot`／`unit_price_snapshot`，建單當下**值複製** |
| `product_variant_id` | **僅供追溯**。讀取訂單時**不得回頭 join 取名稱與價格** |
| 理由 | 商品改名、改價、下架**都不得改動歷史訂單**，否則對帳與客訴舉證失去依據 |
| 同類 | `DrawRoster`、`StoreInvoice` 適用同一原則 |

### 6.9 `StoreInvoice`

| 項目 | 規則 |
|---|---|
| `club_id` | **必填，值複製自 `Order`** |
| 抬頭 | **俱樂部**。與協會發票**分屬不同字軌**，不得共用 |
| 三選一 | `carrier_type`＋`carrier_id_encrypted`（載具）／`tax_id`（統編）／`donation_code`（捐贈碼）——**恰有一組非空**。`carrier_type` 值域：`mobile_barcode`（手機條碼）／`citizen_cert`（自然人憑證）。**`carrier_id_encrypted` 為 `nvarchar(500)`**（F 批放寬，Data Protection 密文長度） |
| 建立時機（F 批） | **結帳時**建一列 `issue_status = pending`，只記顧客選的方式；**付款確認後**才呼叫 `IInvoiceIssuer` 開立。失敗記 `failed`＋`retry_count`，由重試作業補開（次數與間隔用 S6「發票重試」設定）。**沒付款而取消／逾時的訂單，未開立的資料列由維護作業清掉** |
| `issue_status` | `pending`／`issued`／`failed`，失敗可重試（`retry_count`） |
| `void_status` | `none`／`voided`（作廢）／`allowance`（折讓）。**退貨必須作廢或折讓** |
| 前提 | **LINE Pay 本身不開發票**，須另接發票服務（`STATUS.md` B-10） |

### 6.10 `PaymentChannel`

| 項目 | 規則 |
|---|---|
| **`owner_club_id`** | 🔴 **v3.0 取代原本的 `subject enum(club, association)`**。主站只會有**俱樂部一列** |
| 唯一鍵 | **`(owner_club_id, channel_type, environment)`** UNIQUE |
| `channel_type` | `linepay`／`einvoice` |
| `environment` | `sandbox`／`production` |
| 憑證 | 🔒 加密儲存，**僅系統管理員可見**（S6）。輪替記 `rotated_at` |
| 協會的憑證 | **不在本庫**——慈善平台是獨立資料庫，見 [`16-charity-schema.md`](16-charity-schema.md)。🔴 **三方的 LINE Pay 商店號一律不得共用** |
| 風險 | **填錯商店號＝款項進錯法人**，同時踩稅務與《公益勸募條例》 |
| 出口 IP | ⚠️ 正式環境須於商店管理後台登記**付款伺服器的出口 IP**，見 [`17-deployment.md`](17-deployment.md) §3 |

### 6.11 `CalendarEvent`（視圖）

| 項目 | 規則 |
|---|---|
| 實作 | **第一期用一般 VIEW**，以 `source_type` + `source_id` 指向來源，**不複製資料**。`club_id` 由來源推導不另存 |
| ⚠️ 升級路徑 | 🔴 **SQL Server 的 indexed view 明文禁止 `UNION`／`UNION ALL`**——沒有 materialized view 這條路。效能不足直接走索引表 ＋ 寫入時同步 |
| `source_type` | `match`（來源 C4）／`trial`（**L3 開關，預設關閉**）／`custom`（來源 `CalendarCustomEvent`） |
| 隊別分類 | `CalendarEventTeam` 關聯表（`team_codes[]` 的實作），第一層分類 |
| **永不進入** | **`Session` 課程時段**——行事曆以比賽為核心，學院課程、營隊、專項訓練不納入 |
| 權限 | **跟隨來源模組**：能編輯哪些事件取決於對賽事資料的權限 |

> **S1-11 新增（2026-09-25）**：本輪只做**合併讀取**（`GET /api/v1/admin/{club}/calendar/events`
> 與公開端點 `GET /api/v1/{club}/calendar/events`），不是把這個 VIEW 本身接上程式碼——兩支端點都
> 是後端分別查詢 `matches`／`calendar_custom_events` 兩張來源表後在應用層合併，理由是這個 VIEW
> 欄位過少（沒有標題、隊別、賽事系列名稱），撐不起畫面需要的欄位，直接查來源表反而更直接。
> `CalendarCustomEvent.repeat_rule`／新增的 `repeat_until` 欄位由
> `apps/api/Common/RecurrenceExpander.cs` 在讀取當下即時展開，不 materialize 成事件實例
> （docs/12-database-schema.md §12 第 39 點）。權限碼：`calendar.view`（L1 總覽，一律
> `scope_type=all`，賽事本身已公開，見 [`14-invariants.md`](14-invariants.md)「own_teams 盤點
> 結果」）、`calendar.custom_event.view/create/update/delete`（L2，`module_code=L`，
> `domain=calendar`）。

### 6.12 `AdminUser`

見 [§7.6](#76-管理員用-username-不用-email)。⚠️ **`primary_club_id` 只是站台切換器的預設值，不是資料範圍**——範圍在 `AdminUserClub`。

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

### 7.2 十個角色是資料不是列舉

規劃書 §4.10 明寫「**角色建立與功能權限勾選**」——所以角色必須是**資料列**。
§6 現列**十個角色**（v3.0 新增合作球隊管理），全部是 `is_system = true` 的**種子資料**：**不可刪除，但權限可調**；客戶可再自建更多角色。

> ⚠️ **`code` 已於 2026-09-23 改為與實際種子一致**（`S1-3` 登入與權限地基，`db/seed/generate-club-seed-sql.py` §18.1）。舊版本表用的是 `super_admin`／`team_manager`／`academy_manager`／`business`／`support` 五個代碼，**規劃書本身沒有寫代碼，只有中文角色名稱**，這五個是本檔早期自行擬定、從未真的建過表；種子腳本改採**與慈善庫（`db/seed/generate-charity-seed-sql.py`）已上線的九個代碼對齊**，理由是兩庫角色代碼一致、日後合併報表或人工比對不必再做一次轉換表。`content_editor`／`pr_media`／`translator`／`viewer`／`partner_club_manager` 五碼本來就與種子一致，未變。下表已更新為與種子相同的十個代碼，並逐一核對規劃書 §6（1601–1610 行，十列角色矩陣）的中文角色名稱與 `scope_mode` 一致，**沒有落差**。

| `code` | `name_zh` | `name_en` | `scope_mode` | 備註 |
|---|---|---|---|---|
| `system_admin` | 系統管理員 | System Administrator | `all_clubs` | 全模組全權限 |
| `content_editor` | 內容編輯 | Content Editor | `all_clubs` | 送審→發布流程中具發布權 |
| `team_competition` | 競技／球隊管理 | Team & Competition Manager | `all_clubs` | |
| `academy_program` | 學院／課程管理 | Academy & Program Manager | `all_clubs` | 賽事權限限 `scope_type = academy_only` |
| `business_sponsorship` | 商務／贊助 | Business & Sponsorship | `all_clubs` | 商店限 S1／S6 且**訂單個資遮罩** |
| `pr_media` | 公關／媒體 | PR & Media | `all_clubs` | |
| `customer_service_admin` | 客服／行政 | Customer Service & Admin | `all_clubs` | 會員與商店 S2–S5 |
| `translator` | 翻譯人員 | Translator | `all_clubs` | `scope_type = translate_only` |
| `viewer` | 檢視者 | Viewer | `all_clubs` | 唯讀，商店不含金額 |
| **`partner_club_manager`** | **合作球隊管理**（v3.0 新增） | Partner Club Manager | **`own_clubs`** | 可維護自家內容、球隊、課程、夥伴贊助與商品訂單；**可存取自家會籍但 `Member` 主檔遮罩**；**無推播、無廣告、無版本憑證、無 `J` 系統管理**。⚠️ **開通前提：資料範圍已落地 ＋ 兩法人間的個資委託處理約定已簽署** |

### 7.3 權限碼命名

`<domain>.<object>.<action>`，例：`shop.order.export`、`member.pii.reveal`、`shop.refund.execute`。

| `Permission` 欄位 | 值域 |
|---|---|
| `module_code` | `A` `B` `C` `E` `F` `G` `H` `I` `J` `K` `L` `P` `S`。⚠️ **v3.0 移除 `N`**（慈善已獨立）。**禁用 `D`（撞 `D1`）／`U`（撞 `U15`）／`O`（形近 `0`）／`M`（App，本檔不含）** |
| `submodule_code` | `B1`–`B6`、`C1`–`C5`、`P1`–`P4`、`E1`–`E6`（**E4–E6 為 App 廣告**）、`F1`–`F2`、`G1`–`G3`、**`J1`–`J4`（v3.0：`J4` 為俱樂部與授權管理）**、`K1`–`K5`、`L1`–`L4`、`S1`–`S6`、`H1`–`H6`、**`I1`（S1-12d 新增，2026-09-29：`I` 網站設定首次有後端實作，規劃書原文沒有逐項編號，本輪自行分配）、`I2`–`I6`（H 批，2026-10-02：選單／全域設定／多語系與字串翻譯表／場地／EDM 設定，同樣自行分配）**。⚠️ **v3.0 移除 `N1`–`N7`** |
| `domain` | `content` `faq` `charity` `team` `program` `calendar` `member` `business` `shop` `enquiry` `seo` `system` `site`（**`site` 為 S1-12d 新增，`I` 網站設定的 GEO-03／GEO-04 事實**）。⚠️ **v3.0 移除 `donation`**（慈善已獨立） |
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
| 學院／課程管理 | 表單詢問 **課程類詢問** | `enquiry.course.view`、`enquiry.course.update`（只看 `form_code IN (academy_children_training, camp_registration)`，見 `apps/api/README.md`「S1-10」段） |
| 商務／贊助 | 表單詢問 **合作／贊助類詢問** | `enquiry.partnership.view`、`enquiry.partnership.update`（只看 `form_code IN (partnership_sponsorship, proposal_download)`——`proposal_download` 併入本類為本輪判斷，規劃書未明文，見任務回報） |
| 公關／媒體 | 表單詢問 **媒體類詢問** | `enquiry.media.view`、`enquiry.media.update`（只看 `form_code = media_enquiry`） |
| 客服／行政 | 表單詢問 **✔全** | `form.view`、`form.update`、`enquiry.inbox.view`、`enquiry.inbox.update`（**不含** `enquiry.inbox.export`，比照 P3 匯出不給客服／行政的既有保守預設） |
| 合作球隊管理 | 表單詢問 **自家** | `form.view`、`form.update`、`enquiry.inbox.view`、`enquiry.inbox.update`（`scope_type = own_clubs`，不含匯出） |
| 系統管理員 | SEO／設定 **✔全** | `seo.setting.view/update`（全站 SEO 預設、追蹤碼、robots.txt 自訂規則）、`seo.redirect.*`（含 `import`）、`seo.report.view`（孤立頁面偵測）、`seo.llms.view/update`（`GEO-01` `llms.txt` 維護，S1-12a）、`seo.crawler.view/update`（`GEO-02` AI 爬蟲授權，S1-12b）、`seo.schema.view`（`GEO-05` 結構化資料完整性檢查，S1-12c）——**六組皆 `sysadmin_only`** |
| 內容編輯 | SEO／設定 **單頁 SEO** | 不新增權限碼——`content.page.update`／`content.article.update` 既有請求已擴充 `canonicalPath`／`isNoindex`／`isExcludedFromSitemap`／`seoKeywords` 四個欄位，跟隨既有內容編輯權限，不是獨立的 SEO 權限碼 |
| 系統管理員 | 網站設定 **✔全**（S1-12d 新增） | `site.fact.view/update`（成立年份、主場與場地、所屬聯賽、梯隊組成、聯絡方式、**台中藍鯨官網網址**——`GEO-03`／`GEO-04`／主站規劃書 §3.6）——**`sysadmin_only`** |
| 商務／贊助 | 商業／贊助 **✔全**（E1a 新增） | `business.partner.*`（E1 夥伴）、`business.sponsor.*`（E2 贊助商與贊助活動）、`business.sponsor_package.*`（E2 贊助方案）、`business.proposal.*`（E3 提案與檔案）、`business.lead.view`／`update`（E3 Lead 名單）、**`business.lead.export`（`is_restricted`）** |
| 內容編輯／公關媒體／檢視者 | 商業／贊助 **唯讀**（E1a 新增） | 只給 `business.partner.view`／`sponsor.view`／`sponsor_package.view`／`proposal.view`。🔴 **不給 `business.lead.*`**：Lead 含公司、姓名、Email，矩陣「唯讀」講的是夥伴與贊助內容，最小授權原則不外推到個資 |
| 合作球隊管理 | 商業／贊助 **自家夥伴與贊助**（E1a 新增） | 夥伴／贊助商／方案／提案 全動作＋`business.lead.view`／`update`（`own_clubs`）；**不含 Lead 匯出** |
| 內容編輯／公關媒體 | 慈善 **✔編輯**（E1a 新增） | `charity.content.*`（公益團體／慈善計畫／事蹟／影響力數據，含刪除，理由同 `content.article.*`）、`charity.setting.view`／`update`（捐款導流與參與方式設定）。商務／贊助、檢視者 唯讀；**合作球隊管理無**（慈善是磐石主站單元，藍鯨不設） |
| 內容編輯／公關媒體 | 內容 **✔編輯**（B6 媒體專區，E1a 新增） | `content.press.*`；檢視者 唯讀；合作球隊管理 view／create／update（不含刪除，比照 `content.article.*`） |
| 競技／球隊管理 | 球隊／賽事 **✔全**（C5，E1a 新增） | `team.achievement.*`（榮譽）、`team.milestone.*`（里程碑）。學院／課程管理：**只有 `team.achievement.*` 且 `scope_type = academy_only`**（榮譽有 `team_id`，列級授權生效）；`team.milestone.*` 不給（里程碑沒有球隊維度，無從判斷「學院梯隊」，理由同 C4 積分榜）。其餘角色唯讀 |
| 客服／行政 | 會員 **✔ 檢視／處理**（K1–K4，B1 新增，取代上一格的概念性寫法） | `member.account.view／create／update`、`member.pii.reveal`（解除遮罩）、`member.export`（`is_restricted`，K1 名單與續會名單）、`member.membership.view／create／update`（開通、調整、批次到期）、`member.plan.view`（**不含**方案新增編輯刪除）、`member.setting.view`、`member.jersey.view／create／update`、`member.jersey.export`（`is_restricted`）、`member.store.*`、`member.benefit.*`。**不含** `member.account.merge`（`sysadmin_only`） |
| 合作球隊管理 | 會員 **僅自家會籍，`Member` 主檔遮罩**（B1 新增） | 只給 `member.account.view`、`member.membership.view`、`member.plan.view`、`member.jersey.view`（`own_clubs`），**不含** `member.pii.reveal`／任何處理與匯出。K4 特約店家與權益不給 |
| 系統管理員 | 會員（B1 新增） | 全部 `member.*`，含 `member.account.merge`（合併重複帳號，不可逆，`sysadmin_only`）。`member.setting.update`（會員編號規則）、`member.plan.create／update／delete` 目前也只有系統管理員 |
| 公關／媒體 | 會員 **K5 公布稿交接**（C 批畫面回報後修正，2026-09-30） | `member.draw.view`（**遮罩版**抽獎活動與名單）＋`member.draw.announce`（產生公布稿草稿）。🔴 原本只給 `announce`，但抽獎清單與詳情都要 `view`，公關／媒體打不開任何活動、無從撰寫公布稿；補 `view` **不洩漏個資**——姓名、收件人資料一律遮罩，完整值需 `member.pii.reveal`（公關／媒體沒有）。**仍不給** `member.draw.create／update／export` 與任何會員模組權限（規劃書 §6：只取得遮罩版名單） |
| 客服／行政 | 詢問 **G3 電子報**（D 批新增） | `form.newsletter.view`／`update`（新增、退訂、同步 EDM）。**不含** `form.newsletter.export`（`is_restricted`，僅系統管理員，比照會員名單匯出）。檢視者不給（含 Email 個資）；合作球隊管理 `view`／`update`（`own_clubs`） |
| 商務／贊助 | 商業 **E4–E6 廣告 ✔全**（D 批新增） | `ad.advertiser.*`、`ad.slot.*`、`ad.campaign.view／create／update／delete／review／pause`、**`ad.contract.view／update`（`is_restricted`，合約金額）**、`ad.report.view`、**`ad.report.export`（`is_restricted`）**。**不含** `ad.maintenance.run`（`sysadmin_only`）。⚠️ 規劃書 §11 寫「財務 檢視合約金額」，但本專案十個角色沒有財務角色（見 §7.2），合約金額目前只有商務／贊助與系統管理員 |
| 公關／媒體 | 廣告 **檢視報表**（D 批新增） | 只有 `ad.report.view`（**不含**匯出與合約金額）。檢視者「檢視（不含金額）」：`ad.advertiser.view`、`ad.slot.view`、`ad.campaign.view`、`ad.report.view`。**合作球隊管理無任何 `ad.*`**（規劃書 §11） |
| 內容編輯 | 行動 App **M2 內容編排**（D 批新增） | `app.layout.view`／`update`（首頁區塊開關與排序、快捷入口、「更多」分頁、公告條、深連結對照） |
| 公關／媒體 | 行動 App **M3 推播（需覆核）**（D 批新增） | `app.push.view`／`create`（建立、預覽、試送、送審）。🔴 **`app.push.approve`（覆核、重送、自動推播規則）為 `sysadmin_only`，且核可者不得是建立者本人**（雙人覆核，規劃書 §11 補充規則 2） |
| 客服／行政 | 行動 App **M4 檢視（遮罩）**（D 批新增） | 只有 `app.device.view`。完整值 `app.device.reveal`（`is_restricted`＋`sysadmin_only`）、失效權杖清理 `app.device.update`（`sysadmin_only`）。檢視者 `app.release.view`、`app.layout.view`、`app.push.view`、`app.device.view`、`app.config.view`、`app.diagnostic.view` |
| 系統管理員 | 行動 App **M1／M5**（D 批新增） | `app.release.update`（版本、更新門檻、維護模式）、`app.config.update`（功能開關）、`app.credential.view`／`update`（憑證列管，`is_restricted`）、`app.diagnostic.update`——**全部 `sysadmin_only`**（規劃書 §11：誤設最低支援版本會讓全體使用者無法使用；合作球隊管理無權限） |
| 系統管理員 | **J3 帳號活動概況**（D 批） | `system.audit.view`（早已存在，`sysadmin_only`）。🔴 **只回帳號目前狀態與登入異常提醒**，沒有操作稽核記錄與登入歷程（§13.1，見 `docs/12` §12 第 46 點） |
| 內容編輯／競技／商務／檢視者 | 課程／報名 **唯讀**（P4，B1 新增） | `program.trial.view`、`program.trial_registration.view`。學院／課程管理 ✔全（`program.trial.*`、`program.trial_registration.view／create／update／export`）；客服／行政「報名處理」＝場次唯讀＋`program.trial_registration.view／update`；合作球隊管理 ✔自家（不含匯出）；公關／媒體「—」 |
| 所有持有 `calendar.view` 的角色 | 行事曆（L3／L4，B1 新增） | `calendar.setting.view`、`calendar.subscription.view`、`calendar.export`（行事曆是公開資料，不設 `is_restricted`）。`calendar.setting.update` 給內容編輯／公關媒體／合作球隊管理（比照自建事件）；**賽事類型（`event_types`）兩隊共用，寫入一律只有系統管理員**。賽事改期用來源模組的 `team.match.update`＋球隊列級授權，自建活動改期用 `calendar.custom_event.update`，整季 CSV 匯入用 `team.match.create` |

> **S1-12d 新增（2026-09-29）**：規劃書 §6 權限矩陣**沒有「網站設定」欄**（`I` 模組在本輪之前
> 完全沒有後端實作）。本輪比照 `seo.*`／`system.*` 既有先例——「全站層級設定、非逐篇內容編輯」
> 的既有判斷——把 `site.fact.view`／`site.fact.update` 兩碼都標記 `sysadmin_only=1`，十個角色
> 只有系統管理員持有。這是本輪自行判斷，規劃書沒有明文要求，見 `apps/api/README.md`「S1-12d」
> 節「規劃書沒寫清楚、自行判斷」。**2026-09-29 後續補**：`site.fact.*` 承載的欄位新增
> `site.blue_whale_site_url`（主站規劃書 §3.6「藍鯨官網網址於後台 `I` 網站設定可維護」），
> 沿用同一組既有權限碼，不新增權限碼。同一輪也新增一支**唯讀**場地清單端點
> （`GET /api/v1/admin/{club}/venues`，`Features/AdminVenues`），**不新增權限碼**，改用
> `IAdminClubAuthorizer.AuthorizeAnyAsync` 讓 `site.fact.view` 或 `team.match.view` 任一通過即可
> ——這支端點只是既有 `venues` 主檔的唯讀清單，不是需要獨立把關的新業務功能，見
> `apps/api/README.md`「S1-12d」節「藍鯨官網網址」與「場地清單端點」兩小節。

> **H 批新增（2026-10-02）**：`I` 網站設定其餘子模組落地，新增 **15 個權限碼**（module=`I`，domain=`site`；permissions 260 → 275、role_permissions 782 → 799）。矩陣沒有「網站設定」欄（只有「SEO／設定」欄：內容編輯「單頁 SEO」、**翻譯人員「字串翻譯表」**），判斷延續上方 `site.fact.*`／`seo.*` 的先例：

| 權限碼 | 子模組 | `is_club_scoped` | `sysadmin_only` | 持有者 |
|---|---|---|---|---|
| `site.menu.view`／`update` | `I2` 選單管理 | 1 | 1 | 僅系統管理員 |
| `site.global.view`／`update` | `I3` 全域設定（Logo、品牌色、Favicon、政策頁、維護模式） | 1 | 1 | 僅系統管理員 |
| `site.locale.view`／`update` | `I4` 多語系管理（語系、備援規則、日期數字格式、翻譯狀態總覽） | 1 | 1 | 僅系統管理員 |
| `site.string.view`／`update`／`translate` | `I4` 字串翻譯表 | 0（全站共用主檔） | **0** | 系統管理員全部；**翻譯人員 `view`＋`translate`**（`scope_type='translate_only'`） |
| `site.venue.view`／`create`／`update`／`delete` | `I5` 場地管理 | 0（`Venue` 不帶 `club_id`） | 1 | 僅系統管理員 |
| `site.edm.view`／`update` | `I6` EDM 平台設定 | 1 | 1，且 `is_restricted=1`（含憑證） | 僅系統管理員 |

- **`site.string.update` 與 `site.string.translate` 的分工是伺服器端強制的**（規劃書 §6 補充規則 ※：翻譯人員僅能編輯 `en` 語系欄位，不得修改繁中原文）：`translate` 只能新增／修改／清除非預設語系的翻譯；請求中任何改動繁中原文或分組的內容 → 整個請求 `403`；新增與刪除字串一律要 `update`。這是**第一個真的在伺服器端做出「僅翻譯欄位」限制的模組**，所以翻譯人員才能被指派權限（其他模組的 `translate_only` 仍待欄位級強制，見 `generate-club-seed-sql.py` 翻譯人員段落的說明）。
- 儀表板（`A`）**沒有專屬權限碼**：呼叫者持有儀表板用到的任一檢視／建立權限即可進入，各區塊再依對應模組權限決定有沒有（`AdminDashboardRepository.AllCandidateCodes`）；翻譯人員與持有 `site.locale.view` 者看得到全類別的「未翻譯內容數」。
- 既有 `GET /api/v1/admin/{club}/venues`（清單）允許 `site.fact.view`／`team.match.view`／`site.venue.view` 任一通過；其餘場地端點要 `site.venue.*`。

> **S1-12 新增（2026-09-25）**：矩陣「SEO／設定」欄除了內容編輯的「單頁 SEO」外，**十個角色裡只有
> 系統管理員打勾**，性質上與 J 模組的「系統」欄同樣是單一角色的排他欄位，因此 `seo.setting.*`／
> `seo.redirect.*`／`seo.report.view` 比照 `system.*` 的既有先例一律標記 `sysadmin_only=1`（雙重
> 防線：即使日後 J2「角色權限指派」畫面誤把這幾個權限碼勾給其他角色，`PermissionChecker` 仍會
> 擋下）。這是本輪的判斷，規劃書矩陣沒有明文要求用 `sysadmin_only` 而非單純不指派其他角色，見
> `apps/api/README.md`「S1-12」段「規劃書沒寫清楚、自行判斷」。**S1-12a／S1-12b（2026-09-25）
> 新增的 `seo.llms.*`／`seo.crawler.*` 沿用同一個判斷**，見 `apps/api/README.md`「S1-12a」
> 「S1-12b」兩節。**S1-12c（2026-09-25）新增的 `seo.schema.view` 沿用同一個判斷**，見
> `apps/api/README.md`「S1-12c」節。

> **S1-10 新增（2026-09-25）**：「課程類詢問」「合作／贊助類詢問」「媒體類詢問」三格**不是**用
> `role_permissions.scope_type` 表達（不像 `academy_only`／`own_teams` 需要另外解析列級範圍），
> 而是直接拆成三組獨立權限碼（`enquiry.course.*`／`enquiry.partnership.*`／`enquiry.media.*`），
> 應用層依角色持有哪一組碼決定 `WHERE form_code IN (...)` 的過濾條件——因為這裡的「類別」邊界是
> 固定的（9 個 `form_code` 的分類不會因使用者而變），不像球隊授權需要 `AdminUserTeam` 這種
> 逐人指派的關聯表，用更細的權限碼比多一個 `scope_type` 列舉值＋硬編碼分類對照表更直接，也不需要
> 修改 `role_permissions.scope_type` 的 CHECK 值域。⚠️ **規劃書 §6（行 1604）原始表格「廣告」／
> 「行動 App」／「表單詢問」三欄內容與表頭錯位**，本表格已改依 [`03-admin-spec.md`](03-admin-spec.md)
> §3 手動修正過的版本核對，完整說明見 [`12-database-schema.md`](12-database-schema.md#12-踩雷點)
> 第 37 點。

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
| 種子密碼 | **`Admin@123`**，以**雜湊儲存**（演算法待選型，優先 Argon2id，次選 bcrypt）。**`must_change_password` 預設 `0`（`DEFAULT 0`，2026-09-30 使用者裁決「都放寬」）**：新建帳號與本機種子帳號一律為 `0`，帳號列表不因預設值出現「待改密碼」標籤；欄位保留，僅在**管理員代為重設他人密碼**時設為 `1`（提示該帳號持有人換掉管理員設定的密碼，**只是提示、不強制**），持有人自行變更密碼後回到 `0`（唯一例外：測試帳號 `fresh.setup@tcrfc.test` 刻意種為 `1`，用來驗證「改密碼後旗標清除」）；**主站規劃書 v3.15 起登入流程不強制首次變更密碼**（客戶 2026-09-30 裁決，正式環境亦同），變更密碼由帳號持有人自行於帳號安全頁操作 |
| 密碼政策 | J 模組要求，以 `password_changed_at` 支援到期強制更換 |
| 2FA | `two_factor_enabled`／`two_factor_secret_encrypted`／`two_factor_confirmed_at`。**主站規劃書 v3.17 起後台介面暫不提供 2FA 設定入口**（§4.10 J1、§8〈安全〉列；使用者 2026-09-30 裁決）：登入不強制啟用，帳號安全頁不出現啟用選項；**`two_factor_enabled = true` 的帳號登入時仍須輸入驗證碼**（驗證流程保留）。三欄保留，日後開放入口時不須改綱要 |
| 登入失敗鎖定 | `failed_attempt_count` ＋ `locked_until`。**這是狀態欄位不是日誌表** |
| 最後登入 | `last_login_at` **單一欄位**，取代規劃書 J 的「登入紀錄」表 |
| 系統保護 | 至少保留一筆 `is_super_admin = true` 且 `status = 'active'` 的帳號，**不可全數停用** |

> ⚠️ **前台 `Member.email` 是另一件事**，會員維持 Email ＋ LINE 一鍵登入不變（**不用 Google**）。兩套帳號系統**完全獨立，不共用表、不共用登入**。

### 7.7 `admin_refresh_tokens`：更新權杖的工作階段狀態（`S1-3` 新增，2026-09-23 補文件）

`AdminUser` 的登入採**存取權杖（短效，簽在應用層，不落表）＋ 更新權杖（本表）**的雙權杖模式。本表**不是權限模型的一部分**（不參與角色與範圍判斷），是**登入工作階段能不能延續**的必要狀態——沒有它，發出去的更新權杖既無法輪替也無法撤銷，等於每個裝置的登入永遠有效直到過期，這比目前的設計更不安全。

| 欄位 | 用途 |
|---|---|
| `token_hash` | **只存 SHA-256 雜湊，不存明文**；UNIQUE，登入時以雜湊查表 |
| `issued_at`／`expires_at` | 核發與到期時間 |
| `revoked_at` | 非空＝已撤銷（正常輪替、登出、或重放偵測觸發） |
| `replaced_by_id` | 指向輪替後的新一筆，串出**輪替鏈** |

**輪替與重放偵測**（`AdminAuthService.RefreshAsync`）：每次用更新權杖換取新的存取權杖，舊的一筆立刻標記 `revoked_at` 並填 `replaced_by_id` 指向新一筆；**若有人拿一把已經被標記 `revoked_at` 的權杖來用**（代表它被偷過、合法使用者早已換到新的一把），系統判定為重放攻擊，**撤銷該帳號名下全部有效更新權杖**，逼使用者全部裝置重新登入。

> 🔵 **本表整體不是 [§13.1](12-database-schema.md#131-沒有稽核與登入日誌表) 排除的日誌表。**
> §13.1 排除的是「事後查詢用的操作與登入歷程」；本表刪掉就無法完成登入輪替與重放偵測，是**功能運作必需**，
> 與 `EmailLog`（§12 踩雷點 5：功能單元不是日誌）同一類判準，不是「誰在何時做了什麼」的旁路記錄。
> **判準是「拿掉它系統還能不能運作」。**
>
> 🔴 **原本有 `created_ip`／`user_agent` 兩欄，已於 2026-09-23 依使用者裁決拿掉。**
> 查證 `apps/api/Features/AdminAuth/AdminAuthService.cs` 與 `AdminAuthEndpoints.cs` 後確認：這兩欄
> **只在核發時寫入，程式碼裡沒有任何地方讀取或用來做判斷**（不綁定裝置、不比對來源、不影響輪替或重放偵測的結果），
> 且**沒有清除機制**，撤銷與過期的舊列會無限累積——功能上等同一份持續增長的登入位置紀錄，
> 正好落在 §13.1 明文「不能回答：登入歷程、異常偵測、**來源 IP**」那一條上。
> **日後若真要做裝置綁定或異常偵測再加回來**——那時它才有讀取端、才說得上是功能而不是紀錄。
>
> ⚠️ **這一筆的教訓**：「本表不是日誌表」這個判定**不是欄位層級的通行證**。
> 表可以是功能單元，裡面仍然可以夾帶純紀錄性質的欄位——
> **判準要逐欄問一次「有沒有讀取端」，不是整表過關就算數。**

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
| 🔴 **`Registration`** | **`health_declaration`**（健康聲明） | **🔐 建議** | ⚠️ **待法務確認** | 行 373、1087 |
| `Member` | `line_user_id_encrypted` | 🔐 | 系統管理員 | 行 1282 |
| `JerseyIssue` | `recipient_name`、`address` | 🔒 | 系統管理員、客服／行政、出貨角色 | K3 |
| `Order` | `recipient_name`、`recipient_phone`、`recipient_address` | 🔒 ⚖️ | 系統管理員、客服／行政、出貨角色 | 行 1327 |
| `OrderItem`／`StoreInvoice` | 全表 | ⚖️ | — | 稅法保存，年限待確認第 28 點 |
| `StoreInvoice` | `carrier_id_encrypted` | 🔐 | 系統管理員 | S6 |
| `ProductVariant` | `cost` | 🔒 | 系統管理員、商務／贊助（**C1 權限碼 `shop.cost.view`／`shop.cost.update`，is_restricted**；檢視者連規格與售價都看不到，`shop.variant.view`） | S1 |
| `PaymentChannel` | `credential_encrypted` | 🔐 | **僅系統管理員** | S6 |
| `DrawRoster` | `name_snapshot` | 🔒 | 系統管理員、客服／行政（公關只拿遮罩版） | 行 1322 |
| `DrawRoster` | `withholding_data_encrypted` | 🔐 ⚖️ | **僅系統管理員**，達扣繳門檻才蒐集 | 行 1142–1155 |
| `Membership` | `tier`、`membership_end_on` | 🔒 | 系統管理員、客服／行政、**合作球隊管理（僅自家 `club_id`）** | K1 |
| `Registration`／`Enquiry`／`EnquiryAnswer` | 姓名、電話、Email、生日 | 🔒 | 依模組權限 | 行 1320 |
| `NewsletterSubscriber` | `email` | 🔒 | 系統管理員、公關／媒體 | G3 |
| `AdminUser` | `password_hash`、`two_factor_secret_encrypted` | 🔐 | **不可讀取，僅比對** | J |
| `AdminUser` | `email` | 🔒 | **不作登入鍵**，僅通知用 | 本檔決定 |

**個資保存期限未定**：規劃書要求七類表單顯示保存期限說明，但實際年限未定（待確認第 15 點，法遵項目，**擋表單上線**）。交易紀錄的年限待會計師確認（第 28 點）。

---

> 🔴 **`Registration.health_declaration` 的分級須由法務確認，不是設計決定。**
> 課程與營隊的報名流程（行 373）明文要求填「**健康聲明與同意條款**」，後台 P3（行 1087）也列「健康聲明」。
> **《個人資料保護法》§6 把病歷、醫療、健康檢查列為特種個資**，蒐集、處理與利用的條件比一般個資嚴格，
> 且本表的當事人**多為未成年學員**。本檔先標 **🔐 加密**是保守作法，
> **但真正要確認的是「能不能蒐集、要不要蒐集、保存多久」，那在儲存方式之前**——
> 繫於 `STATUS.md` **B-9**（個資保存期限與條款未經法務核定）。

## 9. 快照、視圖與不可變資料

三條結構原則的完整論證。**違反其中任何一條，錯誤都不會立刻顯現，而是在對帳或客訴時才爆。**

### 9.1 值複製快照：讀取不得回頭 join

適用：`OrderItem`、`DrawRoster`、`StoreInvoice`。**全部必填 `club_id`**——快照凍結的包含歸屬。

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
| 賽事延賽（v3.13） | 原定日期時間存 `Match.original_match_on`／`original_kickoff`，**只有 `Match` 有這兩欄**；`CalendarEvent` 是彙整層不重複存放，一律反映 `Match` 現行的 `match_on`／`kickoff` |

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

> ⚠️ 目前 [`../content/migration/舊官網URL盤點.csv`](../content/migration/舊官網URL盤點.csv) 的「客戶決定」與「新站對應頁面」兩欄**全空**，301 對照表尚無法產生。
> ✅ **（S1-12，2026-09-25）匯入機制本身已完成**（`Features/AdminSeo/AdminRedirectsRepository.ImportCsvAsync`，重用 `Common/CsvUtils.cs`——該檔案檔頭原本就預告本項會重用它），
> **upsert 鍵 `(club_id, from_path)`**（沿用既有 `UQ_redirects_club_path`）：CSV 裡的舊網址若已存在就整列覆寫 `to_path`／`is_active`，否則新增；比照 `Features/AdminFaqs` 的 CSV 匯入語意（整批驗證、任一列有誤就整批不寫入），不是比照 `Features/AdminMatches`（純建立）。**匯出（`ExportCsvAsync`）同一輪一併補上**，格式與匯入欄位對稱，方便下載現況後回頭編輯再匯入。**表格內容本身仍待客戶決定**（上一句「全空」的現況未變），本輪只完成匯入匯出這個機制。

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
| 發票會計 CSV | `StoreInvoice` | ✔ |

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

**維持全站唯一（v3.0 不變）**——這五個是刻意不改成複合鍵的，各有硬理由：

| 表 | 唯一鍵 | 為什麼不加 `club_id` |
|---|---|---|
| `Team` | `code` | 行事曆訂閱網址與 `/schedule/d1/` 的識別鍵，**已在外流通**。藍鯨一線隊是 `BW1` 不是第二個 `D1` |
| `Article` | `slug` | 共同文章必須有**單一 canonical**，否則兩站兩份等於重複內容；App 分享也只能送出一條連結 |
| `ProductVariant` | `sku` | 揀貨與庫存識別鍵，跨店唯一才不會出錯貨 |
| `Order` | `order_no`、`lookup_token` | 單一商店號對帳不得重號（加俱樂部前綴，不改鍵） |
| `Member` | `member_no`、`email` | **一人一帳號**；`Member` 本身不帶 `club_id`，俱樂部維度在 `Membership` |

**改為 `(club_id, …)` 複合唯一（v3.0）**：

| 表 | v2.6 | v3.0 | 理由 |
|---|---|---|---|
| `Page` | `slug` | **`(club_id, slug)`** | 兩站必然都有 `about`／`contact`／`privacy` |
| `Setting` | `setting_key` | **`(club_id, setting_key)`** | 聯絡資訊、社群連結、運費設定兩站不同 |
| `Redirect` | `from_path` | **`(club_id, from_path)`** | 兩站都會有 `/zh/about/` |
| `Season` | `code` | **`(club_id, code)`** | 兩隊球季不同步 |
| `NewsletterSubscriber` | `email` | **`(club_id, email)`** | **法遵**：訂閱同意與退訂須分別成立，同一人可以只退訂其中一站 |

⚠️ **`club_id` 可為空的表（`Article`／`PressResource`／`Faq`／`Staff`／`Charity`／`CharityProgram`／`ImpactRecord`／`ImpactMetric`）**
用 `UNIQUE (club_id, slug)` 即可——**SQL Server 的唯一索引把 NULL 當成相等**，不需要篩選唯一索引。
`(NULL,'x')`、`(1,'x')`、`(2,'x')` 允許併存，網址對應哪一筆由**路由優先順序**解決（俱樂部專屬優先、回退共同）。
🔴 **但 `Article.slug` 是上表的例外，維持全站唯一**，不走這條。

**其餘唯一鍵**：

| 表 | 唯一鍵 |
|---|---|
| `Club` | `code`、`domain` |
| `Competition` | `(club_id, code)` |
| `Membership` | **`(member_id, club_id, season_id)`**——一人每俱樂部每季一份 |
| `MemberCard` | `token`（**不可由會員編號推導**） |
| `AdminUser` | **`username`**（**`email` 不設唯一**，只作通知用） |
| `AdminRole` / `Permission` | `code` |
| `AdminUserClub` | `(admin_user_id, club_id)` |
| `AdminUserTeam` | `(admin_user_id, team_id)` |
| `PaymentChannel` | **`(owner_club_id, channel_type, environment)`** |
| `DrawRoster` | `(member_draw_id, roster_version, serial_no)`、`(member_draw_id, roster_version, member_no_snapshot)`（C1：含版本） |
| `DrawRosterVersion`（C1） | `(member_draw_id, roster_version)` |
| `ComicEpisode`（C1） | `(club_id, episode_no)` |
| `Shipment`（C1） | `(order_id)`（每張訂單一筆） |
| `FanEventRegistration`（C1） | `(fan_event_id, member_id)`，**過濾唯一索引**：`member_id` 非空且狀態不是 `cancelled` |
| `FormField`（S1-10） | `(form_id)`，**過濾唯一索引** `WHERE is_summary = 1`：同一張表單最多一個「內容摘要」欄位（`UQ_form_fields_one_summary_per_form`，第二道防線，主要防線在應用層；2026-10-01 對齊 DDL） |
| `Locale` | `code` |
| `PressResource` | `(club_id, slug)` |
| `FaqEmbedSlot` | `code`（S1-8 新增） |
| 所有 `*_i18n` | `(<entity>_id, locale)` |
| 其餘內容表 | `(club_id, slug)`（`club_id` 必填者）或 `slug`（不帶 `club_id` 者） |

> `MembershipPlan` **`(club_id, season_id, code)`**；`PartnerStore` **`(club_id, slug)`**（可為空，NULL 視為相等故共同店家的 slug 亦唯一）；
> `MembershipBenefit` 不帶 `club_id`，唯一鍵為 `(membership_plan_id, sort_order)`。
> ⚠️ **慈善的 `DonationStore`／`DonationProject`／`Donation` 已移出本檔**（獨立資料庫），見 [`16-charity-schema.md`](16-charity-schema.md)。

### 11.2 查詢索引

| 表 | 索引 | 用途 |
|---|---|---|
| `Article` | `(status, published_at desc)`、`(article_category_id, published_at desc)`、**`(club_id, status, published_at desc)`** | 新聞列表與分類頁、**依俱樂部與追蹤過濾** |
| `Match` | `(season_id, match_on)`、`(status, match_on)` | 賽程／賽果切換 |
| `CalendarEventTeam` | `(team_id, source_type)` | 行事曆隊別篩選 |
| `Registration` | `(session_id, status)`、`(member_id)` | 報名管理與我的報名 |
| `Order` | `(member_id, created_at desc)`、`(order_status)`、`(payment_status, created_at)`、**`(club_id, created_at desc)`** | 我的訂單、出貨佇列、逾時清理、**後台依俱樂部過濾** |
| `OrderItem` | `(order_id)` | |
| `InventoryMovement` | `(product_variant_id, occurred_at desc)` | 庫存異動查詢 |
| `Membership` | `(club_id, status, membership_end_on)`、`(member_id)` | 會籍到期提醒、會員中心逐俱樂部列出 |
| `DrawRoster` | `(member_draw_id, roster_version, serial_no)`、`(member_draw_id, roster_version, is_winner)` | 名單匯出、中獎人清單 |
| `Order`（C1 補） | `(selling_club_id, created_at desc)` | 後台依賣方俱樂部（資料範圍）過濾 |
| `ProductVariant`（C1 補） | `(product_id)` | 商品的規格清單 |
| `RefundRequest`（C1 補） | `(club_id, status)`、`(order_id)` | 案件清單、訂單的退款案件 |
| `EmailLog` | `(member_id, sent_at desc)`、`(type, sent_at)` | |
| `Enquiry` | `(form_id, status, created_at desc)`、`(assignee_admin_user_id)` | 收件匣 |
| `AdminUserClub` | `(admin_user_id, is_active)` | **每個請求都要算資料範圍，這條是熱路徑** |
| `FaqEmbedSlotLink` | `(faq_embed_slot_id)`（S1-8 新增） | 依掛載點反查有哪些題目被額外指定 |
| `Registration`（試訓補） | `(trial_id, status)`（`IX_registrations_trial_status`） | `registrations` 同時服務梯次與試訓，試訓報名清單依 `trial_id` 查（2026-10-01 對齊 DDL） |
| 帶 `club_id` 的內容表 | `(slug, club_id)` | 路由解析：俱樂部專屬優先、回退共同 |
| 所有 `*_i18n` | `(locale)` | 翻譯狀態矩陣 |

### 11.3 外鍵刪除行為

| 關係 | 行為 |
|---|---|
| 母表 → `*_i18n` | `CASCADE` |
| 母表 → 圖集子表（`ProductImage`／`CharityProgramImage`／`ComicPage`／`ProposalFile`） | **`CASCADE`**（圖集依附母體，母體沒了圖集無意義） |
| `Order` → `OrderItem`／`Shipment`／`StoreInvoice` | **`RESTRICT`**（⚖️ 法定保存，不得刪） |
| `MemberDraw` → `DrawRoster` | **`RESTRICT`**（不可變名單） |
| `Member` → `Order`／`Registration` | **`SET NULL`**（刪帳號後訂單與報名仍在） |
| `Product` → `OrderItem` | **無外鍵約束的刪除行為**——`OrderItem` 是快照，商品下架不影響歷史訂單 |
| `Member` → `Membership` | **`RESTRICT`**（會籍涉金流與發票，刪帳號不得連帶刪會籍） |
| `Membership` → `MemberCard` | **`CASCADE`**（每份會籍一張卡，會籍沒了卡就該失效） |
| `Club` → 任何帶 `club_id` 的表 | **`RESTRICT`**（俱樂部是主檔，有資料就不得刪） |
| `AdminUser` → `AdminUserClub`／`AdminUserTeam` | `CASCADE` |
| `Cart` → `CartItem` | `CASCADE` |

---

> ⚠️ **慈善捐款平台的資料表全部不在本檔**（獨立後台與獨立資料庫），另出 [`16-charity-schema.md`](16-charity-schema.md)。

---

## 16. 行動 App 與廣告型別（D 批延伸設計，2026-09-30）

> **來源**：App 規劃書 §7（廣告）、§8.1–8.9（後台 M／E4–E6）、§10.1（型別）、§6（推播）、§12（個資）；`docs/19` §5／§6／§7。
> **不是新規格**：欄位都是規劃書 §10.1 列出的，加上 M2／M3／M5 功能需要的附屬表（標「補」）。
> DDL 在 `db/club-schema.sql` 的 **4.13**；EF 實體與 migration `AlignSchemaD1`。**全部不加 `club_id`**（兩隊共用一個 App，見 `docs/12` §12 第 46 點）。
> 通則同 [§6](#6-關鍵資料表明細)：`id` 非叢集主鍵＋`row_seq` 叢集鍵（`ad_events` 是 `bigint IDENTITY` 叢集主鍵、`ad_daily_stats`／`push_message_stats`／`*_i18n` 是複合主鍵）；
> 金額 `int` 元；雙語走 `*_i18n`（`zh-Hant` 必存、`en` 可缺）；json 欄位在 Azure SQL 為原生 `json`（本機 SQL Server 2022 轉 `nvarchar(max)`，見 `deploy/local-ddl.sh`）；時間戳一律 UTC。

### 16.1 廣告（E4–E6）

| 表 | 欄位重點（規劃書欄位；標「補」者為本檔補的） | 約束與索引 |
|---|---|---|
| `ad_slots` | `slot_code`（`<畫面>_<位置>`，唯一，建立後不可改）、`surface`（`app`；預留 `web`）、`screen_code`／`block_order`（畫面位置，§2.2）、素材規格：`aspect_ratio`（`16:9`）／`min_width`／`min_height`／`max_file_kb`／`allowed_formats`、`allow_video`、`session_impression_cap`、`rotation_cap`（1–10）、備援素材：`fallback_image_key`／`_width`／`_height`／`fallback_link`、`is_active` | `UQ(slot_code)`。**兒童向畫面（S15／S16／S17 課程與報名）不設版位；不設慈善相關版位**（應用層強制） |
| `ad_slots_i18n` | `name`、`fallback_alt`（備援素材說明文字） | PK `(ad_slot_id, locale)` |
| `advertisers` | `tax_id`、`contact_name`／`contact_phone`／`contact_email`、`contract_note`、`cooperation_start_on`／`_end_on`、**`sponsor_id`（可為空 → `sponsors`）**、`status`（`negotiating`／`active`／`ended`） | `sponsor_id` 只用來避免重複維護聯絡窗口，**不是合併**（五種商業對象不混用，App 規劃書 §10.3） |
| `advertisers_i18n` | `name` | |
| `ad_campaigns` | `advertiser_id`、`slot_id`（一個檔期綁一個版位，不做多對多）、`name`、`starts_at`／`ends_at`（`CK ends_at > starts_at`）、`weight`（1–100）、`daily_impression_cap`、`per_device_daily_cap`、`goal_type`（`guaranteed`／`traffic`）、`goal_impressions`、`delivered_today`＋`delivered_on`（pacing 計數，跨日以 `delivered_on` 判斷重置）、**補：`delivered_total`**（累計，「目標 vs 已達成」不必每次加總日聚合）、`contract_amount`（🔒 受限：`ad.contract.view`）＋`is_amount_hidden`、`status`、**補：`paused_from`／`pause_reason`**（暫停前狀態與原因、作廢原因）、`reviewed_by`／`reviewed_at` | 狀態機 `draft → pending_review → scheduled → running → ended → closed`；`running ↔ paused`（回到暫停前狀態）；`voided`（任一狀態，不可逆）。🔴 **素材未通過審核的檔期不得進入 `running`**。`IX(slot_id, status, starts_at)` |
| `ad_creatives` | `campaign_id`、`locale`（依語系分別上傳）、`image_key`／`_width`／`_height`、`video_key`（影片仍須海報圖）、`alt_text`、`title`、`cta_text`、`click_url`（`tcrfc://` 或 http(s)）、`theme`（`light`／`dark`／`both`）、`variant_tag`（A／B）、`review_status`（`pending`／`approved`／`rejected`）＋`reject_reason`／`reviewed_by`／`reviewed_at`、**補：`is_paused`**（單一素材緊急暫停）、`sort_order` | 🔴 素材內容被修改（含換圖）後回到 `pending`；已排程或投放過的檔期不能刪素材（成效要留著對帳） |
| `ad_events` | **原始事件（保存 90 天）**：`event_type`（`impression`／`click`）、`creative_id`／`campaign_id`／`slot_id`、`occurred_at`（發生時間，不是上傳時間）、`received_at`、`device_install_id`、`platform`／`app_version`／`locale`、`presentation_id`、`batch_id`、**補：`dedupe_key char(32)`**（去重鍵，唯一）、`aggregated_at`。🔴 **不存 `member_id`、完整 IP、定位座標、廣告識別碼** | `UQ(dedupe_key)`；`IX(aggregated_at, occurred_at)`、`IX(device_install_id, creative_id, occurred_at)`。去重：曝光以 `presentation_id`（沒有就以素材＋裝置＋秒）、點擊以「同裝置同素材 5 秒」；拒收超過 24 小時與明顯在未來的事件 |
| `ad_daily_stats` | 日期（**台灣當地日期**）× 檔期 × 素材 × 版位 × 平台 × 語系 → `impressions`／`clicks`／`unique_devices`（**CTR 由 clicks／impressions 現算，不存**） | 複合主鍵。⚠️ 跨多日彙總時 `unique_devices` 只能加總（＝裝置日），不是期間內真正的不重複人數（原始事件只留 90 天且不跨日去重） |

**聚合作業**（`AdMaintenanceService`）：有未聚合事件的日期整天重算（冪等）；清除只刪「已聚合且超過 90 天」的事件；超過 2 天仍未聚合者告警（聚合失敗不得靜默跳過）；只重算保留期內的日期。

### 16.2 App 營運（M1／M3／M4／M5）

| 表 | 欄位重點 | 約束與索引 |
|---|---|---|
| `app_devices` | `device_install_id`（唯一，解除安裝即失效、不跨 App）、`platform`、`os_version`／`app_version`／`locale`、`push_token_encrypted`（**Data Protection 加密**）＋`push_token_hash`（SHA-256，只供去重與失效清理）＋`push_token_status`（`none`／`valid`／`invalid`）、`push_permission`（`not_determined`／`granted`／`denied`／`provisional`）、`member_id`（可為空的弱關聯，登出即解除）、`first_seen_at`／`last_active_at`、更新權杖四欄（`refresh_token_hash`／`_expires_at`／`_rotated_at`、`revoked_at`——**AP-3 會員登入使用，M4 不讀寫**） | `UQ(device_install_id)`。🔒 **權杖視同個資**：後台清單只給遮罩識別碼，完整值需 `app.device.reveal` |
| `push_topic_subscriptions` | `device_id`（規劃書欄位 `device_install_id` 的內部外鍵形式）、`member_id`、`topic_type`（`team`／`news_category`／`club`）、`topic_value`（球隊代碼／分類代碼／俱樂部代碼）、`is_following`、`is_push_enabled` | `UQ(device_id, topic_type, topic_value)`；**這是我們自己的表，不是 FCM topic**（docs/19 §5） |
| `push_messages` | `kind`（`announcement`／`news`／`match`）、`image_key`／`_width`／`_height`、`deep_link`、分眾：`audience_tier`（`all`／`fan_club`／`registered`／`anonymous`）＋`audience_club_id`＋`audience_team_codes`（json），`scheduled_at`、`status`（`draft → pending_review → scheduled → sending → sent／partial／failed`；`cancelled`）、`reject_note`、**`reviewed_by`／`reviewed_at`（🔴 雙人覆核：不得是 `created_by`）**、`sent_at`、`audience_estimate`、`sent_count`／`delivered_count`／`failed_count`／`opened_count`、**補：`send_cursor`**（分批送出的游標：已處理到的 `app_devices.row_seq`，失敗重送從這裡續，避免不記錄個人層級投遞紀錄卻重複送）、`failure_message` | `IX(status, scheduled_at)`。分眾三維度（會籍層級、追蹤球隊、俱樂部歸屬），**刻意不做行為定向** |
| `push_messages_i18n` | `title`、`body`、`image_alt`（**文案須雙語**，英文缺漏時英文語系裝置收到繁中） | |
| `push_message_stats` | **補**：`(push_message_id, platform, locale)` → `sent`／`delivered`／`opened`。「送達」＝推播服務接受且未回報權杖失效，**不等於到達裝置**；🔴 **不記錄個人層級的開啟行為** | 複合主鍵 |
| `app_releases` | `platform`、`version`（`主.次.修`，建置號不參與比較）、`build_number`、`released_on`、`status`（`testing`／`live`／`withdrawn`）、`is_min_supported`（低於它強制更新）、`is_recommended`（低於它建議更新，可略過） | `UQ(platform, version)`。每平台各至多一筆為 true（應用層強制，不加篩選唯一索引）；只有 `live` 的版本能設為門檻；被設為門檻的版本不能下架或刪除；設最低支援版本須二次確認 |
| `app_releases_i18n` | `whats_new`、`force_message`、`recommend_message` | |
| `app_diagnostic_reports` | `device_install_id`（可為空）、`platform`、`app_version`／`build_number`／`os_version`、`occurred_at`、`report_type`（`crash`／`abnormal_exit`／`api_error`／`startup_time`／`user_report`）、**補：`metric_value`**（啟動耗時毫秒或錯誤次數）、`summary`／`detail`、`status`（`new`／`reviewing`／`resolved`／`ignored`） | 🔴 **不得存個資**：不記 `member_id`、完整 IP、定位座標；自由文字入庫前把 Email 與 8 位以上數字遮成 `[已遮蔽]`；保存 90 天 |

### 16.3 M2 內容編排與 M5 設定（補的附屬表）

| 表 | 欄位重點 | 說明 |
|---|---|---|
| `app_deep_links`（＋`_i18n.label`） | `code`（唯一）、`app_link`（**必須 `tcrfc://`**）、`web_url`、`requires_login`、`is_active`、`sort_order` | 深連結對照表（§2.3：App 畫面 ↔ 官網網址，供推播與廣告素材選用）。有版面項目引用時不能刪 |
| `app_layout_items`（＋`_i18n.label`） | `kind`（`home_section`／`quick_entry`／`more_item`）、`item_key`、`deep_link_id`、`icon_key`、`sort_order`、`is_enabled` | `UQ(kind, item_key)`。**首頁區塊是 §3.1 的固定九個**（種子種入，只能開關與排序，不能新增或刪除） |
| `app_announcements`（＋`_i18n.message`） | `link_url`、`starts_at`／`ends_at`、`audience_tier`、`audience_club_id`、`is_enabled` | App 專屬公告條；目標對象只在帶裝置識別的讀取才篩選 |
| `app_feature_flags` | `flag_key`（`{模組}_{功能}` 小寫蛇形）、`is_enabled`、`string_value`（**三態旗標的值，目前只有 `payment_mode`：`off`／`external`／`inapp`**）、`platform`（`all`／`ios`／`android`）、`description` | `UQ(flag_key, platform)`；平台值優先於 `all`。🔴 `payment_mode` **只能降級**：不得從 `external`／`off` 遠端開成 `inapp`（docs/19 §10） |
| `app_credentials` | `kind`（`apns_key`／`fcm_credential`／`apple_developer_program`／`google_play_account`／`maps_api_key`／`other`）、`label`、`external_ref`（Key ID 之類，**不是金鑰**）、`created_on`／`last_rotated_on`／`expires_on`／`rotation_period_days`、`note` | 🔴 **只存列管資訊，絕不存金鑰本身**。部分金鑰沒有到期日：無 `expires_on` 時以「上次輪替日（沒有就建立日）＋輪替週期」為基準；**已屆期或屆期前 60 天告警**；輪替寫敏感操作日誌 |
| `app_settings` | `setting_key`（唯一）、`setting_value`（json，只存不查） | `maintenance.all`／`maintenance.ios`／`maintenance.android`（維護模式：`enabled`＋雙語訊息）、`push.rules`（自動推播規則：賽事提醒提前小時數、會籍到期提醒天數、各類自動推播開關） |

### 16.4 這一節刻意沒有的東西

- 沒有任何日誌表：`ad_events`（功能單元、90 天）、`push_message_stats`（彙總數字）都不是「誰在何時做了什麼」的旁路記錄；**沒有 `push_delivery` 之類的逐裝置投遞表**（會變成個人層級的推播行為紀錄）。
- 沒有通知中心專用表：App 通知中心（§3.13）＝已送出的 `push_messages`（`sent`／`partial`、90 天內）依分眾過濾；會籍到期、開通完成這類「對單一會員」的推播屬 AP-3 之後，那時再定收件匣的資料形狀。
- 沒有 `Club` 之外的俱樂部維度：`audience_club_id` 只用於分眾條件，不是資料歸屬。
