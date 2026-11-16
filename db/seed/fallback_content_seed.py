#!/usr/bin/env python3
# db/seed/fallback_content_seed.py — 官網「後台沒資料時改顯示寫死內容」的真實內容，改成後台資料（區段 60b，2026-11-13）
#
# 起因：使用者 2026-11-13 拍板，官網各處「後台沒資料就顯示寫死版」的後備要拿掉（docs/23 H-1 B 類與 C-5）。
# 拿掉之前，寫死版裡「真實」的內容必須先進後台資料，否則正式站會突然少一塊。本檔只放這一輪盤點後
# 「確實是真實、後台資料模型也裝得下、而且種子與資料遷移都還沒有」的那幾項；其他項目的處置與理由見
# db/seed/README.md「官網寫死後備轉後台資料（區段 60b）」一節。
#
# 內容（全部有中英兩份；沒有圖片，圖片需營運人員在後台上傳，種子不帶圖——見 README）：
#   ① 磐石國際夥伴 3 筆（Hellas Verona／Rayo Alcobendas／Rot-Weiss Ahlen）  → partners（類型「國際夥伴」）
#   ② 藍鯨建教合作學校 5 所                                               → partners（類型「教育夥伴」）
#   ③ 贊助方案名稱（磐石 9、藍鯨 8；規劃書 §3.9 9.4 的方案名稱，只有名稱，沒有任何金額與權益）→ sponsor_packages
#   ④ 磐石榮譽：成立首年頭銜（2024 全國乙級聯賽 冠軍）                       → achievements
#   ⑤ 頁尾簡介／社群連結／藍鯨聯絡 Email（只補「從沒被人編輯過的空值」）      → settings
#
# 冪等策略（與其他區段不同的地方）：
#   • 夥伴用 (club_id, slug)；
#   • 贊助方案用「同俱樂部、同繁中名稱」判斷（不是 slug）——本機種子的 tcrfc 已有同名的【測試】方案
#     （test-package-*，名稱就是規劃書的方案名、內文為測試），名稱相同就不再補一份，避免官網出現 18 張卡；
#     正式庫沒有測試方案，所以會補齊 9 張；
#   • 榮譽用 (club_id, year, 繁中賽事名)；球隊 D1 或任何球季不存在就整筆略過（正式庫由後台建好球隊後再補）；
#   • settings 只在「列不存在」或「值為空且 updated_by IS NULL（沒有任何人儲存過）」時寫入，
#     後台人員存過的值（包含刻意清空）一律不動。
#
# 兩個入口共用同一份定義與 SQL：
#   ① db/seed/generate-club-seed-sql.py 呼叫 emit_all()（本機種子與驗收用內容種子，區段 60b）；
#   ② db/seed/generate-fallback-content-migration.py 產生 db/migrations/20261113_fallback-content-import.sql
#      （已經有資料的庫——正式庫、本機開發庫——內容種子的「擁有的表必須全空」條件不成立，只能走資料遷移）。

# 國際夥伴：country 欄位同時帶中英（「義大利 Italy」），因為國際路徑頁（3.4）依 country 比對地區（日本／香港／中國，其餘歐洲）。
# 合作內容只寫新聞稿標題已公開的事實，不寫簽署日期（新聞日期不等於簽署日）、不寫金額。
INTL_PARTNERS = [
    # slug, 名稱, 國家, 合作內容 zh, 合作內容 en, logo 替代文字 zh, logo 替代文字 en, 排序
    ("intl-hellas-verona", "Hellas Verona FC", "義大利 Italy",
     "義大利甲級球會。雙方簽署合作備忘錄，推動台義足球交流。",
     "A Serie A club from Italy. The two clubs signed a memorandum of cooperation to promote Taiwan–Italy football exchange.",
     "義大利 Hellas Verona FC 隊徽", "Hellas Verona FC crest (Italy)", 20),
    ("intl-rayo-alcobendas", "Rayo Ciudad Alcobendas CF", "西班牙 Spain",
     "西班牙球會。雙方簽訂國際足球合作備忘錄。",
     "A club from Spain. The two clubs signed an international football cooperation memorandum.",
     "西班牙 Rayo Ciudad Alcobendas CF 隊徽", "Rayo Ciudad Alcobendas CF crest (Spain)", 21),
    ("intl-rot-weiss-ahlen", "Rot-Weiss Ahlen", "德國 Germany",
     "德國球會。雙方簽署合作諒解備忘錄。",
     "A club from Germany. The two clubs signed a memorandum of understanding on cooperation.",
     "德國 Rot-Weiss Ahlen 隊徽", "Rot-Weiss Ahlen crest (Germany)", 22),
]

