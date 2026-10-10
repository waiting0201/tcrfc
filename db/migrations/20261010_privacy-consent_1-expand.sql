/* ============================================================================
   報名與詢問留存隱私同意紀錄（展開，expand）
   2026-10-10｜依據：主站規劃書 v3.25 §3.5、§3.10、§4.4 P3／P4、§4.7 G2、§4.9 I、§5.1 Registration／Enquiry；App 規劃書 v3.18 §3.9、§9、§10。
   registrations、enquiries 各新增兩個可為空欄位：
     - privacy_consented_at   datetime2(3)  UTC；全庫時間欄位一律 datetime2(3) UTC，不另開 datetimeoffset（EF 全域 UTC 轉換器只處理 DateTime）。
     - privacy_policy_version nvarchar(50)  送出當下該俱樂部設定（settings.legal.privacy_policy_version，未設定用 1.0）的政策版本編號。
   由伺服器於送出時寫入，不信任客戶端；既有資料列維持 NULL，不回填。
   純新增可為空欄位：舊版 api 不讀不寫，新版 api 會讀寫——🔴 必須先 migrate 再 deploy（E-289：反過來會讓報名與詢問端點回 500）。
   🔴 冪等：欄位已存在就跳過。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261010_privacy-consent_1-expand.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'registrations', N'privacy_consented_at') IS NULL EXEC(N'ALTER TABLE registrations ADD privacy_consented_at datetime2(3) NULL;');
IF COL_LENGTH(N'registrations', N'privacy_policy_version') IS NULL EXEC(N'ALTER TABLE registrations ADD privacy_policy_version nvarchar(50) NULL;');
IF COL_LENGTH(N'enquiries', N'privacy_consented_at') IS NULL EXEC(N'ALTER TABLE enquiries ADD privacy_consented_at datetime2(3) NULL;');
IF COL_LENGTH(N'enquiries', N'privacy_policy_version') IS NULL EXEC(N'ALTER TABLE enquiries ADD privacy_policy_version nvarchar(50) NULL;');

COMMIT TRANSACTION;
