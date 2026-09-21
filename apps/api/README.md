# apps/api — api（單一 .NET 行程，唯讀讀取 API）

**S0-7b（2026-09-21，`backend-engineer`）**：讓 Nuxt 前台（[`apps/web`](../web/README.md)）能打真實資料庫，
建立球員、教練與職員、新聞、賽程與賽果、俱樂部主檔五組**唯讀** GET 端點。

**S0-7d（2026-09-21，`backend-engineer`）**：補齊 S0-7b 刻意縮減的三個缺口——① `/readyz` 加上慈善庫與
Redis 檢查 ② `Caching/IQueryCache.cs` 接縫接上真正的 Redis 實作 ③ 建立 `Tcrfc.Api.Tests` 自動化測試專案
（S0-7b 為止零測試）。細節見下方各段落與「S0-7d 驗收紀錄」。

🔴 **不含**：任何寫入、登入與權限、商店與金流、後台任何模組、慈善捐款平台的業務功能（S0-7d 對慈善庫
只做「開一條連線探活」，不做任何查詢，見下方「`/readyz` 範圍」）。這些留給後續任務，屆時會用到本檔
還沒接的 EF Core（寫入）與完整權限模型（[`docs/12b`](../../docs/12b-database-tables.md) §7）。

🔴 **2026-09-21（`deployment-engineer`）：本機開發資料庫合併進既有的 `sqlserver` 容器**，
`docker-compose.dev.yml` 的 `mssql-dev` 服務已退場（見 [`deploy/README.md`](../../deploy/README.md)）。
**下方所有連線範例已改為新位址**（容器內連 `host.docker.internal,1433`、宿主機直連
`127.0.0.1,1433`）；文中提到「本機 `mssql-dev`」的地方是**當時（S0-7b／S0-7d）的驗證紀錄**，
如實保留不回頭改寫歷史，但那個服務現在已不存在，照著跑之前請先看這一段與 `deploy/README.md`。

---

## 目錄結構

```
apps/api/
├── Program.cs                     # DI 註冊、middleware 管線、路由掛載
├── Tcrfc.Api.csproj                # net10.0，Dapper + Microsoft.Data.SqlClient + AspNetCore.OpenApi
├── Data/
│   ├── IClubSqlConnectionFactory.cs / ClubSqlConnectionFactory.cs   # 唯一的主站庫連線來源
│   └── ClubOrSharedSql.cs         # 「俱樂部專屬優先、回退共同」SQL 片段的唯一真實來源
├── Security/
│   ├── ClubScope.cs               # 已驗證的俱樂部範圍，建構子 internal，繞不過去
│   ├── IClubResolver.cs / ClubResolver.cs   # 唯一能建立 ClubScope 的地方
│   └── ClubNotFoundException.cs
├── Localization/
│   └── RequestLocale.cs           # zh/en ↔ zh-Hant/en 轉換與回退規則
├── Caching/
│   ├── IQueryCache.cs             # 快取接縫（entity/club/locale/qualifier 四維度 key＋InvalidateAsync）
│   ├── NoOpQueryCache.cs          # REDIS_HOST 未設定時的實作
│   └── RedisQueryCache.cs         # REDIS_HOST 有設定時的實作（S0-7d，fail-open／版本號失效／TTL／single-flight）
├── Common/
│   ├── PagedResult.cs / PagingQuery.cs
│   ├── ApiExceptionHandler.cs     # 統一例外處理，不外流資料庫例外訊息
│   └── HealthEndpoints.cs         # /healthz、/readyz（S0-7d 起含慈善庫與 Redis 檢查）
└── Features/
    ├── Clubs/      (ClubDto, ClubsRepository, ClubsEndpoints)
    ├── Players/    (PlayerDto, PlayersRepository, PlayersEndpoints)
    ├── Staff/      (StaffDto, StaffRepository, StaffEndpoints)
    ├── News/       (ArticleListItemDto/ArticleDetailDto, ArticlesRepository, ArticlesEndpoints)
    └── Schedule/   (MatchDto, MatchesRepository, MatchesEndpoints)

apps/api/Tcrfc.Api.Tests/    # S0-7d：自動化測試專案（獨立 .csproj，不進 Docker 映像檔，見下方「測試」）
```

每個 Features 子目錄都是「DTO＋repository＋endpoints」三件套，彼此不互相依賴（除了都經過 `IClubResolver`）。
`Tcrfc.Api.Tests` 是同目錄下的獨立子專案（沒有 `.sln`），`Tcrfc.Api.csproj` 已明確 `<Compile Remove>` 排除它，
避免遞迴萬用字元把測試原始碼一起編譯進主專案（測試專案參照的 xunit／`Microsoft.AspNetCore.Mvc.Testing`
主專案完全不需要）。

---

## 怎麼跑（本機開發）

### 前置

1. 本機既有的 `sqlserver` 容器已啟動（不是本專案 compose 管理的，見
   [`deploy/README.md`](../../deploy/README.md)），DDL 與種子資料已灌入（見
   [`db/seed/README.md`](../../db/seed/README.md)）：
   ```bash
   docker ps --filter name=sqlserver   # 確認既有容器在跑（不是本專案啟動它）
   ./deploy/local-ddl.sh --apply
   ./db/seed/apply-seed.sh
   ```
2. 確認 `.env` 有 `MSSQL_DEV_SA_PASSWORD`，且**值與該既有 `sqlserver` 容器的 SA 密碼一致**
   （這是既有容器，密碼不是本專案決定的）。

### 直接用 dotnet 跑（不經 Docker，最快的開發迴圈）

```bash
cd apps/api
dotnet build

# 連線字串等機密一律用環境變數，⛔ 絕對不要寫進任何檔案並 commit。
# 🔴 用 shell 變數存這種帶分號的連線字串時務必加引號——不加引號被當成 shell 腳本
#    source 執行時，分號會被解讀成命令分隔符，字串會在第一個分號處被悄悄截斷
#    （見 docs/18-work-errors.md，本次任務期間在本機驗證時才發現，非常隱蔽因為
#    程式仍會啟動、仍會嘗試連線，只是連線字串缺了 Database／帳密／TrustServerCertificate）。
export ASPNETCORE_ENVIRONMENT=Development
export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club_dev;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;"
export CORS_ALLOWED_ORIGINS="http://localhost:3000,http://localhost:3001"
export ASPNETCORE_URLS="http://127.0.0.1:5299"

dotnet run --no-launch-profile
```

驗證：
```bash
curl http://127.0.0.1:5299/healthz     # {"status":"ok"}
curl http://127.0.0.1:5299/readyz
# {"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"not_configured"}
# （club_db／charity_db 都是實際開一條連線查 SELECT 1；charity_db／redis 沒設定對應環境變數時
#  回 not_configured，不影響 ready，見下方「/readyz 範圍」）
curl http://127.0.0.1:5299/api/v1/clubs
```

Swagger／OpenAPI 只在 `ASPNETCORE_ENVIRONMENT=Development` 開放：`http://127.0.0.1:5299/openapi/v1.json`
（沒有掛 Swagger UI 頁面，本次只用建置期就有的 `Microsoft.AspNetCore.OpenApi`，之後要加互動式 UI 可疊 Scalar／Swashbuckle）。
**正式環境（`ASPNETCORE_ENVIRONMENT=Production`）此端點回 404，已用容器實測驗證**（見下方「驗收紀錄」）。

### 用容器跑（貼近正式環境的驗證）

