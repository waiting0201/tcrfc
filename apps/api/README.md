# apps/api — api（單一 .NET 行程，唯讀讀取 API）

**本次交付範圍（S0-7b，2026-09-21，`backend-engineer`）**：讓 Nuxt 前台（[`apps/web`](../web/README.md)）能打真實資料庫，
建立球員、教練與職員、新聞、賽程與賽果、俱樂部主檔五組**唯讀** GET 端點。

🔴 **不含**：任何寫入、登入與權限、商店與金流、後台任何模組、慈善捐款平台（獨立庫，見 [`docs/17-deployment.md`](../../docs/17-deployment.md) §5）。
這些留給後續任務，屆時會用到本檔還沒接的 EF Core（寫入）與完整權限模型（[`docs/12b`](../../docs/12b-database-tables.md) §7）。

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
│   ├── IQueryCache.cs             # 快取接縫（本次注入 no-op）
│   └── NoOpQueryCache.cs
├── Common/
│   ├── PagedResult.cs / PagingQuery.cs
│   ├── ApiExceptionHandler.cs     # 統一例外處理，不外流資料庫例外訊息
│   └── HealthEndpoints.cs         # /healthz、/readyz
└── Features/
    ├── Clubs/      (ClubDto, ClubsRepository, ClubsEndpoints)
    ├── Players/    (PlayerDto, PlayersRepository, PlayersEndpoints)
    ├── Staff/      (StaffDto, StaffRepository, StaffEndpoints)
    ├── News/       (ArticleListItemDto/ArticleDetailDto, ArticlesRepository, ArticlesEndpoints)
    └── Schedule/   (MatchDto, MatchesRepository, MatchesEndpoints)
```

每個 Features 子目錄都是「DTO＋repository＋endpoints」三件套，彼此不互相依賴（除了都經過 `IClubResolver`）。

---

## 怎麼跑（本機開發）

### 前置

1. 本機資料庫容器已啟動且已灌種子資料（見 [`db/seed/README.md`](../../db/seed/README.md)）：
   ```bash
   docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d mssql-dev
   ./deploy/local-ddl.sh --apply
   ./db/seed/apply-seed.sh
   ```
2. 確認 `.env` 有 `MSSQL_DEV_SA_PASSWORD`。

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
export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,14330;Database=tcrfc_club_dev;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;"
export CORS_ALLOWED_ORIGINS="http://localhost:3000,http://localhost:3001"
export ASPNETCORE_URLS="http://127.0.0.1:5299"

dotnet run --no-launch-profile
```

驗證：
```bash
curl http://127.0.0.1:5299/healthz     # {"status":"ok"}
curl http://127.0.0.1:5299/readyz      # {"status":"ready","club_db":"ok"}（實際開一條連線查 SELECT 1）
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
  -e CLUB_SQL_CONNECTION_STRING="Server=host.docker.internal,14330;Database=tcrfc_club_dev;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e CORS_ALLOWED_ORIGINS="http://localhost:3000" \
  -p 18080:8080 \
  tcrfc-api-local

curl http://127.0.0.1:18080/healthz
docker inspect --format='{{.State.Health.Status}}' tcrfc-api-local   # 等 healthcheck 跑完應為 healthy
```

`host.docker.internal` 是容器連回宿主機 `127.0.0.1:14330`（`mssql-dev` 對外埠）的方式；
真正的 `docker-compose.yml` 疊 `docker-compose.dev.yml` 跑起來時，容器間走內部網路的服務名 `mssql-dev`，
不需要這個 workaround（見 `deploy/dev/club.env`）。

正式部署（VM 上、走 `docker-compose.yml`）的環境變數來源見 [`docs/17-deployment.md`](../../docs/17-deployment.md) §1
與 [`docs/20-cicd.md`](../../docs/20-cicd.md) §7.2——本檔不重複那份清單，只列本次新增／確認用得到的鍵名（見下）。

---

## 環境變數

| 變數 | 必填 | 說明 |
|---|---|---|
| `CLUB_SQL_CONNECTION_STRING` | ✅ | 主站庫連線字串。本機見 [`deploy/dev/club.env`](../../deploy/dev/club.env)（不進版控），正式環境見 VM 上 `/opt/tcrfc/secrets/club.env`（`docs/20-cicd.md` §7.2）。**鍵名故意跟兩邊祕密檔一致**，不繞去 `ConnectionStrings:Club` 這種 ASP.NET Core 慣用間接層——本檔的 `ClubSqlConnectionFactory` 直接讀這個鍵。⛔ **不得寫死在任何檔案裡** |
| `CORS_ALLOWED_ORIGINS` | 正式環境必填，本機可省略 | 逗號分隔的允許來源清單，來自 `docker-compose.yml` 的 `api` 服務定義（`docs/17-deployment.md` §10.2 的既有缺口，本次由前一任務補上）。本機開發若沒帶，`Development` 環境會退回 `localhost:3000/3001/3002` 三個 `apps/web` 常用埠；**正式環境沒有這個退回值**——沒設定就是沒有任何來源被允許，比「忘記設定就開放全部」安全 |
| `ASPNETCORE_ENVIRONMENT` | 建議設 | `Development` 才會開 OpenAPI 端點，其餘值一律關閉 |
| `ASPNETCORE_URLS` | 本機開發用 | 監聽位址，容器內固定用 `Dockerfile` 的 `ASPNETCORE_HTTP_PORTS=8080` |

