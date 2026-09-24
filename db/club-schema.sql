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
   兩個外鍵外空無一欄，已於本版全數補齊（見任務回報）。本版原本以 (a)–(e)
   五組記錄無法單靠 docs/12 系列消解的問題，其中 9 處逐表以行內註解標註
   "-- ⚠️ 待確認"。**2026-09-22 逐處核對 docs/12／12a／12c 與本檔的實際
   內容後**：7 處行內標記已收斂為已確認並拿掉 "⚠️ 待確認" 字樣，只剩
   `trials`／`form_fields` 兩處行內標記仍待確認；另外 (c) 是本來就沒有
   行內標記、只記在此處的法務事項，同樣仍待確認——**合計仍待確認的共
   3 處**（`trials`、`form_fields`、`health_declaration`），逐一說明如下：
     (a) ✅ 已確認（2026-09-22）：clubs 的「簡介」欄位落點是
         clubs_i18n.description（見下方 CREATE TABLE clubs_i18n）。
         docs/12a §5.11 已補畫 club_i18n 實體，docs/12c §3.8 已標
         「✅ 已定案：走側表」。此項原記錄的缺口已補齊，不再是問題。
     (b) ✅ 已確認（2026-09-22）：clubs／competitions 的雙語作法最終走
         側表（clubs_i18n／competitions_i18n），不是並排欄位。
         docs/12a 的兩張主表 ERD 已移除 name_zh／name_en、補畫兩張側表；
         docs/12c §3.2／§3.8 已標「✅ 已定案：走側表」。**本檔下方
         CREATE TABLE clubs／competitions 之前殘留的舊行內註解（曾寫
         「name 走並排欄位」）已一併更正**，organizer 中英拆分沿用
         clubs_i18n／competitions_i18n 側表既有形狀，不再是本檔獨自延伸
         的並排欄位命名。
     (c) 🔴 **仍待確認**：registrations.health_declaration 的分級
         （🔒 明文存 vs 🔐 加密）。docs/12b §8 明文列為待法務確認事項，
         本檔先以 nvarchar(max) 明文儲存（不預先加密，避免加密演算法與
         金鑰管理未定案卻搶先綁架欄位型別），待法務確認後可能需改為
         應用層加密。**這是本檔唯一卡在法務事項、無法由 docs/12 系列
         自行收斂的問題**（(e) 的 `trials`／`form_fields` 是產品範圍
         待確認，性質不同），不屬於本輪 i18n 側表裁決範圍。
     (d) ✅ 已確認：page_blocks 的 content 放在主表（json），docs/12c
         §3.1 的側表草案與主表重複，依 docs/12c §1 第 3 條「主表已放的
         欄位優先尊重主表」不建 page_blocks_i18n。無殘留疑義。
     (e) seasons／standings／achievements／sessions／proposals：
         ✅ 已確認（2026-09-22）不建對應 *_i18n 表——docs/12 曾標🌐，
         但 docs/12c §5 第 4、5 點核對後找不到任何可列的側表欄位（規劃書
         與 ERD 全文查無文字型欄位），docs/12 §4.2／§4.3／§4.4 已同步
         拿掉這 5 個 🌐 標記。
         🔴 **trials／form_fields 仍待確認**：docs/12c §4「信心度低的
         欄位」各列了一個候選（trials 的 `audience`；form_fields 的
         `label`／`placeholder`），本檔目前選擇不建 trials_i18n／
         form_fields_i18n，但這只是「沒人要求所以先不做」的預設值，
         不是對候選欄位是否要雙語做出的正面確認——留待下一輪明確裁決或
         使用者拍板，**不要因為想讓待確認數字歸零就逕自標記為已確認**。
   ========================================================================== */

/* ============================================================================
   4.0 共通機制
   ============================================================================ */

-- 啟用語系字典。加第三語系＝ INSERT 一列，不改 DDL。
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- ⚠️ 上面兩個 SET 是必要的，不是樣板：篩選索引（WHERE ... IS NOT NULL）、
-- 檢視上的索引與計算欄位索引都要求 QUOTED_IDENTIFIER ON，否則建立時會失敗
-- （Msg 1934）。sqlcmd 與部分用戶端預設不是 ON。

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

/* ============================================================================
   4.1 B 內容管理 ＋ H 搜尋與 AI 能見度
   ============================================================================ */

-- 靜態頁面主檔。藍鯨官網入口頁亦屬此型別。唯一鍵 (club_id, slug)。
CREATE TABLE pages (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)    NOT NULL,
  status          nvarchar(16)     NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  published_at    datetime2(3)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_pages PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_pages_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 頁面 SEO 逐語系欄位（docs/12c §3.1：無 title——內文與標題全走區塊編輯器）。
CREATE TABLE pages_i18n (
  page_id         uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  seo_title       nvarchar(200)    NULL,
  seo_description nvarchar(300)    NULL,
  CONSTRAINT PK_pages_i18n PRIMARY KEY CLUSTERED (page_id, locale)
);

