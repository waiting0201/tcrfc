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
