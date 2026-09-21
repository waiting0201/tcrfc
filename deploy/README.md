# deploy/ — 部署設定說明（S0-7a 本機骨架）

> 對應 [`docs/17-deployment.md`](../docs/17-deployment.md)（拓撲、網路、快取、DBMS）與
> [`docs/20-cicd.md`](../docs/20-cicd.md)（CI/CD、映像檔、部署方式、Secrets）。
> 🔴 **這一階段（S0-7a）不開任何 Azure 資源**，全部是本機／repo 內的檔案。

## 這個目錄有什麼

| 檔案 | 用途 |
|---|---|
| [`Caddyfile`](Caddyfile) | 正式環境的 proxy 設定：依 Host 分流到五個上游、自動 TLS |
| [`Caddyfile.prelaunch`](Caddyfile.prelaunch) | **同一台正式 VM 在正式網址到位前**用（不是另一套環境），與 `Caddyfile` 幾乎相同，差異只有三個公開前台加 HTTP Basic Auth（[`docs/17-deployment.md`](../docs/17-deployment.md) §10.4）。用 `.env` 的 `CADDYFILE` 指定，**沒有第二份 compose 檔** |
| [`Caddyfile.dev`](Caddyfile.dev) | 本機開發用，明文 HTTP，`auto_https off` |
| [`local-ddl.sh`](local-ddl.sh) | 把 `db/*.sql` 轉成本機 SQL Server 2022 相容版本（`json`→`nvarchar(max)`），不改動原始檔 |
| [`dev/club.env.example`](dev/club.env.example)／[`dev/charity.env.example`](dev/charity.env.example) | 本機開發用機密範本，複製成同目錄下拿掉 `.example` 的檔名後使用（該檔名已被 `.gitignore` 排除） |

`../docker-compose.yml`（正式）與 `../docker-compose.dev.yml`（本機開發 override）放在 repo 根目錄，不在這裡——
和專案裡其他工具的慣例一致（`docs/`、`db/` 都在根目錄），`deploy/` 只放 proxy 與部署腳本相關的檔案。

## 🔴 目前的限制：應用程式都還不存在

`apps/web`、`apps/web-charity`、`apps/admin`、`apps/admin-charity`、`apps/api` 現在全部是空的
（只有 `README.md`、`Dockerfile`、`.dockerignore`）。**`docker compose build` 現在會失敗，這是預期行為**——
Dockerfile 假設的是專案建好之後的產物形狀（`.output/`、`dist/`、`.csproj` 的 publish 輸出），
專案本身還沒建立。`docker compose config`（純語法驗證，不觸碰任何映像檔）可以正常跑。

## 🔴🔴 2026-09-21：本機開發資料庫已合併進既有的 `sqlserver` 容器 🔴🔴

> 這是一次**刻意的方向反轉**。S0-6c 當時的決定是「本機另開一個 `mssql-dev` 容器，絕對不要動
> 既有的 `sqlserver` 容器」，理由是不確定共用是否安全。**使用者已於 2026-09-21 明確拍板改用
> 既有容器**：`docker-compose.dev.yml` 的 `mssql-dev` 服務已移除，`tcrfc_club_dev`、
> `tcrfc_charity_dev` 兩個資料庫現在建在宿主機上那個既有、非本專案 compose 管理的 `sqlserver`
> 容器裡（`mcr.microsoft.com/mssql/server:2022-latest`，與舊 `mssql-dev` 完全同映像檔，
> `MSSQL_PID=developer`，佔用宿主機 `1433` port）。**那個容器是使用者另一個專案在用的**，
> 裡面已有約 25 個屬於別的專案的資料庫——本專案只是在同一個 instance 裡多開兩個資料庫，
> **不得以任何方式改動、重建、停止或刪除這個容器本身**。

### 為什麼要合併——省一個容器，代價是換一種要小心的方式

舊方案（兩個容器）的好處是「風險徹底隔離、寫死拒絕接觸 `sqlserver`」，代價是本機多跑一個
2GB 記憶體、amd64 模擬層的 SQL Server 容器。合併後代價與好處對調：**省了那個容器，但風險
性質從「接錯容器」變成「動到錯的資料庫」**——因為現在兩個專案的資料庫住在同一個 instance 裡。

### 防呆怎麼改（重要：讀完再動手）

