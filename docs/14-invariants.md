# 14. 全站不變量與踩雷速查

> **這一份是「改錯會出事」的清單**，不是規格。規格的真實來源是 [`../output/`](../output/) 的四份規劃書。
> 本檔只收**跨模組、違反成本高、而且容易在不知情下改壞**的事實——代號、色彩、命名、範圍邊界、
> 以及幾條在規劃書裡分散、合起來才看得懂的執行層決定。
>
> **每個 session 的建議讀法**：先讀 [`00-harness.md`](00-harness.md)，要動手前掃一次本檔，
> **再掃一次 [`18-work-errors.md`](18-work-errors.md) §1**（那是實際犯過的錯，本檔是不能犯的錯）。
> 本檔與規劃書衝突時**一律以規劃書為準**，並回頭修正本檔。

---

## 速查

- 🔴 **正式 VM 的容器只由 CD 管：compose 專案目錄是 runner 的 checkout 目錄，映像檔標籤一律每個映像檔各自一個 `TAG_*`**（2026-10-02，`docs/20` §4a）。
  ① **不要在 VM 上手動 `docker compose up -d`**，尤其不要從 `/home/runner/tcrfc-src`（首次 CD 成功後即退役）：專案目錄不同＝設定 hash 不同＝八個容器全部重建一次，且 proxy 會掛到舊 Caddyfile；
  確要手動，在 checkout 目錄用 `--env-file /opt/tcrfc/.env` 並**自己帶齊五個 `TAG_*`**（照 `deploy-state.env`），否則標籤退回 `.env` 的 `IMAGE_TAG=master`，等於把已部署的 SHA 版本換成浮動標籤。
  ② **不要用單一 `IMAGE_TAG=<sha>` 部署**——`deploy.yml` 只重建有變動的映像檔，沒重建的沒有該 SHA 標籤，拉不到。
  ③ **不要加 `-p`／`COMPOSE_PROJECT_NAME`**：專案名由 compose 檔的 `name: tcrfc` 決定，與現行容器一致。
  ④ `deploy-state.env`／`deploy-history.log` 只由 `deploy/cd-deploy.sh` 寫入，勿手改；`/opt/tcrfc/.env`、`secrets/`、`data-protection/` 部署程序不得改寫或刪除。
  ⑤ Caddyfile 是單檔 bind mount，git 換檔後容器仍抱舊檔；CD 以「比對執行中 proxy 的檔案雜湊」決定是否重建 proxy，不要改成只靠 compose 的重建判斷。

- 🔴 **正式庫的結構變更只走 `db-migrate.yml`，且 `production-db` 環境必須先建好**（2026-10-02，`docs/20` §5「`db-migrate.yml` 實作」）。
  ① **`deploy.yml`／`rollback.yml` 永遠不碰資料庫**；順序是先 migrate、後 deploy，結構變更用展開—收縮（`rollback.yml` 只退映像檔、不退結構）。
  ② ⛔ **不要在正式庫跑 `dotnet ef database update`、不要手改 `__EFMigrationsHistory`**（正式庫是 DDL 建的，歷史表是 `prod-db-init.sh` 寫的）；套用的東西＝preview 產出、SHA-256 鎖定、核准者讀過的 `idempotent.sql`。
  ③ 🔴 **workflow 引用不存在的 environment，GitHub 會自動建立「無保護」的同名環境**＝核准關卡被靜默繞過；`production-db`（Required reviewers ＋ 僅 `master`）要事先建（`infra/README.md`「資料庫 migration」）。
  ④ **sqlcmd（Linux 的 `mssql-tools`）不支援 `-f`**，不要加 `-f 65001`；要 `-I`（QUOTED_IDENTIFIER ON）。⑤ **EF 的 `--idempotent` 是每支 migration 一個交易，失敗只回滾那一支**，不是整批全成全敗；Basic 層 PITR 只有 7 天。
  ⑥ 公開 repo 的 Actions log 人人可看：腳本**不得印伺服器主機名稱**（只印資料庫名），密碼不進命令列／log／summary。

- 🔴 **客戶照片不得被 import 進建置，也不得進映像檔**（2026-10-02 使用者決定，`E-113`）。`apps/web/public/assets/img/`（含未成年學員肖像，不納版控）
  一律寫成 `siteImg('/assets/img/…')`（`apps/web/app/utils/siteImage.ts`），**不得**寫靜態 `src="/assets/img/…"`、`import … from '/assets/img/…'`、
  `new URL('/assets/img/…', import.meta.url)`、CSS `url(/assets/img/…)`——Nuxt 編譯器會把前兩者變成建置期 import，乾淨 checkout 直接 `UNRESOLVED_IMPORT`。
  照片執行期來源是 Azure Blob（`NUXT_PUBLIC_MEDIA_BASE_URL`，物件鍵 `site/<路徑>.webp`）；`apps/web/.dockerignore` 排除 `public/assets/img`。
  防呆：`npm run lint` 的 `lint:site-images`（`scripts/check-site-images.mjs`）＋ `scripts/site-images.txt` 一致性。看到文件寫「build 前先 rsync 照片」一律視為舊流程。

- 🔵 **後台登入不強制變更密碼、不強制 2FA，正式環境亦同**（客戶 2026-09-30 裁決，主站規劃書 v3.15 §4.10 J1、§8〈安全〉）。
  變更密碼保留為帳號持有人**自行選用**（帳號安全頁）。🔵 **2FA 自 v3.17（使用者 2026-09-30 裁決）起後台介面暫不提供設定入口**（帳號安全頁不出現啟用選項；系統能力與資料欄位保留，日後可開放）；**已啟用 2FA 的帳號登入時仍須輸入驗證碼**（驗證流程保留）。
  `AdminUser.must_change_password`／`two_factor_*` 欄位保留，語意由「登入強制」改為「管理員可要求某帳號改密碼」與「使用者自選功能的狀態」。**`must_change_password` 預設 `0`**（新建帳號、種子帳號皆然；僅管理員代為重設他人密碼時才設 `1`，只是提示、不強制）。
  ⛔ 看到舊文件或程式寫「首次登入強制改密碼」「後台強制 2FA」「帳號安全頁可自行啟用 2FA」一律視為舊規格。**慈善捐款平台的獨立後台亦已比照放寬**（客戶 2026-09-30 裁決；慈善規劃書 v2.5 只把「獨立 2FA」寫成能力、未寫強制，故不需改版；見 `docs/16` §2.3 `AdminUser`）。

- 🔵 **E1a 後台（E1 夥伴／E2 贊助／E3 提案與 Lead／B5 慈善／B6 媒體專區／C5 榮譽）的五條不能改壞的規則**（2026-09-30，細節見 `apps/api/README.md`「E1a」節）：
  ① **提案 PDF 只放私有容器**，訪客填表單後才拿 30 分鐘限時連結，任何地方都不得輸出提案檔的公開網址；② **Lead 含個資**，`business.lead.view` 只給商務／贊助與合作球隊管理，匯出是受限碼；
  ③ **慈善捐款導流文案（設定了捐款網址時）必須點明「台灣足球策略發展協會」**，後端強制驗證，前台不得寫死捐款網址、一律讀 `GET /api/v1/{club}/charity/cta`；④ **影響力數據金額類預設不公開**（`is_public` 預設 `false`，公開端點只輸出 `is_public = 1`）；
  ⑤ **贊助商聯絡窗口、合約日期、到期提醒不進公開 DTO**；未公開價格的贊助方案公開端點完全不輸出價格。**贊助故事不用 `article_relations`**（B2 儲存時整批取代該表）。

- 🔵 **C1 批（F1 漫畫／F2 球迷會活動／S1–S6 站內商店後台／K5 抽獎）的五條不能改壞的規則**（2026-09-30，細節見 `apps/api/README.md`「C1」節）：
  ① **庫存的 `stock_qty`／`reserved_qty` 只有 `InventoryService` 能寫**（交易內 `UPDLOCK`、每次異動一筆 `inventory_movements`、資料庫 CHECK 擋負庫存），其他程式碼不得直接改，`AdminC1MiscTests` 的原始碼掃描會失敗；
  ② **商店與抽獎相關類別不得注入 `IQueryCache`**（庫存、金流冪等、會籍與訂單狀態、購物車不得讀快取），同一組測試以反射鎖住；
  ③ **`ILinePayGateway`／`IEInvoiceService` 目前是「未串接」實作**（介面已定、呼叫端不變），正式串接只換實作、不改訂單狀態機；訂單狀態轉換一律條件式單句更新（冪等，不會重複扣庫存或重複退款）；
  ④ **時間欄位：資料庫與 JSON 一律 UTC、不帶 `Z`，`Unspecified` 視為 UTC**；日期欄位是台灣當地日期（`TaiwanClock`）；
  ④-1 🔴 **賽事時間語意（App 規劃書 v3.14 §3.2，2026-10-05 拍板）：`matches.match_on`＋`kickoff`（含 `original_*`）是台北牆上時間，不是 UTC 時間戳，不得當 UTC 存取，也不得換算兩次**；`kickoff` 可為空＝開賽時間未定（顯示只有日期、不排開賽提醒、`.ics` 為全天事件，**不得猜 00:00**）。換算後的時刻只有衍生欄位 `MatchDto.kickoffAt`（UTC 帶 `Z`），客戶端優先用它、不自行推算；其餘時間戳一律 UTC。見 [`19`](19-app-tech-stack.md) §11f；
  ⑤ **整合測試動到共用設定必須快照還原**（`C1Test.SnapshotSettingsAsync`，`E-81`／`E-89`），`種子基線_C1示範資料…` 測試守著種子不被吃掉。漫畫功能藍鯨一律 403（`FeatureNotAvailableException`）。

- 🔵 **H 批（A 儀表板／P4 試訓公開報名／G-09 電子報訂閱／G-02 全站搜尋／I 網站設定）的七條不能改壞的規則**（2026-10-02，細節見 `apps/api/README.md`「H 批」節）：
  ① **全站搜尋的可見性規則必須與各自的公開端點完全一致**（新聞已發布且到時間、課程已發布、球員／教練照片遵守肖像同意 fail-closed），只比對標題／名稱／摘要／簡介／職稱，**不得比對個資欄位或區塊內文 json**；新增搜尋類別時先抄對應公開 repository 的 `WHERE`，不要自己重寫一版；
  ② **電子報退訂是黏著的**：曾退訂的信箱再從公開表單訂閱**不得**改回訂閱（沒有信箱驗證，等於替第三人違反退訂意願），公開訂閱的回應**不得**透露名單狀態；重新訂閱只走後台（須註明原因）。若日後加雙重確認信，必須同時新增「待確認」狀態值，不要用現有兩態硬湊；
  ③ **EDM API 金鑰只寫不讀**：任何回應（含部分字元）都不得輸出，存放一律 Data Protection 加密；**政策頁內文是純文字**，前台不得 `v-html`（沒有 HTML 清理器）；
  ④ **字串翻譯表的「翻譯人員只能改非預設語系」在伺服器端強制**（`site.string.translate` 碰繁中原文或分組 → 整個請求 `403`），這是目前唯一真的做出欄位級「僅翻譯」限制的模組，其他模組的 `translate_only` 指派仍要等各自的欄位級強制；
  ⑤ **儀表板沒有專屬權限碼，區塊依對應模組權限出現；依權限出現的數字用可為空型別，`null`＝看不到、`0`＝真的是零**（`E-125`）；新增一個會被所有角色進入的頁面，先逐角色列出落點再寫程式；
  ⑥ **I 網站設定的前台讀取在後台儲存後立即失效快取**（`SiteSettingsRepository.CacheEntities`）——維護模式不得延後生效；語系表是全站共用，變更時清所有俱樂部的站台設定快取。維護模式**只是旗標**，不會自動攔截其他公開端點；
  ⑦ **新增權限碼必須同時寫三處**：`db/seed/generate-club-seed-sql.py`（新建庫）→ 重跑 `generate-prod-reference-sql.py`（正式庫首次初始化）→ **migration 內的參考資料 SQL**（已建好的庫只能靠它取得，業務自然鍵「不存在才新增」）；漏了第三處，本機與正式庫會悄悄缺權限、非系統管理員永遠 403（系統管理員因為略過權限檢查而不會發現）。

- 📄 **轉述會過期，來源不會——把狀態類敘述當成行動依據之前，一律回查來源**（2026-09-22 立，
  根因與實例見 [`18-work-errors.md`](18-work-errors.md) **`E-34` 升級段**）。
  **哪些是「轉述」**：[`../STATUS.md`](../STATUS.md) 的工作列與備註、`docs/` 裡的待辦／缺口／待裁決段落、
  交接說明與上一輪交付報告裡的「已知問題」。**哪些是「來源」**：規格看[四份規劃書](../output/)、
  資料表看 [`../db/`](../db/) 的 `*.sql`、程式行為看程式碼、工具的設計原則看該工具的檔頭註解。
  ⚠️ **這不是勤勞問題是老化問題**——每一份轉述在**寫下的當下都是對的**，
  它會在來源被改掉之後繼續被引用，而且**老化不會發出任何信號**。
  🔴 **發現轉述與來源衝突時**：以來源為準，並**在同一次交付內把過期的轉述改掉**。
  只記一筆不改，下一個人會再調查一次同樣的事。
  🔵 **實例（同一天撞到四次，其中一次是寫這條紀律的當下犯的）**：
  `STATUS` 說是缺口／`docs/12d` 說不算缺；`docs/12c` 說待裁決／DDL 早就建好了；
  比對基準說有差異／那是刻意修正沒登記；紀律初稿宣稱某個防呆已實作／它不存在且違反已拍板的原則。

- 🔵 **藍鯨站＝主站同一套網站，只有配色不同**（藍鯨規劃書 **§1.3 總則，v1.5，2026-09-18 客戶指示**）。
  版型、元件、頁面結構、互動行為與前後台功能一律**完全比照主站**，**不另做設計提案、不調整版型、不換字體與間距系統**。
  **唯一的品牌差異是七個配色變數**（取自隊徽，見下一條）。**例外只有五項單元取捨**：不設 `06 女子足球`、不設 `11 慈善與社會影響`、`04` 為**青年隊**（U15／U12 女子隊，不沿用學院的招生與課程架構）、`08` 不設 **`8.1` 漫畫**（台中藍鯨沒有漫畫，客戶 2026-09-30 回覆）、`09 夥伴與贊助`須與磐石**分區**。`08` 的 8.2 球迷會與 8.3 周邊商品比照主站，後台有內容就顯示、沒有就空狀態。
  ⚠️ **五項以外任何「藍鯨這裡要不要不一樣」的提議一律不成立**——要做出差異＝規格變更，須先改規劃書。前台實作對應 [`13`](13-blue-whale-site.md) §6：`site-bw/` 複製 `site/` 骨架，只換 7 個 CSS 變數與 `theme-color`。
- 🔴🔴 **藍鯨關閉單元只限 §1.3 五項例外及其直接推導，「沒有內容」不是關閉整頁的理由**（2026-09-29 立，S1-15／S2-8／S2-10 連續三輪犯過、BW-C1 修正，見 [`18-work-errors.md`](18-work-errors.md) `E-76`）。
  `shared/utils/units.ts` 的 `BLUE_WHALE_DISABLED_UNITS` 只能收上一條五項例外能**直接推導**的代號（目前是 `06`／`11`／`4.7`／依附 `4.7` 的 `12.2`，另加 **`8.1`**——藍鯨規劃書 v1.9 §2.1）——「藍鯨沒有對應的具名內容／真實素材可換」是內容缺漏，不是功能取捨，**正確處理是重開頁面、版型比照主站，有真實素材就換，沒有就顯示既有的「準備中／收錄中」空狀態**（3.5 球員故事、4.3／4.4 是既有先例），不是整頁 404。
  ⚠️ **一個單元代號可能同時要出現在兩處**：頁面自己的 `definePageMeta({ unit })`（給 `unit-gate.global.ts` 擋 404 用）與 `FAQ_CATEGORY_UNIT_CODES`（給沒有自己路由的 FAQ 分類用）——只改其中一處會漏掉真正該關閉或該開放的行為，改動後兩處都要對照。
  🔴 **`BLUE_WHALE_DISABLED_UNITS` 陣列每一項都必須在同一行帶 `//` 行內註解且含 `§`**（引用藍鯨規劃書章節），由 `apps/web/scripts/check-bw-units-citation.mjs` 靜態檢查並掛進 `npm run lint`，沒有章節依據的關閉會直接讓 `lint` 失敗。
