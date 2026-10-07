# db/seed/ — 本機開發用種子資料（S0-6c／S0-6d）

> 對應 [`../../docs/17-deployment.md`](../../docs/17-deployment.md)（本機開發環境）與
> [`../../deploy/README.md`](../../deploy/README.md)（S0-7a 本機骨架、本機資料庫容器）。
> 本目錄同時處理**兩個獨立資料庫**的種子資料，但用**兩支互不相干的腳本**：
> 主站庫（`tcrfc_club`，`apply-seed.sh`）與慈善庫（`tcrfc_charity`，`apply-charity-seed.sh`）。
> **這兩支腳本刻意不共用、不互相呼叫**——慈善捐款平台是獨立法人邊界（主辦與收款主體是
> 台灣足球策略發展協會，不是台中磐石足球俱樂部），見下方「慈善庫的種子資料」一節。
>
> 🔴 **2026-09-21 起：本機開發資料庫已合併進既有的 `sqlserver` 容器**（不再是本專案自己起的
> `mssql-dev` 服務），詳見 [`../../deploy/README.md`](../../deploy/README.md)「本機開發資料庫已
> 合併進既有的 `sqlserver` 容器」一節。本檔下方的指令已同步更新為新的容器與連線方式。
>
> 🔴 **2026-09-30 起：本機只保留兩個資料庫——網站 `tcrfc_club`、慈善 `tcrfc_charity`。**
> 舊的 `tcrfc_club_dev`、`tcrfc_club_test`、`tcrfc_charity_dev` 廢除；**不再有獨立測試庫**，
> `apps/api` 的 `dotnet test` 直接跑在 `tcrfc_club` 上。見下方「本機網站庫與整合測試（`tcrfc_club`）」一節。

## 正式庫的參照資料（`db/prod/`，2026-10-01）——🔴 種子資料絕不能灌進正式庫

本目錄其餘一切（`apply-seed.sh`、`generate-*-seed-sql.py`、`reset-admin-accounts.sh`）都是**本機開發**用，內含 mockup 內容、【測試】資料、虛構會員與訂單，以及**種子超管 `sa@system.local`／`Admin@123`**。**正式庫（Azure SQL）首次初始化只用 [`db/prod/`](../prod/) 底下的兩個檔案**，由 [`generate-prod-reference-sql.py`](generate-prod-reference-sql.py) 自原產生器**篩出**（定義仍只有一份，不另抄）：

- `db/prod/club-reference-data.sql`（主站庫）、`db/prod/charity-reference-data.sql`（慈善庫，獨立產生、獨立執行，docs/17 §5）。
- **允許清單制**：只有列在腳本 `ALLOW_CLUB`／`ALLOW_CHARITY` 的區段編號會輸出；之後新增的種子區段**預設不進正式庫**，要進必須在腳本裡明確加一行。輸出另有內容守衛（【測試】、`@example`、`*.test`、Argon2id 雜湊、測試密碼、`INSERT INTO admin_users` 任何一個出現就整份拒絕寫出）。
- 改了原產生器或允許清單後執行 `python3 db/seed/generate-prod-reference-sql.py` 重新產生並一併提交；`--check` 只比對、不一致 exit 1（可掛 CI）。
- 檔頭的 `-- MANIFEST table=n` 是各表預期筆數，`deploy/prod-db-init.sh` 灌完後逐表核對。
- 怎麼灌：[`deploy/prod-db-init.sh`](../../deploy/prod-db-init.sh)，步驟見 [`infra/README.md`](../../infra/README.md) §4.8。

**判準**：「沒有這些列，程式會丟例外、404、被外鍵擋住，或後台無法操作」＝系統運作必需；球員、新聞、頁面文案、賽程這類**後台都能自己建的內容**不算。

### 🔵 例外：驗收用的內容種子匯入（2026-10-03，使用者決定）

上面「種子資料絕不能灌進正式庫」的**帳號與假個資**界線不變。使用者另決定把**內容**部分匯入正式庫做前後台串接驗收、驗收完清除：由 [`generate-prod-content-sql.py`](generate-prod-content-sql.py) 篩出 `db/prod/*-content-seed.sql`，以 [`deploy/prod-seed-import.sh`](../../deploy/prod-seed-import.sh) 匯入／清除。區段歸類（匯入／參照／帳號／假個資）與限制見 [`docs/20`](../../docs/20-cicd.md) §5「正式庫的內容種子匯入與清除」。新增種子區段時要在該腳本的 `*_SECTIONS` 表態，否則產生器失敗。

### ✅ 灌（系統運作必需，皆取自規劃書的固定項目）

| 庫 | 表（筆數，含 `*_i18n`） | 來源／理由 |
|---|---|---|
| 主站 | `locales` 2 | 所有 `*_i18n` 側表的外鍵目標 |
| 主站 | `clubs` 2、`clubs_i18n` 3 | 台中磐石、台中藍鯨：路由與一切 `club_id` 的根。`domain` **不寫死**，取自 VM `/opt/tcrfc/.env` 的 `TCRFC_DOMAIN`／`BW_DOMAIN`（暫用網址階段就是 stg 網域）。藍鯨英文全名仍待確認，故 `clubs_i18n` 藍鯨只有中文 |
| 主站 | `admin_roles` 10、`permissions` 275、`role_permissions` 799 | 規劃書 §6 角色與權限矩陣；非系統管理員的授權完全依賴這三張表 |
| 主站 | `article_categories` 8（＋16）、`faq_categories` 10（＋20）、`faq_embed_slots` 4 | 規劃書 7.1–7.8／3.12／G-12 的固定字典；新聞與 FAQ 的外鍵目標 |
| 主站 | `home_sections` 18、`forms` 18、`form_fields` 114（＋228） | 首頁九大固定區塊、九個固定表單與預設欄位，兩俱樂部各一份（後台只能排序／編輯，不能憑空新增） |
| 主站 | `event_types` 6（＋12） | L2 自建事件的起始分類（記者會、簽名會……）；後台 L3 可再編輯 |
| 慈善 | `locales` 2、`admin_roles` 9、`permissions` 24、`role_permissions` 45 | 慈善後台自己的帳號體系（與主站庫互不包含） |

**`settings` 沒有任何必需列**：抽樣核對的鍵（`member.no_prefix`／`member.no_digits`、`shop.*`、`geo.crawler_*`）程式都有預設值；其餘鍵請在後台驗收時逐一確認「缺列時前台與 API 的行為」。

### ⛔ 不得進正式庫（測試或 mockup）

- **帳號**：種子 `admin_users` 全部（`sa@system.local`、`*@tcrfc.test`……）、`admin_user_roles`／`admin_user_clubs`。第一個管理員由 `create-admin` 建立。
- **全部【測試】內容**：頁面草稿、輪播（`seed-placeholder/no-image`）、tcrfc 的 FAQ／課程／梯次／積分榜／行事曆事件／夥伴／贊助／提案與 Lead／慈善與社會影響／媒體專區／榮譽／試訓與報名／會員與會籍與球衣／特約店家與權益／漫畫／球迷會活動／商店設定與商品與訂單／抽獎／電子報名單／App 裝置與推播與廣告／診斷回報，以及兩俱樂部的 `site.contact_phone`（`04-0000-0000`）與 `site.contact_hours`。
- **mockup 骨架**：`players`／`staff`／`matches`／`articles`（`site/src/data/*.json`）。
- **慈善庫種子**：`charity_refs`、店家、項目、捐款、金流、發票、結算、對帳、稽核、`email_logs`、`payment_channels`（sandbox 占位憑證）、四個種子帳號——全部虛構。

### ❓ 待使用者或客戶決定（我不自行判定，預設不灌）

