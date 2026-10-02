#!/usr/bin/env bash
# shellcheck disable=SC2319 # 測試檔刻意把「條件的結果」傳給 expect
# deploy/test-cd.sh — 以「假 docker／curl／sha256sum」驗證 cd-deploy.sh 與 cd-purge-cache.sh 的主要路徑。
#
# 不碰任何真實容器或網路。建議用 macOS 內建的 /bin/bash（3.2）跑，涵蓋 E-108 的語法陷阱：
#   /bin/bash deploy/test-cd.sh
# 情境：首次銜接成功、第二次只換 api、健康檢查失敗→自動退回成功、退回也失敗、前置檢查失敗、
#       人工回滾（歷史紀錄／無紀錄退回 ghcr 標籤）、Caddyfile 變動強制重建 proxy、清快取（有／無 token）。

set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(mktemp -d)"
trap 'rm -rf "${ROOT}"' EXIT

PASS=0
FAIL=0
ok() { PASS=$((PASS + 1)); printf '  ok   %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL %s\n' "$1"; }
expect() { # 描述 條件（0 通過）
  if [ "$2" -eq 0 ]; then ok "$1"; else bad "$1"; fi
}

FAKEBIN="${ROOT}/bin"
mkdir -p "${FAKEBIN}"

# ── 假 docker ─────────────────────────────────────────────────────
# 狀態都在 ${FAKE_DIR}：running（上一次 up 的 TAG_*）、images（本機有的映像檔）、calls（呼叫紀錄）、
# proxy_hash（proxy 容器內 Caddyfile 的雜湊）。FAKE_BAD＝空白分隔的標籤；running 含其中任何一個就視為不健康。
cat > "${FAKEBIN}/docker" << 'EOF'
#!/usr/bin/env bash
echo "docker $*" >> "${FAKE_DIR}/calls"
services="proxy nuxt-tcrfc nuxt-bw nuxt-charity admin-web admin-charity api redis"
is_bad() {
  local t
  [ -f "${FAKE_DIR}/running" ] || return 1
  for t in ${FAKE_BAD:-}; do
    grep -q "=${t}\$" "${FAKE_DIR}/running" && return 0
  done
  return 1
}
case "$1" in
  compose)
    shift
    while [ $# -gt 0 ]; do case "$1" in --env-file | -f) shift 2 ;; *) break ;; esac; done
    sub="$1"; shift
    case "${sub}" in
      config)
        [ "${FAKE_CONFIG_FAIL:-}" = 1 ] && exit 1
        [ "${1:-}" = "--services" ] && for s in ${services}; do echo "${s}"; done
        exit 0 ;;
      ps)
        if [ "${1:-}" = "-q" ] && [ -n "${2:-}" ]; then echo "cid-$2"; exit 0; fi
        for s in ${services}; do echo "cid-${s}"; done; exit 0 ;;
      up)
        if [ "${FAKE_UP_FAIL:-}" = 1 ] && ! is_bad && false; then exit 1; fi
        case " $* " in *" --force-recreate "*) echo "${FAKE_CADDY_HASH}" > "${FAKE_DIR}/proxy_hash" ;; esac
        : > "${FAKE_DIR}/running"
        for v in TAG_NUXT_CLUB TAG_NUXT_CHARITY TAG_ADMIN_WEB TAG_ADMIN_CHARITY TAG_API; do
          printf '%s=%s\n' "${v}" "${!v:-unset}" >> "${FAKE_DIR}/running"
        done
        is_bad && [ "${FAKE_UP_FAIL_ON_BAD:-}" = 1 ] && exit 1
        exit 0 ;;
      exec)
        if [ "$2" = proxy ] || [ "$1" = "-T" -a "$2" = proxy ]; then
          echo "$(cat "${FAKE_DIR}/proxy_hash")  /etc/caddy/Caddyfile"; exit 0
        fi
        if is_bad; then echo '{"status":"not_ready","club_db":"fail"}'; exit 22; fi
        echo '{"status":"ready","club_db":"ok","charity_db":"ok","redis":"ok"}'; exit 0 ;;
    esac ;;
  inspect)
    case "$*" in
      *"{{.Image}}"*) echo "sha256:fakeimage-${@: -1}"; exit 0 ;;
      *) cid="${@: -1}"
         if is_bad; then echo "/tcrfc-x running unhealthy"; else echo "/tcrfc-${cid} running healthy"; fi; exit 0 ;;
    esac ;;
  image) grep -qxF "$3" "${FAKE_DIR}/images" ; exit $? ;;
  pull)
    ref="${@: -1}"
    [ "${FAKE_PULL_FAIL:-}" = 1 ] && exit 1
    echo "${ref}" >> "${FAKE_DIR}/images"; exit 0 ;;
  tag) echo "$3" >> "${FAKE_DIR}/images"; exit 0 ;;
  manifest)
    case " ${FAKE_MANIFEST_MISSING:-} " in *" ${@: -1} "*) exit 1 ;; esac
    exit 0 ;;
