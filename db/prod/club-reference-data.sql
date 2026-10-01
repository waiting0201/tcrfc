-- ============================================================================
-- TCRFC 主站庫（tcrfc_club）正式庫首次初始化：參照資料
-- 自動產生：python3 db/seed/generate-prod-reference-sql.py（請勿手動編輯；改定義請改原產生器後重新產生）
-- 來源產生器：db/seed/generate-club-seed-sql.py
-- 🔴 這是「參照資料」不是種子資料：只含系統運作必需的列，沒有任何測試內容與測試帳號。
-- 冪等：每個實體以業務自然鍵判斷（IF NOT EXISTS 才 INSERT），但 deploy/prod-db-init.sh 仍只在空庫上執行。
-- 區段（允許清單）：
--   0     locales：所有 *_i18n 側表的外鍵目標
--   1     clubs（台中磐石／台中藍鯨）：多俱樂部路由與一切 club_id 的根；網域改用 sqlcmd 變數
--   5     article_categories：規劃書 7.1–7.8 固定八類，新聞的外鍵目標
--   18.1  admin_roles：規劃書 §6 的十個角色
--   18.2  permissions：權限碼目錄（J 與各模組）
--   18.3  role_permissions：角色與權限碼的對應（規劃書 §6 矩陣）
--   19    faq_categories：規劃書 3.12 十個固定主題
--   20    home_sections：規劃書 3.1 首頁九大固定區塊（兩俱樂部各一份）
--   21    faq_embed_slots：G-12 四個固定掛載點
--   22    forms／form_fields：規劃書 §3.10 九個固定表單與預設欄位（兩俱樂部各一份）
--   23    event_types：L2 自建事件的起始分類字典（後台 L3 可再編輯）
-- 預期列數（deploy/prod-db-init.sh 灌完後逐表核對；`-- MANIFEST` 行是機器讀的）：
-- MANIFEST admin_roles=10
-- MANIFEST article_categories=8
-- MANIFEST article_categories_i18n=16
-- MANIFEST clubs=2
-- MANIFEST clubs_i18n=3
-- MANIFEST event_types=6
-- MANIFEST event_types_i18n=12
-- MANIFEST faq_categories=10
-- MANIFEST faq_categories_i18n=20
-- MANIFEST faq_embed_slots=4
-- MANIFEST form_fields=114
-- MANIFEST form_fields_i18n=228
-- MANIFEST forms=18
-- MANIFEST home_sections=18
-- MANIFEST locales=2
-- MANIFEST permissions=260
-- MANIFEST role_permissions=782
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- ── 0. locales：zh-Hant（預設）＋ en，供所有 *_i18n 側表的 FK 參照 ──────────
IF NOT EXISTS (SELECT 1 FROM locales WHERE code = N'zh-Hant')
  INSERT INTO locales (code, name, is_default, sort_order) VALUES (N'zh-Hant', N'繁體中文', 1, 0);
IF NOT EXISTS (SELECT 1 FROM locales WHERE code = N'en')
  INSERT INTO locales (code, name, is_default, sort_order) VALUES (N'en', N'English', 0, 1);
GO

-- ── 1. clubs：兩俱樂部的主檔 ─────────────────────────────────────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM clubs WHERE code = N'tcrfc';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f7cb2444-e607-57fc-aea5-6aa11239d4ea';
  INSERT INTO clubs (id, code, domain, brand_color, brand_secondary_color, is_collecting_subject, default_locale, sort_order, status)
  VALUES (@id, N'tcrfc', N'$(CLUB_DOMAIN_TCRFC)', N'#E0218A', N'#231916', 1, N'zh-Hant', 0, N'active');
  INSERT INTO clubs_i18n (club_id, locale, name) VALUES (@id, N'zh-Hant', N'台中磐石');
  INSERT INTO clubs_i18n (club_id, locale, name) VALUES (@id, N'en', N'Taichung Rock FC');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM clubs WHERE code = N'bw';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7bd7fca4-7989-5fb3-b14b-7d09de5b406c';
  INSERT INTO clubs (id, code, domain, brand_color, brand_secondary_color, is_collecting_subject, default_locale, sort_order, status)
  VALUES (@id, N'bw', N'$(CLUB_DOMAIN_BW)', N'#2196D5', N'#040000', 0, N'zh-Hant', 1, N'active');
  INSERT INTO clubs_i18n (club_id, locale, name) VALUES (@id, N'zh-Hant', N'台中藍鯨');
  COMMIT TRANSACTION;
END
GO

-- ── 5. article_categories：規劃書 7.1–7.8 八個分類（不帶 club_id，全站共用主檔） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = N'club';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'920a21c7-82eb-529d-b23a-f1e04852757b';
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, N'club', 0);
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', N'俱樂部新聞');
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', N'Club News');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = N'match';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c7d5538b-f9f7-589a-8714-c5c511473fc0';
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, N'match', 1);
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', N'比賽報導');
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', N'Match Reports');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = N'academy';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'484af158-12a2-5f3c-81e2-65fea5443cfb';
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, N'academy', 2);
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', N'學院新聞');
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', N'Academy News');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = N'player-stories';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1b166b53-2a7c-565d-8b21-45b9efe899ce';
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, N'player-stories', 3);
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', N'球員故事');
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', N'Player Stories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = N'international';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'887e5b9a-f6b5-55db-ad52-5b5abfa1f142';
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, N'international', 4);
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', N'國際動態');
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', N'International');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = N'camps-events';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1ad01ce5-9cc1-555e-9697-5f33fc67aea3';
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, N'camps-events', 5);
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', N'營隊與活動');
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', N'Camps & Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = N'community';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f6ed4186-8f70-52e8-b446-d2eb48f3b4e0';
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, N'community', 6);
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', N'社區活動');
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', N'Community');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = N'media';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd1676997-1b35-50bc-b1b5-230069309b7b';
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, N'media', 7);
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', N'媒體專區');
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', N'Media');
  COMMIT TRANSACTION;
END
GO

-- ── 18.1 admin_roles：十個角色（九個沿用慈善庫代碼＋合作球隊管理） ──────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'system_admin';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'573c7920-a70c-5970-b63f-645f9b5c3979';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'system_admin', N'系統管理員', N'System Administrator', N'all_clubs', 1, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'content_editor';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3e27c11e-c84c-52d7-b34a-fe937d5d80c4';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'content_editor', N'內容編輯', N'Content Editor', N'all_clubs', 1, 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'team_competition';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4c743ab2-09f0-52c2-9598-9c091f4cbdb0';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'team_competition', N'競技／球隊管理', N'Team & Competition Manager', N'all_clubs', 1, 2);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'academy_program';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'49da5494-2ef0-51fd-99fe-2641e8802f4d';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'academy_program', N'學院／課程管理', N'Academy & Program Manager', N'all_clubs', 1, 3);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'business_sponsorship';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f9576b35-8e54-5dd6-b53a-d7e5acf3f27c';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'business_sponsorship', N'商務／贊助', N'Business & Sponsorship', N'all_clubs', 1, 4);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'pr_media';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'16ba3e9e-bc2b-511e-983b-da337f9d2276';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'pr_media', N'公關／媒體', N'PR & Media', N'all_clubs', 1, 5);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'customer_service_admin';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2b91b9b1-6031-5914-a280-9415c8825182';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'customer_service_admin', N'客服／行政', N'Customer Service & Admin', N'all_clubs', 1, 6);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'translator';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ef480e90-e0c9-5120-92f0-182533d0799e';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'translator', N'翻譯人員', N'Translator', N'all_clubs', 1, 7);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'viewer';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a22ef84e-c50c-58c6-b9f0-bed5eb52aae9';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'viewer', N'檢視者', N'Viewer', N'all_clubs', 1, 8);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'partner_club_manager';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'09385b5f-aeee-5f01-bbde-2d603384571c';
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, N'partner_club_manager', N'合作球隊管理', N'Partner Club Manager', N'own_clubs', 1, 9);
  COMMIT TRANSACTION;
END
GO

