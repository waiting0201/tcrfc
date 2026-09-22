# apps/admin — admin-web（官網共用後台）

台中磐石官網主站與台中藍鯨官網**共用同一個後台**的 Vue 3 SPA。

🟡 **目前是可以在瀏覽器裡點的 mockup（S0-12），不是接上真實資料庫的產品**：外殼＋儀表板＋
「新聞與故事」的列表頁與編輯頁是真的可以互動的畫面，其餘 13 個模組是明確的「尚未建置」佔位頁
（不是死連結）。所有資料都是寫死在前端的假資料，**刻意不接 `apps/api`**——那支 API 目前唯讀、
只回已發布內容，接了反而驗證不到草稿／排程發布／已停用這三種非公開狀態（見下方「假資料」）。

規格的真實來源是規劃書與 [`docs/03-admin-spec.md`](../../docs/03-admin-spec.md)；版面與視覺規則是
[`docs/21-admin-ui.md`](../../docs/21-admin-ui.md)（v3，`visual-design-architect` 產出，
本次實作依它逐條核對）。

🔴 **深色是唯一主題，不是淺色的延伸**（2026-09-21，`S0-12c` 依 `docs/21` v2 重做）：舊的淺色＋
Element Plus 預設藍版本已整批換掉，**不做主題切換開關，不留淺色 token 分支**（`docs/21` §7.0
使用者已拍板）。版面骨架也不是換色而已——頂欄拆成系統列／頁面列兩層、側欄選中態改用 accent bar、
狀態 tag 換成深色語意底＋亮色文字、編輯頁分段卡片改用四層背景色階（tonal elevation）取代陰影、
圖片預覽區塊加了不隨主題變化的「中性看片台」。細節見下方各節與 `docs/21` §1–§9。

🔴 **配色 v3（2026-09-22）：中性深灰＋Element Plus 藍 → 品牌黑＋品牌桃紅**（依 `docs/21` §4／§5／§7／§15
重做）：四層背景色階改由品牌黑 `--ink`／`--ink-2` 推導、操作主色改用品牌桃紅系 `--brand-aa`／
`--brand-bright`／`--brand-deep`、文字與邊框階跟著暖化、危險色橘紅化（`H≈0°→14°`）、站台切換器隊徽
從純色色塊改用兩隊各自的真實隊徽圖像（`src/assets/brand/`）。**切換站台仍然不換主色**（`docs/21`
§5.1／§5.1.1：後台永遠是磐石桃紅）。中性看片台（`--admin-lightbox-*`）明文不隨這次換色調整，維持
中性冷灰。`--admin-border-input` 用的是**門檻反推值 `#8A7E75`**，不是暖化等亮度換算的 `#7A6F68`——
後者對 `overlay` 只有 2.66:1，過不了 3:1 的 UI 元件門檻，這條與 E-31 同根因，已寫進
`scripts/check-contrast.mjs` 的邊框笛卡兒積檢查（見下方「檢查腳本：對比度檢查」）。

---

## 怎麼跑

```bash
cd apps/admin
npm install
npm run dev        # http://localhost:5174，熱重載
npm run build      # 型別檢查（vue-tsc）＋ Vite production build，輸出到 dist/
npm run preview    # 起一個本機 server 服務 dist/，用來驗收 build 產物
npm run lint       # ESLint ＋ 禁用詞掃描 ＋ 對比度檢查（見下方「檢查腳本」），三者都要過
```

Docker（多階段 build，nginx 服務靜態檔）：

```bash
docker build -t tcrfc-admin apps/admin
docker run --rm -p 8080:8080 tcrfc-admin
curl -I http://localhost:8080/          # 應該看到 X-Robots-Tag: noindex, nofollow
curl http://localhost:8080/healthz      # 應該回 "ok"
```

## 專案結構

