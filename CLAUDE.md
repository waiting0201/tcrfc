# CLAUDE.md — TCRFC 官方網站專案

台中磐石足球俱樂部（Taichung Rock FC，**TCRFC**）官方網站建置專案。
品牌主張：**LOCAL ROOTS. GLOBAL PATHWAYS.｜在地扎根 · 放眼世界**

> **本檔是索引，不是規格書。** 規格的真實來源是兩份規劃書：
> [`output/TCRFC_前後台功能規劃書.md`](output/TCRFC_前後台功能規劃書.md)（v2.3，1323 行，官網主站）與
> [`output/TCRFC_慈善捐款平台功能規劃書.md`](output/TCRFC_慈善捐款平台功能規劃書.md)（v1.1，686 行，獨立網域的掃碼捐款平台）。
> `docs/` 是為了讓 AI 與新進人員快速上手，從規劃書拆解出來的**導航層與執行規範**。
> **兩者衝突時，一律以規劃書為準**，並回頭修正 `docs/`。

---

## 一分鐘現況

| 項目 | 狀態 |
|---|---|
| 功能規劃 | ✅ 完成（規劃書 v2.3，中英雙版 md／PDF 皆已更新）。**v2.3 新增付費會員抽獎（後台 K5）；v2.2 更正英文隊名為 `Taichung Rock FC`；v2.1 將球迷捐款移出主站，志工報名移出範圍** |
| 慈善捐款平台 | 📄 **規格已完成**（獨立規劃書 v1.1，中英雙版）。獨立網域、共用主站後台與資料庫、串接 LINE Pay 與電子發票。**尚未開發；網域與勸募資格未定** |
| 視覺方向 | ✅ 單頁 mockup 已完成（Cloudflare Pages 專案 `tcrfc-mockup` 目前部署的是 `site/dist` 73 頁前台，全站 `noindex`） |
| 品牌資產 | ✅ [`brand/`](brand/) 已由 logo 主檔萃取完成；design tokens 已校正為 `.ai` 品牌色 |
| 技術選型 | ❌ **未定案**（規劃書第 10 節明確排除在範圍外） |
| 網站本體 | ❌ 尚未開發 |
| 內容 | 🔄 **已首批交件**（456MB／212 張原始照片／113 篇文稿）。盤點見 [`docs/09-intake-inventory.md`](docs/09-intake-inventory.md)。**阻塞：文稿全為 `.gdoc` 捷徑，本機讀不到** |
| 版本控制 | ✅ 已 `git init`（branch `master`）。收件夾與大型素材未納管，覆寫或刪除前仍請先看過內容 |

**目前的主線工作**：首批素材已到、盤點完成 → **取回 113 篇 Google Docs 文稿**（阻塞中）→ 改寫成網頁文案 → 套入網站。
流程見 [`docs/07-content-pipeline.md`](docs/07-content-pipeline.md)。

---

## 目錄地圖

| 路徑 | 內容 | 性質 |
|---|---|---|
| [`output/`](output/) | **兩份規劃書**（主站＋慈善捐款平台）、開發里程碑（中英 × md/html/pdf）。PDF 由 [`output/tools/`](output/tools/README.md) 產生，勿手改 | **交付物，真實來源** |
| [`docs/`](docs/) | 從規劃書拆解的工作文件 | 導航層（本專案自用） |
| [`mockup/`](mockup/) | 單頁視覺 mockup（`index.html` 1330 行，內含 design tokens）與示意圖 | 視覺基準 |
| [`brand/`](brand/) | 由 `.ai` 萃取的 SVG 標誌、favicon／PWA icon、OG 圖，說明見 [`brand/README.md`](brand/README.md) | **品牌資產庫** |
| [`reference/`](reference/) | 品牌簡報 pptx、sitemap 圖、Logo 主檔 `TCR_logo_CMYK.ai`、參考網站截圖 | 客戶提供素材 |
| [`TCRFC_資料收件夾/`](TCRFC_資料收件夾/) | 給客戶放既有檔案的分類結構（83 個資料夾，對應 13 單元） | 內容收件 |
| [`content/`](content/) | 抽出的結構化資料：2026/27 企甲賽程、舊官網 128 筆 URL 盤點 | 抽取產物 |
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
| [`docs/09-intake-inventory.md`](docs/09-intake-inventory.md) | 要知道客戶交了什麼、缺什麼、哪裡卡住 |
| [`docs/10-charity-donation-site.md`](docs/10-charity-donation-site.md) | **慈善捐款平台的任何工作**（掃碼、捐款、LINE Pay、發票、分潤、報表） |

