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
  NUXT_PUBLIC_CLUB=tcrfc NUXT_PUBLIC_SITE_URL=https://tcrfc.tw NUXT_PUBLIC_SITE_NAME=TCRFC \
  node .output/server/index.mjs &

NODE_ENV=production NITRO_PORT=3002 \
  NUXT_PUBLIC_CLUB=bw NUXT_PUBLIC_SITE_URL=https://bw-stg.tcrfc.tw NUXT_PUBLIC_SITE_NAME=台中藍鯨 \
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
| `NUXT_PUBLIC_SITE_NAME` | 🔴 只在 `docker run`，同 `NUXT_PUBLIC_SITE_URL` 的規則 | 覆寫 `nuxt.config.ts` 的 `site.name`（`og:site_name`／`<title>` 後綴／Schema.org `WebSite.name` 三處都跟著換，實測與 `SITE_URL` 同一套 priority-stack）。**藍鯨容器一律帶 `台中藍鯨`**，忘記帶就會悄悄顯示 `nuxt.config.ts` 裡的預設值 `TCRFC`（docs/13 §6 紀律 11） |
| `NUXT_PUBLIC_SITE_ENV` | `docker run` | `prelaunch`／`production`，目前只接住變數，三層防護（見 `docs/17-deployment.md` §10.4）留給 S0-9 之後接上 |
| `NUXT_PUBLIC_BLUE_WHALE_SITE_URL` | 選填，`docker run`（有內建 staging 預設值，不像 `SITE_URL` 一定要給） | 主站 06 單元（女子足球）外連藍鯨官網的按鈕網址，預設 `https://bw-stg.tcrfc.tw`。**S1-12d 收尾第二輪（2026-09-29）起降為備援值**：`womens/index.vue` 改以後端 `GET /api/v1/tcrfc/site-facts` 的 `blueWhaleSiteUrl` 為主要來源（後台 `I` 網站設定可維護），這個環境變數只在 API 打不到或該欄位尚未設定（`null`）時才生效，藍鯨正式網域定案後改後台設定值即可，不必再改這個環境變數或重新部署容器 |
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
4. `server/routes/llms.txt.ts` / `llms-en.txt.ts`（GEO-01，S1-12a 起內容改讀後台
   `GET /api/v1/{club}/seo/llms-content`，「代表頁清單」欄位空白時仍回退呼叫這支函式組出預設值，
   不得另開一份邏輯）；`robots.txt` 見 `server/routes/robots.txt.ts`（S1-12／S1-12b，取代
   `nuxt.config.ts` 原本 `@nuxtjs/robots` 的固定輸出，見下方「已知缺口」S1-12 區塊）

資料表本身（`shared/utils/site-units.ts`）目前只到「單元」層級的骨架設定檔，
不是最終資料來源——之後應該改讀後台維護的真實內容。

## 藍鯨品牌詞彙殘留檢查（S0-9n，獨立指令，不在 `npm run lint` 裡）

`npm run lint` 只驗 `club-copy.ts` 有沒有兩家都填值（`check-club-copy.mjs`），
**驗不到「填得對不對」也驗不到「該引用的地方有沒有去引用」**——`SiteHeader.vue`
04 學院 mega menu 主標籤正確用 `identity.academyLabelZh`，但子項目字面寫死「學院」，
藍鯨站因此長期印出自相矛盾的選單，`lint` 全程不會有任何反應（`docs/18-work-errors.md`
`E-42` 升級段、`STATUS.md` `S0-9n`）。

`scripts/check-club-brand-leak.mjs` 補這一塊：把藍鯨站**已經渲染出來的 SSR 輸出**
（不是原始碼、不是猜意圖）整份跟一份磐石專屬詞彙表比對。**刻意不掛進 `npm run lint`**
——這支檢查需要先把藍鯨站真的跑起來（連帶要後端 API 與資料庫），跟 `lint` 目前純靜態、
秒級跑完的性質不合，硬掛上去會讓 `lint` 變慢又依賴外部環境，重演 `E-34`「長期因環境
紅燈、真錯誤被當雜訊放過」那一類問題。理由、詞表怎麼挑、掃描範圍為什麼是整份 HTML
（含 `<head>`）不是只掃 `<body>`、棘輪（ratchet）機制怎麼運作，全部寫在腳本檔頭，
不在這裡重複。

**什麼時候手動跑**：藍鯨站每次要部署前、`BW-2`～`BW-8` 每完成一批頁面後。

```bash
# 先照上面「本機測試兩個 club」把 bw 容器跑起來（記得帶 NUXT_PUBLIC_SITE_NAME=台中藍鯨）
node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:3012
```

離開碼：任何非 `EXEMPT_PAGES` 例外的頁面命中詞表，或例外清單棘輪被違反（只能往下減）→ `1`。

### 藍鯨圖片來源檢查（`E-83`，2026-09-30）

詞彙檢查**看不見圖片**（`alt=""`、檔名 `nav-about.jpg` 都不含詞表用字）。
`scripts/check-club-image-leak.mjs` 補這一塊，與詞彙檢查共用 `scripts/lib/collect-routes.mjs` 的路由
（`/zh/`＋`/en/` 全部 200 路由）：

```bash
# bw 容器同樣要帶 NUXT_PUBLIC_SITE_NAME=台中藍鯨
node scripts/check-club-image-leak.mjs --base-url=http://127.0.0.1:3012 [--inventory]
```

- **掃描來源**：`<img src/srcset>`、`<source>`、`<video poster/src>`、`<link rel=preload as=image>`、icon／apple-touch-icon、
  inline `style` 與 `<style>`／外部樣式表的 `url(...)`、`og:image`／`twitter:image`、JSON-LD 的 `image`／`logo`／`thumbnailUrl`／`contentUrl`。
  每次執行先跑抽取器自我測試（樣本涵蓋以上所有型別），漏抓任何一種即 `exit 2`。
- **分類**：藍鯨素材（`/assets/brand/bw/`）→ 允許；`NEUTRAL_ALLOWED` 明列的中性素材（每筆附理由，**目前 0 筆**）→ 允許；
  其餘**一律視為磐石素材（不確定歸磐石）**→ 失敗。`--inventory` 印出全站不重複來源與出現頁數。
- **判斷「中性」前必須打開圖片看過**：厚底緩震機能襪 7 張商品照每張右上角都印有 TCRFC 標誌（BW-C1 曾誤稱「無隊徽通用配件」）。
- **盲區**：API 回傳的圖片網址（藍鯨自己的橫幅／新聞封面／球員照）與動態路由 `news/[slug]` 不在掃描範圍。
  `apps/api` 起來、藍鯨有自己的媒體後，來源網域不在允許清單會被判違規——屆時要**有意識地**把藍鯨媒體來源加進 `ALLOWED_PREFIXES`，不要放寬成通配。
- **共用元件**：`ClubHeroBg`（頁首背景，藍鯨輸出既有 `page-hero__bg--pending` 漸層）、`ClubImg`（一般圖，藍鯨輸出同比例佔位方塊）、
  `hasNewsCover(slug, club)`／`newsFallbackMarkSrc(club)`（`app/utils/news.ts`）。新增圖片到兩站共用位置請用這些或 `v-if="isTcrfc"`。

**修正前命中清單**（bw 容器實測，146 條路由、26 個不重複來源、23 個磐石；修正後 3 個來源全為藍鯨隊徽／圖示、0 違規）：

| 來源 | 頁面 |
|---|---|
| `nav-about/club/academy/programs/news/culture/partners.jpg`（磐石學員與球員） | **全部 146 頁**（導覽下拉，`SiteHeader.vue`） |
| `nav-about.jpg` | `about/*` 九頁 hero |
| `nav-news.jpg` | `news/{index,academy,camps-events,club,community,international,match,media,player-stories}` hero |
| `nav-partners.jpg`／`nav-culture.jpg` | `partners/`、`culture/` hero |
| `academy/life-05.jpg`（未成年學員）／`life-02.jpg` | `academy/` hero、`academy/join/` hero |
| `trencin-04.jpg`／`trencin-02.jpg` | `club/opportunities/`、`club/player-stories/` hero |
| `news-mcu.jpg`／`trencin-04.jpg`／`trencin-05.jpg`／`news-w20.jpg` | 首頁「四大支柱」圖卡 |
| `merch-socks-01`–`07.jpg`（每張印有 TCRFC 標誌） | `shop/`、`shop/cushioned-socks/`、`cart/`、`culture/merchandise/` |
| `partner-intl-01/02/03`（Hellas Verona、Rayo Alcobendas、Rot-Weiss Ahlen 隊徽） | `partners/our-partners/` |
| `tcrfc-mark-white.svg` | `club/first-team/player/`（例外頁範本） |
| `tcrfc-mark-black.svg`（無封面佔位） | `news/[slug]`、`NewsCard`、首頁新聞卡（動態，讀原始碼發現） |

**各頁處置**：頁首背景圖 27 頁改 `<ClubHeroBg>`（tcrfc 輸出不變，bw 為漸層佔位）；導覽下拉七張特色圖加 `v-if="isTcrfc"`（按鈕保留）；
首頁四大支柱圖 `v-if="isTcrfc"`（圖卡退為深色底＋scrim，並更正原註解「通用足球場景照」）；襪子商品照改 `<ClubImg>`；
國際夥伴隊徽在藍鯨改顯示「尚未公開」空格；新聞無封面佔位標誌藍鯨改用藍鯨隊徽、且藍鯨不走本地封面路徑；球員範本頁磐石標誌對藍鯨隱藏。
`og:image` 原本已是 `/assets/brand/bw/bw-crest-512.png`（`shared/utils/club.ts`），無須修改。

**驗證（2026-09-30，`apps/api` 未啟動）**：`npm run lint` 0 錯誤／393 警告（基準 395）；`npm run build`、`docker build -f apps/web/Dockerfile apps/web` 皆過；
本機 tcrfc／bw 兩容器：`check-club-image-leak.mjs`（bw）0 違規；`check-club-brand-leak.mjs`（bw）、`check-heading-structure.mjs`（兩站）、
`check-site-units-coverage.mjs`、`check-bw-units-citation.mjs`、`check-faq-schema-live.mjs`（兩站）皆通過；tcrfc 164 條路由的圖片來源與頁首背景元素數，
與修正前映像檔逐頁比對 0 差異；兩站 `X-Robots-Tag: noindex, nofollow` 仍在。**未驗證**：API 有資料時的圖片來源（見盲區）；兩站的瀏覽器視覺（藍鯨佔位方塊、頁首漸層版面）未截圖確認。

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

- ✅ **S1-12（H 搜尋與 AI 能見度）前台串接已完成（2026-09-25，含驗收退回後補做）**，詳見
  `apps/api/README.md`「S1-12」整節（含完整驗收紀錄與真實 HTML 輸出核對）：
  - `server/utils/sitemap-urls.ts` 改呼叫 `GET /api/v1/{club}/seo/sitemap-entries`（取代直接打
    `/news` 的舊寫法），會依後端的 `is_noindex`／`is_excluded_from_sitemap` 排除文章，
    `server/routes/sitemap.xml.ts` 補上 `<lastmod>`。
  - 新增 `server/middleware/redirects.ts`（Nitro 伺服器層中介軟體，不是 `app/middleware/`）：
    每個非靜態資源請求查一次公開的 301 轉址清單，命中就送 301。用伺服器層攔截是必要的——
    舊網址（例如 Wix 商店網址）在新站沒有對應頁面元件，Vue Router 連比對這一步都不會發生。
  - `app/app.vue` 新增追蹤碼腳本注入（GA4／GTM／Meta Pixel／LINE Tag），走既有的
    `/api/backend/{club}/...` 同源代理讀 `seo.settings`，個別 ID 未設定時整段不輸出。
  - ✅ **新增 `server/routes/robots.txt.ts`**（取代 `nuxt.config.ts` 原本 `@nuxtjs/robots` 的
    `disallow: ['/']`，改用 `enabled: false` 完全關閉該模組——理由跟下方 E-18 的 sitemap
    案例完全同一個模式）：只有 `NUXT_PUBLIC_SITE_ENV` **精確等於** `'production'` 時才輸出
    「允許索引＋後台 `seo.robots_custom_rules` 自訂規則＋Sitemap 參照」，任何其他值（未設定、
    拼錯、大小寫不符）一律回傳 `Disallow: /`——白名單判斷，不是黑名單（`!== 'prelaunch'`），
    確保漏設變數時落在封鎖側。**全站 `X-Robots-Tag` noindex 標頭完全沒有被觸碰**（該標頭無條件
    套用，不看 `siteEnv`，是否也要讓它跟著切換是 `docs/17-deployment.md` §10.4「上線前三層
    防護」的完整機制要決定的事）。已用「未設定」「`production`」「`Production`（刻意打錯）」
    三種情境實測。
  - ✅ **`app/pages/zh/news/[slug]/index.vue`**（目前唯一有動態內容可渲染 SEO 資料的公開頁面）
    新增 `og:title`／`og:description`／`og:image`（含尺寸與 alt）／`keywords`／
    `<meta name="robots">`（`isNoindex`）／`<link rel="canonical">`（`canonicalPath` 有值時），
    已用一篇真實文章的實際 SSR HTML 輸出逐一核對過。
- ✅ **S1-12a（`GEO-01` `llms.txt` 維護）／S1-12b（`GEO-02` AI 爬蟲授權）後端與前台串接已完成
  （2026-09-25）**，詳見 `apps/api/README.md`「S1-12a」「S1-12b」兩節：
  - `server/routes/llms.txt.ts`／`llms-en.txt.ts` 改讀 `GET /api/v1/{club}/seo/llms-content`
    的五個區塊（站點定位／代表頁清單／事實摘要／授權與引用方式／聯絡窗口，逐語系）。管理員
    任一區塊未填寫時，個別區塊回退到路由檔內建的預設文字（英文版另外多一層「英文空白時退回
    中文」），不是整份輸出失敗；`apps/api` 暫時連不上時整份回退到內建預設（跟既有
    `sitemap-urls.ts`／`robots.txt.ts` 同一種防禦性寫法）。「隨發布重產、不以人工改檔」的落實：
    這兩支路由每個請求都重新呼叫後端組字串，管理員儲存後下一次請求即反映，不需要另外部署。
  - `server/routes/robots.txt.ts` 的 `production` 分支擴充：改讀
    `GET /api/v1/{club}/seo/crawler-settings`（合併後的排除路徑：規劃書強制的路徑
    ∪ 後台自行再加的路徑），套用到 `User-agent: *`（全站對所有爬蟲一視同仁，理由是這些排除
    是個資防線不是純 SEO 設定，見 `docs/14-invariants.md`），並為後台設定的每個 AI 使用者代理
    輸出專屬區塊（允許＝`Allow: /` ＋ 同一份排除清單；拒絕＝整段 `Disallow: /`）。已用真實
    HTTP 請求驗證（見 `apps/api/README.md`「S1-12b」節的驗收紀錄），含 `tcrfc`／`bw` 兩站
    強制清單各自正確（`bw` 目前沒有對應的未成年學員照片頁面，不會誤套用 `tcrfc` 專屬那一條）。
- 🔄 **S1-12f（Schema 逐型別輸出第一批：`Organization`／`SportsTeam`／`Person`／`Article`／
  `BreadcrumbList`）程式碼完成（2026-09-25）**，詳見 `apps/api/README.md`「S1-12f」節：
  - **前台頁面盤點（本輪任務要求先做的事）**：`apps/web` 目前只有 6 頁真的呼叫 `apps/api`
    公開端點（`/api/backend/...`）——`schedule.vue`（賽程）、`news/index.vue` 與 5 個分類頁
    （新聞列表）、`news/[slug]/index.vue`（文章詳情）、`about/our-people.vue`（教練／團隊
    成員）、`academy/teams.vue`（球員／教練，不輸出 Person，見下）。其餘約 74 頁仍是 mockup 靜態搬遷頁（文案取自
    `shared/utils/club-copy.ts` 或直接手寫在樣板裡），**沒有對應的 DB 記錄可供 GEO-05「缺不
    缺」判斷**，比照既有 Sitemap／`llms.txt` 對「靜態頁不進清單」的判斷（見上方 S1-12 段落），
    本輪對純靜態頁**不輸出**任何新增型別的 JSON-LD，不擬造一份假的 `schemaEligible`。
  - **`Organization`**：新增 `app/composables/useSchemaOrgClub.ts` 的 `useOrganizationSchema()`，
    接上 `app/pages/zh/index.vue`（首頁）與 `app/pages/zh/about/index.vue`（關於頁）——規劃書
    只列型別清單沒有指定頁面，這兩頁是本輪判斷的候選位置（見任務指示「全站或首頁與關於頁」）。
    用 `useSchemaOrg`＋`defineOrganization`（有專用定義器，比照 `news/[slug]` 頁 `Article` 的
    既有寫法）。🔴 **現況會整段不輸出**：`clubs.logo_light_key` 目前沒有任何寫入路徑（種子資料
    與既有後台皆為 `null`），`schemaEligible` 恆為 `false`，見 `apps/api/README.md`「S1-12f」
    「已知現況」——這是 GEO-05 正確行為，不是接線有誤。
  - **`SportsTeam`**：`useSportsTeamSchema('D1')` 接上 `app/pages/zh/club/first-team/index.vue`
    （一線隊，本輪判斷比首頁更貼近球隊實體）。沒有專用定義器，比照 `schedule.vue` 對
    `SportsEvent` 的既有手刻 JSON-LD 做法直接用 `useHead`。現況同樣恆為不合格（`teams.hero_key`
    與回退用的 `clubs.logo_light_key` 皆無寫入路徑）。
  - **`Person`**：只在 `about/our-people.vue`（教練／顧問，**8 位真實資料，現況會真的輸出**）。
    用 `useSchemaOrg`＋`definePerson`，`image` 只在 `photoUrl` 有值（已同意肖像使用，S1-7a
    既有 fail-closed）時才帶。⛔ **`academy/teams.vue`（梯隊）刻意不輸出 Person**：梯隊球員是
    未成年學員，主站規劃書 GEO-02 把未成年素材列為個資防線、明文不得放寬，該路徑也已在
    `robots.txt` 對所有爬蟲排除（agent 原本加上，主 session 驗收時移除，見 `docs/14`）。
    🔴 **一線隊 28 位球員名單頁（`first-team/index.vue`）仍是 mockup 靜態版面，沒有呼叫
    `/players`**，一線隊球員的 Person 留給該頁串接 API 時一併補上（一線隊為成年球員，仍要走
    肖像同意與 `schemaEligible` 閘門）。
  - **`BreadcrumbList`**：只接上 `news/[slug]/index.vue`（`defineBreadcrumb`），用
    `ArticleDetailDto.BreadcrumbSchemaEligible`（標題與 slug）。**其餘 79 頁的麵包屑刻意不輸出**：
    本專案沒有頁面階層資料表（B1 頁面管理尚未接上前台動態路由），那些頁面的麵包屑是純手寫
    HTML，沒有對應的 DB 記錄可供「這一頁缺不缺標題／網址」判斷，比照 Sitemap「`Page` 待 B1
    動態路由落地後補」的既有先例，不在本輪擴大範圍。
  - **`Article`**：確認既有做法（S1-12c）已經是 `schemaEligible` 閘門，未改動。
  - `npm run lint`（0 錯誤）、`npm run build`、`docker build` 皆過。🔴 **無頭瀏覽器實走未完成，
    標記未驗證**：本機啟動 `apps/api` 依硬規則被擋就停，兩站首頁／球員頁／新聞頁的實際 HTML
    輸出、`X-Robots-Tag: noindex` 標頭是否仍在，這幾項本輪皆未驗證，需要在允許啟動本機
    `apps/api` 的環境下補做。
- ✅ **`/sitemap.xml` 已修好**（2026-09-22）。真正根因不是「動態來源偵測」——是
  `@nuxtjs/sitemap` 內建路由會把命中全站 `X-Robots-Tag: noindex` route rule 的網址
  整批排除，本站上線前必然全站 noindex（CLAUDE.md 第 5 條），所以每一筆都被排除。
  改法：`nuxt.config.ts` 設 `sitemap: { enabled: false }` 關閉該模組的內建路由，
  改由 `server/routes/sitemap.xml.ts` 自組 XML，資料來源抽到
  `server/utils/sitemap-urls.ts`（`server/api/__sitemap__/urls.ts` 保留、改呼叫同一支
  函式，仍可獨立 curl 驗證）。實測：tcrfc **93 筆**（10 單元＋83 篇新聞）、
  bw **8 筆**（正確排除 `06`／`11`，0 篇新聞不報錯）；兩站 `/sitemap.xml` 皆送出
  `X-Robots-Tag: noindex, nofollow`；`hreflang` 目前只有 `zh-Hant`／`x-default` 自我參照
  （en 頁面尚未搬遷，不虛構會 404 的 `/en/` alternate）。詳見
  [`docs/18-work-errors.md`](../../docs/18-work-errors.md) E-18。
- ✅ **schedule 頁的 `SportsEvent` JSON-LD 已動態化**（2026-09-22，GEO-08）。逐場檢查
  `matchOn`／`kickoff`／`homeAway`／`opponent`／`venue`／`competitionName` 六欄齊全才輸出
  （GEO-05：資料不足時不輸出該型別），用 `useHead` 直接組 `<script type="application/ld+json">`
  （比照 mockup 原始寫法，不透過 `useSchemaOrg`／`defineEvent`——後者沒有 `SportsEvent`
  專用定義器）。實測：tcrfc **21 筆**（全數齊全）、bw **6 筆**（21 場歷史賽果中 15 場缺
  `kickoff`／`homeAway`，個別跳過、不報錯）。⚠️ **附帶發現但不在本次範圍**：
  `app/utils/schedule.ts` 的 `mapMatchStatus()` 顯示文字對照表沒有 `'played'` 這個真實
  status 值（只對到 `'finished'`），bw 的已完成賽事在畫面上會被歸類成「未開始」——
  JSON-LD 沒有沿用這張表，是獨立寫的 `EVENT_STATUS_MAP`，不受影響，但畫面顯示本身仍是
  既有缺口，回報給下一個處理 schedule 頁顯示邏輯的人。
- 🔴 **`compare-dom.mjs` 全站重跑（2026-09-22）發現 13 頁有差異，比已記錄的基準（77/80 零
  差異，其餘 3 頁歸因 `news.json` intcup 分類）多出 10 頁**：`zh/index.html`（20 處）、
  `zh/about/`（1）、`zh/about/ecosystem/`（7）、`zh/about/milestones/`（2）、
  `zh/club/first-team/`（1）、`zh/join/`（1）、`zh/news/article/`（0，僅結構性差異）、
  `zh/news/community/`（3）。**本次任務只改 `schedule.vue`（僅 `<head>`）與 sitemap 相關的
  `server/` 檔案，這些頁面的 `.vue` 檔一個都沒碰過**（`git status` 可核對），`zh/schedule/`
  本身在這次比對裡是乾淨的。抽查幾筆差異，性質像是 **S0-9f（新聞逐篇網址）落地後的自然
  後果**：mockup 的首頁／新聞列表頁卡片連結原本是佔位符 `/zh/news/article/`，S0-9f 讓
  `apps/web` 正確改指向真實文章 slug，於是相對於「凍結不變的 `site/dist`」出現差異，但這
  代表**真實資料已經生效**，不一定是退步；也有像 `zh/about/`（動態俱樂部全名 vs mockup
  固定較短的文案）這類看起來與資料驅動相關的差異。**這批差異沒有被本次任務修正**（超出
  「只改 schedule／sitemap」的授權範圍，且修正需要改 `<main>` 內容），留給下一個處理
  S0-9f 後續或做全站 compare-dom 複查的人接手；命令：
  `node site/tools/compare-dom.mjs --expected site/dist --actual http://127.0.0.1:<port> --json`。
- 🔴 **新聞逐篇網址已做出來**（S0-9e，`/zh/news/{slug}/`），但有兩個跟著浮出的落差：
  ① `apps/api` 的 `ArticleDetailDto` 沒有 `updatedAt`／`dateModified` 可用的欄位——DB 的
  `articles.updated_at` 只在後台寫入端點當樂觀並行權杖，沒有經公開 API 輸出。文章詳情頁的
  Article Schema（GEO-08）因此只輸出 `datePublished`，不輸出 `dateModified`（沒有真實資料
  就不輸出，不拿發布時間頂替，見 `app/pages/zh/news/[slug]/index.vue` 檔頭說明）。
  ② **共用文章（`club_id` 為空）的 canonical 該掛哪一站尚未定案**（規劃書第 10 章第 38 點／
  `docs/05-i18n-seo.md` §4b①），文章詳情頁目前依 nuxt-seo-utils 預設行為（canonical 指向
  當前這一站自己），刻意不預先替共用文章決定要掛哪一站——83 篇種子資料目前沒有任何一篇
  `club_id` 為空，這個分支還沒有真實資料能驗證，見同一個檔案的檔頭說明。
- 🟡 **藍鯨的 favicon／apple-touch-icon／OG 圖不是正式設計稿**：`brand/blue-whale/` 只有隊徽
  點陣主檔（3299×3243 去背 PNG），沒有向量、沒有專屬的 favicon／OG 設計。本骨架用「裁切成正方形
  ＋ 縮放」從官方點陣主檔產生 `public/assets/brand/bw/{favicon-32,favicon-48,apple-touch-icon}.png`
  （純幾何操作，不是造標或描摹，`brand/blue-whale/README.md` 明列這是點陣主檔的允許用途之一），
  OG 圖直接重用 `bw-crest-512.png` 本身、沒有另外設計版面。**缺**：向量標誌主檔、專屬 OG 設計稿、
  印刷色票——見 `docs/13-blue-whale-site.md` §5 待確認事項第 2 項。
- ⬜ 只有 `/`（轉址）與 `/zh/`（首頁）兩個路由**真的有內容可看**；其餘導覽連結
  （`/zh/about/`、`/zh/club/`…）目前都是有效的 `<a href>` 但頁面不存在，會 404——
  這是刻意的（S0-9 完整搬遷前的預期狀態），`npm run lint` 的 `link-checker/valid-route`
  錯誤就是在提醒這件事，不是骨架本身的 bug。**S1-13 起這件事對 `/en/...` 也成立**：
  `/en/...` 路由本身已存在（見下方「多語系框架」），但只有 `/zh/...` 有真實頁面內容的
  那些 `/en/...` 孿生路由才會回 200（其餘同樣 404，跟 `/zh/...` 版本狀態一致）。
- ✅ **S1-13 多語系框架已完成**（2026-09-25），見下方「多語系框架（S1-13）」一節。
- ✅ `app/middleware/unit-gate.global.ts` 已用真實路由驗證（S1-13）：`bw` 容器
  `curl /en/womens/`／`curl /zh/womens/` 皆回 404（unit `06` 對 `bw`停用），`tcrfc`
  容器同兩條路徑皆回 200——證明 `definePageMeta({ unit })` 這個 meta 綁在「檔案」上，
  `pages:extend` 複製出來的 `/en/...` 路由（指向同一個 file）會拿到完全相同的 meta，
  不需要在複製時另外手動搬一份 unit／nav／bodyClass。

## 多語系框架（S1-13，2026-09-25）

**做法：不用 `@nuxtjs/i18n`，手刻一套輕量框架**——理由見下方「為什麼不用
`@nuxtjs/i18n`」。單一真實來源在 [`shared/utils/locale.ts`](shared/utils/locale.ts)
（`SUPPORTED_LOCALES`／`DEFAULT_LOCALE`／`HREFLANG_MAP`／`resolveLocaleFromPath()`／
`localizePath()`），新增語系只改這一個檔案。

**URL 結構**：`nuxt.config.ts` 的 `hooks['pages:extend']` 會把每一個 `/zh/...` 頁面
自動複製出一個 `/en/...` 孿生路由，兩者指向**同一個 `.vue` 檔案**——不必手動複製 80
個檔案，也不會有兩份路由各自維護、彼此漏改的風險（比照 `docs/13-blue-whale-site.md`
§6 紀律 3「單元開關只有一個真實來源」延伸到語系）。`definePageMeta()` 的 meta 綁在
「檔案」上不是「路由項目」上，因此 `unit`／`nav`／`bodyClass` 等既有 meta 對 `/en/...`
路由一樣正確生效（已用 `unit-gate` 對 `bw` 容器實測，見上方「已知缺口」）。根路徑 `/`
（`app/pages/index.vue`）不複製，它的職責是依 `Accept-Language` 轉去 `/zh/` 或
`/en/`（見 `app/middleware/redirect-root.ts`；規劃書與 `docs/05` 都沒規定站根轉址
要不要看瀏覽器語言，這是本輪的判斷，預設值仍是 zh）。

**`<html lang>`／`og:locale`／canonical 大小寫**：`app/plugins/site-locale.ts` 把
「目前路由算出來的語系」餵給 `nuxt-site-config` 的 `currentLocale`（用該套件自己
`addImportsDir` 出來的公開 composable `updateSiteConfig()`，不是繞過模組的 hack），
`@nuxtjs/seo` 的 `nuxt-seo-utils` 子模組本來就設計成讀這個值決定 `<html lang>` 等
三件事，只是沒裝 `@nuxtjs/i18n` 時沒有人餵——這是 `docs/18-work-errors.md` E-17 的
後續發展，E-17 當時（只有 zh 頁面）建議的「寫死在 `nuxt.config.ts` 的靜態值最穩」
已經不適用，該檔案的 `app.head.htmlAttrs.lang` 已移除。

**hreflang**：`@nuxtjs/seo` 沒裝 `@nuxtjs/i18n` 不會自動產生 hreflang alternate，
`app/layouts/default.vue` 手刻（每頁一份 zh／en／x-default 三條，x-default 固定指
向 zh 版本）；`server/routes/sitemap.xml.ts` 也已更新，改為每個候選網址各輸出
zh／en 兩筆 `<url>`，每筆都帶完整三條 hreflang alternate。

**語系切換器**：mockup 原本就有三處「繁中｜EN」的靜態按鈕（`SiteHeader.vue` 兩處、
`SiteFooter.vue` 一處），S1-13 把它們接上 `useLocale().switchTo()`，停留在目前這一頁
換語系（不跳回首頁，`docs/05-i18n-seo.md` §1「切換行為」）。DOM／class 一律不動，
只加 `@click` 與把靜態 `aria-current="true"` 改成依 `locale` 動態算。

**Fallback**：`docs/05-i18n-seo.md` §1 規則是「未翻譯內容顯示繁中，並標示本頁尚無
此語系版本」，`app/components/LocaleFallbackNotice.vue` 落實這件事——`en` 路由預設
一律顯示這則提示（因為 S1-13 當下沒有任何一頁真的翻譯完成），頁面本身的中文內容原樣
顯示在提示下方。真的做完英文翻譯的頁面用 `definePageMeta({ enReady: true })` 關掉
提示，這個旗標與既有 `unit`／`nav`／`bodyClass` 同一種機制。

**API 呼叫的 `lang` 參數**：`apps/api` 對 `?lang=zh|en` 已有完整逐欄位回退機制（見
`apps/api/README.md`），問題只在前台過去把它寫死成 `'zh'`。S1-13 把用到這個參數的
呼叫點（`schedule.vue`、`news/index.vue`＋5 個分類頁、`news/[slug]/index.vue`、
`academy/teams.vue`）全部改成 `useLocale().locale.value`，`about/our-people.vue`
維持既有「同時抓 zh 與 en 兩種姓名」設計不變（那是既有的雙語顯示邏輯，不是本輪的
lang 參數問題）。

**內部連結**：`SiteHeader.vue`／`SiteFooter.vue`（每頁共用，含語系切換器本身）、
`NewsCard.vue`／`NewsCategoryTabs.vue`（07 單元多頁共用）、**首頁**
`zh/index.vue`（含 `shared/utils/club-copy.ts` 裡 `ctaPrimaryHref`／`ctaSecondaryHref`／
`pillars[].href`／`ctaTrio[].href` 這幾個「裸 `/zh/...` 路徑」資料欄位的消費端）與
`about/ecosystem.vue` 的內部連結，已一律改用 `useLocale().lp()` 換算成目前語系版本。
✅ **S1-13 缺口①已補完（2026-09-25）**：其餘 68 個純靜態頁面＋`MembershipBenefits.vue`
元件（共 69 個檔案、366 處）的頁內硬編碼 `/zh/...` 連結，已用同一招（`href="/zh/...`
→ `:href="lp('/zh/...')"` 的正規表達式替換＋補 `const { lp } = useLocale()`）批次處理完成。
另外 `club-copy.ts` 裡 3 處帶內嵌連結標記、以 `v-html` 渲染的文案欄位（`HISTORY_HERO.tcrfc.lede`、
`JOIN_CONTACT_HERO.*.lede`，消費端是 `about/history.vue`／`join/contact/index.vue`）不能直接
套用同一招——這些欄位是純資料常數，不是元件、不能呼叫 `useLocale()`——改為新增
`shared/utils/locale.ts` 的 `localizeHtmlLinks(html, locale)`，在消費端頁面用 `computed` 於
`v-html` 渲染前把字串裡的 `/zh/...` 換算成目前語系。**刻意保留 1 處硬編碼**：
`app/pages/index.vue`（根路徑 `/`）樣板裡的 `<NuxtLink to="/zh/">` 回退連結——這個頁面
本身不參與 `pages:extend` 的 `/zh/`／`/en/` 孿生路由複製（它的職責是依 `Accept-Language`
轉址，見上方說明），沒有語系上下文，`lp()` 在此恆等於 no-op，轉換沒有實質意義。
🔴 **防呆（E-63 同一種錯誤形狀）**：批次改樣板＋補解構這個動作本身在 S1-13 第一輪就出過包
（`SiteHeader.vue`／`SiteFooter.vue` 漏解構 `lp`，見 `docs/18-work-errors.md` E-63），這輪新增
`scripts/check-undefined-template-refs.mjs` 掛進 `npm run lint`：跑 `npx nuxi typecheck`（不是
裸 `vue-tsc --noEmit`——裸的解不開 Nuxt 的自動匯入型別，會把 `useLocale`／`lp` 本身都判成
「Cannot find name」，失去訊號區分度），只挑訊息含 `ComponentInternalInstance` 的 `TS2339`
（樣板存取到元件 proxy 型別裡不存在的屬性，即「用了沒宣告的識別字」在 Vue SFC 型別檢查下的
樣子）與 `TS2304`／`TS2552`，這三種訊號在本專案既有型別債（`useFetch().items` 回傳 `{}`
等）裡零筆出現，不需要維護 baseline／allowlist。已用故意刪掉一個頁面的
`const { lp } = useLocale()` 實測會變紅、補回後變綠。**這批型別債清完、能把完整
`nuxi typecheck` 接進 `lint` 之後，這支腳本可以退役**（比照 `scripts/check-club-copy.mjs`
檔頭同一句話）。

**為什麼不用 `@nuxtjs/i18n`**：現有 80 頁全部是 `app/pages/zh/...` 檔案路徑（不是
`@nuxtjs/i18n` 慣用的「檔案名不含語系前綴、由模組產生 `/zh/`／`/en/` 兩份路由」那種
結構），改用該模組等於要把 80 個頁面檔案搬家、重寫所有內部連結／`NuxtLink`、且要
重新驗證 S1-12 系列已經很精細的 sitemap／robots／llms.txt／canonical 串接（那些串接
已經各自繞過 `@nuxtjs/sitemap`／`@nuxtjs/robots` 的內建邏輯一次，見 E-18）——風險與
成本都高於「只加一個 `pages:extend` hook＋一個 plugin＋一個 composable」。**代價**：
`@nuxtjs/i18n` 原生的翻譯字串管理（`$t()`／訊息檔）沒有一起拿到，本輪也沒有引入替代
方案——目前只有版型層級的少數字串（語系切換器、提示訊息）需要雙語，用字面文字直接
寫在元件裡就夠，尚未到需要訊息字典的規模。之後若英文內容大量上線、版型文字量變大，
可以重新評估。

## S1-14（01 首頁九大區塊／02 關於台中磐石，2026-09-29）

**02 關於台中磐石（2.1–2.8）本次確認為既有工作已完成**：`app/pages/zh/about/` 8 個頁面
（`index`／`our-story`／`vision-mission`／`philosophy`／`our-people`／`governance`／
`ecosystem`／`history`／`milestones`）在更早的 S0-9（靜態頁搬遷）與 S1-12f（Person Schema）
就已經逐頁完成，`our-people.vue` 更已接上真實 Staff API。規劃書 §3.2 本身**沒有列「資料來源」
欄**（跟 §3.1 首頁九大區塊不同），這 7 個長文頁維持既有的靜態富文本（文案在
`shared/utils/club-copy.ts`，雙語齊全）——B1 頁面管理雖有對應的公開 API
（`GET /api/v1/{club}/pages/{slug}`），但 `pages` 資料表目前沒有這些 slug 的種子資料，
串了也只會拿到 404，故本輪不動這 7 頁，只重新驗證（見下方驗收紀錄）。

**01 首頁九大區塊本輪重點是把既有靜態骨架（S0-9 搬遷、S1-13 語系化）接上規劃書 §3.1
「資料來源」欄點名的既有公開 API**，對應 `db/seed` 的 `home_sections` 九個代碼
（`hero`／`core_values`／`ecosystem_nav`／`upcoming_match`／`recent_fixtures`／
`latest_news`／`partner_logos`／`shop_entry`／`bottom_cta`）：

