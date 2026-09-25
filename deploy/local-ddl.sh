#!/usr/bin/env bash
# deploy/local-ddl.sh — 產生 db/*.sql 的「本機 SQL Server 2022 相容版」，不改動原始檔
#
# 為什麼需要這支腳本：
#   db/club-schema.sql、db/charity-schema.sql 用的是 Azure SQL 原生 `json` 型別
#   （docs/12-database-schema.md §1.4 第 2 件、docs/17-deployment.md §6）。
#   本機開發用的是一般 SQL Server 2022 容器，**SQL Server 2022 沒有原生 json 型別**，
#   直接執行原始 .sql 會建表失敗。
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
#   deploy/local-ddl.sh                          只產生轉換後的 SQL，印出結果路徑
#   deploy/local-ddl.sh --apply                  產生後，額外對本機 SQL Server instance 執行 sqlcmd
#                                                 建庫＋建表（tcrfc_club_dev、tcrfc_charity_dev 兩個）
#   deploy/local-ddl.sh --apply-test-db          只建立／灌 tcrfc_club_test（整合測試專用庫，
#                                                 S0-13），資料庫已存在且已有資料表時略過 DDL
#                                                 （CREATE TABLE 不是冪等的，重灌會直接失敗）
#   deploy/local-ddl.sh --apply-test-db --recreate
#                                                 先 DROP（若存在）再 CREATE tcrfc_club_test，
#                                                 取得一個全新的資料庫再灌 DDL——「從零建立測試庫」
#                                                 用這個。一鍵版見 db/seed/setup-test-db.sh
#                                                 （另外呼叫種子腳本）。
#
# ──────────────────────────────────────────────────────────────────────────────
# 🔴🔴 2026-09-21：安全模型變更——目標容器改為可設定，但目標資料庫名稱寫死白名單 🔴🔴
#
# 舊版本檔的防呆是「絕不碰名為 sqlserver 的既有容器」，因為當時本機開發資料庫跑在本專案
# 自己起的 mssql-dev 容器裡，風險是「接錯容器」。**使用者已明確拍板改用既有的 `sqlserver`
# 容器**（本機另一個專案在用，見 docker-compose.dev.yml、deploy/README.md）——本機開發資料庫
# 現在直接建在那個既有 instance 裡，不再另開容器。
#
# 風險性質因此改變：不再是「接錯容器」（容器本來就是刻意共用的），而是「動到錯的資料庫」——
# 那個既有 instance 裡還有大約 25 個屬於使用者其他專案的資料庫。防呆改成：
#   - 目標容器名稱可由 LOCAL_MSSQL_CONTAINER 環境變數指定，預設 "sqlserver"（不再寫死拒絕它）。
#   - ⛔ 目標資料庫名稱寫死只允許 tcrfc_club_dev、tcrfc_charity_dev 兩個（見下方 ALLOWED_DATABASES）。
#     任何呼叫路徑上出現這兩個名字以外的資料庫，一律拒絕執行並印出錯誤。
#   - 建庫一律 `IF DB_ID(...) IS NULL CREATE DATABASE`（本來就是），任何情況下都不對這個
#     instance 上「非本專案兩個庫」的既有資料庫下 DROP／ALTER——本腳本從頭到尾唯一會執行的
#     DDL 動作就是「建立這兩個資料庫（若不存在）」與「在這兩個資料庫裡跑 db/*.sql 的建表語句」，
#     不會、也沒有任何程式碼路徑可以碰到 sqlcmd 目標資料庫以外的任何資料庫物件。
#   - 執行前先印出「即將操作哪個容器、哪個資料庫」，不悄悄動手。
# ──────────────────────────────────────────────────────────────────────────────
#
# ──────────────────────────────────────────────────────────────────────────────
# 🔴 2026-09-25（S0-13）：白名單新增第三個資料庫 tcrfc_club_test，供整合測試專用
#
# 背景：`dotnet test` 與無頭瀏覽器實走原本共用 tcrfc_club_dev，同一天發生三次互相干擾
# （測試把實走中帳號的 2FA 狀態、settings 的 SEO 值重置回種子，見 docs/14-invariants.md、
# STATUS.md S0-13）。解法是讓整合測試改連一個完全獨立的資料庫，本機開發與無頭瀏覽器實走繼續
# 用 tcrfc_club_dev，兩者不再共用。
#
# 這個新資料庫一樣受本檔開頭的安全模型約束：**先確認名稱不存在的既有規則不變**（`IF DB_ID(...)
# IS NULL CREATE DATABASE`），差別只在 tcrfc_club_test 是「整合測試專屬、可以被完全重建」的資料
# 庫——`--recreate` 模式因此被允許對它、也僅對它執行 DROP DATABASE（一律先呼叫
# assert_allowed_database 確認目標就是白名單裡的名字，不會、也不可能被導向其他資料庫）。
# ──────────────────────────────────────────────────────────────────────────────

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC_DIR="${REPO_ROOT}/db"
OUT_DIR="${REPO_ROOT}/deploy/dev/.local-ddl-output"

