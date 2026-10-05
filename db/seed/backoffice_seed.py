#!/usr/bin/env python3
# db/seed/backoffice_seed.py — 後台已完成模組的種子資料（2026-09-30，使用者要求「後台打開就有資料可以看、可以測試」）
#
# 由 generate-club-seed-sql.py 在輸出尾端呼叫 `emit_all(...)`；本檔只放「資料定義＋把資料轉成冪等 T-SQL」，
# 不自己連資料庫、不自己 print。所有 SQL 沿用主腳本的 emit／block／esc／new_id 與「業務自然鍵
# 找不到才 INSERT」的冪等慣例（見主腳本檔頭）。
#
# 🔴 資料分兩種，界線一律在資料上標明，不得混淆：
#   ① 真實：來源是 content/blue-whale/*（舊站原文）、apps/web/shared/utils/club-copy.ts 已核實文案、
#      舊官網 URL 盤點。每一筆註解都寫來源。
#   ② 測試值：沒有真實來源的欄位。一律以「【測試】」前綴（文字欄位）或 example.com／全 0 電話
#      （不能加前綴的欄位）標明，並登記在 db/seed/README.md「測試值清單」——正式資料上線前逐一替換。
#
# 🔴 個資與肖像（CLAUDE.md 全域規定第 7 條，repo 公開）：本檔不含任何未成年學員姓名或照片、
#   不含新的球員／教練姓名；不含真實聯絡人。tcrfc 沒有可核實的 Email，用 example.com。
#
# 🔴 圖片：本檔只寫文字。banners.image_key 是 NOT NULL，沒有可上傳的公開素材與 Azurite 上傳腳本，
#   所以輪播以 draft 狀態＋明顯的佔位鍵寫入（公開端點只回 published，不會顯示壞圖），見 README。

import hashlib
import json
from urllib.parse import quote

TEST = "【測試】"
TEST_BODY = "【測試】這是測試用內容，正式內容上線前請於後台替換。"


def jdump(obj) -> str:
    return json.dumps(obj, ensure_ascii=False, separators=(",", ":"))


def bi(zh, en=None):
    """區塊內容的雙語文字欄位（docs/14：page_blocks.content 雙語靠 JSON 內巢狀物件）。"""
    return {"zh": zh, "en": en}


# ============================================================================
# B1 頁面（pages）——真實文案取自 apps/web/shared/utils/club-copy.ts（已核實）
# ============================================================================
PAGES = {
    "tcrfc": [
        {
            "slug": "about/vision-mission",
            "status": "published",
            "seo_title": "願景與使命 Vision & Mission｜關於台中磐石｜台中磐石足球俱樂部",
            "seo_description": "台中磐石足球俱樂部的願景與使命：透過專業化培育體系，讓台中在地選手邁向職業舞台，並以足球讓世界看見台灣。",
            "blocks": [
                ("text", {"body": bi("<h2>願景</h2><p>從台中出發，培育本土選手邁向職業舞台，成為在地榮耀的來源。</p>")}),
                ("text", {"body": bi("<h2>使命</h2><p>以扎實的訓練體系與國際連結，讓世界看見台灣足球。</p>")}),
                ("quote", {"text": bi("在地扎根．放眼世界", "LOCAL ROOTS. GLOBAL PATHWAYS."),
                          "attribution": bi("台中磐石足球俱樂部")}),
                ("cta", {"text": bi("想進一步認識台中磐石？"), "buttonLabel": bi("關於台中磐石"),
                        "buttonUrl": "/zh/about/"}),
            ],
        },
        {
            "slug": "about/philosophy",
            "status": "published",
            "seo_title": "足球理念 Our Philosophy｜關於台中磐石｜台中磐石足球俱樂部",
            "seo_description": "台中磐石足球俱樂部的足球理念與五大核心價值：以球員為本、追求卓越、國際發展、社區共好、誠信專業。",
            "blocks": [
                ("text", {"body": bi("<p>透過專業模式，培育選手追求卓越，讓世界看見台灣足球。</p>")}),
                ("text", {"body": bi("<h2>五大核心價值</h2><ul><li>以球員為本</li><li>追求卓越</li><li>國際發展</li>"
                                     "<li>社區共好</li><li>誠信專業</li></ul>")}),
            ],
        },
        {
            "slug": "test-draft-page",
            "status": "draft",
            "seo_title": TEST + "草稿頁面",
            "seo_description": TEST_BODY,
            "blocks": [("text", {"body": bi("<p>" + TEST_BODY + "</p>")})],
        },
    ],
    "bw": [
        {
            "slug": "about/our-story",
            "status": "published",
            "seo_title": "我們的故事｜關於台中藍鯨｜台中藍鯨女子足球隊",
            "seo_description": "台中藍鯨女子足球隊 2014 年成立於台中，隸屬臺中市女子足球協會。認識這支球隊的定位與成立宗旨。",
            "blocks": [
                # 逐字取自 club-copy.ts OUR_STORY_BODY_BW（節錄自 content/blue-whale/club-profile.md §1）
                ("text", {"body": bi("<p>隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，簡稱為台中藍鯨，是台灣木蘭足球聯賽的球隊之一。"
                                     "以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態，重視團隊合作，鯨翅為台灣意象代表"
                                     "引領台灣足球向前邁進。台中藍鯨希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。</p>")}),
            ],
        },
        {
            "slug": "about/vision",
            "status": "published",
            "seo_title": "發展願景｜關於台中藍鯨｜台中藍鯨女子足球隊",
            "seo_description": "台中藍鯨女子足球隊的發展願景：無止盡的探索、不怕難的堅韌、更細膩的態度、最真實的影響、更深遠之目的。",
            "blocks": [
                ("quote", {"text": bi("追尋卓越 止於至善（Pursuit of Brilliance）")}),
                # 逐字取自 club-copy.ts VISION_ITEMS.bw（club-profile.md §5 五節「行動目標」句）
                ("steps", {"items": [
                    {"title": bi("無止盡的探索"), "description": bi("提昇及普及大台中足球水準，吸收更專業精進足球技術，追上亞洲足球技術水平迎接世界潮流。")},
                    {"title": bi("不怕難的堅韌"), "description": bi("創造足球運動文化與風氣，以球迷為本讓足球比賽呈現更有水準，場內技術提升，場外足球比賽氛圍更加提升。")},
                    {"title": bi("更細膩的態度"), "description": bi("增加足球選手發展管道，讓選手有更好的發展空間，家長支持，學校支持，政府支持，產業支持，民眾支持。")},
                    {"title": bi("最真實的影響"), "description": bi("建立台中為台灣足球之都的美名與榮耀，健康正向的足球風氣，連結喜愛足球運動的球迷及選手所追求的足球夢想。")},
                    {"title": bi("更深遠之目的"), "description": bi("持續回饋社會，致力為人們創造更美好的生活，深信我們能帶來改變，幫助人們以全新的方式彼此分享與連結，讓世界更加和諧。")},
                ]}),
            ],
        },
        {
            "slug": "about/philosophy",
            "status": "published",
            "seo_title": "俱樂部口號與培訓精神｜關於台中藍鯨｜台中藍鯨女子足球隊",
            "seo_description": "台中藍鯨女子足球隊的俱樂部口號與培訓精神，以及隊徽「藍鯨」象徵的設計理念。",
            "blocks": [
                # 逐字取自 club-copy.ts PHILOSOPHY_QUOTES_BW（club-profile.md §3）
                ("quote", {"text": bi("以「藍鯨」作為象徵，代表追求更快、更堅強、更現代化的足球型態、重視團隊合作，鯨翅為台灣意象代表引領台灣足球向前邁進。"),
                          "attribution": bi("隊徽設計理念")}),
                ("quote", {"text": bi("藍色的天空是我們心中夢想的方向，閃爍的陽光是走向夢想的力量，草地上揮灑汗水是成長過往 堅定信仰，"
                                      "有你在身旁 就不再徬徨，此時此刻，我們與我們的球迷站在一起。一起迎向世界。"),
                          "attribution": bi("俱樂部口號")}),
                ("quote", {"text": bi("別害怕 勇敢去闖，邁開步伐乘風破浪，就算遍體鱗傷 也要逆風飛翔，抬起頭 夢在前方，"
                                      "越過那重重的高牆 沒有誰能阻擋，眼神越是發光，世界都是我的舞台。"),
                          "attribution": bi("培訓精神")}),
            ],
        },
        {
            "slug": "test-draft-page",
            "status": "draft",
            "seo_title": TEST + "草稿頁面",
            "seo_description": TEST_BODY,
            "blocks": [("text", {"body": bi("<p>" + TEST_BODY + "</p>")})],
        },
    ],
}

# ============================================================================
# B3 首頁輪播（banners）——文字取自 club-copy.ts getHomeHero（已核實）；圖片沒有，見檔頭
# ============================================================================
BANNER_PLACEHOLDER_KEY = "seed-placeholder/no-image"
BANNERS = {
    "tcrfc": [
        {"title": ("在地扎根 放眼世界", "LOCAL ROOTS. GLOBAL PATHWAYS."),
         "subtitle": ("台中磐石足球俱樂部 · 2024 年創立 · 2024 全國乙級聯賽冠軍", None),
         "alt": (TEST + "輪播圖片說明（尚未上傳圖片）", None),
         "cta1": ("認識台中磐石", "/zh/about/"), "cta2": ("查看賽程", "/zh/schedule/")},
        {"title": (TEST + "第二張輪播標題", None),
         "subtitle": (TEST_BODY, None),
         "alt": (TEST + "輪播圖片說明（尚未上傳圖片）", None),
         "cta1": ("加入球隊", "/zh/join/player/"), "cta2": None},
    ],
    "bw": [
        {"title": ("航向世界的藍鯨", "Taichung Blue Whale rides the waves towards the open ocean"),
         "subtitle": ("台中藍鯨女子足球隊 · 2014 年 4 月 12 日成立 · 隊史五度奪得木蘭聯賽冠軍", None),
         "alt": (TEST + "輪播圖片說明（尚未上傳圖片）", None),
         "cta1": ("認識台中藍鯨", "/zh/about/"), "cta2": ("查看賽程", "/zh/schedule/")},
        {"title": (TEST + "第二張輪播標題", None),
         "subtitle": (TEST_BODY, None),
         "alt": (TEST + "輪播圖片說明（尚未上傳圖片）", None),
         "cta1": ("加入球隊", "/zh/join/player/"), "cta2": None},
    ],
}

# ============================================================================
# B4 常見問題（faqs）
#   tcrfc：沒有真實問答來源 → 十個分類各一題測試題（【測試】）。
#   bw：真實——content/blue-whale/programs.md §3 常見問答 8 題＋squad/youth-teams.md U15 FAQ 2 題（舊站原文）。
#   category 只能用 faq_categories 十個 slug；藍鯨的 academy-admission（12.2）依規格關閉，不掛。
#   embeds 的 slot code 見 FAQ_EMBED_SLOTS（academy_admission／program_detail／trials／sponsorship）。
# ============================================================================
_TCRFC_FAQ_TOPICS = [
    ("join-team", "加入球隊", ["trials"]),
    ("academy-admission", "學院招生", ["academy_admission"]),
    ("programs-camps", "課程與營隊報名", ["program_detail"]),
    ("fees-refunds", "費用與退費", ["program_detail"]),
    ("trials", "試訓", ["trials"]),
    ("international", "國際發展與海外球員", []),
    ("womens-football", "女子足球", []),
    ("fan-club-merchandise", "球迷會與商品", []),
    ("partnerships-sponsorship", "合作與贊助", ["sponsorship"]),
    ("other", "其他", []),
]
FAQS = {
    "tcrfc": [
        {
            "slug": f"test-faq-{i + 1:02d}",
            "categories": [cat],
            "embeds": embeds,
            "q": (f"{TEST}{label}的常見問題範例？", None),
            "a": (TEST_BODY, None),
        }
        for i, (cat, label, embeds) in enumerate(_TCRFC_FAQ_TOPICS)
    ],
    "bw": [
        {"slug": "bw-trial-class", "categories": ["trials", "programs-camps"], "embeds": ["trials", "program_detail"],
         "q": ("可以先試上嗎？", None), "a": ("可以，請直接到現場上課然後現金繳費。", None)},
        {"slug": "bw-single-class", "categories": ["programs-camps", "fees-refunds"], "embeds": ["program_detail"],
         "q": ("可以先上單堂嗎？", None), "a": ("可以，現場上課後現金繳費；守門員班請先填報名表單。", None)},
        {"slug": "bw-weather", "categories": ["programs-camps"], "embeds": ["program_detail"],
         "q": ("天氣不穩定怎麼知道今天要不要上課？", None),
         "a": ("兒童班課前 1.5 小時在 LINE 群組公告；若無公告為正常上課；活動類型課程另於太原足球場 FB 粉絲頁公告。", None)},
        {"slug": "bw-leave-refund", "categories": ["fees-refunds"], "embeds": [],
         "q": ("請假可以折抵退費嗎？", None), "a": ("單堂收費，請假無折抵學費或補課。", None)},
        {"slug": "bw-payment-methods", "categories": ["fees-refunds"], "embeds": [],
         "q": ("可以刷卡或數位支付嗎？", None), "a": ("沒有刷卡、數位支付服務。", None)},
        {"slug": "bw-siblings", "categories": ["programs-camps"], "embeds": [],
         "q": ("弟弟可以跟哥哥同一班嗎？", None),
         "a": ("不建議，請評估身體與心理狀態及運動強度，強行加入容易受傷。", None)},
        {"slug": "bw-football-shoes", "categories": ["programs-camps"], "embeds": ["program_detail"],
         "q": ("上課一定要買足球鞋嗎？", None), "a": ("不需要，如要購買可詢問教練。", None)},
        {"slug": "bw-beginners", "categories": ["join-team", "programs-camps"], "embeds": [],
         "q": ("沒有經驗才能參加嗎？", None),
         "a": ("課程是推廣性質，沒有經驗也可以參加；進階課程請衡量自身狀態。", None)},
        {"slug": "bw-u15-trial", "categories": ["trials", "join-team"], "embeds": [],
         "q": ("U15 女子隊可以試上嗎？", None), "a": ("可以試上，費用 300 元／次（現場支付）。", None)},
        {"slug": "bw-u15-experience", "categories": ["join-team"], "embeds": [],
         "q": ("U15 女子隊需要有程度才能參加嗎？", None),
         "a": ("沒有經驗也可以參加，只要認真學習有機會一起成長成為選手。", None)},
    ],
}

# ============================================================================
# P1／P2 課程與梯次（programs／sessions）
#   tcrfc：沒有梯次真實資料 → 全部測試（名稱含【測試】）。
#   bw：真實——content/blue-whale/programs.md §1（社區足球學校）、§2（運動 i 台灣 2.0 十種課程）、§4（教練講習）。
#   ⚠️ weekly_schedule 是 json 欄位，內容必須是合法 JSON（AdminProgramsRepository.ValidateContentJson）；
#      前台目前把它當字串直接印出（S1-15，未解析），這是前台顯示落差，回報中列出。
#   ⚠️ 舊站原文自相矛盾處「照抄不動」（U8 年齡與 U10 相同；教練講習報名期限年份），只用不矛盾的欄位。
# ============================================================================
VENUE_TCRFC = "西屯"
VENUE_BW = "太原"
PROGRAMS = {
    "tcrfc": [
        {"slug": "test-childrens-training-mixed-age", "type": "children_training", "age": (5, 12), "status": "published",
         "name": TEST + "兒童足球訓練（混齡體驗班）", "intro": TEST_BODY, "venue": VENUE_TCRFC,
         "sessions": [
             {"status": "開放", "start": "2026-10-12", "end": "2027-01-18", "weekly": {"mon": "18:00-19:30", "wed": "18:00-19:30"},
              "capacity": 20, "enrolled": 8, "price": 4800, "early_price": 4200, "early_until": "2026-10-05",
              "opens": "2026-09-20T00:00:00", "closes": "2026-10-10T23:59:00"},
             {"status": "已結束", "start": "2026-04-06", "end": "2026-06-29", "weekly": {"mon": "18:00-19:30"},
              "capacity": 20, "enrolled": 20, "price": 4800},
         ]},
        {"slug": "test-summer-camp", "type": "summer_camp", "age": (8, 14), "status": "published",
         "name": TEST + "暑期足球營", "intro": TEST_BODY, "venue": VENUE_TCRFC,
         "sessions": [
             {"status": "額滿", "start": "2027-07-05", "end": "2027-07-09", "weekly": {"mon-fri": "09:00-16:00"},
              "capacity": 30, "enrolled": 30, "price": 6000},
             {"status": "候補", "start": "2027-07-19", "end": "2027-07-23", "weekly": {"mon-fri": "09:00-16:00"},
              "capacity": 30, "enrolled": 30, "price": 6000},
         ]},
        {"slug": "test-winter-camp", "type": "winter_camp", "age": (8, 14), "status": "published",
         "name": TEST + "寒假足球營", "intro": TEST_BODY, "venue": VENUE_TCRFC,
         "sessions": [
             {"status": "開放", "start": "2027-01-25", "end": "2027-01-29", "weekly": {"mon-fri": "09:00-16:00"},
              "capacity": 25, "enrolled": 3, "price": 5500},
         ]},
        {"slug": "test-specialist-goalkeeper", "type": "specialist_training", "age": (10, 15), "status": "published",
         "name": TEST + "守門員專項訓練", "intro": TEST_BODY, "venue": VENUE_TCRFC,
         "sessions": [
             {"status": "開放", "start": "2026-11-07", "end": "2026-12-19", "weekly": {"sat": "09:00-11:00"},
              "capacity": 12, "enrolled": 5, "price": 3600},
         ]},
        {"slug": "test-school-community", "type": "school_community", "age": (None, None), "status": "published",
         "name": TEST + "校園與社區足球推廣", "intro": TEST_BODY, "venue": None,
         "sessions": [
             {"status": "開放", "start": "2026-11-20", "end": "2026-11-20", "weekly": None,
              "capacity": None, "enrolled": 0, "price": None},
         ]},
        {"slug": "test-draft-program", "type": "children_training", "age": (6, 10), "status": "draft",
         "name": TEST + "草稿課程", "intro": TEST_BODY, "venue": None, "sessions": []},
    ],
    "bw": [
        {"slug": "bw-community-football-school", "type": "children_training", "age": (None, None), "status": "published",
         "name": "社區足球學校（小藍鯨）",
         "intro": "2017 年成立的社區足球學校，暱稱小藍鯨。強調運動樂趣、身體健康、團隊合作和足球技能學習；封閉式專用足球場、"
                  "由經驗豐富的教練指導。免試上、免測試、免入會費，現場個人報名；原價 300 元／堂，優待價均一價 200 元／堂，"
                  "現場單次單堂現金繳交。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": None, "price": 200}]},
        {"slug": "bw-toddler-football", "type": "children_training", "age": (3, 4), "status": "published",
         "name": "社區幼幼足球班", "intro": "運動 i 台灣 2.0 運動熱區課程。男女不拘，每週一、三，1.5 小時／堂，200 元／堂，現場個人報名。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": {"mon": "1.5 小時", "wed": "1.5 小時"}, "price": 200}]},
        {"slug": "bw-kids-community-football", "type": "children_training", "age": (6, 8), "status": "published",
         "name": "幼兒社區足球班", "intro": "運動 i 台灣 2.0 運動熱區課程。男女不拘，每週一、三，1.5 小時／堂，200 元／堂，現場個人報名。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": {"mon": "1.5 小時", "wed": "1.5 小時"}, "price": 200}]},
        {"slug": "bw-u8-football-class", "type": "children_training", "age": (9, 10), "status": "published",
         "name": "藍鯨 U8 足球教室", "intro": "運動 i 台灣 2.0 運動熱區課程。男女不拘，每週一、三，1.5 小時／堂，200 元／堂，現場個人報名。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": {"mon": "1.5 小時", "wed": "1.5 小時"}, "price": 200}]},
        {"slug": "bw-u10-football-class", "type": "children_training", "age": (9, 10), "status": "published",
         "name": "藍鯨 U10 足球教室", "intro": "運動 i 台灣 2.0 運動熱區課程。男女不拘，每週一、三，1.5 小時／堂，200 元／堂，現場個人報名。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": {"mon": "1.5 小時", "wed": "1.5 小時"}, "price": 200}]},
        {"slug": "bw-u12-girls-class", "type": "children_training", "age": (10, 12), "status": "published",
         "name": "藍鯨 U12 女子足球班", "intro": "運動 i 台灣 2.0 運動熱區課程。限女性，每週一、三、五，1.5 小時／堂，200 元／堂，現場個人報名。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": {"mon": "1.5 小時", "wed": "1.5 小時", "fri": "1.5 小時"}, "price": 200}]},
        {"slug": "bw-u15-girls-class", "type": "children_training", "age": (13, None), "status": "published",
         "name": "藍鯨 U15 女子足球班", "intro": "運動 i 台灣 2.0 運動熱區課程。13 歲以上，限女性，每週五，1.5 小時／堂，200 元／堂，現場個人報名。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": {"fri": "1.5 小時"}, "price": 200}]},
        {"slug": "bw-goalkeeper-basic", "type": "specialist_training", "age": (7, 12), "status": "published",
         "name": "藍鯨守門員基礎班", "intro": "運動 i 台灣 2.0 運動熱區課程。7–12 歲，男女不拘、女生保留錄取，限額 10 位，1.5 小時／堂，200 元／堂，"
                                         "須先填寫報名表單。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": None, "capacity": 10, "price": 200}]},
        {"slug": "bw-coach-workshop-2025", "type": "specialist_training", "age": (None, None), "status": "published",
         "name": "足球人才教練暨 TDS 守門員人才培訓（教練講習）",
         "intro": "114 年 8 月 24 日、25 日共 2 天，具備教練資格且目前有實際帶隊之教練優先錄取；每人新臺幣 800 元，當日現場繳交"
                  "（含資料、保險、午餐等）。指導機關：教育部體育署、臺中市政府運動局；辦理機構：國立臺灣體育運動大學；"
                  "合辦機關：臺中市女子足球協會、台中藍鯨女子足球隊；協辦機關：中華民國足球協會。",
         "venue": VENUE_BW,
         "sessions": [{"status": "已結束", "start": "2025-08-24", "end": "2025-08-25", "weekly": None, "price": 800}]},
        {"slug": "bw-football-free-day", "type": "school_community", "age": (None, None), "status": "published",
         "name": "藍鯨足球自由日", "intro": "運動 i 台灣 2.0 運動熱區活動。全年齡、男女不拘，1.5 小時／次，免費，不需報名。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": None, "price": 0}]},
        {"slug": "bw-children-football-day", "type": "school_community", "age": (None, None), "status": "published",
         "name": "兒童足球日", "intro": "運動 i 台灣 2.0 運動熱區活動。幼兒園、國小，男女不拘，3 小時／次，免費，限團體"
                                      "（請至太原足球場 FB 粉絲頁私訊申請）。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": None, "price": 0}]},
        {"slug": "bw-adult-football-match", "type": "school_community", "age": (None, None), "status": "published",
         "name": "野團成人足球賽", "intro": "運動 i 台灣 2.0 運動熱區活動。國中年齡以上、男女不拘，2–3 小時／次，場地費 100 元／人；"
                                          "團體（8 人以上）或個人現場報名。",
         "venue": VENUE_BW,
         "sessions": [{"status": "開放", "weekly": None, "price": 100}]},
    ],
}

