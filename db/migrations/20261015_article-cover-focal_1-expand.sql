/* ============================================================================
   新聞封面焦點（展開，expand）
   2026-10-15｜依據：主站規劃書 v3.28 §3.7、§4.2 B2、§5.1 Article。
   articles 新增兩個可為空欄位與一個具名 CHECK：
     - cover_focal_x decimal(5,2) NULL  焦點水平位置（百分比 0–100）
     - cover_focal_y decimal(5,2) NULL  焦點垂直位置（百分比 0–100）
     - CK_articles_cover_focal          兩欄同為 NULL（＝置中），或兩欄皆在 0–100
   既有資料列維持 NULL（置中），不回填。
   純新增可為空欄位：舊版 api 不讀不寫，新版 api 會讀寫——🔴 必須先 migrate 再 deploy（E-289）。
   🔴 冪等：欄位／約束已存在就跳過。
   與 EF migration ArticleCoverFocal 同源。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261015_article-cover-focal_1-expand.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'articles', N'cover_focal_x') IS NULL EXEC(N'ALTER TABLE articles ADD cover_focal_x decimal(5,2) NULL;');
IF COL_LENGTH(N'articles', N'cover_focal_y') IS NULL EXEC(N'ALTER TABLE articles ADD cover_focal_y decimal(5,2) NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_articles_cover_focal' AND parent_object_id = OBJECT_ID(N'articles'))
    EXEC(N'ALTER TABLE articles ADD CONSTRAINT CK_articles_cover_focal CHECK ((cover_focal_x IS NULL AND cover_focal_y IS NULL) OR (cover_focal_x BETWEEN 0 AND 100 AND cover_focal_y BETWEEN 0 AND 100));');

COMMIT TRANSACTION;