| # | 項目 | 為什麼卡住 | 預設 |
|---|---|---|---|
| 1 | **球隊（`D1`／`BW1`／`BW-U15`／`BW-U12`）、球季、賽事系列（企甲）、場地（西屯足球場）** | 後台都能建，屬內容；但 `site.squad_codes`、行事曆訂閱網址（`/schedule/d1/`）依賴球隊代號。球季起訖是種子由賽程**推導**（非官方）。`BW-U15`／`BW-U12` 的性別與年齡帶沒有來源 | 不灌；上線前在後台建，或決定匯入範圍後另案 |
| 2 | **`site.*` 站台事實**（成立年、聯賽名、梯隊敘述、藍鯨網址；GEO-03／04 與 `llms.txt` 的事實來源） | 種子的值是「已核實真實值」，但電話與營業時間是測試值、藍鯨英文全名待確認 | 不灌；後台「網站設定」填 |
| 3 | **藍鯨舊站整理的真實內容**（2024 名單、教練團、賽事 21 場（另有 2026-10-07 補的 25/26 木蘭 21 場，同屬內容種子）、里程碑、夥伴 26、FAQ 10、課程 12、活動） | 含真人姓名與經歷；規劃書與 CLAUDE.md 第 7 條要求肖像／個資同意在先 | 不灌 |
| 4 | **頁面真實文案、SEO／GEO 預設**（願景使命、`seo.title_template`、`geo.llms_*`） | 已核實但屬內容；是否以種子文案為起點由客戶決定 | 不灌 |
| 5 | **301 轉址**（舊網址 → 新網址） | 客戶「決定」欄至今為空，對應頁是種子推測 | 不灌；切正式網址前處理 |
| 6 | **`event_types` 的色碼與圖示** | 種子註解自述「隨意選用」，不是客戶定案 | 已灌（後台可改）；若要等客戶定案再灌，從 `ALLOW_CLUB` 移除 `23` |
| 7 | **行動 App 固定資料**（`app_deep_links` 8 條、`app_layout_items`、`app_feature_flags` 預設） | 部分取自規劃書 §2.3／`docs/19`，部分是測試；App 客戶端尚未開發 | 不灌；App 上線前決定 |
| 8 | **慈善 `settings`（`donation.*` 文案）與 `email_templates`（4 封信）** | 種子內容是「開發占位文字」，正式文案待協會與法務確認；**缺列時慈善前台的行為尚未核對** | 不灌；上線前由協會提供文案並先驗證缺列行為 |
| 9 | **正式網域切換後 `clubs.domain`** | 初始化時是 stg 網域 | 切網址（infra §5）時在後台「俱樂部」改 |

## 本機驗收專用測資（區段 60，2026-10-05）——只給本機，不進正式庫

補 S2-11 球衣登記與 S3-2 漫畫閱讀器「缺資料未驗」的最小假資料（全部虛構，`generate-prod-content-sql.py` 分類為 `PERSONAL`，**不會進 `db/prod/`**）：

| 資料 | 內容 | 怎麼用 |
|---|---|---|
| 會員 `M900101`（`jersey-single@example.com`） | tcrfc 2026-27 單人方案、有效付費會籍、球衣 0／1 件 | 登入後在會員中心登記球衣，登記第二件會被擋（`jersey_quota_reached`） |
| 會員 `M900102`（`jersey-family@example.com`） | tcrfc 2026-27 家庭方案、有效付費會籍、球衣 0／3 件 | 走滿三件、修改（`pending` 才可改）、超量被擋 |
| 漫畫第 101、102 集 | tcrfc、已發布、各 3 頁（800×1200），102 為最新 | 前台 `/zh/culture/manga/`（閱讀器、上下集導覽、閱讀數） |

- 登入密碼與其他種子會員相同（`ContentEditor@123`，僅本機）。既有 `M900001`／`M900002` 的球衣額度已用滿或剩 1 件，不足以走完整條路，故另建。
- 集號取 101／102，遠低於整合測試用的 9000 段，與既有 3 集草稿互不干擾。
- 漫畫圖片的**物件**不在資料庫：先 `apps/api/scripts/dev-azurite.sh up`，再 `python3 db/seed/seed-dev-blobs.py` 傳占位圖（含 1280／640／320／thumb 衍生檔，鍵 `seed-dev/comic/{集}/{頁}.webp`）。
- 守門測試：`DevAcceptanceSeedTests`（唯讀，種子被改掉或庫重建沒灌這一段會先紅燈）。已存在的開發庫補灌：`apply-seed.sh`（冪等，只補缺的列）。

## 英文欄位回填（區段 61，2026-10-05）——主站英文版的 `*_i18n` en 列

[`en_backfill_seed.py`](en_backfill_seed.py) 由 `generate-club-seed-sql.py` 在所有區段之後呼叫，補齊 en 列（第一、二輪只對 tcrfc；**第三輪補藍鯨 bw，見下方「第三輪：藍鯨」**）。以下為主站範圍：新聞標題（`articles_i18n.title`，83 篇，第二輪；本文與摘要不存在故不補）、競賽名、賽程場地、教練職稱、輪播、頁面 SEO、FAQ、自建行事曆事件、夥伴、贊助、媒體專區、特約店家、慈善與社會影響、會員抽獎、`site.founding_title`。

- **為什麼另開區段**：各區段是「繁中列不存在才整批 INSERT」，已灌過的庫改來源不會補 en；回填用「缺 en 列就 INSERT、已有 en 列只填 NULL／空字串／與繁中完全相同的欄位」，**不覆寫後台改過的英文**，可重複執行。
- **用詞**：照 [`docs/06`](../../docs/06-conventions.md) §1.1；聯賽沿用 `Enterprise Premier League`（正式名待客戶確認）；【測試】內容維持 `[Test] …` 標記；賽程場地英文名（台北田徑場等）為地名拼音＋通用場地詞，**待客戶確認**。
- **刻意不補**：沒有英文來源的球員與教練姓名（不音譯，前台回退中文原名）；新聞標題內沒有英文來源的對手隊、人名、學校、地名（保留中文原名夾在英文句內，不音譯，清單待客戶補英文名）；`site.founding_date_display`（`SiteFactsTests` 用它驗證缺英文回退）；`site.contact_hours`／`contact_phone`（測試值）；頁面隱私／條款內文；`page_blocks` 與 `charity_programs_i18n.content` 這類 json 內文。
- **正式庫內容種子**：`generate-prod-content-sql.py` 將區段 61 列為 `IMPORT`；`member_draws_i18n` 屬禁用表，該兩批自動剔除。改動後須重產 `db/prod/club-content-seed.sql` 並更新 `club-content-manifest.tsv` 的 sha256 與各 `*_i18n` 筆數（見該腳本 `--check`）。

### 第三輪：藍鯨（bw，2026-10-05，B-5 定案）

B-5 已於 2026-10-05 拍板：藍鯨英文簡稱 `Taichung Blue Whale`、全名 `Taichung Blue Whale Women's Football Club`（舊站 Bluewhale 等變體不用）。`en_backfill_seed.py` 檔尾（同一區段 61，不另開區段編號）補藍鯨，規則同前（缺 en 列就 INSERT、已有 en 列只補 NULL／空／與繁中相同者，冪等）。

