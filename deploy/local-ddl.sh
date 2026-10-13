#!/usr/bin/env bash
# deploy/local-ddl.sh — 把 db/*.sql 原樣灌進本機 SQL Server（建庫＋建表），不改動原始檔
#
# 這支腳本做什麼：把 db/*.sql 原樣複製到 deploy/dev/.local-ddl-output/*.local.sql，並（--apply 系列）灌進本機 SQL Server。
#
# 🔴 2026-10-13 起不再做任何型別轉換：使用者決定資料庫不使用 Azure SQL 原生 `json` 型別，JSON 內容欄位一律
#   nvarchar(max) ＋ 具名 CHECK（ISJSON）。DDL 本來就是本機 2022 與 Azure SQL 都吃得下的版本，
#   不再有「本機副本」與「正式 DDL」的差異（舊版曾把 12 個 json 欄位轉成 nvarchar(max)，導致本機與正式永遠不一致，
#   是 docs/18 E-111 的根因）。保留 *.local.sql 這個輸出路徑只是為了不動既有呼叫端（setup-club-db.sh、ci.yml 等）。
#
# 防呆：db/*.sql 若又出現 `json` 型別的欄位宣告，直接拒絕執行（見 reject_native_json）。
#
# 用法：
#   deploy/local-ddl.sh                          只複製出 SQL（原樣），印出結果路徑
#   deploy/local-ddl.sh --apply                  產生後，額外對本機 SQL Server instance 執行 sqlcmd
#                                                 建庫＋建表（tcrfc_club、tcrfc_charity 兩個）
#   deploy/local-ddl.sh --apply-club-db          只建立／灌 tcrfc_club（網站庫），資料庫已存在且已有
#                                                 資料表時略過 DDL（CREATE TABLE 不是冪等的，重灌會直接失敗）
#   deploy/local-ddl.sh --apply-club-db --recreate
#                                                 先 DROP（若存在）再 CREATE tcrfc_club，取得一個全新的
#                                                 資料庫再灌 DDL。一鍵版（另外呼叫種子腳本）見
#                                                 db/seed/setup-club-db.sh。
#
# ──────────────────────────────────────────────────────────────────────────────
# 🔴 安全模型：目標容器可設定，但目標資料庫名稱寫死白名單
#
# 本機開發資料庫建在使用者既有的 `sqlserver` 容器裡（本機另一個專案在用，見
# docker-compose.dev.yml、deploy/README.md），該 instance 裡還有大約 25 個屬於使用者其他專案
# 的資料庫。風險因此是「動到錯的資料庫」，防呆為：
#   - 目標容器名稱可由 LOCAL_MSSQL_CONTAINER 環境變數指定，預設 "sqlserver"。
#   - ⛔ 目標資料庫名稱寫死只允許 tcrfc_club、tcrfc_charity 兩個（見下方 ALLOWED_DATABASES），
#     任何呼叫路徑上出現這兩個名字以外的資料庫，一律拒絕執行。
#   - 一般建庫一律 `IF DB_ID(...) IS NULL CREATE DATABASE`。唯一會執行 DROP DATABASE 的路徑是
#     `--apply-club-db --recreate`，且只作用在 tcrfc_club（先過 assert_allowed_database）。
#   - 執行前先印出「即將操作哪個容器、哪個資料庫」，不悄悄動手。
#
# 🔴 2026-09-30 使用者裁決：本機只保留 tcrfc_club 與 tcrfc_charity 兩庫，舊的 tcrfc_club_dev、
# tcrfc_club_test、tcrfc_charity_dev 一律廢除（自動測試直接跑在 tcrfc_club 上，見
# docs/14-invariants.md「S0-13」）。原 --apply-club-db 更名為 --apply-club-db。
# ──────────────────────────────────────────────────────────────────────────────

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC_DIR="${REPO_ROOT}/db"
OUT_DIR="${REPO_ROOT}/deploy/dev/.local-ddl-output"

# 本機 SQL Server 所在的容器名稱：預設用既有的 sqlserver 容器，可用環境變數覆寫
# （例如指到別的本機 SQL Server 容器）。這不是白名單——白名單管的是「資料庫名稱」，見下方。
LOCAL_MSSQL_CONTAINER="${LOCAL_MSSQL_CONTAINER:-sqlserver}"