# ============================================================================
# C4 積分榜（standings）——沒有任何真實積分來源（磐石球季才剛開打、藍鯨舊站沒有積分表）→ 全部測試
# ============================================================================
STANDINGS = {
    "tcrfc": ("2026-27", [(1, 3, 9), (2, 3, 7), (3, 3, 6), (4, 3, 4), (5, 3, 3), (6, 3, 1)]),
    "bw": ("2023", [(1, 15, 40), (2, 15, 34), (3, 15, 28), (4, 15, 21), (5, 15, 12), (6, 15, 5)]),
}

# ============================================================================
# L2 自建事件（calendar_custom_events）
#   tcrfc：全測試。bw：一筆真實（2024 台中女子足球節，programs.md §5 ＋ news-index #4 兩處互證 2024-07-13）＋ 兩筆測試。
# ============================================================================
CALENDAR_EVENTS = {
    "tcrfc": [
        {"type": "press_conference", "team": "D1", "venue": VENUE_TCRFC, "starts": "2026-10-08T14:00:00", "ends": "2026-10-08T15:30:00",
         "all_day": 0, "public": 1, "title": (TEST + "新賽季記者會", None), "desc": (TEST_BODY, None)},
        {"type": "fan_meet", "team": "D1", "venue": VENUE_TCRFC, "starts": "2026-10-31T15:00:00", "ends": "2026-10-31T17:00:00",
         "all_day": 0, "public": 1, "title": (TEST + "球迷見面會", None), "desc": (TEST_BODY, None)},
        {"type": "open_training", "team": "D1", "venue": VENUE_TCRFC, "starts": "2026-10-14T17:00:00", "ends": "2026-10-14T18:30:00",
         "all_day": 0, "public": 1, "repeat": "weekly", "repeat_until": "2026-12-30", "exceptions": ["2026-11-25"],
         "title": (TEST + "每週公開訓練", None), "desc": (TEST_BODY, None)},
        {"type": "closure_notice", "team": None, "venue": VENUE_TCRFC, "starts": "2026-12-25T00:00:00", "ends": None,
         "all_day": 1, "public": 1, "title": (TEST + "場地休館公告", None), "desc": (TEST_BODY, None)},
        {"type": "other", "team": None, "venue": None, "starts": "2026-10-20T10:00:00", "ends": "2026-10-20T11:00:00",
         "all_day": 0, "public": 0, "title": (TEST + "內部工作會議（不公開）", None), "desc": (TEST_BODY, None)},
    ],
    "bw": [
        {"type": "other", "team": "BW1", "venue": VENUE_BW, "starts": "2024-07-13T16:00:00", "ends": "2024-07-13T18:00:00",
         "all_day": 0, "public": 1,
         "title": ("2024 台中女子足球節「夏洛特的下午茶」", None),
         "desc": ("運動 i 台灣 & 台中女子足球節。臺中北屯太原足球場，15:30 報到、16:00 開始、18:00 結束；對象為國小 1–5 年級女生，"
                  "推廣組與競賽組，公益推廣活動、全程免費參加。", None)},
        {"type": "fan_meet", "team": "BW1", "venue": VENUE_BW, "starts": "2026-11-14T15:00:00", "ends": "2026-11-14T17:00:00",
         "all_day": 0, "public": 1, "title": (TEST + "球迷見面會", None), "desc": (TEST_BODY, None)},
        {"type": "open_training", "team": "BW1", "venue": VENUE_BW, "starts": "2026-10-16T19:30:00", "ends": "2026-10-16T21:00:00",
         "all_day": 0, "public": 1, "repeat": "biweekly", "repeat_until": "2026-12-25", "exceptions": [],
         "title": (TEST + "公開訓練", None), "desc": (TEST_BODY, None)},
    ],
}

# ============================================================================
# B2 新聞的標籤／核心價值標籤／精選，以及藍鯨測試新聞
#   標籤主檔是全域（tags 不帶 club_id）。標籤名稱是功能性分類詞，不是事實宣稱。
#   ⚠️ 歸類（哪一類新聞掛哪個標籤／核心價值）是種子的編輯性判斷，非規劃書明文，供後台測試篩選用。
# ============================================================================
TAGS = [
    ("youth-development", "青訓發展", "Youth Development"),
    ("international", "國際交流", "International"),
    ("match", "賽事", "Matches"),
    ("community", "社區", "Community"),
    ("fans", "球迷", "Fans"),
]
# news category code → (tag slugs, value tags)；value tag 值域見 value_tag_links CHECK
NEWS_TAG_RULES = {
    "match": (["match"], []),
    "international": (["international"], ["global_pathways"]),
    "community": (["community"], ["community"]),
    "camps-events": (["youth-development"], ["players_first"]),
    "club": ([], []),
}
FEATURED_TCRFC_COUNT = 2  # 依 published_at 最新的兩篇設為精選

BW_TEST_NEWS = [
    {"slug": "bw-test-news-club", "category": "club", "date": "2026-09-20",
     "title": TEST + "台中藍鯨俱樂部消息範例", "summary": TEST_BODY, "tags": [], "value_tags": []},
    {"slug": "bw-test-news-match", "category": "match", "date": "2026-09-25",
     "title": TEST + "台中藍鯨賽事報導範例", "summary": TEST_BODY, "tags": ["match"], "value_tags": []},
    {"slug": "bw-test-news-community", "category": "community", "date": "2026-09-28",
     "title": TEST + "台中藍鯨社區活動範例", "summary": TEST_BODY, "tags": ["community"], "value_tags": ["community"]},
]

# ============================================================================
# H3 301 轉址（redirects）
#   來源：content/migration/舊官網URL盤點.csv（tcrfc 舊站 Wix）與 content/blue-whale/site-map.md（藍鯨舊站 Google Sites，
#   規劃書 §7 第 3 點要求「未編碼與百分比編碼兩種形式都涵蓋」，故兩種各寫一筆）。
#   ⚠️ 只寫「新站對應頁能由名稱直接推得」的列：CSV 的 40 則「保留」新聞文章沒有舊 slug→新 slug 對照
#   （舊 slug 是英數混合縮寫，新 slug 是日期＋序號），不猜；客戶「決定」欄仍是空的，本表的對應是種子的建議，
#   待客戶確認前不代表定案。新站路由皆核對過 apps/web/app/pages/zh/ 實際存在。
# ============================================================================
REDIRECTS_RAW = {
    "tcrfc": [
        ("/tcrfcnews", "/zh/news/"),
        ("/tcrfcnews/categories/社區-台中磐石-taichung-rock-fc", "/zh/news/community/"),
        ("/tcrfcnews/categories/公告-台中磐石-taichung-rock-fc", "/zh/news/club/"),
        ("/tcrfcnews/categories/2025台中磐石國際盃-2025-tcrfc-int-cup", "/zh/news/match/"),
        ("/tcrfcnews/categories/一線隊-first-team", "/zh/news/club/"),
        ("/tcrfcnews/categories/消息-news", "/zh/news/"),
        ("/tcrfcnews/categories/比賽-taichung-rock-fc", "/zh/news/match/"),
        ("/tcrfcnews/categories/english-news", "/en/news/"),
        ("/product-page/厚底緩震機能襪", "/zh/shop/cushioned-socks/"),
        ("/product-page/台中磐石主場球衣-2026賽季", "/zh/shop/home-jersey-2026/"),
        ("/category/足部裝備", "/zh/shop/"),
        ("/category/球衣-jersey", "/zh/shop/"),
        ("/aboutus", "/zh/about/"),
        ("/tcrfcteam", "/zh/club/"),
        ("/tcrfcacademy", "/zh/academy/"),
        ("/partnership", "/zh/partners/"),
        ("/比賽-matches", "/zh/schedule/"),
        ("/積分表-tables", "/zh/schedule/"),
        ("/en-home", "/en/"),
    ],
    "bw": [
        ("/首頁", "/zh/"),
        ("/新聞中心", "/zh/news/"),
        ("/一線隊", "/zh/club/first-team/"),
        ("/一線隊/教練團", "/zh/club/first-team/"),
        ("/木蘭聯賽", "/zh/schedule/"),
        ("/賽事", "/zh/schedule/"),
        ("/賽事/2025全國總統盃足球錦標賽", "/zh/schedule/"),
        ("/比賽球場", "/zh/join/location/"),
        ("/足球青年隊", "/zh/academy/"),
        ("/足球青年隊/u15女子隊", "/zh/academy/teams/"),
        ("/足球青年隊/u12女子隊", "/zh/academy/teams/"),
        ("/推廣活動", "/zh/programs/"),
        ("/推廣活動/社區足球學校", "/zh/programs/childrens-training/"),
        ("/推廣活動/運動i台灣-運動熱區", "/zh/programs/childrens-training/"),
        ("/推廣活動/教練講習", "/zh/programs/specialist/"),
        ("/推廣活動/台中女子足球節", "/zh/programs/school-community/"),
        ("/推廣活動/建教合作", "/zh/programs/school-community/"),
        ("/加油團", "/zh/culture/fan-club/"),
        ("/關於台中藍鯨", "/zh/about/"),
        ("/關於台中藍鯨/球隊經理寄語", "/zh/about/our-people/"),
    ],
}


def _redirect_rows(club: str):
    """每一組來源路徑：未編碼（中文原樣）＋百分比編碼兩種都收；純 ASCII 路徑兩者相同只留一筆。"""
    rows, seen = [], set()
    for from_path, to_path in REDIRECTS_RAW[club]:
        for candidate in (from_path, quote(from_path, safe="/-_.~")):
            if candidate not in seen:
                seen.add(candidate)
                rows.append((candidate, to_path))
    return rows


# ============================================================================
# H1 全站 SEO 預設、H4 llms.txt 五區塊、H5 AI 爬蟲
#   文案取自 club-copy.ts CLUB_IDENTITY（footerBlurb／slogan，已核實）與 site-facts（已核實事實）。
#   ⛔ 不種：seo.robots_custom_rules（會直接寫進 robots.txt，沒有需求就不預設）、tracking.*（假的追蹤碼會讓前台載入
#      無效腳本）、預設 OG 圖（需要圖片上傳）。⛔ 藍鯨英文：英文正式全名待客戶確認（docs/14），不自行挑一個。
# ============================================================================
SEO_SETTINGS = {
    "tcrfc": {
        "title_template": ("{title}｜台中磐石足球俱樂部", "{title} | Taichung Rock FC"),
        "default_description": (
            "台中磐石足球俱樂部致力於透過專業模式，培育選手追求卓越，讓世界看見台灣足球。",
            "Taichung Rock FC develops players through a professional model, striving for excellence so the world can see Taiwan football.",
        ),
    },
    "bw": {
        "title_template": ("{title}｜台中藍鯨女子足球隊", None),
        "default_description": (
            "隸屬於臺中市女子足球協會之台中藍鯨女子足球隊，是台灣木蘭足球聯賽的球隊之一，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。",
            None,
        ),
    },
}

_TCRFC_KEY_PAGES_ZH = "\n".join([
    "- [首頁](/zh/)", "- [關於台中磐石](/zh/about/)", "- [俱樂部](/zh/club/)", "- [足球學院](/zh/academy/)",
    "- [課程與活動](/zh/programs/)", "- [女子足球](/zh/womens/)", "- [新聞](/zh/news/)", "- [台中磐石文化](/zh/culture/)",
    "- [夥伴與贊助](/zh/partners/)", "- [加入與聯絡](/zh/join/)", "- [慈善](/zh/charity/)", "- [常見問題](/zh/faq/)",
    "- [賽事行事曆](/zh/schedule/)",
])
_TCRFC_KEY_PAGES_EN = "\n".join([
    "- [Home](/en/)", "- [About TCRFC](/en/about/)", "- [Club](/en/club/)", "- [Academy](/en/academy/)",
    "- [Programs](/en/programs/)", "- [Women's Football](/en/womens/)", "- [News](/en/news/)", "- [Culture](/en/culture/)",
    "- [Partners](/en/partners/)", "- [Join & Contact](/en/join/)", "- [Charity](/en/charity/)", "- [FAQ](/en/faq/)",
    "- [Fixtures & Calendar](/en/schedule/)",
])
# 藍鯨不設 06 女子足球、11 慈善，青年隊取代學院（藍鯨規劃書 §1.3）
_BW_KEY_PAGES_ZH = "\n".join([
    "- [首頁](/zh/)", "- [關於台中藍鯨](/zh/about/)", "- [一線隊](/zh/club/)", "- [青年隊](/zh/academy/)",
    "- [推廣活動](/zh/programs/)", "- [新聞](/zh/news/)", "- [台中藍鯨文化](/zh/culture/)", "- [夥伴與贊助](/zh/partners/)",
    "- [加入與聯絡](/zh/join/)", "- [常見問題](/zh/faq/)", "- [賽事行事曆](/zh/schedule/)",
])
_BW_KEY_PAGES_EN = "\n".join([
    "- [Home](/en/)", "- [About](/en/about/)", "- [Club](/en/club/)", "- [Youth Teams](/en/academy/)",
    "- [Programs](/en/programs/)", "- [News](/en/news/)", "- [Culture](/en/culture/)", "- [Partners](/en/partners/)",
    "- [Join & Contact](/en/join/)", "- [FAQ](/en/faq/)", "- [Fixtures & Calendar](/en/schedule/)",
])

# 授權文字沿用前台 llms.txt 路由內建預設（apps/web/server/routes/llms*.txt.ts），不新增政策
_LICENSE_ZH = "本站內容歡迎摘要引用，請註明來源為本站並附上原始網址。"
_LICENSE_EN = "Content may be summarized with attribution linking back to the source page."

