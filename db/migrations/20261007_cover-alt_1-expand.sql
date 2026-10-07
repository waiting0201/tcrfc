/* ============================================================================
   fan_events_i18n／press_resources_i18n 補封面圖片替代文字 cover_alt（展開，expand）
   2026-10-07｜依據：主站規劃書 §4.0 圖片欄位組要求每個圖片欄位都有雙語替代文字（比照 articles_i18n.cover_alt）。
   純新增可為空欄位：舊版 api 不讀不寫，但新版 api 會讀——🔴 必須先 migrate 再 deploy（E-289：反過來會讓 fan-events／press 回 500）。既有列維持 NULL。
   🔴 冪等：欄位已存在就跳過。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261007_cover-alt_1-expand.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'fan_events_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE fan_events_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'press_resources_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE press_resources_i18n ADD cover_alt nvarchar(200) NULL;');

COMMIT TRANSACTION;
