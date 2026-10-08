#!/bin/sh
# 容器啟動時依環境變數產生 /config.js（SPA 的執行期設定）。
# 由 nginxinc/nginx-unprivileged 的 /docker-entrypoint.sh 在啟動 nginx 之前自動執行。
#
# 為什麼存在：SPA 是純靜態檔，API 網址若在建置期烤進去，換網域（docs/17 §10：4webdemo.com → tcrfc.tw）
# 就得重建映像檔；而過去「執行期注入」只寫在註解裡、從未實作，正式映像檔因此退回寫死的
# 127.0.0.1:5299（docs/18 E-112 升級段第三次）。
#
# 三個網址：
#   ADMIN_API_BASE_URL     API（必填，未設定時畫面顯示「未設定 API 位址」）
#   ADMIN_WEB_BASE_URL     主站前台（「這裡管理的是：… ↗」與預覽前台；未設定時不放連結）
#   ADMIN_BW_WEB_BASE_URL  藍鯨官網前台（同上，後台切到藍鯨時使用）
#
# 輸出到 /tmp（非 root 的 nginx 使用者一定可寫），nginx 以 `location = /config.js` 的 alias 提供。
set -eu

OUT=/tmp/config.js

# 驗證並回傳去掉結尾斜線的網址；$1＝變數名稱
check_url() {
  name="$1"
  eval "val=\"\${$name:-}\""
  val="${val%/}"
  case "$val" in
    "")
      echo "[runtime-config] 警告：$name 未設定。" >&2
      ;;
    http://*|https://*)
      ;;
    *)
      echo "[runtime-config] 錯誤：$name 必須以 http:// 或 https:// 開頭，目前是「${val}」。" >&2
      exit 1
      ;;
  esac
  # 只允許網址常見字元：值會被放進 JS 字串，不接受引號、反斜線、空白、角括號等（避免注入）。
  case "$val" in
    *[!A-Za-z0-9:/._~%@-]*)
      echo "[runtime-config] 錯誤：$name 含有不允許的字元。" >&2
      exit 1
      ;;
  esac
  printf '%s' "$val"
}

API="$(check_url ADMIN_API_BASE_URL)"
WEB="$(check_url ADMIN_WEB_BASE_URL)"
BW="$(check_url ADMIN_BW_WEB_BASE_URL)"

printf 'window.__TCRFC_CONFIG__ = { apiBaseUrl: "%s", webBaseUrl: "%s", bwWebBaseUrl: "%s" };\n' "$API" "$WEB" "$BW" > "$OUT"
echo "[runtime-config] 已產生 ${OUT}（apiBaseUrl=${API:-<未設定>}，webBaseUrl=${WEB:-<未設定>}，bwWebBaseUrl=${BW:-<未設定>}）"