LLMS = {
    "tcrfc": {
        "positioning": (
            "台中磐石足球俱樂部（TCRFC）位於台中，致力於透過專業模式培育選手追求卓越，讓世界看見台灣足球。"
            "品牌主張：在地扎根．放眼世界。",
            "Taichung Rock FC (TCRFC) is a football club based in Taichung, Taiwan. LOCAL ROOTS. GLOBAL PATHWAYS.",
        ),
        "key_pages": (_TCRFC_KEY_PAGES_ZH, _TCRFC_KEY_PAGES_EN),
        "facts_summary": (
            "2024 年創立；目前參加企業甲級聯賽；主場為西屯足球場（台中市北屯區崇平路二段景谷巷 11 弄 41 號）；"
            "一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系。",
            "Founded in 2024. Competes in the Enterprise Premier League (企業甲級聯賽). Home ground: Xitun Football Field, Taichung. "
            "Squads: the first team plus the Academy (U15 / U14 / U12).",
        ),
        "license": (_LICENSE_ZH, _LICENSE_EN),
        "contact": (
            "官方社群：Facebook https://www.facebook.com/TCRFC2024、Instagram https://www.instagram.com/tcr_fc_2024、"
            "YouTube https://www.youtube.com/@TCRFC-2024。事實查證或引用疑問請透過官網聯絡表單（/zh/join/contact/）；"
            "Email：contact@example.com（" + TEST + "測試信箱，正式信箱確認後請替換）。",
            "Official channels: Facebook https://www.facebook.com/TCRFC2024, Instagram https://www.instagram.com/tcr_fc_2024, "
            "YouTube https://www.youtube.com/@TCRFC-2024. For fact-checking or citation questions please use the contact form "
            "(/en/join/contact/). Email: contact@example.com (" + TEST + " placeholder, to be replaced).",
        ),
    },
    "bw": {
        "positioning": (
            "台中藍鯨女子足球隊隸屬臺中市女子足球協會，是台灣木蘭足球聯賽的球隊之一。以「藍鯨」作為象徵，代表追求更快、更堅強、"
            "更現代化的足球型態，希望能帶動台中足球基層環境風氣，帶動中部地區女子足球的發展。口號：航向世界的藍鯨。",
            None,
        ),
        "key_pages": (_BW_KEY_PAGES_ZH, _BW_KEY_PAGES_EN),
        "facts_summary": (
            "2014 年 4 月 12 日成立；所屬聯賽為台灣木蘭足球聯賽；主場為台中北屯太原足球場（現行），創隊時期主場為台中市立豐原體育場；"
            "一線隊與青年隊（U15／U12）兩個梯隊並行的發展體系。",
            None,
        ),
        "license": (_LICENSE_ZH, _LICENSE_EN),
        "contact": (
            "Email：fbbh2014@gmail.com；Facebook https://www.facebook.com/tbwfc；Instagram https://instagram.com/tcbw2014；"
            "YouTube https://www.youtube.com/@user-xu1wm3xx1w；LINE 官方帳號 https://lin.ee/CS65qCR。",
            "Email: fbbh2014@gmail.com; Facebook https://www.facebook.com/tbwfc; Instagram https://instagram.com/tcbw2014; "
            "YouTube https://www.youtube.com/@user-xu1wm3xx1w; LINE https://lin.ee/CS65qCR.",
        ),
    },
}

# 規劃書 §7 GEO-02 條文列的五個範例代理，全部允許（與 GeoCrawlerDefaults.DefaultUserAgents 一致）
CRAWLER_AGENTS = [
    {"userAgent": "GPTBot", "allowed": True},
    {"userAgent": "ClaudeBot", "allowed": True},
    {"userAgent": "PerplexityBot", "allowed": True},
    {"userAgent": "Google-Extended", "allowed": True},
    {"userAgent": "CCBot", "allowed": True},
]
# 藍鯨的青年隊名單頁（/zh/academy/teams/）尚未列入強制排除清單，本檔刻意不在此補
# （AdminGeoCrawlerTests 明文斷言 bw 不含該路徑，補了會讓既有測試失敗），已在回報中列為待處理事項。
CRAWLER_EXTRA_EXCLUDE = {"tcrfc": [], "bw": []}


# ============================================================================
# T-SQL 產生
# ============================================================================
def emit_all(*, emit, block, esc, new_id, clubs, season_sq, venue_by_keyword_sq, category_sq):
    """clubs：{'tcrfc': '(SELECT id FROM clubs ...)', 'bw': ...}（純量子查詢字串）。"""

    def team_sq(code: str) -> str:
        return f"(SELECT id FROM teams WHERE code = N'{code}')"

    def setting_value(club_code, key, value, group):
        if value is None:
            return
        club_sq = clubs[club_code]
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = {club_sq} AND setting_key = {esc(key)};
IF @id IS NULL
BEGIN
  SET @id = {esc(new_id("setting", club_code, key))};
  INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  VALUES (@id, {club_sq}, {esc(key)}, {esc(value)}, {esc(group)});
END
""")

    def setting_i18n(club_code, key, zh_value, en_value, group):
        if zh_value is None and en_value is None:
            return
        club_sq = clubs[club_code]
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE club_id = {club_sq} AND setting_key = {esc(key)};
IF @id IS NULL
BEGIN
  SET @id = {esc(new_id("setting", club_code, key))};
  INSERT INTO settings (id, club_id, setting_key, setting_group)
  VALUES (@id, {club_sq}, {esc(key)}, {esc(group)});
END
""")
        for locale, value in (("zh-Hant", zh_value), ("en", en_value)):
            if value is None:
                continue
            block(f"""
IF NOT EXISTS (
  SELECT 1 FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
  WHERE s.club_id = {club_sq} AND s.setting_key = {esc(key)} AND si.locale = {esc(locale)}
)
BEGIN
  INSERT INTO settings_i18n (setting_id, locale, value)
  SELECT s.id, {esc(locale)}, {esc(value)} FROM settings s
  WHERE s.club_id = {club_sq} AND s.setting_key = {esc(key)};
END
""")

    # ── 25. B1 頁面 ─────────────────────────────────────────────────────────
    emit("-- ── 25. pages／page_blocks／page_versions：B1 頁面（真實文案＋每俱樂部一頁測試草稿） ──")
    for club_code, pages in PAGES.items():
        club_sq = clubs[club_code]
        for page in pages:
            page_id = new_id("page", club_code, page["slug"])
            published = page["status"] == "published"
            published_at = "'2026-09-30T00:00:00'" if published else "NULL"
            snapshot = {
                "seo": {"zh": {"seoTitle": page["seo_title"], "seoDescription": page["seo_description"]}},
                "blocks": [{"blockType": t, "content": c} for t, c in page["blocks"]],
            }
            block_sql = "\n".join(
                f"  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES "
                f"({esc(new_id('page_block', club_code, page['slug'], str(i)))}, @id, {esc(t)}, {esc(jdump(c))}, {i});"
                for i, (t, c) in enumerate(page["blocks"])
            )
            token = hashlib.sha256(f"preview|{club_code}|{page['slug']}".encode()).hexdigest()
            block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM pages WHERE club_id = {club_sq} AND slug = {esc(page["slug"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(page_id)};
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, {club_sq}, {esc(page["slug"])}, {esc(page["status"])}, {published_at});
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', {esc(page["seo_title"])}, {esc(page["seo_description"])});
{block_sql}
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES ({esc(new_id('page_version', club_code, page['slug'], '1'))}, @id, 1, {esc(jdump(snapshot))}, {esc(token)});
END
""")

    # ── 26. B3 輪播（draft＋佔位圖鍵） ───────────────────────────────────────
    emit("-- ── 26. banners／banners_i18n：首頁輪播，一律 draft＋佔位圖鍵（沒有可上傳素材，公開端點不會顯示） ──")
    for club_code, banners in BANNERS.items():
        club_sq = clubs[club_code]
        for i, b in enumerate(banners):
            banner_id = new_id("banner", club_code, str(i))
            title_zh, title_en = b["title"]
            sub_zh, sub_en = b["subtitle"]
            alt_zh, alt_en = b["alt"]
            cta1 = b["cta1"]
            cta2 = b["cta2"]

            def i18n_row(locale, title, sub, alt):
                return (
                    f"  INSERT INTO banners_i18n (banner_id, locale, title, subtitle, image_alt, cta_1_label, cta_1_url, cta_2_label, cta_2_url) "
                    f"VALUES (@id, {esc(locale)}, {esc(title)}, {esc(sub)}, {esc(alt)}, {esc(cta1[0])}, {esc(cta1[1])}, "
                    f"{esc(cta2[0] if cta2 else None)}, {esc(cta2[1] if cta2 else None)});"
                )
            rows = [i18n_row("zh-Hant", title_zh, sub_zh, alt_zh)]
            if title_en:
                rows.append(i18n_row("en", title_en, sub_en, alt_en))
            block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = b.id FROM banners b JOIN banners_i18n bi ON bi.banner_id = b.id AND bi.locale = N'zh-Hant'
  WHERE b.club_id = {club_sq} AND bi.title = {esc(title_zh)};
IF @id IS NULL
BEGIN
  SET @id = {esc(banner_id)};
  INSERT INTO banners (id, club_id, media_type, image_key, sort_order, status) VALUES (@id, {club_sq}, N'image', {esc(BANNER_PLACEHOLDER_KEY)}, {i}, N'draft');
{chr(10).join(rows)}
END
""")

    # ── 27. B4 FAQ ──────────────────────────────────────────────────────────
    emit("-- ── 27. faqs／faqs_i18n／faq_category_links／faq_embed_slot_links：B4 常見問題 ──")
    for club_code, faqs in FAQS.items():
        club_sq = clubs[club_code]
        for i, f in enumerate(faqs):
            faq_id = new_id("faq", club_code, f["slug"])
            q_zh, q_en = f["q"]
            a_zh, a_en = f["a"]
            lines = [
                f"  INSERT INTO faqs (id, club_id, slug, sort_order, status) VALUES (@id, {club_sq}, {esc(f['slug'])}, {i}, N'published');",
                f"  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'zh-Hant', {esc(q_zh)}, {esc(a_zh)});",
            ]
            if q_en and a_en:
                lines.append(f"  INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@id, N'en', {esc(q_en)}, {esc(a_en)});")
            for cat in f["categories"]:
                lines.append(
                    f"  INSERT INTO faq_category_links (faq_id, faq_category_id) "
                    f"VALUES (@id, (SELECT id FROM faq_categories WHERE slug = {esc(cat)}));")
            for j, slot in enumerate(f["embeds"]):
                lines.append(
                    f"  INSERT INTO faq_embed_slot_links (faq_id, faq_embed_slot_id, sort_order) "
                    f"VALUES (@id, (SELECT id FROM faq_embed_slots WHERE code = {esc(slot)}), {i * 10 + j});")
            block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM faqs WHERE club_id = {club_sq} AND slug = {esc(f["slug"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(faq_id)};
{chr(10).join(lines)}
END
""")

    # ── 28. P1／P2 課程與梯次 ────────────────────────────────────────────────
    emit("-- ── 28. programs／programs_i18n／sessions：P1 課程項目與 P2 梯次（bw 真實、tcrfc 測試） ──")
    for club_code, programs in PROGRAMS.items():
        club_sq = clubs[club_code]
        for p in programs:
            program_id = new_id("program", club_code, p["slug"])
            age_min, age_max = p["age"]
            lines = [
                f"  INSERT INTO programs (id, club_id, slug, program_type, age_min, age_max, status) "
                f"VALUES (@id, {club_sq}, {esc(p['slug'])}, {esc(p['type'])}, {esc(age_min)}, {esc(age_max)}, {esc(p['status'])});",
                f"  INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@id, N'zh-Hant', {esc(p['name'])}, {esc(p['intro'])});",
            ]
            venue_sql = venue_by_keyword_sq(p["venue"]) if p["venue"] else "NULL"
            for k, s in enumerate(p["sessions"]):
                weekly = jdump(s["weekly"]) if s.get("weekly") else None
                lines.append(
                    "  INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, weekly_schedule, capacity, enrolled_count, "
                    "price, early_bird_price, early_bird_until, signup_opens_at, signup_closes_at, status) VALUES "
                    f"({esc(new_id('session', club_code, p['slug'], str(k)))}, {club_sq}, @id, {venue_sql}, "
                    f"{esc(s.get('start'))}, {esc(s.get('end'))}, {esc(weekly)}, {esc(s.get('capacity'))}, {esc(s.get('enrolled', 0))}, "
                    f"{esc(s.get('price'))}, {esc(s.get('early_price'))}, {esc(s.get('early_until'))}, "
                    f"{esc(s.get('opens'))}, {esc(s.get('closes'))}, {esc(s['status'])});")
            block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM programs WHERE club_id = {club_sq} AND slug = {esc(p["slug"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(program_id)};
{chr(10).join(lines)}
END
""")

    # ── 29. C4 積分榜 ────────────────────────────────────────────────────────
    emit("-- ── 29. standings：C4 積分榜（全部測試值，沒有真實積分來源） ──")
    for club_code, (season_code, rows) in STANDINGS.items():
        club_sq = clubs[club_code]
        season = season_sq(club_sq, season_code)
        for rank, played, points in rows:
            name = f"{TEST}隊伍 {chr(64 + rank)}"
            block(f"""
IF NOT EXISTS (SELECT 1 FROM standings WHERE club_id = {club_sq} AND season_id = {season} AND team_name = {esc(name)})
  INSERT INTO standings (id, club_id, season_id, team_name, rank, played, points)
  VALUES ({esc(new_id("standing", club_code, season_code, name))}, {club_sq}, {season}, {esc(name)}, {rank}, {played}, {points});
""")

    # ── 30. L2 自建事件 ──────────────────────────────────────────────────────
    emit("-- ── 30. calendar_custom_events：L2 自建事件（bw 一筆真實過往活動、其餘測試） ──")
    for club_code, events in CALENDAR_EVENTS.items():
        club_sq = clubs[club_code]
        for ev in events:
            title_zh, title_en = ev["title"]
            desc_zh, desc_en = ev["desc"]
            event_id = new_id("calendar_event", club_code, title_zh)
            venue_sql = venue_by_keyword_sq(ev["venue"]) if ev["venue"] else "NULL"
            lines = [
                "  INSERT INTO calendar_custom_events (id, club_id, event_type_id, venue_id, starts_at, ends_at, is_all_day, repeat_rule, repeat_until, is_public) "
                f"VALUES (@id, {club_sq}, (SELECT id FROM event_types WHERE code = {esc(ev['type'])}), {venue_sql}, {esc(ev['starts'])}, {esc(ev['ends'])}, "
                f"{ev['all_day']}, {esc(ev.get('repeat'))}, {esc(ev.get('repeat_until'))}, {ev['public']});",
                f"  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'zh-Hant', {esc(title_zh)}, {esc(desc_zh)});",
            ]
            if title_en:
                lines.append(f"  INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title, description) VALUES (@id, N'en', {esc(title_en)}, {esc(desc_en)});")
            if ev["team"]:
                lines.append(f"  INSERT INTO calendar_event_teams (source_type, source_id, team_id) VALUES (N'custom', @id, {team_sq(ev['team'])});")
            for ex in ev.get("exceptions", []):
                lines.append(f"  INSERT INTO calendar_event_exceptions (calendar_custom_event_id, excluded_on) VALUES (@id, {esc(ex)});")
            block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = e.id FROM calendar_custom_events e JOIN calendar_custom_events_i18n i ON i.calendar_custom_event_id = e.id AND i.locale = N'zh-Hant'
  WHERE e.club_id = {club_sq} AND i.title = {esc(title_zh)};
IF @id IS NULL
BEGIN
  SET @id = {esc(event_id)};
{chr(10).join(lines)}
END
""")

    # ── 31. B2 標籤／核心價值標籤／精選／藍鯨測試新聞 ─────────────────────────
    emit("-- ── 31. tags／article_tags／value_tag_links：新聞標籤（全域主檔）與 tcrfc 既有新聞的歸類 ──")
    for i, (slug, name_zh, name_en) in enumerate(TAGS):
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM tags WHERE slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(new_id("tag", slug))};
  INSERT INTO tags (id, slug) VALUES (@id, {esc(slug)});
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'zh-Hant', {esc(name_zh)});
  INSERT INTO tags_i18n (tag_id, locale, name) VALUES (@id, N'en', {esc(name_en)});
END
""")
    tcrfc = clubs["tcrfc"]
    for cat_code, (tag_slugs, value_tags) in NEWS_TAG_RULES.items():
        for tag_slug in tag_slugs:
            block(f"""
INSERT INTO article_tags (article_id, tag_id)
SELECT a.id, t.id FROM articles a CROSS JOIN tags t
WHERE a.club_id = {tcrfc} AND a.article_category_id = {category_sq(cat_code)} AND t.slug = {esc(tag_slug)}
  AND NOT EXISTS (SELECT 1 FROM article_tags x WHERE x.article_id = a.id AND x.tag_id = t.id);
""")
        for vt in value_tags:
            block(f"""
INSERT INTO value_tag_links (entity_type, entity_id, value_tag)
SELECT N'article', a.id, {esc(vt)} FROM articles a
WHERE a.club_id = {tcrfc} AND a.article_category_id = {category_sq(cat_code)}
  AND NOT EXISTS (SELECT 1 FROM value_tag_links x WHERE x.entity_type = N'article' AND x.entity_id = a.id AND x.value_tag = {esc(vt)});
""")
    block(f"""
UPDATE articles SET is_featured = 1
WHERE id IN (SELECT TOP {FEATURED_TCRFC_COUNT} id FROM articles WHERE club_id = {tcrfc} AND status = N'published' ORDER BY published_at DESC, row_seq DESC);
""")

    emit("-- ── 31b. articles：藍鯨測試新聞（藍鯨舊站沒有自有新聞全文，見 content/blue-whale/news-index.md） ──")
    bw = clubs["bw"]
    for a in BW_TEST_NEWS:
        article_id = new_id("article", a["slug"])
        lines = [
            f"  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at) "
            f"VALUES (@id, {bw}, {esc(a['slug'])}, {category_sq(a['category'])}, N'published', {esc(a['date'] + 'T00:00:00')});",
            f"  INSERT INTO articles_i18n (article_id, locale, title, summary) VALUES (@id, N'zh-Hant', {esc(a['title'])}, {esc(a['summary'])});",
        ]
        for tag_slug in a["tags"]:
            lines.append(f"  INSERT INTO article_tags (article_id, tag_id) VALUES (@id, (SELECT id FROM tags WHERE slug = {esc(tag_slug)}));")
        for vt in a["value_tags"]:
            lines.append(f"  INSERT INTO value_tag_links (entity_type, entity_id, value_tag) VALUES (N'article', @id, {esc(vt)});")
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = {esc(a["slug"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(article_id)};
{chr(10).join(lines)}
END
""")

    # ── 32. H3 301 轉址 ─────────────────────────────────────────────────────
    emit("-- ── 32. redirects：H3 301 轉址（舊站網址→新站，藍鯨兩種編碼各一筆） ──")
    for club_code in REDIRECTS_RAW:
        club_sq = clubs[club_code]
        for from_path, to_path in _redirect_rows(club_code):
            block(f"""
IF NOT EXISTS (SELECT 1 FROM redirects WHERE club_id = {club_sq} AND from_path = {esc(from_path)})
  INSERT INTO redirects (id, club_id, from_path, to_path, is_active)
  VALUES ({esc(new_id("redirect", club_code, from_path))}, {club_sq}, {esc(from_path)}, {esc(to_path)}, 1);
""")

    # ── 33. H1 全站 SEO 預設／H4 llms.txt／H5 AI 爬蟲 ─────────────────────────
    emit("-- ── 33. settings：seo.*（全站 SEO 預設）、geo.llms_*（llms.txt 五區塊）、geo.crawler_*（AI 爬蟲） ──")
    for club_code, s in SEO_SETTINGS.items():
        setting_i18n(club_code, "seo.title_template", *s["title_template"], "seo")
        setting_i18n(club_code, "seo.default_description", *s["default_description"], "seo")
    for club_code, blocks in LLMS.items():
        for name in ("positioning", "key_pages", "facts_summary", "license", "contact"):
            zh, en = blocks[name]
            setting_i18n(club_code, f"geo.llms_{name}", zh, en, "geo")
    for club_code in ("tcrfc", "bw"):
        setting_value(club_code, "geo.crawler_agents", jdump(CRAWLER_AGENTS), "geo")
        setting_value(club_code, "geo.crawler_extra_exclude_paths", jdump(CRAWLER_EXTRA_EXCLUDE[club_code]), "geo")

    # ========================================================================
    # 34–41. E1 夥伴／E2 贊助／E3 提案與 Lead／B5 慈善／B6 媒體專區／C5 榮譽（2026-09-30，E1a 後台 API 任務）
    #
    # 🔴 全部是【測試】虛構資料：沒有任何真實來源（bw 夥伴早已有真實種子，見主腳本 §17，這裡不動）。
    # 名稱一律以「【測試】」開頭；Email 一律 example.com；金額與名額是整數，一律視為測試。
    # 圖片與檔案：沒有可上傳的公開素材與 Azurite 上傳腳本，所有 *_key 一律留空（NULL），
    # 媒體資源的 file_key（NOT NULL）用佔位鍵 seed-placeholder/no-file 且一律 draft（公開端點只回 published，不會顯示壞連結）。
    # 只種 tcrfc（慈善單元不屬於藍鯨，藍鯨夥伴／贊助另有真實種子或尚無資料）。
    # ========================================================================
    tc = clubs["tcrfc"]
    D1 = "(SELECT id FROM teams WHERE code = N'D1')"

    emit("-- ── 34. partners／partners_i18n：E1 夥伴（tcrfc，五種類型各一，全部【測試】） ──")
    PARTNERS_TEST = [
        ("test-partner-strategic", "策略夥伴", "台灣", "【測試】示範策略夥伴", "Test Strategic Partner", "【測試】共同推動在地足球發展的策略合作。", "2026-01-01", None, 0, 1, 1),
        ("test-partner-international", "國際夥伴", "日本", "【測試】示範國際夥伴", "Test International Partner", "【測試】國際球探與交流合作。", "2026-03-01", "2027-02-28", 1, 1, 0),
        ("test-partner-training", "訓練夥伴", "台灣", "【測試】示範訓練夥伴", "Test Training Partner", "【測試】體能與技術訓練合作。", "2025-07-01", "2026-06-30", 2, 0, 0),
        ("test-partner-education", "教育夥伴", "台灣", "【測試】示範教育夥伴", None, "【測試】校園與社區足球推廣合作。", None, None, 3, 0, 0),
        ("test-partner-brand", "品牌夥伴", "台灣", "【測試】示範品牌夥伴", "Test Brand Partner", None, None, None, 4, 0, 1),
    ]
    for slug, ptype, country, zh, en, content, start, end, order, footer, home in PARTNERS_TEST:
        pid = new_id("partner", "tcrfc", slug)
        lines = [
            f"  INSERT INTO partners (id, club_id, slug, partner_type, country, website_url, start_on, end_on, show_in_footer, show_on_home, sort_order) "
            f"VALUES (@id, {tc}, {esc(slug)}, {esc(ptype)}, {esc(country)}, N'https://example.com/{slug}', {esc(start)}, {esc(end)}, {footer}, {home}, {order});",
            f"  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'zh-Hant', {esc(zh)}, {esc(content)});",
        ]
        if en:
            lines.append(f"  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@id, N'en', {esc(en)}, NULL);")
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = {tc} AND slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(pid)};
{chr(10).join(lines)}
END
""")

    emit("-- ── 35. sponsors／sponsors_i18n／sponsor_packages／sponsor_package_links／sponsor_activations：E2 贊助（tcrfc，全部【測試】） ──")
    PACKAGES_TEST = [  # 規劃書 §3.9 9.4 九種方案名稱（真實）＋ 內容／價格（測試）
        ("test-package-club", "俱樂部贊助", "Club Sponsorship", 500000, 1000000, 1),
        ("test-package-academy", "學院贊助", "Academy Sponsorship", 200000, 400000, 1),
        ("test-package-team", "球隊贊助", "Team Sponsorship", 300000, 600000, 1),
        ("test-package-camp", "營隊贊助", "Camp Sponsorship", 100000, 200000, 1),
        ("test-package-international", "國際計畫贊助", "International Programme Sponsorship", None, None, 0),
        ("test-package-comic", "漫畫內容合作", "Comic Content Partnership", 80000, 150000, 1),
        ("test-package-merch", "商品合作", "Merchandise Partnership", None, None, 0),
        ("test-package-fanclub", "球迷會贊助", "Fan Club Sponsorship", 50000, 100000, 1),
        ("test-package-naming", "場館冠名", "Venue Naming Rights", 1000000, 2000000, 0),
    ]
    for i, (slug, zh, en, pmin, pmax, public) in enumerate(PACKAGES_TEST):
        pkg_id = new_id("sponsor_package", "tcrfc", slug)
        status = "published" if i < 8 else "draft"
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsor_packages WHERE club_id = {tc} AND slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(pkg_id)};
  INSERT INTO sponsor_packages (id, club_id, slug, price_min, price_max, is_price_public, sort_order, status)
  VALUES (@id, {tc}, {esc(slug)}, {esc(pmin)}, {esc(pmax)}, {public}, {i}, {esc(status)});
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name, content, benefit_list, audience)
  VALUES (@id, N'zh-Hant', {esc(zh)}, {esc("【測試】方案內容說明。正式內容上線前請於後台替換。")},
          {esc("【測試】權益一：場邊看板" + chr(10) + "【測試】權益二：官網露出" + chr(10) + "【測試】權益三：活動邀請")}, {esc("【測試】適合企業與品牌")});
  INSERT INTO sponsor_packages_i18n (sponsor_package_id, locale, name) VALUES (@id, N'en', {esc(en)});
