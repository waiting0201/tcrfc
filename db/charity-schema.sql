/* ============================================================================
   TCRFC 慈善捐款平台資料庫 — Azure SQL DDL
   ============================================================================

   資料庫：sqldb-charity

   🔴 這是一個完全獨立的資料庫，與主站資料庫 sqldb-club 之間絕對不得建立外鍵、
      不得跨庫查詢（Azure SQL 本身也不支援跨庫查詢，這條在技術上是硬的）。
      主站的 Charity／CharityProgram 在本庫只有唯讀複本（見下方 CharityRef／
      CharityProgramRef），以 ref_code ＋ 名稱快照參照，不是外鍵。
      主站資料庫由另一位同事負責，不在本次範圍。

   主辦與收款主體：台灣足球策略發展協會（不是台中磐石足球俱樂部）。

   對應文件（真實來源，改綱要要先改這裡再回頭改本檔）：
     - docs/16-charity-schema.md（本檔的主要依據，2026-09-20 版）
     - docs/10-charity-donation-site.md（功能導航）
     - output/TCRFC_慈善捐款平台功能規劃書.md（v2.5，共 806 行，上游真實來源）
     - docs/12-database-schema.md §1.2（主鍵與命名慣例，本庫沿用）

   產生日期：2026-09-20
   表數：23 張（N 捐款核心 8 ／主站唯讀複本 2 ／後台帳號與權限 5 ／稽核 1 ／
        共通機制 7）＋ 4 張 *_i18n 側表

   修訂：2026-09-20（二次修訂，依協調者回饋與 docs/16 §0／§2.3 的補寫更新）
     1. 所有實體表補上共通欄位 created_at／updated_at／created_by／
        updated_by（沿用 docs/12 §1.3，本庫不因有 AuditLog 而省略；docs/16
        §0 已補寫這條）。例外維持不變：locale（自然鍵）、admin_user_role／
        role_permission（純關聯表）、四張 *_i18n 側表。created_by／
        updated_by 一律指向 admin_user(id)，系統自動產生的列為空。
     2. Permission 加回 module_code／submodule_code／domain／action 四欄
        分解（docs/16 §2.3：本庫 module_code 固定為 N，submodule_code 值域
        N1–N7，與主站同形；唯一不套的是 is_club_scoped）。

   ----------------------------------------------------------------------------
   技術約束（對應任務書的硬性規定，編號沿用）：

   1. 主鍵 id uniqueidentifier 一律 NONCLUSTERED；另加不對外的 seq bigint
      IDENTITY 當叢集鍵（CLUSTERED UNIQUE 約束）。原因：SQL Server 的
      uniqueidentifier 比較位元組順序是反的，NEWSEQUENTIALID() 又是伺服器端
      產生、故障移轉後還會產生新序列，應用層無法在 INSERT 前先知道 id，因此
      改用非叢集主鍵 ＋ 獨立叢集鍵。id 一律 DEFAULT NEWID()（隨機、不對外保
      證遞增，僅供應用層在 INSERT 前先行取得）。
      例外：Locale 是全庫共用的字典表，以 code 為自然鍵、被所有 i18n 側表以
      locale nvarchar(10) 外鍵參照，不另建 uuid 主鍵；AdminUserRole／
      RolePermission／*_i18n 等純關聯表沿用 docs/12 §1.2「關聯表複合主鍵」
      慣例，同樣不另建 uuid 主鍵。
   2. 本庫沒有 club_id 欄位——單一法人（協會），不是多俱樂部架構。
   3. 本庫沒有 Member 表。Donation 絕對沒有 member_id 外鍵，捐款人不登入不
      註冊；會員比對只在後台查詢當下以 Email 軟性進行，不寫入任何表。
   4. 本庫有 AuditLog（與主站相反）——退款、分潤百分比設定、含個資的明細
      匯出三類操作須留稽核軌跡，是勸募法遵要求（規劃書 §11.2）。
   5. 金額一律 int 存「元」，百分比 decimal(5,2)。分潤無條件捨去至整數元，
      store_amount + project_amount + association_amount 必須等於 amount
      （見 donation 表的 CHECK 約束）。
   6. 雙語走 <entity>_i18n 側表，唯一鍵 (<entity>_id, locale)。
   7. national_id_encrypted（身分證字號）必須是加密欄位，存密文字串，不存
      明碼；carrier_id_encrypted、two_factor_secret_encrypted、
      credential_encrypted 比照辦理。
   8. JSON 用原生 json 型別，只有 DonationPayment.raw_response 與
      donation_project_i18n.description（說明內文，區塊編輯器內容）兩處，
      只存不查。
   9. 索引、唯一鍵與外鍵刪除行為照 docs/16 §6。DonationInvoice.invoice_no
      是「已開立者唯一、未開立為空」，用篩選唯一索引
      （WHERE invoice_no IS NOT NULL）。
   10. 圖片沒有外鍵，是欄位組 <名稱>_key（nvarchar(500)）＋ _width／_height，
       Alt 走 i18n 側表。

   ----------------------------------------------------------------------------
   定序（COLLATE）：本檔刻意不寫 COLLATE 子句。定序在建庫時決定，且
   🔴 建庫後不可改，必須一次定對（見 docs/17-deployment.md §6 型別對照）。
   建庫時請與主站資料庫的定序決策一併確認（雖為不同庫，比對規則不一致會在
   未來若有任何文字比對整合時產生困擾）。

   本檔的三段式結構：
     第一段：CREATE TABLE（欄位、NULL 性、預設值、主鍵、叢集鍵）
     第二段：唯一鍵與索引
     第三段：外鍵約束（含刪除行為）
   外鍵集中在最後一段建立，因此 CREATE TABLE 的順序只需大致符合閱讀動線，
   不受外鍵參照方向限制。
   ============================================================================ */


/* ============================================================================
   第一段：CREATE TABLE
   ============================================================================ */

-- ----------------------------------------------------------------------------
-- 0. 共通機制先建：Locale 是全庫字典表，AdminUser／AdminRole 被最多表參照
-- ----------------------------------------------------------------------------

-- 啟用語系字典。加第三語系＝INSERT 一列，零 DDL。以 code 為自然鍵，供全庫
-- 所有 *_i18n 側表以 locale 外鍵參照，不另建 uuid 主鍵。
CREATE TABLE dbo.locale (
    code            nvarchar(10)    NOT NULL,
    name_zh         nvarchar(50)    NOT NULL,
    name_en         nvarchar(50)    NULL,
    is_default      bit             NOT NULL DEFAULT (0),
    fallback_locale nvarchar(10)    NULL,
    sort_order      int             NOT NULL DEFAULT (0),
    CONSTRAINT PK_locale PRIMARY KEY CLUSTERED (code)
);

