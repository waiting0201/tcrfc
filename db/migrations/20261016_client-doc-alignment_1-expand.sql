/* ============================================================================
   客戶文件對齊（展開，expand）：核心價值四項、球迷會兩級方案、商店會員折扣
   2026-10-16｜依據：使用者 2026-10-16 裁決「照客戶文件」；主站規劃書 v3.29（§1.2、§3.8 8.2／8.3／8.4、§3.14、§4.11 K1–K5、§4.13 S1／S3、§5.1）、
   藍鯨規劃書 v1.12、App 規劃書 v3.22。
   本檔只做『新增與放寬』，不刪資料、不收緊：
     ① value_tag_links.value_tag、memberships.tier、partner_stores.applicable_tier、
        push_messages／app_announcements.audience_tier、membership_benefits.benefit_group 的 CHECK 放寬為「舊值＋新值」並存；
        draw_rosters.tier_snapshot 放寬為 registered／fan_club／standard／premium 並存（凍結名單不改寫，這即最終值域）；
     ② membership_plans 新增 tier（收縮前可為空）、shop_discount_percent（預設 0）；
     ③ membership_benefits 新增 benefit_code（既有列補 legacy_<row_seq>）；membership_benefits_i18n 新增 value（既有 paid_value 複製過來）；
     ④ products.member_discount_eligible、orders.member_discount_amount／member_discount_plan_id（值複製，不建外鍵）、order_items.member_discount_amount。
   🔴 先 migrate（本檔）再 deploy（E-289）：舊版 api 在放寬後的結構上照常運作（舊欄位與舊值都還在）；新版 api 才會讀寫新欄位與新值。
   🔴 新版 api 部署並驗證後，再跑 20261016_client-doc-alignment_2-contract.sql（資料遷移與收緊）。兩支之間，新版 api 對未遷移的舊資料採寬容讀取。
   🔴 冪等：欄位／約束已是目標形狀就跳過；每個 CHECK 以具名約束處理（DDL 與 EF 同名）。
   與 apps/api/Data/Migrations 的 ClientDocAlignmentExpand 遷移同源。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261016_client-doc-alignment_1-expand.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1) 核心價值標籤：先放寬為『舊五＋新四』並存（資料遷移在收縮）

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_value_tag_links_value_tag' AND parent_object_id = OBJECT_ID(N'value_tag_links') AND definition LIKE N'%inspiring_players%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_value_tag_links_value_tag' AND parent_object_id = OBJECT_ID(N'value_tag_links'))
        EXEC(N'ALTER TABLE value_tag_links DROP CONSTRAINT CK_value_tag_links_value_tag;');
    EXEC(N'ALTER TABLE value_tag_links ADD CONSTRAINT CK_value_tag_links_value_tag CHECK (value_tag IN (''players_first'',''excellence'',''global_pathways'',''community'',''integrity'',''inspiring_players'',''shaping_champions'',''local_pride'',''global_influence''));');
END;

-- 2) 會籍層級：放寬為『舊二＋新二』並存

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_memberships_tier' AND parent_object_id = OBJECT_ID(N'memberships') AND definition LIKE N'%''standard''%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_memberships_tier' AND parent_object_id = OBJECT_ID(N'memberships'))
        EXEC(N'ALTER TABLE memberships DROP CONSTRAINT CK_memberships_tier;');
    EXEC(N'ALTER TABLE memberships ADD CONSTRAINT CK_memberships_tier CHECK (tier IN (''registered'',''fan_club'',''standard'',''premium''));');
END;

-- 3) 會籍方案：層級（收縮前可為空）與商店會員折扣比例

IF COL_LENGTH(N'membership_plans', N'tier') IS NULL EXEC(N'ALTER TABLE membership_plans ADD tier nvarchar(16) NULL;');
IF COL_LENGTH(N'membership_plans', N'shop_discount_percent') IS NULL EXEC(N'ALTER TABLE membership_plans ADD shop_discount_percent int NOT NULL CONSTRAINT DF_membership_plans_shop_discount_percent DEFAULT 0;');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_membership_plans_tier' AND parent_object_id = OBJECT_ID(N'membership_plans'))
    EXEC(N'ALTER TABLE membership_plans ADD CONSTRAINT CK_membership_plans_tier CHECK (tier IN (''standard'',''premium''));');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_membership_plans_shop_discount_percent' AND parent_object_id = OBJECT_ID(N'membership_plans'))
    EXEC(N'ALTER TABLE membership_plans ADD CONSTRAINT CK_membership_plans_shop_discount_percent CHECK (shop_discount_percent BETWEEN 0 AND 100);');

-- 4) 權益對照條目：條目代碼（收縮前可為空；既有列補 legacy_<row_seq>）、分組值域放寬為新舊並存

IF COL_LENGTH(N'membership_benefits', N'benefit_code') IS NULL EXEC(N'ALTER TABLE membership_benefits ADD benefit_code nvarchar(64) NULL;');
EXEC(N'UPDATE membership_benefits SET benefit_code = CONCAT(N''legacy_'', row_seq) WHERE benefit_code IS NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_membership_benefits_benefit_group' AND parent_object_id = OBJECT_ID(N'membership_benefits') AND definition LIKE N'%''gift''%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_membership_benefits_benefit_group' AND parent_object_id = OBJECT_ID(N'membership_benefits'))
        EXEC(N'ALTER TABLE membership_benefits DROP CONSTRAINT CK_membership_benefits_benefit_group;');
    EXEC(N'ALTER TABLE membership_benefits ADD CONSTRAINT CK_membership_benefits_benefit_group CHECK (benefit_group IN (''member_card'',''store_discount'',''jersey'',''event'',''discount'',''gift'',''content''));');
END;

-- 5) 權益對應值：value 取代 free_value／paid_value（舊欄位在收縮才刪；既有 paid_value 複製到 value）

IF COL_LENGTH(N'membership_benefits_i18n', N'value') IS NULL EXEC(N'ALTER TABLE membership_benefits_i18n ADD value nvarchar(255) NULL;');
IF COL_LENGTH(N'membership_benefits_i18n', N'paid_value') IS NOT NULL
    EXEC(N'UPDATE membership_benefits_i18n SET value = paid_value WHERE value IS NULL AND paid_value IS NOT NULL;');

-- 6) 特約店家適用層級、抽獎名單層級快照、推播與公告分眾：放寬為新舊並存

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_partner_stores_applicable_tier' AND parent_object_id = OBJECT_ID(N'partner_stores') AND definition LIKE N'%''premium''%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_partner_stores_applicable_tier' AND parent_object_id = OBJECT_ID(N'partner_stores'))
        EXEC(N'ALTER TABLE partner_stores DROP CONSTRAINT CK_partner_stores_applicable_tier;');
    EXEC(N'ALTER TABLE partner_stores ADD CONSTRAINT CK_partner_stores_applicable_tier CHECK (applicable_tier IN (''all'',''fan_club'',''premium''));');
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_draw_rosters_tier_snapshot' AND parent_object_id = OBJECT_ID(N'draw_rosters') AND definition LIKE N'%''standard''%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_draw_rosters_tier_snapshot' AND parent_object_id = OBJECT_ID(N'draw_rosters'))
        EXEC(N'ALTER TABLE draw_rosters DROP CONSTRAINT CK_draw_rosters_tier_snapshot;');
    EXEC(N'ALTER TABLE draw_rosters ADD CONSTRAINT CK_draw_rosters_tier_snapshot CHECK (tier_snapshot IN (''registered'',''fan_club'',''standard'',''premium''));');
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_push_messages_audience_tier' AND parent_object_id = OBJECT_ID(N'push_messages') AND definition LIKE N'%''premium''%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_push_messages_audience_tier' AND parent_object_id = OBJECT_ID(N'push_messages'))
        EXEC(N'ALTER TABLE push_messages DROP CONSTRAINT CK_push_messages_audience_tier;');
    EXEC(N'ALTER TABLE push_messages ADD CONSTRAINT CK_push_messages_audience_tier CHECK (audience_tier IN (''all'',''fan_club'',''registered'',''anonymous'',''member'',''premium''));');
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_app_announcements_audience_tier' AND parent_object_id = OBJECT_ID(N'app_announcements') AND definition LIKE N'%''premium''%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_app_announcements_audience_tier' AND parent_object_id = OBJECT_ID(N'app_announcements'))
        EXEC(N'ALTER TABLE app_announcements DROP CONSTRAINT CK_app_announcements_audience_tier;');
    EXEC(N'ALTER TABLE app_announcements ADD CONSTRAINT CK_app_announcements_audience_tier CHECK (audience_tier IN (''all'',''fan_club'',''registered'',''anonymous'',''member'',''premium''));');
END;

-- 7) 商店會員折扣：商品是否適用、訂單與訂單品項的折扣快照（新欄位都在表尾）

IF COL_LENGTH(N'products', N'member_discount_eligible') IS NULL EXEC(N'ALTER TABLE products ADD member_discount_eligible bit NOT NULL CONSTRAINT DF_products_member_discount_eligible DEFAULT 0;');
IF COL_LENGTH(N'orders', N'member_discount_amount') IS NULL EXEC(N'ALTER TABLE orders ADD member_discount_amount int NOT NULL CONSTRAINT DF_orders_member_discount_amount DEFAULT 0;');
IF COL_LENGTH(N'orders', N'member_discount_plan_id') IS NULL EXEC(N'ALTER TABLE orders ADD member_discount_plan_id uniqueidentifier NULL;');
IF COL_LENGTH(N'order_items', N'member_discount_amount') IS NULL EXEC(N'ALTER TABLE order_items ADD member_discount_amount int NOT NULL CONSTRAINT DF_order_items_member_discount_amount DEFAULT 0;');

COMMIT TRANSACTION;