END
""")

    SPONSORS_TEST = [  # slug, tier, zh, en, start, end, alert_on, packages
        ("test-sponsor-main", "主贊助", "【測試】示範主贊助商", "Test Main Sponsor", "2026-01-01", "2027-06-30", None, ["test-package-club"]),
        ("test-sponsor-official", "官方贊助", "【測試】示範官方贊助商", "Test Official Sponsor", "2025-10-01", "2026-12-31", "2026-09-01", ["test-package-team", "test-package-camp"]),
        ("test-sponsor-support", "支持夥伴", "【測試】示範支持夥伴", None, "2025-01-01", "2026-06-30", "2026-05-01", ["test-package-fanclub"]),
    ]
    for i, (slug, tier, zh, en, start, end, alert, pkgs) in enumerate(SPONSORS_TEST):
        sid = new_id("sponsor", "tcrfc", slug)
        link_sql = "\n".join(
            f"  INSERT INTO sponsor_package_links (sponsor_id, sponsor_package_id) VALUES (@id, (SELECT id FROM sponsor_packages WHERE club_id = {tc} AND slug = {esc(p)}));"
            for p in pkgs)
        en_sql = f"  INSERT INTO sponsors_i18n (sponsor_id, locale, name) VALUES (@id, N'en', {esc(en)});" if en else ""
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM sponsors WHERE club_id = {tc} AND slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(sid)};
  INSERT INTO sponsors (id, club_id, slug, tier, contract_start_on, contract_end_on, contact_name, contact_phone, contact_email, expiry_alert_on, sort_order)
  VALUES (@id, {tc}, {esc(slug)}, {esc(tier)}, {esc(start)}, {esc(end)}, {esc("【測試】聯絡人")}, N'04-0000-0000', N'sponsor-{i}@example.com', {esc(alert)}, {i});
  INSERT INTO sponsors_i18n (sponsor_id, locale, name, content) VALUES (@id, N'zh-Hant', {esc(zh)}, {esc("【測試】贊助內容：示範用，正式內容上線前請於後台替換。")});
{en_sql}
{link_sql}
END
""")

    ACTIVATIONS_TEST = [
        ("test-sponsor-main", "【測試】示範贊助活動：開幕戰球迷日", "2026-09-13", "【測試】現場約 500 人參與，社群曝光 10 萬次（示範數字）。"),
        ("test-sponsor-main", "【測試】示範贊助活動：青訓體驗營", "2026-07-20", "【測試】共 60 位小朋友參加（示範數字）。"),
    ]
    for j, (sponsor_slug, title, day, summary) in enumerate(ACTIVATIONS_TEST):
        aid = new_id("sponsor_activation", "tcrfc", sponsor_slug, str(j))
        block(f"""
IF NOT EXISTS (SELECT 1 FROM sponsor_activations_i18n i WHERE i.locale = N'zh-Hant' AND i.title = {esc(title)})
BEGIN
  INSERT INTO sponsor_activations (id, club_id, sponsor_id, happened_on, sort_order)
  VALUES ({esc(aid)}, {tc}, (SELECT id FROM sponsors WHERE club_id = {tc} AND slug = {esc(sponsor_slug)}), {esc(day)}, {j});
  INSERT INTO sponsor_activations_i18n (sponsor_activation_id, locale, title, result_summary)
  VALUES ({esc(aid)}, N'zh-Hant', {esc(title)}, {esc(summary)});
END
""")

    emit("-- ── 36. proposals／enquiries：E3 提案（兩份 A/B 草稿，沒有檔案）與三筆 Lead（全部【測試】） ──")
    for slug_key, title, version in (("a", "【測試】贊助提案簡介（A 版）", 1), ("b", "【測試】贊助提案簡介（B 版）", 2)):
        block(f"""
IF NOT EXISTS (SELECT 1 FROM proposals WHERE club_id = {tc} AND title = {esc(title)})
  INSERT INTO proposals (id, club_id, title, version_no, status)
  VALUES ({esc(new_id("proposal", "tcrfc", slug_key))}, {tc}, {esc(title)}, {version}, N'draft');
""")
    LEADS_TEST = [
        ("a", "【測試】示範公司甲", "【測試】聯絡人甲", "lead-a@example.com", "/zh/partners/sponsorship/", "新進"),
        ("a", "【測試】示範公司乙", "【測試】聯絡人乙", "lead-b@example.com", "/zh/partners/", "處理中"),
        ("b", "【測試】示範公司丙", "【測試】聯絡人丙", "lead-c@example.com", "/zh/partners/sponsorship/", "已回覆"),
    ]
    for k, (ab, company, name, email, source, status) in enumerate(LEADS_TEST):
        eid = new_id("enquiry", "proposal_download", "tcrfc", str(k))
        form_sq = f"(SELECT id FROM forms WHERE club_id = {tc} AND form_code = N'proposal_download')"
        def ans(field_key, value):
            return (f"  INSERT INTO enquiry_answers (enquiry_id, form_field_id, value) VALUES (@id, "
                    f"(SELECT id FROM form_fields WHERE form_id = {form_sq} AND field_key = N'{field_key}'), {esc(value)});")
        block(f"""
DECLARE @id uniqueidentifier = {esc(eid)};
IF NOT EXISTS (SELECT 1 FROM enquiries WHERE id = @id)
BEGIN
  INSERT INTO enquiries (id, club_id, form_id, proposal_id, source_path, utm_source, status)
  VALUES (@id, {tc}, {form_sq}, (SELECT id FROM proposals WHERE id = {esc(new_id("proposal", "tcrfc", ab))}), {esc(source)}, N'seed-test', {esc(status)});
{ans("company", company)}
{ans("name", name)}
{ans("contact", email)}
END
""")

    emit("-- ── 37. charities／charity_programs／impact_records／impact_metrics／settings：B5 慈善與社會影響（tcrfc，全部【測試】） ──")
    CHARITIES_TEST = [
        ("test-org-a", "【測試】示範公益團體甲", "Test Charity A", "【測試】這是示範用的公益團體簡介。"),
        ("test-org-b", "【測試】示範公益團體乙", None, "【測試】另一個示範用的公益團體。"),
    ]
    for slug, zh, en, intro in CHARITIES_TEST:
        cid = new_id("charity", "tcrfc", slug)
        en_sql = f"  INSERT INTO charities_i18n (charity_id, locale, name) VALUES (@id, N'en', {esc(en)});" if en else ""
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM charities WHERE club_id = {tc} AND slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(cid)};
  INSERT INTO charities (id, club_id, slug, website_url, contact_name, contact_phone)
  VALUES (@id, {tc}, {esc(slug)}, N'https://example.com/{slug}', {esc("【測試】聯絡窗口")}, N'04-0000-0000');
  INSERT INTO charities_i18n (charity_id, locale, name, intro) VALUES (@id, N'zh-Hant', {esc(zh)}, {esc(intro)});
{en_sql}
END
""")

    PROGRAMS_TEST = [  # slug, charity slug, zh, en, start, end, status, pinned, order
        ("test-charity-program-a", "test-org-a", "【測試】示範慈善計畫：偏鄉足球捐贈", "Test Charity Program A", "2026-03-01", None, "published", 1, 0),
        ("test-charity-program-b", "test-org-b", "【測試】示範慈善計畫：公益義賽", None, "2025-05-01", "2025-12-31", "published", 0, 1),
        ("test-charity-program-c", "test-org-a", "【測試】示範慈善計畫（草稿）", None, "2026-10-01", None, "draft", 0, 2),
    ]
    for slug, org, zh, en, start, end, status, pinned, order in PROGRAMS_TEST:
        pid = new_id("charity_program", "tcrfc", slug)
        content = jdump([{"blockType": "text", "content": {"body": bi("<p>【測試】計畫緣起與內容：示範用，正式內容上線前請於後台替換。</p>")}}])
        en_sql = (f"  INSERT INTO charity_programs_i18n (charity_program_id, locale, name) VALUES (@id, N'en', {esc(en)});" if en else "")
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_programs WHERE club_id = {tc} AND slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(pid)};
  INSERT INTO charity_programs (id, club_id, slug, charity_id, start_on, end_on, status, sort_order, is_pinned)
  VALUES (@id, {tc}, {esc(slug)}, (SELECT id FROM charities WHERE club_id = {tc} AND slug = {esc(org)}), {esc(start)}, {esc(end)}, {esc(status)}, {order}, {pinned});
  INSERT INTO charity_programs_i18n (charity_program_id, locale, name, target_audience, content, donation_content)
  VALUES (@id, N'zh-Hant', {esc(zh)}, {esc("【測試】偏鄉學童")}, {esc(content)}, {esc("【測試】足球 50 顆、訓練背心 100 件（示範數字）")});
{en_sql}
END
""")
    block(f"""
INSERT INTO charity_program_partners (charity_program_id, partner_id)
SELECT p.id, x.id FROM charity_programs p CROSS JOIN partners x
WHERE p.club_id = {tc} AND p.slug = N'test-charity-program-a' AND x.club_id = {tc} AND x.slug = N'test-partner-strategic'
  AND NOT EXISTS (SELECT 1 FROM charity_program_partners l WHERE l.charity_program_id = p.id AND l.partner_id = x.id);
INSERT INTO charity_program_sponsors (charity_program_id, sponsor_id)
SELECT p.id, x.id FROM charity_programs p CROSS JOIN sponsors x
WHERE p.club_id = {tc} AND p.slug = N'test-charity-program-a' AND x.club_id = {tc} AND x.slug = N'test-sponsor-main'
  AND NOT EXISTS (SELECT 1 FROM charity_program_sponsors l WHERE l.charity_program_id = p.id AND l.sponsor_id = x.id);
""")

    RECORDS_TEST = [  # key, org, program, day, donation, location, brief
        ("a", "test-org-a", "test-charity-program-a", "2026-04-10", "【測試】足球 50 顆、訓練背心 100 件（示範數字）", "【測試】示範地點：南投縣", "【測試】示範簡述。"),
        ("b", "test-org-b", "test-charity-program-b", "2025-11-22", "【測試】獎助學金 5 名（示範數字）", "【測試】示範地點：台中市", None),
    ]
    for key, org, prog, day, donation, loc, brief in RECORDS_TEST:
        rid = new_id("impact_record", "tcrfc", key)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM impact_records_i18n WHERE locale = N'zh-Hant' AND donation_content = {esc(donation)})
BEGIN
  INSERT INTO impact_records (id, club_id, charity_program_id, charity_id, happened_on, sort_order)
  VALUES ({esc(rid)}, {tc}, (SELECT id FROM charity_programs WHERE club_id = {tc} AND slug = {esc(prog)}),
          (SELECT id FROM charities WHERE club_id = {tc} AND slug = {esc(org)}), {esc(day)}, 0);
  INSERT INTO impact_records_i18n (impact_record_id, locale, donation_content, location, brief_description)
  VALUES ({esc(rid)}, N'zh-Hant', {esc(donation)}, {esc(loc)}, {esc(brief)});
END
""")

    METRICS_TEST = [  # key, zh, unit, value, public, program(None=全站)
        ("groups", "【測試】合作公益團體數", "個", 2, 1, None),
        ("items", "【測試】累計捐助項次", "項", 155, 1, None),
        ("amount", "【測試】累計捐助金額（不公開）", "元", 123456, 0, "test-charity-program-a"),
    ]
    for key, zh, unit, value, public, prog in METRICS_TEST:
        mid = new_id("impact_metric", "tcrfc", key)
        prog_sql = f"(SELECT id FROM charity_programs WHERE club_id = {tc} AND slug = {esc(prog)})" if prog else "NULL"
        block(f"""
IF NOT EXISTS (SELECT 1 FROM impact_metrics_i18n WHERE locale = N'zh-Hant' AND name = {esc(zh)})
BEGIN
  INSERT INTO impact_metrics (id, club_id, charity_program_id, metric_key, metric_value, is_public, sort_order)
  VALUES ({esc(mid)}, {tc}, {prog_sql}, {esc("seed-" + key)}, {value}, {public}, 0);
  INSERT INTO impact_metrics_i18n (impact_metric_id, locale, name, unit) VALUES ({esc(mid)}, N'zh-Hant', {esc(zh)}, {esc(unit)});
