#!/usr/bin/env python3
# db/seed/generate-prod-content-sql.py — 產生「把本機種子的內容資料匯入正式庫」用的 SQL（2026-10-03，使用者決定：
# 為了在正式機做前後台串接的實機驗收，把本機種子匯入正式庫；驗收結束後可清除）
#
# 輸出（納入版控，供審查；與 db/prod/*-reference-data.sql 分開，兩者不重疊）：
#   db/prod/club-content-seed.sql       主站庫 tcrfc_club
#   db/prod/charity-content-seed.sql    慈善庫 tcrfc_charity（獨立法人邊界，單獨產生、單獨執行）
#
# 與 generate-prod-reference-sql.py 的關係：兩支腳本都是「跑原產生器 → 依區段編號篩」，定義仍只有一份。
# 差別在於分類——每一個區段編號都必須被明確歸入下列四類之一，**沒有歸類的新區段會讓本腳本直接失敗**
# （不是悄悄丟掉，也不是悄悄灌進去；新增種子區段時必須在這裡表態）：
#   IMPORT      內容資料：匯入（區段內仍會再依「禁用表」逐個 GO 批次剔除，見下）
#   REFERENCE   參照資料：正式庫初始化時已由 db/prod/*-reference-data.sql 灌過，不重複插入
#   ACCOUNTS    帳號類：admin_users／角色綁定。🔴 絕不匯入——正式庫只有 prod-db-init.sh create-admin 建的那一位
#   PERSONAL    假個資或假交易：會員、報名、訂單、捐款、金流、發票、對帳、稽核、寄信紀錄、App 裝置……預設不匯入
#
# 區段內的「禁用表」守衛（DENY_TABLES）：IMPORT 區段中若某個 GO 批次會寫入禁用表，整批剔除。
# 這讓 36（提案可匯入、Lead 不匯入）、42（試訓場次可匯入、報名不匯入）、50、54 能在同一區段內分流。
# 參照表（REFERENCE_TABLES）同理：任何會 INSERT 參照表的批次整批剔除（正式庫已有），
# 唯一例外是 `UPDATE clubs_i18n SET description`（藍鯨簡介文字，內容，參照資料腳本刻意丟掉的那一段）。
#
# 每個 GO 批次大多是「IF NOT EXISTS 才 INSERT」的自然鍵寫法，但匯入程序（deploy/prod-seed-import.sh）
# 仍只在「擁有的表全為空」時執行，並把整份檔案包在單一交易裡（任何一句失敗整批回滾），
# 最後寫入資料庫層級的延伸屬性 tcrfc.seed_import 作為「已匯入」標記。
#
# 輸出檔檔頭的機器可讀行：
#   -- OWNED <table>      本次匯入會寫入的表（清除程序只動這些表；匯入前必須全空）
#   -- DROPPED section=… 被剔除的批次摘要（審查用）
# 各表匯入後的實際筆數不由本腳本推算（多列 VALUES、INSERT…SELECT 無法可靠數），而是在本機演練庫實跑一次後，
# 由 `deploy/prod-seed-import.sh record-manifest` 寫入 db/prod/*-content-manifest.tsv，verify 逐表核對。
#
# 輸出檔使用的 sqlcmd 變數（由 deploy/prod-seed-import.sh 以環境變數提供）：
#   $(CHARITY_DOMAIN)   主站 charity.donation_url 設定值（本機種子是 charity.example.com 占位；
#                       改成 VM /opt/tcrfc/.env 的 CHARITY_DOMAIN，讓導流連結實際可點）
#   $(IMPORT_BATCH)     寫進延伸屬性的批次字串（庫名、UTC 時間、SQL 檔 sha256 前 12 碼）
#
# 用法：
#   python3 db/seed/generate-prod-content-sql.py           重新產生兩份檔案
#   python3 db/seed/generate-prod-content-sql.py --check   只比對，不一致 exit 1（可掛 CI）
#   python3 db/seed/generate-prod-content-sql.py --report  印出每個區段的分類與剔除摘要（不寫檔）

