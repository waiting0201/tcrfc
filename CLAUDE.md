# CLAUDE.md — TCRFC 官方網站專案

台中磐石足球俱樂部（Taichung Cornerstone RFC，**TCRFC**）官方網站建置專案。
品牌主張：**LOCAL ROOTS. GLOBAL PATHWAYS.｜在地扎根 · 放眼世界**

> **本檔是索引，不是規格書。** 規格的唯一真實來源是 [`output/TCRFC_前後台功能規劃書.md`](output/TCRFC_前後台功能規劃書.md)（v1.8，1138 行）。
> `docs/` 是為了讓 AI 與新進人員快速上手，從規劃書拆解出來的**導航層與執行規範**。
> **兩者衝突時，一律以規劃書為準**，並回頭修正 `docs/`。

---

## 一分鐘現況

| 項目 | 狀態 |
|---|---|
| 功能規劃 | ✅ 完成（規劃書 v1.8，中英雙版） |
| 視覺方向 | ✅ 單頁 mockup 已完成並部署（Cloudflare Pages 專案 `tcrfc-mockup`，全站 `noindex`） |
| 品牌資產 | ✅ [`brand/`](brand/) 已由 logo 主檔萃取完成；design tokens 已校正為 `.ai` 品牌色 |
| 技術選型 | ❌ **未定案**（規劃書第 10 節明確排除在範圍外） |
| 網站本體 | ❌ 尚未開發 |
| 內容 | 🔄 **收件中** — 客戶依 [`TCRFC_資料收件夾/`](TCRFC_資料收件夾/) 的結構放入既有檔案 |
| 版本控制 | ❌ 本目錄**不是 git repo**。覆寫或刪除檔案前務必先看過內容 |

**目前的主線工作**：等客戶把既有素材放進收件夾 → 盤點 → 改寫成網頁文案 → 套入網站。
流程見 [`docs/07-content-pipeline.md`](docs/07-content-pipeline.md)。

---

## 目錄地圖

| 路徑 | 內容 | 性質 |
|---|---|---|
| [`output/`](output/) | 規劃書、開發里程碑（中英 × md/html/pdf） | **交付物，唯一真實來源** |
| [`docs/`](docs/) | 從規劃書拆解的工作文件 | 導航層（本專案自用） |
| [`mockup/`](mockup/) | 單頁視覺 mockup（`index.html` 1330 行，內含 design tokens）與示意圖 | 視覺基準 |
| [`brand/`](brand/) | 由 `.ai` 萃取的 SVG 標誌、favicon／PWA icon、OG 圖，說明見 [`brand/README.md`](brand/README.md) | **品牌資產庫** |
| [`reference/`](reference/) | 品牌簡報 pptx、sitemap 圖、Logo 主檔 `TCR_logo_CMYK.ai`、參考網站截圖 | 客戶提供素材 |
| [`TCRFC_資料收件夾/`](TCRFC_資料收件夾/) | 給客戶放既有檔案的分類結構（83 個資料夾，對應 13 單元） | 內容收件 |
| `.wrangler/` | Cloudflare Pages 部署快取 | 工具產生，勿手動改 |

---

## 文件索引

| 文件 | 什麼時候讀 |
|---|---|
| [`docs/00-harness.md`](docs/00-harness.md) | **每個 session 先讀這份**。文件如何分工、任務對應該讀哪一段（含規劃書行號對照） |
| [`docs/01-site-architecture.md`](docs/01-site-architecture.md) | 需要知道網站有哪些頁、層級怎麼分、URL 怎麼定 |
| [`docs/02-frontend-spec.md`](docs/02-frontend-spec.md) | 要做前台任一頁面／區塊 |
| [`docs/03-admin-spec.md`](docs/03-admin-spec.md) | 要做後台模組或處理權限 |
| [`docs/04-data-model.md`](docs/04-data-model.md) | 要設計資料表、內容型別、匯入格式 |
| [`docs/05-i18n-seo.md`](docs/05-i18n-seo.md) | 處理雙語、SEO/GEO、效能與無障礙 |
| [`docs/06-conventions.md`](docs/06-conventions.md) | 命名、術語、色彩字級、日期與檔名格式 |
| [`docs/07-content-pipeline.md`](docs/07-content-pipeline.md) | 處理客戶交來的素材 |
| [`docs/08-roadmap-decisions.md`](docs/08-roadmap-decisions.md) | 排程、已定案前提、待確認事項 |

---

## 關鍵事實速查

- **隊別代號**：`D1`（＝ First Team 一線隊，全站僅一支）／`U15`／`U14`／`U12`。
  `D1` 是代號，對外顯示一律寫 `First Team / 一線隊`。女足**不建立**球隊資料。
- **品牌色**：桃紅 `#E0218A`（PANTONE 225 C／C5 M90 Y0 K0）＋ 品牌黑 `#231916`（K100，**暖調近黑，不是深藍**）。
  唯一來源是 [`reference/TCR_logo_CMYK.ai`](reference/TCR_logo_CMYK.ai)。小字情境改用 AA 安全版 `#D61E83`。
- **雙語**：繁中（預設）＋英文，URL 以 `/zh/`、`/en/` 區隔，架構須預留第三語系。
- **不做的事**：站內電商、金流、購物車、訂單、庫存（全數導向 Shopify）；票務；女足名單與賽程（導向女足官網）。
- **賽事資料全部人工維護**，不串接外部 API，提供 CSV 批次匯入。
- **既有數位資產**：官網 www.tcrfc.tw（待遷移）、IG `@tcr_fc_2024`、FB `TCRFC2024`、YouTube `@TCRFC-2024`。
- **成立年份 2024**，2024 全國乙級聯賽冠軍。

---

## 工作守則

1. **不要整份讀規劃書。** 1138 行會吃掉大量 context。先查 [`docs/00-harness.md`](docs/00-harness.md) 的行號對照表，只讀需要的章節。
2. **規劃書是唯一真實來源。** `docs/` 只做導航與濃縮，不得引入規劃書沒有的新規格；若發現需要新增規格，先改規劃書再同步 `docs/`。
3. **`D1` 的雙重身分要小心。** 後台課程模組原編號 `D1–D4` 已因撞名改為 `P1–P4`，看到舊文件寫 `D1 課程` 一律視為錯誤。
4. **所有前台可見的內容型別都要有 `zh` / `en` 雙欄位**，英文可空但欄位必須存在。
5. **mockup 的 `noindex` 不要拿掉**（[`mockup/_headers`](mockup/_headers)），正式站上線前它不該被索引。
6. **本目錄沒有版本控制。** 大範圍修改或刪除前先確認，必要時建議先 `git init`。
7. **客戶素材涉及個資與肖像權**（未成年學員照片、會員資料）。處理收件夾內容時不要外傳、不要放進會被公開的檔案。
