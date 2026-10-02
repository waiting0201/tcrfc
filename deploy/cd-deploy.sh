#!/usr/bin/env bash
# deploy/cd-deploy.sh — 在正式 VM 上（self-hosted runner）部署或回滾映像檔。
#
# 由 .github/workflows/deploy.yml（CD_MODE=deploy）與 rollback.yml（CD_MODE=rollback）呼叫。
# 設計與理由：docs/20-cicd.md §4a、§6。**不執行任何資料庫 migration**（§5）。
#
# 做的事（依序）：
#   1. 取鎖、前置檢查（docker、.env、compose 檔語法）
#   2. 讀 deploy-state.env 取得「上一個成功版本」的五個映像檔標籤（沒有就是 master）
#   3. 算出本次五個映像檔各自的標籤（deploy：有重建的用 git SHA、其餘沿用；rollback：依歷史紀錄）
#   4. 缺的映像檔從 ghcr 拉；標籤是浮動的 master 且要被換掉時，先把「現在正在跑的映像檔」在本機另打
#      一個 cd-prev 標籤，當首次銜接與自動回滾的退路
#   5. docker compose up -d --wait；Caddyfile 內容與執行中的 proxy 不同就強制重建 proxy
#   6. 健康檢查：容器全 healthy、api /readyz、六個網址經 VM 本機（--resolve 127.0.0.1）回 200
#   7. 失敗 → 自動退回上一個成功版本並再檢查一次；成功 → 更新 deploy-state.env 與 deploy-history.log
#
# 輸入（環境變數，全部由 workflow 以 env: 傳入，不內插進 shell 字串）：
#   CD_MODE            deploy（預設）｜rollback
#   CD_SHA             40 字元 git SHA（deploy：本次提交；rollback：要回到的那一版）
#   CD_REBUILT         deploy 用：本次有重建的映像檔鍵（空白或逗號分隔）：
#                      nuxt_club nuxt_charity admin_web admin_charity api
#   CD_ENV_FILE        預設 /opt/tcrfc/.env
#   CD_STATE_FILE      預設 /opt/tcrfc/deploy-state.env
#   CD_HISTORY_FILE    預設 /opt/tcrfc/deploy-history.log
#   CD_COMPOSE_FILE    預設 ${PWD}/docker-compose.yml（workflow 在 checkout 目錄執行）
#   CD_LOCK_FILE       預設 /opt/tcrfc/.deploy.lock（有 flock 才用）
#   CD_HEALTH_TIMEOUT  預設 300 秒；CD_HEALTH_INTERVAL 預設 5 秒
# 輸出：GITHUB_OUTPUT（result、changed_images）、GITHUB_STEP_SUMMARY（沒有就印到 stdout）
# 結束碼：0 成功；1 失敗但已退回（或前置檢查失敗、尚未動任何東西）；2 失敗且退回也失敗（要人工處理）
#
# 🔴 給 /bin/bash 3.2（macOS）也能跑：不用關聯陣列／mapfile；字串內的變數一律 ${VAR}（E-108）。

set -euo pipefail

CD_MODE="${CD_MODE:-deploy}"
CD_SHA="${CD_SHA:-}"
CD_REBUILT="${CD_REBUILT:-}"
CD_ENV_FILE="${CD_ENV_FILE:-/opt/tcrfc/.env}"
CD_STATE_FILE="${CD_STATE_FILE:-/opt/tcrfc/deploy-state.env}"
CD_HISTORY_FILE="${CD_HISTORY_FILE:-/opt/tcrfc/deploy-history.log}"
CD_COMPOSE_FILE="${CD_COMPOSE_FILE:-${PWD}/docker-compose.yml}"
CD_LOCK_FILE="${CD_LOCK_FILE:-/opt/tcrfc/.deploy.lock}"
CD_HEALTH_TIMEOUT="${CD_HEALTH_TIMEOUT:-300}"
CD_HEALTH_INTERVAL="${CD_HEALTH_INTERVAL:-5}"
CD_PULL_RETRY_SLEEP="${CD_PULL_RETRY_SLEEP:-10}"

