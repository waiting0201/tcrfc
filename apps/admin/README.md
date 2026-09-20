# apps/admin — admin-web（官網共用後台）

🔴 **本目錄目前是空的。** 待 `frontend-architect` 在此建立 Vue 3 SPA 專案。

## 這個目錄之後要放什麼

- 標準 Vue 3 SPA 專案結構（Vite ＋ Vue 3，`package.json`…）。
- 官網主站 ＋ 藍鯨官網**共用同一個後台**，以 `club_id` 分資料、後台需要站台切換器，
  見 [`docs/03-admin-spec.md`](../../docs/03-admin-spec.md)、[`CLAUDE.md`](../../CLAUDE.md)「後台設計」。
- 後台不需要 SEO（SPA、`noindex`），不裝 `@nuxtjs/seo` 之類的模組。
- build 產物是靜態檔（`dist/`），由 [`Dockerfile`](Dockerfile) 的 nginx 階段服務。

## 相關文件

- [`docs/03-admin-spec.md`](../../docs/03-admin-spec.md) — 後台模組與權限
- [`docs/06-conventions.md`](../../docs/06-conventions.md) §1 — 後台介面用語對照表（日常中文，不顯示模組代號）