```
apps/admin/
├── src/
│   ├── main.ts              # 掛載點：固定在 <html> 掛 class="dark"（深色是唯一主題，不做切換開關）
│   ├── App.vue               # 只有一個 <router-view />
│   ├── router/index.ts       # 路由表：已實作的頁面 + 從 data/nav.ts 自動展開的佔位頁路由
│   ├── layouts/AdminLayout.vue   # 外殼：側欄／頂欄／drawer／響應式斷點切換（桌面／平板／手機）
│   ├── components/
│   │   ├── AppSidebar.vue        # 側欄選單（6 組視覺分組、el-sub-menu 手風琴、選中態改 accent bar）
│   │   ├── AppTopbar.vue         # 系統列（桌面/平板 40px）／合併列（手機 56px，含頁面標題）
│   │   ├── PageHeader.vue        # 頁面列：模組標題＋「這裡管理的是」固定兩行，各頁面內容區頂端自己渲染
│   │   ├── SiteSwitcher.vue      # 站台切換器（docs/21 §5：只換徽章，不換整套主色；手機移進 drawer 頂部）
│   │   ├── UserMenu.vue
│   │   ├── FrontendUnitBanner.vue # 「這裡管理的是：{前台單元} ↗」（規劃書 §4.0 硬性規定）
│   │   ├── StatusTag.vue         # 草稿／排程發布／已發布／已停用 四態，深色語意底＋亮色文字（非 el-tag 預設 type）
│   │   ├── ImageUploader.vue     # S0-8 起接上真實上傳端點，見下方「圖片上傳共用元件的前端接線」；預覽框鋪中性看片台（docs/21 §9.1）
│   │   └── BilingualShortField.vue # 雙語短欄位：桌面並排、手機自動變 tabs
│   ├── views/
│   │   ├── DashboardView.vue
│   │   ├── PlaceholderView.vue   # 尚未建置模組的共用畫面（不是死連結）
│   │   ├── NotFoundView.vue
│   │   └── news/
│   │       ├── NewsListView.vue  # 列表頁標準型（篩選列／批次操作列／表格／分頁／空狀態）
│   │       └── NewsEditView.vue  # 編輯頁標準型（分段表單／雙語／狀態／離開未儲存提醒）
│   ├── composables/
│   │   ├── useBreakpoint.ts      # 斷點判斷（<768 / 768–1024 / ≥1024）
│   │   └── useUnsavedChanges.ts  # 路由離開 + beforeunload 兩處攔截
│   ├── data/                 # 假資料與靜態設定，見下方「假資料放哪」
│   ├── types/                 # 共用 TypeScript 型別
│   └── styles/admin-theme.css # 深色 design tokens：四層背景色階、文字/邊框/主色/四態色票（docs/21 §7）
├── scripts/
│   ├── check-forbidden-terms.mjs  # 禁用詞掃描（見下方）
│   └── check-contrast.mjs         # 對比度檢查：token 表 × docs/21 §7 逐一比對（見下方，E-31 的防呆）
├── Dockerfile / nginx-spa.conf / .dockerignore  # noindex 標頭固定在每個 location（見 E-29）
└── package.json
```

## 假資料放哪

`src/data/` 是這份 mockup 唯一的資料來源：

| 檔案 | 內容 |
|---|---|
| `nav.ts` | 側欄的 14 個一級模組（`docs/03-admin-spec.md` §1 的模組代號）、6 組視覺分組、哪些已實作 |
| `frontendUnits.ts` | 模組代號 → 前台單元名稱與網址，**逐字取自規劃書 §4.0「前後台對照表」**，不是自己編的 |
| `session.ts` | 目前登入帳號、可操作的俱樂部清單（改成只留一筆可以測「單一俱樂部不顯示切換箭頭」的行為） |
| `dashboard.ts` | 儀表板的待辦提醒／行程／快速入口／統計數字 |
| `news.ts` | 8 篇文章的初始假資料，涵蓋四態、共用內容、有無封面圖等組合 |
| `newsStore.ts` | 把 `news.ts` 包成一個 `reactive()` 單例，讓列表頁與編輯頁共用同一份記憶體資料——編輯頁存檔後回列表看得到變更，重新整理頁面才會重置回初始假資料 |