import importlib.util
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUT_DIR = REPO / "db" / "prod"

_spec = importlib.util.spec_from_file_location("prodref", HERE / "generate-prod-reference-sql.py")
prodref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(prodref)  # 只取 split_chunks／run_generator，不會執行 main（有 __main__ 守衛）

# ── 區段分類 ────────────────────────────────────────────────────────────────────
CLUB_SECTIONS = {
    # 參照資料（正式庫初始化已灌；與 generate-prod-reference-sql.py 的 ALLOW_CLUB 完全對應）
    "0": "REFERENCE", "5": "REFERENCE", "18.1": "REFERENCE", "18.2": "REFERENCE", "18.3": "REFERENCE",
    "19": "REFERENCE", "20": "REFERENCE", "21": "REFERENCE", "22": "REFERENCE", "23": "REFERENCE",
    # 帳號類：絕不匯入
    "18.4": "ACCOUNTS",
    # 假個資／假交易：預設不匯入
    "44": "PERSONAL",  # 虛構會員、會籍、會員卡、付款、球衣領取
    "53": "PERSONAL",  # 虛構訂單、出貨、退款（連帶庫存異動）
    "55": "PERSONAL",  # 電子報名單
    "59": "PERSONAL",  # App 示範裝置、推播送達、診斷回報
    "60": "PERSONAL",  # 本機驗收專用（球衣登記會員、漫畫第 101／102 集，圖片鍵指向本機 Azurite 才有的物件）：只給本機開發庫
    # 內容資料
    **{s: "IMPORT" for s in [
        "1",  # 只留藍鯨簡介 UPDATE（clubs 本身是參照資料）
        "2", "2b", "3", "4", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17",
        "24", "24b", "24c", "25", "26", "27", "28", "29", "30", "31", "31b", "32", "33", "34", "35",
        "36",  # 提案可匯入；Lead（enquiries）依禁用表剔除
        "37", "37b", "38", "39",
        "42",  # 試訓場次可匯入；報名（registrations）依禁用表剔除
        "43", "45", "46", "47", "48", "49",
        "50",  # 活動可匯入；活動報名依禁用表剔除
        "51", "52",
        "54",  # 只剩 member.draw_notice_confirmed 設定；抽獎名單（含會員快照）依禁用表剔除
        "56", "57", "58",
    ]},
}
CHARITY_SECTIONS = {
    "0": "REFERENCE", "1": "REFERENCE", "2": "REFERENCE", "3": "REFERENCE", "3b": "REFERENCE",
    "4": "ACCOUNTS",
    "8": "PERSONAL", "9": "PERSONAL", "10": "PERSONAL", "11": "PERSONAL", "12a": "PERSONAL", "12b": "PERSONAL",
    "13": "PERSONAL", "16": "PERSONAL",
    "17": "PERSONAL",  # payment_channels：sandbox 占位憑證，正式庫的金流設定不該是占位值
    **{s: "IMPORT" for s in ["5a", "5b", "6", "7", "7b", "14a", "14b", "15"]},
}

# 參照表：正式庫已有，任何 INSERT 都剔除
CLUB_REFERENCE_TABLES = {
    "locales", "clubs", "clubs_i18n", "admin_roles", "permissions", "role_permissions",
    "article_categories", "article_categories_i18n", "faq_categories", "faq_categories_i18n",
    "home_sections", "faq_embed_slots", "forms", "form_fields", "form_fields_i18n",
    "event_types", "event_types_i18n",
}
CHARITY_REFERENCE_TABLES = {"locales", "admin_roles", "permissions", "role_permissions"}

