#!/usr/bin/env python3
# db/seed/generate-prod-reference-sql.py — 產生「正式庫首次初始化」用的參照資料 SQL
#
# 輸出（納入版控，與測試種子 db/seed/.generated/*.local.sql 完全分開）：
#   db/prod/club-reference-data.sql      主站庫 tcrfc_club
#   db/prod/charity-reference-data.sql   慈善庫 tcrfc_charity（獨立法人邊界，單獨產生、單獨執行）
#
# 為什麼不另寫一份：參照資料（角色、權限碼、固定字典、固定表單……）的定義早已在
# generate-club-seed-sql.py／generate-charity-seed-sql.py，另抄一份就會有兩份事實漂移。
# 本腳本改為「跑原產生器 → 只留允許清單內的區段」，定義仍只有一份。
#
# 🔴 這不是種子資料。種子資料含 mockup 內容與測試帳號（sa@system.local／Admin@123 等），
#   絕不能進正式庫。本腳本的防線：
#     1. 允許清單（ALLOW_*）：只列區段編號，沒列的區段一律丟棄。新增種子區段預設是「不進正式庫」，
#        要進必須在這裡明確加一行並說明理由——忘記加＝不灌，不會悄悄灌出去。
#     2. 內容守衛（FORBIDDEN）：輸出若出現測試標記（【測試】、@example、*.test、argon2id 雜湊……）
#        整份拒絕寫出。
#     3. `--check`：比對版控內的檔案與現在重新產生的結果，不一致就失敗（可掛進 CI）。
#
# 用法：
#   python3 db/seed/generate-prod-reference-sql.py           重新產生兩份檔案
#   python3 db/seed/generate-prod-reference-sql.py --check   只比對，不一致 exit 1
#
# 輸出檔的 sqlcmd 變數（由 deploy/prod-db-init.sh 以環境變數提供）：
#   $(CLUB_DOMAIN_TCRFC)／$(CLUB_DOMAIN_BW)   clubs.domain，取自 VM 的 /opt/tcrfc/.env（TCRFC_DOMAIN／BW_DOMAIN），
#                                              讓「資料庫裡的網域」與「實際部署的網域」只有一個來源，不在 SQL 裡寫死。

import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUT_DIR = REPO / "db" / "prod"

# ── 允許清單：區段編號（見各產生器的 `-- ── N. 標題` 標記）──────────────────────────
# 準則：「沒有這些列，程式會丟例外、404、被外鍵擋住，或後台無法操作」＝系統運作必需。
# 內容（球員、新聞、頁面文案、賽程……）後台都能自己建，不在此列。
ALLOW_CLUB = {
    "0": "locales：所有 *_i18n 側表的外鍵目標",
    "1": "clubs（台中磐石／台中藍鯨）：多俱樂部路由與一切 club_id 的根；網域改用 sqlcmd 變數",
    "5": "article_categories：規劃書 7.1–7.8 固定八類，新聞的外鍵目標",
    "18.1": "admin_roles：規劃書 §6 的十個角色",
    "18.2": "permissions：權限碼目錄（J 與各模組）",
    "18.3": "role_permissions：角色與權限碼的對應（規劃書 §6 矩陣）",
    "19": "faq_categories：規劃書 3.12 十個固定主題",
    "20": "home_sections：規劃書 3.1 首頁九大固定區塊（兩俱樂部各一份）",
    "21": "faq_embed_slots：G-12 四個固定掛載點",
    "22": "forms／form_fields：規劃書 §3.10 八個固定表單與預設欄位（兩俱樂部各一份）",
    "23": "event_types：L2 自建事件的起始分類字典（後台 L3 可再編輯）",
}
ALLOW_CHARITY = {
    "0": "locales",
    "1": "admin_roles：沿用主站九個角色",
    "2": "permissions：N1–N7 權限碼",
    "3": "role_permissions",
    "3b": "CH-3 新增的 API 專用權限碼（重新向金流確認付款結果）",
}

