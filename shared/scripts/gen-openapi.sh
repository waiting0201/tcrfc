#!/usr/bin/env bash
# shared/scripts/gen-openapi.sh — 由 apps/api 在「建置期」產出 OpenAPI 3.1，濾成 App 可呼叫的範圍後寫入 shared/openapi.json。
#
# 為什麼是建置期：Microsoft.Extensions.ApiDescription.Server 以記憶體內的 host 讀端點中繼資料，
# 不開 Kestrel、不開任何連線，所以 CI 不需要資料庫、Redis、Blob。連線字串與簽章金鑰只是讓 Program.cs
# 的啟動檢查通過的「假值」，不是任何環境的憑證（也連不上：埠號 1）。
# 正式環境不公開 OpenAPI／swagger：Program.cs 只在 Development 掛 MapOpenApi，本腳本不改動這一點。
#
# 用法：shared/scripts/gen-openapi.sh   （在 repo 任一處執行皆可）
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# 清掉開發機上可能存在、會改變端點集合或啟用其他模組的設定，讓輸出與機器無關。
# （dotnet user-secrets 在 Development 會被讀進來；環境變數優先於 user-secrets，所以用空值蓋掉）
env \
  ASPNETCORE_ENVIRONMENT=Development \
  CLUB_SQL_CONNECTION_STRING='Server=127.0.0.1,1;Database=openapi_gen;User Id=sa;Password=not-a-real-password;TrustServerCertificate=True;' \
  JWT_SIGNING_KEY_CLUB='openapi-gen-only-not-a-secret-0123456789abcdef' \
  CHARITY_SQL_CONNECTION_STRING= \
  REDIS_HOST= \
  AZURE_BLOB_CONNECTION_STRING= \
  PAYMENT_GATEWAY= \
  INVOICE_ISSUER= \
  dotnet build "$root/apps/api/Tcrfc.Api.csproj" -c Release --nologo -v:q --no-incremental \
    -p:OpenApiGenerateDocuments=true \
    -p:OpenApiDocumentsDirectory="$tmp"

raw="$tmp/Tcrfc.Api.json"
[ -f "$raw" ] || { echo "找不到建置期產出的 OpenAPI 文件：$raw" >&2; exit 1; }
node "$root/shared/scripts/filter-openapi.mjs" "$raw" "$root/shared/openapi.json"
