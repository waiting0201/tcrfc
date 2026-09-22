# STATUS — 要做的事，一件一件來

> **最後更新**：2026-09-21
> **這是工作追蹤表，不是規格書。** 規格的真實來源永遠是 [`output/`](output/) 的四份規劃書；
> 本檔只回答「**現在該做哪一件、做到哪了、卡在哪**」。
> 文件分工見 [`CLAUDE.md`](CLAUDE.md)，工作流程見 [`docs/00-harness.md`](docs/00-harness.md)。

---

## 這份檔怎麼用

1. **從最上面還沒打勾的那一列開始。** 表是排過序的，上面沒做完不要跳到下面——前置關係寫在「前置」欄。
2. **動手前先讀「規格在哪」欄指的那一段**，不要整份讀規劃書（[`docs/00-harness.md`](docs/00-harness.md) §2 有行號對照）。
3. **動手前掃一次 [`docs/14-invariants.md`](docs/14-invariants.md)**，那裡是「改錯會出事」的清單。
3b. 🧭 **做任何後台模組前先看規劃書 §4.0 的後台設計通則與前後台對照表。**
   後台依**前台單元**切分、**前後台同名**、介面一律**日常中文**——不顯示模組代號、權限碼、隊別代號與英文技術詞。
   **本檔用代號（`S1-12`、`B3`、`K4`）是內部溝通用，那些字不進後台畫面。**
4. **做完把 ⬜ 改成 ✅ 並補上完成日期。** 開始做但沒做完的改 🔄，被卡住的改 🚫 並在「卡住原因」欄寫清楚。
5. ⚠️ **做的過程發現規格要改，不要直接改這裡，也不要只在對話裡講。**
   跑 [`docs/00-harness.md`](docs/00-harness.md) **§2.5 規格異動的同步鏈**（七個環節），改完規劃書再回來更新本檔。

6. ⚠️ **做錯了就記一筆到 [`docs/18-work-errors.md`](docs/18-work-errors.md)**，在同一次交付內補上，不要留到下次。
   改規格、改共用區塊、順移代號、重產客戶版之前**先掃該檔 §1 速查**——這四件事都有前科。

**狀態符號**：⬜ 未開始　🔄 進行中　✅ 完成　🚫 被擋住

---

## 要做的是這五個

| # | 平台 | 型態 | 後台與資料庫 | 收款主體 | 規格 |
|---|---|---|---|---|---|
| 1 | **TCRFC 官網主站前台** | 現有網域，13 單元＋站內商店，中英雙語 | 共用 ② | 俱樂部 | [主站規劃書](output/TCRFC_前後台功能規劃書.md) v3.12 §3 |
| 2 | **共用後台 Admin** | 一個入口＋站台切換器，**14 個模組字母**（`A B C P E F G H I J K L M S`；慈善的 `N` 是獨立後台不算在內） | **本體** | — | 同上 §4 |
| 3 | **台中藍鯨官網前台** | **獨立網域**，11 單元，中英雙語 | **共用 ②**（`club_id` 分資料） | 內容藍鯨／收款俱樂部 | [藍鯨規劃書](output/TCRFC_台中藍鯨官網功能規劃書.md) v1.8 |
| 4 | **慈善捐款平台** | **獨立網域**，掃碼捐款前台＋自己的後台 | **完全獨立**（自建約 22 張表） | **台灣足球策略發展協會** | [慈善規劃書](output/TCRFC_慈善捐款平台功能規劃書.md) v2.5 |
| 5 | **行動 App** | **原生 iOS（Swift／SwiftUI）＋ Android（Kotlin／Compose）**，雙隊共同平台 | 共用 ②（後台 `M1–M5`） | 俱樂部（**首版不含 App 內付款**） | [App 規劃書](output/TCRFC_行動App功能規劃書.md) v3.12、[`docs/19`](docs/19-app-tech-stack.md) |

> **③ 藍鯨站與主站是同一套網站，只有配色不同**（藍鯨規劃書 §1.3 總則）。前端複製 `site/` 骨架、換 7 個 CSS 變數；
> 例外只有四項單元取捨。**不要為藍鯨站另做設計。**

---

## 0. 現在擋住開工的事

**這一段沒解掉，下面一件都不能真的開始。**

| # | 狀態 | 擋住什麼 | 事項 | 誰能解 |
|---|---|---|---|---|
| ~~B-1~~ | ✅ | ~~全部開發~~ | **技術選型已定案**（2026-09-18）：Nuxt 4 SSR ＋ .NET／EF Core＋Dapper ＋ Azure SQL ＋ Azure Blob ＋ Redis，單一 Azure VM（**Japan East／東京**，2026-09-20 改定）＋ Docker。見 [`docs/17`](docs/17-deployment.md) | — |
| ~~B-2~~ | ✅ | ~~建資料表~~ | **資料庫綱要 v3.0 同步完成**（2026-09-20）：§4 總覽、14 張 ERD、§6 明細、§11 唯一鍵與索引、§14 檢核表全部改寫；`club_id` **50 必填／9 可為空／43 不加**，與主站 §5.4 逐名一致（三張漏列的型別已於主站 v3.10 補齊）。**可以轉 DDL** | — |
| B-3 | 🚫 | **113 篇文稿轉網頁文案** | 客戶交來的文稿全是 `.gdoc` 捷徑，**本機讀不到**（[`docs/09`](docs/09-intake-inventory.md)） | 客戶 |
| B-4 | 🚫 | **藍鯨站上線、App 深連結** | **藍鯨網域**：名稱、持有人、DNS 控制權（Universal Link 只能綁自己持有的網域）。**過渡期先用 `bw-stg.tcrfc.tw` 開發測試**（[`docs/17`](docs/17-deployment.md) §10.1），但**不解決** App 上架前需要最終網域這件事 | 客戶 |
| B-5 | 🚫 | **藍鯨印刷品**、**Schema／`llms.txt`／App 的英文名** | **藍鯨隊徽向量主檔**（`.ai`／`.svg`／`.eps`，含深色版）與**英文正式全名**。⚠️ 網頁色值與點陣主檔已到位，**網站與 App 不受阻**。🔴 **2026-09-22 盤點發現英文全名不只是「還沒給」——舊站自己就有三種寫法並存**（`Taichung Bluewhale`／`Taichung Blue Whale Women's Football Team`／`Taichung blue whale`），**隊徽兩代的使用年份在舊站也互相重疊（2016–2023）**。選錯會一路汙染 Schema、`llms.txt` 與 App，**必須由客戶指定唯一寫法，不得由開發端挑一個** | 客戶 |
| B-6 | 🚫 | **藍鯨一線隊／青年隊／行事曆** | **藍鯨球員與教練名單（含肖像同意）**、**12 個月賽程資料**。🔴 **2026-09-22 盤點後具體化**：舊站有 **2014–2024 共 11 年名單**，但**欄位只有背號＋姓名**——位置／生日／身高／國籍／加盟年全缺；**2025 名單缺**；**球員個人頁 11 年只有 1 頁**（蔡明容，可當欄位範本）；**肖像同意狀態一律未知**；U15 有 18 位未成年球員名單。**未來 12 個月賽程完全沒有**，舊站最新的完整賽季資料是 **2023** | 客戶（藍鯨由磐石團隊營運，資料在自己手上） |
| B-7 | 🚫 | **慈善平台全部** | **慈善網域、協會品牌資產、法人登記與統編、獨立後台的維運人力**。**過渡期先用 `charity-stg.tcrfc.tw` 開發測試，🔴 不得印上任何對外物料**（QR Code 必須等最終網域，[`docs/17`](docs/17-deployment.md) §10.3 不可逆類第②項） | 客戶／協會 |
| B-8 | 🚫 | **藍鯨商店結帳** | **代收代付的稅務認定**（銷貨還是受託代銷）。此判定連帶決定「**訂單是否於結帳時依俱樂部拆單**」 | 會計師 |
| B-9 | 🚫 | **表單上線、抽獎上線、課程報名上線** | **個資保存期限**未定；**抽獎辦法與會員條款**未經法務核定。🔴 **新增（2026-09-20）：課程報名的「健康聲明」可能構成《個資法》§6 的特種個資**（病歷／醫療／健康檢查），且當事人多為未成年學員——**要確認的是能不能蒐集、要不要蒐集、保存多久，那在「怎麼存」之前**。見 [`docs/12b`](docs/12b-database-tables.md) §8 | 法務 |
| B-10 | 🚫 | **Phase 3 商店** | **俱樂部 LINE Pay 商店號**與**電子發票開立管道**（字軌不得與協會共用）。**申請有前置期，須於 Phase 2 就啟動**。⚠️ **取得商店號後須一併登記伺服器出口 IP 才能切正式環境** | 客戶 |
| **B-11** | 🚫 | **上線（全平台）** | **個資跨境存放的法遵確認**：會員與**捐款人**個資將存於**日本**（VM、Azure SQL、Blob 全在 Japan East）。⚠️ **改日本不等於解除這條**，仍是境外傳輸。個資法的跨境傳輸限制，以及協會與俱樂部間的委託處理約定須載明境外存放 | 法務 |
| ~~B-12~~ | ✅ | ~~轉 DDL~~ | **採弱讀法 ＋ 路由優先順序**（2026-09-20）：`UNIQUE (club_id, slug)` 就夠（SQL Server 把 NULL 當相等），**不加篩選索引、不加觸發器**；網址對應哪一筆由「俱樂部專屬優先、回退共同」解決 | — |
| ~~B-13~~ | ✅ | ~~iOS 上架、深連結~~ | **開發者帳號已到位**（2026-09-20）：D-U-N-S 已有，**Apple Developer 與 Google Play 法人／組織帳號均已註冊**。**Team ID 與 package name 在手，官網的 `.well-known` 兩個關聯檔可以直接產** | — |
| ~~B-14~~ | ✅ | ~~App 全部技術實作~~ | **App 與官方網站共用同一套後端 API**（2026-09-20 拍板，已寫入 App 規劃書 v3.12 §16.1）。不另建獨立服務——領域邏輯與權限只有一份實作，資料也只有一個真實來源。[`docs/19`](docs/19-app-tech-stack.md) 的前提因此成立 | — |
| **B-15** | 🚫 | **`news/camps-events`、`news/match` 兩頁驗收** | **新聞分類「台中磐石國際足球盃」6 篇的歸屬待客戶確認**：`site/src/data/news.json` 的 `category=intcup` 6 篇，mockup 歸在 7.6 營隊與活動（`site/src/pages/zh/news/camps-events/index.html` 本來就有一段 `editorial-note` 明講這是人工決定、待與客戶確認），資料庫種子（S0-6c）則歸在 7.2 比賽報導——**這是內容決策不是程式問題**，兩種歸類都合理，需要客戶定案 | 客戶 |

> 其餘待確認見 [`docs/08`](docs/08-roadmap-decisions.md) §3；藍鯨另有項目見其規劃書 §10；App 另有 9 項見其規劃書 §16.2。
> **行政與法務背景**（法人歸屬、授權、個資委託）不寫進規劃書，記在 [`docs/15`](docs/15-out-of-scope-record.md)。

---

## 1. 階段 0 — 開工前的決策與地基

