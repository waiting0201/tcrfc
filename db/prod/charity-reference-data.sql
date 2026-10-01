-- ============================================================================
-- TCRFC 慈善庫（tcrfc_charity）正式庫首次初始化：參照資料
-- 自動產生：python3 db/seed/generate-prod-reference-sql.py（請勿手動編輯；改定義請改原產生器後重新產生）
-- 來源產生器：db/seed/generate-charity-seed-sql.py
-- 🔴 這是「參照資料」不是種子資料：只含系統運作必需的列，沒有任何測試內容與測試帳號。
-- 冪等：每個實體以業務自然鍵判斷（IF NOT EXISTS 才 INSERT），但 deploy/prod-db-init.sh 仍只在空庫上執行。
-- 區段（允許清單）：
--   0     locales
--   1     admin_roles：沿用主站九個角色
--   2     permissions：N1–N7 權限碼
--   3     role_permissions
--   3b    CH-3 新增的 API 專用權限碼（重新向金流確認付款結果）
-- 預期列數（deploy/prod-db-init.sh 灌完後逐表核對；`-- MANIFEST` 行是機器讀的）：
-- MANIFEST admin_roles=9
-- MANIFEST locales=2
-- MANIFEST permissions=24
-- MANIFEST role_permissions=45
-- ============================================================================

SET XACT_ABORT ON;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── 0. locales：zh-Hant（預設）＋ en ──────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM locales WHERE code = N'zh-Hant')
  INSERT INTO locales (code, name_zh, name_en, is_default, sort_order) VALUES (N'zh-Hant', N'繁體中文', N'Traditional Chinese', 1, 0);
IF NOT EXISTS (SELECT 1 FROM locales WHERE code = N'en')
  INSERT INTO locales (code, name_zh, name_en, is_default, sort_order) VALUES (N'en', N'英文', N'English', 0, 1);
GO

-- ── 1. admin_roles：沿用主站九個角色（docs/16 §5：本庫不新增第十個「合作球隊管理」） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'system_admin';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'18164663-3778-54db-8667-af62d8a5079b';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'system_admin', N'系統管理員', N'System Administrator', 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'content_editor';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c10d0dae-5ebf-508f-8a94-7298b11846c3';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'content_editor', N'內容編輯', N'Content Editor', 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'team_competition';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8e580439-4d91-537e-bce5-524c142ab990';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'team_competition', N'競技／球隊管理', N'Team & Competition Manager', 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'academy_program';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd85cd9c3-5d4f-5fea-80f5-4d9a41ab4c4c';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'academy_program', N'學院／課程管理', N'Academy & Program Manager', 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'business_sponsorship';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ff08d8bf-eca9-5f82-ab52-628e171bc77e';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'business_sponsorship', N'商務／贊助', N'Business & Sponsorship', 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'pr_media';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4ea8034f-7e68-58f7-b703-7f9fc19840cb';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'pr_media', N'公關／媒體', N'PR & Media', 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'customer_service_admin';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0ff8795a-b2cb-5427-b0b4-865e774e43fe';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'customer_service_admin', N'客服／行政', N'Customer Service & Admin', 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'translator';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'940823c7-c41f-5ea9-be26-b004d8c9a123';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'translator', N'翻譯人員', N'Translator', 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = N'viewer';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ee168395-6c30-53a0-87d9-28c368f239a0';
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, N'viewer', N'檢視者', N'Viewer', 1);
  COMMIT TRANSACTION;
END
GO

