# 19 — 行動 App 技術選型與實作決定

> 🔵 **這份是執行層決定，不是規格。** App 規劃書 §1.3 明文排除技術選型（L99／L225），
> 所以**選型結果不進規劃書，只記在這裡**。本檔與規劃書衝突時一律以規劃書為準，並回頭修正本檔。
>
> 本檔承接 App 規劃書 **§1.5「平台能力需求（取代技術選型）」的八項能力**，
> 以及 **§16.2 技術前提第 17、19 兩項**。
>
> App 要有什麼功能看 [`11-mobile-app.md`](11-mobile-app.md) → App 規劃書；
> 伺服器端、部署與快取策略看 [`17-deployment.md`](17-deployment.md)；資料表看 [`12`](12-database-schema.md)／[`12a`](12a-database-erd.md)／[`12b`](12b-database-tables.md)。
>
> ⚠️ **§12「本檔不決定的事」列出仍未決的項目**，其中 §16.2 第 19 項若答案是「否」，本檔 §2、§3、§7、§8 全部要重寫。

---

## 0. 一分鐘理解

| 項目 | 選定 |
|---|---|
| iOS | **Swift 5.9+／SwiftUI 為主，導覽容器用 UIKit**（iOS 15+） |
| Android | **Kotlin 2.x／Jetpack Compose ＋ Material 3**（`minSdk 29`） |
| 型別來源 | .NET 產 **OpenAPI 3.1 進版控** → 兩端各自產生 DTO（**不產 client**） |
| 本機儲存 | 兩端 **SQLite**：iOS GRDB.swift／Android SQLDelight，**共用一份 DDL** |
| 權杖 | 存取 **JWT 15 分鐘** ＋ 更新 **不透明字串 90 天滑動、可撤銷、每次使用即輪替** |
| 安全儲存 | iOS Keychain（`AfterFirstUnlockThisDeviceOnly`）／Android **Tink ＋ Keystore** |
| 推播 | **自家 .NET 直送** APNs（`.p8` token 認證）與 FCM HTTP v1，**不接推播平台** |
| 可見度量測 | 雙平台共同行為規格 ＋ 各自原生實作，**跑同一份 fixtures** |
| 監控 | **不裝第三方崩潰 SDK**：iOS MetricKit／Android 自捕，回自家端點 |
| CI | iOS **Xcode Cloud**／Android **GitHub Actions ＋ Gradle**，兩個 private repo |

**三個起點**：① **原生雙平台是客戶決定**（2026-09-20），本檔不重新論證，只誠實寫代價與緩解（§11）。
② §1.5 的八項能力**實質排除純 WebView 外殼**（規劃書原文），所以不考慮 Capacitor 類方案。
③ **首個上架版本不含 App 內付款**（規劃書 v3.10），付款相關的預留寫在 §10。

---

## 1. 兩個客戶端

### iOS

| 項目 | 決定 | 理由 |
|---|---|---|
| 語言 | Swift 5.9+，**Swift 6 嚴格並行先關閉**（`SWIFT_STRICT_CONCURRENCY = minimal`） | 嚴格檢查會讓 UIKit 互通大量報錯，v1.0 不值得付這個成本 |
| 導覽 | **`UITabBarController`（五分頁）＋ 每分頁一個 `UINavigationController` ＋ 畫面用 `UIHostingController` 包 SwiftUI** | 一次解決三件事：iOS 15 沒有 `NavigationStack`、深連結要能精確 push 到任意層、**可見度量測需要精確的生命週期訊號** |
| 狀態 | `ObservableObject` ＋ `@Published` | `@Observable` 要 iOS 17 |
| 影像 | **Nuke**（或自寫磁碟快取） | `AsyncImage` 無磁碟快取，撐不住 §13 的 50MB／月 |

**iOS 15 實際擋掉什麼**（這張表是本節的重點）：

| 想用的 | 最低版本 | 替代 |
|---|---|---|
| `NavigationStack`（可程式化路由，深連結需要） | iOS 16 | UIKit `UINavigationController` |
| `@Observable` | iOS 17 | `ObservableObject` |
| SwiftUI `Map` 的自訂標註 | iOS 17 | `MKMapView` ＋ `UIViewRepresentable` |
| `onScrollVisibilityChange` 之類的可見度 API | iOS 18 | **UIKit 自行量測**（§6） |

### Android

Kotlin 2.x／JDK 17／AGP 8.x，`minSdk 29`，**`targetSdk` 跟隨 Play 的當期要求**（上架後每年須跟進）。

**`minSdk 29` 必須寫兩條路徑的項目**：