| ID | 狀態 | 平台 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|---|
| S0-1 | ✅ | 全部 | **技術選型拍板**（2026-09-18）：Nuxt 4 SSR／.NET＋EF Core＋Dapper／**Azure SQL**／**Azure Blob**／Redis／單一 Azure VM。⚠️ **CI 仍未定**（目前無 `.github/`，部署是人工） | [`docs/17`](docs/17-deployment.md) | — |
| S0-2 | ✅ | 全部 | **[`docs/12`](docs/12-database-schema.md) §1.4 五件事全部定案**（陣列→維持關聯表／JSON→原生 `json` 型別／`CalendarEvent`→一般 VIEW，**禁 indexed view**／全文檢索→第一期 `LIKE`／NULL 語意→**弱讀法 ＋ 路由優先順序**） | `docs/12` §1.4 | — |
| S0-3 | ✅ | 全部 | **資料庫綱要 13 項落差已補完**（2026-09-20）：§4 總覽逐張標 `club_id`、14 張 ERD 重繪、§6 明細重寫、§11 唯一鍵與索引、§14 檢核表重算 | `docs/12` 檔頭 | — |
| **S0-3b** | 🔄 | 全部 | **39 張側表欄位已起草**（2026-09-20，[`docs/12c`](docs/12c-i18n-tables.md)，`system-analyst` 逐欄附規劃書行號與信心度）。⚠️ **§5 列出 7 項問題待裁決**，其中三項是實質的欄位缺漏不是寫法之爭，見 S0-3c | 主站 §4、§5.1 | — |
| **S0-3c** | 🔄 | 全部 | **全表核對完成**（2026-09-20，[`docs/12d`](docs/12d-field-audit.md)）：104 張全過一遍，**26 張有缺漏、約 43 筆**，其中 **6 張在 ERD 裡連屬性方塊都沒有**。最嚴重五筆：**`Member` 整表沒有姓名欄位**（K1 行 1233 與 §5.1 行 1500 都明列）；`Venue` 無屬性方塊；**「贊助活動 Activations」整個型別不存在**（前台 9.2 行 486 ＋ 後台 E2 行 1107 都要求）；`MatchGoal`／`MatchCard`／`MatchLineup`／`PlayerSeasonStat` 四張只有關聯線沒有欄位；`ImpactRecord` 三欄 ＋ 多圖子表全缺 | 主站 §3／§4／§5.1 | — |
| **S0-3d** | ✅ | 全部 | **43 筆缺漏補完**（2026-09-20）：第 1 批 37 筆（規劃書已逐欄列出）由 `system-analyst` 補進 [`12a`](docs/12a-database-erd.md)／[`12c`](docs/12c-i18n-tables.md)；第 2 批 6 張整表無欄位定義的（`PlayerSeasonStat`／`MatchGoal`／`MatchCard`／`MatchLineup`／`Venue`／`InvoiceDonationCode`）直接補上屬性方塊。**ERD 實體方塊 105 個，全部配對** | [`docs/12d`](docs/12d-field-audit.md) | — |
| S0-4 | ✅ | 全部 | **`media_asset` 三表已移除、落差第 13 項結案**（2026-09-20）。10 處外鍵全部成為欄位組；查證時另補回三處被誤刪的圖片欄位：`Article.cover_key`、`Banner.image_key`、`ImpactRecord.image_key`（`ImpactRecord` 不在原本列的 10 處內） | 主站 §4.0、`docs/12` 第 13 項 | — |
| **S0-4b** | ✅ | 全部 | **ERD 圖片欄位盤點完成**（2026-09-20，`system-analyst` 逐條回查規劃書、我方逐行核對）：補 `Player.photo_key`／`Staff.photo_key`／`Program.cover_key`／`CalendarCustomEvent.cover_key`／`CharityProgram.cover_key`＋新子表 `CharityProgramImage`／`MemberDraw.cover_key`。`Page` **本來就沒有**（內文插圖存於 `PageBlock.content json`）、`Venue` 刻意不加。表數 103 → **104** | 主站 §4.0、§5.1 | — |
| **S0-4c** | ✅ | 全部 | **兩項規劃書內部矛盾已修**（2026-09-20，主站 **v3.11**）：① §4.12 L3 補上「圖示（自預設圖示集選擇，**不是上傳圖片**）」——確認 ERD 的 `event_type.icon string_64` 是對的 ② §5.1 `MemberDraw` 補上「封面圖」，與 §4.11 K5 一致 | 主站 §4.12／§5.1 | — |
| **S0-4d** | ✅ | 全部 | **`F` 漫畫 ERD 已補**（2026-09-20）：[`12a`](docs/12a-database-erd.md) 新增 **§5.5b F 文化模組 — 漫畫**（`ComicCharacter`／`ComicEpisode`／`ComicPage`），ERD 由 14 張增為 **15 張** | `docs/12` §4.5、主站 §4.6 F1 | — |
| **S0-5b** | ✅ | 慈善 | **7 張缺漏已補**（2026-09-20）：新增 `ReconciliationRun`／`ReconciliationDiscrepancy` 兩張對帳表（表數 23 → **25**）、`DonationInvoice` 補 5 欄（發票抬頭／收據抬頭／年度彙總旗標／作廢原因／經辦人）、`Settlement.remit_method`、`SettlementLine.clawback_reason`、`DonationStore.logo_key`、`DonationProject.cover_key`。[`docs/16`](docs/16-charity-schema.md) 與 [`db/charity-schema.sql`](db/charity-schema.sql) 同步。原始盤點：（2026-09-20，[`docs/16a`](docs/16a-charity-field-audit.md)）。最嚴重：**對帳結果完全沒有資料結構**——規劃書 §4.5 行 403–405 明文「每日與 LINE Pay 交易明細比對／差異列為異常清單／**對帳結果保留供稽核**」，N3 異常佇列（行 517）也把「對帳差異」列為三類待處理之一，但 23 張表裡沒有任何對帳型別（`Settlement` 是分潤結算，不是金流對帳）。另：`DonationInvoice` 缺 4 欄（發票抬頭／收據抬頭／年度彙總旗標／作廢折讓原因與經辦人） | 慈善規劃書 §4.5、§5、§6.3 | — |
| S0-5 | ✅ | 慈善 | **慈善獨立庫綱要已完成**（2026-09-20，[`docs/16`](docs/16-charity-schema.md)）：**23 張表** ＋ 4 張 i18n 側表。🔴 **有 `AuditLog`**（勸募法遵要求，與主站相反）、**沒有 `club_id`／`Member`**、`Charity`／`CharityProgram` 是**唯讀快照不是外鍵** | 慈善規劃書 §9 | — |
| **S0-6a** | ✅ | 全部 | **DDL 已重產**（2026-09-20）：[`db/club-schema.sql`](db/club-schema.sql) **144 張表 ＋ 1 視圖**（2,900 行），`⚠️ 待確認` 由 **64 處降為 8 處**；[`db/charity-schema.sql`](db/charity-schema.sql) 27 張。驗證：CONSTRAINT 無重名、FK 目標全部存在、括號配對、主鍵與叢集鍵逐表配對。**`Club`／`Competition` 的雙語定案走側表**，新增 `clubs_i18n`／`competitions_i18n`。原初版作廢原因：——DDL 是照 ERD 產的，而 ERD 經 S0-3c 查出 26 張有欄位缺漏，缺漏已傳進 DDL（`members` 沒有姓名欄、`player_season_stats` 除兩個外鍵外一個統計欄位都沒有）。**現有 `db/*.sql` 不得拿去建庫。** 原始產出紀錄：（2026-09-20，`backend-engineer` × 2 並行）：[`db/club-schema.sql`](db/club-schema.sql) **150 張表**（104 實體 ＋ 46 側表）＋ 1 視圖、3,504 行；[`db/charity-schema.sql`](db/charity-schema.sql) **27 張表**。硬性約束全數通過（無日誌表、無 media 三表、無 indexed view、`club_id` 50 必填／9 可為空、PK 非叢集、FK 來源與目標全部存在）。⚠️ **主站有 64 處 `⚠️ 待確認`**，大宗是 S0-3b | `docs/12`／`12a`／`12b`／[`16`](docs/16-charity-schema.md) | — |
| S0-6 | ⏸ | 全部 | ⏸ **暫緩**（隨 S0-7）。**建 Azure SQL 正式庫** | 同上 | S0-6b、S0-7 |
| **S0-7c** | ⬜ | 全部 | **CI/CD 規劃已完成**（2026-09-20，[`docs/20`](docs/20-cicd.md)），**workflow 檔待寫**。✅ **兩件已拍板（2026-09-20）**：① **self-hosted runner 裝在正式 VM**（接受「部署執行環境＝正式環境」的取捨，防護鏈見 `docs/20` §4，**第 0 條的 repo 設定是地基**） ③ **資料庫遷移定為 EF Core Migrations**。✅ ② **主站與藍鯨共用一個 Nuxt 映像檔**（`frontend-architect` 已驗證：現有 CSS 全是 custom properties、無 Tailwind，`[data-club]` 屬性選擇器切色。**六條開發紀律見 [`docs/13`](docs/13-blue-whale-site.md) §6**） | [`docs/20`](docs/20-cicd.md) | — |
| **S0-9b** | ✅ | 主站／藍鯨 | **實測通過**（2026-09-20）：同一份 build、兩個 process 帶不同 `NUXT_PUBLIC_SITE_URL`，canonical 與 sitemap 各自正確，偽造 `Host` 也蓋不掉。模組定為 **`@nuxtjs/seo`**，`site.url` 留空不寫進 `nuxt.config.ts`。**單一映像檔方案的最後一個不確定性歸零**。設定寫法與紀律 7、8 見 [`docs/13`](docs/13-blue-whale-site.md) §6 | [`docs/13`](docs/13-blue-whale-site.md) §6 | — |
| **S0-7a** | ✅ | 全部 | **本機骨架完成**（2026-09-20，`deployment-engineer`）。`apps/{web,web-charity,admin,admin-charity,api}` 五個目錄＋各自 `Dockerfile`／`.dockerignore`（**五個，不是八個**——`proxy`／`redis` 用官方映像檔，不需要自建）；`docker-compose.yml`（正式，八服務）＋ `docker-compose.dev.yml`（覆寫：`mssql-dev` 容器取代 Azure SQL、`deploy/Caddyfile.dev` 關閉 TLS）；`deploy/Caddyfile`（自動 TLS，Cloudflare 代理下用 `trusted_proxies`／`client_ip_headers` 取真實 IP，已用 WebFetch 核對 Caddy 官方語法）；`deploy/local-ddl.sh`（`json`→`nvarchar(max)`，已實測命中 club 8 處／charity 2 處，原始 `db/*.sql` 未變動）；`.env.example`、`deploy/dev/{club,charity}.env.example`；`deploy/README.md`。`docker compose -f docker-compose.yml config` 與疊加 `docker-compose.dev.yml` 皆已驗證通過。**順帶修正 `docs/17` §1 Redis healthcheck 的既有錯誤**（exec form 不會展開 `$$REDIS_PASSWORD`，見 [`docs/18`](docs/18-work-errors.md) E-12）。**Azure 資源一個都沒開，未 `docker build`，未動既有 `sqlserver` 容器**。✅ **兩項自選值已確認（2026-09-20）**：`GHCR_OWNER=waiting0201`（使用者確認為本人帳號，與 [`docs/20`](docs/20-cicd.md) §0 的 repo 一致）、**API 定為 .NET 10 LTS**（見 [`docs/17`](docs/17-deployment.md) §0）。**複核時補兩處**：① dev 的 7 個對外埠原本綁 `0.0.0.0`（含 sa 密碼公開的 `14330`），已全部改綁 `127.0.0.1` ② 本機兩庫同 instance、正式是兩個獨立 Azure SQL 的**跨庫 JOIN 陷阱**（本機過、正式爆，且**混用等於混兩個法人的個資**）已寫入 `deploy/README.md` 與 [`docs/14`](docs/14-invariants.md)。`apps/*` 命名與 `docs/20` §3 原規劃不同（`web`／`web-charity`／`admin` 取代 `nuxt-club`／`nuxt-charity`／`admin-web`），**已回填 `docs/20` §3** | [`docs/17`](docs/17-deployment.md) §1、[`docs/20`](docs/20-cicd.md) §3 | — |
| **S0-6b** | ✅ | 全部 | **DDL 本機實測通過**（2026-09-20）：主站 **144 表／380 外鍵／1 視圖**、慈善 **29 表／65 外鍵**，兩份都零錯誤執行完成。⚠️ **驗證時須先把 `json` 換成 `nvarchar(max)`**——本機是 SQL Server 2022，原生 `json` 只有 Azure SQL 與 2025 有。原任務說明：🔵 **DDL 本機實測**：用 `mcr.microsoft.com/mssql/server` 容器實際跑一次兩份 `.sql`。**這是目前唯一能真正驗證 DDL 的方法**——正則只能查括號與名稱，查不出型別錯誤、外鍵順序、`CHECK` 語法。**不需要 Azure** | [`db/`](db/) | — |
| **S0-6c** | ✅ | 主站 | **本機開發資料庫從拋棄式驗證升級為可持續使用**（2026-09-21，`backend-engineer`）。`docker-compose.dev.yml` 的 `mssql-dev` 本來就有具名 volume（`mssql_dev_data`，S0-7a 已建），本次確認重啟不掉資料；灌 DDL 後**逐表比對 S0-6b 的數字，主站 144／380／1、慈善 29／65／0 視圖，完全一致**。**新增種子資料**（[`db/seed/`](db/seed/)）：讀 `site/src/data/*.json`（mockup 的六個 JSON）產生冪等 T-SQL，灌入 `clubs`（台中磐石＋台中藍鯨主檔，**藍鯨只有 Club 一筆**，見下）、`teams`（僅 D1）、`players`（28）、`staff`（8，4 筆連 D1）、`matches`（21）、`articles`（83，`news.json` 全數 83 篇成功歸類到 7.1–7.8）。**逐檔位對照與落差已補進 [`docs/12d-field-audit.md`](docs/12d-field-audit.md)**：`matches` 缺欄位承接 `match_no`、`intcup` 分類人工判斷歸 `match`、`coaches-academy.json` 的青訓教練無法判斷對應 U15／U14／U12（不臆測，不連結 Team）、圖片全數留 `NULL`（JSON 的路徑不是走過縮圖 pipeline 的 Blob key）。**個資判斷**：`site/src/data/*.json` 已在版控內、已是公開 mockup 的一部分，灌資料非新增揭露；種子腳本本身（`generate-club-seed-sql.py`）**不重複硬編碼任何姓名**，只放讀取轉換邏輯，產出的 `.sql`（含真實姓名）**不進版控**。**藍鯨只建 `clubs` 主檔**（品牌色、`is_collecting_subject=0` 代收代付等已定案事實），`domain` 用明確標示佔位的 `bw-domain-pending.invalid`（B-4 未解除前無法留白，NOT NULL UNIQUE）；不建球員／教練／賽程（客戶尚未提供，B-5／B-6，不臆造）。**踩坑一則見 [`docs/18`](docs/18-work-errors.md) E-15**（`locales` 主檔未種導致 `clubs_i18n` 部分失敗又被冪等判斷誤判為已完成，已修正為每個防呆插入區塊包 `BEGIN TRANSACTION`）。**未動 `apps/web`／`site/`，未 commit，未開 Azure 資源，未碰既有 `sqlserver` 容器** | [`db/seed/`](db/seed/README.md)、[`deploy/README.md`](deploy/README.md) | — |
| **S0-6d** | ✅ | 全部 | **本機開發資料庫合併進既有的 `sqlserver` 容器**（2026-09-21，`deployment-engineer`；🔴 **使用者拍板：不要另外再開一個 instance**）。原本 `docker-compose.dev.yml` 另起一個 `mssql-dev`（埠 14330、具名 volume `mssql_dev_data`），刻意避開使用者既有的 `sqlserver` 容器；現改為兩個開發庫（`tcrfc_club_dev`／`tcrfc_charity_dev`）直接建進那個既有容器，`mssql-dev` 服務退場，`api` 改走 `host.docker.internal:1433`（補 `extra_hosts` 為 Linux 相容）。🔴 **防呆的性質跟著改寫，不是刪掉**：風險從「接錯容器」變成「動到錯的資料庫」——`deploy/local-ddl.sh` 的目標資料庫寫死白名單（`assert_allowed_database`，唯一一條 sqlcmd 路徑進去第一行就檢查）、`db/seed/apply-seed.sh` 寫死單一目標、只 `IF DB_ID(...) IS NULL CREATE DATABASE` 不 DROP／ALTER、執行前先印目標、容器名用 `^/name$` 錨定比對；已用 `master` 與別專案的 `20Skin` 實測會被拒絕。容器名可用 `LOCAL_MSSQL_CONTAINER` 指定（預設 `sqlserver`）。**驗證**：主站 144 表／380 外鍵／1 視圖、慈善 29／65／0 與 S0-6b 完全一致；種子 `clubs` 2／`players` 28／`staff` 8／`articles` 83／`matches` 21；**`apps/api` 測試 30 項全綠**（對合併後的 instance，主 session 另行覆核過一次）；`api` **容器**實連 `host.docker.internal` 通、`/readyz` 回 `charity_db:"ok"`；操作前後 `sys.databases` 逐一比對，**25→27 只多兩筆，使用者另一個專案約 25 個資料庫一筆未動**。⚠️ **兩個取捨要知道**：① 既有容器**沒有掛 volume**，`docker rm sqlserver` 會讓兩個開發庫連同別專案的資料庫一起消失（復原＝重跑 DDL ＋ 種子，指令在 [`deploy/README.md`](deploy/README.md)）②它綁 `0.0.0.0:1433` 對同網段開放，與本專案原本一律綁 `127.0.0.1` 的做法不同——**不動它本身是刻意的**（改埠或加 volume 都要重建容器，會清掉別專案的資料）。舊的 `tcrfc-mssql-dev-1` 容器與 `mssql_dev_data` volume **刻意保留未刪**，回收指令見 `deploy/README.md`，何時清由使用者決定 | [`deploy/README.md`](deploy/README.md)、[`docs/17`](docs/17-deployment.md) §6 | S0-6c |
| **S0-7b** | ✅ | 主站／藍鯨 | **`apps/api/` .NET 10 唯讀讀取 API 骨架已建立**（2026-09-21，`backend-engineer`）。範圍：球員、教練與職員、新聞、賽程與賽果、俱樂部主檔五組 GET 端點，讓 `apps/web` 能打真實資料庫（承接 S0-9c 的決定）。**不含**寫入、登入權限、商店金流、後台任何模組、慈善平台。技術：唯讀查詢一律 **Dapper**（EF Core 留給日後寫入功能）。**`club_id` 強制機制走型別系統**：`ClubScope`（`readonly struct`，建構子 `internal`）只能由 `IClubResolver` 建立，repository 方法簽章要求 `ClubScope` 不是 `Guid`／`string`，編譯期就擋掉「忘記驗證就查資料庫」；9 張可為空 `club_id` 表的「俱樂部專屬優先、回退共同」集中在 `Data/ClubOrSharedSql.cs` 單一來源。**快取接縫已留（`Caching/IQueryCache.cs`），本次注入 no-op**——本次任務沒有後台可觸發 write-invalidate，接 Redis 等於加一層永不失效的快取，日後只需換 DI 註冊不改端點。**雙語**：對外 `lang=zh/en`、資料庫存 `zh-Hant/en`，回退規則「請求語系非空白用它，否則用 `zh-Hant`，兩者皆無回傳 `null`」逐欄位判斷，已用種子資料真實缺漏驗證（`matches_i18n`／`competitions_i18n` 完全無英文列）。**受限欄位**：查過 [`docs/12b`](docs/12b-database-tables.md) §8，本次碰到的六張表（`players`／`staff`／`articles`／`matches`／`clubs`／`teams`）都不在清單內，⛔ 全程無 `SELECT *`。**已驗收**：`dotnet build` 成功；`docker build` 用 `apps/api/Dockerfile` 實際建置成功；容器實跑打本機 `mssql-dev`，`/readyz` 與 Docker `HEALTHCHECK` 皆為 healthy；球員 28／新聞 83／賽事 21 筆數與種子資料一致；**`club_id` 繞不過去已用三種方式證明**（`bw` 路由讀 `tcrfc` 專屬資料一律 0 筆或 404、跨俱樂部讀已知 slug 的文章詳情回 404、不存在的俱樂部代碼回 404）；SQL injection 嘗試（`team` 參數帶 `DROP TABLE`）安全無事。**開發過程踩兩坑並記錄**：`InvariantGlobalization=true` 讓 `Microsoft.Data.SqlClient` 連線直接炸（[`docs/18`](docs/18-work-errors.md) E-19）；Dapper 的 record 具現化對 `DateOnly`／SELECT 欄位數要求比預期嚴格（E-20）。**未動 `apps/web`／`site/`／`db/seed/`／`db/*.sql`，未開 Azure 資源，未 commit，未碰既有 `sqlserver` 容器** | [`apps/api/README.md`](apps/api/README.md)、[`docs/17`](docs/17-deployment.md) §11 | S0-6c、S0-7a、S0-9c |
| **S0-7d** | ✅ | 主站／藍鯨 | **補齊 S0-7b 刻意縮減的三個缺口**（2026-09-21，`backend-engineer`）：① `/readyz` 加慈善庫與 Redis 檢查——`club_db` 必檢查失敗即 not ready（既有行為未改）；`charity_db` 有設定 `CHARITY_SQL_CONNECTION_STRING` 才檢查，沒設定 `not_configured` 不影響 ready，設定了連不上算 `fail` 導致 503；`redis` 有註冊才檢查，連不上只標示 `degraded`，**兩種情況都不影響 ready**（依 [`docs/17`](docs/17-deployment.md) §4「連線失敗算警告不算失敗」）；慈善庫檢查只開連線查 `SELECT 1`，不建立任何 repository 或常駐連線工廠。② **`Caching/IQueryCache.cs` 接上真正的 `RedisQueryCache`**：`REDIS_HOST` 有設定才注入，逐條落實 §4「實作五條硬規則」（fail-open／版本號失效／TTL 兜底／single-flight／五類禁用不注入）；key 格式 `v{ver}:{club}:{locale}:{entity}:{qualifier}`；新增 `InvalidateAsync(entity, club)` 給日後後台寫入層呼叫；TTL 預設 **300 秒**（`QUERY_CACHE_TTL_SECONDS` 可覆寫，理由：目前沒有任何寫入層會呼叫失效，TTL 是唯一止血）。當時**目前唯一接線點是 `ClubResolver`**——刻意沒有修改任何 `Features/*` repository 去接快取（任務範圍認定接快取＝改建構子簽章＝修改 repository），留給下一個任務判斷。③ **新增 `Tcrfc.Api.Tests`**（xUnit + `WebApplicationFactory<Program>`，14 個測試，涵蓋 `club_id` 繞不過去、語系逐欄位回退、分頁正規化、SQL injection、快取 fail-open），**資料庫不可用時測試回報 `Failed` 附修復指令，不是悄悄略過**；`Tcrfc.Api.csproj` 明確 `<Compile Remove>` 排除測試子專案，`.dockerignore` 也排除，`docker build -t x apps/api` 不受影響（已實測）。**驗證**：`dotnet build`／`dotnet test`（14/14 通過）／`docker build` 全部實跑成功；容器層級用 `docker run` 起已建好的映像檔＋`brew install redis` 的真實 `redis-server`（此沙盒環境 `docker pull redis:8-alpine` 逾 30 分鐘未完成，改走 brew 驗證等效行為），驗證三種情境：Redis 從未連上（啟動即 fail-open，`/readyz` degraded，請求仍 200）、Redis 正常運作（key／TTL／版本號失效機制皆符合設計，`redis-cli` 直接核對）、Redis 執行中途掛掉（同一容器不重啟，仍 fail-open）。

