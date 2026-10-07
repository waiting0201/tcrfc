#!/usr/bin/env bash
# deploy/prod-db-init.sh — 正式庫（Azure SQL）首次初始化：建表 → 灌參照資料 → 標記 EF migration 已套用 → 建第一個管理員
#
# 在 VM 上以 runner 使用者執行（機密檔 /opt/tcrfc/secrets/*.env 權限 600、擁有者 runner；SQL 防火牆只放行 VM 子網，
# 從別處連不上）。完整步驟、為什麼這樣設計、失敗怎麼辦：docs/20-cicd.md §5「正式庫首次初始化」、infra/README.md §4.8。
#
# 子命令（每個命令一次只處理一個庫；club 與 charity 互不讀對方的設定檔，docs/17 §5）：
#   preflight  <club|charity>   唯讀：檢查工具、設定檔、連線、目標庫現況（empty／initialized／partial／other）
#   init       <club|charity>   建表 → 參照資料 → migration 歷史（最後一步＝完成標記）→ 驗證。
#                                目標庫必須「完全沒有使用者物件」，否則拒絕；執行前要手動輸入庫名確認。
#   create-admin <club|charity> 互動建立第一個管理員（密碼只在執行時由你輸入，不進命令列、不進 log）。
#                                僅在 admin_users 為空時可執行。
#   reset-password <club|charity> 重設既有管理員的密碼（登不進去時用）：列出帳號 → 輸入要重設的登入帳號 → 輸入庫名確認 →
#                                新密碼輸入兩次（不顯示、不進命令列與 log）→ 更新密碼、清除鎖定與失敗次數、撤銷該帳號所有登入狀態；
#                                可順便改登入帳號、啟用被停用的帳號。需要含 9 字元政策的 API 映像檔（先 push 並部署新版 api）。
#   verify     <club|charity>   唯讀：核對表／外鍵／視圖數、migration 歷史、參照資料筆數、沒有測試帳號。
#   wipe-partial <club|charity> 🔴 init 中途失敗（沒有 __EFMigrationsHistory 且 admin_users 為空）時，把半成品清乾淨以便重跑。
#                                危險指令：要輸入庫名與 WIPE 確認。
#
# 環境變數（皆有預設，通常不用動）：
#   TCRFC_SECRETS_DIR    預設 /opt/tcrfc/secrets（club.env、charity.env）
#   TCRFC_COMPOSE_ENV    預設 /opt/tcrfc/.env（只有 init club 讀：TCRFC_DOMAIN、BW_DOMAIN 寫入 clubs.domain）
#   MSSQL_TOOLS_IMAGE    預設 mcr.microsoft.com/mssql-tools（與 infra/provision-secrets.sh 驗證連線用的同一個映像檔）
#   API_IMAGE            預設 ghcr.io/waiting0201/tcrfc-api:master（create-admin 用它算 Argon2id 雜湊）
#
# 🔴 只給本機演練用的覆寫（必須同時設 PRODINIT_REHEARSAL=1；目標主機若是 *.database.windows.net 一律拒絕）：
#   PRODINIT_REHEARSAL=1  PRODINIT_TRUST_CERT=1（sqlcmd -C，本機容器是自簽憑證）
#   PRODINIT_EXPECT_DB_CLUB／PRODINIT_EXPECT_DB_CHARITY（演練庫名）
#   PRODINIT_DDL_DIR（含 club-schema.sql／charity-schema.sql 的目錄；本機 SQL Server 2022 沒有 json 型別，演練時放轉換過的副本）
#
# 安全模型：
#   - 連線資訊從 env 檔解析後以環境變數（SQLCMD*）傳進容器：`docker run -e NAME` 不帶值，密碼不會出現在 ps／命令列／輸出。
#   - 登入帳號是一般字串（可含中文；去前後空白、不得含空白、最長 64）。寫入 SQL 時一律轉成 UTF-16 十六進位字面值
#     （CAST(0x… AS nvarchar)），不會把帳號文字放進 SQL 字串，沒有引號／`$(` 注入問題，也不受 sqlcmd 編碼影響。
#   - 管理員密碼只經標準輸入（read -s → 管線 → `docker run -i`），雜湊以環境變數傳給 sqlcmd，SQL 檔裡只有 $(變數) 佔位。
#   - 正式模式只允許 *.database.windows.net，且資料庫名必須是 tcrfc_club／tcrfc_charity（防止把主站 DDL 灌進慈善庫）。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

SECRETS_DIR="${TCRFC_SECRETS_DIR:-/opt/tcrfc/secrets}"
COMPOSE_ENV="${TCRFC_COMPOSE_ENV:-/opt/tcrfc/.env}"
MSSQL_TOOLS_IMAGE="${MSSQL_TOOLS_IMAGE:-mcr.microsoft.com/mssql-tools}"
API_IMAGE="${API_IMAGE:-ghcr.io/waiting0201/tcrfc-api:master}"
REHEARSAL="${PRODINIT_REHEARSAL:-0}"
DDL_DIR="${PRODINIT_DDL_DIR:-${REPO_ROOT}/db}"

WORK_DIR=""
cleanup() {
  if [[ -n "${WORK_DIR}" && -d "${WORK_DIR}" ]]; then
    rm -rf "${WORK_DIR}"
  fi
}
trap cleanup EXIT

die() { echo "[錯] $*" >&2; exit 1; }
info() { echo "==> $*"; }
ok() { echo "   [OK] $*"; }
warn() { echo "   [注意] $*" >&2; }

usage() {
  sed -n '2,24p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit "${1:-1}"
}

# ── 目標庫設定（每個 target 只讀自己的檔案）──────────────────────────────────────
TARGET="" ENV_FILE="" CONN_KEY="" EXPECT_DB="" DDL_FILE="" REF_FILE="" MIGRATION_DIR="" SNAPSHOT_FILE=""
DB_SERVER="" DB_USER="" DB_PASSWORD="" DB_NAME=""

