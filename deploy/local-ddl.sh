#!/usr/bin/env bash
# deploy/local-ddl.sh — 產生 db/*.sql 的「本機 SQL Server 2022 相容版」，不改動原始檔
#
# 為什麼需要這支腳本：
#   db/club-schema.sql、db/charity-schema.sql 用的是 Azure SQL 原生 `json` 型別
#   （docs/12-database-schema.md §1.4 第 2 件、docs/17-deployment.md §6）。
#   本機開發用的是一般 SQL Server 2022 容器（docker-compose.dev.yml 的 mssql-dev 服務），
#   **SQL Server 2022 沒有原生 json 型別**，直接執行原始 .sql 會建表失敗。
#
#   這支腳本只轉換「本機要用的副本」，絕不改動版控裡的原始 db/*.sql
#   ——那兩份檔案的真實來源是 docs/12 系列文件，不能被本機環境的限制牽著走
#   （CLAUDE.md 全域規定第 2 條：docs/ 不得引入規劃書沒有的規格；本檔比照，不得讓「本機能跑」
#   反過來改變交付物的綱要）。
#
# 轉換規則：只把「欄位型別是 json」換成 nvarchar(max)，不動其他任何字元。
#   比對規則刻意寫成「前面是空白字元、後面是空白字元＋NULL」，而不是單純比對 "json" 這個詞——
#   這份 DDL 裡還有很多註解也寫到「json」（例如「json 欄位一律 Azure SQL 原生 json 型別」），
#   這些不是欄位型別宣告，不能被誤換。已實測：club-schema.sql 精準命中 10 處、
#   charity-schema.sql 精準命中 2 處欄位宣告，註解行一處都沒被動到。
#
# 用法：
#   deploy/local-ddl.sh              只產生轉換後的 SQL，印出結果路徑
#   deploy/local-ddl.sh --apply      產生後，額外對已啟動的 mssql-dev 容器執行 sqlcmd 建庫＋建表
#                                    （需要 docker compose -f docker-compose.yml -f docker-compose.dev.yml
#                                      的 mssql-dev 服務已經在跑；本腳本本身不會啟動任何容器）
#
# 🔴 本腳本絕不觸碰名為 "sqlserver" 的既有容器（那是另一個、非本專案 compose 管理的既有環境，
# 依任務指示不得動它或它裡面的任何資料庫）。--apply 只認 compose service 名稱 mssql-dev。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC_DIR="${REPO_ROOT}/db"
OUT_DIR="${REPO_ROOT}/deploy/dev/.local-ddl-output"

APPLY=0
if [[ "${1:-}" == "--apply" ]]; then
  APPLY=1
fi

mkdir -p "${OUT_DIR}"

# 只把「欄位型別是 json」換成 nvarchar(max)：比對「前面一個空白字元、json、後面一個以上空白字元、NULL」。
# 用 sed -E（跨 macOS／Linux 皆可，不依賴 GNU-only 的 \b）。
convert() {
  local src="$1" dst="$2"
  sed -E 's/([[:space:]])json([[:space:]]+NULL)/\1nvarchar(max)\2/g' "${src}" > "${dst}"
}

echo "==> 轉換 db/club-schema.sql"
convert "${SRC_DIR}/club-schema.sql" "${OUT_DIR}/club-schema.local.sql"
CLUB_CHANGED=$(diff "${SRC_DIR}/club-schema.sql" "${OUT_DIR}/club-schema.local.sql" | grep -c '^>' || true)
echo "    共替換 ${CLUB_CHANGED} 處欄位型別 → ${OUT_DIR}/club-schema.local.sql"

echo "==> 轉換 db/charity-schema.sql"
convert "${SRC_DIR}/charity-schema.sql" "${OUT_DIR}/charity-schema.local.sql"
CHARITY_CHANGED=$(diff "${SRC_DIR}/charity-schema.sql" "${OUT_DIR}/charity-schema.local.sql" | grep -c '^>' || true)
echo "    共替換 ${CHARITY_CHANGED} 處欄位型別 → ${OUT_DIR}/charity-schema.local.sql"

echo
echo "==> 原始檔完全未變動（只讀，未寫入）："
echo "    ${SRC_DIR}/club-schema.sql"
echo "    ${SRC_DIR}/charity-schema.sql"

if [[ "${APPLY}" -eq 0 ]]; then
  echo
  echo "只產生檔案，未執行。要灌進本機開發用資料庫，加 --apply（需先啟動 mssql-dev 服務）。"
  exit 0
fi

echo
echo "==> --apply：對 mssql-dev 容器執行 sqlcmd"

# 只認 compose service 名稱 mssql-dev，不會、也不允許對到既有的 "sqlserver" 容器。
CONTAINER_ID="$(docker compose -f "${REPO_ROOT}/docker-compose.yml" -f "${REPO_ROOT}/docker-compose.dev.yml" ps -q mssql-dev || true)"
if [[ -z "${CONTAINER_ID}" ]]; then
  echo "找不到執行中的 mssql-dev 容器。請先：" >&2
  echo "  docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d mssql-dev" >&2
  exit 1
fi

: "${MSSQL_DEV_SA_PASSWORD:?請設定 MSSQL_DEV_SA_PASSWORD（要與 docker-compose.dev.yml 給 mssql-dev 的 SA 密碼一致，見 .env）}"

run_sqlcmd() {
  local db="$1" sql_file="$2"
  echo "    建庫與建表：${db}"
  docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
    -Q "IF DB_ID('${db}') IS NULL CREATE DATABASE [${db}];"
  docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C -d "${db}" \
    < "${sql_file}"
}

run_sqlcmd "tcrfc_club_dev" "${OUT_DIR}/club-schema.local.sql"
run_sqlcmd "tcrfc_charity_dev" "${OUT_DIR}/charity-schema.local.sql"

echo
echo "完成。"
