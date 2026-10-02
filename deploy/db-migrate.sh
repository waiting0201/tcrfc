#!/usr/bin/env bash
# deploy/db-migrate.sh — 正式庫（Azure SQL）的 EF Core migration 關卡（docs/20-cicd.md §5「db-migrate.yml」）
#
# 只給 .github/workflows/db-migrate.yml 呼叫（也可在 VM 上以 runner 使用者手動跑 pending 做唯讀檢查）。
# 三個子命令，對應 workflow 的三個 job：
#
#   generate <club|charity|both>   【GitHub-hosted runner，需要 .NET SDK】用 `dotnet ef migrations script`
#                                   產出 ① idempotent.sql（真正會被執行的那一份）② per-migration/<id>.sql
#                                   （每支 migration 單獨的 SQL，給核准者閱讀）③ SHA256SUMS。不連資料庫。
#   pending  <club|charity|both>   【VM，唯讀】連正式庫讀 __EFMigrationsHistory，與 repo 內的 migration 比對，
#                                   列出「待套用」名單與其 SQL。沒有任何寫入。
#   apply    <club|charity|both>   【VM，經 production-db 核准後】再比對一次（與核准時看到的名單不同就拒絕），
#                                   用 sqlcmd 執行 idempotent.sql，完成後驗證歷史表。DBM_DRY_RUN=true 時只列出不執行。
#
# 為什麼用「idempotent SQL ＋ sqlcmd 容器」而不是 efbundle／在 VM 裝 SDK（docs/20 §5）：
#   1. 核准者看到的就是會被執行的東西：SQL 檔經 SHA-256 鎖定，apply 執行的位元組＝preview 產出的位元組；
#      efbundle 是不透明的二進位檔，審查的 SQL 與實際執行的程式碼之間要靠「相信 EF 會做同一件事」。
#   2. 不在 VM 裝 .NET SDK、也不需要 api 映像檔含 dotnet-ef（映像檔是 aspnet 執行環境，沒有 SDK）；
#      sqlcmd 容器與 deploy/prod-db-init.sh 是同一個映像檔、同一種連線方式，已在正式庫實測過。
#   3. 冪等：每支 migration 用 IF NOT EXISTS (… __EFMigrationsHistory …) 包起來，已套用的自動跳過；
#      重跑（例如上次中途失敗）是安全的。
#   交易行為：EF 為每支 migration 各開一個 BEGIN TRANSACTION … COMMIT。sqlcmd -b 遇到錯誤就中止並斷線，
#   未提交的交易由 SQL Server 回滾。所以：失敗的那支完全沒套用；在它之前已 COMMIT 的 migration 保持已套用。
#   不自動重試。
#
# 環境變數：
#   DBM_SQL_DIR                   【generate 為輸出目錄；pending／apply 為 artifact 目錄】絕對路徑，底下 <target>/
#   DBM_EXPECT_SHA_CLUB／_CHARITY   【pending／apply】preview job 輸出的 SHA256SUMS 雜湊（64 位小寫十六進位）
#   DBM_EXPECT_PENDING_HASH_CLUB／_CHARITY  【apply】pending job 輸出的待套用名單雜湊；核准後名單變了就拒絕
#   DBM_DRY_RUN                   true／false。apply 必填；只有 false 才會寫入
#   TCRFC_SECRETS_DIR             預設 /opt/tcrfc/secrets（club.env、charity.env）
#   MSSQL_TOOLS_IMAGE             預設 mcr.microsoft.com/mssql-tools（與 prod-db-init.sh、provision-secrets.sh 同一個）
#   GITHUB_STEP_SUMMARY／GITHUB_OUTPUT   有就寫（job summary 與 job outputs），沒有就只印到標準輸出
#
# 🔴 只給本機演練用的覆寫（必須同時設 DBM_REHEARSAL=1；目標主機若是 *.database.windows.net 一律拒絕）：
#   DBM_REHEARSAL=1  DBM_TRUST_CERT=1（sqlcmd -C）  DBM_EXPECT_DB_CLUB／DBM_EXPECT_DB_CHARITY（演練庫名）
#
# 安全模型（與 prod-db-init.sh 相同）：
#   - 連線資訊從 env 檔解析後以環境變數（SQLCMD*）傳進容器：`docker run -e NAME` 不帶值，密碼不出現在 ps／命令列／log。
#   - 🔴 不印伺服器主機名稱（公開 repo 的 Actions log 人人可看），只印資料庫名。
#   - 正式模式只允許 *.database.windows.net，且資料庫名必須是 tcrfc_club／tcrfc_charity（防止 club 的 migration 打進 charity）。
#   - 所有輸入（target、dry_run、雜湊）都經環境變數傳入並在這裡驗證格式，workflow 不把輸入內插進 shell 字串。