END
""")

    emit("-- ── 37b. settings：charity.*（捐款導流與參與方式）。導流網址是 example.com 測試值；文案已依規劃書 §3.11 點明收受者 ──")
    setting_value("tcrfc", "charity.donation_url", "https://charity.example.com/", "charity")
    setting_i18n("tcrfc", "charity.donation_cta", "【測試】球迷捐款（捐款由台灣足球策略發展協會收受）", "【測試】Fan donations (received by the Taiwan Football Strategic Development Association)", "charity")
    setting_i18n("tcrfc", "charity.fan_cta", "【測試】球迷捐款", "【測試】Donate", "charity")
    setting_i18n("tcrfc", "charity.corporate_cta", "【測試】企業合作公益專案", "【測試】Corporate partnership", "charity")
    setting_value("tcrfc", "charity.corporate_url", "/zh/join/", "charity")

    emit("-- ── 38. press_resources：B6 媒體專區（三類各一，全部 draft＋佔位檔案鍵；【測試】） ──")
    PRESS_TEST = [
        ("test-press-release", "press_release", "【測試】示範新聞稿", "Test Press Release", 0),
        ("test-brand-kit", "brand_kit", "【測試】示範品牌識別包", "Test Brand Kit", 1),
        ("test-hires-image", "hires_image", "【測試】示範高解析圖", None, 2),
    ]
    for slug, rtype, zh, en, order in PRESS_TEST:
        rid = new_id("press_resource", "tcrfc", slug)
        en_sql = f"  INSERT INTO press_resources_i18n (press_resource_id, locale, title) VALUES (@id, N'en', {esc(en)});" if en else ""
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM press_resources WHERE club_id = {tc} AND slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(rid)};
  INSERT INTO press_resources (id, club_id, slug, resource_type, file_key, sort_order, status)
  VALUES (@id, {tc}, {esc(slug)}, {esc(rtype)}, N'seed-placeholder/no-file', {order}, N'draft');
  INSERT INTO press_resources_i18n (press_resource_id, locale, title, description)
  VALUES (@id, N'zh-Hant', {esc(zh)}, {esc("【測試】示範說明。這筆沒有真實檔案（佔位），請於後台上傳後再改為顯示。")});
{en_sql}
END
""")

    emit("-- ── 39. achievements：C5 榮譽（tcrfc 一線隊，三筆【測試】） ──")
    for k, (year, comp, placing) in enumerate((
        (2026, "【測試】示範盃賽", "冠軍"), (2025, "【測試】示範聯賽", "第三名"), (2024, "【測試】示範友誼賽", "亞軍"))):
        block(f"""
IF NOT EXISTS (SELECT 1 FROM achievements WHERE club_id = {tc} AND competition_name = {esc(comp)})
  INSERT INTO achievements (id, club_id, season_id, team_id, year, competition_name, placing)
  VALUES ({esc(new_id("achievement", "tcrfc", str(k)))}, {tc}, {season_sq(tc, "2026-27")}, {D1}, {year}, {esc(comp)}, {esc(placing)});
""")

    # ========================================================================
    # 42–49. P4 試訓／K 會員系統／L3 行事曆設定（2026-09-30，B1 後台 API 任務，S2-4／S2-5／S2-6）
    #
    # 🔴 全部是【測試】虛構資料：會員姓名一律「【測試】會員○」、Email 一律 @example.com、電話一律 0900-000-0XX
    # （不能加前綴的欄位以 example.com／全 0 電話標明）。不含任何真實個資。密碼雜湊沿用測試帳號的公開測試雜湊
    # （Argon2id，明文 ContentEditor@123），純本機測試用，前台會員登入尚未開發（S2-11）。
    # 會籍付款是【測試】金額。沒有圖片（特約店家 image_key 留空）。
    # ========================================================================
    bw = clubs["bw"]
    BW1 = "(SELECT id FROM teams WHERE code = N'BW1')"
    BWU15 = "(SELECT id FROM teams WHERE code = N'BW-U15')"
    TEST_HASH = "$argon2id$v=19$m=65536,t=3,p=1$UMd2bX7X1E+kvJZReK7EXQ==$hZSGgfUeivSwdQGUg/7Bc8bHO8oHbiBuObGaPXR50EQ="

    emit("-- ── 42. trials／trials_i18n／registrations：P4 試訓場次與報名（tcrfc 一線隊、bw 青年隊，全部【測試】） ──")
    TRIALS_TEST = [  # key, club_code, team_sq, venue keyword, trial_on, capacity, deadline, status, audience zh, audience en
        ("d1-open", "tcrfc", D1, "西屯", "2026-11-14", 20, "2026-11-07", "開放",
         "【測試】一線隊公開試訓：18 歲以上，具比賽經驗者", "Test open trial for the first team: age 18+, match experience required"),
        ("d1-past", "tcrfc", D1, "西屯", "2026-08-16", 15, "2026-08-09", "已結束",
         "【測試】一線隊夏季試訓（已結束）", "Test summer trial (closed)"),
        ("bwu15-open", "bw", BWU15, "豐原", "2026-12-05", 12, "2026-11-28", "開放",
         "【測試】青年隊 U15 試訓：2011–2012 年出生", "Test U15 youth trial: born 2011–2012"),
    ]
    for key, ccode, team, venue_kw, on, cap, deadline, status, aud_zh, aud_en in TRIALS_TEST:
        tid = new_id("trial", ccode, key)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM trials_i18n WHERE audience = {esc(aud_zh)})
BEGIN
  INSERT INTO trials (id, club_id, team_id, venue_id, trial_on, capacity, deadline_on, sync_to_calendar, status)
  VALUES ({esc(tid)}, {clubs[ccode]}, {team}, {venue_by_keyword_sq(venue_kw)}, {esc(on)}, {cap}, {esc(deadline)}, 0, {esc(status)});
  INSERT INTO trials_i18n (trial_id, locale, audience) VALUES ({esc(tid)}, N'zh-Hant', {esc(aud_zh)});
  INSERT INTO trials_i18n (trial_id, locale, audience) VALUES ({esc(tid)}, N'en', {esc(aud_en)});
END
""")
    TRIAL_REGS = [  # no, trial key, club, name, phone, email, birth, guardian, status
        ("SEED-TRIAL-0001", "d1-open", "tcrfc", "【測試】試訓者甲", "0900-000-101", "trial-a@example.com", "2001-03-04", None, "已確認"),
        ("SEED-TRIAL-0002", "d1-open", "tcrfc", "【測試】試訓者乙", "0900-000-102", "trial-b@example.com", "1999-07-21", None, "待確認"),
        ("SEED-TRIAL-0003", "d1-open", "tcrfc", "【測試】試訓者丙", "0900-000-103", "trial-c@example.com", "2000-12-01", None, "候補"),
        ("SEED-TRIAL-0004", "bwu15-open", "bw", "【測試】青年試訓者丁", "0900-000-104", "trial-d@example.com", "2011-05-05", "【測試】家長丁", "待確認"),
    ]
    for no, tkey, ccode, name, phone, email, birth, guardian, status in TRIAL_REGS:
        tid = new_id("trial", ccode, tkey)
        occupies = 1 if status in ("待確認", "已確認", "已繳費", "完成") else 0
        block(f"""
IF NOT EXISTS (SELECT 1 FROM registrations WHERE registration_no = {esc(no)})
BEGIN
  INSERT INTO registrations (id, registration_no, club_id, trial_id, applicant_name, phone, email, birth_on, guardian_name, guardian_phone, status)
  VALUES ({esc(new_id("registration", no))}, {esc(no)}, {clubs[ccode]}, {esc(tid)}, {esc(name)}, {esc(phone)}, {esc(email)}, {esc(birth)},
          {esc(guardian)}, {esc("0900-000-199" if guardian else None)}, {esc(status)});
  UPDATE trials SET enrolled_count = enrolled_count + {occupies} WHERE id = {esc(tid)};
END
""")

    emit("-- ── 43. membership_plans／membership_plans_i18n：K2 會籍方案（tcrfc 2026-27 單人＋家庭、bw 2025 單人，【測試】價格） ──")
    PLANS_TEST = [  # club, season, code, fee, cards, jerseys, rule, starts, ends, order, zh, en, note zh
        ("tcrfc", "2026-27", "single", 1200, 1, 1, "【測試】季中入會照比例計價", "2026-09-13", "2027-05-02", 0, "【測試】球迷會員（單人）", "Fan Club Member (Single)", "【測試】含會員卡一張、入會球衣一件。"),
        ("tcrfc", "2026-27", "family", 3000, 3, 3, "【測試】季中入會不折價", "2026-09-13", "2027-05-02", 1, "【測試】球迷會員（家庭）", "Fan Club Member (Family)", "【測試】1 位成人＋2 位小童，含會員卡三張、球衣三件。"),
        ("bw", "2025", "single", 800, 1, 1, "【測試】季中入會照比例計價", "2025-04-23", "2025-06-15", 0, "【測試】藍鯨球迷會員（單人）", "Blue Whale Fan Club (Single)", "【測試】含會員卡一張、入會球衣一件。"),
    ]
    for ccode, season, code, fee, cards, jerseys, rule, starts, ends, order, zh, en, note in PLANS_TEST:
        pid = new_id("membership_plan", ccode, season, code)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM membership_plans WHERE club_id = {clubs[ccode]} AND season_id = {season_sq(clubs[ccode], season)} AND code = {esc(code)})
BEGIN
  INSERT INTO membership_plans (id, club_id, season_id, code, fee, card_quota, jersey_quota, mid_season_rule, sort_order, starts_on, ends_on, status)
  VALUES ({esc(pid)}, {clubs[ccode]}, {season_sq(clubs[ccode], season)}, {esc(code)}, {fee}, {cards}, {jerseys}, {esc(rule)}, {order}, {esc(starts)}, {esc(ends)}, N'published');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note) VALUES ({esc(pid)}, N'zh-Hant', {esc(zh)}, {esc(note)});
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name, benefit_note) VALUES ({esc(pid)}, N'en', {esc(en)}, NULL);
END
""")

    emit("-- ── 44. members／memberships／member_cards／membership_payments／jersey_issues：K1–K3（7 位虛構會員，涵蓋雙會籍、即將到期、停用、疑似重複帳號） ──")
    # no, name, email, phone, birth, source, status, verified, locale, note
    MEMBERS_TEST = [
        ("M900001", "【測試】會員甲", "member-a@example.com", "0900-000-001", "1990-01-15", "web", "active", 1, "zh-Hant", "【測試】雙會籍示範：同時持有磐石與藍鯨付費會籍。"),
        ("M900002", "【測試】會員乙", "member-b@example.com", "0900-000-002", "1985-06-30", "line", "active", 1, "zh-Hant", None),
        ("M900003", "【測試】會員丙", "member-c@example.com", "0900-000-003", "1978-11-02", "admin", "active", 1, "en", "【測試】現場入會。"),
        ("M900004", "【測試】會員丁", "member-d@example.com", "0900-000-004", "2000-02-29", "web", "active", 0, "zh-Hant", "【測試】Email 尚未驗證。"),
        ("M900005", "【測試】會員戊", "member-e@example.com", "0900-000-005", "1995-09-09", "app", "suspended", 1, "zh-Hant", "【測試】已停用帳號示範。"),
        ("M900006", "【測試】會員己", "member-f@example.com", "0900-000-006", "1992-04-18", "web", "active", 1, "zh-Hant", None),
        ("M900007", "【測試】會員甲（另一個帳號）", "member-a2@example.com", "0900-000-001", "1990-01-15", "line", "active", 1, "zh-Hant", "【測試】與會員甲同一支電話，供「疑似重複帳號」比對示範。"),
    ]
    for no, name, email, phone, birth, source, status, verified, locale, note in MEMBERS_TEST:
        mid = new_id("member", no)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM members WHERE member_no = {esc(no)})
  INSERT INTO members (id, member_no, name, email, password_hash, phone, birth_on, signup_source, status, email_verified_at, last_login_at, internal_note, locale)
  VALUES ({esc(mid)}, {esc(no)}, {esc(name)}, {esc(email)}, {esc(TEST_HASH)}, {esc(phone)}, {esc(birth)}, {esc(source)}, {esc(status)},
          {"SYSUTCDATETIME()" if verified else "NULL"}, {"DATEADD(day, -3, SYSUTCDATETIME())" if verified else "NULL"}, {esc(note)}, {esc(locale)});
""")
    # member no, club, season, plan code, tier, start, end, status, payment (method, amount, paid_on) | None
    MEMBERSHIPS_TEST = [
        ("M900001", "tcrfc", "2026-27", "single", "fan_club", "2026-09-20", "2027-05-02", "active", ("linepay", 1200, "2026-09-20")),
        ("M900001", "bw", "2025", "single", "fan_club", "2025-04-25", "2025-06-15", "expired", ("onsite", 800, "2025-04-25")),
        ("M900002", "tcrfc", "2026-27", "family", "fan_club", "2026-09-22", "2027-05-02", "active", ("onsite", 3000, "2026-09-22")),
        ("M900003", "tcrfc", "2026-27", None, "registered", "2026-09-13", "2027-05-02", "active", None),
        ("M900004", "tcrfc", "2026-27", None, "registered", "2026-09-25", "2027-05-02", "pending", None),
        ("M900005", "tcrfc", "2026-27", "single", "fan_club", "2026-09-14", "2026-10-15", "active", ("linepay", 1200, "2026-09-14")),
        ("M900006", "bw", "2025", None, "registered", "2025-04-23", "2025-06-15", "active", None),
        ("M900007", "tcrfc", "2026-27", None, "registered", "2026-09-27", "2027-05-02", "active", None),
    ]
    for no, ccode, season, plan, tier, start, end, status, pay in MEMBERSHIPS_TEST:
        msid = new_id("membership", no, ccode, season)
        cs = clubs[ccode]
        plan_sql = f"(SELECT id FROM membership_plans WHERE club_id = {cs} AND season_id = {season_sq(cs, season)} AND code = {esc(plan)})" if plan else "NULL"
        block(f"""
IF NOT EXISTS (SELECT 1 FROM memberships WHERE member_id = (SELECT id FROM members WHERE member_no = {esc(no)}) AND club_id = {cs} AND season_id = {season_sq(cs, season)})
BEGIN
  INSERT INTO memberships (id, member_id, club_id, season_id, tier, membership_start_on, membership_end_on, status, membership_plan_id)
  VALUES ({esc(msid)}, (SELECT id FROM members WHERE member_no = {esc(no)}), {cs}, {season_sq(cs, season)}, {esc(tier)}, {esc(start)}, {esc(end)}, {esc(status)}, {plan_sql});
  INSERT INTO member_cards (id, membership_id, club_id, holder_name, token, status, issued_at)
  VALUES ({esc(new_id("member_card", msid))}, {esc(msid)}, {cs}, (SELECT name FROM members WHERE member_no = {esc(no)}),
          {esc(hashlib.sha256(("seed-card-token|" + msid).encode()).hexdigest()[:43])}, N'active', SYSUTCDATETIME());
END
""")
        if pay:
            method, amount, paid_on = pay
            block(f"""
IF NOT EXISTS (SELECT 1 FROM membership_payments WHERE membership_id = {esc(msid)})
  INSERT INTO membership_payments (id, membership_id, club_id, collecting_club_id, membership_plan_id, method, amount, paid_on, note, activated_start_on, activated_end_on)
  VALUES ({esc(new_id("membership_payment", msid))}, {esc(msid)}, {cs}, {clubs["tcrfc"]}, {plan_sql}, {esc(method)}, {amount}, {esc(paid_on)}, N'【測試】種子付款紀錄', {esc(start)}, {esc(end)});
""")
    # 家庭方案的副卡（card_quota=3，主卡已在上面建立）
    block(f"""
IF (SELECT COUNT(*) FROM member_cards WHERE membership_id = {esc(new_id("membership", "M900002", "tcrfc", "2026-27"))}) < 2
  INSERT INTO member_cards (id, membership_id, club_id, holder_name, token, status, issued_at)
  VALUES ({esc(new_id("member_card", "M900002-extra-1"))}, {esc(new_id("membership", "M900002", "tcrfc", "2026-27"))}, {clubs["tcrfc"]}, N'【測試】副卡持有人（小童）',
          {esc(hashlib.sha256(b"seed-card-token|M900002-extra-1").hexdigest()[:43])}, N'active', SYSUTCDATETIME());
""")
    JERSEYS_TEST = [  # member no, club, season, recipient, size, method, status, shipped, address
        ("M900001", "tcrfc", "2026-27", "【測試】會員甲", "L", "ship", "pending", None, "【測試】台中市西屯區測試路 1 號"),
        ("M900002", "tcrfc", "2026-27", "【測試】會員乙", "M", "pickup", "pending", None, None),
        ("M900002", "tcrfc", "2026-27", "【測試】副卡持有人（小童）", "S", "ship", "shipped", "2026-09-28", "【測試】台中市北屯區測試路 2 號"),
        ("M900005", "tcrfc", "2026-27", "【測試】會員戊", "XL", "pickup", "received", None, None),
    ]
    for no, ccode, season, recipient, size, method, status, shipped, address in JERSEYS_TEST:
        msid = new_id("membership", no, ccode, season)
        jid = new_id("jersey_issue", no, recipient)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM jersey_issues WHERE id = {esc(jid)})
  INSERT INTO jersey_issues (id, club_id, member_id, membership_id, recipient_name, phone, size, delivery_method, address, status, shipped_on, received_on)
  VALUES ({esc(jid)}, {clubs[ccode]}, (SELECT id FROM members WHERE member_no = {esc(no)}), {esc(msid)}, {esc(recipient)}, N'0900-000-099', {esc(size)}, {esc(method)},
          {esc(address)}, {esc(status)}, {esc(shipped)}, {"'2026-09-29'" if status == "received" else "NULL"});
""")

    emit("-- ── 45. settings：member.no_prefix／no_digits（K2 會員編號規則）與 calendar.*（L3 顯示設定） ──")
    for ccode in ("tcrfc", "bw"):
        setting_value(ccode, "member.no_prefix", "M", "member")
        setting_value(ccode, "member.no_digits", "6", "member")
        setting_value(ccode, "calendar.default_view", "list", "calendar")
        setting_value(ccode, "calendar.default_range", "upcoming", "calendar")
        setting_value(ccode, "calendar.sync_trials", "false", "calendar")
    setting_value("tcrfc", "calendar.default_team", "D1", "calendar")
    setting_value("tcrfc", "calendar.embed.first_team_code", "D1", "calendar")
    setting_value("tcrfc", "calendar.embed.home_teams", jdump(["D1"]), "calendar")
    setting_value("bw", "calendar.default_team", "BW1", "calendar")
    setting_value("bw", "calendar.embed.first_team_code", "BW1", "calendar")
    setting_value("bw", "calendar.embed.home_teams", jdump(["BW1"]), "calendar")

    emit("-- ── 46. calendar_team_settings：L3 隊別分類顯示設定（一線隊顯示名稱與代表色；藍鯨青年隊 U12 示範不公開） ──")
    for ccode, team_code, zh, en, colour, order, public in (
        ("tcrfc", "D1", "一線隊", "First Team", "#0B3D91", 0, 1),
        ("bw", "BW1", "藍鯨一線隊", "Blue Whale First Team", "#2196D5", 0, 1),
        ("bw", "BW-U12", "U12 青年隊", "U12 Youth", None, 3, 0),
    ):
        sid = new_id("calendar_team_setting", team_code)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM calendar_team_settings WHERE team_id = (SELECT id FROM teams WHERE code = N'{team_code}'))
BEGIN
  INSERT INTO calendar_team_settings (id, club_id, team_id, colour, sort_order, is_public)
  VALUES ({esc(sid)}, {clubs[ccode]}, (SELECT id FROM teams WHERE code = N'{team_code}'), {esc(colour)}, {order}, {public});
  INSERT INTO calendar_team_settings_i18n (calendar_team_setting_id, locale, display_name) VALUES ({esc(sid)}, N'zh-Hant', {esc(zh)});
  INSERT INTO calendar_team_settings_i18n (calendar_team_setting_id, locale, display_name) VALUES ({esc(sid)}, N'en', {esc(en)});
END
""")

    emit("-- ── 47. partner_stores／partner_stores_i18n：K4 特約店家（tcrfc 4 家、兩隊共同 1 家、bw 1 家，全部【測試】，座標為台中市區近似值） ──")
    STORES_TEST = [  # slug, club_code|None, category, region, zh name, en name, zh addr, en addr, lat, lng, tier, status, offer zh, offer en
        ("test-store-cafe", "tcrfc", "餐飲", "台中市西屯區", "【測試】示範咖啡館", "Test Cafe", "【測試】台中市西屯區測試路 10 號", "No. 10 Test Rd., Xitun Dist., Taichung", 24.1810, 120.6060, "all", "published",
         "【測試】出示會員卡飲品九折", "Test: 10% off drinks with member card"),
        ("test-store-sports", "tcrfc", "運動用品", "台中市北屯區", "【測試】示範運動用品店", "Test Sports Shop", "【測試】台中市北屯區測試路 20 號", None, 24.1830, 120.7080, "fan_club", "published",
         "【測試】付費會員全店九五折", "Test: 5% off for fan club members"),
        ("test-store-gym", "tcrfc", "健身", "台中市南屯區", "【測試】示範健身房", None, "【測試】台中市南屯區測試路 30 號", None, None, None, "all", "draft",
         "【測試】體驗課程一堂免費（草稿，尚未上架）", None),
        ("test-store-food", "tcrfc", "餐飲", "台中市西區", "【測試】示範小吃店", "Test Snack Shop", "【測試】台中市西區測試路 40 號", None, 24.1400, 120.6640, "all", "published",
         "【測試】招牌小吃加購優惠", "Test: snack combo discount"),
        ("test-store-shared", None, "生活", "台中市", "【測試】兩隊共同特約店家", "Test Shared Partner Store", "【測試】台中市測試路 50 號", "No. 50 Test Rd., Taichung", 24.1500, 120.6800, "all", "published",
         "【測試】兩隊會員皆適用的優惠", "Test: offer valid for both clubs' members"),
        ("test-store-bw", "bw", "餐飲", "台中市豐原區", "【測試】藍鯨示範店家", None, "【測試】台中市豐原區測試路 60 號", None, 24.2520, 120.7220, "all", "published",
         "【測試】藍鯨會員專屬優惠", None),
    ]
    for i, (slug, ccode, cat, region, zh, en, addr_zh, addr_en, lat, lng, tier, status, offer_zh, offer_en) in enumerate(STORES_TEST):
        sid = new_id("partner_store", ccode or "shared", slug)
        club_sql = clubs[ccode] if ccode else "NULL"
        block(f"""