-- 後台帳號。username 是唯一登入識別，不是 Email；強制 2FA；密碼雜湊優先
-- Argon2id 次選 bcrypt。欄位形狀比照主站 admin_user（docs/16 §3 註記「與主
-- 站同形」，見 docs/12b §7.6）。
CREATE TABLE dbo.admin_user (
    seq                          bigint IDENTITY(1,1) NOT NULL,
    id                           uniqueidentifier NOT NULL DEFAULT NEWID(),
    username                     nvarchar(191)    NOT NULL,
    email                        nvarchar(255)    NULL,
    display_name                 nvarchar(100)    NOT NULL,
    password_hash                nvarchar(255)    NOT NULL,
    must_change_password         bit              NOT NULL DEFAULT (0),
    password_changed_at          datetime2(3)     NULL,
    two_factor_enabled           bit              NOT NULL DEFAULT (0),
    two_factor_secret_encrypted  nvarchar(255)    NULL,           -- 🔐 加密欄位
    two_factor_confirmed_at      datetime2(3)     NULL,
    failed_attempt_count         int              NOT NULL DEFAULT (0),
    locked_until                 datetime2(3)     NULL,
    last_login_at                datetime2(3)     NULL,
    is_super_admin               bit              NOT NULL DEFAULT (0),
    status                       nvarchar(16)     NOT NULL DEFAULT ('active'),  -- active／disabled
    created_at                   datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    updated_at                   datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    created_by                   uniqueidentifier NULL,           -- 自參照 → admin_user.id
    updated_by                   uniqueidentifier NULL,           -- 自參照 → admin_user.id
    CONSTRAINT PK_admin_user PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_admin_user_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_admin_user_status CHECK (status IN ('active', 'disabled'))
);

-- 後台角色。沿用主站 §6 的九個角色，is_system = true 的種子資料，不可刪除
-- 但權限可調。🔴 本庫沒有 scope_mode（多俱樂部資料範圍）——單一法人，沒有
-- 這個維度，此欄位刻意不建。
CREATE TABLE dbo.admin_role (
    seq         bigint IDENTITY(1,1) NOT NULL,
    id          uniqueidentifier NOT NULL DEFAULT NEWID(),
    code        nvarchar(64)     NOT NULL,
    name_zh     nvarchar(50)     NOT NULL,
    name_en     nvarchar(50)     NULL,
    is_system   bit              NOT NULL DEFAULT (0),
    created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_admin_role PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_admin_role_seq UNIQUE CLUSTERED (seq)
);

-- (admin_user_id, admin_role_id)，多角色取聯集。純關聯表，複合主鍵即叢集鍵，
-- 不另建 uuid 欄位（docs/12 §1.2「關聯表複合主鍵」慣例）。
CREATE TABLE dbo.admin_user_role (
    admin_user_id uniqueidentifier NOT NULL,
    admin_role_id uniqueidentifier NOT NULL,
    CONSTRAINT PK_admin_user_role PRIMARY KEY CLUSTERED (admin_user_id, admin_role_id)
);

-- 權限碼字典。與主站 Permission 同形（docs/16 §2.3，2026-09-20 更新）：保留
-- module_code／submodule_code／domain／action 四欄分解（docs/12b §7.3）。
-- 本庫只有一個模組，module_code 固定為 N；submodule_code 值域是 N1–N7
-- （docs/16 §2.5 既有的 N1–N7 模組清單）。唯一不套的是 is_club_scoped——
-- 本庫沒有俱樂部維度。code 仍照 <domain>.<object>.<action> 命名慣例
-- （docs/12 §7.3）與四欄分解並存，不衝突：四欄供後台分組與過濾，code 供
-- 程式判權限。
-- ⚠️ domain 欄的具體值域（如 store／project／donation／settlement／invoice／
-- report／setting）docs/16 未逐一列舉，此處不加 CHECK 約束，留給應用層依
-- N1–N7 實際權限碼盤點後再定，已在回報列出。
CREATE TABLE dbo.permission (
    seq             bigint IDENTITY(1,1) NOT NULL,
    id              uniqueidentifier NOT NULL DEFAULT NEWID(),
    code            nvarchar(100)    NOT NULL,
    module_code     nvarchar(8)      NOT NULL DEFAULT ('N'),  -- 本庫僅一個模組，固定為 N
    submodule_code  nvarchar(8)      NOT NULL,                -- N1–N7
    domain          nvarchar(32)     NULL,                    -- ⚠️ 值域未定，見上方註解與回報
    action          nvarchar(16)     NOT NULL,
    name_zh         nvarchar(100)    NOT NULL,
    name_en         nvarchar(100)    NULL,
    is_restricted   bit              NOT NULL DEFAULT (0),   -- 退款／分潤設定／個資明細匯出等須額外授權
    sysadmin_only   bit              NOT NULL DEFAULT (0),
    created_at      datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    updated_at      datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    created_by      uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
    updated_by      uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_permission PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_permission_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_permission_module_code CHECK (module_code = 'N'),
    CONSTRAINT CK_permission_submodule_code CHECK (submodule_code IN ('N1', 'N2', 'N3', 'N4', 'N5', 'N6', 'N7')),
    CONSTRAINT CK_permission_action CHECK (action IN ('view', 'create', 'update', 'delete', 'publish', 'export', 'translate', 'execute', 'reveal'))
);

-- (admin_role_id, permission_id)。scope_type 用來表達矩陣裡不是布林的格子
-- （如 translate_only／masked），比照主站 docs/12b §7.4 的既有用法，NULL
-- 即代表 'all'（不受額外限制）。
CREATE TABLE dbo.role_permission (
    admin_role_id uniqueidentifier NOT NULL,
    permission_id uniqueidentifier NOT NULL,
    scope_type    nvarchar(32)     NULL,
    CONSTRAINT PK_role_permission PRIMARY KEY CLUSTERED (admin_role_id, permission_id)
);

-- ----------------------------------------------------------------------------
-- 1. 主站主檔的唯讀複本（2）
-- ----------------------------------------------------------------------------

