#!/usr/bin/env bash
# shellcheck disable=SC2319,SC2046,SC2155 # 測試檔刻意把「條件的結果」傳給 expect（SC2319／SC2046）；export 內的指令替換結果不需檢查（SC2155）
# deploy/test-db-migrate.sh — 以「假 docker（模擬 sqlcmd 的真實回應形狀）」驗證 deploy/db-migrate.sh 的 pending／apply 主要路徑。
#
# 不碰任何真實資料庫或網路。建議用 macOS 內建的 /bin/bash（3.2）跑，涵蓋 E-108 的語法陷阱：
#   /bin/bash deploy/test-db-migrate.sh
# 假 docker 以「歷史表檔案」為狀態：-Q 查詢回傳 DB_NAME()／表是否存在／筆數／MigrationId 清單，
# -i 套用檔案時依檔內 N'<MigrationId>' 補寫歷史（與 EF idempotent 腳本同語意），並檢查有帶 -I、沒帶 -f（真實 Linux sqlcmd 不認得 -f）。
# 情境（E-115 教訓：假物件要長得像真的）：歷史表已存在且 pending 為零、有 pending、套用成功、套用失敗（整支回滾）、
#   套用到一半失敗（前面已提交）、核准後名單變了、both 其中一個失敗、庫名不符、非 Azure 主機、artifact 被改、
#   歷史表比 repo 多、輸出被截斷、歷史表不存在、輸入驗證、密碼與主機名稱不外洩。

set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SCRIPT="${HERE}/db-migrate.sh"
REPO_ROOT="$(cd "${HERE}/.." && pwd)"
ROOT="$(mktemp -d)"
trap 'rm -rf "${ROOT}"' EXIT

PASS=0
FAIL=0
ok() { PASS=$((PASS + 1)); printf '  ok   %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL %s\n' "$1"; }
expect() { # 描述 條件（0 通過）
  if [ "$2" -eq 0 ]; then ok "$1"; else bad "$1"; fi
}
has() { grep -qF -- "$2" "$1"; }   # 檔案 字串
hasnt() { ! grep -qF -- "$2" "$1"; }

sha() { if command -v sha256sum >/dev/null 2>&1; then sha256sum | cut -d' ' -f1; else shasum -a 256 | cut -d' ' -f1; fi; }

SECRET_PW='Sup3r-Secret-Pw!42'
HOST_CLUB='sql-fake-xyz123.database.windows.net'

FAKEBIN="${ROOT}/bin"
mkdir -p "${FAKEBIN}"

# ── 假 docker ─────────────────────────────────────────────────────
cat > "${FAKEBIN}/docker" << 'EOF'
#!/usr/bin/env bash
echo "docker $*" >> "${FAKE_DIR}/calls"
[ "${1:-}" = info ] && exit 0
[ "${1:-}" = run ] || exit 0
shift
vol=""
while [ $# -gt 0 ]; do
  case "$1" in
    -v) vol="$2"; shift 2 ;;
    -e) shift 2 ;;
    */sqlcmd) shift; break ;;
    *) shift ;;
  esac
done
query="" file="" has_I=0
while [ $# -gt 0 ]; do
  case "$1" in
    -Q) query="$2"; shift 2 ;;
    -i) file="$2"; shift 2 ;;
    -I) has_I=1; shift ;;
    -f*) echo "Sqlcmd: '${1#-}${2:-}': Unknown Option. Enter '-?' for help."; exit 1 ;;   # 真實 Linux sqlcmd 不支援 -f（本機演練實測）
    *) shift ;;
  esac
done
hist="${FAKE_DIR}/history.${SQLCMDDBNAME}"
[ -f "${hist}" ] || : > "${hist}"
if [ -n "${query}" ]; then
  case "${query}" in
    *"DB_NAME()"*) printf '%s\n' "${FAKE_DB_NAME:-${SQLCMDDBNAME}}"; exit 0 ;;
    *"sys.tables WHERE name = '__EFMigrationsHistory'"*)
      if [ "${FAKE_NO_HISTORY:-}" = 1 ]; then echo 0; else echo 1; fi; exit 0 ;;
    *"COUNT(*) FROM __EFMigrationsHistory"*) echo "$(wc -l < "${hist}" | tr -d ' ')"; exit 0 ;;
    *"SELECT MigrationId"*)
      if [ "${FAKE_TRUNCATED:-}" = 1 ]; then head -n 2 "${hist}"; else cat "${hist}"; fi
      echo; exit 0 ;;
  esac
  exit 0