- **`clubs_i18n`**：`generate-club-seed-sql.py` §1 的 bw 首次建立區塊改為一併插入 en 列（原註解「簡稱英文一律不插」已更新）；既有庫由區段 61 補（已有 en 列只補 NULL）。🔴 `clubs_i18n` 是**參照表**，所以：① `db/prod/club-reference-data.sql` 會帶 en 列（`MANIFEST clubs_i18n=4`）；② 區段 61 內這一批在內容種子中被剔除（`DROPPED section=61 … tables=clubs_i18n`）。**已初始化完成的正式庫不會因重產檔案而得到藍鯨英文名稱**，需要另行補一次（見回報／STATUS）。
- **補齊範圍（只動藍鯨）**：賽事系列 2、場地 2（豐原體育場、北屯太原）、賽程場地 21（10 種寫法）、教練職稱 4 種＋經歷 4 人、里程碑 12（含描述）、輪播 2、頁面 SEO 4、FAQ 10、課程與活動 12、自建行事曆事件 3、夥伴 8（只補有英文來源者）、新聞 3（【測試】）、站台設定 6（聯賽名／簡稱、SEO 標題樣板與預設描述、llms 定位與事實摘要）、【測試】商品／會籍／球迷活動／系列／特約店家。`teams`、`players`、`staff` 姓名、`calendar_team_settings` 早有 en 列，不動。
- **用詞**：木蘭聯賽 Taiwan Mulan Football League；總統盃 President's Cup；`AFC Club Licence`；場地 Taichung Beitun Taiyuan Football Field（太原足球場各種寫法同一座）、Taichung Fengyuan Stadium；Blue Whale Cup；青年隊稱 Youth（U15／U12 girls' teams），**不得出現 Academy**；協會採 `Taichung Women's Football Association`（與 `club-copy-en-core.ts`、`docs/06` §1.1 一致；規劃書藍鯨英文版寫 Taichung City Women's Football Association，待客戶確認）。
- **刻意保留中文（en 欄位留 NULL 或句內原樣保留，前台逐欄位回退）**：沒有英文來源的選手與教練人名（有資料庫英文列者用其羅馬拼音）、學校（五權國中、惠文高中）、公益機構（台中惠明盲校）、公司名夥伴（17 家）、活動名「夏洛特的下午茶」、場地地址與交通說明（含公車業者、管理單位）、`site.contact_hours`（測試值）、`clubs_i18n.description`。
- **待客戶確認的寫法**：`Sport i Taiwan 2.0 sports hotspot`（運動 i 台灣 2.0 運動熱區）、Pick-up Adult Football Matches（野團）、`Taichung Blue Whale Football School`、賽程場地的地名拼音（Kaohsiung Nanzih、Qingpu、Meilun Junior High School、Ming Chuan University）、教育部體育署／國立臺灣體育運動大學的英文名、教練經歷中的賽事名（Chinese Taipei women's national team、East Asian Cup 等）、2022 里程碑「疫情有成舉辦首場頂級足球開門賽」原文語意不明（採字面譯）。
- **品牌外洩**：藍鯨英文內容不含 Taichung Rock／TCRFC／Academy（詞表見 `apps/web/scripts/check-club-brand-leak.mjs`），產生的 SQL 已 grep 驗證。
- **正式庫內容種子與 manifest**：重產 `club-content-seed.sql` 後，`club-content-manifest.tsv` 的 sha256 與各 `*_i18n` 筆數以腳本精準更新（斷言舊值）：articles_i18n +3、banners_i18n +1、calendar_custom_events_i18n +3、competitions_i18n +2、faqs_i18n +10、matches_i18n +21、milestones_i18n +12、pages_i18n +4（2026-10-07 固定頁改版後：pages 22／page_blocks 33／page_versions 22／pages_i18n 44；manifest 以計算值更新，尚未經本機演練 record-manifest 核對）、partner_stores_i18n +1、partners_i18n +8、programs_i18n +12、settings_i18n +6、venues_i18n +2。
- **藍鯨 25/26 木蘭 21 場（2026-10-07）**：`generate-club-seed-sql.py` §13 加 `emit_bw_matches("matches-2025-26-mulan.json", "league")`（來源 [`content/blue-whale/data/matches-2025-26-mulan.json`](../../content/blue-whale/data/matches-2025-26-mulan.json)，官方行事曆）、§11 的 `seasons.json` 補 `2025-26`。藍鯨 `matches` 21→42、`matches_i18n`／`match_teams` 同步、`seasons` 2→3。競賽沿用 `mulan` 那一列（唯一鍵 `(club_id, code)`，不依球季另建，`competitions` 不變）；`status=played`、比分 NULL；**場地只連太原**（`venue_id` 其餘為 NULL，西屯足球場等寫在 `matches_i18n.venue` 原文，因 §13 在 §24 建西屯之前執行）；**`venue_address_zh`、`calendar_home_tag` 無對應欄位，不匯入**。開幕戰採行事曆版本（見 `calendar.md` §3）。這批屬 `IMPORT` 區段，`db/prod/club-content-seed.sql` 與 manifest 已重產／重錄。
  - ✅ **`import` 的 sqlcmd 13 `Invalid cursor state` 已修（2026-10-07，E-295）**：`deploy/prod-seed-import.sh import` 讀到區段 61 的批次時 sqlcmd 13 會吐 `SqlState 24000`、交易靜默回滾、結束碼 0；**在正式 VM（x86）同樣發生，不是模擬問題**。現在 `import` 以 `SET NOCOUNT ON` 包一層再 `:r` 種子（種子檔與 manifest 不變），並在執行後立刻查匯入標記。**本機演練不必再手動改用 `mssql-tools18`**，整個流程完全走腳本即可。
- **API 測試**：`AppContractBatch4Tests`（俱樂部簡稱）、`AppContractBatch5Tests`（後台簡稱讀寫）、`LocalizationFallbackTests`（俱樂部名稱）原本把「藍鯨沒有英文」當前提，已改為新定案值斷言；回退行為改經後台 `PUT /admin/clubs/{id}`（`en = null` 即刪列）自建並於 `finally` 還原。⚠️ 後台 PUT 的 `en = null` 會**刪掉英文列**，測試改動藍鯨時必須把原英文內容帶回。

## 🔴 灌種子一律「docker cp ＋ sqlcmd -i」，不用 stdin 串流（2026-10-07，E-296）

`apply-seed.sh`（與 `apply-charity-seed.sh`）原本用 `docker exec -i … sqlcmd < 檔案` 餵 SQL。sqlcmd 對管線分塊讀取，**3 位元組的中文字剛好跨塊就會被切成兩個 U+FFFD**（實例：`achievements_i18n` 的「示範友誼賽」→「示範��誼賽」）。斷點隨時序而變，多數次正常、偶爾壞一處；而種子的冪等判斷是字串比對，壞掉的列比對不到，下一次就重複 INSERT，在區段 39 以 `PK_achievements` 重複中止，其後區段（會籍方案、`M900001` 起會員、權益、藍鯨英文回填、區段 60）全不會灌。產生器輸出的 SQL 是正確的。

現在的做法：先 `docker cp` 進容器、`sqlcmd -i` 讀檔（密碼走容器環境變數 `SQLCMDPASSWORD`，不在指令列），套完後**掃全庫 nvarchar 欄位，含 U+FFFD 即 exit 1**。若看到這個錯誤：先 `SELECT` 列出髒列、確認是亂碼，刪掉髒列（主列連同子列；只有字串被切壞的非主鍵欄位直接 `UPDATE` 成產生器的原字串）後重跑，冪等補回。


## 這個目錄有什麼

| 檔案 | 用途 |
|---|---|
| [`generate-club-seed-sql.py`](generate-club-seed-sql.py) | 讀 [`site/src/data/*.json`](../../site/src/data/)（六個 mockup 資料檔），產生冪等的 T-SQL——灌進 `tcrfc_club` |
| [`backoffice_seed.py`](backoffice_seed.py) | 🔴 **2026-09-30 新增**：後台已完成模組的種子資料定義與 T-SQL 產生（頁面、輪播、FAQ、課程與梯次、積分榜、自建事件、新聞標籤、301 轉址、SEO／llms.txt／AI 爬蟲設定）。由 `generate-club-seed-sql.py` 尾端 `import` 呼叫，**不是獨立執行的腳本**；真實內容與測試值的界線見下方「後台模組種子（2026-09-30）」 |
| [`apply-seed.sh`](apply-seed.sh) | 呼叫上面那支腳本，再用 `sqlcmd` 把產生的 SQL 灌進本機 SQL Server（既有 `sqlserver` 容器）；容器名稱可用 `LOCAL_MSSQL_CONTAINER` 環境變數覆寫，**目標資料庫寫死只認 `tcrfc_club`**（原 `SEED_TARGET_DATABASE` 切換用途已於 2026-09-30 移除，殘留該變數且值不同會直接拒絕執行） |
| [`setup-club-db.sh`](setup-club-db.sh) | 一鍵建立／重建本機網站庫 `tcrfc_club`（串接 `deploy/local-ddl.sh --apply-club-db` ＋本檔的 `apply-seed.sh`；前身是 `setup-test-db.sh`），見下方「本機網站庫與整合測試」一節 |
| [`generate-charity-seed-sql.py`](generate-charity-seed-sql.py) | 產生 `tcrfc_charity` 冪等的 T-SQL。**資料直接寫在腳本內**（不像 club 腳本讀外部 JSON）——因為這批資料**從一開始就是虛構測試資料**，不是需要另外隔離的真人個資，見下方一節的說明 |
| [`apply-charity-seed.sh`](apply-charity-seed.sh) | 呼叫上面那支腳本，灌進 `tcrfc_charity`；目標資料庫寫死只認 `tcrfc_charity`，與 `apply-seed.sh` 的白名單分開維護 |
| `.generated/`（**不進版控**） | 兩支產生器的輸出（`club-seed.local.sql`／`charity-seed.local.sql`），隨時可重新產生，見 [`.gitignore`](../../.gitignore) |

## 為什麼種子資料用「讀 JSON 產生 SQL」而不是寫死在腳本裡

`site/src/data/*.json`（`players.json`／`staff.json`／`coaches-d1.json`／`coaches-academy.json`）
含球員與教練的真實姓名，屬個資性質欄位（CLAUDE.md 第 7 條）。這些 JSON 本身已經是 mockup 的一部分、
已經在版控裡（且公開站台本來就會顯示球員名單，不是新的揭露），所以**灌資料本身不是新增風險**；
但 `generate-club-seed-sql.py` 這支「邏輯腳本」刻意不重複硬編碼任何姓名——它只寫「JSON 欄位對到哪張表
哪個欄位」的規則，**真正含個資的內容只活在 JSON 來源與執行期產生的 `.generated/*.sql`（不進版控）**。
這樣即使未來球員異動，也只需要改 JSON，不必動這支腳本。

## 怎麼從零開始（本機第一次建置）