-- 頁面區塊（12 種型別，規劃書第 1012 行），content 只存不查。由 Page 推導，不帶 club_id。
-- ✅ 已確認（2026-09-22）：content 已在本表（非側表），docs/12c §3.1 的側表草案與本表重複，
-- 依 docs/12c §1 第 3 條「主表已放的欄位優先」不建 page_blocks_i18n。
-- ⚠️ docs/12 §4.1（PageBlock 列）截至本次同步仍標 🌐，未隨本檔更新——不在本輪 i18n 裁決範圍內，留待下一輪同步。
CREATE TABLE page_blocks (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  page_id         uniqueidentifier NOT NULL,
  block_type      nvarchar(32)     NOT NULL,
  content         json             NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_page_blocks PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_page_blocks_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 頁面版本歷程與還原點、預覽分享 token。由 Page 推導，不帶 club_id。
CREATE TABLE page_versions (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  page_id         uniqueidentifier NOT NULL,
  version_no      int              NOT NULL,
  snapshot        json             NULL,
  preview_token   nvarchar(64)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_page_versions PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_page_versions_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 新聞與故事。club_id 可為空＝兩隊共同；slug 維持全站唯一（共同文章須單一 canonical）。
CREATE TABLE articles (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  club_id             uniqueidentifier NULL,
  slug                nvarchar(160)    NOT NULL,
  article_category_id uniqueidentifier NOT NULL,
  cover_key           nvarchar(500)    NULL,
  is_featured         bit              NOT NULL DEFAULT 0,
  view_count          int              NOT NULL DEFAULT 0,
  status              nvarchar(16)     NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft','published','scheduled')),
  published_at        datetime2(3)     NULL,
  created_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_articles PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_articles_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 文章逐語系內容（docs/12 §2.2 範例表）。
CREATE TABLE articles_i18n (
  article_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  title           nvarchar(200)    NULL,
  summary         nvarchar(max)    NULL,
  body            json             NULL,
  seo_title       nvarchar(200)    NULL,
  seo_description nvarchar(300)    NULL,
  CONSTRAINT PK_articles_i18n PRIMARY KEY CLUSTERED (article_id, locale)
);

-- 7.1–7.8 八分類。刻意不帶 club_id（分類是內容主題，不是俱樂部維度）。
CREATE TABLE article_categories (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  code            nvarchar(32)     NOT NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_article_categories PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_article_categories_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE article_categories_i18n (
  article_category_id uniqueidentifier NOT NULL,
  locale               nvarchar(10)    NOT NULL,
  name                 nvarchar(64)    NULL,
  CONSTRAINT PK_article_categories_i18n PRIMARY KEY CLUSTERED (article_category_id, locale)
);

-- 標籤。刻意不帶 club_id，同 article_categories。
CREATE TABLE tags (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  slug            nvarchar(160)    NOT NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_tags PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_tags_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE tags_i18n (
  tag_id          uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NULL,
  CONSTRAINT PK_tags_i18n PRIMARY KEY CLUSTERED (tag_id, locale)
);

-- 文章與標籤多對多。
CREATE TABLE article_tags (
  article_id      uniqueidentifier NOT NULL,
  tag_id          uniqueidentifier NOT NULL,
  CONSTRAINT PK_article_tags PRIMARY KEY CLUSTERED (article_id, tag_id)
);

-- 文章的多型關聯（target_type 指向 player／team／match／program／partner／charity 等）。
CREATE TABLE article_relations (
  article_id      uniqueidentifier NOT NULL,
  target_type     nvarchar(32)     NOT NULL,
  target_id       uniqueidentifier NOT NULL,
  CONSTRAINT PK_article_relations PRIMARY KEY CLUSTERED (article_id, target_type, target_id)
);

-- 媒體資源（新聞稿／品牌識別包／高解析圖），7.8 媒體專區。
CREATE TABLE press_resources (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NULL,
  slug            nvarchar(160)    NOT NULL,
  resource_type   nvarchar(32)     NOT NULL,
  file_key        nvarchar(500)    NOT NULL,
  file_bytes      int              NULL,
  cover_key       nvarchar(500)    NULL,
  cover_width     int              NULL,
  cover_height    int              NULL,
  published_on    date             NULL,
  download_count  int              NOT NULL DEFAULT 0,
  sort_order      int              NOT NULL DEFAULT 0,
  status          nvarchar(16)     NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_press_resources PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_press_resources_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE press_resources_i18n (
  press_resource_id uniqueidentifier NOT NULL,
  locale             nvarchar(10)    NOT NULL,
  title              nvarchar(200)   NULL,
  description        nvarchar(max)   NULL,
  CONSTRAINT PK_press_resources_i18n PRIMARY KEY CLUSTERED (press_resource_id, locale)
);

-- 首頁 Hero 輪播（≤5）：素材、CTA、上下架期間、排序。
CREATE TABLE banners (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  image_key       nvarchar(500)    NOT NULL,
  start_at        datetime2(3)     NULL,
  end_at          datetime2(3)     NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_banners PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_banners_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE banners_i18n (
  banner_id       uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  title           nvarchar(200)    NULL,
  subtitle        nvarchar(300)    NULL,
  cta_1_label     nvarchar(64)     NULL,
  cta_1_url       nvarchar(500)    NULL,
  cta_2_label     nvarchar(64)     NULL,
  cta_2_url       nvarchar(500)    NULL,
  CONSTRAINT PK_banners_i18n PRIMARY KEY CLUSTERED (banner_id, locale)
);

-- 首頁九大區塊的開關、排序與精選指定。featured_banner_id 承載「精選指定」關聯
-- （docs/12a §5.1 有畫關聯線但主表屬性未列出對應欄位，本檔依關聯線補上此欄）。
CREATE TABLE home_sections (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  club_id             uniqueidentifier NOT NULL,
  section_code        nvarchar(32)     NOT NULL,
  is_enabled          bit              NOT NULL DEFAULT 1,
  sort_order          int              NOT NULL DEFAULT 0,
  featured_banner_id  uniqueidentifier NULL,
  created_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_home_sections PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_home_sections_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 常見問題；👍／👎 計數。
CREATE TABLE faqs (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NULL,
  slug              nvarchar(160)    NOT NULL,
  view_count        int              NOT NULL DEFAULT 0,
  helpful_count     int              NOT NULL DEFAULT 0,
  unhelpful_count   int              NOT NULL DEFAULT 0,
  sort_order        int              NOT NULL DEFAULT 0,
  status            nvarchar(16)     NOT NULL DEFAULT 'draft'
                      CHECK (status IN ('draft','published','scheduled')),
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_faqs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_faqs_row_seq UNIQUE CLUSTERED (row_seq)
);

-- question／answer 為推定欄位（docs/12c §2：B4 原文未逐欄明列）。
CREATE TABLE faqs_i18n (
  faq_id          uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  question        nvarchar(500)    NULL,
  answer          nvarchar(max)    NULL,
  CONSTRAINT PK_faqs_i18n PRIMARY KEY CLUSTERED (faq_id, locale)
);

-- 主題分類（10 個）。刻意不帶 club_id，同 article_categories。
CREATE TABLE faq_categories (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  slug            nvarchar(160)    NOT NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_faq_categories PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_faq_categories_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE faq_categories_i18n (
  faq_category_id uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NULL,
  CONSTRAINT PK_faq_categories_i18n PRIMARY KEY CLUSTERED (faq_category_id, locale)
);

-- 一題可屬多分類。
CREATE TABLE faq_category_links (
  faq_id            uniqueidentifier NOT NULL,
  faq_category_id   uniqueidentifier NOT NULL,
  CONSTRAINT PK_faq_category_links PRIMARY KEY CLUSTERED (faq_id, faq_category_id)
);

-- 零結果搜尋關鍵字與次數（成效統計，不是搜尋日誌）。
CREATE TABLE faq_search_misses (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  keyword           nvarchar(200)    NOT NULL,
  hit_count         int              NOT NULL DEFAULT 1,
  last_searched_at  datetime2(3)     NULL,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_faq_search_misses PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_faq_search_misses_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 301 對照。唯一鍵 (club_id, from_path)——兩站都會有 /zh/about/。
CREATE TABLE redirects (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  from_path       nvarchar(500)    NOT NULL,
  to_path         nvarchar(500)    NOT NULL,
  is_active       bit              NOT NULL DEFAULT 1,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_redirects PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_redirects_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.2 C 球隊管理
   ============================================================================ */

-- 賽事系列（v3.0 新增）：代號、類型、所屬球季、排序、啟用狀態；名稱與主辦單位走 competitions_i18n（見檔頭 (b)）。
CREATE TABLE competitions (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NOT NULL,
  code            nvarchar(16)     NOT NULL,
  comp_type       nvarchar(32)     NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  status          nvarchar(16)     NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_competitions PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_competitions_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 賽事系列的名稱與主辦單位（規劃書行 1463）。
CREATE TABLE competitions_i18n (
  competition_id  uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NOT NULL,
  organizer       nvarchar(128)    NULL,
  CONSTRAINT PK_competitions_i18n PRIMARY KEY CLUSTERED (competition_id, locale)
);

-- 賽季。唯一鍵 (club_id, code)——兩隊球季不同步。
-- ✅ 已確認（2026-09-22）：docs/12 曾標🌐，但規劃書與 ERD 全文查無任何文字型欄位（docs/12c §3.2／§5 第 4 點），
-- 不建 seasons_i18n；docs/12 §4.2 已同步拿掉 🌐。
CREATE TABLE seasons (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  code            nvarchar(16)     NOT NULL,
  start_on        date             NOT NULL,
  end_on          date             NOT NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_seasons PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_seasons_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 球隊。code 全站唯一（行事曆訂閱網址與 /schedule/d1/ 的識別鍵，已在外流通，不得改複合鍵）。
CREATE TABLE teams (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  code            nvarchar(8)      NOT NULL,
  type            nvarchar(16)     NOT NULL CHECK (type IN ('first_team','academy')),
  gender          nvarchar(16)     NOT NULL CHECK (gender IN ('men','women','mixed')),
  age_band        nvarchar(16)     NULL,
  hero_key        nvarchar(500)    NULL,
  team_color      nvarchar(16)     NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_teams PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_teams_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE teams_i18n (
  team_id         uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NULL,
  intro           nvarchar(max)    NULL,
  CONSTRAINT PK_teams_i18n PRIMARY KEY CLUSTERED (team_id, locale)
);

-- 球員：背號、位置、生日、身高體重、國籍、慣用腳、加入日期、狀態。
CREATE TABLE players (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  shirt_no        int              NULL,
  position        nvarchar(32)     NULL,
  birth_on        date             NULL,
  height_cm       int              NULL,
  weight_kg       int              NULL,
  nationality     nvarchar(32)     NULL,
  preferred_foot  nvarchar(16)     NULL,
  joined_on       date             NULL,
  status          nvarchar(16)     NULL,
  photo_key       nvarchar(500)    NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_players PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_players_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE players_i18n (
  player_id       uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NULL,
  bio             nvarchar(max)    NULL,
  CONSTRAINT PK_players_i18n PRIMARY KEY CLUSTERED (player_id, locale)
);

-- 逐季數據。由 Player 推導，不帶 club_id。唯一鍵 (player_id, season_id)。
CREATE TABLE player_season_stats (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  player_id       uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NOT NULL,
  appearances     int              NOT NULL DEFAULT 0,
  goals           int              NOT NULL DEFAULT 0,
  assists         int              NOT NULL DEFAULT 0,
  yellow_cards    int              NOT NULL DEFAULT 0,
  red_cards       int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_player_season_stats PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_player_season_stats_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 教練與團隊成員：證照、專長、分組。club_id 可為空＝兩隊共同（行政與醫療多為共用）。
CREATE TABLE staff (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NULL,
  staff_group     nvarchar(32)     NULL,
  licence         nvarchar(64)     NULL,
  photo_key       nvarchar(500)    NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_staff PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_staff_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE staff_i18n (
  staff_id        uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NULL,
  title           nvarchar(64)     NULL,
  bio             nvarchar(max)    NULL,
  CONSTRAINT PK_staff_i18n PRIMARY KEY CLUSTERED (staff_id, locale)
);

-- 教練／團隊成員與球隊多對多，帶職務代碼。
CREATE TABLE staff_teams (
  staff_id        uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  role_code       nvarchar(64)     NULL,
  CONSTRAINT PK_staff_teams PRIMARY KEY CLUSTERED (staff_id, team_id)
);

-- 賽事。competition_id 可空；對手與場地的英文走 matches_i18n。
-- match_no：聯賽官方配發的場次編號，與 round_no（第幾輪）是兩回事——同一輪可能有多場比賽，
-- 各自有各自的官方編號；非聯賽賽事（如盃賽、友誼賽）可能沒有官方編號，故可為空（docs/12d §9）。
-- original_match_on／original_kickoff（v3.13）：延賽前的原定日期時間，只有 status = 'postponed' 時有值，
-- 沿用 match_on／kickoff 既有的兩欄配對寫法（皆為當地牆上時間展示值，不是 UTC 時間戳）；兩欄皆可為空、
-- 不加 CHECK——status 本身沒有 CHECK 約束（值域仍待確認，見 docs/12d §6），是否必填交後台 C4 表單驗證
-- （docs/12 §12 第 31 點）。
CREATE TABLE matches (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  club_id             uniqueidentifier NOT NULL,
  season_id           uniqueidentifier NOT NULL,
  competition_id      uniqueidentifier NULL,
  venue_id            uniqueidentifier NULL,
  match_on            date             NOT NULL,
  kickoff             nvarchar(8)      NULL,
  home_away           nvarchar(16)     NULL,
  opponent            nvarchar(128)    NULL,
  competition         nvarchar(16)     NULL,
  status              nvarchar(16)     NULL,
  score_home          int              NULL,
  score_away          int              NULL,
  round_no            int              NULL,
  match_no            int              NULL,
  original_match_on   date             NULL,
  original_kickoff    nvarchar(8)      NULL,
  created_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_matches PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_matches_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 對手與場地是自由文字（docs/12 §2.4）；venue 為顯示用文字欄位，與 matches.venue_id（結構化主場地）並存。
CREATE TABLE matches_i18n (
  match_id        uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  opponent        nvarchar(128)    NULL,
  venue           nvarchar(128)    NULL,
  CONSTRAINT PK_matches_i18n PRIMARY KEY CLUSTERED (match_id, locale)
);

-- 本方參賽隊。
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
  player_id       uniqueidentifier NOT NULL,
  minute          int              NULL,
  goal_type       nvarchar(32)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
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
  player_id       uniqueidentifier NOT NULL,
  card_type       nvarchar(16)     NULL,
  minute          int              NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
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
  is_starter      bit              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_match_lineups PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_match_lineups_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 積分榜。對手隊名是自由文字，不是 Team。
-- ✅ 已確認（2026-09-22）：docs/12 曾標🌐，但 team_name 已是主表自由文字，規劃書無其他文字欄位
-- （docs/12c §3.2／§5 第 4 點），不建 standings_i18n；docs/12 §4.2 已同步拿掉 🌐。
CREATE TABLE standings (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  season_id       uniqueidentifier NOT NULL,
  team_name       nvarchar(128)    NOT NULL,
  rank            int              NULL,
  played          int              NULL,
  points          int              NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_standings PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_standings_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 榮譽（年份、賽事、名次、隊伍）。
-- ✅ 已確認（2026-09-22）：docs/12 曾標🌐，但 competition_name／placing 已是主表欄位，規劃書無其他文字欄位
-- （docs/12c §5 第 4 點），不建 achievements_i18n；docs/12 §4.2 已同步拿掉 🌐。
CREATE TABLE achievements (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  season_id         uniqueidentifier NOT NULL,
  team_id           uniqueidentifier NOT NULL,
  year              int              NULL,
  competition_name  nvarchar(128)    NULL,
  placing           nvarchar(32)     NULL,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_achievements PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_achievements_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 里程碑時間軸。
CREATE TABLE milestones (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  happened_on     date             NOT NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_milestones PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_milestones_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE milestones_i18n (
  milestone_id    uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  title           nvarchar(200)    NULL,
  description     nvarchar(max)    NULL,
  CONSTRAINT PK_milestones_i18n PRIMARY KEY CLUSTERED (milestone_id, locale)
);

/* ============================================================================
   4.3 P 課程與活動
   ============================================================================ */

-- 課程／營隊／專項項目：類型、對象、年齡區間、區塊內容。
CREATE TABLE programs (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)    NOT NULL,
  program_type    nvarchar(32)     NULL,
  audience        nvarchar(32)     NULL,
  age_min         int              NULL,
  age_max         int              NULL,
  status          nvarchar(16)     NULL,
  cover_key       nvarchar(500)    NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_programs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_programs_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE programs_i18n (
  program_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(128)    NULL,
  intro           nvarchar(max)    NULL,
  content         json             NULL,
  CONSTRAINT PK_programs_i18n PRIMARY KEY CLUSTERED (program_id, locale)
);

-- 教練團。
CREATE TABLE program_staff (
  program_id      uniqueidentifier NOT NULL,
  staff_id        uniqueidentifier NOT NULL,
  CONSTRAINT PK_program_staff PRIMARY KEY CLUSTERED (program_id, staff_id)
);

-- 合作單位。
CREATE TABLE program_partners (
  program_id      uniqueidentifier NOT NULL,
  partner_id      uniqueidentifier NOT NULL,
  CONSTRAINT PK_program_partners PRIMARY KEY CLUSTERED (program_id, partner_id)
);

-- 梯次／場次：期間、時段、場地、名額、已報名數、價格、報名起訖、狀態。永不進 CalendarEvent。
-- ✅ 已確認（2026-09-22）：docs/12 曾標🌐，但規劃書與 ERD 全文查無任何文字型欄位（docs/12c §3.3／§5 第 4 點），
-- 不建 sessions_i18n；docs/12 §4.3 已同步拿掉 🌐。
CREATE TABLE sessions (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  club_id             uniqueidentifier NOT NULL,
  program_id          uniqueidentifier NOT NULL,
  venue_id            uniqueidentifier NULL,
  start_on            date             NULL,
  end_on              date             NULL,
  weekly_schedule     json             NULL,
  capacity            int              NULL,
  enrolled_count      int              NOT NULL DEFAULT 0,
  price               int              NULL,
  early_bird_price    int              NULL,
  early_bird_until    date             NULL,
  signup_opens_at     datetime2(3)     NULL,
  signup_closes_at    datetime2(3)     NULL,
  status              nvarchar(16)     NULL,
  created_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_sessions PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_sessions_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 報名。member_id 可為空（非會員可報名）；繳費線下。同時服務 session 與 trial，兩外鍵恰有一個非空。
CREATE TABLE registrations (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  registration_no       nvarchar(32)     NOT NULL,
  club_id               uniqueidentifier NOT NULL,
  session_id            uniqueidentifier NULL,
  trial_id              uniqueidentifier NULL,
  member_id             uniqueidentifier NULL,
  applicant_name        nvarchar(64)     NOT NULL,
  phone                 nvarchar(32)     NULL,
  email                 nvarchar(255)    NULL,
  birth_on              date             NULL,
  guardian_name         nvarchar(64)     NULL,
  guardian_phone        nvarchar(32)     NULL,
  health_declaration     nvarchar(max)   NULL,
  note                  nvarchar(max)    NULL,
  status                nvarchar(16)     NOT NULL DEFAULT N'待確認'
                          CHECK (status IN (N'待確認',N'已確認',N'已繳費',N'完成',N'取消',N'候補')),
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_registrations PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_registrations_row_seq UNIQUE CLUSTERED (row_seq),
  CONSTRAINT CK_registrations_session_or_trial CHECK (
    (CASE WHEN session_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN trial_id   IS NULL THEN 0 ELSE 1 END) = 1)
);

-- 試訓場次：日期、場地、對象、名額、截止。同步行事曆由 L3 開關決定，預設關閉。
-- ⚠️ 待確認：docs/12 標🌐，但僅有低信心度候選欄位（audience，docs/12c §4），本版不建 trials_i18n。
CREATE TABLE trials (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  team_id           uniqueidentifier NULL,
  venue_id          uniqueidentifier NULL,
  trial_on          date             NOT NULL,
  capacity          int              NULL,
  deadline_on       date             NULL,
  sync_to_calendar  bit              NOT NULL DEFAULT 0,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_trials PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_trials_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.4 E 商業模組
   ============================================================================ */

-- 合作夥伴（B2B Logo 牆）：Logo 深底／淺底兩版、類型、國家、合作內容與期間、官網、排序、曝光位置。
CREATE TABLE partners (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)    NOT NULL,
  partner_type    nvarchar(32)     NULL,
  country         nvarchar(32)     NULL,
  logo_dark_key   nvarchar(500)    NULL,
  logo_light_key  nvarchar(500)    NULL,
  start_on        date             NULL,
  end_on          date             NULL,
  website_url     nvarchar(500)    NULL,
  show_in_footer  bit              NOT NULL DEFAULT 0,
  show_on_home    bit              NOT NULL DEFAULT 0,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_partners PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_partners_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE partners_i18n (
  partner_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(128)    NULL,
  CONSTRAINT PK_partners_i18n PRIMARY KEY CLUSTERED (partner_id, locale)
);

-- 贊助商：Logo 兩版、等級、合約期間、贊助內容、聯絡窗口、到期提醒、排序。
CREATE TABLE sponsors (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  slug              nvarchar(160)    NOT NULL,
  tier              nvarchar(32)     NULL
                      CHECK (tier IN (N'主贊助',N'官方贊助',N'支持夥伴')),
  logo_dark_key     nvarchar(500)    NULL,
  logo_light_key    nvarchar(500)    NULL,
  contract_start_on date             NULL,
  contract_end_on   date             NULL,
  contact_name      nvarchar(64)     NULL,
  contact_phone     nvarchar(32)     NULL,
  contact_email     nvarchar(255)    NULL,
  expiry_alert_on   date             NULL,
  sort_order        int              NOT NULL DEFAULT 0,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_sponsors PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_sponsors_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE sponsors_i18n (
  sponsor_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(128)    NULL,
  content         nvarchar(max)    NULL,
  CONSTRAINT PK_sponsors_i18n PRIMARY KEY CLUSTERED (sponsor_id, locale)
);

-- 贊助方案（9 種）：內容、權益清單、適合對象、價格區間（可設不公開）、上下架。
CREATE TABLE sponsor_packages (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  slug              nvarchar(160)    NOT NULL,
  price_min         int              NULL,
  price_max         int              NULL,
  is_price_public   bit              NOT NULL DEFAULT 1,
  sort_order        int              NOT NULL DEFAULT 0,
  status            nvarchar(16)     NOT NULL DEFAULT 'draft'
                      CHECK (status IN ('draft','published','scheduled')),
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_sponsor_packages PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_sponsor_packages_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE sponsor_packages_i18n (
  sponsor_package_id uniqueidentifier NOT NULL,
  locale              nvarchar(10)    NOT NULL,
  name                nvarchar(128)   NULL,
  content              nvarchar(max)  NULL,
  benefit_list         nvarchar(max)  NULL,
  audience              nvarchar(128) NULL,
  CONSTRAINT PK_sponsor_packages_i18n PRIMARY KEY CLUSTERED (sponsor_package_id, locale)
);

-- 贊助商與贊助方案多對多。
CREATE TABLE sponsor_package_links (
  sponsor_id          uniqueidentifier NOT NULL,
  sponsor_package_id  uniqueidentifier NOT NULL,
  CONSTRAINT PK_sponsor_package_links PRIMARY KEY CLUSTERED (sponsor_id, sponsor_package_id)
);

-- 提案簡介（多版本、多語 PDF）。title 為單一欄位，多語需求由 proposal_files 承載，不建 proposals_i18n。
-- ✅ 已確認（2026-09-22，docs/12c §5 第 5 點）：docs/12 §4.4 已同步拿掉 Proposal 的 🌐。
CREATE TABLE proposals (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  title           nvarchar(128)    NOT NULL,
  version_no      int              NOT NULL DEFAULT 1,
  status          nvarchar(16)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_proposals PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_proposals_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (proposal_id, locale, file_key, version) 之形狀（docs/12 §4.4）；version_no 對應 ERD 未逐欄畫出但文字明列的欄位。
CREATE TABLE proposal_files (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  proposal_id     uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  file_key        nvarchar(500)    NOT NULL,
  file_bytes      int              NULL,
  version_no      int              NOT NULL DEFAULT 1,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_proposal_files PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_proposal_files_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.5 F 文化模組
   ============================================================================ */

-- 漫畫角色。player_id 可為空（可對應真實球員為原型）。
CREATE TABLE comic_characters (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  player_id       uniqueidentifier NULL,
  image_key       nvarchar(500)    NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_comic_characters PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_comic_characters_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE comic_characters_i18n (
  comic_character_id uniqueidentifier NOT NULL,
  locale               nvarchar(10)   NOT NULL,
  name                 nvarchar(64)   NULL,
  description           nvarchar(max) NULL,
  CONSTRAINT PK_comic_characters_i18n PRIMARY KEY CLUSTERED (comic_character_id, locale)
);

-- 集數、閱讀數。is_latest 為系統自動判定，不是人工勾選。
CREATE TABLE comic_episodes (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  episode_no      int              NOT NULL,
  cover_key       nvarchar(500)    NULL,
  published_on    date             NULL,
  status          nvarchar(16)     NULL,
  is_latest       bit              NOT NULL DEFAULT 0,
  view_count      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_comic_episodes PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_comic_episodes_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE comic_episodes_i18n (
  comic_episode_id uniqueidentifier NOT NULL,
  locale            nvarchar(10)    NOT NULL,
  title             nvarchar(128)   NULL,
  CONSTRAINT PK_comic_episodes_i18n PRIMARY KEY CLUSTERED (comic_episode_id, locale)
);

-- 內頁 (episode_id, sort_order, image_key)。由 comic_episode 推導，不帶 club_id。
CREATE TABLE comic_pages (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  comic_episode_id  uniqueidentifier NOT NULL,
  image_key         nvarchar(500)    NOT NULL,
  sort_order        int              NOT NULL DEFAULT 0,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_comic_pages PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_comic_pages_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 球迷會活動。
CREATE TABLE fan_events (
  id                      uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                 bigint IDENTITY(1,1) NOT NULL,
  club_id                 uniqueidentifier NOT NULL,
  slug                    nvarchar(160)    NOT NULL,
  starts_at               datetime2(3)     NULL,
  capacity                int              NULL,
  is_paid_members_only    bit              NOT NULL DEFAULT 0,
  created_at              datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at              datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by              uniqueidentifier NULL,
  updated_by              uniqueidentifier NULL,
  CONSTRAINT PK_fan_events PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_fan_events_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE fan_events_i18n (
  fan_event_id    uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(128)    NULL,
  description      nvarchar(max)   NULL,
  CONSTRAINT PK_fan_events_i18n PRIMARY KEY CLUSTERED (fan_event_id, locale)
);

-- 活動報名，member_id 可為空。
CREATE TABLE fan_event_registrations (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  fan_event_id    uniqueidentifier NOT NULL,
  member_id       uniqueidentifier NULL,
  status          nvarchar(16)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_fan_event_registrations PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_fan_event_registrations_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.6 G 表單與詢問
   ============================================================================ */

-- 表單定義（7 類 ＋ 提案下載 ＋ 捐助洽詢）：通知信收件者、CAPTCHA 開關、送出後導向。
CREATE TABLE forms (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  form_code         nvarchar(32)     NOT NULL,
  notify_emails     nvarchar(500)    NULL,
  captcha_enabled   bit              NOT NULL DEFAULT 1,
  redirect_path     nvarchar(500)    NULL,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_forms PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_forms_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 自動回覆信模板（docs/12c §3.6：未列出表單名稱／標題欄位，名稱較像固定介面文案，不進本側表）。
-- ✅ 已拍板（2026-09-22，使用者決定，docs/12c §5 第 6 點）：表單顯示名稱維持規劃書 §3.10 固定表格寫死，
-- 不建 forms_i18n.name、不開放後台編輯。
CREATE TABLE forms_i18n (
  form_id           uniqueidentifier NOT NULL,
  locale            nvarchar(10)     NOT NULL,
  auto_reply_body   nvarchar(max)    NULL,
  CONSTRAINT PK_forms_i18n PRIMARY KEY CLUSTERED (form_id, locale)
);

-- 動態欄位（型別、必填、驗證、排序）。
-- ⚠️ 待確認：docs/12 標🌐，但僅有低信心度候選欄位（label／placeholder，docs/12c §4），本版不建 form_fields_i18n。
CREATE TABLE form_fields (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  form_id           uniqueidentifier NOT NULL,
  field_key         nvarchar(64)     NOT NULL,
  field_type        nvarchar(32)     NOT NULL,
  is_required       bit              NOT NULL DEFAULT 0,
  validation_rule   nvarchar(255)    NULL,
  sort_order        int              NOT NULL DEFAULT 0,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_form_fields PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_form_fields_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 收件：來源頁、UTM、狀態、指派、備註、標籤。涵蓋 7 類表單 ＋ 提案下載 ＋ 捐助洽詢。
CREATE TABLE enquiries (
  id                        uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                   bigint IDENTITY(1,1) NOT NULL,
  club_id                   uniqueidentifier NOT NULL,
  form_id                   uniqueidentifier NOT NULL,
  assignee_admin_user_id    uniqueidentifier NULL,
  source_path               nvarchar(500)    NULL,
  utm_source                nvarchar(255)    NULL,
  utm_campaign              nvarchar(255)    NULL,
  status                    nvarchar(16)     NULL,
  internal_note             nvarchar(max)    NULL,
  tags                      nvarchar(255)    NULL,
  created_at                datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                uniqueidentifier NULL,
  updated_by                uniqueidentifier NULL,
  CONSTRAINT PK_enquiries PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_enquiries_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (enquiry_id, form_field_id) → value。
CREATE TABLE enquiry_answers (
  enquiry_id      uniqueidentifier NOT NULL,
  form_field_id   uniqueidentifier NOT NULL,
  value           nvarchar(max)    NULL,
  CONSTRAINT PK_enquiry_answers PRIMARY KEY CLUSTERED (enquiry_id, form_field_id)
);

-- 電子報名單：來源、訂閱／退訂狀態。唯一鍵 (club_id, email)——同一人可只退訂其中一站。
CREATE TABLE newsletter_subscribers (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  email           nvarchar(255)    NOT NULL,
  source          nvarchar(64)     NULL,
  status          nvarchar(16)     NULL,
  subscribed_at   datetime2(3)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
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
  menu_location   nvarchar(16)     NULL,
  url             nvarchar(500)    NULL,
  is_external     bit              NOT NULL DEFAULT 0,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_menu_items PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_menu_items_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE menu_items_i18n (
  menu_item_id    uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  label           nvarchar(64)     NULL,
  CONSTRAINT PK_menu_items_i18n PRIMARY KEY CLUSTERED (menu_item_id, locale)
);

-- 場地：地址、lat／lng、交通說明、照片。刻意不帶 club_id——兩隊共用同一座球場。
CREATE TABLE venues (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  lat             decimal(9,6)     NULL,
  lng             decimal(9,6)     NULL,
  photo_key       nvarchar(500)    NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_venues PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_venues_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE venues_i18n (
  venue_id        uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(128)    NULL,
  address         nvarchar(255)    NULL,
  directions      nvarchar(max)    NULL,
  CONSTRAINT PK_venues_i18n PRIMARY KEY CLUSTERED (venue_id, locale)
);

/* ============================================================================
   4.8 J 系統管理
   ============================================================================ */

-- 俱樂部主檔（後台 J4）。它自己不帶 club_id。刪除 RESTRICT——有任何帶 club_id 的資料就不得刪。
-- 名稱與簡介走 clubs_i18n（見檔頭 (a)(b)），規劃書「簡介（中／英）」落點是 clubs_i18n.description。
CREATE TABLE clubs (
  id                        uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                   bigint IDENTITY(1,1) NOT NULL,
  code                      nvarchar(16)     NOT NULL,
  domain                    nvarchar(128)    NOT NULL,
  logo_light_key            nvarchar(255)    NULL,
  logo_dark_key             nvarchar(255)    NULL,
  favicon_key               nvarchar(255)    NULL,
  og_image_key              nvarchar(255)    NULL,
  brand_color               nvarchar(16)     NULL,
  brand_secondary_color     nvarchar(16)     NULL,
  invoice_title             nvarchar(64)     NULL,
  tax_id                    nvarchar(16)     NULL,
  is_collecting_subject     bit              NOT NULL DEFAULT 1,
  default_locale            nvarchar(10)     NOT NULL DEFAULT N'zh-Hant',
  sort_order                int              NOT NULL DEFAULT 0,
  status                    nvarchar(16)     NULL,
  created_at                datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                uniqueidentifier NULL,
  updated_by                uniqueidentifier NULL,
  CONSTRAINT PK_clubs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_clubs_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 俱樂部的名稱與簡介（規劃書行 1216「代號、名稱與簡介（中／英）」）。
CREATE TABLE clubs_i18n (
  club_id         uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NOT NULL,
  description     nvarchar(max)    NULL,
  CONSTRAINT PK_clubs_i18n PRIMARY KEY CLUSTERED (club_id, locale)
);

-- 後台帳號。username 是唯一登入識別，不是 Email；primary_club_id 只是站台切換器預設值，不是資料範圍。
CREATE TABLE admin_users (
  id                              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                         bigint IDENTITY(1,1) NOT NULL,
  username                        nvarchar(64)     NOT NULL,
  primary_club_id                 uniqueidentifier NULL,
  password_hash                   nvarchar(255)    NOT NULL,
  must_change_password            bit              NOT NULL DEFAULT 1,
  password_changed_at             datetime2(3)     NULL,
  display_name                    nvarchar(64)     NOT NULL,
  email                            nvarchar(255)   NULL,
  status                          nvarchar(16)     NOT NULL DEFAULT 'active',
  is_super_admin                  bit              NOT NULL DEFAULT 0,
  two_factor_enabled              bit              NOT NULL DEFAULT 0,
  two_factor_secret_encrypted     nvarchar(255)    NULL,
  two_factor_confirmed_at         datetime2(3)     NULL,
  failed_attempt_count            int              NOT NULL DEFAULT 0,
  locked_until                    datetime2(3)     NULL,
  last_login_at                   datetime2(3)     NULL,
  locale                          nvarchar(10)     NULL,
  created_at                      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                      uniqueidentifier NULL,
  updated_by                      uniqueidentifier NULL,
  CONSTRAINT PK_admin_users PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_admin_users_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 角色。scope_mode 決定「對誰做」是否受 admin_user_clubs 範圍限制。九個角色是 is_system=1 的種子資料。
CREATE TABLE admin_roles (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  code            nvarchar(64)     NOT NULL,
  name_zh         nvarchar(64)     NOT NULL,
  name_en         nvarchar(64)     NULL,
  scope_mode      nvarchar(16)     NOT NULL DEFAULT 'all_clubs'
                    CHECK (scope_mode IN ('all_clubs','own_clubs')),
  is_system       bit              NOT NULL DEFAULT 0,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_admin_roles PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_admin_roles_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 帳號多角色，取聯集。
CREATE TABLE admin_user_roles (
  admin_user_id   uniqueidentifier NOT NULL,
  admin_role_id   uniqueidentifier NOT NULL,
  CONSTRAINT PK_admin_user_roles PRIMARY KEY CLUSTERED (admin_user_id, admin_role_id)
);

-- 資料範圍：對哪個俱樂部（v3.0 新增）。expires_on 到期自動失效，不需人工回收。
CREATE TABLE admin_user_clubs (
  admin_user_id   uniqueidentifier NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  granted_on      date             NOT NULL,
  expires_on      date             NULL,
  granted_by      uniqueidentifier NULL,
  is_active       bit              NOT NULL DEFAULT 1,
  CONSTRAINT PK_admin_user_clubs PRIMARY KEY CLUSTERED (admin_user_id, club_id)
);

-- 資料範圍：對哪一隊（v3.0 新增）——補上「學院管理者不能改一線隊」這條資料模型上原本沒有欄位可擋的坑。
CREATE TABLE admin_user_teams (
  admin_user_id   uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  expires_on      date             NULL,
  is_active       bit              NOT NULL DEFAULT 1,
  CONSTRAINT PK_admin_user_teams PRIMARY KEY CLUSTERED (admin_user_id, team_id)
);

-- 權限碼字典。後台專用表，比照 admin_roles 用並排 name_zh／name_en，不走 i18n 側表。
CREATE TABLE permissions (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  code              nvarchar(96)     NOT NULL,
  module_code       nvarchar(4)      NOT NULL,
  submodule_code    nvarchar(8)      NULL,
  domain            nvarchar(32)     NULL,
  action            nvarchar(16)     NULL,
  is_club_scoped    bit              NOT NULL DEFAULT 0,
  is_restricted     bit              NOT NULL DEFAULT 0,
  sysadmin_only     bit              NOT NULL DEFAULT 0,
  name_zh           nvarchar(64)     NOT NULL,
  name_en           nvarchar(64)     NULL,
  sort_order        int              NOT NULL DEFAULT 0,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_permissions PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_permissions_row_seq UNIQUE CLUSTERED (row_seq)
);

-- (role_id, permission_id)。scope_type 承載矩陣裡非布林的格子。scope_value json 已刪除（v3.0）。
CREATE TABLE role_permissions (
  admin_role_id   uniqueidentifier NOT NULL,
  permission_id   uniqueidentifier NOT NULL,
  scope_type      nvarchar(32)     NOT NULL DEFAULT 'all'
                    CHECK (scope_type IN
                      ('all','own_teams','academy_only','masked','translate_only','own_clubs')),
  CONSTRAINT PK_role_permissions PRIMARY KEY CLUSTERED (admin_role_id, permission_id)
);

-- 刷新權杖（J1 登入實作，2026-09-23 新增）：加值來源是 docs/12 §13.1「若日後法遵或客戶要求補上，
-- 只需新增表，不需改動既有綱要」——這張是那句話的第一個兌現。token_hash 存 SHA-256，原始權杖
-- 一律不落地；replaced_by_id 串成輪替鏈供偵測「舊權杖被重放」時一次撤銷整條鏈。
-- ⚠️ 2026-09-23（使用者裁決）：拿掉原本的 created_ip／user_agent 兩欄——只寫入、程式裡沒有任何
-- 地方讀取（不做裝置綁定、不做來源比對），且沒有清除機制、會無限累積，功能上等同一份持續增長
-- 的登入位置紀錄，docs/12 §13.1「不能回答」清單裡正好列著「來源 IP」。真的要做裝置綁定或異常
-- 偵測時再加回來（屆時要有讀取端才說得上是功能，不是紀錄）。
CREATE TABLE admin_refresh_tokens (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  admin_user_id     uniqueidentifier NOT NULL,
  token_hash        nvarchar(128)    NOT NULL,
  issued_at         datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  expires_at        datetime2(3)     NOT NULL,
  revoked_at        datetime2(3)     NULL,
  replaced_by_id    uniqueidentifier NULL,
  CONSTRAINT PK_admin_refresh_tokens PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_admin_refresh_tokens_row_seq UNIQUE CLUSTERED (row_seq),
  CONSTRAINT UQ_admin_refresh_tokens_token_hash UNIQUE (token_hash)
);

/* ============================================================================
   4.9 K 會員管理
   ============================================================================ */

-- 會員帳號：會員編號、姓名、註冊來源、LINE 綁定識別碼（加密）、Email／電話／生日（受限）。
-- 刻意不加 club_id——Email 是登入鍵、LINE 綁定 1:1，個資法上的當事人是「人」不是「會籍」。
CREATE TABLE members (
  id                          uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                     bigint IDENTITY(1,1) NOT NULL,
  member_no                   nvarchar(32)     NOT NULL,
  name                        nvarchar(64)     NOT NULL,
  email                       nvarchar(255)    NOT NULL,
  password_hash               nvarchar(255)    NOT NULL,
  phone                       nvarchar(32)     NULL,
  birth_on                    date             NULL,
  line_user_id_encrypted      nvarchar(255)    NULL,
  signup_source                nvarchar(16)    NOT NULL
                                 CHECK (signup_source IN ('web','line','admin','app')),
  status                      nvarchar(16)     NOT NULL DEFAULT 'active'
                                 CHECK (status IN ('active','suspended','deleted')),
  created_at                  datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                  datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                  uniqueidentifier NULL,
  updated_by                  uniqueidentifier NULL,
  CONSTRAINT PK_members PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_members_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 會籍（v3.0 新增）：member_id × club_id × season_id、層級、起訖、狀態。一人每俱樂部一份。
CREATE TABLE memberships (
  id                        uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                   bigint IDENTITY(1,1) NOT NULL,
  member_id                 uniqueidentifier NOT NULL,
  club_id                   uniqueidentifier NOT NULL,
  season_id                 uniqueidentifier NOT NULL,
  tier                      nvarchar(16)     NOT NULL
                              CHECK (tier IN ('registered','fan_club')),
  membership_start_on       date             NULL,
  membership_end_on         date             NULL,
  status                    nvarchar(16)     NULL,
  created_at                datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                uniqueidentifier NULL,
  updated_by                uniqueidentifier NULL,
  CONSTRAINT PK_memberships PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_memberships_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 電子會員卡，一張一列；membership_id 必填——每份會籍一張卡。
CREATE TABLE member_cards (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  membership_id     uniqueidentifier NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  holder_name       nvarchar(64)     NOT NULL,
  token             nvarchar(64)     NOT NULL,
  status            nvarchar(16)     NULL,
  reissue_count     int              NOT NULL DEFAULT 0,
  issued_at         datetime2(3)     NULL,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_member_cards PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_member_cards_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 會籍方案：費用、season、期間、card_quota、jersey_quota、季中計價規則。
-- code 為 docs/12b §11.1 唯一鍵 (club_id, season_id, code) 直接指名的欄位，ERD 主表屬性未逐一畫出，本檔據此補上。
CREATE TABLE membership_plans (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  season_id         uniqueidentifier NOT NULL,
  code              nvarchar(32)     NOT NULL,
  fee               int              NOT NULL,
  card_quota        int              NOT NULL DEFAULT 1,
  jersey_quota      int              NOT NULL DEFAULT 0,
  mid_season_rule   nvarchar(255)    NULL,
  sort_order        int              NOT NULL DEFAULT 0,
  status            nvarchar(16)     NULL,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_membership_plans PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_membership_plans_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE membership_plans_i18n (
  membership_plan_id uniqueidentifier NOT NULL,
  locale               nvarchar(10)   NOT NULL,
  name                 nvarchar(64)   NULL,
  benefit_note          nvarchar(max) NULL,
  CONSTRAINT PK_membership_plans_i18n PRIMARY KEY CLUSTERED (membership_plan_id, locale)
);

-- 會籍付款與開通：方式、金額、日期、經辦人、開通起訖；collecting_club_id 供代收代付分帳。
CREATE TABLE membership_payments (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  membership_id         uniqueidentifier NOT NULL,
  club_id               uniqueidentifier NOT NULL,
  collecting_club_id    uniqueidentifier NOT NULL,
  membership_plan_id    uniqueidentifier NOT NULL,
  method                nvarchar(32)     NULL,
  amount                int              NOT NULL,
  paid_on               date             NULL,
  note                  nvarchar(255)    NULL,
  handled_by            uniqueidentifier NULL,
  activated_start_on    date             NULL,
  activated_end_on      date             NULL,
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_membership_payments PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_membership_payments_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 權益對照條目（由父表 membership_plans 推導）：分組、排序。不帶 club_id。
CREATE TABLE membership_benefits (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  membership_plan_id    uniqueidentifier NOT NULL,
  benefit_group         nvarchar(64)     NOT NULL,
  sort_order            int              NOT NULL DEFAULT 0,
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_membership_benefits PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_membership_benefits_row_seq UNIQUE CLUSTERED (row_seq)
);

-- group_label 為顯示文案（対 benefit_group 代碼的翻譯），free_value／paid_value 為免費／付費層值。
CREATE TABLE membership_benefits_i18n (
  membership_benefit_id uniqueidentifier NOT NULL,
  locale                 nvarchar(10)    NOT NULL,
  group_label            nvarchar(64)    NULL,
  free_value             nvarchar(255)   NULL,
  paid_value             nvarchar(255)   NULL,
  CONSTRAINT PK_membership_benefits_i18n PRIMARY KEY CLUSTERED (membership_benefit_id, locale)
);

-- 球衣發放，一件一列：領用人姓名、尺寸、配送方式、地址、狀態。
CREATE TABLE jersey_issues (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  member_id         uniqueidentifier NOT NULL,
  recipient_name    nvarchar(64)     NOT NULL,
  phone             nvarchar(32)     NULL,
  size              nvarchar(16)     NULL,
  delivery_method   nvarchar(32)     NULL,
  address           nvarchar(500)    NULL,
  status            nvarchar(16)     NULL,
  shipped_on        date             NULL,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_jersey_issues PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_jersey_issues_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 特約店家（適用範圍可設單一俱樂部或兩隊共同）：類別、地址、營業時間、優惠內容、適用層級、合作起訖。無金流無分潤。
CREATE TABLE partner_stores (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  club_id             uniqueidentifier NULL,
  slug                nvarchar(160)    NOT NULL,
  image_key           nvarchar(500)    NULL,
  category            nvarchar(32)     NULL,
  address             nvarchar(500)    NULL,
  lat                 decimal(9,6)     NULL,
  lng                 decimal(9,6)     NULL,
  phone               nvarchar(32)     NULL,
  business_hours      json             NULL,
  website_url         nvarchar(500)    NULL,
  applicable_tier     nvarchar(16)     NULL,
  start_on            date             NULL,
  end_on              date             NULL,
  sort_order          int              NOT NULL DEFAULT 0,
  status              nvarchar(16)     NULL,
  created_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_partner_stores PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_partner_stores_row_seq UNIQUE CLUSTERED (row_seq)
);

-- address 已在主表（docs/12a 既有決定），本側表僅補 name／offer_content（docs/12c §1 第 3 條）。
CREATE TABLE partner_stores_i18n (
  partner_store_id  uniqueidentifier NOT NULL,
  locale             nvarchar(10)    NOT NULL,
  name               nvarchar(128)   NULL,
  offer_content       nvarchar(max)  NULL,
  CONSTRAINT PK_partner_stores_i18n PRIMARY KEY CLUSTERED (partner_store_id, locale)
);

-- 抽獎活動：snapshot_at、開獎時間、領獎期限、狀態、roster_version、total_count、roster_hash。各俱樂部各自舉辦。
CREATE TABLE member_draws (
  id                        uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                   bigint IDENTITY(1,1) NOT NULL,
  club_id                   uniqueidentifier NOT NULL,
  draw_code                 nvarchar(32)     NOT NULL,
  snapshot_at               datetime2(3)     NULL,
  drawn_at                  datetime2(3)     NULL,
  draw_occasion             nvarchar(32)     NULL,
  claim_deadline_on         date             NULL,
  status                    nvarchar(16)     NOT NULL DEFAULT 'draft'
                              CHECK (status IN
                                ('draft','roster_locked','drawn','announced','closed','voided')),
  roster_version            int              NOT NULL DEFAULT 1,
  total_count               int              NULL,
  roster_hash               nvarchar(64)     NULL,
  announcement_article_id   uniqueidentifier NULL,
  locked_by                 uniqueidentifier NULL,
  locked_at                 datetime2(3)     NULL,
  cover_key                 nvarchar(500)    NULL,
  created_at                datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                uniqueidentifier NULL,
  updated_by                uniqueidentifier NULL,
  CONSTRAINT PK_member_draws PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_member_draws_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE member_draws_i18n (
  member_draw_id    uniqueidentifier NOT NULL,
  locale             nvarchar(10)    NOT NULL,
  name                nvarchar(128)  NULL,
  prize_description    nvarchar(max) NULL,
  rules                 nvarchar(max)NULL,
  notes                  nvarchar(max) NULL,
  CONSTRAINT PK_member_draws_i18n PRIMARY KEY CLUSTERED (member_draw_id, locale)
);

-- 合格名單快照，一人一列。值複製，鎖定後不得重排。
CREATE TABLE draw_rosters (
  id                              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                         bigint IDENTITY(1,1) NOT NULL,
  club_id                         uniqueidentifier NOT NULL,
  member_draw_id                  uniqueidentifier NOT NULL,
  serial_no                       int              NOT NULL,
  member_no_snapshot               nvarchar(32)    NOT NULL,
  name_snapshot                    nvarchar(64)    NULL,
  tier_snapshot                     nvarchar(16)   NULL CHECK (tier_snapshot IN ('registered','fan_club')),
  membership_end_on_snapshot         date          NULL,
  is_winner                        bit              NOT NULL DEFAULT 0,
  prize_name                       nvarchar(128)    NULL,
  claim_method                     nvarchar(32)     NULL,
  fulfilment_status                nvarchar(16)     NULL,
  withholding_data_encrypted        nvarchar(255)   NULL,
  note                              nvarchar(max)   NULL,
  created_at                       datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                       datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                       uniqueidentifier NULL,
  updated_by                       uniqueidentifier NULL,
  CONSTRAINT PK_draw_rosters PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_draw_rosters_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.10 L 行事曆管理（含 1 視圖，視圖定義於本檔最末）
   ============================================================================ */

-- L2 自建事件——行事曆唯一的自有資料：雙語標題、全天／多日、場地、封面、CTA、前台可見性。
CREATE TABLE calendar_custom_events (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  event_type_id   uniqueidentifier NULL,
  venue_id        uniqueidentifier NULL,
  starts_at       datetime2(3)     NOT NULL,
  ends_at         datetime2(3)     NULL,
  is_all_day      bit              NOT NULL DEFAULT 0,
  repeat_rule     nvarchar(32)     NULL,
  is_public       bit              NOT NULL DEFAULT 1,
  cover_key       nvarchar(500)    NULL,
  cta_url         nvarchar(500)    NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_calendar_custom_events PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_calendar_custom_events_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE calendar_custom_events_i18n (
  calendar_custom_event_id uniqueidentifier NOT NULL,
  locale                    nvarchar(10)    NOT NULL,
  title                     nvarchar(128)   NULL,
  description                nvarchar(max) NULL,
  CONSTRAINT PK_calendar_custom_events_i18n PRIMARY KEY CLUSTERED (calendar_custom_event_id, locale)
);

-- team_codes[] 的關聯表實作：(source_type, source_id, team_id)。
CREATE TABLE calendar_event_teams (
  source_type     nvarchar(16)     NOT NULL,
  source_id       uniqueidentifier NOT NULL,
  team_id         uniqueidentifier NOT NULL,
  CONSTRAINT PK_calendar_event_teams PRIMARY KEY CLUSTERED (source_type, source_id, team_id)
);

-- L2 重複規則的例外日期。
CREATE TABLE calendar_event_exceptions (
  calendar_custom_event_id uniqueidentifier NOT NULL,
  excluded_on               date            NOT NULL,
  CONSTRAINT PK_calendar_event_exceptions PRIMARY KEY CLUSTERED (calendar_custom_event_id, excluded_on)
);

-- 賽事／活動類型：圖示、色彩、顯示規則、是否公開。不帶 club_id。
CREATE TABLE event_types (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  code            nvarchar(32)     NOT NULL,
  colour          nvarchar(16)     NULL,
  icon            nvarchar(64)     NULL,
  is_public       bit              NOT NULL DEFAULT 1,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_event_types PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_event_types_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE event_types_i18n (
  event_type_id   uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NULL,
  CONSTRAINT PK_event_types_i18n PRIMARY KEY CLUSTERED (event_type_id, locale)
);

/* ============================================================================
   4.11 S 商店
   ============================================================================ */

-- 商品分類，含品牌敘事區塊。
CREATE TABLE collections (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)    NOT NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  status          nvarchar(16)     NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_collections PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_collections_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE collections_i18n (
  collection_id   uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(64)     NULL,
  narrative        nvarchar(max)   NULL,
  CONSTRAINT PK_collections_i18n PRIMARY KEY CLUSTERED (collection_id, locale)
);

-- 商品：分類、標籤、敘事、尺碼表、狀態（含缺貨自動判定）、排序、SEO。無會員價欄位。
CREATE TABLE products (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  slug            nvarchar(160)    NOT NULL,
  collection_id   uniqueidentifier NULL,
  is_new_arrival  bit              NOT NULL DEFAULT 0,
  size_chart      json             NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  status          nvarchar(16)     NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_products PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_products_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE products_i18n (
  product_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(128)    NULL,
  narrative        nvarchar(max)   NULL,
  seo_title         nvarchar(200)  NULL,
  seo_description    nvarchar(300) NULL,
  tags                nvarchar(255)NULL,
  CONSTRAINT PK_products_i18n PRIMARY KEY CLUSTERED (product_id, locale)
);

-- 圖集 (product_id, sort_order, image_key)。由 Product 推導，不帶 club_id。
CREATE TABLE product_images (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  product_id      uniqueidentifier NOT NULL,
  image_key       nvarchar(500)    NOT NULL,
  width           int              NULL,
  height          int              NULL,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_product_images PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_product_images_row_seq UNIQUE CLUSTERED (row_seq)
);

-- SKU：尺寸／顏色、貨號（維持全站唯一）、售價、促銷價、成本（受限）、庫存量、預留量。
CREATE TABLE product_variants (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  product_id      uniqueidentifier NOT NULL,
  sku             nvarchar(64)     NOT NULL,
  size            nvarchar(32)     NULL,
  colour          nvarchar(32)     NULL,
  price           int              NOT NULL,
  sale_price      int              NULL,
  cost            int              NULL,
  stock_qty       int              NOT NULL DEFAULT 0,
  reserved_qty    int              NOT NULL DEFAULT 0,
  status          nvarchar(16)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_product_variants PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_product_variants_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 庫存異動：類型、數量、原因、經辦人、時間、關聯訂單。功能單元，不是日誌。
CREATE TABLE inventory_movements (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  club_id               uniqueidentifier NOT NULL,
  product_variant_id    uniqueidentifier NOT NULL,
  order_id              uniqueidentifier NULL,
  movement_type         nvarchar(32)     NULL,
  quantity              int              NOT NULL,
  reason                nvarchar(255)    NULL,
  handled_by            uniqueidentifier NULL,
  occurred_at           datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_inventory_movements PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_inventory_movements_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 購物車：member_id（可空）或 anonymous_token；登入後合併。不得跨俱樂部混買，切換站台即切換購物車。
CREATE TABLE carts (
  id                uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq           bigint IDENTITY(1,1) NOT NULL,
  club_id           uniqueidentifier NOT NULL,
  member_id         uniqueidentifier NULL,
  anonymous_token   nvarchar(64)     NULL,
  created_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at        datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by        uniqueidentifier NULL,
  updated_by        uniqueidentifier NULL,
  CONSTRAINT PK_carts PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_carts_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE cart_items (
  cart_id             uniqueidentifier NOT NULL,
  product_variant_id  uniqueidentifier NOT NULL,
  quantity            int              NOT NULL DEFAULT 1,
  CONSTRAINT PK_cart_items PRIMARY KEY CLUSTERED (cart_id, product_variant_id)
);

-- 訂單：訂單編號（維持全站唯一，加前綴）、member_id 可為空、收件人資料（受限）、金額、
-- LINE Pay 交易編號與付款狀態、出貨狀態、查詢 token、is_manual。selling_club_id（受益方）＋ collecting_club_id（收款法人）。
CREATE TABLE orders (
  id                        uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                   bigint IDENTITY(1,1) NOT NULL,
  order_no                  nvarchar(32)     NOT NULL,
  club_id                   uniqueidentifier NOT NULL,
  selling_club_id            uniqueidentifier NOT NULL,
  collecting_club_id          uniqueidentifier NOT NULL,
  member_id                 uniqueidentifier NULL,
  lookup_token               nvarchar(64)    NOT NULL,
  recipient_name             nvarchar(64)    NULL,
  recipient_phone            nvarchar(32)    NULL,
  recipient_address           nvarchar(500)  NULL,
  subtotal                  int              NOT NULL,
  shipping_fee               int             NOT NULL DEFAULT 0,
  total                     int              NOT NULL,
  linepay_transaction_id      nvarchar(64)   NULL,
  payment_status             nvarchar(16)    NOT NULL DEFAULT 'pending'
                               CHECK (payment_status IN ('pending','paid','failed','expired','refunded')),
  order_status               nvarchar(16)    NOT NULL DEFAULT N'待付款'
                               CHECK (order_status IN
                                 (N'待付款',N'已付款',N'備貨中',N'已出貨',N'已完成',N'已取消',N'退貨處理中',N'已退款')),
  delivery_method             nvarchar(16)   NULL
                               CHECK (delivery_method IN ('home_delivery','cvs_pickup','onsite_pickup')),
  is_manual                  bit             NOT NULL DEFAULT 0,
  paid_at                    datetime2(3)    NULL,
  created_at                 datetime2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                 datetime2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                 uniqueidentifier NULL,
  updated_by                 uniqueidentifier NULL,
  CONSTRAINT PK_orders PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_orders_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 訂單品項：SKU 快照（值複製），club_id 值複製自 Order，絕對不可為空。讀取不得回頭 join 取名稱與價格。
CREATE TABLE order_items (
  id                        uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq                   bigint IDENTITY(1,1) NOT NULL,
  club_id                   uniqueidentifier NOT NULL,
  order_id                  uniqueidentifier NOT NULL,
  product_variant_id        uniqueidentifier NOT NULL,
  product_name_snapshot      nvarchar(200)   NOT NULL,
  variant_label_snapshot      nvarchar(64)   NULL,
  sku_snapshot                nvarchar(64)   NOT NULL,
  unit_price_snapshot         int            NOT NULL,
  quantity                  int              NOT NULL,
  line_total                 int             NOT NULL,
  created_at                 datetime2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at                 datetime2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by                 uniqueidentifier NULL,
  updated_by                 uniqueidentifier NULL,
  CONSTRAINT PK_order_items PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_order_items_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 出貨：物流方式、單號（CSV 回填，不串物流商 API）、時間、超商門市代碼、自取領取狀態。
CREATE TABLE shipments (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  club_id               uniqueidentifier NOT NULL,
  order_id              uniqueidentifier NOT NULL,
  carrier               nvarchar(32)     NULL,
  tracking_no           nvarchar(64)     NULL,
  store_branch_code     nvarchar(32)     NULL,
  shipped_at            datetime2(3)     NULL,
  delivered_at          datetime2(3)     NULL,
  pickup_status         nvarchar(16)     NULL,
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_shipments PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_shipments_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 退貨退款申請：原因、狀態、退款金額與方式。
CREATE TABLE refund_requests (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NOT NULL,
  order_id        uniqueidentifier NOT NULL,
  reason          nvarchar(255)    NULL,
  status          nvarchar(16)     NULL,
  refund_amount   int              NULL,
  refund_method   nvarchar(32)     NULL,
  approved_by     uniqueidentifier NULL,
  refunded_at     datetime2(3)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_refund_requests PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_refund_requests_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 退貨品項（支援部分退款）。
CREATE TABLE refund_request_items (
  refund_request_id  uniqueidentifier NOT NULL,
  order_item_id       uniqueidentifier NOT NULL,
  quantity            int              NOT NULL,
  CONSTRAINT PK_refund_request_items PRIMARY KEY CLUSTERED (refund_request_id, order_item_id)
);

-- 電子發票：號碼、開立時間、載具／統編／捐贈碼三選一、開立結果與重試、作廢與折讓。club_id 值複製自 Order。
CREATE TABLE store_invoices (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  club_id               uniqueidentifier NOT NULL,
  order_id              uniqueidentifier NOT NULL,
  payment_channel_id    uniqueidentifier NULL,
  invoice_no            nvarchar(32)     NULL,
  issued_at             datetime2(3)     NULL,
  carrier_type          nvarchar(16)     NULL,
  carrier_id_encrypted   nvarchar(64)    NULL,
  tax_id                nvarchar(16)     NULL,
  donation_code         nvarchar(16)     NULL,
  issue_status          nvarchar(16)     NOT NULL DEFAULT 'pending'
                          CHECK (issue_status IN ('pending','issued','failed')),
  retry_count           int              NOT NULL DEFAULT 0,
  void_status           nvarchar(16)     NOT NULL DEFAULT 'none'
                          CHECK (void_status IN ('none','voided','allowance')),
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_store_invoices PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_store_invoices_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 捐贈碼名單（S6 維護）。全系統共用，不帶 club_id。
CREATE TABLE invoice_donation_codes (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  code            nvarchar(16)     NOT NULL,
  org_name        nvarchar(128)    NOT NULL,
  is_active       bit              NOT NULL DEFAULT 1,
  sort_order      int              NOT NULL DEFAULT 0,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_invoice_donation_codes PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_invoice_donation_codes_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 金流與發票憑證：owner_club_id（非 club_id）、channel_type、environment、憑證（加密）、字軌、輪替時間。
-- 主站只會有俱樂部一列；協會憑證在慈善獨立庫，三方 LINE Pay 商店號一律不得共用。
CREATE TABLE payment_channels (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  owner_club_id         uniqueidentifier NOT NULL,
  channel_type          nvarchar(16)     NOT NULL CHECK (channel_type IN ('linepay','einvoice')),
  environment           nvarchar(16)     NOT NULL CHECK (environment IN ('sandbox','production')),
  credential_encrypted   nvarchar(500)   NOT NULL,
  invoice_prefix        nvarchar(16)     NULL,
  rotated_at            datetime2(3)     NULL,
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_payment_channels PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_payment_channels_row_seq UNIQUE CLUSTERED (row_seq)
);

/* ============================================================================
   4.12 B6 慈善內容（主站）——主檔留在主站，慈善獨立庫只持有唯讀快照
   ============================================================================ */

-- 受贈公益團體：名稱、簡介、Logo、官網。
CREATE TABLE charities (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NULL,
  slug            nvarchar(160)    NOT NULL,
  logo_key        nvarchar(500)    NULL,
  website_url     nvarchar(500)    NULL,
  contact_name    nvarchar(64)     NULL,
  contact_phone   nvarchar(32)     NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_charities PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_charities_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE charities_i18n (
  charity_id      uniqueidentifier NOT NULL,
  locale          nvarchar(10)     NOT NULL,
  name            nvarchar(128)    NULL,
  intro            nvarchar(max)   NULL,
  CONSTRAINT PK_charities_i18n PRIMARY KEY CLUSTERED (charity_id, locale)
);

-- 已執行的公益計畫（11.2）：cover_key 封面、對象、期間、狀態、流程。
CREATE TABLE charity_programs (
  id              uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq         bigint IDENTITY(1,1) NOT NULL,
  club_id         uniqueidentifier NULL,
  slug            nvarchar(160)    NOT NULL,
  charity_id      uniqueidentifier NOT NULL,
  start_on        date             NULL,
  end_on          date             NULL,
  status          nvarchar(16)     NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','published','scheduled')),
  cover_key       nvarchar(500)    NULL,
  created_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at      datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by      uniqueidentifier NULL,
  updated_by      uniqueidentifier NULL,
  CONSTRAINT PK_charity_programs PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_charity_programs_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE charity_programs_i18n (
  charity_program_id uniqueidentifier NOT NULL,
  locale               nvarchar(10)   NOT NULL,
  name                 nvarchar(128)  NULL,
  target_audience        nvarchar(200) NULL,
  content                 json         NULL,
  donation_content          nvarchar(max) NULL,
  CONSTRAINT PK_charity_programs_i18n PRIMARY KEY CLUSTERED (charity_program_id, locale)
);

-- 圖集（§3.11「活動圖片藝廊」）(charity_program_id, image_key, sort_order)。
CREATE TABLE charity_program_images (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  charity_program_id  uniqueidentifier NOT NULL,
  image_key           nvarchar(500)    NOT NULL,
  sort_order          int              NOT NULL DEFAULT 0,
  created_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_charity_program_images PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_charity_program_images_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 慈善事蹟紀錄。三項核心資料必填：公益團體名稱（經 charity_id 取得）、捐助內容、活動圖片。
CREATE TABLE impact_records (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  club_id               uniqueidentifier NULL,
  charity_program_id    uniqueidentifier NULL,
  charity_id            uniqueidentifier NOT NULL,
  image_key             nvarchar(500)    NULL,
  image_width           int              NULL,
  image_height          int              NULL,
  happened_on           date             NULL,
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_impact_records PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_impact_records_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 三欄補齊 docs/12a 主表遺漏的必填文字欄位落點（docs/12c §3.12、§5 第 3 點）。
CREATE TABLE impact_records_i18n (
  impact_record_id  uniqueidentifier NOT NULL,
  locale              nvarchar(10)   NOT NULL,
  donation_content      nvarchar(max) NULL,
  location                nvarchar(128) NULL,
  brief_description         nvarchar(max) NULL,
  CONSTRAINT PK_impact_records_i18n PRIMARY KEY CLUSTERED (impact_record_id, locale)
);

-- 圖集（S0-3d 新增，行 1025「活動圖片（可多張）」的子表承接）。
CREATE TABLE impact_record_images (
  id                  uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq             bigint IDENTITY(1,1) NOT NULL,
  impact_record_id    uniqueidentifier NOT NULL,
  image_key           nvarchar(500)    NOT NULL,
  sort_order          int              NOT NULL DEFAULT 0,
  created_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at          datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by          uniqueidentifier NULL,
  updated_by          uniqueidentifier NULL,
  CONSTRAINT PK_impact_record_images PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_impact_record_images_row_seq UNIQUE CLUSTERED (row_seq)
);

-- 影響力統計項目（金額類預設不公開）。
CREATE TABLE impact_metrics (
  id                    uniqueidentifier NOT NULL DEFAULT NEWID(),
  row_seq               bigint IDENTITY(1,1) NOT NULL,
  club_id               uniqueidentifier NULL,
  charity_program_id    uniqueidentifier NOT NULL,
  metric_key            nvarchar(64)     NOT NULL,
  metric_value          int              NULL,
  is_public             bit              NOT NULL DEFAULT 0,
  created_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at            datetime2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by            uniqueidentifier NULL,
  updated_by            uniqueidentifier NULL,
  CONSTRAINT PK_impact_metrics PRIMARY KEY NONCLUSTERED (id),
  CONSTRAINT UQ_impact_metrics_row_seq UNIQUE CLUSTERED (row_seq)
);

CREATE TABLE impact_metrics_i18n (
  impact_metric_id  uniqueidentifier NOT NULL,
  locale             nvarchar(10)    NOT NULL,
  name               nvarchar(64)    NULL,
  CONSTRAINT PK_impact_metrics_i18n PRIMARY KEY CLUSTERED (impact_metric_id, locale)
);

/* ============================================================================
   統一建唯一鍵（業務語意的唯一鍵；row_seq 叢集唯一鍵已隨主鍵定義於建表區）
   ============================================================================ */

-- 全站維持唯一（docs/12b §11.1，刻意不改複合鍵的五個例外）
ALTER TABLE teams            ADD CONSTRAINT UQ_teams_code            UNIQUE (code);
ALTER TABLE articles         ADD CONSTRAINT UQ_articles_slug         UNIQUE (slug);
ALTER TABLE product_variants ADD CONSTRAINT UQ_product_variants_sku  UNIQUE (sku);
ALTER TABLE orders           ADD CONSTRAINT UQ_orders_order_no       UNIQUE (order_no);
ALTER TABLE orders           ADD CONSTRAINT UQ_orders_lookup_token   UNIQUE (lookup_token);
ALTER TABLE members          ADD CONSTRAINT UQ_members_member_no     UNIQUE (member_no);
ALTER TABLE members          ADD CONSTRAINT UQ_members_email         UNIQUE (email);

-- 不帶 club_id 的內容字典（全站唯一）
ALTER TABLE article_categories ADD CONSTRAINT UQ_article_categories_code UNIQUE (code);
ALTER TABLE tags               ADD CONSTRAINT UQ_tags_slug               UNIQUE (slug);
ALTER TABLE faq_categories     ADD CONSTRAINT UQ_faq_categories_slug     UNIQUE (slug);
ALTER TABLE event_types        ADD CONSTRAINT UQ_event_types_code        UNIQUE (code);
ALTER TABLE invoice_donation_codes ADD CONSTRAINT UQ_invoice_donation_codes_code UNIQUE (code);
ALTER TABLE ui_strings         ADD CONSTRAINT UQ_ui_strings_string_key   UNIQUE (string_key);
ALTER TABLE admin_roles        ADD CONSTRAINT UQ_admin_roles_code        UNIQUE (code);
ALTER TABLE permissions        ADD CONSTRAINT UQ_permissions_code        UNIQUE (code);
ALTER TABLE admin_users        ADD CONSTRAINT UQ_admin_users_username    UNIQUE (username);
ALTER TABLE registrations      ADD CONSTRAINT UQ_registrations_registration_no UNIQUE (registration_no);

-- club_id 必填或可為空的複合唯一鍵：UNIQUE (club_id, 業務鍵)——SQL Server 把 NULL 當相等，
-- 可為空 club_id 的表（Article 除外，見上）用同一寫法即可，不加篩選唯一索引（docs/12 §1.4 第 5 件）
ALTER TABLE settings              ADD CONSTRAINT UQ_settings_club_key             UNIQUE (club_id, setting_key);
ALTER TABLE email_templates       ADD CONSTRAINT UQ_email_templates_club_code     UNIQUE (club_id, template_code);
ALTER TABLE pages                 ADD CONSTRAINT UQ_pages_club_slug               UNIQUE (club_id, slug);
ALTER TABLE press_resources       ADD CONSTRAINT UQ_press_resources_club_slug     UNIQUE (club_id, slug);
ALTER TABLE home_sections         ADD CONSTRAINT UQ_home_sections_club_code       UNIQUE (club_id, section_code);
ALTER TABLE faqs                  ADD CONSTRAINT UQ_faqs_club_slug                UNIQUE (club_id, slug);
ALTER TABLE redirects             ADD CONSTRAINT UQ_redirects_club_path           UNIQUE (club_id, from_path);
ALTER TABLE faq_search_misses     ADD CONSTRAINT UQ_faq_search_misses_club_keyword UNIQUE (club_id, keyword);
ALTER TABLE competitions          ADD CONSTRAINT UQ_competitions_club_code        UNIQUE (club_id, code);
ALTER TABLE seasons               ADD CONSTRAINT UQ_seasons_club_code             UNIQUE (club_id, code);
ALTER TABLE player_season_stats   ADD CONSTRAINT UQ_player_season_stats_player_season UNIQUE (player_id, season_id);
ALTER TABLE programs              ADD CONSTRAINT UQ_programs_club_slug            UNIQUE (club_id, slug);
ALTER TABLE partners              ADD CONSTRAINT UQ_partners_club_slug            UNIQUE (club_id, slug);
ALTER TABLE sponsors              ADD CONSTRAINT UQ_sponsors_club_slug            UNIQUE (club_id, slug);
ALTER TABLE sponsor_packages      ADD CONSTRAINT UQ_sponsor_packages_club_slug    UNIQUE (club_id, slug);
ALTER TABLE fan_events            ADD CONSTRAINT UQ_fan_events_club_slug          UNIQUE (club_id, slug);
ALTER TABLE forms                 ADD CONSTRAINT UQ_forms_club_code               UNIQUE (club_id, form_code);
ALTER TABLE newsletter_subscribers ADD CONSTRAINT UQ_newsletter_subscribers_club_email UNIQUE (club_id, email);
ALTER TABLE memberships           ADD CONSTRAINT UQ_memberships_member_club_season UNIQUE (member_id, club_id, season_id);
ALTER TABLE member_cards          ADD CONSTRAINT UQ_member_cards_token            UNIQUE (token);
ALTER TABLE membership_plans      ADD CONSTRAINT UQ_membership_plans_club_season_code UNIQUE (club_id, season_id, code);
ALTER TABLE partner_stores        ADD CONSTRAINT UQ_partner_stores_club_slug      UNIQUE (club_id, slug);
ALTER TABLE member_draws          ADD CONSTRAINT UQ_member_draws_club_code        UNIQUE (club_id, draw_code);
ALTER TABLE draw_rosters          ADD CONSTRAINT UQ_draw_rosters_draw_serial      UNIQUE (member_draw_id, serial_no);
ALTER TABLE draw_rosters          ADD CONSTRAINT UQ_draw_rosters_draw_member_no   UNIQUE (member_draw_id, member_no_snapshot);
ALTER TABLE collections           ADD CONSTRAINT UQ_collections_club_slug         UNIQUE (club_id, slug);
ALTER TABLE products              ADD CONSTRAINT UQ_products_club_slug            UNIQUE (club_id, slug);
ALTER TABLE payment_channels      ADD CONSTRAINT UQ_payment_channels_owner_type_env UNIQUE (owner_club_id, channel_type, environment);
ALTER TABLE charities             ADD CONSTRAINT UQ_charities_club_slug           UNIQUE (club_id, slug);
ALTER TABLE charity_programs      ADD CONSTRAINT UQ_charity_programs_club_slug    UNIQUE (club_id, slug);

-- clubs 自身欄位唯一鍵
ALTER TABLE clubs ADD CONSTRAINT UQ_clubs_code   UNIQUE (code);
ALTER TABLE clubs ADD CONSTRAINT UQ_clubs_domain UNIQUE (domain);

/* ============================================================================
   統一建索引（docs/12b §11.2；覆蓋索引與篩選索引的細部調校屬實作階段，不在此指定）
   ============================================================================ */

-- 逐表指定索引
CREATE INDEX IX_articles_status_published            ON articles (status, published_at DESC);
CREATE INDEX IX_articles_category_published          ON articles (article_category_id, published_at DESC);
CREATE INDEX IX_articles_club_status_published        ON articles (club_id, status, published_at DESC);
CREATE INDEX IX_matches_season_matchon                ON matches (season_id, match_on);
CREATE INDEX IX_matches_status_matchon                ON matches (status, match_on);
CREATE INDEX IX_calendar_event_teams_team_source       ON calendar_event_teams (team_id, source_type);
CREATE INDEX IX_registrations_session_status           ON registrations (session_id, status);
CREATE INDEX IX_registrations_member                   ON registrations (member_id);
CREATE INDEX IX_orders_member_created                  ON orders (member_id, created_at DESC);
CREATE INDEX IX_orders_order_status                    ON orders (order_status);
CREATE INDEX IX_orders_payment_status_created           ON orders (payment_status, created_at);
CREATE INDEX IX_orders_club_created                     ON orders (club_id, created_at DESC);
CREATE INDEX IX_order_items_order                       ON order_items (order_id);
CREATE INDEX IX_inventory_movements_variant_occurred     ON inventory_movements (product_variant_id, occurred_at DESC);
CREATE INDEX IX_memberships_club_status_end             ON memberships (club_id, status, membership_end_on);
CREATE INDEX IX_memberships_member                      ON memberships (member_id);
CREATE INDEX IX_email_logs_member_sent                  ON email_logs (member_id, sent_at DESC);
CREATE INDEX IX_email_logs_type_sent                    ON email_logs (type, sent_at);
CREATE INDEX IX_enquiries_form_status_created           ON enquiries (form_id, status, created_at DESC);
CREATE INDEX IX_admin_refresh_tokens_user               ON admin_refresh_tokens (admin_user_id);
CREATE INDEX IX_enquiries_assignee                      ON enquiries (assignee_admin_user_id);
CREATE INDEX IX_admin_user_clubs_user_active            ON admin_user_clubs (admin_user_id, is_active);

-- 帶 club_id 的內容表：(slug, club_id)，供路由解析（俱樂部專屬優先、回退共同）
CREATE INDEX IX_pages_slug_club              ON pages (slug, club_id);
CREATE INDEX IX_press_resources_slug_club    ON press_resources (slug, club_id);
CREATE INDEX IX_faqs_slug_club               ON faqs (slug, club_id);
CREATE INDEX IX_programs_slug_club           ON programs (slug, club_id);
CREATE INDEX IX_partners_slug_club           ON partners (slug, club_id);
CREATE INDEX IX_sponsors_slug_club           ON sponsors (slug, club_id);
CREATE INDEX IX_sponsor_packages_slug_club   ON sponsor_packages (slug, club_id);
CREATE INDEX IX_fan_events_slug_club         ON fan_events (slug, club_id);
CREATE INDEX IX_collections_slug_club        ON collections (slug, club_id);
CREATE INDEX IX_products_slug_club           ON products (slug, club_id);
CREATE INDEX IX_charities_slug_club          ON charities (slug, club_id);
CREATE INDEX IX_charity_programs_slug_club   ON charity_programs (slug, club_id);
CREATE INDEX IX_partner_stores_slug_club     ON partner_stores (slug, club_id);

-- 所有 *_i18n 側表：(locale)，供翻譯狀態總覽矩陣查詢
CREATE INDEX IX_settings_i18n_locale                 ON settings_i18n (locale);
CREATE INDEX IX_clubs_i18n_locale                    ON clubs_i18n (locale);
CREATE INDEX IX_competitions_i18n_locale             ON competitions_i18n (locale);
CREATE INDEX IX_email_templates_i18n_locale          ON email_templates_i18n (locale);
CREATE INDEX IX_pages_i18n_locale                    ON pages_i18n (locale);
CREATE INDEX IX_articles_i18n_locale                 ON articles_i18n (locale);
CREATE INDEX IX_article_categories_i18n_locale       ON article_categories_i18n (locale);
CREATE INDEX IX_tags_i18n_locale                     ON tags_i18n (locale);
CREATE INDEX IX_press_resources_i18n_locale          ON press_resources_i18n (locale);
CREATE INDEX IX_banners_i18n_locale                  ON banners_i18n (locale);
CREATE INDEX IX_faqs_i18n_locale                     ON faqs_i18n (locale);
CREATE INDEX IX_faq_categories_i18n_locale           ON faq_categories_i18n (locale);
CREATE INDEX IX_teams_i18n_locale                    ON teams_i18n (locale);
CREATE INDEX IX_players_i18n_locale                  ON players_i18n (locale);
CREATE INDEX IX_staff_i18n_locale                    ON staff_i18n (locale);
CREATE INDEX IX_matches_i18n_locale                  ON matches_i18n (locale);
CREATE INDEX IX_milestones_i18n_locale               ON milestones_i18n (locale);
CREATE INDEX IX_programs_i18n_locale                 ON programs_i18n (locale);
CREATE INDEX IX_partners_i18n_locale                 ON partners_i18n (locale);
CREATE INDEX IX_sponsors_i18n_locale                 ON sponsors_i18n (locale);
CREATE INDEX IX_sponsor_packages_i18n_locale         ON sponsor_packages_i18n (locale);
CREATE INDEX IX_comic_characters_i18n_locale         ON comic_characters_i18n (locale);
CREATE INDEX IX_comic_episodes_i18n_locale           ON comic_episodes_i18n (locale);
CREATE INDEX IX_fan_events_i18n_locale               ON fan_events_i18n (locale);
CREATE INDEX IX_forms_i18n_locale                    ON forms_i18n (locale);
CREATE INDEX IX_menu_items_i18n_locale               ON menu_items_i18n (locale);
CREATE INDEX IX_venues_i18n_locale                   ON venues_i18n (locale);
CREATE INDEX IX_membership_plans_i18n_locale         ON membership_plans_i18n (locale);
CREATE INDEX IX_membership_benefits_i18n_locale      ON membership_benefits_i18n (locale);
CREATE INDEX IX_partner_stores_i18n_locale           ON partner_stores_i18n (locale);
CREATE INDEX IX_member_draws_i18n_locale             ON member_draws_i18n (locale);
CREATE INDEX IX_calendar_custom_events_i18n_locale   ON calendar_custom_events_i18n (locale);
CREATE INDEX IX_event_types_i18n_locale              ON event_types_i18n (locale);
CREATE INDEX IX_collections_i18n_locale              ON collections_i18n (locale);
CREATE INDEX IX_products_i18n_locale                 ON products_i18n (locale);
CREATE INDEX IX_charities_i18n_locale                ON charities_i18n (locale);
CREATE INDEX IX_charity_programs_i18n_locale         ON charity_programs_i18n (locale);
CREATE INDEX IX_impact_records_i18n_locale           ON impact_records_i18n (locale);
CREATE INDEX IX_impact_metrics_i18n_locale           ON impact_metrics_i18n (locale);

/* ============================================================================
   統一建外鍵（先共通稽核欄位 created_by／updated_by，再逐模組業務外鍵）
   ============================================================================ */

-- created_by／updated_by 一律 → admin_users(id)，可為空，NO ACTION（未特別列出行為者維持預設）
ALTER TABLE achievements ADD CONSTRAINT FK_achievements_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE achievements ADD CONSTRAINT FK_achievements_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE admin_roles ADD CONSTRAINT FK_admin_roles_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE admin_roles ADD CONSTRAINT FK_admin_roles_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE admin_users ADD CONSTRAINT FK_admin_users_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE admin_users ADD CONSTRAINT FK_admin_users_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE article_categories ADD CONSTRAINT FK_article_categories_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE article_categories ADD CONSTRAINT FK_article_categories_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE articles ADD CONSTRAINT FK_articles_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE articles ADD CONSTRAINT FK_articles_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE banners ADD CONSTRAINT FK_banners_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE banners ADD CONSTRAINT FK_banners_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE calendar_custom_events ADD CONSTRAINT FK_calendar_custom_events_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE calendar_custom_events ADD CONSTRAINT FK_calendar_custom_events_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE carts ADD CONSTRAINT FK_carts_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE carts ADD CONSTRAINT FK_carts_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE charities ADD CONSTRAINT FK_charities_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE charities ADD CONSTRAINT FK_charities_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE charity_program_images ADD CONSTRAINT FK_charity_program_images_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE charity_program_images ADD CONSTRAINT FK_charity_program_images_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE charity_programs ADD CONSTRAINT FK_charity_programs_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE charity_programs ADD CONSTRAINT FK_charity_programs_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE clubs ADD CONSTRAINT FK_clubs_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE clubs ADD CONSTRAINT FK_clubs_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE collections ADD CONSTRAINT FK_collections_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE collections ADD CONSTRAINT FK_collections_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE comic_characters ADD CONSTRAINT FK_comic_characters_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE comic_characters ADD CONSTRAINT FK_comic_characters_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE comic_episodes ADD CONSTRAINT FK_comic_episodes_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE comic_episodes ADD CONSTRAINT FK_comic_episodes_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE comic_pages ADD CONSTRAINT FK_comic_pages_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE comic_pages ADD CONSTRAINT FK_comic_pages_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE competitions ADD CONSTRAINT FK_competitions_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE competitions ADD CONSTRAINT FK_competitions_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE draw_rosters ADD CONSTRAINT FK_draw_rosters_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE draw_rosters ADD CONSTRAINT FK_draw_rosters_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE email_logs ADD CONSTRAINT FK_email_logs_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE email_logs ADD CONSTRAINT FK_email_logs_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE email_templates ADD CONSTRAINT FK_email_templates_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE email_templates ADD CONSTRAINT FK_email_templates_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE enquiries ADD CONSTRAINT FK_enquiries_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE enquiries ADD CONSTRAINT FK_enquiries_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE event_types ADD CONSTRAINT FK_event_types_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE event_types ADD CONSTRAINT FK_event_types_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE fan_event_registrations ADD CONSTRAINT FK_fan_event_registrations_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE fan_event_registrations ADD CONSTRAINT FK_fan_event_registrations_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE fan_events ADD CONSTRAINT FK_fan_events_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE fan_events ADD CONSTRAINT FK_fan_events_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE faq_categories ADD CONSTRAINT FK_faq_categories_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE faq_categories ADD CONSTRAINT FK_faq_categories_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE faq_search_misses ADD CONSTRAINT FK_faq_search_misses_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE faq_search_misses ADD CONSTRAINT FK_faq_search_misses_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE faqs ADD CONSTRAINT FK_faqs_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE faqs ADD CONSTRAINT FK_faqs_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE form_fields ADD CONSTRAINT FK_form_fields_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE form_fields ADD CONSTRAINT FK_form_fields_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE forms ADD CONSTRAINT FK_forms_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE forms ADD CONSTRAINT FK_forms_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE home_sections ADD CONSTRAINT FK_home_sections_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE home_sections ADD CONSTRAINT FK_home_sections_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE impact_metrics ADD CONSTRAINT FK_impact_metrics_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE impact_metrics ADD CONSTRAINT FK_impact_metrics_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE impact_record_images ADD CONSTRAINT FK_impact_record_images_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE impact_record_images ADD CONSTRAINT FK_impact_record_images_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE impact_records ADD CONSTRAINT FK_impact_records_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE impact_records ADD CONSTRAINT FK_impact_records_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE inventory_movements ADD CONSTRAINT FK_inventory_movements_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE inventory_movements ADD CONSTRAINT FK_inventory_movements_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE invoice_donation_codes ADD CONSTRAINT FK_invoice_donation_codes_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE invoice_donation_codes ADD CONSTRAINT FK_invoice_donation_codes_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE jersey_issues ADD CONSTRAINT FK_jersey_issues_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE jersey_issues ADD CONSTRAINT FK_jersey_issues_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE match_cards ADD CONSTRAINT FK_match_cards_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE match_cards ADD CONSTRAINT FK_match_cards_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE match_goals ADD CONSTRAINT FK_match_goals_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE match_goals ADD CONSTRAINT FK_match_goals_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE match_lineups ADD CONSTRAINT FK_match_lineups_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE match_lineups ADD CONSTRAINT FK_match_lineups_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE matches ADD CONSTRAINT FK_matches_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE matches ADD CONSTRAINT FK_matches_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE member_cards ADD CONSTRAINT FK_member_cards_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE member_cards ADD CONSTRAINT FK_member_cards_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE member_draws ADD CONSTRAINT FK_member_draws_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE member_draws ADD CONSTRAINT FK_member_draws_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE members ADD CONSTRAINT FK_members_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE members ADD CONSTRAINT FK_members_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE membership_benefits ADD CONSTRAINT FK_membership_benefits_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE membership_benefits ADD CONSTRAINT FK_membership_benefits_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE membership_payments ADD CONSTRAINT FK_membership_payments_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE membership_payments ADD CONSTRAINT FK_membership_payments_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE membership_plans ADD CONSTRAINT FK_membership_plans_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE membership_plans ADD CONSTRAINT FK_membership_plans_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE memberships ADD CONSTRAINT FK_memberships_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE memberships ADD CONSTRAINT FK_memberships_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE menu_items ADD CONSTRAINT FK_menu_items_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE menu_items ADD CONSTRAINT FK_menu_items_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE milestones ADD CONSTRAINT FK_milestones_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE milestones ADD CONSTRAINT FK_milestones_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE newsletter_subscribers ADD CONSTRAINT FK_newsletter_subscribers_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE newsletter_subscribers ADD CONSTRAINT FK_newsletter_subscribers_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE order_items ADD CONSTRAINT FK_order_items_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE order_items ADD CONSTRAINT FK_order_items_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE orders ADD CONSTRAINT FK_orders_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE orders ADD CONSTRAINT FK_orders_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE page_blocks ADD CONSTRAINT FK_page_blocks_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE page_blocks ADD CONSTRAINT FK_page_blocks_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE page_versions ADD CONSTRAINT FK_page_versions_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE page_versions ADD CONSTRAINT FK_page_versions_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE pages ADD CONSTRAINT FK_pages_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE pages ADD CONSTRAINT FK_pages_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE partner_stores ADD CONSTRAINT FK_partner_stores_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE partner_stores ADD CONSTRAINT FK_partner_stores_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE partners ADD CONSTRAINT FK_partners_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE partners ADD CONSTRAINT FK_partners_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE payment_channels ADD CONSTRAINT FK_payment_channels_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE payment_channels ADD CONSTRAINT FK_payment_channels_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE permissions ADD CONSTRAINT FK_permissions_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE permissions ADD CONSTRAINT FK_permissions_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE player_season_stats ADD CONSTRAINT FK_player_season_stats_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE player_season_stats ADD CONSTRAINT FK_player_season_stats_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE players ADD CONSTRAINT FK_players_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE players ADD CONSTRAINT FK_players_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE press_resources ADD CONSTRAINT FK_press_resources_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE press_resources ADD CONSTRAINT FK_press_resources_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE product_images ADD CONSTRAINT FK_product_images_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE product_images ADD CONSTRAINT FK_product_images_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE product_variants ADD CONSTRAINT FK_product_variants_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE product_variants ADD CONSTRAINT FK_product_variants_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE products ADD CONSTRAINT FK_products_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE products ADD CONSTRAINT FK_products_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE programs ADD CONSTRAINT FK_programs_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE programs ADD CONSTRAINT FK_programs_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE proposal_files ADD CONSTRAINT FK_proposal_files_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE proposal_files ADD CONSTRAINT FK_proposal_files_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE proposals ADD CONSTRAINT FK_proposals_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE proposals ADD CONSTRAINT FK_proposals_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE redirects ADD CONSTRAINT FK_redirects_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE redirects ADD CONSTRAINT FK_redirects_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE refund_requests ADD CONSTRAINT FK_refund_requests_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE refund_requests ADD CONSTRAINT FK_refund_requests_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE registrations ADD CONSTRAINT FK_registrations_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE registrations ADD CONSTRAINT FK_registrations_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE seasons ADD CONSTRAINT FK_seasons_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE seasons ADD CONSTRAINT FK_seasons_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE sessions ADD CONSTRAINT FK_sessions_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE sessions ADD CONSTRAINT FK_sessions_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE settings ADD CONSTRAINT FK_settings_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE settings ADD CONSTRAINT FK_settings_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE shipments ADD CONSTRAINT FK_shipments_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE shipments ADD CONSTRAINT FK_shipments_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE sponsor_packages ADD CONSTRAINT FK_sponsor_packages_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE sponsor_packages ADD CONSTRAINT FK_sponsor_packages_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE sponsors ADD CONSTRAINT FK_sponsors_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE sponsors ADD CONSTRAINT FK_sponsors_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE staff ADD CONSTRAINT FK_staff_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE staff ADD CONSTRAINT FK_staff_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE standings ADD CONSTRAINT FK_standings_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE standings ADD CONSTRAINT FK_standings_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE store_invoices ADD CONSTRAINT FK_store_invoices_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE store_invoices ADD CONSTRAINT FK_store_invoices_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE tags ADD CONSTRAINT FK_tags_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE tags ADD CONSTRAINT FK_tags_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE teams ADD CONSTRAINT FK_teams_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE teams ADD CONSTRAINT FK_teams_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE trials ADD CONSTRAINT FK_trials_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE trials ADD CONSTRAINT FK_trials_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE ui_strings ADD CONSTRAINT FK_ui_strings_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE ui_strings ADD CONSTRAINT FK_ui_strings_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
ALTER TABLE venues ADD CONSTRAINT FK_venues_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
ALTER TABLE venues ADD CONSTRAINT FK_venues_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);

-- 4.0 共通機制
ALTER TABLE ui_string_translations ADD CONSTRAINT FK_ui_string_translations_ui_string FOREIGN KEY (ui_string_id) REFERENCES ui_strings(id) ON DELETE CASCADE;
ALTER TABLE settings              ADD CONSTRAINT FK_settings_club                FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE settings_i18n         ADD CONSTRAINT FK_settings_i18n_setting        FOREIGN KEY (setting_id) REFERENCES settings(id) ON DELETE CASCADE;
ALTER TABLE clubs_i18n            ADD CONSTRAINT FK_clubs_i18n_club              FOREIGN KEY (club_id) REFERENCES clubs(id) ON DELETE CASCADE;
ALTER TABLE clubs_i18n            ADD CONSTRAINT FK_clubs_i18n_locale            FOREIGN KEY (locale) REFERENCES locales(code);
ALTER TABLE competitions_i18n     ADD CONSTRAINT FK_competitions_i18n_comp       FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE;
ALTER TABLE competitions_i18n     ADD CONSTRAINT FK_competitions_i18n_locale     FOREIGN KEY (locale) REFERENCES locales(code);
ALTER TABLE email_templates       ADD CONSTRAINT FK_email_templates_club         FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE email_templates_i18n  ADD CONSTRAINT FK_email_templates_i18n_template FOREIGN KEY (email_template_id) REFERENCES email_templates(id) ON DELETE CASCADE;
ALTER TABLE email_logs            ADD CONSTRAINT FK_email_logs_club              FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE email_logs            ADD CONSTRAINT FK_email_logs_template          FOREIGN KEY (email_template_id) REFERENCES email_templates(id);
ALTER TABLE email_logs            ADD CONSTRAINT FK_email_logs_member            FOREIGN KEY (member_id) REFERENCES members(id);

-- 4.1 B 內容管理 ＋ H
ALTER TABLE pages                  ADD CONSTRAINT FK_pages_club                  FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE pages_i18n              ADD CONSTRAINT FK_pages_i18n_page             FOREIGN KEY (page_id) REFERENCES pages(id) ON DELETE CASCADE;
ALTER TABLE page_blocks             ADD CONSTRAINT FK_page_blocks_page            FOREIGN KEY (page_id) REFERENCES pages(id) ON DELETE CASCADE;
ALTER TABLE page_versions           ADD CONSTRAINT FK_page_versions_page          FOREIGN KEY (page_id) REFERENCES pages(id) ON DELETE CASCADE;
ALTER TABLE articles                ADD CONSTRAINT FK_articles_club               FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE articles                ADD CONSTRAINT FK_articles_category           FOREIGN KEY (article_category_id) REFERENCES article_categories(id);
ALTER TABLE articles_i18n           ADD CONSTRAINT FK_articles_i18n_article       FOREIGN KEY (article_id) REFERENCES articles(id) ON DELETE CASCADE;
ALTER TABLE article_categories_i18n ADD CONSTRAINT FK_article_categories_i18n_cat FOREIGN KEY (article_category_id) REFERENCES article_categories(id) ON DELETE CASCADE;
ALTER TABLE tags_i18n               ADD CONSTRAINT FK_tags_i18n_tag               FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE;
ALTER TABLE article_tags            ADD CONSTRAINT FK_article_tags_article        FOREIGN KEY (article_id) REFERENCES articles(id) ON DELETE CASCADE;
ALTER TABLE article_tags            ADD CONSTRAINT FK_article_tags_tag            FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE;
ALTER TABLE article_relations       ADD CONSTRAINT FK_article_relations_article   FOREIGN KEY (article_id) REFERENCES articles(id) ON DELETE CASCADE;
ALTER TABLE press_resources         ADD CONSTRAINT FK_press_resources_club        FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE press_resources_i18n    ADD CONSTRAINT FK_press_resources_i18n_pr     FOREIGN KEY (press_resource_id) REFERENCES press_resources(id) ON DELETE CASCADE;
ALTER TABLE banners                 ADD CONSTRAINT FK_banners_club                FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE banners_i18n            ADD CONSTRAINT FK_banners_i18n_banner         FOREIGN KEY (banner_id) REFERENCES banners(id) ON DELETE CASCADE;
ALTER TABLE home_sections           ADD CONSTRAINT FK_home_sections_club          FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE home_sections           ADD CONSTRAINT FK_home_sections_banner        FOREIGN KEY (featured_banner_id) REFERENCES banners(id);
ALTER TABLE faqs                    ADD CONSTRAINT FK_faqs_club                   FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE faqs_i18n               ADD CONSTRAINT FK_faqs_i18n_faq               FOREIGN KEY (faq_id) REFERENCES faqs(id) ON DELETE CASCADE;
ALTER TABLE faq_categories_i18n     ADD CONSTRAINT FK_faq_categories_i18n_cat     FOREIGN KEY (faq_category_id) REFERENCES faq_categories(id) ON DELETE CASCADE;
ALTER TABLE faq_category_links      ADD CONSTRAINT FK_faq_category_links_faq      FOREIGN KEY (faq_id) REFERENCES faqs(id) ON DELETE CASCADE;
ALTER TABLE faq_category_links      ADD CONSTRAINT FK_faq_category_links_cat      FOREIGN KEY (faq_category_id) REFERENCES faq_categories(id) ON DELETE CASCADE;
ALTER TABLE faq_search_misses       ADD CONSTRAINT FK_faq_search_misses_club      FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE redirects                ADD CONSTRAINT FK_redirects_club              FOREIGN KEY (club_id) REFERENCES clubs(id);

-- 4.2 C 球隊管理
ALTER TABLE competitions   ADD CONSTRAINT FK_competitions_club     FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE competitions   ADD CONSTRAINT FK_competitions_season   FOREIGN KEY (season_id) REFERENCES seasons(id);
ALTER TABLE seasons        ADD CONSTRAINT FK_seasons_club          FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE teams          ADD CONSTRAINT FK_teams_club            FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE teams_i18n     ADD CONSTRAINT FK_teams_i18n_team       FOREIGN KEY (team_id) REFERENCES teams(id) ON DELETE CASCADE;
ALTER TABLE players        ADD CONSTRAINT FK_players_club          FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE players        ADD CONSTRAINT FK_players_team          FOREIGN KEY (team_id) REFERENCES teams(id);
ALTER TABLE players_i18n   ADD CONSTRAINT FK_players_i18n_player   FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE;
ALTER TABLE player_season_stats ADD CONSTRAINT FK_player_season_stats_player FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE;
ALTER TABLE player_season_stats ADD CONSTRAINT FK_player_season_stats_season FOREIGN KEY (season_id) REFERENCES seasons(id);
ALTER TABLE staff           ADD CONSTRAINT FK_staff_club            FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE staff_i18n      ADD CONSTRAINT FK_staff_i18n_staff      FOREIGN KEY (staff_id) REFERENCES staff(id) ON DELETE CASCADE;
ALTER TABLE staff_teams     ADD CONSTRAINT FK_staff_teams_staff     FOREIGN KEY (staff_id) REFERENCES staff(id) ON DELETE CASCADE;
ALTER TABLE staff_teams     ADD CONSTRAINT FK_staff_teams_team      FOREIGN KEY (team_id) REFERENCES teams(id) ON DELETE CASCADE;
ALTER TABLE matches         ADD CONSTRAINT FK_matches_club          FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE matches         ADD CONSTRAINT FK_matches_season        FOREIGN KEY (season_id) REFERENCES seasons(id);
ALTER TABLE matches         ADD CONSTRAINT FK_matches_competition   FOREIGN KEY (competition_id) REFERENCES competitions(id);
ALTER TABLE matches         ADD CONSTRAINT FK_matches_venue         FOREIGN KEY (venue_id) REFERENCES venues(id);
ALTER TABLE matches_i18n    ADD CONSTRAINT FK_matches_i18n_match    FOREIGN KEY (match_id) REFERENCES matches(id) ON DELETE CASCADE;
ALTER TABLE match_teams     ADD CONSTRAINT FK_match_teams_match     FOREIGN KEY (match_id) REFERENCES matches(id) ON DELETE CASCADE;
ALTER TABLE match_teams     ADD CONSTRAINT FK_match_teams_team      FOREIGN KEY (team_id) REFERENCES teams(id) ON DELETE CASCADE;
ALTER TABLE match_goals     ADD CONSTRAINT FK_match_goals_match     FOREIGN KEY (match_id) REFERENCES matches(id) ON DELETE CASCADE;
ALTER TABLE match_goals     ADD CONSTRAINT FK_match_goals_player    FOREIGN KEY (player_id) REFERENCES players(id);
ALTER TABLE match_cards     ADD CONSTRAINT FK_match_cards_match     FOREIGN KEY (match_id) REFERENCES matches(id) ON DELETE CASCADE;
ALTER TABLE match_cards     ADD CONSTRAINT FK_match_cards_player    FOREIGN KEY (player_id) REFERENCES players(id);
ALTER TABLE match_lineups   ADD CONSTRAINT FK_match_lineups_match   FOREIGN KEY (match_id) REFERENCES matches(id) ON DELETE CASCADE;
ALTER TABLE match_lineups   ADD CONSTRAINT FK_match_lineups_player  FOREIGN KEY (player_id) REFERENCES players(id);
ALTER TABLE standings       ADD CONSTRAINT FK_standings_club        FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE standings       ADD CONSTRAINT FK_standings_season      FOREIGN KEY (season_id) REFERENCES seasons(id);
ALTER TABLE achievements    ADD CONSTRAINT FK_achievements_club     FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE achievements    ADD CONSTRAINT FK_achievements_season   FOREIGN KEY (season_id) REFERENCES seasons(id);
ALTER TABLE achievements    ADD CONSTRAINT FK_achievements_team     FOREIGN KEY (team_id) REFERENCES teams(id);
ALTER TABLE milestones      ADD CONSTRAINT FK_milestones_club       FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE milestones_i18n ADD CONSTRAINT FK_milestones_i18n_ms    FOREIGN KEY (milestone_id) REFERENCES milestones(id) ON DELETE CASCADE;

-- 4.3 P 課程與活動
ALTER TABLE programs        ADD CONSTRAINT FK_programs_club          FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE programs_i18n   ADD CONSTRAINT FK_programs_i18n_program  FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;
ALTER TABLE program_staff   ADD CONSTRAINT FK_program_staff_program  FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;
ALTER TABLE program_staff   ADD CONSTRAINT FK_program_staff_staff    FOREIGN KEY (staff_id) REFERENCES staff(id) ON DELETE CASCADE;
ALTER TABLE program_partners ADD CONSTRAINT FK_program_partners_program FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;
ALTER TABLE program_partners ADD CONSTRAINT FK_program_partners_partner FOREIGN KEY (partner_id) REFERENCES partners(id) ON DELETE CASCADE;
ALTER TABLE sessions        ADD CONSTRAINT FK_sessions_club          FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE sessions        ADD CONSTRAINT FK_sessions_program       FOREIGN KEY (program_id) REFERENCES programs(id);
ALTER TABLE sessions        ADD CONSTRAINT FK_sessions_venue         FOREIGN KEY (venue_id) REFERENCES venues(id);
ALTER TABLE registrations   ADD CONSTRAINT FK_registrations_club     FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE registrations   ADD CONSTRAINT FK_registrations_session  FOREIGN KEY (session_id) REFERENCES sessions(id);
ALTER TABLE registrations   ADD CONSTRAINT FK_registrations_trial    FOREIGN KEY (trial_id) REFERENCES trials(id);
ALTER TABLE registrations   ADD CONSTRAINT FK_registrations_member   FOREIGN KEY (member_id) REFERENCES members(id) ON DELETE SET NULL;
ALTER TABLE trials          ADD CONSTRAINT FK_trials_club            FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE trials          ADD CONSTRAINT FK_trials_team            FOREIGN KEY (team_id) REFERENCES teams(id);
ALTER TABLE trials          ADD CONSTRAINT FK_trials_venue           FOREIGN KEY (venue_id) REFERENCES venues(id);

-- 4.4 E 商業模組
ALTER TABLE partners             ADD CONSTRAINT FK_partners_club                FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE partners_i18n        ADD CONSTRAINT FK_partners_i18n_partner        FOREIGN KEY (partner_id) REFERENCES partners(id) ON DELETE CASCADE;
ALTER TABLE sponsors             ADD CONSTRAINT FK_sponsors_club                FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE sponsors_i18n        ADD CONSTRAINT FK_sponsors_i18n_sponsor        FOREIGN KEY (sponsor_id) REFERENCES sponsors(id) ON DELETE CASCADE;
ALTER TABLE sponsor_packages     ADD CONSTRAINT FK_sponsor_packages_club        FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE sponsor_packages_i18n ADD CONSTRAINT FK_sponsor_packages_i18n_pkg   FOREIGN KEY (sponsor_package_id) REFERENCES sponsor_packages(id) ON DELETE CASCADE;
ALTER TABLE sponsor_package_links ADD CONSTRAINT FK_sponsor_package_links_sponsor FOREIGN KEY (sponsor_id) REFERENCES sponsors(id) ON DELETE CASCADE;
ALTER TABLE sponsor_package_links ADD CONSTRAINT FK_sponsor_package_links_pkg     FOREIGN KEY (sponsor_package_id) REFERENCES sponsor_packages(id) ON DELETE CASCADE;
ALTER TABLE proposals             ADD CONSTRAINT FK_proposals_club               FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE proposal_files        ADD CONSTRAINT FK_proposal_files_proposal      FOREIGN KEY (proposal_id) REFERENCES proposals(id) ON DELETE CASCADE;

-- 4.5 F 文化模組
ALTER TABLE comic_characters       ADD CONSTRAINT FK_comic_characters_club       FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE comic_characters       ADD CONSTRAINT FK_comic_characters_player     FOREIGN KEY (player_id) REFERENCES players(id);
ALTER TABLE comic_characters_i18n  ADD CONSTRAINT FK_comic_characters_i18n_cc    FOREIGN KEY (comic_character_id) REFERENCES comic_characters(id) ON DELETE CASCADE;
ALTER TABLE comic_episodes         ADD CONSTRAINT FK_comic_episodes_club         FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE comic_episodes_i18n    ADD CONSTRAINT FK_comic_episodes_i18n_ep      FOREIGN KEY (comic_episode_id) REFERENCES comic_episodes(id) ON DELETE CASCADE;
ALTER TABLE comic_pages            ADD CONSTRAINT FK_comic_pages_episode         FOREIGN KEY (comic_episode_id) REFERENCES comic_episodes(id) ON DELETE CASCADE;
ALTER TABLE fan_events             ADD CONSTRAINT FK_fan_events_club             FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE fan_events_i18n        ADD CONSTRAINT FK_fan_events_i18n_event       FOREIGN KEY (fan_event_id) REFERENCES fan_events(id) ON DELETE CASCADE;
ALTER TABLE fan_event_registrations ADD CONSTRAINT FK_fan_event_registrations_club FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE fan_event_registrations ADD CONSTRAINT FK_fan_event_registrations_event FOREIGN KEY (fan_event_id) REFERENCES fan_events(id);
ALTER TABLE fan_event_registrations ADD CONSTRAINT FK_fan_event_registrations_member FOREIGN KEY (member_id) REFERENCES members(id) ON DELETE SET NULL;

-- 4.6 G 表單與詢問
ALTER TABLE forms               ADD CONSTRAINT FK_forms_club                  FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE forms_i18n          ADD CONSTRAINT FK_forms_i18n_form             FOREIGN KEY (form_id) REFERENCES forms(id) ON DELETE CASCADE;
ALTER TABLE form_fields         ADD CONSTRAINT FK_form_fields_form            FOREIGN KEY (form_id) REFERENCES forms(id) ON DELETE CASCADE;
ALTER TABLE enquiries           ADD CONSTRAINT FK_enquiries_club              FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE enquiries           ADD CONSTRAINT FK_enquiries_form              FOREIGN KEY (form_id) REFERENCES forms(id);
ALTER TABLE enquiries           ADD CONSTRAINT FK_enquiries_assignee          FOREIGN KEY (assignee_admin_user_id) REFERENCES admin_users(id);
ALTER TABLE enquiry_answers     ADD CONSTRAINT FK_enquiry_answers_enquiry     FOREIGN KEY (enquiry_id) REFERENCES enquiries(id) ON DELETE CASCADE;
ALTER TABLE enquiry_answers     ADD CONSTRAINT FK_enquiry_answers_field       FOREIGN KEY (form_field_id) REFERENCES form_fields(id);
ALTER TABLE newsletter_subscribers ADD CONSTRAINT FK_newsletter_subscribers_club FOREIGN KEY (club_id) REFERENCES clubs(id);

-- 4.7 I 網站設定
ALTER TABLE menu_items          ADD CONSTRAINT FK_menu_items_club             FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE menu_items          ADD CONSTRAINT FK_menu_items_parent           FOREIGN KEY (parent_id) REFERENCES menu_items(id);
ALTER TABLE menu_items_i18n     ADD CONSTRAINT FK_menu_items_i18n_item        FOREIGN KEY (menu_item_id) REFERENCES menu_items(id) ON DELETE CASCADE;
ALTER TABLE venues_i18n         ADD CONSTRAINT FK_venues_i18n_venue           FOREIGN KEY (venue_id) REFERENCES venues(id) ON DELETE CASCADE;

-- 4.8 J 系統管理
ALTER TABLE admin_users        ADD CONSTRAINT FK_admin_users_primary_club     FOREIGN KEY (primary_club_id) REFERENCES clubs(id);
ALTER TABLE admin_user_roles   ADD CONSTRAINT FK_admin_user_roles_user        FOREIGN KEY (admin_user_id) REFERENCES admin_users(id) ON DELETE CASCADE;
ALTER TABLE admin_user_roles   ADD CONSTRAINT FK_admin_user_roles_role        FOREIGN KEY (admin_role_id) REFERENCES admin_roles(id) ON DELETE CASCADE;
ALTER TABLE admin_user_clubs   ADD CONSTRAINT FK_admin_user_clubs_user        FOREIGN KEY (admin_user_id) REFERENCES admin_users(id) ON DELETE CASCADE;
ALTER TABLE admin_user_clubs   ADD CONSTRAINT FK_admin_user_clubs_club        FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE admin_user_clubs   ADD CONSTRAINT FK_admin_user_clubs_granted_by  FOREIGN KEY (granted_by) REFERENCES admin_users(id);
ALTER TABLE admin_user_teams   ADD CONSTRAINT FK_admin_user_teams_user        FOREIGN KEY (admin_user_id) REFERENCES admin_users(id) ON DELETE CASCADE;
ALTER TABLE admin_user_teams   ADD CONSTRAINT FK_admin_user_teams_team        FOREIGN KEY (team_id) REFERENCES teams(id);
ALTER TABLE role_permissions   ADD CONSTRAINT FK_role_permissions_role        FOREIGN KEY (admin_role_id) REFERENCES admin_roles(id) ON DELETE CASCADE;
ALTER TABLE role_permissions   ADD CONSTRAINT FK_role_permissions_permission  FOREIGN KEY (permission_id) REFERENCES permissions(id) ON DELETE CASCADE;
ALTER TABLE admin_refresh_tokens ADD CONSTRAINT FK_admin_refresh_tokens_user      FOREIGN KEY (admin_user_id) REFERENCES admin_users(id) ON DELETE CASCADE;
ALTER TABLE admin_refresh_tokens ADD CONSTRAINT FK_admin_refresh_tokens_replaced FOREIGN KEY (replaced_by_id) REFERENCES admin_refresh_tokens(id);

-- 4.9 K 會員管理
ALTER TABLE memberships             ADD CONSTRAINT FK_memberships_member          FOREIGN KEY (member_id) REFERENCES members(id);
ALTER TABLE memberships             ADD CONSTRAINT FK_memberships_club            FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE memberships             ADD CONSTRAINT FK_memberships_season          FOREIGN KEY (season_id) REFERENCES seasons(id);
ALTER TABLE member_cards            ADD CONSTRAINT FK_member_cards_membership     FOREIGN KEY (membership_id) REFERENCES memberships(id) ON DELETE CASCADE;
ALTER TABLE member_cards            ADD CONSTRAINT FK_member_cards_club           FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE membership_plans        ADD CONSTRAINT FK_membership_plans_club       FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE membership_plans        ADD CONSTRAINT FK_membership_plans_season     FOREIGN KEY (season_id) REFERENCES seasons(id);
ALTER TABLE membership_plans_i18n   ADD CONSTRAINT FK_membership_plans_i18n_plan  FOREIGN KEY (membership_plan_id) REFERENCES membership_plans(id) ON DELETE CASCADE;
ALTER TABLE membership_payments     ADD CONSTRAINT FK_membership_payments_membership FOREIGN KEY (membership_id) REFERENCES memberships(id);
ALTER TABLE membership_payments     ADD CONSTRAINT FK_membership_payments_club       FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE membership_payments     ADD CONSTRAINT FK_membership_payments_collecting_club FOREIGN KEY (collecting_club_id) REFERENCES clubs(id);
ALTER TABLE membership_payments     ADD CONSTRAINT FK_membership_payments_plan        FOREIGN KEY (membership_plan_id) REFERENCES membership_plans(id);
ALTER TABLE membership_payments     ADD CONSTRAINT FK_membership_payments_handled_by  FOREIGN KEY (handled_by) REFERENCES admin_users(id);
ALTER TABLE membership_benefits     ADD CONSTRAINT FK_membership_benefits_plan        FOREIGN KEY (membership_plan_id) REFERENCES membership_plans(id) ON DELETE CASCADE;
ALTER TABLE membership_benefits_i18n ADD CONSTRAINT FK_membership_benefits_i18n_benefit FOREIGN KEY (membership_benefit_id) REFERENCES membership_benefits(id) ON DELETE CASCADE;
ALTER TABLE jersey_issues           ADD CONSTRAINT FK_jersey_issues_club              FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE jersey_issues           ADD CONSTRAINT FK_jersey_issues_member            FOREIGN KEY (member_id) REFERENCES members(id);
ALTER TABLE partner_stores          ADD CONSTRAINT FK_partner_stores_club             FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE partner_stores_i18n     ADD CONSTRAINT FK_partner_stores_i18n_store       FOREIGN KEY (partner_store_id) REFERENCES partner_stores(id) ON DELETE CASCADE;
ALTER TABLE member_draws            ADD CONSTRAINT FK_member_draws_club               FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE member_draws            ADD CONSTRAINT FK_member_draws_article            FOREIGN KEY (announcement_article_id) REFERENCES articles(id);
ALTER TABLE member_draws            ADD CONSTRAINT FK_member_draws_locked_by          FOREIGN KEY (locked_by) REFERENCES admin_users(id);
ALTER TABLE member_draws_i18n       ADD CONSTRAINT FK_member_draws_i18n_draw          FOREIGN KEY (member_draw_id) REFERENCES member_draws(id) ON DELETE CASCADE;
ALTER TABLE draw_rosters            ADD CONSTRAINT FK_draw_rosters_club               FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE draw_rosters            ADD CONSTRAINT FK_draw_rosters_draw               FOREIGN KEY (member_draw_id) REFERENCES member_draws(id);

-- 4.10 L 行事曆管理
ALTER TABLE calendar_custom_events       ADD CONSTRAINT FK_calendar_custom_events_club       FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE calendar_custom_events       ADD CONSTRAINT FK_calendar_custom_events_event_type FOREIGN KEY (event_type_id) REFERENCES event_types(id);
ALTER TABLE calendar_custom_events       ADD CONSTRAINT FK_calendar_custom_events_venue      FOREIGN KEY (venue_id) REFERENCES venues(id);
ALTER TABLE calendar_custom_events_i18n  ADD CONSTRAINT FK_calendar_custom_events_i18n_event FOREIGN KEY (calendar_custom_event_id) REFERENCES calendar_custom_events(id) ON DELETE CASCADE;
ALTER TABLE calendar_event_teams         ADD CONSTRAINT FK_calendar_event_teams_team         FOREIGN KEY (team_id) REFERENCES teams(id);
ALTER TABLE calendar_event_exceptions    ADD CONSTRAINT FK_calendar_event_exceptions_event   FOREIGN KEY (calendar_custom_event_id) REFERENCES calendar_custom_events(id) ON DELETE CASCADE;
ALTER TABLE event_types_i18n             ADD CONSTRAINT FK_event_types_i18n_type             FOREIGN KEY (event_type_id) REFERENCES event_types(id) ON DELETE CASCADE;

-- 4.11 S 商店
ALTER TABLE collections         ADD CONSTRAINT FK_collections_club              FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE collections_i18n    ADD CONSTRAINT FK_collections_i18n_collection   FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE;
ALTER TABLE products            ADD CONSTRAINT FK_products_club                 FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE products            ADD CONSTRAINT FK_products_collection           FOREIGN KEY (collection_id) REFERENCES collections(id);
ALTER TABLE products_i18n       ADD CONSTRAINT FK_products_i18n_product         FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE;
ALTER TABLE product_images      ADD CONSTRAINT FK_product_images_product        FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE;
ALTER TABLE product_variants    ADD CONSTRAINT FK_product_variants_club         FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE product_variants    ADD CONSTRAINT FK_product_variants_product      FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE inventory_movements ADD CONSTRAINT FK_inventory_movements_club      FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE inventory_movements ADD CONSTRAINT FK_inventory_movements_variant   FOREIGN KEY (product_variant_id) REFERENCES product_variants(id);
ALTER TABLE inventory_movements ADD CONSTRAINT FK_inventory_movements_order     FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE carts               ADD CONSTRAINT FK_carts_club                    FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE carts               ADD CONSTRAINT FK_carts_member                  FOREIGN KEY (member_id) REFERENCES members(id);
ALTER TABLE cart_items          ADD CONSTRAINT FK_cart_items_cart               FOREIGN KEY (cart_id) REFERENCES carts(id) ON DELETE CASCADE;
ALTER TABLE cart_items          ADD CONSTRAINT FK_cart_items_variant            FOREIGN KEY (product_variant_id) REFERENCES product_variants(id);
ALTER TABLE orders              ADD CONSTRAINT FK_orders_club                   FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE orders              ADD CONSTRAINT FK_orders_selling_club           FOREIGN KEY (selling_club_id) REFERENCES clubs(id);
ALTER TABLE orders              ADD CONSTRAINT FK_orders_collecting_club        FOREIGN KEY (collecting_club_id) REFERENCES clubs(id);
ALTER TABLE orders              ADD CONSTRAINT FK_orders_member                 FOREIGN KEY (member_id) REFERENCES members(id) ON DELETE SET NULL;
ALTER TABLE order_items         ADD CONSTRAINT FK_order_items_club              FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE order_items         ADD CONSTRAINT FK_order_items_order             FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE order_items         ADD CONSTRAINT FK_order_items_variant           FOREIGN KEY (product_variant_id) REFERENCES product_variants(id);
ALTER TABLE shipments           ADD CONSTRAINT FK_shipments_club                FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE shipments           ADD CONSTRAINT FK_shipments_order               FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE refund_requests     ADD CONSTRAINT FK_refund_requests_club          FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE refund_requests     ADD CONSTRAINT FK_refund_requests_order         FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE refund_requests     ADD CONSTRAINT FK_refund_requests_approved_by   FOREIGN KEY (approved_by) REFERENCES admin_users(id);
ALTER TABLE refund_request_items ADD CONSTRAINT FK_refund_request_items_request FOREIGN KEY (refund_request_id) REFERENCES refund_requests(id) ON DELETE CASCADE;
ALTER TABLE refund_request_items ADD CONSTRAINT FK_refund_request_items_item    FOREIGN KEY (order_item_id) REFERENCES order_items(id);
ALTER TABLE store_invoices      ADD CONSTRAINT FK_store_invoices_club           FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE store_invoices      ADD CONSTRAINT FK_store_invoices_order          FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE store_invoices      ADD CONSTRAINT FK_store_invoices_channel        FOREIGN KEY (payment_channel_id) REFERENCES payment_channels(id);
ALTER TABLE payment_channels    ADD CONSTRAINT FK_payment_channels_owner_club   FOREIGN KEY (owner_club_id) REFERENCES clubs(id);

-- 4.12 B6 慈善內容（主站）
ALTER TABLE charities               ADD CONSTRAINT FK_charities_club                  FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE charities_i18n          ADD CONSTRAINT FK_charities_i18n_charity          FOREIGN KEY (charity_id) REFERENCES charities(id) ON DELETE CASCADE;
ALTER TABLE charity_programs        ADD CONSTRAINT FK_charity_programs_club           FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE charity_programs        ADD CONSTRAINT FK_charity_programs_charity        FOREIGN KEY (charity_id) REFERENCES charities(id);
ALTER TABLE charity_programs_i18n   ADD CONSTRAINT FK_charity_programs_i18n_program   FOREIGN KEY (charity_program_id) REFERENCES charity_programs(id) ON DELETE CASCADE;
ALTER TABLE charity_program_images  ADD CONSTRAINT FK_charity_program_images_program  FOREIGN KEY (charity_program_id) REFERENCES charity_programs(id) ON DELETE CASCADE;
ALTER TABLE impact_records          ADD CONSTRAINT FK_impact_records_club             FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE impact_records          ADD CONSTRAINT FK_impact_records_program          FOREIGN KEY (charity_program_id) REFERENCES charity_programs(id);
ALTER TABLE impact_records          ADD CONSTRAINT FK_impact_records_charity          FOREIGN KEY (charity_id) REFERENCES charities(id);
ALTER TABLE impact_records_i18n     ADD CONSTRAINT FK_impact_records_i18n_record      FOREIGN KEY (impact_record_id) REFERENCES impact_records(id) ON DELETE CASCADE;
ALTER TABLE impact_record_images    ADD CONSTRAINT FK_impact_record_images_record     FOREIGN KEY (impact_record_id) REFERENCES impact_records(id) ON DELETE CASCADE;
ALTER TABLE impact_metrics          ADD CONSTRAINT FK_impact_metrics_club             FOREIGN KEY (club_id) REFERENCES clubs(id);
ALTER TABLE impact_metrics          ADD CONSTRAINT FK_impact_metrics_program          FOREIGN KEY (charity_program_id) REFERENCES charity_programs(id);
ALTER TABLE impact_metrics_i18n     ADD CONSTRAINT FK_impact_metrics_i18n_metric      FOREIGN KEY (impact_metric_id) REFERENCES impact_metrics(id) ON DELETE CASCADE;

/* ============================================================================
   CalendarEvent 視圖（一般 VIEW，UNION ALL；禁止 indexed view／WITH SCHEMABINDING）
   ============================================================================ */
GO

CREATE VIEW calendar_events AS
-- 來源：C4 賽事。event_type_id 無對應欄位，維持 NULL；starts_at 取 match_on（未併入 kickoff 時刻，
-- 屬視圖第一期簡化，未在 docs 中逐一定義精確合併規則）。
SELECT
  'match'              AS source_type,
  m.id                 AS source_id,
  CAST(NULL AS uniqueidentifier) AS event_type_id,
  CAST(m.match_on AS datetime2(3)) AS starts_at,
  CAST(0 AS bit)        AS is_all_day,
  m.venue_id            AS venue_id,
  m.club_id             AS club_id
FROM matches m

UNION ALL

-- 來源：P 試訓。L3 開關 sync_to_calendar = 1 才納入，預設關閉。
SELECT
  'trial'               AS source_type,
  t.id                  AS source_id,
  CAST(NULL AS uniqueidentifier) AS event_type_id,
  CAST(t.trial_on AS datetime2(3)) AS starts_at,
  CAST(0 AS bit)         AS is_all_day,
  t.venue_id             AS venue_id,
  t.club_id              AS club_id
FROM trials t
WHERE t.sync_to_calendar = 1

UNION ALL

-- 來源：L2 自建事件。行事曆唯一的自有資料。
SELECT
  'custom'               AS source_type,
  c.id                   AS source_id,
  c.event_type_id        AS event_type_id,
  c.starts_at            AS starts_at,
  c.is_all_day           AS is_all_day,
  c.venue_id             AS venue_id,
  c.club_id              AS club_id
FROM calendar_custom_events c;
GO
