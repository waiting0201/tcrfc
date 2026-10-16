using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 客戶文件對齊（展開，expand；主站規劃書 v3.29）：核心價值四項、球迷會兩級方案、商店會員折扣的新增欄位，以及相關 CHECK 放寬為「舊值＋新值」並存。
    /// 不刪資料、不收緊；🔴 必須先 migrate 再 deploy（E-289）。SQL 與 <c>db/migrations/20261016_client-doc-alignment_1-expand.sql</c> 同源（冪等，docs/20 §5 冪等契約）。
    /// <c>Down</c> 把新欄位與新值拿掉（含新值資料轉回舊值），還原成舊庫形狀。
    /// </summary>
    public partial class ClientDocAlignmentExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'order_items', N'member_discount_amount') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE order_items DROP CONSTRAINT DF_order_items_member_discount_amount;');
    EXEC(N'ALTER TABLE order_items DROP COLUMN member_discount_amount;');
END;
IF COL_LENGTH(N'orders', N'member_discount_plan_id') IS NOT NULL EXEC(N'ALTER TABLE orders DROP COLUMN member_discount_plan_id;');
IF COL_LENGTH(N'orders', N'member_discount_amount') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE orders DROP CONSTRAINT DF_orders_member_discount_amount;');
    EXEC(N'ALTER TABLE orders DROP COLUMN member_discount_amount;');
END;
IF COL_LENGTH(N'products', N'member_discount_eligible') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE products DROP CONSTRAINT DF_products_member_discount_eligible;');
    EXEC(N'ALTER TABLE products DROP COLUMN member_discount_eligible;');
END;

EXEC(N'UPDATE app_announcements SET audience_tier = N''all'' WHERE audience_tier NOT IN (N''all'',N''fan_club'',N''registered'',N''anonymous'');');
EXEC(N'UPDATE push_messages SET audience_tier = N''all'' WHERE audience_tier NOT IN (N''all'',N''fan_club'',N''registered'',N''anonymous'');');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_app_announcements_audience_tier' AND parent_object_id = OBJECT_ID(N'app_announcements'))
    EXEC(N'ALTER TABLE app_announcements DROP CONSTRAINT CK_app_announcements_audience_tier;');
EXEC(N'ALTER TABLE app_announcements ADD CONSTRAINT CK_app_announcements_audience_tier CHECK (audience_tier IN (''all'',''fan_club'',''registered'',''anonymous''));');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_push_messages_audience_tier' AND parent_object_id = OBJECT_ID(N'push_messages'))
    EXEC(N'ALTER TABLE push_messages DROP CONSTRAINT CK_push_messages_audience_tier;');
EXEC(N'ALTER TABLE push_messages ADD CONSTRAINT CK_push_messages_audience_tier CHECK (audience_tier IN (''all'',''fan_club'',''registered'',''anonymous''));');

EXEC(N'UPDATE draw_rosters SET tier_snapshot = N''fan_club'' WHERE tier_snapshot IN (N''standard'',N''premium'');');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_draw_rosters_tier_snapshot' AND parent_object_id = OBJECT_ID(N'draw_rosters'))
    EXEC(N'ALTER TABLE draw_rosters DROP CONSTRAINT CK_draw_rosters_tier_snapshot;');
EXEC(N'ALTER TABLE draw_rosters ADD CONSTRAINT CK_draw_rosters_tier_snapshot CHECK (tier_snapshot IN (''registered'',''fan_club''));');

EXEC(N'UPDATE partner_stores SET applicable_tier = N''fan_club'' WHERE applicable_tier = N''premium'';');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_partner_stores_applicable_tier' AND parent_object_id = OBJECT_ID(N'partner_stores'))
    EXEC(N'ALTER TABLE partner_stores DROP CONSTRAINT CK_partner_stores_applicable_tier;');
EXEC(N'ALTER TABLE partner_stores ADD CONSTRAINT CK_partner_stores_applicable_tier CHECK (applicable_tier IN (''all'',''fan_club''));');

IF COL_LENGTH(N'membership_benefits_i18n', N'value') IS NOT NULL EXEC(N'ALTER TABLE membership_benefits_i18n DROP COLUMN value;');
EXEC(N'UPDATE membership_benefits SET benefit_group = CASE benefit_group WHEN N''discount'' THEN N''store_discount'' WHEN N''gift'' THEN N''jersey'' WHEN N''content'' THEN N''event'' ELSE benefit_group END WHERE benefit_group IN (N''discount'',N''gift'',N''content'');');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_membership_benefits_benefit_group' AND parent_object_id = OBJECT_ID(N'membership_benefits'))
    EXEC(N'ALTER TABLE membership_benefits DROP CONSTRAINT CK_membership_benefits_benefit_group;');
EXEC(N'ALTER TABLE membership_benefits ADD CONSTRAINT CK_membership_benefits_benefit_group CHECK (benefit_group IN (''member_card'',''store_discount'',''jersey'',''event''));');

IF COL_LENGTH(N'membership_benefits', N'benefit_code') IS NOT NULL EXEC(N'ALTER TABLE membership_benefits DROP COLUMN benefit_code;');
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_membership_plans_shop_discount_percent' AND parent_object_id = OBJECT_ID(N'membership_plans'))
    EXEC(N'ALTER TABLE membership_plans DROP CONSTRAINT CK_membership_plans_shop_discount_percent;');
IF COL_LENGTH(N'membership_plans', N'shop_discount_percent') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE membership_plans DROP CONSTRAINT DF_membership_plans_shop_discount_percent;');
    EXEC(N'ALTER TABLE membership_plans DROP COLUMN shop_discount_percent;');
END;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_membership_plans_tier' AND parent_object_id = OBJECT_ID(N'membership_plans'))
    EXEC(N'ALTER TABLE membership_plans DROP CONSTRAINT CK_membership_plans_tier;');
IF COL_LENGTH(N'membership_plans', N'tier') IS NOT NULL EXEC(N'ALTER TABLE membership_plans DROP COLUMN tier;');

EXEC(N'UPDATE memberships SET tier = N''fan_club'' WHERE tier IN (N''standard'',N''premium'');');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_memberships_tier' AND parent_object_id = OBJECT_ID(N'memberships'))
    EXEC(N'ALTER TABLE memberships DROP CONSTRAINT CK_memberships_tier;');
EXEC(N'ALTER TABLE memberships ADD CONSTRAINT CK_memberships_tier CHECK (tier IN (''registered'',''fan_club''));');

EXEC(N'DELETE FROM value_tag_links WHERE value_tag IN (N''inspiring_players'',N''shaping_champions'',N''local_pride'',N''global_influence'');');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_value_tag_links_value_tag' AND parent_object_id = OBJECT_ID(N'value_tag_links'))
    EXEC(N'ALTER TABLE value_tag_links DROP CONSTRAINT CK_value_tag_links_value_tag;');
EXEC(N'ALTER TABLE value_tag_links ADD CONSTRAINT CK_value_tag_links_value_tag CHECK (value_tag IN (''players_first'',''excellence'',''global_pathways'',''community'',''integrity''));');
");
        }
    }
}
