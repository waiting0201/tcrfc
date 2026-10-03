#!/usr/bin/env bash
# deploy/prod-seed-import.sh — 把本機種子的「內容資料」匯入正式庫（Azure SQL），供前後台串接實機驗收；驗收完可全部清除
#
# 🔴 這是「驗收用的暫時性匯入」，不是上線資料。使用者 2026-10-03 決定：正式庫內容表全空，無法在正式機驗收前後台串接，
#    因此把本機種子的內容匯入、驗收完清除。界線（哪些匯入、哪些絕不匯入）定義在
#    db/seed/generate-prod-content-sql.py，說明見 docs/20-cicd.md §5「正式庫的內容種子匯入」與 db/seed/README.md。
#
# 在 VM 上以 runner 使用者執行（與 prod-db-init.sh 相同：讀 /opt/tcrfc/secrets/*.env、SQL 防火牆只放行 VM 子網）。
# 連線解析、sqlcmd 容器封裝、庫名與主機白名單、確認提示全部沿用 prod-db-init.sh（source 進來，不另抄一份）。
#
# 子命令（每次只處理一個庫）：
#   preflight  <club|charity>   唯讀：庫已初始化？尚未匯入？擁有的表是否全空？有沒有帳號夾帶？
#   import     <club|charity>   匯入內容。整份 SQL 在單一交易內（失敗整批回滾），最後寫入資料庫延伸屬性
#                                tcrfc.seed_import 作為「已匯入」標記；執行前要輸入「IMPORT <庫名>」確認。
#   verify     <club|charity>   唯讀：標記存在、各擁有表筆數＝manifest、clubs.domain 仍是現值，
#                                並呼叫 prod-db-init.sh 的 verify（含「沒有測試帳號」）。
#   status     <club|charity>   唯讀：只印目前是否已匯入與各表筆數，不判定對錯。
#   clean      <club|charity>   清除：只動「擁有的表」，依外鍵順序整批刪除、還原藍鯨簡介、移除標記；單一交易。
#                                執行前要輸入「CLEAN <庫名>」確認。
#                                ⚠️ 擁有的表在匯入後新增的列（驗收時手動建的內容）也會一起被刪；表裡多出的列會先列出來。
#                                ⛔ 若有「擁有的表以外」的表（例如前台真的有人填的試訓報名）仍參照這些列，清除會中止、不會動任何東西。
#   record-manifest <club|charity>  🔴 只給本機演練（PRODINIT_REHEARSAL=1）：把演練庫匯入後的實際筆數寫入
#                                db/prod/<庫>-content-manifest.tsv（連同 SQL 檔 sha256），之後 import／verify 以它為準。
#
# 環境變數：同 prod-db-init.sh（TCRFC_SECRETS_DIR／TCRFC_COMPOSE_ENV／MSSQL_TOOLS_IMAGE，以及僅限演練的 PRODINIT_*）。
#
# 為什麼不在每筆資料上打批次標記：種子的主鍵是 uuid5（由自然鍵決定，穩定），但 107 張表裡有複合主鍵、identity、
# 多列 VALUES，逐列記錄需要為正式庫新增物件。改用「表層級擁有權」——擁有的表在匯入前必須全空（preflight 檢查），
# 所以匯入後表內每一列都是這次匯入的；標記放在資料庫延伸屬性（不是資料表，不影響 prod-db-init.sh verify 的表數核對）。

set -euo pipefail

SCRIPT_DIR_SEED="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=prod-db-init.sh
source "${SCRIPT_DIR_SEED}/prod-db-init.sh"

PROP_NAME="tcrfc.seed_import"
CONTENT_FILE="" MANIFEST_FILE=""

usage_seed() {
  sed -n '2,30p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit "${1:-1}"
}