load_target() {
  TARGET="${1:-}"
  case "${TARGET}" in
    club)
      ENV_FILE="${SECRETS_DIR}/club.env"
      CONN_KEY="CLUB_SQL_CONNECTION_STRING"
      EXPECT_DB="${PRODINIT_EXPECT_DB_CLUB:-tcrfc_club}"
      DDL_FILE="${DDL_DIR}/club-schema.sql"
      REF_FILE="${REPO_ROOT}/db/prod/club-reference-data.sql"
      MIGRATION_DIR="${REPO_ROOT}/apps/api/Data/Migrations"
      SNAPSHOT_FILE="${MIGRATION_DIR}/ClubDbContextModelSnapshot.cs"
      ;;
    charity)
      ENV_FILE="${SECRETS_DIR}/charity.env"
      CONN_KEY="CHARITY_SQL_CONNECTION_STRING"
      EXPECT_DB="${PRODINIT_EXPECT_DB_CHARITY:-tcrfc_charity}"
      DDL_FILE="${DDL_DIR}/charity-schema.sql"
      REF_FILE="${REPO_ROOT}/db/prod/charity-reference-data.sql"
      MIGRATION_DIR="${REPO_ROOT}/apps/api/CharityPlatform/Data/Migrations"
      SNAPSHOT_FILE="${MIGRATION_DIR}/CharityDbContextModelSnapshot.cs"
      ;;
    *)
      die "目標只能是 club 或 charity（收到「${TARGET}」）"
      ;;
  esac

  if [[ "${REHEARSAL}" != "1" ]]; then
    if [[ "${EXPECT_DB}" != "tcrfc_club" && "${EXPECT_DB}" != "tcrfc_charity" ]] \
       || [[ -n "${PRODINIT_EXPECT_DB_CLUB:-}${PRODINIT_EXPECT_DB_CHARITY:-}${PRODINIT_DDL_DIR:-}${PRODINIT_TRUST_CERT:-}" ]]; then
      die "演練用覆寫（PRODINIT_EXPECT_DB_*／PRODINIT_DDL_DIR／PRODINIT_TRUST_CERT）只能搭配 PRODINIT_REHEARSAL=1"
    fi
  fi

  [[ -f "${DDL_FILE}" ]] || die "找不到 DDL：${DDL_FILE}"
  [[ -f "${REF_FILE}" ]] || die "找不到參照資料 SQL：${REF_FILE}（先在 repo 根目錄執行 python3 db/seed/generate-prod-reference-sql.py）"
  [[ -d "${MIGRATION_DIR}" ]] || die "找不到 migration 目錄：${MIGRATION_DIR}"
  [[ -f "${SNAPSHOT_FILE}" ]] || die "找不到 ModelSnapshot：${SNAPSHOT_FILE}"
  [[ -r "${ENV_FILE}" ]] || die "讀不到 ${ENV_FILE}（請以擁有者 runner 執行：sudo -iu runner；檔案權限應為 600）"

  parse_connection_string
}

# 從 env 檔取值（不 source，避免執行檔案內容）；值可能被單引號包住
env_get() { # ${1}=檔案 ${2}=鍵
  local line
  line="$(awk -v k="${2}" 'index($0, k "=") == 1 { print substr($0, length(k) + 2); exit }' "${1}")"
  line="${line#\'}"
  line="${line%\'}"
  printf '%s' "${line}"
}

parse_connection_string() {
  local conn part
  local -a parts
  conn="$(env_get "${ENV_FILE}" "${CONN_KEY}")"
  [[ -n "${conn}" ]] || die "${ENV_FILE} 沒有 ${CONN_KEY}"
  DB_SERVER="" DB_USER="" DB_PASSWORD="" DB_NAME=""
  IFS=';' read -ra parts <<<"${conn}"
  for part in "${parts[@]}"; do
    part="${part#"${part%%[![:space:]]*}"}"
    case "${part}" in
      Server=*) DB_SERVER="${part#Server=}" ;;
      "Data Source="*) DB_SERVER="${part#Data Source=}" ;;
      "User ID="*) DB_USER="${part#User ID=}" ;;
      Password=*) DB_PASSWORD="${part#Password=}" ;;
      Database=*) DB_NAME="${part#Database=}" ;;
      "Initial Catalog="*) DB_NAME="${part#Initial Catalog=}" ;;
      *) ;;
    esac
  done
  [[ -n "${DB_SERVER}" && -n "${DB_USER}" && -n "${DB_PASSWORD}" && -n "${DB_NAME}" ]] \
    || die "${CONN_KEY} 解析不出 Server／User ID／Password／Database"

  local host="${DB_SERVER#tcp:}"
  host="${host%%,*}"
  if [[ "${REHEARSAL}" == "1" ]]; then
    [[ "${host}" != *.database.windows.net ]] || die "PRODINIT_REHEARSAL=1 時拒絕連到 Azure SQL（${host}）——演練只能對本機容器"
  else
    [[ "${host}" == *.database.windows.net ]] || die "正式模式只允許 *.database.windows.net（目前是 ${host}）"
  fi
  [[ "${DB_NAME}" == "${EXPECT_DB}" ]] \
    || die "${CONN_KEY} 的資料庫是「${DB_NAME}」，${TARGET} 目標應為「${EXPECT_DB}」——拒絕（防止把 ${TARGET} 的 DDL 灌進別的庫）"
}

# ── sqlcmd（在容器內執行；連線資訊走環境變數）────────────────────────────────────
# 注意：不加 `docker run -i`——sqlcmd 只用 -Q／-i 檔案，不需要 stdin；加了會把終端機（或管線）的輸入吃掉，
# 後面的 read（確認字串、密碼）就讀不到。
sqlcmd_container() { # 參數：額外的 -v 掛載與 -e 選項請用 SQLCMD_DOCKER_EXTRA 陣列；其餘直接給 sqlcmd
  local trust=()
  if [[ "${PRODINIT_TRUST_CERT:-0}" == "1" ]]; then
    trust=(-C)
  fi
  SQLCMDSERVER="${DB_SERVER}" SQLCMDUSER="${DB_USER}" SQLCMDPASSWORD="${DB_PASSWORD}" SQLCMDDBNAME="${DB_NAME}" \
    docker run --rm \
      -e SQLCMDSERVER -e SQLCMDUSER -e SQLCMDPASSWORD -e SQLCMDDBNAME \
      ${SQLCMD_DOCKER_EXTRA[@]+"${SQLCMD_DOCKER_EXTRA[@]}"} \
      "${MSSQL_TOOLS_IMAGE}" /opt/mssql-tools/bin/sqlcmd -N ${trust[@]+"${trust[@]}"} -l 30 -b "$@"
}
SQLCMD_DOCKER_EXTRA=()

