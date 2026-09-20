# apps/admin-charity — admin-charity（慈善平台獨立後台）

🔴 **本目錄目前是空的。** 待 `frontend-architect` 在此建立 Vue 3 SPA 專案。

## 這個目錄之後要放什麼

- 標準 Vue 3 SPA 專案結構，**獨立帳號體系、獨立 2FA**（[`docs/17-deployment.md`](../../docs/17-deployment.md) §5），
  不與 `apps/admin` 共用任何程式碼或部署單元。
- 功能模組 `N1–N7`，見 [`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md)。
- build 產物是靜態檔（`dist/`），由 [`Dockerfile`](Dockerfile) 的 nginx 階段服務。

## 相關文件

- [`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md) — 慈善捐款平台功能規格
- [`docs/17-deployment.md`](../../docs/17-deployment.md) §5 — 慈善平台獨立性的邊界與補償措施