IF NOT EXISTS (SELECT 1 FROM partner_stores WHERE slug = {esc(slug)})
BEGIN
  INSERT INTO partner_stores (id, club_id, slug, category, region, address, lat, lng, phone, website_url, map_url, applicable_tier, start_on, sort_order, status)
  VALUES ({esc(sid)}, {club_sql}, {esc(slug)}, {esc(cat)}, {esc(region)}, {esc(addr_zh)}, {esc(lat)}, {esc(lng)}, N'04-0000-0000', N'https://example.com/{slug}',
          N'https://maps.example.com/{slug}', {esc(tier)}, N'2026-01-01', {i}, {esc(status)});
  INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES ({esc(sid)}, N'zh-Hant', {esc(zh)}, NULL, {esc(offer_zh)});
  {"INSERT INTO partner_stores_i18n (partner_store_id, locale, name, address, offer_content) VALUES (" + esc(sid) + ", N'en', " + esc(en) + ", " + esc(addr_en) + ", " + esc(offer_en) + ");" if en else ""}
END
""")

    emit("-- ── 48. membership_benefits／membership_benefits_i18n：K4 權益對照表（掛在 tcrfc 單人方案，六條【測試】條目，分組涵蓋四類） ──")
    BENEFITS_TEST = [  # group, order, zh name, en name, zh desc, free, paid
        ("member_card", 0, "電子會員卡", "Digital member card", "【測試】手機出示即可", "✓", "✓"),
        ("store_discount", 1, "全會員適用的特約店家折扣", "Partner store discounts (all members)", "【測試】標示「全會員適用」的店家", "✓", "✓"),
        ("store_discount", 2, "限付費會員的特約店家折扣", "Partner store discounts (fan club only)", "【測試】標示「限付費」的店家", "✗", "✓"),
        ("jersey", 3, "入會球衣", "Membership jersey", "【測試】依方案含球衣件數", "✗", "一件"),
        ("event", 4, "球迷活動優先報名", "Priority sign-up for fan events", "【測試】球迷見面會等活動", "✗", "✓"),
        ("event", 5, "球迷會員抽獎資格", "Fan club prize draw eligibility", "【測試】會籍有效期間自動具備", "✗", "✓"),
    ]
    single_plan_sq = f"(SELECT id FROM membership_plans WHERE club_id = {tc} AND season_id = {season_sq(tc, '2026-27')} AND code = N'single')"
    group_labels = {"member_card": ("會員卡", "Member card"), "store_discount": ("店家折扣", "Store discounts"), "jersey": ("球衣", "Jersey"), "event": ("活動", "Events")}
    for group, order, zh, en, desc, free, paid in BENEFITS_TEST:
        bid = new_id("membership_benefit", "tcrfc-single", str(order))
        gl_zh, gl_en = group_labels[group]
        block(f"""
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = {single_plan_sq} AND sort_order = {order})
BEGIN
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_group, sort_order, status)
  VALUES ({esc(bid)}, {single_plan_sq}, {esc(group)}, {order}, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES ({esc(bid)}, N'zh-Hant', {esc(zh)}, {esc(desc)}, {esc(gl_zh)}, {esc(free)}, {esc(paid)});
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, free_value, paid_value)
  VALUES ({esc(bid)}, N'en', {esc(en)}, NULL, {esc(gl_en)}, {esc(free)}, {esc(paid)});
END
""")

    # ========================================================================
    # C1（2026-09-30，S3-1／S3-3／S3-4／S3-8）：F1 漫畫、F2 球迷會活動、S1–S6 站內商店、K5 抽獎名單
    # 🔴 全部是虛構測試資料，一律以「【測試】」前綴或 TEST-／SEED- 代碼標明（見 db/seed/README.md「測試值清單」）。
    # 🔴 個資：收件人、會員一律虛構（example.com／0900-000-XXX／「【測試】」姓名）；不含任何真人資料。
    # 🔴 圖片：不種圖片（沒有可上傳的公開素材），漫畫集數一律草稿（發布規則要求至少一張內頁）。
    # ========================================================================
    tc = clubs["tcrfc"]
    bw = clubs["bw"]

    emit("-- ── 49. F1 漫畫（tcrfc）：企劃設定 settings＋3 個角色＋3 集草稿（藍鯨不設漫畫，不種） ──")
    setting_i18n("tcrfc", "comic.about_title", "【測試】漫畫世界觀", "[Test] Comic universe", "comic")
    setting_i18n("tcrfc", "comic.about_body", "【測試】這是測試用的漫畫世界觀說明，正式內容上線前請於後台替換。", "[Test] Placeholder universe description for the comic.", "comic")
    for i, (zh, en) in enumerate([("【測試】角色甲", "Test Character A"), ("【測試】角色乙", "Test Character B"), ("【測試】角色丙", "Test Character C")]):
        cid = new_id("comic_character", "tcrfc", str(i))
        block(f"""
IF NOT EXISTS (SELECT 1 FROM comic_characters_i18n WHERE comic_character_id = {esc(cid)})
BEGIN
  INSERT INTO comic_characters (id, club_id, sort_order) VALUES ({esc(cid)}, {tc}, {i});
  INSERT INTO comic_characters_i18n (comic_character_id, locale, name, description) VALUES ({esc(cid)}, N'zh-Hant', {esc(zh)}, N'【測試】角色設定占位文字。');
  INSERT INTO comic_characters_i18n (comic_character_id, locale, name, description) VALUES ({esc(cid)}, N'en', {esc(en)}, NULL);
END
""")
    for no in (1, 2, 3):
        eid = new_id("comic_episode", "tcrfc", str(no))
        block(f"""
IF NOT EXISTS (SELECT 1 FROM comic_episodes WHERE club_id = {tc} AND episode_no = {no})
BEGIN
  INSERT INTO comic_episodes (id, club_id, episode_no, status, is_latest, view_count) VALUES ({esc(eid)}, {tc}, {no}, N'draft', 0, 0);
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES ({esc(eid)}, N'zh-Hant', {esc(f"【測試】第 {no} 集")});
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES ({esc(eid)}, N'en', {esc(f"[Test] Episode {no}")});
END
""")

    emit("-- ── 50. F2 球迷會活動：tcrfc 3 場（付費限定／公開／草稿）＋報名、bw 1 場 ──")
    FAN_EVENTS = [  # slug, club, name zh, en, description, days offset, capacity, paid_only, status
        ("test-fan-meet", "tcrfc", "【測試】球迷見面會（付費會員限定）", "[Test] Fan meet-up (fan club members only)", "【測試】球員與付費球迷會員面對面，名額 30 人。", 14, 30, 1, "published"),
        ("test-match-day-party", "tcrfc", "【測試】主場賽事日球迷派對", "[Test] Match-day fan party", "【測試】主場賽事日的球迷同樂活動，不限名額。", 30, None, 0, "published"),
        ("test-past-event", "tcrfc", "【測試】上季球迷活動回顧（草稿）", "[Test] Last season fan event (draft)", "【測試】已結束的活動，回顧圖集與文章尚未整理。", -30, 50, 0, "draft"),
        ("test-bw-fan-day", "bw", "【測試】藍鯨球迷日", "[Test] Blue Whale fan day", "【測試】藍鯨方的球迷活動示範。", 20, 40, 0, "published"),
    ]
    for slug, ccode, zh, en, desc, days, cap, paid_only, status in FAN_EVENTS:
        fid = new_id("fan_event", ccode, slug)
        cs = clubs[ccode]
        block(f"""
IF NOT EXISTS (SELECT 1 FROM fan_events WHERE club_id = {cs} AND slug = {esc(slug)})
BEGIN
  INSERT INTO fan_events (id, club_id, slug, starts_at, ends_at, registration_deadline_at, capacity, is_paid_members_only, status)
  VALUES ({esc(fid)}, {cs}, {esc(slug)}, DATEADD(day, {days}, SYSUTCDATETIME()), DATEADD(hour, 3, DATEADD(day, {days}, SYSUTCDATETIME())),
          DATEADD(day, {days - 2}, SYSUTCDATETIME()), {esc(cap)}, {paid_only}, {esc(status)});
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES ({esc(fid)}, N'zh-Hant', {esc(zh)}, {esc(desc)}, N'【測試】台中市西屯區測試路 1 號');
  INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES ({esc(fid)}, N'en', {esc(en)}, NULL, NULL);
END
""")
    for slug, ccode, member_no, guest, status in (
        ("test-fan-meet", "tcrfc", "M900001", None, "registered"),
        ("test-fan-meet", "tcrfc", "M900002", None, "registered"),
        ("test-match-day-party", "tcrfc", "M900003", None, "registered"),
        ("test-match-day-party", "tcrfc", None, "【測試】路人甲", "waitlist"),
    ):
        fid = new_id("fan_event", ccode, slug)
        rid = new_id("fan_event_registration", slug, member_no or guest)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM fan_event_registrations WHERE id = {esc(rid)})
  INSERT INTO fan_event_registrations (id, club_id, fan_event_id, member_id, status, applicant_name, phone, email)
  VALUES ({esc(rid)}, {clubs[ccode]}, {esc(fid)}, {f"(SELECT id FROM members WHERE member_no = {esc(member_no)})" if member_no else "NULL"}, {esc(status)},
          {esc(guest)}, {esc("0900-000-301" if guest else None)}, {esc("guest@example.com" if guest else None)});
""")

    emit("-- ── 51. S6 商店設定、發票捐贈碼（虛構代碼 999000X） ──")
    SHOP_SETTINGS = {
        "tcrfc": {"shipping_fee": "80", "free_shipping_threshold": "2000", "excluded_regions": jdump(["【測試】離島地區"]), "low_stock_threshold": "5", "pending_timeout_minutes": "30"},
        "bw": {"shipping_fee": "100", "free_shipping_threshold": "1500", "excluded_regions": jdump(["【測試】離島地區"]), "low_stock_threshold": "3", "pending_timeout_minutes": "30"},
    }
    for ccode, values in SHOP_SETTINGS.items():
        for key, value in values.items():
            setting_value(ccode, "shop." + key, value, "shop")
        setting_i18n(ccode, "shop.entry_title", "【測試】官方商店", "[Test] Official shop", "shop")
        setting_i18n(ccode, "shop.entry_intro", "【測試】商店入口說明占位文字。", "[Test] Shop entry placeholder.", "shop")
        setting_i18n(ccode, "shop.policy_shipping", "【測試】運送說明占位文字：宅配、超商取貨與現場自取。", "[Test] Shipping policy placeholder.", "shop")
        setting_i18n(ccode, "shop.policy_returns", "【測試】退換貨政策占位文字。", "[Test] Returns policy placeholder.", "shop")
    for code, name, active, order in (("9990001", "【測試】示範公益團體甲", 1, 0), ("9990002", "【測試】示範公益團體乙（已停用）", 0, 1)):
        block(f"""
IF NOT EXISTS (SELECT 1 FROM invoice_donation_codes WHERE code = {esc(code)})
  INSERT INTO invoice_donation_codes (id, code, org_name, is_active, sort_order) VALUES ({esc(new_id("donation_code", code))}, {esc(code)}, {esc(name)}, {active}, {order});
""")

    emit("-- ── 52. S1／S2 商品系列、商品、規格與庫存（tcrfc 5 件、bw 1 件；含低庫存與缺貨示範） ──")
    COLLECTIONS = [  # club, slug, zh, en, narrative, order
        ("tcrfc", "test-club-collection", "【測試】俱樂部系列", "[Test] Club collection", "【測試】俱樂部系列的品牌敘事占位文字。", 0),
        ("tcrfc", "test-academy-collection", "【測試】學院系列", "[Test] Academy collection", "【測試】學院系列的品牌敘事占位文字。", 1),
        ("tcrfc", "test-fan-collection", "【測試】球迷系列", "[Test] Fan collection", "【測試】球迷系列的品牌敘事占位文字。", 2),
        ("bw", "test-bw-collection", "【測試】藍鯨系列", "[Test] Blue Whale collection", "【測試】藍鯨系列的品牌敘事占位文字。", 0),
    ]
    for ccode, slug, zh, en, narrative, order in COLLECTIONS:
        col_id = new_id("collection", ccode, slug)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM collections WHERE club_id = {clubs[ccode]} AND slug = {esc(slug)})
BEGIN
  INSERT INTO collections (id, club_id, slug, sort_order, status) VALUES ({esc(col_id)}, {clubs[ccode]}, {esc(slug)}, {order}, N'published');
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES ({esc(col_id)}, N'zh-Hant', {esc(zh)}, {esc(narrative)});
  INSERT INTO collections_i18n (collection_id, locale, name, narrative) VALUES ({esc(col_id)}, N'en', {esc(en)}, NULL);