```bash
cd apps/api
docker build -t tcrfc-api-local .

docker run -d --name tcrfc-api-local \
  --add-host=host.docker.internal:host-gateway \
  -e CLUB_SQL_CONNECTION_STRING="Server=host.docker.internal,1433;Database=tcrfc_club_dev;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e CORS_ALLOWED_ORIGINS="http://localhost:3000" \
  -p 18080:8080 \
  tcrfc-api-local

curl http://127.0.0.1:18080/healthz
docker inspect --format='{{.State.Health.Status}}' tcrfc-api-local   # 等 healthcheck 跑完應為 healthy
```

`host.docker.internal` 是容器連回宿主機（`sqlserver` 容器對外發布的 `1433` port）的方式；
`--add-host=host.docker.internal:host-gateway` 在 macOS 的 Docker Desktop 上其實不必要
（已內建），但補上這行是為了 Linux 相容（Docker 20.10+ 才有 `host-gateway` 這個特殊值），
兩邊都能用。真正的 `docker-compose.yml` 疊 `docker-compose.dev.yml` 跑起來時，`api` 服務已經
在 `docker-compose.dev.yml` 加了同樣的 `extra_hosts`（見 `deploy/dev/club.env`
的 `CLUB_SQL_CONNECTION_STRING` 也是 `host.docker.internal,1433`），不需要在指令列額外帶這個參數。

⚠️ **2026-09-21 之前**這裡連的是本專案自己起的 `mssql-dev` 服務、對外埠 `14330`——那個服務
已退場，現在的既有 `sqlserver` 容器用的是標準 `1433` port，且不是本專案 compose 管理的服務，
容器間無法用服務名稱互連，一律要透過 `host.docker.internal` 這條路徑（即使是
`docker-compose.yml` 疊 `docker-compose.dev.yml` 跑也一樣，因為 `sqlserver` 不在同一個
compose 網路裡）。

正式部署（VM 上、走 `docker-compose.yml`）的環境變數來源見 [`docs/17-deployment.md`](../../docs/17-deployment.md) §1
與 [`docs/20-cicd.md`](../../docs/20-cicd.md) §7.2——本檔不重複那份清單，只列本次新增／確認用得到的鍵名（見下）。

---

## 環境變數

| 變數 | 必填 | 說明 |
|---|---|---|
| `CLUB_SQL_CONNECTION_STRING` | ✅ | 主站庫連線字串。本機見 [`deploy/dev/club.env`](../../deploy/dev/club.env)（不進版控），正式環境見 VM 上 `/opt/tcrfc/secrets/club.env`（`docs/20-cicd.md` §7.2）。**鍵名故意跟兩邊祕密檔一致**，不繞去 `ConnectionStrings:Club` 這種 ASP.NET Core 慣用間接層——本檔的 `ClubSqlConnectionFactory` 直接讀這個鍵。⛔ **不得寫死在任何檔案裡** |
| `CHARITY_SQL_CONNECTION_STRING` | 選填（S0-7d） | 慈善庫連線字串，**只用在 `/readyz` 開一條連線查 `SELECT 1`**，本檔沒有任何 repository 或查詢邏輯碰這個庫。沒設定時 `/readyz` 的 `charity_db` 回 `not_configured`（不影響 ready）；設定了卻連不上回 `fail`（連帶 not ready）。見 [`deploy/dev/charity.env.example`](../../deploy/dev/charity.env.example) |
| `REDIS_HOST` | 選填（S0-7d） | 有設定才會注入 `RedisQueryCache`，沒設定注入 `NoOpQueryCache`。`docker-compose.yml`／`docker-compose.dev.yml` 的 `api` 服務**一律有帶**（值為 `redis`），只有本機直接 `dotnet run`（不經 Docker）不帶時才會落到 no-op |
| `REDIS_PORT` | 選填（S0-7d） | 預設 `6379`。本次新增這個變數主要是為了讓測試能指到一個確定沒人聽的本機連接埠（見 `Tcrfc.Api.Tests`），正式環境不需要設定 |
| `REDIS_PASSWORD` | `REDIS_HOST` 有設定時對應生效 | 與 `docker-compose.yml`／`deploy/dev/club.env` 的既有鍵名一致 |
| `QUERY_CACHE_TTL_SECONDS` | 選填（S0-7d） | 每個快取 key 的 TTL 秒數，預設 `300`（5 分鐘）。預設值的理由與可否調整見下方「快取接縫」段落 |
| `CORS_ALLOWED_ORIGINS` | 正式環境必填，本機可省略 | 逗號分隔的允許來源清單，來自 `docker-compose.yml` 的 `api` 服務定義（`docs/17-deployment.md` §10.2 的既有缺口，本次由前一任務補上）。本機開發若沒帶，`Development` 環境會退回 `localhost:3000/3001/3002` 三個 `apps/web` 常用埠；**正式環境沒有這個退回值**——沒設定就是沒有任何來源被允許，比「忘記設定就開放全部」安全 |
| `ASPNETCORE_ENVIRONMENT` | 建議設 | `Development` 才會開 OpenAPI 端點，其餘值一律關閉 |
| `ASPNETCORE_URLS` | 本機開發用 | 監聽位址，容器內固定用 `Dockerfile` 的 `ASPNETCORE_HTTP_PORTS=8080` |

⛔ **S0-7d 之後仍完全不碰 LINE Pay、JWT、Blob**——這些鍵名雖然已經在 `docker-compose.yml` 的
`api` 服務與 `deploy/dev/{club,charity}.env` 裡預留，但本檔的程式碼**沒有讀取它們**，留給接下來實作那些功能的
session 使用。

---

## 端點清單

所有端點前綴 `/api/v1`；除 `/api/v1/clubs` 系列外，其餘一律要求路由帶 `{club}`（俱樂部代碼，如 `tcrfc`、`bw`）。

| 方法與路徑 | 說明 | 查詢參數 |
|---|---|---|
| `GET /api/v1/clubs` | 俱樂部清單 | `lang` |
| `GET /api/v1/clubs/{club}` | 單一俱樂部主檔 | `lang` |
| `GET /api/v1/{club}/players` | 球員名單 | `team`（球隊代碼）、`lang`、`page`、`pageSize`（預設 50，上限 200） |
| `GET /api/v1/{club}/staff` | 教練與職員 | `team`、`lang`、`page`、`pageSize`（預設 50，上限 200） |
| `GET /api/v1/{club}/news` | 新聞列表 | `category`（分類代碼）、`lang`、`page`、`pageSize`（預設 20，上限 100） |
| `GET /api/v1/{club}/news/{slug}` | 新聞單篇 | `lang` |
| `GET /api/v1/{club}/schedule` | 賽程與賽果 | `team`、`season`（賽季代碼）、`status`、`lang`、`page`、`pageSize`（預設 20，上限 100） |
| `GET /healthz` | 存活探針 | — |
| `GET /readyz` | 就緒探針（真的開連線查主站庫，見下方「/readyz 範圍」） | — |

`lang` 值域 `zh`／`en`（不帶預設 `zh`），與 [`docs/06-conventions.md`](../../docs/06-conventions.md)「語系代碼」一致，
不是資料庫實際存的 `zh-Hant`／`en`（轉換邏輯見 `Localization/RequestLocale.cs`）。

分頁回應統一外殼：
```json
{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 83, "totalPages": 5 }
```
`page`／`pageSize` 一律正規化（`page<=0` → `1`；`pageSize<=0` → 該端點預設值；超過上限 → 截到上限），
不會讓呼叫端用超大 `pageSize` 一次撈整張表。

### `/readyz` 範圍（S0-7d 已補齊）

`apps/api/Dockerfile` 原註解寫「真的檢查兩個 DbContext 能連線」。S0-7b 當時刻意縮減範圍只驗證主站庫，
S0-7d 補上慈善庫與 Redis，三者的失敗語意各不相同：