-- ⚠️ 待確認（docs/16 §10）：這兩張表的存在形式規劃書沒有明文列為型別，只說
-- 「清單同步採 CSV 匯入或唯讀 API 拉取」。本檔依 docs/16 目前的讀法（要匯入
-- 就要有地方放）建表；若日後改為「每次在 N2 現場貼上名稱」，則這兩張表可能
-- 不需要獨立存在，屆時走同步鏈改規劃書與本檔。
--
-- 🔴 CharityRef／CharityProgramRef 是主站 Charity／CharityProgram 主檔的
--    唯讀複本，不是本庫的真實來源。DonationProject 絕對不得以外鍵指向
--    這兩張表——只能在選定撥付對象當下把 ref_code 與名稱值複製進
--    DonationProject 的四個快照欄位（charity_ref_code／charity_name_snapshot／
--    charity_program_ref_code／charity_program_name_snapshot）。
--    若建了外鍵，清單重新匯入覆蓋這兩張表時會連動改動已開立憑證
--    （對帳單、捐贈收據）上印出的名稱，那是法遵不允許的行為。
CREATE TABLE dbo.charity_ref (
    seq         bigint IDENTITY(1,1) NOT NULL,
    id          uniqueidentifier NOT NULL DEFAULT NEWID(),
    ref_code    nvarchar(32)     NOT NULL,
    name        nvarchar(128)    NOT NULL,
    imported_at datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    source      nvarchar(32)     NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_charity_ref PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_charity_ref_seq UNIQUE CLUSTERED (seq)
);

-- 同上警語——唯讀複本，DonationProject 不得外鍵指向本表。
CREATE TABLE dbo.charity_program_ref (
    seq             bigint IDENTITY(1,1) NOT NULL,
    id              uniqueidentifier NOT NULL DEFAULT NEWID(),
    ref_code        nvarchar(32)     NOT NULL,
    charity_ref_id  uniqueidentifier NOT NULL,
    name            nvarchar(128)    NOT NULL,
    imported_at     datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_charity_program_ref PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_charity_program_ref_seq UNIQUE CLUSTERED (seq)
);

-- ----------------------------------------------------------------------------
-- 2. N 捐款核心（8）
-- ----------------------------------------------------------------------------

-- 捐款合作店家：掃碼引流、有分潤有金流。🔴 不是 PartnerStore（主站 8.4 的
-- 會員折扣特約店家，無金流無分潤）——同一家店兩邊都做時各建一筆，不共用
-- 紀錄、不建外鍵。
CREATE TABLE dbo.donation_store (
    seq               bigint IDENTITY(1,1) NOT NULL,
    id                uniqueidentifier NOT NULL DEFAULT NEWID(),
    store_slug        nvarchar(160)    NOT NULL,   -- 系統產生、全站唯一、不可由編號推導
    category          nvarchar(32)     NULL,
    address           nvarchar(500)    NULL,
    contact_name      nvarchar(64)     NULL,
    contact_phone     nvarchar(32)     NULL,
    store_share_pct   decimal(5,2)     NOT NULL DEFAULT (0),
    logo_key          nvarchar(500)    NULL,
    logo_width        int              NULL,
    logo_height       int              NULL,
    start_on          date             NULL,
    end_on            date             NULL,
    status            nvarchar(16)     NOT NULL DEFAULT ('active'),  -- active(合作中)／inactive(已停止)
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_donation_store PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_store_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_store_status CHECK (status IN ('active', 'inactive')),
    CONSTRAINT CK_donation_store_share_pct CHECK (store_share_pct BETWEEN 0 AND 100)
);

-- 捐款項目：募款的標的。撥付對象與慈善計畫以四個快照欄位參照主站主檔，
-- 不是外鍵（見上方 CharityRef／CharityProgramRef 的警語）。不設目標金額、
-- 不設回饋品——前台不呈現募款進度，項目也不得附回饋品。
CREATE TABLE dbo.donation_project (
    seq                           bigint IDENTITY(1,1) NOT NULL,
    id                            uniqueidentifier NOT NULL DEFAULT NEWID(),
    project_slug                  nvarchar(160)    NOT NULL,
    min_amount                    int              NULL,
    max_amount                    int              NULL,
    project_share_pct             decimal(5,2)     NOT NULL DEFAULT (0),
    invoice_mode                  nvarchar(20)     NOT NULL,   -- b2c_invoice／donation_receipt
    -- 📸 以下四欄是主站 Charity／CharityProgram 的值複製快照，不是外鍵：
    charity_ref_code              nvarchar(32)     NULL,
    charity_name_snapshot         nvarchar(128)    NULL,
    charity_program_ref_code      nvarchar(32)     NULL,
    charity_program_name_snapshot nvarchar(128)    NULL,
    cover_key                     nvarchar(500)    NULL,
    cover_width                   int              NULL,
    cover_height                  int              NULL,
    sort_order                    int              NOT NULL DEFAULT (0),
    status                        nvarchar(16)     NOT NULL DEFAULT ('draft'),  -- draft／published（上下架）
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_donation_project PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_project_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_project_invoice_mode CHECK (invoice_mode IN ('b2c_invoice', 'donation_receipt')),
    CONSTRAINT CK_donation_project_status CHECK (status IN ('draft', 'published')),
    CONSTRAINT CK_donation_project_share_pct CHECK (project_share_pct BETWEEN 0 AND 100),
    CONSTRAINT CK_donation_project_amount_range CHECK (min_amount IS NULL OR max_amount IS NULL OR min_amount <= max_amount)
    -- ⚠️ 「store_share_pct + project_share_pct ≤ 100%」是跨 donation_store／
    --    donation_project 兩張表的約束，CHECK 約束無法跨表比對，依規劃書
    --    §6.2「儲存時驗證，超過即擋下」，屬 N2 應用層職責，不在 DDL 表達。
);

-- 金額選項卡 (donation_project_id, amount, sort_order)
CREATE TABLE dbo.donation_amount_option (
    seq                 bigint IDENTITY(1,1) NOT NULL,
    id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
    donation_project_id uniqueidentifier NOT NULL,
    amount              int              NOT NULL,
    sort_order          int              NOT NULL DEFAULT (0),
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_donation_amount_option PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_amount_option_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_amount_option_amount CHECK (amount > 0)
);