# 回傳單一純量（去除空白）。查詢一律自帶 SET NOCOUNT ON。
sql_scalar() { # ${1}=T-SQL
  local out
  out="$(sqlcmd_container -h -1 -W -Q "SET NOCOUNT ON; ${1}")" || die "查詢失敗：${1}"
  printf '%s' "${out}" | tr -d '[:space:]'
}

# 🔴 sqlcmd 13（mssql-tools 映像檔）遇到部分錯誤會「印出錯誤、結束碼卻是 0」（E-295：SqlState 24000 Invalid cursor state，
# 連線中斷、交易回滾，結束碼仍 0）。所以除了結束碼，還要掃輸出：Level 11 以上的 Msg（SQL 錯誤）、SqlState（ODBC 錯誤）、
# `Sqlcmd:` 開頭（sqlcmd 自己的錯誤）一律視為失敗。Level 10 以下是資訊訊息，不算。
sqlcmd_log_has_error() { # ${1}=輸出檔；有錯誤字樣回傳 0（真）
  grep -Eq '^(Msg [0-9]+, Level (1[1-9]|2[0-5])|SqlState |Sqlcmd: )' "${1}"
}

# 執行 SQL 檔；成功只印結尾摘要，失敗印 sqlcmd 的訊息（不含密碼）。${1}=容器內路徑 ${2}=標題 ${3}=失敗時的補充說明（選填）
run_sql_file() {
  local file_in_container="${1}" title="${2}" hint="${3:-}" log
  log="${WORK_DIR}/sqlcmd.$$.log"
  info "${title}"
  if sqlcmd_container -i "${file_in_container}" >"${log}" 2>&1 && ! sqlcmd_log_has_error "${log}"; then
    ok "完成（sqlcmd 輸出 $(wc -l <"${log}" | tr -d ' ') 行）"
  else
    echo "   sqlcmd 輸出最後 30 行：" >&2
    tail -n 30 "${log}" | sed 's/^/     /' >&2
    die "${title} 失敗。${hint:-這個庫現在可能是半成品——先看上面的錯誤，確認後用 wipe-partial 清掉再重跑（見 infra/README.md §4.8）}"
  fi
}

# ── 狀態 ───────────────────────────────────────────────────────────────────────
USER_OBJECTS_SQL="SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped = 0 AND type IN ('U','V','P','FN','IF','TF','TR','SO')"

db_state() { # 印出 empty／initialized／partial／other
  local objects history
  objects="$(sql_scalar "${USER_OBJECTS_SQL}")"
  history="$(sql_scalar "SELECT COUNT(*) FROM sys.tables WHERE name = '__EFMigrationsHistory'")"
  if [[ "${objects}" == "0" ]]; then
    echo "empty"
  elif [[ "${history}" == "1" ]]; then
    echo "initialized"
  else
    echo "partial"
  fi
}

migration_ids() { # 以 Designer 的 [Migration("…")] 為準（檔名只是慣例）
  grep -hoE '\[Migration\("[0-9]{14}_[A-Za-z0-9_]+"\)\]' "${MIGRATION_DIR}"/*.Designer.cs \
    | sed -E 's/^\[Migration\("(.*)"\)\]$/\1/' | sort
}

product_version() { # 取自 ModelSnapshot（與 EF 執行期套件版本一致）
  grep -oE 'HasAnnotation\("ProductVersion", "[^"]+"\)' "${SNAPSHOT_FILE}" | head -n 1 | sed -E 's/.*"ProductVersion", "([^"]+)".*/\1/'
}

strip_sql_comments() { perl -0777 -pe 's{/\*.*?\*/}{}gs; s{--[^\n]*}{}g' "${1}"; }

expected_tables() { strip_sql_comments "${DDL_FILE}" | grep -ciE '\bCREATE[[:space:]]+TABLE\b' || true; }
expected_fks() { strip_sql_comments "${DDL_FILE}" | grep -oiE 'FOREIGN[[:space:]]+KEY' | wc -l | tr -d ' '; }
expected_views() { strip_sql_comments "${DDL_FILE}" | grep -ciE '\bCREATE[[:space:]]+VIEW\b' || true; }

confirm() { # ${1}=必須輸入的字串
  local answer
  printf '   請輸入「%s」確認（其他任何輸入都會中止）：' "${1}" >&2
  read -r answer || answer=""
  [[ "${answer}" == "${1}" ]] || die "確認字串不符，已中止，沒有寫入任何東西"
}

require_tools() {
  command -v docker >/dev/null 2>&1 || die "找不到 docker"
  command -v perl >/dev/null 2>&1 || die "找不到 perl（用來去除 SQL 註解後數 CREATE TABLE）"
  docker info >/dev/null 2>&1 || die "docker 無法使用（目前使用者要在 docker 群組；runner 使用者已是）"
  WORK_DIR="$(mktemp -d)"
  chmod 700 "${WORK_DIR}"
}