⛔ **本次任務範圍完全不碰慈善庫、Redis、LINE Pay、JWT、Blob**——這些鍵名雖然已經在 `docker-compose.yml` 的
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

### `/readyz` 範圍

`apps/api/Dockerfile` 原註解寫「真的檢查兩個 DbContext 能連線」，但**本次任務完全不碰慈善庫與 Redis**
（任務範圍明文排除慈善平台），所以目前 `/readyz` **只驗證主站庫連線**。慈善庫連線檢查與 Redis
連線檢查（依 [`docs/17-deployment.md`](../../docs/17-deployment.md) §4「連線失敗算警告不算失敗」）
留給那兩塊功能真正接上時補齊——這是刻意的範圍縮減，不是遺漏。

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

## 快取接縫

`Caching/IQueryCache.cs` 定義接縫，本次注入 `NoOpQueryCache`（原樣呼叫，不快取）。

**為什麼這次不接 Redis**：[`docs/17-deployment.md`](../../docs/17-deployment.md) §4 定的是
cache-aside ＋ SQL fallback，**失效的觸發點是「後台寫入成功後刪 key」**——但後台目前還不存在，
這時候接 Redis 等於加一層永遠不會失效的快取（球員異動、新聞改稿都沒有任何東西會去刪對應的 key）。

**之後怎麼接**：新增一個 cache-aside 實作（`GetOrCreateAsync` 內部改成真的查 Redis、未命中才呼叫
`factory`），改 `Program.cs` 的 DI 註冊（`AddSingleton<IQueryCache, RedisQueryCache>()`），
**端點與 repository 完全不用改**——這是這層接縫存在的目的。

🔴 **`IQueryCache` 的 XML 文件註解裡完整抄錄了 [`docs/17-deployment.md`](../../docs/17-deployment.md) §4
「⛔ 不得讀快取的資料」五類清單**（庫存與商品可購買狀態、金流回呼冪等檢查、會員卡驗證、
會籍與訂單狀態、購物車），提醒之後接 Redis 的人：**本次五個端點都不在那五類之列可以安全接**，
但那五類**未來實作對應功能時，repository 根本不該注入 `IQueryCache`**——這是 `docs/17` §4
「五類禁用的 repository 根本不注入快取服務」那條硬規則在本檔案結構上的落點。

`ClubResolver` 已示範用法（`clubs` 主檔查詢走 `cache.GetOrCreateAsync(...)`），
key 命名含 club 代碼維度（`club-id:{code}`），呼應 `docs/17` §4「key 命名必須含 club_id 與 locale 維度」。

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

### 追加：`matches.match_no`（2026-09-21）

`db/club-schema.sql` 補上 `matches.match_no`（聯賽官方場次編號）後，`MatchDto`／`MatchesRepository`／
`MatchesEndpoints` 同步補上這個欄位（明確 SELECT，非 `SELECT *`；沿用既有 `ClubScope` 機制，
沒有另外開任何繞過路徑）。驗證：

```
$ curl "/api/v1/tcrfc/schedule?pageSize=200" | jq '[.items[] | select(.matchNo == null)] | length'
0   # 21 筆全部帶值，與 site/src/data/schedule.json 的 match_no 逐筆核對一致
```

---

## 相關文件

- [`docs/12-database-schema.md`](../../docs/12-database-schema.md)／[`12a`](../../docs/12a-database-erd.md)／[`12b`](../../docs/12b-database-tables.md)／[`12c`](../../docs/12c-i18n-tables.md) — 資料表設計、權限模型、受限欄位、i18n 側表
- [`docs/14-invariants.md`](../../docs/14-invariants.md) — `club_id` 維度、跨庫 JOIN 陷阱、不得讀快取清單
- [`docs/17-deployment.md`](../../docs/17-deployment.md) §0／§1／§4／§6 — 技術選型、容器佈局、快取策略、本機開發資料庫
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) E-19／E-20 — 本次開發踩的坑
- [`db/seed/README.md`](../../db/seed/README.md) — 本機資料庫怎麼連、種了哪些資料、已知落差
- [`apps/web/README.md`](../web/README.md) — 這支 API 的呼叫端（Nuxt 前台骨架）