# 藍鯨建教合作學校（content/blue-whale/club-profile.md 資訊表「建教合作」；2015 年協助五權國中女足隊成立見同檔沿革）。
# start_on 不填：只知道年份，填 01-01 會在夥伴頁顯示出不存在的精確日期；年份寫進合作內容文字。
BW_SCHOOLS = [
    # slug, 名稱 zh, 名稱 en, 內容 zh, 內容 en, 排序
    ("school-ntsu-womens", "國立臺灣體育運動大學女子足球隊", "National Taiwan Sport University women's football team",
     "建教合作（女子足球隊）", "Industry-academia cooperation (women's football team)", 101),
    ("school-wuquan-jhs", "臺中市立五權國民中學女子足球隊", "Taichung Municipal Wuquan Junior High School women's football team",
     "建教合作（女子足球隊）；2015 年協助球隊成立", "Industry-academia cooperation (women's football team); helped establish the team in 2015", 102),
    ("school-shuili-jhs", "南投縣立水里國民中學女子足球隊", "Nantou County Shuili Junior High School women's football team",
     "建教合作（女子足球隊）", "Industry-academia cooperation (women's football team)", 103),
    ("school-yongjing-jhs", "彰化縣立永靖國民中學女子足球隊", "Changhua County Yongjing Junior High School women's football team",
     "建教合作（女子足球隊）", "Industry-academia cooperation (women's football team)", 104),
    ("school-duxing-es", "臺中市篤行國小女子足球隊", "Taichung Duxing Elementary School women's football team",
     "建教合作（女子足球隊）", "Industry-academia cooperation (women's football team)", 105),
]

# 贊助方案：規劃書 §3.9 9.4 的方案名稱（磐石九種；藍鯨不設漫畫為八種，且「學院贊助」稱「青年隊贊助」）。
# 只有名稱——規劃書與前台都沒有任何金額、權益、適合對象，那些由營運人員在後台填；價格預設不公開。
PACKAGES = {
    "tcrfc": [
        ("package-club", "俱樂部贊助", "Club Sponsorship"),
        ("package-academy", "學院贊助", "Academy Sponsorship"),
        ("package-team", "球隊贊助", "Team Sponsorship"),
        ("package-camp", "營隊贊助", "Camp Sponsorship"),
        ("package-international", "國際計畫贊助", "International Program Sponsorship"),
        ("package-comic", "漫畫內容合作", "Manga Content Partnership"),
        ("package-merch", "商品合作", "Merchandise Partnership"),
        ("package-fanclub", "球迷會贊助", "Fan Club Sponsorship"),
        ("package-naming", "場館冠名", "Venue Naming Rights"),
    ],
    "bw": [
        ("package-club", "俱樂部贊助", "Club Sponsorship"),
        ("package-youth", "青年隊贊助", "Youth Team Sponsorship"),
        ("package-team", "球隊贊助", "Team Sponsorship"),
        ("package-camp", "營隊贊助", "Camp Sponsorship"),
        ("package-international", "國際計畫贊助", "International Program Sponsorship"),
        ("package-merch", "商品合作", "Merchandise Partnership"),
        ("package-fanclub", "球迷會贊助", "Fan Club Sponsorship"),
        ("package-naming", "場館冠名", "Venue Naming Rights"),
    ],
}

