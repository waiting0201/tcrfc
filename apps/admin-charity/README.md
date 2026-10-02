# apps/admin-charity — 慈善捐款平台獨立後台（Vue 3 SPA）

> **2026-10-02（CH-3 補完／CH-4／CH-5）**：`N1`–`N7` 七個模組**全部接真 API**，不再有示範畫面。2026-10-01 接了登入與 `N1`–`N3`；2026-10-02 補上店家 CSV 匯入、項目內文編輯、徵信名單逐筆隱藏、回饋金結算、每日對帳、憑證管理、報表、站台設定與操作紀錄。見文末「CH-3 串接」與「CH-3 補完／CH-4／CH-5 畫面」。

Vue 3 + Vite + TypeScript + Element Plus 的獨立 SPA，比照 `apps/admin` 的專案結構與慣例，
但配色、版面骨架、資料完全不同——見 [`docs/22-charity-ui.md`](../../docs/22-charity-ui.md) §3
（後台版面）。**沒有 club_id、沒有站台切換器**，是台灣足球策略發展協會的獨立系統。

## 開發

```bash
npm install
npm run dev       # http://localhost:5175
npm run build     # vue-tsc -b && vite build
npm run preview   # 預覽 dist/，port 4174
```

## 假資料（⚠️ 2026-10-02 起沒有任何畫面使用，只剩同步檢查）

一律 import `fixtures/charity-fixtures.json`（相對路徑，Vite alias `@fixtures/charity-fixtures.json`）。
這份檔案是 `db/seed/charity-fixtures.json` 的**機器產生鏡射複本**，不是另外手寫的資料——
`db/seed/emit-charity-fixtures.py` 同時輸出三份內容相同的檔案（`db/seed／apps/web-charity／
apps/admin-charity` 各一份）。

> 🟡 **2026-10-02**：`N4`–`N7` 全部改接真 API 後，`src/data/fixtures.ts` 已刪除（狀態型別搬到 `src/types/invoice.ts`、`@/api/donations`、`@/api/settlements`），`fixtures/charity-fixtures.json` 已沒有畫面引用（原本手抄的 `reconciliationAudit.ts`、`session.ts` 也已刪除）。
> 保留的原因只有一個：`db/seed/emit-charity-fixtures.py` 仍會輸出並檢查這份鏡射複本，`npm run lint` 的 `lint:fixtures` 因此仍會比對。
> 要真正移除，須先請維護該腳本的人把本專案從匯出目標拿掉（與 `apps/web-charity` 的殘留檔案是同一件事）。

⛔ **不要手動編輯 `fixtures/charity-fixtures.json`**，也不要另外手寫慈善假資料。
要改資料一律改 `db/seed/generate-charity-seed-sql.py`，重跑 `db/seed/emit-charity-fixtures.py`。

**為什麼不直接指到 `../../db/seed/`**：`docker-compose.yml` 把本專案的 Docker build context
定為 `apps/admin-charity`（docs/20-cicd.md §3），`db/seed/` 在那個 build context 之外，容器內
建置時讀不到——這是本次任務用 `docker build` 實測才發現的落差，不能只靠 `npm run build` 判斷
（那個指令是在有完整 repo 的檔案系統上跑，不會露出這個問題）。詳細理由見
[`vite.config.ts`](vite.config.ts) 的註解。


## 專案結構