SUMMARY_FILE="${GITHUB_STEP_SUMMARY:-/dev/stdout}"
IMAGE_KEYS="nuxt_club nuxt_charity admin_web admin_charity api"
TAG_RE='^[A-Za-z0-9_.-]{1,128}$'

log() { printf '[cd] %s\n' "$*"; }
warn() { printf '[cd] ⚠️  %s\n' "$*" >&2; }
emit_output() { # key value（值一律是本檔產生的固定字串，不含換行）
  if [ -n "${GITHUB_OUTPUT:-}" ]; then printf '%s=%s\n' "$1" "$2" >> "${GITHUB_OUTPUT}"; fi
}
summary() { printf '%s\n' "$*" >> "${SUMMARY_FILE}"; }

# ── 五個映像檔的對照 ───────────────────────────────────────────────
img_name() {
  case "$1" in
    nuxt_club) echo tcrfc-nuxt-club ;;
    nuxt_charity) echo tcrfc-nuxt-charity ;;
    admin_web) echo tcrfc-admin-web ;;
    admin_charity) echo tcrfc-admin-charity ;;
    api) echo tcrfc-api ;;
    *) return 1 ;;
  esac
}
img_var() { # 對應 docker-compose.yml 的 TAG_* 變數
  case "$1" in
    nuxt_club) echo TAG_NUXT_CLUB ;;
    nuxt_charity) echo TAG_NUXT_CHARITY ;;
    admin_web) echo TAG_ADMIN_WEB ;;
    admin_charity) echo TAG_ADMIN_CHARITY ;;
    api) echo TAG_API ;;
    *) return 1 ;;
  esac
}
img_first_service() { # 取一個執行中的容器來查「現在跑的映像檔」
  case "$1" in
    nuxt_club) echo nuxt-tcrfc ;;
    nuxt_charity) echo nuxt-charity ;;
    admin_web) echo admin-web ;;
    admin_charity) echo admin-charity ;;
    api) echo api ;;
    *) return 1 ;;
  esac
}

# ── 設定檔讀取：只用 grep 取值，不 source（檔案是機密或狀態，不當程式執行） ──
file_get() { # file key → 值（去引號），沒有則空字串
  local line
  line="$(grep -E "^${2}=" "$1" 2>/dev/null | tail -n 1 || true)"
  line="${line#*=}"
  line="${line%$'\r'}"
  case "${line}" in
    \"*\") line="${line#\"}"; line="${line%\"}" ;;
    \'*\') line="${line#\'}"; line="${line%\'}" ;;
  esac
  printf '%s' "${line}"
}

get_var() { eval "printf '%s' \"\${$1:-}\""; }
set_var() { printf -v "$1" '%s' "$2"; }

dc() { docker compose --env-file "${CD_ENV_FILE}" -f "${CD_COMPOSE_FILE}" "$@"; }

FAIL_REASON=""
die_early() { # 還沒動任何東西就失敗
  warn "$*"
  summary "### ❌ 部署中止（尚未變動任何容器）"
  summary ""
  summary "${*}"
  emit_output result preflight_failed
  emit_output changed_images ""
  exit 1
}

# ── 1. 前置檢查 ────────────────────────────────────────────────────
case "${CD_MODE}" in deploy | rollback) ;; *) die_early "CD_MODE 只能是 deploy 或 rollback（收到：${CD_MODE}）" ;; esac
[[ "${CD_SHA}" =~ ^[0-9a-f]{40}$ ]] || die_early "CD_SHA 必須是 40 字元的小寫 git SHA（收到：${CD_SHA}）"
command -v docker > /dev/null 2>&1 || die_early "找不到 docker"
[ -r "${CD_ENV_FILE}" ] || die_early "讀不到 compose 用的 .env：${CD_ENV_FILE}"
[ -r "${CD_COMPOSE_FILE}" ] || die_early "找不到 compose 檔：${CD_COMPOSE_FILE}"
COMPOSE_DIR="$(cd "$(dirname "${CD_COMPOSE_FILE}")" && pwd)"