- `deploy/local-ddl.sh`、`db/seed/apply-seed.sh` 的目標容器現在可由環境變數
  `LOCAL_MSSQL_CONTAINER` 指定，**預設 `sqlserver`**（不再寫死拒絕這個名字）。
- ⛔ **資料庫名稱寫死白名單**：`deploy/local-ddl.sh` 只認 `tcrfc_club_dev`／`tcrfc_charity_dev`
  兩個；`db/seed/apply-seed.sh` 只認 `tcrfc_club_dev` 一個。任何其他資料庫名稱一律被腳本拒絕
  執行（`assert_allowed_database`／`TARGET_DATABASE` 常數），不接受呼叫端覆寫。
- ⛔ **任何情況下都不對這個 instance 上的既有資料庫下 `DROP`／`ALTER`**。兩支腳本從頭到尾唯一
  會執行的 DDL 動作是 `IF DB_ID(...) IS NULL CREATE DATABASE`（建庫，若不存在）與在
  `tcrfc_club_dev`／`tcrfc_charity_dev` 內部建表，沒有任何程式碼路徑碰得到這兩個庫以外的物件。
- 兩支腳本執行 `--apply` 時**開頭都會先印出目標容器與目標資料庫**，不悄悄動手。
- **驗證方式**：操作前後各對 `sqlserver` 容器跑一次 `SELECT name FROM sys.databases`，
  比對差異應該只有新增 `tcrfc_club_dev`／`tcrfc_charity_dev` 這兩筆，其他 25 個既有資料庫
  一筆都不該變動。

### ⛔ 這個既有容器沒有掛 volume——資料在容器可寫層

跟舊的 `mssql-dev`（掛了具名 volume `mssql_dev_data`，容器可以自由重建）不同，**這個既有
`sqlserver` 容器完全沒有掛任何 volume**（`docker inspect sqlserver` 的 `Mounts` 是空陣列）。
這代表：

- `docker rm sqlserver`（或任何導致這個容器被刪除重建的操作）會讓 `tcrfc_club_dev`、
  `tcrfc_charity_dev`**連同使用者另一個專案的全部約 25 個資料庫一起消失**，且**沒有
  volume 可以復原別的專案的資料**。
- **這不是在警告使用者不要動這個容器**——那個容器是別的專案在用，何時重建、要不要重建，
  是使用者的決定，本專案沒有立場也沒有必要阻止；**這裡只記錄一件事**：如果哪天 TCRFC 的
  本機資料庫突然「憑空消失」，根因很可能是這個容器被重建過，而不是 TCRFC 這邊的程式碼或
  資料庫哪裡壞了。
- **TCRFC 這兩個庫的復原方式很簡單**（因為本來就是可重新產生的 mockup 資料，見
  [`../db/seed/README.md`](../db/seed/README.md)）：
  ```bash
  ./deploy/local-ddl.sh --apply    # 重建 tcrfc_club_dev、tcrfc_charity_dev 的表結構
  ./db/seed/apply-seed.sh          # 重灌主站庫的種子資料（慈善庫沒有種子來源，維持空表）
  ```
  **別的專案的資料庫沒有這條路**——它們不是本專案管理的，本專案也沒有它們的建置腳本，
  這一點只能記錄、不能代為解決。

## 本機怎麼起（等 apps/* 建好之後）

```bash
# 1. 複製環境變數範本
cp .env.example .env                                  # 填 REDIS_PASSWORD、MSSQL_DEV_SA_PASSWORD 等
cp deploy/dev/club.env.example deploy/dev/club.env     # 填本機開發用的假機密
cp deploy/dev/charity.env.example deploy/dev/charity.env

# 2. 語法驗證（現在就能跑，不需要 apps/* 存在）
docker compose -f docker-compose.yml config
docker compose -f docker-compose.yml -f docker-compose.dev.yml config

# 3. 確認宿主機既有的 sqlserver 容器已在跑（不是本專案啟動它，見上一節）
docker ps --filter name=sqlserver

# 4. 起本機開發堆疊（要等 apps/* 有內容才 build 得起來；不再包含資料庫，資料庫是上一步的既有容器）
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build

# 5. 轉換並灌入本機用的 DDL（對既有 sqlserver 容器建 tcrfc_club_dev／tcrfc_charity_dev 兩個庫）
./deploy/local-ddl.sh --apply
```

啟動後：

