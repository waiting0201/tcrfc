#!/usr/bin/env bash
# shared/scripts/gen-all.sh — 重新產生 shared/ 所有「產生檔」，最後跑一致性檢查。
#
# 產生檔（不要手改，改來源再重跑本腳本）：
#   shared/openapi.json                      <- apps/api（建置期產生，不連資料庫）
#   shared/generated/swift/*.swift           <- shared/openapi.json
#   shared/generated/kotlin/*.kt             <- shared/openapi.json
#   shared/error-codes.json                  <- apps/api 原始碼掃描
#   shared/image-derivatives.json            <- apps/api/Images（圖片衍生檔網址命名規則）
#   shared/enums.json                        <- db/club-schema.sql 的 CHECK 約束＋Common/EnrollmentStatus.cs（封閉值域）
#   shared/news-body-blocks.json             <- apps/web/app/utils/news-body.ts（新聞內文 bodyJson 區塊型別與別名）
# 手寫檔（只檢查，不產生）：cache-schema.sql、cache-policy.json、deeplinks.json、ad-viewability.md、ad-viewability-cases.json
#
# CI（.github/workflows/ci.yml 的 shared-contract job）跑完本腳本後做 `git diff --exit-code`：
# 後端改了 API 而沒有重新產生並提交 shared/，PR 就會紅燈。需要：.NET SDK 10、Node（.node-version）、Python 3。
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$here/gen-openapi.sh"
node "$here/gen-dto.mjs"
node "$here/gen-error-codes.mjs"
node "$here/gen-image-derivatives.mjs"
node "$here/gen-enums.mjs"
node "$here/gen-news-body-blocks.mjs"
python3 "$here/check-shared.py"
