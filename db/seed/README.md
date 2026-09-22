# db/seed/ — 本機開發用種子資料（S0-6c／S0-6d）

> 對應 [`../../docs/17-deployment.md`](../../docs/17-deployment.md)（本機開發環境）與
> [`../../deploy/README.md`](../../deploy/README.md)（S0-7a 本機骨架、本機資料庫容器）。
> 本目錄同時處理**兩個獨立資料庫**的種子資料，但用**兩支互不相干的腳本**：
> 主站庫（`tcrfc_club_dev`，`apply-seed.sh`）與慈善庫（`tcrfc_charity_dev`，`apply-charity-seed.sh`）。
> **這兩支腳本刻意不共用、不互相呼叫**——慈善捐款平台是獨立法人邊界（主辦與收款主體是
> 台灣足球策略發展協會，不是台中磐石足球俱樂部），見下方「慈善庫的種子資料」一節。
>
> 🔴 **2026-09-21 起：本機開發資料庫已合併進既有的 `sqlserver` 容器**（不再是本專案自己起的
> `mssql-dev` 服務），詳見 [`../../deploy/README.md`](../../deploy/README.md)「本機開發資料庫已
> 合併進既有的 `sqlserver` 容器」一節。本檔下方的指令已同步更新為新的容器與連線方式。

## 這個目錄有什麼

| 檔案 | 用途 |
|---|---|
| [`generate-club-seed-sql.py`](generate-club-seed-sql.py) | 讀 [`site/src/data/*.json`](../../site/src/data/)（六個 mockup 資料檔），產生 `tcrfc_club_dev` 冪等的 T-SQL |
| [`apply-seed.sh`](apply-seed.sh) | 呼叫上面那支腳本，再用 `sqlcmd` 把產生的 SQL 灌進本機 SQL Server（既有 `sqlserver` 容器）的 `tcrfc_club_dev`；容器名稱可用 `LOCAL_MSSQL_CONTAINER` 環境變數覆寫，但目標資料庫寫死只認 `tcrfc_club_dev` |
| [`generate-charity-seed-sql.py`](generate-charity-seed-sql.py) | 產生 `tcrfc_charity_dev` 冪等的 T-SQL。**資料直接寫在腳本內**（不像 club 腳本讀外部 JSON）——因為這批資料**從一開始就是虛構測試資料**，不是需要另外隔離的真人個資，見下方一節的說明 |
| [`apply-charity-seed.sh`](apply-charity-seed.sh) | 呼叫上面那支腳本，灌進 `tcrfc_charity_dev`；目標資料庫寫死只認 `tcrfc_charity_dev`，與 `apply-seed.sh` 的白名單分開維護 |
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

# 2. 灌 DDL（兩個庫都會建：tcrfc_club_dev、tcrfc_charity_dev，建在既有 sqlserver 容器裡）
./deploy/local-ddl.sh --apply

# 3a. 灌主站庫種子資料（只灌 tcrfc_club_dev）
./db/seed/apply-seed.sh

# 3b. 灌慈善庫種子資料（只灌 tcrfc_charity_dev；兩支腳本互不相依，順序不影響結果）
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
CID=$(docker ps -q --filter "name=^/sqlserver\$")

# 只丟 tcrfc_club_dev，不要動 tcrfc_charity_dev（慈善庫沒有種子來源，留著也沒用但不該順手清掉）
# ⛔ 只准對這兩個名字動這種 DROP／CREATE，絕不對這個 instance 上其他資料庫做同樣的事
#    （這個容器裡還有使用者另一個專案的約 25 個既有資料庫）。
docker exec -i "$CID" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_DEV_SA_PASSWORD" -C \
  -Q "IF DB_ID('tcrfc_club_dev') IS NOT NULL DROP DATABASE tcrfc_club_dev; CREATE DATABASE [tcrfc_club_dev];"

docker exec -i "$CID" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_DEV_SA_PASSWORD" -C \
  -d tcrfc_club_dev < deploy/dev/.local-ddl-output/club-schema.local.sql

./db/seed/apply-seed.sh
```

⚠️ **2026-09-21 之前**「完全砍掉重練」的做法是 `docker compose down -v` 連 volume 一起刪、
容器重建。**現在不能這樣做**——`sqlserver` 是既有容器，不是本專案 compose 管理的，`down -v`
管不到它，而且那樣做等於刪除使用者另一個專案的全部資料庫（見
[`../../deploy/README.md`](../../deploy/README.md)「⛔ 這個既有容器沒有掛 volume」）。
本機重來的正確做法就是上面「只丟 `tcrfc_club_dev`」那段，只對這兩個資料庫本身動手，
不對容器動手。

## 連線字串長什麼樣

跟 [`deploy/dev/club.env.example`](../../deploy/dev/club.env.example) 一致：

```
Server=host.docker.internal,1433;Database=tcrfc_club_dev;User Id=sa;Password=<MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;
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

## ⛔ 哪些事不能做

- **不得對 `tcrfc_charity_dev` 執行任何跨庫 JOIN**（例如 `SELECT ... FROM tcrfc_charity_dev.dbo.x JOIN tcrfc_club_dev.dbo.y`）。
  本機兩庫在同一個 instance 裡，這種寫法**本機測得過、正式環境的兩個獨立 Azure SQL 一定爆**，
  而且違反的是法遵邊界不只是相容性——見 [`../../deploy/README.md`](../../deploy/README.md) 與
  [`../../docs/14-invariants.md`](../../docs/14-invariants.md)。
- **不得對這個既有 `sqlserver` 容器本身動手**：不得重建、不得改埠繫結、不得加 volume、
  不得改任何設定、不得 `docker rm`／`docker restart`。它是使用者另一個專案在用，裡面還有
  約 25 個既有資料庫，且**沒有掛任何 volume**（重建＝資料全滅，見
  [`../../deploy/README.md`](../../deploy/README.md)）。本目錄的腳本只被授權在裡面操作
  `tcrfc_club_dev`（`db/seed/apply-seed.sh`）與 `tcrfc_club_dev`／`tcrfc_charity_dev`
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

## 慈善庫（`tcrfc_charity_dev`）的種子資料

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
- **不得對 `tcrfc_club_dev` 執行任何跨庫 JOIN**，也不得把兩個資料庫的種子腳本合併——理由同上一節。

**種了什麼**（29 張表：25 主表 ＋ 4 張 `*_i18n`；`ui_strings`／`ui_string_translations` 刻意留空
——本次任務範圍未列，也還沒有實際介面字串內容可種）：

| 分類 | 表 | 筆數 | 涵蓋的狀態／分支 |
|---|---|---|---|
| 語系 | `locales` | 2 | zh-Hant／en |
| 後台帳號權限 | `admin_roles`／`permissions`／`role_permissions`／`admin_users`／`admin_user_roles` | 9／23／43／4／4 | 沿用主站九個角色；5 個與慈善無對應職能的角色刻意不掛權限（見腳本註解） |
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
但本機的資料庫（既有 `sqlserver` 容器裡的 `tcrfc_club_dev`／`tcrfc_charity_dev`）是「建好一次、
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
