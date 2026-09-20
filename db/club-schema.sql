/* ============================================================================
   TCRFC 主站資料庫（sqldb-club）— Azure SQL DDL
   ============================================================================
   用途：官網主站 ＋ 台中藍鯨官網 ＋ 共用後台 ＋ 行動 App 後端的資料庫綱要。
   對應文件版本：docs/12-database-schema.md、docs/12a-database-erd.md、
                 docs/12b-database-tables.md、docs/12c-i18n-tables.md
                 （皆為 2026-09-20 v3.0／S0-3d 同步版）；docs/17-deployment.md §6。
   產生日期：2026-09-20（第二版，全表核對後重產，S0-3e）
   產生者：backend-engineer agent（依使用者指示轉譯 docs/12 系列為 DDL）

   🔴 ⚠️ 改綱要要先改 docs/12／12a／12b／12c（規劃書 → docs/12 → 本檔），
      不要直接改這個檔案後不回寫文件。docs/00-harness.md §2.5 同步鏈。

   🔴 範圍邊界：
     - 本庫只涵蓋主站（含站內商店 S）＋ 後台帳號與權限 J。
     - 不含慈善捐款平台（獨立庫 sqldb-charity，見另案 docs/16-charity-schema.md）。
     - 不含行動 App 專屬 11 個型別（AdSlot／Advertiser／AdCampaign／AdCreative／
       AdEvent／AdDailyStat／AppDevice／PushTopicSubscription／PushMessage／
       AppRelease／AppDiagnosticReport）。
     - 🔴 本庫與慈善庫（sqldb-charity）之間絕對不得有外鍵、不得跨庫查詢。

   —— 硬性技術約束（docs/12 §1.2、§1.4，docs/17 §6，本次任務指示）——
    1. 主鍵 id uniqueidentifier 一律 PRIMARY KEY NONCLUSTERED；另加不對外的
       row_seq bigint IDENTITY(1,1) 欄位作為叢集索引（UNIQUE CLUSTERED）。
       row_seq 不進 API、不進 URL。純關聯表（複合主鍵，含 *_i18n 側表——
       側表同樣沒有獨立 id 欄，比照關聯表處理）直接 PRIMARY KEY CLUSTERED
       在複合鍵上，不加 row_seq。
    2. club_id 的必填／可為空／不加，逐表依 docs/12 §4 標註，未自行判斷。
    3. 可為空 club_id 的複合唯一鍵一律 UNIQUE (club_id, slug)，不加篩選唯一
       索引、不加觸發器（SQL Server 唯一索引把 NULL 當相等）。
    4. 金額一律 int，單位「元」；只有百分比用 decimal(5,2)。
    5. 雙語走 <entity>_i18n 側表，複合主鍵 (<entity>_id, locale)；三個例外
       （快照表／AdminRole・Permission／UiString 機制）不走側表；另有若干
       表經核對 ERD 與規劃書後判定無側表欄位可列，詳見表格上方註記。
    6. json 欄位一律 Azure SQL 原生 json 型別，只存不查。
    7. calendar_events 是一般 VIEW（UNION ALL），不得嘗試 indexed view /
       WITH SCHEMABINDING（SQL Server 禁止 indexed view 含 UNION）。
    8. 本庫沒有任何日誌表（AuditLog／LoginLog／ExportLog／OperationLog）。
       EmailLog／InventoryMovement／PageVersion／FaqSearchMiss 是功能單元，
       照建。
    9. 圖片沒有外鍵，是該表自己的欄位組：<名稱>_key（nvarchar(500)）＋
       _width／_height（int）＋ _alt_zh／_alt_en（走 i18n 側表）。多圖以
       子表承載加 sort_order。全系統不設媒體庫。
   10. 索引、唯一鍵、外鍵刪除行為依 docs/12b §11.1／§11.2／§11.3；未逐一
       列出行為的關係一律預設 NO ACTION（省略 ON DELETE 子句）。
   11. 輸出結構：本檔先依 4.0–4.12 分模組建表（僅含欄位與主鍵／叢集鍵／
       CHECK），再統一建唯一鍵、統一建索引、統一建外鍵，最後 CREATE VIEW。

   —— 定序（COLLATE）提醒 ——
   🔴 本檔刻意不在任何欄位或資料庫層級寫死 COLLATE。定序在建庫時一次決定、
      建庫後不可改（docs/17-deployment.md §8 列為未決項）。建庫時另下
      CREATE DATABASE ... COLLATE <選定值>，本檔全部沿用資料庫預設定序。

   —— 物理命名 ——
   物理表名 snake_case 複數（docs/12 §1.2）；外鍵欄位單數（team_id）；
   不加 dbo. 前綴，使用預設 schema。

   —— 本版與上一版的差異，以及本檔範圍內尚無法從文件消解的問題 ——
   上一版依不完整 ERD 產生，members 缺姓名欄位、player_season_stats 除
   兩個外鍵外空無一欄，已於本版全數補齊（見任務回報）。本版另發現並記錄
   以下無法單靠 docs/12 系列消解的問題，均以下方對應表格上方的行內註解
   標註 "-- ⚠️ 待確認"：
     (a) clubs 缺「簡介」欄位——規劃書明文要求「名稱與簡介（中／英）」，
         但 docs/12a §5.11 的 club 主表與 docs/12c 起草的側表都沒有
         description／intro 落點（docs/12c §5 第 2 點）。本檔不新增未經
         文件定義的欄位，缺口原樣保留待補文件。
     (b) clubs／competitions 的雙語作法與 docs/12 §2.1 側表原則衝突：
         docs/12a 兩張主表 ERD 直接放 name_zh／name_en 並排欄位，不是側表
         （docs/12c §5 第 1 點）。本檔尊重 ERD 既有決定（欄位與型別以
         docs/12a 為主要來源），competitions 另補 organizer_zh／
         organizer_en 沿用同一並排欄位慣例（規劃書僅給「主辦單位」單一
         欄名，中英拆分寫法為本檔延伸，非文件字面）。
     (c) registrations.health_declaration 的分級（🔒 明文存 vs 🔐 加密）
         docs/12b §8 明文列為待法務確認事項，本檔先以 nvarchar(max) 明文
         儲存（不預先加密，避免加密演算法與金鑰管理未定案卻搶先綁架欄位
         型別），待法務確認後可能需改為應用層加密。
     (d) page_blocks：docs/12 標🌐，但 docs/12a §5.1 的 page_block 主表已
         把 content 放在主表（json），docs/12c §3.1 的側表草案重複提出
         同一欄位——依 docs/12c §1 第 3 條「主表已放的欄位優先尊重主表」，
         本檔不建 page_blocks_i18n。
     (e) seasons／standings／achievements／sessions／proposals／trials／
         form_fields／clubs／competitions：docs/12 標🌐但 docs/12c 逐一
         核對後找不到任何可列的側表欄位（docs/12c §4／§5 第 4、5 點），
         本檔不建對應 *_i18n 表。
   ========================================================================== */

