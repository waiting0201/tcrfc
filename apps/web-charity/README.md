# apps/web-charity — nuxt-charity

🔴 **本目錄目前是空的。** 待 `frontend-architect` 在此建立 Nuxt 4 SSR 專案。

## 這個目錄之後要放什麼

- 標準 Nuxt 4 專案結構，**獨立於 `apps/web`**——慈善捐款平台功能本質不同（掃碼捐款、LINE Pay、電子發票），
  不是配色差異，見 [`docs/20-cicd.md`](../../docs/20-cicd.md) §3。
- 慈善平台**明文不做 SEO／GEO**（[`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md)），
  `@nuxtjs/seo` 是否仍要裝（僅用其他子模組）由 `frontend-architect` 決定。
- build 產物是 `.output/`，執行方式是 `node .output/server/index.mjs`。

## 相關文件

- [`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md) — 慈善捐款平台功能規格
- [`docs/17-deployment.md`](../../docs/17-deployment.md) §5 — 慈善平台獨立性的邊界與補償措施
