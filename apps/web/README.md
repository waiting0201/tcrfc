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

離開碼：`PROTECTED_PAGES`（已宣告完工、必須保持乾淨的頁面）裡任何一頁命中詞表，或
保護清單本身被棘輪擋下（清單只能往上加、不能往下拿）→ `1`；其餘頁面的命中只當進度計
印出來，不影響離開碼。

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
