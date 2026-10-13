#!/usr/bin/env bash
# shellcheck disable=SC2319,SC2046,SC2155 # 測試檔刻意把「條件的結果」傳給 expect；export 內的指令替換結果不需檢查
# deploy/test-prod-schema-snapshot.sh — 以「假 docker」驗證 deploy/prod-schema-snapshot.sh 的防呆（不碰任何資料庫或網路）。
#
#   /bin/bash deploy/test-prod-schema-snapshot.sh      （建議用 macOS 內建 bash 3.2 跑）
#
# 腳本本體會被複製到暫存的 deploy/ 目錄，並配上「自訂的 schema-snapshot.sql」，所以能測靜態唯讀檢查而不動真檔。
# 假 docker 把掛進去的 wrapper 內容存起來、依 FAKE_MODE 回應 sqlcmd 的各種輸出形狀。

set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(mktemp -d)"
trap 'rm -rf "${ROOT}"' EXIT
PASS=0
FAIL=0
ok() { PASS=$((PASS + 1)); printf '  ok   %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL %s\n' "$1"; }
expect() { if [ "$2" -eq 0 ]; then ok "$1"; else bad "$1"; fi; }
has() { grep -qF -- "$2" "$1"; }
hasnt() { ! grep -qF -- "$2" "$1"; }

SECRET_PW='Sup3r-Secret-Pw!42'
HOST='sqlfakeserver123.database.windows.net'
SQLUSER='sqladmin'

# ── 假 docker ─────────────────────────────────────────────────────
FAKEBIN="${ROOT}/bin"
mkdir -p "${FAKEBIN}"
cat >"${FAKEBIN}/docker" <<'EOF'
#!/usr/bin/env bash
echo "docker $*" >>"${FAKE_DIR}/calls"
[ "${1:-}" = info ] && exit 0
[ "${1:-}" = run ] || exit 0
wrap=""
args="$*"
for a in "$@"; do
  case "$a" in
    *:/wrap/snapshot.sql:ro) wrap="${a%%:/wrap/snapshot.sql:ro}" ;;
  esac
done
[ -n "${wrap}" ] && cp "${wrap}" "${FAKE_DIR}/wrapper.sql"
case "${args}" in *" -y 0 "*) ;; *) echo "FAKE: 缺 -y 0" >>"${FAKE_DIR}/problems" ;; esac
case "${args}" in *" -I "*) ;; *) echo "FAKE: 缺 -I" >>"${FAKE_DIR}/problems" ;; esac
case "${args}" in *" -f 65001"*) echo "FAKE: 不可有 -f（sqlcmd 13 不認得）" >>"${FAKE_DIR}/problems" ;; esac
case "${FAKE_MODE:-ok}" in
  ok)
    printf 'TABLE|pages\nCOL|pages|id|uniqueidentifier|NOT NULL||\nIDX|pages|id|unique=1|clustered|include=|where=\n'
    printf 'CK|pages|status IN (1,2)\0\n' # 行尾 NUL（sqlcmd 13 的真實行為）
    printf -- '------\n\n' ;;
  nul_mid) printf 'TABLE|pa\0ges\nTABLE|x\n' ;;
  leak_pw) printf 'TABLE|pages\nDEFAULT|pages|note|%s\n' "${FAKE_PW}" ;;
  leak_host) printf 'TABLE|pages\nDEFAULT|pages|note|%s\n' "${FAKE_HOST}" ;;
  leak_ip) printf 'TABLE|pages\nDEFAULT|pages|note|10.1.2.3\n' ;;
  junk) printf 'TABLE|pages\nthis is not a snapshot line\n' ;;
  empty) printf '\n' ;;
  login_fail)
    printf 'Sqlcmd: Error: Microsoft ODBC Driver 13 for SQL Server : Login failed for user %s.\n' "${FAKE_USER}"
    printf "Msg 40615, Level 14, State 1, Server %s, Line 1\nCannot open server '%s' requested by the login. Client with IP address '203.0.113.7' is not allowed. pw=%s\n" "${FAKE_SHORT}" "${FAKE_SHORT}" "${FAKE_PW}"
    exit 1 ;;
  exit0_sqlstate) printf 'TABLE|pages\nSqlState 24000, Invalid cursor state\n'; exit 0 ;;
