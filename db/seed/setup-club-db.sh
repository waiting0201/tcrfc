#!/usr/bin/env bash
# db/seed/setup-club-db.sh — 一鍵建立／重建本機網站庫 tcrfc_club（DDL ＋ 種子）
#
# 2026-09-30 使用者裁決：本機只保留兩個資料庫——網站 tcrfc_club、慈善 tcrfc_charity。
# 舊的 tcrfc_club_dev／tcrfc_club_test 合併為 tcrfc_club：本機開發、無頭瀏覽器實走與
# `dotnet test` 全部共用它（代價與處理見 docs/14-invariants.md「S0-13」、deploy/README.md）。
# 本檔前身是 setup-test-db.sh（建 tcrfc_club_test）。不保留舊名轉呼叫：舊名的語意
# 是「測試專用庫」，留著只會讓人以為還有獨立測試庫；所有引用已一併改掉。
#
# 這支腳本只是把既有兩支腳本串起來，沒有另外寫一份建庫或灌種子的邏輯：
#   1. deploy/local-ddl.sh --apply-club-db [--recreate]  建立／（可選）重建 tcrfc_club，
#      灌入轉換過 json→nvarchar(max) 的 club-schema
#   2. db/seed/apply-seed.sh  灌種子資料（含 ADMIN_USERS 測試帳號）
#
# 用法：
#   ./db/seed/setup-club-db.sh              建立（若不存在）＋灌 DDL（若是全新庫）＋灌種子。
#                                            已存在且已有資料表時略過 DDL，只重新跑一次種子
#                                            （種子腳本本身冪等，可安心重複執行）。
#   ./db/seed/setup-club-db.sh --recreate   從零重來：先 DROP（若存在）再 CREATE，再灌 DDL＋種子。
#                                            測試中途失敗留下殘骸、或資料被弄髒時用這個。
#   ./db/seed/setup-club-db.sh --dry-run    只產生種子 .sql，不建庫也不套用。
#
# 需要環境變數 MSSQL_DEV_SA_PASSWORD（例如 set -a; source .env; set +a）。
#
# ⛔ 安全模型：目標容器可用 LOCAL_MSSQL_CONTAINER 指定（預設 "sqlserver"），目標資料庫
# 名稱寫死只允許 tcrfc_club 一個；--recreate 的 DROP 只會作用在這個名字，不會、也沒有任何
# 程式碼路徑可以碰到該 instance 上其他資料庫（含 tcrfc_charity 與使用者其他專案的資料庫）。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && cd .. && pwd)"

readonly TARGET_DATABASE="tcrfc_club"

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
  "${SCRIPT_DIR}/apply-seed.sh" --dry-run
  exit 0
fi

if [[ "${RECREATE}" -eq 1 ]]; then
  echo "==> 步驟 1／2：建立／灌 DDL（deploy/local-ddl.sh --apply-club-db --recreate）"
  "${REPO_ROOT}/deploy/local-ddl.sh" --apply-club-db --recreate
else
  echo "==> 步驟 1／2：建立／灌 DDL（deploy/local-ddl.sh --apply-club-db）"
  "${REPO_ROOT}/deploy/local-ddl.sh" --apply-club-db
fi

echo
echo "==> 步驟 2／2：灌種子資料（db/seed/apply-seed.sh，目標 ${TARGET_DATABASE}）"
"${SCRIPT_DIR}/apply-seed.sh"

echo
echo "完成——${TARGET_DATABASE} 已就緒，CLUB_SQL_CONNECTION_STRING 的 Database 應為 ${TARGET_DATABASE}。"
echo "例如："
echo "  export CLUB_SQL_CONNECTION_STRING=\"Server=127.0.0.1,1433;Database=${TARGET_DATABASE};User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;\""