```bash
# 0. 確認 .env 有 MSSQL_DEV_SA_PASSWORD，且與既有 sqlserver 容器的 SA 密碼一致
#    （這不是本專案自訂的密碼——那是別的專案在用的既有容器，密碼請向其擁有者要，
#      或用 `docker inspect sqlserver --format '{{range .Config.Env}}{{println .}}{{end}}'` 查）
set -a && source .env && set +a

# 1. 確認既有的 sqlserver 容器已在跑（不是本專案啟動它，也不是本專案 compose 管理的服務）
docker ps --filter name=sqlserver

# 2. 灌 DDL（兩個庫都會建：tcrfc_club、tcrfc_charity，建在既有 sqlserver 容器裡）
./deploy/local-ddl.sh --apply

# 3a. 灌主站庫種子資料（只灌 tcrfc_club；或用 ./db/seed/setup-club-db.sh 一鍵 DDL＋種子）
./db/seed/apply-seed.sh

# 3b. 灌慈善庫種子資料（只灌 tcrfc_charity；兩支腳本互不相依，順序不影響結果）
./db/seed/apply-charity-seed.sh
```

⚠️ **2026-09-21 之前**這裡第 1 步是啟動本專案自己的 `mssql-dev` 服務——那個服務已退場，
現在改成「確認既有容器在跑」，本專案不負責啟動或管理它，見
[`../../deploy/README.md`](../../deploy/README.md)。

## 怎麼重來一次（清掉重灌）

種子腳本本身是冪等的（可重複執行、不會長出重複資料），**正常情況下不需要重來**。
真的要從乾淨狀態重建（例如懷疑資料被手動改壞）：

```bash
set -a && source .env && set +a
# 只丟 tcrfc_club，不要動 tcrfc_charity。一鍵版（DROP＋CREATE＋DDL＋種子）：
# ⛔ DROP／CREATE 只准對 tcrfc_club、tcrfc_charity 這兩個名字，絕不對這個 instance 上其他資料庫做同樣的事
#    （這個容器裡還有使用者另一個專案的約 25 個既有資料庫）；腳本已把名稱寫死。
./db/seed/setup-club-db.sh --recreate
```

⚠️ **2026-09-21 之前**「完全砍掉重練」的做法是 `docker compose down -v` 連 volume 一起刪、
容器重建。**現在不能這樣做**——`sqlserver` 是既有容器，不是本專案 compose 管理的，`down -v`
管不到它，而且那樣做等於刪除使用者另一個專案的全部資料庫（見
[`../../deploy/README.md`](../../deploy/README.md)「⛔ 這個既有容器沒有掛 volume」）。
本機重來的正確做法就是上面 `setup-club-db.sh --recreate` 那段，只對這兩個資料庫本身動手，
不對容器動手。

## 本機網站庫與整合測試（`tcrfc_club`，2026-09-30 起）

🔴 **背景**：S0-13（2026-09-25）曾為 `dotnet test` 另建獨立測試庫 `tcrfc_club_test`，避免與本機開發／無頭瀏覽器
實走共用同一個庫而互相干擾。**2026-09-30 使用者裁決合併**：本機只保留 `tcrfc_club`（網站）與
`tcrfc_charity`（慈善），`dotnet test` 直接跑在 `tcrfc_club` 上。**已知代價**：測試會改動後台看到的資料
（測試帳號的 2FA 狀態、`settings` 的 SEO 值、孤兒測試資料），中途失敗可能留下殘骸；需要時重灌種子即可。
（見 [`../../docs/14-invariants.md`](../../docs/14-invariants.md)「S0-13」、
[`../../docs/18-work-errors.md`](../../docs/18-work-errors.md)。）

**一鍵建立／灌種子**：

```bash
set -a; source .env; set +a   # 取得 MSSQL_DEV_SA_PASSWORD
./db/seed/setup-club-db.sh --recreate   # 從零重來：先 DROP（若存在）再 CREATE，灌 DDL＋種子
./db/seed/setup-club-db.sh              # 平常用：資料庫不存在才建立＋灌 DDL，種子一律重灌（冪等）
./db/seed/setup-club-db.sh --dry-run    # 只產生種子 .sql，不建庫也不套用
```

這支腳本只是把既有兩支腳本串起來，沒有另外寫一份建庫或灌種子的邏輯：

1. `deploy/local-ddl.sh --apply-club-db [--recreate]`——建立（或重建）`tcrfc_club`，
   灌入轉換過 `json→nvarchar(max)` 的 `club-schema.sql`。`CREATE TABLE` 不是冪等的，所以資料庫
   已存在且已有資料表時**預設略過 DDL**（只重灌種子），要重灌表結構才需要加 `--recreate`。
2. `db/seed/apply-seed.sh`——灌種子，含全部 `ADMIN_USERS` 測試帳號
   （見 `apps/api/README.md`「種子測試帳號」）。

**防呆**：`deploy/local-ddl.sh`（`tcrfc_club`／`tcrfc_charity`）與 `db/seed/apply-seed.sh`、
`setup-club-db.sh`（只認 `tcrfc_club`）的資料庫名稱都寫死，白名單以外的名字一律拒絕執行。
`apps/api` 這一側另在 `Tcrfc.Api.Tests/Fixtures/TestDatabaseGuard.cs` 加第二道防線——**`dotnet test`
用的 `CLUB_SQL_CONNECTION_STRING` 若沒有精確指向 `tcrfc_club`，六個 fixture 一律在 `InitializeAsync()`
直接拒絕啟動**（擋住 `tcrfc_charity`、已廢除的舊名稱與容器內其他專案的資料庫），見
[`../../apps/api/README.md`](../../apps/api/README.md)「怎麼跑」。

## 連線字串長什麼樣

跟 [`deploy/dev/club.env.example`](../../deploy/dev/club.env.example) 一致：

```
Server=host.docker.internal,1433;Database=tcrfc_club;User Id=sa;Password=<MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;
```

**從宿主機（不是容器內）連線**：`Server=127.0.0.1,1433;...`（既有 `sqlserver` 容器對外發布的
標準 SQL Server port）。

⚠️ **2026-09-21 之前**這裡是 `Server=mssql-dev,1433`（容器間走 compose 服務名稱）／
`Server=127.0.0.1,14330`（宿主機走本專案自訂的對外埠）。現在資料庫是宿主機上一個獨立於
本專案 compose 的既有容器，容器之間無法用服務名稱互連，一律要透過 `host.docker.internal`
連回宿主機（`docker-compose.dev.yml` 的 `api` 服務已加對應的 `extra_hosts`），且對外埠是
標準的 `1433`（不是 `14330`）。

## 種了什麼資料

| 來源 JSON | 灌進哪些表 | 筆數 |
|---|---|---|
| — | `locales`（`zh-Hant`、`en`——DDL 本身不含種子，是 `*_i18n` 側表 FK 的必要前提） | 2 |
| — | `clubs`／`clubs_i18n`（台中磐石 `tcrfc`＋台中藍鯨 `bw`，**藍鯨只有 Club 主檔**，見下方「藍鯨為什麼只有這一筆」） | 2／3 |
| — | `teams`／`teams_i18n`（只有台中磐石一線隊 `D1`） | 1 |
| `schedule.json`（日期範圍推導） | `seasons`（`2026-27`） | 1 |
| — | `competitions`／`competitions_i18n`（企業甲級足球聯賽） | 1 |
| — | `article_categories`／`article_categories_i18n`（規劃書 7.1–7.8 全部八個分類） | 8 |
| `players.json` | `players`／`players_i18n` | 28 |
| `staff.json`（含 `coaches-d1.json`／`coaches-academy.json` 用於判斷 `staff_teams`） | `staff`／`staff_i18n`／`staff_teams` | 8／4 |
| `schedule.json` | `matches`／`matches_i18n`／`match_teams` | 21 |
| `news.json` | `articles`／`articles_i18n` | 83 |
| — | `venues`／`venues_i18n`（S1-12d 新增：台中磐石主場「西屯足球場」；台中藍鯨的太原／豐原兩筆早於本表列出的版本就已建立，見「藍鯨場地」段） | 3 |
| `apps/web/shared/utils/site-facts.ts`（已核實真實值） | `settings`／`settings_i18n`（`setting_group='site'`，`GEO-03`／`GEO-04` 站台事實，見 `apps/api/README.md`「S1-12d」節；電話與營業時間為 2026-09-30 補的**測試值**） | 兩俱樂部各約 11 個鍵，**另加 `site.blue_whale_site_url` 只種給 `tcrfc` 一筆**（主站規劃書 §3.6，2026-09-29 後續補，見 `apps/api/README.md`「S1-12d」節「藍鯨官網網址」） |

## ⛔ 哪些事不能做