| 服務 | 本機網址 |
|---|---|
| 經 proxy（依 Host 分流，需要 `*.localhost` 能解析到 127.0.0.1，現代系統預設就會） | `http://localhost:8080`、`http://bw.localhost:8080`、`http://charity.localhost:8080`、`http://admin.localhost:8080`、`http://admin-charity.localhost:8080`、`http://api.localhost:8080` |
| 略過 proxy，直接打各容器（方便單獨除錯） | `nuxt-tcrfc` → `:3001`／`nuxt-bw` → `:3002`／`nuxt-charity` → `:3003`／`admin-web` → `:8081`／`admin-charity` → `:8082` |
| 資料庫（既有 `sqlserver` 容器，取代 Azure SQL，**不是本專案啟動的**） | `localhost:1433`，帳號 `sa`，密碼是 `.env` 的 `MSSQL_DEV_SA_PASSWORD`（**必須與該既有容器的 SA 密碼一致**，這不是本專案自訂的密碼） |

⚠️ **`redis` 本機也不開對外連接埠**——docs/17 §1 的三條規則在本機一樣適用，要查資料用
`docker compose exec redis redis-cli`。

⚠️ **`api` 容器怎麼連到這個資料庫**：`sqlserver` 不是本專案 compose 管理的服務，容器間無法
用服務名稱互連。`api` 服務改用 `host.docker.internal`（`docker-compose.dev.yml` 已加
`extra_hosts: ["host.docker.internal:host-gateway"]`，macOS／Windows 的 Docker Desktop
其實內建就有，這行主要是為了 Linux 相容）連回宿主機，打 `sqlserver` 容器對外發布的 `1433`
port——見 `deploy/dev/club.env.example`／`charity.env.example` 的連線字串。

## 🔴 本機兩個庫同一個 instance，正式是兩個獨立的 Azure SQL——現在還多一層：這個 instance 裡還有別的專案的資料庫

`tcrfc_club_dev` 與 `tcrfc_charity_dev` 現在放在**同一個** `sqlserver` 容器裡（省資源，且是
既有容器，見上一節）。**正式環境不是這樣**——那是兩個完全獨立的 Azure SQL 單庫（`docs/17` §1）。

⚠️ **這個差異會製造一種本機測得過、正式一定爆的錯**：

```sql
-- 本機跑得動（同 instance，三段式命名有效），正式環境直接失敗
SELECT ... FROM tcrfc_charity_dev.dbo.donations d
JOIN   tcrfc_club_dev.dbo.members m ON ...
```

Azure SQL Database **不支援跨庫查詢**（`docs/14`），所以上面這種寫法在正式環境無論如何都不會動。

🔴 **但這條的重點不是相容性，是法遵。** 慈善平台的獨立資料庫是**刻意的邊界**——主辦與收款主體是
台灣足球策略發展協會，不是俱樂部；協會自己控管自己蒐集的個資（`docs/14`、`docs/16` §9）。
**跨庫 JOIN 等於把兩個法人的資料混在一起**，即使技術上做得到也不該做。

⛔ **任何一句 SQL 只能碰一個庫。** 兩邊的資料要湊在一起，走應用層各自查詢再組合，不走資料庫。

🔴 **2026-09-21 起多一層風險，不只是「兩個 TCRFC 庫混淆」**：這個 instance 現在同時裝著
TCRFC 的兩個開發庫**與使用者另一個專案的約 25 個資料庫**。寫 SQL、連線字串或任何腳本時，
**資料庫名稱打錯字有可能真的連到別的專案的資料庫**（instance 層級沒有隔離，只靠資料庫名稱
與應用層的連線字串正確性）。這是上面「防呆怎麼改」那段寫死白名單的直接原因。

## 種子資料（S0-6c）

DDL 灌完之後，`tcrfc_club_dev` 還是空的。要灌 mockup 的球員／新聞／賽程等種子資料，
見 [`../db/seed/README.md`](../db/seed/README.md)：`./db/seed/apply-seed.sh`（讀
[`../site/src/data/*.json`](../site/src/data/) 產生冪等 T-SQL 並套用）。**慈善庫沒有種子來源**，
只有主站庫會被灌資料。

---

## 🔴 既有的 `sqlserver` 容器——絕對不要動這個容器本身

本機已有一個獨立、非本專案 compose 管理的容器，名為 `sqlserver`，佔用 `1433` port，
是**使用者另一個專案在用的**，裡面已有約 25 個既有資料庫，且**沒有掛任何 volume**（見上方
「這個既有容器沒有掛 volume」）。**不得重建、不得改埠繫結、不得加 volume、不得改任何設定、
不得 `docker rm`／`docker restart`。** 本專案只被授權**在裡面建立 `tcrfc_club_dev`／
`tcrfc_charity_dev` 這兩個新資料庫**，不做任何其他事。