esac
exit 0
EOF
chmod +x "${FAKEBIN}/docker"
export PATH="${FAKEBIN}:${PATH}"
export FAKE_PW="${SECRET_PW}" FAKE_HOST="${HOST}" FAKE_USER="${SQLUSER}" FAKE_SHORT="${HOST%%.*}"

# ── 暫存的 deploy/ 目錄（可換 schema-snapshot.sql）──────────────────────
D="${ROOT}/deploy"
mkdir -p "${D}/lib"
cp "${HERE}/prod-schema-snapshot.sh" "${D}/"
cp "${HERE}/lib/sqlcmd-guard.sh" "${D}/lib/"
SCRIPT="${D}/prod-schema-snapshot.sh"
REAL_SQL="${HERE}/schema-snapshot.sql"

new_case() { # 名稱 [sql 檔]
  CASE="${ROOT}/case-$1"
  rm -rf "${CASE}"
  mkdir -p "${CASE}/secrets" "${CASE}/fake" "${CASE}/out"
  export FAKE_DIR="${CASE}/fake"
  : >"${FAKE_DIR}/calls"
  cp "${2:-${REAL_SQL}}" "${D}/schema-snapshot.sql"
  unset DBM_REHEARSAL DBM_TRUST_CERT DBM_EXPECT_DB_CLUB DBM_EXPECT_DB_CHARITY
  export FAKE_MODE=ok
  export TCRFC_SECRETS_DIR="${CASE}/secrets"
  export GITHUB_STEP_SUMMARY="${CASE}/summary.md"
  printf "CLUB_SQL_CONNECTION_STRING='Server=tcp:%s,1433;Initial Catalog=tcrfc_club;User ID=%s;Password=%s;Encrypt=True;'\n" "${HOST}" "${SQLUSER}" "${SECRET_PW}" >"${CASE}/secrets/club.env"
  printf "CHARITY_SQL_CONNECTION_STRING='Server=tcp:%s,1433;Initial Catalog=tcrfc_charity;User ID=%s;Password=%s;Encrypt=True;'\n" "${HOST}" "${SQLUSER}" "${SECRET_PW}" >"${CASE}/secrets/charity.env"
  chmod 600 "${CASE}/secrets/"*.env
}
run() { /bin/bash "${SCRIPT}" "$@" >"${CASE}/stdout" 2>"${CASE}/stderr"; }
all_text() { cat "${CASE}/stdout" "${CASE}/stderr" "${CASE}/summary.md" 2>/dev/null; }

echo "== 正常路徑（真實的 schema-snapshot.sql 必須通過靜態唯讀檢查）"
new_case ok
run both "${CASE}/out"; rc=$?
expect "both 成功" $((rc != 0))
expect "club 快照存在且行尾 NUL 已剔除" $([ -s "${CASE}/out/club.snapshot.txt" ] && [ "$(tr -cd '\000' <"${CASE}/out/club.snapshot.txt" | wc -c | tr -d ' ')" = 0 ]; echo $?)
expect "快照保留 CK 行（NUL 前的內容完整）" $(has "${CASE}/out/club.snapshot.txt" 'CK|pages|status IN (1,2)'; echo $?)
expect "快照沒有標題底線／空行" $(grep -Eq '^-+$|^$' "${CASE}/out/club.snapshot.txt"; [ $? -eq 1 ]; echo $?)
expect "charity 快照存在" $([ -s "${CASE}/out/charity.snapshot.txt" ]; echo $?)
expect "MANIFEST.txt 與 SHA256SUMS 存在" $([ -s "${CASE}/out/MANIFEST.txt" ] && [ -s "${CASE}/out/SHA256SUMS" ]; echo $?)
expect "SHA256SUMS 驗得過" $( (cd "${CASE}/out" && { shasum -a 256 -c SHA256SUMS >/dev/null 2>&1 || sha256sum -c SHA256SUMS >/dev/null 2>&1; }); echo $?)
expect "wrapper 在交易內且最後 ROLLBACK" $( (has "${FAKE_DIR}/wrapper.sql" 'BEGIN TRANSACTION' && has "${FAKE_DIR}/wrapper.sql" 'ROLLBACK TRANSACTION' && has "${FAKE_DIR}/wrapper.sql" 'READ UNCOMMITTED' && has "${FAKE_DIR}/wrapper.sql" 'SET NOCOUNT ON' && has "${FAKE_DIR}/wrapper.sql" 'LOCK_TIMEOUT'); echo $?)
expect "假 docker 沒回報旗標問題（-y 0／-I／無 -f）" $([ ! -s "${FAKE_DIR}/problems" ]; echo $?)
expect "輸出與 summary 不含密碼" $(all_text | grep -qF "${SECRET_PW}"; [ $? -eq 1 ]; echo $?)
expect "輸出與 summary 不含主機名稱" $(all_text | grep -qF "${HOST%%.*}"; [ $? -eq 1 ]; echo $?)
expect "summary 有 SHA-256 與類別計數" $( (has "${CASE}/summary.md" 'SHA-256' && has "${CASE}/summary.md" 'COL：'); echo $?)