| 區塊 | 規劃書資料來源 | 本輪狀態 |
|---|---|---|
| Hero 主視覺 | 後台 Banner 管理 | 🟡 部分真資料：`GET /api/backend/{club}/banners` 已接上，用來覆蓋主要 CTA 文字／連結（`primaryCta`）。輪播**圖片**仍是既有 3 張真實照片（tcrfc）／純色回退（bw）——`banners` 資料表目前 0 筆種子資料，且 `HomeRepository.ListBannersAsync` 沒有把 `imageKey` 解析成完整網址（見 `docs/18-work-errors.md` `E-64`），前端拿到鍵值也無法正確組圖，故暫不消費 |
| 五大核心價值 | 後台設定 | ⬜ 靜態（僅 tcrfc 顯示，藍鯨無對等的自訂品牌框架，見既有頁內註解）。目前開關由 `home_sections.core_values` 控制顯示/隱藏，內容本身沒有對應的「網站設定」公開端點可接 |
| 四大體系導覽卡（四大支柱） | 靜態模組＋可換圖文 | ⬜ 靜態（`shared/utils/club-copy.ts` `HOME_PILLARS`），開關已接 `home_sections.ecosystem_nav` |
| 最新賽事區（下一場倒數＋最近比賽結果） | 賽事管理模組 | 🟢 真資料：`GET /api/backend/{club}/schedule`，取一線隊（D1）依日期排序的最新一筆 `played`／前一筆 `played`／最早一筆 `matchOn ≥ 今天` 的 `scheduled`。開關 `home_sections.upcoming_match` |
| 近期賽事（未來 30 天摘要＋隊別快切） | 行事曆模組 | 🟢 真資料：同一支 `schedule` API，篩出非 D1、未來 30 天內的 `scheduled` 場次。目前種子資料只有一線隊有賽程，梯隊面板會顯示既有的「尚未公開發布」占位文字，邏輯已就緒、有資料會自動出現。開關 `home_sections.recent_fixtures` |
| 最新消息 | 新聞模組 | 🟢 真資料：`GET /api/backend/{club}/news`，精選（`isFeatured`）優先、不足用最新發布日期補滿，固定 5 格（feature／sml×2／wide×2），版位樣式沿用既有 CSS class。開關 `home_sections.latest_news`；藍鯨新聞 0 篇時陣列自然為空，區塊自動不顯示（不再靠 `isTcrfc` 判斷） |
| 贊助夥伴 Logo 牆 | 夥伴模組 | ⬜ 靜態占位（10 個空白 tile，兩俱樂部皆無真實贊助商資料可上）。`apps/api` 目前**沒有對應的公開讀取端點**（`Features` 底下沒有 `Partners`／`Sponsors` 公開 endpoints，只有 EF 實體），無法接。開關已接 `home_sections.partner_logos` |
| 官方商店入口 | 商店模組 S1 | ⬜ 靜態連結（僅 tcrfc，商店模組後端尚未開發，`STATUS.md` 站內商店列仍是「沒有後端」）。開關 `home_sections.shop_entry` |
| 底部 CTA 帶 | CTA 元件 | ⬜ 靜態（`HOME_CTA_TRIO`），開關 `home_sections.bottom_cta` |

**新增檔案**：`app/composables/useHomeSections.ts`（讀 `home-sections` API、提供
`isSectionEnabled(code)`，API 失敗或空陣列時 fail-open＝全部視為啟用，理由見檔頭註解）。

**已知範圍縮減**（非本輪判斷有誤，是任務內明確排除或需要 `backend-engineer` 配合）：

1. **只做區塊開關，不做動態排序**——`home_sections.sortOrder` 目前恰好與樣板既有 DOM
   順序一致（見 `useHomeSections.ts` 檔頭），本輪沒有把九個區塊改寫成 `v-for` 動態排序，
   後台如果之後真的調整排序，畫面不會跟著動。
2. ✅ **（S1-14 缺口①，2026-09-29 補完）Hero 輪播圖片已串接**——`apps/api` 已於
   `E-64` 補上 `ImageUrl`／`VideoUrl`，`app/pages/zh/index.vue` 改讀這兩個欄位，見下方
   「S1-14 缺口補完」節。
3. **贊助夥伴／官方商店兩區塊沒有可接的公開 API**——不是本輪漏做，是後端範圍本來就還沒開放
   （`Partner`／`Sponsor`／商店模組目前只有 EF 實體或完全未開發）。
4. **一線隊球員橫幅（roster-strip）維持原樣**——這個區塊本身不在規劃書 §3.1 九大區塊清單內
   （既有 mockup 多出來的內容），本輪不動、也不受 `home_sections` 開關控制。
5. **Hero 內的迷你新聞卡（`hero__news`，兩張）維持靜態**——這是 Hero 區塊內的裝飾性連結，
   不是首頁「最新消息」正式區塊，本輪沒有一併接上 `homeNews`，維持既有硬編碼內容。
6. ✅ **（S1-14 缺口②，2026-09-29 已裁決並補完）藍鯨賽事行事曆區塊已顯示**——藍鯨規劃書
   §1.3「與主站同一套網站，只有配色不同」，整段隱藏是過度保守的既有判斷。藍鯨一線隊
   （`BW1`）21 筆真實歷史賽果會顯示在「最新戰績／上一場」，「下一場」卡自然落回既有
   v-else 占位文案（不是新造分支）。見下方「S1-14 缺口補完」節。
7. **台中磐石（tcrfc）目前在 `matches` 表只有 21 筆 `scheduled`（全部是未來賽程），沒有任何
   `played` 紀錄**，所以「最新戰績 LATEST RESULT」「上一場 PREVIOUS」兩張卡在真實資料下
   會顯示「尚無已完賽數據」占位文字（見下方驗收紀錄的真實 curl 結果），跟舊版 mockup 寫死的
   「3:0 銘傳大學」等具體比分不同——那些具體比分是否為真實已發生的比賽結果、要不要回填進
   `matches` 種子資料，是內容／資料盤點的問題，不在本次前端任務範圍內，留給主 session／
   `backend-engineer` 判斷。

**驗收紀錄（2026-09-29）**：

```
npm run lint     # 0 errors, 534 warnings（與改動前完全相同，未新增）
npm run build    # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機用同一份映像檔起兩個容器（`NUXT_PUBLIC_CLUB=tcrfc` port 13001／`NUXT_PUBLIC_CLUB=bw`
port 13002，**`apps/api` 未啟動**，依派工規則不自行啟動、不碰密碼），對 `/zh/`／`/en/` 的
首頁與 `/zh/about/`／`/en/about/` 各自 `curl`：

- 8 個網址（tcrfc×2／bw×2 × 首頁／關於頁）全部 `200`。
- `<html lang>` 四組合皆正確（`zh-Hant`／`en`），`hreflang` 三條齊全，
  `X-Robots-Tag: noindex, nofollow` 四組合皆在。
- **`apps/api` 不可達時的優雅降級已確認**（`useFetch` 失敗回傳 `null`，樣板一律用
  `?? []`／三元運算接住）：tcrfc 首頁「最新戰績」「下一場賽程」「近期賽事」三處皆正確落回
  占位文字，「最新消息」區塊因 `homeNews` 為空陣列而整段不渲染，藍鯨首頁 Hero 落回既有
  純色區塊、賽事區塊維持隱藏，皆無 500 或未捕捉例外。
- `node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:13002`：
  `exit 0`，保護清單 13 頁（含 `/zh/`／`/zh/about/` 與其餘 7 個關於子頁）全數乾淨，
  棘輪未被違反。

🔴 **未驗證項目**（因為 `apps/api` 未啟動，只能驗證請求參數正確，不能驗證真實資料的
畫面呈現）：

- `banners`／`schedule`／`news` 三支 API 在有真實資料時，首頁實際渲染的內容是否正確
  （欄位對應、日期換算、精選排序等邏輯只做過程式碼審視與 apps/api 原始碼比對，沒有用
  真實資料庫跑過一次）。
- 瀏覽器端互動（Hero 輪播、賽事分頁按鈕、`primaryCta` 的實際點擊行為）未做無頭瀏覽器模擬，
  只驗證 SSR 輸出的 HTML 結構。
- `/en/` 版本的首頁／關於頁內容仍以中文為主（沿用 S1-13 既定範圍：版型與框架元素雙語，
  頁面本文尚未整頁翻譯），本輪新增的三處占位文字（「尚無已完賽數據」等）同樣只有中文，
  與既有頁面內文的雙語完成度一致，不是新的缺口。

## S1-14 缺口補完（Hero 輪播圖片／影片、藍鯨賽事區，2026-09-29，`frontend-architect`）

延續上方 S1-14，補完當時留下的兩個缺口。**改動只在 `app/pages/zh/index.vue`**（同時套用到
`/en/index.vue`，兩者是同一份檔案，見 S1-13 的 `pages:extend` 孿生路由機制）。

### 缺口①：Hero 輪播圖片／影片串接

`apps/api` 已於 `E-64`（見 `docs/18-work-errors.md`）修正 `HomeRepository.ListBannersAsync`，
`banners` API 新增 `imageUrl`／`videoUrl`（含 `mediaType`／`imageAlt`／`imageWidth`／
`imageHeight`）。本輪把 Hero 輪播改成資料驅動：

- 新增 `heroBanners`（只收「真的有完整網址可用」的輪播：`image` 模式要有 `imageUrl`；
  `video` 模式要海報圖＋影片網址皆有，缺一律整則跳過——不對缺欄位的資料猜網址，寧可不顯示
  也不要顯示壞圖）與 `heroSlides`（`heroBanners` 有值就用，否則 tcrfc 落回既有 3 張靜態照片、
  bw 落回既有純色區塊）兩個 computed。
- 樣板改用 `v-for` 渲染輪播 `<li>`／`<video>`｜`<img>`／`.hero__dot`，`aria-label`／
  `aria-hidden` 依 `heroSlides.length` 動態算，不再寫死「共 3 張」。
- `video` 模式輸出 `<video :poster="imageUrl" muted loop playsinline autoplay>`＋
  `<source :src="videoUrl" type="video/mp4">`（格式與海報圖規則見 `docs/17-deployment.md`
  §6「Hero 輪播影片上傳」）。**已知範圍縮減**：既有的「方塊拆解」轉場動畫（`animateSwap`）
  用 `querySelector('img')` 抓出場那張的像素做馬賽克裁切，`video` 輪播沒有 `<img>`，這裡沿用
  既有的既有防呆分支（找不到 `<img>` 就 `swapInstant` 直接切換），不是新造規則，也沒有為
  `video` 另外做等價的馬賽克轉場——`banners` 資料表兩俱樂部皆 0 筆種子資料，目前無法用真實
  影片輪播驗證這個分支，之後有真實影片資料時應補一次實機驗收。
- `total`（輪播張數）從寫死的 `const total = 3` 改成 `computed(() => heroSlides.value.length)`，
  `goTo`／`startAutoplay`／`onMounted` 的掛載條件一併從「是否為 tcrfc」改成「是否有可顯示的
  輪播素材」——這讓機制本身不再綁死俱樂部別，之後任一俱樂部的 `banners` 有真實資料就會自動
  生效，不用再改程式碼。
- `banners` 資料表目前兩俱樂部皆 0 筆種子資料（`db/seed`），**現況下行為與改動前肉眼不可見的
  差異只有「輪播張數改成動態計算」這件事本身**——3 張靜態照片與純色回退都還在，只是判斷依據
  從硬編碼改成資料驅動。

### 缺口②：藍鯨賽事行事曆區塊

藍鯨官網規劃書 §1.3 明文「與主站同一套網站，只有配色不同」，S0-9 當時把整個「賽事行事曆」
區塊對藍鯨用 `v-if="isTcrfc"` 整段隱藏，理由是「藍鯨未來 12 個月賽程完全沒有」——但沒有考慮
藍鯨一線隊（`BW1`）其實有 21 筆真實**歷史**賽果（2023 木蘭聯賽／2025 總統盃，`status='played'`）
可以顯示。本輪修正：

- 移除 `<section>` 上的 `isTcrfc &&` 閘門，改成單純看 `home_sections` 開關。
- **修正一個連帶發現的既有錯誤**：`d1Sorted`／`otherTeamUpcoming` 兩個 computed 原本寫死
  `m.teamCode === 'D1'`——這對 bw 資料一定比對不到任何一筆（藍鯨一線隊代號是 `BW1`，不是
  `D1`，見 `docs/14-invariants.md`「`BW1` 不是第二個 `D1`」），等於就算拿掉 `isTcrfc` 閘門，
  藍鯨也只會看到「尚無已完賽數據」等占位文字，不會顯示那 21 筆真實戰績。新增
  `firstTeamCode`（`clubKey==='bw' ? 'BW1' : 'D1'`）取代寫死值。
- 隊伍等級 chips：藍鯨青年隊只有 `BW-U15`／`BW-U12`（見 `db/seed`），沒有 `U14`，`U14` chip
  改成 `v-if="isTcrfc"` 只在磐石顯示；一線隊 chip 的 `data-team` 屬性改用 `firstTeamCode`（純
  裝飾用途，樣板與 CSS 都沒有消費這個屬性值，修正只是讓標記本身不誤導）。
- 「下一場」卡沒有另外處理——藍鯨沒有任何未來賽程，`nextFixture` 恆為 `null`，既有的
  `v-else` 分支本來就會落到「下一場賽程尚未公告，敬請關注後續公告。」這個占位文案，符合
  「顯示戰績、下一場用既有占位文案」的裁決結果，不是新造的特殊分支。
- 「近期賽事」（`otherTeamUpcoming`，非一線隊未來 30 天賽程）：藍鯨的 `BW-U15`／`BW-U12`
  在 `matches` 表沒有任何紀錄，這個子區塊會落到既有的「青訓梯隊賽程尚未公開發布」占位文字，
  跟磐石梯隊目前的狀態一致，不是新缺口。

### 驗收紀錄（2026-09-29）

```
npm run lint     # 0 errors, 532 warnings（較改動前 534 筆減少，因 3 顆重複的輪播按鈕警告
                 # 收斂成 1 顆 v-for 樣板；未新增任何警告或錯誤）
npm run build    # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機用同一份映像檔起兩個容器（`NUXT_PUBLIC_CLUB=tcrfc`／`NUXT_PUBLIC_CLUB=bw`，**`apps/api`
未啟動**，依派工規則不自行啟動、不碰密碼）：

- `/zh/`／`/en/` 首頁在 tcrfc、bw 兩容器共 4 個網址皆 `200`，`X-Robots-Tag: noindex, nofollow`
  四組合皆在。
- tcrfc 首頁 SSR 輸出仍含 `hero-01.jpg`／`hero-02.jpg`／`hero-03.jpg`（`apps/api` 不可達時正確
  落回既有 3 張靜態照片，不是壞圖）；bw 首頁仍輸出 `hero__media--pending`（純色回退，未誤植
  磐石照片）。
- bw 首頁 SSR 輸出含「賽事行事曆」標題與「一線隊 First Team」chip（區塊本身確認已顯示；
  `apps/api` 未啟動、`scheduleData` 抓不到資料，兩俱樂部這輪測試都只看得到占位文字，`BW1`
  真實 21 筆戰績的實際渲染結果**未驗證**，見下方「未驗證項目」）。
- bw 首頁 SSR 輸出不含 `data-team="U14"`；tcrfc 首頁仍含（chip 顯示邏輯正確）。
- `node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:<bw 容器 port>`：**必須
  同時帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`**（`docs/13-blue-whale-site.md` §6 紀律 11a）——
  第一次少帶這個環境變數時，13 頁保護清單全數因 SEO 模組回退用的預設站名含 `TCRFC` 而失敗
  （`exit 1`）；用同一份映像檔、不帶這個變數重跑基準（`git stash` 回到本輪改動前的程式碼）
  得到完全相同的 13 頁失敗清單與次數，確認**這不是本輪改動造成的迴歸，純粹是本輪測試一開始
  漏帶環境變數**——帶對之後兩份程式碼（改動前／改動後）都是 `exit 0`，13 頁保護清單全數
  乾淨，棘輪未被違反。

🔴 **未驗證項目**（因為 `apps/api` 未啟動）：

- `banners` 有真實輪播資料（尤其是 `video` 模式）時的實際渲染結果、`animateSwap` 對 `video`
  輪播的 `swapInstant` 回退分支，皆只做過程式碼審視，沒有用真實資料跑過。
- 藍鯨 `BW1` 21 筆真實歷史賽果的「最新戰績／上一場」卡在真實資料下的實際畫面（比分、對手
  名稱、`resultMetaLine` 日期格式），只驗證了程式碼邏輯（`firstTeamCode` 過濾條件、
  `clubScore`／`opponentScore` 主客場換算）與區塊本身確實會渲染，未接上真實資料庫核對。

## S1-15（03.1 一線隊／04 學院 4.1・4.2・4.7／05 課程 5.1・5.2，2026-09-29）

主站規劃書 §3.3（03.1）／§3.4（4.1／4.2／4.7）／§3.5（5.1／5.2）。改動檔案：
`app/pages/zh/club/first-team/index.vue`、`app/pages/zh/academy/{overview,teams,join}.vue`、
`app/pages/zh/programs/{childrens-training,summer-camp}/index.vue`、
`shared/utils/{club-copy.ts,units.ts}`、新增 `app/composables/useFaqEmbed.ts`、
`scripts/check-club-brand-leak.mjs`（`PROTECTED_PAGES` 新增兩頁）。

### 各頁資料來源

| 頁面 | 區塊 | 來源 |
|---|---|---|
| 03.1 一線隊 | 球員名單／教練團／賽程表 | 🟢 真資料：`GET /{club}/players\|staff\|schedule?team={D1\|BW1}`。**磐石與藍鯨共用同一套樣板**——改動前藍鯨這三區塊被 `v-if="isTcrfc"` 整段隱藏（理由寫的是「藍鯨 0 素材」），但 `db/seed` 其實已經替 `BW1` 種了 21 筆真實賽果與球員／教練名單，是本輪發現並修正的既有缺口（不是新迴歸），見 `docs/18-work-errors.md` 沒有另開一筆（不是「犯錯」，是修正轉述過期，已在檔頭註解說明） |
| 03.1 | 成績與積分榜 | 🟡 成績（已完賽場次）改資料驅動；**積分榜維持靜態說明**——只有 `Features/AdminStandings` 後台端點，沒有公開讀取端點可接 |
| 03.1 | 榮譽時間軸 | ⬜ 靜態、`isTcrfc` 專屬——沒有公開 API 可查「俱樂部歷史榮譽」（比照 `about/milestones.vue` 現況），且藍鯨無可查證的逐年獎盃資料可引用 |
| 4.1 學院總覽 | 定位段落／數據亮點 | ⬜ 靜態，改為讀 `club-copy.ts` 的 `ACADEMY_OVERVIEW_*`／`ACADEMY_POSITIONING`（原本零俱樂部分支，字面寫死磐石內容，藍鯨版逐句節錄自 `content/blue-whale/squad/youth-teams.md`）。「數據亮點」兩俱樂部皆無對應公開 API |
| 4.2 學院隊伍 | 名單／教練／賽程 | 🔵 真實 API（`players`／`staff`／`schedule`，不帶 `team` 篩選、前端依隊代碼過濾），分頁籤改依 `club-copy.ts` 的 `ACADEMY_TEAM_TABS` 動態產生：磐石 U15／U14／U12／其他年齡層（磐石學院球隊 `Team` 主檔尚未建立，故仍是空清單）；藍鯨只有 U15／U12 兩個真實 `Team`（`BW-U15`／`BW-U12`），沒有 U14、沒有「其他年齡層」——改動前分頁籤寫死磐石的三個代碼，藍鯨容器會用錯誤的隊代碼查詢，是本輪修正的既有缺口 |
| 4.7 加入學院 | 常見問題快捷區塊（G-12） | 🟢 真資料：`GET /{club}/faqs/embeds/academy_admission`（S1-7a 已種掛載點字典，本輪首次消費）。**本頁對藍鯨整頁 404**（見下方「單元開關」） |
| 5.1 兒童足球訓練 | 週期課表 | 🟢 真資料：`GET /{club}/programs?type=children_training` 取第一筆同類型項目，再打 `GET /{club}/programs/{slug}` 取 `sessions[]`。現況 `programs` 表 0 筆種子資料，故仍顯示既有「準備中」提示 |
| 5.1／5.2 | 常見問題快捷區塊 | 🟢 真資料：`GET /{club}/faqs/embeds/program_detail`（同一掛載點兩頁共用，`db/seed` 的 `FAQ_EMBED_SLOTS` 字典本來就標註「課程詳情頁（5.x 各課程）」） |
| 5.2 夏令營 | 早鳥價／剩餘名額／梯次 | 🟢 真資料：`GET /{club}/programs?type=summer_camp` 同上邏輯，取第一個 `open`／`waitlist` 梯次；無資料時維持既有「待公告」 |
| 訓練地點（5.1）／教練團・對象內容（5.2） | 已知事實／空白區塊 | ⬜ 維持既有靜態或空白，不臆造——5.1 場地資訊本身已是已知正確事實，沒有理由用目前為空的 API 結果覆蓋；5.2 空白段落沒有對應內容來源，不是本輪要補的文案缺口 |

`program_type` 值域（`children_training`／`summer_camp`／`winter_camp`／`specialist_training`／
`school_community`）取自既有 `apps/admin/src/types/program.ts`（與
`AdminProgramsRepository.AllowedProgramTypes` 一致），非本輪自訂。

### 單元開關（藍鯨）

`shared/utils/units.ts` 的 `BLUE_WHALE_DISABLED_UNITS` 新增 `'4.7'`／`'5.1'`／`'5.2'`：

- **4.7（加入學院）**：藍鯨規劃書 §3.4「04 青年隊沿用主站 04 的梯隊版型，但不沿用招生與
  課程報名架構」——整頁就是磐石的招生流程與費用表，明文排除。
- **5.1／5.2（兒童足球訓練／夏令營）**：藍鯨自己的「05 推廣活動」是完全不同的活動集合
  （社區與學校推廣、足球節、藍鯨盃），不是磐石課程頁換配色就能沿用的內容。這兩頁改動前
  對兩俱樂部**零分支**（0 筆 `isTcrfc`／`clubKey` 判斷）——代表藍鯨容器過去會直接顯示磐石
  課程內容，這是本輪盤點時發現的既有缺口，關閉後改回誠實的 404（`check-club-brand-leak.mjs`
  對 404 有既有豁免）。

`academy/{overview,teams,join}.vue` 的 `definePageMeta unit` 同時從粗粒度 `'04'` 改為細粒度
`'4.1'`／`'4.2'`／`'4.7'`，讓 `units.ts` 能單獨關閉 4.7 而不影響 4.1／4.2（比照 `05` 系列頁面
本來就是逐頁 `'5.1'`–`'5.5'` 的既有慣例）。

⚠️ **範圍縮減（明確排除，非本輪判斷有誤）**：`academy/{coaches,curriculum,life,pathway}.vue`
（4.3／4.4／4.5／4.6）**不在本輪範圍**，仍是粗粒度 `unit: '04'`、零俱樂部分支，藍鯨容器
現況仍會顯示磐石內容（`check-club-brand-leak.mjs` 的「進度計」可見）。`programs/{winter-camp,
specialist,school-community}/index.vue`（5.3–5.5）同樣未關閉、零分支，理由同上——STATUS.md
把它們排在 `S2-10`，本輪不擴大範圍。

> 🔴🔴 **已修正（BW-C1，2026-09-29）**：上面把 `5.1`／`5.2` 加進
> `BLUE_WHALE_DISABLED_UNITS`（用 404 關閉整頁）違反藍鯨規劃書 §1.3 總則「例外只有四項
> 單元取捨」——「藍鯨沒有對應的活動集合」是內容缺漏，不是關閉整頁的理由。`5.1`／`5.2`
> 已於 BW-C1 重開（版型不變，內容改為藍鯨真實課程班別或誠實空狀態），詳見本檔「BW-C1」節
> 與 [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-76`。

### 驗收紀錄（2026-09-29）

```
npm run lint    # 0 errors, 527 warnings（低於既有基準 532，未新增）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機用同一份映像檔起兩個容器（`NUXT_PUBLIC_CLUB=tcrfc` port 13001／`NUXT_PUBLIC_CLUB=bw`
port 13002 且帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`，**`apps/api` 未啟動**，依派工規則不自行
啟動、不碰密碼），對六個改動頁的 `/zh/`／`/en/` 版本（共 12 條網址）逐一 `curl`：

- 8 條（03.1、4.1、4.2 兩俱樂部 × 兩語系）全部 `200`，`<html lang>` 正確（`zh-Hant`／`en`）、
  hreflang 三條齊全（`zh-Hant`／`en`／`x-default`）、`X-Robots-Tag: noindex, nofollow` 皆在。
- 4 條（4.7、5.1、5.2 對藍鯨的 `/zh/`／`/en/`，共 6 條中的 4 條非 tcrfc 部分）**依設計回
  `404`**（單元關閉），tcrfc 對應頁維持 `200`。
- **`apps/api` 不可達時的優雅降級已確認**：03.1 球員／教練／賽程三區塊落回「準備中」文字，
  4.2 各梯隊面板同樣落回「準備中」，5.1／5.2 的 Program／FAQ 區塊落回「待公告」／
  「收錄中」提示，皆無 500 或未捕捉例外。
- `node scripts/check-match-status.mjs`：本輪一度未過（03.1／4.2 直接比對 `m.status ===
  'played'`／`'scheduled'` 字面值，未走 `mapMatchStatus()`），已修正並轉綠，記於
  `docs/18-work-errors.md` `E-65`。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs
  --base-url=http://127.0.0.1:13002`：`exit 0`，棘輪通過；`/zh/academy/overview/`／
  `/zh/academy/teams/`（含對應 `/en/`）實測 0 筆磐石／`TCRFC`／學院詞彙命中，已新增進
  `PROTECTED_PAGES`（13 → 15 頁）。
- tcrfc 容器對 4.2 分頁籤實測：4 個分頁籤（U15／U14／U12／其他年齡層）；bw 容器實測：
  2 個分頁籤（U15／U12），無「其他年齡層」面板——符合 `ACADEMY_TEAM_TABS` 設計。

🔴 **未驗證項目**（因為 `apps/api` 未啟動，只能驗證請求參數與欄位對應，不能驗證真實資料的
畫面呈現）：

- `players`／`staff`／`schedule`／`programs`／`faqs/embeds` 五支 API 在有真實資料時的實際
  渲染結果（欄位對應、`portrait_consent_status` 白名單、梯次早鳥價計算等）只做過程式碼與
  DTO 比對，沒有用真實資料庫跑過。
- 03.1 對藍鯨（`BW1`）真實 21 筆賽果、真實球員／教練名單在畫面上的實際內容（球員照片是否
  正確依肖像同意顯示、賽事對手名稱是否恰好含有詞表字樣造成偶發性 brand-leak 假警報）未做
  真實資料庫驗證，只做過靜態程式碼審視與「API 不可達時不會壞」的驗證。
- 瀏覽器端互動（4.2 分頁籤鍵盤導覽、ARIA focus 行為）未做無頭瀏覽器模擬，只驗證 SSR 輸出的
  HTML 結構與既有分頁邏輯改寫是否忠於原邏輯。

### 規格疑點（列出，未自行決定）

1. **`programs` 表現況 0 筆種子資料**：5.1／5.2 串接的 Program／FAQ 快捷區塊在目前資料庫
   狀態下全部落回既有的靜態占位文字，實際效果要等後台建立課程項目與梯次後才看得出來。
2. **4.3／4.4／4.5／4.6（學院發展路徑／訓練課程／教練團／學生生活）與 5.3–5.5（冬令營／
   專項訓練／校園社區）仍是粗粒度 `unit: '04'`／`'5.x'` 且零俱樂部分支**，藍鯨容器目前仍會
   顯示磐石專屬內容——不在 `S1-15` 範圍（STATUS.md 排在之後的工作），列出供下一輪處理時
   參考本輪的做法（細粒度 `unit` ＋ `club-copy.ts` 分支 ＋ `units.ts` 視情況關閉）。
3. **03.1 球員卡不再顯示外籍球員的獨立英文姓名列**：真實 `PlayerDto.name` 只回傳依語系
   解析後的單一姓名欄位，不像舊版靜態內容那樣額外提供一行英文姓名——這是真實資料的欄位
   形狀限制，不是本輪遺漏；如果客戶需要雙語姓名同時顯示，需要後端額外開放球員雙語姓名欄位。
4. **教練頭銜的英文標籤（如「總教練 Head Coach」）目前只有中文**：`site/src/data/coaches-
   d1.json` 只有 `role_zh`，沒有 `role_en`，API 的 `Title` 欄位因此只有中文——舊版靜態內容
   手動補的英文頭銜（"Head Coach" 等）是搬遷時自行加上的文案，不是資料庫既有欄位，真實
   API 接上後自然消失，屬於資料完整度問題，不是本輪的接線錯誤。

## S1-16（06 女子足球＝藍鯨官網入口頁，2026-09-29，`frontend-architect`）

主站規劃書 §3.6：本頁是台中藍鯨女足官網的入口頁，藍鯨球隊資料建於本資料庫
（`club_id=TCBW`），但名單／賽程／積分榜一律由藍鯨官網呈現，本頁不重複建置。

只改一個檔案本體 ＋ 一個共用設定：`app/pages/zh/womens/index.vue`（`/en/womens/` 由
S1-13 的 `pages:extend` 孿生路由機制自動產生，不需要另外新增檔案）、
`nuxt.config.ts`（新增 `runtimeConfig.public.blueWhaleSiteUrl`）。

### 各區塊資料來源

| 區塊 | 來源 |
|---|---|
| ①主視覺與標題 | ⬜ 既有靜態文字，本輪未改動 |
| ②台中藍鯨女子隊介紹文 | 🟢 原檔 `<h2>` 底下是空段落（無文字）——本輪改讀 `club-copy.ts` 既有的 `OUR_STORY_BODY_BW`（已核實、逐字節錄自 `content/blue-whale/club-profile.md` 的既有文案，不是新寫文案） |
| ②事實面板（成立／聯賽／主場／梯隊體系） | 🟢 `site-facts.ts` `getSiteFacts('bw')`（GEO-03 事實單一來源，S1-12d 已建立），未改動其資料結構 |
| ②圖片 | 🟢 `getClubAssets('bw').headerMark.src`（`/assets/brand/bw/bw-crest-512.png`，既有已發布的隊徽點陣主檔衍生圖，`brand/blue-whale/README.md` 明列「網頁圖示」為可用情境）。**沒有藍鯨球隊合影或訓練照可用**（客戶尚未提供，肖像同意狀態未知，比照 `club/first-team/index.vue` 既有做法不臆造），故只放隊徽，不是完整的「圖片藝廊」 |
| ~~③台中藍鯨一線隊近期賽果~~ | ❌ **已移除（2026-09-29，主 session）**：規劃書 §3.6「不含功能」明文排除「藍鯨賽程與比賽結果」，一律由藍鯨官網呈現。派工指示誤把它列為可接的範例，見 `docs/18-work-errors.md` `E-67` |
| ④前往藍鯨官網按鈕 | 🔴 原檔寫死舊站網址 `https://www.tcbw2014.com/`（既有 Google Sites，規劃書明文「新站上線後 301 轉址」，不是永久連結目標）。改讀 `useRuntimeConfig().public.blueWhaleSiteUrl`，預設 staging 網域 `https://bw-stg.tcrfc.tw`，比照 `NUXT_PUBLIC_SITE_URL`／`NUXT_PUBLIC_SITE_NAME` 既有「staging 預設值＋容器啟動時可覆寫」做法（環境變數表新增一列）。正式網域定案後改 env 即可，不必動程式碼或重 build |
| ⑤底部 CTA | ⬜ 既有靜態內容，本輪未改動 |

**顏色**：沿用既有 `--brand-aa` 等既有 CSS 變數（`brand/blue-whale/README.md` 已定案的隊徽取樣色），
未新增任何色值。

**單元開關**：`06` 早已在 `shared/utils/units.ts` 的 `BLUE_WHALE_DISABLED_UNITS` 清單中（bw 容器
本頁恆 404），本輪未新增改動——確認既有機制仍然生效，不是本輪新做的開關。

### 驗收紀錄（2026-09-29）

```
npm run lint    # 0 errors, 527 warnings（等於既有基準上限，未超過）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機用同一份映像檔起兩個容器（`NUXT_PUBLIC_CLUB=tcrfc` port 13101／`NUXT_PUBLIC_CLUB=bw`
port 13102 且帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`，**`apps/api` 未啟動**，依派工規則不自行
啟動、不碰密碼）：

- `curl http://127.0.0.1:13101/zh/womens/` → `200`；`curl http://127.0.0.1:13101/en/womens/` → `200`。
- `curl http://127.0.0.1:13102/zh/womens/` → `404`；`curl http://127.0.0.1:13102/en/womens/` → `404`（單元開關生效）。
- 兩者皆有 `X-Robots-Tag: noindex, nofollow`。
- `node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:13101` 與
  `--base-url=http://127.0.0.1:13102`：**H1 唯一、標題不跳階皆 0 違規**（156 條路由，含本頁）。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs
  --base-url=http://127.0.0.1:13102`：**通過**，「保護清單（15 頁）全數乾淨，棘輪未被違反」
  （本頁對 bw 是 404，不在保護清單頁面範圍內，此檢查主要確認本輪改動沒有連帶弄壞其他頁）。
- ③近期賽果區塊已移除（`E-67`），本頁不再呼叫任何 API；移除後主 session 重跑 `npm run lint`（0 錯誤、527 警告）、`npm run build`，並以本機 `node .output/server/index.mjs` 實測 `/zh/womens/`、`/en/womens/` 皆 200、畫面無「近期賽果」字樣、`check-heading-structure.mjs` 通過。
- 實測 SSR 輸出：`href="https://bw-stg.tcrfc.tw"`（外連按鈕）、
  `src="/assets/brand/bw/bw-crest-512.png"`（隊徽圖）、`台中藍鯨女子足球隊`（事實面板隊名）
  皆正確出現在 `/zh/womens/` 的渲染結果中。

🔴 **未驗證項目**（因為 `apps/api` 未啟動）：

- 藍鯨正式網域定案後，`NUXT_PUBLIC_BLUE_WHALE_SITE_URL` 覆寫是否確實生效未實測（機制與
  `NUXT_PUBLIC_SITE_URL`／`NUXT_PUBLIC_SITE_NAME` 同一套 Nuxt `runtimeConfig.public`
  env 覆寫，S0-9b／docs/13 §6 紀律 11 已對後兩者實測過，本鍵理論上同機制，但未獨立重跑
  一次覆寫測試）。

### 缺內容清單

- **無藍鯨球隊合影／訓練／賽事照片可用**：②區塊目前只能放隊徽，不是規劃書建議內容
  「圖片藝廊」的完整呈現。需要客戶提供已核實肖像同意的藍鯨照片素材。
- **無影音嵌入內容**：規劃書建議區塊之一，目前沒有可核實來源的藍鯨影片可嵌入。

### 規格疑點（列出，未自行決定）

1. **「圖片藝廊／影音嵌入」是規劃書建議內容區塊，不是強制要求**——本頁目前只有隊徽可用，
   已列入「缺內容清單」，未自行決定要不要用其他素材（如既有的 `brand/svg/` 磐石標誌）
   湊版面充數，因為那會誤導成「藍鯨有這張圖」。
2. ~~③近期賽果~~：已裁決移除——§3.6「不含功能」明文排除藍鯨賽果（`E-67`）。

## S1-12d（`GEO-03`／`GEO-04` 事實單一來源與雙重呈現，2026-09-29，`frontend-architect`）

主站規劃書 §7 `GEO-03`（成立年份、主場與場地、梯隊組成、所屬聯賽、聯絡方式全站只有一個
維護處）／`GEO-04`（結構化資料與明文同時輸出、數值一致）。

### 🔴 事實盤點結論：後端目前沒有任何欄位承載這五類事實

先盤點「這五類事實現在存在哪裡」——`apps/api/Features/Clubs/ClubDto.cs` 只有名稱／網域／
標誌／品牌色／`SchemaEligible`（S1-12f），沒有成立年份／主場／聯賽／聯絡方式欄位；
`Setting`（`Features/AdminSeo/AdminSeoSettingsRepository.cs`）與 `Venue` 兩張表都**沒有
對外公開的端點**（只有後台管理路由）。也就是說，「後台 `I` 網站設定」目前在資料庫層是
存在的（`Setting`／`Venue` 兩張表），但**沒有一條路徑能讓前台讀到裡面的值**——依任務指示
不得自行改資料庫綱要或新增後端端點，本輪把**前台暫定的單一維護處**做成新檔案
[`shared/utils/site-facts.ts`](shared/utils/site-facts.ts)，並在該檔案檔頭與下方「已知缺口」
清楚標明這是暫定方案，等後台補上對應欄位與公開端點後要整批改回 `useFetch`。

### 事實盤點對照表

| 事實 | 原本在哪裡（散落狀況） | 現在的單一來源 |
|---|---|---|
| 成立年份／首季頭銜 | `club-copy.ts` 內部 8+ 處字面值重複，另外 `about/history.vue`／`about/our-story.vue`／`charity/commitment.vue` 各自再寫一份 | `SITE_FACTS[club].foundedYear`／`.foundedDisplayZh`／`.foundingDateIso`／`.foundingTitleZh` |
| 主場（場地名稱＋地址） | 「西屯足球場」9 處、地址全文「台中市北屯區崇平路二段景谷巷 11 弄 41 號」4 處獨立打字（`join/contact`／`join/location`／`programs/childrens-training` 三個頁面＋ `club-copy.ts`），藍鯨兩座場地名稱另外在 `club-copy.ts` 重複 3 處 | `SITE_FACTS[club].venues`／`getPrimaryVenue(club)` |
| 所屬聯賽 | 「企業甲級聯賽」9 處、「台灣木蘭足球聯賽」6 處（不含歷史時間軸逐年記錄，那批刻意不動，見下方「刻意不動的範圍」） | `SITE_FACTS[club].league.nameZh`／`.nameEn`／`.shortNameZh` |
| 梯隊組成（年齡層代碼） | 「U15／U14／U12」等代碼組合字面值在 11 個頁面 ＋ `club-copy.ts` 4 處各自重打（含 2 處代碼順序不一致的既有缺陷：`U12／U14／U15` vs `U15／U14／U12`） | `SITE_FACTS[club].squadCodes` ＋ `academyTeamCodesLabel(club, separator?)`；`ACADEMY_TEAM_TABS`（S1-15）改為由 `squadCodes` 衍生，不再自己重打一份年齡層清單 |
| 聯絡方式（地址／電話／營業時間） | 地址同「主場」一列；電話與營業時間**目前沒有任何頁面填過值**（`join/contact/index.vue` 只有標籤沒有內容），不是遺漏而是核實資料未到位 | `SITE_FACTS[club].contact`（`address` 直接引用 `venues[0].address`；`phone`／`hours` 現況皆為 `null`，不放佔位假資料） |

### 刻意不動的範圍（不是漏做，是不同種類的事實）

- **歷史時間軸資料**：`club-copy.ts` 的 `HISTORY_YEARS_BW`（藍鯨逐年沿革）、`TIMELINE_BW`
  （藍鯨隊史逐項頭銜，含每年「參加第 X 屆台灣木蘭足球聯賽」「隊史第 X 座台灣木蘭聯賽
  冠軍」共 25 筆）、`app/pages/zh/about/milestones.vue`（磐石里程碑時間軸）——這些是
  「哪一年發生了什麼事」的既有核實歷史紀錄，跟「我們現在的主場／聯賽是什麼」是不同的
  事實類型，統一改寫成單一來源反而會抹掉逐年的真實差異，故不動。
