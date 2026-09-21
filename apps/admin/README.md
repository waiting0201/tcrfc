# apps/admin — admin-web（官網共用後台）

台中磐石官網主站與台中藍鯨官網**共用同一個後台**的 Vue 3 SPA。

🟡 **目前是可以在瀏覽器裡點的 mockup（S0-12），不是接上真實資料庫的產品**：外殼＋儀表板＋
「新聞與故事」的列表頁與編輯頁是真的可以互動的畫面，其餘 13 個模組是明確的「尚未建置」佔位頁
（不是死連結）。所有資料都是寫死在前端的假資料，**刻意不接 `apps/api`**——那支 API 目前唯讀、
只回已發布內容，接了反而驗證不到草稿／排程發布／已停用這三種非公開狀態（見下方「假資料」）。

規格的真實來源是規劃書與 [`docs/03-admin-spec.md`](../../docs/03-admin-spec.md)；版面與視覺規則是
[`docs/21-admin-ui.md`](../../docs/21-admin-ui.md)（535+ 行，第一階段 `visual-design-architect`
產出，本次實作依它逐條核對）。

---

## 怎麼跑

```bash
cd apps/admin
npm install
npm run dev        # http://localhost:5174，熱重載
npm run build      # 型別檢查（vue-tsc）＋ Vite production build，輸出到 dist/
npm run preview    # 起一個本機 server 服務 dist/，用來驗收 build 產物
npm run lint       # ESLint ＋ 禁用詞掃描（見下方「檢查腳本」），兩者都要過
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
│   ├── main.ts              # 掛載點，全域註冊 Element Plus 與圖示
│   ├── App.vue               # 只有一個 <router-view />
│   ├── router/index.ts       # 路由表：已實作的頁面 + 從 data/nav.ts 自動展開的佔位頁路由
│   ├── layouts/AdminLayout.vue   # 外殼：側欄／頂欄／響應式斷點切換（桌面／平板／手機）
│   ├── components/
│   │   ├── AppSidebar.vue        # 側欄選單（6 組視覺分組、el-sub-menu 手風琴）
│   │   ├── AppTopbar.vue         # 頂欄：收合/漢堡按鈕、站台切換器、通知、使用者選單
│   │   ├── SiteSwitcher.vue      # 站台切換器（docs/21 §5：只換徽章，不換整套主色）
│   │   ├── UserMenu.vue
│   │   ├── FrontendUnitBanner.vue # 「這裡管理的是：{前台單元} ↗」（規劃書 §4.0 硬性規定）
│   │   ├── StatusTag.vue         # 草稿／排程發布／已發布／已停用 四態 el-tag
│   │   ├── ImageUploader.vue     # 選檔只預覽、按儲存才「上傳」的圖片元件
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
│   └── styles/admin-theme.css # 後台操作型 design tokens（不是品牌色，見 docs/21 §7）
├── scripts/check-forbidden-terms.mjs  # 禁用詞掃描（見下方）
├── Dockerfile / nginx-spa.conf / .dockerignore  # 既有檔案，本次只修正 nginx-spa.conf 一處 bug
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

## 響應式：三個斷點都手動驗證過

`docs/21-admin-ui.md` §8（使用者 2026-09-21 拍板：後台要完整響應式）。用無頭 Chrome + CDP
（`Emulation.setDeviceMetricsOverride`，不是 `--window-size` 命令列參數——後者在無頭模式下有實測過
的視窗floor問題，見專案外的 agent 除錯筆記）逐一驗證過：

- **桌面 `≥1024px`**：側欄常駐可收合（240px/64px，收合偏好存 `localStorage`）、表格完整欄位。
- **平板 `768–1024px`**：側欄強制收合但可手動展開、表格隱藏次要欄位（分類／發布時間）。
- **手機 `<768px`**：側欄變 `el-drawer`（漢堡按鈕開關，導覽後自動關閉）、列表頁表格換成卡片式
  （每筆一張 `el-card`，只留封面／標題／分類／狀態／操作）、編輯頁雙語欄位（含長欄位）一律變成
  `el-tabs`、底部操作列的按鈕改滿版寬度堆疊。

## 已知的 mockup 簡化（不是 bug，記錄給下一階段）

- 「內容」的富文本編輯器用 `<textarea>` 代替——規劃書明文「不代為決定」實際編輯器選型，第二階段先
  用文字框佔位，畫面上也用中文說明這一點。
- 圖片上傳沒有真的上傳到任何地方：按「儲存」時用 `URL.createObjectURL()` 產生的本機預覽網址取代
  「伺服器回傳的正式圖片網址」，行為上模擬了「選檔只預覽、儲存才送出」，但沒有真的網路請求。
- 「預覽」按鈕對已發布文章會 `window.open` 一個 `/zh/news/{網址名稱}/` 的網址，這個網址在本機開發
  環境下不保證能真的打開（`apps/web` 前台是否有跑、跑在哪個 port，跟這個 admin 專案無關）。
- `newsStore` 只存在瀏覽器分頁的記憶體裡，重新整理頁面會重置回 `data/news.ts` 的初始假資料。

## 交付範圍與邊界

本階段**只做**：外殼（側欄＋頂欄＋站台切換器＋使用者選單）、儀表板、新聞與故事的列表頁與編輯頁。
**不做**：角色與權限頁、其餘 12 個模組的真實功能（都是明確的佔位頁）、任何後端串接。