-- ── 2. permissions：N1–N7 共 23 筆（domain 值域為本次工程判斷，見上方註解） ──────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n1.donation_store.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'78186cdd-d7aa-5c6c-8142-392f7bbfe76a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n1.donation_store.view', N'N', N'N1', N'store', N'view', N'檢視捐款店家', N'View Donation Stores', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n1.donation_store.manage';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9c80a9e0-1fe4-5f25-8802-47ad598cef8a';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n1.donation_store.manage', N'N', N'N1', N'store', N'update', N'編輯捐款店家', N'Manage Donation Stores', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n1.donation_store.share_pct';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'32061608-5f8f-58fe-b4c2-87711ec2e155';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n1.donation_store.share_pct', N'N', N'N1', N'store', N'execute', N'設定店家分潤比例', N'Set Store Share %', 1, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n1.donation_store.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6bbb6281-f90e-510e-aa1c-f0324b88cc34';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n1.donation_store.export', N'N', N'N1', N'store', N'export', N'匯出店家與 QR 清單', N'Export Stores & QR', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n2.donation_project.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e580caf5-6410-5d32-89c7-dfebcbd621b9';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n2.donation_project.view', N'N', N'N2', N'project', N'view', N'檢視捐款項目', N'View Donation Projects', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n2.donation_project.manage';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3335a15c-b6c8-5b6e-a4ca-8e3811a2aa12';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n2.donation_project.manage', N'N', N'N2', N'project', N'update', N'編輯捐款項目', N'Manage Donation Projects', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n2.donation_project.publish';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1db2ee48-04d8-5ad0-85f6-8abd02a5eedc';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n2.donation_project.publish', N'N', N'N2', N'project', N'publish', N'上下架捐款項目', N'Publish Donation Projects', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n2.donation_project.share_pct';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'543c1fc0-8b91-5a90-8727-ca697333fc58';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n2.donation_project.share_pct', N'N', N'N2', N'project', N'execute', N'設定項目分潤比例', N'Set Project Share %', 1, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n3.donation.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'156bebbc-41a3-5379-8b2a-ccabb8db403f';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n3.donation.view', N'N', N'N3', N'donation', N'view', N'檢視捐款紀錄（遮罩）', N'View Donations (Masked)', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n3.donation.reveal';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ebd64e73-0b0d-5e40-b753-1fed5d9553be';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n3.donation.reveal', N'N', N'N3', N'donation', N'reveal', N'檢視捐款人完整個資', N'Reveal Donor PII', 1, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n3.donation.refund';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2cefab7d-8770-5b71-a13e-65a05622c2e3';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n3.donation.refund', N'N', N'N3', N'donation', N'execute', N'執行人工退款', N'Execute Manual Refund', 1, 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n3.donation.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5331ed41-d190-5280-8fe7-a29a352e21cf';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n3.donation.export', N'N', N'N3', N'donation', N'export', N'匯出含個資之捐款明細', N'Export Donation PII', 1, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n4.settlement.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'27fbe80e-629a-5c4b-9d76-92e71b299d78';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n4.settlement.view', N'N', N'N4', N'settlement', N'view', N'檢視結算單', N'View Settlements', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n4.settlement.execute';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'04b6e34f-1dd2-5c48-a986-09c5438d99e6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n4.settlement.execute', N'N', N'N4', N'settlement', N'execute', N'執行結算與登記匯款', N'Execute Settlement', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n4.settlement.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6072b1b5-26aa-5c75-9305-4f1962ba7f08';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n4.settlement.export', N'N', N'N4', N'settlement', N'export', N'匯出對帳單', N'Export Settlement Reports', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n5.donation_invoice.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'30fc64a8-003f-54da-b167-bfe4f49e63a6';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n5.donation_invoice.view', N'N', N'N5', N'invoice', N'view', N'檢視發票與收據', N'View Invoices', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n5.donation_invoice.issue';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2e4cf3c6-8c3c-5eb0-abb3-6128d16dade7';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n5.donation_invoice.issue', N'N', N'N5', N'invoice', N'execute', N'開立或補開發票', N'Issue Invoice', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n5.donation_invoice.void';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'948646d3-f81b-590a-ad00-7d2f765783a7';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n5.donation_invoice.void', N'N', N'N5', N'invoice', N'execute', N'作廢或折讓發票', N'Void / Allowance Invoice', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n6.report.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7c4adc6c-00b4-5986-a85f-9810eaee7669';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n6.report.view', N'N', N'N6', N'report', N'view', N'檢視捐款報表', N'View Donation Reports', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n6.report.export';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ba13dd9b-2f04-58f4-9845-a187c4e4e402';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n6.report.export', N'N', N'N6', N'report', N'export', N'匯出捐款報表', N'Export Donation Reports', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n7.setting.view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4d7c0153-12dc-5e01-b215-60fb1fa4653c';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n7.setting.view', N'N', N'N7', N'setting', N'view', N'檢視站台設定', N'View Site Settings', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n7.setting.manage';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bda8c4d6-ce42-5bd2-872a-a965847991a7';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n7.setting.manage', N'N', N'N7', N'setting', N'update', N'編輯站台設定與文案', N'Manage Site Settings', 0, 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = N'n7.payment_channel.manage';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'aadafc6b-ccf0-5b93-a5c7-2f4363cd6555';
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, N'n7.payment_channel.manage', N'N', N'N7', N'setting', N'update', N'管理金流與發票憑證', N'Manage Payment Channels', 1, 1);
  COMMIT TRANSACTION;