- **`club/index.vue`／`club/opportunities/index.vue`／`academy/{index,life,pathway,coaches,join}.vue`
  等單元 `03`／`04` 粗粒度頁面**：這些頁面目前對兩俱樂部零分支（藍鯨容器會直接顯示磐石
  內容），是 S1-15 就已經記錄、留給後續工作（STATUS.md 排在之後）的既有缺口，不在本輪
  範圍。本輪只把這些頁面裡「西屯足球場」「U15／U14／U12」等字面值改成引用
  `site-facts.ts`（避免事實本身分裂成兩份），**沒有**額外加上 `clubKey`／`isTcrfc` 分支
  把整頁改成雙俱樂部——那是更大範圍的頁面重構，超出 `GEO-03` 的任務邊界。
- **`join/location/index.vue`／`join/academy/index.vue`**：同上，單元 `10-location`／
  `10.2` 目前也是零俱樂部分支的既有缺口（藍鯨訪客會看到磐石的主場卡片與表單選項），
  本輪同樣只換掉事實字面值的來源，不擴大範圍做整頁雙俱樂部化。

### 真的修正的既有缺口（在盤點過程中發現，順手修）

`app/pages/zh/schedule.vue`（單元 `13`，S1-15 已確認**兩俱樂部共用同一套資料驅動樣板**）
改動前 SEO 標題／描述、ICS 事件標題、「梯隊介紹」CTA 卡、官方公告文字**不論 `club` 一律
寫死磐石的俱樂部名稱、「企業甲級聯賽」與「U15／U14／U12」**——藍鯨容器訪客會看到錯誤的
俱樂部名稱與聯賽名稱。改為讀 `getClubAssets(club).nameZh`／`getSiteFacts(club).league.nameZh`／
`academyTeamCodesLabel(club)`；SEO 描述的賽程場次數改讀 `matches.value.length`（原本寫死
「21 場」，藍鯨的真實賽程筆數不是 21）。**未動**：`state.team` 篩選分頁與 `teamLabels`
仍是寫死的 `D1`／`U15`／`U14`／`U12`（藍鯨真實隊代碼是 `BW1`／`BW-U15`／`BW-U12`，沒有
`U14`）——這是比 GEO-03 更大範圍的「賽事行事曆逐隊代碼」架構問題，屬於 `STATUS.md` `S1-19`
（尚未開工），本輪不擴大範圍處理，列在此提醒下一輪。

### JSON-LD 雙重呈現（GEO-04）

[`app/composables/useSchemaOrgClub.ts`](app/composables/useSchemaOrgClub.ts) 新增：

- `useOrganizationSchema()`：`defineOrganization()` 新增 `foundingDate`（`SITE_FACTS[club].foundingDateIso`，
  `null` 時不輸出，tcrfc 現況即為此情形，確切成立月日未核實不臆測）與 `address`（`PostalAddress`，
  `SITE_FACTS[club].contact.address` 為 `null` 時不輸出，bw 現況即為此情形）。
- `useSportsTeamSchema()`：手刻 JSON-LD 新增 `memberOf`（`SportsOrganization`，`league.nameZh`）
  與 `location`（`Place`，`getPrimaryVenue(club).nameZh`）。

兩者都跟頁面明文（各頁的 `SITE_FACTS.xxx`／`academyTeamCodesLabel()` 用法）讀同一份
`site-facts.ts`，滿足 GEO-04「結構化資料與明文同時呈現、數值一致」——不是分別維護兩份
再手動核對，是同一個值餵給兩種輸出，不一致在結構上不可能發生。🔴 **未能實機驗證**：
Organization／SportsTeam 的 `schemaEligible` 現況恆為 `false`（S1-12f 已知現況，隊徽物件鍵
無寫入路徑），本輪未啟動 `apps/api`，無法用真實 HTTP 確認 `foundingDate`／`address`／
`memberOf`／`location` 四個新欄位在合格時的實際輸出內容，只做到程式碼審視與型別檢查
（`npx nuxi typecheck` 對本輪新增／改動檔案無錯誤）。

### 防呆：`scripts/check-fact-single-source.mjs`

新增並掛進 `npm run lint`（`lint:fact-single-source`）。掃描 `app/`／`shared/` 底下的
`.vue`／`.ts` 檔案，禁止 11 條已核實的事實字面值（地址全文、三個場地名稱、兩個聯賽全名、
「全國乙級聯賽冠軍」、四種「U15／U14／U12」組合的字面拼法）出現在 `site-facts.ts` 與
歷史時間軸資料檔案（見上方「刻意不動的範圍」）以外的地方。用改動前的程式碼驗證過紅燈
（會抓到當時散落的字面值），改完後綠燈；也在改動過程中實際攔下 3 筆——`schedule.vue`
自己新增的說明註解不慎重複打了一次「企業甲級聯賽」字面值、`academy/teams.vue` 與
`index.vue` 兩處純開發註解命中（已加入允許清單，見腳本內註解）。

### 已知缺口（回報，不在本輪範圍）

1. ~~後端沒有這五類事實的欄位與公開端點~~：**已由 `backend-engineer` 補上**（同一天，
   見 `apps/api/README.md`「S1-12d」節），前台已於「S1-12d 收尾」（見下方新增小節）
   整批改讀 API，`site-facts.ts` 降級為備援快照。
2. **`schedule.vue` 的逐隊代碼篩選（`state.team`／`teamLabels`）仍寫死磐石代碼**：見上方
   「真的修正的既有缺口」，這是比本輪任務範圍更大的架構問題，留給 `S1-19`。
3. **多個單元 `03`／`04`／`10-location`／`10.2` 頁面仍是零俱樂部分支的既有缺口**：見上方
   「刻意不動的範圍」，本輪只換掉事實來源，沒有加上整頁雙俱樂部分支。
4. **無頭瀏覽器與真實 API 的 JSON-LD 輸出未驗證**：見上方「JSON-LD 雙重呈現」小節，
   延續 S1-12a／b／c／f 同一個環境限制（本機啟動 `apps/api` 需要在指令列具現化資料庫
   密碼，被 session 自動模式安全防護擋下，依硬規則被擋就停）。**S1-12d 收尾同樣未解除
   這個限制**，見下方新增小節「未驗證項目」。

### S1-12d 收尾——改讀後端公開端點（2026-09-29，`frontend-architect`）

後端已交付 `GET /api/v1/{club}/site-facts?lang=zh|en`（見 `apps/api/README.md`「S1-12d」節）。
本輪把「已知缺口」第 1 點清掉：所有讀 `getSiteFacts()`／`SITE_FACTS`／`getPrimaryVenue()`／
`academyTeamCodesLabel()` 的頁面（`about/history.vue`／`about/our-story.vue`／
`charity/commitment.vue`／`club/index.vue`／`club/first-team/index.vue`／
`club/opportunities/index.vue`／`join/academy/index.vue`／`join/contact/index.vue`／
`join/location/index.vue`／`programs/childrens-training/index.vue`／`womens/index.vue`／
`academy/{coaches,index,join,life,overview,pathway}.vue`／`schedule.vue`，共 17 個頁面檔案）
與 `useSchemaOrgClub.ts` 的 JSON-LD，改讀新增的
[`app/composables/useSiteFacts.ts`](app/composables/useSiteFacts.ts)。

**設計**：`useSiteFacts(club)` 固定同時打兩次 API（`lang=zh`／`lang=en`，比照既有頁面「同一
段落需要 `nameZh` 與 `nameEn` 並列」的既有慣例，例如 `club/opportunities/index.vue` 的外籍
球員英文段落），合併成與舊版 `SiteFacts` 型別（`shared/utils/site-facts.ts`）相容的形狀，
回傳 `{ facts, primaryVenue, academyLabel(separator?) }` 三個值。呼叫端從
`SITE_FACTS.tcrfc.foundedYear` 這類靜態屬性存取，改成 `tcrfcFacts.foundedYear`（`useSiteFacts`
回傳的 `facts` 是 `ComputedRef`，屬性名稱完全不變，只是換了資料來源），改動量因此壓到最小。

**`site-facts.ts` 的去留（任務要求二選一，已選）**：**保留**，角色改為「`useSiteFacts()` 的
降級備援快照」——理由是它同時還有第二個消費者：`shared/utils/club-copy.ts`（見下方
「已知限制」）。整個刪除會讓 club-copy.ts 立即編譯失敗，且會失去 API 打不到時的degrade
內容來源。`getSiteFacts`／`getPrimaryVenue`／`academyTeamCodesLabel` 三個既有匯出函式**保留
不動**（club-copy.ts 與 `useSiteFacts.ts` 內部的降級路徑都還在用），但檔頭註解已更新，
明文要求「新頁面一律呼叫 `useSiteFacts(club)`，不要再直接讀本檔」。

**降級行為**：`useSiteFacts()` 的 `mergeSiteFacts()` 只要 `lang=zh` 那次 `useFetch` 失敗
（`data` 為 `null`，例如 `apps/api` 未啟動或連線被拒），整組回傳值就退回
`SITE_FACTS[club]` 靜態快照，不做「部分欄位打 API、部分欄位退回快照」的混合狀態。這比照
`useHomeSections`／`useFaqEmbed` 既有 fail-open 慣例，**不出 500**——本機用 `apps/api`
未啟動的容器實測過（見下方「驗收」），頁面仍是 200，畫面顯示快照內容，只有 server log
會印 `FetchError: connect ECONNREFUSED`（不會傳到瀏覽器）。

**JSON-LD（`useSchemaOrgClub.ts`）**：`useOrganizationSchema()`／`useSportsTeamSchema()`
改讀 `useSiteFacts(club).facts.value`／`.primaryVenue.value`。**中文全名的處理方式**：
不需要額外呼叫——`useSiteFacts()` 本來就一律同時抓 zh／en 兩次，`facts.xxxZh` 欄位固定
來自 `lang=zh` 那次呼叫，不受目前頁面（`/en/`）語系影響，天生滿足
`apps/api/README.md`「S1-12d」節「回應形狀」建議的「JSON-LD 需要不受 `lang` 影響的中文
全名時，另外用 `?lang=zh` 呼叫一次即可」——因為本來就有這一次呼叫，不必再加一次。

**藍鯨場地正式名稱／地址以後端為準**：`apps/api/README.md`「S1-12d」節「種子資料」段
記錄後端已用既有 `Venue` 列（豐原體育場官方全名、太原／豐原兩座場地的真實地址）接上
`home_venue_ids`，跟 `site-facts.ts` 快照裡「台中豐原體育場」簡稱與 `address: null` 不同。
本輪**沒有覆寫或過濾 API 回傳值**——`useSiteFacts()` 的 `mergeSiteFacts()` 只要 zh 那次
成功就直接使用 `zh.venues`／`zh.contact.address`，不比對快照、不做「跟舊資料不一致就
隱藏」的特殊處理，前台會如實顯示後端資料（本機未啟動 `apps/api`，無法用真實回應核對，
見下方「未驗證項目」）。

**已知限制（回報，留給下一輪決定）**：`shared/utils/club-copy.ts`（1044 行，近 40 處引用
`SITE_FACTS`）**沒有改接** `useSiteFacts()`——它是模組層級常數，在 `import` 當下同步組出
一大批 SEO／Hero 文案物件，沒有 Nuxt 元件的請求生命週期可以掛非同步抓取。要接上 API
得把整個檔案改成吃 `facts` 參數的工廠函式，並改寫 15 個以上消費頁面（`about/history.vue`
等本輪已改頁面之外，還有 `index.vue`／`club/first-team/player/index.vue` 等未列在本輪
呼叫清單裡、但 import `club-copy.ts` 的頁面）的呼叫方式，是遠超「一個 composable＋17 個
直接呼叫頁面」這一輪邊界的重構。已在 `club-copy.ts`／`site-facts.ts` 兩處檔頭加註明文
記錄，也同步進 `lint:fact-single-source` 的檔頭說明（見下方「防呆」）。

**防呆調整**：`scripts/check-fact-single-source.mjs` 檔頭更新為反映新架構（真正的單一
維護處是後端資料庫，`site-facts.ts` 降級為備援快照與 `club-copy.ts` 例外），**功能邏輯
未改**——`ALLOWED_FILES`／`FORBIDDEN_LITERALS` 兩份清單維持原樣，因為本輪沒有在允許清單
以外的地方新增任何字面值（新增的 `useSiteFacts.ts` 一度在 JSDoc 舉例寫了「U15／U14／U12」
被本腳本攔下，已改寫成不含字面值的敘述，見 `docs/18-work-errors.md` `E-68`）。

**驗收（2026-09-29）**：
```
npm run lint    # 0 errors, 527 warnings（等於既有基準上限，未超過）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```
本機用同一份映像檔起兩個容器（`tcrfc` port 13101／`bw` port 13102 帶
`NUXT_PUBLIC_SITE_NAME=台中藍鯨`，`apps/api` 未啟動，依派工規則不自行啟動、不碰密碼）：

- 17 個改動頁面 × `/zh/`／`/en/`（`womens`／`charity/commitment` 僅 tcrfc、`schedule` 兩站
  皆測）全數 200，`docker logs` 只看到預期的 `ECONNREFUSED` 伺服器端錯誤紀錄，**沒有** 500
  回應或未捕捉例外導致的頁面崩潰。
- 降級內容實際核對：`/zh/about/history/`（`2024`／`全國乙級聯賽冠軍`）、`/zh/club/`
  （`西屯足球場`）、`/zh/schedule/`（tcrfc 顯示`企業甲級聯賽`、bw 顯示`台灣木蘭足球聯賽`）、
  `/zh/womens/`（tcrfc 容器讀 bw 快照：`台灣木蘭足球聯賽`／`太原足球場`／`豐原體育場`）
  皆正確顯示 `site-facts.ts` 快照內容（用 `curl | grep` 核對字串實際出現在渲染結果中）。
- `node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:13101`（156 條路由）
  與 `--base-url=http://127.0.0.1:13102`（138 條路由，bw 停用單元後略少）：**H1 唯一、
  標題不跳階皆 0 違規**。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs
  --base-url=http://127.0.0.1:13102`：**保護清單（15 頁）全數乾淨，棘輪未被違反**。
  `/zh/club/opportunities/` 在 bw 容器仍同時顯示磐石與藍鯨兩個聯賽名稱——這是「已知限制」
  提到的既有零分支缺口（`club/opportunities/index.vue` 本輪只換掉事實來源沒有加俱樂部
  分支），不是本輪造成的新迴歸，也不在保護清單頁面範圍內。
- `/zh/womens/`／`/zh/charity/commitment/` 在 bw 容器皆正確 404（單元開關不受本輪影響）。

🔴 **未驗證項目**（因為 `apps/api` 未啟動，依派工規則不自行啟動、不碰密碼）：

- API 實際成功時的資料是否正確渲染（本輪只驗證了「API 打不到→退回快照」這條路徑，沒有
  驗證「API 打得到→顯示真實資料」這條路徑，包含藍鯨場地真實地址／官方全名、
  `useSchemaOrgClub.ts` 的 `foundingDate`／`address`／`memberOf`／`location` 四個 JSON-LD
  欄位在 `schemaEligible=true` 時的實際輸出內容）。
- `apps/api/README.md`「S1-12d」節記錄的公開端點快取（`IQueryCache`，TTL 300 秒）與後台寫入
  不主動 invalidate 的行為，前台未做對應測試（本輪不涉及後台編輯畫面）。

**後端待補事項（不在本輪範圍，回報）**：
- ~~主站規劃書 §3.6「藍鯨官網網址於後台 `I` 網站設定可維護」——後端已於 2026-09-29 補上 `blueWhaleSiteUrl`……`womens/index.vue` 仍讀 `useRuntimeConfig().public.blueWhaleSiteUrl`，待改接~~：已於 S1-12d 收尾第二輪改接，見下方新增小節。
- ~~`apps/admin` 尚無 `I` 模組編輯畫面~~：已於 2026-09-29 完成（`apps/admin/README.md`「I：網站設定」）。後端同日補上 `blueWhaleSiteUrl` 欄位與 `GET /admin/{club}/venues` 場地清單，前台 `womens/index.vue` 與後台挑選場地待改接（前台部分已於 S1-12d 收尾第二輪完成）。

### S1-12d 收尾第二輪——`club-copy.ts` 改為工廠函式、`womens/index.vue` 改讀後端 `blueWhaleSiteUrl`（2026-09-29，`frontend-architect`）

延續上方「S1-12d 收尾」節記錄的「已知限制」：`shared/utils/club-copy.ts`（1044 行，近 40 處
引用 `SITE_FACTS`）當時沒有改接 `useSiteFacts()`，因為它是模組層級常數，在 `import` 當下同步
組出一大批 SEO／Hero 文案物件，沒有 Nuxt 元件的請求生命週期可以掛非同步抓取。本輪把這個限制
清掉。

**新結構**：club-copy.ts 依賴 GEO-03 五類事實（成立年份、主場、聯賽、梯隊代碼、聯絡方式）的
18 個內容鍵，全部從 `export const NAME: ClubText<T> = { tcrfc: {...}, bw: {...} }` 改成
`export function getXxx(club: string, facts: SiteFacts): T`——函式內部固定寫法是
`if (normalizeClub(club) === 'bw') { return {...bw...} }` 提前 return，接著一個無條件的
`return {...tcrfc...}`（兩個俱樂部分支都硬寫在程式碼裡，不是從 `facts` 動態算出「該顯示哪個
俱樂部」，`club` 參數純粹用來選文案分支，`facts` 提供該分支需要的事實數值）：

`getHomeSeo`／`getHomeHero`／`getHomeCtaTrio`／`getAboutIndexHero`／`getOurStorySeo`／
`getOurStoryHero`／`getEcosystemNodes`／`getHistorySeo`／`getFirstTeamSeo`／`getFirstTeamHero`／
`getFirstTeamIntro`／`getAcademyOverviewSeo`／`getAcademyOverviewHero`／`getAcademyPositioning`／
`getAcademyTeamsSeo`／`getAcademyTeamsHero`／`getAcademyTeamTabs`／`getJoinAcademyCard`。

不依賴事實的既有內容鍵（`HOME_PILLARS`／`ABOUT_NAV_DESC`／`JOIN_INDEX_HERO`／
`GOVERNANCE_HERO` 等，以及 `CLUB_IDENTITY` 扣掉下面提到的 `foundedZh` 之後的其餘欄位）
**維持原樣**，沒有無謂改成函式。

新增兩個模組內部輔助函式（不匯出）：`primaryVenueOf(facts)`／`squadCodesLabel(facts, sep?)`，
等價於 site-facts.ts 既有的 `getPrimaryVenue(club)`／`academyTeamCodesLabel(club, sep?)`，
差別是吃已經取得的 `facts` 物件、不用再認識 `club` 字串去查表。

**`CLUB_IDENTITY.foundedZh` 直接刪除**（不是改成工廠函式）：盤點發現全站沒有任何頁面實際
讀取 `identity.foundedZh`（`grep -rn "foundedZh" app` 零命中），是個讀著 SITE_FACTS 靜態快照
但沒有消費者的死欄位。`CLUB_IDENTITY`／`getClubIdentity()` 被 `SiteHeader.vue`／
`SiteFooter.vue` 與十餘個頁面共用，其餘欄位（`aboutLabelZh`／`slogan`／`social` 等）都不依賴
事實——為了一個沒人用的欄位把整個 `CLUB_IDENTITY` 改成吃 facts 參數的工廠函式、牽動所有
呼叫端，不符比例，直接刪除欄位本身才是對的做法（判斷寫在檔案裡的新增註解，供之後查證）。

**各消費頁改法**：呼叫端一律先 `const { facts } = useSiteFacts(clubKey.value)`（或既有頁面
已經在用的 `useSiteFacts('tcrfc')`／`useSiteFacts('bw')` 雙呼叫模式，見下方），再把原本
`computed(() => HOME_SEO[clubKey.value])` 這種直接查表，改成
`computed(() => getHomeSeo(clubKey.value, facts.value))`。改動的 9 個頁面檔案：
`app/pages/zh/index.vue`、`club/first-team/index.vue`、`join/index.vue`、`about/index.vue`、
`about/ecosystem.vue`、`about/our-story.vue`、`about/history.vue`、`academy/overview.vue`、
`academy/teams.vue`。

- `club/first-team/index.vue`／`about/our-story.vue`／`about/history.vue` 這三頁在上一輪
  （S1-12d 收尾）已經因為某個 `v-if="isTcrfc"` 區塊固定呼叫過 `useSiteFacts('tcrfc')`——
  本輪既然 hero／SEO 兩個俱樂部都要讀，改成動態的 `useSiteFacts(clubKey.value)`，變數名稱從
  `tcrfcFacts`／`tcrfcVenue` 改回 `facts`／`primaryVenue`（`clubKey.value==='tcrfc'` 時兩者
  等價，不影響那個 tcrfc 專屬區塊原本的顯示內容）。
- `academy/overview.vue` 本來就因為畫面兩個俱樂部都會渲染，同時呼叫
  `useSiteFacts('tcrfc')`／`useSiteFacts('bw')` 各一次（現有 fetch 不變），本輪只是從既有
  呼叫多解構一個 `facts` 欄位，沒有新增 fetch 次數。
- 其餘頁面（`index.vue`／`join/index.vue`／`about/index.vue`／`about/ecosystem.vue`／
  `academy/teams.vue`）新增一次 `useSiteFacts(clubKey.value)`——`clubKey` 在單一容器內是
  runtime 固定值（由 `NUXT_PUBLIC_CLUB` 決定），動態傳入不影響正確性，也不需要像
  `academy/overview.vue` 那樣兩個俱樂部都抓。

**`womens/index.vue` 的藍鯨官網連結**：改讀既有 `useSiteFacts('tcrfc')` 呼叫（本頁事實面板
本來就在用）多解構出的 `facts.blueWhaleSiteUrl`（對應後端 `PublicSiteFactsDto.BlueWhaleSiteUrl`，
`apps/api/README.md`「S1-12d」節「後續補完」已交付），`|| config.public.blueWhaleSiteUrl`
退回既有環境變數。因此新增：`SiteFacts` 型別（`site-facts.ts`）與 `PublicSiteFactsDto`
（`useSiteFacts.ts`）都補上 `blueWhaleSiteUrl: string | null` 欄位，`site-facts.ts` 靜態快照
兩俱樂部皆固定 `null`（降級時一律退回環境變數，不在快照裡放一個會過期的網址字面值）。

**`site-facts.ts` 現在的角色**：只剩 `useSiteFacts()` 的 `mergeSiteFacts()` 在 API 打不到時
讀取 `SITE_FACTS[club]` 當降級備援快照這一個消費者；`getPrimaryVenue()`／
`academyTeamCodesLabel()` 兩個輔助函式目前沒有任何呼叫端（`club-copy.ts` 已改用自己的
`primaryVenueOf(facts)`／`squadCodesLabel(facts)`），保留匯出是維持既有公開介面完整，
不是遺漏清理。

**改壞了一支驗證腳本，已修好**：`scripts/check-homepage-fidelity.mjs` 原本用
`clubCopySrc.indexOf('export const HOME_HERO')` 這種純文字掃描核對首頁 mockup 的 hero CTA
逐字值，`HOME_HERO` 改成函式後找不到宣告，`npm run lint` 當場報錯攔下（見
`docs/18-work-errors.md` `E-69`）。已改寫該腳本，解析 `export function getHomeHero` 函式
本體「最後一個頂層 `return { ... }`」（對應本檔工廠函式的既有寫作慣例：bw 分支先用 `if`
提前 return，tcrfc 分支是函式最後一個無條件 return），驗證邏輯本身（比對三個 CTA 欄位、
四張支柱卡片）沒有改變。

### SSR 輸出比對方法與結果（改動前後必須逐字一致）

用 `git stash` 取得改動前的 `apps/web` 原始碼，各自 `docker build -f apps/web/Dockerfile`
出一個映像檔（`tcrfc-web-before`／`tcrfc-web-s112d`），各起兩個容器（`tcrfc`／`bw`，皆未帶
`NUXT_PUBLIC_BLUE_WHALE_SITE_URL`、`apps/api` 未啟動，走 API 打不到的降級快照路徑），對
本輪實際改動的 9 個消費頁面 × `/zh/`／`/en/`（`womens` 只 tcrfc 容器有內容，bw 對單元 `06`
本來就 404，兩容器都測）逐一 `curl` 全文比對：

```
tcrfc：/、/club/first-team/、/join/、/about/、/about/ecosystem/、/about/our-story/、
       /about/history/、/academy/overview/、/academy/teams/、/womens/（各 zh／en，共 20 個網址）
bw：  同上 9 頁（不含 womens，bw 上是 404）（各 zh／en，共 18 個網址）
```

**結果：38 個網址，改動前後 SSR 輸出逐字元完全一致（0 處差異）**——確認這是重構而非改文案。
`/zh/womens/` 額外核對藍鯨官網按鈕的 `href` 兩容器映像檔皆輸出
`https://bw-stg.tcrfc.tw`（環境變數預設值，因為 API 未啟動、`facts.blueWhaleSiteUrl` 走
降級快照固定為 `null`，退回既有環境變數這條路徑本輪唯一驗證得到的路徑）。

🔴 **未驗證項目**：API 打得到、後台已設定 `blueWhaleSiteUrl` 時前台是否正確顯示後台填的值
（本輪只驗證「API 打不到／欄位為 null → 退回環境變數」這條路徑，延續 S1-12d 收尾同一個
環境限制，`apps/api` 未啟動）。

**驗收（2026-09-29）**：
```
npm run lint    # 0 errors, 527 warnings（等於既有基準上限，未超過）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```
本機起 `tcrfc`／`bw` 兩容器（`apps/api` 未啟動）：
- `node scripts/check-heading-structure.mjs`：`tcrfc`（156 條路由）／`bw`（138 條路由）皆
  H1 唯一、標題不跳階 0 違規。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs`（對 `bw`）：
  保護清單（15 頁）全數乾淨，棘輪未被違反，exit 0。
- `curl -I` 兩容器 `/zh/` 皆仍是 `X-Robots-Tag: noindex, nofollow`。
- 上方「SSR 輸出比對方法與結果」的 38 網址比對。

## S1-12e（`GEO-07`／`GEO-08` 內容結構與引用資訊，2026-09-29，`frontend-architect`）

主站規劃書 §7 `GEO-07`（H1 唯一、H2/H3 不跳階、首段獨立成立的摘要段、不以圖片承載文字）／
`GEO-08`（canonical、發布與更新時間、語言、作者或署名單位）。

### 新增的自動檢查：`scripts/check-heading-structure.mjs`

對**已渲染的 SSR 輸出**（不是原始碼）逐頁檢查標題大綱，理由與 `check-club-brand-leak.mjs`
完全同一個模式——標題大綱是 `SiteHeader.vue`／`SiteFooter.vue`／`MembershipBenefits.vue`
這些共用元件插進頁面之後的「組合結果」，單一 `.vue` 檔案看不到組合後前後接的是哪一級
標題。**刻意不掛進 `npm run lint`**：`lint` 目前純靜態、秒級跑完，這支腳本需要先把前台
跑起來才能執行，理由詳見腳本檔頭「為什麼不掛進 `npm run lint`」（同 `E-34` 的教訓）。

檢查三件事：① H1 恰好一個且文字非空（抓「H1 被圖片取代、沒有文字節點」的情形）；
② 整份 HTML 的標題依文件順序不得跳階（可以往回降級，只有「往下跳超過 1 級」算違規，
跟 axe-core `heading-order` 規則同一套判斷方式；「有效層級」優先讀 `aria-level` 屬性，
沒有才用標籤本身數字）；③ 首段摘要段的**結構性**存在（H1 之後最近的 `<p>`），標記
「占位／流程骨架」（`pending-inline`／`mock-flag`／文字含「待補」「待確認」）——**這項
不影響離開碼**，沒有真實內容可寫的頁面是內容缺口不是程式錯誤，比照 `check-club-brand-leak.mjs`
「進度計不影響離開碼」同一個理由（避免對缺內容的頁面 hard-fail 製造永久紅燈）。

**用法**（怎麼跑，納入驗收流程）：
```bash
# 先依上方「本機測試兩個 club」把 tcrfc／bw 兩個前台容器跑起來
node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:3001   # tcrfc
node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:3002   # bw
```

離開碼：H1 不是恰好一個、H1 文字為空、或出現標題跳階 → `1`；首段摘要缺口只列出來 → `0`。

### 修正前後違規數

首次執行（修正前）在 `tcrfc` 容器發現：
- **4 處標題跳階**，其中 **1 處是全站性的**：`SiteFooter.vue` 四個頁尾標題是 `<h4>`，
  但大多數頁面走到頁尾前最後一個標題只到 `<h2>`（例如首頁 `cta-title` 之後直接接頁尾），
  h2 → h4 跳兩級；另 2 處是個別頁面缺 h2：`zh/academy/coaches.vue`（h1 直接接教練卡片的
  h3 人名）、`zh/academy/teams.vue`（h1 直接接分頁面板內的 h3「名單」）。
- H1 唯一性：全數通過（79 個非動態頁面各自恰好一個 `<h1>`，根路徑轉址頁與
  `news/[slug]` 動態頁不計入，見腳本檔頭說明）。

修正後（`tcrfc`／`bw` 兩容器皆已實測）：H1 唯一與標題跳階兩項**全數通過（0 違規）**；
首段摘要段缺口 4 筆（`/zh/`／`/en/` 各 2 頁：`cart/`、`checkout/`，見下方「已知內容缺口」）。

### 改了哪些檔案

- [`app/components/SiteFooter.vue`](app/components/SiteFooter.vue)：四個 `<h4>` 標題加上
  `aria-level="2"`。**tag 名稱、class、DOM 結構本身完全不動**（本檔開頭明文「DOM／class
  不動」的既有紀律）——`aria-level` 是 WAI-ARIA 允許的既有技巧，只覆寫輔助技術與遵循
  ARIA 的爬蟲讀到的標題層級，`.footer-col h4` 這個 CSS 選擇器與既有 DOM 比對機制
  （`check-club-brand-leak.mjs`）完全不受影響。
- [`app/pages/zh/academy/coaches.vue`](app/pages/zh/academy/coaches.vue)／
  [`app/pages/zh/academy/teams.vue`](app/pages/zh/academy/teams.vue)：各加一個
  `class="visually-hidden"` 的 `h2`，比照本站既有慣例（`zh/member/index.vue` 的
  `member-title`、`zh/news/[slug]/index.vue` 的 `article-body-title`），補上大綱層級，
  不影響版面。
- [`app/pages/zh/news/[slug]/index.vue`](app/pages/zh/news/%5Bslug%5D/index.vue)：
  `GEO-08`「文章型內容另輸出作者或署名單位」——原本可見的「作者」欄位只有標籤沒有值
  （`article.vue` 時代就留白），這裡補上 `siteName`（俱樂部本身），與同頁 JSON-LD 的
  `author`／`publisher` 讀同一個值，滿足 `GEO-04` 明文與結構化資料一致。
- 新增 [`scripts/check-heading-structure.mjs`](scripts/check-heading-structure.mjs)。

### `GEO-08` 其餘三項（canonical、語言、共用機制）現況

**都已由既有機制涵蓋，本輪沒有新增程式碼**：canonical 由 `@nuxtjs/seo`（`nuxt-seo-utils`）
依 `site.url` ＋ 目前路徑自動產生（S0-9b 已實測 `NUXT_PUBLIC_SITE_URL` 可 runtime 覆寫）；
`<html lang>` 由 `app/plugins/site-locale.ts` 依路由動態決定（S1-13）；`noindex` 由
`nuxt.config.ts` 的 `routeRules['/**']` 加 `X-Robots-Tag` 標頭（CLAUDE.md 全域規定第 5 條）。
本輪已用本機 `tcrfc`／`bw` 兩容器對 `/zh/`／`/en/` 各實測一次，四種組合的 `<html lang>`、
`<link rel="canonical">`、`X-Robots-Tag` 皆正確（見下方「驗證指令與結果」）。

**發布與更新時間**：`publishedAt` 已輸出（JSON-LD `datePublished` ＋頁面可見的
「發布日期」），`dateModified` 因 `apps/api` 的 `ArticleDetailDto` 沒有公開 `updatedAt`
欄位，維持不輸出（S1-12f 已知缺口，不在 `apps/web` 範圍內，見該處程式碼註解——
`GEO-08` 明文「更新時間要真的更新，不是發布時間複製一份」，沒有真實資料就不輸出，
不臆造）。

### 首段摘要段：已核實的既有慣例與無法自動判斷的缺口

**72 個內容頁已有結構性首段摘要**，慣例是 `.page-hero__lede`（首頁另用
`.hero__sub`）——H1 之後緊接一段可獨立理解的摘要文字，這是搬遷時就存在的既有慣例，
本輪沒有新增規則，只是確認並用自動檢查釘住。

**沒有真實內容可寫、刻意沒有臆造文案的頁面**（列出來，不是漏做）：
- `/zh/cart/`、`/zh/checkout/`（含 `/en/` 孿生路由）：「流程骨架」頁面，H1 後的文字是
  QA 提示（「表單不會送出」），不是內容摘要——這兩頁本身是购物流程的功能性骨架，
  尚無真實文案可摘要，腳本已標記為 `PLACEHOLDER`。
- `zh/shop/home-jersey-2026/`、`zh/shop/cushioned-socks/`（商品頁）：H1 後緊接的是
  「尚有庫存」等庫存狀態文字，不是商品描述——頁面上材質／產地／退換貨政策全部標記
  `pending-inline`（待客戶提供），沒有真實商品描述可摘要。**自動腳本沒有標記這兩頁**
  （因為 H1 後確實存在非空、非占位標記的 `<p>`，只是語意上不是摘要），這裡改用人工
  審查列出，供下一輪有真實商品資料時補上。
- `zh/checkout/complete/`、`zh/club/first-team/player/`：H1 後的文字是交易確認訊息／
  球員背號與租借狀態的 meta 列，結構上存在且非占位，但同樣不是敘事型摘要——列出供
  參考，不強制修改（訂單完成頁與球員 meta 列本來就是這種簡短資訊格式，非本輪判定
  為缺陷）。

### 驗證指令與實際結果

```bash
npm run lint    # 0 errors, 527 warnings（與改動前基準相同，未新增警告）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功

# 本機起兩個前台容器（見上方「本機測試兩個 club」）後：
node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:3001  # tcrfc：H1／跳階皆 0 違規
node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:3002  # bw：H1／跳階皆 0 違規
node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:3002    # 保護清單 15 頁乾淨，棘輪未違反