-- 捐款主檔。🔴 沒有 member_id 外鍵——捐款人不登入不註冊，只填姓名與 Email；
-- 會員比對只在後台查詢當下以 Email 軟性進行，不寫入本表。分潤五欄是成立
-- （paid）當下的快照，事後改設定不追溯。
CREATE TABLE dbo.donation (
    seq                         bigint IDENTITY(1,1) NOT NULL,
    id                          uniqueidentifier NOT NULL DEFAULT NEWID(),
    order_no                    nvarchar(32)     NOT NULL,
    donation_project_id         uniqueidentifier NOT NULL,
    donation_store_id           uniqueidentifier NULL,       -- 可為空：非掃碼進入
    amount                      int              NOT NULL,
    status                      nvarchar(16)     NOT NULL DEFAULT ('created'),
    created_at                  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    paid_at                     datetime2(3)     NULL,
    donor_name                  nvarchar(64)     NOT NULL,
    donor_email                 nvarchar(255)    NOT NULL,
    is_anonymous                bit              NOT NULL DEFAULT (0),
    store_share_pct_snapshot    decimal(5,2)     NOT NULL DEFAULT (0),
    project_share_pct_snapshot  decimal(5,2)     NOT NULL DEFAULT (0),
    store_amount                int              NOT NULL DEFAULT (0),
    project_amount               int              NOT NULL DEFAULT (0),
    association_amount          int              NOT NULL DEFAULT (0),
    invoice_mode                nvarchar(20)     NOT NULL,   -- 建單時自 DonationProject 快照
    refund_reason                nvarchar(255)    NULL,
    refunded_by                 uniqueidentifier NULL,       -- → admin_user.id
        updated_at                   datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by                   uniqueidentifier NULL,       -- → admin_user.id，系統自動產生的列為空
        updated_by                   uniqueidentifier NULL,       -- → admin_user.id
    CONSTRAINT PK_donation PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_status CHECK (status IN ('created', 'pending', 'paid', 'failed', 'expired', 'refunded')),
    CONSTRAINT CK_donation_invoice_mode CHECK (invoice_mode IN ('b2c_invoice', 'donation_receipt')),
    CONSTRAINT CK_donation_amount_positive CHECK (amount > 0),
    -- 🔴 硬性要求：三筆分潤金額相加必須等於捐款金額（無條件捨去尾差併入協會留存）
    CONSTRAINT CK_donation_amount_split CHECK (store_amount + project_amount + association_amount = amount),
    -- 防呆：快照的兩個百分比合計不得超過 100（N2 的即時驗證理論上已擋下，這裡加一道資料層防線）
    CONSTRAINT CK_donation_share_pct_snapshot CHECK (store_share_pct_snapshot + project_share_pct_snapshot <= 100)
);

-- 金流交易紀錄（LINE Pay 的 Request／Confirm 兩段式）。raw_response 用原生
-- json 型別，只存不查。
-- ⚠️ 待確認：status 值域（requested／confirmed／failed）規劃書與 docs/16
--    均未明文列舉，是依 §4.2「Request／Confirm 兩段式」流程與
--    requested_at／confirmed_at 兩個時間欄位推得的技術判斷，非規格明文。
CREATE TABLE dbo.donation_payment (
    seq            bigint IDENTITY(1,1) NOT NULL,
    id             uniqueidentifier NOT NULL DEFAULT NEWID(),
    donation_id    uniqueidentifier NOT NULL,
    transaction_id nvarchar(64)     NULL,       -- 金流端交易識別碼
    requested_at   datetime2(3)     NULL,
    confirmed_at   datetime2(3)     NULL,
    amount         int              NOT NULL,
    status         nvarchar(16)     NOT NULL,   -- ⚠️ 待確認，見上方欄位註解
    raw_response   json             NULL,       -- 只存不查
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_donation_payment PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_payment_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_payment_status CHECK (status IN ('requested', 'confirmed', 'failed'))
);

-- 發票／收據。issue_status 與 void_status 是兩個獨立欄位（docs/16 §4.4 已
-- 把規劃書 §5.3 的合併五值列表拆成這兩欄，本檔採用 docs/16 的拆法）。
-- 🔐 national_id_encrypted 必須加密儲存，後台預設遮罩，僅在開立收據的必要
-- 範圍內使用。
CREATE TABLE dbo.donation_invoice (
    seq                     bigint IDENTITY(1,1) NOT NULL,
    id                      uniqueidentifier NOT NULL DEFAULT NEWID(),
    donation_id             uniqueidentifier NOT NULL,
    invoice_type            nvarchar(20)     NOT NULL,   -- b2c_invoice／donation_receipt
    invoice_no              nvarchar(32)     NULL,       -- 已開立者唯一，未開立為空（見篩選唯一索引）
    issued_at               datetime2(3)     NULL,
    carrier_type            nvarchar(16)     NULL,       -- 手機條碼／捐贈碼／統編，自由文字非受限值域
    carrier_id_encrypted    nvarchar(255)    NULL,       -- 🔐 加密欄位
    tax_id                  nvarchar(16)     NULL,
    national_id_encrypted   nvarchar(255)    NULL,       -- 🔐 加密欄位，身分證字號
    receipt_address         nvarchar(500)    NULL,
    issue_status            nvarchar(16)     NOT NULL DEFAULT ('pending'),
    void_status              nvarchar(16)     NOT NULL DEFAULT ('none'),
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_donation_invoice PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_invoice_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_invoice_type CHECK (invoice_type IN ('b2c_invoice', 'donation_receipt')),
    CONSTRAINT CK_donation_invoice_issue_status CHECK (issue_status IN ('pending', 'issued', 'failed')),
    CONSTRAINT CK_donation_invoice_void_status CHECK (void_status IN ('none', 'voided', 'allowance'))
);

-- 結算單：店家與項目分開結算，各自產出對帳單。payee_id 是多型參照
-- （payee_type = 'store' 時指向 donation_store.id，= 'project' 時指向
-- donation_project.id），無法用單一外鍵表達，刻意不建 FK。
CREATE TABLE dbo.settlement (
    seq              bigint IDENTITY(1,1) NOT NULL,
    id               uniqueidentifier NOT NULL DEFAULT NEWID(),
    period_start     date             NOT NULL,
    period_end       date             NOT NULL,
    payee_type       nvarchar(16)     NOT NULL,   -- store／project
    payee_id         uniqueidentifier NOT NULL,   -- 多型參照，不建外鍵
    donation_count   int              NOT NULL DEFAULT (0),
    donation_total   int              NOT NULL DEFAULT (0),
    payable_amount   int              NOT NULL DEFAULT (0),
    status           nvarchar(16)     NOT NULL DEFAULT ('pending'),  -- pending(待結算)／settled(已結算)／paid(已付款)
    remitted_on      date             NULL,
    remit_note       nvarchar(255)    NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_settlement PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_settlement_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_settlement_payee_type CHECK (payee_type IN ('store', 'project')),
    CONSTRAINT CK_settlement_status CHECK (status IN ('pending', 'settled', 'paid')),
    CONSTRAINT CK_settlement_period CHECK (period_start <= period_end)
);

