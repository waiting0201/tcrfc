#!/usr/bin/env bash
# infra/provision-secrets.sh — 把機密檔寫進 VM（infra/README.md §4.3／§4.4）
#
# 在「你自己的 Mac」執行：  bash infra/provision-secrets.sh
#
# 做什麼
#   1. 用 az 取：SQL 伺服器位址與管理員登入名、兩個儲存體帳戶的連線字串、Public IP（全部唯讀）
#   2. 用 openssl 產生 JWT 簽章金鑰與 Redis 密碼；互動輸入 SQL 管理員密碼、ACME 信箱
#   3. 組出三個檔案，經 ssh 的 stdin 管線寫進 VM（本機不落地、不進命令列參數、不印出）：
#        /opt/tcrfc/secrets/club.env      俱樂部（api 讀）
#        /opt/tcrfc/secrets/charity.env   協會（api 讀）
#        /opt/tcrfc/.env                  docker compose 用（網域、Redis 密碼）
#      並建立 /opt/tcrfc/data-protection（Data Protection 金鑰環，docker-compose.yml 的 api 掛載它）
#   4. 在 VM 上驗證：只顯示鍵名與「有值／空」，並從 VM 實際連一次兩個資料庫（SELECT 1）
#
# 可重跑
#   - VM 上已存在的檔案會先問是否覆寫（預設不覆寫）
#   - 覆寫時：JWT 金鑰、Redis 密碼、網域等「非 Azure 衍生」的值預設沿用舊檔，
#     只有你明確選擇才重新產生（重生 JWT 會讓所有已登入者被登出）；舊檔裡本腳本不管理的鍵會原樣保留
#   - 2026-10-02 起測試站不再有 Basic Auth：舊 .env 若還有 PRELAUNCH_BASIC_AUTH_USER／_HASH，
#     覆寫時不會寫回（屬「已淘汰的鍵」，不會被當成手動新增的鍵保留）
#   - 連線字串一律以 az 查到的值與你輸入的 SQL 密碼重建
#
# 🔴 只負責「上線前（prelaunch）」設定。若 VM 上的 /opt/tcrfc/.env 已是 SITE_ENV=production，
#    本腳本拒絕改它，避免把正式站設回上線前狀態（robots 全擋＋X-Robots-Tag）。
#
# 環境變數（皆可省略）：
#   RG              資源群組，預設 rg-tcrfc-prod
#   VM_USER         VM 管理員，預設 azureuser
#   SSH_KEY         SSH 私鑰路徑，預設交給 ssh 自己決定
#   GHCR_OWNER      預設取 .env.example
#   IMAGE_TAG       預設 master（deploy.yml 只推 :master 與 git SHA 兩種標籤，沒有 :latest）
#   MSSQL_TOOLS_IMAGE  驗證連線用的映像檔，預設 mcr.microsoft.com/mssql-tools
set -euo pipefail

RG="${RG:-rg-tcrfc-prod}"
VM_USER="${VM_USER:-azureuser}"
SSH_KEY="${SSH_KEY:-}"
MSSQL_TOOLS_IMAGE="${MSSQL_TOOLS_IMAGE:-mcr.microsoft.com/mssql-tools}"
PIP_NAME=pip-tcrfc-prod
REMOTE_ROOT=/opt/tcrfc
REMOTE_SECRETS=/opt/tcrfc/secrets
REMOTE_DP=/opt/tcrfc/data-protection
APP_UID=1654 # aspnet 映像檔內建非 root 使用者 app（docker-compose.yml 有說明與重驗指令）

ROOT=$(cd "$(dirname "$0")/.." && pwd)
STAMP=$(date -u +%Y-%m-%dT%H:%M:%SZ)

step() { printf '\n== %s ==\n' "$1"; }
info() { printf '   %s\n' "$1"; }
die() { printf '\n錯誤：%s\n' "$1" >&2; exit 1; }

# ── 前置檢查 ─────────────────────────────────────────────────────────────
step "前置檢查"
for tool in az ssh openssl awk sed; do
  command -v "${tool}" >/dev/null 2>&1 || die "找不到 ${tool}"
