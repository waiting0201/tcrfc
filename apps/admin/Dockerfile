# apps/admin/Dockerfile — admin-web（官網共用後台，Vue 3 SPA）
#
# 🔴 本檔待 apps/admin 專案建立後才能 build（S0-7a 階段目錄是空的，見 apps/admin/README.md）。
# 對應決定：docs/20-cicd.md §3「五個映像檔」——後台不需要 SSR／SEO，純靜態 SPA。
#
# runtime 用 nginxinc/nginx-unprivileged：官方 nginx 映像檔預設以 root 啟動（再 drop 權限），
# unprivileged 版本從一開始就以非 root（uid 101）執行、監聽 8080，不需要額外 chown／setcap 步驟，
# 是「非 root 使用者跑」這條要求最省事、最不容易漏掉的做法（本檔自行決定，docs 未指定 base image）。

# ---- 1. build：Vite 產出靜態檔 ----
FROM node:22.12-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# API 的公開網址等屬於 runtime config 的值，不要用 --build-arg 烤進靜態產物；
# 若專案設計成 build-time 注入（SPA 常見限制），改版時请同步這裡的註解與 .env.example。
RUN npm run build

# ---- 2. runtime：靜態檔 + SPA fallback ----
FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
COPY --chown=nginx:nginx nginx-spa.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=nginx:nginx /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/healthz >/dev/null || exit 1
CMD ["nginx", "-g", "daemon off;"]