load_seed_target() {
  load_target "${1:-}"
  CONTENT_FILE="${REPO_ROOT}/db/prod/${TARGET}-content-seed.sql"
  MANIFEST_FILE="${REPO_ROOT}/db/prod/${TARGET}-content-manifest.tsv"
  [[ -f "${CONTENT_FILE}" ]] || die "找不到內容種子 SQL：${CONTENT_FILE}（先在 repo 根目錄執行 python3 db/seed/generate-prod-content-sql.py）"
}

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "${1}" | awk '{print $1}'; else shasum -a 256 "${1}" | awk '{print $1}'; fi
}

# 檔頭的機器可讀行 → 每行一個表名（只收合法識別字，防止檔案被改成帶 SQL 的表名）
header_tables() { # ${1}=OWNED|DENIED
  local t
  while IFS= read -r t; do
    [[ "${t}" =~ ^[a-z_0-9]+$ ]] || die "內容種子檔頭的表名不合法：${t}"
    printf '%s\n' "${t}"
  done < <(grep -E "^-- ${1} " "${CONTENT_FILE}" | sed -E "s/^-- ${1} //")
}

# 執行 SQL 檔並把 stdout 原樣回傳（run_sql_file 會吞掉輸出）；失敗時印出 sqlcmd 輸出並中止
run_sql_capture() { # ${1}=容器內路徑 ${2}=標題
  local out
  if ! out="$(sqlcmd_container -h -1 -W -s'|' -i "${1}" 2>&1)"; then
    echo "   sqlcmd 輸出最後 30 行：" >&2
    printf '%s\n' "${out}" | tail -n 30 | sed 's/^/     /' >&2
    die "${2} 失敗"
  fi
  printf '%s\n' "${out}"
}

# 每個表一列：「表名|筆數」。${1}=OWNED|DENIED
table_counts() {
  local kind="${1}" f="${WORK_DIR}/counts-${1}.sql" t first=1
  {
    echo "SET NOCOUNT ON;"
    while IFS= read -r t; do
      [[ -n "${t}" ]] || continue
      if [[ "${first}" -eq 1 ]]; then first=0; else echo "UNION ALL"; fi
      # 表不存在（例如慈善庫沒有的表）視為 0 不可能發生：DENIED／OWNED 都來自同一份 DDL 的表
      echo "SELECT N'${t}' AS t, COUNT_BIG(*) AS c FROM [${t}]"
    done < <(header_tables "${kind}")
    echo ";"
  } >"${f}"
  SQLCMD_DOCKER_EXTRA=(-v "${WORK_DIR}:/work/q:ro")
  run_sql_capture "/work/q/counts-${kind}.sql" "計算 ${kind} 表筆數" | grep -E '^[a-z_0-9]+\|[0-9]+$' || true
}

sum_counts() { awk -F'|' '{ s += $2 } END { print s + 0 }'; }

get_prop() { # 空字串＝沒有標記。值不含空白（sql_scalar 會去掉空白）
  sql_scalar "SELECT ISNULL((SELECT CAST(value AS nvarchar(400)) FROM sys.extended_properties WHERE class = 0 AND name = N'${PROP_NAME}'), N'')"
}

compose_value() { env_get "${COMPOSE_ENV}" "${1}"; }

bw_description_is_null() { # club 專用
  [[ "$(sql_scalar "SELECT COUNT(*) FROM clubs_i18n WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND locale = N'zh-Hant' AND description IS NOT NULL AND LEN(description) > 0")" == "0" ]]
}