**續作（同日，使用者拍板）**：使用者澄清「端點與 repository 不得修改」的本意是「接縫換 no-op↔Redis 不需要動它們」，不是「一行都不能碰」——加一個 `IQueryCache` 建構子參數、對外契約不變，是接縫本來就預期的用法。**因此把 `ClubsRepository`／`PlayersRepository`／`StaffRepository`／`ArticlesRepository`／`MatchesRepository` 五個 `Features/*` repository 全部接上快取**：entity 分別是 `clubs-list`（club 維度 `_shared`）／`club-detail`／`players`／`staff`／`articles`／`article-detail`／`schedule`；qualifier 逐一涵蓋 `team`／`category`／`season`／`status`／`page`／`pageSize`／`slug`（每個會改變結果的參數都編進去，缺一個就會讓換了篩選條件的請求拿到上一次的快取結果）。**新增一條通用規則**：`GetOrCreateAsync` 對 `factory` 回傳 `null` 的結果不寫入快取（`ClubDto?`／`ArticleDetailDto?` 適用），查無資料的 404 不會被快取住；空清單（`TotalCount=0`）不受影響、正常快取。🔴 **文件明講的語意後果**：`articles`／`article-detail` 因為「排程發布時間到」沒有任何寫入事件可觸發失效，排程發布的實際生效時間完全依賴 TTL，最多延後一個 TTL 週期（預設 300 秒）——不是 bug，是已知取捨，寫進 `apps/api/README.md`「排程發布與快取的語意後果」供日後後台排程發布功能對照。**測試從 14 增為 30 項**：新增 `CacheBehaviorTests`（真的啟動 `redis-server` 子行程驗證 key／qualifier／club 與 locale 隔離／TTL／404 不快取），`CacheFailOpenTests` 擴大為涵蓋全部五組端點＋文章單篇。**驗證**：`dotnet build`／`dotnet test`（30/30 通過）／`docker build -t x apps/api` 全部實跑成功；容器層級用 `redis-cli` 逐一核對五組 entity 的 key 形狀（例如 `schedule` 的 `v0:tcrfc:zh-Hant:schedule::::1:5`，team／season／status 三段皆空、page=1、pageSize=5）、`tcrfc`／`bw` 各自獨立的 key 與內容、TTL（282 秒，在預設 300 秒之內）、404 不寫入 key、版本號遞增只影響對應俱樂部的 key；Redis 執行中途被 kill（同一容器不重啟）後五組端點與 `/readyz` 均正確 fail-open。**明確不做**：任何寫入、登入權限、商店金流、後台任何模組、慈善平台業務功能、開任何 Azure 資源、新增前台無消費者的讀取端點（FAQ／PageBlock／Event／贊助夥伴）。**未 commit** | [`apps/api/README.md`](apps/api/README.md)、[`docs/17`](docs/17-deployment.md) §11、[`docs/14`](docs/14-invariants.md) | S0-7b |
| **S0-7e** | ✅ | 主站／藍鯨 | **後台新聞（B2）寫入端點**（2026-09-22，`backend-engineer`）。🔴 **範圍刻意只做新聞**——`apps/admin` 目前只有新聞的列表頁與編輯頁是真畫面，其餘 13 個模組是佔位頁；做沒有畫面可驗的端點等於沒被驗過。同時建立寫入端通則供其他模組照抄。新增 `GET/POST /api/v1/admin/{club}/news`、`GET/PUT/DELETE .../{id}`、`POST .../{id}/publish`、`.../{id}/schedule`，**以及後台專用的讀取端點**（回傳全部狀態含未發布——`apps/admin` 當初刻意不接 API 就是因為公開 API 只回已發布）。**EF Core 一次性 handoff 完成**（[`docs/20`](docs/20-cicd.md) §5）：`scaffold` 產出 138 個實體 ＋ `InitialBaseline` migration **`Up()`／`Down()` 手動清空、只寫 `__EFMigrationsHistory`，沒有對資料庫跑任何實際 DDL**（實測 `articles` 83 筆不變）。**讀取維持 Dapper 未改寫**（選型是兩者並用不是二選一）。踩到 `programs` 表被 scaffold 成 `Program` 與 ASP.NET Core 頂層陳述式撞名，已改名 `TrainingProgram`（重新 scaffold 要記得重做，README 已記）。**驗收**：`dotnet build` 0 警告、`dotnet test` **48/48**（30 既有＋18 新增）連跑 5 次穩定、`docker build` 過、完整生命週期 curl（建立→改→排程→發布→刪除）每步查庫、既有五組 GET 無回歸（磐石 28／8／83／21、藍鯨 28／21） | [`apps/api/README.md`](apps/api/README.md) | S0-7d |
| **S0-7f** | ✅ | 主站／藍鯨 | 🔴 **寫入端點不得可部署**（2026-09-22 派工時定下的硬性約束）。專案還沒有登入與權限，**沒有把關的寫入端點一旦部署出去就是任何人都能改資料庫**。實作 `Security/DevWriteGate.cs`：要 `ASPNETCORE_ENVIRONMENT=Development` **且** `ENABLE_UNSAFE_DEV_WRITES=true` **同時成立**才註冊路由；關閉時**整條路由不存在（404）而不是 403**，不洩漏「這裡有寫入端點」。**三層實測**（我獨立複驗過）：① Development 未設旗標 → GET／POST／PUT／DELETE／publish **全部 404 且回應體為空** ② Development ＋ 旗標 → 200 ③ **Production ＋ 旗標=true → 仍是 404**（正式部署一定會踩到的那一層）。`created_by`／`updated_by` 由 `X-Dev-Operator-Id` 標頭解析並驗證存在於 `admin_users`，**明確不是身分驗證**且註解寫明。⛔ **明確禁止它自己實作任何認證、權杖或密碼雜湊**——半套的認證比沒有認證更危險 | [`apps/api/README.md`](apps/api/README.md) | S0-7e |
| **S0-7g** | ⬜ | 主站／藍鯨 | 🔴 **待決：排程發布「誰把 `scheduled` 改成 `published`」規劃書沒有定義**。已用 curl 實測證實：排程一篇文章到未來時間，**即使時間過了，只要沒人呼叫 `/publish`，狀態仍是 `scheduled`，公開 API 永遠 404**——既有唯讀 repository 的 WHERE 要求 `status='published'` 字面成立，光是 `published_at` 到期不夠。可能的解法（背景排程器／定時 Function／讀取時視為已發布）各有代價，**規劃書沒寫，開發端不得自行發明**。⚠️ 這條不解，「排程發布」這個功能實際上是壞的 | 主站規劃書 B2 | 客戶／規格決定 |
| **S0-7h** | ⬜ | 主站／藍鯨 | 🟡 **待確認：兩項實作時的合理猜測，非規劃書明文**。① **置頂精選「限 3」判斷成逐俱樂部算**（不是全站）——規劃書沒明講，但與其他 `club_id` 維度規則一致 ② **狀態轉換規則**（`publish`／`schedule` 只接受從 `draft`／`scheduled` 出發）。兩項都已實作並測試，**但要確認是不是客戶要的**。另見 [`docs/12d`](docs/12d-field-audit.md) §11：`articles` 的圖片欄位組不完整（缺 `cover_alt`／`cover_width`／`cover_height`），**缺 Alt 是無障礙問題、缺寬高會造成 CLS** | 主站規劃書 B2、[`docs/12d`](docs/12d-field-audit.md) §11 | 客戶 |
| **S0-7i** | ✅ | 主站／藍鯨 | **`apps/admin` 的新聞模組已接上真實 API**（2026-09-22，`frontend-architect`）。刪掉 `data/news.ts`／`newsStore.ts` 兩份前端假資料，改打 `apps/api` 的後台端點。API client 分三層：`api/http.ts`（傳輸與錯誤分類）→ `api/gate.ts`（偵測寫入開關是否開啟）→ `api/adminNews.ts`（六支端點＋DTO↔畫面型別雙向轉換）。**六種狀態各有對應畫面行為**，不是通通跳同一個錯誤：409 並行衝突（給「重新載入」／「留在這裡」兩個選擇，**不默默覆蓋**）、409 slug 重複（錯誤指回欄位不清空表單）、400 驗證、404（整頁替換＋返回列表）、**API 連不上 vs 寫入開關關閉兩種不同文案**、載入中與空狀態（再分「本來就沒資料」與「篩選查無結果」）。**`coverImageAlt`／`noIndex`／`canonicalUrl` 三個存不進去的欄位整個拿掉**，不留假輸入框；封面改純文字接 `coverKey` 並明講上傳尚未開放（`S0-8`）。**驗收**：用 CDP 驅動真實瀏覽器跑完整生命週期（建立→草稿→改→排程→發布→刪除），每步查資料庫；排程後公開端點回 404、發布後回 200；跑完 `articles` 回到 **83 筆**。`apps/api/Program.cs` 只改 CORS 開發預設那一行加 `localhost:5174`（後台 vite 埠），**API 測試 48/48 無回歸，我另外實測 preflight 放行 5174、非白名單來源無 allow-origin 標頭** | [`apps/admin/README.md`](apps/admin/README.md) | S0-7f |
| **S0-9e** | ⬜ | 主站／藍鯨 | 🔴 **新聞詳情頁沒有逐篇網址**——`apps/web/app/pages/zh/news/article.vue` 的 `ARTICLE_SLUG` 是**寫死的**（固定示範 `2026-05-24-match-001` 那一篇），`NewsCard.vue` 的 `href` 也是靜態的 `/zh/news/article/`。**83 篇文章的卡片全部連到同一頁、顯示同一篇**。這是 mockup 時代的刻意簡化（原 mockup 的 template-banner 就寫「正式站將由 CMS 為每篇文章產生獨立網址」），搬到 Nuxt 時原樣保留——**但這件事從來沒有被記進 `STATUS.md` 或任何 `docs/`**，是知道卻沒被追蹤的工作。⚠️ **現在開始擋事情了**：① 後台的「檢視」／「預覽」連到 `/zh/news/{slug}/`，那個路由不存在 ② 每篇文章沒有自己的網址 → **SEO 的 canonical、Schema `Article`、`llms.txt` 全部無從輸出** ③ App 深連結沒有目標。🔵 **現在做得起來了**：`apps/api` 已有 `GET /api/v1/{club}/news/{slug}`，改成動態 `[slug].vue` 路由即可 | `apps/web/app/pages/zh/news/article.vue` 檔頭註解、主站規劃書 §7 | S0-7i |
| S0-7 | ⏸ | 全部 | ⏸ **暫緩（2026-09-20 使用者指示：Azure 資源先不開）**。**專案骨架與部署管線**：Docker Compose **八個容器**（反向代理／`nuxt-tcrfc`／`nuxt-bw`／`nuxt-charity`／`admin-web`／`admin-charity`／`api`／`redis`）、VNet ＋ 服務端點、靜態 Public IP、NSG。🔵 **`redis` 的 compose 定義已寫好可直接抄**（[`docs/17`](docs/17-deployment.md) §1「Redis 怎麼裝」）——⛔ **絕對不要寫 `ports:`** | [`docs/17`](docs/17-deployment.md) §1–§2 | — |
| **S0-9c** | ✅ | 主站／藍鯨 | 🔴 **2026-09-21 使用者拍板：先建本機資料庫，前台接真實資料，不做讀 JSON 的 Nitro 過渡層。** 原始待決：mockup 的 `site/src/data/*.json`（`players`／`news`／`schedule`／`staff`／`coaches-d1`／`coaches-academy`）是 build 時讀進靜態 HTML；正式站是 .NET API ＋ Azure SQL，Nuxt 版要走 `useFetch` 打真實 API 還是先用 Nitro server route 包一層讀 JSON 的過渡層？**選了前者**——理由是過渡層是一次性投入但之後要整個丟掉重寫，而 S0-6c 已把本機資料庫做成可持續使用的環境（非拋棄式），直接對真實綱要開發可以及早暴露欄位落差（已發現數項，見 `docs/12d`），比事後才發現划算。**本機庫與種子資料已備妥**（S0-6c），Nuxt 頁面實際改走 `useFetch` 待 API 專案骨架建立（S0-7b／S0-9）才能真的接上，本列只記決策本身 | [`docs/17`](docs/17-deployment.md) §6、[`db/seed/`](db/seed/README.md) | — |
| **S0-9d** | ✅ | 主站／藍鯨 | **三項前置工具完成**（2026-09-21，`frontend-architect`），全部收在 `site/tools/`（新增 `site/package.json` 鎖 `parse5` 版本，唯一相依）：① `check-wellformed.mjs`——對 `site/dist` 實測：**0 個標籤未閉合、0 個孤立結束標籤**（`zh/index.html` 那處歷史問題仍修復有效，無回歸），`<main>` 內 `<style>`／`<script>` 違規 84 筆（67＋17，66 個檔案，**已知待辦不是新 bug**，S0-9 正式搬遷時處理）。🔵 **順帶驗證 `docs/13` §6 的數字**：頁數口徑「65/80 頁有 `<style>`、17 頁 `<script>` 去重 11 種」仍正確，但**標籤實例數是 67 不是 65**——`membership-benefits.html` 共用片段自帶一個 `<style>`，被 2 頁 include 各多算一次。② `codemod-root.mjs`——實查 `build.mjs` 確認頁面層只有 `{{ROOT}}`（1035 處）與 `{{>partial}}`（2 處）兩種 token，全域文字替換天生涵蓋 CSS 註解／`<style>`／`<script>` 內的 token（已修過切片時漏抄的那類坑），拒絕寫回 `site/src/` 的安全防呆已測試。③ `compare-dom.mjs`——比對 `<main id="main">` 內容，正規化涵蓋空白摺疊／屬性排序／`data-v-*` 排除／註解節點排除；`site/dist` 對自己 80 頁零差異、人工製造文字差異與屬性差異均正確抓到、整頁重新排版不誤判。**尚無 Nuxt 專案可實測 URL 模式**，但檔案／目錄模式與比對邏輯已驗證。用法、正規化規則、搬頁順序見 [`site/tools/README.md`](site/tools/README.md)。踩過的坑（parse5 `onParseError` 不會回報孤立標籤／標籤未閉合）記在 [`docs/18`](docs/18-work-errors.md) E-14 | `docs/13` §6、[`docs/14`](docs/14-invariants.md)、[`docs/18`](docs/18-work-errors.md) E-14、`site/tools/README.md` | — |
| **S0-9a** | ✅ | 主站／藍鯨 | **`apps/web/` Nuxt 4 SSR 骨架已建立**（2026-09-21，`frontend-architect`）。標準 Nuxt 4 專案（`app/`、`shared/`、`server/`、`public/`），`@nuxtjs/seo` ＋ `@nuxt/eslint`。**品牌切換機制落地**：`runtimeConfig.public.club` ＋ `app.vue` 的 `useHead({ htmlAttrs: { 'data-club' } })`；`tcrfc.css`（69KB）原封搬進 `public/`（**SHA-256 與 `site/` 原檔一致**）；藍鯨 7 個色票變數放獨立的 `club-bw.css` 覆寫檔，未動 `tcrfc.css` 本體。**單元開關單一真實來源** `isUnitEnabledForClub()`（`shared/utils/units.ts`）四個呼叫點全部接線：route middleware、導覽選單（`SiteHeader.vue`／`SiteFooter.vue`）、sitemap 資料端點（`server/api/__sitemap__/urls.ts`）、`llms.txt`／`llms-en.txt`。`site/src/partials/{shell,header,footer}.html` 轉成 `app/layouts/default.vue` ＋ `SiteHeader.vue`／`SiteFooter.vue`，DOM／class 未動；`site/src/pages/{index,zh/index}.html` 轉成驗證頁，`<style>`／`<script>` 全部移出樣板區塊（無 `scoped`）。`_headers`／`_redirects`／`robots.txt` 改用 `routeRules` ＋ `nuxt-robots`（`disallow: ['/']`，**noindex 仍在**）。**已實測**：`npm run build` 成功；同一份 build、兩個 process 帶不同 `NUXT_PUBLIC_CLUB`／`NUXT_PUBLIC_SITE_URL`，`data-club`／canonical／favicon／theme-color／og:image 各自正確，偽造 `Host` header 蓋不掉 canonical，`X-Robots-Tag: noindex` 存在；藍鯨的 nav／llms.txt／sitemap 資料端點正確排除 06／11。🟡 **已知缺口**：`/sitemap.xml` 本身輸出仍是空的（資料端點正確，模組沒接進 XML，見 [`docs/18`](docs/18-work-errors.md) E-18）；藍鯨 favicon／OG 圖是隊徽點陣主檔裁切縮放的暫代品，非正式設計稿；`llms.txt` 只到事實骨架。**三個坑記入 [`docs/18`](docs/18-work-errors.md) E-16／E-17／E-18**（Vue SFC 註解裡的字面標籤把 build 拖壞、`nuxt-seo-utils` 蓋掉 `<html lang>`、`@nuxtjs/sitemap` 動態來源沒被偵測到）。**只做骨架，80 頁完整搬遷未動**，見 S0-9 | [`apps/web/README.md`](apps/web/README.md)、`docs/13` §6、[`docs/18`](docs/18-work-errors.md) E-16–E-18 | — |
| **S0-9** | ✅ | 主站／藍鯨／慈善 | **78 頁完整搬遷完成**（2026-09-21，`frontend-architect`）：**45 頁靜態＋33 頁資料驅動**，加上 S0-9a 骨架期已完成的 2 頁（`/`、`zh/`），**共 80 頁與 `site/dist` 對齊**。**共用元件化**：`MembershipBenefits.vue`（`components/content/` 子目錄元件）、6 個 news 頁共用同一份元件、`useYearChips` composable 統一年份篩選邏輯。**API 資料走 Nitro 同源代理**（`server/api/` 轉打 `apps/api`，前端不直連後端網域）。**比對關卡**：`site/tools/compare-dom.mjs` 逐頁正規化 diff `<main>` 內容零差異，**必然差異擴充為六類**（見 [`docs/14`](docs/14-invariants.md)：新增 style 屬性結尾分號、`v-model` 顯式 `selected`／`value`）。搬遷過程踩三坑，記入 [`docs/18`](docs/18-work-errors.md) `E-22`–`E-24`（子目錄元件標籤前綴、`nuxt prepare` 順序、`:style="undefined"` 印出空字串）。**`verify.mjs` 的六項檢查已移植為 Nuxt 專案的 lint／test**（token 殘留／h1 數量／缺 alt／寫死色碼／站內斷鏈／`.pending` 統計）；`noindex`、`_headers`、`_redirects` 改用 `routeRules` ＋ response header。🟡 **已知缺口見本表下方備註**（`/sitemap.xml`、schedule 頁 `SportsEvent` JSON-LD、部分種子欄位為 `NULL`）。S0-9a 骨架期的踩坑記錄（`E-16`–`E-18`）與本次三坑合計六筆，同屬「前台跟 mockup 必須一模一樣」這條驗收線。🔴 **關卡實跑數字（2026-09-21 收尾更新，`backend-engineer`）**：`compare-dom.mjs --expected dist --actual <Nuxt>` 全站 80 頁，**77 頁 `<main>` 零差異**；其餘 3 頁（`zh/news`／`zh/news/match`／`zh/news/camps-events`）可歸因於既有的 `news.json` intcup 分類待客戶確認，非搬遷差異。原本另外失敗的 `zh/schedule`（缺 `match_no`）與 `/`（誤跟 `/zh/` 比內容）兩頁已隨場次編號同步鏈解決，見「已完成」段落 | [`docs/17`](docs/17-deployment.md)、[`docs/14`](docs/14-invariants.md)、`docs/13` §6、[`apps/web/README.md`](apps/web/README.md) | S0-7a、S0-9a、S0-9c、S0-9d |
| **S0-11** | ✅ | App | **App 客戶端技術選型拍板**（2026-09-20）：原生 Swift／SwiftUI ＋ Kotlin／Compose；`shared/` 契約目錄、權杖機制、推播直送、可見度量測、設定下發三層、CI 管線全部定案。⚠️ **CI 的 macOS runner 承擔方式仍未定** | [`docs/19`](docs/19-app-tech-stack.md) | — |
| **S0-10** | ⬜ | 全部 | **上線前壓測**：Azure SQL 先開 Basic（5 DTU／2 GB）。DTU 不足只是變慢且可線上升級，但 **2 GB 是硬上限、寫滿即寫入失敗**——**儲存空間告警須設在 1.5 GB** | [`docs/17`](docs/17-deployment.md) §6 | S0-6 |
| S0-8 | ⬜ | 全部 | ✅ **HEIC 已定案（2026-09-20）：前端瀏覽器轉 JPEG 再送**，伺服器端仍須擋解不開的檔（見 [`docs/17`](docs/17-deployment.md) §6）。**圖片上傳共用元件**：選檔 → JS 預覽 → 表單儲存才寫 blob（**Azure Blob Storage**）；換圖成功才刪舊物件（**主檔＋衍生檔一起刪**）；前後端雙重驗證（伺服器端以檔頭判格式）；**伺服器端一律重新編碼**（轉正 → 長邊 > 2560px 等比縮小 → WebP → 去 EXIF 含 GPS，**不留原始檔**），一次產完 **1280／640／320 ＋ 160px 方形縮圖**，鍵由主鍵推導（**ImageSharp**——年營收 100 萬美元以下適用 Apache 2.0，本案符合） | **主站 §4.0 後台圖片上傳通則（v3.9）** | S0-7 |
| **S0-12** | ✅ | 全部 | **後台介面版面決策與可點 mockup**。第一階段（2026-09-21，`visual-design-architect`）：[`docs/21-admin-ui.md`](docs/21-admin-ui.md)（v1 535 行，**現已被 S0-12b 改寫為 v2／901 行**）產出側欄結構（14 個一級模組、6 組視覺分組、手風琴子選單）、列表頁與編輯頁標準型、狀態四態、站台切換器行為（**只換徽章不換主色**）、~~配色（Element Plus 預設中性藍 `#409EFF`）~~ 🔴 **已於同日 S0-12b 推翻，改深色**、🔴 **完整響應式**（使用者拍板推翻原建議的「只做關鍵流程」）、圖片上傳元件畫面。第二階段（同日，`frontend-architect`）：**`apps/admin/` 建成可以在瀏覽器裡點的 Vue 3＋Vite＋TypeScript＋Element Plus SPA**——外殼（側欄／頂欄／站台切換器／使用者選單，14 個模組全部可點、未實作的走明確的「尚未建置」佔位頁不是死連結）＋儀表板＋「新聞與故事」列表頁與編輯頁，資料全部是前端假資料（⛔ 刻意不接 `apps/api`，那支 API 唯讀只回已發布內容，接了驗證不到草稿／排程發布／已停用三態）。**新增禁用詞掃描腳本**（`scripts/check-forbidden-terms.mjs`，掛進 `npm run lint`）：只解析 `.vue` 的 `<template>` 區塊、丟棄 `{{ }}` 內插值與動態綁定後比對模組代號／權限碼／隊別代號／英文技術詞清單，用「暫時塞違規字串確認腳本抓得到、改回來確認會過」驗證過腳本真的有作用。**響應式三斷點皆用無頭 Chrome + CDP（`Emulation.setDeviceMetricsOverride`）逐一實測**：桌面側欄收合 240/64px、平板強制收合可展開＋表格隱藏次要欄位、手機側欄變 `el-drawer`＋列表變卡片式＋雙語欄位變 `el-tabs`。過程中發現並修正兩個問題：① `nginx-spa.conf` 的 `X-Robots-Tag` 原本只寫在 server 層級，被每個 location 自己的 `add_header` 蓋掉、`noindex` 沒有真的送出（`docker build`＋`docker run`＋`curl -I` 實測發現，已修正並重新驗證），記 [`docs/18`](docs/18-work-errors.md) E-29 ② `structuredClone()` 直接對 Vue reactive 來源的物件呼叫會丟 `DataCloneError` 導致編輯頁空白（改用 `toRaw()`＋`shallowRef`），記 [`docs/18`](docs/18-work-errors.md) E-30。`docker build -t x apps/admin` 已實測成功，容器內 `curl -I`／`/healthz` 皆正常。**不做**：角色與權限頁、其餘 12 個模組的真實功能、任何後端串接 | [`docs/21`](docs/21-admin-ui.md)、[`docs/03`](docs/03-admin-spec.md)、[`apps/admin/README.md`](apps/admin/README.md) | — |
| **S0-12b** | ✅ | 全部 | **後台改深色 ＋ 版面骨架重新設計**（2026-09-21，`visual-design-architect`）。🔴 **使用者拍板兩件事，推翻 S0-12 第一階段的視覺決定**：① 配色改**深色後台**（不再是 Element Plus 預設淺底＋`#409EFF`）② **版面骨架重新設計**（不是既有骨架上精修）。[`docs/21`](docs/21-admin-ui.md) 全文改寫 535→**901 行**：§1 正式比較「側欄＋頂欄」vs「圖示軌＋內容面板」兩種骨架後決定維持側欄但**頂欄拆系統列／頁面列兩層**、側欄選中態改 accent bar；§7 整節重寫成**四層背景色階**（`canvas`／`surface`／`surface-2`／`overlay`）＋文字四階＋primary 拆兩層（深色底下「文字要夠亮」與「實心按鈕白字要夠暗的底」是相反需求，`#409EFF` 配白字只有 2.78:1）；§4 四態與語意色全部為深色底重算（**不照搬** Element Plus 淺色預設值）；§3 陰影分層改**色階分層**（深色底陰影實質失效）；§6「這裡管理的是」改頁面列獨立第二行，**從結構解決** S0-12 遺留的手機換行問題；新增 §10 空狀態／載入中／**錯誤狀態**（v1 沒有）。實作走 **Element Plus 2.9 官方深色模式**（`html.dark` ＋ 覆寫 CSS variable），唯一要自己接線的是官方只有三層、本檔需要四層的 `surface-2`。**被推翻的 v1 決定逐條留在 §13 落差記錄**，不是悄悄刪掉。🔴 **交付後複驗抓到色票表 27 組裡 2 組數字錯**，其中一組牽出真缺陷（`--admin-text-tertiary` 兼任 placeholder，對最亮的 `overlay` 層只有 4.35:1 過不了 AA，而原表只驗了兩層）——已修正為 `#9299A8`（四層全過 AA），記 [`docs/18`](docs/18-work-errors.md) **E-31** | [`docs/21`](docs/21-admin-ui.md) §13、[`docs/18`](docs/18-work-errors.md) E-31 | S0-12 |
| **S0-12c** | ✅ | 全部 | **`apps/admin/` 已依 [`docs/21`](docs/21-admin-ui.md) v2 重做**（2026-09-21，`frontend-architect`）。深色 token 整份換掉（`main.ts` 載入 Element Plus 官方深色變數表＋掛 `class="dark"`，`index.html` 的 `<html>` 直接帶 class 避免白底閃爍）；**結構性修改**：頂欄拆系統列／頁面列兩層（手機合併單列）、新增 `PageHeader.vue` 承接標題＋「這裡管理的是」固定兩行、側欄選中態改 3px accent bar（`box-shadow: inset`）、`StatusTag.vue` 改自訂 class 帶 §4.2 精確色票（不再靠 `el-tag` 的 `type`，那是為淺色設計的）、`ImageUploader.vue` 加 `variant` prop 與**中性看片台**、抽屜頂部加站台切換器。**四項驗收實跑**：`npm run build` 過、`npm run lint` 過、三斷點以 CDP `Emulation.setDeviceMetricsOverride` 實測（390／768／1440 **scrollWidth 均等於 viewport，無水平溢出**）、console 零錯誤；`nginx-spa.conf` 未改動但重驗 `X-Robots-Tag: noindex` 仍正確送出（E-29 無回歸）。🔴 **驗收時發現兩件事**：① **`Total 8`／`20/page` 等英文文案**——Element Plus 未設語系，違反 §4.0，禁用詞掃描掃不到元件庫自帶文案（記 [`docs/18`](docs/18-work-errors.md) **E-33**，已修並補防呆）② CDP `/json/new` 在 Chrome 153 改成只收 PUT（記 **E-32**）。**兩支 lint 腳本**：`check-contrast.mjs`（新增，34 組色票＋token 值與 §7 一致）與 `check-forbidden-terms.mjs`（加驗語系），**都用「塞回錯誤值確認抓得到、再復原」實測過真的有作用** | [`docs/21`](docs/21-admin-ui.md)、[`apps/admin/README.md`](apps/admin/README.md)、[`docs/18`](docs/18-work-errors.md) E-31–E-33 | S0-12b |
| **S0-12d** | ✅ | 全部 | **後台改品牌配色**（2026-09-22）。🔴 **使用者拍板推翻 S0-12b 的兩項視覺決定**：① 配色改用**品牌形象的配色**——深色維持不變，但**四層背景色階改由品牌黑 `#231916`／`#31231F` 推導**（`canvas #150F0D`／`surface #231916`／`surface-2 #31231F`／`overlay #3F2D28`），**操作主色由 Element Plus 藍 `#409EFF` 改為品牌桃紅系**（文字用 `--brand-bright #E85BA9`、實心底用 `--brand-aa #D61E83`、hover 用 `--brand-deep #AE186B`，**不新造色，全部沿用既有品牌四階**）② **切站仍不換主色**（§5.1 維持，後台永遠桃紅）。第一階段（`visual-design-architect`）：[`docs/21`](docs/21-admin-ui.md) 907→**1263 行**，§7 全節重寫、§4.3 危險色橘紅化（主色變桃紅後與 danger 紅的色差要重算，ΔE76 49.31→65.03）、新增 §5.1.1 正面回答「主色帶磐石識別，藍鯨維運人員的觀感」、新增 §15 第二階段檔案清單、§13.9–§13.13 逐條記落差。第二階段（`frontend-architect`）：`admin-theme.css` 整批換值、`SiteSwitcher.vue` 隊徽改**真實圖像**（主色同色化後色塊會失去辨識作用）、補 `vite-env.d.ts`（**S0-12 遺留缺口**，專案至今沒有元件匯入過圖片資源，這次第一次匯入才踩到）。⛔ **中性看片台 `--admin-lightbox-*` 明文不動**（照片判色的參考面必須維持中性灰）。🔴 **交付後獨立複驗抓到 `--admin-border-input` 對 `overlay` 只有 2.66:1 過不了 3:1**（文件宣稱「全過、最低 3.52」，那是對 `surface` 算的）——已修為 `#8A7E75`（五層全過，最低 3.29）。**與 [`docs/18`](docs/18-work-errors.md) `E-31` 同一根因、漏在同一層，依全域規定 13 不新增 `E-34` 而是把 `E-31` 升級成機制**：`check-contrast.mjs` 現在邊框也跑五層笛卡兒積、豁免要寫明理由、預期不過的組合以區間斷言釘住。**59 組全過**，並以「塞錯值確認抓得到再復原」驗證過兩條路徑（規格比對、笛卡兒積本身）真的有作用 | [`docs/21`](docs/21-admin-ui.md) §7／§13／§15、[`docs/18`](docs/18-work-errors.md) E-31 | S0-12c |