-- ── 18.2 permissions：J 系統管理 ＋ B2 新聞（本次唯一接真實授權的既有模組） ─────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.article.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'55ff257d-6467-5d64-993d-7c760d4db8d9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.article.view', N'B', N'B2', N'content', N'view', 1, 0, 0, N'檢視新聞與故事', N'View News & Stories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.article.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'314c4b88-03e3-5032-b519-e16337025ba8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.article.create', N'B', N'B2', N'content', N'create', 1, 0, 0, N'建立新聞與故事', N'Create News & Stories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.article.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4b083b79-924a-5a85-b5d5-478f953d0977';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.article.update', N'B', N'B2', N'content', N'update', 1, 0, 0, N'編輯新聞與故事', N'Edit News & Stories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.article.publish';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5fa8b0bb-1978-5238-9b05-31992905b79d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.article.publish', N'B', N'B2', N'content', N'publish', 1, 0, 0, N'發布新聞與故事', N'Publish News & Stories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.article.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'915fcd0e-2047-51d9-8c79-031187cffdd4';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.article.delete', N'B', N'B2', N'content', N'delete', 1, 0, 0, N'刪除新聞與故事', N'Delete News & Stories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.page.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3eaea3d4-139b-5bf8-9908-1481aea7cf4c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.page.view', N'B', N'B1', N'content', N'view', 1, 0, 0, N'檢視頁面', N'View Pages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.page.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'244ef636-4232-5696-8403-2811508bde74';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.page.create', N'B', N'B1', N'content', N'create', 1, 0, 0, N'建立頁面', N'Create Pages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.page.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2d2914d9-3a23-5fc9-bef4-88cc54ebb03e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.page.update', N'B', N'B1', N'content', N'update', 1, 0, 0, N'編輯頁面', N'Edit Pages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.page.publish';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'eed15f72-0e54-5c67-926e-9c049d8a7305';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.page.publish', N'B', N'B1', N'content', N'publish', 1, 0, 0, N'發布頁面', N'Publish Pages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.page.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'18171863-dcb7-5d14-9baa-fcfdce019194';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.page.delete', N'B', N'B1', N'content', N'delete', 1, 0, 0, N'刪除頁面', N'Delete Pages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.account.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e6d53917-719b-50fb-b2e1-e280ca943862';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.account.view', N'J', N'J1', N'system', N'view', 0, 0, 1, N'檢視後台帳號', N'View Admin Accounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.account.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'64997430-d702-5639-ab05-4ddf9fe2fbd5';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.account.create', N'J', N'J1', N'system', N'create', 0, 0, 1, N'新增後台帳號', N'Create Admin Accounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.account.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fbc2b352-94d2-553c-9792-7cafdd1a88b4';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.account.update', N'J', N'J1', N'system', N'update', 0, 0, 1, N'停用／更新後台帳號', N'Update Admin Accounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.role.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bec25d39-a172-5628-9f86-c259674fd1cf';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.role.view', N'J', N'J2', N'system', N'view', 0, 0, 1, N'檢視角色與權限', N'View Roles & Permissions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.role.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b2aa8828-2005-59aa-9d4f-2de062371066';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.role.update', N'J', N'J2', N'system', N'update', 0, 0, 1, N'建立角色與勾選權限', N'Update Roles & Permissions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.audit.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'69c7deb6-81ca-5edd-b9cf-c16ac483166a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.audit.view', N'J', N'J3', N'system', N'view', 0, 0, 1, N'檢視操作稽核記錄', N'View Audit Logs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.club_grant.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'100bd6a4-551c-514b-b9d8-dbc13b9715b7';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.club_grant.view', N'J', N'J4', N'system', N'view', 0, 0, 1, N'檢視俱樂部授權', N'View Club Grants');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.club_grant.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2b3b757a-6fe5-5d8b-a40a-cbc78706be85';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.club_grant.update', N'J', N'J4', N'system', N'update', 0, 0, 1, N'指派俱樂部授權', N'Update Club Grants');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.club.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3148c07c-a9a4-5ef0-a3c3-a76d8cfe2a1f';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.club.view', N'J', N'J4', N'system', N'view', 0, 0, 1, N'檢視俱樂部主檔', N'View Clubs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.club.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'610b0e7d-15aa-53da-a8c2-9412c4710163';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.club.update', N'J', N'J4', N'system', N'update', 0, 0, 1, N'建立／編輯俱樂部主檔', N'Update Clubs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.team_grant.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'29c7bce9-49a9-59c8-a4a5-01a435cb6c40';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.team_grant.view', N'J', N'J4', N'system', N'view', 0, 0, 1, N'檢視球隊授權', N'View Team Grants');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'system.team_grant.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4a742a2b-c28a-5bd4-b7c2-90d5e422260f';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'system.team_grant.update', N'J', N'J4', N'system', N'update', 0, 0, 1, N'指派球隊授權', N'Update Team Grants');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.competition.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fc4a5395-9ad3-5edb-b64b-24c24fb49e44';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.competition.view', N'C', N'C4', N'team', N'view', 1, 0, 0, N'檢視賽事系列', N'View Competitions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.competition.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cd7a5a6c-835b-5d76-a363-07b24874e15b';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.competition.create', N'C', N'C4', N'team', N'create', 1, 0, 0, N'建立賽事系列', N'Create Competitions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.competition.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'75b1b8a0-2a67-51c0-afb4-660aaa7fc70c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.competition.update', N'C', N'C4', N'team', N'update', 1, 0, 0, N'編輯賽事系列', N'Update Competitions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.team.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3dd5d498-98fb-5820-a97b-42da21129001';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.team.view', N'C', N'C1', N'team', N'view', 1, 0, 0, N'檢視球隊', N'View Teams');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.team.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4c651165-65a0-5f1e-898d-6bc43fe6c066';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.team.create', N'C', N'C1', N'team', N'create', 1, 0, 0, N'建立球隊', N'Create Teams');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.team.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9113120a-e03d-52ff-b68f-285d8947a4f0';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.team.update', N'C', N'C1', N'team', N'update', 1, 0, 0, N'編輯球隊', N'Update Teams');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.player.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f6b65ac7-6929-5192-b477-111b7efa986b';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.player.view', N'C', N'C2', N'team', N'view', 1, 0, 0, N'檢視球員', N'View Players');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.player.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5a6a769c-caf9-5e21-b541-ef571a8b3eb5';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.player.create', N'C', N'C2', N'team', N'create', 1, 0, 0, N'建立球員', N'Create Players');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.player.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'52d2a9a2-9888-588d-a69b-8bec0542438f';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.player.update', N'C', N'C2', N'team', N'update', 1, 0, 0, N'編輯球員', N'Update Players');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.staff.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e70ef0d7-1aa6-5fe9-b15b-0986f4b60daf';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.staff.view', N'C', N'C3', N'team', N'view', 1, 0, 0, N'檢視教練與團隊成員', N'View Staff');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.staff.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3eb1d405-503b-5509-a65d-0f73e3bc196f';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.staff.create', N'C', N'C3', N'team', N'create', 1, 0, 0, N'建立教練與團隊成員', N'Create Staff');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.staff.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0376ed32-ec09-5536-995b-0f4deafc0987';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.staff.update', N'C', N'C3', N'team', N'update', 1, 0, 0, N'編輯教練與團隊成員', N'Update Staff');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.banner.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd443aefa-6576-5295-b9ea-6737fa7149ab';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.banner.view', N'B', N'B3', N'content', N'view', 1, 0, 0, N'檢視首頁輪播', N'View Home Banners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.banner.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1f623686-79a2-5b8d-b34d-9bdbabfd4a21';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.banner.create', N'B', N'B3', N'content', N'create', 1, 0, 0, N'新增首頁輪播', N'Create Home Banners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.banner.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ac458b15-4f57-59fd-9cd8-c560377b79b0';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.banner.update', N'B', N'B3', N'content', N'update', 1, 0, 0, N'編輯首頁輪播', N'Update Home Banners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.banner.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fdb6bda0-9ac1-5e9a-ba4c-3bf5b7b47487';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.banner.delete', N'B', N'B3', N'content', N'delete', 1, 0, 0, N'刪除首頁輪播', N'Delete Home Banners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.home_section.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'004736d3-2c4e-5142-ba81-4a6c9a2a59f8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.home_section.view', N'B', N'B3', N'content', N'view', 1, 0, 0, N'檢視首頁區塊編排', N'View Home Sections');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.home_section.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'be887717-30b4-55f0-8b40-60263358551b';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.home_section.update', N'B', N'B3', N'content', N'update', 1, 0, 0, N'調整首頁區塊開關與排序', N'Update Home Sections');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.faq.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e6e49705-ed83-52b9-8011-ca08ee07a579';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.faq.view', N'B', N'B4', N'content', N'view', 1, 0, 0, N'檢視常見問題', N'View FAQs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.faq.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3988fd76-eaa7-5739-84ae-b9570b0641b8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.faq.create', N'B', N'B4', N'content', N'create', 1, 0, 0, N'新增常見問題', N'Create FAQs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.faq.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'aed19ece-2396-520f-8dc7-b8ab6de55c42';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.faq.update', N'B', N'B4', N'content', N'update', 1, 0, 0, N'編輯常見問題', N'Update FAQs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.faq.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'77554a8a-b787-5f91-98b7-9d775319f829';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.faq.delete', N'B', N'B4', N'content', N'delete', 1, 0, 0, N'刪除常見問題', N'Delete FAQs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.faq_category.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'23d73e6d-0a97-5d33-9bc1-cc9fb2735dad';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.faq_category.view', N'B', N'B4', N'content', N'view', 0, 0, 0, N'檢視常見問題分類', N'View FAQ Categories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.faq_category.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'eb6cc860-2403-544f-bbff-a2d0ceff64c2';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.faq_category.create', N'B', N'B4', N'content', N'create', 0, 0, 0, N'新增常見問題分類', N'Create FAQ Categories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.faq_category.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c88d8321-eed6-51fa-acb2-6a06481d31cf';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.faq_category.update', N'B', N'B4', N'content', N'update', 0, 0, 0, N'編輯常見問題分類', N'Update FAQ Categories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.faq_category.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'82ce6aa6-e1ab-5e35-a305-76ef38c8f6b3';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.faq_category.delete', N'B', N'B4', N'content', N'delete', 0, 0, 0, N'刪除常見問題分類', N'Delete FAQ Categories');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.match.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7624b3a7-ddce-51a5-8557-2b2548d8a3a7';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.match.view', N'C', N'C4', N'team', N'view', 1, 0, 0, N'檢視賽程與賽果', N'View Matches');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.match.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6854bbf5-91df-5983-ad09-440424b9f95a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.match.create', N'C', N'C4', N'team', N'create', 1, 0, 0, N'建立賽程與賽果', N'Create Matches');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.match.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cf71279a-75ae-52df-8b56-dcb739b3a638';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.match.update', N'C', N'C4', N'team', N'update', 1, 0, 0, N'編輯賽程與賽果', N'Update Matches');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.match.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'65808dfc-84b8-5fc5-b3dd-bb8449ba43fe';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.match.delete', N'C', N'C4', N'team', N'delete', 1, 0, 0, N'刪除賽程與賽果', N'Delete Matches');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.standing.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'615c4539-ea86-510c-b26a-f8749da142e8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.standing.view', N'C', N'C4', N'team', N'view', 1, 0, 0, N'檢視積分榜', N'View Standings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.standing.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd3c10b79-2cab-513c-8653-2066771f4b24';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.standing.create', N'C', N'C4', N'team', N'create', 1, 0, 0, N'建立積分榜', N'Create Standings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.standing.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b0068df1-b87a-51a2-b8a6-5d082c946f82';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.standing.update', N'C', N'C4', N'team', N'update', 1, 0, 0, N'編輯積分榜', N'Update Standings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.standing.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'005976b0-af6d-587d-8022-35f986112616';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.standing.delete', N'C', N'C4', N'team', N'delete', 1, 0, 0, N'刪除積分榜', N'Delete Standings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.item.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f3c3c92e-7d01-5f4d-a44c-6c8b6cf7c917';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.item.view', N'P', N'P1', N'program', N'view', 1, 0, 0, N'檢視課程／營隊項目', N'View Program Items');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.item.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9054c7d0-6106-50fa-8901-1ae65dfde03d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.item.create', N'P', N'P1', N'program', N'create', 1, 0, 0, N'建立課程／營隊項目', N'Create Program Items');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.item.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4aa817f3-4490-5ce7-a8ac-fa5d02644adb';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.item.update', N'P', N'P1', N'program', N'update', 1, 0, 0, N'編輯課程／營隊項目', N'Update Program Items');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.session.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0f2e34e3-70f6-579b-aef1-c0ef0680b889';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.session.view', N'P', N'P2', N'program', N'view', 1, 0, 0, N'檢視梯次與場次', N'View Program Sessions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.session.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cbe8f4f3-afa1-50ef-b3e6-713020c349f7';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.session.create', N'P', N'P2', N'program', N'create', 1, 0, 0, N'建立梯次與場次', N'Create Program Sessions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.session.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'30c1e0b8-95c2-5a1d-86bb-3693ec8f42fb';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.session.update', N'P', N'P2', N'program', N'update', 1, 0, 0, N'編輯梯次與場次', N'Update Program Sessions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.registration.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'488fccc5-2a42-595e-a5ac-0579dcbf6169';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.registration.view', N'P', N'P3', N'program', N'view', 1, 0, 0, N'檢視報名', N'View Registrations');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.registration.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'16664546-5bc0-5fd3-86d9-d1ae037915d3';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.registration.create', N'P', N'P3', N'program', N'create', 1, 0, 0, N'建立報名（後台代填）', N'Create Registrations');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.registration.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a0c30e50-4d9c-5de0-8cdc-68b94d4602ae';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.registration.update', N'P', N'P3', N'program', N'update', 1, 0, 0, N'處理報名（確認／取消／轉梯次／候補／備註）', N'Update Registrations');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.registration.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4781ab00-f26b-5e88-b817-c6b40cf6c7b2';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.registration.export', N'P', N'P3', N'program', N'export', 1, 1, 0, N'匯出報名名單', N'Export Registrations');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'form.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'42deeecf-7b8f-51a5-ad37-3efd24ea2c42';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'form.view', N'G', N'G1', N'enquiry', N'view', 1, 0, 0, N'檢視表單設計', N'View Forms');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'form.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f0b1f9dd-4005-5f3b-9f07-6ff184de2e8a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'form.update', N'G', N'G1', N'enquiry', N'update', 1, 0, 0, N'編輯表單設計', N'Update Forms');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.inbox.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'92db477d-6b40-5ce7-a7c7-5f58bb177475';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.inbox.view', N'G', N'G2', N'enquiry', N'view', 1, 0, 0, N'檢視全部詢問', N'View All Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.inbox.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fedf35de-0050-5ba1-9ce4-e2da90df29ad';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.inbox.update', N'G', N'G2', N'enquiry', N'update', 1, 0, 0, N'處理全部詢問', N'Update All Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.inbox.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'836c3003-bc7a-5952-b4ac-20d3be556f3c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.inbox.export', N'G', N'G2', N'enquiry', N'export', 1, 1, 0, N'匯出詢問名單', N'Export Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.course.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4ffeb7f3-584f-5aaa-a89b-fbd23d493d49';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.course.view', N'G', N'G2', N'enquiry', N'view', 1, 0, 0, N'檢視課程類詢問', N'View Course Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.course.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7c6ac673-7bd3-584d-b1b5-23f4222af51f';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.course.update', N'G', N'G2', N'enquiry', N'update', 1, 0, 0, N'處理課程類詢問', N'Update Course Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.partnership.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e466598e-77df-5245-b171-73e0ce650bc8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.partnership.view', N'G', N'G2', N'enquiry', N'view', 1, 0, 0, N'檢視合作／贊助類詢問', N'View Partnership Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.partnership.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3d75c829-5471-5185-aebb-fbcea05cdc7a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.partnership.update', N'G', N'G2', N'enquiry', N'update', 1, 0, 0, N'處理合作／贊助類詢問', N'Update Partnership Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.media.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8953aa25-e557-536a-a2c5-e22b498b723a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.media.view', N'G', N'G2', N'enquiry', N'view', 1, 0, 0, N'檢視媒體類詢問', N'View Media Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'enquiry.media.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2c674ea6-b760-5179-962b-288597060736';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'enquiry.media.update', N'G', N'G2', N'enquiry', N'update', 1, 0, 0, N'處理媒體類詢問', N'Update Media Enquiries');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'dcf57081-7940-5eb5-ae18-5c28908684f5';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.view', N'L', N'L1', N'calendar', N'view', 1, 0, 0, N'檢視行事曆總覽', N'View Calendar Overview');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.custom_event.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'51a099ff-a187-5d58-a1d1-95e90574d438';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.custom_event.view', N'L', N'L2', N'calendar', N'view', 1, 0, 0, N'檢視自建事件', N'View Custom Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.custom_event.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'159a9d18-08d7-59e2-95a2-1e8663936358';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.custom_event.create', N'L', N'L2', N'calendar', N'create', 1, 0, 0, N'建立自建事件', N'Create Custom Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.custom_event.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'59017a9a-11e3-55f8-a7a1-f4086a5cfc02';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.custom_event.update', N'L', N'L2', N'calendar', N'update', 1, 0, 0, N'編輯自建事件', N'Update Custom Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.custom_event.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1d2944a6-3e0b-5b16-bac9-0a479de723a6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.custom_event.delete', N'L', N'L2', N'calendar', N'delete', 1, 0, 0, N'刪除自建事件', N'Delete Custom Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.setting.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a9335f67-7d5f-5f57-833f-6d2dbd8c0c18';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.setting.view', N'H', N'H1', N'seo', N'view', 1, 0, 1, N'檢視全站 SEO 設定', N'View SEO Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.setting.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'469fc4f7-fb2d-55cd-bdd3-cdc5c36cdd60';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.setting.update', N'H', N'H1', N'seo', N'update', 1, 0, 1, N'編輯全站 SEO 設定', N'Update SEO Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.redirect.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'95926377-21dc-5f6d-918c-dd544104dfd0';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.redirect.view', N'H', N'H2', N'seo', N'view', 1, 0, 1, N'檢視 301 轉址', N'View Redirects');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.redirect.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f1f86576-2497-56ee-b332-88b896ff49f1';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.redirect.create', N'H', N'H2', N'seo', N'create', 1, 0, 1, N'新增 301 轉址', N'Create Redirects');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.redirect.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'74560844-f746-5530-b36b-ba728340c595';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.redirect.update', N'H', N'H2', N'seo', N'update', 1, 0, 1, N'編輯 301 轉址', N'Update Redirects');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.redirect.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7f2beb75-61b6-5353-80a3-c988d68509e4';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.redirect.delete', N'H', N'H2', N'seo', N'delete', 1, 0, 1, N'刪除 301 轉址', N'Delete Redirects');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.redirect.import';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a8971f84-d2cc-52d1-95ae-5cfbccd27988';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.redirect.import', N'H', N'H2', N'seo', N'import', 1, 0, 1, N'批次匯入 301 轉址', N'Import Redirects');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.report.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'eace6e02-5c9f-5d1e-8397-704942771393';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.report.view', N'H', N'H3', N'seo', N'view', 1, 0, 1, N'檢視孤立頁面偵測', N'View Orphan Page Report');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.llms.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b8f305d4-0f4e-55a5-98af-6e161dca7d56';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.llms.view', N'H', N'H4', N'seo', N'view', 1, 0, 1, N'檢視 llms.txt 內容維護', N'View llms.txt Content');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.llms.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ffaa5a6a-acab-568b-85d4-051938db9e98';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.llms.update', N'H', N'H4', N'seo', N'update', 1, 0, 1, N'編輯 llms.txt 內容維護', N'Update llms.txt Content');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.crawler.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'91dd2d16-8666-5be8-835c-ab0e2fccdd83';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.crawler.view', N'H', N'H5', N'seo', N'view', 1, 0, 1, N'檢視 AI 爬蟲授權設定', N'View AI Crawler Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.crawler.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0023fbb3-d799-516c-bbfe-969f00c6abb6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.crawler.update', N'H', N'H5', N'seo', N'update', 1, 0, 1, N'編輯 AI 爬蟲授權設定', N'Update AI Crawler Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'seo.schema.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd55c318d-c56c-5d7b-99ad-4ad630c007cd';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'seo.schema.view', N'H', N'H6', N'seo', N'view', 1, 0, 1, N'檢視結構化資料完整性檢查', N'View Structured Data Completeness Report');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'site.fact.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f5083b41-f8bd-5be2-bc37-014dec8816e0';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'site.fact.view', N'I', N'I1', N'site', N'view', 1, 0, 1, N'檢視網站設定（站台事實）', N'View Site Facts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'site.fact.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c28a5e28-536f-5341-abf9-94f4e406b4fb';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'site.fact.update', N'I', N'I1', N'site', N'update', 1, 0, 1, N'編輯網站設定（站台事實）', N'Update Site Facts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.partner.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'153f2b49-175d-5941-b613-de2748f468cd';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.partner.view', N'E', N'E1', N'business', N'view', 1, 0, 0, N'檢視合作夥伴', N'View Partners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.partner.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9a1add15-b7b8-5bcd-a340-e4d4d70d0932';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.partner.create', N'E', N'E1', N'business', N'create', 1, 0, 0, N'新增合作夥伴', N'Create Partners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.partner.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ff376985-b3ef-5986-865e-ed15e30be562';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.partner.update', N'E', N'E1', N'business', N'update', 1, 0, 0, N'編輯合作夥伴', N'Update Partners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.partner.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'01645c38-1095-5201-ab01-0faa48bb7a3e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.partner.delete', N'E', N'E1', N'business', N'delete', 1, 0, 0, N'刪除合作夥伴', N'Delete Partners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.sponsor.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'51d0cb9d-8886-510e-9807-5a26c1d82823';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.sponsor.view', N'E', N'E2', N'business', N'view', 1, 0, 0, N'檢視贊助商', N'View Sponsors');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.sponsor.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'977994c3-68f6-5ba4-9647-74b18c141262';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.sponsor.create', N'E', N'E2', N'business', N'create', 1, 0, 0, N'新增贊助商', N'Create Sponsors');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.sponsor.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9930859b-9744-5197-8066-b2b8a7d2b26a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.sponsor.update', N'E', N'E2', N'business', N'update', 1, 0, 0, N'編輯贊助商與贊助活動', N'Update Sponsors');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.sponsor.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9f77136a-a9eb-5e86-b705-f18cfb446713';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.sponsor.delete', N'E', N'E2', N'business', N'delete', 1, 0, 0, N'刪除贊助商', N'Delete Sponsors');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.sponsor_package.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9a45c6a9-8ba9-5d56-afd4-6c2f445a5fd1';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.sponsor_package.view', N'E', N'E2', N'business', N'view', 1, 0, 0, N'檢視贊助方案', N'View Sponsorship Packages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.sponsor_package.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'72a3cb07-552f-5ea1-a493-32a6711d8781';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.sponsor_package.create', N'E', N'E2', N'business', N'create', 1, 0, 0, N'新增贊助方案', N'Create Sponsorship Packages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.sponsor_package.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7a16bc62-69a7-53c7-b743-0fd658e746fe';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.sponsor_package.update', N'E', N'E2', N'business', N'update', 1, 0, 0, N'編輯贊助方案', N'Update Sponsorship Packages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.sponsor_package.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'41bf8782-e757-50ca-9214-ed539fa4c85d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.sponsor_package.delete', N'E', N'E2', N'business', N'delete', 1, 0, 0, N'刪除贊助方案', N'Delete Sponsorship Packages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.proposal.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'aa6580c4-fc27-5ada-9294-9fea5e53809b';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.proposal.view', N'E', N'E3', N'business', N'view', 1, 0, 0, N'檢視提案簡介', N'View Proposals');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.proposal.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fb1ec849-4500-5fac-bff4-9b36bd83ebec';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.proposal.create', N'E', N'E3', N'business', N'create', 1, 0, 0, N'新增提案簡介', N'Create Proposals');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.proposal.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0834cf72-a414-5fa0-ae36-78e0ca4daf5c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.proposal.update', N'E', N'E3', N'business', N'update', 1, 0, 0, N'編輯提案簡介與檔案', N'Update Proposals');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.proposal.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'05a2c8cb-3f40-554a-90b9-ce5d43f6ffd9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.proposal.delete', N'E', N'E3', N'business', N'delete', 1, 0, 0, N'刪除提案簡介', N'Delete Proposals');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.lead.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd409aa5c-0ae8-580d-9399-f97056705215';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.lead.view', N'E', N'E3', N'business', N'view', 1, 0, 0, N'檢視提案下載名單', N'View Proposal Leads');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.lead.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b852b979-b690-57b7-b06b-a5439d013877';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.lead.update', N'E', N'E3', N'business', N'update', 1, 0, 0, N'標記提案下載名單的跟進狀態', N'Update Proposal Leads');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'business.lead.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2d23f6fe-4614-51d0-a264-5ccf8e6a396e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'business.lead.export', N'E', N'E3', N'business', N'export', 1, 1, 0, N'匯出提案下載名單', N'Export Proposal Leads');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'charity.content.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'62ff2c86-28df-515d-b7e4-09209711e60d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'charity.content.view', N'B', N'B5', N'charity', N'view', 1, 0, 0, N'檢視慈善內容', N'View Charity Content');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'charity.content.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'160577a5-5b61-5322-9a9a-461cb87e923b';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'charity.content.create', N'B', N'B5', N'charity', N'create', 1, 0, 0, N'新增慈善內容', N'Create Charity Content');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'charity.content.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'92f9f62d-3fb5-5030-95b1-87dbf600700d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'charity.content.update', N'B', N'B5', N'charity', N'update', 1, 0, 0, N'編輯慈善內容', N'Update Charity Content');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'charity.content.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd6a65a00-ffc6-5259-a784-843af2dfe318';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'charity.content.delete', N'B', N'B5', N'charity', N'delete', 1, 0, 0, N'刪除慈善內容', N'Delete Charity Content');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'charity.setting.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'04a5657e-dfc2-59a8-a59f-28e8932f47cb';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'charity.setting.view', N'B', N'B5', N'charity', N'view', 1, 0, 0, N'檢視捐款導流與參與方式設定', N'View Charity Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'charity.setting.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7a1c1ac9-b2d0-5bdc-b4a8-49244a2f8fea';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'charity.setting.update', N'B', N'B5', N'charity', N'update', 1, 0, 0, N'編輯捐款導流與參與方式設定', N'Update Charity Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.press.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'466d2cd8-1281-5bd7-ac9d-98dece068c30';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.press.view', N'B', N'B6', N'content', N'view', 1, 0, 0, N'檢視媒體專區', N'View Press Resources');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.press.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4fbbb75c-e138-5d5e-8867-884e94d617f3';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.press.create', N'B', N'B6', N'content', N'create', 1, 0, 0, N'新增媒體資源', N'Create Press Resources');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.press.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ffcea5a9-3211-5b31-b684-9036c10db25b';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.press.update', N'B', N'B6', N'content', N'update', 1, 0, 0, N'編輯媒體資源', N'Update Press Resources');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'content.press.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4c94e3b2-6f0e-5146-8d31-f4400de14b84';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'content.press.delete', N'B', N'B6', N'content', N'delete', 1, 0, 0, N'刪除媒體資源', N'Delete Press Resources');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.achievement.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'921e10a8-4948-5035-8b6e-e7fa2d72f2c2';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.achievement.view', N'C', N'C5', N'team', N'view', 1, 0, 0, N'檢視榮譽', N'View Achievements');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.achievement.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fd3cdb5b-f232-5368-87eb-85dcdaa5ce80';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.achievement.create', N'C', N'C5', N'team', N'create', 1, 0, 0, N'新增榮譽', N'Create Achievements');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.achievement.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f77f4d52-39c8-5747-a6c9-fd2613d06e02';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.achievement.update', N'C', N'C5', N'team', N'update', 1, 0, 0, N'編輯榮譽', N'Update Achievements');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.achievement.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6c35f4a4-0438-5871-a5d7-5049e05e0099';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.achievement.delete', N'C', N'C5', N'team', N'delete', 1, 0, 0, N'刪除榮譽', N'Delete Achievements');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.milestone.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6eb50217-8331-5db5-a8ae-4fedc0fdfd78';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.milestone.view', N'C', N'C5', N'team', N'view', 1, 0, 0, N'檢視里程碑', N'View Milestones');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.milestone.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'430ce112-72a8-5b6e-a42d-413d6a440617';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.milestone.create', N'C', N'C5', N'team', N'create', 1, 0, 0, N'新增里程碑', N'Create Milestones');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.milestone.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c03fa695-f936-5104-be90-c5b805373a49';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.milestone.update', N'C', N'C5', N'team', N'update', 1, 0, 0, N'編輯里程碑', N'Update Milestones');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'team.milestone.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6b39bf74-ca95-5f8c-a1a6-bbad08d63522';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'team.milestone.delete', N'C', N'C5', N'team', N'delete', 1, 0, 0, N'刪除里程碑', N'Delete Milestones');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.trial.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'da756bfd-f550-563f-8ba8-b8e6b1b02e02';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.trial.view', N'P', N'P4', N'program', N'view', 1, 0, 0, N'檢視試訓場次', N'View Trials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.trial.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'04a2681a-7e5f-5f71-920b-5fe1ff5e1fa3';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.trial.create', N'P', N'P4', N'program', N'create', 1, 0, 0, N'建立試訓場次', N'Create Trials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.trial.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2763f3c6-90e6-55c6-8571-dbcda4f4bc3d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.trial.update', N'P', N'P4', N'program', N'update', 1, 0, 0, N'編輯試訓場次', N'Update Trials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.trial.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'415a291d-efb8-5715-b93b-9b17e350c961';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.trial.delete', N'P', N'P4', N'program', N'delete', 1, 0, 0, N'刪除試訓場次', N'Delete Trials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.trial_registration.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'dbc70eab-5213-5766-8a1b-500b7603dcbd';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.trial_registration.view', N'P', N'P4', N'program', N'view', 1, 0, 0, N'檢視試訓報名', N'View Trial Registrations');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.trial_registration.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'af2e0f00-b5bd-5ebd-bd59-79bb1b3a1d21';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.trial_registration.create', N'P', N'P4', N'program', N'create', 1, 0, 0, N'建立試訓報名（後台代填）', N'Create Trial Registrations');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.trial_registration.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4aaf02bd-a56f-57b1-b6bd-b693cd174bf6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.trial_registration.update', N'P', N'P4', N'program', N'update', 1, 0, 0, N'處理試訓報名', N'Update Trial Registrations');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'program.trial_registration.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7f662f6f-f47d-5a9b-bd84-73abdd664e1c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'program.trial_registration.export', N'P', N'P4', N'program', N'export', 1, 1, 0, N'匯出試訓報名名單', N'Export Trial Registrations');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.account.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8174ab57-2dc2-5f1b-9f4f-4ab2a4ffc917';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.account.view', N'K', N'K1', N'member', N'view', 1, 0, 0, N'檢視會員名單', N'View Member Accounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.account.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'efb0c7c0-c77b-5fca-a65e-a638f5a966cf';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.account.create', N'K', N'K1', N'member', N'create', 1, 0, 0, N'建立會員（現場入會）', N'Create Member Accounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.account.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'78dcd0a7-9229-52c4-93dc-29f368aeb7dd';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.account.update', N'K', N'K1', N'member', N'update', 1, 0, 0, N'處理會員帳號（停用、備註、重產會員卡 QR）', N'Update Member Accounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.account.merge';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6eaa5729-2625-5ecd-81f5-e9e61a7b030d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.account.merge', N'K', N'K1', N'member', N'execute', 1, 0, 1, N'合併重複會員帳號', N'Merge Member Accounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.pii.reveal';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'03b3f237-6122-5421-81bd-bf5a6f8882ab';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.pii.reveal', N'K', N'K1', N'member', N'reveal', 1, 0, 0, N'檢視會員完整個資', N'Reveal Member PII');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'570bc347-0cdb-54ae-beab-be081e78e7e9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.export', N'K', N'K1', N'member', N'export', 1, 1, 0, N'匯出會員名單', N'Export Members');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.membership.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'04d4d8d4-96a7-5db7-b00a-6953439572e5';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.membership.view', N'K', N'K2', N'member', N'view', 1, 0, 0, N'檢視會籍與付款紀錄', N'View Memberships');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.membership.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4e0a6d0c-15c7-5fae-a857-e93a71833c96';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.membership.create', N'K', N'K2', N'member', N'create', 1, 0, 0, N'開通會籍（手動開通與續會）', N'Activate Memberships');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.membership.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'459cff7b-3984-5c8c-837c-c61200a157c1';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.membership.update', N'K', N'K2', N'member', N'update', 1, 0, 0, N'調整會籍（層級、到期、批次到期）', N'Update Memberships');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.plan.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8880d493-d2ac-56d2-94c2-52f7bccce917';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.plan.view', N'K', N'K2', N'member', N'view', 1, 0, 0, N'檢視會籍方案', N'View Membership Plans');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.plan.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'84df2338-d372-582c-81b4-0cf85f0e3617';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.plan.create', N'K', N'K2', N'member', N'create', 1, 0, 0, N'新增會籍方案', N'Create Membership Plans');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.plan.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fdfcc11b-8ecc-5c41-986a-0953fadd4e24';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.plan.update', N'K', N'K2', N'member', N'update', 1, 0, 0, N'編輯會籍方案', N'Update Membership Plans');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.plan.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'81c589bc-23d1-5fdd-8190-dfb041268542';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.plan.delete', N'K', N'K2', N'member', N'delete', 1, 0, 0, N'刪除會籍方案', N'Delete Membership Plans');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.setting.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4c02e02d-2638-54bd-94ba-c0a5849f3451';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.setting.view', N'K', N'K2', N'member', N'view', 1, 0, 0, N'檢視會員編號規則', N'View Member Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.setting.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3e4f1ee1-c696-5b4d-b4d0-f6cfab1b73dc';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.setting.update', N'K', N'K2', N'member', N'update', 1, 0, 0, N'編輯會員編號規則', N'Update Member Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.jersey.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b4e1f31a-f09a-5aae-937f-3b7e8676d1a2';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.jersey.view', N'K', N'K3', N'member', N'view', 1, 0, 0, N'檢視球衣發放', N'View Jersey Issues');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.jersey.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'db3a7d9b-ae51-5c15-867b-eb260c004d56';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.jersey.create', N'K', N'K3', N'member', N'create', 1, 0, 0, N'建立球衣登記（後台代填）', N'Create Jersey Issues');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.jersey.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1efcd573-361d-5f71-9b91-7741456328d1';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.jersey.update', N'K', N'K3', N'member', N'update', 1, 0, 0, N'處理球衣發放', N'Update Jersey Issues');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.jersey.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'33202d73-6118-58e2-96d3-706095fa723a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.jersey.export', N'K', N'K3', N'member', N'export', 1, 1, 0, N'匯出球衣出貨清單', N'Export Jersey Issues');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.store.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b6202e5c-a908-53d3-a012-f967dc62a4ed';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.store.view', N'K', N'K4', N'member', N'view', 1, 0, 0, N'檢視特約店家', N'View Partner Stores');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.store.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4ca7f115-94d3-58aa-9f7e-9e3362ca41f0';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.store.create', N'K', N'K4', N'member', N'create', 1, 0, 0, N'新增特約店家', N'Create Partner Stores');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.store.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'28514a79-f7eb-5e52-8464-4285e264c629';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.store.update', N'K', N'K4', N'member', N'update', 1, 0, 0, N'編輯特約店家', N'Update Partner Stores');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.store.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5da571ab-e918-5775-b656-2e64ee570958';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.store.delete', N'K', N'K4', N'member', N'delete', 1, 0, 0, N'刪除特約店家', N'Delete Partner Stores');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.benefit.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a502c22e-7185-565f-9a3d-684b7d7008c8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.benefit.view', N'K', N'K4', N'member', N'view', 1, 0, 0, N'檢視權益對照表', N'View Membership Benefits');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.benefit.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'05a03863-68ed-5ff9-b8fa-70359f35b577';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.benefit.create', N'K', N'K4', N'member', N'create', 1, 0, 0, N'新增權益條目', N'Create Membership Benefits');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.benefit.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2c8e5bb3-d861-5c35-b8ae-89b6e50c42b9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.benefit.update', N'K', N'K4', N'member', N'update', 1, 0, 0, N'編輯權益條目', N'Update Membership Benefits');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.benefit.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2d46d033-e4d7-5493-974b-a2e7aba8f704';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.benefit.delete', N'K', N'K4', N'member', N'delete', 1, 0, 0, N'刪除權益條目', N'Delete Membership Benefits');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.setting.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd2eda6ee-99cc-516b-832b-d09bd4e10e56';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.setting.view', N'L', N'L3', N'calendar', N'view', 1, 0, 0, N'檢視行事曆分類與顯示設定', N'View Calendar Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.setting.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'40538e77-c650-5514-9b8a-6173d07e5ec7';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.setting.update', N'L', N'L3', N'calendar', N'update', 1, 0, 0, N'編輯行事曆分類與顯示設定', N'Update Calendar Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.subscription.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'86b53442-bf33-5bfe-b0a2-96e65f589ad6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.subscription.view', N'L', N'L4', N'calendar', N'view', 1, 0, 0, N'檢視行事曆訂閱網址與訂閱數', N'View Calendar Subscriptions');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'calendar.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'52ec0da2-3c78-562c-9595-80ade449cb57';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'calendar.export', N'L', N'L4', N'calendar', N'export', 1, 0, 0, N'匯出行事曆（CSV／.ics）', N'Export Calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'culture.comic.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c582c3a0-010e-5ee2-a337-3319e5b235ce';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'culture.comic.view', N'F', N'F1', N'culture', N'view', 1, 0, 0, N'檢視漫畫', N'View Comics');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'culture.comic.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'713aa5a0-a23f-5498-8e44-7ea784fa3699';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'culture.comic.create', N'F', N'F1', N'culture', N'create', 1, 0, 0, N'新增漫畫角色與集數', N'Create Comics');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'culture.comic.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9c28ab54-f7dd-5086-b079-839e03aa9e7c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'culture.comic.update', N'F', N'F1', N'culture', N'update', 1, 0, 0, N'編輯漫畫企劃、角色與集數', N'Update Comics');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'culture.comic.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b6f99784-501a-5174-99f2-71cba2342222';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'culture.comic.delete', N'F', N'F1', N'culture', N'delete', 1, 0, 0, N'刪除漫畫角色與集數', N'Delete Comics');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'culture.fan_event.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'680f3435-cb36-59e3-b65c-b0dae080889d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'culture.fan_event.view', N'F', N'F2', N'culture', N'view', 1, 0, 0, N'檢視球迷會活動與報名名單', N'View Fan Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'culture.fan_event.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b0738cc5-ccde-55c4-8065-cb8570c7803a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'culture.fan_event.create', N'F', N'F2', N'culture', N'create', 1, 0, 0, N'新增球迷會活動', N'Create Fan Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'culture.fan_event.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e868876c-4f5d-539c-bf41-18257ff13601';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'culture.fan_event.update', N'F', N'F2', N'culture', N'update', 1, 0, 0, N'編輯球迷會活動與處理報名', N'Update Fan Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'culture.fan_event.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3ff87d08-650f-5477-8c0b-7769f5cfced1';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'culture.fan_event.delete', N'F', N'F2', N'culture', N'delete', 1, 0, 0, N'刪除球迷會活動', N'Delete Fan Events');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.collection.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ab8af551-7f8f-5424-bcf3-0acf5d872850';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.collection.view', N'S', N'S1', N'shop', N'view', 1, 0, 0, N'檢視商品系列', N'View Shop Collections');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.collection.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6b4bf495-b2b2-561d-ae26-7813dcc16d4e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.collection.create', N'S', N'S1', N'shop', N'create', 1, 0, 0, N'新增商品系列', N'Create Shop Collections');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.collection.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'aada12a4-7103-5cd4-a275-16afacf5edcf';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.collection.update', N'S', N'S1', N'shop', N'update', 1, 0, 0, N'編輯商品系列與系列介紹文', N'Update Shop Collections');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.collection.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'81fd4d17-9626-581d-a54e-60bbd06c0426';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.collection.delete', N'S', N'S1', N'shop', N'delete', 1, 0, 0, N'刪除商品系列', N'Delete Shop Collections');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.product.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'827b034b-d502-5306-ab3f-c1d9408cca7d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.product.view', N'S', N'S1', N'shop', N'view', 1, 0, 0, N'檢視商品', N'View Products');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.product.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c1b27411-465f-540c-842d-4856968c1c55';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.product.create', N'S', N'S1', N'shop', N'create', 1, 0, 0, N'新增商品', N'Create Products');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.product.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7a5966f1-a92d-5a94-b1ef-f86beff26d4d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.product.update', N'S', N'S1', N'shop', N'update', 1, 0, 0, N'編輯商品文案、圖片與上下架', N'Update Products');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.product.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'51173579-6c3b-5508-b912-04ed300bc3d6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.product.delete', N'S', N'S1', N'shop', N'delete', 1, 0, 0, N'刪除商品', N'Delete Products');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.variant.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6439dcde-d3bf-52ab-a334-c4f7db0c9ada';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.variant.view', N'S', N'S1', N'shop', N'view', 1, 0, 0, N'檢視商品規格與售價', N'View Product Variants');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.variant.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'dec3c6be-2a94-5f66-81a7-537d1eee8202';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.variant.create', N'S', N'S1', N'shop', N'create', 1, 0, 0, N'新增商品規格', N'Create Product Variants');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.variant.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3d2d8219-c857-514d-9e82-462b3a358a15';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.variant.update', N'S', N'S1', N'shop', N'update', 1, 0, 0, N'編輯商品規格與售價', N'Update Product Variants');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.variant.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'92a5c33a-9bac-5322-bc30-5d03a7ffbb40';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.variant.delete', N'S', N'S1', N'shop', N'delete', 1, 0, 0, N'刪除商品規格', N'Delete Product Variants');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.cost.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd98efb6e-94ca-510b-b0e5-6e4663ac3610';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.cost.view', N'S', N'S1', N'shop', N'view', 1, 1, 0, N'檢視商品成本', N'View Product Costs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.cost.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd3f08ba4-66c7-5230-9b26-7a224ae4b3d8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.cost.update', N'S', N'S1', N'shop', N'update', 1, 1, 0, N'編輯商品成本', N'Update Product Costs');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.inventory.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'22ab699c-8877-5c2e-ad55-a3ea3342b34a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.inventory.view', N'S', N'S2', N'shop', N'view', 1, 0, 0, N'檢視庫存與異動紀錄', N'View Inventory');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.inventory.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7eab1a49-1596-534d-b0fc-e7c34f945b5f';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.inventory.update', N'S', N'S2', N'shop', N'update', 1, 0, 0, N'進貨、盤點、報損與調整庫存', N'Update Inventory');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.order.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f57afa16-f064-52e5-83db-4725778d9fff';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.order.view', N'S', N'S3', N'shop', N'view', 1, 0, 0, N'檢視訂單（收件人資料依權限遮罩）', N'View Orders');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.order.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cf6537f5-590a-5b3b-9c23-fc5b4ee85882';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.order.create', N'S', N'S3', N'shop', N'create', 1, 0, 0, N'建立與補登訂單（現場收款）', N'Create Orders');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.order.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd1d89713-26c5-5b80-87e2-e567467aaf4e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.order.update', N'S', N'S3', N'shop', N'update', 1, 0, 0, N'處理訂單狀態、備註與分帳標記', N'Update Orders');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.order.reveal';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'aa50af3b-2282-501c-8200-a259ede607f3';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.order.reveal', N'S', N'S3', N'shop', N'reveal', 1, 0, 0, N'檢視訂單收件人完整資料', N'Reveal Order Recipient');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.order.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ce981186-fb3c-5d9f-ab50-c41c13e3bb8e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.order.export', N'S', N'S3', N'shop', N'export', 1, 1, 0, N'匯出訂單', N'Export Orders');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.shipment.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'943491e0-960d-5eb5-9bc4-9c4003d6a360';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.shipment.view', N'S', N'S4', N'shop', N'view', 1, 0, 0, N'檢視出貨與揀貨單', N'View Shipments');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.shipment.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fe350eac-fa4c-54f9-9522-d41bbb596cdb';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.shipment.update', N'S', N'S4', N'shop', N'update', 1, 0, 0, N'處理出貨、物流單號與自取', N'Update Shipments');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.refund.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'672b98a3-feae-57f7-b043-4e3d617b1b6b';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.refund.view', N'S', N'S5', N'shop', N'view', 1, 0, 0, N'檢視退貨退款案件', N'View Refund Requests');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.refund.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c751bdb2-1b84-55b7-956f-97081b5460a9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.refund.update', N'S', N'S5', N'shop', N'update', 1, 0, 0, N'建立、審核與驗收退貨退款案件', N'Update Refund Requests');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.refund.execute';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a567732d-3772-5cde-b42c-3ed1bb92e72d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.refund.execute', N'S', N'S5', N'shop', N'execute', 1, 0, 1, N'執行退款', N'Execute Refunds');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.setting.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c17268ec-ce2a-5129-bc80-c81a0c9dbb5c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.setting.view', N'S', N'S6', N'shop', N'view', 1, 0, 0, N'檢視商店設定', N'View Shop Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.setting.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'697621ff-c8c1-5bc6-87ec-c293aaa5d370';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.setting.update', N'S', N'S6', N'shop', N'update', 1, 0, 0, N'編輯運費與商店政策', N'Update Shop Settings');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.credential.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'620dc2f3-91e9-50c7-a58f-06cb348051f2';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.credential.view', N'S', N'S6', N'shop', N'view', 1, 1, 1, N'檢視金流與發票憑證狀態', N'View Payment Credentials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.credential.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b91a0e08-9635-5de0-ac15-f7cdd51684a5';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.credential.update', N'S', N'S6', N'shop', N'update', 1, 1, 1, N'編輯金流與發票憑證', N'Update Payment Credentials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.report.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6492ea17-c1e7-53f7-b3ed-c1c6045d1cba';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.report.view', N'S', N'S6', N'shop', N'view', 1, 0, 0, N'檢視商店報表', N'View Shop Reports');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.report.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'94f41d7a-1590-557c-9018-c80bcd948167';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.report.export', N'S', N'S6', N'shop', N'export', 1, 1, 0, N'匯出商店報表與分帳彙總', N'Export Shop Reports');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.donation_code.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a57da854-dd55-579f-afad-6cfc7e405a98';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.donation_code.view', N'S', N'S6', N'shop', N'view', 0, 0, 0, N'檢視發票捐贈碼名單', N'View Donation Codes');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.donation_code.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cc34c79b-3f13-5a36-9223-aaa89404d101';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.donation_code.create', N'S', N'S6', N'shop', N'create', 0, 0, 0, N'新增發票捐贈碼', N'Create Donation Codes');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.donation_code.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'52302c94-ef4a-5fca-ae76-11966f36d338';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.donation_code.update', N'S', N'S6', N'shop', N'update', 0, 0, 0, N'編輯發票捐贈碼', N'Update Donation Codes');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'shop.donation_code.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b03882dd-1722-554f-b2f1-9372bbd84665';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'shop.donation_code.delete', N'S', N'S6', N'shop', N'delete', 0, 0, 0, N'刪除發票捐贈碼', N'Delete Donation Codes');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.draw.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9f3b5f8d-b3a0-5d07-9d5a-634897217c23';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.draw.view', N'K', N'K5', N'member', N'view', 1, 0, 0, N'檢視抽獎活動與遮罩名單', N'View Member Draws');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.draw.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'691588fe-cdb8-5c23-8316-9625ae32d0b9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.draw.create', N'K', N'K5', N'member', N'create', 1, 0, 0, N'建立抽獎活動', N'Create Member Draws');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.draw.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'906b96ce-a6d1-5005-bda9-f865107b0dc4';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.draw.update', N'K', N'K5', N'member', N'update', 1, 0, 0, N'產生名單、回填中獎人與處理獎品發放', N'Update Member Draws');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.draw.announce';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'824c59c4-3f94-5399-805a-ba8c99c1992c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.draw.announce', N'K', N'K5', N'member', N'update', 1, 0, 0, N'產生抽獎公布稿草稿（只看得到遮罩名單）', N'Announce Member Draws');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'member.draw.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9b019a08-5117-5a93-a37b-9d245b659a8d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'member.draw.export', N'K', N'K5', N'member', N'export', 1, 1, 0, N'匯出中獎人聯絡名單與獎品出貨清單', N'Export Member Draw Winners');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'form.newsletter.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'48f012ea-2259-51b5-9806-7fb047d68e09';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'form.newsletter.view', N'G', N'G3', N'enquiry', N'view', 1, 0, 0, N'檢視電子報訂閱名單', N'View Newsletter Subscribers');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'form.newsletter.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3904b6db-d618-5f55-a6fe-6cdb481b3855';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'form.newsletter.update', N'G', N'G3', N'enquiry', N'update', 1, 0, 0, N'新增、退訂與同步電子報訂閱者', N'Update Newsletter Subscribers');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'form.newsletter.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'160f71b6-75b3-5723-98bb-ddbea8e646b6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'form.newsletter.export', N'G', N'G3', N'enquiry', N'export', 1, 1, 0, N'匯出電子報訂閱名單', N'Export Newsletter Subscribers');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.advertiser.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'feb790b4-bdcd-5503-a70e-4479e5da417d';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.advertiser.view', N'E', N'E4', N'ad', N'view', 0, 0, 0, N'檢視廣告主', N'View Advertisers');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.advertiser.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8fb7023a-8132-5723-ab90-0b915d72e245';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.advertiser.create', N'E', N'E4', N'ad', N'create', 0, 0, 0, N'新增廣告主', N'Create Advertisers');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.advertiser.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2457c9f2-4fd7-5944-a260-9838588bea12';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.advertiser.update', N'E', N'E4', N'ad', N'update', 0, 0, 0, N'編輯廣告主', N'Update Advertisers');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.advertiser.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'31de4e23-67c4-5079-85be-6e42370c0d9f';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.advertiser.delete', N'E', N'E4', N'ad', N'delete', 0, 0, 0, N'刪除廣告主', N'Delete Advertisers');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.slot.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'157c25fa-54c4-5654-9adc-f41ba36e62d4';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.slot.view', N'E', N'E4', N'ad', N'view', 0, 0, 0, N'檢視廣告版位', N'View Ad Slots');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.slot.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'744e2b03-f5c0-54e0-8140-5555bd1ce6c6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.slot.create', N'E', N'E4', N'ad', N'create', 0, 0, 0, N'新增廣告版位', N'Create Ad Slots');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.slot.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'807d21a9-be87-5be6-a1e8-e4f94e0ff68e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.slot.update', N'E', N'E4', N'ad', N'update', 0, 0, 0, N'編輯廣告版位與備援素材', N'Update Ad Slots');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.slot.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7e70e4f9-ebbb-5dc4-aed8-05a923ae68c9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.slot.delete', N'E', N'E4', N'ad', N'delete', 0, 0, 0, N'刪除廣告版位', N'Delete Ad Slots');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.campaign.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8c54f9da-89eb-5dc9-86f0-ca3ba41b6a35';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.campaign.view', N'E', N'E5', N'ad', N'view', 0, 0, 0, N'檢視投放檔期與素材', N'View Ad Campaigns');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.campaign.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a75e8822-461e-5a94-a644-73a49f300633';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.campaign.create', N'E', N'E5', N'ad', N'create', 0, 0, 0, N'建立投放檔期', N'Create Ad Campaigns');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.campaign.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e3572408-7dc2-50d2-8ca5-c759933aa9a8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.campaign.update', N'E', N'E5', N'ad', N'update', 0, 0, 0, N'編輯檔期、素材、送審、結案與作廢', N'Update Ad Campaigns');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.campaign.delete';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'86bf1e91-9109-52db-aa48-7e6230e87d0a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.campaign.delete', N'E', N'E5', N'ad', N'delete', 0, 0, 0, N'刪除草稿檔期', N'Delete Ad Campaigns');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.campaign.review';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9ee19bf0-1cdc-5031-b855-dbaec43ef35a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.campaign.review', N'E', N'E5', N'ad', N'review', 0, 0, 0, N'審核檔期與廣告素材', N'Review Ad Campaigns');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.campaign.pause';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5f53e7ee-1960-5d16-ae0b-61201b9f195e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.campaign.pause', N'E', N'E5', N'ad', N'pause', 0, 0, 0, N'緊急暫停與恢復檔期、素材', N'Pause Ad Campaigns');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.contract.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b303d8b7-3103-5031-b8bf-b7ce70ab5f1c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.contract.view', N'E', N'E5', N'ad', N'view', 0, 1, 0, N'檢視廣告合約金額', N'View Ad Contract Amounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.contract.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f9d9a346-eaa6-554f-96b1-267c6a564242';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.contract.update', N'E', N'E5', N'ad', N'update', 0, 1, 0, N'編輯廣告合約金額', N'Update Ad Contract Amounts');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.report.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0959cf83-a464-53fc-80b5-761d35b159c4';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.report.view', N'E', N'E6', N'ad', N'view', 0, 0, 0, N'檢視廣告成效報表', N'View Ad Reports');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.report.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'205bbaf6-12f4-5e55-b716-a5fd7d484a20';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.report.export', N'E', N'E6', N'ad', N'export', 0, 1, 0, N'匯出廣告成效報表', N'Export Ad Reports');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'ad.maintenance.run';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e0b1a0c1-54c1-554e-a0a9-ca8348f20394';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'ad.maintenance.run', N'E', N'E6', N'ad', N'run', 0, 0, 1, N'手動執行廣告維護作業（推進檔期、聚合、清除）', N'Run Ad Maintenance');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.release.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'27b4d4ea-9543-5b5e-af85-3f24a57de250';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.release.view', N'M', N'M1', N'app', N'view', 0, 0, 0, N'檢視 App 版本與維護模式', N'View App Releases');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.release.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6edfe5f7-7298-5aa1-a6ca-06cb5506431b';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.release.update', N'M', N'M1', N'app', N'update', 0, 0, 1, N'管理 App 版本、強制更新與維護模式', N'Update App Releases');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.layout.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7a2224c4-8c93-5f5f-9fad-9a91ac0ecfc8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.layout.view', N'M', N'M2', N'app', N'view', 0, 0, 0, N'檢視 App 內容編排與深連結', N'View App Layout');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.layout.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c7ea8a83-feec-59bc-8e2e-87687cd1dac9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.layout.update', N'M', N'M2', N'app', N'update', 0, 0, 0, N'編排首頁區塊、快捷入口、更多分頁、公告條與深連結', N'Update App Layout');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.push.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6b3392de-b80c-54a4-acc0-fec4b32df931';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.push.view', N'M', N'M3', N'app', N'view', 0, 0, 0, N'檢視推播批次與發送紀錄', N'View Push Messages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.push.create';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd61cecb1-ee27-5383-8a8e-307dfa8b1e77';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.push.create', N'M', N'M3', N'app', N'create', 0, 0, 0, N'建立推播批次、預覽、試送與送審', N'Create Push Messages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.push.approve';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4a0fddd0-461c-52ae-8f1f-85f775e544a8';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.push.approve', N'M', N'M3', N'app', N'approve', 0, 0, 1, N'覆核推播批次、重送與設定自動推播規則', N'Approve Push Messages');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.device.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5a28b81d-8308-5616-9ca8-881d66463aab';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.device.view', N'M', N'M4', N'app', N'view', 0, 0, 0, N'檢視推播裝置（遮罩）與統計', N'View App Devices');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.device.reveal';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'68191315-1af2-5b05-9422-cc32dc3b1535';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.device.reveal', N'M', N'M4', N'app', N'reveal', 0, 1, 1, N'檢視推播權杖與裝置識別碼完整值', N'Reveal App Device Tokens');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.device.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'97e0ebef-d234-5391-9bc6-8ba4c4c47404';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.device.update', N'M', N'M4', N'app', N'update', 0, 0, 1, N'清理失效推播權杖', N'Update App Devices');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.config.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f7863236-849b-50a4-8aae-7332eda76fde';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.config.view', N'M', N'M5', N'app', N'view', 0, 0, 0, N'檢視功能開關與連線檢查', N'View App Config');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.config.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'82a58e27-c859-5c3e-9fe9-645637c8a28e';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.config.update', N'M', N'M5', N'app', N'update', 0, 0, 1, N'管理 App 功能開關', N'Update App Config');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.credential.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd2ead3f2-f386-5d79-82db-1e24b0a70adc';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.credential.view', N'M', N'M5', N'app', N'view', 0, 1, 1, N'檢視 App 金鑰與憑證列管', N'View App Credentials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.credential.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0689dd5e-b428-570a-8e62-597065b858e0';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.credential.update', N'M', N'M5', N'app', N'update', 0, 1, 1, N'管理 App 金鑰與憑證列管與輪替', N'Update App Credentials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.diagnostic.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'00d7a4c5-ace7-5ec6-8e92-9638a10ffba5';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.diagnostic.view', N'M', N'M5', N'app', N'view', 0, 0, 0, N'檢視 App 診斷回報', N'View App Diagnostics');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'app.diagnostic.update';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1cd82a82-f4dc-5500-9244-a3b7eb22207a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, N'app.diagnostic.update', N'M', N'M5', N'app', N'update', 0, 0, 0, N'處理 App 診斷回報', N'Update App Diagnostics');
  COMMIT TRANSACTION;