-- 結算明細：逐筆捐款的分潤金額，含退款沖回的負項（is_clawback = true）。
CREATE TABLE dbo.settlement_line (
    seq            bigint IDENTITY(1,1) NOT NULL,
    id             uniqueidentifier NOT NULL DEFAULT NEWID(),
    settlement_id  uniqueidentifier NOT NULL,
    donation_id    uniqueidentifier NOT NULL,
    share_amount   int              NOT NULL,
    is_clawback    bit              NOT NULL DEFAULT (0),
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_settlement_line PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_settlement_line_seq UNIQUE CLUSTERED (seq)
);

-- ----------------------------------------------------------------------------
-- 3. 稽核（1）
-- ----------------------------------------------------------------------------

-- 操作稽核。🔴 本庫刻意有這張表（與主站相反）——退款、分潤百分比設定、
-- 含個資的明細匯出三類操作須留稽核軌跡，是勸募法遵要求（規劃書 §11.2）。
CREATE TABLE dbo.audit_log (
    seq             bigint IDENTITY(1,1) NOT NULL,
    id              uniqueidentifier NOT NULL DEFAULT NEWID(),
    admin_user_id   uniqueidentifier NOT NULL,
    occurred_at     datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    action          nvarchar(32)     NOT NULL,
    target_type     nvarchar(32)     NOT NULL,
    target_id       uniqueidentifier NULL,       -- 多型參照，不建外鍵
    change_summary  nvarchar(500)    NULL,
    purpose_note    nvarchar(255)    NULL,       -- 個資明細匯出須記錄用途備註
    source_ip       nvarchar(45)     NULL,
    -- 🔴 append-only：刻意沒有 created_at／updated_at／created_by／updated_by。
    -- 有「誰更新了這筆稽核」等於宣告稽核可被改，軌跡就失去意義（docs/16 §0、§2.4）。
    -- occurred_at 與 admin_user_id 已承載時間與操作者。
    CONSTRAINT PK_audit_log PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_audit_log_seq UNIQUE CLUSTERED (seq)
);

-- ----------------------------------------------------------------------------
-- 4. 共通機制（其餘，Locale／AdminUser 系列已在最前面建立）
-- ----------------------------------------------------------------------------

-- 介面字串鍵與所屬分組。
CREATE TABLE dbo.ui_string (
    seq         bigint IDENTITY(1,1) NOT NULL,
    id          uniqueidentifier NOT NULL DEFAULT NEWID(),
    string_key  nvarchar(191)    NOT NULL,
    group_name  nvarchar(64)     NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_ui_string PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_ui_string_seq UNIQUE CLUSTERED (seq)
);

-- (ui_string_id, locale) → value。複合主鍵即叢集鍵。⚠️ 與下方四張
-- donation_store_i18n 等 *_i18n 側表不同：docs/16 §0 的共通欄位例外只明列
-- AdminUserRole／RolePermission／四張 *_i18n，本表不在例外名單內（它是
-- §2.5 共通機制 7 張主表之一，不計入「4 張 i18n 側表」的表數），因此仍套用
-- created_at／updated_at／created_by／updated_by。
CREATE TABLE dbo.ui_string_translation (
    ui_string_id uniqueidentifier NOT NULL,
    locale       nvarchar(10)     NOT NULL,
    value        nvarchar(max)    NOT NULL,
    -- 🔴 結構上是翻譯側表（複合主鍵＋內容欄位），不套共通欄位——同 docs/12 §2.2 的 article_i18n。
    CONSTRAINT PK_ui_string_translation PRIMARY KEY CLUSTERED (ui_string_id, locale)
);

-- 站台設定鍵值（首頁說明、感謝語樣板、捐款須知、隱私權政策、金額上下限
-- 預設值、徵信名單顯示規則等）。文案類的雙語內容另存 setting_i18n；本表的
-- value 供非文案類（如金額上下限的數值）直接存放。
CREATE TABLE dbo.setting (
    seq          bigint IDENTITY(1,1) NOT NULL,
    id           uniqueidentifier NOT NULL DEFAULT NEWID(),
    setting_key  nvarchar(191)    NOT NULL,
    value        nvarchar(max)    NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_setting PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_setting_seq UNIQUE CLUSTERED (seq)
);

-- 系統信樣板（四封：捐款感謝信／發票收據通知／開立失敗通知／退款通知）。
CREATE TABLE dbo.email_template (
    seq        bigint IDENTITY(1,1) NOT NULL,
    id         uniqueidentifier NOT NULL DEFAULT NEWID(),
    code       nvarchar(64)     NOT NULL,   -- donation_thanks／invoice_issued／invoice_failed／refund_notice
    is_active  bit              NOT NULL DEFAULT (1),
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_email_template PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_email_template_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_email_template_code CHECK (code IN ('donation_thanks', 'invoice_issued', 'invoice_failed', 'refund_notice'))
);

-- 系統信寄送紀錄。type 值域 4 個——本平台自己的四封（規劃書 §3.5）。
-- ⚠️ recipient_email／sent_at／status／email_template_id 四欄是本檔依「寄送
-- 紀錄」的功能需求補上的最小必要欄位，docs/16 §2.5 僅明文列出 type 值域，
-- 未逐欄列舉，屬合理的工程判斷而非規格新增業務規則，仍在回報中列出供確認。
CREATE TABLE dbo.email_log (
    seq                bigint IDENTITY(1,1) NOT NULL,
    id                 uniqueidentifier NOT NULL DEFAULT NEWID(),
    email_template_id  uniqueidentifier NULL,
    type               nvarchar(32)     NOT NULL,
    recipient_email    nvarchar(255)    NOT NULL,
    sent_at            datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    status             nvarchar(16)     NOT NULL DEFAULT ('sent'),   -- sent／failed
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_email_log PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_email_log_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_email_log_type CHECK (type IN ('donation_thanks', 'invoice_issued', 'invoice_failed', 'refund_notice')),
    CONSTRAINT CK_email_log_status CHECK (status IN ('sent', 'failed'))
);

