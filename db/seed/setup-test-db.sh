#!/usr/bin/env bash
# db/seed/setup-test-db.sh — 一鍵建立／灌整合測試專用庫 tcrfc_club_test（S0-13，2026-09-25）
#
# 為什麼需要這支腳本：
#   `dotnet test` 與無頭瀏覽器實走原本共用 tcrfc_club_dev，同一天發生三次互相干擾——測試把
#   實走中帳號的 2FA 狀態、settings 的 SEO 值重置回種子（見 docs/14-invariants.md、
#   docs/18-work-errors.md、STATUS.md S0-13）。解法是讓整合測試改連一個完全獨立的資料庫，
#   本機開發與無頭瀏覽器實走繼續用 tcrfc_club_dev 不受影響。
#
# 這支腳本只是把既有兩支腳本（deploy/local-ddl.sh、db/seed/apply-seed.sh）串起來，
# 沒有另外寫一份建庫或灌種子的邏輯：
#   1. deploy/local-ddl.sh --apply-test-db [--recreate]  建立／（可選）重建 tcrfc_club_test，
#      灌入轉換過 json→nvarchar(max) 的 club-schema
#   2. db/seed/apply-seed.sh（SEED_TARGET_DATABASE=tcrfc_club_test）  灌種子資料
#      （含 ADMIN_USERS 測試帳號，跟 tcrfc_club_dev 用同一份 generate-club-seed-sql.py，
#      不必為測試庫另外維護一份種子資料定義）
#
# 用法：
#   ./db/seed/setup-test-db.sh              建立（若不存在）＋灌 DDL（若是全新庫）＋灌種子。
#                                            已存在且已有資料表時略過 DDL，只重新跑一次種子
#                                            （種子腳本本身冪等，可安心重複執行）。
#   ./db/seed/setup-test-db.sh --recreate   從零重來：先 DROP（若存在）再 CREATE，取得全新的
#                                            資料庫，再灌 DDL＋種子。「從零建立測試庫」用這個，
#                                            驗收／懷疑資料庫被測試弄髒時也用這個。
#   ./db/seed/setup-test-db.sh --dry-run    只跑到「產生種子 .sql」這一步，不建庫也不套用
#                                            （沿用 apply-seed.sh 既有的 --dry-run 語意）。
#
# ⛔ 安全模型與 deploy/local-ddl.sh／db/seed/apply-seed.sh 逐字比照：目標容器可用
# LOCAL_MSSQL_CONTAINER 環境變數指定（預設 "sqlserver"），目標資料庫名稱寫死只允許
# tcrfc_club_test 一個——本腳本從頭到尾唯一會建立／寫入的資料庫就是這一個，不會、也沒有任何
# 程式碼路徑可以碰到這個 SQL Server instance 上其他任何資料庫（含 tcrfc_club_dev／
# tcrfc_charity_dev／使用者其他專案的既有資料庫）。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && cd .. && pwd)"

readonly TARGET_DATABASE="tcrfc_club_test"

RECREATE=0
DRY_RUN=0
for arg in "$@"; do
  case "${arg}" in
    --recreate)
      RECREATE=1
      ;;
    --dry-run)
      DRY_RUN=1
      ;;
    *)
      echo "不認得的參數：${arg}（合法值：--recreate、--dry-run）" >&2
      exit 1
      ;;
  esac
done

if [[ "${DRY_RUN}" -eq 1 ]]; then
  echo "==> --dry-run：只產生種子 SQL，不建庫也不套用（見 apply-seed.sh --dry-run）"
  SEED_TARGET_DATABASE="${TARGET_DATABASE}" "${SCRIPT_DIR}/apply-seed.sh" --dry-run
  exit 0
fi

if [[ "${RECREATE}" -eq 1 ]]; then
  echo "==> 步驟 1／2：建立／灌 DDL（deploy/local-ddl.sh --apply-test-db --recreate）"
  "${REPO_ROOT}/deploy/local-ddl.sh" --apply-test-db --recreate
else
  echo "==> 步驟 1／2：建立／灌 DDL（deploy/local-ddl.sh --apply-test-db）"
  "${REPO_ROOT}/deploy/local-ddl.sh" --apply-test-db
fi

echo
echo "==> 步驟 2／2：灌種子資料（db/seed/apply-seed.sh，目標 ${TARGET_DATABASE}）"
SEED_TARGET_DATABASE="${TARGET_DATABASE}" "${SCRIPT_DIR}/apply-seed.sh"

echo
echo "完成——${TARGET_DATABASE} 已就緒，可以把 dotnet test 用的 CLUB_SQL_CONNECTION_STRING 指向它了。"
echo "例如："
echo "  export CLUB_SQL_CONNECTION_STRING=\"Server=127.0.0.1,1433;Database=${TARGET_DATABASE};User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;\""
