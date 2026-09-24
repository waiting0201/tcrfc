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
#
# 🔴 --reset-admin-accounts（2026-09-24 新增，回應前端 agent 端對端驗收後種子帳號狀態
# 漂移的問題）：
#   python3 db/seed/generate-club-seed-sql.py --reset-admin-accounts > db/seed/.generated/reset-admin-accounts.local.sql
#   （或直接執行 db/seed/reset-admin-accounts.sh，會自動呼叫本腳本並套用到 tcrfc_club_dev）
#
#   一般模式的「IF NOT EXISTS 才 INSERT」對「密碼、2FA 狀態、鎖定計數」這類**本來就會被
#   正常使用改掉**的欄位沒有用——帳號列本來就已經存在，正常種子邏輯永遠不會回頭 UPDATE
#   它。端對端驗收會真的登入、變更密碼、設定或停用 2FA、甚至觸發鎖定計數，這些狀態一旦偏離
#   種子腳本原本設定的值，後續測試/驗收拿種子帳號登入的假設就會不成立（本次任務就是這樣發現
#   `sa@system.local` 的密碼與 TOTP 狀態已經不是種子初始值）。
#   `--reset-admin-accounts` 改印出一組只含 UPDATE（不含 INSERT）的陳述式，把下方
#   `ADMIN_USERS` 清單裡每一個帳號的 `password_hash`／`must_change_password`／
#   `is_super_admin`／`two_factor_enabled`／`two_factor_secret_encrypted`／
#   `two_factor_confirmed_at`／`failed_attempt_count`／`locked_until`／`status` 全部改回
#   種子腳本定義的初始值——**刻意重用同一份 `ADMIN_USERS` 清單，不另外複製一份密碼雜湊**
#   （CLAUDE.md 第 7 條精神：同一份事實不分散在兩個檔案）。角色指派與俱樂部授權
#   （`admin_user_roles`／`admin_user_clubs`）不受影響，這兩張表本來就是「新增才會種」，
#   端對端驗收不會讓它們偏離種子值。

import json
import re
import sys
import uuid
from datetime import date, datetime
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DATA_DIR = REPO_ROOT / "site" / "src" / "data"

# 台中藍鯨（bw）的中間格式 JSON——舊站原文（content/blue-whale/*.md）整理而來，見該目錄 README。
# 與 DATA_DIR（tcrfc mockup 資料）分開存放是刻意的：兩者的原始素材本來就不同來源、不同性質，
# 混在同一個資料夾容易誤以為是同一批可互換的 mockup 資料。
BW_DATA_DIR = REPO_ROOT / "content" / "blue-whale" / "data"

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


def load_bw(name: str):
    path = BW_DATA_DIR / name
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
TEAM_BW1 = "(SELECT id FROM teams WHERE code = N'BW1')"


def admin_sq(username: str) -> str:
    return f"(SELECT id FROM admin_users WHERE username = N'{username}')"


def role_sq(code: str) -> str:
    return f"(SELECT id FROM admin_roles WHERE code = N'{code}')"


def perm_sq(code: str) -> str:
    return f"(SELECT id FROM permissions WHERE code = N'{code}')"


def category_sq(code: str) -> str:
    return f"(SELECT id FROM article_categories WHERE code = N'{code}')"


def season_sq(club_sql: str, code: str) -> str:
    return f"(SELECT id FROM seasons WHERE club_id = {club_sql} AND code = N'{code}')"


def competition_sq(club_sql: str, code: str) -> str:
    return f"(SELECT id FROM competitions WHERE club_id = {club_sql} AND code = N'{code}')"


def venue_by_keyword_sq(keyword: str) -> str:
    """依 venues_i18n.name 的關鍵字模糊比對場地 id（僅用於藍鯨賽事匯入時的已知場地連結，
    見 db/seed/README.md「藍鯨場地連結」）。找不到時回傳 NULL 純量子查詢，不會讓外層 INSERT 失敗。"""
    escaped = keyword.replace("'", "''")
    return (
        "(SELECT TOP 1 v.id FROM venues v "
        "JOIN venues_i18n vi ON vi.venue_id = v.id "
        f"WHERE vi.locale = N'zh-Hant' AND vi.name LIKE N'%{escaped}%')"
    )


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
# 1. 俱樂部（Club）：台中磐石（tcrfc）＋ 台中藍鯨（bw）
#    bw 的球隊／球員／賽事等資料來源見 §10 之後（content/blue-whale/data/*.json，
#    整理自舊站 www.tcbw2014.com 原文，2026-09-22 客戶拍板匯入範圍）。
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

# 補 clubs_i18n.description（既有欄位，不是新增欄位）。獨立成一句 UPDATE、不放進上面
# IF @id IS NULL 的區塊——bw 的 clubs 主檔在本次之前就已經種過（沒有 description），
# 那個區塊判斷「clubs 這筆存不存在」，永遠不會因為「description 還缺」而重新執行。
# 這是 match_no 那個先例的同一招（見上面 §8 或 docs/18 E-15）。
bw_club = load_bw("club.json")
emit("-- 補 clubs_i18n.description（既有欄位）：成立日期／口號／隸屬協會等事實文字化存入簡介。")
emit("-- ⚠️ 成立日期、口號、社群連結、隸屬協會目前 db/club-schema.sql 沒有專屬欄位，本次不新增")
emit("-- 欄位（CLAUDE.md 全域規定 2），只把可以放進既有『簡介』欄位的事實寫入，其餘留待回報。")
block(f"""
UPDATE clubs_i18n SET description = {esc(bw_club["description_zh"])}
WHERE club_id = {CLUB_BW} AND locale = N'zh-Hant';
""")

# ============================================================================
# 2. 球隊（Team）：台中磐石一線隊 D1 ＋ 台中藍鯨一線隊 BW1 與其青年隊
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