-- 協會的 LINE Pay 與發票加值中心憑證。🔴 沒有 subject 欄也沒有
-- owner_club_id——本庫只服務協會一個法人；三方（俱樂部／協會／本庫）的
-- LINE Pay 商店號一律不得共用。
-- ⚠️ (channel_type, environment) 唯一鍵是依主站 PaymentChannel 的對應設計
-- （docs/12 v3.0 落差第 7 項：owner_club_id + channel_type + environment）
-- 類推而來，docs/16 未明文列出本表的唯一鍵，屬工程判斷，已在回報列出。
CREATE TABLE dbo.payment_channel (
    seq                   bigint IDENTITY(1,1) NOT NULL,
    id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
    channel_type          nvarchar(20)     NOT NULL,   -- line_pay／einvoice
    environment           nvarchar(16)     NOT NULL,   -- sandbox／production
    credential_encrypted  nvarchar(500)    NOT NULL,   -- 🔐 加密欄位
    invoice_prefix        nvarchar(16)     NULL,
    rotated_at            datetime2(3)     NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_user.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_user.id
    CONSTRAINT PK_payment_channel PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_payment_channel_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_payment_channel_type CHECK (channel_type IN ('line_pay', 'einvoice')),
    CONSTRAINT CK_payment_channel_environment CHECK (environment IN ('sandbox', 'production'))
);

-- ----------------------------------------------------------------------------
-- 5. i18n 側表（4，不計入 23 張主表）
-- ----------------------------------------------------------------------------

-- donation_store 的雙語欄位：店名、Logo 替代文字。
CREATE TABLE dbo.donation_store_i18n (
    donation_store_id uniqueidentifier NOT NULL,
    locale            nvarchar(10)     NOT NULL,
    name              nvarchar(128)    NOT NULL,
    logo_alt          nvarchar(255)    NULL,
    CONSTRAINT PK_donation_store_i18n PRIMARY KEY CLUSTERED (donation_store_id, locale)
);

-- donation_project 的雙語欄位：名稱、一句話說明、說明內文（區塊編輯器，只存
-- 不查故用原生 json 型別）、款項用途、封面替代文字。
CREATE TABLE dbo.donation_project_i18n (
    donation_project_id uniqueidentifier NOT NULL,
    locale              nvarchar(10)     NOT NULL,
    name                nvarchar(128)    NOT NULL,
    one_liner           nvarchar(255)    NULL,
    description         json             NULL,      -- 說明內文，區塊編輯器內容，只存不查
    fund_usage          nvarchar(max)    NULL,       -- 款項用途
    cover_alt           nvarchar(255)    NULL,
    CONSTRAINT PK_donation_project_i18n PRIMARY KEY CLUSTERED (donation_project_id, locale)
);

-- setting 的雙語文案內容（首頁說明、感謝語樣板、捐款須知、隱私權政策等）。
CREATE TABLE dbo.setting_i18n (
    setting_id uniqueidentifier NOT NULL,
    locale     nvarchar(10)     NOT NULL,
    value      nvarchar(max)    NULL,
    CONSTRAINT PK_setting_i18n PRIMARY KEY CLUSTERED (setting_id, locale)
);

-- email_template 的雙語樣板內容。
CREATE TABLE dbo.email_template_i18n (
    email_template_id uniqueidentifier NOT NULL,
    locale             nvarchar(10)     NOT NULL,
    subject            nvarchar(255)    NOT NULL,
    body               nvarchar(max)    NOT NULL,
    CONSTRAINT PK_email_template_i18n PRIMARY KEY CLUSTERED (email_template_id, locale)
);


/* ============================================================================
   第二段：唯一鍵與索引
   ============================================================================ */

-- ---- 唯一鍵（docs/16 §6） ----

ALTER TABLE dbo.donation_store   ADD CONSTRAINT UQ_donation_store_slug   UNIQUE (store_slug);
ALTER TABLE dbo.donation_project ADD CONSTRAINT UQ_donation_project_slug UNIQUE (project_slug);
ALTER TABLE dbo.donation         ADD CONSTRAINT UQ_donation_order_no     UNIQUE (order_no);
ALTER TABLE dbo.charity_ref         ADD CONSTRAINT UQ_charity_ref_code         UNIQUE (ref_code);
ALTER TABLE dbo.charity_program_ref ADD CONSTRAINT UQ_charity_program_ref_code UNIQUE (ref_code);
ALTER TABLE dbo.admin_user   ADD CONSTRAINT UQ_admin_user_username UNIQUE (username);  -- email 不設唯一
ALTER TABLE dbo.admin_role   ADD CONSTRAINT UQ_admin_role_code     UNIQUE (code);
ALTER TABLE dbo.permission   ADD CONSTRAINT UQ_permission_code     UNIQUE (code);
ALTER TABLE dbo.ui_string    ADD CONSTRAINT UQ_ui_string_key       UNIQUE (string_key);
ALTER TABLE dbo.setting      ADD CONSTRAINT UQ_setting_key         UNIQUE (setting_key);
ALTER TABLE dbo.email_template ADD CONSTRAINT UQ_email_template_code UNIQUE (code);

-- invoice_no：已開立者唯一，未開立為空 → 篩選唯一索引（docs/16 §6 明文要求）
CREATE UNIQUE NONCLUSTERED INDEX UX_donation_invoice_invoice_no
    ON dbo.donation_invoice (invoice_no)
    WHERE invoice_no IS NOT NULL;

-- ⚠️ 依主站 PaymentChannel 的對應唯一鍵設計類推而來，docs/16 未明文列出（見上方表定義註解）
ALTER TABLE dbo.payment_channel
    ADD CONSTRAINT UQ_payment_channel_type_env UNIQUE (channel_type, environment);

-- ---- 查詢索引（docs/16 §6） ----

CREATE INDEX IX_donation_status_paid_at         ON dbo.donation (status, paid_at);
CREATE INDEX IX_donation_store_paid_at          ON dbo.donation (donation_store_id, paid_at);
CREATE INDEX IX_donation_project_paid_at        ON dbo.donation (donation_project_id, paid_at);
-- 異常佇列：已扣款但確認失敗
CREATE INDEX IX_donation_status_created_at      ON dbo.donation (status, created_at);

-- 異常佇列：發票開立失敗
CREATE INDEX IX_donation_invoice_issue_status   ON dbo.donation_invoice (issue_status, issued_at);

-- 沖回對應
CREATE INDEX IX_settlement_line_settlement_id   ON dbo.settlement_line (settlement_id);
CREATE INDEX IX_settlement_line_donation_id     ON dbo.settlement_line (donation_id);