done
az account show >/dev/null 2>&1 || die "請先 az login"
[ -f "${ROOT}/.env.example" ] || die "找不到 ${ROOT}/.env.example"
[ -f "${ROOT}/docker-compose.yml" ] || die "找不到 ${ROOT}/docker-compose.yml"
if [ -n "${SSH_KEY}" ] && [ ! -f "${SSH_KEY}" ]; then die "SSH_KEY 指向的檔案不存在：${SSH_KEY}"; fi

# 從 .env.example 取預設值（只取鍵名，不 source，避免執行檔案內容）
example_get() {
  awk -v k="$1" 'index($0, k "=") == 1 { print substr($0, length(k) + 2); exit }' "${ROOT}/.env.example"
}
valid_domain() { printf '%s' "$1" | grep -Eq '^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$'; }

# 已淘汰的鍵：不再管理、也不當成「手動新增」保留（舊檔覆寫時丟掉）
COMPOSE_RETIRED_KEYS="PRELAUNCH_BASIC_AUTH_USER PRELAUNCH_BASIC_AUTH_HASH"
DOMAIN_KEYS="TCRFC_DOMAIN BW_DOMAIN CHARITY_DOMAIN ADMIN_WEB_DOMAIN ADMIN_CHARITY_DOMAIN API_DOMAIN"
for k in ${DOMAIN_KEYS}; do
  v=$(example_get "${k}")
  valid_domain "${v}" || die ".env.example 的 ${k} 解析不到合法網域"
done
GHCR_OWNER="${GHCR_OWNER:-$(example_get GHCR_OWNER)}"
IMAGE_TAG="${IMAGE_TAG:-master}"
[ -n "${GHCR_OWNER}" ] || die "GHCR_OWNER 為空"

# ── az 查詢（唯讀）──────────────────────────────────────────────────────
# 回傳非空、非 None 的單一值，否則中止並說明是哪一項
az_value() { # $1=說明，其餘＝az 指令
  local what=$1 out; shift
  out=$("$@") || die "az 指令失敗（${what}）"
  case "${out}" in
    "" | None | null) die "查不到${what}（資源群組 ${RG} 內沒有對應資源？）" ;;
  esac
  printf '%s' "${out}"
}
# 必須恰好一筆
az_single() { # $1=說明，其餘＝會輸出多行名稱的 az 指令
  local what=$1 out count; shift
  out=$("$@") || die "az 指令失敗（${what}）"
  count=$(printf '%s\n' "${out}" | awk 'NF { n++ } END { print n + 0 }')
  [ "${count}" = "1" ] || die "${what}應該恰好 1 筆，實際 ${count} 筆"
  printf '%s' "${out}"
}

step "從 Azure 取得資源資訊（唯讀）"
VM_IP=$(az_value "Public IP" az network public-ip show -g "${RG}" -n "${PIP_NAME}" --query ipAddress -o tsv)
SQL_NAME=$(az_single "SQL 伺服器" az sql server list -g "${RG}" --query "[].name" -o tsv)
SQL_FQDN=$(az_value "SQL 伺服器位址" az sql server show -g "${RG}" -n "${SQL_NAME}" --query fullyQualifiedDomainName -o tsv)
SQL_ADMIN=$(az_value "SQL 管理員登入名" az sql server show -g "${RG}" -n "${SQL_NAME}" --query administratorLogin -o tsv)
SA_CLUB=$(az_single "俱樂部儲存體帳戶" az storage account list -g "${RG}" --query "[?starts_with(name, 'sttcrfcclub')].name" -o tsv)
SA_CHARITY=$(az_single "慈善儲存體帳戶" az storage account list -g "${RG}" --query "[?starts_with(name, 'sttcrfccharity')].name" -o tsv)
MEDIA_BASE_URL_DEFAULT="$(az_value "俱樂部儲存體 blob 端點" az storage account show -g "${RG}" -n "${SA_CLUB}" --query primaryEndpoints.blob -o tsv)"
MEDIA_BASE_URL_DEFAULT="${MEDIA_BASE_URL_DEFAULT%/}/images"
case "${MEDIA_BASE_URL_DEFAULT}" in
  https://*.blob.core.windows.net/images) ;;
  *) die "俱樂部 blob 端點格式異常，無法推出 MEDIA_BASE_URL" ;;
