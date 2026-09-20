# apps/web/Dockerfile — nuxt-club（主站 ＋ 藍鯨共用一個映像檔）
#
# 🔴 本檔待 apps/web 專案建立後才能 build（S0-7a 階段目錄是空的，見 apps/web/README.md）。
# 對應決定：docs/20-cicd.md §3「五個映像檔」、docs/13-blue-whale-site.md §6 六＋二條開發紀律。
#
# 🔴 硬性規則（違反其中一條，主站／藍鯨共用映像檔就悄悄失效，見 docs/13 §6）：
#   - 絕對不要在這個 Dockerfile 用 ARG／ENV 帶入 NUXT_PUBLIC_SITE_URL 或任何 site.url 相關值。
#     它只能在 `docker run`／compose 的 environment: 給，帶進 build 會被 nuxt-site-config
#     以較低優先權烤進 .output 當「靜默錯誤」的預設回退值（docs/13 §6 紀律 7、8）。
#   - 不要把任何真正的機密（連線字串、API 金鑰）當 build arg——本檔全程不接受這類 ARG，
#     映像檔會推到公開的 ghcr.io（docs/20-cicd.md §2）。
#
# 產物假設：`npm run build` 產出 Nuxt 4 的 `.output/`，內含 `.output/server/index.mjs`，
# 用 `node` 直接執行（Nuxt 4 SSR 的標準 Node 部署方式，非 Nitro preset 特例）。
# 若專案實際採用其他 Nitro preset（如 bun、deno），此檔需要對應調整。

# ---- 1. deps：只裝相依，最大化 layer cache 命中率 ----
FROM node:22.12-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# ---- 2. build：帶入原始碼，產出 .output ----
FROM node:22.12-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# ⛔ 不要在這裡加 ENV NUXT_PUBLIC_SITE_URL=... —— 見檔頭說明
RUN npm run build

# ---- 3. runtime：最小化，非 root 使用者 ----
FROM node:22.12-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NITRO_PORT=3000 \
    NITRO_HOST=0.0.0.0
# node:alpine 內建的 `node` 使用者（uid 1000），不用另外建立
COPY --from=build --chown=node:node /app/.output ./.output
USER node
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:3000/healthz').then(r=>{if(r.status>=500)process.exit(1)}).catch(()=>process.exit(1))"
CMD ["node", ".output/server/index.mjs"]
