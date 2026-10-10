/* ============================================================================
   圖片欄位組補齊（展開，expand）——S0-7h 收尾
   2026-10-09｜依據：主站規劃書 §4.0「每一個圖片欄位是一組欄位：物件鍵、寬、高、雙語 Alt 文字」；docs/12d §12。
   比照 articles（AlignSchemaG1）、venues（AlignSchemaI1）、20261007_cover-alt_1-expand.sql：
     - 寬高 <名稱>_width／<名稱>_height 放主表；Alt <名稱>_alt 放 _i18n 側表（nvarchar(200)）。
     - 多圖子表（無側表）：image_alt_zh／image_alt_en 並排欄位；多圖子表若原本無寬高則一併補 image_width／image_height。
     - partners／sponsors 的深色／淺色標誌是同一個標誌的兩種配色，共用側表一個 logo_alt，寬高各自一組。
   純新增可為空欄位：舊版 api 不讀不寫，但新版 api 會讀——🔴 必須先 migrate 再 deploy（E-289：反過來會讓對應端點回 500）。
   既有資料列一律維持 NULL，不回填（前台無寬高就不輸出屬性）。
   🔴 冪等：欄位已存在就跳過。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261009_image-field-group_1-expand.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'teams', N'hero_width') IS NULL EXEC(N'ALTER TABLE teams ADD hero_width int NULL;');
IF COL_LENGTH(N'teams', N'hero_height') IS NULL EXEC(N'ALTER TABLE teams ADD hero_height int NULL;');
IF COL_LENGTH(N'teams_i18n', N'hero_alt') IS NULL EXEC(N'ALTER TABLE teams_i18n ADD hero_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'players', N'photo_width') IS NULL EXEC(N'ALTER TABLE players ADD photo_width int NULL;');
IF COL_LENGTH(N'players', N'photo_height') IS NULL EXEC(N'ALTER TABLE players ADD photo_height int NULL;');
IF COL_LENGTH(N'players_i18n', N'photo_alt') IS NULL EXEC(N'ALTER TABLE players_i18n ADD photo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'staff', N'photo_width') IS NULL EXEC(N'ALTER TABLE staff ADD photo_width int NULL;');
IF COL_LENGTH(N'staff', N'photo_height') IS NULL EXEC(N'ALTER TABLE staff ADD photo_height int NULL;');
IF COL_LENGTH(N'staff_i18n', N'photo_alt') IS NULL EXEC(N'ALTER TABLE staff_i18n ADD photo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'programs', N'cover_width') IS NULL EXEC(N'ALTER TABLE programs ADD cover_width int NULL;');
IF COL_LENGTH(N'programs', N'cover_height') IS NULL EXEC(N'ALTER TABLE programs ADD cover_height int NULL;');
IF COL_LENGTH(N'programs_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE programs_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'comic_characters', N'image_width') IS NULL EXEC(N'ALTER TABLE comic_characters ADD image_width int NULL;');
IF COL_LENGTH(N'comic_characters', N'image_height') IS NULL EXEC(N'ALTER TABLE comic_characters ADD image_height int NULL;');
IF COL_LENGTH(N'comic_characters_i18n', N'image_alt') IS NULL EXEC(N'ALTER TABLE comic_characters_i18n ADD image_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'comic_episodes', N'cover_width') IS NULL EXEC(N'ALTER TABLE comic_episodes ADD cover_width int NULL;');
IF COL_LENGTH(N'comic_episodes', N'cover_height') IS NULL EXEC(N'ALTER TABLE comic_episodes ADD cover_height int NULL;');
IF COL_LENGTH(N'comic_episodes_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE comic_episodes_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'fan_events', N'cover_width') IS NULL EXEC(N'ALTER TABLE fan_events ADD cover_width int NULL;');
IF COL_LENGTH(N'fan_events', N'cover_height') IS NULL EXEC(N'ALTER TABLE fan_events ADD cover_height int NULL;');
IF COL_LENGTH(N'partner_stores', N'image_width') IS NULL EXEC(N'ALTER TABLE partner_stores ADD image_width int NULL;');
IF COL_LENGTH(N'partner_stores', N'image_height') IS NULL EXEC(N'ALTER TABLE partner_stores ADD image_height int NULL;');
IF COL_LENGTH(N'partner_stores_i18n', N'image_alt') IS NULL EXEC(N'ALTER TABLE partner_stores_i18n ADD image_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'member_draws', N'cover_width') IS NULL EXEC(N'ALTER TABLE member_draws ADD cover_width int NULL;');
IF COL_LENGTH(N'member_draws', N'cover_height') IS NULL EXEC(N'ALTER TABLE member_draws ADD cover_height int NULL;');
IF COL_LENGTH(N'member_draws_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE member_draws_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'calendar_custom_events', N'cover_width') IS NULL EXEC(N'ALTER TABLE calendar_custom_events ADD cover_width int NULL;');
IF COL_LENGTH(N'calendar_custom_events', N'cover_height') IS NULL EXEC(N'ALTER TABLE calendar_custom_events ADD cover_height int NULL;');
IF COL_LENGTH(N'calendar_custom_events_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE calendar_custom_events_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'product_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE product_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'product_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE product_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'charities', N'logo_width') IS NULL EXEC(N'ALTER TABLE charities ADD logo_width int NULL;');
IF COL_LENGTH(N'charities', N'logo_height') IS NULL EXEC(N'ALTER TABLE charities ADD logo_height int NULL;');
IF COL_LENGTH(N'charities_i18n', N'logo_alt') IS NULL EXEC(N'ALTER TABLE charities_i18n ADD logo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'charity_programs', N'cover_width') IS NULL EXEC(N'ALTER TABLE charity_programs ADD cover_width int NULL;');
IF COL_LENGTH(N'charity_programs', N'cover_height') IS NULL EXEC(N'ALTER TABLE charity_programs ADD cover_height int NULL;');
IF COL_LENGTH(N'charity_programs_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE charity_programs_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'charity_program_images', N'image_width') IS NULL EXEC(N'ALTER TABLE charity_program_images ADD image_width int NULL;');
IF COL_LENGTH(N'charity_program_images', N'image_height') IS NULL EXEC(N'ALTER TABLE charity_program_images ADD image_height int NULL;');
IF COL_LENGTH(N'charity_program_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE charity_program_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'charity_program_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE charity_program_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'impact_record_images', N'image_width') IS NULL EXEC(N'ALTER TABLE impact_record_images ADD image_width int NULL;');
IF COL_LENGTH(N'impact_record_images', N'image_height') IS NULL EXEC(N'ALTER TABLE impact_record_images ADD image_height int NULL;');
IF COL_LENGTH(N'impact_record_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE impact_record_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'impact_record_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE impact_record_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'impact_records_i18n', N'image_alt') IS NULL EXEC(N'ALTER TABLE impact_records_i18n ADD image_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'partners', N'logo_dark_width') IS NULL EXEC(N'ALTER TABLE partners ADD logo_dark_width int NULL;');
IF COL_LENGTH(N'partners', N'logo_dark_height') IS NULL EXEC(N'ALTER TABLE partners ADD logo_dark_height int NULL;');
IF COL_LENGTH(N'partners', N'logo_light_width') IS NULL EXEC(N'ALTER TABLE partners ADD logo_light_width int NULL;');
IF COL_LENGTH(N'partners', N'logo_light_height') IS NULL EXEC(N'ALTER TABLE partners ADD logo_light_height int NULL;');
IF COL_LENGTH(N'partners_i18n', N'logo_alt') IS NULL EXEC(N'ALTER TABLE partners_i18n ADD logo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'sponsors', N'logo_dark_width') IS NULL EXEC(N'ALTER TABLE sponsors ADD logo_dark_width int NULL;');
IF COL_LENGTH(N'sponsors', N'logo_dark_height') IS NULL EXEC(N'ALTER TABLE sponsors ADD logo_dark_height int NULL;');
IF COL_LENGTH(N'sponsors', N'logo_light_width') IS NULL EXEC(N'ALTER TABLE sponsors ADD logo_light_width int NULL;');
IF COL_LENGTH(N'sponsors', N'logo_light_height') IS NULL EXEC(N'ALTER TABLE sponsors ADD logo_light_height int NULL;');
IF COL_LENGTH(N'sponsors_i18n', N'logo_alt') IS NULL EXEC(N'ALTER TABLE sponsors_i18n ADD logo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'sponsor_activation_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE sponsor_activation_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'sponsor_activation_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE sponsor_activation_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'comic_pages', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE comic_pages ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'comic_pages', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE comic_pages ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'fan_event_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE fan_event_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'fan_event_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE fan_event_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'clubs_i18n', N'og_image_alt') IS NULL EXEC(N'ALTER TABLE clubs_i18n ADD og_image_alt nvarchar(200) NULL;');

COMMIT TRANSACTION;
