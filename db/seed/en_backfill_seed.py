# db/seed/en_backfill_seed.py — 主站（tcrfc）結構化內容的英文欄位回填（2026-10-05，主站英文版 C-6／S2-13）
#
# 為什麼獨立成一個區段（61），而不是回頭改各區段的 INSERT：
#   各區段的冪等寫法是「IF NOT EXISTS（zh 列）才整批 INSERT」。已經灌過的開發庫，改了來源也不會再補 en 列，
#   所以英文欄位另用「回填」寫法：只補缺的 en 列、只填空白欄位，**已有的 en 值一律不覆寫**。
#   這樣新庫（從零灌）與舊庫（已有 zh 列）跑完結果相同，且可重複執行。
#
# 範圍：只有主站 tcrfc。藍鯨（bw）不補——英文全名卡 B-5、英文全新生產 C-10（docs/06 §1.1）。
#       新聞標題（articles_i18n.title）第二輪補（僅 tcrfc；本文不存在、藍鯨不補）。球員姓名沒有英文來源者不音譯（見回報）。
#
# 回填規則（fill）：
#   1. 沒有 en 列 → INSERT 一列（只帶有值的欄位）。
#   2. 已有 en 列 → 只更新「NULL、空字串、或與繁中完全相同（＝種子當初直接複製、從未翻譯）」的欄位；
#      有人在後台改過的英文不會被動到。
#   3. 用繁中列的自然鍵（標題、名稱、鍵名）找主列，不寫死 GUID，所以資料列 id 與產生器無關。
#
# 英文用詞一律照 docs/06-conventions.md §1.1：聯賽名沿用 `Enterprise Premier League`（正式名待客戶確認），
# 單元名、職稱、位置照表；【測試】內容維持「[Test] …」標記，讓 /en/ 驗收時不是整頁中文。
#
# 🔴 與 apps/api 測試的相依（見交付回報）：LocalizationFallbackTests／AppContractGapsTests／BusinessPublicEndpointsTests
#    曾以「種子沒有這些英文」為前提；補上之後那幾條斷言要同步改。

TEST_BODY_EN = "[Test] This is test content. Please replace it in the back office before launch."