- **不得對 `tcrfc_charity` 執行任何跨庫 JOIN**（例如 `SELECT ... FROM tcrfc_charity.dbo.x JOIN tcrfc_club.dbo.y`）。
  本機兩庫在同一個 instance 裡，這種寫法**本機測得過、正式環境的兩個獨立 Azure SQL 一定爆**，
  而且違反的是法遵邊界不只是相容性——見 [`../../deploy/README.md`](../../deploy/README.md) 與
  [`../../docs/14-invariants.md`](../../docs/14-invariants.md)。
- **不得對這個既有 `sqlserver` 容器本身動手**：不得重建、不得改埠繫結、不得加 volume、
  不得改任何設定、不得 `docker rm`／`docker restart`。它是使用者另一個專案在用，裡面還有
  約 25 個既有資料庫，且**沒有掛任何 volume**（重建＝資料全滅，見
  [`../../deploy/README.md`](../../deploy/README.md)）。本目錄的腳本只被授權在裡面操作
  `tcrfc_club`（`db/seed/apply-seed.sh`、`setup-club-db.sh`）與 `tcrfc_club`／`tcrfc_charity`
  （`deploy/local-ddl.sh`）這兩個資料庫，寫死白名單、不接受呼叫端覆寫。
- **不得把 `db/seed/.generated/*.sql` 加進版控**——它含真實姓名，`.gitignore` 已排除，不要用 `git add -f` 硬加。
- **不得把種子資料當成正式內容的替代品**：`players.json`／`news.json` 等六個 JSON 檔本身是 mockup 骨架，
  不是後台維運後的真實資料——例如 `articles.cover_key` 全部是 `NULL`（沒有走過圖片上傳 pipeline）、
  `news.json` 的 `body_zh` 全部是 `null`（文稿還在 `.gdoc` 裡讀不到）。**這些留白是刻意的，不要為了畫面好看去填假資料。**

## 藍鯨為什麼只有這一筆

台中藍鯨的球員名單、教練、賽程客戶還沒給（`STATUS.md` B-5、B-6），不得編造。本次只建立 `clubs` 主檔一筆，
讓 `club_id` 的兩俱樂部維度在本機是活的（品牌色、`is_collecting_subject` 等已定案的事實見 docs/14）。
`clubs.domain` 用了一個明確標示「非真實」的技術佔位值（`bw-domain-pending.invalid`，`.invalid` 是 IANA
保留給這類用途的 TLD）——`domain` 欄位是 `NOT NULL UNIQUE`，正式網域待 B-4 解除前無法留白，
**這個值不得沿用到任何正式環境**，正式網域確定後由後台人工更新。

## 慈善庫（`tcrfc_charity`）的種子資料

**與主站庫的種子資料是兩回事，不要混為一談**：

- **這批資料全部是虛構測試資料**，不是像 club 種子那樣「mockup 用的真實骨架資料（球員名單、
  新聞標題等）」。捐款人姓名、Email、電話、統一編號、身分證字號、「加密」欄位的內容，
  一律是清楚標明假的占位值（姓名如「測試用．王小明」、Email 一律 `@example.test`、
  電話與統編一律用連續 0 這種明顯不可能存在的號碼、`*_encrypted` 欄位是
  `ENC-PLACEHOLDER-NOT-REAL::...` 這種明講「不是真的加密」的字串——加密是應用層職責，
  本機種子階段尚未實作）。**正因為資料本身就是虛構的，不像 club 種子有真人個資需要隔離，
  所以 `generate-charity-seed-sql.py` 把資料直接寫在腳本內，不像 club 腳本那樣讀外部 JSON。**
- **`db/seed/apply-seed.sh` 與 `db/seed/apply-charity-seed.sh` 是兩支完全獨立的腳本**，
  各自的目標資料庫白名單寫死、互不共用、互不呼叫。這不是疏漏，是刻意的——慈善捐款平台
  自 v2.0 起是獨立法人邊界（主辦與收款主體是台灣足球策略發展協會，不是台中磐石足球俱樂部，
  見 [`../../docs/16-charity-schema.md`](../../docs/16-charity-schema.md) §0／§9）。把兩庫的
  種子混進同一支腳本，等於在程式碼層面抹掉這條邊界。
- **不得對 `tcrfc_club` 執行任何跨庫 JOIN**，也不得把兩個資料庫的種子腳本合併——理由同上一節。

**種了什麼**（29 張表：25 主表 ＋ 4 張 `*_i18n`；`ui_strings`／`ui_string_translations` 刻意留空
——本次任務範圍未列，也還沒有實際介面字串內容可種）：

| 分類 | 表 | 筆數 | 涵蓋的狀態／分支 |
|---|---|---|---|
| 語系 | `locales` | 2 | zh-Hant／en |
| 後台帳號權限 | `admin_roles`／`permissions`／`role_permissions`／`admin_users`／`admin_user_roles` | 9／**27**／**49**／4／4 | 沿用主站九個角色；5 個與慈善無對應職能的角色刻意不掛權限（見腳本註解）。🔴 **CH-3（2026-10-01）**：①新增 API 專用權限碼 `n3.donation.recheck_payment`（腳本 §3b，**刻意不併入匯出給前端 fixtures 的 `PERMISSIONS`／`ROLE_PERMISSION_MAP`**，免得逼兩個前端同步改檔）；②四個種子帳號改用**真的 Argon2id 雜湊、可登入**（`sa@charity.local`／`Admin@123`、`cs.admin@`／`biz.admin@`＝`ContentEditor@123`、`viewer@`＝`Viewer@123`，🔴 只供本機開發）。CH-1b 時期建好的庫裡仍是 `DEV-SEED-` 占位雜湊，重跑 `apply-charity-seed.sh` 會升級（只動仍是占位的列）。🔴 **CH-4／CH-5（2026-10-02）**：再新增三個 API 專用權限碼（同在腳本 §3b 的 `EXTRA_PERMISSIONS`）——`n4.settlement.mark_paid`（登記已付款，與執行結算分離）、`n7.audit_log.view`（稽核查詢，僅系統管理員）、`n3.donation.hide_credit`（徵信名單逐筆隱藏，系統管理員與客服／行政）；已建好的庫由 EF migration `AddCh4Ch5Permissions` 補上，不必重灌種子 |
| 主站唯讀複本 | `charity_refs`／`charity_program_refs` | 2／3 | 虛構占位（協會統編未定，不得沿用正式環境） |
| 店家 | `donation_stores`（+i18n） | 3 | 不同類別、有／無 Logo、合作中／已停止 |
| 項目 | `donation_projects`（+i18n）／`donation_amount_options` | 3／10 | 已上架 ×2（不同 `invoice_mode`）＋已下架 ×1（⚠️ 綱要沒有「已結束」狀態值，見腳本註解的已知缺口） |
| 捐款 | `donations` | 10 | `created`／`pending`／`paid`×5／`failed`／`expired`／`refunded` 六種狀態全數涵蓋 |
| 金流 | `donation_payments` | 9 | `requested`／`confirmed`／`failed`／`cancelled`；D04（`created`）刻意不建，示範「金流尚未起動」 |
| 發票 | `donation_invoices` | 6 | 已開立／待開立／開立失敗／已作廢／折讓，涵蓋三種 `carrier_type`（手機條碼載具／統一編號／捐贈發票） |
| 結算 | `settlements`／`settlement_lines` | 6／12 | 一個完整結算週期（2026-08，含 `paid`／`settled`）＋下一期（2026-09 上半）的退款沖回負項 |
| 對帳 | `reconciliation_runs`／`reconciliation_discrepancies` | 2／3 | 一次乾淨、一次涵蓋三種差異類型（`site_only`／`gateway_only`／`amount_mismatch`） |
| 稽核 | `audit_logs` | 3 | 退款／分潤百分比設定／含個資明細匯出三類法遵稽核 |
| 機制表 | `settings`（+i18n）／`email_templates`（+i18n）／`email_logs`／`payment_channels` | 7／4／4／2 | 站台文案、四封系統信、寄送紀錄（`sent`／`failed`）、LINE Pay 與電子發票憑證（皆 sandbox 占位） |

**已知的綱要缺口**（回報用，未動 `db/charity-schema.sql`，改綱要要先改 `docs/16` 再回頭改 DDL）：

1. `donation_projects.status` 只有 `draft`／`published` 兩值（`CK_donation_projects_status`），
   且沒有起訖日欄位——**無法區分「已下架」與「已結束」**，兩者在目前的資料模型裡是同一個
   狀態值。本次種子用 `draft` 代表「已下架（含已結束的情形）」，不新增狀態值也不新增欄位。
