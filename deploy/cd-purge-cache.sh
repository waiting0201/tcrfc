#!/usr/bin/env bash
# deploy/cd-purge-cache.sh — 部署成功後清 Cloudflare 快取（docs/20-cicd.md §4「部署後：清 Cloudflare 快取」）。
#
# 🔵 「選配」：沒設 CLOUDFLARE_API_TOKEN 就略過並在 job summary 註明，**絕不讓部署因此失敗**；
# 呼叫 Cloudflare 失敗也只警告、結束碼 0（部署已經健康，快取最多自然過期）。
#
# 清哪些：依「哪個映像檔換了」決定（CHANGED_IMAGES，cd-deploy.sh 的輸出）
#   nuxt_club    → 主站與藍鯨兩個網域
#   nuxt_charity → 慈善網域
#   其他（admin_*／api）→ 不清（後台與 API 不經 Cloudflare 快取內容）
# 用「依主機名稱清除（hosts）」而不是整個 zone：暫用網域 4webdemo.com 與其他網站共用同一個 zone，
# purge_everything 會連帶清掉別人的快取；hosts 清除只影響指定主機。
#
# 輸入（環境變數）：
#   CLOUDFLARE_API_TOKEN   選配；權限只要 Zone → Cache Purge，限定對應 zone
#   CF_ZONE_ID_TCRFC／CF_ZONE_ID_BW／CF_ZONE_ID_CHARITY   Actions Variables（zone ID 不是機密）；
#                          暫用網域期間三個可以是同一個 zone ID
#   CHANGED_IMAGES         逗號分隔的映像檔鍵
#   CD_ENV_FILE            預設 /opt/tcrfc/.env（讀網域名稱）
# token 以 curl -K - 從 stdin 餵入，不出現在命令列參數（ps 看不到）。

set -euo pipefail

CD_ENV_FILE="${CD_ENV_FILE:-/opt/tcrfc/.env}"
CHANGED_IMAGES="${CHANGED_IMAGES:-}"
SUMMARY_FILE="${GITHUB_STEP_SUMMARY:-/dev/stdout}"

summary() { printf '%s\n' "$*" >> "${SUMMARY_FILE}"; }
log() { printf '[purge] %s\n' "$*"; }

file_get() {
  local line
  line="$(grep -E "^${2}=" "$1" 2> /dev/null | tail -n 1 || true)"
  line="${line#*=}"
  line="${line%$'\r'}"
  case "${line}" in
    \"*\") line="${line#\"}"; line="${line%\"}" ;;
    \'*\') line="${line#\'}"; line="${line%\'}" ;;
  esac
  printf '%s' "${line}"
}

if [ -z "${CLOUDFLARE_API_TOKEN:-}" ]; then
  log "沒有設定 CLOUDFLARE_API_TOKEN，略過清快取"
  summary "### ⚪ Cloudflare 快取：略過"
  summary ""
  summary "沒有設定 \`CLOUDFLARE_API_TOKEN\`（選配）。若本次改了前台內容，Cloudflare 邊緣可能留著舊版 HTML；設定方式見 infra/README.md「Cloudflare 清快取 token（選配）」。"
  exit 0
fi

want_club=false
want_charity=false
case ",${CHANGED_IMAGES}," in *,nuxt_club,*) want_club=true ;; esac
case ",${CHANGED_IMAGES}," in *,nuxt_charity,*) want_charity=true ;; esac

if [ "${want_club}" = false ] && [ "${want_charity}" = false ]; then
  log "本次沒有換前台映像檔，不需要清快取"
  summary "### ⚪ Cloudflare 快取：本次不需要（前台映像檔沒換）"
  exit 0
fi

purged=""
skipped=""

purge_host() { # 參數：說明 網域鍵 zone 變數名
  local label="$1" domain_key="$2" zone_var="$3" domain zone body
  domain="$(file_get "${CD_ENV_FILE}" "${domain_key}")"
  zone="${!zone_var:-}"
  if [ -z "${domain}" ]; then skipped="${skipped} ${label}（.env 沒有 ${domain_key}）"; return 0; fi
  if [ -z "${zone}" ]; then skipped="${skipped} ${label}（沒設 Actions Variable ${zone_var}）"; return 0; fi
  if ! [[ "${zone}" =~ ^[0-9a-f]{32}$ ]]; then skipped="${skipped} ${label}（${zone_var} 不是 32 字元的 zone ID）"; return 0; fi
  body="$(printf 'header = "Authorization: Bearer %s"\n' "${CLOUDFLARE_API_TOKEN}" \
    | curl -sS -K - --max-time 30 -X POST \
      -H 'Content-Type: application/json' \
      --data "{\"hosts\":[\"${domain}\"]}" \
      "https://api.cloudflare.com/client/v4/zones/${zone}/purge_cache" 2> /dev/null || true)"
  if printf '%s' "${body}" | grep -Eq '"success" *: *true'; then
    purged="${purged} ${domain}"
  else
    skipped="${skipped} ${label}（Cloudflare 回應失敗：請確認 token 權限與 zone）"
  fi
}

if [ "${want_club}" = true ]; then
  purge_host 主站 TCRFC_DOMAIN CF_ZONE_ID_TCRFC
  purge_host 藍鯨 BW_DOMAIN CF_ZONE_ID_BW
fi
if [ "${want_charity}" = true ]; then
  purge_host 慈善 CHARITY_DOMAIN CF_ZONE_ID_CHARITY
fi

summary "### Cloudflare 快取"
summary ""
summary "- 已清除：${purged:-（無）}"
if [ -n "${skipped}" ]; then
  summary "- ⚠️ 未清除：${skipped}"
  log "有項目未清除（不影響部署結果）：${skipped}"
fi
exit 0