# ── preflight ──────────────────────────────────────────────────────────────────
preflight_checks() { # 回傳 0＝可以匯入；非 0＝不可。印出原因
  local state prop owned_total fail=0
  state="$(db_state)"
  if [[ "${state}" != "initialized" ]]; then
    echo "   [不可] ${DB_NAME} 狀態是 ${state}，不是 initialized（先 prod-db-init.sh init／verify）" >&2; return 1
  fi
  ok "庫已初始化（有 __EFMigrationsHistory）"
  prop="$(get_prop)"
  if [[ -n "${prop}" ]]; then
    echo "   [不可] 已有匯入標記 ${PROP_NAME} = ${prop}；要重新匯入請先 clean ${TARGET}" >&2; return 1
  fi
  ok "尚無匯入標記"
  local counts
  counts="$(table_counts OWNED)"
  owned_total="$(printf '%s\n' "${counts}" | sum_counts)"
  if [[ "${owned_total}" != "0" ]]; then
    echo "   [不可] 擁有的表不是全空（合計 ${owned_total} 列）；非空的表：" >&2
    printf '%s\n' "${counts}" | awk -F'|' '$2 > 0 { printf "     %s = %s\n", $1, $2 }' >&2
    echo "   匯入只在這些表全空時執行（否則清除時無法區分哪些列是匯入的）。請先決定這些列怎麼處理，不要自行刪除。" >&2
    fail=1
  else
    ok "擁有的 $(header_tables OWNED | wc -l | tr -d ' ') 張表全空"
  fi
  if [[ "${TARGET}" == "club" ]] && ! bw_description_is_null; then
    echo "   [不可] clubs_i18n 藍鯨 zh-Hant 的 description 已有內容（匯入會覆寫、清除會設回 NULL）" >&2; fail=1
  fi
  local seed_admins
  seed_admins="$(sql_scalar "SELECT COUNT(*) FROM admin_users")"
  ok "admin_users 目前 ${seed_admins} 筆（匯入不會動帳號表）"
  if [[ ! -f "${MANIFEST_FILE}" ]]; then
    if [[ "${REHEARSAL}" == "1" ]]; then
      warn "找不到 ${MANIFEST_FILE}（演練模式允許；匯入後用 record-manifest 產生）"
    else
      echo "   [不可] 找不到 ${MANIFEST_FILE}（verify 的筆數基準；本機演練後用 record-manifest 產生並提交）" >&2; fail=1
    fi
  fi
  return "${fail}"
}

cmd_preflight() {
  load_seed_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()
  info "preflight：${TARGET}（唯讀）→ ${DB_NAME}"
  if preflight_checks; then
    echo "   → 可以執行：import ${TARGET}（SQL sha256 前 12 碼 $(sha256_of "${CONTENT_FILE}" | cut -c1-12)）"
  else
    die "preflight 未通過（見上面 [不可]），沒有寫入任何東西"
  fi
}

