# apps/api — api（單一 .NET 行程，唯讀讀取 API）

**S0-7b（2026-09-21，`backend-engineer`）**：讓 Nuxt 前台（[`apps/web`](../web/README.md)）能打真實資料庫，
建立球員、教練與職員、新聞、賽程與賽果、俱樂部主檔五組**唯讀** GET 端點。

**S0-7d（2026-09-21，`backend-engineer`）**：補齊 S0-7b 刻意縮減的三個缺口——① `/readyz` 加上慈善庫與
Redis 檢查 ② `Caching/IQueryCache.cs` 接縫接上真正的 Redis 實作 ③ 建立 `Tcrfc.Api.Tests` 自動化測試專案
（S0-7b 為止零測試）。細節見下方各段落與「S0-7d 驗收紀錄」。

🔴 **不含**：登入與權限、商店與金流、除新聞以外的後台模組、慈善捐款平台的業務功能（S0-7d 對慈善庫
只做「開一條連線探活」，不做任何查詢，見下方「`/readyz` 範圍」）。這些留給後續任務，屆時會用到
完整權限模型（[`docs/12b`](../../docs/12b-database-tables.md) §7）。

---

**本輪（新聞 B2 寫入垂直切片，2026-09-22，`backend-engineer`）**：讓 `apps/admin` 能從前端假資料
改接真實資料庫，第一步先做**新聞（B2）**——刻意只做這一個模組，因為它是目前唯一有真實後台畫面
（`apps/admin/src/views/news/`）的模組，做完才有畫面可以驗。新增：

1. **EF Core 一次性 handoff**（`docs/20-cicd.md` §5）：對已用 `db/club-schema.sql` 建好的
   `tcrfc_club_dev` 跑 `dotnet ef dbcontext scaffold`，產出 `Data/ClubDbContext.cs` ＋
   `Data/EfEntities/`（138 個實體，對應全部 145 張表），並建立標記為已套用的空白基準 migration
   `InitialBaseline`（`Data/Migrations/`）——**沒有對資料庫真的跑任何 DDL**，只在
   `__EFMigrationsHistory` 插入一筆紀錄。細節與怎麼重做見下方「EF Core 一次性 handoff」。
2. **後台新聞寫入端點**（`Features/AdminNews/`）＋**後台新聞讀取端點**（同一組檔案，補
   `STATUS.md` S0-12 提到的缺口：既有五組 GET 只回已發布內容，後台驗證不到草稿／排程／已停用）。
3. **寫入端點開發模式開關**（`Security/DevWriteGate.cs`）：🔴 這組端點在接上登入與權限之前
   **不得在任何對外環境啟用**，見下方「寫入端點開發模式開關」整節。
4. ⚠️ **讀取仍是 Dapper（既有五組 GET／repository 一行未動，除 `ClubScopingTests`／
   `CacheBehaviorTests` 兩個因為藍鯨真實資料到位而過期的斷言外，見下方「發現的既有落差」），
   寫入改走 EF Core**——docs/17-deployment.md §0 的技術選型「EF Core ＋ Dapper」本輪首次真正落地。

🔴 **2026-09-21（`deployment-engineer`）：本機開發資料庫合併進既有的 `sqlserver` 容器**，
`docker-compose.dev.yml` 的 `mssql-dev` 服務已退場（見 [`deploy/README.md`](../../deploy/README.md)）。
**下方所有連線範例已改為新位址**（容器內連 `host.docker.internal,1433`、宿主機直連
`127.0.0.1,1433`）；文中提到「本機 `mssql-dev`」的地方是**當時（S0-7b／S0-7d）的驗證紀錄**，
如實保留不回頭改寫歷史，但那個服務現在已不存在，照著跑之前請先看這一段與 `deploy/README.md`。

---

**本輪（圖片上傳共用元件，2026-09-22，`backend-engineer`）**：`STATUS.md` S0-8——後台每一個表單
的圖片欄位都會用到的共用後端元件，逐字對應規劃書 §4.0「後台圖片上傳通則」（v3.9）。新增：

1. **`Images/`**：純轉檔邏輯（`ImageProcessor`，不碰任何 I/O）＋物件儲存接縫（`IImageStorageService`，
   Azure Blob Storage 實作 `BlobImageStorageService`，本機開發接 Azurite）。伺服器端一律重新編碼、
   去除全部中繼資料（含 EXIF GPS）、長邊超過 2560px 等比縮小、固定產 1280／640／320／160 四個衍生檔，
   全部 WebP。
2. **`Features/Uploads/`**：一個通用上傳端點（`POST /api/v1/admin/{club}/uploads/images/{entityType}/{entityId}/{field}`），
   跟 `Features/AdminNews` 一樣掛在 `DevWriteGate` 後面。**這是共用元件，不是新聞專用**——見下方
   「圖片上傳共用元件」整節的完整契約。
3. **接線示範**：`Features/AdminNews/AdminArticlesRepository` 換圖（`UpdateAsync`）與刪除文章
   （`DeleteAsync`）時呼叫 `IImageStorageService.DeleteAsync` 清掉舊物件——這是任務指示「用一個真實
   模組示範接線」的落點，其他模組（球員照片、贊助商標誌……）之後照抄同一個模式即可，見下方
   `UploadSlotPolicy` 的說明。

---

🔴🔴🔴 **本輪修正（上傳時機違反規劃書，2026-09-22，`backend-engineer`）**：上一輪把上傳做成
「先呼叫獨立端點拿物件鍵、表單儲存時再把鍵塞進純 JSON 建立／更新請求」的兩段式設計，README
當時也如實記錄了這是「本輪的判斷，不是規劃書明文」。**這個判斷是錯的**——規劃書第 53 行與
第 972 行明文「選檔不上傳、儲存才上傳……離開或取消表單不留下任何檔案」，兩段式設計下「選檔」
那一刻檔案就已經真的寫進物件儲存，違反這條規則。使用者拍板：改成符合規劃書，不是回頭改規格。
本輪異動：

1. **`Features/AdminNews` 的建立／更新端點改成單一 `multipart/form-data` 請求**（封面圖片跟
   其餘欄位一起送出），新增 `AdminArticleRequestForm.cs`（解析 `payload`＋`file` 兩個表單欄位）、
   `CoverKeyUpdate.cs`（封面圖片三態：維持不變／清空／換新值）。`CreateArticleRequest` 拿掉
   `CoverKey` 欄位、`UpdateArticleRequest` 拿掉 `CoverKey` 改成 `RemoveCover`（布林）。
2. **整支移除 `Features/Uploads` 的獨立上傳端點**（`UploadsEndpoints.cs`／`UploadDtos.cs`，
   `Program.cs` 不再呼叫 `MapUploadsEndpoints`）——那個端點的存在本身就是「選檔即上傳」兩段式
   設計的載體，留著就是留一個讓下一個模組複製同一個錯誤的樣板。`UploadSlotPolicy.cs`（欄位插槽
   允許清單）留下來，改成由每個模組自己的建立／更新端點在同一次 multipart 請求裡直接呼叫。
3. **失敗時的補償交易**：圖片已經上傳成功、但資料列寫入失敗（分類不存在、slug 重複、樂觀並行
   衝突……）時，端點會刪掉剛剛上傳的物件，不留下孤兒物件——見下方「圖片上傳共用元件」整節與
   `AdminNewsCoverUploadTests` 的自動化測試。