set -euo pipefail
export LC_ALL=C

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SECRETS_DIR="${TCRFC_SECRETS_DIR:-/opt/tcrfc/secrets}"
MSSQL_TOOLS_IMAGE="${MSSQL_TOOLS_IMAGE:-mcr.microsoft.com/mssql-tools}"
REHEARSAL="${DBM_REHEARSAL:-0}"
SUMMARY_FILE="${GITHUB_STEP_SUMMARY:-}"
OUTPUT_FILE="${GITHUB_OUTPUT:-}"

WORK_DIR=""
cleanup() {
  if [[ -n "${WORK_DIR}" && -d "${WORK_DIR}" ]]; then
    rm -rf "${WORK_DIR}"
  fi
}
trap cleanup EXIT

# ── 輸出 ───────────────────────────────────────────────────────────────────────
# say：同時印到 log 與 job summary；emit_md：只寫 summary（沒有 summary 檔時印到標準輸出）。
say() {
  printf '%s\n' "$*"
  if [[ -n "${SUMMARY_FILE}" ]]; then
    printf '%s\n' "$*" >>"${SUMMARY_FILE}"
  fi
}
emit_md() {
  if [[ -n "${SUMMARY_FILE}" ]]; then
    printf '%s\n' "$*" >>"${SUMMARY_FILE}"
  else
    printf '%s\n' "$*"
  fi
}
emit_output() { # ${1}=鍵 ${2}=值（單行）
  if [[ -n "${OUTPUT_FILE}" ]]; then
    printf '%s=%s\n' "${1}" "${2}" >>"${OUTPUT_FILE}"
  fi
}
die() {
  echo "[錯] $*" >&2
  if [[ -n "${SUMMARY_FILE}" ]]; then
    printf '\n> 🔴 **失敗**：%s\n' "$*" >>"${SUMMARY_FILE}"
  fi
  exit 1
}
info() { echo "==> $*"; }
ok() { echo "   [OK] $*"; }
warn() { echo "   [注意] $*" >&2; }

usage() {
  sed -n '2,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit "${1:-1}"
}

sha256_stdin() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum | cut -d' ' -f1
  else
    shasum -a 256 | cut -d' ' -f1
  fi
}
sha256_file() { sha256_stdin <"${1}"; }

# ── 目標庫設定（每個 target 只讀自己的檔案）──────────────────────────────────────
T="" CONTEXT="" EXPECT_DB="" ENV_FILE="" CONN_KEY="" MIGRATION_DIR=""
DB_SERVER="" DB_USER="" DB_PASSWORD="" DB_NAME=""

target_defaults() { # ${1}=club|charity；只設定固定值，不讀機密
  T="${1}"
  case "${T}" in
    club)
      CONTEXT="ClubDbContext"
      EXPECT_DB="${DBM_EXPECT_DB_CLUB:-tcrfc_club}"
      ENV_FILE="${SECRETS_DIR}/club.env"
      CONN_KEY="CLUB_SQL_CONNECTION_STRING"
      MIGRATION_DIR="${REPO_ROOT}/apps/api/Data/Migrations"
      ;;
    charity)
      CONTEXT="CharityDbContext"
      EXPECT_DB="${DBM_EXPECT_DB_CHARITY:-tcrfc_charity}"
      ENV_FILE="${SECRETS_DIR}/charity.env"
      CONN_KEY="CHARITY_SQL_CONNECTION_STRING"
      MIGRATION_DIR="${REPO_ROOT}/apps/api/CharityPlatform/Data/Migrations"
      ;;
    *)
      die "目標只能是 club 或 charity（收到「${T}」）"
      ;;
  esac
  if [[ "${REHEARSAL}" != "1" ]]; then
    if [[ "${EXPECT_DB}" != "tcrfc_club" && "${EXPECT_DB}" != "tcrfc_charity" ]] \
       || [[ -n "${DBM_EXPECT_DB_CLUB:-}${DBM_EXPECT_DB_CHARITY:-}${DBM_TRUST_CERT:-}" ]]; then
      die "演練用覆寫（DBM_EXPECT_DB_*／DBM_TRUST_CERT）只能搭配 DBM_REHEARSAL=1"
    fi
  fi
  [[ -d "${MIGRATION_DIR}" ]] || die "找不到 migration 目錄：${MIGRATION_DIR}"
}