emit("-- ── 2b. teams：台中藍鯨一線隊 BW1 與其青年隊（不是第二個 D1，docs/13 踩雷點 2） ──")
emit("-- U15／U12 只建隊伍記錄，不建球員——舊站沒有這兩隊的名單（content/blue-whale/squad/")
emit("-- youth-teams.md 已註明抓不到），不得編造。code 加 BW- 前綴避免與磐石未來可能建立的")
emit("-- 同名學院隊（docs/12 §4.2 值域列的 U15／U14／U12 是磐石保留）撞號——Team.code 全站唯一。")
bw_teams = [
    ("BW1", "first_team", "women", None, "一線隊", "First Team"),
    ("BW-U15", "academy", "women", "U15", "U15 青少年女子足球隊", "U15 Girls"),
    ("BW-U12", "academy", "women", "U12", "U12 青少年女子足球隊", "U12 Girls"),
]
for i, (code, ttype, gender, age_band, name_zh, name_en) in enumerate(bw_teams):
    team_id = new_id("team", code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM teams WHERE code = {esc(code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(team_id)};
  INSERT INTO teams (id, club_id, code, type, gender, age_band, sort_order)
  VALUES (@id, {CLUB_BW}, {esc(code)}, {esc(ttype)}, {esc(gender)}, {esc(age_band)}, {i});
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'zh-Hant', {esc(name_zh)});
  INSERT INTO teams_i18n (team_id, locale, name) VALUES (@id, N'en', {esc(name_en)});
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

# ============================================================================
# 10. 台中藍鯨（bw）場地（Venue）：任務範圍只建 venues.md 明確記載、且本次賽事資料
#     會連結到的太原足球場／豐原體育場兩座。Venue 不帶 club_id（兩隊共用地理實體，
#     docs/12 §4.7——場地是地理實體，重複建會產生兩組人工標的座標）。
#     其餘賽事出現過的場地（花蓮美崙國中足球場等）不建 Venue，只存在 matches_i18n.venue
#     這個自由文字欄位裡，這是任務明文的匯入範圍，不是遺漏。
# ============================================================================
bw_venues = load_bw("venues.json")
emit("-- ── 10. venues：藍鯨主場兩座（太原足球場／豐原體育場，不帶 club_id） ─────────")
for v in bw_venues:
    venue_id = new_id("venue", v["name_zh"])
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = vi.venue_id FROM venues_i18n vi WHERE vi.locale = N'zh-Hant' AND vi.name = {esc(v["name_zh"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(venue_id)};
  INSERT INTO venues (id, sort_order) VALUES (@id, 0);
  INSERT INTO venues_i18n (venue_id, locale, name, address, directions)
  VALUES (@id, N'zh-Hant', {esc(v["name_zh"])}, {esc(v["address_zh"])}, {esc(v["directions_zh"])});
END
""")

# ============================================================================
# 11. 台中藍鯨（bw）球季（Season）：2023（木蘭聯賽奪冠季）／2025（總統盃亞軍季）。
#     起訖日＝該季實際比賽日期範圍，非官方球季框架（比照 §3 磐石既有慣例，規劃書未給）。
#     唯一鍵 (club_id, code)，與磐石球季各自獨立，不需要同一套 code 命名。
# ============================================================================
bw_seasons = load_bw("seasons.json")
emit("-- ── 11. seasons：藍鯨 2023／2025 兩個球季（球季不與磐石同步，docs/13 踩雷點 6/14） ──")
for s in bw_seasons:
    bw_season_id = new_id("season", "bw", s["code"])
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM seasons WHERE club_id = {CLUB_BW} AND code = {esc(s["code"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(bw_season_id)};
  INSERT INTO seasons (id, club_id, code, start_on, end_on)
  VALUES (@id, {CLUB_BW}, {esc(s["code"])}, {esc(s["start_on"])}, {esc(s["end_on"])});
END
""")

# ============================================================================
# 12. 台中藍鯨（bw）賽事系列（Competition）：木蘭聯賽（2023）／總統盃（2025）。
#     只插 zh-Hant 名稱——英文賽事名稱規劃書與來源皆未給，不臆造翻譯（同 §4 慣例）。
# ============================================================================
bw_competitions = load_bw("competitions.json")
emit("-- ── 12. competitions：藍鯨木蘭聯賽（2023）／總統盃（2025） ──────────────────")
for c in bw_competitions:
    bw_comp_id = new_id("competition", "bw", c["season_code"], c["code"])
    bw_season_sub = season_sq(CLUB_BW, c["season_code"])
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM competitions WHERE club_id = {CLUB_BW} AND code = {esc(c["code"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(bw_comp_id)};
  INSERT INTO competitions (id, club_id, season_id, code, comp_type, status)
  VALUES (@id, {CLUB_BW}, {bw_season_sub}, {esc(c["code"])}, {esc(c["comp_type"])}, N'published');
  INSERT INTO competitions_i18n (competition_id, locale, name) VALUES (@id, N'zh-Hant', {esc(c["name_zh"])});
END
""")

# ============================================================================
# 13. 台中藍鯨（bw）賽事（Match）：2023 木蘭聯賽 15 場 ＋ 2025 總統盃 6 場，逐場匯入。
#     自然鍵 club_id+season_id+match_on+opponent（兩個競賽分屬不同球季，日期＋對手
#     在各自批次內已足夠唯一，不需要再疊 round_no／match_no 進鍵——round_no／match_no
#     的定義與可得性兩批不同，見各自 JSON 檔頭註解，勉強塞進同一套鍵值語意反而混淆）。
#     venue_id：僅當場地文字含「太原」時連結到 §10 建立的太原足球場，其餘場地維持
#     venue_id = NULL、venue 文字保留在 matches_i18n（任務明文的匯入範圍）。
#     status 一律 N'played'——這是本檔第一次出現非 'scheduled' 的賽事狀態值，因為這批
#     全部是已經打完、有比分的歷史賽事，套用磐石 schedule.json 那套「一律 scheduled」
#     慣例會與事實矛盾；status 欄位本身無 CHECK 約束（db/club-schema.sql 已確認）。
# ============================================================================
emit("-- ── 13. matches：藍鯨 2023 木蘭聯賽 15 場 ＋ 2025 總統盃 6 場 ─────────────────")


def emit_bw_matches(file_name: str, comp_tag: str):
    data = load_bw(file_name)
    bw_season_sub = season_sq(CLUB_BW, data["season_code"])
    bw_comp_sub = competition_sq(CLUB_BW, data["competition_code"])
    for m in data["matches"]:
        match_on = m["match_on"]
        opponent = m["opponent_zh"]
        venue_zh = m["venue_zh"]
        venue_link = venue_by_keyword_sq("太原") if "太原" in venue_zh else "NULL"
        bw_match_id = new_id("match", "bw", data["season_code"], data["competition_code"], match_on, opponent)
        block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = mt.id FROM matches mt
  WHERE mt.club_id = {CLUB_BW} AND mt.season_id = {bw_season_sub}
    AND mt.match_on = {esc(match_on)} AND mt.opponent = {esc(opponent)};
IF @id IS NULL
BEGIN
  SET @id = {esc(bw_match_id)};
  INSERT INTO matches (id, club_id, season_id, competition_id, venue_id, match_on, kickoff, home_away, opponent, competition, status, score_home, score_away, round_no, match_no)
  VALUES (@id, {CLUB_BW}, {bw_season_sub}, {bw_comp_sub}, {venue_link}, {esc(match_on)}, {esc(m.get("kickoff"))}, {esc(m.get("home_away"))}, {esc(opponent)}, {esc(comp_tag)}, N'played', {esc(m.get("score_home"))}, {esc(m.get("score_away"))}, {esc(m.get("round_no"))}, {esc(m.get("match_no"))});
  INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@id, N'zh-Hant', {esc(venue_zh)});
  INSERT INTO match_teams (match_id, team_id) VALUES (@id, {TEAM_BW1});
END
""")
    return len(data["matches"])


bw_mulan_count = emit_bw_matches("matches-2023-mulan.json", "league")
bw_cup_count = emit_bw_matches("matches-2025-presidents-cup.json", "cup")
print(f"INFO: 藍鯨 matches 匯入 {bw_mulan_count} 筆（2023 木蘭）＋ {bw_cup_count} 筆（2025 總統盃）", file=sys.stderr)

# ============================================================================
# 14. 台中藍鯨（bw）球員（Player）：只匯 2024 年度名單（客戶 2026-09-22 拍板，見本次回報）。
#     欄位只有背號＋姓名，其餘一律 NULL，不臆測補值。
#     ⚠️ 背號 26 有兩人（史詠甄／瓦拉邦・汶廷，季中轉會所致，舊站原樣保留）——natural key
#     不能只用 team_id+shirt_no（會誤判成同一人），改用 team_id+shirt_no+姓名。
# ============================================================================
bw_players_2024 = load_bw("players-2024.json")
emit(f"-- ── 14. players：藍鯨 2024 年度名單共 {len(bw_players_2024['players'])} 筆，team_id 一律 BW1 ──")
emit("-- 背號 26 重複（史詠甄／瓦拉邦・汶廷），natural key 加姓名判斷，避免互相覆蓋。")
for p in bw_players_2024["players"]:
    shirt_no = p["number"]
    name_zh = p["name_zh"]
    name_en = (p.get("name_en") or "").strip()
    bw_player_id = new_id("player", "BW1", str(shirt_no), name_zh)
    en_insert = (
        f"INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'en', {esc(name_en)});"
        if name_en
        else "-- 無英文姓名，不插入 en 列"
    )
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = pl.id FROM players pl
  JOIN players_i18n pi ON pi.player_id = pl.id AND pi.locale = N'zh-Hant'
  WHERE pl.team_id = {TEAM_BW1} AND pl.shirt_no = {esc(shirt_no)} AND pi.name = {esc(name_zh)};
IF @id IS NULL
BEGIN
  SET @id = {esc(bw_player_id)};
  INSERT INTO players (id, club_id, team_id, shirt_no)
  VALUES (@id, {CLUB_BW}, {TEAM_BW1}, {esc(shirt_no)});
  INSERT INTO players_i18n (player_id, locale, name) VALUES (@id, N'zh-Hant', {esc(name_zh)});
  {en_insert}
END
""")

# ============================================================================
# 15. 台中藍鯨（bw）教練團（Staff）：5 人全匯（客戶 2026-09-22 拍板），含完整經歷。
#     club_id = bw（不是共用）——這 5 人是藍鯨一線隊自己的教練團，比照磐石既有慣例
#     （tcrfc 自己的教練也是 club_id = tcrfc，不是 NULL；NULL 只留給真正兩隊共用的人員）。
# ============================================================================
bw_coaches = load_bw("coaches.json")
emit(f"-- ── 15. staff：藍鯨教練團共 {len(bw_coaches['coaches'])} 人，全數連結 BW1 ──────────────")
for s in bw_coaches["coaches"]:
    name_zh = s["name_zh"]
    role_zh = s["role_zh"]
    bw_staff_id = new_id("staff", "bw", name_zh, role_zh)
    name_en = (s.get("name_en") or "").strip()
    en_insert = (
        f"INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@id, N'en', {esc(name_en)}, NULL);"
        if name_en
        else "-- 無英文姓名，不插入 en 列"
    )
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = si.staff_id
  FROM staff_i18n si
  JOIN staff st ON st.id = si.staff_id
  WHERE st.club_id = {CLUB_BW} AND si.locale = N'zh-Hant' AND si.name = {esc(name_zh)} AND si.title = {esc(role_zh)};
IF @id IS NULL
BEGIN
  SET @id = {esc(bw_staff_id)};
  INSERT INTO staff (id, club_id) VALUES (@id, {CLUB_BW});
  INSERT INTO staff_i18n (staff_id, locale, name, title, bio) VALUES (@id, N'zh-Hant', {esc(name_zh)}, {esc(role_zh)}, {esc(s.get("bio_zh"))});
  {en_insert}
  INSERT INTO staff_teams (staff_id, team_id) VALUES (@id, {TEAM_BW1});
END
""")

# ============================================================================
# 16. 台中藍鯨（bw）里程碑（Milestone）：沿革 2014–2025，逐年一筆（不是逐條）。
#     happened_on 的月日是技術性佔位（YYYY-01-01）——原文只有年份，schema 的 date 型別
#     NOT NULL 需要完整日期，這個月日不代表任何真實事件發生日，見本次回報。
# ============================================================================
bw_milestones = load_bw("milestones.json")
emit(f"-- ── 16. milestones：藍鯨沿革 {len(bw_milestones['milestones'])} 筆（2014–2025，逐年） ──────")
emit("-- ⚠️ happened_on 只有年份可考，月日固定 01-01 純屬技術性佔位，不代表真實事件發生日期。")
for i, ms in enumerate(bw_milestones["milestones"]):
    happened_on = f"{ms['year']}-01-01"
    title_zh = ms["title_zh"]
    bw_milestone_id = new_id("milestone", "bw", title_zh)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = mi.milestone_id
  FROM milestones_i18n mi
  JOIN milestones m ON m.id = mi.milestone_id
  WHERE m.club_id = {CLUB_BW} AND mi.locale = N'zh-Hant' AND mi.title = {esc(title_zh)};
IF @id IS NULL
BEGIN
  SET @id = {esc(bw_milestone_id)};
  INSERT INTO milestones (id, club_id, happened_on, sort_order) VALUES (@id, {CLUB_BW}, {esc(happened_on)}, {i});
  INSERT INTO milestones_i18n (milestone_id, locale, title, description) VALUES (@id, N'zh-Hant', {esc(title_zh)}, {esc(ms["description_zh"])});
END
""")

# ============================================================================
# 17. 台中藍鯨（bw）指導單位與官方合作夥伴（partners.md）：存入 partners／partners_i18n。
#
#     🔴 綱要落點是本次的判斷（2026-09-22 改正），不是規劃書明文規定，理由逐條寫在
#     content/blue-whale/data/partners.json 的 _comment：
#       ① club-schema.sql 對 partners 的註解就是「合作夥伴（B2B Logo 牆）：Logo 兩版、類型、
#          國家、合作內容與期間、官網、排序、曝光位置」——正是舊站首頁那面牆，且 website_url
#          是結構化欄位。
#       ② sponsors 表要的是 tier（CHECK 限定三種）、合約起訖、聯絡窗口、到期提醒，而
#          gap-analysis.md 明載舊站「無分級、無方案與價目、無合約起訖」，放進去全是 NULL。
#       ③ 前三筆是「指導單位」（教育部體育署、臺中市政府、臺中市政府運動局），那是主管機關
#          不是贊助商。
#     ⚠️ 初版曾存進 sponsors 並把網址塞進 sponsors_i18n.content 的自由文字
#        （「指導單位｜官網：https://…」），等於把分類與網址兩個結構化欄位混進一個沒人查得了
#        的字串。改正後 partner_type 與 website_url 各自有欄位。
#
#     ⛔ show_in_footer／show_on_home 一律 0：舊站確實把這面牆放在首頁，但「新站要不要放、
#        放哪裡」是前台版位決定不是資料事實，由後台自行設定，種子不預設替它決定。
#     ⛔ start_on／end_on 留 NULL：舊站標題是「2024 合作夥伴」，但那是牆的標題不是合約期間，
#        不得拿它當 start_on/end_on（gap-analysis.md：無合約起訖）。
# ============================================================================
bw_partners = load_bw("partners.json")
emit(f"-- ── 17. partners：藍鯨指導單位＋官方合作夥伴共 {len(bw_partners['partners'])} 筆 ──────────")
emit("-- 這批是舊站「2024 合作夥伴」，到 2026 年是否仍有效全部要重新確認（gap-analysis.md 09 單元）。")
for pt in bw_partners["partners"]:
    slug = pt["slug"]
    bw_partner_id = new_id("partner", slug)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM partners WHERE club_id = {CLUB_BW} AND slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(bw_partner_id)};
  INSERT INTO partners (id, club_id, slug, partner_type, website_url, sort_order)
  VALUES (@id, {CLUB_BW}, {esc(slug)}, {esc(pt["partner_type"])}, {esc(pt["website_url"])}, {pt["sort_order"]});
  INSERT INTO partners_i18n (partner_id, locale, name) VALUES (@id, N'zh-Hant', {esc(pt["name_zh"])});
  -- 無 en 列：舊站全站 0 個英文字，夥伴名稱一律沒有官方英文寫法，不臆測轉寫（docs/13 踩雷點 17）。
END
""")

# ============================================================================
# 18. J1–J3 帳號、角色與權限（2026-09-23，backend-engineer，登入與授權地基任務）。
#
#     🔴 角色代碼刻意沿用 db/seed/generate-charity-seed-sql.py 已經在用的九個代碼
#     （system_admin／content_editor／team_competition／academy_program／
#     business_sponsorship／pr_media／customer_service_admin／translator／viewer）——
#     慈善庫種子腳本的註解寫「沿用主站規劃書 §6 的九個角色」，但主站庫在本次之前從未
#     真的種過這九個角色。docs/12b-database-tables.md §7.2 的角色代碼表用的是另一套命名
#     （super_admin／team_manager／academy_manager／business／support），與慈善庫已經
#     上線的代碼不一致。本次選擇跟慈善庫對齊（兩庫角色代碼一致，日後合併報表或人工比對
#     不必再做一次轉換表），**但這代表 docs/12b §7.2 的角色代碼表已經與實際種子資料不同步，
#     需要另外排程更新該文件**（backend-engineer 任務指示明文不得由本次交付自行修改
#     docs/14／18／STATUS.md，docs/12b 的更新留給下一輪同步鏈处理，見任務回報）。
#     第十個角色 partner_club_manager（合作球隊管理，scope_mode=own_clubs）兩份文件寫法一致，
#     沿用不變。
#
#     權限碼本次只鋪三類：J 系統管理本身（J1–J4，sysadmin_only）、B2 新聞（content.article.*）
#     與（S1-4 新增）B1 頁面（content.page.*）。其餘模組的權限碼留給日後對應模組接真實授權時再補，
#     這是刻意的範圍縮減不是遺漏，見 apps/api/README.md「本輪沒做的部分」。
#
#     S1-4（B1 頁面管理）新增 content.page.*，逐字比照 content.article.* 的鋪法（module_code=B、
#     submodule_code=B1、domain=content），角色指派依規劃書 §6 矩陣「內容」欄——那一欄同時涵蓋
#     B1 頁面與 B2 新聞（矩陣沒有分別列 B1／B2 兩欄），故 content.page.* 的 ROLE_PERMISSIONS
#     指派與 content.article.* 逐列一致。
# ============================================================================

ROLES = [
    # (code, name_zh, name_en, scope_mode)
    ("system_admin", "系統管理員", "System Administrator", "all_clubs"),
    ("content_editor", "內容編輯", "Content Editor", "all_clubs"),
    ("team_competition", "競技／球隊管理", "Team & Competition Manager", "all_clubs"),
    ("academy_program", "學院／課程管理", "Academy & Program Manager", "all_clubs"),
    ("business_sponsorship", "商務／贊助", "Business & Sponsorship", "all_clubs"),
    ("pr_media", "公關／媒體", "PR & Media", "all_clubs"),
    ("customer_service_admin", "客服／行政", "Customer Service & Admin", "all_clubs"),
    ("translator", "翻譯人員", "Translator", "all_clubs"),
    ("viewer", "檢視者", "Viewer", "all_clubs"),
    ("partner_club_manager", "合作球隊管理", "Partner Club Manager", "own_clubs"),
]

emit("-- ── 18.1 admin_roles：十個角色（九個沿用慈善庫代碼＋合作球隊管理） ──────────")
for sort_order, (code, name_zh, name_en, scope_mode) in enumerate(ROLES):
    role_id = new_id("admin_role", code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = {esc(code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(role_id)};
  INSERT INTO admin_roles (id, code, name_zh, name_en, scope_mode, is_system, sort_order)
  VALUES (@id, {esc(code)}, {esc(name_zh)}, {esc(name_en)}, {esc(scope_mode)}, 1, {sort_order});
END
""")

# permissions：(code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
PERMISSIONS = [
    ("content.article.view", "B", "B2", "content", "view", 1, 0, 0, "檢視新聞與故事", "View News & Stories"),
    ("content.article.create", "B", "B2", "content", "create", 1, 0, 0, "建立新聞與故事", "Create News & Stories"),
    ("content.article.update", "B", "B2", "content", "update", 1, 0, 0, "編輯新聞與故事", "Edit News & Stories"),
    ("content.article.publish", "B", "B2", "content", "publish", 1, 0, 0, "發布新聞與故事", "Publish News & Stories"),
    ("content.article.delete", "B", "B2", "content", "delete", 1, 0, 0, "刪除新聞與故事", "Delete News & Stories"),
    # S1-4（B1 頁面管理，2026-09-24）：逐字比照 content.article.* 的鋪法。
    ("content.page.view", "B", "B1", "content", "view", 1, 0, 0, "檢視頁面", "View Pages"),
    ("content.page.create", "B", "B1", "content", "create", 1, 0, 0, "建立頁面", "Create Pages"),
    ("content.page.update", "B", "B1", "content", "update", 1, 0, 0, "編輯頁面", "Edit Pages"),
    ("content.page.publish", "B", "B1", "content", "publish", 1, 0, 0, "發布頁面", "Publish Pages"),
    ("content.page.delete", "B", "B1", "content", "delete", 1, 0, 0, "刪除頁面", "Delete Pages"),
    ("system.account.view", "J", "J1", "system", "view", 0, 0, 1, "檢視後台帳號", "View Admin Accounts"),
    ("system.account.create", "J", "J1", "system", "create", 0, 0, 1, "新增後台帳號", "Create Admin Accounts"),
    ("system.account.update", "J", "J1", "system", "update", 0, 0, 1, "停用／更新後台帳號", "Update Admin Accounts"),
    ("system.role.view", "J", "J2", "system", "view", 0, 0, 1, "檢視角色與權限", "View Roles & Permissions"),
    ("system.role.update", "J", "J2", "system", "update", 0, 0, 1, "建立角色與勾選權限", "Update Roles & Permissions"),
    ("system.audit.view", "J", "J3", "system", "view", 0, 0, 1, "檢視操作稽核記錄", "View Audit Logs"),
    ("system.club_grant.view", "J", "J4", "system", "view", 0, 0, 1, "檢視俱樂部授權", "View Club Grants"),
    ("system.club_grant.update", "J", "J4", "system", "update", 0, 0, 1, "指派俱樂部授權", "Update Club Grants"),
    # S1-3 續作（2026-09-24）：J4「俱樂部品牌與法人資料」本身（Club 型別）——與上面兩碼一樣
    # sysadmin_only，規劃書 §6 矩陣「系統」欄只有系統管理員打勾。
    ("system.club.view", "J", "J4", "system", "view", 0, 0, 1, "檢視俱樂部主檔", "View Clubs"),
    ("system.club.update", "J", "J4", "system", "update", 0, 0, 1, "建立／編輯俱樂部主檔", "Update Clubs"),
    # S1-3 續作第二輪（2026-09-24，coordinator 補派）：J4「球隊授權」（admin_user_teams，
    # 規劃書第 1223–1231 行 J4 表格），供「學院管理者不得改動一線隊賽程」這類列級限制使用。
    # 獨立於 system.club_grant.* 之外自成一組（不共用），理由見 apps/api/README.md。
    ("system.team_grant.view", "J", "J4", "system", "view", 0, 0, 1, "檢視球隊授權", "View Team Grants"),
    ("system.team_grant.update", "J", "J4", "system", "update", 0, 0, 1, "指派球隊授權", "Update Team Grants"),
    # Competition（賽事系列）型別：is_club_scoped=1（competitions.club_id 必填），不是
    # sysadmin_only——歸在 module_code=C（球隊管理）／submodule=C4（賽程與賽果），比照矩陣
    # 「球隊／賽事」欄，競技／球隊管理角色 ✔全，其餘角色唯讀或不給，見下方 ROLE_PERMISSIONS。
    ("team.competition.view", "C", "C4", "team", "view", 1, 0, 0, "檢視賽事系列", "View Competitions"),
    ("team.competition.create", "C", "C4", "team", "create", 1, 0, 0, "建立賽事系列", "Create Competitions"),
    ("team.competition.update", "C", "C4", "team", "update", 1, 0, 0, "編輯賽事系列", "Update Competitions"),
    # S1-7 新增：C1 球隊／C2 球員／C3 教練與團隊成員。domain 沿用既有的 "team"（跟
    # team.competition.* 同一個 domain 值，方便權限查詢時整組 domain='team' 一次撈，
    # 見 apps/api/README.md「新增後台端點的必要形狀」清單第 3 點）。三者都是
    # is_club_scoped=1（teams／players／staff 三張表都有 club_id 欄位，staff 雖可為空但欄位
    # 本身存在——is_club_scoped 標示的是「有這個維度」不是「必填」，articles.club_id 同樣
    # 可為空卻仍是 is_club_scoped=1，見既有 content.article.* 那一組），非 sysadmin_only。
    ("team.team.view", "C", "C1", "team", "view", 1, 0, 0, "檢視球隊", "View Teams"),
    ("team.team.create", "C", "C1", "team", "create", 1, 0, 0, "建立球隊", "Create Teams"),
    ("team.team.update", "C", "C1", "team", "update", 1, 0, 0, "編輯球隊", "Update Teams"),
    ("team.player.view", "C", "C2", "team", "view", 1, 0, 0, "檢視球員", "View Players"),
    ("team.player.create", "C", "C2", "team", "create", 1, 0, 0, "建立球員", "Create Players"),
    ("team.player.update", "C", "C2", "team", "update", 1, 0, 0, "編輯球員", "Update Players"),
    ("team.staff.view", "C", "C3", "team", "view", 1, 0, 0, "檢視教練與團隊成員", "View Staff"),
    ("team.staff.create", "C", "C3", "team", "create", 1, 0, 0, "建立教練與團隊成員", "Create Staff"),
    ("team.staff.update", "C", "C3", "team", "update", 1, 0, 0, "編輯教練與團隊成員", "Update Staff"),
    # S1-6 新增：B3 首頁編排——banners／home_sections 皆為 club_id 必填，is_club_scoped=1，
    # 非 sysadmin_only。規劃書 §6 矩陣把 B3 歸在「內容」欄，角色足跡（見下方 ROLE_PERMISSIONS）
    # 沿用 content.article.*／content.page.* 既有的四個角色（system_admin／content_editor／
    # viewer／partner_club_manager），其餘角色本輪未指派，理由同 content.page.*（apps/api/README.md）。
    ("content.banner.view", "B", "B3", "content", "view", 1, 0, 0, "檢視首頁輪播", "View Home Banners"),
    ("content.banner.create", "B", "B3", "content", "create", 1, 0, 0, "新增首頁輪播", "Create Home Banners"),
    ("content.banner.update", "B", "B3", "content", "update", 1, 0, 0, "編輯首頁輪播", "Update Home Banners"),
    ("content.banner.delete", "B", "B3", "content", "delete", 1, 0, 0, "刪除首頁輪播", "Delete Home Banners"),
    ("content.home_section.view", "B", "B3", "content", "view", 1, 0, 0, "檢視首頁區塊編排", "View Home Sections"),
    ("content.home_section.update", "B", "B3", "content", "update", 1, 0, 0, "調整首頁區塊開關與排序", "Update Home Sections"),
    # S1-6 新增：B4 常見問題——規劃書 §6 矩陣把 FAQ 獨立成一欄，權限分佈跟「內容」欄不同
    # （見 apps/api/README.md 逐列說明），因此另外命名一組，不沿用 content.article.* 的角色足跡。
    # content.faq_category.*（faq_categories 無 club_id，10 個固定主題＋可再新增）is_club_scoped=0，
    # 執行層走 IAdminSystemAuthorizer（跟 J 模組同一支介面，但不是 sysadmin_only）。
    ("content.faq.view", "B", "B4", "content", "view", 1, 0, 0, "檢視常見問題", "View FAQs"),
    ("content.faq.create", "B", "B4", "content", "create", 1, 0, 0, "新增常見問題", "Create FAQs"),
    ("content.faq.update", "B", "B4", "content", "update", 1, 0, 0, "編輯常見問題", "Update FAQs"),
    ("content.faq.delete", "B", "B4", "content", "delete", 1, 0, 0, "刪除常見問題", "Delete FAQs"),
    ("content.faq_category.view", "B", "B4", "content", "view", 0, 0, 0, "檢視常見問題分類", "View FAQ Categories"),
    ("content.faq_category.create", "B", "B4", "content", "create", 0, 0, 0, "新增常見問題分類", "Create FAQ Categories"),
    ("content.faq_category.update", "B", "B4", "content", "update", 0, 0, 0, "編輯常見問題分類", "Update FAQ Categories"),
    ("content.faq_category.delete", "B", "B4", "content", "delete", 0, 0, 0, "刪除常見問題分類", "Delete FAQ Categories"),
]

emit("-- ── 18.2 permissions：J 系統管理 ＋ B2 新聞（本次唯一接真實授權的既有模組） ─────")
for code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en in PERMISSIONS:
    perm_id = new_id("permission", code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = {esc(code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(perm_id)};
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
  VALUES (@id, {esc(code)}, {esc(module_code)}, {esc(submodule_code)}, {esc(domain)}, {esc(action)}, {esc(bool(is_club_scoped))}, {esc(bool(is_restricted))}, {esc(bool(sysadmin_only))}, {esc(name_zh)}, {esc(name_en)});
END
""")

# role_permissions：系統管理員全給（雖然 is_super_admin 已經略過權限檢查，比照慈善庫慣例仍種）；
# 內容編輯給新聞的檢視／建立／編輯／發布／刪除（矩陣寫「✔ 編輯／發布」，刪除自己編輯的草稿是
# 內容管理的常態操作，本次工程判斷視為隱含在「編輯」權限內，見 apps/api/README.md 的說明）；
# 檢視者只給檢視；合作球隊管理給自家內容的檢視／建立／編輯（矩陣「✔ 自家內容」，不含發布／刪除，
# 發布與刪除留給日後有實際使用者指派時再依需求開放，屬保守預設）。
#
# S1-3 續作（2026-09-24）：team.competition.* 依規劃書 §6 矩陣「球隊／賽事」欄逐列展開——
# 競技／球隊管理 ✔全、內容編輯與檢視者唯讀、合作球隊管理僅自家（own_clubs）。system.club.*／
# system.club_grant.* 只給系統管理員（sysadmin_only，其餘角色矩陣「系統」欄皆為「—」／「✗」），
# 系統管理員那一列用 [p[0] for p in PERMISSIONS] 自動涵蓋，不需要另外列出。
#
# S1-7（2026-09-24）：team.team.*／team.player.*／team.staff.* 依規劃書 §6 矩陣「球隊／賽事」欄
# 逐列展開，跟上面 team.competition.* 用同一欄、同一套判讀（矩陣沒有為 C1/C2/C3 與 C4 分欄）：
# 系統管理員 ✔全（[p[0] for p in PERMISSIONS] 自動涵蓋）；競技／球隊管理 ✔全；內容編輯／
# 商務／贊助／公關／媒體／檢視者 唯讀；合作球隊管理 ✔自家（own_clubs，全權限）；
# 客服／行政 該欄是「—」不給任何權限；翻譯人員／學院／課程管理本輪刻意不給，見下方兩則說明。
#
# 🔴 學院／課程管理（academy_program）本輪刻意不指派 team.team.*／team.player.*／team.staff.*：
# 矩陣寫的是「學院梯隊」（scope_type=academy_only，只能碰 team.type='academy' 的球隊與其球員／
# 教練），但這個角色的列級範圍過濾（依 AdminUserTeam／team.type 篩資料列）本輪沒有做——
# 派工單明確要求「本輪球員／教練的寫入若規劃書要求依球隊授權限制，先回報再決定，不要自己擴大
# 範圍」。在列級強制做出來之前先發這三組權限碼給 academy_program，效果等同給它跟
# team_competition 一樣的全俱樂部球隊存取權（含一線隊），超出矩陣「僅學院梯隊」的授權意圖，
# 是擴大範圍不是保守預設，因此本輪不發，回報給下一輪決定（見任務回報「綱要缺口或待裁決」）。
# 🔴 翻譯人員（translator，scope_type=translate_only）本輪同樣不指派——這個角色在整份種子腳本
# 目前對任何模組都沒有半筆權限（news／pages 都還沒給），代表「僅翻譯欄位」這個範圍限制本身
# 尚未有任何模組真的做出列級或欄位級的強制，C1–C3 若第一個開先例單獨給它會造成「這個角色能寫
# 中文姓名以外的欄位」這種矩陣沒有授權的能力，故跟其餘模組保持一致，暫不指派。
ROLE_PERMISSIONS = [
    ("system_admin", [p[0] for p in PERMISSIONS], "all"),
    ("content_editor", [
        "content.article.view", "content.article.create", "content.article.update", "content.article.publish", "content.article.delete",
        # S1-4：content.page.* 逐字比照 content.article.* 的指派——規劃書 §6 矩陣「內容編輯｜內容」
        # 欄是「✔ 編輯／發布」，同一格同時管 B1 頁面與 B2 新聞（矩陣沒有分欄），刪除跟新聞一樣採
        # 「編輯自己編輯的內容屬於編輯權限的常態操作」這條既有的工程判斷（見上方 role_permissions
        # 註解），不是另外重新判斷一次。
        "content.page.view", "content.page.create", "content.page.update", "content.page.publish", "content.page.delete",
        "team.competition.view",
        "team.team.view", "team.player.view", "team.staff.view",
    ], "all"),
    ("team_competition", [
        "team.competition.view", "team.competition.create", "team.competition.update",
        "team.team.view", "team.team.create", "team.team.update",
        "team.player.view", "team.player.create", "team.player.update",
        "team.staff.view", "team.staff.create", "team.staff.update",
    ], "all"),
    # S1-7 新增：商務／贊助、公關／媒體——矩陣「球隊／賽事」欄皆為「唯讀」，這兩個角色在
    # 整份種子腳本目前完全沒有任何權限（其餘模組尚未接真實授權），本輪是第一次真的種進去。
    ("business_sponsorship", ["team.team.view", "team.player.view", "team.staff.view"], "all"),
    ("pr_media", ["team.team.view", "team.player.view", "team.staff.view"], "all"),
    ("viewer", [
        "content.article.view", "content.page.view", "team.competition.view",
        "team.team.view", "team.player.view", "team.staff.view",
    ], "all"),
    ("partner_club_manager", [
        "content.article.view", "content.article.create", "content.article.update",
        "content.page.view", "content.page.create", "content.page.update",
    ], "own_clubs"),
    ("partner_club_manager", [
        "team.competition.view", "team.competition.create", "team.competition.update",
        "team.team.view", "team.team.create", "team.team.update",
        "team.player.view", "team.player.create", "team.player.update",
        "team.staff.view", "team.staff.create", "team.staff.update",
    ], "own_clubs"),
    # S1-6 新增：B3 首頁編排——沿用「內容」欄既有四個角色的範圍（見上方 PERMISSIONS 註解）。
    ("content_editor", [
        "content.banner.view", "content.banner.create", "content.banner.update", "content.banner.delete",
        "content.home_section.view", "content.home_section.update",
    ], "all"),
    ("viewer", ["content.banner.view", "content.home_section.view"], "all"),
    ("partner_club_manager", [
        "content.banner.view", "content.banner.create", "content.banner.update",
        "content.home_section.view", "content.home_section.update",
    ], "own_clubs"),
    # S1-6 新增：B4 常見問題——矩陣「FAQ」欄是獨立分佈，不沿用「內容」欄的角色足跡（見上方
    # PERMISSIONS 註解逐列說明）。team_competition／academy_program／business_sponsorship
    # 「相關題目」需要依分類做列級限定，本輪尚未建立這種限定，本輪未指派（不是遺漏，見
    # apps/api/README.md）；translator「僅翻譯欄位」同樣需要欄位層限制，本輪未指派，跟既有
    # 模組一致的範圍縮減。
    ("content_editor", [
        "content.faq.view", "content.faq.create", "content.faq.update", "content.faq.delete",
        "content.faq_category.view", "content.faq_category.create", "content.faq_category.update", "content.faq_category.delete",
    ], "all"),
    ("pr_media", ["content.faq.view", "content.faq_category.view"], "all"),
    ("customer_service_admin", [
        "content.faq.view", "content.faq.create", "content.faq.update", "content.faq.delete",
        "content.faq_category.view",
    ], "all"),
    ("viewer", ["content.faq.view", "content.faq_category.view"], "all"),
    ("partner_club_manager", [
        "content.faq.view", "content.faq.create", "content.faq.update", "content.faq_category.view",
    ], "own_clubs"),
]

emit("-- ── 18.3 role_permissions ──────────────────────────────────────────")
for role_code, perm_codes, scope_type in ROLE_PERMISSIONS:
    for perm_code in perm_codes:
        block(f"""
IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = {role_sq(role_code)} AND permission_id = {perm_sq(perm_code)})
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ({role_sq(role_code)}, {perm_sq(perm_code)}, {esc(scope_type)});
""")

# admin_users：種子超管（docs/12b-database-tables.md §7.6 明文的帳號本身）＋ 四個「已可直接使用」
# 的測試帳號（two_factor_enabled 直接設為 1、two_factor_secret_encrypted 留 NULL）。
# 🔴 後兩者的密碼雜湊是本次用 apps/api 實際的 PasswordHasher（Argon2id）算出來的真雜湊，
# 不是像慈善庫種子腳本那樣的占位字串——這批帳號可以真的登入。two_factor_secret_encrypted
# 留 NULL 是刻意的：ASP.NET Core Data Protection 的金鑰環綁在執行中的行程，種子腳本在行程外
# 執行，沒有能力產生「這個行程解得開」的密文；AdminClubAuthorizer 只檢查 two_factor_enabled
# 布林值本身（見 Security/AdminClubAuthorizer.cs），不會去解密這個欄位，所以直接把布林值種為
# 已完成即可讓這些帳號通過強制 2FA 檢查——真正要驗證「TOTP 碼本身對不對」的流程，
# 走 sa@system.local 這個帳號實際呼叫 /2fa/setup、/2fa/confirm 兩個端點（測試見
# apps/api/Tcrfc.Api.Tests/AdminAuthTests.cs）。
ADMIN_USERS = [
    # (username, display_name, password_hash, is_super_admin, must_change_password, two_factor_enabled, role_code, club_grants)
    ("sa@system.local", "Super Admin", "$argon2id$v=19$m=65536,t=3,p=1$qA4b7/CNXFRrB044hvtBzQ==$j2coAMUKbu3mFe+Vyf1oXd4E1Rq8F1RHO/KHt4lDHQ4=",
     True, True, False, "system_admin", []),
    ("super.admin@tcrfc.test", "系統管理員（測試帳號）", "$argon2id$v=19$m=65536,t=3,p=1$zi4N9bpx0UfKi4XF1FoSlA==$S/20fV7gT9mFmaGaZ2fcAuSBZ4Pb99QdKqEqrdp6uBA=",
     True, False, True, "system_admin", []),
    ("content.editor@tcrfc.test", "內容編輯（測試帳號）", "$argon2id$v=19$m=65536,t=3,p=1$UMd2bX7X1E+kvJZReK7EXQ==$hZSGgfUeivSwdQGUg/7Bc8bHO8oHbiBuObGaPXR50EQ=",
     False, False, True, "content_editor", [("tcrfc", None)]),
    ("viewer@tcrfc.test", "檢視者（測試帳號）", "$argon2id$v=19$m=65536,t=3,p=1$Yas4MM7rRKPwY6uB3kh9NQ==$Ga0YQU8vLGh9mTJ6cbfVwDL66Guv54QyNHq44I46lD4=",
     False, False, True, "viewer", [("tcrfc", None)]),
    ("partner.club@tcrfc.test", "合作球隊管理（測試帳號，僅藍鯨）", "$argon2id$v=19$m=65536,t=3,p=1$QAl31fxqFgoUhEuvf6+j3w==$gtn5/eEYXVsALxi8DR1g4sWdzrnT3BQ1BI4m48MJjqc=",
     False, False, True, "partner_club_manager", [("bw", None)]),
    # S1-7 新增：C1–C3（team.team.*／team.player.*／team.staff.* ✔全）測試帳號，只授權 tcrfc——
    # 沿用 content.editor@tcrfc.test 的雜湊（純測試帳號不需各自唯一密碼，跟 expired.grant 同例）。
    ("team.manager@tcrfc.test", "競技／球隊管理（測試帳號）", "$argon2id$v=19$m=65536,t=3,p=1$UMd2bX7X1E+kvJZReK7EXQ==$hZSGgfUeivSwdQGUg/7Bc8bHO8oHbiBuObGaPXR50EQ=",
     False, False, True, "team_competition", [("tcrfc", None)]),
    # 🔴 授權已過期的測試帳號：expires_on 給昨天日期，專門用來驗證「授權有起訖日，到期自動失效」
    # （主站規劃書 §6「資料範圍規則」、AdminClubAuthorizer 的第③步）。
    ("expired.grant@tcrfc.test", "已過期授權（測試帳號）", "$argon2id$v=19$m=65536,t=3,p=1$UMd2bX7X1E+kvJZReK7EXQ==$hZSGgfUeivSwdQGUg/7Bc8bHO8oHbiBuObGaPXR50EQ=",
     False, False, True, "content_editor", [("tcrfc", "yesterday")]),
    # 🔴 專供 AdminAuthTests 走完整登入鎖定／2FA 設定流程的帳號，狀態刻意跟 sa@system.local 一樣
    # （must_change_password=1、two_factor_enabled=0），但不是正式的種子超管本身，避免測試改動
    # 影響到 sa@system.local 這個「文件與客戶都認得」的帳號。測試結束後會把這個帳號重設回本狀態
    # （見 AdminAuthTests 的清理邏輯），讓測試可重複執行。
    ("fresh.setup@tcrfc.test", "尚未完成設定（測試帳號）", "$argon2id$v=19$m=65536,t=3,p=1$qA4b7/CNXFRrB044hvtBzQ==$j2coAMUKbu3mFe+Vyf1oXd4E1Rq8F1RHO/KHt4lDHQ4=",
     False, True, False, "viewer", [("tcrfc", None)]),
    # 🔴 專供登入鎖定測試使用的獨立帳號——鎖定狀態是可變狀態，跟其他測試共用帳號會互相污染。
    ("lockout.test@tcrfc.test", "鎖定測試專用（測試帳號）", "$argon2id$v=19$m=65536,t=3,p=1$Yas4MM7rRKPwY6uB3kh9NQ==$Ga0YQU8vLGh9mTJ6cbfVwDL66Guv54QyNHq44I46lD4=",
     False, False, True, "viewer", [("tcrfc", None)]),
    # 🔴 唯一「兩階段驗證已停用」且「不需要先改密碼」的帳號——上面幾個「已可直接使用」的帳號
    # two_factor_enabled 都直接種為 1 但沒有真正可解密的密鑰（見本節開頭說明，只能靠
    # TestAdminTokens 直接簽權杖繞過登入本身），沒有一個帳號能真的完整走一次
    # 「打 /login → 拿到存取權杖與更新權杖 Cookie」的 HTTP 往返。這個帳號專門補這個缺口，
    # 供 AdminAuthTests 測登入、更新權杖輪替、登出這些不需要先過 2FA 關卡的端點行為。
    ("clean.login@tcrfc.test", "登入流程測試專用（測試帳號）", "$argon2id$v=19$m=65536,t=3,p=1$zi4N9bpx0UfKi4XF1FoSlA==$S/20fV7gT9mFmaGaZ2fcAuSBZ4Pb99QdKqEqrdp6uBA=",
     True, False, False, "system_admin", []),
]

emit("-- ── 18.4 admin_users：種子超管（真雜湊，Admin@123）＋ 五個角色測試帳號（真雜湊） ──")
emit("-- ⚠️ 這批雜湊是用 apps/api 的 PasswordHasher（Argon2id）實際算出來的，可以直接登入測試。")
emit("-- 密碼明文（僅供本機開發測試，不得用於任何正式環境）：")
emit("--   sa@system.local              / Admin@123")
emit("--   super.admin@tcrfc.test       / SuperAdmin@123")
emit("--   content.editor@tcrfc.test    / ContentEditor@123")
emit("--   viewer@tcrfc.test            / Viewer@123")
emit("--   partner.club@tcrfc.test      / PartnerClub@123")
emit("--   team.manager@tcrfc.test      / ContentEditor@123（沿用同一組雜湊，純測試帳號不需各自唯一密碼）")
emit("--   expired.grant@tcrfc.test     / ContentEditor@123（沿用同一組雜湊，純測試帳號不需各自唯一密碼）")
emit("--   fresh.setup@tcrfc.test       / Admin@123（沿用同一組雜湊）")
emit("--   lockout.test@tcrfc.test      / Viewer@123（沿用同一組雜湊）")
emit("--   clean.login@tcrfc.test       / SuperAdmin@123（沿用同一組雜湊，two_factor_enabled=0，唯一能走完整 /login 流程的帳號）")
for username, display_name, password_hash, is_super, must_change, two_factor, role_code, club_grants in ADMIN_USERS:
    user_id = new_id("admin_user", username)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_users WHERE username = {esc(username)};
IF @id IS NULL
BEGIN
  SET @id = {esc(user_id)};
  INSERT INTO admin_users (id, username, display_name, password_hash, must_change_password, is_super_admin, two_factor_enabled, status)
  VALUES (@id, {esc(username)}, {esc(display_name)}, {esc(password_hash)}, {esc(must_change)}, {esc(is_super)}, {esc(two_factor)}, N'active');
END
""")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM admin_user_roles WHERE admin_user_id = {admin_sq(username)} AND admin_role_id = {role_sq(role_code)})
  INSERT INTO admin_user_roles (admin_user_id, admin_role_id) VALUES ({admin_sq(username)}, {role_sq(role_code)});
""")
    for club_code, expiry in club_grants:
        club_ref = CLUB_TCRFC if club_code == "tcrfc" else CLUB_BW
        expires_sql = "DATEADD(day, -1, CAST(SYSUTCDATETIME() AS date))" if expiry == "yesterday" else "NULL"
        block(f"""
IF NOT EXISTS (SELECT 1 FROM admin_user_clubs WHERE admin_user_id = {admin_sq(username)} AND club_id = {club_ref})
  INSERT INTO admin_user_clubs (admin_user_id, club_id, granted_on, expires_on, is_active)
  VALUES ({admin_sq(username)}, {club_ref}, CAST(SYSUTCDATETIME() AS date), {expires_sql}, 1);
""")

# ============================================================================
# S1-6（B4 常見問題，2026-09-24）：faq_categories 十個固定主題
# ----------------------------------------------------------------------------
# 逐字對應主站規劃書 3.12「主題分類導覽卡」列出的十個主題（加入球隊、學院招生、課程與營隊
# 報名、費用與退費、試訓、國際發展與海外球員、女子足球、球迷會與商品、合作與贊助、其他）。
# 沒有 club_id（同 article_categories 的共用主檔設計）。後台「新增分類」端點仍可再新增，
# 這裡只是把規劃書明文列出的十個起始分類種好，不代表分類數量從此固定死。
# ============================================================================
FAQ_CATEGORIES = [
    ("join-team", "加入球隊", "Join the Club"),
    ("academy-admission", "學院招生", "Academy Admission"),
    ("programs-camps", "課程與營隊報名", "Programs & Camp Registration"),
    ("fees-refunds", "費用與退費", "Fees & Refunds"),
    ("trials", "試訓", "Trials"),
    ("international", "國際發展與海外球員", "International Development & Overseas Players"),
    ("womens-football", "女子足球", "Women's Football"),
    ("fan-club-merchandise", "球迷會與商品", "Fan Club & Merchandise"),
    ("partnerships-sponsorship", "合作與贊助", "Partnerships & Sponsorship"),
    ("other", "其他", "Other"),
]

emit("-- ── 19. faq_categories：規劃書 3.12 十個主題（不帶 club_id，全站共用主檔） ──────")
for i, (slug, name_zh, name_en) in enumerate(FAQ_CATEGORIES):
    cat_id = new_id("faq_category", slug)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM faq_categories WHERE slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(cat_id)};
  INSERT INTO faq_categories (id, slug, sort_order) VALUES (@id, {esc(slug)}, {i});
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'zh-Hant', {esc(name_zh)});
  INSERT INTO faq_categories_i18n (faq_category_id, locale, name) VALUES (@id, N'en', {esc(name_en)});