esac
exit 0
EOF
chmod +x "${FAKEBIN}/docker"

cat > "${FAKEBIN}/curl" << 'EOF'
#!/usr/bin/env bash
echo "curl $*" >> "${FAKE_DIR}/calls"
case "$*" in
  *api.cloudflare.com*)
    cat > "${FAKE_DIR}/curl_stdin" || true
    echo '{"success":true}'; exit 0 ;;
esac
if [ -f "${FAKE_DIR}/running" ]; then
  for t in ${FAKE_BAD:-}; do grep -q "=${t}\$" "${FAKE_DIR}/running" && { printf '502'; exit 0; }; done
fi
printf '200'
EOF
chmod +x "${FAKEBIN}/curl"

# macOS 沒有 sha256sum；VM 上有。假的用 shasum 或 cksum 湊出穩定雜湊。
cat > "${FAKEBIN}/sha256sum" << 'EOF'
#!/usr/bin/env bash
h="$(cksum < "$1" | cut -d' ' -f1)"
echo "${h}  $1"
EOF
chmod +x "${FAKEBIN}/sha256sum"

SHA_A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
SHA_B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
SHA_C="cccccccccccccccccccccccccccccccccccccccc"

new_case() { # 建立乾淨的假 VM 環境，設定好環境變數
  CASE="${ROOT}/case-$1"
  rm -rf "${CASE}"
  mkdir -p "${CASE}/fake" "${CASE}/work/deploy"
  export FAKE_DIR="${CASE}/fake"
  : > "${FAKE_DIR}/calls"
  : > "${FAKE_DIR}/images"
  echo "oldhash" > "${FAKE_DIR}/proxy_hash"
  cat > "${CASE}/env" << 'EOF'
IMAGE_TAG='master'
GHCR_OWNER=waiting0201
CADDYFILE=./deploy/Caddyfile.prelaunch
TCRFC_DOMAIN=tcrfc.example.test
BW_DOMAIN=bw.example.test
CHARITY_DOMAIN=charity.example.test
ADMIN_WEB_DOMAIN=admin.example.test
ADMIN_CHARITY_DOMAIN=admin-charity.example.test
API_DOMAIN=api.example.test
REDIS_PASSWORD='s3cret'
EOF
  echo "# fake caddy" > "${CASE}/work/deploy/Caddyfile.prelaunch"
  : > "${CASE}/work/docker-compose.yml"
  : > "${CASE}/state"
  : > "${CASE}/history"
  : > "${CASE}/summary"
  : > "${CASE}/output"
  export CD_ENV_FILE="${CASE}/env" CD_STATE_FILE="${CASE}/state" CD_HISTORY_FILE="${CASE}/history"
  export CD_COMPOSE_FILE="${CASE}/work/docker-compose.yml" CD_LOCK_FILE="${CASE}/lock"
  export CD_HEALTH_TIMEOUT=3 CD_HEALTH_INTERVAL=1
  export GITHUB_STEP_SUMMARY="${CASE}/summary" GITHUB_OUTPUT="${CASE}/output"
  # proxy 內的雜湊預設等於 checkout 的檔案，避免每個情境都觸發 proxy 重建（要測的情境自己改）
  export FAKE_CADDY_HASH
  FAKE_CADDY_HASH="$(PATH="${FAKEBIN}:${PATH}" sha256sum "${CASE}/work/deploy/Caddyfile.prelaunch" | cut -d' ' -f1)"
  echo "${FAKE_CADDY_HASH}" > "${FAKE_DIR}/proxy_hash"
  unset FAKE_BAD FAKE_CONFIG_FAIL FAKE_PULL_FAIL FAKE_MANIFEST_MISSING FAKE_UP_FAIL_ON_BAD CD_REBUILT
}