`deploy/local-ddl.sh`、`db/seed/apply-seed.sh` 預設對到這個容器名稱（`LOCAL_MSSQL_CONTAINER`
環境變數可覆寫容器名稱，但資料庫名稱白名單寫死不可覆寫，見上方「防呆怎麼改」）。

## `mssql-dev` 退場後的回收與復原（舊 volume 怎麼處理）

舊的 `mssql-dev` 服務與它的具名 volume `mssql_dev_data` 已從 `docker-compose.dev.yml` 移除，
但**這個 volume 本身沒有被主動刪除**——它還在 Docker 裡，只是不再被任何 compose 檔引用。
是否清掉、何時清掉，由使用者決定，不是本次任務代為決定的事：

```bash
# 查看舊容器／volume 現況（可能還在跑，也可能已經沒有容器只剩 volume）
docker ps -a --filter "name=mssql-dev"
docker volume ls --filter "name=mssql_dev_data"

# 確認新環境（sqlserver 容器裡的 tcrfc_club_dev／tcrfc_charity_dev）已可用之後，
# 要回收舊容器與 volume：
docker stop tcrfc-mssql-dev-1        # 若容器名稱不同，以 docker ps -a 的實際名稱為準
docker rm tcrfc-mssql-dev-1
docker volume rm tcrfc_mssql_dev_data # 具名 volume 前綴是 compose 專案名稱 tcrfc
```

⚠️ **這是單向操作，volume 一旦刪除就是真的沒了**——但因為新環境已完整驗證可用（見本次交付
報告的驗收數字），舊 volume 裡的資料已無留存必要，只是保守起見交給使用者親自執行。

## 正式網址到位前怎麼起（同一台 VM、真的 Azure 資源）

> 🔴 **這不是第三套環境。** 本專案只有兩套：**本機開發**（上一節）與**正式 VM**（下一節）。
> 這一節講的是同一台正式 VM、同一批容器、同一個資料庫，在藍鯨／慈善正式網域還沒到位、
> 主站還沒從 Wix 切換前的**暫時設定**——對應 [`docs/17-deployment.md`](../docs/17-deployment.md) §10。
> 跟下面「VM 上怎麼起」的差異全部在 `.env`：**指令一字不差，沒有第二份 compose 檔**。

```bash
# 1. .env 用上線前的值（.env.example 的預設值就是這一組，直接 cp 即可）
cp .env.example .env
#    其中三個值決定「現在是上線前階段」：
#      SITE_ENV=prelaunch                       → Nuxt 輸出 robots.txt 全擋 ＋ noindex（第 1、2 層）
#      CADDYFILE=./deploy/Caddyfile.prelaunch   → proxy 改掛加 Basic Auth 的設定（第 3 層）
#      TCRFC_DOMAIN=stg.tcrfc.tw（等六個）      → 暫用的 tcrfc.tw 子網域
#    另外填 PRELAUNCH_BASIC_AUTH_USER／PRELAUNCH_BASIC_AUTH_HASH，雜湊用下面這行產生：
#      docker run --rm caddy:2.9.1-alpine caddy hash-password --plaintext '<密碼>'

# 2. DNS：五個暫用子網域（stg／bw-stg／charity-stg／admin-stg／admin-charity-stg，
#    API_DOMAIN 另計）的 A/AAAA 記錄指到 VM 的靜態 Public IP，Cloudflare 代理（橘雲）

# 3. 起動指令與正式期完全相同
docker compose pull nuxt-tcrfc nuxt-bw nuxt-charity admin-web admin-charity api
docker compose up -d
```

**切到正式網址時**：只改 `.env`——六個網域值換成正式值、`SITE_ENV=production`、**把 `CADDYFILE`
那一行註解掉**（回到預設的 `deploy/Caddyfile`），重新 `docker compose up -d`。只有 `proxy` 的
掛載內容變了會被重建，其餘容器不受影響。**主站的完整切換步驟**（Wix DNS 現況確認、apex／`www`
取捨、128 筆 301、回滾程序）見 [`docs/17-deployment.md`](../docs/17-deployment.md) §10.6，
**不是改個 `.env` 就結束**。

