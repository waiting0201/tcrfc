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
`NewsCard.vue`／`NewsCategoryTabs.vue`（07 單元多頁共用）、以及**首頁**
`zh/index.vue`（含 `shared/utils/club-copy.ts` 裡 `ctaPrimaryHref`／`ctaSecondaryHref`／
`pillars[].href`／`ctaTrio[].href` 這幾個「裸 `/zh/...` 路徑」資料欄位的消費端）與
`about/ecosystem.vue` 的內部連結，已一律改用 `useLocale().lp()` 換算成目前語系版本。
🔴 **其餘約 65 個純靜態頁面（尚無真實英文內容、也不在本輪 lang 參數清單內）的內部連結
仍是硬編碼 `/zh/...`**——在自己的 `/en/...` 孿生路由上會把讀者連回 `/zh/...` 而不是
留在 `/en/...`。這是刻意的範圍邊界（那些頁面本來就 100% 中文內容，連到 zh 版本不算
明顯錯誤，只是不夠一致），機制上可以用跟本輪同一招（`href="/zh/...` → `:href="lp('/zh/...')"`
的正規表達式替換＋補 `useLocale()`）批次處理，留給下一個做這批頁面英文化的人一併做，
不在 S1-13 框架範圍內。

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

## 相關文件

- [`docs/02-frontend-spec.md`](../../docs/02-frontend-spec.md) — 前台頁面規格
- [`docs/13-blue-whale-site.md`](../../docs/13-blue-whale-site.md) §6／§6a — 共用映像檔的技術判斷、七個品牌色變數
- [`docs/05-i18n-seo.md`](../../docs/05-i18n-seo.md) — 雙語、SEO、GEO
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) — E-16／E-17／E-18，本次骨架踩到的三個坑
- [`STATUS.md`](../../STATUS.md) — S0-9a（本次成果）、S0-9（完整搬遷，尚未開始）
