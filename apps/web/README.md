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

1. **後端沒有這五類事實的欄位與公開端點**：見上方「事實盤點結論」。`site-facts.ts` 是
   前台暫定方案，等後台 `I` 網站設定（或其所屬模組）補上對應欄位與公開端點後，要把這裡
   整批改成 `useFetch`，呼叫端（`SITE_FACTS[club].xxx`／`getPrimaryVenue()`／
   `academyTeamCodesLabel()`）的介面盡量維持不變。
2. **`schedule.vue` 的逐隊代碼篩選（`state.team`／`teamLabels`）仍寫死磐石代碼**：見上方
   「真的修正的既有缺口」，這是比本輪任務範圍更大的架構問題，留給 `S1-19`。
3. **多個單元 `03`／`04`／`10-location`／`10.2` 頁面仍是零俱樂部分支的既有缺口**：見上方
   「刻意不動的範圍」，本輪只換掉事實來源，沒有加上整頁雙俱樂部分支。
4. **無頭瀏覽器與真實 API 的 JSON-LD 輸出未驗證**：見上方「JSON-LD 雙重呈現」小節，
   延續 S1-12a／b／c／f 同一個環境限制（本機啟動 `apps/api` 需要在指令列具現化資料庫
   密碼，被 session 自動模式安全防護擋下，依硬規則被擋就停）。

## 相關文件

- [`docs/02-frontend-spec.md`](../../docs/02-frontend-spec.md) — 前台頁面規格
- [`docs/13-blue-whale-site.md`](../../docs/13-blue-whale-site.md) §6／§6a — 共用映像檔的技術判斷、七個品牌色變數
- [`docs/05-i18n-seo.md`](../../docs/05-i18n-seo.md) — 雙語、SEO、GEO
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) — E-16／E-17／E-18，本次骨架踩到的三個坑
- [`STATUS.md`](../../STATUS.md) — S0-9a（本次成果）、S0-9（完整搬遷，尚未開始）