> 🔵 **S0-8 是全後台每一個表單都會用到的元件，先做好再開模組**，不要每個模組各寫一份。
> 🔴 **S0-8 開工前要先答一件事：HEIC 誰來解。** 規劃書 §4.0 接受 HEIC，但 **ImageSharp 不解 HEIC／HEIF**，
> 而後台使用者多半用 iPhone。三條路（前端先轉 JPEG／伺服器端加 HEIF 解碼元件／改規格不收 HEIC）見 [`docs/17`](docs/17-deployment.md) §6 型別對照與其他定案。
>
> 🟡 **S0-9 完成後已知的三個缺口（不是阻塞，是待辦）**：
> ① `/sitemap.xml` 仍輸出空的 `<urlset>`（[`docs/18`](docs/18-work-errors.md) `E-18` 尚未解，資料端點本身已驗證正確）
> ② schedule 頁 21 筆賽事的 `SportsEvent` JSON-LD（`GEO-08`）尚未做成動態版本
> ③ 種子資料裡 `staff.bio`／`staff.staff_group`／`articles.cover_key` 全為 `NULL`（來源 mockup JSON 本來就沒有這些值，見 [`docs/12d`](docs/12d-field-audit.md) §9）
>
> 🟡 **既有的文件債（2026-09-21 跑 `match_no` 同步鏈時發現，非當次造成）**：
> [`docs/00-harness.md`](docs/00-harness.md) §3「任務→該讀什麼」表格裡有數處**內嵌在句子中**的規劃書行號引用
> （如「規劃書 **1447–1579**」「5.4（1533–1579）」），**在此之前就已與實際章節位置對不上**。
> `docs/tools/check-linerefs.mjs` 的正規表達式只認獨立表格欄位的行號，抓不到這種寫法，所以每次規格異動都會再飄一次。
> **要嘛升級 checker 的比對規則，要嘛人工逐一核對**——這是系統性問題，值得排一次專門處理。