# 禁用表：帳號、假個資、假交易。IMPORT 區段內的批次只要碰到（寫入）就整批剔除
CLUB_DENY_TABLES = {
    "admin_users", "admin_user_roles", "admin_user_clubs",
    "members", "memberships", "member_cards", "membership_payments", "jersey_issues",
    "registrations", "enquiries", "enquiry_answers", "fan_event_registrations",
    "member_draws", "member_draws_i18n", "draw_roster_versions", "draw_rosters",
    "orders", "order_items", "shipments", "refund_requests", "refund_request_items",
    "newsletter_subscribers", "app_devices", "push_messages", "push_messages_i18n",
    "push_message_stats", "app_diagnostic_reports",
}
CHARITY_DENY_TABLES = {
    "admin_users", "admin_user_roles",
    "donations", "donation_payments", "donation_invoices", "settlements", "settlement_lines",
    "reconciliation_runs", "reconciliation_discrepancies", "audit_logs", "email_logs", "payment_channels",
}

# 參照表唯一允許的 UPDATE：(表, SET 的第一個欄位)
CLUB_ALLOWED_REFERENCE_UPDATES = {("clubs_i18n", "description")}

# sqlcmd 變數替換（斷言恰好換到指定次數，產生器改版後換不到就失敗）
SUBSTITUTE_CLUB = [("N'https://charity.example.com/'", "N'https://$(CHARITY_DOMAIN)/'", 1)]

# 內容守衛（輸出出現就整份拒絕）。注意：【測試】、@example 在內容資料裡是預期的（標示用），不在此列
FORBIDDEN = [
    (re.compile(r"\$argon2id\$"), "Argon2id 雜湊（測試帳號或會員）"),
    (re.compile(r"Admin@123|SuperAdmin@123|ContentEditor@123|Viewer@123|PartnerClub@123"), "測試密碼"),
    (re.compile(r"sa@system\.local|@system\.local|@tcrfc\.test"), "種子管理員帳號"),
    (re.compile(r"bw-domain-pending\.invalid|N'stg\.tcrfc\.tw'"), "clubs.domain 的本機值（正式庫維持現值）"),
    (re.compile(r"(UPDATE|INSERT\s+INTO|DELETE\s+FROM)\s+clubs\b(?!_i18n)"), "寫入 clubs（domain 必須維持正式庫現值）"),
]
ALLOWED_VARS = {"CHARITY_DOMAIN", "IMPORT_BATCH"}

TOUCH = re.compile(
    r"\b(INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+\[?([a-z_0-9]+)\]?(?:\s+SET\s+\[?([a-z_0-9]+)\]?)?")


def touches(body: str):
    """回傳 [(動作, 表, SET 第一欄或 None)]。動作為 INSERT／UPDATE／DELETE。"""
    out = []
    for m in TOUCH.finditer(body):
        kind = m.group(1).split()[0]
        out.append((kind, m.group(2), m.group(3)))
    return out


