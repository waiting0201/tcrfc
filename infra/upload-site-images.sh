#!/usr/bin/env bash
# infra/upload-site-images.sh — 把主站前台的站台照片處理後上傳到 Azure Blob（infra/README.md §4.9）
#
# 在「你自己的 Mac」執行：
#   bash infra/upload-site-images.sh --dry-run     # 只列出會處理／上傳哪些檔，不處理、不上傳
#   bash infra/upload-site-images.sh               # 實際處理並上傳
#
# 做什麼
#   1. 讀 apps/web/public/assets/img/ 的來源照片（🔴 只讀：容器以唯讀掛載，腳本不寫、不刪、不改來源檔）。
#      範圍＝apps/web/scripts/site-images.txt 列的檔案（前台實際引用的照片）；清單不存在時退回
#      「整個資料夾」並警告。.svg 不處理（隨前台建置）。
#   2. 在容器（ImageMagick）裡依規劃書 §4.0 圖片上傳通則處理：依 EXIF 方向轉正 → 長邊超過 2560px
#      才等比縮小 → 去除全部中繼資料（EXIF 含 GPS、XMP、ICC）→ 存 WebP。處理結果只寫進暫存目錄，
#      結束時清掉。每張處理後立刻驗證：格式為 WebP、長邊 <= 2560、沒有任何 profile／EXIF。
#   3. 上傳到 俱樂部儲存體帳戶 的容器 images，物件鍵 site/<相對路徑，副檔名改 .webp>，
#      Content-Type: image/webp、Cache-Control: public, max-age=604800。
#
# 可重跑：每個物件的 metadata 記著「處理配方＋來源檔 SHA-256」；來源與配方都沒變就略過。
#         改了來源檔、品質或長邊上限就會重傳。本腳本永遠不刪雲端物件（雲端多出來的檔不管）。
#
# 認證（不印出任何金鑰）
#   AUTH_MODE=login（預設）：用 az login 的身分，需要儲存體帳戶上的「Storage Blob Data Contributor」角色。
#                           沒有時腳本會停在上傳前，並印出授權指令（由你自己執行）。
#   AUTH_MODE=key         ：讓 az 自己用帳戶金鑰（需要你對帳戶有 listKeys 權限；金鑰不會出現在命令列或輸出）。
#
# 環境變數（皆可省略）
#   RG                資源群組，預設 rg-tcrfc-prod
#   STORAGE_ACCOUNT   儲存體帳戶，預設從 RG 內名稱以 sttcrfcclub 開頭者查出（必須恰好一個）
#   CONTAINER         容器，預設 images
#   AUTH_MODE         login 或 key，預設 login
#   QUALITY           WebP 品質，預設 82
#   JOBS              容器內平行處理數，預設 4
#   SITE_IMG_SRC      來源資料夾，預設 apps/web/public/assets/img（測試用；永遠只讀）
#   IM_IMAGE          ImageMagick 映像檔，預設 dpokidov/imagemagick:latest（需 ImageMagick 7 且支援 WebP）
#
# 選項
#   --dry-run         只列出清單與狀態
#   --all             忽略 site-images.txt，處理整個資料夾
#   --list <檔案>     指定清單檔（每行一個相對路徑；# 開頭與空行忽略）
#   --force           忽略雲端現況，全部重傳
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/.." && pwd)
SRC_DIR="${SITE_IMG_SRC:-${ROOT}/apps/web/public/assets/img}"
LIST_FILE="${ROOT}/apps/web/scripts/site-images.txt"

RG="${RG:-rg-tcrfc-prod}"
STORAGE_ACCOUNT="${STORAGE_ACCOUNT:-}"
CONTAINER="${CONTAINER:-images}"
AUTH_MODE="${AUTH_MODE:-login}"
QUALITY="${QUALITY:-82}"
JOBS="${JOBS:-4}"
IM_IMAGE="${IM_IMAGE:-dpokidov/imagemagick:latest}"

