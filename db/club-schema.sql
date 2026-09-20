/* ============================================================================
   TCRFC 主站資料庫（sqldb-club）— Azure SQL DDL
   ============================================================================
   用途：官網主站 ＋ 台中藍鯨官網 ＋ 共用後台 ＋ 行動 App 後端的資料庫綱要。
   對應文件版本：docs/12-database-schema.md、docs/12a-database-erd.md、
                 docs/12b-database-tables.md（皆為 2026-09-20 v3.0 同步版）；
                 docs/17-deployment.md §6（DBMS 連帶決定）。
   產生日期：2026-09-20
   產生者：backend-engineer agent（依使用者指示轉譯 docs/12 系列為 DDL）

   🔴 ⚠️ 改綱要要先改 docs/12／12a／12b（規劃書 → docs/12 → 本檔），
      不要直接改這個檔案後不回寫文件。docs/00-harness.md §2.5 同步鏈。

   🔴 範圍邊界：
     - 本庫只涵蓋主站（含站內商店 S）＋ 後台帳號與權限 J。
     - 不含慈善捐款平台（獨立庫 sqldb-charity，見另案 docs/16-charity-schema.md）。
     - 不含行動 App 專屬 11 個型別（AdSlot／Advertiser／AdCampaign／AdCreative／
       AdEvent／AdDailyStat／AppDevice／PushTopicSubscription／PushMessage／
       AppRelease／AppDiagnosticReport）。
     - 🔴 本庫與慈善庫（sqldb-charity）之間絕對不得有外鍵、不得跨庫查詢
       （Azure SQL 本不支援跨庫 FK／查詢，此處僅重申，不代表本檔曾嘗試建立）。

   —— 硬性技術約束（已定案，見 docs/12 §1.2、§1.4，docs/17 §6）——
   1. 主鍵 id uniqueidentifier 一律 PRIMARY KEY NONCLUSTERED；另加不對外的
      row_seq bigint IDENTITY(1,1) 欄位作為叢集索引（UNIQUE CLUSTERED）。
      row_seq 不進 API、不進 URL，純粹是叢集鍵。
      本檔將此規則一併套用到「複合主鍵皆為 uniqueidentifier」的關聯表
      （例如 article_tags、cart_items），理由相同（GUID 比較位元組順序反轉，
      叢集在 GUID 上會造成頁分裂）——這是本檔在文件既有規則上的合理延伸，
      不是文件另外要求的新規格，特此註記。純自然鍵的表（locales）維持
      叢集在自然鍵上。
   2. club_id 的必填／可為空／不加，逐表依 docs/12 §4 標註，未自行判斷。
   3. 可為空 club_id 的複合唯一鍵一律 UNIQUE (club_id, slug)，不加篩選唯一
      索引、不加觸發器（SQL Server 唯一索引把 NULL 當相等）。
   4. 金額一律 int，單位「元」；只有百分比用 decimal(5,2)。
   5. 雙語走 <entity>_i18n 側表，複合主鍵 (<entity>_id, locale)；三個例外
      （快照表／AdminRole・Permission／UiString 機制）不走側表。
   6. json 欄位一律 Azure SQL 原生 json 型別，只存不查。
   7. calendar_events 是一般 VIEW（UNION），不得嘗試 indexed view /
      WITH SCHEMABINDING（SQL Server 禁止 indexed view 含 UNION）。
   8. 本庫沒有任何日誌表（AuditLog／LoginLog／ExportLog／OperationLog）。
      EmailLog／InventoryMovement／PageVersion／FaqSearchMiss 是功能單元，
      不在移除範圍內，本檔照建。
   9. 圖片沒有外鍵，是該表自己的欄位組：<名稱>_key（nvarchar(500)）＋
      _width／_height（int）＋ _alt_zh／_alt_en（走 i18n 側表，或該表既有
      的並排 zh/en 欄位，視該表既有雙語機制而定）。多圖以子表承載加
      sort_order。全系統不設媒體庫。
  10. 索引、唯一鍵、外鍵刪除行為依 docs/12b §11.1／§11.2／§11.3。

   —— 定序（COLLATE）提醒 ——
   🔴 本檔刻意不在任何欄位或資料庫層級寫死 COLLATE。定序在建庫時一次決定、
      建庫後不可改（docs/17-deployment.md §8 列為未決項）。建庫時另下
      CREATE DATABASE ... COLLATE <選定值>，本檔全部沿用資料庫預設定序。

   —— 物理命名 ——
   物理表名 snake_case 複數（docs/12 §1.2）；本檔逐表套用，僅少數集合名詞
   （staff）維持單複數同形，屬命名判斷不影響規格。schema 使用預設 dbo，
   不另外加前綴。

   —— 本檔對 docs 的必要補充與已發現的落差（詳細清單見任務回報，此處僅索引）——
   本檔中所有標記 "-- ⚠️ 待確認" 的行，都是 docs/12 系列本身資訊不足、
   或 docs/12 與 docs/12a／12b／規劃書本文互相矛盾之處。已在能查到規劃書
   原文的地方（如 Club、Venue、Match.competition／status 的值域）直接引用
   規劃書原文（output/TCRFC_前後台功能規劃書.md）補齊，其餘一律標註待確認、
   不自行杜撰業務邏輯值域。
   ========================================================================== */

/* ============================================================================
   4.0 共通機制
   ============================================================================ */

-- 啟用語系字典。加第三語系＝ INSERT 一列，不改 DDL。
CREATE TABLE locales (
  code            nvarchar(10)   NOT NULL,
  name            nvarchar(64)   NOT NULL,
  is_default      bit            NOT NULL DEFAULT 0,
  fallback_code   nvarchar(10)   NULL,
  is_enabled      bit            NOT NULL DEFAULT 1,
  sort_order      int            NOT NULL DEFAULT 0,
  CONSTRAINT PK_locales PRIMARY KEY CLUSTERED (code)
);

-- 介面字串鍵（按鈕、標籤、提示、錯誤訊息）與所屬分組。
CREATE TABLE ui_strings (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  string_key      nvarchar(128)  NOT NULL,
  string_group    nvarchar(64)   NULL,
  CONSTRAINT PK_ui_strings PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_ui_strings_row_seq UNIQUE CLUSTERED (row_seq)
);