- 🔴 **本機起 `bw` 容器（含跑 `check-club-brand-leak.mjs`）一律要帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`**（藍鯨規劃書 §6 紀律 11a，`docs/13-blue-whale-site.md`）。漏帶時 `og:site_name`／`<title>` 後綴／Schema.org `WebSite.name` 會悄悄落回 `nuxt.config.ts` 的本機開發預設值 `TCRFC`，讓品牌外洩檢查全站誤判失敗——這條已經在 `docs/13` 記過一次（S1-14 缺口補完，2026-09-29 又撞到一次驗證流程本身的失誤，不是程式碼迴歸），本條把它抬進速查清單，動手驗 `bw` 容器前先看這裡，不用先找到 `docs/13` 才知道。
- 🔴🔴 **`check-club-brand-leak.mjs`（BW-C1，2026-09-29 全面改版）現在是全站涵蓋、預設 hard-fail**，不是「只驗一份手動清單、其餘只計數」——舊版 `PROTECTED_PAGES`（33 頁、只含 `/zh/`）漏掉的 `join/international-player/`（10.4，`Taichung Rock FC`／`TCRFC` 整頁沒有俱樂部分支）與全站 `/en/` 版本，都是這次改版才第一次被抓到，見 [`18-work-errors.md`](18-work-errors.md) `E-77`／`E-78`。
  **新設計**：自動收集 `app/pages/zh/` 算出的全部路由（`/zh/`＋`/en/`，動態路由排除），逐頁掃描回 200 的內容；命中詞表任何一個詞就是失敗，**除非**該路由＋該詞的組合列在腳本內 `EXEMPT_PAGES`（每筆必附規格依據或既有缺口編號，不是「看起來還好」）。**例外清單只能往下減、不能往上加**——腳本自己用 `git show HEAD:<this file>` 比對上一版，新增例外會讓腳本自己先失敗（方向跟舊版 `PROTECTED_PAGES`「只能往上加」刻意相反）。
  詞表：`磐石`／`TCRFC`／`學院`／`Taichung Rock`／`www.tcrfc.tw`（**不是裸 `tcrfc.tw`**——裸網域會撞到 `blueWhaleSiteUrl` 這個 runtime config 預設值，序列化進每一頁的 hydration payload，全站每頁誤判命中一次，見 `E-77`）。
  現有例外清單（均已附規格依據，見腳本內註解）：結帳／商店頁的「收款方為台中磐石足球俱樂部」（主站與藍鯨規劃書 §1.3 明文要求的真實揭露，不是外洩）、商品過渡期文案的 `www.tcrfc.tw` 舊站連結（`club/first-team/player/` 範本頁已於 S3-9 由資料驅動的 `player/[id]` 取代，例外已移除；商店頁的收款主體字樣在藍鯨只允許出現於 `shop/`、`checkout/` 兩頁，其餘商店頁一律只用 API 的 `collectingSubjectName`，且不得讓它進入頁面 payload）。
  **用法不變**：`node scripts/check-club-brand-leak.mjs --base-url=http://127.0.0.1:PORT`，只對 bw 容器跑（tcrfc 站允許出現這些詞）。
- 🔴🔴 **品牌外洩也包含圖片（`E-83`，2026-09-30）**：文字詞表看不見圖片，**藍鯨站不得輸出任何磐石圖片**（未成年學員、球員、隊徽、贊助、Trenčín 等一律不行）。`apps/web/scripts/check-club-image-leak.mjs`（與詞彙檢查共用 `scripts/lib/collect-routes.mjs` 的路由）對 bw 全站 `/zh/`＋`/en/` 抓 `<img>`／`<source>`／`<video poster>`／inline `style` 與樣式表 `url()`／`og:image`／JSON-LD `image`／`logo`／icon link，**白名單制**：只允許 `/assets/brand/bw/` 與腳本內 `NEUTRAL_ALLOWED` 明列（每筆附理由、目前 0 筆）的素材，**不確定一律歸磐石**。**判斷「中性」前必須打開圖片看過**——厚底緩震機能襪的商品照每張都印有 TCRFC 標誌，不是通用素材。新增圖片到兩站共用位置一律用 `ClubHeroBg`／`ClubImg` 元件或 `v-if="isTcrfc"`。盲區：API 回傳圖片與動態路由 `[slug]` 不在掃描內。用法：`node scripts/check-club-image-leak.mjs --base-url=http://127.0.0.1:PORT [--inventory]`。
- 🧭 **後台是為了產出前台而存在的**（主站規劃書 **§4.0 後台設計通則，v3.7，2026-09-18 客戶指示**）。
  **模組依前台單元切分**，不依資料表切分；每個模組頁首寫明**它產出前台的哪一頁或哪一區塊**（對照表在規劃書 §4.0）。**對不上的模組就是切錯了。**
  **前後台同名**：前台叫「新聞中心」，後台就不叫「文章管理」。
  ⚠️ **介面上不得出現**：資料表名與欄位名、**模組代號**（`B1`／`K4`／`S3`）、**權限碼**（`shop.order.export`）、**隊別代號**（`D1`／`BW1`）、英文技術詞（`slug`／`canonical`／`token`／`SKU`／`blob`／`Schema`／`club_id`）。**CSV 匯出的欄位標題同此規則。**
  技術概念用中文說法（對照表在 [`06-conventions.md`](06-conventions.md) §1）。
  ⚠️ **這條約束介面文字，不是資料結構**——資料表名、欄位名與權限碼維持英文 `snake_case`，不得因此去改資料庫命名。
  🔴 **後端任何會回給使用者的訊息（400／403／409 的 `detail`／`title`）都視同介面文字**，一樣受本條約束——`E-52`（`docs/18-work-errors.md`）就是 `AdminClubAuthorizer` 把權限碼內插進 403 訊息、畫面照原樣顯示出來。**已有自動化防呆**：`apps/api/Tcrfc.Api.Tests/UserFacingMessageContentTests.cs`（靜態掃描全部 400/403/409 例外的訊息組成路徑）與`UserFacingMessageHttpContentTests.cs`（代表性端點實打），兩者互補，見兩份檔案各自的類別說明與 `apps/api/README.md`「S1-8 續作」。技術細節（例外堆疊、資料庫錯誤片段）一律只進 log。
  **v3.7 更名九個子模組**：首頁編排｜常見問題｜慈善與社會影響｜媒體專區｜賽程與賽果｜搜尋與 AI 能見度｜推播裝置｜App 設定與連線檢查｜商品與規格。**代號與功能範圍未變。**
- 🤖 **GEO 是正式規格，不是建議**（主站規劃書 **§7 `GEO-01`–`GEO-09`，v3.6，2026-09-18 客戶指示**）。
  **兩個官網各自完整實作一份**：各自的 `llms.txt`（繁中英文各一份，後台 `H` 維護、隨發布重產，**不以人工改檔**）與 `robots.txt`。
  **AI 爬蟲全站允許，但一律排除**：會員中心、七類表單、訂單查詢、`/m/<token>` 會員卡驗證頁、**未成年與學員照片路徑**（藍鯨為 U15／U12；兩站共用前台路由，清單在 `apps/api` `GeoCrawlerDefaults.ClubLocalizedSegments`，**前台新增顯示未成年照片的頁面必須同一次交付補進去**，`E-82`）。
  ⚠️ **這條排除是個資防線，不是 SEO 設定——不得為了「讓 AI 多抓一點」而放寬。** 肖像同意未到位的素材本來就不得公開。
  **重要事實全站單一來源**（`GEO-03`）＋ **結構化資料與明文雙重呈現**（`GEO-04`）；同一事實不得在兩處各寫一份。
  **Schema 缺必填欄位時不輸出該型別**（`GEO-05`），不輸出殘缺 Schema。**不新增資料型別**，設定存 `Setting`。
  ✗ 站內 AI 問答機器人　✗ AI 內容生成　✗ 為 AI 另建 API 或 MCP 端點　✗ `llms-full.txt`　✗ 任何排名相關的驗收標準。
  ⚠️ **慈善捐款平台明文不做 SEO／GEO**，本條不適用該平台。
- 🖼 **後台圖片一律欄位直傳，全系統不設媒體庫**（主站規劃書 **§4.0，v3.5，2026-09-18 客戶指示**）。
  圖片**屬於它所描述的那一筆資料**，是該表自己的欄位組：`<名稱>_key`（物件儲存鍵）＋ `_width`／`_height` ＋ `_alt_zh`／`_alt_en`。多圖用子表，每列一組欄位加 `sort_order`。
  **選檔只在瀏覽器端產生預覽，按下「儲存」才寫入 blob**；寫入成功才更新資料列，取消表單不留下任何檔案。**新圖寫入成功後才刪舊物件**，刪資料列一併刪圖。
  **兩道驗證**：前端擋副檔名／大小／尺寸，**伺服器端一律重新驗證**（格式以實際檔頭判定，不看副檔名）。
  🔴 **上傳即縮圖，原始檔不保留**（v3.9）：伺服器端寫入時**一律重新編碼**——依 EXIF 轉正 → 長邊超過 **2560px** 等比縮小 → 轉 **WebP** 存為主檔，
  同時**移除 EXIF（含 GPS 位置）**。主檔之外固定產 **1280／640／320** 與**後台列表用 160px 方形縮圖**，鍵由主鍵推導、**不另存欄位**。
  **單檔上限 10 MB**；尺寸低於版位下限**擋下不收**，不放大補齊。刪除或換圖時**主檔與全部衍生檔一起刪**。
  ⚠️ **欄位組的 `_width`／`_height` 存的是主檔（縮過之後）的尺寸**，不是上傳檔的原始尺寸。
  ⚠️ **前台不得直接引用主檔**，一律依版位挑尺寸（`srcset`）、必帶寬高、首屏外延遲載入。
  ⚠️ **`MediaAsset`／`MediaFolder`／`MediaUsage` 不存在**，看到 `media_asset_id` 外鍵一律是錯的（已知 10 處：`Article` 封面、`Banner`、`Partner`／`Sponsor` 雙色標誌、`ProposalFile`、`ProductImage`、`ComicPage`、`Charity` 標誌）。**一張圖只屬於一筆資料列**，同一張要出現兩處就上傳兩次。
  7.8 媒體專區改由 **`PressResource`** 承載，後台模組是 **`B6 媒體資源`**；`B` 模組為 **B1 頁面／B2 新聞／B3 Banner／B4 FAQ／B5 慈善／B6 媒體資源**。
- 🔴 **前台選單固定、後台沒有選單管理**（主站規劃書 v3.22，2026-10-07，使用者拍板）：主選單、Mega Menu、行動版選單、Footer 選單由前台版型寫死，後台（`I` 網站設定）不得有新增、刪除、排序、改連結的畫面；兩站一體適用。後端 `menus` API、`menu_items`／`menu_items_i18n` 資料表與 `site.menu.*` 權限碼**已於 2026-10-07 移除**（EF 遷移 `ClubMenuItemsDropContract`、`db/migrations/20261007_menu-items-drop_2-contract.sql`）；不得再建回，前台不得再從 API 讀選單。看到「選單管理」「`I2`」「Mega Menu 拖曳排序」一律是 v3.21 以前的寫法。
- 🔴 **B1 是固定頁、固定欄位**（主站規劃書 v3.21，2026-10-07）：不新增／刪除頁面，欄位由版型定、後台只填值、不能新增／刪除／調整欄位順序，只有標示「可增刪列」的欄位能增減條目；`page_blocks` 資料表結構不變。看到「區塊化編輯器」「12 種區塊」「可自由新增頁面」一律是 v3.20 以前的寫法。
- 🔴 **B1 頁面的區塊內容（`page_blocks.content`）雙語靠 JSON 內的巢狀物件，不是側表**（S1-4，2026-09-24）：
  `db/club-schema.sql` 已明確拒絕 `page_blocks_i18n`（「主表已放的欄位優先」），所以每一個使用者看得到的
  自由文字欄位本身要是 `{"zh": "非空白字串", "en": null|"字串"}` 這種巢狀物件（CLAUDE.md 全域規定 4，
  英文可空但鍵一定存在），不是像 `articles_i18n` 那樣整列各自一個語系。**網址、識別碼、日期、數值**這類
  非語言內容維持單一純值，不套用這個規則。**圖片替代文字例外**：沿用既有後台圖片上傳通則的扁平
  `altZh`／`altEn` 兩個鍵，不是巢狀物件——兩種雙語形狀刻意並存，見
  `apps/api/Features/AdminPages/PageBlockContentProcessor.cs` 檔頭。**日後任何「只存不查」JSON 欄位
  需要雙語時，先確認資料庫是否已經拒絕建側表**（`docs/12` §1 第 3 條「主表已放的欄位優先」），
  拒絕了就走這個巢狀物件慣例，不要另外發明第三種雙語形狀。