KEY_PREFIX="site"
MAX_EDGE=2560
CACHE_CONTROL="public, max-age=604800"
# 處理配方：任何會改變輸出像素的參數都要進來，這樣改參數後舊物件會被判定為「要重傳」
RECIPE="v1-q${QUALITY}-m6-l${MAX_EDGE}"
TAB=$(printf '\t')

DRY_RUN=0 ALL=0 FORCE=0

step() { printf '\n== %s ==\n' "$1"; }
info() { printf '   %s\n' "$1"; }
warn() { printf '   警告：%s\n' "$1" >&2; }
die() { printf '\n錯誤：%s\n' "$1" >&2; exit 1; }

while [ $# -gt 0 ]; do
  case "$1" in
    --dry-run) DRY_RUN=1 ;;
    --all) ALL=1 ;;
    --force) FORCE=1 ;;
    --list)
      [ $# -ge 2 ] || die "--list 需要一個檔案路徑"
      LIST_FILE=$2
      shift
      ;;
    -h | --help)
      sed -n '2,/^set -euo/p' "$0" | sed '$d' | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) die "不認得的選項：${1}（--help 看用法）" ;;
  esac
  shift
done

case "${AUTH_MODE}" in login | key) ;; *) die "AUTH_MODE 只能是 login 或 key" ;; esac
case "${QUALITY}" in '' | *[!0-9]*) die "QUALITY 必須是整數" ;; esac
{ [ "${QUALITY}" -ge 1 ] && [ "${QUALITY}" -le 100 ]; } || die "QUALITY 必須在 1 到 100"
case "${JOBS}" in '' | *[!0-9]*) die "JOBS 必須是整數" ;; esac
[ "${JOBS}" -ge 1 ] || die "JOBS 必須 >= 1"

# ── 前置檢查 ─────────────────────────────────────────────────────────────
step "前置檢查"
command -v az >/dev/null || die "找不到 az（安裝 Azure CLI 並 az login）"
az account show >/dev/null 2>&1 || die "az 尚未登入（先執行 az login）"
if [ "${DRY_RUN}" = 0 ]; then
  command -v docker >/dev/null || die "找不到 docker（處理照片用容器化的 ImageMagick，不需另外安裝工具）"
  docker info >/dev/null 2>&1 || die "docker 沒有在執行（請先開啟 Docker Desktop）"
fi
[ -d "${SRC_DIR}" ] || die "找不到來源資料夾 ${SRC_DIR}（這個資料夾不納版控，要在有照片的那台機器執行）"

sha256() { # $1=檔案
  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{ print $1 }'
  elif command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{ print $1 }'
  else
    die "找不到 shasum 或 sha256sum"
  fi
}

BASE_TMP="${TMPDIR:-/tmp}"
BASE_TMP=${BASE_TMP%/}
TMP=$(mktemp -d "${BASE_TMP}/tcrfc-site-images.XXXXXX") || die "建立暫存目錄失敗"
case "${TMP}" in
  "${BASE_TMP}"/tcrfc-site-images.*) ;;
  *) die "暫存目錄路徑異常：${TMP}" ;;
esac
cleanup() { rm -rf "${TMP}"; }
trap cleanup EXIT

# ── 決定來源清單 ──────────────────────────────────────────────────────────
step "決定要處理的照片"
CAND="${TMP}/candidates.txt"
: >"${CAND}"
if [ "${ALL}" = 0 ] && [ -f "${LIST_FILE}" ]; then
  info "依清單：${LIST_FILE}"
  # 去註解與空行、去前後空白、去開頭的 ./ ／ public/ ／ assets/img/，不論清單寫的是來源路徑或網址路徑
  sed -e 's/#.*$//' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' "${LIST_FILE}" \
    | sed -e '/^$/d' -e 's#^\./##' -e 's#^/##' -e 's#^public/##' -e 's#^assets/img/##' \
    | sort -u >"${CAND}"
