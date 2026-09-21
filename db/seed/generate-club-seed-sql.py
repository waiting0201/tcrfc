#!/usr/bin/env python3
# db/seed/generate-club-seed-sql.py — 讀 site/src/data/*.json，產生主站庫（tcrfc_club_dev）的種子 T-SQL
#
# 為什麼用「讀 JSON 產生 SQL」而不是把資料寫死在腳本裡：
#   site/src/data/*.json 含球員姓名、教練姓名等個資性質欄位。這些 JSON 檔本身已經納入版控
#   （site/src/data/ 未被 .gitignore 排除、已 git add），是既有 mockup 的一部分，公開揭露的
#   風險並非本次新增。但本檔（generate-club-seed-sql.py）**只放讀取與轉換邏輯，不重複硬編碼
#   任何球員／教練姓名**——這樣即使未來 JSON 內容變動（例如球員異動、移除某人），
#   本腳本不需要跟著改，且 CLAUDE.md 第 7 條的精神（個資不重複散落在多個檔案）可以維持。
#
# 產出的 .sql 檔（db/seed/.generated/）本身含真實姓名，**不進版控**（.gitignore 已排除），
# 只在本機執行期間存在，用完即可重新產生。
#
# 冪等策略：每個實體用一個「業務自然鍵」判斷是否已存在（IF NOT EXISTS 才 INSERT），
# 可重複執行不會炸主鍵、不會長出重複資料。
#
# ⚠️ T-SQL 變數是「批次（batch）」作用域，sqlcmd 遇到獨立一行的 GO 就會切一個新批次、
# 清空所有區域變數。本檔案的產生策略因此是：
#   1. 每一筆記錄的 INSERT 區塊都用 GO 隔開，彼此獨立、互不依賴前一批次留下的變數；
#   2. 需要引用「父列」（俱樂部、球隊、球季、賽事系列、新聞分類）的 id 時，一律用
#      純量子查詢（SELECT id FROM ... WHERE 業務鍵 = ...）內嵌在 INSERT 裡，不依賴變數傳遞。
#   這樣才能安全地對兩千行等級的種子腳本逐段執行，除錯時也能單獨重跑某一段。
#
# 用法：
#   python3 db/seed/generate-club-seed-sql.py > db/seed/.generated/club-seed.local.sql
#   （或直接執行 db/seed/apply-seed.sh，會自動呼叫本腳本）

import json
import re
import sys
import uuid
from datetime import date, datetime
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DATA_DIR = REPO_ROOT / "site" / "src" / "data"

# 固定命名空間，讓同一筆業務資料每次產生的 GUID 都一樣（純粹方便本機除錯時比對，
# 資料庫端仍然是用 IF NOT EXISTS 判斷是否要插入，不依賴這個 GUID 是否「看起來穩定」）。
NS = uuid.UUID("6f6a9c9e-3b1a-4e7a-9c2e-2f6a1f2b7a10")


def new_id(*parts: str) -> str:
    """由業務鍵決定性地衍生一個 GUID 字面量（UUIDv5），方便重跑時人工比對，不影響冪等邏輯本身。"""
    return str(uuid.uuid5(NS, "|".join(parts)))


def esc(value) -> str:
    """T-SQL 字面量：None → NULL；字串一律 N'...'，單引號用兩個單引號跳脫。"""
    if value is None:
        return "NULL"
    if isinstance(value, bool):
        return "1" if value else "0"
    if isinstance(value, (int, float)):
        return str(value)
    if isinstance(value, (date, datetime)):
        return f"'{value.isoformat()}'"
    s = str(value).replace("'", "''")
    return f"N'{s}'"


def load(name: str):
    path = DATA_DIR / name
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


out = []


def emit(text: str = ""):
    out.append(text)


def block(sql: str):
    """一個獨立批次：內容 + GO。

    若內容符合「IF @id IS NULL BEGIN ... END」的防呆插入樣式，自動包一層
    BEGIN TRANSACTION／COMMIT TRANSACTION——配合檔頭的 SET XACT_ABORT ON，
    任何一句 INSERT 出錯都會整組回滾，不會留下「父列已插入、子列沒插入」的半殘資料。

    這是吃過虧才加的（見 docs/18-work-errors.md）：locales 主檔一開始沒種，
    clubs_i18n 因為 FK 失敗而整批中止，但同批次裡先執行的 INSERT INTO clubs
    已經自動 commit（沒有交易包住），下次重跑時「IF @id IS NULL」又因為 clubs
    那筆已經存在而整段跳過，永遠補不回缺的 clubs_i18n——冪等判斷本身沒錯，
    錯在「判斷用的父列」跟「這個區塊真正該保證存在的東西」不是同一組。
    """
    text = sql.strip()
    if "\nBEGIN\n" in text and text.endswith("END"):
        text = re.sub(r"\nBEGIN\n", "\nBEGIN\n  BEGIN TRANSACTION;\n", text, count=1)
        text = re.sub(r"\nEND$", "\n  COMMIT TRANSACTION;\nEND", text, count=1)
    emit(text)
    emit("GO")
    emit()


