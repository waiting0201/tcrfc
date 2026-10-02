import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// 後台是純 SPA，不需要 SSR／SEO 相關 plugin（比照 apps/admin）。
//
// ⚠️ 2026-10-02 起沒有任何畫面使用假資料（N4–N7 全部接真 API，src/data/fixtures.ts 已刪除）。
// 下面的 `@fixtures` 別名與 fixtures/charity-fixtures.json（db/seed/charity-fixtures.json 的機器產生鏡射複本）
// 只因 `npm run lint` 的 lint:fixtures 仍會比對而保留，見 README「假資料」節。
// 🔴 當初這裡刻意不直接指到 `../../db/seed/charity-fixtures.json`：docker-compose.yml 把本專案的
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