esac
BLOB_CLUB=$(az_value "俱樂部儲存體連線字串" az storage account show-connection-string -g "${RG}" -n "${SA_CLUB}" --query connectionString -o tsv)
BLOB_CHARITY=$(az_value "慈善儲存體連線字串" az storage account show-connection-string -g "${RG}" -n "${SA_CHARITY}" --query connectionString -o tsv)
info "SQL 伺服器：${SQL_NAME}"
info "儲存體：${SA_CLUB}、${SA_CHARITY}"
info "VM 位址已取得"

# ── SSH 輔助 ────────────────────────────────────────────────────────────
# vm：stdin 由呼叫端決定（要送內容就用管線；不送就明確 </dev/null，否則 ssh 會吞掉終端機輸入）
vm() {
  if [ -n "${SSH_KEY}" ]; then
    ssh -i "${SSH_KEY}" -o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 "${VM_USER}@${VM_IP}" "$@"
  else
    ssh -o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 "${VM_USER}@${VM_IP}" "$@"
  fi
}

step "確認 VM 可連線"
vm 'sudo -n true && command -v docker >/dev/null && test -d /opt/tcrfc/secrets' </dev/null \
  || die "無法連到 VM，或 VM 缺少 passwordless sudo／docker／/opt/tcrfc/secrets（SSH 來源 IP 是否在 NSG 允許名單？cloud-init 是否完成？）"
info "連線正常，docker 與 /opt/tcrfc/secrets 都在"

# 檔案是否存在：0＝存在、1＝不存在，ssh 本身失敗直接中止（不把連線失敗當成「不存在」）
remote_exists() {
  local rc=0
  vm "sudo test -f '$1'" </dev/null || rc=$?
  case "${rc}" in
    0) return 0 ;;
    1) return 1 ;;
    *) die "ssh 檢查 $1 失敗（結束碼 ${rc}）" ;;
  esac
}
remote_cat() { vm "sudo cat '$1'" </dev/null; }

# 把內容（變數）經 stdin 寫成 VM 上的檔案：先寫暫存檔再 mv，避免連線中斷留下半個檔案。
# 檔案路徑是命令列唯一出現的東西；內容只走管線（printf 是 shell 內建，不會出現在 ps）。
remote_write() { # $1=路徑  $2=內容
  local path=$1 content=$2
  printf '%s\n' "${content}" | vm "sudo install -o runner -g runner -m 600 /dev/stdin '${path}.new' && sudo mv '${path}.new' '${path}'" \
    || die "寫入 ${path} 失敗"
}

# 從內容字串取某鍵的值（去掉成對單引號）；沒有則輸出空字串
kv_get() { # $1=內容  $2=鍵
  local v
  v=$(printf '%s\n' "$1" | awk -v k="$2" 'index($0, k "=") == 1 && !f { v = substr($0, length(k) + 2); f = 1 } END { print v }')
  v=${v#\'}
  v=${v%\'}
  printf '%s' "${v}"
}
# 舊檔中「不在管理清單」的 KEY=VALUE 行，原樣保留
kv_unmanaged() { # $1=內容  $2=以空白分隔的管理鍵
  printf '%s\n' "$1" | awk -v managed=" $2 " '
    /^[A-Za-z_][A-Za-z0-9_]*=/ { split($0, p, "="); if (index(managed, " " p[1] " ") == 0) print }'
}
# 保留行的鍵名（只顯示鍵名給使用者看）
kv_keys() { printf '%s\n' "$1" | awk -F= 'NF && $1 ~ /^[A-Za-z_][A-Za-z0-9_]*$/ { print $1 }'; }

ask_yes_no() { # $1=問題  $2=預設（y 或 n）
  local ans prompt
  if [ "$2" = "y" ]; then prompt="(Y/n)"; else prompt="(y/N)"; fi
  while true; do
    read -rp "${1} ${prompt} " ans || die "輸入中斷"
    ans=${ans:-$2}
    case "${ans}" in
      y | Y | yes | YES) return 0 ;;
      n | N | no | NO) return 1 ;;
      *) echo "   請輸入 y 或 n" ;;
    esac
  done
}