# ── 子命令 ─────────────────────────────────────────────────────────────────────
cmd_preflight() {
  load_target "${1:-}"
  require_tools
  info "preflight：${TARGET}（唯讀）"
  ok "設定檔 ${ENV_FILE}（${CONN_KEY} 可解析；目標庫 ${DB_NAME}）"
  SQLCMD_DOCKER_EXTRA=()
  local who state
  who="$(sql_scalar "SELECT DB_NAME()")"
  [[ "${who}" == "${DB_NAME}" ]] || die "連上後 DB_NAME() 是 ${who}，與設定不符"
  ok "連線成功，DB_NAME() = ${who}"
  state="$(db_state)"
  ok "目標庫狀態：${state}"
  case "${state}" in
    empty) echo "   → 可以執行：init ${TARGET}" ;;
    initialized) echo "   → 已初始化。要檢查請執行：verify ${TARGET}" ;;
    partial) echo "   → 有物件但沒有 __EFMigrationsHistory＝半成品或別人建的表。init 會拒絕；確認是上次 init 中斷後用 wipe-partial" ;;
    *) echo "   → 不明狀態" ;;
  esac
  echo "   DDL：$(expected_tables) 張表／$(expected_fks) 個外鍵／$(expected_views) 個視圖；migration $(migration_ids | wc -l | tr -d ' ') 支；ProductVersion $(product_version)"
  if [[ "${TARGET}" == "club" ]]; then
    [[ -r "${COMPOSE_ENV}" ]] && ok "compose 設定 ${COMPOSE_ENV} 可讀（init club 會用其中的 TCRFC_DOMAIN／BW_DOMAIN）" \
      || warn "讀不到 ${COMPOSE_ENV}；init club 需要它"
  fi
}

cmd_init() {
  load_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()

  local domain_tcrfc="" domain_bw="" domain_re='^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$'
  if [[ "${TARGET}" == "club" ]]; then
    [[ -r "${COMPOSE_ENV}" ]] || die "讀不到 ${COMPOSE_ENV}（club 的 clubs.domain 取自其中的 TCRFC_DOMAIN／BW_DOMAIN）"
    domain_tcrfc="$(env_get "${COMPOSE_ENV}" TCRFC_DOMAIN)"
    domain_bw="$(env_get "${COMPOSE_ENV}" BW_DOMAIN)"
    [[ "${domain_tcrfc}" =~ ${domain_re} ]] || die "TCRFC_DOMAIN 不是合法網域：「${domain_tcrfc}」"
    [[ "${domain_bw}" =~ ${domain_re} ]] || die "BW_DOMAIN 不是合法網域：「${domain_bw}」"
    [[ "${domain_tcrfc}" != "${domain_bw}" ]] || die "TCRFC_DOMAIN 與 BW_DOMAIN 相同（clubs.domain 有唯一限制）"
  fi

  local state
  state="$(db_state)"
  [[ "${state}" == "empty" ]] || die "目標庫 ${DB_NAME} 不是空的（狀態 ${state}）。init 只在完全沒有使用者物件的庫上執行，拒絕。"

  local ids version n_ids exp_tables exp_fks exp_views
  ids="$(migration_ids)"
  n_ids="$(printf '%s\n' "${ids}" | grep -c . || true)"
  version="$(product_version)"
  [[ "${n_ids}" -ge 1 ]] || die "找不到任何 migration"
  [[ -n "${version}" ]] || die "從 ${SNAPSHOT_FILE} 取不到 ProductVersion"
  exp_tables="$(expected_tables)"; exp_fks="$(expected_fks)"; exp_views="$(expected_views)"

  info "即將在 ${DB_NAME}（空庫）執行："
  echo "   1. 建表：$(basename "${DDL_FILE}")（${exp_tables} 表／${exp_fks} 外鍵／${exp_views} 視圖，原樣、不轉換型別）"
  echo "   2. 參照資料：$(basename "${REF_FILE}")"
  if [[ "${TARGET}" == "club" ]]; then
    echo "      clubs.domain：tcrfc → ${domain_tcrfc}，bw → ${domain_bw}（取自 ${COMPOSE_ENV}）"
  fi
  echo "   3. __EFMigrationsHistory：${n_ids} 筆（ProductVersion ${version}）——最後一步，也是「初始化完成」的標記"
  confirm "INIT ${DB_NAME}"

  # 步驟 1：DDL
  SQLCMD_DOCKER_EXTRA=(-v "$(dirname "${DDL_FILE}"):/work/ddl:ro")
  run_sql_file "/work/ddl/$(basename "${DDL_FILE}")" "步驟 1/3：建表（${DB_NAME}）"

  # 步驟 2：參照資料
  SQLCMD_DOCKER_EXTRA=(-v "$(dirname "${REF_FILE}"):/work/ref:ro")
  if [[ "${TARGET}" == "club" ]]; then
    SQLCMD_DOCKER_EXTRA+=(-e CLUB_DOMAIN_TCRFC -e CLUB_DOMAIN_BW)
    CLUB_DOMAIN_TCRFC="${domain_tcrfc}" CLUB_DOMAIN_BW="${domain_bw}" \
      run_sql_file "/work/ref/$(basename "${REF_FILE}")" "步驟 2/3：灌參照資料（${DB_NAME}）"
  else
    run_sql_file "/work/ref/$(basename "${REF_FILE}")" "步驟 2/3：灌參照資料（${DB_NAME}）"
  fi

  # 步驟 3：migration 歷史（與 EF 自己建的 __EFMigrationsHistory 同結構，之後 dotnet ef database update 視為已套用）
  local hist_sql="${WORK_DIR}/history.sql" id first=1
  {
    echo "SET XACT_ABORT ON;"
    echo "GO"
    echo "BEGIN TRANSACTION;"
    echo "CREATE TABLE [__EFMigrationsHistory] ("
    echo "    [MigrationId] nvarchar(150) NOT NULL,"
    echo "    [ProductVersion] nvarchar(32) NOT NULL,"
    echo "    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])"
    echo ");"
    echo "INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES"
    while IFS= read -r id; do
      [[ -n "${id}" ]] || continue
      if [[ "${first}" -eq 1 ]]; then first=0; else echo ","; fi
      printf "    (N'%s', N'%s')" "${id}" "${version}"
    done <<<"${ids}"
    echo ";"
    echo "COMMIT TRANSACTION;"
    echo "GO"
  } >"${hist_sql}"
  SQLCMD_DOCKER_EXTRA=(-v "${WORK_DIR}:/work/hist:ro")
  run_sql_file "/work/hist/history.sql" "步驟 3/3：寫入 __EFMigrationsHistory（${DB_NAME}）"

  cmd_verify_loaded
  echo
  info "${DB_NAME} 初始化完成。"
  if [[ "${TARGET}" == "club" ]]; then
    echo "   下一步：create-admin club（建立第一個管理員），再依 infra/README.md §4.8 起容器。"
  else
    echo "   下一步：create-admin charity（慈善後台是獨立帳號體系，由協會指定的人建立）。"
  fi
}