**沒有 Pinia**，`newsStore.ts` 是純模組層級的 reactive 單例，因為目前只有一個資料模組用得到跨頁共用
狀態；之後模組多起來、狀態間有耦合時再評估要不要導入正式的狀態管理框架。

## 檢查腳本：禁用詞掃描

`npm run lint` = `eslint .` + `node scripts/check-forbidden-terms.mjs`。後者對應規劃書 §4.0 後台設計
通則③④（也是 `docs/06-conventions.md` §1）：**後台畫面不得出現模組代號（`B1`／`K4`）、權限碼
（`shop.order.export`）、隊別代號（`D1`／`BW1`）、英文技術詞（`slug`／`canonical`／`SKU`…）**。

做法：只解析每個 `.vue` 檔的 `<template>` 區塊，先丟掉 `{{ }}` 內插值與 `:xxx=`／`v-xxx=`／`@xxx=`
動態綁定（這些是程式碼識別字，不是使用者看得到的文字），只留下純文字節點與少數會直接顯示成文字的靜態
屬性（`placeholder`／`label`／`title`／`alt`／`aria-label`…），再對這段「畫面文字」比對禁用清單。
`<script>`／`<style>` 完全不掃——那裡本來就該是英文識別字。

**寧可少抓也不要誤報**：模組代號只比對「字母＋數字」的完整清單（`B1`…`S6`），不比對裸的單字母
（`A`／`H`／`I`）——英文分頁內容常見獨立單字 "I"，裸字母規則會一直誤報；權限碼的偵測規則會排除看起來
像網域的字串（`www.tcrfc.tw` 這種），避免把「正規網址」欄位的範例文字誤判成權限碼。已用「暫時塞一個
`slug`／`B2` 進畫面文字確認腳本真的會抓到、改回來確認會過」驗證過腳本本身有在運作，不是空腳本。

已知限制：`{{ }}` 內插值裡如果真的寫死一個禁用字串常值（例如 `{{ 'B2' }}`），腳本目前抓不到——這種
寫法很少見，抓太寬會把每一個 prop 值、資料欄位都當成可疑，誤報率太高，權衡後選擇漏抓這種情況。

## 檢查腳本：對比度檢查

`scripts/check-contrast.mjs`（`npm run lint:contrast`）針對 `docs/21-admin-ui.md` §7／§4／§15 的深色
色票表逐一驗算 WCAG 對比度，防的是 [`docs/18` E-31](../../docs/18-work-errors.md)——那次色票表宣稱
「全部實測過」，實際有兩組沒驗到／驗錯，其中一組還漏驗了四層背景裡最亮的 `overlay` 層。

v3 配色改版時同一個根因又踩了一次：`--admin-border-input` 的暖化換算值只驗了 `surface` 一層，沒驗到
`overlay`（實際只有 2.66:1，過不了 3:1）。腳本因此擴大範圍，現在跑六類檢查：①文字 × 背景五層笛卡兒積
（4.5:1）②**邊框 × 背景五層笛卡兒積（3:1，這次新補的洞）**③豁免 token（disabled、純裝飾邊框，逐層
算出數字＋理由，不是默默跳過）④成對色票（四態 tag、語意色按鈕、primary 文字四層、focus 外框五層）
⑤反例與已知限制釘住（含 `--admin-primary` 對 `overlay` 明文只有 4.02:1 這種「規格承認的已知限制」，
以及 border-input 若誤用舊值 `#7A6F68` 的迴歸記錄）⑥規格 vs 實作比對。用「塞回錯誤值確認抓得到、
再復原」驗證過兩次：一次改 `admin-theme.css` 的值（規格 vs 實作那關會抓到），一次改腳本自己的
`BORDERS_CARTESIAN` 常數（笛卡兒積本身會抓到，不是只靠字串比對）。

