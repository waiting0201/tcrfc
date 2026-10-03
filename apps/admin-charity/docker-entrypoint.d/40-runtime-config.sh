#!/bin/sh
# 容器啟動時依環境變數產生 /config.js（SPA 的執行期設定）。
# 由 nginxinc/nginx-unprivileged 的 /docker-entrypoint.sh 在啟動 nginx 之前自動執行。
#
# 為什麼存在：SPA 是純靜態檔，API 網址若在建置期烤進去，換網域（docs/17 §10：4webdemo.com → tcrfc.tw）
# 就得重建映像檔；而過去「執行期注入」只寫在註解裡、從未實作，正式映像檔因此退回寫死的
# 127.0.0.1:5299（docs/18 E-112 升級段第三次）。
#
# 輸出到 /tmp（非 root 的 nginx 使用者一定可寫），nginx 以 `location = /config.js` 的 alias 提供。
set -eu

OUT=/tmp/config.js
BASE="${ADMIN_API_BASE_URL:-}"
BASE="${BASE%/}"

case "$BASE" in
  "")
    echo "[runtime-config] 警告：ADMIN_API_BASE_URL 未設定，後台畫面會顯示「未設定 API 位址」。" >&2
    ;;
  http://*|https://*)
    ;;
  *)
    echo "[runtime-config] 錯誤：ADMIN_API_BASE_URL 必須以 http:// 或 https:// 開頭，目前是「$BASE」。" >&2
    exit 1
    ;;
esac

# 只允許網址常見字元：值會被放進 JS 字串，不接受引號、反斜線、空白、角括號等（避免注入）。
case "$BASE" in
  *[!A-Za-z0-9:/._~%@-]*)
    echo "[runtime-config] 錯誤：ADMIN_API_BASE_URL 含有不允許的字元。" >&2
    exit 1
    ;;
esac

printf 'window.__TCRFC_CONFIG__ = { apiBaseUrl: "%s" };\n' "$BASE" > "$OUT"
echo "[runtime-config] 已產生 $OUT（apiBaseUrl=${BASE:-<未設定>}）"