-- 稽核查詢
CREATE INDEX IX_audit_log_occurred_at_desc      ON dbo.audit_log (occurred_at DESC);
CREATE INDEX IX_audit_log_admin_user_occurred   ON dbo.audit_log (admin_user_id, occurred_at DESC);
CREATE INDEX IX_audit_log_target                ON dbo.audit_log (target_type, target_id);

-- 所有 *_i18n：(locale) 供翻譯狀態矩陣使用
CREATE INDEX IX_donation_store_i18n_locale      ON dbo.donation_store_i18n (locale);
CREATE INDEX IX_donation_project_i18n_locale    ON dbo.donation_project_i18n (locale);
CREATE INDEX IX_setting_i18n_locale             ON dbo.setting_i18n (locale);
CREATE INDEX IX_email_template_i18n_locale      ON dbo.email_template_i18n (locale);
CREATE INDEX IX_ui_string_translation_locale    ON dbo.ui_string_translation (locale);

-- ---- 以下為外鍵查詢效能所加的支援索引，非 docs/16 §6 明列，SQL Server
--      不會自動為外鍵欄位建索引，屬標準工程實務 ----

CREATE INDEX IX_donation_amount_option_project  ON dbo.donation_amount_option (donation_project_id);
CREATE INDEX IX_donation_payment_donation_id    ON dbo.donation_payment (donation_id);
CREATE INDEX IX_donation_invoice_donation_id    ON dbo.donation_invoice (donation_id);
CREATE INDEX IX_charity_program_ref_charity_ref ON dbo.charity_program_ref (charity_ref_id);
CREATE INDEX IX_email_log_email_template_id     ON dbo.email_log (email_template_id);


/* ============================================================================
   第三段：外鍵約束（含刪除行為，依 docs/16 §6；未明列者以註解說明判斷依據）
   ============================================================================ */

-- ---- N 捐款核心 ----

ALTER TABLE dbo.donation_amount_option
    ADD CONSTRAINT FK_donation_amount_option_project
    FOREIGN KEY (donation_project_id) REFERENCES dbo.donation_project (id)
    ON DELETE CASCADE;  -- 純子表，隨項目刪除；docs/16 §6 未列，工程判斷

ALTER TABLE dbo.donation
    ADD CONSTRAINT FK_donation_project
    FOREIGN KEY (donation_project_id) REFERENCES dbo.donation_project (id);
    -- DonationProject → Donation：RESTRICT（docs/16 §6）。T-SQL 無 RESTRICT
    -- 關鍵字，省略 ON DELETE 子句即預設 NO ACTION，效果相同：有捐款記錄的
    -- 項目不得被刪除。

ALTER TABLE dbo.donation
    ADD CONSTRAINT FK_donation_store
    FOREIGN KEY (donation_store_id) REFERENCES dbo.donation_store (id)
    ON DELETE SET NULL;  -- DonationStore → Donation：SET NULL（docs/16 §6）

ALTER TABLE dbo.donation
    ADD CONSTRAINT FK_donation_refunded_by
    FOREIGN KEY (refunded_by) REFERENCES dbo.admin_user (id);
    -- docs/16 §6 未列此關係之刪除行為；預設 NO ACTION（工程判斷：退款經辦人
    -- 帳號依系統設計只停用不刪除，見 admin_user 說明）

ALTER TABLE dbo.donation_payment
    ADD CONSTRAINT FK_donation_payment_donation
    FOREIGN KEY (donation_id) REFERENCES dbo.donation (id);
    -- Donation → DonationPayment：RESTRICT（⚖️ 法定保存，不得刪，docs/16 §6）

ALTER TABLE dbo.donation_invoice
    ADD CONSTRAINT FK_donation_invoice_donation
    FOREIGN KEY (donation_id) REFERENCES dbo.donation (id);
    -- Donation → DonationInvoice：RESTRICT（⚖️ 法定保存，不得刪，docs/16 §6）

ALTER TABLE dbo.settlement_line
    ADD CONSTRAINT FK_settlement_line_settlement
    FOREIGN KEY (settlement_id) REFERENCES dbo.settlement (id);
    -- Settlement → SettlementLine：RESTRICT（docs/16 §6）

ALTER TABLE dbo.settlement_line
    ADD CONSTRAINT FK_settlement_line_donation
    FOREIGN KEY (donation_id) REFERENCES dbo.donation (id);
    -- docs/16 §6 未列此關係；預設 NO ACTION（工程判斷：結算明細是法定保存
    -- 對象的子紀錄，不應允許刪除被引用的捐款單）

-- ---- 主站唯讀複本 ----

ALTER TABLE dbo.charity_program_ref
    ADD CONSTRAINT FK_charity_program_ref_charity_ref
    FOREIGN KEY (charity_ref_id) REFERENCES dbo.charity_ref (id);
    -- docs/16 §6 未列此關係；預設 NO ACTION（工程判斷：避免刪除仍被慈善計畫
    -- 複本引用的公益團體複本，造成孤兒列）

-- 🔴 再次強調：donation_project 的 charity_ref_code／charity_program_ref_code
-- 兩欄「不」在此建外鍵指向 charity_ref／charity_program_ref——依任務要求與
-- docs/16 §2.2 明文規定，這兩欄是值複製快照，刻意不建關聯。

-- ---- 後台帳號與權限 ----

ALTER TABLE dbo.admin_user
    ADD CONSTRAINT FK_admin_user_created_by
    FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
    -- 自參照，SQL Server 不允許自參照 FK 使用 CASCADE，維持 NO ACTION

ALTER TABLE dbo.admin_user
    ADD CONSTRAINT FK_admin_user_updated_by
    FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);

ALTER TABLE dbo.admin_user_role
    ADD CONSTRAINT FK_admin_user_role_user
    FOREIGN KEY (admin_user_id) REFERENCES dbo.admin_user (id)
    ON DELETE CASCADE;

ALTER TABLE dbo.admin_user_role
    ADD CONSTRAINT FK_admin_user_role_role
    FOREIGN KEY (admin_role_id) REFERENCES dbo.admin_role (id)
    ON DELETE CASCADE;

ALTER TABLE dbo.role_permission
    ADD CONSTRAINT FK_role_permission_role
    FOREIGN KEY (admin_role_id) REFERENCES dbo.admin_role (id)
    ON DELETE CASCADE;

ALTER TABLE dbo.role_permission
    ADD CONSTRAINT FK_role_permission_permission
    FOREIGN KEY (permission_id) REFERENCES dbo.permission (id)
    ON DELETE CASCADE;

