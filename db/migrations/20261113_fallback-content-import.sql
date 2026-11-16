/* ============================================================================
   官網「後台沒資料就顯示寫死內容」的真實內容轉入後台資料（資料遷移）
   2026-11-13｜使用者拍板拿掉前台的寫死後備（docs/23 H-1 B 類、C-5）；拿掉之前，真實內容必須先在資料庫裡。
   定義與種子區段 60b 同一份（db/seed/fallback_content_seed.py）；本檔由 db/seed/generate-fallback-content-migration.py 產生，請勿手改。

   ── 本檔補的內容（全部有中英兩份；沒有圖片，圖片由營運人員在後台上傳）──
     ① 磐石國際夥伴 3 筆（Hellas Verona FC／Rayo Ciudad Alcobendas CF／Rot-Weiss Ahlen；類型「國際夥伴」）
     ② 藍鯨建教合作學校 5 所（類型「教育夥伴」）
     ③ 贊助方案名稱：磐石 9、藍鯨 8（規劃書 §3.9 9.4；只有名稱，價格不公開、沒有權益文字）
     ④ 磐石榮譽：成立首年頭銜「2024 全國乙級聯賽 冠軍」（找不到球隊 D1 或任何球季就略過）
     ⑤ 頁尾簡介、社群連結、藍鯨聯絡 Email（只補「列不存在」或「值為空且從沒人儲存過」）

   ── 不在本檔（原因見 db/seed/README.md「官網寫死後備轉後台資料（區段 60b）」）──
     里程碑（正式庫已有客戶文件匯入的 49 筆）、慈善事蹟與影響力（同上，3 筆）、球迷會活動照片（沒有活動名稱與日期）、
     藍鯨兒童訓練班別與守門員班（課程梯次的欄位對不上「元／堂」）、首頁新聞卡（新聞已在庫）、場地占位卡（占位）、商店球衣照（占位）。

   ── 冪等與保護 ──
     夥伴以 (俱樂部, slug)；贊助方案以 (俱樂部, 繁中名稱)——同名方案（含本機種子的【測試】方案）已存在就不再補；
     榮譽以 (俱樂部, 年, 繁中賽事名)；設定只動空值且沒人儲存過者。後台人員改過的內容一律不動。可重複執行。
   • 任何事後斷言失敗即 THROW；搭配 XACT_ABORT 與 sqlcmd -b，失敗時不留半套資料（每批各自成交易）。
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF (SELECT COUNT(*) FROM clubs WHERE code IN (N'tcrfc', N'bw')) <> 2
  THROW 51301, N'找不到 tcrfc 或 bw 俱樂部（clubs.code）', 1;
GO

-- ── 60b. 官網寫死後備轉後台資料：磐石國際夥伴、藍鯨建教合作學校、贊助方案名稱、磐石成立首年榮譽、頁尾簡介與社群（真實內容，無圖片） ──
DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'intl-hellas-verona';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'76c420f0-1c3e-5db8-a320-f16ccda2c040';
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'intl-hellas-verona', N'國際夥伴', N'義大利 Italy', 0, 0, 20);
  INSERT INTO partners_i18n (partner_id, locale, name, content, logo_alt) VALUES (@id, N'zh-Hant', N'Hellas Verona FC', N'義大利甲級球會。雙方簽署合作備忘錄，推動台義足球交流。', N'義大利 Hellas Verona FC 隊徽');
  INSERT INTO partners_i18n (partner_id, locale, name, content, logo_alt) VALUES (@id, N'en', N'Hellas Verona FC', N'A Serie A club from Italy. The two clubs signed a memorandum of cooperation to promote Taiwan–Italy football exchange.', N'Hellas Verona FC crest (Italy)');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'intl-rayo-alcobendas';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'4053d789-a82b-56b0-b9d1-cb1bdb51b471';
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'intl-rayo-alcobendas', N'國際夥伴', N'西班牙 Spain', 0, 0, 21);
  INSERT INTO partners_i18n (partner_id, locale, name, content, logo_alt) VALUES (@id, N'zh-Hant', N'Rayo Ciudad Alcobendas CF', N'西班牙球會。雙方簽訂國際足球合作備忘錄。', N'西班牙 Rayo Ciudad Alcobendas CF 隊徽');
  INSERT INTO partners_i18n (partner_id, locale, name, content, logo_alt) VALUES (@id, N'en', N'Rayo Ciudad Alcobendas CF', N'A club from Spain. The two clubs signed an international football cooperation memorandum.', N'Rayo Ciudad Alcobendas CF crest (Spain)');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug = N'intl-rot-weiss-ahlen';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'95b46613-a14d-5c34-81c7-a06800c890c8';
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'intl-rot-weiss-ahlen', N'國際夥伴', N'德國 Germany', 0, 0, 22);
  INSERT INTO partners_i18n (partner_id, locale, name, content, logo_alt) VALUES (@id, N'zh-Hant', N'Rot-Weiss Ahlen', N'德國球會。雙方簽署合作諒解備忘錄。', N'德國 Rot-Weiss Ahlen 隊徽');
  INSERT INTO partners_i18n (partner_id, locale, name, content, logo_alt) VALUES (@id, N'en', N'Rot-Weiss Ahlen', N'A club from Germany. The two clubs signed a memorandum of understanding on cooperation.', N'Rot-Weiss Ahlen crest (Germany)');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'school-ntsu-womens';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'179a520b-0ae4-5050-b577-276ae8b52c2d';
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'school-ntsu-womens', N'教育夥伴', NULL, 0, 0, 101);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'國立臺灣體育運動大學女子足球隊', N'建教合作（女子足球隊）');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'National Taiwan Sport University women''s football team', N'Industry-academia cooperation (women''s football team)');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'school-wuquan-jhs';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'91bb5970-27cc-52e5-9cc4-0259f731b95d';
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'school-wuquan-jhs', N'教育夥伴', NULL, 0, 0, 102);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'臺中市立五權國民中學女子足球隊', N'建教合作（女子足球隊）；2015 年協助球隊成立');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'Taichung Municipal Wuquan Junior High School women''s football team', N'Industry-academia cooperation (women''s football team); helped establish the team in 2015');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'school-shuili-jhs';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'8046b202-fbc1-563d-a766-5926a2b4be40';
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'school-shuili-jhs', N'教育夥伴', NULL, 0, 0, 103);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'南投縣立水里國民中學女子足球隊', N'建教合作（女子足球隊）');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'Nantou County Shuili Junior High School women''s football team', N'Industry-academia cooperation (women''s football team)');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'school-yongjing-jhs';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'c788fd6b-21d4-50ba-92a7-9a765aa3a23f';
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'school-yongjing-jhs', N'教育夥伴', NULL, 0, 0, 104);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'彰化縣立永靖國民中學女子足球隊', N'建教合作（女子足球隊）');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'Changhua County Yongjing Junior High School women''s football team', N'Industry-academia cooperation (women''s football team)');
  COMMIT TRANSACTION;
END
GO

DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug = N'school-duxing-es';
IF @id IS NULL
BEGIN
  BEGIN TRANSACTION;
  SET @id = N'47835a56-9b8d-56c4-bb92-d5a70f2bf05e';
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, (SELECT id FROM clubs WHERE code = N'bw'), N'school-duxing-es', N'教育夥伴', NULL, 0, 0, 105);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', N'臺中市篤行國小女子足球隊', N'建教合作（女子足球隊）');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', N'Taichung Duxing Elementary School women''s football team', N'Industry-academia cooperation (women''s football team)');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'俱樂部贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'2105268b-b625-5c0e-b6f8-890162e8b6a1', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-club', 0, 0, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'2105268b-b625-5c0e-b6f8-890162e8b6a1', N'zh-Hant', N'俱樂部贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'2105268b-b625-5c0e-b6f8-890162e8b6a1', N'en', N'Club Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'學院贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'846cd385-8ed3-56b0-b719-447f0712d649', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-academy', 0, 1, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'846cd385-8ed3-56b0-b719-447f0712d649', N'zh-Hant', N'學院贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'846cd385-8ed3-56b0-b719-447f0712d649', N'en', N'Academy Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'球隊贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'f641d41b-88fd-51a7-a488-77300411b223', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-team', 0, 2, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'f641d41b-88fd-51a7-a488-77300411b223', N'zh-Hant', N'球隊贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'f641d41b-88fd-51a7-a488-77300411b223', N'en', N'Team Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'營隊贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'c926b4b1-7a1f-501a-9eaf-4a3179f3facb', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-camp', 0, 3, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'c926b4b1-7a1f-501a-9eaf-4a3179f3facb', N'zh-Hant', N'營隊贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'c926b4b1-7a1f-501a-9eaf-4a3179f3facb', N'en', N'Camp Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'國際計畫贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'a4cc4c03-376e-5783-880e-13f7391795a5', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-international', 0, 4, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'a4cc4c03-376e-5783-880e-13f7391795a5', N'zh-Hant', N'國際計畫贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'a4cc4c03-376e-5783-880e-13f7391795a5', N'en', N'International Program Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'漫畫內容合作')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'bfc399fe-6a69-55bf-89d8-b351ce7b26f5', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-comic', 0, 5, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'bfc399fe-6a69-55bf-89d8-b351ce7b26f5', N'zh-Hant', N'漫畫內容合作');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'bfc399fe-6a69-55bf-89d8-b351ce7b26f5', N'en', N'Manga Content Partnership');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'商品合作')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'2fe775d3-55d8-5900-93e5-37fc81f0384b', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-merch', 0, 6, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'2fe775d3-55d8-5900-93e5-37fc81f0384b', N'zh-Hant', N'商品合作');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'2fe775d3-55d8-5900-93e5-37fc81f0384b', N'en', N'Merchandise Partnership');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'球迷會贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'0e7e5ff4-1d6c-5735-80ce-310dd8907157', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-fanclub', 0, 7, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'0e7e5ff4-1d6c-5735-80ce-310dd8907157', N'zh-Hant', N'球迷會贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'0e7e5ff4-1d6c-5735-80ce-310dd8907157', N'en', N'Fan Club Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND i.name = N'場館冠名')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'9ebfa5e7-d4fb-524d-944f-ad2d5dd559e6', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'package-naming', 0, 8, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'9ebfa5e7-d4fb-524d-944f-ad2d5dd559e6', N'zh-Hant', N'場館冠名');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'9ebfa5e7-d4fb-524d-944f-ad2d5dd559e6', N'en', N'Venue Naming Rights');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.name = N'俱樂部贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'1d6f80a4-0e29-509f-87df-c49d9c485fc7', (SELECT id FROM clubs WHERE code = N'bw'), N'package-club', 0, 0, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'1d6f80a4-0e29-509f-87df-c49d9c485fc7', N'zh-Hant', N'俱樂部贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'1d6f80a4-0e29-509f-87df-c49d9c485fc7', N'en', N'Club Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.name = N'青年隊贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'472e5a30-4768-5cc1-a725-6226d4062b1e', (SELECT id FROM clubs WHERE code = N'bw'), N'package-youth', 0, 1, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'472e5a30-4768-5cc1-a725-6226d4062b1e', N'zh-Hant', N'青年隊贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'472e5a30-4768-5cc1-a725-6226d4062b1e', N'en', N'Youth Team Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.name = N'球隊贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'7be5a99c-77d6-5ab0-b753-47912499fa59', (SELECT id FROM clubs WHERE code = N'bw'), N'package-team', 0, 2, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'7be5a99c-77d6-5ab0-b753-47912499fa59', N'zh-Hant', N'球隊贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'7be5a99c-77d6-5ab0-b753-47912499fa59', N'en', N'Team Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.name = N'營隊贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'a49b05a9-1ac7-54ba-963b-7801a734432f', (SELECT id FROM clubs WHERE code = N'bw'), N'package-camp', 0, 3, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'a49b05a9-1ac7-54ba-963b-7801a734432f', N'zh-Hant', N'營隊贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'a49b05a9-1ac7-54ba-963b-7801a734432f', N'en', N'Camp Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.name = N'國際計畫贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'6235f7bd-684d-56a4-850b-b739c31fbb39', (SELECT id FROM clubs WHERE code = N'bw'), N'package-international', 0, 4, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'6235f7bd-684d-56a4-850b-b739c31fbb39', N'zh-Hant', N'國際計畫贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'6235f7bd-684d-56a4-850b-b739c31fbb39', N'en', N'International Program Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.name = N'商品合作')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'27ced9f0-b44d-580f-aa59-75891076e2dd', (SELECT id FROM clubs WHERE code = N'bw'), N'package-merch', 0, 5, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'27ced9f0-b44d-580f-aa59-75891076e2dd', N'zh-Hant', N'商品合作');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'27ced9f0-b44d-580f-aa59-75891076e2dd', N'en', N'Merchandise Partnership');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.name = N'球迷會贊助')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'b6cb5ae3-0ec5-575b-b3a8-17b2b1f10b0b', (SELECT id FROM clubs WHERE code = N'bw'), N'package-fanclub', 0, 6, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'b6cb5ae3-0ec5-575b-b3a8-17b2b1f10b0b', N'zh-Hant', N'球迷會贊助');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'b6cb5ae3-0ec5-575b-b3a8-17b2b1f10b0b', N'en', N'Fan Club Sponsorship');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = (SELECT id FROM clubs WHERE code = N'bw') AND i.name = N'場館冠名')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES (N'9e30472f-5bfd-555e-b267-ce449ae39c18', (SELECT id FROM clubs WHERE code = N'bw'), N'package-naming', 0, 7, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'9e30472f-5bfd-555e-b267-ce449ae39c18', N'zh-Hant', N'場館冠名');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (N'9e30472f-5bfd-555e-b267-ce449ae39c18', N'en', N'Venue Naming Rights');
  COMMIT TRANSACTION;
END
GO

DECLARE @team uniqueidentifier = (SELECT id FROM teams WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND code = N'D1');
DECLARE @season uniqueidentifier = (SELECT TOP 1 id FROM seasons WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') ORDER BY start_on DESC);
IF @team IS NOT NULL AND @season IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM achievements a JOIN achievements_i18n i ON i.achievement_id = a.id AND i.locale = N'zh-Hant'
                   WHERE a.club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND a.year = 2024 AND i.competition_name = N'全國乙級聯賽')
BEGIN
  BEGIN TRANSACTION;
  INSERT INTO achievements (id, club_id, season_id, team_id, year) VALUES (N'd6de4108-d61d-5acc-9606-092107f2e1e3', (SELECT id FROM clubs WHERE code = N'tcrfc'), @season, @team, 2024);
  INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing) VALUES (N'd6de4108-d61d-5acc-9606-092107f2e1e3', N'zh-Hant', N'全國乙級聯賽', N'冠軍');
  INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing) VALUES (N'd6de4108-d61d-5acc-9606-092107f2e1e3', N'en', N'National Second Division', N'Champions');
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_facebook')
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (N'be3ffc40-aea4-5a28-8e76-be394930393f', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.social_facebook', N'https://www.facebook.com/TCRFC2024', N'site');
ELSE
  UPDATE settings SET setting_value = N'https://www.facebook.com/TCRFC2024'
  WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_facebook' AND setting_value IS NULL AND updated_by IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_instagram')
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (N'0b1d4eb6-af56-5030-b668-a12f050cef77', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.social_instagram', N'https://www.instagram.com/tcr_fc_2024', N'site');
ELSE
  UPDATE settings SET setting_value = N'https://www.instagram.com/tcr_fc_2024'
  WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_instagram' AND setting_value IS NULL AND updated_by IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_youtube')
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (N'f0c730e8-1ba2-5056-a7a0-5b6b439ec318', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.social_youtube', N'https://www.youtube.com/@TCRFC-2024', N'site');
ELSE
  UPDATE settings SET setting_value = N'https://www.youtube.com/@TCRFC-2024'
  WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.social_youtube' AND setting_value IS NULL AND updated_by IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.footer_blurb')
  INSERT INTO settings (id, club_id, setting_key, setting_group) VALUES (N'd50816cc-ebc4-5f9f-8b42-23d365828379', (SELECT id FROM clubs WHERE code = N'tcrfc'), N'site.footer_blurb', N'site');
GO

DECLARE @sid uniqueidentifier = (SELECT id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.footer_blurb');
IF @sid IS NOT NULL
BEGIN
  BEGIN TRANSACTION;
  IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = N'zh-Hant')
    INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, N'zh-Hant', N'台中磐石足球俱樂部致力於透過專業模式，培育選手追求卓越，讓世界看見台灣足球。');
  ELSE
    UPDATE settings_i18n SET value = N'台中磐石足球俱樂部致力於透過專業模式，培育選手追求卓越，讓世界看見台灣足球。'
    WHERE setting_id = @sid AND locale = N'zh-Hant' AND NULLIF(value, N'') IS NULL
      AND EXISTS (SELECT 1 FROM settings s WHERE s.id = @sid AND s.updated_by IS NULL);
  COMMIT TRANSACTION;
END
GO

DECLARE @sid uniqueidentifier = (SELECT id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key = N'site.footer_blurb');
IF @sid IS NOT NULL
BEGIN
  BEGIN TRANSACTION;
  IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = N'en')
    INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, N'en', N'Taichung Rock FC develops players who pursue excellence through a professional model, so the world can see Taiwan football.');
  ELSE
    UPDATE settings_i18n SET value = N'Taichung Rock FC develops players who pursue excellence through a professional model, so the world can see Taiwan football.'
    WHERE setting_id = @sid AND locale = N'en' AND NULLIF(value, N'') IS NULL
      AND EXISTS (SELECT 1 FROM settings s WHERE s.id = @sid AND s.updated_by IS NULL);
  COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_facebook')
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (N'de48ac01-1dbf-50cb-a096-de5314e38c68', (SELECT id FROM clubs WHERE code = N'bw'), N'site.social_facebook', N'https://www.facebook.com/tbwfc', N'site');
ELSE
  UPDATE settings SET setting_value = N'https://www.facebook.com/tbwfc'
  WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_facebook' AND setting_value IS NULL AND updated_by IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_instagram')
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (N'4b373e77-80b5-58ee-bab3-a47c0524e699', (SELECT id FROM clubs WHERE code = N'bw'), N'site.social_instagram', N'https://instagram.com/tcbw2014', N'site');
ELSE
  UPDATE settings SET setting_value = N'https://instagram.com/tcbw2014'
  WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_instagram' AND setting_value IS NULL AND updated_by IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_youtube')
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (N'4b4a45cc-0945-5689-b432-7e40467c596b', (SELECT id FROM clubs WHERE code = N'bw'), N'site.social_youtube', N'https://www.youtube.com/@user-xu1wm3xx1w', N'site');
ELSE
  UPDATE settings SET setting_value = N'https://www.youtube.com/@user-xu1wm3xx1w'
  WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_youtube' AND setting_value IS NULL AND updated_by IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_line')
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (N'c1667937-8534-51b4-93a6-ad9ac9fee289', (SELECT id FROM clubs WHERE code = N'bw'), N'site.social_line', N'https://lin.ee/CS65qCR', N'site');
ELSE
  UPDATE settings SET setting_value = N'https://lin.ee/CS65qCR'
  WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.social_line' AND setting_value IS NULL AND updated_by IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.contact_email')
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (N'fd0c329c-bcee-599f-a262-3b61cddae0fd', (SELECT id FROM clubs WHERE code = N'bw'), N'site.contact_email', N'fbbh2014@gmail.com', N'site');
ELSE
  UPDATE settings SET setting_value = N'fbbh2014@gmail.com'
  WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.contact_email' AND setting_value IS NULL AND updated_by IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.footer_blurb')
  INSERT INTO settings (id, club_id, setting_key, setting_group) VALUES (N'70f3941b-d85e-5188-8c43-3d5f941c6648', (SELECT id FROM clubs WHERE code = N'bw'), N'site.footer_blurb', N'site');
GO

DECLARE @sid uniqueidentifier = (SELECT id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.footer_blurb');
IF @sid IS NOT NULL
BEGIN
  BEGIN TRANSACTION;
  IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = N'zh-Hant')
    INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, N'zh-Hant', N'隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。');
  ELSE
    UPDATE settings_i18n SET value = N'隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。'
    WHERE setting_id = @sid AND locale = N'zh-Hant' AND NULLIF(value, N'') IS NULL
      AND EXISTS (SELECT 1 FROM settings s WHERE s.id = @sid AND s.updated_by IS NULL);
  COMMIT TRANSACTION;
END
GO

DECLARE @sid uniqueidentifier = (SELECT id FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND setting_key = N'site.footer_blurb');
IF @sid IS NOT NULL
BEGIN
  BEGIN TRANSACTION;
  IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = N'en')
    INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, N'en', N'Taichung Blue Whale Women''s Football Club, part of the Taichung City Women''s Football Association, is one of the teams of the Taiwan Mulan Football League. Taking the blue whale as its symbol, it stands for a faster, stronger and more modern style of football, and hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan.');
  ELSE
    UPDATE settings_i18n SET value = N'Taichung Blue Whale Women''s Football Club, part of the Taichung City Women''s Football Association, is one of the teams of the Taiwan Mulan Football League. Taking the blue whale as its symbol, it stands for a faster, stronger and more modern style of football, and hopes to lift the grassroots football culture in Taichung and drive the development of women''s football in central Taiwan.'
    WHERE setting_id = @sid AND locale = N'en' AND NULLIF(value, N'') IS NULL
      AND EXISTS (SELECT 1 FROM settings s WHERE s.id = @sid AND s.updated_by IS NULL);
  COMMIT TRANSACTION;
END
GO

DECLARE @intl int = (SELECT COUNT(*) FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug IN (N'intl-hellas-verona', N'intl-rayo-alcobendas', N'intl-rot-weiss-ahlen'));
DECLARE @schools int = (SELECT COUNT(*) FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug IN (N'school-ntsu-womens', N'school-wuquan-jhs', N'school-shuili-jhs', N'school-yongjing-jhs', N'school-duxing-es'));
DECLARE @missingPkg int = (SELECT COUNT(*) FROM (
    SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'俱樂部贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'學院贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'球隊贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'營隊贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'國際計畫贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'漫畫內容合作')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'商品合作')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'球迷會贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'tcrfc' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'場館冠名')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'bw' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'俱樂部贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'bw' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'青年隊贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'bw' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'球隊贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'bw' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'營隊贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'bw' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'國際計畫贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'bw' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'商品合作')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'bw' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'球迷會贊助')
    UNION ALL SELECT 1 AS ok FROM clubs c WHERE c.code = N'bw' AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = N'場館冠名')
  ) x);
IF @intl <> 3 THROW 51302, N'磐石國際夥伴應有 3 筆', 1;
IF @schools <> 5 THROW 51303, N'藍鯨建教合作學校應有 5 所', 1;
IF @missingPkg <> 0 THROW 51304, N'贊助方案名稱未補齊', 1;
PRINT N'完成：磐石國際夥伴 ' + CAST(@intl AS nvarchar(10)) + N'、藍鯨建教合作學校 ' + CAST(@schools AS nvarchar(10)) + N'、贊助方案名稱齊全。';
GO