/* ============================================================================
   4.0 共通機制
   ============================================================================ */

-- 啟用語系字典。加第三語系＝ INSERT 一列，不改 DDL。
CREATE TABLE locales (
  code            nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NOT NULL,
  is_default      bit              NOT NULL DEFAULT 0,
  fallback_code   nvarchar(10)     NULL,
  is_enabled      bit              NOT NULL DEFAULT 1,
  sort_order      int              NOT NULL DEFAULT 0,
  CONSTRAINT PK_locales PRIMARY KEY CLUSTERED (code)
);

-- 介面字串鍵（按鈕、標籤、提示、錯誤訊息）與所屬分組。
CREATE TABLE ui_strings (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  string_key      nvarchar(128)    NOT NULL,
  string_group    nvarchar(64)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_ui_strings PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_ui_strings_row_seq UNIQUE CLUSTERED (row_seq)
);

-- UI 字串逐語系翻譯值（I 模組字串翻譯表，與內容 i18n 是兩套機制，形狀相同）。
CREATE TABLE ui_string_translations (
  ui_string_id    uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  value           nvarchar(max)    NOT NULL,
  CONSTRAINT PK_ui_string_translations PRIMARY KEY CLUSTERED (ui_string_id, locale)
);

-- 五大核心價值標籤的多型關聯，可掛任何內容型別。
CREATE TABLE value_tag_links (
  entity_type     nvarchar(32)     NOT NULL,
  entity_id       uniqueidentifier NOT NULL,
  value_tag       nvarchar(32)     NOT NULL
                    CHECK (value_tag IN
                      ('players_first','excellence','global_pathways','community','integrity')),
  CONSTRAINT PK_value_tag_links PRIMARY KEY CLUSTERED (entity_type, entity_id, value_tag)
);

-- 全域設定鍵值（含商店設定、聯絡資訊、社群連結、政策頁、維護模式）。
-- 唯一鍵 (club_id, setting_key)。setting_value 供非語系設定值；文案類設定另見 settings_i18n。
CREATE TABLE settings (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  setting_key     nvarchar(128)    NOT NULL,
  setting_value   nvarchar(max)    NULL,
  setting_group   nvarchar(32)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_settings PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_settings_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 設定值的逐語系文案（docs/12c §2 對照組，未逐欄明列，採單一 value 欄）。
CREATE TABLE settings_i18n (
  setting_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  value           nvarchar(max)    NULL,
  CONSTRAINT PK_settings_i18n PRIMARY KEY CLUSTERED (setting_id, locale)
);

-- 系統信樣板。
CREATE TABLE email_templates (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  template_code   nvarchar(32)     NOT NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_email_templates PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_email_templates_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 系統信樣板逐語系主旨與內文（docs/12c §3.0）。
CREATE TABLE email_templates_i18n (
  email_template_id uniqueidentifier NOT NULL,
  locale            nvarchar(10)   NOT NULL,
  subject           nvarchar(200)  NULL,
  body              nvarchar(max)  NULL,
  CONSTRAINT PK_email_templates_i18n PRIMARY KEY CLUSTERED (email_template_id, locale)
);

-- 系統信寄送紀錄（功能單元，不是操作日誌）。type 值域 9 個（會員 5＋商店 4，
-- docs/12 §4.0 僅稱值域數量未逐一列出字面值，本表不加 CHECK 以免杜撰值域）。
CREATE TABLE email_logs (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  email_template_id uniqueidentifier NULL,
  type              nvarchar(32)     NOT NULL,
  to_email          nvarchar(255)    NOT NULL,
  member_id         uniqueidentifier NULL,
  send_status       nvarchar(20)     NOT NULL,
  sent_at           datetime2(3)     NULL,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_email_logs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_email_logs_row_seq UNIQUE CLUSTERED (row_seq)
);