else
  if [ "${ALL}" = 1 ]; then
    info "指定 --all：處理整個資料夾 ${SRC_DIR}"
  else
    warn "找不到清單 ${LIST_FILE}，退回處理「整個資料夾」——會把前台沒用到的照片也傳上去（含未成年學員素材）。"
    warn "前台產出 site-images.txt 後再跑一次較精確（本腳本不刪雲端物件，多傳的要手動刪）。"
  fi
  (cd "${SRC_DIR}" && find . -type f ! -name '.DS_Store' | sed 's#^\./##' | sort -u) >"${CAND}"
fi
[ -s "${CAND}" ] || die "沒有任何候選照片"

# 解析成實際存在的來源檔：REL（來源相對路徑）與 KEY（雲端物件鍵）
SRC_LIST="${TMP}/sources.txt"
: >"${SRC_LIST}"
MISSING=0
SKIPPED_SVG=0
SKIPPED_OTHER=0
while IFS= read -r entry; do
  [ -n "${entry}" ] || continue
  case "${entry}" in
    *.svg | *.SVG)
      SKIPPED_SVG=$((SKIPPED_SVG + 1))
      continue
      ;;
  esac
  rel=""
  if [ -f "${SRC_DIR}/${entry}" ]; then
    rel=${entry}
  else
    # 清單寫的是目標鍵（.webp）而來源是 .jpg／.png 的情況
    stem=${entry%.*}
    for ext in jpg jpeg JPG JPEG png PNG webp; do
      if [ -f "${SRC_DIR}/${stem}.${ext}" ]; then
        rel="${stem}.${ext}"
        break
      fi
    done
  fi
  if [ -z "${rel}" ]; then
    warn "清單列了但來源不存在：${entry}"
    MISSING=$((MISSING + 1))
    continue
  fi
  case "${rel}" in
    *[!A-Za-z0-9._/-]*) die "檔名含不支援的字元（只接受英數與 . _ - /）：${rel}" ;;
    *..*) die "路徑含 ..：${rel}" ;;
  esac
  case "${rel}" in
    *.jpg | *.jpeg | *.png | *.webp | *.JPG | *.JPEG | *.PNG) ;;
    *)
      warn "略過不支援的格式：${rel}"
      SKIPPED_OTHER=$((SKIPPED_OTHER + 1))
      continue
      ;;
  esac
  printf '%s\t%s/%s.webp\n' "${rel}" "${KEY_PREFIX}" "${rel%.*}" >>"${SRC_LIST}"
done <"${CAND}"
sort -u "${SRC_LIST}" -o "${SRC_LIST}"

# 兩個來源對到同一個物件鍵（a.jpg 與 a.png）會互相覆蓋，直接中止
DUP=$(cut -f2 "${SRC_LIST}" | sort | uniq -d | head -3)
[ -z "${DUP}" ] || die "多個來源檔對到同一個物件鍵（副檔名不同、主檔名相同）：${DUP}"
TOTAL=$(awk 'END { print NR + 0 }' "${SRC_LIST}")
[ "${TOTAL}" -gt 0 ] || die "沒有可處理的照片"
info "來源 ${TOTAL} 張（略過 .svg ${SKIPPED_SVG} 個、不支援格式 ${SKIPPED_OTHER} 個、清單有但檔案不存在 ${MISSING} 個）"

# ── 查 Azure（唯讀）──────────────────────────────────────────────────────
step "查詢儲存體帳戶（唯讀）"
if [ -z "${STORAGE_ACCOUNT}" ]; then
  names=$(az storage account list -g "${RG}" --query "[?starts_with(name, 'sttcrfcclub')].name" -o tsv) \
    || die "az 查詢儲存體帳戶失敗（資源群組 ${RG}）"
  count=$(printf '%s\n' "${names}" | awk 'NF { n++ } END { print n + 0 }')
  [ "${count}" = "1" ] || die "俱樂部儲存體帳戶應該恰好 1 個，實際 ${count} 個（可用 STORAGE_ACCOUNT=<名稱> 指定）"
  STORAGE_ACCOUNT=${names}
