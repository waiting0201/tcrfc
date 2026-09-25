# CLAUDE.md — TCRFC 官方網站專案

台中磐石足球俱樂部（Taichung Rock FC，**TCRFC**）官方網站建置專案。
品牌主張：**LOCAL ROOTS. GLOBAL PATHWAYS.｜在地扎根 · 放眼世界**

> **本檔是索引，不是規格書。** 規格的真實來源是**四份規劃書**：
> [`output/TCRFC_前後台功能規劃書.md`](output/TCRFC_前後台功能規劃書.md)（**v3.14**，官網主站，含站內商店，**多俱樂部架構的定義處**）、
> [`output/TCRFC_台中藍鯨官網功能規劃書.md`](output/TCRFC_台中藍鯨官網功能規劃書.md)（**v1.8**，藍鯨官網，**只寫與主站的差異**）、
> [`output/TCRFC_慈善捐款平台功能規劃書.md`](output/TCRFC_慈善捐款平台功能規劃書.md)（**v2.5，806 行**，協會主辦的獨立網域掃碼捐款平台，**v2.0 起為獨立後台與獨立資料庫**）與
> [`output/TCRFC_行動App功能規劃書.md`](output/TCRFC_行動App功能規劃書.md)（**v3.13**，iOS／Android App，台中磐石 × 台中藍鯨雙隊共同平台，收款主體是俱樂部）。
> **四份的主從關係**：主站規劃書是上游，藍鯨規劃書只寫差異，衝突時以主站為準。
> [`docs/`](docs/) 是為了讓 AI 與新進人員快速上手，從規劃書拆解出來的**導航層與執行規範**。
> **兩者衝突時，一律以規劃書為準**，並回頭修正 `docs/`。
>
> **本檔只放三件事**：一分鐘現況、目錄與文件索引、全域規定。
> 事實與踩雷點在 [`docs/14-invariants.md`](docs/14-invariants.md)，非交付物的記錄在 [`docs/15-out-of-scope-record.md`](docs/15-out-of-scope-record.md)，
> **做錯過的事在 [`docs/18-work-errors.md`](docs/18-work-errors.md)**，工作流程在 [`docs/00-harness.md`](docs/00-harness.md)。

---

## 一分鐘現況

