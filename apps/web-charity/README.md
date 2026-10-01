# apps/web-charity — nuxt-charity（慈善捐款平台前台）

Nuxt 4 SSR，捐款人使用、不登入不註冊。**已接真 API**（CH-2，2026-10-01）：掃碼落地頁 → 項目列表與詳情 →
捐款表單 → 付款跳轉 → 付款返回（confirm／cancel）→ 結果頁（輪詢）→ 隱私權政策／捐款須知。
主辦與收款主體是**台灣足球策略發展協會**（不是俱樂部），CTA 與文案一律明示。**慈善平台明文不做 SEO／GEO**，全站 `noindex`。
規格來源：[`docs/22-charity-ui.md`](../../docs/22-charity-ui.md)（版面）、[`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md)（功能）、
後端契約見 [`apps/api/README.md`](../api/README.md)「慈善 CH-2／CH-3」。

## 怎麼跑

```bash
cd apps/web-charity
npm install
npm run dev        # 或 npm run build && node .output/server/index.mjs
npm run typecheck  # nuxt typecheck
npm run lint
```

環境變數（皆為 Nuxt runtimeConfig，執行期才讀，見 `nuxt.config.ts`）：

| 變數 | 用途 | 預設 |
|---|---|---|
| `NUXT_API_INTERNAL_BASE` | SSR 階段呼叫 api（Docker 內網 `http://api:8080`） | `http://127.0.0.1:5299` |
| `NUXT_PUBLIC_API_BASE` | 瀏覽器階段呼叫 api（公開 API 網域） | `http://127.0.0.1:5299` |
| `NUXT_PUBLIC_TURNSTILE_SITE_KEY` | 設了才顯示 Turnstile（須與後端 `TURNSTILE_SECRET_KEY_CHARITY` 一起開） | 空（不顯示） |
| `NUXT_PUBLIC_SIMULATED_PAYMENT` | `true` 才開 `/{lang}/pay/<單號>` 模擬付款頁（對應後端假金流）；`nuxt dev` 時一律開 | `false` |

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
| `/{lang}/` | 一般入口（項目卡片牆） |
| `/{lang}/s/<store_slug>` | 掃碼落地頁；對不到有效店家（API 回 `store: null`）一律降級成一般入口，不報錯，並清掉殘留的店家 Cookie |
| `/{lang}/p/<project_slug>` | 項目詳情頁（含捐款表單；店家歸屬優先序 `?s=` > Cookie > 無） |
| `/{lang}/pay/<order_no>` | 🔴 **僅測試環境**：模擬 LINE Pay 付款頁（見上表 `NUXT_PUBLIC_SIMULATED_PAYMENT`），正式環境回 404 |
| `/{lang}/result/<order_no>` | 付款返回頁＋結果頁（`?transactionId=` → confirm、`?cancel=1` → cancel、其餘 → 查詢） |
| `/{lang}/donors/` | 徵信名單（⚠️ 後端無端點，只顯示「未開放」或「準備中」） |
| `/{lang}/privacy/`、`/{lang}/terms/` | 內容來自 `GET /settings`（後台維護），前台不自編法律文字 |

## 相關文件

- [`docs/22-charity-ui.md`](../../docs/22-charity-ui.md) — 版面與視覺規格（配色、色票、a11y 規則）
- [`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md) — 功能規格
- [`docs/16-charity-schema.md`](../../docs/16-charity-schema.md) — 資料表（本專案只透過公開 API 取得資料）

## CH-2 串接（2026-10-01）

### 資料來源

全部資料來自 apps/api 的公開端點 `/api/v1/donation-platform/…`，由 `app/composables/useCharityApi.ts` 統一呼叫：
SSR 讀取走內網（結果隨 payload 帶到瀏覽器），**寫入與輪詢由瀏覽器直打公開 API 網域**——不經 Nuxt 代轉，理由：api 的限流依訪客真實 IP，
`nuxt-charity` 容器刻意不在 `TRUSTED_PROXY_IPS`（docs/14），代轉會讓全站捐款人共用一份額度（30 次／10 分鐘）。代價是 api 的 CORS 必須允許本站網域。
本機開發要把 `http://localhost:3003`（或你用的埠）加進 api 的 `CORS_ALLOWED_ORIGINS`（Development 預設清單只有 3000／3001／3002／5174）。

### 流程與冪等

填表送出 → `POST /donations`（`Idempotency-Key`＝UUID；表單內容沒變就沿用同一個鍵，內容變了換新鍵）→ `POST /donations/{單號}/pay` → 整頁導向 `paymentUrl`。
建單成功但發起付款失敗時，再按一次送出會沿用同一個鍵，後端回原單，不會重複建單。付款失敗／取消後的重試走「沿用原單」的 `/pay`，不需重填。
結果頁：`?transactionId=` → `confirm`（成功才清掉網址參數）；confirm 暫時打不通時**保留參數**並明示「請不要重複付款」；`?cancel=1` → `cancel`；
`processing:true` 每 3 秒輪詢，約 3 分鐘後停止並提供「重新查詢結果」按鈕（不無限轉圈）。發票開立失敗對外一律顯示「開立中」。

### 範圍縮減與缺口

- **徵信名單**：後端無公開端點（CH-5），頁面不顯示任何名單、不用假資料頂替；`creditListEnabled=false` 時頁尾連結也隱藏。
- **Turnstile**：後端沒有「是否啟用」的查詢端點，前台以 `NUXT_PUBLIC_TURNSTILE_SITE_KEY` 決定要不要顯示，兩邊必須一起開（後端開、前台沒給 → 建單回 422）。**未以真實 site key 實測**（只驗證不給 key 時的流程）。
- 「連回俱樂部官網 11 章成果紀錄」（規劃書 §3.1）：沒有確認的網址，不放猜測的連結。
- 英文版協會名稱一律「the Association」＋中文正式名，不自創英文全名（docs/22 §6 第 5 項）。
- 表單草稿只存在瀏覽器記憶體（含身分證字號等個資，不寫 localStorage），重新整理會清空。

### 規格疑點

1. 規劃書 §3.4 要「逾時上限與逾時後文案需明確定義」但未給秒數：本輪採輪詢 3 秒、提示 15 秒、停止 3 分鐘（執行層值，可調）。
2. 捐贈發票的愛心碼在規劃書是「必填」，舊 mockup 未驗證；現在必填並驗 3–7 碼數字（與後端一致）。
3. 手機條碼後端只收大寫，前台送出前轉大寫。

### 🔴 待使用者處理：殘留的舊 mockup 檔案

本輪 agent 要刪除下列已無人使用的假資料檔案時被權限分類器擋下，**尚未刪除**，目前以 `nuxt.config.ts` 的 `ignore` 暫時排除（不會被打包成路由）：

- `server/api/charity/`（整個資料夾）、`server/utils/fixtures.ts`、`scripts/sync-fixtures.mjs`、`fixtures/charity-fixtures.json`、（本機的 `.data/`）

刪除後請一併移除 `nuxt.config.ts` 的 `ignore` 設定。注意 `db/seed/emit-charity-fixtures.py` 仍會產生並檢查 `apps/web-charity/fixtures/charity-fixtures.json`（`apps/admin-charity` 的 `lint:fixtures` 會呼叫它）：
**要刪 `fixtures/` 之前，須先請維護該腳本的人把 web-charity 從匯出目標拿掉**，否則 admin-charity 的 lint 會失敗。Dockerfile 的「build context 缺口」註解已隨之失效。

### 驗收步驟（等使用者啟動 API 並合併後）

1. 使用者啟動 `apps/api`（port 5299，並設 `CHARITY_SQL_CONNECTION_STRING`、`JWT_SIGNING_KEY_CHARITY`，`CORS_ALLOWED_ORIGINS` 含本站來源；本機假金流需 `Development`）。agent 不啟動 API、不碰密碼（E-56）。
2. `NUXT_PUBLIC_SIMULATED_PAYMENT` 在 `nuxt dev` 下自動開。`npm run dev` → 開 `/zh/`，確認項目卡片來自種子資料。
3. 用種子店家的 slug 開 `/zh/s/<slug>` → 點項目 → 填表 → 送出 → 進模擬付款頁 → 「模擬付款成功」→ 結果頁顯示成功與遮罩個資。
4. 重複：取消返回 → 結果頁「尚未完成」→「重新嘗試付款」沿用原單。
5. 亂填店家 slug → 頁面降級為一般入口且可正常捐款。
6. `curl -I /zh/` 確認 `X-Robots-Tag: noindex, nofollow`。
