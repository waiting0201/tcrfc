#!/usr/bin/env bash
# db/seed/apply-seed.sh — 產生並灌入本機開發用種子資料（只灌 tcrfc_club_dev）
#
# 做兩件事：
#   1. 執行 generate-club-seed-sql.py，讀 site/src/data/*.json 產生 db/seed/.generated/club-seed.local.sql
#      （該檔含真實姓名，不進版控，見 .gitignore）
#   2. 用 sqlcmd 對本機 SQL Server instance 內的 tcrfc_club_dev 資料庫套用該檔
#
# 冪等：整份 .sql 用「業務自然鍵 IF NOT EXISTS 才 INSERT」寫成，可重複執行。
#
# 🔴🔴 2026-09-21：本機開發資料庫已合併進既有的 sqlserver 容器（見 docker-compose.dev.yml、
# deploy/README.md）。防呆模型因此改變：目標容器可由 LOCAL_MSSQL_CONTAINER 環境變數指定
# （預設 "sqlserver"），但目標資料庫名稱寫死只允許 tcrfc_club_dev 一個——本腳本從來就只灌
# 主站庫，不動 tcrfc_charity_dev（慈善庫是獨立法人邊界，本腳本不處理慈善資料，也沒有慈善
# 的種子來源），這條白名單同時防止「打錯容器」與「打錯庫」兩種情況，兩者現在是同一個風險
# 來源（同一個 instance 裡還有使用者其他專案的資料庫）。
#
# 用法：
#   ./db/seed/apply-seed.sh            產生並套用（預設灌 tcrfc_club_dev）
#   ./db/seed/apply-seed.sh --dry-run  只產生 .sql，不套用（等同直接跑 generate 腳本）
#
# 🔴 2026-09-25（S0-13）：目標資料庫可用 SEED_TARGET_DATABASE 環境變數覆寫，僅接受下方
# ALLOWED_DATABASES 白名單裡的名字（新增 tcrfc_club_test，整合測試專用庫，見
# db/seed/setup-test-db.sh）。不帶這個環境變數時行為與之前完全一致（灌 tcrfc_club_dev），
# 不影響既有呼叫端（含 CI）。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && cd .. && pwd)"
OUT_DIR="${SCRIPT_DIR}/.generated"
OUT_FILE="${OUT_DIR}/club-seed.local.sql"

# 本機 SQL Server 所在的容器名稱：預設用既有的 sqlserver 容器，可用環境變數覆寫。
LOCAL_MSSQL_CONTAINER="${LOCAL_MSSQL_CONTAINER:-sqlserver}"

# ⛔ 本腳本允許寫入的資料庫只有這兩個。與 deploy/local-ddl.sh 的白名單分開維護是刻意的——
# 那支腳本管建表，這支腳本只管種子資料，範圍本來就不同，不共用同一份清單反而更清楚「這支
# 腳本能碰到的資料庫就只有這些」。tcrfc_club_test 是 2026-09-25（S0-13）新增的整合測試專用庫。
ALLOWED_DATABASES=("tcrfc_club_dev" "tcrfc_club_test")
TARGET_DATABASE="${SEED_TARGET_DATABASE:-tcrfc_club_dev}"

assert_allowed_database() {
  local db="$1"
  for allowed in "${ALLOWED_DATABASES[@]}"; do
    if [[ "${db}" == "${allowed}" ]]; then
      return 0
    fi
  done
  echo "拒絕執行：SEED_TARGET_DATABASE='${db}' 不在允許清單內（${ALLOWED_DATABASES[*]}）。" >&2
  exit 1
}
assert_allowed_database "${TARGET_DATABASE}"

mkdir -p "${OUT_DIR}"

echo "==> 產生種子 SQL（來源：site/src/data/*.json）"
python3 "${SCRIPT_DIR}/generate-club-seed-sql.py" > "${OUT_FILE}"
echo "    ${OUT_FILE}（$(wc -l < "${OUT_FILE}" | tr -d ' ') 行，不進版控）"

if [[ "${1:-}" == "--dry-run" ]]; then
  echo
  echo "--dry-run：只產生檔案，未套用。"
  exit 0
fi

echo
echo "==> 即將操作的目標"
echo "    容器（docker container name）：${LOCAL_MSSQL_CONTAINER}"
echo "    資料庫（僅白名單內的名字，見 ALLOWED_DATABASES）：${TARGET_DATABASE}"

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

# -f 65001：以 UTF-8 讀取輸入檔，種子資料含中文姓名／標題，不指定會被系統預設 codepage 誤譯。
docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
  -d "${TARGET_DATABASE}" -f 65001 -b \
  < "${OUT_FILE}"

echo
echo "完成。"
