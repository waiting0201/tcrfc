#!/usr/bin/env python3
# db/seed/generate-fallback-content-migration.py — 產生 db/migrations/20261113_fallback-content-import.sql（2026-11-13）
#
# 為什麼要有資料遷移，而不只是種子區段 60b：
#   內容種子（db/prod/club-content-seed.sql）只能灌進「擁有的表全空」的庫（deploy/prod-seed-import.sh preflight），
#   而正式庫與本機開發庫早已有資料（2026-10-16 的 20261016_intake-docs-import*.sql 已建了里程碑、夥伴、慈善事蹟…），
#   種子過不了 preflight。已經有資料的庫要補這幾筆真實內容，只能像 intake-docs-import 那樣走冪等資料遷移。
#   定義與 SQL 與種子區段 60b 同一份（db/seed/fallback_content_seed.py），這裡只負責包裝與事後斷言。
#
# 用法：python3 db/seed/generate-fallback-content-migration.py [--check]
#   （--check：只比對檔案是否與產生結果一致，不一致 exit 1，可掛 CI）
# 執行（本機）：
#   docker cp 進容器後 sqlcmd -S localhost -U sa -C -d <庫> -b -I -f 65001 -i <檔案>
#   正式庫：走 deploy/lib/sqlcmd-guard.sh 的 sqlcmd_to_utf16（sqlcmd 13 讀 UTF-8 會切壞多位元組字元，E-296／E-330）。

import hashlib
import importlib.util
import re
import sys
import uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[0] / "migrations" / "20261113_fallback-content-import.sql"

_spec = importlib.util.spec_from_file_location("fallback_content_seed", HERE / "fallback_content_seed.py")
fcs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fcs)

# 與 generate-club-seed-sql.py 相同的固定命名空間：同一筆業務資料在種子與遷移得到相同的 GUID
NS = uuid.UUID("6f6a9c9e-3b1a-4e7a-9c2e-2f6a1f2b7a10")


def new_id(*parts: str) -> str:
    return str(uuid.uuid5(NS, "|".join(parts)))


def esc(value) -> str:
    if value is None:
        return "NULL"
    if isinstance(value, bool):
        return "1" if value else "0"
    if isinstance(value, (int, float)):
        return str(value)
    return "N'" + str(value).replace("'", "''") + "'"


out: list[str] = []


def emit(text: str = ""):
    out.append(text)


def block(sql: str):
    """同 generate-club-seed-sql.py 的 block()：「IF … BEGIN … END」樣式自動包交易。"""
    text = sql.strip()
    if "\nBEGIN\n" in text and text.endswith("END"):
        text = re.sub(r"\nBEGIN\n", "\nBEGIN\n  BEGIN TRANSACTION;\n", text, count=1)
        text = re.sub(r"\nEND$", "\n  COMMIT TRANSACTION;\nEND", text, count=1)
    emit(text)
    emit("GO")
    emit()


