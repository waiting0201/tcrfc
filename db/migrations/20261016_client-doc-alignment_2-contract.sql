/* ============================================================================
   客戶文件對齊（收縮，contract）：資料遷移與約束收緊
   2026-10-16｜依據同 20261016_client-doc-alignment_1-expand.sql。
   🔴 前提：expand 已套用，且新版 api（讀寫新欄位與新值）已部署並驗證。舊版 api 仍會寫 fan_club／registered 與舊核心價值 code，先跑會被收緊後的 CHECK 擋掉。
   做的事（全部冪等，目標形狀已達成就跳過）：
     ① 核心價值標籤：players_first→inspiring_players、excellence→shaping_champions、community→local_pride、global_pathways→global_influence；
        integrity 無對應，連結列刪除（內容可無標籤）；CHECK 收緊為新四值。
     ② 會籍：刪除 tier = 'registered' 的會籍與其會員卡（註冊帳號不是會籍）——若帶球衣登記／付款／訂單紀錄則 THROW 中止；
        fan_club→方案層級（既有方案一律補 tier = 'standard'）；membership_plan_id 無值者以同俱樂部同球季的方案補、補不到就 THROW；
        CHECK 收緊為 standard／premium；membership_plan_id 與 membership_plans.tier 改 NOT NULL。
     ③ 權益對照條目：補 benefit_code、分組 store_discount→discount、jersey→gift；value 取代 free_value／paid_value（舊欄位刪除）；
        benefit_code 改 NOT NULL 並加 (membership_plan_id, benefit_code) 唯一；分組 CHECK 收緊。
     ④ partner_stores.applicable_tier：fan_club→premium（保守對應，待客戶確認，主站 §10 第 42 點）。
     ⑤ push_messages／app_announcements.audience_tier：fan_club→member、registered→anonymous。
   （draw_rosters.tier_snapshot 不在收縮裡：凍結名單的名單雜湊含層級字串，改寫歷史快照會讓雜湊對不上，所以 expand 就放寬為
     registered／fan_club／standard／premium 並存，這就是最終值域；舊快照維持原值。）
   🔴 刪除無法找回（PITR 只有 7 天，docs/20 §5）；刪除的列數會 PRINT 出來供核對。
   與 apps/api/Data/Migrations 的 ClientDocAlignmentContract 遷移同源。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261016_client-doc-alignment_2-contract.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 0) 前提檢查：展開型遷移必須已套用

IF COL_LENGTH(N'membership_plans', N'tier') IS NULL OR COL_LENGTH(N'membership_benefits', N'benefit_code') IS NULL OR COL_LENGTH(N'membership_benefits_i18n', N'value') IS NULL
    THROW 50100, N'請先套用 20261016_client-doc-alignment_1-expand（缺 membership_plans.tier／membership_benefits.benefit_code／membership_benefits_i18n.value），中止', 1;

-- 1) 核心價值標籤：舊五→新四（integrity 無對應，連結列刪除）；內容可無標籤

DECLARE @vt TABLE (old_tag nvarchar(32) NOT NULL, new_tag nvarchar(32) NOT NULL);
INSERT INTO @vt VALUES (N'players_first', N'inspiring_players'), (N'excellence', N'shaping_champions'), (N'community', N'local_pride'), (N'global_pathways', N'global_influence');
DELETE o FROM value_tag_links o JOIN @vt m ON m.old_tag = o.value_tag
    WHERE EXISTS (SELECT 1 FROM value_tag_links n WHERE n.entity_type = o.entity_type AND n.entity_id = o.entity_id AND n.value_tag = m.new_tag);
UPDATE o SET o.value_tag = m.new_tag FROM value_tag_links o JOIN @vt m ON m.old_tag = o.value_tag;
DELETE FROM value_tag_links WHERE value_tag = N'integrity';

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_value_tag_links_value_tag' AND parent_object_id = OBJECT_ID(N'value_tag_links') AND definition LIKE N'%players_first%')
BEGIN
    EXEC(N'ALTER TABLE value_tag_links DROP CONSTRAINT CK_value_tag_links_value_tag;');
    EXEC(N'ALTER TABLE value_tag_links ADD CONSTRAINT CK_value_tag_links_value_tag CHECK (value_tag IN (''inspiring_players'',''shaping_champions'',''local_pride'',''global_influence''));');
END;

-- 2) 會籍：註冊帳號不是會籍（刪 registered 列與其會員卡）；fan_club→standard（依方案層級）；方案必填

IF EXISTS (SELECT 1 FROM memberships WHERE tier = N'registered')
BEGIN
    IF EXISTS (SELECT 1 FROM memberships m WHERE m.tier = N'registered'
               AND (EXISTS (SELECT 1 FROM jersey_issues j WHERE j.membership_id = m.id)
                 OR EXISTS (SELECT 1 FROM membership_payments p WHERE p.membership_id = m.id)
                 OR EXISTS (SELECT 1 FROM membership_orders o WHERE o.membership_id = m.id)))
        THROW 50101, N'有 tier = registered 的會籍帶有球衣登記、付款或訂單紀錄，不能直接刪除，請人工處理後再跑，中止', 1;
    DELETE c FROM member_cards c JOIN memberships m ON m.id = c.membership_id WHERE m.tier = N'registered';
    PRINT CONCAT(N'member_cards（registered 會籍的卡）刪除 ', @@ROWCOUNT, N' 列');
    DELETE FROM memberships WHERE tier = N'registered';
    PRINT CONCAT(N'memberships（tier = registered）刪除 ', @@ROWCOUNT, N' 列');
END;
EXEC(N'UPDATE membership_plans SET tier = N''standard'' WHERE tier IS NULL;');
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'membership_plans') AND name = N'tier' AND is_nullable = 1)
    EXEC(N'ALTER TABLE membership_plans ALTER COLUMN tier nvarchar(16) NOT NULL;');
EXEC(N'UPDATE m SET m.membership_plan_id = x.id FROM memberships m
       CROSS APPLY (SELECT TOP 1 p.id FROM membership_plans p WHERE p.club_id = m.club_id AND p.season_id = m.season_id ORDER BY p.sort_order, p.row_seq) x
       WHERE m.membership_plan_id IS NULL;');
IF EXISTS (SELECT 1 FROM memberships WHERE membership_plan_id IS NULL)
    THROW 50102, N'有會籍沒有方案，且同俱樂部同球季找不到可補的方案，請人工指定 membership_plan_id 後再跑，中止', 1;
EXEC(N'UPDATE m SET m.tier = p.tier FROM memberships m JOIN membership_plans p ON p.id = m.membership_plan_id WHERE m.tier NOT IN (N''standard'', N''premium'');');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_memberships_tier' AND parent_object_id = OBJECT_ID(N'memberships') AND definition LIKE N'%registered%')
BEGIN
    EXEC(N'ALTER TABLE memberships DROP CONSTRAINT CK_memberships_tier;');
    EXEC(N'ALTER TABLE memberships ADD CONSTRAINT CK_memberships_tier CHECK (tier IN (''standard'',''premium''));');
END;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'memberships') AND name = N'membership_plan_id' AND is_nullable = 1)
    EXEC(N'ALTER TABLE memberships ALTER COLUMN membership_plan_id uniqueidentifier NOT NULL;');

-- 3) 權益對照條目：補代碼、分組改名（store_discount→discount、jersey→gift）、value 取代 free_value／paid_value

EXEC(N'UPDATE membership_benefits SET benefit_code = CONCAT(N''legacy_'', row_seq) WHERE benefit_code IS NULL;');
EXEC(N'UPDATE membership_benefits SET benefit_group = CASE benefit_group WHEN N''store_discount'' THEN N''discount'' WHEN N''jersey'' THEN N''gift'' ELSE benefit_group END WHERE benefit_group IN (N''store_discount'', N''jersey'');');
IF COL_LENGTH(N'membership_benefits_i18n', N'paid_value') IS NOT NULL
BEGIN
    EXEC(N'UPDATE membership_benefits_i18n SET value = paid_value WHERE value IS NULL AND paid_value IS NOT NULL;');
    EXEC(N'ALTER TABLE membership_benefits_i18n DROP COLUMN paid_value;');
END;
IF COL_LENGTH(N'membership_benefits_i18n', N'free_value') IS NOT NULL EXEC(N'ALTER TABLE membership_benefits_i18n DROP COLUMN free_value;');
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'membership_benefits') AND name = N'benefit_code' AND is_nullable = 1)
    EXEC(N'ALTER TABLE membership_benefits ALTER COLUMN benefit_code nvarchar(64) NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_membership_benefits_plan_code' AND object_id = OBJECT_ID(N'membership_benefits'))
    EXEC(N'ALTER TABLE membership_benefits ADD CONSTRAINT UQ_membership_benefits_plan_code UNIQUE (membership_plan_id, benefit_code);');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_membership_benefits_benefit_group' AND parent_object_id = OBJECT_ID(N'membership_benefits') AND definition LIKE N'%store_discount%')
BEGIN
    EXEC(N'ALTER TABLE membership_benefits DROP CONSTRAINT CK_membership_benefits_benefit_group;');
    EXEC(N'ALTER TABLE membership_benefits ADD CONSTRAINT CK_membership_benefits_benefit_group CHECK (benefit_group IN (''member_card'',''discount'',''gift'',''event'',''content''));');
END;

-- 4) 特約店家適用層級：舊「限付費」fan_club→premium（保守對應，待客戶確認，主站 §10 第 42 點）

UPDATE partner_stores SET applicable_tier = N'premium' WHERE applicable_tier = N'fan_club';

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_partner_stores_applicable_tier' AND parent_object_id = OBJECT_ID(N'partner_stores') AND definition LIKE N'%fan_club%')
BEGIN
    EXEC(N'ALTER TABLE partner_stores DROP CONSTRAINT CK_partner_stores_applicable_tier;');
    EXEC(N'ALTER TABLE partner_stores ADD CONSTRAINT CK_partner_stores_applicable_tier CHECK (applicable_tier IN (''all'',''premium''));');
END;

-- 5) 推播與公告分眾：fan_club（付費會員）→member（持有效會籍者）；registered（無付費會籍的帳號）→anonymous（無有效會籍者，含未登入裝置）

UPDATE push_messages SET audience_tier = N'member' WHERE audience_tier = N'fan_club';
UPDATE push_messages SET audience_tier = N'anonymous' WHERE audience_tier = N'registered';
UPDATE app_announcements SET audience_tier = N'member' WHERE audience_tier = N'fan_club';
UPDATE app_announcements SET audience_tier = N'anonymous' WHERE audience_tier = N'registered';

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_push_messages_audience_tier' AND parent_object_id = OBJECT_ID(N'push_messages') AND definition LIKE N'%fan_club%')
BEGIN
    EXEC(N'ALTER TABLE push_messages DROP CONSTRAINT CK_push_messages_audience_tier;');
    EXEC(N'ALTER TABLE push_messages ADD CONSTRAINT CK_push_messages_audience_tier CHECK (audience_tier IN (''all'',''member'',''premium'',''anonymous''));');
END;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_app_announcements_audience_tier' AND parent_object_id = OBJECT_ID(N'app_announcements') AND definition LIKE N'%fan_club%')
BEGIN
    EXEC(N'ALTER TABLE app_announcements DROP CONSTRAINT CK_app_announcements_audience_tier;');
    EXEC(N'ALTER TABLE app_announcements ADD CONSTRAINT CK_app_announcements_audience_tier CHECK (audience_tier IN (''all'',''member'',''premium'',''anonymous''));');
END;

COMMIT TRANSACTION;
