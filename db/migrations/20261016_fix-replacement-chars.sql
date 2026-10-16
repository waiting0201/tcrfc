/* ============================================================================
   修復 U+FFFD（亂碼）——資料遷移（E-296 第二次，E-330）
   2026-10-16｜根因：deploy 腳本用 mssql-tools（sqlcmd 13）讀 UTF-8 SQL 檔，多位元組字元落在緩衝邊界時被切壞（結束碼 0、無警告）；
   已改餵 UTF-16LE（deploy/lib/sqlcmd-guard.sh sqlcmd_to_utf16）。本檔只修「已寫進庫的殘骸」。
   只更新「目前值含 U+FFFD」的列（冪等；乾淨的庫印 0 列）。正確值逐字取自 repo 來源檔：
     1. faqs_i18n.answer  faq_id=7a2b5380-bf86-5881-998f-f06ebfd7515d（bw-beginners）locale=zh-Hant
        來源 db/prod/club-content-seed.sql 第 5757 行（亦見 db/seed/backoffice_seed.py、content/blue-whale/programs.md）
     2. articles_i18n.title  article_id=64090cca-d288-575e-9536-74f6bfa9adc6（tcrfc 2025-12-07-match-022）locale=en
        來源 db/prod/club-content-seed.sql 第 11583 行（亦見 db/seed/en_backfill_seed.py）
     3. page_versions.snapshot  id=de9c548a-203a-5b9b-b8f4-c3612168015f（bw club/player-development 第 1 版）
        來源 db/prod/club-content-seed.sql 第 5398 行（整列同一份快照；亦見 db/migrations/20261007_page-templates.sql 第 650 行）
   找不到來源的列不在此檔：套用後會印出仍含 U+FFFD 的欄位數，非 0 時用 db/tools/scan-replacement-chars.sql 點名、回報補來源。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261016_fix-replacement-chars.sql
   🔴 本檔含中文：用 sqlcmd 18／go-sqlcmd 並加 -f 65001，或用 UTF-16 副本；不要用 mssql-tools（sqlcmd 13）直接讀 UTF-8（E-330）。
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @n int = 0, @r int;

UPDATE faqs_i18n SET answer = N'課程是推廣性質，沒有經驗也可以參加；進階課程請衡量自身狀態。'
WHERE faq_id = N'7a2b5380-bf86-5881-998f-f06ebfd7515d' AND locale = N'zh-Hant'
  AND CHARINDEX(NCHAR(65533), answer COLLATE Latin1_General_BIN2) > 0;
SET @r = @@ROWCOUNT; SET @n += @r;
IF @r > 1 THROW 50001, N'faqs_i18n 更新超過 1 列（主鍵應唯一）', 1;

UPDATE articles_i18n SET title = N'Enterprise Premier League: 台灣電力 1-2 Taichung Rock FC'
WHERE article_id = N'64090cca-d288-575e-9536-74f6bfa9adc6' AND locale = N'en'
  AND CHARINDEX(NCHAR(65533), title COLLATE Latin1_General_BIN2) > 0;
SET @r = @@ROWCOUNT; SET @n += @r;
IF @r > 1 THROW 50001, N'articles_i18n 更新超過 1 列（主鍵應唯一）', 1;

UPDATE page_versions SET snapshot = N'{"seo":{"zh":{"seoTitle":"球員培育重點｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨球員培育重點：技術戰術分析、體能訓練、比賽判讀、心理韌性、影片分析、個人發展計畫、營養與生活、教育與語言，各面向詳細內容整理中。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"技術戰術分析","en":"Technical & Tactical Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"體能訓練","en":"Physical Fitness Training"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"比賽判讀","en":"Match Reading"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"心理韌性","en":"Mental Resilience"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"影片分析","en":"Video Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"IDP 個人發展計畫","en":"Individual Development Plan"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"營養與生活","en":"Nutrition & Lifestyle"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"教育與語言","en":"Education & Language"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}}]}},{"blockType":"text","content":{"body":{"zh":"八大面向，持續培育選手\n\n八大面向共同支持一線隊與青年隊球員的成長，銜接青年隊訓練與國際發展通道，協助選手持續進步。","en":"Eight areas, continued growth\n\nThe eight areas together support the growth of First Team and Youth team players, linking Youth team training with International Pathways to help players keep improving."}}}]}'
WHERE id = N'de9c548a-203a-5b9b-b8f4-c3612168015f'
  AND CHARINDEX(NCHAR(65533), snapshot COLLATE Latin1_General_BIN2) > 0;
SET @r = @@ROWCOUNT; SET @n += @r;
IF @r > 1 THROW 50001, N'page_versions 更新超過 1 列（主鍵應唯一）', 1;

-- 事後斷言：上面三列不得再含 U+FFFD
IF EXISTS (SELECT 1 FROM faqs_i18n WHERE faq_id = N'7a2b5380-bf86-5881-998f-f06ebfd7515d' AND CHARINDEX(NCHAR(65533), answer COLLATE Latin1_General_BIN2) > 0)
   OR EXISTS (SELECT 1 FROM articles_i18n WHERE article_id = N'64090cca-d288-575e-9536-74f6bfa9adc6' AND CHARINDEX(NCHAR(65533), title COLLATE Latin1_General_BIN2) > 0)
   OR EXISTS (SELECT 1 FROM page_versions WHERE id = N'de9c548a-203a-5b9b-b8f4-c3612168015f' AND CHARINDEX(NCHAR(65533), snapshot COLLATE Latin1_General_BIN2) > 0)
  THROW 50002, N'修復後仍含 U+FFFD', 1;

PRINT CONCAT(N'fix-replacement-chars rows updated: ', @n, N'（受損庫應為 1–3；乾淨庫為 0）');
COMMIT;
GO