| 項目 | 狀態 |
|---|---|
| 功能規劃 | ✅ 完成（**主站 v3.14／藍鯨 v1.8／慈善 v2.5／App v3.13**，中英雙版與 PDF 均已同步）。**四份規劃書只描述現行規格，不標注曾經移除的內容**（見工作守則 3）。**v3.0 把系統改為多俱樂部架構**：台中藍鯨女足納入為第二個俱樂部，新增藍鯨官網、`Club`／`Competition`／`Membership` 型別、`club_id` 維度、`J` 資料範圍權限、雙會籍與每份會籍一張卡；慈善平台拆為獨立後台與獨立資料庫 |
| **多俱樂部架構** | 📄 **規格已完成**（主站規劃書 §1.3／§5.3／§5.4／§6，藍鯨規劃書）。**一個後台入口、資料以 `club_id` 分開**；**50 張必填 `club_id`、9 張可為空（＝兩隊共同）、43 張不加**（主站 §5.4，v3.10 補齊）。**尚未開發**。**規劃書只寫系統怎麼做，法人與授權事項已移出**（見下方「行政與法務事項」）。擋開發的是：藍鯨網域、**品牌資產向量主檔**（隊徽點陣主檔已於 2026-09-14 取得）、球員名單與肖像同意、12 個月賽程 |
| **台中藍鯨官網** | 📄 **規格已完成**（獨立規劃書 v1.7，中英雙版）。**獨立網域、獨立前台專案、中英雙語，共用主站後台與資料庫**。**v1.5 定為「與主站同一套網站，只有配色不同」**——版型、頁面結構與功能一律比照主站，例外只有四項單元取捨（不設 06 與 11、04 為青年隊、09 分區）。**尚未開發**；網域、**品牌資產向量主檔**（隊徽點陣主檔已取得）、球員名單與肖像同意、12 個月賽程未到位。⚠️ **藍鯨由磐石的團隊在營運**，這些資料在自己手上，是盤點問題不是外部依賴 |
| **行動 App** | 📄 **規格已完成**（獨立規劃書 v3.13，1801 行）。**會籍是一人每俱樂部一份、每份會籍一張卡**；`Club`／`Competition` 定義於主站、俱樂部授權在主站 `J4`。**§16.2 阻塞級 9 項**（法務與會計事項已移出）。**客戶端技術選型已定案（原生 Swift ＋ Kotlin，見 [`docs/19`](docs/19-app-tech-stack.md)）；首個上架版本不含 App 內付款**，IAP 判定與 LINE Pay 商店號因此不擋首版。擋開發的是藍鯨品牌資產（**隊徽點陣主檔已取得，向量仍缺**）、賽程、名單、網域 DNS 控制權、店家座標。**開發者帳號已到位**（主體為俱樂部，D-U-N-S、Apple 與 Google Play 法人帳號均已完成，Team ID 與 package name 在手） |
| 慈善捐款平台 | 📄 **規格已完成，v2.0 改為獨立後台與獨立資料庫**（規劃書 v2.4，中英雙版）。**主辦與收款主體是台灣足球策略發展協會，不是磐石**。**功能範圍一項未改**，改的是架構——`N` 模組與 8 張捐款表移出主站，自建約 22 張表。**法遵風險因此下降**（協會自己控管自己蒐集的資料）。**尚未開發；網域、協會品牌資產、法人登記與統編、獨立後台的維運人力未定** |
| **站內商店** | 📄 **規格已完成**（主站規劃書 §8.3、後台 `S1–S6`）。收款主體是俱樂部，**付款只用 LINE Pay、必開電子發票**。**v3.0 加分帳欄位**（`selling_club_id`／`collecting_club_id`），**購物車不得跨俱樂部混買**。**前台已有 7 頁流程骨架**，但沒有後端、沒有金流、沒有庫存。⚠️ **「訂單是否於結帳時依俱樂部拆單」尚未定案**（現行禁止混買故不會發生，但開放混買前必須先答） |
| **後台設計** | ✅ **通則已定**（主站規劃書 §4.0，v3.7；圖片上傳通則 v3.9）。**上傳即縮圖**：伺服器端一律重新編碼為 WebP、長邊上限 2560px、**不留原始檔**、去 EXIF（含 GPS），固定產 1280／640／320 ＋ 後台 160px 方形縮圖。**後台依前台單元切分、前後台同名、介面一律日常中文**——不顯示模組代號、權限碼、隊別代號與英文技術詞（對照表見 [`docs/06`](docs/06-conventions.md) §1）。附**前後台對照表**：每個模組產出前台的哪裡。v3.7 更名九個子模組 |
| **SEO／GEO** | ✅ **規格已完成**（主站規劃書 §7）。SEO 九項基礎 ＋ **`GEO-01`–`GEO-09`**，**兩個官網各自完整實作一份**：各自的 `llms.txt`（雙語）與 `robots.txt`、事實單一來源、Schema 全輸出。**AI 爬蟲全站允許但排除個資與未成年素材**。後台 `H` 模組承接。⚠️ **慈善平台明文不做 SEO／GEO** |
| 視覺方向 | ✅ 已定案並落實於 [`site/src/assets/css/tcrfc.css`](site/src/assets/css/tcrfc.css)（design tokens 在 `:root`）。Cloudflare Pages 專案 `tcrfc-mockup` 部署 `site/dist` **80 頁**前台，全站 `noindex`。**藍鯨站將複製骨架、只換 7 個品牌變數**（見 [`docs/13`](docs/13-blue-whale-site.md) §6） |
| 品牌資產 | ✅ TCRFC 已由 logo 主檔萃取完成；design tokens 已校正為 `.ai` 品牌色。🟡 **藍鯨隊徽已取得點陣主檔**（2026-09-14 自既有官網下載原圖，3299×3243 去背 PNG，見 [`brand/blue-whale/`](brand/blue-whale/README.md)）——**足夠文件、網頁與 App 圖示，但不是向量**。✅ **網頁色值已自隊徽取樣定案**（七個變數）。🔴 **向量原始檔、印刷色票、英文正式全名仍未提供** |
| **資料庫綱要** | ✅ **v3.0 已同步、可轉 DDL**（2026-09-20，[`docs/12-database-schema.md`](docs/12-database-schema.md)）。**103 張表**，`club_id` **50 必填／9 可為空／43 不加**。原「v3.0 落差」的 **13 項**必須以規劃書為準的事項（**第 13 項為 v3.5 的圖片欄位直傳：`MediaAsset`／`MediaFolder`／`MediaUsage` 三表移除、10 處外鍵改欄位組**）；**§0／§1.4／§7 權限模型／女足相關敘述已先行更新**，但 **§4 資料表總覽、17 張 ERD、§6 五節明細、§11.1 唯一鍵表、§14 檢核表尚未逐一改寫**。**轉 DDL 前必須完成** |
| **技術選型** | ✅ **已定案**（2026-09-18，見 [`docs/17`](docs/17-deployment.md)）：**Nuxt 4 SSR ＋ .NET／EF Core＋Dapper ＋ Azure SQL ＋ Azure Blob ＋ Redis**，跑在**單一 Azure VM（Japan East／東京）** 的 Docker 上（前台三個、後台兩個、API 一個、快取一個），Cloudflare 在前。**規劃書仍不記技術選型**（§1.3 明文排除），結果只在導航層；**App 客戶端另見 [`docs/19`](docs/19-app-tech-stack.md)**。`docs/12` §1.4 的**五件事已全部定案** |
| **部署與金流前提** | 🔴 **LINE Pay 正式環境須登記付款伺服器的出口 IP**——這條外部約束是選「自架 VM ＋ 靜態 Public IP」的原因，也是規劃書 v3.8／v2.4 唯一新增的內容。**改機器＝改白名單，等同停機事件**。⚠️ 另有**五類資料不得讀快取**（庫存、金流冪等、會員卡驗證、會籍與訂單狀態、購物車），見 [`docs/14`](docs/14-invariants.md) |
| 網站本體 | ❌ 尚未開發。⚠️ **前台定為 Nuxt 4 SSR，現有 `site/` 的 80 頁靜態骨架與 `build.mjs`／`verify.mjs` 要重做**；`verify.mjs` 的六項檢查須移植為 Nuxt 專案的 lint／test，不要直接丟棄 |
| 內容 | 🔄 **已首批交件**（456MB／212 張原始照片／113 篇文稿）。盤點見 [`docs/09-intake-inventory.md`](docs/09-intake-inventory.md)。**阻塞：文稿全為 `.gdoc` 捷徑，本機讀不到** |
| 版本控制 | ✅ 已 `git init`。收件夾與大型素材未納管，覆寫或刪除前仍請先看過內容 |