def build() -> str:
    clubs = {"tcrfc": "(SELECT id FROM clubs WHERE code = N'tcrfc')", "bw": "(SELECT id FROM clubs WHERE code = N'bw')"}
    emit(f"""/* ============================================================================
   官網「後台沒資料就顯示寫死內容」的真實內容轉入後台資料（資料遷移）
   2026-11-13｜使用者拍板拿掉前台的寫死後備（docs/23 H-1 B 類、C-5）；拿掉之前，真實內容必須先在資料庫裡。
   定義與種子區段 60b 同一份（db/seed/fallback_content_seed.py）；本檔由 db/seed/generate-fallback-content-migration.py 產生，請勿手改。

   ── 本檔補的內容（全部有中英兩份；沒有圖片，圖片由營運人員在後台上傳）──
     ① 磐石國際夥伴 3 筆（Hellas Verona FC／Rayo Ciudad Alcobendas CF／Rot-Weiss Ahlen；類型「國際夥伴」）
     ② 藍鯨建教合作學校 5 所（類型「教育夥伴」）
     ③ 贊助方案名稱：磐石 9、藍鯨 8（規劃書 §3.9 9.4；只有名稱，價格不公開、沒有權益文字）
     ④ 磐石榮譽：成立首年頭銜「2024 全國乙級聯賽 冠軍」（找不到球隊 D1 或任何球季就略過）
     ⑤ 頁尾簡介、社群連結、藍鯨聯絡 Email（只補「列不存在」或「值為空且從沒人儲存過」）

   ── 不在本檔（原因見 db/seed/README.md「官網寫死後備轉後台資料（區段 60b）」）──
     里程碑（正式庫已有客戶文件匯入的 49 筆）、慈善事蹟與影響力（同上，3 筆）、球迷會活動照片（沒有活動名稱與日期）、
     藍鯨兒童訓練班別與守門員班（課程梯次的欄位對不上「元／堂」）、首頁新聞卡（新聞已在庫）、場地占位卡（占位）、商店球衣照（占位）。

   ── 冪等與保護 ──
     夥伴以 (俱樂部, slug)；贊助方案以 (俱樂部, 繁中名稱)——同名方案（含本機種子的【測試】方案）已存在就不再補；
     榮譽以 (俱樂部, 年, 繁中賽事名)；設定只動空值且沒人儲存過者。後台人員改過的內容一律不動。可重複執行。
   • 任何事後斷言失敗即 THROW；搭配 XACT_ABORT 與 sqlcmd -b，失敗時不留半套資料（每批各自成交易）。
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF (SELECT COUNT(*) FROM clubs WHERE code IN (N'tcrfc', N'bw')) <> 2
  THROW 51301, N'找不到 tcrfc 或 bw 俱樂部（clubs.code）', 1;
GO
""")
    fcs.emit_all(emit=emit, block=block, esc=esc, new_id=new_id, clubs=clubs)

    # 事後斷言：每一項「應該存在」的自然鍵都查得到；榮譽依前置條件不斷言
    intl = ", ".join(esc(p[0]) for p in fcs.INTL_PARTNERS)
    schools = ", ".join(esc(p[0]) for p in fcs.BW_SCHOOLS)
    pkg_checks = []
    for code, rows in fcs.PACKAGES.items():
        for _slug, zh, _en in rows:
            pkg_checks.append(
                f"SELECT 1 AS ok FROM clubs c WHERE c.code = {esc(code)} AND NOT EXISTS (SELECT 1 FROM sponsor_packages p JOIN sponsor_packages_i18n i "
                f"ON i.sponsor_package_id = p.id AND i.locale = N'zh-Hant' WHERE p.club_id = c.id AND i.name = {esc(zh)})")
    missing_pkg = "\n    UNION ALL ".join(pkg_checks)
    emit(f"""DECLARE @intl int = (SELECT COUNT(*) FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND slug IN ({intl}));
DECLARE @schools int = (SELECT COUNT(*) FROM partners WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND slug IN ({schools}));
DECLARE @missingPkg int = (SELECT COUNT(*) FROM (
    {missing_pkg}
  ) x);
IF @intl <> {len(fcs.INTL_PARTNERS)} THROW 51302, N'磐石國際夥伴應有 {len(fcs.INTL_PARTNERS)} 筆', 1;
IF @schools <> {len(fcs.BW_SCHOOLS)} THROW 51303, N'藍鯨建教合作學校應有 {len(fcs.BW_SCHOOLS)} 所', 1;
IF @missingPkg <> 0 THROW 51304, N'贊助方案名稱未補齊', 1;
PRINT N'完成：磐石國際夥伴 ' + CAST(@intl AS nvarchar(10)) + N'、藍鯨建教合作學校 ' + CAST(@schools AS nvarchar(10)) + N'、贊助方案名稱齊全。';
GO
""")
    return "\n".join(out).rstrip("\n") + "\n"


def main():
    sql = build()
    if "--check" in sys.argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != sql:
            print(f"{OUT} 與產生結果不一致；請重新執行 python3 db/seed/generate-fallback-content-migration.py", file=sys.stderr)
            sys.exit(1)
        print("OK")
        return
    OUT.write_text(sql, encoding="utf-8")
    print(f"已寫入 {OUT}（sha256 {hashlib.sha256(sql.encode('utf-8')).hexdigest()[:12]}）")


if __name__ == "__main__":
    main()