| 項目 | 影響 |
|---|---|
| `POST_NOTIFICATIONS` 執行期權限 | API 33+ 才有。33 以下直接可發、33 以上要請求；§14.3「請求前須有前置說明」對兩條路徑都適用 |
| 位置精度選擇 | API 31+ 使用者可只給 coarse。**必須接受 coarse 並在距離前加「約」**，不得因此中止功能（§3.8 規則 3） |
| `ApplicationExitInfo` | API 30+。API 29 取不到上次異常退出原因，只能靠自捕（§8） |
| Edge-to-edge 強制 | API 35+。Compose 的 insets 一開始就要做對，不要等被強制才補 |

> ⚠️ **iOS 15／Android 10 是 §13 的規格值。** 要提高最低支援版本**必須先走同步鏈改規劃書 §13**（v3.11 已把這條程序寫進規格），不得在本檔擅自提高。

### repo

**兩個獨立 private repo**：`tcrfc-app-ios`、`tcrfc-app-android`。
🔴 **必須 private**——`CLAUDE.md` 第 7 條寫明現有 repo 是公開的，而 App repo 會含 provisioning profile、keystore、`.p8`、service account JSON。**App 原始碼不得放進現有的公開 repo。**

---

## 2. 不重複做兩次：`shared/` 契約目錄

App 規劃書 §9.2 的端點清單**只有資源、動作、呼叫者三欄，全表只有一個真實路徑，沒有 schema**。
型別的真實來源必須另外建立，而且**只能有一份**。

**決定：後端 .NET 以 Swashbuckle／NSwag 產出 OpenAPI 3.1，該檔進版控，作為 API 的機器可讀真實來源。**

| 檔案 | 產生者 | 消費者 |
|---|---|---|
| `shared/openapi.json` | .NET CI | iOS `swift-openapi-generator`／Android `openapi-generator` |
| `shared/cache-schema.sql` | 手寫（一份 DDL ＋ migration 編號） | iOS GRDB `DatabaseMigrator`／Android SQLDelight `.sq` |
| `shared/cache-policy.json` | 手寫（§2.4 時效表逐 key） | 兩端產生常數檔 |
| `shared/deeplinks.json` | 手寫（§2.3 的 8 條 ＋ 官網回退網址） | 兩端產生路由表；**同時產生官網要放的 AASA／assetlinks 的 `paths` 區段** |
| `shared/error-codes.json` | 後端定義（§9.5：代碼 ＋ 雙語訊息 ＋ 可否重試） | 兩端產生錯誤映射 |
| `shared/ad-viewability-cases.json` | 手寫（§6 的測試案例） | 兩端跑同一份單元測試 |

**三條紀律**：

1. **只產生 DTO，不產生 client。** 產生器產的 client 塞不進權杖續期、冪等鍵、離線佇列、退避重試這些橫切關注。
2. 🔴 **CI 的漂移檢查**：兩個 App repo 的 CI 重跑產生器後 `git diff --exit-code`，有差異就 fail。後端改了 API 而 App 沒同步，在 CI 就擋下來。
3. **`shared/` 是執行層產物，不得放規格。** 要新增欄位語意，先改規劃書。

---

## 3. 網路層、離線儲存與快取

| 項目 | iOS | Android |
|---|---|---|
| Client | `URLSession`（不引入 Alamofire） | OkHttp ＋ Retrofit ＋ `kotlinx.serialization` |
| 超時 | 連線 10s／讀取 20s | 同左 |
| 條件請求 | 列表端點一律送 `If-None-Match`；收 304 不換資料只更新 `fetched_at` | 同左 |
| 重試 | **5xx 與網路錯誤指數退避 3 次（1／2／4 秒 ＋ jitter）；4xx 不重試**（§9.5） | 同左 |
| 權杖續期 | **single-flight**：同時多個 401 只觸發一次 refresh | 同左 |

🔴 **付款的特例，兩句話要並排讀**：§9.5 規定「付款相關不得離線佇列」，但**帶了冪等鍵的訂單建立在 5xx 時可以安全重試**——冪等鍵的存在就是為了這個。
**判準：可即時重試（同一冪等鍵），不可暫存後擇日補送。** 不要為了守前半句連即時重試都不做，讓使用者在網路抖動時付不了錢。

### 本機儲存

**兩端都用 SQLite**：iOS **GRDB.swift**（可直接吃 raw SQL 與 migration）、Android **SQLDelight**（吃 `.sq`，schema 本身就是 SQL）。
❌ 不用 SwiftData（要 iOS 17）、Core Data（`.xcdatamodeld` 無法與 Android 共用）；若團隊只熟 Room，退路是 Room ＋ 用測試比對匯出的 schema 與 `shared/cache-schema.sql`。

一張 `cache_meta(cache_key, etag, fetched_at_utc, ttl_sec, server_time_utc)` 統一承載 §2.4 的時效表；
**TTL 值不寫死在程式，由 `shared/cache-policy.json` 產生常數**，避免一邊寫 6 小時另一邊寫 6 分鐘。

