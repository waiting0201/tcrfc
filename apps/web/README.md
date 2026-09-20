# apps/web — nuxt-club（主站 ＋ 藍鯨共用）

🔴 **本目錄目前是空的。** 待 `frontend-architect` 在此建立 Nuxt 4 SSR 專案。

## 這個目錄之後要放什麼

- 標準 Nuxt 4 專案結構（`app/`、`nuxt.config.ts`、`package.json`、`package-lock.json`…）
- **一份程式碼，同時服務主站（`nuxt-tcrfc`）與藍鯨官網（`nuxt-bw`）兩個容器**，
  由 `NUXT_PUBLIC_CLUB=tcrfc|bw` 這個 runtime 環境變數決定品牌（色彩、favicon、OG 圖），
  見 [`docs/13-blue-whale-site.md`](../../docs/13-blue-whale-site.md) §6 的六＋二條開發紀律。
- 模組 `@nuxtjs/seo`，已於 2026-09-20 實測 `NUXT_PUBLIC_SITE_URL` 可 runtime 覆寫
  （見 `docs/13` §6「SEO 的 canonical 與 sitemap 已實測可 runtime 覆寫」）。
- **🔴 `site.url` 絕對不寫進 `nuxt.config.ts`；`NUXT_PUBLIC_SITE_URL` 只在 `docker run`／容器啟動時給，
  `docker build` 階段絕對不要帶這個環境變數**（紀律 7、8，烤進 build 產物會變成靜默錯誤的預設回退值）。
- build 產物是 `.output/`，執行方式是 `node .output/server/index.mjs`（[`Dockerfile`](Dockerfile) 已依此假設撰寫）。

## 相關文件

- [`docs/02-frontend-spec.md`](../../docs/02-frontend-spec.md) — 前台頁面規格
- [`docs/13-blue-whale-site.md`](../../docs/13-blue-whale-site.md) §6／§6a — 共用映像檔的技術判斷、七個品牌色變數
- [`docs/05-i18n-seo.md`](../../docs/05-i18n-seo.md) — 雙語、SEO、GEO
