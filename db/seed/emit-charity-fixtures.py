#!/usr/bin/env python3
"""db/seed/emit-charity-fixtures.py — 把慈善種子資料匯出成 JSON，給前後台 mockup 當假資料用。

## 為什麼需要這支

`apps/web-charity`（捐款前台）與 `apps/admin-charity`（協會後台）這一輪都是**可點 mockup**，
還不接 API（慈善的 API 端點尚未實作，且後台要驗的是捐款成功／失敗／退款、發票已開立／作廢、
對帳有差異這些多態資料，唯讀 API 本來就給不出來）。

問題在於：如果兩個前端各自手寫一份假資料，就會出現**三份互相對不上的資料**——
資料庫裡一份、前台一份、後台一份。同一筆捐款在後台顯示「已退款」、在前台結果頁卻是「成功」，
而且沒有任何機制會發現。

所以這支腳本讓假資料成為**衍生物而不是第二份手寫資料**：
`generate-charity-seed-sql.py` 是唯一真實來源，本檔只是把它的資料結構換一種格式輸出。

⚠️ **不要在前端另外手寫慈善假資料。** 要改資料就改 `generate-charity-seed-sql.py`，
然後重跑本檔與 `apply-charity-seed.sh`，資料庫與兩個前端就會同步。

## 用法

    python3 db/seed/emit-charity-fixtures.py            # 寫入 db/seed/charity-fixtures.json
    python3 db/seed/emit-charity-fixtures.py --check    # 只檢查檔案是不是最新的，過期就 exit 1

🔴 **產物 `db/seed/charity-fixtures.json` 是納版控的**，刻意不放 `.generated/`。
理由：兩個前端在 build 時會 import 它，而 `.generated/` 不納版控——
乾淨 clone 下來那個目錄是空的，**build 會直接壞在找不到檔案**，
而且是建置期硬錯誤不是執行期 404（`apps/web` 的圖片就踩過同一類坑，見 `docs/18` `E-26`）。

這批資料**全部是虛構測試資料**，沒有主站種子那種真人姓名的顧慮，納版控是安全的。

⚠️ **納版控的衍生檔會過期。** `--check` 模式重新產生一次並與檔案內容比對，
不一致就 exit 1——掛進兩個前端的 `npm run lint`，改了種子卻忘記重跑本檔就會被擋下。

🔵 **這批資料本身全部是虛構測試資料**（姓名「測試用．…」、Email `@example.test`、
電話與統編連續 0、`*_encrypted` 欄位是明講不是真加密的占位字串），所以它與主站的
`generate-club-seed-sql.py` 不同——主站那支讀外部 JSON 是為了不把**真人姓名**硬編碼進腳本，
慈善這邊沒有那個顧慮，資料直接寫在腳本裡是合理的。
"""

import importlib.util
import json
import sys
from datetime import date, datetime
from decimal import Decimal
from pathlib import Path

HERE = Path(__file__).resolve().parent
SEED_SCRIPT = HERE / "generate-charity-seed-sql.py"
REPO_ROOT = HERE.parents[1]
# 🔴 納版控，不放 .generated/——理由見檔頭「用法」一節。
OUT_FILE = HERE / "charity-fixtures.json"

# 🔴 各 app 目錄內的副本，也納版控。
#
# 為什麼要有副本（不是偷懶，是被 Docker 的 build context 逼出來的）：
# `docs/20-cicd.md` §3 的既有慣例是**以 app 目錄為 build context**
#（`docker build -t x apps/admin` 已實測過），所以 repo 根目錄的
# db/seed/charity-fixtures.json **落在 build context 之外，容器內讀不到**。
# 2026-09-22 實測 `docker build apps/web-charity` 確實失敗：
#   [sync-fixtures] 找不到來源檔案：/db/seed/charity-fixtures.json
#
# 兩條路：① 把 build context 改成 repo root（要改兩支 Dockerfile、要加根目錄
# .dockerignore 擋掉 456MB 的收件夾與 reference/，並推翻 docs/20 已記錄的慣例）
# ② 在各 app 內放一份納版控的副本。選 ②，因為改動面小、不動既有部署慣例，
# 而副本最怕的「漂移」由下方 --check 擋住（三份檔案內容必須完全一致）。
#
# ⛔ 這些副本是機器產生的，不要手動編輯，也不要在前端另外手寫假資料。
APP_COPIES = [
    HERE.parents[1] / "apps" / "web-charity" / "fixtures" / "charity-fixtures.json",
    HERE.parents[1] / "apps" / "admin-charity" / "fixtures" / "charity-fixtures.json",
]


def load_seed_module():
    """把 generate-charity-seed-sql.py 當模組載入。

    ⚠️ 該檔名有連字號，不是合法的 Python 識別字，`import` 進不來，只能走 importlib。
    載入時它會把整份種子 SQL 印到 stdout（那是它的正常行為），所以先把 stdout
    暫時導到 stderr 以外的地方吞掉，避免污染本檔要輸出的 JSON。
    """
    spec = importlib.util.spec_from_file_location("charity_seed", SEED_SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"載入不了 {SEED_SCRIPT}")
    module = importlib.util.module_from_spec(spec)

    real_stdout = sys.stdout
    try:
        sys.stdout = open("/dev/null", "w", encoding="utf-8")
        spec.loader.exec_module(module)
    finally:
        sys.stdout.close()
        sys.stdout = real_stdout
    return module