| 檢查項 | 沒設定對應變數 | 設定了但連不上 | 對 `status` 的影響 |
|---|---|---|---|
| `club_db`（`CLUB_SQL_CONNECTION_STRING`） | 不適用（必填） | `fail` | 連不上 → `not_ready`（503）——既有行為，未改 |
| `charity_db`（`CHARITY_SQL_CONNECTION_STRING`） | `not_configured` | `fail` | 沒設定不影響；連不上 → `not_ready`（503） |
| `redis`（`REDIS_HOST`） | `not_configured` | `degraded` | **兩種情況都不影響 `status`**，永遠不會因為 Redis 而 not ready |

`charity_db` 的「沒設定＝not_configured、不影響 ready」是刻意的：慈善平台是還沒開工的獨立交付物
（獨立後台、獨立資料庫，`docs/17-deployment.md` §5），大多數環境（本機、CI、正式站上線初期）本來就
不會設定這個變數，不該為一個沒人用的連線讓整個 `api` 容器被判定 unhealthy。`redis` 的「連不上也不影響
ready」直接對應 [`docs/17-deployment.md`](../../docs/17-deployment.md) §4「連線失敗算警告不算失敗」——
Redis 掛掉時服務仍應該收流量（每個讀取會 fail-open 回源 SQL，只是變慢）。

🔴 **慈善庫檢查只開連線查 `SELECT 1`，不做任何查詢，也不建立任何 repository 或連線工廠**——
慈善庫是台灣足球策略發展協會（另一個法人）的資料，`docs/14-invariants.md`、`docs/17-deployment.md` §5
明訂不得跨庫存取；本檔完全沒有為慈善庫建立任何可被日後誤用來查資料的常駐管道，用完即丟。

---

## 每個端點吐出哪些欄位、為什麼可以公開

先查過 [`docs/12b-database-tables.md`](../../docs/12b-database-tables.md) §8「受限與加密欄位盤點」：
`players`／`staff`／`articles`／`matches`／`clubs`／`teams`／`article_categories`／`competitions`／`seasons`
**都不在那份清單裡**——受限清單裡的表全是 `Member`／`Registration`／`AdminUser`／`Order`／`DrawRoster`／
`PaymentChannel`／`StoreInvoice` 這類承載個資或金流的表，本次沒有碰到任何一張。

以下逐端點列出**明確 SELECT 的欄位**（⛔ 全程沒有任何 `SELECT *`）：

### `GET /api/v1/clubs`、`GET /api/v1/clubs/{club}`

| 欄位 | 公開理由 |
|---|---|
| `code`、`domain`、`defaultLocale` | 俱樂部的公開識別資訊，前台切換站台就是靠這個 |
| `name`、`description`（`clubs_i18n`） | GEO-03「事實單一來源」要用的官方名稱與簡介 |
| `logoLightKey`／`logoDarkKey`／`faviconKey`／`ogImageKey` | 品牌資產物件鍵（**目前種子資料全為 `NULL`**，尚未走過上傳即縮圖 pipeline，見「已知落差」） |
| `brandColor`／`brandSecondaryColor` | 前台品牌色參考值（實際網頁配色仍以 CSS custom properties 為唯一來源，`docs/14-invariants.md`，這裡只是資料庫存的參考色值） |

⛔ **不吐出**：`invoice_title`、`tax_id`（發票與稅務欄位，不是前台事實內容，本次不公開）、`status`（後台操作狀態）。

### `GET /api/v1/{club}/players`

| 欄位 | 公開理由 |
|---|---|
| `id`、`teamCode`、`shirtNo`、`position`、`birthOn`、`heightCm`、`weightKg`、`nationality`、`preferredFoot`、`photoKey`、`name`、`bio` | 球隊官網例行公開的球員名冊資訊（背號、位置、身材、國籍、簡介），`players` 不在 §8 受限清單，且前台本來就有「球員名單」公開頁面（`docs/02-frontend-spec.md`） |

### `GET /api/v1/{club}/staff`

| 欄位 | 公開理由 |
|---|---|
| `id`、`staffGroup`、`licence`、`photoKey`、`name`、`title`、`bio`、`teamCodes`、`isShared` | 教練與團隊成員簡介，官網例行公開內容。`isShared` 是本 API 自己加的旗標（見下方「`club_id` 強制機制」），不是資料庫欄位，用來讓前台知道這筆是不是兩隊共同資料 |

### `GET /api/v1/{club}/news`、`GET /api/v1/{club}/news/{slug}`

| 欄位 | 公開理由 |
|---|---|
| `id`、`slug`、`categoryCode`／`categoryName`、`coverKey`、`isFeatured`、`publishedAt`、`title`、`summary`、`isShared` | 新聞列表頁需要的欄位，`articles` 不在受限清單。⛔ **只回傳 `status='published'` 且已到發布時間的文章**——草稿與排程中的文章即使 slug 被猜到也一律 404，這是刻意的業務規則 |
| 單篇另外多帶：`viewCount`、`bodyJson`、`seoTitle`、`seoDescription` | 單篇詳情頁才需要，列表故意不帶（避免每次列表都拉可能很大的 `body` JSON） |

### `GET /api/v1/{club}/schedule`

| 欄位 | 公開理由 |
|---|---|
| `id`、`seasonCode`、`teamCode`、`matchOn`、`kickoff`、`homeAway`、`opponent`、`venue`、`competitionTag`／`competitionName`、`status`、`scoreHome`、`scoreAway`、`roundNo`、`matchNo` | 賽程賽果公開頁面的標準欄位，`matches` 不在受限清單。`matchNo`（聯賽官方場次編號，`matches.match_no`）2026-09-21 補上，前台用它重建 mockup 原本的賽事卡片錨點 id（`fx-{日期}-{h\|a}-{場次編號}`），與 `roundNo`（第幾輪）是兩個不同欄位 |

---

## `club_id` 強制機制：為什麼繞不過去

`docs/14-invariants.md` 與主站規劃書 §5.4 明訂「`club_id` 過濾不得依賴呼叫端」。本次的落點是
**型別系統**，不是「每個查詢記得加 WHERE」：

1. **`Security/ClubScope.cs`** 是一個 `readonly struct`，建構子是 `internal`。整個 `Tcrfc.Api` 組件裡，
   只有 `Security/ClubResolver.cs`（`IClubResolver` 的唯一實作）能建立它的實例。
2. **所有 repository 的公開方法簽章都要求 `ClubScope`**，不是 `Guid` 或 `string`
   （例如 `PlayersRepository.ListAsync(ClubScope scope, ...)`）。呼叫端**編譯不過**，除非先呼叫
   `IClubResolver.ResolveAsync(clubCode, ct)` 拿到一個真正的 `ClubScope`。
3. **`ClubResolver.ResolveAsync`** 對 `clubs` 表做參數化查詢（`WHERE code = @Code AND status = 'active'`），
   查不到就丟 `ClubNotFoundException`（對應 404）。端點永遠拿不到指向不存在、或非啟用俱樂部的範圍。
4. Repository 內部組 SQL 時，`club_id` 一律用 `scope.ClubId`（來自已驗證的 `ClubScope`），
   **不是從路由字串或任何呼叫端輸入直接組**。

**「俱樂部專屬優先、回退共同」**（9 張 `club_id` 可為空的表，本次碰到 `staff`／`articles`）的弱讀法
集中在 `Data/ClubOrSharedSql.cs` 的兩個常數（`WhereClubOrShared`／`OrderClubBeforeShared`），
`docs/17-deployment.md` §6 的 SQL 寫法原封搬過來，**不是每個 repository 各自重寫一份等價邏輯**。

### 實測：試圖繞過的案例（見下方「驗收紀錄」有完整 curl 輸出）