2. `docs/16a-charity-field-audit.md` 已盤點出的其餘缺漏（`DonationInvoice` 抬頭欄位、
   `Settlement.remit_method`、`SettlementLine.clawback_reason` 等）**`db/charity-schema.sql`
   已經補上**，本次種子直接使用，未發現新的缺口。

## DDL 改了、既有本機庫沒跟上——以後一定會再發生

`db/club-schema.sql`／`db/charity-schema.sql` 是持續在改的交付物（真實來源是 `docs/12`／`docs/16`），
但本機的資料庫（既有 `sqlserver` 容器裡的 `tcrfc_club`／`tcrfc_charity`）是「建好一次、
之後重複重跑種子腳本」在用，**兩者不會自動保持同步**。
2026-09-21（`matches.match_no` 補進規格與 DDL）就踩過一次：本機庫是在這個欄位補進 DDL 之前建的，
`ALTER TABLE` 之類的 schema 變更不會自動套用到已存在的資料庫。

判斷用哪一種方式補齊：

- **新增欄位（可為 NULL 或有安全預設值）、且本機已有想保留的資料** → 手動對該資料庫跑一句
  `ALTER TABLE ... ADD ...`（用 `db/club-schema.sql` 裡真正的欄位型別宣告，不要自己猜），
  再視情況修改 `generate-club-seed-sql.py` 補一句可重複執行的 `UPDATE`（見下方為什麼不能只加
  `INSERT`），最後重跑 `./db/seed/apply-seed.sh` 讓既有資料被補值。**優點**：不中斷本機正在跑的
  `apps/api`／`apps/web`（不需要停資料庫），改動範圍精準對應這次的 schema 變更。
- **改動較大**（改欄位型別、砍欄位、加 `NOT NULL` 約束、或懷疑本機資料已經手動改壞）
  → 走上面「怎麼重來一次」的整庫重建流程（`DROP DATABASE` ＋ 重跑 `deploy/local-ddl.sh --apply`
  對應那個資料庫 ＋ `apply-seed.sh`）。**代價**：`local-ddl.sh --apply` 對已存在資料表的資料庫會
  直接失敗（`db/club-schema.sql` 的 `CREATE TABLE` 沒有 `IF NOT EXISTS` 防呆，見 `deploy/local-ddl.sh`
  檔頭），所以整庫重建一定要先 `DROP DATABASE`，會中斷任何正在連線的本機服務，需要的話重啟。

⚠️ **只加 `ALTER TABLE` 還不夠**：`generate-club-seed-sql.py` 的冪等寫法是「用業務自然鍵
`SELECT` 找列，找不到才 `INSERT`」（`IF @id IS NULL BEGIN ... END`）——這個區塊**只在列不存在時
執行**，既有列不會因為你在 `INSERT` 的欄位清單裡加了新欄位就被回填。`matches.match_no` 的解法是
在 `INSERT` 區塊之外**另外加一句獨立、可重複執行的 `UPDATE ... WHERE <業務自然鍵>`**（見
`generate-club-seed-sql.py` 第 8 節），涵蓋「列已存在但缺新欄位的值」這種情況；只改 `INSERT`
只對「全新建庫」有效，對「既有庫追加欄位」無效。

## 後台模組種子（2026-09-30）

> 使用者要求「後台打開就有資料可以看、可以測試」。定義與 SQL 產生在 [`backoffice_seed.py`](backoffice_seed.py)（每筆資料旁都註明來源），
> 冪等寫法沿用主腳本（業務自然鍵找不到才 `INSERT`、`block()` 自動包交易），**重跑 `apply-seed.sh` 不會重複插入**；
> 兩俱樂部資料以 `club_id` 區隔，共用主檔（標籤、FAQ 分類、事件類型）不帶 `club_id`。
> 🔴 **只有「列不存在才建」**：已存在的列不會被回填新欄位（同上方「DDL 改了…」一節的說明）；要讓既有列換成新值請手動改或重建庫。

### 種了什麼（筆數，tcrfc／bw）

| 模組（後台名稱） | 表 | tcrfc | bw | 資料來源 |
|---|---|---|---|---|
| 頁面（B1 **固定頁**，2026-10-07） | `pages`／`pages_i18n`／`page_blocks`／`page_versions`（v1 快照＋預覽權杖） | 12 頁（版型 `PageTemplates.cs` 全部建齊，已發布）／18 區塊 | 10 頁（藍鯨不設 06／11，已發布）／15 區塊 | **真實**：前台目前寫死的備用文案（`page_seed_content.json`，由 `page_seed_content.extract.ts` 自 `apps/web` 取出）；不再有測試草稿頁。既有資料庫用 `db/migrations/20261007_page-templates.sql`（`generate-page-template-migration.py` 產生）對齊 |
| 首頁輪播 | `banners`／`banners_i18n` | 2（draft） | 2（draft） | 第一張文字取自 `getHomeHero` 已核實文案（**真實**），第二張**測試**；圖片沒有，見下方「圖片」 |
| 常見問題 | `faqs`／`faqs_i18n`／`faq_category_links`／`faq_embed_slot_links` | 10 題（十個分類各一）／4 個嵌入點皆有 | 10 題／2 個嵌入點有（`trials`、`program_detail`；藍鯨不設學院招生，無贊助題） | tcrfc **全測試**；bw **真實**（`content/blue-whale/programs.md` §3 的 8 題＋`squad/youth-teams.md` U15 的 2 題，舊站原文） |
| 課程與梯次 | `programs`／`programs_i18n`／`sessions` | 6 課程（五種型別各一＋1 草稿）／7 梯次（開放／額滿／候補／已結束皆有） | 12 課程／12 梯次 | tcrfc **全測試**；bw **真實**（`programs.md` §1 社區足球學校、§2 運動 i 台灣十種課程、§4 教練講習） |
| 賽程賽果／積分榜 | `standings` | 6（2026-27） | 6（2023） | **全測試**（沒有真實積分來源）。賽程賽果早已有種子（tcrfc 21 場、bw 21 場），本輪未動 |
| 行事曆自建事件 | `calendar_custom_events`（＋i18n、`calendar_event_teams`、`calendar_event_exceptions`） | 5（含每週重複＋例外日、全天、不公開各一） | 3（1 筆**真實**過往活動＋2 測試；含每兩週重複） | bw 真實：2024 台中女子足球節（`programs.md` §5，日期 2024-07-13 經 `news-index.md` #4 互證）；其餘**測試** |
| 新聞 | `tags`／`tags_i18n`（全域）、`article_tags`、`value_tag_links`、`articles.is_featured`；bw 另新增 3 篇 `articles` | tag 5 個（全域）；既有 83 篇新聞掛 72 個標籤、16 個核心價值標籤；最新 2 篇設為精選 | 3 篇測試新聞（`bw-test-news-*`，只有標題與摘要） | 標籤名稱是功能性分類詞；**歸類是種子的編輯性判斷**（見下）；bw 新聞**全測試**（舊站 17 則都是外部媒體連結，不得轉載，見 `news-index.md`） |
| 搜尋與 AI 能見度 | `redirects` | 31（19 組來源路徑） | 40（20 組來源路徑，未編碼＋百分比編碼各一筆，藍鯨規劃書 §7 第 3 點） | tcrfc 取自 `舊官網URL盤點.csv`；bw 取自 `site-map.md`。**新站對應頁是種子依名稱推得的建議，客戶「決定」欄仍為空** |
| 〃 | `settings`：`seo.title_template`／`seo.default_description` | zh＋en | zh（bw 英文全名待確認，不種 en） | **真實**（`CLUB_IDENTITY` 已核實文案） |
| 〃 | `settings`：`geo.llms_positioning`／`key_pages`／`facts_summary`／`license`／`contact` | 五區塊 zh＋en | 五區塊 zh；en 只有 `key_pages`／`license`／`contact` | **真實**（已核實事實與公開社群連結）；tcrfc 聯絡 Email 為**測試** |
| 〃 | `settings`：`geo.crawler_agents`（五個預設代理皆允許）、`geo.crawler_extra_exclude_paths` | 有（`[]`） | 有（`[]`） | 規劃書 §7 `GEO-02` 條文範例 |
| 網站設定 | `settings`：`site.*`（補齊） | 電話、營業時間（中文）、梯隊敘述英文 | 電話、營業時間（中文）、成立日英文顯示、梯隊敘述英文 | 電話與營業時間**測試**；英文為已核實中文事實的直譯 |
| 合作夥伴 E1（E1a 新增，2026-09-30） | `partners`／`partners_i18n`（含 `content`） | 5（五種類型各一；1 筆合作期間已結束、驗證公開端點只列進行中） | 既有 26 筆真實（主腳本 §17），未動 | **全測試** |
| 贊助 E2 | `sponsors`／`sponsor_packages`／`sponsor_package_links`／`sponsor_activations` | 贊助商 3（三等級；1 筆已到提醒日、1 筆合約已結束）／方案 9（規劃書九種名稱，價格為測試）／活動 2 | — | **全測試**（方案名稱是規劃書 §3.9 9.4 的真實用詞，內容與價格為測試） |
| 提案與 Lead E3 | `proposals`／`enquiries`（`proposal_id`）／`enquiry_answers` | 提案 2（A/B 草稿，**沒有檔案**）／Lead 3（`@example.com`，含三種跟進狀態） | — | **全測試**（公司與姓名皆【測試】前綴） |
| 慈善 B5 | `charities`／`charity_programs`／`charity_program_partners`／`charity_program_sponsors`／`impact_records`／`impact_metrics`／`settings`（`charity.*`） | 團體 2／計畫 3（置頂進行中、已完成、草稿）／事蹟 2／統計 3（含 1 筆不公開金額）／導流設定（網址 `charity.example.com`，文案已點明協會） | —（藍鯨不設慈善單元） | **全測試** |
| 媒體專區 B6 | `press_resources`／`press_resources_i18n` | 3（三類各一，**draft＋佔位檔案鍵 `seed-placeholder/no-file`**，公開端點不會顯示） | — | **全測試** |
| 榮譽 C5 | `achievements` | 3（一線隊 D1，2024–2026） | — | **全測試**（里程碑既有真實種子，未動） |
| 試訓 P4（B1 新增，2026-09-30） | `trials`／`trials_i18n`／`registrations`（`trial_id`） | 場次 2（一線隊：1 場開放、1 場已結束）＋報名 3／藍鯨 U15 場次 1＋報名 1 | 場次 1＋報名 1（見左） | **全測試**（報名者【測試】前綴、電話全 0、`@example.com`；`enrolled_count` 與報名狀態一致） |
| 會員系統 K1–K3（B1 新增） | `members`／`memberships`／`member_cards`／`membership_payments`／`membership_plans`(+`_i18n`)／`jersey_issues` | 方案 2（單人、家庭）；會員 7 位涵蓋雙會籍、現場入會、未驗證、停用、疑似重複帳號、即將到期；會籍 8、卡 9、付款 4、球衣 4 | 方案 1；藍鯨會籍 2（皆已到期球季 2025） | **全測試**（會員編號 `M900001`–`M900007`、姓名【測試】、Email `@example.com`；即將到期的示範會籍到期日 2026-10-15 是固定日期，過了就變成已到期） |
| 特約店家與權益 K4 | `partner_stores`(+`_i18n`)／`membership_benefits`(+`_i18n`) | 店家 4（含 1 家草稿）＋兩隊共同 1；權益 6 條（四個分組） | 店家 1 | **全測試**（座標為台中市區近似值，非真實店家；沒有圖片） |
| 行事曆設定 L3（B1 新增） | `settings`：`calendar.*`／`member.no_*`；`calendar_team_settings`(+`_i18n`) | 預設檢視、範圍、隊別、試訓同步（關閉）；`D1` 顯示名稱與代表色 | 同左；`BW1` 顯示名稱與代表色、`BW-U12` 不公開（示範） | 顯示名稱與色碼是**測試值**（色碼取自既有隊徽色，非正式定案） |