**圖片**不進 SQLite，走獨立檔案快取（iOS Nuke／Android Coil）。依 §13 的 50MB／月與規劃書 v3.9：
**列表一律取 320、詳情依螢幕倍率取 640 或 1280，任何情況不取主檔**；磁碟上限兩端各 150MB、LRU 淘汰。

---

## 4. 身分、權杖與會員卡

| 項目 | 決定 | 理由 |
|---|---|---|
| 存取權杖 | **JWT，15 分鐘** | 短效、可自驗、不打 DB |
| 更新權杖 | 🔴 **不透明隨機字串（32 bytes CSPRNG），伺服器只存雜湊** | §4.3 要求「須可由伺服器端撤銷」，**JWT 做不到撤銷** |
| 期限 | **90 天滑動**，對齊 §4.3「閒置 90 天重新登入」 | |
| 輪替 | **每次使用即輪替 ＋ 重用偵測**：舊權杖再被用到＝外洩，立即撤銷該裝置整條鏈 | |
| 承載處 | `AppDevice` 的四個欄位（規劃書 **v3.11 §10.1** 已補） | §4.4 已定「一裝置只綁一個會員」，權杖鏈與裝置天然 1:1 |

**安全儲存區**：

- **iOS Keychain，`kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly`。**
  ⚠️ 不要用 `WhenUnlocked`——背景收到推播要更新徽章時讀不到。⚠️ 不要用 `WhenPasscodeSetThisDeviceOnly`——使用者沒設密碼時整組資料會消失。`ThisDeviceOnly` 是為了不進 iCloud Keychain、不隨備份搬到新裝置。
- 🔴 **Android 的 `androidx.security:security-crypto`（`EncryptedSharedPreferences`）已棄用，本專案不得採用。**
  改用 **Google Tink（`tink-android`）＋ Android Keystore 承載的 keyset**（AES-256-GCM，StrongBox 可用則用）。自己寫 GCM 的 nonce 管理很容易錯，用 Tink 是為了避開這件事。
- 🔴 **必須排除自動備份**：`android:allowBackup` 與 `dataExtractionRules` 排除權杖儲存檔。否則 Google 備份會把權杖帶到新裝置，「一裝置一鏈」與「登出全部裝置」**同時失效**。

**生物辨識**（§1.5 第 8 項，選配）**只保護「會員卡快速開啟」，不保護存取權杖**——用它鎖權杖會讓背景續期整個卡住。

### 🔴 會員卡：最後同步時間與 7 天提醒的實作位置

| 決定 | 內容 |
|---|---|
| **時間的定義** | `last_verified_at` ＝ **伺服器回傳會籍狀態成功的那一刻，取自回應中的伺服器時間**；不是裝置時鐘、不是 App 開啟時間、不是本機寫入時間 |
| 存放 | SQLite `member_card` 表，與卡面欄位同列。**每份會籍一張卡各自有自己的 `last_verified_at`** |
| token 存哪 | 🔴 **存安全儲存區，不存 SQLite 明文**——它是撤銷憑證（§12.3），與存取權杖同級 |
| QR 怎麼產 | 內容 ＝ `{官網 base}/m/{token}`，**完全在本機組出，離線不需網路** |
| **防改時鐘** | 同時存伺服器時間與當時的**單調時鐘**（`CLOCK_MONOTONIC`／`SystemClock.elapsedRealtime()`）。顯示天數取牆鐘差與單調時鐘差**較大者**——把時鐘往回調消不掉提醒 |
| 7 天提醒 | 純 UI 判定；卡面永遠常駐「最後同步：YYYY-MM-DD HH:mm」 |
| ⛔ 不得做 | **不得因逾期未同步就停用卡片或隱藏 QR**——§3.6 只要求「顯示提醒」，加上停用是新規格 |
| ⛔ 不得做 | **不得把「有效」渲染成即時保證**；狀態與同步時間必須並陳（§2.4 硬規則 2） |

> 這條與 [`17-deployment.md`](17-deployment.md) §4「會籍有效性不得讀快取」是同一條規則在兩端的落點。

---

## 5. 推播傳輸

**先把 §16.2 第 17 項的問句拆對**：

| 層 | 是否可避免 |
|---|---|
| **APNs／FCM 本身** | **不可避免。** Android 沒有第二條路（自建長連線在 Doze 下不可行）。FCM 是作業系統層的推播管道，不是可替換的第三方平台 |
| **推播平台**（OneSignal／Airship／Braze 等） | **可避免，且本案應避免。** §6.6 只要三個彙總數字、§6.8 明文不做行為定向——這些平台的價值全在我們刻意不做的功能上 |

**決定：由自家 .NET 直送。** iOS 走 APNs HTTP/2，**用 `.p8` token 認證不用 `.p12` 憑證**（一把金鑰通吃所有 App 與兩個環境）；Android 走 FCM HTTP v1（service account 換 OAuth2 token）。

