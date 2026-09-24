#!/usr/bin/env bash
# db/seed/reset-admin-accounts.sh — 把種子測試帳號的密碼／2FA 狀態／鎖定計數還原成初始值
# （只還原 tcrfc_club_dev，不動 tcrfc_charity_dev）
#
# 🔴 為什麼需要這支腳本：db/seed/apply-seed.sh 的「IF NOT EXISTS 才 INSERT」冪等策略對這件事
# 沒有幫助——帳號列本來就已經存在，正常種子邏輯永遠不會回頭 UPDATE 它。但密碼、2FA 狀態、
# 登入失敗鎖定計數這幾個欄位本來就是「正常使用會被改掉」的欄位：apps/admin 端對端驗收會真的
# 登入、變更密碼、設定或停用 2FA，這些操作會讓種子帳號（尤其 sa@system.local）的狀態偏離
# 種子腳本原本設定的初始值，導致下一輪測試或驗收「拿種子帳號的已知密碼登入」的假設不成立。
#
# 做法：呼叫 generate-club-seed-sql.py --reset-admin-accounts 產生一組只含 UPDATE（不含
# INSERT）的陳述式，只作用在 db/seed/generate-club-seed-sql.py 的 ADMIN_USERS 清單裡已經存在
# 的帳號列，套用到本機 tcrfc_club_dev。角色指派（admin_user_roles）與俱樂部授權
# （admin_user_clubs）不受影響——那兩張表本來就是「新增才會種」，不會被端對端驗收弄髒。
#
# 安全模型與 db/seed/apply-seed.sh 逐字比照：目標容器可由 LOCAL_MSSQL_CONTAINER 環境變數指定
# （預設 "sqlserver"），目標資料庫名稱寫死只允許 tcrfc_club_dev 一個。
#
# 用法：
#   ./db/seed/reset-admin-accounts.sh            產生並套用
#   ./db/seed/reset-admin-accounts.sh --dry-run  只產生 .sql，不套用
#
# ⚠️ 什麼時候要跑這支：apps/admin 做完一輪端對端驗收（登入、改密碼、設定/停用 2FA）之後，
# 若下一輪工作需要種子帳號回到已知的初始密碼與 2FA 狀態，執行這支腳本一次即可。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="${SCRIPT_DIR}/.generated"
OUT_FILE="${OUT_DIR}/reset-admin-accounts.local.sql"

LOCAL_MSSQL_CONTAINER="${LOCAL_MSSQL_CONTAINER:-sqlserver}"

# ⛔ 本腳本唯一允許寫入的資料庫，比照 apply-seed.sh 的白名單模型，理由同該檔案的說明。
readonly TARGET_DATABASE="tcrfc_club_dev"

mkdir -p "${OUT_DIR}"

echo "==> 產生還原用 SQL（只含 UPDATE，不含 INSERT）"
python3 "${SCRIPT_DIR}/generate-club-seed-sql.py" --reset-admin-accounts > "${OUT_FILE}"
echo "    ${OUT_FILE}（$(wc -l < "${OUT_FILE}" | tr -d ' ') 行，不進版控）"

if [[ "${1:-}" == "--dry-run" ]]; then
  echo
  echo "--dry-run：只產生檔案，未套用。"
  exit 0
fi

echo
echo "==> 即將操作的目標"
echo "    容器（docker container name）：${LOCAL_MSSQL_CONTAINER}"
echo "    資料庫（僅這一個，寫死）：${TARGET_DATABASE}"

CONTAINER_ID="$(docker ps -q --filter "name=^/${LOCAL_MSSQL_CONTAINER}\$" || true)"
if [[ -z "${CONTAINER_ID}" ]]; then
  echo "找不到執行中的容器 '${LOCAL_MSSQL_CONTAINER}'。" >&2
  echo "若你要用的是既有的 sqlserver 容器，請確認它已在跑（docker ps）；" >&2
  echo "若容器名稱不同，設定環境變數 LOCAL_MSSQL_CONTAINER 指到正確的容器名稱。" >&2
  exit 1
fi

: "${MSSQL_DEV_SA_PASSWORD:?請設定 MSSQL_DEV_SA_PASSWORD（須與 '${LOCAL_MSSQL_CONTAINER}' 容器的 SA 密碼一致，例如 set -a; source .env; set +a）}"

echo
echo "==> 套用到 ${TARGET_DATABASE}"

docker exec -i "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${MSSQL_DEV_SA_PASSWORD}" -C \
  -d "${TARGET_DATABASE}" -f 65001 -b \
  < "${OUT_FILE}"

echo
echo "完成——種子測試帳號的密碼／2FA 狀態／鎖定計數已還原成初始值。"