# 榮譽：磐石成立首年頭銜。名稱與 site.founding_title（全國乙級聯賽冠軍）一致；英文取客戶里程碑文件的英譯
# （National Second Division champions）。achievements 沒有圖片欄位，獎盃照無處可放（見 README）。
HONORS_TCRFC = [
    # 年, 賽事 zh, 名次 zh, 賽事 en, 名次 en
    (2024, "全國乙級聯賽", "冠軍", "National Second Division", "Champions"),
]

# 頁尾簡介、社群連結、聯絡 Email：與 generate-club-seed-sql.py 的 CONTACT_SEEDS 同值（皆為俱樂部官方公開資訊；
# 藍鯨 Email 出自舊站頁尾，content/blue-whale/club-profile.md，本來就在版控內）。重複定義是刻意的：
# 資料遷移要能獨立執行，不能 import 另一支產生器；改 CONTACT_SEEDS 時記得同步改這裡（兩處值相同才不會讓兩個入口補出不同的值）。
SETTINGS = {
    "tcrfc": {
        "values": {
            "site.social_facebook": "https://www.facebook.com/TCRFC2024",
            "site.social_instagram": "https://www.instagram.com/tcr_fc_2024",
            "site.social_youtube": "https://www.youtube.com/@TCRFC-2024",
        },
        "footer_blurb": ("台中磐石足球俱樂部致力於透過專業模式，培育選手追求卓越，讓世界看見台灣足球。",
                         "Taichung Rock FC develops players who pursue excellence through a professional model, so the world can see Taiwan football."),
    },
    "bw": {
        "values": {
            "site.social_facebook": "https://www.facebook.com/tbwfc",
            "site.social_instagram": "https://instagram.com/tcbw2014",
            "site.social_youtube": "https://www.youtube.com/@user-xu1wm3xx1w",
            "site.social_line": "https://lin.ee/CS65qCR",
            "site.contact_email": "fbbh2014@gmail.com",
        },
        "footer_blurb": (
            "隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。",
            "Taichung Blue Whale Women's Football Club, part of the Taichung City Women's Football Association, is one of the teams of the Taiwan Mulan Football League. Taking the blue whale as its symbol, it stands for a faster, stronger and more modern style of football, and hopes to lift the grassroots football culture in Taichung and drive the development of women's football in central Taiwan."),
    },
}

SECTION_TITLE = "60b. 官網寫死後備轉後台資料：磐石國際夥伴、藍鯨建教合作學校、贊助方案名稱、磐石成立首年榮譽、頁尾簡介與社群（真實內容，無圖片）"


def emit_all(*, emit, block, esc, new_id, clubs):
    """clubs：{'tcrfc': '(SELECT id FROM clubs …)', 'bw': …}（純量子查詢字串）。每個 block 是獨立 GO 批次。"""
    emit(f"-- ── {SECTION_TITLE} ──")
    tc, bw = clubs["tcrfc"], clubs["bw"]

    def partner(club_code, club_sq, slug, ptype, country, zh, en, content_zh, content_en, alt_zh, alt_en, order):
        pid = new_id("partner", club_code, slug)
        alt_cols = ", logo_alt" if alt_zh else ""
        alt_zh_v = f", {esc(alt_zh)}" if alt_zh else ""
        alt_en_v = f", {esc(alt_en)}" if alt_zh else ""
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = {club_sq} AND slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(pid)};
  INSERT INTO partners (id, club_id, slug, partner_type, country, show_in_footer, show_on_home, sort_order)
  VALUES (@id, {club_sq}, {esc(slug)}, {esc(ptype)}, {esc(country)}, 0, 0, {order});
  INSERT INTO partners_i18n (partner_id, locale, name, content{alt_cols}) VALUES (@id, N'zh-Hant', {esc(zh)}, {esc(content_zh)}{alt_zh_v});
  INSERT INTO partners_i18n (partner_id, locale, name, content{alt_cols}) VALUES (@id, N'en', {esc(en)}, {esc(content_en)}{alt_en_v});