if command -v flock > /dev/null 2>&1; then
  if exec 9> "${CD_LOCK_FILE}"; then
    flock -n 9 || die_early "另一個部署正在進行（鎖：${CD_LOCK_FILE}）"
  else
    warn "無法建立鎖檔 ${CD_LOCK_FILE}，略過互斥（workflow 的 concurrency 仍會排隊）"
  fi
fi

OWNER="$(file_get "${CD_ENV_FILE}" GHCR_OWNER)"
OWNER="${OWNER:-waiting0201}"
[[ "${OWNER}" =~ ^[A-Za-z0-9_.-]+$ ]] || die_early "GHCR_OWNER 格式不合法"

dc config -q > /dev/null 2>&1 || die_early "docker compose config 驗證失敗（.env 缺必填變數，或 compose 檔語法錯誤）"
EXPECTED_SERVICES="$(dc config --services | wc -l | tr -d ' ')"
[ "${EXPECTED_SERVICES}" -ge 1 ] || die_early "compose 沒有任何服務"

# ── 2. 上一個成功版本 ──────────────────────────────────────────────
PREV_SHA="$(file_get "${CD_STATE_FILE}" LAST_GOOD_SHA)"
for k in ${IMAGE_KEYS}; do
  v="$(file_get "${CD_STATE_FILE}" "$(img_var "${k}")")"
  v="${v:-master}"
  [[ "${v}" =~ ${TAG_RE} ]] || die_early "deploy-state.env 的 $(img_var "${k}") 值不合法：${v}"
  set_var "PREV_${k}" "${v}"
done
if [ -z "${PREV_SHA}" ]; then
  FIRST_BRIDGE=true
else
  FIRST_BRIDGE=false
fi

# ── 3. 本次五個映像檔的標籤 ─────────────────────────────────────────
for k in ${IMAGE_KEYS}; do set_var "NEW_${k}" "$(get_var "PREV_${k}")"; done

if [ "${CD_MODE}" = deploy ]; then
  for k in $(printf '%s' "${CD_REBUILT}" | tr ',' ' '); do
    img_name "${k}" > /dev/null 2>&1 || die_early "CD_REBUILT 含未知的映像檔鍵：${k}"
    set_var "NEW_${k}" "${CD_SHA}"
  done
else
  # 回滾：優先用歷史紀錄裡「那一版當時的完整標籤組合」；沒有（例如那次部署早於 CD、或紀錄被清掉）
  # 才退回「逐一檢查 :<sha> 標籤是否存在於 ghcr，沒有的沿用現在的」。
  line=""
  if [ -r "${CD_HISTORY_FILE}" ]; then
    line="$(grep -E "^[^ ]+ ${CD_SHA} .* result=ok\$" "${CD_HISTORY_FILE}" | tail -n 1 || true)"
  fi
  if [ -n "${line}" ]; then
    ROLLBACK_SOURCE="歷史紀錄（${CD_HISTORY_FILE}）"
    for k in ${IMAGE_KEYS}; do
      v="$(printf '%s\n' "${line}" | tr ' ' '\n' | grep -E "^${k}=" | tail -n 1 | cut -d= -f2- || true)"
      [ -n "${v}" ] || die_early "歷史紀錄缺少 ${k} 的標籤：${line}"
      [[ "${v}" =~ ${TAG_RE} ]] || die_early "歷史紀錄的 ${k} 標籤不合法：${v}"
      set_var "NEW_${k}" "${v}"
    done
  else
    ROLLBACK_SOURCE="ghcr 上存在的 :${CD_SHA} 標籤（歷史紀錄沒有這一版）"
    any=false
    for k in ${IMAGE_KEYS}; do
      if docker manifest inspect "ghcr.io/${OWNER}/$(img_name "${k}"):${CD_SHA}" > /dev/null 2>&1; then
        set_var "NEW_${k}" "${CD_SHA}"
        any=true
      else
        warn "ghcr 上沒有 $(img_name "${k}"):${CD_SHA}（該次提交沒重建這個映像檔），沿用目前的 $(get_var "PREV_${k}")"
      fi
    done
    [ "${any}" = true ] || die_early "ghcr 上找不到任何帶 :${CD_SHA} 標籤的映像檔，也不在歷史紀錄裡——這個 SHA 沒有可回滾的版本"
  fi