## 響應式：三個斷點都手動驗證過

`docs/21-admin-ui.md` §8（使用者 2026-09-21 拍板：後台要完整響應式）。用無頭 Chrome + CDP
（`Emulation.setDeviceMetricsOverride`，不是 `--window-size` 命令列參數——後者在無頭模式下有實測過
的視窗 floor 問題，見專案外的 agent 除錯筆記；呼叫 `/json/new` 開分頁要用 `PUT` 不是 `GET`，見
[`docs/18` E-32](../../docs/18-work-errors.md)）逐一驗證過：

- **桌面 `≥1024px`**：系統列 40px＋頁面列 64px 兩層、側欄常駐可收合（240px/64px，收合偏好存
  `localStorage`）、表格完整欄位、側欄選中態 accent bar 清楚可見。
- **平板 `768–1024px`**：頂欄同桌面兩層、側欄強制收合但可手動展開、表格隱藏次要欄位（分類／發布時間）。
- **手機 `<768px`**：頂欄合併成單列 56px（hamburger＋標題＋通知＋使用者頭像）、側欄變 `el-drawer`
  （站台切換器移進抽屜頂部，導覽後自動關閉）、頁面列的標題不重複顯示、「這裡管理的是」落到內容區
  最上方、列表頁表格換成卡片式（每筆一張 `el-card`，只留封面／標題／分類／狀態／操作，縮圖 56×56px）、
  編輯頁雙語欄位（含長欄位）一律變成 `el-tabs`、底部操作列的按鈕改滿版寬度堆疊。

深色生效範圍額外確認過 `el-dialog`／`el-dropdown`／`el-select` 下拉／`el-drawer` 四種浮層元件
（用真實滑鼠事件點開後截圖比對，不是只看 `docker build` 成功），背景都正確落在 `--admin-bg-overlay`
`#2A2E36`，沒有殘留淺色區塊；`el-popover`／`el-tooltip` 沒有另外截圖，但兩者與 dropdown／select 走
同一套 Element Plus popper 底層機制與同一組 `--el-bg-color-overlay` 變數，架構上已一併涵蓋。

## v2 深色重做時的實作取捨（`docs/21` 沒有逐字規定、由這次實作判斷的地方）

- **頁面列（PageHeader）跟著內容區捲動，不是釘住在系統列下方**：`docs/21` §1.2 的 wireframe 畫出來的
  堆疊順序是「系統列→頁面列→內容區」，但沒有明講頁面列要不要 `sticky`；v1 的標題／banner 本來就是
  跟著內容捲動的。這次選擇沿用同樣的捲動行為（`PageHeader.vue` 是每個 view 自己在內容區頂端渲染，
  不是 `AdminLayout` 的固定 header 的一部分），因為改成真正 sticky 需要一個跨元件的頁首狀態（各 view
  的標題／banner 傳給外層 layout），複雜度換來的視覺差異不大，且 §1.2 真正要解決的問題（單列頂欄擠爆、
  手機換行裁字）跟 sticky 與否無關。如果之後要補成真的 sticky，`PageHeader` 目前用「負邊界 bleed 到
  內容區邊緣」做出獨立色塊的手法要整個換掉。
- **手機合併列（`AppTopbar.vue` 的 `isMobile` 分支）背景色用 `surface-2`**：`docs/21` §8.2 沒有指定
  這一列的色階，只說它是系統列＋頁面列合併後的結果。這次選 `surface-2`（系統列原本的色階），理由是
  合併後這一列仍然承載 hamburger／通知／使用者選單這些系統層級控制，視覺識別上延續系統列比較合理。