fi
if [ -n "${file}" ]; then
  [ "${has_I}" = 1 ] || { echo "fake: 缺 -I" >&2; exit 9; }
  host_dir="${vol%%:*}"
  sql="${host_dir}/$(basename "${file}")"
  [ -f "${sql}" ] || { echo "fake: 容器內找不到 ${file}" >&2; exit 8; }
  echo "applied:${SQLCMDDBNAME}" >> "${FAKE_DIR}/applied"
  ids="$(grep -o "N'[0-9]\{14\}_[A-Za-z0-9_]*'" "${sql}" | tr -d "N'" | sort -u)"
  n=0
  for id in ${ids}; do
    grep -qxF "${id}" "${hist}" && continue
    n=$((n + 1))
    if [ "${FAKE_APPLY_FAIL:-}" = 1 ]; then
      echo "Msg 1913, Level 16, State 1, Server fake, Line 12"
      echo "The operation failed because an index or statistics with name 'IX_fake' already exists on table 'fake'."
      exit 1
    fi
    if [ "${FAKE_APPLY_PARTIAL:-}" = 1 ] && [ "${n}" -ge 2 ]; then
      echo "Msg 207, Level 16, State 1, Server fake, Line 99"
      echo "Invalid column name 'nope'."
      exit 1
    fi
    echo "${id}" >> "${hist}"
    sort -o "${hist}" "${hist}"
  done
  echo "Changed database context to '${SQLCMDDBNAME}'."
  exit 0
fi
exit 0
EOF
chmod +x "${FAKEBIN}/docker"
export PATH="${FAKEBIN}:${PATH}"