fi

CHANGED=""
for k in ${IMAGE_KEYS}; do
  if [ "$(get_var "NEW_${k}")" != "$(get_var "PREV_${k}")" ]; then CHANGED="${CHANGED:+${CHANGED} }${k}"; fi
done

# ── 4. 映像檔：缺的拉下來；浮動的 master 先留本機退路 ─────────────────
local_has() { docker image inspect "$1" > /dev/null 2>&1; }

for k in ${CHANGED}; do
  prev="$(get_var "PREV_${k}")"
  if [ "${prev}" = master ]; then
    # :master 是浮動標籤，build 已經把它指到新版；要退回「現在跑的那一版」只能靠本機映像檔 ID。
    cid="$(dc ps -q "$(img_first_service "${k}")" 2> /dev/null | head -n 1 || true)"
    if [ -n "${cid}" ]; then
      imgid="$(docker inspect --format '{{.Image}}' "${cid}" 2> /dev/null || true)"
      if [ -n "${imgid}" ]; then
        docker tag "${imgid}" "ghcr.io/${OWNER}/$(img_name "${k}"):cd-prev"
        set_var "PREV_${k}" cd-prev
        log "$(img_name "${k}")：目前跑的是浮動的 :master，已在本機另存為 :cd-prev 當退路"
      fi
    fi
  fi
done

for k in ${CHANGED}; do
  ref="ghcr.io/${OWNER}/$(img_name "${k}"):$(get_var "NEW_${k}")"
  if local_has "${ref}"; then
    log "本機已有 ${ref}，不重拉"
    continue
  fi
  n=0
  until docker pull --quiet "${ref}" > /dev/null; do
    n=$((n + 1))
    if [ "${n}" -ge 3 ]; then die_early "拉不到映像檔 ${ref}（已重試 3 次；ghcr 套件是否為 Public？build 是否成功？）"; fi
    warn "拉取 ${ref} 失敗，${CD_PULL_RETRY_SLEEP} 秒後重試（${n}/3）"
    sleep "${CD_PULL_RETRY_SLEEP}"
  done
done

export_tags() { # 參數：前綴 PREV 或 NEW
  local k
  for k in ${IMAGE_KEYS}; do
    export "$(img_var "${k}")=$(get_var "$1_${k}")"
  done
}