1. **跨俱樂部讀清單**：`bw`（藍鯨）目前只有 `clubs` 主檔一筆，沒有球員／教練／新聞資料
   （見 [`db/seed/README.md`](../../db/seed/README.md)「藍鯨為什麼只有這一筆」）。
   打 `GET /api/v1/bw/players`、`/staff`、`/news`、`/schedule` 全部回傳 `totalCount: 0`，
   即使 `tcrfc` 底下有 28 筆球員、83 篇新聞、21 場賽事——證明 `club_id` 過濾確實生效，不是「忘記加條件」。
2. **跨俱樂部讀單篇（更直接的繞過嘗試）**：`articles.slug` 是**全站唯一**（不是 `(club_id, slug)` 複合唯一），
   理論上「知道別俱樂部一篇文章的 slug」就有機會繞過範圍檢查直接讀到內容。實測：先用
   `GET /api/v1/tcrfc/news/{slug}` 確認某篇文章存在且屬於 `tcrfc`，換成
   `GET /api/v1/bw/news/{slug}`（同一個 slug，改用 `bw` 的路由）**回傳 404**，不是內容。
   這是因為 `GetBySlugAsync` 的 WHERE 子句永遠帶 `(club_id = @ClubId OR club_id IS NULL)`，
   `@ClubId` 來自已解析的 `bw` 範圍，跟這篇文章實際的 `club_id`（`tcrfc`）對不上。
3. **不存在的俱樂部代碼**：`GET /api/v1/does-not-exist/players` 回傳 `404`（`ClubNotFoundException`），
   連「範圍是什麼」都建立不起來，更談不上查到資料。
4. **SQL Injection**：`GET /api/v1/tcrfc/players?team=D1';DROP TABLE players;--` 回傳
   `{"items":[],...,"totalCount":0}`（合法但查無資料，因為沒有球隊代碼長那樣），資料庫表安然無恙——
   所有查詢一律走 Dapper 的 `CommandDefinition` 參數化，⛔ 全程沒有字串拼接 SQL。

---

## 英文缺漏時的回退行為

見 `Localization/RequestLocale.cs`。規則只有一條，全部端點一致套用：

> **請求語系的欄位值非空白 → 用它；否則 → 用預設語系（`zh-Hant`）的同一欄位值；
> 兩者都沒有 → 回傳 `null`（不是空字串）。**

實測驗證（真實種子資料，見下方「驗收紀錄」）：

- `players_i18n`／`staff_i18n` 的英文姓名是**有的存、有的沒存**（依來源 JSON `name_en` 是否為空字串決定）：
  有存的球員／教練，`?lang=en` 回傳英文名；沒存的（例如教練「許志傑」），`?lang=en` 回傳中文名——
  不會是空字串或 `undefined`。
- `staff_i18n.title`（職稱）**完全沒有任何英文列**：`?lang=en` 時 `title` 欄位一律回退成中文職稱
  （例如「守門員教練」），即使同一筆資料的 `name` 已經是英文——**回退是逐欄位判斷，不是整筆記錄二選一**。
- `matches_i18n` 的 `opponent`／`venue` 種子資料**完全沒有任何英文列**（只有 `venue` 的 `zh-Hant` 列）：
  `?lang=en` 時兩者都回退——`opponent` 回退到 `matches` 基礎表的中文欄位（因為這張表的中文內容存在
  基礎表不是側表，回退鏈與其他實體不同，見 `MatchesRepository` 類別註解），`venue` 回退到
  `matches_i18n` 的 `zh-Hant` 列。
- `competitions_i18n` 只有 `zh-Hant` 列：`?lang=en` 時 `competitionName` 回退成中文賽事名稱
  「企業甲級足球聯賽」，⛔ **不是**因為 SQL JOIN 條件剛好篩不到而悄悄變成 `null`
  （這是本次開發中途發現並修正的一個坑，原本用 `LEFT JOIN ... AND locale = @lang` 會在請求 `en`
  時直接得到 `null`，看起來像正常回應、其實是遺漏，已改成跟其他實體一致的批次回退查詢）。
- `clubs_i18n`（`bw` 台中藍鯨）：`name` 只有 `zh-Hant` 列（無英文），`?lang=en` 回退成「台中藍鯨」；
  `tcrfc` 兩個語系都有，`?lang=en` 正確回傳「Taichung Rock FC」。

---

## 快取接縫（S0-7d 起接上真正的 Redis）

`Caching/IQueryCache.cs` 定義接縫。`Program.cs` 依 `REDIS_HOST` 是否有設定切換 DI 註冊：

- 沒設定 → `NoOpQueryCache`（原樣呼叫 `factory`，不快取）。本機直接 `dotnet run`（不經 Docker）不帶
  這個變數時就是這條路，本機開發不需要 Redis 也能跑。
- 有設定 → `RedisQueryCache`。`docker-compose.yml`／`docker-compose.dev.yml` 的 `api` 服務**一律有帶**
  `REDIS_HOST`，所以容器化跑法（本機 compose 或正式 VM）一定會走這條路。

### 逐條對應 `docs/17-deployment.md` §4「實作的五條硬規則」

| # | 規則 | 落點 |
|---|---|---|
| 1 | fail-open，Redis 掛掉不得讓請求失敗 | `RedisQueryCache` 的每個私有方法（`TryBuildKeyAsync`／`TryGetAsync`／`TrySetAsync`／`InvalidateAsync`）都把 Redis 例外吞掉，讀取失敗一律回源 `factory`，寫入／失效失敗一律忽略（記警告日誌）。`Program.cs` 起始建立 `ConnectionMultiplexer` 也設 `AbortOnConnectFail = false` 並包 try/catch——連 Redis 從一開始就連不上也不得讓行程無法啟動 |
| 2 | 失效用版本號，⛔ 不用 `KEYS` | key 格式 `v{ver}:{club}:{locale}:{entity}:{qualifier}`；失效＝`INCR ver:{entity}:{club}`（`IQueryCache.InvalidateAsync`）。全程沒有任何 `KEYS`／`SCAN` |
| 3 | 每個 key 一定要有 TTL 兜底 | 見下方「TTL 決定」 |
| 4 | single-flight | `RedisQueryCache` 內部 `ConcurrentDictionary<string, SemaphoreSlim>`，per-key 鎖；拿到鎖後再讀一次快取，避免鎖排隊期間前一個請求已經填好 |
| 5 | 五類禁用資料的 repository 根本不注入 | 目前完全沒有任何程式碼把 `IQueryCache` 注入五類禁用資料的 repository（那些 repository 現在也還不存在）——見 `IQueryCache.cs` 上完整抄錄的五類清單 |

### TTL 決定（執行層決定，§4 沒給數字）

預設 **300 秒（5 分鐘）**，可用 `QUERY_CACHE_TTL_SECONDS` 覆寫。理由：**目前後台還不存在，沒有任何寫入層
會呼叫 `InvalidateAsync`**——這是已知且被接受的取捨，TTL 現在是**唯一**會觸發的失效機制，不是「反正有
版本號機制的兜底而已」。5 分鐘讓「後台之後才補上失效呼叫」這段期間的最大陳舊視窗有界，同時仍能吸收
SSR 同一頁重複讀取的量（`docs/17` §4「甜蜜點」講的就是這種反覆被打的小資料）。**之後後台寫入模組做
write-invalidate 時，直接呼叫 `IQueryCache.InvalidateAsync(entity, club, ct)` 即可，介面不用再改**——
這正是本次任務要求「一併提供遞增版本號的公開方法」的目的。

### key 命名維度