SQLN=0
LEAKN=0
echo "== 唯讀靜態檢查"
bad_sql() { # 名稱 內容 → 應被拒絕
  SQLN=$((SQLN + 1)); new_case "sql${SQLN}"
  printf '%s\n' "$2" >"${CASE}/s.sql"
  cp "${CASE}/s.sql" "${D}/schema-snapshot.sql"
  run club "${CASE}/out"; rc=$?
  expect "拒絕：$1" $((rc == 0))
  expect "拒絕：$1（沒有呼叫 docker run）" $(grep -q 'docker run' "${FAKE_DIR}/calls"; [ $? -eq 1 ]; echo $?)
}
good_sql() { # 名稱 內容 → 應通過
  SQLN=$((SQLN + 1)); new_case "sql${SQLN}"
  printf '%s\n' "$2" >"${CASE}/s.sql"
  cp "${CASE}/s.sql" "${D}/schema-snapshot.sql"
  run club "${CASE}/out"; rc=$?
  expect "通過：$1" $((rc != 0)); [ "${rc}" -eq 0 ] || cat "${CASE}/stderr"
}
# new_case 會把真檔複製回去，所以 bad_sql／good_sql 要在 new_case 之後覆蓋（上面已處理）
bad_sql "DROP TABLE" $'SELECT 1;\nDROP TABLE pages;'
bad_sql "INSERT" $'SELECT 1;\nINSERT INTO t VALUES (1);'
bad_sql "UPDATE" $'SELECT 1;\nUPDATE t SET a = 1;'
bad_sql "DELETE" $'SELECT 1;\nDELETE FROM t;'
bad_sql "ALTER" $'SELECT 1;\nALTER TABLE t ADD c int;'
bad_sql "CREATE" $'SELECT 1;\nCREATE TABLE t (a int);'
bad_sql "SELECT INTO" $'SELECT a INTO #t FROM sys.tables;'
bad_sql "EXEC sp_" $'SELECT 1;\nEXEC sp_who;'
bad_sql "EXEC (動態 SQL)" $'EXEC (\'SELECT 1\');'
bad_sql "sp_ 呼叫（小寫）" $'SELECT 1; sp_executesql N\'select 1\';'
bad_sql "TRUNCATE" $'SELECT 1;\nTRUNCATE TABLE t;'
bad_sql "USE" $'USE master;\nSELECT 1;'
bad_sql "WAITFOR" $'SELECT 1;\nWAITFOR DELAY \'00:00:10\';'
bad_sql "OPENROWSET" $'SELECT * FROM OPENROWSET(\'x\',\'y\',\'z\');'
bad_sql "沒有 SELECT" $'SET NOCOUNT ON;'
good_sql "註解裡的 DROP／ALTER 不算" $'/* DROP TABLE x; ALTER */\n-- INSERT INTO\nSELECT fk.update_referential_action_desc, fk.delete_referential_action_desc, o.create_date FROM sys.foreign_keys fk JOIN sys.objects o ON 1=1;'