def plain(value):
    """Decimal／date／set／tuple 轉成 JSON 表達得出來的型別。金額一律轉字串，不轉 float。"""
    if isinstance(value, (date, datetime)):
        # ISO 8601，與資料庫的 date／datetime2 欄位對得上；前端自己決定顯示格式
        #（日期格式規則見 docs/06-conventions.md §5，不在這裡先格式化）。
        return value.isoformat()
    if isinstance(value, Decimal):
        # ⛔ 不要轉 float：金額與百分比走二進位浮點會產生 0.1+0.2 那種誤差，
        #    這批資料會被前端拿去顯示金額，字串保留精確值由前端自己決定怎麼格式化。
        return str(value)
    if isinstance(value, set):
        return sorted(plain(v) for v in value)
    if isinstance(value, (list, tuple)):
        return [plain(v) for v in value]
    if isinstance(value, dict):
        return {str(k): plain(v) for k, v in value.items()}
    return value


def main():
    m = load_seed_module()

    # 要匯出的常數名稱 → 輸出鍵。刻意逐一列出而不是「全部大寫變數都匯出」——
    # 後者會把 ZERO_PCT 這類計算用的中間值也一起倒出來，前端拿到只會困惑。
    EXPORT = [
        ("ROLES", "roles"),
        ("PERMISSIONS", "permissions"),
        ("ROLE_PERMISSION_MAP", "rolePermissionMap"),
        ("ADMIN_USERS", "adminUsers"),
        ("CHARITY_REFS", "charityRefs"),
        ("CHARITY_PROGRAM_REFS", "charityProgramRefs"),
        ("STORES", "stores"),
        ("STORE_SLUGS", "storeSlugs"),
        ("PROJECTS", "projects"),
        ("PROJECT_SLUGS", "projectSlugs"),
        ("AMOUNT_OPTIONS", "amountOptions"),
        ("DONATIONS", "donations"),
        ("DONATION_SPLITS", "donationSplits"),
        ("PAYMENTS", "payments"),
        ("INVOICES", "invoices"),
        ("SETTLEMENT_PLAN", "settlementPlan"),
        # 🔴 2026-09-22 補上：原本這兩類資料在種子腳本裡是 SQL 字面值，匯不出來，
        #    慈善後台只好逐字轉錄自己存一份——那正是本管線要防止的第二份真實來源。
        ("RECONCILIATION_RUNS", "reconciliationRuns"),
        ("RECONCILIATION_DISCREPANCIES", "reconciliationDiscrepancies"),
        ("AUDIT_LOGS", "auditLogs"),
        ("SETTINGS_PLAIN", "settings"),
        ("SETTINGS_I18N", "settingsI18n"),
        ("EMAIL_TEMPLATES", "emailTemplates"),
        ("EMAIL_LOGS", "emailLogs"),
    ]

    data = {}
    missing = []
    for const_name, out_key in EXPORT:
        if not hasattr(m, const_name):
            missing.append(const_name)
            continue
        data[out_key] = plain(getattr(m, const_name))

    if missing:
        # 🔴 失敗而不是靜默少匯出幾塊：常數改名了卻沒人發現，前端就會拿到一份
        #    悄悄缺了幾類資料的 fixtures，而畫面上只會看起來「這一區剛好是空的」。
        raise SystemExit(
            "generate-charity-seed-sql.py 裡找不到這些常數："
            + "、".join(missing)
            + "\n（常數被改名或移除了？請同步更新本檔的 EXPORT 清單。）"
        )

    out = {
        "_comment": (
            "本檔由 db/seed/emit-charity-fixtures.py 從 db/seed/generate-charity-seed-sql.py 產生，"
            "是衍生物不是手寫資料。⛔ 不要直接編輯本檔，也不要在前端另外手寫一份慈善假資料——"
            "要改就改 generate-charity-seed-sql.py，再重跑本檔與 apply-charity-seed.sh，"
            "資料庫與兩個前端才會是同一份資料。"
            "🔵 全部是本機開發用的虛構測試資料：姓名「測試用．…」、Email @example.test、"
            "電話與統編連續 0、*_encrypted 欄位是明講不是真加密的占位字串。"
        ),
        "_source": "db/seed/generate-charity-seed-sql.py",
        **data,
    }
    rendered = json.dumps(out, ensure_ascii=False, indent=2) + "\n"

    targets = [OUT_FILE, *APP_COPIES]

    if "--check" in sys.argv:
        stale = []
        for t in targets:
            rel = t.relative_to(REPO_ROOT)
            if not t.exists():
                stale.append(f"{rel}（不存在）")
            elif t.read_text(encoding="utf-8") != rendered:
                stale.append(f"{rel}（內容與種子不同步）")
        if stale:
            raise SystemExit(
                "✗ 慈善 fixtures 不同步：\n  - "
                + "\n  - ".join(stale)
                + "\n\n  種子資料改過了但衍生檔沒有重新產生——前端會拿到舊資料而資料庫是新的，"
                "\n  兩邊對不上，而且畫面上看不出來。"
                "\n  請跑：python3 db/seed/emit-charity-fixtures.py"
            )
        print(f"✓ 慈善 fixtures 與種子同步（{len(targets)} 份檔案內容一致）")
        return

    for t in targets:
        t.parent.mkdir(parents=True, exist_ok=True)
        t.write_text(rendered, encoding="utf-8")
        print(f"✓ 已寫入 {t.relative_to(REPO_ROOT)}", file=sys.stderr)


if __name__ == "__main__":
    main()
