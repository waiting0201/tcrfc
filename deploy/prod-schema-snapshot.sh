#!/usr/bin/env bash
# deploy/prod-schema-snapshot.sh — 正式庫（Azure SQL）結構快照（唯讀）（docs/20-cicd.md §5「prod-schema-snapshot.yml」）
#
# 只給 .github/workflows/prod-schema-snapshot.yml 呼叫（也可在 VM 上以 runner 使用者手動跑）。
# 目的：直接讀「正式庫真實的結構」，抽成與 deploy/schema-snapshot.sql 同格式的文字快照，下載回本機後
#       用 deploy/schema-diff.py 與「今天的 DDL 新建的庫」或本機庫比對——用事實證明本機與正式庫結構一致，而不是靠模擬。
#
# 用法：
#   deploy/prod-schema-snapshot.sh <club|charity|both> <輸出目錄（絕對路徑）>
#   產出：<輸出目錄>/<target>.snapshot.txt（每個目標一份）、MANIFEST.txt、SHA256SUMS
#
# 環境變數：
#   TCRFC_SECRETS_DIR    預設 /opt/tcrfc/secrets（club.env、charity.env；與 db-migrate.sh 同一個讀法）
#   MSSQL_TOOLS_IMAGE    預設 mcr.microsoft.com/mssql-tools（sqlcmd 13；與 db-migrate.sh、prod-db-init.sh 同一個映像檔）
#   GITHUB_STEP_SUMMARY  有就寫 job summary（只寫庫名、行數、類別計數、SHA-256；不寫快照內容、不寫主機名）
#
# 🔴 只給本機演練用的覆寫（必須同時設 DBM_REHEARSAL=1；目標主機若是 *.database.windows.net 一律拒絕）：
#   DBM_REHEARSAL=1  DBM_TRUST_CERT=1（sqlcmd -C）  DBM_EXPECT_DB_CLUB／DBM_EXPECT_DB_CHARITY（演練庫名）
#
# 🔴 唯讀保證（多層，任何一層單獨都不夠，所以疊起來）：
#   1. 只執行 deploy/schema-snapshot.sql 這一個檔案，路徑寫死、沒有參數可換。
#   2. 執行前先靜態檢查該檔（去掉註解後）不得出現任何寫入／執行類關鍵字（INSERT UPDATE DELETE MERGE DROP ALTER CREATE
#      TRUNCATE EXEC INTO GRANT REVOKE DENY BACKUP RESTORE DBCC KILL SHUTDOWN BULK OPENROWSET OPENQUERY OPENDATASOURCE
#      WAITFOR USE xp_／sp_ 開頭的程序）；有就拒絕執行。（檢查的是詞，不是子字串：update_referential_action_desc 不誤判。）
#   3. 連線層級：SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED（讀目錄檢視不取共用鎖）、SET LOCK_TIMEOUT 10000
#      （被擋超過 10 秒就失敗，不卡住別人）、SET DEADLOCK_PRIORITY LOW（真死結時犧牲自己）。
#   4. 整段包在 BEGIN TRANSACTION … ROLLBACK TRANSACTION：即使第 2 層被繞過、檔案被塞進寫入，交易層仍會回滾。
#   5. 沒有核准關卡之外的新能力：用的是 VM 上既有的 club.env／charity.env 連線字串（與 db-migrate.sh pending 相同）。
#      Azure SQL 沒有「唯讀登入」可以在這裡用（ApplicationIntent=ReadOnly 只在有 read scale-out 的層級才導向唯讀副本，
#      Basic 層會落在主要副本，不是保證）。若要根治：建議由使用者另建一個只有 VIEW DEFINITION 的資料庫使用者，
#      見 docs/20 §5「prod-schema-snapshot.yml」的「建議」。本腳本不建立任何帳號或改權限。
#
# 🔴 機密／外洩防護（公開 repo：Actions log 與 artifact 任何登入 GitHub 的人都看得到）：
#   - 連線資訊從 env 檔解析後以環境變數（SQLCMD*）傳進容器：`docker run -e NAME` 不帶值，密碼不進命令列／log。
#   - 不印伺服器主機名稱，只印資料庫名；sqlcmd 的錯誤輸出先經 redact（主機全名／短名／帳號／密碼／IP 換成 ***）才會印。
#   - 輸出快照發佈前掃描：每一行必須是「類別|…」格式；不得含主機全名／短名、帳號、密碼、*.database.windows.net、
#     Server=／Password=／User Id=、IP 位址。任何一項命中就刪掉輸出並失敗（不上傳）。
#   - 輸入（target、輸出目錄）都在這裡驗證格式；workflow 不把輸入內插進 shell 字串。