# ── 新聞標題英文（2026-10-05 第二輪；slug → title）。sentence case；聯賽名沿用 Enterprise Premier League；
# 無英文來源的對手隊／人名／學校／地名一律保留中文原名。
L = "Enterprise Premier League"
S = "National Second Division"
T = "Taichung Rock FC"
TR = "Taichung Rock FC Reserves"
NEWS_TITLES_EN = {
"2026-08-10-international-000": f"{T} and AS Trenčín deepen youth development partnership, opening a new chapter in Taiwan-Slovakia football exchange",
"2026-05-24-match-001": f"{L}: {T} 3-0 銘傳大學",
"2026-05-17-match-002": f"{L}: {T} 1-2 陽信北競",
"2026-05-10-match-003": f"{L}: {T} 2-4 南市台鋼",
"2026-05-09-match-004": f"{S}: 陽信北競 Reserves 5-0 {TR}",
"2026-05-03-match-005": f"{S}: {TR} 0-2 銘傳Desafio",
"2026-05-03-match-006": f"{L}: {T} 1-0 大同足球",
"2026-04-27-match-007": f"{L}: {T} 2-2 台電",
"2026-04-25-match-008": f"{S}: 桃園國際 1-1 {TR}",
"2026-04-19-match-009": f"{L}: 台中未來 0-1 {T}",
"2026-04-18-match-010": f"{S}: {TR} 4-1 灣島",
"2026-04-12-match-011": f"{L}: 新北航源 0-2 {T}",
"2026-04-11-match-012": f"{S}: 高雄先鋒 4-1 {TR}",
"2026-03-22-match-013": f"{S}: {TR} 5-2 新北航源輔大",
"2026-03-09-match-014": f"{L}: {T} 1-1 銘傳大學",
"2026-03-02-match-015": f"{L}: {T} 3-1 陽信北競",
"2026-02-06-international-016": f"Widening international horizons: 5 {T} players called up to train in Italy by 萊尼亞戈",
"2026-01-12-community-017": f"{T} teams up with Subkarma on local charity, donating English books to 潭秀非營利幼兒園",
"2025-12-22-match-018": f"{L}: {T} 0-0 南市台鋼",
"2025-12-14-match-019": f"{L}: {T} 0-1 大同足球隊",
"2025-12-14-match-020": f"{L}: 台灣電力 1-2 {T}",
"2025-12-09-match-021": f"{L}: 台灣電力 1-2 {T}",
"2025-12-07-match-022": f"{L}: 台灣電力 1-2 {T}",
"2025-12-01-match-023": f"{L}: 台中FUTURO 4-0 {T}",
"2025-11-24-match-024": f"{S}: 銘傳大學Desafio 1-3 {TR}",
"2025-11-24-match-025": f"{L}: 新北航源 3-0 {T}",
"2025-11-04-international-026": f"First step in the Hellas Verona partnership: {T} players depart for training in Italy",
"2025-11-03-club-027": f"陳曉明 appointed Technical Advisor of {T}",
"2025-11-02-match-028": f"{L}: 銘傳大學 0-1 {T}",
"2025-11-01-match-029": f"{S}: {TR} 2-0 桃園國際",
"2025-10-26-match-030": f"{S}: 灣島 2-7 {TR}",
"2025-10-26-match-031": f"{L}: 陽信北競 2-2 {T}",
"2025-09-28-match-032": f"{L}: 南市台鋼 3-3 {T}",
"2025-09-21-match-033": f"{L}: 大同足球 2-2 {T}",
"2025-09-20-match-034": f"{S}: 新北航源輔大 1-0 {TR}",
"2025-09-14-match-035": f"{L}: {T} 0-2 台灣電力",
"2025-08-24-match-036": f"{L}: {T} 0-1 台中未來",
"2025-08-18-match-037": f"{L}: {T} 2-1 新北航源",
"2025-07-31-intcup-038": f"{T} International Cup: {T} vs 東方",
"2025-07-31-intcup-039": f"{T} International Cup: 紅白艾倫 vs 台中Futuro",
"2025-07-30-international-040": f"{T} to sign memorandum of cooperation with Serie A club Hellas Verona to advance Taiwan-Italy football exchange",
"2025-07-29-intcup-041": f"{T} International Cup: {T} vs 紅白艾倫",
"2025-07-29-intcup-042": f"{T} International Cup: 東方 vs 台中Futuro",
"2025-07-27-international-043": f"{T} signs memorandum of understanding with Germany's Rot Weiss Ahlen",
"2025-07-27-intcup-044": f"{T} International Cup: {T} vs 台中未來",
"2025-07-27-intcup-045": f"{T} International Cup: 紅白艾倫 vs 東方",
"2025-07-25-club-046": f"2025 {T} International Cup press conference",
"2025-07-21-community-047": "Taichung City Government Sports Bureau flag presentation ceremony",
"2025-07-12-club-048": f"2025 {T} International Cup kicks off",
"2025-07-08-club-049": f"{T} and 龜記茗品 continue their partnership",
"2025-06-11-community-050": f"{T} attends the graduation ceremony at 南投縣雙龍國小",
"2025-05-17-match-051": f"President's Cup quarter-final: 台灣電力 1-1 (penalties 4-2) {T}",
"2025-05-13-match-052": f"President's Cup: {T} 2-2 高大國光",
"2025-05-13-match-053": f"President's Cup: {TR} 8-1 銘傳B",
"2025-05-04-match-054": f"President's Cup: {T} 1-5 南市台鋼藍",
"2025-05-04-match-055": f"President's Cup: {TR} 1-2 台中FUTURO",
"2025-05-03-camps-056": f"2025 {T} Cup football invitational",
"2025-04-29-match-057": f"President's Cup: 桃園國際灰 0-7 {TR}",
"2025-04-28-match-058": f"President's Cup: 陽信北競 Reserves 2-3 {T}",
"2025-04-12-match-059": f"President's Cup: {T} 2-1 新北輔大",
"2025-04-12-match-060": f"President's Cup: {TR} 1-5 南市台鋼綠",
"2025-04-11-club-061": f"周宇杰 joins {T}",
"2025-04-11-club-062": f"廖奕盛 joins {T}",
"2025-04-11-club-063": f"Germany-based 王義友 joins {T}",
"2025-04-08-international-064": "孫恩祈 invited to stay on in Spain until the end of the season",
"2025-04-07-match-065": f"Club friendly: 陽信北競 2-1 {T}",
"2025-04-07-international-066": "梁顥騰 completes his training trip in Spain, deeply inspired",
"2025-04-03-match-067": f"Fourth warm-up match: 陽信北競 vs {T}",
"2025-04-02-match-068": f"Two {T} squads set out for the 2025 National President's Cup",
"2025-03-29-match-069": f"Warm-up match: {T} 1-3 陽信北競",
"2025-03-22-match-070": f"Third warm-up match: {T} vs 陽信北競",
"2025-03-17-club-071": "高冠宇 called up to the Chinese Taipei national team",
"2025-03-15-match-072": f"Warm-up match: {T} vs 台中Futuro",
"2025-03-13-match-073": f"{T} take on 台中Futuro in a warm-up match",
"2025-03-04-international-074": "Two players join training at RC Alcobendas in Spain",
"2025-02-28-international-075": "Four players join training with the Tokyo University of Agriculture football team",
"2025-02-19-international-076": "Japan training camp wrap-up",
"2025-02-04-international-077": "楊朝景 transfers to Hong Kong Premier League club 九龍城",
"2025-01-07-club-078": f"{T} recognised by the Taichung City Government Sports Bureau for cooperation and naming rights",
"2024-12-18-club-079": f"{T} conditionally passes top-tier club certification",
"2024-12-18-club-080": "Coach 林 wins Best Coach award, 楊朝景 wins Golden Boot",
"2024-12-07-match-081": f"{S}: {T} 9-0 銘傳大學Desafio",
"2024-11-05-international-082": f"{T} reaches cooperation agreement with RC Alcobendas",
}