# 抽查 canonical／<html lang>／noindex（curl，四種組合皆正確）：
curl -s http://127.0.0.1:3001/zh/ | grep -oE '<html[^>]*>|<link rel="canonical"[^>]*>'
curl -sI http://127.0.0.1:3001/zh/ | grep -i x-robots-tag
# （/en/、bw 容器同樣抽查過，結果見上方「其餘三項現況」）
```

### 未驗證項目

- 動態文章頁（`news/[slug]`）的標題結構與可見作者欄位**沒有用真實資料實機驗證**——
  本輪未啟動 `apps/api`（任務指示明文禁止），該頁在沒有後端時恆 404，`check-heading-structure.mjs`
  的路由收集規則因此排除它（同 `check-club-brand-leak.mjs` 既有慣例）。作者欄位的樣板
  改動已通過 `npx nuxi typecheck`（隨 `npm run build` 一併跑）與程式碼審視，實際渲染
  留給下一輪有 `apps/api` 可用時確認。
- 「不以圖片排版承載文字」只驗證了 H1 本身（文字節點非空），沒有掃描全站其餘內文
  是否有「整段文字做成圖片」的情形——本站目前全站是語意化 HTML＋CSS 排版，沒有
  既有樣式特徵可以自動比對，這項留給人工審查，腳本檔頭已誠實列為「明文不做的事」。

## S1-17（07 新聞中心／10 表單中心／Location & Map，2026-09-29，`frontend-architect`）

主站規劃書 §3.7（新聞中心 8 分類）、§3.10（表單中心 7 類 ＋ 附屬頁 Location & Map／
Contact Information）。**Contact Information 頁不在本次主輪任務範圍**（未點名），
下方「規格疑點」第 5 點記錄的缺口已在「S1-17 收尾修正」（見該節）補上。

### 改了哪些檔案

**新增**：
- `app/composables/useFormSubmit.ts` — 7 張表單共用的送出狀態機（呼叫 `POST
  /api/v1/{club}/forms/{formCode}/submissions`）
- `app/components/FormStatusBanner.vue` — 送出成功／失敗的畫面提示（mockup 原本沒有這塊，
  `action=""` 的純靜態表單本來就不會有送出後狀態）
- `app/components/HoneypotField.vue` — 誘捕欄位（比照 `SubmitFormRequest.Website`）
- `server/api/backend/[...path].ts` — **改動**：原本只轉發 GET，補上 POST 轉發（讀
  body 一併帶過去），方法白名單限定 GET／POST 兩種

**改動（07 新聞中心）**：
- `app/pages/zh/news/academy.vue`、`app/pages/zh/news/player-stories.vue` — 接上真實
  API（`category=academy`／`category=player-stories`），取代原本寫死的空狀態文案
- `app/pages/zh/news/index.vue` — 修正「精選置頂」邏輯（原本是 `slice(0,3)` 沒有真的檢查
  `isFeatured`），新增標籤篩選 state
- `app/pages/zh/news/club.vue`／`match.vue`／`international.vue`／`camps-events.vue`／
  `community.vue` — 新增標籤篩選 state 並傳給 `NewsFilterForm`／`NewsListBody`
- `app/pages/zh/news/[slug]/index.vue` — 詳情頁 `.article-meta-row` 新增「標籤」欄（有
  標籤才顯示）
- `app/components/news/NewsFilterForm.vue` — 新增「標籤」下拉（`tags`/`tag` prop，
  沒有標籤選項時整格不顯示）
- `app/components/news/NewsListBody.vue` — `matches()` 新增標籤比對
- `app/utils/news.ts` — 新增 `newsDistinctTags()`

**改動（10 表單中心，7 類）**：`app/pages/zh/join/player/index.vue`、`academy/index.vue`、
`camp-registration/index.vue`、`international-player/index.vue`、`partnership/index.vue`、
`media/index.vue`、`general/index.vue` — 每頁：欄位 `v-model` 化、`@submit.prevent`、
移除 `novalidate`（讓瀏覽器原生必填／格式驗證生效，取代原本永遠 `display:none`
且從未被任何腳本觸發過的 `.field-error` 手刻訊息，見下方「規格疑點」第 4 點）、
`<HoneypotField>`、`<FormStatusBanner>`、送出中停用送出鈕。

**改動（Location & Map）**：`app/pages/zh/join/location/index.vue` — 嵌入地圖從
「準備中」佔位文字改為真的 Google Maps 免金鑰 `output=embed` iframe（用主場地址算網址）；
「開啟 Google 導航」連結從寫死的 URL 編碼字串改為同一份地址算出來（GEO-03 單一來源，
避免地址與導航連結各自維護一份、後台改了地址卻忘記同步這條連結）。

### 07 新聞中心：各項需求對照

| 需求（規劃書 3.7） | 狀態 | 說明 |
|---|---|---|
| 分類 Tab（8 類） | ✅ 既有（S0-9e／既有搬遷），本輪未改動路由本身 |
| 標籤篩選 | ✅ **本輪新增**——`ArticleListItemDto.tags` 是 S1-5 就已回傳的既有欄位，先前完全沒有前台頁面消費，本輪補上下拉篩選（news/index 與 5 個分類頁）與詳情頁顯示 |
| 年月篩選 | ✅ 既有（S0-9e），本輪未改動邏輯 |
| 關鍵字搜尋 | ✅ 既有（S0-9e），本輪未改動邏輯 |
| 分頁／無限捲動 | ✅ 既有「載入更多」按鈕（S0-9e），規劃書「分頁／無限捲動」擇一即可，未改動 |
| 精選置頂（最多 3 則） | 🟡 **本輪修正**——`news/index.vue` 原本是「列表前 3 篇」，沒有真的檢查 `isFeatured`，與首頁 `S1-14` 已經做對的邏輯（精選優先、不足補最新發布）不一致。已改成同一套邏輯 |
| 熱門文章側欄 | 🔴 **API 不支援，未做**——`ArticleListItemDto`（列表端點）沒有回傳瀏覽數，`ArticleDetailDto`（單篇端點）才有 `viewCount`；且列表端點也沒有「依熱門度排序」的查詢參數。要做「熱門文章側欄」，必須先改 `apps/api` 補上其中一項，不在本次任務範圍（任務指示「不要改 apps/api」），未自行變通（例如用發布時間排序假裝熱門）誤導使用者 |
| 7.8 媒體專區（新聞稿下載／品牌識別包／高解析圖庫／媒體聯絡窗口） | 🔴 **API 不支援，未做**——對應後台 `B6` 媒體資源模組（`STATUS.md` `S2-3`，排在本次任務之後才開工），`media.vue` 維持既有「建置中」占位文案，未改動 |
| 詳情頁：標籤 | ✅ **本輪新增**——`.article-meta-row` 補上「標籤」欄 |
| 詳情頁：其餘（封面圖／日期／作者／內文／社群分享／相關文章） | ⬜ 既有（S0-9e／S1-12e），本輪未改動 |

### 10 表單中心：欄位對應表（每張表單的完整決策，2026-09-29 收尾修正後）

規劃書 §3.10 只列「主要欄位」，`apps/api` 的 `form_fields` 種子資料（`db/seed/
generate-club-seed-sql.py` 的 `FORM_FIELD_DEFAULTS`）採**最小可行欄位組**，跟原始
mockup 前台欄位數量對不齊（mockup 是完整 UX 設計稿，欄位遠多於後端目前定義的）。
**任務指示明文「欄位以後端表單定義為準，不要在前台自己加欄位」**——S1-17 主輪交付時
的做法是「可見欄位不動、對應不到的欄位悄悄不送出」，事後複核認定這樣會誤導使用者以為
填了會被收到，且違反「個資只收必要的」。**S1-17 收尾修正（2026-09-29）已把下表「原
未送出」欄列出的欄位全部從畫面移除**（不是隱藏），現在畫面上看得到的欄位＝會被送出的
欄位，兩者不再有落差。

| 表單 | 後端欄位（`form_code`） | 對應決策 | 已從畫面移除（原「未送出」，規格與後端皆無對應） |
|---|---|---|---|
| 10.1 加入球隊 | `join_player`：`name`／`birth_date`／`position`／`experience`／`video_url`／`contact` | `name`＝中文姓名（＋英文姓名括號附加，英文姓名保留，因為它會被合併送出）；`position`＝選單顯示文字（後端是 `text` 型別，無選項限制）；`contact`＝電話＋Email 合併 | 性別、居住城市、慣用腳、目前球隊、履歷／照片檔案（含整個「上傳資料」區塊） |
| 10.2 加入學院／兒童訓練 | `academy_children_training`：`enrollment_category`（**封閉選項**）／`name`／`birth_date`／`location_preference`／`contact`／`experience`／`health_status` | 🔴 **mockup 13 個報名細項對到後端封閉的 7 選項**：6 種專項訓練細項收斂為單一「專項訓練」；「尚未確定，請協助建議」**沒有對應選項**（後端封閉選項沒有「不確定」），退回「兒童混齡班」當技術預設值，同時把使用者實際選擇的文字併入 `experience` 欄位開頭，不遺失真正的選擇；`contact`（後端標籤是「家長聯絡方式」）＝家長姓名＋關係＋電話＋Email 合併 | 學員性別、居住地區、學員照片／健康聲明以外的證明文件（含整個「上傳資料」區塊） |
| 10.3 營隊報名 | `camp_registration`：`session_choice`／`name`／`birth_date`／`health_declaration`（**consent 布林型別**）／`contact` | ✅ **重大欄位型別落差已改用另一種做法**——見下方「規格疑點」第 1 點（`health_declaration` 改為勾選框，不再把健康聲明文字併入 `contact`）；`contact`＝緊急聯絡人姓名＋關係＋電話 | 家長聯絡資料（`parent_name`／`parent_phone`／`parent_email`）、健康聲明書或其他文件上傳（見規格疑點第 2 點） |
| 10.4 國際球員詢問 | `international_player_enquiry`：`name`／`nationality`／`passport_no`／`experience`／`video_url`／`visa_status`／`contact` | `experience`＝場上位置＋目前球隊＋比賽等級併入足球經歷摘要前面；`visa_status`＝選單顯示文字（`text` 型別）；`contact`＝Email＋電話合併 | 出生日期（這張表單後端沒有 `birth_date` 鍵，跟 10.1／10.2 不同，且原本就沒有 `v-model`，填了也從未被讀取）、經紀人聯絡方式、CV／其他文件（含整個「Uploads」區塊） |
| 10.5 合作夥伴與贊助洽詢 | `partnership_sponsorship`：`enquiry_type`（**封閉選項**）／`company`／`industry`／`budget_range`／`cooperation_direction`／`sponsorship_interest`／`name`／`contact` | `enquiry_type`：mockup 送英文代碼，後端封閉選項是中文字面值 `["合作夥伴","贊助","兩者"]`，用對照表轉換；`industry`／`budget_range`＝選單顯示文字（`text` 型別）；`cooperation_direction`＝勾選的「合作方向」項目名稱＋「合作構想」欄位原文合併；`sponsorship_interest`＝勾選的「感興趣贊助方案」項目名稱 | 統一編號、聯絡人職稱、提案文件（含整個「上傳資料」區塊） |
| 10.6 媒體詢問 | `media_enquiry`：`media_name`／`name`／`topic`／`deadline`／`contact` | `topic`＝採訪類型顯示文字＋採訪主題原文合併；`contact`＝電話＋Email 合併 | 聯絡人職稱、採訪大綱文件（含整個「上傳資料」區塊） |
| 10.7 一般聯絡 | `general_contact`：`name`／`contact`／`subject`／`message` | `contact`＝Email（規劃書 §3.10 10.7 欄位定義本來就只寫「Email」，後端 `contact` 這一鍵的題目文字也是「Email」）；`subject`＝選單顯示文字（`text` 型別） | 聯絡電話（選填欄位，規格與後端都沒有這個鍵）、附件（含整個「上傳附件」區塊） |

**共通機制對照**（規劃書 §3.10「共通機制」）：

| 需求 | 狀態 |
|---|---|
| 必填驗證 | ✅ 移除 `<form novalidate>`，改用瀏覽器原生驗證（`required`／`type=email`／`type=url`／`type=date` 皆為 mockup 既有屬性，只是先前 `novalidate` 讓它們完全失效）。**未做**：逐欄位 JS 自訂訊息（`.field-error` 段落，`.field-error{display:none}` 的 CSS 規則本來就存在，但 mockup 從未有任何腳本觸發它顯示——維持這個既有落差，改用瀏覽器原生提示） |
| 送出後自動回覆信／通知信／寫入後台 | 🔵 已由 `apps/api` 端實作（`FormsRepository.SubmitAsync` 寫入 `enquiries`／`enquiry_answers`），Email 通知另見 `apps/api` README；本輪只負責前台送出，未驗證信件是否真的寄出（未啟動 `apps/api`） |
| 個資同意條款勾選 | ✅ 既有 `consent` 核取方塊，`required` 屬性現在真的生效（見上方必填驗證） |
| 防機器人（reCAPTCHA／Turnstile） | 🔵 既有 Cloudflare Turnstile 佔位（`data-sitekey=""`，sitekey 待客戶申請帳號，維持既有落差不動）＋ **本輪新增**誘捕欄位（`HoneypotField`）與（`apps/api` 端既有的）Rate Limiting，兩者都是不需要外部服務金鑰的防線 |
| 檔案上傳（履歷／影片連結） | 🔴 影片連結（`video_url`）已送出；**檔案本身無法上傳**——`apps/api` 的 `FormFieldTypes.File` 註解明文「本輪未建立真正的檔案上傳通路」，屬既有缺口非本輪造成，各表單的檔案欄位維持在畫面上但不送出任何內容 |

### Location & Map：各項需求對照

| 需求（規劃書 §3.10 附屬頁） | 狀態 |
|---|---|
| 多場地列表（訓練基地、主場、學院場地） | 🟡 **主場**已讀 `useSiteFacts('tcrfc')`（GEO-03 既有機制）；**訓練基地／學院場地**維持「地址資訊準備中」——`venues` 資料表目前只有 1 筆場地種子資料（西屯足球場，`db/seed/generate-club-seed-sql.py` `site.home_venue_ids`），沒有另外的訓練基地／學院場地主檔可用，這是資料現況不是前台缺陷 |
| 嵌入地圖 | ✅ **本輪新增**——改用 Google Maps 免金鑰 `output=embed` iframe（依主場地址算網址），只有唯讀顯示、沒有互動路線規劃（那需要 Maps API 金鑰） |
| 交通指引 | ⬜ 既有靜態文字（開車／大眾運輸），本輪未改動 |
| 導航連結 | ✅ **本輪修正**——原本是寫死的 URL 編碼地址字串（跟 `site-facts` 的地址是兩份各自維護的資料），改成用同一份 `useSiteFacts` 地址即時算出，地址若在後台改了不會再有連結跟著過期不同步的風險 |

### 規格疑點（列出，未自行決定）

1. 🔴 **`camp_registration` 的 `health_declaration` 欄位型別是 `consent`（布林同意），
   但規劃書 §3.10「健康聲明」與 mockup 前台顯然是要收「過敏史、慢性病、目前服用藥物」
   這類**自由文字內容**，兩者無法兩全**——`consent` 型別只接受 `true`／`1`／`on`／`yes`，
   送出自由文字會被 `FormsRepository.ValidateFieldValue` 的 `Consent` 分支拒絕（400）。
   **S1-17 收尾修正（2026-09-29）已改做法**：原本「固定送 `true`、健康聲明文字併入
   `contact` 欄位」的暫行寫法已移除（那樣會讓緊急聯絡人欄位混入不相干的健康資訊，
   且畫面是文字框、送出時卻被當成單純打勾，一樣是「畫面與送出行為不一致」）。現在
   `cp-health-declaration` 是**一個勾選框**，文案為中性聲明「本人確認已據實告知學員的
   過敏史、慢性病、目前服用藥物等健康狀況，如有變動將主動告知課程部。」——**這段文案是
   本輪自擬的最精簡說明，規劃書與既有文案都沒有現成的健康聲明勾選文字，需要客戶確認
   措辭是否足夠、是否需要法務再審**。⚠️ **代價**：改用勾選框後，**營隊實際的健康狀況
   細節（過敏史、慢性病、服用藥物的具體內容）現在完全沒有欄位可以收**，比修正前的暫行
   做法（至少內容還在，只是位置不對）更保守。**建議下一輪把 `health_declaration` 欄位
   型別改為 `text`／`textarea`**，讓真正的健康內容有正確的地方存——這是涉及兒童安全
   資訊的欄位，目前「有勾選、無內容」的狀態不建議長期維持。
2. 🔴 **`camp_registration` 沒有承接「家長聯絡資料」的欄位**——後端只有 `contact`
   一鍵，語意標籤是「緊急聯絡人」，家長姓名／電話／Email 三個 mockup 欄位完全沒有
   對應鍵可送。**S1-17 收尾修正（2026-09-29）已把這三個欄位從畫面移除**（規劃書 §3.10
   10.3 的欄位定義本來就只列「營隊梯次、學員資料、健康聲明、緊急聯絡人」，沒有「家長
   聯絡」這一項，跟 10.2 不同，移除後畫面與規格一致）。已知風險不變：緊急聯絡人未必是
   家長本人，若後台窗口只看得到 `enquiries` 的 `contact` 欄位（緊急聯絡人），可能找不到
   真正該聯繫的家長。**需要客戶確認**：營隊報名是否也要收家長聯絡方式，若要，須先把
   欄位補進規劃書與後端 `form_fields`，不能只在前台加回畫面。
3. **10.2 學院／兒童訓練報名項目「尚未確定，請協助建議」無法對應後端封閉選項**——
   後端 `enrollment_category` 只有 7 個固定值，沒有「不確定」這個選項；已在上方欄位
   對應表說明暫行做法（退回「兒童混齡班」＋原文併入 `experience`）。
4. **`.field-error` 手刻錯誤訊息維持未啟用狀態**——`tcrfc.css` 的 `.field-error{
   display:none }` 規則從 mockup 時代就存在，但沒有任何腳本（mockup 原始版或本輪）
   真的去觸發顯示；本輪選擇移除 `novalidate` 讓瀏覽器原生驗證接手（成本低、行為
   正確），沒有另外實作 60＋ 個欄位各自的 JS 顯示邏輯——這是效益判斷，不是規格要求，
   下一輪若要更客製化的錯誤訊息樣式，這批 `.field-error` 段落還在，可以另外接上。
5. ✅ **Contact Information 頁（`/zh/join/contact/`）的電話／營業時間欄位曾經一直空白，
   已在 S1-17 收尾修正（2026-09-29）補上綁定**——`PublicSiteFactsDto.contact` 早就有
   `phone`／`hours` 兩個欄位，但該頁 template 的「電話」「營業時間」兩格先前從未綁定
   顯示。現已改為 `tcrfcFacts.contact.phone`／`.hours`，有值才顯示（`v-if`），沒有值
   （目前兩俱樂部都是 `null`）就只顯示標籤，不顯示假資料。**待客戶提供正式電話與營業
   時間、後台 `I` 網站設定填值後，這裡會自動顯示**，不需要再改程式碼。

## S1-17 收尾修正（欄位收斂、代理限縮與 IP 轉發、聯絡資訊補綁，2026-09-29，`frontend-architect`）

主 session 複核 S1-17 交付後回饋五項問題，全部在本輪處理，範圍限定 `apps/web`
（未動 `apps/api`，未啟動 `apps/api`）：

1. **7 張表單移除「畫面有、送出卻悄悄丟棄」的欄位**——見上方「10 表單中心：欄位對應表」
   已更新為收尾修正後的版本，逐表單移除清單見該表「已從畫面移除」欄。
2. **10.3 營隊報名健康聲明改為勾選框**、10.3 家長聯絡欄位整組移除——見「規格疑點」第 1、2 點。
3. **`server/api/backend/[...path].ts`**：POST 收窄為只放行表單送出路徑（正規表示式
   `/^[a-z][a-z0-9-]*\/forms\/[a-z][a-z0-9_]*\/submissions$/`），其他路徑的 POST 一律
   405；並新增訪客真實 IP 轉發（見下方「代理的 IP 轉發設計」）。
4. **Contact Information 頁補綁電話／營業時間**——見「規格疑點」第 5 點。

### 改了哪些檔案

- `app/pages/zh/join/player/index.vue` — 移除性別、居住城市、慣用腳、目前球隊、
  履歷／照片檔案上傳（整個「上傳資料」fieldset）；同步移除側欄「填寫前可以先準備」
  提到履歷檔案的那一條（欄位都拿掉了，提示使用者準備一份無處可交的檔案沒有意義）。
- `app/pages/zh/join/academy/index.vue` — 移除性別、居住地區、學員照片／健康聲明證明
  文件上傳（整個「上傳資料」fieldset）。
- `app/pages/zh/join/camp-registration/index.vue` — 移除整個「家長聯絡資料」fieldset
  （`parent_name`／`parent_phone`／`parent_email`）與「上傳資料」fieldset（`doc_file`）；
  `healthDeclaration`（textarea，字串）改為 `healthDeclarationConsent`（checkbox，布林），
  `onSubmit` 不再把健康聲明文字併入 `contact`，`contact` 現在只有緊急聯絡人資訊。
- `app/pages/zh/join/international-player/index.vue` — 移除出生日期（`dob`，本來就沒有
  `v-model`，是三個沒被讀取的欄位之一）、Agent / Representative Contact、整個「Uploads」
  fieldset（`cv_file`／`doc_file`）；「Visa & Representation」legend 改名「Visa Status」
  （拿掉 agent 欄位後原名不再準確）。
- `app/pages/zh/join/partnership/index.vue` — 移除統一編號（`tax_id`）、職稱
  （`contact_title`）、整個「上傳資料」fieldset（`doc_file`）。
- `app/pages/zh/join/media/index.vue` — 移除職稱（`contact_title`）、整個「上傳資料」
  fieldset（`doc_file`）。
- `app/pages/zh/join/general/index.vue` — 移除聯絡電話（選填，`phone`）、整個「上傳附件」
  fieldset（`doc_file`）。
- `app/pages/zh/join/contact/index.vue` — 電話／營業時間改綁
  `useSiteFacts('tcrfc').contact.phone`／`.hours`，`v-if` 有值才顯示。
- `server/api/backend/[...path].ts` — 重寫：GET 邏輯不變；POST 改為先驗證 path 是否符合
  表單送出路徑正規表示式，符合才轉發（否則 405），並讀取 Caddy 設定的 `X-Real-IP`
  （經 `isIP` 驗證）、以單一值的 `X-Forwarded-For` 標頭轉發給 `apps/api`（主 session
  於 2026-09-29 由 `getRequestIP(xForwardedFor)` 改為此做法，見 `E-71`）。

### 代理的 IP 轉發設計

`apps/api` 依「真實訪客 IP」對表單送出端點做固定視窗限流（`Program.cs`「S1-10（審查
回饋修正）」段、`Security/TrustedProxyConfiguration.cs`）。但這支代理呼叫 `apps/api`
走 Docker 內部網路（`backendApiBase()` 在 SSR 階段解析成 `http://api:8080`），
**完全繞過 Caddy**——從 `apps/api` 的角度看，這次連線來源是 `web` 容器本身，不是 Caddy。

- **這裡（前台代理）做的事**：只讀 Caddy 設定的 `X-Real-IP`（`deploy/Caddyfile` 對
  nuxt-* 上游 `header_up X-Real-IP {client_ip}`），經 `isIP` 驗證後，以**單一值**的
  `X-Forwarded-For` 轉給 `apps/api`。**不讀 `X-Forwarded-For`**：Caddy 對已信任的上游
  （Cloudflare）是「附加」，而 Cloudflare 會保留訪客自己送的 XFF，所以 XFF 的第一個值
  可以被訪客偽造、繞過限流（`E-71`，主 session 複核時發現；本輪 agent 原本用
  `getRequestIP(event, { xForwardedFor: true })` 取第一個值）。`{client_ip}` 是 Caddy
  依 `client_ip_headers`＋`trusted_proxies` 解出的值，`header_up` 會覆蓋訪客自送的同名
  標頭；`nuxt-tcrfc`／`nuxt-bw` 沒有對外發布 port，只有 Caddy 連得進來。
- **後端那一側（另一個 agent 負責）**：`apps/api` 既有的 `TRUSTED_PROXY_IP` 信任清單
  若只認 Caddy 的固定 IP（`172.28.238.2`），不會信任這支代理送來的 `X-Forwarded-For`
  ——把 `web` 容器的 IP 也一併納入信任清單是後端那一側的工作。**兩側要一起上線才會
  生效**：只改前台這一側，標頭會被 `apps/api` 忽略（因為它不信任 `web` 容器的來源
  IP）；只改後端那一側，前台沒送這個標頭一樣拿不到真實 IP。
- **本機開發／沒有 Caddy 在前面時**：沒有 `X-Real-IP`，這裡選擇不送這個標頭（而不是送一個假值），讓 `apps/api` 退回連線本身
  看到的來源 IP（本機開發時就是 `web` 容器或 `dotnet run` 的直連 IP，跟正式環境的行為
  一致：沒有可信的訪客 IP 就不假裝有）。
- ⚠️ **已知限制**：這個「只信任 Caddy」的邊界是靠 Docker 網路拓撲（`web` 容器沒發布
  port）而不是應用層驗證來成立的——如果本機直接對外發布 `nuxt-tcrfc` 的 port（例如本輪
  驗證時用 `docker run -p 13001:3000`），任何人都能直接對這個容器送任意
  `X-Real-IP`，這裡會照樣轉發出去。正式環境不會有這個落差（`docker-compose.yml`
  沒有對 `nuxt-tcrfc`／`nuxt-bw` 發布 port），純粹是本機單獨測試這支代理時的已知情境，
  記錄在此供下一輪留意。

### 驗證指令與實際結果（2026-09-29，收尾修正）

```bash
npm run lint    # 0 errors, 429 warnings（低於 467 上限）
npm run build   # 成功（含 nuxi typecheck）
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機起兩個容器（`tcrfc` port 13001／`bw` port 13002，`bw` 帶
`NUXT_PUBLIC_CLUB=bw`，**`apps/api` 未啟動**，依派工規則不自行啟動、不碰密碼）：

- 兩容器對 7 個表單頁＋Contact Information 頁的 `/zh/`／`/en/`（共 16 條路徑）
  **全數 `200`**，無 `500`；容器 log 顯示表單頁在 SSR 階段呼叫
  `useFormSubmit`／`useSiteFacts` 以外沒有拋出例外。
- `curl` 抓取渲染後 HTML，用 `grep -oE 'name="..."'` 逐表單核對：`gender`／`city`／
  `preferred_foot`／`current_team`／`resume_file`／`photo_file`（10.1）、
  `student_gender`／`city`／`photo_file`／`doc_file`（10.2）、`parent_name`／
  `parent_phone`／`parent_email`（10.3）、`dob`／`agent_contact`／`cv_file`／`doc_file`
  （10.4）、`tax_id`／`contact_title`／`doc_file`（10.5）、`contact_title`／`doc_file`
  （10.6）、`phone`／`doc_file`（10.7）**全數 0 筆**，確認已從 DOM 移除而非隱藏；10.3
  的 `health_declaration` 欄位確認渲染為 `<input type="checkbox">`。
- `node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:13001`／`13002`：
  **H1 唯一、標題不跳階皆 0 違規**（tcrfc 156 條路由、bw 138 條路由，其餘為 30x／404 略過）。
- `node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:13002`：**通過**
  （保護清單 15 頁全數乾淨，棘輪未被違反）。
- 兩容器皆有 `X-Robots-Tag: noindex, nofollow`。
- 代理實測（對 `tcrfc` 容器）：
  - `POST /api/backend/tcrfc/news`、`POST /api/backend/admin/accounts`、
    `POST /api/backend/tcrfc/forms/join_player/submissions/../../accounts` 三種非表單
    送出形狀的 POST **皆回 `405`**。
  - `POST /api/backend/tcrfc/forms/join_player/submissions`（表單送出路徑，帶合法
    JSON body）：因 `apps/api` 未啟動，回應 `500`（`ECONNREFUSED 127.0.0.1:5299`，
    容器 log 已確認是連線層失敗，不是這支代理自己的邏輯錯誤）——代理正確嘗試轉發，
    只是後端不在，不是本輪能驗證到「後端真的收到並處理」的程度。
  - `GET /api/backend/tcrfc/site-facts?lang=zh`：同樣因 `apps/api` 未啟動回 `500`，
    確認 GET 既有行為未被本輪改動影響（跟 POST 一樣卡在連線層，不是被誤判 405）。

### 未驗證項目

- **表單送出（POST）本身未實機測試**——`apps/api` 未啟動，無法驗證 `answers` 對應是否
  真的被後端接受、`enquiries`／`enquiry_answers` 是否正確寫入、自動回覆信與通知信是否
  寄出。上方「欄位對應表」的映射邏輯已通過 TypeScript 型別檢查（`npm run build` 含
  `nuxi typecheck`）與程式碼審視，但沒有一次真正打過端點。
- **`academy`／`player-stories` 兩個新聞分類的「真的查完 API 回傳 0 筆」與「API 打不到
  時的降級空清單」，本輪只驗證到後者**——兩種情況目前渲染結果相同（`articles.length
  === 0`），語意上是同一組程式碼路徑，理論上正確，但沒有機會用真正啟動的 `apps/api`
  驗證這兩個分類確實查得到「0 筆」而不是別的錯誤。
- **標籤篩選下拉的實際互動行為**（選了標籤後清單是否正確收斂）——`apps/api` 未啟動時
  沒有任何文章帶標籤可供測試，`newsDistinctTags()`／`NewsListBody.matches()` 的邏輯已
  經程式碼審視但未用真實資料按過一次。
- **Google Maps `output=embed` iframe 在瀏覽器裡的實際渲染**（是否顯示正確地圖位置）
  ——只驗證了 SSR 輸出的 `<iframe src>` 網址字串正確（地址 URL 編碼無誤），沒有用
  無頭瀏覽器截圖確認地圖畫面本身。
- **收尾修正**：`camp_registration` 送出 `health_declaration: 'true'`（勾選）後，
  `apps/api` 端的 `consent` 型別驗證是否真的接受、`enquiries`／`enquiry_answers` 是否
  正確寫入——`apps/api` 未啟動，只驗證了前台 payload 的形狀（型別檢查＋程式碼審視）。
- **收尾修正**：代理的 `X-Forwarded-For` 轉發只驗證到「這裡有正確解析並附加標頭」（見
  `server/api/backend/[...path].ts` 邏輯與程式碼審視），沒有機會驗證 `apps/api` 那一側
  收到後是否真的被 `TrustedProxyConfiguration` 信任並用於限流分區——這一半在另一個
  agent 的改動範圍內，需要兩側都上線後才能端到端驗證（例如從兩個不同來源 IP 送出、
  確認限流分區確實各自獨立）。

## S1-18（12 FAQ 獨立單元，先建 3–4 個高頻主題，2026-09-29，`frontend-architect`）

主站規劃書 §3.12（`docs/02-frontend-spec.md` 行 228–236）：FAQ 首頁主題分類導覽卡、
關鍵字搜尋、手風琴＋單題深層連結、分類頁具備獨立 SEO、有用回饋回寫後台、搜尋無結果導
10.7 聯絡表單、G-12 嵌入元件（既有，S1-15 已接）。**`GEO-06` FAQPage Schema 是下一輪
`S1-18a`，本輪不做，但資料結構（`FaqItem`／`FaqCategory` 兩個 composable 的回傳形狀）
刻意讓它能直接接上**：每題的 question／answer／slug 已經是攤平的字串欄位，不需要額外
轉換就能塞進 `mainEntity` 陣列。

### 資料來源

後端（`apps/api`）在更早的 S1-6／S1-7a 已經整套建好，本輪**沒有改動 `apps/api` 任何
一行**，純粹接上既有端點：

- `GET /api/v1/faq-categories?lang=` — 全站共用十個主題分類（不分俱樂部，`faq_categories`
  沒有 `club_id`），後端只回 `is_enabled=1`。
- `GET /api/v1/{club}/faqs?category=&keyword=&lang=&page=&pageSize=` — 常見問題列表，
  `club_id` 是 9 張可為空表之一（俱樂部專屬優先、回退共用）。
- `POST /api/v1/{club}/faqs/{slug}/feedback`（body `{ helpful: true|false }`）——規劃書
  明文要求「回饋數據回寫後台」，本輪唯一接上真實寫入的互動。

**種子資料現況**（`db/seed/generate-club-seed-sql.py` 已核對）：十個分類本身已種好
（`join-team`／`academy-admission`／`programs-camps`／`fees-refunds`／`trials`／
`international`／`womens-football`／`fan-club-merchandise`／`partnerships-sponsorship`／
`other`），**但沒有任何一筆真實問答內容被種入 `faqs` 表**——本輪頁面因此在真實環境下
會先顯示「收錄中」空狀態，等後台編輯真的建立題目後才會有內容，這不是本輪的缺漏，是
故意不臆造問答（任務指示明文禁止）。

### 新增檔案

- [`app/composables/useFaqCategories.ts`](app/composables/useFaqCategories.ts)：主題分類，
  fail-open 但不臆造十個固定名稱——API 打不到時回空陣列，不是拿規劃書列的十個主題名稱
  頂替。
- [`app/composables/useFaqList.ts`](app/composables/useFaqList.ts)：常見問題列表，
  `pageSize: 200`（比照 news／schedule「單頁全載」既有慣例），可選 `category` 篩選。
- [`app/components/FaqAccordion.vue`](app/components/FaqAccordion.vue)：手風琴＋深層連結
  ＋有用回饋三段邏輯的共用元件，供首頁（依分類分組後逐組呼叫）與 4 個獨立主題頁共用。
  原生 `<details>/<summary>`，鍵盤操作（Tab／Enter／Space）不需要額外處理。
  `question`／`answer` 任一為 `null` 的題目不渲染（防呆，見元件檔頭）。
- [`app/pages/zh/faq/index.vue`](app/pages/zh/faq/index.vue)：FAQ 首頁，由 S0-9 的十組
  靜態占位改為資料驅動。
- 4 個獨立主題頁（規劃書順序前四項，見下方「先建哪 4 個」）：
  [`app/pages/zh/faq/join-team/index.vue`](app/pages/zh/faq/join-team/index.vue)、
  [`app/pages/zh/faq/academy-admission/index.vue`](app/pages/zh/faq/academy-admission/index.vue)、
  [`app/pages/zh/faq/programs-camps/index.vue`](app/pages/zh/faq/programs-camps/index.vue)、
  [`app/pages/zh/faq/fees-refunds/index.vue`](app/pages/zh/faq/fees-refunds/index.vue)。
  `/en/faq/...` 由 S1-13 的 `pages:extend` 孿生路由機制自動產生，不需要另外新增檔案。

### 改了哪些既有檔案

- [`server/api/backend/[...path].ts`](server/api/backend/%5B...path%5D.ts)：POST 白名單
  新增 `FAQ_FEEDBACK_PATH`（`{club}/faqs/{slug}/feedback` 形狀），維持既有「白名單只放行
  明確需要的路徑形狀」原則，**沒有**放寬到「瀏覽數遞增」（`.../views`，規格未列，本輪
  不做）與「零結果搜尋記錄」（`.../search-misses`，規格未列，本輪不做）兩個既有但沒用到
  的寫入端點。
- [`shared/utils/site-units.ts`](shared/utils/site-units.ts)：`SITE_UNITS` 補上 `12`，讓
  FAQ 首頁納入 `sitemap.xml`／`llms.txt`——過程中發現 `13`（賽事行事曆）自 S1-15 建成起
  就沒有補進這份清單，同一個缺口記錄於 `docs/18-work-errors.md` `E-72`，留給下一個處理
  該單元的人一併修正。

### 先建哪 4 個高頻主題（取捨說明）

STATUS.md 只寫「先建 3–4 個高頻主題」，規劃書沒有明文排序哪幾個優先。本輪選擇規劃書
§3.12 列出的**前四項**：加入球隊、學院招生、課程與營隊報名、費用與退費——這四項同時是
G-12 嵌入元件已經在消費的兩個掛載點（`academy_admission`／`program_detail`，S1-15）的
上游主題，選它們可以讓「獨立主題頁」與「既有嵌入區塊」互相呼應。其餘六個主題（試訓、
國際發展與海外球員、女子足球、球迷會與商品、合作與贊助、其他）本輪仍只在 FAQ 首頁的
分類區塊彙整呈現，沒有各自的獨立頁面與獨立 SEO 設定。

### 單題深層連結的設計取捨：一題只在一個分類出現一次

`FaqItem.categorySlugs` 是陣列——後端刻意支援一題掛多個分類（`FaqsRepository.ListAsync`
檔頭說明）。但規劃書「單題深層連結 `/faq/#q-123`」要求連結全站唯一才有意義（客服才能
放心傳給使用者）。**FAQ 首頁因此把每題指派給「排序最前的一個分類」陳列一次**（見
`app/pages/zh/faq/index.vue` 的 `faqsByCategory`），不是每個分類都各自重複顯示同一題——
多分類標記的用途留給未來的搜尋／嵌入場景使用，首頁顯示本身不重複。

### 藍鯨的已知內容缺口（不是本輪迴歸）

🔴 **本節現況已由「S1-18b」節處理，見檔案最後一節**：`academy-admission`（學院招生）與
`programs-camps`（課程與營隊報名）兩個分類已對藍鯨關閉（獨立主題頁 404、FAQ 首頁不
顯示對應分類卡片），下方描述的「「學院」計入進度計」問題已隨之消失（該分類文字不再
對 bw 輸出）。以下維持原始記錄供對照，不代表現況。

`faq_categories` 沒有 `club_id`、全站共用同一份分類名稱字典，`academy-admission` 分類
的中文名稱是「學院招生」——藍鯨規劃書 §3「04 由學院改為青年隊」是內容取捨，不是這張
分類字典的欄位，本頁沒有臆自改名。`check-club-brand-leak.mjs` 對 bw 容器實測會在
`/zh/faq/`（「學院」×3）與 `/zh/faq/academy-admission/`（「學院」×9）計入禁詞出現次數
——**兩頁都不在 `PROTECTED_PAGES` 保護清單裡，只計數不影響離開碼**，棘輪本身沒有被違反
（已實測，見下方「驗證」）。這是既有資料設計（分類字典跨俱樂部共用）的已知限制，不是
本輪引入的迴歸，是否要讓藍鯨的 FAQ 分類顯示不同名稱留給之後決定資料模型是否要補
`club_id` 覆寫欄位時再處理。

### 驗證

```bash
npm run lint    # 0 錯誤、395 警告（既有基準內，含本輪新檔）
npm run build   # 通過
docker build -f apps/web/Dockerfile apps/web   # 通過
```

本機起 `tcrfc`（3001）／`bw`（3002）兩容器（`apps/api` **未啟動**，依派工規則不自行
啟動、不碰密碼）：

- 兩容器對 `/zh/faq/`、4 個獨立主題頁、`/en/faq/...` 孿生路由**全數 `200`**，無 `500`
  （fail-open：分類與題目 API 打不到時，首頁不顯示任何分類區塊、獨立主題頁顯示「本主題
  常見問題收錄中」空狀態，兩者皆已用 `curl` 實測 HTML）。
- `curl -sI` 兩容器皆有 `X-Robots-Tag: noindex, nofollow`。
- `node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:3001`／`3002`：
  **H1 唯一、標題不跳階皆 0 違規**（164 條路由含 zh／en，其餘為 30x／404 略過）。
- `node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:3002`：**通過**
  （離開碼 0，保護清單 15 頁全數乾淨，棘輪未被違反；FAQ 兩頁的「學院」命中計入進度計，
  細節見上方「藍鯨的已知內容缺口」）。
- `curl -s http://127.0.0.1:3001/sitemap.xml` 與 `/llms.txt`：確認 `/zh/faq/`（含
  `hreflang` 雙語與 x-default）已列入，`bw` 容器的 `/llms.txt` 同樣列出「常見問題」。
- 白名單代理實測（對 `tcrfc` 容器，`apps/api` 未啟動）：
  - `POST /api/backend/tcrfc/faqs/some-slug/feedback`（合法路徑形狀）：回 `500`
    （`ECONNREFUSED`，連線層失敗，確認代理有正確嘗試轉發、不是被誤判 `405`）。
  - `POST /api/backend/tcrfc/faqs/some-slug/views`（規格未列、刻意不放行的路徑）：
    確認仍回 `405`。
  - `POST /api/backend/tcrfc/forms/general/submissions`（既有表單路徑）：確認未被本輪
    的正規表示式異動影響，行為與 S1-17 收尾時一致（回 `500`，同一個連線層原因）。

### 未驗證項目（API 實機驗收未做）

- **搜尋、有用回饋按鈕、深層連結自動展開三段互動行為**——需要真實題目資料才能觀察到
  非空狀態下的畫面，`apps/api` 未啟動、`faqs` 表也還沒有真實種子問答，本輪只能驗證
  程式邏輯（TypeScript 型別檢查＋程式碼審視）與空狀態下的降級渲染，沒有機會端到端驗證
  「輸入關鍵字後篩選結果是否正確」「按讚後 `aria-pressed` 與後端 `helpful_count` 是否
  一致」「`#q-<slug>` 網址是否真的自動展開對應題目」。
- **回饋 POST 是否真的被 `apps/api` 接受並寫入 `helpful_count`／`unhelpful_count`**——
  只驗證了代理白名單正確放行到連線層，沒有機會驗證後端真的收到。
- **10 個分類裡另外 6 個（沒有獨立頁面的那些）在 FAQ 首頁的分組陳列是否正確**——邏輯
  與 4 個有獨立頁的分類共用同一套 `faqsByCategory`／`FaqAccordion`，理論上一致，但沒有
  真實跨分類題目可供實測「一題掛兩個分類時只出現一次」這個防呆是否真的生效。

## S1-18b（藍鯨 FAQ 分類取捨、`E-72` 補完、`SITE_UNITS` 涵蓋度防呆，2026-09-29，`frontend-architect`）

延續 S1-18：主 session 複核後回饋兩件事，本輪處理，範圍限定 `apps/web`（**沒有動
`apps/api` 任何一行**）。