🔴 **分眾不得用 FCM topic**——理由兩條：① §6.3 的「會籍層級」是伺服器端資料，**放進 topic 等於把付費狀態送進 Google 的 topic 索引**；② topic 模式拿不到 per-device 結果，§6.6 的數字就算不出來。
**`PushTopicSubscription` 是我們自己的表，不是 FCM topic。分眾一律在 .NET 端解析成裝置權杖清單，逐則併發直送。**

**失效權杖清理（M4）**：APNs `410 Unregistered`／FCM `UNREGISTERED`｜`INVALID_ARGUMENT` → 標記失效；APNs `429`／FCM `RESOURCE_EXHAUSTED` → 退避重試，**不標記失效**。

其他：**payload 不放任何個資**（會經過 Apple 與 Google）；**不使用靜默推播**（§13 背景作業僅限推播接收）；
**使用 APNs／FCM ＝ 權杖與訊息內容經境外處理**，§12.1 明訂推播權杖視同個資，**§16.2 第 12 項的會員條款未完成前不得啟用推播**（§6.7）。

---

## 6. 廣告可見度量測

### 6.1 雙平台共同行為規格

**這一節必須抄成 `shared/ad-viewability.md`，兩端不得各自解釋。**

| 項目 | 規則 |
|---|---|
| 可見比例 | `visibleRatio` ＝ 版位矩形 ∩ 有效可見區 的面積比。有效可見區 ＝ 視窗 ∩ 所有會裁切的祖先容器 ∩ 扣除已知固定遮擋（底部分頁列、彈窗） |
| 取樣 | **200ms 一次**（5Hz） |
| 累計 | 🔴 **用單調時鐘累積「連續滿足 ≥50%」的時長，不是數取樣次數**——掉幀時數次數會低估 |
| 成立 | 連續時長 **≥1000ms**；掉到 <50% 即歸零重計 |
| 只計一次 | 每次素材裝載進版位產生一個 `presentation_id`（UUID），同一個只發一次曝光。**輪播每則各自有自己的 `presentation_id`**（§7.5） |
| App 背景 | 進背景或失去作用中狀態（含來電橫幅、控制中心）**立即停止累計並歸零** |
| 備援素材 | `is_fallback` 為真時**量測器根本不啟動**（§7.5） |
| 載入失敗 | **圖片解碼成功之後才啟動量測器**（§7.5） |
| 點擊去重 | 裝置端 5 秒內不重複發 ＋ **伺服器端同時去重**（§9.6）。兩層都要，裝置時鐘可被改 |
| 🔴 時鐘校正 | 啟動時由設定端點回應的 `Date` 標頭算 `server_skew`；事件 `occurred_at` ＝ 單調時鐘推算 ＋ skew。**沒有這條，「伺服器拒收超過 24 小時」就是可被改時鐘繞過的擺設** |

### 6.2 iOS

版位以 **UIKit `AdSlotView`** 承載，即使畫面主體是 SwiftUI 也用 `UIViewRepresentable` 包進去——**SwiftUI 在 iOS 15 沒有任何可靠的可見度 API**。
矩形：`view.convert(view.bounds, to: nil)` → 與 `window.bounds` 取交集 → **再沿 superview 鏈對每個 `clipsToBounds` 或 `UIScrollView` 祖先取交集**。
驅動：**`CADisplayLink`，`preferredFramesPerSecond = 5`**。
🔴 **必須 `add(to: .main, forMode: .common)`**——用預設的 `.default` mode，**捲動時 display link 會停跑**，而捲動正是最需要量測的時刻。
⚠️ cell 重用的 `prepareForReuse` **必須重置 `presentation_id`**，否則換了素材還沿用舊 id 會漏計。

### 6.3 Android

Compose 1.8+ 用 `Modifier.onLayoutRectChanged(throttleMillis = 200)`；1.8 以下用 `onGloballyPositioned` **只寫進一個 state**，判定交給 200ms 迴圈——⚠️ `onGloballyPositioned` 捲動時每幀都會被呼叫，**不可在其中判定或發事件**。
`boundsInWindow()` 已做過父容器裁切，但**不處理覆蓋在上面的其他 Composable**，須另扣已知固定遮擋。前景判定用 `ProcessLifecycleOwner` 的 `RESUMED`。
⚠️ **已知誤差**：Android 10 分割畫面下兩個 App 可能同時 RESUMED，此時可能高估。**登記為已知誤差，不做額外工程**（§13 驗證程序的接受範圍）。

### 6.4 事件佇列