4. **這是一筆規格違反，已記入 [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-34`
   升級段**（根因不是實作本身，是判斷被當成既定契約往下傳，見該檔案說明）。

✅ **`apps/admin` 已跟進（2026-09-22）**：`ImageUploader.vue` 已改成受控元件（選檔只預覽、檔案留記憶體），由 `NewsEditView.vue` 在按「儲存」時組 multipart 一起送。下一棒
`frontend-architect` 要照這份新契約重做，見下方「給前端接的契約」整節。

---

## 目錄結構

```
apps/api/
├── Program.cs                     # DI 註冊、middleware 管線、路由掛載、寫入端點開發模式開關
├── Tcrfc.Api.csproj                # net10.0，Dapper + Microsoft.Data.SqlClient + AspNetCore.OpenApi
│                                    #   + EF Core SqlServer/Design（本輪新增，只用在寫入與 migration）
├── Data/
│   ├── IClubSqlConnectionFactory.cs / ClubSqlConnectionFactory.cs   # 唯一的主站庫連線來源（Dapper 用）
│   ├── ClubOrSharedSql.cs         # 「俱樂部專屬優先、回退共同」SQL 片段的唯一真實來源
│   ├── ClubDbContext.cs           # 🔴 產生檔（dotnet ef dbcontext scaffold），不要手改
│   ├── ClubDbContextCustomizations.cs   # 客製化掛進 OnModelCreatingPartial（本輪：並行權杖設定）
│   ├── EfEntities/                # 🔴 產生檔，138 個實體類別，對應 145 張表（含本輪新增的
│   │                                #   __EFMigrationsHistory）。programs 表改名 TrainingProgram，
│   │                                #   見下方「EF Core 一次性 handoff」的命名衝突說明
│   └── Migrations/                # InitialBaseline：Up()/Down() 刻意留空，見下方說明
├── Security/
│   ├── ClubScope.cs               # 已驗證的俱樂部範圍，建構子 internal，繞不過去
│   ├── IClubResolver.cs / ClubResolver.cs   # 唯一能建立 ClubScope 的地方
│   ├── ClubNotFoundException.cs
│   ├── DevWriteGate.cs            # 🔴🔴🔴 本輪新增：寫入端點的唯一總開關
│   └── IDevOperatorResolver.cs / DevOperatorResolver.cs   # 🔴 本輪新增：不是身分驗證，見檔案內說明
├── Localization/
│   └── RequestLocale.cs           # zh/en ↔ zh-Hant/en 轉換與回退規則
├── Caching/
│   ├── IQueryCache.cs             # 快取接縫（entity/club/locale/qualifier 四維度 key＋InvalidateAsync）
│   ├── NoOpQueryCache.cs          # REDIS_HOST 未設定時的實作
│   └── RedisQueryCache.cs         # REDIS_HOST 有設定時的實作（S0-7d，fail-open／版本號失效／TTL／single-flight）
├── Common/
│   ├── PagedResult.cs / PagingQuery.cs
│   ├── ApiExceptionHandler.cs     # 統一例外處理，不外流資料庫例外訊息；本輪擴充 AdminArticleException 家族
│   └── HealthEndpoints.cs         # /healthz、/readyz（S0-7d 起含慈善庫與 Redis 檢查）
├── Images/                         # 🔴🔴🔴 S0-8 本輪新增：圖片上傳共用元件
│   ├── ImageUploadOptions.cs       #   數字常數（10MB／2560／1280,640,320／160／WebP 品質）唯一來源
│   ├── ImageProcessor.cs           #   純轉檔邏輯：格式驗證→轉正→去中繼資料→縮放→WebP 編碼，不碰 I/O
│   ├── ImageObjectKey.cs           #   衍生檔物件鍵推導規則（由主鍵算出，不另存欄位）
│   ├── ProcessedImageSet.cs        #   ImageProcessor 的輸出型別
│   ├── ImageProcessingExceptions.cs#   驗證失敗例外家族（400，日常中文訊息）
│   ├── IImageStorageService.cs     #   儲存層接縫：UploadAsync／DeleteAsync
│   ├── BlobImageStorageService.cs  #   Azure Blob Storage 實作（本機開發接 Azurite）
│   └── UnavailableImageStorageService.cs  # AZURE_BLOB_CONNECTION_STRING 未設定時的替身
└── Features/
    ├── Clubs/      (ClubDto, ClubsRepository, ClubsEndpoints)
    ├── Players/    (PlayerDto, PlayersRepository, PlayersEndpoints)
    ├── Staff/      (StaffDto, StaffRepository, StaffEndpoints)
    ├── News/       (ArticleListItemDto/ArticleDetailDto, ArticlesRepository, ArticlesEndpoints)   # 唯讀，Dapper，未改
    ├── Schedule/   (MatchDto, MatchesRepository, MatchesEndpoints)
    ├── AdminNews/  # 🔴🔴🔴 開發模式限定：AdminArticleDtos／AdminArticlesRepository（EF Core）／
    │                #   AdminArticlesEndpoints／AdminArticleExceptions。
    │                #   🔴 S0-8 修正（2026-09-22）新增：AdminArticleRequestForm（解析
    │                #   multipart/form-data 的 payload＋file 兩個欄位）、CoverKeyUpdate
    │                #   （封面圖片三態）。建立／更新端點在同一次請求裡處理封面圖片上傳與
    │                #   失敗回滾，不再呼叫任何獨立的上傳端點。
    └── Uploads/    # 🔴🔴🔴 S0-8 本輪新增：UploadSlotPolicy（欄位插槽允許清單，仿
                     #   AdminNews/SlugPolicy.cs 的形狀）。⚠️ S0-8 修正（2026-09-22）：這裡原本
                     #   還有一個獨立的上傳端點（UploadsEndpoints／UploadDtos），因為違反規劃書
                     #   「選檔不上傳、儲存才上傳」已整支移除——現在只剩這份允許清單，由每個
                     #   模組自己的建立／更新端點直接呼叫，不再對外開路由。

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
| `ENABLE_UNSAFE_DEV_WRITES` | 🔴 開發用，⛔ 正式環境不得設定 | 本輪新增。要同時滿足 `ASPNETCORE_ENVIRONMENT=Development` **且**這個值字面等於 `"true"`，`Features/AdminNews` 的寫入與後台讀取端點才會被註冊。見下方「寫入端點開發模式開關」整節 |
| `AZURE_BLOB_CONNECTION_STRING` | 選填（S0-8） | 圖片上傳共用元件的物件儲存連線字串。**未設定不會讓服務無法啟動**（跟 `CLUB_SQL_CONNECTION_STRING` 不同）——只有真的呼叫圖片上傳／刪除時才會需要它，沒設定時注入 `UnavailableImageStorageService`（上傳丟出訊息清楚的例外，刪除安靜略過）。本機開發見下方「本機開發：Azurite」，正式環境見 VM 上 `/opt/tcrfc/secrets/club.env` |
| `AZURE_BLOB_CONTAINER_IMAGES` | 選填（S0-8） | 圖片物件儲存的容器名稱，預設 `images` |

⛔ **S0-8 之後仍完全不碰 LINE Pay、JWT**——這些鍵名雖然已經在 `docker-compose.yml` 的
`api` 服務與 `deploy/dev/{club,charity}.env` 裡預留，但本檔的程式碼**沒有讀取它們**，留給接下來實作那些功能的
session 使用。**Blob 已在本輪接上**，見下方「圖片上傳共用元件」整節。

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
| 🔴 `GET /api/v1/admin/{club}/news` | **開發模式限定**（下方整節）。後台新聞清單，**含全部狀態**（草稿／排程／已發布） | `status`、`category`、`keyword`（搜中文標題）、`page`、`pageSize`（預設 20，上限 100） |
| 🔴 `GET /api/v1/admin/{club}/news/{id}` | 後台單篇詳情（依 GUID，不是 slug），雙語內容不回退，原封回傳 | — |
| 🔴 `POST /api/v1/admin/{club}/news` | 建立文章，一律從 `draft` 開始 | — |
| 🔴 `PUT /api/v1/admin/{club}/news/{id}` | 整份取代可編輯內容，不改狀態（樂觀並行） | — |
| 🔴 `POST /api/v1/admin/{club}/news/{id}/publish` | `draft`／`scheduled` → `published`，立即生效 | — |
| 🔴 `POST /api/v1/admin/{club}/news/{id}/schedule` | `draft`／`scheduled` → `scheduled`（未來時間） | — |
| 🔴 `DELETE /api/v1/admin/{club}/news/{id}` | 刪除（樂觀並行） | `expectedUpdatedAt`（必填，ISO 8601） |
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

## 🔴🔴🔴 寫入端點開發模式開關

專案還沒有登入與權限。**沒有權限把關的寫入端點如果被部署出去，就是任何人都能改資料庫**。
`Features/AdminNews` 的所有端點（含後台專用的讀取端點）因此掛在一個明確、預設關閉的開關後面：

```csharp
// Security/DevWriteGate.cs
public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    => environment.IsDevelopment()
       && string.Equals(configuration[EnableFlagKey], "true", StringComparison.OrdinalIgnoreCase);
```

`Program.cs` 只在這個判斷式回傳 `true` 時才呼叫 `app.MapAdminNewsEndpoints()`。兩個條件缺一都算關閉：

1. `ASPNETCORE_ENVIRONMENT=Development`——正式環境的 `docker-compose.yml`／`docs/20-cicd.md` §7.2
   一律設 `Production`，就算忘記設下面那個旗標，光是環境判斷這關就先擋住。
2. `ENABLE_UNSAFE_DEV_WRITES=true`——刻意用「unsafe」命名，且要求字面等於 `"true"`（不是「有設定
   就算」），降低「複製一份 `.env` 忘記砍掉」被誤帶到正式環境的機率。

**關閉時的行為是路由完全不註冊，回應是 404，不是 403**——不透露「這裡本來有一組寫入端點」這件事。
已用三種方式實測（見下方「驗收紀錄」的完整輸出）：
- `dotnet run`（`ASPNETCORE_ENVIRONMENT=Development`）不設 `ENABLE_UNSAFE_DEV_WRITES` → 404。
- 同一個 `dotnet run` 設了 `ENABLE_UNSAFE_DEV_WRITES=true` → 端點出現，且啟動時印一則
  `LogWarning` 級別的警告（訊息裡再講一次「不得在任何對外環境開啟」）。
- **容器層級**（`ASPNETCORE_ENVIRONMENT=Production`，模擬正式部署，即使沒設
  `ENABLE_UNSAFE_DEV_WRITES` 也一樣）→ 404，這是兩層防護裡更重要的那一層，因為正式環境的
  `ASPNETCORE_ENVIRONMENT` 本來就固定是 `Production`。

`created_by`／`updated_by` 這兩個稽核欄位由 `Security/IDevOperatorResolver.cs`
（`DevOperatorResolver` 唯一實作）從請求標頭 `X-Dev-Operator-Id` 解析：

> 🔴 **這不是身分驗證。** 沒有任何簽章、沒有 session，呼叫端說是誰就是誰。標頭值會先驗證在
> `admin_users` 表真的存在（那兩個欄位的 FK 約束要求指向真實列），驗不到就回傳 `null`
> （欄位本來就允許 NULL），不會讓外鍵違反炸成 500。**`admin_users` 目前是空表**（登入系統還沒做），
> 所以現況下不管標頭給什麼值，`created_by`／`updated_by` 實際上都會是 `null`——這是正確、預期的
> 行為，不是本輪沒做完。等登入系統做出來、`admin_users` 真的有資料時，這個機制不用改就能接上。

---

## EF Core 一次性 handoff（本輪完成，`docs/20-cicd.md` §5）

`docs/20-cicd.md` §5 明訂：`api` 專案第一次要寫入時，對**已經用 `db/*.sql` 建好的資料庫**跑
`dotnet ef dbcontext scaffold` 產出 Entity，並建立一個**標記為已套用的空白基準 migration**
（`InitialBaseline`），不實際重跑 DDL；之後才進入「每次改動都是一個新 migration」的常態。

### 怎麼做的

```bash
# 1. Scaffold：對本機 tcrfc_club_dev 反向工程出 DbContext 與 138 個實體類別
cd apps/api
dotnet ef dbcontext scaffold "$CLUB_SQL_CONNECTION_STRING" Microsoft.EntityFrameworkCore.SqlServer \
  --context ClubDbContext --context-dir Data --output-dir Data/EfEntities \
  --namespace Tcrfc.Api.Data.EfEntities --context-namespace Tcrfc.Api.Data \
  --no-onconfiguring --force

# 2. 建立基準 migration（此時 Up()/Down() 還是「建立全部 145 張表」的完整 DDL）
dotnet ef migrations add InitialBaseline --context ClubDbContext \
  -o Data/Migrations --namespace Tcrfc.Api.Data.Migrations

# 3. 手動清空 Up()/Down() 的內容（只留註解），Designer.cs 的模型快照原封不動保留
#    （這一步刻意不用工具自動化，直接編輯產生的 .cs 檔——見下方檔案內容）

# 4. 套用：因為 Up() 是空的，這裡只會在 __EFMigrationsHistory 插入一筆紀錄，不會真的動任何 DDL
dotnet ef database update --context ClubDbContext
```

**套用前後的資料庫表數比對**（實跑紀錄，見下方「驗收紀錄」有完整輸出）：套用前 `sys.tables`
144 張、`articles` 83 筆；套用後 145 張（只多了 `__EFMigrationsHistory`）、`articles` 仍是 83 筆。

### 🔴 命名衝突：`programs` 表撞到 ASP.NET Core 的頂層 `Program` 類別

Scaffold 完 `dotnet build` 直接炸掉一百多個 `CS1061`，根因是 P1「課程與活動」模組的 `programs` 表
被 scaffold 成類別 `Program`，跟 `Program.cs` 頂層陳述式產生的 `global::Program`（也是這個組件裡
`WebApplicationFactory<Program>` 測試要用到的那個類別）同名。C# 名稱解析規則下，**全域命名空間的
型別比 `using` 匯入的型別優先**，所以 `ClubDbContext.cs` 裡所有 `modelBuilder.Entity<Program>(...)`
都被誤解析成頂層那個空的 `Program`。**處理方式**：把這一個實體類別（及所有參照它的巡覽屬性）
改名成 `TrainingProgram`（`programs` 表在後台叫「課程項目」，這個名字語意上也更貼切），
只改型別名稱，不改資料表名、不改任何欄位對應。**之後重新 scaffold 會再產生一次 `Program.cs`
這個檔名**，要記得重做這個改名——這是產生流程的已知步驟，不是一次性修好就沒事。

### 之後怎麼加真正會執行 DDL 的 migration（下一位接手者看這裡）

```bash
# 1. 先改 db/club-schema.sql（走 docs/12 的同步鏈，CLAUDE.md 第 2、3 條）
# 2. 手動對本機資料庫套用那個 DDL 異動（或整個重建本機庫）
# 3. 重新 scaffold（見上面第 1 步），這次會抓到新綱要
# 4. dotnet ef migrations add <描述性名稱> --context ClubDbContext -o Data/Migrations
#    這次不用清空 Up()/Down()——這是真正要執行的變更，讓它照常產生 DDL
# 5. 正式環境套用走 docs/20-cicd.md 的 db-migrate.yml（需要 production-db 環境核准），
#    不是在本機對正式庫下 dotnet ef database update
```

⚠️ **本輪只做了 handoff 本身，沒有新增任何真正的結構異動**——`InitialBaseline` 是唯一一個
migration，Up()/Down() 都是空的，且已用「套用前後表數不變」實測驗證過。

---

## 後台新聞（B2）寫入垂直切片

`Features/AdminNews/` 是這一輪的主體，也是之後其他模組（球員、教練、賽程、商店……）抄的樣板。
四件套：`AdminArticleDtos.cs`（請求／回應）、`AdminArticlesRepository.cs`（EF Core 寫入邏輯）、
`AdminArticlesEndpoints.cs`（路由）、`AdminArticleExceptions.cs`（業務例外，集中在
`Common/ApiExceptionHandler.cs` 轉狀態碼）。

### 通則（其他模組照抄的部分）

| 關注點 | 落點 |
|---|---|
| **俱樂部範圍** | 沿用既有 `IClubResolver`／`ClubScope`（跟五組唯讀端點同一套，型別系統擋，不是 code review 記住）。寫入路徑另外用 `AdminArticlesRepository.LoadTrackedForWriteAsync` 統一擋「共用內容」與「跨俱樂部」兩種情況 |
| **共用內容唯讀** | `club_id IS NULL` 的文章：讀（`GetByIdAsync`）允許，**寫（Update／Delete）一律 403**（`SharedArticleReadOnlyException`）。目前**沒有任何管道能透過這組端點建立共用內容**——`CreateAsync` 寫死 `ClubId = scope.ClubId`，因為「共同內容只有超管能建立」（docs/14-invariants.md），而超管角色還不存在，這裡選擇完全不開這條路，不留半套的後門 |
| **樂觀並行控制** | `articles.updated_at` 當並行權杖，用 EF Core `IsConcurrencyToken()`（`Data/ClubDbContextCustomizations.cs`）——寫入前把追蹤實體的「原始值」設成呼叫端宣稱看到的 `updatedAt`，`SaveChanges` 產生的 SQL 帶 `WHERE updated_at = @原始值`，0 筆命中就丟 `DbUpdateConcurrencyException`，`AdminArticlesRepository.SaveWithConcurrencyHandlingAsync` 接住轉成 `ArticleConcurrencyConflictException`（409）。⛔ 沒有「後寫的贏」 |
| **slug 重複** | 建立與更新前先查 `SELECT ... WHERE slug = @Slug`（更新時排除自己），撞到就丟 `ArticleSlugConflictException`（409），回可讀訊息，不是讓 SQL Server 的 `UQ_articles_slug` 唯一鍵違反直接冒出 `SqlException` |
| **slug 格式與保留字（本輪新增）** | 建立與更新前先呼叫 `SlugPolicy.Validate`（400，`AdminArticleValidationException`）。見下方「網址名稱（slug）保留字與格式驗證」整節 |
| **雙語側表** | `AdminArticleContentInput.Zh` 必填（`Title` 不得空白，400），`En` 可省略。**PUT 是整份取代語意**：省略 `en`＝清掉既有英文列（不是「沒帶就維持原樣」），已用自動化測試涵蓋兩個方向 |
| **狀態轉換** | 獨立端點（`/publish`、`/schedule`），不是 PUT 的一個欄位。轉換規則見下方「三態轉換規則（本輪判斷）」 |
| **快取失效** | 寫入成功（`SaveChangesAsync` 交易完成）後呼叫 `IQueryCache.InvalidateAsync("articles", scope.ClubCode)` 與 `InvalidateAsync("article-detail", scope.ClubCode)`，跟公開讀取 API 用同一組 entity 名稱，讓下一次公開讀取立刻回新值。用真正的 `redis-server` 測過（見下方測試段落） |

### 網址名稱（slug）保留字與格式驗證（本輪新增，`Features/AdminNews/SlugPolicy.cs`）

使用者拍板新聞詳情頁的網址是扁平的 `/zh/news/<網址名稱>/`（跟商品詳情同形狀）。07 新聞單元底下
另外已有 **9 個分類 landing 頁**（`apps/web/app/pages/zh/news/{club,match,academy,player-stories,
international,camps-events,community,media,article}.vue`）。一篇文章的網址名稱如果（不分大小寫）
剛好等於其中一個，前台路由就會跟分類頁撞在一起——使用者點進去的會是分類清單，不是文章。
`SlugPolicy.Validate` 在建立（`CreateAsync`）與更新（`UpdateAsync`）兩條路徑最前面呼叫，
擋到就丟 `AdminArticleValidationException`（400，中文可讀訊息，不含 `slug` 這種英文技術詞，
docs/06 §1）。

**擋兩類東西**：

1. **保留字**（上述 9 個，不分大小寫）。
2. **格式**：只准小寫英文字母、數字、連字號組成，不能開頭／結尾是連字號、不能連續兩個連字號、
   不能整段只有數字。這一條規則同時擋掉了任務指示要求考慮的幾種危險形狀——**含斜線、含句點、
   含空白、大小寫變形、純數字**——不需要為每一種各寫一條規則；大小寫變形因此**多數情況下是被
   格式規則擋下（不是保留字比對），但結果一律是 400，不影響效果**。已用 83 篇既有文章的真實
   slug 逐筆核對過，**全部符合這條格式規則**（見下方「既有資料核對」），所以套用不會影響現有內容。

**🔴🔴🔴 保留字清單放哪裡、怎麼維護（這是本輪要自己判斷的重點）**：

清單的**真實來源其實不是這個檔案**，而是 `apps/web` 的路由檔名——今天 `apps/web` 只要新增一個
07 單元的分類頁，或替既有分類頁改檔名，`SlugPolicy.cs` 裡手動抄的這份清單就會悄悄過期，而且
過期的方向永遠是「漏擋」（新分類頁沒被列進來），不會有任何編譯錯誤、測試失敗或執行期例外提醒
維護者去補。這正是 [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-36`
「共用真實來源分裂成兩份」的同一種形狀。

本輪**只做到**：把清單集中在單一檔案（`SlugPolicy.cs`）、把來源路徑與盤點日期寫死在該檔案的
XML 文件註解裡，讓下一個改 `apps/web` 路由的人至少有機會搜到這裡。**⛔ 沒有做**跨專案的自動比對
（例如讓 CI 讀 `apps/web` 的實際路由檔名去驗證這份清單是否過期）——一來 `apps/web` 本輪由另一個
agent 在改，任務邊界不允許本輪觸碰；二來這需要「A 專案的檔案異動觸發 B 專案檢查」這種
`docs/20-cicd.md` 目前還沒有的建置機制，屬於「需要跨專案改動才能真正解決」的情況，依任務邊界
只回報建議、不動手發明。

**回報給下一位／使用者的建議**（根治漂移的做法，其中一種，需要同時改 `apps/web`，本輪不做）：

1. 把 9 個分類代碼與其路由片段抽成一份兩邊都讀的共用資料。這 9 個路由片段本來就跟
   `article_categories.code`（7.1–7.8 分類代碼）同名，是本來就存在的同一份事實——`SlugPolicy.cs`
   目前是重複硬編碼一份而不是查表，長期應該讓保留字清單直接查 `article_categories.code`
   （這樣分類本身的異動至少會自動反映到保留字清單，不會漏掉「改分類代碼」這一種漂移；但「新增一個
   不是分類、純粹是路由層級的頁面」這種漂移仍然擋不住，因為那不是資料庫裡的事實），或建一份
   repo 根目錄的共用設定檔（例如 `content/news-category-slugs.json`），`apps/web` 的路由設定與
   這裡都改成讀它。
2. 或在 CI 加一道檢查：`apps/web` 的 07 單元路由檔名異動時，比對這個檔案的清單是否同步，
   不一致就讓 CI 失敗（跟 `docs/18` `E-35`／`E-36` 已經記錄的「共用真實來源要有跨專案檢查」
   同一個精神）。

**既有資料核對**（實跑，2026-09-22）：對本機 `tcrfc_club_dev` 的 83 篇文章逐筆核對，
**0 筆違反保留字清單、0 筆違反新格式規則**（含大小寫、斜線、句點、空白、純數字全部核對過）。
83 篇全部是「日期開頭＋分類詞＋流水號」的形狀（例如 `2024-12-18-club-079`），分類詞出現在中段
不是整段等於保留字，不受影響。

### 🔴 這一輪不做的部分（沒有畫面可驗，刻意不做）

- **標籤（`article_tags`）、關聯（`article_relations`）**：規劃書 B2 有提到，但 `NewsEditView.vue`／
  `NewsListView.vue` 目前**完全沒有對應欄位**，做了也是沒有畫面可驗的端點——跟任務指示「不要順手做
  球員／教練／賽程／商店」是同一個精神，只是套用在同一個模組內的子功能上。`db/club-schema.sql` 已有
  這兩張表，下一輪 `apps/admin` 補上畫面時再一併做。
- **圖片上傳管線**：`coverKey` 只是個欄位（存什麼字串都收），沒有「選檔→縮圖→WebP→去 EXIF」那一整套
  （那是 `S0-8`）。

### 🔴 我的判斷（規劃書沒定義，本輪做了選擇，需要使用者／下一位確認）

1. **置頂精選「限 3」是逐俱樂部算，不是全站算。** 規劃書只寫「置頂精選（限 3）」，沒說是全站
   共用一個上限還是每個俱樂部各自 3 篇。多俱樂部架構下我選了「逐俱樂部」（`EnsureFeaturedCapAsync`
   只算 `club_id = scope.ClubId` 的筆數），理由：這組端點的其他每一條規則（讀、寫、快取失效）
   全部是逐俱樂部隔離的，全站共用一個計數器會是唯一的例外，也會讓藍鯨官網的置頂精選被磐石的
   文章佔滿額度，直覺上不合理。**這是本輪的假設，不是規劃書明文，需要業務判斷確認。**
2. **狀態轉換規則**：`publish`／`schedule` 只接受從 `draft` 或 `scheduled` 出發；已經
   `published` 的文章不能再呼叫 `/schedule`（會 409，理由：把一篇已經上線的文章排到未來，
   等於讓它從公開站消失，這個操作應該要更明確，不該跟「第一次發布前的排程」共用同一個按鈕語意，
   規劃書沒有講這種邊界情況）。`published` 文章仍可以呼叫 `/publish`（视为「立即重新發布」，
   幂等地把 `published_at` 推進到現在——沒有測試涵蓋這個分支的必要性，因為它不影響資料正確性，
   只是把時間戳推近）。**這組轉換規則沒有規劃書依據，是本輪的合理猜測，需要確認。**

### 🔴🔴🔴 排程發布：時間到了，誰把狀態從 `scheduled` 改成 `published`？（回報，沒有動手發明）

實測發現一個貫穿既有 README「排程發布與快取」段落與本輪新程式碼的邏輯落差：

- 既有的公開讀取 `ArticlesRepository`（`Features/News/`，唯讀，Dapper，本輪未改）WHERE 子句是
  `a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())`——
  **兩個條件都要成立**，`status` 必須字面等於 `'published'`。
- 本輪新增的 `/schedule` 端點把文章狀態設成 `'scheduled'`（不是 `'published'`），`published_at`
  設成未來時間，這是 `articles.status` CHECK 約束（`draft`／`published`／`scheduled`）唯一合法
  的做法——不可能既符合「排程中」的業務語意又把 `status` 直接寫成 `published`。
- **後果**：`published_at` 那個未來時間**真的到了之後，這篇文章的 `status` 仍然是 `'scheduled'`**，
  不會自動符合公開 API 的 WHERE 條件，**不會自動出現在公開站上**，除非有某個東西主動把
  `status` 改成 `published`。

**已用 curl 實測驗證這個落差確實存在**（見下方驗收紀錄）：排程一篇文章到未來時間後，
`GET /api/v1/tcrfc/news/{slug}`（公開 API）回 404；即使等到那個時間點過了，只要沒有人呼叫
`/publish`，狀態仍是 `scheduled`，公開 API 仍然 404。

**沒有找到任何規劃書段落定義「誰、什麼時候、用什麼機制」把 `scheduled` 轉成 `published`**
（背景排程器？Azure Function 計時器觸發？後台頁面打開時順便檢查？）。**依任務指示，這裡不自己
發明一個排程器**——這是一個需要業務判斷與架構決策的缺口，回報給使用者／下一位接手者：

- 若要「時間到了自動生效」，需要一個會定期執行的背景工作（例如 Azure Function Timer Trigger，
  或 VM 上的 cron 呼叫一支內部端點），把 `status='scheduled' AND published_at <= now()` 的文章
  批次轉成 `published`，並呼叫 `IQueryCache.InvalidateAsync`。
- 或者，改變既有公開讀取 API 的 WHERE 條件語意，讓 `status IN ('published', 'scheduled')` 都算
  可見（只要 `published_at` 已過）——但這樣「排程中」這個狀態值本身的意義會變得模糊（它就只是
  「有一個未來發布時間的已發布文章」，那 `scheduled` 存在的意義是什麼），且這條路要改既有
  `Features/News/ArticlesRepository.cs`，本輪任務範圍明講「一行都不要動，除非發現真的 bug（發現就
  回報）」——**這就是那個要回報的 bug／缺口**，不是本輪自己去改。

### 已發現、未動手修改的既有落差（回報）

1. **`apps/admin` 的 `ContentStatus` 型別有四態**（`draft`／`scheduled`／`published`／`disabled`，
   `apps/admin/src/types/common.ts`），**但 `articles.status` 的 CHECK 約束只有三態**（沒有
   `disabled`／下架）。`NewsListView.vue` 的下架按鈕、狀態篩選下拉選單在真的接上這組 API 之後
   會有一個選項對不到任何資料庫狀態。⛔ 沒有自己加值域——CHECK 約束要改是規格變更，得先改
   `docs/12` 再走同步鏈。
2. **`NewsArticle`（`apps/admin/src/types/news.ts`）的三個欄位在資料庫找不到對應欄位**：
   `coverImageAlt`（圖片替代文字，雙語）、`noIndex`（不讓搜尋引擎收錄）、`canonicalUrl`（正規網址）。
   `articles`／`articles_i18n` 都沒有這三個欄位。本輪的 DTO 因此**不包含**這三個欄位（寫了也沒地方
   存）。`coverImageAlt` 尤其值得注意：後台圖片上傳通則（規劃書 §4.0 v3.9）明講圖片要有雙語 Alt，
   但 `articles`／`articles_i18n` 的欄位清單裡沒有這個位置——這可能是 `docs/12` 尚未逐張同步的
   13 項落差之一（README 檔頭「v3.0 落差」有提到 `docs/12` §4／§6 明細尚未逐一改寫），也可能是
   真的漏了，需要回頭核對 `docs/12b-database-tables.md` §6 的欄位明細。
3. **`articles.slug` 是全站唯一（`UQ_articles_slug UNIQUE (slug)`），不是 `(club_id, slug)`
   複合唯一**——這點既有 README 段落「`club_id` 強制機制」已經寫過、已經用 curl 實測過，本輪
   只是再次確認：任務指示文字裡寫的「`UNIQUE (club_id, slug)`」跟 `db/club-schema.sql` 第 2309 行
   實際的 DDL 不一致，這裡以資料庫實際的 DDL 為準（已重新用 `grep` 核對過原始檔）。
4. **兩個既有測試假設過期，已修正**（不算本輪的錯，但在本輪執行 `dotnet test` 時才發現）：
   `ClubScopingTests`／`CacheBehaviorTests` 各有一個測試斷言「藍鯨（`bw`）球員數應該是 0」，
   這個假設在 BW-0g（藍鯨舊站資料匯入本機開發資料庫，見 `git log`，與本輪無關的獨立任務）之後
   不再成立——`bw` 現在也有 28 名真實球員。已改成驗證「兩俱樂部球員 id 集合互不重疊」（不論資料
   量怎麼變都能驗證 `club_id` 範圍真的有隔離，見測試檔內註解）。**這件事照 CLAUDE.md 第 13 條
   應該記進 `docs/18-work-errors.md`，但本輪的邊界明講不能碰 `docs/`，這裡先在 README 交代清楚，
   麻煩使用者或下一位轉記一筆。**

---

## 圖片上傳共用元件（S0-8）

規劃書 §4.0「後台圖片上傳通則」（v3.9）的後端落點。**這是全後台每一個表單共用的元件**——不是新聞
模組專屬，`Features/AdminNews` 只是目前唯一有真實後台畫面可以驗收接線的示範。下一棒
`frontend-architect` 接 `apps/admin` 的 `ImageUploader.vue` 時，照這份契約打就好。

### 給前端接的契約（S0-8 修正，2026-09-22：單一請求）

🔴🔴🔴 **這個契約整節改寫過**——舊版是「先呼叫獨立上傳端點拿 key、表單儲存時再把 key 塞進純
JSON 的建立／更新請求」的兩段式設計，因為違反規劃書「選檔不上傳、儲存才上傳」已經整支移除。
新契約：**建立／更新文章時，封面圖片跟其餘欄位在同一次 `multipart/form-data` 請求裡一起送出**。

**端點**（形狀不變，路徑與方法本來就是這兩個）：
- `POST /api/v1/admin/{club}/news`（建立，一律成草稿）
- `PUT /api/v1/admin/{club}/news/{id}`（更新，不改狀態）

（🔴 跟其餘 `Features/AdminNews` 端點一樣掛在 `DevWriteGate` 後面，關閉時 404）

**請求**：`multipart/form-data`，固定兩個欄位：

| 欄位名 | 必填 | 說明 |
|---|---|---|
| `payload` | ✅ | JSON 文字（camelCase），其餘欄位。建立用 `CreateArticleRequest` 的形狀（`slug`／`categoryCode`／`isFeatured`／`content`，**不含封面圖片**）；更新用 `UpdateArticleRequest` 的形狀（同上再加 `expectedUpdatedAt`／`removeCover`） |
| `file` | 選填 | 封面圖片。**建立時**：有夾檔案就上傳成封面，沒夾就是「這篇文章沒有封面圖片」。**更新時**：見下方「封面圖片三態」 |

**封面圖片三態（只影響 PUT）**：呼叫端不會、也不能直接指定物件鍵字串，只能表達「意圖」——

| 這次請求 | 效果 |
|---|---|
| 有夾 `file`，`removeCover` 隨意（會被忽略，見下一列） | 換成新圖：新圖上傳成功才會更新資料列，資料列更新成功後才刪舊物件（換圖與刪除順序不變，見 `AdminArticlesRepository.UpdateAsync`） |
| 沒夾 `file`，`payload.removeCover = true` | 清空封面圖片，並刪掉舊物件 |
| 沒夾 `file`，`payload.removeCover = false`（預設） | 維持目前的封面圖片不變，完全不碰物件儲存 |
| **同時**有夾 `file` **且** `payload.removeCover = true` | ⛔ 視為請求矛盾，回 400（「不能同時上傳新的封面圖片與移除封面圖片，請擇一。」），**不會**嘗試任何上傳 |

**回應**：跟建立／更新端點本來的回應完全一樣（`AdminArticleDetailDto`，含 `coverKey`），**沒有另外的
上傳回應格式**——這是這次修正的重點之一：不再有獨立的「上傳成功」這個中間狀態需要前端自己保管
物件鍵字串，伺服器端把上傳結果直接寫進同一次回應的 `coverKey`。四個衍生檔（1280／640／320／160
方形縮圖）的物件鍵由 `coverKey` 推導（`Images/ImageObjectKey.cs`：把副檔名前插入
`-1280`／`-640`／`-320`／`-thumb`），前端需要顯示縮圖時自己用同一套規則從 `coverKey` 算出來。

**錯誤（一律日常中文，`docs/06-conventions.md` §1）**：

| HTTP | 情境 | `detail` 文案 |
|---|---|---|
| 400 | 請求不是 `multipart/form-data`，或缺少 `payload` 欄位 | 「請求格式錯誤，需要 multipart/form-data（欄位 payload ＋ 選填的 file）。」／「缺少 payload 欄位。」 |
| 400 | `payload` 不是合法 JSON，或缺少必填欄位 | 「payload 欄位不是合法的 JSON，或缺少必填欄位。」 |
| 400 | 更新時同時夾檔案又勾選移除封面 | 「不能同時上傳新的封面圖片與移除封面圖片，請擇一。」 |
| 400 | 夾了檔案但是空檔案 | 「沒有收到圖片檔案，請重新選擇圖片。」 |
| 400 | 夾的檔案超過 10 MB | 「圖片檔案太大（上限 10 MB），請換一張或先壓縮。」 |
| 400 | 格式不支援（含假副檔名、HEIC） | 「圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。」 |
| 404 | `club` 代碼不存在或非啟用 | 沿用既有 `ClubNotFoundException` 文案（**在任何上傳嘗試之前就會擋下**，不會浪費一次上傳） |
| 409 | slug 重複／樂觀並行衝突／狀態轉換不合法／置頂精選已達上限 | 沿用既有文案（見上方各例外類別）。**若這次請求有夾檔案，圖片已經上傳成功但資料列寫入失敗時，伺服器端會自動刪掉剛剛上傳的物件**（補償交易，見下方「失敗回滾」），呼叫端不需要、也不應該自己再呼叫任何清理 |
| 413 | **超過約 11 MB**（見下方「Kestrel 請求主體上限」） | 平台預設的空白 413，不是本服務的 JSON 錯誤格式 |
| 404（路由不存在） | 開發模式開關關閉 | 沒有回應內容，見「寫入端點開發模式開關」 |

### 失敗回滾（補償交易，不是真正的跨系統 atomic transaction）

物件儲存（Azure Blob／Azurite）跟 SQL Server 是兩個獨立系統，做不到兩者要嘛都成功、要嘛都失敗
的真正 atomic transaction。伺服器端用**補償交易**模擬同樣的使用者體感——`AdminArticlesEndpoints`
的建立／更新處理常式裡：

1. 若這次請求有夾 `file`：**先**呼叫 `IImageStorageService.UploadAsync`（寫入物件儲存），
   成功才繼續下一步（規劃書「寫入 blob 成功才更新資料列」）。
2. 呼叫 `AdminArticlesRepository.CreateAsync`／`UpdateAsync` 寫資料列——這一步本身是 EF Core
   單一 `SaveChangesAsync`（單一 SQL transaction，多筆 INSERT／UPDATE 要嘛都成功要嘛都失敗）。
3. **第 2 步丟例外，或（僅更新）回傳 `null`（找不到這篇文章）**：`catch` 區塊／`null` 分支呼叫
   `IImageStorageService.DeleteAsync` 刪掉第 1 步剛剛上傳的物件（主檔＋四個衍生檔），再把原例外
   原樣往上丟／回 404——不留下沒有任何資料列指著它的孤兒物件。

已知的殘留缺口（見下方「已知缺口」第 4 點）：`BlobImageStorageService.UploadAsync` 本身在寫五個
物件（主檔＋四個衍生檔）時是循序寫入，不是單一原子操作——如果寫到一半（例如寫完主檔＋兩個衍生檔）
網路中斷，會留下**部分**衍生檔的孤兒物件，這個更深一層的缺口跟本次「兩段式改單一請求」的修正
無關，本次沒有動手處理（見下方說明）。

### `UploadSlotPolicy`：欄位插槽允許清單（`Features/Uploads/UploadSlotPolicy.cs`）

跟 `Features/AdminNews/SlugPolicy.cs` 同一種形狀：一份集中、有清楚維護說明的允許清單，不是散在
各處各自檢查。🔴🔴🔴 **目前只有一格**：`articles.cover`（對應 `articles.cover_key`，`AdminArticlesRepository`
換圖時的清理也是唯一真的接上的示範）。**這是刻意的最小化**——S0-8 的任務邊界是「共用元件做好＋
一個真實模組示範接線」，不是把後台全部圖片欄位一次接完。**⚠️ S0-8 修正（2026-09-22）：這份清單
不再對應一個獨立的 HTTP 端點**——原本的 `Features/Uploads/UploadsEndpoints.cs` 已整支移除（那是
「選檔即上傳」兩段式設計的載體）。之後其他模組（球員照片 `players.photo_key`、教練職員照片、
贊助商雙色標誌、商品圖集……）真的動工時，各自：

1. 先查 [`docs/12b-database-tables.md`](../../docs/12b-database-tables.md) §6 或
   `db/club-schema.sql` 確認資料表真的有對應的 `_key` 欄位，**不要假設**。
2. 在 `UploadSlotPolicy.AllowedSlots` 加一格 `entityType → { field, ... }`。
3. **把封面／照片欄位併進自己模組的建立／更新端點，比照 `AdminArticlesEndpoints` 的做法**——
   multipart 請求、`payload`＋`file` 兩欄位、上傳成功才寫資料列、失敗回滾——**不要另外開一個
   獨立的上傳端點**，那正是這次要修正的錯誤設計。
4. 在該模組自己的 repository 比照 `AdminArticlesRepository.UpdateAsync`／`DeleteAsync` 的寫法，
   換圖或刪資料列時呼叫 `IImageStorageService.DeleteAsync` 清掉舊物件。

### Kestrel 請求主體上限（本輪新增，`Program.cs`）

全域設定 `MaxRequestBodySize = 10MB + 1MB`（`ImageUploadOptions.MaxUploadBytes` 加一點緩衝給
multipart 邊界字串與其他表單欄位）。⚠️ **這只把「檔案太大」的邊界從 Kestrel 預設的 30MB 下移到
約 11MB，沒有讓所有超過上限的檔案都得到本服務的友善訊息**——實測驗證過兩種情況：

- **10–11 MB 之間**：程式碼裡的 `ImageUploadOptions.MaxUploadBytes` 檢查先跑到，回本服務的 400
  JSON（「圖片檔案太大」）。
- **超過約 11 MB**：Kestrel 在請求主體讀取階段就直接中止連線，回應是**平台內建的空白 413**，
  不會進到本服務的任何程式碼、不會是 JSON 格式、不會有中文訊息。**這是已知且接受的落差**——
  要讓所有超過 10MB 的檔案都得到一致的友善訊息，需要用 `IHttpMaxRequestBodySizeFeature` 搭配
  中介軟體攔截 `BadHttpRequestException` 自己組回應，本輪判斷這個投資報酬率不高（10–11MB 那個窄
  範圍已經涵蓋「使用者選錯檔、稍微超標」的常見情境，真正離譜過大的檔案回一個通用 413 也不算
  太差的使用者體驗），沒有動手做，列為已知缺口。

### 已知缺口（回報，不是自己判斷做或不做）

1. 🔴🔴🔴 **多數帶圖片欄位的資料表沒有 `_width`／`_height`／雙語 `_alt` 欄位**——規劃書 §4.0
   明訂每個圖片欄位是「一組欄位（物件鍵、寬、高、雙語 Alt 文字）」，但 `docs/12d-field-audit.md`
   §11 已經記錄：`db/club-schema.sql` 裡多數 `*_key` 欄位旁邊都沒有 `_width`／`_height`（只有
   `press_resources.cover_width`／`cover_height`、`impact_records.image_width`／`image_height`
   兩張表有），**全庫沒有任何 `_alt` 欄位**。這代表：
   - 本服務的上傳端點回應裡確實有算出真正的 `MainWidth`／`MainHeight`（`ImageProcessor` 的輸出），
     但**呼叫端目前沒有資料庫欄位可以存這兩個值**（`articles.cover_key` 就是唯一的例子——只有
     一個 `_key` 欄位，寬高算出來也沒地方寫回去）。
   - 雙語 Alt 文字完全沒有落點——前台 `<img alt>` 目前只能留空或沿用標題，這是**無障礙缺口**。
   - **這是資料庫綱要的落差，不是本次任務範圍能修的**——要補欄位得先走 `docs/00-harness.md`
     §2.5 同步鏈（改規劃書確認 → 改 `docs/12b` → 改 `db/club-schema.sql` → 重新 scaffold EF Core）。
     **本輪沒有自己加欄位**，只在這裡回報：`docs/12d-field-audit.md` §11 已經有一模一樣的結論，
     這不是新發現，是重新確認同一個已知缺口在圖片上傳管線真的做出來之後確實會卡到。
2. **沒有做「依版位設定尺寸下限」**：規劃書 §4.0「尺寸下限依版位另定……低於下限擋下不收，
   不放大補齊」——這是一個依「這張圖要放在前台哪個版位」而變動的業務規則（例如主視覺類不得小於
   1600px 寬），本服務目前是**通用元件**，沒有這一層「版位」概念，也就沒有實作這個下限檢查。
   呼叫端（各模組自己的前端表單）如果需要，要嘛在前端擋（跟規劃書「兩道驗證」的前端那一道一致），
   要嘛之後在 `UploadSlotPolicy` 的允許清單裡幫每個插槽加一個「最小尺寸」欄位，本輪沒有做，
   因為只有一個插槽（新聞封面圖）在跑，規劃書也沒有明講新聞封面圖的下限是多少。
3. **HEIC 拒絕只用手動測試驗證過，沒有進自動化測試**：`ImageProcessorTests`／`AdminNewsCoverUploadTests`
   涵蓋「假副檔名文字檔」與「截斷的損毀 JPEG」兩種「格式不支援」情境（在任何機器上都能重現，
   不依賴外部工具），但**沒有涵蓋真的 HEIC 檔案**——本機驗證時用 macOS 內建的 `sips` 工具產生了
   一個真正的 HEIC 檔案（`sips -s format heic ...`）並實測確認被擋下（見上方驗收紀錄），但這個
   產生方式是 macOS 專屬，寫進自動化測試會在 CI 的 Linux runner 上跑不動。**兩者的擋法完全相同**
   （格式偵測得出容器格式，但 `ImageSharp` 沒有對應解碼器 → 同一個 `UnsupportedImageFormatException`
   例外路徑），所以自動化測試涵蓋的兩種情境已經證明了同一段程式碼會攔下 HEIC，只是沒有對「HEIC
   本身」這個具體格式跑一次可重複的自動化斷言。
4. **孤兒物件**：🔴 S0-8 修正（2026-09-22）後，「先上傳拿 key、使用者取消表單」這個情境
   **已經不存在**——改成單一 multipart 請求後，使用者不按「儲存」就不會有任何 HTTP 請求送出，
   天然不會有任何物件被寫進儲存體；請求送出但資料列寫入失敗的情境也已經用補償交易回滾（見上方
   「失敗回滾」，`AdminNewsCoverUploadTests` 有自動化測試釘住）。**殘留的孤兒物件來源縮小為兩種**：
   - 換圖或刪除**舊**物件失敗時（`IImageStorageService.DeleteAsync` fail-open 吞例外）——這是
     刻意的取捨（見該方法上的說明：資料庫的新值已經寫入成功，不該讓一個非關鍵的清理步驟讓
     整個請求變成 500），舊物件因此可能永遠留在儲存體裡。
   - `BlobImageStorageService.UploadAsync` 寫五個物件（主檔＋四個衍生檔）本身不是單一原子操作
     （見上方「失敗回滾」末段）——寫到一半失敗會留下部分衍生檔的孤兒物件。這是比本次修正更深一層
     的既有缺口，本輪沒有動手處理（不在「上傳時機」這個修正範圍內，需要另外評估要不要做，
     例如改成先寫暫存前綴、全部成功才「原子性地」讓主鍵可見，或接受現狀）。

   本服務沒有做孤兒物件的定期清理（例如比對資料庫實際引用的 key 集合，反查儲存體多出來的物件），
   這是規劃書 §4.0 明文「不做」的範圍之一（「本通則不做：使用位置追蹤與刪除前警示」），不是漏做。

---

## 開發過程踩的坑（已修正，記錄見 `docs/18-work-errors.md` E-19／E-20／E-3?）

1. **`InvariantGlobalization=true` 讓 `Microsoft.Data.SqlClient` 連線直接炸**（E-19）：
   這個旗標對純 HTTP／JSON 服務通常安全，但只要相依鏈裡有需要定序或編碼轉換的資料庫驅動就是地雷。
   已移除。
2. **Dapper 的 record 具現化對 `DateOnly` 與 SELECT 欄位數要求比預期嚴格**（E-20）：
   SQL `date` 欄位在 ADO.NET 層永遠回報 `DateTime`，record 屬性宣告成 `DateOnly` 會讓 Dapper
   找不到相符的建構子而整支查詢丟 `InvalidOperationException`；另外兩個查詢共用一個 record 型別
   但 SELECT 的欄位數不同，也是同一種例外。已修正（`PlayersRepository`／`MatchesRepository` 的
   內部 row 型別改用 `DateTime`，`Map()` 再轉 `DateOnly`；`ArticlesRepository` 拆出精確對應欄位的型別）。
3. **本輪（S0-8）新踩到：容器「已確保存在」旗標在失敗時也被設成已完成，導致真正的錯誤被
   後續呼叫的誤導性錯誤蓋掉**：`BlobImageStorageService.EnsureContainerAsync` 原本用
   `Interlocked.CompareExchange` 在呼叫 `CreateIfNotExistsAsync` **之前**就把旗標設成「已確保」，
   本機對 Azurite 實測時，第一次呼叫因為 `Azure.Storage.Blobs` SDK 版本比 Azurite 認得的 API
   版本新而失敗（見 README「本機開發：Azurite」），但旗標已經被設成 true；**第二次呼叫因此跳過
   容器建立**，改直接對一個從未真正建立成功的容器寫入，得到的錯誤變成「容器不存在」而不是
   一開始那個真正的 API 版本不相容——排查時得先想到旗標邏輯本身可能有問題，不能只看最新一次的
   錯誤訊息。已修正：改用 `SemaphoreSlim` 包住整段（`CreateIfNotExistsAsync` 之後才設旗標），
   失敗時旗標維持未確保，下一次呼叫會重新嘗試。**這一類「先設完成旗標、再做真正會失敗的動作」
   的寫法本身就是根因**，下次寫類似的「只做一次」快取旗標時要記得旗標必須在動作成功之後才設定。

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

## 驗收紀錄（本輪，2026-09-22，後台新聞寫入垂直切片）

環境：本機既有 `sqlserver` 容器（`tcrfc_club_dev`，`articles` 83 筆，`clubs` 2 筆：`tcrfc`／`bw`）。

### 1. `dotnet build`／`dotnet test` 全過，既有測試不回歸

```
$ cd apps/api && dotnet build
建置成功。0 個警告 0 個錯誤

$ cd Tcrfc.Api.Tests && dotnet build
建置成功。0 個警告 0 個錯誤

$ export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club_dev;User Id=sa;Password=***;TrustServerCertificate=True;"
$ dotnet test
已通過! - 失敗: 0，通過: 48，略過: 0，總計: 48，持續時間: 21 s
# 30 項既有（含本輪修正的 2 項過期斷言，見上方「已發現、未動手修改的既有落差」第 4 點）
# + 18 項本輪新增（AdminNewsGateClosedTests 3、AdminNewsWriteTests 14、AdminNewsCacheInvalidationTests 1）
```

測完直接查資料庫確認測試自己建立的資料都清乾淨、沒有污染種子資料：

```
$ (查 slug LIKE 'admin-write-test-%' OR 'shared-test-%' OR 'cache-invalidate-test-%')
leftover_test_articles: 0
total_articles: 83   # 跟測試前一致
```

### 2. EF Core handoff 實測（見上方整節說明）

```
# 套用基準 migration 前
table_count: 144　ef_history_exists: 0　articles_before: 83

$ dotnet ef database update --context ClubDbContext
Applying migration '20260922070223_InitialBaseline'.
# 觀察到的 SQL 只有：CREATE TABLE __EFMigrationsHistory + INSERT 一筆紀錄，沒有其他 DDL

# 套用後
table_count: 145（只多 __EFMigrationsHistory）　articles_after: 83（不變）
```

### 3. 開發模式開關實測：關 → 404 → 開 → 可用 → 容器層級 Production 下仍是 404

```
# 情境一：dotnet run，Development，不設 ENABLE_UNSAFE_DEV_WRITES
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/admin/tcrfc/news
404
$ curl -o /dev/null -w "%{http_code}\n" -X POST /api/v1/admin/tcrfc/news -d '{...}'
404
$ curl /api/v1/tcrfc/news?pageSize=1   # 既有公開端點不受影響
{"items":[...],"totalCount":83,...}

# 情境二：dotnet run，Development，ENABLE_UNSAFE_DEV_WRITES=true
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/admin/tcrfc/news
200
# 啟動 log 印出：🔴 寫入端點開發模式開關已開啟...切勿在任何對外環境開啟此設定。

# 情境三（更重要）：docker build 出來的映像檔，ASPNETCORE_ENVIRONMENT=Production
#   （模擬正式部署，即使沒設 ENABLE_UNSAFE_DEV_WRITES，正式環境本來就不會設）
$ docker build -t x apps/api   # 成功，Tcrfc.Api.Tests 未被送進 build context（.dockerignore 既有排除）
$ docker run ... -e ASPNETCORE_ENVIRONMENT=Production ...
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/admin/tcrfc/news
404
$ curl /healthz
{"status":"ok"}
```

### 4. 完整生命週期（建立草稿 → 改內容 → 排程 → 發布 → 刪除），每一步查資料庫

```
# 1. 建立草稿
$ curl -X POST /api/v1/admin/tcrfc/news -d '{"slug":"curl-verify-article","categoryCode":"club",...}'
{"id":"69fa9fae-...","status":"draft","updatedAt":"2026-09-22T07:15:13.883",...}
$ SELECT id,slug,status,club_id,created_by,updated_by FROM articles WHERE slug='curl-verify-article'
69FA9FAE-... curl-verify-article draft F7CB2444-...(tcrfc) NULL NULL
$ SELECT locale,title FROM articles_i18n WHERE article_id='69fa9fae-...'
zh-Hant  curl 驗證文章

# 2. 改內容（PUT，帶 X-Dev-Operator-Id 標頭示範，admin_users 是空表故仍為 NULL）
$ curl -X PUT .../news/69fa9fae-... -H "X-Dev-Operator-Id: <random-guid>" -d '{...expectedUpdatedAt:上一步的值...}'
{"status":"draft","coverKey":"articles/2026/09/curl-cover.webp","zh":{"title":"curl 驗證文章（已編輯）"},"en":{"title":"Curl Verify Article (Edited)"},"updatedAt":"2026-09-22T07:15:53.661"}
$ SELECT slug,cover_key,updated_by,updated_at FROM articles WHERE id='69fa9fae-...'
curl-verify-article  articles/2026/09/curl-cover.webp  NULL  2026-09-22 07:15:53.661
$ SELECT locale,title,summary FROM articles_i18n WHERE article_id='69fa9fae-...'
en       Curl Verify Article (Edited)   NULL
zh-Hant  curl 驗證文章（已編輯）          改過的摘要

# 3. 排程發布（未來 30 分鐘）
$ curl -X POST .../news/69fa9fae-.../schedule -d '{"expectedUpdatedAt":"...","publishAt":"<UTC+30min>"}'
{"status":"scheduled","publishedAt":"2026-09-22T07:46:04.227",...}
$ SELECT status,published_at,SYSUTCDATETIME() FROM articles WHERE id='69fa9fae-...'
scheduled  2026-09-22 07:46:04.227  2026-09-22 07:16:04.44（確認 published_at 真的在未來）
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/tcrfc/news/curl-verify-article   # 排程中，公開 API 應該看不到
404   # 🔴 見上方「排程發布：誰改狀態」整節——這個 404 會一直維持到有人呼叫 /publish 為止

# 4. 發布（立即生效）
$ curl -X POST .../news/69fa9fae-.../publish -d '{"expectedUpdatedAt":"..."}'
{"status":"published","publishedAt":"2026-09-22T07:16:14.426",...}
$ SELECT status,published_at FROM articles WHERE id='69fa9fae-...'
published  2026-09-22 07:16:14.426
$ curl /api/v1/tcrfc/news/curl-verify-article   # 公開 API 現在看得到了
{"id":"69fa9fae-...","title":"curl 驗證文章（已編輯）","summary":"改過的摘要",...}

# 5. 刪除
$ curl -X DELETE ".../news/69fa9fae-...?expectedUpdatedAt=<url-encoded>"
HTTP 204
$ SELECT COUNT(*) FROM articles WHERE id='69fa9fae-...'          → 0
$ SELECT COUNT(*) FROM articles_i18n WHERE article_id='69fa9fae-...' → 0（FK ON DELETE CASCADE）
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/tcrfc/news/curl-verify-article        → 404
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/admin/tcrfc/news/69fa9fae-...         → 404
```

### 5. 補充驗證（業務規則）

```
$ curl -X POST .../news -d '{"slug":"2026-08-10-international-000",...}'   # 撞既有文章的 slug
{"title":"網址名稱重複","status":409,"detail":"網址名稱「2026-08-10-international-000」已經被使用，請換一個。"}

$ curl -X POST .../news -d '{"categoryCode":"not-real",...}'
{"title":"輸入內容有誤","status":400,"detail":"找不到分類代碼「not-real」。"}
```

俱樂部範圍（另一俱樂部路由改不到／刪不到）、共用內容唯讀（403）、樂觀並行衝突（409，且確認
「不是後寫的贏」）、三態轉換（已發布不能再排程）、雙語側表整份取代語意、快取失效（真實 Redis，
更新後公開 API 立刻反映新標題）——這幾項改用自動化測試涵蓋（見下方「測試」），不在這裡重複貼
curl，測試本身也是對 `tcrfc_club_dev` 打真正的 HTTP 管線，不是 mock。

### 6. 既有五組 GET 端點回歸比對（同一次跑，前後對照 S0-7d 驗收紀錄的數字）

```
clubs 清單筆數:      2     （不變）
tcrfc players 筆數:  28    （不變）
tcrfc staff 筆數:    8     （不變）
tcrfc news 筆數:     83    （不變，且等於本輪操作完、測試清乾淨之後的筆數）
tcrfc schedule 筆數: 21    （不變）
/readyz:             {"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"not_configured"}
```

---

## 測試（`apps/api/Tcrfc.Api.Tests`，共 96 項）

S0-7b 為止零測試——所有行為保證只存在於本檔的 curl 紀錄裡。S0-7d 新增獨立測試專案
`Tcrfc.Api.Tests`（xUnit 2.9 + `Microsoft.AspNetCore.Mvc.Testing`），對 `Program`
用 `WebApplicationFactory<Program>` 啟動真正的行程內主機，打真正的 HTTP 管線，接真正的
本機 `mssql-dev`——不 mock 資料庫，也不 mock Redis：`RedisUnavailableApiFixture` 用「指向一個
確定沒人聽的本機連接埠」讓失敗是真的失敗；`RedisEnabledApiFixture`（S0-7d 續作新增）直接啟動一個
真正的 `redis-server` 子行程（`brew install redis` 裝的那個二進位檔），驗證 key／TTL／隔離這些
「Redis 正常運作時該長什麼樣」的行為，不是靠讀程式碼推論。**S0-8 沿用同一個原則接上真正的
物件儲存**：`AdminWriteAzuriteEnabledApiFixture` 啟動一個真正的 `azurite-blob` 子行程
（`npm install -g azurite` 裝的那個執行檔），不 mock `Azure.Storage.Blobs`。
🔴 **`99 = 既有 78（零回歸）＋ 本輪新增 21`**（`ImageProcessorTests` 9 項純邏輯單元測試＋
`AdminNewsCoverBlobCleanupTests` 5 項＋`AdminNewsCoverUploadTests` 7 項）。**S0-8 修正
（2026-09-22，上傳時機改單一請求）把原本的 `UploadImagesEndpointTests`（6 項）／
`UploadImagesGateClosedTests`（1 項）整批刪除**——這兩支測試打的是已經整支移除的獨立上傳端點，
不是「測試變少了」，格式／大小／空檔案的驗證覆蓋改搬進 `AdminNewsCoverUploadTests`（透過建立
文章端點觸發同一段驗證邏輯），另外新增了兩支舊契約下不可能寫的測試：「上傳成功但資料列寫入
失敗時回滾、不留孤兒物件」（建立與更新各一支，這正是「取消表單不留下任何檔案」在後端的可驗證
等價形式，見 `apps/api/README.md`「圖片上傳共用元件」§「失敗回滾」）。⚠️ **這個沙盒環境跑 99 項
整套要 3–4 分鐘**（單獨跑
一個測試類別通常在數百毫秒內完成，但 `dotnet test` 的行程啟動與 JIT 暖機開銷在這台開發機上
明顯偏高——本輪驗證時第一次以為是卡死，用 macOS `sample` 對行程取樣後看到主執行緒卡在
`WaitHandle_WaitOneCore`，才確認是「這台機器上等待真的很慢」而不是死鎖，耐心等到最後真的印出
`Passed!` 為止，不要在還沒看到終端摘要前就判斷為卡住而砍掉）。

### 涵蓋範圍

| 測試檔 | 驗證什麼 |
|---|---|
| `ClubScopingTests` | 跨俱樂部球員清單 id 集合互不重疊（2026-09-22 改法，見「已發現、未動手修改的既有落差」第 4 點）、跨俱樂部讀已知 slug 的文章詳情回 404（不是內容）、不存在的俱樂部代碼回 404 |
| `LocalizationFallbackTests` | 語系逐欄位回退（同一筆記錄可以一半英文一半中文）；用 Unicode 範圍判斷中文／英文，不硬編碼種子資料的確切字串 |
| `PagingNormalizationTests` | `page<=0`→1、`pageSize<=0`→端點預設值、超過上限截斷 |
| `SqlInjectionTests` | 惡意 `team` 參數不炸、不回傳資料、資料表安然無恙 |
| `CacheFailOpenTests` | Redis 不可用時，`ClubResolver`＋**五組接了快取的端點**（`clubs`／`players`／`staff`／`news` 列表與單篇／`schedule`）全部仍然成功；`/readyz` 仍回 `ready` 但 `redis` 標示 `degraded`（S0-7d 續作擴大涵蓋範圍，原本只驗證 `players` 一個端點） |
| **`CacheBehaviorTests`（S0-7d 續作新增）** | qualifier 是否真的涵蓋每個會改變結果的參數（`pageSize`／`team`／`category`／`season`／`status`）、同參數兩次結果一致、文章單篇不同 slug 互不污染、**404 不寫入快取**、**用 `IConnectionMultiplexer` 直接核對 key 真的依 club／locale 隔離**（2026-09-22 同上改成 id 集合不重疊）、key 有 TTL 兜底 |
| **`AdminNewsGateClosedTests`（本輪新增）** | 開發模式開關關閉時，後台清單／單篇／建立三個端點一律 404（不是 403），用既有的 `ApiFixture`（不設 `ENABLE_UNSAFE_DEV_WRITES`）驗證 |
| **`AdminNewsWriteTests`（本輪新增）** | 完整生命週期（建立→改內容→排程→發布→刪除）、分類代碼不存在／中文標題空白回 400、slug 重複回 409、俱樂部範圍（另一俱樂部路由更新／刪除回 404 且本尊不變）、共用內容唯讀（更新／刪除回 403，且後台讀取看得到並標記 `isShared`）、樂觀並行控制（過期 `updatedAt` 回 409 且不是後寫的贏）、三態轉換（已發布不能再排程）、排程時間不在未來回 400、雙語側表（加英文／省略英文清空）、置頂精選超過 3 篇回 409 |
| **`AdminNewsCacheInvalidationTests`（本輪新增）** | 用真正的 `redis-server`（`AdminWriteRedisEnabledApiFixture`）驗證：後台更新已發布文章後，公開 API 立刻反映新標題，不是被 TTL 內的舊快取值擋住 |
| **`ImageProcessorTests`（S0-8 新增，純單元測試，不需要任何 fixture）** | 長邊超過 2560px 等比縮小、固定產出 4 個衍生檔（1280／640／320／160 方形縮圖）、全部輸出真的是 WebP、主檔與衍生檔的 EXIF／ICC／IPTC／XMP 全部清除、主檔小於目標尺寸時不放大補齊、接受 PNG／WebP 格式、假副檔名文字檔與損毀 JPEG 檔頭被擋（見 `TestImages.cs`，全部圖片用 ImageSharp 在記憶體現產，不依賴外部檔案，任何機器都能重現） |
| **`AdminNewsCoverBlobCleanupTests`（S0-8 新增，S0-8 修正改寫）** | 圖片上傳共用元件跟 `Features/AdminNews` 實際接線後的行為，改用單一 multipart 請求：建立文章時附封面圖片、五個物件真的寫進儲存體且物件鍵含俱樂部與文章 id；換圖成功後舊的主檔與全部衍生檔被刪除、新的完整保留；`removeCover=true` 清空封面且刪舊物件；沒夾檔案也沒勾選移除時封面維持不變（Keep 語意）；刪除文章後圖片物件一併被刪除 |
| **`AdminNewsCoverUploadTests`（S0-8 修正新增，2026-09-22）** | 🔴 這次修正的核心驗收：格式不支援／空檔案／超過 10MB／俱樂部不存在四種情境透過建立端點觸發，回 400／404 且不留下任何物件；**slug 重複時夾正常圖片**——圖片已上傳成功但建立失敗（409），驗證儲存體物件數量沒有增加（補償交易生效）；**並行衝突時夾正常圖片**——圖片已上傳成功但更新失敗（409），驗證沒有新增物件且舊封面不受影響；同時夾檔案又勾選移除封面回 400 且完全不嘗試上傳 |

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

🔴 **S0-8 起額外需要 `azurite-blob` 執行檔**（`AdminWriteAzuriteEnabledApiFixture` 用）：
`npm install -g azurite`（macOS／Linux 預設裝在 `/usr/local/bin/azurite-blob`，找不到時可用
`AZURITE_EXECUTABLE_PATH` 環境變數指到實際位置），沿用既有「找不到 `redis-server` 就丟清楚例外」
同一種紀律，不悄悄跳過。⚠️ **這個沙盒環境整套 96 項測試實測約需 3–4 分鐘**（本輪驗收時第一次
懷疑卡死，用 `sample <pid>` 對行程取樣看到主執行緒在 `WaitHandle_WaitOneCore` 才確認只是這台機器
的 `dotnet test` 行程啟動開銷偏高，不是死鎖——耐心等到 `Passed!` 摘要列印出來為止）。

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

### 六個 fixture、六種環境設定

- `ApiFixture`：`REDIS_HOST` 清空（強制走 `NoOpQueryCache`），只驗證主站庫相關行為。
  **本輪起也是「開發模式開關關閉」狀態的代表**（不設 `ENABLE_UNSAFE_DEV_WRITES`），
  `AdminNewsGateClosedTests` 就是刻意用這個 fixture，驗證「大多數測試在關閉狀態下跑」。
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
- **`AdminWriteApiFixture`（本輪新增）**：`ENABLE_UNSAFE_DEV_WRITES=true` ＋ `REDIS_HOST` 清空，
  是唯一開啟寫入端點但不驗證快取行為的 fixture，`AdminNewsWriteTests` 用這個。
- **`AdminWriteRedisEnabledApiFixture`（本輪新增）**：`ENABLE_UNSAFE_DEV_WRITES=true` ＋真正的
  `redis-server` 子行程（跟 `RedisEnabledApiFixture` 同樣的啟動方式），專門驗證「寫入後公開快取
  真的失效」這件事，`AdminNewsCacheInvalidationTests` 用這個。
- **`AdminWriteAzuriteEnabledApiFixture`（S0-8 新增）**：`ENABLE_UNSAFE_DEV_WRITES=true` ＋真正的
  `azurite-blob` 子行程（同樣用 `TcpListener` 現抓現放的埠號；`--location` 指到
  `Directory.CreateTempSubdirectory()` 產生的暫存目錄，避免像 `redis-server` 那樣有寫入目前
  工作目錄的風險）＋`REDIS_HOST` 清空。`AdminNewsCoverBlobCleanupTests`／`AdminNewsCoverUploadTests`
  用這個，並透過 `InspectorContainer`（獨立的 `BlobContainerClient`）直接核對物件是否存在，
  不透過應用程式自己的連線斷言。找不到 `azurite-blob` 執行檔時同樣丟清楚例外
  （可用 `AZURITE_EXECUTABLE_PATH` 覆寫），加了 `--skipApiVersionCheck`（見「本機開發：Azurite」）。

六者都用**行程環境變數**（`Environment.SetEnvironmentVariable`）而不是
`WebApplicationFactory.ConfigureWebHost` 的 `ConfigureAppConfiguration` 來傳遞設定——
因為 `Program.cs` 在 `builder.Build()` 之前就會讀 `REDIS_HOST` 決定要不要注入
`RedisQueryCache`，那段程式碼跑在測試主機的攔截點之前。因此測試組件用
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` 停用平行化，
避免不同 fixture 的環境變數互相污染（測試數量少，停用平行化的時間成本可以接受）。

> 🔴 **本輪實際踩到的坑，記錄下來給下一個加 fixture 的人**：`DisableTestParallelization`
> 只保證**測試方法**不平行跑，**不保證不同 `[Collection]` 的 fixture 之間 `InitializeAsync`
> 的執行順序**。加入 `AdminWriteApiFixture`（會把 `ENABLE_UNSAFE_DEV_WRITES` 設成 `"true"`）之後，
> `AdminNewsGateClosedTests`（用 `ApiFixture`，預期這個變數是關閉的）開始**偶發**失敗——
> 不是每次跑都會中，因為環境變數是行程全域的，`ApiFixture` 若剛好在 `AdminWriteApiFixture`
> **之後**才呼叫 `_ = Server`，就會在變數已經被設成 `"true"` 的狀態下建立測試主機。
> **修法**：每一個「這個開關應該是關的」fixture（`ApiFixture`／`RedisUnavailableApiFixture`／
> `RedisEnabledApiFixture`）都在自己的 `InitializeAsync` 明確把 `ENABLE_UNSAFE_DEV_WRITES`
> 設成 `null`——不依賴「反正我沒設定過就是關閉」，跟既有 `REDIS_HOST` 的處理方式一致（同一段
> 已經在做的事，本輪只是多了一個新變數要比照辦理）。**下一次新增任何會動到行程環境變數的
> fixture，都要檢查現有的「應該關閉」fixture 有沒有明確清掉那個變數**，不要假設「別的 fixture
> 不會設定就不用管」。已用連續 4 次 `dotnet test` 重跑驗證修好（4 次全綠，`48/48`）。

### Docker 映像檔不受影響

`Tcrfc.Api.Tests` 是 `apps/api/` 目錄下的獨立子專案（沒有 `.sln`）。`Tcrfc.Api.csproj`
已明確 `<Compile Remove="Tcrfc.Api.Tests/**/*.cs" />`（否則預設的遞迴萬用字元會把測試原始碼
一起編進主專案，因為兩個 .csproj 在同一個目錄樹下、沒有解決方案檔幫忙切開建置範圍）；
`apps/api/.dockerignore` 也排除了這個目錄，`docker build -t x apps/api` 不受影響
（已實跑驗證，見「S0-7d 驗收紀錄」與本輪「驗收紀錄」第 3 點——本輪特別因為新增了 EF Core
套件與 `Data/Migrations/`／`Data/EfEntities/` 兩個新目錄，額外確認過 `docker build` 仍然成功，
不是只跑 `dotnet build` 就假設容器也沒問題，見 `docs/18-work-errors.md` E-35 的教訓）。

### 本輪驗收紀錄（slug 保留字驗證，2026-09-22）

環境：本機既有 `sqlserver` 容器（同上），`tcrfc_club_dev`，套用前 `articles` **83 筆**。

```
$ dotnet build   # apps/api 與 apps/api/Tcrfc.Api.Tests
建置成功。0 個警告 0 個錯誤

$ dotnet test    # CLUB_SQL_CONNECTION_STRING 指向本機 tcrfc_club_dev
已通過! - 失敗: 0，通過: 78，略過: 0，總計: 78
# 78 = 既有 48（不變，見「開發過程踩的坑」段落末的「4 次全綠 48/48」）
#    ＋ 本輪新增 30 項（9 個保留字逐一 × Theory ＋ 4 種大小寫變形 ＋ 10 種危險格式 ＋
#      建立／更新兩條路徑各自的保留字與格式測試 ＋ 3 個合法 slug 不受誤擋的驗證）
```

⚠️ **過程中發現並修正一個既有測試的隱性缺陷**：`AdminNewsWriteTests.UniqueSlug()` 原本用
`[CallerMemberName]` 把測試方法名稱（中文，含底線）直接嵌進網址名稱（例如
`admin-write-test-完整生命週期_建立草稿到刪除-xxxx`）。這在本輪新增格式驗證之前沒有任何影響，
新增之後**這些測試會被自己送出的 slug 卡在 400**——不是新規則寫錯，是舊的測試資料產生器本來就
沒有產出合法的網址名稱，只是在格式驗證出現之前，沒有任何東西會檢查它合不合法。已改成純 ASCII
亂數字串，不再嵌入呼叫端方法名稱（`apps/api/Tcrfc.Api.Tests/AdminNewsWriteTests.cs`）。

實際打執行中的 API（`ASPNETCORE_ENVIRONMENT=Development` ＋ `ENABLE_UNSAFE_DEV_WRITES=true`）：

```
# 保留字 club（建立）
$ curl -X POST /api/v1/admin/tcrfc/news -d '{"slug":"club",...}'
400 {"detail":"網址名稱「club」是系統保留給分類頁面使用的名稱，...請換一個能代表這篇文章內容的網址名稱..."}

# 保留字大小寫變形 Club（建立，被格式規則擋下，結果一致是 400）
$ curl -X POST /api/v1/admin/tcrfc/news -d '{"slug":"Club",...}'
400 {"detail":"網址名稱「Club」格式不正確：只能使用小寫英文字母、數字與連字號（-）組成..."}

# 保留字 media（建立）
400 {"detail":"網址名稱「media」是系統保留給分類頁面使用的名稱..."}

# 含斜線 has/slash（建立）
400 {"detail":"網址名稱「has/slash」格式不正確...（例如大寫字母、空白、斜線、句點都不能出現）"}

# 純數字 20260922（建立）
400 {"detail":"網址名稱「20260922」不能整段只有數字，請加入能代表文章內容的文字..."}

# 合法網址名稱（建立）
$ curl -X POST ... -d '{"slug":"curl-manual-verify-1790066402",...}'
201 {"id":"822f5299-...","slug":"curl-manual-verify-1790066402",...}
$ curl -X DELETE .../822f5299-...?expectedUpdatedAt=...
204   # 清乾淨

# 更新路徑改成保留字 international
400 {"detail":"網址名稱「international」是系統保留給分類頁面使用的名稱..."}
# 確認原文章沒有被改到：slug／title 皆原封不動
```

套用後 `articles` 仍是 **83 筆**（手動驗收建立的兩筆測試資料都已用 `DELETE` 清掉，實測核對過）；
對 83 筆逐一核對，**0 筆違反保留字清單、0 筆違反新格式規則**。

---

### S0-8 驗收紀錄（圖片上傳共用元件，2026-09-22）

🔴🔴🔴 **這份紀錄是原始（兩段式）設計的驗收，如實保留、不回頭改寫歷史**——那個設計已經因為
違反規劃書「選檔不上傳、儲存才上傳」被判定為規格違反並修正，見本檔最上方「本輪修正（上傳時機
違反規劃書）」與下方「**S0-8 修正驗收紀錄（單一請求，2026-09-22）**」。這份舊紀錄裡提到的
`POST .../uploads/images/...` 端點**已經整支移除**，照著這裡的 `curl` 指令打會得到 404——
要驗證現在的行為請看下面的新紀錄。

環境：本機既有 `sqlserver` 容器（`tcrfc_club_dev`），本機 `npx azurite --skipApiVersionCheck`
（連線字串 `BlobEndpoint=http://127.0.0.1:11000/devstoreaccount1`）。

#### 1. 建置

```
$ dotnet build   # apps/api
建置成功。0 個警告 0 個錯誤

$ docker build -t x apps/api
...
naming to docker.io/library/tcrfc-api-s08:latest done   # 成功
```

#### 2. 手動 curl 驗收（真實檔案，非模擬）

用 Python Pillow＋piexif 現產一張 **3000×2000、帶 EXIF（相機廠牌、方向標記 6＝需要旋轉）與
GPS 座標（24°9'0"N 120°41'0"E）**的 JPEG，另用 macOS `sips -s format heic` 從同一張圖轉出
**真正的 HEIC 檔案**（不是模擬，是系統內建工具真的轉檔）：

```
# 正常 JPEG（3000x2000，有 GPS/EXIF，orientation=6）
$ curl -X POST .../uploads/images/articles/<id>/cover -F "file=@big_with_gps.jpg"
{"key":"tcrfc/articles/<id>/cover/035c0dbd....webp","width":1707,"height":2560,"sizeBytes":7464}
# 1707x2560：3000x2000 因 orientation=6 轉正後變 2000x3000（縱向），長邊 3000 超過 2560 上限，
# 等比縮小為 2560/3000*2000=1706.67→1707，與 ImageProcessorTests 的斷言一致

# 正常 PNG（500x400）
$ curl -X POST .../uploads/images/articles/<id>/cover -F "file=@small.png"
{"key":"...webp","width":500,"height":400,"sizeBytes":432}

# 假副檔名（純文字改名 .jpg）
$ curl -X POST .../uploads/images/articles/<id>/cover -F "file=@fake.jpg"
400 {"detail":"圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。"}

# 真正的 HEIC 檔案（macOS sips 轉出）
$ curl -X POST .../uploads/images/articles/<id>/cover -F "file=@real.heic"
400 {"detail":"圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。"}

# 不支援的欄位插槽
$ curl -X POST .../uploads/images/players/<id>/photo -F "file=@small.png"
400 {"detail":"不支援的圖片欄位「players.photo」。"}
```

**用 Python `azure-storage-blob` SDK 直接查 Azurite 容器內容**（不透過應用程式自己的連線），
對 JPEG 那次上傳的五個物件逐一下載並用 Pillow 檢查：

```
main  size_bytes=7464  1707x2560  WEBP  exif_tags=0
1280  size_bytes=1944   854x1280  WEBP  exif_tags=0
640   size_bytes=546    427x640   WEBP  exif_tags=0
320   size_bytes=200    213x320   WEBP  exif_tags=0
thumb size_bytes=122    160x160   WEBP  exif_tags=0
```

**五個物件全部是真正的 WebP、尺寸比例正確、`exif_tags=0`（含 GPS 在內的全部中繼資料已清除）**。

其餘實測（見上方「圖片上傳共用元件」整節引用的行為）：

- **超過 10MB 上限**（11MB 測試檔）：`400`，友善訊息「圖片檔案太大（上限 10 MB）...」。
- **超過約 11MB**（31MB 測試檔）：`413`（Kestrel 平台層級擋下，非本服務 JSON 格式，見「Kestrel
  請求主體上限」）。
- **空檔案**：`400`，「沒有收到圖片檔案，請重新選擇圖片。」
- **不存在的俱樂部**：`404`。
- **跨俱樂部鍵隔離**：`tcrfc` 與 `bw` 對同一個 `entityId` 各自上傳，物件鍵前綴分別是
  `tcrfc/articles/.../cover/...` 與 `bw/articles/.../cover/...`，互不覆蓋。
- **換圖成功才刪舊物件、主檔與衍生檔一起刪**：對一篇真實建立的文章（`AdminNews` API）上傳
  封面圖 A、`PUT` 存入 `coverKey`，確認 A 的五個物件都在 Azurite 裡；再上傳封面圖 B、`PUT`
  更新 `coverKey`，確認 **A 的五個物件全部消失，B 的五個物件完整保留**。
- **刪除文章一併刪除圖片物件**：`DELETE` 該文章後，B 的五個物件也全部消失。
- **開發模式開關關閉時上傳端點回 404**：確認跟 `Features/AdminNews` 同一套機制。

#### 3. 自動化測試

```
$ cd apps/api/Tcrfc.Api.Tests && dotnet test
已通過! - 失敗: 0，通過: 96，略過: 0，總計: 96，持續時間: 3 m 50 s
# 96 = 既有 78（零回歸）＋ 本輪新增 18（ImageProcessorTests 9、UploadImagesEndpointTests 6、
#      UploadImagesGateClosedTests 1、AdminNewsCoverBlobCleanupTests 2）
```

---

### S0-8 修正驗收紀錄（上傳時機改單一請求，2026-09-22）

環境跟上面一樣：本機既有 `sqlserver` 容器（`tcrfc_club_dev`，`127.0.0.1,1433`）、本機
`azurite-blob`（`npm install -g azurite` 裝的執行檔，測試 fixture 自動啟動子行程，見
`Fixtures/AdminWriteAzuriteEnabledApiFixture.cs`）。

#### 1. 建置

```
$ cd apps/api && dotnet build
建置成功。0 個警告 0 個錯誤

$ cd apps/api && docker build -t tcrfc-api-test-build:s0-8fix .
...
naming to docker.io/library/tcrfc-api-test-build:s0-8fix done   # 成功
```

#### 2. 自動化測試（真實 HTTP 管線＋真實 SQL Server＋真實 Azurite，不 mock）

```
$ cd apps/api/Tcrfc.Api.Tests && dotnet test
已通過! - 失敗: 0，通過: 99，略過: 0，總計: 99，持續時間: 29 s
```

這一次跑就是「實跑驗證」本身，不是另外用 `curl` 模擬一遍：`AdminNewsCoverBlobCleanupTests`／
`AdminNewsCoverUploadTests` 兩支測試檔用真正的 `WebApplicationFactory<Program>`＋真正的
`azurite-blob`＋真正的 SQL Server 走了 CLAUDE.md 任務指示要求的全部四個實跑情境：

- **正常建立帶圖**：`建立文章時附封面圖片_五個物件都真的寫進儲存體_物件鍵含俱樂部與文章id`——
  真的建立一篇文章、真的夾一張帶 EXIF／GPS 的 JPEG，確認回應的 `coverKey` 前綴是
  `tcrfc/articles/<id>/cover/`、五個物件（主檔＋1280／640／320／160）真的存在於 Azurite。
- **更新換圖（舊物件被清）**：`換圖成功後_舊的主檔與全部衍生檔被刪除_新的完整保留`——PUT 夾一張
  新圖，確認新圖先上傳成功、資料列更新成功後舊圖的五個物件才被刪除，新圖五個物件完整保留。
  另外新增 `更新時勾選移除封面圖片且不夾檔案_封面清空_舊物件被刪除`（`removeCover=true`）與
  `更新時沒有夾檔案也沒有勾選移除_封面維持不變_物件不受影響`（Keep 語意），完整涵蓋封面圖片
  三態，不只是「換圖」這一種情境。
- **🔴 中途放棄不留物件**：規劃書「離開或取消表單不留下任何檔案」在單一請求契約下的天然結果是
  「使用者不按儲存＝從來沒有這次請求」，不需要（也不可能）用後端測試模擬「使用者按了取消」這個
  瀏覽器端動作。真正需要自動化釘住的是「請求送出了、圖片已經上傳成功，但後面的驗證失敗」這個
  唯一可能留下孤兒物件的情境，這支測試檔專門針對這個情境：
  - `建立文章_網址名稱重複但夾了正常圖片_圖片已上傳成功但建立失敗_回滾不留孤兒物件`：故意用
    已存在的 slug 建立第二篇文章、夾一張完全正常的圖片，伺服器端先上傳成功、repository 才發現
    slug 衝突丟 409，斷言**儲存體物件總數在請求前後完全相同**（沒有任何孤兒物件殘留）。
  - `更新文章_並行衝突但夾了正常圖片_圖片已上傳成功但更新失敗_回滾不留孤兒物件_舊封面圖片不受影響`：
    故意用過期的 `expectedUpdatedAt` 更新、夾一張新圖，伺服器端先上傳新圖成功、
    樂觀並行檢查才發現版本不對丟 409，斷言物件總數不變**且**舊封面的物件維持存在（沒有被誤刪）。
  - `建立文章_夾假副檔名文字檔_回400_不建立文章_不留下任何物件`／`夾空檔案`／`夾超過10MB的檔案`／
    `不存在的俱樂部代碼`：四種在碰到資料庫之前就會失敗的情境，逐一驗證物件總數不變。
  - `更新文章_同時夾檔案又勾選移除封面_回400_不嘗試上傳`：驗證這個矛盾請求連一次上傳嘗試都不會有。
- **寫入失敗不留半套**：跟上面「中途放棄不留物件」是同一組測試——「半套」在這個情境下指的正是
  「圖寫進去了但資料列沒更新」，兩支回滾測試已經涵蓋建立與更新兩條路徑。

**99 = 既有 78（零回歸，`AdminNewsWriteTests`／`AdminNewsSlugPolicyTests`／
`AdminNewsCacheInvalidationTests` 三支既有測試檔改用 multipart 呼叫，斷言強度不變或提高，
見 `AdminArticleMultipart.cs`）＋ `ImageProcessorTests` 9（不受影響）＋
`AdminNewsCoverBlobCleanupTests` 5（原 2 支＋新增 3 支涵蓋 `removeCover`／Keep 語意）＋
`AdminNewsCoverUploadTests` 7（全新，取代刪掉的 `UploadImagesEndpointTests` 6＋
`UploadImagesGateClosedTests` 1，並新增兩支回滾測試）**。

---

## 相關文件

- [`docs/12-database-schema.md`](../../docs/12-database-schema.md)／[`12a`](../../docs/12a-database-erd.md)／[`12b`](../../docs/12b-database-tables.md)／[`12c`](../../docs/12c-i18n-tables.md) — 資料表設計、權限模型、受限欄位、i18n 側表
- [`docs/14-invariants.md`](../../docs/14-invariants.md) — `club_id` 維度、跨庫 JOIN 陷阱、不得讀快取清單
- [`docs/17-deployment.md`](../../docs/17-deployment.md) §0／§1／§4／§6 — 技術選型、容器佈局、快取策略、本機開發資料庫
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) E-19／E-20／E-35／E-38 — 本次與前次開發踩的坑
  （本輪的「兩個測試假設過期」與「programs 撞 Program」兩件事尚未寫進這份文件，見上方「已發現、
  未動手修改的既有落差」第 4 點——本輪邊界不含 `docs/`，麻煩使用者或下一位轉記）
- [`docs/20-cicd.md`](../../docs/20-cicd.md) §5 — EF Core 與手寫 DDL 怎麼接軌（本輪執行的依據）
- [`docs/03-admin-spec.md`](../../docs/03-admin-spec.md) B2 — 新聞模組規格（CRUD、分類、標籤、排程發布、置頂精選）
- [`docs/12d-field-audit.md`](../../docs/12d-field-audit.md) §11 — 圖片欄位組缺 `_width`／`_height`／`_alt` 的資料庫綱要落差（S0-8 本輪再次確認，未動手加欄位）
- [`db/seed/README.md`](../../db/seed/README.md) — 本機資料庫怎麼連、種了哪些資料、已知落差
- [`apps/web/README.md`](../web/README.md) — 這支 API 唯讀端點的呼叫端（Nuxt 前台骨架）
- [`apps/admin/src/views/news/`](../admin/src/views/news/) — 這支 API 後台端點要餵的畫面（✅ 已接上，2026-09-22）
- [`apps/admin/src/`](../admin/src/) 的 `ImageUploader.vue` — S0-8 圖片上傳共用元件的契約消費端（✅ **已接上，2026-09-22**，走單一 multipart 契約「儲存才上傳」，契約見本檔「圖片上傳共用元件」整節）