### 🔴 測試值清單（正式資料上線前逐一替換）

文字欄位一律以「【測試】」前綴標明；不能加前綴的欄位用一看就不可能存在的值。**搜尋 `【測試】` 即可找出全部文字型測試值。**

| 表／設定鍵 | 欄位 | 測試值 | 兩俱樂部 |
|---|---|---|---|
| `settings` `site.contact_phone` | 值 | `04-0000-0000` | tcrfc、bw |
| `settings_i18n` `site.contact_hours` | zh | 【測試】平日 09:00–18:00 | tcrfc、bw |
| `settings_i18n` `geo.llms_contact` | zh／en 內的 Email | `contact@example.com`（附【測試】說明） | 只有 tcrfc（bw 用舊站公開的官方信箱，已核實） |
| `banners_i18n` | 第二張輪播的標題、副標、圖片說明；第一張的圖片說明；兩張的 `image_key` | 【測試】…；`image_key` = `seed-placeholder/no-image`（**不是真實物件**） | tcrfc、bw |
| `faqs`／`faqs_i18n`（slug `test-faq-01`～`10`） | 問題、答案 | 【測試】…常見問題範例？／測試用內容 | 只有 tcrfc |
| `programs`／`programs_i18n`／`sessions`（slug `test-*`） | 名稱、簡介、梯次的日期／名額／價格／時段 | 名稱含【測試】；**價格與名額是整數無法加前綴，一律視為測試** | 只有 tcrfc |
| `standings` | `team_name`、名次、出賽、積分 | 【測試】隊伍 A～F＋編造數字 | tcrfc、bw |
| `calendar_custom_events`（title 含【測試】者） | 標題、說明、日期時間 | 【測試】… | tcrfc 全部 5 筆；bw 2 筆（`2024 台中女子足球節` 那筆是真實） |
| `articles`（slug `bw-test-news-*`）＋`articles_i18n` | 標題、摘要 | 【測試】… | 只有 bw |
| `article_tags`／`value_tag_links` | tcrfc 既有新聞的標籤與核心價值歸類 | 種子的編輯性判斷（match→賽事；international→國際交流＋全球通道；community→社區＋社區；camps-events→青訓發展＋以球員為本） | tcrfc |
| `articles.is_featured` | 精選 | 依日期最新兩篇 | tcrfc |
| `redirects` | 新站對應頁 | 由舊網址名稱推得的建議，**客戶尚未決定** | tcrfc、bw |
| `partners`／`sponsors`／`sponsor_packages`／`sponsor_activations`／`proposals`／`enquiries`（Lead）／`charities`／`charity_programs`／`impact_*`／`press_resources`／`achievements`（slug 或名稱含 `test-`、`【測試】`） | 全部欄位（含整數價格與金額） | 【測試】…／`@example.com`／`charity.example.com`／`seed-placeholder/no-file` | 只有 tcrfc |

### C1 批新增（2026-09-30，區段 49–54）

| 模組 | 表 | tcrfc | bw | 資料來源 |
|---|---|---|---|---|
| 漫畫 F1 | `settings` `comic.about_*`／`comic_characters`／`comic_episodes`／`comic_pages` | 企劃說明＋3 角色＋3 集草稿 | 無（藍鯨不設漫畫） | **全測試** |
| 球迷會活動 F2 | `fan_events`(+`_i18n`)／`fan_event_registrations` | 4 場（含額滿、已結束）＋報名 | — | **全測試** |
| 站內商店 S1–S6 | `settings` `shop.*`（18）／`invoice_donation_codes`（`9990001`、`9990002`）／`collections`／`products`／`product_variants`／`orders`／`shipments`／`refund_requests` | 4 系列／6 商品／11 規格／9 訂單（涵蓋各狀態）／3 出貨／2 退款 | 含 1 筆藍鯨訂單（驗證俱樂部隔離） | **全測試**（`TEST-`／`SEED-` 前綴；`TEST-BALL` 庫存 0 用來驗證缺貨） |
| 抽獎 K5 | `member_draws`／`draw_rosters`／`draw_roster_versions`、`settings` `member.draw_notice_confirmed` | `TEST-DRAW-01`（已抽出，名單 2 人）、`TEST-DRAW-02`（草稿） | — | **全測試** |

C1 的測試值一律視為測試：所有 `【測試】`、`TEST-`、`SEED-` 前綴值，捐贈碼 `9990001`／`9990002`，以及整數價格、庫存、數量。權限：C1 新增 49 個權限碼與角色對應寫在 `generate-club-seed-sql.py`（`cost.*` 只給 sysadmin＋商務）。
`AdminC1MiscTests.種子基線_C1示範資料…` 會檢查上述資料是否仍在；失敗時重跑 `apply-seed.sh`。

### 刻意沒種的（與原因）