# ── import ─────────────────────────────────────────────────────────────────────
cmd_import() {
  load_seed_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()
  info "匯入內容種子：${TARGET} → ${DB_NAME}"
  preflight_checks || die "preflight 未通過，拒絕匯入（沒有寫入任何東西）"

  local denied_before sha batch domain_charity=""
  denied_before="$(table_counts DENIED)"
  sha="$(sha256_of "${CONTENT_FILE}" | cut -c1-12)"
  batch="${TARGET}|$(date -u +%Y-%m-%dT%H:%M:%SZ)|sha256-${sha}"
  [[ "${batch}" =~ ^[a-z]+\|[0-9TZ:-]+\|sha256-[0-9a-f]{12}$ ]] || die "批次字串格式不合：${batch}"

  if [[ "${TARGET}" == "club" ]]; then
    [[ -r "${COMPOSE_ENV}" ]] || die "讀不到 ${COMPOSE_ENV}（內容種子的 charity.donation_url 取自 CHARITY_DOMAIN）"
    domain_charity="$(compose_value CHARITY_DOMAIN)"
    [[ "${domain_charity}" =~ ^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$ ]] || die "CHARITY_DOMAIN 不是合法網域：「${domain_charity}」"
  fi

  echo "   即將在 ${DB_NAME} 執行（單一交易，失敗整批回滾）："
  echo "   - $(basename "${CONTENT_FILE}")：$(wc -l <"${CONTENT_FILE}" | tr -d ' ') 行、$(header_tables OWNED | wc -l | tr -d ' ') 張表"
  echo "   - 絕不匯入：帳號、會員、報名、訂單、捐款、金流、發票、對帳、稽核、寄信紀錄（$(header_tables DENIED | wc -l | tr -d ' ') 張表匯入前後筆數必須不變）"
  [[ "${TARGET}" != "club" ]] || echo "   - clubs.domain 不動（維持正式庫現值）；charity.donation_url → https://${domain_charity}/"
  echo "   - 標記：${PROP_NAME} = ${batch}"
  confirm "IMPORT ${DB_NAME}"

  SQLCMD_DOCKER_EXTRA=(-v "$(dirname "${CONTENT_FILE}"):/work/content:ro" -e IMPORT_BATCH)
  if [[ "${TARGET}" == "club" ]]; then
    SQLCMD_DOCKER_EXTRA+=(-e CHARITY_DOMAIN)
    CHARITY_DOMAIN="${domain_charity}" IMPORT_BATCH="${batch}" \
      run_sql_file "/work/content/$(basename "${CONTENT_FILE}")" "匯入內容（${DB_NAME}）" \
      "整份在同一個交易內，已回滾，庫維持匯入前的樣子；修正後可重跑 import ${TARGET}。"
  else
    IMPORT_BATCH="${batch}" \
      run_sql_file "/work/content/$(basename "${CONTENT_FILE}")" "匯入內容（${DB_NAME}）" \
      "整份在同一個交易內，已回滾，庫維持匯入前的樣子；修正後可重跑 import ${TARGET}。"
  fi
  SQLCMD_DOCKER_EXTRA=()

  local denied_after
  denied_after="$(table_counts DENIED)"
  if [[ "${denied_before}" != "${denied_after}" ]]; then
    echo "   🔴 帳號／假個資表的筆數在匯入前後不同（不應發生）：" >&2
    diff <(printf '%s\n' "${denied_before}") <(printf '%s\n' "${denied_after}") >&2 || true
    die "請立刻執行 clean ${TARGET} 並回報；不要繼續使用這個庫"
  fi
  ok "帳號／假個資／假交易表筆數匯入前後不變"
  cmd_verify_loaded_seed
  echo
  info "${DB_NAME} 匯入完成。若前台有快取（Redis／Cloudflare）請依 docs/20 §5「快取」處理；驗收完用 clean ${TARGET} 清除。"
}