# 個別區段裡仍要剔除的區塊（區段整體需要，但其中某個 GO 區塊是內容）
DROP_CHUNK_CLUB = [
    # §1 尾端：藍鯨簡介文字（舊官網文案，屬內容，不是系統必需）
    re.compile(r"UPDATE\s+clubs_i18n\s+SET\s+description"),
]

# 區段內需要把寫死的值換成 sqlcmd 變數（斷言恰好換到指定次數，換不到就失敗，避免產生器改版後悄悄漏換）
SUBSTITUTE_CLUB = [
    ("N'stg.tcrfc.tw'", "N'$(CLUB_DOMAIN_TCRFC)'", 1),
    ("N'bw-domain-pending.invalid'", "N'$(CLUB_DOMAIN_BW)'", 1),
]

# 內容守衛：輸出出現這些就拒絕（測試資料與測試帳號的特徵）
FORBIDDEN = [
    (re.compile(r"【測試】"), "【測試】前綴"),
    (re.compile(r"\[Test\]"), "[Test] 前綴"),
    (re.compile(r"@example\.|@[a-z0-9.-]+\.test\b|example\.com|\.invalid"), "測試網域／信箱"),
    (re.compile(r"\$argon2id\$"), "Argon2id 雜湊（測試帳號）"),
    (re.compile(r"Admin@123|SuperAdmin@123|ContentEditor@123|Viewer@123|PartnerClub@123"), "測試密碼"),
    (re.compile(r"INSERT\s+INTO\s+admin_users\b", re.I), "寫入 admin_users（正式庫第一個管理員由 prod-db-init.sh create-admin 建立）"),
    (re.compile(r"dev seed|開發測試|佔位|占位"), "開發占位文字"),
]

# 允許出現的 sqlcmd 變數（其餘 $( 一律視為異常）
ALLOWED_VARS = {"CLUB_DOMAIN_TCRFC", "CLUB_DOMAIN_BW"}

MARKER = re.compile(r"^-- ── (\S+?)\.?\s")


def run_generator(script: str, extra: list[str] | None = None) -> str:
    cmd = [sys.executable, str(HERE / script)] + (extra or [])
    res = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", check=False)
    if res.returncode != 0:
        sys.stderr.write(res.stderr)
        raise SystemExit(f"{script} 執行失敗（exit {res.returncode}）")
    return res.stdout


def split_chunks(text: str) -> list[tuple[str, list[str]]]:
    """把產生器輸出切成 (區段編號, 行清單) 的 GO 批次。區段編號取自最近一次出現的 `-- ── N.` 標記，
    第一個標記之前的批次編號為 'pre'。"""
    chunks: list[tuple[str, list[str]]] = []
    section = "pre"
    cur: list[str] = []
    for line in text.splitlines():
        m = MARKER.match(line)
        if m:
            section = m.group(1)
        if line.strip() == "GO":
            if any(l.strip() for l in cur):
                chunks.append((section, cur))
            cur = []
        else:
            cur.append(line)
    if any(l.strip() for l in cur):
        chunks.append((section, cur))
    return chunks