# ── 父列的純量子查詢（不依賴變數，任何批次都能安全引用） ──────────────────
CLUB_TCRFC = "(SELECT id FROM clubs WHERE code = N'tcrfc')"
CLUB_BW = "(SELECT id FROM clubs WHERE code = N'bw')"
TEAM_D1 = "(SELECT id FROM teams WHERE code = N'D1')"


def category_sq(code: str) -> str:
    return f"(SELECT id FROM article_categories WHERE code = N'{code}')"


emit("-- ============================================================================")
emit("-- db/seed/.generated/club-seed.local.sql — 自動產生，請勿手動編輯")
emit("-- 產生自：db/seed/generate-club-seed-sql.py（來源：site/src/data/*.json）")
emit("-- 冪等：可重複執行，每個實體用業務自然鍵判斷是否已存在。")
emit("-- 目標資料庫：tcrfc_club_dev（本機既有 sqlserver 容器內，2026-09-21 起與 mssql-dev 合併），")
emit("-- 不得對到 tcrfc_charity_dev，也不得對到同一個 instance 裡其他專案的資料庫。")
emit("-- ============================================================================")
emit()
emit("-- SET 選項是連線層級、跨 GO 批次仍然有效（不像變數會被 GO 清空）。")
emit("-- 搭配每個防呆插入區塊自動包的 BEGIN TRANSACTION／COMMIT TRANSACTION，")
emit("-- 任何一句 INSERT 失敗就整組回滾，不會留下「父列插入成功、子列插入失敗」的半殘資料。")
emit("SET XACT_ABORT ON;")
emit("GO")
emit()

# ============================================================================
# 0. 語系主檔（locales）：clubs_i18n／competitions_i18n 等側表的 locale 欄位有 FK 指向這裡
#    （db/club-schema.sql FK_*_i18n_locale 系列），DDL 本身不含種子資料，是本腳本的職責。
#    只放目前實際使用的兩個語系；docs/14「架構須預留第三語系」是欄位設計（nvarchar(10) 可
#    放任何 BCP-47 代碼），不是要求現在就插入第三筆語系資料。
# ============================================================================
emit("-- ── 0. locales：zh-Hant（預設）＋ en，供所有 *_i18n 側表的 FK 參照 ──────────")
block("""
IF NOT EXISTS (SELECT 1 FROM locales WHERE code = N'zh-Hant')
  INSERT INTO locales (code, name, is_default, sort_order) VALUES (N'zh-Hant', N'繁體中文', 1, 0);
IF NOT EXISTS (SELECT 1 FROM locales WHERE code = N'en')
  INSERT INTO locales (code, name, is_default, sort_order) VALUES (N'en', N'English', 0, 1);
""")

# ============================================================================
# 1. 俱樂部（Club）：台中磐石（tcrfc）＋ 台中藍鯨（bw，只建主檔）
# ============================================================================
emit("-- ── 1. clubs：兩俱樂部的主檔 ─────────────────────────────────────────")

tcrfc_club_id = new_id("club", "tcrfc")
bw_club_id = new_id("club", "bw")

# 台中磐石：domain 用 .env 目前的實際上線前網域（stg.tcrfc.tw），不是憑空捏造。
block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM clubs WHERE code = N'tcrfc';
IF @id IS NULL
BEGIN
  SET @id = {esc(tcrfc_club_id)};
  INSERT INTO clubs (id, code, domain, brand_color, brand_secondary_color, is_collecting_subject, default_locale, sort_order, status)
  VALUES (@id, N'tcrfc', N'stg.tcrfc.tw', N'#E0218A', N'#231916', 1, N'zh-Hant', 0, N'active');
  INSERT INTO clubs_i18n (club_id, locale, name) VALUES (@id, N'zh-Hant', N'台中磐石');
  INSERT INTO clubs_i18n (club_id, locale, name) VALUES (@id, N'en', N'Taichung Rock FC');