# ── verify ─────────────────────────────────────────────────────────────────────
cmd_verify_loaded_seed() {
  SQLCMD_DOCKER_EXTRA=()
  local failures=0 prop
  info "驗證匯入結果：${DB_NAME}"
  prop="$(get_prop)"
  [[ -n "${prop}" ]] || die "沒有匯入標記 ${PROP_NAME}（尚未匯入或已清除）"
  ok "匯入標記：${prop}"

  local got_sha
  got_sha="$(sha256_of "${CONTENT_FILE}")"
  check_eq "標記內的 sha256 前 12 碼" "sha256-${got_sha:0:12}" "$(printf '%s' "${prop}" | awk -F'|' '{print $3}')" || failures=$((failures + 1))
  if [[ ! -f "${MANIFEST_FILE}" ]]; then
    [[ "${REHEARSAL}" == "1" ]] || die "找不到 ${MANIFEST_FILE}"
    warn "演練模式且沒有 manifest：略過逐表筆數核對（record-manifest 之後再 verify）"
  else
    local want_sha counts t want got n_manifest n_owned
    want_sha="$(awk -F'\t' '$1 == "#sha256" { print $2; exit }' "${MANIFEST_FILE}")"
    check_eq "manifest 對應的 SQL sha256（不符＝SQL 改過但 manifest 沒重錄）" "${want_sha}" "${got_sha}" || failures=$((failures + 1))
    counts="$(table_counts OWNED)"
    while IFS=$'\t' read -r t want; do
      [[ -n "${t}" && "${t}" != "#sha256" ]] || continue
      [[ "${t}" =~ ^[a-z_0-9]+$ ]] || die "manifest 表名不合法：${t}"
      got="$(printf '%s\n' "${counts}" | awk -F'|' -v t="${t}" '$1 == t { print $2 }')"
      if [[ "${want}" != "${got:-缺}" ]]; then
        echo "   [不符] 匯入表 ${t}：預期 ${want}，實際 ${got:-缺}" >&2
        failures=$((failures + 1))
      fi
    done <"${MANIFEST_FILE}"
    n_manifest="$(grep -vc '^#' "${MANIFEST_FILE}" || true)"
    n_owned="$(header_tables OWNED | wc -l | tr -d ' ')"
    check_eq "manifest 涵蓋的表數＝擁有的表數" "${n_owned}" "${n_manifest}" || failures=$((failures + 1))
    ok "匯入表逐表筆數核對完成（${n_manifest} 張）"
  fi

  if [[ "${TARGET}" == "club" ]]; then
    local d_tcrfc d_bw
    d_tcrfc="$(sql_scalar "SELECT domain FROM clubs WHERE code = N'tcrfc'")"
    d_bw="$(sql_scalar "SELECT domain FROM clubs WHERE code = N'bw'")"
    if [[ -r "${COMPOSE_ENV}" ]]; then
      check_eq "clubs.domain（tcrfc）仍是部署網域" "$(compose_value TCRFC_DOMAIN)" "${d_tcrfc}" || failures=$((failures + 1))
      check_eq "clubs.domain（bw）仍是部署網域" "$(compose_value BW_DOMAIN)" "${d_bw}" || failures=$((failures + 1))
    fi
    check_eq "中文編碼（藍鯨簡介首字「臺」= 33274）" "33274" \
      "$(sql_scalar "SELECT ISNULL(MAX(UNICODE(LEFT(description, 1))), 0) FROM clubs_i18n WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND locale = N'zh-Hant'")" \
      || failures=$((failures + 1))
  fi
  [[ "${failures}" -eq 0 ]] || die "匯入驗證有 ${failures} 項失敗（上面標 [不符] 者）；不要手動補資料，回報後 clean 重來"
  ok "匯入驗證通過"
  # 沿用 prod-db-init.sh 的整體驗證：表／外鍵／視圖數、歷史、參照資料筆數、沒有測試帳號、domain 不是佔位值
  cmd_verify_loaded
}

cmd_verify() {
  load_seed_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()
  [[ "$(db_state)" == "initialized" ]] || die "${DB_NAME} 不是 initialized"
  cmd_verify_loaded_seed
}

cmd_status() {
  load_seed_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()
  info "status：${TARGET} → ${DB_NAME}（唯讀）"
  local prop counts
  prop="$(get_prop)"
  echo "   匯入標記：${prop:-（無）}"
  counts="$(table_counts OWNED)"
  echo "   擁有的表合計：$(printf '%s\n' "${counts}" | sum_counts) 列；非空的表 $(printf '%s\n' "${counts}" | awk -F'|' '$2 > 0' | wc -l | tr -d ' ') 張"
  echo "   admin_users：$(sql_scalar "SELECT COUNT(*) FROM admin_users") 筆（帳號表不在匯入範圍）"
  if [[ -f "${MANIFEST_FILE}" && -n "${prop}" ]]; then
    local t want got extra=0
    while IFS=$'\t' read -r t want; do
      [[ -n "${t}" && "${t}" != "#sha256" ]] || continue
      got="$(printf '%s\n' "${counts}" | awk -F'|' -v t="${t}" '$1 == t { print $2 }')"
      if [[ "${got}" != "${want}" ]]; then
        echo "   [與匯入時不同] ${t}：匯入時 ${want}，現在 ${got}"; extra=1
      fi
    done <"${MANIFEST_FILE}"
    [[ "${extra}" -eq 1 ]] || echo "   所有擁有的表筆數與匯入時相同"
  fi
}