- **編輯頁的「這裡管理的是」與狀態列合併進 `PageHeader` 的 `#meta` 插槽，疊成兩行**：`docs/21` §3.2
  的 wireframe 只畫出標題下面一行「狀態：…」，沒有畫出 banner 出現在哪；§6 另外又說編輯頁的 banner
  「同樣位置」。這次的讀法是兩者都屬於頁面列的 meta 區塊，各自一行（banner 在上、狀態在下），沒有
  互相取代。
- **`ImageUploader` 新增 `variant: 'photo' | 'logo'` prop 控制中性看片台是純灰還是棋盤格**：
  `docs/21` §9.1 只描述兩種底色何時使用，沒有規定用什麼機制切換；預設 `'photo'`，之後有隊徽／去背
  類上傳情境時記得傳 `variant="logo"`。
- **站台切換器隊徽圖檔放在 `src/assets/brand/`，不是直接引用 `apps/web/public/` 或專案根目錄 `brand/`
  資料夾**（v3，`docs/21` §5.3）：`apps/admin` 是獨立的 Vite 專案，建置時只看得到自己目錄底下的檔案，
  所以把磐石隊徽（`brand/svg/tcrfc-mark-pink.svg` 的複本）與藍鯨隊徽（`apps/web/public/assets/brand/bw/
  favicon-48.png` 的複本）各拷貝一份進來，透過 Vite 的資源匯入機制取得建置後的網址。**這是複本，
  不是唯一來源**——藍鯨隊徽的唯一來源仍是 `brand/blue-whale/`，磐石隊徽仍是 `reference/TCR_logo_CMYK.ai`
  萃取出的 `brand/svg/`，日後那兩處的圖檔更新了，這裡的複本要跟著手動同步。

## 圖片上傳共用元件的前端接線（S0-8 修正版，2026-09-22：單一請求）

🔴🔴🔴 **本節整節改寫**：S0-8 原始接線（選檔後立刻呼叫獨立上傳端點拿物件鍵）被發現違反規劃書
第 53 行／第 972 行「選檔不上傳、儲存才上傳……離開或取消表單不留下任何檔案」，`apps/api` 已把
該兩段式端點整支移除，改成建立／更新文章時封面圖片跟其餘欄位一起送出的單一 `multipart/form-data`
請求（契約見 [`apps/api/README.md`](../api/README.md)「圖片上傳共用元件」整節）。本節記錄跟進
後的前端行為，取代舊版接線紀錄。目前仍只有新聞編輯頁的封面圖（`articles.cover`）這一個插槽真的
接線——後端 `UploadSlotPolicy` 目前也只開放這一格，其餘模組的圖片欄位等各自動工時再依樣接。

**流程**：選檔（或拖曳）→ HEIC 在瀏覽器端轉成 JPEG（[`docs/17-deployment.md`](../../docs/17-deployment.md)
§6 定案，用 `heic2any`，動態 `import()` 避免那顆內建 WASM 解碼器拖大主要頁面的打包體積）→ 前端驗證
格式／大小（10MB）／解析度（`minWidth`／`minHeight`，預設 1600×900）→ 立刻顯示本機預覽（`URL.
createObjectURL`，檔案只存在瀏覽器記憶體，**不呼叫任何 API**）→ 把通過驗證的 `File` 物件透過
`update:file` 交給外層表單（`ImageUploader.vue` 是完全受控元件，`file`／`removeCover` 都是
`v-model` prop，不是內部 state）→ 使用者按下「儲存」時，`NewsEditView.vue` 才把這個 `File`
（`coverFile` ref）跟其餘欄位的 JSON payload 組成同一個 multipart 請求，一次送給建立／更新 API
（`apps/admin/src/api/adminNews.ts` 的 `createAdminNews`／`updateAdminNews`）。

