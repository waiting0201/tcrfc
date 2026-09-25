# 17 — 部署架構與技術選型

> 🔵 **這份是執行層決定，不是規格。** 四份規劃書 §1.3 一致明文排除「框架、CMS、主機、部署環境」等技術決策
> （主站 L172、藍鯨 L102、慈善 L173、App L99／L225），所以**選型結果不進規劃書，只記在這裡**。
> **App 客戶端的選型另立一檔：[`19-app-tech-stack.md`](19-app-tech-stack.md)。**
> 本檔與規劃書衝突時一律以規劃書為準，並回頭修正本檔。
>
> 本檔承接 [`STATUS.md`](../STATUS.md) 的 **B-1／S0-1「技術選型未定案」**。
> 資料表怎麼長看 [`12`](12-database-schema.md)／[`12a`](12a-database-erd.md)／[`12b`](12b-database-tables.md)；
> 慈善平台的功能看 [`10`](10-charity-donation-site.md)；藍鯨站看 [`13`](13-blue-whale-site.md)。

---

## 0. 一分鐘理解

| 層 | 選定 |
|---|---|
| 前台 | **Nuxt 4 SSR**（Vue 3）——主站、藍鯨、慈善**各一個 instance**。⚠️ **2026-09-18 定案時寫的是 Nuxt 3，2026-09-20 改為 4**：`npx nuxi init` 現在預設就是 v4（v4 目錄結構 `app/`），greenfield 專案沒有理由起手就鎖在舊的 major。S0-9b 的 SEO 實測是在 **Nuxt 4.5.2 ＋ `@nuxtjs/seo` 5.3.16** 上通過的 |
| 後台 | Vue 3——官網、慈善**各一個 instance**（後台不需 SEO） |
| API | **.NET 10（LTS）／C#，EF Core ＋ Dapper**——**單一 instance**，持有兩個 `DbContext`。⚠️ **版本於 2026-09-20 補定**：.NET 10 是 2025-11 發布的 LTS（支援至 2028-11），涵蓋本案的整個交付與初期維運期。組件名 `Tcrfc.Api.dll`（`apps/api/Dockerfile` 的 `ENTRYPOINT` 依此，建專案時要對齊） |
| 快取 | **Redis 一個 instance，只服務俱樂部**；cache-aside ＋ SQL fallback |
| DBMS | **Azure SQL Database**，兩個獨立單庫，先用 Basic |
| 物件儲存 | **Azure Blob Storage** |
| 執行環境 | **單一 Azure VM（Japan East／東京）＋ Docker**，前台、後台、API、Redis 全在此 VM |
| 網路 | **單一 VNet**，PaaS 以 **VNet Service Endpoint** 接入 |
| 邊緣 | Cloudflare（DNS／CDN／WAF，proxy 回源 VM） |
| 前端建置 Node.js | **`node:24.13.1-alpine`（四個 Node 應用一致）**，2026-09-22 定案；**版本防漂移**（S0-9h）見 [§12](#12-前端建置用的-nodejs-版本) |

> 🔵 **區域：Japan East（東京）**，2026-09-20 由 West US 2 改定。
> **趕在申請 LINE Pay 商店號之前決定**——換區域＝換出口 IP＝改白名單，是有前置期的變更（見 [§7](#7-已知風險) 風險 4、9）。
> **台中↔東京 RTT 由 130–160ms 降到約 30–50ms**，SSR 首屏的往返成本降到約三分之一。
> ⚠️ **兩個代價**：① **Japan East 的單價高於 West US 2**（VM 與 Azure SQL 都是），上線前要重估月成本
> ② **個資仍存於境外**（[`STATUS.md`](../STATUS.md) B-11 不因此解除，只是接受度可能與美國不同，**結論要法務給**）。

**這個架構的起點是一個外部約束**：**LINE Pay Online API 正式環境要求商家登記付款伺服器的出口 IP**
（商店管理後台的「管理付款伺服器 IP」，sandbox 不需要）。這條否決了無固定出口 IP 的執行環境
（Cloudflare Workers／Pages 要 Enterprise 的 Dedicated Egress IP 才有），因而決定了「自架 VM ＋ 靜態 Public IP」這個骨架。

---

## 1. 部署拓撲

```
                     Cloudflare（DNS + CDN + WAF + 快取）
                                    │  proxy 回源
                                    ▼
   ┌──────────────────────────────────────────────────────────────┐
   │ VNet ─ snet-app（Service Endpoint: Microsoft.Sql / .Storage）│
   │                                                              │
   │  ┌────────────────────────────────────────────────────────┐  │
   │  │ Azure VM（Japan East）Ubuntu LTS ＋ Docker Compose      │  │
   │  │ ★ Standard SKU 靜態 Public IP                           │──┼──▶ LINE Pay API
   │  │   ＝ 兩個商店號登記的同一個出口 IP                       │  │
   │  │                                                        │  │
   │  │  proxy（Caddy／Traefik）TLS ＋ 依 Host 分流             │  │
   │  │                                                        │  │
   │  │  前台三個            後台兩個          API 一個  快取   │  │
   │  │  ├ nuxt-tcrfc       ├ admin-web       └ api     redis  │  │
   │  │  ├ nuxt-bw          └ admin-charity     ├ DbCtx   ▲    │  │
   │  │  └ nuxt-charity                         │  Club ──┘    │  │
   │  │                                         └ DbCtx        │  │
   │  │                                            Charity     │  │
   │  │                                         （慈善不接快取）│  │
   │  └───────────────────────┬─────────────────┬──────────────┘  │
   │                          │ 服務端點         │ 服務端點        │
   └──────────────────────────┼─────────────────┼─────────────────┘
                              ▼                 ▼
                 Azure SQL（兩個獨立單庫）   Azure Blob Storage
                 sqldb-club / sqldb-charity  圖片與檔案
```

**八個容器**：`proxy`、`nuxt-tcrfc`、`nuxt-bw`、`nuxt-charity`、`admin-web`、`admin-charity`、`api`、`redis`。

### VM 規格

起點 **`Standard_B2ms`（2 vCPU／8 GB，叢發型）**，應用行程估 1.5–2 GB ＋ Redis 的 `maxmemory`。

⚠️ **叢發型的 CPU 額度要盯**：SSR 在持續流量下可能耗盡額度，上線後看額度餘額，不夠就換 `D2s_v5`。

### Redis 怎麼裝

🔴 **不是裝在主機上，是八個容器裡的一個。** ⛔ **不要 `apt install redis-server`**——
那會多一套啟動方式、一套記錄位置、一套升級流程；而且容器裡的 `api` 要連主機上的 Redis 就得走
bridge 網段，Redis 因此必須 `bind` 到對外介面上，**與 [§2](#2-網路)「不對外開任何連接埠」直接衝突**。
Ubuntu 套件庫的版本也會落後。

```yaml
services:
  redis:
    image: redis:8-alpine          # 釘住次版本，不要用 latest
    restart: unless-stopped
    command: >
      redis-server
      --maxmemory 512mb
      --maxmemory-policy allkeys-lru
      --save ""
      --appendonly no
      --requirepass ${REDIS_PASSWORD}
    environment:
      REDIS_PASSWORD: ${REDIS_PASSWORD}   # 只給 healthcheck 用；真正生效的密碼來源是上面的 --requirepass
    networks: [internal]
    healthcheck:
      # CMD-SHELL（不是 CMD）：exec form 不經過 shell，`$$REDIS_PASSWORD` 展開後只是字面上的
      # `$REDIS_PASSWORD` 字串，redis-cli 不會做變數代換，healthcheck 會一直失敗（S0-7a 實作時發現）。
      test: ["CMD-SHELL", "redis-cli -a \"$$REDIS_PASSWORD\" ping | grep -q PONG"]
      interval: 10s
      timeout: 3s
      retries: 5
    deploy:
      resources:
        limits:
          memory: 640M             # 512 ＋ 開銷，避免拖垮同機的其他容器
```

`api` 的連線目標就是 **`redis:6379`**（服務名即主機名）。

**三條不能弄錯的**：

| # | 規則 | 不照做會怎樣 |
|---|---|---|
| 1 | ⛔ **絕對不要寫 `ports:`** | Docker 發布連接埠會**繞過 UFW 直接改 iptables**——主機防火牆關了也擋不住。未授權的 Redis 被掃到是最常見的入侵途徑之一。同網路內用服務名互連即可，不需要發布連接埠 |
| 2 | **`--save "" --appendonly no`（不開持久化）** | 在 8 GB 小機器上多一份 I/O 與記憶體壓力。**重開機資料掉光本來就可接受**——那正是 cache-aside ＋ SQL fallback 的意義（[§4](#4-快取策略)） |
| 3 | **`maxmemory` 一定要設** | 不設會一路吃到 OOM killer 出手，**而它殺的不一定是 Redis**，可能是 `api` 或某個 `nuxt`。compose 的 `memory: 640M` 是第二道保險 |

⚠️ **`requirepass` [§2](#2-網路) 沒要求**（它本來就不對外），但成本只有一行環境變數，
擋掉的是「某個容器被打穿後 Redis 完全不設防」。**密碼放 `.env`，`.env` 不納版控。**

⚠️ **映像檔**：`redis:8-alpine` 為 AGPLv3，**自用不散布不受影響**。
要完全避開授權問題可換 `valkey/valkey:8-alpine`（BSD，協定相容，`StackExchange.Redis` 不用改）。**兩者皆可。**

> 🔴 **什麼情況下才該改成主機原生安裝**：Redis 變成有狀態的關鍵元件時（存 session、當佇列、
> 需要調 `vm.overcommit_memory` 之類的核心參數）。**採 cache-aside 後它是純快取，掉了就回源**，沒有這個需求。

---

## 2. 網路

| 項目 | 做法 |
|---|---|
| VNet | 單一 VNet ＋ 一個子網 `snet-app` |
| PaaS 接入 | 子網啟用 `Microsoft.Sql` 與 `Microsoft.Storage` 的 **Service Endpoint** |
| SQL／儲存體防火牆 | 改用 **VNet 規則**（只允許該子網），**移除公開 IP 規則**。兩個資料庫各自設定 |
| 對外連線 | VM 直掛 **Standard SKU 靜態 Public IP** |
| NSG 入站 | 443／80 限 Cloudflare IP 段，SSH 限固定來源，其餘全關 |
| Redis | **不對外開任何連接埠**，只在 Docker 內部網路可達 |

> **為什麼用 Service Endpoint 不用 Private Endpoint**：Service Endpoint 免費；Private Endpoint 每個約
> US$7–8／月 ＋ 資料處理費，兩個（SQL ＋ Blob）加起來比兩個資料庫本身還貴。

### ⚠️ 兩個容易搞錯的地方

1. **Service Endpoint 只改變「往 SQL 與 Storage」的路由。** 往網際網路（LINE Pay）的流量仍從 VM 的靜態
   Public IP 出去——**出口 IP 不受影響**。不要因為啟用了服務端點就以為不再需要 Public IP。
2. **必須明確配置 outbound。** Azure 的 default outbound access 已於 **2026-03-31** 退場，新建 VNet
   子網預設為 private。直掛 Standard SKU Public IP 即滿足，但它必須是刻意配置的，不能靠預設。

### 🔴 靜態 Public IP 必須與 VM 生命週期解耦

保留為獨立資源，VM 重建時重新掛載同一個 IP。
**換 VM、換區域、重建機器都會換 IP，而換 IP ＝ 要改 LINE Pay 白名單，是有前置期的變更、等同停機事件。**

---

## 3. LINE Pay 的固定 IP

- 兩個商店號（俱樂部、協會）**各自**在自己的商店管理後台登記**同一個** VM Public IP。
  白名單是 per-merchant 設定，登記同一個 IP 完全合法，且可 Add Row 供日後擴充。
- **一律使用 `confirmUrlType: CLIENT`**——使用者瀏覽器導回後，由伺服器端主動呼叫 Confirm。
  這與[慈善規劃書 §4.2](../output/TCRFC_慈善捐款平台功能規劃書.md) 已寫的五步驟流程一致，
  且**避免產生第二組要維護的 inbound 白名單**：若改用 `SERVER` 模式，就必須額外在 Cloudflare WAF
  與 NSG 放行 LINE Pay 的來源 IP 段。

🔴 **受約束的判準是通路，不是「會籍 vs 商品」**——看的是**有沒有我方伺服器對 LINE Pay 發出 API 請求**：

| 通路 | 我方伺服器呼叫 LINE Pay API？ | 受出口 IP 約束 |
|---|---|---|
| 官網站內商店結帳（俱樂部商店號） | ✅ 有 | **受** |
| 慈善平台捐款（協會商店號） | ✅ 有 | **受** |
| **App 會籍付款**（俱樂部商店號，見 App 規劃書 §5.2／§5.4 的兩段式流程） | ✅ 有 | **受** |
| 官網網頁的會籍費用走 **LINE Pay 收款連結**（付款發生在 LINE Pay 自己的頁面） | ❌ 沒有 | 不受 |
| 現場收款 | ❌ 沒有 | 不受 |

⚠️ **同一筆「會籍費用」在網頁與 App 走的是兩條不同通路**，不能一概而論。

⚠️ **App 首個上架版本不含 App 內付款**（App 規劃書 v3.10 §5.1），**但這不代表不需要固定出口 IP**——
站內商店與慈善平台都走 API 串接，本節的架構理由一字不變，且 App 的 Phase E 會回到這條路上。

---

## 4. 快取策略

**目的**：Basic 層只有 5 DTU，而 Nuxt SSR 每次算繪都打資料庫。把讀取吸到 Redis，5 DTU 只需應付寫入與後台查詢。

### 讀取：cache-aside ＋ SQL fallback

讀 Redis，未命中就讀 SQL 並回填。`maxmemory` 先設 **512 MB**，policy `allkeys-lru`（有 fallback，淘汰是安全的）。

> **為什麼不採用「Redis 為唯一讀取路徑」**：沒有 fallback 時，快取遺漏會**偽裝成「資料不存在」**——
> 使用者看到的是內容整頁消失，而不是變慢。而遺漏是必然會發生的：容器重啟、VM 重開機、記憶體淘汰、
> 寫入寫一半失敗、有人直接改資料庫。
> 而且在 8 GB 的小 VM 上 Redis 必須設 `maxmemory`，「Redis-only」等於要求所有被讀到的資料永久常駐記憶體，
> 與「用最便宜的配置」直接衝突。

### 寫入：write-invalidate，不是 write-update

後台寫入的順序是**先寫 SQL，交易成功後刪除相關 key**。所有快取 key 另設 TTL 兜底。

> **為什麼刪 key 而不是覆寫 key**：SQL 與 Redis 不在同一個交易裡。覆寫失敗會留下陳舊值；
> 而順序顛倒或 SQL 回滾，會在 Redis 裡留下**資料庫根本不存在的幻影資料**。
> 刪 key 沒有這個失敗模式，最壞情況只是下次讀取回源。

### ⛔ 不得讀快取的資料

| 資料 | 規格依據 | 讀到陳舊值的後果 |
|---|---|---|
| **庫存與商品可購買狀態** | 「付款未完成的訂單逾時自動取消並**釋回庫存**」（主站 L423）、「缺貨**由庫存自動判定**」（L1367）、後台需**庫存負值告警**（L1660） | 超賣 |
| **金流回呼的冪等檢查** | 「回呼須驗簽且**具冪等性**」（L423／L1406／L1656） | 重複入帳、重複開立發票 |
| **會員卡 `/m/<token>` 驗證** | 「會員可**重新產生** token（卡片外流時自保）」（L718） | 🔴 **安全問題**：已撤銷的卡仍通過查驗，撤銷機制失效 |
| **會籍有效性、訂單與付款狀態** | 驗證頁回傳「有效／已過期」（L716） | 已過期會籍被認可、重複開通。**App 端的落點見 [`19`](19-app-tech-stack.md) §4**：卡面不得以本機快取宣告有效 |
| **購物車** | 「不得跨俱樂部混買，切換站台即切換購物車」（L1452） | 結帳金額與內容不一致 |

### ✅ 快取的甜蜜點

- **每頁 SSR 都要、幾乎不變、量極小**：`Setting`、`UiString`、`Locale`、導覽選單、分類樹、`Banner`、
  夥伴與贊助商 Logo 牆。**收益最扎實的一塊，先做這個。**
- 已發布的內容頁（`Article`、`Player`、`Staff`、`Page`）。
- 綱要明訂的**不可變快照**：`DrawRoster`、`OrderItem`——天生適合快取。

⚠️ **收益要務實評估**：Cloudflare 已在前面快取內容頁 HTML，匿名訪客的讀取大多打不到 VM。
Redis 的實際收益集中在**共用小資料**與**登入後頁面**，而登入後頁面恰好多半落在上面的禁用清單裡。
先做共用小資料，量測後再決定要不要擴大。

### 實作的五條硬規則

**策略（上面）與實作（這裡）是兩件事。** 以下五條動手前就要定，否則會踩到固定的幾個坑。

**1. 🔴 Redis 掛掉不得讓請求失敗。**
上面的「未命中就讀 SQL」講的是 **cache miss**；**連線中斷是另一回事**——
`ConnectionMultiplexer` 斷線時每個讀取都會拋例外。
**每個快取讀取都要 try/catch 吞掉並回源**，快取層永遠不得成為請求的失敗點。
寫入端的失效刪除失敗同理：記一筆告警，**不得回滾已成功的 SQL 交易**（TTL 會兜底）。

**2. 🔴 失效用版本號，絕對不要掃 key。**
「刪除相關 key」的難處在於「相關」是哪些。⛔ **不得使用 `KEYS`——它會阻塞整個 Redis。**
把版本號嵌進 key，失效改成遞增一個計數器：

```
ver  = GET  ver:article:{club}                  // 計數器
key  = v{ver}:{club}:{locale}:article:{slug}    // 實際快取 key
失效 = INCR ver:article:{club}                  // 所有衍生 key 一次全部失效
```

舊 key 不用刪，交給 `allkeys-lru` 自然淘汰。**不用掃、不用維護 key 清單、不用知道哪些 key 存在。**

**3. 每個 key 一定要有 TTL 兜底。**
即使失效是明確的。漏掉一個失效點是必然會發生的事，**TTL 是唯一的止血**。

**4. 🔴 單飛（single-flight）。**
這在 **Basic 層 5 DTU** 上是硬需求：SSR 一頁多個區塊、多個併發請求同時 miss 同一個 key，
會一起打 SQL。要有 per-key 鎖，**只讓一個去查、其餘等結果**。

**5. 🔴 五類禁用要讓它「做不到」，不是「記得別做」。**
靠人記，半年後一定會破。**那五類的 repository 根本不注入快取服務**——想快取也沒有東西可呼叫。

**key 命名**必須含 `club_id` 與 `locale` 維度（約 50 張表有 `club_id`，所有前台內容走 `*_i18n` 側表），
否則會跨俱樂部或跨語系污染。`locale` 值域限定 `zh`／`en`（[`06-conventions.md`](06-conventions.md) §3），
**有限集合才不會讓 key 爆炸**。

---

## 5. 慈善平台的邊界：誠實盤點

慈善與俱樂部共用 VM 且共用一個 API 行程。規劃書的獨立要求逐條檢視：

| 規劃書要求 | 是否成立 | 憑什麼 |
|---|---|---|
| 獨立網域 | ✅ | 各自網域 |
| 獨立前台 | ✅ | `nuxt-charity` 獨立 instance |
| **獨立後台** | ✅ | `admin-charity` 獨立 instance、獨立帳號體系、獨立 2FA |
| 獨立資料庫 | ✅ | `sqldb-charity` 獨立 Azure SQL |
| **不得即時 join、不得同步查詢主站資料庫**（§9.4） | ✅ **平台強制** | **Azure SQL Database 不支援三段式跨庫查詢**，同一個邏輯伺服器或彈性集區也一樣。唯一繞道是 Elastic Query（長年 Preview、僅 SELECT、僅 SQL 驗證、不支援私人端點）。**只要不啟用它，這是硬邊界不是紀律** |
| 不共用任何執行環境 | ❌ | 共用 VM，且 API 同一個行程 |
| 任一方停機不得影響另一方 | ❌ | API 或 VM 停機兩邊一起停 |

> **法遵論述不受影響。** 獨立資料庫仍然成立，慈善規劃書 §2 的「協會自己控管自己蒐集的資料、
> 委託範圍縮小為單純的維運服務」這個主張不因宿主機共置而改變。

### 補償措施（紀律層，必須逐條落實）

- 兩個完全分離的 `DbContext`，**慈善的那個不註冊任何俱樂部實體型別**——跨庫 join 在型別層就寫不出來
- **不啟用 Elastic Query／External Table**，這是維持上表那條硬邊界的唯一條件
- **慈善的任何讀寫路徑不得接觸 Redis**
- 慈善端點掛獨立 host 與獨立授權 policy，走協會的帳號體系
- 兩組 LINE Pay 憑證與兩組連線字串分開的設定來源，**不得共用同一個 `.env`**

---

## 6. DBMS 的連帶決定

[`12` §1.4](12-database-schema.md) 寫明「選定後回頭改這五處即可」。Azure SQL 的答案：

| # | 議題 | 定案 |
|---|---|---|
| 1 | **陣列欄位** | **維持關聯表**。`ValueTagLink` 是多型關聯，要能反查「哪些內容掛了這個標籤」；`CalendarEventTeam`、`FaqCategoryLink` 同理。SQL Server 無陣列型別，此項無變數 |
| 2 | **JSON 欄位** | 用 **Azure SQL 原生 `json` 型別**（已 GA，二進位儲存、`JSON_VALUE` 相容、JSON 索引推出中），不用 `nvarchar(max)`。**維持「只存不查」作為設計紀律**，但原生型別保留逃生口 |
| 3 | **`CalendarEvent`** | 第一期用**一般 VIEW**（UNION）。⚠️ **SQL Server 的 indexed view 明文禁止 UNION／UNION ALL**，所以沒有 materialized view 這條升級路；效能不足時走**索引表 ＋ 來源模組寫入時同步**，或先靠 Redis 吸收 |
| 4 | **全文檢索** | **第一期用跨表 `LIKE` 比對**，不建搜尋索引表、不預先加索引（資料量在數百至數千列，掃描可接受）。升級路徑是 **Azure SQL 內建全文檢索**（有中文斷詞），**不需要外掛 Meilisearch** |
| 5 | **NULL 語意** | **弱讀法：`UNIQUE (club_id, slug)` 就夠**——SQL Server 的唯一索引把 NULL 當成相等，不需要篩選唯一索引、不需要觸發器。網址對應哪一筆由路由優先順序解決，見下 |

### ✅ §1.4 第 5 件（2026-09-20 定案）

綱要寫的是：「`(club_id, slug)` 唯一，**且 `club_id` 為空時 `slug` 亦須全站唯一**」。

⚠️ **SQL Server 的唯一索引把 NULL 當成相等**（與 PostgreSQL 相反），
所以 `UNIQUE (club_id, slug)` 本身就擋掉了兩筆 `(NULL, 'about')`——**不需要篩選唯一索引。**

`(NULL,'about')`、`(1,'about')`、`(2,'about')` **允許併存**；某個站台的 `/about/` 對應哪一筆，
由查詢的優先順序解決——**俱樂部專屬優先，沒有才回退共同內容**：

```sql
WHERE slug = @slug AND (club_id = @club_id OR club_id IS NULL)
ORDER BY CASE WHEN club_id IS NULL THEN 1 ELSE 0 END
```

索引 `(slug, club_id)` 支撐這個查詢。連帶得到的行為是**共同內容可被單一俱樂部覆寫**。
不採「任何俱樂部都不得再有同名 slug」的強讀法——那無法用唯一索引表達、要加並行安全的觸發器，
**維護成本高過它擋掉的風險**（頁面數十筆、由自己人在後台維護）。

### 資料庫層級與容量

兩個資料庫皆先開 **Basic（5 DTU／2 GB，約 US$5／月）**。

- 🔴 **2 GB 是硬上限，寫滿就寫入失敗**（不像 DTU 只是變慢）。**必須設定儲存空間告警在 1.5 GB。**
  圖片在 Blob，主要成長來源是 `Order`、`EmailLog`、`InventoryMovement`、`Donation` 與各 `*_i18n` 側表。
- **Blob 的成長受 §4.0 的縮圖規定壓住**：上傳的原始檔**不保留**，只存長邊 ≤ 2560px 的 WebP 主檔
  ＋ 1280／640／320 ＋ 160px 方形縮圖，**一張圖約五個物件、量級是原始檔的數分之一**。
  衍生檔在**伺服器端寫入時一次產完**（`ImageSharp`），不走 on-the-fly 轉檔，因此 CDN 命中率高、VM 不承擔轉檔尖峰。
- DTU 風險已由 Redis 大幅緩解，但**上線前仍要壓測一次**。
  層級變更是線上作業，不足即升 S0（10 DTU／250 GB，約 US$15／月）。

### 型別對照與其他定案

| 項目 | 定案 | 依據 |
|---|---|---|
| **主鍵策略** | **`id uniqueidentifier` 非叢集主鍵 ＋ `bigint IDENTITY` 叢集鍵**（每表加一欄，不對外）。用 EF Core convention 統一套用 | 見下方說明 |
| 物理表名 | snake_case 複數（`product_variants`） | `12` §1.2「選型時一併定案」 |
| `string(n)` | **`nvarchar(n)`**（Unicode 字元數，非位元組） | `12` §1.1 |
| `text` | `nvarchar(max)` | |
| `int` 金額 | `int`，單位元，**不用 `decimal`** | `12` §1.3 金額規則 |
| `decimal(5,2)` | `decimal(5,2)`，**只有分潤百分比用** | `12` §1.3 |
| `datetime` | **`datetime2(3)` 存 UTC**（SQL Server 無 `timestamptz`），EF Core 設 `DateTimeKind.Utc` convention | `12` §1.1 |
| `enum` | **`nvarchar` ＋ CHECK 約束**，不用查表也不用數字碼——值域演進最容易，且後台介面要顯示日常中文（[`06`](06-conventions.md)） | `12` §1.1「屬選型決定」 |
| 定序 | 建庫時指定；🔴 **建庫後不可改，必須一次定對** | |
| 排程 | **Azure SQL 無 SQL Agent**。排程發布、逾時取消訂單、每日對帳一律由 .NET 的 hosted service 承擔。**排程發布（新聞）已於 S0-7g 接上**：條件式 `UPDATE ... OUTPUT`（`status='scheduled' AND published_at<=SYSUTCDATETIME()` → `published`），對多個 API 容器同時執行天生冪等（不需分散式鎖），預設每 60 秒掃一次（`SCHEDULED_PUBLISH_INTERVAL_SECONDS`），轉換成功立刻呼叫 `IQueryCache.InvalidateAsync`，不是純靠快取 TTL 兜底。實作與驗收見 [`apps/api/README.md`](../apps/api/README.md)「排程發布：時間到了自動轉為 published」與「S0-7g 驗收紀錄」 | |
| 影像處理 | **ImageSharp**（年營收 100 萬美元以下適用 Apache 2.0，俱樂部與協會均符合；**此前提要記錄**）。日後若超過門檻改 SkiaSharp | `STATUS.md` S0-8 需伺服器端產 WebP 與多尺寸 |

> ✅ **HEIC 已定案（2026-09-20）：走第 ① 條——前端在瀏覽器轉成 JPEG 再送。**
> 理由：② 伺服器端加 HEIF 解碼元件會牽進 **HEVC 專利授權**，一個俱樂部官網不值得進這個坑；
> ③ 改成不收 HEIC 要走同步鏈改規劃書，而且對 iPhone 使用者不友善——規劃書 §4.0 明文接受 HEIC 是對的。
> 🔴 **但伺服器端仍要擋**：收到解不開的檔一律以「格式不支援」回絕，**不得假設前端一定轉過**
> （前端可能失敗、可能被繞過）。前端轉檔是為了體驗，伺服器端驗證是為了正確性，兩者都要。
>
> 以下為原始的三選一說明：
>
> 🔴 **ImageSharp 不解 HEIC／HEIF，但規劃書 §4.0 明文接受 HEIC。** iPhone 拍的照片預設就是 HEIC，
> 而後台的實際使用者多半用 iPhone。三條路：
> ① 前端在瀏覽器就把 HEIC 轉成 JPEG 再送（多一個前端相依，但伺服器端最單純）；
> ② 伺服器端加一個 HEIF 解碼元件（要一併確認授權與 **HEVC 專利**）；
> ③ 規格改為**不收 HEIC**，後台上傳欄位明說只收 JPG／PNG／WebP（**要走同步鏈改規劃書**，不能只改這裡）。
> ⚠️ Safari 透過 `<input type="file">` 上傳時**多半**已自動轉成 JPEG，但**不能當成保證**——拖曳與部分版本仍會送出原始 `.heic`。

> **🔴 為什麼主鍵要拆成兩層。** 綱要規定 ~108 張表全部 `id uuid`、明文不用自增整數。
> SQL Server 的 `uniqueidentifier` **比較位元組的順序是反的**——先比 byte 10–15，byte 0–3 擺最後。
> UUIDv7 把時間戳放在 byte 0–5，正好是最低優先的那段，**所以 UUIDv7 在 SQL Server 的索引行為
> 幾乎等同 UUIDv4**，照樣頁分裂。（這招在 PostgreSQL 有效，在這裡無效，不要照搬別處的經驗。）
> `NEWSEQUENTIALID()` 由伺服器端產生，應用層無法在 INSERT 前先知道 id（影響批次匯入與關聯建立），
> 且 **Azure SQL 故障移轉後會產生新的序列叢集**。因此改用非叢集主鍵 ＋ 獨立叢集鍵。
>
> **這不違反 `12` §1.2 的「不用自增整數」**——那句針對的是對外識別碼（避免匯入與跨環境搬移撞號）。
> 新增的 `bigint` 叢集鍵**不對外、不進 API、不進 URL**。

### 本機開發資料庫（S0-6c，2026-09-21；**2026-09-21 併入既有容器，`deployment-engineer`**）

🔴 **這一段已因使用者拍板而反轉一次方向，讀最新結論即可，不必照時間順序理解演變過程**：
本機開發資料庫現在建在**宿主機上既有、非本專案 compose 管理的 `sqlserver` 容器**裡，
`docker-compose.dev.yml` 原本自己起的 `mssql-dev` 服務**已移除**。理由與完整操作方式見
[`../deploy/README.md`](../deploy/README.md)「本機開發資料庫已合併進既有的 `sqlserver` 容器」，
本節只記錄對這份文件（拓撲、DBMS 選型）而言重要的事實：

- **兩個庫是同一個既有 SQL Server 2022 instance 裡的兩個獨立 database**
  （`tcrfc_club_dev`、`tcrfc_charity_dev`），這件事本身沒變——變的只是「這個 instance
  是本專案自己開的容器」變成「這個 instance 是使用者另一個專案原本就在用的既有容器，
  本專案借用來多開兩個資料庫」。**跟正式環境（兩個完全獨立的 Azure SQL 單庫）的落差因此
  多了一層**：本機不只是「兩庫同 instance」，還是「同 instance 裡混了其他專案的資料庫」——
  這一層在正式環境完全不存在，純粹是本機省資源與共用既有容器的產物，見
  [`14-invariants.md`](14-invariants.md)。
- ⛔ **這個既有容器沒有掛任何 volume**（資料在容器可寫層），跟舊 `mssql-dev` 的具名 volume
  `mssql_dev_data`（已隨服務移除而停用，是否清掉由使用者決定）不同。這代表容器若被重建，
  TCRFC 的兩個開發庫會跟著消失，但因為本來就是可重新產生的 DDL＋種子資料組合，復原成本低；
  真正的風險在於**這個容器同時裝著使用者另一個專案的資料**，那些資料沒有這條復原路徑。
- **灌完 DDL 後的表數／外鍵數與 S0-6b 記錄的數字一致**（主站 144 表／380 外鍵／1 視圖、
  慈善 29 表／65 外鍵），**併入既有容器後已重新驗證一次、數字不變**——證明合併沒有改變
  綱要本身，只換了資料庫所在的 instance。每次重灌都可以拿這組數字回歸比對。

**主站庫已有種子資料**（`db/seed/`，見 [`../db/seed/README.md`](../db/seed/README.md)）：讀
`site/src/data/*.json`（mockup 用的球員／新聞／賽程等六個 JSON）產生冪等 T-SQL 灌入，涵蓋
`clubs`（台中磐石＋台中藍鯨主檔）、`teams`（僅 D1）、`players`、`staff`、`matches`、`articles` 等表。
**慈善庫沒有種子來源，維持空表。**

**S0-9c 已拍板（2026-09-21，使用者指示）**：前台改 Nuxt 的資料串接**不做「讀 JSON 的 Nitro 過渡層」**，
直接由本機這套資料庫供真實資料，Nuxt 端一律走 `useFetch` 打 `.NET API`（等 API 專案骨架建立後接上）。
原本 `STATUS.md` S0-9c 列出的兩個選項（過渡層 vs 直接打 API）**選了後者**——理由是過渡層是一次性投入
但之後要整個丟掉重寫，而本機資料庫現在已經是可持續使用的環境，直接對真實綱要開發可以及早暴露
`site/src/data/*.json` 與資料表之間的欄位落差（見 `docs/12d-field-audit.md` 本次新增的項目），
比事後才發現划算。

⛔ **本機開發、種子資料與跨庫查詢的所有紀律**（既有 `sqlserver` 容器本身不得碰、只能在裡面
建 TCRFC 這兩個資料庫、兩庫必須各自獨立不得跨庫 JOIN）不變，完整版見
[`../deploy/README.md`](../deploy/README.md)。

### 本機開發物件儲存（S0-8，2026-09-22；`backend-engineer`）

圖片上傳共用元件（規劃書 §4.0，見 [`apps/api/README.md`](../apps/api/README.md)「圖片上傳共用元件」）
正式環境接真正的 **Azure Blob Storage**，本機開發接 **Azurite**（Microsoft 官方的 Azure Storage 模擬器）：

- `docker-compose.dev.yml` 新增 `azurite` 服務（`mcr.microsoft.com/azure-storage/azurite:3.35.0`，
  只跑 Blob 服務），對外發布 `127.0.0.1:10000`——跟 `sqlserver` 容器對外發布 `1433` 同一類刻意例外
  （Azurite 沒有機密可洩漏：帳號金鑰是 Azurite 專案公開文件的固定值，不是真正密鑰）。
- `api` 服務容器化跑法（本機 compose 或正式 VM）連 `azurite` 這個服務名稱；**直接 `dotnet run`
  （不經 Docker）連 `127.0.0.1:10000`**（`AZURE_BLOB_CONNECTION_STRING=UseDevelopmentStorage=true`
  這個 Azure SDK 內建的簡寫值，本質就是展開成 `127.0.0.1:10000/10001/10002`）——跟 `sqlserver`
  的「容器內用 `host.docker.internal`、宿主機直連用 `127.0.0.1`」是同一種本機／容器化二選一，
  但反過來：**Azurite 是這份 compose 自己起的服務**（用服務名稱連），`sqlserver` 是宿主機上
  既有、非本專案 compose 管理的容器（用 `host.docker.internal` 連）——兩者連線方式不同的原因
  不是「哪個是資料庫哪個是物件儲存」，是「這個依賴由誰啟動」。
- 🔴 **`Azure.Storage.Blobs` SDK 版本可能比本機 Azurite 認得的 API 版本新**（本機實測踩過：
  SDK 12.29.2 預設送 `2026-06-06`，Azurite 3.35.0 尚未支援，回 400
  `The API version ... is not supported by Azurite`）。兩個服務定義都加了
  `--skipApiVersionCheck`——這是 Azurite 官方文件記載的本機開發解法，**只用在本機／測試，
  不是正式環境的行為**（正式環境是真正的 Azure Blob Storage，沒有這個旗標）。
- 本機沒有 Docker（或想更快的開發迴圈）時可用 `npm install -g azurite` 後執行
  `azurite-blob --blobHost 127.0.0.1 --blobPort 10000 --skipApiVersionCheck --location <任一目錄>`，
  效果等價。`Tcrfc.Api.Tests` 的整合測試（`AdminWriteAzuriteEnabledApiFixture`）就是用這個
  npm 套件裝的 `azurite-blob` 執行檔，不用 Docker（沿用本專案「Redis 測試 fixture 用
  `brew install redis` 而不是拉 `redis:8-alpine`」同一個理由：這個沙盒環境拉 Docker Hub 映像檔慢，
  但 `mcr.microsoft.com/azure-storage/azurite` 本身經實測拉取正常，只有 `docker-compose.dev.yml`
  常態跑的時候才用容器版本，測試追求的是啟動速度用 npm 套件版本）。

### Hero 輪播影片上傳（v3.14，2026-09-24；`backend-engineer`）

主站規劃書 §4.2 B3「Hero 輪播管理（排序、圖／影片、標題、CTA、上架期間）」原本只有圖片欄位可用，
v3.14 使用者拍板開放影片；規劃書本身不記技術選型（§1.3 明文排除），格式、大小上限、是否轉碼
三件事由本輪執行層決定：

| 議題 | 定案 | 理由 |
|---|---|---|
| 格式 | **只收 MP4（H.264／AAC）** | 這是瀏覽器原生 `<video>` 元素支援度最高、最不需要額外相容性處理的組合；不像圖片上傳規劃書明文接受 HEIC 那種既有相容性負擔（見上方「HEIC 已定案」段），影片沒有對應的規格壓力要求收更寬 |
| 大小上限 | **50 MB** | Hero 輪播是短片，不是完整賽事錄影；50 MB 大約是 1080p、30 秒上下、中等位元率的匯出品質，一般社群媒體剪輯工具的匯出檔落在這個範圍內 |
| 是否轉碼 | **伺服器端不轉碼，原封不動寫入物件儲存** | 這套系統跑在單一 Azure VM（本檔 §1），沒有另外的轉碼佇列或算力可以吸收 ffmpeg 這類工具的 CPU 成本，勉強做只會拖垮同一台機器上的 API／DB 容器。跟圖片上傳（伺服器端一律重新編碼為 WebP 並產四個衍生尺寸）刻意不同——圖片轉檔用 ImageSharp 是純 CPU、毫秒等級；影片轉碼是分鐘等級的重工作，量級不同 |

**驗證機制**（`apps/api/Videos/`）：

- `VideoValidator.Validate`：以 **`ftyp` box 檔頭**（ISO Base Media File Format 容器格式共用的
  magic bytes，位元組 4–7 應為 ASCII `"ftyp"`）判斷是否為 MP4 容器，**不看副檔名**——逐字比照
  圖片上傳「以 `ImageSharp` 偵測實際格式，不信任副檔名」的既有原則。
- 🔴 **已知邊界（誠實列出，不是遺漏）：這只驗證容器格式，不解封裝驗證內部視訊／音訊軌道是否真的
  是 H.264／AAC。** 完整驗證編碼需要引入媒體處理函式庫（例如綁定 ffprobe），本專案刻意不引入
  ——跟「伺服器端不轉碼」同一個理由：不值得為了一個俱樂部官網的 Hero 輪播負擔這個相依與維運成本。
  代價是理論上一個容器是合法 MP4、但內部編碼是其他格式（例如 HEVC／VP9 塞進 MP4 容器）的檔案
  能通過伺服器驗證，但瀏覽器播放時可能失敗——這個風險由後台人員自律（用一般匯出工具產生的 MP4
  幾乎必然是 H.264／AAC）與人工預覽（後台上傳後應該試播確認）承擔，不是伺服器強制保證。
- **必須搭配海報圖**：`banners.image_key` 是既有欄位、`NOT NULL`，`media_type='video'` 時作為
  影片的海報格（poster）——`<video poster>` 屬性、影片載入前與行動網路關閉自動播放時的預覽畫面。
  這不是新規則，是沿用既有「一張圖片欄位」的資料結構，只是語意從「輪播圖本身」變成「影片的
  縮圖」。
- 大小檢查（`IFormFile.Length`，只讀 metadata）先擋超額檔案，格式驗證留給
  `IVideoStorageService.UploadAsync` 內部——跟圖片上傳同一種「先擋大小、格式驗證在真的處理
  位元組時做」的分工。

**儲存**（`Videos/BlobVideoStorageService.cs`）：跟圖片共用同一個 Azure Storage 帳號、**獨立容器**
（`AZURE_BLOB_CONTAINER_VIDEOS`，預設 `videos`；圖片是 `AZURE_BLOB_CONTAINER_IMAGES`，預設
`images`）——分開容器方便未來各自套用不同的保留政策或 CDN 快取規則。本機開發同樣接 Azurite
（跟圖片一樣），不需要另外的模擬器。**不產生任何衍生檔**（跟圖片「一張圖五個物件」刻意不同）：
收到的位元組原封不動存成一個物件，物件鍵格式 `{club}/banners/{id}/video/{guid}.mp4`。

**Kestrel 請求主體上限**（`Program.cs`）：因為 Hero 輪播「影片」模式在同一次 `multipart/form-data`
請求裡同時送海報圖（≤10 MB）與影片（≤50 MB），上限已調整為兩者總和加緩衝
（`ImageUploadOptions.MaxUploadBytes + VideoUploadOptions.MaxUploadBytes + 1 MB`），不是只算
圖片那組數字。

**API 契約**：`banners` 的 `MediaType` 欄位開放 `video`（原本 S1-7a 只開放 `image`，`video` 送出
一律 400），建立／更新時多帶一個 `video` multipart 欄位（`file` 欄位固定是海報圖）；影片模式下
`video` 是否必填視情境而定，見 `apps/api/README.md` S1-7b 段的完整說明。**草稿／發布**
（`banners.status`）與影片上傳是同一輪一起接的兩件事，但彼此獨立——草稿狀態的輪播一樣可以是
影片模式，只是不會出現在公開端點。

---

## 7. 已知風險

| # | 風險 | 成因 | 緩解 |
|---|---|---|---|
| 1 | **宿主機與 API 單點故障** | 一台 VM、一個 API 行程承載五個平台與兩個收款主體 | 資料在 Azure SQL 與 Blob（不隨 VM 毀損）；備份與重建程序必須演練並記錄 RTO |
| 2 | **兩法人憑證同行程** | 協會與俱樂部的 LINE Pay 憑證與資料庫連線字串在同一個 .NET 行程 | `DbContext` 完全分離、慈善 context 不含俱樂部型別、設定來源分離、不啟用 Elastic Query、慈善不接 Redis |
| 3 | ~~West US 2 延遲~~ **已於 2026-09-20 改為 Japan East** | 台中↔東京 RTT 約 **30–50ms**（原美西 130–160ms）。SSR 首屏仍須往返，但成本降到約三分之一 | Cloudflare 快取內容頁 ＋ `stale-while-revalidate` 仍照做；結帳流程壓低往返次數仍照做 |
| 4 | **出口 IP 綁死機器** | 換 VM／換區域即需改 LINE Pay 白名單 | 靜態 Public IP 獨立於 VM 生命週期；變更視為需事前申請的停機事件 |
| 5 | **個資跨境存放** | 會員與**捐款人**個資存於日本（VM、Azure SQL、Blob 全在 Japan East） | ⚠️ **法務待確認**：個資法的跨境傳輸限制，以及協會與俱樂部間的委託處理約定須載明境外存放。⚠️ **改日本不等於解除這條**——仍是境外，只是接受度可能與美國不同，**結論要法務給不是我們推定** |
| 6 | **Basic 層 2 GB 硬上限** | 寫滿即寫入失敗（非降速） | 儲存空間告警設在 1.5 GB；層級變更是線上作業，可即時升 S0 |
| 7 | **快取陳舊造成錯誤決策** | 有人把 §4 禁用清單裡的資料加進快取 | 清單於 [`12`](12-database-schema.md) §12 與 [`14`](14-invariants.md) 交叉引用；write-invalidate ＋ TTL 兜底；驗證項逐條實測 |
| 8 | **G-02 搜尋第一期不完整** | `LIKE` 比對做不到分類篩選與關鍵字高亮 | 登記為已知落差；升級路徑為 Azure SQL 內建全文檢索 |
| 9 | ~~區域延遲與出口 IP 的時序耦合~~ **已解除** | **2026-09-20 於申請商店號前改為 Japan East**，正好趕在登記出口 IP 之前定案 | ✅ 此後再遷區域仍要改 LINE Pay 白名單，成本照舊高——**視同停機事件，不要再動** |
| 10 | **App 在 API 全滅時無法宣告維護中** | 用來宣告「維護中」的設定端點與 API 同一個行程 | App 的設定、最低支援版本與維護模式另有一份**靜態備援放在 Cloudflare**（Workers KV／R2），不經 VM；強制更新畫面的版面與雙語文案打包進 App。見 [`19`](19-app-tech-stack.md) §7

---

## 8. 本檔不決定的事

- ~~網站與 API 的 CI 管線~~ ✅ **已規劃於 [`20-cicd.md`](20-cicd.md)**（2026-09-20）：GHCR ＋ VM 上的 self-hosted runner ＋ 需人工核准的資料庫遷移關卡。**workflow 檔尚未撰寫。App 的兩條管線見 [`19-app-tech-stack.md`](19-app-tech-stack.md) §9**
- **Azure SQL 定序的具體值** —— 建庫前定，建庫後不可改
- **Redis 是否需要持久化** —— 採 cache-aside 後可視為純快取，預設不開 AOF；若日後拿它存 session 再重新評估
- **各 entity 的快取 TTL 實際值** —— §4 只定了「先做共用小資料」的順序，數值待量測後定
- **快取值的序列化格式** —— `System.Text.Json` 應足夠，但未定案

---

## 9. 驗證程序

上線前逐條實測，結果留存。

| # | 驗證 | 通過條件 |
|---|---|---|
| 1 | **出口 IP 正確** | 從 `api` container 內 `curl https://ifconfig.me`，回傳值 ＝ Azure Portal 上的靜態 Public IP |
| 2 | **靜態 IP 與 VM 解耦** | Public IP 是獨立資源、SKU 為 Standard、指派方式為 Static |
| 3 | **服務端點未影響出口** | 啟用服務端點後**重跑第 1 項**，出口 IP 不變。⚠️ 出口 IP 一旦改變，金流會在沒有明顯錯誤訊息的情況下整個失效 |
| 4 | **PaaS 只收 VNet 流量** | 從 VM 以外的來源用相同連線字串連 Azure SQL **必須被拒**；兩個資料庫與儲存體帳戶都要測 |
| 5 | **跨庫邊界成立** | 在 `api` 內以三段式名稱從慈善庫查俱樂部庫的表**必須失敗**；確認訂閱中**未建立任何 External Data Source／External Table** |
| 6 | **Redis 邊界成立** | Redis 未對外開啟連接埠；慈善的程式碼路徑**沒有任何 Redis 相依** |
| 7 | **快取 fallback 生效** | 清空 Redis（`FLUSHALL`）後，前台所有頁面**照常顯示**（只是變慢）。🔴 **這是驗證「沒有把快取當資料庫」的唯一方法** |
| 8 | **禁用清單生效** | 後台改庫存後前台商品頁**立即**反映；重新產生會員卡 token 後舊 token 的 `/m/<token>` **立即**失效 |
| 9 | **白名單生效** | 用正式商店號打一支唯讀 API 不被拒；刻意從未登記的來源打一次，**必須**被拒（證明白名單真的在作用，而非尚未生效） |
| 10 | **主鍵策略生效** | 寫入十萬列測試資料，叢集索引碎片率維持低檔（對照組：不加 `bigint` 叢集鍵的同結構表） |
| 11 | **資料庫層級足夠** | 對首頁與新聞列表壓測，觀察 DTU 使用率與查詢等待；儲存空間告警已設定 |
| 12 | **`noindex` 未遺失** | 回應標頭含 `X-Robots-Tag: noindex, nofollow`，`robots.txt` 為 `Disallow: /` |
| 13 | **上線前三層防護生效**（§10.4） | 未持憑證訪問三個公開前台的 stg 網址得到 401；`curl -I` 任一 stg 網址含 `X-Robots-Tag: noindex, nofollow, noarchive`；兩個後台網址（stg 與正式）同樣含此標頭 |
| 14 | **cookie 不跨網域環境**（§10.5） | 登入 `admin-stg.{$TCRFC_DOMAIN}` 後，瀏覽器開發者工具檢視 Set-Cookie 標頭**不含 `Domain` 屬性**（或使用 `__Host-` 前綴）；手動在瀏覽器把該 cookie 的 domain 改成 `.{$TCRFC_DOMAIN}` 重送請求到正式後台，**必須被拒** |
| 15 | **切正式網址後 `SITE_ENV` 已改回 `production`、`CADDYFILE` 已取消** | 三個公開前台的 `robots.txt` 不再是 `Disallow: /`；`<head>` 的 `noindex` meta 已移除（呼應全域規定第 5 條）；`llms.txt` 可正常存取；`docker compose config` 顯示 `proxy` 掛的是 `deploy/Caddyfile`（不是 `.prelaunch`），未帶帳密也能正常存取前台 |

---

## 10. 網址：從暫用網址到正式網址的策略

> 承接使用者提問「網址要怎麼設定，因為會先有測試網址，最後才會有正式網址」。
>
> 🔴 **全專案只有兩套環境：本機開發（`docker-compose.dev.yml`）與正式 VM（`docker-compose.yml`）。**
> **本節不是第三套環境**，是同一台正式 VM、同一批容器、同一個資料庫，在藍鯨網域
> （[`STATUS.md`](../STATUS.md) B-4）與慈善網域（B-7）尚未到位、主站尚未從 Wix 切換前的
> **暫時設定**。這個階段與正式期的差異**全部在 `.env` 的值**（`SITE_ENV`、`CADDYFILE`、六個網域），
> **沒有第二份 compose 檔、指令一字不差**——這是 2026-09-21 定案的做法
> （原本規劃過的 `docker-compose.staging.yml` override 已撤銷，理由見 [`18`](18-work-errors.md) `E-13`）。
>
> 跟 [`20-cicd.md`](20-cicd.md) §1「不建持久 staging 環境」講的是兩件不同的事——那一條講的是
> **不另建一套基礎設施**做 CI 用的一次性整合測試環境；本節講的是**這一套正式基礎設施本身**，
> 在網域到位前先怎麼跑。兩者不衝突，且**都不會產生一個叫 staging 的環境**。
>
> 本節只規劃網址策略。**不開 Azure 資源、不改 DNS、不碰 Cloudflare、不真的部署**——
> 下面的檔案異動全部是 repo 內的設定與程式碼骨架，執行時才需要真的動手做 DNS／Cloudflare 操作。

### 10.1 上線前的暫用網址（已定案）

一律用已持有、DNS 自控的 `tcrfc.tw` 子網域，**不臨時申請新網域、不用第三方免費子網域服務**：

| 服務 | 上線前暫用網址 | 對應正式網址（六個中五個尚未定案） |
|---|---|---|
| 主站前台 | `stg.tcrfc.tw` | `tcrfc.tw` 或 `www.tcrfc.tw`——**尚未定案**，見 §10.6 |
| 藍鯨官網前台 | `bw-stg.tcrfc.tw` | 藍鯨自己的網域，擋在 B-4 |
| 慈善平台前台 | `charity-stg.tcrfc.tw` | 慈善自己的網域，擋在 B-7 |
| 官網共用後台 | `admin-stg.tcrfc.tw` | `admin.tcrfc.tw`——**不受主站切換影響，可提前定案**（見 §10.7） |
| 慈善獨立後台 | `admin-charity-stg.tcrfc.tw` | 依慈善網域決定 |
| API | `api-stg.tcrfc.tw` | `api.tcrfc.tw` |

**選這條路的理由**：`tcrfc.tw` 本身的 DNS 控制權已在手上（不像藍鯨與慈善還在等網域），子網域的 TLS 憑證用
既有的 Caddy 自動 HTTPS 機制照樣簽得出來，且**切換時只需要改 `.env` 的值再重啟 `proxy`**——
不需要換基礎設施、不需要換資料庫、不需要換 CI 設定。這正是 [`13-blue-whale-site.md`](13-blue-whale-site.md) §6
紀律 7、8「網域只能在 `docker run` 階段給」這個既有設計換來的紅利：**上線前到正式期的切換，本質上只是換一次環境變數的值**。

⚠️ **暫用網址不是「假資料的沙盒」**——它跑的是同一套 Azure SQL、同一套 Blob、同一套 Redis（除慈善外）。
這正是「只有兩套環境」的必然結果：暫用網址背後就是正式資料庫。
若在這個階段就對客戶／真實使用者開放（例如讓客戶用它試填表單、試辦會員），那些資料就是**未來正式站的真實資料**，
不會在切換時自動清空。**這件事目前沒有定案**，列入 §10.9 待決事項。

### 10.2 「網址只能有一個真實來源」的落實：全系統盤點

⛔ **任何一處寫死網址都是錯的**。下表逐一指出真實來源；標「外部、只能手動改」的是誠實的例外——
那些是第三方服務自己的設定介面，本系統管不到，只能列入上線檢查表。

| 出現位置 | 真實來源 | 備註 |
|---|---|---|
| Nuxt canonical／sitemap／`hreflang` | `NUXT_PUBLIC_SITE_URL`（`docker run` 階段給，⛔ 絕不在 `docker build` 帶，[`13`](13-blue-whale-site.md) §6 紀律 7、8） | `@nuxtjs/seo` 統一從這個值算，已實測（S0-9b） |
| `robots.txt`／`llms.txt` | 同上 `NUXT_PUBLIC_SITE_URL` ＋ **本次新增** `NUXT_PUBLIC_SITE_ENV`（`prelaunch`／`production`，見 §10.4） | 網域來自 `SITE_URL`，允不允許被索引來自 `SITE_ENV` |
| Schema.org 輸出 | 同 `NUXT_PUBLIC_SITE_URL`（`@nuxtjs/seo` 的 schema-org 子模組吃同一個 `site.url`） | 不另外設定 |
| 系統信裡的連結 | **缺口，本次補上**：`api` 容器過去完全沒收到任何網域環境變數（見 §10.8），已在 `docker-compose.yml` 補上六個網域變數，供 `backend-engineer` 建立 `api` 專案時組信件連結用 | 系統信連結若寫死主機名，上線前寄出的信全部指向錯的網址 |
| LINE Login callback | 🔴 **外部、只能手動改**：LINE Developers Console 的 Callback URL 清單 | **建議同一個 Channel 同時登記 stg 與正式兩組 callback URL**（LINE 允許一個 Channel 有多筆），不要為上線前另開一個 Channel——否則兩邊的 LINE 綁定關係不通，上線前綁定過的帳號到正式站要重綁一次 |
| LINE Pay `confirmUrl` | 由 `NUXT_PUBLIC_SITE_URL` 組出，**不需要外部登記網址本身**（`confirmUrlType: CLIENT`，[§3](#3-line-pay-的固定-ip)） | 需要外部登記的是**出口 IP**，不是網址；兩者是分開的兩件事，不要混為一談 |
| 電子發票服務 callback | 🔴 **外部、只能手動改**：發票服務商後台登記的通知網址 | 待 B-10（俱樂部 LINE Pay 商店號與發票管道）取得帳號後才有介面可設定，**現在無法預先準備** |
| 會員卡 `/m/<token>` | 同 `NUXT_PUBLIC_SITE_URL`（是主站路由的一部分，不是獨立設定） | 連結本身零成本可改，**但一旦印出或寄出就不可逆**，見 §10.3 |
| App `apple-app-site-association`／`assetlinks.json` | 內容由 `shared/deeplinks.json`（[`docs/19`](19-app-tech-stack.md) §2）產生；**部署到哪個網域是一次性選擇** | 見 §10.3 不可逆類第一項 |
| CORS 允許來源 | **缺口，本次補上**：`api` 容器新增 `CORS_ALLOWED_ORIGINS`，組成同一組網域環境變數（見 §10.8） | 之前完全沒有這個變數，是本次盤點抓到的洞 |
| CSP | 尚未定案（`apps/*` 專案尚未建立）；**建議**沿用同一組網域環境變數 ＋ 固定的第三方清單（LINE、Cloudflare、字型服務等）組出 `Content-Security-Policy` | 留給 `frontend-architect`／`backend-engineer` 建專案時定案，本節只定原則：不寫死、來源與 CORS 同一組變數 |
| cookie 作用域 | **不設定 `Domain` 屬性，或用 `__Host-` 前綴** | 這條的「單一來源」反而是「不要設來源」——host-only 就不會有作用域問題，見 §10.5 |
| 舊官網 128 筆 301 | 後台 `H` 模組的「301 轉址批次匯入」（資料庫驅動） | 見 §10.6，**不建議**另外在 Nitro `routeRules` 或 Cloudflare 端常駐一份 |
| Google Search Console／GA4 Property | 🔴 **外部、只能手動改** | 切網域時需另外用 Change of Address 工具或重新驗證 Property，超出本系統控制範圍，列入上線檢查表 |
| 社群平台／名片等離線素材 | 🔴 **外部、只能手動改，且系統無法自動偵測遺漏** | LINE 官方帳號、FB／IG 簡介連結、Google 商家檔案、既有印刷品——上線檢查表需**人工列出**逐一核對 |

### 10.3 換網址的成本分級表

| 級別 | 項目 | 說明 |
|---|---|---|
| 零成本 | canonical／sitemap／`hreflang`／Schema／`robots.txt`／`llms.txt`／CORS／系統信連結／`/m/<token>` 連結本身 | 全部從 `NUXT_PUBLIC_SITE_URL`／`CORS_ALLOWED_ORIGINS` 這類環境變數算出，**改 `.env` 重啟即生效，不必改程式** |
| 低成本 | DNS 記錄、TLS 憑證（Caddy 自動簽發）、Cloudflare Access／Page Rules 設定 | 有 TTL／簽發等待（分鐘到數小時），但可控、可重來，**不會造成永久性損失** |
| 中成本 | LINE Login callback URL、電子發票服務 callback、Search Console／GA4 Property | 外部服務手動改，部分有生效延遲，但**沒有作廢風險**，改完即可用 |
| 高成本 | 已印刷但尚未派發的品牌素材（倉庫裡的傳單／名片）、社群平台 bio 連結 | 需要人工逐一更新，麻煩但可行，**無不可逆風險** |
| 🔴 **不可逆** | ① **已上架 App 的 Universal Link**　② **已印製的慈善 QR Code**　③ **已發出（系統信寄出或實體卡片印出）的會員卡 `/m/<token>` 連結** | 見下方逐項說明——**這張表存在的目的就是標出這一列** |

#### 🔴 三類不可逆，逐項說明

**① 已上架 App 的 Universal Link。**
`apple-app-site-association` 部署在哪個網域，是 App 二進位檔簽署時的 **Associated Domains entitlement** 的一部分——
綁定的是 Team ID ＋ 網域這組組合，寫入送審的 App 版本裡。事後想換網域，**不是改個檔案內容就好**：
AASA 檔案「內容」（`paths` 清單）可以隨時更新、Apple 的 CDN 會重新抓取，這部分零成本；
但 AASA「放在哪個網域」一旦變了，等於 Associated Domains entitlement 要跟著改，就要出一個新版本送審
（Apple／Google 審查數天到數週），而且**已安裝舊版的使用者不會立刻更新**，網域切換後到使用者全部更新完之間，
深連結會在一段時間內失效或退回網頁回退。**規則：App 送審前，主站（Team ID 已有）與（若含藍鯨深連結）
藍鯨都必須已經是最終網域，不能先用 `stg.tcrfc.tw` 送審再換**——這直接影響 [`19`](19-app-tech-stack.md) §9 的
`AP-9`／`AP-6` 時序。

**② 已印製的慈善 QR Code。**
QR Code 編碼的是固定字串（網址），一旦印出並分發給合作店家，**物理上無法修改**。網域若事後更換，
所有已印出去的 QR Code 全部作廢，須重新印刷、重新分發給每一家合作店家；空窗期內若有人掃到舊 QR，
只能落到「網域已停用」的失效頁面（除非刻意留長期 301，但那本身是額外負擔且有安全疑慮——
掃碼付款的入口被長期轉址，是釣魚攻擊的天然溫床）。**規則與規劃書既有的「未完成社團法人登記前不得印製」
是同一類前提的延伸：慈善網域必須在印製第一批 QR Code 前就已經是最終網域**，這對應 `STATUS.md` 的 `CH-0`／`B-7`。

**③ 已發出的會員卡 `/m/<token>` 連結。**
連結本身走 `NUXT_PUBLIC_SITE_URL`，改網域零成本；但一旦**印在實體會員卡上**或**透過系統信寄給會員**，
物理上同樣無法回收。與 QR Code 同理，事後改網域＝所有已發出的卡與信一次作廢，
且長期轉址一樣有安全疑慮——`/m/<token>` 本身是驗證性質的連結，轉址鏈越長，被偽冒的風險越高。
**規則：會員卡（無論實體印刷或系統信寄發）第一次發出前，主站必須已經是最終網域**，對應 `STATUS.md` 的 `S2-11`。

**這三類的共同用途**：它們是「點火開關」——按下去就回不去，而其他一切（前台內容、後台開發、API 開發、
甚至整段上線前的訪客互動）都可以在 `stg.tcrfc.tw` 之類的暫用網址下先做。**用這張表回答「什麼可以先做、
什麼要等正式網址」**：凡是還沒點火的，用暫用網址儘管做；凡是準備點火的（App 送審、QR 印製、會員卡首次發出），
先確認網址已經是最終版本。

### 10.4 🔴 上線前的站必須真的擋住：三層防護

不能只靠 meta `noindex`。以下三層**缺一不可**，理由各自不同：

| 層 | 做法 | 擋的是什麼 | 擋不住什麼 |
|---|---|---|---|
| 1．HTTP 標頭 | `X-Robots-Tag: noindex, nofollow, noarchive`（Nuxt 端由 `NUXT_PUBLIC_SITE_ENV=prelaunch` 觸發；兩個後台由 `deploy/Caddyfile` **永久**加，不分階段） | 涵蓋**非 HTML 資源**（PDF、圖片）——`<meta>` 標籤只在 HTML 的 `<head>` 有效，標頭則對任何回應都有效 | 只對**遵守規則**的爬蟲有效；不阻止人類訪客、不阻止惡意爬蟲、不阻止連結被分享 |
| 2．`robots.txt` | `NUXT_PUBLIC_SITE_ENV=prelaunch` 時輸出 `Disallow: /`（覆蓋 `GEO-02` 平常「全站允許但排除特定路徑」的正式規則） | 涵蓋**遵守 `robots.txt` 的爬蟲**，包含 `GEO-02` 允許清單上的 AI 爬蟲 | 同上，是**自願遵守**的協議，不是技術屏障 |

> ✅ **這一列的環境旗標切換已於 S1-12（H 模組，2026-09-25 驗收退回後補做）實作**：
> `apps/web/server/routes/robots.txt.ts` 只有 `NUXT_PUBLIC_SITE_ENV` 精確等於 `production`
> 時才輸出允許索引的版本，其餘任何值（含未設定）一律輸出本列描述的封鎖版，見
> `apps/api/README.md`「S1-12」段「驗收退回後補做」小節。**目前 production 分支只有基本
> 允許索引＋後台線上編輯的自訂規則，尚未包含這一列括號裡提到的 `GEO-02` 逐一 AI 爬蟲允許
> 清單與排除路徑**——那屬於 `S1-12b`（依 `STATUS.md` 排程），等那張票做完再擴充。
> ⚠️ **第 1 層（`X-Robots-Tag` 標頭）目前仍是無條件套用，沒有跟著這個變數切換**——真正上線時
> 兩層要一起由本節「上線前三層防護」的完整程序處理，不是各自獨立切換。
| 3．存取控制 | 三個公開前台（stg）：**HTTP Basic Auth**（`deploy/Caddyfile.prelaunch`，由 `.env` 的 `CADDYFILE` 指定，見 §10.8）；兩個後台（stg）：**建議 Cloudflare Access**（在 Cloudflare 端設定，email 一次性驗證碼，不改本檔案） | **真正的技術屏障**：沒有帳密／沒通過 Access 政策，連 HTML 本身都拿不到——爬蟲擋得住，意外分享的連結也擋得住 | 若設定有疏漏（例如忘記幫新開的子網域套用同一組保護），這層可能出現漏洞 |

**為什麼三層都要，不能只做第 3 層**：
第 3 層是唯一的技術屏障，但**它是靠人維護的設定**——漏了一個網域、Access 政策設錯放行條件，
第 3 層可能悄悄失效。第 1、2 層是**寫進應用程式與代理層設定裡的預設值**，就算第 3 層某處出錯，
守規矩的爬蟲仍然會被第 1、2 層擋下——這是 defense-in-depth，不是重複勞動。
**⚠️ 第 3 層沒設好時會「大聲失敗」**：`PRELAUNCH_BASIC_AUTH_HASH` 在 `docker-compose.yml` 裡是可為空的
（正式期用不到），所以掛了 `Caddyfile.prelaunch` 卻忘記填帳密時，Caddy 會在啟動時因 bcrypt 雜湊解不出來
而拒絕啟動。**這是刻意的設計**——寧可整個 `proxy` 起不來（立刻被發現），也不要悄悄變成一個沒有帳密保護、
卻自以為受保護的公開站。

**後台不用 Basic Auth 是因為它本來就有自己的登入系統與 2FA**（既有的存取控制），第 3 層改用
Cloudflare Access 是**再加一層在應用程式登入頁之前**的網路層防護，理由同上。

**為什麼公開前台用 Basic Auth 而不是 Cloudflare Access**：這個階段的公開前台要給客戶窗口與非技術團隊成員
（可能沒有 Google／GitHub 帳號可綁 Access 政策）隨時查看，一組帳密最低摩擦；後台的使用者是固定的內部團隊，
Cloudflare Access 的 email 驗證碼摩擦可以接受、防護等級也更高（後台含這個階段累積的真實個資，風險更高）。

#### ⚠️ 被索引後的清理成本

如果站在防護生效前被爬過、被索引：

- **Google Search Console 的移除要求是暫時的**（約 90 天），過後需要重新確認頁面已真的下線／`noindex`，
  否則會重新出現在索引裡；**真正從索引消失，要等 Google 重新爬取並看到 `noindex`**，可能是數週後的事，
  不是提交移除申請就立刻生效。
- **暫用網址與正式網址在切換窗口期間會被判定為重複內容**，如果 canonical 設定有誤（例如上線前 `NUXT_PUBLIC_SITE_ENV`
  忘了關掉 `noindex`，或反過來正式站上線初期 canonical 沒有正確指回自己），Google 甚至可能選錯 canonical，
  用暫用網址代表這個內容——**稀釋、甚至誤導正式站上線後的排名**。
- **若上線前已有真實使用者個資**（會員報名、表單留資）被索引，那已經不是 SEO 問題，是**個資外洩事件**。
- **AI 爬蟲一旦抓取，可能已進入某些訓練資料的快照**，這類抓取沒有「請求移除」的機制可用——
  這正是 `GEO-02` 要求「排除路徑」而不是「事後移除」的原因，上線前的站更需要在第一次部署就把三層防護做好，
  不能想著「先上線，之後再擋」。

### 10.5 🔴 cookie 作用域陷阱

`stg.tcrfc.tw` 與（未來的）`www.tcrfc.tw` 同屬 `tcrfc.tw` 這個註冊網域。**cookie 若設定了
`Domain=.tcrfc.tw`（前面帶點），瀏覽器會把它送到 `tcrfc.tw` 底下的任何子網域**——包含
`stg.tcrfc.tw`、`admin.tcrfc.tw`、`api.tcrfc.tw`，全部共用同一顆 cookie。這代表：**暫用網址的登入
session 會被瀏覽器自動帶到正式站**（反之亦然），即使兩邊接的是完全不同的容器與（理論上）不同的簽章金鑰。

**正確做法（擇一，第二個更保險）**：

1. **完全不要設定 `Set-Cookie` 的 `Domain` 屬性。** 省略 `Domain` 時，瀏覽器預設是 **host-only**——
   cookie 只送回設定它的那個確切主機名，`stg.tcrfc.tw` 設的 cookie 不會被送到 `admin.tcrfc.tw`
   或 `www.tcrfc.tw`。這是最單純、也是本專案**唯一需要**的行為——沒有任何模組需要跨子網域共享登入狀態。

2. **用 `__Host-` 前綴**（例如 `__Host-session`）——瀏覽器層級強制三件事：必須有 `Secure`、
   必須 `Path=/`、**且不得含 `Domain` 屬性**（含了就整顆 cookie 直接被拒收，不是悄悄放寬）。
   這比「記得別設 `Domain`」更保險，因為錯誤設定的後果從「悄悄跨網域生效」變成「cookie 直接失敗、
   當場就看得出問題」，不會等到真的漏出去才發現。**建議後台（`admin-*`）一律採此前綴**——
   後台的 session 一旦跨環境生效，後果比前台嚴重得多。

⚠️ **這條在後台尤其危險，原因是時間軸**：`admin-stg.tcrfc.tw` 與 `admin.tcrfc.tw` 會**同時存在一段時間**
（上線前到主站切換之間，後台不受 Wix 切換時程限制，很可能提前上線，見 §10.7）。如果 cookie 作用域設錯，
**在暫用網址登入過的人，之後打開正式後台分頁會直接是登入狀態**——這不是理論風險，是這段共存期必然會發生的事。

**深一層防禦（給 `backend-engineer` 建 `api` 專案時的提醒，不是本檔能寫的程式碼）**：
即使 cookie 作用域設定不慎放寬，JWT／session 內容裡若綁定簽發時的 host（`aud` claim 或等效欄位），
後端驗證時比對目前請求的 host 是否吻合，可以再擋一次。`docs/20-cicd.md` §7.2 已經把
`JWT_SIGNING_KEY_CLUB` 定為只在 VM 本機的機密——**但這條本身不能假設上線前與正式期一定用不同的簽章金鑰**，
若日後圖方便共用同一把 key，這層防禦就會失效。**建議上線前與正式期的 JWT 簽章金鑰使用不同值**
（即使都存在同一份 `.env`，值本身也要不同），讓 cookie 作用域與簽章金鑰兩層防禦互相獨立，其中一層失守
另一層還在。

### 10.6 主站的切換程序：`www.tcrfc.tw` 現有 Wix 站

> **這是寫給要執行切換的人看的步驟。** 前置確認沒做完，不要進入正式切換步驟。

#### 前置確認（切換前至少一週完成）

1. **查目前 `tcrfc.tw`／`www.tcrfc.tw` 的 DNS 現況**（🔴 目前未知，不可假設）：
   ```bash
   dig tcrfc.tw NS +short
   dig www.tcrfc.tw CNAME +short
   whois tcrfc.tw | grep -i "registrar\|name server"
   ```
   要確認兩件事：**(a) 網域註冊商是誰、登入帳號在誰手上**；**(b) Nameserver 現在是不是指向 Wix**——
   若是，需要先把網域的 Nameserver 改指向 Cloudflare（DNS 代管），**這本身是一次有風險的變更**：
   轉移期間會有 DNS 生效空窗（新舊 Nameserver 交接期間，不同使用者依 DNS 快取可能看到不同結果），
   建議排在離峰時段，且與客戶提前約定時間窗口。

2. **apex 與 `www` 的取捨（🔴 待使用者決定，不可由本檔代為決定）**：
   目前 `.env.example` 的 `TCRFC_DOMAIN=tcrfc.tw` 是 apex（不帶 `www`）；但專案既有文件
   （[`docs/14`](14-invariants.md) 多處）稱既有官網為「`www.tcrfc.tw`」，這是客戶既有品牌識別、
   社群簡介、既有印刷品目前認知的版本。**建議擇一為 canonical**，另一個做永久 301：
   - 選 `www.tcrfc.tw`：與既有品牌識別一致，不用更新其他外部素材；`.env.example` 的
     `TCRFC_DOMAIN` 要改成 `www.tcrfc.tw`。
   - 選 `tcrfc.tw`（apex）：現行 `.env.example` 的既有值，較現代的慣例，但要回頭盤點所有
     已經在用 `www.tcrfc.tw` 的外部素材（名片、社群 bio、既有 Google 商家檔案等）一併更新。
   **這個決定要在切換前定案**——它決定 §10.2 所有以 `NUXT_PUBLIC_SITE_URL` 為源頭的輸出
   （sitemap、Schema、LINE Login callback、系統信連結……）填哪個值，事後更改等於再切換一次。

3. **確認 C-4（既有內容遷移範圍拍板）與 C-5（128 筆 URL 的 301 對應表）已完成**——
   沒有對應表，第 3 步的轉址無從實作。

#### 切換步驟

1. **正式 `.env` 就位**：`SITE_ENV=production`、**`CADDYFILE` 那一行註解掉**（回到預設的
   `deploy/Caddyfile`，取消 Basic Auth）、六個網域依上面第 2 點的決定填正式值。
   **先不接 DNS**，讓新站先用 `stg.tcrfc.tw` 之類的暫用網址完整驗證過一輪（內容、表單、
   金流測試模式等），確認沒有明顯問題再進下一步。

2. **先簽正式網域的 TLS 憑證**（[`deploy/README.md`](../deploy/README.md) 已有詳細步驟，摘要）：
   該筆 DNS 記錄**先設成「僅 DNS」（灰雲，不代理）**直接指到 VM 的靜態 Public IP，
   啟動 `proxy` 容器、等 Caddy 針對該網域成功簽出憑證，確認 Cloudflare SSL/TLS 模式為
   **Full (strict)**，再把記錄改回「代理」（橘雲）。**這一步必須在把 DNS 從 Wix 切過來之前完成**——
   否則使用者在 DNS 生效的那一刻會直接看到 TLS 錯誤（比502／503更糟，多數瀏覽器不給「忽略繼續」的選項）。

3. **128 筆 301**：**建議走後台 `H` 模組的「301 轉址批次匯入」**（主站規劃書已有此功能，
   `STATUS.md` `S1-12` 排在 Phase 1，屆時應已存在）——資料庫驅動、後台可持續維護、符合
   規格「批次匯入」的語意。**不建議**另外在 Nuxt 的 `routeRules`（build 時期寫死，改一筆要
   重新 build＋deploy，不符合「後台可持續維護」）或 Cloudflare Redirect Rules **常駐**一份
   （會變成第二份要維護、兩處不同步就會踩坑的清單——[`docs/18`](18-work-errors.md) 已記錄過
   類似的「兩處不同步」錯誤模式）。**例外**：若切換當下 `H` 模組還沒做完，可用 Cloudflare 的
   redirect 機制**暫時頂著**（實際可用則數依當時的 Cloudflare 方案而定，執行前在 Cloudflare
   後台核對，不要假設一個可能已經過時的數字），**待 `H` 模組上線後撤掉這份暫時的，改回資料庫
   那份，不要兩份長期並存**。

4. **DNS 切換本身**：把 `www.tcrfc.tw`／`tcrfc.tw`（依前置確認第 2 點的決定，其中一個為
   canonical、另一個轉址過去）的記錄指到 VM 的靜態 Public IP，Cloudflare 代理（橘雲）開啟。
   **切換前 24–48 小時把該筆記錄的 TTL 調低**（例如降到 300 秒）——這不影響切換本身，
   但能把「回滾生效時間」從數小時壓縮到數分鐘。**保留舊 Wix DNS 設定的截圖或匯出**，回滾需要用。

5. **切換後立即驗證**：
   - 抽測 CSV 中 10–20 筆有代表性的舊網址（商品頁、新聞頁、分類頁），逐一確認 301 生效且
     目的地正確；
   - 確認首頁、canonical、sitemap 指向的是決定後的 canonical 網址（不是另一個）；
   - `X-Robots-Tag`／`robots.txt` 已經是**正式版**（`NUXT_PUBLIC_SITE_ENV=production`，不是
     `prelaunch` 的全擋）；
   - **Basic Auth 已解除**（`CADDYFILE` 已註解掉）——沒帶帳密也能正常瀏覽，否則正式網域
     一上線就是 401；
   - `<head>` 的 `noindex` meta **已經移除**（全域規定第 5 條：noindex 在正式上線前不移除，
     這一步就是移除的時間點——**不要提前移除，也不要忘記移除**）。

6. **觀察窗口（建議 48–72 小時）**：盯 Cloudflare 的 4xx／5xx 錯誤率、`api`／`nuxt-*` 的健康檢查、
   Google Search Console 是否回報新的爬取錯誤。

#### 回滾程序

若切換後發現嚴重問題（大量 404、金流頁面出錯、後台不可用），**回滾＝把 DNS 記錄改回步驟 4 保留的
原始值（指回 Wix）**。生效時間取決於 DNS TTL——這正是切換步驟第 4 步要求**提前 24–48 小時調低 TTL** 的原因，
把回滾生效時間從「數小時到一天」壓縮到「數分鐘」。⚠️ **回滾只解決「網址回到舊站」**，不解決切換窗口內
已經在新站發生的事（新的訂單、新的會員報名）——那些資料留在新系統裡，回滾後舊站看不到，是後續要另外
處理的營運問題，不是本節能解決的。

#### ⚠️ 舊站還在賣東西：只點風險，不解決（營運決定）

- 切換當下若 Wix 商店仍有進行中的未出貨訂單，**DNS 切走後客服／出貨團隊還能不能登入 Wix 後台處理**？
  （Wix 帳號與網域是否綁定、之後是否續租網域會影響後台存取，需向 Wix 或代管方確認。）
- Wix 綁定的既有金流（若有）在切換窗口內的交易對帳與撥款，要確認去哪裡查——Wix 後台在網域切走後
  可能仍可存取一段時間，但**建議切換前先把進行中的訂單處理完或至少列出清單**，降低交接複雜度。
- 建議：切換前一週公告「即將停止透過官網下單，請盡速完成現有訂單」，並在切換當天前確認 Wix 商店
  沒有進行中的未結訂單。

### 10.7 各平台網址時程表

| 平台 | 現在用什麼網址 | 正式網址必須到位的時點 | 被什麼擋住 | 晚了卡到誰 |
|---|---|---|---|---|
| 主站前台 | `stg.tcrfc.tw`（S1 起可用） | `S3-7`（舊 Wix 商店退場）之前，且須晚於 `C-4`／`C-5`（內容遷移範圍與 301 對應表）完成 | Wix 現況 DNS 確認、apex／`www` 決定、`C-4`／`C-5` | `S3-6`／`S3-7`（商店上線與舊站退場）；App 的主站深連結（`.well-known` 須部署在最終網域） |
| 藍鯨官網前台 | `bw-stg.tcrfc.tw`（`S1-2` 起可用） | **建議在 `BW-0` 之後即切**，不要拖到 App 上架才切 | **`B-4`**（藍鯨網域持有與 DNS 控制權） | `AP-2`（藍鯨 Universal Link）——未解則深連結退回自訂 scheme＋網頁回退，不阻塞 App 上架，但使用者體驗打折 |
| 慈善平台前台 | `charity-stg.tcrfc.tw`（`CH-1` 後可用，**僅供開發測試，不得印上任何對外物料**） | **第一批 QR Code 印製前**（不可逆類第②項） | **`B-7`**（網域／協會品牌資產／法人登記與統編／勸募資格） | `CH-2` 以後全部——即使系統做完，沒有最終網域就不能印 QR、不能真的上線收款 |
| 官網共用後台 | `admin-stg.tcrfc.tw` | **不受主站 Wix 切換時程限制**，`admin.tcrfc.tw` 是全新子網域、與 Wix 無關，**可提前定案並切換**，不需要等 `S3-7` | 無（不擋在既有阻塞清單上） | 提前切對其他平台沒有負面影響，只需確保切之前後台的兩層防護（Cloudflare Access）在上線前就已經是常態，不是切換那天才裝上 |
| 慈善獨立後台 | `admin-charity-stg.tcrfc.tw` | 依慈善網域決定 | `B-7` | 同慈善前台 |
| App | `stg`／`bw-stg` 供開發期測試 | **App 送審前**——主站（Team ID 已有）與（若含藍鯨深連結）藍鯨都必須是最終網域（不可逆類第①項） | 主站：同上主站列；藍鯨：`B-4` | `AP-9`／`AP-6`。⚠️ **`STATUS.md` 現有的 `AP-9` 只寫「填入 Team ID 與 package name 產出關聯檔」，沒有明文「必須是最終網域而非 `stg`」——本節找出的這個缺口已同步補進 `STATUS.md`（見 §10.8）** |

### 10.8 本節新增／修改的設定檔

| 檔案 | 異動 |
|---|---|
| [`docs/17-deployment.md`](17-deployment.md) | 新增本節（§10）；§9 驗證程序補三項（#13–15）。**2026-09-21 修訂**：撤銷 `docker-compose.staging.yml`，改為單一 compose ＋ `.env` 的 `CADDYFILE` 切換；`SITE_ENV` 的值由 `staging` 改為 `prelaunch` |
| [`docs/14-invariants.md`](14-invariants.md) | 新增 cookie 作用域、三類不可逆網域切換兩則不變量 |
| [`docs/20-cicd.md`](20-cicd.md) §1 | 加一段釐清文字：區分「不建持久 CI staging 環境」與本節「上線前的暫用網址」 |
| [`STATUS.md`](../STATUS.md) | `AP-9` 補「必須是最終網域」的條件；`B-4`／`B-7` 加一行指向本節；新增待確認事項（apex／`www`、Wix DNS 現況、上線前累積的資料是否清空） |
| [`.env.example`](../.env.example) | 新增 `SITE_ENV`（值為 `prelaunch`／`production`）；新增暫用網域區塊與正式網域註解；新增 `CADDYFILE`、`PRELAUNCH_BASIC_AUTH_USER`／`PRELAUNCH_BASIC_AUTH_HASH` |
| [`docker-compose.yml`](../docker-compose.yml) | `nuxt-tcrfc`／`nuxt-bw`／`nuxt-charity` 新增 `NUXT_PUBLIC_SITE_ENV`；`api` 新增六個網域變數與 `CORS_ALLOWED_ORIGINS`（過去完全沒有，是本次盤點抓到的缺口）；`proxy` 的 Caddyfile 掛載改為 `${CADDYFILE:-./deploy/Caddyfile}`，並帶入兩個可為空的 Basic Auth 變數 |
| ~~`docker-compose.staging.yml`~~ | 🔴 **2026-09-21 撤銷、檔案已刪除**。理由：本專案只有本機開發與正式 VM 兩套環境，多一個名為 staging 的 compose 檔會讓人以為有第三套，與 [`20-cicd.md`](20-cicd.md) §1 直接牴觸。功能改由 `.env` 的 `CADDYFILE` 達成 |
| [`deploy/Caddyfile`](../deploy/Caddyfile) | 兩個後台網址**永久**加 `X-Robots-Tag: noindex, nofollow, noarchive`（不分測試或正式，後台本來就不該被索引） |
| `deploy/Caddyfile.prelaunch`（新檔，原名 `Caddyfile.staging`） | 三個公開前台加 HTTP Basic Auth（§10.4 第 3 層）。由 `.env` 的 `CADDYFILE` 指定掛載，**不需要 override 檔** |
| [`deploy/README.md`](../deploy/README.md) | 新增「正式網址到位前怎麼起」一節（原名「測試環境怎麼起」，2026-09-21 改名並改寫，強調只有兩套環境） |

### 10.9 待決事項（🔴 不可由本檔代為決定）

| # | 事項 | 為什麼不能由部署層決定 |
|---|---|---|
| 1 | **apex（`tcrfc.tw`）還是 `www.tcrfc.tw`為 canonical** | 牽涉既有品牌識別與外部素材更新成本，是行銷／品牌決定，不是技術決定 |
| 2 | **上線前累積的資料，正式上線時是否清空重置** | 暫用網址背後就是正式資料庫（只有兩套環境的必然結果）。若這個階段就對客戶／真實使用者開放互動（試填表單、試辦會員），這些資料要不要保留、要不要當成正式資料的一部分，是業務決定；若保留，當時的個資蒐集告知與同意是否足夠，還要再確認一次法遵 |
| 3 | **Wix 目前的 DNS 是否已代管於 Cloudflare** | 純屬未知事實，需要有網域註冊商登入權限的人去查，不是能從現有文件推導的 |

---

## 11. `api` 唯讀讀取端點骨架（S0-7b，2026-09-21；S0-7d 補強，2026-09-21）

> 🔵 **執行層決定，不是規格**（同本檔通則）。範圍：球員、教練與職員、新聞、賽程與賽果、俱樂部主檔
> 五組唯讀 GET 端點，讓 `apps/web` 能打真實資料庫。**不含**寫入、登入權限、商店金流、後台、慈善平台的
> 業務功能。**S0-7d（同日）補齊 S0-7b 刻意縮減的三個缺口**：`/readyz` 加慈善庫與 Redis 檢查（第 6 項）、
> `IQueryCache` 接縫接上真正的 Redis 實作（第 3 項）、新增自動化測試專案（第 10、11 項）。
> 完整端點清單、欄位公開性對照、驗收紀錄見 [`../apps/api/README.md`](../apps/api/README.md)，本節只記
> 「日後接其他功能時必須沿用」的執行層決定。

| # | 決定 | 為什麼 |
|---|---|---|
| 1 | **`club_id` 強制機制走型別系統，不是 code review 紀律**：`ClubScope`（`readonly struct`）建構子 `internal`，唯一產生者是 `IClubResolver`；所有 repository 方法簽章要求 `ClubScope` 而非 `Guid`／`string` | §4「五類禁用的 repository 根本不注入快取服務」用的是同一種「型別上做不到」而不是「記得別做」的思路，這裡把它套用到 `club_id` 過濾。**日後任何碰 `club_id` 範圍資料的新端點都必須沿用此模式**，不得自己另開一條「先拿 `club_id` 字串再查」的路 |
| 2 | **9 張 `club_id` 可為空表的「俱樂部專屬優先、回退共同」SQL 片段集中在 `Data/ClubOrSharedSql.cs`** 兩個常數（`WhereClubOrShared`／`OrderClubBeforeShared`） | 對應本檔 §6 第 5 件事定案的 SQL 寫法；**只能有一個真實來源**，比照 §6「單元開關只能有一個真實來源」的同一個精神 |
| 3 | **快取接縫（`Caching/IQueryCache.cs`）：`REDIS_HOST` 有設定注入 `RedisQueryCache`，沒設定注入 `NoOpQueryCache`**（S0-7d，2026-09-21 起真的接了 Redis，取代原本「本次注入 no-op」的敘述） | S0-7b 當時沒有後台可以觸發 write-invalidate（§4 的失效前提），接 Redis 會變成「永遠不失效的快取」；S0-7d 補上後**改以 TTL 頂住這段空窗**（預設 300 秒，可用 `QUERY_CACHE_TTL_SECONDS` 覆寫），並新增 `IQueryCache.InvalidateAsync(entity, club, ct)` 給日後後台寫入層直接呼叫，**介面不用再改**。`GetOrCreateAsync` 改為四個字串維度（`entity`／`club`／`locale`／`qualifier`）組 key，對應 §4「key 命名必須含 club_id 與 locale 維度」；`Caching.CacheDimensions` 提供 `SharedClub`／`AnyLocale`／`NoQualifier` 三個共用常數。⚠️ **現況已不只 `Security/ClubResolver.cs`**（S0-7d 續作，同日，使用者拍板）：`ClubsRepository`／`PlayersRepository`／`StaffRepository`／`ArticlesRepository`／`MatchesRepository` 五個 `Features/*` repository 全部接上快取——之前把「端點與 repository 不得修改」解讀成「連建構子參數都不能加」是過度保守，使用者澄清那句話的本意是「接縫換 no-op↔Redis 不需要動它們」，加一個 `IQueryCache` 建構子參數且對外契約不變是接縫本來就預期的用法。**額外規則**：`factory` 回傳 `null` 不寫入快取（負向結果／404 不快取，見 [`apps/api/README.md`](../apps/api/README.md)）；`articles`／`article-detail` 兩個 entity 因為「排程發布時間到」沒有寫入事件可觸發失效，**排程發布的實際生效時間完全依賴 TTL**，最多延後一個 TTL 週期——這是已知取捨，寫進 README 供日後後台排程發布功能開發時對照 |
| 4 | **對外語系參數固定 `zh`／`en`，資料庫實際存 `zh-Hant`／`en`**，轉換與逐欄位回退規則集中在 `Localization/RequestLocale.cs` | 對齊 [`06-conventions.md`](06-conventions.md)「語系代碼」一節（API 與 App 的 `lang` 一律 `zh`／`en`）；回退規則是「請求語系非空白用它，否則用 `zh-Hant`，兩者皆無回傳 `null`」——**逐欄位判斷，不是整筆記錄二選一**，已用種子資料的真實缺漏（`matches_i18n`／`competitions_i18n` 完全沒有英文列）驗證 |
| 5 | **連線字串環境變數鍵名固定為 `CLUB_SQL_CONNECTION_STRING`**，直接讀（不繞 ASP.NET Core 慣用的 `ConnectionStrings:Club` 間接層） | 與 `deploy/dev/club.env`、`/opt/tcrfc/secrets/club.env`（[`20-cicd.md`](20-cicd.md) §7.2）既有鍵名一致，設定與程式碼不用互相翻譯 |
| 6 | **`/readyz` 已補齊慈善庫與 Redis 檢查**（S0-7d，2026-09-21 起，取代原本「目前只驗證主站庫連線」的敘述）：`club_db` 必檢查失敗即 not ready；`charity_db` 有設定 `CHARITY_SQL_CONNECTION_STRING` 才檢查（沒設定 `not_configured` 不影響 ready，設定了連不上算 `fail` 導致 not ready）；`redis` 有註冊 `IConnectionMultiplexer` 才檢查（沒註冊 `not_configured`，連不上 `degraded`，**兩種情況都不影響 ready**） | 慈善庫檢查只開連線查 `SELECT 1`，不建立任何 repository 或常駐連線工廠——`docs/14-invariants.md`、本檔 §5 明訂不得跨庫存取，這裡連「留一個可被誤用的管道」都不留。Redis 的「連不上不影響 ready」直接對應本節「連線失敗算警告不算失敗」；已用 `docker run` 起真正的映像檔＋`brew install redis` 的真實 `redis-server`，驗證過連線建立前就掛掉、連線建立後才掛掉兩種情境，見 [`apps/api/README.md`](../apps/api/README.md)「S0-7d 驗收紀錄」 |
| 7 | **OpenAPI（`Microsoft.AspNetCore.OpenApi`）只在 `ASPNETCORE_ENVIRONMENT=Development` 掛載，未掛 Swagger UI** | 已用容器實測：`Production` 環境 `/openapi/v1.json` 回 404。日後要加互動式文件頁面（Scalar／Swashbuckle）再疊加，不影響此開關 |
| 8 | ⚠️ **`InvariantGlobalization` 不得開**：`Microsoft.Data.SqlClient` 連線需要完整 ICU，開了會在 `SqlConnection.OpenAsync()` 直接丟 `NotSupportedException` | 本機實測踩到的坑，見 [`18-work-errors.md`](18-work-errors.md) E-19 |
| 9 | ⚠️ **Dapper 用 record 具現化時，SQL `date`／`datetime` 欄位對應的屬性一律宣告 `DateTime`，不要宣告 `DateOnly`**，需要 `DateOnly` 語意在應用層轉 | ADO.NET 對 `date` 欄位回報的 CLR 型別永遠是 `DateTime`，`DateOnly` 屬性會讓 Dapper 找不到相符建構子而整支查詢丟例外。見 [`18-work-errors.md`](18-work-errors.md) E-20 |
| 10 | **`apps/api` 的自動化測試用獨立子專案 `Tcrfc.Api.Tests`（同目錄、無 `.sln`），用 `WebApplicationFactory<Program>` 打真正的行程內主機，接真正的本機 `mssql-dev`，不 mock 資料庫與 Redis**（S0-7d，2026-09-21） | 主專案 `Tcrfc.Api.csproj` 要明確 `<Compile Remove="Tcrfc.Api.Tests/**/*.cs" />`——沒有 `.sln` 幫忙切開建置範圍時，SDK 專案預設的遞迴 `**/*.cs` 萬用字元會把子目錄的測試原始碼一起編譯進主專案。`apps/api/.dockerignore` 另外排除該目錄，`docker build -t x apps/api` 不受影響。**測試環境設定用行程環境變數（`Environment.SetEnvironmentVariable`）而非 `ConfigureAppConfiguration`**——因為 `Program.cs` 在 `builder.Build()` 之前就依 `REDIS_HOST` 決定 DI 注入哪個 `IQueryCache` 實作，那段程式碼跑在 `WebApplicationFactory` 的設定攔截點之前；測試組件因此用 `[assembly: CollectionBehavior(DisableTestParallelization = true)]` 停用平行化，避免不同 fixture 的環境變數互相污染 |
| 11 | **資料庫不可用時，`apps/api` 的測試回報「失敗」而不是「略過」**（S0-7d） | fixture 的 `IAsyncLifetime.InitializeAsync()` 連不上資料庫就直接丟一個訊息清楚（含修復指令）的例外，xUnit 對每個測試都回報 `Failed`。刻意不引入 `Xunit.SkippableFact` 之類的第三方套件做「略過」——失敗比略過更難被 CI 儀表板悄悄忽略，且維持相依套件最少。**這是本次的執行層選擇，不是規格**；之後接上 CI 若情境不同（例如 CI 固定會提供資料庫），可重新評估 |
| 12 | ⚠️ **這個開發環境的 Docker Desktop 對 Docker Hub 拉取異常緩慢**（`redis:8-alpine` 拉取超過 30 分鐘未完成，`registry-1.docker.io` 本身用 `curl` 直接測是正常的，問題出在 Docker VM 的網路路徑，不是 registry） | 遇到需要「真的跑一個 Redis 起來測試」但又不想空等的情況，`brew install redis`（Homebrew 的下載路徑走不同 CDN，實測正常）裝一個真正的 `redis-server` 二進位檔跑在本機某個 port，讓容器用 `host.docker.internal:<port>` 連過去，一樣是真實 Redis、不是 mock，且可控（能直接 `kill` 掉模擬「執行中掛掉」）。**只是這個環境的限制，不是專案本身要不要用 Docker 跑 Redis 的決定**——正式 VM 與一般開發機器的網路預期不會有這個問題 |

---

## 12. 前端建置用的 Node.js 版本

> 🔴 **2026-09-22 補記的執行層決定。** 在此之前，四個 Node 應用（`apps/web`、`apps/admin`、
> `apps/web-charity`、`apps/admin-charity`）的 `Dockerfile` 全部寫 `node:22.12-alpine`，
> **但這個版本從沒有人選過、也沒有人記錄過理由**——是專案起手時複製貼上的值，本檔與
> [`20-cicd.md`](20-cicd.md) 過去都沒提到它。這次是因為 `apps/web` 的 `docker build` 實際壞掉
> 才回頭補上決定，見 [`18-work-errors.md`](18-work-errors.md) 對應條目。

### 事發經過（為什麼要重新選版本）

`docker build -f apps/web/Dockerfile apps/web` 的 `npm ci` 失敗，錯誤訊息表面上指向鎖檔（
`Missing: eslint@9.39.5 from lock file`），但鎖檔本身沒有壞（`npm install --package-lock-only`
重跑後 `git diff` 完全沒有變化）。**真正原因是 `EBADENGINE`**：相依樹裡多個套件
（`@eslint/compat`、`@eslint/core`、`@nuxt/nitro-server` 等，隨 Nuxt 4／ESLint 9 一起進來）的
`engines` 要求 `^20.19.0 || ^22.13.0 || >=24`，而 `node:22.12-alpine` 的 **22.12.0 不滿足
`^22.13.0`**——差一個 patch 版本，npm 10.9.0（該映像檔內建版本）預設把它降級成一則容易被忽略的
警告，`npm ci` 卻仍然失敗，只是錯誤訊息選了鎖檔當代罪羔羊。

`apps/admin`／`apps/web-charity`／`apps/admin-charity` 當時相依樹比較輕、還沒踩到這個門檻，
所以只有 `apps/web` 先炸——**這不是 `apps/web` 特有的問題，是四個應用共用的地基已經過期**，
只是暴露的時間點不同。

### 定案：`node:24.13.1-alpine`，四個 Dockerfile 一致

| 項目 | 內容 |
|---|---|
| 版本 | **`node:24.13.1-alpine`**（Alpine 3.23），四個 `Dockerfile` 的 `deps`／`build`／`runtime` 階段一律套用同一個標籤 |
| 適用範圍 | `apps/web/Dockerfile`、`apps/admin/Dockerfile`、`apps/web-charity/Dockerfile`、`apps/admin-charity/Dockerfile`（`apps/api` 是 .NET，不適用） |
| 是否浮動標籤 | **否**——不用 `node:24-alpine`。可重現性優先：同一份 `Dockerfile` 在不同時間 `docker build` 必須拉到同一個 Node 版本，不因 Docker Hub 上游改點釋出而悄悄變動 |

**為什麼是 Node 24、不是把 22 補到 `22.13.0`**：

1. **滿足下限只是及格，不是長期解**——`^22.13.0` 是這次卡住的相依樹現在的要求，Nuxt／ESLint／
   其他相依套件的 `engines` 門檻只會繼續往前推進，選一個貼著下限的版本明年大概率要重選一次。
2. **本機開發版本是 Node 24.13.1／npm 11.8.0**（`node -v`／`npm -v` 實測）。**這次的根因能潛伏
   三個交付都沒被發現，就是因為本機開發版本（24.x）與建置映像檔版本（22.12）差了一個 major，
   本機從來不會跑到 `npm ci` 這條在建置映像檔裡才會走的路徑**（本機用 `npm install`／已存在的
   `node_modules`，不會重新觸發 engines 檢查）。選 Node 24 讓建置環境與開發環境的 major 版本
   一致，這個落差類的問題下次會在本機先炸，而不是等到 `docker build`。
3. **Node 24「Krypton」的支援期比 22「Jod」長**：22 已於 2025 年 10 月進入 Maintenance LTS，
   維護期到 **2027-04**；24 於 2025 年 10 月進入 Active LTS（**約 2026 年 10 月轉 Maintenance**），
   維護期到 **2028-04**——多出約一年的支援期，涵蓋本案交付與初期維運期更有餘裕。
   ⚠️ 月份為 Node.js 官方發布節奏的一般模式（每年 4 月 Current、10 月轉 LTS），實際切換日期
   以 [nodejs.org 的 Release schedule](https://nodejs.org/en/about/previous-releases) 為準，
   本節不代表精確到日的官方公告。
4. **不是 Nitro／.NET 以外的專案有特殊需求**——四個 Node 應用（Nuxt 4 SSR 兩個、Vite SPA 兩個）
   對 Node 版本沒有特殊上限，純粹是 `engines` 下限的問題，選一個滿足下限、支援期夠長、且貼近
   本機開發版本的即可，不需要為某個框架單獨遷就。

**為什麼精確釘到 `24.13.1`、不是釘另一個 24.x patch**：直接對齊本機開發版本的 patch
（`node -v` → `v24.13.1`、`npm -v` → `11.8.0`），**把「本機能跑」與「建置映像檔能跑」收斂成同一個
版本**，是徹底消除本次這種落差的最簡單做法。`docker pull node:24.13.1-alpine` 已驗證該標籤存在
（Docker Hub 有發布對應的 Alpine 3.23 映像檔）。

### 下次什麼情況要重新檢視

- **`npm ci` 又出現 `EBADENGINE` 或看似指向鎖檔但鎖檔沒壞的失敗**——先懷疑 base image 版本，
  不要重複這次繞了一圈才找到根因的過程。
- **本機開發版本（`node -v`）升級到新的 major，且與 `24.13.1-alpine` 差距拉大**——這正是這次
  問題潛伏的成因，本機升版時應同步評估是否要把四個 Dockerfile 一起升版並更新此節。
- **Node 24 於 2026-10 前後轉入 Maintenance LTS**——不代表要立刻換，維護期到 2028-04
  仍然涵蓋本案，但若屆時 Node 26（下一個預期於 2026-10 前後成為 Current 的版本）已發布且相依樹
  的 `engines` 開始要求它，比照這次的判準重新選一次。
- **四個應用之間版本又出現不一致**（例如只改了一個 `Dockerfile`）——立即視為缺陷，四個必須永遠
  一致，這是這次任務刻意收斂的狀態。

### 鎖檔：這次不需要重產

升版後**四個 `package-lock.json` 一個字都沒改**——`docker build` 在新版映像檔下直接用既有鎖檔
`npm ci` 成功（四個都驗證過），問題只出在 base image 滿不滿足 `engines`，與鎖檔內容無關。
**若日後真的需要重產鎖檔，一律用與新映像檔相同的 Node／npm 版本產**（`docker run --rm -v
"$PWD":/w -w /w node:24.13.1-alpine npm install --package-lock-only`），並逐一檢查版本變動幅度，
不要讓它順手把整批相依套件升級。

### ✅ 已實作（S0-9h，2026-09-22）：`.node-version` 單一事實來源 ＋ 檢查腳本掛進 `npm run lint`

STATUS.md S0-9h 當時列了兩案：① `engines` + `engine-strict` ② 寫檢查腳本掛進 `npm run lint`。
**採第二案**——理由是本專案既有慣例本來就是「檢查腳本掛 `lint`」（`check-contrast.mjs`、
`check-forbidden-terms.mjs`、`emit-charity-fixtures.py --check`），跟這個機制同一套路；
`engine-strict` 只在「別人 `npm install` 時」才會擋，且四個專案要重複設定四次、訊息又是
npm 自己的格式，不會比腳本更早或更清楚地被看到。

**單一事實來源**：repo 根目錄的 `.node-version`（純文字一行版本號，無 `v` 前綴）。選它的原因是
`actions/setup-node` **原生支援** `node-version-file` 輸入直接讀這個檔案——CI 因此不需要在
workflow YAML 裡再複製一份版本號字串，結構上不存在「CI 用了另一個版本」這條漂移路徑。四個
`Dockerfile` 仍然手動維護 `FROM node:24.13.1-alpine`（Docker 語法無法用變數注入 `FROM` 的 base
image tag，這是 Docker 本身的限制，不是本專案的選擇），所以還是需要一支檢查腳本補上這一段的校驗。

**[`scripts/check-node-version.mjs`](../scripts/check-node-version.mjs)**（repo 根目錄，跨四個
應用共用一份，不是各自維護一份）檢查兩件事：
1. 四個 `Dockerfile` 的每一個 `FROM node:...` 是否等於 `.node-version` 內容 `+ -alpine`；
2. `.github/workflows/*.yml` 每一個 `actions/setup-node` 步驟——優先要求
   `node-version-file: .node-version`；若改用硬編碼 `node-version:`，該值必須與 `.node-version`
   一致；兩者都沒有（版本來源不明）也視為錯誤。

四個 `package.json` 各自新增 `"lint:node-version": "node ../../scripts/check-node-version.mjs"`，
並放在各自 `lint` 這條 `&&` 鏈的**第一個**（不是隨意位置）——[`18-work-errors.md`](18-work-errors.md)
E-34 的教訓是「串在可能非零離開碼的指令後面的檢查，事實上不會被執行」，排第一個保證它一定會跑，
不受同一條鏈上其他檢查是否失敗影響。

**兩個方向都已實測**（2026-09-22，`deployment-engineer`）：現況跑 `node
scripts/check-node-version.mjs` 離開碼 0；分別故意改壞 `apps/admin/Dockerfile` 的版本號、
與 workflow 裡 `setup-node` 的硬編碼版本，兩種情況都被抓到、離開碼 1，訊息指出確切檔案與行號。

**未採用 `engine-strict`／`.nvmrc`／`packageManager` 三件套**（本節原本的建議）：
`node-version-file` 讓 CI 不需要它；四個 `Dockerfile` 的 `FROM` 仍是唯一真正決定建置環境版本的
地方，檢查腳本已經校驗它，多加 `.nvmrc`／`engines` 只是多幾個要手動同步的位置，卻沒有增加新的
保護範圍——不做。

---