**客戶已同意製作。要交付的是五個平台**（主站前台／共用後台／藍鯨官網／慈善捐款平台／行動 App），
**逐項工作、阻塞清單與前置關係全部在 [`STATUS.md`](STATUS.md)**——動手前先開那一份。
**技術選型已定案、資料庫綱要已可轉 DDL**（2026-09-20）。擋在最前面的改為**前台改 Nuxt 的骨架重做**與**藍鯨的網域、賽程與名單**；內容線則卡在 113 篇 `.gdoc` 取不回來
（流程見 [`docs/07-content-pipeline.md`](docs/07-content-pipeline.md)）。

---

## 目錄地圖

| 路徑 | 內容 | 性質 |
|---|---|---|
| [`output/`](output/) | **四份規劃書**（主站＋**台中藍鯨官網**＋慈善捐款平台＋行動 App）、開發里程碑，以及**四份客戶版文件**：**兩份版面式**（慈善捐款站台地圖、行動 App 功能說明，含手機示意畫面，由 `tools/` 的 Python 腳本產生）＋ **兩份純文字**（**官網功能說明**、**台中藍鯨官網功能說明**，Markdown 母檔，**含後台完整模組一覽**）。中英雙版。**只有母檔（`.md`、里程碑 `.html`、`tools/`）納版控，PDF 與兩組產生的 HTML 不納管**，見 [`output/tools/`](output/tools/README.md) | **交付物，真實來源** |
| [`STATUS.md`](STATUS.md) | **工作追蹤表**：五個平台、阻塞清單、階段 0–4 的逐項工作 | 執行層（本專案自用） |
| [`docs/`](docs/) | 從規劃書拆解的工作文件 | 導航層（本專案自用） |
| [`db/`](db/) | **資料庫 DDL**：`club-schema.sql`（主站）與 `charity-schema.sql`（慈善獨立庫）。**綱要的真實來源是 [`docs/12`](docs/12-database-schema.md)／[`docs/16`](docs/16-charity-schema.md)，改綱要要先改文件再改 DDL** | 交付物 |
| [`brand/`](brand/) | 由 `.ai` 萃取的 SVG 標誌、favicon／PWA icon、OG 圖，說明見 [`brand/README.md`](brand/README.md) | **品牌資產庫** |
| [`reference/`](reference/) | 品牌簡報 pptx、sitemap 圖、Logo 主檔 `TCR_logo_CMYK.ai`、參考網站截圖、協會立案證書。**不納版控**（客戶資產且含個資，GitHub repo 是公開的），clone 下來不會有這個資料夾 | 客戶提供素材 |
| [`TCRFC_資料收件夾/`](TCRFC_資料收件夾/) | 給客戶放既有檔案的分類結構（83 個資料夾，對應 13 單元） | 內容收件 |
| [`content/`](content/) | 抽出的結構化資料：2026/27 企甲賽程、舊官網 128 筆 URL 盤點 | 抽取產物 |
| `.wrangler/` | Cloudflare Pages 部署快取 | 工具產生，勿手動改 |