---

## 2. 階段 1 — 主站 MVP ＋ 多俱樂部地基（約 8–10 週）

> 🔵 **S0-3d 的 43 筆分類結果（2026-09-20）**：**全部規劃書都寫了，缺的是 ERD，所以一次同步鏈都不用跑。**
>
> | 類別 | 筆數 | 怎麼處理 |
> |---|---|---|
> | **A. ERD 漏畫** —— 規劃書明文有、ERD 沒有 | **41** | 直接補 [`12a`](docs/12a-database-erd.md) 的屬性方塊與 [`12c`](docs/12c-i18n-tables.md) 的側表欄位。**不動規劃書** |
> | **B. 型別總表沒有，但功能在後台章節** | **1**（贊助活動 Activations，後台 E2 行 1107「活動名稱、日期、圖集、成效摘要」） | 走 [`12` §14.3](docs/12-database-schema.md) 的既有先例——該節原文就是「**每一筆都是把規劃書已有的功能落到資料表，不是新增規格**」，`Season`／`PageBlock`／`Banner` 等 21 張都是這樣處理的。**同樣不動規劃書** |
> | **C. 語意待確認** | **1**（`FanEvent` 的「活動回顧（**關聯**圖集與文章）」——「關聯」可能指連到既有 `Article`，不是自己的圖集子表） | **先不建子表**，確認後再說 |
>
> ⚠️ **但 6 張「整表無欄位定義」要小心**（`PlayerSeasonStat`／`MatchGoal`／`MatchCard`／`MatchLineup`／`Venue`／`InvoiceDonationCode`）：
> 規劃書只給了功能敘述（如「出賽、進球、助攻、黃紅牌」），**沒給欄位名與型別**，補的時候容易變成發明。
> `InvoiceDonationCode` 尤其單薄——行 1429 只寫「捐贈碼名單」四個字。
>
> 🔴 **多俱樂部是地基不是後期擴充。** `club_id` 一旦鋪到 50 張表，後台每一個清單查詢都要決定要不要過濾；
> 先當單一俱樂部做、日後再補＝**重寫整個後台查詢層**。即使藍鯨官網晚上線，地基也排這裡。

| ID | 狀態 | 平台 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|---|
| S1-1 | ⬜ | 後台 | **多俱樂部地基**：`Club`／`Competition` 型別、50 張表的 `club_id`、**資料存取層強制**（不得依賴站台切換器） | 主站 §5.1／§5.4 | S0-6 |
| S1-2 | ⬜ | 後台 | **站台切換器**、`AdminUserClub`／`AdminUserTeam`、`J4 俱樂部與授權管理` | 主站 §4.0／§5.3／§4.10 | S1-1 |
| S1-3 | ⬜ | 後台 | **`J1` 帳號／`J2` 角色與權限／`J3` 稽核與備份**；十種角色與資料範圍 | 主站 §4.10／§6 | S1-2 |
| S1-4 | ⬜ | 後台 | **`B1` 頁面管理**（區塊化編輯器 13 種區塊、版本還原、預覽連結） | 主站 §4.2 B1 | S0-8、S1-3 |
| S1-5 | ⬜ | 後台 | **`B2` 新聞與故事**（八分類、標籤、核心價值標籤、多型關聯、排程發布） | 主站 §4.2 B2 | S1-4 |
| S1-6 | ⬜ | 後台 | **`B3` 首頁編排**、**`B4` 常見問題** | 主站 §4.2 B3／B4 | S1-4 |
| S1-7 | ⬜ | 後台 | **`C1–C3` 球隊／球員／教練**（`C1` 含 `BW1`、`gender` 欄位、`first_team` 每俱樂部至多一筆） | 主站 §4.3 | S1-1 |
| S1-8 | ⬜ | 後台 | **`C4` 賽事**（賽程／賽果／積分，CSV 批次匯入；賽事資料全部人工維護不串 API） | 主站 §4.3 C4 | S1-7 |
| S1-9 | ⬜ | 後台 | **`P1–P3` 課程項目／梯次／報名** | 主站 §4.4 | S1-3 |
| S1-10 | ⬜ | 後台 | **`G1` 表單設計器／`G2` 詢問收件匣**（7 類表單） | 主站 §4.7 | S1-3 |
| S1-11 | ⬜ | 後台 | **`L1` 行事曆總覽／`L2` 自建事件** | 主站 §4.12 | S1-8 |
| S1-12 | ⬜ | 後台 | **`H` SEO 基礎**：全站預設、單頁 Meta／OG／Canonical／noindex、Sitemap（含 hreflang）、robots.txt、**301 轉址批次匯入**、追蹤碼、孤立頁面偵測 | 主站 §4.8 | S1-4 |
| S1-12a | ⬜ | 後台 | **`GEO-01` `llms.txt` 維護**：站點定位、代表頁清單、事實摘要、授權與引用；**雙語各一份、兩站各自一份、隨發布重產**（設定存 `Setting`，不新增型別） | 主站 §7 `GEO-01`、§4.8 | S1-12 |
| S1-12b | ⬜ | 後台 | **`GEO-02` AI 爬蟲授權**：使用者代理清單 ＋ **排除路徑**（會員區、七類表單、訂單查詢、`/m/<token>`、**未成年與學員照片路徑**），輸出至 `robots.txt` | 主站 §7 `GEO-02` | S1-12 |
| S1-12c | ⬜ | 後台 | **`GEO-05` 結構化資料完整性檢查**：列出必填欄位缺漏的頁面與型別，**缺漏者不輸出該型別** | 主站 §7 `GEO-05`、§4.8 | S1-12 |
| S1-12d | ⬜ | 主站前台 | **`GEO-03`／`GEO-04` 事實單一來源與雙重呈現**：成立年份、主場、梯隊、聯賽、聯絡方式全站只有一個維護處（`I` 網站設定），結構化資料與明文同時輸出 | 主站 §7 `GEO-03`／`GEO-04` | S1-14 |
| S1-12e | ⬜ | 主站前台 | **`GEO-07`／`GEO-08` 內容結構與引用資訊**：H1 唯一、H2/H3 不跳階、首段獨立成立的摘要段、不以圖片承載文字；canonical ＋ 發布與更新時間 ＋ 語言 ＋ 作者 | 主站 §7 `GEO-07`／`GEO-08` | S1-13 |
| S1-12f | ⬜ | 主站前台 | **Schema 逐型別輸出（第一批）**：Organization、SportsTeam、Person、Article、BreadcrumbList | 主站 §7 SEO 九項 | S1-5、S1-7 |
| S1-13 | ⬜ | 主站前台 | **多語系框架**：繁中先上線，`en` 欄位與 URL 結構同步就位 | 主站 §7、[`docs/05`](docs/05-i18n-seo.md) | S0-7 |
| S1-14 | ⬜ | 主站前台 | **01 首頁**（九大區塊）、**02 關於台中磐石** | 主站 §3.1／§3.2 | S1-13、S1-4 |
| S1-15 | ⬜ | 主站前台 | **03.1 一線隊（基本）**、**04 學院 4.1／4.2／4.7**、**05 課程 5.1／5.2** | 主站 §3.3／§3.4／§3.5 | S1-7、S1-9 |
| S1-16 | ⬜ | 主站前台 | **06 女子足球＝藍鯨官網入口頁**（單頁） | 主站 §3.6 | S1-14 |
| S1-17 | ⬜ | 主站前台 | **07 新聞中心**（全分類）、**10 表單中心**（7 類）、Location & Map | 主站 §3.7／§3.10 | S1-5、S1-10 |
| S1-18 | ⬜ | 主站前台 | **12 FAQ 獨立單元**（先建 3–4 個高頻主題） | 主站 §3.12 | S1-6 |
| S1-18a | ⬜ | 主站前台 | **`GEO-06` FAQPage Schema**：單元 12 與各頁 FAQ 快捷區塊（G-12）一律輸出；**問題寫成完整句子、答案首句即結論** | 主站 §7 `GEO-06` | S1-18 |
| S1-19 | ⬜ | 主站前台 | **13 賽事行事曆**：`D1／U15／U14／U12` 分類、賽程賽果切換、列表與月曆、單場 `.ics` | 主站 §3.13 | S1-8、S1-11 |
| S1-20 | ⬜ | 主站前台 | **Schema 第二批**：SportsEvent、Event（行事曆）、Course（課程） | 主站 §7 SEO 九項 | S1-19、S1-15 |