### 1. 藍鯨的 FAQ 不得出現磐石專屬主題

`faq_categories` 沒有 `club_id`，兩站共用同一份十個分類主檔（見 S1-18「藍鯨的已知
內容缺口」節，已標記為過期記錄）。逐一對照藍鯨規劃書 §2.1／§3 後：

- **`academy-admission`（學院招生）／`programs-camps`（課程與營隊報名）明確關閉**：
  藍鯨規劃書 §2.1「總則的例外只有四項單元取捨」明文「04 為青年隊而非學院」「04 青年隊
  沿用主站 04 的梯隊版型，但不沿用招生與課程報名架構」——這兩個分類的問答內容正是
  磐石學院招生流程與磐石 05 課程頁的報名收費架構，同 S1-15 關閉 4.7／5.1／5.2 的理由
  一致。做法：兩個獨立主題頁 `definePageMeta` 的 `unit` 改為細粒度代號 `'12.2'`／
  `'12.3'`（`shared/utils/units.ts` 新增 `BLUE_WHALE_DISABLED_UNITS` 兩筆與
  `FAQ_CATEGORY_UNIT_CODES`／`isFaqCategoryEnabledForClub` 對照表），對藍鯨回 404；
  FAQ 首頁（`app/pages/zh/faq/index.vue`）新增 `visibleCategories` 計算屬性過濾這兩個
  分類，不在導覽卡與分類區塊出現。
- **其餘八個分類（加入球隊／費用與退費／試訓／國際發展與海外球員／女子足球／球迷會
  與商品／合作與贊助／其他）本輪沒有找到明文排除依據，維持對兩俱樂部開放**——取捨
  依據列在下方「規格疑點」，交給使用者裁決是否需要進一步調整。
- **順手修正一個既有問題**：FAQ 首頁的 SEO `description` 原本寫死列出全部十個分類
  中文名稱（含「學院招生」），對藍鯨會固定輸出這個詞、與畫面上已隱藏該分類矛盾，且是
  S1-18 節記錄的「「學院」計入進度計」問題的實際成因。已改為由 `visibleCategories`
  動態組出主題名稱清單，兩俱樂部都不再寫死；同時把頁面顯示的「目前共收錄 N 題」改為
  只計算可見分類底下的題目數（`totalCategorizedCount`），不再把已關閉分類的題目算進
  藍鯨看得到的總數。

### 2. `E-72` 補完：`13`／`10` 補進 `SITE_UNITS`

`docs/18-work-errors.md` `E-72` 記錄「13（賽事行事曆）自 S1-15 建置完成起漏列於
`SITE_UNITS`」，本輪一併檢查發現 **`10`（加入與聯絡，S1-17 建置完成）也同樣漏列**——
兩者都通過 `unit-gate.global.ts`、`curl` 回 `200`，但從未出現在 `sitemap.xml`／
`llms.txt`。已補上兩筆（`shared/utils/site-units.ts`），並移除該檔案裡「留給下一個人」
的暫存措辭，改寫為現況說明（見該檔案檔頭）。

### 3. 新防呆：`SITE_UNITS` 涵蓋度檢查

新增 [`scripts/check-site-units-coverage.mjs`](scripts/check-site-units-coverage.mjs)，
掃描 `app/pages/zh/` 底下所有 `definePageMeta({ unit: 'XX' })`，取每個代號的頂層數字
（如 `'3.1'`→`'03'`、`'10-contact'`→`'10'`），確認頂層代碼要嘛在 `SITE_UNITS`、要嘛在
腳本內 `EXCLUDED_TOP_LEVEL_UNITS`（目前兩筆並附理由：`14` 會員中心／`G-07` 站務法遵
頁面），否則讓 `npm run lint` 失敗。已掛進 `package.json` 的 `lint:site-units-coverage`
（`npm run lint` 鏈的一環）。已用「暫時拿掉 `SITE_UNITS` 裡的 `13`」手動驗證紅燈
（報出 `頂層代碼 '13'` 缺漏）與改回後的綠燈，見下方「驗證」。

### 改了哪些既有檔案

- [`shared/utils/units.ts`](shared/utils/units.ts)：`BLUE_WHALE_DISABLED_UNITS` 新增
  `'12.2'`／`'12.3'`；新增 `FAQ_CATEGORY_UNIT_CODES`／`isFaqCategoryEnabledForClub`。
- [`shared/utils/site-units.ts`](shared/utils/site-units.ts)：`SITE_UNITS` 補上
  `10`／`13`；改寫檔頭說明反映現況。
- [`app/pages/zh/faq/academy-admission/index.vue`](app/pages/zh/faq/academy-admission/index.vue)：
  `unit` 改為 `'12.2'`。
- [`app/pages/zh/faq/programs-camps/index.vue`](app/pages/zh/faq/programs-camps/index.vue)：
  `unit` 改為 `'12.3'`。
- [`app/pages/zh/faq/index.vue`](app/pages/zh/faq/index.vue)：新增 `visibleCategories`／
  `totalCategorizedCount`，`description` 改為動態組字，模板 `v-for` 改讀
  `visibleCategories`。
- [`scripts/check-club-brand-leak.mjs`](scripts/check-club-brand-leak.mjs)：
  `PROTECTED_PAGES` 新增 `/zh/faq/`、`/zh/faq/join-team/`、`/zh/faq/fees-refunds/`
  （15 → 18 頁）。
- `package.json`：新增 `lint:site-units-coverage`，掛進 `lint` 鏈。

### 規格疑點（列出，未自行決定）

1. **「女子足球」分類是否該對藍鯨關閉**：藍鯨規劃書只排除**單元** `06`（自我指涉的
   入口頁，見 §2.1），沒有提到 FAQ 分類字典。這個分類名稱表面上像候選（藍鯨站整站
   就是女足），但沒有明文依據，本輪維持開放。
2. **「費用與退費」分類是否該對藍鯨關閉**：藍鯨規劃書 §4.3「方案與費用｜藍鯨自訂
   （`MembershipPlan.club_id = TCBW`），與磐石各自獨立」——代表藍鯨有自己的會籍費用，
   這個主題對藍鯨仍然適用，本輪維持開放。
3. **「試訓」「國際發展與海外球員」「球迷會與商品」「合作與贊助」「其他」五個分類**：
   規劃書沒有明文提及這幾個分類本身，本輪對照藍鯨既有單元（03 一線隊／04 青年隊／
   08 文化＋商店／09 夥伴需分區）判斷內容性質上都通用，維持開放。若客戶認為某個分類
   下的實際問答內容其實是磐石專屬（例如試訓問答只寫磐石學院試訓流程），需要的是
   「修正該分類底下的題目內容」而不是關閉整個分類——`faqs` 表目前仍是 0 筆種子資料，
   等後台真的建立題目後才看得出實際內容是否合適。

### 驗證

```bash
npm run lint    # 0 錯誤、395 警告（既有基準內，含本輪新檔）
npm run build   # 通過
docker build -f apps/web/Dockerfile apps/web   # 通過
```

本機用同一份映像檔起兩個容器（`NUXT_PUBLIC_CLUB=tcrfc` port 13001／`NUXT_PUBLIC_CLUB=bw`
port 13002 且帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`，**`apps/api` 未啟動**，依派工規則不自行
啟動、不碰密碼）：

- FAQ 頁面狀態碼（`/zh/`／`/en/` 各一輪，共 10 條網址）：`/zh/faq/`、
  `/zh/faq/join-team/`、`/zh/faq/fees-refunds/` 兩俱樂部皆 `200`；
  `/zh/faq/academy-admission/`、`/zh/faq/programs-camps/` tcrfc `200`、bw **依設計
  `404`**（含對應 `/en/`）。
- `isFaqCategoryEnabledForClub` 邏輯直接驗證（node 腳本模擬十個分類 slug）：
  只有 `academy-admission`／`programs-camps` 對 bw 回 `false`，其餘八個對兩俱樂部皆
  `true`——與程式碼設計一致。
- `curl` 兩容器 `/zh/faq/` 的 `<meta name="description">`：`apps/api` 未啟動、分類為
  空陣列時兩者皆輸出「依主題分類整理，目前共收錄 0 題」，**不再出現寫死的「學院招生」
  字面值**（真實環境下分類非空時仍待實機驗證，見下方「未驗證項目」）。
- `sitemap.xml`／`llms.txt`：兩容器皆確認收錄 `/zh/schedule/`（含 `/en/`）與
  `/zh/faq/`，`llms.txt` 「代表頁面」清單兩者皆列出「賽事行事曆」與「常見問題」。
- `node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:13001`／
  `13002`：**H1 唯一、標題不跳階皆 0 違規**（tcrfc 164 條、bw 142 條路由，其餘為
  30x／404 略過——bw 少 22 條是因為新關閉的兩個 FAQ 分類頁 × 2 語系等既有 404 頁面）。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs
  --base-url=http://127.0.0.1:13002`：`exit 0`，保護清單 **18 頁**（15 → 18）全數
  乾淨，棘輪未被違反；`/zh/faq/` 不再計入「學院」命中（原本 ×3，已隨動態 description
  修正消失），`/zh/faq/academy-admission/` 因整頁 404 不再輸出任何內容。
- `node scripts/check-site-units-coverage.mjs`：**紅燈驗證**——暫時刪除
  `SITE_UNITS` 的 `'13'` 那筆，跑出 `頂層代碼 '13'`（來自 `schedule.vue`）缺漏訊息、
  離開碼 `1`；改回後**綠燈**，離開碼 `0`。

### 未驗證項目（`apps/api` 未啟動）

- **真實 `faqs` 種子資料下，八個維持開放分類的實際問答內容是否真的與藍鯨無關**——目前
  `faqs` 表 0 筆種子資料，本輪只能驗證分類層級的開關邏輯，無法驗證題目內容本身，見上方
  「規格疑點」第 3 點。
- **後台新增／修改 FAQ 分類時，`slug` 是否可能被改成與 `FAQ_CATEGORY_UNIT_CODES` 對照
  表不符的值**——目前對照表用 `slug` 字串比對，後台若允許修改既有分類的 `slug`（而非
  只能新增），改了 `academy-admission`／`programs-camps` 的 slug 會讓這個關閉機制悄悄
  失效。本輪沒有查證後台是否允許改既有分類的 `slug`。

## S1-18a（`GEO-06` FAQPage Schema，2026-09-29，`frontend-architect`）

主站規劃書 §7 `GEO-06`「單元 12 與 G-12 一律輸出 FAQPage」；`docs/05-i18n-seo.md` §3
同條。**這是 GEO 的核心資產**，內容規範本身（問題寫成完整句子、答案首句即結論）由
後台編輯負責，前台只負責輸出，不改寫內容。

### 沿用既有機制，接線方式

沿用 S1-12f／S1-12d 已建立的 JSON-LD 輸出機制（`useSchemaOrg`，見
[`app/composables/useSchemaOrgClub.ts`](app/composables/useSchemaOrgClub.ts)），不另起
爐灶。`nuxt-schema-org`（`@nuxtjs/seo` 內建）沒有 `defineFaqPage()` 專用型別，只有
`defineQuestion()`；查原始碼（`node_modules/nuxt-schema-org/dist/schema.mjs`
`questionResolver.resolveRootNode`）確認機制：`defineQuestion()` 節點在 resolve 階段
會找這一頁的 Primary WebPage 節點，只有該節點 `@type` 含 `FAQPage`（或 `QAPage`）時，
才會把這題併入它的 `mainEntity` 陣列。因此輸出 FAQPage 的正確作法是「把這一頁的
WebPage 型別宣告為 `FAQPage`」＋「逐題呼叫 `defineQuestion()`」兩件事一起做。

### 新增檔案

- [`shared/utils/faq-schema.ts`](shared/utils/faq-schema.ts)：不依賴 Vue／Nuxt runtime
  的純函式，供 composable 與檢查腳本共用同一份判斷（單一來源，比照 `useSchemaOrgClub.ts`
  的 E-39 原則）：
  - `cleanFaqSchemaText()`：去除 HTML 標籤（不留空格）、解碼常見實體、壓縮空白，用於
    schema.org 純文字欄位。防禦性處理——`FaqListItemDto.question`／`answer` 目前是純
    文字欄位、`FaqAccordion.vue` 也是文字插值不是 `v-html`，本來就沒有把答案當 HTML
    渲染，這裡只防「萬一混入標籤」。
  - `buildFaqSchemaQuestions()`：GEO-05／GEO-06 判斷——`question`／`answer` 任一為
    `null` 或清理後為空字串則不合格、不輸出；同一題 `id` 出現兩次只保留第一次（同一頁
    多個 FAQ 區塊只輸出一份合併 FAQPage，不重複輸出同一題）；輸入為空時回傳空陣列。
- [`app/composables/useFaqPageSchema.ts`](app/composables/useFaqPageSchema.ts)：接上
  `useSchemaOrg`。沒有合格題目時完全不呼叫（連 `defineWebPage` 都不呼叫），GEO-05
  「資料不足時不輸出該型別」。**已知限制見下方「已知限制」節與 `docs/18-work-errors.md`
  `E-74`**。

### 掛載點：12 FAQ 首頁、4 個獨立主題頁、3 個 G-12 嵌入頁

- [`app/pages/zh/faq/index.vue`](app/pages/zh/faq/index.vue)：把 `faqsByCategory`（未套
  用搜尋關鍵字篩選、每題只指派給第一個可見分類的完整清單）攤平後餵給
  `useFaqPageSchema()`，只輸出一份合併的 FAQPage。用 `faqsByCategory` 而不是套用搜尋
  篩選後的 `visibleByCategory`：搜尋框是 client-side 互動，SSR 輸出的 JSON-LD 應反映
  「這一頁完整收錄的題目」，且 SSR 階段 `search` 恆為空字串，兩者在初始渲染時本來就
  相同。
- 4 個獨立主題頁（[`join-team`](app/pages/zh/faq/join-team/index.vue)／
  [`academy-admission`](app/pages/zh/faq/academy-admission/index.vue)／
  [`programs-camps`](app/pages/zh/faq/programs-camps/index.vue)／
  [`fees-refunds`](app/pages/zh/faq/fees-refunds/index.vue)）：各自只有一個 FAQ 區塊，
  直接把 `useFaqList()` 的完整清單餵給 `useFaqPageSchema()`。
- 3 個 G-12 嵌入頁（[`academy/join.vue`](app/pages/zh/academy/join.vue)／
  [`programs/childrens-training/index.vue`](app/pages/zh/programs/childrens-training/index.vue)／
  [`programs/summer-camp/index.vue`](app/pages/zh/programs/summer-camp/index.vue)）：
  沿用同一份 `useFaqEmbed()` 資料餵給畫面與結構化資料，不另外重打一次 API。

### 注入防護

JSON 字串逸出（雙引號、反斜線、控制字元）交給框架序列化時的 `JSON.stringify()` 處理；
`</script>` 提前結束標籤的防護不是本輪新增的機制——`useSchemaOrg()` 最終透過 `useHead()`
輸出，由 unhead 的 `tagToString()`（`node_modules/unhead/dist/shared/unhead.*.mjs`
`CLOSE_TAG_RE`）對每個 `<script>` 標籤的 `innerHTML` 一律把字面 `</script` 取代成
`<\/script`，這是全站既有 JSON-LD（Organization／SportsTeam／Article／SportsEvent）共用
的框架層保護。`cleanFaqSchemaText()` 的 HTML 標籤去除是另一層、不同目的的資料清理
（見上方「新增檔案」說明），兩者缺一不影響另一個成立。

### 自動檢查

- [`scripts/check-faq-schema.mjs`](scripts/check-faq-schema.mjs)：固定 fixture 資料驗證
  （不需要任何服務就能跑，已掛進 `npm run lint` 的 `lint:faq-schema`）：
  1. `buildFaqSchemaQuestions()` 形狀與過濾（正常題目、null 過濾、標籤去除後為空字串、
     同 id 去重、空陣列）。
  2. `cleanFaqSchemaText()` 獨立驗證（標籤去除、實體解碼）。
  3. **注入防護用 `unhead/server` 匯出的真正 `tagToString()` 驗證**（不是自己重寫一份
     「看起來像」的正規表示式去驗證自己）：對含 `</script>` 提前結束標籤、雙引號、
     反斜線、換行的惡意字面值組出跟正式頁面一模一樣的
     `<script type="application/ld+json">…</script>` 標籤字串，斷言序列化輸出真正收尾
     之前不存在字面 `</script`，且把轉義還原後 `JSON.parse()` 能一字不差還原原始資料。
     這裡刻意不先經過 `buildFaqSchemaQuestions()`（那支函式會把「長得像標籤」的字串
     整段去除，會「順便」清乾淨惡意字串，測不出框架序列化層本身是否安全）。
  新增 `unhead`（`3.4.1`，與專案現有 transitive 版本一致）為 `devDependencies`，供這支
  腳本直接 `import { tagToString } from 'unhead/server'`——Node 24（本專案 `.node-version`
  釘住的版本）原生支援 `import('*.ts')`（type-stripping），這支腳本因此可以直接
  `import '../shared/utils/faq-schema.ts'`，不需要另外編譯或維護一份重複邏輯。
- [`scripts/check-faq-schema-live.mjs`](scripts/check-faq-schema-live.mjs)：對已渲染的
  SSR 輸出檢查（比照 `check-heading-structure.mjs`，需要前台先跑起來，刻意不掛
  `npm run lint`，`E-34`）。抓取 11 個掛載點頁面 zh／en 共 22 條路由的
  `<script type="application/ld+json">`，還原 unhead 的 `</script` 轉義後
  `JSON.parse()`，驗證找到的 FAQPage 節點 `mainEntity` 非空、每題都有 `name` 與
  `acceptedAnswer.text`（**E-74 修正後，`/zh/faq/`／`/en/faq/` 不再有例外，缺
  `mainEntity` 一律 hard-fail**），並額外驗證任一節點不得帶有猜測表裡的
  `AboutPage`／`ContactPage`／`CheckoutPage`／`SearchResultsPage`（見下方
  「`E-74` 修正」）。**已知限制**：`apps/api` 未啟動、`faqs` 表 0 筆種子資料，本機驗收
  時全數路由「沒有 FAQPage」，這是 GEO-05 正確行為，不是缺陷；已用臨時 fixture
  （`smoke-test-1` 假題目，含 HTML 標籤與 `</script>` 字樣，驗證後已還原）手動確認
  「有資料時」的完整輸出正確（見下方「驗證」）。

### `E-74` 修正（2026-09-29，根因在設定層，不在任何一個頁面）

**根因**：`nuxt-schema-org` 的 `webPageResolver.defaults()`
（`node_modules/nuxt-schema-org/dist/schema.mjs`）依「這一頁網址最後一段路徑」
（`endPath = withoutTrailingSlash(meta.url.substring(meta.url.lastIndexOf("/") + 1))`）
猜頁面型別（內建對照表：`about`／`about-us`→`AboutPage`、`search`→`SearchResultsPage`、
`checkout`→`CheckoutPage`、`contact`／`get-in-touch`／`contact-us`→`ContactPage`、
`faq`→`FAQPage`），而這個網址是 `nuxt-site-config` 依 `siteConfig.trailingSlash` 算出來
的（`site-config-stack/dist/urls.mjs` `resolveSitePath()`→`fixSlashes()`）——本站
`nuxt.config.ts` 先前沒有設定這個鍵（預設 falsy），於是**canonical／schema.org 用的網址
一律被去掉結尾斜線**，跟本站實際的 URL 慣例（一律帶結尾斜線，見 hreflang 自我參照與
`server/routes/sitemap.xml.ts` 的既有輸出）不一致，讓 `endPath` 剛好等於猜測表關鍵字。
**受影響的不只 `/zh/faq/`**：盤點全站頁面路徑最後一段，共 4 條 zh 路由（各自的 `/en/`
孿生路由一併中招，共 8 條）撞上猜測表：`/zh/about/`（`AboutPage`）、`/zh/faq/`
（`FAQPage`，S1-18a 發現）、`/zh/checkout/`（`CheckoutPage`）、`/zh/join/contact/`
（`ContactPage`）——四頁都沒有對應的必要欄位（`mainEntity` 等），且都沒有任何頁面程式碼
主動宣告這些型別。全站沒有其餘頁面撞到 `search`／`about-us`／`get-in-touch`／
`contact-us` 這幾個關鍵字。

**修法**：[`nuxt.config.ts`](nuxt.config.ts) 的 `site` 加上 `trailingSlash: true`（見該
檔案行內註解的完整原始碼追查記錄）。這個鍵是 `nuxt-schema-org`／`nuxt-seo-utils` 共用
的同一套 `createSitePathResolver`／`resolveSitePath` 機制唯一的真實來源，設定一次即可
同時修正兩邊：canonical／`og:url`（`applyDefaults.js`）與 schema.org 的
`webPageResolver` 型別猜測（`endPath` 變成空字串，猜測表不再命中，型別退回預設的
`WebPage`）。**不影響**：`server/routes/sitemap.xml.ts`（自組 XML，直接用
`shared/utils/site-units.ts` 的 `unit.path` 字串）與 `app/layouts/default.vue` 的
hreflang（直接用 `route.fullPath`）——這兩處本來就不經過 `resolveSitePath`，也本來就
已經帶結尾斜線；副作用是**修正了一個先前沒被注意到的既有落差**：canonical（先前不帶
斜線）與同一頁的 hreflang 自我參照（`route.fullPath`，本來就帶斜線）互相矛盾，現在
兩者一致。

**試過但沒有用的修法**（`E-74` 原記錄，仍然成立，設定層修好後不需要這條路）：在
`useFaqPageSchema()` 的空狀態顯式呼叫
`defineWebPage({'@type':'WebPage', _dedupeStrategy:'replace'})` 覆寫，用本機真實 SSR
輸出＋`console.error` 除錯確認**無效**：`nuxt-schema-org` 對同一個 `@id` 多節點合併的
`@type` 是陣列聯集、`_dedupeStrategy` 只判斷當前合併進來的節點，頁面自己的覆寫永遠贏
不了框架的猜測結果——這也是為什麼這次選擇修設定層而不是逐頁覆寫。

**防呆**：[`scripts/check-faq-schema-live.mjs`](scripts/check-faq-schema-live.mjs) 移除
了原本只列資訊行的 `/zh/faq/`／`/en/faq/` 白名單，改成通用 hard-fail：任何 FAQPage 缺
`mainEntity`一律離開碼 `1`；任何節點帶有 `AboutPage`／`ContactPage`／`CheckoutPage`／
`SearchResultsPage`（本站目前沒有任何頁面的宣告機制會產生這些型別）也一律離開碼 `1`
——這條檢查同時涵蓋「猜測表又回來了」（例如這次的 `trailingSlash` 設定被還原）與
「有人新增了未經宣告機制產出的特殊型別」兩種情況。

### 驗證

```bash
npm run lint    # 0 錯誤、395 警告（含 lint:faq-schema，見上方「自動檢查」）
npm run build   # 通過
docker build -f apps/web/Dockerfile apps/web   # 通過
```

本機起 `tcrfc`（3001）／`bw`（3002）兩容器（`apps/api` 未啟動，依派工規則不自行啟動、
不碰密碼）：

- `curl` 抽查 `/zh/about/`／`/zh/faq/`／`/zh/checkout/`／`/zh/join/contact/`（含各自
  `/en/` 孿生路由）：**修正前**四頁 `@type` 皆含猜測表型別（`AboutPage`／`FAQPage`／
  `CheckoutPage`／`ContactPage`）且無 `mainEntity`；**修正後**（`site.trailingSlash:
  true`）八條路由 `@type` 全部退回 `"WebPage"`，`<link rel="canonical">` 全部改為帶結尾
  斜線（例如 `https://tcrfc.tw/zh/about/`），與同頁 hreflang 自我參照網址一致。
  `bw` 容器同樣核對（`https://bw-stg.tcrfc.tw/...`），且 `WebSite.name` 正確顯示
  `台中藍鯨`（未外洩 `TCRFC`）。
- `curl -sI` 抽查：`X-Robots-Tag: noindex, nofollow` 仍在（`/zh/`／`/zh/about/`）。
- `curl -s .../sitemap.xml`／`.../robots.txt`：輸出與本次修改前一致（自組 XML／
  `Disallow: /`），不受 `trailingSlash` 設定影響。
- `node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:3001`／`3002`：
  **H1 唯一、標題不跳階皆 0 違規**（`3002` 額外列出 4 頁購物流程骨架首段摘要缺漏，不
  影響離開碼，屬既有內容缺口）。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs
  --base-url=http://127.0.0.1:3002`：**exit 0**，保護清單 18 頁全數乾淨，棘輪未被違反。
- `node scripts/check-faq-schema-live.mjs --base-url=http://127.0.0.1:3001`／`3002`：
  **exit 0**（tcrfc 22 條路由全檢查、bw 12 條，其餘 404 跳過）；全站無 FAQPage 輸出
  （GEO-05 正確行為，`faqs` 表 0 筆種子資料）；`/zh/about/`／`/zh/checkout/`／
  `/zh/join/contact/` 三頁不再命中猜測表型別。
- **手動 fixture 驗證「有資料時」的完整輸出**（臨時在 `join-team` 頁塞一筆假題目，
  含 `<b>標籤</b>` 與 `</script>` 字樣，驗證後已還原、不留在程式碼裡）：SSR 輸出正確
  產生 `"@type":["WebPage","FAQPage"]`、`"mainEntity":[{"@id":".../question/1"}]`，
  獨立的 Question 節點 `name`／`acceptedAnswer.text` 皆為清理過的純文字（標籤已去除、
  `</script>` 字樣已去除），無注入痕跡。

### 過程中發現並修正：`E-73`

驗收時 `check-club-brand-leak.mjs` 對 `bw` 容器回報「棘輪被違反」，但檔案本身完全沒有
改動。追查是 `checkRatchet()` 的正規表示式把 S1-18b 加進 `PROTECTED_PAGES` 陣列裡的
說明**註解**（提到 `'12.2'`／`'12.3'` 這兩個單引號字串）誤判成陣列元素，跟自己（HEAD）
比對都會失敗。已修正（去除 `//` 行內註解再抓值、收斂為只認 `/` 開頭的字串），完整記錄
見 `docs/18-work-errors.md` `E-73`。

### 規格疑點

無——`GEO-06` 規格單純（單元 12 與 G-12 一律輸出 FAQPage），本輪沒有遇到規劃書未列或
與其他規格衝突的情況。

## S1-19（13 賽事行事曆——隊別分頁改依俱樂部動態產生，`.ics` 逸出修正，2026-09-29，`frontend-architect`）

`app/pages/zh/schedule.vue` 本身（隊別分頁、賽程／賽果切換、列表／月曆檢視、單場 `.ics`
下載）在 S0-9／S1-12d 已經建好，S1-12d 收尾時明確記錄「逐隊代碼篩選仍寫死磐石代碼，
留給 S1-19」——本輪就是處理這個已知缺口，以及審這頁時一併發現的幾個既有問題。
**沒有動 `apps/api` 任何一行**（依派工規則），對照後端既有公開端點（`Features/Schedule`／
`Features/Calendar`）欄位形狀決定畫面能不能接，只用請求參數與回應形狀驗證，不啟動服務。

### 1. 隊別分頁改依俱樂部動態產生（主要任務）

**改動前**：`TEAM_TABS` 是一個寫死磐石代碼（`D1`／三個梯隊代碼／`club`）的常數陣列。
bw 容器套用同一份程式碼時，分頁按鈕會拿磐石的隊別代碼去查 `matches.teamCode`，藍鯨的
賽事一筆都查不到（`BW1` 不是第二個 `D1`，`docs/14-invariants.md` 既有踩雷點），且會多
顯示一個藍鯨沒有的梯隊分頁。

**改動後**：新增計算屬性 `teamTabs`，由兩個既有單一來源組出：

| 分頁 | 來源 |
|---|---|
| `全部`／`俱樂部活動` | 兩俱樂部共同的固定分頁（沿用既有） |
| 一線隊 | 新增 `getFirstTeamCode(club)`（`shared/utils/club.ts`）：磐石 `D1`、藍鯨 `BW1` |
| 各梯隊 | 既有 `getAcademyTeamTabs(club, facts)`（4.2 學院隊伍頁的單一來源，底層事實來自 GEO-03 `useSiteFacts().squadCodes`），排除其中 `teamCode: null` 的「其他年齡層」靜態說明分頁——賽事行事曆的分頁必須對應真實可查詢的 `Team.code` |

實測（`curl` SSR 輸出，`data-team-filter` 屬性）：
- tcrfc：`all`／`D1`／`U15`／`U14`／`U12`／`club`
- bw：`all`／`BW1`／`BW-U15`／`BW-U12`／`club`（**沒有 `U14`、沒有 `D1`**）

`getFirstTeamCode()` 同時取代 `app/pages/zh/index.vue`（S1-14）原本內嵌的
`club === 'bw' ? 'BW1' : 'D1'` 三元運算式，收斂為單一來源，避免第三個地方又各自寫一份
（同一個判斷式已經在兩個檔案各寫一次，是規劃書 v3.13、`docs/14` 反覆提醒的那類坑）。

隊別頭部標題（`teamHeadName`）、無資料文案（`emptyDesc`）、鍵盤導覽（`onTabKeydown`）
三處原本各自手刻對照 `D1`／`U15`／`U14`／`U12` 四個字面值的邏輯，一併改為讀
`teamTabs`／`firstTeamCode`，不再依賴固定隊別集合。

### 2. 修正 fixture-card 與相關連結原本的字面寫死俱樂部名稱（發現的既有缺口）

審上述隊別分頁時發現，即使分頁能正確篩到藍鯨的賽事，賽事卡片本身「我方」一側與頁尾
「相關連結」CTA 卡片仍會顯示錯誤的俱樂部資訊：

| 位置 | 改動前 | 改動後 |
|---|---|---|
| fixture-card「我方」隊徽圖 | 字面寫死 `/assets/brand/svg/tcrfc-mark-pink.svg` | `clubAssets.headerMark.src`（`getClubAssets(club)`） |
| fixture-card「我方」隊名 | 字面寫死「台中磐石」 | `clubAssets.shortNameZh` |
| `.ics` `DESCRIPTION` | 字面寫死「請以台中磐石足球俱樂部官方公告為準」 | 依 `clubAssets.nameZh` 動態組字 |
| CTA 卡片「一線隊 First Team」 | 兩俱樂部皆顯示英文附標 | 只有磐石顯示（藍鯨英文正式全名尚未確認，不得自行選一個顯示，見 `docs/13-blue-whale-site.md` 紀律 11） |
| CTA 卡片「學院隊伍」 | 兩俱樂部皆顯示磐石單元名稱 | 依 `isTcrfc` 顯示「學院隊伍」／「青年隊」（藍鯨規劃書 §3.4） |

已用 `check-club-brand-leak.mjs` 對 bw 容器實測 0 筆命中（含 `/en/schedule/`），
`/zh/schedule/` 已加入該腳本的 `PROTECTED_PAGES`（18 → 19 頁）。

### 3. `.ics` 逸出規則補齊、行折疊、UID／PRODID 依俱樂部

**改動前的 `icsEscape` 只處理分號與逗號**：

```ts
function icsEscape(s: string): string {
  return s.replace(/[;,]/g, (c) => `\\${c}`)
}
```

沒有處理反斜線本身與換行字元。這是可被資料內容觸發的兩個問題：① 反斜線沒有優先逸出，
若欄位本身含反斜線，會跟後續逸出規則加上的反斜線混在一起、無法正確還原；② 換行字元
（`\r`／`\n`）完全沒有處理，若後台可自由輸入的文字欄位（例如 `matches.opponent`／
`venue`）意外含有真實換行，會被行事曆應用程式解讀成 `.ics` 的下一個屬性行，破壞整份
檔案結構——這是注入風險，不是理論疑慮。

已改為比照 `apps/api/Common/IcsBuilder.cs` 的 `Escape()`（後端既有、已測試過的單場
`.ics` 端點 `GET /api/v1/{club}/matches/{id}/ics` 用的同一套規則）：反斜線最先逸出，
接著分號、逗號，最後把 `\r\n`／孤立 `\n` 都轉成逸出後的字面 `\n`：

```ts
function icsEscape(s: string): string {
  return s
    .replace(/\\/g, '\\\\')
    .replace(/;/g, '\\;')
    .replace(/,/g, '\\,')
    .replace(/\r\n/g, '\\n')
    .replace(/\n/g, '\\n')
}
```

同時新增 `foldIcsLine()`（RFC 5545 §3.1 行折疊：內容行以 UTF-8 位元組計超過 75 就要
折行，延續行以單一空白開頭，且不得從多位元組字元中間切斷），比照同一份 `IcsBuilder.cs`
的 `FoldLine()`。中文全名（俱樂部＋對手＋聯賽）疊在同一行很容易超過 75 位元組，不折行
可能被部分行事曆應用程式截斷或誤判。`downloadIcs()` 逐行折疊並補上規定的 `\r\n`
（含最後一行）。

其餘依俱樂部動態化：`UID` 網域原本寫死 `@tcrfc.tw`，改用 `siteConfig.url` 的
hostname（`uidHost`，取不到時退回 `tcrfc.tw`／`bw.tcrfc.tw`）；`PRODID` 原本固定
`-//TCRFC//`，改依俱樂部代碼；下載檔名前綴原本固定 `tcrfc-`，改為 `${club}-`。

**驗證方式**（純函式邏輯，不需要瀏覽器）：另外用 Node 腳本複製同一段
`icsEscape`／`foldIcsLine` 邏輯驗證：
- 一段人造惡意內容（含反斜線、分號、逗號、`\r\n`）逸出後，`(?<!\\);`／`(?<!\\),`／
  裸露 `\r`／裸露 `\n` 皆確認清除（找不到任何未逸出的分隔符或裸露換行）。
- 一段刻意超過 75 位元組的中文標題折成 3 行，每行皆 ≤ 75 位元組，重新接回（去除延續行
  開頭補的空白）後與原字串逐字元相同，且沒有 U+FFFD 替代字元（確認多位元組字元沒有
  被從中間切斷）。

**未驗證**：瀏覽器實際下載 `.ics` 後用真實行事曆應用程式（Google Calendar／Apple
行事曆／Outlook）開啟匯入是否正確顯示——`apps/api` 未啟動、本機沒有真實賽程資料可下載，
只驗證了純邏輯層。

### 4. 月曆檢視：同一天多場賽事的既有資料流失問題、鍵盤與無障礙補強

審「月曆檢視要可用鍵盤操作、符合無障礙」這條要求時，發現 `renderCalendar()` 用
`new Map(byMonth.get(key)!.map((ev) => [ev.day, ev]))` 把同一天的賽事陣列轉成
`Map<日期, 單一事件>`——**如果同一天有兩場賽事（例如磐石一線隊與某個梯隊同一天都有
比賽），`Array.map` 建構 Map 時後面的會覆蓋前面的，第二場會悄悄從月曆消失**，使用者
點擊該日期永遠只能跳到被保留的那一場。已改為 `Map<日期, 事件陣列>`（`grouped`），
同一天有多場賽事時全部可達，日期格顯示小數字角標（`cal-day__count`）並在點擊時把
當天全部場次一併納入強制顯示集合（原本的 `forcedVisibleId: ref<string|null>` 改為
`forcedVisibleIds: ref<Set<string>>`），對應規劃書 v3.13 §3.13「點擊日期展開當日賽事」
的原文用語（複數）。

其餘無障礙補強：
- 月曆日期連結補上 `aria-label`（組合月份、日期、對手、「查看詳情」），原本只有滑鼠
  `title` 屬性（螢幕閱讀器不保證讀出）；星期標題列與空白墊格標成 `aria-hidden="true"`
  （純視覺輔助，實際資訊已在每個日期連結的 `aria-label` 裡）。
- `:focus-visible` 補上可見的外框樣式（原本只有底線變化，鍵盤使用者不容易注意到目前
  焦點在哪一格）。
- 月曆日期是原生 `<a href="#...">`，Tab 鍵可依 DOM 順序逐一到達、Enter 鍵原生觸發
  `click`，本來就可鍵盤操作；本輪沒有另外實作 `role="grid"` 的方向鍵導覽——那是一個
  更完整的 ARIA grid widget（需要 roving tabindex ＋ 方向鍵切格），沒有一併實作反而會
  比純 `<a>` 清單更容易誤導螢幕閱讀器（宣告了 grid 語意卻沒有對應的鍵盤互動），評估後
  判斷維持現況的線性 Tab 導覽更安全。

**同時修正一個 HTML 注入風險**：月曆是用字串組 `innerHTML`（非 Vue 樣板，原始設計如此，
理由見檔案頂端既有註解），`ev.opponent`（後台可自由輸入的文字欄位）原本未經任何逸出
就直接插入 `title` 屬性與文字節點。新增 `escapeHtml()`，所有插入 `innerHTML` 字串的
動態值（對手名稱、由 `fixtureId()` 程式產生的 id）一律先過這一層。

**延賽原定時間**：`postponedNote()`（`app/utils/schedule.ts`）在 S0-9l 已經實作且本頁
既有樣板已經在用，本輪沒有變動；確認畫面上延賽賽事卡片會顯示「原定 YYYY-MM-DD HH:mm」
副標（見該函式檔頭「只用人造資料驗證過」的既有備註，資料庫目前沒有真實延賽賽事）。

### 改了哪些檔案

- [`app/pages/zh/schedule.vue`](app/pages/zh/schedule.vue)：本輪唯一大改的頁面，
  上述 1–4 項全部在這個檔案內。
- [`shared/utils/club.ts`](shared/utils/club.ts)：新增 `getFirstTeamCode(club)`。
- [`app/pages/zh/index.vue`](app/pages/zh/index.vue)：`firstTeamCode` 改呼叫上面
  這個新的單一來源函式，取代原本內嵌的三元運算式（純收斂，行為不變）。
- [`scripts/check-club-brand-leak.mjs`](scripts/check-club-brand-leak.mjs)：
  `PROTECTED_PAGES` 新增 `/zh/schedule/`（18 → 19 頁）。

### 規格疑點與範圍縮減（列出，未自行決定／未做）

