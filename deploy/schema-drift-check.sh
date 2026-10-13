#!/usr/bin/env bash
# deploy/schema-drift-check.sh — 資料庫結構的「兩個真實來源」一致性檢查（docs/20-cicd.md §3「schema-drift」、docs/18 E-321）
#
# 為什麼要有這支：資料庫結構有兩個真實來源，會靜靜分岔：
#   ① db/club-schema.sql／db/charity-schema.sql（手寫 DDL）——正式庫（deploy/prod-db-init.sh）與本機建庫都用它；
#      建完後 prod-db-init 把「全部」EF migration 標記為已套用，一支都沒真的執行。
#   ② EF 模型（ClubDbContext／CharityDbContext）＋ migration——程式讀寫資料庫時相信的結構。
# 既有的 `dotnet ef migrations has-pending-model-changes` 只比「模型 vs migration snapshot」，看不到 ①。
# 改結構時漏改 DDL（正式庫就沒有新欄位）或漏寫 migration（既有庫升級不到），兩邊就悄悄分岔。
#
# 為什麼不能「從空庫跑全部 migration」當基準：InitialBaseline 的 Up()／Down() 是空的（資料庫本來就是 DDL 建的），
# 跑完得到的是空庫。所以基準改為「EF 模型」本身：
#   A 庫 = DDL 原樣建庫（等同正式庫 prod-db-init.sh init 的第 1 步結果；migration 全部視為已套用，與 init 一致）
#   B 庫 = 由 EF 模型產生（`dotnet ef dbcontext script` 的 create script）
# 兩庫各抽一份正規化快照（deploy/schema-snapshot.sql），用 deploy/schema-diff.py 比對。
#
# 模式：
#   ddl-vs-ef（預設）  A 庫 vs B 庫。CI 與本機都用；建四個一次性的 tcrfc_drift_* 庫，跑完丟掉。
#   local-vs-ddl       本機現況 vs DDL：把本機 tcrfc_club（或 tcrfc_charity）和「原樣 DDL 新建的庫」比，
#                      用來發現「本機庫落後」——db/seed/setup-club-db.sh 遇到已有資料表會略過 DDL，
#                      DDL 後來改了本機卻沒重建，兩邊就分岔。對本機現有庫只做 SELECT（唯讀）。
#
# 用法：
#   deploy/schema-drift-check.sh [--mode ddl-vs-ef|local-vs-ddl] [--target club|charity|all] [--keep] [--dry-run]
#   --target   預設 all。
#   --keep     跑完保留 tcrfc_drift_* 庫（除錯用）；預設一律丟掉。
#   --dry-run  只做前置檢查（檔案、工具、容器）並印出會做什麼，不建庫、不連線寫入。
#
# 環境變數：
#   LOCAL_MSSQL_CONTAINER   目標 SQL Server 容器（預設 sqlserver；CI 是 mssql-ci）。與 local-ddl.sh 同名同義。
#   MSSQL_DEV_SA_PASSWORD   該容器的 SA 密碼（必填，不印出、不進命令列：以 docker -e SQLCMDPASSWORD 傳入）。
#
# 🔴 安全模型（比照 deploy/local-ddl.sh）：
#   - 本腳本「會寫」的庫名寫死只有四個：tcrfc_drift_club_ddl、tcrfc_drift_club_ef、tcrfc_drift_charity_ddl、
#     tcrfc_drift_charity_ef（先 DROP 再 CREATE 的只有這四個，且每次破壞性操作前都過 assert_scratch_db）。
#   - 本腳本「只讀」的既有庫只有 tcrfc_club、tcrfc_charity（local-vs-ddl 模式），只執行 deploy/schema-snapshot.sql
#     （純 SELECT sys.*）。沒有任何程式路徑對這兩個庫或該 instance 上其他庫做 DDL／DML／DROP。
#   - 執行前先印出「容器與庫」，不悄悄動手。
#
# 結束碼：0 一致｜1 有結構差異（差異清單印在輸出）｜2 環境／用法問題（不能當作一致，也不能當作差異）。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
LOCAL_MSSQL_CONTAINER="${LOCAL_MSSQL_CONTAINER:-sqlserver}"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

