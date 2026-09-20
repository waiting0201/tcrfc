-- ✅ 已於 2026-09-20 做過欄位核對（docs/16a-charity-field-audit.md），查出的 7 張
-- 缺漏已全數補上，含新增對帳兩張表。

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
        §0 已補寫這條）。例外維持不變：locales（自然鍵）、admin_user_roles／
        role_permissions（純關聯表）、四張 *_i18n 側表。created_by／
        updated_by 一律指向 admin_users(id)，系統自動產生的列為空。
        audit_logs／ui_string_translations 兩表結構上不套用（見各自表定義
        上方註解），協調者已直接修正，本檔不再變動。
     2. Permission 加回 module_code／submodule_code／domain／action 四欄
        分解（docs/16 §2.3：本庫 module_code 固定為 N，submodule_code 值域
        N1–N7，與主站同形；唯一不套的是 is_club_scoped）。

   修訂：2026-09-20（三次修訂，依協調者回饋）
     3. 物理表名全部改為 snake_case 複數（docs/12 §1.2 明訂，docs/16 §0
        已補寫），並移除 dbo. 前綴以對齊主站 DDL 的寫法（預設 schema 本來就
        是 dbo，寫不寫效果相同）。⚠️ 外鍵欄位名維持單數不變（如
        donation_id、admin_user_id、charity_ref_id），因為 docs/12 §1.2
        訂的是「<單數表名>_id」，這條沒有變。CONSTRAINT／INDEX 名稱中代表
        「這是哪張表」的那一段一併改為複數；代表「關聯到誰」的描述性字尾
        （如 FK_donations_project 的 _project、FK_audit_logs_admin_user 的
        _admin_user）維持原樣，因為那些從一開始就是欄位名的簡稱，不是物理
        表名的字面複製。

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
      （見 donations 表的 CHECK 約束）。
   6. 雙語走 <entity>_i18n 側表，唯一鍵 (<entity>_id, locale)。
   7. national_id_encrypted（身分證字號）必須是加密欄位，存密文字串，不存
      明碼；carrier_id_encrypted、two_factor_secret_encrypted、
      credential_encrypted 比照辦理。
   8. JSON 用原生 json 型別，只有 DonationPayment.raw_response 與
      donation_projects_i18n.description（說明內文，區塊編輯器內容）兩處，
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
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- ⚠️ 上面兩個 SET 是必要的，不是樣板：篩選索引（WHERE ... IS NOT NULL）、
-- 檢視上的索引與計算欄位索引都要求 QUOTED_IDENTIFIER ON，否則建立時會失敗
-- （Msg 1934）。sqlcmd 與部分用戶端預設不是 ON。

CREATE TABLE locales (
    code            nvarchar(10)    NOT NULL,
    name_zh         nvarchar(50)    NOT NULL,
    name_en         nvarchar(50)    NULL,
    is_default      bit             NOT NULL DEFAULT (0),
    fallback_locale nvarchar(10)    NULL,
    sort_order      int             NOT NULL DEFAULT (0),
    CONSTRAINT PK_locales PRIMARY KEY CLUSTERED (code)
);

-- 後台帳號。username 是唯一登入識別，不是 Email；強制 2FA；密碼雜湊優先
-- Argon2id 次選 bcrypt。欄位形狀比照主站 admin_users（docs/16 §3 註記「與主
-- 站同形」，見 docs/12b §7.6）。
CREATE TABLE admin_users (
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
    created_by                   uniqueidentifier NULL,           -- 自參照 → admin_users.id
    updated_by                   uniqueidentifier NULL,           -- 自參照 → admin_users.id
    CONSTRAINT PK_admin_users PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_admin_users_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_admin_users_status CHECK (status IN ('active', 'disabled'))
);

-- 後台角色。沿用主站 §6 的九個角色，is_system = true 的種子資料，不可刪除
-- 但權限可調。🔴 本庫沒有 scope_mode（多俱樂部資料範圍）——單一法人，沒有
-- 這個維度，此欄位刻意不建。
CREATE TABLE admin_roles (
    seq         bigint IDENTITY(1,1) NOT NULL,
    id          uniqueidentifier NOT NULL DEFAULT NEWID(),
    code        nvarchar(64)     NOT NULL,
    name_zh     nvarchar(50)     NOT NULL,
    name_en     nvarchar(50)     NULL,
    is_system   bit              NOT NULL DEFAULT (0),
    created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_admin_roles PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_admin_roles_seq UNIQUE CLUSTERED (seq)
);

