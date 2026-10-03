-- ============================================================================
-- TCRFC 慈善庫（tcrfc_charity）：本機種子的「內容資料」匯入正式庫（供前後台串接實機驗收）
-- 自動產生：python3 db/seed/generate-prod-content-sql.py（請勿手動編輯；改界線請改該腳本後重新產生）
-- 來源產生器：db/seed/generate-charity-seed-sql.py
-- 🔴 這不是參照資料（那是 db/prod/*-reference-data.sql），也不是帳號：
--    不含任何 admin_users、會員、報名、訂單、捐款、金流、發票、對帳、稽核、寄信紀錄（見 db/seed/README.md「匯入正式庫的內容種子」）。
-- 內容仍帶有【測試】前綴與 example.com 信箱（標示用）；驗收結束後用 deploy/prod-seed-import.sh clean 全部清除。
-- 整份檔案由 deploy/prod-seed-import.sh 包在單一交易內執行（開頭 BEGIN TRANSACTION、結尾寫延伸屬性並 COMMIT）。
-- 區段分類：
--   IMPORT    5a, 5b, 6, 7, 7b, 14a, 14b, 15
--   REFERENCE 0, 1, 2, 3, 3b
--   ACCOUNTS  4
--   PERSONAL  8, 9, 10, 11, 12a, 12b, 13, 16, 17
-- 區段內剔除的批次（審查用）：
-- 匯入會寫入的表（清除程序只動這些表；匯入前必須全空）：
-- OWNED charity_program_refs
-- OWNED charity_refs
-- OWNED donation_amount_options
-- OWNED donation_projects
-- OWNED donation_projects_i18n
-- OWNED donation_stores
-- OWNED donation_stores_i18n
-- OWNED email_templates
-- OWNED email_templates_i18n
-- OWNED settings
-- OWNED settings_i18n
-- 匯入前後筆數必須不變的表（帳號、假個資、假交易；匯入程序以此確認沒有夾帶）：
-- DENIED admin_user_roles
-- DENIED admin_users
-- DENIED audit_logs
-- DENIED donation_invoices
-- DENIED donation_payments
-- DENIED donations
-- DENIED email_logs
-- DENIED payment_channels
-- DENIED reconciliation_discrepancies
-- DENIED reconciliation_runs
-- DENIED settlement_lines
-- DENIED settlements
-- ============================================================================

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── 5a. charity_refs：主站公益團體主檔的虛構唯讀複本（2 筆） ─────────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_refs WHERE ref_code = N'TEST-CHARITY-A';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fe766761-c4f1-5231-9715-9ce654cc6f21';
  INSERT INTO charity_refs (id, ref_code, name, source)
  VALUES (@id, N'TEST-CHARITY-A', N'測試用．公益機構Ａ（僅供本機開發顯示）', N'manual_test_seed');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_refs WHERE ref_code = N'TEST-CHARITY-B';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ccf36aa5-eaa5-5e3c-9957-9334b50265ff';
  INSERT INTO charity_refs (id, ref_code, name, source)
  VALUES (@id, N'TEST-CHARITY-B', N'測試用．公益機構Ｂ（僅供本機開發顯示）', N'manual_test_seed');
  COMMIT TRANSACTION;
END
GO

-- ── 5b. charity_program_refs：主站慈善計畫主檔的虛構唯讀複本（3 筆） ──────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_program_refs WHERE ref_code = N'TEST-PROGRAM-A1';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a8b5b142-ceb3-51ce-91a7-1c4cb7e85e71';
  INSERT INTO charity_program_refs (id, ref_code, charity_ref_id, name)
  VALUES (@id, N'TEST-PROGRAM-A1', (SELECT id FROM charity_refs WHERE ref_code = N'TEST-CHARITY-A'), N'測試用．公益計畫Ａ－１（僅供本機開發顯示）');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_program_refs WHERE ref_code = N'TEST-PROGRAM-A2';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd9e5c6b9-ee35-5b72-bb2b-cd2cafd195b9';
  INSERT INTO charity_program_refs (id, ref_code, charity_ref_id, name)
  VALUES (@id, N'TEST-PROGRAM-A2', (SELECT id FROM charity_refs WHERE ref_code = N'TEST-CHARITY-A'), N'測試用．公益計畫Ａ－２（僅供本機開發顯示）');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_program_refs WHERE ref_code = N'TEST-PROGRAM-B1';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fc1196d3-143d-58e3-a449-6dca43022ed5';
  INSERT INTO charity_program_refs (id, ref_code, charity_ref_id, name)
  VALUES (@id, N'TEST-PROGRAM-B1', (SELECT id FROM charity_refs WHERE ref_code = N'TEST-CHARITY-B'), N'測試用．公益計畫Ｂ－１（僅供本機開發顯示）');
  COMMIT TRANSACTION;
END
GO

-- ── 6. donation_stores：3 家，涵蓋不同類別／有無 Logo／合作中或已停止 ───────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM donation_stores WHERE store_slug = N'store-19c29e0fbf';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8bd5c076-12c7-5baf-9992-9642744bf05e';
  INSERT INTO donation_stores (id, store_slug, category, address, contact_name, contact_phone, store_share_pct, logo_key, logo_width, logo_height, start_on, end_on, status)
  VALUES (@id, N'store-19c29e0fbf', N'餐飲', N'臺中市西區測試路 1 號（虛構地址）', N'測試用．店長甲', N'04-00000001', 5.00, N'dev-seed/donation-stores/store-a.webp', 640, 640, '2026-06-01', NULL, N'active');
  INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'zh-Hant', N'測試用．早安豆漿店', N'測試占位 Logo');
  INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'en', N'Test Breakfast Diner', N'Dev seed placeholder logo');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM donation_stores WHERE store_slug = N'store-e64afe2e6c';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b61c8402-0f18-5473-82c0-7c8069b1f1d5';
  INSERT INTO donation_stores (id, store_slug, category, address, contact_name, contact_phone, store_share_pct, logo_key, logo_width, logo_height, start_on, end_on, status)
  VALUES (@id, N'store-e64afe2e6c', N'飲料', N'臺中市北區測試街 2 號（虛構地址）', N'測試用．店長乙', N'04-00000002', 8.00, NULL, NULL, NULL, '2026-06-15', NULL, N'active');
  INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'zh-Hant', N'測試用．巷口咖啡', NULL);
  INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'en', N'Test Corner Cafe', NULL);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM donation_stores WHERE store_slug = N'store-7d35025776';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0375db08-7f95-580e-b96c-f70fb505bde4';
  INSERT INTO donation_stores (id, store_slug, category, address, contact_name, contact_phone, store_share_pct, logo_key, logo_width, logo_height, start_on, end_on, status)
  VALUES (@id, N'store-7d35025776', N'零售', N'臺中市南區測試巷 3 號（虛構地址）', N'測試用．店長丙', N'04-00000003', 3.00, NULL, NULL, NULL, '2026-01-01', '2026-07-31', N'inactive');
  INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'zh-Hant', N'測試用．已停止合作服飾行', NULL);
  INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'en', N'Test Ended Apparel Shop', NULL);
  COMMIT TRANSACTION;
END
GO

-- ── 7. donation_projects：已上架 2（不同 invoice_mode）＋ 已下架 1 ──────────
-- ⚠️ 已知綱要缺口：schema 沒有『已結束』狀態值、也沒有起訖日欄位，見上方註解，已列入回報。
DECLARE @id uniqueidentifier;
SELECT @id = id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'67a6dd59-d7f1-547b-aa15-74926e9babb1';
  INSERT INTO donation_projects (id, project_slug, min_amount, max_amount, project_share_pct, invoice_mode, charity_ref_code, charity_name_snapshot, charity_program_ref_code, charity_program_name_snapshot, sort_order, status)
  VALUES (@id, N'project-0e3ffbf31d', 100, 100000, 10.00, N'b2c_invoice', N'TEST-CHARITY-A', N'測試用．公益機構Ａ（僅供本機開發顯示）', N'TEST-PROGRAM-A1', N'測試用．公益計畫Ａ－１（僅供本機開發顯示）', 0, N'published');
  INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage) VALUES (@id, N'zh-Hant', N'測試用．兒童足球獎學金計畫', N'讓偏鄉孩子也能踢上球（測試文案）', N'{"blocks": [{"type": "paragraph", "text": "測試用．兒童足球獎學金計畫的詳細說明（開發測試用占位內文，非正式文案）。"}]}', N'款項用於球具採購與交通補助（測試占位文字）');
  INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage) VALUES (@id, N'en', N'Test Youth Football Scholarship', N'Helping children in rural areas play football (dev seed copy)', N'{"blocks": [{"type": "paragraph", "text": "Placeholder body copy for Test Youth Football Scholarship (dev seed only)."}]}', N'Funds are used for equipment and transport subsidies (dev seed placeholder).');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM donation_projects WHERE project_slug = N'project-b35b011f64';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7e529bb2-f868-5f23-9393-c0dddd35b65e';
  INSERT INTO donation_projects (id, project_slug, min_amount, max_amount, project_share_pct, invoice_mode, charity_ref_code, charity_name_snapshot, charity_program_ref_code, charity_program_name_snapshot, sort_order, status)
  VALUES (@id, N'project-b35b011f64', 200, 200000, 12.00, N'donation_receipt', N'TEST-CHARITY-B', N'測試用．公益機構Ｂ（僅供本機開發顯示）', N'TEST-PROGRAM-B1', N'測試用．公益計畫Ｂ－１（僅供本機開發顯示）', 1, N'published');
  INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage) VALUES (@id, N'zh-Hant', N'測試用．偏鄉球場整建計畫', N'修一座能安全踢球的場地（測試文案）', N'{"blocks": [{"type": "paragraph", "text": "測試用．偏鄉球場整建計畫的詳細說明（開發測試用占位內文，非正式文案）。"}]}', N'款項用於場地整地與圍網修繕（測試占位文字）');
  INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage) VALUES (@id, N'en', N'Test Rural Pitch Renovation', N'Rebuilding a safe pitch (dev seed copy)', N'{"blocks": [{"type": "paragraph", "text": "Placeholder body copy for Test Rural Pitch Renovation (dev seed only)."}]}', N'Funds are used for pitch grading and fence repair (dev seed placeholder).');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM donation_projects WHERE project_slug = N'project-99e6630e75';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cbb9d74c-7701-5ecf-803c-b8e1dec5a0f3';
  INSERT INTO donation_projects (id, project_slug, min_amount, max_amount, project_share_pct, invoice_mode, charity_ref_code, charity_name_snapshot, charity_program_ref_code, charity_program_name_snapshot, sort_order, status)
  VALUES (@id, N'project-99e6630e75', 100, 50000, 6.00, N'b2c_invoice', N'TEST-CHARITY-A', N'測試用．公益機構Ａ（僅供本機開發顯示）', N'TEST-PROGRAM-A2', N'測試用．公益計畫Ａ－２（僅供本機開發顯示）', 2, N'draft');
  INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage) VALUES (@id, N'zh-Hant', N'測試用．已下架示範項目', N'示範已下架狀態（測試文案）', N'{"blocks": [{"type": "paragraph", "text": "測試用．已下架示範項目的詳細說明（開發測試用占位內文，非正式文案）。"}]}', N'（已下架，款項用途說明僅供介面示範）');
  INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage) VALUES (@id, N'en', N'Test Unpublished Sample Project', N'Demonstrates an unpublished project (dev seed copy)', N'{"blocks": [{"type": "paragraph", "text": "Placeholder body copy for Test Unpublished Sample Project (dev seed only)."}]}', N'(Unpublished; description shown for UI demo only.)');
  COMMIT TRANSACTION;
END
GO

-- ── 7b. donation_amount_options：每個項目 3–4 檔金額選項卡 ──────────────
IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d') AND amount = 300)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d'), 300, 0);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d') AND amount = 500)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d'), 500, 1);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d') AND amount = 1000)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d'), 1000, 2);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d') AND amount = 3000)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-0e3ffbf31d'), 3000, 3);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-b35b011f64') AND amount = 500)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-b35b011f64'), 500, 0);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-b35b011f64') AND amount = 1000)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-b35b011f64'), 1000, 1);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-b35b011f64') AND amount = 2000)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-b35b011f64'), 2000, 2);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-b35b011f64') AND amount = 5000)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-b35b011f64'), 5000, 3);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-99e6630e75') AND amount = 200)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-99e6630e75'), 200, 0);
GO

IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = (SELECT id FROM donation_projects WHERE project_slug = N'project-99e6630e75') AND amount = 500)
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ((SELECT id FROM donation_projects WHERE project_slug = N'project-99e6630e75'), 500, 1);
GO

-- ── 14a. settings：非文案類規則性數值（3 筆） ────────────────────────
IF NOT EXISTS (SELECT 1 FROM settings WHERE setting_key = N'donation.default_min_amount')
  INSERT INTO settings (setting_key, value) VALUES (N'donation.default_min_amount', N'100');
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE setting_key = N'donation.default_max_amount')
  INSERT INTO settings (setting_key, value) VALUES (N'donation.default_max_amount', N'1000000');
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE setting_key = N'donation.credit_list_display_rule')
  INSERT INTO settings (setting_key, value) VALUES (N'donation.credit_list_display_rule', N'named_unless_anonymous');
GO

-- ── 14b. settings：文案類（4 筆，含 zh-Hant／en） ───────────────────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE setting_key = N'donation.home_intro';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ba464ddc-0f77-54b3-ac33-6755792eef29';
  INSERT INTO settings (id, setting_key, value) VALUES (@id, N'donation.home_intro', NULL);
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'zh-Hant', N'（開發測試用首頁說明文案，正式文案待客戶與協會確認）');
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'en', N'(Dev seed placeholder home intro copy, pending client/association confirmation.)');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE setting_key = N'donation.thank_you_message_template';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'485e5db6-028d-5243-aabd-dc428780db4f';
  INSERT INTO settings (id, setting_key, value) VALUES (@id, N'donation.thank_you_message_template', NULL);
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'zh-Hant', N'感謝您的愛心捐款！（開發測試用感謝語樣板）');
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'en', N'Thank you for your generous donation! (dev seed placeholder)');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE setting_key = N'donation.notice';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'934e39bf-1a89-5e14-b388-7039092cecfe';
  INSERT INTO settings (id, setting_key, value) VALUES (@id, N'donation.notice', NULL);
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'zh-Hant', N'本平台捐款一經完成，對外恕不受理退款申請（開發測試用捐款須知占位文字）。');
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'en', N'Donations are final and non-refundable via the public site (dev seed placeholder notice).');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE setting_key = N'donation.privacy_policy';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'34023aa2-6d92-52f9-8415-f81a27c70700';
  INSERT INTO settings (id, setting_key, value) VALUES (@id, N'donation.privacy_policy', NULL);
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'zh-Hant', N'（開發測試用隱私權政策占位文字，正式內容待法務確認）');
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'en', N'(Dev seed placeholder privacy policy, pending legal review.)');
  COMMIT TRANSACTION;
END
GO

-- ── 15. email_templates／email_templates_i18n：四封系統信 ───────────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM email_templates WHERE code = N'donation_thanks';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1cb681f1-c232-5db3-a09a-2271c55ff7ad';
  INSERT INTO email_templates (id, code, is_active) VALUES (@id, N'donation_thanks', 1);
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'zh-Hant', N'感謝您的捐款（開發測試用樣板主旨）', N'感謝您對本次公益計畫的支持，這是開發測試用的樣板內文，正式文案待協會確認。');
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'en', N'Thank you for your donation (dev seed placeholder subject)', N'Thank you for supporting this program. This is a dev seed placeholder body; final copy pending association review.');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM email_templates WHERE code = N'invoice_issued';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a5e011c4-71c8-52d0-8b94-3cd652c71845';
  INSERT INTO email_templates (id, code, is_active) VALUES (@id, N'invoice_issued', 1);
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'zh-Hant', N'您的電子發票／收據已開立（開發測試用樣板主旨）', N'您的憑證已開立，這是開發測試用的樣板內文，正式文案待協會確認。');
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'en', N'Your invoice/receipt has been issued (dev seed placeholder subject)', N'Your document has been issued. This is a dev seed placeholder body; final copy pending association review.');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM email_templates WHERE code = N'invoice_failed';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'16e28144-3142-5496-96d9-de20f4cb38e2';
  INSERT INTO email_templates (id, code, is_active) VALUES (@id, N'invoice_failed', 1);
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'zh-Hant', N'發票開立失敗通知（開發測試用樣板主旨）', N'您的憑證開立失敗，我們將盡快處理，這是開發測試用的樣板內文。');
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'en', N'Invoice issuance failed (dev seed placeholder subject)', N'Your document failed to issue and will be retried. This is a dev seed placeholder body.');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM email_templates WHERE code = N'refund_notice';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd74e60ae-3f5a-592b-9ef1-39bb0f8ba4c4';
  INSERT INTO email_templates (id, code, is_active) VALUES (@id, N'refund_notice', 1);
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'zh-Hant', N'退款通知（開發測試用樣板主旨）', N'您的捐款已完成退款，這是開發測試用的樣板內文，正式文案待協會確認。');
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'en', N'Refund notice (dev seed placeholder subject)', N'Your donation has been refunded. This is a dev seed placeholder body; final copy pending association review.');
  COMMIT TRANSACTION;
END
GO

EXEC sys.sp_addextendedproperty @name = N'tcrfc.seed_import', @value = N'$(IMPORT_BATCH)';
COMMIT TRANSACTION;
GO