1. **「俱樂部活動」分頁本輪仍是裝飾性、沒有真實資料可顯示**：審這頁時發現
   `apps/api` 其實已經有一支更完整的公開端點 `GET /api/v1/{club}/calendar/events`
   （`Features/Calendar/CalendarRepository.cs`），能合併 `matches` 與公開的
   `calendar_custom_events`（L2 自建活動），且已經處理好 `team=club` 這個分頁的查詢
   模式與月曆模式的重複規則展開——但這支端點比這個頁面晚建成（`calendar_custom_events`
   相關 migration 是 2026-09-25，本頁面主體是 S0-9 時期建的），前台從未接上。本次任務
   指示的「要求」清單沒有列這項（只列了隊別分類、`.ics`、月曆鍵盤與無障礙、延賽原定
   時間四項），且改用這支端點會把目前「一次抓 200 筆賽事、全部交給前端做篩選」的架構
   換成「依篩選條件、伺服器端分頁」的架構，是比本輪範圍大很多的重構（會牽動深層連結、
   月曆涵蓋範圍、批次 `.ics` 下載等既有行為），評估後判斷不在本輪動手，原樣保留「俱樂部
   活動」分頁的既有空狀態文案，把這支既有端點的存在與用途記在這裡，留給下一個處理這個
   分頁的人。**確認過 `db/seed` 目前沒有任何 `calendar_custom_events` 種子資料**，所以
   這個分頁維持空白不會造成內容從有變沒有的退步。
2. **時區顯示文案與實際行為的既有落差（沒有改，回報用）**：頁面 Hero 文字宣稱「所有
   時間皆依瀏覽器所在時區顯示」，但 `matchWeekday()`／`matchDay()` 等既有工具函式
   （`app/utils/schedule.ts`）是直接用 `Date.UTC` 解析 `matchOn` 純日期字串，顯示的
   星期／日期／時間是資料庫存的台灣本地牆上時間字面值，並沒有依瀏覽器時區換算——這是
   S0-9 搬遷時就存在的既有實作選擇（比照球隊官網賽程頁的常見慣例，賽事時間本來就是主辦
   單位公告的當地時間，不是要海外球迷各自換算的時間戳），跟 `.ics` 的 `DTSTART`／
   `DTEND`（`toUtcIcs()`）確實有做台北時間→UTC 換算是兩回事。這次任務要求的「.ics
   時區正確」已核對無誤（見上方第 3 節），但頁面文字與畫面顯示的落差本輪沒有動——
   改動畫面顯示邏輯本身超出本次任務範圍（隊別分類／`.ics`／月曆／延賽四項），只記錄
   在這裡供裁決是否要調整文案措辭或實際換算邏輯。
3. **API 實機驗收未做**：本輪依規則沒有啟動 `apps/api`，`/schedule`、`/matches/{id}/ics`
   兩支端點的欄位形狀只用原始碼比對（`Features/Schedule/MatchDto.cs`、
   `Features/Calendar/CalendarEndpoints.cs`），沒有真實資料可以驗證畫面實際渲染結果——
   本輪能驗證的是「程式邏輯正確」（純函式測試、SSR 輸出的分頁代碼與文案），不是「真實
   賽程資料下畫面長什麼樣子」。

### 驗證

```bash
npm run lint    # 0 錯誤、395 警告（與既有基準相同，未增加）
npm run build   # 通過
docker build -f apps/web/Dockerfile apps/web   # 通過
```

本機用同一份映像檔起兩個容器（`NUXT_PUBLIC_CLUB=tcrfc`／`NUXT_PUBLIC_CLUB=bw` 且帶
`NUXT_PUBLIC_SITE_NAME=台中藍鯨`，**`apps/api` 未啟動**，依派工規則不自行啟動、不碰
密碼）：

- `/zh/schedule/`、`/en/schedule/` 兩容器皆 `200`，`X-Robots-Tag: noindex, nofollow`
  仍在。
- `data-team-filter` 屬性：tcrfc 為 `all`／`D1`／`U15`／`U14`／`U12`／`club`；
  bw 為 `all`／`BW1`／`BW-U15`／`BW-U12`／`club`（**沒有 `D1`、沒有 `U14`**）。
- 分頁按鈕可見文字：tcrfc「一線隊 First Team」；bw 只有「一線隊」（無英文附標）。
- `<meta name="description">`：tcrfc「……依隊別（一線隊／U15／U14／U12）分類……」；
  bw「……依隊別（一線隊／U15／U12）分類……」（自動反映藍鯨少一個梯隊年齡層）。
- CTA 卡片標題：tcrfc「一線隊 First Team」「學院隊伍」；bw「一線隊」「青年隊」。
- `node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:<port>`：
  兩容器皆 **H1 唯一、標題不跳階 0 違規**。
- `node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:<bw 容器 port>`：
  **`exit 0`**，保護清單 **19 頁**（18 → 19，新增 `/zh/schedule/`）全數乾淨，棘輪未被
  違反；`/zh/schedule/` 修正前曾出現「學院×1」命中（CTA 卡片字面寫死「學院隊伍」），
  修正後 0 筆。
- `node scripts/check-site-units-coverage.mjs`：通過（本輪沒有新增或修改
  `definePageMeta` 的 `unit`，行為不變）。
- `curl` 兩容器 `sitemap.xml`：皆確認收錄 `/zh/schedule/`／`/en/schedule/`。
- `.ics` 逸出與行折疊邏輯：見上方第 3 節「驗證方式」，用抽出的純函式邏輯以 Node
  腳本驗證（不需要瀏覽器或真實賽程資料）。

### 未驗證項目

- 真實賽程資料下的畫面渲染（`apps/api` 未啟動，見「規格疑點」第 3 點）。
- 瀏覽器實際下載 `.ics` 並用真實行事曆應用程式開啟匯入。
- 月曆檢視的鍵盤導覽與 `aria-label` 內容，只用原始碼審查與 SSR 輸出比對確認邏輯正確，
  沒有用螢幕閱讀器（VoiceOver／NVDA）實機朗讀測試。
- 同一天多場賽事的月曆顯示（目前種子資料是否真的有同日多場賽事未查證，此為邏輯層修正，
  防止未來出現這種資料時失效，不代表目前已有這種資料可供肉眼核對畫面）。

## S1-19 補完（主 session 對照規劃書 §3.13 逐條複查後要求補齊，2026-09-29，`frontend-architect`）

主 session 對照規劃書 §3.13 全文複查上一節的交付，指出四項規格明文要求但尚未做的項目。
本節記錄補完內容，**只改 `apps/web`**（沒有動 `apps/api`、`nuxt.config.ts`，`apps/api`
未啟動）。

### 1. 時區換算（§3.13「時區處理」／「左側時間欄」）

**改動前**：頁首文案宣稱「所有時間皆依瀏覽器所在時區顯示」，但賽事卡片實際顯示的是
資料庫存的 Asia/Taipei 牆上時間字面值，從未真正換算——文案與行為不一致。

**改動後**：`app/utils/schedule.ts` 新增：
- `matchInstantUtc(dateStr, kickoff)`：把 Asia/Taipei 牆上時間換算成真正的 UTC
  `Date`（收斂自原本 `schedule.vue` 的 `toUtcIcs()` 內嵌邏輯，兩處共用同一個換算來源）。
- `matchTimeDisplay(dateStr, kickoff, viewerTimeZone)`：`viewerTimeZone` 傳 `null`
  時直接回傳既有的 `matchWeekday()`／`matchDay()`／`matchMonthAbbr()`／原始 `kickoff`
  字面值，**完全不呼叫 `Intl`**；不是 `null` 且不等於 `Asia/Taipei` 時才用
  `Intl.DateTimeFormat` 換算。
- `instantTimeDisplay(instant, viewerTimeZone)`：給俱樂部活動（真正的 UTC 時間戳）用
  的對應版本。

**Hydration 安全設計**：`schedule.vue` 新增 `viewerTimeZone = ref<string | null>(null)`，
只在 `onMounted()` 用 `Intl.DateTimeFormat().resolvedOptions().timeZone` 偵測一次。
SSR 與 client 掛載前的第一次渲染都會用 `viewerTimeZone === null` 這條路徑，輸出與
改動前逐字元相同（不呼叫 `Intl`），保證不會有 hydration mismatch；掛載後才可能更新
顯示（若瀏覽器時區不是台灣），這次更新發生在 hydration 完成之後，是正常的反應式 DOM
patch。已用本機容器實測 SSR 輸出：`viewerTimeZone`／`nextMatchCountdown` 兩個 client-only
欄位相關的段落（`v-if="mounted && ..."`）在 SSR body 裡完全不存在（只出現在 `<style>`
區塊的 CSS 選擇器字面值裡，不是實際 DOM 元素），確認掛載前後的 DOM 結構一致。

頁首文案改為規劃書原文用語「**所有時間為當地時間，可能異動**」，並新增一行 client-only
提示（偵測到非台灣時區時才顯示）：「已依您目前的裝置時區（`{時區}`）換算顯示；台灣
官方公告時間請見各賽事詳情。」

**範圍縮減（明確決定，列出理由）**：月曆分組（`monthGroups`）、賽事卡片錨點 id
（`fixtureId()`）、`.ics` 的 `UID`、深層連結 `#fx-...` 全部維持用**資料庫存的原始
台灣日期**，不隨顯示時區改變——這些是穩定識別碼與既有分享連結依賴的基準，若隨瀏覽器
時區重新分組會讓同一場賽事在不同時區的使用者眼中被分到不同月份群組、id 改變會讓已經
分享出去的連結失效。換算只發生在「這張卡片顯示的星期／日期／時刻」這個純顯示層級，
不影響資料組織方式。

### 2. 俱樂部活動（§3.13「資料來源」第二列、分頁規則第 2 點）

**改動前**：「俱樂部活動」分頁已存在於隊別分頁清單，但沒有接任何資料來源，永遠顯示
空狀態文案。

**改動後**：接上既有 `GET /api/v1/{club}/calendar/events?team=club`（`apps/api`
`Features/Calendar/CalendarRepository.cs` 既有的公開端點，在這個頁面主體建成
（S0-9／S1-12d 時期）之後才由另一條工作線建成，但前台從未接上）。

**整合方式的決定（coordinator 明確授權自行決定並記錄）**：club events 用獨立的
`clubEvents`／`visibleClubEvents` 計算屬性與獨立的卡片樣板（`.club-event-card`，沿用
`.fixture-card` 的既有版面骨架），附加在既有賽事列表之後，**不併入** `matches`／
`monthGroups`／`visibleMatches` 那條既有管線。理由：
- 後者牽動月曆檢視、批次 `.ics` 下載、`SportsEvent` JSON-LD、深層連結等大量已驗證過的
  既有邏輯，把兩種形狀不同的資料（比賽 vs. 自建活動）塞進同一個型別與同一組篩選函式，
  需要把 `cardMatches`／`isCardHidden`／`renderCalendar`／`sportsEvents` 全部改寫成能
  處理判別聯集型別，是遠大於「補一個分頁的資料」這件事本身的重構，且會讓已經在
  S1-19 主輪驗證過的既有賽事邏輯重新暴露在迴歸風險下。
- `db/seed` 目前沒有任何 `calendar_custom_events` 種子資料，維持獨立管線不會犧牲任何
  已知的真實內容，之後真的需要「賽事與活動在同一份月份分組列表裡逐日交錯」時，再視
  實際內容量評估要不要合併。
- 「全部」檢視包含俱樂部活動：`showClubEvents` 計算屬性讓 `state.team === 'all'` 時
  一併顯示；「賽果」模式不顯示（後端 `ListClubEventsAsync` 檔頭明講「俱樂部活動沒有
  『賽果』的語意」，`showClubEvents` 據此排除 `results` 模式）。

**已知範圍縮減**：月曆檢視本輪未涵蓋俱樂部活動（只在列表檢視顯示）——`renderCalendar()`
仍然只讀 `visibleMatches`，維持原樣不動，降低對既有已驗證邏輯的觸碰面。批次 `.ics`
下載（`onBulkIcs`）也維持只打包賽事，不含俱樂部活動（後端目前也沒有自建活動的 `.ics`
端點，見下一節）。這兩項都是本輪的明確取捨，留給下一個真正有俱樂部活動內容時的人評估
是否要擴大範圍。

### 3. 依隊別訂閱（§3.13「加入我的行事曆」、分頁規則第 3 點）

**查證結果**：`apps/api` 只有單場 `.ics` 下載端點
（`GET /api/v1/{club}/matches/{id}/ics`，`Features/Calendar/CalendarEndpoints.cs`），
**沒有任何 webcal／訂閱 feed 端點**。`STATUS.md` `S2-6`（`L3`／`L4` 行事曆進階：分軌
檢視、衝突偵測、拖曳改期、**訂閱匯出**）排在階段 2，尚未開發——這不是本輪查漏，是
既有規劃就還沒排到的工作。**前台沒有自行產生假的訂閱網址**（依指示）。

實際做的：
- **下一場賽事倒數**（規劃書原文「選定隊別後，頁面標頭顯示：隊伍名稱、下一場賽事
  倒數」的可實作部分）：`computeNextMatchCountdown()` 從已載入的 `matches` 找出目前
  選定隊別最近的一場「未開始」賽事，算出天／小時／分鐘差距。**同樣是 client-only**
  （`nextMatchCountdown` 初始 `null`，只在 `onMounted` 與之後每分鐘的 `setInterval`
  更新）——倒數文字是「現在時刻」與賽事時刻的相對差，SSR 渲染當下與瀏覽器 hydration
  完成當下必然相差數十到數百毫秒，若 SSR 就算好文字會跟 client 掛載後重新計算的文字
  不一致，是另一種 hydration mismatch 來源，處理原則與時區換算一致：非確定性內容一律
  延後到掛載後才計算並顯示。
- **誠實的訂閱狀態說明**：選定特定隊別（非「全部」／「俱樂部活動」）時，頁面標頭新增
  一行文字，明講「該隊別專屬的行事曆訂閱（webcal）網址尚未上線（後端訂閱 feed 端點
  待開發）」，並指向頁面下方既有的「訂閱賽程」區塊使用 `.ics` 下載——不是新造的說詞，
  既有「訂閱賽程」區塊的既有文案本來就已經誠實說明這件事，這裡只是在使用者實際點選
  隊別、最可能想找訂閱功能的當下，把同一句誠實說明前移到看得到的地方。

**回報給後端／下一輪的待補項目**：`L4` 需要新增至少一支「依隊別（`Team.code`）持續
產生 `.ics` feed」的公開端點（`GET /api/v1/{club}/teams/{teamCode}/schedule.ics` 之類
的形狀），且要考慮 webcal 快取／更新頻率與 `IQueryCache` 現有機制的關係——本輪只查證
「不存在」，沒有進一步設計這支端點的形狀，留給 `S2-6` 真正動手時處理。

### 4. §3.13 其餘明文項目逐條核對

| 項目 | 狀態 | 說明 |
|---|---|---|
| 賽季篩選 | ✅ 本輪補上 | 原本是 `disabled` 的裝飾用下拉選單，字面寫死「2026/27 賽季」——這對藍鯨是錯的事實（藍鯨兩個球季代碼是「2023」「2025」，不是「2026/27」，且藍鯨 21 場歷史賽果橫跨這兩個不同球季，原本的篩選功能對藍鯨完全不可用）。改為從 `matches` 既有回應的 `seasonCode` 欄位（`MatchDto.SeasonCode`，API 一直都有回，前台介面原本沒有宣告、也從未使用）動態算出可選賽季清單（`availableSeasons`），預設選最新一季（`defaultSeason`），真正可回溯往季，不需要新增後端端點。順手修正 `teamHeadMeta`／SEO `description` 兩處原本同樣字面寫死「2026/27」的既有落差。 |
| 賽事類型篩選 | ✅ 既有 | `state.comp`（全部／聯賽／盃賽／友誼賽／其他），S0-9 既有，本輪未變動。 |
| 主客場篩選 | ✅ 既有 | `state.ha`（全部／主場／客場），S0-9 既有，本輪未變動。 |
| 動作按鈕：賽事詳情 | ✅ 本輪補上 | 規劃書「賽事卡片欄位」表非條件式列出，原本完全沒有這顆按鈕。站內沒有任何獨立的單場賽事詳情頁路由，比照規劃書「前台功能」表「事件詳情｜側邊抽屜或彈窗」，實作為原生 `<dialog>` 彈窗（`detailDialog`／`detailMatch`／`openDetail()`／`closeDetail()`）——原生 focus trap、Escape 關閉、`::backdrop`，不需要手刻鍵盤陷阱，內容重用既有欄位（對戰、時間、場地＋地圖連結、狀態、延賽原定時間、`.ics`／分享按鈕）。 |
| 動作按鈕：購票（若有） | ⬜ 維持不顯示，非漏做 | `MatchDto` 沒有票務欄位，站內也沒有商店與賽事的關聯機制，規劃書本身標「若有」，資料不存在時不顯示是正確行為。 |
| 動作按鈕：轉播資訊（若有） | ⬜ 維持不顯示，非漏做 | 同上，`MatchDto` 沒有轉播／直播連結欄位，規劃書標「若有」。 |
| 行動版次要篩選收合 | ✅ 本輪補上 | 賽季／賽事類型／主客場三個下拉原本在窄螢幕會直接换行擠在一起，沒有收合機制。新增「篩選」按鈕（`filtersOpen`，`aria-expanded`），窄螢幕（`max-width:720px`）預設收合、點擊展開；桌面版不受影響（純 CSS media query 控制預設可見度，按鈕本身在桌面版隱藏）。`filtersOpen` 初始值 `false` 在 SSR／掛載前 client 端第一次渲染皆相同，不影響 hydration。 |
| 延賽顯示原定時間 | ✅ 既有，本輪確認未回歸 | `postponedNote()`（`app/utils/schedule.ts`，S0-9l 既有）維持不變，本輪新增的時區換算邏輯不影響這個欄位的顯示（`originalMatchOn`／`originalKickoff` 是獨立欄位，不經過 `matchTimeDisplay()`）。 |

### 改了哪些檔案

- [`app/utils/schedule.ts`](app/utils/schedule.ts)：新增 `TAIPEI_TIME_ZONE`／
  `matchInstantUtc()`／`MatchTimeDisplay`／`matchTimeDisplay()`／`instantTimeDisplay()`
  五個匯出，純新增，未改動既有匯出的簽章與行為。
- [`app/pages/zh/schedule.vue`](app/pages/zh/schedule.vue)：本輪唯一大改的頁面
  （時區偵測與顯示、俱樂部活動管線、賽季篩選、下一場倒數、賽事詳情彈窗、行動版篩選
  收合，全部在這個檔案內）。

### 驗證

```bash
npm run lint    # 0 錯誤、395 警告（與既有基準相同，未增加——過程中新增的 3 個
                # attributes-order 警告已逐一修正回到基準內）
npm run build   # 通過
docker build -f apps/web/Dockerfile apps/web   # 通過
```

本機用同一份映像檔起兩個容器（`NUXT_PUBLIC_CLUB=tcrfc`／`NUXT_PUBLIC_CLUB=bw` 且帶
`NUXT_PUBLIC_SITE_NAME=台中藍鯨`，**`apps/api` 未啟動**，依規則不自行啟動、不碰密碼）：

- `/zh/schedule/`、`/en/schedule/` 兩容器皆 `200`，`X-Robots-Tag: noindex, nofollow`
  仍在。
- 頁首文案已改為「所有時間為當地時間，可能異動」（`curl` 確認逐字元存在）。
- `viewerTimeZone`／`nextMatchCountdown` 相關的 `<p v-if="mounted && ...">` 段落
  **確認不出現在 SSR 輸出的 body 裡**（只在 `<style>` 選擇器字面值出現），驗證
  「client-only 內容延後渲染」的設計確實落地，不是空談。
- 賽季下拉：`apps/api` 未啟動、`matches` 為空陣列時，正確退化為只有「全部賽季」一個
  選項且 `disabled`（`availableSeasons.length === 0` 時停用），沒有殘留寫死的
  「2026/27 賽季」字樣。
- `<dialog class="match-detail">` 元素存在於 SSR 輸出（初始為空，`v-if="detailMatch"`
  內容只在點擊「賽事詳情」後才有）。
- 「篩選」收合按鈕（`.sched-filters-toggle`）存在於 SSR 輸出，`aria-expanded="false"`
  初始值正確。
- `node scripts/check-heading-structure.mjs --base-url=...`：兩容器皆 **H1 唯一、
  標題不跳階 0 違規**（彈窗內容因 `v-if="detailMatch"` 初始為 `false`，不在初始 DOM
  中，不影響此檢查）。
- `node scripts/check-club-brand-leak.mjs --base-url=...<bw 容器>`：**`exit 0`**，
  保護清單 19 頁（含 `/zh/schedule/`）維持全數乾淨，棘輪未被違反。
- `node scripts/check-site-units-coverage.mjs`：通過（本輪未新增或修改 `unit` meta）。
- `curl` 兩容器 `sitemap.xml`：仍收錄 `/zh/schedule/`／`/en/schedule/`，未受影響。
- Docker 容器日誌（`docker logs`）確認除了既有的「`apps/api` 連不上」既有降級路徑
  （`ECONNREFUSED`，`/schedule`／`/calendar/events` 兩個端點皆同一種既有模式，新增
  的 `/calendar/events` 呼叫只是同一種既有降級模式多了一個端點，不是新的失敗型態）
  以外，沒有任何 `Vue warn`／`TypeError`／`ReferenceError`。

### 未驗證項目（延續上一節，新增本輪特有的）

- **真實 `.ics` feed／webcal 訂閱**：後端本來就沒有這支端點，無從驗證，已列為後端
  待補（見上方第 3 節）。
- **非台灣時區的實際換算結果**：本機瀏覽器／容器時區皆為預設（多半是 UTC 或系統
  時區），沒有實際切換系統時區跑一次瀏覽器並肉眼核對換算後的星期／日期／時刻是否
  正確；只驗證了「SSR／掛載前不呼叫 `Intl`、掛載後才可能更新」這個 hydration-safe
  的結構本身。
- **下一場賽事倒數的即時性**：`setInterval` 每分鐘更新一次的邏輯只用原始碼審查確認，
  沒有掛在瀏覽器裡實際等一分鐘看文字是否真的更新。
- **原生 `<dialog>` 的 focus trap 與 Escape 關閉**：這是瀏覽器原生行為（不是本輪自己
  刻的邏輯），依 HTML 標準規格應該正確，但沒有實機鍵盤操作測試確認。
- **真實俱樂部活動資料下的畫面**：`db/seed` 目前沒有任何 `calendar_custom_events`
  種子資料，`.club-event-card` 樣板只經過原始碼審查，沒有真實內容可供肉眼核對版面。

## S1-20（`GEO-05` 結構化資料第二批：SportsEvent、Event、Course，2026-09-29，`frontend-architect`）

主站規劃書 §7「結構化資料 Schema Markup」型別清單：`SportsEvent`（既有）補齊「場地與
地址」；`Event`（俱樂部活動）／`Course`（課程）兩型別為本輪新增輸出。前置 `S1-19`
（賽事行事曆、俱樂部活動已接上）與 `S1-15`（05 課程頁已接真實 Program API）皆已完成。

### 既有 `SportsEvent` 輸出的盤點與處置

`app/pages/zh/schedule.vue` 的 `sportsEvents` 計算屬性（S0-9j／S1-12c 既有）已經涵蓋：
主客隊（`homeTeam`／`awayTeam`／`competitor`，依 `homeAway` 判斷己方隊名讀
`getClubAssets(club).nameZh`）、開始時間（`startDate` 固定接 `+08:00`——Asia/Taipei
全年無日光節約，資料庫存的就是牆上時間字面值，這個固定偏移本來就正確，不需要換算）、
`eventStatus`（`matchStatusSchemaOrg()`，與畫面 `status-pill` 共用同一份
`MATCH_STATUS_MAP`，S0-9j 已收斂單一來源）。**本輪只補一項**：場地目前只有
`location.name`（比賽場地文字）與寫死的 `addressCountry: 'TW'`，沒有街址。改為用
`m.venue`（自由文字）比對既有 `useSiteFacts(club).facts.value.venues`（GEO-03 單一
來源，已含 `nameZh`／`address`）找出對應地址，找到才加 `streetAddress`，找不到維持
只有 `addressCountry`（不臆造）。**沒有重複輸出**：確認過本輪沒有另外新增第二處
`SportsEvent` 節點（首頁 `match-band` 區塊顯示同一批賽事資料，但決定不重複輸出，
見下方「規格疑點」第 1 點）。

### `Event`（俱樂部活動）

`nuxt-schema-org` 有專用的 `defineEvent()` 定義器（不像 `SportsEvent` 沒有專用型別
要手刻原始 JSON-LD，見 `useSchemaOrgClub.ts` 檔頭既有說明），直接使用：

- [`shared/utils/schema-batch2.ts`](shared/utils/schema-batch2.ts) 的
  `isClubEventSchemaEligible()`／`buildClubEventSchemaNodes()`：純函式，`name`／
  `startDate`／`location`（`venueName`）三者齊全才合格——鏡射
  `apps/api/Features/Seo/SchemaCompleteness.cs` 的 `SchemaType.Event` 必填欄位
  （該檔行 116–121），逐筆判斷，不合格的那一筆不輸出、其餘合格的仍要輸出。
  `url` 錨點對應樣板新增的 `:id="ce-{id}"`（原本 `.club-event-card` 沒有任何可定位的
  id）。
- [`app/composables/useClubEventSchema.ts`](app/composables/useClubEventSchema.ts)：
  接上 `useSchemaOrg`／`defineEvent`，寫法比照 `useFaqPageSchema.ts`。
- 掛載點：`app/pages/zh/schedule.vue`，餵入**未經 client 端篩選的完整清單**
  `clubEvents`（不是 `visibleClubEvents`）——跟既有 `sportsEvents` 用 `matches`
  （不是 `visibleMatches`）同一個既有理由：SSR 輸出應反映「這一頁完整收錄的資料」
  （比照 S1-18a FAQPage schema 的同一原則）。

**過程中發現並修正一個框架陷阱（`inheritMeta`）**：查 `node_modules/nuxt-schema-org/
dist/schema.mjs` 的 `eventResolver`，其 `inheritMeta: ['inLanguage', 'description',
'image', {meta:'title', key:'name'}]` 會在節點缺 `description`／`image` 鍵時，自動
拿「這一頁的 SEO meta description／預設 OG 圖」頂替（`setIfEmpty()` 只在鍵值為
`undefined` 時才生效）——用臨時 fixture 實測驗證到這個行為：沒有專屬說明或封面圖的
活動，會被冠上一整份跟這個活動毫無關係的全站預設圖文，正是「只輸出有真實資料的欄位」
要防的事。**修法**：`buildClubEventSchemaNodes()` 改為沒有真實資料時明確填 `null`
（不是省略鍵），序列化前的 `stripNullProperties(ctx.nodes[i])`（同檔案已查證，在
`resolveRootNode` 之後、輸出前對每個節點遞迴呼叫）會把值為 `null` 的鍵整個移除，
最終輸出不會出現 `"image":null` 這種殘缺欄位，也不會被框架的預設值頂替。**`Course`
沒有這個問題**：`courseResolver`（同檔案）沒有宣告 `inheritMeta`。

### `Course`（課程）

- [`shared/utils/schema-batch2.ts`](shared/utils/schema-batch2.ts) 的
  `isCourseSchemaEligible()`／`buildCourseSchemaNode()`：`name`／`description`
  （`intro`）兩者齊全才合格——鏡射 `SchemaCompleteness.cs` 的 `SchemaType.Course`
  必填欄位（該檔行 141–145）。`provider` 固定為俱樂部本身（`getClubAssets(club).nameZh`
  ＋ `siteConfig.url`），任務指示原文「provider 為俱樂部」，且後端同一份註解確認
  `provider.name` 本來就「永遠有值，不列為必填判斷」。`educationalLevel` 只在
  `ageMin`／`ageMax`（`ProgramDetailDto` 既有欄位）兩者皆有值時才附上，組成
  「6–12 歲」這種文字——只輸出有真實資料的欄位，不臆造年齡範圍。
- [`app/composables/useCourseSchema.ts`](app/composables/useCourseSchema.ts)：接上
  `useSchemaOrg`／`defineCourse`。
- 掛載點：[`app/pages/zh/programs/childrens-training/index.vue`](app/pages/zh/programs/childrens-training/index.vue)／
  [`app/pages/zh/programs/summer-camp/index.vue`](app/pages/zh/programs/summer-camp/index.vue)
  （S1-15 已接真實 `programDetail`，本輪只是餵給新的 composable）。兩頁對藍鯨已整頁
  404（`units.ts` 的 `'5.1'`／`'5.2'`，S1-15 既有決定），不需要俱樂部分支。

### 為什麼三種型別的合格判斷不直接讀後端算好的欄位（已知落差）

`MatchDto.SchemaEligible`／`ArticleDetailDto`（S1-12c／S1-12f 既有）都是後端算好、
前台只讀布林值的既有模式（E-39「單一來源」）。但 `PublicCalendarEventDto`（俱樂部
活動）與 `ProgramDetailDto`（課程）**目前都沒有對應的 `SchemaEligible` 欄位**——
`SchemaCompleteness.cs` 已經把 `SchemaType.Event`／`SchemaType.Course` 的必填欄位
單一來源宣告好，只是還沒有任何公開端點真的呼叫它算出這兩型別的布林值。本輪依派工
規則不改 `apps/api`，`shared/utils/schema-batch2.ts` 的 `isClubEventSchemaEligible()`／
`isCourseSchemaEligible()` 是前台暫時鏡射同一份必填欄位判斷（逐一對照
`SchemaRequiredFields.ByType` 的欄位鍵，沒有新增或放寬任何一條）——**回報**：後端
補上這兩個布林值後，前台應該改回直接讀後端欄位，不再自行判斷，比照 `SportsEvent`／
`Article` 的既有模式。

### 新增檔案

- [`shared/utils/schema-batch2.ts`](shared/utils/schema-batch2.ts)：三種型別共用的
  純函式單一來源（見上方各節）。
- [`app/composables/useClubEventSchema.ts`](app/composables/useClubEventSchema.ts)、
  [`app/composables/useCourseSchema.ts`](app/composables/useCourseSchema.ts)：
  Nuxt／`useSchemaOrg` 接線。
- [`scripts/check-schema-batch2.mjs`](scripts/check-schema-batch2.mjs)：固定 fixture
  驗證（比照 `check-faq-schema.mjs` 既有先例，已掛 `npm run lint` 的
  `lint:schema-batch2`），涵蓋：`venueAddressByName()` 名稱比對／`isClubEventSchemaEligible()`
  與 `buildClubEventSchemaNodes()` 的 GEO-05 過濾與輸出形狀（含「只輸出有真實資料的
  欄位」用明確 `null` 防止框架 `inheritMeta` 頂替）／`isCourseSchemaEligible()` 與
  `buildCourseSchemaNode()` 的同一套判斷／注入防護（`unhead/server` 真正的
  `tagToString()` 驗證 `</script>` 提前結束標籤與 JSON 逸出，做法比照
  `check-faq-schema.mjs`，刻意繞過清理函式直接構造「已合格待輸出」的原始節點，
  測框架序列化層本身而不是巧合借用清理副作用）。共 22 項斷言，全數通過。

### 改了哪些檔案

- `shared/utils/schema-batch2.ts`（新增）
- `app/composables/useClubEventSchema.ts`（新增）
- `app/composables/useCourseSchema.ts`（新增）
- `scripts/check-schema-batch2.mjs`（新增）
- `app/pages/zh/schedule.vue`：`sportsEvents` 的 `location.address` 補上
  `venueAddressByName()` 查找；新增 `useClubEventSchema()` 呼叫；`.club-event-card`
  樣板補上 `:id="ce-{id}"` 錨點。
- `app/pages/zh/programs/childrens-training/index.vue`／
  `app/pages/zh/programs/summer-camp/index.vue`：新增 `useCourseSchema()` 呼叫。
- `package.json`：`lint` 新增 `lint:schema-batch2` 步驟。

### 驗證

```bash
npm run lint    # 0 錯誤、395 警告（與既有基準相同，含新增 lint:schema-batch2）
npm run build   # 通過
docker build -f apps/web/Dockerfile apps/web   # 通過
node scripts/check-schema-batch2.mjs   # 22 項斷言全數通過
```

本機起 `tcrfc`／`bw` 兩容器（`apps/api` 未啟動，依派工規則不自行啟動、不碰密碼）：

- `/zh/schedule/`、`/zh/programs/childrens-training/`、`/zh/programs/summer-camp/`
  （含各自 `/en/` 孿生路由）：tcrfc 全部 `200`；bw 的 `schedule` 為 `200`、兩個課程頁
  依既有單元開關為 `404`（S1-15 既有決定，非本輪迴歸）。`X-Robots-Tag: noindex,
  nofollow` 三頁皆在。
- **`apps/api` 打不到時（GEO-05 正確行為）**：三頁的 JSON-LD 皆只有 `WebSite`／
  `WebPage` 兩個節點，沒有 `SportsEvent`／`Event`／`Course`，也沒有殘缺節點。
- **「有資料時」的真實輸出**（比照 S1-18a 既有先例，臨時 fixture 手動驗證、驗證後已
  還原，程式碼裡不留任何測試資料）：暫時覆寫 `schedule.vue` 的 `data.value`（人造一筆
  賽事，`venue: '西屯足球場'`）與 `clubEventsData.value`（人造一筆俱樂部活動），暫時
  覆寫兩個課程頁的 `programDetail.value`；`npm run build` 後用
  `node .output/server/index.mjs` 起服務（不透過 Docker，較快）curl 驗證：
  - `SportsEvent.location.address.streetAddress` 正確顯示「台中市北屯區崇平路二段
    景谷巷 11 弄 41 號」（比對 `venueAddressByName()` 找到「西屯足球場」）。
  - `Event` 節點正確輸出，`url` 為 `.../zh/schedule/#ce-test-event-1`，與樣板
    `:id="ce-test-event-1"` 對應。
  - **驗證 `inheritMeta` 陷阱修法確實生效**：把人造活動的 `description`／`coverUrl`
    改成 `null` 後，SSR 輸出的 `Event` 節點**完全沒有** `description`／`image` 兩個
    鍵（框架沒有拿全站預設 OG 圖／SEO 說明頂替）；改回有值時兩個鍵正確顯示人造內容。
  - `Course` 節點在兩個課程頁面（`ageMin`/`ageMax` 皆有值／皆為 `null` 兩種情況）
    皆正確輸出，`educationalLevel` 依前者才出現。
- `node scripts/check-heading-structure.mjs --base-url=...`：兩容器皆 H1 唯一、標題
  不跳階 0 違規（沿用既有 22 條路由清單，未擴充，本輪未改動標題結構）。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs
  --base-url=...`：`exit 0`，保護清單 19 頁（含 `/zh/schedule/`，S1-19 既有）全數
  乾淨，棘輪未被違反——本輪未新增受保護頁面（課程頁對 bw 是 404，不需要加入保護清單）。
- `node scripts/check-site-units-coverage.mjs`：通過（本輪未新增或修改 `unit` meta）。
- Docker 容器日誌：除既有的 `apps/api` 連不上降級路徑外，無 `Vue warn`／
  `TypeError`／`ReferenceError`。

### 未驗證項目

- 真實資料下的畫面渲染（`apps/api` 未啟動，只用臨時 fixture 驗證過一次結構正確性）。
- 非台灣時區使用者看到的 `SportsEvent.startDate`（固定 `+08:00` 偏移）實際效果——
  邏輯上這是 ISO 8601 帶時區偏移的絕對時間戳，任何時區的消費端（搜尋引擎、行事曆
  應用程式）都應該正確換算，理論上不需要額外處理，但沒有實際跨時區環境驗證。
- `Event`／`Course` 兩型別在真實種子資料（`calendar_custom_events`／`programs` 兩表
  現況皆 0 筆）下的實際輸出，需等後台建立資料後才能用 `apps/api` 真實驗證。

### 需要後端補的欄位

- `PublicCalendarEventDto`（`apps/api/Features/Calendar/CalendarDto.cs`）：補上
  `SchemaEligible` 布林值（依 `SchemaCompleteness.cs` 的 `SchemaType.Event` 判斷），
  取代前台目前的鏡射判斷。
- `ProgramDetailDto`（`apps/api/Features/Programs/ProgramDtos.cs`）：補上
  `SchemaEligible` 布林值（依 `SchemaType.Course` 判斷），同上。

### 規格疑點（列出，未自行決定）

1. **首頁「最新賽事區」是否要重複輸出 `SportsEvent`**：規劃書只列型別清單，沒有
   逐頁指定。首頁 `match-band` 區塊（S1-14）顯示的「最新戰績／上一場／下一場」正是
   `schedule.vue` 輸出過的同一批賽事資料。本輪判斷**不重複輸出**——比照 S1-12f
   對 `Organization`／`SportsTeam`／`BreadcrumbList` 已經建立的既有慣例「一種型別
   在全站選一個判斷最合適的位置輸出，不是每個提到該實體的頁面都各自輸出一次」，
   且 `schedule.vue` 的 `url` 欄位已經是這批賽事的 canonical 網址。留給下一次覆查
   規格時確認這個判斷是否要調整。
2. **`educationalLevel` 的格式**：規劃書沒有規定 Course 的 `educationalLevel` 要怎麼
   表示，本輪用 `ProgramDetailDto` 既有的 `ageMin`／`ageMax` 組成「6–12 歲」文字，
   是本輪判斷，不是規格明文格式。

## S2-8（03.2–03.5 球員發展、球員機會、國際發展通道、球員故事，2026-09-29，`frontend-architect`）

主站規劃書 §3.3（03 FOOTBALL CLUB 的 3.2–3.5 四個子單元）。前置 `S1-15`（03.1 一線隊）
已完成。派工指示額外要求一併處理「S1-15 回報的已知缺口」：`academy/{pathway,curriculum,
coaches,life}.vue`（4.3–4.6）與 `club/opportunities/index.vue` 的藍鯨品牌外洩，一併在
本節處理（不算在 03.2–03.5 範圍內，但同一批交付）。

### 各頁資料來源與藍鯨取捨

| 頁面 | 對藍鯨 | 依據 |
|---|---|---|
| 3.2 球員發展系統 | 🔴 **關閉**（新增 `units.ts` `'3.2'`） | 內容是「台中磐石球員發展系統」八大模組這個具名的內部培訓框架，屬於磐石自己的機構性宣稱——`content/blue-whale/` 全部既有舊站內容盤點都沒有藍鯨對應的具名系統可引用，換抬頭字樣就宣稱藍鯨也有這套框架等於臆造機構事實，不是換配色 |
| 3.3 球員機會 | 🟢 **開放**，改讀真資料與 club-copy 文案 | 藍鯨規劃書 §1.3「主站有的功能，本站就有；主站沒有的，本站也不做」「總則的例外只有四項單元取捨，四項以外不得另行設計」（行 24、92），本頁不在四項例外之列。內容本身無磐石專屬事實（試訓表格兩俱樂部皆通用空白狀態），「歡迎外籍球員」邀請文字有真實史實佐證（`club-profile.md` 沿革歷年招募日本／泰國／香港／美國籍球員），不是臆造 |
| 3.4 國際發展通道 | 🔴 **關閉**（新增 `units.ts` `'3.4'`） | 內容 100% 是磐石一線隊真實海外合作俱樂部（Hellas Verona／Rayo Ciudad Alcobendas／Rot-Weiss Ahlen）與真實旅外球員（楊朝景，香港九龍城）——沒有真實藍鯨內容可換 |
| 3.5 球員故事 | 🟢 **開放**，藍鯨版改為空狀態 | 藍鯨規劃書 §3.3「一線隊」明文「沿用主站 03 的球員卡、球員頁與球員故事版型」——版型承諾沿用，但沒有已核實、已取得肖像同意的藍鯨球員故事案例可用，故不挪用磐石球員（孫恩祈／山內大空／楊朝景）充數，改為 0 案例的誠實空狀態 |
| 4.3 學院發展路徑 | 🟢 **開放**，改為細粒度 `unit '4.3'` | 全部既有內容是「準備中」通用佔位文字，沒有磐石專屬真實事實，只換抬頭與 CTA 連結即可 |
| 4.4 訓練課程與課綱 | 🟢 **開放**，改為細粒度 `unit '4.4'` | 同上，五大訓練面向（技術／戰術／體能／比賽判讀／品格）與課綱表格皆為通用佔位內容 |
| 4.5 學院教練團 | 🔴 **關閉**（新增 `units.ts` `'4.5'`，改為細粒度 `unit`） | 磐石學院三位真實教練（徐翊／許志傑／黃聖傑，含真實照片），不能顯示成藍鯨教練；`coaching-staff.md` 雖有鄭雅薰／李彥廷等人「曾任」U15 教練的舊站原文，但該檔案標明「舊站教練經歷最新只到 2024，2025 賽季未更新」且無照片，教練異動不是「創立年份」那種不會過期的事實，風險與臆造相近 |
| 4.6 學院生活 | 🔴 **關閉**（新增 `units.ts` `'4.6'`，改為細粒度 `unit`） | 13 張磐石學院學員（未成年）真實訓練／比賽照片，不能挪用成藍鯨——藍鯨沒有對應、已核實肖像同意的青年隊照片可用（既有缺口，同 `S1-16`「缺內容清單」） |

> 🔴🔴 **已修正（BW-C1，2026-09-29）**：上表把 `3.2`／`3.4`／`4.5`／`4.6` 標「關閉」
> 違反藍鯨規劃書 §1.3 總則「例外只有四項單元取捨」——「藍鯨沒有對應真實內容」是內容
> 缺漏，不是關閉整頁的理由。四頁已於 BW-C1 重開：`3.2`（球員培育重點文案，避免對藍鯨
> 宣稱「系統」框架）、`3.4`（`club-profile.md` 沿革其實有真實旅外案例：蔡明容／程思瑜／
> 蘇育萱旅外日本、蘇育萱旅外中國，本輪當時只查了「有沒有合作俱樂部 Logo」沒查沿革）、
> `4.5`／`4.6`（教練名單／訓練影像維持既有「收錄中」空狀態，不放任何名字或照片）。
> 下方「規格疑點」第 1 點已由 BW-C1 處理，見本檔「BW-C1」節與
> [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-76`。