# ⛔ 資料庫名稱白名單。本腳本唯一允許建立／寫入的資料庫只有這兩個，不接受任何呼叫端覆寫。
ALLOWED_DATABASES=("tcrfc_club" "tcrfc_charity")

# 網站庫名稱——--apply-club-db 模式（含 --recreate 的 DROP）只作用在這個名字，
# 與 db/seed/setup-club-db.sh、db/seed/apply-seed.sh 的寫死名稱是同一個。
readonly CLUB_DATABASE_NAME="tcrfc_club"

assert_allowed_database() {
  local db="$1"
  for allowed in "${ALLOWED_DATABASES[@]}"; do
    if [[ "${db}" == "${allowed}" ]]; then
      return 0
    fi
  done
  echo "拒絕執行：資料庫名稱 '${db}' 不在允許清單內（${ALLOWED_DATABASES[*]}）。" >&2
  echo "本腳本只允許操作 TCRFC 這兩個本機庫，不得誤觸同一個 SQL Server instance 裡的其他資料庫。" >&2
  exit 1
}

MODE="${1:-}"
APPLY=0
APPLY_CLUB_DB=0
RECREATE_CLUB_DB=0
case "${MODE}" in
  --apply)
    APPLY=1
    ;;
  --apply-club-db)
    APPLY_CLUB_DB=1
    if [[ "${2:-}" == "--recreate" ]]; then
      RECREATE_CLUB_DB=1
    fi
    ;;
  "")
    ;;
  *)
    echo "不認得的參數：${MODE}（合法值：--apply、--apply-club-db [--recreate]，或不帶參數）" >&2
    exit 1
    ;;
esac

mkdir -p "${OUT_DIR}"

# 🔴 不得再出現原生 json 型別的欄位宣告（欄位名稱＋空白＋json＋空白＋NULL/NOT NULL）。註解裡的「json」字樣不算。
reject_native_json() {
  local src="$1"
  if grep -nE '^[[:space:]]+[a-z_0-9]+[[:space:]]+json([[:space:]]|,|$)' "${src}" >&2; then
    echo "拒絕執行：${src} 含原生 json 型別欄位（上列）。2026-10-13 起 JSON 內容欄位一律 nvarchar(max) ＋ 具名 CHECK（ISJSON）——見 docs/14、docs/17 §6 第 2 項。" >&2
    exit 1
  fi
}

copy_ddl() {
  local src="$1" dst="$2"
  reject_native_json "${src}"
  cp "${src}" "${dst}"
}

echo "==> 複製 db/club-schema.sql（原樣，不轉換）"
copy_ddl "${SRC_DIR}/club-schema.sql" "${OUT_DIR}/club-schema.local.sql"
echo "    → ${OUT_DIR}/club-schema.local.sql"

echo "==> 複製 db/charity-schema.sql（原樣，不轉換）"
copy_ddl "${SRC_DIR}/charity-schema.sql" "${OUT_DIR}/charity-schema.local.sql"
echo "    → ${OUT_DIR}/charity-schema.local.sql"

if [[ "${APPLY}" -eq 0 && "${APPLY_CLUB_DB}" -eq 0 ]]; then
  echo
  echo "只產生檔案，未執行。要灌進本機資料庫，加 --apply；" \
       "要建立／灌網站庫，加 --apply-club-db（見檔頭「用法」）。"
  exit 0
fi

echo
if [[ "${APPLY_CLUB_DB}" -eq 1 ]]; then
  echo "==> --apply-club-db：即將操作的目標"
  echo "    容器（docker container name）：${LOCAL_MSSQL_CONTAINER}"
  echo "    資料庫（僅這一個）：${CLUB_DATABASE_NAME}$( [[ "${RECREATE_CLUB_DB}" -eq 1 ]] && echo '（--recreate：先 DROP 再 CREATE）' )"
else
  echo "==> --apply：即將操作的目標"
  echo "    容器（docker container name）：${LOCAL_MSSQL_CONTAINER}"
  echo "    資料庫（僅這兩個，寫死白名單）：tcrfc_club tcrfc_charity"
