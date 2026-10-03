# apps/admin-charity/Dockerfile — admin-charity（慈善獨立後台，Vue 3 SPA）
#
# 🔴 本檔待 apps/admin-charity 專案建立後才能 build（S0-7a 階段目錄是空的，見 apps/admin-charity/README.md）。
# 對應決定：docs/20-cicd.md §3「五個映像檔」——docs/17-deployment.md §5「獨立後台、獨立帳號體系、獨立 2FA」，
# 與 apps/admin 功能模組不重疊，因此獨立映像檔，架構與 apps/admin/Dockerfile 相同。

FROM node:24.13.1-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

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