### 3.3 球員機會：修正既有品牌外洩

`club/opportunities/index.vue` 改動前固定呼叫 `useSiteFacts('tcrfc')`——單元 `'3.3'`
本來就沒關閉，藍鯨容器過去會直接顯示磐石的聯賽名稱與文案，是本輪盤點發現的既有缺口
（不是本輪新增的迴歸）。改為依 `clubKey` 動態抓取；「加入」／「外籍球員招募」兩段正文
改為 `club-copy.ts` 工廠函式 `getJoinFirstTeamBody()`／`getForeignPlayerBody()`；新增
`useFaqEmbed(club, 'trials', locale)` 消費 `FAQ_EMBED_SLOTS` 既有掛載點「試訓頁
（3.3）」（`db/seed` 早已定義、`S1-15` 尚未消費）並掛 `useFaqPageSchema()`（GEO-06）。

### 3.5 球員故事：藍鯨空狀態的具體做法

`club-copy.ts` 新增 `getPlayerStoriesHero()`／`getPlayerStoriesSeo()`／
`getPlayerStoriesEmptyNote()`。藍鯨版空狀態說明**不重複**磐石版指向藍鯨官網的自我指涉
句子（「台中藍鯨的球員名單與賽程請見台中藍鯨女子隊官網」——這句在藍鯨自己的站上會變成
「藍鯨站告訴藍鯨訪客去藍鯨官網」的自我循環），改寫為單純的「案例陸續建立中」。

### 既有品牌外洩連帶修正：導覽選單與 03／04 hub 頁的死連結

本輪關閉 3.2／3.4／4.5／4.6 四個單元後，發現以下頁面（不在 03.2–03.5 範圍內，但是
本輪改動的直接後果）對藍鯨會連到現在會 404 的頁面，一併修正：

- `app/components/SiteHeader.vue`：03／04 mega menu 的子項目改為
  `v-if="isUnitEnabledForClub('X.Y', club)"` 個別判斷。**順手發現並修正既有缺口**：
  05 mega menu 的 5.1／5.2（`S1-15` 已關閉）與 04 mega menu 的 4.7（既有 `S1-15`
  已關閉）子項目改動前完全沒有判斷，藍鯨選單一直連到會 404 的頁面，不是本輪新增的
  迴歸。
- `app/pages/zh/club/index.vue`（03 單元 hub 頁，本身仍是既有的 `S1-12d` 記錄缺口，
  整頁固定磐石內容，本輪未擴大範圍）：新增 `isTcrfc` 判斷，`v-if` 掉連到 3.2／3.4
  的兩張單元卡片與一張底部 CTA 卡片，不讓本輪的關閉連帶產生新的死連結。
- `academy/pathway.vue`／`academy/curriculum.vue`：底部 CTA 卡片 `v-if` 掉連到 4.5／
  4.6／4.7 的卡片（4.7 是既有缺口，`S1-15` 關閉後這兩頁从未補上判斷）。

### 驗證指令與實際結果（2026-09-29）

```
npm run lint    # 0 errors, 395 warnings（等於既有基準上限，未超過）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機起 `tcrfc`（port 14001）／`bw`（port 14002，帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`）
兩容器，`apps/api` 未啟動（依派工規則不自行啟動、不碰密碼）：

- 11 個改動頁（3.2–3.5、4.3–4.6）的 `/zh/`／`/en/` 狀態碼（共 22 條網址 × 2 容器＝
  44 條）逐一 `curl` 全部符合單元開關設計：tcrfc 全數 `200`；bw 的 3.3／3.5／4.3／4.4
  為 `200`，3.2／3.4／4.5／4.6 為 `404`。`X-Robots-Tag: noindex, nofollow` 全數皆在。
- `node scripts/check-heading-structure.mjs`：`tcrfc`（164 條路由）／`bw`（130 條
  路由）皆 H1 唯一、標題不跳階 0 違規。
- `node scripts/check-site-units-coverage.mjs`：通過（本輪 unit 代號改動皆仍在既有
  頂層代碼下，未新增頂層代碼）。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs
  --base-url=http://127.0.0.1:14002`：**通過**，保護清單新增 4 頁（`opportunities`／
  `player-stories`／`academy/pathway`／`academy/curriculum`，共 24 頁）全數乾淨，
  棘輪未被違反。
- `node scripts/check-faq-schema-live.mjs`：路由清單新增 `club/opportunities`（本輪
  一併新增消費 `useFaqPageSchema` 的頁面），`apps/api` 未啟動情境下 0 個 FAQPage
  節點（GEO-05「資料不足時不輸出」正確行為），JSON 皆可解析。
- Docker 容器日誌：除既有的 `apps/api` 連不上降級路徑外，無 `Vue warn`／
  `TypeError`／`ReferenceError`。

🔴 **未驗證項目**：真實資料下（`faqs`／`programs` 表皆 0 筆種子資料）的實際渲染效果，
`apps/api` 未啟動，只驗證了「API 打不到時優雅降級」這條路徑。

### 規格疑點（列出，未自行決定）

1. **4.5／4.6 是否真的該關閉，或應該用 `coaching-staff.md` 的舊教練異動紀錄開放**：
   本輪判斷「可能過期的真實資料風險與臆造相近」而關閉，但這是本輪的判斷，不是規格
   明文——`coaching-staff.md` 確實有鄭雅薰／李彥廷「曾任」U15 教練的舊站原文，若客戶
   確認現況仍適用，可以改為開放。
2. **3.3「外籍球員招募」子區塊藍鯨規劃書沒有明文提及**：本輪判斷開放（理由見上表），
   但藍鯨規劃書 §3.3「一線隊」只提到球員卡／球員頁／球員故事三種版型沿用，沒有單獨
   點名「球員機會」（試訓／外籍招募）整個 3.3 子單元，是本輪依總則（「四項以外不得
   另行設計」）推論，不是逐頁明文指定。

## S2-10（05.3–05.5 冬令營、專項訓練、校園社區，2026-09-29，`frontend-architect`）

主站規劃書 §3.5（05 PROGRAMS 的 5.3–5.5 三個子單元）。前置 `S1-15`（5.1／5.2）已完成，
其 README 節「規格疑點」第 2 點明確把 5.3–5.5 的藍鯨取捨留給本輪評估，不是照抄同一個
關閉決定。

### 各頁資料來源與藍鯨取捨

| 頁面 | 對藍鯨 | 資料來源 | 依據 |
|---|---|---|---|
| 5.3 冬令營 | 🔴 **關閉**（新增 `units.ts` `'5.3'`） | 🟢 真資料：`GET /{club}/programs?type=winter_camp`（同 5.2 既有做法） | 與 5.2 共用同一份資料模型／版型（規劃書明文「同 5.2 結構」），`content/blue-whale/programs.md` 的舊站內容盤點沒有對應的「寒假營隊」產品，與 5.1／5.2 同一個關閉理由 |
| 5.4 專項訓練 | 🔴 **關閉**（新增 `units.ts` `'5.4'`） | 🟢 真資料：`GET /{club}/programs?type=specialist_training`，新增「目前開放報名的專項」區塊；六大專項介紹卡片維持既有靜態內容 | 現況內容是磐石男子一線隊球員真實訓練照片與「台中磐石成人足球訓練營」具名宣傳文案；藍鯨唯一沾得上邊的是社區推廣性質的「藍鯨守門員基礎班」（`programs.md` §2 第 10 項，7–12 歲兒童班），規模與定位都不是同一種六大專項競技訓練產品 |
| 5.5 校園與社區 | 🟢 **開放**，藍鯨版改讀真實建教合作內容 | 🟢 真資料：`GET /{club}/programs?type=school_community`；「合作學校列表」藍鯨版讀 `club-copy.ts` `SCHOOL_PARTNERS_BW`（5 校，逐字節錄 `club-profile.md` §1「建教合作」欄）；「社區計畫」／「教練培訓」讀 `COMMUNITY_PROGRAM_BODY_BW`／`COACH_TRAINING_BODY_BW`（節錄 `programs.md` §2／§4） | `content/blue-whale/club-profile.md` §1「建教合作」欄有 5 校真實名單、`programs.md` §2／§4 有真實社區推廣（運動 i 台灣 2.0）與教練講習內容，與本頁三個子區塊直接對應，有真實內容可換，不需要關閉 |