`GetOrCreateAsync<T>(entity, club, locale, qualifier, factory, ct)` 四個字串參數對應 `docs/17` §4
「key 命名必須含 club_id 與 locale 維度」；`Caching.CacheDimensions` 提供 `SharedClub`／`AnyLocale`／
`NoQualifier` 三個常數，避免各處各自寫一份「跨俱樂部共用」的魔術字串（字串對不起來＝快取永遠失效或
誤命中）。

### 🔴 `null` 不快取

`RedisQueryCache.GetOrCreateAsync<T>` 只在 `factory` 回傳非 `null` 時才寫入快取（`ClubDto?`、
`ArticleDetailDto?`、`ClubResolver` 內部用的 `Guid?` 皆適用）。查無資料（404）的負向結果不會被快取住，
下一次同樣的請求會直接回源——草稿文章排程發布後、或 slug 打錯字修正後，不會被一個 TTL 週期內的
「查無資料」快取檔住。**傳回空清單**（例如某個篩選條件下 `PagedResult<T>.Items` 為空、`TotalCount`
為 0）**不受影響、正常快取**——`PagedResult<T>` 物件本身從來不是 `null`，「查到 0 筆」是合法且穩定的
答案（`bw` 目前所有內容端點都是這種情況：物件存在、`TotalCount` 是 0，這筆 0 結果本身會被快取）。

### 五組唯讀 repository 全部接上 `IQueryCache`（S0-7d 續作，2026-09-21，使用者拍板）

上一版本次任務原本只在 `Security/ClubResolver.cs` 示範用法、刻意沒有動 `Features/*` 的五個
repository——當時把任務指示「端點與 repository 不得修改」解讀為「連建構子參數都不能加」。
**使用者拍板澄清**：那句話的本意是「接縫從 no-op 換成 Redis 不需要動它們」，不是「一行都不能碰」；
加一個 `IQueryCache` 建構子參數、對外契約（路由、查詢參數、回應 JSON 形狀）完全不變，是接縫本來就
預期的用法。**因此本次把 `ClubsRepository`／`PlayersRepository`／`StaffRepository`／
`ArticlesRepository`／`MatchesRepository` 全部接上**，只改建構子與方法內部（把既有查詢邏輯包進
`cache.GetOrCreateAsync(...)` 的 `factory`），**端點簽章、DTO、JSON 回應欄位逐一未變**——這條線由
`Tcrfc.Api.Tests`（詳見下方「測試」）與 `site/tools/compare-dom.mjs` 既有的逐頁驗收把關。

**逐一 repository 的 entity／club／locale／qualifier：**

| Repository ／方法 | entity | club 維度 | qualifier 涵蓋的參數 |
|---|---|---|---|
| `ClubsRepository.ListAsync` | `clubs-list` | `CacheDimensions.SharedClub`（不屬於任一俱樂部，這支端點本來就回傳全部俱樂部） | 無（只有 locale） |
| `ClubsRepository.GetAsync` | `club-detail` | `scope.ClubCode` | 無（只有 locale）。🔴 查無資料不快取 |
| `PlayersRepository.ListAsync` | `players` | `scope.ClubCode` | `team:page:pageSize` |
| `StaffRepository.ListAsync` | `staff` | `scope.ClubCode` | `team:page:pageSize` |
| `ArticlesRepository.ListAsync` | `articles` | `scope.ClubCode` | `category:page:pageSize` |
| `ArticlesRepository.GetBySlugAsync` | `article-detail` | `scope.ClubCode` | `slug`。🔴 查無資料（404）不快取，見下方「排程發布與快取」 |
| `MatchesRepository.ListAsync` | `schedule` | `scope.ClubCode` | `team:season:status:page:pageSize` |
| `Security/ClubResolver.ResolveAsync`（既有，S0-7d 第一階段） | `club-scope` | 已解析出的俱樂部代碼 | 無 |

`staff`／`articles`／`schedule` 三個 qualifier 裡沒帶值的段落用空字串（不是 `null` 字串），
例如賽程完全不帶篩選時 qualifier 是 `"::1:20"`（team／season／status 三段皆空，page=1，pageSize=20）
——這是刻意的，`CacheDimensions.NoQualifier` 本身就是空字串，讓「沒有這個篩選條件」與「這個篩選條件
剛好是空字串」用同一種表示法，不會有兩種不同的「空」互相衝突。

### 🔴 排程發布與快取的語意後果（必讀，不是 bug）

`ArticlesRepository` 的兩個方法只回傳 `status = 'published'` **且已到發布時間**
（`published_at <= SYSUTCDATETIME()`）的文章。**「時間到了」這件事本身沒有任何寫入事件**——
後台排定 10:00 發布一篇文章，不會有任何程式碼在 10:00 那一刻呼叫 `IQueryCache.InvalidateAsync`
（因為根本沒有寫入動作發生，只是時間流逝讓 SQL 的 `WHERE` 條件從不成立變成成立）。

**後果**：`articles` 與 `article-detail` 這兩個 entity 底下，「文章排程發布後多久會真的在 API 回應
出現」完全由 **TTL 決定**——最多延後一個 TTL 週期（預設 300 秒／5 分鐘）。這不是遺漏也不是 bug，
是本次任務範圍內、後台尚不存在時的已知取捨；一旦後台的排程發布功能真的開發，該功能**必須**在文章
狀態變成可見的那一刻主動呼叫 `InvalidateAsync("articles", club)` 與 `InvalidateAsync("article-detail",
club)`（或改用排程觸發器主動失效），否則使用者會持續看到最多 5 分鐘前的排程發布狀態。

⚠️ **若這個延遲對排程發布不可接受**（例如客戶期待「10:00 設定發布，10:00 準時看得到」），
`QUERY_CACHE_TTL_SECONDS` 可以調低，但代價是每個接了快取的端點在 TTL 內能吸收的重複讀取量變少、
對 Basic 層 5 DTU 的保護效果變弱——**這是一個需要業務判斷的取捨，本檔沒有代為決定新的預設值**，
維持 300 秒是延續上一階段任務的既有決定，不是這次重新評估過的結論。

---

## 錯誤處理

`Common/ApiExceptionHandler.cs` 是全站最後一道例外處理防線：

- `ClubNotFoundException` → `404`，訊息只說「找不到俱樂部」＋俱樂部代碼，不含任何 SQL／連線細節。
- 其他任何例外 → `500`，回應固定是「伺服器發生未預期的錯誤，請稍後再試」，**完整例外（含堆疊）只寫進
  `ILogger`**，⛔ 不會出現在 HTTP 回應裡。本次開發期間實際踩過的兩個 500（見下方「開發過程踩的坑」）
  都是先看伺服器端日誌才找到根因，而不是看回應內容——這正是這個設計要達成的效果。

---

## 開發過程踩的坑（已修正，記錄見 `docs/18-work-errors.md` E-19／E-20）

1. **`InvariantGlobalization=true` 讓 `Microsoft.Data.SqlClient` 連線直接炸**（E-19）：
   這個旗標對純 HTTP／JSON 服務通常安全，但只要相依鏈裡有需要定序或編碼轉換的資料庫驅動就是地雷。
   已移除。
2. **Dapper 的 record 具現化對 `DateOnly` 與 SELECT 欄位數要求比預期嚴格**（E-20）：
   SQL `date` 欄位在 ADO.NET 層永遠回報 `DateTime`，record 屬性宣告成 `DateOnly` 會讓 Dapper
   找不到相符的建構子而整支查詢丟 `InvalidOperationException`；另外兩個查詢共用一個 record 型別
   但 SELECT 的欄位數不同，也是同一種例外。已修正（`PlayersRepository`／`MatchesRepository` 的
   內部 row 型別改用 `DateTime`，`Map()` 再轉 `DateOnly`；`ArticlesRepository` 拆出精確對應欄位的型別）。