---

## 3. 階段 2 — 商業、會員與深度內容（約 6–8 週）

| ID | 狀態 | 平台 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|---|
| S2-1 | ⬜ | 後台 | **`E1–E3` 商業模組**（夥伴／贊助／提案下載與 Lead 追蹤） | 主站 §4.5 | S1-3 |
| S2-2 | ⬜ | 後台 | **`B5` 慈善與社會影響**（四張表留在主站作為主檔） | 主站 §4.2 B5 | S1-4 |
| S2-3 | ⬜ | 後台 | **`B6` 媒體資源**（新聞稿／品牌識別包／高解析圖，對應前台 7.8） | 主站 §4.2 B6 | S0-8、S1-4 |
| S2-4 | ⬜ | 後台 | **`P4` 試訓管理**、報名進階 | 主站 §4.4 | S1-9 |
| S2-5 | ⬜ | 後台 | **會員系統 `K1–K4`**：名單（跨俱樂部＋遮罩）／會籍與方案（受益俱樂部、代收代付）／球衣發放／特約店家與權益 | 主站 §4.11、§5.1 `Membership` | S1-2 |
| S2-6 | ⬜ | 後台 | **`L3`／`L4` 行事曆進階**：分軌檢視、衝突偵測、拖曳改期、訂閱匯出 | 主站 §4.12 | S1-11 |
| S2-7 | ⬜ | 主站前台 | **09 合作夥伴與贊助全區** | 主站 §3.9 | S2-1 |
| S2-8 | ⬜ | 主站前台 | **03.2–03.5** 球員發展、國際通道、球員故事 | 主站 §3.3 | S1-15 |
| S2-9 | ⬜ | 主站前台 | **11 慈善與社會影響全區**（只做介紹與導流，CTA 須明示收受者是協會） | 主站 §3.11 | S2-2 |
| S2-10 | ⬜ | 主站前台 | **05.3–05.5** 冬令營、專項訓練、校園社區 | 主站 §3.5 | S1-15 |
| S2-11 | ⬜ | 主站前台 | **會員中心**：Email 註冊、LINE 一鍵登入與綁定、兩層級會籍、**電子會員卡與公開驗證頁**、權益對照表、特約店家清單（8.4）、升級續會、球衣登記 | 主站 §3.14 | S2-5 |
| S2-12 | ⬜ | 主站前台 | **7.8 媒體專區** | 主站 §3.7 | S2-3 |
| S2-13 | ⬜ | 全部 | **英文版內容上線** | 主站 §7 | B-3、S1-13 |
| S2-14 | ⬜ | 行政 | **啟動 LINE Pay 商店號與電子發票管道申請**（有前置期，不在這裡啟動就會卡住階段 3） | `docs/08` §3 第 22–23 點 | B-10 |

---

## 4. 階段 3 — 文化、商店與社群（約 6 週）

| ID | 狀態 | 平台 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|---|
| S3-1 | ⬜ | 後台 | **`F1` 漫畫／`F2` 球迷會活動** | 主站 §4.6 | S1-4 |
| S3-2 | ⬜ | 主站前台 | **08 文化**：漫畫閱讀器（全免費不設付費牆）、8.2 球迷會 | 主站 §3.8 | S3-1、S2-11 |
| S3-3 | ⬜ | 後台 | **`S1` 商品與規格／`S2` 庫存** | 主站 §4.13 | S2-5 |
| S3-4 | ⬜ | 後台 | **`S3` 訂單／`S4` 出貨物流／`S5` 退貨退款／`S6` 設定與報表** | 主站 §4.13 | S3-3 |
| S3-5 | ⬜ | 主站前台 | **8.3 站內商店 7 頁流程**（前台骨架已有，接後端） | 主站 §3.8 8.3 | S3-3 |
| S3-5a | ⬜ | 主站前台 | **Schema 第三批**：Product 與 Offer（含價格、幣別、供貨狀態） | 主站 §7 SEO 九項 | S3-5 |
| S3-6 | ⬜ | 主站前台 | **LINE Pay 串接**（俱樂部收款，僅限實體商品）＋**電子發票開立** | 主站 §4.13、`docs/08` §2 | B-10、S2-14 |
| S3-7 | ⬜ | 全部 | **舊 Wix 商店退場**：停售、商品與分類網址 301 轉址（`H` 模組批次匯入） | `docs/08` §3 第 1 點 | S3-6 |
| S3-8 | ⬜ | 後台 | **`K5` 抽獎名單管理**：名單快照與序號配發、兩種 CSV 匯出、中獎人回填、獎品發放、公布稿交接 `B2` | 主站 §4.11 K5 | S2-5、B-9 |
| S3-9 | ⬜ | 主站前台 | 賽事積分榜、球員數據自動彙總 | 主站 §3.13 | S1-19 |
| S3-10 | ⬜ | 後台 | **`G3` 電子報整合** | 主站 §4.7 | S1-10 |

> ⚠️ **購物車不得跨俱樂部混買**（主站 §4.13）。開放混買前必須先答 B-8。

---

## 5. 台中藍鯨官網

> **依賴主站階段 1 的多俱樂部地基**（S1-1、S1-2）。地基未完成前無法開始。
> 🔴 **抬頭已於 2026-09-22 修正**：原本寫「複製 `site/` 骨架、換 7 個 CSS 變數，`build.mjs` 一行都不用改」，
> 那是 `site/` 靜態站時代的寫法，**2026-09-20 已改為「一份 Nuxt 映像檔、兩個容器，靠 `NUXT_PUBLIC_CLUB` 切品牌」**
> （[`docs/13`](docs/13-blue-whale-site.md) §6），`apps/web/` 也已照新做法建好。**不會有 `site-bw/` 這個專案。**
>
> 🔴 **2026-09-22 盤點後的真正結論**：擋住藍鯨站的**不是開發，是內容**。
> 機制（品牌切換、單元開關、SEO runtime 覆寫）都已驗證可行，但舊站盤點顯示
> **自有新聞 0 篇、商店 0 件、會籍方案 0 案、未來賽程 0 場、全站 0 個英文字**——
> 見 [`content/blue-whale/gap-analysis.md`](content/blue-whale/gap-analysis.md)。

| ID | 狀態 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|
| BW-0 | ⬜ | **前置**：網域確認、球員與教練名單（含肖像同意）、12 個月賽程 | 藍鯨 §10 第 1、3、4 項 | B-4、B-6 |
| **BW-0b** | ✅ | **舊站內容盤點完成**（2026-09-22）：抓取 `www.tcbw2014.com` 10 單元 46 頁（**取得 37 頁、9 頁未取得**），整理成 [`content/blue-whale/`](content/blue-whale/) 14 個檔案 2200 行——俱樂部沿革 2014–2025、教練團、**2014–2024 共 11 個年度名單**、木蘭聯賽歷屆與 2023 奪冠球季 15 場戰績、U15／U12 招募簡章、推廣活動 10 種課程、夥伴 26 家、新聞 17 則索引。🔴 **[`gap-analysis.md`](content/blue-whale/gap-analysis.md) 是重點**：新站 12 個單元裡 🟢2 可當底稿（02 關於、12 FAQ）／🟡8 部分有／🔴2 幾乎沒有（07 新聞、08 文化＋商店）。**四個零素材子區塊每一個都足以擋上線**：商店商品、會籍方案、自有新聞內文、未來 12 個月賽程 | [`content/blue-whale/gap-analysis.md`](content/blue-whale/gap-analysis.md) | — |
| **BW-0c** | ⬜ | **請營運團隊從 Google Sites 後台補齊未取得的 9 頁**。擋到東西的 5 頁：**2024 陽信銀行國際邀請賽**（冠軍賽事）、**24/25 AFC 亞洲女子俱樂部聯賽**（隊史最高成就 8 強）、**藍鯨盃**（自辦賽事）、**校園巡迴**、**2024-U15 名單**。原因是 Google Sites 動態載入抓不到，**不排除內容是以圖片呈現**。⚠️ 藍鯨由磐石團隊營運，**這些內容在自己手上，是盤點問題不是外部依賴** | [`content/blue-whale/gap-analysis.md`](content/blue-whale/gap-analysis.md) §6 | — |
| **BW-0d** | ✅ | **文案依俱樂部切換的機制已建立並套進核心頁**（2026-09-22，`frontend-architect`）。新增 `apps/web/shared/utils/club-copy.ts` 當單一真實來源，**34 個文案常數兩家都有值**。分類規則（寫進 [`docs/13`](docs/13-blue-whale-site.md) §6 **紀律 11**）：版型與功能性 UI 文字留在樣板；**俱樂部自己的事實與敘事（含 `useSeoMeta` 的 title／description）進資料層**；動態內容（新聞／球員／賽程／商品）不進靜態資料層，走後台 API 依 `club_id` 供給。**缺素材的區塊一律不顯示**（比照 §4 踩雷點 8 對標誌的處理，不放假圖假文、不沿用磐石的）。已套用：01 首頁、**02 關於全部 9 頁**、03 一線隊、10 加入與聯絡；藍鯨版逐字節錄 [`content/blue-whale/club-profile.md`](content/blue-whale/) 的舊站原文。**順帶修掉兩個既有 bug**：頁尾「女子足球」連結沒被 `showWomens` 擋掉、ecosystem 頁的自我指涉節點沒被擋掉——兩者都會讓藍鯨站出現指向 404 的連結。**驗收**：同一份 build 起兩個 process，**藍鯨 13 個核心頁 `grep` 零「磐石／TCRFC」殘留**、`<title>` 逐頁不同、`data-club`／`theme-color`／`canonical` 三處正確分流。⚠️ **新發現：`NUXT_PUBLIC_SITE_NAME` 也必須在藍鯨容器啟動時帶**（`og:site_name`／`<title>` 後綴／Schema `WebSite.name` 三處否則會顯示 `TCRFC`），已記入 §6 紀律 11a。⛔ **不做**：04 青年隊（結構性差異比文案替換大，是 §3 四項單元取捨之一）、07 新聞、08 文化＋商店、MEMBER、13 賽事行事曆（素材 0 或幾乎 0，見 `C-7`–`C-10`） | [`docs/13`](docs/13-blue-whale-site.md) §6 紀律 11／11a | BW-0b |
| **BW-0e** | ✅ | **文案完整性防呆**（2026-09-22）。🔴 **`docs/13` §6 紀律 11 原本寫「只填一家會是 TypeScript 編譯錯誤」——實測發現這個保證在管線裡不成立**：拿掉 `HOME_SEO.bw` 之後 `npm run build` **照樣成功零警告**（Vite／esbuild 只做語法轉譯）、`npm run lint` 也抓不到，只有手動 `npx vue-tsc --noEmit` 抓得到。真正會發生的是該頁在藍鯨容器 SSR 時丟 `TypeError`，**壞在正式環境不是建置時**。新增 `apps/web/scripts/check-club-copy.mjs` 用結構檢查頂住同一個保證並掛進 `npm run lint`，同時驗全域規定第 4 條的 zh／en 雙欄位，**並主動印出自己沒涵蓋的範圍**（16 種 `*Zh` 後綴欄位還沒有 `*En`，因為英文版尚未開工）。已用「移除 `bw` 鍵 → 離開碼 1 → 復原 → 離開碼 0」實測。🔵 **還完既有型別債後可由 `vue-tsc --noEmit` 取代這支腳本** | [`docs/13`](docs/13-blue-whale-site.md) §6 紀律 11 | BW-0d |
| **BW-0f** | ✅ | **修掉 `apps/web` 長期紅燈的 lint ＋ 一個真的壞掉的連結**（2026-09-22）。`npm run lint` 自 S0-9 起固定印 `1 error`，被連續兩輪交付轉述成「`.ics` 靜態檔連結**誤判**」。實際查證：**不是誤判**——`site/src/assets/ics/first-team-2026-27.ics` 存在且納版控，但 S0-9 搬 80 頁時沒複製到 `apps/web/public/assets/ics/`，**一線隊頁的「訂閱一線隊賽程」按鈕真的會 404**。已補檔並 `curl` 實測回 **200**。連帶發現 `check-club-copy.mjs` 原本掛在 `eslint &&` 後面，**因 eslint 固定 exit 1 而從掛上去那刻起就不會執行**。已改串接順序並把 eslint 修到 **0 error**，`npm run lint` 離開碼現在是 0。記 [`docs/18`](docs/18-work-errors.md) **E-34**（根因：長期紅燈的檢查會把「讀錯誤訊息」從流程裡淘汰掉，真錯混在同一個數字裡不會被看見，串在後面的檢查也不會執行） | [`docs/18`](docs/18-work-errors.md) E-34 | BW-0d |
| **BW-0g** | ✅ | **藍鯨舊站資料已匯入本機開發資料庫**（2026-09-22，`backend-engineer`）。作法：`content/blue-whale/` 的 Markdown → `content/blue-whale/data/*.json`（10 個中間格式檔）→ 擴充既有的 `db/seed/generate-club-seed-sql.py`（磐石既有輸出以 `diff` 確認**一行未變**，純新增）。**匯入筆數**：`partners` 26（指導單位 3＋官方夥伴 23）、`teams` 3（`BW1`／U15／U12，**後兩者只建隊伍不建球員**，舊站沒名單）、`seasons` 2、`competitions` 2、`matches` **21**（2023 木蘭 15 場＋2025 總統盃 6 場，逐場）、`players` 28（**只匯 2024 年度**，使用者拍板）、`staff` 5、`milestones` 12（沿革 2014–2025）、`venues` 2（共用表無 `club_id`，符合 `docs/12` 的「43 不加」）。**冪等**：連續套用三次筆數完全不變。**跨俱樂部隔離**：磐石 28 球員／21 賽事／83 文章匯入前後一致。`apps/api` 實測 `/api/v1/bw/{players,staff,schedule}` 與 `/api/v1/clubs/bw` 皆回得出資料。🔴 **交付後複驗改正一處建模錯誤**：26 筆夥伴原本存進 `sponsors` 並把網址塞進 `sponsors_i18n.content` 的自由文字（`指導單位｜官網：https://…`），把分類與網址兩個結構化欄位混進一個查不了的字串；已改存 `partners`（`partner_type`／`website_url` 各有欄位），理由記 [`docs/12d`](docs/12d-field-audit.md) §10。⛔ **未匯入**：新聞 17 則外部連結（見下方待決）、2023 賽季彙總統計（舊站自相矛盾，只匯逐場）、英文名稱（三種寫法，`B-5` 待客戶指定）、U15／U12 球員（舊站沒有）、加油團會員規則與 CSR 段落（明文不得沿用） | [`docs/12d`](docs/12d-field-audit.md) §10、[`db/seed/`](db/seed/) | BW-0b |
| **BW-0h** | ⬜ | 🔴 **待決：舊站 17 則外部媒體報導要怎麼存**。舊站新聞中心**自有全文 0 篇**，只有 17 則外部連結（標題／日期／作者／網址，以 GoGoal 勁球網為主）。`articles` 是為自有文章設計的，**硬塞等於新增規劃書沒有的規格**（全域規定 2）；第三方著作也不得轉載全文，最多做連結牆。**這是規格問題不是實作問題**——要嘛規劃書補一個「外部報導」型別再匯，要嘛先不做。⛔ **不得用既有欄位變通**，那會製造下一筆像 `sponsors_i18n.content` 那樣的糊塗帳 | 藍鯨規劃書 §3.7 | 客戶／規格決定 |
| ~~BW-1~~ | ✅ | ~~建 `site-bw/`~~ **已被單一映像檔取代**：`apps/web/` 一份程式碼服務兩站，`NUXT_PUBLIC_CLUB=bw` ＋ `public/assets/css/club-bw.css`（7 個變數）＋ `shared/utils/club.ts`（favicon／OG／隊徽兩份都打包）。**不會有 `site-bw/`** | 藍鯨 §8.1、[`docs/13`](docs/13-blue-whale-site.md) §6 | S0-9a |
| BW-2 | ⬜ | **Phase B1**：01 首頁、02 關於、03 一線隊、07 新聞、13 賽事行事曆（**這五個是 App 深連結的回退目標，優先完成**） | 藍鯨 §3、§9 | BW-0、BW-1 |
| BW-3 | ⬜ | **Phase B2**：04 青年隊、05 推廣活動、09 夥伴與贊助（**須與磐石分區，不得混列**）、10 加入與聯絡、12 FAQ；英文版內容 | 藍鯨 §3、§9 | BW-2 |
| BW-4 | ⬜ | **Phase B3**：MEMBER 會員中心與藍鯨會籍（獨立的一份、每份會籍一張卡）、08 文化 | 藍鯨 §5.1、§9 | BW-3、S2-11 |
| BW-5 | ⬜ | **Phase B4**：站內商店（代收代付） | 藍鯨 §5.2、§5.3 | BW-4、S3-6、**B-8** |
| BW-6 | ⬜ | **舊 Google Sites 301 轉址**（10 個單元、網址含中文路徑，須人工整理） | 藍鯨 §7、§10 第 14 項 | BW-3 |
| BW-7 | ⬜ | **藍鯨站自己的 `llms.txt`（雙語）與 `robots.txt`**；事實清單（成立 2014-04-12、AFC 執照、木蘭聯賽、太原與豐原球場、吉祥物布魯威）依 `GEO-03`／`GEO-04` 建立。⚠️ **`robots.txt` 排除 U15／U12 球員照片路徑** | 藍鯨 §7 第 4 點、主站 `GEO-01`／`GEO-02`／`GEO-09` | BW-2、S1-12a、S1-12b |
| BW-8 | ⬜ | **藍鯨站 Schema 全輸出**（比照主站逐型別，資料不足時不輸出該型別） | 主站 `GEO-05` | BW-3 |