END
""")
    # club, slug, collection slug, zh, en, status, new, (sku, size, colour, price, sale, cost, initial_stock)
    PRODUCTS = [
        ("tcrfc", "test-home-jersey", "test-club-collection", "【測試】主場球衣", "[Test] Home jersey", "published", 1,
         [("TEST-JSY-S", "S", "藍", 1800, None, 900, 20), ("TEST-JSY-M", "M", "藍", 1800, None, 900, 20), ("TEST-JSY-L", "L", "藍", 1800, None, 900, 20), ("TEST-JSY-XL", "XL", "藍", 1800, 1600, 900, 12)]),
        ("tcrfc", "test-training-jacket", "test-academy-collection", "【測試】訓練外套", "[Test] Training jacket", "published", 0,
         [("TEST-JKT-M", "M", "黑", 1500, None, 700, 10), ("TEST-JKT-L", "L", "黑", 1500, None, 700, 10)]),
        ("tcrfc", "test-scarf", "test-fan-collection", "【測試】球迷圍巾", "[Test] Fan scarf", "published", 0,
         [("TEST-SCARF", None, "藍白", 500, 450, 200, 6)]),
        ("tcrfc", "test-limited-ball", "test-fan-collection", "【測試】限量紀念球（缺貨示範）", "[Test] Limited ball (sold-out demo)", "published", 1,
         [("TEST-BALL", None, None, 1200, None, 600, 1)]),
        ("tcrfc", "test-draft-product", "test-fan-collection", "【測試】尚未上架的商品", "[Test] Draft product", "draft", 0,
         [("TEST-DRAFT", None, None, 300, None, 100, 5)]),
        ("bw", "test-bw-jersey", "test-bw-collection", "【測試】藍鯨球衣", "[Test] Blue Whale jersey", "published", 1,
         [("TEST-BW-JSY-M", "M", "藍", 1600, None, 800, 15), ("TEST-BW-JSY-L", "L", "藍", 1600, None, 800, 15)]),
    ]
    variant_id = {}
    for order, (ccode, slug, col_slug, zh, en, status, is_new, variants) in enumerate(PRODUCTS):
        cs = clubs[ccode]
        pid = new_id("product", ccode, slug)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM products WHERE club_id = {cs} AND slug = {esc(slug)})
BEGIN
  INSERT INTO products (id, club_id, slug, collection_id, is_new_arrival, sort_order, status, out_of_stock_behavior)
  VALUES ({esc(pid)}, {cs}, {esc(slug)}, (SELECT id FROM collections WHERE club_id = {cs} AND slug = {esc(col_slug)}), {is_new}, {order}, {esc(status)}, N'show_unavailable');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES ({esc(pid)}, N'zh-Hant', {esc(zh)}, N'【測試】商品敘事占位文字。', N'【測試】,示範');
  INSERT INTO products_i18n (product_id, locale, name, narrative, tags) VALUES ({esc(pid)}, N'en', {esc(en)}, NULL, NULL);
END
""")
        for vi, (sku, size, colour, price, sale, cost, initial) in enumerate(variants):
            vid = new_id("product_variant", sku)
            variant_id[sku] = (vid, ccode)
            block(f"""
IF NOT EXISTS (SELECT 1 FROM product_variants WHERE sku = {esc(sku)})
BEGIN
  INSERT INTO product_variants (id, club_id, product_id, sku, size, colour, price, sale_price, cost, stock_qty, reserved_qty, status, sort_order)
  VALUES ({esc(vid)}, {cs}, {esc(pid)}, {esc(sku)}, {esc(size)}, {esc(colour)}, {price}, {esc(sale)}, {cost}, 0, 0, N'active', {vi});
  UPDATE product_variants SET stock_qty = {initial}, updated_at = SYSUTCDATETIME() WHERE id = {esc(vid)};
  INSERT INTO inventory_movements (id, club_id, product_variant_id, movement_type, quantity, stock_after, reserved_after, reason)
  VALUES ({esc(new_id("inventory_movement", sku, "initial"))}, {cs}, {esc(vid)}, N'stock_in', {initial}, {initial}, 0, N'【測試】初始庫存');
END
""")

    emit("-- ── 53. S3／S4／S5 訂單（tcrfc 8 張涵蓋各狀態＋現場收款、bw 1 張代收代付）、出貨、退款案件；庫存與異動同步 ──")
    # order key, club, status, payment_status, method, delivery, hours_ago, lines [(sku, qty)], recipient idx, manual, shipping_fee
    ORDERS = [
        ("TR-SEED-0001", "tcrfc", "待付款", "pending", "linepay", "home_delivery", 0.2, [("TEST-JSY-M", 1)], 1, 0, 80),
        ("TR-SEED-0002", "tcrfc", "已付款", "paid", "linepay", "home_delivery", 6, [("TEST-SCARF", 2)], 2, 0, 80),
        ("TR-SEED-0003", "tcrfc", "備貨中", "paid", "linepay", "cvs_pickup", 30, [("TEST-JSY-L", 1), ("TEST-JKT-M", 1)], 3, 0, 60),
        ("TR-SEED-0004", "tcrfc", "已出貨", "paid", "linepay", "home_delivery", 55, [("TEST-JSY-S", 1)], 4, 0, 80),
        ("TR-SEED-0005", "tcrfc", "已完成", "paid", "linepay", "cvs_pickup", 200, [("TEST-JKT-L", 1)], 5, 0, 60),
        ("TR-SEED-0006", "tcrfc", "已取消", "paid", "linepay", "home_delivery", 80, [("TEST-SCARF", 1)], 6, 0, 80),
        ("TR-SEED-0007", "tcrfc", "退貨處理中", "paid", "linepay", "home_delivery", 260, [("TEST-JSY-XL", 2)], 7, 0, 0),
        ("TR-SEED-0008", "tcrfc", "已完成", "paid", "onsite", "onsite_pickup", 100, [("TEST-BALL", 1)], 8, 1, 0),
        ("BW-SEED-0001", "bw", "已付款", "paid", "linepay", "home_delivery", 12, [("TEST-BW-JSY-M", 1)], 9, 0, 100),
    ]
    sold_by_variant = {}
    reserved_by_variant = {}
    for key, ccode, status, pay_status, method, delivery, hours, lines, ridx, manual, ship_fee in ORDERS:
        cs = clubs[ccode]
        oid = new_id("order", key)
        subtotal = 0
        item_sql = []
        movement_sql = []
        for sku, qty in lines:
            vid, _ = variant_id[sku]
            price = next(v[3] if v[4] is None else v[4] for p in PRODUCTS for v in p[7] if v[0] == sku)
            subtotal += price * qty
            iid = new_id("order_item", key, sku)
            label_row = next(v for p in PRODUCTS for v in p[7] if v[0] == sku)
            label = "／".join(x for x in (label_row[1], label_row[2]) if x)
            pname = next(p[3] for p in PRODUCTS if any(v[0] == sku for v in p[7]))
            item_sql.append(f"INSERT INTO order_items (id, club_id, order_id, product_variant_id, product_name_snapshot, variant_label_snapshot, sku_snapshot, unit_price_snapshot, quantity, line_total) "
                            f"VALUES ({esc(iid)}, {cs}, {esc(oid)}, {esc(vid)}, {esc(pname)}, {esc(label or None)}, {esc(sku)}, {price}, {qty}, {price * qty});")
            if status == "待付款":
                reserved_by_variant[sku] = reserved_by_variant.get(sku, 0) + qty
                movement_sql.append((sku, "reserve", qty, "【測試】下單保留"))
            else:
                sold_by_variant[sku] = sold_by_variant.get(sku, 0) + qty
                movement_sql.append((sku, "sale", -qty, "【測試】" + ("現場收款訂單" if manual else "付款成立，扣減庫存")))
                if status == "已取消":
                    sold_by_variant[sku] -= qty
                    movement_sql.append((sku, "cancel_restock", qty, "【測試】取消未出貨訂單"))
        total = subtotal + ship_fee
        completed = "SYSUTCDATETIME()" if status == "已完成" else "NULL"
        cancelled = "SYSUTCDATETIME()" if status == "已取消" else "NULL"
        block(f"""
IF NOT EXISTS (SELECT 1 FROM orders WHERE order_no = {esc(key)})
BEGIN
  INSERT INTO orders (id, order_no, club_id, selling_club_id, collecting_club_id, lookup_token, recipient_name, recipient_phone, recipient_address,
                      subtotal, shipping_fee, total, payment_status, order_status, delivery_method, is_manual, payment_method, paid_at, completed_at,
                      cancelled_at, cancel_reason, customer_note, internal_note, settlement_status, created_at)
  VALUES ({esc(oid)}, {esc(key)}, {cs}, {cs}, {clubs["tcrfc"]}, {esc(hashlib.sha256(("seed-order-token|" + key).encode()).hexdigest()[:43])},
          {esc(f"【測試】收件人{ridx}")}, {esc(f"0900-000-2{ridx:02d}")}, {esc(f"【測試】台中市測試區測試路 {ridx} 號")},
          {subtotal}, {ship_fee}, {total}, {esc(pay_status)}, {esc(status)}, {esc(delivery)}, {manual}, {esc(method)},
          {"DATEADD(minute, -" + str(int(hours * 60) - 5) + ", SYSUTCDATETIME())" if pay_status == "paid" else "NULL"}, {completed}, {cancelled},
          {esc("【測試】顧客取消" if status == "已取消" else None)}, {esc("【測試】示範訂單" if ridx in (2, 5) else None)},
          {esc("【測試】內部註記示範" if ridx == 3 else None)}, N'pending', DATEADD(minute, -{int(hours * 60)}, SYSUTCDATETIME()));
  {chr(10).join("  " + s for s in item_sql)}
END
""")
        for sku, mtype, qty, reason in movement_sql:
            vid, _ = variant_id[sku]
            block(f"""
IF NOT EXISTS (SELECT 1 FROM inventory_movements WHERE id = {esc(new_id("inventory_movement", key, sku, mtype))})
  INSERT INTO inventory_movements (id, club_id, product_variant_id, order_id, movement_type, quantity, reason)
  VALUES ({esc(new_id("inventory_movement", key, sku, mtype))}, {cs}, {esc(vid)}, {esc(oid)}, {esc(mtype)}, {qty}, {esc(reason)});
""")
    # 依訂單結算庫存水位（只在種子第一次寫入時動；用 UPDATE 依「初始庫存－已售＋…」重算，冪等：以異動總和為準）
    for sku, (vid, _) in variant_id.items():
        block(f"""
UPDATE product_variants SET
  stock_qty = COALESCE((SELECT SUM(CASE WHEN movement_type IN (N'reserve', N'release') THEN 0 ELSE quantity END) FROM inventory_movements WHERE product_variant_id = {esc(vid)}), 0),
  reserved_qty = COALESCE((SELECT SUM(CASE WHEN movement_type IN (N'reserve', N'release') THEN quantity ELSE 0 END) FROM inventory_movements WHERE product_variant_id = {esc(vid)}), 0)
WHERE id = {esc(vid)};
""")
    # 出貨資料
    block(f"""
IF NOT EXISTS (SELECT 1 FROM shipments WHERE order_id = {esc(new_id("order", "TR-SEED-0004"))})
  INSERT INTO shipments (id, club_id, order_id, carrier, tracking_no, shipped_at)
  VALUES ({esc(new_id("shipment", "TR-SEED-0004"))}, {tc}, {esc(new_id("order", "TR-SEED-0004"))}, N'【測試】物流商', N'TEST0000000004', DATEADD(hour, -30, SYSUTCDATETIME()));
IF NOT EXISTS (SELECT 1 FROM shipments WHERE order_id = {esc(new_id("order", "TR-SEED-0005"))})
  INSERT INTO shipments (id, club_id, order_id, carrier, tracking_no, store_branch_code, shipped_at, delivered_at, pickup_status, pickup_deadline_on, arrival_notified_at)
  VALUES ({esc(new_id("shipment", "TR-SEED-0005"))}, {tc}, {esc(new_id("order", "TR-SEED-0005"))}, N'【測試】超商物流', N'TEST0000000005', N'TEST001',
          DATEADD(hour, -150, SYSUTCDATETIME()), DATEADD(hour, -100, SYSUTCDATETIME()), N'picked_up', CAST(DATEADD(day, -3, SYSUTCDATETIME()) AS date), DATEADD(hour, -120, SYSUTCDATETIME()));
IF NOT EXISTS (SELECT 1 FROM shipments WHERE order_id = {esc(new_id("order", "TR-SEED-0008"))})
  INSERT INTO shipments (id, club_id, order_id, shipped_at, delivered_at, pickup_status)
  VALUES ({esc(new_id("shipment", "TR-SEED-0008"))}, {tc}, {esc(new_id("order", "TR-SEED-0008"))}, DATEADD(hour, -100, SYSUTCDATETIME()), DATEADD(hour, -100, SYSUTCDATETIME()), N'picked_up');
""")
    # 退款案件：TR-SEED-0006（取消未出貨，自動建立、不需退回、已核准）；TR-SEED-0007（申請中，部分退款，需退回）
    o6 = new_id("order", "TR-SEED-0006")
    o7 = new_id("order", "TR-SEED-0007")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM refund_requests WHERE order_id = {esc(o6)})
BEGIN
  INSERT INTO refund_requests (id, club_id, order_id, reason, status, refund_amount, needs_return, review_note)
  VALUES ({esc(new_id("refund", "TR-SEED-0006"))}, {tc}, {esc(o6)}, N'取消訂單：【測試】顧客取消', N'approved', (SELECT total FROM orders WHERE id = {esc(o6)}), 0, N'取消未出貨訂單，系統自動建立全額退款案件');
  INSERT INTO refund_request_items (refund_request_id, order_item_id, quantity)
  SELECT {esc(new_id("refund", "TR-SEED-0006"))}, id, quantity FROM order_items WHERE order_id = {esc(o6)};
END
IF NOT EXISTS (SELECT 1 FROM refund_requests WHERE order_id = {esc(o7)})
BEGIN
  INSERT INTO refund_requests (id, club_id, order_id, reason, status, refund_amount, needs_return)
  VALUES ({esc(new_id("refund", "TR-SEED-0007"))}, {tc}, {esc(o7)}, N'【測試】尺寸不合，申請退一件', N'requested', 1600, 1);
  INSERT INTO refund_request_items (refund_request_id, order_item_id, quantity)
  SELECT TOP 1 {esc(new_id("refund", "TR-SEED-0007"))}, id, 1 FROM order_items WHERE order_id = {esc(o7)};
END
""")

    emit("-- ── 54. K5 抽獎名單：蒐集告知確認、tcrfc 1 場已抽出（名單 2 人、1 位中獎）＋1 場草稿 ──")
    setting_value("tcrfc", "member.draw_notice_confirmed", "2026-09-30T00:00:00.0000000Z", "member")
    DRAW_ROSTER = [(1, "M900001", "【測試】會員甲", "2027-05-02"), (2, "M900002", "【測試】會員乙", "2027-05-02")]
    roster_text = "TEST-DRAW-01|1\n" + "".join(f"{s}|{m}|{n}|fan_club|{e}\n" for s, m, n, e in DRAW_ROSTER)
    roster_hash = hashlib.sha256(roster_text.encode("utf-8")).hexdigest()
    d1 = new_id("member_draw", "tcrfc", "TEST-DRAW-01")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM member_draws WHERE club_id = {tc} AND draw_code = N'TEST-DRAW-01')
BEGIN
  INSERT INTO member_draws (id, club_id, draw_code, snapshot_at, drawn_at, draw_occasion, claim_deadline_on, status, roster_version, total_count, roster_hash, locked_at)
  VALUES ({esc(d1)}, {tc}, N'TEST-DRAW-01', '2026-09-25T16:00:00', '2026-09-26T10:00:00', N'home_match', '2026-12-31', N'drawn', 1, 2, {esc(roster_hash)}, SYSUTCDATETIME());
  INSERT INTO member_draws_i18n (member_draw_id, locale, name, prize_description, rules, notes) VALUES
    ({esc(d1)}, N'zh-Hant', N'【測試】主場賽事日球迷抽獎', N'【測試】簽名球衣一件', N'【測試】活動辦法占位文字：資格為基準時間當下持有本俱樂部有效球迷會員會籍；同時具備兩隊會籍者可分別參加兩隊抽獎。', N'【測試】注意事項占位文字');
  INSERT INTO member_draws_i18n (member_draw_id, locale, name, prize_description, rules, notes) VALUES
    ({esc(d1)}, N'en', N'[Test] Match-day fan prize draw', N'[Test] One signed jersey', NULL, NULL);
  INSERT INTO draw_roster_versions (id, member_draw_id, roster_version, snapshot_at, total_count, roster_hash) VALUES ({esc(new_id("draw_roster_version", "TEST-DRAW-01", "1"))}, {esc(d1)}, 1, '2026-09-25T16:00:00', 2, {esc(roster_hash)});
  {"".join(f"INSERT INTO draw_rosters (id, club_id, member_draw_id, roster_version, serial_no, member_no_snapshot, name_snapshot, tier_snapshot, membership_end_on_snapshot, is_winner, prize_name, claim_method, fulfilment_status, recipient_name, recipient_phone, recipient_address) VALUES ({esc(new_id('draw_roster', 'TEST-DRAW-01', str(s)))}, {tc}, {esc(d1)}, 1, {s}, {esc(m)}, {esc(n)}, N'fan_club', {esc(e)}, {1 if s == 1 else 0}, {esc('【測試】簽名球衣' if s == 1 else None)}, {esc('ship' if s == 1 else None)}, {esc('pending' if s == 1 else None)}, {esc('【測試】會員甲' if s == 1 else None)}, {esc('0900-000-001' if s == 1 else None)}, {esc('【測試】台中市西屯區測試路 1 號' if s == 1 else None)});" for s, m, n, e in DRAW_ROSTER)}
END
""")
    d2 = new_id("member_draw", "tcrfc", "TEST-DRAW-02")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM member_draws WHERE club_id = {tc} AND draw_code = N'TEST-DRAW-02')
BEGIN
  INSERT INTO member_draws (id, club_id, draw_code, snapshot_at, drawn_at, draw_occasion, status, roster_version)
  VALUES ({esc(d2)}, {tc}, N'TEST-DRAW-02', DATEADD(day, 20, SYSUTCDATETIME()), DATEADD(day, 20, SYSUTCDATETIME()), N'livestream', N'draft', 1);
  INSERT INTO member_draws_i18n (member_draw_id, locale, name, prize_description, rules, notes) VALUES
    ({esc(d2)}, N'zh-Hant', N'【測試】直播球迷抽獎（草稿）', N'【測試】球迷圍巾三條', NULL, NULL);
END
""")

    # ── 55. D 批：G3 電子報、E4–E6 廣告、M1–M5 App 後台 ───────────────────────────────────────────────
    # 🔴 全部是【測試】虛構資料（電子報 Email 用 example.com、廣告主與裝置一眼可辨）。真實結構（首頁九個區塊、深連結對照、
    #   功能開關預設）取自 App 規劃書 §3.1／§2.3 與 docs/19 §7，不加【測試】前綴。沒有圖片（廣告素材 image_key 為 NULL，公開投放端點會回沒有圖的項目——
    #   種子只用於後台畫面驗證，不用於 App 實測）。
    bwc = clubs["bw"]
    emit("-- ── 55. newsletter_subscribers：G3 電子報名單（tcrfc 3 訂閱＋1 退訂、bw 1 訂閱；example.com） ──")
    for club_sq, club_code, email, status, source in [
        (tc, "tcrfc", "test.subscriber1@example.com", "subscribed", "官網頁尾訂閱"),
        (tc, "tcrfc", "test.subscriber2@example.com", "subscribed", "活動現場填單"),
        (tc, "tcrfc", "test.subscriber3@example.com", "subscribed", "官網頁尾訂閱"),
        (tc, "tcrfc", "test.unsubscribed@example.com", "unsubscribed", "官網頁尾訂閱"),
        (bwc, "bw", "test.subscriber-bw@example.com", "subscribed", "官網頁尾訂閱"),
    ]:
        block(f"""
IF NOT EXISTS (SELECT 1 FROM newsletter_subscribers WHERE club_id = {club_sq} AND email = {esc(email)})
  INSERT INTO newsletter_subscribers (id, club_id, email, source, status, subscribed_at, unsubscribed_at)
  VALUES ({esc(new_id("newsletter", club_code, email))}, {club_sq}, {esc(email)}, {esc(source)}, {esc(status)}, DATEADD(day, -20, SYSUTCDATETIME()),
          {"DATEADD(day, -5, SYSUTCDATETIME())" if status == "unsubscribed" else "NULL"});
""")

    emit("-- ── 56. app_deep_links／app_layout_items：M2 深連結對照（規劃書 §2.3 的 8 條）、首頁九個區塊、快捷入口、「更多」分頁 ──")
    DEEP_LINKS = [
        ("schedule_d1", "tcrfc://schedule/d1", "/zh/schedule/d1/", 0, "賽程（磐石一線隊）", "Schedule (Rock FC First Team)"),
        ("schedule_bw1", "tcrfc://schedule/bw1", None, 0, "賽程（藍鯨一線隊）", "Schedule (Blue Whale First Team)"),
        ("match", "tcrfc://match/{id}", "/zh/schedule/{slug}", 0, "賽事詳情", "Match Details"),
        ("news", "tcrfc://news/{slug}", "/zh/news/{slug}", 0, "新聞內文", "News Article"),
        ("player", "tcrfc://player/{slug}", "/zh/club/first-team/player/{slug}", 0, "球員詳情", "Player Profile"),
        ("store", "tcrfc://store/{id}", "/zh/perks/{slug}", 0, "特約店家詳情", "Partner Store"),
        ("program", "tcrfc://program/{slug}", "/zh/programs/{slug}", 0, "課程詳情", "Program Details"),
        ("membercard", "tcrfc://membercard", None, 1, "會員卡", "Membership Card"),
        ("upgrade", "tcrfc://upgrade", "/zh/member/upgrade/", 0, "會籍升級", "Membership Upgrade"),
    ]
    for i, (code, app_link, web, login, zh, en) in enumerate(DEEP_LINKS):
        lid = new_id("app_deep_link", code)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM app_deep_links WHERE code = {esc(code)})
BEGIN
  INSERT INTO app_deep_links (id, code, app_link, web_url, requires_login, is_active, sort_order)
  VALUES ({esc(lid)}, {esc(code)}, {esc(app_link)}, {esc(web) if web else 'NULL'}, {login}, 1, {i});
  INSERT INTO app_deep_links_i18n (app_deep_link_id, locale, label) VALUES ({esc(lid)}, N'zh-Hant', {esc(zh)}), ({esc(lid)}, N'en', {esc(en)});
END
""")
    LAYOUT = [
        ("home_section", "next_match", None, "下一場賽事", "Next Match"),
        ("home_section", "ad_home_top", None, "廣告版位（首頁上）", "Ad Slot (Home Top)"),
        ("home_section", "latest_news", None, "最新消息", "Latest News"),
        ("home_section", "member_card", "membercard", "會員卡快捷", "Membership Card"),
        ("home_section", "recent_matches", None, "近期賽事", "Recent Matches"),
        ("home_section", "ad_home_mid", None, "廣告版位（首頁中）", "Ad Slot (Home Middle)"),
        ("home_section", "nearby_stores", None, "特約店家（附近）", "Nearby Partner Stores"),
        ("home_section", "quick_entries", None, "快捷入口", "Quick Entries"),
        ("home_section", "sponsor_wall", None, "贊助商 Logo 牆", "Sponsors"),
        ("quick_entry", "programs", "program", "課程報名", "Programs"),
        ("quick_entry", "shop", None, "商店", "Shop"),
        ("more_item", "teams", None, "球隊名單", "Teams"),
        ("more_item", "partner_stores", None, "特約店家", "Partner Stores"),
        ("more_item", "programs", "program", "課程報名", "Programs"),
        ("more_item", "partners", None, "夥伴贊助", "Partners & Sponsors"),
        ("more_item", "charity", None, "慈善（外連）", "Charity (external)"),
        ("more_item", "shop", None, "商店", "Shop"),
        ("more_item", "faq", None, "FAQ", "FAQ"),
        ("more_item", "follow_settings", None, "追蹤設定", "Follow Settings"),
        ("more_item", "settings", None, "設定", "Settings"),
    ]
    order: dict = {}
    for kind, key, link, zh, en in LAYOUT:
        n = order.get(kind, 0)
        order[kind] = n + 1
        iid = new_id("app_layout_item", kind, key)
        link_sql = f"(SELECT id FROM app_deep_links WHERE code = {esc(link)})" if link else "NULL"
        block(f"""
IF NOT EXISTS (SELECT 1 FROM app_layout_items WHERE kind = {esc(kind)} AND item_key = {esc(key)})
BEGIN
  INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled)
  VALUES ({esc(iid)}, {esc(kind)}, {esc(key)}, {link_sql}, {n}, 1);
  INSERT INTO app_layout_items_i18n (app_layout_item_id, locale, label) VALUES ({esc(iid)}, N'zh-Hant', {esc(zh)}), ({esc(iid)}, N'en', {esc(en)});
END
""")
    a1 = new_id("app_announcement", "test-1")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM app_announcements WHERE id = {esc(a1)})