fi
BLOB_ENDPOINT=$(az storage account show -g "${RG}" -n "${STORAGE_ACCOUNT}" --query primaryEndpoints.blob -o tsv) \
  || die "查不到帳戶 ${STORAGE_ACCOUNT} 的 blob 端點"
case "${BLOB_ENDPOINT}" in
  https://*) ;;
  *) die "blob 端點格式異常" ;;
esac
BLOB_ENDPOINT=${BLOB_ENDPOINT%/}
MEDIA_BASE_URL="${BLOB_ENDPOINT}/${CONTAINER}"
info "帳戶：${STORAGE_ACCOUNT}，容器：${CONTAINER}，認證：${AUTH_MODE}"

REMOTE="${TMP}/remote.tsv" # 物件鍵<TAB>srchash
: >"${REMOTE}"
role_hint() {
  local acct_id
  acct_id=$(az storage account show -g "${RG}" -n "${STORAGE_ACCOUNT}" --query id -o tsv 2>/dev/null || true)
  echo "   若是權限不足（AuthorizationPermissionMismatch），請你自己執行下面這行授權（角色生效約需 1 到 5 分鐘），再重跑本腳本："
  echo "     az role assignment create --assignee \"\$(az ad signed-in-user show --query id -o tsv)\" \\"
  echo "       --role 'Storage Blob Data Contributor' --scope '${acct_id:-<儲存體帳戶的資源 ID>}'"
  echo "   或改用帳戶金鑰（需 listKeys 權限）：AUTH_MODE=key bash infra/upload-site-images.sh"
}
if az storage blob list --account-name "${STORAGE_ACCOUNT}" --container-name "${CONTAINER}" \
  --prefix "${KEY_PREFIX}/" --include m --num-results '*' --auth-mode "${AUTH_MODE}" \
  --query "[].[name, metadata.srchash]" -o tsv >"${REMOTE}" 2>"${TMP}/az.err"; then
  info "雲端 ${KEY_PREFIX}/ 目前有 $(awk 'END { print NR + 0 }' "${REMOTE}") 個物件"
else
  sed 's/^/   az：/' "${TMP}/az.err" >&2
  : >"${REMOTE}"
  if [ "${DRY_RUN}" = 1 ]; then
    warn "讀不到雲端現況，dry-run 把全部視為「待上傳」。"
    role_hint
  else
    role_hint
    die "無法讀取容器 ${CONTAINER}，上傳前中止（沒有任何變更）"
  fi
fi

remote_hash() { awk -F'\t' -v k="$1" '$1 == k { print $2; exit }' "${REMOTE}"; }

# ── 決定哪些要傳 ──────────────────────────────────────────────────────────
step "比對"
PLAN="${TMP}/plan.tsv" # REL KEY HASH 狀態
: >"${PLAN}"
N_UP=0
N_SKIP=0
while IFS="${TAB}" read -r rel key; do
  sha=$(sha256 "${SRC_DIR}/${rel}")
  want="${RECIPE}:${sha}"
  have=$(remote_hash "${key}")
  if [ "${FORCE}" = 0 ] && [ "${have}" = "${want}" ]; then
    printf '%s\t%s\t%s\tskip\n' "${rel}" "${key}" "${want}" >>"${PLAN}"
    N_SKIP=$((N_SKIP + 1))
  else
    printf '%s\t%s\t%s\tupload\n' "${rel}" "${key}" "${want}" >>"${PLAN}"
    N_UP=$((N_UP + 1))
  fi
done <"${SRC_LIST}"
info "待上傳 ${N_UP}、已是最新而略過 ${N_SKIP}"