END
GO

-- ── 3. role_permissions：系統管理員全給；五個與慈善無對應職能的角色刻意不掛任何權限 ──
IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n1.donation_store.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n1.donation_store.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n1.donation_store.manage'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n1.donation_store.manage'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n1.donation_store.share_pct'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n1.donation_store.share_pct'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n1.donation_store.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n1.donation_store.export'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n2.donation_project.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n2.donation_project.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n2.donation_project.manage'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n2.donation_project.manage'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n2.donation_project.publish'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n2.donation_project.publish'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n2.donation_project.share_pct'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n2.donation_project.share_pct'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.reveal'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.reveal'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.refund'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.refund'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.export'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n4.settlement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n4.settlement.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n4.settlement.execute'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n4.settlement.execute'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n4.settlement.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n4.settlement.export'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.issue'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.issue'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.void'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.void'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n6.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n6.report.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n6.report.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n6.report.export'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n7.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n7.setting.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n7.setting.manage'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n7.setting.manage'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n7.payment_channel.manage'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n7.payment_channel.manage'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.reveal'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.reveal'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.export'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.export'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.issue'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.issue'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.void'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.void'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n6.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'n6.report.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'n1.donation_store.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'n1.donation_store.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'n1.donation_store.manage'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'n1.donation_store.manage'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'n2.donation_project.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'n2.donation_project.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'n2.donation_project.manage'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'n2.donation_project.manage'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'n2.donation_project.publish'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'n2.donation_project.publish'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'business_sponsorship') AND permission_id = (SELECT id FROM permissions WHERE code = N'n6.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'business_sponsorship'), (SELECT id FROM permissions WHERE code = N'n6.report.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'n1.donation_store.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'n1.donation_store.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'n2.donation_project.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'n2.donation_project.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'n3.donation.view'), N'masked');
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'n4.settlement.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'n4.settlement.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'n5.donation_invoice.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'n6.report.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'n6.report.view'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'viewer') AND permission_id = (SELECT id FROM permissions WHERE code = N'n7.setting.view'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'viewer'), (SELECT id FROM permissions WHERE code = N'n7.setting.view'), NULL);
GO

-- ── 3b. CH-3 新增的權限碼（API 專用，未併入匯出給前端的常數，理由見上方註解） ────────
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'n3.donation.recheck_payment')
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (N'1dea463c-865c-5faa-93e6-54c06c6abefb', N'n3.donation.recheck_payment', N'N', N'N3', N'donation', N'execute', N'重新確認付款結果', N'Recheck Payment Result', 0, 0);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'system_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.recheck_payment'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'system_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.recheck_payment'), NULL);
GO

IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = N'customer_service_admin') AND permission_id = (SELECT id FROM permissions WHERE code = N'n3.donation.recheck_payment'))
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ((SELECT id FROM admin_roles WHERE code = N'customer_service_admin'), (SELECT id FROM permissions WHERE code = N'n3.donation.recheck_payment'), NULL);
GO