run_cd() { # 以 /bin/bash 跑（與使用者 Mac 相同的 3.2）
  PATH="${FAKEBIN}:${PATH}" /bin/bash "${HERE}/cd-deploy.sh" > "${CASE}/stdout" 2>&1
}
output_has() { grep -q "$1" "${CASE}/output"; }
state_has() { grep -q "$1" "${CD_STATE_FILE}"; }
calls_have() { grep -q "$1" "${FAKE_DIR}/calls"; }

echo "== 1. 首次銜接：空狀態，全部重建（手動 dispatch）"
new_case 1
CD_MODE=deploy CD_SHA="${SHA_A}" CD_REBUILT="nuxt_club nuxt_charity admin_web admin_charity api" run_cd
rc=$?
expect "結束碼 0" "$([ ${rc} -eq 0 ]; echo $?)"
output_has "result=success"; expect "result=success" $?
state_has "LAST_GOOD_SHA=${SHA_A}"; expect "state 寫入 SHA" $?
state_has "TAG_API=${SHA_A}"; expect "state 寫入 TAG_API=sha" $?
grep -q "result=ok" "${CD_HISTORY_FILE}"; expect "history 有一行 ok" $?
calls_have "docker tag sha256:fakeimage-cid-api ghcr.io/waiting0201/tcrfc-api:cd-prev"; expect "浮動 master 先另存 cd-prev" $?
calls_have "docker pull --quiet ghcr.io/waiting0201/tcrfc-api:${SHA_A}"; expect "拉 :sha 映像檔" $?
grep -q "第一次由 CD 部署" "${CASE}/summary"; expect "summary 註明首次銜接" $?
grep -q -- "--wait" "${FAKE_DIR}/calls"; expect "up 帶 --wait" $?
grep -q "resolve tcrfc.example.test:443:127.0.0.1" "${FAKE_DIR}/calls"; expect "網址檢查用 --resolve 到本機" $?

echo "== 2. 第二次只重建 api：其餘沿用 state 的標籤"
CD_MODE=deploy CD_SHA="${SHA_B}" CD_REBUILT="api" run_cd
rc=$?
expect "結束碼 0" "$([ ${rc} -eq 0 ]; echo $?)"
state_has "TAG_API=${SHA_B}"; expect "TAG_API 更新為新 sha" $?
state_has "TAG_NUXT_CLUB=${SHA_A}"; expect "TAG_NUXT_CLUB 沿用上一版" $?
grep -q "api=${SHA_B}" "${CD_HISTORY_FILE}" && grep -q "nuxt_club=${SHA_A}" "${CD_HISTORY_FILE}"; expect "history 記完整標籤組合" $?
! calls_have "tcrfc-nuxt-club:${SHA_B}"; expect "沒重建的映像檔不會被要求 :新sha" $?

echo "== 3. 只改 deploy/ 設定（無映像檔變動）：仍部署，標籤不變"
CD_MODE=deploy CD_SHA="${SHA_C}" CD_REBUILT="" run_cd
rc=$?
expect "結束碼 0" "$([ ${rc} -eq 0 ]; echo $?)"
output_has "changed_images=$"; expect "changed_images 為空" $?
state_has "LAST_GOOD_SHA=${SHA_C}"; expect "LAST_GOOD_SHA 更新" $?
state_has "TAG_API=${SHA_B}"; expect "標籤不變" $?

echo "== 4. Caddyfile 內容與執行中 proxy 不同：強制重建 proxy"
echo "# changed" >> "${CASE}/work/deploy/Caddyfile.prelaunch"
FAKE_CADDY_HASH="$(PATH="${FAKEBIN}:${PATH}" sha256sum "${CASE}/work/deploy/Caddyfile.prelaunch" | cut -d' ' -f1)"
CD_MODE=deploy CD_SHA="${SHA_C}" CD_REBUILT="" run_cd
calls_have "up -d --no-deps --force-recreate"; expect "有 force-recreate proxy" $?
grep -q "proxy 因 Caddyfile 變動而重建：true" "${CASE}/summary"; expect "summary 記錄 proxy 重建" $?