-- UI 字串逐語系翻譯值。
CREATE TABLE ui_string_translations (
  ui_string_id    uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  value           nvarchar(max)  NOT NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_ui_string_translations PRIMARY KEY NONCLUSTERED (ui_string_id, locale),
  CONSTRAINT UQ_ui_string_translations_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 五大核心價值標籤的多型關聯（players_first／excellence／global_pathways／community／integrity），
-- 可掛任何內容型別。value_tag 五值取自 docs/06-conventions.md 五大核心價值中英對照，轉為 snake_case 代碼。
CREATE TABLE value_tag_links (
  entity_type     nvarchar(32)   NOT NULL,
  entity_id       uniqueidentifier NOT NULL,
  value_tag       nvarchar(32)   NOT NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_value_tag_links PRIMARY KEY NONCLUSTERED (entity_type, entity_id, value_tag),
  CONSTRAINT UQ_value_tag_links_row_seq UNIQUE CLUSTERED (row_seq),
  CONSTRAINT CK_value_tag_links_value_tag CHECK (value_tag IN
    ('players_first','excellence','global_pathways','community','integrity'))
);

-- 全域設定鍵值（含商店設定、聯絡資訊、社群連結、政策頁、維護模式）。唯一鍵 (club_id, setting_key)。
CREATE TABLE settings (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  setting_key     nvarchar(128)  NOT NULL,
  setting_value   nvarchar(max)  NULL,
  setting_group   nvarchar(32)   NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_settings PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_settings_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 僅以🌐標記 Setting 有 i18n 側表，未列出確切欄位。本表採側表慣例，
-- 以 value 承載該筆設定文案的逐語系內容（setting_value 保留給非語系設定值）。
CREATE TABLE settings_i18n (
  setting_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  value           nvarchar(max)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_settings_i18n PRIMARY KEY NONCLUSTERED (setting_id, locale),
  CONSTRAINT UQ_settings_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 系統信樣板。
CREATE TABLE email_templates (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  template_code   nvarchar(32)   NOT NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_email_templates PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_email_templates_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出 email_templates 的翻譯欄位清單，本處依信件樣板的明顯必要
-- 欄位（主旨、內文）推定最小可用欄位。
CREATE TABLE email_templates_i18n (
  email_template_id uniqueidentifier NOT NULL,
  locale            nvarchar(10)   NOT NULL,
  subject           nvarchar(200)  NULL,
  body_html         nvarchar(max)  NULL,
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_email_templates_i18n PRIMARY KEY NONCLUSTERED (email_template_id, locale),
  CONSTRAINT UQ_email_templates_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 系統信寄送紀錄（功能單元，不是操作日誌）。type 值域 9 個：會員 5 ＋ 商店 4。
-- ⚠️ 待確認：docs/12 §4.0 僅稱「9 個值（會員5＋商店4）」，未逐一列出字面值，
-- 本處不加 CHECK 約束（避免杜撰值域字面）。
CREATE TABLE email_logs (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  email_template_id uniqueidentifier NULL,
  type              nvarchar(32)   NOT NULL,
  to_email          nvarchar(255)  NOT NULL,
  member_id         uniqueidentifier NULL,
  send_status       nvarchar(20)   NOT NULL,
  sent_at           datetime2(3)   NULL,
  created_at        datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_email_logs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_email_logs_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.1 B 內容管理 ＋ H 搜尋與 AI 能見度
   ============================================================================ */

-- 靜態頁面主檔。藍鯨官網入口頁亦屬此型別。唯一鍵 (club_id, slug)。
CREATE TABLE pages (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)  NOT NULL,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  published_at    datetime2(3)   NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_pages PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_pages_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出 pages_i18n 確切欄位，title 為頁面顯示必要欄位推定；
-- seo_title／seo_description 依 docs/12 §1.3「具前台展示的內容表」通則加入。
CREATE TABLE pages_i18n (
  page_id         uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  title           nvarchar(200)  NULL,
  seo_title       nvarchar(200)  NULL,
  seo_description nvarchar(300)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_pages_i18n PRIMARY KEY NONCLUSTERED (page_id, locale),
  CONSTRAINT UQ_pages_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 頁面區塊（13 種型別），content 為只存不查的 json。由 page 推導，不帶 club_id。
CREATE TABLE page_blocks (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  page_id         uniqueidentifier NOT NULL,
  block_type      nvarchar(32)   NOT NULL,
  content         json           NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_page_blocks PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_page_blocks_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：PageBlock.content 本身即為 json「只存不查」內容；此側表用於承載
-- 逐語系版本的 content（區塊內容依語系不同）。
CREATE TABLE page_blocks_i18n (
  page_block_id   uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  content         json           NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_page_blocks_i18n PRIMARY KEY NONCLUSTERED (page_block_id, locale),
  CONSTRAINT UQ_page_blocks_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 版本歷程與還原點、預覽分享 token。這是內容版本，不是操作日誌。不帶 club_id（由 page 推導）。
CREATE TABLE page_versions (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  page_id         uniqueidentifier NOT NULL,
  version_no      int            NOT NULL,
  snapshot        json           NULL,
  preview_token   nvarchar(64)   NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_page_versions PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_page_versions_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 新聞與故事。空＝兩隊共同；slug 維持全站唯一（共同文章須有單一 canonical）。
CREATE TABLE articles (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  club_id             uniqueidentifier NULL,
  slug                nvarchar(160)  NOT NULL,
  article_category_id uniqueidentifier NULL,
  cover_key           nvarchar(500)  NULL,
  cover_width         int            NULL,
  cover_height        int            NULL,
  is_featured         bit            NOT NULL DEFAULT 0,
  view_count          int            NOT NULL DEFAULT 0,
  status              nvarchar(16)   NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft','published','scheduled')),
  published_at        datetime2(3)   NULL,
  created_at          datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_articles PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_articles_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 明確依據 docs/12 §5.12／§2.4：title／summary／body(json)／seo_title／seo_description。
-- cover_alt 依規則 9（圖片 _alt 走 i18n 側表）補上。
CREATE TABLE articles_i18n (
  article_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  title           nvarchar(200)  NULL,
  summary         nvarchar(max)  NULL,
  body            json           NULL,
  seo_title       nvarchar(200)  NULL,
  seo_description nvarchar(300)  NULL,
  cover_alt       nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_articles_i18n PRIMARY KEY NONCLUSTERED (article_id, locale),
  CONSTRAINT UQ_articles_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 7.1–7.8 八分類。刻意不帶 club_id。
CREATE TABLE article_categories (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  code            nvarchar(32)   NOT NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_article_categories PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_article_categories_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name 為分類顯示必要欄位推定。
CREATE TABLE article_categories_i18n (
  article_category_id uniqueidentifier NOT NULL,
  locale               nvarchar(10)   NOT NULL,
  name                 nvarchar(64)   NULL,
  row_seq              bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_article_categories_i18n PRIMARY KEY NONCLUSTERED (article_category_id, locale),
  CONSTRAINT UQ_article_categories_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 標籤。刻意不帶 club_id。
CREATE TABLE tags (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  slug            nvarchar(160)  NOT NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_tags PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_tags_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name 為標籤顯示必要欄位推定。
CREATE TABLE tags_i18n (
  tag_id          uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(64)   NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_tags_i18n PRIMARY KEY NONCLUSTERED (tag_id, locale),
  CONSTRAINT UQ_tags_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 文章與標籤的多對多。
CREATE TABLE article_tags (
  article_id      uniqueidentifier NOT NULL,
  tag_id          uniqueidentifier NOT NULL,
  CONSTRAINT PK_article_tags PRIMARY KEY CLUSTERED (article_id, tag_id)
);

-- 文章的多型關聯 (article_id, target_type, target_id)。target_type 指向
-- player／team／match／program／partner／charity（刻意用多型而非六個外鍵）。
CREATE TABLE article_relations (
  article_id      uniqueidentifier NOT NULL,
  target_type     nvarchar(32)   NOT NULL,
  target_id       uniqueidentifier NOT NULL,
  CONSTRAINT PK_article_relations PRIMARY KEY CLUSTERED (article_id, target_type, target_id)
);

-- 媒體資源（新聞稿／品牌識別包／高解析圖），對應 7.8。
CREATE TABLE press_resources (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NULL,
  slug            nvarchar(160)  NOT NULL,
  resource_type   nvarchar(32)   NOT NULL,
  file_key        nvarchar(500)  NULL,
  file_bytes      int            NULL,
  cover_key       nvarchar(500)  NULL,
  cover_width     int            NULL,
  cover_height    int            NULL,
  published_on    date           NULL,
  download_count  int            NOT NULL DEFAULT 0,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_press_resources PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_press_resources_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，title／summary 依用途推定；cover_alt 依規則 9 補上。
CREATE TABLE press_resources_i18n (
  press_resource_id uniqueidentifier NOT NULL,
  locale             nvarchar(10)   NOT NULL,
  title              nvarchar(200)  NULL,
  summary            nvarchar(max)  NULL,
  cover_alt          nvarchar(255)  NULL,
  row_seq            bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_press_resources_i18n PRIMARY KEY NONCLUSTERED (press_resource_id, locale),
  CONSTRAINT UQ_press_resources_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 首頁 Hero 輪播（≤5）：素材、CTA、上下架期間、排序。
CREATE TABLE banners (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  image_key       nvarchar(500)  NULL,
  image_width     int            NULL,
  image_height    int            NULL,
  start_at        datetime2(3)   NULL,
  end_at          datetime2(3)   NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_banners PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_banners_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，caption／CTA 依「素材、CTA」用途描述推定；image_alt 依規則 9。
CREATE TABLE banners_i18n (
  banner_id       uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  caption         nvarchar(200)  NULL,
  cta_label       nvarchar(64)   NULL,
  cta_url         nvarchar(500)  NULL,
  image_alt       nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_banners_i18n PRIMARY KEY NONCLUSTERED (banner_id, locale),
  CONSTRAINT UQ_banners_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 首頁九大區塊的開關、排序與精選指定。無 i18n（🌐 未標）。
CREATE TABLE home_sections (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  section_code    nvarchar(32)   NOT NULL,
  is_enabled      bit            NOT NULL DEFAULT 1,
  featured_banner_id uniqueidentifier NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_home_sections PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_home_sections_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 常見問題；👍／👎 計數。
CREATE TABLE faqs (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NULL,
  slug            nvarchar(160)  NOT NULL,
  helpful_count   int            NOT NULL DEFAULT 0,
  unhelpful_count int            NOT NULL DEFAULT 0,
  sort_order      int            NOT NULL DEFAULT 0,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_faqs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_faqs_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，question／answer 為 FAQ 顯示必要欄位推定。
CREATE TABLE faqs_i18n (
  faq_id          uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  question        nvarchar(300)  NULL,
  answer          nvarchar(max)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_faqs_i18n PRIMARY KEY NONCLUSTERED (faq_id, locale),
  CONSTRAINT UQ_faqs_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 主題分類（10 個）。刻意不帶 club_id。
CREATE TABLE faq_categories (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  slug            nvarchar(160)  NOT NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_faq_categories PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_faq_categories_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name 為分類顯示必要欄位推定。
CREATE TABLE faq_categories_i18n (
  faq_category_id uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(64)   NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_faq_categories_i18n PRIMARY KEY NONCLUSTERED (faq_category_id, locale),
  CONSTRAINT UQ_faq_categories_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 一題可屬多分類。
CREATE TABLE faq_category_links (
  faq_id          uniqueidentifier NOT NULL,
  faq_category_id uniqueidentifier NOT NULL,
  CONSTRAINT PK_faq_category_links PRIMARY KEY CLUSTERED (faq_id, faq_category_id)
);

-- 零結果搜尋關鍵字與次數。這是成效統計不是日誌。
CREATE TABLE faq_search_misses (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  keyword           nvarchar(200)  NOT NULL,
  hit_count         int            NOT NULL DEFAULT 1,
  last_searched_at  datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_at        datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_faq_search_misses PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_faq_search_misses_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 301 對照（from_path、to_path、is_active）。唯一鍵 (club_id, from_path)。
CREATE TABLE redirects (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  from_path       nvarchar(500)  NOT NULL,
  to_path         nvarchar(500)  NOT NULL,
  is_active       bit            NOT NULL DEFAULT 1,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_redirects PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_redirects_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.2 C 球隊管理
   ============================================================================ */

-- 賽事系列（v3.0 新增）。與 match.competition 四值 enum 並存不互相取代。
-- ⚠️ 待確認：Competition 在 docs/12 §4.2 標記🌐（有 i18n 側表），但 ERD（12a §5.2）
-- 給的欄位是 name_zh／name_en 並排直接放在主表，未走側表——與 Club 相同的矛盾情形。
-- 本檔依 ERD 字面（並排欄位）建表，不另建 competitions_i18n，並在此註記落差。
CREATE TABLE competitions (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NOT NULL,
  code            nvarchar(16)   NOT NULL,
  name_zh         nvarchar(64)   NOT NULL,
  name_en         nvarchar(64)   NULL,
  comp_type       nvarchar(32)   NOT NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_competitions PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_competitions_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 賽季（code 如 2026-27，起訖日）。唯一鍵 (club_id, code)——兩隊球季不同步。
CREATE TABLE seasons (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  code            nvarchar(16)   NOT NULL,
  start_on        date           NOT NULL,
  end_on          date           NOT NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_seasons PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_seasons_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name 為球季顯示名稱（如「2026-27 球季」）推定。
CREATE TABLE seasons_i18n (
  season_id       uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(64)   NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_seasons_i18n PRIMARY KEY NONCLUSTERED (season_id, locale),
  CONSTRAINT UQ_seasons_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 球隊。code 全站唯一（不得改複合鍵），值域 D1／BW1／U15／U14／U12。
-- type = first_team（每俱樂部至多一筆）／academy。gender 取代已廢除的 type='women'。
CREATE TABLE teams (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  code            nvarchar(8)    NOT NULL,
  type            nvarchar(16)   NOT NULL CHECK (type IN ('first_team','academy')),
  gender          nvarchar(8)    NOT NULL CHECK (gender IN ('men','women','mixed')),
  age_band        nvarchar(16)   NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_teams PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_teams_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：Team 主表無 name 欄位（ERD 僅給 code／type／gender／age_band），
-- 依 docs/12 §4.2🌐標記推定 team 顯示名稱走側表。
CREATE TABLE teams_i18n (
  team_id         uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(64)   NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_teams_i18n PRIMARY KEY NONCLUSTERED (team_id, locale),
  CONSTRAINT UQ_teams_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 球員：背號、位置、生日、身高體重、國籍、慣用腳、加入日期、狀態。
-- ⚠️ 待確認：docs/12a §5.2 ERD 未列身高體重／慣用腳／加入日期欄位（§4.2 用途文字提到但 ERD 未給），
-- 本表僅建 ERD 明確給出的欄位（shirt_no／position／birth_on／nationality／status／photo_key），
-- 身高體重／慣用腳／加入日期暫不加入（docs 沒寫的欄位不自行發明）。
CREATE TABLE players (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  shirt_no        int            NULL,
  position        nvarchar(32)   NULL,
  birth_on        date           NULL,
  nationality     nvarchar(32)   NULL,
  status          nvarchar(20)   NOT NULL DEFAULT 'active',
  photo_key       nvarchar(500)  NULL,
  photo_width     int            NULL,
  photo_height    int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_players PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_players_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name 為球員顯示必要欄位推定；photo_alt 依規則 9。
CREATE TABLE players_i18n (
  player_id       uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(64)   NULL,
  photo_alt       nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_players_i18n PRIMARY KEY NONCLUSTERED (player_id, locale),
  CONSTRAINT UQ_players_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 逐季數據。由 player 推導，不帶 club_id。
CREATE TABLE player_season_stats (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  player_id       uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NOT NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_player_season_stats PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_player_season_stats_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 教練與團隊成員：證照、專長、分組。空＝兩隊共同（行政與醫療多為共用）。
CREATE TABLE staff (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NULL,
  staff_group     nvarchar(32)   NULL,
  licence         nvarchar(64)   NULL,
  photo_key       nvarchar(500)  NULL,
  photo_width     int            NULL,
  photo_height    int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_staff PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_staff_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name 為顯示必要欄位推定；photo_alt 依規則 9。
CREATE TABLE staff_i18n (
  staff_id        uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(64)   NULL,
  photo_alt       nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_staff_i18n PRIMARY KEY NONCLUSTERED (staff_id, locale),
  CONSTRAINT UQ_staff_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (staff_id, team_id) 帶職務。
CREATE TABLE staff_teams (
  staff_id        uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  role_code       nvarchar(64)   NULL,
  CONSTRAINT PK_staff_teams PRIMARY KEY CLUSTERED (staff_id, team_id)
);

-- 賽事。competition_id 可空；status 與 competition 四值 enum 皆為正式欄位
-- （規劃書 v2.5 §5.1 行 1469：competition＝聯賽／盃賽／友誼／其他，
--   status＝未開始／進行中／已結束／延期，字面值取自主站規劃書原文）。
-- 對手與場地的英文走 match_i18n。
CREATE TABLE matches (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NOT NULL,
  competition_id  uniqueidentifier NULL,
  venue_id        uniqueidentifier NULL,
  match_on        date           NOT NULL,
  kickoff         nvarchar(8)    NULL,
  home_away       nvarchar(8)    NOT NULL CHECK (home_away IN ('home','away')),
  opponent        nvarchar(128)  NULL,
  competition     nvarchar(8)    NOT NULL
                    CHECK (competition IN (N'聯賽', N'盃賽', N'友誼', N'其他')),
  status          nvarchar(8)    NOT NULL
                    CHECK (status IN (N'未開始', N'進行中', N'已結束', N'延期')),
  score_home      int            NULL,
  score_away      int            NULL,
  round_no        int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_matches PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_matches_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 明確依據 docs/12 §2.4：opponent／venue 為自由文字（非實體），逐語系存放。
-- ⚠️ 待確認：venue（自由文字）與 matches.venue_id（結構化 FK）並存，語意重疊——
-- 推測 venue_id 用於場地為本系統已登錄之 Venue，venue（自由文字）用於未登錄的客場館，
-- 但 docs 未明文釐清兩者的使用時機，建議與規劃書撰寫人確認。
CREATE TABLE matches_i18n (
  match_id        uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  opponent        nvarchar(128)  NULL,
  venue           nvarchar(128)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_matches_i18n PRIMARY KEY NONCLUSTERED (match_id, locale),
  CONSTRAINT UQ_matches_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 本方參賽隊 (match_id, team_id)。
CREATE TABLE match_teams (
  match_id        uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  CONSTRAINT PK_match_teams PRIMARY KEY CLUSTERED (match_id, team_id)
);

-- 進球（球員、時間、類型）。
CREATE TABLE match_goals (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  match_id        uniqueidentifier NOT NULL,
  player_id       uniqueidentifier NULL,
  minute          int            NULL,
  goal_type       nvarchar(32)   NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_match_goals PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_match_goals_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 黃紅牌。
CREATE TABLE match_cards (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  match_id        uniqueidentifier NOT NULL,
  player_id       uniqueidentifier NULL,
  minute          int            NULL,
  card_type       nvarchar(16)   NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_match_cards PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_match_cards_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 先發與替補名單。
CREATE TABLE match_lineups (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  match_id        uniqueidentifier NOT NULL,
  player_id       uniqueidentifier NOT NULL,
  is_starting     bit            NOT NULL DEFAULT 0,
  shirt_no        int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_match_lineups PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_match_lineups_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 積分榜 (season_id, team_name, ...)。對手隊名是自由文字不是 Team。
CREATE TABLE standings (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NOT NULL,
  team_name       nvarchar(128)  NOT NULL,
  rank            int            NULL,
  played          int            NULL,
  points          int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_standings PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_standings_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，team_name 為自由文字，比照 match.opponent 的雙語處理方式推定。
CREATE TABLE standings_i18n (
  standing_id     uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  team_name       nvarchar(128)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_standings_i18n PRIMARY KEY NONCLUSTERED (standing_id, locale),
  CONSTRAINT UQ_standings_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 榮譽（年份、賽事、名次、隊伍）。
-- ⚠️ 待確認：Achievement 標記🌐，但 ERD 已給 competition_name／placing 為主表並排欄位
-- （非 zh/en 對，是單一自由文字），本檔依 ERD 字面不另建 achievements_i18n，僅註記落差。
CREATE TABLE achievements (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NULL,
  team_id         uniqueidentifier NULL,
  year            int            NOT NULL,
  competition_name nvarchar(128) NOT NULL,
  placing         nvarchar(32)   NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_achievements PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_achievements_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 里程碑時間軸。
-- ⚠️ 待確認：ERD（12a §5.2）僅給 happened_on／sort_order，無任何文字欄位，
-- 但里程碑顯然需要標題／說明文字；milestones_i18n 為必要推定，非 docs 明文列出。
CREATE TABLE milestones (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  happened_on     date           NOT NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_milestones PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_milestones_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE milestones_i18n (
  milestone_id    uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  title           nvarchar(200)  NULL,
  description     nvarchar(max)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_milestones_i18n PRIMARY KEY NONCLUSTERED (milestone_id, locale),
  CONSTRAINT UQ_milestones_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.3 P 課程與活動
   ============================================================================ */

-- 課程／營隊／專項項目：類型、對象、年齡區間、區塊內容。
CREATE TABLE programs (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)  NOT NULL,
  program_type    nvarchar(32)   NULL,
  audience        nvarchar(32)   NULL,
  age_min         int            NULL,
  age_max         int            NULL,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  cover_key       nvarchar(500)  NULL,
  cover_width     int            NULL,
  cover_height    int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_programs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_programs_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name／description 依課程需明顯可讀之名稱與介紹推定；
-- cover_alt 依規則 9；seo_title／seo_description 依 §1.3 通則（program 有 slug）。
CREATE TABLE programs_i18n (
  program_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(128)  NULL,
  description     nvarchar(max)  NULL,
  cover_alt       nvarchar(255)  NULL,
  seo_title       nvarchar(200)  NULL,
  seo_description nvarchar(300)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_programs_i18n PRIMARY KEY NONCLUSTERED (program_id, locale),
  CONSTRAINT UQ_programs_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (program_id, staff_id) 教練團。
CREATE TABLE program_staff (
  program_id      uniqueidentifier NOT NULL,
  staff_id        uniqueidentifier NOT NULL,
  CONSTRAINT PK_program_staff PRIMARY KEY CLUSTERED (program_id, staff_id)
);

-- (program_id, partner_id) 合作單位。
CREATE TABLE program_partners (
  program_id      uniqueidentifier NOT NULL,
  partner_id      uniqueidentifier NOT NULL,
  CONSTRAINT PK_program_partners PRIMARY KEY CLUSTERED (program_id, partner_id)
);

-- 梯次／場次：期間、時段、場地、名額、已報名數、價格、報名起訖、狀態。永不進 CalendarEvent。
CREATE TABLE sessions (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  club_id             uniqueidentifier NOT NULL,
  program_id          uniqueidentifier NOT NULL,
  venue_id            uniqueidentifier NULL,
  start_on            date           NULL,
  end_on              date           NULL,
  capacity            int            NULL,
  enrolled_count      int            NOT NULL DEFAULT 0,
  price               int            NULL,
  early_bird_price    int            NULL,
  early_bird_until    date           NULL,
  signup_opens_at     datetime2(3)   NULL,
  signup_closes_at    datetime2(3)   NULL,
  status              nvarchar(20)   NOT NULL DEFAULT 'draft', -- ⚠️ 待確認：值域未在 docs 列舉
  created_at          datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_sessions PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_sessions_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，label 為梯次顯示名稱（如「平日早班」）推定。
CREATE TABLE sessions_i18n (
  session_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  label           nvarchar(64)   NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_sessions_i18n PRIMARY KEY NONCLUSTERED (session_id, locale),
  CONSTRAINT UQ_sessions_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 報名。member_id 可為空（非會員可報名）；繳費線下。同時服務 session 與 trial，
-- 兩個外鍵恰有一個非空。status 值域取自規劃書：待確認→已確認→已繳費→完成／取消／候補。
CREATE TABLE registrations (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  session_id      uniqueidentifier NULL,
  trial_id        uniqueidentifier NULL,
  member_id       uniqueidentifier NULL,
  applicant_name  nvarchar(64)   NOT NULL,
  phone           nvarchar(32)   NULL,
  email           nvarchar(255)  NULL,
  birth_on        date           NULL,
  status          nvarchar(8)    NOT NULL DEFAULT N'待確認'
                    CHECK (status IN (N'待確認', N'已確認', N'已繳費', N'完成', N'取消', N'候補')),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_registrations PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_registrations_row_seq UNIQUE CLUSTERED (row_seq),
  CONSTRAINT CK_registrations_session_xor_trial CHECK (
    (CASE WHEN session_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN trial_id  IS NULL THEN 0 ELSE 1 END) = 1
  )
);

-- 試訓場次：日期、場地、對象、名額、截止。同步行事曆由 L3 開關決定，預設關閉。
CREATE TABLE trials (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NULL,
  venue_id        uniqueidentifier NULL,
  trial_on        date           NOT NULL,
  capacity        int            NULL,
  deadline_on     date           NULL,
  sync_to_calendar bit           NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_trials PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_trials_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，notes 為試訓公告文字（對象、注意事項）推定。
CREATE TABLE trials_i18n (
  trial_id        uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  notes           nvarchar(max)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_trials_i18n PRIMARY KEY NONCLUSTERED (trial_id, locale),
  CONSTRAINT UQ_trials_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.4 E 商業模組
   ============================================================================ */

-- 合作夥伴（B2B Logo 牆）：Logo 深底／淺底兩版、類型、國家、合作內容與期間、官網、排序、曝光位置。
-- partner_type 值域取自規劃書 §5.1 原文：策略／國際／訓練／教育／品牌。
CREATE TABLE partners (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)  NOT NULL,
  partner_type    nvarchar(8)    NULL
                    CHECK (partner_type IS NULL OR partner_type IN
                      (N'策略', N'國際', N'訓練', N'教育', N'品牌')),
  country         nvarchar(32)   NULL,
  logo_dark_key   nvarchar(500)  NULL,
  logo_dark_width  int           NULL,
  logo_dark_height int           NULL,
  logo_light_key  nvarchar(500)  NULL,
  logo_light_width  int          NULL,
  logo_light_height int          NULL,
  start_on        date           NULL,
  end_on          date           NULL,
  website_url     nvarchar(500)  NULL,
  show_in_footer  bit            NOT NULL DEFAULT 0,
  show_on_home    bit            NOT NULL DEFAULT 0,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_partners PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_partners_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 明確依據規劃書 §5.1：「名稱（中／英）」。logo 兩版 alt 依規則 9 補上。
CREATE TABLE partners_i18n (
  partner_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(128)  NULL,
  logo_dark_alt   nvarchar(255)  NULL,
  logo_light_alt  nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_partners_i18n PRIMARY KEY NONCLUSTERED (partner_id, locale),
  CONSTRAINT UQ_partners_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 贊助商：Logo 兩版、等級、合約期間、贊助內容、聯絡窗口、到期提醒、排序。
-- tier 值域取自規劃書 §5.1 原文：主贊助／官方／支持。
CREATE TABLE sponsors (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  slug              nvarchar(160)  NOT NULL,
  tier              nvarchar(8)    NULL
                      CHECK (tier IS NULL OR tier IN (N'主贊助', N'官方', N'支持')),
  logo_dark_key     nvarchar(500)  NULL,
  logo_dark_width   int            NULL,
  logo_dark_height  int            NULL,
  logo_light_key    nvarchar(500)  NULL,
  logo_light_width  int            NULL,
  logo_light_height int            NULL,
  contract_start_on date          NULL,
  contract_end_on   date          NULL,
  contact_name      nvarchar(64)  NULL,
  contact_phone     nvarchar(32)  NULL,
  contact_email     nvarchar(255) NULL,
  expiry_alert_on   date          NULL,
  sort_order        int           NOT NULL DEFAULT 0,
  created_at        datetime2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_sponsors PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_sponsors_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 明確依據規劃書 §5.1：「名稱（中／英）」。logo 兩版 alt 依規則 9 補上。
CREATE TABLE sponsors_i18n (
  sponsor_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(128)  NULL,
  logo_dark_alt   nvarchar(255)  NULL,
  logo_light_alt  nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_sponsors_i18n PRIMARY KEY NONCLUSTERED (sponsor_id, locale),
  CONSTRAINT UQ_sponsors_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 贊助方案（9 種）：內容、權益清單、適合對象、價格區間（可設不公開）、上下架。
CREATE TABLE sponsor_packages (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)  NOT NULL,
  price_min       int            NULL,
  price_max       int            NULL,
  is_price_public bit            NOT NULL DEFAULT 1,
  sort_order      int            NOT NULL DEFAULT 0,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_sponsor_packages PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_sponsor_packages_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 明確依據規劃書 §5.1：「名稱（中／英）、方案內容、權益清單」。
CREATE TABLE sponsor_packages_i18n (
  sponsor_package_id uniqueidentifier NOT NULL,
  locale              nvarchar(10)   NOT NULL,
  name                nvarchar(128)  NULL,
  content             nvarchar(max)  NULL,
  benefits            nvarchar(max)  NULL,
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_sponsor_packages_i18n PRIMARY KEY NONCLUSTERED (sponsor_package_id, locale),
  CONSTRAINT UQ_sponsor_packages_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (sponsor_id, sponsor_package_id) 多對多。
CREATE TABLE sponsor_package_links (
  sponsor_id         uniqueidentifier NOT NULL,
  sponsor_package_id uniqueidentifier NOT NULL,
  CONSTRAINT PK_sponsor_package_links PRIMARY KEY CLUSTERED (sponsor_id, sponsor_package_id)
);

-- 提案簡介（多版本、多語 PDF）。title 為單一欄位——本表雙語機制是 proposal_files
-- 每語系一份 PDF，不是文字側表，故不建 proposals_i18n（與 Club／Competition 的
-- 「🌐 但主表已有並排欄位」情形不同類，屬合理設計，非落差）。
CREATE TABLE proposals (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  title           nvarchar(128)  NOT NULL,
  version_no      int            NOT NULL DEFAULT 1,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_proposals PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_proposals_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (proposal_id, locale, file_key, version)。
CREATE TABLE proposal_files (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  proposal_id     uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  file_key        nvarchar(500)  NOT NULL,
  file_bytes      int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_proposal_files PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_proposal_files_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.5 F 文化模組
   ============================================================================ */

-- 漫畫角色，player_id 可為空（可對應真實球員為原型）。
CREATE TABLE comic_characters (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  player_id       uniqueidentifier NULL,
  image_key       nvarchar(500)  NULL,
  image_width     int            NULL,
  image_height    int            NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_comic_characters PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_comic_characters_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name／bio 為角色設定顯示必要欄位推定；image_alt 依規則 9。
CREATE TABLE comic_characters_i18n (
  comic_character_id uniqueidentifier NOT NULL,
  locale               nvarchar(10)   NOT NULL,
  name                 nvarchar(64)   NULL,
  bio                  nvarchar(max)  NULL,
  image_alt            nvarchar(255)  NULL,
  row_seq              bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_comic_characters_i18n PRIMARY KEY NONCLUSTERED (comic_character_id, locale),
  CONSTRAINT UQ_comic_characters_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 集數、閱讀數。is_latest 為自動判定不是人工勾選。
CREATE TABLE comic_episodes (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  episode_no      int            NOT NULL,
  cover_key       nvarchar(500)  NULL,
  cover_width     int            NULL,
  cover_height    int            NULL,
  published_on    date           NULL,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  is_latest       bit            NOT NULL DEFAULT 0,
  view_count      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_comic_episodes PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_comic_episodes_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，title／synopsis 為集數顯示必要欄位推定；cover_alt 依規則 9。
CREATE TABLE comic_episodes_i18n (
  comic_episode_id uniqueidentifier NOT NULL,
  locale            nvarchar(10)   NOT NULL,
  title             nvarchar(128)  NULL,
  synopsis          nvarchar(max)  NULL,
  cover_alt         nvarchar(255)  NULL,
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_comic_episodes_i18n PRIMARY KEY NONCLUSTERED (comic_episode_id, locale),
  CONSTRAINT UQ_comic_episodes_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 內頁 (episode_id, sort_order, image_key)。不帶 club_id，由 comic_episode 推導。
CREATE TABLE comic_pages (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  comic_episode_id uniqueidentifier NOT NULL,
  image_key       nvarchar(500)  NULL,
  image_width     int            NULL,
  image_height    int            NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_comic_pages PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_comic_pages_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs/12 §4.5 未將 ComicPage 標🌐，但本檔的硬性規則 9（圖片 _alt_zh／_alt_en
-- 一律走 i18n 側表）要求逐頁 alt 文字，故新增此表——這是本檔為滿足圖片替代文字規則
-- 而超出 docs §4「約 40 張 i18n 側表」既有清單的新增表，請與 docs/12 核對是否要正式登記。
CREATE TABLE comic_pages_i18n (
  comic_page_id   uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  image_alt       nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_comic_pages_i18n PRIMARY KEY NONCLUSTERED (comic_page_id, locale),
  CONSTRAINT UQ_comic_pages_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 球迷會活動。
CREATE TABLE fan_events (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)  NOT NULL,
  starts_at       datetime2(3)   NOT NULL,
  capacity        int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_fan_events PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_fan_events_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name／description 為活動顯示必要欄位推定。
CREATE TABLE fan_events_i18n (
  fan_event_id    uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(128)  NULL,
  description     nvarchar(max)  NULL,
  seo_title       nvarchar(200)  NULL,
  seo_description nvarchar(300)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_fan_events_i18n PRIMARY KEY NONCLUSTERED (fan_event_id, locale),
  CONSTRAINT UQ_fan_events_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 活動報名，member_id 可為空。
CREATE TABLE fan_event_registrations (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  fan_event_id    uniqueidentifier NOT NULL,
  member_id       uniqueidentifier NULL,
  status          nvarchar(20)   NOT NULL DEFAULT 'pending', -- ⚠️ 待確認：值域未在 docs 列舉
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_fan_event_registrations PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_fan_event_registrations_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.6 G 表單與詢問
   ============================================================================ */

-- 表單定義（7 類 ＋ 提案下載 ＋ 捐助洽詢）：通知信收件者、自動回覆樣板、CAPTCHA 開關、送出後導向。
CREATE TABLE forms (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  form_code       nvarchar(32)   NOT NULL,
  notify_emails   nvarchar(500)  NULL,
  captcha_enabled bit            NOT NULL DEFAULT 1,
  redirect_path   nvarchar(500)  NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_forms PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_forms_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，title／intro_text／success_message 為表單頁面顯示必要文案推定。
CREATE TABLE forms_i18n (
  form_id         uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  title           nvarchar(128)  NULL,
  intro_text      nvarchar(max)  NULL,
  success_message nvarchar(max)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_forms_i18n PRIMARY KEY NONCLUSTERED (form_id, locale),
  CONSTRAINT UQ_forms_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 動態欄位（型別、必填、驗證、排序）。
CREATE TABLE form_fields (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  form_id         uniqueidentifier NOT NULL,
  field_key       nvarchar(64)   NOT NULL,
  field_type      nvarchar(32)   NOT NULL,
  is_required     bit            NOT NULL DEFAULT 0,
  validation_rule nvarchar(255)  NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_form_fields PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_form_fields_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，label／placeholder／help_text 為表單欄位顯示必要文案推定。
CREATE TABLE form_fields_i18n (
  form_field_id   uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  label           nvarchar(128)  NULL,
  placeholder     nvarchar(255)  NULL,
  help_text       nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_form_fields_i18n PRIMARY KEY NONCLUSTERED (form_field_id, locale),
  CONSTRAINT UQ_form_fields_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 收件：來源頁、UTM、狀態、assignee、備註、標籤。涵蓋 7 類表單 ＋ 提案下載 ＋ 捐助洽詢。
CREATE TABLE enquiries (
  id                      uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                 bigint IDENTITY(1,1) NOT NULL,
  club_id                 uniqueidentifier NOT NULL,
  form_id                 uniqueidentifier NOT NULL,
  assignee_admin_user_id  uniqueidentifier NULL,
  source_path             nvarchar(500)  NULL,
  utm_source              nvarchar(255)  NULL,
  utm_campaign            nvarchar(255)  NULL,
  status                  nvarchar(20)   NOT NULL DEFAULT 'new', -- ⚠️ 待確認：值域未在 docs 列舉
  internal_note           nvarchar(max)  NULL,
  created_at              datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at              datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by              uniqueidentifier NULL,
  updated_by              uniqueidentifier NULL,
  CONSTRAINT PK_enquiries PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_enquiries_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (enquiry_id, form_field_id, value)。
CREATE TABLE enquiry_answers (
  enquiry_id      uniqueidentifier NOT NULL,
  form_field_id   uniqueidentifier NOT NULL,
  value           nvarchar(max)  NULL,
  CONSTRAINT PK_enquiry_answers PRIMARY KEY CLUSTERED (enquiry_id, form_field_id)
);

-- 電子報名單：來源、訂閱／退訂狀態。唯一鍵 (club_id, email)——法遵：同一人可以只退訂其中一站。
CREATE TABLE newsletter_subscribers (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  email           nvarchar(255)  NOT NULL,
  source          nvarchar(64)   NULL,
  status          nvarchar(20)   NOT NULL DEFAULT 'subscribed', -- ⚠️ 待確認：值域未在 docs 列舉
  subscribed_at   datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_newsletter_subscribers PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_newsletter_subscribers_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.7 I 網站設定
   ============================================================================ */

-- 主選單／Mega Menu／Footer：多層級（parent_id）、排序、外部連結。
CREATE TABLE menu_items (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  parent_id       uniqueidentifier NULL,
  menu_location   nvarchar(16)   NOT NULL,
  url             nvarchar(500)  NULL,
  is_external     bit            NOT NULL DEFAULT 0,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_menu_items PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_menu_items_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，label 為選單項目顯示必要欄位推定。
CREATE TABLE menu_items_i18n (
  menu_item_id    uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  label           nvarchar(64)   NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_menu_items_i18n PRIMARY KEY NONCLUSTERED (menu_item_id, locale),
  CONSTRAINT UQ_menu_items_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 場地：地址、lat/lng、交通說明、照片。刻意不加 club_id——兩隊共用同一座球場。
-- ⚠️ 待確認：docs/12a 全文從未給出 Venue 的欄位級 ERD 區塊（只作為其他表的 GHOST FK 目標）。
-- 本表依規劃書 §4.9「場地管理：場地名稱、地址、經緯度、交通說明、照片」原文重建欄位，
-- 而非依據 docs/12a 的 ERD（因為根本不存在）。
CREATE TABLE venues (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  address         nvarchar(500)  NULL,
  lat             decimal(9,6)   NULL,
  lng             decimal(9,6)   NULL,
  photo_key       nvarchar(500)  NULL,
  photo_width     int            NULL,
  photo_height    int            NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_venues PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_venues_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：欄位依規劃書「場地名稱」「交通說明」原文重建，非 docs/12a ERD（不存在）。
CREATE TABLE venues_i18n (
  venue_id        uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(128)  NULL,
  transport_note  nvarchar(max)  NULL,
  photo_alt       nvarchar(255)  NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_venues_i18n PRIMARY KEY NONCLUSTERED (venue_id, locale),
  CONSTRAINT UQ_venues_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.8 J 系統管理
   ============================================================================ */

-- 俱樂部主檔（v3.0 新增，後台 J4）。它自己不帶 club_id；它自己就是俱樂部。
-- ⚠️ 待確認：docs/12a ERD（5.11）只給 code／domain／name_zh／name_en／logo_light_key／
-- logo_dark_key／brand_color／invoice_title／tax_id／is_collecting_subject／sort_order／status，
-- 欄位比主站規劃書 §5.1 原文（含簡介、favicon、OG 圖、輔色、官網連結、預設語系）少。
-- 本表依規劃書原文（output/TCRFC_前後台功能規劃書.md 行 1461）補齊，規劃書為上游、
-- 衝突時以其為準；is_collecting_subject 沿用 docs/12a／12b 既有命名
-- （規劃書原文用詞為 is_payment_subject，同一概念、命名不同，非規格衝突）。
-- website_url 與 domain 語意可能重疊，規劃書未進一步區分，本檔兩欄並存待確認。
-- 🌐 標記在 §4.8 出現，但 ERD 給的是並排 name_zh／name_en，本表沿用並排欄位（不建 clubs_i18n），
-- 簡介與圖片 alt 文字同樣採並排 zh/en，與 Club 既有的非側表模式一致。
CREATE TABLE clubs (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  code                  nvarchar(16)   NOT NULL,
  domain                nvarchar(128)  NOT NULL,
  website_url           nvarchar(500)  NULL,
  name_zh               nvarchar(64)   NOT NULL,
  name_en               nvarchar(64)   NULL,
  intro_zh              nvarchar(max)  NULL,
  intro_en              nvarchar(max)  NULL,
  logo_light_key        nvarchar(500)  NULL,
  logo_light_width      int            NULL,
  logo_light_height     int            NULL,
  logo_light_alt_zh     nvarchar(255)  NULL,
  logo_light_alt_en     nvarchar(255)  NULL,
  logo_dark_key         nvarchar(500)  NULL,
  logo_dark_width       int            NULL,
  logo_dark_height      int            NULL,
  logo_dark_alt_zh      nvarchar(255)  NULL,
  logo_dark_alt_en      nvarchar(255)  NULL,
  favicon_key           nvarchar(500)  NULL,
  favicon_width         int            NULL,
  favicon_height        int            NULL,
  og_image_key          nvarchar(500)  NULL,
  og_image_width        int            NULL,
  og_image_height       int            NULL,
  og_image_alt_zh       nvarchar(255)  NULL,
  og_image_alt_en       nvarchar(255)  NULL,
  brand_color_primary   nvarchar(16)   NULL,
  brand_color_secondary nvarchar(16)   NULL,
  default_locale        nvarchar(10)   NULL,
  invoice_title         nvarchar(128)  NULL,
  tax_id                nvarchar(16)   NULL,
  is_collecting_subject bit            NOT NULL DEFAULT 0,
  sort_order            int            NOT NULL DEFAULT 0,
  status                nvarchar(16)   NOT NULL DEFAULT 'active'
                          CHECK (status IN ('active','inactive')), -- ⚠️ 待確認：二值為推定，docs 僅稱「啟用狀態」
  created_at            datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_clubs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_clubs_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 後台帳號。username 是唯一登入識別，不是 Email；primary_club_id 只是站台切換器
-- 的預設值，不是資料範圍。
CREATE TABLE admin_users (
  id                              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                         bigint IDENTITY(1,1) NOT NULL,
  username                        nvarchar(64)   NOT NULL,
  primary_club_id                 uniqueidentifier NULL,
  password_hash                   nvarchar(255)  NOT NULL,
  must_change_password            bit            NOT NULL DEFAULT 1,
  password_changed_at             datetime2(3)   NULL,
  display_name                    nvarchar(64)   NOT NULL,
  email                            nvarchar(255)  NULL,
  status                          nvarchar(20)   NOT NULL DEFAULT 'active', -- ⚠️ 待確認：值域未在 docs 列舉
  is_super_admin                  bit            NOT NULL DEFAULT 0,
  two_factor_enabled              bit            NOT NULL DEFAULT 0,
  two_factor_secret_encrypted     nvarchar(255)  NULL,
  two_factor_confirmed_at         datetime2(3)   NULL,
  failed_attempt_count            int            NOT NULL DEFAULT 0,
  locked_until                    datetime2(3)   NULL,
  last_login_at                   datetime2(3)   NULL,
  locale                          nvarchar(10)   NULL,
  created_at                      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                      uniqueidentifier NULL,
  updated_by                      uniqueidentifier NULL,
  CONSTRAINT PK_admin_users PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_admin_users_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 角色。scope_mode（all_clubs／own_clubs）。九個角色是 is_system = true 的種子資料。
CREATE TABLE admin_roles (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  code            nvarchar(64)   NOT NULL,
  name_zh         nvarchar(64)   NOT NULL,
  name_en         nvarchar(64)   NULL,
  scope_mode      nvarchar(16)   NOT NULL DEFAULT 'all_clubs'
                    CHECK (scope_mode IN ('all_clubs','own_clubs')),
  is_system       bit            NOT NULL DEFAULT 0,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_admin_roles PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_admin_roles_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (admin_user_id, admin_role_id)，多角色取聯集。
CREATE TABLE admin_user_roles (
  admin_user_id   uniqueidentifier NOT NULL,
  admin_role_id   uniqueidentifier NOT NULL,
  CONSTRAINT PK_admin_user_roles PRIMARY KEY CLUSTERED (admin_user_id, admin_role_id)
);

-- 資料範圍：對誰做（俱樂部維度）。到期自動失效不需人工回收。
CREATE TABLE admin_user_clubs (
  admin_user_id   uniqueidentifier NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  granted_on      date           NOT NULL DEFAULT CAST(SYSUTCDATETIME() AS date),
  expires_on      date           NULL,
  granted_by      uniqueidentifier NULL,
  is_active       bit            NOT NULL DEFAULT 1,
  CONSTRAINT PK_admin_user_clubs PRIMARY KEY CLUSTERED (admin_user_id, club_id)
);

-- 資料範圍：對哪一隊（球隊維度）。順帶補上「學院管理者不能改一線隊」這條資料模型上的坑。
CREATE TABLE admin_user_teams (
  admin_user_id   uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  expires_on      date           NULL,
  is_active       bit            NOT NULL DEFAULT 1,
  CONSTRAINT PK_admin_user_teams PRIMARY KEY CLUSTERED (admin_user_id, team_id)
);

-- 權限碼字典 ＋ is_club_scoped。module_code／domain／action 值域明確取自規劃書 §6／docs/12b §7.3。
CREATE TABLE permissions (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  code            nvarchar(96)   NOT NULL,
  module_code     nvarchar(4)    NOT NULL
                    CHECK (module_code IN ('A','B','C','E','F','G','H','I','J','K','L','P','S')),
  submodule_code  nvarchar(8)    NULL,
  domain          nvarchar(32)   NOT NULL
                    CHECK (domain IN ('content','faq','charity','team','program','calendar',
                      'member','business','shop','enquiry','seo','system')),
  action          nvarchar(16)   NOT NULL
                    CHECK (action IN ('view','create','update','delete','publish','export',
                      'translate','execute','reveal')),
  is_club_scoped  bit            NOT NULL DEFAULT 0,
  is_restricted   bit            NOT NULL DEFAULT 0,
  sysadmin_only   bit            NOT NULL DEFAULT 0,
  name_zh         nvarchar(64)   NOT NULL,
  name_en         nvarchar(64)   NULL,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_permissions PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_permissions_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (admin_role_id, permission_id)。scope_type 依據 docs/12b §7.1／§7.4 文字明確存在，
-- 但 ERD（12a §5.11）的欄位區塊漏列此欄——本檔依 12b 文字補上，非自行發明。
-- scope_value json 已於 v3.0 刪除（資料範圍改由 admin_user_clubs／admin_user_teams 承載）。
CREATE TABLE role_permissions (
  admin_role_id   uniqueidentifier NOT NULL,
  permission_id   uniqueidentifier NOT NULL,
  scope_type      nvarchar(16)   NOT NULL DEFAULT 'all'
                    CHECK (scope_type IN ('all','own_teams','academy_only','masked','translate_only')),
  CONSTRAINT PK_role_permissions PRIMARY KEY CLUSTERED (admin_role_id, permission_id)
);

/* ============================================================================
   4.9 K 會員管理
   ============================================================================ */

-- 會員帳號：會員編號、註冊來源、LINE 綁定識別碼（加密）、Email／電話／生日（受限）。
-- 刻意不加 club_id。signup_source 值域取自 docs/12b §6.3（不含 google）。
CREATE TABLE members (
  id                      uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                 bigint IDENTITY(1,1) NOT NULL,
  member_no               nvarchar(32)   NOT NULL,
  email                    nvarchar(255)  NOT NULL,
  password_hash            nvarchar(255)  NOT NULL,
  phone                    nvarchar(32)   NULL,
  birth_on                 date           NULL,
  line_user_id_encrypted   nvarchar(255)  NULL,
  signup_source            nvarchar(16)   NOT NULL
                             CHECK (signup_source IN ('web','line','admin','app')),
  status                   nvarchar(16)   NOT NULL DEFAULT 'active'
                             CHECK (status IN ('active','suspended','deleted')),
  created_at               datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at               datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by               uniqueidentifier NULL,
  updated_by               uniqueidentifier NULL,
  CONSTRAINT PK_members PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_members_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 會籍（v3.0 新增）：member_id × club_id × season_id（三者唯一）。一人每俱樂部一份。
CREATE TABLE memberships (
  id                      uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                 bigint IDENTITY(1,1) NOT NULL,
  member_id                uniqueidentifier NOT NULL,
  club_id                  uniqueidentifier NOT NULL,
  season_id                uniqueidentifier NOT NULL,
  tier                     nvarchar(16)   NOT NULL
                             CHECK (tier IN ('registered','fan_club')),
  membership_start_on      date           NULL,
  membership_end_on        date           NULL,
  status                   nvarchar(20)   NOT NULL DEFAULT 'active', -- ⚠️ 待確認：值域未在 docs 列舉
  created_at               datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at               datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by               uniqueidentifier NULL,
  updated_by               uniqueidentifier NULL,
  CONSTRAINT PK_memberships PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_memberships_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 電子會員卡，一張一列；membership_id 必填——每份會籍一張卡。
CREATE TABLE member_cards (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  membership_id   uniqueidentifier NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  holder_name     nvarchar(64)   NOT NULL,
  token           nvarchar(64)   NOT NULL,
  status          nvarchar(20)   NOT NULL DEFAULT 'active', -- ⚠️ 待確認：值域未在 docs 列舉
  reissue_count   int            NOT NULL DEFAULT 0,
  issued_at       datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_member_cards PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_member_cards_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 會籍方案：費用、season_id、期間、card_quota、jersey_quota、季中計價規則。
-- ⚠️ 待確認：`code` 欄位不在 docs/12a ERD（5.6）欄位區塊內，但 docs/12b §11.1 明文給出
-- 唯一鍵 (club_id, season_id, code)——本檔依 12b 文字補上 code 欄，屬合併兩份文件、非發明。
CREATE TABLE membership_plans (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NOT NULL,
  code            nvarchar(32)   NOT NULL,
  fee             int            NOT NULL,
  card_quota      int            NOT NULL DEFAULT 1,
  jersey_quota    int            NOT NULL DEFAULT 0,
  mid_season_rule nvarchar(255)  NULL,
  status          nvarchar(16)   NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_membership_plans PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_membership_plans_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name／description 為方案顯示必要欄位推定。
CREATE TABLE membership_plans_i18n (
  membership_plan_id uniqueidentifier NOT NULL,
  locale              nvarchar(10)   NOT NULL,
  name                nvarchar(128)  NULL,
  description         nvarchar(max)  NULL,
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_membership_plans_i18n PRIMARY KEY NONCLUSTERED (membership_plan_id, locale),
  CONSTRAINT UQ_membership_plans_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 會籍付款與開通：方式、金額、日期、經辦人、開通起訖；collecting_club_id 供代收代付分帳。
CREATE TABLE membership_payments (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  membership_id          uniqueidentifier NOT NULL,
  club_id                uniqueidentifier NOT NULL,
  collecting_club_id     uniqueidentifier NOT NULL,
  membership_plan_id     uniqueidentifier NOT NULL,
  method                 nvarchar(32)   NULL,
  amount                 int            NOT NULL,
  paid_on                date           NOT NULL,
  note                   nvarchar(255)  NULL,
  handled_by             uniqueidentifier NULL,
  activated_start_on     date           NULL,
  activated_end_on       date           NULL,
  created_at             datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at             datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by             uniqueidentifier NULL,
  updated_by             uniqueidentifier NULL,
  CONSTRAINT PK_membership_payments PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_membership_payments_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 權益對照條目（由父表 membership_plan 推導）：分組、免費層值、付費層值、排序。不帶 club_id。
CREATE TABLE membership_benefits (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  membership_plan_id  uniqueidentifier NOT NULL,
  benefit_group       nvarchar(64)   NOT NULL,
  sort_order          int            NOT NULL DEFAULT 0,
  created_at          datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_membership_benefits PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_membership_benefits_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 明確依據 docs/12 §2.4：group_label／free_value／paid_value。
CREATE TABLE membership_benefits_i18n (
  membership_benefit_id uniqueidentifier NOT NULL,
  locale                 nvarchar(10)   NOT NULL,
  group_label            nvarchar(64)   NULL,
  free_value             nvarchar(255)  NULL,
  paid_value             nvarchar(255)  NULL,
  row_seq                bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_membership_benefits_i18n PRIMARY KEY NONCLUSTERED (membership_benefit_id, locale),
  CONSTRAINT UQ_membership_benefits_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 球衣發放，一件一列：領用人姓名、尺寸、配送方式、地址、狀態。
CREATE TABLE jersey_issues (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  member_id         uniqueidentifier NOT NULL,
  recipient_name    nvarchar(64)   NOT NULL,
  size              nvarchar(16)   NULL,
  delivery_method   nvarchar(32)   NULL,
  address           nvarchar(500)  NULL,
  status            nvarchar(20)   NOT NULL DEFAULT 'pending', -- ⚠️ 待確認：值域未在 docs 列舉
  shipped_on        date           NULL,
  created_at        datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_jersey_issues PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_jersey_issues_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 特約店家（適用範圍可設單一俱樂部或兩隊共同）：類別、地址、電話、營業時間、優惠內容、
-- 適用層級、合作起訖、lat/lng。無金流無分潤。applicable_tier 沿用 Membership.tier 值域
-- （⚠️ 待確認：docs 未明文重申此欄與 Membership.tier 共用同一值域，屬合理推定）。
CREATE TABLE partner_stores (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NULL,
  slug              nvarchar(160)  NOT NULL,
  category          nvarchar(32)   NULL,
  address           nvarchar(500)  NULL,
  lat               decimal(9,6)   NULL,
  lng               decimal(9,6)   NULL,
  phone             nvarchar(32)   NULL,
  applicable_tier   nvarchar(16)   NULL
                      CHECK (applicable_tier IS NULL OR applicable_tier IN ('registered','fan_club')),
  start_on          date           NULL,
  end_on            date           NULL,
  created_at        datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_partner_stores PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_partner_stores_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name／discount_description 依「優惠內容」用途描述推定。
CREATE TABLE partner_stores_i18n (
  partner_store_id   uniqueidentifier NOT NULL,
  locale               nvarchar(10)   NOT NULL,
  name                 nvarchar(128)  NULL,
  discount_description nvarchar(max)  NULL,
  seo_title            nvarchar(200)  NULL,
  seo_description      nvarchar(300)  NULL,
  row_seq              bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_partner_stores_i18n PRIMARY KEY NONCLUSTERED (partner_store_id, locale),
  CONSTRAINT UQ_partner_stores_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 抽獎活動：snapshot_at、開獎時間、領獎期限、狀態、roster_version、total_count、roster_hash。
-- status 值域明確依 docs/12b §6.6。
CREATE TABLE member_draws (
  id                        uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                   bigint IDENTITY(1,1) NOT NULL,
  club_id                    uniqueidentifier NOT NULL,
  draw_code                  nvarchar(32)   NOT NULL,
  snapshot_at                datetime2(3)   NULL,
  drawn_at                   datetime2(3)   NULL,
  draw_occasion               nvarchar(32)   NULL,
  claim_deadline_on          date           NULL,
  status                     nvarchar(16)   NOT NULL DEFAULT 'draft'
                               CHECK (status IN ('draft','roster_locked','drawn','announced','closed','voided')),
  roster_version              int            NOT NULL DEFAULT 1,
  total_count                 int            NOT NULL DEFAULT 0,
  roster_hash                 nvarchar(64)   NULL,
  announcement_article_id     uniqueidentifier NULL,
  locked_by                   uniqueidentifier NULL,
  locked_at                   datetime2(3)   NULL,
  cover_key                   nvarchar(500)  NULL,
  cover_width                 int            NULL,
  cover_height                int            NULL,
  created_at                  datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                  datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                  uniqueidentifier NULL,
  updated_by                  uniqueidentifier NULL,
  CONSTRAINT PK_member_draws PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_member_draws_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 明確依據 docs/12 §2.4：name／prize_description／rules／notes。cover_alt 依規則 9 補上。
CREATE TABLE member_draws_i18n (
  member_draw_id      uniqueidentifier NOT NULL,
  locale                nvarchar(10)   NOT NULL,
  name                  nvarchar(128)  NULL,
  prize_description     nvarchar(max)  NULL,
  rules                 nvarchar(max)  NULL,
  notes                 nvarchar(max)  NULL,
  cover_alt              nvarchar(255)  NULL,
  row_seq                bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_member_draws_i18n PRIMARY KEY NONCLUSTERED (member_draw_id, locale),
  CONSTRAINT UQ_member_draws_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 合格名單快照，一人一列（值複製，不可回頭 join）。claim_method／fulfilment_status
-- 值域明確取自規劃書 §5.1 原文（領取方式：寄送／現場領取；發放狀態：待處理／已寄出／已領取／逾期）。
-- tier_snapshot 沿用 Membership.tier 值域。
CREATE TABLE draw_rosters (
  id                              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                         bigint IDENTITY(1,1) NOT NULL,
  club_id                          uniqueidentifier NOT NULL,
  member_draw_id                   uniqueidentifier NOT NULL,
  serial_no                        int            NOT NULL,
  member_no_snapshot               nvarchar(32)   NULL,
  name_snapshot                    nvarchar(64)   NULL,
  tier_snapshot                    nvarchar(16)   NULL
                                     CHECK (tier_snapshot IS NULL OR tier_snapshot IN ('registered','fan_club')),
  membership_end_on_snapshot       date           NULL,
  is_winner                        bit            NOT NULL DEFAULT 0,
  prize_name                       nvarchar(128)  NULL,
  claim_method                     nvarchar(16)   NULL
                                     CHECK (claim_method IS NULL OR claim_method IN (N'寄送', N'現場領取')),
  fulfilment_status                nvarchar(8)    NULL
                                     CHECK (fulfilment_status IS NULL OR fulfilment_status IN
                                       (N'待處理', N'已寄出', N'已領取', N'逾期')),
  withholding_data_encrypted       nvarchar(255)  NULL,
  note                             nvarchar(max)  NULL,
  created_at                       datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                       datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                       uniqueidentifier NULL,
  updated_by                       uniqueidentifier NULL,
  CONSTRAINT PK_draw_rosters PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_draw_rosters_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.10 L 行事曆管理（calendar_events 視圖見檔尾）
   ============================================================================ */

-- L2 自建事件——行事曆唯一的自有資料：雙語標題、全天／多日、場地、封面、CTA、前台可見性。
CREATE TABLE calendar_custom_events (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  event_type_id   uniqueidentifier NULL,
  venue_id        uniqueidentifier NULL,
  starts_at       datetime2(3)   NOT NULL,
  ends_at         datetime2(3)   NULL,
  is_all_day      bit            NOT NULL DEFAULT 0,
  repeat_rule     nvarchar(32)   NULL, -- ⚠️ 待確認：格式（如 RRULE）未在 docs 指定
  is_public       bit            NOT NULL DEFAULT 1,
  cover_key       nvarchar(500)  NULL,
  cover_width     int            NULL,
  cover_height    int            NULL,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_calendar_custom_events PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_calendar_custom_events_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，title／description 為自建事件顯示必要欄位推定；cover_alt 依規則 9。
CREATE TABLE calendar_custom_events_i18n (
  calendar_custom_event_id uniqueidentifier NOT NULL,
  locale                     nvarchar(10)   NOT NULL,
  title                      nvarchar(128)  NULL,
  description                nvarchar(max)  NULL,
  cover_alt                  nvarchar(255)  NULL,
  row_seq                    bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_calendar_custom_events_i18n PRIMARY KEY NONCLUSTERED (calendar_custom_event_id, locale),
  CONSTRAINT UQ_calendar_custom_events_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);

-- team_codes[] 的關聯表實作 (source_type, source_id, team_id)。多型關聯，無 club_id。
CREATE TABLE calendar_event_teams (
  source_type     nvarchar(16)   NOT NULL,
  source_id       uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  CONSTRAINT PK_calendar_event_teams PRIMARY KEY CLUSTERED (source_type, source_id, team_id)
);

-- L2 重複規則的例外日期。
CREATE TABLE calendar_event_exceptions (
  calendar_custom_event_id uniqueidentifier NOT NULL,
  excluded_on               date           NOT NULL,
  CONSTRAINT PK_calendar_event_exceptions PRIMARY KEY CLUSTERED (calendar_custom_event_id, excluded_on)
);

-- 賽事／活動類型：圖示、色彩、顯示規則、是否公開。icon 為系統預設圖示集代碼（非上傳圖片，
-- 依 docs/12 v3.11 修正確認），不走 _key/_width/_height 模式。
CREATE TABLE event_types (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  code            nvarchar(32)   NOT NULL,
  colour          nvarchar(16)   NULL,
  icon            nvarchar(64)   NULL,
  is_public       bit            NOT NULL DEFAULT 1,
  sort_order      int            NOT NULL DEFAULT 0,
  created_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_event_types PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_event_types_row_seq UNIQUE CLUSTERED (row_seq)
);

-- ⚠️ 待確認：docs 未列出欄位，name 為類型顯示名稱（如「聯賽」「盃賽」）推定。
CREATE TABLE event_types_i18n (
  event_type_id   uniqueidentifier NOT NULL,
  locale          nvarchar(10)   NOT NULL,
  name            nvarchar(64)   NULL,
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  CONSTRAINT PK_event_types_i18n PRIMARY KEY NONCLUSTERED (event_type_id, locale),
  CONSTRAINT UQ_event_types_i18n_row_seq UNIQUE CLUSTERED (row_seq)
);