# 已載入 target 之後的驗證（init 結尾與 verify 共用）
cmd_verify_loaded() {
  SQLCMD_DOCKER_EXTRA=()
  local failures=0
  info "驗證：${DB_NAME}"

  local want_t want_f want_v got_t got_f got_v
  want_t="$(expected_tables)"; want_f="$(expected_fks)"; want_v="$(expected_views)"
  got_t="$(sql_scalar "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> '__EFMigrationsHistory'")"
  got_f="$(sql_scalar "SELECT COUNT(*) FROM sys.foreign_keys")"
  got_v="$(sql_scalar "SELECT COUNT(*) FROM sys.views WHERE is_ms_shipped = 0")"
  check_eq "資料表（不含 __EFMigrationsHistory）" "${want_t}" "${got_t}" || failures=$((failures + 1))
  check_eq "外鍵" "${want_f}" "${got_f}" || failures=$((failures + 1))
  check_eq "視圖" "${want_v}" "${got_v}" || failures=$((failures + 1))
  ok "CHECK 約束 $(sql_scalar "SELECT COUNT(*) FROM sys.check_constraints")、索引 $(sql_scalar "SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE i.type > 0")（僅供參考，不比對）"

  local want_h got_h missing
  want_h="$(migration_ids | grep -c . || true)"
  got_h="$(sql_scalar "SELECT COUNT(*) FROM [__EFMigrationsHistory]")"
  check_eq "__EFMigrationsHistory 筆數" "${want_h}" "${got_h}" || failures=$((failures + 1))
  missing="$(sql_scalar "SELECT COUNT(*) FROM [__EFMigrationsHistory] WHERE [ProductVersion] <> N'$(product_version)'")"
  check_eq "ProductVersion 不是 $(product_version) 的列" "0" "${missing}" || failures=$((failures + 1))

  # 參照資料筆數：以 SQL 檔頭的 `-- MANIFEST table=n` 為準
  local table want got line
  while IFS= read -r line; do
    table="${line%%=*}"
    want="${line##*=}"
    [[ "${table}" =~ ^[a-z_0-9]+$ ]] || die "MANIFEST 表名不合法：${table}"
    got="$(sql_scalar "SELECT COUNT_BIG(*) FROM [${table}]")"
    check_eq "參照資料 ${table}" "${want}" "${got}" || failures=$((failures + 1))
  done < <(grep -E '^-- MANIFEST ' "${REF_FILE}" | sed -E 's/^-- MANIFEST //')

  # 中文是否被原樣寫入（sqlcmd 13 沒有 -f 選項，靠容器對 UTF-8 檔案的預設行為）：zh-Hant 語系名稱第一個字是「繁」(U+7E41 = 32321)
  local zh_col="name"
  [[ "${TARGET}" == "charity" ]] && zh_col="name_zh"
  check_eq "中文編碼（locales.${zh_col} 首字碼位，繁＝32321）" "32321" \
    "$(sql_scalar "SELECT ISNULL(MAX(UNICODE(LEFT(${zh_col}, 1))), 0) FROM locales WHERE code = N'zh-Hant'")" || failures=$((failures + 1))

  # 不得有測試帳號：*.test／*.local（sa@system.local 例外，2026-10-03 使用者裁決）命名，或任何帳號的密碼雜湊仍等於種子雜湊
  local bad
  bad="$(sql_scalar "SELECT COUNT(*) FROM admin_users WHERE (username LIKE N'%.test' OR username LIKE N'%.local') AND username <> N'sa@system.local'")"
  check_eq "測試帳號命名（*.test／*.local，sa@system.local 除外）" "0" "${bad}" || failures=$((failures + 1))
  bad="$(sql_scalar "SELECT COUNT(*) FROM admin_users WHERE password_hash IN ($(seed_password_hashes))")"
  check_eq "密碼雜湊等於種子雜湊的帳號（種子帳號誤灌，或沿用 Admin@123 等種子密碼）" "0" "${bad}" || failures=$((failures + 1))
  ok "admin_users 筆數：$(sql_scalar "SELECT COUNT(*) FROM admin_users")"

  if [[ "${TARGET}" == "club" ]]; then
    local invalid
    invalid="$(sql_scalar "SELECT COUNT(*) FROM clubs WHERE domain LIKE N'%.invalid'")"
    check_eq "clubs.domain 仍是佔位值" "0" "${invalid}" || failures=$((failures + 1))
  fi

  [[ "${failures}" -eq 0 ]] || die "驗證有 ${failures} 項失敗（上面標 [不符] 者）"
  ok "全部驗證通過"
}

check_eq() { # ${1}=名稱 ${2}=預期 ${3}=實際
  if [[ "${2}" == "${3}" ]]; then
    ok "${1}：${3}"
    return 0
  fi
  echo "   [不符] ${1}：預期 ${2}，實際 ${3}" >&2
  return 1
}

cmd_verify() {
  load_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()
  local state
  state="$(db_state)"
  [[ "${state}" == "initialized" ]] || die "${DB_NAME} 狀態是 ${state}，不是 initialized（沒有 __EFMigrationsHistory）"
  cmd_verify_loaded
}

# ── 登入帳號處理（一般字串；規則同 API AdminAccountsRepository.ValidateUsername）────────────
# 帳號以 UTF-16LE 十六進位傳入 SQL：長度以 UTF-16 單位計（與 .NET string.Length、nvarchar 一致，不受 shell locale 影響），
# 且文字本身不進 SQL 字串（無注入面）。
MIN_PASSWORD_LENGTH=9

