#!/usr/bin/env bash
# apps/api/scripts/native-json-test.sh — 在「原生 json 型別」的 SQL Server 2025 容器上跑整套 api 測試
#
# 為什麼要有這支：正式環境（Azure SQL）的 json 欄位是原生 json 型別，只接受 JSON 物件或陣列，
# 純量（字串、數字、true、null 字面值）寫入會被拒（Msg 13609）。本機預設的 SQL Server 2022 容器
# 沒有原生 json，deploy/local-ddl.sh 把它轉成 nvarchar(max)——nvarchar 什麼都收，所以只在 2022 上
# 跑測試永遠看不到這類錯誤（docs/18 E-111）。這支腳本用 mssql/server:2025-latest 接「原樣 DDL」
# （不經 local-ddl.sh 的轉換），重現正式環境的欄位行為。
#
# 與開發用的 sqlserver 容器完全隔離：獨立容器名、獨立埠（預設 14335）、獨立 SA 密碼，
# 不碰任何既有容器或資料庫。
#
# 用法：
#   apps/api/scripts/native-json-test.sh up            建容器、建庫（原樣 DDL）、灌種子（冪等；已存在就重用）
#   apps/api/scripts/native-json-test.sh test [dotnet test 參數…]
#                                                      在該容器上跑 dotnet test（沒有容器會先 up）
#   apps/api/scripts/native-json-test.sh reset         丟掉兩個庫、重建（測試弄髒資料庫時用）
#   apps/api/scripts/native-json-test.sh down          移除容器（資料一併丟棄）
#   apps/api/scripts/native-json-test.sh sql "<T-SQL>" 對該容器執行一段 SQL（除錯用）
#
# 環境變數：NATIVE_JSON_CONTAINER（預設 tcrfc-mssql2025-nativejson）、NATIVE_JSON_PORT（預設 14335）、
#           NATIVE_JSON_IMAGE（預設 mcr.microsoft.com/mssql/server:2025-latest）。
# SA 密碼首次建立時隨機產生，存在 ~/.cache/tcrfc-native-json/sa-password（權限 600），不進版控、不印出。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../../.." && pwd)"
CONTAINER="${NATIVE_JSON_CONTAINER:-tcrfc-mssql2025-nativejson}"
PORT="${NATIVE_JSON_PORT:-14335}"
IMAGE="${NATIVE_JSON_IMAGE:-mcr.microsoft.com/mssql/server:2025-latest}"
STATE_DIR="${HOME}/.cache/tcrfc-native-json"
PW_FILE="${STATE_DIR}/sa-password"

sa_password() {
  mkdir -p "${STATE_DIR}"
  if [[ ! -s "${PW_FILE}" ]]; then
    # 前綴保證通過 SQL Server 的複雜度規則；其餘為隨機字元
    umask 077
    printf 'Nj1%s' "$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 20)" > "${PW_FILE}"
  fi
  cat "${PW_FILE}"
}

SA_PW="$(sa_password)"

sqlcmd_in() {  # sqlcmd_in <db> [args…]；SQL 由 stdin 或 -Q 帶入
  local db="$1"; shift
  docker exec -i -e SQLCMDPASSWORD="${SA_PW}" "${CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -C -d "${db}" -f 65001 -b "$@"
}

container_running() { [[ -n "$(docker ps -q --filter "name=^/${CONTAINER}\$")" ]]; }

ensure_container() {
  if container_running; then return 0; fi
  if [[ -n "$(docker ps -aq --filter "name=^/${CONTAINER}\$")" ]]; then
    echo "==> 啟動既有容器 ${CONTAINER}"
    docker start "${CONTAINER}" >/dev/null
  else
    echo "==> 建立容器 ${CONTAINER}（${IMAGE}，127.0.0.1:${PORT}）"
    docker run -d --name "${CONTAINER}" -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="${SA_PW}" \
      -p "127.0.0.1:${PORT}:1433" "${IMAGE}" >/dev/null
  fi
  echo -n "    等待 SQL Server 就緒"
  for _ in $(seq 1 60); do
    if docker exec -e SQLCMDPASSWORD="${SA_PW}" "${CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
         -S localhost -U sa -C -Q "SELECT 1" >/dev/null 2>&1; then echo " 完成"; return 0; fi
    echo -n "."; sleep 2
  done
  echo; echo "SQL Server 未在 120 秒內就緒" >&2; exit 1
}