- **`seo.robots_custom_rules`**：會原樣寫進 `robots.txt`，沒有需求就不預設；**`tracking.*`（GA4／GTM／Meta Pixel／LINE）**：假的追蹤碼會讓前台載入無效腳本，沒有真實 ID 不種；**全站預設 OG 圖**：`clubs.og_image_key` 需要圖片上傳。三者都留空，等真實值。
- **藍鯨英文**（`seo.title_template` en、`geo.llms_positioning`／`facts_summary` en）：英文名已於 2026-10-05 定案（B-5：簡稱 `Taichung Blue Whale`、全名 `Taichung Blue Whale Women's Football Club`，見 `docs/14`），種子寫入時只用這兩種寫法；`site.founding_title`／`site.league_name` en 同。
- **`site.founding_date`（tcrfc）**：成立月日至今沒有核實來源，日期欄位無法用前綴標示為測試，不種假日期。tcrfc 的 `site.founding_date_display` 英文也刻意不種（`SiteFactsTests` 用它驗證「缺英文時回退中文」）。
- **課程教練連結（`program_staff`）、課程報名、詢問收件匣（`enquiries`）**：涉及個資或需要真實人員，不種。教練連結需要「課程與教練」的真實對應。（**會員／會籍與試訓報名已於 B1 補種虛構資料**，見上表：全部【測試】前綴與 `example.com`，不含任何真實個資。）
- **磐石學院球隊 U15／U14／U12（`teams`）**：性別與年齡帶的真實定義沒有來源（`gender` 是必填），不臆測；`site.squad_codes` 已有這三個代碼但 `teams` 表沒有對應列（既有落差，`apps/api/README.md`「S1-12d」節已記）。
- **商店、漫畫、球迷活動（`S`／`F` 模組）**：後台尚未完成（`STATUS.md` S2 以後）。**夥伴、贊助、提案與 Lead、慈善、媒體專區、榮譽已於 E1a（2026-09-30）補種**（見上表；圖片一律沒有，媒體資源與提案沒有真實檔案）。bw 夥伴早已有真實種子。
- **賽程賽果**：早已有種子，本輪未動。
- **`tcrfc` 新聞 `articles_i18n.body`／`summary`**：文稿仍是讀不到的 `.gdoc`，留白是刻意的（見上方「哪些事不能做」）。

### 圖片

種子只寫文字。`banners.image_key` 是 `NOT NULL`，而 repo 內沒有可公開上傳的素材與 Azurite 上傳腳本（`site/src/assets/img/`、`apps/web/public/assets/img/` 不納管，且含未成年學員照片，**不得**拿來用），所以輪播以 **draft** 狀態＋佔位鍵寫入：後台列表看得到、可編輯、可測試發布流程；公開端點只回 `published`，**前台不會出現壞圖**（首頁繼續用既有靜態素材頂替）。要看到真實輪播請在後台上傳圖片後改為發布。其他 `*_key` 欄位（新聞封面、球員照片、課程封面、頁面 OG 圖）一律留空。

### ⚠️ 前台顯示的已知落差（種子欄位無法解決，回報給前台）

- **梯次「時段」欄**：`sessions.weekly_schedule` 依 API 規則必須是合法 JSON（後台提示範例 `{ mon: 18:00-19:30 }`），而前台 `programs/*.vue` 把它當字串**原樣印出**（沒有解析），所以畫面會出現 `{"mon":"18:00-19:30"}` 這樣的文字。要好看需要前台解析，或後端／規格另訂欄位形狀。
- **藍鯨青年隊名單頁未列入 AI 爬蟲強制排除**：`GeoCrawlerDefaults` 的 `/zh/academy/teams/` 只對 tcrfc 生效，`AdminGeoCrawlerTests` 還明文斷言 bw 不含該路徑；bw 站同一路由是 U15／U12 青年隊，藍鯨規劃書 BW-7 要求排除。種子**不能**繞過（會讓該測試失敗），需要後端補 `ClubLocalizedSegments["bw"]` 並同步改測試。

### 灌庫步驟（使用者執行）

```bash
set -a && source .env && set +a          # 需要 MSSQL_DEV_SA_PASSWORD
./db/seed/apply-seed.sh                   # 灌 tcrfc_club（冪等，可重複執行；或 ./db/seed/setup-club-db.sh 一鍵 DDL＋種子）
# 灌完後跑一次 apps/api 全套測試，確認三處調整過的測試通過（見 docs/18-work-errors.md E-81）
```

> 既有本機庫已有資料時，`apply-seed.sh` 只會**補上缺的列**，不會改動已存在的列——本輪新增的資料都是全新列，所以直接生效；
> 但 `site.contact_phone`／`site.contact_hours` 若你手動改過就不會被覆蓋。

## 已知落差（詳細對照見本次回報／`docs/12d-field-audit.md`）

- `news.json` 的 `category` 值 `intcup`（台中磐石國際足球盃）沒有任何一個 7.1–7.8 分類代碼字面對得上，
  本次逐篇標題人工確認內容皆為賽事報導，歸類到 `match`（7.2）——這是本次 seed 的人工判斷，正式資料應由後台複核。
- `coaches-academy.json` 的青訓教練／青訓總監無法判斷對應 `U15`／`U14`／`U12` 哪一隊（來源 JSON 沒有
  這個維度），`staff_teams` 刻意不連結；本次也沒有建立這三支學院球隊（無球員名單佐證需要建隊）。
- 圖片一律沒有 `*_key`：`players.photo`／`staff.has_photo`／`news.cover` 都只是 mockup 靜態資源的存在旗標
  或相對路徑，不是走過「上傳即縮圖」pipeline（docs/14）後的 Blob object key，混用會誤導未來的開發者。
- **`staff.json` 職稱「顧問」（陳曉明）歸類已拍板，但尚未真正填值**：規劃書 C3（後台 `Staff` 模組）只定義
  四個分組——管理層／行政／醫療／後勤，`staff.json` 的「顧問」不屬於任何一個。**2026-09-22 使用者拍板：
  歸入「管理層」**——這是執行層決定，不是規劃書明文，也**沒有跑同步鏈改規劃書**（使用者選的是不跑同步鏈
  那一案）。目前 `generate-club-seed-sql.py` 第 7 節的 `staff` 建表邏輯還沒有寫入 `staff_group` 欄位
  （`INSERT INTO staff (id, club_id)` 沒有這一欄），這個決定**要等 C3 模組實際開工、seed 腳本補上
  `staff_group` 賦值時才會真正套用**，本次只記錄決定，不代表已經填值，也不要因此去改這支腳本或重灌資料庫。
  同一筆記錄另見 [`../docs/12d-field-audit.md`](../docs/12d-field-audit.md) §9。

## D 批種子（2026-09-30，`backoffice_seed.py` §55–59，全部【測試】虛構）

G3 電子報名單 5 筆（`example.com`）、M2 深連結 9 條與首頁九個固定區塊／快捷入口／「更多」分頁／公告條、M5 功能開關 4 個（`payment_mode=external`）與憑證列管 3 筆、M1 版本 3 筆（含各平台最低支援版本 0.9.0）、
E4–E6 版位 2／廣告主 2／檔期 2（含 1 個已結束檔期與 14 天日聚合示範）、M3／M4／M5 示範裝置 5 台（**沒有推播權杖**）、推播 2 則、診斷回報 3 筆。權限碼 38 個與角色指派在 `generate-club-seed-sql.py`。
**沒有圖片**（廣告素材與備援 `image_key` 為 NULL）。🔴 **驗證一律跑 `./db/seed/apply-seed.sh` 本身並確認輸出沒有 `Msg`**（`docs/18` `E-94`：超長單行 SQL 會被 `sqlcmd` 從標準輸入讀取時切斷）。
測試對這批資料的約定見 `apps/api/README.md` D 批「測試」：測試資料用 `ZZTEST`／`test-dev-`／`zz-test-` 前綴，會動共用設定的測試拍照還原。

## 2026-10-06 種子調整（靜態頁草稿、藍鯨核心價值關閉、聯絡與社群）

- **靜態頁種子一律 `draft`**（磐石 `about/vision-mission`、`about/philosophy`；藍鯨 `about/our-story`、`about/vision`、`about/philosophy`）：前台已改為「CMS 有已發布內容就顯示 CMS」，種子文案比前台寫死版精簡，發布會讓頁面變差；內容補齊後由後台發布。`text` 區塊 body 只放純文字（空行分段），**不得含 HTML**；需要標題／清單時用 `steps` 等區塊（見 `apps/api/Features/AdminPages/PageBlockTypes.cs`）。
- **藍鯨首頁 `core_values` 預設 `is_enabled = 0`**（磐石不變）。
- **聯絡與社群 setting**（`CONTACT_SEEDS`，`generate-club-seed-sql.py`）：`site.social_facebook／instagram／youtube`（藍鯨另有 `site.social_line`、`site.contact_email`）寫 `settings.setting_value`；`site.footer_blurb` 逐語系寫 `settings_i18n`（en 取自 `apps/web/shared/utils/club-copy-en-core.ts`）。部門窗口不種。
- `clubs` 不再有品牌欄位（標誌、Favicon、品牌色）；遷移 `db/migrations/20261006_club-brand-drop_2-contract.sql`。