---

## 驗收紀錄（2026-09-21，本機環境）

環境：`docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d mssql-dev`（已跑、healthy），
DDL 與種子資料已灌（`clubs` 2、`players` 28、`staff` 8、`articles` 83、`matches` 21、`article_categories` 8）。

```
$ dotnet build
建置成功。0 個警告 0 個錯誤

$ docker build -t tcrfc-api-test apps/api    # 用 apps/api/Dockerfile 實際建置一次
...
naming to docker.io/library/tcrfc-api-test:latest done   # 成功

$ docker run ... tcrfc-api-test   # 容器內連本機 mssql-dev（host.docker.internal）
$ docker inspect --format='{{.State.Health.Status}}' tcrfc-api-test-run
healthy    # apps/api/Dockerfile 的 HEALTHCHECK 打 /readyz，通過

$ curl /healthz
{"status":"ok"}
$ curl /readyz
{"status":"ready","club_db":"ok"}

$ curl /api/v1/clubs
[{"code":"tcrfc","name":"台中磐石",...},{"code":"bw","name":"台中藍鯨",...}]

$ curl "/api/v1/tcrfc/players?pageSize=100" | jq '.totalCount'
28
$ curl "/api/v1/bw/players?pageSize=100" | jq '.totalCount'
0

$ curl "/api/v1/tcrfc/staff?pageSize=100" | jq '.totalCount'
8

$ curl "/api/v1/tcrfc/news?pageSize=200" | jq '.totalCount'
83
$ curl "/api/v1/tcrfc/news?category=match&pageSize=200" | jq '.totalCount'
56

$ curl "/api/v1/tcrfc/schedule?pageSize=100" | jq '.totalCount'
21
$ curl "/api/v1/bw/schedule?pageSize=100" | jq '.totalCount'
0

# 跨俱樂部繞過嘗試：拿 tcrfc 一篇文章的 slug 改用 bw 路由查
$ curl -o /dev/null -w "%{http_code}\n" "/api/v1/bw/news/2026-08-10-international-000"
404

# 不存在的俱樂部
$ curl -o /dev/null -w "%{http_code}\n" "/api/v1/does-not-exist/players"
404

# SQL injection 嘗試
$ curl "/api/v1/tcrfc/players?team=D1';DROP TABLE players;--"
{"items":[],"page":1,"pageSize":50,"totalCount":0,"totalPages":0}
$ curl "/api/v1/tcrfc/players?pageSize=1" | jq '.totalCount'   # 確認表還在
28

# 語言回退
$ curl "/api/v1/tcrfc/clubs/tcrfc?lang=en" | jq '.name'   # 有英文
"Taichung Rock FC"
$ curl "/api/v1/clubs/bw?lang=en" | jq '.name'             # 沒英文，回退中文
"台中藍鯨"
$ curl "/api/v1/tcrfc/staff?lang=en&pageSize=50" | jq -r '.items[] | "\(.name) | \(.title)"'
Juliano Rodrigues | 守門員教練    # name 有英文、title 沒有英文各自獨立回退
...
許志傑 | 青訓教練                # name 也沒英文，整欄回退中文

# 正式環境關閉 OpenAPI（容器內 ASPNETCORE_ENVIRONMENT=Production）
$ curl -o /dev/null -w "%{http_code}\n" "/openapi/v1.json"
404
```

（原始輸出格式已用 `jq`／`python3 -m json.tool` 整理以便閱讀，數字與行為與實際 curl 回應一致。）

### S0-7d 驗收紀錄（2026-09-21）

環境：本機 `mssql-dev`（同上，種子資料未變）。**這個環境的 Docker Desktop 對 Docker Hub 的拉取
異常緩慢**（`redis:8-alpine` 光拉取層就卡了超過 30 分鐘，`docker compose up` 因此不可行）——
改用 `brew install redis` 取得**真正的 `redis-server` 二進位檔**（非 mock、非模擬），跑在本機
`16379` port，讓 `apps/api` 的 Docker 容器透過 `host.docker.internal` 連過去，等於用另一條路徑
换到同樣是「真實 Redis」的驗證環境。這件事本身也記進 [`docs/18-work-errors.md`](../../docs/18-work-errors.md)。

```
$ dotnet build   # apps/api 與 apps/api/Tcrfc.Api.Tests 兩個專案
Build succeeded. 0 Warning(s) 0 Error(s)

$ docker build -t x apps/api      # 任務要求的確認命令，build context 仍是 ./apps/api
...
naming to ghcr.io/waiting0201/tcrfc-api:latest done   # 成功，Tcrfc.Api.Tests 未被送進 build context

$ cd apps/api/Tcrfc.Api.Tests && dotnet test
# CLUB_SQL_CONNECTION_STRING 未設定時：
Failed! - Failed: 14, Passed: 0, Skipped: 0, Total: 14   # 全部清楚回報失敗＋可執行的修復訊息，不是悄悄略過
# 設定 CLUB_SQL_CONNECTION_STRING 指向本機 mssql-dev 後：
Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 2 s
```

`/readyz` 三種情境（直接 `dotnet run`，未經 Docker）：

```
# 完全沒設定 CHARITY_SQL_CONNECTION_STRING／REDIS_HOST
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"not_configured"}

# CHARITY_SQL_CONNECTION_STRING 指向一個確定連不上的位址（127.0.0.1:19999）
$ curl -w "\nHTTP:%{http_code}\n" /readyz
{"status":"not_ready","club_db":"ok","charity_db":"fail","redis":"not_configured"}
HTTP:503
```

容器層級的 Redis fail-open（用 `docker run` 跑已建好的 `ghcr.io/waiting0201/tcrfc-api:latest`，
`ASPNETCORE_ENVIRONMENT=Production`，連本機 `mssql-dev` 與 `brew` 裝的真實 `redis-server`）：

```
# 情境一：REDIS_HOST 指到一個從未有任何服務存在的位址與埠——啟動時就連不上
$ docker run -d -e REDIS_HOST=host.docker.internal -e REDIS_PORT=59999 ... tcrfc-api
$ docker logs <container>   # 正常啟動，沒有因為 Redis 連不上而崩潰或延遲
Now listening on: http://[::]:8080
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"degraded"}
$ curl "/api/v1/tcrfc/players?pageSize=1"   # 經過 IClubResolver → RedisQueryCache
{"items":[{...,"name":"蔡俊昇",...}],"page":1,"pageSize":1,"totalCount":28,"totalPages":28}
$ docker inspect --format='{{.State.Health.Status}}' <container>
healthy

# 情境二：REDIS_HOST 指到一個真的在跑的 redis-server（brew，16379 埠，requirepass 開著）
$ docker run -d -e REDIS_HOST=host.docker.internal -e REDIS_PORT=16379 -e REDIS_PASSWORD=<本機一次性測試密碼，已隨 redis-server 程序關閉失效> ...
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"ok"}

# 命中 IClubResolver 的快取路徑後，redis-cli 直接檢查——key 命名、TTL、內容全部符合設計：
$ curl "/api/v1/tcrfc/players?pageSize=1" >/dev/null
$ redis-cli -p 16379 -a *** keys '*'
v0:tcrfc:_any:club-scope:
$ redis-cli -p 16379 -a *** ttl "v0:tcrfc:_any:club-scope:"
300
$ redis-cli -p 16379 -a *** get "v0:tcrfc:_any:club-scope:"
"f7cb2444-e607-57fc-aea5-6aa11239d4ea"   # 真正快取住的俱樂部 GUID

# 手動遞增版本號（模擬日後後台寫入層呼叫 InvalidateAsync），驗證失效機制：
$ redis-cli -p 16379 -a *** incr "ver:club-scope:tcrfc"
(integer) 1
$ curl "/api/v1/tcrfc/players?pageSize=1" >/dev/null
$ redis-cli -p 16379 -a *** keys '*club-scope*'
v0:tcrfc:_any:club-scope:
ver:club-scope:tcrfc
v1:tcrfc:_any:club-scope:   # 版本號一變，立刻改寫新 key；舊 v0 key 原封不動留給 TTL／LRU 淘汰

# 情境三：Redis 在連線建立「之後」才掛掉（比情境一更貼近正式環境的真實故障模式）
$ kill <redis-server pid>   # 直接停掉 redis-server，模擬正式環境 Redis 中途掛掉
$ redis-cli -p 16379 -a *** ping
Could not connect to Redis at 127.0.0.1:16379: Connection refused
$ curl "/api/v1/tcrfc/players?pageSize=1"   # 同一個、已經連線過的容器，不重啟
{"items":[{...,"name":"蔡俊昇",...}],"page":1,"pageSize":1,"totalCount":28,"totalPages":28}   # 仍然 200，回源 SQL
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"degraded"}   # 從 ok 變 degraded，status 仍是 ready
```

