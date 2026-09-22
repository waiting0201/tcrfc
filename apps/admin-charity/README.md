# apps/admin-charity — 慈善捐款平台獨立後台（Vue 3 SPA，可點 mockup）

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

## 假資料

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

## 假身分切換（mockup 用）

⛔ 沒有真的認證（`admin_users.password_hash` 是明顯的測試占位字串，後端認證尚未實作）。
右上角使用者選單可以切換成種子資料裡任一個測試帳號，全站畫面依所選身分的角色權限即時反應
（例如退款按鈕只有有權限的身分看得到）——這是為了讓「畫面依權限而變」這件事能被實際點著看
而做的展示設計，不是規劃書規格本身。

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
