/* ============================================================================
   移除「捐助洽詢」表單種類（contract）：刪除 form_code = 'donation_enquiry' 的表單定義、欄位、欄位文字與收件資料
   2026-10-10｜依據：使用者 2026-10-09 裁決——捐助洽詢整個拿掉（主站規劃書 v3.25：G2 收件匣分頁與 Enquiry 涵蓋刪除，記錄於 docs/15）。表單種類由 9 種變 8 種。

   🔴 先確認新版 api 已上線（FormCatalog 已無 donation_enquiry）再跑：舊版 api 仍會讀該表單，跑了後台表單清單會缺一列、
      公開 GET /forms/donation_enquiry 會 404。本遷移不改任何結構（無 DDL），只刪資料。
   🔴 刪除無法找回（PITR 只有 7 天，docs/20 §5）。前台從未有這張表單的入口（docs/23 B-17），預期 enquiries 與 enquiry_answers 皆為 0 列，
      只有 forms 2 列（兩俱樂部各一）、form_fields 8 列、form_fields_i18n 16 列（種子建的預設欄位）；實際列數會 PRINT 出來供核對。
   🔴 冪等：沒有符合列就什麼也不刪。
   刪除順序：enquiry_answers → enquiries → forms（form_fields、form_fields_i18n、forms_i18n 由 ON DELETE CASCADE 帶走）。
   與 apps/api/Data/Migrations 的 DonationEnquiryDropContract 遷移同源。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261010_donation-enquiry-drop_2-contract.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @n int;

DELETE a FROM enquiry_answers a JOIN enquiries e ON e.id = a.enquiry_id JOIN forms f ON f.id = e.form_id WHERE f.form_code = N'donation_enquiry';
SET @n = @@ROWCOUNT; PRINT CONCAT(N'enquiry_answers 刪除 ', @n, N' 列');

DELETE e FROM enquiries e JOIN forms f ON f.id = e.form_id WHERE f.form_code = N'donation_enquiry';
SET @n = @@ROWCOUNT; PRINT CONCAT(N'enquiries 刪除 ', @n, N' 列');

DELETE FROM forms WHERE form_code = N'donation_enquiry';
SET @n = @@ROWCOUNT; PRINT CONCAT(N'forms 刪除 ', @n, N' 列（欄位與欄位文字由 CASCADE 帶走）');

COMMIT TRANSACTION;