本機表 `ad_event_queue(id, type, creative_id, campaign_id, slot_code, presentation_id, occurred_at_utc, platform, lang, club_id, send_state)`。
🔴 **不得放入 `member_id`、完整 IP、座標、廣告識別碼（§7.8）——在 App 端就不放進 payload，不是靠伺服器過濾。**
送出時機：每 30 秒／累積 50 筆／進背景，三者任一；單批上限 200 筆並帶 `batch_id` 供伺服器冪等去重；
`inflight` 超過 60 秒回 `pending`；佇列上限 2000 筆或 48 小時，FIFO 丟棄並記丟棄計數（走 M5）。
**留 48 小時只為診斷，伺服器 24 小時就會拒收。** §9.5 明文允許離線佇列的只有廣告事件與推播訂閱。

---

## 7. 定位、地圖與設定下發

| 平台 | 定位 |
|---|---|
| iOS | `CoreLocation`，`requestWhenInUseAuthorization()`，`requestLocation()` 單次取位，精度 `HundredMeters`。🔴 **Info.plist 只放 `NSLocationWhenInUseUsageDescription`**——加 `Always` 審查會追問背景用途，而 §3.8 明文不做背景追蹤 |
| Android | `FusedLocationProviderClient.getCurrentLocation(BALANCED_POWER)`；無 GMS 退回 `LocationManager`。**只拿到 coarse 也接受** |

🔴 **座標只存在於記憶體**：不寫 SQLite、不進任何 log、不進崩潰回報的 breadcrumb（§3.8 規則 4）。距離用 Haversine 在裝置端算。

**地圖**：iOS **MapKit**（`MKMapView` ＋ `UIViewRepresentable`，免金鑰免帳單）／Android **Google Maps SDK**（`maps-compose`，🔴 金鑰**必須綁套件名 ＋ SHA-1 憑證指紋並設每日配額上限**；⚠️ **計價方案會變動，上線前重新確認當期價目**）。
**導航與撥號都不在 App 內做**：iOS `MKMapItem.openInMaps`／`UIApplication.open(tel:)`；Android `geo:` Intent **＋ `createChooser`**（不寫死套件名）與 **`ACTION_DIAL`**（不是 `ACTION_CALL`，免權限、不會直接撥出）。

🔴 **App 不得使用 `CLGeocoder`／`Geocoder`**（§3.8 不做執行期 geocoding）。座標一律來自 API 的 `PartnerStore.lat`／`lng`（後台 K4 人工確認後儲存）；§3.8 規則 3 的「手動輸入地址查詢」是**對已取得的店家名稱、地址與地區欄位做文字比對**，不轉座標（規劃書 v3.11 已澄清）。

### 🔴 設定下發與 API 單點故障

[`17-deployment.md`](17-deployment.md) §7 風險 1：一台 VM、一個 API 行程承載五個平台。而 §9.4 要求「不得白畫面」、§8.1 要求維護模式可遠端開啟——**API 掛掉時，用來宣告「維護中」的那支端點也掛了**。

**三層來源，依序嘗試**：

| 層 | 來源 | 什麼情況下還活著 |
|---|---|---|
| 1 | 🔵 **Cloudflare 上的靜態設定 JSON**（Workers KV／R2） | **VM 全滅時仍可讀。** 後台 M5 存檔時 write-through 推上去 |
| 2 | `GET /v1/app/config`（§9.2「設定 讀取 匿名」） | API 活著時取即時值。邊緣設 `max-age=60, stale-while-revalidate=60, stale-if-error=86400` |
| 3 | 本機最後一次成功的設定（含取得時間） | 前兩層都失敗時 |

三層都失敗 → **離線模式**：會員卡、已快取賽程、已讀新聞照常可用（§2.4）。**絕不白畫面。**
🔴 **強制更新畫面必須是純本機資源**（版面、圖示、雙語文案全部打包進 App）——否則「叫使用者更新」這件事本身就依賴那支掛掉的 API。
最低支援版本用語意化比較 `主.次.修`（[`06-conventions.md`](06-conventions.md) §3），**建置號不參與比較**；低於最低版不可略過，低於建議版可略過並每 7 天再提醒。
Feature flag 命名 **`{模組}_{功能}` 小寫蛇形**：`ads_enabled`、`map_enabled`、`biometric_unlock_enabled`、`payment_mode`。

---

## 8. 延遲、流量與監控

§13 要求冷啟動至首頁可互動 ≤3 秒，而台中↔West US 2 的 RTT 是 **130–160ms**。首頁若串行呼叫三支端點，光網路就 0.5–1 秒。

**七項緩解，按收益排序**：

1. 🔵 **逐端點分類邊緣快取**——最大的一筆，且完全落在「不得讀快取五類」之外：

| 可在 Cloudflare 邊緣快取 | 🔴 不可快取（`Cache-Control: private, no-store`） |
|---|---|
| 設定、俱樂部、賽事系列、賽事、球隊球員、新聞、特約店家、夥伴贊助、FAQ、會籍方案與權益、廣告投放 | **會員資料、會員卡、會籍狀態、付款訂單、我的報名、抽獎資格、課程即時名額** |