# ⛔ 唯一允許被 DROP／CREATE／灌 DDL 的庫（一次性）
SCRATCH_DBS=(tcrfc_drift_club_ddl tcrfc_drift_club_ef tcrfc_drift_charity_ddl tcrfc_drift_charity_ef)
# ⛔ 唯一允許被「讀」的既有庫（local-vs-ddl 模式；只跑 SELECT）
READONLY_DBS=(tcrfc_club tcrfc_charity)

MODE="ddl-vs-ef"; TARGET="all"; KEEP=0; DRY=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode) MODE="${2:-}"; shift 2 ;;
    --target) TARGET="${2:-}"; shift 2 ;;
    --keep) KEEP=1; shift ;;
    --dry-run) DRY=1; shift ;;
    -h|--help) sed -n '1,45p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "不認得的參數：$1（見 --help）" >&2; exit 2 ;;
  esac
done
[[ "${MODE}" == "ddl-vs-ef" || "${MODE}" == "local-vs-ddl" ]] || { echo "--mode 只接受 ddl-vs-ef 或 local-vs-ddl" >&2; exit 2; }
[[ "${TARGET}" == "club" || "${TARGET}" == "charity" || "${TARGET}" == "all" ]] || { echo "--target 只接受 club、charity、all" >&2; exit 2; }

in_list() { local x="$1"; shift; for i in "$@"; do [[ "$i" == "$x" ]] && return 0; done; return 1; }
assert_scratch_db() { in_list "$1" "${SCRATCH_DBS[@]}" || { echo "拒絕：'$1' 不在一次性庫白名單（${SCRATCH_DBS[*]}），不得建立／丟棄／寫入。" >&2; exit 2; }; }
assert_readable_db() { if in_list "$1" "${SCRATCH_DBS[@]}" || in_list "$1" "${READONLY_DBS[@]}"; then return 0; fi
  echo "拒絕：'$1' 不在可讀白名單。" >&2; exit 2; }

die() { echo "❌ $*" >&2; exit 2; }

# ── 前置檢查 ──────────────────────────────────────────────────────────────────
[[ -f "${REPO_ROOT}/db/club-schema.sql" && -f "${REPO_ROOT}/db/charity-schema.sql" ]] || die "找不到 db/club-schema.sql 或 db/charity-schema.sql"
[[ -f "${SCRIPT_DIR}/schema-snapshot.sql" && -f "${SCRIPT_DIR}/schema-diff.py" ]] || die "找不到 deploy/schema-snapshot.sql 或 schema-diff.py"
command -v docker >/dev/null || die "找不到 docker"
command -v python3 >/dev/null || die "找不到 python3"
if [[ "${MODE}" == "ddl-vs-ef" ]]; then
  command -v dotnet >/dev/null || die "ddl-vs-ef 需要 dotnet（dotnet ef dbcontext script）"
fi

targets=(); case "${TARGET}" in all) targets=(club charity) ;; *) targets=("${TARGET}") ;; esac

echo "==> schema-drift-check：模式 ${MODE}｜目標 ${targets[*]}｜容器 ${LOCAL_MSSQL_CONTAINER}"
echo "    會寫的一次性庫：$(printf '%s ' "${SCRATCH_DBS[@]}")"
[[ "${MODE}" == "local-vs-ddl" ]] && echo "    只讀的既有庫：$(printf '%s ' "${READONLY_DBS[@]}")（僅 SELECT sys.*）"

if [[ "${DRY}" -eq 1 ]]; then
  echo "==> --dry-run：前置檢查通過，不連線、不建庫。"
  bash -n "${BASH_SOURCE[0]}" && echo "    bash 語法 OK"
  python3 -I -m py_compile "${SCRIPT_DIR}/schema-diff.py" && echo "    schema-diff.py 語法 OK"
  rm -rf "${SCRIPT_DIR}/__pycache__"
  exit 0
