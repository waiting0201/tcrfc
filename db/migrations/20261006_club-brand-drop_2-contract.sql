/* ============================================================================
   clubs 品牌欄位刪除（contract）：logo_light_key／logo_dark_key／favicon_key／brand_color／brand_secondary_color
   2026-10-06｜依據：主站規劃書 v3.20——兩站標誌、Favicon、品牌色由前台靜態資產與 CSS 定義，後台不設定。
   保留 og_image_key／og_image_width／og_image_height（全站預設 OG 圖）。

   🔴 先確認新版 api 已上線（EF 的 Club 實體已移除這五個屬性）再跑：舊版 api 仍會 SELECT 這些欄位，跑了會立刻 500。
   🔴 欄位一刪無法找回（PITR 只有 7 天，docs/20 §5）；這五欄的內容全是後台曾填的品牌檔案路徑與色碼，
      前台不再讀取，視為可丟棄。需要留底的話先自行匯出：SELECT code, logo_light_key, ... FROM clubs;
   🔴 冪等：欄位不存在就跳過。
   執行：sqlcmd -S <server> -d tcrfc_club -b -i db/migrations/20261006_club-brand-drop_2-contract.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'clubs', N'logo_light_key') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN logo_light_key;');
IF COL_LENGTH(N'clubs', N'logo_dark_key') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN logo_dark_key;');
IF COL_LENGTH(N'clubs', N'favicon_key') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN favicon_key;');
IF COL_LENGTH(N'clubs', N'brand_color') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN brand_color;');
IF COL_LENGTH(N'clubs', N'brand_secondary_color') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN brand_secondary_color;');

COMMIT TRANSACTION;
