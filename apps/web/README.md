# apps/web — nuxt-club（主站 ＋ 藍鯨共用）

**骨架已完成**（2026-09-21，S0-9a，`frontend-architect`）。這是標準 Nuxt 4 SSR 專案，
**一份程式碼同時服務主站（`tcrfc`）與藍鯨官網（`bw`）兩個容器**，由 `NUXT_PUBLIC_CLUB` 這個
runtime 環境變數決定品牌。

🔴 **只做到「首頁一頁能跑起來、機制驗證通過」**，80 頁完整搬遷是後續工作（`STATUS.md` S0-9，
尚未開始）。本檔案只記錄骨架的真實現況，不是完整規格——規格看
[`docs/02-frontend-spec.md`](../../docs/02-frontend-spec.md)。

---

## 怎麼跑

> 🔴 **先做這一步，否則 `npm run build` 會失敗**：`public/assets/img/` 的 158 張客戶照片
> **不納版控**（含未成年學員肖像，GitHub repo 是公開的，見 `.gitignore` 與 [`docs/18`](../../docs/18-work-errors.md) `E-26`）。
> 乾淨 clone 下來這個目錄是空的，而 Vite 會把 `<img src="/assets/...">` 編譯成 import——
> **檔案不存在是 build-time 硬錯誤，不是 runtime 404**。先補檔再 build：
>
> ```bash
> rsync -a --ignore-existing site/src/assets/img/ apps/web/public/assets/img/
> ```
>
> `site/src/assets/img/` 本身也不納版控，重建方式是 `bash site/tools/build-images.sh`（讀客戶收件夾）。
> ⚠️ **部署映像檔時同理**——`docker build` 的環境必須先有這批檔案，見 [`docs/20`](../../docs/20-cicd.md)。


```bash
cd apps/web
npm install
npm run build              # 產出 .output/
```

本機測試兩個 club（同一份 build、兩個 process）：

```bash
NODE_ENV=production NITRO_PORT=3001 \
  NUXT_PUBLIC_CLUB=tcrfc NUXT_PUBLIC_SITE_URL=https://tcrfc.tw \
  node .output/server/index.mjs &

NODE_ENV=production NITRO_PORT=3002 \
  NUXT_PUBLIC_CLUB=bw NUXT_PUBLIC_SITE_URL=https://bw-stg.tcrfc.tw \
  node .output/server/index.mjs &

curl -s http://127.0.0.1:3001/zh/ | grep -o 'data-club="[a-z]*"'   # tcrfc
curl -s http://127.0.0.1:3002/zh/ | grep -o 'data-club="[a-z]*"'   # bw
```

開發模式：`npm run dev`（會用 `nuxt.config.ts` 的 `runtimeConfig.public.club` 預設值 `tcrfc`，
除非另外帶 `NUXT_PUBLIC_CLUB=bw npm run dev`）。

## 環境變數

| 變數 | 何時給 | 說明 |
|---|---|---|
| `NUXT_PUBLIC_CLUB` | `docker run` / 容器啟動 | `tcrfc` 或 `bw`，決定品牌色、favicon、OG 圖、導覽單元開關 |
| `NUXT_PUBLIC_SITE_URL` | 🔴 只在 `docker run`，**絕不在 `docker build`** | canonical／sitemap／`hreflang`／Schema／`og:image` 的網域來源（`docs/13-blue-whale-site.md` §6 紀律 7、8） |
| `NUXT_PUBLIC_SITE_ENV` | `docker run` | `prelaunch`／`production`，目前只接住變數，三層防護（見 `docs/17-deployment.md` §10.4）留給 S0-9 之後接上 |
| `NITRO_PORT` / `NITRO_HOST` | 容器啟動 | `apps/web/Dockerfile` 已設定為 `3000` / `0.0.0.0` |

## 十條紀律落在哪個檔案（`docs/13-blue-whale-site.md` §6）

| # | 紀律 | 落點 |
|---|---|---|
| 1 | 顏色只能是 CSS custom properties | `public/assets/css/tcrfc.css`（原封不動）＋ `public/assets/css/club-bw.css`（只覆寫 7 個變數）；沒有裝 Tailwind |
| 2 | club 專屬靜態資產兩份都打包進映像檔，runtime 選路徑 | `app/utils/club.ts` 的 `getClubAssets()`，呼叫點在 `app/app.vue` |
| 3 | 單元開關只有一個真實來源 | `shared/utils/units.ts` 的 `isUnitEnabledForClub()`；四個呼叫點見下表 |
| 4 | 選 SEO 模組先實測 `NUXT_PUBLIC_SITE_URL` 能不能 runtime 覆寫 | 已於 S0-9b 完成（`@nuxtjs/seo`），本次骨架沿用同一個結論 |
| 5 | 新增前台功能先問「兩站都該有嗎」 | 骨架階段體現在 `SiteHeader.vue`／`SiteFooter.vue` 用 `v-if="isUnitEnabledForClub(...)"` 過濾導覽項 |
| 6 | i18n 設定兩站一致 | `nuxt.config.ts` 的 `site.defaultLocale`；目前只有 `zh` 頁面，`en` 留給 S0-9 |
| 7 | `site.url` 不寫進 `nuxt.config.ts` | 已遵守，見 `nuxt.config.ts` 檔頭註解 |
| 8 | `NUXT_PUBLIC_SITE_URL` 不在 `docker build` 階段帶 | `apps/web/Dockerfile` 本來就沒有帶，本次沒有新增任何違反 |
| 9 | `<style>`／`<script>` 不得留在樣板區塊裡 | 全部轉成 SFC 頂層 `<script setup>` 與 `<style>`（本次骨架頁面沒有頁面專屬 CSS，故沒有頂層 `<style>` 區塊） |
| 10 | 移上去的 `<style>` 不得加 `scoped` | 目前沒有任何 `<style>` 區塊；日後補頁面樣式時要記得這條 |