END
""")

# 台中藍鯨：只建 Club 主檔（任務指示，客戶提供的名單／賽程尚未到位，不建 Team／Player）。
# domain 待 B-4 解除（docs/13 §1、STATUS.md B-4）——藍鯨網域尚未取得，NOT NULL UNIQUE 欄位
# 用明確標示「非真實」的技術佔位值（.invalid 是 IANA 保留給這類用途的 TLD），
# 正式網域確定後由後台人工更新，不得沿用此值上線。
# 英文正式全名待確認（docs/14 §「名稱寫法」），故 clubs_i18n 只插 zh-Hant，不插 en。
block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM clubs WHERE code = N'bw';
IF @id IS NULL
BEGIN
  SET @id = {esc(bw_club_id)};
  INSERT INTO clubs (id, code, domain, brand_color, brand_secondary_color, is_collecting_subject, default_locale, sort_order, status)
  VALUES (@id, N'bw', N'bw-domain-pending.invalid', N'#2196D5', N'#040000', 0, N'zh-Hant', 1, N'active');
  INSERT INTO clubs_i18n (club_id, locale, name) VALUES (@id, N'zh-Hant', N'台中藍鯨');
END
""")

# ============================================================================
# 2. 球隊（Team）：只建台中磐石一線隊 D1（players.json 是一線隊名單，無藍鯨／學院資料來源）
# ============================================================================
emit("-- ── 2. teams：台中磐石一線隊 D1（code 全站唯一） ───────────────────────")
d1_team_id = new_id("team", "D1")
block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM teams WHERE code = N'D1';
IF @id IS NULL
BEGIN
  SET @id = {esc(d1_team_id)};
  INSERT INTO teams (id, club_id, code, type, gender, sort_order)
  VALUES (@id, {CLUB_TCRFC}, N'D1', N'first_team', N'men', 0);
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'zh-Hant', N'一線隊');
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'en', N'First Team');
END
""")

# ============================================================================
# 3. 球季（Season）與賽事系列（Competition）：由 schedule.json 的實際日期範圍推導
# ============================================================================
schedule = load("schedule.json")
match_dates = sorted(m["date"] for m in schedule)
season_start = match_dates[0]
season_end = match_dates[-1]
season_code = "2026-27"  # 對照 content/schedule/2026-27_企甲賽程.csv 檔名，非憑空命名

emit("-- ── 3. seasons：2026-27 球季（起訖日＝schedule.json 實際最早／最晚比賽日期，")
emit("--    規劃書未給官方球季框架日期，用真實賽程日期推導以滿足 NOT NULL，非官方球季起訖） ──")
season_id = new_id("season", "tcrfc", season_code)
block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM seasons WHERE club_id = {CLUB_TCRFC} AND code = {esc(season_code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(season_id)};
  INSERT INTO seasons (id, club_id, code, start_on, end_on)
  VALUES (@id, {CLUB_TCRFC}, {esc(season_code)}, {esc(season_start)}, {esc(season_end)});
END
""")

SEASON = f"(SELECT id FROM seasons WHERE club_id = {CLUB_TCRFC} AND code = N'{season_code}')"

emit("-- ── 4. competitions：企業甲級足球聯賽（規劃書行 1463 提到的具名賽事系列範例） ──")
comp_code = "enterprise-a"
comp_id = new_id("competition", "tcrfc", season_code, comp_code)
block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM competitions WHERE club_id = {CLUB_TCRFC} AND code = {esc(comp_code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(comp_id)};
  INSERT INTO competitions (id, club_id, season_id, code, comp_type, status)
  VALUES (@id, {CLUB_TCRFC}, {SEASON}, {esc(comp_code)}, N'league', N'published');
  -- 只插 zh-Hant：英文正式賽事名稱規劃書與 docs 均未給，不臆造翻譯（docs/12c 側表原則：無來源不硬填）。
  INSERT INTO competitions_i18n (competition_id, locale, name) VALUES (@id, N'zh-Hant', N'企業甲級足球聯賽');
END
""")

COMPETITION = f"(SELECT id FROM competitions WHERE club_id = {CLUB_TCRFC} AND code = N'{comp_code}')"

# ============================================================================
# 5. 新聞分類（ArticleCategory）：規劃書 §7.1–7.8 全數八個分類（與俱樂部無關，共用主檔）
#    code 採用 mockup 既有的 URL slug（site/src/pages/*/news/<slug>/），非本腳本發明。
# ============================================================================
CATEGORIES = [
    ("club", "俱樂部新聞", "Club News"),
    ("match", "比賽報導", "Match Reports"),
    ("academy", "學院新聞", "Academy News"),
    ("player-stories", "球員故事", "Player Stories"),
    ("international", "國際動態", "International"),
    ("camps-events", "營隊與活動", "Camps & Events"),
    ("community", "社區活動", "Community"),
    ("media", "媒體專區", "Media"),
]

emit("-- ── 5. article_categories：規劃書 7.1–7.8 八個分類（不帶 club_id，全站共用主檔） ──")
for i, (code, name_zh, name_en) in enumerate(CATEGORIES):
    cat_id = new_id("article_category", code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM article_categories WHERE code = {esc(code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(cat_id)};
  INSERT INTO article_categories (id, code, sort_order) VALUES (@id, {esc(code)}, {i});
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'zh-Hant', {esc(name_zh)});
  INSERT INTO article_categories_i18n (article_category_id, locale, name) VALUES (@id, N'en', {esc(name_en)});
END
""")