gen_hex() { # $1=位元組數
  local out
  out=$(openssl rand -hex "$1") || die "openssl rand 失敗"
  [ "${#out}" -ge $(($1 * 2)) ] || die "openssl rand 輸出長度不足"
  printf '%s' "${out}"
}

# ── 判斷哪些檔案要處理 ─────────────────────────────────────────────────────
step "檢查 VM 上現有的檔案"
CLUB_PATH="${REMOTE_SECRETS}/club.env"
CHARITY_PATH="${REMOTE_SECRETS}/charity.env"
COMPOSE_PATH="${REMOTE_ROOT}/.env"

DO_CLUB=1 DO_CHARITY=1 DO_COMPOSE=1
OLD_CLUB="" OLD_CHARITY="" OLD_COMPOSE=""

consider() { # $1=標籤  $2=路徑  $3=旗標變數名  $4=舊內容變數名
  if remote_exists "$2"; then
    if ask_yes_no "$1 已存在，要覆寫嗎？（會先沿用舊檔的非 Azure 衍生值）" n; then
      local content
      content=$(remote_cat "$2") || die "讀取 $2 失敗"
      printf -v "$4" '%s' "${content}"
    else
      printf -v "$3" '%s' 0
      info "${1}：保留原檔，略過"
    fi
  else
    info "${1}：不存在，將建立"
  fi
}
consider "club.env" "${CLUB_PATH}" DO_CLUB OLD_CLUB
consider "charity.env" "${CHARITY_PATH}" DO_CHARITY OLD_CHARITY
consider "/opt/tcrfc/.env" "${COMPOSE_PATH}" DO_COMPOSE OLD_COMPOSE

if [ "${DO_COMPOSE}" = 1 ] && [ -n "${OLD_COMPOSE}" ]; then
  if [ "$(kv_get "${OLD_COMPOSE}" SITE_ENV)" = "production" ]; then
    DO_COMPOSE=0
    info "/opt/tcrfc/.env 已是 SITE_ENV=production：本腳本只處理上線前設定，這個檔案不會被改動，請手動編輯"
  fi
fi

# ── 決定哪些「可重生」的機密要沿用 ───────────────────────────────────────────
OLD_JWT_CLUB=$(kv_get "${OLD_CLUB}" JWT_SIGNING_KEY_CLUB)
OLD_JWT_MEMBER=$(kv_get "${OLD_CLUB}" JWT_SIGNING_KEY_MEMBER)
OLD_JWT_CHARITY=$(kv_get "${OLD_CHARITY}" JWT_SIGNING_KEY_CHARITY)
OLD_REDIS=$(kv_get "${OLD_COMPOSE}" REDIS_PASSWORD)

REGEN=0
if [ -n "${OLD_JWT_CLUB}${OLD_JWT_MEMBER}${OLD_JWT_CHARITY}${OLD_REDIS}" ]; then
  echo
  echo "舊檔裡已有 JWT 簽章金鑰／Redis 密碼。預設沿用；重新產生會讓所有已登入的人（後台、會員、慈善後台）被登出，"
  echo "並讓後台行事曆訂閱連結失效（行事曆訂閱 token 由 JWT_SIGNING_KEY_CLUB 衍生）。"
  if ask_yes_no "要重新產生這些金鑰嗎？" n; then REGEN=1; fi
fi
pick_secret() { # $1=舊值  $2=位元組數
  if [ "${REGEN}" = 0 ] && [ -n "$1" ]; then printf '%s' "$1"; else gen_hex "$2"; fi
}

# ── 互動輸入（只問需要的）─────────────────────────────────────────────────
read_secret_twice() { # $1=提示  輸出到變數 SECRET_OUT
  local a b
  while true; do
    read -rsp "${1}：" a || die "輸入中斷"; echo
    read -rsp "再輸入一次確認：" b || die "輸入中斷"; echo
    if [ -z "${a}" ]; then echo "   不可為空"; continue; fi
    if [ "${a}" != "${b}" ]; then echo "   兩次輸入不一致"; continue; fi
    SECRET_OUT=${a}
    return 0
  done
}