-- (admin_user_id, admin_role_id)，多角色取聯集。純關聯表，複合主鍵即叢集鍵，
-- 不另建 uuid 欄位（docs/12 §1.2「關聯表複合主鍵」慣例）。
CREATE TABLE admin_user_roles (
    admin_user_id uniqueidentifier NOT NULL,
    admin_role_id uniqueidentifier NOT NULL,
    CONSTRAINT PK_admin_user_roles PRIMARY KEY CLUSTERED (admin_user_id, admin_role_id)
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
CREATE TABLE permissions (
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
    created_by      uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
    updated_by      uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_permissions PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_permissions_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_permissions_module_code CHECK (module_code = 'N'),
    CONSTRAINT CK_permissions_submodule_code CHECK (submodule_code IN ('N1', 'N2', 'N3', 'N4', 'N5', 'N6', 'N7')),
    CONSTRAINT CK_permissions_action CHECK (action IN ('view', 'create', 'update', 'delete', 'publish', 'export', 'translate', 'execute', 'reveal'))
);

-- (admin_role_id, permission_id)。scope_type 用來表達矩陣裡不是布林的格子
-- （如 translate_only／masked），比照主站 docs/12b §7.4 的既有用法，NULL
-- 即代表 'all'（不受額外限制）。
CREATE TABLE role_permissions (
    admin_role_id uniqueidentifier NOT NULL,
    permission_id uniqueidentifier NOT NULL,
    scope_type    nvarchar(32)     NULL,
    CONSTRAINT PK_role_permissions PRIMARY KEY CLUSTERED (admin_role_id, permission_id)
);

-- ----------------------------------------------------------------------------
-- 1. 主站主檔的唯讀複本（2）
-- ----------------------------------------------------------------------------

-- ⚠️ 待確認（docs/16 §10）：這兩張表的存在形式規劃書沒有明文列為型別，只說
-- 「清單同步採 CSV 匯入或唯讀 API 拉取」。本檔依 docs/16 目前的讀法（要匯入
-- 就要有地方放）建表；若日後改為「每次在 N2 現場貼上名稱」，則這兩張表可能
-- 不需要獨立存在，屆時走同步鏈改規劃書與本檔。
--
-- 每日對帳批次（規劃書 §4.5 行 402–406：每日與 LINE Pay 交易明細比對）。
-- 🔴「對帳結果保留供稽核」——本表與 reconciliation_discrepancies 不得清除。
CREATE TABLE reconciliation_runs (
    id                      uniqueidentifier NOT NULL DEFAULT NEWID(),
    row_seq                 bigint IDENTITY(1,1) NOT NULL,
    run_on                  date             NOT NULL,
    source                  nvarchar(32)     NOT NULL DEFAULT ('linepay'),
    compared_count          int              NOT NULL DEFAULT 0,
    matched_count           int              NOT NULL DEFAULT 0,
    discrepancy_count       int              NOT NULL DEFAULT 0,
    ran_at                  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    status                  nvarchar(16)     NOT NULL,
    created_at              datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    updated_at              datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    created_by              uniqueidentifier NULL,
    updated_by              uniqueidentifier NULL,
    CONSTRAINT PK_reconciliation_runs PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_reconciliation_runs_seq UNIQUE CLUSTERED (row_seq),
    CONSTRAINT UQ_reconciliation_runs_run_on UNIQUE (run_on, source)
);

-- 對帳差異明細。三種類型對應規劃書字面：本站有金流無／金流有本站無／金額不符。
CREATE TABLE reconciliation_discrepancies (
    id                      uniqueidentifier NOT NULL DEFAULT NEWID(),
    row_seq                 bigint IDENTITY(1,1) NOT NULL,
    reconciliation_run_id   uniqueidentifier NOT NULL,
    discrepancy_type        nvarchar(24)     NOT NULL,
    donation_id             uniqueidentifier NULL,       -- 金流有本站無時為空
    gateway_transaction_id  nvarchar(64)     NULL,       -- 本站有金流無時為空
    site_amount             int              NULL,
    gateway_amount          int              NULL,
    resolution_status       nvarchar(16)     NOT NULL DEFAULT ('pending'),
    resolved_by             uniqueidentifier NULL,
    resolve_note            nvarchar(255)    NULL,
    created_at              datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    updated_at              datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    created_by              uniqueidentifier NULL,
    updated_by              uniqueidentifier NULL,
    CONSTRAINT PK_reconciliation_discrepancies PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_reconciliation_discrepancies_seq UNIQUE CLUSTERED (row_seq),
    CONSTRAINT CK_reconciliation_discrepancies_type
        CHECK (discrepancy_type IN ('site_only', 'gateway_only', 'amount_mismatch'))
);

-- 🔴 CharityRef／CharityProgramRef 是主站 Charity／CharityProgram 主檔的
--    唯讀複本，不是本庫的真實來源。DonationProject 絕對不得以外鍵指向
--    這兩張表——只能在選定撥付對象當下把 ref_code 與名稱值複製進
--    DonationProject 的四個快照欄位（charity_ref_code／charity_name_snapshot／
--    charity_program_ref_code／charity_program_name_snapshot）。
--    若建了外鍵，清單重新匯入覆蓋這兩張表時會連動改動已開立憑證
--    （對帳單、捐贈收據）上印出的名稱，那是法遵不允許的行為。
CREATE TABLE charity_refs (
    seq         bigint IDENTITY(1,1) NOT NULL,
    id          uniqueidentifier NOT NULL DEFAULT NEWID(),
    ref_code    nvarchar(32)     NOT NULL,
    name        nvarchar(128)    NOT NULL,
    imported_at datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    source      nvarchar(32)     NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_charity_refs PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_charity_refs_seq UNIQUE CLUSTERED (seq)
);

-- 同上警語——唯讀複本，DonationProject 不得外鍵指向本表。
CREATE TABLE charity_program_refs (
    seq             bigint IDENTITY(1,1) NOT NULL,
    id              uniqueidentifier NOT NULL DEFAULT NEWID(),
    ref_code        nvarchar(32)     NOT NULL,
    charity_ref_id  uniqueidentifier NOT NULL,
    name            nvarchar(128)    NOT NULL,
    imported_at     datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_charity_program_refs PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_charity_program_refs_seq UNIQUE CLUSTERED (seq)
);

-- ----------------------------------------------------------------------------
-- 2. N 捐款核心（8）
-- ----------------------------------------------------------------------------

-- 捐款合作店家：掃碼引流、有分潤有金流。🔴 不是 PartnerStore（主站 8.4 的
-- 會員折扣特約店家，無金流無分潤）——同一家店兩邊都做時各建一筆，不共用
-- 紀錄、不建外鍵。
CREATE TABLE donation_stores (
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
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_donation_stores PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_stores_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_stores_status CHECK (status IN ('active', 'inactive')),
    CONSTRAINT CK_donation_stores_share_pct CHECK (store_share_pct BETWEEN 0 AND 100)
);

-- 捐款項目：募款的標的。撥付對象與慈善計畫以四個快照欄位參照主站主檔，
-- 不是外鍵（見上方 CharityRef／CharityProgramRef 的警語）。不設目標金額、
-- 不設回饋品——前台不呈現募款進度，項目也不得附回饋品。
CREATE TABLE donation_projects (
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
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_donation_projects PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_projects_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_projects_invoice_mode CHECK (invoice_mode IN ('b2c_invoice', 'donation_receipt')),
    CONSTRAINT CK_donation_projects_status CHECK (status IN ('draft', 'published')),
    CONSTRAINT CK_donation_projects_share_pct CHECK (project_share_pct BETWEEN 0 AND 100),
    CONSTRAINT CK_donation_projects_amount_range CHECK (min_amount IS NULL OR max_amount IS NULL OR min_amount <= max_amount)
    -- ⚠️ 「store_share_pct + project_share_pct ≤ 100%」是跨 donation_stores／
    --    donation_projects 兩張表的約束，CHECK 約束無法跨表比對，依規劃書
    --    §6.2「儲存時驗證，超過即擋下」，屬 N2 應用層職責，不在 DDL 表達。
);

-- 金額選項卡 (donation_project_id, amount, sort_order)
CREATE TABLE donation_amount_options (
    seq                 bigint IDENTITY(1,1) NOT NULL,
    id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
    donation_project_id uniqueidentifier NOT NULL,
    amount              int              NOT NULL,
    sort_order          int              NOT NULL DEFAULT (0),
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_donation_amount_options PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_amount_options_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_amount_options_amount CHECK (amount > 0)
);

-- 捐款主檔。🔴 沒有 member_id 外鍵——捐款人不登入不註冊，只填姓名與 Email；
-- 會員比對只在後台查詢當下以 Email 軟性進行，不寫入本表。分潤五欄是成立
-- （paid）當下的快照，事後改設定不追溯。
CREATE TABLE donations (
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
    refunded_by                 uniqueidentifier NULL,       -- → admin_users.id
        updated_at                   datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by                   uniqueidentifier NULL,       -- → admin_users.id，系統自動產生的列為空
        updated_by                   uniqueidentifier NULL,       -- → admin_users.id
    CONSTRAINT PK_donations PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donations_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donations_status CHECK (status IN ('created', 'pending', 'paid', 'failed', 'expired', 'refunded')),
    CONSTRAINT CK_donations_invoice_mode CHECK (invoice_mode IN ('b2c_invoice', 'donation_receipt')),
    CONSTRAINT CK_donations_amount_positive CHECK (amount > 0),
    -- 🔴 硬性要求：三筆分潤金額相加必須等於捐款金額（無條件捨去尾差併入協會留存）
    CONSTRAINT CK_donations_amount_split CHECK (store_amount + project_amount + association_amount = amount),
    -- 防呆：快照的兩個百分比合計不得超過 100（N2 的即時驗證理論上已擋下，這裡加一道資料層防線）
    CONSTRAINT CK_donations_share_pct_snapshot CHECK (store_share_pct_snapshot + project_share_pct_snapshot <= 100)
);

-- 金流交易紀錄（LINE Pay 的 Request／Confirm 兩段式）。raw_response 用原生
-- json 型別，只存不查。
-- ⚠️ 待確認：status 值域（requested／confirmed／failed）規劃書與 docs/16
--    均未明文列舉，是依 §4.2「Request／Confirm 兩段式」流程與
--    requested_at／confirmed_at 兩個時間欄位推得的技術判斷，非規格明文。
CREATE TABLE donation_payments (
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
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_donation_payments PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_payments_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_payments_status CHECK (status IN ('requested', 'confirmed', 'failed'))
);

-- 發票／收據。issue_status 與 void_status 是兩個獨立欄位（docs/16 §4.4 已
-- 把規劃書 §5.3 的合併五值列表拆成這兩欄，本檔採用 docs/16 的拆法）。
-- 🔐 national_id_encrypted 必須加密儲存，後台預設遮罩，僅在開立收據的必要
-- 範圍內使用。
CREATE TABLE donation_invoices (
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
    invoice_title            nvarchar(128)    NULL,       -- 統編模式必填（行 433）
    receipt_title            nvarchar(128)    NULL,       -- 預設帶入捐款人姓名，可修改（行 439）
    is_annual_summary        bit              NOT NULL DEFAULT 0,  -- 單筆／年度彙總（行 442）
    issue_status            nvarchar(16)     NOT NULL DEFAULT ('pending'),
    void_status              nvarchar(16)     NOT NULL DEFAULT ('none'),
    void_reason              nvarchar(255)    NULL,       -- 作廢／折讓原因（行 455）
    voided_by                uniqueidentifier NULL,       -- 經辦人（行 455）
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_donation_invoices PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_donation_invoices_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_donation_invoices_type CHECK (invoice_type IN ('b2c_invoice', 'donation_receipt')),
    CONSTRAINT CK_donation_invoices_issue_status CHECK (issue_status IN ('pending', 'issued', 'failed')),
    CONSTRAINT CK_donation_invoices_void_status CHECK (void_status IN ('none', 'voided', 'allowance'))
);

-- 結算單：店家與項目分開結算，各自產出對帳單。payee_id 是多型參照
-- （payee_type = 'store' 時指向 donation_stores.id，= 'project' 時指向
-- donation_projects.id），無法用單一外鍵表達，刻意不建 FK。
CREATE TABLE settlements (
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
    remit_method             nvarchar(32)     NULL,       -- 匯款方式（行 529「日期、方式與備註」）
    remit_note       nvarchar(255)    NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_settlements PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_settlements_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_settlements_payee_type CHECK (payee_type IN ('store', 'project')),
    CONSTRAINT CK_settlements_status CHECK (status IN ('pending', 'settled', 'paid')),
    CONSTRAINT CK_settlements_period CHECK (period_start <= period_end)
);

-- 結算明細：逐筆捐款的分潤金額，含退款沖回的負項（is_clawback = true）。
CREATE TABLE settlement_lines (
    seq            bigint IDENTITY(1,1) NOT NULL,
    id             uniqueidentifier NOT NULL DEFAULT NEWID(),
    settlement_id  uniqueidentifier NOT NULL,
    donation_id    uniqueidentifier NOT NULL,
    share_amount   int              NOT NULL,
    is_clawback    bit              NOT NULL DEFAULT (0),
    clawback_reason          nvarchar(255)    NULL,       -- 沖回原因（行 622「明列沖回原因」）
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_settlement_lines PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_settlement_lines_seq UNIQUE CLUSTERED (seq)
);

-- ----------------------------------------------------------------------------
-- 3. 稽核（1）
-- ----------------------------------------------------------------------------

-- 操作稽核。🔴 本庫刻意有這張表（與主站相反）——退款、分潤百分比設定、
-- 含個資的明細匯出三類操作須留稽核軌跡，是勸募法遵要求（規劃書 §11.2）。
CREATE TABLE audit_logs (
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
    CONSTRAINT PK_audit_logs PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_audit_logs_seq UNIQUE CLUSTERED (seq)
);

-- ----------------------------------------------------------------------------
-- 4. 共通機制（其餘，Locale／AdminUser 系列已在最前面建立）
-- ----------------------------------------------------------------------------

-- 介面字串鍵與所屬分組。
CREATE TABLE ui_strings (
    seq         bigint IDENTITY(1,1) NOT NULL,
    id          uniqueidentifier NOT NULL DEFAULT NEWID(),
    string_key  nvarchar(191)    NOT NULL,
    group_name  nvarchar(64)     NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_ui_strings PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_ui_strings_seq UNIQUE CLUSTERED (seq)
);

-- (ui_string_id, locale) → value。複合主鍵即叢集鍵。⚠️ 與下方四張
-- donation_stores_i18n 等 *_i18n 側表不同：docs/16 §0 的共通欄位例外只明列
-- AdminUserRole／RolePermission／四張 *_i18n，本表不在例外名單內（它是
-- §2.5 共通機制 7 張主表之一，不計入「4 張 i18n 側表」的表數），因此仍套用
-- created_at／updated_at／created_by／updated_by。
CREATE TABLE ui_string_translations (
    ui_string_id uniqueidentifier NOT NULL,
    locale       nvarchar(10)     NOT NULL,
    value        nvarchar(max)    NOT NULL,
    -- 🔴 結構上是翻譯側表（複合主鍵＋內容欄位），不套共通欄位——同 docs/12 §2.2 的 article_i18n。
    CONSTRAINT PK_ui_string_translations PRIMARY KEY CLUSTERED (ui_string_id, locale)
);

-- 站台設定鍵值（首頁說明、感謝語樣板、捐款須知、隱私權政策、金額上下限
-- 預設值、徵信名單顯示規則等）。文案類的雙語內容另存 settings_i18n；本表的
-- value 供非文案類（如金額上下限的數值）直接存放。
CREATE TABLE settings (
    seq          bigint IDENTITY(1,1) NOT NULL,
    id           uniqueidentifier NOT NULL DEFAULT NEWID(),
    setting_key  nvarchar(191)    NOT NULL,
    value        nvarchar(max)    NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_settings PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_settings_seq UNIQUE CLUSTERED (seq)
);

-- 系統信樣板（四封：捐款感謝信／發票收據通知／開立失敗通知／退款通知）。
CREATE TABLE email_templates (
    seq        bigint IDENTITY(1,1) NOT NULL,
    id         uniqueidentifier NOT NULL DEFAULT NEWID(),
    code       nvarchar(64)     NOT NULL,   -- donation_thanks／invoice_issued／invoice_failed／refund_notice
    is_active  bit              NOT NULL DEFAULT (1),
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_email_templates PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_email_templates_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_email_templates_code CHECK (code IN ('donation_thanks', 'invoice_issued', 'invoice_failed', 'refund_notice'))
);

-- 系統信寄送紀錄。type 值域 4 個——本平台自己的四封（規劃書 §3.5）。
-- ⚠️ recipient_email／sent_at／status／email_template_id 四欄是本檔依「寄送
-- 紀錄」的功能需求補上的最小必要欄位，docs/16 §2.5 僅明文列出 type 值域，
-- 未逐欄列舉，屬合理的工程判斷而非規格新增業務規則，仍在回報中列出供確認。
CREATE TABLE email_logs (
    seq                bigint IDENTITY(1,1) NOT NULL,
    id                 uniqueidentifier NOT NULL DEFAULT NEWID(),
    email_template_id  uniqueidentifier NULL,
    type               nvarchar(32)     NOT NULL,
    recipient_email    nvarchar(255)    NOT NULL,
    sent_at            datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    status             nvarchar(16)     NOT NULL DEFAULT ('sent'),   -- sent／failed
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_email_logs PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_email_logs_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_email_logs_type CHECK (type IN ('donation_thanks', 'invoice_issued', 'invoice_failed', 'refund_notice')),
    CONSTRAINT CK_email_logs_status CHECK (status IN ('sent', 'failed'))
);

-- 協會的 LINE Pay 與發票加值中心憑證。🔴 沒有 subject 欄也沒有
-- owner_club_id——本庫只服務協會一個法人；三方（俱樂部／協會／本庫）的
-- LINE Pay 商店號一律不得共用。
-- ⚠️ (channel_type, environment) 唯一鍵是依主站 PaymentChannel 的對應設計
-- （docs/12 v3.0 落差第 7 項：owner_club_id + channel_type + environment）
-- 類推而來，docs/16 未明文列出本表的唯一鍵，屬工程判斷，已在回報列出。
CREATE TABLE payment_channels (
    seq                   bigint IDENTITY(1,1) NOT NULL,
    id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
    channel_type          nvarchar(20)     NOT NULL,   -- line_pay／einvoice
    environment           nvarchar(16)     NOT NULL,   -- sandbox／production
    credential_encrypted  nvarchar(500)    NOT NULL,   -- 🔐 加密欄位
    invoice_prefix        nvarchar(16)     NULL,
    rotated_at            datetime2(3)     NULL,
        created_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        updated_at  datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        created_by  uniqueidentifier NULL,           -- → admin_users.id，系統自動產生的列為空
        updated_by  uniqueidentifier NULL,           -- → admin_users.id
    CONSTRAINT PK_payment_channels PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_payment_channels_seq UNIQUE CLUSTERED (seq),
    CONSTRAINT CK_payment_channels_type CHECK (channel_type IN ('line_pay', 'einvoice')),
    CONSTRAINT CK_payment_channels_environment CHECK (environment IN ('sandbox', 'production'))
);

-- ----------------------------------------------------------------------------
-- 5. i18n 側表（4，不計入 23 張主表）
-- ----------------------------------------------------------------------------

-- donation_stores 的雙語欄位：店名、Logo 替代文字。
CREATE TABLE donation_stores_i18n (
    donation_store_id uniqueidentifier NOT NULL,
    locale            nvarchar(10)     NOT NULL,
    name              nvarchar(128)    NOT NULL,
    logo_alt          nvarchar(255)    NULL,
    CONSTRAINT PK_donation_stores_i18n PRIMARY KEY CLUSTERED (donation_store_id, locale)
);

-- donation_projects 的雙語欄位：名稱、一句話說明、說明內文（區塊編輯器，只存
-- 不查故用原生 json 型別）、款項用途、封面替代文字。
CREATE TABLE donation_projects_i18n (
    donation_project_id uniqueidentifier NOT NULL,
    locale              nvarchar(10)     NOT NULL,
    name                nvarchar(128)    NOT NULL,
    one_liner           nvarchar(255)    NULL,
    description         json             NULL,      -- 說明內文，區塊編輯器內容，只存不查
    fund_usage          nvarchar(max)    NULL,       -- 款項用途
    cover_alt           nvarchar(255)    NULL,
    CONSTRAINT PK_donation_projects_i18n PRIMARY KEY CLUSTERED (donation_project_id, locale)
);

-- setting 的雙語文案內容（首頁說明、感謝語樣板、捐款須知、隱私權政策等）。
CREATE TABLE settings_i18n (
    setting_id uniqueidentifier NOT NULL,
    locale     nvarchar(10)     NOT NULL,
    value      nvarchar(max)    NULL,
    CONSTRAINT PK_settings_i18n PRIMARY KEY CLUSTERED (setting_id, locale)
);

-- email_templates 的雙語樣板內容。
CREATE TABLE email_templates_i18n (
    email_template_id uniqueidentifier NOT NULL,
    locale             nvarchar(10)     NOT NULL,
    subject            nvarchar(255)    NOT NULL,
    body               nvarchar(max)    NOT NULL,
    CONSTRAINT PK_email_templates_i18n PRIMARY KEY CLUSTERED (email_template_id, locale)
);


/* ============================================================================
   第二段：唯一鍵與索引
   ============================================================================ */

-- ---- 唯一鍵（docs/16 §6） ----

ALTER TABLE donation_stores   ADD CONSTRAINT UQ_donation_stores_slug   UNIQUE (store_slug);
ALTER TABLE donation_projects ADD CONSTRAINT UQ_donation_projects_slug UNIQUE (project_slug);
ALTER TABLE donations         ADD CONSTRAINT UQ_donations_order_no     UNIQUE (order_no);
ALTER TABLE charity_refs         ADD CONSTRAINT UQ_charity_refs_code         UNIQUE (ref_code);
ALTER TABLE charity_program_refs ADD CONSTRAINT UQ_charity_program_refs_code UNIQUE (ref_code);
ALTER TABLE admin_users   ADD CONSTRAINT UQ_admin_users_username UNIQUE (username);  -- email 不設唯一
ALTER TABLE admin_roles   ADD CONSTRAINT UQ_admin_roles_code     UNIQUE (code);
ALTER TABLE permissions   ADD CONSTRAINT UQ_permissions_code     UNIQUE (code);
ALTER TABLE ui_strings    ADD CONSTRAINT UQ_ui_strings_key       UNIQUE (string_key);
ALTER TABLE settings      ADD CONSTRAINT UQ_settings_key         UNIQUE (setting_key);
ALTER TABLE email_templates ADD CONSTRAINT UQ_email_templates_code UNIQUE (code);

-- invoice_no：已開立者唯一，未開立為空 → 篩選唯一索引（docs/16 §6 明文要求）
CREATE UNIQUE NONCLUSTERED INDEX UX_donation_invoices_invoice_no
    ON donation_invoices (invoice_no)
    WHERE invoice_no IS NOT NULL;

-- 對帳（規劃書 §4.5）
CREATE INDEX IX_reconciliation_runs_run_on ON reconciliation_runs (run_on DESC);
CREATE INDEX IX_reconciliation_discrepancies_status ON reconciliation_discrepancies (resolution_status, reconciliation_run_id);

-- ⚠️ 依主站 PaymentChannel 的對應唯一鍵設計類推而來，docs/16 未明文列出（見上方表定義註解）
ALTER TABLE payment_channels
    ADD CONSTRAINT UQ_payment_channels_type_env UNIQUE (channel_type, environment);

-- ---- 查詢索引（docs/16 §6） ----

CREATE INDEX IX_donations_status_paid_at         ON donations (status, paid_at);
CREATE INDEX IX_donations_store_paid_at          ON donations (donation_store_id, paid_at);
CREATE INDEX IX_donations_project_paid_at        ON donations (donation_project_id, paid_at);
-- 異常佇列：已扣款但確認失敗
CREATE INDEX IX_donations_status_created_at      ON donations (status, created_at);

-- 異常佇列：發票開立失敗
CREATE INDEX IX_donation_invoices_issue_status   ON donation_invoices (issue_status, issued_at);

-- 沖回對應
CREATE INDEX IX_settlement_lines_settlement_id   ON settlement_lines (settlement_id);
CREATE INDEX IX_settlement_lines_donation_id     ON settlement_lines (donation_id);

-- 稽核查詢
CREATE INDEX IX_audit_logs_occurred_at_desc      ON audit_logs (occurred_at DESC);
CREATE INDEX IX_audit_logs_admin_user_occurred   ON audit_logs (admin_user_id, occurred_at DESC);
CREATE INDEX IX_audit_logs_target                ON audit_logs (target_type, target_id);

-- 所有 *_i18n：(locale) 供翻譯狀態矩陣使用
CREATE INDEX IX_donation_stores_i18n_locale      ON donation_stores_i18n (locale);
CREATE INDEX IX_donation_projects_i18n_locale    ON donation_projects_i18n (locale);
CREATE INDEX IX_settings_i18n_locale             ON settings_i18n (locale);
CREATE INDEX IX_email_templates_i18n_locale      ON email_templates_i18n (locale);
CREATE INDEX IX_ui_string_translations_locale    ON ui_string_translations (locale);

-- ---- 以下為外鍵查詢效能所加的支援索引，非 docs/16 §6 明列，SQL Server
--      不會自動為外鍵欄位建索引，屬標準工程實務 ----

CREATE INDEX IX_donation_amount_options_project  ON donation_amount_options (donation_project_id);
CREATE INDEX IX_donation_payments_donation_id    ON donation_payments (donation_id);
CREATE INDEX IX_donation_invoices_donation_id    ON donation_invoices (donation_id);
CREATE INDEX IX_charity_program_refs_charity_ref ON charity_program_refs (charity_ref_id);
CREATE INDEX IX_email_logs_email_template_id     ON email_logs (email_template_id);


/* ============================================================================
   第三段：外鍵約束（含刪除行為，依 docs/16 §6；未明列者以註解說明判斷依據）
   ============================================================================ */

-- ---- N 捐款核心 ----

ALTER TABLE donation_amount_options
    ADD CONSTRAINT FK_donation_amount_options_project
    FOREIGN KEY (donation_project_id) REFERENCES donation_projects (id)
    ON DELETE CASCADE;  -- 純子表，隨項目刪除；docs/16 §6 未列，工程判斷

ALTER TABLE donations
    ADD CONSTRAINT FK_donations_project
    FOREIGN KEY (donation_project_id) REFERENCES donation_projects (id);
    -- DonationProject → Donation：RESTRICT（docs/16 §6）。T-SQL 無 RESTRICT
    -- 關鍵字，省略 ON DELETE 子句即預設 NO ACTION，效果相同：有捐款記錄的
    -- 項目不得被刪除。

ALTER TABLE donations
    ADD CONSTRAINT FK_donations_store
    FOREIGN KEY (donation_store_id) REFERENCES donation_stores (id)
    ON DELETE SET NULL;  -- DonationStore → Donation：SET NULL（docs/16 §6）

ALTER TABLE donations
    ADD CONSTRAINT FK_donations_refunded_by
    FOREIGN KEY (refunded_by) REFERENCES admin_users (id);
    -- docs/16 §6 未列此關係之刪除行為；預設 NO ACTION（工程判斷：退款經辦人
    -- 帳號依系統設計只停用不刪除，見 admin_users 說明）

ALTER TABLE donation_payments
    ADD CONSTRAINT FK_donation_payments_donation
    FOREIGN KEY (donation_id) REFERENCES donations (id);
    -- Donation → DonationPayment：RESTRICT（⚖️ 法定保存，不得刪，docs/16 §6）

ALTER TABLE donation_invoices
    ADD CONSTRAINT FK_donation_invoices_donation
    FOREIGN KEY (donation_id) REFERENCES donations (id);
    -- Donation → DonationInvoice：RESTRICT（⚖️ 法定保存，不得刪，docs/16 §6）

ALTER TABLE settlement_lines
    ADD CONSTRAINT FK_settlement_lines_settlement
    FOREIGN KEY (settlement_id) REFERENCES settlements (id);

ALTER TABLE reconciliation_discrepancies
    ADD CONSTRAINT FK_reconciliation_discrepancies_run
    FOREIGN KEY (reconciliation_run_id) REFERENCES reconciliation_runs (id);

ALTER TABLE reconciliation_discrepancies
    ADD CONSTRAINT FK_reconciliation_discrepancies_donation
    FOREIGN KEY (donation_id) REFERENCES donations (id);

ALTER TABLE donation_invoices
    ADD CONSTRAINT FK_donation_invoices_voided_by
    FOREIGN KEY (voided_by) REFERENCES admin_users (id);
    -- Settlement → SettlementLine：RESTRICT（docs/16 §6）

ALTER TABLE settlement_lines
    ADD CONSTRAINT FK_settlement_lines_donation
    FOREIGN KEY (donation_id) REFERENCES donations (id);
    -- docs/16 §6 未列此關係；預設 NO ACTION（工程判斷：結算明細是法定保存
    -- 對象的子紀錄，不應允許刪除被引用的捐款單）

-- ---- 主站唯讀複本 ----

ALTER TABLE charity_program_refs
    ADD CONSTRAINT FK_charity_program_refs_charity_ref
    FOREIGN KEY (charity_ref_id) REFERENCES charity_refs (id);
    -- docs/16 §6 未列此關係；預設 NO ACTION（工程判斷：避免刪除仍被慈善計畫
    -- 複本引用的公益團體複本，造成孤兒列）

-- 🔴 再次強調：donation_projects 的 charity_ref_code／charity_program_ref_code
-- 兩欄「不」在此建外鍵指向 charity_refs／charity_program_refs——依任務要求與
-- docs/16 §2.2 明文規定，這兩欄是值複製快照，刻意不建關聯。

-- ---- 後台帳號與權限 ----

ALTER TABLE admin_users
    ADD CONSTRAINT FK_admin_users_created_by
    FOREIGN KEY (created_by) REFERENCES admin_users (id);
    -- 自參照，SQL Server 不允許自參照 FK 使用 CASCADE，維持 NO ACTION

ALTER TABLE admin_users
    ADD CONSTRAINT FK_admin_users_updated_by
    FOREIGN KEY (updated_by) REFERENCES admin_users (id);

ALTER TABLE admin_user_roles
    ADD CONSTRAINT FK_admin_user_roles_user
    FOREIGN KEY (admin_user_id) REFERENCES admin_users (id)
    ON DELETE CASCADE;

ALTER TABLE admin_user_roles
    ADD CONSTRAINT FK_admin_user_roles_role
    FOREIGN KEY (admin_role_id) REFERENCES admin_roles (id)
    ON DELETE CASCADE;

ALTER TABLE role_permissions
    ADD CONSTRAINT FK_role_permissions_role
    FOREIGN KEY (admin_role_id) REFERENCES admin_roles (id)
    ON DELETE CASCADE;

ALTER TABLE role_permissions
    ADD CONSTRAINT FK_role_permissions_permission
    FOREIGN KEY (permission_id) REFERENCES permissions (id)
    ON DELETE CASCADE;

-- ---- 稽核 ----

ALTER TABLE audit_logs
    ADD CONSTRAINT FK_audit_logs_admin_user
    FOREIGN KEY (admin_user_id) REFERENCES admin_users (id);
    -- 預設 NO ACTION：稽核軌跡不得因帳號變動而遺失，admin_user 只停用不刪除

-- ---- 共通機制 ----

ALTER TABLE locales
    ADD CONSTRAINT FK_locales_fallback
    FOREIGN KEY (fallback_locale) REFERENCES locales (code);
    -- 自參照，NO ACTION

ALTER TABLE ui_string_translations
    ADD CONSTRAINT FK_ui_string_translations_string
    FOREIGN KEY (ui_string_id) REFERENCES ui_strings (id)
    ON DELETE CASCADE;  -- 母表 → *_i18n：CASCADE（docs/16 §6 慣例，ui_string_translation 同構）

ALTER TABLE ui_string_translations
    ADD CONSTRAINT FK_ui_string_translations_locale
    FOREIGN KEY (locale) REFERENCES locales (code);
    -- NO ACTION：不因刪除語系而連鎖刪除所有已翻譯內容，語系停用改由 Locale
    -- 本身的欄位（如未來新增 is_active）控管，不在本次範圍內新增

ALTER TABLE email_logs
    ADD CONSTRAINT FK_email_logs_template
    FOREIGN KEY (email_template_id) REFERENCES email_templates (id);
    -- docs/16 §6 未列；預設 NO ACTION（工程判斷：樣板不應被刪除，只能停用）

-- ---- i18n 側表：母表 → *_i18n 皆為 CASCADE（docs/16 §6），
--      locale → *_i18n 皆為 NO ACTION（理由同 ui_string_translations） ----

ALTER TABLE donation_stores_i18n
    ADD CONSTRAINT FK_donation_stores_i18n_store
    FOREIGN KEY (donation_store_id) REFERENCES donation_stores (id)
    ON DELETE CASCADE;

ALTER TABLE donation_stores_i18n
    ADD CONSTRAINT FK_donation_stores_i18n_locale
    FOREIGN KEY (locale) REFERENCES locales (code);

ALTER TABLE donation_projects_i18n
    ADD CONSTRAINT FK_donation_projects_i18n_project
    FOREIGN KEY (donation_project_id) REFERENCES donation_projects (id)
    ON DELETE CASCADE;

ALTER TABLE donation_projects_i18n
    ADD CONSTRAINT FK_donation_projects_i18n_locale
    FOREIGN KEY (locale) REFERENCES locales (code);

ALTER TABLE settings_i18n
    ADD CONSTRAINT FK_settings_i18n_setting
    FOREIGN KEY (setting_id) REFERENCES settings (id)
    ON DELETE CASCADE;

ALTER TABLE settings_i18n
    ADD CONSTRAINT FK_settings_i18n_locale
    FOREIGN KEY (locale) REFERENCES locales (code);

ALTER TABLE email_templates_i18n
    ADD CONSTRAINT FK_email_templates_i18n_template
    FOREIGN KEY (email_template_id) REFERENCES email_templates (id)
    ON DELETE CASCADE;

ALTER TABLE email_templates_i18n
    ADD CONSTRAINT FK_email_templates_i18n_locale
    FOREIGN KEY (locale) REFERENCES locales (code);

-- ---- 共通欄位 created_by／updated_by（docs/16 §0 新增規則，2026-09-20 二次
--      修訂）：一律 → admin_users.id，NO ACTION（不因帳號變動遺失歸屬紀錄，
--      admin_users 依系統設計只停用不刪除）。系統自動產生的列兩者皆為 NULL。
--      admin_users 自身的 created_by／updated_by 已在上方「後台帳號與權限」
--      段落建立，此處不重複。 ----

ALTER TABLE admin_roles ADD CONSTRAINT FK_admin_roles_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE admin_roles ADD CONSTRAINT FK_admin_roles_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE permissions ADD CONSTRAINT FK_permissions_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE permissions ADD CONSTRAINT FK_permissions_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE charity_refs ADD CONSTRAINT FK_charity_refs_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE charity_refs ADD CONSTRAINT FK_charity_refs_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE charity_program_refs ADD CONSTRAINT FK_charity_program_refs_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE charity_program_refs ADD CONSTRAINT FK_charity_program_refs_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE donation_stores ADD CONSTRAINT FK_donation_stores_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE donation_stores ADD CONSTRAINT FK_donation_stores_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE donation_projects ADD CONSTRAINT FK_donation_projects_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE donation_projects ADD CONSTRAINT FK_donation_projects_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE donation_amount_options ADD CONSTRAINT FK_donation_amount_options_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE donation_amount_options ADD CONSTRAINT FK_donation_amount_options_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE donations ADD CONSTRAINT FK_donations_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE donations ADD CONSTRAINT FK_donations_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE donation_payments ADD CONSTRAINT FK_donation_payments_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE donation_payments ADD CONSTRAINT FK_donation_payments_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE donation_invoices ADD CONSTRAINT FK_donation_invoices_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE donation_invoices ADD CONSTRAINT FK_donation_invoices_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE settlements ADD CONSTRAINT FK_settlements_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE settlements ADD CONSTRAINT FK_settlements_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE settlement_lines ADD CONSTRAINT FK_settlement_lines_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE settlement_lines ADD CONSTRAINT FK_settlement_lines_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE ui_strings ADD CONSTRAINT FK_ui_strings_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE ui_strings ADD CONSTRAINT FK_ui_strings_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE settings ADD CONSTRAINT FK_settings_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE settings ADD CONSTRAINT FK_settings_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE email_templates ADD CONSTRAINT FK_email_templates_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE email_templates ADD CONSTRAINT FK_email_templates_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE email_logs ADD CONSTRAINT FK_email_logs_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE email_logs ADD CONSTRAINT FK_email_logs_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);
ALTER TABLE payment_channels ADD CONSTRAINT FK_payment_channels_created_by FOREIGN KEY (created_by) REFERENCES admin_users (id);
ALTER TABLE payment_channels ADD CONSTRAINT FK_payment_channels_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users (id);

-- ============================================================================
-- 檔案結束。共 23 張主表 ＋ 4 張 *_i18n 側表 ＝ 27 個 CREATE TABLE。
-- ============================================================================