> 🔵 **BW-2〜BW-5 的頁面結構與功能一律照主站，不另行設計**（藍鯨 §1.3 總則）。
> 例外只有四項：不設 06、不設 11、04 為青年隊、09 分區。
> **主要成本不是開發是內容生產**——沿革、球員簡介、賽事資料、英文翻譯。

---

## 6. 慈善捐款平台

> **完全獨立的系統**：獨立網域、獨立前台、獨立後台、獨立資料庫，捐款人個資由協會自行蒐集與控管。
> ⚠️ **但與俱樂部共用同一台 VM 與同一個 API 行程**（2026-09-18 定案）。「不得即時 join」由 **Azure SQL 不支援跨庫查詢**強制，
> 條件是**不啟用 Elastic Query**；**慈善不接 Redis**。邊界逐條盤點見 [`docs/17`](docs/17-deployment.md) §5。
> **主辦與收款主體是台灣足球策略發展協會，不是磐石。**

| ID | 狀態 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|
| CH-0 | ⬜ | **前置**：網域、協會品牌資產、法人登記與統編、**勸募資格確認**、獨立後台的維運人力 | 慈善 §13 | B-7 |
| **CH-1** | ✅ | **建庫已完成**（本機 `tcrfc_charity_dev`）。🔴 **2026-09-22 查證後改正**：本列原標 ⬜ 是過時的——`db/charity-schema.sql` 的 **29 張表**（25 主表 ＋ 4 張 `*_i18n`）與資料庫**逐張一致**，已核對過。前置寫 `CH-0` 也不正確：建庫不需要網域或統編。⚠️ **正式環境的建庫仍待 `CH-0`**，本列只涵蓋本機開發庫 | 慈善 §9、S0-5 | S0-5 |
| **CH-1b** | ✅ | **慈善庫種子資料**（2026-09-22，`backend-engineer`）。新增 `db/seed/generate-charity-seed-sql.py` ＋ `apply-charity-seed.sh`（白名單**寫死只允許 `tcrfc_charity_dev`**，與主站那支完全獨立——`apply-seed.sh` 的註解明寫「慈善庫是獨立法人邊界」，那是刻意設計不是疏漏）。**29 張表共 202 列**，每個狀態分支都有資料：捐款六態、發票五態（含作廢與折讓）、結算三態（含**退款沖回負項**）、對帳**三種差異類型**、稽核紀錄三類法遵操作。**冪等**：連續套用三次列數不變。**主站庫未受影響**（2／56／42／83 前後一致）。🔴 **個資紀律**：捐款人全部虛構且一眼可辨（「測試用．王小明」、`@example.test`、電話與統編連續 0、`*_encrypted` 是明講不是真加密的占位字串、`source_ip` 用 RFC 5737 文件保留網段）；**協會統編未定（`B-7`）一律用測試值並標註，未自行編造** | [`db/seed/README.md`](db/seed/) | CH-1 |
| **CH-1c** | ✅ | **fixtures 管線**（2026-09-22）。種子資料寫在 `.py` 裡，兩個前端沒有共同的假資料來源——各自手寫就會變成**三份互相對不上的資料**（同一筆捐款在後台顯示「已退款」、前台結果頁卻是「成功」，且無任何機制會發現）。新增 `db/seed/emit-charity-fixtures.py` 把種子匯出成 JSON。**兩個執行層決定**：① 產物**納版控**（不放 `.generated/`）——前端 build 時會 import 它，乾淨 clone 會是建置期硬錯誤，而這批資料全虛構、納版控安全 ② 同時輸出**三份**（`db/seed/` 正本 ＋ 兩個 app 目錄內副本），因為 `docs/20` §3 的 build context 是 app 目錄，根目錄的檔案在容器內讀不到（見 [`docs/18`](docs/18-work-errors.md) **E-35**）。`--check` 一次驗三份是否同步，已掛進兩個前端的 lint，並用「竄改 → exit 1 → 復原 → exit 0」實測過 | [`docs/18`](docs/18-work-errors.md) E-35 | CH-1b |
| **CH-2a** | ✅ | **慈善版面與配色規格**（2026-09-22，`visual-design-architect`）→ [`docs/22-charity-ui.md`](docs/22-charity-ui.md)（843 行）。🔴 **使用者拍板：中性配色、不放 logo**（協會品牌資產未到位，`B-7`）。**前後台都選淺色**，且是重新判斷過的不是跟著俱樂部後台走——前台因為掃碼的人在店裡、戶外光線不可控且是金流憑證情境；後台因為它本質是財務稽核工具（結算、發票、對帳），性質接近記帳軟體不是營運中控台。前台暖灰、後台冷灰做調性區分。**操作主色是墨色**（前台 `#2C2621`／後台 `#21242C`）＝各自的主文字色，理由是任何有飽和度的色相都會被誤讀成「這是暫定的協會品牌色」。⛔ **明文不得沿用俱樂部後台的品牌桃紅**——慈善是**另一個法人**（台灣足球策略發展協會），兩個法人的系統長得一樣會製造真實的混淆風險。**協會識別區塊在資產到位前降級為純文字，不留空 Logo 方框**（套用規劃書本來就替「店家 Logo 未上傳」訂好的降級規則）。色票我獨立重跑笛卡兒積驗過，全過 | [`docs/22`](docs/22-charity-ui.md) | — |
| **CH-2b** | ✅ | **慈善捐款前台**（2026-09-22，`frontend-architect`）。`apps/web-charity/` 從只有 Dockerfile 建成 Nuxt 4 SSR。**8 頁**：一般入口、🔴 掃碼落地頁、項目詳情（含捐款表單）、付款模擬轉場、**結果頁三態**、徵信名單、隱私政策、捐款須知。資料全部來自 fixtures。**`@nuxtjs/seo` 判斷不裝**（只服務一個網域一個法人，用不到主站那套 runtime 多租戶 `site.url` 覆寫；且不裝就不會撞上 `E-17` 的 `htmlAttrs.lang` 覆蓋問題）。**驗收**：lint／build／`docker build`／容器內 `curl` 全過，`X-Robots-Tag: noindex` 確實送出，六種捐款狀態的結果頁逐一回得出畫面，三斷點 33 組零溢出。⛔ **LINE Pay 不做**（`B-7`），停在模擬轉場 | [`docs/22`](docs/22-charity-ui.md) §2 | CH-1c、CH-2a |
| **CH-3a** | ✅ | **慈善協會後台**（2026-09-22，`frontend-architect`）。`apps/admin-charity/` 建成 Vue 3＋Vite＋TS＋Element Plus SPA，**`N1`–`N7` 七個模組全部可點**。🔴 **使用者拍板完整響應式，比照俱樂部後台**（推翻 `docs/22` §6 原本「桌面優先」的暫定假設，已回填文件）。特別做好兩個畫面：**對帳異常佇列**（三種差異類型用符號＋色塊＋文字三重標示，差異記錄不得刪除只能更新處理狀態）與 **`N4` 回饋金結算**（含退款沖回負項，呈現得出 `docs/10` §4 的三條分潤規則）。⛔ **慈善與俱樂部後台的四個差異**：沒有站台切換器（無 `club_id`）、沒有多俱樂部語境、**有稽核紀錄**（勸募法遵）、一級模組是 7 個不是 14 個。修掉 `nginx-spa.conf` 與 `apps/admin` 相同的 `E-29` 標頭繼承 bug。**驗收**：lint／build／`docker build`／容器內 `curl`（首頁與靜態資源皆送出 `noindex`）／SPA fallback 全過，對比度 59 組並以塞錯值實測過 | [`docs/22`](docs/22-charity-ui.md) §3 | CH-1c、CH-2a |
| **CH-3b** | ✅ | **消滅第二份真實來源**（2026-09-22，收尾時修）。後台交付時有一支 `src/data/reconciliationAudit.ts` 是**逐字手抄**種子腳本裡的對帳與稽核數字——原因是那兩段在 `generate-charity-seed-sql.py` 裡是 SQL 字面值不是模組常數，匯出腳本讀不到。交付的 agent 如實回報並照邊界沒自己動 `db/`，是正確的做法。已修：種子腳本 §12／§13 重構成 `RECONCILIATION_RUNS`／`RECONCILIATION_DISCREPANCIES`／`AUDIT_LOGS` 三個常數再迴圈 INSERT，匯出腳本 `EXPORT` 清單補上，後台改為從 fixtures 衍生。**重構後重跑種子：總列數仍 202、對帳 2／差異 3／稽核 3 皆正確，行為未變**；建置產物確認對帳與稽核資料改由 fixtures chunk 提供 | [`db/seed/`](db/seed/) | CH-3a |
| CH-2 | ⬜ | **Phase A 捐款主幹**：掃碼落地頁 → 項目列表與詳情 → 捐款表單 → **LINE Pay 串接（協會收款）** → 結果頁 → 發票開立 → 感謝信 | 慈善 §3、§4、§5 | CH-1 |
| CH-3 | ⬜ | **Phase A 後台**：`N1` 店家與 QR 產生、`N2` 項目管理、`N3` 捐款紀錄。**跑通即可上線收款** | 慈善 §6 | CH-2 |
| CH-4 | ⬜ | **Phase B 帳務與報表**：`N4` 回饋金結算、`N5` 發票管理、`N6` 捐款報表、對帳與異常佇列。**不得晚於第一次結算週期** | 慈善 §6、§7、§8 | CH-3 |
| CH-5 | ⬜ | **Phase C 延伸**：捐款徵信名單、成果回顧頁、**英文版**、`N7` 站台設定細項 | 慈善 §6、§12 | CH-4 |
| CH-6 | ⬜ | **主站 11 章的導流連結指向本平台**（連結網址由後台 `B5` 設定，不寫死在版型內） | 主站 §3.11 | CH-2、S2-9 |

> ⚠️ **兩個主體的 LINE Pay 商店號、憑證與發票字軌不得共用。**
> ⚠️ **退款對外不做、對內做得到**：前台無退款規則與申請管道，但後台 `N3` 人工退款、回饋金沖回、退款通知信全部保留。

---

## 7. 行動 App

> **內容主體是台中磐石 × 台中藍鯨兩隊，收款與法律主體只有俱樂部。** 共用主站後台與資料庫。
> **開發者帳號已到位**：主體為俱樂部，D-U-N-S、Apple Developer 與 Google Play 法人／組織帳號均已完成，**Team ID 與 package name 在手**。
> 🔵 **客戶端技術選型已定案**：原生 Swift／SwiftUI ＋ Kotlin／Compose，見 [`docs/19`](docs/19-app-tech-stack.md)。
> ⚠️ **首個上架版本不含 App 內付款**（規劃書 v3.10），加入與升級以外部瀏覽器導回官網完成。