三個情境合起來證明：Redis 從一開始就連不上、Redis 正常運作時真的會寫入並照設計的 key／TTL／版本號
規則運作、以及 Redis 在執行中途才斷線——三種情況下 API 都沒有變成不可用，只有 `/readyz` 的 `redis`
欄位如實反映狀態。

### S0-7d 續作驗收紀錄（2026-09-21，五組 repository 接上 `IQueryCache`）

自動化測試：`dotnet test` 從 14 項增為 **30 項**，全部沿用真實 `mssql-dev`；新增的 16 項裡
10 項是 `CacheBehaviorTests`（用真的啟動的 `redis-server`），6 項是 `CacheFailOpenTests` 擴增
（改用 `[Theory]` 涵蓋五組端點＋新增文章單篇的 fail-open 測試）。

```
$ dotnet build   # apps/api 與 apps/api/Tcrfc.Api.Tests
Build succeeded. 0 Warning(s) 0 Error(s)

$ cd apps/api/Tcrfc.Api.Tests && dotnet test
Passed! - Failed: 0, Passed: 30, Skipped: 0, Total: 30, Duration: 16 s

$ docker build -t x apps/api
...
naming to docker.io/library/x:latest done   # 成功
```

容器層級驗證（`docker run` 跑上面建好的映像檔，接本機 `mssql-dev`，以及 `brew install redis` 裝的
真實 `redis-server`，`ASPNETCORE_ENVIRONMENT=Production`）：

```
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"ok"}

# 依序打熱五組端點（clubs／tcrfc 的 players／staff／news／schedule），外加 bw 的 players
$ redis-cli -p 16380 -a *** keys '*'
v0:tcrfc:zh-Hant:players::1:5
v0:tcrfc:_any:club-scope:
v0:tcrfc:zh-Hant:staff::1:5
v0:bw:zh-Hant:players::1:5
v0:tcrfc:zh-Hant:schedule::::1:5
v0:_shared:zh-Hant:clubs-list:
v0:tcrfc:zh-Hant:articles::1:5
v0:bw:_any:club-scope:
```

逐一核對：

- **entity 名稱與 club／locale 維度如設計**：`players`／`staff`／`articles`／`schedule` 都帶著
  `tcrfc` 或 `bw` 的 club 維度；`clubs-list` 用 `_shared`（不屬於任一俱樂部）；`club-scope`
  （既有的 `ClubResolver` 接線）維度是 `_any`（與語系無關）。
- **qualifier 真的把每個參數編進 key**：`schedule` 的 `v0:tcrfc:zh-Hant:schedule::::1:5`——
  `team:season:status:page:pageSize` 五段，前三段（team／season／status）都沒帶篩選時是空字串，
  page=1、pageSize=5 兩段有值，跟 `MatchesRepository.ListAsync` 的 qualifier 組字串邏輯逐字對得上。
- **TTL**：`redis-cli -p 16380 -a *** ttl "v0:tcrfc:zh-Hant:players::1:5"` → `282`（在預設 300 秒
  之內，是打完 API 之後幾秒才查詢的合理耗損）。`clubs-list` 的 TTL 也是 `282`，同一批請求、同一個
  TTL 設定，數字一致。
- **club 隔離不是憑空推論**：`v0:bw:zh-Hant:players::1:5` 的值是
  `{"Items":[],"Page":1,"PageSize":5,"TotalCount":0,"TotalPages":0}`——`bw` 自己的 key、自己的
  0 筆結果，跟 `tcrfc` 那把 28 筆的 key 完全分開。額外用 HTTP 逐一核對 `bw` 的
  `staff`／`news`／`schedule` 三個端點也都回 `totalCount: 0`，而 `tcrfc` 的 `players` 仍是 28。
- **404 不快取**：打一個確定不存在的 slug（`this-does-not-exist-xyz`）回 404 後，
  `redis-cli -p 16380 -a *** keys '*article-detail*'` 回傳空——沒有任何 key 被寫入。
- **版本號失效在新接的 entity 上也成立**：`redis-cli -p 16380 -a *** incr "ver:players:tcrfc"` 之後
  再打一次 `tcrfc` 的 `players`，`keys '*players*'` 同時看得到 `v0:tcrfc:...`（舊，留給 TTL／LRU）
  與 `v1:tcrfc:...`（新），而 `v0:bw:...` 沒被這次遞增影響——版本號的 club 維度也正確隔離。
- **Redis 執行中途掛掉，五組端點與 `/readyz` 都正確 fail-open**（同一個、不重啟的容器）：

  ```
  $ kill <redis-server pid>
  $ for p in /api/v1/clubs "/api/v1/tcrfc/players?pageSize=1" "/api/v1/tcrfc/staff?pageSize=1" \
             "/api/v1/tcrfc/news?pageSize=1" "/api/v1/tcrfc/schedule?pageSize=1"; do
      curl -s -o /dev/null -w "%{http_code}\n" "http://127.0.0.1:18097$p"
    done
  200
  200
  200
  200
  200
  $ curl /readyz
  {"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"degraded"}
  ```

⚠️ **本次驗證再度用 `brew install redis` 的真實 `redis-server` 而不是 Docker 容器**——這個沙盒環境
拉 `redis:8-alpine` 依然極慢（同一個環境限制，見上一階段「S0-7d 驗收紀錄」與 `docs/17` §11 第 12 條），
沿用同一個解法。收尾已確認清掉手動啟動的 `redis-server` 行程與可能產生的 `dump.rdb`
（`--save ""` 已避免它產生，但仍執行過一次 `ls dump.rdb` 確認）。

### 追加：`matches.match_no`（2026-09-21）

`db/club-schema.sql` 補上 `matches.match_no`（聯賽官方場次編號）後，`MatchDto`／`MatchesRepository`／
`MatchesEndpoints` 同步補上這個欄位（明確 SELECT，非 `SELECT *`；沿用既有 `ClubScope` 機制，
沒有另外開任何繞過路徑）。驗證：

```
$ curl "/api/v1/tcrfc/schedule?pageSize=200" | jq '[.items[] | select(.matchNo == null)] | length'
0   # 21 筆全部帶值，與 site/src/data/schedule.json 的 match_no 逐筆核對一致
```

---

## 測試（S0-7d，`apps/api/Tcrfc.Api.Tests`，共 30 項）

