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
| 前台 | **Nuxt 3 SSR**（Vue 3）——主站、藍鯨、慈善**各一個 instance** |
| 後台 | Vue 3——官網、慈善**各一個 instance**（後台不需 SEO） |
| API | **.NET / C#，EF Core ＋ Dapper**——**單一 instance**，持有兩個 `DbContext` |
| 快取 | **Redis 一個 instance，只服務俱樂部**；cache-aside ＋ SQL fallback |
| DBMS | **Azure SQL Database**，兩個獨立單庫，先用 Basic |
| 物件儲存 | **Azure Blob Storage** |
| 執行環境 | **單一 Azure VM（West US 2）＋ Docker**，前台、後台、API、Redis 全在此 VM |
| 網路 | **單一 VNet**，PaaS 以 **VNet Service Endpoint** 接入 |
| 邊緣 | Cloudflare（DNS／CDN／WAF，proxy 回源 VM） |

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
   │  │ Azure VM（West US 2）Ubuntu LTS ＋ Docker Compose       │  │
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
| 排程 | **Azure SQL 無 SQL Agent**。排程發布、逾時取消訂單、每日對帳一律由 .NET 的 hosted service 承擔 | |
| 影像處理 | **ImageSharp**（年營收 100 萬美元以下適用 Apache 2.0，俱樂部與協會均符合；**此前提要記錄**）。日後若超過門檻改 SkiaSharp | `STATUS.md` S0-8 需伺服器端產 WebP 與多尺寸 |

> 🔴 **ImageSharp 不解 HEIC／HEIF，但規劃書 §4.0 明文接受 HEIC。** iPhone 拍的照片預設就是 HEIC，
> 而後台的實際使用者多半用 iPhone。**這一項在動工前必須先決定**，三條路擇一：
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

---

## 7. 已知風險

| # | 風險 | 成因 | 緩解 |
|---|---|---|---|
| 1 | **宿主機與 API 單點故障** | 一台 VM、一個 API 行程承載五個平台與兩個收款主體 | 資料在 Azure SQL 與 Blob（不隨 VM 毀損）；備份與重建程序必須演練並記錄 RTO |
| 2 | **兩法人憑證同行程** | 協會與俱樂部的 LINE Pay 憑證與資料庫連線字串在同一個 .NET 行程 | `DbContext` 完全分離、慈善 context 不含俱樂部型別、設定來源分離、不啟用 Elastic Query、慈善不接 Redis |
| 3 | **West US 2 延遲** | 台中↔美西 RTT 約 130–160ms，且 **SSR 首屏必須往返** | Cloudflare 快取內容頁 ＋ `stale-while-revalidate`；結帳流程壓低往返次數 |
| 4 | **出口 IP 綁死機器** | 換 VM／換區域即需改 LINE Pay 白名單 | 靜態 Public IP 獨立於 VM 生命週期；變更視為需事前申請的停機事件 |
| 5 | **個資跨境存放** | 會員與**捐款人**個資存於美國（VM、Azure SQL、Blob 全在 West US 2） | ⚠️ **法務待確認**：個資法跨境傳輸限制，以及協會與俱樂部間的委託處理約定須載明境外存放 |
| 6 | **Basic 層 2 GB 硬上限** | 寫滿即寫入失敗（非降速） | 儲存空間告警設在 1.5 GB；層級變更是線上作業，可即時升 S0 |
| 7 | **快取陳舊造成錯誤決策** | 有人把 §4 禁用清單裡的資料加進快取 | 清單於 [`12`](12-database-schema.md) §12 與 [`14`](14-invariants.md) 交叉引用；write-invalidate ＋ TTL 兜底；驗證項逐條實測 |
| 8 | **G-02 搜尋第一期不完整** | `LIKE` 比對做不到分類篩選與關鍵字高亮 | 登記為已知落差；升級路徑為 Azure SQL 內建全文檢索 |
| 9 | **區域延遲與出口 IP 的時序耦合** | 唯一能真正解決 130–160ms 的是把 VM 移到亞洲區，但換區域＝換出口 IP＝改 LINE Pay 白名單 | 🔴 **要遷區域必須在申請商店號並登記出口 IP 之前決定完**；過了那個點，遷移成本高一個數量級 |
| 10 | **App 在 API 全滅時無法宣告維護中** | 用來宣告「維護中」的設定端點與 API 同一個行程 | App 的設定、最低支援版本與維護模式另有一份**靜態備援放在 Cloudflare**（Workers KV／R2），不經 VM；強制更新畫面的版面與雙語文案打包進 App。見 [`19`](19-app-tech-stack.md) §7

---

## 8. 本檔不決定的事

- **網站與 API 的 CI 管線** —— 目前無 `.github/`，部署是人工；Nuxt ＋ .NET 的建置與推送流程另案。**App 的兩條管線見 [`19-app-tech-stack.md`](19-app-tech-stack.md) §9**
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