echo "== 5. 新版不健康：自動退回上一個成功版本（state 不變）"
new_case 5
CD_MODE=deploy CD_SHA="${SHA_A}" CD_REBUILT="api" run_cd   # 先有一個基準
cp "${CD_STATE_FILE}" "${CASE}/state.before"
: > "${CASE}/output"; : > "${CASE}/summary"
FAKE_BAD="${SHA_B}" CD_MODE=deploy CD_SHA="${SHA_B}" CD_REBUILT="api" run_cd
rc=$?
expect "結束碼 1（失敗但已退回）" "$([ ${rc} -eq 1 ]; echo $?)"
output_has "result=rolled_back"; expect "result=rolled_back" $?
cmp -s "${CD_STATE_FILE}" "${CASE}/state.before"; expect "deploy-state.env 沒被改" $?
grep -q "TAG_API=${SHA_A}" "${FAKE_DIR}/running"; expect "最後跑的是上一版 SHA" $?
grep -q "已自動退回" "${CASE}/summary"; expect "summary 寫明已退回" $?
[ "$(grep -c 'result=ok' "${CD_HISTORY_FILE}")" -eq 1 ]; expect "history 沒有新增失敗的版本" $?

echo "== 6. 首次銜接就失敗：退回到本機 cd-prev（手動啟動的那一版）"
new_case 6
FAKE_BAD="${SHA_A}" CD_MODE=deploy CD_SHA="${SHA_A}" CD_REBUILT="api" run_cd
rc=$?
expect "結束碼 1" "$([ ${rc} -eq 1 ]; echo $?)"
grep -q "TAG_API=cd-prev" "${FAKE_DIR}/running"; expect "退回用 cd-prev" $?
[ ! -s "${CD_STATE_FILE}" ]; expect "state 仍是空的" $?

echo "== 7. 新版不健康，退回也不健康：結束碼 2、明示人工處理"
new_case 7
FAKE_BAD="${SHA_A} cd-prev master" CD_MODE=deploy CD_SHA="${SHA_A}" CD_REBUILT="api" run_cd
rc=$?
expect "結束碼 2" "$([ ${rc} -eq 2 ]; echo $?)"
output_has "result=rollback_failed"; expect "result=rollback_failed" $?
grep -q "自動退回也失敗" "${CASE}/summary"; expect "summary 寫明退回失敗" $?

echo "== 8. 前置檢查：非法 SHA／compose 驗證失敗／拉不到映像檔 → 結束碼 1、沒動容器"
new_case 8
CD_MODE=deploy CD_SHA="not-a-sha" run_cd
rc=$?
expect "非法 SHA 結束碼 1" "$([ ${rc} -eq 1 ]; echo $?)"
output_has "result=preflight_failed"; expect "result=preflight_failed" $?
! calls_have " up "; expect "沒有執行 up" $?
FAKE_CONFIG_FAIL=1 CD_MODE=deploy CD_SHA="${SHA_A}" run_cd
rc=$?
expect "compose config 失敗結束碼 1" "$([ ${rc} -eq 1 ]; echo $?)"
FAKE_PULL_FAIL=1 CD_MODE=deploy CD_SHA="${SHA_A}" CD_REBUILT="api" CD_PULL_RETRY_SLEEP=0 run_cd
rc=$?
expect "拉不到映像檔結束碼 1" "$([ ${rc} -eq 1 ]; echo $?)"
! calls_have " up "; expect "拉不到映像檔時沒有執行 up" $?

echo "== 9. 人工回滾：歷史紀錄裡的版本"
new_case 9
CD_MODE=deploy CD_SHA="${SHA_A}" CD_REBUILT="nuxt_club nuxt_charity admin_web admin_charity api" run_cd
CD_MODE=deploy CD_SHA="${SHA_B}" CD_REBUILT="api" run_cd
: > "${CASE}/summary"
CD_MODE=rollback CD_SHA="${SHA_A}" run_cd
rc=$?
expect "結束碼 0" "$([ ${rc} -eq 0 ]; echo $?)"
state_has "TAG_API=${SHA_A}"; expect "api 回到 A" $?
state_has "LAST_GOOD_SHA=${SHA_A}"; expect "LAST_GOOD_SHA 回到 A" $?
grep -q "mode=rollback result=ok" "${CD_HISTORY_FILE}"; expect "history 記一筆 rollback" $?

echo "== 10. 人工回滾：歷史沒有該 SHA，退回 ghcr 標籤（只有 api 有該 sha 標籤）"
new_case 10
CD_MODE=deploy CD_SHA="${SHA_A}" CD_REBUILT="nuxt_club nuxt_charity admin_web admin_charity api" run_cd
FAKE_MANIFEST_MISSING="ghcr.io/waiting0201/tcrfc-nuxt-club:${SHA_C} ghcr.io/waiting0201/tcrfc-nuxt-charity:${SHA_C} ghcr.io/waiting0201/tcrfc-admin-web:${SHA_C} ghcr.io/waiting0201/tcrfc-admin-charity:${SHA_C}" \
  CD_MODE=rollback CD_SHA="${SHA_C}" run_cd