echo "== 連線防呆"
new_case host_prod
printf "CLUB_SQL_CONNECTION_STRING='Server=tcp:localhost,1433;Initial Catalog=tcrfc_club;User ID=sa;Password=%s;'\n" "${SECRET_PW}" >"${CASE}/secrets/club.env"
run club "${CASE}/out"; rc=$?
expect "正式模式拒絕非 *.database.windows.net" $((rc == 0))
new_case host_reh
export DBM_REHEARSAL=1
run club "${CASE}/out"; rc=$?
expect "DBM_REHEARSAL=1 拒絕連 *.database.windows.net" $((rc == 0))
expect "拒絕時沒有呼叫 docker run" $(grep -q 'docker run' "${FAKE_DIR}/calls"; [ $? -eq 1 ]; echo $?)
new_case dbname
printf "CLUB_SQL_CONNECTION_STRING='Server=tcp:%s,1433;Initial Catalog=tcrfc_charity;User ID=%s;Password=%s;'\n" "${HOST}" "${SQLUSER}" "${SECRET_PW}" >"${CASE}/secrets/club.env"
run club "${CASE}/out"; rc=$?
expect "club 目標指到 tcrfc_charity 被拒絕" $((rc == 0))
new_case override
export DBM_TRUST_CERT=1
run club "${CASE}/out"; rc=$?
expect "沒有 DBM_REHEARSAL 就不允許 DBM_TRUST_CERT" $((rc == 0))
new_case override2
export DBM_EXPECT_DB_CLUB=other
run club "${CASE}/out"; rc=$?
expect "沒有 DBM_REHEARSAL 就不允許 DBM_EXPECT_DB_CLUB" $((rc == 0))
new_case reh_ok
printf "CLUB_SQL_CONNECTION_STRING='Server=tcp:localhost,1433;Initial Catalog=rehearsal_club;User ID=sa;Password=%s;'\n" "${SECRET_PW}" >"${CASE}/secrets/club.env"
export DBM_REHEARSAL=1 DBM_TRUST_CERT=1 DBM_EXPECT_DB_CLUB=rehearsal_club
run club "${CASE}/out"; rc=$?
expect "演練模式可連本機、可改庫名" $((rc != 0))

echo "== 輸入驗證"
new_case args
run xyz "${CASE}/out"; rc=$?; expect "target 只接受 club／charity／both" $((rc == 0))
run club relative/dir; rc=$?; expect "輸出目錄必須是絕對路徑" $((rc == 0))
run club "/tmp/a/../b"; rc=$?; expect "輸出目錄不可含 .." $((rc == 0))
run club "/tmp/a b"; rc=$?; expect "輸出目錄不可含空白" $((rc == 0))
run club; rc=$?; expect "缺參數" $((rc == 0))

echo "== 外洩與壞輸出（皆須失敗且不留下輸出檔）"
leak_case() { # 名稱 FAKE_MODE
  LEAKN=$((LEAKN + 1)); new_case "leak${LEAKN}"
  export FAKE_MODE="$2"
  run club "${CASE}/out"; rc=$?
  expect "$1：失敗" $((rc == 0))
  expect "$1：沒有留下快照檔" $([ ! -e "${CASE}/out/club.snapshot.txt" ]; echo $?)
  expect "$1：輸出不含密碼／主機／IP" $( (all_text | grep -qF "${SECRET_PW}") || (all_text | grep -qF "${HOST%%.*}") || (all_text | grep -qF '203.0.113.7'); [ $? -eq 1 ]; echo $?)
}
leak_case "快照含密碼" leak_pw
leak_case "快照含主機名稱" leak_host
leak_case "快照含 IP" leak_ip
leak_case "快照含非格式行" junk
leak_case "快照是空的" empty
leak_case "NUL 出現在行中間" nul_mid
leak_case "登入失敗（錯誤輸出要遮蔽）" login_fail
leak_case "結束碼 0 但輸出有 SqlState（E-295）" exit0_sqlstate
new_case login_redact
export FAKE_MODE=login_fail
run club "${CASE}/out"
expect "登入失敗時 stderr 有提供遮蔽後的錯誤線索" $(has "${CASE}/stderr" 'Msg 40615'; echo $?)

echo
echo "通過 ${PASS}，失敗 ${FAIL}"
[ "${FAIL}" -eq 0 ]
