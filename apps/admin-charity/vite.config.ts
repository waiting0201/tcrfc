import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// 後台是純 SPA，不需要 SSR／SEO 相關 plugin（比照 apps/admin）。
//
// ⚠️ 假資料一律 import fixtures/charity-fixtures.json（單一真實來源 db/seed/charity-fixtures.json
// 的機器產生鏡射複本，見 fixtures/README 與 db/seed/emit-charity-fixtures.py 檔頭）。
// 🔴 這裡刻意不直接指到 `../../db/seed/charity-fixtures.json`：docker-compose.yml 把本專案的
// Docker build context 定為 `./apps/admin-charity`（docs/20-cicd.md §3），db/seed/ 在這個
// build context 之外，容器內建置時完全讀不到——實測 `docker build` 才發現這個落差
// （本機 `vite build` 不會露出這個問題，因為它直接在有完整 repo 的檔案系統上跑）。
// `fixtures/charity-fixtures.json` 在專案目錄內，Docker COPY 與本機開發都吃得到同一份路徑，
// 不需要放行 dev server 的 fs.allow。
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@fixtures/charity-fixtures.json': fileURLToPath(new URL('./fixtures/charity-fixtures.json', import.meta.url)),
    },
  },
  server: {
    port: 5175,
  },
  preview: {
    port: 4174,
  },
})