# 從 env 檔取值（不 source，避免執行檔案內容）；值可能被單引號包住
env_get() { # ${1}=檔案 ${2}=鍵
  local line
  line="$(awk -v k="${2}" 'index($0, k "=") == 1 { print substr($0, length(k) + 2); exit }' "${1}")"
  line="${line#\'}"
  line="${line%\'}"
  printf '%s' "${line}"
}

load_connection() {
  [[ -r "${ENV_FILE}" ]] || die "讀不到 ${ENV_FILE}（請在 VM 上以擁有者 runner 執行；檔案權限應為 600）"
  local conn part
  local -a parts
  conn="$(env_get "${ENV_FILE}" "${CONN_KEY}")"
  [[ -n "${conn}" ]] || die "${ENV_FILE} 沒有 ${CONN_KEY}"
  DB_SERVER="" DB_USER="" DB_PASSWORD="" DB_NAME=""
  IFS=';' read -ra parts <<<"${conn}"
  local key lower
  for part in "${parts[@]}"; do
    part="${part#"${part%%[![:space:]]*}"}"
    [[ "${part}" == *=* ]] || continue
    key="${part%%=*}"
    lower="$(printf '%s' "${key}" | tr '[:upper:]' '[:lower:]')" # .NET 連線字串的鍵不分大小寫
    case "${lower}" in
      server | "data source") DB_SERVER="${part#*=}" ;;
      "user id" | uid) DB_USER="${part#*=}" ;;
      password | pwd) DB_PASSWORD="${part#*=}" ;;
      database | "initial catalog") DB_NAME="${part#*=}" ;;
      *) ;;
    esac
  done
  [[ -n "${DB_SERVER}" && -n "${DB_USER}" && -n "${DB_PASSWORD}" && -n "${DB_NAME}" ]] \
    || die "${CONN_KEY} 解析不出 Server／User ID／Password／Database"

  local host="${DB_SERVER#tcp:}"
  host="${host%%,*}"
  if [[ "${REHEARSAL}" == "1" ]]; then
    [[ "${host}" != *.database.windows.net ]] || die "DBM_REHEARSAL=1 時拒絕連到 Azure SQL——演練只能對本機容器"
  else
    [[ "${host}" == *.database.windows.net ]] || die "正式模式只允許 *.database.windows.net（${CONN_KEY} 的主機不符）"
  fi
  [[ "${DB_NAME}" == "${EXPECT_DB}" ]] \
    || die "${CONN_KEY} 的資料庫是「${DB_NAME}」，${T} 目標應為「${EXPECT_DB}」——拒絕（防止把 ${T} 的 migration 打進別的庫）"
}

# ── sqlcmd（在容器內執行；連線資訊走環境變數）────────────────────────────────────
# 不加 `docker run -i`：sqlcmd 只用 -Q／-i 檔案，不需要 stdin。
SQLCMD_DOCKER_EXTRA=()
sqlcmd_container() { # 額外的 -v 掛載用 SQLCMD_DOCKER_EXTRA；其餘參數直接給 sqlcmd
  local trust=()
  if [[ "${DBM_TRUST_CERT:-0}" == "1" ]]; then
    trust=(-C)
  fi
  SQLCMDSERVER="${DB_SERVER}" SQLCMDUSER="${DB_USER}" SQLCMDPASSWORD="${DB_PASSWORD}" SQLCMDDBNAME="${DB_NAME}" \
    docker run --rm \
      -e SQLCMDSERVER -e SQLCMDUSER -e SQLCMDPASSWORD -e SQLCMDDBNAME \
      ${SQLCMD_DOCKER_EXTRA[@]+"${SQLCMD_DOCKER_EXTRA[@]}"} \
      "${MSSQL_TOOLS_IMAGE}" /opt/mssql-tools/bin/sqlcmd -N ${trust[@]+"${trust[@]}"} -l 30 -b "$@"
}

sql_scalar() { # ${1}=T-SQL；回傳單一純量（去除空白）
  local out
  out="$(sqlcmd_container -h -1 -W -Q "SET NOCOUNT ON; ${1}")" || die "查詢失敗：${1}"
  printf '%s' "${out}" | tr -d '[:space:]'
}

require_tools() {
  command -v docker >/dev/null 2>&1 || die "找不到 docker"
  docker info >/dev/null 2>&1 || die "docker 無法使用（runner 使用者要在 docker 群組）"
  WORK_DIR="$(mktemp -d)"
  chmod 700 "${WORK_DIR}"
}