SQL_PASSWORD=""
if [ "${DO_CLUB}" = 1 ] || [ "${DO_CHARITY}" = 1 ]; then
  step "輸入 SQL 管理員密碼"
  echo "   這是 GitHub secret SQL_ADMIN_PASSWORD 的同一個值。輸入時不會顯示。"
  echo "   連線字串無法安全表示的字元（; ' \" \\ 以及前後空白、換行）不接受。"
  while true; do
    read_secret_twice "SQL 管理員密碼"
    SQL_PASSWORD=${SECRET_OUT}
    case "${SQL_PASSWORD}" in
      *\;* | *\'* | *\"* | *\\*)
        echo "   密碼含有 ; ' \" \\ 之一，無法放進連線字串。請先把 SQL 密碼改成不含這些字元的值"
        echo "   （更新 GitHub secret SQL_ADMIN_PASSWORD 後重跑 infra.yml），再重新執行本腳本。"
        die "SQL 密碼含不支援的字元"
        ;;
    esac
    if [ "${SQL_PASSWORD}" != "$(printf '%s' "${SQL_PASSWORD}" | awk '{ gsub(/^[ \t]+|[ \t]+$/, ""); print }')" ]; then
      die "SQL 密碼前後有空白，不接受"
    fi
    break
  done
fi

if [ "${DO_COMPOSE}" = 1 ]; then
  step "Let's Encrypt 信箱"
  for rk in ${COMPOSE_RETIRED_KEYS}; do
    if [ -n "$(kv_get "${OLD_COMPOSE}" "${rk}")" ]; then info "舊檔的 ${rk} 已淘汰（測試站不再有 Basic Auth），不會寫回"; fi
  done
  OLD_ACME=$(kv_get "${OLD_COMPOSE}" ACME_EMAIL)
  while true; do
    if [ -n "${OLD_ACME}" ] && [ "${OLD_ACME}" != "ops@example.tw" ]; then
      read -rp "Let's Encrypt 通知信箱（Enter 沿用 ${OLD_ACME}）：" ACME_EMAIL || die "輸入中斷"
      ACME_EMAIL=${ACME_EMAIL:-${OLD_ACME}}
    else
      read -rp "Let's Encrypt 通知信箱：" ACME_EMAIL || die "輸入中斷"
    fi
    if printf '%s' "${ACME_EMAIL}" | grep -Eq '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$' \
      && [ "${ACME_EMAIL}" != "ops@example.tw" ]; then
      break
    fi
    echo "   不是有效信箱（也不能用範本的 ops@example.tw）"
  done
fi

# ── 組內容 ──────────────────────────────────────────────────────────────
CLUB_MANAGED="CLUB_SQL_CONNECTION_STRING AZURE_BLOB_CONNECTION_STRING JWT_SIGNING_KEY_CLUB JWT_SIGNING_KEY_MEMBER"
CHARITY_MANAGED="CHARITY_SQL_CONNECTION_STRING AZURE_BLOB_CONNECTION_STRING_CHARITY JWT_SIGNING_KEY_CHARITY"
COMPOSE_MANAGED="GHCR_OWNER IMAGE_TAG SITE_ENV ${DOMAIN_KEYS} ACME_EMAIL CADDYFILE REDIS_PASSWORD MEDIA_BASE_URL"

conn_string() { # $1=資料庫名
  printf 'Server=tcp:%s,1433;Database=%s;User ID=%s;Password=%s;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;' \
    "${SQL_FQDN}" "$1" "${SQL_ADMIN}" "${SQL_PASSWORD}"
}

append_unmanaged() { # $1=舊內容  $2=管理鍵  輸出附加段（無則空）
  local extra
  extra=$(kv_unmanaged "$1" "$2")
  if [ -n "${extra}" ]; then
    printf '\n# 以下為手動新增的鍵，provision-secrets.sh 原樣保留\n%s\n' "${extra}"
  fi
}