set -euo pipefail
export LC_ALL=C

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=deploy/lib/sqlcmd-guard.sh
source "${SCRIPT_DIR}/lib/sqlcmd-guard.sh"
SNAPSHOT_SQL="${SCRIPT_DIR}/schema-snapshot.sql" # 🔴 寫死，不接受參數
SECRETS_DIR="${TCRFC_SECRETS_DIR:-/opt/tcrfc/secrets}"
MSSQL_TOOLS_IMAGE="${MSSQL_TOOLS_IMAGE:-mcr.microsoft.com/mssql-tools}"
REHEARSAL="${DBM_REHEARSAL:-0}"
SUMMARY_FILE="${GITHUB_STEP_SUMMARY:-}"

WORK_DIR=""
cleanup() {
  if [[ -n "${WORK_DIR}" && -d "${WORK_DIR}" ]]; then
    rm -rf "${WORK_DIR}"
  fi
}
trap cleanup EXIT

# ── 輸出 ───────────────────────────────────────────────────────────────────────
say() {
  printf '%s\n' "$*"
  if [[ -n "${SUMMARY_FILE}" ]]; then
    printf '%s\n' "$*" >>"${SUMMARY_FILE}"
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

usage() {
  sed -n '2,9p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit "${1:-1}"
}

sha256_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum <"${1}" | cut -d' ' -f1
  else
    shasum -a 256 <"${1}" | cut -d' ' -f1
  fi
}

# ── 目標庫設定（與 db-migrate.sh 相同的讀法；每個 target 只讀自己的檔案）─────────────
T="" EXPECT_DB="" ENV_FILE="" CONN_KEY=""
DB_SERVER="" DB_USER="" DB_PASSWORD="" DB_NAME="" DB_HOST=""

target_defaults() { # ${1}=club|charity
  T="${1}"
  case "${T}" in
    club)
      EXPECT_DB="${DBM_EXPECT_DB_CLUB:-tcrfc_club}"
      ENV_FILE="${SECRETS_DIR}/club.env"
      CONN_KEY="CLUB_SQL_CONNECTION_STRING"
      ;;
    charity)
      EXPECT_DB="${DBM_EXPECT_DB_CHARITY:-tcrfc_charity}"
      ENV_FILE="${SECRETS_DIR}/charity.env"
      CONN_KEY="CHARITY_SQL_CONNECTION_STRING"
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
  local conn part key lower
  local -a parts
  conn="$(env_get "${ENV_FILE}" "${CONN_KEY}")"
  [[ -n "${conn}" ]] || die "${ENV_FILE} 沒有 ${CONN_KEY}"
  DB_SERVER="" DB_USER="" DB_PASSWORD="" DB_NAME=""
  IFS=';' read -ra parts <<<"${conn}"
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

  DB_HOST="${DB_SERVER#tcp:}"
  DB_HOST="${DB_HOST%%,*}"
  if [[ "${REHEARSAL}" == "1" ]]; then
    [[ "${DB_HOST}" != *.database.windows.net ]] || die "DBM_REHEARSAL=1 時拒絕連到 Azure SQL——演練只能對本機容器"
  else
    [[ "${DB_HOST}" == *.database.windows.net ]] || die "正式模式只允許 *.database.windows.net（${CONN_KEY} 的主機不符）"
  fi
  [[ "${DB_NAME}" == "${EXPECT_DB}" ]] \
    || die "${CONN_KEY} 的資料庫是「${DB_NAME}」，${T} 目標應為「${EXPECT_DB}」——拒絕（防止快照抓到別的庫）"
}