END
GO

-- ── 18.3 role_permissions ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.article.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.article.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.article.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.publish'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.article.publish'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.article.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.page.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.page.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.page.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.publish'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.page.publish'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.page.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.account.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.account.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.account.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.account.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.account.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.account.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.role.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.role.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.role.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.role.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.audit.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.audit.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.club_grant.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.club_grant.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.club_grant.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.club_grant.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.club.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.club.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.club.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.club.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.team_grant.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.team_grant.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'system.team_grant.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'system.team_grant.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.competition.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.competition.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.competition.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.team.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.team.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.team.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.player.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.player.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.player.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.staff.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.staff.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.staff.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.banner.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.banner.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.banner.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.banner.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.home_section.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.home_section.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.home_section.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.home_section.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.faq.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.faq.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.faq.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.faq.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.faq_category.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.faq_category.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.faq_category.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.faq_category.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.match.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.match.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.match.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.match.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.standing.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.standing.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.standing.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.standing.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.item.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.item.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.item.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.session.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.session.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.session.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.registration.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.registration.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.registration.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'form.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'form.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.inbox.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.inbox.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.inbox.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.inbox.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.inbox.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.inbox.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.course.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.course.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.course.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.course.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.partnership.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.partnership.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.partnership.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.partnership.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.media.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.media.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.media.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.media.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.redirect.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.redirect.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.redirect.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.redirect.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.redirect.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.redirect.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.redirect.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.redirect.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.redirect.import'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.redirect.import'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.report.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.llms.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.llms.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.llms.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.llms.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.crawler.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.crawler.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.crawler.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.crawler.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'seo.schema.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'seo.schema.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'site.fact.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'site.fact.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'site.fact.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'site.fact.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.partner.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.partner.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.partner.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.partner.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.sponsor.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.sponsor.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.sponsor.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.sponsor.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.proposal.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.proposal.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.proposal.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.proposal.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.lead.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.lead.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.lead.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.lead.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.lead.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'business.lead.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'charity.content.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'charity.content.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'charity.content.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'charity.content.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'charity.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'charity.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.press.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.press.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.press.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'content.press.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.achievement.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.achievement.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.achievement.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.achievement.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.milestone.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.milestone.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.milestone.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'team.milestone.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.trial.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.trial.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.trial.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.trial.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.account.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.account.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.account.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.account.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.account.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.account.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.account.merge'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.account.merge'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.pii.reveal'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.pii.reveal'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.membership.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.membership.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.membership.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.membership.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.membership.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.membership.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.plan.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.plan.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.plan.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.plan.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.plan.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.plan.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.plan.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.plan.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.jersey.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.jersey.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.jersey.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.jersey.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.store.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.store.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.store.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.store.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.store.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.store.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.store.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.store.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.benefit.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.benefit.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.benefit.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.benefit.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.benefit.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.benefit.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.benefit.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.benefit.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'culture.comic.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'culture.comic.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'culture.comic.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'culture.comic.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.collection.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.collection.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.collection.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.collection.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.product.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.product.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.product.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.product.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.variant.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.variant.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.variant.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.variant.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.cost.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.cost.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.cost.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.cost.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.inventory.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.inventory.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.inventory.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.inventory.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.reveal'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.reveal'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.shipment.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.shipment.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.shipment.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.shipment.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.refund.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.refund.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.refund.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.refund.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.refund.execute'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.refund.execute'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.credential.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.credential.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.credential.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.credential.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.report.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.report.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.report.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.donation_code.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.donation_code.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.donation_code.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.donation_code.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.donation_code.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.donation_code.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.donation_code.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'shop.donation_code.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.announce'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.announce'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.newsletter.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'form.newsletter.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.newsletter.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'form.newsletter.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.newsletter.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'form.newsletter.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.slot.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.slot.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.slot.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.slot.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.campaign.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.campaign.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.campaign.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.campaign.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.review'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.campaign.review'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.pause'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.campaign.pause'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.contract.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.contract.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.contract.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.contract.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.report.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.report.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.report.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.maintenance.run'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'ad.maintenance.run'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.release.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.release.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.release.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.release.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.layout.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.layout.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.layout.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.layout.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.push.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.push.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.push.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.push.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.push.approve'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.push.approve'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.device.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.device.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.device.reveal'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.device.reveal'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.device.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.device.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.config.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.config.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.config.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.config.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.credential.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.credential.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.credential.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.credential.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.diagnostic.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.diagnostic.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.diagnostic.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'app.diagnostic.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.article.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.article.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.article.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.publish'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.article.publish'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.article.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.page.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.page.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.page.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.publish'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.page.publish'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.page.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'team.competition.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'team.team.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'team.player.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'team.staff.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.competition.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.competition.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.competition.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.team.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.team.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.team.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.player.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.player.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.player.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.staff.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.staff.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.staff.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'team.team.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'team.player.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'team.staff.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'team.team.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'team.player.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'team.staff.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'content.article.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'content.page.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'team.competition.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'team.team.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'team.player.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'team.staff.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.article.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.article.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.article.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.article.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.page.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.page.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.page.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.page.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.competition.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.competition.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.competition.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.team.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.team.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.team.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.player.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.player.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.player.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.staff.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.staff.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.staff.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.banner.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.banner.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.banner.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.banner.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.home_section.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.home_section.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.home_section.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.home_section.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'content.banner.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.home_section.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'content.home_section.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.banner.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.banner.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.banner.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.banner.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.home_section.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.home_section.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.home_section.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.home_section.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.faq.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.faq.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.faq.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.faq.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.faq_category.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.faq_category.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.faq_category.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.faq_category.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'content.faq.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'content.faq_category.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'content.faq.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'content.faq.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'content.faq.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'content.faq.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'content.faq_category.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'content.faq.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'content.faq_category.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.faq.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.faq.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.faq.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.faq_category.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.faq_category.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.match.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.match.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.match.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.match.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.standing.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.standing.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.standing.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.standing.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'team.match.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'team.standing.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'team.match.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'team.standing.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'team.match.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'team.standing.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.match.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.match.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.match.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.match.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.standing.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.standing.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.standing.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.standing.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.standing.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.team.view'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.team.create'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.team.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.team.update'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.player.view'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.player.create'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.player.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.player.update'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.staff.view'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.staff.create'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.staff.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.staff.update'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.match.view'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.match.create'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.match.update'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.match.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.match.delete'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.competition.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.competition.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'program.item.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'program.session.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'program.registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'program.item.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'program.session.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'program.registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'program.item.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'program.session.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'program.registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'program.item.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'program.session.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'program.registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.item.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.item.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.item.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.session.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.session.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.session.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.registration.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.registration.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.registration.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'program.registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'program.registration.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.item.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.item.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.item.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.item.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.session.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.session.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.session.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.session.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.registration.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.registration.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.registration.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.registration.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.course.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'enquiry.course.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.course.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'enquiry.course.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.partnership.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'enquiry.partnership.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.partnership.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'enquiry.partnership.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.media.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'enquiry.media.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.media.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'enquiry.media.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'form.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'form.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.inbox.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.inbox.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.inbox.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'enquiry.inbox.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'form.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.inbox.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'enquiry.inbox.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'form.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'form.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.inbox.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'enquiry.inbox.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'enquiry.inbox.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'enquiry.inbox.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.custom_event.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.custom_event.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.partner.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.partner.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.partner.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.partner.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.sponsor.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.sponsor.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.sponsor.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.sponsor.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.proposal.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.proposal.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.proposal.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.proposal.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.lead.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.lead.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.lead.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.lead.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.lead.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'business.lead.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'business.partner.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'business.sponsor.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'business.proposal.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'business.partner.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'business.sponsor.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'business.proposal.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'business.partner.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'business.sponsor.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'business.proposal.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.partner.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.partner.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.partner.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.partner.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.partner.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.sponsor.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.sponsor.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.sponsor.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.sponsor.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.sponsor_package.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.sponsor_package.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.proposal.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.proposal.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.proposal.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.proposal.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.proposal.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.lead.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.lead.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'business.lead.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'business.lead.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'charity.content.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'charity.content.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'charity.content.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'charity.content.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'charity.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'charity.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'charity.content.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'charity.content.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'charity.content.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'charity.content.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'charity.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'charity.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'charity.content.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'charity.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.content.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'charity.content.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'charity.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'charity.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.press.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.press.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.press.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'content.press.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'content.press.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'content.press.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'content.press.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'content.press.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'content.press.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.press.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.press.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'content.press.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'content.press.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.achievement.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.achievement.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.achievement.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.achievement.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.milestone.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.milestone.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.milestone.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'team.milestone.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.achievement.view'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.achievement.create'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.achievement.update'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'team.achievement.delete'), N'academy_only');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'team.achievement.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'team.milestone.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'team.achievement.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'team.milestone.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'team.achievement.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'team.milestone.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'team.achievement.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'team.milestone.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.achievement.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.achievement.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.achievement.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.achievement.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.achievement.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.milestone.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.milestone.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.milestone.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'team.milestone.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'team.milestone.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'program.trial.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'program.trial.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'program.trial.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'program.trial.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.trial.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.trial.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.trial.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.trial.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'program.trial.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.trial.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.trial.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.trial.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.trial.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'program.trial_registration.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'program.trial_registration.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.account.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.account.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.account.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.account.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.account.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.account.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.pii.reveal'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.pii.reveal'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.membership.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.membership.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.membership.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.membership.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.membership.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.membership.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.plan.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.plan.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.jersey.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.jersey.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.jersey.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.jersey.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.store.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.store.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.store.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.store.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.store.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.store.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.store.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.store.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.benefit.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.benefit.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.benefit.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.benefit.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.benefit.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.benefit.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.benefit.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.benefit.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.account.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'member.account.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.membership.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'member.membership.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.plan.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'member.plan.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.jersey.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'member.jersey.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'team_competition') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'team_competition'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'academy_program') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'academy_program'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.setting.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.setting.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.subscription.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'calendar.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'calendar.export'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'culture.comic.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'culture.comic.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'culture.comic.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'culture.comic.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'culture.comic.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'culture.comic.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'culture.comic.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'culture.comic.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.comic.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'culture.comic.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'culture.fan_event.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'culture.fan_event.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'shop.collection.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'shop.collection.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'shop.collection.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'shop.product.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'shop.product.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'shop.product.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'shop.variant.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.collection.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.collection.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.collection.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.collection.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.product.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.product.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.product.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.product.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.variant.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.variant.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.variant.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.variant.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.cost.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.cost.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.cost.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.cost.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.inventory.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.inventory.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.order.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.setting.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.setting.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.report.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.report.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.report.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.donation_code.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.donation_code.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.donation_code.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.donation_code.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.donation_code.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.donation_code.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.donation_code.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'shop.donation_code.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'shop.collection.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'shop.product.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'shop.variant.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.product.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.variant.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.inventory.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.inventory.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.inventory.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.inventory.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.reveal'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.reveal'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.order.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.shipment.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.shipment.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.shipment.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.shipment.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.refund.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.refund.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.refund.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'shop.refund.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'shop.collection.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'shop.product.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.collection.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.collection.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.collection.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.collection.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.collection.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.product.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.product.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.product.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.product.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.product.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.variant.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.variant.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.variant.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.variant.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.variant.delete'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.inventory.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.inventory.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.inventory.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.inventory.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.order.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.order.create'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.order.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.order.reveal'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.order.reveal'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.shipment.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.shipment.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.shipment.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.shipment.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.refund.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.refund.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.setting.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.setting.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.setting.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'shop.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'shop.report.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.announce'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.announce'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'member.draw.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'member.draw.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'member.draw.announce'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'member.draw.announce'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.newsletter.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'form.newsletter.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.newsletter.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'form.newsletter.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.newsletter.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'form.newsletter.view'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'partner_club_manager') AND permission_id = (SELECT id FROM permissions WHERE code = N'form.newsletter.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'partner_club_manager'), (SELECT id FROM permissions WHERE code = N'form.newsletter.update'), N'own_clubs');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.slot.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.slot.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.slot.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.slot.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.campaign.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.campaign.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.campaign.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.delete'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.campaign.delete'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.review'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.campaign.review'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.pause'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.campaign.pause'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.contract.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.contract.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.contract.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.contract.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.report.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.report.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'ad.report.export'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'ad.report.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.advertiser.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'ad.advertiser.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.slot.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'ad.slot.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.campaign.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'ad.campaign.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'ad.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'ad.report.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.layout.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'app.layout.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'content_editor') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.layout.update'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'content_editor'), (SELECT id FROM permissions WHERE code = N'app.layout.update'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.push.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'app.push.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'pr_media') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.push.create'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'pr_media'), (SELECT id FROM permissions WHERE code = N'app.push.create'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.device.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'app.device.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.release.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'app.release.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.layout.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'app.layout.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.push.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'app.push.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.device.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'app.device.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.config.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'app.config.view'), N'all');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'app.diagnostic.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'app.diagnostic.view'), N'all');
GO