---

## 關鍵事實速查

- **隊別代號**：`D1`（＝ First Team 一線隊，全站僅一支）／`U15`／`U14`／`U12`。
  `D1` 是代號，對外顯示一律寫 `First Team / 一線隊`。女足**不建立**球隊資料。
- **品牌色**：桃紅 `#E0218A`（PANTONE 225 C／C5 M90 Y0 K0）＋ 品牌黑 `#231916`（K100，**暖調近黑，不是深藍**）。
  唯一來源是 [`reference/TCR_logo_CMYK.ai`](reference/TCR_logo_CMYK.ai)。小字情境改用 AA 安全版 `#D61E83`。
- **名稱寫法**：中文簡稱一律「**台中磐石**」，不單獨用「磐石」。英文名一律 `Taichung Rock FC`
  （全稱 `TAICHUNG ROCK FOOTBALL CLUB`）。舊稿的 `Taichung Cornerstone RFC` 已汰換，看到視為錯誤。
- **標誌**：只用 [`brand/svg/`](brand/svg/) 由 `.ai` 萃取的三種組合（隊徽／隊徽＋TCRFC 英文版／隊徽＋TCRFC＋台中磐石足球俱樂部 中文版）。
  **不得自行排字、不得加 `SINCE` 或年份。**
- **雙語**：繁中（預設）＋英文，URL 以 `/zh/`、`/en/` 區隔，架構須預留第三語系。
- **主站不做的事**：站內電商、金流、購物車、訂單、庫存（全數導向 Shopify）；票務與門票套票；會員點數與電子錢包；特約店家掃碼核銷；**站內捐款與志工報名**（v2.1 移出）；女足名單與賽程（導向女足官網）；**系統隨機開獎、前台抽獎頁與「我的抽獎」、中獎通知信**（v2.3）。
  「**不接金流**」這一條**只適用於主站**——慈善捐款平台正式串接 LINE Pay Online API。
- **會員系統只做會籍**：免費與付費兩層，會員享特約店家折扣，付費會員（＝球迷會員 `fan_club`，不是另一種身分）另獲球衣與**抽獎資格**。
  會籍採球季制，會費走 LINE Pay 收款連結與現場收款，官網不接金流。**不做**報名歸戶、表單自動帶入、學員家長綁定、通知中心、LINE 推播、Google 登入。
- **付費會員抽獎（v2.3，後台 K5）**：資格是**布林值**——基準時間會籍有效即**自動具備，會員零操作**，一人一號，不因消費／簽到／分享／參加次數增加機會（**所以不是點數**）。
  系統**只做名單快照、序號配發、CSV 匯出**；**實體開獎由人工在現場或直播進行**，中獎人後台回填。中獎**只以最新消息公布**（7.1 ＋標籤，遮罩），系統信維持五封。
  獎品為**實體物品**人工寄送或現場領取（**所以不碰票務與掃碼核銷**）。`DrawRoster` 是**不可變快照**，值複製非外鍵，不得改成即時 query。
- **慈善捐款平台**：獨立網域、獨立前台專案，**共用主站後台與資料庫**（新增 `N` 模組）。合作店家貼 QR → 客人掃碼看到店名 → 選捐款項目 → 讀說明 → 頁尾以 **LINE Pay** 捐款 → 自動開**電子發票或捐贈收據**（依項目設定）。
  捐款人**不登入不註冊**，只填姓名與 Email。**店家分潤與項目分潤相加、分別入帳**，系統結算並產對帳單，**實際匯款人工執行**。**不做店家帳號、不做掃碼核銷、不做 SEO／GEO 優化**（v1.1；入口是 QR 掃碼，但頁面仍維持可被索引）。