fi
echo

# 依「docker container 名稱」（不是 compose service 名稱——這個容器不是本專案 compose 管理的）
# 精確比對容器名稱，避免子字串誤配到名稱相近的其他容器。
CONTAINER_ID="$(docker ps -q --filter "name=^/${LOCAL_MSSQL_CONTAINER}\$" || true)"
if [[ -z "${CONTAINER_ID}" ]]; then
  echo "找不到執行中的容器 '${LOCAL_MSSQL_CONTAINER}'。" >&2
  echo "若你要用的是既有的 sqlserver 容器，請確認它已在跑（docker ps）；" >&2
  echo "若容器名稱不同，設定環境變數 LOCAL_MSSQL_CONTAINER 指到正確的容器名稱。" >&2
  exit 1
fi

: "${MSSQL_DEV_SA_PASSWORD:?請設定 MSSQL_DEV_SA_PASSWORD（須與 '${LOCAL_MSSQL_CONTAINER}' 容器的 SA 密碼一致，見 .env）}"

run_sqlcmd() {
  local db="$1" sql_file="$2"
  assert_allowed_database "${db}"
  echo "    建庫（若不存在）與建表：${db}"
  # 只建立、不存在才建立；絕不 DROP／ALTER 這個 instance 上任何既有資料庫。
  docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
    -Q "IF DB_ID('${db}') IS NULL CREATE DATABASE [${db}];"
  docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C -d "${db}" \
    < "${sql_file}"
}

apply_club_db() {
  assert_allowed_database "${CLUB_DATABASE_NAME}"

  if [[ "${RECREATE_CLUB_DB}" -eq 1 ]]; then
    echo "    --recreate：先 DROP（若存在）再 CREATE，取得全新的資料庫"
    # ALTER ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE：踢掉任何還連著這個庫的既有連線
    # （例如上一輪 dotnet test 沒乾淨結束留下的連線），避免 DROP DATABASE 卡住或失敗。
    # 這一整條指令只點名 CLUB_DATABASE_NAME 這一個資料庫，assert_allowed_database 已先擋過
    # 這個變數不可能是白名單以外的名字。
    docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
      -Q "IF DB_ID('${CLUB_DATABASE_NAME}') IS NOT NULL BEGIN ALTER DATABASE [${CLUB_DATABASE_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${CLUB_DATABASE_NAME}]; END; CREATE DATABASE [${CLUB_DATABASE_NAME}];"
    NEEDS_DDL=1
  else
    echo "    建庫（若不存在）：${CLUB_DATABASE_NAME}"
    docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
      -Q "IF DB_ID('${CLUB_DATABASE_NAME}') IS NULL CREATE DATABASE [${CLUB_DATABASE_NAME}];"

    # CREATE TABLE 不是冪等的——已經建過表的資料庫再灌一次 DDL 會直接失敗。用資料表數量判斷
    # 這次是不是全新的資料庫，不是全新的就略過 DDL（呼叫端要重灌表結構，請加 --recreate）。
    local table_count
    table_count="$(docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C -d "${CLUB_DATABASE_NAME}" -h -1 -W \
      -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables;" | tr -d '[:space:]')"

    if [[ "${table_count}" == "0" ]]; then
      NEEDS_DDL=1
    else
      NEEDS_DDL=0
      echo "    資料庫已存在且已有 ${table_count} 張表，略過 DDL。要重建表結構請加 --recreate。"
    fi
  fi

  if [[ "${NEEDS_DDL}" -eq 1 ]]; then
    echo "    套用 DDL：${OUT_DIR}/club-schema.local.sql"
    docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C -d "${CLUB_DATABASE_NAME}" \
      < "${OUT_DIR}/club-schema.local.sql"
  fi
}

if [[ "${APPLY_CLUB_DB}" -eq 1 ]]; then
  apply_club_db
else
  run_sqlcmd "tcrfc_club" "${OUT_DIR}/club-schema.local.sql"
  run_sqlcmd "tcrfc_charity" "${OUT_DIR}/charity-schema.local.sql"
fi

echo
echo "完成。"