-- ── 19. faq_categories：規劃書 3.12 十個主題（不帶 club_id，全站共用主檔） ──────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'join-team';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'238fd9b0-66bc-5fd0-bfa8-9d75f3df748f';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'join-team', 0);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'加入球隊');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Join the Club');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'academy-admission';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0711a517-07d2-5464-98ab-46763e2af2a4';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'academy-admission', 1);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'學院招生');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Academy Admission');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'programs-camps';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'23236273-db7c-51a5-b7be-adc56f625f92';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'programs-camps', 2);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'課程與營隊報名');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Programs & Camp Registration');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'fees-refunds';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'95e5744f-73a2-55f4-b8ed-f9d52bc541af';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'fees-refunds', 3);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'費用與退費');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Fees & Refunds');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'trials';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'22741634-ce7a-5c98-b2fb-fb59fd167567';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'trials', 4);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'試訓');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Trials');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'international';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cdd031b7-2a45-555a-a272-469f8753bcad';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'international', 5);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'國際發展與海外球員');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'International Development & Overseas Players');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'womens-football';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b1684f77-c4a1-56de-89f6-bfc305b12a14';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'womens-football', 6);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'女子足球');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Women''s Football');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'fan-club-merchandise';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c80fee1a-a5b3-54e6-ae77-7056a8f35c8f';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'fan-club-merchandise', 7);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'球迷會與商品');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Fan Club & Merchandise');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'partnerships-sponsorship';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'653bfe64-630d-5c7c-bdbb-6c60e6d06c0e';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'partnerships-sponsorship', 8);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'合作與贊助');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Partnerships & Sponsorship');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = N'other';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ff6a6833-38c0-5db1-89b6-7fd2be3b8d09';
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, N'other', 9);
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', N'其他');
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', N'Other');
  COMMIT TRANSACTION;