# news.json 的 category 值 → article_categories.code 的對照。
# ⚠️ 'intcup'（台中磐石國際足球盃）沒有任何一個 7.1–7.8 的分類代碼字面對得上，
#    逐篇標題確認過內容皆為賽事報導（見本次回報），歸類到 match（7.2）。
#    這是本次 seed 的人工判斷，不是規劃書明文規則，正式資料應由後台人工複核分類。
NEWS_CATEGORY_MAP = {
    "club": "club",
    "match": "match",
    "intcup": "match",  # 人工判斷，見上方註解
    "international": "international",
    "community": "community",
    "camps": "camps-events",
}

# ============================================================================
# 6. 球員（Player）：players.json → players／players_i18n，team_id 固定 D1
#    （players.json 是一線隊名單，無法區分藍鯨／學院，不臆測）
# ============================================================================
players = load("players.json")
emit(f"-- ── 6. players：players.json 共 {len(players)} 筆，team_id 一律 D1 ─────────────────────")
for p in players:
    shirt_no = p["number"]
    player_id = new_id("player", "D1", str(shirt_no))
    name_en = (p.get("name_en") or "").strip()
    en_insert = (
        f"INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', {esc(name_en)});"
        if name_en
        else "-- 無英文姓名（players.json name_en 為空字串），不插入 en 列"
    )
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl WHERE pl.team_id = {TEAM_D1} AND pl.shirt_no = {esc(shirt_no)};
IF @id IS NULL
BEGIN
  SET @id = {esc(player_id)};
  INSERT INTO players (id, club_id, team_id, shirt_no, position)
  VALUES (@id, {CLUB_TCRFC}, {TEAM_D1}, {esc(shirt_no)}, {esc(p.get("position"))});
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', {esc(p["name_zh"])});
  {en_insert}
END
""")

# ============================================================================
# 7. 教練與團隊成員（Staff）：coaches-d1.json（可對應 D1）＋ coaches-academy.json（無法對應
#    到 U15／U14／U12 哪一隊，不臆測）＋ staff.json 裡兩者皆未涵蓋的「顧問」角色。
# ============================================================================
coaches_d1 = load("coaches-d1.json")
coaches_academy = load("coaches-academy.json")
staff_all = load("staff.json")


def staff_key(item):
    return (item["name_zh"], item["role_zh"])


d1_keys = {staff_key(x) for x in coaches_d1}

emit(f"-- ── 7. staff：staff.json 共 {len(staff_all)} 筆 ──────────────────────────────")
emit("-- coaches-d1.json 的成員連到 D1（staff_teams）；coaches-academy.json 的成員")
emit("-- （青訓教練／青訓總監）**不連結任何 Team**——來源 JSON 沒有標明是 U15／U14／U12")
emit("-- 哪一隊，本俱樂部學院球隊（U15／U14／U12）本次也未建立（無球員名單佐證需要建隊）。")
emit("-- 這是刻意的欄位留白，不是遺漏，見本次回報「落差清單」。")
emit()

for s in staff_all:
    key = staff_key(s)
    staff_id = new_id("staff", s["name_zh"], s["role_zh"])
    name_en = (s.get("name_en") or "").strip()
    en_insert = (
        f"INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', {esc(name_en)}, NULL);"
        if name_en
        else "-- 無英文姓名，不插入 en 列"
    )
    team_insert = (
        f"INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, {TEAM_D1});"
        if key in d1_keys
        else "-- 不連結 Team（見上方說明）"
    )
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  WHERE si.locale = N'zh-Hant' AND si.name = {esc(s["name_zh"])} AND si.title = {esc(s["role_zh"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(staff_id)};
  INSERT INTO staff (id, club_id) VALUES (@id, {CLUB_TCRFC});
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'zh-Hant', {esc(s["name_zh"])}, {esc(s["role_zh"])});
  {en_insert}
  {team_insert}
END
""")