**封面圖片三態在前端怎麼表達**：`NewsEditView.vue` 用兩個獨立於 `form`（`NewsArticle`）之外的
本機狀態 `coverFile: File | null` 與 `removeCover: boolean` 表達使用者這次瀏覽階段的意圖——
選新檔案＝`coverFile` 有值（換新，同時把 `removeCover` 重置為 `false`）；點擊既有封面圖的移除
按鈕＝`removeCover = true`（清空，等按下儲存才真的生效）；兩者都沒有＝維持不變。存檔成功或重新
載入資料時（`applyLoadedArticle`）兩者都會重置為初始值，這是「離開或取消表單不留下任何檔案」
在多次編輯之間仍然成立的關鍵——這兩個值只存在瀏覽器分頁的記憶體，從未送出就會被捨棄。

**已知限制：編輯既有資料時，已經存過的封面圖沒有辦法在後台預覽**（這條不受本次修正影響，維持
原樣）——物件儲存容器目前是私有（`PublicAccessType.None`），也還沒有任何「用物件鍵換可顯示網址」
的端點，這是後端契約本身的邊界。畫面上這種情況顯示「已上傳圖片，目前系統還無法在後台預覽」的
提示卡片，不是空的上傳框（避免誤以為沒有圖片），這次額外在這張卡片上補了一顆移除（×）按鈕——
v1 規格「換圖/移除…全部沿用 v1」列出的既有行為裡，「移除」原本只在本機已選新檔案的預覽卡片上
有按鈕，既有封面圖（無法預覽的那張卡片）沒有對應入口，這是舊版接線的既有落差，這次補齊，不是
新設計（沿用同一顆 `.image-uploader__remove-btn`）。`ImageUploader.vue` 已經留了
`existingPreviewUrl` 這個 prop，等後端補上換算網址的管道之後，呼叫端把算出來的網址傳進來就會
自動改顯示真正的預覽圖，元件本身不需要再改。

**實測**（本機環境：既有 `sqlserver` 容器 ＋ `npx azurite-blob --skipApiVersionCheck` ＋
`dotnet run` 開 `ENABLE_UNSAFE_DEV_WRITES=true` 的 `apps/api` ＋ `npm run dev` 的這個專案，用
headless Chrome + CDP（`Emulation.setDeviceMetricsOverride` 固定桌面寬度 1440px）真的操作瀏覽器、
攔截 `Network.requestWillBeSent`／`Network.getResponseBody`／`Network.getRequestPostData` 確認
真實網路行為，不是模擬）：

- 🔴 **核心驗收：選了封面圖片但沒按儲存就離開，Azurite 裡不留任何物件**——在「新增文章」頁選
  中文標題、網址名稱，並選一張 2000×1400 的 JPEG 當封面（畫面確實顯示本機預覽），全程**攔截到
  的 API 請求數是 0**；接著用 `Page.navigate` 做一次真正的整頁離開（不經過 Vue Router，等同關
  分頁）。離開前後直接查 Azurite 容器內容：**物件數量不變（0 → 0，同一批基準測試環境下）**，
  沒有任何孤兒物件——這正是「選檔不上傳、儲存才上傳」在單一請求契約下的天然結果：沒有送出過
  請求，儲存體從來沒被寫入過。
- **建立文章夾封面圖片**：選檔（不送請求）→ 填標題與網址名稱 → 按「儲存草稿」→ 攔截到
  **恰好一個** `POST /api/v1/admin/tcrfc/news` 請求，`multipart/form-data` body 含 `payload`
  （JSON）與 `file` 兩個欄位；回應的 `coverKey` 開頭是 `tcrfc/articles/<id>/cover/`；直接查
  Azurite，五個物件（主檔＋1280／640／320／縮圖）全部存在。
- **換圖（更新，夾新檔案）**：在已有封面圖 A 的編輯頁選封面圖 B → 按「儲存草稿」→ 攔截到
  `PUT /api/v1/admin/{id}` 請求，回應 `coverKey` 換成 B 的鍵；查 Azurite，**A 的五個物件全部
  消失，B 的五個物件完整保留**。