def build(script: str, allow: dict[str, str], title: str, substitutions, drop_patterns) -> str:
    raw = run_generator(script)
    chunks = split_chunks(raw)
    seen_sections = {s for s, _ in chunks}
    missing = [s for s in allow if s not in seen_sections]
    if missing:
        raise SystemExit(f"{script}：允許清單的區段在產生器輸出中找不到：{missing}（產生器改版？請更新 ALLOW_*）")

    kept: list[str] = []
    for section, lines in chunks:
        if section == "pre":
            # 只保留 SET 選項（連線層級，篩選唯一索引與 XACT_ABORT 需要）；檔頭註解丟棄
            sets = [l for l in lines if l.startswith("SET ")]
            if sets:
                kept.append("\n".join(sets) + "\nGO\n")
            continue
        if section not in allow:
            continue
        body = "\n".join(lines).strip("\n")
        if any(p.search(body) for p in drop_patterns):
            continue
        kept.append(body + "\nGO\n")

    sql = "\n".join(kept)

    for old, new, times in substitutions:
        found = sql.count(old)
        if found != times:
            raise SystemExit(f"{script}：預期替換「{old}」{times} 次，實際 {found} 次（產生器改版？）")
        sql = sql.replace(old, new)

    for pattern, label in FORBIDDEN:
        hit = pattern.search(sql)
        if hit:
            line_no = sql[: hit.start()].count("\n") + 1
            raise SystemExit(f"{script}：輸出含禁止內容（{label}），第 {line_no} 行附近：{sql[hit.start():hit.start()+60]!r}")

    for var in re.findall(r"\$\(([A-Za-z0-9_]+)\)", sql):
        if var not in ALLOWED_VARS:
            raise SystemExit(f"{script}：出現未預期的 sqlcmd 變數 $({var})")
    if re.search(r"\$\((?![A-Za-z0-9_]+\))", sql):
        raise SystemExit(f"{script}：出現無法解析的 `$(`")

    # 各表預期列數（欄位 INSERT INTO <table> 的出現次數；在空庫上每個陳述式恰好執行一次，
    # 因為產生器的迴圈在 Python 端已展開）。deploy/prod-db-init.sh 以此核對灌完後的實際筆數。
    counts: dict[str, int] = {}
    for m in re.finditer(r"^\s*INSERT\s+INTO\s+([a-z_0-9]+)\b", sql, re.I | re.M):
        counts[m.group(1)] = counts.get(m.group(1), 0) + 1

    header = [
        f"-- ============================================================================",
        f"-- {title}",
        f"-- 自動產生：python3 db/seed/generate-prod-reference-sql.py（請勿手動編輯；改定義請改原產生器後重新產生）",
        f"-- 來源產生器：db/seed/{script}",
        f"-- 🔴 這是「參照資料」不是種子資料：只含系統運作必需的列，沒有任何測試內容與測試帳號。",
        f"-- 冪等：每個實體以業務自然鍵判斷（IF NOT EXISTS 才 INSERT），但 deploy/prod-db-init.sh 仍只在空庫上執行。",
        f"-- 區段（允許清單）：",
    ]
    for sec, why in allow.items():
        header.append(f"--   {sec:<5} {why}")
    header.append("-- 預期列數（deploy/prod-db-init.sh 灌完後逐表核對；`-- MANIFEST` 行是機器讀的）：")
    for table in sorted(counts):
        header.append(f"-- MANIFEST {table}={counts[table]}")
    header.append("-- ============================================================================")
    header.append("")
    return "\n".join(header) + "\n" + sql


def main() -> int:
    check = "--check" in sys.argv[1:]
    outputs = {
        OUT_DIR / "club-reference-data.sql": build(
            "generate-club-seed-sql.py", ALLOW_CLUB,
            "TCRFC 主站庫（tcrfc_club）正式庫首次初始化：參照資料", SUBSTITUTE_CLUB, DROP_CHUNK_CLUB),
        OUT_DIR / "charity-reference-data.sql": build(
            "generate-charity-seed-sql.py", ALLOW_CHARITY,
            "TCRFC 慈善庫（tcrfc_charity）正式庫首次初始化：參照資料", [], []),
    }
    drift = False
    for path, content in outputs.items():
        if check:
            current = path.read_text(encoding="utf-8") if path.exists() else None
            if current != content:
                print(f"[不一致] {path.relative_to(REPO)} 與重新產生的結果不同——請執行 python3 db/seed/generate-prod-reference-sql.py 後一併提交")
                drift = True
            else:
                print(f"[一致] {path.relative_to(REPO)}")
        else:
            OUT_DIR.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8", newline="\n")
            n_insert = len(re.findall(r"^\s*INSERT\s+INTO", content, re.M))
            print(f"寫出 {path.relative_to(REPO)}（{len(content.splitlines())} 行、{n_insert} 個 INSERT）")
    return 1 if drift else 0


if __name__ == "__main__":
    raise SystemExit(main())
