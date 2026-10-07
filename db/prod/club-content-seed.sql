-- ============================================================================
-- TCRFC 主站庫（tcrfc_club）：本機種子的「內容資料」匯入正式庫（供前後台串接實機驗收）
-- 自動產生：python3 db/seed/generate-prod-content-sql.py（請勿手動編輯；改界線請改該腳本後重新產生）
-- 來源產生器：db/seed/generate-club-seed-sql.py
-- 🔴 這不是參照資料（那是 db/prod/*-reference-data.sql），也不是帳號：
--    不含任何 admin_users、會員、報名、訂單、捐款、金流、發票、對帳、稽核、寄信紀錄（見 db/seed/README.md「匯入正式庫的內容種子」）。
-- 內容仍帶有【測試】前綴與 example.com 信箱（標示用）；驗收結束後用 deploy/prod-seed-import.sh clean 全部清除。
-- 整份檔案由 deploy/prod-seed-import.sh 包在單一交易內執行（開頭 BEGIN TRANSACTION、結尾寫延伸屬性並 COMMIT）。
-- 區段分類：
--   IMPORT    1, 2, 2b, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 24, 24b, 24c, 25, 26, 27, 28, 29, 30, 31, 31b, 32, 33, 34, 35, 36, 37, 37b, 38, 39, 42, 43, 45, 46, 47, 48, 49, 50, 51, 52, 54, 56, 57, 58, 61
--   REFERENCE 0, 5, 18.1, 18.2, 18.3, 19, 20, 21, 22, 23
--   ACCOUNTS  18.4
--   PERSONAL  44, 53, 55, 59, 60
-- 區段內剔除的批次（審查用）：
-- DROPPED section=1 batches=2 reason=參照表（正式庫已有） tables=clubs,clubs_i18n
-- DROPPED section=36 batches=3 reason=禁用表 tables=enquiries,enquiry_answers
-- DROPPED section=42 batches=4 reason=禁用表 tables=registrations
-- DROPPED section=50 batches=4 reason=禁用表 tables=fan_event_registrations
-- DROPPED section=54 batches=1 reason=禁用表 tables=draw_roster_versions,draw_rosters,member_draws,member_draws_i18n
-- DROPPED section=54 batches=1 reason=禁用表 tables=member_draws,member_draws_i18n
-- DROPPED section=61 batches=2 reason=禁用表 tables=member_draws_i18n
-- DROPPED section=61 batches=1 reason=參照表（正式庫已有） tables=clubs_i18n
-- 匯入會寫入的表（清除程序只動這些表；匯入前必須全空）：
-- OWNED achievements
-- OWNED achievements_i18n
-- OWNED ad_campaigns
-- OWNED ad_creatives
-- OWNED ad_daily_stats
-- OWNED ad_slots
-- OWNED ad_slots_i18n
-- OWNED advertisers
-- OWNED advertisers_i18n
-- OWNED app_announcements
-- OWNED app_announcements_i18n
-- OWNED app_credentials
-- OWNED app_deep_links
-- OWNED app_deep_links_i18n
-- OWNED app_feature_flags
-- OWNED app_layout_items
-- OWNED app_layout_items_i18n
-- OWNED app_releases
-- OWNED app_releases_i18n
-- OWNED article_tags
-- OWNED articles
-- OWNED articles_i18n
-- OWNED banners
-- OWNED banners_i18n
-- OWNED calendar_custom_events
-- OWNED calendar_custom_events_i18n
-- OWNED calendar_event_exceptions
-- OWNED calendar_event_teams
-- OWNED calendar_team_settings
-- OWNED calendar_team_settings_i18n
-- OWNED charities
-- OWNED charities_i18n
-- OWNED charity_program_partners
-- OWNED charity_program_sponsors
-- OWNED charity_programs
-- OWNED charity_programs_i18n
-- OWNED collections
-- OWNED collections_i18n
-- OWNED comic_characters
-- OWNED comic_characters_i18n
-- OWNED comic_episodes
-- OWNED comic_episodes_i18n
-- OWNED competitions
-- OWNED competitions_i18n
-- OWNED fan_events
-- OWNED fan_events_i18n
-- OWNED faq_category_links
-- OWNED faq_embed_slot_links
-- OWNED faqs
-- OWNED faqs_i18n
-- OWNED impact_metrics
-- OWNED impact_metrics_i18n
-- OWNED impact_records
-- OWNED impact_records_i18n
-- OWNED inventory_movements
-- OWNED invoice_donation_codes
-- OWNED match_teams
-- OWNED matches
-- OWNED matches_i18n
-- OWNED membership_benefits
-- OWNED membership_benefits_i18n
-- OWNED membership_plans
-- OWNED membership_plans_i18n
-- OWNED milestones
-- OWNED milestones_i18n
-- OWNED page_blocks
-- OWNED page_versions
-- OWNED pages
-- OWNED pages_i18n
-- OWNED partner_stores
-- OWNED partner_stores_i18n
-- OWNED partners
-- OWNED partners_i18n
-- OWNED players
-- OWNED players_i18n
-- OWNED press_resources
-- OWNED press_resources_i18n
-- OWNED product_variants
-- OWNED products
-- OWNED products_i18n
-- OWNED programs
-- OWNED programs_i18n
-- OWNED proposals
-- OWNED proposals_i18n
-- OWNED redirects
-- OWNED seasons
-- OWNED sessions
-- OWNED settings
-- OWNED settings_i18n
-- OWNED sponsor_activations
-- OWNED sponsor_activations_i18n
-- OWNED sponsor_package_links
-- OWNED sponsor_packages
-- OWNED sponsor_packages_i18n
-- OWNED sponsors
-- OWNED sponsors_i18n
-- OWNED staff
-- OWNED staff_i18n
-- OWNED staff_teams
-- OWNED standings
-- OWNED standings_i18n
-- OWNED tags
-- OWNED tags_i18n
-- OWNED teams
-- OWNED teams_i18n
-- OWNED trials
-- OWNED trials_i18n
-- OWNED value_tag_links
-- OWNED venues
-- OWNED venues_i18n
-- 匯入前後筆數必須不變的表（帳號、假個資、假交易；匯入程序以此確認沒有夾帶）：
-- DENIED admin_user_clubs
-- DENIED admin_user_roles
-- DENIED admin_users
-- DENIED app_devices
-- DENIED app_diagnostic_reports
-- DENIED draw_roster_versions
-- DENIED draw_rosters
-- DENIED enquiries
-- DENIED enquiry_answers
-- DENIED fan_event_registrations
-- DENIED jersey_issues
-- DENIED member_cards
-- DENIED member_draws
-- DENIED member_draws_i18n
-- DENIED members
-- DENIED membership_payments
-- DENIED memberships
-- DENIED newsletter_subscribers
-- DENIED order_items
-- DENIED orders
-- DENIED push_message_stats
-- DENIED push_messages
-- DENIED push_messages_i18n
-- DENIED refund_request_items
-- DENIED refund_requests
-- DENIED registrations
-- DENIED shipments
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

-- 補 clubs_i18n.description（既有欄位）：成立日期／口號／隸屬協會等事實文字化存入簡介。
-- ⚠️ 成立日期、口號、社群連結、隸屬協會目前 db/club-schema.sql 沒有專屬欄位，本次不新增
-- 欄位（CLAUDE.md 全域規定 2），只把可以放進既有『簡介』欄位的事實寫入，其餘留待回報。
UPDATE clubs_i18n SET description = N'臺中市女子足球協會－台中藍鯨女子足球隊，簡稱台中藍鯨，成立於 2014 年 4 月 12 日，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。俱樂部口號：「藍色的天空是我們心中夢想的方向，閃爍的陽光是走向夢想的力量，草地上揮灑汗水是成長過往堅定信仰，有你在身旁就不再徬徨，此時此刻，我們與我們的球迷站在一起，一起迎向世界。」'
WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND locale = N'zh-Hant';
GO

-- ── 2. teams：台中磐石一線隊 D1（code 全站唯一） ───────────────────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM teams WHERE code = N'D1';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd758d495-1a82-52a3-9cf1-51691b2f6a1b';
  INSERT INTO teams (id, club_id, code, type, gender, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'D1', N'first_team', N'men', 0);
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'zh-Hant', N'一線隊');
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'en', N'First Team');
  COMMIT TRANSACTION;
END
GO

-- ── 2b. teams：台中藍鯨一線隊 BW1 與其青年隊（不是第二個 D1，docs/13 踩雷點 2） ──
-- U15／U12 只建隊伍記錄，不建球員——舊站沒有這兩隊的名單（content/blue-whale/squad/
-- youth-teams.md 已註明抓不到），不得編造。code 加 BW- 前綴避免與磐石未來可能建立的
-- 同名學院隊（docs/12 §4.2 值域列的 U15／U14／U12 是磐石保留）撞號——Team.code 全站唯一。
DECLARE @id uniqueidentifier;
SELECT @id = id FROM teams WHERE code = N'BW1';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'029bbb95-e50b-522c-b422-f8688c07ae7a';
  INSERT INTO teams (id, club_id, code, type, gender, age_band, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'BW1', N'first_team', N'women', NULL, 0);
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'zh-Hant', N'一線隊');
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'en', N'First Team');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM teams WHERE code = N'BW-U15';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a30cef55-053e-5170-956b-8c6d9adc9807';
  INSERT INTO teams (id, club_id, code, type, gender, age_band, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'BW-U15', N'academy', N'women', N'U15', 1);
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'zh-Hant', N'U15 青少年女子足球隊');
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'en', N'U15 Girls');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM teams WHERE code = N'BW-U12';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'682bf04f-ba86-50b0-aafe-6f4e3691227d';
  INSERT INTO teams (id, club_id, code, type, gender, age_band, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'BW-U12', N'academy', N'women', N'U12', 2);
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'zh-Hant', N'U12 青少年女子足球隊');
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'en', N'U12 Girls');
  COMMIT TRANSACTION;
END
GO

-- ── 3. seasons：2026-27 球季（起訖日＝schedule.json 實際最早／最晚比賽日期，
--    規劃書未給官方球季框架日期，用真實賽程日期推導以滿足 NOT NULL，非官方球季起訖） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'40586e03-a071-5f97-b3fe-2fd309063ee4';
  INSERT INTO seasons (id, club_id, code, start_on, end_on)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-27', N'2026-09-13', N'2027-05-02');
  COMMIT TRANSACTION;
END
GO

-- ── 4. competitions：企業甲級足球聯賽（規劃書行 1463 提到的具名賽事系列範例） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'65072c87-355a-51b1-a7ac-ab12025e1ed6';
  INSERT INTO competitions (id, club_id, season_id, code, comp_type, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), N'enterprise-a', N'league', N'published');
  -- 只插 zh-Hant：英文正式賽事名稱規劃書與 docs 均未給，不臆造翻譯（docs/12c 側表原則：無來源不硬填）。
  INSERT INTO competitions_i18n (competition_id, locale, name) VALUES (@id, N'zh-Hant', N'企業甲級足球聯賽');
  COMMIT TRANSACTION;
END
GO

-- ── 6. players：players.json 共 28 筆，team_id 一律 D1 ─────────────────────
DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 25;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'81e93288-fa80-5b46-a849-8d25a3698ac9';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'igor-zavis', 25, N'GK');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'伊戈・澤維斯');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Igor Zavis');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'igor-zavis'
WHERE id = N'81e93288-fa80-5b46-a849-8d25a3698ac9' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 70;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0bf7f09e-c0fb-5e33-8bfb-b5f4af25f846';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'andrea-boschi', 70, N'GK');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'安德烈亞・柏思祺');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Andrea Boschi');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'andrea-boschi'
WHERE id = N'0bf7f09e-c0fb-5e33-8bfb-b5f4af25f846' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 99;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c89e2728-1e82-5244-b54c-edb6626d510f';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-99', 99, N'GK');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'林駿樺');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-99'
WHERE id = N'c89e2728-1e82-5244-b54c-edb6626d510f' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 4;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'55a0c420-2ff9-559a-a54d-bb2a6286c233';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-4', 4, N'DF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'蔡俊昇');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-4'
WHERE id = N'55a0c420-2ff9-559a-a54d-bb2a6286c233' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 6;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1ea54380-a049-58da-b228-be1b1024800e';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-6', 6, N'DF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'孫恩祈');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-6'
WHERE id = N'1ea54380-a049-58da-b228-be1b1024800e' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 12;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'677fae19-73a8-52b0-863e-300db1e9c396';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-12', 12, N'DF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'李毓霖');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-12'
WHERE id = N'677fae19-73a8-52b0-863e-300db1e9c396' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 24;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9c5e2be8-e0b4-5330-94c5-4b926d61a6ca';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'dominik-limprecht', 24, N'DF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'多米尼克・林普瑞希特');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Dominik Limprecht');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'dominik-limprecht'
WHERE id = N'9c5e2be8-e0b4-5330-94c5-4b926d61a6ca' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 48;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'75cf5148-1a27-5417-b79e-ee2079ddc45f';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-48', 48, N'DF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'王義友');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-48'
WHERE id = N'75cf5148-1a27-5417-b79e-ee2079ddc45f' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 66;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ba6ab51a-eace-556a-a03b-1438c7511995';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-66', 66, N'DF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'曾畇浩');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-66'
WHERE id = N'ba6ab51a-eace-556a-a03b-1438c7511995' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 78;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4ec69fe4-b0ee-587f-82fa-580a1148c768';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'lorenzo-costa', 78, N'DF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'羅倫佐・柯思達');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Lorenzo Costa');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'lorenzo-costa'
WHERE id = N'4ec69fe4-b0ee-587f-82fa-580a1148c768' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 5;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0cc6c01e-a341-5b61-9202-85a0dd75c20d';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-5', 5, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'周宇杰');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-5'
WHERE id = N'0cc6c01e-a341-5b61-9202-85a0dd75c20d' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 7;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'78cdfc0c-2d3c-5327-bf27-b69c6f9e48b2';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-7', 7, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'龔致宇');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-7'
WHERE id = N'78cdfc0c-2d3c-5327-bf27-b69c6f9e48b2' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 10;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9d322264-d203-545f-b817-bcf174e6739e';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'filip-ime-ek', 10, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'菲利普・希梅哲克');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Filip Šimeček');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'filip-ime-ek'
WHERE id = N'9d322264-d203-545f-b817-bcf174e6739e' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 11;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2d1e5240-48e6-5bfe-b025-88c8bbc0498e';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-11', 11, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'楊朝景');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-11'
WHERE id = N'2d1e5240-48e6-5bfe-b025-88c8bbc0498e' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 13;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'54c98904-723e-5952-be50-a282c38a3d86';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-13', 13, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'陳柏崴');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-13'
WHERE id = N'54c98904-723e-5952-be50-a282c38a3d86' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 16;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4847ce98-75f4-532c-a159-e72f671397e3';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-16', 16, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'魏志荃');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-16'
WHERE id = N'4847ce98-75f4-532c-a159-e72f671397e3' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 18;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'823939ce-754c-529d-87e8-e0f86373df98';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'nichita-josan', 18, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'尼基塔・若桑');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Nichita Josan');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'nichita-josan'
WHERE id = N'823939ce-754c-529d-87e8-e0f86373df98' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 27;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fb00ca83-e024-557f-861d-df31a279acab';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-27', 27, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'施靖堂');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-27'
WHERE id = N'fb00ca83-e024-557f-861d-df31a279acab' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 29;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4a48104a-e442-5b93-b726-d0db5928a8e5';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-29', 29, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'江均堯');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-29'
WHERE id = N'4a48104a-e442-5b93-b726-d0db5928a8e5' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 32;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cd906d1e-eed0-5110-965c-6d160c965e7c';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-32', 32, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'柯岳廷');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-32'
WHERE id = N'cd906d1e-eed0-5110-965c-6d160c965e7c' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 35;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c95819c5-ceae-5801-8c07-fc04b0871ebb';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-35', 35, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'李鴻均');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-35'
WHERE id = N'c95819c5-ceae-5801-8c07-fc04b0871ebb' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 37;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ddb76f26-bf71-5f02-a6fa-f5533ef33d65';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-37', 37, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'梁顥騰');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-37'
WHERE id = N'ddb76f26-bf71-5f02-a6fa-f5533ef33d65' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 45;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1351ef77-57af-5022-8efb-4d3c18e75c11';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-45', 45, N'MF');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'胡淯翔');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-45'
WHERE id = N'1351ef77-57af-5022-8efb-4d3c18e75c11' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 9;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5af62492-68a5-5c5f-a778-260533783577';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-9', 9, N'FW');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'劉建緯');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-9'
WHERE id = N'5af62492-68a5-5c5f-a778-260533783577' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 14;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5d8764df-1ff2-5222-ac6e-dde2d0e1134e';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-14', 14, N'FW');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'李偉綸');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-14'
WHERE id = N'5d8764df-1ff2-5222-ac6e-dde2d0e1134e' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 28;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f6c6d585-b256-5151-850d-fad180fd87c8';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-28', 28, N'FW');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'陳治瑋');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-28'
WHERE id = N'f6c6d585-b256-5151-850d-fad180fd87c8' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 44;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'28f71881-6f0e-5a27-b4df-dbc4e6174ee9';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-44', 44, N'FW');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'山內大空');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-44'
WHERE id = N'28f71881-6f0e-5a27-b4df-dbc4e6174ee9' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'D1') AND pl.shirt_no = 77;
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a449c631-3c74-552d-b62f-3247ccb83461';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no, position)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'd1-77', 77, N'FW');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'林偉傑');
  -- 無英文姓名（players.json name_en 為空字串），不插入 en 列
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'd1-77'
WHERE id = N'a449c631-3c74-552d-b62f-3247ccb83461' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

-- ── 7. staff：staff.json 共 8 筆 ──────────────────────────────
-- coaches-d1.json 的成員連到 D1（staff_teams）；coaches-academy.json 的成員
-- （青訓教練／青訓總監）**不連結任何 Team**——來源 JSON 沒有標明是 U15／U14／U12
-- 哪一隊，本俱樂部學院球隊（U15／U14／U12）本次也未建立（無球員名單佐證需要建隊）。
-- 這是刻意的欄位留白，不是遺漏，見本次回報「落差清單」。

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = N'儒利亞諾・羅德里格斯' AND si.title = N'守門員教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'16c81168-1fc3-56f1-833d-e916427a9626';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'));
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', N'儒利亞諾・羅德里格斯', N'守門員教練');
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', N'Juliano Rodrigues', NULL);
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = N'托馬斯・卡斯泰洛' AND si.title = N'教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2d4ad822-d792-52b7-b5c0-70ae6ef1b2d8';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'));
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', N'托馬斯・卡斯泰洛', N'教練');
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', N'Thomas Castello', NULL);
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = N'瑪蒂諾' AND si.title = N'總教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8d1061b6-11c2-56dd-bd97-415dc94cccff';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'));
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', N'瑪蒂諾', N'總教練');
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', N'Matino Sofia', NULL);
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = N'許志傑' AND si.title = N'青訓教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1f5258ed-0aa8-50e8-829b-3c67a972ec8a';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'));
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', N'許志傑', N'青訓教練');
  -- 無英文姓名，不插入 en 列
  -- 不連結 Team（見上方說明）
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = N'黃聖傑' AND si.title = N'青訓教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3bf57239-023d-54bf-b2b3-86a38a70b066';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'));
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', N'黃聖傑', N'青訓教練');
  -- 無英文姓名，不插入 en 列
  -- 不連結 Team（見上方說明）
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = N'徐翊' AND si.title = N'青訓總監';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fe64746f-bee7-5d8d-97be-86b6b8b61008';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'));
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', N'徐翊', N'青訓總監');
  -- 無英文姓名，不插入 en 列
  -- 不連結 Team（見上方說明）
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = N'陳曉明' AND si.title = N'顧問';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b26d447f-5886-543a-9214-7242579ab5cd';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'));
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', N'陳曉明', N'顧問');
  -- 無英文姓名，不插入 en 列
  -- 不連結 Team（見上方說明）
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = N'江奕璠' AND si.title = N'體能教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'175c1aa4-83fe-551a-8146-b3b8a20f706e';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'));
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', N'江奕璠', N'體能教練');
  -- 無英文姓名，不插入 en 列
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

-- ── 8. matches：schedule.json 共 21 筆，全數為 D1／企業甲級聯賽 ────────
-- ⚠️ schedule.json 沒有比分（本來就是賽程表不是賽果表），status 一律 N'scheduled'。
-- match_no（聯賽官方場次編號）v3.11 才補進 matches 表；下面每筆除了原本的
-- 「找不到才 INSERT」區塊，另外接一句獨立的 UPDATE 用業務自然鍵補 match_no——
-- 這是為了讓「DDL 追加欄位、既有本機庫在欄位補進之前已經種過資料」這種情況可以單靠
-- 重跑本腳本補齊，不必整庫重建（見 db/seed/README.md「DDL 改了但本機庫沒跟上」）。
-- UPDATE 故意不放進上面 IF @id IS NULL 的 BEGIN/END 區塊——那個區塊只在列不存在時
-- 執行，既有列永遠補不到；獨立成一句可重複執行的 UPDATE 才能兩種情況都涵蓋。

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 1 AND mt.match_on = N'2026-09-13' AND mt.opponent = N'高雄先鋒';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4445a43b-2b42-5d60-9eab-b5d21730b4f5';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-09-13', N'19:00', N'AWAY', N'高雄先鋒', N'league', N'scheduled', 1, 3);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'楠梓足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 3
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 1 AND match_on = N'2026-09-13' AND opponent = N'高雄先鋒';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 2 AND mt.match_on = N'2026-09-20' AND mt.opponent = N'台灣電力';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1239610c-bb45-5e89-863b-d88069ffb12b';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-09-20', N'16:30', N'HOME', N'台灣電力', N'league', N'scheduled', 2, 6);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 6
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 2 AND match_on = N'2026-09-20' AND opponent = N'台灣電力';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 3 AND mt.match_on = N'2026-10-11' AND mt.opponent = N'陽信北競';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6eb00ead-d87b-5886-9794-4559f2d89eb9';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-10-11', N'16:00', N'AWAY', N'陽信北競', N'league', N'scheduled', 3, 12);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'汐止綜合運動場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 12
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 3 AND match_on = N'2026-10-11' AND opponent = N'陽信北競';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 4 AND mt.match_on = N'2026-10-18' AND mt.opponent = N'南市台鋼';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c0c1f6b3-7d38-5795-8972-76697e814efb';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-10-18', N'18:30', N'AWAY', N'南市台鋼', N'league', N'scheduled', 4, 13);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台南市立足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 13
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 4 AND match_on = N'2026-10-18' AND opponent = N'南市台鋼';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 5 AND mt.match_on = N'2026-10-25' AND mt.opponent = N'台中FUTURO';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'14fe7602-33a6-5650-8991-c703ae1e60e1';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-10-25', N'19:00', N'HOME', N'台中FUTURO', N'league', N'scheduled', 5, 20);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 20
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 5 AND match_on = N'2026-10-25' AND opponent = N'台中FUTURO';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 6 AND mt.match_on = N'2026-11-01' AND mt.opponent = N'新北航源';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fa770cda-f1a6-5f9b-8636-abbffa7e090e';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-11-01', N'19:00', N'AWAY', N'新北航源', N'league', N'scheduled', 6, 22);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'輔仁大學足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 22
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 6 AND match_on = N'2026-11-01' AND opponent = N'新北航源';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 7 AND mt.match_on = N'2026-11-22' AND mt.opponent = N'大同足球';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'03f501d8-caff-5493-a881-814e229df668';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-11-22', N'15:30', N'HOME', N'大同足球', N'league', N'scheduled', 7, 27);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'TBC');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 27
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 7 AND match_on = N'2026-11-22' AND opponent = N'大同足球';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 8 AND mt.match_on = N'2026-11-29' AND mt.opponent = N'高雄先鋒';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'87a7eaf4-c835-5b47-96a8-0a87c421e97a';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-11-29', N'19:00', N'HOME', N'高雄先鋒', N'league', N'scheduled', 8, 31);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 31
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 8 AND match_on = N'2026-11-29' AND opponent = N'高雄先鋒';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 9 AND mt.match_on = N'2026-12-06' AND mt.opponent = N'台灣電力';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fbaefb17-8433-5e47-8a7f-064701a5b5c4';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-12-06', N'15:30', N'AWAY', N'台灣電力', N'league', N'scheduled', 9, 34);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'楠梓足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 34
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 9 AND match_on = N'2026-12-06' AND opponent = N'台灣電力';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 10 AND mt.match_on = N'2026-12-13' AND mt.opponent = N'陽信北競';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e7f0af6b-42a2-56c4-88b6-d89e2d2dcff9';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2026-12-13', N'19:00', N'HOME', N'陽信北競', N'league', N'scheduled', 10, 40);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 40
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 10 AND match_on = N'2026-12-13' AND opponent = N'陽信北競';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 11 AND mt.match_on = N'2027-01-24' AND mt.opponent = N'南市台鋼';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'df1b91e5-3e94-532c-a1a4-2f5aa846bf2d';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-01-24', N'16:00', N'HOME', N'南市台鋼', N'league', N'scheduled', 11, 41);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 41
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 11 AND match_on = N'2027-01-24' AND opponent = N'南市台鋼';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 12 AND mt.match_on = N'2027-01-31' AND mt.opponent = N'台中FUTURO';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'91d03fb3-55a0-5e9d-bc99-187aad60523d';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-01-31', N'19:00', N'AWAY', N'台中FUTURO', N'league', N'scheduled', 12, 48);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 48
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 12 AND match_on = N'2027-01-31' AND opponent = N'台中FUTURO';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 13 AND mt.match_on = N'2027-02-21' AND mt.opponent = N'新北航源';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'053920c5-d084-5ca8-8551-b8b6e04a5cef';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-02-21', N'19:00', N'HOME', N'新北航源', N'league', N'scheduled', 13, 50);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 50
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 13 AND match_on = N'2027-02-21' AND opponent = N'新北航源';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 14 AND mt.match_on = N'2027-02-28' AND mt.opponent = N'大同足球';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7a9afb6f-f18e-59aa-87fe-1e85fdeff424';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-02-28', N'16:00', N'AWAY', N'大同足球', N'league', N'scheduled', 14, 55);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'輔仁大學足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 55
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 14 AND match_on = N'2027-02-28' AND opponent = N'大同足球';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 15 AND mt.match_on = N'2027-03-07' AND mt.opponent = N'高雄先鋒';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e359485a-8a7f-51a9-a389-3a5d76449e2e';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-03-07', N'19:00', N'AWAY', N'高雄先鋒', N'league', N'scheduled', 15, 59);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'楠梓足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 59
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 15 AND match_on = N'2027-03-07' AND opponent = N'高雄先鋒';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 16 AND mt.match_on = N'2027-03-14' AND mt.opponent = N'台灣電力';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4fc1f0d5-24bc-5059-8b43-a030940e672a';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-03-14', N'16:00', N'HOME', N'台灣電力', N'league', N'scheduled', 16, 62);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 62
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 16 AND match_on = N'2027-03-14' AND opponent = N'台灣電力';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 17 AND mt.match_on = N'2027-03-21' AND mt.opponent = N'陽信北競';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7f4efa11-2c5b-56b5-b0c2-336f398ff481';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-03-21', N'15:30', N'AWAY', N'陽信北競', N'league', N'scheduled', 17, 68);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'汐止綜合運動場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 68
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 17 AND match_on = N'2027-03-21' AND opponent = N'陽信北競';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 18 AND mt.match_on = N'2027-04-11' AND mt.opponent = N'南市台鋼';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a0731c9b-8f80-503c-88c6-4172ba87d3c2';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-04-11', N'18:30', N'AWAY', N'南市台鋼', N'league', N'scheduled', 18, 69);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台南市立足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 69
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 18 AND match_on = N'2027-04-11' AND opponent = N'南市台鋼';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 19 AND mt.match_on = N'2027-04-18' AND mt.opponent = N'台中FUTURO';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6951be96-a6ac-5698-ad53-21246f86fbe3';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-04-18', N'19:00', N'HOME', N'台中FUTURO', N'league', N'scheduled', 19, 76);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 76
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 19 AND match_on = N'2027-04-18' AND opponent = N'台中FUTURO';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 20 AND mt.match_on = N'2027-04-25' AND mt.opponent = N'新北航源';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ab4372c9-7476-5f8a-b73a-dff065a94838';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-04-25', N'15:30', N'AWAY', N'新北航源', N'league', N'scheduled', 20, 78);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台北田徑場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 78
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 20 AND match_on = N'2027-04-25' AND opponent = N'新北航源';
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
    AND mt.round_no = 21 AND mt.match_on = N'2027-05-02' AND mt.opponent = N'大同足球';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b056ddcd-5e70-56f6-b988-19466e04a7ad';
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'enterprise-a'), N'2027-05-02', N'16:30', N'HOME', N'大同足球', N'league', N'scheduled', 21, 83);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

UPDATE matches SET match_no = 83
WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27')
  AND round_no = 21 AND match_on = N'2027-05-02' AND opponent = N'大同足球';
GO

-- ── 9. articles：news.json 共 83 筆。slug 全站唯一，直接沿用 JSON 既有 slug。 ──
-- cover_key 留 NULL：JSON 的 cover／cover_web 是 mockup 靜態資源路徑，不是走過
-- 「上傳即縮圖」pipeline 後的 Blob object key（docs/14），兩者不能混用，此為已知落差。
-- body 留 NULL：news.json 的 body_zh 全部是 null（文稿仍是 .gdoc 捷徑讀不到，docs/07）。

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-08-10-international-000';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fc7b9fe2-ba11-5c29-b3c8-716b467de0b1';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-08-10-international-000', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2026-08-10T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石與AS Trenčín深化青訓合作　共創台斯足球交流新篇章');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-05-24-match-001';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f8d50d69-3bb1-5aff-a7dd-00c2580f7f05';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-05-24-match-001', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-05-24T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 3-0 銘傳大學');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-05-17-match-002';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'81e6b0c1-762b-5c52-a9b9-0d60e800935c';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-05-17-match-002', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-05-17T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 1-2 陽信北競');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-05-10-match-003';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'23625ada-1f10-5d26-a42b-4fbf76a02dff';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-05-10-match-003', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-05-10T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 2-4 南市台鋼');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-05-09-match-004';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1a4f1c0d-9f75-5f3a-ad64-75b8181e1e73';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-05-09-match-004', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-05-09T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 陽信北競預備隊 5-0 台中磐石預備隊');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-05-03-match-005';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'aa85e082-99bb-57bb-8738-f3bb2e156f73';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-05-03-match-005', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-05-03T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 台中磐石預備隊 0-2 銘傳Desafio');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-05-03-match-006';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e945abf4-1ba8-5ed1-a98f-a410a653b6ef';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-05-03-match-006', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-05-03T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 1-0 大同足球');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-04-27-match-007';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5255b952-1701-5880-86ae-74bb4236b66e';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-04-27-match-007', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-04-27T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 2-2 台電');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-04-25-match-008';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3095a278-5e4b-548c-bc8d-1f51f0e5fbe9';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-04-25-match-008', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-04-25T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 桃園國際 1-1 台中磐石預備隊');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-04-19-match-009';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2c63283b-e9e9-5193-9b1d-b0515a7d9ee9';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-04-19-match-009', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-04-19T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中未來 0-1 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-04-18-match-010';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a0ead13a-5d46-5fbe-b2f7-abad93ebb7e1';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-04-18-match-010', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-04-18T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 台中磐石預備隊 4-1 灣島');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-04-12-match-011';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd4acc713-a89d-545c-8ee8-5526fc167598';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-04-12-match-011', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-04-12T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 新北航源 0-2 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-04-11-match-012';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'193f2a41-2b53-51ad-8ec9-36e4f8d4b5ef';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-04-11-match-012', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-04-11T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 高雄先鋒 4-1 台中磐石預備隊');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-03-22-match-013';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0b5b712f-17c0-5ad8-9af0-c3566f378fb1';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-03-22-match-013', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-03-22T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 台中磐石預備隊 5-2 新北航源輔大');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-03-09-match-014';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9198fbf9-0666-5138-afbe-fa4b82e821dd';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-03-09-match-014', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-03-09T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 1-1 銘傳大學');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-03-02-match-015';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f1ce44a3-b072-513f-a597-7327ed61e5bb';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-03-02-match-015', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-03-02T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 3-1 陽信北競');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-02-06-international-016';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1280a0e4-72c8-5b4e-8534-a0f3fb933a66';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-02-06-international-016', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2026-02-06T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'持續拓展國際視野 台中磐石5球員獲義大利萊尼亞戈點名赴義訓練');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2026-01-12-community-017';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2b78fadf-5853-50f1-b0a2-60b74cb2ada3';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2026-01-12-community-017', (SELECT id FROM article_categories WHERE code = N'community'), N'published', N'2026-01-12T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石攜手Subkarma深耕在地公益　捐贈英語書籍走進潭秀非營利幼兒園');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-12-22-match-018';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fc1b6d73-4147-50ed-93b5-7353d9ff74ec';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-12-22-match-018', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-12-22T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 0-0 南市台鋼');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-12-14-match-019';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fd0aa8be-72fe-529f-8d16-c1d87ea0f01f';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-12-14-match-019', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-12-14T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 0-1 大同足球隊');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-12-14-match-020';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'38d86750-587e-5e32-8163-a610fa92c9ac';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-12-14-match-020', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-12-14T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台灣電力 1-2 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-12-09-match-021';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e5e70b1d-d654-572f-a05d-7544f8e79482';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-12-09-match-021', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-12-09T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台灣電力 1-2 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-12-07-match-022';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'64090cca-d288-575e-9536-74f6bfa9adc6';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-12-07-match-022', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-12-07T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台灣電力 1-2 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-12-01-match-023';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cb4bac5f-4429-53d2-8e98-3b98a890da11';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-12-01-match-023', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-12-01T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中FUTURO 4-0 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-11-24-match-024';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'227800a7-7bca-5dfa-bedd-77d91a6c11c2';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-11-24-match-024', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-11-24T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 銘傳大學Desafio 1-3 台中磐石足球預備隊');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-11-24-match-025';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'405b92c9-1847-52de-ad47-7b3104cbaf19';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-11-24-match-025', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-11-24T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽：新北航源 3-0 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-11-04-international-026';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e7e6f36a-d003-5ab7-8464-890c12576a94';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-11-04-international-026', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-11-04T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'與義甲維羅納合作邁出第一步 台中磐石球員啟程赴義大利訓練');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-11-03-club-027';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'296e9d54-301a-5995-98d9-ab66497c2bd0';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-11-03-club-027', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-11-03T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'陳曉明出任台中磐石足球俱樂部技術顧問');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-11-02-match-028';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b0b4517a-67f2-5ff3-b25a-672453225e58';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-11-02-match-028', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-11-02T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 銘傳大學 0-1 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-11-01-match-029';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd095ea19-f2dd-5ef6-a0bf-699c3d151a8e';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-11-01-match-029', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-11-01T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 台中磐石預備隊 2-0 桃園國際');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-10-26-match-030';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'638e2287-9e6a-51c7-9c1e-50f5c16c1419';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-10-26-match-030', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-10-26T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 灣島 2-7 台中磐石預備隊');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-10-26-match-031';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ba59971e-f3a5-5c97-a641-1e47d8965911';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-10-26-match-031', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-10-26T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 陽信北競 2-2 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-09-28-match-032';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3497ff49-4755-5946-93e2-4dcce093a0d4';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-09-28-match-032', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-09-28T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 南市台鋼 3-3 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-09-21-match-033';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'99876136-b68d-5383-802a-83b384b98e58';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-09-21-match-033', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-09-21T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 大同足球 2-2 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-09-20-match-034';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2d423d5f-29fe-5767-b400-f8110f97c2b3';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-09-20-match-034', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-09-20T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 新北航源輔大 1-0 台中磐石預備隊');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-09-14-match-035';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'54a3dc62-16d3-5459-a639-8faef69583a0';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-09-14-match-035', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-09-14T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 0-2 台灣電力');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-08-24-match-036';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c3fc25a9-f052-5aa3-bafe-28fe13e41c7d';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-08-24-match-036', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-08-24T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽：台中磐石 0-1 台中未來');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-08-18-match-037';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2a3b1b80-c5fe-546b-b27e-e4481666e229';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-08-18-match-037', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-08-18T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'企甲聯賽 台中磐石 2-1 新北航源');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-31-intcup-038';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1b1b8b67-9f69-576a-936e-61459b2ee061';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-31-intcup-038', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-07-31T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石國際足球盃 - 台中磐石 vs 東方');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-31-intcup-039';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4ef04742-6018-5e9f-8229-778d7aa08e78';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-31-intcup-039', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-07-31T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石國際足球盃 - 紅白艾倫 vs 台中Futuro');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-30-international-040';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6d3169e9-7c11-5985-aa9d-7f6f5c335f10';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-30-international-040', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-07-30T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石將與義甲球會 Hellas Verona 簽署合作備忘錄攜手推動台義足球交流');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-29-intcup-041';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8aff7040-1a02-59a1-94d3-bc490d933d2d';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-29-intcup-041', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-07-29T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石國際足球盃 - 台中磐石 vs 紅白艾倫');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-29-intcup-042';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f5698e9d-3d81-5e93-9917-0b9f2532f725';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-29-intcup-042', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-07-29T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石國際足球盃 - 東方 vs 台中Futuro');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-27-international-043';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'89e807a0-c036-5803-8bda-5f6f90f54541';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-27-international-043', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-07-27T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石與德國 Rot Weiss Ahlen簽署合作諒解備忘錄');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-27-intcup-044';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1f78e1b1-8b60-540e-92ca-6a11fba7fd0e';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-27-intcup-044', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-07-27T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石國際足球盃 - 台中磐石 vs 台中未來');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-27-intcup-045';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'07bf65b9-b336-544c-ac2e-e7620bf0a0a5';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-27-intcup-045', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-07-27T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石國際足球盃 - 紅白艾倫 vs 東方');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-25-club-046';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c9915018-8064-57ac-a056-0edde40b63c9';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-25-club-046', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-07-25T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'2025台中磐石國際足球盃 記者會');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-21-community-047';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'be07bdfd-52a3-5fe8-a688-6e6040ad8ab1';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-21-community-047', (SELECT id FROM article_categories WHERE code = N'community'), N'published', N'2025-07-21T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'臺中市政府運動局授旗典禮');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-12-club-048';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ba12af22-d0b4-5727-9f88-44b854f72ba6';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-12-club-048', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-07-12T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'2025台中磐石國際足球盃熱血開踢');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-07-08-club-049';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'104be69e-bf0e-5538-8b70-9e2cca623669';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-07-08-club-049', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-07-08T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石與龜記茗品繼續攜手合作');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-06-11-community-050';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'60f0a6a5-0688-5dc5-8fc1-6c66d345f85f';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-06-11-community-050', (SELECT id FROM article_categories WHERE code = N'community'), N'published', N'2025-06-11T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石出席南投縣雙龍國小畢業典禮');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-05-17-match-051';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c3790ea4-7c27-540e-b5cb-d0bd44cfd2d0';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-05-17-match-051', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-05-17T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃八強賽 台灣電力 1-1 (PK 4-2) 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-05-13-match-052';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b425c738-92fd-5642-b5a5-7dbe2551bfe0';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-05-13-match-052', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-05-13T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃 台中磐石 2-2 高大國光');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-05-13-match-053';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cb514542-f44a-544f-988d-f0e1980fa62a';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-05-13-match-053', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-05-13T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃 台中磐石預備隊 8-1 銘傳B');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-05-04-match-054';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e9796735-b2ec-5630-b106-acab804b14e8';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-05-04-match-054', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-05-04T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃 台中磐石 1-5 南市台鋼藍');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-05-04-match-055';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5f258565-7ad2-55b9-92b0-675864e27488';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-05-04-match-055', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-05-04T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃 台中磐石預備隊 1-2 台中FUTURO');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-05-03-camps-056';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b388439f-65d8-5554-87b0-16febc5ef092';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-05-03-camps-056', (SELECT id FROM article_categories WHERE code = N'camps-events'), N'published', N'2025-05-03T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'2025台中磐石盃足球邀請賽');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-29-match-057';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c368c634-46d5-5544-afdf-15fc66bd932e';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-29-match-057', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-04-29T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃 桃園國際灰 0-7 台中磐石預備隊');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-28-match-058';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0e0eaf2e-103b-53ab-9a11-7cc7bc33b917';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-28-match-058', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-04-28T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃陽信北競預備隊 2-3 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-12-match-059';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0eb7aaa5-e576-5ae9-86d2-fb778723485d';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-12-match-059', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-04-12T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃 台中磐石 2-1 新北輔大');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-12-match-060';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7eab4b29-c5b0-5d98-98c6-376724d33782';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-12-match-060', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-04-12T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'總統盃 台中磐石預備隊 1-5 南市台鋼綠');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-11-club-061';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'417905e4-bfb5-5eba-ace4-8d7c5d8896ee';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-11-club-061', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-04-11T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'周宇杰加盟台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-11-club-062';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1711856f-2a62-588d-9aa6-6fc80e83156a';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-11-club-062', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-04-11T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'廖奕盛加盟台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-11-club-063';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'33481619-933c-5378-aa94-49b8ada43fb0';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-11-club-063', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-04-11T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'旅德好手王義友加盟台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-08-international-064';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'923c9f80-b91b-577a-bb6e-895036bbfc18';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-08-international-064', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-04-08T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'孫恩祈獲邀續留西班牙 征戰至本季結束');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-07-match-065';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'87181ce9-7b47-5854-8dc8-bb7342ffe900';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-07-match-065', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-04-07T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'俱樂部友誼賽 陽信北競 2-1 台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-07-international-066';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e04523b8-6ddf-51d0-8752-50d1a89cb5a5';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-07-international-066', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-04-07T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'梁顥騰完成西班牙訓練行程 深受啟發');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-03-match-067';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'46bfeec5-f209-565c-b09b-fda761b62652';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-03-match-067', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-04-03T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'熱身賽第四戰 陽信北競對台中磐石');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-04-02-match-068';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1474dbcf-5b25-5565-940a-02b4fa3694d1';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-04-02-match-068', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-04-02T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石雙隊出征2025全國總統盃');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-03-29-match-069';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'db215722-f34d-5736-8db6-ca5e8451f4ab';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-03-29-match-069', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-03-29T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'熱身賽 台中磐石 1-3 陽信北競');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-03-22-match-070';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c135f206-ffa3-562a-9e9d-03e898c963aa';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-03-22-match-070', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-03-22T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'熱身賽第三戰 台中磐石對陽信北競');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-03-17-club-071';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f96f885f-5322-56df-974e-e46ba434d4f0';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-03-17-club-071', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-03-17T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'高冠宇獲中華隊徵召');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-03-15-match-072';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'91be551d-a197-5a6c-b8bf-11d06a741d77';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-03-15-match-072', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-03-15T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'熱身賽 台中磐石對台中Futuro');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-03-13-match-073';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd110111d-c211-5130-a4a9-56c7e48e6c77';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-03-13-match-073', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2025-03-13T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石熱身迎戰台中Futuro');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-03-04-international-074';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a05443a5-bd45-5d08-98eb-ee0894ca17e4';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-03-04-international-074', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-03-04T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'兩名球員參與西班牙 RC Alcobendas 訓練');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-02-28-international-075';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2e268f8e-d1ad-52eb-b04f-f9875da1ee2d';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-02-28-international-075', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-02-28T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'四名球員參與東京農業大學足球隊訓練');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-02-19-international-076';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'62818b5e-bf8f-5e81-9bfd-52fd065579ee';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-02-19-international-076', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-02-19T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'日本移地訓練總結');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-02-04-international-077';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6f62bc53-1be4-5234-8a22-ab14a0632717';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-02-04-international-077', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2025-02-04T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'楊朝景轉港超聯九龍城');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2025-01-07-club-078';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3cf0c697-d8f2-5ef1-8920-44be876c10db';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2025-01-07-club-078', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2025-01-07T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石獲臺中市政府運動局在合作及冠名上的認可');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2024-12-18-club-079';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cedea152-3ca4-55a2-8d72-d68961466994';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2024-12-18-club-079', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2024-12-18T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石有條件地通過甲級俱樂部認證');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2024-12-18-club-080';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'66c14990-b3fb-5cfc-a64f-63a27edd7820';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2024-12-18-club-080', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2024-12-18T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'林教練得最佳教練獎、楊朝景得金靴獎');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2024-12-07-match-081';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2f26648b-0e74-5bab-97f1-5150b562c1cf';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2024-12-07-match-081', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2024-12-07T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'乙級聯賽 台中磐石 9-0 銘傳大學Desafio');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'2024-11-05-international-082';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'44547162-1351-50e0-8869-8eb8fe879764';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'2024-11-05-international-082', (SELECT id FROM article_categories WHERE code = N'international'), N'published', N'2024-11-05T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', N'台中磐石與RC Alcobendas達成合作協議');
  COMMIT TRANSACTION;
END
GO

-- ── 10. venues：藍鯨主場兩座（太原足球場／豐原體育場，不帶 club_id） ─────────
DECLARE @id uniqueidentifier;
SELECT @id = vi.venue_id FROM venues_i18n vi WHERE vi.locale = N'zh-Hant' AND vi.name = N'台中北屯太原足球場';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5603c002-59a6-5d79-8097-87e8e68439c1';
  INSERT INTO venues (id, sort_order) VALUES (@id, 0);
  INSERT INTO venues_i18n (venue_id, locale, name, address, directions)
  VALUES (@id, N'zh-Hant', N'台中北屯太原足球場', N'台中市北屯區建軍一街6-8號', N'太原火車站至太原足球場約2.2公里。公車：總達客運246號、統聯客運81號、中台灣客運20號可達「澄清／弘光醫院（太原路）」站；亦可租借UBike，騎乘約10分鐘。管理單位：臺灣體育運動大學（管理電話 (04)2221-3108 #2273）。觀眾席約400人（階梯椅），人工草皮。2017年啟用，現為一線隊主場。');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = vi.venue_id FROM venues_i18n vi WHERE vi.locale = N'zh-Hant' AND vi.name = N'台中市立豐原體育場';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cefd200d-1fd2-56d5-93a3-7b46240813ad';
  INSERT INTO venues (id, sort_order) VALUES (@id, 0);
  INSERT INTO venues_i18n (venue_id, locale, name, address, directions)
  VALUES (@id, N'zh-Hant', N'台中市立豐原體育場', N'台中市豐原區豐北街221號', N'豐原火車站至豐原體育場約1.5公里。公車：豐原客運92號、63號，全航客運12號可達「豐原國中」站；亦可租借UBike，騎乘約5分鐘。管理單位：豐原國中（管理電話 (04)2525-1200 #107）。觀眾席約20000人，天然草皮。（原文「自行開車」段落本身即為空白，未收錄）');
  COMMIT TRANSACTION;
END
GO

-- ── 11. seasons：藍鯨 2023／2025 兩個球季（球季不與磐石同步，docs/13 踩雷點 6/14） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'695b36f8-f0f2-56eb-a2f7-517f3a3d6f6d';
  INSERT INTO seasons (id, club_id, code, start_on, end_on)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2023', N'2023-04-22', N'2023-12-16');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7fbb6764-0daf-502f-9de8-be27e45d186b';
  INSERT INTO seasons (id, club_id, code, start_on, end_on)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2025', N'2025-04-23', N'2025-06-15');
  COMMIT TRANSACTION;
END
GO

-- ── 12. competitions：藍鯨木蘭聯賽（2023）／總統盃（2025） ──────────────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f477da34-8635-5ed4-b162-31fcf321bc61';
  INSERT INTO competitions (id, club_id, season_id, code, comp_type, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), N'mulan', N'league', N'published');
  INSERT INTO competitions_i18n (competition_id, locale, name) VALUES (@id, N'zh-Hant', N'台灣木蘭女子足球聯賽');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'presidents-cup';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7eec4a2e-581a-51ed-bf56-d18033c3f21c';
  INSERT INTO competitions (id, club_id, season_id, code, comp_type, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025'), N'presidents-cup', N'cup', N'published');
  INSERT INTO competitions_i18n (competition_id, locale, name) VALUES (@id, N'zh-Hant', N'2025全國總統盃足球錦標賽');
  COMMIT TRANSACTION;
END
GO

-- ── 13. matches：藍鯨 2023 木蘭聯賽 15 場 ＋ 2025 總統盃 6 場 ─────────────────
DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-04-22' AND mt.opponent = N'花蓮';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd8835d94-f985-5df7-981c-2f603c9f9a5a';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), NULL, N'2023-04-22', NULL, NULL, N'花蓮', N'league', N'played', 2, 2, 1, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'花蓮美崙國中足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-04-29' AND mt.opponent = N'新北航源';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9da66c37-4478-5a71-bf59-10803ff2fbe4';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), NULL, N'2023-04-29', NULL, NULL, N'新北航源', N'league', N'played', 0, 1, 1, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'新北輔仁大學足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-04-29' AND mt.opponent = N'桃園戰神';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a4454f4c-4f12-5f4a-b4db-61c9cbbdaedc';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), NULL, N'2023-04-29', NULL, NULL, N'桃園戰神', N'league', N'played', 2, 0, 1, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'新北輔仁大學足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-05-13' AND mt.opponent = N'臺北熊讚';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'590b5030-2631-5e1f-9a3c-0a905f2fbce7';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2023-05-13', NULL, NULL, N'臺北熊讚', N'league', N'played', 1, 1, 1, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台中太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-05-20' AND mt.opponent = N'高雄陽信銀行';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e0ca2cf3-b930-5099-bc9c-57d6c5b20998';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), NULL, N'2023-05-20', NULL, NULL, N'高雄陽信銀行', N'league', N'played', 1, 1, 1, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'高雄市立楠梓足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-05-27' AND mt.opponent = N'花蓮';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ec405b7c-8c86-5072-8d7c-6678d984483d';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2023-05-27', NULL, NULL, N'花蓮', N'league', N'played', 0, 0, 2, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台中太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-06-18' AND mt.opponent = N'新北航源';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bccd6403-e937-5b62-a9db-75e129eb72e4';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2023-06-18', NULL, NULL, N'新北航源', N'league', N'played', 2, 1, 2, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台中太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-07-01' AND mt.opponent = N'桃園戰神';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1d737644-b978-5d6a-a418-cd9cd5cd9f77';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2023-07-01', NULL, NULL, N'桃園戰神', N'league', N'played', 3, 1, 2, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台中太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-07-08' AND mt.opponent = N'臺北熊讚';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'eb7e0fa6-7123-56fb-afe9-9fb2b403d0c8';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), NULL, N'2023-07-08', NULL, NULL, N'臺北熊讚', N'league', N'played', 1, 3, 2, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'桃園青埔足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-11-04' AND mt.opponent = N'高雄陽信銀行';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'936d409f-0074-5187-9613-d3b30a55d35f';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2023-11-04', NULL, NULL, N'高雄陽信銀行', N'league', N'played', 1, 0, 2, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'臺中太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-12-02' AND mt.opponent = N'花蓮';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'83f926c7-b5ab-547a-9b8b-3057721b35bd';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), NULL, N'2023-12-02', NULL, NULL, N'花蓮', N'league', N'played', 1, 2, 3, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'花蓮美崙國中足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-11-18' AND mt.opponent = N'新北航源';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'eb697b10-bd61-5e3a-a2ab-169d40dbb4d8';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), NULL, N'2023-11-18', NULL, NULL, N'新北航源', N'league', N'played', 1, 1, 3, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'桃園龜山銘傳大學足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-11-25' AND mt.opponent = N'桃園戰神 Mars';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'aba62e80-4237-5812-b084-c2b27da67320';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2023-11-25', NULL, NULL, N'桃園戰神 Mars', N'league', N'played', 1, 0, 3, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'臺中太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-12-09' AND mt.opponent = N'臺北熊讚';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4bf417f5-e0f3-54e1-931f-ba608aa3f86b';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2023-12-09', NULL, NULL, N'臺北熊讚', N'league', N'played', 0, 0, 3, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'臺中太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023')
    AND mt.match_on = N'2023-12-16' AND mt.opponent = N'高雄陽信銀行';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'51ba77c3-4c09-5f2c-bb5f-2d78b7bc5289';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'mulan'), NULL, N'2023-12-16', NULL, NULL, N'高雄陽信銀行', N'league', N'played', 0, 3, 3, NULL);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'高雄市立楠梓足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025')
    AND mt.match_on = N'2025-04-23' AND mt.opponent = N'新北航源';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'15ca12a1-50b6-55f7-ad5d-9e18c0a344ce';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'presidents-cup'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2025-04-23', N'16:00', N'HOME', N'新北航源', N'cup', N'played', 4, 0, NULL, 1);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台中北屯太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025')
    AND mt.match_on = N'2025-04-27' AND mt.opponent = N'陽信北競';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b03a297f-876b-54e3-9630-86858deddaa2';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'presidents-cup'), NULL, N'2025-04-27', N'13:00', N'AWAY', N'陽信北競', N'cup', N'played', 2, 7, NULL, 2);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台中西屯足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025')
    AND mt.match_on = N'2025-05-11' AND mt.opponent = N'科學城女足';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9bb1e2c5-7a22-5940-a478-0bbcd333ebc5';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'presidents-cup'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2025-05-11', N'16:00', N'AWAY', N'科學城女足', N'cup', N'played', 0, 2, NULL, 3);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台中北屯太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025')
    AND mt.match_on = N'2025-06-08' AND mt.opponent = N'台中櫻花';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7ac2f53a-2449-5271-81ad-a710afd12b64';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'presidents-cup'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2025-06-08', N'16:00', N'HOME', N'台中櫻花', N'cup', N'played', 2, 1, NULL, 4);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'台中北屯太原足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025')
    AND mt.match_on = N'2025-06-13' AND mt.opponent = N'新竹STRIKERS';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e6a76e06-fbf8-54b5-b54d-fef32ffab84c';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'presidents-cup'), NULL, N'2025-06-13', N'16:00', N'HOME', N'新竹STRIKERS', N'cup', N'played', 1, 1, NULL, 5);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'高雄楠梓足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mt.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025')
    AND mt.match_on = N'2025-06-15' AND mt.opponent = N'高雄ATTACKERS';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'642ed19b-9101-5c6f-958f-07bb9aab73ed';
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025'), (SELECT id FROM competitions WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'presidents-cup'), NULL, N'2025-06-15', N'16:00', N'AWAY', N'高雄ATTACKERS', N'cup', N'played', 1, 0, NULL, 6);
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', N'高雄楠梓足球場');
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

-- ── 14. players：藍鯨 2024 年度名單共 28 筆，team_id 一律 BW1 ──
-- 背號 26 重複（史詠甄／瓦拉邦・汶廷），natural key 加姓名判斷，避免互相覆蓋。
DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 1 AND pi.name = N'蔡明容';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3d3ac5b7-874e-5699-8457-ff47b21b93c9';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'tsai-ming-jung', 1);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'蔡明容');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'TSAI,MING-JUNG');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'tsai-ming-jung'
WHERE id = N'3d3ac5b7-874e-5699-8457-ff47b21b93c9' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 2 AND pi.name = N'張季蘭';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ba3afe94-b951-5419-8a11-f511130db235';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'chang-chi-lan', 2);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'張季蘭');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'CHANG,CHI-LAN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'chang-chi-lan'
WHERE id = N'ba3afe94-b951-5419-8a11-f511130db235' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 3 AND pi.name = N'沈彥君';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c81d29c1-720e-5fd1-82f5-68620576a8e0';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'shen-yen-chun', 3);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'沈彥君');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'SHEN,YEN-CHUN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'shen-yen-chun'
WHERE id = N'c81d29c1-720e-5fd1-82f5-68620576a8e0' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 5 AND pi.name = N'黃可欣';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'55fab44b-c7a8-5669-92ea-15ce4114f931';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'huang-ke-sin', 5);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'黃可欣');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'HUANG,KE-SIN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'huang-ke-sin'
WHERE id = N'55fab44b-c7a8-5669-92ea-15ce4114f931' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 6 AND pi.name = N'席拉萬茵樂敏';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f21f7655-b7fc-5c58-956e-c1d755154c73';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'intamee-silawan', 6);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'席拉萬茵樂敏');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'INTAMEE SILAWAN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'intamee-silawan'
WHERE id = N'f21f7655-b7fc-5c58-956e-c1d755154c73' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 7 AND pi.name = N'潘昕妤';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b191bc60-3030-5e30-b7d7-8dec47cc8c00';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'pan-shin-yu', 7);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'潘昕妤');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'PAN,SHIN-YU');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'pan-shin-yu'
WHERE id = N'b191bc60-3030-5e30-b7d7-8dec47cc8c00' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 8 AND pi.name = N'程思瑜';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'42874f77-6593-5e37-b988-dc953e168135';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'cheng-ssu-yu', 8);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'程思瑜');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'CHENG,SSU-YU');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'cheng-ssu-yu'
WHERE id = N'42874f77-6593-5e37-b988-dc953e168135' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 9 AND pi.name = N'粘菁云';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'db3c586a-9977-528b-aa4b-1f0316275844';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'nien-ching-yun', 9);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'粘菁云');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'NIEN,CHING-YUN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'nien-ching-yun'
WHERE id = N'db3c586a-9977-528b-aa4b-1f0316275844' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 12 AND pi.name = N'吳悠';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'05a2a861-db18-5321-8994-9c3980edd437';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'wu-yu', 12);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'吳悠');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'WU,YU');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'wu-yu'
WHERE id = N'05a2a861-db18-5321-8994-9c3980edd437' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 13 AND pi.name = N'薩瓦拉克·彭甘';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'69a79d69-cfce-5c0b-a566-7791726a0608';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'saowalak-peng-ngam', 13);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'薩瓦拉克·彭甘');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'SAOWALAK PENG-NGAM');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'saowalak-peng-ngam'
WHERE id = N'69a79d69-cfce-5c0b-a566-7791726a0608' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 14 AND pi.name = N'田中麻帆';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5fa6f45b-2bef-5c69-bdc4-d50d73ccf0c5';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'tanaka-maho', 14);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'田中麻帆');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'TANAKA MAHO');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'tanaka-maho'
WHERE id = N'5fa6f45b-2bef-5c69-bdc4-d50d73ccf0c5' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 15 AND pi.name = N'林雅萱';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0546e556-bc3f-5c2a-ace5-3a2a9bdbea5a';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'lin-ya-hsuan', 15);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'林雅萱');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'LIN,YA-HSUAN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'lin-ya-hsuan'
WHERE id = N'0546e556-bc3f-5c2a-ace5-3a2a9bdbea5a' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 16 AND pi.name = N'陳妗文';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9a172e3f-bb3d-56fb-afc9-aa0762107387';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'chen-jin-wen', 16);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'陳妗文');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'CHEN,JIN-WEN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'chen-jin-wen'
WHERE id = N'9a172e3f-bb3d-56fb-afc9-aa0762107387' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 17 AND pi.name = N'林靜萱';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'78b2e202-028c-5702-a3e5-1597ff3c3f0e';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'lin-jing-xuan', 17);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'林靜萱');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'LIN,JING-XUAN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'lin-jing-xuan'
WHERE id = N'78b2e202-028c-5702-a3e5-1597ff3c3f0e' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 18 AND pi.name = N'江子善';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a50ec57d-7901-516b-b4a0-e33a5289f5f5';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'chiang-tzu-shan', 18);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'江子善');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'CHIANG,TZU-SHAN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'chiang-tzu-shan'
WHERE id = N'a50ec57d-7901-516b-b4a0-e33a5289f5f5' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 19 AND pi.name = N'皮薩邁．頌賽';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b34ebbcf-0e57-5cba-9571-d1dd8852b4f5';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'sornsai-pitsamai', 19);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'皮薩邁．頌賽');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'SORNSAI PITSAMAI');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'sornsai-pitsamai'
WHERE id = N'b34ebbcf-0e57-5cba-9571-d1dd8852b4f5' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 20 AND pi.name = N'陳姿蓁';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bfb33d31-88e3-5fcd-94f2-7c0194366e26';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'chen-tzu-chen', 20);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'陳姿蓁');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'CHEN,TZU-CHEN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'chen-tzu-chen'
WHERE id = N'bfb33d31-88e3-5fcd-94f2-7c0194366e26' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 21 AND pi.name = N'黃薈珊';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1006e5d1-b001-565c-a5bf-a7e574d0a287';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'huang-hui-shan', 21);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'黃薈珊');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'HUANG,HUI-SHAN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'huang-hui-shan'
WHERE id = N'1006e5d1-b001-565c-a5bf-a7e574d0a287' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 22 AND pi.name = N'李佩容';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0f9ec745-b9ea-5cd8-899c-f83edfa0dae6';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'li-pei-jung', 22);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'李佩容');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'LI,PEI-JUNG');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'li-pei-jung'
WHERE id = N'0f9ec745-b9ea-5cd8-899c-f83edfa0dae6' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 23 AND pi.name = N'劉千芸';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd68002e5-ed4e-5b66-9425-dc073fc49556';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'liu-chien-yun', 23);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'劉千芸');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'LIU,CHIEN-YUN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'liu-chien-yun'
WHERE id = N'd68002e5-ed4e-5b66-9425-dc073fc49556' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 24 AND pi.name = N'林妤璇';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'170881df-cfa6-5620-94a4-7b8579da214e';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'lin-yu-syuan', 24);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'林妤璇');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'LIN,YU-SYUAN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'lin-yu-syuan'
WHERE id = N'170881df-cfa6-5620-94a4-7b8579da214e' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 25 AND pi.name = N'吳芳瑜';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f6b25cd7-65c1-57fc-bfa1-7dd7cea93351';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'wu-fang-yu', 25);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'吳芳瑜');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Wu Fang Yu');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'wu-fang-yu'
WHERE id = N'f6b25cd7-65c1-57fc-bfa1-7dd7cea93351' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 26 AND pi.name = N'史詠甄';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cd821689-de76-532a-8df9-e9724000a92f';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'shih-yung-chen', 26);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'史詠甄');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'SHIH, YUNG-CHEN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'shih-yung-chen'
WHERE id = N'cd821689-de76-532a-8df9-e9724000a92f' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 26 AND pi.name = N'瓦拉邦·汶廷';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'325d44f0-aa51-5300-9e9f-ed23f7b37a88';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'waraporn-boonsing', 26);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'瓦拉邦·汶廷');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Waraporn Boonsing');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'waraporn-boonsing'
WHERE id = N'325d44f0-aa51-5300-9e9f-ed23f7b37a88' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 27 AND pi.name = N'李翊瑄';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'14c8b342-c5d7-5ca4-b0f1-d3be036915a8';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'li-yi-syuan', 27);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'李翊瑄');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'Li,YI-SYUAN');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'li-yi-syuan'
WHERE id = N'14c8b342-c5d7-5ca4-b0f1-d3be036915a8' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 28 AND pi.name = N'林佳盈';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'05470b30-b800-58b0-8bff-a0238ebe6468';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'lin-chia-ying', 28);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'林佳盈');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'LIN CHIA-YING');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'lin-chia-ying'
WHERE id = N'05470b30-b800-58b0-8bff-a0238ebe6468' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 29 AND pi.name = N'廖婕甯';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c7f8321d-12bb-54f0-8644-9ed477ce34c7';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'liao-jie-ning', 29);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'廖婕甯');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'LIAO,JIE-NING');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'liao-jie-ning'
WHERE id = N'c7f8321d-12bb-54f0-8644-9ed477ce34c7' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = (SELECT id FROM teams WHERE code = N'BW1') AND pl.shirt_no = 30 AND pi.name = N'吳亞諭';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e77d14ee-2c30-502d-967d-dee5998da0da';
  INSERT INTO players (id, club_id, team_id, slug, shirt_no)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'wu-ya-yu', 30);
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', N'吳亞諭');
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', N'WU,YA-YU');
  COMMIT TRANSACTION;
END
GO

UPDATE players SET slug = N'wu-ya-yu'
WHERE id = N'e77d14ee-2c30-502d-967d-dee5998da0da' AND slug = N'player-' + CONVERT(nvarchar(20), row_seq);
GO

-- ── 15. staff：藍鯨教練團共 5 人，全數連結 BW1 ──────────────
DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  JOIN staff st ON st.id = si.staff_id
  WHERE st.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND si.locale = N'zh-Hant' AND si.name = N'呂桂花' AND si.title = N'總教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'151deff9-e634-5ee9-8608-605e12df96b2';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'));
  INSERT INTO staff_i18n (staff_id, locale, name, title, bio) VALUES (@id, N'zh-Hant', N'呂桂花', N'總教練', N'2008　U-19亞洲盃資格賽中華女足代表隊-總教練
2011　深圳世界大學運動會-中華女足代表隊-總教練
2013　東亞運-中華女子足球隊-教練
2013　喀山世界大學運動會-中華女足代表隊-總教練
2014　東亞盃-中華女子足球隊-教練
2014　韓國仁川亞運-中華女足代表隊-教練
2015　光州世界大學運動會-中華女足代表隊-教練
2016　里約奧運中華女足代表隊-教練
2014-2016　台中藍鯨女子足球隊-第一隊總教練
2017　台中藍鯨女子足球隊-第一隊-教練
2018-2024　台中藍鯨女子足球隊-第一隊-總教練');
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', N'LU,KUEI-HUA', NULL);
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  JOIN staff st ON st.id = si.staff_id
  WHERE st.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND si.locale = N'zh-Hant' AND si.name = N'李彥廷' AND si.title = N'教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f88a2ab2-08ba-5686-8648-848ba6ecc1ec';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'));
  INSERT INTO staff_i18n (staff_id, locale, name, title, bio) VALUES (@id, N'zh-Hant', N'李彥廷', N'教練', N'2021-2023　台中藍鯨 教練
2022-2023　台中藍鯨U15女子足球隊 教練
2022-2024　中華民國女子足球代表隊 教練');
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', N'LI,YAN-TING', NULL);
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  JOIN staff st ON st.id = si.staff_id
  WHERE st.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND si.locale = N'zh-Hant' AND si.name = N'鄭雅薰' AND si.title = N'教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'88cfa9ab-7042-581d-a103-36e9b11345e2';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'));
  INSERT INTO staff_i18n (staff_id, locale, name, title, bio) VALUES (@id, N'zh-Hant', N'鄭雅薰', N'教練', N'2018　高雄陽信女子足球隊 教練
2016-2022　五權國民中學女子足球隊 總教練
2019-2024　台中藍鯨女子足球隊 第一隊教練
2022　台中藍鯨足球俱樂部U15青年隊-總教練');
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', N'CHENG,YA-HSUN', NULL);
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  JOIN staff st ON st.id = si.staff_id
  WHERE st.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND si.locale = N'zh-Hant' AND si.name = N'張博翔' AND si.title = N'守門教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd2da63d5-a852-5acc-97b4-c86073fdc115';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'));
  INSERT INTO staff_i18n (staff_id, locale, name, title, bio) VALUES (@id, N'zh-Hant', N'張博翔', N'守門教練', N'2019-2024　台中藍鯨女子足球隊守門員教練');
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', N'CHANG,PO-HSIANG', NULL);
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  JOIN staff st ON st.id = si.staff_id
  WHERE st.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND si.locale = N'zh-Hant' AND si.name = N'邱毓芳' AND si.title = N'防護員兼體能教練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'95df2e12-a388-56d9-9dc3-2a7a76d142af';
  INSERT INTO staff (id, club_id) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'));
  INSERT INTO staff_i18n (staff_id, locale, name, title, bio) VALUES (@id, N'zh-Hant', N'邱毓芳', N'防護員兼體能教練', N'2023-2024　台中藍鯨女子足球隊防護員兼體能教練');
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', N'CHIU,YU-FANG', NULL);
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

-- ── 16. milestones：藍鯨沿革 12 筆（2014–2025，逐年） ──────
-- ⚠️ happened_on 只有年份可考，月日固定 01-01 純屬技術性佔位，不代表真實事件發生日期。
DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2014';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c7b2087e-3b7a-5911-a2a6-dea3cb97fabe';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2014-01-01', 0);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2014', N'1. 籌組台中藍鯨女子足球隊參加木蘭聯賽
2. 參加第一屆台灣木蘭足球聯賽
3. FB粉絲人數1200人
4. 台灣體育運動大學小型人工草足球場完成');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2015';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd7a3abb8-7ec0-5b61-a33c-e71a404235f6';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2015-01-01', 1);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2015', N'1. 參加第二屆台灣木蘭足球聯賽
2. 協助台中市五權國中女足隊成立
3. 承接辦理D級教練講習');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2016';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0eef4c79-f07b-5d0d-9ecb-44f0a86cdba2';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2016-01-01', 2);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2016', N'1. 參加第三屆台灣木蘭足球聯賽
2. 成立中部菁英女子訓練站
3. 首次舉辦AFC女子足球節');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2017';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'42fae516-0dc8-5656-9500-2d44dc6af2cb';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2017-01-01', 3);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2017', N'1. 參加第三屆台灣木蘭足球聯賽
2. 舉辦第一屆藍鯨盃足球賽
3. 菁英女子足球訓練站改名中部訓練站
4. 聘請JFA S級教練 堀野博幸擔任一線隊總教練
5. 舉辦地區性教練講習
6. 台中北屯太原足球場啟用
7. 隊史第一座台灣木蘭聯賽冠軍
8. FB粉絲人數達6500人');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2018';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f073a6ef-89da-5eeb-9171-14ca6621500b';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2018-01-01', 4);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2018', N'1. 參加第四屆台灣木蘭足球聯賽
2. 成立台中藍鯨足球學校
3. 中部訓練站更名為藍鯨中部足球訓練站
4. 第一位職業選手包欣玄加入台中藍鯨
5. 協助台中市惠文高中成立女足隊成立
6. 隊史第二座台灣木蘭聯賽冠軍');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2019';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'19a7f6ed-0414-5caf-8dbb-9ab88edbc5b9';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2019-01-01', 5);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2019', N'1. 參加第五屆台灣木蘭足球聯賽
2. 守門員蔡明容輸出旅外日本成功
3. 第一位日本選手田中麻帆加入
4. 爭取台中足球園區興建
5. 成立電競小隊參加PES2020世界盃
6. 隊史第三座台灣木蘭聯賽冠軍
7. 聯賽第一次藍鯨主場售票
8. 通過AFC CLUB License俱樂部認證
9. 總教練呂桂花獲得AFC2019草根領袖獎
10. 首次全年度主場舉辦主題日
11. 成立藍鯨女孩啦啦隊
12. 承接運動i台灣-運動熱區推廣計畫');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2020';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'18242636-474b-52f3-879f-18f77c86193f';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2020-01-01', 6);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2020', N'1. 參加第六屆台灣木蘭足球聯賽
2. 第一位香港籍選手吳卓蔚加入
3. 第一位美國籍選手瑪芮兒加入
4. 守門員程思瑜輸出旅外日本成功
5. 選手蘇育萱輸出旅外日本成功
6. 聯賽藍鯨主場售票
7. 台中藍鯨U15女子足球隊參加第一屆台灣青年聯賽
8. 隊史第一座台灣木蘭聯賽亞軍
9. 承接運動i台灣-運動熱區推廣計畫');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2021';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1be040a8-1754-5d82-8822-31704ae5a2bf';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2021-01-01', 7);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2021', N'1. 參加第八屆台灣木蘭足球聯賽
2. 第二位日本籍選手日高偉織加入
3. 第一位泰國籍選手皮薩邁頌賽加入
4. 第一位泰國籍守門員納塔魯亞牧塔納維奇加入
5. 隊史第四座台灣木蘭聯賽冠軍
6. 隊史第一座台灣木蘭聯賽盃MLC冠軍
7. 台中藍鯨U15女子足球隊參加第二屆台灣青年聯賽
8. 台中藍鯨U18女子足球隊參加第二屆台灣青年聯賽
9. 承接運動i台灣-運動熱區推廣計畫');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2022';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8eefeca7-5349-5b58-83ad-550ef731a8f6';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2022-01-01', 8);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2022', N'1. 參加第九屆台灣木蘭足球聯賽
2. 代表台灣參加AFC女子足球俱樂部錦標賽(泰國)
3. 史上第三位泰國籍選手席拉萬茵樂敏加入
4. 疫情有成舉辦首場頂級足球開門賽
5. 台中藍鯨U15女子足球隊參加第三屆台灣青年聯賽
6. 台中藍鯨U18女子足球隊參加第三屆台灣青年聯賽
7. 台中藍鯨U15獲得第一座台灣青年聯賽U15女子組冠軍
8. 隊史第二座台灣木蘭聯賽亞軍
9. 承接運動i台灣-運動熱區推廣計畫');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2023';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1fe2ea02-4bc5-548a-8dd1-225c941b41cf';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2023-01-01', 9);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2023', N'1. 參加第十屆台灣木蘭足球聯賽
2. 台中足球園區動土並獲邀參加動土典禮
3. 選手蘇育萱輸出旅外中國成功
4. 史上第四位泰國籍選手席菲拉萬茵樂敏加入
5. 史上第五位泰國籍選手薩瓦拉克彭甘加入
6. FB粉絲人數達16500人
7. 隊史第五座台灣木蘭聯賽冠軍
8. 承接運動i台灣-運動熱區推廣計畫');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2024';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'13cdd16b-4e58-5b52-b483-7ee37749f9f7';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2024-01-01', 10);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2024', N'1. 參加第十一屆台灣木蘭足球聯賽
2. 成立台中藍鯨U10女子隊
3. 台中藍鯨U10女子隊首次參加臺中市市長盃
4. 獲邀參加陽信盃國際邀請賽並獲得冠軍
5. 代表台灣參加24/25年亞足聯女子冠軍聯賽並順利小組晉級
6. 隊史第三座台灣木蘭聯賽亞軍
7. 史上第六位泰國籍選手第二位守門員瓦拉邦汶廷加入
8. 隊史第二位外籍選手薩瓦拉克彭甘獲得台灣木蘭足球聯賽年度金靴獎
9. 承接運動i台灣-運動熱區推廣計畫');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND mi.locale = N'zh-Hant' AND mi.title = N'2025';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'88a362ca-dbf7-5eb5-9480-70124c237195';
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'2025-01-01', 11);
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2025', N'1. 參加第十二屆台灣木蘭足球聯賽
2. 代表台灣參加2024年至2025年亞足聯女子冠軍聯賽8強賽獲得亞洲前8名成績
3. 2025台灣總統盃足球錦標賽亞軍
4. 史上第七位泰國籍選手第三位守門員邱瑪尼-通蒙戈加入
5. 史上第二位泰國籍選手冼仲意加入
6. 總教練呂桂花獲得AFC亞足聯亞洲最佳女足隊教練提名
7. 球衣首次放上公益團體機關台中惠明盲校
8. 承接運動i台灣-運動熱區推廣計畫
9. 首次接受英國世界足球雜誌專訪');
  COMMIT TRANSACTION;
END
GO

-- ── 17. partners：藍鯨指導單位＋官方合作夥伴共 26 筆 ──────────
-- 這批是舊站「2024 合作夥伴」，到 2026 年是否仍有效全部要重新確認（gap-analysis.md 09 單元）。
DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-01';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'86360dee-7a15-565f-aed7-62dffc8f64d9';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-01', N'指導單位', N'https://www.sa.gov.tw/', 1);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'教育部體育署');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-02';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'651b5b1f-2497-5066-a680-019f42baf31a';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-02', N'指導單位', N'https://www.taichung.gov.tw/', 2);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'臺中市政府');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-03';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f9c339cf-faa8-5a6d-8988-a3ef2a7790e8';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-03', N'指導單位', N'https://www.sport.taichung.gov.tw/System/main/Home/Index.php', 3);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'臺中市政府運動局');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-04';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6d8f71d3-2dcc-51c9-b9e7-49f9bee9c97b';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-04', N'官方合作夥伴', NULL, 4);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'大力卜股份有限公司');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-05';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bc35a805-5e21-558b-a582-7c7742479366';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-05', N'官方合作夥伴', N'https://www.skechers-twn.com/zh-tw/home', 5);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'SKECHERS／思克威爾股份有限公司');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-06';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ed1b2742-103c-5612-935d-e2c17cc15b90';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-06', N'官方合作夥伴', N'http://www.best-giving.com/', 6);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'麗明營造');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-07';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'57194622-105c-508a-b406-470f4e4bca8a';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-07', N'官方合作夥伴', N'https://www.facebook.com/mietaiwan/', 7);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'MIE Taiwan');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-08';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0648bca8-c710-52ed-9902-83111c524ef0';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-08', N'官方合作夥伴', N'https://www.facebook.com/yesports/', 8);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'原野運動用品');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-09';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e7ec312a-de5f-5103-813f-5940a9cc2b39';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-09', N'官方合作夥伴', N'https://www.i2tec.com/', 9);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'緯思創國際有限公司');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-10';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4e9a9f75-4968-51c1-b37a-de369eb92389';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-10', N'官方合作夥伴', N'http://www.weikang.tw/', 10);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'威康環保科技有限公司');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-11';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'570cd10b-09bc-583c-9314-b60cd17dbd3d';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-11', N'官方合作夥伴', NULL, 11);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'恆春鄉村冬粉鴨');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-12';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'af6b7d19-38e2-541a-a69c-a31cc2c8240e';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-12', N'官方合作夥伴', N'https://www.facebook.com/up888/', 12);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'桃園市私立雲鵬老人長期照顧中心(養護型)');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-13';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2e98c075-d73b-5d54-9443-737032f654f1';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-13', N'官方合作夥伴', N'https://www.fs-ks.com.tw/index.php', 13);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'時尚假期醫美集團');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-14';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5b41eb58-ce9d-5945-b2b0-c9a57bc568ff';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-14', N'官方合作夥伴', NULL, 14);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'程式方塊教育事業股份有限公司');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-15';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6589bc43-c787-517e-a913-f58ba7b32428';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-15', N'官方合作夥伴', NULL, 15);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'麒碩科技有限公司');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-16';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'68eaaea7-1b79-590b-94ad-e04fad6013e1';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-16', N'官方合作夥伴', NULL, 16);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'黃界銘環境工程技師事務所');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-17';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd3607a51-d2f5-5cca-b35c-77cc8b995ed5';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-17', N'官方合作夥伴', N'https://freeair.com.tw/', 17);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'富立業工程顧問股份有限公司');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-18';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b15c4d3a-a5bc-5a2f-88dd-f5d944a45822';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-18', N'官方合作夥伴', N'https://pe.ntus.edu.tw/', 18);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'國立臺灣體育運動大學體育學系');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-19';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1a1f8996-4d0a-51ba-96f9-3982d4d044f9';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-19', N'官方合作夥伴', N'https://www.inkism.com.tw/', 19);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'墨力國際股份有限公司');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-20';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0aa6dcec-de9c-5270-a5de-a7bbeae3ac65';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-20', N'官方合作夥伴', N'https://www.facebook.com/YK0422918736', 20);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'永康中醫 (台中水湳)');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-21';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd704f30b-a3b3-5029-a952-29b6c87e7bf6';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-21', N'官方合作夥伴', N'https://www.fruitwhisper.com/', 21);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'講果語');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-22';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'930e5cd3-d45f-5851-a00f-0f1200ba8786';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-22', N'官方合作夥伴', N'https://www.foodmarket.com.tw/', 22);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'真匯吃-真正匯集台灣最好吃，嚴選在地台灣味');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-23';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1679229a-3626-54dd-9d94-b7a2fe3682ea';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-23', N'官方合作夥伴', N'https://www.facebook.com/DefuncTaiwan/', 23);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'Defunc Taiwan');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-24';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'67f984a3-778b-5fbd-b714-42d59c560827';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-24', N'官方合作夥伴', N'https://www.pocari.com.tw/', 24);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'寶礦力水得 Pocari Sweat');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-25';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9b02b54b-906a-5cb8-a139-955b609e2fb3';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-25', N'官方合作夥伴', N'https://www.facebook.com/paintbear', 25);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'油漆熊工程行');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-partner-26';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4f78e422-f850-5e8b-b0c8-939530ee73dd';
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-partner-26', N'官方合作夥伴', N'https://www.facebook.com/TaiwanNewGenerationCare', 26);
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', N'台灣新世代關懷協會');
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
  COMMIT TRANSACTION;
END
GO

-- ── 24. venues：台中磐石（tcrfc）主場西屯足球場（site-facts.ts 已核實真實值） ─────
DECLARE @id uniqueidentifier;
SELECT @id = vi.venue_id FROM venues_i18n vi WHERE vi.locale = N'zh-Hant' AND vi.name = N'西屯足球場';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'79528e9e-c8a8-5fa3-b645-4c7f80e6a966';
  INSERT INTO venues (id, sort_order) VALUES (@id, 0);
  INSERT INTO venues_i18n (venue_id, locale, name, address, directions)
  VALUES (@id, N'zh-Hant', N'西屯足球場', N'台中市北屯區崇平路二段景谷巷 11 弄 41 號', NULL);
  INSERT INTO venues_i18n (venue_id, locale, name, address, directions)
  VALUES (@id, N'en', N'Xitun Football Field', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

-- ── 24b. settings：site.* 站台事實（GEO-03／GEO-04），兩俱樂部各自一份 ───────────
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.founded_year';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7b4a1df2-8f15-590b-a932-a35bae7fa40c';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.founded_year', N'2024', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.founding_date_display';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'27ade707-c0ea-5461-b904-cb1ef6159df3';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.founding_date_display', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.founding_date_display' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'2024 年創立' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.founding_date_display';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.founding_title';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'695cf05d-7b4d-5c94-be35-959457d7e2c5';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.founding_title', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.founding_title' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'全國乙級聯賽冠軍' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.founding_title';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.league_name';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'93f0a16c-b32b-5a61-b768-77d35e664f29';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.league_name', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.league_name' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'企業甲級聯賽' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.league_name';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.league_name' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Enterprise Premier League' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.league_name';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.squad_structure_summary';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd615ff39-267e-5a7d-825c-146924d0234a';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.squad_structure_summary', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.squad_structure_summary' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.squad_structure_summary';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.squad_structure_summary' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'A development pathway with the first team and the Academy (U15 / U14 / U12) running in parallel' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.squad_structure_summary';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.squad_codes';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd6901be4-64cd-52aa-85fa-7f4f175be7ff';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.squad_codes', N'U15,U14,U12', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.contact_phone';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'797bae35-da8c-5867-b0a6-e153d8b2f429';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.contact_phone', N'04-0000-0000', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.contact_hours';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'edc9dd0d-a4b2-5a43-8426-c1ed10b7742b';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.contact_hours', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.contact_hours' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】平日 09:00–18:00' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.contact_hours';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.founded_year';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'891d56ee-a49a-5203-bdf4-412bdb020fcb';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.founded_year', N'2014', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.founding_date';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8ac174ae-138d-5f39-9df2-f4d35a3d5d14';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.founding_date', N'2014-04-12', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.founding_date_display';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a026601c-803e-594b-8711-f56f0e16acb9';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.founding_date_display', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.founding_date_display' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'2014 年 4 月 12 日成立' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.founding_date_display';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.founding_date_display' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Founded on April 12, 2014' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.founding_date_display';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.league_name';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'147a7701-f853-5b16-9fba-d9b3f669af64';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.league_name', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.league_name' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'台灣木蘭足球聯賽' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.league_name';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.league_short_name';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b5cdaedf-4e7d-5b0c-9963-5b81e8a7af21';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.league_short_name', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.league_short_name' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'木蘭聯賽' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.league_short_name';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.squad_structure_summary';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0e17a623-ad57-5120-9d6e-8f7eac7ac7d6';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.squad_structure_summary', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.squad_structure_summary' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'一線隊與青年隊（U15／U12）兩個梯隊並行的發展體系' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.squad_structure_summary';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.squad_structure_summary' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'A development pathway with the first team and the youth teams (U15 / U12) running in parallel' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.squad_structure_summary';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.squad_codes';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4c6def04-8673-5378-945f-36a03fa0dc59';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.squad_codes', N'U15,U12', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.contact_phone';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0874575d-3fb4-5470-8dfa-bb0bfdfe10fa';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.contact_phone', N'04-0000-0000', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.contact_hours';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fc9762ed-cbb4-5968-a653-1b06331a219c';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.contact_hours', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.contact_hours' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】平日 09:00–18:00' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.contact_hours';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_facebook';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'be3ffc40-aea4-5a28-8e76-be394930393f';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.social_facebook', N'https://www.facebook.com/TCRFC2024', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_instagram';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0b1d4eb6-af56-5030-b668-a12f050cef77';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.social_instagram', N'https://www.instagram.com/tcr_fc_2024', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_youtube';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f0c730e8-1ba2-5056-a7a0-5b6b439ec318';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.social_youtube', N'https://www.youtube.com/@TCRFC-2024', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.footer_blurb';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd50816cc-ebc4-5f9f-8b42-23d365828379';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.footer_blurb', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.footer_blurb' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'台中磐石足球俱樂部致力於透過專業模式，培育選手追求卓越，讓世界看見台灣足球。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.footer_blurb';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.footer_blurb' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Taichung Rock FC develops players who pursue excellence through a professional model, so the world can see Taiwan football.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'site.footer_blurb';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_facebook';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'de48ac01-1dbf-50cb-a096-de5314e38c68';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.social_facebook', N'https://www.facebook.com/tbwfc', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_instagram';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4b373e77-80b5-58ee-bab3-a47c0524e699';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.social_instagram', N'https://instagram.com/tcbw2014', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_youtube';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4b4a45cc-0945-5689-b432-7e40467c596b';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.social_youtube', N'https://www.youtube.com/@user-xu1wm3xx1w', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_line';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c1667937-8534-51b4-93a6-ad9ac9fee289';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.social_line', N'https://lin.ee/CS65qCR', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.contact_email';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fd0c329c-bcee-599f-a262-3b61cddae0fd';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.contact_email', N'fbbh2014@gmail.com', N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.footer_blurb';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'70f3941b-d85e-5188-8c43-3d5f941c6648';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.footer_blurb', N'site');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.footer_blurb' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.footer_blurb';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.footer_blurb' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Taichung Blue Whale Women''s Football Club, part of the Taichung City Women''s Football Association, is one of the teams of the Taiwan Mulan Football League. Taking the blue whale as its symbol, it stands for a faster, stronger and more modern style of football, and hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'site.footer_blurb';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.blue_whale_site_url';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'528c7e08-7b06-5874-aa84-bf9132178175';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.blue_whale_site_url', N'https://bw-stg.tcrfc.tw', N'site');
  COMMIT TRANSACTION;
END
GO

-- ── 24c. settings：site.home_venue_ids（主場場地引用清單，依主場優先順序） ──────
DECLARE @venueIds nvarchar(400);
SELECT @venueIds = CONVERT(nvarchar(36), v.id)
FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id AND vi.locale = N'zh-Hant'
WHERE vi.name = N'西屯足球場';

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.home_venue_ids';
IF @id IS NULL AND @venueIds IS NOT NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'695f9ea1-6fff-5649-8fa7-496d81b5d3f1';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.home_venue_ids', @venueIds, N'site');
  COMMIT TRANSACTION;
END
GO

DECLARE @venueIds nvarchar(400);
-- 太原排在前面＝主要主場（見 content/blue-whale/data/venues.json 的 period_note：太原「現行主場」，
-- 豐原「創隊時期主場」），STRING_AGG 需要明確 ORDER BY 才能保證順序。
SELECT @venueIds = STRING_AGG(CONVERT(nvarchar(36), v.id), ',')
  WITHIN GROUP (ORDER BY CASE WHEN vi.name = N'台中北屯太原足球場' THEN 0 ELSE 1 END)
FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id AND vi.locale = N'zh-Hant'
WHERE vi.name IN (N'台中北屯太原足球場', N'台中市立豐原體育場');

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.home_venue_ids';
IF @id IS NULL AND @venueIds IS NOT NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'74c7835e-10e8-56a1-a728-bdc53b4f1c77';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'site.home_venue_ids', @venueIds, N'site');
  COMMIT TRANSACTION;
END
GO

-- ── 25. pages／page_blocks／page_versions：B1 固定頁（每俱樂部建齊其版型頁，內容為前台備用文案，狀態已發布） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'about/our-story';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2be381af-8f20-590e-aa6a-9f1f7522459c';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'about/our-story', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'我們的故事 Our Story｜關於台中磐石｜台中磐石足球俱樂部', N'台中磐石足球俱樂部（TCRFC）於 2024 年在台中成立。這裡收錄俱樂部從創立至今的沿革故事，完整內文正在整理中。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'3b0aecd4-3d00-5da4-bfbe-ca6289bd9f3b', @id, N'text', N'{"body":{"zh":"台中磐石足球俱樂部（Taichung Rock FC）於 2024 年在台中成立，同年即拿下全國乙級聯賽冠軍。俱樂部主場設於西屯足球場，以「在地扎根．放眼世界」為品牌主張，逐步建立起一線隊、學院與課程並行的發展體系。","en":"Taichung Rock FC was founded in Taichung in 2024 and won the National Second Division champions title in its first year. The club''s home ground is Xitun Football Field. With “LOCAL ROOTS. GLOBAL PATHWAYS.” as its brand promise, it has steadily built a development system in which the First Team, the Academy and our programs run side by side."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'b83e4161-519c-5309-8423-34216bd4876c', @id, 1, N'{"seo":{"zh":{"seoTitle":"我們的故事 Our Story｜關於台中磐石｜台中磐石足球俱樂部","seoDescription":"台中磐石足球俱樂部（TCRFC）於 2024 年在台中成立。這裡收錄俱樂部從創立至今的沿革故事，完整內文正在整理中。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"台中磐石足球俱樂部（Taichung Rock FC）於 2024 年在台中成立，同年即拿下全國乙級聯賽冠軍。俱樂部主場設於西屯足球場，以「在地扎根．放眼世界」為品牌主張，逐步建立起一線隊、學院與課程並行的發展體系。","en":"Taichung Rock FC was founded in Taichung in 2024 and won the National Second Division champions title in its first year. The club''s home ground is Xitun Football Field. With “LOCAL ROOTS. GLOBAL PATHWAYS.” as its brand promise, it has steadily built a development system in which the First Team, the Academy and our programs run side by side."}}}]}', N'5ddb1a97654bd5e668f5107617a50dbcebbe290dde74b35a3100722cef2a4715');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'about/vision-mission';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cbb3f2ea-6ae5-5f41-adfb-ccf2a96ee5b1';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'about/vision-mission', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'願景與使命 Vision & Mission｜關於台中磐石｜台中磐石足球俱樂部', N'台中磐石足球俱樂部的願景與使命：透過專業化培育體系，讓台中在地選手邁向職業舞台，並以足球讓世界看見台灣。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'55fb2d41-a67e-5427-a1df-ef4e54ead483', @id, N'steps', N'{"items":[{"title":{"zh":"願景","en":"Vision"},"description":{"zh":"從台中出發，培育本土選手邁向職業舞台，成為在地榮耀的來源。","en":"Starting from Taichung, we develop local players for the professional stage and become a source of local pride."}},{"title":{"zh":"使命","en":"Mission"},"description":{"zh":"以扎實的訓練體系與國際連結，讓世界看見台灣足球。","en":"Through a solid training system and international connections, we let the world see Taiwan football."}}]}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'2d47faad-76c8-5118-ab59-892a4b27327f', @id, 1, N'{"seo":{"zh":{"seoTitle":"願景與使命 Vision & Mission｜關於台中磐石｜台中磐石足球俱樂部","seoDescription":"台中磐石足球俱樂部的願景與使命：透過專業化培育體系，讓台中在地選手邁向職業舞台，並以足球讓世界看見台灣。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"願景","en":"Vision"},"description":{"zh":"從台中出發，培育本土選手邁向職業舞台，成為在地榮耀的來源。","en":"Starting from Taichung, we develop local players for the professional stage and become a source of local pride."}},{"title":{"zh":"使命","en":"Mission"},"description":{"zh":"以扎實的訓練體系與國際連結，讓世界看見台灣足球。","en":"Through a solid training system and international connections, we let the world see Taiwan football."}}]}}]}', N'7a14bf65bc30087bea379c8183b22f76488803b7068bea043cad3b60169ec99a');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'about/philosophy';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f51f3fe3-399a-5f55-8ac5-0679d72e48cc';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'about/philosophy', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'足球理念 Our Philosophy｜關於台中磐石｜台中磐石足球俱樂部', N'台中磐石足球俱樂部的足球理念與五大核心價值：以球員為本、追求卓越、國際發展、社區共好、誠信專業。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'd2d351b2-be49-5430-a4a9-f5a0a7cee541', @id, N'text', N'{"body":{"zh":"同時作為全站內容標籤，貫穿俱樂部各項訓練規劃與對外溝通。","en":"They also serve as content tags across the site, running through all of the club''s training plans and external communications."}}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'89624843-cf98-507d-9561-99cfab4a2903', @id, N'steps', N'{"items":[{"title":{"zh":"以球員為本","en":"Players First"},"description":{"zh":"所有訓練規劃與資源配置，皆以球員的長期發展與福祉為核心考量。","en":"All training plans and resource allocation put players'' long-term development and wellbeing first."}},{"title":{"zh":"追求卓越","en":"Excellence"},"description":{"zh":"建立專業化訓練與教練體系，協助選手邁向職業舞台所需的實力與態度。","en":"We build a professional training and coaching system to help players develop the ability and mindset needed to reach the professional stage."}},{"title":{"zh":"國際發展","en":"Global Pathways"},"description":{"zh":"從台中出發、放眼世界，透過海外交流建立選手與職業舞台接軌的路徑。","en":"Starting from Taichung and looking to the world, we build pathways to the professional stage through international exchange."}},{"title":{"zh":"社區共好","en":"Community"},"description":{"zh":"紮根台中在地，成為社區認同與榮耀的來源，與球迷共同成長。","en":"Rooted in Taichung, we aim to be a source of local pride and identity, growing together with our fans."}},{"title":{"zh":"誠信專業","en":"Integrity"},"description":{"zh":"以誠信治理與專業制度，支撐俱樂部長期穩健發展。","en":"Honest governance and professional systems support the club''s long-term, steady development."}}]}', 1);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'06b3c71a-0ddd-5f14-8a99-901a7e5d0ada', @id, 1, N'{"seo":{"zh":{"seoTitle":"足球理念 Our Philosophy｜關於台中磐石｜台中磐石足球俱樂部","seoDescription":"台中磐石足球俱樂部的足球理念與五大核心價值：以球員為本、追求卓越、國際發展、社區共好、誠信專業。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"同時作為全站內容標籤，貫穿俱樂部各項訓練規劃與對外溝通。","en":"They also serve as content tags across the site, running through all of the club''s training plans and external communications."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"以球員為本","en":"Players First"},"description":{"zh":"所有訓練規劃與資源配置，皆以球員的長期發展與福祉為核心考量。","en":"All training plans and resource allocation put players'' long-term development and wellbeing first."}},{"title":{"zh":"追求卓越","en":"Excellence"},"description":{"zh":"建立專業化訓練與教練體系，協助選手邁向職業舞台所需的實力與態度。","en":"We build a professional training and coaching system to help players develop the ability and mindset needed to reach the professional stage."}},{"title":{"zh":"國際發展","en":"Global Pathways"},"description":{"zh":"從台中出發、放眼世界，透過海外交流建立選手與職業舞台接軌的路徑。","en":"Starting from Taichung and looking to the world, we build pathways to the professional stage through international exchange."}},{"title":{"zh":"社區共好","en":"Community"},"description":{"zh":"紮根台中在地，成為社區認同與榮耀的來源，與球迷共同成長。","en":"Rooted in Taichung, we aim to be a source of local pride and identity, growing together with our fans."}},{"title":{"zh":"誠信專業","en":"Integrity"},"description":{"zh":"以誠信治理與專業制度，支撐俱樂部長期穩健發展。","en":"Honest governance and professional systems support the club''s long-term, steady development."}}]}}]}', N'152257a532d7ddcc78db424ea3e236d8e52d31e5573616a6780ffac68e325a6f');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'about/governance';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'859968da-b0f6-5fc0-ac75-5ac4856bae67';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'about/governance', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'治理與管理 Governance｜關於台中磐石｜台中磐石足球俱樂部', N'台中磐石足球俱樂部治理相關資訊與公開文件下載區，內容持續更新中。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'a8ebf62e-ac5b-526b-a696-11199effa86c', @id, N'text', N'{"body":{"zh":"目前尚無可供下載的公開文件。","en":"There are no public documents available to download at the moment."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'8369aa37-cdcb-58d1-b25d-2dbf61e62990', @id, 1, N'{"seo":{"zh":{"seoTitle":"治理與管理 Governance｜關於台中磐石｜台中磐石足球俱樂部","seoDescription":"台中磐石足球俱樂部治理相關資訊與公開文件下載區，內容持續更新中。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"目前尚無可供下載的公開文件。","en":"There are no public documents available to download at the moment."}}}]}', N'c62566853ce56934e04476b752e1cc6ba95be5b5d92301e13dd53ba5c4a477cf');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'about/history';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd19f08fc-d12b-5911-9d43-ddf11ebb56b3';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'about/history', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'俱樂部歷程 Club History｜關於台中磐石｜台中磐石足球俱樂部', N'台中磐石足球俱樂部的圖文歷史敘事，記錄俱樂部自 2024 年成立以來的發展歷程。完整內文正在整理中。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'33cb0c78-67c6-5afa-9341-1a09f1f74dec', @id, N'text', N'{"body":{"zh":"台中磐石足球俱樂部（Taichung Rock FC）於 2024 年在台中成立，成立當年即拿下全國乙級聯賽冠軍，並持續擴展一線隊、學院與國際交流網絡。","en":"Taichung Rock FC was founded in Taichung in 2024 and won the National Second Division champions title in its founding year. The club has since continued to expand the First Team, the Academy and its international exchange network."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'af78095b-b8d4-5eec-a26c-daedac4d5a0f', @id, 1, N'{"seo":{"zh":{"seoTitle":"俱樂部歷程 Club History｜關於台中磐石｜台中磐石足球俱樂部","seoDescription":"台中磐石足球俱樂部的圖文歷史敘事，記錄俱樂部自 2024 年成立以來的發展歷程。完整內文正在整理中。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"台中磐石足球俱樂部（Taichung Rock FC）於 2024 年在台中成立，成立當年即拿下全國乙級聯賽冠軍，並持續擴展一線隊、學院與國際交流網絡。","en":"Taichung Rock FC was founded in Taichung in 2024 and won the National Second Division champions title in its founding year. The club has since continued to expand the First Team, the Academy and its international exchange network."}}}]}', N'e49a4e643407bd7efed6b638ba1bbdc56929c2424ad62f820cdb932a9a5ef652');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'club/player-development';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a97d7dc3-5337-5544-9657-352d5c7c6d6e';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'club/player-development', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'球員發展系統 Player Development｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC', N'台中磐石足球俱樂部球員發展系統：技術戰術分析、體能訓練、比賽判讀、心理韌性、影片分析、IDP 個人發展計畫、營養與生活、教育與語言，八大模組完整說明。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'd6410c25-ba4d-5acb-8c15-c3e21c89ad2b', @id, N'steps', N'{"items":[{"title":{"zh":"技術戰術分析","en":"Technical & Tactical Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"體能訓練","en":"Physical Fitness Training"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"比賽判讀","en":"Match Reading"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"心理韌性","en":"Mental Resilience"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"影片分析","en":"Video Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"IDP 個人發展計畫","en":"Individual Development Plan"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"營養與生活","en":"Nutrition & Lifestyle"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"教育與語言","en":"Education & Language"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}}]}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'1661127e-5aaf-54af-9f69-945b1f99b528', @id, N'text', N'{"body":{"zh":"八大模組，一套完整體系\n\n八大模組共同構成一線隊與各梯隊球員的養成框架，銜接學院訓練與國際發展通道，是選手邁向職業舞台的核心支持系統。","en":"Eight modules, one complete system\n\nThe eight modules together form the development framework for First Team and age-group players, connecting Academy training with International Pathways as the core support system for players heading to the professional stage."}}', 1);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'b7cafdaf-d9c6-5865-bb29-e57c03a64332', @id, 1, N'{"seo":{"zh":{"seoTitle":"球員發展系統 Player Development｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC","seoDescription":"台中磐石足球俱樂部球員發展系統：技術戰術分析、體能訓練、比賽判讀、心理韌性、影片分析、IDP 個人發展計畫、營養與生活、教育與語言，八大模組完整說明。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"技術戰術分析","en":"Technical & Tactical Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"體能訓練","en":"Physical Fitness Training"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"比賽判讀","en":"Match Reading"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"心理韌性","en":"Mental Resilience"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"影片分析","en":"Video Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"IDP 個人發展計畫","en":"Individual Development Plan"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"營養與生活","en":"Nutrition & Lifestyle"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"教育與語言","en":"Education & Language"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}}]}},{"blockType":"text","content":{"body":{"zh":"八大模組，一套完整體系\n\n八大模組共同構成一線隊與各梯隊球員的養成框架，銜接學院訓練與國際發展通道，是選手邁向職業舞台的核心支持系統。","en":"Eight modules, one complete system\n\nThe eight modules together form the development framework for First Team and age-group players, connecting Academy training with International Pathways as the core support system for players heading to the professional stage."}}}]}', N'39df34ddcba9b515b3a4d5cd821063557947eeed02e2a778a6ac9a3f30a257a2');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'club/opportunities';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0abf64b1-b0fc-51ec-8d3f-4baff2ed4bda';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'club/opportunities', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'球員機會 Player Opportunities｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC', N'台中磐石足球俱樂部球員機會：加入台中磐石一線隊的資格與報名方式、試訓場次列表與線上報名、外籍球員招募管道（英文優先）。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'3825198a-3269-54cd-8fd0-4d01ba03c605', @id, N'text', N'{"body":{"zh":"台中磐石一線隊代表俱樂部出戰企業甲級聯賽，持續招募具備競技實力的球員加入陣容。填寫報名表後，將由競技部（10.1 表單收件單位）與您聯繫後續評估流程。","en":"The Taichung Rock First Team represents the club in the Enterprise Premier League and continues to recruit players with the competitive ability to join the squad. After you submit the registration form, our football department, which receives these forms, will contact you about the next steps of the assessment."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'122ba52b-21f5-578e-ba51-ee254fd6fe2b', @id, 1, N'{"seo":{"zh":{"seoTitle":"球員機會 Player Opportunities｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC","seoDescription":"台中磐石足球俱樂部球員機會：加入台中磐石一線隊的資格與報名方式、試訓場次列表與線上報名、外籍球員招募管道（英文優先）。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"台中磐石一線隊代表俱樂部出戰企業甲級聯賽，持續招募具備競技實力的球員加入陣容。填寫報名表後，將由競技部（10.1 表單收件單位）與您聯繫後續評估流程。","en":"The Taichung Rock First Team represents the club in the Enterprise Premier League and continues to recruit players with the competitive ability to join the squad. After you submit the registration form, our football department, which receives these forms, will contact you about the next steps of the assessment."}}}]}', N'ce9821a062c1bd76b4d63f9da3dc506f140f49fe2de35c26c64a3331faf77328');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'club/international-pathways';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cd7d5448-962b-53be-b7ec-e42e22650e58';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'club/international-pathways', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'國際發展通道 International Pathways｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC', N'台中磐石足球俱樂部國際發展通道：從在地到海外俱樂部的完整路徑、歐洲／日本／香港分區資訊、合作俱樂部 Logo 牆，以及試訓球探與海外媒合諮詢管道。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'64829788-a247-5973-9e50-d1adf0ed7ba8', @id, N'steps', N'{"items":[{"title":{"zh":"在地","en":"Local Roots"}},{"title":{"zh":"國內職業","en":"Domestic Pro"}},{"title":{"zh":"海外訓練","en":"Overseas Training"}},{"title":{"zh":"海外俱樂部","en":"Clubs Abroad"}}]}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'8cabebae-57f0-5d8d-a045-c5e668298f32', @id, 1, N'{"seo":{"zh":{"seoTitle":"國際發展通道 International Pathways｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC","seoDescription":"台中磐石足球俱樂部國際發展通道：從在地到海外俱樂部的完整路徑、歐洲／日本／香港分區資訊、合作俱樂部 Logo 牆，以及試訓球探與海外媒合諮詢管道。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"在地","en":"Local Roots"}},{"title":{"zh":"國內職業","en":"Domestic Pro"}},{"title":{"zh":"海外訓練","en":"Overseas Training"}},{"title":{"zh":"海外俱樂部","en":"Clubs Abroad"}}]}}]}', N'16864a559f8bfeca7ff914b10b7abc855a55ea410e531b16c8f4eb1dcafb622f');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'club/player-stories';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8fa81517-0f8e-5e37-b16a-b1b07f0c92f6';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'club/player-stories', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'球員故事 Player Stories｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC', N'台中磐石足球俱樂部球員故事案例，可依學院、一線隊、海外、女足篩選，記錄選手從加入台中磐石到邁向更大舞台的真實歷程。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'a37e733c-4dd0-5a52-9a26-1bd30b310b74', @id, N'steps', N'{"items":[{"title":{"zh":"孫恩祈","en":null},"description":{"zh":"一線隊 · 後衛 DF · 背號 6","en":"First Team · Defender (DF) · No. 6"}},{"title":{"zh":"山內大空","en":null},"description":{"zh":"一線隊 · 前鋒 FW · 背號 44","en":"First Team · Forward (FW) · No. 44"}},{"title":{"zh":"楊朝景","en":null},"description":{"zh":"海外 · 中場 MF · 背號 11 · 現效力香港九龍城","en":"Overseas · Midfielder (MF) · No. 11 · currently playing for Kowloon City in Hong Kong"}}]}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'10bac778-eafa-52d5-b600-3de5e55fb5f5', @id, N'text', N'{"body":{"zh":"學院與女足類別目前尚無已建立的球員故事案例。","en":"There are no player stories in the Academy or Women''s categories yet."}}', 1);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'6771eeb5-5c61-5a27-a7cc-68e7410aed94', @id, 1, N'{"seo":{"zh":{"seoTitle":"球員故事 Player Stories｜台中磐石足球俱樂部｜台中磐石足球俱樂部 TCRFC","seoDescription":"台中磐石足球俱樂部球員故事案例，可依學院、一線隊、海外、女足篩選，記錄選手從加入台中磐石到邁向更大舞台的真實歷程。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"孫恩祈","en":null},"description":{"zh":"一線隊 · 後衛 DF · 背號 6","en":"First Team · Defender (DF) · No. 6"}},{"title":{"zh":"山內大空","en":null},"description":{"zh":"一線隊 · 前鋒 FW · 背號 44","en":"First Team · Forward (FW) · No. 44"}},{"title":{"zh":"楊朝景","en":null},"description":{"zh":"海外 · 中場 MF · 背號 11 · 現效力香港九龍城","en":"Overseas · Midfielder (MF) · No. 11 · currently playing for Kowloon City in Hong Kong"}}]}},{"blockType":"text","content":{"body":{"zh":"學院與女足類別目前尚無已建立的球員故事案例。","en":"There are no player stories in the Academy or Women''s categories yet."}}}]}', N'f87813cfca2b521f1faf86a837ba682535152075d59dfe600f5023a7265579a3');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'womens';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd2855326-5e4f-5510-aa4b-e0384d583c2f';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'womens', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'女子足球 Women''s Football｜台中磐石足球俱樂部', N'台中藍鯨女子隊的介紹與官網入口。完整球員名單、教練陣容、賽程與成績請至台中藍鯨官方網站。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'ec34e0ea-f7a2-545e-8e93-585dd03e5546', @id, N'text', N'{"body":{"zh":"隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，簡稱為台中藍鯨，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。台中藍鯨希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。","en":"The Taichung Blue Whale women''s football team belongs to the Taichung City Women''s Football Association, is known for short as Taichung Blue Whale, and is one of the teams in the Taiwan Mulan Football League. The blue whale is its symbol of a faster, stronger and more modern way of playing football, with an emphasis on teamwork, and the whale''s fin is an emblem of Taiwan, leading Taiwan football forward. Taichung Blue Whale hopes to lift the culture of grassroots football in Taichung and drive the development of women''s football in central Taiwan."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'7b524638-a5bb-53af-a135-0710d5575dd9', @id, 1, N'{"seo":{"zh":{"seoTitle":"女子足球 Women''s Football｜台中磐石足球俱樂部","seoDescription":"台中藍鯨女子隊的介紹與官網入口。完整球員名單、教練陣容、賽程與成績請至台中藍鯨官方網站。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，簡稱為台中藍鯨，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。台中藍鯨希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。","en":"The Taichung Blue Whale women''s football team belongs to the Taichung City Women''s Football Association, is known for short as Taichung Blue Whale, and is one of the teams in the Taiwan Mulan Football League. The blue whale is its symbol of a faster, stronger and more modern way of playing football, with an emphasis on teamwork, and the whale''s fin is an emblem of Taiwan, leading Taiwan football forward. Taichung Blue Whale hopes to lift the culture of grassroots football in Taichung and drive the development of women''s football in central Taiwan."}}}]}', N'811b6f303c7a4cd643003816c0dc16e75cac50a8a349f8dbd082a2a2142e099d');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'partners/become-a-partner';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b9480927-db89-5b56-8b6a-ea7b19eb4ba7';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'partners/become-a-partner', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'成為合作夥伴 Become a Partner｜合作夥伴與贊助｜台中磐石足球俱樂部', N'了解與台中磐石足球俱樂部合作的六大價值：品牌曝光、受眾觸及、社會影響力與國際發展網絡。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'789601ac-3c07-55c3-a5b1-7cd3e4a1c204', @id, N'steps', N'{"items":[{"title":{"zh":"為何合作","en":"Why Partner"},"description":{"zh":"台中磐石是台中在地唯一的企業甲級足球俱樂部，是品牌走進運動與社區場域的直接管道。","en":"Taichung Rock FC is the only club in Taichung competing in the top-tier corporate league, giving brands a direct route into sport and the local community."}},{"title":{"zh":"受眾分析","en":"Audience Analysis"},"description":{"zh":"涵蓋一線隊球迷、學院學員家庭與賽事現場觀眾，詳細數據見下方。","en":"Covers First Team fans, Academy student families and matchday crowds. Detailed figures are below."}},{"title":{"zh":"品牌曝光","en":"Brand Exposure"},"description":{"zh":"球衣、場邊看板、官網與社群通路曝光，讓品牌與職業足球賽事同框。","en":"Exposure on the kit, pitchside boards, our website and social media channels puts your brand alongside professional football fixtures."}},{"title":{"zh":"社會影響力","en":"Social Impact"},"description":{"zh":"結合社區足球日與慈善活動，讓品牌參與有意義的在地社會實踐。","en":"Community football days and charity activities let your brand take part in meaningful local social action."}},{"title":{"zh":"中介影響力","en":"Community Reach"},"description":{"zh":"透過學院與課程與家庭、學校建立長期信任關係，擴大品牌口碑觸及。","en":"Through the Academy and our programs, build long-term trust with families and schools and widen your brand''s word-of-mouth reach."}},{"title":{"zh":"國際影響力","en":"International Impact"},"description":{"zh":"藉由國際交流與海外拓展計畫，讓品牌隨台中磐石球員的腳步走向世界。","en":"Through international exchange and overseas expansion programs, let your brand travel the world with Taichung Rock FC''s players."}}]}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'fa0ce7d4-756c-502e-8515-6401a6ed4447', @id, N'text', N'{"body":{"zh":"完整受眾與社群數據報告可於洽談合作時提供，歡迎聯絡商務部索取最新資料。","en":"The full audience and social media data report is available when we discuss a partnership. Please contact our Partnerships Department for the latest figures."}}', 1);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'f06f7f3d-1cbb-5959-9925-454c19159ff9', @id, 1, N'{"seo":{"zh":{"seoTitle":"成為合作夥伴 Become a Partner｜合作夥伴與贊助｜台中磐石足球俱樂部","seoDescription":"了解與台中磐石足球俱樂部合作的六大價值：品牌曝光、受眾觸及、社會影響力與國際發展網絡。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"為何合作","en":"Why Partner"},"description":{"zh":"台中磐石是台中在地唯一的企業甲級足球俱樂部，是品牌走進運動與社區場域的直接管道。","en":"Taichung Rock FC is the only club in Taichung competing in the top-tier corporate league, giving brands a direct route into sport and the local community."}},{"title":{"zh":"受眾分析","en":"Audience Analysis"},"description":{"zh":"涵蓋一線隊球迷、學院學員家庭與賽事現場觀眾，詳細數據見下方。","en":"Covers First Team fans, Academy student families and matchday crowds. Detailed figures are below."}},{"title":{"zh":"品牌曝光","en":"Brand Exposure"},"description":{"zh":"球衣、場邊看板、官網與社群通路曝光，讓品牌與職業足球賽事同框。","en":"Exposure on the kit, pitchside boards, our website and social media channels puts your brand alongside professional football fixtures."}},{"title":{"zh":"社會影響力","en":"Social Impact"},"description":{"zh":"結合社區足球日與慈善活動，讓品牌參與有意義的在地社會實踐。","en":"Community football days and charity activities let your brand take part in meaningful local social action."}},{"title":{"zh":"中介影響力","en":"Community Reach"},"description":{"zh":"透過學院與課程與家庭、學校建立長期信任關係，擴大品牌口碑觸及。","en":"Through the Academy and our programs, build long-term trust with families and schools and widen your brand''s word-of-mouth reach."}},{"title":{"zh":"國際影響力","en":"International Impact"},"description":{"zh":"藉由國際交流與海外拓展計畫，讓品牌隨台中磐石球員的腳步走向世界。","en":"Through international exchange and overseas expansion programs, let your brand travel the world with Taichung Rock FC''s players."}}]}},{"blockType":"text","content":{"body":{"zh":"完整受眾與社群數據報告可於洽談合作時提供，歡迎聯絡商務部索取最新資料。","en":"The full audience and social media data report is available when we discuss a partnership. Please contact our Partnerships Department for the latest figures."}}}]}', N'a8b85f377be19179ed7811280c0c9c88f700ae8deb8f14908accc6b33d60c72e');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'charity/commitment';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9036139f-2074-5362-9a87-d7c52a7d9898';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'charity/commitment', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'慈善理念與投入領域 Our Commitment｜慈善與社會影響｜台中磐石足球俱樂部', N'台中磐石足球俱樂部的慈善理念與四大投入領域：青少年扶助、偏鄉足球、弱勢家庭與公益義賽，實踐 Community 社區共好核心價值。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'7bd26630-2dc8-5476-986f-33a29431da0f', @id, N'text', N'{"body":{"zh":"台中磐石足球俱樂部自 2024 年成立以來，將「Community 社區共好」列為五大核心價值之一，相信足球能為社區帶來的影響不只在球場上。我們相信優質的足球資源不該只集中在少數人身上，因此持續尋找機會，把訓練、場地與人才帶到需要的地方。","en":"Since Taichung Rock FC was founded in 2024, Community has been one of our five core values. We believe the impact football can have on a community goes well beyond the pitch. We also believe that good football resources should not be concentrated in the hands of a few, so we keep looking for opportunities to bring training, facilities and talent to the places that need them."}}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'6a39f2f4-54ad-5ab1-9691-703930b83c05', @id, N'text', N'{"body":{"zh":"以下四大領域為規劃書明列的公益投入方向，各領域詳細計畫請見 11.2 慈善計畫。","en":"The four areas below are our public-interest priorities. For the programs in each area, see 11.2 Charity Programs."}}', 1);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'f9375502-6364-590d-afac-de942b17da3d', @id, N'steps', N'{"items":[{"title":{"zh":"青少年扶助","en":"Youth Support"},"description":{"zh":"支持經濟弱勢或資源不足地區的青少年參與足球運動，降低參與門檻。","en":"Supporting young people from economically disadvantaged or under-resourced areas to take part in football, and lowering the barriers to participation."}},{"title":{"zh":"偏鄉足球","en":"Rural Football"},"description":{"zh":"將教練資源與訓練機會帶到偏遠地區的學校與社區，拉近城鄉足球資源落差。","en":"Bringing coaching resources and training opportunities to schools and communities in remote areas, narrowing the gap in football resources between cities and the countryside."}},{"title":{"zh":"弱勢家庭","en":"Family Support"},"description":{"zh":"與在地社福單位合作，提供弱勢家庭孩子參與足球活動的機會與必要物資。","en":"Working with local social welfare organisations to give children from disadvantaged families the chance to take part in football activities, along with the necessary supplies."}},{"title":{"zh":"公益義賽","en":"Charity Matches"},"description":{"zh":"舉辦或參與公益義賽，將活動收益或關注度轉化為對特定公益團體的實質支持。","en":"Organising or taking part in charity matches, turning event proceeds or attention into real support for specific charities."}}]}', 2);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'2f7f2f0a-431a-5988-9155-b86848f4bc7d', @id, 1, N'{"seo":{"zh":{"seoTitle":"慈善理念與投入領域 Our Commitment｜慈善與社會影響｜台中磐石足球俱樂部","seoDescription":"台中磐石足球俱樂部的慈善理念與四大投入領域：青少年扶助、偏鄉足球、弱勢家庭與公益義賽，實踐 Community 社區共好核心價值。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"台中磐石足球俱樂部自 2024 年成立以來，將「Community 社區共好」列為五大核心價值之一，相信足球能為社區帶來的影響不只在球場上。我們相信優質的足球資源不該只集中在少數人身上，因此持續尋找機會，把訓練、場地與人才帶到需要的地方。","en":"Since Taichung Rock FC was founded in 2024, Community has been one of our five core values. We believe the impact football can have on a community goes well beyond the pitch. We also believe that good football resources should not be concentrated in the hands of a few, so we keep looking for opportunities to bring training, facilities and talent to the places that need them."}}},{"blockType":"text","content":{"body":{"zh":"以下四大領域為規劃書明列的公益投入方向，各領域詳細計畫請見 11.2 慈善計畫。","en":"The four areas below are our public-interest priorities. For the programs in each area, see 11.2 Charity Programs."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"青少年扶助","en":"Youth Support"},"description":{"zh":"支持經濟弱勢或資源不足地區的青少年參與足球運動，降低參與門檻。","en":"Supporting young people from economically disadvantaged or under-resourced areas to take part in football, and lowering the barriers to participation."}},{"title":{"zh":"偏鄉足球","en":"Rural Football"},"description":{"zh":"將教練資源與訓練機會帶到偏遠地區的學校與社區，拉近城鄉足球資源落差。","en":"Bringing coaching resources and training opportunities to schools and communities in remote areas, narrowing the gap in football resources between cities and the countryside."}},{"title":{"zh":"弱勢家庭","en":"Family Support"},"description":{"zh":"與在地社福單位合作，提供弱勢家庭孩子參與足球活動的機會與必要物資。","en":"Working with local social welfare organisations to give children from disadvantaged families the chance to take part in football activities, along with the necessary supplies."}},{"title":{"zh":"公益義賽","en":"Charity Matches"},"description":{"zh":"舉辦或參與公益義賽，將活動收益或關注度轉化為對特定公益團體的實質支持。","en":"Organising or taking part in charity matches, turning event proceeds or attention into real support for specific charities."}}]}}]}', N'e342fcf08724a24b4205016e65ba48fa298470e1bb5bb9c7b0cd0bb72054bc2c');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'about/our-story';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0249604e-b3c3-5842-aa82-9da7bb785584';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'about/our-story', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'我們的故事｜關於台中藍鯨｜台中藍鯨女子足球隊', N'台中藍鯨女子足球隊 2014 年成立於台中，隸屬臺中市女子足球協會。認識這支球隊的定位與成立宗旨。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'10b433d8-089c-59da-92cf-3daeacbb481a', @id, N'text', N'{"body":{"zh":"隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，簡稱為台中藍鯨，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。台中藍鯨希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。","en":"Taichung Blue Whale Women''s Football Club, part of the Taichung City Women''s Football Association and known for short as Taichung Blue Whale, is one of the teams of the Taiwan Mulan Football League. Taking the blue whale as its symbol, it stands for a faster, stronger and more modern style of football with an emphasis on teamwork; the whale''s fin is an emblem of Taiwan, leading Taiwan football forward. Taichung Blue Whale hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'4d7d19dc-25bf-5810-8620-0077ea90f808', @id, 1, N'{"seo":{"zh":{"seoTitle":"我們的故事｜關於台中藍鯨｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨女子足球隊 2014 年成立於台中，隸屬臺中市女子足球協會。認識這支球隊的定位與成立宗旨。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，簡稱為台中藍鯨，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。台中藍鯨希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。","en":"Taichung Blue Whale Women''s Football Club, part of the Taichung City Women''s Football Association and known for short as Taichung Blue Whale, is one of the teams of the Taiwan Mulan Football League. Taking the blue whale as its symbol, it stands for a faster, stronger and more modern style of football with an emphasis on teamwork; the whale''s fin is an emblem of Taiwan, leading Taiwan football forward. Taichung Blue Whale hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan."}}}]}', N'4b6c0faea53c05fd12abecdf98c211dc575fc676f5580eff556346db6ce6fd84');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'about/vision-mission';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'57b88e11-1027-58dc-998d-705ad5dde036';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'about/vision-mission', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'發展願景｜關於台中藍鯨｜台中藍鯨女子足球隊', N'台中藍鯨女子足球隊的發展願景：無止盡的探索、不怕難的堅韌、更細膩的態度、最真實的影響、更深遠之目的。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'2d381217-89b7-5b04-a646-03ab5dcff4cc', @id, N'steps', N'{"items":[{"title":{"zh":"無止盡的探索","en":"Endless Exploration"},"description":{"zh":"提昇及普及大台中足球水準，吸收更專業精進足球技術，追上亞洲足球技術水平迎接世界潮流。","en":"Raise and popularise the standard of football in greater Taichung, absorb more professional and refined football skills, catch up with the technical level of Asian football and meet the global trend."}},{"title":{"zh":"不怕難的堅韌","en":"Resilience That Fears No Difficulty"},"description":{"zh":"創造足球運動文化與風氣，以球迷為本讓足球比賽呈現更有水準，場內技術提升，場外足球比賽氛圍更加提升。","en":"Create a football culture and atmosphere with fans at the centre, so that matches reach a higher standard: better technique on the pitch and a livelier matchday atmosphere off it."}},{"title":{"zh":"更細膩的態度","en":"A More Refined Attitude"},"description":{"zh":"增加足球選手發展管道，讓選手有更好的發展空間，家長支持，學校支持，政府支持，產業支持，民眾支持。","en":"Widen the pathways for football players so they have more room to develop, with the support of parents, schools, government, industry and the public."}},{"title":{"zh":"最真實的影響","en":"The Most Genuine Impact"},"description":{"zh":"建立台中為台灣足球之都的美名與榮耀，健康正向的足球風氣，連結喜愛足球運動的球迷及選手所追求的足球夢想。","en":"Build Taichung''s reputation and pride as the capital of Taiwan football, with a healthy and positive football culture that connects fans who love the sport with the football dreams that players pursue."}},{"title":{"zh":"更深遠之目的","en":"A Deeper Purpose"},"description":{"zh":"持續回饋社會，致力為人們創造更美好的生活，深信我們能帶來改變，幫助人們以全新的方式彼此分享與連結，讓世界更加和諧。","en":"Keep giving back to society and strive to create a better life for people. We believe we can bring change, helping people share and connect with one another in new ways and making the world more harmonious."}}]}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'07e8646f-2df2-59ef-861c-5d05e722c3c0', @id, 1, N'{"seo":{"zh":{"seoTitle":"發展願景｜關於台中藍鯨｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨女子足球隊的發展願景：無止盡的探索、不怕難的堅韌、更細膩的態度、最真實的影響、更深遠之目的。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"無止盡的探索","en":"Endless Exploration"},"description":{"zh":"提昇及普及大台中足球水準，吸收更專業精進足球技術，追上亞洲足球技術水平迎接世界潮流。","en":"Raise and popularise the standard of football in greater Taichung, absorb more professional and refined football skills, catch up with the technical level of Asian football and meet the global trend."}},{"title":{"zh":"不怕難的堅韌","en":"Resilience That Fears No Difficulty"},"description":{"zh":"創造足球運動文化與風氣，以球迷為本讓足球比賽呈現更有水準，場內技術提升，場外足球比賽氛圍更加提升。","en":"Create a football culture and atmosphere with fans at the centre, so that matches reach a higher standard: better technique on the pitch and a livelier matchday atmosphere off it."}},{"title":{"zh":"更細膩的態度","en":"A More Refined Attitude"},"description":{"zh":"增加足球選手發展管道，讓選手有更好的發展空間，家長支持，學校支持，政府支持，產業支持，民眾支持。","en":"Widen the pathways for football players so they have more room to develop, with the support of parents, schools, government, industry and the public."}},{"title":{"zh":"最真實的影響","en":"The Most Genuine Impact"},"description":{"zh":"建立台中為台灣足球之都的美名與榮耀，健康正向的足球風氣，連結喜愛足球運動的球迷及選手所追求的足球夢想。","en":"Build Taichung''s reputation and pride as the capital of Taiwan football, with a healthy and positive football culture that connects fans who love the sport with the football dreams that players pursue."}},{"title":{"zh":"更深遠之目的","en":"A Deeper Purpose"},"description":{"zh":"持續回饋社會，致力為人們創造更美好的生活，深信我們能帶來改變，幫助人們以全新的方式彼此分享與連結，讓世界更加和諧。","en":"Keep giving back to society and strive to create a better life for people. We believe we can bring change, helping people share and connect with one another in new ways and making the world more harmonious."}}]}}]}', N'dcf2129d22a599bcdafca9d3807eff44055f6228871acb575ab9cdd5b7fb414a');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'about/philosophy';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a0b242e4-5f52-56c1-9787-05cef7837386';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'about/philosophy', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'俱樂部口號與培訓精神｜關於台中藍鯨｜台中藍鯨女子足球隊', N'台中藍鯨女子足球隊的俱樂部口號與培訓精神，以及隊徽「藍鯨」象徵的設計理念。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'6e1bb4e2-de93-5487-8623-5262b42527cc', @id, N'quote', N'{"text":{"zh":"以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態、重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。","en":"The blue whale is our symbol, representing a faster, stronger and more modern style of football with an emphasis on teamwork; the whale''s fin is an emblem of Taiwan, leading Taiwan football forward."},"attribution":{"zh":"隊徽理念","en":"Crest Concept"}}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'81fb083f-e197-5177-b3bc-571bea9df9a5', @id, N'quote', N'{"text":{"zh":"藍色的天空是我們心中夢想的方向，閃爍的陽光是走向夢想的力量，草地上揮灑汗水是成長過往 堅定信仰，有你在身旁 就不再徬徨，此時此刻，我們與我們的球迷站在一起。一起迎向世界。","en":"The blue sky is the direction of the dreams in our hearts, the glittering sunlight is the strength that carries us towards them, and the sweat shed on the grass is our growth and our steadfast belief. With you by our side we are no longer lost. Right now, we stand together with our fans, and face the world together."},"attribution":{"zh":"俱樂部口號","en":"Club Slogan"}}', 1);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'06f0fe86-4821-52f8-9640-936486d302d9', @id, N'quote', N'{"text":{"zh":"別害怕 勇敢去闖，邁開步伐乘風破浪，就算遍體鱗傷 也要逆風飛翔，抬起頭 夢在前方，越過那重重的高牆 沒有誰能阻擋，眼神越是發光，世界都是我的舞台。","en":"Do not be afraid; be brave and go for it. Stride forward and ride the waves; even if you are covered in scars, fly into the wind. Lift your head, the dream is ahead. Cross those high walls, and no one can stop us. The brighter our eyes shine, the more the whole world is our stage."},"attribution":{"zh":"培訓精神","en":"Training Spirit"}}', 2);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'05a0fdae-5603-526e-9b18-f9bae260ff79', @id, 1, N'{"seo":{"zh":{"seoTitle":"俱樂部口號與培訓精神｜關於台中藍鯨｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨女子足球隊的俱樂部口號與培訓精神，以及隊徽「藍鯨」象徵的設計理念。"}},"blocks":[{"blockType":"quote","content":{"text":{"zh":"以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態、重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。","en":"The blue whale is our symbol, representing a faster, stronger and more modern style of football with an emphasis on teamwork; the whale''s fin is an emblem of Taiwan, leading Taiwan football forward."},"attribution":{"zh":"隊徽理念","en":"Crest Concept"}}},{"blockType":"quote","content":{"text":{"zh":"藍色的天空是我們心中夢想的方向，閃爍的陽光是走向夢想的力量，草地上揮灑汗水是成長過往 堅定信仰，有你在身旁 就不再徬徨，此時此刻，我們與我們的球迷站在一起。一起迎向世界。","en":"The blue sky is the direction of the dreams in our hearts, the glittering sunlight is the strength that carries us towards them, and the sweat shed on the grass is our growth and our steadfast belief. With you by our side we are no longer lost. Right now, we stand together with our fans, and face the world together."},"attribution":{"zh":"俱樂部口號","en":"Club Slogan"}}},{"blockType":"quote","content":{"text":{"zh":"別害怕 勇敢去闖，邁開步伐乘風破浪，就算遍體鱗傷 也要逆風飛翔，抬起頭 夢在前方，越過那重重的高牆 沒有誰能阻擋，眼神越是發光，世界都是我的舞台。","en":"Do not be afraid; be brave and go for it. Stride forward and ride the waves; even if you are covered in scars, fly into the wind. Lift your head, the dream is ahead. Cross those high walls, and no one can stop us. The brighter our eyes shine, the more the whole world is our stage."},"attribution":{"zh":"培訓精神","en":"Training Spirit"}}}]}', N'5bb052a90203ce6393f08724679190386f23ed67d751f110471508133a30aa46');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'about/governance';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e458432b-a2ad-56d8-b1e2-ac0748798976';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'about/governance', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'治理與管理｜關於台中藍鯨｜台中藍鯨女子足球隊', N'台中藍鯨女子足球隊治理相關資訊與公開文件下載區，內容持續更新中。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'f04b0268-5a44-5a44-afc3-1d5afe60b8ba', @id, N'text', N'{"body":{"zh":"目前尚無可供下載的公開文件。","en":"There are no public documents available to download at the moment."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'4b8095e7-5ea7-57cc-9d71-c630d2c76c35', @id, 1, N'{"seo":{"zh":{"seoTitle":"治理與管理｜關於台中藍鯨｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨女子足球隊治理相關資訊與公開文件下載區，內容持續更新中。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"目前尚無可供下載的公開文件。","en":"There are no public documents available to download at the moment."}}}]}', N'870695482b746abc74078cd60cc24a0563770bb82d11078a9560a0f448dd9843');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'about/history';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fcd681f1-8a61-5447-8a68-b72c30bb6d4e';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'about/history', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'俱樂部歷程｜關於台中藍鯨｜台中藍鯨女子足球隊', N'台中藍鯨女子足球隊 2014～2025 年逐年沿革，整理自舊官網公開內容。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'a761fa04-5f0e-594b-a202-5e62fa20cb6c', @id, N'timeline', N'{"items":[{"date":"2014","title":{"zh":"2014 年","en":"2014"},"description":{"zh":"籌組台中藍鯨女子足球隊參加木蘭聯賽\n參加第一屆台灣木蘭足球聯賽\nFB 粉絲人數 1200 人\n台灣體育運動大學小型人工草足球場完成","en":"Formed the Taichung Blue Whale women''s football team to compete in the Mulan League\nTook part in the 1st Taiwan Mulan Football League\nFacebook followers reached 1,200\nSmall-sided artificial-turf football pitch completed at the National Taiwan Sport University"}},{"date":"2015","title":{"zh":"2015 年","en":"2015"},"description":{"zh":"參加第二屆台灣木蘭足球聯賽\n協助台中市五權國中女足隊成立\n承接辦理 D 級教練講習","en":"Took part in the 2nd Taiwan Mulan Football League\nHelped establish the girls'' football team at 五權國中, a junior high school in Taichung\nTook on the organisation of a D-level coaching course"}},{"date":"2016","title":{"zh":"2016 年","en":"2016"},"description":{"zh":"參加第三屆台灣木蘭足球聯賽\n成立中部菁英女子訓練站\n首次舉辦 AFC 女子足球節","en":"Took part in the 3rd Taiwan Mulan Football League\nEstablished the Central Taiwan Elite Women''s Training Station\nHeld the AFC Women''s Football Day for the first time"}},{"date":"2017","title":{"zh":"2017 年","en":"2017"},"description":{"zh":"參加第三屆台灣木蘭足球聯賽\n舉辦第一屆藍鯨盃足球賽\n菁英女子足球訓練站改名中部訓練站\n聘請 JFA S 級教練 堀野博幸擔任一線隊總教練\n舉辦地區性教練講習\n台中北屯太原足球場啟用\n隊史第一座台灣木蘭聯賽冠軍\nFB 粉絲人數達 6500 人","en":"Took part in the 3rd Taiwan Mulan Football League\nHeld the 1st Blue Whale Cup football tournament\nRenamed the Elite Women''s Football Training Station the Central Training Station\nAppointed JFA S-level coach 堀野博幸 as First Team head coach\nHeld a regional coaching course\nTaichung Beitun Taiyuan Football Field opened\nFirst Taiwan Mulan League title in club history\nFacebook followers reached 6,500"}},{"date":"2018","title":{"zh":"2018 年","en":"2018"},"description":{"zh":"參加第四屆台灣木蘭足球聯賽\n成立台中藍鯨足球學校\n中部訓練站更名為藍鯨中部足球訓練站\n第一位職業選手包欣玄加入台中藍鯨\n協助台中市惠文高中成立女足隊成立\n隊史第二座台灣木蘭聯賽冠軍","en":"Took part in the 4th Taiwan Mulan Football League\nFounded the Taichung Blue Whale Football School\nRenamed the Central Training Station the Blue Whale Central Football Training Station\nFirst professional player 包欣玄 joined Taichung Blue Whale\nHelped establish the girls'' football team at 惠文高中, a senior high school in Taichung\nSecond Taiwan Mulan League title in club history"}},{"date":"2019","title":{"zh":"2019 年","en":"2019"},"description":{"zh":"參加第五屆台灣木蘭足球聯賽\n守門員蔡明容輸出旅外日本成功\n第一位日本選手田中麻帆加入\n爭取台中足球園區興建\n成立電競小隊參加 PES2020 世界盃\n隊史第三座台灣木蘭聯賽冠軍\n聯賽第一次藍鯨主場售票\n通過 AFC CLUB License 俱樂部認證\n總教練呂桂花獲得 AFC2019 草根領袖獎\n首次全年度主場舉辦主題日\n成立藍鯨女孩啦啦隊\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 5th Taiwan Mulan Football League\nGoalkeeper 蔡明容 successfully moved abroad to Japan\nFirst Japanese player 田中麻帆 joined\nCampaigned for the construction of a Taichung football park\nFormed an esports squad to compete in the PES2020 World Cup\nThird Taiwan Mulan League title in club history\nFirst time the club sold tickets for a league home match\nPassed the AFC Club Licence accreditation\nHead coach 呂桂花 won the AFC 2019 Grassroots Leader Award\nFirst full season of themed days at home matches\nFounded the Blue Whale Girls cheerleading squad\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2020","title":{"zh":"2020 年","en":"2020"},"description":{"zh":"參加第六屆台灣木蘭足球聯賽\n第一位香港籍選手吳卓蔚加入\n第一位美國籍選手瑪芮兒加入\n守門員程思瑜輸出旅外日本成功\n選手蘇育萱輸出旅外日本成功\n聯賽藍鯨主場售票\n台中藍鯨 U15 女子足球隊參加第一屆台灣青年聯賽\n隊史第一座台灣木蘭聯賽亞軍\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 6th Taiwan Mulan Football League\nFirst Hong Kong player 吳卓蔚 joined\nFirst American player 瑪芮兒 joined\nGoalkeeper 程思瑜 successfully moved abroad to Japan\nPlayer 蘇育萱 successfully moved abroad to Japan\nSold tickets for league home matches\nThe Taichung Blue Whale U15 girls'' team took part in the 1st Taiwan Youth League\nFirst Taiwan Mulan League runners-up finish in club history\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2021","title":{"zh":"2021 年","en":"2021"},"description":{"zh":"參加第八屆台灣木蘭足球聯賽\n第二位日本籍選手日高偉織加入\n第一位泰國籍選手皮薩邁頌賽加入\n第一位泰國籍守門員納塔魯亞牧塔納維奇加入\n隊史第四座台灣木蘭聯賽冠軍\n隊史第一座台灣木蘭聯賽盃 MLC 冠軍\n台中藍鯨 U15 女子足球隊參加第二屆台灣青年聯賽\n台中藍鯨 U18 女子足球隊參加第二屆台灣青年聯賽\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 8th Taiwan Mulan Football League\nSecond Japanese player 日高偉織 joined\nFirst Thai player 皮薩邁頌賽 joined\nFirst Thai goalkeeper 納塔魯亞牧塔納維奇 joined\nFourth Taiwan Mulan League title in club history\nFirst Taiwan Mulan League Cup (MLC) title in club history\nThe Taichung Blue Whale U15 girls'' team took part in the 2nd Taiwan Youth League\nThe Taichung Blue Whale U18 girls'' team took part in the 2nd Taiwan Youth League\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2022","title":{"zh":"2022 年","en":"2022"},"description":{"zh":"參加第九屆台灣木蘭足球聯賽\n代表台灣參加 AFC 女子足球俱樂部錦標賽(泰國)\n史上第三位泰國籍選手席拉萬茵樂敏加入\n疫情有成舉辦首場頂級足球開門賽\n台中藍鯨 U15 女子足球隊參加第三屆台灣青年聯賽\n台中藍鯨 U18 女子足球隊參加第三屆台灣青年聯賽\n台中藍鯨 U15 獲得第一座台灣青年聯賽 U15 女子組冠軍\n隊史第二座台灣木蘭聯賽亞軍\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 9th Taiwan Mulan Football League\nRepresented Taiwan at the AFC Women''s Club Championship (Thailand)\nThird Thai player in club history, 席拉萬茵樂敏, joined\nAfter successfully managing the pandemic, held the first top-level football opening match\nThe Taichung Blue Whale U15 girls'' team took part in the 3rd Taiwan Youth League\nThe Taichung Blue Whale U18 girls'' team took part in the 3rd Taiwan Youth League\nThe Taichung Blue Whale U15 team won its first Taiwan Youth League U15 girls'' title\nSecond Taiwan Mulan League runners-up finish in club history\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2023","title":{"zh":"2023 年","en":"2023"},"description":{"zh":"參加第十屆台灣木蘭足球聯賽\n台中足球園區動土並獲邀參加動土典禮\n選手蘇育萱輸出旅外中國成功\n史上第四位泰國籍選手席菲拉萬茵樂敏加入\n史上第五位泰國籍選手薩瓦拉克彭甘加入\nFB 粉絲人數達 16500 人\n隊史第五座台灣木蘭聯賽冠軍\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 10th Taiwan Mulan Football League\nBroke ground on the Taichung football park and was invited to the groundbreaking ceremony\nPlayer 蘇育萱 successfully moved abroad to China\nFourth Thai player in club history, 席菲拉萬茵樂敏, joined\nFifth Thai player in club history, 薩瓦拉克彭甘, joined\nFacebook followers reached 16,500\nFifth Taiwan Mulan League title in club history\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2024","title":{"zh":"2024 年","en":"2024"},"description":{"zh":"參加第十一屆台灣木蘭足球聯賽\n成立台中藍鯨 U10 女子隊\n台中藍鯨 U10 女子隊首次參加臺中市市長盃\n獲邀參加陽信盃國際邀請賽並獲得冠軍\n代表台灣參加 24/25 年亞足聯女子冠軍聯賽並順利小組晉級\n隊史第三座台灣木蘭聯賽亞軍\n史上第六位泰國籍選手第二位守門員瓦拉邦汶廷加入\n隊史第二位外籍選手薩瓦拉克彭甘獲得台灣木蘭足球聯賽年度金靴獎\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 11th Taiwan Mulan Football League\nFounded the Taichung Blue Whale U10 girls'' team\nThe Taichung Blue Whale U10 girls'' team took part in the Taichung Mayor''s Cup for the first time\nInvited to the 陽信盃 international invitational tournament and won the title\nRepresented Taiwan in the 2024/25 AFC Women''s Champions League and advanced from the group stage\nThird Taiwan Mulan League runners-up finish in club history\nSixth Thai player in club history and second Thai goalkeeper, 瓦拉邦汶廷, joined\nThe club''s second foreign player, 薩瓦拉克彭甘, won the Taiwan Mulan Football League Golden Boot of the year\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2025","title":{"zh":"2025 年","en":"2025"},"description":{"zh":"參加第十二屆台灣木蘭足球聯賽\n代表台灣參加 2024 年至 2025 年亞足聯女子冠軍聯賽 8 強賽獲得亞洲前 8 名成績\n2025 台灣總統盃足球錦標賽亞軍\n史上第七位泰國籍選手第三位守門員邱瑪尼-通蒙戈加入\n史上第二位泰國籍選手冼仲意加入\n總教練呂桂花獲得 AFC 亞足聯亞洲最佳女足隊教練提名\n球衣首次放上公益團體機關台中惠明盲校\n承接運動 i 台灣-運動熱區推廣計畫\n首次接受英國世界足球雜誌專訪","en":"Took part in the 12th Taiwan Mulan Football League\nRepresented Taiwan in the 2024-25 AFC Women''s Champions League quarter-finals and finished among the top 8 in Asia\nRunners-up at the 2025 Taiwan President''s Cup football tournament\nSeventh Thai player in club history and third Thai goalkeeper, 邱瑪尼-通蒙戈, joined\nSecond Thai player in club history, 冼仲意, joined\nHead coach 呂桂花 was nominated for the AFC Asian Best Women''s Team Coach award\nFor the first time, the team shirt carried a public-interest organisation: 台中惠明盲校, a school for the blind in Taichung\nTook on the Sports iTaiwan sports hotspot promotion programme\nFirst interview with the British magazine World Soccer"}}]}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'7f185765-6eaa-5ca2-8707-fdd8996b94b6', @id, N'text', N'{"body":{"zh":"上列沿革整理自舊官網公開內容，部分年度屆數標示與其他資料有出入，正式版本將於俱樂部確認後更新。","en":"The history above is compiled from content published on the former official website. The edition numbers of some seasons differ from other sources and will be updated once the club has confirmed them."}}', 1);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'bb12a774-d8a8-5ded-a2e5-566a1caa9a5c', @id, 1, N'{"seo":{"zh":{"seoTitle":"俱樂部歷程｜關於台中藍鯨｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨女子足球隊 2014～2025 年逐年沿革，整理自舊官網公開內容。"}},"blocks":[{"blockType":"timeline","content":{"items":[{"date":"2014","title":{"zh":"2014 年","en":"2014"},"description":{"zh":"籌組台中藍鯨女子足球隊參加木蘭聯賽\n參加第一屆台灣木蘭足球聯賽\nFB 粉絲人數 1200 人\n台灣體育運動大學小型人工草足球場完成","en":"Formed the Taichung Blue Whale women''s football team to compete in the Mulan League\nTook part in the 1st Taiwan Mulan Football League\nFacebook followers reached 1,200\nSmall-sided artificial-turf football pitch completed at the National Taiwan Sport University"}},{"date":"2015","title":{"zh":"2015 年","en":"2015"},"description":{"zh":"參加第二屆台灣木蘭足球聯賽\n協助台中市五權國中女足隊成立\n承接辦理 D 級教練講習","en":"Took part in the 2nd Taiwan Mulan Football League\nHelped establish the girls'' football team at 五權國中, a junior high school in Taichung\nTook on the organisation of a D-level coaching course"}},{"date":"2016","title":{"zh":"2016 年","en":"2016"},"description":{"zh":"參加第三屆台灣木蘭足球聯賽\n成立中部菁英女子訓練站\n首次舉辦 AFC 女子足球節","en":"Took part in the 3rd Taiwan Mulan Football League\nEstablished the Central Taiwan Elite Women''s Training Station\nHeld the AFC Women''s Football Day for the first time"}},{"date":"2017","title":{"zh":"2017 年","en":"2017"},"description":{"zh":"參加第三屆台灣木蘭足球聯賽\n舉辦第一屆藍鯨盃足球賽\n菁英女子足球訓練站改名中部訓練站\n聘請 JFA S 級教練 堀野博幸擔任一線隊總教練\n舉辦地區性教練講習\n台中北屯太原足球場啟用\n隊史第一座台灣木蘭聯賽冠軍\nFB 粉絲人數達 6500 人","en":"Took part in the 3rd Taiwan Mulan Football League\nHeld the 1st Blue Whale Cup football tournament\nRenamed the Elite Women''s Football Training Station the Central Training Station\nAppointed JFA S-level coach 堀野博幸 as First Team head coach\nHeld a regional coaching course\nTaichung Beitun Taiyuan Football Field opened\nFirst Taiwan Mulan League title in club history\nFacebook followers reached 6,500"}},{"date":"2018","title":{"zh":"2018 年","en":"2018"},"description":{"zh":"參加第四屆台灣木蘭足球聯賽\n成立台中藍鯨足球學校\n中部訓練站更名為藍鯨中部足球訓練站\n第一位職業選手包欣玄加入台中藍鯨\n協助台中市惠文高中成立女足隊成立\n隊史第二座台灣木蘭聯賽冠軍","en":"Took part in the 4th Taiwan Mulan Football League\nFounded the Taichung Blue Whale Football School\nRenamed the Central Training Station the Blue Whale Central Football Training Station\nFirst professional player 包欣玄 joined Taichung Blue Whale\nHelped establish the girls'' football team at 惠文高中, a senior high school in Taichung\nSecond Taiwan Mulan League title in club history"}},{"date":"2019","title":{"zh":"2019 年","en":"2019"},"description":{"zh":"參加第五屆台灣木蘭足球聯賽\n守門員蔡明容輸出旅外日本成功\n第一位日本選手田中麻帆加入\n爭取台中足球園區興建\n成立電競小隊參加 PES2020 世界盃\n隊史第三座台灣木蘭聯賽冠軍\n聯賽第一次藍鯨主場售票\n通過 AFC CLUB License 俱樂部認證\n總教練呂桂花獲得 AFC2019 草根領袖獎\n首次全年度主場舉辦主題日\n成立藍鯨女孩啦啦隊\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 5th Taiwan Mulan Football League\nGoalkeeper 蔡明容 successfully moved abroad to Japan\nFirst Japanese player 田中麻帆 joined\nCampaigned for the construction of a Taichung football park\nFormed an esports squad to compete in the PES2020 World Cup\nThird Taiwan Mulan League title in club history\nFirst time the club sold tickets for a league home match\nPassed the AFC Club Licence accreditation\nHead coach 呂桂花 won the AFC 2019 Grassroots Leader Award\nFirst full season of themed days at home matches\nFounded the Blue Whale Girls cheerleading squad\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2020","title":{"zh":"2020 年","en":"2020"},"description":{"zh":"參加第六屆台灣木蘭足球聯賽\n第一位香港籍選手吳卓蔚加入\n第一位美國籍選手瑪芮兒加入\n守門員程思瑜輸出旅外日本成功\n選手蘇育萱輸出旅外日本成功\n聯賽藍鯨主場售票\n台中藍鯨 U15 女子足球隊參加第一屆台灣青年聯賽\n隊史第一座台灣木蘭聯賽亞軍\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 6th Taiwan Mulan Football League\nFirst Hong Kong player 吳卓蔚 joined\nFirst American player 瑪芮兒 joined\nGoalkeeper 程思瑜 successfully moved abroad to Japan\nPlayer 蘇育萱 successfully moved abroad to Japan\nSold tickets for league home matches\nThe Taichung Blue Whale U15 girls'' team took part in the 1st Taiwan Youth League\nFirst Taiwan Mulan League runners-up finish in club history\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2021","title":{"zh":"2021 年","en":"2021"},"description":{"zh":"參加第八屆台灣木蘭足球聯賽\n第二位日本籍選手日高偉織加入\n第一位泰國籍選手皮薩邁頌賽加入\n第一位泰國籍守門員納塔魯亞牧塔納維奇加入\n隊史第四座台灣木蘭聯賽冠軍\n隊史第一座台灣木蘭聯賽盃 MLC 冠軍\n台中藍鯨 U15 女子足球隊參加第二屆台灣青年聯賽\n台中藍鯨 U18 女子足球隊參加第二屆台灣青年聯賽\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 8th Taiwan Mulan Football League\nSecond Japanese player 日高偉織 joined\nFirst Thai player 皮薩邁頌賽 joined\nFirst Thai goalkeeper 納塔魯亞牧塔納維奇 joined\nFourth Taiwan Mulan League title in club history\nFirst Taiwan Mulan League Cup (MLC) title in club history\nThe Taichung Blue Whale U15 girls'' team took part in the 2nd Taiwan Youth League\nThe Taichung Blue Whale U18 girls'' team took part in the 2nd Taiwan Youth League\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2022","title":{"zh":"2022 年","en":"2022"},"description":{"zh":"參加第九屆台灣木蘭足球聯賽\n代表台灣參加 AFC 女子足球俱樂部錦標賽(泰國)\n史上第三位泰國籍選手席拉萬茵樂敏加入\n疫情有成舉辦首場頂級足球開門賽\n台中藍鯨 U15 女子足球隊參加第三屆台灣青年聯賽\n台中藍鯨 U18 女子足球隊參加第三屆台灣青年聯賽\n台中藍鯨 U15 獲得第一座台灣青年聯賽 U15 女子組冠軍\n隊史第二座台灣木蘭聯賽亞軍\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 9th Taiwan Mulan Football League\nRepresented Taiwan at the AFC Women''s Club Championship (Thailand)\nThird Thai player in club history, 席拉萬茵樂敏, joined\nAfter successfully managing the pandemic, held the first top-level football opening match\nThe Taichung Blue Whale U15 girls'' team took part in the 3rd Taiwan Youth League\nThe Taichung Blue Whale U18 girls'' team took part in the 3rd Taiwan Youth League\nThe Taichung Blue Whale U15 team won its first Taiwan Youth League U15 girls'' title\nSecond Taiwan Mulan League runners-up finish in club history\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2023","title":{"zh":"2023 年","en":"2023"},"description":{"zh":"參加第十屆台灣木蘭足球聯賽\n台中足球園區動土並獲邀參加動土典禮\n選手蘇育萱輸出旅外中國成功\n史上第四位泰國籍選手席菲拉萬茵樂敏加入\n史上第五位泰國籍選手薩瓦拉克彭甘加入\nFB 粉絲人數達 16500 人\n隊史第五座台灣木蘭聯賽冠軍\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 10th Taiwan Mulan Football League\nBroke ground on the Taichung football park and was invited to the groundbreaking ceremony\nPlayer 蘇育萱 successfully moved abroad to China\nFourth Thai player in club history, 席菲拉萬茵樂敏, joined\nFifth Thai player in club history, 薩瓦拉克彭甘, joined\nFacebook followers reached 16,500\nFifth Taiwan Mulan League title in club history\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2024","title":{"zh":"2024 年","en":"2024"},"description":{"zh":"參加第十一屆台灣木蘭足球聯賽\n成立台中藍鯨 U10 女子隊\n台中藍鯨 U10 女子隊首次參加臺中市市長盃\n獲邀參加陽信盃國際邀請賽並獲得冠軍\n代表台灣參加 24/25 年亞足聯女子冠軍聯賽並順利小組晉級\n隊史第三座台灣木蘭聯賽亞軍\n史上第六位泰國籍選手第二位守門員瓦拉邦汶廷加入\n隊史第二位外籍選手薩瓦拉克彭甘獲得台灣木蘭足球聯賽年度金靴獎\n承接運動 i 台灣-運動熱區推廣計畫","en":"Took part in the 11th Taiwan Mulan Football League\nFounded the Taichung Blue Whale U10 girls'' team\nThe Taichung Blue Whale U10 girls'' team took part in the Taichung Mayor''s Cup for the first time\nInvited to the 陽信盃 international invitational tournament and won the title\nRepresented Taiwan in the 2024/25 AFC Women''s Champions League and advanced from the group stage\nThird Taiwan Mulan League runners-up finish in club history\nSixth Thai player in club history and second Thai goalkeeper, 瓦拉邦汶廷, joined\nThe club''s second foreign player, 薩瓦拉克彭甘, won the Taiwan Mulan Football League Golden Boot of the year\nTook on the Sports iTaiwan sports hotspot promotion programme"}},{"date":"2025","title":{"zh":"2025 年","en":"2025"},"description":{"zh":"參加第十二屆台灣木蘭足球聯賽\n代表台灣參加 2024 年至 2025 年亞足聯女子冠軍聯賽 8 強賽獲得亞洲前 8 名成績\n2025 台灣總統盃足球錦標賽亞軍\n史上第七位泰國籍選手第三位守門員邱瑪尼-通蒙戈加入\n史上第二位泰國籍選手冼仲意加入\n總教練呂桂花獲得 AFC 亞足聯亞洲最佳女足隊教練提名\n球衣首次放上公益團體機關台中惠明盲校\n承接運動 i 台灣-運動熱區推廣計畫\n首次接受英國世界足球雜誌專訪","en":"Took part in the 12th Taiwan Mulan Football League\nRepresented Taiwan in the 2024-25 AFC Women''s Champions League quarter-finals and finished among the top 8 in Asia\nRunners-up at the 2025 Taiwan President''s Cup football tournament\nSeventh Thai player in club history and third Thai goalkeeper, 邱瑪尼-通蒙戈, joined\nSecond Thai player in club history, 冼仲意, joined\nHead coach 呂桂花 was nominated for the AFC Asian Best Women''s Team Coach award\nFor the first time, the team shirt carried a public-interest organisation: 台中惠明盲校, a school for the blind in Taichung\nTook on the Sports iTaiwan sports hotspot promotion programme\nFirst interview with the British magazine World Soccer"}}]}},{"blockType":"text","content":{"body":{"zh":"上列沿革整理自舊官網公開內容，部分年度屆數標示與其他資料有出入，正式版本將於俱樂部確認後更新。","en":"The history above is compiled from content published on the former official website. The edition numbers of some seasons differ from other sources and will be updated once the club has confirmed them."}}}]}', N'b32d6c0e134b0faa39569882c726a5f5b15006f5619bd0c1563829319340d259');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'club/player-development';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2642f812-d372-5156-9706-037c5f66b8b5';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'club/player-development', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'球員培育重點｜台中藍鯨女子足球隊', N'台中藍鯨球員培育重點：技術戰術分析、體能訓練、比賽判讀、心理韌性、影片分析、個人發展計畫、營養與生活、教育與語言，各面向詳細內容整理中。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'9ffed93e-007a-5578-8bc4-f81ba87dd9e8', @id, N'steps', N'{"items":[{"title":{"zh":"技術戰術分析","en":"Technical & Tactical Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"體能訓練","en":"Physical Fitness Training"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"比賽判讀","en":"Match Reading"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"心理韌性","en":"Mental Resilience"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"影片分析","en":"Video Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"IDP 個人發展計畫","en":"Individual Development Plan"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"營養與生活","en":"Nutrition & Lifestyle"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"教育與語言","en":"Education & Language"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}}]}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'a667ce64-d8f5-5e3e-84ff-8d948cdc9f8a', @id, N'text', N'{"body":{"zh":"八大面向，持續培育選手\n\n八大面向共同支持一線隊與青年隊球員的成長，銜接青年隊訓練與國際發展通道，協助選手持續進步。","en":"Eight areas, continued growth\n\nThe eight areas together support the growth of First Team and Youth team players, linking Youth team training with International Pathways to help players keep improving."}}', 1);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'de9c548a-203a-5b9b-b8f4-c3612168015f', @id, 1, N'{"seo":{"zh":{"seoTitle":"球員培育重點｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨球員培育重點：技術戰術分析、體能訓練、比賽判讀、心理韌性、影片分析、個人發展計畫、營養與生活、教育與語言，各面向詳細內容整理中。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"技術戰術分析","en":"Technical & Tactical Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"體能訓練","en":"Physical Fitness Training"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"比賽判讀","en":"Match Reading"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"心理韌性","en":"Mental Resilience"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"影片分析","en":"Video Analysis"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"IDP 個人發展計畫","en":"Individual Development Plan"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"營養與生活","en":"Nutrition & Lifestyle"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}},{"title":{"zh":"教育與語言","en":"Education & Language"},"description":{"zh":"詳細內容準備中，稍後將於本頁公開。","en":"Details are being prepared and will be published on this page soon."}}]}},{"blockType":"text","content":{"body":{"zh":"八大面向，持續培育選手\n\n八大面向共同支持一線隊與青年隊球員的成長，銜接青年隊訓練與國際發展通道，協助選手持續進步。","en":"Eight areas, continued growth\n\nThe eight areas together support the growth of First Team and Youth team players, linking Youth team training with International Pathways to help players keep improving."}}}]}', N'abaf26963b53313f6bbc32662c1b502d540d02e7c25ef1d15ca98b3017bcfab8');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'club/opportunities';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c32bbf3c-8e01-54e5-94cc-5b1a12b06289';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'club/opportunities', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'球員機會｜台中藍鯨女子足球隊', N'台中藍鯨女子足球隊球員機會：加入一線隊的資格與報名方式、試訓場次列表與線上報名、外籍球員招募管道。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'fd8d50f0-4ad2-5c8e-8105-124d7f5c0c63', @id, N'text', N'{"body":{"zh":"台中藍鯨一線隊代表俱樂部出戰台灣木蘭足球聯賽，持續招募具備競技實力的球員加入陣容。填寫報名表後，將由俱樂部與您聯繫後續評估流程。","en":"The Taichung Blue Whale First Team represents the club in the Taiwan Mulan Football League and continues to recruit players with the competitive ability to join the squad. After you submit the registration form, the club will contact you about the next steps of the assessment."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'6df0492c-4c6d-519f-928c-334609411b79', @id, 1, N'{"seo":{"zh":{"seoTitle":"球員機會｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨女子足球隊球員機會：加入一線隊的資格與報名方式、試訓場次列表與線上報名、外籍球員招募管道。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"台中藍鯨一線隊代表俱樂部出戰台灣木蘭足球聯賽，持續招募具備競技實力的球員加入陣容。填寫報名表後，將由俱樂部與您聯繫後續評估流程。","en":"The Taichung Blue Whale First Team represents the club in the Taiwan Mulan Football League and continues to recruit players with the competitive ability to join the squad. After you submit the registration form, the club will contact you about the next steps of the assessment."}}}]}', N'6a6c837adb0bb6363757878b0b3ff1bf8b5da0e88c47f10da4af4fd00ead3494');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'club/international-pathways';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5e770656-1852-5d62-8352-b54963e10cbf';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'club/international-pathways', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'國際發展通道｜台中藍鯨女子足球隊', N'台中藍鯨國際發展通道：球員旅外日本、中國的真實案例，以及海外試訓與媒合諮詢管道。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'da7596ff-9269-5336-b1ba-498b530025c8', @id, N'steps', N'{"items":[{"title":{"zh":"在地","en":"Local Roots"}},{"title":{"zh":"國內職業","en":"Domestic Pro"}},{"title":{"zh":"海外訓練","en":"Overseas Training"}},{"title":{"zh":"海外俱樂部","en":"Clubs Abroad"}}]}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'59155ca1-cd8b-578d-a44f-43b66b7f1deb', @id, 1, N'{"seo":{"zh":{"seoTitle":"國際發展通道｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨國際發展通道：球員旅外日本、中國的真實案例，以及海外試訓與媒合諮詢管道。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"在地","en":"Local Roots"}},{"title":{"zh":"國內職業","en":"Domestic Pro"}},{"title":{"zh":"海外訓練","en":"Overseas Training"}},{"title":{"zh":"海外俱樂部","en":"Clubs Abroad"}}]}}]}', N'199afde2b4fab8e661c0dfc9651999e65246ec0a9bc1b4dc52f4e027d3d01403');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'club/player-stories';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ae54697e-3d51-5c17-b279-74fffc6b75a1';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'club/player-stories', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'球員故事｜台中藍鯨女子足球隊', N'台中藍鯨女子足球隊球員故事案例，記錄選手從加入台中藍鯨到邁向更大舞台的真實歷程。案例陸續建立中。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'04e1d3f9-dd51-50d6-8efc-a894106afc5c', @id, N'text', N'{"body":{"zh":"目前尚無已核實、取得肖像同意的球員故事案例，內容將依球員名單與肖像同意進度陸續建立。","en":"There are no verified player stories with image consent yet. Cases will be added as the squad list and image consents progress."}}', 0);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'eb72cfe0-7357-554a-998b-1855bb397839', @id, 1, N'{"seo":{"zh":{"seoTitle":"球員故事｜台中藍鯨女子足球隊","seoDescription":"台中藍鯨女子足球隊球員故事案例，記錄選手從加入台中藍鯨到邁向更大舞台的真實歷程。案例陸續建立中。"}},"blocks":[{"blockType":"text","content":{"body":{"zh":"目前尚無已核實、取得肖像同意的球員故事案例，內容將依球員名單與肖像同意進度陸續建立。","en":"There are no verified player stories with image consent yet. Cases will be added as the squad list and image consents progress."}}}]}', N'd5d06e187b9576b3712e1eb877b9c4c7220f0b926f94b39d3302d11bae425745');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'partners/become-a-partner';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'052ac23a-2e2f-58ad-8bfe-6c70e4166bb7';
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'partners/become-a-partner', N'published', '2026-09-30T00:00:00');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', N'成為合作夥伴 Become a Partner｜合作夥伴與贊助｜台中藍鯨女子足球隊', N'了解與台中藍鯨女子足球隊合作的六大價值：品牌曝光、受眾觸及、社會影響力與國際發展網絡。');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'50ee9e34-ebd8-5aae-ab2d-02bfcee72d7a', @id, N'steps', N'{"items":[{"title":{"zh":"為何合作","en":"Why Partner"},"description":{"zh":"台中藍鯨是台灣木蘭聯賽的球隊之一，是品牌走進女子足球與社區場域的直接管道。","en":"Taichung Blue Whale is one of the teams in the Taiwan Mulan Football League, giving brands a direct route into women''s football and the local community."}},{"title":{"zh":"受眾分析","en":"Audience Analysis"},"description":{"zh":"涵蓋一線隊球迷、青年隊學員家庭與賽事現場觀眾，詳細數據見下方。","en":"Covers First Team fans, Youth team student families and matchday crowds. Detailed figures are below."}},{"title":{"zh":"品牌曝光","en":"Brand Exposure"},"description":{"zh":"球衣、場邊看板、官網與社群通路曝光，讓品牌與職業足球賽事同框。","en":"Exposure on the kit, pitchside boards, our website and social media channels puts your brand alongside professional football fixtures."}},{"title":{"zh":"社會影響力","en":"Social Impact"},"description":{"zh":"結合社區足球推廣活動，讓品牌參與有意義的在地社會實踐。","en":"Community football promotion activities let your brand take part in meaningful local social action."}},{"title":{"zh":"中介影響力","en":"Community Reach"},"description":{"zh":"透過青年隊與推廣活動與家庭、學校建立長期信任關係，擴大品牌口碑觸及。","en":"Through the Youth team and our community promotion activities, build long-term trust with families and schools and widen your brand''s word-of-mouth reach."}},{"title":{"zh":"國際影響力","en":"International Impact"},"description":{"zh":"藉由國際交流與海外拓展計畫，讓品牌隨台中藍鯨球員的腳步走向世界。","en":"Through international exchange and overseas expansion programs, let your brand travel the world with Taichung Blue Whale''s players."}}]}', 0);
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (N'36d8cef0-bdbd-5771-afe3-b5ef8d79902b', @id, N'text', N'{"body":{"zh":"完整受眾與社群數據報告可於洽談合作時提供，歡迎聯絡商務部索取最新資料。","en":"The full audience and social media data report is available when we discuss a partnership. Please contact our Partnerships Department for the latest figures."}}', 1);
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (N'135ec756-e174-5ccb-9785-a9e3fa83e727', @id, 1, N'{"seo":{"zh":{"seoTitle":"成為合作夥伴 Become a Partner｜合作夥伴與贊助｜台中藍鯨女子足球隊","seoDescription":"了解與台中藍鯨女子足球隊合作的六大價值：品牌曝光、受眾觸及、社會影響力與國際發展網絡。"}},"blocks":[{"blockType":"steps","content":{"items":[{"title":{"zh":"為何合作","en":"Why Partner"},"description":{"zh":"台中藍鯨是台灣木蘭聯賽的球隊之一，是品牌走進女子足球與社區場域的直接管道。","en":"Taichung Blue Whale is one of the teams in the Taiwan Mulan Football League, giving brands a direct route into women''s football and the local community."}},{"title":{"zh":"受眾分析","en":"Audience Analysis"},"description":{"zh":"涵蓋一線隊球迷、青年隊學員家庭與賽事現場觀眾，詳細數據見下方。","en":"Covers First Team fans, Youth team student families and matchday crowds. Detailed figures are below."}},{"title":{"zh":"品牌曝光","en":"Brand Exposure"},"description":{"zh":"球衣、場邊看板、官網與社群通路曝光，讓品牌與職業足球賽事同框。","en":"Exposure on the kit, pitchside boards, our website and social media channels puts your brand alongside professional football fixtures."}},{"title":{"zh":"社會影響力","en":"Social Impact"},"description":{"zh":"結合社區足球推廣活動，讓品牌參與有意義的在地社會實踐。","en":"Community football promotion activities let your brand take part in meaningful local social action."}},{"title":{"zh":"中介影響力","en":"Community Reach"},"description":{"zh":"透過青年隊與推廣活動與家庭、學校建立長期信任關係，擴大品牌口碑觸及。","en":"Through the Youth team and our community promotion activities, build long-term trust with families and schools and widen your brand''s word-of-mouth reach."}},{"title":{"zh":"國際影響力","en":"International Impact"},"description":{"zh":"藉由國際交流與海外拓展計畫，讓品牌隨台中藍鯨球員的腳步走向世界。","en":"Through international exchange and overseas expansion programs, let your brand travel the world with Taichung Blue Whale''s players."}}]}},{"blockType":"text","content":{"body":{"zh":"完整受眾與社群數據報告可於洽談合作時提供，歡迎聯絡商務部索取最新資料。","en":"The full audience and social media data report is available when we discuss a partnership. Please contact our Partnerships Department for the latest figures."}}}]}', N'c8ff34aadbaf63cadbb156c1689c483ae3ad725630f7967eacebd6de4e569ea9');
  COMMIT TRANSACTION;
END
GO

-- ── 26. banners／banners_i18n：首頁輪播，一律 draft＋佔位圖鍵（沒有可上傳素材，公開端點不會顯示） ──
DECLARE @id uniqueidentifier;
SELECT @id = b.id FROM banners b JOIN banners_i18n bi ON bi.banner_id = b.id AND bi.locale = N'zh-Hant'
  WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND bi.title = N'在地扎根 放眼世界';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'300ec766-28d5-5fe5-b1f7-d14f5fd60204';
  INSERT INTO banners (id, club_id, media_type, image_key, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'image', N'seed-placeholder/no-image', 0, N'draft');
  INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url) VALUES (@id, N'zh-Hant', N'在地扎根 放眼世界', N'台中磐石足球俱樂部 · 2024 年創立 · 2024 全國乙級聯賽冠軍', N'【測試】輪播圖片說明（尚未上傳圖片）', N'認識台中磐石', N'/zh/about/', N'查看賽程', N'/zh/schedule/');
  INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url) VALUES (@id, N'en', N'LOCAL ROOTS. GLOBAL PATHWAYS.', NULL, NULL, N'認識台中磐石', N'/zh/about/', N'查看賽程', N'/zh/schedule/');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = b.id FROM banners b JOIN banners_i18n bi ON bi.banner_id = b.id AND bi.locale = N'zh-Hant'
  WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND bi.title = N'【測試】第二張輪播標題';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9a0e4454-0fb3-5f6d-ad14-4f2b5859c2ca';
  INSERT INTO banners (id, club_id, media_type, image_key, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'image', N'seed-placeholder/no-image', 1, N'draft');
  INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url) VALUES (@id, N'zh-Hant', N'【測試】第二張輪播標題', N'【測試】這是測試用內容，正式內容上線前請於後台替換。', N'【測試】輪播圖片說明（尚未上傳圖片）', N'加入球隊', N'/zh/join/player/', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = b.id FROM banners b JOIN banners_i18n bi ON bi.banner_id = b.id AND bi.locale = N'zh-Hant'
  WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND bi.title = N'航向世界的藍鯨';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'76bf804a-2fe7-5324-97d1-b97734c2a5d6';
  INSERT INTO banners (id, club_id, media_type, image_key, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'image', N'seed-placeholder/no-image', 0, N'draft');
  INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url) VALUES (@id, N'zh-Hant', N'航向世界的藍鯨', N'台中藍鯨女子足球隊 · 2014 年 4 月 12 日成立 · 隊史五度奪得木蘭聯賽冠軍', N'【測試】輪播圖片說明（尚未上傳圖片）', N'認識台中藍鯨', N'/zh/about/', N'查看賽程', N'/zh/schedule/');
  INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url) VALUES (@id, N'en', N'Taichung Blue Whale rides the waves towards the open ocean', NULL, NULL, N'認識台中藍鯨', N'/zh/about/', N'查看賽程', N'/zh/schedule/');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = b.id FROM banners b JOIN banners_i18n bi ON bi.banner_id = b.id AND bi.locale = N'zh-Hant'
  WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND bi.title = N'【測試】第二張輪播標題';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a2bd3ef6-20bb-5402-a39a-ec2dc2bd06f6';
  INSERT INTO banners (id, club_id, media_type, image_key, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'image', N'seed-placeholder/no-image', 1, N'draft');
  INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url) VALUES (@id, N'zh-Hant', N'【測試】第二張輪播標題', N'【測試】這是測試用內容，正式內容上線前請於後台替換。', N'【測試】輪播圖片說明（尚未上傳圖片）', N'加入球隊', N'/zh/join/player/', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

-- ── 27. faqs／faqs_i18n／faq_category_links／faq_embed_slot_links：B4 常見問題 ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-01';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4e8a298a-b7da-5c85-a6af-316f4a32f035';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-01', 0, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】加入球隊的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'join-team'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'trials'), 0);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-02';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b2fc78ae-7d6e-518b-9c87-dc24b98053c7';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-02', 1, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】學院招生的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'academy-admission'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'academy_admission'), 10);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-03';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'94ed2cf6-f5ec-5574-b874-d74a6adaf89d';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-03', 2, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】課程與營隊報名的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'programs-camps'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'program_detail'), 20);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-04';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e8bfe985-6e66-53e5-bb83-b1547a475a13';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-04', 3, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】費用與退費的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'fees-refunds'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'program_detail'), 30);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-05';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c9e7b71d-75f1-5a3f-a8c2-4bc414c27345';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-05', 4, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】試訓的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'trials'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'trials'), 40);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-06';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'17b75c37-013c-5df9-a3cb-3424face0fa2';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-06', 5, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】國際發展與海外球員的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'international'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-07';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cce0c233-acec-5640-a3f4-e00a12708ddc';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-07', 6, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】女子足球的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'womens-football'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-08';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'13700b8f-d104-5439-8296-44651a10497a';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-08', 7, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】球迷會與商品的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'fan-club-merchandise'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-09';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bbc749eb-9f34-58d1-8bb7-096f51d09925';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-09', 8, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】合作與贊助的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'partnerships-sponsorship'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'sponsorship'), 80);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-faq-10';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7eeb7671-0485-5555-9d29-426feb429b79';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-faq-10', 9, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'【測試】其他的常見問題範例？', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'other'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-trial-class';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'03986272-946f-5e90-8165-dfef053ffe7d';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-trial-class', 0, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'可以先試上嗎？', N'可以，請直接到現場上課然後現金繳費。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'trials'));
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'programs-camps'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'trials'), 0);
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'program_detail'), 1);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-single-class';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'934475bb-6d30-540c-b34a-9d5eeb33e299';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-single-class', 1, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'可以先上單堂嗎？', N'可以，現場上課後現金繳費；守門員班請先填報名表單。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'programs-camps'));
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'fees-refunds'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'program_detail'), 10);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-weather';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ebe452de-f57a-5f18-9d1f-dc0a81487f33';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-weather', 2, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'天氣不穩定怎麼知道今天要不要上課？', N'兒童班課前 1.5 小時在 LINE 群組公告；若無公告為正常上課；活動類型課程另於太原足球場 FB 粉絲頁公告。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'programs-camps'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'program_detail'), 20);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-leave-refund';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6bb65126-63f1-5a7f-86bb-ff479531b5a5';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-leave-refund', 3, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'請假可以折抵退費嗎？', N'單堂收費，請假無折抵學費或補課。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'fees-refunds'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-payment-methods';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0b346f65-23fa-5c92-ac5f-3d61dbf5139b';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-payment-methods', 4, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'可以刷卡或數位支付嗎？', N'沒有刷卡、數位支付服務。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'fees-refunds'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-siblings';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'59c75c48-f45b-5a55-a7b5-e3be665346af';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-siblings', 5, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'弟弟可以跟哥哥同一班嗎？', N'不建議，請評估身體與心理狀態及運動強度，強行加入容易受傷。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'programs-camps'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-football-shoes';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e5d44f9a-d414-5a5a-a1aa-3af9cad9352c';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-football-shoes', 6, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'上課一定要買足球鞋嗎？', N'不需要，如要購買可詢問教練。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'programs-camps'));
  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = N'program_detail'), 60);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-beginners';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7a2b5380-bf86-5881-998f-f06ebfd7515d';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-beginners', 7, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'沒有經驗才能參加嗎？', N'課程是推廣性質，沒有經驗也可以參加；進階課程請衡量自身狀態。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'join-team'));
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'programs-camps'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-u15-trial';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'93410cb3-52de-5f48-aeaa-cb6393452728';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-u15-trial', 8, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'U15 女子隊可以試上嗎？', N'可以試上，費用 300 元／次（現場支付）。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'trials'));
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'join-team'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-u15-experience';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8c3983ec-a855-552f-9587-904d60e73a09';
  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-u15-experience', 9, N'published');
  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', N'U15 女子隊需要有程度才能參加嗎？', N'沒有經驗也可以參加，只要認真學習有機會一起成長成為選手。');
  INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@id, (SELECT id FROM faq_categories WHERE slug = N'join-team'));
  COMMIT TRANSACTION;
END
GO

-- ── 28. programs／programs_i18n／sessions：P1 課程項目與 P2 梯次（bw 真實、tcrfc 測試） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-childrens-training-mixed-age';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'abb45be3-1603-5c6a-bb08-8e5a537585d8';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-childrens-training-mixed-age', N'children_training', 5, 12, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'【測試】兒童足球訓練（混齡體驗班）', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'668a5d64-2928-5e9a-b7cb-1b2f4ffd226c', (SELECT id FROM clubs WHERE code = N'tcrfc'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-10-12', N'2027-01-18', N'{"mon":"18:00-19:30","wed":"18:00-19:30"}', 20, 8, 4800, 4200, N'2026-10-05', N'2026-09-20T00:00:00', N'2026-10-10T23:59:00', N'開放');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'24f438bd-c075-5843-b3b2-edd3eeaebbad', (SELECT id FROM clubs WHERE code = N'tcrfc'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-04-06', N'2026-06-29', N'{"mon":"18:00-19:30"}', 20, 20, 4800, NULL, NULL, NULL, NULL, N'已結束');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-summer-camp';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3685ee3a-cca5-5c81-a5a7-6856ced8a605';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-summer-camp', N'summer_camp', 8, 14, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'【測試】暑期足球營', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'5dbc3be0-a26c-5805-b257-38a10f3ea28b', (SELECT id FROM clubs WHERE code = N'tcrfc'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2027-07-05', N'2027-07-09', N'{"mon-fri":"09:00-16:00"}', 30, 30, 6000, NULL, NULL, NULL, NULL, N'額滿');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'2e6d6609-33ad-5dd7-a885-b6c88af39855', (SELECT id FROM clubs WHERE code = N'tcrfc'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2027-07-19', N'2027-07-23', N'{"mon-fri":"09:00-16:00"}', 30, 30, 6000, NULL, NULL, NULL, NULL, N'候補');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-winter-camp';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fa727899-7d6c-5e2b-9d75-ae5777b11b09';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-winter-camp', N'winter_camp', 8, 14, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'【測試】寒假足球營', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'39e5f4cd-ba27-5aea-9c66-39956898eca7', (SELECT id FROM clubs WHERE code = N'tcrfc'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2027-01-25', N'2027-01-29', N'{"mon-fri":"09:00-16:00"}', 25, 3, 5500, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-specialist-goalkeeper';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'063bb172-0756-5a5e-92af-9080abaf4f73';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-specialist-goalkeeper', N'specialist_training', 10, 15, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'【測試】守門員專項訓練', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'2291c3e7-a55f-5a32-831d-1dcbc70c60ca', (SELECT id FROM clubs WHERE code = N'tcrfc'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-11-07', N'2026-12-19', N'{"sat":"09:00-11:00"}', 12, 5, 3600, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-school-community';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0bef42c3-ab04-5792-a165-4d070ac24b62';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-school-community', N'school_community', NULL, NULL, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'【測試】校園與社區足球推廣', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'bcb3d5c8-eb19-5f09-9d13-2fbab7bdcbc7', (SELECT id FROM clubs WHERE code = N'tcrfc'), @id, NULL, N'2026-11-20', N'2026-11-20', NULL, NULL, 0, NULL, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-draft-program';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a751caab-94cf-5e64-8595-be2a29558766';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-draft-program', N'children_training', 6, 10, N'draft');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'【測試】草稿課程', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-community-football-school';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'50edbef9-732e-59f2-acfe-4b9d86ac9ab2';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-community-football-school', N'children_training', NULL, NULL, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'社區足球學校（小藍鯨）', N'2017 年成立的社區足球學校，暱稱小藍鯨。強調運動樂趣、身體健康、團隊合作和足球技能學習；封閉式專用足球場、由經驗豐富的教練指導。免試上、免測試、免入會費，現場個人報名；原價 300 元／堂，優待價均一價 200 元／堂，現場單次單堂現金繳交。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'e8b2f4df-6691-5c5c-8ba5-a6c3151d6235', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, NULL, NULL, 0, 200, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-toddler-football';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ce7942ef-cce7-57ba-88d5-38622d0ef3a7';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-toddler-football', N'children_training', 3, 4, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'社區幼幼足球班', N'運動 i 台灣 2.0 運動熱區課程。男女不拘，每週一、三，1.5 小時／堂，200 元／堂，現場個人報名。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'6f330d41-f1b4-5d23-af81-69bbd2522c13', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, N'{"mon":"1.5 小時","wed":"1.5 小時"}', NULL, 0, 200, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-kids-community-football';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7f80867b-3577-590b-9085-e7c6d567d3a8';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-kids-community-football', N'children_training', 6, 8, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'幼兒社區足球班', N'運動 i 台灣 2.0 運動熱區課程。男女不拘，每週一、三，1.5 小時／堂，200 元／堂，現場個人報名。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'4aecf328-f82f-5793-b7f7-678fa622f713', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, N'{"mon":"1.5 小時","wed":"1.5 小時"}', NULL, 0, 200, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-u8-football-class';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'766c27a0-3d9c-5a89-9276-abb867610fb4';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-u8-football-class', N'children_training', 9, 10, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'藍鯨 U8 足球教室', N'運動 i 台灣 2.0 運動熱區課程。男女不拘，每週一、三，1.5 小時／堂，200 元／堂，現場個人報名。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'5ff905ec-5981-55a0-aeef-26af5545b56c', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, N'{"mon":"1.5 小時","wed":"1.5 小時"}', NULL, 0, 200, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-u10-football-class';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd24e15f6-bbe1-511c-8918-bde639de0c49';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-u10-football-class', N'children_training', 9, 10, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'藍鯨 U10 足球教室', N'運動 i 台灣 2.0 運動熱區課程。男女不拘，每週一、三，1.5 小時／堂，200 元／堂，現場個人報名。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'7173d1a9-ca78-5595-be45-7b8e542f2779', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, N'{"mon":"1.5 小時","wed":"1.5 小時"}', NULL, 0, 200, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-u12-girls-class';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9f553712-e111-599e-9aec-7a32e793872d';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-u12-girls-class', N'children_training', 10, 12, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'藍鯨 U12 女子足球班', N'運動 i 台灣 2.0 運動熱區課程。限女性，每週一、三、五，1.5 小時／堂，200 元／堂，現場個人報名。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'1b52b29f-6e6e-589a-b12a-c29d18983243', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, N'{"mon":"1.5 小時","wed":"1.5 小時","fri":"1.5 小時"}', NULL, 0, 200, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-u15-girls-class';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a18d5d90-8906-590c-9ba3-0f17a800e88c';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-u15-girls-class', N'children_training', 13, NULL, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'藍鯨 U15 女子足球班', N'運動 i 台灣 2.0 運動熱區課程。13 歲以上，限女性，每週五，1.5 小時／堂，200 元／堂，現場個人報名。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'35fbc098-b53f-59ca-b18a-194e339cdf41', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, N'{"fri":"1.5 小時"}', NULL, 0, 200, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-goalkeeper-basic';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ac23dc74-547a-5c82-b601-04f60ac82508';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-goalkeeper-basic', N'specialist_training', 7, 12, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'藍鯨守門員基礎班', N'運動 i 台灣 2.0 運動熱區課程。7–12 歲，男女不拘、女生保留錄取，限額 10 位，1.5 小時／堂，200 元／堂，須先填寫報名表單。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'8cf91ee4-c6f9-50e0-b594-d129400bd99e', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, NULL, 10, 0, 200, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-coach-workshop-2025';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'95d79334-40c3-5d04-81b6-71db65bb303b';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-coach-workshop-2025', N'specialist_training', NULL, NULL, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'足球人才教練暨 TDS 守門員人才培訓（教練講習）', N'114 年 8 月 24 日、25 日共 2 天，具備教練資格且目前有實際帶隊之教練優先錄取；每人新臺幣 800 元，當日現場繳交（含資料、保險、午餐等）。指導機關：教育部體育署、臺中市政府運動局；辦理機構：國立臺灣體育運動大學；合辦機關：臺中市女子足球協會、台中藍鯨女子足球隊；協辦機關：中華民國足球協會。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'a225ce16-f2f6-53bd-891f-4652452ecb9e', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2025-08-24', N'2025-08-25', NULL, NULL, 0, 800, NULL, NULL, NULL, NULL, N'已結束');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-football-free-day';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'98e1e377-0cd5-5c19-ace6-e37d7adbd21c';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-football-free-day', N'school_community', NULL, NULL, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'藍鯨足球自由日', N'運動 i 台灣 2.0 運動熱區活動。全年齡、男女不拘，1.5 小時／次，免費，不需報名。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'6cf78959-4f83-5e73-b9d1-36516546b10a', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, NULL, NULL, 0, 0, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-children-football-day';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3b0c6a6b-1323-56c2-81ca-44a6d95dd302';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-children-football-day', N'school_community', NULL, NULL, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'兒童足球日', N'運動 i 台灣 2.0 運動熱區活動。幼兒園、國小，男女不拘，3 小時／次，免費，限團體（請至太原足球場 FB 粉絲頁私訊申請）。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'672ae6bf-1c18-5220-af94-a25e22a81406', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, NULL, NULL, 0, 0, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'bw-adult-football-match';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'80d4a50e-776f-5643-a7ef-9835e76a0722';
  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-adult-football-match', N'school_community', NULL, NULL, N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'野團成人足球賽', N'運動 i 台灣 2.0 運動熱區活動。國中年齡以上、男女不拘，2–3 小時／次，場地費 100 元／人；團體（8 人以上）或個人現場報名。');
  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES (N'ee9f295c-2654-5cca-a933-dca11829cdba', (SELECT id FROM clubs WHERE code = N'bw'), @id, (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), NULL, NULL, NULL, NULL, 0, 100, NULL, NULL, NULL, NULL, N'開放');
  COMMIT TRANSACTION;
END
GO

-- ── 29. standings：C4 積分榜（全部測試值，沒有真實積分來源） ──
IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND i.team_name = N'【測試】隊伍 A')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'c0be4279-b76f-5ce1-8ea9-367e8ebc35e4', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), 1, 3, 9);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'c0be4279-b76f-5ce1-8ea9-367e8ebc35e4', N'zh-Hant', N'【測試】隊伍 A');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND i.team_name = N'【測試】隊伍 B')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'8beb5319-135e-5103-abb6-a5e500f4b9a7', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), 2, 3, 7);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'8beb5319-135e-5103-abb6-a5e500f4b9a7', N'zh-Hant', N'【測試】隊伍 B');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND i.team_name = N'【測試】隊伍 C')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'ea420597-6742-5986-b9a4-ec972c9d46d7', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), 3, 3, 6);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'ea420597-6742-5986-b9a4-ec972c9d46d7', N'zh-Hant', N'【測試】隊伍 C');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND i.team_name = N'【測試】隊伍 D')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'bab061f1-44e8-5fb7-b9ee-74d4c0446cba', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), 4, 3, 4);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'bab061f1-44e8-5fb7-b9ee-74d4c0446cba', N'zh-Hant', N'【測試】隊伍 D');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND i.team_name = N'【測試】隊伍 E')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'06179fa4-2372-5fcb-8f9c-090a795c0efe', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), 5, 3, 3);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'06179fa4-2372-5fcb-8f9c-090a795c0efe', N'zh-Hant', N'【測試】隊伍 E');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND i.team_name = N'【測試】隊伍 F')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'75cf2815-9b03-5fc5-a11e-d8d3096144c8', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), 6, 3, 1);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'75cf2815-9b03-5fc5-a11e-d8d3096144c8', N'zh-Hant', N'【測試】隊伍 F');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023') AND i.team_name = N'【測試】隊伍 A')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'b26edb98-7e6e-5f04-8caf-166a88a2330b', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), 1, 15, 40);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'b26edb98-7e6e-5f04-8caf-166a88a2330b', N'zh-Hant', N'【測試】隊伍 A');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023') AND i.team_name = N'【測試】隊伍 B')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'2e4a522c-8350-588e-ab51-a558850a1e27', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), 2, 15, 34);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'2e4a522c-8350-588e-ab51-a558850a1e27', N'zh-Hant', N'【測試】隊伍 B');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023') AND i.team_name = N'【測試】隊伍 C')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'b7af59c8-202c-57fc-b62a-a3aade2651b4', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), 3, 15, 28);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'b7af59c8-202c-57fc-b62a-a3aade2651b4', N'zh-Hant', N'【測試】隊伍 C');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023') AND i.team_name = N'【測試】隊伍 D')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'9a36686f-b452-51be-a9b1-56c964f968e8', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), 4, 15, 21);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'9a36686f-b452-51be-a9b1-56c964f968e8', N'zh-Hant', N'【測試】隊伍 D');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023') AND i.team_name = N'【測試】隊伍 E')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'9aafd2df-8750-5393-a9a1-8490469124e8', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), 5, 15, 12);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'9aafd2df-8750-5393-a9a1-8490469124e8', N'zh-Hant', N'【測試】隊伍 E');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM standings s JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N'zh-Hant'
               WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023') AND i.team_name = N'【測試】隊伍 F')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO standings (id, club_id, season_id, rank, played, points)
  VALUES (N'0f317c2c-38b7-51e7-82a0-209c858e221c', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2023'), 6, 15, 5);
  INSERT INTO standings_i18n (standing_id, locale, team_name)
  VALUES (N'0f317c2c-38b7-51e7-82a0-209c858e221c', N'zh-Hant', N'【測試】隊伍 F');
  COMMIT TRANSACTION;
END
GO

-- ── 30. calendar_custom_events：L2 自建事件（bw 一筆真實過往活動、其餘測試） ──
DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.title = N'【測試】新賽季記者會';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b069fd5a-8d7a-51a6-a287-f5a45f48540a';
  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM event_types WHERE code = N'press_conference'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-10-08T14:00:00', N'2026-10-08T15:30:00', 0, NULL, NULL, 1);
  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', N'【測試】新賽季記者會', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO calendar_event_teams (source_type, source_id, team_id) VALUES (N'custom', @id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.title = N'【測試】球迷見面會';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'75e97c08-516c-5673-af3e-48f8e7522a78';
  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM event_types WHERE code = N'fan_meet'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-10-31T15:00:00', N'2026-10-31T17:00:00', 0, NULL, NULL, 1);
  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', N'【測試】球迷見面會', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO calendar_event_teams (source_type, source_id, team_id) VALUES (N'custom', @id, (SELECT id FROM teams WHERE code = N'D1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.title = N'【測試】每週公開訓練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3d16825f-105a-5150-8621-0813c69ba9b9';
  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM event_types WHERE code = N'open_training'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-10-14T17:00:00', N'2026-10-14T18:30:00', 0, N'weekly', N'2026-12-30', 1);
  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', N'【測試】每週公開訓練', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO calendar_event_teams (source_type, source_id, team_id) VALUES (N'custom', @id, (SELECT id FROM teams WHERE code = N'D1'));
  INSERT INTO calendar_event_exceptions (calendar_custom_event_id, excluded_on) VALUES (@id, N'2026-11-25');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.title = N'【測試】場地休館公告';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4d6c9d74-d91b-5dcc-94f3-f66b5b4f148d';
  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM event_types WHERE code = N'closure_notice'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-12-25T00:00:00', NULL, 1, NULL, NULL, 1);
  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', N'【測試】場地休館公告', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.title = N'【測試】內部工作會議（不公開）';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'96eea0cc-befe-5514-a710-bb1745598730';
  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM event_types WHERE code = N'other'), NULL, N'2026-10-20T10:00:00', N'2026-10-20T11:00:00', 0, NULL, NULL, 0);
  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', N'【測試】內部工作會議（不公開）', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.title = N'2024 台中女子足球節「夏洛特的下午茶」';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a02ae866-daa1-5d26-9316-49aeaa201483';
  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM event_types WHERE code = N'other'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2024-07-13T16:00:00', N'2024-07-13T18:00:00', 0, NULL, NULL, 1);
  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', N'2024 台中女子足球節「夏洛特的下午茶」', N'運動 i 台灣 & 台中女子足球節。臺中北屯太原足球場，15:30 報到、16:00 開始、18:00 結束；對象為國小 1–5 年級女生，推廣組與競賽組，公益推廣活動、全程免費參加。');
  INSERT INTO calendar_event_teams (source_type, source_id, team_id) VALUES (N'custom', @id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.title = N'【測試】球迷見面會';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'035153e4-904d-5a76-b66b-e324e99d8eea';
  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM event_types WHERE code = N'fan_meet'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2026-11-14T15:00:00', N'2026-11-14T17:00:00', 0, NULL, NULL, 1);
  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', N'【測試】球迷見面會', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO calendar_event_teams (source_type, source_id, team_id) VALUES (N'custom', @id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.title = N'【測試】公開訓練';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1d420be1-c18e-58bb-be13-5c7d44a68fad';
  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM event_types WHERE code = N'open_training'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%太原%'), N'2026-10-16T19:30:00', N'2026-10-16T21:00:00', 0, N'biweekly', N'2026-12-25', 1);
  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', N'【測試】公開訓練', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO calendar_event_teams (source_type, source_id, team_id) VALUES (N'custom', @id, (SELECT id FROM teams WHERE code = N'BW1'));
  COMMIT TRANSACTION;
END
GO

-- ── 31. tags／article_tags／value_tag_links：新聞標籤（全域主檔）與 tcrfc 既有新聞的歸類 ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM tags WHERE slug = N'youth-development';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f2d76565-c8b9-586e-90d7-6a3614d440c2';
  INSERT INTO tags (id, slug) VALUES (@id, N'youth-development');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'zh-Hant', N'青訓發展');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'en', N'Youth Development');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM tags WHERE slug = N'international';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b4a3cb8f-b347-5bc5-ae35-f929bca07905';
  INSERT INTO tags (id, slug) VALUES (@id, N'international');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'zh-Hant', N'國際交流');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'en', N'International');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM tags WHERE slug = N'match';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9bb68d3f-3534-57b0-8ef9-3082978d1245';
  INSERT INTO tags (id, slug) VALUES (@id, N'match');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'zh-Hant', N'賽事');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'en', N'Matches');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM tags WHERE slug = N'community';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b6020ca8-af02-5838-b4dd-6bf22b413244';
  INSERT INTO tags (id, slug) VALUES (@id, N'community');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'zh-Hant', N'社區');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'en', N'Community');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM tags WHERE slug = N'fans';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'912c2f6e-6465-5e44-9318-ad15d058f1a0';
  INSERT INTO tags (id, slug) VALUES (@id, N'fans');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'zh-Hant', N'球迷');
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'en', N'Fans');
  COMMIT TRANSACTION;
END
GO

INSERT INTO article_tags (article_id, tag_id)
SELECT a.id, t.id FROM articles a CROSS JOIN tags t
WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND a.article_category_id = (SELECT id FROM article_categories WHERE code = N'match') AND t.slug = N'match'
  AND NOT EXISTS (SELECT 1 FROM article_tags x WHERE x.article_id = a.id AND x.tag_id = t.id);
GO

INSERT INTO article_tags (article_id, tag_id)
SELECT a.id, t.id FROM articles a CROSS JOIN tags t
WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND a.article_category_id = (SELECT id FROM article_categories WHERE code = N'international') AND t.slug = N'international'
  AND NOT EXISTS (SELECT 1 FROM article_tags x WHERE x.article_id = a.id AND x.tag_id = t.id);
GO

INSERT INTO value_tag_links (entity_type, entity_id, value_tag)
SELECT N'article', a.id, N'global_pathways' FROM articles a
WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND a.article_category_id = (SELECT id FROM article_categories WHERE code = N'international')
  AND NOT EXISTS (SELECT 1 FROM value_tag_links x WHERE x.entity_type = N'article' AND x.entity_id = a.id AND x.value_tag = N'global_pathways');
GO

INSERT INTO article_tags (article_id, tag_id)
SELECT a.id, t.id FROM articles a CROSS JOIN tags t
WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND a.article_category_id = (SELECT id FROM article_categories WHERE code = N'community') AND t.slug = N'community'
  AND NOT EXISTS (SELECT 1 FROM article_tags x WHERE x.article_id = a.id AND x.tag_id = t.id);
GO

INSERT INTO value_tag_links (entity_type, entity_id, value_tag)
SELECT N'article', a.id, N'community' FROM articles a
WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND a.article_category_id = (SELECT id FROM article_categories WHERE code = N'community')
  AND NOT EXISTS (SELECT 1 FROM value_tag_links x WHERE x.entity_type = N'article' AND x.entity_id = a.id AND x.value_tag = N'community');
GO

INSERT INTO article_tags (article_id, tag_id)
SELECT a.id, t.id FROM articles a CROSS JOIN tags t
WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND a.article_category_id = (SELECT id FROM article_categories WHERE code = N'camps-events') AND t.slug = N'youth-development'
  AND NOT EXISTS (SELECT 1 FROM article_tags x WHERE x.article_id = a.id AND x.tag_id = t.id);
GO

INSERT INTO value_tag_links (entity_type, entity_id, value_tag)
SELECT N'article', a.id, N'players_first' FROM articles a
WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND a.article_category_id = (SELECT id FROM article_categories WHERE code = N'camps-events')
  AND NOT EXISTS (SELECT 1 FROM value_tag_links x WHERE x.entity_type = N'article' AND x.entity_id = a.id AND x.value_tag = N'players_first');
GO

UPDATE articles SET is_featured = 1
WHERE id IN (SELECT TOP 2 id FROM articles WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND status = N'published' ORDER BY published_at DESC, row_seq DESC);
GO

-- ── 31b. articles：藍鯨測試新聞（藍鯨舊站沒有自有新聞全文，見 content/blue-whale/news-index.md） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'bw-test-news-club';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e7000f49-a703-538a-9bc1-69649a11370d';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-test-news-club', (SELECT id FROM article_categories WHERE code = N'club'), N'published', N'2026-09-20T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title, summary) VALUES (@id, N'zh-Hant', N'【測試】台中藍鯨俱樂部消息範例', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'bw-test-news-match';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ed4ee3be-2b87-5104-9836-947fc1eec56c';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-test-news-match', (SELECT id FROM article_categories WHERE code = N'match'), N'published', N'2026-09-25T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title, summary) VALUES (@id, N'zh-Hant', N'【測試】台中藍鯨賽事報導範例', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO article_tags (article_id, tag_id) VALUES (@id, (SELECT id FROM tags WHERE slug = N'match'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = N'bw-test-news-community';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'62eae2c8-e8f8-5e7c-bdc6-8e517365dd5b';
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at) VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'bw-test-news-community', (SELECT id FROM article_categories WHERE code = N'community'), N'published', N'2026-09-28T00:00:00');
  INSERT INTO articles_i18n (article_id, locale, title, summary) VALUES (@id, N'zh-Hant', N'【測試】台中藍鯨社區活動範例', N'【測試】這是測試用內容，正式內容上線前請於後台替換。');
  INSERT INTO article_tags (article_id, tag_id) VALUES (@id, (SELECT id FROM tags WHERE slug = N'community'));
  INSERT INTO value_tag_links (entity_type, entity_id, value_tag) VALUES (N'article', @id, N'community');
  COMMIT TRANSACTION;
END
GO

-- ── 32. redirects：H3 301 轉址（舊站網址→新站，藍鯨兩種編碼各一筆） ──
IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'0366a68d-3d63-5005-a72f-48fc27ca78ff', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews', N'/zh/news/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/社區-台中磐石-taichung-rock-fc')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'7e462956-9ae5-50f0-b47c-8b27c6c5cbde', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/社區-台中磐石-taichung-rock-fc', N'/zh/news/community/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/%E7%A4%BE%E5%8D%80-%E5%8F%B0%E4%B8%AD%E7%A3%90%E7%9F%B3-taichung-rock-fc')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'3ef736fa-93e5-51ea-93a6-0607f85aa8cf', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/%E7%A4%BE%E5%8D%80-%E5%8F%B0%E4%B8%AD%E7%A3%90%E7%9F%B3-taichung-rock-fc', N'/zh/news/community/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/公告-台中磐石-taichung-rock-fc')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'7f4d86e1-a67b-5040-867d-150fe2fc8ede', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/公告-台中磐石-taichung-rock-fc', N'/zh/news/club/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/%E5%85%AC%E5%91%8A-%E5%8F%B0%E4%B8%AD%E7%A3%90%E7%9F%B3-taichung-rock-fc')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'f7875255-3084-5956-86a7-a6e65d7ca442', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/%E5%85%AC%E5%91%8A-%E5%8F%B0%E4%B8%AD%E7%A3%90%E7%9F%B3-taichung-rock-fc', N'/zh/news/club/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/2025台中磐石國際盃-2025-tcrfc-int-cup')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'bd42f902-1187-56aa-a550-0edf0fcb1044', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/2025台中磐石國際盃-2025-tcrfc-int-cup', N'/zh/news/match/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/2025%E5%8F%B0%E4%B8%AD%E7%A3%90%E7%9F%B3%E5%9C%8B%E9%9A%9B%E7%9B%83-2025-tcrfc-int-cup')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'c3d9085f-0e85-5dec-a569-884495ee4b17', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/2025%E5%8F%B0%E4%B8%AD%E7%A3%90%E7%9F%B3%E5%9C%8B%E9%9A%9B%E7%9B%83-2025-tcrfc-int-cup', N'/zh/news/match/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/一線隊-first-team')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'2cd88987-e66f-5f27-9cec-9b430dae48e3', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/一線隊-first-team', N'/zh/news/club/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/%E4%B8%80%E7%B7%9A%E9%9A%8A-first-team')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'2c5691b7-ba22-5fc5-b0d4-cb8d5a3f1eb6', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/%E4%B8%80%E7%B7%9A%E9%9A%8A-first-team', N'/zh/news/club/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/消息-news')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'0d6ba709-28c0-59bb-bed1-4bacb559b818', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/消息-news', N'/zh/news/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/%E6%B6%88%E6%81%AF-news')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'ad89b1b2-cacc-5814-aff6-964530490b87', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/%E6%B6%88%E6%81%AF-news', N'/zh/news/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/比賽-taichung-rock-fc')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'08dcdaed-780a-5b9b-824c-49d22838c48f', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/比賽-taichung-rock-fc', N'/zh/news/match/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/%E6%AF%94%E8%B3%BD-taichung-rock-fc')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'68623c6e-fd52-51a5-9e85-a3fb565d0a4b', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/%E6%AF%94%E8%B3%BD-taichung-rock-fc', N'/zh/news/match/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcnews/categories/english-news')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'e14aa8b2-c452-5d0c-a7e6-a04f40003a25', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcnews/categories/english-news', N'/en/news/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/product-page/厚底緩震機能襪')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'cedfe97f-07a2-55e5-a304-7d6cb2777351', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/product-page/厚底緩震機能襪', N'/zh/shop/cushioned-socks/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/product-page/%E5%8E%9A%E5%BA%95%E7%B7%A9%E9%9C%87%E6%A9%9F%E8%83%BD%E8%A5%AA')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'adfe85d3-4c2f-5dc5-b97b-939798f41c5a', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/product-page/%E5%8E%9A%E5%BA%95%E7%B7%A9%E9%9C%87%E6%A9%9F%E8%83%BD%E8%A5%AA', N'/zh/shop/cushioned-socks/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/product-page/台中磐石主場球衣-2026賽季')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'2b516e11-5565-56a6-8b40-2cbe5f84c34b', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/product-page/台中磐石主場球衣-2026賽季', N'/zh/shop/home-jersey-2026/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/product-page/%E5%8F%B0%E4%B8%AD%E7%A3%90%E7%9F%B3%E4%B8%BB%E5%A0%B4%E7%90%83%E8%A1%A3-2026%E8%B3%BD%E5%AD%A3')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'da8c5bfb-c040-59b7-a901-f49ae7551d2a', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/product-page/%E5%8F%B0%E4%B8%AD%E7%A3%90%E7%9F%B3%E4%B8%BB%E5%A0%B4%E7%90%83%E8%A1%A3-2026%E8%B3%BD%E5%AD%A3', N'/zh/shop/home-jersey-2026/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/category/足部裝備')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'043b6e71-7e66-517a-b305-336d5f9d1136', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/category/足部裝備', N'/zh/shop/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/category/%E8%B6%B3%E9%83%A8%E8%A3%9D%E5%82%99')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'625b9f7f-af59-59e2-94d3-db99cb37731a', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/category/%E8%B6%B3%E9%83%A8%E8%A3%9D%E5%82%99', N'/zh/shop/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/category/球衣-jersey')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'0d991c9c-64c3-5f0a-afef-3e3de30db157', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/category/球衣-jersey', N'/zh/shop/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/category/%E7%90%83%E8%A1%A3-jersey')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'2f0763b9-024e-5542-a096-0ec93b79d6ff', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/category/%E7%90%83%E8%A1%A3-jersey', N'/zh/shop/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/aboutus')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'c5a03c66-5753-5d86-9fa0-51486dadb7cf', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/aboutus', N'/zh/about/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcteam')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'ce262b67-d2e6-591a-a781-5d27a794deb2', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcteam', N'/zh/club/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/tcrfcacademy')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'72ddde90-79a4-5f4a-84a5-9e6ee31a9171', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/tcrfcacademy', N'/zh/academy/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/partnership')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'6e0faeb5-6091-5dbd-85c9-6ea5d1a4ff88', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/partnership', N'/zh/partners/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/比賽-matches')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'199749b1-4f16-5e11-a356-94076400a982', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/比賽-matches', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/%E6%AF%94%E8%B3%BD-matches')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'3cc60da8-af0e-5013-be40-0622db38c756', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/%E6%AF%94%E8%B3%BD-matches', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/積分表-tables')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'a3c7e1c9-4799-50c0-ac8b-5b87eff3bcae', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/積分表-tables', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/%E7%A9%8D%E5%88%86%E8%A1%A8-tables')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'09256346-23e4-5d0c-a988-31ca8dc67efb', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/%E7%A9%8D%E5%88%86%E8%A1%A8-tables', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND from_path = N'/en-home')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'66e3e16b-ea29-5dbc-b200-aa266c2eeaa1', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'/en-home', N'/en/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/首頁')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'98d465c7-8cc7-5bc5-83e0-e01edd531bf3', (SELECT id FROM clubs WHERE code = N'bw'), N'/首頁', N'/zh/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E9%A6%96%E9%A0%81')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'89a8d960-0ca3-5f65-b4e3-f452176b8331', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E9%A6%96%E9%A0%81', N'/zh/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/新聞中心')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'1052ef4e-ffb3-5310-b5b8-8eb5a8c0b424', (SELECT id FROM clubs WHERE code = N'bw'), N'/新聞中心', N'/zh/news/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%96%B0%E8%81%9E%E4%B8%AD%E5%BF%83')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'f4090004-9839-577e-adb0-63672cbb9c8a', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%96%B0%E8%81%9E%E4%B8%AD%E5%BF%83', N'/zh/news/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/一線隊')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'01240d14-2f6f-5c86-8471-b34d7ab79ca1', (SELECT id FROM clubs WHERE code = N'bw'), N'/一線隊', N'/zh/club/first-team/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E4%B8%80%E7%B7%9A%E9%9A%8A')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'324447d1-d602-5abf-a9f1-8a410dfeefbb', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E4%B8%80%E7%B7%9A%E9%9A%8A', N'/zh/club/first-team/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/一線隊/教練團')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'9d8f4c3d-12a8-5b06-ba60-2364a8a13b52', (SELECT id FROM clubs WHERE code = N'bw'), N'/一線隊/教練團', N'/zh/club/first-team/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E4%B8%80%E7%B7%9A%E9%9A%8A/%E6%95%99%E7%B7%B4%E5%9C%98')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'afa6c439-4360-596c-8978-2bce91393aec', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E4%B8%80%E7%B7%9A%E9%9A%8A/%E6%95%99%E7%B7%B4%E5%9C%98', N'/zh/club/first-team/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/木蘭聯賽')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'f74afe5d-3096-504f-86b8-7a506073e9b5', (SELECT id FROM clubs WHERE code = N'bw'), N'/木蘭聯賽', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%9C%A8%E8%98%AD%E8%81%AF%E8%B3%BD')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'811198e8-4e8a-5682-aa46-e2d4bab8fc30', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%9C%A8%E8%98%AD%E8%81%AF%E8%B3%BD', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/賽事')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'1f05b91f-024e-529b-adad-a8616fff15a9', (SELECT id FROM clubs WHERE code = N'bw'), N'/賽事', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E8%B3%BD%E4%BA%8B')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'ba18fa98-7713-57ab-bc74-2766c5b00b26', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E8%B3%BD%E4%BA%8B', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/賽事/2025全國總統盃足球錦標賽')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'd1772860-16dd-56d8-9240-8601b1539119', (SELECT id FROM clubs WHERE code = N'bw'), N'/賽事/2025全國總統盃足球錦標賽', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E8%B3%BD%E4%BA%8B/2025%E5%85%A8%E5%9C%8B%E7%B8%BD%E7%B5%B1%E7%9B%83%E8%B6%B3%E7%90%83%E9%8C%A6%E6%A8%99%E8%B3%BD')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'9504712b-e535-57c8-8809-240f3861c587', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E8%B3%BD%E4%BA%8B/2025%E5%85%A8%E5%9C%8B%E7%B8%BD%E7%B5%B1%E7%9B%83%E8%B6%B3%E7%90%83%E9%8C%A6%E6%A8%99%E8%B3%BD', N'/zh/schedule/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/比賽球場')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'5a80331f-5fe6-57d0-a4f6-4c9331894192', (SELECT id FROM clubs WHERE code = N'bw'), N'/比賽球場', N'/zh/join/location/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%AF%94%E8%B3%BD%E7%90%83%E5%A0%B4')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'f0668008-fe6a-5b9a-aae2-251fec98bbf3', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%AF%94%E8%B3%BD%E7%90%83%E5%A0%B4', N'/zh/join/location/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/足球青年隊')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'cd9f9e3b-7730-5f85-9830-a64ed8e1d81d', (SELECT id FROM clubs WHERE code = N'bw'), N'/足球青年隊', N'/zh/academy/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E8%B6%B3%E7%90%83%E9%9D%92%E5%B9%B4%E9%9A%8A')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'ce5d3ad6-3e66-51e8-a287-22e3dff6822d', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E8%B6%B3%E7%90%83%E9%9D%92%E5%B9%B4%E9%9A%8A', N'/zh/academy/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/足球青年隊/u15女子隊')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'53d91d53-6892-5a0a-aa98-0183226ad0b2', (SELECT id FROM clubs WHERE code = N'bw'), N'/足球青年隊/u15女子隊', N'/zh/academy/teams/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E8%B6%B3%E7%90%83%E9%9D%92%E5%B9%B4%E9%9A%8A/u15%E5%A5%B3%E5%AD%90%E9%9A%8A')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'091223f9-e32b-5d2f-a4d3-6f08f3f770a7', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E8%B6%B3%E7%90%83%E9%9D%92%E5%B9%B4%E9%9A%8A/u15%E5%A5%B3%E5%AD%90%E9%9A%8A', N'/zh/academy/teams/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/足球青年隊/u12女子隊')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'b1e4d2ec-b6fa-5611-92da-3fc2fa0cd1eb', (SELECT id FROM clubs WHERE code = N'bw'), N'/足球青年隊/u12女子隊', N'/zh/academy/teams/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E8%B6%B3%E7%90%83%E9%9D%92%E5%B9%B4%E9%9A%8A/u12%E5%A5%B3%E5%AD%90%E9%9A%8A')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'4f0f952c-5e10-5307-b385-e39bb04c72a3', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E8%B6%B3%E7%90%83%E9%9D%92%E5%B9%B4%E9%9A%8A/u12%E5%A5%B3%E5%AD%90%E9%9A%8A', N'/zh/academy/teams/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/推廣活動')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'5e2117de-3723-53a6-865f-b2d2eae38fed', (SELECT id FROM clubs WHERE code = N'bw'), N'/推廣活動', N'/zh/programs/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'a214884f-f9f2-5e76-8cdb-6069d199949f', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95', N'/zh/programs/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/推廣活動/社區足球學校')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'7e354a17-9332-55f6-9375-be7fde9c7d67', (SELECT id FROM clubs WHERE code = N'bw'), N'/推廣活動/社區足球學校', N'/zh/programs/childrens-training/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E7%A4%BE%E5%8D%80%E8%B6%B3%E7%90%83%E5%AD%B8%E6%A0%A1')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'03c79fbc-b477-5567-a5ec-4d7b1efa8db9', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E7%A4%BE%E5%8D%80%E8%B6%B3%E7%90%83%E5%AD%B8%E6%A0%A1', N'/zh/programs/childrens-training/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/推廣活動/運動i台灣-運動熱區')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'104c0d60-078e-56f8-94f7-f395afe19f09', (SELECT id FROM clubs WHERE code = N'bw'), N'/推廣活動/運動i台灣-運動熱區', N'/zh/programs/childrens-training/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E9%81%8B%E5%8B%95i%E5%8F%B0%E7%81%A3-%E9%81%8B%E5%8B%95%E7%86%B1%E5%8D%80')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'175e12ff-09db-5a06-acf8-5bf913a90de6', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E9%81%8B%E5%8B%95i%E5%8F%B0%E7%81%A3-%E9%81%8B%E5%8B%95%E7%86%B1%E5%8D%80', N'/zh/programs/childrens-training/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/推廣活動/教練講習')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'0a19e49a-01dc-532a-8a02-69ca6c3d22a7', (SELECT id FROM clubs WHERE code = N'bw'), N'/推廣活動/教練講習', N'/zh/programs/specialist/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E6%95%99%E7%B7%B4%E8%AC%9B%E7%BF%92')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'4043e264-ab49-59f4-976c-cc4318d529a2', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E6%95%99%E7%B7%B4%E8%AC%9B%E7%BF%92', N'/zh/programs/specialist/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/推廣活動/台中女子足球節')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'db7c118c-ceaa-517f-93fd-b2ab7de2fe59', (SELECT id FROM clubs WHERE code = N'bw'), N'/推廣活動/台中女子足球節', N'/zh/programs/school-community/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E5%8F%B0%E4%B8%AD%E5%A5%B3%E5%AD%90%E8%B6%B3%E7%90%83%E7%AF%80')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'389759b4-6eaf-51e4-84b8-82c594f750ad', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E5%8F%B0%E4%B8%AD%E5%A5%B3%E5%AD%90%E8%B6%B3%E7%90%83%E7%AF%80', N'/zh/programs/school-community/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/推廣活動/建教合作')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'22839966-8f5a-5376-b65a-ff4fd4abf71c', (SELECT id FROM clubs WHERE code = N'bw'), N'/推廣活動/建教合作', N'/zh/programs/school-community/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E5%BB%BA%E6%95%99%E5%90%88%E4%BD%9C')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'7797a0bb-50d9-58a8-9e56-42ea063a6a6c', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E6%8E%A8%E5%BB%A3%E6%B4%BB%E5%8B%95/%E5%BB%BA%E6%95%99%E5%90%88%E4%BD%9C', N'/zh/programs/school-community/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/加油團')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'8a16be72-8c60-5b4f-b22c-6c8e2de7c8c6', (SELECT id FROM clubs WHERE code = N'bw'), N'/加油團', N'/zh/culture/fan-club/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E5%8A%A0%E6%B2%B9%E5%9C%98')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'655c568f-3369-5288-a33e-999c712ebb58', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E5%8A%A0%E6%B2%B9%E5%9C%98', N'/zh/culture/fan-club/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/關於台中藍鯨')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'6aa2fbef-58b3-5acf-84ee-cb881314ceac', (SELECT id FROM clubs WHERE code = N'bw'), N'/關於台中藍鯨', N'/zh/about/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E9%97%9C%E6%96%BC%E5%8F%B0%E4%B8%AD%E8%97%8D%E9%AF%A8')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'49454947-3b77-550a-8d6b-21b6f6012785', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E9%97%9C%E6%96%BC%E5%8F%B0%E4%B8%AD%E8%97%8D%E9%AF%A8', N'/zh/about/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/關於台中藍鯨/球隊經理寄語')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'8af4289a-31c5-566e-add9-310de89751e5', (SELECT id FROM clubs WHERE code = N'bw'), N'/關於台中藍鯨/球隊經理寄語', N'/zh/about/our-people/', 1);
GO

IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND from_path = N'/%E9%97%9C%E6%96%BC%E5%8F%B0%E4%B8%AD%E8%97%8D%E9%AF%A8/%E7%90%83%E9%9A%8A%E7%B6%93%E7%90%86%E5%AF%84%E8%AA%9E')
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES (N'395e9904-e9d9-5463-8c7c-904bed8d5bd1', (SELECT id FROM clubs WHERE code = N'bw'), N'/%E9%97%9C%E6%96%BC%E5%8F%B0%E4%B8%AD%E8%97%8D%E9%AF%A8/%E7%90%83%E9%9A%8A%E7%B6%93%E7%90%86%E5%AF%84%E8%AA%9E', N'/zh/about/our-people/', 1);
GO

-- ── 33. settings：seo.*（全站 SEO 預設）、geo.llms_*（llms.txt 五區塊）、geo.crawler_*（AI 爬蟲） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'seo.title_template';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd91cb75f-697e-54d2-ab20-a82f739dc286';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'seo.title_template', N'seo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'seo.title_template' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'{title}｜台中磐石足球俱樂部' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'seo.title_template';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'seo.title_template' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'{title} | Taichung Rock FC' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'seo.title_template';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'seo.default_description';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'221c8120-3274-5773-932b-a7f1c6c9b745';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'seo.default_description', N'seo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'seo.default_description' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'台中磐石足球俱樂部致力於透過專業模式，培育選手追求卓越，讓世界看見台灣足球。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'seo.default_description';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'seo.default_description' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Taichung Rock FC develops players through a professional model, striving for excellence so the world can see Taiwan football.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'seo.default_description';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'seo.title_template';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'adc6acc9-72fd-5ac0-b73c-914aa46f1ef0';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'seo.title_template', N'seo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'seo.title_template' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'{title}｜台中藍鯨女子足球隊' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'seo.title_template';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'seo.default_description';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'76052147-2bad-5e5d-af70-367e422fb57b';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'seo.default_description', N'seo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'seo.default_description' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，是台灣木蘭足球聯賽的球隊之一，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'seo.default_description';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'geo.llms_positioning';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c2392ccf-2fcf-5ebd-a76e-2a1f46bfbf9b';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'geo.llms_positioning', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_positioning' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'台中磐石足球俱樂部（TCRFC）位於台中，致力於透過專業模式培育選手追求卓越，讓世界看見台灣足球。品牌主張：在地扎根．放眼世界。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_positioning';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_positioning' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Taichung Rock FC (TCRFC) is a football club based in Taichung, Taiwan. LOCAL ROOTS. GLOBAL PATHWAYS.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_positioning';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'geo.llms_key_pages';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1140960a-e914-5b13-904d-7e19ac84606b';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'geo.llms_key_pages', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_key_pages' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'- [首頁](/zh/)
- [關於台中磐石](/zh/about/)
- [俱樂部](/zh/club/)
- [足球學院](/zh/academy/)
- [課程與活動](/zh/programs/)
- [女子足球](/zh/womens/)
- [新聞](/zh/news/)
- [台中磐石文化](/zh/culture/)
- [夥伴與贊助](/zh/partners/)
- [加入與聯絡](/zh/join/)
- [慈善](/zh/charity/)
- [常見問題](/zh/faq/)
- [賽事行事曆](/zh/schedule/)' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_key_pages';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_key_pages' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'- [Home](/en/)
- [About TCRFC](/en/about/)
- [Club](/en/club/)
- [Academy](/en/academy/)
- [Programs](/en/programs/)
- [Women''s Football](/en/womens/)
- [News](/en/news/)
- [Culture](/en/culture/)
- [Partners](/en/partners/)
- [Join & Contact](/en/join/)
- [Charity](/en/charity/)
- [FAQ](/en/faq/)
- [Fixtures & Calendar](/en/schedule/)' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_key_pages';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'geo.llms_facts_summary';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'52f16f78-2b1b-5d89-87cf-e16a880fef99';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'geo.llms_facts_summary', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_facts_summary' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'2024 年創立；目前參加企業甲級聯賽；主場為西屯足球場（台中市北屯區崇平路二段景谷巷 11 弄 41 號）；一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_facts_summary';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_facts_summary' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Founded in 2024. Competes in the Enterprise Premier League (企業甲級聯賽). Home ground: Xitun Football Field, Taichung. Squads: the first team plus the Academy (U15 / U14 / U12).' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_facts_summary';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'geo.llms_license';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b1841050-4a78-5fc0-97b8-27c570cf26f6';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'geo.llms_license', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_license' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'本站內容歡迎摘要引用，請註明來源為本站並附上原始網址。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_license';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_license' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Content may be summarized with attribution linking back to the source page.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_license';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'geo.llms_contact';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f3f64fa7-7ab5-5875-8623-08e73fdf1496';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'geo.llms_contact', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_contact' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'官方社群：Facebook https://www.facebook.com/TCRFC2024、Instagram https://www.instagram.com/tcr_fc_2024、YouTube https://www.youtube.com/@TCRFC-2024。事實查證或引用疑問請透過官網聯絡表單（/zh/join/contact/）；Email：contact@example.com（【測試】測試信箱，正式信箱確認後請替換）。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_contact';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_contact' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Official channels: Facebook https://www.facebook.com/TCRFC2024, Instagram https://www.instagram.com/tcr_fc_2024, YouTube https://www.youtube.com/@TCRFC-2024. For fact-checking or citation questions please use the contact form (/en/join/contact/). Email: contact@example.com (【測試】 placeholder, to be replaced).' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'geo.llms_contact';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'geo.llms_positioning';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ae2b1b0c-319a-5426-8edd-1657dcddeebc';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'geo.llms_positioning', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_positioning' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'台中藍鯨女子足球隊隸屬臺中市女子足球協會，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。口號：航向世界的藍鯨。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_positioning';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'geo.llms_key_pages';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'be96854c-64d2-5962-b12b-8dfed4002d7e';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'geo.llms_key_pages', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_key_pages' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'- [首頁](/zh/)
- [關於台中藍鯨](/zh/about/)
- [一線隊](/zh/club/)
- [青年隊](/zh/academy/)
- [推廣活動](/zh/programs/)
- [新聞](/zh/news/)
- [台中藍鯨文化](/zh/culture/)
- [夥伴與贊助](/zh/partners/)
- [加入與聯絡](/zh/join/)
- [常見問題](/zh/faq/)
- [賽事行事曆](/zh/schedule/)' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_key_pages';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_key_pages' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'- [Home](/en/)
- [About](/en/about/)
- [Club](/en/club/)
- [Youth Teams](/en/academy/)
- [Programs](/en/programs/)
- [News](/en/news/)
- [Culture](/en/culture/)
- [Partners](/en/partners/)
- [Join & Contact](/en/join/)
- [FAQ](/en/faq/)
- [Fixtures & Calendar](/en/schedule/)' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_key_pages';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'geo.llms_facts_summary';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'db117fd1-af17-5d20-be3c-63c2cca7a649';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'geo.llms_facts_summary', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_facts_summary' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'2014 年 4 月 12 日成立；所屬聯賽為台灣木蘭足球聯賽；主場為台中北屯太原足球場（現行），創隊時期主場為台中市立豐原體育場；一線隊與青年隊（U15／U12）兩個梯隊並行的發展體系。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_facts_summary';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'geo.llms_license';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8b735abd-9486-5155-af61-3fb183f57028';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'geo.llms_license', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_license' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'本站內容歡迎摘要引用，請註明來源為本站並附上原始網址。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_license';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_license' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Content may be summarized with attribution linking back to the source page.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_license';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'geo.llms_contact';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'89601208-adf5-5536-bd59-89a6e63627b0';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'geo.llms_contact', N'geo');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_contact' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'Email：fbbh2014@gmail.com；Facebook https://www.facebook.com/tbwfc；Instagram https://instagram.com/tcbw2014；YouTube https://www.youtube.com/@user-xu1wm3xx1w；LINE 官方帳號 https://lin.ee/CS65qCR。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_contact';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_contact' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'Email: fbbh2014@gmail.com; Facebook https://www.facebook.com/tbwfc; Instagram https://instagram.com/tcbw2014; YouTube https://www.youtube.com/@user-xu1wm3xx1w; LINE https://lin.ee/CS65qCR.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'geo.llms_contact';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'geo.crawler_agents';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8d13a4d0-f849-512b-acd8-612d1b647eef';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'geo.crawler_agents', N'[{"userAgent":"GPTBot","allowed":true},{"userAgent":"ClaudeBot","allowed":true},{"userAgent":"PerplexityBot","allowed":true},{"userAgent":"Google-Extended","allowed":true},{"userAgent":"CCBot","allowed":true}]', N'geo');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'geo.crawler_extra_exclude_paths';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'08cd550a-c367-5c42-9d39-feedc5e524cb';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'geo.crawler_extra_exclude_paths', N'[]', N'geo');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'geo.crawler_agents';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'05a1068a-1db2-5d8d-8381-200f8ca7b0a5';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'geo.crawler_agents', N'[{"userAgent":"GPTBot","allowed":true},{"userAgent":"ClaudeBot","allowed":true},{"userAgent":"PerplexityBot","allowed":true},{"userAgent":"Google-Extended","allowed":true},{"userAgent":"CCBot","allowed":true}]', N'geo');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'geo.crawler_extra_exclude_paths';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'42c0bd9b-27bf-5356-a450-85f7adba057b';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'geo.crawler_extra_exclude_paths', N'[]', N'geo');
  COMMIT TRANSACTION;
END
GO

-- ── 34. partners／partners_i18n：E1 夥伴（tcrfc，五種類型各一，全部【測試】） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-partner-strategic';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1a6e26cd-767a-56d5-9c08-31f533d1a9b0';
  INSERT INTO partners (id, club_id, slug, partner_type, country, website_url, start_on, end_on, show_in_footer, show_on_home, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-partner-strategic', N'策略夥伴', N'台灣', N'https://example.com/test-partner-strategic', N'2026-01-01', NULL, 1, 1, 0);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'【測試】示範策略夥伴', N'【測試】共同推動在地足球發展的策略合作。');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'Test Strategic Partner', NULL);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-partner-international';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a8dbf920-efaa-5adc-be27-67bb0a4d0d26';
  INSERT INTO partners (id, club_id, slug, partner_type, country, website_url, start_on, end_on, show_in_footer, show_on_home, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-partner-international', N'國際夥伴', N'日本', N'https://example.com/test-partner-international', N'2026-03-01', N'2027-02-28', 1, 0, 1);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'【測試】示範國際夥伴', N'【測試】國際球探與交流合作。');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'Test International Partner', NULL);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-partner-training';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'79613b5c-5e9e-57f7-bdcc-b1ff04483a3d';
  INSERT INTO partners (id, club_id, slug, partner_type, country, website_url, start_on, end_on, show_in_footer, show_on_home, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-partner-training', N'訓練夥伴', N'台灣', N'https://example.com/test-partner-training', N'2025-07-01', N'2026-06-30', 0, 0, 2);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'【測試】示範訓練夥伴', N'【測試】體能與技術訓練合作。');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'Test Training Partner', NULL);
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-partner-education';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'14e0dc4a-265f-52f3-8862-fc31a89ce6ef';
  INSERT INTO partners (id, club_id, slug, partner_type, country, website_url, start_on, end_on, show_in_footer, show_on_home, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-partner-education', N'教育夥伴', N'台灣', N'https://example.com/test-partner-education', NULL, NULL, 0, 0, 3);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'【測試】示範教育夥伴', N'【測試】校園與社區足球推廣合作。');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-partner-brand';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b1c55c6b-75ff-5e9b-80cd-feee0bf5f76e';
  INSERT INTO partners (id, club_id, slug, partner_type, country, website_url, start_on, end_on, show_in_footer, show_on_home, sort_order) VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-partner-brand', N'品牌夥伴', N'台灣', N'https://example.com/test-partner-brand', NULL, NULL, 0, 1, 4);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'【測試】示範品牌夥伴', NULL);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'Test Brand Partner', NULL);
  COMMIT TRANSACTION;
END
GO

-- ── 35. sponsors／sponsors_i18n／sponsor_packages／sponsor_package_links／sponsor_activations：E2 贊助（tcrfc，全部【測試】） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-club';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6db5a53c-8e6b-5f8f-968a-61645a310fe5';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-club', 500000, 1000000, 1, 0, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'俱樂部贊助', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'Club Sponsorship');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-academy';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2addfc11-7bff-5f1b-8723-e54c2157ba12';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-academy', 200000, 400000, 1, 1, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'學院贊助', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'Academy Sponsorship');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-team';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'02b1c8e4-08ce-5a3f-92d1-ca6ee5d1f064';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-team', 300000, 600000, 1, 2, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'球隊贊助', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'Team Sponsorship');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-camp';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b1b5c773-debe-5c59-8bcd-6ef7c384ab06';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-camp', 100000, 200000, 1, 3, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'營隊贊助', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'Camp Sponsorship');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-international';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ec12d5c0-2c9b-5e26-8607-bb797a6aaf26';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-international', NULL, NULL, 0, 4, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'國際計畫贊助', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'International Programme Sponsorship');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-comic';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'370cb455-b57b-5428-a69d-ce6ce0b78f51';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-comic', 80000, 150000, 1, 5, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'漫畫內容合作', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'Comic Content Partnership');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-merch';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ffca03f6-45cb-50e9-99f2-32c802a81189';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-merch', NULL, NULL, 0, 6, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'商品合作', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'Merchandise Partnership');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-fanclub';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c7721a65-6140-5615-a56b-38e8c2fe1f9a';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-fanclub', 50000, 100000, 1, 7, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'球迷會贊助', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'Fan Club Sponsorship');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-naming';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'904d2143-5f3d-54fd-b269-32ae4d637ad3';
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-package-naming', 1000000, 2000000, 0, 8, N'draft');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', N'場館冠名', N'【測試】方案內容說明。正式內容上線前請於後台替換。',
          N'【測試】權益一：場邊看板
【測試】權益二：官網露出
【測試】權益三：活動邀請', N'【測試】適合企業與品牌');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', N'Venue Naming Rights');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsors WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-sponsor-main';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'910e77d6-4b05-5565-80ee-c7abd4c4bff5';
  INSERT INTO sponsors (id, club_id, slug, tier, contract_start_on, contract_end_on, contact_name, contact_phone, contact_email, expiry_alert_on, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-sponsor-main', N'主贊助', N'2026-01-01', N'2027-06-30', N'【測試】聯絡人', N'04-0000-0000', N'sponsor-0@example.com', NULL, 0);
  INSERT INTO sponsors_i18n (sponsor_id, locale, name, content) VALUES (@id, N'zh-Hant', N'【測試】示範主贊助商', N'【測試】贊助內容：示範用，正式內容上線前請於後台替換。');
  INSERT INTO sponsors_i18n (sponsor_id, locale, name) VALUES (@id, N'en', N'Test Main Sponsor');
  INSERT INTO sponsor_package_links (sponsor_id, sponsor_package_id) VALUES (@id, (SELECT id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-club'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsors WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-sponsor-official';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bcb3eb45-74fe-55c4-a979-161a512435a3';
  INSERT INTO sponsors (id, club_id, slug, tier, contract_start_on, contract_end_on, contact_name, contact_phone, contact_email, expiry_alert_on, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-sponsor-official', N'官方贊助', N'2025-10-01', N'2026-12-31', N'【測試】聯絡人', N'04-0000-0000', N'sponsor-1@example.com', N'2026-09-01', 1);
  INSERT INTO sponsors_i18n (sponsor_id, locale, name, content) VALUES (@id, N'zh-Hant', N'【測試】示範官方贊助商', N'【測試】贊助內容：示範用，正式內容上線前請於後台替換。');
  INSERT INTO sponsors_i18n (sponsor_id, locale, name) VALUES (@id, N'en', N'Test Official Sponsor');
  INSERT INTO sponsor_package_links (sponsor_id, sponsor_package_id) VALUES (@id, (SELECT id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-team'));
  INSERT INTO sponsor_package_links (sponsor_id, sponsor_package_id) VALUES (@id, (SELECT id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-camp'));
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsors WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-sponsor-support';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fb2b0b69-746e-5d21-aeab-f850b16395db';
  INSERT INTO sponsors (id, club_id, slug, tier, contract_start_on, contract_end_on, contact_name, contact_phone, contact_email, expiry_alert_on, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-sponsor-support', N'支持夥伴', N'2025-01-01', N'2026-06-30', N'【測試】聯絡人', N'04-0000-0000', N'sponsor-2@example.com', N'2026-05-01', 2);
  INSERT INTO sponsors_i18n (sponsor_id, locale, name, content) VALUES (@id, N'zh-Hant', N'【測試】示範支持夥伴', N'【測試】贊助內容：示範用，正式內容上線前請於後台替換。');

  INSERT INTO sponsor_package_links (sponsor_id, sponsor_package_id) VALUES (@id, (SELECT id FROM sponsor_packages WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-package-fanclub'));
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_activations_i18n i WHERE i.locale = N'zh-Hant' AND i.title = N'【測試】示範贊助活動：開幕戰球迷日')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_activations (id, club_id, sponsor_id, happened_on, sort_order)
  VALUES (N'7f75842e-9632-5613-a4a0-9c04d0779c7f', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM sponsors WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-sponsor-main'), N'2026-09-13', 0);
  INSERT INTO sponsor_activations_i18n (sponsor_activation_id, locale, title, result_summary)
  VALUES (N'7f75842e-9632-5613-a4a0-9c04d0779c7f', N'zh-Hant', N'【測試】示範贊助活動：開幕戰球迷日', N'【測試】現場約 500 人參與，社群曝光 10 萬次（示範數字）。');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_activations_i18n i WHERE i.locale = N'zh-Hant' AND i.title = N'【測試】示範贊助活動：青訓體驗營')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_activations (id, club_id, sponsor_id, happened_on, sort_order)
  VALUES (N'9cce63cf-6fac-5136-be3e-e504c06eaf2d', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM sponsors WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-sponsor-main'), N'2026-07-20', 1);
  INSERT INTO sponsor_activations_i18n (sponsor_activation_id, locale, title, result_summary)
  VALUES (N'9cce63cf-6fac-5136-be3e-e504c06eaf2d', N'zh-Hant', N'【測試】示範贊助活動：青訓體驗營', N'【測試】共 60 位小朋友參加（示範數字）。');
  COMMIT TRANSACTION;
END
GO

-- ── 36. proposals／enquiries：E3 提案（兩份 A/B 草稿，沒有檔案）與三筆 Lead（全部【測試】） ──
IF NOT EXISTS (SELECT 1 FROM proposals p JOIN proposals_i18n i ON i.proposal_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.title = N'【測試】贊助提案簡介（A 版）')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO proposals (id, club_id, version_no, status)
  VALUES (N'be52716b-00dc-5195-be6a-02e9e2571a64', (SELECT id FROM clubs WHERE code = N'tcrfc'), 1, N'draft');
  INSERT INTO proposals_i18n (proposal_id, locale, title)
  VALUES (N'be52716b-00dc-5195-be6a-02e9e2571a64', N'zh-Hant', N'【測試】贊助提案簡介（A 版）');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM proposals p JOIN proposals_i18n i ON i.proposal_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.title = N'【測試】贊助提案簡介（B 版）')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO proposals (id, club_id, version_no, status)
  VALUES (N'd5980014-01ab-5b4e-a698-fbfb8a4db12f', (SELECT id FROM clubs WHERE code = N'tcrfc'), 2, N'draft');
  INSERT INTO proposals_i18n (proposal_id, locale, title)
  VALUES (N'd5980014-01ab-5b4e-a698-fbfb8a4db12f', N'zh-Hant', N'【測試】贊助提案簡介（B 版）');
  COMMIT TRANSACTION;
END
GO

-- ── 37. charities／charity_programs／impact_records／impact_metrics／settings：B5 慈善與社會影響（tcrfc，全部【測試】） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM charities WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-org-a';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7eaac7c5-c5c8-5594-bf68-2ff56ba67b17';
  INSERT INTO charities (id, club_id, slug, website_url, contact_name, contact_phone)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-org-a', N'https://example.com/test-org-a', N'【測試】聯絡窗口', N'04-0000-0000');
  INSERT INTO charities_i18n (charity_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'【測試】示範公益團體甲', N'【測試】這是示範用的公益團體簡介。');
  INSERT INTO charities_i18n (charity_id, locale, name) VALUES (@id, N'en', N'Test Charity A');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM charities WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-org-b';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd4aa0950-48d5-5b2e-9dc4-c79920136192';
  INSERT INTO charities (id, club_id, slug, website_url, contact_name, contact_phone)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-org-b', N'https://example.com/test-org-b', N'【測試】聯絡窗口', N'04-0000-0000');
  INSERT INTO charities_i18n (charity_id, locale, name, intro) VALUES (@id, N'zh-Hant', N'【測試】示範公益團體乙', N'【測試】另一個示範用的公益團體。');

  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-charity-program-a';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2462bed7-b0a7-5eba-a21c-8b1e35544b62';
  INSERT INTO charity_programs (id, club_id, slug, charity_id, start_on, end_on, status, sort_order, is_pinned)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-charity-program-a', (SELECT id FROM charities WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-org-a'), N'2026-03-01', NULL, N'published', 0, 1);
  INSERT INTO charity_programs_i18n (charity_program_id, locale, name, target_audience, content, donation_content)
  VALUES (@id, N'zh-Hant', N'【測試】示範慈善計畫：偏鄉足球捐贈', N'【測試】偏鄉學童', N'[{"blockType":"text","content":{"body":{"zh":"【測試】計畫緣起與內容：示範用，正式內容上線前請於後台替換。","en":null}}}]', N'【測試】足球 50 顆、訓練背心 100 件（示範數字）');
  INSERT INTO charity_programs_i18n (charity_program_id, locale, name) VALUES (@id, N'en', N'Test Charity Program A');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-charity-program-b';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9c6435d5-0f80-5eca-8b91-36c95aeca23c';
  INSERT INTO charity_programs (id, club_id, slug, charity_id, start_on, end_on, status, sort_order, is_pinned)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-charity-program-b', (SELECT id FROM charities WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-org-b'), N'2025-05-01', N'2025-12-31', N'published', 1, 0);
  INSERT INTO charity_programs_i18n (charity_program_id, locale, name, target_audience, content, donation_content)
  VALUES (@id, N'zh-Hant', N'【測試】示範慈善計畫：公益義賽', N'【測試】偏鄉學童', N'[{"blockType":"text","content":{"body":{"zh":"【測試】計畫緣起與內容：示範用，正式內容上線前請於後台替換。","en":null}}}]', N'【測試】足球 50 顆、訓練背心 100 件（示範數字）');

  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-charity-program-c';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'49a2ff2b-d122-5f99-81f8-dd489c3bb64a';
  INSERT INTO charity_programs (id, club_id, slug, charity_id, start_on, end_on, status, sort_order, is_pinned)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-charity-program-c', (SELECT id FROM charities WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-org-a'), N'2026-10-01', NULL, N'draft', 2, 0);
  INSERT INTO charity_programs_i18n (charity_program_id, locale, name, target_audience, content, donation_content)
  VALUES (@id, N'zh-Hant', N'【測試】示範慈善計畫（草稿）', N'【測試】偏鄉學童', N'[{"blockType":"text","content":{"body":{"zh":"【測試】計畫緣起與內容：示範用，正式內容上線前請於後台替換。","en":null}}}]', N'【測試】足球 50 顆、訓練背心 100 件（示範數字）');

  COMMIT TRANSACTION;
END
GO

INSERT INTO charity_program_partners (charity_program_id, partner_id)
SELECT p.id, x.id FROM charity_programs p CROSS JOIN partners x
WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND p.slug = N'test-charity-program-a' AND x.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND x.slug = N'test-partner-strategic'
  AND NOT EXISTS (SELECT 1 FROM charity_program_partners l WHERE l.charity_program_id = p.id AND l.partner_id = x.id);
INSERT INTO charity_program_sponsors (charity_program_id, sponsor_id)
SELECT p.id, x.id FROM charity_programs p CROSS JOIN sponsors x
WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND p.slug = N'test-charity-program-a' AND x.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND x.slug = N'test-sponsor-main'
  AND NOT EXISTS (SELECT 1 FROM charity_program_sponsors l WHERE l.charity_program_id = p.id AND l.sponsor_id = x.id);
GO

IF NOT EXISTS (SELECT 1 FROM impact_records_i18n WHERE locale = N'zh-Hant' AND donation_content = N'【測試】足球 50 顆、訓練背心 100 件（示範數字）')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO impact_records (id, club_id, charity_program_id, charity_id, happened_on, sort_order)
  VALUES (N'09cf06ae-5f10-5c28-af48-0f5811e6c7f5', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM charity_programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-charity-program-a'),
          (SELECT id FROM charities WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-org-a'), N'2026-04-10', 0);
  INSERT INTO impact_records_i18n (impact_record_id, locale, donation_content, location, brief_description)
  VALUES (N'09cf06ae-5f10-5c28-af48-0f5811e6c7f5', N'zh-Hant', N'【測試】足球 50 顆、訓練背心 100 件（示範數字）', N'【測試】示範地點：南投縣', N'【測試】示範簡述。');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM impact_records_i18n WHERE locale = N'zh-Hant' AND donation_content = N'【測試】獎助學金 5 名（示範數字）')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO impact_records (id, club_id, charity_program_id, charity_id, happened_on, sort_order)
  VALUES (N'8d8c5feb-364b-5ba3-b51b-1b182bd6f7ac', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM charity_programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-charity-program-b'),
          (SELECT id FROM charities WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-org-b'), N'2025-11-22', 0);
  INSERT INTO impact_records_i18n (impact_record_id, locale, donation_content, location, brief_description)
  VALUES (N'8d8c5feb-364b-5ba3-b51b-1b182bd6f7ac', N'zh-Hant', N'【測試】獎助學金 5 名（示範數字）', N'【測試】示範地點：台中市', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM impact_metrics_i18n WHERE locale = N'zh-Hant' AND name = N'【測試】合作公益團體數')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO impact_metrics (id, club_id, charity_program_id, metric_key, metric_value, is_public, sort_order)
  VALUES (N'e5782434-9440-58b2-a9dd-5a837210a4e4', (SELECT id FROM clubs WHERE code = N'tcrfc'), NULL, N'seed-groups', 2, 1, 0);
  INSERT INTO impact_metrics_i18n (impact_metric_id, locale, name, unit) VALUES (N'e5782434-9440-58b2-a9dd-5a837210a4e4', N'zh-Hant', N'【測試】合作公益團體數', N'個');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM impact_metrics_i18n WHERE locale = N'zh-Hant' AND name = N'【測試】累計捐助項次')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO impact_metrics (id, club_id, charity_program_id, metric_key, metric_value, is_public, sort_order)
  VALUES (N'15b84666-c590-5021-9f06-33d082ffee8a', (SELECT id FROM clubs WHERE code = N'tcrfc'), NULL, N'seed-items', 155, 1, 0);
  INSERT INTO impact_metrics_i18n (impact_metric_id, locale, name, unit) VALUES (N'15b84666-c590-5021-9f06-33d082ffee8a', N'zh-Hant', N'【測試】累計捐助項次', N'項');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM impact_metrics_i18n WHERE locale = N'zh-Hant' AND name = N'【測試】累計捐助金額（不公開）')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO impact_metrics (id, club_id, charity_program_id, metric_key, metric_value, is_public, sort_order)
  VALUES (N'f3c6657b-c052-5c08-966b-55b16ca8e7a4', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM charity_programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-charity-program-a'), N'seed-amount', 123456, 0, 0);
  INSERT INTO impact_metrics_i18n (impact_metric_id, locale, name, unit) VALUES (N'f3c6657b-c052-5c08-966b-55b16ca8e7a4', N'zh-Hant', N'【測試】累計捐助金額（不公開）', N'元');
  COMMIT TRANSACTION;
END
GO

-- ── 37b. settings：charity.*（捐款導流與參與方式）。導流網址是 example.com 測試值；文案已依規劃書 §3.11 點明收受者 ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'charity.donation_url';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4b92bfd3-0635-5ee5-97cb-1e7abf8566b8';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'charity.donation_url', N'https://$(CHARITY_DOMAIN)/', N'charity');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'charity.donation_cta';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0fcb458b-8f05-5345-b34c-242dba1394f0';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'charity.donation_cta', N'charity');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.donation_cta' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】球迷捐款（捐款由台灣足球策略發展協會收受）' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.donation_cta';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.donation_cta' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'【測試】Fan donations (received by the Taiwan Football Strategic Development Association)' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.donation_cta';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'charity.fan_cta';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'30fcf3ad-856f-5a04-91ca-ed0128ec4c8f';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'charity.fan_cta', N'charity');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.fan_cta' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】球迷捐款' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.fan_cta';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.fan_cta' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'【測試】Donate' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.fan_cta';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'charity.corporate_cta';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fdd280a8-7bdb-5951-8cad-8cf7a0a6d594';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'charity.corporate_cta', N'charity');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.corporate_cta' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】企業合作公益專案' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.corporate_cta';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.corporate_cta' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'【測試】Corporate partnership' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'charity.corporate_cta';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'charity.corporate_url';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ee9fa209-3f55-5b5d-bb31-37c745f3cf05';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'charity.corporate_url', N'/zh/join/', N'charity');
  COMMIT TRANSACTION;
END
GO

-- ── 38. press_resources：B6 媒體專區（三類各一，全部 draft＋佔位檔案鍵；【測試】） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM press_resources WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-press-release';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6b43e460-8675-5150-9c76-ffb806879801';
  INSERT INTO press_resources (id, club_id, slug, resource_type, file_key, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-press-release', N'press_release', N'seed-placeholder/no-file', 0, N'draft');
  INSERT INTO press_resources_i18n (press_resource_id, locale, title, description)
  VALUES (@id, N'zh-Hant', N'【測試】示範新聞稿', N'【測試】示範說明。這筆沒有真實檔案（佔位），請於後台上傳後再改為顯示。');
  INSERT INTO press_resources_i18n (press_resource_id, locale, title) VALUES (@id, N'en', N'Test Press Release');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM press_resources WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-brand-kit';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'aa4a5b79-0930-545d-b72a-8f16bf238593';
  INSERT INTO press_resources (id, club_id, slug, resource_type, file_key, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-brand-kit', N'brand_kit', N'seed-placeholder/no-file', 1, N'draft');
  INSERT INTO press_resources_i18n (press_resource_id, locale, title, description)
  VALUES (@id, N'zh-Hant', N'【測試】示範品牌識別包', N'【測試】示範說明。這筆沒有真實檔案（佔位），請於後台上傳後再改為顯示。');
  INSERT INTO press_resources_i18n (press_resource_id, locale, title) VALUES (@id, N'en', N'Test Brand Kit');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM press_resources WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-hires-image';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3127b299-ef8f-547e-9d23-06e413ec1add';
  INSERT INTO press_resources (id, club_id, slug, resource_type, file_key, sort_order, status)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-hires-image', N'hires_image', N'seed-placeholder/no-file', 2, N'draft');
  INSERT INTO press_resources_i18n (press_resource_id, locale, title, description)
  VALUES (@id, N'zh-Hant', N'【測試】示範高解析圖', N'【測試】示範說明。這筆沒有真實檔案（佔位），請於後台上傳後再改為顯示。');

  COMMIT TRANSACTION;
END
GO

-- ── 39. achievements：C5 榮譽（tcrfc 一線隊，三筆【測試】） ──
IF NOT EXISTS (SELECT 1 FROM achievements a JOIN achievements_i18n i ON i.achievement_id = a.id AND i.locale = N'zh-Hant'
               WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.competition_name = N'【測試】示範盃賽')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO achievements (id, club_id, season_id, team_id, year)
  VALUES (N'2353250f-271c-53ef-b407-c291d1035640', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM teams WHERE code = N'D1'), 2026);
  INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing)
  VALUES (N'2353250f-271c-53ef-b407-c291d1035640', N'zh-Hant', N'【測試】示範盃賽', N'冠軍');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM achievements a JOIN achievements_i18n i ON i.achievement_id = a.id AND i.locale = N'zh-Hant'
               WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.competition_name = N'【測試】示範聯賽')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO achievements (id, club_id, season_id, team_id, year)
  VALUES (N'37d6ca91-4426-5140-b5e8-137028e4b5cc', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM teams WHERE code = N'D1'), 2025);
  INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing)
  VALUES (N'37d6ca91-4426-5140-b5e8-137028e4b5cc', N'zh-Hant', N'【測試】示範聯賽', N'第三名');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM achievements a JOIN achievements_i18n i ON i.achievement_id = a.id AND i.locale = N'zh-Hant'
               WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.competition_name = N'【測試】示範友誼賽')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO achievements (id, club_id, season_id, team_id, year)
  VALUES (N'453deaab-0847-58c7-92d7-28329af43b75', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), (SELECT id FROM teams WHERE code = N'D1'), 2024);
  INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing)
  VALUES (N'453deaab-0847-58c7-92d7-28329af43b75', N'zh-Hant', N'【測試】示範友誼賽', N'亞軍');
  COMMIT TRANSACTION;
END
GO

-- ── 42. trials／trials_i18n／registrations：P4 試訓場次與報名（tcrfc 一線隊、bw 青年隊，全部【測試】） ──
IF NOT EXISTS (SELECT 1 FROM trials_i18n WHERE audience = N'【測試】一線隊公開試訓：18 歲以上，具比賽經驗者')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO trials (id, club_id, team_id, venue_id, trial_on, capacity, deadline_on, sync_to_calendar, status)
  VALUES (N'ee14ce21-2bb5-5bbd-88dc-83f03ab02900', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-11-14', 20, N'2026-11-07', 0, N'開放');
  INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (N'ee14ce21-2bb5-5bbd-88dc-83f03ab02900', N'zh-Hant', N'【測試】一線隊公開試訓：18 歲以上，具比賽經驗者');
  INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (N'ee14ce21-2bb5-5bbd-88dc-83f03ab02900', N'en', N'Test open trial for the first team: age 18+, match experience required');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM trials_i18n WHERE audience = N'【測試】一線隊夏季試訓（已結束）')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO trials (id, club_id, team_id, venue_id, trial_on, capacity, deadline_on, sync_to_calendar, status)
  VALUES (N'6f40f969-a4a3-523d-b827-6cb124101c12', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%西屯%'), N'2026-08-16', 15, N'2026-08-09', 0, N'已結束');
  INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (N'6f40f969-a4a3-523d-b827-6cb124101c12', N'zh-Hant', N'【測試】一線隊夏季試訓（已結束）');
  INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (N'6f40f969-a4a3-523d-b827-6cb124101c12', N'en', N'Test summer trial (closed)');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM trials_i18n WHERE audience = N'【測試】青年隊 U15 試訓：2011–2012 年出生')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO trials (id, club_id, team_id, venue_id, trial_on, capacity, deadline_on, sync_to_calendar, status)
  VALUES (N'760710ce-e3f1-59f8-959a-547da41ebadc', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW-U15'), (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n vi ON vi.venue_id = v.id WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%豐原%'), N'2026-12-05', 12, N'2026-11-28', 0, N'開放');
  INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (N'760710ce-e3f1-59f8-959a-547da41ebadc', N'zh-Hant', N'【測試】青年隊 U15 試訓：2011–2012 年出生');
  INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (N'760710ce-e3f1-59f8-959a-547da41ebadc', N'en', N'Test U15 youth trial: born 2011–2012');
  COMMIT TRANSACTION;
END
GO

-- ── 43. membership_plans／membership_plans_i18n：K2 會籍方案（tcrfc 2026-27 單人＋家庭、bw 2025 單人，【測試】價格） ──
IF NOT EXISTS (SELECT 1 FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_plans (id, club_id, season_id, code, fee, card_quota, jersey_quota, sort_order, starts_on, ends_on, status)
  VALUES (N'17b2fc21-71fd-541c-a642-b29ce43a5248', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), N'single', 1200, 1, 1, 0, N'2026-09-13', N'2027-05-02', N'published');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note, mid_season_rule) VALUES (N'17b2fc21-71fd-541c-a642-b29ce43a5248', N'zh-Hant', N'【測試】球迷會員（單人）', N'【測試】含會員卡一張、入會球衣一件。', N'【測試】季中入會照比例計價');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note) VALUES (N'17b2fc21-71fd-541c-a642-b29ce43a5248', N'en', N'Fan Club Member (Single)', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'family')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_plans (id, club_id, season_id, code, fee, card_quota, jersey_quota, sort_order, starts_on, ends_on, status)
  VALUES (N'3a03b84d-5772-54cf-ba29-a356f1c47180', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27'), N'family', 3000, 3, 3, 1, N'2026-09-13', N'2027-05-02', N'published');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note, mid_season_rule) VALUES (N'3a03b84d-5772-54cf-ba29-a356f1c47180', N'zh-Hant', N'【測試】球迷會員（家庭）', N'【測試】1 位成人＋2 位小童，含會員卡三張、球衣三件。', N'【測試】季中入會不折價');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note) VALUES (N'3a03b84d-5772-54cf-ba29-a356f1c47180', N'en', N'Fan Club Member (Family)', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025') AND code = N'single')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_plans (id, club_id, season_id, code, fee, card_quota, jersey_quota, sort_order, starts_on, ends_on, status)
  VALUES (N'4002c0fb-ee7e-5122-9dca-4a5c1d7d2cee', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND code = N'2025'), N'single', 800, 1, 1, 0, N'2025-04-23', N'2025-06-15', N'published');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note, mid_season_rule) VALUES (N'4002c0fb-ee7e-5122-9dca-4a5c1d7d2cee', N'zh-Hant', N'【測試】藍鯨球迷會員（單人）', N'【測試】含會員卡一張、入會球衣一件。', N'【測試】季中入會照比例計價');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note) VALUES (N'4002c0fb-ee7e-5122-9dca-4a5c1d7d2cee', N'en', N'Blue Whale Fan Club (Single)', NULL);
  COMMIT TRANSACTION;
END
GO

-- ── 45. settings：member.no_prefix／no_digits（K2 會員編號規則）與 calendar.*（L3 顯示設定） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'member.no_prefix';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'023154b9-6815-5465-88bd-0865fc3d04b9';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'member.no_prefix', N'M', N'member');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'member.no_digits';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4a95ce6e-10f7-51ce-bf87-7ae88cd47da8';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'member.no_digits', N'6', N'member');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'calendar.default_view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'56f25f0e-02f0-50aa-8344-6cdef3f66c9a';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'calendar.default_view', N'list', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'calendar.default_range';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'24c44a13-3aa8-5952-8fd0-a970b4a8a503';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'calendar.default_range', N'upcoming', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'calendar.sync_trials';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'972d0f2d-719a-53d4-b099-04f18e9a425f';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'calendar.sync_trials', N'false', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'member.no_prefix';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'5a343584-1fab-55b2-b62c-9443847d1acc';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'member.no_prefix', N'M', N'member');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'member.no_digits';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'a83801f9-561a-5a7a-8e4a-bd93a0d09d65';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'member.no_digits', N'6', N'member');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'calendar.default_view';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7e204394-1d31-5976-ade8-14b73d2f1ad9';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'calendar.default_view', N'list', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'calendar.default_range';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f77b84e2-271c-5b20-8cf3-7e3e74615f4c';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'calendar.default_range', N'upcoming', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'calendar.sync_trials';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ffd4331a-b574-502d-a821-0d7d3170981d';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'calendar.sync_trials', N'false', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'calendar.default_team';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'9605c5f0-6e2e-5392-9547-d6c6124cd6dc';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'calendar.default_team', N'D1', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'calendar.embed.first_team_code';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c513c0ca-9ac1-5f7a-ac0e-4497a71eb56f';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'calendar.embed.first_team_code', N'D1', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'calendar.embed.home_teams';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'1263bc29-d54a-5fb8-bbda-6e292bb3eb4b';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'calendar.embed.home_teams', N'["D1"]', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'calendar.default_team';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'cb34217a-c9cf-5950-933f-622939b3921f';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'calendar.default_team', N'BW1', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'calendar.embed.first_team_code';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'0147d442-44ef-5acf-867c-8909c37ee041';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'calendar.embed.first_team_code', N'BW1', N'calendar');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'calendar.embed.home_teams';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c4d6bd71-8280-576f-990e-98f314ac445f';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'calendar.embed.home_teams', N'["BW1"]', N'calendar');
  COMMIT TRANSACTION;
END
GO

-- ── 46. calendar_team_settings：L3 隊別分類顯示設定（一線隊顯示名稱與代表色；藍鯨青年隊 U12 示範不公開） ──
IF NOT EXISTS (SELECT 1 FROM calendar_team_settings WHERE team_id = (SELECT id FROM teams WHERE code = N'D1'))
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO calendar_team_settings (id, club_id, team_id, colour, sort_order, is_public)
  VALUES (N'14926c54-7e97-5727-9037-ecbf709fdd2c', (SELECT id FROM clubs WHERE code = N'tcrfc'), (SELECT id FROM teams WHERE code = N'D1'), N'#0B3D91', 0, 1);
  INSERT INTO calendar_team_settings_i18n (calendar_team_setting_id, locale, display_name) VALUES (N'14926c54-7e97-5727-9037-ecbf709fdd2c', N'zh-Hant', N'一線隊');
  INSERT INTO calendar_team_settings_i18n (calendar_team_setting_id, locale, display_name) VALUES (N'14926c54-7e97-5727-9037-ecbf709fdd2c', N'en', N'First Team');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM calendar_team_settings WHERE team_id = (SELECT id FROM teams WHERE code = N'BW1'))
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO calendar_team_settings (id, club_id, team_id, colour, sort_order, is_public)
  VALUES (N'9615ff98-8711-5293-9d11-cf453fd7af29', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW1'), N'#2196D5', 0, 1);
  INSERT INTO calendar_team_settings_i18n (calendar_team_setting_id, locale, display_name) VALUES (N'9615ff98-8711-5293-9d11-cf453fd7af29', N'zh-Hant', N'藍鯨一線隊');
  INSERT INTO calendar_team_settings_i18n (calendar_team_setting_id, locale, display_name) VALUES (N'9615ff98-8711-5293-9d11-cf453fd7af29', N'en', N'Blue Whale First Team');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM calendar_team_settings WHERE team_id = (SELECT id FROM teams WHERE code = N'BW-U12'))
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO calendar_team_settings (id, club_id, team_id, colour, sort_order, is_public)
  VALUES (N'9e124c0a-b4d9-5f35-8888-b5b558246957', (SELECT id FROM clubs WHERE code = N'bw'), (SELECT id FROM teams WHERE code = N'BW-U12'), NULL, 3, 0);
  INSERT INTO calendar_team_settings_i18n (calendar_team_setting_id, locale, display_name) VALUES (N'9e124c0a-b4d9-5f35-8888-b5b558246957', N'zh-Hant', N'U12 青年隊');
  INSERT INTO calendar_team_settings_i18n (calendar_team_setting_id, locale, display_name) VALUES (N'9e124c0a-b4d9-5f35-8888-b5b558246957', N'en', N'U12 Youth');
  COMMIT TRANSACTION;
END
GO

-- ── 47. partner_stores／partner_stores_i18n：K4 特約店家（tcrfc 4 家、兩隊共同 1 家、bw 1 家，全部【測試】，座標為台中市區近似值） ──
IF NOT EXISTS (SELECT 1 FROM partner_stores WHERE slug = N'test-store-cafe')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO partner_stores (id, club_id, slug, category, region, address, lat, lng, phone, website_url, map_url, applicable_tier, start_on, sort_order, status)
  VALUES (N'1625d957-ab43-5bfc-a819-823124b43381', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-store-cafe', N'餐飲', N'台中市西屯區', N'【測試】台中市西屯區測試路 10 號', 24.181, 120.606, N'04-0000-0000', N'https://example.com/test-store-cafe',
          N'https://maps.example.com/test-store-cafe', N'all', N'2026-01-01', 0, N'published');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'1625d957-ab43-5bfc-a819-823124b43381', N'zh-Hant', N'【測試】示範咖啡館', NULL, N'【測試】出示會員卡飲品九折');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'1625d957-ab43-5bfc-a819-823124b43381', N'en', N'Test Cafe', N'No. 10 Test Rd., Xitun Dist., Taichung', N'Test: 10% off drinks with member card');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM partner_stores WHERE slug = N'test-store-sports')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO partner_stores (id, club_id, slug, category, region, address, lat, lng, phone, website_url, map_url, applicable_tier, start_on, sort_order, status)
  VALUES (N'a9ae9a9e-2eb9-52cc-9a28-a3f320ed7c1c', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-store-sports', N'運動用品', N'台中市北屯區', N'【測試】台中市北屯區測試路 20 號', 24.183, 120.708, N'04-0000-0000', N'https://example.com/test-store-sports',
          N'https://maps.example.com/test-store-sports', N'fan_club', N'2026-01-01', 1, N'published');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'a9ae9a9e-2eb9-52cc-9a28-a3f320ed7c1c', N'zh-Hant', N'【測試】示範運動用品店', NULL, N'【測試】付費會員全店九五折');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'a9ae9a9e-2eb9-52cc-9a28-a3f320ed7c1c', N'en', N'Test Sports Shop', NULL, N'Test: 5% off for fan club members');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM partner_stores WHERE slug = N'test-store-gym')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO partner_stores (id, club_id, slug, category, region, address, lat, lng, phone, website_url, map_url, applicable_tier, start_on, sort_order, status)
  VALUES (N'6c5fc7c1-566f-57a6-a126-0dd8f7e08012', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-store-gym', N'健身', N'台中市南屯區', N'【測試】台中市南屯區測試路 30 號', NULL, NULL, N'04-0000-0000', N'https://example.com/test-store-gym',
          N'https://maps.example.com/test-store-gym', N'all', N'2026-01-01', 2, N'draft');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'6c5fc7c1-566f-57a6-a126-0dd8f7e08012', N'zh-Hant', N'【測試】示範健身房', NULL, N'【測試】體驗課程一堂免費（草稿，尚未上架）');
  
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM partner_stores WHERE slug = N'test-store-food')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO partner_stores (id, club_id, slug, category, region, address, lat, lng, phone, website_url, map_url, applicable_tier, start_on, sort_order, status)
  VALUES (N'f92afb72-2841-5261-b8bf-49c3e4712b7a', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-store-food', N'餐飲', N'台中市西區', N'【測試】台中市西區測試路 40 號', 24.14, 120.664, N'04-0000-0000', N'https://example.com/test-store-food',
          N'https://maps.example.com/test-store-food', N'all', N'2026-01-01', 3, N'published');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'f92afb72-2841-5261-b8bf-49c3e4712b7a', N'zh-Hant', N'【測試】示範小吃店', NULL, N'【測試】招牌小吃加購優惠');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'f92afb72-2841-5261-b8bf-49c3e4712b7a', N'en', N'Test Snack Shop', NULL, N'Test: snack combo discount');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM partner_stores WHERE slug = N'test-store-shared')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO partner_stores (id, club_id, slug, category, region, address, lat, lng, phone, website_url, map_url, applicable_tier, start_on, sort_order, status)
  VALUES (N'e74499ce-3629-5225-b1fa-477fc718dde2', NULL, N'test-store-shared', N'生活', N'台中市', N'【測試】台中市測試路 50 號', 24.15, 120.68, N'04-0000-0000', N'https://example.com/test-store-shared',
          N'https://maps.example.com/test-store-shared', N'all', N'2026-01-01', 4, N'published');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'e74499ce-3629-5225-b1fa-477fc718dde2', N'zh-Hant', N'【測試】兩隊共同特約店家', NULL, N'【測試】兩隊會員皆適用的優惠');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'e74499ce-3629-5225-b1fa-477fc718dde2', N'en', N'Test Shared Partner Store', N'No. 50 Test Rd., Taichung', N'Test: offer valid for both clubs'' members');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM partner_stores WHERE slug = N'test-store-bw')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO partner_stores (id, club_id, slug, category, region, address, lat, lng, phone, website_url, map_url, applicable_tier, start_on, sort_order, status)
  VALUES (N'c0604c9e-bfb3-524a-9048-37e7f929d3cc', (SELECT id FROM clubs WHERE code = N'bw'), N'test-store-bw', N'餐飲', N'台中市豐原區', N'【測試】台中市豐原區測試路 60 號', 24.252, 120.722, N'04-0000-0000', N'https://example.com/test-store-bw',
          N'https://maps.example.com/test-store-bw', N'all', N'2026-01-01', 5, N'published');
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (N'c0604c9e-bfb3-524a-9048-37e7f929d3cc', N'zh-Hant', N'【測試】藍鯨示範店家', NULL, N'【測試】藍鯨會員專屬優惠');
  
  COMMIT TRANSACTION;
END
GO

-- ── 48. membership_benefits／membership_benefits_i18n：K4 權益對照表（掛在 tcrfc 單人方案，六條【測試】條目，分組涵蓋四類） ──
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single') AND sort_order = 0)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_group, sort_order, status)
  VALUES (N'fad9b2d3-ab65-5ffd-8d02-fcea2a3879c4', (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single'), N'member_card', 0, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'fad9b2d3-ab65-5ffd-8d02-fcea2a3879c4', N'zh-Hant', N'電子會員卡', N'【測試】手機出示即可', N'會員卡', N'✓', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'fad9b2d3-ab65-5ffd-8d02-fcea2a3879c4', N'en', N'Digital member card', NULL, N'Member card', N'✓', N'✓');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single') AND sort_order = 1)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_group, sort_order, status)
  VALUES (N'a73e1cfd-53db-5ab0-b362-31591babc5ef', (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single'), N'store_discount', 1, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'a73e1cfd-53db-5ab0-b362-31591babc5ef', N'zh-Hant', N'全會員適用的特約店家折扣', N'【測試】標示「全會員適用」的店家', N'店家折扣', N'✓', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'a73e1cfd-53db-5ab0-b362-31591babc5ef', N'en', N'Partner store discounts (all members)', NULL, N'Store discounts', N'✓', N'✓');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single') AND sort_order = 2)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_group, sort_order, status)
  VALUES (N'cee7d3dd-e99d-5cfa-9a41-8145e27f131a', (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single'), N'store_discount', 2, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'cee7d3dd-e99d-5cfa-9a41-8145e27f131a', N'zh-Hant', N'限付費會員的特約店家折扣', N'【測試】標示「限付費」的店家', N'店家折扣', N'✗', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'cee7d3dd-e99d-5cfa-9a41-8145e27f131a', N'en', N'Partner store discounts (fan club only)', NULL, N'Store discounts', N'✗', N'✓');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single') AND sort_order = 3)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_group, sort_order, status)
  VALUES (N'f287b0e7-0227-5abc-af58-9d2019b12538', (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single'), N'jersey', 3, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'f287b0e7-0227-5abc-af58-9d2019b12538', N'zh-Hant', N'入會球衣', N'【測試】依方案含球衣件數', N'球衣', N'✗', N'一件');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'f287b0e7-0227-5abc-af58-9d2019b12538', N'en', N'Membership jersey', NULL, N'Jersey', N'✗', N'一件');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single') AND sort_order = 4)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_group, sort_order, status)
  VALUES (N'7673129f-90c6-509e-abab-60bde8e2f783', (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single'), N'event', 4, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'7673129f-90c6-509e-abab-60bde8e2f783', N'zh-Hant', N'球迷活動優先報名', N'【測試】球迷見面會等活動', N'活動', N'✗', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'7673129f-90c6-509e-abab-60bde8e2f783', N'en', N'Priority sign-up for fan events', NULL, N'Events', N'✗', N'✓');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single') AND sort_order = 5)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_group, sort_order, status)
  VALUES (N'18853fa3-3204-516f-9348-7e87423b1b4e', (SELECT id FROM membership_plans WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND season_id = (SELECT id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'2026-27') AND code = N'single'), N'event', 5, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'18853fa3-3204-516f-9348-7e87423b1b4e', N'zh-Hant', N'球迷會員抽獎資格', N'【測試】會籍有效期間自動具備', N'活動', N'✗', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES (N'18853fa3-3204-516f-9348-7e87423b1b4e', N'en', N'Fan club prize draw eligibility', NULL, N'Events', N'✗', N'✓');
  COMMIT TRANSACTION;
END
GO

-- ── 49. F1 漫畫（tcrfc）：企劃設定 settings＋3 個角色＋3 集草稿（藍鯨不設漫畫，不種） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'comic.about_title';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'720d0fd7-a573-5438-b023-e155e360d2ff';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'comic.about_title', N'comic');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'comic.about_title' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】漫畫世界觀' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'comic.about_title';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'comic.about_title' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Comic universe' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'comic.about_title';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'comic.about_body';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd7b40121-2fe5-5d6c-8dd2-06ba70b78f40';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'comic.about_body', N'comic');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'comic.about_body' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】這是測試用的漫畫世界觀說明，正式內容上線前請於後台替換。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'comic.about_body';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'comic.about_body' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Placeholder universe description for the comic.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'comic.about_body';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM comic_characters_i18n WHERE comic_character_id = N'06a92337-a596-55fc-ac33-1000f14ba458')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO comic_characters (id, club_id, sort_order) VALUES (N'06a92337-a596-55fc-ac33-1000f14ba458', (SELECT id FROM clubs WHERE code = N'tcrfc'), 0);
  INSERT INTO comic_characters_i18n (comic_character_id, locale, name, description) VALUES (N'06a92337-a596-55fc-ac33-1000f14ba458', N'zh-Hant', N'【測試】角色甲', N'【測試】角色設定占位文字。');
  INSERT INTO comic_characters_i18n (comic_character_id, locale, name, description) VALUES (N'06a92337-a596-55fc-ac33-1000f14ba458', N'en', N'Test Character A', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM comic_characters_i18n WHERE comic_character_id = N'1450a5b6-15a7-5d7a-b0d1-fd2011500f3a')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO comic_characters (id, club_id, sort_order) VALUES (N'1450a5b6-15a7-5d7a-b0d1-fd2011500f3a', (SELECT id FROM clubs WHERE code = N'tcrfc'), 1);
  INSERT INTO comic_characters_i18n (comic_character_id, locale, name, description) VALUES (N'1450a5b6-15a7-5d7a-b0d1-fd2011500f3a', N'zh-Hant', N'【測試】角色乙', N'【測試】角色設定占位文字。');
  INSERT INTO comic_characters_i18n (comic_character_id, locale, name, description) VALUES (N'1450a5b6-15a7-5d7a-b0d1-fd2011500f3a', N'en', N'Test Character B', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM comic_characters_i18n WHERE comic_character_id = N'cf4b4574-e36c-522b-a12f-f5027dfc0b6b')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO comic_characters (id, club_id, sort_order) VALUES (N'cf4b4574-e36c-522b-a12f-f5027dfc0b6b', (SELECT id FROM clubs WHERE code = N'tcrfc'), 2);
  INSERT INTO comic_characters_i18n (comic_character_id, locale, name, description) VALUES (N'cf4b4574-e36c-522b-a12f-f5027dfc0b6b', N'zh-Hant', N'【測試】角色丙', N'【測試】角色設定占位文字。');
  INSERT INTO comic_characters_i18n (comic_character_id, locale, name, description) VALUES (N'cf4b4574-e36c-522b-a12f-f5027dfc0b6b', N'en', N'Test Character C', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM comic_episodes WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND episode_no = 1)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO comic_episodes (id, club_id, episode_no, status, is_latest, view_count) VALUES (N'09e46d48-41d3-5957-b847-ae560969db48', (SELECT id FROM clubs WHERE code = N'tcrfc'), 1, N'draft', 0, 0);
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES (N'09e46d48-41d3-5957-b847-ae560969db48', N'zh-Hant', N'【測試】第 1 集');
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES (N'09e46d48-41d3-5957-b847-ae560969db48', N'en', N'[Test] Episode 1');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM comic_episodes WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND episode_no = 2)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO comic_episodes (id, club_id, episode_no, status, is_latest, view_count) VALUES (N'faf51d1f-0aa5-5bbd-b203-4ab34f17f17a', (SELECT id FROM clubs WHERE code = N'tcrfc'), 2, N'draft', 0, 0);
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES (N'faf51d1f-0aa5-5bbd-b203-4ab34f17f17a', N'zh-Hant', N'【測試】第 2 集');
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES (N'faf51d1f-0aa5-5bbd-b203-4ab34f17f17a', N'en', N'[Test] Episode 2');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM comic_episodes WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND episode_no = 3)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO comic_episodes (id, club_id, episode_no, status, is_latest, view_count) VALUES (N'bc819229-12a5-526b-a88c-e9f1c9471abd', (SELECT id FROM clubs WHERE code = N'tcrfc'), 3, N'draft', 0, 0);
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES (N'bc819229-12a5-526b-a88c-e9f1c9471abd', N'zh-Hant', N'【測試】第 3 集');
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES (N'bc819229-12a5-526b-a88c-e9f1c9471abd', N'en', N'[Test] Episode 3');
  COMMIT TRANSACTION;
END
GO

-- ── 50. F2 球迷會活動：tcrfc 3 場（付費限定／公開／草稿）＋報名、bw 1 場 ──
IF NOT EXISTS (SELECT 1 FROM fan_events WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-fan-meet')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO fan_events (id, club_id, slug, starts_at, ends_at, registration_deadline_at, capacity, is_paid_members_only, status)
  VALUES (N'423e5f41-a890-5d46-b02b-efcfac8cfcbd', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-fan-meet', DATEADD(day, 14, SYSUTCDATETIME()), DATEADD(hour, 3, DATEADD(day, 14, SYSUTCDATETIME())),
          DATEADD(day, 12, SYSUTCDATETIME()), 30, 1, N'published');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (N'423e5f41-a890-5d46-b02b-efcfac8cfcbd', N'zh-Hant', N'【測試】球迷見面會（付費會員限定）', N'【測試】球員與付費球迷會員面對面，名額 30 人。', N'【測試】台中市西屯區測試路 1 號');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (N'423e5f41-a890-5d46-b02b-efcfac8cfcbd', N'en', N'[Test] Fan meet-up (fan club members only)', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM fan_events WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-match-day-party')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO fan_events (id, club_id, slug, starts_at, ends_at, registration_deadline_at, capacity, is_paid_members_only, status)
  VALUES (N'5c05699f-5ac6-54a4-8705-db3366692178', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-match-day-party', DATEADD(day, 30, SYSUTCDATETIME()), DATEADD(hour, 3, DATEADD(day, 30, SYSUTCDATETIME())),
          DATEADD(day, 28, SYSUTCDATETIME()), NULL, 0, N'published');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (N'5c05699f-5ac6-54a4-8705-db3366692178', N'zh-Hant', N'【測試】主場賽事日球迷派對', N'【測試】主場賽事日的球迷同樂活動，不限名額。', N'【測試】台中市西屯區測試路 1 號');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (N'5c05699f-5ac6-54a4-8705-db3366692178', N'en', N'[Test] Match-day fan party', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM fan_events WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-past-event')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO fan_events (id, club_id, slug, starts_at, ends_at, registration_deadline_at, capacity, is_paid_members_only, status)
  VALUES (N'0202b5bf-9059-54cf-abec-9316d335a93a', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-past-event', DATEADD(day, -30, SYSUTCDATETIME()), DATEADD(hour, 3, DATEADD(day, -30, SYSUTCDATETIME())),
          DATEADD(day, -32, SYSUTCDATETIME()), 50, 0, N'draft');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (N'0202b5bf-9059-54cf-abec-9316d335a93a', N'zh-Hant', N'【測試】上季球迷活動回顧（草稿）', N'【測試】已結束的活動，回顧圖集與文章尚未整理。', N'【測試】台中市西屯區測試路 1 號');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (N'0202b5bf-9059-54cf-abec-9316d335a93a', N'en', N'[Test] Last season fan event (draft)', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM fan_events WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'test-bw-fan-day')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO fan_events (id, club_id, slug, starts_at, ends_at, registration_deadline_at, capacity, is_paid_members_only, status)
  VALUES (N'c9751afc-e6cc-51a1-a361-eb1f7f028476', (SELECT id FROM clubs WHERE code = N'bw'), N'test-bw-fan-day', DATEADD(day, 20, SYSUTCDATETIME()), DATEADD(hour, 3, DATEADD(day, 20, SYSUTCDATETIME())),
          DATEADD(day, 18, SYSUTCDATETIME()), 40, 0, N'published');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (N'c9751afc-e6cc-51a1-a361-eb1f7f028476', N'zh-Hant', N'【測試】藍鯨球迷日', N'【測試】藍鯨方的球迷活動示範。', N'【測試】台中市西屯區測試路 1 號');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (N'c9751afc-e6cc-51a1-a361-eb1f7f028476', N'en', N'[Test] Blue Whale fan day', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

-- ── 51. S6 商店設定、發票捐贈碼（虛構代碼 999000X） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.shipping_fee';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'b92ecb95-5a15-50bb-bccd-03e12ecfc94c';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.shipping_fee', N'80', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.free_shipping_threshold';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'53aa0187-e3c2-5706-a77b-09f8479c2c53';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.free_shipping_threshold', N'2000', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.excluded_regions';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'fcdc0759-94e4-593b-a51d-1cf5f2d5196c';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.excluded_regions', N'["【測試】離島地區"]', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.low_stock_threshold';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'682da04f-fe2c-54ae-9af9-8cea35107abc';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.low_stock_threshold', N'5', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.pending_timeout_minutes';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3208e694-c572-501a-bd8b-91c8720bd166';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.pending_timeout_minutes', N'30', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.entry_title';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'3cf6e429-56aa-5ffe-8e97-c08b6e025055';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.entry_title', N'shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.entry_title' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】官方商店' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.entry_title';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.entry_title' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Official shop' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.entry_title';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.entry_intro';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'f37cce29-6cfa-5183-9f5c-4d2cd5841354';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.entry_intro', N'shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.entry_intro' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】商店入口說明占位文字。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.entry_intro';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.entry_intro' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Shop entry placeholder.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.entry_intro';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.policy_shipping';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'dccad59a-fbf2-55b5-9e7b-12546bf176cf';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.policy_shipping', N'shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.policy_shipping' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】運送說明占位文字：宅配、超商取貨與現場自取。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.policy_shipping';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.policy_shipping' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Shipping policy placeholder.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.policy_shipping';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'shop.policy_returns';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'7dbd9b19-f6e9-59e9-8ae4-5b9ac6d91ed6';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'shop.policy_returns', N'shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.policy_returns' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】退換貨政策占位文字。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.policy_returns';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.policy_returns' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Returns policy placeholder.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND s.setting_key = N'shop.policy_returns';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.shipping_fee';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'2653a47b-6944-5fcc-a101-deec92780b86';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.shipping_fee', N'100', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.free_shipping_threshold';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'97240eec-1cbe-57e1-91cb-fcf685e5d8cc';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.free_shipping_threshold', N'1500', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.excluded_regions';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'd21a5b9a-3db2-571e-84da-d1ba7e502aa9';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.excluded_regions', N'["【測試】離島地區"]', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.low_stock_threshold';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'e973c5e1-e980-5b3a-9be2-af32a4cb7699';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.low_stock_threshold', N'3', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.pending_timeout_minutes';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'782f711e-0841-5ea4-9379-352e4001b1dc';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.pending_timeout_minutes', N'30', N'shop');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.entry_title';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'391ec1bc-1f25-56c4-b85d-b0194c88bb0a';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.entry_title', N'shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.entry_title' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】官方商店' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.entry_title';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.entry_title' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Official shop' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.entry_title';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.entry_intro';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'ff555495-a533-5a37-a7bb-18440818f603';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.entry_intro', N'shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.entry_intro' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】商店入口說明占位文字。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.entry_intro';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.entry_intro' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Shop entry placeholder.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.entry_intro';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.policy_shipping';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'6cb0c7b1-df29-597f-bde7-453733f450eb';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.policy_shipping', N'shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.policy_shipping' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】運送說明占位文字：宅配、超商取貨與現場自取。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.policy_shipping';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.policy_shipping' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Shipping policy placeholder.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.policy_shipping';
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'shop.policy_returns';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'34129663-e22c-5ef1-90c0-426540e92166';
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'shop.policy_returns', N'shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.policy_returns' AND si.locale = N'zh-Hant'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'zh-Hant', N'【測試】退換貨政策占位文字。' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.policy_returns';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.policy_returns' AND si.locale = N'en'
)
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, N'en', N'[Test] Returns policy placeholder.' FROM settings s
  WHERE s.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND s.setting_key = N'shop.policy_returns';
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM invoice_donation_codes WHERE code = N'9990001')
  INSERT INTO invoice_donation_codes (id, code, org_name, is_active, sort_order) VALUES (N'6de8e4a2-3f36-511d-88a0-f364d0b4c260', N'9990001', N'【測試】示範公益團體甲', 1, 0);
GO

IF NOT EXISTS (SELECT 1 FROM invoice_donation_codes WHERE code = N'9990002')
  INSERT INTO invoice_donation_codes (id, code, org_name, is_active, sort_order) VALUES (N'ff6b8580-c336-5d11-a398-c59ed13a278e', N'9990002', N'【測試】示範公益團體乙（已停用）', 0, 1);
GO

-- ── 52. S1／S2 商品系列、商品、規格與庫存（tcrfc 5 件、bw 1 件；含低庫存與缺貨示範） ──
IF NOT EXISTS (SELECT 1 FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-club-collection')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO collections (id, club_id, slug, sort_order, status) VALUES (N'c3b8a153-5803-5b81-b40c-34628d293fec', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-club-collection', 0, N'published');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES (N'c3b8a153-5803-5b81-b40c-34628d293fec', N'zh-Hant', N'【測試】俱樂部系列', N'【測試】俱樂部系列的品牌敘事占位文字。');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES (N'c3b8a153-5803-5b81-b40c-34628d293fec', N'en', N'[Test] Club collection', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-academy-collection')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO collections (id, club_id, slug, sort_order, status) VALUES (N'c30db23f-81fa-5961-b595-3029d8c645db', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-academy-collection', 1, N'published');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES (N'c30db23f-81fa-5961-b595-3029d8c645db', N'zh-Hant', N'【測試】學院系列', N'【測試】學院系列的品牌敘事占位文字。');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES (N'c30db23f-81fa-5961-b595-3029d8c645db', N'en', N'[Test] Academy collection', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-fan-collection')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO collections (id, club_id, slug, sort_order, status) VALUES (N'de4e1062-5bab-5f64-b3a6-39e4e73fed67', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-fan-collection', 2, N'published');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES (N'de4e1062-5bab-5f64-b3a6-39e4e73fed67', N'zh-Hant', N'【測試】球迷系列', N'【測試】球迷系列的品牌敘事占位文字。');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES (N'de4e1062-5bab-5f64-b3a6-39e4e73fed67', N'en', N'[Test] Fan collection', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'test-bw-collection')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO collections (id, club_id, slug, sort_order, status) VALUES (N'98566f83-4dd2-5288-bddb-956c3ae8b062', (SELECT id FROM clubs WHERE code = N'bw'), N'test-bw-collection', 0, N'published');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES (N'98566f83-4dd2-5288-bddb-956c3ae8b062', N'zh-Hant', N'【測試】藍鯨系列', N'【測試】藍鯨系列的品牌敘事占位文字。');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES (N'98566f83-4dd2-5288-bddb-956c3ae8b062', N'en', N'[Test] Blue Whale collection', NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM products WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-home-jersey')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO products (id, club_id, slug, collection_id, is_new_arrival, sort_order, status, out_of_stock_behavior)
  VALUES (N'dabaded9-5ade-51d1-9a03-8a3a7a8196c7', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-home-jersey', (SELECT id FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-club-collection'), 1, 0, N'published', N'show_unavailable');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'dabaded9-5ade-51d1-9a03-8a3a7a8196c7', N'zh-Hant', N'【測試】主場球衣', N'【測試】商品敘事占位文字。', N'【測試】,示範');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'dabaded9-5ade-51d1-9a03-8a3a7a8196c7', N'en', N'[Test] Home jersey', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-JSY-S')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'bccdc2c3-a765-5c22-8c25-ff883453bc29', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'dabaded9-5ade-51d1-9a03-8a3a7a8196c7', N'TEST-JSY-S', N'S', N'藍', 1800, NULL, 900, 0, 0, N'active', 0);
  UPDATE product_variants SET stock_qty = 20, updated_at = SYSUTCDATETIME() WHERE id = N'bccdc2c3-a765-5c22-8c25-ff883453bc29';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'02b22b38-6ec4-5cf4-ae77-c8850caae427', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'bccdc2c3-a765-5c22-8c25-ff883453bc29', N'stock_in', 20, 20, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-JSY-M')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'aace3f53-c027-5e55-8909-334d1c18de7f', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'dabaded9-5ade-51d1-9a03-8a3a7a8196c7', N'TEST-JSY-M', N'M', N'藍', 1800, NULL, 900, 0, 0, N'active', 1);
  UPDATE product_variants SET stock_qty = 20, updated_at = SYSUTCDATETIME() WHERE id = N'aace3f53-c027-5e55-8909-334d1c18de7f';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'c104771b-ad0e-5429-9c1d-abea99f7f38a', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'aace3f53-c027-5e55-8909-334d1c18de7f', N'stock_in', 20, 20, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-JSY-L')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'1f18b2e4-b896-5853-8c2d-183ac10b13f8', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'dabaded9-5ade-51d1-9a03-8a3a7a8196c7', N'TEST-JSY-L', N'L', N'藍', 1800, NULL, 900, 0, 0, N'active', 2);
  UPDATE product_variants SET stock_qty = 20, updated_at = SYSUTCDATETIME() WHERE id = N'1f18b2e4-b896-5853-8c2d-183ac10b13f8';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'61741a0d-5ef7-52ea-b1fb-ff7ded336934', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'1f18b2e4-b896-5853-8c2d-183ac10b13f8', N'stock_in', 20, 20, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-JSY-XL')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'f1ca1431-89d4-5c20-9e5b-95f28ea10fb5', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'dabaded9-5ade-51d1-9a03-8a3a7a8196c7', N'TEST-JSY-XL', N'XL', N'藍', 1800, 1600, 900, 0, 0, N'active', 3);
  UPDATE product_variants SET stock_qty = 12, updated_at = SYSUTCDATETIME() WHERE id = N'f1ca1431-89d4-5c20-9e5b-95f28ea10fb5';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'f79a98f0-67f3-5c2a-9387-2d666c40dae8', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'f1ca1431-89d4-5c20-9e5b-95f28ea10fb5', N'stock_in', 12, 12, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM products WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-training-jacket')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO products (id, club_id, slug, collection_id, is_new_arrival, sort_order, status, out_of_stock_behavior)
  VALUES (N'd5fc5036-f3a6-5d73-977d-a896fe323c49', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-training-jacket', (SELECT id FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-academy-collection'), 0, 1, N'published', N'show_unavailable');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'd5fc5036-f3a6-5d73-977d-a896fe323c49', N'zh-Hant', N'【測試】訓練外套', N'【測試】商品敘事占位文字。', N'【測試】,示範');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'd5fc5036-f3a6-5d73-977d-a896fe323c49', N'en', N'[Test] Training jacket', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-JKT-M')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'9fc6a906-f7ca-5418-9a40-b4c0ba1c1fb8', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'd5fc5036-f3a6-5d73-977d-a896fe323c49', N'TEST-JKT-M', N'M', N'黑', 1500, NULL, 700, 0, 0, N'active', 0);
  UPDATE product_variants SET stock_qty = 10, updated_at = SYSUTCDATETIME() WHERE id = N'9fc6a906-f7ca-5418-9a40-b4c0ba1c1fb8';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'0c73c316-381d-5173-a327-49fb60dee145', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'9fc6a906-f7ca-5418-9a40-b4c0ba1c1fb8', N'stock_in', 10, 10, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-JKT-L')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'20b1e91b-0530-501f-aa19-e4fcf1b38889', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'd5fc5036-f3a6-5d73-977d-a896fe323c49', N'TEST-JKT-L', N'L', N'黑', 1500, NULL, 700, 0, 0, N'active', 1);
  UPDATE product_variants SET stock_qty = 10, updated_at = SYSUTCDATETIME() WHERE id = N'20b1e91b-0530-501f-aa19-e4fcf1b38889';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'0acaa335-e592-598f-946f-75d50a0d6ee1', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'20b1e91b-0530-501f-aa19-e4fcf1b38889', N'stock_in', 10, 10, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM products WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-scarf')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO products (id, club_id, slug, collection_id, is_new_arrival, sort_order, status, out_of_stock_behavior)
  VALUES (N'e202d9bf-c987-5743-8591-a7714e32a946', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-scarf', (SELECT id FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-fan-collection'), 0, 2, N'published', N'show_unavailable');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'e202d9bf-c987-5743-8591-a7714e32a946', N'zh-Hant', N'【測試】球迷圍巾', N'【測試】商品敘事占位文字。', N'【測試】,示範');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'e202d9bf-c987-5743-8591-a7714e32a946', N'en', N'[Test] Fan scarf', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-SCARF')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'f467850a-2673-591a-8701-10efa33ca2f3', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'e202d9bf-c987-5743-8591-a7714e32a946', N'TEST-SCARF', NULL, N'藍白', 500, 450, 200, 0, 0, N'active', 0);
  UPDATE product_variants SET stock_qty = 6, updated_at = SYSUTCDATETIME() WHERE id = N'f467850a-2673-591a-8701-10efa33ca2f3';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'bb96d506-1bb7-59d3-8280-e0c62d5e3797', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'f467850a-2673-591a-8701-10efa33ca2f3', N'stock_in', 6, 6, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM products WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-limited-ball')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO products (id, club_id, slug, collection_id, is_new_arrival, sort_order, status, out_of_stock_behavior)
  VALUES (N'0745b48e-a11a-577b-ab03-ee28758e0606', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-limited-ball', (SELECT id FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-fan-collection'), 1, 3, N'published', N'show_unavailable');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'0745b48e-a11a-577b-ab03-ee28758e0606', N'zh-Hant', N'【測試】限量紀念球（缺貨示範）', N'【測試】商品敘事占位文字。', N'【測試】,示範');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'0745b48e-a11a-577b-ab03-ee28758e0606', N'en', N'[Test] Limited ball (sold-out demo)', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-BALL')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'1afed918-a41f-578a-9245-1ace91c7e660', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'0745b48e-a11a-577b-ab03-ee28758e0606', N'TEST-BALL', NULL, NULL, 1200, NULL, 600, 0, 0, N'active', 0);
  UPDATE product_variants SET stock_qty = 1, updated_at = SYSUTCDATETIME() WHERE id = N'1afed918-a41f-578a-9245-1ace91c7e660';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'ca571fe5-d74e-5f62-916c-b41b4dc3e7d9', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'1afed918-a41f-578a-9245-1ace91c7e660', N'stock_in', 1, 1, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM products WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-draft-product')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO products (id, club_id, slug, collection_id, is_new_arrival, sort_order, status, out_of_stock_behavior)
  VALUES (N'f46c1571-bcce-5c59-9c94-fcaa77e91ff1', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'test-draft-product', (SELECT id FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'test-fan-collection'), 0, 4, N'draft', N'show_unavailable');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'f46c1571-bcce-5c59-9c94-fcaa77e91ff1', N'zh-Hant', N'【測試】尚未上架的商品', N'【測試】商品敘事占位文字。', N'【測試】,示範');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'f46c1571-bcce-5c59-9c94-fcaa77e91ff1', N'en', N'[Test] Draft product', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-DRAFT')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'fd4799f9-1d38-5d03-910f-fc7a66b339ed', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'f46c1571-bcce-5c59-9c94-fcaa77e91ff1', N'TEST-DRAFT', NULL, NULL, 300, NULL, 100, 0, 0, N'active', 0);
  UPDATE product_variants SET stock_qty = 5, updated_at = SYSUTCDATETIME() WHERE id = N'fd4799f9-1d38-5d03-910f-fc7a66b339ed';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'8a70b244-b45c-5e24-ae9c-93e8dfe38d1c', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'fd4799f9-1d38-5d03-910f-fc7a66b339ed', N'stock_in', 5, 5, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM products WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'test-bw-jersey')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO products (id, club_id, slug, collection_id, is_new_arrival, sort_order, status, out_of_stock_behavior)
  VALUES (N'7a29e8df-e60c-5dac-a317-9f4facd09bf5', (SELECT id FROM clubs WHERE code = N'bw'), N'test-bw-jersey', (SELECT id FROM collections WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'test-bw-collection'), 1, 5, N'published', N'show_unavailable');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'7a29e8df-e60c-5dac-a317-9f4facd09bf5', N'zh-Hant', N'【測試】藍鯨球衣', N'【測試】商品敘事占位文字。', N'【測試】,示範');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES (N'7a29e8df-e60c-5dac-a317-9f4facd09bf5', N'en', N'[Test] Blue Whale jersey', NULL, NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-BW-JSY-M')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'bf8fef88-4c50-5801-a728-518b24df5fc1', (SELECT id FROM clubs WHERE code = N'bw'), N'7a29e8df-e60c-5dac-a317-9f4facd09bf5', N'TEST-BW-JSY-M', N'M', N'藍', 1600, NULL, 800, 0, 0, N'active', 0);
  UPDATE product_variants SET stock_qty = 15, updated_at = SYSUTCDATETIME() WHERE id = N'bf8fef88-4c50-5801-a728-518b24df5fc1';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'e53f2fb9-808e-5313-b1b0-ee275866a32d', (SELECT id FROM clubs WHERE code = N'bw'), N'bf8fef88-4c50-5801-a728-518b24df5fc1', N'stock_in', 15, 15, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = N'TEST-BW-JSY-L')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES (N'dbd201be-d837-54db-b50a-9f3d5bc3c1f9', (SELECT id FROM clubs WHERE code = N'bw'), N'7a29e8df-e60c-5dac-a317-9f4facd09bf5', N'TEST-BW-JSY-L', N'L', N'藍', 1600, NULL, 800, 0, 0, N'active', 1);
  UPDATE product_variants SET stock_qty = 15, updated_at = SYSUTCDATETIME() WHERE id = N'dbd201be-d837-54db-b50a-9f3d5bc3c1f9';
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES (N'508942d4-c0dc-5122-a7c1-5e871e8bd570', (SELECT id FROM clubs WHERE code = N'bw'), N'dbd201be-d837-54db-b50a-9f3d5bc3c1f9', N'stock_in', 15, 15, 0, N'【測試】初始庫存');
  COMMIT TRANSACTION;
END
GO

-- ── 54. K5 抽獎名單：蒐集告知確認、tcrfc 1 場已抽出（名單 2 人、1 位中獎）＋1 場草稿 ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'member.draw_notice_confirmed';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'bcd7df37-9085-512b-a0da-7b11fe4a5ab9';
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'member.draw_notice_confirmed', N'2026-09-30T00:00:00.0000000Z', N'member');
  COMMIT TRANSACTION;
END
GO

-- ── 56. app_deep_links／app_layout_items：M2 深連結對照（規劃書 §2.3 的 8 條）、首頁九個區塊、快捷入口、「更多」分頁 ──
IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'schedule_d1')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'ba77a354-36d7-5ee4-8bcf-9749109dff66', N'schedule_d1', N'tcrfc://schedule/d1', N'/zh/schedule/d1/', 0, 1, 0);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'ba77a354-36d7-5ee4-8bcf-9749109dff66', N'zh-Hant', N'賽程（磐石一線隊）'), (N'ba77a354-36d7-5ee4-8bcf-9749109dff66', N'en', N'Schedule (Rock FC First Team)');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'schedule_bw1')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'b53db7b0-6ddc-5231-a91c-d2ef612bd38e', N'schedule_bw1', N'tcrfc://schedule/bw1', NULL, 0, 1, 1);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'b53db7b0-6ddc-5231-a91c-d2ef612bd38e', N'zh-Hant', N'賽程（藍鯨一線隊）'), (N'b53db7b0-6ddc-5231-a91c-d2ef612bd38e', N'en', N'Schedule (Blue Whale First Team)');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'match')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'013e1afc-d907-5a55-9936-b18face7b5de', N'match', N'tcrfc://match/{id}', N'/zh/schedule/{slug}', 0, 1, 2);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'013e1afc-d907-5a55-9936-b18face7b5de', N'zh-Hant', N'賽事詳情'), (N'013e1afc-d907-5a55-9936-b18face7b5de', N'en', N'Match Details');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'news')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'70356ace-dfc4-5a6a-9215-5eccf3d68b45', N'news', N'tcrfc://news/{slug}', N'/zh/news/{slug}', 0, 1, 3);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'70356ace-dfc4-5a6a-9215-5eccf3d68b45', N'zh-Hant', N'新聞內文'), (N'70356ace-dfc4-5a6a-9215-5eccf3d68b45', N'en', N'News Article');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'player')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'cabc1aff-f63a-54b8-9246-26408d112750', N'player', N'tcrfc://player/{slug}', N'/zh/club/first-team/player/{slug}', 0, 1, 4);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'cabc1aff-f63a-54b8-9246-26408d112750', N'zh-Hant', N'球員詳情'), (N'cabc1aff-f63a-54b8-9246-26408d112750', N'en', N'Player Profile');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'store')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'2b1f8878-0349-53a5-9ae8-39093207f50f', N'store', N'tcrfc://store/{id}', N'/zh/perks/{slug}', 0, 1, 5);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'2b1f8878-0349-53a5-9ae8-39093207f50f', N'zh-Hant', N'特約店家詳情'), (N'2b1f8878-0349-53a5-9ae8-39093207f50f', N'en', N'Partner Store');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'program')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'01db2c47-277e-5121-8fc8-a74338b01233', N'program', N'tcrfc://program/{slug}', N'/zh/programs/{slug}', 0, 1, 6);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'01db2c47-277e-5121-8fc8-a74338b01233', N'zh-Hant', N'課程詳情'), (N'01db2c47-277e-5121-8fc8-a74338b01233', N'en', N'Program Details');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'membercard')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'2da94ef1-89bb-5fc0-aab6-134d18e140f9', N'membercard', N'tcrfc://membercard', NULL, 1, 1, 7);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'2da94ef1-89bb-5fc0-aab6-134d18e140f9', N'zh-Hant', N'會員卡'), (N'2da94ef1-89bb-5fc0-aab6-134d18e140f9', N'en', N'Membership Card');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = N'upgrade')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES (N'14006681-dafc-5715-8c9a-4955cac52a0f', N'upgrade', N'tcrfc://upgrade', N'/zh/member/upgrade/', 0, 1, 8);
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES (N'14006681-dafc-5715-8c9a-4955cac52a0f', N'zh-Hant', N'會籍升級'), (N'14006681-dafc-5715-8c9a-4955cac52a0f', N'en', N'Membership Upgrade');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'next_match')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'1ddb7c65-d4d7-5a7b-91ce-1ca014cef5f3', N'home_section', N'next_match', NULL, 0, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'1ddb7c65-d4d7-5a7b-91ce-1ca014cef5f3', N'zh-Hant', N'下一場賽事'), (N'1ddb7c65-d4d7-5a7b-91ce-1ca014cef5f3', N'en', N'Next Match');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'ad_home_top')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'ba2c3a6f-158c-58ed-9a72-386f5d47fd11', N'home_section', N'ad_home_top', NULL, 1, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'ba2c3a6f-158c-58ed-9a72-386f5d47fd11', N'zh-Hant', N'廣告版位（首頁上）'), (N'ba2c3a6f-158c-58ed-9a72-386f5d47fd11', N'en', N'Ad Slot (Home Top)');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'latest_news')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'e37cd5b1-3843-5808-b8ae-e4a91dc79fc0', N'home_section', N'latest_news', NULL, 2, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'e37cd5b1-3843-5808-b8ae-e4a91dc79fc0', N'zh-Hant', N'最新消息'), (N'e37cd5b1-3843-5808-b8ae-e4a91dc79fc0', N'en', N'Latest News');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'member_card')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'a6f1a545-faec-5ca8-809f-754deeb11a0c', N'home_section', N'member_card', (SELECT id FROM app_deep_links WHERE code = N'membercard'), 3, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'a6f1a545-faec-5ca8-809f-754deeb11a0c', N'zh-Hant', N'會員卡快捷'), (N'a6f1a545-faec-5ca8-809f-754deeb11a0c', N'en', N'Membership Card');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'recent_matches')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'a690f8bd-10c8-5428-8c9e-7125cd330ac0', N'home_section', N'recent_matches', NULL, 4, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'a690f8bd-10c8-5428-8c9e-7125cd330ac0', N'zh-Hant', N'近期賽事'), (N'a690f8bd-10c8-5428-8c9e-7125cd330ac0', N'en', N'Recent Matches');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'ad_home_mid')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'3127d1ff-8e20-5a89-89a4-fcebb89bb559', N'home_section', N'ad_home_mid', NULL, 5, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'3127d1ff-8e20-5a89-89a4-fcebb89bb559', N'zh-Hant', N'廣告版位（首頁中）'), (N'3127d1ff-8e20-5a89-89a4-fcebb89bb559', N'en', N'Ad Slot (Home Middle)');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'nearby_stores')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'76ee3db6-8671-5aa2-a376-fab76cb0f97a', N'home_section', N'nearby_stores', NULL, 6, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'76ee3db6-8671-5aa2-a376-fab76cb0f97a', N'zh-Hant', N'特約店家（附近）'), (N'76ee3db6-8671-5aa2-a376-fab76cb0f97a', N'en', N'Nearby Partner Stores');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'quick_entries')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'764c239e-cbe3-538b-82ce-2e24e2f2ec59', N'home_section', N'quick_entries', NULL, 7, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'764c239e-cbe3-538b-82ce-2e24e2f2ec59', N'zh-Hant', N'快捷入口'), (N'764c239e-cbe3-538b-82ce-2e24e2f2ec59', N'en', N'Quick Entries');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'home_section' AND item_key = N'sponsor_wall')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'924551f7-57fe-5f38-bbbe-6b3941afc58c', N'home_section', N'sponsor_wall', NULL, 8, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'924551f7-57fe-5f38-bbbe-6b3941afc58c', N'zh-Hant', N'贊助商 Logo 牆'), (N'924551f7-57fe-5f38-bbbe-6b3941afc58c', N'en', N'Sponsors');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'quick_entry' AND item_key = N'programs')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'a1496180-12d1-5ab4-8b38-fb457428bb8e', N'quick_entry', N'programs', (SELECT id FROM app_deep_links WHERE code = N'program'), 0, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'a1496180-12d1-5ab4-8b38-fb457428bb8e', N'zh-Hant', N'課程報名'), (N'a1496180-12d1-5ab4-8b38-fb457428bb8e', N'en', N'Programs');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'quick_entry' AND item_key = N'shop')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'6bc77f50-8126-5eda-9dec-c407c008c70a', N'quick_entry', N'shop', NULL, 1, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'6bc77f50-8126-5eda-9dec-c407c008c70a', N'zh-Hant', N'商店'), (N'6bc77f50-8126-5eda-9dec-c407c008c70a', N'en', N'Shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'teams')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'357ef08d-f9e2-52f0-897c-890e716983be', N'more_item', N'teams', NULL, 0, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'357ef08d-f9e2-52f0-897c-890e716983be', N'zh-Hant', N'球隊名單'), (N'357ef08d-f9e2-52f0-897c-890e716983be', N'en', N'Teams');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'partner_stores')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'a089b26f-fac1-53fb-b258-b5889df1ef9e', N'more_item', N'partner_stores', NULL, 1, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'a089b26f-fac1-53fb-b258-b5889df1ef9e', N'zh-Hant', N'特約店家'), (N'a089b26f-fac1-53fb-b258-b5889df1ef9e', N'en', N'Partner Stores');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'programs')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'b016a177-adf9-524f-91e7-cbfced030a0b', N'more_item', N'programs', (SELECT id FROM app_deep_links WHERE code = N'program'), 2, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'b016a177-adf9-524f-91e7-cbfced030a0b', N'zh-Hant', N'課程報名'), (N'b016a177-adf9-524f-91e7-cbfced030a0b', N'en', N'Programs');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'partners')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'bc12a59b-9f8f-5afd-8250-157ea5714950', N'more_item', N'partners', NULL, 3, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'bc12a59b-9f8f-5afd-8250-157ea5714950', N'zh-Hant', N'夥伴贊助'), (N'bc12a59b-9f8f-5afd-8250-157ea5714950', N'en', N'Partners & Sponsors');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'charity')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'2b3d7052-96f8-5640-ac56-62636fb1e4fa', N'more_item', N'charity', NULL, 4, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'2b3d7052-96f8-5640-ac56-62636fb1e4fa', N'zh-Hant', N'慈善（外連）'), (N'2b3d7052-96f8-5640-ac56-62636fb1e4fa', N'en', N'Charity (external)');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'shop')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'331779d3-6d64-5cc7-b8f9-4fd3885de5a8', N'more_item', N'shop', NULL, 5, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'331779d3-6d64-5cc7-b8f9-4fd3885de5a8', N'zh-Hant', N'商店'), (N'331779d3-6d64-5cc7-b8f9-4fd3885de5a8', N'en', N'Shop');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'faq')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'39441d6d-5eda-5eb2-9e9d-8a7a6f817591', N'more_item', N'faq', NULL, 6, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'39441d6d-5eda-5eb2-9e9d-8a7a6f817591', N'zh-Hant', N'FAQ'), (N'39441d6d-5eda-5eb2-9e9d-8a7a6f817591', N'en', N'FAQ');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'follow_settings')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'436ef7f9-799d-5fb1-9f0a-95ad241d4e00', N'more_item', N'follow_settings', NULL, 7, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'436ef7f9-799d-5fb1-9f0a-95ad241d4e00', N'zh-Hant', N'追蹤設定'), (N'436ef7f9-799d-5fb1-9f0a-95ad241d4e00', N'en', N'Follow Settings');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = N'more_item' AND item_key = N'settings')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES (N'2f560071-1c9a-5e7d-ae42-abf8b37ddba8', N'more_item', N'settings', NULL, 8, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES (N'2f560071-1c9a-5e7d-ae42-abf8b37ddba8', N'zh-Hant', N'設定'), (N'2f560071-1c9a-5e7d-ae42-abf8b37ddba8', N'en', N'Settings');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_announcements WHERE id = N'fed00235-c9ca-501b-b48f-297da8684a6c')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_announcements (id, link_url, starts_at, ends_at, audience_tier, is_enabled)
  VALUES (N'fed00235-c9ca-501b-b48f-297da8684a6c', NULL, DATEADD(day, -1, SYSUTCDATETIME()), DATEADD(year, 5, SYSUTCDATETIME()), N'all', 1);
  INSERT INTO app_announcements_i18n (app_announcement_id, locale, message)
  VALUES (N'fed00235-c9ca-501b-b48f-297da8684a6c', N'zh-Hant', N'【測試】App 專屬公告條：歡迎使用台中磐石 × 台中藍鯨官方 App。'), (N'fed00235-c9ca-501b-b48f-297da8684a6c', N'en', N'[Test] App announcement bar.');
  COMMIT TRANSACTION;
END
GO

-- ── 57. app_feature_flags／app_releases／app_credentials：M1、M5（功能開關預設取自 docs/19 §7、§10；版本與憑證為【測試】） ──
IF NOT EXISTS (SELECT 1 FROM app_feature_flags WHERE flag_key = N'ads_enabled' AND platform = N'all')
  INSERT INTO app_feature_flags (id, flag_key, is_enabled, string_value, platform, description)
  VALUES (N'6c8d1462-cd15-51ef-93c1-84aa44230a6b', N'ads_enabled', 1, NULL, N'all', N'廣告版位總開關');
GO

IF NOT EXISTS (SELECT 1 FROM app_feature_flags WHERE flag_key = N'map_enabled' AND platform = N'all')
  INSERT INTO app_feature_flags (id, flag_key, is_enabled, string_value, platform, description)
  VALUES (N'a6d7d3a9-5d33-5226-8945-14e6c3deb2f7', N'map_enabled', 1, NULL, N'all', N'附近店家地圖');
GO

IF NOT EXISTS (SELECT 1 FROM app_feature_flags WHERE flag_key = N'biometric_unlock_enabled' AND platform = N'all')
  INSERT INTO app_feature_flags (id, flag_key, is_enabled, string_value, platform, description)
  VALUES (N'5065b4c8-ab5d-5f3b-aab4-f314de469d9e', N'biometric_unlock_enabled', 0, NULL, N'all', N'會員卡生物辨識快速開啟（選配）');
GO

IF NOT EXISTS (SELECT 1 FROM app_feature_flags WHERE flag_key = N'payment_mode' AND platform = N'all')
  INSERT INTO app_feature_flags (id, flag_key, is_enabled, string_value, platform, description)
  VALUES (N'287b9e6f-536a-59a8-85c2-3ea9b8c642a1', N'payment_mode', 1, N'external', N'all', N'付款模式（off／external／inapp）：首個上架版本不含 App 內付款，只能降級不得反向開啟 inapp');
GO

IF NOT EXISTS (SELECT 1 FROM app_releases WHERE platform = N'ios' AND version = N'0.9.0')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_releases (id, platform, version, build_number, released_on, status, is_min_supported, is_recommended)
  VALUES (N'fe3798ba-abfe-5fce-8ade-b593943ab09a', N'ios', N'0.9.0', N'900', '2026-09-30', N'live', 1, 0);
  INSERT INTO app_releases_i18n (app_release_id, locale, whats_new, force_message, recommend_message)
  VALUES (N'fe3798ba-abfe-5fce-8ade-b593943ab09a', N'zh-Hant', N'【測試】最低支援版本示範', N'【測試】請更新到最新版本才能繼續使用。', N'【測試】有新版本可以更新。'),
         (N'fe3798ba-abfe-5fce-8ade-b593943ab09a', N'en', N'[Test] release', N'[Test] Please update to keep using the app.', N'[Test] A new version is available.');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_releases WHERE platform = N'ios' AND version = N'1.0.0')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_releases (id, platform, version, build_number, released_on, status, is_min_supported, is_recommended)
  VALUES (N'e2fb5308-ba12-5af8-b183-0c1dd7837a16', N'ios', N'1.0.0', N'900', '2026-09-30', N'testing', 0, 0);
  INSERT INTO app_releases_i18n (app_release_id, locale, whats_new, force_message, recommend_message)
  VALUES (N'e2fb5308-ba12-5af8-b183-0c1dd7837a16', N'zh-Hant', N'【測試】測試中的版本', N'【測試】請更新到最新版本才能繼續使用。', N'【測試】有新版本可以更新。'),
         (N'e2fb5308-ba12-5af8-b183-0c1dd7837a16', N'en', N'[Test] release', N'[Test] Please update to keep using the app.', N'[Test] A new version is available.');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_releases WHERE platform = N'android' AND version = N'0.9.0')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO app_releases (id, platform, version, build_number, released_on, status, is_min_supported, is_recommended)
  VALUES (N'5359ddfa-04dc-5a16-a010-b6c28a69b328', N'android', N'0.9.0', N'900', '2026-09-30', N'live', 1, 0);
  INSERT INTO app_releases_i18n (app_release_id, locale, whats_new, force_message, recommend_message)
  VALUES (N'5359ddfa-04dc-5a16-a010-b6c28a69b328', N'zh-Hant', N'【測試】最低支援版本示範', N'【測試】請更新到最新版本才能繼續使用。', N'【測試】有新版本可以更新。'),
         (N'5359ddfa-04dc-5a16-a010-b6c28a69b328', N'en', N'[Test] release', N'[Test] Please update to keep using the app.', N'[Test] A new version is available.');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM app_credentials WHERE kind = N'apns_key' AND label = N'【測試】APNs 金鑰（.p8）')
  INSERT INTO app_credentials (id, kind, label, external_ref, created_on, last_rotated_on, expires_on, rotation_period_days, note)
  VALUES (N'048e7128-9d0b-58c9-93de-420ddd6c33fd', N'apns_key', N'【測試】APNs 金鑰（.p8）', N'TESTKEYID01', '2026-01-15', '2026-06-01',
          NULL, 365, N'【測試】只存列管資訊，金鑰本身不進資料庫。');
GO

IF NOT EXISTS (SELECT 1 FROM app_credentials WHERE kind = N'fcm_credential' AND label = N'【測試】FCM 服務帳號')
  INSERT INTO app_credentials (id, kind, label, external_ref, created_on, last_rotated_on, expires_on, rotation_period_days, note)
  VALUES (N'66c938ad-b7ab-582c-8f06-2f689bed4d59', N'fcm_credential', N'【測試】FCM 服務帳號', N'test-project-000', '2026-01-15', NULL,
          NULL, 180, N'【測試】只存列管資訊，金鑰本身不進資料庫。');
GO

IF NOT EXISTS (SELECT 1 FROM app_credentials WHERE kind = N'apple_developer_program' AND label = N'【測試】Apple 開發者帳號會籍')
  INSERT INTO app_credentials (id, kind, label, external_ref, created_on, last_rotated_on, expires_on, rotation_period_days, note)
  VALUES (N'a2aa105f-2c33-52e9-9a90-98542bf2f5f2', N'apple_developer_program', N'【測試】Apple 開發者帳號會籍', NULL, '2025-11-01', NULL,
          '2026-11-20', NULL, N'【測試】只存列管資訊，金鑰本身不進資料庫。');
GO

-- ── 58. ad_slots／advertisers／ad_campaigns／ad_creatives／ad_daily_stats：E4–E6（2 版位、2 廣告主、1 個已結束的曝光保證檔期含 14 天示範成效；【測試】） ──
IF NOT EXISTS (SELECT 1 FROM ad_slots WHERE slot_code = N'home_top')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO ad_slots (id, slot_code, surface, screen_code, block_order, aspect_ratio, min_width, min_height, max_file_kb, allowed_formats, allow_video, session_impression_cap, rotation_cap, is_active)
  VALUES (N'54ae5b48-4dbe-52b6-a9b5-d6887682ecc2', N'home_top', N'app', N'S01', 2, N'16:9', 1280, 720, 500, N'JPEG／PNG／WebP', 0, 3, 3, 1);
  INSERT INTO ad_slots_i18n (ad_slot_id, locale, name, fallback_alt)
  VALUES (N'54ae5b48-4dbe-52b6-a9b5-d6887682ecc2', N'zh-Hant', N'【測試】首頁上方版位', N'【測試】自家內容'), (N'54ae5b48-4dbe-52b6-a9b5-d6887682ecc2', N'en', N'[Test] Home Top', N'[Test] House content');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM ad_slots WHERE slot_code = N'home_mid')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO ad_slots (id, slot_code, surface, screen_code, block_order, aspect_ratio, min_width, min_height, max_file_kb, allowed_formats, allow_video, session_impression_cap, rotation_cap, is_active)
  VALUES (N'053061f5-197d-5c9d-abe2-c001099f2dca', N'home_mid', N'app', N'S01', 6, N'3:1', 1200, 400, 500, N'JPEG／PNG／WebP', 0, 3, 3, 1);
  INSERT INTO ad_slots_i18n (ad_slot_id, locale, name, fallback_alt)
  VALUES (N'053061f5-197d-5c9d-abe2-c001099f2dca', N'zh-Hant', N'【測試】首頁中段版位', N'【測試】自家內容'), (N'053061f5-197d-5c9d-abe2-c001099f2dca', N'en', N'[Test] Home Middle', N'[Test] House content');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM advertisers WHERE id = N'10729898-d78a-5d3b-99dc-b057e528043d')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO advertisers (id, tax_id, contact_name, contact_phone, contact_email, contract_note, cooperation_start_on, status)
  VALUES (N'10729898-d78a-5d3b-99dc-b057e528043d', N'00000000', N'【測試】聯絡人', N'0900-000-000', N'advertiser-a@example.com', N'【測試】合約備註', '2026-09-01', N'active');
  INSERT INTO advertisers_i18n (advertiser_id, locale, name) VALUES (N'10729898-d78a-5d3b-99dc-b057e528043d', N'zh-Hant', N'【測試】廣告主甲'), (N'10729898-d78a-5d3b-99dc-b057e528043d', N'en', N'[Test] Advertiser A');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM advertisers WHERE id = N'a75e1365-ffae-5753-a26b-e1a88146c771')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO advertisers (id, tax_id, contact_name, contact_phone, contact_email, contract_note, cooperation_start_on, status)
  VALUES (N'a75e1365-ffae-5753-a26b-e1a88146c771', N'00000000', N'【測試】聯絡人', N'0900-000-000', N'advertiser-b@example.com', N'【測試】合約備註', '2026-09-01', N'negotiating');
  INSERT INTO advertisers_i18n (advertiser_id, locale, name) VALUES (N'a75e1365-ffae-5753-a26b-e1a88146c771', N'zh-Hant', N'【測試】廣告主乙'), (N'a75e1365-ffae-5753-a26b-e1a88146c771', N'en', N'[Test] Advertiser B');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM ad_campaigns WHERE id = N'db15a997-1f96-5f2b-9282-1906014a62be')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO ad_campaigns (id, advertiser_id, slot_id, name, starts_at, ends_at, weight, goal_type, goal_impressions, delivered_total, contract_amount, is_amount_hidden, status)
  VALUES (N'db15a997-1f96-5f2b-9282-1906014a62be', N'10729898-d78a-5d3b-99dc-b057e528043d', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'【測試】已結束的曝光保證檔期',
          DATEADD(day, -16, SYSUTCDATETIME()), DATEADD(day, -2, SYSUTCDATETIME()), 3, N'guaranteed', 5000, 4674, 30000, 1, N'ended');
  INSERT INTO ad_creatives (id, campaign_id, locale, alt_text, title, cta_text, click_url, theme, review_status)
  VALUES (N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', N'db15a997-1f96-5f2b-9282-1906014a62be', N'zh-Hant', N'【測試】素材替代文字', N'【測試】廣告標題', N'了解更多', N'https://example.com/ad', N'both', N'approved');
  INSERT INTO ad_daily_stats (stat_date, campaign_id, creative_id, slot_id, platform, locale, impressions, clicks, unique_devices) VALUES
    ('2026-09-22', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 160, 6, 128),
    ('2026-09-22', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 140, 5, 112),
    ('2026-09-23', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 167, 6, 133),
    ('2026-09-23', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 147, 5, 117),
    ('2026-09-24', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 174, 6, 139),
    ('2026-09-24', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 154, 6, 123),
    ('2026-09-25', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 181, 7, 144),
    ('2026-09-25', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 161, 6, 128),
    ('2026-09-26', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 188, 7, 150),
    ('2026-09-26', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 168, 6, 134),
    ('2026-09-27', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 195, 7, 156),
    ('2026-09-27', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 175, 7, 140),
    ('2026-09-28', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 162, 6, 129),
    ('2026-09-28', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 142, 5, 113),
    ('2026-09-29', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 169, 6, 135),
    ('2026-09-29', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 149, 5, 119),
    ('2026-09-30', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 176, 7, 140),
    ('2026-09-30', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 156, 6, 124),
    ('2026-10-01', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 183, 7, 146),
    ('2026-10-01', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 163, 6, 130),
    ('2026-10-02', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 190, 7, 152),
    ('2026-10-02', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 170, 6, 136),
    ('2026-10-03', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 197, 7, 157),
    ('2026-10-03', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 177, 7, 141),
    ('2026-10-04', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 164, 6, 131),
    ('2026-10-04', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 144, 5, 115),
    ('2026-10-05', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'ios', N'zh-Hant', 171, 6, 136),
    ('2026-10-05', N'db15a997-1f96-5f2b-9282-1906014a62be', N'8c9cb2a9-2eb5-500c-9ee4-dffeee520b24', (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'android', N'zh-Hant', 151, 6, 120);
  COMMIT TRANSACTION;
END
GO

UPDATE ad_campaigns SET delivered_total = 4674
WHERE id = N'db15a997-1f96-5f2b-9282-1906014a62be' AND delivered_total <> 4674
  AND (SELECT COALESCE(SUM(impressions), 0) FROM ad_daily_stats WHERE campaign_id = N'db15a997-1f96-5f2b-9282-1906014a62be') = 4674;
GO

IF NOT EXISTS (SELECT 1 FROM ad_campaigns WHERE id = N'7ee4f0cc-f0eb-589b-b68b-5dc2ec29d205')
  INSERT INTO ad_campaigns (id, advertiser_id, slot_id, name, starts_at, ends_at, weight, goal_type, status)
  VALUES (N'7ee4f0cc-f0eb-589b-b68b-5dc2ec29d205', N'a75e1365-ffae-5753-a26b-e1a88146c771', (SELECT id FROM ad_slots WHERE slot_code = N'home_mid'), N'【測試】草稿檔期',
          DATEADD(day, 7, SYSUTCDATETIME()), DATEADD(day, 21, SYSUTCDATETIME()), 1, N'traffic', N'draft');
GO

-- ── 61. 英文欄位回填（*_i18n 的 en 列；只補缺的、不覆寫既有英文；僅 tcrfc，藍鯨不在此列） ──
INSERT INTO competitions_i18n (competition_id, locale, name)
SELECT b.id, N'en', N'Enterprise Premier League'
FROM competitions b JOIN competitions_i18n z ON z.competition_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'企業甲級足球聯賽'
  AND NOT EXISTS (SELECT 1 FROM competitions_i18n e WHERE e.competition_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Enterprise Premier League' ELSE e.name END
FROM competitions_i18n e
JOIN competitions b ON b.id = e.competition_id
JOIN competitions_i18n z ON z.competition_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'企業甲級足球聯賽';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team A'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 A'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team A' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 A';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team B'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 B'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team B' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 B';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team C'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 C'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team C' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 C';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team D'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 D'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team D' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 D';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team E'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 E'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team E' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 E';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team F'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 F'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team F' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 F';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team G'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 G'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team G' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 G';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team H'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 H'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team H' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 H';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team I'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 I'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team I' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 I';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team J'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 J'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team J' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 J';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team K'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 K'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team K' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 K';
GO

INSERT INTO standings_i18n (standing_id, locale, team_name)
SELECT b.id, N'en', N'[Test] Team L'
FROM standings b JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE z.team_name = N'【測試】隊伍 L'
  AND NOT EXISTS (SELECT 1 FROM standings_i18n e WHERE e.standing_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.team_name = CASE WHEN e.team_name IS NULL OR e.team_name = N'' OR e.team_name = z.team_name THEN N'[Test] Team L' ELSE e.team_name END
FROM standings_i18n e
JOIN standings b ON b.id = e.standing_id
JOIN standings_i18n z ON z.standing_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.team_name = N'【測試】隊伍 L';
GO

INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing)
SELECT b.id, N'en', N'[Test] Demo cup', N'Champions'
FROM achievements b JOIN achievements_i18n z ON z.achievement_id = b.id AND z.locale = N'zh-Hant'
WHERE z.competition_name = N'【測試】示範盃賽'
  AND NOT EXISTS (SELECT 1 FROM achievements_i18n e WHERE e.achievement_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.competition_name = CASE WHEN e.competition_name IS NULL OR e.competition_name = N'' OR e.competition_name = z.competition_name THEN N'[Test] Demo cup' ELSE e.competition_name END,
    e.placing = CASE WHEN e.placing IS NULL OR e.placing = N'' OR e.placing = z.placing THEN N'Champions' ELSE e.placing END
FROM achievements_i18n e
JOIN achievements b ON b.id = e.achievement_id
JOIN achievements_i18n z ON z.achievement_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.competition_name = N'【測試】示範盃賽';
GO

INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing)
SELECT b.id, N'en', N'[Test] Demo league', N'3rd place'
FROM achievements b JOIN achievements_i18n z ON z.achievement_id = b.id AND z.locale = N'zh-Hant'
WHERE z.competition_name = N'【測試】示範聯賽'
  AND NOT EXISTS (SELECT 1 FROM achievements_i18n e WHERE e.achievement_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.competition_name = CASE WHEN e.competition_name IS NULL OR e.competition_name = N'' OR e.competition_name = z.competition_name THEN N'[Test] Demo league' ELSE e.competition_name END,
    e.placing = CASE WHEN e.placing IS NULL OR e.placing = N'' OR e.placing = z.placing THEN N'3rd place' ELSE e.placing END
FROM achievements_i18n e
JOIN achievements b ON b.id = e.achievement_id
JOIN achievements_i18n z ON z.achievement_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.competition_name = N'【測試】示範聯賽';
GO

INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing)
SELECT b.id, N'en', N'[Test] Demo friendly', N'Runners-up'
FROM achievements b JOIN achievements_i18n z ON z.achievement_id = b.id AND z.locale = N'zh-Hant'
WHERE z.competition_name = N'【測試】示範友誼賽'
  AND NOT EXISTS (SELECT 1 FROM achievements_i18n e WHERE e.achievement_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.competition_name = CASE WHEN e.competition_name IS NULL OR e.competition_name = N'' OR e.competition_name = z.competition_name THEN N'[Test] Demo friendly' ELSE e.competition_name END,
    e.placing = CASE WHEN e.placing IS NULL OR e.placing = N'' OR e.placing = z.placing THEN N'Runners-up' ELSE e.placing END
FROM achievements_i18n e
JOIN achievements b ON b.id = e.achievement_id
JOIN achievements_i18n z ON z.achievement_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.competition_name = N'【測試】示範友誼賽';
GO

INSERT INTO proposals_i18n (proposal_id, locale, title)
SELECT b.id, N'en', N'[Test] Sponsorship proposal (Version A)'
FROM proposals b JOIN proposals_i18n z ON z.proposal_id = b.id AND z.locale = N'zh-Hant'
WHERE z.title = N'【測試】贊助提案簡介（A 版）'
  AND NOT EXISTS (SELECT 1 FROM proposals_i18n e WHERE e.proposal_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Sponsorship proposal (Version A)' ELSE e.title END
FROM proposals_i18n e
JOIN proposals b ON b.id = e.proposal_id
JOIN proposals_i18n z ON z.proposal_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.title = N'【測試】贊助提案簡介（A 版）';
GO

INSERT INTO proposals_i18n (proposal_id, locale, title)
SELECT b.id, N'en', N'[Test] Sponsorship proposal (Version B)'
FROM proposals b JOIN proposals_i18n z ON z.proposal_id = b.id AND z.locale = N'zh-Hant'
WHERE z.title = N'【測試】贊助提案簡介（B 版）'
  AND NOT EXISTS (SELECT 1 FROM proposals_i18n e WHERE e.proposal_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Sponsorship proposal (Version B)' ELSE e.title END
FROM proposals_i18n e
JOIN proposals b ON b.id = e.proposal_id
JOIN proposals_i18n z ON z.proposal_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.title = N'【測試】贊助提案簡介（B 版）';
GO

INSERT INTO membership_plans_i18n (membership_plan_id, locale, mid_season_rule)
SELECT b.id, N'en', N'[Test] Pro-rated pricing for mid-season sign-ups'
FROM membership_plans b JOIN membership_plans_i18n z ON z.membership_plan_id = b.id AND z.locale = N'zh-Hant'
WHERE z.mid_season_rule = N'【測試】季中入會照比例計價'
  AND NOT EXISTS (SELECT 1 FROM membership_plans_i18n e WHERE e.membership_plan_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.mid_season_rule = CASE WHEN e.mid_season_rule IS NULL OR e.mid_season_rule = N'' OR e.mid_season_rule = z.mid_season_rule THEN N'[Test] Pro-rated pricing for mid-season sign-ups' ELSE e.mid_season_rule END
FROM membership_plans_i18n e
JOIN membership_plans b ON b.id = e.membership_plan_id
JOIN membership_plans_i18n z ON z.membership_plan_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.mid_season_rule = N'【測試】季中入會照比例計價';
GO

INSERT INTO membership_plans_i18n (membership_plan_id, locale, mid_season_rule)
SELECT b.id, N'en', N'[Test] No discount for mid-season sign-ups'
FROM membership_plans b JOIN membership_plans_i18n z ON z.membership_plan_id = b.id AND z.locale = N'zh-Hant'
WHERE z.mid_season_rule = N'【測試】季中入會不折價'
  AND NOT EXISTS (SELECT 1 FROM membership_plans_i18n e WHERE e.membership_plan_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.mid_season_rule = CASE WHEN e.mid_season_rule IS NULL OR e.mid_season_rule = N'' OR e.mid_season_rule = z.mid_season_rule THEN N'[Test] No discount for mid-season sign-ups' ELSE e.mid_season_rule END
FROM membership_plans_i18n e
JOIN membership_plans b ON b.id = e.membership_plan_id
JOIN membership_plans_i18n z ON z.membership_plan_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.mid_season_rule = N'【測試】季中入會不折價';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'TBC'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'TBC'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'TBC' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'TBC';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Xitun Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'西屯足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Xitun Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'西屯足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Taipei Athletics Stadium'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'台北田徑場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Taipei Athletics Stadium' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'台北田徑場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Tainan Municipal Football Stadium'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'台南市立足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Tainan Municipal Football Stadium' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'台南市立足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Nanzih Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'楠梓足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Nanzih Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'楠梓足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Xizhi Sports Complex'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'汐止綜合運動場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Xizhi Sports Complex' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'汐止綜合運動場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Fu Jen Catholic University Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'輔仁大學足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Fu Jen Catholic University Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.venue = N'輔仁大學足球場';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Goalkeeper Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'守門員教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Goalkeeper Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'守門員教練';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'教練';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Head Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'總教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Head Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'總教練';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Youth Development Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'青訓教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Youth Development Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'青訓教練';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Youth Development Director'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'青訓總監'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Youth Development Director' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'青訓總監';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Advisor'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'顧問'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Advisor' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'顧問';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Fitness Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'體能教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Fitness Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'體能教練';
GO

INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url)
SELECT b.id, N'en', N'LOCAL ROOTS. GLOBAL PATHWAYS.', N'Taichung Rock FC · Founded in 2024 · 2024 National Second Division champions', N'[Test] Carousel image description (image not yet uploaded)', N'About TCRFC', N'/en/about/', N'View schedule', N'/en/schedule/'
FROM banners b JOIN banners_i18n z ON z.banner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'在地扎根 放眼世界'
  AND NOT EXISTS (SELECT 1 FROM banners_i18n e WHERE e.banner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'LOCAL ROOTS. GLOBAL PATHWAYS.' ELSE e.title END,
    e.subtitle = CASE WHEN e.subtitle IS NULL OR e.subtitle = N'' OR e.subtitle = z.subtitle THEN N'Taichung Rock FC · Founded in 2024 · 2024 National Second Division champions' ELSE e.subtitle END,
    e.image_alt = CASE WHEN e.image_alt IS NULL OR e.image_alt = N'' OR e.image_alt = z.image_alt THEN N'[Test] Carousel image description (image not yet uploaded)' ELSE e.image_alt END,
    e.cta_1_label = CASE WHEN e.cta_1_label IS NULL OR e.cta_1_label = N'' OR e.cta_1_label = z.cta_1_label THEN N'About TCRFC' ELSE e.cta_1_label END,
    e.cta_1_url = CASE WHEN e.cta_1_url IS NULL OR e.cta_1_url = N'' OR e.cta_1_url = z.cta_1_url THEN N'/en/about/' ELSE e.cta_1_url END,
    e.cta_2_label = CASE WHEN e.cta_2_label IS NULL OR e.cta_2_label = N'' OR e.cta_2_label = z.cta_2_label THEN N'View schedule' ELSE e.cta_2_label END,
    e.cta_2_url = CASE WHEN e.cta_2_url IS NULL OR e.cta_2_url = N'' OR e.cta_2_url = z.cta_2_url THEN N'/en/schedule/' ELSE e.cta_2_url END
FROM banners_i18n e
JOIN banners b ON b.id = e.banner_id
JOIN banners_i18n z ON z.banner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'在地扎根 放眼世界';
GO

INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url)
SELECT b.id, N'en', N'[Test] Second carousel slide title', N'[Test] This is test content. Please replace it in the back office before launch.', N'[Test] Carousel image description (image not yet uploaded)', N'Join as a Player', N'/en/join/player/'
FROM banners b JOIN banners_i18n z ON z.banner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】第二張輪播標題'
  AND NOT EXISTS (SELECT 1 FROM banners_i18n e WHERE e.banner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Second carousel slide title' ELSE e.title END,
    e.subtitle = CASE WHEN e.subtitle IS NULL OR e.subtitle = N'' OR e.subtitle = z.subtitle THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.subtitle END,
    e.image_alt = CASE WHEN e.image_alt IS NULL OR e.image_alt = N'' OR e.image_alt = z.image_alt THEN N'[Test] Carousel image description (image not yet uploaded)' ELSE e.image_alt END,
    e.cta_1_label = CASE WHEN e.cta_1_label IS NULL OR e.cta_1_label = N'' OR e.cta_1_label = z.cta_1_label THEN N'Join as a Player' ELSE e.cta_1_label END,
    e.cta_1_url = CASE WHEN e.cta_1_url IS NULL OR e.cta_1_url = N'' OR e.cta_1_url = z.cta_1_url THEN N'/en/join/player/' ELSE e.cta_1_url END
FROM banners_i18n e
JOIN banners b ON b.id = e.banner_id
JOIN banners_i18n z ON z.banner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】第二張輪播標題';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Our Story | About TCRFC | Taichung Rock FC', N'Taichung Rock FC (TCRFC) was founded in Taichung in 2024. This page will tell the club''s story from its founding to today; the full text is still being prepared.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/our-story'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Our Story | About TCRFC | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Taichung Rock FC (TCRFC) was founded in Taichung in 2024. This page will tell the club''s story from its founding to today; the full text is still being prepared.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/our-story';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Vision & Mission | About TCRFC | Taichung Rock FC', N'The vision and mission of Taichung Rock FC: through a professional development system, we help local players in Taichung reach the professional stage and let the world see Taiwan football.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/vision-mission'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Vision & Mission | About TCRFC | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'The vision and mission of Taichung Rock FC: through a professional development system, we help local players in Taichung reach the professional stage and let the world see Taiwan football.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/vision-mission';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Our Philosophy | About TCRFC | Taichung Rock FC', N'The football philosophy and five core values of Taichung Rock FC: Players First, Excellence, Global Pathways, Community and Integrity.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/philosophy'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Our Philosophy | About TCRFC | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'The football philosophy and five core values of Taichung Rock FC: Players First, Excellence, Global Pathways, Community and Integrity.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/philosophy';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Governance | About TCRFC | Taichung Rock FC', N'Governance information and a download area for the public documents of Taichung Rock FC. Content is updated regularly.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/governance'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Governance | About TCRFC | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Governance information and a download area for the public documents of Taichung Rock FC. Content is updated regularly.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/governance';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Club History | About TCRFC | Taichung Rock FC', N'A visual narrative of how Taichung Rock FC has developed since its founding in 2024. The full text is still being prepared.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/history'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Club History | About TCRFC | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'A visual narrative of how Taichung Rock FC has developed since its founding in 2024. The full text is still being prepared.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'about/history';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Player Development | Taichung Rock FC', N'Taichung Rock Football Club Player Development: technical and tactical analysis, physical fitness training, match reading, mental resilience, video analysis, Individual Development Plans, nutrition and lifestyle, and education and language, with all eight modules explained.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'club/player-development'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Player Development | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Taichung Rock Football Club Player Development: technical and tactical analysis, physical fitness training, match reading, mental resilience, video analysis, Individual Development Plans, nutrition and lifestyle, and education and language, with all eight modules explained.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'club/player-development';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Player Opportunities | Taichung Rock FC', N'Player opportunities at Taichung Rock Football Club: how to apply to join the First Team, trial dates with online registration, and the recruitment channel for international players (English first).'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'club/opportunities'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Player Opportunities | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Player opportunities at Taichung Rock Football Club: how to apply to join the First Team, trial dates with online registration, and the recruitment channel for international players (English first).' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'club/opportunities';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'International Pathways | Taichung Rock FC', N'Taichung Rock Football Club International Pathways: the full route from local football to clubs abroad, regional information for Europe, Japan and Hong Kong, a wall of partner clubs, and channels for trial scouting and overseas club matching.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'club/international-pathways'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'International Pathways | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Taichung Rock Football Club International Pathways: the full route from local football to clubs abroad, regional information for Europe, Japan and Hong Kong, a wall of partner clubs, and channels for trial scouting and overseas club matching.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'club/international-pathways';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Player Stories | Taichung Rock FC', N'Player stories from Taichung Rock Football Club, filterable by Academy, First Team, Overseas and Women''s football, recording each player''s real journey from joining Taichung Rock to a bigger stage.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'club/player-stories'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Player Stories | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Player stories from Taichung Rock Football Club, filterable by Academy, First Team, Overseas and Women''s football, recording each player''s real journey from joining Taichung Rock to a bigger stage.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'club/player-stories';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Women''s Football | Taichung Rock FC', N'An introduction to the Taichung Blue Whale women''s team and the entry point to its official website. For the full squad, coaching staff, fixtures and results, please visit the Taichung Blue Whale official website.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'womens'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Women''s Football | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'An introduction to the Taichung Blue Whale women''s team and the entry point to its official website. For the full squad, coaching staff, fixtures and results, please visit the Taichung Blue Whale official website.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'womens';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Become a Partner | Partners & Sponsors | Taichung Rock FC', N'Learn about the six values of partnering with Taichung Rock FC: brand exposure, audience reach, social impact and an international development network.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'partners/become-a-partner'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Become a Partner | Partners & Sponsors | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Learn about the six values of partnering with Taichung Rock FC: brand exposure, audience reach, social impact and an international development network.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'partners/become-a-partner';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Our Commitment and Focus Areas | Charity & Impact | Taichung Rock FC', N'The charitable philosophy and four focus areas of Taichung Rock FC: youth support, rural football, disadvantaged families and charity matches, putting our core value of Community into practice.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'charity/commitment'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Our Commitment and Focus Areas | Charity & Impact | Taichung Rock FC' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'The charitable philosophy and four focus areas of Taichung Rock FC: youth support, rural football, disadvantaged families and charity matches, putting our core value of Community into practice.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'charity/commitment';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about joining the team?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】加入球隊的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about joining the team?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】加入球隊的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about Academy admissions?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】學院招生的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about Academy admissions?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】學院招生的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about program and camp registration?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】課程與營隊報名的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about program and camp registration?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】課程與營隊報名的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about fees and refunds?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】費用與退費的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about fees and refunds?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】費用與退費的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about trials?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】試訓的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about trials?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】試訓的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about international pathways and overseas players?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】國際發展與海外球員的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about international pathways and overseas players?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】國際發展與海外球員的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about women''s football?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】女子足球的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about women''s football?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】女子足球的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about the fan club and merchandise?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】球迷會與商品的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about the fan club and merchandise?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】球迷會與商品的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about partnerships and sponsorship?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】合作與贊助的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about partnerships and sponsorship?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】合作與贊助的常見問題範例？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'[Test] Sample question about other topics?', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】其他的常見問題範例？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'[Test] Sample question about other topics?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.question = N'【測試】其他的常見問題範例？';
GO

INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description)
SELECT b.id, N'en', N'[Test] New season press conference', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM calendar_custom_events b JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】新賽季記者會'
  AND NOT EXISTS (SELECT 1 FROM calendar_custom_events_i18n e WHERE e.calendar_custom_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] New season press conference' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.description END
FROM calendar_custom_events_i18n e
JOIN calendar_custom_events b ON b.id = e.calendar_custom_event_id
JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】新賽季記者會';
GO

INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Fan meet-and-greet', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM calendar_custom_events b JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】球迷見面會'
  AND NOT EXISTS (SELECT 1 FROM calendar_custom_events_i18n e WHERE e.calendar_custom_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Fan meet-and-greet' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.description END
FROM calendar_custom_events_i18n e
JOIN calendar_custom_events b ON b.id = e.calendar_custom_event_id
JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】球迷見面會';
GO

INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Weekly open training session', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM calendar_custom_events b JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】每週公開訓練'
  AND NOT EXISTS (SELECT 1 FROM calendar_custom_events_i18n e WHERE e.calendar_custom_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Weekly open training session' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.description END
FROM calendar_custom_events_i18n e
JOIN calendar_custom_events b ON b.id = e.calendar_custom_event_id
JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】每週公開訓練';
GO

INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Venue closure notice', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM calendar_custom_events b JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】場地休館公告'
  AND NOT EXISTS (SELECT 1 FROM calendar_custom_events_i18n e WHERE e.calendar_custom_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Venue closure notice' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.description END
FROM calendar_custom_events_i18n e
JOIN calendar_custom_events b ON b.id = e.calendar_custom_event_id
JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】場地休館公告';
GO

INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Internal staff meeting (not public)', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM calendar_custom_events b JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】內部工作會議（不公開）'
  AND NOT EXISTS (SELECT 1 FROM calendar_custom_events_i18n e WHERE e.calendar_custom_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Internal staff meeting (not public)' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.description END
FROM calendar_custom_events_i18n e
JOIN calendar_custom_events b ON b.id = e.calendar_custom_event_id
JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】內部工作會議（不公開）';
GO

INSERT INTO partners_i18n (partner_id, locale, name, content)
SELECT b.id, N'en', N'[Test] Demo Strategic Partner', N'[Test] A strategic partnership to advance local football development together.'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範策略夥伴'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Strategic Partner' ELSE e.name END,
    e.content = CASE WHEN e.content IS NULL OR e.content = N'' OR e.content = z.content THEN N'[Test] A strategic partnership to advance local football development together.' ELSE e.content END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範策略夥伴';
GO

INSERT INTO partners_i18n (partner_id, locale, name, content)
SELECT b.id, N'en', N'[Test] Demo International Partner', N'[Test] International scouting and exchange partnership.'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範國際夥伴'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo International Partner' ELSE e.name END,
    e.content = CASE WHEN e.content IS NULL OR e.content = N'' OR e.content = z.content THEN N'[Test] International scouting and exchange partnership.' ELSE e.content END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範國際夥伴';
GO

INSERT INTO partners_i18n (partner_id, locale, name, content)
SELECT b.id, N'en', N'[Test] Demo Training Partner', N'[Test] Physical and technical training partnership.'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範訓練夥伴'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Training Partner' ELSE e.name END,
    e.content = CASE WHEN e.content IS NULL OR e.content = N'' OR e.content = z.content THEN N'[Test] Physical and technical training partnership.' ELSE e.content END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範訓練夥伴';
GO

INSERT INTO partners_i18n (partner_id, locale, name, content)
SELECT b.id, N'en', N'[Test] Demo Education Partner', N'[Test] Campus and community football promotion partnership.'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範教育夥伴'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Education Partner' ELSE e.name END,
    e.content = CASE WHEN e.content IS NULL OR e.content = N'' OR e.content = z.content THEN N'[Test] Campus and community football promotion partnership.' ELSE e.content END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範教育夥伴';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'[Test] Demo Brand Partner'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範品牌夥伴'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Brand Partner' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範品牌夥伴';
GO

INSERT INTO sponsors_i18n (sponsor_id, locale, name, content)
SELECT b.id, N'en', N'[Test] Demo Main Sponsor', N'[Test] Sponsorship details: for demonstration only. Please replace in the back office before launch.'
FROM sponsors b JOIN sponsors_i18n z ON z.sponsor_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範主贊助商'
  AND NOT EXISTS (SELECT 1 FROM sponsors_i18n e WHERE e.sponsor_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Main Sponsor' ELSE e.name END,
    e.content = CASE WHEN e.content IS NULL OR e.content = N'' OR e.content = z.content THEN N'[Test] Sponsorship details: for demonstration only. Please replace in the back office before launch.' ELSE e.content END
FROM sponsors_i18n e
JOIN sponsors b ON b.id = e.sponsor_id
JOIN sponsors_i18n z ON z.sponsor_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範主贊助商';
GO

INSERT INTO sponsors_i18n (sponsor_id, locale, name, content)
SELECT b.id, N'en', N'[Test] Demo Official Sponsor', N'[Test] Sponsorship details: for demonstration only. Please replace in the back office before launch.'
FROM sponsors b JOIN sponsors_i18n z ON z.sponsor_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範官方贊助商'
  AND NOT EXISTS (SELECT 1 FROM sponsors_i18n e WHERE e.sponsor_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Official Sponsor' ELSE e.name END,
    e.content = CASE WHEN e.content IS NULL OR e.content = N'' OR e.content = z.content THEN N'[Test] Sponsorship details: for demonstration only. Please replace in the back office before launch.' ELSE e.content END
FROM sponsors_i18n e
JOIN sponsors b ON b.id = e.sponsor_id
JOIN sponsors_i18n z ON z.sponsor_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範官方贊助商';
GO

INSERT INTO sponsors_i18n (sponsor_id, locale, name, content)
SELECT b.id, N'en', N'[Test] Demo Supporting Partner', N'[Test] Sponsorship details: for demonstration only. Please replace in the back office before launch.'
FROM sponsors b JOIN sponsors_i18n z ON z.sponsor_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範支持夥伴'
  AND NOT EXISTS (SELECT 1 FROM sponsors_i18n e WHERE e.sponsor_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Supporting Partner' ELSE e.name END,
    e.content = CASE WHEN e.content IS NULL OR e.content = N'' OR e.content = z.content THEN N'[Test] Sponsorship details: for demonstration only. Please replace in the back office before launch.' ELSE e.content END
FROM sponsors_i18n e
JOIN sponsors b ON b.id = e.sponsor_id
JOIN sponsors_i18n z ON z.sponsor_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範支持夥伴';
GO

INSERT INTO sponsor_activations_i18n (sponsor_activation_id, locale, title, result_summary)
SELECT b.id, N'en', N'[Test] Demo sponsor activation: opening match fan day', N'[Test] About 500 attendees on site and 100,000 social media impressions (demo figures).'
FROM sponsor_activations b JOIN sponsor_activations_i18n z ON z.sponsor_activation_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範贊助活動：開幕戰球迷日'
  AND NOT EXISTS (SELECT 1 FROM sponsor_activations_i18n e WHERE e.sponsor_activation_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Demo sponsor activation: opening match fan day' ELSE e.title END,
    e.result_summary = CASE WHEN e.result_summary IS NULL OR e.result_summary = N'' OR e.result_summary = z.result_summary THEN N'[Test] About 500 attendees on site and 100,000 social media impressions (demo figures).' ELSE e.result_summary END
FROM sponsor_activations_i18n e
JOIN sponsor_activations b ON b.id = e.sponsor_activation_id
JOIN sponsor_activations_i18n z ON z.sponsor_activation_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範贊助活動：開幕戰球迷日';
GO

INSERT INTO sponsor_activations_i18n (sponsor_activation_id, locale, title, result_summary)
SELECT b.id, N'en', N'[Test] Demo sponsor activation: youth training experience camp', N'[Test] 60 children took part (demo figures).'
FROM sponsor_activations b JOIN sponsor_activations_i18n z ON z.sponsor_activation_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範贊助活動：青訓體驗營'
  AND NOT EXISTS (SELECT 1 FROM sponsor_activations_i18n e WHERE e.sponsor_activation_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Demo sponsor activation: youth training experience camp' ELSE e.title END,
    e.result_summary = CASE WHEN e.result_summary IS NULL OR e.result_summary = N'' OR e.result_summary = z.result_summary THEN N'[Test] 60 children took part (demo figures).' ELSE e.result_summary END
FROM sponsor_activations_i18n e
JOIN sponsor_activations b ON b.id = e.sponsor_activation_id
JOIN sponsor_activations_i18n z ON z.sponsor_activation_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範贊助活動：青訓體驗營';
GO

INSERT INTO press_resources_i18n (press_resource_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Demo press release', N'[Test] Demo description. This entry has no actual file (placeholder); please upload a file in the back office and then set it to visible.'
FROM press_resources b JOIN press_resources_i18n z ON z.press_resource_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範新聞稿'
  AND NOT EXISTS (SELECT 1 FROM press_resources_i18n e WHERE e.press_resource_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Demo press release' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] Demo description. This entry has no actual file (placeholder); please upload a file in the back office and then set it to visible.' ELSE e.description END
FROM press_resources_i18n e
JOIN press_resources b ON b.id = e.press_resource_id
JOIN press_resources_i18n z ON z.press_resource_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範新聞稿';
GO

INSERT INTO press_resources_i18n (press_resource_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Demo brand kit', N'[Test] Demo description. This entry has no actual file (placeholder); please upload a file in the back office and then set it to visible.'
FROM press_resources b JOIN press_resources_i18n z ON z.press_resource_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範品牌識別包'
  AND NOT EXISTS (SELECT 1 FROM press_resources_i18n e WHERE e.press_resource_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Demo brand kit' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] Demo description. This entry has no actual file (placeholder); please upload a file in the back office and then set it to visible.' ELSE e.description END
FROM press_resources_i18n e
JOIN press_resources b ON b.id = e.press_resource_id
JOIN press_resources_i18n z ON z.press_resource_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範品牌識別包';
GO

INSERT INTO press_resources_i18n (press_resource_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Demo high-resolution images', N'[Test] Demo description. This entry has no actual file (placeholder); please upload a file in the back office and then set it to visible.'
FROM press_resources b JOIN press_resources_i18n z ON z.press_resource_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範高解析圖'
  AND NOT EXISTS (SELECT 1 FROM press_resources_i18n e WHERE e.press_resource_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Demo high-resolution images' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] Demo description. This entry has no actual file (placeholder); please upload a file in the back office and then set it to visible.' ELSE e.description END
FROM press_resources_i18n e
JOIN press_resources b ON b.id = e.press_resource_id
JOIN press_resources_i18n z ON z.press_resource_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.title = N'【測試】示範高解析圖';
GO

INSERT INTO partner_stores_i18n (partner_store_id, locale, name, offer_content)
SELECT b.id, N'en', N'[Test] Demo Gym', N'[Test] One free trial class (draft, not yet listed)'
FROM partner_stores b JOIN partner_stores_i18n z ON z.partner_store_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範健身房'
  AND NOT EXISTS (SELECT 1 FROM partner_stores_i18n e WHERE e.partner_store_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Gym' ELSE e.name END,
    e.offer_content = CASE WHEN e.offer_content IS NULL OR e.offer_content = N'' OR e.offer_content = z.offer_content THEN N'[Test] One free trial class (draft, not yet listed)' ELSE e.offer_content END
FROM partner_stores_i18n e
JOIN partner_stores b ON b.id = e.partner_store_id
JOIN partner_stores_i18n z ON z.partner_store_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範健身房';
GO

INSERT INTO charities_i18n (charity_id, locale, name, intro)
SELECT b.id, N'en', N'[Test] Demo Charity A', N'[Test] This is a demo charity profile.'
FROM charities b JOIN charities_i18n z ON z.charity_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範公益團體甲'
  AND NOT EXISTS (SELECT 1 FROM charities_i18n e WHERE e.charity_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Charity A' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'[Test] This is a demo charity profile.' ELSE e.intro END
FROM charities_i18n e
JOIN charities b ON b.id = e.charity_id
JOIN charities_i18n z ON z.charity_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範公益團體甲';
GO

INSERT INTO charities_i18n (charity_id, locale, name, intro)
SELECT b.id, N'en', N'[Test] Demo Charity B', N'[Test] Another demo charity.'
FROM charities b JOIN charities_i18n z ON z.charity_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範公益團體乙'
  AND NOT EXISTS (SELECT 1 FROM charities_i18n e WHERE e.charity_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo Charity B' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'[Test] Another demo charity.' ELSE e.intro END
FROM charities_i18n e
JOIN charities b ON b.id = e.charity_id
JOIN charities_i18n z ON z.charity_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範公益團體乙';
GO

INSERT INTO charity_programs_i18n (charity_program_id, locale, name, target_audience, donation_content)
SELECT b.id, N'en', N'[Test] Demo charity program: football donations to rural areas', N'[Test] Children in rural areas', N'[Test] 50 footballs and 100 training bibs (demo figures)'
FROM charity_programs b JOIN charity_programs_i18n z ON z.charity_program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範慈善計畫：偏鄉足球捐贈'
  AND NOT EXISTS (SELECT 1 FROM charity_programs_i18n e WHERE e.charity_program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo charity program: football donations to rural areas' ELSE e.name END,
    e.target_audience = CASE WHEN e.target_audience IS NULL OR e.target_audience = N'' OR e.target_audience = z.target_audience THEN N'[Test] Children in rural areas' ELSE e.target_audience END,
    e.donation_content = CASE WHEN e.donation_content IS NULL OR e.donation_content = N'' OR e.donation_content = z.donation_content THEN N'[Test] 50 footballs and 100 training bibs (demo figures)' ELSE e.donation_content END
FROM charity_programs_i18n e
JOIN charity_programs b ON b.id = e.charity_program_id
JOIN charity_programs_i18n z ON z.charity_program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範慈善計畫：偏鄉足球捐贈';
GO

INSERT INTO charity_programs_i18n (charity_program_id, locale, name, target_audience, donation_content)
SELECT b.id, N'en', N'[Test] Demo charity program: charity match', N'[Test] Children in rural areas', N'[Test] 50 footballs and 100 training bibs (demo figures)'
FROM charity_programs b JOIN charity_programs_i18n z ON z.charity_program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範慈善計畫：公益義賽'
  AND NOT EXISTS (SELECT 1 FROM charity_programs_i18n e WHERE e.charity_program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo charity program: charity match' ELSE e.name END,
    e.target_audience = CASE WHEN e.target_audience IS NULL OR e.target_audience = N'' OR e.target_audience = z.target_audience THEN N'[Test] Children in rural areas' ELSE e.target_audience END,
    e.donation_content = CASE WHEN e.donation_content IS NULL OR e.donation_content = N'' OR e.donation_content = z.donation_content THEN N'[Test] 50 footballs and 100 training bibs (demo figures)' ELSE e.donation_content END
FROM charity_programs_i18n e
JOIN charity_programs b ON b.id = e.charity_program_id
JOIN charity_programs_i18n z ON z.charity_program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範慈善計畫：公益義賽';
GO

INSERT INTO charity_programs_i18n (charity_program_id, locale, name, target_audience, donation_content)
SELECT b.id, N'en', N'[Test] Demo charity program (draft)', N'[Test] Children in rural areas', N'[Test] 50 footballs and 100 training bibs (demo figures)'
FROM charity_programs b JOIN charity_programs_i18n z ON z.charity_program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範慈善計畫（草稿）'
  AND NOT EXISTS (SELECT 1 FROM charity_programs_i18n e WHERE e.charity_program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Demo charity program (draft)' ELSE e.name END,
    e.target_audience = CASE WHEN e.target_audience IS NULL OR e.target_audience = N'' OR e.target_audience = z.target_audience THEN N'[Test] Children in rural areas' ELSE e.target_audience END,
    e.donation_content = CASE WHEN e.donation_content IS NULL OR e.donation_content = N'' OR e.donation_content = z.donation_content THEN N'[Test] 50 footballs and 100 training bibs (demo figures)' ELSE e.donation_content END
FROM charity_programs_i18n e
JOIN charity_programs b ON b.id = e.charity_program_id
JOIN charity_programs_i18n z ON z.charity_program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】示範慈善計畫（草稿）';
GO

INSERT INTO impact_metrics_i18n (impact_metric_id, locale, name, unit)
SELECT b.id, N'en', N'[Test] Partner charities', N'orgs'
FROM impact_metrics b JOIN impact_metrics_i18n z ON z.impact_metric_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】合作公益團體數'
  AND NOT EXISTS (SELECT 1 FROM impact_metrics_i18n e WHERE e.impact_metric_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Partner charities' ELSE e.name END,
    e.unit = CASE WHEN e.unit IS NULL OR e.unit = N'' OR e.unit = z.unit THEN N'orgs' ELSE e.unit END
FROM impact_metrics_i18n e
JOIN impact_metrics b ON b.id = e.impact_metric_id
JOIN impact_metrics_i18n z ON z.impact_metric_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】合作公益團體數';
GO

INSERT INTO impact_metrics_i18n (impact_metric_id, locale, name, unit)
SELECT b.id, N'en', N'[Test] Total donation items', N'items'
FROM impact_metrics b JOIN impact_metrics_i18n z ON z.impact_metric_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】累計捐助項次'
  AND NOT EXISTS (SELECT 1 FROM impact_metrics_i18n e WHERE e.impact_metric_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Total donation items' ELSE e.name END,
    e.unit = CASE WHEN e.unit IS NULL OR e.unit = N'' OR e.unit = z.unit THEN N'items' ELSE e.unit END
FROM impact_metrics_i18n e
JOIN impact_metrics b ON b.id = e.impact_metric_id
JOIN impact_metrics_i18n z ON z.impact_metric_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】累計捐助項次';
GO

INSERT INTO impact_metrics_i18n (impact_metric_id, locale, name, unit)
SELECT b.id, N'en', N'[Test] Total donation amount (not public)', N'NTD'
FROM impact_metrics b JOIN impact_metrics_i18n z ON z.impact_metric_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】累計捐助金額（不公開）'
  AND NOT EXISTS (SELECT 1 FROM impact_metrics_i18n e WHERE e.impact_metric_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Total donation amount (not public)' ELSE e.name END,
    e.unit = CASE WHEN e.unit IS NULL OR e.unit = N'' OR e.unit = z.unit THEN N'NTD' ELSE e.unit END
FROM impact_metrics_i18n e
JOIN impact_metrics b ON b.id = e.impact_metric_id
JOIN impact_metrics_i18n z ON z.impact_metric_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.name = N'【測試】累計捐助金額（不公開）';
GO

INSERT INTO impact_records_i18n (impact_record_id, locale, donation_content, location, brief_description)
SELECT b.id, N'en', N'[Test] 50 footballs and 100 training bibs (demo figures)', N'[Test] Demo location: Nantou County', N'[Test] Demo summary.'
FROM impact_records b JOIN impact_records_i18n z ON z.impact_record_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.donation_content = N'【測試】足球 50 顆、訓練背心 100 件（示範數字）'
  AND NOT EXISTS (SELECT 1 FROM impact_records_i18n e WHERE e.impact_record_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.donation_content = CASE WHEN e.donation_content IS NULL OR e.donation_content = N'' OR e.donation_content = z.donation_content THEN N'[Test] 50 footballs and 100 training bibs (demo figures)' ELSE e.donation_content END,
    e.location = CASE WHEN e.location IS NULL OR e.location = N'' OR e.location = z.location THEN N'[Test] Demo location: Nantou County' ELSE e.location END,
    e.brief_description = CASE WHEN e.brief_description IS NULL OR e.brief_description = N'' OR e.brief_description = z.brief_description THEN N'[Test] Demo summary.' ELSE e.brief_description END
FROM impact_records_i18n e
JOIN impact_records b ON b.id = e.impact_record_id
JOIN impact_records_i18n z ON z.impact_record_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.donation_content = N'【測試】足球 50 顆、訓練背心 100 件（示範數字）';
GO

INSERT INTO impact_records_i18n (impact_record_id, locale, donation_content, location)
SELECT b.id, N'en', N'[Test] 5 scholarships (demo figures)', N'[Test] Demo location: Taichung City'
FROM impact_records b JOIN impact_records_i18n z ON z.impact_record_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.donation_content = N'【測試】獎助學金 5 名（示範數字）'
  AND NOT EXISTS (SELECT 1 FROM impact_records_i18n e WHERE e.impact_record_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.donation_content = CASE WHEN e.donation_content IS NULL OR e.donation_content = N'' OR e.donation_content = z.donation_content THEN N'[Test] 5 scholarships (demo figures)' ELSE e.donation_content END,
    e.location = CASE WHEN e.location IS NULL OR e.location = N'' OR e.location = z.location THEN N'[Test] Demo location: Taichung City' ELSE e.location END
FROM impact_records_i18n e
JOIN impact_records b ON b.id = e.impact_record_id
JOIN impact_records_i18n z ON z.impact_record_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND z.donation_content = N'【測試】獎助學金 5 名（示範數字）';
GO

INSERT INTO settings_i18n (setting_id, locale, value)
SELECT b.id, N'en', N'National Second Division champions'
FROM settings b JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.setting_key = N'site.founding_title'
  AND NOT EXISTS (SELECT 1 FROM settings_i18n e WHERE e.setting_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.value = CASE WHEN e.value IS NULL OR e.value = N'' OR e.value = z.value THEN N'National Second Division champions' ELSE e.value END
FROM settings_i18n e
JOIN settings b ON b.id = e.setting_id
JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.setting_key = N'site.founding_title';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC and AS Trenčín deepen youth development partnership, opening a new chapter in Taiwan-Slovakia football exchange'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-08-10-international-000'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC and AS Trenčín deepen youth development partnership, opening a new chapter in Taiwan-Slovakia football exchange' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-08-10-international-000';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 3-0 銘傳大學'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-24-match-001'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 3-0 銘傳大學' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-24-match-001';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 1-2 陽信北競'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-17-match-002'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 1-2 陽信北競' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-17-match-002';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 2-4 南市台鋼'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-10-match-003'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 2-4 南市台鋼' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-10-match-003';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: 陽信北競 Reserves 5-0 Taichung Rock FC Reserves'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-09-match-004'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: 陽信北競 Reserves 5-0 Taichung Rock FC Reserves' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-09-match-004';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: Taichung Rock FC Reserves 0-2 銘傳Desafio'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-03-match-005'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: Taichung Rock FC Reserves 0-2 銘傳Desafio' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-03-match-005';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 1-0 大同足球'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-03-match-006'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 1-0 大同足球' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-05-03-match-006';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 2-2 台電'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-27-match-007'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 2-2 台電' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-27-match-007';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: 桃園國際 1-1 Taichung Rock FC Reserves'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-25-match-008'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: 桃園國際 1-1 Taichung Rock FC Reserves' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-25-match-008';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 台中未來 0-1 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-19-match-009'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 台中未來 0-1 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-19-match-009';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: Taichung Rock FC Reserves 4-1 灣島'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-18-match-010'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: Taichung Rock FC Reserves 4-1 灣島' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-18-match-010';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 新北航源 0-2 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-12-match-011'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 新北航源 0-2 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-12-match-011';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: 高雄先鋒 4-1 Taichung Rock FC Reserves'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-11-match-012'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: 高雄先鋒 4-1 Taichung Rock FC Reserves' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-04-11-match-012';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: Taichung Rock FC Reserves 5-2 新北航源輔大'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-03-22-match-013'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: Taichung Rock FC Reserves 5-2 新北航源輔大' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-03-22-match-013';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 1-1 銘傳大學'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-03-09-match-014'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 1-1 銘傳大學' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-03-09-match-014';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 3-1 陽信北競'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-03-02-match-015'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 3-1 陽信北競' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-03-02-match-015';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Widening international horizons: 5 Taichung Rock FC players called up to train in Italy by 萊尼亞戈'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-02-06-international-016'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Widening international horizons: 5 Taichung Rock FC players called up to train in Italy by 萊尼亞戈' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-02-06-international-016';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC teams up with Subkarma on local charity, donating English books to 潭秀非營利幼兒園'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-01-12-community-017'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC teams up with Subkarma on local charity, donating English books to 潭秀非營利幼兒園' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2026-01-12-community-017';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 0-0 南市台鋼'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-22-match-018'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 0-0 南市台鋼' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-22-match-018';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 0-1 大同足球隊'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-14-match-019'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 0-1 大同足球隊' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-14-match-019';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 台灣電力 1-2 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-14-match-020'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 台灣電力 1-2 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-14-match-020';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 台灣電力 1-2 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-09-match-021'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 台灣電力 1-2 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-09-match-021';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 台灣電力 1-2 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-07-match-022'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 台灣電力 1-2 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-07-match-022';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 台中FUTURO 4-0 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-01-match-023'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 台中FUTURO 4-0 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-12-01-match-023';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: 銘傳大學Desafio 1-3 Taichung Rock FC Reserves'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-24-match-024'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: 銘傳大學Desafio 1-3 Taichung Rock FC Reserves' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-24-match-024';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 新北航源 3-0 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-24-match-025'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 新北航源 3-0 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-24-match-025';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'First step in the Hellas Verona partnership: Taichung Rock FC players depart for training in Italy'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-04-international-026'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'First step in the Hellas Verona partnership: Taichung Rock FC players depart for training in Italy' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-04-international-026';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'陳曉明 appointed Technical Advisor of Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-03-club-027'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'陳曉明 appointed Technical Advisor of Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-03-club-027';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 銘傳大學 0-1 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-02-match-028'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 銘傳大學 0-1 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-02-match-028';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: Taichung Rock FC Reserves 2-0 桃園國際'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-01-match-029'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: Taichung Rock FC Reserves 2-0 桃園國際' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-11-01-match-029';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: 灣島 2-7 Taichung Rock FC Reserves'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-10-26-match-030'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: 灣島 2-7 Taichung Rock FC Reserves' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-10-26-match-030';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 陽信北競 2-2 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-10-26-match-031'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 陽信北競 2-2 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-10-26-match-031';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 南市台鋼 3-3 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-09-28-match-032'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 南市台鋼 3-3 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-09-28-match-032';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: 大同足球 2-2 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-09-21-match-033'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: 大同足球 2-2 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-09-21-match-033';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: 新北航源輔大 1-0 Taichung Rock FC Reserves'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-09-20-match-034'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: 新北航源輔大 1-0 Taichung Rock FC Reserves' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-09-20-match-034';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 0-2 台灣電力'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-09-14-match-035'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 0-2 台灣電力' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-09-14-match-035';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 0-1 台中未來'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-08-24-match-036'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 0-1 台中未來' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-08-24-match-036';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Enterprise Premier League: Taichung Rock FC 2-1 新北航源'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-08-18-match-037'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Enterprise Premier League: Taichung Rock FC 2-1 新北航源' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-08-18-match-037';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC International Cup: Taichung Rock FC vs 東方'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-31-intcup-038'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC International Cup: Taichung Rock FC vs 東方' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-31-intcup-038';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC International Cup: 紅白艾倫 vs 台中Futuro'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-31-intcup-039'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC International Cup: 紅白艾倫 vs 台中Futuro' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-31-intcup-039';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC to sign memorandum of cooperation with Serie A club Hellas Verona to advance Taiwan-Italy football exchange'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-30-international-040'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC to sign memorandum of cooperation with Serie A club Hellas Verona to advance Taiwan-Italy football exchange' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-30-international-040';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC International Cup: Taichung Rock FC vs 紅白艾倫'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-29-intcup-041'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC International Cup: Taichung Rock FC vs 紅白艾倫' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-29-intcup-041';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC International Cup: 東方 vs 台中Futuro'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-29-intcup-042'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC International Cup: 東方 vs 台中Futuro' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-29-intcup-042';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC signs memorandum of understanding with Germany''s Rot Weiss Ahlen'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-27-international-043'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC signs memorandum of understanding with Germany''s Rot Weiss Ahlen' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-27-international-043';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC International Cup: Taichung Rock FC vs 台中未來'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-27-intcup-044'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC International Cup: Taichung Rock FC vs 台中未來' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-27-intcup-044';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC International Cup: 紅白艾倫 vs 東方'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-27-intcup-045'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC International Cup: 紅白艾倫 vs 東方' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-27-intcup-045';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'2025 Taichung Rock FC International Cup press conference'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-25-club-046'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2025 Taichung Rock FC International Cup press conference' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-25-club-046';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung City Government Sports Bureau flag presentation ceremony'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-21-community-047'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung City Government Sports Bureau flag presentation ceremony' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-21-community-047';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'2025 Taichung Rock FC International Cup kicks off'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-12-club-048'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2025 Taichung Rock FC International Cup kicks off' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-12-club-048';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC and 龜記茗品 continue their partnership'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-08-club-049'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC and 龜記茗品 continue their partnership' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-07-08-club-049';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC attends the graduation ceremony at 南投縣雙龍國小'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-06-11-community-050'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC attends the graduation ceremony at 南投縣雙龍國小' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-06-11-community-050';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup quarter-final: 台灣電力 1-1 (penalties 4-2) Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-17-match-051'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup quarter-final: 台灣電力 1-1 (penalties 4-2) Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-17-match-051';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup: Taichung Rock FC 2-2 高大國光'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-13-match-052'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup: Taichung Rock FC 2-2 高大國光' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-13-match-052';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup: Taichung Rock FC Reserves 8-1 銘傳B'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-13-match-053'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup: Taichung Rock FC Reserves 8-1 銘傳B' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-13-match-053';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup: Taichung Rock FC 1-5 南市台鋼藍'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-04-match-054'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup: Taichung Rock FC 1-5 南市台鋼藍' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-04-match-054';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup: Taichung Rock FC Reserves 1-2 台中FUTURO'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-04-match-055'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup: Taichung Rock FC Reserves 1-2 台中FUTURO' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-04-match-055';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'2025 Taichung Rock FC Cup football invitational'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-03-camps-056'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2025 Taichung Rock FC Cup football invitational' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-05-03-camps-056';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup: 桃園國際灰 0-7 Taichung Rock FC Reserves'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-29-match-057'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup: 桃園國際灰 0-7 Taichung Rock FC Reserves' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-29-match-057';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup: 陽信北競 Reserves 2-3 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-28-match-058'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup: 陽信北競 Reserves 2-3 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-28-match-058';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup: Taichung Rock FC 2-1 新北輔大'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-12-match-059'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup: Taichung Rock FC 2-1 新北輔大' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-12-match-059';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'President''s Cup: Taichung Rock FC Reserves 1-5 南市台鋼綠'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-12-match-060'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'President''s Cup: Taichung Rock FC Reserves 1-5 南市台鋼綠' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-12-match-060';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'周宇杰 joins Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-11-club-061'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'周宇杰 joins Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-11-club-061';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'廖奕盛 joins Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-11-club-062'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'廖奕盛 joins Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-11-club-062';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Germany-based 王義友 joins Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-11-club-063'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Germany-based 王義友 joins Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-11-club-063';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'孫恩祈 invited to stay on in Spain until the end of the season'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-08-international-064'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'孫恩祈 invited to stay on in Spain until the end of the season' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-08-international-064';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Club friendly: 陽信北競 2-1 Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-07-match-065'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Club friendly: 陽信北競 2-1 Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-07-match-065';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'梁顥騰 completes his training trip in Spain, deeply inspired'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-07-international-066'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'梁顥騰 completes his training trip in Spain, deeply inspired' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-07-international-066';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Fourth warm-up match: 陽信北競 vs Taichung Rock FC'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-03-match-067'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Fourth warm-up match: 陽信北競 vs Taichung Rock FC' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-03-match-067';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Two Taichung Rock FC squads set out for the 2025 National President''s Cup'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-02-match-068'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Two Taichung Rock FC squads set out for the 2025 National President''s Cup' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-04-02-match-068';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Warm-up match: Taichung Rock FC 1-3 陽信北競'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-29-match-069'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Warm-up match: Taichung Rock FC 1-3 陽信北競' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-29-match-069';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Third warm-up match: Taichung Rock FC vs 陽信北競'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-22-match-070'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Third warm-up match: Taichung Rock FC vs 陽信北競' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-22-match-070';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'高冠宇 called up to the Chinese Taipei national team'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-17-club-071'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'高冠宇 called up to the Chinese Taipei national team' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-17-club-071';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Warm-up match: Taichung Rock FC vs 台中Futuro'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-15-match-072'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Warm-up match: Taichung Rock FC vs 台中Futuro' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-15-match-072';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC take on 台中Futuro in a warm-up match'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-13-match-073'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC take on 台中Futuro in a warm-up match' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-13-match-073';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Two players join training at RC Alcobendas in Spain'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-04-international-074'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Two players join training at RC Alcobendas in Spain' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-03-04-international-074';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Four players join training with the Tokyo University of Agriculture football team'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-02-28-international-075'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Four players join training with the Tokyo University of Agriculture football team' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-02-28-international-075';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Japan training camp wrap-up'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-02-19-international-076'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Japan training camp wrap-up' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-02-19-international-076';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'楊朝景 transfers to Hong Kong Premier League club 九龍城'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-02-04-international-077'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'楊朝景 transfers to Hong Kong Premier League club 九龍城' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-02-04-international-077';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC recognised by the Taichung City Government Sports Bureau for cooperation and naming rights'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-01-07-club-078'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC recognised by the Taichung City Government Sports Bureau for cooperation and naming rights' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2025-01-07-club-078';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC conditionally passes top-tier club certification'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2024-12-18-club-079'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC conditionally passes top-tier club certification' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2024-12-18-club-079';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Coach 林 wins Best Coach award, 楊朝景 wins Golden Boot'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2024-12-18-club-080'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Coach 林 wins Best Coach award, 楊朝景 wins Golden Boot' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2024-12-18-club-080';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'National Second Division: Taichung Rock FC 9-0 銘傳大學Desafio'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2024-12-07-match-081'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'National Second Division: Taichung Rock FC 9-0 銘傳大學Desafio' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2024-12-07-match-081';
GO

INSERT INTO articles_i18n (article_id, locale, title)
SELECT b.id, N'en', N'Taichung Rock FC reaches cooperation agreement with RC Alcobendas'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2024-11-05-international-082'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Rock FC reaches cooperation agreement with RC Alcobendas' ELSE e.title END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND b.slug = N'2024-11-05-international-082';
GO

INSERT INTO competitions_i18n (competition_id, locale, name)
SELECT b.id, N'en', N'Taiwan Mulan Football League'
FROM competitions b JOIN competitions_i18n z ON z.competition_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'台灣木蘭女子足球聯賽'
  AND NOT EXISTS (SELECT 1 FROM competitions_i18n e WHERE e.competition_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Taiwan Mulan Football League' ELSE e.name END
FROM competitions_i18n e
JOIN competitions b ON b.id = e.competition_id
JOIN competitions_i18n z ON z.competition_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'台灣木蘭女子足球聯賽';
GO

INSERT INTO competitions_i18n (competition_id, locale, name)
SELECT b.id, N'en', N'2025 National President''s Cup Football Championship'
FROM competitions b JOIN competitions_i18n z ON z.competition_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'2025全國總統盃足球錦標賽'
  AND NOT EXISTS (SELECT 1 FROM competitions_i18n e WHERE e.competition_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'2025 National President''s Cup Football Championship' ELSE e.name END
FROM competitions_i18n e
JOIN competitions b ON b.id = e.competition_id
JOIN competitions_i18n z ON z.competition_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'2025全國總統盃足球錦標賽';
GO

INSERT INTO venues_i18n (venue_id, locale, name)
SELECT b.id, N'en', N'Taichung Fengyuan Stadium'
FROM venues b JOIN venues_i18n z ON z.venue_id = b.id AND z.locale = N'zh-Hant'
WHERE z.name = N'台中市立豐原體育場'
  AND NOT EXISTS (SELECT 1 FROM venues_i18n e WHERE e.venue_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Taichung Fengyuan Stadium' ELSE e.name END
FROM venues_i18n e
JOIN venues b ON b.id = e.venue_id
JOIN venues_i18n z ON z.venue_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.name = N'台中市立豐原體育場';
GO

INSERT INTO venues_i18n (venue_id, locale, name)
SELECT b.id, N'en', N'Taichung Beitun Taiyuan Football Field'
FROM venues b JOIN venues_i18n z ON z.venue_id = b.id AND z.locale = N'zh-Hant'
WHERE z.name = N'台中北屯太原足球場'
  AND NOT EXISTS (SELECT 1 FROM venues_i18n e WHERE e.venue_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Taichung Beitun Taiyuan Football Field' ELSE e.name END
FROM venues_i18n e
JOIN venues b ON b.id = e.venue_id
JOIN venues_i18n z ON z.venue_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND z.name = N'台中北屯太原足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Taichung Beitun Taiyuan Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'台中太原足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Taichung Beitun Taiyuan Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'台中太原足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Taichung Beitun Taiyuan Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'臺中太原足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Taichung Beitun Taiyuan Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'臺中太原足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Taichung Beitun Taiyuan Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'台中北屯太原足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Taichung Beitun Taiyuan Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'台中北屯太原足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Taichung Xitun Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'台中西屯足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Taichung Xitun Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'台中西屯足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Kaohsiung Nanzih Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'高雄楠梓足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Kaohsiung Nanzih Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'高雄楠梓足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Kaohsiung Nanzih Football Field'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'高雄市立楠梓足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Kaohsiung Nanzih Football Field' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'高雄市立楠梓足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Fu Jen Catholic University Football Field, New Taipei'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'新北輔仁大學足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Fu Jen Catholic University Football Field, New Taipei' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'新北輔仁大學足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Ming Chuan University Football Field, Guishan, Taoyuan'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'桃園龜山銘傳大學足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Ming Chuan University Football Field, Guishan, Taoyuan' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'桃園龜山銘傳大學足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Meilun Junior High School Football Field, Hualien'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'花蓮美崙國中足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Meilun Junior High School Football Field, Hualien' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'花蓮美崙國中足球場';
GO

INSERT INTO matches_i18n (match_id, locale, venue)
SELECT b.id, N'en', N'Qingpu Football Field, Taoyuan'
FROM matches b JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'桃園青埔足球場'
  AND NOT EXISTS (SELECT 1 FROM matches_i18n e WHERE e.match_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.venue = CASE WHEN e.venue IS NULL OR e.venue = N'' OR e.venue = z.venue THEN N'Qingpu Football Field, Taoyuan' ELSE e.venue END
FROM matches_i18n e
JOIN matches b ON b.id = e.match_id
JOIN matches_i18n z ON z.match_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.venue = N'桃園青埔足球場';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'教練';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Head Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'總教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Head Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'總教練';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Goalkeeper Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'守門教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Goalkeeper Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'守門教練';
GO

INSERT INTO staff_i18n (staff_id, locale, title)
SELECT b.id, N'en', N'Athletic Trainer and Fitness Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'防護員兼體能教練'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Athletic Trainer and Fitness Coach' ELSE e.title END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'防護員兼體能教練';
GO

INSERT INTO staff_i18n (staff_id, locale, bio)
SELECT b.id, N'en', N'2008  U-19 Asian Cup qualifiers, Chinese Taipei women''s national team: Head Coach
2011  Shenzhen Universiade, Chinese Taipei women''s national team: Head Coach
2013  East Asian Games, Chinese Taipei women''s football team: Coach
2013  Kazan Universiade, Chinese Taipei women''s national team: Head Coach
2014  East Asian Cup, Chinese Taipei women''s football team: Coach
2014  Incheon Asian Games (Korea), Chinese Taipei women''s national team: Coach
2015  Gwangju Universiade, Chinese Taipei women''s national team: Coach
2016  Rio Olympics, Chinese Taipei women''s national team: Coach
2014-2016  Taichung Blue Whale women''s football team, First Team: Head Coach
2017  Taichung Blue Whale women''s football team, First Team: Coach
2018-2024  Taichung Blue Whale women''s football team, First Team: Head Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'呂桂花'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.bio = CASE WHEN e.bio IS NULL OR e.bio = N'' OR e.bio = z.bio THEN N'2008  U-19 Asian Cup qualifiers, Chinese Taipei women''s national team: Head Coach
2011  Shenzhen Universiade, Chinese Taipei women''s national team: Head Coach
2013  East Asian Games, Chinese Taipei women''s football team: Coach
2013  Kazan Universiade, Chinese Taipei women''s national team: Head Coach
2014  East Asian Cup, Chinese Taipei women''s football team: Coach
2014  Incheon Asian Games (Korea), Chinese Taipei women''s national team: Coach
2015  Gwangju Universiade, Chinese Taipei women''s national team: Coach
2016  Rio Olympics, Chinese Taipei women''s national team: Coach
2014-2016  Taichung Blue Whale women''s football team, First Team: Head Coach
2017  Taichung Blue Whale women''s football team, First Team: Coach
2018-2024  Taichung Blue Whale women''s football team, First Team: Head Coach' ELSE e.bio END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'呂桂花';
GO

INSERT INTO staff_i18n (staff_id, locale, bio)
SELECT b.id, N'en', N'2021-2023  Taichung Blue Whale: Coach
2022-2023  Taichung Blue Whale U15 girls'' team: Coach
2022-2024  Chinese Taipei women''s national team: Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'李彥廷'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.bio = CASE WHEN e.bio IS NULL OR e.bio = N'' OR e.bio = z.bio THEN N'2021-2023  Taichung Blue Whale: Coach
2022-2023  Taichung Blue Whale U15 girls'' team: Coach
2022-2024  Chinese Taipei women''s national team: Coach' ELSE e.bio END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'李彥廷';
GO

INSERT INTO staff_i18n (staff_id, locale, bio)
SELECT b.id, N'en', N'2018  高雄陽信 women''s football team: Coach
2016-2022  五權國民中學 girls'' football team: Head Coach
2019-2024  Taichung Blue Whale women''s football team, First Team: Coach
2022  Taichung Blue Whale Football Club U15 youth team: Head Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'鄭雅薰'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.bio = CASE WHEN e.bio IS NULL OR e.bio = N'' OR e.bio = z.bio THEN N'2018  高雄陽信 women''s football team: Coach
2016-2022  五權國民中學 girls'' football team: Head Coach
2019-2024  Taichung Blue Whale women''s football team, First Team: Coach
2022  Taichung Blue Whale Football Club U15 youth team: Head Coach' ELSE e.bio END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'鄭雅薰';
GO

INSERT INTO staff_i18n (staff_id, locale, bio)
SELECT b.id, N'en', N'2019-2024  Taichung Blue Whale women''s football team: Goalkeeper Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'張博翔'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.bio = CASE WHEN e.bio IS NULL OR e.bio = N'' OR e.bio = z.bio THEN N'2019-2024  Taichung Blue Whale women''s football team: Goalkeeper Coach' ELSE e.bio END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'張博翔';
GO

INSERT INTO staff_i18n (staff_id, locale, bio)
SELECT b.id, N'en', N'2023-2024  Taichung Blue Whale women''s football team: Athletic Trainer and Fitness Coach'
FROM staff b JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'邱毓芳'
  AND NOT EXISTS (SELECT 1 FROM staff_i18n e WHERE e.staff_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.bio = CASE WHEN e.bio IS NULL OR e.bio = N'' OR e.bio = z.bio THEN N'2023-2024  Taichung Blue Whale women''s football team: Athletic Trainer and Fitness Coach' ELSE e.bio END
FROM staff_i18n e
JOIN staff b ON b.id = e.staff_id
JOIN staff_i18n z ON z.staff_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'邱毓芳';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2014', N'1. Formed the Taichung Blue Whale women''s football team to compete in the Mulan League
2. Took part in the 1st Taiwan Mulan Football League
3. 1,200 Facebook followers
4. Small artificial-turf football pitch completed at National Taiwan Sport University'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2014'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2014' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Formed the Taichung Blue Whale women''s football team to compete in the Mulan League
2. Took part in the 1st Taiwan Mulan Football League
3. 1,200 Facebook followers
4. Small artificial-turf football pitch completed at National Taiwan Sport University' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2014';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2015', N'1. Took part in the 2nd Taiwan Mulan Football League
2. Helped set up the girls'' football team at 台中市五權國中
3. Ran a D-licence coaching course'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2015'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2015' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 2nd Taiwan Mulan Football League
2. Helped set up the girls'' football team at 台中市五權國中
3. Ran a D-licence coaching course' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2015';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2016', N'1. Took part in the 3rd Taiwan Mulan Football League
2. Established the central Taiwan elite women''s training centre
3. Hosted the AFC Women''s Football Festival for the first time'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2016'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2016' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 3rd Taiwan Mulan Football League
2. Established the central Taiwan elite women''s training centre
3. Hosted the AFC Women''s Football Festival for the first time' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2016';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2017', N'1. Took part in the 3rd Taiwan Mulan Football League
2. Held the 1st Blue Whale Cup football tournament
3. Elite Women''s Football Training Centre renamed the Central Training Centre
4. Appointed JFA S-licence coach 堀野博幸 as First Team head coach
5. Held a regional coaching workshop
6. Taichung Beitun Taiyuan Football Field opened
7. First Taiwan Mulan Football League title in club history
8. Facebook followers reached 6,500'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2017'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2017' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 3rd Taiwan Mulan Football League
2. Held the 1st Blue Whale Cup football tournament
3. Elite Women''s Football Training Centre renamed the Central Training Centre
4. Appointed JFA S-licence coach 堀野博幸 as First Team head coach
5. Held a regional coaching workshop
6. Taichung Beitun Taiyuan Football Field opened
7. First Taiwan Mulan Football League title in club history
8. Facebook followers reached 6,500' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2017';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2018', N'1. Took part in the 4th Taiwan Mulan Football League
2. Founded the Taichung Blue Whale football school
3. Central Training Centre renamed the Blue Whale Central Football Training Centre
4. First professional player 包欣玄 joined Taichung Blue Whale
5. Helped 台中市惠文高中 set up a women''s football team
6. Second Taiwan Mulan Football League title in club history'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2018'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2018' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 4th Taiwan Mulan Football League
2. Founded the Taichung Blue Whale football school
3. Central Training Centre renamed the Blue Whale Central Football Training Centre
4. First professional player 包欣玄 joined Taichung Blue Whale
5. Helped 台中市惠文高中 set up a women''s football team
6. Second Taiwan Mulan Football League title in club history' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2018';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2019', N'1. Took part in the 5th Taiwan Mulan Football League
2. Goalkeeper Tsai Ming-Jung moved abroad to Japan
3. First Japanese player Tanaka Maho joined
4. Campaigned for the construction of the Taichung football park
5. Formed an esports squad to compete in the PES 2020 World Cup
6. Third Taiwan Mulan Football League title in club history
7. First time the league held Blue Whale home-match ticket sales
8. Passed the AFC Club Licence certification
9. Head coach Lu Kuei-Hua won the AFC 2019 Grassroots Leader Award
10. Themed match days held at home throughout the whole year for the first time
11. Founded the Blue Whale Girls cheerleading squad
12. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2019'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2019' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 5th Taiwan Mulan Football League
2. Goalkeeper Tsai Ming-Jung moved abroad to Japan
3. First Japanese player Tanaka Maho joined
4. Campaigned for the construction of the Taichung football park
5. Formed an esports squad to compete in the PES 2020 World Cup
6. Third Taiwan Mulan Football League title in club history
7. First time the league held Blue Whale home-match ticket sales
8. Passed the AFC Club Licence certification
9. Head coach Lu Kuei-Hua won the AFC 2019 Grassroots Leader Award
10. Themed match days held at home throughout the whole year for the first time
11. Founded the Blue Whale Girls cheerleading squad
12. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2019';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2020', N'1. Took part in the 6th Taiwan Mulan Football League
2. First Hong Kong player 吳卓蔚 joined
3. First US player 瑪芮兒 joined
4. Goalkeeper Cheng Ssu-Yu moved abroad to Japan
5. Player 蘇育萱 moved abroad to Japan
6. Blue Whale home-match ticket sales held in the league
7. Taichung Blue Whale U15 girls'' team took part in the 1st Taiwan Youth League
8. First Taiwan Mulan Football League runners-up finish in club history
9. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2020'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2020' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 6th Taiwan Mulan Football League
2. First Hong Kong player 吳卓蔚 joined
3. First US player 瑪芮兒 joined
4. Goalkeeper Cheng Ssu-Yu moved abroad to Japan
5. Player 蘇育萱 moved abroad to Japan
6. Blue Whale home-match ticket sales held in the league
7. Taichung Blue Whale U15 girls'' team took part in the 1st Taiwan Youth League
8. First Taiwan Mulan Football League runners-up finish in club history
9. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2020';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2021', N'1. Took part in the 8th Taiwan Mulan Football League
2. Second Japanese player 日高偉織 joined
3. First Thai player Sornsai Pitsamai joined
4. First Thai goalkeeper 納塔魯亞牧塔納維奇 joined
5. Fourth Taiwan Mulan Football League title in club history
6. First Taiwan Mulan League Cup (MLC) title in club history
7. Taichung Blue Whale U15 girls'' team took part in the 2nd Taiwan Youth League
8. Taichung Blue Whale U18 girls'' team took part in the 2nd Taiwan Youth League
9. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2021'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2021' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 8th Taiwan Mulan Football League
2. Second Japanese player 日高偉織 joined
3. First Thai player Sornsai Pitsamai joined
4. First Thai goalkeeper 納塔魯亞牧塔納維奇 joined
5. Fourth Taiwan Mulan Football League title in club history
6. First Taiwan Mulan League Cup (MLC) title in club history
7. Taichung Blue Whale U15 girls'' team took part in the 2nd Taiwan Youth League
8. Taichung Blue Whale U18 girls'' team took part in the 2nd Taiwan Youth League
9. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2021';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2022', N'1. Took part in the 9th Taiwan Mulan Football League
2. Represented Taiwan at the AFC Women''s Club Championship (Thailand)
3. Third Thai player in club history, Intamee Silawan, joined
4. Held the first top-level football opening match, a success amid the pandemic
5. Taichung Blue Whale U15 girls'' team took part in the 3rd Taiwan Youth League
6. Taichung Blue Whale U18 girls'' team took part in the 3rd Taiwan Youth League
7. Taichung Blue Whale U15 won the first Taiwan Youth League U15 girls'' title
8. Second Taiwan Mulan Football League runners-up finish in club history
9. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2022'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2022' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 9th Taiwan Mulan Football League
2. Represented Taiwan at the AFC Women''s Club Championship (Thailand)
3. Third Thai player in club history, Intamee Silawan, joined
4. Held the first top-level football opening match, a success amid the pandemic
5. Taichung Blue Whale U15 girls'' team took part in the 3rd Taiwan Youth League
6. Taichung Blue Whale U18 girls'' team took part in the 3rd Taiwan Youth League
7. Taichung Blue Whale U15 won the first Taiwan Youth League U15 girls'' title
8. Second Taiwan Mulan Football League runners-up finish in club history
9. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2022';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2023', N'1. Took part in the 10th Taiwan Mulan Football League
2. Groundbreaking of the Taichung football park, with the club invited to the ceremony
3. Player 蘇育萱 moved abroad to China
4. Fourth Thai player in club history, 席菲拉萬茵樂敏, joined
5. Fifth Thai player in club history, Saowalak Peng-ngam, joined
6. Facebook followers reached 16,500
7. Fifth Taiwan Mulan Football League title in club history
8. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2023'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2023' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 10th Taiwan Mulan Football League
2. Groundbreaking of the Taichung football park, with the club invited to the ceremony
3. Player 蘇育萱 moved abroad to China
4. Fourth Thai player in club history, 席菲拉萬茵樂敏, joined
5. Fifth Thai player in club history, Saowalak Peng-ngam, joined
6. Facebook followers reached 16,500
7. Fifth Taiwan Mulan Football League title in club history
8. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2023';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2024', N'1. Took part in the 11th Taiwan Mulan Football League
2. Founded the Taichung Blue Whale U10 girls'' team
3. Taichung Blue Whale U10 girls'' team took part in the Taichung Mayor''s Cup for the first time
4. Invited to the Yangxin Cup International Invitational and won the title
5. Represented Taiwan in the 2024/25 AFC Women''s Champions League and advanced from the group stage
6. Third Taiwan Mulan Football League runners-up finish in club history
7. Sixth Thai player in club history and second Thai goalkeeper, Waraporn Boonsing, joined
8. Saowalak Peng-ngam, the club''s second foreign player, won the Taiwan Mulan Football League Golden Boot of the year
9. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2024'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2024' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 11th Taiwan Mulan Football League
2. Founded the Taichung Blue Whale U10 girls'' team
3. Taichung Blue Whale U10 girls'' team took part in the Taichung Mayor''s Cup for the first time
4. Invited to the Yangxin Cup International Invitational and won the title
5. Represented Taiwan in the 2024/25 AFC Women''s Champions League and advanced from the group stage
6. Third Taiwan Mulan Football League runners-up finish in club history
7. Sixth Thai player in club history and second Thai goalkeeper, Waraporn Boonsing, joined
8. Saowalak Peng-ngam, the club''s second foreign player, won the Taiwan Mulan Football League Golden Boot of the year
9. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2024';
GO

INSERT INTO milestones_i18n (milestone_id, locale, title, description)
SELECT b.id, N'en', N'2025', N'1. Took part in the 12th Taiwan Mulan Football League
2. Represented Taiwan in the 2024-25 AFC Women''s Champions League quarter-final stage and finished among the top 8 in Asia
3. Runners-up at the 2025 National President''s Cup Football Championship
4. Seventh Thai player in club history and third goalkeeper, 邱瑪尼-通蒙戈, joined
5. Second Thai player 冼仲意 joined
6. Head coach Lu Kuei-Hua nominated for the AFC award for Asia''s best women''s team coach
7. Jersey carried the name of a charitable organisation for the first time: 台中惠明盲校
8. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme
9. First interview with a British world-football magazine'
FROM milestones b JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2025'
  AND NOT EXISTS (SELECT 1 FROM milestones_i18n e WHERE e.milestone_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2025' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'1. Took part in the 12th Taiwan Mulan Football League
2. Represented Taiwan in the 2024-25 AFC Women''s Champions League quarter-final stage and finished among the top 8 in Asia
3. Runners-up at the 2025 National President''s Cup Football Championship
4. Seventh Thai player in club history and third goalkeeper, 邱瑪尼-通蒙戈, joined
5. Second Thai player 冼仲意 joined
6. Head coach Lu Kuei-Hua nominated for the AFC award for Asia''s best women''s team coach
7. Jersey carried the name of a charitable organisation for the first time: 台中惠明盲校
8. Took on the Sport i Taiwan 2.0 sports hotspot promotion programme
9. First interview with a British world-football magazine' ELSE e.description END
FROM milestones_i18n e
JOIN milestones b ON b.id = e.milestone_id
JOIN milestones_i18n z ON z.milestone_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2025';
GO

INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url)
SELECT b.id, N'en', N'Taichung Blue Whale rides the waves towards the open ocean', N'Taichung Blue Whale Women''s Football Club · Founded on 12 April 2014 · Five-time Taiwan Mulan Football League champions', N'[Test] Carousel image description (image not yet uploaded)', N'About Taichung Blue Whale', N'/en/about/', N'View schedule', N'/en/schedule/'
FROM banners b JOIN banners_i18n z ON z.banner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'航向世界的藍鯨'
  AND NOT EXISTS (SELECT 1 FROM banners_i18n e WHERE e.banner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'Taichung Blue Whale rides the waves towards the open ocean' ELSE e.title END,
    e.subtitle = CASE WHEN e.subtitle IS NULL OR e.subtitle = N'' OR e.subtitle = z.subtitle THEN N'Taichung Blue Whale Women''s Football Club · Founded on 12 April 2014 · Five-time Taiwan Mulan Football League champions' ELSE e.subtitle END,
    e.image_alt = CASE WHEN e.image_alt IS NULL OR e.image_alt = N'' OR e.image_alt = z.image_alt THEN N'[Test] Carousel image description (image not yet uploaded)' ELSE e.image_alt END,
    e.cta_1_label = CASE WHEN e.cta_1_label IS NULL OR e.cta_1_label = N'' OR e.cta_1_label = z.cta_1_label THEN N'About Taichung Blue Whale' ELSE e.cta_1_label END,
    e.cta_1_url = CASE WHEN e.cta_1_url IS NULL OR e.cta_1_url = N'' OR e.cta_1_url = z.cta_1_url THEN N'/en/about/' ELSE e.cta_1_url END,
    e.cta_2_label = CASE WHEN e.cta_2_label IS NULL OR e.cta_2_label = N'' OR e.cta_2_label = z.cta_2_label THEN N'View schedule' ELSE e.cta_2_label END,
    e.cta_2_url = CASE WHEN e.cta_2_url IS NULL OR e.cta_2_url = N'' OR e.cta_2_url = z.cta_2_url THEN N'/en/schedule/' ELSE e.cta_2_url END
FROM banners_i18n e
JOIN banners b ON b.id = e.banner_id
JOIN banners_i18n z ON z.banner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'航向世界的藍鯨';
GO

INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url)
SELECT b.id, N'en', N'[Test] Second carousel slide title', N'[Test] This is test content. Please replace it in the back office before launch.', N'[Test] Carousel image description (image not yet uploaded)', N'Join as a Player', N'/en/join/player/'
FROM banners b JOIN banners_i18n z ON z.banner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'【測試】第二張輪播標題'
  AND NOT EXISTS (SELECT 1 FROM banners_i18n e WHERE e.banner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Second carousel slide title' ELSE e.title END,
    e.subtitle = CASE WHEN e.subtitle IS NULL OR e.subtitle = N'' OR e.subtitle = z.subtitle THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.subtitle END,
    e.image_alt = CASE WHEN e.image_alt IS NULL OR e.image_alt = N'' OR e.image_alt = z.image_alt THEN N'[Test] Carousel image description (image not yet uploaded)' ELSE e.image_alt END,
    e.cta_1_label = CASE WHEN e.cta_1_label IS NULL OR e.cta_1_label = N'' OR e.cta_1_label = z.cta_1_label THEN N'Join as a Player' ELSE e.cta_1_label END,
    e.cta_1_url = CASE WHEN e.cta_1_url IS NULL OR e.cta_1_url = N'' OR e.cta_1_url = z.cta_1_url THEN N'/en/join/player/' ELSE e.cta_1_url END
FROM banners_i18n e
JOIN banners b ON b.id = e.banner_id
JOIN banners_i18n z ON z.banner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'【測試】第二張輪播標題';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Our Story | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club', N'Taichung Blue Whale Women''s Football Club was founded in Taichung in 2014 under the Taichung City Women''s Football Association. Learn about the team''s positioning and founding purpose.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/our-story'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Our Story | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Taichung Blue Whale Women''s Football Club was founded in Taichung in 2014 under the Taichung City Women''s Football Association. Learn about the team''s positioning and founding purpose.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/our-story';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Development Vision | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club', N'The development vision of Taichung Blue Whale Women''s Football Club: endless exploration, resilience that fears no difficulty, a more refined attitude, the most genuine impact and a deeper purpose.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/vision-mission'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Development Vision | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'The development vision of Taichung Blue Whale Women''s Football Club: endless exploration, resilience that fears no difficulty, a more refined attitude, the most genuine impact and a deeper purpose.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/vision-mission';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Club Slogan and Training Spirit | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club', N'The club slogan and training spirit of Taichung Blue Whale Women''s Football Club, and the design concept behind the blue whale on the club crest.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/philosophy'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Club Slogan and Training Spirit | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'The club slogan and training spirit of Taichung Blue Whale Women''s Football Club, and the design concept behind the blue whale on the club crest.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/philosophy';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Governance | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club', N'Governance information and a download area for the public documents of Taichung Blue Whale Women''s Football Club. Content is updated regularly.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/governance'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Governance | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Governance information and a download area for the public documents of Taichung Blue Whale Women''s Football Club. Content is updated regularly.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/governance';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Club History | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club', N'A year-by-year history of Taichung Blue Whale Women''s Football Club from 2014 to 2025, compiled from content published on the former official website.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/history'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Club History | About Taichung Blue Whale | Taichung Blue Whale Women''s Football Club' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'A year-by-year history of Taichung Blue Whale Women''s Football Club from 2014 to 2025, compiled from content published on the former official website.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'about/history';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Player Development Priorities | Taichung Blue Whale', N'Player development priorities at Taichung Blue Whale: technical and tactical analysis, physical fitness training, match reading, mental resilience, video analysis, Individual Development Plans, nutrition and lifestyle, and education and language. The details of each area are being prepared.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'club/player-development'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Player Development Priorities | Taichung Blue Whale' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Player development priorities at Taichung Blue Whale: technical and tactical analysis, physical fitness training, match reading, mental resilience, video analysis, Individual Development Plans, nutrition and lifestyle, and education and language. The details of each area are being prepared.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'club/player-development';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Player Opportunities | Taichung Blue Whale', N'Player opportunities at Taichung Blue Whale: how to join the First Team, trial dates with online registration, and the recruitment channel for foreign players.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'club/opportunities'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Player Opportunities | Taichung Blue Whale' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Player opportunities at Taichung Blue Whale: how to join the First Team, trial dates with online registration, and the recruitment channel for foreign players.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'club/opportunities';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'International Pathways | Taichung Blue Whale', N'Taichung Blue Whale International Pathways: real cases of players going abroad to Japan and China, and the channels for overseas trials and club-matching enquiries.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'club/international-pathways'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'International Pathways | Taichung Blue Whale' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Taichung Blue Whale International Pathways: real cases of players going abroad to Japan and China, and the channels for overseas trials and club-matching enquiries.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'club/international-pathways';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Player Stories | Taichung Blue Whale', N'Player story cases from Taichung Blue Whale, recording each player''s real journey from joining Taichung Blue Whale to a bigger stage. Cases are being added.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'club/player-stories'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Player Stories | Taichung Blue Whale' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Player story cases from Taichung Blue Whale, recording each player''s real journey from joining Taichung Blue Whale to a bigger stage. Cases are being added.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'club/player-stories';
GO

INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description)
SELECT b.id, N'en', N'Become a Partner | Partners & Sponsors | Taichung Blue Whale', N'Learn about the six values of partnering with Taichung Blue Whale: brand exposure, audience reach, social impact and an international development network.'
FROM pages b JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'partners/become-a-partner'
  AND NOT EXISTS (SELECT 1 FROM pages_i18n e WHERE e.page_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.seo_title = CASE WHEN e.seo_title IS NULL OR e.seo_title = N'' OR e.seo_title = z.seo_title THEN N'Become a Partner | Partners & Sponsors | Taichung Blue Whale' ELSE e.seo_title END,
    e.seo_description = CASE WHEN e.seo_description IS NULL OR e.seo_description = N'' OR e.seo_description = z.seo_description THEN N'Learn about the six values of partnering with Taichung Blue Whale: brand exposure, audience reach, social impact and an international development network.' ELSE e.seo_description END
FROM pages_i18n e
JOIN pages b ON b.id = e.page_id
JOIN pages_i18n z ON z.page_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'partners/become-a-partner';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Do I have to buy football boots to join the sessions?', N'No. If you would like to buy a pair, you can ask the coach.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'上課一定要買足球鞋嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Do I have to buy football boots to join the sessions?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'No. If you would like to buy a pair, you can ask the coach.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'上課一定要買足球鞋嗎？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Can I pay by card or digital payment?', N'Card and digital payment services are not available.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'可以刷卡或數位支付嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Can I pay by card or digital payment?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'Card and digital payment services are not available.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'可以刷卡或數位支付嗎？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Do I need a certain level to join the U15 girls'' team?', N'No experience is needed. As long as you learn with commitment, you will have the chance to grow together and become a player.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'U15 女子隊需要有程度才能參加嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Do I need a certain level to join the U15 girls'' team?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'No experience is needed. As long as you learn with commitment, you will have the chance to grow together and become a player.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'U15 女子隊需要有程度才能參加嗎？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Can I try a single session first?', N'Yes. Attend the session on site and pay in cash afterwards; for the goalkeeper class, please fill in the registration form first.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'可以先上單堂嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Can I try a single session first?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'Yes. Attend the session on site and pay in cash afterwards; for the goalkeeper class, please fill in the registration form first.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'可以先上單堂嗎？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Can I take a trial session with the U15 girls'' team?', N'Yes. The fee is NT$300 per session, paid on site.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'U15 女子隊可以試上嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Can I take a trial session with the U15 girls'' team?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'Yes. The fee is NT$300 per session, paid on site.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'U15 女子隊可以試上嗎？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'How do I know whether a session is on when the weather is unsettled?', N'For children''s classes, an announcement is posted in the LINE group 1.5 hours before the session; if there is no announcement, the session goes ahead as normal. Activity-type sessions are announced separately on the Taiyuan Football Field Facebook page.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'天氣不穩定怎麼知道今天要不要上課？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'How do I know whether a session is on when the weather is unsettled?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'For children''s classes, an announcement is posted in the LINE group 1.5 hours before the session; if there is no announcement, the session goes ahead as normal. Activity-type sessions are announced separately on the Taiyuan Football Field Facebook page.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'天氣不穩定怎麼知道今天要不要上課？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Can I try a session first?', N'Yes. Please come to the session on site and pay in cash.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'可以先試上嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Can I try a session first?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'Yes. Please come to the session on site and pay in cash.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'可以先試上嗎？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Can a younger brother join the same class as his older brother?', N'Not recommended. Please consider physical and mental readiness and the intensity of the activity; joining by force makes injury more likely.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'弟弟可以跟哥哥同一班嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Can a younger brother join the same class as his older brother?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'Not recommended. Please consider physical and mental readiness and the intensity of the activity; joining by force makes injury more likely.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'弟弟可以跟哥哥同一班嗎？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Is the class only open to people with no experience?', N'The courses are introductory in nature, so no experience is needed. For advanced courses, please consider your own condition.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'沒有經驗才能參加嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Is the class only open to people with no experience?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'The courses are introductory in nature, so no experience is needed. For advanced courses, please consider your own condition.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'沒有經驗才能參加嗎？';
GO

INSERT INTO faqs_i18n (faq_id, locale, question, answer)
SELECT b.id, N'en', N'Can an absence be credited or refunded?', N'Fees are charged per session. An absence is not credited against fees and there are no make-up sessions.'
FROM faqs b JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'請假可以折抵退費嗎？'
  AND NOT EXISTS (SELECT 1 FROM faqs_i18n e WHERE e.faq_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.question = CASE WHEN e.question IS NULL OR e.question = N'' OR e.question = z.question THEN N'Can an absence be credited or refunded?' ELSE e.question END,
    e.answer = CASE WHEN e.answer IS NULL OR e.answer = N'' OR e.answer = z.answer THEN N'Fees are charged per session. An absence is not credited against fees and there are no make-up sessions.' ELSE e.answer END
FROM faqs_i18n e
JOIN faqs b ON b.id = e.faq_id
JOIN faqs_i18n z ON z.faq_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.question = N'請假可以折抵退費嗎？';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Blue Whale Goalkeeper Foundation Class', N'A Sport i Taiwan 2.0 sports hotspot course. Ages 7–12, open to boys and girls with places reserved for girls, limited to 10 places, 1.5 hours per session, NT$200 per session; the registration form must be completed first.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨守門員基礎班'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Blue Whale Goalkeeper Foundation Class' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot course. Ages 7–12, open to boys and girls with places reserved for girls, limited to 10 places, 1.5 hours per session, NT$200 per session; the registration form must be completed first.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨守門員基礎班';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Blue Whale U15 Girls'' Football Class', N'A Sport i Taiwan 2.0 sports hotspot course. Ages 13 and above, girls and women only, every Friday, 1.5 hours per session, NT$200 per session; individual registration on site.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨 U15 女子足球班'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Blue Whale U15 Girls'' Football Class' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot course. Ages 13 and above, girls and women only, every Friday, 1.5 hours per session, NT$200 per session; individual registration on site.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨 U15 女子足球班';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Community Toddler Football Class', N'A Sport i Taiwan 2.0 sports hotspot course. Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'社區幼幼足球班'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Community Toddler Football Class' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot course. Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'社區幼幼足球班';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Children''s Football Day', N'A Sport i Taiwan 2.0 sports hotspot activity. Kindergarten and elementary school children, open to boys and girls, 3 hours per event, free of charge, groups only (please apply by private message on the Taiyuan Football Field Facebook page).'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'兒童足球日'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Children''s Football Day' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot activity. Kindergarten and elementary school children, open to boys and girls, 3 hours per event, free of charge, groups only (please apply by private message on the Taiyuan Football Field Facebook page).' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'兒童足球日';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Community Football School (Little Blue Whale)', N'A community football school founded in 2017 and nicknamed Little Blue Whale. It emphasises the joy of sport, physical health, teamwork and learning football skills, on an enclosed dedicated football pitch under experienced coaches. No audition, no test and no joining fee; individual registration on site. Regular price NT$300 per session, special flat price NT$200 per session, paid in cash on site for each single session.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'社區足球學校（小藍鯨）'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Community Football School (Little Blue Whale)' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A community football school founded in 2017 and nicknamed Little Blue Whale. It emphasises the joy of sport, physical health, teamwork and learning football skills, on an enclosed dedicated football pitch under experienced coaches. No audition, no test and no joining fee; individual registration on site. Regular price NT$300 per session, special flat price NT$200 per session, paid in cash on site for each single session.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'社區足球學校（小藍鯨）';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Football Talent Coach and TDS Goalkeeper Talent Training (Coaching Course)', N'24 and 25 August 2025 (2 days). Coaches who hold a coaching qualification and currently lead a team are admitted first; NT$800 per person, paid in cash on the day (covers materials, insurance, lunch and so on). Supervising authorities: Sports Administration, Ministry of Education, and Taichung City Government Sports Bureau; organiser: National Taiwan Sport University; co-organisers: Taichung City Women''s Football Association and the Taichung Blue Whale women''s football team; supporting organisation: Chinese Taipei Football Association.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'足球人才教練暨 TDS 守門員人才培訓（教練講習）'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Football Talent Coach and TDS Goalkeeper Talent Training (Coaching Course)' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'24 and 25 August 2025 (2 days). Coaches who hold a coaching qualification and currently lead a team are admitted first; NT$800 per person, paid in cash on the day (covers materials, insurance, lunch and so on). Supervising authorities: Sports Administration, Ministry of Education, and Taichung City Government Sports Bureau; organiser: National Taiwan Sport University; co-organisers: Taichung City Women''s Football Association and the Taichung Blue Whale women''s football team; supporting organisation: Chinese Taipei Football Association.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'足球人才教練暨 TDS 守門員人才培訓（教練講習）';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Blue Whale U12 Girls'' Football Class', N'A Sport i Taiwan 2.0 sports hotspot course. Girls and women only, every Monday, Wednesday and Friday, 1.5 hours per session, NT$200 per session; individual registration on site.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨 U12 女子足球班'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Blue Whale U12 Girls'' Football Class' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot course. Girls and women only, every Monday, Wednesday and Friday, 1.5 hours per session, NT$200 per session; individual registration on site.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨 U12 女子足球班';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Pick-up Adult Football Matches', N'A Sport i Taiwan 2.0 sports hotspot activity. Junior high school age and above, open to men and women, 2–3 hours per event, pitch fee NT$100 per person; register on site as a group (8 or more people) or as an individual.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'野團成人足球賽'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Pick-up Adult Football Matches' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot activity. Junior high school age and above, open to men and women, 2–3 hours per event, pitch fee NT$100 per person; register on site as a group (8 or more people) or as an individual.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'野團成人足球賽';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Blue Whale U8 Football Class', N'A Sport i Taiwan 2.0 sports hotspot course. Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨 U8 足球教室'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Blue Whale U8 Football Class' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot course. Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨 U8 足球教室';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Blue Whale U10 Football Class', N'A Sport i Taiwan 2.0 sports hotspot course. Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨 U10 足球教室'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Blue Whale U10 Football Class' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot course. Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨 U10 足球教室';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Blue Whale Free Play Day', N'A Sport i Taiwan 2.0 sports hotspot activity. All ages, open to boys and girls, 1.5 hours per event, free of charge, no registration needed.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨足球自由日'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Blue Whale Free Play Day' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot activity. All ages, open to boys and girls, 1.5 hours per event, free of charge, no registration needed.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'藍鯨足球自由日';
GO

INSERT INTO programs_i18n (program_id, locale, name, intro)
SELECT b.id, N'en', N'Community Early-Years Football Class', N'A Sport i Taiwan 2.0 sports hotspot course. Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site.'
FROM programs b JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'幼兒社區足球班'
  AND NOT EXISTS (SELECT 1 FROM programs_i18n e WHERE e.program_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Community Early-Years Football Class' ELSE e.name END,
    e.intro = CASE WHEN e.intro IS NULL OR e.intro = N'' OR e.intro = z.intro THEN N'A Sport i Taiwan 2.0 sports hotspot course. Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site.' ELSE e.intro END
FROM programs_i18n e
JOIN programs b ON b.id = e.program_id
JOIN programs_i18n z ON z.program_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'幼兒社區足球班';
GO

INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description)
SELECT b.id, N'en', N'2024 Taichung Women''s Football Festival 「夏洛特的下午茶」', N'Sport i Taiwan and the Taichung Women''s Football Festival. Taichung Beitun Taiyuan Football Field; check-in at 15:30, start at 16:00, finish at 18:00. For girls in grades 1–5 of elementary school, with a promotion group and a competition group; a public-interest promotional event, free to attend throughout.'
FROM calendar_custom_events b JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2024 台中女子足球節「夏洛特的下午茶」'
  AND NOT EXISTS (SELECT 1 FROM calendar_custom_events_i18n e WHERE e.calendar_custom_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'2024 Taichung Women''s Football Festival 「夏洛特的下午茶」' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'Sport i Taiwan and the Taichung Women''s Football Festival. Taichung Beitun Taiyuan Football Field; check-in at 15:30, start at 16:00, finish at 18:00. For girls in grades 1–5 of elementary school, with a promotion group and a competition group; a public-interest promotional event, free to attend throughout.' ELSE e.description END
FROM calendar_custom_events_i18n e
JOIN calendar_custom_events b ON b.id = e.calendar_custom_event_id
JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'2024 台中女子足球節「夏洛特的下午茶」';
GO

INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Open training session', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM calendar_custom_events b JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'【測試】公開訓練'
  AND NOT EXISTS (SELECT 1 FROM calendar_custom_events_i18n e WHERE e.calendar_custom_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Open training session' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.description END
FROM calendar_custom_events_i18n e
JOIN calendar_custom_events b ON b.id = e.calendar_custom_event_id
JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'【測試】公開訓練';
GO

INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description)
SELECT b.id, N'en', N'[Test] Fan meet-and-greet', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM calendar_custom_events b JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'【測試】球迷見面會'
  AND NOT EXISTS (SELECT 1 FROM calendar_custom_events_i18n e WHERE e.calendar_custom_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Fan meet-and-greet' ELSE e.title END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.description END
FROM calendar_custom_events_i18n e
JOIN calendar_custom_events b ON b.id = e.calendar_custom_event_id
JOIN calendar_custom_events_i18n z ON z.calendar_custom_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.title = N'【測試】球迷見面會';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'Taichung City Government'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'臺中市政府'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Taichung City Government' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'臺中市政府';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'Taichung City Government Sports Bureau'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'臺中市政府運動局'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Taichung City Government Sports Bureau' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'臺中市政府運動局';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'Sports Administration, Ministry of Education'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'教育部體育署'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Sports Administration, Ministry of Education' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'教育部體育署';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'Department of Physical Education, National Taiwan Sport University'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'國立臺灣體育運動大學體育學系'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Department of Physical Education, National Taiwan Sport University' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'國立臺灣體育運動大學體育學系';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'Pocari Sweat'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'寶礦力水得 Pocari Sweat'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Pocari Sweat' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'寶礦力水得 Pocari Sweat';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'MIE Taiwan'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'MIE Taiwan'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'MIE Taiwan' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'MIE Taiwan';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'SKECHERS'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'SKECHERS／思克威爾股份有限公司'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'SKECHERS' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'SKECHERS／思克威爾股份有限公司';
GO

INSERT INTO partners_i18n (partner_id, locale, name)
SELECT b.id, N'en', N'Defunc Taiwan'
FROM partners b JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'Defunc Taiwan'
  AND NOT EXISTS (SELECT 1 FROM partners_i18n e WHERE e.partner_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Defunc Taiwan' ELSE e.name END
FROM partners_i18n e
JOIN partners b ON b.id = e.partner_id
JOIN partners_i18n z ON z.partner_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'Defunc Taiwan';
GO

INSERT INTO articles_i18n (article_id, locale, title, summary)
SELECT b.id, N'en', N'[Test] Taichung Blue Whale club news sample', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'bw-test-news-club'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Taichung Blue Whale club news sample' ELSE e.title END,
    e.summary = CASE WHEN e.summary IS NULL OR e.summary = N'' OR e.summary = z.summary THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.summary END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'bw-test-news-club';
GO

INSERT INTO articles_i18n (article_id, locale, title, summary)
SELECT b.id, N'en', N'[Test] Taichung Blue Whale community event sample', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'bw-test-news-community'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Taichung Blue Whale community event sample' ELSE e.title END,
    e.summary = CASE WHEN e.summary IS NULL OR e.summary = N'' OR e.summary = z.summary THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.summary END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'bw-test-news-community';
GO

INSERT INTO articles_i18n (article_id, locale, title, summary)
SELECT b.id, N'en', N'[Test] Taichung Blue Whale match report sample', N'[Test] This is test content. Please replace it in the back office before launch.'
FROM articles b JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'bw-test-news-match'
  AND NOT EXISTS (SELECT 1 FROM articles_i18n e WHERE e.article_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.title = CASE WHEN e.title IS NULL OR e.title = N'' OR e.title = z.title THEN N'[Test] Taichung Blue Whale match report sample' ELSE e.title END,
    e.summary = CASE WHEN e.summary IS NULL OR e.summary = N'' OR e.summary = z.summary THEN N'[Test] This is test content. Please replace it in the back office before launch.' ELSE e.summary END
FROM articles_i18n e
JOIN articles b ON b.id = e.article_id
JOIN articles_i18n z ON z.article_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.slug = N'bw-test-news-match';
GO

INSERT INTO settings_i18n (setting_id, locale, value)
SELECT b.id, N'en', N'Taiwan Mulan Football League'
FROM settings b JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'site.league_name'
  AND NOT EXISTS (SELECT 1 FROM settings_i18n e WHERE e.setting_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.value = CASE WHEN e.value IS NULL OR e.value = N'' OR e.value = z.value THEN N'Taiwan Mulan Football League' ELSE e.value END
FROM settings_i18n e
JOIN settings b ON b.id = e.setting_id
JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'site.league_name';
GO

INSERT INTO settings_i18n (setting_id, locale, value)
SELECT b.id, N'en', N'Mulan League'
FROM settings b JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'site.league_short_name'
  AND NOT EXISTS (SELECT 1 FROM settings_i18n e WHERE e.setting_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.value = CASE WHEN e.value IS NULL OR e.value = N'' OR e.value = z.value THEN N'Mulan League' ELSE e.value END
FROM settings_i18n e
JOIN settings b ON b.id = e.setting_id
JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'site.league_short_name';
GO

INSERT INTO settings_i18n (setting_id, locale, value)
SELECT b.id, N'en', N'{title} | Taichung Blue Whale Women''s Football Club'
FROM settings b JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'seo.title_template'
  AND NOT EXISTS (SELECT 1 FROM settings_i18n e WHERE e.setting_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.value = CASE WHEN e.value IS NULL OR e.value = N'' OR e.value = z.value THEN N'{title} | Taichung Blue Whale Women''s Football Club' ELSE e.value END
FROM settings_i18n e
JOIN settings b ON b.id = e.setting_id
JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'seo.title_template';
GO

INSERT INTO settings_i18n (setting_id, locale, value)
SELECT b.id, N'en', N'Taichung Blue Whale Women''s Football Club, part of the Taichung City Women''s Football Association, is one of the teams of the Taiwan Mulan Football League. The club hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan.'
FROM settings b JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'seo.default_description'
  AND NOT EXISTS (SELECT 1 FROM settings_i18n e WHERE e.setting_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.value = CASE WHEN e.value IS NULL OR e.value = N'' OR e.value = z.value THEN N'Taichung Blue Whale Women''s Football Club, part of the Taichung City Women''s Football Association, is one of the teams of the Taiwan Mulan Football League. The club hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan.' ELSE e.value END
FROM settings_i18n e
JOIN settings b ON b.id = e.setting_id
JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'seo.default_description';
GO

INSERT INTO settings_i18n (setting_id, locale, value)
SELECT b.id, N'en', N'Taichung Blue Whale Women''s Football Club belongs to the Taichung City Women''s Football Association and is one of the teams of the Taiwan Mulan Football League. The blue whale is its symbol of a faster, stronger and more modern style of football, and the club hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan. Slogan: Taichung Blue Whale rides the waves towards the open ocean.'
FROM settings b JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'geo.llms_positioning'
  AND NOT EXISTS (SELECT 1 FROM settings_i18n e WHERE e.setting_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.value = CASE WHEN e.value IS NULL OR e.value = N'' OR e.value = z.value THEN N'Taichung Blue Whale Women''s Football Club belongs to the Taichung City Women''s Football Association and is one of the teams of the Taiwan Mulan Football League. The blue whale is its symbol of a faster, stronger and more modern style of football, and the club hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan. Slogan: Taichung Blue Whale rides the waves towards the open ocean.' ELSE e.value END
FROM settings_i18n e
JOIN settings b ON b.id = e.setting_id
JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'geo.llms_positioning';
GO

INSERT INTO settings_i18n (setting_id, locale, value)
SELECT b.id, N'en', N'Founded on 12 April 2014; plays in the Taiwan Mulan Football League; home ground is Taichung Beitun Taiyuan Football Field (current), and the home ground in the founding period was Taichung Fengyuan Stadium; a development pathway with the first team and the youth teams (U15 / U12) running in parallel.'
FROM settings b JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'geo.llms_facts_summary'
  AND NOT EXISTS (SELECT 1 FROM settings_i18n e WHERE e.setting_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.value = CASE WHEN e.value IS NULL OR e.value = N'' OR e.value = z.value THEN N'Founded on 12 April 2014; plays in the Taiwan Mulan Football League; home ground is Taichung Beitun Taiyuan Football Field (current), and the home ground in the founding period was Taichung Fengyuan Stadium; a development pathway with the first team and the youth teams (U15 / U12) running in parallel.' ELSE e.value END
FROM settings_i18n e
JOIN settings b ON b.id = e.setting_id
JOIN settings_i18n z ON z.setting_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND b.setting_key = N'geo.llms_facts_summary';
GO

INSERT INTO collections_i18n (collection_id, locale, name, narrative)
SELECT b.id, N'en', N'[Test] Blue Whale collection', N'[Test] Placeholder brand narrative for the Blue Whale collection.'
FROM collections b JOIN collections_i18n z ON z.collection_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨系列'
  AND NOT EXISTS (SELECT 1 FROM collections_i18n e WHERE e.collection_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Blue Whale collection' ELSE e.name END,
    e.narrative = CASE WHEN e.narrative IS NULL OR e.narrative = N'' OR e.narrative = z.narrative THEN N'[Test] Placeholder brand narrative for the Blue Whale collection.' ELSE e.narrative END
FROM collections_i18n e
JOIN collections b ON b.id = e.collection_id
JOIN collections_i18n z ON z.collection_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨系列';
GO

INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location)
SELECT b.id, N'en', N'[Test] Blue Whale fan day', N'[Test] Demo fan event hosted by Blue Whale.', N'[Test] No. 1, Test Road, Xitun District, Taichung City'
FROM fan_events b JOIN fan_events_i18n z ON z.fan_event_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨球迷日'
  AND NOT EXISTS (SELECT 1 FROM fan_events_i18n e WHERE e.fan_event_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Blue Whale fan day' ELSE e.name END,
    e.description = CASE WHEN e.description IS NULL OR e.description = N'' OR e.description = z.description THEN N'[Test] Demo fan event hosted by Blue Whale.' ELSE e.description END,
    e.location = CASE WHEN e.location IS NULL OR e.location = N'' OR e.location = z.location THEN N'[Test] No. 1, Test Road, Xitun District, Taichung City' ELSE e.location END
FROM fan_events_i18n e
JOIN fan_events b ON b.id = e.fan_event_id
JOIN fan_events_i18n z ON z.fan_event_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨球迷日';
GO

INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note)
SELECT b.id, N'en', N'Blue Whale Fan Club (Single)', N'[Test] Includes one membership card and one welcome jersey.'
FROM membership_plans b JOIN membership_plans_i18n z ON z.membership_plan_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨球迷會員（單人）'
  AND NOT EXISTS (SELECT 1 FROM membership_plans_i18n e WHERE e.membership_plan_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'Blue Whale Fan Club (Single)' ELSE e.name END,
    e.benefit_note = CASE WHEN e.benefit_note IS NULL OR e.benefit_note = N'' OR e.benefit_note = z.benefit_note THEN N'[Test] Includes one membership card and one welcome jersey.' ELSE e.benefit_note END
FROM membership_plans_i18n e
JOIN membership_plans b ON b.id = e.membership_plan_id
JOIN membership_plans_i18n z ON z.membership_plan_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨球迷會員（單人）';
GO

INSERT INTO products_i18n (product_id, locale, name, narrative, tags)
SELECT b.id, N'en', N'[Test] Blue Whale jersey', N'[Test] Placeholder product narrative.', N'[Test],Demo'
FROM products b JOIN products_i18n z ON z.product_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨球衣'
  AND NOT EXISTS (SELECT 1 FROM products_i18n e WHERE e.product_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Blue Whale jersey' ELSE e.name END,
    e.narrative = CASE WHEN e.narrative IS NULL OR e.narrative = N'' OR e.narrative = z.narrative THEN N'[Test] Placeholder product narrative.' ELSE e.narrative END,
    e.tags = CASE WHEN e.tags IS NULL OR e.tags = N'' OR e.tags = z.tags THEN N'[Test],Demo' ELSE e.tags END
FROM products_i18n e
JOIN products b ON b.id = e.product_id
JOIN products_i18n z ON z.product_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨球衣';
GO

INSERT INTO partner_stores_i18n (partner_store_id, locale, name, offer_content)
SELECT b.id, N'en', N'[Test] Blue Whale Demo Store', N'[Test] Exclusive offer for Blue Whale members'
FROM partner_stores b JOIN partner_stores_i18n z ON z.partner_store_id = b.id AND z.locale = N'zh-Hant'
WHERE b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨示範店家'
  AND NOT EXISTS (SELECT 1 FROM partner_stores_i18n e WHERE e.partner_store_id = b.id AND e.locale = N'en');

UPDATE e SET
    e.name = CASE WHEN e.name IS NULL OR e.name = N'' OR e.name = z.name THEN N'[Test] Blue Whale Demo Store' ELSE e.name END,
    e.offer_content = CASE WHEN e.offer_content IS NULL OR e.offer_content = N'' OR e.offer_content = z.offer_content THEN N'[Test] Exclusive offer for Blue Whale members' ELSE e.offer_content END
FROM partner_stores_i18n e
JOIN partner_stores b ON b.id = e.partner_store_id
JOIN partner_stores_i18n z ON z.partner_store_id = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND b.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND z.name = N'【測試】藍鯨示範店家';
GO

EXEC sys.sp_addextendedproperty @name = N'tcrfc.seed_import', @value = N'$(IMPORT_BATCH)';
COMMIT TRANSACTION;
GO