- 🔴🔴 **多個 agent 並行時，任何 agent 都不得執行改動工作樹或索引的 git 指令**（`git stash`、`git checkout -- <path>`、`git reset`、`git restore`、`git clean`、`git switch`），只能用唯讀指令（`git diff`、`git show HEAD:<path>`、`git log`）或另開 `git worktree`（`docs/18` `E-281`，2026-10-06：一次 `git stash` 收走 170 個未提交檔案）。主流程派工時，prompt 必須寫明這條禁令。**2026-10-07 起本機以 `.claude/settings.json` 的 `permissions.deny` 機制擋下 `git stash`／`reset`／`restore`／`clean`／`checkout --`／`rm`／`switch`／`pull`／`rebase`**（`.claude/settings.json` 已納版控）。
- 🔴 **前台看得到的文字欄位一律要有 zh／en，判準是「前台會不會顯示」，不是「規劃書有沒有逐表標雙語」**（`docs/18` `E-280`，2026-10-06）：全域規定 4 是通則。新增或審查資料表時，凡是前台會顯示的自由文字（名稱、名次、隊名、對象、規則說明、標題），一律放進 `*_i18n` 側表（`zh-Hant` 必存、`en` 可缺），不得以「規劃書沒寫雙語」為由留在主表單一欄位。
- 🔴🔴 **寫入 `json` 欄位的內容必須是 JSON 物件或陣列，輸入驗證不符一律回 400，不得讓它走到資料庫變成 500**（`docs/18` `E-111`，2026-10-01）：正式環境（Azure SQL）的 `json` 是**原生型別**，字串、數字、`true`／`false`、`null` 字面值、空字串全部被拒（`Msg 13609`）；本機 2022 與預設測試用的 `nvarchar(max)` 什麼都收，**只在 2022 上綠不代表正式環境能寫**。共同守門在 `apps/api/Common/JsonColumn.cs`（`IsObjectOrArray`）：空白視為 `NULL`、其餘必須是物件或陣列。**自由文字**存進 json 欄位要包成物件（`JsonColumn.WrapText` → `{"text":"…"}`，如 `partner_stores.business_hours`；新聞內文 `articles_i18n.body` 收純文字時同樣包成 `{"text":"…"}`、物件／陣列輸入原樣存為區塊編輯器保留，用 `NormalizeTextOrStructured`），**所有讀取端一律 `UnwrapText` 還原，對外契約維持純文字**（相容舊字串純量）；**外部原始回應**（金流）用 `CoerceToObject`。新增或改動 json 欄位的寫入路徑，要在 `JsonColumnTests` 補測，並跑 `apps/api/scripts/native-json-test.sh`（SQL Server 2025 原生 json 容器＋原樣 DDL）。12 個 json 欄位清單見 `docs/20` §5。
- 🔴 **`players`／`staff.portrait_consent_status` 預設值永遠是 `'not_consented'`，改成別的預設值是個資事故**
  （S1-8，2026-09-24；藍鯨規劃書行 198／321：「球員照片須有肖像同意（未成年須監護人同意）。同意未到位
  的球員不顯示照片，以預設圖或純文字卡呈現——不得放假圖」；主站規劃書行 1361／1691 為既有的未成年
  素材處理原則）。**公開讀取 API 必須依這個欄位擋 `photo_key`**：非 `consented`／`consented_by_guardian`
  一律不得回傳球員或教練照片。這條同時是 `GEO-02`（AI 爬蟲排除未成年學員與球員照片路徑）能落地執行
  的資料前提——沒有這個欄位，「排除未同意的素材」無從查詢起，見 [`12-database-schema.md`](12-database-schema.md#12-踩雷點) 第 32 點。
  ✅ **（2026-09-24 客戶裁決）後台不建同意書檔案留存或覈實流程**，只保留這個狀態欄位由操作者手動設定，見 [`15-out-of-scope-record.md`](15-out-of-scope-record.md)。
- 🔴 **`GEO-02` 強制排除的學員照片以「頁面＋圖片目錄」兩層防護**（2026-09-30 主 session 裁決）：會收件或整頁為未成年影像的頁面（`academy/teams/`、`academy/life/`、`programs/{childrens-training,summer-camp,specialist}/`）整頁排除；只在頁首當背景用學員照片的學院行銷頁**不整頁排除**（避免學院頁無法收錄），改排除圖片目錄 `/assets/img/academy/`、`/assets/img/programs/`（不分語系、兩俱樂部皆適用，`GeoCrawlerDefaults.StaticExcludePaths`）。排除套在 `User-agent: *`，圖片搜尋也不索引。前台新增學員照片目錄時須同次補進清單。
- **隊別代號**：`D1`（磐石一線隊）／**`BW1`（藍鯨一線隊）**／`U15`／`U14`／`U12`。
  代號**維持全站唯一**（它是行事曆訂閱網址與 `/zh/schedule/d1/` 的識別鍵，**不得改成「俱樂部 × 代號」複合鍵**）。對外顯示磐石寫 `First Team / 一線隊`、藍鯨寫 `Blue Whale First Team / 藍鯨一線隊`。
  **`Team.type` 的 `women` 值已於 v3.0 廢除**，改用獨立的 `Team.gender`（`men`／`women`／`mixed`）——性別是球隊屬性不是隊型。`type = first_team` 由「全站僅一筆」改為「**每個俱樂部至多一筆**」。
  ⚠️ **藍鯨的 `U15`／`U12` 與磐石學院的 `U15`／`U12` 是不同球隊**，以 `club_id` 區隔。
- **品牌色**：桃紅 `#E0218A`（PANTONE 225 C／C5 M90 Y0 K0）＋ 品牌黑 `#231916`（K100，**暖調近黑，不是深藍**）。
  唯一來源是 [`reference/TCR_logo_CMYK.ai`](../reference/TCR_logo_CMYK.ai)。小字情境改用 AA 安全版 `#D61E83`。
  **台中藍鯨的品牌色（2026-09-14 定案，客戶指示「配色按照 logo 去抓」）**：隊徽藍 `#2196D5`（對白 3.29:1，**只能用於大字與色塊**）
  ＋ 隊徽黑 `#040000`；**小字一律改用 AA 安全版 `#1A78AA`**（4.87:1）。唯一來源是 [`brand/blue-whale/bw-crest.png`](../brand/blue-whale/README.md)，
  推導出的七個變數值寫在**三處且必須一致**：藍鯨規劃書 §8.1、[`docs/06`](06-conventions.md) §2、[`brand/blue-whale/README.md`](../brand/blue-whale/README.md)。
  🔵 **後台主色也依目前操作的俱樂部切換**（使用者 2026-09-30 裁決，主站規劃書 v3.16 §4.0 站台切換器）：切到藍鯨用上述藍鯨色、切回磐石用桃紅，**色值沿用兩隊已定案品牌色、不另立**。此條**推翻** [`docs/21`](21-admin-ui.md) §5.1／§13.3 的「切換站台不換主色」。
  **不自行配色**——主色與深色是隊徽上的實際用色，其餘四個由主色依對比度需求推導。
  🔴 **印刷色票（PANTONE／CMYK）仍未提供，印刷品不得依這組 RGB 發包。**
  🔒 **後台不提供品牌設定**（使用者 2026-10-06，主站規劃書 v3.20）：兩個官網的標誌、Favicon、品牌主色與輔助色**只由前台靜態資產與 `tcrfc.css` design tokens 定義**，後台沒有任何入口可改；`Club` 型別不存標誌、Favicon、品牌色。**後台介面自己的主色切換不在此限**。App 的兩隊標誌與品牌色為內建資源。
- **名稱寫法**：中文簡稱一律「**台中磐石**」，不單獨用「磐石」。英文名一律 `Taichung Rock FC`
  （全稱 `TAICHUNG ROCK FOOTBALL CLUB`）。舊稿的 `Taichung Cornerstone RFC` 已汰換，看到視為錯誤。
  **行動 App 對外名稱是中性的「台中足球 / Taichung Football」**（兩隊共同品牌），**但官網與所有主站文件仍是 TCRFC**——這兩者不衝突，是刻意的分工。
  **藍鯨的中文一律寫「台中藍鯨」**，不單用「藍鯨」（比照磐石的規則）。🔴 **藍鯨的英文名稱全系統只有兩種寫法（客戶 2026-10-05 定案，B-5 英文名部分解除）：簡稱 `Taichung Blue Whale`（一般內文、標題、導覽）、全名 `Taichung Blue Whale Women's Football Club`（Schema、`llms.txt`、頁尾、法律條款、首次提及）。** 不得寫成 `Taichung Bluewhale`、`Taichung blue whale`、`Women's Football Team` 結尾、只寫 `Blue Whale` 等任何變體；程式一律取 `club-copy.ts` 的藍鯨名稱常數，不得在頁面或後端另寫字面值，`apps/web/scripts/check-bw-en-name.mjs`（掛 `npm run lint`）負責擋。
  **藍鯨的法人歸屬與營運關係見 [`15-out-of-scope-record.md`](15-out-of-scope-record.md)——那些不寫進規劃書。**
- **英文版（C-6）只覆蓋主站**：`useLocale().isEn` 僅主站 `/en/` 為 true，藍鯨 `/en/` 維持繁中備援＋提示；`enReady` 對藍鯨無效（layout 一律顯示提示）。**英文值一律來自 `club-copy-en-*.ts`／頁面 `tx()`，不得在 `club-copy.ts` 加英文；取用後端英文欄位要過濾中文回退**（E-213）；英文用詞照 [`06`](06-conventions.md) §1.1，待確認名詞不得自挑（E-210）。
- **標誌**：只用 [`brand/svg/`](../brand/svg/) 由 `.ai` 萃取的三種組合（隊徽／隊徽＋TCRFC 英文版／隊徽＋TCRFC＋台中磐石足球俱樂部 中文版）。
  **不得自行排字、不得加 `SINCE` 或年份。**
- **雙語**：繁中（預設）＋英文，URL 以 `/zh/`、`/en/` 區隔，架構須預留第三語系。
- **主站不做的事**：票務與門票套票；會員點數與電子錢包；特約店家掃碼核銷；**站內捐款與志工報名**（v2.1 移出）；**系統隨機開獎、前台抽獎頁與「我的抽獎」、中獎通知信**（v2.3）；**兩隊之間的分潤與結算**（v3.0，走線下合約）；**購物車跨俱樂部混買**（v3.0）。
  **站內電商已不在此列**（v2.6 改為自建）。**「女足名單與賽程不做」也已不在此列**（v3.0 改為藍鯨建於本資料庫、由藍鯨官網呈現）。
- **站內商店（v2.6，主站自建電商，規模比照舊 Wix 商店）**：前台 8.3、後台 `S1–S6`（S1 商品與規格／S2 庫存／S3 訂單／S4 出貨物流／S5 退貨退款／S6 設定與報表），**商品與規格 一律在 `S1` 維護**（規劃書中沒有 `ProductShowcase`；⚠️ **`E4` 現在是「廣告主與版位管理」**，看到舊文件寫「E4 商品櫥窗」一律視為錯誤）。
  商品、購物車、結帳、**LINE Pay 付款**、**電子發票**、物流、訂單查詢、退換貨**全部站內，收款主體是俱樂部**。**支援非會員結帳**（`Order.member_id` 可為空）；**不需要 SSO**（會員帳號即商店帳號）。`OrderItem` 是**值複製快照**，改名改價不得動歷史訂單。
  **付款只有 LINE Pay**（不做信用卡、超商代碼、ATM、貨到付款）；**必開電子發票**（載具／統編／捐贈碼，抬頭俱樂部，退貨作廢或折讓；**LINE Pay 本身不開發票，須另接發票服務**）。
  **本期不做**：會員價與折扣碼（**付費會籍權益不含商品折扣**）、運費級距（只有固定運費＋免運門檻）、訂閱制、跨境多幣別、多商家、二手寄售、點數折抵、票務、以商品綁定會籍或抽獎資格、物流商 API、商品評論。
  **商店只賣實體商品**：**會籍、課程費、捐款都不走商店結帳**；**不得販售捐款品項**、**不得販售抽獎機會或以消費增加中獎機會**。與慈善平台**商店號、金流帳號、發票字軌一律分離**。
  **不做**：訂閱制、跨境與多幣別、多商家平台、二手寄售、點數折抵、票務與門票套票、物流商 API 串接。**Shopify 不再是導流目標**，舊官網 Wix 商店是過渡通路、上線後停售並 301 轉址。
- **三條「不做」現在都有適用範圍，別再當全域禁令用**（v2.5、v2.6）：
  - **金流（v2.6 起四分，v3.0 加收款主體的說明）**：主站網頁的**商店結帳接**（俱樂部收款，僅限實體商品）；**會籍與課程費維持不接**（收款連結與現場收款）；**慈善平台接**（協會收款，**v2.0 起獨立資料庫**）；**行動 App 接**（俱樂部收款，**僅限會籍費用**，⚠️ **首個上架版本不含 App 內付款**，改外開瀏覽器導回官網）。「不存卡號」全域適用，結帳一律導轉金流商代管頁面或 SDK。
    ⚠️ **「受不受 LINE Pay 出口 IP 約束」的判準是通路，不是「會籍 vs 商品」**——看的是有沒有我方伺服器對 LINE Pay 發出 API 請求。對照表見 [`17-deployment.md`](17-deployment.md) §3。
    🔴 **呼叫 LINE Pay API 的執行環境必須有固定出口 IP**——正式環境須在商店管理後台登記付款伺服器 IP，**兩個商店號（俱樂部、協會）各自登記**，登記同一個 IP 合法。**改機器＝改白名單，是有前置期的變更，等同停機事件。**`confirmUrlType` 一律用 **CLIENT**，不需另開放連入白名單。見 [`17-deployment.md`](17-deployment.md) §3。
    🔴 **Azure 基礎設施只用 Incremental 部署，絕不用 Complete mode**（會刪掉模板沒寫的資源，例如手動建的部署身分）；**靜態 Public IP、SQL 伺服器、兩個儲存體帳戶有 `CanNotDelete` 鎖**，要刪先由擁有者移除鎖，**刪 Public IP 前先處理 LINE Pay 白名單**。Bicep 不得寫死任何網域。見 [`17-deployment.md`](17-deployment.md) §13、[`../infra/README.md`](../infra/README.md)。
    **v3.0：藍鯨的商品與會籍採「代收代付」**——收款主體仍只有俱樂部，單一 LINE Pay 商店號、單一發票抬頭，事後依 `selling_club_id`／受益俱樂部加總分帳。**系統不做兩隊分潤、不建結算單。** ⚠️ **代收代付的稅務認定未完成**（銷貨還是受託代銷？），**未確認前不得開發藍鯨結帳流程**。
  - **廣告版位**：主站網頁不設固定版位；**行動 App 做自售版位含成效統計**。**慈善版位網頁與 App 皆不做**。
    🔴 **App 離線時不得量測廣告曝光**（App 規劃書 §2.4 硬規則 1）：連不上即時端點、改用預載素材或備援素材時，顯示照常但**不產生曝光與點擊計數事件**；只有「線上已量到、尚未送出就斷線」的事件可暫存並帶原始發生時間補送（伺服器拒收超過 24 小時者）。預載只為加速載入，不是離線計數的依據（2026-10-05，`E-164` 再犯時寫入）。
  - **推播與通知中心**：主站網頁不做（含 LINE 推播）；**行動 App 做原生推播與 App 內通知中心**。系統信維持五封。
- **報名歸戶只解除兩項**（v2.5）：App 提供**表單自動帶入**與**我的報名**（僅限會員本人資料）；**學員家長綁定網頁、App、後台三方皆維持不做**。`Registration.member_id` 可為空——非會員仍可報名。
- **會員系統只做會籍，但 v3.0 起是「一人每俱樂部一份」**：免費與付費兩層，會員享特約店家折扣，付費會員（＝球迷會員 `fan_club`，不是另一種身分）另獲球衣與**抽獎資格**。
  **`Member` 是一人一組帳號、不分俱樂部**（Email 是登入鍵、LINE 綁定 1:1、個資法上的當事人是人不是會籍）；**俱樂部維度在新的 `Membership` 型別上**。`Member` 的 `tier`／`membership_start_on`／`membership_end_on` 三欄**已移入 `Membership`**——**不要用「`Member` 保留三欄當快取」的折衷**，那是兩個真實來源。
  **會員卡改為每份會籍一張**（`MemberCard.membership_id` 必填）。**「一張卡只有一組 token」沒有變**，但它約束的是**卡與 token 的 1:1**，不是人與卡的 1:1（`card_quota` 家庭方案本來就可以是 3 張）；驗證頁 `/m/<token>` 的回傳欄位**一欄未改**，**不得新增「適用球隊」欄位**。
  **兩隊球季不同步**，到期日與續會提醒各自計算——這讓人工開通全年散布且件數翻倍。
  會籍採球季制，會費走 LINE Pay 收款連結與現場收款，**不走商店結帳**；**藍鯨會籍採代收代付**（收款主體仍是俱樂部）。**不做**報名歸戶、表單自動帶入、學員家長綁定、通知中心、LINE 推播、Google 登入。
- **付費會員抽獎（v2.3，後台 K5）**：資格是**布林值**——基準時間會籍有效即**自動具備，會員零操作**，一人一號，不因消費／簽到／分享／參加次數增加機會（**所以不是點數**）。
  系統**只做名單快照、序號配發、CSV 匯出**；**實體開獎由人工在現場或直播進行**，中獎人後台回填。中獎**只以最新消息公布**（7.1 ＋標籤，遮罩），系統信維持五封。
  獎品為**實體物品**人工寄送或現場領取（**所以不碰票務與掃碼核銷**）。`DrawRoster` 是**不可變快照**，值複製非外鍵，不得改成即時 query。
- **慈善捐款平台**：**主辦與收款主體是「台灣足球策略發展協會」，不是台中磐石**（v1.4）——前台標誌、發票與收據抬頭、系統信署名、對帳單一律為協會，俱樂部只剩導流來源與後台宿主，分潤裡的留存方是**協會留存**（`association_amount`）。**協會品牌資產尚未提供，一律留佔位，不得沿用 TCRFC 標誌、不得自行造標**；英文全名待確認。
  協會身分：社會團體，內政部**台內團字第 1150283692 號**，2025-10-08 成立，會址臺中市潭子區勝利三街 95 號。**立案 ≠ 法人登記**——勸募團體須是公益性社團法人，法人登記與統編未確認前**不得公開勸募、不得印 QR**。負責人姓名與出生日期屬個資，只留在 [`reference/`](../reference/)，不得寫入任何文件。
  **v2.0 起為完全獨立的系統——獨立網域、獨立前台、獨立後台、獨立資料庫**（`N` 模組與 8 張捐款表已移出主站，自建約 22 張表）。**法遵風險因此下降**：協會自己控管自己蒐集的資料，委託處理範圍縮小為單純的維運服務。
  ⚠️ **主站 `B6` 的 `Charity`／`CharityProgram`／`ImpactRecord`／`ImpactMetric` 留在主站作為主檔**，慈善庫只有唯讀快照，**兩邊不同步時以主站為準、不得即時 join**。**主站系統信由 13 封降為 9 封**（會員 5 ＋ 商店 4）。
  🔴 **慈善 API（CH-2／CH-3，2026-10-01，`apps/api/CharityPlatform/`）七條不能改壞的規則**（細節與契約見 `apps/api/README.md`「慈善 CH-2／CH-3」，其中 ①③⑤⑥ 有 `CharityArchitectureTests` 自動守著）：
  ① **獨立帳號體系**：慈善後台用自己的 JWT 方案（`CharityAdminBearer`，issuer／audience／金鑰 `JWT_SIGNING_KEY_CHARITY` 都與主站不同）與自己的更新權杖 Cookie **`__Host-tcrfc-charity-admin-rt`**——兩個後台打同一個 API 網域，Cookie 同名會互相蓋掉登入，**不得改成與主站同名**；
  ② **假金流／假發票／假寄信只在 `Development`（或明確 `CHARITY_ALLOW_FAKE_PROVIDERS=true`）運作**，正式環境一律「尚未設定」——假實作對任何交易回扣款成功，在正式環境等於憑空確認收款；正式實作到位時換掉 DI 註冊，不得移除這道防線的測試；
  ③ **冪等**：建單冪等靠「單號由冪等鍵 HMAC 推導＋`UQ_donations_order_no`」（資料庫沒有冪等鍵欄位，不得為此自己發明欄位）；所有狀態轉移是條件式 `UPDATE … WHERE status IN (…)`，只有贏的請求才入帳、寄信、開票；**慈善完全不讀快取**；
  ④ **「已扣款但 Confirm 失敗」＝捐款單 `pending` ＋最近一次付款 `failed`**：此狀態**禁止重新發起付款**（會重複扣款）、**逾時工作不得轉成 `expired`**、**不得當成失敗**，只能由人員（`recheck-payment`）或使用者再次確認處理；金流確認成功後的寫入一律不可被請求取消中斷；
  ⑤ **個資只在 API 層遮罩**（姓名、Email、收據抬頭、身分證字號、載具、地址），明文需 `reveal` 權限且**每次寫稽核**；退款、分潤百分比設定、含個資匯出三類操作的稽核與變更在**同一次 `SaveChanges`**，`audit_logs` append-only；
  ⑥ **慈善與主站程式碼互不相依**（`CharityPlatform/` 不引用 `ClubDbContext`／主站 `Features`，主站不引用 `CharityPlatform`），命名空間不得叫 `Tcrfc.Api.Charity`（會遮蔽主站的 `Charity` 實體）；
  ⑦ **`DATA_PROTECTION_KEYS_PATH` 遺失＝已加密的捐款人身分證字號永久無法解密**（比主站 2FA 嚴重：捐款人不登入，無從補填），正式環境務必持久化或改接 Key Vault。🔴 **`docker-compose.yml` 的 `api` 必須設 `DATA_PROTECTION_KEYS_PATH` 並掛持久化目錄**（現為 bind mount `/opt/tcrfc/data-protection`，擁有者 uid/gid 1654、`700`，`create_host_path: false`）——程式沒讀到這個變數**不會報錯**，容器重建就全部解不開（E-109）；目錄不可放 cloud-init（`customData` 不可變），由 `infra/provision-secrets.sh` 建立；金鑰每 90 天輪替，備份要定期重做（`infra/README.md` §4.3）。改 compose 的 `api` 服務時不得拿掉這個掛載。
  合作店家貼 QR → 客人掃碼看到店名 → 選捐款項目 → 讀說明 → 頁尾以 **LINE Pay** 捐款 → 自動開**電子發票或捐贈收據**（依項目設定）。
  捐款人**不登入不註冊**，只填姓名與 Email。**店家分潤與項目分潤相加、分別入帳**，系統結算並產對帳單，**實際匯款人工執行**。**不做店家帳號、不做掃碼核銷、不做 SEO／GEO 優化**（v1.1；入口是 QR 掃碼，但頁面仍維持可被索引）、**前台不做募款進度**（v1.2；無進度條、目標金額與捐款筆數，數字只在後台 N6 報表）、**對外不受理退款**（v1.3；但後台保留人工退款供誤捐個案，發票作廢折讓依稅法保留，項目不得附回饋品）。
- ⛔ **正式庫（Azure SQL）絕不灌 `db/seed/` 的種子資料**（2026-10-01）：種子含 mockup、【測試】資料與種子超管 `sa@system.local`／`Admin@123`。正式庫首次初始化只用 `db/prod/*-reference-data.sql`（允許清單制，新增的種子區段預設不進去），第一個管理員由 `deploy/prod-db-init.sh create-admin` 互動建立；`verify` 會檢查沒有測試帳號。**正式庫的 EF 基準不能用 `dotnet ef database update` 標記**（`InitialBaseline` 之後的 migration 有真的 `ALTER`），由同一支腳本直接寫 `__EFMigrationsHistory`。見 `docs/20` §5、`infra/README.md` §4.8。　🔵 **2026-10-03 使用者裁決：正式庫允許管理員帳號名就叫 `sa@system.local`**，因此「種子帳號誤灌」的偵測不得再依帳號名稱，`prod-db-init.sh verify` 改比對密碼雜湊是否等於種子雜湊。⚠️ 代價：帳號名公開，只剩密碼一道防線（後台未強制 2FA），密碼須高強度。
- ⛔ **後台編輯頁的欄位錯誤規則**（2026-10-06，S2-20，細節見 [`21`](21-admin-ui.md) §3.5–§3.8）：① **欄位鍵不得出現在畫面上**（`nameZh`、`blocks[2].bodyEn` 只用於前後端對應與 `data-field`），對不到欄位的錯誤只顯示 `detail` 訊息 ② 前端**不得再用單一字串 `formError` 做驗證**——`validate()` 回傳 `Record<鍵,訊息>`，一次檢查全部，`replaceAll`→`focusFirst`；頁首提示只留給沒有欄位歸屬的錯誤（`check-edit-layout.mjs` 守） ③ **後端新增欄位類驗證時必須帶 `field`**（`IFieldApiException`，`ArchitectureTests` 守所有 `*ValidationException` 實作介面），鍵＝前端 form 的邏輯名稱、雙語 `xxxZh`／`xxxEn`、日期範圍標在結束欄、上傳用圖片邏輯名 ④ 並行改後台時型別檢查用 `npm run typecheck`（＝`vue-tsc -b`，與 build 同範圍），不要用 `vue-tsc --noEmit`（[`18`](18-work-errors.md) `E-275`）。
- ⛔ **並行派工時，agent 不得執行會改動整個工作樹的 git 指令**（2026-10-06，[`18`](18-work-errors.md) `E-214` 同類第二次升級）：`git stash`、`checkout`／`switch`、`reset`、`restore .`、`clean`、`pull`、`rebase` 會把**另一個 agent 正在寫的檔案**一併收走或覆寫，失敗時無聲遺失。要比對改動前的基準，用 `git diff`、`git show HEAD:<檔案>`，或另開 `git worktree`；**主 session 派出兩個以上 agent 時，派工提示必須寫明這一條**。
- ⛔ **瀏覽器實機驗收用的本機 `apps/api` 由使用者啟動，agent 不自行啟動**（2026-09-25 使用者裁定）：agent 在指令中帶資料庫密碼啟動 API，曾被 session 的安全防護擋下。之後的做法是：使用者在自己的終端機把 API 跑在 `http://127.0.0.1:5299`（指令見 `apps/api/README.md`「直接用 dotnet 跑」），agent 只連上去驗收。agent 開始實走前，先用 `curl -s http://127.0.0.1:5299/api/v1/tcrfc/schedule` 確認 API 真的在回資料（`/healthz` 不碰資料庫，不算數）；沒在跑就標「未驗證」並回報，**不得自行啟動、不得以任何方式讀取或傳遞資料庫密碼**。`dotnet test` 不受影響（現在跑在 `tcrfc_club` 上，照舊由 agent 執行；但測試會動到同一個庫，使用者實走進行中請先確認再跑，見下方 `S0-13`）。
- ⛔ **本機 API 設定檔 `apps/api/appsettings.Development.json` 不入版控**（2026-09-30 使用者裁決）：內含 SA 密碼與 JWT 金鑰，已列入 `.gitignore` 與 `apps/api/.dockerignore`（不得被 COPY 進映像檔）；納管的只有佔位範本 `appsettings.Development.example.json`。實際檔由 `apps/api/scripts/init-local-settings.sh` 從 `.env` 與 `deploy/dev/club.env` 產生（不覆寫既有檔、不印出密碼）。環境變數仍優先於檔案，Docker 與正式環境不變。`dotnet test` 只從該檔補 `CLUB_SQL_CONNECTION_STRING`（`TestLocalSettings`），「只准連 `tcrfc_club`」檢查不變。⚠️ 此檔勿加入 `REDIS_HOST`／`AZURE_BLOB_CONNECTION_STRING`：測試主機用 `Environment=Development` 也會讀它，會改變各 fixture 刻意清空這些值的假設。
- ⛔ **未成年學員不輸出結構化資料（`Person` JSON-LD）**（2026-09-25，`S1-12f`）：學院梯隊（U15／U14／U12，藍鯨 U15／U12）等未成年學員的姓名與照片，不得整理成 JSON-LD 給搜尋引擎或 AI。這是 `GEO-02`「未成年素材是個資防線、不得放寬」的延伸；`schemaEligible` 只判斷欄位齊不齊，**不判斷該不該公開**，不能拿它當這條的防線。成人（一線隊、教練、職員）照常輸出，但仍要走肖像同意。
- ⛔ **整合測試寫入資料庫的資料，一律在 `finally` 清掉，並斷言清理成功**（2026-09-25，`E-62`）。沒清的資料會跨次累積，累積到某個門檻就讓別支測試失敗（例：搜尋無結果排行取前 50 名）。無法刪除的單例設定（如 `settings`）要在測試前記下原值、測試後還原。**測試失敗時不得只用 `git stash` 對照就判定為「既有失敗」**——`stash` 不會還原資料庫。
- ✅ **`S0-13`（2026-09-25 建立、2026-09-30 使用者裁決改寫）：本機只保留兩個資料庫——網站 `tcrfc_club`、慈善 `tcrfc_charity`；不再有獨立測試庫。** 沿革：2026-09-25 曾為 `dotnet test` 另建 `tcrfc_club_test`（因測試與本機開發／無頭瀏覽器實走共用 `tcrfc_club_dev` 時，同一天發生三次互相干擾：測試把實走中帳號的 2FA 狀態、`settings` 的 SEO 值重置回種子）；2026-09-30 使用者裁決合併，`tcrfc_club_dev`、`tcrfc_club_test`、`tcrfc_charity_dev` 一律廢除，**自動測試直接跑在 `tcrfc_club` 上**。🔴 **已知代價（使用者已被告知並接受）**：測試會改動後台看到的資料，中途失敗可能留下殘骸；需要時重灌種子即可，**測試與無頭瀏覽器實走不要同時進行**。**現行做法**：`set -a; source .env; set +a` → `./db/seed/setup-club-db.sh [--recreate]`（一鍵 DDL＋種子，見 `db/seed/README.md`「本機網站庫與整合測試」）→ `CLUB_SQL_CONNECTION_STRING` 的 `Database` 指向 `tcrfc_club` → `dotnet test`。**兩道防呆（只准連本專案庫）**：①`deploy/local-ddl.sh` 白名單只有 `tcrfc_club`／`tcrfc_charity`，`db/seed/apply-seed.sh`／`setup-club-db.sh` 只認 `tcrfc_club`，`apply-charity-seed.sh` 只認 `tcrfc_charity`；`DROP DATABASE` 只在 `--recreate` 路徑且只對 `tcrfc_club`；②`apps/api/Tcrfc.Api.Tests/Fixtures/TestDatabaseGuard.cs`——`CLUB_SQL_CONNECTION_STRING` 的資料庫名稱沒有精確等於 `tcrfc_club` 時（含容器內其他專案的資料庫、`tcrfc_charity`、舊名稱），六個 fixture 一律在 `InitializeAsync()` 直接拒絕啟動。**正式環境（Azure SQL）與 CI 用完即丟的容器不受影響**（CI 內同樣建 `tcrfc_club`）。細節見 `apps/api/README.md`「怎麼跑」、`deploy/README.md`「本機只保留兩個資料庫」（含遷移與刪舊庫步驟）。
- ⛔ **依訪客 IP 計算的濫用防護（限流），只信任明確列舉的固定 IP，不信任整個網段**（2026-09-25 立，`S1-10`；**2026-09-29 由 S1-17 修正——舊版本行文已過期，見下段**）：
  API 只信任 `TRUSTED_PROXY_IPS`（複數，逗號分隔）明確列出的來源轉來的 `X-Forwarded-For`。
  🔴 **舊規則已撤銷，不要再引用**：S1-10 當時的規則是「前台的公開表單送出一律由瀏覽器直接呼叫公開 API 網域，不得經由 Nuxt 伺服器端（`NUXT_API_INTERNAL_BASE`）代轉」——**這條已被 S1-17（10 表單中心改走 Nuxt 伺服器端代理）與其後端配套（S1-17 修正）取代**：現在允許代轉，前提是代轉來源容器（`nuxt-tcrfc`／`nuxt-bw`）在 `docker-compose.yml` 的 `internal` 網段裡有固定 IP、且該 IP 已列入 `api` 的 `TRUSTED_PROXY_IPS`（目前是 `172.28.238.2`／`.3`／`.4`，對應 `proxy`／`nuxt-tcrfc`／`nuxt-bw`）；**`nuxt-charity` 刻意不在清單內**，若慈善前台日後也要走同一種代轉，必須先幫它的容器配固定 IP 並補進清單，不能假設「跟主站一樣做法」會自動生效。
  仍然成立、不因這次修正改變的部分：**不信任整個 `internal` 子網段**（只信任明確列舉的幾個 IP，同網段其他容器如 `admin-web`／`admin-charity`／`nuxt-charity`／`redis` 一律不受信任）；ASP.NET Core 的 `KnownProxies` 與 `KnownIPNetworks` 兩者皆空時代表「信任所有來源」，不是「都不信任」——**`TRUSTED_PROXY_IPS` 沒設定就完全不掛 `UseForwardedHeaders`**，不要改成「掛上、但清單留空」。細節見 `apps/api/README.md`「S1-17 修正」段與 `apps/api/Security/TrustedProxyConfiguration.cs` 檔頭。
  🔴🔴 **`api` 端信任設定正確，不代表 Caddy 轉出去的標頭就正確**：本機實測發現 Caddy 對 `reverse_proxy` 的 `X-Forwarded-For` 預設行為是「把連線本身看到的直接對端原封加到既有標頭最後面」，不是加已用 `client_ip_headers` 解析過的可信值——正式環境「Caddy 的直接對端」永遠是 **Cloudflare 邊緣節點的 IP**，不是訪客真實 IP。沒有明確處理時，任何略過 Nuxt、直接打 `API_DOMAIN` 的呼叫，`api` 取到的會是 Cloudflare 邊緣節點 IP（全球客戶共用的一個小集合），依 IP 分區限流會把不同訪客誤判成同一人。**修法已落地**：`deploy/Caddyfile`（`Caddyfile.prelaunch`／`Caddyfile.dev` 同步）在 `reverse_proxy nuxt-tcrfc`／`nuxt-bw`／`nuxt-charity` 加 `header_up X-Real-IP {client_ip}`、在 `reverse_proxy api` 加 `header_up X-Forwarded-For {client_ip}`（皆為取代語意，`{client_ip}` 是 Caddy 已用 `CF-Connecting-IP` 優先解析過的可信值）。**改 Caddy 上游設定（新增 `reverse_proxy` 區塊、換一支容器）時，必須連這個 `header_up` 一起帶過去**，不要只複製 `reverse_proxy <upstream>` 這一行就以為設定完整——沒帶到的話不會報錯，只會悄悄退回「共用 Cloudflare 邊緣節點 IP 分區」這個不安全的預設值。完整實測步驟見 `docs/17-deployment.md` §2「Caddy 這一側也要正確設定」。
- ⛔ **公開寫入端點一律限流**（2026-09-29，`S1-18c`）：`apps/api` 底下 `Features/*`（非 `Features/AdminXxx`）的任何非 GET 端點（`MapPost`／`MapPut`／`MapDelete`／`MapPatch`）一律不需要登入，一律要掛 `.RequireRateLimiting(...)`——沒有例外，新增這類端點時要一併決定掛哪一個既有政策（`FormsEndpoints.RateLimitPolicyName`／`Common/PublicRateLimitPolicies.LightInteraction`／`.Submission`）或評估是否需要新政策，不能假設「這只是個計數／記錄用途，不重要」就略過。額度依「業務嚴重度」分類，不是一律套同一組數字：會建立真正業務紀錄的（表單送出、課程報名）用 20 次／5 分鐘；瀏覽數／回饋／零結果搜尋這類輕量互動用 60 次／1 分鐘，見 `Common/PublicRateLimitPolicies.cs` 檔頭與 `apps/api/README.md`「S1-18c」段完整理由。**自動化防呆**：`Tcrfc.Api.Tests/ArchitectureTests.cs` 的 `公開端點的非GET寫入呼叫都必須掛限流政策` 用 Roslyn 語意掃描，忘記掛的話 `dotnet test` 直接失敗。⚠️ **`Features/AdminAuth` 的 `/login`／`/refresh`／`/logout` 雖然呼叫當下也不需要先登入，但仍然刻意排除在「本條」（公開內容端點）之外**——風險模型（帳號枚舉／密碼暴力破解／密碼噴灑／刻意觸發鎖定的阻斷服務）跟本條要擋的「公開內容端點被灌爆資料庫」不同，不是同一套政策。**這個缺口已於同日（2026-09-29，`backend-engineer`）補上，登入端點另有專屬政策，不是完全不設防**：`/login` 掛 `AdminAuthEndpoints.LoginRateLimitPolicyName`（`admin-login`）、`/refresh` 掛 `AdminAuthEndpoints.RefreshRateLimitPolicyName`（`admin-refresh`），跟帳號層級鎖定機制——連續 5 次失敗鎖 15 分鐘——並行、互補而非取代；`/logout` 評估後判斷風險低（只清 Cookie／撤銷單一權杖）刻意不掛。⛔ **額度是可設定值，不是寫死的常數，且預設值是正式環境應有的嚴格數字，不是被測試用量放寬過的數字**（2026-09-29 三度修正：第一版曾經直接把額度寫死成「蓋過既有測試呼叫次數」的數字（40／20），被回饋認為本末倒置——不應該讓測試用量決定正式環境的安全額度——改成第二版；第二版改用環境變數覆寫測試值，又被回饋指出環境變數是行程全域狀態、xUnit 平行執行不同 collection 可能互相踩到彼此的覆寫值，已改成第三版）：`AdminAuthRateLimitOptions.cs` 定義兩個環境變數（`ADMIN_LOGIN_RATE_LIMIT_PERMIT_LIMIT`／`ADMIN_REFRESH_RATE_LIMIT_PERMIT_LIMIT`，皆選填），**未設定時登入預設每 IP 每分鐘 5 次、更新權杖預設每 IP 每分鐘 30 次**（業界常見登入端點限流多落在每分鐘個位數量級）。測試環境改由 `Tcrfc.Api.Tests.Fixtures.TestRateLimitOverrides` 透過 `IWebHostBuilder.ConfigureAppConfiguration` 加入 in-memory 設定來源（不是 `Environment.SetEnvironmentVariable`）——每個 `WebApplicationFactory` 的覆寫值只存在於該實例自己的 `IConfiguration`，不是行程全域狀態；一般用途 fixture 覆寫成寬鬆值，真正要驗證「額度用盡回 429」的測試改用獨立的 `AdminAuthRateLimitTestApiFixture`（設一個很小的專用額度）。`Program.cs` 對應改讀 `httpContext.RequestServices` 解析出的 `IConfiguration`（DI 容器裡 `Build()` 完成後的那一份），不是 `builder.Configuration` 的閉包——已用獨立重現專案實測驗證這個時機正確。🔴 **盤點發現**：本測試組件既有的 `REDIS_HOST`／`CLUB_SQL_CONNECTION_STRING` 等環境變數覆寫，理論上有同一種「不同 collection 平行執行互相踩到彼此」的風險，但目前靠 `Tcrfc.Api.Tests/AssemblyInfo.cs` 的 `[assembly: CollectionBehavior(DisableTestParallelization = true)]`（全組件停用平行化）頂住，尚未實際發生過；這是仰賴一個組件層級全域開關的權宜狀態，不是這批既有覆寫本身該有的隔離範圍——本輪不修（任務指示明文排除），需要時可比照本次做法改用 `ConfigureAppConfiguration`。**自動化防呆**：`ArchitectureTests.cs` 的 `AdminAuth的登入與更新權杖端點必須掛限流政策` 專門鎖定這兩個路由字面值掃描；`AdminAuthRateLimitPoliciesTests` 驗證「未設定環境變數時解析出來的就是嚴格預設值」＋政策本身的額度用盡行為；`AdminAuthRateLimitingTests` 走真 HTTP 驗證兩個政策真的接上路由。**同一次修正**也補了帳號枚舉的時序側錄漏洞：`AdminAuthService.LoginAsync` 原本「帳號不存在」路徑不會跑 Argon2id 比對、比「帳號存在但密碼錯誤」快非常多，即使兩者回應狀態碼與訊息一致仍可被量測時間枚舉——已加入對固定假雜湊值的 Argon2id 比對（`DummyPasswordHashForTimingSafety`）拉平耗時，驗證見 `AdminAuthTests.登入時序安全_帳號不存在與密碼錯誤耗時相近_防止枚舉攻擊`。完整理由見 `apps/api/README.md`「S1-18d」段與 `AdminAuthRateLimitOptions.cs`／`AdminAuthEndpoints.cs` 檔頭。
- ⛔ **會員（前台帳號）權杖與後台權杖不得共用**（2026-10-01，E 批）：會員用獨立的驗證機制（`MemberBearer`）、獨立 issuer／audience（`tcrfc-member`／`tcrfc-member-api`）、獨立簽章金鑰（`JWT_SIGNING_KEY_MEMBER`，未設時由後台金鑰 HKDF 衍生）；兩邊互相打對方的端點一律 401（`MemberAuthTests` 鎖定）。**會員端點的查詢條件一律來自權杖裡的會員 id，絕不接受路由或請求本文傳會員 id**（App 規劃書 §9.3：任何回傳個資的端點只能回傳呼叫者本人的資料，唯一例外是欄位嚴格受限的 `/m/<token>`）。
- ⛔ **假金流與本機寫檔寄信不得進 Production**（2026-10-01，E 批）：`LocalFakePaymentGateway` 只在 Development 預設啟用，`PAYMENT_GATEWAY=fake` 在 `Production` **啟動即失敗**；`LocalFileEmailSender`（信件含一次性權杖）絕不在 Production 註冊。**會籍開通金額一律由伺服器依方案重算，開通以伺服器向金流方確認的結果為準，不接受用戶端回報付款成功**（App 規劃書 §5.4 三條硬規則）。
- ⛔ **冪等／防重複的建立端點必須有「同鍵（或同一人）16 並行 × 多輪」的壓測**（`E-172`）：「先查已存在 → 拒絕，否則插入」的流程，在拒絕前必須先確認「已存在的東西」是不是同一把冪等鍵造成的（是就回原結果 200）；同一人重複報名類的檢查要先鎖父列（`UPDLOCK`）再查。**不同使用者搶有限資源**（庫存、梯次／試訓／活動名額）同樣要壓：剩 1 件／1 名額、16 個不同請求並行多輪，斷言成功數不超過上限、庫存不為負、保留量等於有效訂單件數；名額語意是**硬上限＋候補**（超過進候補，不是拒絕、也不是只警示），庫存是硬上限（超過回 409 `insufficient_stock`）。公開報名端點有 IP 限流（20 次／5 分鐘），壓測名額要直接並行呼叫 repository，不要走 HTTP。只有 6～8 並行 × 1 輪的測試抓不到。偶發紅燈不得當作不穩定測試放著。
- ⛔ **五類資料不得讀快取**（2026-09-18，隨 Redis 納入架構）：**庫存與商品可購買狀態**、**金流回呼的冪等檢查**、
  **會員卡 `/m/<token>` 驗證**、**會籍有效性與訂單付款狀態**、**購物車**。
  會員卡那條是**安全問題**不是新鮮度問題——規劃書明訂會員可重新產生 token 以在卡片外流時自保，讀到陳舊值等於**撤銷機制失效**。
  讀取一律 **cache-aside ＋ SQL fallback**（不得讓 Redis 成為唯一讀取路徑），寫入一律 **write-invalidate**（先寫 SQL、成功後刪 key）。
  **慈善平台完全不接快取。** 清單與規格依據見 [`17-deployment.md`](17-deployment.md) §4。
  ⛔ **部署**：Redis 是 compose 的一個容器，**不是 `apt install` 在主機上**；
  ⛔ **compose 絕對不要寫 `ports:`**——Docker 發布連接埠會繞過 UFW 直接改 iptables，主機防火牆擋不住（`docs/17` §1）。
  ⛔ **實作五條**（`docs/17` §4）：**Redis 連線失敗必須回源不得讓請求失敗**（斷線與 cache miss 是兩回事）；
  **失效用嵌在 key 裡的版本號遞增，絕對不得用 `KEYS` 掃**（會阻塞整個 Redis）；
  **每個 key 一定要有 TTL 兜底**；**single-flight**（per-key 鎖，同一個 key 的併發 miss 只讓一個打 SQL，
  Basic 層 5 DTU 上是硬需求）；**五類禁用的 repository 根本不注入快取服務**——靠人記半年後一定會破。
  ✅ **已於 S0-7d（2026-09-21）落地**：`apps/api/Caching/RedisQueryCache.cs`＋`IQueryCache.cs`
  （四維度 key：`v{ver}:{club}:{locale}:{entity}:{qualifier}`；`InvalidateAsync(entity, club)` 遞增版本號，
  供日後後台寫入層呼叫；TTL 預設 300 秒，可用 `QUERY_CACHE_TTL_SECONDS` 覆寫），見
  [`apps/api/README.md`](../apps/api/README.md)「快取接縫」。✅ **S0-7d 續作（同日，使用者拍板）
  已擴大到全部五個 `Features/*` repository**（`Clubs`／`Players`／`Staff`／`Articles`／`Matches`）——
  「不得修改 repository」的本意是「接縫換 no-op↔Redis 不需要動它們」，不是「一行都不能碰」，
  加一個 `IQueryCache` 建構子參數、對外契約不變，是接縫本來就預期的用法。⚠️ **`articles`／
  `article-detail` 有一個必須記住的語意後果**：排程發布「時間到」沒有寫入事件可觸發失效，
  排程發布的實際生效時間完全依賴 TTL，最多延後一個 TTL 週期（預設 300 秒）——不是 bug，
  日後做後台排程發布功能時要嘛接受這個延遲、要嘛在該功能裡主動呼叫 `InvalidateAsync`。
- 🔵 **技術選型已定案**（2026-09-18）：Nuxt 4 SSR ＋ .NET／EF Core＋Dapper ＋ **Azure SQL** ＋ Azure Blob ＋ Redis，
  跑在**單一 Azure VM（Japan East／東京，2026-09-20 由 West US 2 改定）** 的 Docker 上，Cloudflare 在前。**規劃書仍不記技術選型**，結果只在 [`17-deployment.md`](17-deployment.md)。
  ⚠️ 隨之而來的三條硬限制：`uniqueidentifier` 主鍵須**非叢集**（UUIDv7 在 SQL Server 無效）、
  `CalendarEvent` **不能用 indexed view**（禁 UNION）、Azure SQL **不支援跨庫查詢**（這反而讓慈善的邊界變硬）。
  ⚠️ **本機開發把兩個庫放在同一個 SQL Server instance 裡**（2026-09-21 起併入宿主機上**既有、非本專案 compose 管理的 `sqlserver` 容器**——本機不再另開容器，見 `docker-compose.dev.yml`／`deploy/README.md`），**跨庫 JOIN 在本機跑得動、正式一定爆**。但重點不是相容性是**法遵**——慈善庫的獨立是刻意的法人邊界，⛔ **任何一句 SQL 只能碰一個庫**，要合併走應用層各自查再組合（`deploy/README.md`）。
  🔴 **這個陷阱現在多一層**：那個既有 instance 裡除了 TCRFC 的開發庫，還裝著**使用者另一個專案的約 25 個資料庫**——本機測試時打錯資料庫名稱，理論上真的連得到別的專案的資料，不只是「TCRFC 自己的庫互相混淆」而已。防呆是**資料庫名稱寫死白名單**（`deploy/local-ddl.sh` 認 `tcrfc_club`／`tcrfc_charity`，`db/seed/apply-seed.sh`／`setup-club-db.sh` 只認 `tcrfc_club`，`apply-charity-seed.sh` 只認 `tcrfc_charity`；2026-09-30 起本機只剩這兩個庫，見上方 `S0-13` 一條），且**該既有容器沒有掛 volume**——重建它會讓 TCRFC 這幾個庫連同別的專案的資料庫一起消失，細節見 `deploy/README.md`。
- **五種「商業對象」不要搞混**：`Partner`（B2B Logo 牆）／`Sponsor`（贊助商）／`PartnerStore`（主站 8.4 特約店家，會員折扣，**無金流無分潤**）／`DonationStore`（慈善站掃碼引流，**有金流有分潤**）／`Advertiser`（**App 廣告主，計曝光**）。
  同一家實體公司可能同時是數種，**各建一筆、不共用紀錄**。唯一例外是 `Advertiser.sponsor_id` 可關聯回 `Sponsor`，那是關聯不是合併。**贊助商 Logo 牆不計曝光、不進廣告報表。**
  **`Club`（俱樂部型別，v3.0 已移入主站）不是第六種**——它是內容主體不是商業對象，不計曝光、無金流、無分潤。**兩隊的贊助商與夥伴須分區呈現不得混列**（合約是各自簽的）；同一家公司同時是兩隊的夥伴時，**比照上述原則各建一筆**。
  **別再把 `PartnerStore` 跟站內商店的 `Product` 搞混**：前者是會員到店出示卡片的折扣店家（**無金流**），後者是本站自己賣的商品（**有金流**）。
- **行動 App（v3.0，雙隊共同平台＋雙會籍）**：**內容主體是台中磐石 × 台中藍鯨兩隊，收款與法律主體只有俱樂部**——這兩件事必須分開理解。
  **v3.0 推翻 v2.0 的「一張卡通兩隊」**：會籍改為**一人每俱樂部一份**（`Membership`），**每份會籍一張卡**。`Member` 仍不加 `club_id`（理由換了：Email 是登入鍵、LINE 綁定 1:1、當事人是人不是會籍）。**「一張卡一組 token」與「驗證頁不得加適用球隊欄位」兩條一字未改。**
  **收款仍只有俱樂部**：藍鯨會籍採**代收代付**，單一 LINE Pay 商店號（**不得與協會或藍鯨共用**）、單一發票抬頭。**系統不做兩隊分潤**（屬線下合約）。
  對外名稱中性「台中足球 Taichung Football」、雙標誌並列，但**開發者帳號、憑證、收款帳戶一律掛俱樂部**；深連結 scheme **維持 `tcrfc://`**。
  `Club`（俱樂部）與 `Competition`（賽事系列）**已於 v3.0 移至主站規劃書 §5**；後台 `M6` **已移至主站 `J4`**，`M` 模組回到 M1–M5。
  ✅ **v2.0 的兩項阻塞已解除**：官網同步（客戶決定建藍鯨官網，即解法 A）、主站 `J` 資料範圍權限（主站 v3.0 已補）。
  ⚠️ **但新增三項阻塞**：①兩法人間的個資委託處理約定 ②代收代付的會計認定 ③**藍鯨網域的持有與 DNS 控制權**（Universal Link 只能綁自己持有的網域）。
  **藍鯨的品牌資產與名單在磐石的營運團隊手上**（藍鯨由磐石在 running），是**盤點問題不是外部依賴**。**但品質規則不放寬**：標誌必須是**向量主檔**，**不得自行造標、不得從網頁截圖描摹、不得把點陣圖放大充當向量**。未到位前對應區塊不顯示，**不得放假圖或空 Logo 格**。
  ✅ **2026-09-14 已自既有官網取得隊徽的點陣主檔**（下載原圖，3299×3243 去背 PNG＠300dpi，存於 [`brand/blue-whale/`](../brand/blue-whale/README.md)）——**下載原圖不違反上面三條**（不是截圖、不是描摹、不是放大）。可用於文件、網頁與 App 圖示；**大尺寸印刷、單色版、反白版仍須向量原始檔**。
  **新增第十種角色「合作球隊管理」**。⚠️ **v3.0 改寫其會員權限**：由「絕不接觸 K 模組」改為「**可存取自家 `club_id` 的會籍與訂單，但 `Member` 主檔欄位一律遮罩，對方俱樂部的任何會籍列絕對不可見**」——藍鯨有自己的會員後，原規則會讓系統無法使用。**無推播權、無廣告權、無 `J` 系統管理權。**
  **第一階段這個角色可以先不指派使用者**（藍鯨由磐石的團隊在維護，用自己既有的帳號）。**但資料範圍權限仍要做**——`club_id` 已鋪到 50 張表，是系統地基，補在後面等於重寫查詢層；**角色定義先建好、先不指派即可，不擋上線**。
  **不接第三方廣告聯播網、不用廣告識別碼、不做行為定向、不做開屏廣告**；`AdEvent` 原始事件只留 90 天，之後只留日聚合。
  **會員卡只在 App 內出示**（離線可用、標示最後同步時間、**v3.0 起卡面帶該俱樂部標誌，中性雙標誌單卡已作廢**）。**App 抽獎只顯示個人資格布林**（v3.0 起**逐俱樂部顯示**），不顯示序號、不做查詢與名單頁、**不得讀取 `DrawRoster`**；**推播不得用於個別中獎通知**（後台 M3 須系統層阻擋）。
  `PartnerStore` 與 `Venue` 已加座標欄位但**資料要人工標**。後台模組 `M` 與會員卡驗證頁 `/m/<token>` 無關。

- 🔴 **前台改 Nuxt 後必須與現有 mockup 一模一樣**（2026-09-20 客戶指示）。**這是 S0-9 的驗收條件，不是努力目標。**
  **基準線是 [`site/src`](../site/src)（131 檔，納管）＋ [`site/build.mjs`](../site/build.mjs)**；`site/dist` 只是產物（未納管，`node site/build.mjs` 可重產）。
  ⛔ **[`site/src/assets/css/tcrfc.css`](../site/src/assets/css/tcrfc.css)（69KB）整份原封搬過去**——不拆檔、不轉 Tailwind、不改 scoped、不重新命名 class。它是視覺的唯一真實來源，**改 CSS ＝ 改視覺 ＝ 違反本條**。
  ⛔ **body 的 DOM 結構、class 名稱、元素順序與文字內容一律不動。** 不得「順手」優化語意標籤、調整巢狀或重排區塊——**遷移不是重構的時機**。
  ⚠️ **「一模一樣」指視覺與行為，不是 HTML 原始碼逐字元相同**（2026-09-20 切片實測，後者技術上做不到）。
  **驗收要能被腳本驗證**，不是用眼睛看：Nuxt SSR 輸出與 `site/dist` 對應頁**正規化後逐頁 diff**，`<main>` 內容零差異。
  **必然差異是封閉的六類**（2026-09-21 由四類擴充為六類，客戶已拍板接受第五、六類），逐條有理由且都已查證不影響視覺：
  `{{ROOT}}` 相對路徑 → Nuxt 絕對路徑；`<div id="__nuxt">` 包裹
  （`tcrfc.css` 的 `body{}` 只有 margin／font／overflow，無 `body >` 子選擇器，多一層不改版面）；Nuxt 注入的 `modulepreload`／importmap；
  **頁面 `<style>` 由 body 移到 SFC 頂層**（`<style>` 本身不渲染，內容一字不改）；
  **Vue 對靜態 `style="..."` 屬性一律補結尾分號**（`color:#fff` → `color:#fff;`，已查證 mockup 有 **102 個** style 屬性沒有結尾分號，Vue SSR 的 `stringifyStyle` 一律正規化；CSS 結尾分號本來就可有可無，語意不變）；
  **`<select v-model>`／`<input v-model>` 的 SSR 顯式印出 `selected=""`／`value=""`**，mockup 未繫結的原生元素沒有這兩個屬性，瀏覽器預設行為等價（首個 option 本來就會被選取、空字串本來就是 input 預設值）
  ⚠️ **第六類要寫明範圍限制**：只有「值為空」或「指向首個 option」這種情形算必然差異，**其他任何 `selected`／`value` 差異一律是真差異**。
  🔴 **這六類已寫進 [`site/tools/compare-dom.mjs`](../site/tools/compare-dom.mjs) 的正規化規則，工具會自動排除**——**因此工具報出來的任何差異都是真差異，一律不得人工放行**。
  🔵 **驗收線從 2026-09-23 起不再是單純的「80/80 零差異」**：同一支工具另有一份**退役頁面清單**（`RETIRED_ROUTES`，位置在 `compare-dom.mjs` 檔頭「退役頁面清單」一節），
  管的是**「這一整頁還能不能拿 `site/dist` 當基準」**——與上面六類是**兩套不同機制，互不借用理由**：
  六類是「兩邊其實等價、只是序列化方式不同」，退役是「基準本身已經不代表正確答案」（例如 mockup 的新聞卡全指向單一佔位頁，Nuxt 改成逐篇 slug 才是對的）。
  ⛔ **退役不等於少驗**：每一筆**必須同時寫明 `why` 與 `covered_by`（改由哪個檢查接手）**，缺 `covered_by` 工具會在做任何比對之前直接 `exit 1`；
  報告把通過／失敗／退役三個數字分開印，退役不為 0 時結尾一定明講，**無法偽裝成全綠**。同樣**不提供任何 CLI 旁路**，清單只能改程式碼。
  ⛔ **不得連坐退役**：只有「同一頁裡每一筆差異追根究底都是同一個已查證的基準落差」才進清單，**任何一筆成因不同的差異都不准搭便車**。
  ⚠️ **反方向一樣會出事**（2026-09-23 實際發生）：`/zh/news/` 曾因鄰近頁面有別的問題而被**錯誤地留在紅燈裡**，逐筆核對後才確認它 20 筆差異零例外、符合退役條件。
  **兩個方向都只能靠逐筆核對，不能靠印象分組。**
  ✅ **比對範圍已於 2026-09-23（`S0-9m`）擴大到整個 `<body>`，與本條對齊**——涵蓋 skip-link、utility-bar、
  頁首、行動選單、行動 CTA 列、`<main>`、頁尾。擴大當下抓到兩個既有真 bug（頁尾「加入足球學院」、`<body class>` 全站沒輸出）。
  已用突變測試證明關卡真的變寬（四個區塊各改一字，全部被抓到，不是「數字剛好沒變」）。
  🔴 **但這道關卡有一個它天生碰不到的區域，不要對它有超過事實的信心**：它比對的是
  **「Nuxt 輸出 vs 磐石的靜態 mockup」**，不是「兩家俱樂部之間該不該一致」。
  **藍鯨站沒有 mockup，所以任何「藍鯨頁面上印著磐石專屬文案」的問題，比對範圍再怎麼擴大都抓不到**
  （2026-09-23 獨立審查當場就找到一個活的：`SiteHeader.vue` 的學院 mega 選單硬寫「學院」，
  藍鯨站實測照樣印出來，見 `S0-9n`）。**這一類只能靠 `docs/13` §6 紀律 11 的 `club-copy.ts` 機制與人工把關。**
  ⛔ **不得為了讓比對變綠而放寬正規化**——那會讓這道關卡變成擺設。**發現無法歸類的差異要停下來問，不得自行放行。**
  這條在 2026-09-21 已經破過一次（[`18`](18-work-errors.md) `E-21`）：兩個 agent 各自判定第五、六類「無害」就自行放行，事後才由使用者拍板追認——**判斷結果雖然正確，但流程錯了**，判斷本身應該交給使用者不是自己做。
  ⛔ **`<style>`／`<script>` 不得留在 `<template>` 裡，移上去的 `<style>` 不得加 `scoped`**（紀律 9、10，[`13`](13-blue-whale-site.md) §6）——
  **build 全綠但執行期壞掉**，CSS 那條還是無聲失效。
  🔵 **`<head>` 不受本條約束**——mockup 是視覺骨架不是 SEO 實作（全站 `noindex`，`shell.html` **從未輸出過 canonical**，
  `build.mjs` 算了 `CANONICAL` 變數卻沒人用）。**Nuxt 版必須補上 canonical、Schema、`hreflang`**（`GEO-08`，規劃書 §7），
  **這是照規格補做，不是違反「一模一樣」**。

- 🔴 **主站與藍鯨共用一個 Nuxt 映像檔**（2026-09-20 定案，六條紀律見 [`13-blue-whale-site.md`](13-blue-whale-site.md) §6）。兩條最容易破的：
  ⛔ **色彩永遠只能是 CSS custom properties**——不得引入 Tailwind JIT class 或任何把顏色編譯成字面值的工具，否則 runtime 換色直接失效。
  ⛔ **單元開關（藍鯨不設 06／11）只能有一個真實來源**：`isUnitEnabledForClub()`，route middleware、選單、sitemap、`llms.txt`／`robots.txt` 四處都呼叫它。
  ⚠️ **單一映像檔放大而不是縮小「忘了加 club 判斷」的風險**——兩站永遠部署同一份程式碼，漏判斷就是**兩站同時見紅**。
  ⛔ **`site.url` 不得寫進 `nuxt.config.ts`，`NUXT_PUBLIC_SITE_URL` 不得在 `docker build` 階段帶**——後者會被烤進 `.output` 當**預設回退值**，某個容器忘記帶環境變數時會**悄悄**用到錯的網域，**不是直接壞掉是靜默錯誤**。只在 `docker run` 時給。

- 🔵 **HEIC 由前端瀏覽器轉 JPEG 再送**（2026-09-20 定案，`docs/17` §6）。ImageSharp 不解 HEIC，而伺服器端加 HEIF 解碼會牽進 **HEVC 專利授權**。
  ⛔ **但伺服器端仍要擋**：收到解不開的檔一律回絕，**不得假設前端一定轉過**——前端可能失敗、可能被繞過。

- 🔴 **全專案只有兩套環境：本機開發與正式 VM。沒有 staging，也不准長出第三套**
  （2026-09-21 定案，見 [`17-deployment.md`](17-deployment.md) §10、[`20-cicd.md`](20-cicd.md) §1、[`18`](18-work-errors.md) `E-13`）。
  **本機開發**＝`docker-compose.yml` ＋ `docker-compose.dev.yml`（假資料庫、明文 HTTP）；
  **正式 VM**＝`docker-compose.yml` 單獨跑。CI 的整合測試是**用完即丟的一次性堆疊**，不是環境。
  ⛔ **「正式網址到位前」是同一套正式環境的一個階段，不是環境**——同一台 VM、同一批容器、
  同一個資料庫、**同一份 compose 檔、同一道指令**，差異全部在 `.env` 的值
  （`SITE_ENV=prelaunch`、`CADDYFILE=./deploy/Caddyfile.prelaunch`、六個暫用網域）。
  ⛔ **不得新增 `docker-compose.<環境名>.yml` 之類用檔名區分階段的檔案**——`docker-compose.staging.yml`
  已因此被刪除一次（`E-13`）。**階段用值切換，不用檔案切換。**
  ⚠️ 例外只有既有的 `docker-compose.dev.yml`，因為它切的是真正的第二套環境。

- 🔴 **網址：上線前到正式期，cookie 絕對不得設 `Domain` 屬性**（2026-09-20 定案，見 [`17-deployment.md`](17-deployment.md) §10.5）。
  暫用網址（`stg.tcrfc.tw` 等）與未來的正式網址同屬 `tcrfc.tw`，**cookie 若設 `Domain=.tcrfc.tw`（前面帶點）會被瀏覽器送到所有子網域**，
  暫用網址的登入 session 會被自動帶到正式站的後台（反之亦然）。⛔ **不得設定 `Set-Cookie` 的 `Domain` 屬性**（省略即 host-only，最安全的預設）；
  **後台一律採 `__Host-` 前綴**（瀏覽器層級強制不得有 `Domain` 屬性，設錯直接整顆 cookie 失敗，不會悄悄放寬）。
  `admin-stg.tcrfc.tw` 與 `admin.tcrfc.tw` 會有一段共存期（後台不受主站 Wix 切換時程限制，見 `17` §10.7），**這條在共存期特別危險**。
  ⚠️ **上線前與正式期的 JWT 簽章金鑰建議用不同值**——即使 cookie 作用域設定不慎放寬，簽章金鑰不同仍能擋一次；但**不能假設這條永遠成立**，圖方便共用同一把 key 會讓這層防禦一起失效。
- 🔴 **三類網址一旦「點火」就不可逆，先確認正式網址才能做**（2026-09-20，見 [`17-deployment.md`](17-deployment.md) §10.3）：
  **①已上架 App 的 Universal Link**（送審前主站與藍鯨都必須是最終網域，事後換網域要出新版本送審，且舊版使用者深連結會失效一段時間）、
  **②已印製的慈善 QR Code**（印出即物理不可修改，換網域＝全數作廢重印）、
  **③已發出（系統信寄出或實體印出）的會員卡 `/m/<token>` 連結**（同理，且長期轉址對驗證性質的連結是額外的安全風險）。
  **其餘一切**（前台內容、後台開發、API 開發、上線前的訪客互動）**都可以先用暫用網址做**，凡準備做上面三件事之一，先確認網址已是最終版本。
  ⚠️ **暫用網址（`stg.tcrfc.tw` 等）不是假資料沙盒**——同一套 Azure SQL／Blob／Redis（只有兩套環境的必然結果），上線前若對外開放互動，那些資料就是未來正式資料，不會在切網域時自動清空（此事尚未定案，見 `17` §10.9）。
  **上線前的站必須真的擋住**（HTTP 標頭 ＋ `robots.txt` 兩層，缺一不可，理由與被索引後的清理成本見 `17` §10.4）——不能只靠 meta `noindex`。**測試站無帳密（2026-10-02 使用者決定拿掉 Basic Auth）：知道網址即可瀏覽，背後是正式資料庫，有人填表即為真實資料**，不要公開張貼測試站網址。
- 🔴 **CI/CD：公開 repo ＋ self-hosted runner 的唯一地基**（見 [`20-cicd.md`](20-cicd.md) §4）：
  **Repo 設定的「Fork pull request workflows → Require approval for all outside collaborators」必須是開的。**
  ⛔ **不要以為「我們的 workflow 沒讓 fork 用 self-hosted」就安全**——fork PR 跑的是**該 fork 版本的 workflow 檔**，
  惡意 fork 可以自己加上 `runs-on: self-hosted`，在正式 VM 上執行任意程式碼。
  GitHub 公開 repo 的預設只擋「首次貢獻者」，**要手動改嚴**。
  ⛔ **`pull_request_target` 一律不得使用**（它會把 workflow 檔本身的權限用在 fork 的程式碼上）。

- 🔵 **行動 App 的執行層決定**（2026-09-20，見 [`19-app-tech-stack.md`](19-app-tech-stack.md)）。**規劃書 §1.3 明文排除技術選型**，以下是執行層決定不是規格，但改錯會出事：
  1. **客戶端是原生 Swift ＋ Kotlin**（iOS 15+／Android 10+），**不是 React Native、不是 Flutter、不是 WebView 外殼**。唯一來源是 `docs/19`，規劃書只寫 §1.5 的八項平台能力需求。
  2. ⛔ **廣告曝光判定的行為規格只有一份**（`shared/ad-viewability.md`：200ms 取樣、以單調時鐘累積連續 ≥50% 的時長、≥1000ms 成立）。**兩個平台不得各自解釋，必須跑同一份 fixtures**——判定分歧會直接變成對廣告主的對帳爭議。
  3. ⛔ **更新權杖必須是可由伺服器端撤銷的不透明字串，不得用長效 JWT**（§4.3 硬性要求撤銷，JWT 做不到）。Android 的 **`androidx.security:security-crypto` 已棄用，不得採用**，改 Google Tink ＋ Keystore，且**必須排除自動備份**（否則備份會把權杖帶到新裝置）。
  4. ⛔ **推播分眾不得用 FCM topic**——「會籍層級」是伺服器端資料，放進 topic 等於把付費狀態送進 Google 的索引；topic 也拿不到 per-device 結果，§6.6 的數字算不出來。分眾一律在 .NET 端解析成裝置權杖清單直送 APNs／FCM。
  5. ⛔ **App 端不得做執行期 geocoding**（不得用 `CLGeocoder`／`Geocoder`）；**定位座標只在記憶體，不上傳、不寫本機資料庫、不進日誌與崩潰回報**。距離在裝置端算。
  6. ⛔ **會籍有效性不得以本機快取宣告**——這是上面「五類不得讀快取」在 App 端的落點。卡面把狀態與**伺服器回傳的最後同步時間**並陳，天數比較同時參照單調時鐘（**改裝置時鐘不得消掉 7 天提醒**）。**不得因逾期未同步就停用卡片或隱藏 QR**，§3.6 只要求顯示提醒。
  7. ⛔ **設定、最低支援版本與維護模式必須有不經 API 的第二來源**（Cloudflare 上的靜態 JSON）。單一 VM、單一 API 行程是單點故障，**API 掛掉時用來宣告「維護中」的端點也掛了**；強制更新畫面的版面與雙語文案**必須打包進 App**。⛔ **任何情況都不得白畫面。**
  8. ⛔ **`payment_mode` 只能從 `inapp` 降級回 `external`，不得反向**。以 `external` 送審、通過後再遠端開啟未經審查的 `inapp` 是違規，會下架。§5.5「不得寫死成只支援其中一種」的目的是**審查打回時能退回可用狀態**，不是為了繞過審查。

- **資料庫綱要的執行層決定**（見 [`docs/12-database-schema.md`](12-database-schema.md)）：
  - ✅ **`docs/12` 的 v3.0 同步已於 2026-09-20 完成**（§4 總覽、**15 張 ERD**、§6 明細、§11 唯一鍵與索引、§14 檢核表），**可以轉 DDL**。
    `club_id`：**50 張必填、9 張可為空（＝兩隊共同）、43 張不加**，與主站 §5.4 逐名一致。
  - **不建任何日誌表**（`AuditLog`／`LoginLog`／`ExportLog`／`OperationLog`）——與規劃書「操作稽核記錄保存 ≥ 12 個月」及「匯出須寫稽核」**衝突**，落差列於 `docs/12` §13.1。
    **B1（2026-09-30）暫行做法**：會員名單／續會名單／球衣出貨清單／試訓名單的匯出、解除個資遮罩、合併帳號、調整會籍等敏感操作，寫**應用程式結構化日誌**（`SensitiveActionLogger`：帳號、俱樂部、對象、筆數、用途，**不含個資**），匯出一律必填「用途」。這不是稽核表；客戶若要求可查詢的稽核紀錄要另行裁決。
    **會員個資（K1／K3）**：名單一律遮罩，完整值只有「會員詳情 `reveal=true`」與有 `member.pii.reveal` 的角色看得到；沒有解除權限者的搜尋只比對會員編號；LINE 識別碼與 QR 憑證字串**永不出現在任何後台回應**；受限帳號只看得到自己授權俱樂部的會籍列，直接打對方的 id 是 404。
    但 **`EmailLog`、`InventoryMovement`、`PageVersion`、`FaqSearchMiss` 不是日誌是功能單元**，一律保留。
    **D 批（2026-09-30）沿用同一條**：J3 的「操作稽核記錄」「登入紀錄」**仍不建表**（客戶指示與 2026-09-23 使用者裁決仍有效，`docs/18` `E-44` 升級段；`SchemaInvariantsTests` 守著）。
    `system.audit.view` 只提供「帳號目前狀態＋登入異常提醒」的唯讀概況（`GET /api/v1/admin/security/overview`，`auditTrailAvailable` 恆為 `false`）；
    **匯出電子報／廣告成效、App 憑證輪替、推播核可、更新門檻與維護模式、裝置完整值檢視**同樣寫 `SensitiveActionLogger`。
    **`ad_events`（原始事件，90 天）與 `push_message_stats`（彙總數字）不是日誌表**：不存 `member_id`、完整 IP、定位座標、廣告識別碼，也不逐裝置記錄推播投遞或開啟。
  - **（D 批）App 與廣告的不變量**：① **推播不得成為繞過「中獎只以最新消息公布」的後門**——`PushContentGuard` 在建立／送審／核可／試送都擋「中獎」字樣與帶抽獎標籤文章的深連結，自動推播（新聞發布）必須先問 `IsMemberDrawArticleAsync`；
    ② **推播核可是雙人覆核**（核可者≠建立者，且需 `sysadmin_only` 的 `app.push.approve`）並要回報預估人數作二次確認；
    ③ **最低支援版本會強制舊版更新**：設定必須 `confirmForceUpdate`、只有已上架版本能設為門檻；**`payment_mode` 只能降級不得反向開成 `inapp`**；
    ④ **兒童向畫面（S15／S16／S17）與慈善相關不設廣告版位**；**素材未通過審核的檔期不得進入投放中**；素材被改回到待審；
    ⑤ **推播權杖加密儲存、後台清單遮罩、完整值需 `app.device.reveal`**；⑥ **`app_credentials` 只存列管資訊，絕不存金鑰本身**；
    ⑦ **JSON 時間戳一律 UTC 帶 `Z`**（`Common/UtcDateTimeJsonConverter`，輸入無時區記號視為 UTC）；⑧ **EF `Where` lambda 內不得呼叫會丟驗證例外的函式**（`docs/18` `E-92`）。
  - **後台帳號用 `username` 登入不用 Email**：`AdminUser.username` UNIQUE，`email` 只作通知、不唯一、不作登入鍵。
    **`username` 是一般字串**（2026-10-03 使用者裁決）：去前後空白後非空、≤64、不含空白字元，中文等 Unicode 皆可，不設最短長度；
    **密碼至少 6 字元**（2026-10-08 使用者裁決由 9 改 6；更早為 10）、不得等於帳號。建立與登入兩端都先 Trim。執行層決定、規劃書未寫死；常數在 `AdminAuthService.MinPasswordLength`，
    兩個後台前端與 `deploy/prod-db-init.sh`（`MIN_PASSWORD_LENGTH`）要同步。
    種子超管 `sa@system.local`／`Admin@123`（雜湊儲存、不強制首次更換）——**它長得像 Email 但存在 `username` 欄**。
    **前台 `Member` 維持 Email ＋ LINE 登入不變，兩套帳號完全獨立。**
  - **雙語採 `<entity>_i18n` 側表**，不是並排 `zh_*`／`en_*` 欄位——為兌現「加第三語系不改程式」。快照表、後台角色表、UI 字串不走側表。
  - **金額一律 `int` 存「元」**，只有百分比用 `decimal(5,2)`。台幣無角分且慈善分潤明訂無條件捨去至整數元。
  - **（v3.0）`club_id` 不是每張表都加**：判定準則見主站規劃書 **§5.4**——後台有獨立清單／前台有獨立路由／承載個資或金流三選一；**能經父表推導的一律不加**。**加了就要同時決定唯一鍵、後台預設過濾、前台路由三件事。**
  - **（v3.0）共同內容（`club_id` 為空）對受範圍限制的帳號一律唯讀**，只有超管能建立與修改。否則「查得到共同內容」與「不能改到別人的內容」無法同時成立。
    ⚠️ **現況（2026-09-24，`S1-5`／`S1-7` 已核實）：目前完全沒有任何後台端點能建立或修改共同列**——
    `articles`（`AdminArticlesRepository.LoadTrackedForWriteAsync`）與 `staff`
    （`AdminStaffRepository.UpdateAsync`）這兩張已接真實授權的可為空表，寫入路徑一律走
    `/api/v1/admin/{club}/...` 俱樂部範圍端點：建立永遠把 `club_id` 填成路由當下的俱樂部（不接受
    建立共同資料），命中既有 `club_id IS NULL` 的列一律 403，**沒有超管特例**——超管一樣得挑一個
    `{club}` 才能打這組端點，而共同列的 `club_id` 是 NULL，邏輯上不屬於任何單一 `{club}`。
    上面這條規則講的「只有超管能建立與修改」目前**只是意圖，還沒有對應的實作路徑**（可能要等
    J4 或一個不掛 `{club}` 路由段的全域端點）。**其餘 7 張可為空表（`Faq`／`Charity`／
    `CharityProgram`／`ImpactRecord`／`ImpactMetric`／`PressResource`／`PartnerStore`）開發後台
    寫入時，先決定「要不要現在就補超管編輯共同內容的路徑」，不要預設沿用 `articles`／`staff`
    這個「共同內容目前無法編輯」的暫定狀態就是最終答案。**
  - 🔴 **（S0-7g，2026-09-24）「排程發布」需要 `published_at` 欄位，`scheduled` 這個狀態值本身不等於「有排程機制」**：`db/club-schema.sql` 有 9 張表帶 `CHECK (status IN ('draft','published','scheduled'))`，但只有 `pages`／`articles` 真的有 `published_at` 欄位；`press_resources`／`faqs`／`competitions`／`sponsor_packages`／`collections`／`products`／`charity_programs` **CHECK 約束允許寫入 `'scheduled'`，資料庫裡卻沒有任何欄位記錄「排定何時發布」**——這 7 張表在後台寫入層開發出來之前，看起來像「支援排程」，實際上不可能真的排程。**開發這 7 張表的後台寫入模組前，先確認是否要補 `published_at`，要補就先走 `docs/12` 同步鏈再走 migration，不要假設欄位已經存在。** ✅ **已裁決（2026-09-24，依規劃書）**：規劃書只在 `B1` 頁面（第 1014 行）與 `B2` 新聞（第 1019 行）給了排程發布，**其餘 7 張表不補 `published_at`**。那幾個模組的後台**不得提供「排程」選項**；`scheduled` 出現在 CHECK 裡，是因為共用同一組狀態詞彙，不代表那些型別有排程功能。
  - 🔴 **（S0-7g；2026-09-24 由 S1-4 擴充）「時間到了」不是寫入事件，需要主動的 hosted service 才會真的轉狀態**——`status='scheduled'` 不會因為 `published_at` 過期而自動變成 `'published'`，公開讀取 API 的 `WHERE status = 'published'` 是字面比對。`Features/News/ScheduledPublishRunner.cs` 是目前唯一接上這個機制的地方，掃 `articles`（`PublishDueArticlesAsync`）與 `pages`（`PublishDuePagesAsync`，S1-4 新增）兩張表，`ScheduledPublishBackgroundService` 每輪依序呼叫兩個方法。**任何新的內容型別要支援「排程發布」，除了要有 `published_at` 欄位，還要把它加進這個 runner（或比照新開一個），否則後台可以把狀態設成 `scheduled`，但公開站永遠不會自動顯示。**
  - 🔴 **判斷「已到發布時間」的兩邊必須來自同一個時鐘（2026-09-24，S1-4 續作，`PagesPublicEndpointTests` 間歇性失敗排查）**：公開讀取一律用 `published_at <= SYSUTCDATETIME()`（資料庫自己的時鐘）。寫入端若把 `published_at` 設成應用程式行程的 `DateTime.UtcNow`（另一台機器的時鐘——本機環境是 API 行程所在主機 vs. `sqlserver` 容器），兩個時鐘只要有任何飄移，剛發布的內容就可能被判定為「還沒到」而暫時查不到，且**發布本身完全成功、不會有任何錯誤或例外**——這是本次實測連跑 15 次失敗 2 次才抓到的間歇性 bug，不是並行權杖精度問題。`AdminArticlesRepository.PublishAsync`／`AdminPagesRepository.PublishAsync`（立即發布，`published_at` 設為「現在」）已改用 `Common/DatabaseClock.GetUtcNowAsync`（跑一次 `SELECT SYSUTCDATETIME()`）取代 `DateTime.UtcNow`。**日後任何寫入路徑會把某個欄位設成「現在」、而這個欄位之後會被拿去跟 `SYSUTCDATETIME()`／`GETUTCDATE()` 比較「是否已經到了」，一律要用 `DatabaseClock`，不要用 `DateTime.UtcNow`**——`ScheduleAsync`（`published_at` 是呼叫端指定的未來時間，不是「現在」）與 `ScheduledPublishRunner`（整句判斷都在同一個 SQL 陳述式內求值）不受影響，理由見 `Common/DatabaseClock.cs` 檔頭。

- 🔴 **（S1-3 續作，2026-09-24）`Permission.sysadmin_only` 是應用層強制，資料庫沒有任何約束擋著**：
  `role_permissions` 沒有 CHECK 或觸發器阻止把一個 `sysadmin_only=1` 的權限碼指派給任何角色——
  真正擋下的是 `Security/PermissionChecker.cs`（`HasPermissionAsync` 在非超管路徑額外比對
  `!p.SysadminOnly`）與 J2「角色權限指派」端點（`Features/AdminRoles/AdminRolesRepository.cs`
  的 `ReplacePermissionsAsync` 拒絕整批寫入含 `sysadmin_only` 權限碼的請求）。**兩道防線都在
  C#，不在 DDL**——日後如果有人繞過這兩個地方直接寫 SQL 進 `role_permissions`（例如批次匯入
  腳本），`sysadmin_only` 這條規則不會自動生效，要嘛也走這兩個檢查、要嘛在資料庫加約束。
  J1（帳號管理）／J2（角色與權限）／J4（`Club` 主檔與 `admin_user_clubs` 授權）的全域端點
  （不含 `{club}` 路由段）改用新的 `Security/IAdminSystemAuthorizer`（不是既有的
  `IAdminClubAuthorizer`）；`Competition` 型別的維護端點雖然歸在「J4 這件工作」底下，
  但因為 `competitions.club_id` 必填，實際權限碼歸在 `module_code=C`（`team.competition.*`），
  執行層判斷見 `apps/api/README.md`。

- 🔴 **（S1-8，2026-09-24）`role_permissions.scope_type` 的列級授權強制，`Security/TeamRowScope.cs`／
  `AdminTeamRowScopeResolver.cs`**：`scope_type` 這個欄位從 `S1-3` 就種在資料庫，但直到 `S1-8`
  才第一次有程式碼真的讀它做過濾——**日後看到某個角色的某個權限碼掛著 `academy_only`／`own_teams`
  之類的值，不代表它真的有效果，要去確認呼叫端有沒有經過 `IAdminTeamRowScopeResolver`**。
  目前只有 C1（`teams`）／C2（`players`）／C3（`staff` 的 `staff_teams`）／C4（`matches` 的
  `match_teams`）四個模組的**寫入端點**接上了這個機制；**列表／檢視端點沒有套用**（例如
  `academy_program` 角色目前看得到整個俱樂部的球隊清單，不會被過濾成只剩學院梯隊）——這是刻意
  縮小的範圍（任務指示只要求「寫入端點」），不是遺漏，但表示**列表畫面上看得到的資料不等於
  寫得進去的資料**，不要以為「畫面上濾掉了」。`standings`（積分榜）**完全不套用**，因為這張表
  沒有 `team_id` 欄位，見 `apps/api/README.md` S1-8「為什麼積分榜不套列級授權」。
  ✅ **（S1-8 續作，2026-09-24）新增 `GET /api/v1/admin/{club}/teams/writable?
  module=team|player|staff|match`（`Features/AdminTeams/AdminTeamsEndpoints.cs`），
  提供「已收斂成呼叫端真的能寫」的球隊清單**——但這是**額外新增的一支端點**，不是把過濾邏輯
  補進既有的 `GET /teams` 等列表端點；**既有列表／檢視端點本身仍然沒有套用列級授權**，上面
  那句「不要以為畫面上濾掉了」對既有端點依然成立，只是現在多了一條「真的濾過」的路徑可以選用，
  見 `apps/api/README.md` 該輪的完整契約說明。
- 🔴 **（S1-8）`role_permissions.scope_type = 'own_clubs'` 在列級授權裡視同 `'all'`（不限）**：
  docs/12b-database-tables.md §7.1 講「`scope_type` 加值 `own_clubs`」，但 §7.4 那張「`scope_type`
  是矩陣裡不是布林的格子」對照表只列了 `own_teams`／`academy_only`／`masked`／`translate_only`
  四個，**兩段自相矛盾**（`own_clubs` 沒被正式收進值域清單）。既有種子資料
  （`db/seed/generate-club-seed-sql.py`）對 `partner_club_manager` 的全部指派都用
  `"own_clubs"`——`AdminTeamRowScopeResolver` 若把它當成未知值處理（fail-closed），會讓合作球隊
  管理角色完全無法操作任何球隊／球員／教練／賽事資料，且**這個 bug 在 `scope_type` 真的被讀取
  之前完全不會顯現**。**已在程式碼修正**：`own_clubs` 標記的是「club 層級」的範圍（已經由
  `IAdminClubAuthorizer` 的 `AdminUserClub` 檢查在更上一層擋住），不代表「同一個俱樂部內部」要
  對球隊再窄化一次，故視同不限。**docs/12b §7.4 尚未同步補上這個值**，是已知的文件缺口，
  不是程式碼缺口。
- 🔴 **（S1-11，2026-09-25）`own_teams` 盤點結果：L 行事曆模組的讀取端點仍然不需要它**——
  S1-8 當時把 `own_teams` 保留給「等 L 模組真的需要時再指派」，S1-11 實作 L1／L2 後盤點：
  行事曆總覽（`calendar.view`）是純讀取，賽事本身早已透過 `GET /api/v1/{club}/schedule`
  對任何人公開，不因為多了「總覽」這個入口變成需要列級限制的資料；L2 自建事件的寫入權限碼
  （`calendar.custom_event.*`）目前拿到的角色（系統管理員／內容編輯／公關媒體／合作球隊管理）
  在種子資料裡也都是 `all`（或效果等同 `all` 的 `own_clubs`），沒有一個角色需要「只能碰特定
  球隊的自建事件」這種列級限制。`own_teams` 依然是**已實作但未被任何內建角色使用**的能力，
  等到 L1「拖曳改期回寫 `Match`」（`S2-6`）真的做出來、且真的有角色需要限縮到特定球隊時再指派，
  不需要為此再改一次程式碼，見 `apps/api/README.md`「S1-11」段。

- **賽事資料全部人工維護**，不串接外部 API，提供 CSV 批次匯入。
- 🏟 **賽事狀態的中文是「延賽」不是「延期」**（主站規劃書 **v3.13，2026-09-23 客戶裁決**，球界慣用語）。
  英文維持 `postponed`（本來就是正確的足球用語，未改動）。**`Match` 補「原定日期」與「原定時間」欄位**
  （`original_match_on`／`original_kickoff`，僅狀態為「延賽」時有值，供賽事卡片與 C4 編輯畫面顯示延賽前的原定時間）——
  沿用 `match_on`／`kickoff` 既有的兩欄配對寫法，**皆可為空、不加 CHECK**（`matches.status` 本身的值域與 CHECK 見下一條與 [`12-database-schema.md`](12-database-schema.md) §12 第 35 點）。
- ✅ **（v3.14，2026-09-24 客戶裁決）`matches.status` 五值行文落差已解決，`db/club-schema.sql` 已補 CHECK**：
  主站規劃書 §3.13／§4.3 C4／§5.1 `Match` 三處已一致為五值（未開始／進行中／已結束／延賽／取消，
  英文 `scheduled`／`live`／`played`／`postponed`／`cancelled`）；`matches.status` 欄位（`nvarchar(16)`）
  補上 `CHECK (status IN ('scheduled','live','played','postponed','cancelled'))`。
  ✅ **（S1-7b，2026-09-24，`backend-engineer`）應用層已跟上**：`Features/AdminMatches/
  AdminMatchesRepository.cs` 的 `AllowedStatuses`／`StatusZhLabels` 已補五值，CSV 匯入、
  中文對照表與 `AdminMatchesEndpoints` 均接受「取消」。「取消」不受 `ValidatePostponedFields`
  的原定時間規則約束（跟延賽不同，不必填也不能填原定日期）。公開端點 `GET /api/v1/{club}/schedule`
  是純值傳遞（不做 enum 對照），前台 `apps/web/app/utils/schedule.ts` 的 `MATCH_STATUS_MAP`
  **本來就已經有 `cancelled`**（S0-9l 遺留的預留鍵，早於本次五值定案），兩處均不需改動即已一致。
- 🔴 **（S1-8）`matches.match_no`（場次編號）同季同聯賽唯一，沒有 DB 唯一索引，只有應用層檢查**
  （`AdminMatchesRepository.EnsureMatchNoUniqueAsync`，範圍是 `(club_id, season_id,
  competition_id, match_no)`）。**任何日後直接寫 SQL 匯入賽事資料的腳本，繞過這個應用層檢查
  就沒有任何東西擋得住重複場次編號**。
- **既有數位資產**：官網 www.tcrfc.tw（待遷移）、IG `@tcr_fc_2024`、FB `TCRFC2024`、YouTube `@TCRFC-2024`、
  台中藍鯨既有官網 [www.tcbw2014.com](https://www.tcbw2014.com/)（Google Sites）——⚠️ **v3.0 起改列為內容遷移來源**，新站由本系統建置（獨立網域、雙語），上線後 301 轉址。
- **成立年份 2024**，2024 全國乙級聯賽冠軍。

- 🔴 **（S1-5，2026-09-24）`value_tag_links`／`article_relations` 這兩張多型關聯表，`entity_type`／
  `target_type` 的字面值是「型別詞彙表單數小寫」，第一個真正接上讀寫邏輯的呼叫端是
  `Features/AdminNews`（`ArticleEntityType = "article"`；`article_relations.target_type` 只收
  `player`／`team`／`match`／`program`／`partner` 五種）——**這是本輪定的慣例，不是規劃書給的字面值**
  （規劃書只講「多型關聯」「核心價值標籤可掛任何內容型別」，沒有給字串長相）。**之後任何模組要讓自己
  的型別可以掛核心價值標籤或被文章關聯，一律沿用同一套命名**（小寫、單數、跟 `docs/12` 型別詞彙表的
  大寫型別名對應，例如 `Program` → `program`），不要各自發明一套大小寫或縮寫規則，否則同一張表裡混著
  兩種大小寫寫法，查詢時忘記轉大小寫就悄悄查不到任何列。
- 🔴 **（S1-5）「多型關聯的跨俱樂部隔離」是應用層強制，資料庫沒有任何約束擋著**——`article_relations`
  是純多型關聯（`target_type`＋`target_id`），型別上不可能對「球員」「球隊」「賽事」「課程」「夥伴」
  五張不同的表同時宣告外鍵，所以**資料庫允許任何 GUID 寫進 `target_id`，包含跨俱樂部或根本不存在的
  id**。真正擋下的是 `AdminArticlesRepository.ValidateRelationsAsync`：寫入前逐筆查對應資料表
  `WHERE id = @TargetId AND club_id = @ArticleClubId`，查不到就整包 400。**日後任何地方要直接寫
  `article_relations`（例如批次匯入腳本、其他模組抄樣板）都要重做這道檢查，不能假設資料庫會擋。**
- 🔴 **（S1-5）標籤／核心價值標籤／關聯三個欄位在後台更新端點是「省略＝維持不變、空陣列＝清空」**，
  跟同一個請求裡雙語內容欄位「省略英文＝清空英文」是**相反的語意**（`UpdateArticleRequest.Tags`／
  `CoreValueTags`／`Relations` 對比 `Content.En`）。原因是雙語內容欄位在既有畫面已經有輸入框、
  省略等於使用者主動清空是合理預期；標籤／核心價值標籤／關聯目前**沒有任何畫面**，若採「省略＝清空」，
  任何只改標題的存檔動作都會把既有標籤／關聯整批清光——**同一支 API、同一種「這個欄位存不存在」的
  判斷方式，不同欄位可能是相反的業務語意，抄樣板時要逐欄位確認，不能整支複製貼上。**
- 🔴 **（S1-5）`articles.status` 沒有「已下架」這個值**（CHECK 約束只有 `draft`／`published`／
  `scheduled`，見既有落差 `apps/api/README.md`「已發現、未動手修改的既有落差」第 1 點）。
  `AdminArticlesRepository.BatchUnpublishAsync`（批次下架）因此**把「下架」實作成轉回 `draft`**——
  這是本輪的判斷，不是規劃書明文，需要業務確認（跟 `S0-7h` 的兩項假設同一種性質）；`published_at`
  不會被清空，這是目前唯一還能分辨「這篇文章曾經發布過」的線索。**日後若要新增真正的「已下架」狀態
  值，是規格變更，先改 `docs/12` 再走同步鏈，不要直接改 CHECK 約束。**
- ✅ **（S1-6 缺口，S1-8 已補）`banners` 的圖片欄位組已補齊**：新增 `media_type`（`image`／`video`，
  規劃書行 1023「圖／影片」一併補上）、`image_width`／`image_height`、`banners_i18n.image_alt`、
  `video_key`（`media_type='video'` 時必填，CHECK 強制）。`db/club-schema.sql`／`docs/12` §4.1／
  `docs/12a` §5.1／`docs/12c` §3.1 已同步。⚠️ **`articles.cover_key`／`teams.hero_key` 等其餘既有
  圖片欄位仍是同樣的缺口**，本輪只處理 `banners`（首頁 Hero 是全站最顯眼的視覺元素，優先度最高），
  未列入本次範圍，日後要補一併走同步鏈。**影片檔本身不經過「上傳即縮圖」流程**（該流程只處理
  圖片），影片的格式、檔案大小上限與是否轉碼規劃書未明訂，見 `docs/12` §12 第 33 點。
  ✅ **（v3.14，2026-09-24，`backend-engineer`）執行層已裁決並落地**：只收 MP4（H.264／AAC）、
  單檔上限 50 MB、**伺服器端不轉碼**（跟圖片刻意不同），必須搭配海報圖（既有 `image_key`），
  以 `ftyp` box 檔頭（magic bytes）驗證容器格式、不只看副檔名。完整理由與邊界見
  [`17-deployment.md`](17-deployment.md) §6「Hero 輪播影片上傳」、`apps/api/Videos/`。
  **影片上傳只驗證容器格式，不解封裝驗證內部視訊／音訊編碼**——這是刻意的驗證邊界，不是遺漏。
- ✅ **（v3.14，2026-09-24 客戶裁決）`banners` 新增 `status`（`draft`／`published`，預設 `draft`）**：
  新增或上傳後為草稿，發布後**依既有 `start_at`／`end_at`（上架期間）自動顯示與下架**。
  🔴 **刻意不用 `scheduled`**——排程語意已經由 `start_at`／`end_at` 承擔，`status` 只分「還沒審過的草稿」
  與「已發布」兩態；也**不比照 `Page`／`Article` 接 `ScheduledPublishRunner`**，因為顯示與下架的時間點
  由公開讀取端查詢時比對 `start_at`／`end_at` 即可判定，不需要一個背景服務去翻轉狀態值本身。
  `db/club-schema.sql`／`docs/12` §4.1／§12 第 35 點／`docs/12a` §5.1（`banner` 實體）已同步；
  `banners_i18n` 不受影響（狀態不是語言相依欄位）。
  ✅ **（S1-7b，2026-09-24，`backend-engineer`）後台寫入端與公開讀取端已實作**：新建立一律
  `draft`，`POST .../banners/{id}/publish`／`.../unpublish` 兩支專用端點切換（沿用既有
  `content.banner.update` 權限碼，未新增權限碼）；公開端點 `GET /api/v1/{club}/banners`
  同時滿足 `status = 'published'`（等於比對，白名單寫法，`Features/Home/HomeRepository.
  ListBannersAsync`）**與** `start_at`／`end_at` 時間窗兩個條件，兩者缺一不可。
- ✅ **（S1-6 缺口，S1-8 已補）`faq_categories` 已加 `is_enabled`（軟停用）**：取代先前「用刪除湊
  停用」的作法——刪除經 `ON DELETE CASCADE` 解除分類關聯且不可逆，題目本身仍在但分類導覽找不到。
  現在可真正停用又重新啟用，題目與既有關聯不受影響。`db/club-schema.sql`／`docs/12` §4.1／
  `docs/12a` §5.1b 已同步。
- ✅ **（S1-6 缺口，S1-8 已判定不需加欄位）`home_sections.featured_banner_id` 只給 `hero` 用是
  正確的最終狀態，不是遺漏**：逐一核對規劃書 B3（行 1023–1024）點名的九個區塊——Hero 已有
  `featured_banner_id`；「最新消息」精選文章已由 `articles.is_featured` 承載（B2，不歸 `home_sections`
  管）；其餘七區塊（核心價值、體系導覽卡、最新賽事、近期賽事、夥伴 Logo 牆、商店入口、底部 CTA）
  依時間或排序自動查詢，或為固定文案，規劃書沒有「指定某一筆」的字面要求。**「精選內容指定」字面
  涵蓋全部九區塊的說法，實際只有 Hero 與最新消息兩者需要，且都已有機制承載**——不加欄位是核對後
  的設計結論，不是待補。**看到「首頁編排可以指定任何區塊的精選內容」仍要先查這欄位存不存在**，
  但不必假設它是遺漏，可能就是不需要。

v2.0 時代「先改 App、官網另議」的刻意落差**已經沒有了**。客戶決定：**藍鯨升級為系統的第二個俱樂部，並建置藍鯨官網**。

| 文件 | 狀態 |
|---|---|
| 主站規劃書 v3.0 | ✅ §3.6【06】已改寫為藍鯨官網入口；多俱樂部架構、`club_id` 維度、`J` 資料範圍權限全部補完 |
| 藍鯨官網規劃書 v1.0 | ✅ 新增（中英雙版） |
| App 規劃書 v3.2 | ✅ 雙會籍；`Club`／`Competition` 移至主站；§16.2 的兩項阻塞解除。**v3.2 更正 §1.1／§3.7 兩處 v3.0 未同步的「一份會籍通兩隊」殘留敘述** |
| `docs/12` 資料庫綱要 | ⚠️ **仍未逐張同步**——檔頭已加「v3.0 落差」段落列出 12 項，**轉 DDL 前必須完成** |
| `site/` 前台 | ✅ 四處女足導流文案已改（不再寫「本站不建立女足球隊資料」）；外連暫維持 `tcbw2014.com`，**待新網域確定後改指新站** |

**兩條仍然成立的判斷規則**：

1. **看到「女足不建立球隊資料」「`team.type` 預留 `women` 不啟用」一律是 v2.6 以前的舊規格。** `type` 的 `women` 值**已廢除**，改用獨立的 `Team.gender`（`men`／`women`／`mixed`）。
2. **`BW1` 不是第二個 `D1`。** `Team.code` 全站唯一，**不得改成「俱樂部 × 代號」複合鍵**——它是行事曆訂閱網址與 `/schedule/d1/` 的識別鍵，已在外流通。

---