def build(script, sections, reference_tables, deny_tables, allowed_ref_updates, substitutions, label, db_name):
    raw = prodref.run_generator(script)
    chunks = prodref.split_chunks(raw)
    seen = {s for s, _ in chunks if s != "pre"}

    unknown = sorted(seen - set(sections))
    if unknown:
        raise SystemExit(
            f"{script}：區段 {unknown} 沒有歸類。請在 generate-prod-content-sql.py 的 *_SECTIONS 明確標為 "
            f"IMPORT／REFERENCE／ACCOUNTS／PERSONAL 之一（新增種子區段時必須表態，不會預設匯入或預設丟棄）")
    missing = sorted(set(sections) - seen)
    if missing:
        raise SystemExit(f"{script}：分類表裡的區段在產生器輸出找不到：{missing}（產生器改版？請更新分類表）")

    # 參照資料腳本的允許清單必須與本檔的 REFERENCE 完全一致（兩邊各自漂移會造成重複插入或漏灌）
    ref_allow = prodref.ALLOW_CLUB if label == "club" else prodref.ALLOW_CHARITY
    ref_here = {s for s, c in sections.items() if c == "REFERENCE"}
    if label == "club":
        ref_here |= {"1"}  # 區段 1（clubs）屬參照資料，但其中藍鯨簡介 UPDATE 這一批是內容，故分類為 IMPORT 並靠批次剔除
    if set(ref_allow) != ref_here:
        raise SystemExit(
            f"{script}：REFERENCE 區段 {sorted(ref_here)} 與 generate-prod-reference-sql.py 的 ALLOW 清單 "
            f"{sorted(ref_allow)} 不一致——兩份腳本的界線必須相同")

    kept, dropped = [], []
    owned: dict[str, int] = {}
    for section, lines in chunks:
        body = "\n".join(lines).strip("\n")
        if section == "pre":
            sets = [l for l in lines if l.startswith("SET ")]
            if sets:
                kept.append("\n".join(sets) + "\nGO\n")
            continue
        if sections[section] != "IMPORT":
            continue
        t = touches(body)
        deny_hit = sorted({tab for _, tab, _ in t if tab in deny_tables})
        if deny_hit:
            dropped.append((section, "禁用表", deny_hit))
            continue
        ref_insert = sorted({tab for k, tab, _ in t if k == "INSERT" and tab in reference_tables})
        if ref_insert:
            dropped.append((section, "參照表（正式庫已有）", ref_insert))
            continue
        for k, tab, first_col in t:
            if k in ("UPDATE", "DELETE") and tab in reference_tables:
                if (tab, first_col) not in allowed_ref_updates:
                    raise SystemExit(f"{script}：區段 {section} 有對參照表的 {k} {tab}（{first_col}），不在允許清單")
        for k, tab, _ in t:
            if k == "INSERT":
                owned[tab] = owned.get(tab, 0) + 1
        kept.append(body + "\nGO\n")

    sql = "\n".join(kept)

    for old, new, times in substitutions:
        found = sql.count(old)
        if found != times:
            raise SystemExit(f"{script}：預期替換「{old}」{times} 次，實際 {found} 次（產生器改版？）")
        sql = sql.replace(old, new)

    for pattern, why in FORBIDDEN:
        hit = pattern.search(sql)
        if hit:
            line_no = sql[: hit.start()].count("\n") + 1
            raise SystemExit(f"{script}：輸出含禁止內容（{why}），第 {line_no} 行附近：{sql[hit.start():hit.start()+60]!r}")
    for var in re.findall(r"\$\(([A-Za-z0-9_]+)\)", sql):
        if var not in ALLOWED_VARS:
            raise SystemExit(f"{script}：出現未預期的 sqlcmd 變數 $({var})")
    if re.search(r"\$\((?![A-Za-z0-9_]+\))", sql):
        raise SystemExit(f"{script}：出現無法解析的 `$(`")
    for tab in owned:
        if tab in deny_tables or tab in reference_tables:
            raise SystemExit(f"{script}：擁有的表含禁用表或參照表：{tab}")

    header = [
        "-- ============================================================================",
        f"-- TCRFC {'主站庫（tcrfc_club）' if label == 'club' else '慈善庫（tcrfc_charity）'}：本機種子的「內容資料」匯入正式庫（供前後台串接實機驗收）",
        "-- 自動產生：python3 db/seed/generate-prod-content-sql.py（請勿手動編輯；改界線請改該腳本後重新產生）",
        f"-- 來源產生器：db/seed/{script}",
        "-- 🔴 這不是參照資料（那是 db/prod/*-reference-data.sql），也不是帳號：",
        "--    不含任何 admin_users、會員、報名、訂單、捐款、金流、發票、對帳、稽核、寄信紀錄（見 db/seed/README.md「匯入正式庫的內容種子」）。",
        "-- 內容仍帶有【測試】前綴與 example.com 信箱（標示用）；驗收結束後用 deploy/prod-seed-import.sh clean 全部清除。",
        "-- 整份檔案由 deploy/prod-seed-import.sh 包在單一交易內執行（開頭 BEGIN TRANSACTION、結尾寫延伸屬性並 COMMIT）。",
        "-- 區段分類：",
    ]
    for cat in ("IMPORT", "REFERENCE", "ACCOUNTS", "PERSONAL"):
        ss = [s for s, c in sections.items() if c == cat]
        header.append(f"--   {cat:<9} {', '.join(sorted(ss, key=_sec_key))}")
    header.append("-- 區段內剔除的批次（審查用）：")
    agg: dict[tuple, int] = {}
    for sec, why, tabs in dropped:
        agg[(sec, why, ",".join(tabs))] = agg.get((sec, why, ",".join(tabs)), 0) + 1
    for (sec, why, tabs), n in sorted(agg.items(), key=lambda kv: _sec_key(kv[0][0])):
        header.append(f"-- DROPPED section={sec} batches={n} reason={why} tables={tabs}")
    header.append("-- 匯入會寫入的表（清除程序只動這些表；匯入前必須全空）：")
    for tab in sorted(owned):
        header.append(f"-- OWNED {tab}")
    header.append("-- 匯入前後筆數必須不變的表（帳號、假個資、假交易；匯入程序以此確認沒有夾帶）：")
    for tab in sorted(deny_tables):
        header.append(f"-- DENIED {tab}")
    header.append("-- ============================================================================")
    header.append("")

    wrap_begin = "BEGIN TRANSACTION;\nGO\n"
    wrap_end = (
        "EXEC sys.sp_addextendedproperty @name = N'tcrfc.seed_import', @value = N'$(IMPORT_BATCH)';\n"
        "COMMIT TRANSACTION;\nGO\n")
    # SET 選項批次要在 BEGIN TRANSACTION 之前（它們是連線層級，順序無關，但保持可讀）
    first_go = sql.index("GO\n") + 3
    body = sql[:first_go] + "\n" + wrap_begin + "\n" + sql[first_go:].lstrip("\n")
    return "\n".join(header) + "\n" + body + "\n" + wrap_end, dropped, owned