rc=$?
expect "結束碼 0" "$([ ${rc} -eq 0 ]; echo $?)"
state_has "TAG_API=${SHA_C}"; expect "api 用 :sha 標籤" $?
state_has "TAG_ADMIN_WEB=${SHA_A}"; expect "沒有該標籤的映像檔沿用目前版本" $?
FAKE_MANIFEST_MISSING="ghcr.io/waiting0201/tcrfc-nuxt-club:${SHA_B} ghcr.io/waiting0201/tcrfc-nuxt-charity:${SHA_B} ghcr.io/waiting0201/tcrfc-admin-web:${SHA_B} ghcr.io/waiting0201/tcrfc-admin-charity:${SHA_B} ghcr.io/waiting0201/tcrfc-api:${SHA_B}" \
  CD_MODE=rollback CD_SHA="${SHA_B}" run_cd
rc=$?
expect "完全找不到該 SHA 的版本：結束碼 1" "$([ ${rc} -eq 1 ]; echo $?)"

echo "== 10b. 鎖：另一個部署進行中 → 中止、不動容器"
new_case 12
printf '#!/usr/bin/env bash\nexit 1\n' > "${FAKEBIN}/flock"; chmod +x "${FAKEBIN}/flock"
CD_MODE=deploy CD_SHA="${SHA_A}" CD_REBUILT="api" run_cd
rc=$?
expect "鎖被占用：結束碼 1" "$([ ${rc} -eq 1 ]; echo $?)"
grep -q "另一個部署正在進行" "${CASE}/summary"; expect "鎖被占用：summary 註明" $?
! calls_have " up "; expect "鎖被占用：沒有執行 up" $?
rm -f "${FAKEBIN}/flock"

echo "== 11. 清 Cloudflare 快取"
new_case 11
run_purge() { PATH="${FAKEBIN}:${PATH}" /bin/bash "${HERE}/cd-purge-cache.sh" > "${CASE}/stdout" 2>&1; }
CHANGED_IMAGES="nuxt_club" run_purge
rc=$?
expect "沒 token：結束碼 0" "$([ ${rc} -eq 0 ]; echo $?)"
grep -q "略過" "${CASE}/summary"; expect "沒 token：summary 註明略過" $?
! calls_have "api.cloudflare.com"; expect "沒 token：沒有呼叫 Cloudflare" $?
Z="0123456789abcdef0123456789abcdef"
CLOUDFLARE_API_TOKEN="tok-secret-123" CF_ZONE_ID_TCRFC="${Z}" CF_ZONE_ID_BW="${Z}" CHANGED_IMAGES="nuxt_club,api" run_purge
rc=$?
expect "有 token：結束碼 0" "$([ ${rc} -eq 0 ]; echo $?)"
[ "$(grep -c 'purge_cache' "${FAKE_DIR}/calls")" -eq 2 ]; expect "nuxt_club 換了 → 清主站與藍鯨兩個主機" $?
! grep -q "tok-secret-123" "${FAKE_DIR}/calls"; expect "token 不在命令列參數裡" $?
grep -q "tok-secret-123" "${FAKE_DIR}/curl_stdin"; expect "token 走 stdin" $?
: > "${FAKE_DIR}/calls"; : > "${CASE}/summary"
CLOUDFLARE_API_TOKEN="tok-secret-123" CHANGED_IMAGES="nuxt_charity" run_purge
rc=$?
expect "缺 zone 變數：結束碼 0（不讓部署失敗）" "$([ ${rc} -eq 0 ]; echo $?)"
grep -q "CF_ZONE_ID_CHARITY" "${CASE}/summary"; expect "缺 zone 變數：summary 註明" $?
: > "${FAKE_DIR}/calls"
CLOUDFLARE_API_TOKEN="tok-secret-123" CHANGED_IMAGES="api,admin_web" run_purge
! calls_have "purge_cache"; expect "只換 api／後台：不清" $?

echo
echo "通過 ${PASS}，失敗 ${FAIL}"
[ "${FAIL}" -eq 0 ]