END
""")

    for slug, name, country, c_zh, c_en, a_zh, a_en, order in INTL_PARTNERS:
        partner("tcrfc", tc, slug, "國際夥伴", country, name, name, c_zh, c_en, a_zh, a_en, order)
    for slug, zh, en, c_zh, c_en, order in BW_SCHOOLS:
        partner("bw", bw, slug, "教育夥伴", None, zh, en, c_zh, c_en, None, None, order)

    for club_code, club_sq in (("tcrfc", tc), ("bw", bw)):
        for order, (slug, zh, en) in enumerate(PACKAGES[club_code]):
            pkg_id = new_id("sponsor_package", club_code, slug)
            block(f"""
IF NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant'
               WHERE p.club_id = {club_sq} AND i.name = {esc(zh)})
BEGIN
  INSERT INTO sponsor_packages (id, club_id, slug, is_price_public, sort_order, status)
  VALUES ({esc(pkg_id)}, {club_sq}, {esc(slug)}, 0, {order}, N'published');
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES ({esc(pkg_id)}, N'zh-Hant', {esc(zh)});
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES ({esc(pkg_id)}, N'en', {esc(en)});
END
""")

    for year, comp_zh, placing_zh, comp_en, placing_en in HONORS_TCRFC:
        aid = new_id("achievement", "tcrfc", "real", str(year), comp_zh)
        block(f"""
DECLARE @team uniqueidentifier = (SELECT id FROM teams WHERE club_id = {tc} AND code = N'D1');
DECLARE @season uniqueidentifier = (SELECT TOP 1 id FROM seasons WHERE club_id = {tc} ORDER BY start_on DESC);
IF @team IS NOT NULL AND @season IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM achievements a JOIN achievements_i18n i ON i.achievement_id = a.id AND i.locale = N'zh-Hant'
                   WHERE a.club_id = {tc} AND a.year = {year} AND i.competition_name = {esc(comp_zh)})
BEGIN
  INSERT INTO achievements (id, club_id, season_id, team_id, year) VALUES ({esc(aid)}, {tc}, @season, @team, {year});
  INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing) VALUES ({esc(aid)}, N'zh-Hant', {esc(comp_zh)}, {esc(placing_zh)});
  INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing) VALUES ({esc(aid)}, N'en', {esc(comp_en)}, {esc(placing_en)});
END
""")

    for club_code, club_sq in (("tcrfc", tc), ("bw", bw)):
        cfg = SETTINGS[club_code]
        for key, value in cfg["values"].items():
            sid = new_id("setting", club_code, key)
            block(f"""
IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = {club_sq} AND setting_key = {esc(key)})
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES ({esc(sid)}, {club_sq}, {esc(key)}, {esc(value)}, N'site');
ELSE
  UPDATE settings SET setting_value = {esc(value)}
  WHERE club_id = {club_sq} AND setting_key = {esc(key)} AND setting_value IS NULL AND updated_by IS NULL;
""")
        key = "site.footer_blurb"
        sid = new_id("setting", club_code, key)
        zh_v, en_v = cfg["footer_blurb"]
        block(f"""
IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = {club_sq} AND setting_key = {esc(key)})
  INSERT INTO settings (id, club_id, setting_key, setting_group) VALUES ({esc(sid)}, {club_sq}, {esc(key)}, N'site');
""")
        for locale, value in (("zh-Hant", zh_v), ("en", en_v)):
            block(f"""
DECLARE @sid uniqueidentifier = (SELECT id FROM settings WHERE club_id = {club_sq} AND setting_key = {esc(key)});
IF @sid IS NOT NULL
BEGIN
  IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = {esc(locale)})
    INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, {esc(locale)}, {esc(value)});
  ELSE
    UPDATE settings_i18n SET value = {esc(value)}
    WHERE setting_id = @sid AND locale = {esc(locale)} AND NULLIF(value, N'') IS NULL
      AND EXISTS (SELECT 1 FROM settings s WHERE s.id = @sid AND s.updated_by IS NULL);
END
""")
