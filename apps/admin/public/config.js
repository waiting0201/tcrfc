// 執行期設定的「開發／預設」版本：空物件＝沒有執行期覆寫。
// 正式容器啟動時，docker-entrypoint.d/40-runtime-config.sh 會依環境變數 ADMIN_API_BASE_URL
// 產生真正的 config.js，由 nginx 在 /config.js 這個路徑蓋過本檔（見 nginx-spa.conf）。
// 本機 `npm run dev` 讀到的就是這份空設定，API 位址退回 import.meta.env（見 src/api/runtimeConfig.ts）。
window.__TCRFC_CONFIG__ = window.__TCRFC_CONFIG__ || {}