```
src/
  components/     共用元件（PageHeader、SemanticTag、MobileCardList、DangerConfirmDialog……）
  composables/    useBreakpoint（三斷點：mobile <768／tablet 768–1023／desktop ≥1024）
  api/            對 apps/api 的呼叫（http.ts 底層、每個模組一支）
  auth/           登入工作階段與權限判斷
  data/           nav.ts（選單與各模組檢視權限）、fixtures.ts（已無人引用，見上）
  utils/          format.ts（日期金額格式）、blocks.ts（項目說明的區塊格式）
  layouts/        AdminLayout.vue（單列頂欄＋平鋪側欄，docs/22 §3.3）
  router/         N1–N7 七個模組的路由
  styles/         charity-admin-theme.css（淺色冷中性 design tokens，docs/22 §1.6／§1.7）
  views/
    stores/       店家管理與 QR Code（含批次匯入對話框 components/StoreImportDialog.vue）
    projects/     捐款項目管理（列表、編輯、內文編輯 ProjectContentView）
    donations/    捐款紀錄（全部捐款、異常處理、每日對帳 components/ReconciliationPanel.vue）
    settlements/  回饋金結算
    invoices/     發票與收據管理
    reports/      捐款報表（五張報表共用篩選）
    settings/     站台設定（頁籤：站台文案與金額、系統信樣板、金流與發票、操作紀錄）
```

## 響應式

三斷點完整支援（2026-09-22 使用者拍板，推翻 docs/22 §6 第 1 項「桌面優先」的暫定假設，
**待補進 docs/22**）：手機側欄變 `el-drawer`、列表變卡片式（`MobileCardList.vue`）、雙語欄位變
`el-tabs`（`BilingualShortField.vue`）。

⚠️ **平板寬度（768–1023px）的表格次要欄位一律用 `v-if="isDesktop"` 整欄不渲染，不要用 CSS
`display:none` 隱藏儲存格**——`el-table` 的欄位總寬是照 `el-table-column` 的數量與
`width`/`min-width` 參數加總計算，跟某個儲存格有沒有被 CSS 藏起來無關；只用 CSS 隱藏會讓
儲存格消失但表格總寬不變，變成內部橫向捲動，且這個捲動不會反映在
`document.documentElement.scrollWidth`（頁面層級的溢出檢查測不出來，只有實際看畫面才會發現）。
這是本次任務實測踩到的坑，見各 `*ListView.vue` 的 `isDesktop` computed 與
`charity-admin-theme.css` 對應段落的註解。

## 驗收指令

```bash
npm run build                       # vue-tsc -b && vite build
npm run lint                        # fixtures 同步 + eslint + 禁用詞 + 對比度
docker build -t admin-charity-mockup -f Dockerfile .
docker run -d --name t -p 18080:8080 admin-charity-mockup
curl -I http://localhost:18080/                    # 確認 X-Robots-Tag: noindex, nofollow
curl -I http://localhost:18080/assets/<任一 .js>    # 同上，E-29 的教訓
docker rm -f t
```

## 相關文件

- [`docs/22-charity-ui.md`](../../docs/22-charity-ui.md) — 版面與視覺規格（真實來源）
- [`docs/10-charity-donation-site.md`](../../docs/10-charity-donation-site.md) — 功能規格
- [`docs/16-charity-schema.md`](../../docs/16-charity-schema.md) — 資料表定義
- [`docs/17-deployment.md`](../../docs/17-deployment.md) §5 — 慈善平台獨立性的邊界與補償措施
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) — E-29／E-30／E-31／E-32／E-33／E-34

## CH-3 串接（2026-10-01）

### 資料來源

- API 位址 `VITE_API_BASE`（預設 `http://127.0.0.1:5299`；🔴 這是 **build 階段**內嵌的環境變數，`docker-compose.yml` 對這個服務給的是執行期 `environment`，**Vite 不會讀到**——部署時需改成 build arg，見下方缺口）。
  路徑一律 `/api/v1/donation-platform/admin/…`，程式在 `src/api/`：`http.ts`（fetch 封裝、401 自動換權杖重試）、`auth.ts`、`stores.ts`、`projects.ts`、`donations.ts`。
- **登入**（`src/auth/session.ts`）：存取權杖只放記憶體；更新權杖是後端設的 HttpOnly Cookie `__Host-tcrfc-charity-admin-rt`（前端讀不到，`credentials: 'include'`）。
  重新整理頁面時路由守衛先靜默換一次權杖並載入 `/me`。**不共用 apps/admin 的程式碼與 Cookie**。兩階段驗證是選用的：帳號啟用後登入頁會多出驗證碼欄位。