🔴 **右欄不只是效能問題，是個資問題**：帶 `Authorization` 的回應若被邊緣快取，**會把 A 的個資回給 B**。必須顯式設定，不能靠 Cloudflare 預設行為。

2. **啟動不等網路**：先用本機快取渲染，網路回來再更新。**3 秒靠這個達成，不是靠網路變快。**
3. **併發不串行**（`async let`／`coroutineScope { async }`）。
4. **連線預熱**：啟動立刻打設定端點建立 TLS 連線，後續請求復用。
5. **ETag／304**：一個 RTT、不傳 body，同時服務 50MB／月。
6. **圖片走 Cloudflare CDN 的 Blob 衍生檔**，不打 VM。
7. **付款流程壓低往返**：建立訂單直接回付款頁 URL。

### 監控：零第三方

**需求衝突**：§8.5 要 M5 看到崩潰率、API 錯誤率、啟動耗時；§7.8 與 §12.4 要能宣告「不追蹤」、隱私標籤最乾淨。

**決定：不裝任何第三方崩潰 SDK。**

| 平台 | 做法 |
|---|---|
| iOS | **MetricKit**：每日 `MXMetricPayload`（含啟動耗時、hang）＋ `MXDiagnosticPayload`（含崩潰），**去識別化後 POST 自家端點**（`AppDiagnosticReport`，規劃書 v3.11 §10.1 已補） |
| Android | `Thread.setDefaultUncaughtExceptionHandler` 自捕 ＋ **`ApplicationExitInfo`**（API 30+；API 29 僅靠自捕） |
| API 錯誤率／啟動耗時 | 由網路層與啟動計時器自行彙總，隨批次上報送出 |
| 分析介面 | **App Store Connect 崩潰報告 ＋ Play Console 的 Android Vitals**——商店帳號本來就有，不是新增第三方 |
| 🔴 符號化 | **CI 每次 release 歸檔 dSYM 與 `mapping.txt`，按版本號 ＋ 建置號存放。** 沒有這步，自捕的堆疊是廢的 |

---

## 9. CI/CD、上架與版本節奏

現況：**無 `.github/`，部署人工，網站與 API 的 CI 仍未定**（[`17`](17-deployment.md) §8）。

| 平台 | 管線 |
|---|---|
| **iOS** | **Xcode Cloud**（主推）——Apple 官方、與 App Store Connect 整合、**不用自己管憑證與 provisioning**；方案與包含的運算額度以 Apple 當期公告為準。<br>退路：GitHub Actions ＋ macOS runner ＋ fastlane（`match`／`gym`／`pilot`）。🔴 **App repo 必須 private ⇒ macOS runner 要付費且分鐘數計價是 Linux 的數倍**；走這條就讓 **PR 只跑 build ＋ test，archive 與上傳 TestFlight 只在 tag 時跑**，或用自有 Mac 當 self-hosted runner |
| **Android** | **GitHub Actions ＋ Gradle**：`bundleRelease` 產 AAB → Play Developer API service account 上傳 internal testing track |

**CI 必跑四件事**：① build ＋ unit test（§6 可見度判定、版本比較、快取 TTL、深連結解析、錯誤映射——**全是無 UI 相依的純函式**）② **OpenAPI 漂移檢查**（§2）③ **`shared/` 一致性檢查** ④ **產物歸檔** dSYM／`mapping.txt`，檔名 `{平台}-{版本}-{建置號}`。

版本號 `主.次.修`（[`06`](06-conventions.md) §3），**建置號 ＝ CI run number**，兩平台各自單調遞增。
環境分離用 build configuration／Gradle flavor ＋ 不同 bundle id（`tw.tcrfc.app.dev`）；⚠️ **AASA 要同時列 dev 與 prod 兩個 appID**。
密鑰（`.p8`、keystore、service account JSON）一律 CI secrets，**不進 repo**。

**上架前置**：

| 項目 | 注意 |
|---|---|
| ✅ **開發者帳號** | **已到位**（2026-09-20）：D-U-N-S、Apple Developer 法人帳號、Google Play 組織帳號均已完成，主體為俱樂部（§14.1） |
| Google Play 帳號 | ✅ 已註冊為**組織**帳號（個人帳號另有封閉測試人數與天數要求，組織帳號不適用） |
| ✅ Apple Team ID ／ Android package name | **已取得**，§2.3 的 `.well-known` 兩個關聯檔**可以直接產**（`shared/deeplinks.json` 的 `paths` 區段，§2）。⚠️ **藍鯨網域的 Universal Link 仍擋在 DNS 控制權**（`STATUS.md` B-4），未解則退回自訂 scheme ＋ 網頁回退 |
| 節奏 | §14.4：4–6 週一版；**強制更新前須先有建議更新的緩衝版**；旗標要兩邊都上架後才能翻 |