BEGIN
  INSERT INTO app_announcements (id, link_url, starts_at, ends_at, audience_tier, is_enabled)
  VALUES ({esc(a1)}, NULL, DATEADD(day, -1, SYSUTCDATETIME()), DATEADD(year, 5, SYSUTCDATETIME()), N'all', 1);
  INSERT INTO app_announcements_i18n (app_announcement_id, locale, message)
  VALUES ({esc(a1)}, N'zh-Hant', N'【測試】App 專屬公告條：歡迎使用台中磐石 × 台中藍鯨官方 App。'), ({esc(a1)}, N'en', N'[Test] App announcement bar.');
END
""")

    emit("-- ── 57. app_feature_flags／app_releases／app_credentials：M1、M5（功能開關預設取自 docs/19 §7、§10；版本與憑證為【測試】） ──")
    for key, enabled, sval, desc in [
        ("ads_enabled", 1, None, "廣告版位總開關"), ("map_enabled", 1, None, "附近店家地圖"),
        ("biometric_unlock_enabled", 0, None, "會員卡生物辨識快速開啟（選配）"), ("payment_mode", 1, "external", "付款模式（off／external／inapp）：首個上架版本不含 App 內付款，只能降級不得反向開啟 inapp"),
    ]:
        block(f"""
IF NOT EXISTS (SELECT 1 FROM app_feature_flags WHERE flag_key = {esc(key)} AND platform = N'all')
  INSERT INTO app_feature_flags (id, flag_key, is_enabled, string_value, platform, description)
  VALUES ({esc(new_id("app_flag", key))}, {esc(key)}, {enabled}, {esc(sval) if sval else 'NULL'}, N'all', {esc(desc)});
""")
    for platform, version, status, is_min, is_rec, note in [
        ("ios", "0.9.0", "live", 1, 0, "【測試】最低支援版本示範"), ("ios", "1.0.0", "testing", 0, 0, "【測試】測試中的版本"),
        ("android", "0.9.0", "live", 1, 0, "【測試】最低支援版本示範"),
    ]:
        rid = new_id("app_release", platform, version)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM app_releases WHERE platform = {esc(platform)} AND version = {esc(version)})
BEGIN
  INSERT INTO app_releases (id, platform, version, build_number, released_on, status, is_min_supported, is_recommended)
  VALUES ({esc(rid)}, {esc(platform)}, {esc(version)}, N'900', '2026-09-30', {esc(status)}, {is_min}, {is_rec});
  INSERT INTO app_releases_i18n (app_release_id, locale, whats_new, force_message, recommend_message)
  VALUES ({esc(rid)}, N'zh-Hant', {esc(note)}, N'【測試】請更新到最新版本才能繼續使用。', N'【測試】有新版本可以更新。'),
         ({esc(rid)}, N'en', N'[Test] release', N'[Test] Please update to keep using the app.', N'[Test] A new version is available.');
END
""")
    for kind, label, ref, created, rotated, expires, period in [
        ("apns_key", "【測試】APNs 金鑰（.p8）", "TESTKEYID01", "2026-01-15", "2026-06-01", None, 365),
        ("fcm_credential", "【測試】FCM 服務帳號", "test-project-000", "2026-01-15", None, None, 180),
        ("apple_developer_program", "【測試】Apple 開發者帳號會籍", None, "2025-11-01", None, "2026-11-20", None),
    ]:
        block(f"""
IF NOT EXISTS (SELECT 1 FROM app_credentials WHERE kind = {esc(kind)} AND label = {esc(label)})
  INSERT INTO app_credentials (id, kind, label, external_ref, created_on, last_rotated_on, expires_on, rotation_period_days, note)
  VALUES ({esc(new_id("app_credential", kind))}, {esc(kind)}, {esc(label)}, {esc(ref) if ref else 'NULL'}, '{created}', {f"'{rotated}'" if rotated else 'NULL'},
          {f"'{expires}'" if expires else 'NULL'}, {period if period else 'NULL'}, N'【測試】只存列管資訊，金鑰本身不進資料庫。');
""")

    emit("-- ── 58. ad_slots／advertisers／ad_campaigns／ad_creatives／ad_daily_stats：E4–E6（2 版位、2 廣告主、1 個已結束的曝光保證檔期含 14 天示範成效；【測試】） ──")
    for code, screen, order_no, ratio, minw, minh, zh, en in [
        ("home_top", "S01", 2, "16:9", 1280, 720, "首頁上方版位", "Home Top"),
        ("home_mid", "S01", 6, "3:1", 1200, 400, "首頁中段版位", "Home Middle"),
    ]:
        sid = new_id("ad_slot", code)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM ad_slots WHERE slot_code = {esc(code)})
BEGIN
  INSERT INTO ad_slots (id, slot_code, surface, screen_code, block_order, aspect_ratio, min_width, min_height, max_file_kb, allowed_formats, allow_video, session_impression_cap, rotation_cap, is_active)
  VALUES ({esc(sid)}, {esc(code)}, N'app', {esc(screen)}, {order_no}, {esc(ratio)}, {minw}, {minh}, 500, N'JPEG／PNG／WebP', 0, 3, 3, 1);
  INSERT INTO ad_slots_i18n (ad_slot_id, locale, name, fallback_alt)
  VALUES ({esc(sid)}, N'zh-Hant', {esc("【測試】" + zh)}, N'【測試】自家內容'), ({esc(sid)}, N'en', {esc("[Test] " + en)}, N'[Test] House content');
END
""")
    for key, zh, en, status in [("a", "【測試】廣告主甲", "[Test] Advertiser A", "active"), ("b", "【測試】廣告主乙", "[Test] Advertiser B", "negotiating")]:
        aid = new_id("advertiser", key)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM advertisers WHERE id = {esc(aid)})
BEGIN
  INSERT INTO advertisers (id, tax_id, contact_name, contact_phone, contact_email, contract_note, cooperation_start_on, status)
  VALUES ({esc(aid)}, N'00000000', N'【測試】聯絡人', N'0900-000-000', N'advertiser-{key}@example.com', N'【測試】合約備註', '2026-09-01', {esc(status)});
  INSERT INTO advertisers_i18n (advertiser_id, locale, name) VALUES ({esc(aid)}, N'zh-Hant', {esc(zh)}), ({esc(aid)}, N'en', {esc(en)});
END
""")
    camp = new_id("ad_campaign", "seed-ended")
    crea = new_id("ad_creative", "seed-ended")
    import datetime as _dt
    stat_rows = []
    total_imp = 0  # delivered_total 必須等於日聚合的曝光加總（B-14：曾寫死 3900，與成效報表 4,674 對不上）
    for d in range(14):
        day = _dt.date.today() - _dt.timedelta(days=15 - d)
        for platform, share in (("ios", 6), ("android", 4)):
            imp = 100 + (d * 7) % 40 + share * 10
            stat_rows.append(f"('{day.isoformat()}', {esc(camp)}, {esc(crea)}, (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'{platform}', N'zh-Hant', {imp}, {imp // 25}, {imp * 8 // 10})")
            total_imp += imp
    block(f"""
IF NOT EXISTS (SELECT 1 FROM ad_campaigns WHERE id = {esc(camp)})
BEGIN
  INSERT INTO ad_campaigns (id, advertiser_id, slot_id, name, starts_at, ends_at, weight, goal_type, goal_impressions, delivered_total, contract_amount, is_amount_hidden, status)
  VALUES ({esc(camp)}, {esc(new_id("advertiser", "a"))}, (SELECT id FROM ad_slots WHERE slot_code = N'home_top'), N'【測試】已結束的曝光保證檔期',
          DATEADD(day, -16, SYSUTCDATETIME()), DATEADD(day, -2, SYSUTCDATETIME()), 3, N'guaranteed', 5000, {total_imp}, 30000, 1, N'ended');
  INSERT INTO ad_creatives (id, campaign_id, locale, alt_text, title, cta_text, click_url, theme, review_status)
  VALUES ({esc(crea)}, {esc(camp)}, N'zh-Hant', N'【測試】素材替代文字', N'【測試】廣告標題', N'了解更多', N'https://example.com/ad', N'both', N'approved');
  INSERT INTO ad_daily_stats (stat_date, campaign_id, creative_id, slot_id, platform, locale, impressions, clicks, unique_devices) VALUES
    {(","+chr(10)+"    ").join(stat_rows)};
END
""")
    # 修復已灌過舊種子（delivered_total 寫死 3900）的資料庫：只動這筆種子檔期，且只在兩邊不一致時更新。
    block(f"""
UPDATE ad_campaigns SET delivered_total = {total_imp}
WHERE id = {esc(camp)} AND delivered_total <> {total_imp}
  AND (SELECT COALESCE(SUM(impressions), 0) FROM ad_daily_stats WHERE campaign_id = {esc(camp)}) = {total_imp};
""")
    draft = new_id("ad_campaign", "seed-draft")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM ad_campaigns WHERE id = {esc(draft)})
  INSERT INTO ad_campaigns (id, advertiser_id, slot_id, name, starts_at, ends_at, weight, goal_type, status)
  VALUES ({esc(draft)}, {esc(new_id("advertiser", "b"))}, (SELECT id FROM ad_slots WHERE slot_code = N'home_mid'), N'【測試】草稿檔期',
          DATEADD(day, 7, SYSUTCDATETIME()), DATEADD(day, 21, SYSUTCDATETIME()), 1, N'traffic', N'draft');
""")

    emit("-- ── 59. app_devices／push_messages／app_diagnostic_reports：M3、M4、M5（5 台示範裝置不含權杖、1 則已發送＋1 則草稿推播、3 筆診斷；【測試】） ──")
    for i, (platform, ver, perm, tstat, locale) in enumerate([
        ("ios", "0.9.0", "granted", "none", "zh-Hant"), ("ios", "0.9.0", "denied", "none", "en"), ("android", "0.9.0", "granted", "invalid", "zh-Hant"),
        ("android", "0.8.0", "not_determined", "none", "zh-Hant"), ("android", "0.9.0", "granted", "none", "en"),
    ]):
        did = f"test-device-seed-{i:04d}"
        block(f"""
IF NOT EXISTS (SELECT 1 FROM app_devices WHERE device_install_id = {esc(did)})
  INSERT INTO app_devices (id, device_install_id, platform, os_version, app_version, locale, push_token_status, push_permission, first_seen_at, last_active_at)
  VALUES ({esc(new_id("app_device", did))}, {esc(did)}, {esc(platform)}, N'測試', {esc(ver)}, {esc(locale)}, {esc(tstat)}, {esc(perm)}, DATEADD(day, -{20 - i}, SYSUTCDATETIME()), DATEADD(day, -{i}, SYSUTCDATETIME()));
""")
    pm1 = new_id("push_message", "seed-sent")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM push_messages WHERE id = {esc(pm1)})
BEGIN
  INSERT INTO push_messages (id, kind, audience_tier, status, reviewed_at, sent_at, audience_estimate, sent_count, delivered_count, failed_count, opened_count)
  VALUES ({esc(pm1)}, N'announcement', N'all', N'sent', DATEADD(day, -3, SYSUTCDATETIME()), DATEADD(day, -3, SYSUTCDATETIME()), 120, 120, 118, 2, 37);
  INSERT INTO push_messages_i18n (push_message_id, locale, title, body)
  VALUES ({esc(pm1)}, N'zh-Hant', N'【測試】主場開賽提醒', N'【測試】這是示範用的已發送推播，數字為虛構。'), ({esc(pm1)}, N'en', N'[Test] Match day', N'[Test] Demo message.');
  INSERT INTO push_message_stats (push_message_id, platform, locale, sent, delivered, opened) VALUES
    ({esc(pm1)}, N'ios', N'zh-Hant', 60, 59, 20), ({esc(pm1)}, N'android', N'zh-Hant', 40, 39, 12), ({esc(pm1)}, N'android', N'en', 20, 20, 5);
END
""")
    pm2 = new_id("push_message", "seed-draft")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM push_messages WHERE id = {esc(pm2)})
BEGIN
  INSERT INTO push_messages (id, kind, audience_tier, status) VALUES ({esc(pm2)}, N'announcement', N'fan_club', N'draft');
  INSERT INTO push_messages_i18n (push_message_id, locale, title, body)
  VALUES ({esc(pm2)}, N'zh-Hant', N'【測試】球迷會員專屬公告（草稿）', N'【測試】草稿內容。'), ({esc(pm2)}, N'en', NULL, NULL);
END
""")
    for i, (platform, ver, rtype, metric, summary, status) in enumerate([
        ("ios", "0.9.0", "startup_time", 1800, "【測試】冷啟動耗時", "new"),
        ("android", "0.9.0", "crash", None, "【測試】示範崩潰摘要", "new"),
        ("android", "0.8.0", "api_error", 3, "【測試】API 逾時 3 次", "resolved"),
    ]):
        block(f"""
IF NOT EXISTS (SELECT 1 FROM app_diagnostic_reports WHERE id = {esc(new_id("app_diag", str(i)))})
  INSERT INTO app_diagnostic_reports (id, device_install_id, platform, app_version, build_number, os_version, occurred_at, report_type, metric_value, summary, detail, status)
  VALUES ({esc(new_id("app_diag", str(i)))}, {esc(f"test-device-seed-{i:04d}")}, {esc(platform)}, {esc(ver)}, N'900', N'測試', DATEADD(day, -{i + 1}, SYSUTCDATETIME()), {esc(rtype)},
          {metric if metric is not None else 'NULL'}, {esc(summary)}, N'【測試】示範技術細節（不含個資）。', {esc(status)});
""")

    # ========================================================================
    # 60. 本機驗收專用測資（2026-10-05）：補 S2-11 球衣登記與 S3-2 漫畫閱讀器「缺資料未驗」的最小假資料。
    # 🔴 只給本機開發庫：不進正式庫（generate-prod-content-sql.py 分類為 PERSONAL），全部虛構、不含任何真實個資。
    #   - 會員 M900101（單人方案，球衣 0／1 件）、M900102（家庭方案，球衣 0／3 件）：均為 tcrfc 2026-27 有效付費會籍、
    #     Email 已驗證，登入密碼與其他種子會員相同（ContentEditor@123）。既有 M900001／M900002 的球衣額度已用滿或剩 1 件，
    #     不足以走完「登記→達上限被擋→修改」整條路，故另建兩位。
    #   - 漫畫第 101、102 集：已發布、各 3 頁（800×1200）。集號刻意取 101／102（遠低於測試用的 9000 段），
    #     既有的 3 集草稿與測試互不干擾。圖片鍵在 seed-dev/comic/ 底下，**物件本身不在資料庫**：
    #     先 `apps/api/scripts/dev-azurite.sh up`，再 `python3 db/seed/seed-dev-blobs.py` 把占位圖傳進 Azurite
    #     （沒傳也能跑，只是閱讀器圖片載入失敗）。
    # ========================================================================
    emit("-- ── 60. 本機驗收測資：球衣登記用會員 M900101／M900102、漫畫閱讀器用第 101／102 集（僅本機開發庫，不進正式庫） ──")
    tc = clubs["tcrfc"]
    JERSEY_DEV_MEMBERS = [  # member no, name, email, phone, plan code, card holder note
        ("M900101", "【測試】球衣登記單人", "jersey-single@example.com", "0900-000-101", "single", 1200),
        ("M900102", "【測試】球衣登記家庭", "jersey-family@example.com", "0900-000-102", "family", 3000),
    ]
    for no, name, email, phone, plan, fee in JERSEY_DEV_MEMBERS:
        mid = new_id("member", no)
        msid = new_id("membership", no, "tcrfc", "2026-27")
        plan_sql = f"(SELECT id FROM membership_plans WHERE club_id = {tc} AND season_id = {season_sq(tc, '2026-27')} AND code = {esc(plan)})"
        block(f"""
IF NOT EXISTS (SELECT 1 FROM members WHERE member_no = {esc(no)})
BEGIN
  INSERT INTO members (id, member_no, name, email, password_hash, phone, birth_on, signup_source, status, email_verified_at, last_login_at, internal_note, locale)
  VALUES ({esc(mid)}, {esc(no)}, {esc(name)}, {esc(email)}, {esc(TEST_HASH)}, {esc(phone)}, N'1990-01-01', N'web', N'active',
          SYSUTCDATETIME(), NULL, N'【測試】本機驗收用：球衣登記（見 backoffice_seed.py 區段 60）。', N'zh-Hant');
  INSERT INTO memberships (id, member_id, club_id, season_id, tier, membership_start_on, membership_end_on, status, membership_plan_id)
  VALUES ({esc(msid)}, {esc(mid)}, {tc}, {season_sq(tc, '2026-27')}, N'fan_club', N'2026-09-20', N'2027-05-02', N'active', {plan_sql});
  INSERT INTO member_cards (id, membership_id, club_id, holder_name, token, status, issued_at)
  VALUES ({esc(new_id("member_card", msid))}, {esc(msid)}, {tc}, {esc(name)},
          {esc(hashlib.sha256(("seed-card-token|" + msid).encode()).hexdigest()[:43])}, N'active', SYSUTCDATETIME());
  INSERT INTO membership_payments (id, membership_id, club_id, collecting_club_id, membership_plan_id, method, amount, paid_on, note, activated_start_on, activated_end_on)
  VALUES ({esc(new_id("membership_payment", msid))}, {esc(msid)}, {tc}, {tc}, {plan_sql}, N'onsite', {fee}, N'2026-09-20', N'【測試】種子付款紀錄', N'2026-09-20', N'2027-05-02');
END
""")

    for no, published_on, is_latest in ((101, "2026-09-01", 0), (102, "2026-09-15", 1)):
        eid = new_id("comic_episode", "tcrfc", str(no))
        pages_sql = "\n".join(
            f"  INSERT INTO comic_pages (id, comic_episode_id, image_key, image_width, image_height, sort_order) "
            f"VALUES ({esc(new_id('comic_page', 'tcrfc', str(no), str(p)))}, {esc(eid)}, N'seed-dev/comic/{no}/{p}.webp', 800, 1200, {p});"
            for p in (1, 2, 3))
        block(f"""
IF NOT EXISTS (SELECT 1 FROM comic_episodes WHERE club_id = {tc} AND episode_no = {no})
BEGIN
  INSERT INTO comic_episodes (id, club_id, episode_no, cover_key, published_on, status, is_latest, view_count)
  VALUES ({esc(eid)}, {tc}, {no}, N'seed-dev/comic/{no}/cover.webp', {esc(published_on)}, N'published', {is_latest}, 0);
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES ({esc(eid)}, N'zh-Hant', {esc(f"【測試】驗收用第 {no} 集")});
  INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES ({esc(eid)}, N'en', {esc(f"[Test] Review episode {no}")});
{pages_sql}
END
""")