if [ "${DRY_RUN}" = 1 ]; then
  step "dry-run：以下是會上傳的檔案（沒有處理、沒有上傳）"
  awk -F'\t' '$4 == "upload" { printf "   %s  ->  %s\n", $1, $2 }' "${PLAN}"
  echo
  info "前台設定 NUXT_PUBLIC_MEDIA_BASE_URL（VM 的 MEDIA_BASE_URL）＝ ${MEDIA_BASE_URL}"
  if [ "${MISSING}" != 0 ]; then warn "有 ${MISSING} 個清單項目找不到來源檔（見上面警告）"; fi
  exit 0
fi

if [ "${N_UP}" = 0 ]; then
  step "完成"
  info "雲端已是最新，沒有要上傳的檔案。"
  info "前台設定 NUXT_PUBLIC_MEDIA_BASE_URL（VM 的 MEDIA_BASE_URL）＝ ${MEDIA_BASE_URL}"
  [ "${MISSING}" = 0 ] || die "有 ${MISSING} 個清單項目找不到來源檔（見上面警告）"
  exit 0
fi

# ── 容器內處理 ───────────────────────────────────────────────────────────
step "處理照片（容器 ${IM_IMAGE}；來源唯讀掛載）"
WORK="${TMP}/work"
mkdir -p "${WORK}/out"
awk -F'\t' '$4 == "upload" { print $1 }' "${PLAN}" >"${WORK}/todo.txt"

# 每張照片的處理腳本（在容器內以 sh 執行）。auto-orient 必須在 strip 之前，否則方向資訊先被丟掉。
cat >"${WORK}/one.sh" <<'ONE'
#!/bin/sh
rel=${1}
out="/work/out/${rel%.*}.webp"
mkdir -p "$(dirname "${out}")"
icc=$(magick identify -format '%[icc:description]' "/src/${rel}[0]" 2>/dev/null | head -1)
case "${icc}" in
  "" | *sRGB* | *srgb*) ;;
  *) echo "WARN ${rel}：內嵌色彩設定檔不是 sRGB（${icc}），去除後顏色可能偏移" ;;
esac
magick "/src/${rel}[0]" -auto-orient -resize "${MAX_EDGE}x${MAX_EDGE}>" -strip \
  -define webp:method=6 -define webp:alpha-quality=100 -quality "${QUALITY}" "${out}" \
  || { echo "FAIL ${rel}：轉檔失敗"; exit 1; }