trim_ws() { # ${1}=字串；去前後空白
  local v="${1}"
  v="${v#"${v%%[![:space:]]*}"}"
  v="${v%"${v##*[![:space:]]}"}"
  printf '%s' "${v}"
}

utf16le_hex() { # ${1}=字串；印出不含 0x 的十六進位；不是合法 UTF-8 時回傳非 0
  local out
  out="$(printf '%s' "${1}" | iconv -f UTF-8 -t UTF-16LE 2>/dev/null | od -An -v -tx1 | tr -d ' \n')" || return 1
  [[ -n "${out}" ]] || return 1
  printf '%s' "${out}"
}

# 查既有帳號用的寬鬆檢查（舊帳號可能不符新規則）：非空、合法 UTF-8、無控制字元、長度 ≤ 191（慈善庫上限）
username_lookup_hex() { # ${1}=已 trim 的帳號 → 印 0x… 字面值
  local u="${1}" hex
  [[ -n "${u}" ]] || die "帳號不可為空"
  [[ ! "${u}" =~ [[:cntrl:]] ]] || die "帳號含控制字元"
  hex="$(utf16le_hex "${u}")" || die "帳號不是合法的 UTF-8 文字"
  [[ "${hex}" =~ ^[0-9a-f]+$ ]] || die "帳號編碼失敗"
  (( ${#hex} / 4 <= 191 )) || die "帳號長度超過上限"
  printf '0x%s' "${hex}"
}

# 新帳號的嚴格規則：非空、≤ 64、不含任何空白字元；中文等 Unicode 字元皆可
username_strict_hex() { # ${1}=已 trim 的帳號 → 印 0x… 字面值
  local u="${1}" hex
  [[ ! "${u}" =~ [[:space:]] ]] || die "帳號不得含空白字元"
  hex="$(username_lookup_hex "${u}")"
  (( (${#hex} - 2) / 4 <= 64 )) || die "帳號長度不得超過 64 個字元"
  printf '%s' "${hex}"
}

reject_test_username() { # 正式庫不接受測試帳號的命名
  # 2026-10-03 使用者裁決：開放 sa@system.local 當正式管理員帳號名（它也是種子超管的名字、公開 repo 看得到；
  # 密碼必須是重新設定的）。種子帳號誤灌的偵測因此改看「密碼雜湊是否等於種子雜湊」，見 cmd_verify 與 seed_password_hashes。
  local lower
  lower="$(printf '%s' "${1}" | tr '[:upper:]' '[:lower:]')"
  [[ "${lower}" == "sa@system.local" ]] && return 0
  case "${lower}" in
    *.test|*.local|*@example.*) die "這是測試帳號的命名，正式庫不接受" ;;
    *) ;;
  esac
}

seed_password_hashes() { # 以 SQL IN 清單輸出 db/seed 產生器裡寫死的全部 Argon2id 種子雜湊（種子帳號誤灌的偵測依據）
  local list
  list="$(grep -ohE '\$argon2id\$[^"'"'"' ]+' "${REPO_ROOT}"/db/seed/generate-*-seed-sql.py | sort -u | sed "s/.*/N'&'/" | paste -sd, -)"
  [[ -n "${list}" ]] || die "從 db/seed/generate-*-seed-sql.py 讀不到任何種子雜湊，無法檢查種子帳號"
  printf '%s' "${list}"
}

read_password_twice() { # ${1}=不得與之相同的帳號；結果放在全域 NEW_PASSWORD
  local pw1 pw2
  printf '   密碼（至少 %s 個字元，輸入時不顯示）：' "${MIN_PASSWORD_LENGTH}" >&2
  read -rs pw1
  echo >&2
  printf '   再輸入一次：' >&2
  read -rs pw2
  echo >&2
  [[ "${pw1}" == "${pw2}" ]] || die "兩次輸入不一致"
  # 這裡的長度依 shell locale 計算（C locale 以位元組計，會偏鬆）；最終以 API 映像檔 --hash-password 的政策檢查為準
  [[ "${#pw1}" -ge "${MIN_PASSWORD_LENGTH}" ]] || die "密碼至少 ${MIN_PASSWORD_LENGTH} 個字元"
  [[ "$(printf '%s' "${pw1}" | tr '[:upper:]' '[:lower:]')" != "$(printf '%s' "${1}" | tr '[:upper:]' '[:lower:]')" ]] || die "密碼不得與帳號相同"
  NEW_PASSWORD="${pw1}"
}

hash_password() { # 從全域 NEW_PASSWORD 算 Argon2id 雜湊 → 放在全域 NEW_HASH，並清掉 NEW_PASSWORD
  info "以 API 映像檔計算 Argon2id 雜湊（${API_IMAGE}；與登入驗證同一份程式）"
  NEW_HASH="$(printf '%s\n' "${NEW_PASSWORD}" | docker run --rm -i "${API_IMAGE}" --hash-password)" \
    || die "雜湊失敗（映像檔是否為含 --hash-password 且接受 ${MIN_PASSWORD_LENGTH} 字元的版本？要先 push 並部署新版 api 映像檔）"
  NEW_PASSWORD=""
  [[ "${NEW_HASH}" == '$argon2id$v=19$'* && "${NEW_HASH}" != *$'\n'* ]] || die "雜湊輸出格式不符，已中止"
}

cmd_create_admin() {
  load_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()

  local state
  state="$(db_state)"
  [[ "${state}" == "initialized" ]] || die "${DB_NAME} 狀態是 ${state}；請先 init ${TARGET}"
  [[ "$(sql_scalar "SELECT COUNT(*) FROM admin_users")" == "0" ]] \
    || die "admin_users 不是空的。第一個管理員只在空表時建立；之後的帳號請在後台「帳號與角色」新增"
  [[ "$(sql_scalar "SELECT COUNT(*) FROM admin_roles WHERE code = N'system_admin'")" == "1" ]] \
    || die "找不到 system_admin 角色（參照資料沒灌好？）"

  local username username_hex display_name email
  info "建立 ${DB_NAME} 的第一個管理員（系統管理員，可存取全部俱樂部；密碼只在這裡輸入，不會顯示、不會寫進任何檔案或 log）"
  printf '   登入帳號（一般字串，可用中文；不得含空白，最長 64 字元）：' >&2
  read -r username
  username="$(trim_ws "${username}")"
  username_hex="$(username_strict_hex "${username}")"
  reject_test_username "${username}"
  printf '   顯示名稱（最多 64 字元，不可含單引號）：' >&2
  read -r display_name
  [[ -n "${display_name}" && "${#display_name}" -le 64 && "${display_name}" != *"'"* && "${display_name}" != *'$('* ]] || die "顯示名稱不合"
  printf '   Email（可留空）：' >&2
  read -r email
  if [[ -n "${email}" ]]; then
    [[ "${email}" =~ ^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$ && "${#email}" -le 255 ]] || die "Email 格式不合"
  fi
  # sqlcmd 把「值為空的環境變數」視為未定義，所以用 - 代表沒有 Email（SQL 端 NULLIF 轉回 NULL）
  [[ -n "${email}" ]] || email="-"
  read_password_twice "${username}"
  hash_password
  local hash="${NEW_HASH}"

  local sql_file="${WORK_DIR}/create-admin.sql"
  cat >"${sql_file}" <<'SQL'
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM admin_users) THROW 50001, N'admin_users 不是空的，拒絕建立第一個管理員', 1;
DECLARE @role_id uniqueidentifier = (SELECT id FROM admin_roles WHERE code = N'system_admin');
IF @role_id IS NULL THROW 50002, N'找不到 system_admin 角色', 1;
DECLARE @id uniqueidentifier = NEWID();
INSERT INTO admin_users (id, username, display_name, email, password_hash, must_change_password, password_changed_at,
                         is_super_admin, two_factor_enabled, status)
VALUES (@id, CAST($(PRODINIT_ADMIN_USERNAME_HEX) AS nvarchar(64)), N'$(PRODINIT_ADMIN_DISPLAY)', NULLIF(N'$(PRODINIT_ADMIN_EMAIL)', N'-'),
        N'$(PRODINIT_ADMIN_HASH)', 0, SYSUTCDATETIME(), 1, 0, N'active');
INSERT INTO admin_user_roles (admin_user_id, admin_role_id) VALUES (@id, @role_id);
COMMIT TRANSACTION;
GO
SQL
  SQLCMD_DOCKER_EXTRA=(-v "${WORK_DIR}:/work/admin:ro" -e PRODINIT_ADMIN_USERNAME_HEX -e PRODINIT_ADMIN_DISPLAY -e PRODINIT_ADMIN_EMAIL -e PRODINIT_ADMIN_HASH)
  PRODINIT_ADMIN_USERNAME_HEX="${username_hex}" PRODINIT_ADMIN_DISPLAY="${display_name}" PRODINIT_ADMIN_EMAIL="${email}" PRODINIT_ADMIN_HASH="${hash}" \
    run_sql_file "/work/admin/create-admin.sql" "寫入管理員（${DB_NAME}）" "管理員沒有建立（整段在同一個交易內，已回滾）；修正後可直接重跑 create-admin ${TARGET}。"
  hash=""

  SQLCMD_DOCKER_EXTRA=()
  check_eq "admin_users 筆數" "1" "$(sql_scalar "SELECT COUNT(*) FROM admin_users")" || die "建立後筆數不符"
  check_eq "帳號寫入後與輸入一致（UTF-16 往返）" "1" "$(sql_scalar "SELECT COUNT(*) FROM admin_users WHERE username = CAST(${username_hex} AS nvarchar(64))")" || die "帳號寫入後與輸入不一致"
  echo
  info "完成。請用剛才的帳號與密碼登入後台（兩階段驗證目前後台不提供設定入口，docs/14；密碼請存進密碼管理器，忘記只能由資料庫端重建）。"
  if [[ "${TARGET}" == "club" ]]; then
    echo "   這個帳號是系統管理員（is_super_admin）：不需要另外授權俱樂部，就能管理兩個俱樂部。"
  else
    echo "   這個帳號是慈善後台的系統管理員（與主站後台是完全獨立的帳號體系）。"
  fi
}

cmd_reset_password() {
  load_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()

  local state
  state="$(db_state)"
  [[ "${state}" == "initialized" ]] || die "${DB_NAME} 狀態是 ${state}；請先 init ${TARGET}"

  info "${DB_NAME} 的管理員帳號（不顯示密碼雜湊）"
  sqlcmd_container -W -s' | ' -Q "SET NOCOUNT ON; SELECT username, display_name, status, CASE WHEN locked_until > SYSUTCDATETIME() THEN 'locked' ELSE '-' END AS locked, failed_attempt_count AS failed FROM admin_users ORDER BY username" \
    || die "列出帳號失敗"
  echo >&2

  local username target_hex new_username="" new_hex="NULL" enable_flag=0 answer
  printf '   要重設密碼的登入帳號（完整輸入，大小寫不拘）：' >&2
  read -r username
  username="$(trim_ws "${username}")"
  target_hex="$(username_lookup_hex "${username}")"
  [[ "$(sql_scalar "SELECT COUNT(*) FROM admin_users WHERE username = CAST(${target_hex} AS nvarchar(191))")" == "1" ]] \
    || die "找不到這個登入帳號（請對照上面的列表）"
  local acct_status
  acct_status="$(sql_scalar "SELECT status FROM admin_users WHERE username = CAST(${target_hex} AS nvarchar(191))")"

  printf '   新的登入帳號（直接按 Enter＝不改；一般字串、可用中文、不得含空白、最長 64 字元）：' >&2
  read -r new_username
  new_username="$(trim_ws "${new_username}")"
  if [[ -n "${new_username}" ]]; then
    new_hex="$(username_strict_hex "${new_username}")"
    reject_test_username "${new_username}"
    [[ "$(sql_scalar "SELECT COUNT(*) FROM admin_users WHERE username = CAST(${new_hex} AS nvarchar(64)) AND username <> CAST(${target_hex} AS nvarchar(191))")" == "0" ]] \
      || die "新的登入帳號已被別的帳號使用"
  fi
  if [[ "${acct_status}" != "active" ]]; then
    printf '   這個帳號目前狀態是「%s」（無法登入）。要一併啟用嗎？(y/N)：' "${acct_status}" >&2
    read -r answer || answer=""
    [[ "${answer}" == "y" || "${answer}" == "Y" ]] && enable_flag=1
  fi

  echo "   🔴 即將重設 ${DB_NAME} 的管理員密碼：帳號「${username}」${new_username:+，並把登入帳號改為「${new_username}」}；"
  echo "      會清除鎖定與失敗次數，並讓該帳號目前所有登入狀態失效（需重新登入）。"
  confirm "${DB_NAME}"

  read_password_twice "${new_username:-${username}}"
  hash_password
  local hash="${NEW_HASH}"

  local sql_file="${WORK_DIR}/reset-password.sql"
  cat >"${sql_file}" <<'SQL'
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @id uniqueidentifier = (SELECT id FROM admin_users WHERE username = CAST($(PRODINIT_TARGET_HEX) AS nvarchar(191)));
IF @id IS NULL THROW 50003, N'找不到要重設的帳號', 1;
DECLARE @new_username nvarchar(64) = CAST($(PRODINIT_NEW_HEX) AS nvarchar(64));
IF @new_username IS NOT NULL AND EXISTS (SELECT 1 FROM admin_users WHERE username = @new_username AND id <> @id)
  THROW 50004, N'新的登入帳號已被別的帳號使用', 1;
UPDATE admin_users
   SET password_hash = N'$(PRODINIT_ADMIN_HASH)',
       password_changed_at = SYSUTCDATETIME(),
       failed_attempt_count = 0,
       locked_until = NULL,
       username = COALESCE(@new_username, username),
       status = CASE WHEN $(PRODINIT_ENABLE) = 1 THEN N'active' ELSE status END,
       updated_at = SYSUTCDATETIME()
 WHERE id = @id;
UPDATE admin_refresh_tokens SET revoked_at = SYSUTCDATETIME() WHERE admin_user_id = @id AND revoked_at IS NULL;
COMMIT TRANSACTION;
GO
SQL
  SQLCMD_DOCKER_EXTRA=(-v "${WORK_DIR}:/work/reset:ro" -e PRODINIT_TARGET_HEX -e PRODINIT_NEW_HEX -e PRODINIT_ADMIN_HASH -e PRODINIT_ENABLE)
  PRODINIT_TARGET_HEX="${target_hex}" PRODINIT_NEW_HEX="${new_hex}" PRODINIT_ADMIN_HASH="${hash}" PRODINIT_ENABLE="${enable_flag}" \
    run_sql_file "/work/reset/reset-password.sql" "重設密碼（${DB_NAME}）" "密碼沒有更動（整段在同一個交易內，已回滾）；修正後可直接重跑 reset-password ${TARGET}。"
  hash=""
  SQLCMD_DOCKER_EXTRA=()

  local final_hex="${target_hex}"
  [[ "${new_hex}" == "NULL" ]] || final_hex="${new_hex}"
  check_eq "密碼已更新且鎖定已清除" "1" "$(sql_scalar "SELECT COUNT(*) FROM admin_users WHERE username = CAST(${final_hex} AS nvarchar(191)) AND password_changed_at > DATEADD(minute, -5, SYSUTCDATETIME()) AND failed_attempt_count = 0 AND locked_until IS NULL")" \
    || die "更新後核對不符，請檢查"
  echo
  info "完成。請用「${new_username:-${username}}」與剛才輸入的新密碼登入後台（舊的登入狀態已全部失效）。"
}

cmd_wipe_partial() {
  load_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()
  local state
  state="$(db_state)"
  case "${state}" in
    empty) die "${DB_NAME} 已經是空的，不需要清除" ;;
    initialized) die "${DB_NAME} 已有 __EFMigrationsHistory＝初始化已完成，絕不清除。" ;;
    *) ;;
  esac
  if [[ "$(sql_scalar "SELECT COUNT(*) FROM sys.tables WHERE name = 'admin_users'")" == "1" ]]; then
    [[ "$(sql_scalar "SELECT COUNT(*) FROM admin_users")" == "0" ]] || die "admin_users 有資料，拒絕清除"
  fi
  echo "   🔴 即將刪除 ${DB_NAME} 內「所有」資料表、外鍵與視圖（沒有 __EFMigrationsHistory、沒有管理員，判定為 init 中途失敗的半成品）。"
  confirm "WIPE ${DB_NAME}"
  local wipe_sql="${WORK_DIR}/wipe.sql"
  cat >"${wipe_sql}" <<'SQL'
SET NOCOUNT ON;
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id = fk.parent_object_id;
EXEC sys.sp_executesql @sql;
SET @sql = N'';
SELECT @sql += N'DROP VIEW ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.views WHERE is_ms_shipped = 0;
EXEC sys.sp_executesql @sql;
SET @sql = N'';
SELECT @sql += N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.tables WHERE is_ms_shipped = 0;
EXEC sys.sp_executesql @sql;
GO
SQL
  SQLCMD_DOCKER_EXTRA=(-v "${WORK_DIR}:/work/wipe:ro")
  run_sql_file "/work/wipe/wipe.sql" "清除半成品（${DB_NAME}）"
  SQLCMD_DOCKER_EXTRA=()
  [[ "$(db_state)" == "empty" ]] || die "清除後仍有物件，請手動檢查"
  ok "${DB_NAME} 已回到空庫，可重新 init ${TARGET}"
}

main() {
  local cmd="${1:-}"
  case "${cmd}" in
    preflight) cmd_preflight "${2:-}" ;;
    init) cmd_init "${2:-}" ;;
    create-admin) cmd_create_admin "${2:-}" ;;
    reset-password) cmd_reset_password "${2:-}" ;;
    verify) cmd_verify "${2:-}" ;;
    wipe-partial) cmd_wipe_partial "${2:-}" ;;
    -h|--help|help) usage 0 ;;
    *) usage 1 ;;
  esac
}

# 被 deploy/prod-seed-import.sh `source` 時只載入函式（共用同一套連線解析與防護），不執行 main
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  main "$@"
fi
