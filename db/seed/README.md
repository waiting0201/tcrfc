# db/seed/ — 本機開發用種子資料（S0-6c）

> 對應 [`../../docs/17-deployment.md`](../../docs/17-deployment.md)（本機開發環境）與
> [`../../deploy/README.md`](../../deploy/README.md)（S0-7a 本機骨架、`mssql-dev` 容器）。
> 這裡只處理**主站庫（`tcrfc_club_dev`）**的種子資料。**慈善庫（`tcrfc_charity_dev`）沒有種子來源，本目錄不處理它。**

## 這個目錄有什麼

| 檔案 | 用途 |
|---|---|
| [`generate-club-seed-sql.py`](generate-club-seed-sql.py) | 讀 [`site/src/data/*.json`](../../site/src/data/)（六個 mockup 資料檔），產生冪等的 T-SQL |
| [`apply-seed.sh`](apply-seed.sh) | 呼叫上面那支腳本，再用 `sqlcmd` 把產生的 SQL 灌進 `mssql-dev` 容器的 `tcrfc_club_dev` |
| `.generated/`（**不進版控**） | 產生出來的 `.sql`，含球員／教練真實姓名，隨時可重新產生，見 [`.gitignore`](../../.gitignore) |

## 為什麼種子資料用「讀 JSON 產生 SQL」而不是寫死在腳本裡

`site/src/data/*.json`（`players.json`／`staff.json`／`coaches-d1.json`／`coaches-academy.json`）
含球員與教練的真實姓名，屬個資性質欄位（CLAUDE.md 第 7 條）。這些 JSON 本身已經是 mockup 的一部分、
已經在版控裡（且公開站台本來就會顯示球員名單，不是新的揭露），所以**灌資料本身不是新增風險**；
但 `generate-club-seed-sql.py` 這支「邏輯腳本」刻意不重複硬編碼任何姓名——它只寫「JSON 欄位對到哪張表
哪個欄位」的規則，**真正含個資的內容只活在 JSON 來源與執行期產生的 `.generated/*.sql`（不進版控）**。
這樣即使未來球員異動，也只需要改 JSON，不必動這支腳本。

## 怎麼從零開始（本機第一次建置）

```bash
# 0. 確認 .env 有 MSSQL_DEV_SA_PASSWORD（.env.example 已有預設值可直接用）
set -a && source .env && set +a

# 1. 啟動本機資料庫容器（只需要 mssql-dev，其他服務要等 apps/* 建好才 build 得起來）
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d mssql-dev

# 2. 等 healthy（Apple Silicon 上是 amd64 模擬，冷啟動可能超過 30 秒）
docker compose -f docker-compose.yml -f docker-compose.dev.yml ps mssql-dev

# 3. 灌 DDL（兩個庫都會建：tcrfc_club_dev、tcrfc_charity_dev）
./deploy/local-ddl.sh --apply

# 4. 灌種子資料（只灌 tcrfc_club_dev）
./db/seed/apply-seed.sh
```

## 怎麼重來一次（清掉重灌）

種子腳本本身是冪等的（可重複執行、不會長出重複資料），**正常情況下不需要重來**。
真的要從乾淨狀態重建（例如懷疑資料被手動改壞）：

```bash
set -a && source .env && set +a
CID=$(docker compose -f docker-compose.yml -f docker-compose.dev.yml ps -q mssql-dev)

# 只丟 tcrfc_club_dev，不要動 tcrfc_charity_dev（慈善庫沒有種子來源，留著也沒用但不該順手清掉）
docker exec -i "$CID" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_DEV_SA_PASSWORD" -C \
  -Q "IF DB_ID('tcrfc_club_dev') IS NOT NULL DROP DATABASE tcrfc_club_dev; CREATE DATABASE [tcrfc_club_dev];"

docker exec -i "$CID" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_DEV_SA_PASSWORD" -C \
  -d tcrfc_club_dev < deploy/dev/.local-ddl-output/club-schema.local.sql

./db/seed/apply-seed.sh
```

要完全砍掉重練（含 volume）：`docker compose -f docker-compose.yml -f docker-compose.dev.yml down -v`
會連 `mssql_dev_data` 具名 volume 一起刪，下次 `up -d` 是全新容器，上面四步從頭做一次。

## 連線字串長什麼樣

跟 [`deploy/dev/club.env.example`](../../deploy/dev/club.env.example) 一致：

```
Server=mssql-dev,1433;Database=tcrfc_club_dev;User Id=sa;Password=<MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;
```

**從宿主機（不是容器內）連線**：`Server=127.0.0.1,14330;...`（本機開發用埠，見
[`docker-compose.dev.yml`](../../docker-compose.dev.yml)，只綁 `127.0.0.1`，不對區網開放）。

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
  本機兩庫在同一個 `mssql-dev` instance 裡，這種寫法**本機測得過、正式環境的兩個獨立 Azure SQL 一定爆**，
  而且違反的是法遵邊界不只是相容性——見 [`../../deploy/README.md`](../../deploy/README.md) 與
  [`../../docs/14-invariants.md`](../../docs/14-invariants.md)。
- **不得直接對 `tcrfc-mssql-dev-1`（或任何 `mssql-dev` 相關容器）以外的容器下手**。
  機器上另外還有一個名為 `sqlserver` 的既有容器，是別的專案在用，**不要停它、不要改它、不要在裡面建庫**。
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

## DDL 改了、既有本機庫沒跟上——以後一定會再發生

`db/club-schema.sql`／`db/charity-schema.sql` 是持續在改的交付物（真實來源是 `docs/12`／`docs/16`），
但本機的 `mssql-dev` 容器是「建好一次、之後重複重跑種子腳本」在用，**兩者不會自動保持同步**。
2026-09-21（`matches.match_no` 補進規格與 DDL）就踩過一次：本機庫是在這個欄位補進 DDL 之前建的，
`ALTER TABLE` 之類的 schema 變更不會自動套用到已存在的資料庫。

判斷用哪一種方式補齊：

- **新增欄位（可為 NULL 或有安全預設值）、且本機已有想保留的資料** → 手動對 `mssql-dev` 容器跑一句
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
