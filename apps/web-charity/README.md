# apps/web-charity — nuxt-charity（慈善捐款平台前台）

Nuxt 4 SSR 的可點 mockup，捐款人使用、不登入不註冊。動線：掃碼落地頁 → 項目詳情頁（含捐款表單）
→ 付款模擬轉場 → 結果頁（三態）→ 徵信名單／隱私權政策／捐款須知。規格來源見
[`docs/22-charity-ui.md`](../../docs/22-charity-ui.md)（版面與視覺）與
[`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md)（功能規格）。

## 怎麼跑

```bash
cd apps/web-charity
npm install
npm run build      # prebuild 會先跑 scripts/sync-fixtures.mjs 同步假資料
node .output/server/index.mjs
```

開發模式：`npm run dev`（predev 同樣會先同步假資料）。

## 假資料

**唯一來源是 [`db/seed/charity-fixtures.json`](../../db/seed/charity-fixtures.json)**（衍生自
`db/seed/generate-charity-seed-sql.py`，灌進 `tcrfc_charity_dev` 的同一份種子），⛔ 本專案沒有
另外手寫任何假資料。`scripts/sync-fixtures.mjs` 在 `npm run dev`／`npm run build` 前會自動把它
複製到本機的 `.data/charity-fixtures.json`（不納版控，每次都重新同步，見該檔案開頭註解說明原因），
`server/utils/fixtures.ts` 是唯一讀取這份資料的地方，所有頁面都經由 `server/api/charity/*` 這幾支
唯讀端點取得資料，JSON 本身不會被打進瀏覽器端的 bundle。

`npm run lint` 會執行 `python3 ../../db/seed/emit-charity-fixtures.py --check`，確認
`db/seed/charity-fixtures.json` 與種子腳本本身沒有漂移。

### 🔴 已知缺口：Docker 建置 context

`Dockerfile` 的 `COPY . .` 目前假設 build context 就是 `apps/web-charity/` 本身
（`docs/20-cicd.md` §3 的 paths-filter 慣例），但 `db/seed/charity-fixtures.json` 在 monorepo
根目錄，不在這個 context 裡。**本地 `npm run build`／`npm run dev` 沒有這個問題**（完整
monorepo checkout 下 `scripts/sync-fixtures.mjs` 找得到來源檔案，已實測通過），但如果之後
`docker build` 真的以 `apps/web-charity/` 為 context 建置這個映像檔，`RUN npm run build` 這一步
會在 `prebuild` 找不到來源檔案而失敗。這是部署層的建置 context 設計問題，留給
`deployment-engineer`／CI 設定決定：要嘛把這個 Dockerfile 的 build context 改成 repo root，
要嘛在 `docker build` 前的 CI 步驟裡先把 `db/seed/charity-fixtures.json` 暫存進這個目錄。
Dockerfile 檔頭已加註解說明，本次任務範圍只到 `apps/web-charity/` 底下，沒有動 CI／
`docker-compose` 設定。

## 技術判斷

### `@nuxtjs/seo` 沒有裝

理由：

1. 本平台明文不做 SEO／GEO（`docs/10-charity-donation-site.md`），且**只服務一個網域、一個法人**，
   沒有 `apps/web` 那種「一份 build、多個容器切換品牌網域」的需求，用不到 `nuxt-site-config` 的
   runtime `site.url` 覆寫機制——那是 `@nuxtjs/seo` 對本專案唯一有價值的部分。
2. 仍要做的「基本可讀性」需求只有三項（`docs/22-charity-ui.md` §2.10、規劃書 §2.4）：`noindex`、
   `hreflang` 三組、`og:` 系列標記。三項都用 `nuxt.config.ts` 的 `routeRules`（標頭）＋
   `server/routes/robots.txt.ts`（純文字）＋ 各頁面自己的 `useHead`／`useHreflang()` composable
   手動處理，程式碼量不大，不需要整包 sitemap／schema-org／og-image 相依。
3. 避開已知的建置陷阱：`@nuxtjs/seo` 內建的 `nuxt-og-image` 子模組缺 renderer 會讓 `build` 直接
   失敗（需另外裝 `@takumi-rs/core`，frontend-architect 記憶庫 `nuxt-seo-module-gotchas.md` 與
   `apps/web` 的 `docs/13-blue-whale-site.md` §6 都記錄過這個坑）。本站沒有動態產生 OG 圖的需求
   （沒有真實項目封面照可用），裝這包相依沒有對應的功能價值。

`<html lang>` 用一般的元件層 `useHead()`（見 `app/layouts/default.vue`）即可正確切換，已用
`curl` 實測 zh／en 兩個版本；沒有撞上 `docs/18-work-errors.md` `E-17` 那個 `nuxt-seo-utils` 覆蓋
`htmlAttrs.lang` 的坑——因為那個坑本來就是 `@nuxtjs/seo` 那個子模組自己造成的，沒裝就沒有那個問題。

### 沒有裝 `@nuxtjs/i18n`

規模只有約 10 個路由，用 `[lang]` 動態路由區段 ＋ `app/utils/i18n.ts` 的純物件字典就能滿足雙語
需求，不需要額外的相依與其 lazy-load／routing 中介層。`app/middleware/lang-guard.global.ts` 擋掉
非 `zh`／`en` 的語系值。

### 沒有放任何圖片

協會與店家品牌資產未到位（`STATUS.md` `B-7`），⛔ 不放假圖、不放空 Logo 方框——比照
`docs/22-charity-ui.md` §2.2.1 的降級規則。項目卡片、項目詳情頁封面一律用「中性色塊 ＋ 項目名稱
首字」的佔位呈現，不是壞掉的圖片，也不是假裝有真實封面照。

## 頁面清單

| 路徑 | 說明 |
|---|---|
| `/` | 302 導向 `/zh/` |
| `/{lang}/` | 一般入口 |
| `/{lang}/s/<store_slug>` | 掃碼落地頁 |
| `/{lang}/p/<project_slug>` | 項目詳情頁（含捐款表單，可帶 `?s=<store_slug>` 承接店家歸屬） |
| `/{lang}/pay/<order_no>` | 付款模擬轉場（🔴 本檔自訂路由，規劃書沒有明訂這一頁的網址，見頁面內註解） |
| `/{lang}/result/<order_no>` | 結果頁三態 |
| `/{lang}/donors/` | 捐款徵信名單 |
| `/{lang}/privacy/` | 隱私權政策 |
| `/{lang}/terms/` | 捐款須知 |

### 三態結果頁的實際示範連結

種子資料（`db/seed/charity-fixtures.json`）裡本來就涵蓋六種捐款狀態，結果頁直接依這些真實狀態
決定顯示哪一態，三態都能實際點到（不需要先跑過一次捐款表單）：

- **成功**：`/zh/result/DEVTEST-DN-0001`（`paid`）、`/zh/result/DEVTEST-DN-0007`（`refunded`，
  額外顯示「已退款」標籤，仍視為成功模板，因為當初付款是成功的）
- **未完成**：`/zh/result/DEVTEST-DN-0005`（`failed`）、`/zh/result/DEVTEST-DN-0006`（`expired`）
- **處理中**：`/zh/result/DEVTEST-DN-0003`（`pending`）、`/zh/result/DEVTEST-DN-0004`（`created`）

從捐款表單實際送出的新訂單（不在種子裡）預設顯示「成功」（`?demo=success`，付款轉場頁自動附加），
這是 mockup 沒有真實後端狀態機的克難做法，`app/composables/useCheckoutDraft.ts` 檔頭有說明。

## 已知限制（mockup 專屬，非最終架構）

- 表單草稿與模擬訂單用 Nuxt 的 `useState` 存在瀏覽器記憶體，重新整理頁面會遺失（真正串接後端後，
  復原的權威來源會是後端的捐款單本身，不會是瀏覽器狀態）。
- 付款轉場頁的倒數秒數（2.2 秒）與結果頁「處理中」轉為逾時文案的秒數（8 秒）都是本輪 mockup 的
  建議值，不是最終規格——`docs/22-charity-ui.md` §6 第 3 項已經列了同一個待決事項。
- 「相關的慈善計畫」區塊只顯示文字（機構名稱、計畫名稱），沒有做成可點連結外連主站單元 11.2——
  因為 `apps/web` 的單元 11 尚未建置、也還沒有確認的網址可以連，連一個猜測的網址風險更高。

## 相關文件

- [`docs/22-charity-ui.md`](../../docs/22-charity-ui.md) — 版面與視覺規格（配色、色票、a11y 規則）
- [`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md) — 功能規格
- [`docs/16-charity-schema.md`](../../docs/16-charity-schema.md) — 資料表（本專案透過 fixtures 間接對應）
- [`db/seed/README.md`](../../db/seed/README.md) — 假資料的真實來源與怎麼重新產生