---

## 10. 首個上架版本：不含 App 內付款

規劃書 **v3.10** 已把 App 內付款移到 **§15 的 Phase E**。

**因此不擋首版的阻塞**：§16.2 第 5 項（IAP 判定）、第 6 項（俱樂部 LINE Pay 商店號）、第 7 項（首波廣告主），以及 `STATUS.md` B-8（代收代付稅務認定）。
**仍擋**：第 1–4 項（藍鯨資產／賽程／名單／網域 DNS）、第 9 項（店家座標）、第 12 項（會員條款，擋推播啟用）。

> 🔴 **防誤讀**：「App 首版不做付款」**不等於**「不需要固定出口 IP」。站內商店與慈善平台都走 API 串接，[`17`](17-deployment.md) 的架構理由一字不變。

**付款入口採保守版**：顯示權益對照與方案比較（§3.7 明寫「未登入即可檢視，是入會轉換關鍵」）、**不顯示金額**、按鈕文案「了解如何加入」→ 外開瀏覽器。**送審前須確認台灣區當期規則。**

**「可切換」要預留的不是 UI，是訂單那一層**（§5.5 原話：A 與 B 共用同一組訂單與開通邏輯）：

1. **訂單建立端點（帶冪等鍵）與 `POST /api/membership/activate` 在 Phase B 就要做完**——外開瀏覽器走的官網流程本來就需要它們。
2. App 端定義 `PaymentCoordinator` 介面，兩個實作：`ExternalBrowserPayment`（`SFSafariViewController`／Custom Tabs）與 `InAppLinePayPayment`（Phase E，先留空樁）。
3. 🔴 切換由 feature flag **`payment_mode`（`off`／`external`／`inapp`）** 決定，**不是編譯期常數**；出廠預設 `external`。
4. **回程通道不做深連結**——§2.3 的 8 條對照表沒有付款回程，加一條就是規格變更。改用**回前景時重拉會籍狀態**，完全符合 §5.4「開通以伺服器回呼為準」，**零規格變更**。
5. 🔴 **不可反向的方向規則**：
   > **`payment_mode` 只能在上線後從 `inapp` 降級回 `external`（把審查通過的功能關掉，合規方向）。
   > 絕不得以 `external` 送審、通過後再遠端開啟未經審查的 `inapp`——那是違規，會下架。**

   這正是 §5.5「不得寫死成只支援其中一種」的目的：**審查打回或上線後出事時，不用重送審就能退回可用狀態。**

⚠️ **若送審時藍鯨內容仍未到位**，§14.3 已寫好答案：「寧可首版只上磐石內容，也不要上一個藍鯨分頁全空的雙隊 App。」
但 **App 名稱與 icon 不改**（中性名稱、雙標誌），只有商店說明與截圖先放磐石。

---

## 11. 原生雙平台的代價與緩解

**代價**（客戶已拍板，本節不重新論證，只如實記錄）：

| # | 代價 |
|---|---|
| 1 | **工時約 1.6–1.8 倍**——後端、設計、內容、規格共用，但 UI、導覽、測試、除錯兩份 |
| 2 | **成為專案的第 6、7 個並行交付項**（另有主站 Nuxt、共用後台、藍鯨站、慈善站、.NET API）。**人力是本決定最大的風險，不是技術** |
| 3 | 🔴 **行為漂移會漂到最痛的地方**——曝光判定的分歧直接變成對廣告主的對帳爭議（§7.5 原文：「沒有這一節，版位就賣不出去」） |
| 4 | **送審節奏不同步**；強制更新要兩邊都上架才能翻 |
| 5 | **第三方相依不同**（MapKit vs Google Maps、CoreLocation vs Fused、Nuke vs Coil），測試各測一次 |
| 6 | **語系切換 Android 一行（`AppCompatDelegate.setApplicationLocales`），iOS 要自寫 `LocalizationManager`** |

**六項緩解**：

1. 🔵 **`shared/` 契約目錄**（§2）——把「同一份輸入產生兩端程式碼」做成機制，不靠紀律。
2. 🔵 **把判定邏輯抽成無 UI 相依的純函式，跑同一份 fixtures**——對付代價 3 最有效的一招。
   `shared/ad-viewability-cases.json` 的每筆是一個 `(時間, visibleRatio, appState)` 序列，輸出是應產生的事件數。
   **UI 框架怎麼取到 `visibleRatio` 可以不同，但拿到之後怎麼判定必須同一套邏輯、被同一份測資驗證。** 同樣處理：版本比較、快取 TTL、深連結解析、錯誤碼映射、7 天提醒的日數計算。
