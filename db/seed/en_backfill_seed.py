# db/seed/en_backfill_seed.py — 主站（tcrfc）結構化內容的英文欄位回填（2026-10-05，主站英文版 C-6／S2-13）
#
# 為什麼獨立成一個區段（61），而不是回頭改各區段的 INSERT：
#   各區段的冪等寫法是「IF NOT EXISTS（zh 列）才整批 INSERT」。已經灌過的開發庫，改了來源也不會再補 en 列，
#   所以英文欄位另用「回填」寫法：只補缺的 en 列、只填空白欄位，**已有的 en 值一律不覆寫**。
#   這樣新庫（從零灌）與舊庫（已有 zh 列）跑完結果相同，且可重複執行。
#
# 範圍：主站 tcrfc（第一、二輪）＋藍鯨 bw（第三輪，檔尾；B-5 於 2026-10-05 定案：簡稱 Taichung Blue Whale、全名 Taichung Blue Whale Women's Football Club）。
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

    # ═══════════════════════════════════════════════════════════════════════════════════════════
    # 藍鯨（bw）第三輪（2026-10-05，B-5 定案：簡稱 Taichung Blue Whale、全名 Taichung Blue Whale Women's Football Club）
    # 只翻譯舊站原文（content/blue-whale/）不增事實；沒有英文來源的人名、學校、公益機構、公司名保留中文，
    # en 列該欄留 NULL 或在句內保留中文原名（前台逐欄位回退）。英文內容不得出現磐石字樣（check-club-brand-leak.mjs）。
    # ═══════════════════════════════════════════════════════════════════════════════════════════
    bw = clubs["bw"]

    def bwc(extra=""):
        return f"b.club_id = {bw}" + (f" AND {extra}" if extra else "")

    BW = "Taichung Blue Whale"
    BW_FULL = "Taichung Blue Whale Women's Football Club"
    ASSOC = "Taichung Women's Football Association"  # 與 club-copy-en-core.ts／docs/06 §1.1 一致（規劃書英文版寫 Taichung City Women's Football Association，待客戶確認）
    SIT = "Sport i Taiwan 2.0 sports hotspot"  # 運動 i 台灣 2.0 運動熱區：保守描述寫法，待客戶確認

    emit("-- （區段 61 續，不另開區段編號）藍鯨 bw 英文回填（B-5）：clubs_i18n 名稱與簡稱、各 *_i18n 的 en 列；只補缺的、不覆寫既有英文 ──")

    # clubs 沒有 club_id 欄位，以 code 找主列。⚠️ clubs_i18n 屬正式庫參照表：這一批在內容種子中會被剔除，
    # 正式庫的藍鯨 en 名稱由 club-reference-data.sql（generate-club-seed-sql.py §1）首次初始化帶入。
    fill("clubs", "clubs_i18n", "club_id", "b.code = N'bw'", {"name": BW_FULL, "short_name": BW})

    # ── 賽事系列 ──
    for zh, en in [("台灣木蘭女子足球聯賽", "Taiwan Mulan Football League"),
                   ("2025全國總統盃足球錦標賽", "2025 National President's Cup Football Championship")]:
        fill("competitions", "competitions_i18n", "competition_id", bwc(f"z.name = {esc(zh)}"), {"name": en})

    # ── 場地（venues 沒有 club_id，以繁中名稱找）。地址、交通說明含公車業者與管理單位等無英文來源的專名，不翻 ──
    for zh, en in [("台中市立豐原體育場", "Taichung Fengyuan Stadium"),
                   ("台中北屯太原足球場", "Taichung Beitun Taiyuan Football Field")]:
        fill("venues", "venues_i18n", "venue_id", f"z.name = {esc(zh)}", {"name": en})

    # ── 賽程場地（自由文字）。太原足球場各種寫法（台中／臺中、有無北屯）同一座；其餘為地名拼音＋通用場地詞，待客戶確認 ──
    TAIYUAN = "Taichung Beitun Taiyuan Football Field"
    for zh, en in [
        ("台中太原足球場", TAIYUAN), ("臺中太原足球場", TAIYUAN), ("台中北屯太原足球場", TAIYUAN),
        ("台中西屯足球場", "Taichung Xitun Football Field"),
        ("高雄楠梓足球場", "Kaohsiung Nanzih Football Field"), ("高雄市立楠梓足球場", "Kaohsiung Nanzih Football Field"),
        ("新北輔仁大學足球場", "Fu Jen Catholic University Football Field, New Taipei"),
        ("桃園龜山銘傳大學足球場", "Ming Chuan University Football Field, Guishan, Taoyuan"),
        ("花蓮美崙國中足球場", "Meilun Junior High School Football Field, Hualien"),
        ("桃園青埔足球場", "Qingpu Football Field, Taoyuan"),
    ]:
        fill("matches", "matches_i18n", "match_id", bwc(f"z.venue = {esc(zh)}"), {"venue": en})

    # ── 教練團：姓名早有英文列（資料來源羅馬拼音），只補職稱與經歷 ──
    for zh, en in [("教練", "Coach"), ("總教練", "Head Coach"), ("守門教練", "Goalkeeper Coach"),
                   ("防護員兼體能教練", "Athletic Trainer and Fitness Coach")]:
        fill("staff", "staff_i18n", "staff_id", bwc(f"z.title = {esc(zh)}"), {"title": en})
    cn_wt = "Chinese Taipei women's national team"
    first = f"{BW} women's football team"
    STAFF_BIO = {
        "呂桂花": "\n".join([
            f"2008  U-19 Asian Cup qualifiers, {cn_wt}: Head Coach",
            f"2011  Shenzhen Universiade, {cn_wt}: Head Coach",
            "2013  East Asian Games, Chinese Taipei women's football team: Coach",
            f"2013  Kazan Universiade, {cn_wt}: Head Coach",
            "2014  East Asian Cup, Chinese Taipei women's football team: Coach",
            f"2014  Incheon Asian Games (Korea), {cn_wt}: Coach",
            f"2015  Gwangju Universiade, {cn_wt}: Coach",
            f"2016  Rio Olympics, {cn_wt}: Coach",
            f"2014-2016  {first}, First Team: Head Coach",
            f"2017  {first}, First Team: Coach",
            f"2018-2024  {first}, First Team: Head Coach"]),
        "李彥廷": "\n".join([
            f"2021-2023  {BW}: Coach",
            f"2022-2023  {BW} U15 girls' team: Coach",
            f"2022-2024  {cn_wt}: Coach"]),
        "鄭雅薰": "\n".join([
            "2018  高雄陽信 women's football team: Coach",
            "2016-2022  五權國民中學 girls' football team: Head Coach",
            f"2019-2024  {first}, First Team: Coach",
            f"2022  {BW} Football Club U15 youth team: Head Coach"]),
        "張博翔": f"2019-2024  {first}: Goalkeeper Coach",
        "邱毓芳": f"2023-2024  {first}: Athletic Trainer and Fitness Coach",
    }
    for zh_name, bio in STAFF_BIO.items():
        fill("staff", "staff_i18n", "staff_id", bwc(f"z.name = {esc(zh_name)}"), {"bio": bio})

    # ── 里程碑（title 為年份，沿用）。無英文來源的選手、學校、公益機構名保留中文原名；有資料庫英文列者用其羅馬拼音 ──
    M = {
        "2014": ["Formed the Taichung Blue Whale women's football team to compete in the Mulan League",
                 "Took part in the 1st Taiwan Mulan Football League", "1,200 Facebook followers",
                 "Small artificial-turf football pitch completed at National Taiwan Sport University"],
        "2015": ["Took part in the 2nd Taiwan Mulan Football League",
                 "Helped set up the girls' football team at 台中市五權國中", "Ran a D-licence coaching course"],
        "2016": ["Took part in the 3rd Taiwan Mulan Football League",
                 "Established the central Taiwan elite women's training centre",
                 "Hosted the AFC Women's Football Festival for the first time"],
        "2017": ["Took part in the 3rd Taiwan Mulan Football League", "Held the 1st Blue Whale Cup football tournament",
                 "Elite Women's Football Training Centre renamed the Central Training Centre",
                 "Appointed JFA S-licence coach 堀野博幸 as First Team head coach", "Held a regional coaching workshop",
                 "Taichung Beitun Taiyuan Football Field opened",
                 "First Taiwan Mulan Football League title in club history", "Facebook followers reached 6,500"],
        "2018": ["Took part in the 4th Taiwan Mulan Football League", "Founded the Taichung Blue Whale football school",
                 "Central Training Centre renamed the Blue Whale Central Football Training Centre",
                 "First professional player 包欣玄 joined Taichung Blue Whale",
                 "Helped 台中市惠文高中 set up a women's football team",
                 "Second Taiwan Mulan Football League title in club history"],
        "2019": ["Took part in the 5th Taiwan Mulan Football League",
                 "Goalkeeper Tsai Ming-Jung moved abroad to Japan", "First Japanese player Tanaka Maho joined",
                 "Campaigned for the construction of the Taichung football park",
                 "Formed an esports squad to compete in the PES 2020 World Cup",
                 "Third Taiwan Mulan Football League title in club history",
                 "First time the league held Blue Whale home-match ticket sales",
                 "Passed the AFC Club Licence certification",
                 "Head coach Lu Kuei-Hua won the AFC 2019 Grassroots Leader Award",
                 "Themed match days held at home throughout the whole year for the first time",
                 "Founded the Blue Whale Girls cheerleading squad",
                 f"Took on the {SIT} promotion programme"],
        "2020": ["Took part in the 6th Taiwan Mulan Football League",
                 "First Hong Kong player 吳卓蔚 joined", "First US player 瑪芮兒 joined",
                 "Goalkeeper Cheng Ssu-Yu moved abroad to Japan", "Player 蘇育萱 moved abroad to Japan",
                 "Blue Whale home-match ticket sales held in the league",
                 f"{BW} U15 girls' team took part in the 1st Taiwan Youth League",
                 "First Taiwan Mulan Football League runners-up finish in club history",
                 f"Took on the {SIT} promotion programme"],
        "2021": ["Took part in the 8th Taiwan Mulan Football League",
                 "Second Japanese player 日高偉織 joined", "First Thai player Sornsai Pitsamai joined",
                 "First Thai goalkeeper 納塔魯亞牧塔納維奇 joined",
                 "Fourth Taiwan Mulan Football League title in club history",
                 "First Taiwan Mulan League Cup (MLC) title in club history",
                 f"{BW} U15 girls' team took part in the 2nd Taiwan Youth League",
                 f"{BW} U18 girls' team took part in the 2nd Taiwan Youth League",
                 f"Took on the {SIT} promotion programme"],
        "2022": ["Took part in the 9th Taiwan Mulan Football League",
                 "Represented Taiwan at the AFC Women's Club Championship (Thailand)",
                 "Third Thai player in club history, Intamee Silawan, joined",
                 "Held the first top-level football opening match, a success amid the pandemic",
                 f"{BW} U15 girls' team took part in the 3rd Taiwan Youth League",
                 f"{BW} U18 girls' team took part in the 3rd Taiwan Youth League",
                 f"{BW} U15 won the first Taiwan Youth League U15 girls' title",
                 "Second Taiwan Mulan Football League runners-up finish in club history",
                 f"Took on the {SIT} promotion programme"],
        "2023": ["Took part in the 10th Taiwan Mulan Football League",
                 "Groundbreaking of the Taichung football park, with the club invited to the ceremony",
                 "Player 蘇育萱 moved abroad to China", "Fourth Thai player in club history, 席菲拉萬茵樂敏, joined",
                 "Fifth Thai player in club history, Saowalak Peng-ngam, joined",
                 "Facebook followers reached 16,500",
                 "Fifth Taiwan Mulan Football League title in club history",
                 f"Took on the {SIT} promotion programme"],
        "2024": ["Took part in the 11th Taiwan Mulan Football League", f"Founded the {BW} U10 girls' team",
                 f"{BW} U10 girls' team took part in the Taichung Mayor's Cup for the first time",
                 "Invited to the Yangxin Cup International Invitational and won the title",
                 "Represented Taiwan in the 2024/25 AFC Women's Champions League and advanced from the group stage",
                 "Third Taiwan Mulan Football League runners-up finish in club history",
                 "Sixth Thai player in club history and second Thai goalkeeper, Waraporn Boonsing, joined",
                 "Saowalak Peng-ngam, the club's second foreign player, won the Taiwan Mulan Football League Golden Boot of the year",
                 f"Took on the {SIT} promotion programme"],
        "2025": ["Took part in the 12th Taiwan Mulan Football League",
                 "Represented Taiwan in the 2024-25 AFC Women's Champions League quarter-final stage and finished among the top 8 in Asia",
                 "Runners-up at the 2025 National President's Cup Football Championship",
                 "Seventh Thai player in club history and third goalkeeper, 邱瑪尼-通蒙戈, joined",
                 "Second Thai player 冼仲意 joined",
                 "Head coach Lu Kuei-Hua nominated for the AFC award for Asia's best women's team coach",
                 "Jersey carried the name of a charitable organisation for the first time: 台中惠明盲校",
                 f"Took on the {SIT} promotion programme",
                 "First interview with a British world-football magazine"],
    }
    for year, items in M.items():
        desc = "\n".join(f"{i}. {t}" for i, t in enumerate(items, 1))
        fill("milestones", "milestones_i18n", "milestone_id", bwc(f"z.title = {esc(year)}"),
             {"title": year, "description": desc})

    # ── 隊伍（en 列已存在）、行事曆球隊設定（en 列已存在）：無缺口 ──

    # ── 輪播（既有 en 標題為種子原稿；CTA 標籤與網址當初直接複製繁中，視為未翻譯）──
    fill("banners", "banners_i18n", "banner_id", bwc(f"z.title = {esc('航向世界的藍鯨')}"), {
        "title": f"{BW} rides the waves towards the open ocean",
        "subtitle": f"{BW_FULL} · Founded on 12 April 2014 · Five-time Taiwan Mulan Football League champions",
        "image_alt": "[Test] Carousel image description (image not yet uploaded)",
        "cta_1_label": f"About {BW}", "cta_1_url": "/en/about/",
        "cta_2_label": "View schedule", "cta_2_url": "/en/schedule/"})
    fill("banners", "banners_i18n", "banner_id", bwc(f"z.title = {esc('【測試】第二張輪播標題')}"), {
        "title": "[Test] Second carousel slide title", "subtitle": TEST_BODY_EN,
        "image_alt": "[Test] Carousel image description (image not yet uploaded)",
        "cta_1_label": "Join as a Player", "cta_1_url": "/en/join/player/"})

    # ── 頁面 SEO ──
    fill("pages", "pages_i18n", "page_id", bwc("b.slug = N'about/our-story'"), {
        "seo_title": f"Our Story | About {BW} | {BW_FULL}",
        "seo_description": f"{BW_FULL} was founded in Taichung in 2014 and belongs to the {ASSOC}. Learn about the team's "
                           "position and the purpose it was founded for."})
    fill("pages", "pages_i18n", "page_id", bwc("b.slug = N'about/vision'"), {
        "seo_title": f"Vision | About {BW} | {BW_FULL}",
        "seo_description": f"The vision of {BW_FULL}: endless exploration, resilience in the face of difficulty, a more "
                           "refined attitude, the most genuine impact, and a more far-reaching purpose."})
    fill("pages", "pages_i18n", "page_id", bwc("b.slug = N'about/philosophy'"), {
        "seo_title": f"Club Slogan and Training Spirit | About {BW} | {BW_FULL}",
        "seo_description": f"The club slogan and training spirit of {BW_FULL}, and the design idea behind the blue whale on the club crest."})
    fill("pages", "pages_i18n", "page_id", bwc("b.slug = N'test-draft-page'"), {
        "seo_title": "[Test] Draft page", "seo_description": TEST_BODY_EN})

    # ── FAQ（十題皆舊站原文：現場上課與收費）──
    for zh_q, en_q, en_a in [
        ("上課一定要買足球鞋嗎？", "Do I have to buy football boots to join the sessions?",
         "No. If you would like to buy a pair, you can ask the coach."),
        ("可以刷卡或數位支付嗎？", "Can I pay by card or digital payment?",
         "Card and digital payment services are not available."),
        ("U15 女子隊需要有程度才能參加嗎？", "Do I need a certain level to join the U15 girls' team?",
         "No experience is needed. As long as you learn with commitment, you will have the chance to grow together and become a player."),
        ("可以先上單堂嗎？", "Can I try a single session first?",
         "Yes. Attend the session on site and pay in cash afterwards; for the goalkeeper class, please fill in the registration form first."),
        ("U15 女子隊可以試上嗎？", "Can I take a trial session with the U15 girls' team?",
         "Yes. The fee is NT$300 per session, paid on site."),
        ("天氣不穩定怎麼知道今天要不要上課？", "How do I know whether a session is on when the weather is unsettled?",
         "For children's classes, an announcement is posted in the LINE group 1.5 hours before the session; if there is no announcement, "
         "the session goes ahead as normal. Activity-type sessions are announced separately on the Taiyuan Football Field Facebook page."),
        ("可以先試上嗎？", "Can I try a session first?", "Yes. Please come to the session on site and pay in cash."),
        ("弟弟可以跟哥哥同一班嗎？", "Can a younger brother join the same class as his older brother?",
         "Not recommended. Please consider physical and mental readiness and the intensity of the activity; joining by force makes injury more likely."),
        ("沒有經驗才能參加嗎？", "Is the class only open to people with no experience?",
         "The courses are introductory in nature, so no experience is needed. For advanced courses, please consider your own condition."),
        ("請假可以折抵退費嗎？", "Can an absence be credited or refunded?",
         "Fees are charged per session. An absence is not credited against fees and there are no make-up sessions."),
    ]:
        fill("faqs", "faqs_i18n", "faq_id", bwc(f"z.question = {esc(zh_q)}"), {"question": en_q, "answer": en_a})

    # ── 課程與活動（運動 i 台灣名稱採保守描述寫法，待客戶確認）──
    P_COURSE = f"A {SIT} course."
    P_EVENT = f"A {SIT} activity."
    for zh, name, intro in [
        ("藍鯨守門員基礎班", "Blue Whale Goalkeeper Foundation Class",
         f"{P_COURSE} Ages 7–12, open to boys and girls with places reserved for girls, limited to 10 places, 1.5 hours per session, NT$200 per session; the registration form must be completed first."),
        ("藍鯨 U15 女子足球班", "Blue Whale U15 Girls' Football Class",
         f"{P_COURSE} Ages 13 and above, girls and women only, every Friday, 1.5 hours per session, NT$200 per session; individual registration on site."),
        ("社區幼幼足球班", "Community Toddler Football Class",
         f"{P_COURSE} Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site."),
        ("兒童足球日", "Children's Football Day",
         f"{P_EVENT} Kindergarten and elementary school children, open to boys and girls, 3 hours per event, free of charge, groups only (please apply by private message on the Taiyuan Football Field Facebook page)."),
        ("社區足球學校（小藍鯨）", "Community Football School (Little Blue Whale)",
         "A community football school founded in 2017 and nicknamed Little Blue Whale. It emphasises the joy of sport, physical health, teamwork and learning football skills, "
         "on an enclosed dedicated football pitch under experienced coaches. No audition, no test and no joining fee; individual registration on site. "
         "Regular price NT$300 per session, special flat price NT$200 per session, paid in cash on site for each single session."),
        ("足球人才教練暨 TDS 守門員人才培訓（教練講習）", "Football Talent Coach and TDS Goalkeeper Talent Training (Coaching Course)",
         "24 and 25 August 2025 (2 days). Coaches who hold a coaching qualification and currently lead a team are admitted first; NT$800 per person, paid in cash on the day (covers materials, insurance, lunch and so on). "
         "Supervising authorities: Sports Administration, Ministry of Education, and Taichung City Government Sports Bureau; organiser: National Taiwan Sport University; "
         "co-organisers: Taichung Women's Football Association and the Taichung Blue Whale women's football team; supporting organisation: Chinese Taipei Football Association."),
        ("藍鯨 U12 女子足球班", "Blue Whale U12 Girls' Football Class",
         f"{P_COURSE} Girls and women only, every Monday, Wednesday and Friday, 1.5 hours per session, NT$200 per session; individual registration on site."),
        ("野團成人足球賽", "Pick-up Adult Football Matches",
         f"{P_EVENT} Junior high school age and above, open to men and women, 2–3 hours per event, pitch fee NT$100 per person; register on site as a group (8 or more people) or as an individual."),
        ("藍鯨 U8 足球教室", "Blue Whale U8 Football Class",
         f"{P_COURSE} Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site."),
        ("藍鯨 U10 足球教室", "Blue Whale U10 Football Class",
         f"{P_COURSE} Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site."),
        ("藍鯨足球自由日", "Blue Whale Free Play Day",
         f"{P_EVENT} All ages, open to boys and girls, 1.5 hours per event, free of charge, no registration needed."),
        ("幼兒社區足球班", "Community Early-Years Football Class",
         f"{P_COURSE} Open to boys and girls, every Monday and Wednesday, 1.5 hours per session, NT$200 per session; individual registration on site."),
    ]:
        fill("programs", "programs_i18n", "program_id", bwc(f"z.name = {esc(zh)}"), {"name": name, "intro": intro})

    # ── 自建行事曆事件（第一筆為真實活動，標題內的活動名無英文來源故保留中文）──
    fill("calendar_custom_events", "calendar_custom_events_i18n", "calendar_custom_event_id",
         bwc(f"z.title = {esc('2024 台中女子足球節「夏洛特的下午茶」')}"), {
             "title": "2024 Taichung Women's Football Festival 「夏洛特的下午茶」",
             "description": "Sport i Taiwan and the Taichung Women's Football Festival. Taichung Beitun Taiyuan Football Field; check-in at 15:30, "
                            "start at 16:00, finish at 18:00. For girls in grades 1–5 of elementary school, with a promotion group and a competition group; "
                            "a public-interest promotional event, free to attend throughout."})
    for zh, en in [("【測試】公開訓練", "[Test] Open training session"), ("【測試】球迷見面會", "[Test] Fan meet-and-greet")]:
        fill("calendar_custom_events", "calendar_custom_events_i18n", "calendar_custom_event_id",
             bwc(f"z.title = {esc(zh)}"), {"title": en, "description": TEST_BODY_EN})

    # ── 夥伴：只補有英文來源者（政府機關為既有英文名、品牌名本身含英文）；其餘公司與機構名無英文來源，維持中文 ──
    for zh, en in [
        ("臺中市政府", "Taichung City Government"),
        ("臺中市政府運動局", "Taichung City Government Sports Bureau"),
        ("教育部體育署", "Sports Administration, Ministry of Education"),
        ("國立臺灣體育運動大學體育學系", "Department of Physical Education, National Taiwan Sport University"),
        ("寶礦力水得 Pocari Sweat", "Pocari Sweat"),
        ("MIE Taiwan", "MIE Taiwan"),
        ("SKECHERS／思克威爾股份有限公司", "SKECHERS"),
        ("Defunc Taiwan", "Defunc Taiwan"),
    ]:
        fill("partners", "partners_i18n", "partner_id", bwc(f"z.name = {esc(zh)}"), {"name": en})

    # ── 新聞（三篇【測試】）──
    for slug, title in [("bw-test-news-club", f"[Test] {BW} club news sample"),
                        ("bw-test-news-community", f"[Test] {BW} community event sample"),
                        ("bw-test-news-match", f"[Test] {BW} match report sample")]:
        fill("articles", "articles_i18n", "article_id", bwc(f"b.slug = {esc(slug)}"),
             {"title": title, "summary": TEST_BODY_EN})

    # ── 站台設定（僅對外顯示者）。site.contact_hours 為測試值不翻（同主站）──
    fill("settings", "settings_i18n", "setting_id", bwc("b.setting_key = N'site.league_name'"), {"value": "Taiwan Mulan Football League"})
    fill("settings", "settings_i18n", "setting_id", bwc("b.setting_key = N'site.league_short_name'"), {"value": "Mulan League"})
    fill("settings", "settings_i18n", "setting_id", bwc("b.setting_key = N'seo.title_template'"), {"value": f"{{title}} | {BW_FULL}"})
    fill("settings", "settings_i18n", "setting_id", bwc("b.setting_key = N'seo.default_description'"), {
        "value": f"{BW_FULL}, part of the {ASSOC}, is one of the teams of the Taiwan Mulan Football League. The club hopes to lift "
                 "the grassroots football culture in Taichung and drive the development of women's football in central Taiwan."})
    fill("settings", "settings_i18n", "setting_id", bwc("b.setting_key = N'geo.llms_positioning'"), {
        "value": f"{BW_FULL} belongs to the {ASSOC} and is one of the teams of the Taiwan Mulan Football League. The blue whale is its "
                 "symbol of a faster, stronger and more modern style of football, and the club hopes to lift the grassroots football culture "
                 f"in Taichung and drive the development of women's football in central Taiwan. Slogan: {BW} rides the waves towards the open ocean."})
    fill("settings", "settings_i18n", "setting_id", bwc("b.setting_key = N'geo.llms_facts_summary'"), {
        "value": "Founded on 12 April 2014; plays in the Taiwan Mulan Football League; home ground is Taichung Beitun Taiyuan Football Field "
                 "(current), and the home ground in the founding period was Taichung Fengyuan Stadium; a development pathway with the first team "
                 "and the youth teams (U15 / U12) running in parallel."})

    # ── 【測試】商品、會籍、球迷活動、合集、特約店家（既有 en 列只缺說明欄）──
    fill("collections", "collections_i18n", "collection_id", bwc(f"z.name = {esc('【測試】藍鯨系列')}"), {
        "name": "[Test] Blue Whale collection", "narrative": "[Test] Placeholder brand narrative for the Blue Whale collection."})
    fill("fan_events", "fan_events_i18n", "fan_event_id", bwc(f"z.name = {esc('【測試】藍鯨球迷日')}"), {
        "name": "[Test] Blue Whale fan day", "description": "[Test] Demo fan event hosted by Blue Whale.",
        "location": "[Test] No. 1, Test Road, Xitun District, Taichung City"})
    fill("membership_plans", "membership_plans_i18n", "membership_plan_id", bwc(f"z.name = {esc('【測試】藍鯨球迷會員（單人）')}"), {
        "name": "Blue Whale Fan Club (Single)", "benefit_note": "[Test] Includes one membership card and one welcome jersey."})
    fill("products", "products_i18n", "product_id", bwc(f"z.name = {esc('【測試】藍鯨球衣')}"), {
        "name": "[Test] Blue Whale jersey", "narrative": "[Test] Placeholder product narrative.", "tags": "[Test],Demo"})
    fill("partner_stores", "partner_stores_i18n", "partner_store_id", bwc(f"z.name = {esc('【測試】藍鯨示範店家')}"), {
        "name": "[Test] Blue Whale Demo Store", "offer_content": "[Test] Exclusive offer for Blue Whale members"})
