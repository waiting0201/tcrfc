#!/usr/bin/env bash
# deploy/prod-schema-compare.sh — 把「正式庫結構快照」與「今天的 DDL 新建的庫」或「本機庫」比對（docs/20-cicd.md §5「prod-schema-snapshot.yml」）
#
# 快照檔由 .github/workflows/prod-schema-snapshot.yml 在 VM 上以 deploy/prod-schema-snapshot.sh 產生，artifact 下載回本機。
# 本腳本不連正式庫、不連網路；只在本機的 SQL Server 容器上動作：
#
#   --against ddl（預設）  先用 db/<target>-schema.sql 在本機容器新建一個一次性庫（tcrfc_prodcmp_<target>），抽快照，丟掉。
#                          這回答「正式庫現在的結構，和今天 repo 的 DDL 一不一樣」。
#   --against local        抽本機 tcrfc_<target> 的快照（只 SELECT）。這回答「本機和正式庫一不一樣」。
#
# 用法：
#   deploy/prod-schema-compare.sh <club|charity> <正式庫快照檔> [--against ddl|local]
#   例：deploy/prod-schema-compare.sh club /tmp/prod-snap/club.snapshot.txt
#
# 環境變數：
#   LOCAL_MSSQL_CONTAINER   目標 SQL Server 容器（預設 sqlserver；與 local-ddl.sh、schema-drift-check.sh 同義）
#   MSSQL_DEV_SA_PASSWORD   該容器的 SA 密碼（必填；不印出、不進命令列，以 docker exec -e 傳入）
#
# 🔴 安全模型（比照 schema-drift-check.sh）：
#   - 會寫（先 DROP 再 CREATE）的庫只有 tcrfc_prodcmp_club／tcrfc_prodcmp_charity 兩個一次性庫，每次破壞性操作前過白名單；
#     該 instance 上其他專案的資料庫、tcrfc_club、tcrfc_charity 都不會被寫。
#   - 唯一讀的既有庫是 tcrfc_club／tcrfc_charity（--against local），只執行 deploy/schema-snapshot.sql（SELECT sys.*）。
#   - 快照檔是下載來的外部資料：先驗檔案大小與每行格式（「類別|…」），並核對同目錄的 SHA256SUMS（若有）；
#     比對用 `python3 -I`（不載入快照所在目錄或目前目錄的模組）。
#
# 比對本體是 deploy/schema-diff.py（--mode same-origin：正式庫與 DDL 同源，不排除任何東西）。
# 結束碼：0 一致｜1 有結構差異｜2 環境／用法／快照檔問題（不能當作一致，也不能當作差異）。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
LOCAL_MSSQL_CONTAINER="${LOCAL_MSSQL_CONTAINER:-sqlserver}"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
SCRATCH_DBS=(tcrfc_prodcmp_club tcrfc_prodcmp_charity) # ⛔ 唯一允許被 DROP／CREATE／灌 DDL 的庫
READONLY_DBS=(tcrfc_club tcrfc_charity)                 # ⛔ 唯一允許被「讀」的既有庫

