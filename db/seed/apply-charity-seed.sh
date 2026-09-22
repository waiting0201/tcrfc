#!/usr/bin/env bash
# db/seed/apply-charity-seed.sh — 產生並灌入本機開發用種子資料（只灌 tcrfc_charity_dev）
#
# 做兩件事：
#   1. 執行 generate-charity-seed-sql.py，產生 db/seed/.generated/charity-seed.local.sql
#      （不含真實個資——內容本身就是虛構測試資料，但產物仍統一放 .generated/ 不進版控，
#      與 club-seed.local.sql 一致存放，方便管理）
#   2. 用 sqlcmd 對本機 SQL Server instance 內的 tcrfc_charity_dev 資料庫套用該檔
#
# 冪等：整份 .sql 用「業務自然鍵 IF NOT EXISTS 才 INSERT」寫成，可重複執行。
#
# 🔴🔴 為什麼這是另一支腳本，不是把慈善庫加進 db/seed/apply-seed.sh 的白名單 🔴🔴
#   慈善捐款平台自 v2.0 起是完全獨立的系統——獨立網域、獨立前台、獨立後台、獨立資料庫，
#   主辦與收款主體是台灣足球策略發展協會，不是台中磐石足球俱樂部（docs/16-charity-schema.md
#   §0、§9）。`db/seed/apply-seed.sh` 的檔頭明文寫「慈善庫是獨立法人邊界，本腳本不處理
#   慈善資料」——這是刻意的設計，不是遺漏。把兩庫的種子混進同一支腳本，或放寬那支腳本的
#   白名單，等於在程式碼層面抹掉這條法人邊界，之後任何人接手都可能誤以為兩庫可以共用
#   同一套灌資料流程。因此本檔獨立存在，目標資料庫白名單只認 tcrfc_charity_dev 一個，
#   不共用 db/seed/apply-seed.sh 的 TARGET_DATABASE 變數、不呼叫該腳本、也不被該腳本呼叫。
#
# 容器防呆模型與 db/seed/apply-seed.sh 相同（見 docker-compose.dev.yml 檔頭、
# deploy/README.md）：本機的 SQL Server 是宿主機上既有的 `sqlserver` 容器，裡面同時住著
# 使用者另一個專案的約 25 個資料庫，且沒有掛 volume。目標容器可由 LOCAL_MSSQL_CONTAINER
# 環境變數指定（預設 "sqlserver"），但目標資料庫名稱寫死只允許 tcrfc_charity_dev 一個。
#
# 用法：
#   ./db/seed/apply-charity-seed.sh            產生並套用
#   ./db/seed/apply-charity-seed.sh --dry-run  只產生 .sql，不套用（等同直接跑 generate 腳本）

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && cd .. && pwd)"
OUT_DIR="${SCRIPT_DIR}/.generated"
OUT_FILE="${OUT_DIR}/charity-seed.local.sql"

# 本機 SQL Server 所在的容器名稱：預設用既有的 sqlserver 容器，可用環境變數覆寫。
LOCAL_MSSQL_CONTAINER="${LOCAL_MSSQL_CONTAINER:-sqlserver}"

# ⛔ 本腳本唯一允許寫入的資料庫。與 db/seed/apply-seed.sh 的白名單刻意分開維護——
# 那支腳本的白名單只認 tcrfc_club_dev，這支只認 tcrfc_charity_dev，兩份清單互不覆蓋、
# 互不放寬，任何一支腳本都不會意外碰到對方的資料庫。
readonly TARGET_DATABASE="tcrfc_charity_dev"

mkdir -p "${OUT_DIR}"

echo "==> 產生種子 SQL（虛構測試資料，來源：generate-charity-seed-sql.py 內建資料）"
python3 "${SCRIPT_DIR}/generate-charity-seed-sql.py" > "${OUT_FILE}"
echo "    ${OUT_FILE}（$(wc -l < "${OUT_FILE}" | tr -d ' ') 行，不進版控）"

if [[ "${1:-}" == "--dry-run" ]]; then
  echo
  echo "--dry-run：只產生檔案，未套用。"
  exit 0
fi

echo
echo "==> 即將操作的目標"
echo "    容器（docker container name）：${LOCAL_MSSQL_CONTAINER}"
echo "    資料庫（僅這一個，寫死）：${TARGET_DATABASE}"

# 依 docker container 名稱精確比對（不是 compose service 名稱——這個容器不是本專案 compose 管理的）。
CONTAINER_ID="$(docker ps -q --filter "name=^/${LOCAL_MSSQL_CONTAINER}\$" || true)"
if [[ -z "${CONTAINER_ID}" ]]; then
  echo "找不到執行中的容器 '${LOCAL_MSSQL_CONTAINER}'。" >&2
  echo "若你要用的是既有的 sqlserver 容器，請確認它已在跑（docker ps）；" >&2
  echo "若容器名稱不同，設定環境變數 LOCAL_MSSQL_CONTAINER 指到正確的容器名稱。" >&2
  exit 1
fi

: "${MSSQL_DEV_SA_PASSWORD:?請設定 MSSQL_DEV_SA_PASSWORD（須與 '${LOCAL_MSSQL_CONTAINER}' 容器的 SA 密碼一致，例如 set -a; source .env; set +a）}"

echo
echo "==> 套用到 ${TARGET_DATABASE}"

# -f 65001：以 UTF-8 讀取輸入檔，種子資料含中文姓名／文案，不指定會被系統預設 codepage 誤譯。
docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
  -d "${TARGET_DATABASE}" -f 65001 -b \
  < "${OUT_FILE}"

echo
echo "完成。"