- 權限碼（`/me` 的 `permissions`）只用來顯示或隱藏按鈕，介面不顯示；真正授權由後端判斷（403 訊息直接顯示後端的日常中文）。
- 時間：API 一律 UTC，畫面以台灣時間（UTC+8）顯示（`src/utils/format.ts` 的 `formatTaipei`）。

### 範圍（2026-10-01 的內容；2026-10-02 補完的部分見下一節）

| 模組 | 狀態 |
|---|---|
| 登入、登出、個人檔案 | 真 API（不含改密碼與兩階段驗證設定畫面；API 已提供，畫面尚未做） |
| 店家 | 列表（關鍵字、狀態、分頁）、新增、編輯、Logo、重產網址名稱（二次確認）、QR 預覽與 PNG／SVG 下載、全部合作中店家 QR 打包 zip。**印刷版 PDF**：後端回 501，按鈕停用並明示「尚未提供」 |
| 項目 | 列表、新增、編輯、封面、上下架、撥付對象與慈善計畫（來自 `charity-refs` 唯讀複本） |
| 捐款紀錄 | 列表（日期、狀態、項目、店家、憑證、單號、金額範圍）、詳情側拉（個資預設遮罩；`reveal` 需權限並記稽核）、退款（原因必填）、重新確認付款、重寄感謝信、補開發票、含個資匯出（用途備註必填）、異常處理四類佇列與徽章 |

### 規格疑點與缺口（2026-10-01）

1. **分潤獨立授權**：店家／項目的分潤比例需要額外權限，沒有時畫面唯讀並提示，送出時省略該欄（後端視為不變）。
2. **店家分潤＋項目分潤 ≤ 100%** 只由後端儲存時驗證（比對合作中店家／所有項目的最高值），前台不再模擬試算；錯誤訊息直接顯示。
3. `docker-compose.yml` 的 `VITE_API_BASE` 是執行期環境變數，對 Vite 靜態建置無效；Dockerfile 需改成 `ARG VITE_API_BASE` 才會生效（屬部署層，本輪未動）。
4. 本機 api 的 CORS 開發預設清單沒有 `http://localhost:5175`，要在 `CORS_ALLOWED_ORIGINS` 加上；且 Cookie 為 `SameSite=None; Secure`，本機需用 `localhost` 連線。

## CH-3 補完／CH-4／CH-5 畫面（2026-10-02）

契約全文見 [`apps/api/README.md`](../api/README.md)「慈善 CH-3 補完／CH-4 帳務與報表／CH-5 延伸」。選單項目依各模組「檢視」權限碼顯示（`src/data/nav.ts` 的 `anyOf`），登入後落在第一個看得到的模組；按鈕依各端點的權限碼顯示或隱藏，權限碼本身不出現在介面上。

