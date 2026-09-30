#!/usr/bin/env bash
# apps/api/scripts/init-local-settings.sh
#
# 產生 apps/api/appsettings.Development.json（本機設定，已被 .gitignore 排除、也被 .dockerignore 排除）。
# ASP.NET Core 在 Development 環境自動讀取它，之後在 apps/api 直接 `dotnet run`／`dotnet test`
# 就不必每次 export 環境變數；環境變數仍優先於檔案（正式環境與 Docker 行為不變）。
#
# 來源：
#   - 根目錄 .env 的 MSSQL_DEV_SA_PASSWORD（本機 SQL Server 容器的 SA 密碼）
#   - deploy/dev/club.env 的 JWT_SIGNING_KEY_CLUB
# 已存在的檔案不覆寫，除非加 --force。本腳本不會把密碼或金鑰印到終端機。
set -euo pipefail

FORCE=0
[[ "${1:-}" == "--force" ]] && FORCE=1

API_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT_DIR="$(cd "$API_DIR/../.." && pwd)"
TARGET="$API_DIR/appsettings.Development.json"

if [[ -e "$TARGET" && $FORCE -eq 0 ]]; then
  echo "已存在：${TARGET}（不覆寫；要重新產生請加 --force）"
  exit 0
fi

# 只取值、不 source（連線字串與金鑰可能含特殊字元）。取第一個 = 之後的全部內容，去掉成對引號。
read_kv() {
  local file="$1" key="$2" line val
  [[ -f "$file" ]] || { echo "找不到 $file" >&2; return 1; }
  line="$(grep -m1 "^${key}=" "$file" || true)"
  [[ -n "$line" ]] || { echo "$file 沒有 ${key}" >&2; return 1; }
  val="${line#*=}"
  val="${val%\"}"; val="${val#\"}"; val="${val%\'}"; val="${val#\'}"
  printf '%s' "$val"
}

SA_PASSWORD="$(read_kv "$ROOT_DIR/.env" MSSQL_DEV_SA_PASSWORD)"
JWT_KEY="$(read_kv "$ROOT_DIR/deploy/dev/club.env" JWT_SIGNING_KEY_CLUB)"
if [[ ${#JWT_KEY} -lt 32 ]]; then
  echo "JWT_SIGNING_KEY_CLUB 短於 32 字元，API 啟動會被拒絕" >&2
  exit 1
fi

CONN="Server=127.0.0.1,1433;Database=tcrfc_club;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False;"
DP_PATH="$HOME/.local/share/tcrfc-dev/dataprotection-keys"
mkdir -p "$DP_PATH"

# 用 python 的 json 序列化，確保特殊字元被正確跳脫；值經環境變數傳入，不會出現在命令列參數。
umask 077
CONN="$CONN" JWT_KEY="$JWT_KEY" DP_PATH="$DP_PATH" TARGET="$TARGET" python3 - <<'PY'
import json, os
doc = {
    "Logging": {"LogLevel": {"Default": "Information", "Microsoft.AspNetCore": "Warning"}},
    "CLUB_SQL_CONNECTION_STRING": os.environ["CONN"],
    "JWT_SIGNING_KEY_CLUB": os.environ["JWT_KEY"],
    "CORS_ALLOWED_ORIGINS": "http://localhost:3000,http://localhost:3001,http://localhost:3002,http://localhost:5174",
    "DATA_PROTECTION_KEYS_PATH": os.environ["DP_PATH"],
}
with open(os.environ["TARGET"], "w", encoding="utf-8") as f:
    json.dump(doc, f, ensure_ascii=False, indent=2)
    f.write("\n")
PY
echo "已產生：${TARGET}（權限 600，已被 .gitignore 排除）"
