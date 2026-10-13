#!/usr/bin/env bash
# db/seed/setup-charity-db.sh — 一鍵建立／重建本機慈善庫 tcrfc_charity（DDL ＋ migration 歷史 ＋ 種子）
#
# 與 db/seed/setup-club-db.sh 對應（2026-10-13，E-322）：只是把既有兩支腳本串起來，沒有另寫建庫或灌種子的邏輯：
#   1. deploy/local-ddl.sh --apply-charity-db [--recreate]   建立／（可選）重建 tcrfc_charity，灌原樣 db/charity-schema.sql，
#      並把全部 migration 標記為已套用（__EFMigrationsHistory，與 deploy/prod-db-init.sh 步驟 3 相同）
#   2. db/seed/apply-charity-seed.sh                          灌種子資料
#
# 用法：
#   ./db/seed/setup-charity-db.sh              已存在且已有資料表時略過 DDL，只重新跑一次種子（種子冪等）
#   ./db/seed/setup-charity-db.sh --recreate   從零重來：DROP（若存在）→ CREATE → DDL → 歷史 → 種子（清掉本機資料）
#
# 需要環境變數 MSSQL_DEV_SA_PASSWORD（例如 set -a; source .env; set +a）。
# ⛔ 安全模型：目標資料庫名稱寫死只允許 tcrfc_charity；DROP 只作用在這個名字，碰不到該 instance 上其他資料庫。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && cd .. && pwd)"

RECREATE=0
for arg in "$@"; do
  case "${arg}" in
    --recreate) RECREATE=1 ;;
    *) echo "不認得的參數：${arg}（合法值：--recreate）" >&2; exit 1 ;;
  esac
done

if [[ "${RECREATE}" -eq 1 ]]; then
  echo "==> 步驟 1／2：建立／灌 DDL（deploy/local-ddl.sh --apply-charity-db --recreate）"
  "${REPO_ROOT}/deploy/local-ddl.sh" --apply-charity-db --recreate
else
  echo "==> 步驟 1／2：建立／灌 DDL（deploy/local-ddl.sh --apply-charity-db）"
  "${REPO_ROOT}/deploy/local-ddl.sh" --apply-charity-db
fi

echo
echo "==> 步驟 2／2：灌種子資料（db/seed/apply-charity-seed.sh，目標 tcrfc_charity）"
"${SCRIPT_DIR}/apply-charity-seed.sh"

echo
echo "完成——tcrfc_charity 已就緒。"