# 本機 SQL Server 所在的容器名稱：預設用既有的 sqlserver 容器，可用環境變數覆寫
# （例如指到別的本機 SQL Server 容器）。這不是白名單——白名單管的是「資料庫名稱」，見下方。
LOCAL_MSSQL_CONTAINER="${LOCAL_MSSQL_CONTAINER:-sqlserver}"

# ⛔ 資料庫名稱白名單。本腳本唯一允許建立／寫入的資料庫只有這三個，不接受任何呼叫端覆寫。
# tcrfc_club_test 是 2026-09-25（S0-13）新增的整合測試專用庫，見上方說明。
ALLOWED_DATABASES=("tcrfc_club_dev" "tcrfc_charity_dev" "tcrfc_club_test")

# 整合測試專用庫的名稱——只在這裡定義一次，--apply-test-db 模式與 db/seed/setup-test-db.sh
# 都以這個名字為準（後者透過 SEED_TARGET_DATABASE 環境變數對齊，不是各自硬編碼一份）。
readonly TEST_DATABASE_NAME="tcrfc_club_test"

assert_allowed_database() {
  local db="$1"
  for allowed in "${ALLOWED_DATABASES[@]}"; do
    if [[ "${db}" == "${allowed}" ]]; then
      return 0
    fi
  done
  echo "拒絕執行：資料庫名稱 '${db}' 不在允許清單內（${ALLOWED_DATABASES[*]}）。" >&2
  echo "本腳本只允許操作 TCRFC 這兩個本機開發庫，不得誤觸同一個 SQL Server instance 裡的其他資料庫。" >&2
  exit 1
}

MODE="${1:-}"
APPLY=0
APPLY_TEST_DB=0
RECREATE_TEST_DB=0
case "${MODE}" in
  --apply)
    APPLY=1
    ;;
  --apply-test-db)
    APPLY_TEST_DB=1
    if [[ "${2:-}" == "--recreate" ]]; then
      RECREATE_TEST_DB=1
    fi
    ;;
  "")
    ;;
  *)
    echo "不認得的參數：${MODE}（合法值：--apply、--apply-test-db [--recreate]，或不帶參數）" >&2
    exit 1
    ;;
esac

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

if [[ "${APPLY}" -eq 0 && "${APPLY_TEST_DB}" -eq 0 ]]; then
  echo
  echo "只產生檔案，未執行。要灌進本機開發用資料庫，加 --apply；" \
       "要建立／灌整合測試專用庫，加 --apply-test-db（見檔頭「用法」）。"
  exit 0
fi

echo
if [[ "${APPLY_TEST_DB}" -eq 1 ]]; then
  echo "==> --apply-test-db：即將操作的目標"
  echo "    容器（docker container name）：${LOCAL_MSSQL_CONTAINER}"
  echo "    資料庫（僅這一個）：${TEST_DATABASE_NAME}$( [[ "${RECREATE_TEST_DB}" -eq 1 ]] && echo '（--recreate：先 DROP 再 CREATE）' )"
else
  echo "==> --apply：即將操作的目標"
  echo "    容器（docker container name）：${LOCAL_MSSQL_CONTAINER}"
  echo "    資料庫（僅這兩個，寫死白名單）：tcrfc_club_dev tcrfc_charity_dev"
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

apply_test_db() {
  assert_allowed_database "${TEST_DATABASE_NAME}"

  if [[ "${RECREATE_TEST_DB}" -eq 1 ]]; then
    echo "    --recreate：先 DROP（若存在）再 CREATE，取得全新的資料庫"
    # ALTER ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE：踢掉任何還連著這個庫的既有連線
    # （例如上一輪 dotnet test 沒乾淨結束留下的連線），避免 DROP DATABASE 卡住或失敗。
    # 這一整條指令只點名 TEST_DATABASE_NAME 這一個資料庫，assert_allowed_database 已先擋過
    # 這個變數不可能是白名單以外的名字。
    docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
      -Q "IF DB_ID('${TEST_DATABASE_NAME}') IS NOT NULL BEGIN ALTER DATABASE [${TEST_DATABASE_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${TEST_DATABASE_NAME}]; END; CREATE DATABASE [${TEST_DATABASE_NAME}];"
    NEEDS_DDL=1
  else
    echo "    建庫（若不存在）：${TEST_DATABASE_NAME}"
    docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
      -Q "IF DB_ID('${TEST_DATABASE_NAME}') IS NULL CREATE DATABASE [${TEST_DATABASE_NAME}];"

    # CREATE TABLE 不是冪等的——已經建過表的資料庫再灌一次 DDL 會直接失敗。用資料表數量判斷
    # 這次是不是全新的資料庫，不是全新的就略過 DDL（呼叫端要重灌表結構，請加 --recreate）。
    local table_count
    table_count="$(docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C -d "${TEST_DATABASE_NAME}" -h -1 -W \
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
      -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C -d "${TEST_DATABASE_NAME}" \
      < "${OUT_DIR}/club-schema.local.sql"
  fi
}

if [[ "${APPLY_TEST_DB}" -eq 1 ]]; then
  apply_test_db
else
  run_sqlcmd "tcrfc_club_dev" "${OUT_DIR}/club-schema.local.sql"
  run_sqlcmd "tcrfc_charity_dev" "${OUT_DIR}/charity-schema.local.sql"
fi

echo
echo "完成。"
