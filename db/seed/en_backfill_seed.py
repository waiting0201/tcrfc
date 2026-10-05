# db/seed/en_backfill_seed.py — 主站（tcrfc）結構化內容的英文欄位回填（2026-10-05，主站英文版 C-6／S2-13）
#
# 為什麼獨立成一個區段（61），而不是回頭改各區段的 INSERT：
#   各區段的冪等寫法是「IF NOT EXISTS（zh 列）才整批 INSERT」。已經灌過的開發庫，改了來源也不會再補 en 列，
#   所以英文欄位另用「回填」寫法：只補缺的 en 列、只填空白欄位，**已有的 en 值一律不覆寫**。
#   這樣新庫（從零灌）與舊庫（已有 zh 列）跑完結果相同，且可重複執行。
#
# 範圍：只有主站 tcrfc。藍鯨（bw）不補——英文全名卡 B-5、英文全新生產 C-10（docs/06 §1.1）。
#       新聞文章（articles_i18n）本輪不翻。球員姓名沒有英文來源者不音譯（見回報）。
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

    emit("-- ── 61. 英文欄位回填（*_i18n 的 en 列；只補缺的、不覆寫既有英文；僅 tcrfc，藍鯨與新聞不在此列） ──")

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