⚠️ **上線前的三層防護**（HTTP 標頭、`robots.txt`、Basic Auth／Cloudflare Access）**不是可有可無的裝飾**，
見 [`docs/17-deployment.md`](../docs/17-deployment.md) §10.4「為什麼三層都要」與「被索引後的清理成本」。
掛了 `Caddyfile.prelaunch` 卻沒填帳密，Caddy 會直接啟動失敗——那是刻意的，寧可起不來也不要
悄悄變成沒有保護的公開站。
兩個後台建議額外在 Cloudflare 端設定 Access（email 一次性驗證碼），這份檔案管不到，需要另外在
Cloudflare Zero Trust 後台設定。

---

## VM 上怎麼起（等 Azure 資源開通之後，S0-7／S0-6 完成後）

```bash
# self-hosted runner 的 deploy job 會做這件事（docs/20-cicd.md §4）：
docker compose -f docker-compose.yml pull nuxt-tcrfc nuxt-bw nuxt-charity admin-web admin-charity api
docker compose -f docker-compose.yml up -d
```

⚠️ **`pull` 特意只列五個服務名稱，不是 `docker compose pull` 全部**——`proxy` 與 `redis` 用官方映像檔
（`caddy:2.9.1-alpine`、`redis:8-alpine`），docker 會直接從 Docker Hub 拉，不需要、也不在 ghcr 的
「五個映像檔」範圍內（`docs/20-cicd.md` §3 明文「五個映像檔，不是六個」，這裡延伸為「proxy／redis
不算在內」）。全部服務首次啟動時 Docker 會依 `image:` 欄位自動決定去哪裡拉，不需要特殊處理。

VM 上還需要（依 `docs/20-cicd.md` §9「開通 Azure 後要補的設定清單」）：

1. `/opt/tcrfc/secrets/club.env`、`/opt/tcrfc/secrets/charity.env`（依 `docs/20` §7.2 建立，權限 `600`）
2. `.env`（根目錄，含 `TCRFC_DOMAIN`／`BW_DOMAIN`／`CHARITY_DOMAIN`／`ADMIN_WEB_DOMAIN`／`ADMIN_CHARITY_DOMAIN`／`API_DOMAIN`／`ACME_EMAIL`／`REDIS_PASSWORD`／`GHCR_OWNER`／`IMAGE_TAG` 真實值）
3. DNS：五個網域（主站、藍鯨、慈善、兩個後台子網域，`API_DOMAIN` 另計）的 A/AAAA 記錄指到 VM 的靜態 Public IP，**Cloudflare 代理（橘雲）**

## Cloudflare 在前面，對 Caddy 的 TLS 有什麼影響——處理方式

`docs/17-deployment.md` §1／§2 只畫了「Cloudflare（DNS + CDN + WAF）proxy 回源」這個事實，
沒有寫到 Caddy 這一層的細節；以下是這次新增的決定，寫在 [`Caddyfile`](Caddyfile) 的註解裡，這裡是摘要：

### 影響 1：Caddy 看到的來源 IP 是 Cloudflare 的邊緣節點，不是訪客真實 IP

`Caddyfile` 全域選項設了 `trusted_proxies static <Cloudflare 官方 IP 段>` ＋
`client_ip_headers CF-Connecting-IP X-Forwarded-For`（語法已用 WebFetch 對照 Caddy 官方文件核實）。
Cloudflare 的 IP 段清單見 <https://www.cloudflare.com/ips/>，**這份清單會變動**，
Cloudflare 改版時要回頭更新 `Caddyfile` 這段，也要同步核對 `docs/17` §2「NSG 入站 443／80 限
Cloudflare IP 段」的白名單是否也要跟著改——兩處是同一組 IP 段的兩個不同用途。

### 影響 2：Caddy 的自動 HTTPS（Let's Encrypt HTTP-01）要穿過 Cloudflare 才能簽出憑證

**選擇維持 Caddy 預設的自動 HTTPS（HTTP-01 挑戰），不另外接 DNS-01 或手動憑證**，理由：
HTTP-01 的挑戰請求會先進 Cloudflare 邊緣、再被正常轉送到本機（只要不是 Flexible 模式、
沒有頁面規則攔截 `/.well-known/acme-challenge/*`），這是社群上驗證過可行的常見組合。

**首次簽發憑證的建議步驟**（避免任何邊界情況，`README` 明列，不是自動化的一部分）：