def _sec_key(s):
    m = re.match(r"(\d+)(.*)", s)
    return (int(m.group(1)), m.group(2)) if m else (9999, s)


def main() -> int:
    check = "--check" in sys.argv[1:]
    report = "--report" in sys.argv[1:]
    club_sql, club_drop, club_owned = build(
        "generate-club-seed-sql.py", CLUB_SECTIONS, CLUB_REFERENCE_TABLES, CLUB_DENY_TABLES,
        CLUB_ALLOWED_REFERENCE_UPDATES, SUBSTITUTE_CLUB, "club", "tcrfc_club")
    charity_sql, charity_drop, charity_owned = build(
        "generate-charity-seed-sql.py", CHARITY_SECTIONS, CHARITY_REFERENCE_TABLES, CHARITY_DENY_TABLES,
        set(), [], "charity", "tcrfc_charity")
    outputs = {
        OUT_DIR / "club-content-seed.sql": club_sql,
        OUT_DIR / "charity-content-seed.sql": charity_sql,
    }
    if report:
        for name, drop, owned in (("club", club_drop, club_owned), ("charity", charity_drop, charity_owned)):
            print(f"[{name}] 擁有 {len(owned)} 張表；剔除 {len(drop)} 個批次")
            for sec, why, tabs in drop:
                print(f"   section {sec}: {why} {tabs}")
        return 0
    drift = False
    for path, content in outputs.items():
        if check:
            current = path.read_text(encoding="utf-8") if path.exists() else None
            if current != content:
                print(f"[不一致] {path.relative_to(REPO)} 與重新產生的結果不同——請執行 python3 db/seed/generate-prod-content-sql.py 後一併提交")
                drift = True
            else:
                print(f"[一致] {path.relative_to(REPO)}")
        else:
            OUT_DIR.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8", newline="\n")
            print(f"寫出 {path.relative_to(REPO)}（{len(content.splitlines())} 行）")
    return 1 if drift else 0


if __name__ == "__main__":
    raise SystemExit(main())
