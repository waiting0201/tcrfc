# shellcheck shell=bash
# deploy/lib/sqlcmd-guard.sh — sqlcmd 13（mcr.microsoft.com/mssql-tools）結束碼不可信的共用防呆（E-295）
#
# 由 deploy/prod-db-init.sh（prod-seed-import.sh 經由它）與 deploy/db-migrate.sh `source`，不單獨執行。
#
# 🔴 sqlcmd 13 處理批次的「(N rows affected)」訊息時，會在部分批次報 `SqlState 24000, Invalid cursor state`：
#    連線進入錯誤狀態、未 COMMIT 的交易隨斷線回滾、之後的批次全部失敗，**結束碼卻是 0（加了 -b 也一樣）**。
#    E-295：內容種子匯入整批回滾；2026-10-13 第二次：db-migrate 8 支只套 3 支。
#    ① 寫入型 SQL 檔一律經 sqlcmd_nocount_wrapper 包一層 SET NOCOUNT ON 再 :r（連線層級設定，不改任何寫入；
#       原檔不動，SHA-256 鎖定不受影響）——實測兩次事故的檔案都因此完整執行。
#    ② 成敗判斷：結束碼 0 **且** sqlcmd_log_has_error 為假。
#    不改用 mssql-tools18：MCR 沒有獨立映像檔（理由見 docs/18 E-295）。

# 輸出檔含錯誤字樣時回傳 0（真）：Level 11 以上的 Msg（SQL 錯誤）、SqlState（ODBC 錯誤）、`Sqlcmd:` 開頭（sqlcmd 自身錯誤）。
# Level 10 以下是資訊訊息，不算。
sqlcmd_log_has_error() { # ${1}=輸出檔
  grep -Eq '^(Msg [0-9]+, Level (1[1-9]|2[0-5])|SqlState |Sqlcmd: )' "${1}"
}

# 產生包裝檔：SET NOCOUNT ON → GO → :r <容器內的 SQL 檔路徑>。${1}=要寫出的包裝檔（主機路徑） ${2}=容器內被包的 SQL 檔路徑
sqlcmd_nocount_wrapper() {
  printf 'SET NOCOUNT ON;\nGO\n:r %s\n' "${2}" >"${1}"
}

# 🔴 E-330（E-296 的第二次）：sqlcmd 13 讀 **UTF-8** 檔時，多位元組字元落在內部讀取緩衝邊界就被拆壞、各位元組變 U+FFFD
#    （「台灣電力」的「灣」→ ���）。這與 stdin 還是 -i／:r 無關，而是 sqlcmd 13 的 UTF-8 解碼本身；同一個檔每次壞在同樣位置，
#    且 sqlcmd 結束碼 0、無任何警告。本機演練實測：db/prod/club-content-seed.sql 經 import 後 5 欄 15 個 U+FFFD，改餵 UTF-16LE（含 BOM）後為 0。
#    規則：任何含非 ASCII 字元的 SQL 檔交給 sqlcmd 13 之前，一律先以本函式轉成 UTF-16LE＋BOM 的副本（sqlcmd 以 BOM 辨識，
#    以 2 位元組為單位讀、不會切到字元）；原檔不動（SHA-256／manifest 不受影響）。純 ASCII 檔轉了也無害，不必分辨。
#    ${1}=來源 UTF-8 檔  ${2}=輸出的 UTF-16LE 檔（主機路徑）
sqlcmd_to_utf16() {
  command -v iconv >/dev/null 2>&1 || { echo "找不到 iconv（轉 UTF-16 用）" >&2; return 1; }
  { printf '\377\376'; iconv -f UTF-8 -t UTF-16LE "${1}"; } >"${2}"
}

# 套用後防線：資料庫任何 nvarchar／nchar／json 欄位含 U+FFFD（亂碼）就失敗。需要呼叫端提供 sql_scalar（prod-db-init.sh）。
# 掃描 SQL 與 db/tools/scan-replacement-chars.sql 同一份邏輯，這裡只回傳「含亂碼的欄位數」。
sqlcmd_fffd_column_count_sql() {
  cat <<'SQL'
DECLARE @s nvarchar(max) = N'';
SELECT @s += N'SELECT 1 AS x FROM [' + t.name + N'] WHERE CHARINDEX(NCHAR(65533) COLLATE Latin1_General_BIN2, '
  + CASE WHEN ty.name = N'json' THEN N'CAST([' + c.name + N'] AS nvarchar(max))' ELSE N'[' + c.name + N']' END
  + N' COLLATE Latin1_General_BIN2) > 0 UNION ALL '
FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE ty.name IN (N'nvarchar', N'nchar', N'json') AND c.max_length <> 0;
SET @s = N'SELECT COUNT(*) FROM (' + @s + N'SELECT 1 AS x WHERE 1 = 0) q';
EXEC sp_executesql @s;
SQL
}