END
""")

# ============================================================================
# S1-6（B3 首頁編排，2026-09-24）：home_sections 九大區塊，兩俱樂部各種一份
# ----------------------------------------------------------------------------
# 代碼與預設排序須與 apps/api/Features/AdminHomeSections/HomeSectionCatalog.cs 逐一對應
# （C# 與本腳本各自宣告一份同樣的九個代碼，這是既有慣例——比照 SlugPolicy.cs 檔頭記錄的
# 「多處字面值常數，各自宣告，靠命名一致與 code review 維持同步」，不是自動化比對）。
# 全部區塊起始狀態為啟用（is_enabled=1），排序依規劃書 §3.1 首頁九大區塊表格列出的順序。
# ============================================================================
HOME_SECTIONS = [
    "hero", "core_values", "ecosystem_nav", "upcoming_match", "recent_fixtures",
    "latest_news", "partner_logos", "shop_entry", "bottom_cta",
]

emit("-- ── 20. home_sections：規劃書 3.1 首頁九大區塊，兩俱樂部各種一份 ───────────────")
for club_code in ("tcrfc", "bw"):
    club_ref = CLUB_TCRFC if club_code == "tcrfc" else CLUB_BW
    for i, section_code in enumerate(HOME_SECTIONS):
        section_id = new_id("home_section", club_code, section_code)
        block(f"""