table_count() {
  docker exec -e SQLCMDPASSWORD="${SA_PW}" "${CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -C -d "$1" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables;" 2>/dev/null | tr -d '[:space:]'
}

ensure_dbs() {
  local db sql
  for db in tcrfc_club tcrfc_charity; do
    sqlcmd_in master -Q "IF DB_ID('${db}') IS NULL CREATE DATABASE [${db}];"
  done
  # 原樣 DDL：不經 deploy/local-ddl.sh 的 json→nvarchar(max) 轉換，這是整支腳本存在的理由。
  if [[ "$(table_count tcrfc_club || echo 0)" == "0" ]]; then
    echo "==> 套用原樣 db/club-schema.sql（原生 json）"
    sqlcmd_in tcrfc_club < "${REPO_ROOT}/db/club-schema.sql"
    SEED_CLUB=1
  fi
  if [[ "$(table_count tcrfc_charity || echo 0)" == "0" ]]; then
    echo "==> 套用原樣 db/charity-schema.sql（原生 json）"
    sqlcmd_in tcrfc_charity < "${REPO_ROOT}/db/charity-schema.sql"
    SEED_CHARITY=1
  fi
  # 種子腳本沿用既有的 apply-seed.sh／apply-charity-seed.sh（只換目標容器與密碼）
  if [[ "${SEED_CLUB:-0}" == "1" ]]; then
    LOCAL_MSSQL_CONTAINER="${CONTAINER}" MSSQL_DEV_SA_PASSWORD="${SA_PW}" "${REPO_ROOT}/db/seed/apply-seed.sh"
  fi
  if [[ "${SEED_CHARITY:-0}" == "1" ]]; then
    LOCAL_MSSQL_CONTAINER="${CONTAINER}" MSSQL_DEV_SA_PASSWORD="${SA_PW}" "${REPO_ROOT}/db/seed/apply-charity-seed.sh"
  fi
}

conn() {  # conn <db>
  printf 'Server=127.0.0.1,%s;Database=%s;User Id=sa;Password=%s;TrustServerCertificate=True;Encrypt=False;' "${PORT}" "$1" "${SA_PW}"
}

case "${1:-}" in
  up)
    ensure_container; ensure_dbs
    echo "完成。連線埠 127.0.0.1:${PORT}；跑測試用：$0 test"
    ;;
  test)
    shift
    ensure_container; ensure_dbs
    echo "==> dotnet test（連到 ${CONTAINER}，原生 json）"
    cd "${REPO_ROOT}/apps/api/Tcrfc.Api.Tests"
    CLUB_SQL_CONNECTION_STRING="$(conn tcrfc_club)" CHARITY_SQL_CONNECTION_STRING="$(conn tcrfc_charity)" \
      dotnet test "$@"
    ;;
  reset)
    ensure_container
    for db in tcrfc_club tcrfc_charity; do
      sqlcmd_in master -Q "IF DB_ID('${db}') IS NOT NULL BEGIN ALTER DATABASE [${db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${db}]; END;"
    done
    ensure_dbs
    ;;
  down)
    docker rm -f "${CONTAINER}" >/dev/null 2>&1 || true
    echo "已移除 ${CONTAINER}"
    ;;
  sql)
    ensure_container
    sqlcmd_in "${3:-tcrfc_club}" -Q "${2:?用法：$0 sql \"<T-SQL>\" [資料庫]}"
    ;;
  *)
    sed -n 2,26p "$0" | sed 's/^# \{0,1\}//'; exit 1
    ;;
esac