# ============================================================================
# 8. 賽事（Match）：schedule.json → matches／matches_i18n／match_teams
# ============================================================================
emit(f"-- ── 8. matches：schedule.json 共 {len(schedule)} 筆，全數為 D1／企業甲級聯賽 ────────")
emit("-- ⚠️ schedule.json 沒有比分（本來就是賽程表不是賽果表），status 一律 N'scheduled'。")
emit("-- match_no（聯賽官方場次編號）v3.11 才補進 matches 表；下面每筆除了原本的")
emit("-- 「找不到才 INSERT」區塊，另外接一句獨立的 UPDATE 用業務自然鍵補 match_no——")
emit("-- 這是為了讓「DDL 追加欄位、既有本機庫在欄位補進之前已經種過資料」這種情況可以單靠")
emit("-- 重跑本腳本補齊，不必整庫重建（見 db/seed/README.md「DDL 改了但本機庫沒跟上」）。")
emit("-- UPDATE 故意不放進上面 IF @id IS NULL 的 BEGIN/END 區塊——那個區塊只在列不存在時")
emit("-- 執行，既有列永遠補不到；獨立成一句可重複執行的 UPDATE 才能兩種情況都涵蓋。")
emit()

for m in schedule:
    round_no = int(m["round"])
    match_on = m["date"]
    match_no = int(m["match_no"])
    match_id = new_id("match", "tcrfc", season_code, str(round_no), match_on, m["opponent_zh"])
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = mt.id
  FROM matches mt
  WHERE mt.club_id = {CLUB_TCRFC} AND mt.season_id = {SEASON}
    AND mt.round_no = {esc(round_no)} AND mt.match_on = {esc(match_on)} AND mt.opponent = {esc(m["opponent_zh"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(match_id)};
  INSERT INTO matches (id, club_id, season_id, competition_id, match_on, kickoff, home_away, opponent, competition, status, round_no, match_no)
  VALUES (@id, {CLUB_TCRFC}, {SEASON}, {COMPETITION}, {esc(match_on)}, {esc(m["kickoff"])}, {esc(m["home_away"])}, {esc(m["opponent_zh"])}, N'league', N'scheduled', {esc(round_no)}, {esc(match_no)});
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', {esc(m["venue_zh"])});
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, {TEAM_D1});
END
""")
    block(f"""
UPDATE matches SET match_no = {esc(match_no)}
WHERE club_id = {CLUB_TCRFC} AND season_id = {SEASON}
  AND round_no = {esc(round_no)} AND match_on = {esc(match_on)} AND opponent = {esc(m["opponent_zh"])};
""")

# ============================================================================
# 9. 新聞（Article）：news.json → articles／articles_i18n
# ============================================================================
news = load("news.json")
emit(f"-- ── 9. articles：news.json 共 {len(news)} 筆。slug 全站唯一，直接沿用 JSON 既有 slug。 ──")
emit("-- cover_key 留 NULL：JSON 的 cover／cover_web 是 mockup 靜態資源路徑，不是走過")
emit("-- 「上傳即縮圖」pipeline 後的 Blob object key（docs/14），兩者不能混用，此為已知落差。")
emit("-- body 留 NULL：news.json 的 body_zh 全部是 null（文稿仍是 .gdoc 捷徑讀不到，docs/07）。")
emit()

skipped_categories = set()
imported = 0
for a in news:
    cat_key = NEWS_CATEGORY_MAP.get(a["category"])
    if cat_key is None:
        skipped_categories.add(a["category"])
        continue
    imported += 1
    slug = a["slug"]
    article_id = new_id("article", slug)
    published_at = f"{a['date']}T00:00:00"
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM articles WHERE slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(article_id)};
  INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
  VALUES (@id, {CLUB_TCRFC}, {esc(slug)}, {category_sq(cat_key)}, N'published', {esc(published_at)});
  INSERT INTO articles_i18n (article_id, locale, title) VALUES (@id, N'zh-Hant', {esc(a["title_zh"])});
END
""")

if skipped_categories:
    print(f"WARNING: 以下 news.json category 沒有對照到任何分類，已略過未匯入：{skipped_categories}", file=sys.stderr)
print(f"INFO: articles 匯入 {imported}／{len(news)} 筆", file=sys.stderr)

print("\n".join(out))