| 畫面 | 內容 | 要點 |
|---|---|---|
| 店家：批次匯入 | 下載範本、上傳 CSV、「略過重複的店家」選項、成功筆數 | 任一列有錯**整批不寫入**，後端回 400 與全部錯誤列；畫面一次列出所有「第幾列＋原因」（列號與 Excel 一致，表頭是第 1 列），不是一次只報一個。前端先擋 1 MB 以上的檔案 |
| 項目：編輯內文 | 一句話介紹、說明（區塊編輯器）、善款用途，中英各一份；列表與編輯頁都有入口 | 呼叫 `PUT /projects/{id}/content`，**只把有改動的欄位放進請求**（省略＝不變）；清空文字欄位送空字串、清空說明送空物件。區塊編輯器支援段落／小標題／引言／清單，**不認得的版型原樣保留**。有未儲存修改時離開頁面會確認。項目編輯頁改為不再重送這三項 |
| 捐款紀錄：徵信名單 | 列表「徵信名單」欄、詳情開關 | 只有「具名」捐款才有開關（匿名的顯示「不會列入」）；呼叫 `credit-visibility` |
| 捐款紀錄：每日對帳 | 第三個頁籤。批次列表、差異明細抽屜、立即對帳、標記已處理 | 差異三類用符號＋文字；**取不到金流明細（503）**會說明原因與系統做了什麼（記成「未能完成」批次、不誤判差異、不蓋掉既有結果）。差異不能刪除，標記已處理必填備註 |
| 捐款紀錄：異常處理 → 對帳差異 | 沿用既有頁籤，差異類型改用同一個標示元件，加上「標記已處理」 | 與每日對帳頁籤共用同一個後端端點 |
| 回饋金結算 | 列表篩選（狀態、對象類型、期間）、產生結算單（可指定對象；結果列出建立與略過的原因）、詳情（逐筆明細＋**退款沖回負項與原因**）、重新計算、確認結算、登記已付款（日期／方式／備註）、刪除草稿、匯出對帳單 | 依狀態只顯示可做的動作；登記已付款是獨立授權；負數的應付金額會警示 |
| 發票與收據 | 列表篩選、匯出、手動補登號碼、作廢、折讓、重新開立 | 列表與匯出不含捐款人個資；**跨期作廢（409）**把後端說明原文顯示並提示改用折讓 |
| 捐款報表 | 總覽（依日／依月）、依店家、依項目、發票與收據狀況、逐筆明細；五張共用篩選；CSV 匯出 | 匯出鈕只在持有匯出授權時顯示（能看不等於能匯出）；手機寬度改卡片 |
| 站台設定 | 頁籤：站台文案與金額（中英文案、俱樂部官網網址、金額預設範圍、徵信名單整站開關）、系統信樣板、金流與發票、操作紀錄 | 文案儲存是**局部更新**（只送有改的）；系統信可在游標處插入代入欄位，英文版主旨與本文要同時填或同時空；**金流與發票憑證只寫不讀**，輸入框是密碼型、送出後清掉、畫面只顯示「已設定」；**切到正式環境要二次確認（輸入確認詞）**，正式環境未設憑證時顯示後端 409 原因；操作紀錄僅系統管理員，只讀 |

### 本輪待決（規劃書沒寫又影響客戶可見行為，未自行發明）

1. **「圖文」區塊**：規劃書 §3.2 說說明內文「支援圖文、引言、清單」，但後端的區塊格式沒有圖片區塊、前台也沒有渲染，所以編輯器目前只有段落／小標題／引言／清單，**沒有插入圖片**。
2. **系統信樣板的代入欄位**：`docs/22` §3.7.7 要求顯示成人類可讀的 `{{訂單編號}}`，後端實際的欄位標記是程式用的字面值。畫面做法：按鈕只顯示中文說明、按下去插入實際標記，所以信件本文裡仍看得到那個標記字樣。
3. **淨額為負的結算單**（後端待決②）：畫面只警示、允許照常登記付款，是否改成遞延到下期待決。
4. **匯款方式**：自由文字（上限 32 字），規劃書只寫「日期、方式與備註」，沒有固定選項。
5. **誰持有結算與登記付款的權限**（後端待決①）：種子只有系統管理員持有；要讓「核對的人」和「登記付款的人」是不同人，需客戶決定指派的角色。
6. **操作紀錄放在站台設定的頁籤、每日對帳放在捐款紀錄的頁籤**：照 `docs/22` §3.8／§3.9，沿用該檔「待客戶確認放法」的標記。
7. **憑證列表的「重新開立」**：沿用捐款詳情的同一支端點，規劃書 §6.5 沒明寫放在哪。
8. 印刷版 QR PDF 仍回 501（缺協會標誌與字型），行為不變。

## 本輪驗證（2026-10-02）

`npm run typecheck`、`npm run lint`（fixtures 同步＋eslint＋禁用詞＋對比度）、`npm run build` 全部通過。

### 畫面驗證與實機驗收