IF NOT EXISTS (SELECT 1 FROM home_sections WHERE club_id = {club_ref} AND section_code = {esc(section_code)})
  INSERT INTO home_sections (id, club_id, section_code, is_enabled, sort_order)
  VALUES ({esc(section_id)}, {club_ref}, {esc(section_code)}, 1, {i});
""")

if "--reset-admin-accounts" in sys.argv:
    # 🔴 丟掉上面（一般模式）已經累積的全部輸出，只印重設用的 UPDATE 陳述式——
    # ADMIN_USERS 此時已經跑過一輪迴圈填好，重用同一份資料，不重新定義。見檔頭說明。
    out.clear()
    emit("-- db/seed/generate-club-seed-sql.py --reset-admin-accounts")
    emit("-- 還原種子測試帳號的密碼／2FA 狀態／鎖定計數回到種子腳本定義的初始值。")
    emit("-- 只 UPDATE 已存在的列，不 INSERT；角色指派與俱樂部授權不受影響（見檔頭說明）。")
    emit()
    for username, display_name, password_hash, is_super, must_change, two_factor, role_code, club_grants in ADMIN_USERS:
        block(f"""
UPDATE admin_users
SET password_hash = {esc(password_hash)},
    must_change_password = {esc(must_change)},
    is_super_admin = {esc(is_super)},
    two_factor_enabled = {esc(two_factor)},
    two_factor_secret_encrypted = NULL,
    two_factor_confirmed_at = NULL,
    failed_attempt_count = 0,
    locked_until = NULL,
    status = N'active'
WHERE username = {esc(username)};
""")

print("\n".join(out))