# ── clean ──────────────────────────────────────────────────────────────────────
cmd_clean() {
  load_seed_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()
  info "清除匯入的內容：${TARGET} → ${DB_NAME}"
  [[ "$(db_state)" == "initialized" ]] || die "${DB_NAME} 不是 initialized"
  local prop counts total
  prop="$(get_prop)"
  counts="$(table_counts OWNED)"
  total="$(printf '%s\n' "${counts}" | sum_counts)"
  if [[ -z "${prop}" ]]; then
    [[ "${total}" == "0" ]] || die "沒有匯入標記，但擁有的表有 ${total} 列。這不是本程序匯入的內容（或標記被移除了），拒絕清除；請人工確認"
    ok "沒有匯入標記、擁有的表全空——沒有東西可清"
    return 0
  fi
  echo "   匯入標記：${prop}"
  echo "   擁有的表合計 ${total} 列（要全部刪除）。與匯入時筆數不同的表（＝匯入後有人新增或刪除）："
  local t want got diff=0
  if [[ -f "${MANIFEST_FILE}" ]]; then
    while IFS=$'\t' read -r t want; do
      [[ -n "${t}" && "${t}" != "#sha256" ]] || continue
      got="$(printf '%s\n' "${counts}" | awk -F'|' -v t="${t}" '$1 == t { print $2 }')"
      if [[ "${got}" != "${want}" ]]; then echo "     ${t}：匯入時 ${want}，現在 ${got}"; diff=1; fi
    done <"${MANIFEST_FILE}"
  fi
  [[ "${diff}" -eq 1 ]] || echo "     （無）"
  [[ "${TARGET}" != "club" ]] || echo "   另會把藍鯨簡介（clubs_i18n.description）設回 NULL。clubs.domain、參照資料、admin_users 不動。"
  confirm "CLEAN ${DB_NAME}"

  local f="${WORK_DIR}/clean.sql" first=1 name
  {
    echo "SET NOCOUNT ON;"
    echo "SET XACT_ABORT ON;"
    echo "SET QUOTED_IDENTIFIER ON;"
    echo "SET ANSI_NULLS ON;"
    echo "GO"
    echo "BEGIN TRANSACTION;"
    echo "CREATE TABLE #owned (name sysname NOT NULL PRIMARY KEY, done bit NOT NULL DEFAULT 0, deleted bigint NOT NULL DEFAULT 0, blocker nvarchar(300) NULL);"
    echo "INSERT INTO #owned (name) VALUES"
    while IFS= read -r name; do
      [[ -n "${name}" ]] || continue
      if [[ "${first}" -eq 1 ]]; then first=0; else echo ","; fi
      printf "  (N'%s')" "${name}"
    done < <(header_tables OWNED)
    echo ";"
    cat <<'SQL'
-- 逐表嘗試整表刪除，被外鍵擋住（錯誤 547）就留待下一輪；直到沒有進展為止。
-- 用「真的刪刪看」而不是預先判斷哪些表有資料：資料表層級的判斷太粗（例如首頁區塊表有資料，
-- 但沒有任何一列參照輪播），列層級只有資料庫自己知道。XACT_ABORT 必須 OFF 才能在 CATCH 裡繼續交易。
SET XACT_ABORT OFF;
DECLARE @progress bit = 1, @t sysname, @sql nvarchar(max), @rows bigint;
WHILE @progress = 1
BEGIN
  SET @progress = 0;
  DECLARE tc CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM #owned WHERE done = 0 ORDER BY name;
  OPEN tc;
  FETCH NEXT FROM tc INTO @t;
  WHILE @@FETCH_STATUS = 0
  BEGIN
    BEGIN TRY
      SET @sql = N'DELETE FROM ' + QUOTENAME(@t) + N'; SET @r = @@ROWCOUNT;';
      EXEC sys.sp_executesql @sql, N'@r bigint OUTPUT', @r = @rows OUTPUT;
      UPDATE #owned SET done = 1, deleted = @rows, blocker = NULL WHERE name = @t;
      SET @progress = 1;
    END TRY
    BEGIN CATCH
      IF ERROR_NUMBER() <> 547 THROW;
      UPDATE #owned SET blocker = LEFT(ERROR_MESSAGE(), 300) WHERE name = @t;
    END CATCH
    FETCH NEXT FROM tc INTO @t;
  END
  CLOSE tc; DEALLOCATE tc;
END
IF EXISTS (SELECT 1 FROM #owned WHERE done = 0)
BEGIN
  DECLARE @msg nvarchar(3000) = N'清除中止（整個交易回滾，沒有刪任何東西）。下列表的列仍被其他資料參照：'
    + (SELECT STRING_AGG(name + N' <- ' + ISNULL(blocker, N'?'), N' ; ') FROM #owned WHERE done = 0);
  SET @msg = LEFT(@msg, 2000); THROW 50020, @msg, 1;
END
SET XACT_ABORT ON;
SQL
    if [[ "${TARGET}" == "club" ]]; then
      echo "UPDATE clubs_i18n SET description = NULL WHERE club_id = (SELECT id FROM clubs WHERE code = N'bw') AND locale = N'zh-Hant';"
    fi
    cat <<SQL
EXEC sys.sp_dropextendedproperty @name = N'${PROP_NAME}';
SELECT N'deleted' AS k, name, deleted FROM #owned WHERE deleted > 0 ORDER BY name;
COMMIT TRANSACTION;
GO
SQL
  } >"${f}"

  SQLCMD_DOCKER_EXTRA=(-v "${WORK_DIR}:/work/clean:ro")
  local out
  out="$(run_sql_capture "/work/clean/clean.sql" "清除（${DB_NAME}）")"
  SQLCMD_DOCKER_EXTRA=()
  ok "已刪除（各表自己的 DELETE 列數；*_i18n 等子表由外鍵 CASCADE 一併刪除，不計入此處）："
  printf '%s\n' "${out}" | grep '^deleted|' | awk -F'|' '{ printf "     %s = %s\n", $2, $3; s += $3 } END { printf "     合計 %d 列\n", s }'

  [[ "$(get_prop)" == "" ]] || die "清除後仍有匯入標記"
  [[ "$(table_counts OWNED | sum_counts)" == "0" ]] || die "清除後擁有的表仍有資料"
  ok "擁有的表全空、匯入標記已移除"
  cmd_verify_loaded
  info "${DB_NAME} 已回到匯入前的狀態（只有參照資料與既有管理員）。"
}

# ── record-manifest（只給本機演練）────────────────────────────────────────────
cmd_record_manifest() {
  [[ "${REHEARSAL}" == "1" ]] || die "record-manifest 只能在 PRODINIT_REHEARSAL=1（本機演練庫）使用；正式庫的基準必須來自演練"
  load_seed_target "${1:-}"
  require_tools
  SQLCMD_DOCKER_EXTRA=()
  [[ -n "$(get_prop)" ]] || die "演練庫還沒匯入（沒有標記），先 import ${TARGET}"
  local counts
  counts="$(table_counts OWNED)"
  {
    printf '#sha256\t%s\n' "$(sha256_of "${CONTENT_FILE}")"
    printf '%s\n' "${counts}" | awk -F'|' '{ printf "%s\t%s\n", $1, $2 }' | sort
  } >"${MANIFEST_FILE}"
  ok "已寫入 ${MANIFEST_FILE}（$(grep -vc '^#' "${MANIFEST_FILE}") 張表、合計 $(printf '%s\n' "${counts}" | sum_counts) 列）；請提交"
}

seed_main() {
  local cmd="${1:-}"
  case "${cmd}" in
    preflight) cmd_preflight "${2:-}" ;;
    import) cmd_import "${2:-}" ;;
    verify) cmd_verify "${2:-}" ;;
    status) cmd_status "${2:-}" ;;
    clean) cmd_clean "${2:-}" ;;
    record-manifest) cmd_record_manifest "${2:-}" ;;
    -h|--help|help) usage_seed 0 ;;
    *) usage_seed 1 ;;
  esac
}

seed_main "$@"