---

## 文件索引

| 文件 | 什麼時候讀 |
|---|---|
| [`STATUS.md`](STATUS.md) | **要動手做事之前**。要做的五個平台、現在擋住開工的 11 件事、階段 0→4 的逐項工作清單與前置關係。**進度追蹤在這裡，規格不在** |
| [`docs/00-harness.md`](docs/00-harness.md) | **每個 session 先讀這份**。文件如何分工、任務對應該讀哪一段（含規劃書行號對照） |
| [`docs/01-site-architecture.md`](docs/01-site-architecture.md) | 需要知道網站有哪些頁、層級怎麼分、URL 怎麼定 |
| [`docs/02-frontend-spec.md`](docs/02-frontend-spec.md) | 要做前台任一頁面／區塊 |
| [`docs/03-admin-spec.md`](docs/03-admin-spec.md) | 要做後台模組或處理權限 |
| [`docs/04-data-model.md`](docs/04-data-model.md) | 要知道有哪些內容型別、哪些關係不能搞錯、匯入格式（**實際資料表看 `docs/12`**） |
| [`docs/05-i18n-seo.md`](docs/05-i18n-seo.md) | 處理雙語、SEO、**GEO（`GEO-01`–`GEO-09`，v3.6 起是正式規格）**、效能與無障礙 |
| [`docs/06-conventions.md`](docs/06-conventions.md) | 命名、術語、**後台介面用語對照表**（技術寫法 → 一般人看得懂的寫法）、色彩字級、日期與檔名格式 |
| [`docs/07-content-pipeline.md`](docs/07-content-pipeline.md) | 處理客戶交來的素材 |
| [`docs/08-roadmap-decisions.md`](docs/08-roadmap-decisions.md) | 排程、已定案前提、待確認事項 |
| [`docs/09-intake-inventory.md`](docs/09-intake-inventory.md) | 要知道客戶交了什麼、缺什麼、哪裡卡住 |
| [`docs/10-charity-donation-site.md`](docs/10-charity-donation-site.md) | **慈善捐款平台的任何工作**（掃碼、捐款、LINE Pay、發票、分潤、報表） |
| [`docs/11-mobile-app.md`](docs/11-mobile-app.md) | **行動 App 的任何工作**（會員卡、賽程、附近店家、課程報名、推播、廣告版位） |
| [`docs/12-database-schema.md`](docs/12-database-schema.md) | **要設計或實作資料表**（入口：型別詞彙、雙語策略、模組地圖、資料表總覽、踩雷點、檢核表）。⚠️ **v3.0 尚未逐張同步，檔頭有「v3.0 落差」段落必讀**。不含行動 App 型別、沒有日誌表 |
| [`docs/12a-database-erd.md`](docs/12a-database-erd.md) | 要看**關聯圖**（§5，15 張 ER 圖） |
| [`docs/12b-database-tables.md`](docs/12b-database-tables.md) | 要寫**欄位**（§6–§11：明細、權限模型、受限與加密欄位、快照、匯入匯出、索引與唯一鍵） |
| [`docs/12c-i18n-tables.md`](docs/12c-i18n-tables.md) | 要寫 **`*_i18n` 側表**：47 張側表的欄位清單，每欄附規劃書行號與信心度 |
| [`docs/12d-field-audit.md`](docs/12d-field-audit.md) | 🔴 **主站 ERD 的欄位缺漏盤點**（104 張全表核對）。43 筆已於 S0-3d 補完 |
| [`docs/16a-charity-field-audit.md`](docs/16a-charity-field-audit.md) | 🔴 **慈善庫的欄位缺漏盤點**（23 張）。**建庫前必看**——含「對帳結果無資料結構」 |
| [`docs/16-charity-schema.md`](docs/16-charity-schema.md) | **慈善捐款平台的資料表**（獨立資料庫，23 張表）。與 `docs/12` 平行且互不包含；四項刻意的差異在 §9 |
| [`docs/13-blue-whale-site.md`](docs/13-blue-whale-site.md) | **台中藍鯨官網的任何工作**（單元取捨、藍鯨方帳號權限、藍鯨會籍與商店、前台建置的技術判斷） |
| [`docs/14-invariants.md`](docs/14-invariants.md) | **動手前掃一次**。全站不變量與踩雷速查：代號、品牌色、命名、範圍邊界、五種商業對象、資料庫執行層決定 |
| [`docs/15-out-of-scope-record.md`](docs/15-out-of-scope-record.md) | **規劃書查不到某功能時先看這裡**。已移出範圍的功能，以及法人歸屬、授權、個資委託等行政法務背景（都是非交付物） |
| [`docs/18-work-errors.md`](docs/18-work-errors.md) | **做錯過的事**。實際犯過的失誤、根因與防呆位置（`E-01`–`E-10`）。**動手前與 [`docs/14`](docs/14-invariants.md) 一起掃**；改規格、改共用區塊、順移代號、重產客戶版之前**一定要看**。與 `docs/14`（改錯會出事）、[`docs/00`](docs/00-harness.md) §5（舊規格會誤導）分工不同 |
| [`docs/20-cicd.md`](docs/20-cicd.md) | **CI/CD 的任何工作**：觸發與分支、GHCR、五個映像檔、self-hosted runner、資料庫遷移關卡、回滾、Secrets 清單。🔴 **公開 repo ＋ self-hosted runner 的防護鏈在 §4，第 0 條是地基** |
| [`docs/17-deployment.md`](docs/17-deployment.md) | **部署、基礎設施、技術選型的任何工作**。拓撲與容器佈局、VNet 與服務端點、**LINE Pay 固定出口 IP**、**快取策略與「不得讀快取」清單**、DBMS 的連帶決定、已知風險與驗證程序。**這是執行層決定，不是規格** |
| [`docs/19-app-tech-stack.md`](docs/19-app-tech-stack.md) | **行動 App 的技術實作、CI 與上架**。原生雙平台選型、`shared/` 契約目錄、權杖與安全儲存、推播傳輸、廣告可見度量測、設定下發的三層來源、監控、送審管線。**執行層決定，不是規格** |