-- ---- 稽核 ----

ALTER TABLE dbo.audit_log
    ADD CONSTRAINT FK_audit_log_admin_user
    FOREIGN KEY (admin_user_id) REFERENCES dbo.admin_user (id);
    -- 預設 NO ACTION：稽核軌跡不得因帳號變動而遺失，admin_user 只停用不刪除

-- ---- 共通機制 ----

ALTER TABLE dbo.locale
    ADD CONSTRAINT FK_locale_fallback
    FOREIGN KEY (fallback_locale) REFERENCES dbo.locale (code);
    -- 自參照，NO ACTION

ALTER TABLE dbo.ui_string_translation
    ADD CONSTRAINT FK_ui_string_translation_string
    FOREIGN KEY (ui_string_id) REFERENCES dbo.ui_string (id)
    ON DELETE CASCADE;  -- 母表 → *_i18n：CASCADE（docs/16 §6 慣例，ui_string_translation 同構）

ALTER TABLE dbo.ui_string_translation
    ADD CONSTRAINT FK_ui_string_translation_locale
    FOREIGN KEY (locale) REFERENCES dbo.locale (code);
    -- NO ACTION：不因刪除語系而連鎖刪除所有已翻譯內容，語系停用改由 Locale
    -- 本身的欄位（如未來新增 is_active）控管，不在本次範圍內新增

ALTER TABLE dbo.email_log
    ADD CONSTRAINT FK_email_log_template
    FOREIGN KEY (email_template_id) REFERENCES dbo.email_template (id);
    -- docs/16 §6 未列；預設 NO ACTION（工程判斷：樣板不應被刪除，只能停用）

-- ---- i18n 側表：母表 → *_i18n 皆為 CASCADE（docs/16 §6），
--      locale → *_i18n 皆為 NO ACTION（理由同 ui_string_translation） ----

ALTER TABLE dbo.donation_store_i18n
    ADD CONSTRAINT FK_donation_store_i18n_store
    FOREIGN KEY (donation_store_id) REFERENCES dbo.donation_store (id)
    ON DELETE CASCADE;

ALTER TABLE dbo.donation_store_i18n
    ADD CONSTRAINT FK_donation_store_i18n_locale
    FOREIGN KEY (locale) REFERENCES dbo.locale (code);

ALTER TABLE dbo.donation_project_i18n
    ADD CONSTRAINT FK_donation_project_i18n_project
    FOREIGN KEY (donation_project_id) REFERENCES dbo.donation_project (id)
    ON DELETE CASCADE;

ALTER TABLE dbo.donation_project_i18n
    ADD CONSTRAINT FK_donation_project_i18n_locale
    FOREIGN KEY (locale) REFERENCES dbo.locale (code);

ALTER TABLE dbo.setting_i18n
    ADD CONSTRAINT FK_setting_i18n_setting
    FOREIGN KEY (setting_id) REFERENCES dbo.setting (id)
    ON DELETE CASCADE;

ALTER TABLE dbo.setting_i18n
    ADD CONSTRAINT FK_setting_i18n_locale
    FOREIGN KEY (locale) REFERENCES dbo.locale (code);

ALTER TABLE dbo.email_template_i18n
    ADD CONSTRAINT FK_email_template_i18n_template
    FOREIGN KEY (email_template_id) REFERENCES dbo.email_template (id)
    ON DELETE CASCADE;

ALTER TABLE dbo.email_template_i18n
    ADD CONSTRAINT FK_email_template_i18n_locale
    FOREIGN KEY (locale) REFERENCES dbo.locale (code);

-- ---- 共通欄位 created_by／updated_by（docs/16 §0 新增規則，2026-09-20 二次
--      修訂）：一律 → admin_user.id，NO ACTION（不因帳號變動遺失歸屬紀錄，
--      admin_user 依系統設計只停用不刪除）。系統自動產生的列兩者皆為 NULL。
--      admin_user 自身的 created_by／updated_by 已在上方「後台帳號與權限」
--      段落建立，此處不重複。 ----

ALTER TABLE dbo.admin_role ADD CONSTRAINT FK_admin_role_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.admin_role ADD CONSTRAINT FK_admin_role_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.permission ADD CONSTRAINT FK_permission_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.permission ADD CONSTRAINT FK_permission_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.charity_ref ADD CONSTRAINT FK_charity_ref_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.charity_ref ADD CONSTRAINT FK_charity_ref_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.charity_program_ref ADD CONSTRAINT FK_charity_program_ref_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.charity_program_ref ADD CONSTRAINT FK_charity_program_ref_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_store ADD CONSTRAINT FK_donation_store_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_store ADD CONSTRAINT FK_donation_store_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_project ADD CONSTRAINT FK_donation_project_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_project ADD CONSTRAINT FK_donation_project_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_amount_option ADD CONSTRAINT FK_donation_amount_option_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_amount_option ADD CONSTRAINT FK_donation_amount_option_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation ADD CONSTRAINT FK_donation_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation ADD CONSTRAINT FK_donation_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_payment ADD CONSTRAINT FK_donation_payment_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_payment ADD CONSTRAINT FK_donation_payment_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_invoice ADD CONSTRAINT FK_donation_invoice_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.donation_invoice ADD CONSTRAINT FK_donation_invoice_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.settlement ADD CONSTRAINT FK_settlement_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.settlement ADD CONSTRAINT FK_settlement_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.settlement_line ADD CONSTRAINT FK_settlement_line_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.settlement_line ADD CONSTRAINT FK_settlement_line_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.ui_string ADD CONSTRAINT FK_ui_string_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.ui_string ADD CONSTRAINT FK_ui_string_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.setting ADD CONSTRAINT FK_setting_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.setting ADD CONSTRAINT FK_setting_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.email_template ADD CONSTRAINT FK_email_template_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.email_template ADD CONSTRAINT FK_email_template_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.email_log ADD CONSTRAINT FK_email_log_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.email_log ADD CONSTRAINT FK_email_log_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.payment_channel ADD CONSTRAINT FK_payment_channel_created_by FOREIGN KEY (created_by) REFERENCES dbo.admin_user (id);
ALTER TABLE dbo.payment_channel ADD CONSTRAINT FK_payment_channel_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.admin_user (id);

-- ============================================================================
-- 檔案結束。共 23 張主表 ＋ 4 張 *_i18n 側表 ＝ 27 個 CREATE TABLE。
-- ============================================================================