# ── repo 內的 migration 清單（以 Designer 的 [Migration("…")] 為準，檔名只是慣例）────
repo_ids() {
  grep -hoE '\[Migration\("[0-9]{14}_[A-Za-z0-9_]+"\)\]' "${MIGRATION_DIR}"/*.Designer.cs \
    | sed -E 's/^\[Migration\("(.*)"\)\]$/\1/' | sort -u
}

# ── 參數 ───────────────────────────────────────────────────────────────────────
target_list() { # ${1}=club|charity|both → 印出以空白分隔的清單
  case "${1}" in
    club) echo "club" ;;
    charity) echo "charity" ;;
    both) echo "club charity" ;;
    *) die "目標只能是 club、charity 或 both（收到「${1}」）" ;;
  esac
}

expect_value() { # ${1}=變數前綴 ${2}=target → 印出 DBM_<前綴>_<TARGET> 的值
  case "${1}:${2}" in
    SHA:club) printf '%s' "${DBM_EXPECT_SHA_CLUB:-}" ;;
    SHA:charity) printf '%s' "${DBM_EXPECT_SHA_CHARITY:-}" ;;
    PENDING:club) printf '%s' "${DBM_EXPECT_PENDING_HASH_CLUB:-}" ;;
    PENDING:charity) printf '%s' "${DBM_EXPECT_PENDING_HASH_CHARITY:-}" ;;
    *) die "內部錯誤：expect_value ${1}:${2}" ;;
  esac
}

require_abs_dir() { # ${1}=DBM_SQL_DIR 的值
  [[ -n "${1}" && "${1}" == /* ]] || die "DBM_SQL_DIR 必須是絕對路徑"
  [[ "${1}" != *..* ]] || die "DBM_SQL_DIR 不可含 .."
}

# ── artifact 完整性：SHA256SUMS 的雜湊必須等於 preview job 輸出的值，且每個檔案都對得上 ──
verify_artifact() { # 使用 T 與 DBM_SQL_DIR
  local dir="${DBM_SQL_DIR}/${T}" expect actual line hash rel n=0 files
  expect="$(expect_value SHA "${T}")"
  [[ "${expect}" =~ ^[0-9a-f]{64}$ ]] || die "DBM_EXPECT_SHA_${T} 不是 64 位小寫十六進位（preview job 沒有輸出雜湊？）"
  [[ -f "${dir}/idempotent.sql" && -f "${dir}/SHA256SUMS" ]] || die "artifact 缺少 ${T}/idempotent.sql 或 SHA256SUMS"
  actual="$(sha256_file "${dir}/SHA256SUMS")"
  [[ "${actual}" == "${expect}" ]] || die "${T} 的 SHA256SUMS 雜湊與 preview job 輸出不符——artifact 被改過，或不是同一次 run 的產物"
  while IFS= read -r line || [[ -n "${line}" ]]; do
    hash="${line%%  *}"
    rel="${line#*  }"
    [[ "${hash}" =~ ^[0-9a-f]{64}$ ]] || die "SHA256SUMS 格式錯誤：${line}"
    [[ "${rel}" == ./* && "${rel}" != *..* ]] || die "SHA256SUMS 含不合法的路徑：${rel}"
    [[ -f "${dir}/${rel}" ]] || die "SHA256SUMS 列出的檔案不存在：${rel}"
    [[ "$(sha256_file "${dir}/${rel}")" == "${hash}" ]] || die "${T}/${rel} 的內容與 SHA256SUMS 不符"
    n=$((n + 1))
  done <"${dir}/SHA256SUMS"
  files="$(find "${dir}" -type f ! -name SHA256SUMS | wc -l | tr -d ' ')"
  [[ "${n}" == "${files}" ]] || die "${T} artifact 的檔案數（${files}）與 SHA256SUMS（${n}）不符——有多出來的檔案"
  grep -qF '  ./idempotent.sql' "${dir}/SHA256SUMS" || die "SHA256SUMS 沒有涵蓋 idempotent.sql"
}

# ── 與正式庫的歷史表比對 ─────────────────────────────────────────────────────────
HIST_COUNT=0 REPO_COUNT=0 PENDING_COUNT=0 PENDING_HASH="" PENDING_FILE=""

read_state() { # 使用 T；唯讀。設定 HIST_COUNT／REPO_COUNT／PENDING_COUNT／PENDING_HASH／PENDING_FILE
  local who has_hist hist_n raw repo_file hist_file unknown
  who="$(sql_scalar "SELECT DB_NAME()")"
  [[ "${who}" == "${DB_NAME}" ]] || die "連上後 DB_NAME() 是 ${who}，與設定的 ${DB_NAME} 不符"
  has_hist="$(sql_scalar "SELECT COUNT(*) FROM sys.tables WHERE name = '__EFMigrationsHistory'")"
  [[ "${has_hist}" == "1" ]] \
    || die "${DB_NAME} 沒有 __EFMigrationsHistory——庫還沒初始化。db-migrate 不負責建庫，請先在 VM 上跑 deploy/prod-db-init.sh（infra/README.md §4.8）"
  hist_n="$(sql_scalar "SELECT COUNT(*) FROM __EFMigrationsHistory")"
  [[ "${hist_n}" =~ ^[0-9]+$ ]] || die "讀不到歷史表筆數（收到「${hist_n}」）"
  raw="$(sqlcmd_container -h -1 -W -Q "SET NOCOUNT ON; SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId")" \
    || die "讀取 __EFMigrationsHistory 失敗"

  repo_file="${WORK_DIR}/repo.${T}.txt"
  hist_file="${WORK_DIR}/hist.${T}.txt"
  PENDING_FILE="${WORK_DIR}/pending.${T}.txt"
  repo_ids >"${repo_file}" || true
  printf '%s\n' "${raw}" | tr -d '\r' | grep -E '^[0-9]{14}_[A-Za-z0-9_]+$' | sort -u >"${hist_file}" || true

  REPO_COUNT="$(grep -c . "${repo_file}" || true)"
  HIST_COUNT="$(grep -c . "${hist_file}" || true)"
  [[ "${REPO_COUNT}" -ge 1 ]] || die "在 ${MIGRATION_DIR} 找不到任何 migration"
  [[ "${HIST_COUNT}" == "${hist_n}" ]] \
    || die "歷史表有 ${hist_n} 筆，但只讀到 ${HIST_COUNT} 筆格式正確的 MigrationId（輸出被截斷或含不明內容）——拒絕在不確定的狀態下比對"

  unknown="$(comm -13 "${repo_file}" "${hist_file}")"
  if [[ -n "${unknown}" ]]; then
    die "正式庫的歷史表有 repo 不認得的 migration：$(printf '%s' "${unknown}" | tr '\n' ' ')——這個 commit 比資料庫舊（從舊分支觸發？）或歷史表被動過。請用 master 最新的 commit 重跑，不要硬套"
  fi
  comm -23 "${repo_file}" "${hist_file}" >"${PENDING_FILE}"
  PENDING_COUNT="$(grep -c . "${PENDING_FILE}" || true)"
  PENDING_HASH="$(sha256_file "${PENDING_FILE}")"
}

inspect_target() { # ${1}=target；完整檢查（設定、連線、artifact、歷史比對）
  target_defaults "${1}"
  load_connection
  SQLCMD_DOCKER_EXTRA=()
  verify_artifact
  read_state
  # 待套用的每一支都必須在 idempotent.sql 裡（artifact 與 checkout 是同一個 commit 的產物）
  local id
  while IFS= read -r id; do
    [[ -n "${id}" ]] || continue
    grep -qF "N'${id}'" "${DBM_SQL_DIR}/${T}/idempotent.sql" \
      || die "待套用的 ${id} 不在 ${T}/idempotent.sql 裡——artifact 與目前的 checkout 不是同一個 commit"
  done <"${PENDING_FILE}"
}

# ── 報告 ───────────────────────────────────────────────────────────────────────
SQL_LINES_SHOWN=0
SQL_LINES_CAP_EACH=400
SQL_LINES_CAP_TOTAL=2000

show_pending_sql() { # ${1}=migration id；SQL 只寫 summary
  local f="${DBM_SQL_DIR}/${T}/per-migration/${1}.sql" n
  [[ -f "${f}" ]] || die "找不到 ${1} 的預覽 SQL（${T}/per-migration/${1}.sql）——核准者看不到要執行什麼，拒絕繼續"
  n="$(wc -l <"${f}" | tr -d ' ')"
  emit_md ""
  emit_md "<details><summary><code>${1}</code>（${n} 行）</summary>"
  emit_md ""
  if [[ $((SQL_LINES_SHOWN + n)) -gt "${SQL_LINES_CAP_TOTAL}" ]]; then
    emit_md "（本頁已列出 ${SQL_LINES_SHOWN} 行 SQL，超過上限，這一支請看 artifact 的 \`${T}/per-migration/${1}.sql\`）"
  elif [[ "${n}" -gt "${SQL_LINES_CAP_EACH}" ]]; then
    emit_md "（超過 ${SQL_LINES_CAP_EACH} 行，只列前 ${SQL_LINES_CAP_EACH} 行；完整內容看 artifact 的 \`${T}/per-migration/${1}.sql\`）"
    emit_md ""
    emit_md '~~~~sql'
    head -n "${SQL_LINES_CAP_EACH}" "${f}" | tr -d '\r' | while IFS= read -r l; do emit_md "${l}"; done
    emit_md '~~~~'
    SQL_LINES_SHOWN=$((SQL_LINES_SHOWN + SQL_LINES_CAP_EACH))
  else
    emit_md '~~~~sql'
    tr -d '\r' <"${f}" | while IFS= read -r l; do emit_md "${l}"; done
    emit_md '~~~~'
    SQL_LINES_SHOWN=$((SQL_LINES_SHOWN + n))
  fi
  emit_md ""
  emit_md "</details>"
}

report_target() { # 使用 T 與 read_state 的結果
  say ""
  say "### ${T}（資料庫 \`${DB_NAME}\`）"
  say "- 歷史表 ${HIST_COUNT} 筆；repo 內 migration ${REPO_COUNT} 支；**待套用 ${PENDING_COUNT} 支**"
  if [[ "${PENDING_COUNT}" -eq 0 ]]; then
    say "- 資料庫已是最新，沒有東西要套用。"
    return 0
  fi
  local id
  say "- 待套用（依序）："
  while IFS= read -r id; do
    [[ -n "${id}" ]] || continue
    say "  - \`${id}\`"
  done <"${PENDING_FILE}"
  say "- 名單雜湊（核准後 apply 會再比對，不同就拒絕）：\`${PENDING_HASH}\`"
  say ""
  say "**會執行的 SQL**（每支 migration 各自一個交易；以下是各支單獨的 SQL，實際執行的是同一批內容的冪等版本 \`${T}/idempotent.sql\`，已由 SHA-256 鎖定）："
  while IFS= read -r id; do
    [[ -n "${id}" ]] || continue
    show_pending_sql "${id}"
  done <"${PENDING_FILE}"
}

# ── 子命令：generate ───────────────────────────────────────────────────────────
strip_bom() { perl -i -pe 's/^\xEF\xBB\xBF// if $. == 1' "${1}"; }

cmd_generate() {
  local targets t id prev out n total_ids
  targets="$(target_list "${1:-}")"
  require_abs_dir "${DBM_SQL_DIR:-}"
  command -v dotnet >/dev/null 2>&1 || die "找不到 dotnet（generate 只在 GitHub-hosted runner 跑，需要 .NET SDK）"
  command -v perl >/dev/null 2>&1 || die "找不到 perl"
  say "## 資料庫 migration 預覽（產生 SQL）"
  for t in ${targets}; do
    target_defaults "${t}"
    out="${DBM_SQL_DIR}/${t}"
    rm -rf "${out}"
    mkdir -p "${out}/per-migration"
    total_ids="$(repo_ids | grep -c . || true)"
    [[ "${total_ids}" -ge 1 ]] || die "找不到 ${t} 的 migration"
    info "${t}：${total_ids} 支 migration，產生 idempotent.sql"
    ( cd "${REPO_ROOT}/apps/api" \
      && dotnet ef migrations script --idempotent --context "${CONTEXT}" --no-build --configuration Release -o "${out}/idempotent.sql" ) \
      || die "${t}：dotnet ef migrations script --idempotent 失敗"
    [[ -s "${out}/idempotent.sql" ]] || die "${t}：產出的 idempotent.sql 是空的"
    strip_bom "${out}/idempotent.sql"
    for id in $(repo_ids); do
      grep -qF "N'${id}'" "${out}/idempotent.sql" || die "${t}：idempotent.sql 沒有涵蓋 ${id}"
    done
    prev="0"
    n=0
    for id in $(repo_ids); do
      ( cd "${REPO_ROOT}/apps/api" \
        && dotnet ef migrations script "${prev}" "${id}" --context "${CONTEXT}" --no-build --configuration Release -o "${out}/per-migration/${id}.sql" ) \
        || die "${t}：dotnet ef migrations script ${prev} ${id} 失敗"
      strip_bom "${out}/per-migration/${id}.sql"
      prev="${id}"
      n=$((n + 1))
    done
    ok "${t}：per-migration ${n} 份"
    ( cd "${out}" && find . -type f ! -name SHA256SUMS | sort | while IFS= read -r f; do
        printf '%s  %s\n' "$(sha256_file "${f}")" "${f}"
      done >SHA256SUMS )
    emit_output "${t}_sha" "$(sha256_file "${out}/SHA256SUMS")"
    say ""
    say "### ${t}（\`${CONTEXT}\`）"
    say "- repo 內 migration ${total_ids} 支；idempotent.sql $(wc -l <"${out}/idempotent.sql" | tr -d ' ') 行（$(wc -c <"${out}/idempotent.sql" | tr -d ' ') bytes）"
    say "- SHA256SUMS 雜湊：\`$(sha256_file "${out}/SHA256SUMS")\`"
    say "- 這份預覽不連資料庫，**不知道哪些已套用**：哪些會真的執行看接下來「VM 唯讀比對」那個 job 的 summary。"
    if [[ "$(wc -c <"${out}/idempotent.sql" | tr -d ' ')" -le 300000 ]]; then
      emit_md ""
      emit_md "<details><summary>${t} 的 idempotent.sql 全文</summary>"
      emit_md ""
      emit_md '~~~~sql'
      tr -d '\r' <"${out}/idempotent.sql" | while IFS= read -r l; do emit_md "${l}"; done
      emit_md '~~~~'
      emit_md ""
      emit_md "</details>"
    else
      say "- idempotent.sql 超過 300 KB，不貼進 summary，請下載 artifact \`migrate-sql\`。"
    fi
  done
}

# ── 子命令：pending ────────────────────────────────────────────────────────────
cmd_pending() {
  local targets t total=0
  targets="$(target_list "${1:-}")"
  require_abs_dir "${DBM_SQL_DIR:-}"
  require_tools
  emit_output club_count 0
  emit_output club_hash ""
  emit_output charity_count 0
  emit_output charity_hash ""
  say "## 資料庫 migration 預檢（正式庫，唯讀）"
  for t in ${targets}; do
    info "pending：${t}（唯讀）"
    inspect_target "${t}"
    ok "${t}：${DB_NAME}，歷史 ${HIST_COUNT} 筆，待套用 ${PENDING_COUNT} 支"
    report_target
    emit_output "${t}_count" "${PENDING_COUNT}"
    emit_output "${t}_hash" "${PENDING_HASH}"
    total=$((total + PENDING_COUNT))
  done
  emit_output has_pending "$([[ "${total}" -gt 0 ]] && echo true || echo false)"
  say ""
  if [[ "${total}" -eq 0 ]]; then
    say "✅ **沒有待套用的 migration**，不需要核准、也不會執行任何寫入。"
  elif [[ "${DBM_DRY_RUN:-}" == "true" ]]; then
    say "🟡 **dry_run = true**：到此為止，沒有任何寫入、也不會要求核准。要套用請重新觸發 workflow 並取消勾選 dry_run。"
  else
    say "🟠 共 ${total} 支待套用。接著 **apply job 會停在 \`production-db\` 的核准關卡**——請先讀完上面的 SQL 再按 Approve。"
    say "⚠️ Azure SQL Basic 層只有 7 天 PITR，這是唯一的救命索（還原步驟見 infra/README.md「資料庫 migration」）。"
  fi
}

# ── 子命令：apply ──────────────────────────────────────────────────────────────
check_expected_pending() { # 核准時看到的名單與現在是否一致
  local expect
  expect="$(expect_value PENDING "${T}")"
  [[ "${expect}" =~ ^[0-9a-f]{64}$ ]] || die "DBM_EXPECT_PENDING_HASH_${T} 不是 64 位小寫十六進位（pending job 沒有輸出？）"
  [[ "${PENDING_HASH}" == "${expect}" ]] \
    || die "${T} 的待套用名單與核准時看到的不同（現在 ${PENDING_COUNT} 支）——核准之後資料庫或 checkout 變了。沒有執行任何東西；請重新觸發讓核准者重看一次"
}

apply_failure_report() { # ${1}=sqlcmd log
  local before_pending="${PENDING_FILE}.before" id
  cp "${PENDING_FILE}" "${before_pending}"
  say ""
  say "### 🔴 ${T}（\`${DB_NAME}\`）套用失敗"
  say ""
  say "sqlcmd 輸出最後 40 行（不含連線資訊）："
  emit_md '~~~~'
  tail -n 40 "${1}" | tr -d '\r' | while IFS= read -r l; do say "${l}"; done
  emit_md '~~~~'
  say ""
  # 盡力查現況（查不到不影響失敗訊息）
  if ( SUMMARY_FILE=""; read_state ) >/dev/null 2>&1; then
    read_state
    say "**目前狀態**：歷史表 ${HIST_COUNT} 筆；仍待套用 ${PENDING_COUNT} 支："
    while IFS= read -r id; do
      [[ -n "${id}" ]] || continue
      say "- \`${id}\`"
    done <"${PENDING_FILE}"
  else
    say "**目前狀態**：連歷史表都讀不到，請在 VM 上手動確認。"
  fi
  say ""
  say "**下一步（沒有自動重試，也沒有自動回復）**"
  say "1. 讀上面的錯誤。EF 每支 migration 一個交易：**失敗的那一支已被回滾**（上方「仍待套用」會列出它），在它之前已提交的維持已套用。"
  say "2. 若該支含不能放進交易的操作（例如 \`ALTER DATABASE\`），那一段可能已生效——對照 per-migration SQL 手動檢查。"
  say "3. 修正方式是**新增一支 migration 或修正尚未套用的那支**，走 PR → master → 重新觸發本 workflow（冪等，已套用的會跳過）。**不要手動改 \`__EFMigrationsHistory\`**，除非你已確認那支的結果完整存在。"
  say "4. 資料已受損且無法就地修復：以 PITR 還原成新庫（Basic 只保留 **7 天**），步驟見 infra/README.md「資料庫 migration」與 docs/17 §6。"
  say "5. 在問題釐清前，**不要部署依賴新結構的 api 版本**。"
}

cmd_apply() {
  local targets t dry="${DBM_DRY_RUN:-}" log done_count=0
  targets="$(target_list "${1:-}")"
  require_abs_dir "${DBM_SQL_DIR:-}"
  case "${dry}" in
    true | false) ;;
    *) die "DBM_DRY_RUN 必須是 true 或 false（收到「${dry}」）——拒絕在不明確的模式下寫入" ;;
  esac
  require_tools

  say "## 資料庫 migration 套用（正式庫）"
  # 第一輪：全部目標先檢查完，任何一個不符就整個不動手
  for t in ${targets}; do
    info "apply 預檢：${t}"
    inspect_target "${t}"
    check_expected_pending
    ok "${t}：${DB_NAME}，待套用 ${PENDING_COUNT} 支，名單與核准時一致"
  done
  if [[ "${dry}" == "true" ]]; then
    for t in ${targets}; do
      inspect_target "${t}"
      report_target
    done
    say ""
    say "🟡 **dry_run = true：沒有執行任何寫入。**"
    return 0
  fi

  # 第二輪：依序套用；一個失敗就停，後面的目標不碰
  for t in ${targets}; do
    inspect_target "${t}"
    check_expected_pending
    if [[ "${PENDING_COUNT}" -eq 0 ]]; then
      say "- ${t}：沒有待套用的 migration，略過。"
      continue
    fi
    say ""
    say "### ${t}（\`${DB_NAME}\`）：套用 ${PENDING_COUNT} 支"
    log="${WORK_DIR}/apply.${t}.log"
    info "套用 ${t}：sqlcmd -b -I -i idempotent.sql"
    SQLCMD_DOCKER_EXTRA=(-v "${DBM_SQL_DIR}/${t}:/work:ro")
    # -I：QUOTED_IDENTIFIER ON（有篩選索引／計算欄位的表，預設的 OFF 會讓 DDL 失敗）。
    # 🔴 不要加 -f 65001：Linux 的 mssql-tools sqlcmd 不支援 -f（本機演練實測：「Unknown Option」），預設已是 UTF-8；
    # 檔案的 BOM 由 generate 在產出時移除。
    if sqlcmd_container -I -i /work/idempotent.sql >"${log}" 2>&1; then
      ok "${t}：sqlcmd 結束碼 0（輸出 $(wc -l <"${log}" | tr -d ' ') 行）"
    else
      SQLCMD_DOCKER_EXTRA=()
      apply_failure_report "${log}"
      die "${t}：套用失敗，已停止（沒有自動重試）。詳見 job summary 的「下一步」。"
    fi
    SQLCMD_DOCKER_EXTRA=()
    # 驗證：歷史表筆數＝repo 筆數、待套用歸零
    read_state
    if [[ "${PENDING_COUNT}" -ne 0 || "${HIST_COUNT}" != "${REPO_COUNT}" ]]; then
      die "${t}：sqlcmd 回報成功，但歷史表 ${HIST_COUNT} 筆／repo ${REPO_COUNT} 筆、仍待套用 ${PENDING_COUNT} 支——狀態不一致，請在 VM 上手動確認（不要重跑前先看清楚）"
    fi
    say "- ✅ 套用完成並驗證：歷史表 ${HIST_COUNT} 筆＝repo ${REPO_COUNT} 筆，待套用 0 支。"
    done_count=$((done_count + 1))
  done
  say ""
  say "✅ **完成**（套用 ${done_count} 個庫）。接著可以讓 deploy.yml 部署使用新結構的 api（docs/20 §5：先 migrate 後 deploy）。"
}

# ── main ───────────────────────────────────────────────────────────────────────
main() {
  local cmd="${1:-}"
  case "${cmd}" in
    generate) cmd_generate "${2:-}" ;;
    pending) cmd_pending "${2:-}" ;;
    apply) cmd_apply "${2:-}" ;;
    -h | --help | help) usage 0 ;;
    *) usage 1 ;;
  esac
}

main "$@"