# 驗證：格式、長邊、無任何中繼資料
meta=$(magick identify -format '%m %[fx:max(w,h)]' "${out}")
fmt=${meta%% *}
edge=${meta##* }
[ "${fmt}" = "WEBP" ] || { echo "FAIL ${rel}：輸出格式 ${fmt}"; exit 1; }
[ "${edge}" -le "${MAX_EDGE}" ] || { echo "FAIL ${rel}：長邊 ${edge} 超過 ${MAX_EDGE}"; exit 1; }
if magick identify -verbose "${out}" | grep -Eiq '^ *(Profile-|exif:|xmp|iptc)'; then
  echo "FAIL ${rel}：輸出仍含中繼資料"; exit 1
fi
exit 0
ONE

UIDGID="$(id -u):$(id -g)"
if ! docker run --rm --user "${UIDGID}" \
  -v "${SRC_DIR}:/src:ro" -v "${WORK}:/work" \
  -e "MAX_EDGE=${MAX_EDGE}" -e "QUALITY=${QUALITY}" \
  --entrypoint sh "${IM_IMAGE}" \
  -c "xargs -d '\\n' -P '${JOBS}' -n 1 sh /work/one.sh < /work/todo.txt" >"${TMP}/process.log" 2>&1; then
  sed 's/^/   /' "${TMP}/process.log" >&2
  die "處理失敗（容器內有照片轉檔或驗證不過；來源檔未被動到）。尚未上傳任何東西"
fi
{ grep '^WARN' "${TMP}/process.log" || true; } | sed 's/^WARN /   警告：/' >&2
DONE_N=$(find "${WORK}/out" -type f -name '*.webp' | awk 'END { print NR + 0 }')
[ "${DONE_N}" = "${N_UP}" ] || die "處理後的檔數（${DONE_N}）與預期（${N_UP}）不符，尚未上傳"
BYTES_IN=0
BYTES_OUT=0
while IFS="${TAB}" read -r rel key hash state; do
  [ "${state}" = "upload" ] || continue
  out="${WORK}/out/${rel%.*}.webp"
  [ -s "${out}" ] || die "輸出檔不存在或為空：${out}"
  BYTES_IN=$((BYTES_IN + $(wc -c <"${SRC_DIR}/${rel}")))
  BYTES_OUT=$((BYTES_OUT + $(wc -c <"${out}")))
done <"${PLAN}"
info "處理 ${DONE_N} 張，全數通過驗證（WebP、長邊 <= ${MAX_EDGE}、無中繼資料）。$((BYTES_IN / 1024 / 1024)) MB -> $((BYTES_OUT / 1024 / 1024)) MB"

# ── 上傳 ─────────────────────────────────────────────────────────────────
step "上傳到 ${STORAGE_ACCOUNT}/${CONTAINER}"
FAILED=0
IDX=0
while IFS="${TAB}" read -r rel key hash state; do
  [ "${state}" = "upload" ] || continue
  IDX=$((IDX + 1))
  out="${WORK}/out/${rel%.*}.webp"
  if az storage blob upload --account-name "${STORAGE_ACCOUNT}" --container-name "${CONTAINER}" \
    --name "${key}" --file "${out}" --overwrite true \
    --content-type image/webp --content-cache-control "${CACHE_CONTROL}" \
    --metadata "srchash=${hash}" \
    --auth-mode "${AUTH_MODE}" --only-show-errors -o none 2>"${TMP}/up.err"; then
    info "[${IDX}/${N_UP}] ${key}"
  else
    FAILED=$((FAILED + 1))
    warn "[${IDX}/${N_UP}] 上傳失敗：${key}"
    sed 's/^/      /' "${TMP}/up.err" >&2
  fi
done <"${PLAN}"

if [ "${FAILED}" != 0 ]; then
  role_hint
  die "${FAILED} 個檔案上傳失敗。已成功的不會重傳，修正後直接重跑本腳本即可"
fi

# ── 驗證公開讀取 ──────────────────────────────────────────────────────────
step "驗證公開讀取（匿名 GET 第一個上傳的物件）"
FIRST_KEY=$(awk -F'\t' '$4 == "upload" { print $2; exit }' "${PLAN}")
TEST_URL="${BLOB_ENDPOINT}/${CONTAINER}/${FIRST_KEY}"
if command -v curl >/dev/null 2>&1; then
  res=$(curl -s -o /dev/null -w '%{http_code} %{content_type}' --max-time 20 "${TEST_URL}") || res="curl 失敗（連不上或 DNS 解析不到）"
  info "${TEST_URL} -> ${res}"
  case "${res}" in
    "200 image/webp"*) ;;
    *) warn "預期 200 image/webp。容器 ${CONTAINER} 是否為匿名 blob 讀取、帳戶是否允許公開網路（infra/README.md §2）？" ;;
  esac
else
  info "沒有 curl，請自行開啟 ${TEST_URL} 確認"
fi

step "完成"
info "上傳 ${N_UP} 張、略過 ${N_SKIP} 張。暫存目錄會在結束時清除。"
info "前台設定 NUXT_PUBLIC_MEDIA_BASE_URL（VM 的 /opt/tcrfc/.env 的 MEDIA_BASE_URL）＝ ${MEDIA_BASE_URL}"
info "下一步：infra/README.md §4.9——重跑 provision-secrets.sh 或手動寫入 MEDIA_BASE_URL，再部署前台。"
[ "${MISSING}" = 0 ] || die "有 ${MISSING} 個清單項目找不到來源檔（見上面警告），該些照片沒有上傳"
