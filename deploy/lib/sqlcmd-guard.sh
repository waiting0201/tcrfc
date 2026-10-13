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