emit_club() {
  cat <<EOF
# club.env — 俱樂部機密（api 讀）。由 infra/provision-secrets.sh 於 ${STAMP} 產生，權限 600、擁有者 runner。
# 值用單引號包起來：compose 的 env_file 會把未加引號的 \$ 當變數展開。鍵名與用途見 infra/README.md §4.3。
# 刻意不放：PAYMENT_GATEWAY／INVOICE_ISSUER／EMAIL_SENDER（正式環境不得設 fake／localfile，未設＝「尚未串接」）、
#   LINE_LOGIN_*（未設＝LINE 登入端點回 503）、AZURE_BLOB_PUBLIC_BASE_URL（Cloudflare 圖片網域就緒後再加）。
CLUB_SQL_CONNECTION_STRING='$(conn_string tcrfc_club)'
AZURE_BLOB_CONNECTION_STRING='${BLOB_CLUB}'
JWT_SIGNING_KEY_CLUB=${JWT_CLUB}
JWT_SIGNING_KEY_MEMBER=${JWT_MEMBER}
EOF
}

emit_charity() {
  cat <<EOF
# charity.env — 協會機密（api 讀）。由 infra/provision-secrets.sh 於 ${STAMP} 產生，權限 600、擁有者 runner。
# 🔴 協會的憑證與俱樂部分開，不得共用任何值。值用單引號包起來的理由同 club.env。
# 刻意不放：CHARITY_ALLOW_FAKE_PROVIDERS（未設＝金流／發票／寄信「尚未設定」，不會假成功）、
#   TURNSTILE_SECRET_KEY_CHARITY（未設＝不做人機驗證，只剩 IP 限流）、AZURE_BLOB_PUBLIC_BASE_URL_CHARITY。
CHARITY_SQL_CONNECTION_STRING='$(conn_string tcrfc_charity)'
AZURE_BLOB_CONNECTION_STRING_CHARITY='${BLOB_CHARITY}'
JWT_SIGNING_KEY_CHARITY=${JWT_CHARITY}
EOF
}

emit_compose() {
  cat <<EOF
# /opt/tcrfc/.env — docker compose 用。由 infra/provision-secrets.sh 於 ${STAMP} 產生，權限 600、擁有者 runner。
# 切到正式網址時手動編輯：六個網域換正式值、SITE_ENV=production、註解掉 CADDYFILE
# （infra/README.md §5）。之後本腳本會拒絕改這個檔案。
GHCR_OWNER=$(carry GHCR_OWNER "${GHCR_OWNER}")
IMAGE_TAG=$(carry IMAGE_TAG "${IMAGE_TAG}")
SITE_ENV=prelaunch
${DOMAIN_LINES}ACME_EMAIL=${ACME_EMAIL}
CADDYFILE=./deploy/Caddyfile.prelaunch
REDIS_PASSWORD=${REDIS_PW}
# 站台照片（Blob 公開唯讀容器 images）的網址基底，前台執行期設定 NUXT_PUBLIC_MEDIA_BASE_URL 的來源（infra/README.md §4.9）。
# 預設由 az 查出俱樂部儲存體帳戶；日後換成 Cloudflare 圖片網域時改這一行即可，本腳本會沿用手改過的值。
MEDIA_BASE_URL=${MEDIA_URL}
EOF
}

if [ "${DO_CLUB}" = 1 ]; then
  JWT_CLUB=$(pick_secret "${OLD_JWT_CLUB}" 32)
  JWT_MEMBER=$(pick_secret "${OLD_JWT_MEMBER}" 32)
  CLUB_CONTENT=$(emit_club)
  CLUB_CONTENT="${CLUB_CONTENT}$(append_unmanaged "${OLD_CLUB}" "${CLUB_MANAGED}")"
fi

if [ "${DO_CHARITY}" = 1 ]; then
  JWT_CHARITY=$(pick_secret "${OLD_JWT_CHARITY}" 32)
  CHARITY_CONTENT=$(emit_charity)
  CHARITY_CONTENT="${CHARITY_CONTENT}$(append_unmanaged "${OLD_CHARITY}" "${CHARITY_MANAGED}")"
fi