# ── 敏感字串遮蔽（awk 以 ENVIRON 取值，字面比對，不經命令列、不當正規式）────────────────
redact() { # stdin → stdout
  local short="${DB_HOST%%.*}"
  RD_HOST="${DB_HOST}" RD_SHORT="${short}" RD_USER="${DB_USER}" RD_PASS="${DB_PASSWORD}" \
    awk '
      function rep(s, needle,   out, i) {
        if (length(needle) < 3) return s
        out = ""
        while ((i = index(s, needle)) > 0) { out = out substr(s, 1, i - 1) "***"; s = substr(s, i + length(needle)) }
        return out s
      }
      {
        s = $0
        s = rep(s, ENVIRON["RD_PASS"]); s = rep(s, ENVIRON["RD_HOST"]); s = rep(s, ENVIRON["RD_SHORT"]); s = rep(s, ENVIRON["RD_USER"])
        gsub(/[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+/, "***", s)
        print s
      }'
}

# ── 唯讀靜態檢查：schema-snapshot.sql 去掉註解後不得含寫入／執行類的詞 ─────────────────────
FORBIDDEN_WORDS='INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|INTO|GRANT|REVOKE|DENY|BACKUP|RESTORE|DBCC|KILL|SHUTDOWN|BULK|OPENROWSET|OPENQUERY|OPENDATASOURCE|WAITFOR|USE|RECONFIGURE'
strip_sql_comments() { # ${1}=檔案 → stdout（去掉 /* … */ 與 -- … 註解）
  perl -0777 -pe 's{/\*.*?\*/}{ }gs; s{--[^\n]*}{}g' "${1}"
}
assert_snapshot_sql_readonly() { # ${1}=SQL 檔
  [[ -f "${1}" ]] || die "找不到 ${1}"
  local body hits
  body="$(strip_sql_comments "${1}")"
  hits="$(printf '%s\n' "${body}" | grep -Eiwn "(${FORBIDDEN_WORDS})" || true)"
  if [[ -n "${hits}" ]]; then
    die "schema-snapshot.sql 含寫入／執行類關鍵字，拒絕執行（唯讀保證第 2 層）：$(printf '%s' "${hits}" | head -3 | tr '\n' ' ')"
  fi
  hits="$(printf '%s\n' "${body}" | grep -Eiwn '(xp|sp)_[a-z0-9_]+' || true)"
  if [[ -n "${hits}" ]]; then
    die "schema-snapshot.sql 含 xp_／sp_ 預存程序呼叫，拒絕執行（唯讀保證第 2 層）：$(printf '%s' "${hits}" | head -3 | tr '\n' ' ')"
  fi
  grep -Eiq '\bSELECT\b' <<<"${body}" || die "schema-snapshot.sql 沒有任何 SELECT，不像快照腳本"
}

# ── sqlcmd（在容器內執行；連線資訊走環境變數）────────────────────────────────────
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

# 唯讀包裝檔：NOCOUNT（sqlcmd 13 防呆）→ 隔離層級／逾時／死結優先權 → BEGIN TRAN → :r 快照 → ROLLBACK
write_wrapper() { # ${1}=主機路徑 ${2}=容器內快照路徑
  {
    printf 'SET NOCOUNT ON;\nGO\n'
    printf 'SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;\nSET LOCK_TIMEOUT 10000;\nSET DEADLOCK_PRIORITY LOW;\nGO\n'
    printf 'BEGIN TRANSACTION;\nGO\n'
    printf ':r %s\n' "${2}"
    printf 'GO\nROLLBACK TRANSACTION;\nGO\n'
  } >"${1}"
}

# 從 sqlcmd 輸出取出快照行：去標題底線／空行／資訊訊息，去尾端空白與 BOM；保留原順序（欄序可能有意義）
# 🔴 sqlcmd 13 搭配 -y 0 會在少數行尾多吐一個 NUL（\0）（實測 tcrfc_club 4763 行中約 1–2 行，位置不固定）；
#    剔除 NUL 後與 mssql-tools18 的輸出逐行相同。為免「NUL 其實取代了字元」被悄悄吞掉，
#    只接受「NUL 緊接換行」（純行尾填充）；NUL 出現在行中間就拒絕。
extract_snapshot() { # ${1}=原始輸出 ${2}=快照檔
  if ! perl -0777 -ne 'exit(/\x00[^\n]/ ? 1 : 0)' "${1}"; then
    die "sqlcmd 輸出的 NUL 出現在行中間（不是行尾填充）——內容可能被截斷，拒絕輸出"
  fi
  local nul
  nul="$(tr -cd '\000' <"${1}" | wc -c | tr -d ' ')"
  if [[ "${nul}" -gt 0 ]]; then
    echo "   [注意] sqlcmd 13 在 ${nul} 個行尾多吐了 NUL，已剔除（已確認都在行尾）" >&2
  fi
  tr -d '\000' <"${1}" | sed -e $'1s/^\xEF\xBB\xBF//' -e 's/[[:space:]]*$//' \
    | grep -Ev '^-+$|^$|^Changed database context|^\([0-9]+ rows? affected\)$' >"${2}" || true
}

# 發佈前掃描：格式與機密。命中就失敗（呼叫端負責刪檔）
scan_snapshot() { # ${1}=快照檔
  local f="${1}" bad short
  [[ -s "${f}" ]] || die "從 ${DB_NAME} 抽出的快照是空的（連線失敗或庫沒有任何物件）"
  bad="$(grep -Evn '^[A-Za-z0-9_]+\|' "${f}" | cut -d: -f1 | head -10 | tr '\n' ' ' || true)"
  [[ -z "${bad}" ]] || die "快照含不符「類別|…」格式的行（行號：${bad}）——為免把非預期內容發佈出去，拒絕輸出"
  short="${DB_HOST%%.*}"
  local -a needles=("${DB_HOST}" "${DB_PASSWORD}")
  [[ ${#short} -ge 8 ]] && needles+=("${short}")
  [[ ${#DB_USER} -ge 4 ]] && needles+=("${DB_USER}")
  local n
  for n in "${needles[@]}"; do
    if grep -Fq -- "${n}" "${f}"; then
      die "快照含連線資訊（主機／帳號／密碼之一）——拒絕輸出（內容不印出）"
    fi
  done
  if grep -Eiq 'database\.windows\.net|(password|pwd|user id|server|accountkey|sharedaccesssignature)[[:space:]]*=|(^|[^0-9.])([0-9]{1,3}\.){3}[0-9]{1,3}([^0-9.]|$)' "${f}"; then
    die "快照含疑似連線字串／IP 的內容——拒絕輸出（內容不印出）"
  fi
}

snapshot_one() { # ${1}=target ${2}=輸出目錄
  target_defaults "${1}"
  load_connection
  info "${T}：資料庫 ${DB_NAME}（唯讀快照）"

  local raw="${WORK_DIR}/${T}.raw" err="${WORK_DIR}/${T}.err" wrapper="${WORK_DIR}/${T}.wrapper.sql" out="${2}/${T}.snapshot.txt"
  write_wrapper "${wrapper}" /work/schema-snapshot.sql
  # -y 0＝取消 256 字元的欄位截斷（CHECK／索引定義可能很長）。sqlcmd 13：-y 與 -h -1／-W 互斥，所以事後濾標題底線與空白。
  SQLCMD_DOCKER_EXTRA=(-v "${SNAPSHOT_SQL}:/work/schema-snapshot.sql:ro" -v "${wrapper}:/wrap/snapshot.sql:ro")
  local rc=0
  sqlcmd_container -I -y 0 -i /wrap/snapshot.sql >"${raw}" 2>"${err}" || rc=$?
  SQLCMD_DOCKER_EXTRA=()
  if [[ ${rc} -ne 0 ]] || sqlcmd_log_has_error "${raw}" || sqlcmd_log_has_error "${err}"; then
    echo "--- sqlcmd 輸出（已遮蔽連線資訊；結束碼 ${rc}）---" >&2
    { head -c 4000 "${raw}"; head -c 2000 "${err}"; } | redact | head -30 >&2
    die "${T}：從 ${DB_NAME} 抽快照失敗（sqlcmd 13 結束碼不可信，E-295；已同時檢查輸出中的錯誤字樣）"
  fi

  extract_snapshot "${raw}" "${WORK_DIR}/${T}.snapshot.txt"
  scan_snapshot "${WORK_DIR}/${T}.snapshot.txt"
  mv "${WORK_DIR}/${T}.snapshot.txt" "${out}"
  chmod 644 "${out}"
  local lines sha
  lines="$(wc -l <"${out}" | tr -d ' ')"
  sha="$(sha256_file "${out}")"
  ok "${T}：${lines} 行（SHA-256 ${sha}）"
  say ""
  say "### ${T}（資料庫 \`${DB_NAME}\`）"
  say "- 快照 ${lines} 行，SHA-256：\`${sha}\`"
  say "- 各類別行數（結構名稱與定義，不含資料）："
  cut -d'|' -f1 "${out}" | sort | uniq -c | sed -E 's/^ *([0-9]+) (.*)$/  - \2：\1/' | while IFS= read -r l; do say "${l}"; done
  {
    printf '%s\t%s\t%s\n' "${T}" "${DB_NAME}" "${lines}"
  } >>"${2}/.manifest.tsv"
}

# ── main ──────────────────────────────────────────────────────────────────────
[[ $# -eq 2 ]] || usage 1
TARGET_ARG="${1}"
OUT_DIR="${2}"
case "${TARGET_ARG}" in
  club) TARGETS=(club) ;;
  charity) TARGETS=(charity) ;;
  both) TARGETS=(club charity) ;;
  *) die "目標只能是 club、charity 或 both（收到「${TARGET_ARG}」）" ;;
esac
[[ "${OUT_DIR}" == /* && "${OUT_DIR}" != *..* ]] || die "輸出目錄必須是絕對路徑且不可含 .."
[[ "${OUT_DIR}" =~ ^/[A-Za-z0-9._/-]+$ ]] || die "輸出目錄含不允許的字元（只接受英數字、. _ - /）"

command -v docker >/dev/null 2>&1 || die "找不到 docker"
docker info >/dev/null 2>&1 || die "docker 無法使用（runner 使用者要在 docker 群組）"
command -v perl >/dev/null 2>&1 || die "找不到 perl（唯讀靜態檢查需要）"

say "## 正式庫結構快照（唯讀）"
say "- 只執行 \`deploy/schema-snapshot.sql\`（SELECT sys.*），包在 BEGIN TRANSACTION … ROLLBACK；不含資料。"

assert_snapshot_sql_readonly "${SNAPSHOT_SQL}"
ok "schema-snapshot.sql 通過唯讀靜態檢查"

WORK_DIR="$(mktemp -d)"
chmod 700 "${WORK_DIR}"
mkdir -p "${OUT_DIR}"
rm -f "${OUT_DIR}"/*.snapshot.txt "${OUT_DIR}/.manifest.tsv" "${OUT_DIR}/MANIFEST.txt" "${OUT_DIR}/SHA256SUMS"

for t in "${TARGETS[@]}"; do
  snapshot_one "${t}" "${OUT_DIR}"
done

{
  printf '# prod-schema-snapshot MANIFEST\n'
  printf 'tool=deploy/prod-schema-snapshot.sh\n'
  printf 'taken_at_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  printf 'git_sha=%s\n' "${GITHUB_SHA:-unknown}"
  printf 'run_id=%s\n' "${GITHUB_RUN_ID:-local}"
  printf 'rehearsal=%s\n' "${REHEARSAL}"
  printf '# target<TAB>database<TAB>lines\n'
  cat "${OUT_DIR}/.manifest.tsv"
} >"${OUT_DIR}/MANIFEST.txt"
rm -f "${OUT_DIR}/.manifest.tsv"
(
  cd "${OUT_DIR}"
  for f in *.snapshot.txt MANIFEST.txt; do
    printf '%s  %s\n' "$(sha256_file "${f}")" "${f}"
  done >SHA256SUMS
)
say ""
say "完成：${OUT_DIR}"