**單元開關的四個呼叫點**（紀律 3）：

1. `app/middleware/unit-gate.global.ts`（route middleware，依 `definePageMeta({ unit })` 檔 404）
2. `app/components/SiteHeader.vue`／`SiteFooter.vue`（導覽選單，`v-if` 過濾女子足球／慈善連結）
3. `server/api/__sitemap__/urls.ts`（sitemap 的網址來源；⚠️ `/sitemap.xml` 本身的輸出尚未接通，見下方「已知缺口」）
4. `server/routes/llms.txt.ts` / `llms-en.txt.ts`（GEO-01）；`robots.txt` 由 `nuxt.config.ts` 的
   `robots: { disallow: ['/'] }` 全站擋（上線前 noindex，`CLAUDE.md` 第 5 條），
   待正式期改為完整 GEO 版時同樣要呼叫這支函式，不得另開一份邏輯

資料表本身（`shared/utils/site-units.ts`）目前只到「單元」層級的骨架設定檔，
不是最終資料來源——之後應該改讀後台維護的真實內容。

## 開發注意事項

三個今天搬遷時踩到、值得動手前先知道的細節（詳見 [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-22`–`E-24`）：

- 🔴 **跑 `npm run lint` 前先跑 `npx nuxt prepare`**：`.nuxt/link-checker/routes.json` 是建置期快取，
  頁面搬遷或路由變動後沒有重跑會讓 link-checker 拿舊路由表比對，回報大量假性斷鏈（今天一度
  118 → 415，重跑後才降回真實的 65）。把 `nuxt prepare` 當成 lint 的必要前置步驟。
- 🔴 **子目錄元件的標籤名要加目錄前綴**：`components/content/X.vue` 的自動匯入標籤是
  `<ContentX />`，不是 `<X />`。**寫錯不報錯也不警告，該元件整塊悄悄不 render**——目視看不出來，
  只有逐 DOM 比對才抓得到，落筆引用前先確認拼出來的 PascalCase 標籤名。
- ⚠️ **`v-show` 不等於 mockup 的 `hidden` 屬性**：mockup 用原生 `hidden` 屬性做顯示切換，
  `v-show` 在 DOM 層是另一種東西（`style="display: none;"` vs `hidden` 屬性），會被 compare-dom
  判定成差異。需要對應 mockup 的 `hidden` 行為時用 `:hidden="condition"` 或直接綁 `hidden` 屬性，
  不要改用 `v-show`。

## 已知缺口與尚未完成的事

- 🔴 **`/sitemap.xml` 的輸出目前是空的 `<urlset>`**（`@nuxtjs/sitemap` 的動態來源偵測與
  「一份 build、runtime 才決定 club」衝突），資料端點 `/api/__sitemap__/urls` 本身已驗證
  依 club 正確過濾，但模組沒有把它接進最終 XML。詳見 [`docs/18-work-errors.md`](../../docs/18-work-errors.md) E-18。
- 🟡 **藍鯨的 favicon／apple-touch-icon／OG 圖不是正式設計稿**：`brand/blue-whale/` 只有隊徽
  點陣主檔（3299×3243 去背 PNG），沒有向量、沒有專屬的 favicon／OG 設計。本骨架用「裁切成正方形
  ＋ 縮放」從官方點陣主檔產生 `public/assets/brand/bw/{favicon-32,favicon-48,apple-touch-icon}.png`
  （純幾何操作，不是造標或描摹，`brand/blue-whale/README.md` 明列這是點陣主檔的允許用途之一），
  OG 圖直接重用 `bw-crest-512.png` 本身、沒有另外設計版面。**缺**：向量標誌主檔、專屬 OG 設計稿、
  印刷色票——見 `docs/13-blue-whale-site.md` §5 待確認事項第 2 項。
- 🟡 **`llms.txt`／`llms-en.txt` 只到事實摘要骨架**：正式規格是「由後台 H 模組維護、隨發布重產、
  不以人工改檔」（`docs/14-invariants.md`），但後台尚未開發，目前是程式碼寫死的骨架文字。
- ⬜ 只有 `/`（轉址）與 `/zh/`（首頁）兩個路由；其餘導覽連結（`/zh/about/`、`/zh/club/`…）
  目前都是有效的 `<a href>` 但頁面不存在，會 404——這是刻意的（S0-9 完整搬遷前的預期狀態），
  `npm run lint` 的 `link-checker/valid-route` 錯誤就是在提醒這件事，不是骨架本身的 bug。
- ⬜ 只有繁中（`zh`）頁面，`en` 語系與 `hreflang` 留給 S0-9。
- ⬜ `app/middleware/unit-gate.global.ts` 目前無法被實際路由觸發測試（唯一頁面 `unit: '01'`
  兩站皆開放），S0-9 加入 `06`／`11` 對應頁面後才能驗證 404 行為。

## 相關文件

- [`docs/02-frontend-spec.md`](../../docs/02-frontend-spec.md) — 前台頁面規格
- [`docs/13-blue-whale-site.md`](../../docs/13-blue-whale-site.md) §6／§6a — 共用映像檔的技術判斷、七個品牌色變數
- [`docs/05-i18n-seo.md`](../../docs/05-i18n-seo.md) — 雙語、SEO、GEO
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) — E-16／E-17／E-18，本次骨架踩到的三個坑
- [`STATUS.md`](../../STATUS.md) — S0-9a（本次成果）、S0-9（完整搬遷，尚未開始）