die() { echo "❌ $*" >&2; exit 2; }
usage() { sed -n '2,19p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit "${1:-2}"; }

[[ "${1:-}" == "-h" || "${1:-}" == "--help" ]] && usage 0
[[ $# -ge 2 ]] || usage 2
WHICH="${1}"
PROD_FILE="${2}"
shift 2
AGAINST="ddl"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --against) AGAINST="${2:-}"; shift 2 ;;
    *) die "不認得的參數：$1（見 --help）" ;;
  esac
done
[[ "${WHICH}" == "club" || "${WHICH}" == "charity" ]] || die "目標只能是 club 或 charity"
[[ "${AGAINST}" == "ddl" || "${AGAINST}" == "local" ]] || die "--against 只接受 ddl 或 local"

in_list() { local x="$1"; shift; local i; for i in "$@"; do [[ "$i" == "$x" ]] && return 0; done; return 1; }
assert_scratch_db() { in_list "$1" "${SCRATCH_DBS[@]}" || die "拒絕：'$1' 不在一次性庫白名單（${SCRATCH_DBS[*]}），不得建立／丟棄／寫入。"; }
assert_readable_db() { in_list "$1" "${SCRATCH_DBS[@]}" || in_list "$1" "${READONLY_DBS[@]}" || die "拒絕：'$1' 不在可讀白名單。"; }

# ── 前置檢查 ──────────────────────────────────────────────────────────────────
DDL="${REPO_ROOT}/db/${WHICH}-schema.sql"
[[ -f "${DDL}" && -f "${SCRIPT_DIR}/schema-snapshot.sql" && -f "${SCRIPT_DIR}/schema-diff.py" ]] || die "找不到 ${DDL}、deploy/schema-snapshot.sql 或 schema-diff.py"
command -v docker >/dev/null || die "找不到 docker"
command -v python3 >/dev/null || die "找不到 python3"

# 快照檔檢查（下載來的外部資料）
[[ -f "${PROD_FILE}" && ! -L "${PROD_FILE}" ]] || die "找不到快照檔（或它是符號連結）：${PROD_FILE}"
size="$(wc -c <"${PROD_FILE}" | tr -d ' ')"
[[ "${size}" -gt 0 && "${size}" -le 5000000 ]] || die "快照檔大小不合理（${size} bytes；預期 > 0 且 ≤ 5MB）"
if bad="$(grep -Evn '^[A-Za-z0-9_]+\|' "${PROD_FILE}" | cut -d: -f1 | head -5 | tr '\n' ' ')" && [[ -n "${bad}" ]]; then
  die "快照檔含不符「類別|…」格式的行（行號：${bad}）——不是 prod-schema-snapshot 的產物？"
fi
sums="$(dirname "${PROD_FILE}")/SHA256SUMS"
if [[ -f "${sums}" ]]; then
  want="$(awk -v f="$(basename "${PROD_FILE}")" '$2 == f { print $1; exit }' "${sums}")"
  if command -v sha256sum >/dev/null 2>&1; then got="$(sha256sum <"${PROD_FILE}" | cut -d' ' -f1)"; else got="$(shasum -a 256 <"${PROD_FILE}" | cut -d' ' -f1)"; fi
  [[ -n "${want}" ]] || die "SHA256SUMS 沒有 $(basename "${PROD_FILE}") 的紀錄"
  [[ "${want}" == "${got}" ]] || die "快照檔的 SHA-256 與 SHA256SUMS 不符——檔案被改過或下載不完整"
  echo "    SHA-256 與 SHA256SUMS 相符：${got}"
else
  echo "    ⚠️ 同目錄沒有 SHA256SUMS，未核對完整性（建議與 workflow 的 job summary 上的 SHA-256 對照）"
fi

CID="$(docker ps -q --filter "name=^/${LOCAL_MSSQL_CONTAINER}\$" || true)"
[[ -n "${CID}" ]] || die "找不到執行中的容器 '${LOCAL_MSSQL_CONTAINER}'（docker ps）。可用 LOCAL_MSSQL_CONTAINER 指定。"
: "${MSSQL_DEV_SA_PASSWORD:?請設定 MSSQL_DEV_SA_PASSWORD（該容器的 SA 密碼）}"

SCRATCH="tcrfc_prodcmp_${WHICH}"
LIVE="tcrfc_${WHICH}"
echo "==> prod-schema-compare：${WHICH}｜比對對象 ${AGAINST}｜容器 ${LOCAL_MSSQL_CONTAINER}"
if [[ "${AGAINST}" == "ddl" ]]; then
  echo "    會寫的一次性庫：${SCRATCH}（跑完丟掉）"
else
  echo "    只讀的既有庫：${LIVE}（僅 SELECT sys.*）"
fi

WORK="$(mktemp -d "${TMPDIR:-/tmp}/tcrfc-prod-compare.XXXXXX")"
cleanup() {
  if [[ "${AGAINST}" == "ddl" ]]; then drop_scratch "${SCRATCH}" 2>/dev/null || true; fi
  rm -rf "${WORK}"
}
trap cleanup EXIT

sql_exec() { local db="$1"; shift
  docker exec -i -e SQLCMDPASSWORD="${MSSQL_DEV_SA_PASSWORD}" "${CID}" "${SQLCMD}" -S localhost -U sa -C -d "${db}" -f 65001 -b -I "$@"; }
drop_scratch() { local db="$1"; assert_scratch_db "${db}"
  sql_exec master -Q "IF DB_ID('${db}') IS NOT NULL BEGIN ALTER DATABASE [${db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${db}]; END;" >/dev/null; }
create_scratch() { local db="$1"; assert_scratch_db "${db}"; drop_scratch "${db}"
  sql_exec master -Q "CREATE DATABASE [${db}];" >/dev/null; }

# 與 prod-schema-snapshot.sh 同一套濾法（-y 0 取消截斷；濾標題底線、空白、資訊訊息；去尾端空白）
snapshot() { # snapshot <db> <輸出檔>
  local db="$1" out="$2"; assert_readable_db "${db}"
  sql_exec "${db}" -y 0 <"${SCRIPT_DIR}/schema-snapshot.sql" >"${out}.raw" || { head -20 "${out}.raw" >&2; die "從 ${db} 抽快照失敗"; }
  tr -d '\000' <"${out}.raw" | sed -e 's/[[:space:]]*$//' | grep -Ev '^-+$|^$|^Changed database context|^\([0-9]+ rows? affected\)$' >"${out}" || true
  [[ -s "${out}" ]] || die "從 ${db} 抽出的快照是空的"
}

if [[ "${AGAINST}" == "ddl" ]]; then
  echo "==> 建一次性庫 ${SCRATCH}（DDL：db/${WHICH}-schema.sql）"
  create_scratch "${SCRATCH}"
  sql_exec "${SCRATCH}" <"${DDL}" >"${WORK}/ddl.log" || { head -40 "${WORK}/ddl.log" >&2; die "DDL 在 ${SCRATCH} 執行失敗（DDL 本身有錯）"; }
  snapshot "${SCRATCH}" "${WORK}/local.txt"
  b_label="今天的 DDL（db/${WHICH}-schema.sql 新建的庫）"
else
  snapshot "${LIVE}" "${WORK}/local.txt"
  b_label="本機 ${LIVE}"
fi

rc=0
python3 -I "${SCRIPT_DIR}/schema-diff.py" --a-label "正式庫（快照檔）" --b-label "${b_label}" --mode same-origin \
  "${PROD_FILE}" "${WORK}/local.txt" || rc=$?
rm -rf "${SCRIPT_DIR}/__pycache__"
case "${rc}" in
  0) echo "✅ 正式庫 ${WHICH} 與 ${b_label} 結構一致" ;;
  1) echo "🔴 正式庫 ${WHICH} 與 ${b_label} 結構有差異（見上方清單）" ;;
  *) echo "❌ 比對本身失敗（結束碼 ${rc}）" ;;
esac
exit "${rc}"