END
GO

-- ── 20. home_sections：規劃書 3.1 首頁九大區塊，兩俱樂部各種一份 ───────────────
IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'hero')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'5ce59fec-ab95-573c-a4fb-e7ef77378bcf', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'hero', 1, 0);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'core_values')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'640e7881-37ca-5311-aa79-6369bcaf8183', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'core_values', 1, 1);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'ecosystem_nav')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'b3d960d5-8958-527d-84c3-4de721086386', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'ecosystem_nav', 1, 2);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'upcoming_match')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'9ed59806-96de-5a23-ace0-8b1ab2708ed0', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'upcoming_match', 1, 3);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'recent_fixtures')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'537ad6ff-d990-5f98-9cbe-83825fd58e4b', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'recent_fixtures', 1, 4);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'latest_news')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'6a95f155-a20e-56da-9ec3-e7660386f289', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'latest_news', 1, 5);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'partner_logos')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'f332e315-bc97-57b0-805e-eb55e6c9913b', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'partner_logos', 1, 6);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'shop_entry')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'521d8386-5551-52d3-af9b-b68ea1f193d7', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop_entry', 1, 7);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND section_code = N'bottom_cta')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'bbf43229-b698-52d3-a4ff-61eaf4b4c094', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'bottom_cta', 1, 8);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'hero')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'4e558c7a-d08a-50a9-b73c-685fcc084344', (SELECT id FROM clubs WHERE code = N'bw'), N'hero', 1, 0);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'core_values')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'50517eea-07ef-5d9b-b6c0-0f4b8cfcf939', (SELECT id FROM clubs WHERE code = N'bw'), N'core_values', 1, 1);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'ecosystem_nav')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'5f28c2f5-1cd0-5fc0-9374-412e5a969a7a', (SELECT id FROM clubs WHERE code = N'bw'), N'ecosystem_nav', 1, 2);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'upcoming_match')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'fc0ab38c-0af0-5da3-a791-bdca9ab7fcc9', (SELECT id FROM clubs WHERE code = N'bw'), N'upcoming_match', 1, 3);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'recent_fixtures')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'f8d89d9a-69cf-5f24-a324-61ab05ac45f7', (SELECT id FROM clubs WHERE code = N'bw'), N'recent_fixtures', 1, 4);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'latest_news')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'1575007a-b06e-531b-8df2-061426ef52c4', (SELECT id FROM clubs WHERE code = N'bw'), N'latest_news', 1, 5);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'partner_logos')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'97e9e9a0-b866-57bb-aa09-8292879e906d', (SELECT id FROM clubs WHERE code = N'bw'), N'partner_logos', 1, 6);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'shop_entry')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'ac4ba76d-5e32-5dc9-8f08-45124b88759b', (SELECT id FROM clubs WHERE code = N'bw'), N'shop_entry', 1, 7);
GO

IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND section_code = N'bottom_cta')
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES (N'349596b1-8014-522f-b4ee-93815c33813c', (SELECT id FROM clubs WHERE code = N'bw'), N'bottom_cta', 1, 8);
GO

-- ── 21. faq_embed_slots：G-12 掛載點字典四筆（不帶 club_id，全站共用） ──────────
IF NOT EXISTS (SELECT 1 FROM faq_embed_slots WHERE code = N'academy_admission')
  INSERT INTO faq_embed_slots (id, code, name) VALUES (N'c9456814-1006-5285-8e20-dc1a4b3cfa80', N'academy_admission', N'學院招生頁（4.7）');
GO

IF NOT EXISTS (SELECT 1 FROM faq_embed_slots WHERE code = N'program_detail')
  INSERT INTO faq_embed_slots (id, code, name) VALUES (N'15058d05-492b-50cb-b7fa-fd1a303a48e5', N'program_detail', N'課程詳情頁（5.x 各課程）');
GO

IF NOT EXISTS (SELECT 1 FROM faq_embed_slots WHERE code = N'trials')
  INSERT INTO faq_embed_slots (id, code, name) VALUES (N'dfea6f84-2dfa-56cd-87cc-d49c2e4e595d', N'trials', N'試訓頁（3.3）');
GO