S0-7b 為止零測試——所有行為保證只存在於本檔的 curl 紀錄裡。S0-7d 新增獨立測試專案
`Tcrfc.Api.Tests`（xUnit 2.9 + `Microsoft.AspNetCore.Mvc.Testing`），對 `Program`
用 `WebApplicationFactory<Program>` 啟動真正的行程內主機，打真正的 HTTP 管線，接真正的
本機 `mssql-dev`——不 mock 資料庫，也不 mock Redis：`RedisUnavailableApiFixture` 用「指向一個
確定沒人聽的本機連接埠」讓失敗是真的失敗；`RedisEnabledApiFixture`（S0-7d 續作新增）直接啟動一個
真正的 `redis-server` 子行程（`brew install redis` 裝的那個二進位檔），驗證 key／TTL／隔離這些
「Redis 正常運作時該長什麼樣」的行為，不是靠讀程式碼推論。

### 涵蓋範圍

| 測試檔 | 驗證什麼 |
|---|---|
| `ClubScopingTests` | 跨俱樂部讀清單為 0 筆、跨俱樂部讀已知 slug 的文章詳情回 404（不是內容）、不存在的俱樂部代碼回 404 |
| `LocalizationFallbackTests` | 語系逐欄位回退（同一筆記錄可以一半英文一半中文）；用 Unicode 範圍判斷中文／英文，不硬編碼種子資料的確切字串 |
| `PagingNormalizationTests` | `page<=0`→1、`pageSize<=0`→端點預設值、超過上限截斷 |
| `SqlInjectionTests` | 惡意 `team` 參數不炸、不回傳資料、資料表安然無恙 |
| `CacheFailOpenTests` | Redis 不可用時，`ClubResolver`＋**五組接了快取的端點**（`clubs`／`players`／`staff`／`news` 列表與單篇／`schedule`）全部仍然成功；`/readyz` 仍回 `ready` 但 `redis` 標示 `degraded`（S0-7d 續作擴大涵蓋範圍，原本只驗證 `players` 一個端點） |
| **`CacheBehaviorTests`（S0-7d 續作新增）** | qualifier 是否真的涵蓋每個會改變結果的參數（`pageSize`／`team`／`category`／`season`／`status`）、同參數兩次結果一致、文章單篇不同 slug 互不污染、**404 不寫入快取**、**用 `IConnectionMultiplexer` 直接核對 key 真的依 club／locale 隔離**、key 有 TTL 兜底 |

### 怎麼跑

需要本機既有的 `sqlserver` 容器已啟動且已灌種子資料（同上方「怎麼跑（本機開發）」的前置；
🔴 2026-09-21 起 `mssql-dev` 已併入這個既有容器，不再是獨立服務，見 `deploy/README.md`）：

```bash
docker ps --filter name=sqlserver   # 確認既有容器在跑
./deploy/local-ddl.sh --apply
./db/seed/apply-seed.sh

export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club_dev;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;"

cd apps/api/Tcrfc.Api.Tests
dotnet test
```

**2026-09-21 驗收**：合併進 `sqlserver` 容器後重跑，30 項全綠（`Passed! - Failed: 0, Passed: 30,
Skipped: 0, Total: 30`），與 S0-7d 的既有結果一致——證明合併容器沒有改變任何行為。

🔴 **資料庫不可用時，測試回報失敗（不是略過、更不會看起來像通過）**：`ApiFixture`／
`RedisUnavailableApiFixture` 的 `IAsyncLifetime.InitializeAsync()` 會先檢查
`CLUB_SQL_CONNECTION_STRING` 是否有設定、能不能連上，連不上就直接丟一個訊息清楚的
`InvalidOperationException`——xUnit 會把使用該 fixture 的每一個測試都回報為 `Failed`
並附上這個訊息（怎麼啟動本機依賴的具體指令）。**這是刻意的選擇，不是引入
`Xunit.SkippableFact` 之類的第三方套件做「略過」**——失敗比略過更難被 CI 儀表板悄悄忽略，
且相依套件數量維持最少；若之後接上 CI 且 CI 固定會提供資料庫，這個決定可以重新評估，
本檔沒有代為決定 CI 一定要用哪一種語意。

### 三個 fixture、三種環境設定

- `ApiFixture`：`REDIS_HOST` 清空（強制走 `NoOpQueryCache`），只驗證主站庫相關行為。
- `RedisUnavailableApiFixture`：`REDIS_HOST=127.0.0.1`＋一個用 `TcpListener` 現抓現放的
  本機閒置連接埠（不是猜一個「應該沒人用」的埠號），讓 `RedisQueryCache` 真的拿到連線失敗，
  專門驗證 fail-open。
- **`RedisEnabledApiFixture`（S0-7d 續作新增）**：真的啟動一個 `redis-server` 子行程（同樣用
  `TcpListener` 現抓現放的埠號，然後在那個埠上啟動 `redis-server --requirepass ... --save ""`），
  專門驗證「Redis 正常運作時」的行為——key 命名、qualifier、跨俱樂部與跨語系隔離、TTL。
  找不到 `redis-server` 執行檔時（本機沒裝）會直接丟清楚的例外（含 `brew install redis` 的指示），
  跟資料庫不可用時的行為一致，不會悄悄跳過。可用 `REDIS_SERVER_PATH` 環境變數覆寫執行檔位置。
  ⚠️ `--save ""` 是必要的——沒有它，`redis-server` 收到終止訊號時會在**目前工作目錄**寫一個
  `dump.rdb`（S0-7d 第一階段手動驗證時真的踩過這個坑，見 `docs/18-work-errors.md` E-27 附近的說明），
  用測試自動化跑的話這個風險更大（工作目錄通常就是專案根目錄）。

三者都用**行程環境變數**（`Environment.SetEnvironmentVariable`）而不是
`WebApplicationFactory.ConfigureWebHost` 的 `ConfigureAppConfiguration` 來傳遞設定——
因為 `Program.cs` 在 `builder.Build()` 之前就會讀 `REDIS_HOST` 決定要不要注入
`RedisQueryCache`，那段程式碼跑在測試主機的攔截點之前。因此測試組件用
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` 停用平行化，
避免不同 fixture 的環境變數互相污染（測試數量少，停用平行化的時間成本可以接受）。

### Docker 映像檔不受影響

`Tcrfc.Api.Tests` 是 `apps/api/` 目錄下的獨立子專案（沒有 `.sln`）。`Tcrfc.Api.csproj`
已明確 `<Compile Remove="Tcrfc.Api.Tests/**/*.cs" />`（否則預設的遞迴萬用字元會把測試原始碼
一起編進主專案，因為兩個 .csproj 在同一個目錄樹下、沒有解決方案檔幫忙切開建置範圍）；
`apps/api/.dockerignore` 也排除了這個目錄，`docker build -t x apps/api` 不受影響
（已實跑驗證，見「S0-7d 驗收紀錄」）。

---

## 相關文件

- [`docs/12-database-schema.md`](../../docs/12-database-schema.md)／[`12a`](../../docs/12a-database-erd.md)／[`12b`](../../docs/12b-database-tables.md)／[`12c`](../../docs/12c-i18n-tables.md) — 資料表設計、權限模型、受限欄位、i18n 側表
- [`docs/14-invariants.md`](../../docs/14-invariants.md) — `club_id` 維度、跨庫 JOIN 陷阱、不得讀快取清單
- [`docs/17-deployment.md`](../../docs/17-deployment.md) §0／§1／§4／§6 — 技術選型、容器佈局、快取策略、本機開發資料庫
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) E-19／E-20 — 本次開發踩的坑
- [`db/seed/README.md`](../../db/seed/README.md) — 本機資料庫怎麼連、種了哪些資料、已知落差
- [`apps/web/README.md`](../web/README.md) — 這支 API 的呼叫端（Nuxt 前台骨架）
