# apps/web-charity/Dockerfile — nuxt-charity
#
# 🔴 本檔待 apps/web-charity 專案建立後才能 build（S0-7a 階段目錄是空的，見 apps/web-charity/README.md）。
# 對應決定：docs/20-cicd.md §3「五個映像檔」——慈善前台獨立映像檔（功能本質不同，不是配色差異）。
#
# ⚠️ 慈善平台明文不做 SEO／GEO（docs/10-charity-donation-site.md），但部署層仍是一般 Nuxt 4 SSR
# 產物，架構與 apps/web/Dockerfile 相同，只是不套用 docs/13 §6 的品牌切換紀律（本站只有一個品牌）。
#
# 產物假設：`npm run build` 產出 Nuxt 4 的 `.output/`，內含 `.output/server/index.mjs`。

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