def emit_all(*, emit, block, esc, clubs):
    tcrfc = clubs["tcrfc"]

    def fill(main, i18n, fk, key, values):
        """key：WHERE 條件，主表別名 b、繁中列別名 z。values：{欄位: 英文值}（None 的欄位略過）。"""
        values = {c: v for c, v in values.items() if v is not None}
        cols = ", ".join(values)
        vals = ", ".join(esc(v) for v in values.values())
        sets = ",\n    ".join(
            f"e.{c} = CASE WHEN e.{c} IS NULL OR e.{c} = N'' OR e.{c} = z.{c} THEN {esc(v)} ELSE e.{c} END"
            for c, v in values.items())
        block(f"""
INSERT INTO {i18n} ({fk}, locale, {cols})
SELECT b.id, N'en', {vals}
FROM {main} b JOIN {i18n} z ON z.{fk} = b.id AND z.locale = N'zh-Hant'
WHERE {key}
  AND NOT EXISTS (SELECT 1 FROM {i18n} e WHERE e.{fk} = b.id AND e.locale = N'en');

UPDATE e SET
    {sets}
FROM {i18n} e
JOIN {main} b ON b.id = e.{fk}
JOIN {i18n} z ON z.{fk} = b.id AND z.locale = N'zh-Hant'
WHERE e.locale = N'en' AND {key};
""")

    def club(extra=""):
        return f"b.club_id = {tcrfc}" + (f" AND {extra}" if extra else "")

    emit("-- ── 61. 英文欄位回填（*_i18n 的 en 列；只補缺的、不覆寫既有英文；僅 tcrfc，藍鯨不在此列） ──")

    # ── 賽事系列：聯賽正式英文名待客戶確認，沿用 site-facts 既有的 Enterprise Premier League ──
    fill("competitions", "competitions_i18n", "competition_id",
         club(f"z.name = {esc('企業甲級足球聯賽')}"),
         {"name": "Enterprise Premier League"})

    # ── 賽程場地（matches_i18n.venue，自由文字）：場地英文名待客戶確認，採地名拼音＋通用場地詞 ──
    for zh, en in [
        ("TBC", "TBC"),
        ("西屯足球場", "Xitun Football Field"),
        ("台北田徑場", "Taipei Athletics Stadium"),
        ("台南市立足球場", "Tainan Municipal Football Stadium"),
        ("楠梓足球場", "Nanzih Football Field"),
        ("汐止綜合運動場", "Xizhi Sports Complex"),
        ("輔仁大學足球場", "Fu Jen Catholic University Football Field"),
    ]:
        fill("matches", "matches_i18n", "match_id", club(f"z.venue = {esc(zh)}"), {"venue": en})

    # ── 教練團職稱（姓名沒有英文來源者不音譯，只補職稱；name 留 NULL 讓前台回退中文原名）──
    for zh, en in [
        ("守門員教練", "Goalkeeper Coach"),
        ("教練", "Coach"),
        ("總教練", "Head Coach"),
        ("青訓教練", "Youth Development Coach"),
        ("青訓總監", "Youth Development Director"),
        ("顧問", "Advisor"),
        ("體能教練", "Fitness Coach"),
    ]:
        fill("staff", "staff_i18n", "staff_id", club(f"z.title = {esc(zh)}"), {"title": en})

    # ── 首頁輪播（draft）：既有 en 列的 CTA 標籤與網址當初直接複製繁中，視為未翻譯 ──
    fill("banners", "banners_i18n", "banner_id", club(f"z.title = {esc('在地扎根 放眼世界')}"), {
        "title": "LOCAL ROOTS. GLOBAL PATHWAYS.",
        "subtitle": "Taichung Rock FC · Founded in 2024 · 2024 National Second Division champions",
        "image_alt": "[Test] Carousel image description (image not yet uploaded)",
        "cta_1_label": "About TCRFC", "cta_1_url": "/en/about/",
        "cta_2_label": "View schedule", "cta_2_url": "/en/schedule/"})
    fill("banners", "banners_i18n", "banner_id", club(f"z.title = {esc('【測試】第二張輪播標題')}"), {
        "title": "[Test] Second carousel slide title",
        "subtitle": TEST_BODY_EN,
        "image_alt": "[Test] Carousel image description (image not yet uploaded)",
        "cta_1_label": "Join as a Player", "cta_1_url": "/en/join/player/"})

    # ── 頁面 SEO（pages_i18n）。頁面內文區塊（page_blocks.content，json）不在側表範圍 ──
    fill("pages", "pages_i18n", "page_id", club("b.slug = N'about/vision-mission'"), {
        "seo_title": "Vision & Mission | About TCRFC | Taichung Rock FC",
        "seo_description": "The vision and mission of Taichung Rock FC: developing local Taichung players through a "
                           "professional pathway towards the professional stage, and showing the world Taiwan through football."})
    fill("pages", "pages_i18n", "page_id", club("b.slug = N'about/philosophy'"), {
        "seo_title": "Our Philosophy | About TCRFC | Taichung Rock FC",
        "seo_description": "The football philosophy and five core values of Taichung Rock FC: Players First, Excellence, "
                           "Global Pathways, Community and Integrity."})
    fill("pages", "pages_i18n", "page_id", club("b.slug = N'test-draft-page'"), {
        "seo_title": "[Test] Draft page", "seo_description": TEST_BODY_EN})

    # ── FAQ（全為【測試】題）──
    for zh_topic, en_topic in [
        ("加入球隊", "joining the team"), ("學院招生", "Academy admissions"),
        ("課程與營隊報名", "program and camp registration"), ("費用與退費", "fees and refunds"),
        ("試訓", "trials"), ("國際發展與海外球員", "international pathways and overseas players"),
        ("女子足球", "women's football"), ("球迷會與商品", "the fan club and merchandise"),
        ("合作與贊助", "partnerships and sponsorship"), ("其他", "other topics"),
    ]:
        fill("faqs", "faqs_i18n", "faq_id",
             club(f"z.question = {esc('【測試】' + zh_topic + '的常見問題範例？')}"),
             {"question": f"[Test] Sample question about {en_topic}?", "answer": TEST_BODY_EN})

    # ── 自建行事曆事件（【測試】）──
    for zh, en in [
        ("【測試】新賽季記者會", "[Test] New season press conference"),
        ("【測試】球迷見面會", "[Test] Fan meet-and-greet"),
        ("【測試】每週公開訓練", "[Test] Weekly open training session"),
        ("【測試】場地休館公告", "[Test] Venue closure notice"),
        ("【測試】內部工作會議（不公開）", "[Test] Internal staff meeting (not public)"),
    ]:
        fill("calendar_custom_events", "calendar_custom_events_i18n", "calendar_custom_event_id",
             club(f"z.title = {esc(zh)}"), {"title": en, "description": TEST_BODY_EN})

    # ── 夥伴（【測試】）──
    for zh, name, content in [
        ("【測試】示範策略夥伴", "[Test] Demo Strategic Partner", "[Test] A strategic partnership to advance local football development together."),
        ("【測試】示範國際夥伴", "[Test] Demo International Partner", "[Test] International scouting and exchange partnership."),
        ("【測試】示範訓練夥伴", "[Test] Demo Training Partner", "[Test] Physical and technical training partnership."),
        ("【測試】示範教育夥伴", "[Test] Demo Education Partner", "[Test] Campus and community football promotion partnership."),
        ("【測試】示範品牌夥伴", "[Test] Demo Brand Partner", None),
    ]:
        fill("partners", "partners_i18n", "partner_id", club(f"z.name = {esc(zh)}"), {"name": name, "content": content})

    # ── 贊助商與贊助活動（【測試】）──
    sponsor_body = "[Test] Sponsorship details: for demonstration only. Please replace in the back office before launch."
    for zh, name in [
        ("【測試】示範主贊助商", "[Test] Demo Main Sponsor"),
        ("【測試】示範官方贊助商", "[Test] Demo Official Sponsor"),
        ("【測試】示範支持夥伴", "[Test] Demo Supporting Partner"),
    ]:
        fill("sponsors", "sponsors_i18n", "sponsor_id", club(f"z.name = {esc(zh)}"), {"name": name, "content": sponsor_body})
    for zh, title, summary in [
        ("【測試】示範贊助活動：開幕戰球迷日", "[Test] Demo sponsor activation: opening match fan day",
         "[Test] About 500 attendees on site and 100,000 social media impressions (demo figures)."),
        ("【測試】示範贊助活動：青訓體驗營", "[Test] Demo sponsor activation: youth training experience camp",
         "[Test] 60 children took part (demo figures)."),
    ]:
        fill("sponsor_activations", "sponsor_activations_i18n", "sponsor_activation_id",
             club(f"z.title = {esc(zh)}"), {"title": title, "result_summary": summary})

    # ── 媒體專區（【測試】）──
    press_desc = ("[Test] Demo description. This entry has no actual file (placeholder); "
                  "please upload a file in the back office and then set it to visible.")
    for zh, en in [
        ("【測試】示範新聞稿", "[Test] Demo press release"),
        ("【測試】示範品牌識別包", "[Test] Demo brand kit"),
        ("【測試】示範高解析圖", "[Test] Demo high-resolution images"),
    ]:
        fill("press_resources", "press_resources_i18n", "press_resource_id",
             club(f"z.title = {esc(zh)}"), {"title": en, "description": press_desc})

    # ── 特約店家（【測試】；地址沒有繁中來源者不補）──
    fill("partner_stores", "partner_stores_i18n", "partner_store_id", club(f"z.name = {esc('【測試】示範健身房')}"), {
        "name": "[Test] Demo Gym", "offer_content": "[Test] One free trial class (draft, not yet listed)"})

    # ── 慈善與社會影響（【測試】）──
    for zh, name, intro in [
        ("【測試】示範公益團體甲", "[Test] Demo Charity A", "[Test] This is a demo charity profile."),
        ("【測試】示範公益團體乙", "[Test] Demo Charity B", "[Test] Another demo charity."),
    ]:
        fill("charities", "charities_i18n", "charity_id", club(f"z.name = {esc(zh)}"), {"name": name, "intro": intro})
    # charity_programs_i18n.content 是 json（內含 {zh,en} 區塊），不在此回填
    program_common = {"target_audience": "[Test] Children in rural areas",
                      "donation_content": "[Test] 50 footballs and 100 training bibs (demo figures)"}
    for zh, name in [
        ("【測試】示範慈善計畫：偏鄉足球捐贈", "[Test] Demo charity program: football donations to rural areas"),
        ("【測試】示範慈善計畫：公益義賽", "[Test] Demo charity program: charity match"),
        ("【測試】示範慈善計畫（草稿）", "[Test] Demo charity program (draft)"),
    ]:
        fill("charity_programs", "charity_programs_i18n", "charity_program_id",
             club(f"z.name = {esc(zh)}"), {"name": name, **program_common})
    for zh, name, unit in [
        ("【測試】合作公益團體數", "[Test] Partner charities", "orgs"),
        ("【測試】累計捐助項次", "[Test] Total donation items", "items"),
        ("【測試】累計捐助金額（不公開）", "[Test] Total donation amount (not public)", "NTD"),
    ]:
        fill("impact_metrics", "impact_metrics_i18n", "impact_metric_id",
             club(f"z.name = {esc(zh)}"), {"name": name, "unit": unit})
    fill("impact_records", "impact_records_i18n", "impact_record_id",
         club(f"z.donation_content = {esc('【測試】足球 50 顆、訓練背心 100 件（示範數字）')}"), {
             "donation_content": "[Test] 50 footballs and 100 training bibs (demo figures)",
             "location": "[Test] Demo location: Nantou County", "brief_description": "[Test] Demo summary."})
    fill("impact_records", "impact_records_i18n", "impact_record_id",
         club(f"z.donation_content = {esc('【測試】獎助學金 5 名（示範數字）')}"), {
             "donation_content": "[Test] 5 scholarships (demo figures)",
             "location": "[Test] Demo location: Taichung City"})

    # ── 會員抽獎（【測試】；member_draws_i18n 是正式庫禁用表，prod 內容種子會自動剔除這兩批）──
    fill("member_draws", "member_draws_i18n", "member_draw_id", club(f"z.name = {esc('【測試】主場賽事日球迷抽獎')}"), {
        "name": "[Test] Match-day fan prize draw", "prize_description": "[Test] One signed jersey",
        "rules": "[Test] Placeholder rules: eligibility is based on holding a valid fan club membership with this club at the "
                 "reference time; members holding memberships with both clubs may enter each club's draw separately.",
        "notes": "[Test] Placeholder notes."})
    fill("member_draws", "member_draws_i18n", "member_draw_id", club(f"z.name = {esc('【測試】直播球迷抽獎（草稿）')}"), {
        "name": "[Test] Livestream fan prize draw (draft)", "prize_description": "[Test] Three fan scarves"})

    # ── 站台設定（僅對外顯示者）。site.founding_date_display 刻意不補：SiteFactsTests 用它驗證「缺英文回退中文」；
    #    site.contact_hours／contact_phone 是測試值，不翻 ──
    fill("settings", "settings_i18n", "setting_id", club("b.setting_key = N'site.founding_title'"),
         {"value": "National Second Division champions"})

    # ── 新聞標題（只補 title；body／summary／SEO 繁中皆空故不補；用 slug 找主列）。
    #    對手隊、人名、學校、地名沒有英文來源者保留中文原名，不音譯 ──
    for slug, title in NEWS_TITLES_EN.items():
        fill("articles", "articles_i18n", "article_id", club(f"b.slug = {esc(slug)}"), {"title": title})