if [ "${DO_COMPOSE}" = 1 ]; then
  REDIS_PW=$(pick_secret "${OLD_REDIS}" 24)
  # 舊檔有值就沿用（使用者手動調整過的網域／映像檔 tag 不被蓋回預設）；沒有才用 .env.example／預設
  carry() { # $1=鍵  $2=預設
    local old
    old=$(kv_get "${OLD_COMPOSE}" "$1")
    if [ -n "${old}" ]; then printf '%s' "${old}"; else printf '%s' "$2"; fi
  }
  MEDIA_URL=$(carry MEDIA_BASE_URL "${MEDIA_BASE_URL_DEFAULT}")
  case "${MEDIA_URL}" in
    https://*) ;;
    *) die "MEDIA_BASE_URL 必須以 https:// 開頭：${MEDIA_URL}" ;;
  esac
  case "${MEDIA_URL}" in
    */ | *[[:space:]]* | *\'* | *\"*) die "MEDIA_BASE_URL 不得以 / 結尾，也不得含空白或引號" ;;
  esac
  DOMAIN_LINES=""
  for k in ${DOMAIN_KEYS}; do
    val=$(carry "${k}" "$(example_get "${k}")")
    valid_domain "${val}" || die "網域 ${k} 的值不合法：${val}"
    DOMAIN_LINES="${DOMAIN_LINES}${k}=${val}
"
  done
  COMPOSE_CONTENT=$(emit_compose)
  COMPOSE_CONTENT="${COMPOSE_CONTENT}$(append_unmanaged "${OLD_COMPOSE}" "${COMPOSE_MANAGED} ${COMPOSE_RETIRED_KEYS}")"
fi

# ── 摘要與確認（不顯示任何值）───────────────────────────────────────────────
step "即將寫入 VM"
[ "${DO_CLUB}" = 1 ] && info "${CLUB_PATH}（鍵：$(kv_keys "${CLUB_CONTENT}" | tr '\n' ' ')）"
[ "${DO_CHARITY}" = 1 ] && info "${CHARITY_PATH}（鍵：$(kv_keys "${CHARITY_CONTENT}" | tr '\n' ' ')）"
[ "${DO_COMPOSE}" = 1 ] && info "${COMPOSE_PATH}（鍵：$(kv_keys "${COMPOSE_CONTENT}" | tr '\n' ' ')）"
info "${REMOTE_DP}（Data Protection 金鑰環目錄，擁有者 ${APP_UID}:${APP_UID}，權限 700）"
if [ "${REGEN}" = 1 ]; then info "⚠️ 已選擇重新產生 JWT 金鑰／Redis 密碼"; fi
ask_yes_no "確認寫入？" y || die "已取消，VM 上沒有任何變更"

# ── 寫入 ─────────────────────────────────────────────────────────────────
step "寫入"
# 金鑰環目錄：不存在才建、已存在不動內容。擁有者是容器內的 app 使用者（uid 1654），0700。
# docker-compose.yml 以 create_host_path: false 掛載它；缺目錄時 compose 會明確報錯。
vm "sudo install -d -o ${APP_UID} -g ${APP_UID} -m 700 '${REMOTE_DP}'" </dev/null || die "建立 ${REMOTE_DP} 失敗"
info "已確認 ${REMOTE_DP}"
if [ "${DO_CLUB}" = 1 ]; then remote_write "${CLUB_PATH}" "${CLUB_CONTENT}"; info "已寫入 club.env"; fi
if [ "${DO_CHARITY}" = 1 ]; then remote_write "${CHARITY_PATH}" "${CHARITY_CONTENT}"; info "已寫入 charity.env"; fi
if [ "${DO_COMPOSE}" = 1 ]; then remote_write "${COMPOSE_PATH}" "${COMPOSE_CONTENT}"; info "已寫入 .env"; fi

# 清掉本機變數（腳本結束行程就沒了，這裡只是不讓後面的程式碼誤用）
CLUB_CONTENT="" CHARITY_CONTENT="" COMPOSE_CONTENT="" SQL_PASSWORD="" BLOB_CLUB="" BLOB_CHARITY=""
JWT_CLUB="" JWT_MEMBER="" JWT_CHARITY="" REDIS_PW="" OLD_CLUB="" OLD_CHARITY="" OLD_COMPOSE=""