fi

CID="$(docker ps -q --filter "name=^/${LOCAL_MSSQL_CONTAINER}\$" || true)"
[[ -n "${CID}" ]] || die "找不到執行中的容器 '${LOCAL_MSSQL_CONTAINER}'（docker ps）。可用 LOCAL_MSSQL_CONTAINER 指定。"
: "${MSSQL_DEV_SA_PASSWORD:?請設定 MSSQL_DEV_SA_PASSWORD（該容器的 SA 密碼）}"

WORK="$(mktemp -d "${TMPDIR:-/tmp}/tcrfc-schema-drift.XXXXXX")"
cleanup() {
  if [[ "${KEEP}" -eq 0 ]]; then
    for d in "${SCRATCH_DBS[@]}"; do drop_scratch "$d" 2>/dev/null || true; done
  fi
  rm -rf "${WORK}"
}
trap cleanup EXIT

# sql_exec <db> [sqlcmd 參數…]：SQL 由 stdin 或 -Q 帶入；SA 密碼走環境變數，不出現在命令列
sql_exec() { local db="$1"; shift
  docker exec -i -e SQLCMDPASSWORD="${MSSQL_DEV_SA_PASSWORD}" "${CID}" "${SQLCMD}" -S localhost -U sa -C -d "${db}" -f 65001 -b -I "$@"; }

drop_scratch() { local db="$1"; assert_scratch_db "${db}"
  sql_exec master -Q "IF DB_ID('${db}') IS NOT NULL BEGIN ALTER DATABASE [${db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${db}]; END;" >/dev/null; }
create_scratch() { local db="$1"; assert_scratch_db "${db}"; drop_scratch "${db}"
  sql_exec master -Q "CREATE DATABASE [${db}];" >/dev/null; }

# 🔴 DDL 不得使用原生 json 型別（2026-10-13 使用者決定：JSON 內容欄位一律 nvarchar(max) ＋ 具名 ISJSON CHECK；docs/14、docs/17 §6 第 2 項）。
# 這道檢查不需要連線，放在建庫之前：DDL 又出現 json 欄位就直接失敗（結束碼 1）並說明怎麼改。
reject_native_json() { # reject_native_json <club|charity>
  local src="${REPO_ROOT}/db/$1-schema.sql"
  if grep -nE '^[[:space:]]+[a-z_0-9]+[[:space:]]+json([[:space:]]|,|$)' "${src}"; then
    echo "🔴 schema-drift-check 失敗：${src} 含原生 json 型別欄位（上列）。" >&2
    echo "   2026-10-13 起資料庫不使用原生 json：欄位宣告為 nvarchar(max) NULL CONSTRAINT CK_<表>_<欄>_json CHECK (ISJSON(<欄>, OBJECT|ARRAY) = 1)，" >&2
    echo "   同時在 EF 模型（ClubDbContextJsonChecks／CharityDbContextCustomizations）與 migration 補同名 CHECK（docs/14、docs/20 §5）。" >&2
    exit 1
  fi
}
ddl_for() { echo "${REPO_ROOT}/db/$1-schema.sql"; } # DDL 原樣建庫（不再有 json→nvarchar 的轉換副本）

snapshot() { # snapshot <db> <輸出檔>
  local db="$1" out="$2"; assert_readable_db "${db}"
  # -y 0 取消 256 字元截斷（CHECK／索引定義可能很長），但它與 -h -1 互斥，所以事後濾掉標題列的底線與空行。
  sql_exec "${db}" -y 0 < "${SCRIPT_DIR}/schema-snapshot.sql" > "${out}.raw" || { head -20 "${out}.raw" >&2; die "從 ${db} 抽快照失敗"; }
  grep -Ev '^-+$|^[[:space:]]*$' "${out}.raw" > "${out}" || true
  [[ -s "${out}" ]] || die "從 ${db} 抽出的快照是空的"
}

