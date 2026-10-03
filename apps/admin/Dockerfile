# apps/admin/Dockerfile — admin-web（官網共用後台，Vue 3 SPA）
#
# 🔴 本檔待 apps/admin 專案建立後才能 build（S0-7a 階段目錄是空的，見 apps/admin/README.md）。
# 對應決定：docs/20-cicd.md §3「五個映像檔」——後台不需要 SSR／SEO，純靜態 SPA。
#
# runtime 用 nginxinc/nginx-unprivileged：官方 nginx 映像檔預設以 root 啟動（再 drop 權限），
# unprivileged 版本從一開始就以非 root（uid 101）執行、監聽 8080，不需要額外 chown／setcap 步驟，
# 是「非 root 使用者跑」這條要求最省事、最不容易漏掉的做法（本檔自行決定，docs 未指定 base image）。

# ---- 1. build：Vite 產出靜態檔 ----
FROM node:24.13.1-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# API 的公開網址屬於 runtime config，不用 --build-arg 烤進靜態產物：容器啟動時由
# docker-entrypoint.d/40-runtime-config.sh 依 ADMIN_API_BASE_URL 產生 /config.js（見下方 runtime 階段）。
RUN npm run build

# ---- 2. runtime：靜態檔 + SPA fallback ----
FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
COPY --chown=nginx:nginx nginx-spa.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=nginx:nginx /app/dist /usr/share/nginx/html
# 執行期注入 API 位址：nginx 官方 entrypoint 會在啟動前執行 /docker-entrypoint.d/*.sh，
# 依環境變數 ADMIN_API_BASE_URL 產生 /tmp/config.js（nginx-spa.conf 的 location = /config.js 提供）。
COPY --chmod=0755 docker-entrypoint.d/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/healthz >/dev/null || exit 1
CMD ["nginx", "-g", "daemon off;"]