---

## 全域規定

> 這十四條是**跨全專案**的。細節在導航層，本檔只留規則本身與指標。

1. **不要整份讀規劃書。** 先查 [`docs/00-harness.md`](docs/00-harness.md) 的行號對照表，只讀需要的章節。
2. **規劃書是唯一真實來源。** [`docs/`](docs/) 只做導航與濃縮，**不得引入規劃書沒有的新規格**；
   需要新增規格時，**先改規劃書再同步 `docs/`**。
3. **功能有任何異動，一律跑完同步鏈**——七個環節、收尾自檢、範圍縮減的寫法、客戶版的三條規則，
   全部在 [`docs/00-harness.md`](docs/00-harness.md) **§2.5 規格異動的同步鏈**。
   **只在對話裡講過不算數**：下一個 session 讀不到，就會照舊規格做下去。
4. **所有前台可見的內容型別都要有 `zh` / `en` 雙欄位**，英文可空但欄位必須存在。
5. **`noindex` 不要拿掉**（[`site/src/_headers`](site/src/_headers)），正式站上線前它不該被索引。
6. **版控範圍**（`.gitignore` 有完整註解）：
   - **不納管**：[`TCRFC_資料收件夾/`](TCRFC_資料收件夾/)、`reference/`、`site/src/assets/img/`、`output/*.pdf`、產生的 `.html`
   - **納管**：規劃書與客戶版母檔、里程碑母檔、`output/tools/`、[`docs/`](docs/)、[`db/`](db/)、[`brand/`](brand/)、[`site/src/`](site/src/) 其餘部分
   - **兩個 remote 內容相同**：`Remote_GitHub`（公開）與 `Remote_NAS`（離線備份）。未納管的素材備份走 NAS 的檔案層
   - **覆寫或刪除未納管的內容前先看過，git 救不回來**