| ID | 狀態 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|
| AP-0 | ⬜ | **前置**：藍鯨品牌資產、兩隊 12 個月賽程、名單、**藍鯨網域 DNS 控制權**（Universal Link） | App §16.2 | B-4、B-5、B-6 |
| AP-1 | ⬜ | **後台 `M1–M5`**：版本發布／內容編排／推播／推播裝置／App 設定與連線檢查 | 主站 §4 `M` 模組、App §8 | S1-3 |
| AP-2 | ⬜ | **Phase A 骨幹**：`Club`／`Competition` 主檔、兩隊賽程賽果、首次啟動引導與追蹤偏好、新聞列表、球隊名單（唯讀）、夥伴贊助、FAQ、**深連結與 Universal Link**、雙語、`AppDevice` 註冊、設定 | App §15 | AP-0、AP-1、S1-8 |
| AP-3 | ⬜ | **Phase B 會員（不含 App 內付款）**：註冊登入、會員中心、**電子會員卡（每份會籍一張）**、特約店家附近地圖、會籍方案與權益、**以外部瀏覽器完成入會與升級**、抽獎資訊唯讀。⚠️ **訂單建立（帶冪等鍵）與 `activate` 冪等開通在本列就要做完** | App §4、§5.1、§15、[`docs/19`](docs/19-app-tech-stack.md) §10 | AP-2、S2-11 |
| AP-4 | ⬜ | **Phase C 互動與變現**：推播通知、賽事日行程包提醒、**廣告版位 `E4–E6` 與成效統計**、課程報名（帶入＋我的報名） | App §6、§7、主站 §4.5 | AP-3 |
| AP-5 | ⬜ | **Phase D 內容相依**：球員照片與簡介、新聞全文與離線閱讀 | App §15 | B-3 |
| AP-6 | ⬜ | **首次上架**：App Store／Google Play 審查、隱私標籤、商店素材 | App §14 | AP-3、AP-9 |
| **AP-3b** | ⬜ | **Phase E — App 內 LINE Pay 付款**：`payment_mode` 由 `external` 切為 `inapp`，共用 AP-3 已做好的訂單與開通邏輯。🔴 **只能降級不得反向**（不得以 `external` 送審後遠端開啟未審查的 `inapp`） | App §5、§15 Phase E、[`docs/19`](docs/19-app-tech-stack.md) §10 | AP-6、B-8、B-10 |
| **AP-7** | ⬜ | **兩個 private repo 與 CI/CD 管線**：`tcrfc-app-ios`（Xcode Cloud 或 Actions＋fastlane）、`tcrfc-app-android`（Actions＋Gradle→Play internal）。🔴 **必須 private**（含 `.p8`、keystore、service account JSON）；**每次 release 歸檔 dSYM 與 `mapping.txt`** | [`docs/19`](docs/19-app-tech-stack.md) §9 | AP-9 |
| **AP-8** | ⬜ | **`shared/` 契約目錄**：OpenAPI 產 DTO、共用 SQLite DDL、快取時效、深連結對照、錯誤碼、**可見度量測 fixtures**。CI 須有漂移檢查（`git diff --exit-code`） | [`docs/19`](docs/19-app-tech-stack.md) §2 | AP-1 |
| **AP-9** | ⬜ | **商店帳號設定與 `.well-known` 產出**：帳號本身已註冊（B-13），本列做的是——填入 Team ID 與 package name 產出 `apple-app-site-association` 與 `assetlinks.json` 並部署到官網、建立 APNs `.p8` 金鑰與 FCM 專案、開 TestFlight 與 Play 內測軌道。🔴 **2026-09-20 補充條件（`docs/17` §10.3 不可逆類第①項）：部署 `.well-known` 的網域必須是最終正式網域，不得是 `stg.tcrfc.tw`／`bw-stg.tcrfc.tw` 這類測試網址**——Universal Link 綁的是 App 簽署時的 Associated Domains entitlement，換網域要出新版本重新送審 | App §2.3、§14.1、[`docs/19`](docs/19-app-tech-stack.md) §9、[`docs/17`](docs/17-deployment.md) §10.3 | S0-9 |

> ⚠️ **AP-9 要排到 AP-2 之前**——深連結是 Phase A 的項目，`.well-known` 關聯檔要先部署到官網才驗證得了。
> ✅ **磐石網域的 Universal Link 不再受阻**（Team ID 已有）；**藍鯨網域的仍擋在 B-4**（DNS 控制權），未解則藍鯨深連結退回自訂 scheme ＋ 網頁回退。
> ⚠️ **IAP 判定與 LINE Pay 商店號現在擋的是 AP-3b，不擋首次上架**；**店家座標仍擋 AP-3** 的附近地圖。
> ⚠️ **店家座標與場地座標要人工標**，不做執行期即時 geocoding。

---

## 8. 內容生產線（與開發平行）

| ID | 狀態 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|
| C-1 | 🚫 | **取回 113 篇 Google Docs 文稿**（`.gdoc` 是捷徑，本機讀不到） | [`docs/09`](docs/09-intake-inventory.md) | B-3 |
| C-2 | ⬜ | **文稿改寫成網頁文案** | [`docs/07`](docs/07-content-pipeline.md) | C-1 |
| C-3 | ⬜ | **212 張原始照片挑選、裁切、命名、Alt 文字** | `docs/07`、`docs/06` §5 | — |
| C-4 | ⬜ | **既有官網內容遷移範圍拍板**（www.tcrfc.tw 哪些保留／改寫／捨棄） | `docs/08` §3 第 2 點 | 客戶 |
| C-5 | ⬜ | **舊官網 128 筆 URL 的 301 對應表**（[`content/migration/舊官網URL盤點.csv`](content/migration/) 的兩欄目前全空） | `docs/12b` §10 | C-4 |
| C-6 | ⬜ | **英文翻譯**：全站內容的英文版由誰產出未定（影響階段 2 時程） | `docs/08` §3 第 10 點 | 客戶 |

> 🔴 **以下四條是藍鯨的內容生產線，2026-09-22 依 [`gap-analysis.md`](content/blue-whale/gap-analysis.md) 新增。**
> **前置時間比開發長，不能等開發完才開始**——機制都已驗證可行，真正擋住藍鯨站上線的是這四件事。

| ID | 狀態 | 工作 | 規格在哪 | 前置 |
|---|---|---|---|---|
| **C-7** | ⬜ | **藍鯨自有新聞內文：目前 0 篇**。舊站新聞中心只有 **17 則外部媒體連結**（以 GoGoal 勁球網為主），**第三方著作不得轉載全文**，最多做連結牆。且**已停更於 2024-09**——期間發生 AFC 亞冠 8 強、2025 總統盃亞軍、25/26 開季**全未記錄**。🔵 對照：磐石站有 83 篇＋267 張圖 | 藍鯨 §3.7、`GEO-06` | — |
| **C-8** | ⬜ | **藍鯨會籍方案與條款：目前 0 案**。🔴 舊站那份「藍鯨球迷俱樂部」13 條**不能用**——第一條寫事務局「設在大阪櫻花體育俱樂部內」，是從日本 J 聯盟球迷俱樂部章程移植未改完，法人形態寫「一般社團法人」（日本制度）。**必須重寫，不是遷移**。🔵 有三條值得帶進討論：未成年監護人同意、會籍期限＝一個賽季、資料變更通知義務 | 藍鯨 §5.1 | B-9 |
| **C-9** | ⬜ | **藍鯨商店商品：目前 0 件**。無商品、無價格、無尺寸表、無物流說明、無商品圖。舊站**全站現金收費、明文「沒有刷卡、數位支付服務」** | 藍鯨 §5.2 | B-8、B-10 |
| **C-10** | ⬜ | **藍鯨英文內容：全站 0 個英文字**。舊站只有中文 → **新站英文是全新生產，不是翻譯既有**。與 C-6 同一個待決（誰產出未定），但藍鯨的量與磐石不同，要分開估 | 藍鯨 §7 | B-5（英文正式全名）、C-6 |

> ⚠️ **另有兩段舊站文字明文不得沿用**（除了上面 C-8 那份會員規則）：
> ① 「關於」頁的 **CSR 段落內文談的是「住商集團」**，與藍鯨無關，必須整段重寫；
> ② **2023 賽季戰績數字舊站自相矛盾**（一線隊頁寫「10 勝 4 平 1 敗」，該季 15 場戰績表是 6 勝 6 平 3 敗）——
> **新站不得沿用任何未經核對的統計數字**，這正是 `GEO-06` 事實單一來源要解決的問題。

---

## 9. 已完成

| 日期 | 事項 |
|---|---|
| 2026-09-21 | **`Match.match_no`（場次編號）一路接到前台 schedule 頁，S0-9 的 21 個賽事錨點 id 缺口消除**（`backend-engineer`，接續同日稍早 `system-analyst` 的規格與 DDL 同步鏈）。本機庫用 `ALTER TABLE matches ADD match_no int NULL` 補欄位（未整庫重建，理由與以後同類情況的處理方式見 [`db/seed/README.md`](db/seed/README.md) 新增段落「DDL 改了、既有本機庫沒跟上」）；`generate-club-seed-sql.py` 在既有「找不到才 INSERT」區塊外**另加一句獨立、可重複執行的 `UPDATE`** backfill 既有 21 筆列（只改 INSERT 對既有列無效）；`apps/api` 的 `MatchDto`／`MatchesRepository`／`MatchesEndpoints` 補上 `matchNo`（明確 SELECT，沿用既有 `ClubScope`）；`apps/web` 的 `fixtureId()` 改回 `fx-{日期}-{h|a}-{場次編號}`。**順帶把站台根路徑 `/` 排除在 `compare-dom.mjs` 的內容比對之外，改斷言「必須 302 且 `Location: /zh/`」**（不是新增第七類必然差異，是承認 `/` 根本不是可比對的內容頁，見 `site/tools/compare-dom.mjs` 檔頭與 [`site/tools/README.md`](site/tools/README.md)）。**`compare-dom.mjs` 全站實跑：80 頁中 77 頁 `<main>` 零差異**（`zh/schedule` 由本次修復、`/` 由本次改為斷言且通過），**其餘 3 頁**（`zh/news`／`zh/news/match`／`zh/news/camps-events`）**全部可歸因於既有、與本次無關的 `news.json` intcup 分類待客戶確認**（`db/seed/README.md`「已知落差」）,沒有出現任何六類以外的新差異。過程中自己踩並修正一坑（id 裡的主客場誤用 `data-ha` 的全字編碼），記為 [`docs/18`](docs/18-work-errors.md) `E-25`。**未 commit** |
| 2026-09-21 | **`Match.match_no`（場次編號）同步鏈跑完**（`system-analyst`）：`docs/12d` §9 記載的落差已解決。主站規劃書 C4 補上「場次編號」欄位並升版 **v3.12**（中英雙版同步）；`docs/12`／`docs/12a`／`docs/12b` 補上欄位說明與 ERD 屬性；`db/club-schema.sql` 的 `matches` 表加 `match_no int NULL`（可為空，因盃賽等非聯賽賽事可能沒有官方編號；未新增唯一鍵，理由見 `docs/12d` §9）。**未動 `db/seed/`／`apps/api/`／`apps/web/`**，那是下一棒的事。行號對照表已用 `node docs/tools/check-linerefs.mjs --fix` 重算 |
| 2026-09-21 | **環境數量定案：全專案只有本機開發與正式 VM 兩套，沒有 staging**（[`docs/14`](docs/14-invariants.md) 新增不變量、[`docs/17`](docs/17-deployment.md) §10、[`docs/20`](docs/20-cicd.md) §1）。前一日新增的 `docker-compose.staging.yml` **已刪除**——「正式網址到位前」是同一套正式環境的**階段**不是環境，改由 `.env` 的 `CADDYFILE` 指向 `deploy/Caddyfile.prelaunch`（原 `Caddyfile.staging`）達成，**同一份 compose、同一道指令**；`SITE_ENV` 的值由 `staging` 改為 `prelaunch`，`STAGING_BASIC_AUTH_*` 改名 `PRELAUNCH_BASIC_AUTH_*`（改為可為空，掛了 prelaunch 卻沒填值時 Caddy 直接啟動失敗）。已用 `caddy validate` 與三種 `.env` 組合的 `docker compose config` 驗證。失誤記為 [`docs/18`](docs/18-work-errors.md) `E-13` |
| 2026-09-20 | **網址從暫用網址到正式網址的策略規劃完成**（[`docs/17-deployment.md`](docs/17-deployment.md) §10）：上線前用 `tcrfc.tw` 子網域（`stg`／`bw-stg`／`charity-stg`／`admin-stg`／`admin-charity-stg`）；換網址成本分級與三類不可逆（App Universal Link／慈善 QR Code／會員卡連結）；三層防護（`NUXT_PUBLIC_SITE_ENV` ＋ `robots.txt` ＋ Basic Auth／Cloudflare Access）；cookie 作用域陷阱與 `__Host-` 前綴；主站切換 www.tcrfc.tw 的具體步驟與回滾程序。新增 `deploy/Caddyfile.prelaunch`（**當日原名 `Caddyfile.staging`，另有 `docker-compose.staging.yml`，均於 2026-09-21 撤銷／改名**）；`docker-compose.yml` 的 `api` 補上網域與 `CORS_ALLOWED_ORIGINS` 變數（原本完全沒有）；`.env.example` 改為暫用網域預設值 |
| 2026-09-18 | **後台上傳的圖片一律縮圖後保存**（主站 v3.9／藍鯨 v1.8／慈善 v2.5／App v3.9，中英雙版與 PDF 同步）：伺服器端重新編碼為 WebP、長邊上限 2560px、**不留原始檔**、去 EXIF（含 GPS）；固定產 1280／640／320 ＋ 後台 160px 方形縮圖；前台不得直接引用主檔 |
| 2026-09-18 | **新增 [`docs/18-work-errors.md`](docs/18-work-errors.md) 作業失誤紀錄**：`E-01`–`E-10` 十筆（含根因與防呆位置），`CLAUDE.md` 全域規定加第 13 條、同步鏈加第二項收尾自檢 |
| 2026-09-18 | **後台設計通則**（主站 v3.7／藍鯨 v1.7／慈善 v2.3／App v3.7）：後台依前台單元切分、前後台同名、介面用日常中文不顯示代號與技術詞；新增前後台對照表；九個子模組更名 |
| 2026-09-18 | **GEO 升格為正式規格 `GEO-01`–`GEO-09`**（主站 v3.6／藍鯨 v1.6）：`llms.txt`、AI 爬蟲授權、事實單一來源與雙重呈現、Schema 全輸出入規格；後台 `H` 加三項功能 |
| 2026-09-18 | **技術選型全面定案並跑完同步鏈**（主站 v3.8／慈善 v2.4／App v3.8，中英雙版與 PDF 同步）：新增 [`docs/17-deployment.md`](docs/17-deployment.md)；規劃書只新增「LINE Pay 須登記出口 IP」這條外部約束 |
| 2026-09-18 | **後台圖片改為欄位直傳、不設媒體庫**（主站 v3.5／慈善 v2.2／App v3.6，中英雙版與 PDF 同步；`B` 模組重編為 `B1–B6`、新增 `PressResource`） |
| 2026-09-18 | **藍鯨站定為主站同骨架站台、只換配色**（藍鯨 v1.5 §1.3 總則，中英雙版與客戶版同步） |
| 2026-09-14 | 藍鯨品牌色自隊徽取樣定案（七個變數）；隊徽點陣主檔取得 |
| 2026-09-14 | 四份客戶版文件產出（中英，含後台完整模組一覽） |
| — | 四份功能規劃書完成；`site/dist` 80 頁前台骨架部署於 Cloudflare Pages（全站 `noindex`） |

---

> **本檔只追蹤進度，不放規格。** 新的規格一律先改 [`output/`](output/) 的規劃書，再同步 [`docs/`](docs/)，最後回來更新本檔的狀態。