2026-10-02 的畫面以 scratchpad 的**假後端**驗證（依 `apps/api/README.md` 的契約回應形狀的 Node 小伺服器，只放在 agent 的暫存目錄、不進版控；用 Chrome 開發者協定驅動真瀏覽器，覆蓋 1440／820／390 三種寬度）：
店家匯入的整批錯誤與略過重複、內文只送有改的欄位（含未知版型保留、清空語意）、徵信名單切換、對帳 503 說明與標記已處理、結算草稿到已付款全流程與負項、憑證補登／跨期作廢 409／折讓、報表五張與匯出、站台設定局部更新、系統信游標插入、金流二次確認與憑證不外露、客服／檢視者的按鈕與選單隱藏、整頁無橫向溢位與表格無內部溢位。

🔵 **實機驗收未做**：沒有對真實的 `apps/api` 與慈善資料庫跑過（agent 不啟動 API、不連真實資料庫、不碰密碼）。待使用者啟動 API 並合併後，依下列步驟實走一次。

### 實機驗收步驟（等使用者啟動 API 並合併後）

1. 使用者啟動 `apps/api`（port 5299，設定慈善連線字串與 JWT 金鑰，CORS 含 `http://localhost:5175`），並已重灌慈善種子（`./db/seed/apply-charity-seed.sh`，種子帳號見 api README）。agent 不啟動 API、不碰密碼（E-56）。
2. `VITE_API_BASE=http://localhost:5299 npm run dev`，開 `http://localhost:5175`：以種子系統管理員登入；重新整理仍保持登入；登出後回登入頁。
3. 店家：新增店家 → 上傳 Logo → 產生 QR → 重產網址（舊 QR 失效）。**批次匯入**：下載範本直接上傳（成功 1 家）→ 同一份再傳一次（應整批被擋，列出「與既有店家重複」）→ 勾「略過重複」再傳（成功 0 家、略過 1 列）→ 故意改壞兩列（空店名、分潤填字母）確認一次列出兩個錯誤列。
4. 項目：新增項目（未上架）→ 設封面 → 上架／下架；故意讓店家＋項目分潤超過 100% 應看到後端錯誤訊息。**編輯內文**：只改中文一句話介紹 → 儲存 → 到前台項目頁確認只有那一欄變；清空善款用途 → 前台該區塊消失；在說明加一個清單再存。用「商務／贊助」種子帳號登入，確認也能存內文（沒有分潤權限）。
5. 捐款紀錄：用前台完成幾筆捐款（見 web-charity README）→ 列表與篩選 → 詳情（個資遮罩 → 顯示完整個資）→ **把一筆具名捐款設為不列入徵信名單**，到前台徵信名單頁確認該姓名消失、恢復後又出現 → 退款（客服角色應沒有退款鈕）→ 匯出（需用途備註）→ 異常處理分頁與頂欄徽章。
6. **每日對帳**：到「每日對帳」頁籤按「立即對帳」。本機假金流來源只在 Development 運作，應回成功且差異 0；若環境沒有啟用假來源會看到「取不到金流明細」的說明（預期行為）。要看差異畫面可在庫裡造一筆金流端沒有的 `paid` 捐款後重跑。
7. **結算**：在有已完成捐款的期間（結束日早於今天）產生結算單 → 看明細 → 重新計算 → 確認結算 → 用有權限的帳號登記已付款 → 匯出對帳單。對一筆已付款結算單內的捐款做退款，再產生下一期結算單，確認出現**負項沖回與原因**。
8. **憑證**：補登號碼、作廢（當期）、對跨期憑證作廢應看到 409 說明並改用折讓、匯出。
9. **報表**：五張報表與篩選、依月／依日；用沒有匯出授權的帳號確認沒有匯出鈕。
10. **站台設定**：改首頁說明與徵信開關（前台頁尾入口隨之出現／消失）；改系統信樣板；以系統管理員看「金流與發票」，更換測試環境憑證、嘗試切正式（沒有正式憑證應被擋）；「操作紀錄」應看到以上操作。
11. 用「檢視者」種子帳號登入，確認按鈕依權限隱藏、403 訊息為日常中文。
12. `curl -I` 確認 `X-Robots-Tag`（見上方驗收指令的 docker 步驟）。