7. **客戶素材涉及個資與肖像權**（未成年學員照片、會員資料）。不要外傳、不要放進會被公開的檔案。
   **GitHub repo 是公開的**——`reference/` 不納管正是因為這個。
8. **開工前先開 [`STATUS.md`](STATUS.md)**，從最上面還沒打勾的那一列做起。它是進度表不是規格書——
   發現規格要改，跑同步鏈改規劃書，不要在 `STATUS.md` 裡寫規格。
9. **動手改東西前掃一次 [`docs/14-invariants.md`](docs/14-invariants.md)。** 那裡是「改錯會出事」的清單：
   隊別代號全站唯一、品牌色的唯一來源、名稱寫法、五種商業對象不可混用、`D1` 的雙重身分
   （後台課程模組已改編為 `P1–P4`，看到舊文件寫 `D1 課程` 一律視為錯誤）。
10. **一律用繁體中文回應。** 對使用者的所有回覆、說明、提交訊息與文件內文都用繁體中文（台灣用語）；
    程式碼、識別字、指令與既有英文專有名詞維持原文，不要硬翻。
11. **改了程式或規則，文件一定要跟著改**——同一次交付內完成，不留「之後再補」。
    改**規格**走第 3 條的同步鏈（先改規劃書再同步 `docs/`）；改**程式或執行層決定**（部署、設定、腳本、
    建置流程）則同步更新導航層對應檔案（[`docs/`](docs/)、[`docs/14-invariants.md`](docs/14-invariants.md)、
    [`docs/17-deployment.md`](docs/17-deployment.md)）與 [`STATUS.md`](STATUS.md) 的進度列。
    **文件沒改＝這件事沒做完。**
12. **該叫 agent 的事就叫 agent 做，不要自己硬幹。** 依任務性質派工：
    後端與資料庫→`backend-engineer`；前台 Nuxt／Vue→`frontend-architect`；行動 App→`mobile-app-engineer`；
    部署與 CI/CD→`deployment-engineer`；資料表與技術文件→`system-analyst`；需求與藍圖→`software-architect-blueprint`；
    版面與視覺→`visual-design-architect`；程式審查→`code-review-optimizer`；測試與品質把關→`qa-test-engineer`；
    大範圍搜尋→`Explore`。多個互不相依的任務要**同一則訊息一次派出**並行跑。
    **判準看產出的性質，不是「自己做得動嗎」**：從零設計資料表、ERD、架構、API 一律派；
    照規劃書逐條搬運可自理。**覺得規定不適用時說出來讓使用者決定，不要自己判進判出**（`E-11`）。
13. **出錯就記到 [`docs/18-work-errors.md`](docs/18-work-errors.md)，同一次交付內補上。**
    不論是使用者當場指正、審查抓到、驗證腳本抓到，還是流程跑完才發現漏了一環，都要留一筆：
    **日期／錯在哪／為什麼會錯（根因，寫成可以被改掉的行為，不准寫「不小心」）／下次怎麼避免／防呆在哪（沒有就寫「無」）**。
    **同一類錯犯第二次不要再加一筆**——回頭把那筆升級成防呆或寫進 [`docs/14-invariants.md`](docs/14-invariants.md)；
    記了兩次還在犯，代表要的是機制不是記錄。**這條的目的是不要犯第二次，不是檢討。**
14. **照規劃書開發，不要問使用者下一件做什麼、怎麼做。** 順序照 [`STATUS.md`](STATUS.md) 由上往下（第 8 條），
    做法照規劃書與由它導出的 `docs/12`／`db/*.sql`；**已有答案的事直接做，不列選項請使用者挑**，
    回報進度時寫「接著做 X」然後開始做，不要用「要從哪一件開始？」收尾。前置未完成或被 🚫 擋住的列跳過並註明原因。
    **只有兩種情況才問**：① 規劃書沒寫、又會影響客戶看得到的行為；② 動到外部或不可逆的事
    （開雲端資源、對外發送、刪除未納管內容、`git push`）。規格已定、只差「怎麼做」的屬於執行層，自己決定後寫進 `docs/`（`E-57`）。

---

> **這份檔案是索引與全域規定，不是規格書，也不是知識庫。**
> 新的事實請寫進導航層（[`docs/`](docs/)）的對應檔案，不要往這裡堆——
> 本檔一旦長成第二份規格書，它就不再會被完整讀完，也就失去索引的作用。
