# deploy/loadtest/ — 上線前壓測（S0-10）操作手冊

> 對應 [`docs/17-deployment.md`](../../docs/17-deployment.md) §6「資料庫層級與容量」與 [`STATUS.md`](../../STATUS.md) `S0-10`。
> 🔴 **腳本已寫好、尚未執行。** 執行壓測、建立告警、調高限流都是對外／雲端操作，**要使用者核准後才做**。
> 工具：[k6](https://k6.io)（`brew install k6`）。腳本語法已用 Node 檢查，**未用 `k6 inspect` 驗證**（開發機沒裝 k6）；第一次先跑 `smoke`。

## 0. 先搞清楚三件事

1. **測試站就是正式 VM。** 全專案只有本機與正式兩套環境（[`docs/17`](../../docs/17-deployment.md) §10.1），`*.4webdemo.com` 是同一台 VM、同樣的兩個 Azure SQL 資料庫、同一個 Redis。壓測會**真的**消耗 VM 的 CPU 額度與 DTU；寫入類會**真的**在資料庫建資料列。
2. **經 Cloudflare 進來。** NSG 只放行 Cloudflare 的 IP 段，壓測機無法繞過 Cloudflare 直打 VM。量到的延遲含「壓測機 → Cloudflare 邊緣」的時間；Cloudflare 對單一來源 IP 的大量請求可能出挑戰頁或封鎖（見 §4）。
3. **壓測機不要用部署用的 VM**（self-hosted runner 在上面）。從你的 Mac 或另一台機器跑；壓測機自己的網路與 CPU 也會是瓶頸，`stress` 建議不要用家用 Wi‑Fi。

## 1. 檔案

| 檔案 | 用途 |
|---|---|
| [`lib.js`](lib.js) | 目標網址、**網域防呆**、負載曲線（smoke／load／stress／soak）、通過門檻 |
| [`read.js`](read.js) | **唯讀**：主站與藍鯨前台熱門頁、公開 API（新聞、賽程、FAQ）、慈善唯讀頁、商店目錄。**預設用這支** |
| [`write-charity.js`](write-charity.js) | **寫入**：慈善捐款 建單→發起付款→確認→查結果，只走假金流。需明確旗標才會執行 |
| 告警 | 資料空間 1.5 GB 告警與 DTU 告警寫在 [`infra/modules/monitoring.bicep`](../../infra/modules/monitoring.bicep)（見 §3） |

## 2. 安全防呆（腳本層，不靠人記得）

| 防呆 | 行為 |
|---|---|
| 網域白名單 | 所有目標主機必須是 `*.4webdemo.com`、`localhost`、`127.0.0.1`；否則 init 階段丟例外、**一個請求都不送**。沒有覆寫旗標；`tcrfc.4webdemo.com.evil.com` 這類仿冒網域也擋得住 |
| 預設目標 | `tcrfc.4webdemo.com`、`tcrfc-bw.4webdemo.com`、`tcrfc-charity.4webdemo.com`、`tcrfc-api.4webdemo.com`（[`docs/17`](../../docs/17-deployment.md) §10.1）；可用 `WEB_BASE`／`BW_BASE`／`CHARITY_BASE`／`API_BASE` 覆寫（仍受白名單限制） |
| 寫入旗標 | `write-charity.js` 沒有 `-e LOADTEST_ENABLE_WRITES=yes` 就拒絕載入 |
| 只打假金流 | 伺服器未設 `CHARITY_ALLOW_FAKE_PROVIDERS=true` 時 `/pay` 回 503，腳本**中止**而不是假裝成功；付款網址的交易識別碼不是 `FAKE-` 開頭也**立即中止**、不呼叫確認 |
| 可辨識 | User-Agent 為 `tcrfc-loadtest/1 (k6)`，Caddy／Cloudflare 日誌可過濾；捐款人姓名以 `LOADTEST ` 開頭、Email 為 `…@example.invalid`（不會寄到真人，也不會被當成真實捐款人） |
| 不碰 | 不打後台、不打會員登入（有帳號鎖定）、不打 `/news/*/views`、購物車與結帳（寫入且需登入，也不屬 S0-10 範圍） |

## 3. 執行前的準備（逐項，需使用者核准的標 🔐）

### 3.1 建立告警（先做，壓測時才有告警可驗證）🔐

**資料空間 1.5 GB 告警其實已經在 Bicep 裡了**（`alert-tcrfc-prod-<庫名>-storage-1_5gb`，兩個庫各一條，`storage` 指標 ≥ 1,610,612,736 bytes，`Maximum`，每 15 分鐘評估、視窗 1 小時，嚴重度 1，寄 `ag-tcrfc-prod-ops`）。這次**新增**四條 DTU 告警（兩個庫各兩級）：

| 告警 | 條件 | 嚴重度 | 意義 | 動作 |
|---|---|---|---|---|
| `alert-tcrfc-prod-<庫>-dtu-80` | `dtu_consumption_percent` 平均 ≥ **80%**、視窗 15 分鐘、每 5 分鐘評估 | 2 | 持續偏高，預警 | 排程升級 S0 |
| `alert-tcrfc-prod-<庫>-dtu-95` | 平均 ≥ **95%**、視窗 10 分鐘、每 5 分鐘評估 | 1 | 接近滿載，請求排隊變慢 | 立刻線上升級（§7） |

> 為什麼是這兩個數字：Basic 只有 5 DTU，DTU 吃滿**只是變慢而不是失敗**，所以用「持續」而不是瞬間尖峰（單次查詢吃 100% 很常見）告警，避免噪音；80% 給你反應時間，95% 代表已經在排隊。這是**執行層建議值**，壓測後依實際曲線調整。

這些告警隨 [`infra.yml`](../../.github/workflows/infra.yml) 建立：**變更合併到 `master`（`infra/**` 有變動）就會自動 `what-if` → `deploy`**。所以「建立告警」＝「核准合併」。要手動先預覽：

```bash
# 只預覽，不建立任何資源（需先 az login，且帳號對 rg-tcrfc-prod 有讀取權）
az deployment group what-if -g rg-tcrfc-prod -f infra/main.bicep -p infra/main.bicepparam \
  -p sqlAdminPassword='<SQL_ADMIN_PASSWORD>' -p alertEmail='<ALERT_EMAIL>' \
  -p sshPublicKey="$(cat ~/.ssh/id_ed25519.pub)" -p sshAllowedCidr='<SSH_ALLOWED_CIDR>'
```

> 參數以 [`infra/README.md`](../../infra/README.md) 與 `main.bicepparam` 為準，上列只是示意；值放佔位符，不要把真實密碼貼進任何檔案或終端歷史之外的地方。

### 3.2 壓測前記錄基準 🔐（唯讀 `az`，仍需登入）

```bash
RG=rg-tcrfc-prod
SERVER=<SQL_SERVER_NAME>   # sql-tcrfc-prod-<uniq>，用 az sql server list -g $RG -o table 查
for DB in tcrfc_club tcrfc_charity; do
  az sql db show -g $RG -s $SERVER -n $DB --query '{name:name, sku:currentSku.name, maxBytes:maxSizeBytes}' -o table
  az monitor metrics list --resource "$(az sql db show -g $RG -s $SERVER -n $DB --query id -o tsv)" \
    --metric storage --aggregation Maximum --interval PT1H --offset 2h -o table
done
```

記下兩個庫壓測前的 `storage`（bytes），壓測後比對成長量。

### 3.3 通知與時間

- 通知相關人員：**壓測會讓測試站變慢**，期間告警信會寄到 `ALERT_EMAIL`（預期內）。
- 避開部署（CD 進行中不要壓）與備份窗口；在 [`STATUS.md`](../../STATUS.md) 或 PR 留下開始／結束時間。
- 建議順序：`smoke` → `load` → `stress` → `soak`。每一關通過再往下；任一關不過就停下來看 §6，不要硬打下一關。

## 4. 怎麼避免把自己限流

API 的限流是**依來源 IP 分區的固定視窗**（`ClientIpResolver`，經 Cloudflare 後取 `CF-Connecting-IP`）。壓測機只有一個出口 IP，所以所有虛擬使用者共用同一份額度。

| 端點 | 限流 | 本手冊怎麼處理 |
|---|---|---|
| 新聞、賽程、FAQ、商店目錄、慈善項目／設定的 **GET** | **沒有**掛限流 | `read.js` 只打這些，不會被 API 限流 |
| 慈善 `GET /donations/{orderNo}` | 120 次／分鐘／IP | `read.js` **不打**；`write-charity.js` 只在每輪結尾打 1 次 |
| 慈善 POST（建單、付款、確認、取消） | **30 次／10 分鐘／IP**（`CHARITY_PUBLIC_WRITE_RATE_LIMIT_PERMITS`） | `write-charity.js` 的 `smoke` 曲線刻意壓在額度內（每 70 秒一輪、一輪 3 次 POST）；超過就計入 `loadtest_rate_limited_429`，門檻為 0 |
| 表單送出 20 次／5 分鐘、輕互動 60 次／分鐘、會員登入 30 次／5 分鐘等 | 有 | 不打 |

要壓**併發寫入**就必須暫時調高伺服器端限流（白名單式的做法在 API 不存在，限流只認 IP 分區）🔐：

1. 在 VM 的 `/opt/tcrfc/secrets/` 對應的 `charity.env` 加 `CHARITY_PUBLIC_WRITE_RATE_LIMIT_PERMITS=100000`、`CHARITY_PUBLIC_READ_RATE_LIMIT_PERMITS=100000` 與 `CHARITY_ALLOW_FAKE_PROVIDERS=true`，重建 `api` 容器（流程見 [`docs/20`](../../docs/20-cicd.md)）。
2. 跑 `write-charity.js` 並加 `-e ALLOW_RATE_LIMIT_RAISED=yes`。
3. **跑完立刻把三個值移除並重建 `api`。** `CHARITY_ALLOW_FAKE_PROVIDERS=true` 留在正式 VM 上＝憑空確認收款（`CharityFakeGuard` 註解）。這一步忘記是最大風險，結束前用 `docker compose exec api printenv | grep CHARITY` 確認。

**Cloudflare 這一層**：單一 IP 大量請求可能被 Bot Fight Mode／速率限制規則擋下，結果是 403／1015／挑戰頁，會被誤判成網站壞掉。處理順序：① 降低 `PEAK_SCALE`（例如 `-e PEAK_SCALE=0.5`）；② 看 Cloudflare Security Events 確認是否被擋；③ 才考慮在 Cloudflare 對**壓測機 IP** 建一條暫時的 Skip 規則 🔐（只放這一個 IP、限定 `*.4webdemo.com`，壓完刪除）。不要為了壓測關掉 WAF。

## 5. 怎麼跑

```bash
cd deploy/loadtest

# 0. 冒煙（約 45 秒）：確認每個網址 200、腳本無誤。不算壓測。
k6 run -e PROFILE=smoke read.js

# 1. 一般負載（約 11 分鐘，峰值 40 VU）
k6 run -e PROFILE=load --summary-export=read-load.json read.js

# 2. 壓力（約 12 分鐘，峰值 120 VU）——先確認 §3.1 告警已存在、有人盯著
k6 run -e PROFILE=stress --summary-export=read-stress.json read.js

# 3. 浸泡（約 33 分鐘，25 VU）
k6 run -e PROFILE=soak --summary-export=read-soak.json read.js

# 寫入（慈善假金流）：預設額度內，驗流程正確
k6 run -e LOADTEST_ENABLE_WRITES=yes write-charity.js
```

| 環境變數 | 預設 | 說明 |
|---|---|---|
| `PROFILE` | `smoke` | `smoke`／`load`／`stress`／`soak`（定義在 `lib.js`） |
| `PEAK_SCALE` | `1` | 乘在峰值 VU 上，例如 `0.5` 先打一半 |
| `WEB_BASE`／`BW_BASE`／`CHARITY_BASE`／`API_BASE` | 見 §2 | 目標，受網域白名單限制 |
| `CLUB_TCRFC`／`CLUB_BW` | `tcrfc`／`bw` | API 路徑的俱樂部代碼 |
| `WEB_PATHS`／`BW_PATHS`／`CHARITY_PATHS` | 內建清單 | 逗號分隔的頁面路徑。藍鯨不設部分單元，**冒煙時若有 404 先改這個清單**，不要拿 404 去跑負載 |
| `CHARITY_STORE_SLUG` | 空 | 一個真實有效的店家 slug（公開 API 無法列舉），給掃碼落地頁用；沒給就略過這條 |
| `CHARITY_PROJECT_SLUG` | 第一個上架項目 | `write-charity.js` 用 |

> `setup()` 會先抓新聞、商店、慈善項目的 slug 讓詳情頁有真實網址；資料庫目前是空的就只打列表，這會讓結果**偏樂觀**——壓測前先確認種子資料（`db/seed/`）已載入，否則延遲數字不能代表上線後的情況。

## 6. 要看哪些指標、通過標準

### 6.1 k6 輸出（腳本內建門檻，不過 k6 會以非 0 結束）

| 指標 | 通過標準 |
|---|---|
| `http_req_failed`（全體） | < 1% |
| 各場景 `http_req_failed` | < 5%（`load`／`stress`／`soak` 超過即自動中止） |
| 前台 SSR 頁（`web_main`、`web_bw`、`charity_read`）`http_req_duration` | p95 < 1.5 s、p99 < 3 s |
| 公開 API（`api_public`、`shop_catalog`）`http_req_duration` | p95 < 500 ms、p99 < 1.5 s |
| `checks` | > 99% |
| `loadtest_rate_limited_429`（寫入腳本） | = 0 |

> 這些數字是**執行層預設**，規劃書與 `docs/` 沒有效能規格。改門檻要同步改 `lib.js` 的 `thresholdsFor` 與本表。

### 6.2 伺服器端（跑的同時看，通過標準以 `load` 為準，`stress` 只求找到拐點）

| 看什麼 | 在哪看 | `load` 通過標準 |
|---|---|---|
| **DTU 使用率** `dtu_consumption_percent`（兩個庫） | Azure Portal → SQL database → Monitoring → Metrics；或 `az monitor metrics list --metric dtu_consumption_percent` | 平均 < 80%、無觸發 `dtu-95` 告警 |
| **資料空間** `storage` | 同上 | 壓測後成長量與 1.5 GB 告警門檻的距離夠大（`read.js` 不應增加；`write-charity.js` 每筆約數 KB，據此推估上線後半年的成長） |
| **VM CPU Credits Remaining**（B2ms 叢發型） | Azure Portal → VM → Metrics；告警 `alert-tcrfc-prod-vm-cpu-credits-low` | `load` 結束時餘額不歸零、不觸發告警。🔴 **`stress` 與 `soak` 若讓額度持續下降到歸零，結論就是要換 D2s_v5**（[`docs/17`](../../docs/17-deployment.md) §1） |
| VM CPU／記憶體／磁碟 | `docker stats`（SSH 到 VM，🔐）；Portal 的 Percentage CPU | 記憶體無單調上升（`soak` 看有無洩漏）、無 OOM 重啟 |
| 容器是否重啟 | `docker compose ps`、`docker inspect -f '{{.RestartCount}}'` | 重啟次數 0；`api` 的 `/readyz` 持續 200 |
| `api` 5xx 與慢查詢 | `docker compose logs api --since 15m \| grep -i error` | 無 5xx 與連線池耗盡（`Timeout expired`／`max pool size`） |
| Redis 命中率 | `docker compose exec redis redis-cli INFO stats`（`keyspace_hits`／`keyspace_misses`） | 讀取端點命中率高（[`docs/17`](../../docs/17-deployment.md) §4 的「不得讀快取」五類本來就不快取，不計） |
| Cloudflare | Analytics ／ Security Events | 無大量 403／1015／挑戰頁（否則結果不可信，見 §4） |

### 6.3 結論怎麼下

- **通過**：`load` 全部達標，`stress` 的拐點（失敗率或 p95 開始惡化的 VU 數）明顯高於預期的活動高峰。
- **DTU 為瓶頸**（`dtu_consumption_percent` 長時間 ≥ 95% 且 p95 惡化、CPU 還有餘裕）：照 §7 升 S0，**升完再跑一次同一關**確認有改善，再決定要不要升更高。
- **CPU 額度為瓶頸**：換 VM 規格＝**換機器**，而 LINE Pay 白名單綁的是 Public IP，Public IP 與 VM 生命週期解耦（[`docs/17`](../../docs/17-deployment.md) §2）——換規格前先讀該節，仍屬停機事件。
- **兩者都沒問題但 p95 超標**：先看是否被 Cloudflare 限制、壓測機本身 CPU／網路是否打滿，再查 `api` 慢查詢。
- 把結果（日期、曲線、DTU／CPU 峰值、拐點、結論）填進 [`docs/17`](../../docs/17-deployment.md) §6 的「壓測結果」，並更新 `STATUS.md` `S0-10`。

## 7. DTU 不足時怎麼線上升級（Basic → S0）🔐

Azure SQL 調整服務層級是**線上作業**：資料庫不下線，完成瞬間會切換一次連線（約數秒內的連線中斷，`api` 的 EF／Dapper 連線會重連）。S0 = 10 DTU、含 250 GB，約 US$15／月（[`docs/17`](../../docs/17-deployment.md) §6）。

```bash
RG=rg-tcrfc-prod
SERVER=<SQL_SERVER_NAME>
DB=tcrfc_club        # 或 tcrfc_charity；兩個庫各自獨立升級

# 1. 升級前記錄現況
az sql db show -g "$RG" -s "$SERVER" -n "$DB" --query '{sku:currentSku.name, tier:currentSku.tier, maxBytes:maxSizeBytes}' -o table

# 2. 升級（立即返回，實際調整在背景進行，通常數分鐘）
az sql db update -g "$RG" -s "$SERVER" -n "$DB" --service-objective S0 --no-wait

# 3'. 升級完成後再放寬容量上限（資料庫的 maxSizeBytes 是獨立設定，升級層級不會自動改它；S0 最大 250 GB）
# az sql db update -g "$RG" -s "$SERVER" -n "$DB" --max-size 250GB

# 3. 看進度（Status 變成 Online 且 sku 為 Standard/S0 即完成）
az sql db show -g "$RG" -s "$SERVER" -n "$DB" --query '{status:status, sku:currentSku.name}' -o table
```

- 升級前先確認 `lock-*` 沒有鎖住資料庫本身（鎖在 SQL 伺服器與儲存體上；`CanNotDelete` 不擋升級）。
- **務必同步改 Bicep**：`infra/modules/sql.bicep` 的 `sku`（Basic／5）與 `maxSizeBytes` 若仍是原值，下一次 `infra.yml` 部署會把它**改回去**（Incremental 部署也會套用資源屬性；降層級還可能因用量超過 2 GB 而失敗）。改法與流程見 [`infra/README.md`](../../infra/README.md)，PR 合併後才算完成。
- 升級的好處不只 DTU：S0 容量上限為 250 GB，**但資料庫目前設定的 `maxSizeBytes`（2 GB）要另外用 `--max-size` 放寬**，升級後以 `az sql db show` 確認 `maxSizeBytes` 已變。放寬後 1.5 GB 儲存告警要依新上限重算（`monitoring.bicep` 的 `storageAlertBytes`），否則會持續誤報。
- 降回 Basic：只有資料庫實際用量 < 2 GB 才能降；降級同樣是線上作業。
- 備份冗餘（Local）、PITR 7 天不受升級影響。

## 8. 清除壓測資料 🔐

- `read.js`：**不寫入任何資料**，無需清理。
- `write-charity.js`：每輪建立 1 筆 `donations`（`donor_name LIKE 'LOADTEST %'`，狀態通常為 `paid`），並連帶 `payments`／發票紀錄／寄信紀錄等子表資料列（外鍵見 [`db/charity-schema.sql`](../../db/charity-schema.sql) `donations` 之後的 `FOREIGN KEY … REFERENCES donations`）。

先唯讀確認筆數：

```sql
-- 在 tcrfc_charity 執行（唯讀）
SELECT COUNT(*) AS loadtest_rows, MIN(created_at) AS first_at, MAX(created_at) AS last_at
FROM donations WHERE donor_name LIKE N'LOADTEST %';
```

**刪除不在本手冊內提供現成指令**：捐款是會計相關資料、子表多、且 PITR 只有 7 天，刪錯很難救。上線前的測試資料清理應與其他上線前資料整理（種子資料、測試帳號）**一起規劃**，由使用者核准後再寫專用腳本並先在本機資料庫演練；在那之前，`LOADTEST` 前綴與 `example.invalid` 網域就是辨識依據。報表若要先排除，以這個前綴過濾。

## 9. 疑難排解

| 現象 | 原因／處理 |
|---|---|
| 一啟動就丟「拒絕執行：… 不是 *.4webdemo.com 或本機」 | 網域防呆生效，目標設錯了。這是預期行為，不要去改防呆 |
| `smoke` 有 404 | 頁面路徑與實際路由不符（藍鯨不設部分單元）。用 `WEB_PATHS`／`BW_PATHS`／`CHARITY_PATHS` 覆寫 |
| 大量 403／429／「Just a moment」 | Cloudflare 或 API 限流，見 §4。降低 `PEAK_SCALE`，不要硬壓 |
| `write-charity.js` 回報「發起付款回 503」 | 伺服器沒開 `CHARITY_ALLOW_FAKE_PROVIDERS=true`（§4 步驟 1）。腳本已中止，沒有建立付款 |
| 建單回 422「人機驗證未通過」 | 已設定 `TURNSTILE_SECRET_KEY_CHARITY`，壓測機沒有權杖。壓測期間暫不設定（目前尚未啟用），不要為壓測偽造權杖 |
| 建單回 400／422（金額或憑證欄位） | 項目的 `invoiceMode`／金額範圍與腳本假設不符；用 `CHARITY_PROJECT_SLUG` 指定另一個項目，或檢視 `write-charity.js` 的 `invoiceFor` |
| k6 本身 CPU 打滿、數字失真 | 壓測機太弱。換機器或降低 `PEAK_SCALE`；k6 輸出的 `iterations` 遠低於預期是徵兆 |
