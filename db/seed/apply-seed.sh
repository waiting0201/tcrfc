#!/usr/bin/env bash
# db/seed/apply-seed.sh — 產生並灌入本機種子資料（只灌 tcrfc_club）
#
# 做兩件事：
#   1. 執行 generate-club-seed-sql.py，讀 site/src/data/*.json 產生 db/seed/.generated/club-seed.local.sql
#      （該檔含真實姓名，不進版控，見 .gitignore）
#   2. 用 sqlcmd 對本機 SQL Server instance 內的 tcrfc_club 資料庫套用該檔
#
# 冪等：整份 .sql 用「業務自然鍵 IF NOT EXISTS 才 INSERT」寫成，可重複執行。
#
# 🔴🔴 2026-09-21：本機開發資料庫已合併進既有的 sqlserver 容器（見 docker-compose.dev.yml、
# deploy/README.md）。防呆模型因此改變：目標容器可由 LOCAL_MSSQL_CONTAINER 環境變數指定
# （預設 "sqlserver"），但目標資料庫名稱寫死只允許 tcrfc_club 一個——本腳本從來就只灌
# 主站庫，不動 tcrfc_charity（慈善庫是獨立法人邊界，本腳本不處理慈善資料，也沒有慈善
# 的種子來源），這條白名單同時防止「打錯容器」與「打錯庫」兩種情況，兩者現在是同一個風險
# 來源（同一個 instance 裡還有使用者其他專案的資料庫）。
#
# 用法：
#   ./db/seed/apply-seed.sh            產生並套用（灌 tcrfc_club）
#   ./db/seed/apply-seed.sh --dry-run  只產生 .sql，不套用（等同直接跑 generate 腳本）
#
# 🔴 2026-09-30：本機只剩 tcrfc_club（網站）與 tcrfc_charity（慈善）兩庫，測試庫已併入 tcrfc_club。
# 已移除 SEED_TARGET_DATABASE 切換目標庫的用途；若環境裡還殘留這個變數且值不是 tcrfc_club，
# 直接拒絕執行（避免舊習慣悄悄失效）。一鍵建庫＋灌種子見 db/seed/setup-club-db.sh。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && cd .. && pwd)"
OUT_DIR="${SCRIPT_DIR}/.generated"
OUT_FILE="${OUT_DIR}/club-seed.local.sql"

# 本機 SQL Server 所在的容器名稱：預設用既有的 sqlserver 容器，可用環境變數覆寫。
LOCAL_MSSQL_CONTAINER="${LOCAL_MSSQL_CONTAINER:-sqlserver}"

# ⛔ 本腳本唯一允許寫入的資料庫，寫死，不接受任何呼叫端覆寫。
readonly TARGET_DATABASE="tcrfc_club"

if [[ -n "${SEED_TARGET_DATABASE:-}" && "${SEED_TARGET_DATABASE}" != "${TARGET_DATABASE}" ]]; then
  echo "拒絕執行：SEED_TARGET_DATABASE='${SEED_TARGET_DATABASE}' 不是 ${TARGET_DATABASE}。" >&2
  echo "本機測試庫已於 2026-09-30 併入 ${TARGET_DATABASE}，SEED_TARGET_DATABASE 已不再使用，請 unset 它。" >&2
  exit 1
fi

mkdir -p "${OUT_DIR}"

echo "==> 產生種子 SQL（來源：site/src/data/*.json）"
python3 "${SCRIPT_DIR}/generate-club-seed-sql.py" > "${OUT_FILE}"
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

# 依 docker container 名稱精確比對（不是 compose service 名稱——這個容器不是本專案 compose 管理的）。
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

# -f 65001：以 UTF-8 讀取輸入檔，種子資料含中文姓名／標題，不指定會被系統預設 codepage 誤譯。
#
# 🔴 不可用 `docker exec -i ... < 檔案`（stdin 串流）餵檔：stdin 是管線，sqlcmd 以固定大小分塊讀取，
# **多位元組 UTF-8 字元剛好跨塊時會被拆成兩半、各自變成 U+FFFD**（實例：「示範友誼賽」的「友」
# 變成兩個 �，冪等判斷比不到字串而重複 INSERT，種子在區段 39 以 PK 重複中止；E-296）。
# 斷點隨管線時序而變，同一檔不同次跑可能壞在不同列。改成先 docker cp 進容器、再用 -i 讀檔案。
# 密碼改走容器環境變數 SQLCMDPASSWORD，不帶在指令列。
CONTAINER_SQL="/tmp/tcrfc-seed-$$.sql"
trap 'docker exec -u 0 "${CONTAINER_ID}" rm -f "${CONTAINER_SQL}" >/dev/null 2>&1 || true' EXIT
docker cp "${OUT_FILE}" "${CONTAINER_ID}:${CONTAINER_SQL}"
docker exec -e SQLCMDPASSWORD="${MSSQL_DEV_SA_PASSWORD}" "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -C \
  -d "${TARGET_DATABASE}" -f 65001 -b \
  -i "${CONTAINER_SQL}"

# 套完後掃一遍：任何 nvarchar 欄位含 U+FFFD 代表亂碼寫入，直接失敗不放行。
BAD="$(docker exec -e SQLCMDPASSWORD="${MSSQL_DEV_SA_PASSWORD}" "${CONTAINER_ID}" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -C -d "${TARGET_DATABASE}" -f 65001 -b -h -1 -W -Q "SET NOCOUNT ON;
DECLARE @s nvarchar(max)=N'';
SELECT @s += N'SELECT N''' + t.name + N'.' + c.name + N''' FROM [' + t.name + N'] WHERE CHARINDEX(NCHAR(65533) COLLATE Latin1_General_BIN2, [' + c.name + N'] COLLATE Latin1_General_BIN2) > 0 UNION ALL '
FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE ty.name IN ('nvarchar','nchar') AND c.max_length <> 0;
SET @s += N'SELECT N''x'' WHERE 1 = 0';
EXEC sp_executesql @s;")"
if [[ -n "${BAD//[[:space:]]/}" ]]; then
  echo "錯誤：下列欄位含 U+FFFD（亂碼）：" >&2
  echo "${BAD}" >&2
  exit 1
fi

echo
echo "完成。"