ef_script() { # ef_script <club|charity> <輸出檔>：dotnet ef dbcontext script（不連資料庫，只看模型）
  local which="$1" out="$2" ctx
  [[ "${which}" == "club" ]] && ctx="ClubDbContext" || ctx="CharityDbContext"
  # 建置 DbContext 需要連線字串與 JWT 金鑰「存在」（Program.cs 啟動檢查）；值是假的，dbcontext script 不連線。
  (
    cd "${REPO_ROOT}/apps/api"
    export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=unused;User Id=sa;Password=unused;TrustServerCertificate=True;"
    export CHARITY_SQL_CONNECTION_STRING="${CLUB_SQL_CONNECTION_STRING}"
    export JWT_SIGNING_KEY_CLUB="ci-only-jwt-signing-key-club-0123456789abcdef"
    export JWT_SIGNING_KEY_CHARITY="ci-only-jwt-signing-key-charity-0123456789abcdef"
    dotnet tool restore >/dev/null
    dotnet ef dbcontext script --context "${ctx}" -o "${out}" >/dev/null
  )
  [[ -s "${out}" ]] || die "dotnet ef dbcontext script 沒有產出 ${ctx} 的 create script"
  sed -i.bak $'1s/^\xEF\xBB\xBF//' "${out}" && rm -f "${out}.bak"   # EF 輸出帶 UTF-8 BOM，sqlcmd 讀 stdin 不吃
}

rc=0
for which in "${targets[@]}"; do
  reject_native_json "${which}"
  a_db="tcrfc_drift_${which}_ddl"
  echo
  echo "════════ ${which} ════════"
  echo "==> 建 A 庫 ${a_db}（DDL：db/${which}-schema.sql）"
  create_scratch "${a_db}"
  sql_exec "${a_db}" < "$(ddl_for "${which}")" > "${WORK}/${which}-ddl.log" || { head -40 "${WORK}/${which}-ddl.log" >&2; die "DDL 在 ${a_db} 執行失敗（DDL 本身有錯，與本檢查無關）"; }
  snapshot "${a_db}" "${WORK}/${which}.a.txt"

  if [[ "${MODE}" == "ddl-vs-ef" ]]; then
    b_db="tcrfc_drift_${which}_ef"
    echo "==> 建 B 庫 ${b_db}（EF 模型：dotnet ef dbcontext script）"
    ef_script "${which}" "${WORK}/${which}-ef.sql"
    create_scratch "${b_db}"
    sql_exec "${b_db}" < "${WORK}/${which}-ef.sql" > "${WORK}/${which}-ef.log" || { head -40 "${WORK}/${which}-ef.log" >&2; die "EF create script 在 ${b_db} 執行失敗"; }
    snapshot "${b_db}" "${WORK}/${which}.b.txt"
    a_label="DDL（db/${which}-schema.sql）"; b_label="EF 模型"; dmode="ef"
  else
    live="tcrfc_${which}"
    echo "==> 抽 B 側：本機現有庫 ${live}（唯讀）"
    snapshot "${live}" "${WORK}/${which}.b.txt"
    a_label="DDL（db/${which}-schema.sql）"; b_label="本機 ${live}"; dmode="same-origin"
  fi

  python3 -I "${SCRIPT_DIR}/schema-diff.py" --a-label "${a_label}" --b-label "${b_label}" --mode "${dmode}" \
    "${WORK}/${which}.a.txt" "${WORK}/${which}.b.txt" || { r=$?; [[ ${r} -eq 1 ]] && rc=1 || exit ${r}; }
done

echo
if [[ ${rc} -eq 0 ]]; then echo "✅ schema-drift-check 通過"; else
  echo "🔴 schema-drift-check 失敗：結構有分岔。改結構必須 DDL（db/*.sql）與 EF 模型＋migration 兩邊一起改（docs/14、docs/20 §3「schema-drift」）。"; fi
exit ${rc}