IF NOT EXISTS (SELECT 1 FROM faq_embed_slots WHERE code = N'sponsorship')
  INSERT INTO faq_embed_slots (id, code, name) VALUES (N'df78b64a-3085-5bb7-a9b7-2e63c79c8471', N'sponsorship', N'贊助頁（9.4）');
GO

-- ── 22. forms／form_fields／form_fields_i18n：9 個固定表單目錄 ＋ 預設欄位，兩俱樂部各種一份 ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3ec9156c-1fc3-5c7e-a62c-302db9694399';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'join_player', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'ce50c419-8241-51b4-bfa4-198f1012faa5', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player'), N'name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'birth_date')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'a2619960-db28-5c6c-9034-02b69882e40f', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player'), N'birth_date', N'date', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'birth_date') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'birth_date'), N'zh-Hant', N'生日');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'birth_date') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'birth_date'), N'en', N'Date of Birth', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'position')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'43728391-d5c6-561d-b90e-0d2e6112d086', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player'), N'position', N'text', 0, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'position') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'position'), N'zh-Hant', N'場上位置');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'position') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'position'), N'en', N'Playing Position', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'experience')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'139648c3-f4c7-54eb-8e49-ff72e30dc66d', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player'), N'experience', N'textarea', 0, NULL, NULL, 1, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'experience') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'experience'), N'zh-Hant', N'足球經歷');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'experience') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'experience'), N'en', N'Football Experience', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'video_url')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'48d48340-a2db-53d8-be36-e5e8b7bf71a8', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player'), N'video_url', N'text', 0, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'video_url') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'video_url'), N'zh-Hant', N'影片連結');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'video_url') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'video_url'), N'en', N'Video Link', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'8b44b672-d6b3-557c-a9d5-1b5f2e65b1fd', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player'), N'contact', N'text', 1, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'4a1b3a60-4291-588b-8f1b-e92d4a86feab', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 6);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'join_player') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7e4d0b46-aff7-5d0d-b113-d602aed7d521';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'academy_children_training', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'43f78941-69c2-55ea-a51d-fb1794082b64', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training'), N'enrollment_category', N'select', 1, NULL, N'["學院 U12", "學院 U14", "學院 U15", "兒童混齡班", "兒童初學班", "兒童技巧發展班", "專項訓練"]', 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category'), N'zh-Hant', N'報名項目');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category'), N'en', N'Enrollment Category', N'["Academy U12", "Academy U14", "Academy U15", "Children Mixed-Age Class", "Children Beginner Class", "Children Skill Development Class", "Specialist Training"]');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'ae087f41-410f-541d-8e6f-8c2d177d9a7c', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training'), N'name', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'name'), N'zh-Hant', N'學員姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'name'), N'en', N'Student Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'birth_date')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'990195f8-3bf5-5bc8-b7c1-b81a7e7a54f8', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training'), N'birth_date', N'date', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'birth_date') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'birth_date'), N'zh-Hant', N'學員生日');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'birth_date') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'birth_date'), N'en', N'Student Date of Birth', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'location_preference')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'6cf58781-a157-571a-adfe-9e5b1bfa9838', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training'), N'location_preference', N'text', 0, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'location_preference') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'location_preference'), N'zh-Hant', N'地點偏好');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'location_preference') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'location_preference'), N'en', N'Preferred Location', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'ad4cc56e-bae3-58a9-9c34-a103bec0e2ff', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training'), N'contact', N'text', 1, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'contact'), N'zh-Hant', N'家長聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'contact'), N'en', N'Parent Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'experience')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'42179845-2ef6-5dc6-a439-1450e70291f7', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training'), N'experience', N'textarea', 0, NULL, NULL, 1, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'experience') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'experience'), N'zh-Hant', N'足球經歷');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'experience') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'experience'), N'en', N'Football Experience', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'health_status')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'0264a309-8cf5-5959-8f8d-4978a74b16ac', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training'), N'health_status', N'textarea', 0, NULL, NULL, 0, 6);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'health_status') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'health_status'), N'zh-Hant', N'健康狀況');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'health_status') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'health_status'), N'en', N'Health Status', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'c5bbba2d-003b-5f2e-a590-728474d745c8', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 7);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bc5cc962-bf47-56fe-82b9-64ce0a4ae341';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'camp_registration', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'session_choice')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'705dde2d-64d5-56cc-9952-754e534a2001', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration'), N'session_choice', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'session_choice') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'session_choice'), N'zh-Hant', N'營隊梯次');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'session_choice') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'session_choice'), N'en', N'Camp Session', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'c7b35ca9-595d-5609-9087-a4ce0dfaa07e', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration'), N'name', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'name'), N'zh-Hant', N'學員姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'name'), N'en', N'Student Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'birth_date')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'04e1f0b6-98b4-566e-bceb-48968b729237', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration'), N'birth_date', N'date', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'birth_date') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'birth_date'), N'zh-Hant', N'學員生日');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'birth_date') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'birth_date'), N'en', N'Student Date of Birth', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'health_declaration')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'b4403659-dc21-5ae3-b941-74cd2e2a4576', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration'), N'health_declaration', N'consent', 1, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'health_declaration') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'health_declaration'), N'zh-Hant', N'健康聲明');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'health_declaration') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'health_declaration'), N'en', N'Health Declaration', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'd4b861b5-7772-5a84-97e5-fb082797d13d', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration'), N'contact', N'text', 1, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'contact'), N'zh-Hant', N'緊急聯絡人');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'contact'), N'en', N'Emergency Contact', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'b0060680-c45e-5e54-bbef-ae2bab6c561e', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'camp_registration') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e6033880-e733-53c6-b282-ade8c2912685';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'international_player_enquiry', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'145143ce-4a3e-5c91-bcca-35e2362b2ac9', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry'), N'name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'nationality')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'8bc711ab-409d-58c6-967c-f8053281f1ff', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry'), N'nationality', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'nationality') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'nationality'), N'zh-Hant', N'國籍');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'nationality') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'nationality'), N'en', N'Nationality', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'78527107-0101-5d45-9cb8-14ab9a6b5633', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry'), N'passport_no', N'text', 0, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no'), N'zh-Hant', N'護照號碼');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no'), N'en', N'Passport Number', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'experience')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'f01d773f-747f-56fa-9d18-55b1b392d5ff', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry'), N'experience', N'textarea', 0, NULL, NULL, 1, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'experience') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'experience'), N'zh-Hant', N'足球經歷');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'experience') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'experience'), N'en', N'Football Experience', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'video_url')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'63664464-944c-56bd-832d-35076d842a74', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry'), N'video_url', N'text', 0, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'video_url') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'video_url'), N'zh-Hant', N'影片連結');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'video_url') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'video_url'), N'en', N'Video Link', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'10ef0723-6ddf-57e7-af7a-fbe7dbbb282b', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry'), N'visa_status', N'text', 0, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status'), N'zh-Hant', N'簽證狀態');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status'), N'en', N'Visa Status', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'7fa49f6a-e785-551d-b298-50bc4533b658', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry'), N'contact', N'text', 1, NULL, NULL, 0, 6);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'0c08a5a3-5e6f-5306-b0cc-c2a650447dd7', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 7);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7cc64627-9d1d-5b94-87db-3ce0c2df4e97';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'partnership_sponsorship', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'6a6c4c17-bd05-589a-b4a0-8cea5f192723', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'enquiry_type', N'select', 1, NULL, N'["合作夥伴", "贊助", "兩者"]', 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type'), N'zh-Hant', N'洽詢類型');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type'), N'en', N'Enquiry Type', N'["Partnership", "Sponsorship", "Both"]');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'company')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'382663cc-9465-5929-b938-3acb7f3c5265', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'company', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'company') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'company'), N'zh-Hant', N'公司名稱');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'company') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'company'), N'en', N'Company Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'industry')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'16d2103d-3d68-5d72-b12b-addb26ba3bbc', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'industry', N'text', 0, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'industry') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'industry'), N'zh-Hant', N'產業別');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'industry') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'industry'), N'en', N'Industry', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'89205a0d-aa57-50e3-a1a6-86dae0b5c51b', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'budget_range', N'text', 0, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range'), N'zh-Hant', N'預算區間');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range'), N'en', N'Budget Range', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'87ec282b-ed9e-51af-80ff-adbb6d739403', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'cooperation_direction', N'textarea', 0, NULL, NULL, 1, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction'), N'zh-Hant', N'合作方向');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction'), N'en', N'Cooperation Direction', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'091456ff-db1e-56a4-93fb-9d201df73049', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'sponsorship_interest', N'text', 0, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest'), N'zh-Hant', N'感興趣的贊助方案');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest'), N'en', N'Sponsorship Package of Interest', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'6f1b7fb6-ebe6-5666-b9b5-450bca9c0349', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'name', N'text', 1, NULL, NULL, 0, 6);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'name'), N'zh-Hant', N'聯絡人姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'name'), N'en', N'Contact Person', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'86f36479-9812-5371-b52b-07554bce6274', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'contact', N'text', 1, NULL, NULL, 0, 7);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'2c90640d-04aa-555e-ac10-81e971dc417d', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 8);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e121835c-8722-593e-841c-bf6fa1dccf32';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'media_enquiry', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'media_name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'4906550f-0fca-59b0-8afc-3d74b02537d2', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry'), N'media_name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'media_name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'media_name'), N'zh-Hant', N'媒體名稱');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'media_name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'media_name'), N'en', N'Media Outlet', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'8fc01a4f-a6f6-5124-a261-fbe278171b58', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry'), N'name', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'name'), N'zh-Hant', N'記者姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'name'), N'en', N'Reporter Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'topic')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'9a151104-f339-52a5-9916-032bc93b5ffa', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry'), N'topic', N'text', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'topic') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'topic'), N'zh-Hant', N'採訪主題');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'topic') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'topic'), N'en', N'Interview Topic', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'deadline')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'a9973152-6640-5ec7-8bc3-df8c5ab58989', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry'), N'deadline', N'date', 0, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'deadline') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'deadline'), N'zh-Hant', N'截稿日');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'deadline') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'deadline'), N'en', N'Deadline', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'0004fa93-785f-5307-81e7-9fa18f53d587', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry'), N'contact', N'text', 1, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'46313d22-5a9a-51ce-b7ed-2499a8155d36', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cfb23981-fd35-549c-8721-e9f68bd9f04d';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'general_contact', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'51589e03-9004-5fa9-a2b7-6e4596503414', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact'), N'name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'b4de604f-112d-5ade-87b7-67aac3c6c377', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact'), N'contact', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'contact'), N'zh-Hant', N'Email');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'contact'), N'en', N'Email', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'subject')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'5ba3bcd5-ecc4-53e9-bb47-26efd0b9c3af', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact'), N'subject', N'text', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'subject') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'subject'), N'zh-Hant', N'主旨');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'subject') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'subject'), N'en', N'Subject', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'message')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'a8604d09-7366-538d-b65c-0aa8edd4f190', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact'), N'message', N'textarea', 1, NULL, NULL, 1, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'message') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'message'), N'zh-Hant', N'內容');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'message') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'message'), N'en', N'Message', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'dc27acd0-596a-5e1f-bec3-d2a24effcfd2', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'general_contact') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ace2b314-fe62-5282-95e6-b56edf664c6b';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'proposal_download', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'company')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'6490b8b8-5325-5956-9103-79bdf249346a', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download'), N'company', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'company') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'company'), N'zh-Hant', N'公司名稱');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'company') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'company'), N'en', N'Company Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'4f872b96-bdd0-55e9-af27-b7a3c6e5cbd1', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download'), N'name', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'1852c580-7103-5d3b-a5a3-c4ccbef07cd0', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download'), N'contact', N'text', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'contact'), N'zh-Hant', N'Email');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'contact'), N'en', N'Email', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'edeaea5c-f4f4-5013-a7cd-dc5fdd6ffa1b', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'proposal_download') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bfd41c29-46ed-5034-b0b1-9123e5847ccb';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'donation_enquiry', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'c01b2416-d726-5121-ad74-27dd3305a48f', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry'), N'name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'797bc24e-5476-5eb9-8a63-f10d79cf1137', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry'), N'contact', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'message')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'084bae4b-b7bc-5097-ae80-e49f1db92f83', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry'), N'message', N'textarea', 0, NULL, NULL, 1, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'message') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'message'), N'zh-Hant', N'洽詢內容');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'message') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'message'), N'en', N'Enquiry Message', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'e8935016-1cec-5681-bf92-43d3a0dc667a', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd51bc209-09fc-5dc7-bbf8-77af9ff29b52';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'join_player', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'347157e5-e4e6-5e11-9457-7eac4709679e', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player'), N'name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'birth_date')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'549ef9a7-f888-55a2-8054-707255b05d82', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player'), N'birth_date', N'date', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'birth_date') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'birth_date'), N'zh-Hant', N'生日');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'birth_date') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'birth_date'), N'en', N'Date of Birth', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'position')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'32463a0f-34d0-5eb1-ae79-63b4e4fae177', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player'), N'position', N'text', 0, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'position') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'position'), N'zh-Hant', N'場上位置');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'position') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'position'), N'en', N'Playing Position', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'experience')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'e7da8279-df9c-5adf-b2fd-03089b17786e', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player'), N'experience', N'textarea', 0, NULL, NULL, 1, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'experience') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'experience'), N'zh-Hant', N'足球經歷');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'experience') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'experience'), N'en', N'Football Experience', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'video_url')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'a2b64224-2678-5d77-9eff-c363a033208e', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player'), N'video_url', N'text', 0, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'video_url') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'video_url'), N'zh-Hant', N'影片連結');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'video_url') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'video_url'), N'en', N'Video Link', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'5341a094-0061-5fe6-859c-74bd15ae65ea', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player'), N'contact', N'text', 1, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'bd0cd10d-89aa-52b2-a204-b28de3655dd5', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 6);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'join_player') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ee8cba5b-65f5-5050-9cb6-cb246cc1f1af';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'academy_children_training', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'6f1fb28f-37ed-5a98-ad35-744a62abc245', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training'), N'enrollment_category', N'select', 1, NULL, N'["學院 U12", "學院 U14", "學院 U15", "兒童混齡班", "兒童初學班", "兒童技巧發展班", "專項訓練"]', 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category'), N'zh-Hant', N'報名項目');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'enrollment_category'), N'en', N'Enrollment Category', N'["Academy U12", "Academy U14", "Academy U15", "Children Mixed-Age Class", "Children Beginner Class", "Children Skill Development Class", "Specialist Training"]');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'481bfea5-b33c-5543-ac2d-7fb8b221823b', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training'), N'name', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'name'), N'zh-Hant', N'學員姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'name'), N'en', N'Student Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'birth_date')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'35f6c7cc-96fa-5731-a379-d90b6ad0578c', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training'), N'birth_date', N'date', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'birth_date') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'birth_date'), N'zh-Hant', N'學員生日');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'birth_date') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'birth_date'), N'en', N'Student Date of Birth', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'location_preference')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'4ca429af-0d57-5496-a44c-cf9c0bde3446', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training'), N'location_preference', N'text', 0, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'location_preference') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'location_preference'), N'zh-Hant', N'地點偏好');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'location_preference') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'location_preference'), N'en', N'Preferred Location', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'cd5438b3-5555-55ca-a865-4e0900c664a6', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training'), N'contact', N'text', 1, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'contact'), N'zh-Hant', N'家長聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'contact'), N'en', N'Parent Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'experience')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'9d4b921b-6769-5884-9d45-e60ff2474a46', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training'), N'experience', N'textarea', 0, NULL, NULL, 1, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'experience') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'experience'), N'zh-Hant', N'足球經歷');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'experience') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'experience'), N'en', N'Football Experience', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'health_status')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'135471b3-b434-5a74-be47-268384329676', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training'), N'health_status', N'textarea', 0, NULL, NULL, 0, 6);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'health_status') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'health_status'), N'zh-Hant', N'健康狀況');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'health_status') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'health_status'), N'en', N'Health Status', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'35618c5a-83ed-570b-b13c-11426dd51550', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 7);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'academy_children_training') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4627757b-0e78-5337-8f58-343361e28aae';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'camp_registration', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'session_choice')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'a94add4c-2772-50ae-99c4-3bf4405de19a', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration'), N'session_choice', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'session_choice') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'session_choice'), N'zh-Hant', N'營隊梯次');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'session_choice') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'session_choice'), N'en', N'Camp Session', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'c1ff5518-9cad-5263-b48e-8e48c8fb647b', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration'), N'name', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'name'), N'zh-Hant', N'學員姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'name'), N'en', N'Student Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'birth_date')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'ab3697b7-9f65-5a15-b41a-2791c1e18f0f', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration'), N'birth_date', N'date', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'birth_date') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'birth_date'), N'zh-Hant', N'學員生日');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'birth_date') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'birth_date'), N'en', N'Student Date of Birth', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'health_declaration')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'61557c4a-1507-57b5-87d3-cc7b9f50175a', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration'), N'health_declaration', N'consent', 1, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'health_declaration') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'health_declaration'), N'zh-Hant', N'健康聲明');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'health_declaration') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'health_declaration'), N'en', N'Health Declaration', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'bd696ff2-00a5-57e3-b383-018ccb13ea04', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration'), N'contact', N'text', 1, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'contact'), N'zh-Hant', N'緊急聯絡人');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'contact'), N'en', N'Emergency Contact', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'fcae1145-006d-51f3-999c-15832adbbc1f', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'camp_registration') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8fb22cf8-b384-5cc7-a31f-f7a0ab174ca3';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'international_player_enquiry', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'd77baf0d-7fc6-5b27-8a1a-202244126e09', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry'), N'name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'nationality')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'4f5dac9d-0ffb-5e3e-86e7-ea133f263e79', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry'), N'nationality', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'nationality') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'nationality'), N'zh-Hant', N'國籍');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'nationality') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'nationality'), N'en', N'Nationality', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'61cc7bd4-6d6e-51dd-96f9-ddbc9fc4ab47', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry'), N'passport_no', N'text', 0, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no'), N'zh-Hant', N'護照號碼');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'passport_no'), N'en', N'Passport Number', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'experience')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'74673a44-d8ea-581f-95df-39ebe1684b3e', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry'), N'experience', N'textarea', 0, NULL, NULL, 1, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'experience') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'experience'), N'zh-Hant', N'足球經歷');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'experience') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'experience'), N'en', N'Football Experience', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'video_url')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'07587ce8-e203-543d-ac58-58b83ec61e7e', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry'), N'video_url', N'text', 0, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'video_url') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'video_url'), N'zh-Hant', N'影片連結');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'video_url') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'video_url'), N'en', N'Video Link', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'3328fe40-55ee-55b2-bab4-9b2fd580e1cf', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry'), N'visa_status', N'text', 0, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status'), N'zh-Hant', N'簽證狀態');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'visa_status'), N'en', N'Visa Status', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'637e9985-0037-5c26-beb0-3e03a7e34b7c', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry'), N'contact', N'text', 1, NULL, NULL, 0, 6);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'7e32ca2a-bda9-5ba7-8154-25a4980d1e89', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 7);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'international_player_enquiry') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4fcc036d-7e48-557b-b796-6ec99eb859c0';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'partnership_sponsorship', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'b197018d-aad4-573a-acef-8c5751001ade', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'enquiry_type', N'select', 1, NULL, N'["合作夥伴", "贊助", "兩者"]', 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type'), N'zh-Hant', N'洽詢類型');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'enquiry_type'), N'en', N'Enquiry Type', N'["Partnership", "Sponsorship", "Both"]');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'company')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'db2d8c0a-6c58-5181-bca3-f1f7e5244ac6', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'company', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'company') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'company'), N'zh-Hant', N'公司名稱');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'company') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'company'), N'en', N'Company Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'industry')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'99c2e20e-3d80-5c16-9587-af7449eade47', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'industry', N'text', 0, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'industry') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'industry'), N'zh-Hant', N'產業別');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'industry') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'industry'), N'en', N'Industry', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'38bbf388-1709-5986-a499-c22e4a57d6ed', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'budget_range', N'text', 0, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range'), N'zh-Hant', N'預算區間');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'budget_range'), N'en', N'Budget Range', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'52505441-7e73-582a-bcf9-4850321795fa', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'cooperation_direction', N'textarea', 0, NULL, NULL, 1, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction'), N'zh-Hant', N'合作方向');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'cooperation_direction'), N'en', N'Cooperation Direction', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'5ca21d8d-2323-57b1-8559-a00ee18c20fe', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'sponsorship_interest', N'text', 0, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest'), N'zh-Hant', N'感興趣的贊助方案');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'sponsorship_interest'), N'en', N'Sponsorship Package of Interest', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'bd6fc9f2-9108-5593-8a3b-ddb0b24900c9', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'name', N'text', 1, NULL, NULL, 0, 6);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'name'), N'zh-Hant', N'聯絡人姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'name'), N'en', N'Contact Person', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'a964e0c8-2dd6-5f3d-a3b1-e76456065989', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'contact', N'text', 1, NULL, NULL, 0, 7);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'7bec3f63-ced1-59f9-84aa-7cd10ccaddc4', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 8);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'partnership_sponsorship') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f4781838-40fa-54db-a48b-7e0551f64ee9';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'media_enquiry', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'media_name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'2cb78f6a-d945-5e51-a2f4-3a345f5c6273', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry'), N'media_name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'media_name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'media_name'), N'zh-Hant', N'媒體名稱');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'media_name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'media_name'), N'en', N'Media Outlet', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'98913810-a162-5860-991f-6ae4f8c7a0a4', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry'), N'name', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'name'), N'zh-Hant', N'記者姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'name'), N'en', N'Reporter Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'topic')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'f982adef-85f2-5a50-9bc5-4a099a880414', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry'), N'topic', N'text', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'topic') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'topic'), N'zh-Hant', N'採訪主題');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'topic') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'topic'), N'en', N'Interview Topic', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'deadline')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'6d8d8c6a-3c40-5329-82e3-992d796d7bfc', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry'), N'deadline', N'date', 0, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'deadline') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'deadline'), N'zh-Hant', N'截稿日');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'deadline') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'deadline'), N'en', N'Deadline', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'cb73c17a-1e5b-5a34-b650-3c553073a985', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry'), N'contact', N'text', 1, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'ee5ea490-c3ff-5f52-94c6-223baf0eef6d', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 5);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'media_enquiry') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'509924a7-a540-513d-a4d8-e809fd36ab98';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'general_contact', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'279c8cab-a2eb-5ee5-b343-08968d9dcfca', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact'), N'name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'f8b54a63-9a6a-5c2a-9481-fb70cfd8c280', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact'), N'contact', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'contact'), N'zh-Hant', N'Email');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'contact'), N'en', N'Email', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'subject')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'7aaae9f9-ad7c-5ea5-8edd-938091627e2d', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact'), N'subject', N'text', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'subject') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'subject'), N'zh-Hant', N'主旨');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'subject') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'subject'), N'en', N'Subject', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'message')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'3c1b78df-e0fa-5796-a8d3-8e836b143707', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact'), N'message', N'textarea', 1, NULL, NULL, 1, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'message') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'message'), N'zh-Hant', N'內容');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'message') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'message'), N'en', N'Message', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'f8fcebcd-81f7-5dd3-ac3a-325dfb8087ad', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 4);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'general_contact') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd4b59a32-88af-5f10-889f-a91d802775da';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'proposal_download', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'company')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'65301457-2084-5eda-9641-7eb84740a0ae', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download'), N'company', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'company') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'company'), N'zh-Hant', N'公司名稱');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'company') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'company'), N'en', N'Company Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'23f29515-a0c0-556c-ba60-99d1afc6bada', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download'), N'name', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'35f63d45-2dc5-5223-81a6-0fca239d774a', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download'), N'contact', N'text', 1, NULL, NULL, 0, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'contact'), N'zh-Hant', N'Email');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'contact'), N'en', N'Email', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'6b42d82a-d045-5150-b4bb-b17bf229bb56', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'proposal_download') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'97870eb1-1cf7-5001-95e4-e748cee92c3b';
  INSERT INTO forms (id, club_id, form_code, captcha_enabled)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'donation_enquiry', 1);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'name')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'a99de6f1-4dcf-5fe3-8fce-0dce728233b8', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry'), N'name', N'text', 1, NULL, NULL, 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'name') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'name'), N'zh-Hant', N'姓名');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'name') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'name'), N'en', N'Name', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'contact')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'9352b60f-ab18-5599-b71b-2ba4db46519d', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry'), N'contact', N'text', 1, NULL, NULL, 0, 1);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'contact') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'contact'), N'zh-Hant', N'聯絡方式');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'contact') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'contact'), N'en', N'Contact Info', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'message')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'206d13a8-8a42-5ebd-986a-e5f68fda9e4d', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry'), N'message', N'textarea', 0, NULL, NULL, 1, 2);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'message') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'message'), N'zh-Hant', N'洽詢內容');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'message') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'message'), N'en', N'Enquiry Message', NULL);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent')
  INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order)
  VALUES (N'cd27576a-59b5-535b-8d5c-91eb2f291368', (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry'), N'privacy_consent', N'consent', 1, NULL, NULL, 0, 3);
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent') AND locale = N'zh-Hant')
  INSERT INTO form_fields_i18n (form_field_id, locale, label)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent'), N'zh-Hant', N'我已閱讀並同意本俱樂部依隱私權政策蒐集、處理及使用我的個人資料');
GO

IF NOT EXISTS (SELECT 1 FROM form_fields_i18n WHERE form_field_id = (SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent') AND locale = N'en')
  INSERT INTO form_fields_i18n (form_field_id, locale, label, options_json)
  VALUES ((SELECT id FROM form_fields WHERE form_id = (SELECT id FROM forms WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND form_code = N'donation_enquiry') AND field_key = N'privacy_consent'), N'en', N'I have read and agree to the club''s collection, processing, and use of my personal data in accordance with its privacy policy', NULL);
GO

-- ── 23. event_types：L2 自建事件六個起始分類（不帶 club_id，全站共用） ────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM event_types WHERE code = N'press_conference';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'815eb264-ca28-5c45-8523-5723ff29d862';
  INSERT INTO event_types (id, code, colour, icon, is_public, sort_order)
  VALUES (@id, N'press_conference', N'#B91C1C', N'megaphone', 1, 0);
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'zh-Hant', N'記者會');
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'en', N'Press Conference');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM event_types WHERE code = N'autograph_session';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b4f4245e-d308-515f-b079-9351140b3fd4';
  INSERT INTO event_types (id, code, colour, icon, is_public, sort_order)
  VALUES (@id, N'autograph_session', N'#B45309', N'pen-line', 1, 1);
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'zh-Hant', N'簽名會');
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'en', N'Autograph Session');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM event_types WHERE code = N'fan_meet';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b16a9745-be98-5508-917d-4f8cc564124d';
  INSERT INTO event_types (id, code, colour, icon, is_public, sort_order)
  VALUES (@id, N'fan_meet', N'#0369A1', N'users', 1, 2);
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'zh-Hant', N'球迷見面會');
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'en', N'Fan Meet');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM event_types WHERE code = N'open_training';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'58b1cc3e-41d6-5686-9762-45c9a75e0664';
  INSERT INTO event_types (id, code, colour, icon, is_public, sort_order)
  VALUES (@id, N'open_training', N'#15803D', N'whistle', 1, 3);
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'zh-Hant', N'公開訓練');
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'en', N'Open Training');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM event_types WHERE code = N'closure_notice';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'900ae9cd-18b7-52f9-8f7a-90d2b2fffe52';
  INSERT INTO event_types (id, code, colour, icon, is_public, sort_order)
  VALUES (@id, N'closure_notice', N'#525252', N'alert-circle', 1, 4);
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'zh-Hant', N'休館公告');
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'en', N'Facility Closure Notice');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM event_types WHERE code = N'other';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6266cbab-a770-5a17-8c80-d65150fc0cdd';
  INSERT INTO event_types (id, code, colour, icon, is_public, sort_order)
  VALUES (@id, N'other', N'#6D28D9', N'calendar', 1, 5);
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'zh-Hant', N'其他');
  INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@id, N'en', N'Other');
  COMMIT TRANSACTION;
END
GO
