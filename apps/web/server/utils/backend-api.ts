// server/utils/backend-api.ts — 讀取 apps/api（.NET 唯讀 API）用的伺服器端基底網址。
//
// 🔴 刻意不透過 nuxt.config.ts 的 runtimeConfig 宣告（S0-9 資料驅動頁搬遷期間，
// 兩個 frontend-architect agent 同時在搬頁，nuxt.config.ts 被列為「絕對不要動」的
// 共用檔案，兩邊同時改會互相覆蓋）。這裡改用最底層的 process.env 直接讀，
// 完全不經過 Nuxt 的 useRuntimeConfig()——這樣不需要在 nuxt.config.ts 預先宣告
// 任何鍵，且因為只在 Nitro 伺服器端程式碼（server/）裡使用，不會被打進瀏覽器端 bundle。
//
// 鍵名沿用 docker-compose.yml 已經定義好的既有慣例（deployment-engineer 已預留）：
//   NUXT_API_INTERNAL_BASE — SSR 階段走 Docker 內部網路（http://api:8080），不繞 Caddy／Cloudflare
//   NUXT_PUBLIC_API_BASE   — 備援，理論上瀏覽器端才用得到，這裡當 SSR 找不到內部網址時的次要來源
// 本機開發兩者都不會有，退回本機 dotnet run 的預設監聽位址（apps/api/README.md）。
export function backendApiBase(): string {
  return (
    process.env.NUXT_API_INTERNAL_BASE
    || process.env.NUXT_PUBLIC_API_BASE
    || 'http://127.0.0.1:5299'
  )
}