# ── repo 內的真實 migration 清單 ────────────────────────────────────
ids_of() { # 目錄
  grep -hoE '\[Migration\("[0-9]{14}_[A-Za-z0-9_]+"\)\]' "$1"/*.Designer.cs | sed -E 's/^\[Migration\("(.*)"\)\]$/\1/' | LC_ALL=C sort -u
}
CLUB_IDS="$(ids_of "${REPO_ROOT}/apps/api/Data/Migrations")"
CHARITY_IDS="$(ids_of "${REPO_ROOT}/apps/api/CharityPlatform/Data/Migrations")"
N_CLUB="$(printf '%s\n' "${CLUB_IDS}" | grep -c .)"
N_CHARITY="$(printf '%s\n' "${CHARITY_IDS}" | grep -c .)"
LAST_CLUB="$(printf '%s\n' "${CLUB_IDS}" | tail -n 1)"
LAST2_CLUB="$(printf '%s\n' "${CLUB_IDS}" | tail -n 2)"
LAST_CHARITY="$(printf '%s\n' "${CHARITY_IDS}" | tail -n 1)"
[ "${N_CLUB}" -ge 3 ] && [ "${N_CHARITY}" -ge 2 ] || { echo "repo 的 migration 數量異常（club ${N_CLUB}／charity ${N_CHARITY}）"; exit 2; }

# ── 建立一個情境：secrets、artifact、歷史表 ─────────────────────────
new_case() { # 名稱
  CASE="${ROOT}/case-$1"
  rm -rf "${CASE}"
  mkdir -p "${CASE}/fake" "${CASE}/secrets" "${CASE}/sql"
  export FAKE_DIR="${CASE}/fake"
  : > "${FAKE_DIR}/calls"
  unset FAKE_DB_NAME FAKE_NO_HISTORY FAKE_APPLY_FAIL FAKE_APPLY_PARTIAL FAKE_TRUNCATED \
        DBM_REHEARSAL DBM_TRUST_CERT DBM_EXPECT_DB_CLUB DBM_EXPECT_DB_CHARITY \
        DBM_EXPECT_SHA_CLUB DBM_EXPECT_SHA_CHARITY DBM_EXPECT_PENDING_HASH_CLUB DBM_EXPECT_PENDING_HASH_CHARITY DBM_DRY_RUN
  export TCRFC_SECRETS_DIR="${CASE}/secrets"
  export DBM_SQL_DIR="${CASE}/sql"
  printf "CLUB_SQL_CONNECTION_STRING='Server=tcp:%s,1433;Initial Catalog=tcrfc_club;User ID=sqladmin;Password=%s;Encrypt=True;'\n" "${HOST_CLUB}" "${SECRET_PW}" > "${CASE}/secrets/club.env"
  printf "CHARITY_SQL_CONNECTION_STRING='Server=tcp:%s,1433;Initial Catalog=tcrfc_charity;User ID=sqladmin;Password=%s;Encrypt=True;'\n" "${HOST_CLUB}" "${SECRET_PW}" > "${CASE}/secrets/charity.env"
  chmod 600 "${CASE}/secrets/"*.env
  export GITHUB_STEP_SUMMARY="${CASE}/summary.md"
  export GITHUB_OUTPUT="${CASE}/output.txt"
  : > "${GITHUB_STEP_SUMMARY}"
  : > "${GITHUB_OUTPUT}"
  make_artifact club "${CLUB_IDS}"
  make_artifact charity "${CHARITY_IDS}"
  set_history club "${CLUB_IDS}"
  set_history charity "${CHARITY_IDS}"
}

make_artifact() { # target ids
  local t="$1" ids="$2" id d="${CASE}/sql/$1"
  mkdir -p "${d}/per-migration"
  : > "${d}/idempotent.sql"
  for id in ${ids}; do
    printf "IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'%s')\nBEGIN\n  PRINT 'x';\nEND;\nGO\n" "${id}" >> "${d}/idempotent.sql"
    printf "ALTER TABLE t_%s ADD c int NULL; -- 預覽 SQL\n" "${id}" > "${d}/per-migration/${id}.sql"
  done
  ( cd "${d}" && find . -type f ! -name SHA256SUMS | LC_ALL=C sort | while IFS= read -r f; do
      printf '%s  %s\n' "$(sha < "${f}")" "${f}"
    done > SHA256SUMS )
  local up
  up="$(echo "$t" | tr '[:lower:]' '[:upper:]')"
  export "DBM_EXPECT_SHA_${up}=$(sha < "${d}/SHA256SUMS")"
}

set_history() { # target ids
  local db
  case "$1" in club) db=tcrfc_club ;; charity) db=tcrfc_charity ;; esac
  printf '%s\n' "$2" | grep . > "${FAKE_DIR}/history.${db}" || : > "${FAKE_DIR}/history.${db}"
}

pending_hash_of() { printf '%s\n' "$1" | grep . | LC_ALL=C sort | sha; }

OUT=""
run() { # 參數交給 db-migrate.sh；輸出存 OUT 檔，回傳結束碼
  OUT="${CASE}/stdout.txt"
  /bin/bash "${SCRIPT}" "$@" > "${OUT}" 2>&1
  return $?
}
out_get() { grep "^$1=" "${GITHUB_OUTPUT}" | tail -n 1 | cut -d= -f2-; }
applies() { # 假 docker 實際執行 sqlcmd -i 的次數（檔案不存在＝0）
  if [ -f "${FAKE_DIR}/applied" ]; then grep -c '^applied:' "${FAKE_DIR}/applied"; else echo 0; fi
}

echo "== 語法 =="
/bin/bash -n "${SCRIPT}"; expect "bash -n 通過（macOS /bin/bash 3.2）" $?

# ────────────────────────────────────────────────────────────────
echo "== pending：歷史表完整、pending 為零 =="
new_case p0
run pending both; rc=$?
expect "結束碼 0" $((rc != 0))
expect "has_pending=false" $([ "$(out_get has_pending)" = false ]; echo $?)
expect "club_count=0、charity_count=0" $([ "$(out_get club_count)" = 0 ] && [ "$(out_get charity_count)" = 0 ]; echo $?)
has "${GITHUB_STEP_SUMMARY}"  "沒有待套用的 migration"; expect "summary 寫明沒有待套用" $?
hasnt "${FAKE_DIR}/calls" " -i /work"; expect "沒有執行 sqlcmd -i（不寫入）" $?

echo "== pending：club 有 2 支 pending，charity 為零 =="
new_case p2
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d' | sed '$d')"
run pending both; rc=$?
expect "結束碼 0" $((rc != 0))
expect "has_pending=true" $([ "$(out_get has_pending)" = true ]; echo $?)
expect "club_count=2" $([ "$(out_get club_count)" = 2 ]; echo $?)
expect "charity_count=0" $([ "$(out_get charity_count)" = 0 ]; echo $?)
expect "club_hash＝兩支 pending 的雜湊" $([ "$(out_get club_hash)" = "$(pending_hash_of "${LAST2_CLUB}")" ]; echo $?)
while IFS= read -r id; do
  has "${GITHUB_STEP_SUMMARY}" "${id}"; expect "summary 列出 ${id}" $?
  has "${GITHUB_STEP_SUMMARY}" "ALTER TABLE t_${id}"; expect "summary 含 ${id} 的預覽 SQL" $?
done <<EOF
${LAST2_CLUB}
EOF
hasnt "${FAKE_DIR}/calls" " -i /work"; expect "唯讀：沒有 sqlcmd -i" $?
has "${GITHUB_STEP_SUMMARY}" "production-db"; expect "summary 提示會停在核准關卡" $?

echo "== pending：dry_run=true 的結語 =="
new_case pd
set_history charity "$(printf '%s\n' "${CHARITY_IDS}" | sed '$d')"
DBM_DRY_RUN=true run pending charity; rc=$?
expect "結束碼 0" $((rc != 0))
has "${GITHUB_STEP_SUMMARY}" "dry_run = true"; expect "summary 寫明 dry_run 到此為止" $?
expect "只查 charity：club_count 維持 0" $([ "$(out_get club_count)" = 0 ] && [ "$(out_get charity_count)" = 1 ]; echo $?)

echo "== pending：歷史表不存在（庫還沒初始化）=="
new_case pnh
export FAKE_NO_HISTORY=1
run pending club; rc=$?
expect "失敗" $((rc == 0))
has "${OUT}" "prod-db-init"; expect "指向 prod-db-init.sh" $?

echo "== pending：歷史表比 repo 多（資料庫比這個 commit 新）=="
new_case pu
set_history club "$(printf '%s\n%s\n' "${CLUB_IDS}" "29990101000000_FromTheFuture")"
run pending club; rc=$?
expect "失敗" $((rc == 0))
has "${OUT}" "29990101000000_FromTheFuture"; expect "點名 repo 不認得的 migration" $?

echo "== pending：sqlcmd 輸出被截斷（筆數不符）=="
new_case ptr
export FAKE_TRUNCATED=1
run pending club; rc=$?
expect "失敗，不在不確定的狀態下比對" $((rc == 0))
has "${OUT}" "不一致" 2>/dev/null || has "${OUT}" "截斷"; expect "訊息說明輸出被截斷" $?

# ────────────────────────────────────────────────────────────────
echo "== apply：套用成功 =="
new_case a1
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
HASH="$(pending_hash_of "${LAST_CLUB}")"
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="${HASH}" DBM_EXPECT_PENDING_HASH_CHARITY="$(pending_hash_of "")" run apply both; rc=$?
expect "結束碼 0" $((rc != 0))
expect "只對 club 執行一次 sqlcmd -i（charity 沒有 pending 不動）" $([ "$(applies)" = 1 ] && has "${FAKE_DIR}/applied" "applied:tcrfc_club"; echo $?)
has "${FAKE_DIR}/calls" "-I"; expect "sqlcmd 有帶 -I" $?
hasnt "${FAKE_DIR}/calls" " -f "; expect "sqlcmd 沒帶 -f（Linux 的 sqlcmd 不支援）" $?
has "${FAKE_DIR}/history.tcrfc_club" "${LAST_CLUB}"; expect "歷史表補上了 ${LAST_CLUB}" $?
has "${GITHUB_STEP_SUMMARY}" "套用完成並驗證"; expect "summary 寫明驗證通過" $?

echo "== apply：核准後歷史表被別人推進（名單變了）=="
new_case a2
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d' | sed '$d')"
OLD_HASH="$(pending_hash_of "${LAST2_CLUB}")"
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"   # 核准期間有人套了一支
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="${OLD_HASH}" run apply club; rc=$?
expect "拒絕" $((rc == 0))
expect "沒有執行 sqlcmd -i" $([ "$(applies)" = 0 ]; echo $?)
has "${OUT}" "核准"; expect "訊息說明是核准後名單變了" $?

echo "== apply：核准時 pending 為零、之後冒出一支 =="
new_case a2b
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "")" run apply club; rc=$?
expect "拒絕" $((rc == 0))
expect "沒有執行 sqlcmd -i" $([ "$(applies)" = 0 ]; echo $?)

echo "== apply：dry_run=true 只列出不執行 =="
new_case a3
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
DBM_DRY_RUN=true DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "${LAST_CLUB}")" run apply club; rc=$?
expect "結束碼 0" $((rc != 0))
expect "沒有執行 sqlcmd -i" $([ "$(applies)" = 0 ]; echo $?)
has "${GITHUB_STEP_SUMMARY}" "沒有執行任何寫入"; expect "summary 寫明沒有寫入" $?
hasnt "${FAKE_DIR}/history.tcrfc_club" "zzz"; expect "歷史表沒變" $([ "$(wc -l < "${FAKE_DIR}/history.tcrfc_club" | tr -d ' ')" = "$((N_CLUB - 1))" ]; echo $?)

echo "== apply：DBM_DRY_RUN 缺或不明 → 拒絕寫入 =="
new_case a4
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "${LAST_CLUB}")" run apply club; rc=$?
expect "沒設 DBM_DRY_RUN 拒絕" $((rc == 0))
DBM_DRY_RUN=maybe DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "${LAST_CLUB}")" run apply club; rc=$?
expect "DBM_DRY_RUN=maybe 拒絕" $((rc == 0))
expect "兩次都沒有寫入" $([ "$(applies)" = 0 ]; echo $?)

echo "== apply：套用失敗（整支回滾，歷史不變，不重試）=="
new_case a5
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
export FAKE_APPLY_FAIL=1
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "${LAST_CLUB}")" run apply club; rc=$?
expect "失敗（非零）" $((rc == 0))
expect "只嘗試一次，沒有重試" $([ "$(applies)" = 1 ]; echo $?)
has "${GITHUB_STEP_SUMMARY}" "套用失敗"; expect "summary 寫明失敗" $?
has "${GITHUB_STEP_SUMMARY}" "Msg 1913"; expect "summary 貼出 sqlcmd 錯誤" $?
has "${GITHUB_STEP_SUMMARY}" "下一步"; expect "summary 寫明下一步" $?
has "${GITHUB_STEP_SUMMARY}" "PITR"; expect "summary 提到 PITR" $?
has "${GITHUB_STEP_SUMMARY}" "仍待套用 1 支"; expect "summary 顯示失敗後仍待套用 1 支" $?
expect "歷史表沒有被補上失敗的那一支" $(grep -qxF "${LAST_CLUB}" "${FAKE_DIR}/history.tcrfc_club"; echo $((! $?)))

echo "== apply：兩支 pending，第二支失敗（第一支已提交）=="
new_case a6
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d' | sed '$d')"
export FAKE_APPLY_PARTIAL=1
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "${LAST2_CLUB}")" run apply club; rc=$?
expect "失敗" $((rc == 0))
FIRST="$(printf '%s\n' "${LAST2_CLUB}" | head -n 1)"
expect "第一支已寫入歷史、第二支沒有" $(grep -qxF "${FIRST}" "${FAKE_DIR}/history.tcrfc_club" && ! grep -qxF "${LAST_CLUB}" "${FAKE_DIR}/history.tcrfc_club"; echo $?)
has "${GITHUB_STEP_SUMMARY}" "仍待套用 1 支"; expect "summary 顯示仍待套用 1 支" $?

echo "== apply：both，club 失敗 → charity 完全不碰 =="
new_case a7
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
set_history charity "$(printf '%s\n' "${CHARITY_IDS}" | sed '$d')"
export FAKE_APPLY_FAIL=1
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "${LAST_CLUB}")" DBM_EXPECT_PENDING_HASH_CHARITY="$(pending_hash_of "${LAST_CHARITY}")" run apply both; rc=$?
expect "失敗" $((rc == 0))
expect "只對 club 嘗試一次，charity 沒有被執行" $([ "$(applies)" = 1 ] && has "${FAKE_DIR}/applied" "applied:tcrfc_club" && ! has "${FAKE_DIR}/applied" "tcrfc_charity"; echo $?)

echo "== apply：both，兩個都有 pending，依序成功 =="
new_case a8
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
set_history charity "$(printf '%s\n' "${CHARITY_IDS}" | sed '$d')"
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "${LAST_CLUB}")" DBM_EXPECT_PENDING_HASH_CHARITY="$(pending_hash_of "${LAST_CHARITY}")" run apply both; rc=$?
expect "結束碼 0" $((rc != 0))
expect "兩個庫各套用一次，club 在前" $([ "$(applies)" = 2 ] && [ "$(head -n 1 "${FAKE_DIR}/applied")" = "applied:tcrfc_club" ]; echo $?)

echo "== apply：核准時有一個庫的 pending 為零，且之後仍為零 =="
new_case a9
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "")" DBM_EXPECT_PENDING_HASH_CHARITY="$(pending_hash_of "")" run apply both; rc=$?
expect "結束碼 0，沒有執行任何東西" $([ "${rc}" = 0 ] && [ "$(applies)" = 0 ]; echo $?)

# ────────────────────────────────────────────────────────────────
echo "== 防呆：庫名、主機、覆寫 =="
new_case g1
printf "CLUB_SQL_CONNECTION_STRING='Server=tcp:%s,1433;Initial Catalog=tcrfc_charity;User ID=u;Password=%s;'\n" "${HOST_CLUB}" "${SECRET_PW}" > "${CASE}/secrets/club.env"
run pending club; rc=$?
expect "club 目標連到 tcrfc_charity 的連線字串 → 拒絕" $((rc == 0))
expect "拒絕發生在任何 docker run 之前" $(grep -q '^docker run' "${FAKE_DIR}/calls"; echo $((! $?)))

new_case g2
printf "CLUB_SQL_CONNECTION_STRING='Server=tcp:evil.example.com,1433;Initial Catalog=tcrfc_club;User ID=u;Password=%s;'\n" "${SECRET_PW}" > "${CASE}/secrets/club.env"
run pending club; rc=$?
expect "非 *.database.windows.net 主機 → 拒絕" $((rc == 0))
expect "拒絕發生在任何 docker run 之前" $(grep -q '^docker run' "${FAKE_DIR}/calls"; echo $((! $?)))

new_case g3
DBM_TRUST_CERT=1 run pending club; rc=$?
expect "沒有 DBM_REHEARSAL 就不允許 DBM_TRUST_CERT" $((rc == 0))
new_case g3b
DBM_EXPECT_DB_CLUB=other run pending club; rc=$?
expect "沒有 DBM_REHEARSAL 就不允許 DBM_EXPECT_DB_CLUB" $((rc == 0))
new_case g3c
DBM_REHEARSAL=1 run pending club; rc=$?
expect "DBM_REHEARSAL=1 時拒絕連 *.database.windows.net" $((rc == 0))

new_case g4
export FAKE_DB_NAME=master
run pending club; rc=$?
expect "連上後 DB_NAME() 與設定不符 → 拒絕" $((rc == 0))

echo "== 防呆：artifact 完整性 =="
new_case t1
echo "DROP TABLE admin_users;" >> "${DBM_SQL_DIR}/club/idempotent.sql"
run pending club; rc=$?
expect "idempotent.sql 被改 → 拒絕" $((rc == 0))
new_case t2
echo "x" > "${DBM_SQL_DIR}/club/extra.sql"
run pending club; rc=$?
expect "多出 SHA256SUMS 沒列的檔案 → 拒絕" $((rc == 0))
new_case t3
export DBM_EXPECT_SHA_CLUB="$(printf 'a' | sha)"
run pending club; rc=$?
expect "雜湊與 preview job 輸出不符 → 拒絕" $((rc == 0))
new_case t4
export DBM_EXPECT_SHA_CLUB="oops"
run pending club; rc=$?
expect "雜湊格式不對 → 拒絕" $((rc == 0))
new_case t5
rm "${DBM_SQL_DIR}/club/per-migration/${LAST_CLUB}.sql"
( cd "${DBM_SQL_DIR}/club" && find . -type f ! -name SHA256SUMS | LC_ALL=C sort | while IFS= read -r f; do printf '%s  %s\n' "$(sha < "${f}")" "${f}"; done > SHA256SUMS )
export DBM_EXPECT_SHA_CLUB="$(sha < "${DBM_SQL_DIR}/club/SHA256SUMS")"
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
run pending club; rc=$?
expect "pending 的那一支沒有預覽 SQL → 拒絕（核准者不能盲按）" $((rc == 0))
new_case t6
sed -i.bak "s/N'${LAST_CLUB}'/N'x'/" "${DBM_SQL_DIR}/club/idempotent.sql" && rm -f "${DBM_SQL_DIR}/club/idempotent.sql.bak"
( cd "${DBM_SQL_DIR}/club" && find . -type f ! -name SHA256SUMS | LC_ALL=C sort | while IFS= read -r f; do printf '%s  %s\n' "$(sha < "${f}")" "${f}"; done > SHA256SUMS )
export DBM_EXPECT_SHA_CLUB="$(sha < "${DBM_SQL_DIR}/club/SHA256SUMS")"
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
run pending club; rc=$?
expect "idempotent.sql 沒涵蓋 pending 的 migration（雜湊仍一致）→ 拒絕" $((rc == 0))

echo "== 防呆：輸入驗證 =="
new_case i1
run pending 'club; rm -rf /'; rc=$?
expect "target 含 shell 字元 → 拒絕" $((rc == 0))
run pending ''; rc=$?
expect "target 空 → 拒絕" $((rc == 0))
run bogus club; rc=$?
expect "未知子命令 → 拒絕" $((rc == 0))
DBM_SQL_DIR="relative/dir" run pending club; rc=$?
expect "DBM_SQL_DIR 非絕對路徑 → 拒絕" $((rc == 0))
new_case i2
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="not-a-hash" run apply club; rc=$?
expect "pending 雜湊格式不對 → 拒絕" $((rc == 0))

echo "== 機密不外洩 =="
new_case s1
set_history club "$(printf '%s\n' "${CLUB_IDS}" | sed '$d')"
set_history charity "$(printf '%s\n' "${CHARITY_IDS}" | sed '$d')"
export FAKE_APPLY_FAIL=1
DBM_DRY_RUN=false DBM_EXPECT_PENDING_HASH_CLUB="$(pending_hash_of "${LAST_CLUB}")" DBM_EXPECT_PENDING_HASH_CHARITY="$(pending_hash_of "${LAST_CHARITY}")" run apply both; rc=$?
for f in "${FAKE_DIR}/calls" "${OUT}" "${GITHUB_STEP_SUMMARY}" "${GITHUB_OUTPUT}"; do
  hasnt "${f}" "${SECRET_PW}"; expect "密碼不在 $(basename "${f}")" $?
  hasnt "${f}" "${HOST_CLUB}"; expect "伺服器主機名稱不在 $(basename "${f}")" $?
done
grep -q -- '-e SQLCMDPASSWORD' "${FAKE_DIR}/calls" && ! grep -q -- 'SQLCMDPASSWORD=' "${FAKE_DIR}/calls"; expect "docker 以 -e SQLCMDPASSWORD（不帶值）傳遞" $?

echo
echo "通過 ${PASS}／失敗 ${FAIL}"
[ "${FAIL}" -eq 0 ]