# ── 5. 部署與健康檢查 ─────────────────────────────────────────────
caddyfile_host_path() {
  local rel
  rel="$(file_get "${CD_ENV_FILE}" CADDYFILE)"
  rel="${rel:-./deploy/Caddyfile}"
  case "${rel}" in
    /*) printf '%s' "${rel}" ;;
    *) printf '%s/%s' "${COMPOSE_DIR}" "${rel#./}" ;;
  esac
}

hash_of() { sha256sum "$1" 2> /dev/null | cut -d' ' -f1 || true; }

up_stack() {
  dc up -d --no-build --pull never --wait --wait-timeout "${CD_HEALTH_TIMEOUT}"
}

# 🔴 Caddyfile 是「單一檔案」bind mount：git 換檔會產生新 inode，容器仍抱著舊檔，compose 也看不出
# 設定變了（來源路徑字串沒變）。所以直接比對「執行中的 proxy 讀到的內容」與 checkout 裡的檔案。
maybe_recreate_proxy() {
  local host_hash running_hash
  host_hash="$(hash_of "$(caddyfile_host_path)")"
  [ -n "${host_hash}" ] || { warn "讀不到 Caddyfile：$(caddyfile_host_path)"; return 0; }
  running_hash="$(dc exec -T proxy sha256sum /etc/caddy/Caddyfile 2> /dev/null | cut -d' ' -f1 || true)"
  if [ "${running_hash}" != "${host_hash}" ]; then
    log "proxy 的 Caddyfile 與 checkout 不同（或讀不到），強制重建 proxy"
    PROXY_RECREATED=true
    dc up -d --no-deps --force-recreate --wait --wait-timeout "${CD_HEALTH_TIMEOUT}" proxy
  fi
}

LAST_FAILURE=""
check_once() {
  LAST_FAILURE=""
  local ids id n=0 line status health
  ids="$(dc ps -a -q 2> /dev/null || true)"
  for id in ${ids}; do
    n=$((n + 1))
    line="$(docker inspect --format '{{.Name}} {{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "${id}" 2> /dev/null || true)"
    status="$(printf '%s' "${line}" | cut -d' ' -f2)"
    health="$(printf '%s' "${line}" | cut -d' ' -f3)"
    if [ "${status}" != running ] || { [ "${health}" != healthy ] && [ "${health}" != none ]; }; then
      LAST_FAILURE="容器不健康：${line}"
      return 1
    fi
  done
  if [ "${n}" -ne "${EXPECTED_SERVICES}" ]; then
    LAST_FAILURE="容器數量不對：預期 ${EXPECTED_SERVICES}，實際 ${n}"
    return 1
  fi

  local ready
  ready="$(dc exec -T api curl -fsS --max-time 10 http://127.0.0.1:8080/readyz 2> /dev/null || true)"
  if ! printf '%s' "${ready}" | grep -Eq '"status" *: *"ready"'; then
    LAST_FAILURE="api /readyz 不是 ready：${ready:-（無回應）}"
    return 1
  fi
  READYZ_BODY="${ready}"

  local pair domain path code
  for pair in TCRFC_DOMAIN:/ BW_DOMAIN:/ CHARITY_DOMAIN:/ ADMIN_WEB_DOMAIN:/ ADMIN_CHARITY_DOMAIN:/ API_DOMAIN:/readyz; do
    domain="$(file_get "${CD_ENV_FILE}" "${pair%%:*}")"
    path="${pair#*:}"
    [ -n "${domain}" ] || { LAST_FAILURE="${pair%%:*} 在 ${CD_ENV_FILE} 是空的"; return 1; }
    # --resolve 把網域直接指到本機 Caddy：不經 Cloudflare、不受 NSG 只放 Cloudflare 段的限制，
    # 但仍走 TLS（SNI 與憑證都用真實網域），測得到「proxy 依 Host 分流到上游」這一層。
    code="$(curl -sS -o /dev/null -w '%{http_code}' --max-time 15 --resolve "${domain}:443:127.0.0.1" "https://${domain}${path}" 2> /dev/null || true)"
    if [ "${code}" != 200 ]; then
      LAST_FAILURE="https://${domain}${path} 回 ${code:-（連不上）}，預期 200"
      return 1
    fi
  done
  return 0
}

READYZ_BODY=""
verify_stack() {
  local start now
  start="$(date +%s)"
  while :; do
    if check_once; then return 0; fi
    now="$(date +%s)"
    if [ $((now - start)) -ge "${CD_HEALTH_TIMEOUT}" ]; then return 1; fi
    sleep "${CD_HEALTH_INTERVAL}"
  done
}

PROXY_RECREATED=false

render_table() { # 參數：前綴 PREV 或 NEW
  local k
  summary "| 映像檔 | 標籤 |"
  summary "|---|---|"
  for k in ${IMAGE_KEYS}; do
    summary "| \`ghcr.io/${OWNER}/$(img_name "${k}")\` | \`$(get_var "$1_${k}")\` |"
  done
}

log "模式：${CD_MODE}；目標 SHA：${CD_SHA}；變動的映像檔：${CHANGED:-（無，只套用 compose／設定）}；首次銜接：${FIRST_BRIDGE}"
export_tags NEW

DEPLOY_OK=false
if up_stack && maybe_recreate_proxy && verify_stack; then
  DEPLOY_OK=true
else
  [ -n "${LAST_FAILURE}" ] || LAST_FAILURE="docker compose up 失敗或等待容器 healthy 逾時"
  FAIL_REASON="${LAST_FAILURE}"
fi

emit_output changed_images "$(printf '%s' "${CHANGED}" | tr ' ' ',')"

if [ "${DEPLOY_OK}" = true ]; then
  # 成功：更新狀態與歷史（原地改寫，不換檔，保留 runner 擁有的檔案權限）
  now_iso="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  tmp="$(mktemp)"
  {
    printf '# %s — 由 deploy/cd-deploy.sh 寫入：最後一次成功的部署（勿手改；回滾讀的就是這份）\n' "$(basename "${CD_STATE_FILE}")"
    printf 'LAST_GOOD_SHA=%s\n' "${CD_SHA}"
    printf 'LAST_GOOD_AT=%s\n' "${now_iso}"
    for k in ${IMAGE_KEYS}; do printf '%s=%s\n' "$(img_var "${k}")" "$(get_var "NEW_${k}")"; done
  } > "${tmp}"
  cat "${tmp}" > "${CD_STATE_FILE}"
  rm -f "${tmp}"
  {
    printf '%s %s' "${now_iso}" "${CD_SHA}"
    for k in ${IMAGE_KEYS}; do printf ' %s=%s' "${k}" "$(get_var "NEW_${k}")"; done
    printf ' mode=%s result=ok\n' "${CD_MODE}"
  } >> "${CD_HISTORY_FILE}"

  summary "### ✅ ${CD_MODE} 成功：\`${CD_SHA}\`"
  summary ""
  [ "${FIRST_BRIDGE}" = true ] && summary "> 這是第一次由 CD 部署（deploy-state.env 原本是空的）；先前是手動以 \`:master\` 起的容器。"
  [ "${CD_MODE}" = rollback ] && summary "> 標籤來源：${ROLLBACK_SOURCE:-}"
  summary ""
  render_table NEW
  summary ""
  summary "- 變動的映像檔：${CHANGED:-（無）}"
  summary "- proxy 因 Caddyfile 變動而重建：${PROXY_RECREATED}"
  summary "- 健康檢查：${EXPECTED_SERVICES} 個容器 healthy；六個網址（經 VM 本機 --resolve）200；api /readyz：\`${READYZ_BODY}\`"
  case "${READYZ_BODY}" in *'"redis":"degraded"'* | *'"redis": "degraded"'*) summary "- ⚠️ redis 狀態為 degraded（不影響就緒，但請查 \`docker compose logs redis\`）" ;; esac
  emit_output result success
  log "完成"
  exit 0
fi

# ── 7. 失敗：退回上一個成功版本並再檢查一次 ───────────────────────────
warn "部署失敗：${FAIL_REASON}"
warn "退回上一個成功版本（${PREV_SHA:-首次銜接前手動啟動的版本}）"
for k in ${CHANGED}; do
  ref="ghcr.io/${OWNER}/$(img_name "${k}"):$(get_var "PREV_${k}")"
  if ! local_has "${ref}"; then docker pull --quiet "${ref}" > /dev/null 2>&1 || warn "本機沒有 ${ref} 而且拉不到"; fi
done
export_tags PREV
ROLLBACK_OK=false
if up_stack && verify_stack; then ROLLBACK_OK=true; fi

summary "### ❌ ${CD_MODE} 失敗：\`${CD_SHA}\`"
summary ""
summary "- 失敗原因：${FAIL_REASON}"
if [ "${ROLLBACK_OK}" = true ]; then
  summary "- ✅ 已自動退回上一個成功版本（\`${PREV_SHA:-首次銜接前手動啟動的版本}\`）並通過健康檢查；deploy-state.env 維持不變"
  summary ""
  render_table PREV
  emit_output result rolled_back
  exit 1
fi
summary "- 🔴 **自動退回也失敗**：${LAST_FAILURE:-（無）}。VM 現在處於不確定狀態，請人工處理（見 docs/20-cicd.md §6「人工處理」）"
summary ""
render_table PREV
emit_output result rollback_failed
exit 2