- **兩種「店家」不要搞混**：`DonationStore`（慈善站掃碼引流，有分潤有金流）≠ `PartnerStore`（主站 8.4 特約店家，會員折扣，無金流無分潤）。同一家實體店在兩邊各建一筆，不共用紀錄。
- **賽事資料全部人工維護**，不串接外部 API，提供 CSV 批次匯入。
- **既有數位資產**：官網 www.tcrfc.tw（待遷移）、IG `@tcr_fc_2024`、FB `TCRFC2024`、YouTube `@TCRFC-2024`、
  女足官網 [台中藍鯨](https://www.tcbw2014.com/)（06 頁導流目標，本站不維護其名單與賽程）。
- **成立年份 2024**，2024 全國乙級聯賽冠軍。

---

## 工作守則

1. **不要整份讀規劃書。** 1200 行會吃掉大量 context。先查 [`docs/00-harness.md`](docs/00-harness.md) 的行號對照表，只讀需要的章節。
2. **規劃書是唯一真實來源。** `docs/` 只做導航與濃縮，不得引入規劃書沒有的新規格；若發現需要新增規格，先改規劃書再同步 `docs/`。
3. **功能有任何異動，一律同步更新文件——這不是收尾工作，是工作的一部分。**
   客戶改需求、範圍增刪、決策拍板，都算功能異動。**只在對話裡講過不算數**，下一個 session 讀不到就會照舊規格做下去。
   異動時**依序**跑完這條鏈（每一環都要做，不要只改第一環）：

   | # | 動作 | 檔案 |
   |---|---|---|
   | 1 | 改規格本體，並**提高版本號**、在開頭補「修訂摘要」 | [`output/TCRFC_前後台功能規劃書.md`](output/TCRFC_前後台功能規劃書.md)／[`output/TCRFC_慈善捐款平台功能規劃書.md`](output/TCRFC_慈善捐款平台功能規劃書.md) |
   | 2 | 同步英文版（中文為主、英文為譯本） | [`output/TCRFC_Website_Functional_Specification_EN.md`](output/TCRFC_Website_Functional_Specification_EN.md)／[`output/TCRFC_Charity_Donation_Platform_Specification_EN.md`](output/TCRFC_Charity_Donation_Platform_Specification_EN.md) |
   | 3 | 重產 PDF（**勿手改 PDF**） | `node output/tools/build-pdf.mjs zh en charity-zh charity-en`（只跑改到的那幾個即可） |
   | 4 | 同步導航層，含**重算行號對照表**與踩雷點 | [`docs/`](docs/)（尤其 [`00-harness.md`](docs/00-harness.md)、慈善站改動另加 [`10-charity-donation-site.md`](docs/10-charity-donation-site.md)） |
   | 5 | 改前台骨架並跑建置與自檢 | [`site/`](site/)；`node site/build.mjs && node site/verify.mjs` |
   | 6 | 登記新的待補項目、刪掉已過期的 | [`content/migration/待補內容清單.csv`](content/migration/待補內容清單.csv) |
   | 7 | 若異動涉及全站前提或「不做的事」，回寫本檔的速查與現況表 | `CLAUDE.md` |

   **收尾自檢**：全文 grep 被刪掉的功能名稱與資料型別，確認沒有殘留引用；
   凡是「決定不做」的事，要寫進規劃書（範圍段、該章節、已定案表）而不是只從清單裡刪掉——
   否則下次會被當成漏寫再加回來。
4. **`D1` 的雙重身分要小心。** 後台課程模組原編號 `D1–D4` 已因撞名改為 `P1–P4`，看到舊文件寫 `D1 課程` 一律視為錯誤。
5. **所有前台可見的內容型別都要有 `zh` / `en` 雙欄位**，英文可空但欄位必須存在。
6. **mockup 的 `noindex` 不要拿掉**（[`mockup/_headers`](mockup/_headers)），正式站上線前它不該被索引。
7. **收件夾與大型素材未納版控。** git 已初始化（branch `master`），但這些內容不在版控內，覆寫或刪除前先看過。
8. **客戶素材涉及個資與肖像權**（未成年學員照片、會員資料）。處理收件夾內容時不要外傳、不要放進會被公開的檔案。
