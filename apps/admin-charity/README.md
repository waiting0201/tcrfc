# apps/admin-charity — 慈善捐款平台獨立後台（Vue 3 SPA）

> **2026-10-01（CH-3）**：登入與 N1 店家／N2 項目／N3 捐款紀錄已接真 API；N4–N7 仍是示範畫面（後端未提供）。見文末「CH-3 串接」。

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

## 假資料（⚠️ 現在只剩 N4–N7 示範畫面使用）

一律 import `fixtures/charity-fixtures.json`（相對路徑，Vite alias `@fixtures/charity-fixtures.json`）。
這份檔案是 `db/seed/charity-fixtures.json` 的**機器產生鏡射複本**，不是另外手寫的資料——
`db/seed/emit-charity-fixtures.py` 同時輸出三份內容相同的檔案（`db/seed／apps/web-charity／
apps/admin-charity` 各一份）。

⛔ **不要手動編輯 `fixtures/charity-fixtures.json`**，也不要另外手寫慈善假資料。
要改資料一律改 `db/seed/generate-charity-seed-sql.py`，重跑 `db/seed/emit-charity-fixtures.py`。

**為什麼不直接指到 `../../db/seed/`**：`docker-compose.yml` 把本專案的 Docker build context
定為 `apps/admin-charity`（docs/20-cicd.md §3），`db/seed/` 在那個 build context 之外，容器內
建置時讀不到——這是本次任務用 `docker build` 實測才發現的落差，不能只靠 `npm run build` 判斷
（那個指令是在有完整 repo 的檔案系統上跑，不會露出這個問題）。詳細理由見
[`src/data/fixtures.ts`](src/data/fixtures.ts) 檔頭與 [`vite.config.ts`](vite.config.ts) 的註解。

🔴 **已知落差**：`db/seed/generate-charity-seed-sql.py` 的對帳（`reconciliation_runs`／
`reconciliation_discrepancies`）與稽核紀錄（`audit_logs`）資料是直接寫成 SQL 字面值，沒有存成
模組層級常數，`emit-charity-fixtures.py` 因此匯不出這兩類資料。
[`src/data/reconciliationAudit.ts`](src/data/reconciliationAudit.ts) 逐字轉錄了種子腳本裡的
實際數字作為過渡資料，檔頭有完整說明；正式修法是把種子腳本那兩段也改成先存常數再迴圈
`INSERT`，這樣匯出腳本才能一併涵蓋，但那是 `db/` 底下的檔案，不在本次「只改
`apps/admin-charity/`」的範圍內。

## 專案結構

```
src/
  components/     共用元件（PageHeader、SemanticTag、MobileCardList、DangerConfirmDialog……）
  composables/    useBreakpoint（三斷點：mobile <768／tablet 768–1023／desktop ≥1024）
  data/           fixtures.ts（假資料的單一入口）、session.ts（mockup 登入身分切換）、nav.ts
  layouts/        AdminLayout.vue（單列頂欄＋平鋪側欄，docs/22 §3.3）
  router/         N1–N7 七個模組的路由
  styles/         charity-admin-theme.css（淺色冷中性 design tokens，docs/22 §1.6／§1.7）
  views/
    stores/       N1 店家管理與 QR Code
    projects/     N2 捐款項目管理
    donations/    N3 捐款紀錄（含異常佇列、對帳與異常佇列）
    settlements/  N4 回饋金結算
    invoices/     N5 發票與收據管理
    reports/      N6 捐款報表
    settings/     N7 站台設定（含稽核紀錄查詢，docs/22 §3.9）
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

### 範圍

| 模組 | 狀態 |
|---|---|
| 登入、登出、個人檔案 | 真 API（不含改密碼與兩階段驗證設定畫面；API 已提供，畫面尚未做） |
| N1 店家 | 列表（關鍵字、狀態、分頁）、新增、編輯、Logo、重產網址名稱（二次確認）、QR 預覽與 PNG／SVG 下載、全部合作中店家 QR 打包 zip。**印刷版 PDF**：後端回 501，按鈕停用並明示「尚未提供」。店家 CSV 批次匯入（規劃書 §6.1）後端也沒有，未做 |
| N2 項目 | 列表、新增、編輯、封面、上下架、撥付對象與慈善計畫（來自 `charity-refs` 唯讀複本）。**說明內文（區塊編輯器）本畫面不提供編輯**，儲存時原樣送回避免被整筆取代清空 |
| N3 捐款紀錄 | 列表（日期、狀態、項目、店家、憑證、單號、金額範圍）、詳情側拉（個資預設遮罩；`reveal` 需權限並記稽核）、退款（原因必填）、重新確認付款、重寄感謝信、補開發票、含個資匯出（用途備註必填）、異常處理四類佇列與徽章 |
| N4 回饋金結算、N5 發票管理、N6 報表、N7 站台設定 | **示範畫面**（仍讀假資料），頁首固定顯示「這是示範畫面」提示；後端屬 CH-4／CH-5 |

### 規格疑點與缺口

1. **分潤獨立授權**：店家／項目的分潤比例需要額外權限，沒有時畫面唯讀並提示，送出時省略該欄（後端視為不變）。
2. **店家分潤＋項目分潤 ≤ 100%** 只由後端儲存時驗證（比對合作中店家／所有項目的最高值），前台不再模擬試算；錯誤訊息直接顯示。
3. **對帳差異**目前只能查看，標記處理與對帳排程沒有 API。**稽核紀錄查詢**沒有 API（舊 N7 示範畫面仍是假資料）。
4. API 沒有「單一店家／項目」以外的批次操作與店家 CSV 匯入；詳情沒有編輯捐款人資料的端點（符合規劃書）。
5. `docker-compose.yml` 的 `VITE_API_BASE` 是執行期環境變數，對 Vite 靜態建置無效；Dockerfile 需改成 `ARG VITE_API_BASE` 才會生效（屬部署層，本輪未動）。
6. 本機 api 的 CORS 開發預設清單沒有 `http://localhost:5175`，要在 `CORS_ALLOWED_ORIGINS` 加上；且 Cookie 為 `SameSite=None; Secure`，本機需用 `localhost` 連線。

### 驗收步驟（等使用者啟動 API 並合併後）

1. 使用者啟動 `apps/api`（port 5299，設定慈善連線字串與 JWT 金鑰，CORS 含 `http://localhost:5175`），並已重灌慈善種子（`./db/seed/apply-charity-seed.sh`，種子帳號見 api README）。agent 不啟動 API、不碰密碼（E-56）。
2. `VITE_API_BASE=http://localhost:5299 npm run dev`，開 `http://localhost:5175`：以種子系統管理員登入；重新整理仍保持登入；登出後回登入頁。
3. N1：新增店家 → 上傳 Logo → 產生 QR（預覽與 PNG／SVG 下載，zip 打包）→ 重產網址（舊 QR 失效）。印刷版 PDF 顯示「尚未提供」。
4. N2：新增項目（未上架）→ 設封面 → 上架／下架；故意讓店家＋項目分潤超過 100% 應看到後端錯誤訊息。
5. N3：用前台完成幾筆捐款（見 web-charity README）→ 列表與篩選 → 詳情（個資遮罩 → 顯示完整個資）→ 退款（客服角色應沒有退款鈕）→ 匯出（需用途備註）→ 異常處理分頁與頂欄徽章。
6. 用「檢視者」種子帳號登入，確認按鈕依權限隱藏、403 訊息為日常中文。
7. `npm run lint && npm run build` 全過；`curl -I` 確認 `X-Robots-Tag`（見驗收指令）。