3. 🔵 **共用 SQLite DDL**（§3）。
4. 🔵 **同名檔案、同名元件**（`HomeScreen.swift` / `HomeScreen.kt`）——低科技，但讓 diff 可以人工對照。
5. 🔵 **iOS 先行 2–3 週，兩邊同週上架。** iOS 限制較多（iOS 15 的缺口、AASA 驗證嚴格、審查較嚴、IAP 風險在 Apple 側），先撞完牆把 `shared/` 與 API 打磨定型；**Android 隨後擔任 `shared/` 規格的驗證者**——Android 實作時發現規格講不清楚的地方，就是規格真的沒寫清楚。
6. 🔵 **KMP 逃生門**：本次不採 Kotlin Multiplatform（它把 Gradle 引進 iOS 建置，且團隊熟悉度未知），**但它是唯一不需要推翻「原生 UI」這個決定的升級路徑**（共用 domain／network／cache，UI 仍各自原生）。
   **觸發條件：第三個版本時若「同一個 bug 要修兩次」超過缺陷總數四成，重新評估。**

---

## 12. 本檔不決定的事

- **§16.2 第 19 項「App 的 API 是否由官網後台承載」** —— 🔴 **本檔所有決定都假設「是」**（與藍鯨官網共用後台同模式）。若答案是「否」，§2、§3、§7、§8 全部要重寫。追蹤於 `STATUS.md` **B-14**
- **§16.2 第 17 項推播是否走自家直送** —— 本檔建議直送（§5），但客戶可能有既有的服務商合約
- **macOS CI 的承擔方式** —— Xcode Cloud 訂閱 vs 自有 Mac 當 self-hosted runner
- **Google Maps API 的帳單帳戶歸屬**（俱樂部？我方代管？）
- **Sign in with Apple 是否要做**（§4.2 與 §16.2 第 15 項雙重待確認）
- **年齡分級是否歸兒童類別**（§12.4、§16.2 第 11 項）
- **App 版的隱私政策與服務條款**（§12.4 明寫「須另行撰寫」，尚不存在）
- **網站與 API 的 CI** —— [`17`](17-deployment.md) §8 已登記，仍未定

> ✅ **原本屬於「這是新規格」的五項已於 2026-09-20 走完同步鏈**：
> 更新權杖的承載欄位、`AppDiagnosticReport`、首版付款範圍（以上 v3.10／v3.11 §10.1、§5.1、§15）、
> 推播「送達」的定義（v3.11 §6.6）、推播金鑰的告警基準（v3.11 §8.5／§12.3）。
> **本檔其餘內容一律是實作手段**，不含規劃書沒有的功能規格。

---

## 13. 驗證程序

開工後逐條實測，結果留存。前六條是硬性安全門檻。

| # | 驗證 | 通過條件 |
|---|---|---|
| 1 | **會員卡離線可出示** | 飛航模式冷啟動，兩張卡與 QR 皆可顯示，各自標出正確的「最後同步」 |
| 2 | 🔴 **7 天提醒不可被改時鐘繞過** | 裝置時鐘往回調 30 天，提醒仍出現 |
| 3 | 🔴 **會籍不以快取宣告有效** | 後台改為過期後，App 回前景即反映；離線期間卡面不得顯示「有效」為即時保證 |
| 4 | 🔴 **會員卡 token 不在明文儲存** | 取出 App 沙箱檔案（含 SQLite、偏好設定）全文搜尋 token，**必須找不到** |
| 5 | 🔴 **Android 備份未帶走權杖** | 備份還原到第二台裝置，**必須要求重新登入** |
| 6 | 🔴 **更新權杖可撤銷** | 後台「登出全部裝置」後，該裝置下一次續期**必須失敗** |
| 7 | 🔴 **個資端點未被邊緣快取** | A 帳號打過會員資料端點後，B 帳號打同一路徑**必須回 B 自己的資料**，且回應無 `CF-Cache-Status: HIT` |
| 8 | 🔴 **曝光判定兩端一致** | 兩端跑 `shared/ad-viewability-cases.json`，事件數**逐案完全相同** |
| 9 | **捲動中仍在量測（iOS）** | 持續捲動時 display link 未停跑（`.common` mode 生效） |
| 10 | 🔴 **時鐘偽造的事件被拒** | 裝置時鐘調到 48 小時前產生曝光事件，伺服器端**必須拒收** |
| 11 | 🔴 **API 全滅不白畫面** | 停掉 API 容器後冷啟動，能顯示維護訊息或離線模式；再把 Cloudflare 的靜態設定翻成維護中，App 讀得到 |
| 12 | **座標未離開裝置** | 抓封包確認任何請求的 body 與 query 皆無座標；診斷回報 payload 亦無 |
| 13 | **深連結未安裝時的回退** | 移除 App 後點 8 條深連結對應的官網網址，**全部落到對應頁面，不得錯誤頁** |
| 14 | **最低支援版本生效** | 後台 M1 調高後，舊版 App 啟動即出現不可略過的更新畫面，且**斷網時仍出現**（本機資源） |
