# apps/web-charity/Dockerfile — nuxt-charity
#
# 對應決定：docs/20-cicd.md §3「五個映像檔」——慈善前台獨立映像檔（功能本質不同，不是配色差異）。
#
# ⚠️ 慈善平台明文不做 SEO／GEO（docs/10-charity-donation-site.md），但部署層仍是一般 Nuxt 4 SSR
# 產物，架構與 apps/web/Dockerfile 相同，只是不套用 docs/13 §6 的品牌切換紀律（本站只有一個品牌）。
#
# 產物假設：`npm run build` 產出 Nuxt 4 的 `.output/`，內含 `.output/server/index.mjs`。
#
# 🔴 已知缺口，留給 deployment-engineer／CI 設定確認（本次任務範圍只到 apps/web-charity/ 底下，
# 不擅自改動 docker-compose／CI 設定，見本次任務回報）：
# 假資料的唯一來源 db/seed/charity-fixtures.json 位在 monorepo 根目錄，本專案用
# `npm run build` 的 `prebuild` 生命週期腳本（scripts/sync-fixtures.mjs）在建置前把它同步進
# 本專案內的 .data/ 目錄再 import——這在「完整 monorepo checkout 下直接跑 npm run build」時
# 沒問題（本地開發、CI 的 lint／unit test 步驟），但如果這個 Dockerfile 實際被建置時的
# build context 局限在 apps/web-charity/ 本身（docs/20-cicd.md §3 的 paths-filter 慣例看起來
# 是這樣），`COPY . .` 拿不到 monorepo 根目錄的 db/seed/，sync-fixtures.mjs 會在 RUN npm run
# build 這一步失敗。需要 deployment-engineer／CI 決定：要嘛把這個 Dockerfile 的 build context
# 改成 repo root，要嘛在 docker build 前的 CI 步驟裡把 db/seed/charity-fixtures.json 暫存進
# 這個目錄下再建置。本檔沒有自行假設其中一種解法。

FROM node:22.12-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:22.12-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:22.12-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NITRO_PORT=3000 \
    NITRO_HOST=0.0.0.0
COPY --from=build --chown=node:node /app/.output ./.output
USER node
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:3000/healthz').then(r=>{if(r.status>=500)process.exit(1)}).catch(()=>process.exit(1))"
CMD ["node", ".output/server/index.mjs"]