# ── 在 VM 上驗證 ────────────────────────────────────────────────────────
step "驗證（在 VM 上；只顯示鍵名與是否有值）"
# 遠端腳本走 stdin，不把任何內容放進命令列。連線字串解析後以環境變數（sqlcmd 的 SQLCMD*）傳進容器：
# `docker run -e NAME` 不帶值，從 docker CLI 的環境變數轉送，所以密碼不會出現在 ps。
VERIFY_FAILED=0
vm "bash -s -- '${CLUB_PATH}' '${CHARITY_PATH}' '${COMPOSE_PATH}' '${REMOTE_DP}' '${APP_UID}' '${MSSQL_TOOLS_IMAGE}'" <<'REMOTE' || VERIFY_FAILED=1
set -euo pipefail
club=${1} charity=${2} compose=${3} dp=${4} uid=${5} image=${6}
fail=0

show_keys() {
  local f=${1}
  if ! sudo test -f "${f}"; then echo "   [缺] ${f} 不存在"; fail=1; return; fi
  echo "   ${f}  （$(sudo stat -c '擁有者 %U，權限 %a' "${f}")）"
  sudo awk -F= -v q="'" '/^[A-Za-z_][A-Za-z0-9_]*=/ {
      k = $1; v = substr($0, length(k) + 2)
      if (substr(v, 1, 1) == q) v = substr(v, 2)
      if (substr(v, length(v), 1) == q) v = substr(v, 1, length(v) - 1)
      printf "     %-40s %s\n", k, (length(v) > 0 ? "有值" : "空")
    }' "${f}"
}
show_keys "${club}"
show_keys "${charity}"
show_keys "${compose}"

echo "   ${dp}  （$(sudo stat -c '擁有者 %u:%g，權限 %a' "${dp}")）"
if [ "$(sudo stat -c '%u:%g %a' "${dp}")" != "${uid}:${uid} 700" ]; then
  echo "   [錯] 金鑰環目錄應為 ${uid}:${uid} 700"; fail=1
fi

test_db() { # ${1}=env 檔  ${2}=鍵名
  local line server user pass db part
  line=$(sudo awk -v k="${2}" 'index($0, k "=") == 1 { print substr($0, length(k) + 2); exit }' "${1}")
  line=${line#\'}; line=${line%\'}
  if [ -z "${line}" ]; then echo "   [錯] ${2} 沒有值"; fail=1; return; fi
  server="" user="" pass="" db=""
  local IFS=';'
  for part in ${line}; do
    case "${part}" in
      Server=*) server=${part#Server=} ;;
      "User ID="*) user=${part#User ID=} ;;
      Password=*) pass=${part#Password=} ;;
      Database=*) db=${part#Database=} ;;
    esac
  done
  if [ -z "${server}" ] || [ -z "${user}" ] || [ -z "${pass}" ] || [ -z "${db}" ]; then
    echo "   [錯] ${2} 解析不出 Server／User ID／Password／Database"; fail=1; return
  fi
  export SQLCMDSERVER="${server}" SQLCMDUSER="${user}" SQLCMDPASSWORD="${pass}" SQLCMDDBNAME="${db}"
  if out=$(sudo -E docker run --rm -e SQLCMDSERVER -e SQLCMDUSER -e SQLCMDPASSWORD -e SQLCMDDBNAME \
        "${image}" /opt/mssql-tools/bin/sqlcmd -N -l 30 -b -h -1 -Q "SET NOCOUNT ON; SELECT 1" 2>&1); then
    echo "   [OK] ${2} → 資料庫 ${db}：SELECT 1 成功"
  else
    echo "   [錯] ${2} 連線失敗（以下是 sqlcmd 的錯誤訊息，不含密碼）："
    printf '%s\n' "${out}" | sed 's/^/        /'
    fail=1
  fi
  unset SQLCMDSERVER SQLCMDUSER SQLCMDPASSWORD SQLCMDDBNAME
}
echo "   連線測試（映像檔 ${image}）："
test_db "${club}" CLUB_SQL_CONNECTION_STRING
test_db "${charity}" CHARITY_SQL_CONNECTION_STRING
exit "${fail}"
REMOTE

if [ "${VERIFY_FAILED}" = 1 ]; then
  die "驗證有失敗項目，請看上面輸出。檔案已寫入，修正後可直接重跑本腳本"
fi

step "完成"
echo "   下一步：infra/README.md §4.4 之後（Cloudflare DNS → 部署）。"
echo "   金鑰環目錄 ${REMOTE_DP} 沒有自動備份，請依 infra/README.md §4.3「金鑰環備份」處理。"