> 🔴🔴 **已修正（BW-C1，2026-09-29）**：上表把 `5.3`／`5.4` 標「關閉」違反藍鯨規劃書
> §1.3 總則「例外只有四項單元取捨」——下方「規格疑點」第 1 點當時已經點出這個張力，
> 但選擇維持關閉、留給下一輪裁決；BW-C1 依總則明文重新判斷後改為重開：`5.3` 冬令營
> 沒有找到對應真實活動，改為誠實空狀態；`5.4` 專項訓練改為只呈現真實對應的「藍鯨
> 守門員基礎班」，不套用磐石六大專項框架。詳見本檔「BW-C1」節與
> [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-76`。

三頁皆新增 `useFaqEmbed(club, 'program_detail', locale)`（同 5.1／5.2 既有掛載點）＋
`useFaqPageSchema()`（GEO-06）；5.3／5.4 對藍鯨已 404，掛載點呼叫本身無副作用（頁面
本來就不會渲染），不需要額外判斷。`useCourseSchema()`（GEO-05／S1-20 既有介面）三頁
皆接上，provider 固定為俱樂部本身；5.4 因為 API 可能回傳多筆專項課程，但
`useCourseSchema` 現況只接單一課程物件（S1-20 既有介面），比照 5.1／5.2 既有做法
只取第一筆，不逐一輸出六個專項。

### 合作學校列表「合作年度」欄的資料完整度

只有臺中市立五權國民中學能在 `club-profile.md` §4 沿革查到明確年份（**2015 年**，
「協助台中市五權國中女足隊成立」）；其餘四校（國立台灣體育運動大學／南投水里國中／
彰化永靖國中／臺中篤行國小）沿革沒有逐校標明年份，維持空白「未標明年度」，不臆測。
磐石版「合作學校列表」維持既有空白狀態（客戶尚未提供對應名單），不因為藍鯨有真實
資料就連帶臆造磐石的對應內容。

### 驗證指令與實際結果（2026-09-29）

```
npm run lint    # 0 errors, 395 warnings（等於既有基準上限，未超過）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機起 `tcrfc`（port 14001）／`bw`（port 14002，帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`）
兩容器，`apps/api` 未啟動：

- 3 個改動頁 `/zh/`／`/en/`（共 6 條網址 × 2 容器＝12 條）逐一 `curl`：tcrfc 全數
  `200`；bw 的 5.5 為 `200`、5.3／5.4 為 `404`。`X-Robots-Tag: noindex, nofollow`
  全數皆在。
- 實測 `/zh/programs/school-community/`（bw 容器）渲染結果：`<h1>校園與社區</h1>`、
  「合作學校列表」表格 5 列真實學校名稱與「建教合作（女子足球隊）」內容，五權國中一列
  顯示「2015 年」，其餘四列顯示「未標明年度」；全頁掃描 0 筆「磐石」／「TCRFC」字樣。
- 實測「API 打不到時優雅降級」：5.3／5.5 的「梯次」／FAQ 區塊落回既有「待公告」／
  「常見問題收錄中」提示；5.4「目前開放報名的專項」落回「目前尚無開放報名中的專項
  梯次」提示，皆無 500 或未捕捉例外。JSON-LD 三頁皆可 `JSON.parse()`，僅
  `WebSite`／`WebPage` 兩節點（無 `Course`，因 `programDetail` 為 `null`，符合
  GEO-05）。
- `node scripts/check-heading-structure.mjs`：同 S2-8 一併驗證，H1 唯一、標題不跳階
  0 違規（沿用同一輪的 164／130 條路由結果）。
- `node scripts/check-site-units-coverage.mjs`：通過（'5.3'／'5.4'／'5.5' 皆仍在頂層
  代碼 `'05'` 下，未新增頂層代碼）。
- `NUXT_PUBLIC_SITE_NAME=台中藍鯨 node scripts/check-club-brand-leak.mjs
  --base-url=http://127.0.0.1:14002`：保護清單新增 `programs/school-community`
  （與 S2-8 的 4 頁併入同一次驗證，共 24 頁），全數乾淨，棘輪未被違反。
- Docker 容器日誌：除既有的 `apps/api` 連不上降級路徑外，無 `Vue warn`／
  `TypeError`／`ReferenceError`。

🔴 **未驗證項目**：`programs` 表現況 0 筆種子資料，`type=specialist_training`／
`type=school_community` 在有真實梯次時的實際渲染（早鳥價、剩餘名額、開放報名專項
清單）只做過程式碼與 DTO 欄位比對，未用真實資料庫驗證。

### 需要後端補的欄位或端點

無新增——沿用 `S1-15`／`S1-20` 已回報的既有缺口（`PublicCalendarEventDto`／
`ProgramDetailDto` 的 `SchemaEligible` 布林值）。

### 規格疑點（列出，未自行決定）

1. 🔴 **`§1.3`「總則的例外只有四項單元取捨，四項以外不得另行設計」與 5.1–5.4 關閉
   決定之間的張力**：藍鯨規劃書行 24、92 明文只列四項結構性例外（不設 06／11、04
   改青年隊、09 分區），05 整個單元不在其中——嚴格照文字，05 應該 1:1 沿用主站五個
   子頁再換內容，不是選擇性關閉部分子頁。`S1-15` 對 5.1／5.2 與本輪對 5.3／5.4 的
   關閉判斷，理由都是「產品／資料模型與藍鯨實際活動不對應、換內容需要臆造」，這是
   合理的謹慎判斷，但終究是判斷不是規格明文授權的例外。`content/blue-whale/
   programs.md` 其實對 5.1（社區足球學校／運動 i 台灣）與部分 5.4（守門員基礎班）
   都有一定程度可對應的真實內容，只是規模與定位不完全相符——**留給下一次覆查規格
   或客戶裁決**：整個 05 單元是否要改為「1:1 開放＋盡力換真實內容」，而不是目前
   「部分關閉」的做法。這個疑點同時回溯適用於 `S1-15` 已關閉的 5.1／5.2，不只是
   本輪新關閉的 5.3／5.4。
   ✅ **已裁決（BW-C1，2026-09-29）**：依 §1.3 總則明文字面重新判斷，「05 應該
   1:1 開放」這個猜測是對的——`5.1`–`5.4` 已全部重開，詳見本檔「BW-C1」節。
2. **5.4「目前開放報名的專項」清單的呈現粒度**：規劃書沒有規定六大專項各自要不要
   有獨立的 `Program` 主檔，本輪判斷用清單方式呈現真實 API 回傳的所有
   `specialist_training` 項目（不限定六類），是否要求後台逐一對應六大專項各建一筆
   `Program`，需要規格或客戶確認。

## BW-C1（修正藍鯨關閉單元誤用，重開 3.2／3.4／4.5／4.6／5.1–5.4／12.3，2026-09-29，`frontend-architect`）

使用者指出 S1-15／S2-8／S2-10 連續三輪把「藍鯨沒有對應真實內容」當成用 404 整頁關閉
單元的正當理由，違反藍鯨規劃書 §1.3 總則（行 87–93）「本站與主站是同一套網站，只有
配色不同」與 §2.1（行 92、135）「例外只有四項單元取捨」。本輪逐一覆查 `units.ts`
`BLUE_WHALE_DISABLED_UNITS` 的 13 個項目，只保留能從四項例外**直接推導**的 3 個
（`06`／`11`／`4.7`）與依附 `4.7` 的 `12.2`，其餘 9 個（`3.2`／`3.4`／`4.5`／`4.6`／
`5.1`–`5.4`／`12.3`）全部重開。詳見 [`docs/18-work-errors.md`](../../docs/18-work-errors.md)
`E-76`、[`docs/14-invariants.md`](../../docs/14-invariants.md) 新增條目。

### 保留關閉與重開的依據（逐項附規劃書行號）

| 單元 | 決定 | 依據 |
|---|---|---|
| `06`／`11` | 維持關閉 | 藍鯨規劃書 §2.1，行 92、135：總則四項例外之二 |
| `4.7`（加入學院） | 維持關閉 | 藍鯨規劃書 §3.4，行 197：「04 青年隊沿用主站 04 的梯隊版型，但不沿用招生與課程報名架構」，4.7 整頁即為該架構 |
| `12.2`（學院招生 FAQ） | 維持關閉 | 依附 `4.7` 同一項例外；**本輪修正**：先前只寫進 `FAQ_CATEGORY_UNIT_CODES`，漏了頁面本身 `unit-gate` 判斷用的主清單（見下方「本輪也修的一個新錯」） |
| `3.2` 球員發展系統 | 🟢 重開 | 八大主題是通用足球培訓詞彙，兩俱樂部可共用；模組詳細內容本來就是「準備中」佔位文字（磐石版也是）。改為 SEO／Hero 依俱樂部切換（`getPlayerDevelopmentSeo/Hero`），藍鯨版避免使用「系統」這個暗示已建制機構框架的用詞，改稱「培育重點」 |
| `3.4` 國際發展通道 | 🟢 重開 | `content/blue-whale/club-profile.md` §4 沿革有真實旅外案例：2019 守門員蔡明容旅外日本（`squad/player-tsai-ming-jung.md` 記錄完整俱樂部經歷：2019–2022 效力 FC ふじざくら山梨）、2020 守門員程思瑜旅外日本、2019 選手蘇育萱旅外日本、2023 選手蘇育萱旅外中國。地區頁籤改為 Japan／China，合作俱樂部 Logo 牆藍鯨維持空狀態（沒有可公開授權使用的海外合作俱樂部 Logo） |
| `4.5` 學院教練團 | 🟢 重開 | 確實沒有已核實、非過期、已取得肖像同意的藍鯨教練名單可用，但這是「此頁此區塊內容缺漏」不是「整頁不存在」。標題改「青年隊教練團」（不用「學院」字樣），教練名單區塊顯示既有「收錄中」空狀態，不放任何名字或照片 |
| `4.6` 學院生活 | 🟢 重開 | 同上，13 張磐石學員真實照片不能挪用，藝廊區塊對藍鯨改為「收錄中」空狀態，不放任何照片 |
| `5.1` 兒童足球訓練 | 🟢 重開，真實內容 | 藍鯨規劃書 §3.5，行 201：「05 推廣活動沿用主站 05 的活動版型；是否開放線上報名與收費，待確認」——頁面開放、報名功能待確認的明文依據。`programs.md` §1（社區足球學校「小藍鯨」）＋§2（運動 i 台灣 2.0 課程表，3–15 歲）有真實對應內容 |
| `5.2` 夏令營 | 🟢 重開，空狀態 | 同上明文依據；`programs.md` 沒有找到對應的夏令營產品，內容維持誠實空狀態 |
| `5.3` 冬令營 | 🟢 重開，空狀態 | 同上，沒有找到對應的冬令營產品 |
| `5.4` 專項訓練 | 🟢 重開，部分真實內容 | 同上明文依據；`programs.md` §2 第 10 項「藍鯨守門員基礎班」（7–12 歲）是唯一真實對應內容，不套用磐石「六大專項」框架，只呈現這一項真實課程 |
| `12.3`（課程與營隊報名 FAQ） | 🟢 重開 | 依附 `5.1`–`5.4` 重開，內容本來就依 `club_id` 撈資料（`useFaqList`），無需額外處理 |

### 各頁改動摘要

- **`shared/utils/units.ts`**：整份重寫檔頭與 `BLUE_WHALE_DISABLED_UNITS`／
  `FAQ_CATEGORY_UNIT_CODES`。新增紀律：陣列每一項都必須在同一行帶 `//` 行內註解且含
  `§`，由新增的 `apps/web/scripts/check-bw-units-citation.mjs` 靜態檢查（掛進
  `npm run lint` 的 `lint:bw-units-citation`）。
- **`shared/utils/club-copy.ts`**：新增 8 組 `getXxxSeo()`／`getXxxHero()` 工廠函式
  （`getPlayerDevelopmentSeo/Hero`、`getInternationalPathwaysSeo/Hero`、
  `getYouthCoachesSeo/Hero`、`getYouthLifeSeo/Hero`、`getChildrensTrainingSeo/Hero`、
  `getSummerCampSeo/Hero`、`getWinterCampSeo/Hero`、`getSpecialistTrainingSeo/Hero`）
  與 5 個資料常數（`INTL_PATHWAY_JAPAN_NOTES_BW`、`INTL_PATHWAY_CHINA_NOTE_BW`、
  `CHILDRENS_TRAINING_CLASSES_BW`、`GOALKEEPER_CLASS_BW`），內容一律逐字節錄
  `content/blue-whale/` 舊站原文，附來源章節註解（延續紀律 11）。
- **8 個頁面檔案**（`app/pages/zh/club/{player-development,international-pathways}/
  index.vue`、`app/pages/zh/academy/{coaches,life}.vue`、`app/pages/zh/programs/
  {childrens-training,summer-camp,winter-camp,specialist}/index.vue`）：加入
  `clubKey`／`isTcrfc` 判斷，SEO／Hero 改讀上述工廠函式；藍鯨專屬真實內容區塊改讀
  對應常數；沒有真實內容的區塊（教練名單、訓練影像、六大專項框架、合作夥伴 Logo、
  往年花絮照片）對藍鯨顯示既有「準備中／收錄中」空狀態樣式，不放任何磐石專屬真實
  人名、照片或機構宣稱；報名 CTA 對藍鯨不連到磐石專屬的 `/zh/join/academy/`／
  `/zh/join/camp-registration/`，改連官方 LINE（`getClubAssets(clubKey).social.line`）
  或真實舊站報名表單（`GOALKEEPER_CLASS_BW.signupUrl`，Google 表單）。
- **`app/pages/zh/faq/programs-camps/index.vue`**：移除關閉理由的過期註解（`unit`
  維持 `'12.3'`，本身邏輯不變，只是不再被關閉）。
- **選單、hub 頁、CTA 卡片同步**：`SiteHeader.vue` 04／05 mega menu 本來就用
  `isUnitEnabledForClub()` 動態判斷，`units.ts` 修正後自動恢復顯示，只更新了過期的
  說明註解；`app/pages/zh/club/index.vue`（03 hub）、`club/opportunities/index.vue`、
  `club/player-stories/index.vue`、`club/first-team/index.vue`、`academy/overview.vue`、
  `academy/curriculum.vue`、`academy/teams.vue` 裡原本用 `v-if="isTcrfc"` 隱藏
  3.2／3.4／4.5 連結的 CTA 卡片，移除隱藏並依俱樂部切換文案（部分卡片同時發現、修正
  了先前遺漏的既有缺口，例如 `academy/teams.vue` 的 4.3 卡片也一直被誤綁在同一個
  `isTcrfc` 判斷裡）。

### 本輪也修的一個新錯（發現於驗證階段，同一次交付內修正）

第一版把 `12.2` 只寫進 `FAQ_CATEGORY_UNIT_CODES`（分類 slug 對照表），以為這樣就夠
——但 `faq/academy-admission/index.vue` 頁面本身也宣告 `definePageMeta({ unit: '12.2'
})`，`unit-gate.global.ts` 檢查的是 `BLUE_WHALE_DISABLED_UNITS` 這份主清單，不是
`FAQ_CATEGORY_UNIT_CODES`。用本機 bw 容器 `curl` 驗證時發現該頁誤回 `200`（該關閉的
沒關閉），已補回 `BLUE_WHALE_DISABLED_UNITS` 的 `'12.2'` 項並重新驗證 404。這個錯已
收進 `docs/18-work-errors.md` `E-76`（同一筆記兩個根因，不分開記兩筆）。

### 驗證指令與實際結果（2026-09-29）

```
npm run lint    # 0 errors, 395 warnings（等於既有基準上限，未超過；曾一度為 399，
                # 已修正 international-pathways/index.vue 新增元素的屬性順序後降回）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機用同一份映像檔起 `tcrfc`（port 13001）／`bw`（port 13012，帶
`NUXT_PUBLIC_SITE_NAME=台中藍鯨`）兩容器，`apps/api` 未啟動（依派工規則不自行啟動、
不碰密碼，`GET /api/backend/*` 皆優雅降級為空清單／既有「待公告」提示，無 500）：

- 8 個重開頁＋1 個 FAQ 分類頁的 `/zh/`／`/en/` 狀態碼（共 18 條網址）逐一 `curl`：
  bw 容器全數 `200`；`academy/join`（4.7）與 `faq/academy-admission`（12.2）維持
  `404`；tcrfc 容器對應頁面全數 `200`（未受影響）。`X-Robots-Tag: noindex, nofollow`
  全數皆在。
- 內容抽查（bw 容器）：`international-pathways` 出現「Japan 日本」「China 中國」
  「蔡明容」「蘇育萱」「FC ふじざくら山梨」；`player-development` 出現「球員培育
  重點」「八大面向」；`childrens-training` 出現「小藍鯨」「運動 i 台灣」「藍鯨
  U15 女子足球班」「洽詢官方 LINE」；`academy/coaches` 出現「青年隊教練團」「教練
  名單整理中」；`academy/life` 出現「青年隊生活」「訓練與比賽影像整理中」；
  `programs/specialist` 出現「藍鯨守門員基礎班」「前往報名表單」；`summer-camp`
  出現「目前尚無對應的夏令營活動」空狀態文字。tcrfc 容器對應頁面內容逐一比對，磐石
  原有內容（八大模組、Hellas Verona、楊朝景等）未受影響。
- `node scripts/check-bw-units-citation.mjs`：通過（4 項全部附 `§` 章節依據）。
- `node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:13012`：
  `PROTECTED_PAGES` 新增本輪 9 頁（8 個重開頁＋`faq/programs-camps`），共 33 頁全數
  乾淨、棘輪未被違反（上一版 24 筆全部還在）。9 頁對應的 `/en/` 版本另行 `curl` 逐一
  確認 0 筆「磐石」／「TCRFC」／「學院」命中（腳本本身路由清單目前只收 `/zh/`）。
- `node scripts/check-heading-structure.mjs --base-url=http://127.0.0.1:13012`：
  148 條路由 H1 唯一、標題不跳階 0 違規。
- `node scripts/check-site-units-coverage.mjs`：通過（本輪 unit 代號未變更頂層代碼）。
- `node scripts/check-faq-schema-live.mjs --base-url=http://127.0.0.1:13012`：通過，
  0 個 `FAQPage` 節點（`apps/api` 未啟動，符合 GEO-05「資料不足時不輸出」）；已同步
  更新腳本檔頭過期的「一律預期 404」說明。
- Docker 容器日誌：除既有的 `apps/api` 連不上降級路徑外，無 `Vue warn`／
  `TypeError`／`ReferenceError`。

🔴 **未驗證項目**：`apps/api` 未啟動，只驗證了「API 打不到時優雅降級」這條路徑；
真實 `programs`／`faqs` 種子資料下的實際渲染效果未驗證。

### 仍有疑義的項目（列出，未自行決定）

1. **`3.4` 國際發展通道的地區頁籤只涵蓋 Japan／China**：`club-profile.md` 沿革另有
   「守門員程思瑜」「選手蘇育萱」旅外日本的紀錄，但未附效力俱樂部名稱（只有蔡明容
   有完整俱樂部經歷可引用）；若客戶之後提供這兩位球員的效力俱樂部名稱，本頁可以
   補上更完整的敘述，目前維持舊站原文的資訊粒度，不臆測俱樂部名稱。
2. **`4.5`／`4.6` 的空狀態是否要進一步收窄**：`coaching-staff.md` 確實有鄭雅薰／
   李彥廷「曾任」U15 教練的舊站原文，本輪判斷這份資料可能已過期（2025 賽季未更新）
   而不用，維持「收錄中」空狀態；若客戶確認這份名單現況仍適用，可以改為顯示（比照
   `3.4` 引用沿革事實文字、不附照片的做法）。
3. **`/zh/club/`（03 hub）與 `/zh/programs/`（05 hub）兩頁整頁仍是既有記錄的缺口**
   （固定磐石內容，零俱樂部分支）：本輪只修正了這兩頁裡連到 3.2／3.4／4.5 的 CTA
   卡片隱藏邏輯，沒有把整頁改成雙俱樂部內容——那是 `S1-12d`／`S2-8` 已記錄的既有
   缺口，不在本輪派工範圍內，需要時另立一輪處理。
4. **`check-club-brand-leak.mjs` 的路由清單只收 `/zh/`**：本輪對新增的 9 頁 `/en/`
   版本是手動 `curl` 逐一驗證，沒有讓腳本自動涵蓋——這是腳本本身的既有限制（見腳本
   檔頭），不是本輪新增的缺口，但值得之後一併補上腳本自動掃 `/en/` 的能力。

## BW-C1 品牌外洩全站盤點（2026-09-29，`frontend-architect`）

上一節「BW-C1」修的是「單元該不該關閉」；本節修的是**偵測機制本身**——舊版
`check-club-brand-leak.mjs` 只對一份手動維護的 `PROTECTED_PAGES`（33 頁、只含
`/zh/`）hard-fail，其餘頁面只計數、不影響離開碼。這個設計留下兩個真正的漏洞：
`/en/` 從未被檢查過；「沒被排進清單就不算數」讓 `join/international-player/`
（10.4，`units.ts` 從未關閉這個單元）整頁固定寫死「Taichung Rock FC」「TCRFC」
「台中磐石足球俱樂部」「International Department」從第一天起就沒被任何自動化
檢查抓到。同時，本節也處理上一節列的「仍有疑義」第 3、4 點：`03`／`05` hub
頁整頁固定磐石內容、品牌外洩檢查只收 `/zh/`。

### 1. 品牌外洩檢查改版：全站自動涵蓋、預設 hard-fail

`apps/web/scripts/check-club-brand-leak.mjs` 全面改寫（保留檔名與用法）：

- **路由收集**：比照 `check-heading-structure.mjs` 的既有作法，從 `app/pages/zh/`
  算出全部路由，`/zh/`／`/en/` 都收（動態路由排除），不再手動維護清單。
- **預設 hard-fail**：任何 200 頁面命中詞表任一詞就是失敗，不再有「其餘頁面只
  計數」這個灰色地帶。
- **`EXEMPT_PAGES` 取代 `PROTECTED_PAGES`**：例外清單方向刻意反過來——舊版是
  「已驗證乾淨的清單只能往上加」，新版是「**已知例外的清單只能往下減**」，每筆
  必須附規格依據或既有缺口編號，不是「看起來還好」。棘輪機制同樣用
  `git show HEAD:<自己>` 比對上一版，防止悄悄新增例外。
- **詞表**：`磐石`／`TCRFC`／`學院`／`Taichung Rock`（磐石英文全名核心詞組）／
  `www.tcrfc.tw`（**不是裸 `tcrfc.tw`**——實測發現裸網域會撞到 `nuxt.config.ts`
  的 `blueWhaleSiteUrl` 這個 runtime config 預設值，序列化進**每一頁**的
  hydration payload，全站 148 條路由誤判命中 148 次，完全沒有鑑別力，見
  [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-77`）。

**修正前全站命中清單**（本機 bw 容器實測，共 76 頁 / 38 條不重複路由）：

```
/zh/academy/、/zh/cart/、/zh/checkout/、/zh/checkout/complete/、
/zh/club/first-team/player/、/zh/cookies/、/zh/culture/、/zh/culture/fan-club/、
/zh/culture/manga/、/zh/culture/merchandise/、/zh/join/academy/、
/zh/join/camp-registration/、/zh/join/general/、/zh/join/international-player/
（單元 10.4，units.ts 從未關閉，先前任何一輪都沒抓到）、/zh/join/location/、
/zh/join/media/、/zh/join/partnership/、/zh/join/player/、/zh/member/、
/zh/news/、/zh/news/academy/、/zh/news/camps-events/、/zh/news/club/、
/zh/news/community/、/zh/news/international/、/zh/news/match/、
/zh/news/media/、/zh/news/player-stories/、/zh/order/lookup/、/zh/partners/、
/zh/partners/become-a-partner/、/zh/partners/opportunities/、
/zh/partners/our-partners/、/zh/partners/our-sponsors/、/zh/perks/、
/zh/privacy/、/zh/programs/、/zh/shop/、/zh/shop/cushioned-socks/、
/zh/shop/home-jersey-2026/
```
（各頁 `/en/` 版本同一份檔案，同樣命中，共 38×2 = 76）。

### 2. 各頁處置

**A. 03／04／05／08 單元 hub 全面雙俱樂部化**（既有記錄缺口，非本輪新增）：

| 頁面 | 處置 |
|---|---|
| `club/index.vue`（03 hub） | SEO／Hero／統計卡／單元卡描述／CTA 標題改依俱樂部切換，新增 `club-copy.ts` 的 `getClubHubSeo/Hero/Stats/...`；tcrfc 分支逐字沿用既有輸出 |
| `academy/index.vue`（04 hub） | 同上模式，7 張卡（tcrfc）／6 張卡（bw，無 4.7）改依 `getAcademyHubCards()` |
| `programs/index.vue`（05 hub） | 同上；藍鯨「線上報名流程」六步驟區塊（假定站內線上流程）對藍鯨隱藏，改顯示如實的「現場個人報名」說明——藍鯨規劃書 §2.1 單元名稱本來就是「PROGRAMS 推廣活動」不是「課程與活動」 |
| `culture/index.vue`（08 hub） | 導覽卡文字改依 `getClubIdentity()`／`getClubAssets()`，8.1–8.3 子頁本身內容是否雙俱樂部化列為規格疑點（見下方） |

**B. 機械式換名（`getClubAssets()`／`getClubIdentity()` 既有欄位即可）**：
`privacy`／`cookies`／`member`／`order/lookup`／`perks`／`partners/index`／
`partners/{our-partners,our-sponsors,opportunities,become-a-partner}`／
`join/{player,media,general,camp-registration,partnership}`／`news/index`／
`news/{club,community,international,match,camps-events}`（描述改為讀
`articles.value.length` 動態计數，不再寫死篇數字面值）。

**C. 新發現的既有缺口（不在先前任何一輪記錄範圍內）**：
`join/international-player/index.vue`（10.4）SEO／Hero／同意聲明／收件單位標籤
整頁固定寫死磐石機構名稱，改讀 `club-copy.ts` 新增的
`getInternationalPlayerSeo/Hero/ConsentAfterLink/DeptLabel()`。

**D. 結構性簡化（不是換名字，是拿掉不適用的功能分支）**：
`join/academy/index.vue`（10.2）原本合併「學院梯隊／兒童訓練／專項訓練」三種
性質完全不同的報名於一份表單——藍鯨青年隊只有 U15／U12（沒有 U14），且藍鯨
兒童訓練／專項訓練現場個人報名、不接這套站內線上流程，bw 版簡化為只收「加入
青年隊」單一報名項目，不提供另外兩類選項；提交給後端的 `enrollment_category`／
`PROGRAM_LABELS` 對照表維持不動（避免不確定 bw 是否有對應的後端封閉選項值）。

**E. 真實網域／收款揭露（判定為規格要求，不是外洩，已列入 `EXEMPT_PAGES`）**：
`checkout/`、`shop/`、`shop/cushioned-socks/` 的「收款方為台中磐石足球俱樂部」——
藍鯨規劃書 §1.3「本站不另設 LINE Pay 商店號、不使用獨立發票字軌，一律沿用主站
的單一金流設定」＋主站規劃書 §1.3「前台必須明示收款方」，即使在藍鯨站上這句話
也是真的，不是磐石內容外洩到藍鯨。`culture/merchandise/` 的 `www.tcrfc.tw` 舊站
過渡期連結同理（既有例外，本輪沿用）。

**F. 誠實空狀態（真實商品／真實照片是磐石專屬設計，不能沿用充當藍鯨商品）**：
`shop/index.vue`（主場球衣卡片）、`shop/home-jersey-2026/`（整頁）、
`cart/index.vue`（示範品項）、`culture/merchandise/`（俱樂部商品區塊）——桃紅
配色主場球衣是磐石真實 2026 賽季設計（含 Joma／San Pellegrino 贊助標誌），對
藍鯨隱藏，改顯示「尚未上架」；無隊徽的通用配件（厚底緩震機能襪，六色皆通用）
維持兩俱樂部共用。`culture/manga/`（台中磐石原創漫畫 IP）、`culture/fan-club/`
（磐石付費會籍方案與真實活動照片）整頁內容對藍鯨隱藏，顯示「尚未推出」——這兩頁
是磐石原創創作／真實商業方案，沒有舊站原文可引用或節錄，紀律 11「不得自行創作」
不允許換個俱樂部名稱就通用。

**G. 留在例外清單、本輪未修正**：`club/first-team/player/`——頁面自稱「球員
詳情頁範本」，以磐石一線隊 11 號球員楊朝景的真實名單資料示範正式站版型結構，
藍鯨球員名單與肖像同意尚未到位（STATUS.md 阻塞清單），沒有可替換的真實藍鯨
球員資料。

### 3. 驗證指令與實際結果（2026-09-29）

```
npm run lint    # 0 errors, 395 warnings（等於既有基準上限，未超過）
npm run build   # 成功
docker build -f apps/web/Dockerfile apps/web   # 成功
```

本機用同一份映像檔起兩個容器（`tcrfc-bwc1` port 15001／`bw-bwc1` port 15002，
`bw` 帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`，`apps/api` 未啟動，依派工規則不自行
啟動、不碰密碼）：

- `node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:15002`：
  **通過**（`exit 0`）。148 條路由（不含例外）全數乾淨；例外清單命中 10 頁，
  全部有明文規格依據（見上方 E／G 兩類）。
- `node scripts/check-heading-structure.mjs` 對兩個容器分別跑一次：**皆通過**
  （H1 唯一、標題不跳階）。過程中抓到一個本輪自己造成的迴歸——`shop/
  home-jersey-2026/` 的 bw 空狀態分支忘記補 `<h1>`，已修正，見
  [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-78`。
- `node scripts/check-site-units-coverage.mjs`：通過。
- `node scripts/check-bw-units-citation.mjs`：通過（`BLUE_WHALE_DISABLED_UNITS`
  本輪未變動）。
- `node scripts/check-faq-schema-live.mjs` 對兩個容器分別跑一次：皆通過，0 個
  `FAQPage` 節點（`apps/api` 未啟動，符合 GEO-05）。
- `curl -I` 兩個容器 `/zh/`：皆 `X-Robots-Tag: noindex, nofollow`。
- Docker 容器日誌：除既有的 `apps/api` 連不上降級路徑（`fetch failed`）外，
  無 `Vue warn`／`TypeError`／`ReferenceError`。

🔴 **未驗證項目**：`apps/api` 未啟動，只驗證了「API 打不到時優雅降級」這條
路徑；真實資料下的實際渲染（尤其 `join/academy/` 表單送出後端是否接受 bw 版
`enrollment_category` 值、`news` 各分類真實文章數）未驗證。

### 仍有疑義的項目（列出，未自行決定）

1. **`culture/{manga,fan-club}/` 對藍鯨是否要規劃全新內容，還是維持空狀態直到
   有真人事**：本輪判定為「無舊站原文可引用，不得自行創作」而顯示空狀態，但
   這兩個單元（原創漫畫、付費會籍）本質上是磐石的商業／創作決策，藍鯨是否要
   做對應的（不同的）內容企劃，屬於客戶決策範圍，不是「換個名字」能解決的。
2. **`join/academy/` 提交的 `enrollment_category` 值（`學院 U15`／`學院 U12`）
   對藍鯨是否仍是後端認得的封閉選項值**：本輪只改了畫面顯示文字（「青年隊」），
   刻意沒有改送出的內部對照表值，因為不確定後端 `form_fields` 的封閉選項是否
   兩俱樂部共用同一組字面值——需要後端確認後才能決定要不要也把送出值改成
   「青年隊 U15」這類。
3. **08 單元 8.1–8.3 子頁（漫畫／球迷會／商品）是否該有藍鯨自己的內容規劃**：
   本輪只處理「不得沿用磐石內容」這一半，沒有處理「藍鯨這三個子單元究竟要
   放什麼」這一半——這是內容企劃問題，不是本輪工程盤點能回答的。

## 藍鯨規劃書 v1.9：08 不設 8.1 漫畫（2026-09-30，`frontend-architect`）

依據：藍鯨規劃書 v1.9 §1.3／§2.1（行 136）／§3.1（行 182）／§3.8（行 232）——總則例外由四項增為**五項**，`08` 不設 `8.1` 漫畫；`8.2` 球迷會、`8.3` 官方商品、`8.4` 特約店家比照主站，「有內容就顯示，沒有就顯示空狀態」。磐石站不受影響。

移除漫畫的位置（全部走同一個開關 `isUnitEnabledForClub('8.1', club)`）：

| 位置 | 做法 |
|---|---|
| `shared/utils/units.ts` | `BLUE_WHALE_DISABLED_UNITS` 加 `'8.1'`（行內註解引用 §2.1 行 136，`lint:bw-units-citation` 通過，現為 5 項） |
| `culture/manga` 頁面（zh／en） | 頁面本來就宣告 `unit: '8.1'`，不需細粒度改代號；middleware 對 bw 回 404 |
| `SiteHeader.vue` 文化 mega menu | 8.1 項目加 `v-if="isUnitEnabledForClub('8.1', club)"` |
| `culture/index.vue`（文化單元首頁） | 8.1 卡片、`<title>`／description、hero 導言、卡片區標題與導言中的「漫畫」字樣，bw 全部換掉；8.2／8.3 卡片不再用磐石照片（`fanclub-event-04.jpg`、`merch-jersey-01.jpg`）當背景，文案改中性 |
| 首頁、頁尾、`sitemap.xml`、`llms.txt`／`llms-en.txt` | 本來就沒有漫畫連結（sitemap／llms 只列到單元層級 `08`，不含子頁），實測 bw 全為 0 筆 |

8.2／8.3 空狀態（後台 `F2`／`S1` 尚未開發，藍鯨維持誠實空狀態，文案「內容由後台提供，目前尚無可顯示的內容」，不寫「尚未推出」「開發中」這類沒有依據的狀態宣稱）：

- `culture/fan-club`：bw 只留「球迷活動」一區空狀態（先前的「方案尚未推出，敬請期待」是規格沒有的說法，已改）。
- `culture/merchandise`：bw 原本展示機能襪（磐石商店商品）、「學院商品／球迷商品 開發中」、指向磐石舊官網的商店區，全部對 bw 隱藏，改為單一空狀態；hero 背景改用無圖漸層。`check-club-brand-leak.mjs` 因此**移除** `culture/merchandise` 兩筆例外（`www.tcrfc.tw` 已不出現）。
- `EXEMPT_PAGES`：`culture/manga`、`culture/fan-club` 在本輪開工時腳本裡已不在清單（`README`／`STATUS` 上一輪的敘述已過時，以腳本為準）；本輪只移除 merchandise 兩筆，棘輪通過。

驗證（bw 容器帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`）：見交付報告。

未動、留待決定：`partners/opportunities`（9.4）與 `join/partnership` 表單仍有「漫畫內容合作」贊助方案——藍鯨規劃書 §3.9 只說「沿用主站 09 的贊助方案版型」，沒有明文刪除，故不擅自改；`shop/*` 商店頁對 bw 仍顯示機能襪（8.3 商店範疇，本輪未動）。


### 連帶：9.4 贊助方案不列「漫畫內容合作」（2026-09-30，主 session）

藍鯨沒有漫畫就不可能提供漫畫合作，屬 v1.9 §2.1 的直接推導。`partners/opportunities`、`partners/index`、
`join/partnership` 以 `isUnitEnabledForClub('8.1', club)` 為唯一判斷：藍鯨隱藏漫畫方案卡與表單勾選項，
方案數改為「八種」、後續卡片編號改為連續 01–08；磐石維持九種、01–09。實測：兩站 `/zh/`、`/en/` 頁面
方案數與編號如上，bw 三頁「漫畫」0 筆；`check-club-brand-leak.mjs`（bw 全站 146 路由）與
`check-heading-structure.mjs` 通過；`npm run lint` 0 錯誤 393 警告、`npm run build` 通過。

## 梯次時段 `weekly_schedule` 格式化（2026-09-30，`frontend-architect`）

`sessions.weekly_schedule` 是 JSON，API 原樣以字串輸出，前台原先直接印出 JSON 原文。
- **共用函式**：`shared/utils/weekly-schedule.ts` 的 `formatWeeklySchedule(raw, locale, onWarn?)`，依路由語系輸出。
  已知形狀：`{"mon":"18:00-19:30"}`、區間鍵 `{"mon-fri":"09:00-16:00"}`、值為自由文字 `{"mon":"1.5 小時"}`（藍鯨舊站原文，原樣顯示、不翻譯）；
  另容許值為陣列（同日多時段）與逗號多天鍵。後台只驗證「合法 JSON」不限形狀，故解析寬鬆。
- **降級**：空值回 `null`；有值卻解析不了（非 JSON、非物件、未知星期鍵、值不是時段文字）也回 `null`，頁面顯示「—」，
  **絕不輸出原始 JSON**，開發環境（`import.meta.dev`）以 `console.warn` 警告。
- **使用處**：目前只有 `app/pages/zh/programs/childrens-training/index.vue`（5.1）顯示 `weeklySchedule`；其他課程頁尚未顯示梯次時段，之後接上請呼叫同一函式。
- **Course 結構化資料（S1-20）**：`buildCourseSchemaNode` 目前不輸出任何梯次或時段（無 `hasCourseInstance`），故無需改動；
  日後若要輸出，正確表示為 `hasCourseInstance.courseSchedule`（schema.org `Schedule`：`byDay`／`startTime`／`endTime`／`repeatFrequency`），
  不可塞入本函式的中文顯示字串。
- **檢查**：`npm run lint:weekly-schedule`（`scripts/check-weekly-schedule.mjs`，20 組固定輸入，已併入 `npm run lint`）。

## S2-7／S2-9／S2-12／S3-9／S1-14 收尾／S1-12f（接上「後端公開端點已存在」的頁面，2026-10-01，`frontend-architect`）

派工：把 `apps/api` 已有公開端點的主站前台頁接上真實資料。**只動 `apps/web`**。新增共用：`shared/utils/{partners,charity,press,content-blocks,api-types}.ts`、
`app/composables/{usePartners,useCharityCta}.ts`、`app/components/PartnerLogoTile.vue`。通則：一律以容器的 `config.public.club` 組網址（磐石／藍鯨由後端 `club_id` 分區，不混列）；
API 失敗＝空資料，頁面落回既有空狀態或過渡內容，不出 500；`/en/` 由孿生路由產生，`lang` 參數跟隨路由，頁面固定文案仍是中文（沿用 S1-13 範圍）。

### 資料來源表

| 列／頁 | 端點 | 狀態 |
|---|---|---|
| S1-14 首頁贊助夥伴 Logo 牆 | `GET partners?home=true` | 🟢 接上；依 9.1 類型順序排、最多 15 家，點擊到 9.1。無資料維持 10 個空格 |
| S1-14 首頁 Hero 迷你新聞卡 | 既有 `news`（與「最新消息」同一份排序取前 2 篇） | 🟢 有新聞用真資料；0 篇時磐石退回既有靜態兩張、藍鯨不顯示 |
| 頁尾贊助夥伴 Logo（主站 §2 Footer） | `GET partners?footer=true` | 🟢 `SiteFooter.vue` 新增 Logo 列（不 await，無資料整段不輸出，最多 8 家） |
| S2-7 9.1 合作夥伴 | `partners` | 🟢 五類＋俱樂部自訂類型（如藍鯨「指導單位」）分區；Logo 牆＋詳情（內容、期間、國家、官網、共同公益計畫）。磐石「國際夥伴」後台尚無資料時沿用三個既有海外隊徽，有資料即整批換掉 |
| S2-7 9.2 贊助商 | `sponsors` | 🟢 三等級＋自訂等級；贊助故事連 `/zh/news/{slug}/`；活動紀錄彙整成依日期表（含成效摘要、縮圖）。不輸出聯絡窗口／合約日期（後端本來就不給） |
| S2-7 9.4 贊助方案＋提案下載 | `sponsor-packages`、`proposals`、`POST proposals/{id}/download-requests` | 🟢 後台無已發布方案時維持既有 9（藍鯨 8）張靜態卡；價格只在後台公開時顯示。下載表單（公司／姓名／Email／個資同意／honeypot／UTM／sourcePath）送出建 Lead，成功後顯示 30 分鐘限時連結。同頁新增 G-12 FAQ 快捷區塊（`faqs/embeds/sponsorship`，首次消費）＋FAQPage Schema |
| S2-9 11.1 慈善理念 | 無（B5 沒有 11.1 內容端點） | ⬜ 維持靜態 |
| S2-9 11.2 慈善計畫 | `charity/programs`、`programs/{slug}`、`records?program=` | 🟢 列表（`?page=N`，12 筆）＋**新增詳情頁** `/zh/charity/programs/{slug}/`（緣起與執行過程、捐助內容、受贈團體、藝廊、執行紀錄、夥伴／贊助商、相關報導）。`programs.vue` 改為 `programs/index.vue`（避免 Nuxt 把它當巢狀父層，URL 不變）。內容區塊 JSON 以純文字節點渲染，**不 v-html** |
| S2-9 11.3 慈善事蹟 | `charity/records`（pageSize 50） | 🟢 年份分組＋年份篩選。後端 0 筆時退回 mockup 的 3 筆真實事蹟（過渡，應補登後台 B5） |
| S2-9 11.4 影響力數據 | `charity/impact` | 🟢 團體數／捐助項次／地區數／公開統計項；計數 0 顯示「—」；金額類由後端預設不公開，前台不補算。0 資料退回既有 3 團體 |
| S2-9 CTA（CH-6） | `charity/cta` | 🟢 11 全區「球迷捐款」卡片與 11 首頁按鈕讀後台設定（外連 https、`noopener`）；後台文案沒提到協會就不採用、退回含「台灣足球策略發展協會」的固定文案。未設定網址維持 disabled 佔位與說明 |
| S2-12 7.8 媒體專區 | `press?type=`×3 | 🟢 新聞稿／品牌識別包／高解析圖庫＋媒體聯絡導 10.6。磐石保留既有靜態識別包（真實向量檔），後台 B6 資源另列「更多識別素材」；藍鯨只顯示 B6 資源，無則空狀態 |
| S3-9 積分榜、球員數據彙總 | ❌ **無公開端點** | ⛔ 未做。`apps/api` 只有 `Features/AdminStandings`（後台），沒有任何積分榜或球員數據公開端點，見下方缺口 |
| S1-12f 一線隊球員 Person | 既有 `players`（`schemaEligible`） | 🟢 `first-team` 輸出 Person（閘門：schemaEligible＋非已知未滿 18 歲＋照片只在已同意時）。**順手修正 E-96**：`our-people.vue` 的教練 Person 原本全部合併成一個 |
| （C5，順手）02 里程碑、3.1 榮譽 | `milestones`、`achievements` | 🟢 後端有資料就換成後台資料（兩俱樂部共用），否則維持原靜態／空狀態 |
| S1-12d | — | ⬜ 本輪無可做項：剩下是「API 實機驗收」與使用者啟動 API 才能驗的部分 |

### 代理與下載（`server/api/backend/[...path].ts`）

新增三條白名單：`POST {club}/proposals/{GUID}/download-requests`（轉發訪客真實 IP 給限流，同表單）；`GET {club}/proposals/downloads/{token}`（串流 PDF/ZIP，只轉出 content-type／disposition／length，保留 `private, no-store`，**不轉發 cookie／Authorization**）；
`GET {club}/press/{slug}/download`（不跟隨轉址，把 Location 回給瀏覽器，下載次數才會累計）。其餘 POST 仍 405。`sitemap` 新增慈善計畫詳情網址（僅開放 11 的俱樂部，最多 50 筆）。

### 單元開關／藍鯨
09 對藍鯨維持開放、與磐石分區（端點分區）；11 對藍鯨維持 404（沿用 §2.1，詳情頁同）；7.8 兩站皆開。本輪未新增任何單元關閉。

### 範圍縮減與判斷（非規格）
1. 夥伴／頁尾 Logo「輪播」未做動態輪播，改靜態列（首頁 15、頁尾 8）；規格寫輪播，待設計定動畫。
2. 提案 A/B：不隨機（會造成 hydration 不一致、Lead 比較基準不可重現），固定挑「有目前語系檔案、版本最大」的一份。
3. 11.3 只取前 50 筆（後端 maxPageSize），超過只顯示提示；年份晶片來自已載入資料，未用 `records/years`。
4. 一線隊 Person：出生日期未填者視為成年（一線隊性質）；已知未滿 18 一律不輸出。需使用者確認這條判斷。
5. 過渡內容（國際夥伴三隊徽、11.3／11.4 三筆事蹟、磐石靜態里程碑）在後台出現任何一筆資料即整批退場，不混搭；應由內容人員補登後台。
6. 動態路由 `charity/programs/[slug]` 不在 `collect-routes` 檢查範圍（含 `[` 的頁面一律略過），標題結構以假資料手動核對過。

### 驗證（2026-10-01）
`npm run lint` 0 錯誤／364 警告（基準 395，未增加）；`npm run build` 通過。**未啟動 apps/api**（E-56）：用 scratchpad 的假後端（固定測試資料、非專案檔）餵兩個容器驗證：
各頁 tcrfc／bw 內容分區正確、bw 不出現磐石夥伴；11 對 bw 404；詳情頁 404／內容 XSS 字串被轉義；POST 轉發 IP 且不轉 cookie、下載標頭正確、press 302 保留、非法路徑 405／404；
`noindex` 仍在；API 打不到時全部落回空狀態且無 500；`check-heading-structure`（兩站）、`check-faq-schema-live`（兩站）、`check-club-brand-leak`／`check-club-image-leak`（bw，API 關閉時）皆過。
**限制**：假資料下 `check-club-image-leak` 會把 API 圖片網域判違規（既有盲區，真實資料出現後須有意識把藍鯨媒體來源加進 `ALLOWED_PREFIXES`）；瀏覽器端互動（表單真實送出、年份篩選）只驗 SSR 與代理契約，未做瀏覽器操作；頁尾／首頁 Logo 列、Hero 迷你新聞卡的視覺只看了 9.1／9.4／詳情頁三張截圖。

### 使用者實機驗收步驟（API port 5299 由使用者啟動）
1. 後台建：夥伴（勾首頁／頁尾曝光，含深淺底 Logo）、贊助商＋故事＋活動、已發布贊助方案、有檔案的已發布提案、新聞稿／識別包／高解析圖、慈善計畫＋事蹟＋公開統計、B5 捐款導流設定、里程碑／榮譽。
2. `NUXT_API_INTERNAL_BASE=http://127.0.0.1:5299` 起兩個容器（見「怎麼跑」），逐頁對照上表；9.4 實際填表取得下載連結並點下載；7.8 點下載確認後台累計次數增加；`/zh/charity/programs/{slug}/` 與 `/en/`。
3. 跑 `check-club-image-leak.mjs`，視結果決定藍鯨媒體網域是否加入允許清單。

## S2-11 會員中心／S3-2 08 文化（接上 E 批公開端點，2026-10-01，`frontend-architect`）

只動 `apps/web`。新增相依：`qrcode-generator@1.4.4`（QR 純前端產生，內容不離開瀏覽器）。新增樣式檔 `public/assets/css/member.css`（`app.vue` 載入；只用 design tokens，`tcrfc.css` 未動）。

### 架構：Nuxt 伺服器是會員工作階段的 BFF
- 後端一律以 `tokenDelivery: "body"` 呼叫；**更新權杖只存在本站 HttpOnly Cookie**（`tcrfc-member-rt-{club}`，HTTPS 時 `__Host-` 前綴＋`Secure`，`SameSite=Strict`，`p.`／`s.` 前綴記「記住我」）；存取權杖（15 分鐘）只在瀏覽器記憶體，不落 localStorage。理由：瀏覽器只和 Nuxt 同源，後端 Cookie 屬性（`SameSite=None`、`__Host-`）是為直連 API 網域設計，經代理轉發兩個方向都容易靜默失敗。與派工「瀏覽器 Cookie 模式」的效果相同（JS 讀不到更新權杖），實作位置不同。
- `server/api/member-auth/{login,refresh,logout,logout-all,change-password,line-callback,line-complete}.post.ts`：持有 Cookie，回應本文剝掉 `refreshToken`；一律 `assertSameOrigin`（Origin／Sec-Fetch-Site）＋`no-store`。
- `server/utils/member-proxy.ts`＋`[...path].ts` 入口：**白名單**（方法＋路徑形狀＋是否需 Bearer）、只轉發 `Authorization`／`Idempotency-Key`／訪客 IP，**不轉發 Cookie**，原樣轉回後端狀態碼與 ProblemDetails。**刻意不放行 `pay`／`confirm`**（主站 §3.14 網頁站內結帳未拍板）。
- **refresh single-flight**：分頁內共用 Promise；跨分頁 Web Locks＋BroadcastChannel 廣播新存取權杖／登出；伺服器端同一更新權杖「進行中」的請求合併成一次後端呼叫（不留結果快取）。實測：兩分頁同時載入 `replayRevokes=0`、併發兩次 refresh 只打一次後端、舊 Cookie 重放被後端撤銷。
- 快取：`nuxt.config.ts` routeRules 對 `/zh|en/member/**`、`/m/**`、`/api/member-auth/**`、`/api/backend/member/**` 加 `Cache-Control: no-store`；會員頁 `noindex`；會員資料只在瀏覽器端載入（SSR 只有「載入中」殼）。

### 資料來源表
| 頁 | 端點 | 狀態 |
|---|---|---|
| `/zh/member/` 登入／加入 | `member/auth/register|resend-verification`、BFF `login` | 🟢 失敗依 `code`（未驗證信箱、`account_locked`＋`lockedUntil` 以台灣時間顯示）；`emailSent=false` 如實告知「驗證信尚未寄出」 |
| `verify-email?token=`、`reset-password?token=`、`forgot-password` | `verify-email`、`reset-password`、`forgot-password`（一律 202） | 🟢 `/en/` 孿生自動產生 |
| LINE 登入／綁定／補 Email 註冊 | `line/authorize`、BFF `line-callback`／`line-complete`、`DELETE me/line` | 🟢 `state` 存 sessionStorage 並比對（15 分鐘、取用即清）；503 顯示「暫不提供」。導回頁 `/zh/member/line-callback/` |
| 會員中心（我的會籍／電子會員卡／球衣登記／個人資料與安全） | `member/memberships|cards|jerseys|me`、`membership-orders`、`{club}/member/memberships/join` 等 | 🟢 藍鯨 409 `season_not_available` 顯示後端說明；QR＝`{站台網址}/m/{token}` |
| 升級續會 | `POST {club}/member/membership-orders`（`Idempotency-Key`，鍵存 sessionStorage 至成功） | 🟢 只送 `created` 申請；畫面說明由工作人員聯繫收款開通；藍鯨會籍明示「款項由台中磐石足球俱樂部代收」（藍鯨規劃書 §5.2，僅登入後客戶端渲染） |
| `/m/{token}` 驗證頁 | `m/{token}` | 🟢 SSR、`no-store`、`noindex`、404 同一句話、只顯示後端五個欄位；`?lang=en` |
| 權益對照表 | `membership/benefits` | 🟢 `ContentMembershipBenefits` 改資料驅動（三處共用）；後台無資料時落回既有靜態說明表 |
| 8.4 特約店家 | `partner-stores`、`filters`、`{slug}` | 🟢 清單（篩選走網址 query）＋新增詳情 `/zh/perks/{slug}/`；無資料誠實空狀態 |
| 8.1 漫畫 | `comic/about|characters|episodes|episodes/latest|episodes/{n}`、`POST …/views` | 🟢 首頁＋新增閱讀器 `/zh/culture/manga/{n}/`（分頁／捲動、鍵盤、滑動、上下集）；內容純文字渲染，不 v-html |
| 8.2 球迷會 | `membership/plans`、`fan-events`（upcoming／past）、詳情、報名、取消 | 🟢 兩俱樂部同版型（移除 BW-C1 的藍鯨整頁空狀態）；新增詳情 `/zh/culture/fan-club/events/{slug}/`；限付費活動未登入引導登入（`?next=`）、`fan_club_required` 顯示後端說明 |

### 單元開關
8.1 對藍鯨維持 404（`unit: '8.1'`，閱讀器頁同；導覽早已以 `isUnitEnabledForClub` 擋；後端亦 403）。其餘無新增關閉。

### 範圍縮減與規格疑點
1. **不做「我的報名」**：派工單列了，但主站 §3.14 明文網頁前台不納入（App 才有）；API README 也寫「網頁前台不做歸戶」，以規劃書為準。
2. **「我的訂單」不在本輪**（商店下一輪）。首頁「最新集數同步曝光」未做（首頁編排 B3 尚無對應區塊定義）。
3. 球衣尺寸為自由文字（尺碼表客戶未提供）；QR 內容用目前站台網址，另一俱樂部站台亦能驗證（驗證頁只認 token）。
4. LINE 導回網址須登記在後端 `LINE_LOGIN_REDIRECT_URIS` 與 LINE Developers（`https://{站台}/zh/member/line-callback/`）。
5. 規格疑點：藍鯨 §5.2 要求「會籍付款頁」明示代收方，目前只加在升級申請區（藍鯨會籍）；是否也要放 8.2 方案區請裁決。磐石 8.2 過渡內容（4 張活動照）在後台出現任何「活動回顧」即整批退場。

### 驗證（2026-10-01）
`npm run lint` 0 錯誤／340 警告（基準 364，未增加）；`npm run build` 通過；`nuxi typecheck` 本輪新增檔案 0 錯誤。**未啟動 apps/api**（E-56）：以 scratchpad 假後端（模擬 E 批契約）＋兩個容器＋CDP 實機走完：註冊／未驗證登入／登入／重新整理還原／電子卡 QR／升級申請（冪等鍵、無 pay／confirm 呼叫、Cookie 不轉給後端）／LINE 503／state 不符丟棄／跨分頁登出與併發 refresh／驗證頁／閱讀器／活動報名取消／限付費活動／特約店家篩選；390px 無橫向溢出；`check-heading-structure`、`check-faq-schema-live`、`check-club-brand-leak`、`check-club-image-leak` 兩站皆過。限制：未對真實 API 與真實 LINE／寄信驗證。

### 使用者實機驗收（API port 5299 由使用者啟動）
1. 後台建：會籍方案與權益條目、特約店家、球迷會活動（含限付費）、漫畫（已發布且發布日已到）。
2. 起兩個容器（見「怎麼跑」，`NUXT_API_INTERNAL_BASE=http://127.0.0.1:5299`）；`EMAIL_OUTBOX_PATH` 的信件檔點連結走完驗證與重設密碼。
3. 走一遍：註冊→驗證→登入→會員卡 QR（手機掃開 `/m/…`）→重產 QR 舊連結 404→升級申請→後台 K2 開通→會籍變「已開通」→球衣登記→兩分頁同時開會員中心→活動報名。
4. 藍鯨容器：`/zh/culture/manga/` 404、加入藍鯨得到「沒有開放的球季」說明。

## S3-5／S3-5a／S3-9（8.3 站內商店 7 頁流程、Product Schema、積分榜與球員數據，2026-10-01，`frontend-architect`）

只動 `apps/web`。對接 `apps/api README`「F 批」契約。新增：`server/api/shop/[...path].ts`（BFF）、`server/utils/shop-session.ts`、`shared/utils/{shop,shop-schema,standings,core-values,page-blocks}.ts`、`app/composables/{useShop,useShopInfo,useProductSchema}.ts`、`app/components/shop/ShopOrderView.vue`、`app/components/member/MemberOrders.vue`、`app/components/content/PageBlocks.vue`、`public/assets/css/shop.css`、`scripts/check-shop-lib.mjs`（已掛 `npm run lint`，91 項斷言）。

### 架構：商店 BFF `/api/shop/**`
- 瀏覽器與 SSR 只呼叫 `/api/shop/{endpoint}`，俱樂部取自容器 `NUXT_PUBLIC_CLUB`（瀏覽器**不能指定**，購物車不得跨俱樂部混買）。**白名單**列明方法＋路徑形狀＋身分模式，其餘 404／405；查詢字串只放行各路由列出的鍵並檢查形狀。非目錄請求一律 `assertSameOrigin`。
- 訪客購物車權杖 `X-Cart-Token`、訂單權杖 `X-Order-Token` 只存 **HttpOnly Cookie**（`tcrfc-shop-ct-{club}`／`tcrfc-shop-ot-{club}`，HTTPS 時 `__Host-`，SameSite=Lax，30 天），由 BFF 依 Cookie 帶標頭，**回應本文剝掉 `cartToken`／`accessToken`**。不轉發瀏覽器 Cookie。件數另寫非 HttpOnly 的 `tcrfc-shop-n-{club}`（只有一個整數）供頁首購物車徽章（原本寫死「2」，已改真值，僅瀏覽器端渲染）。
- 會員：瀏覽器帶 15 分鐘存取權杖（沿用 `useMemberSession`）。登入後載入購物車走 `cart/merge`（沒有訪客 Cookie 時 BFF 直接回會員購物車）；merge 成功清掉訪客 Cookie。信件連結權杖查單成功後權杖寫進 Cookie（可繼續付款），並把網址上的 `token` 移除。
- `pay` 回的 `paymentUrl` 非 `https://` 一律視為 null。全部 `Cache-Control: no-store`（BFF、`/zh|en/{shop,cart,checkout,order}/**` routeRules）；購物車／結帳／結果／查單／會員頁 `noindex`，內容只在瀏覽器端載入。

### 資料來源表
| 頁 | 狀態 |
|---|---|
| `/zh/shop/` 列表 | 🟢 篩選（系列／尺寸／顏色／價格／新上市）、排序、分頁（網址 query＋`<form method="get">`，無 JS 可用）；缺貨／優惠／新上市標示；`info` 入口介紹、運費規則；藍鯨明示代收 |
| `/zh/shop/{slug}/` 詳情（取代兩張寫死示意頁） | 🟢 圖集、規格選擇（尺寸／顏色／自訂標籤，售完停用）、可售量、促銷價、尺碼表（容錯解析後端 JSON）、商品介紹（純文字）、加入購物車／直接結帳；不存在 404；Product＋Offer Schema |
| `/zh/cart/` | 🟢 數量／移除／小計／運費／免運差額皆後端值；`canCheckout=false` 停用結帳；庫存錯誤顯示後端說明並重新載入 |
| `/zh/checkout/` | 🟢 收件資料、三種配送、四種發票（載具格式、統編檢核碼、捐贈碼清單）、條款；冪等鍵存 sessionStorage 至成功；建單後請款並導向 LINE Pay；請款失敗保留訂單可重試；會員自動帶入帳號資料 |
| `/zh/checkout/complete/` | 🟢 `?orderNo&transactionId`→confirm、`&cancel=1`→cancel、其餘查看狀態；**付款成功只認後端 `paymentStatus=paid`**（偽造 transactionId 實測不會顯示成功） |
| `/zh/order/lookup/` | 🟢 訂單編號＋Email（遮罩）、`?token=` 信件連結（完整、可付款）；找不到同一句話 |
| 會員中心「我的訂單」 | 🟢 清單、展開明細、待付款可付款／取消；退換貨只連政策頁與聯絡表單（規格不做退貨精靈） |
| `/zh/shop/policy/`（新增） | 🟢 購物須知／運送／退換貨／交易條款（S6 維護，空段不顯示） |
| 首頁官方商店入口 | 🟢 前 3 件上架商品（名稱／圖／價，不顯示庫存）；磐石無商品維持靜態入口，藍鯨無商品整區不顯示 |
| 首頁核心價值 | 🟢 `home/core-values`（排序、名稱）＋前台依 `code` 補說明；API 空／失敗退回同順序備援；仍只對磐石顯示（藍鯨版標籤文字待確認） |
| 3.1 一線隊積分榜／球員數據 | 🟢 `standings`、`stats/players`，球季 `?season=`；**助攻 `null` 顯示「—」、0 顯示 0**；無資料誠實空狀態 |
| 球員詳情（取代「範本」頁） | 🟢 `/zh/club/first-team/player/{id}/`：基本資料＋`players/{id}/stats` 本季與逐季；名單卡片連過去；舊 `/player/` 302 回名單 |
| 11.1 慈善理念 | 🟢 讀 `pages/charity/commitment`（區塊於伺服器端正規化為純文字節點、連結白名單）；無頁面／無可渲染區塊維持靜態內容 |

### 規格判斷與範圍縮減（非規格，請裁決）
1. **`paymentAvailable=false`（或取不到 info）時結帳頁停用送出、不建單**。API README 寫「可結帳至建立訂單」，但建了付不了只會占庫存 30 分鐘；主要要求是明示並不得假裝付款成功。
2. **付款導回網址約定**（API 目前未指定，見缺口一）：`{站台}/{lang}/checkout/complete/?orderNo={no}&transactionId=…`／`&cancel=1`，建構函式 `checkoutReturnUrl`。
3. Product Schema：多規格用 `AggregateOffer`，`@id` 全唯一，AggregateOffer 的 `availability` 明確給（模組會替缺值補 InStock）；供貨狀態 `<5 件`＝LimitedAvailability，不輸出具體庫存。
4. 規格選項來源（尺寸／顏色下拉）取不帶篩選的前 60 件；超過 60 件商品時選項可能不全。
5. 圖文左右、藝廊、影音、手風琴、檔案下載等 B1 區塊本輪不渲染（只渲染純文字型）。
6. 球員詳情不顯示生日（未成年個資）；「相關新聞」「影片」兩區塊沒有資料來源，已移除而非放空殼。
7. 13 賽事行事曆頁**沒有**放積分榜／球員數據（規格 §3.1 把它們放在一線隊「成績與排名」），只在 3.1 一線隊頁。
8. 新聞的「商品頁 SEO 標題」採後台 `seoTitle`，沒有則自動組。

### API 缺口
1. **LINE Pay 導回網址**：`IPaymentGateway.ReserveAsync` 沒有 confirm／cancel URL 參數；正式實作需把 LINE Pay 的 `confirmUrl`／`cancelUrl` 設成上面第 2 點的網址（LINE Pay 會附 `transactionId`）。
2. 球員沒有 slug／單一球員端點（App 規劃書深連結寫 `/player/{slug}`），目前以 GUID、並由名單端點取基本資料（上限 100）。
3. 商品列表沒有「可用篩選選項」端點（尺寸／顏色清單）。

### 驗證（2026-10-01）
`npm run lint` 0 錯誤／305 警告（基準 340，未增加）；`npm run build` 通過；`nuxi typecheck` 新增檔案 0 錯誤（既有檔案的既有錯誤未動）。**未啟動 apps/api**（E-56）：scratchpad 假後端（依 F 批契約，非專案檔）＋兩個容器：
- BFF 52 項（`curl`／Node）：白名單 404／405、跨站 403、未知 query 被丟、Cookie 屬性、後端**從未收到 Cookie 標頭**、權杖剝除、**並行 6 個同鍵只成立一張**、重複／並行 confirm 冪等、遮罩查單、權杖查單。
- 瀏覽器 CDP E2E：訪客全流程（選規格→購物車→錯誤驗證→**雙擊送出只一張訂單**→導向 LINE Pay→導回確認→重整→偽造 transactionId→取消→查單）；會員流程（登入→merge→預填→我的訂單）；`paymentAvailable=false`；藍鯨明示代收；核心價值／B1 理念；390px 13 頁無橫向溢出。
- `check-heading-structure`、`check-faq-schema-live`（兩站）、`check-club-brand-leak`／`check-club-image-leak`（bw，API 關閉；假資料開著時圖片網域會被判違規，既有盲區）皆過。品牌例外清單只減不增（移除 socks 與 player 兩組）。
**限制**：未對真實 API／LINE Pay／寄信驗證；假後端的冪等是它自己實作，真正的並行冪等由 API 測試鎖定。

### 使用者實機驗收（API port 5299 由使用者啟動）
1. 後台建：S6 入口與政策、系列、上架商品（含多規格、促銷價、尺碼表 JSON、圖片）、發票捐贈碼；賽事積分榜與已結束賽事；B1 頁面 `charity/commitment`。
2. `NUXT_API_INTERNAL_BASE=http://127.0.0.1:5299` 起兩個容器，走：商品→購物車→結帳（`PAYMENT_GATEWAY=fake` 時請款回假網址，可手動開 `…/checkout/complete/?orderNo=…&transactionId=FAKE-{訂單編號}` 模擬導回）→我的訂單；Production 設定下 `paymentAvailable=false` 應看到停用說明。
3. 藍鯨容器：商品頁／結帳頁出現代收說明；首頁商店入口隨商品出現。

## 相關文件

- [`docs/02-frontend-spec.md`](../../docs/02-frontend-spec.md) — 前台頁面規格
- [`docs/13-blue-whale-site.md`](../../docs/13-blue-whale-site.md) §6／§6a — 共用映像檔的技術判斷、七個品牌色變數
- [`docs/05-i18n-seo.md`](../../docs/05-i18n-seo.md) — 雙語、SEO、GEO
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) — E-16／E-17／E-18，本次骨架踩到的三個坑
- [`STATUS.md`](../../STATUS.md) — S0-9a（本次成果）、S0-9（完整搬遷，尚未開始）