- **清空封面（`removeCover`）**：在已有封面圖的編輯頁點擊既有封面卡片上新增的移除（×）按鈕
  （畫面立刻變回空的拖放區）→ 按「儲存草稿」→ 用 `Network.getRequestPostData` 直接讀出這次 PUT
  請求的原始 multipart body，確認 `payload` 內容是
  `{"slug":"...","categoryCode":"club","isFeatured":false,"content":{...},"expectedUpdatedAt":"...","removeCover":true}`
  **且完全沒有 `file` 這個 part**（沒有嘗試任何上傳）；回應 `coverKey` 是 `null`；查 Azurite，
  該文章原本的五個物件全部消失。
- **前端驗證仍在送出請求之前擋下**（沿用既有邏輯，逐一重測確認沒有回歸，四種情況攔截到的 API
  請求數皆為 0）：解析度不足（400×300）「圖片解析度太低（至少需要 1600×900）…」；不支援格式
  （`.bmp`）「圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。」；超過 10MB
  「圖片檔案太大（上限 10 MB）…」；**真正的 HEIC 檔案**（非改副檔名）正確轉檔成 JPEG 並顯示本機
  預覽，無錯誤訊息，同樣沒有送出任何請求。
- 測試文章事後皆用 `DELETE` 清掉，確認資料庫與 Azurite 容器都恢復乾淨（最終物件數為 0）。

**已知的其餘限制**（沿用後端 `apps/api/README.md`「已知缺口」，前端不重複列)：伺服器端沒有依
版位設定尺寸下限（本元件的 `minWidth`／`minHeight` 是前端唯一一道尺寸把關）、圖片欄位沒有
`_width`／`_height`／雙語 `_alt` 資料庫欄位（`form.coverImageUrl` 因此恆為 `null`，不是遺漏）。

**這次修正的連帶影響（回報，未動手改規格）**：`NewsListView.vue` 的「複製」功能（`handleDuplicate`）
原本會把來源文章的 `coverKey` 一併帶進新建立的草稿——`CreateArticleRequest` 已經不接受
`coverKey`（封面圖片只能透過同一次請求真的夾一個檔案上傳），複製品因此**不會**帶著原文章的封面
圖片，使用者需要自己在複製出來的草稿裡重新選一次封面圖。這是這次修正的必然結果（規劃書的圖片
欄位只認得「同一次請求上傳的位元組」，不認物件鍵字串），不是本輪刻意拿掉這個功能，畫面的成功
訊息已經跟著調整為「已建立一份複製的草稿，封面圖片未帶入，請重新選擇後再儲存」。

## 已知的 mockup 簡化（不是 bug，記錄給下一階段）

- 「內容」的富文本編輯器用 `<textarea>` 代替——規劃書明文「不代為決定」實際編輯器選型，第二階段先
  用文字框佔位，畫面上也用中文說明這一點。
- 「預覽」按鈕對已發布文章會 `window.open` 一個 `/zh/news/{網址名稱}/` 的網址，這個網址在本機開發
  環境下不保證能真的打開（`apps/web` 前台是否有跑、跑在哪個 port，跟這個 admin 專案無關）。
- `newsStore` 只存在瀏覽器分頁的記憶體裡，重新整理頁面會重置回 `data/news.ts` 的初始假資料
  （⚠️ 這一則現在只剩「假資料」場景成立——新聞編輯頁走的是真實 `apps/api`，見 S0-7i；`newsStore`
  仍然存在但只有在 `apps/api` 沒開的情境下才會被用到，這裡的措辭是既有落差，本輪未動手修正，
  一併回報）。

## 交付範圍與邊界

本階段**只做**：外殼（側欄＋頂欄＋站台切換器＋使用者選單）、儀表板、新聞與故事的列表頁與編輯頁。
**不做**：角色與權限頁、其餘 12 個模組的真實功能（都是明確的佔位頁）、任何後端串接。