1. 該網域的 DNS 記錄**先設成「僅 DNS」（灰雲，不代理）**，直接指到 VM 的靜態 Public IP；
2. 啟動 `proxy` 容器，等 Caddy 針對該網域完成一次成功的憑證簽發（看 log 或 `caddy_data` volume 有沒有寫入）；
3. 確認 Cloudflare SSL/TLS 加密模式設為 **Full (strict)**（不是 Flexible——Flexible 會讓 Cloudflare
   對本機講明文，撞上 Caddy 自動加的 http→https 轉址造成重導迴圈）；
4. 把該筆 DNS 記錄改回「代理」（橘雲）。之後的憑證更新（每 60–90 天）就會照常透過代理路徑進行，
   不需要每次都重複這個步驟——只有「這個網域第一次簽發」才需要。

**沒有採用的替代方案**：DNS-01 挑戰（透過 Cloudflare API token 動態插入 DNS TXT 記錄）技術上更穩，
不受代理狀態影響，但需要客製 Caddy build（`xcaddy` 加 `caddy-dns/cloudflare` 模組），多一個要維護的
自建映像檔，且 `docs/20-cicd.md` §3 明文「五個映像檔，不是六個」，不想無故生出第六個。
**若日後 HTTP-01 真的在正式環境出狀況，這是文件化的升級路徑**，改動範圍只在 `Caddyfile`（改用
`tls { dns cloudflare {env.CLOUDFLARE_DNS_API_TOKEN} }`）與新增一個 proxy 的自建 Dockerfile，不影響
其他服務。

## 哪些是 placeholder，等什麼條件才能用

| 項目 | 狀態 | 等什麼 |
|---|---|---|
| 五個 `Dockerfile` | 待 `apps/*` 專案建立才能 build | S0-7b／S0-9（`frontend-architect`／`backend-engineer` 建立各專案） |
| `apps/api/Dockerfile` 的 `.NET 10`、`ENTRYPOINT ["dotnet","Tcrfc.Api.dll"]` | 版本與組件檔名為本次自行選定的預設值，未經 `docs/17`／`docs/20` 明文指定 | `backend-engineer` 建 `apps/api` 專案時確認／調整 |
| `.env.example` 的網域值 | 現在填的是上線前暫用網域（`stg.tcrfc.tw` 等，見 `docs/17` §10.1），**可直接用**；正式網域範例留作註解，藍鯨與慈善網域尚未確定 | STATUS.md 阻塞清單 B-4／B-7；主站 apex／`www` 取捨見 `docs/17` §10.6 |
| `deploy/Caddyfile`／`deploy/Caddyfile.prelaunch` 實際簽出憑證 | 完全沒測過（本機無公開 IP，暫用網域雖已可解析但尚未指向任何 VM） | Azure VM ＋ 靜態 Public IP 開通（S0-7） |
| `CADDYFILE`／`PRELAUNCH_BASIC_AUTH_*` 端到端 | `caddy validate` 已在本機用 `caddy:2.9.1-alpine` 跑過、三種 `.env` 組合的 `docker compose config` 也已驗證；**真的用帳密登入一次**尚未測（沒有 VM） | 同上 |
| VM 上的 self-hosted runner、`/opt/tcrfc/secrets/*` | 未建立 | S0-7（Azure 資源）、`docs/20-cicd.md` §9 |
| `docker-compose.yml` 的 `env_file` 指向 `/opt/tcrfc/secrets/club.env`／`charity.env` | 這兩個絕對路徑在本機不存在，只有 VM 上會有 | 同上；本機開發用 `docker-compose.dev.yml` 覆寫成 `deploy/dev/*.env` |
| `deploy/Dockerfile`／xcaddy 自訂 Caddy build（DNS-01 用） | **沒有建立**，只在本文件與 `Caddyfile` 註解裡記錄為「若 HTTP-01 出狀況的升級路徑」 | 只有 HTTP-01 真的行不通才需要 |

## 相關文件

- [`../docs/17-deployment.md`](../docs/17-deployment.md) — 部署拓撲、網路、快取策略、DBMS 連帶決定
- [`../docs/20-cicd.md`](../docs/20-cicd.md) — CI/CD、映像檔、部署方式、Secrets 清單
- [`../docs/13-blue-whale-site.md`](../docs/13-blue-whale-site.md) §6 — 主站／藍鯨共用映像檔的開發紀律
- [`../apps/README.md`](../apps/README.md) — `apps/*` 目錄與映像檔的對照表
