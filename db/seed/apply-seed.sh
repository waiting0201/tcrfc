#!/usr/bin/env bash
# db/seed/apply-seed.sh — 產生並灌入本機開發用種子資料（只灌 tcrfc_club_dev）
#
# 做兩件事：
#   1. 執行 generate-club-seed-sql.py，讀 site/src/data/*.json 產生 db/seed/.generated/club-seed.local.sql
#      （該檔含真實姓名，不進版控，見 .gitignore）
#   2. 用 sqlcmd 對 mssql-dev 容器內的 tcrfc_club_dev 資料庫套用該檔
#
# 冪等：整份 .sql 用「業務自然鍵 IF NOT EXISTS 才 INSERT」寫成，可重複執行。
#
# 🔴 只認 compose service 名稱 mssql-dev，絕不碰名為 "sqlserver" 的既有容器（另一個非本
# 專案 compose 管理的環境）。只灌 tcrfc_club_dev，不動 tcrfc_charity_dev（慈善庫是獨立法人
# 邊界，本腳本不處理慈善資料，也沒有慈善的種子來源）。
#
# 用法：
#   ./db/seed/apply-seed.sh            產生並套用
#   ./db/seed/apply-seed.sh --dry-run  只產生 .sql，不套用（等同直接跑 generate 腳本）

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && cd .. && pwd)"
OUT_DIR="${SCRIPT_DIR}/.generated"
OUT_FILE="${OUT_DIR}/club-seed.local.sql"

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
echo "==> 套用到 mssql-dev 容器的 tcrfc_club_dev"

# 只認 compose service 名稱 mssql-dev。
CONTAINER_ID="$(docker compose -f "${REPO_ROOT}/docker-compose.yml" -f "${REPO_ROOT}/docker-compose.dev.yml" ps -q mssql-dev || true)"
if [[ -z "${CONTAINER_ID}" ]]; then
  echo "找不到執行中的 mssql-dev 容器。請先：" >&2
  echo "  docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d mssql-dev" >&2
  exit 1
fi

: "${MSSQL_DEV_SA_PASSWORD:?請設定 MSSQL_DEV_SA_PASSWORD（與 .env 一致，例如 set -a; source .env; set +a）}"

# -f 65001：以 UTF-8 讀取輸入檔，種子資料含中文姓名／標題，不指定會被系統預設 codepage 誤譯。
docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
  -d tcrfc_club_dev -f 65001 -b \
  < "${OUT_FILE}"

echo
echo "完成。"
