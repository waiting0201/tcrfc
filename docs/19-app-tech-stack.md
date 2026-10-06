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
> ✅ **App 與官方網站共用同一套後端 API**（規劃書 v3.12 §16.1 已定案）——本檔 §2、§3、§7、§8 都建立在這個前提上。
> ⚠️ 仍未決的項目見 §12。

---

## 0. 一分鐘理解

| 項目 | 選定 |
|---|---|
| iOS | **Swift 5.9+／SwiftUI 為主，導覽容器用 UIKit**（iOS 15+） |
| Android | **Kotlin 2.x／Jetpack Compose ＋ Material 3**（`minSdk 29`） |
| 型別來源 | .NET 建置期產 **OpenAPI 3.1 進版控**（`shared/openapi.json`）→ `shared/generated/` 產生 Swift／Kotlin DTO（**不產 client**）；AP-8 ✅ |
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

**本機放置位置（2026-10-06）**：兩個 repo 實體放在本 repo 底下的 `mobile/ios`、`mobile/android`，方便同一個編輯器一起開；它們**仍是各自獨立的 git repo**，本 repo 以 `.gitignore` 的 `/mobile/` 整個排除。🔴 **不得 `git add -f` 其中任何檔案，也不得改成 submodule**（submodule 只記 commit 指標，但會把私有 repo 的存在與網址公開）。App 的 `scripts/sync-shared.sh` 預設上游是上兩層的本 repo，放到別處時以 `TCRFC_REPO` 指定。**遠端（2026-10-06）**：目前只有 `Remote_NAS`（`/Volumes/public/Repo/tcrfc-app-ios`、`tcrfc-app-android`，bare repo，作離線備份）；私有 GitHub repo 與 CI 仍屬 AP-7。

---

## 2. 不重複做兩次：`shared/` 契約目錄

App 規劃書 §9.2 的端點清單**只有資源、動作、呼叫者三欄，全表只有一個真實路徑，沒有 schema**。
型別的真實來源必須另外建立，而且**只能有一份**。

**決定：後端 .NET 在「建置期」產出 OpenAPI 3.1，濾成 App 可呼叫的範圍後進版控（`shared/openapi.json`），作為 API 的機器可讀真實來源。**
✅ **已實作（AP-8，2026-10-02）**：`shared/` 目錄在本 repo 根目錄，總覽與重新產生方式見 [`shared/README.md`](../shared/README.md)。

| 檔案 | 產生者 | 消費者 |
|---|---|---|
| `shared/openapi.json` | **產生檔**：`shared/scripts/gen-openapi.sh`（`apps/api` 建置期，不連資料庫）→ `filter-openapi.mjs` 排除後台／慈善平台／伺服器內部端點並正規化 | 兩端的 DTO 產生（見下）；App 端點的真實來源仍是規劃書 §9.2 |
| `shared/generated/swift/TcrfcApiModels.swift`、`shared/generated/kotlin/TcrfcApiModels.kt` | **產生檔**：`shared/scripts/gen-dto.mjs`（只有 DTO，無 client） | 兩端 App 直接取用 |
| `shared/cache-schema.sql` | 手寫（一份 DDL ＋ `-- migration: N` 編號區塊） | iOS GRDB `DatabaseMigrator`／Android SQLDelight `.sq` |
| `shared/cache-policy.json` | 手寫（§2.4 時效表逐 key，另含會員卡 7 天、事件佇列、圖片、網路常數） | 兩端產生常數檔 |
| `shared/deeplinks.json` | 手寫（§2.3 的 8 條 ＋ 官網回退網址 ＋ `universalLinkPaths` ＋ 解析測資） | 兩端產生路由表；**`universalLinkPaths` 供 AP-9 產出官網要放的 AASA／assetlinks（Team ID 與 package name 填入後才能產）** |
| `shared/error-codes.json` | **產生檔**：`shared/scripts/gen-error-codes.mjs` 掃 `apps/api` 實際會回的 `code`（84 個，皆為會員一族）並解析 `Common/ApiErrorMessages.cs`／`ApiErrorEnvelope.cs`，每個代碼附 `messageEn`／`retryable`，另有 `statusDefaults`（依狀態的通用代碼）；結構對應規劃書 §9.5，不發明規格 | 兩端產生錯誤映射 |
| `shared/image-derivatives.json` | **產生檔**（2026-10-05）：`gen-image-derivatives.mjs` 解析 `apps/api/Images`，圖片衍生檔網址命名規則（`{stem}-{1280｜640｜320｜thumb}.webp`）、挑選建議與範例；DTO 的 `*Url` 是主檔網址，小圖由它推導 | 兩端不必各自複製規則（缺口 A5） |
| `shared/enums.json` | **產生檔**（2026-10-05）：`gen-enums.mjs` 解析 `db/club-schema.sql` 的 CHECK：球隊 `type`／`gender`、賽事 `status` 值域 | 兩端對照；遇到未知值須容錯（缺口 A10） |
| `shared/ad-viewability.md`、`shared/ad-viewability-cases.json` | 手寫（§6 的行為規格與 22 筆測資：18 筆曝光＋4 筆點擊去重） | 兩端跑同一份單元測試；`check-shared.py` 內有參考實作驗證測資 |

**DTO 工具的選擇（2026-10-02）**：自寫約 200 行的 `shared/scripts/gen-dto.mjs`，**不採用** swift-openapi-generator／openapi-generator。理由：
① 兩者都連 client 一起產，而紀律 1 要求只產 DTO；
② swift-openapi-generator 需要 Swift 工具鏈，無法在 GitHub-hosted ubuntu runner 上便宜地跑漂移檢查；
③ 本 API 的 schema 形狀單純（物件、陣列、純量、可為空、字典，沒有 enum／繼承），遇到沒處理過的形狀產生器會直接報錯而不是默默產錯；
④ 零相依、離線、輸出完全決定性。
型別對應：時間與 uuid 一律 `String`（時間解析交給 App 的時間工具）；`int32`→`Int`、`int64`→`Int64`／`Long`；可為空或非必填→Optional（Swift）／`= null`（Kotlin）；任意 JSON→`JSONValue`／`JsonElement`。Kotlin 套件名稱預設 `tw.tcrfc.app.api.dto`（`gen-dto.mjs` 第一個常數，AP-7 建 repo 後若要改只改那一行）。
Kotlin 端的 `Json` 設定請用 `ignoreUnknownKeys = true`，讓後端新增欄位時舊版 App 不會解析失敗。
⚠️ Swift 檔已用 `swiftc -typecheck` 驗過；**Kotlin 檔尚未以 `kotlinc` 驗證**（環境沒有 Kotlin 編譯器），AP-7 的 Android 專案第一次編譯時就是驗證。

**三條紀律**：

1. **只產生 DTO，不產生 client。** 產生器產的 client 塞不進權杖續期、冪等鍵、離線佇列、退避重試這些橫切關注。
2. 🔴 **CI 的漂移檢查**分兩側：
   - **後端這一側（已實作）**：本 repo 的 `ci.yml` 有 `shared-contract` job，重新產生後 `git diff --exit-code`，有差異就 fail（[`20-cicd.md`](20-cicd.md) §3）。後端改了 API 而沒重新產生並提交 `shared/`，在 PR 就會擋下來。
   - **App 端（AP-7 時實作）**：兩個 App repo 固定取用 `shared/` 的某個版本，CI 比對自己取用的那份是否等於上游；上游升版時兩端各自升級並重跑單元測試。取用方式（sparse checkout 固定 commit／發布 tag 的 tarball）**AP-7 決定**——App repo 是 private、本 repo 是公開的，單向取用沒有權限問題。
3. **`shared/` 是執行層產物，不得放規格。** 要新增欄位語意，先改規劃書。

**目前 `shared/` 揭露的缺口（皆為待決，不在 `shared/` 補規格）**：

| # | 缺口 | 現況 |
|---|---|---|
| 1 | ✅ **錯誤結構（2026-10-05 後端已補齊）**：規劃書 §9.5「代碼、雙語訊息、是否可重試」 | **所有** `ProblemDetails`（例外處理器、空本文的 401／403／404／429、`Results.Problem`）都帶 `code`、`messageZh`、`messageEn`、`retryable`，**相容擴充**，既有欄位不動。`code`：有專屬代碼的（會員一族 84 個）沿用，其餘依狀態給通用代碼（`validation_failed`／`unauthenticated`／`forbidden`／`not_found`／`conflict`／`rate_limited`／`server_error`…，見 `shared/error-codes.json` 的 `statusDefaults`）。`retryable` 依規劃書：5xx＝true、4xx（含 423、429）＝false，例外：外部服務尚未設定的 503＝false。`messageEn` 不含動態值（件數、訂單編號），要顯示數字用 `messageZh` 或自備字串表。實作 `apps/api/Common/ApiErrorEnvelope.cs`／`ApiErrorMessages.cs`，測試 `ApiErrorEnvelopeTests`。⚠️ 殘餘：後台一族例外仍沒有**專屬** code（只有通用代碼）；429 是否值得依 `Retry-After` 等待後重試，規劃書未定義（現為 `retryable=false`，標頭照舊） |
| 2 | **App 端點範圍**：`openapi.json` 目前是「全部非後台端點」（124 條路徑），含官網專用的 SEO、表單、行事曆 feed 等 | 規劃書 §9.2 的清單才是 App 真正會呼叫的；收斂成白名單屬後續優化（`filter-openapi.mjs` 的 `EXCLUDED_PREFIXES` 改成白名單即可） |
| 3 | ✅ **官網路徑與深連結對照的落差（2026-10-05 已補齊）** | `apps/web` 已有 `/zh/schedule/{隊別}/`、`/zh/schedule/{賽事 id}/`、`/zh/member/upgrade/`、`/zh/app/`（皆含 `/en/`、兩站共用）；球員路由參數改名 `[slug]`。✅ **球員 `slug` 已補（2026-10-05）**：`players.slug`（必填、`[a-z0-9-]`、唯一鍵 `(club_id, slug)`），公開 `GET /{club}/players`（每筆帶 `slug`）與 `GET /{club}/players/{slug}`（也接受 id），後台可改「網址代稱」。⚠️ **賽事仍沒有 `slug` 欄位**（`matches`），`tcrfc://match/{id}` 目前就是 id，規劃書 §2.3 寫的是賽事 id，不受影響；課程已改依 API 的 `programs.slug` 動態渲染（2026-10-05），五個靜態頁的 slug 若與 API 不同，App 對應不到那五頁（靜態頁優先、API 同 slug 才會被靜態頁蓋住），屬內容對照問題。詳見下方「官網回退規則」 |
| 4 | ✅ **英文路徑（2026-10-05）**：`deeplinks.json` 新增 `webLocales`，`universalLinkPaths` 含 `/en/` 一份（官網每個 `/zh/` 頁面自動有 `/en/` 孿生，路徑規則只換語系前綴） | `check-shared.py` 已驗證兩者一致並加了 `/en/` parseCases；AASA 的 `paths` 要一併列 `/en/*`（AP-9 產檔時照 `universalLinkPaths`） |
| 5 | **藍鯨**：`tcrfc://schedule/bw1` 的官網回退網址、藍鯨網域（B-4） | 回退網址已定為 `/zh/schedule/bw1/`（藍鯨站同一套頁面，2026-10-05 已可用）；**藍鯨網域仍未定**（B-4），`domains.blueWhale` 維持 `null`，未解前藍鯨 Universal Link 不得上線 |
| 6 | ✅ **未知深連結的行為、`/schedule/{隊別代號}` 與 `/schedule/{賽事}` 的區分（2026-10-05 執行層決定）** | 見下方「官網回退規則」 |
| 7 | ✅ **會員卡 7 天提醒跨「裝置重啟」（2026-10-05 產品決定，兩端一致）** | 見 §4「會員卡」表的「最後同步時間與 7 天提醒」跨重啟規則 |
| 8 | **曝光連續區間的起點**（見 `shared/ad-viewability.md` §6） | 取「第一筆達標取樣」，偏保守；對帳需要更精確再改規劃書 |


**官網回退規則（執行層決定，2026-10-05，`apps/web`）**——規劃書 §2.3 只規定「未安裝 App 的裝置點到深連結一律回退官網對應網址，**不得顯示錯誤頁**」，下面是官網怎麼落實：

| 深連結 | 官網網址 | 解析方式 | 認不得時（302，保留語系） |
|---|---|---|---|
| `tcrfc://schedule/{team}` | `/{zh\|en}/schedule/{隊別}/` | 參數不分大小寫等於該站隊別分頁的 `Team.code`（`d1`／`bw1`／`u15`／`bw-u15`）或分頁 id（`first-team`／`club`） | `/schedule/`（別站的隊別代號也算認不得：磐石站不認 `bw1`，藍鯨站不認 `d1`） |
| `tcrfc://match/{id}` | `/{zh\|en}/schedule/{賽事 id}/` | 與上列**同一個路徑前綴**：先比對隊別，不是隊別再當賽事 UUID 查賽事清單；命中則切到該隊別／賽季／賽果或賽程模式並**自動開啟賽事詳情彈窗** | `/schedule/` |
| `tcrfc://player/{slug}` | `/{zh\|en}/club/first-team/player/{slug}/` | ✅ 2026-10-05 後端已有 `slug`（`GET /{club}/players/{slug}`，也接受 id）；✅ 官網前端已改以 `slug` 解析（2026-10-05）：id 或大小寫不同的網址 301 到 slug 正規網址，找不到 302 回名單 | `/club/first-team/#roster`（缺參數的 `/player/` 同） |
| `tcrfc://news/{slug}` | `/{zh\|en}/news/{slug}/` | 既有 | `/news/`（**原本是 404，改為 302**；不回 200 空頁） |
| `tcrfc://store/{id}` | `/{zh\|en}/perks/{slug}/` | 既有 | `/perks/`（原 404） |
| `tcrfc://program/{slug}` | `/{zh\|en}/programs/{slug}/` | 五個靜態頁（`summer-camp`／`winter-camp`／`childrens-training`／`school-community`／`specialist`）優先；其餘 slug 進 `programs/[slug]/`，以 API `GET /{club}/programs/{slug}`（`ProgramDetailDto`）的 slug 渲染詳情（兩站、雙語；Course Schema 資料不足不輸出；線上報名只磐石） | `/programs/`（查無或後端回錯） |
| `tcrfc://upgrade` | `/{zh\|en}/member/upgrade/` | 已登入顯示會員中心「我的會籍」（升級入口）；未登入顯示權益對照表＋登入 | — |
| （下載） | `/{zh\|en}/app/` | App 下載頁；兩個商店網址走 `NUXT_PUBLIC_APP_STORE_URL`／`NUXT_PUBLIC_PLAY_STORE_URL`，空值＝「即將上線」 | — |

- 解析純函式在 `apps/web/shared/utils/schedule-route.ts`；`/schedule/{slug}` 與 `/schedule/` **同一個元件檔**（賽事行事曆不拆檔），第二條路由由 `nuxt.config.ts` 的 `pages:extend` 加，因此單元開關（`13`）兩站一致。
- 新聞／特約店家由 404 改 302 的取捨：站台上線前本來全站 `noindex`；下架文章的收斂交給後台 H 的 301 轉址管理。**不能回 200 配空版面**。
- `apps/web/scripts/check-deeplink-pages.mjs`（掛 `npm run lint`）逐列確認 `shared/deeplinks.json` 的 `webPath` 都有頁面。
- 🔴 **這張表沒涵蓋 App 端對壞連結的處理**（`tcrfc://` 無法解析時 App 怎麼辦）——那是 App 自己的決定。

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

### 🔴 伺服器端實作（AP-3 後端，2026-10-02）

| 項目 | 決定 |
|---|---|
| 入口 | 沿用 `POST /api/v1/member/auth/login`（`/line/callback`、`/line/complete`、`/change-password` 同理）——**請求帶 `deviceInstallId` 就把鏈掛在該裝置**，並強制 `tokenDelivery=body`；續期 `POST /member/auth/refresh`、登出 `POST /member/auth/logout` 帶 `refreshToken`，依權杖前綴 `ad1.` 分流到裝置鏈（網頁仍走 `member_refresh_tokens`）。**裝置須已先 `PUT /app/devices/{id}` 註冊**，否則 400 `device_not_registered` |
| 權杖格式 | `ad1.{裝置列 id}.{簽發毫秒}.{32 bytes 亂數}.{HMAC 簽章}`；伺服器只存整串 SHA-256（`app_devices.refresh_token_hash`）。**簽章金鑰**由會員權杖金鑰（`JWT_SIGNING_KEY_MEMBER`，未設則由 `JWT_SIGNING_KEY_CLUB` 衍生）再以 HKDF（用途標籤 `tcrfc-app-refresh-token-v1`）衍生 |
| 為什麼要簽章與簽發時間 | 規劃書 §10.1 只給四個欄位，**沒有「前一把權杖雜湊」**。輪替後舊權杖再被送來時，單靠雜湊不相符分不出「曾經合法、已被輪替掉（＝外洩）」與「亂猜的垃圾」。簽章通過＝確實核發過；簽發時間早於 `refresh_token_rotated_at`＝已被輪替掉。**簽章不合法一律無副作用拒絕**，所以亂送權杖不可能登出任何人。不必動綱要、不必改規劃書 |
| 四個欄位的語意 | `refresh_token_hash`＝現行那把的雜湊；`refresh_token_expires_at`＝**90 天滑動**（核發與每次輪替都重算）；`refresh_token_rotated_at`＝**現行這把的簽發時間（毫秒精度，首次核發也寫入）**，嚴格遞增；`revoked_at`＝鏈被撤銷的時間（重新登入時清空） |
| 輪替 | 條件式更新（比對舊雜湊且未撤銷），並行用同一把輪替只有一個成功；輸的那個等同重用 |
| 重用偵測 | 簽章合法、雜湊不是現行那把、簽發時間早於上次輪替 → 撤銷整條鏈（雜湊與到期清空、`revoked_at` 設值、**解除 `member_id` 綁定**；裝置列與推播訂閱保留）。**鏈已撤銷後再被送來的舊權杖不再擴大處置** |
| 撤銷時機 | 登出（只撤銷「現行那把」，過期或已輪替掉的舊權杖不能拿來登出別人）、登出全部裝置／變更密碼／重設密碼／刪除帳號（`MemberSessionService.RevokeAllAsync` 兩條鏈一併撤銷）、會員自己撤銷某裝置（`GET /member/devices`、`POST /member/devices/{deviceId}/revoke`，只能動自己名下的裝置，別人的一律 404）、會員被停用（輪替時發現即撤銷） |
| 綁定 | 登入成功把 `member_id` 綁到該裝置（一個裝置同時只綁一個會員，後登入者取代）並同步 `push_topic_subscriptions.member_id`；登出／撤銷解除綁定 |
| ⚠️ 已知取捨 | **行動網路下「伺服器已輪替、回應卻在途中遺失」，用戶端會拿舊權杖重試，會被判為重用而登出該裝置、需重新登入。** 因為沒有「前一把雜湊」欄位，無法安全容忍這種重試；寧可重新登入，不放寬重用偵測。若實測發現太常發生，選項是向規劃書申請增列一個欄位（先改規劃書 §10.1）做短暫寬限。存取權杖是無狀態 JWT，撤銷後最多再有效 15 分鐘（與網頁會員同一個取捨） |

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
| **防改時鐘與跨重啟（2026-10-05 兩端一致規則）** | 同一次開機內取「牆鐘差」與「單調時鐘差」較大者；偵測到**重啟**（Android：`Settings.Global.BOOT_COUNT` 與同步時不同，或 `elapsedRealtime` 比存下的小）後單調時鐘失去意義，**只看牆鐘差**；🔴 **若目前牆鐘早於同步當下的牆鐘（時鐘被往回調），一律視為已超過 7 天並顯示提醒**。牆鐘差比的是「同步當下記下的裝置牆鐘」與現在（不是伺服器時間），所以裝置與伺服器的固定時差不會被誤判為往回調。實作：`CardSync.staleDays`＋`SyncStamp`（`cardsync:{cardId}`，非機密，放一般偏好設定） |
| 7 天提醒 | 純 UI 判定；卡面永遠常駐「最後同步：YYYY-MM-DD HH:mm」 |
| 🔴 **跨重啟與時鐘往回調**（2026-10-05 產品決定，iOS／Android 一致） | ① **同一次開機內**取牆鐘差與單調時鐘差的**較大者**；② **偵測到重啟**（iOS 比對 `kern.boottime`、Android 比對開機時間）後單調時鐘歸零而失效，**改只看牆鐘差**；③ **目前牆鐘早於最後同步當下的裝置牆鐘時間**（時鐘被往回調）：**一律視為已超過 7 天並顯示提醒**。⚠️ 重啟後只看牆鐘，使用者仍可把時鐘調到「同步時之後」來壓低天數——這是已接受的殘餘風險（提醒本來就只是 UI 提示，不停用卡片）。判斷用的「牆鐘」是**裝置**時鐘（同步當下與現在各取一次），不用伺服器時間，免得裝置與伺服器的正常時差造成誤報 |
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

§13 要求冷啟動至首頁可互動 ≤3 秒，而台中↔東京（Japan East）的 RTT 約 **30–50ms**。首頁若串行呼叫三支端點，光網路約 0.1–0.2 秒。
⚠️ **壓力小了但七項緩解不要省**——行動網路的變異遠大於固網，3G／弱訊號下仍可能數百 ms。

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

## 11b. 後端落點：本檔各節在 `apps/api` 的哪裡（D 批，2026-09-30）

> 後台 `M1–M5`／`E4–E6` 與 App 公開端點已在 `apps/api` 實作（契約見 [`apps/api/README.md`](../apps/api/README.md) D 批節，表見 [`12b`](12b-database-tables.md) §16）。
> **App 客戶端仍未開發**——下表是「後端已經備好、客戶端要對齊的東西」，**不是新規格**。

| 本檔 | 後端落點 | 客戶端要對齊的地方 |
|---|---|---|
| §4 更新權杖／§7 設定下發 | `app_devices` 更新權杖四欄（AP-3 使用，M4 不讀寫）；`GET /api/v1/app/config`（`Features/AppPublic/AppConfigComposer`）與後台存檔時的 `IAppConfigPublisher`（**Cloudflare 靜態設定尚未串接**） | 第 1 層（Cloudflare JSON）與第 2 層（API）**內容相同**；`evaluation` 是伺服器代算的判斷，客戶端也可自行判斷，兩者必須一致；**版本用語意化比較，建置號不參與**；`payment_mode` 是 `stringValue`（`off`／`external`／`inapp`），只會降級，不會反向開啟 |
| §5 推播傳輸 | `IPushTransport`（**尚未串接**）、`PushDispatcher`（分眾在 .NET 端解析、逐批直送、游標續送）、`PushContentGuard`（中獎通知系統層阻擋）；**雙人覆核**（核可者≠建立者）與二次確認人數 | 裝置註冊帶 `locale`（推播語系）、`pushToken`、`pushPermission`；權杖失效由推播服務回報後伺服器標記，App 下次啟動要重新註冊權杖；「開啟」回報 `POST /api/v1/app/push/{id}/opened` 只累加彙總；**通知中心**讀 `GET /api/v1/app/notifications`（90 天、依分眾）。**payload 不放個資、不使用靜默推播** |
| §6.4 事件佇列 | `POST /api/v1/app/ads/events`（`AdEventIngestService`）：拒收超過 24 小時與未來 5 分鐘以上的事件、`presentationId` 去重、點擊 5 秒去重、檔期與版位由素材推導；批次上限 200；**同一 `batchId` 整份重送是安全的（全部算重複）** | 事件 `occurredAt` ＝單調時鐘推算＋`server_skew`（§6.1 時鐘校正，`server_skew` 由 `/config` 回應的 `Date` 標頭算）；**備援素材（`isFallback:true`、`creativeId:null`）不啟動量測器、不發事件**；每次素材裝載產生新的 `presentationId` |
| §6 廣告投放 | `GET /api/v1/app/ads/{slotCode}`（`AdServingService`）：只投投放中＋已審核未暫停的素材、每日上限、每人頻次上限（帶 `deviceInstallId` 才套用）、曝光保證 pacing、權重加權隨機（不重複、至多輪播上限）、無檔期回備援；回應的 `disclosureLabel`（「廣告」／「Ad」）**必須顯示** | 預先下載當日檔期素材（§2.4）；`sessionImpressionCap` 由 App 端控制單次使用的曝光；**帶裝置識別的請求不可被邊緣快取**（回應 `no-store`） |
| §8 監控 | `POST /api/v1/app/diagnostics`（`AppDiagnosticsIntake`）→ `app_diagnostic_reports`；後台 M5 診斷收件匣與彙總（無崩潰裝置比例、啟動耗時中位數／P90） | payload 去識別化（**伺服器端也會把 Email 與 8 位以上數字遮成 `[已遮蔽]`，但不能依賴它**）；發生時間不得超過 7 天前；API 錯誤率需要分母，診斷回報只送次數 |
| §7 定位 | 後端沒有任何座標欄位（`ad_events`、`app_diagnostic_reports` 皆無） | 座標只存在於記憶體 |

## 11c. iOS 客戶端 Phase A 的執行層決定（AP-2，2026-10-05）

> App 原始碼在 **`mobile/ios`**（`tcrfc-app-ios` 私有 repo，本機 `git init`，**尚未建 GitHub repo**，remote 只有 `Remote_NAS`（NAS 離線備份）；本 repo 以 `.gitignore` 排除，見 §1）。建置、`shared/` 同步與環境設定見該 repo 的 `README.md`。下列是本檔其他章節沒有、實作時定下的決定。

| 項目 | 決定 |
|---|---|
| 專案產生 | **xcodegen**（`project.yml`），`.xcodeproj` 不納管。Xcode Cloud 需要 `ci_scripts/ci_post_clone.sh` 安裝 xcodegen（AP-7 時做）。相依只有 **GRDB.swift 7**（SPM）；影像用自寫 `ImageLoader`（URLCache 150MB）而非 Nuke，Phase A 只有列表縮圖，免多一個相依 |
| `shared/` 取用 | `scripts/sync-shared.sh` 單向複製進 `Shared/` 並寫 `SHARED_MANIFEST.txt`（上游 commit、是否 dirty、sha256）；`--check` 驗 Shared 沒被手改、`--check-upstream` 驗沒落後上游。**時效、深連結、快取 DDL 一律執行期讀契約檔，不產生第二份常數**（DTO 例外：直接當原始碼編譯） |
| 環境 | `Config/Debug.xcconfig`＝`http://localhost:5299`（bundle id `tw.tcrfc.app.dev`）；`Release.xcconfig` 的 `API_BASE_URL` **留空**（正式網域未定，空值＝顯示無法連線而不是連錯地方）。Debug 另吃 `-TCRFC_FIXTURES YES`（內建預覽資料，`#if DEBUG` 且用 DTO 建構子寫成，Release 不含）與 `-TCRFC_API_BASE_URL` |
| 本機偏好 | `device_install_id` 放 **UserDefaults**（不放 Keychain：Keychain 撐過重裝，違反規劃書 §4.4「解除安裝即失效」）；追蹤偏好放 SQLite `follow_preference`；語系偏好放 UserDefaults |
| 快取鍵 | 契約 `cache_meta` 主鍵只有 `cache_key`，iOS 以 `<key>|<item>|<lang>` 組合鍵存放；契約沒有 TTL 的內容（俱樂部、夥伴、FAQ、設定）視為「每次冷啟動重新驗證、離線沿用本機」，**不發明時效** |
| 追蹤同步 | 追蹤偏好以 `PUT /app/devices/{id}/subscriptions`（`replaceAll`）送 `club`／`team` 訂閱，**`isPushEnabled` 一律 false**（追蹤 ≠ 推播）；離線時保留待補送旗標 |
| 首次啟動引導 | 只做語系與追蹤對象兩步。**第 3 步（推播權限）排到 AP-4**：沒有推播傳輸而請求權限會誤導使用者 |
| 深連結 | 解析在 `DeepLinkParser`（純函式，跑 `deeplinks.json` 全部 parseCases，含 `/en/`）；現在註冊 `tcrfc://`，`scene(_:continue:)` 已接好，**Associated Domains 刻意不設**（docs/17 §10.3），之後加 UL 只是 entitlement＋AASA。Phase A 內處理 schedule／match／news／membercard；player 導到名單；upgrade／program 外開官網對應頁；store 無可組網址時停首頁 |
| ATS | `NSAllowsLocalNetworking`（只放行本機位址）。送審前評估是否改成僅 Debug 生效 |
| 閘門 | 維護／必須更新／建議更新畫面全是本機資源；客戶端以語意化版本比較自判，與伺服器 `evaluation` 取較嚴者；三層來源目前只有 ②③，Cloudflare 靜態 JSON（①）屬外部服務，排後 |
| 未做 | 推播／APNs、Universal Link、行事曆匯入與開賽提醒、App icon（待藍鯨向量）、特約店家、會員（Phase B）、廣告 |

## 11d. Android 客戶端 Phase A 的執行層決定（AP-2，2026-10-05）

> App 原始碼在 **`mobile/android`**（`tcrfc-app-android` 私有 repo，本機 `git init`，**未建 GitHub repo**，remote 只有 `Remote_NAS`（NAS 離線備份）；本 repo 以 `.gitignore` 排除，見 §1）。建置、`shared/` 同步與環境設定見該 repo 的 `README.md`。

| 項目 | 決定 |
|---|---|
| 工具鏈 | AGP 8.10／Kotlin 2.1／Gradle 8.13／JDK 17 target（以 JBR 21 編譯）／`compileSdk`＝`targetSdk` 36／`minSdk` 29。**無 Hilt**（手動 DI）。兩個 flavor：`dev`（`tw.tcrfc.app.dev`、預覽資料、開發工具、僅 dev 允許明文連本機）／`prod`。`testDebugUnitTest` 在雙 flavor 下有歧義，build 檔提供彙總任務 |
| `shared/` 取用 | `scripts/sync-shared.sh`＋`shared_tool.py`：單向複製進 `shared/`，`SOURCE.json` 記上游 commit 與 sha256；`verify` 驗本地未被手改、給上游路徑時驗漂移。DTO（`TcrfcApiModels.kt`）**直接當來源目錄編譯**（✅ **Kotlin DTO 首次編譯通過，§2 的「尚未以 kotlinc 驗證」已解**）；`cache-schema.sql` 由 Gradle 轉成 SQLDelight `.sq`（**migration ≥ 2 時刻意讓建置失敗**，須補 `.sqm`＋`deriveSchemaFromMigrations`）；`cache-policy.json`／`deeplinks.json`／`error-codes.json` 打包成 assets、執行期讀取 |
| 快取鍵 | `cache_meta.cache_key`＝`{policyKey}:{scope}:{lang}`（scope 例：俱樂部代碼）。**規劃書 §2.4 沒列的內容（俱樂部、夥伴贊助、FAQ、App 排版）不另訂 TTL（2026-10-05 與 iOS 對齊）**：每個行程（冷啟動）只重新驗證一次，離線沿用本機；原先暫用的 24 小時已移除。分頁聚合（賽程、名單）不使用 ETag |
| 時間 | `match_on`／`kickoff` 是 **Asia/Taipei 牆上時間**（非 UTC，與 apps/web 同），客戶端換算成時刻後依裝置時區顯示；比分是官方主客隊順序，客場時換邊 |
| 語系 | `AppCompatDelegate.setApplicationLocales`＋`AppLocalesMetadataHolderService`；Activity 設定語系為最終答案；`MainActivity` 繼承 `AppCompatActivity` |
| 備份 | `allowBackup=false`＋雲端備份與裝置搬移規則全排除（deviceInstallId 與日後權杖都不得被帶走） |
| 深連結 | `tcrfc://` 已註冊；https App Links 的 intent-filter 以註解留在 Manifest（取消註解＋填網域＋部署 assetlinks 即可，解析器已支援 https 並有測試）。無法辨識／本版無畫面的連結：未知→首頁、店家／課程／升級→開官網對應頁，**不顯示錯誤頁**。`/schedule/{x}`：x 為 d1／bw1 是球隊，其餘當賽事（缺口 6 的實作決定）；`/en/` 前綴已由 `shared/deeplinks.json` 的 `webLocales` 正式涵蓋（缺口 4 已解） |
| 圖片 | 衍生檔鍵規則（`{stem}-{320｜640｜1280}.webp`，apps/api `ImageObjectKey`）**在客戶端複製了一份**，因為 `shared/` 沒有——見下方缺口 |
| 閘門 | 維護／強制更新畫面為純本機資源；版本客戶端自判並與伺服器 `evaluation` 取較嚴者；三層來源目前只有 ②③（①需 Cloudflare 靜態 JSON，排後） |
| 未做 | 推播與引導第 3 步、App Links、Tink（Phase B 才有權杖）、行事曆整季匯入、開賽提醒、全站搜尋、特約店家／會員／廣告／課程、隱私政策與條款頁、CI（AP-7） |

**Android 實作發現的 `shared/` 契約缺口與後端待配合**（皆待決，不在 `shared/` 補規格）：

| # | 缺口 |
|---|---|
| A1 | ✅ **2026-10-05 已補**：`GET /api/v1/{club}/competitions?season=&lang=`（只回已發布，每筆 `code`／`clubCode`／`seasonCode`／`compType`／`name`／`organizer`，**沒有賽事也會列出**，空狀態可做）；單場賽事 `GET /api/v1/{club}/schedule/{id}`（深連結 `tcrfc://match/{id}`；別的俱樂部 404）；賽程新增篩選 `?competition=<系列代碼>&from=&to=`（含頭尾，from 晚於 to 回 400） |
| A2 | ✅ **2026-10-05 已補（部分）**：`MatchDto` 新增 `clubCode`、`competitionCode`；`TeamDto` 新增 `clubCode`。⚠️ ① **`TeamDto` 沒有賽事系列代碼**——資料模型裡球隊與賽事系列沒有直接關聯（系列掛在球季、賽事掛在系列），要的話只能從賽事推；② **賽事仍沒有 slug**——規劃書 §2.3 寫的本來就是 `tcrfc://match/{id}`（賽事 id），官網 `/schedule/{slug}` 的 `{slug}` 以賽事 id 解析，兩邊一致，不需 slug；③ **`ClubDto` 沒有簡稱欄位**：資料模型（`docs/12`、`clubs_i18n` 只有 `name`／`description`）與兩份規劃書都沒有俱樂部簡稱欄位（規劃書 §0 只規定「中文簡稱一律寫『台中磐石』」的寫法），**沒有發明**，需要的話要先由規劃書決定欄位（建議 `clubs_i18n.short_name`），見回報 |
| A3 | ✅ 2026-10-05（上一批）：`PlayerDto.slug`、`GET /{club}/players/{slug}` |
| A4 | ✅ **2026-10-05 已補**：公開內容列表端點支援 `ETag`／`If-None-Match` → 304（見下方「§11e ETag」）。伺服器時間仍沒有 `Date` 以外的欄位（`/app/config` 與 `/app/layout` 有 `generatedAt`） |
| A5 | ✅ **2026-10-05 已補**：命名規則寫進 `shared/image-derivatives.json`（產生檔，來源 `apps/api/Images`）。DTO 的 `*Url` 仍是**主檔**網址（相容），要小圖由主檔網址推導，規則與範例在該檔 |
| A6 | ✅ **2026-10-05 已補（部分）**：`AppLayoutItemDto` 新增 `isExternal`；`moreItems` 的 `shop` 帶 `webUrl=/{lang}/shop/`（相對路徑，用戶端接官網網域）、`charity` 帶後台 B5 設定的捐款導流網址（絕對網址，`isExternal=true`，須明示收受者為協會）；`AppLayoutResponse.sponsorshipInquiryWebUrl=/{lang}/partners/become-a-partner/`。⚠️ ① 慈善網址取自站台設定 `charity.donation_url`，**未設定時 `webUrl=null`**（本機種子是 `https://charity.example.com/` 測試值）；② 贊助洽詢沒有站台設定，是官網既有頁面路徑寫在 API（若官網改路徑須同步） |
| A7 | ✅ **2026-10-05 已定**：標頭 `X-Device-Install-Id`（見下方「§11e 裝置識別」）。**後端有帶就驗證格式、沒帶不拒絕**——理由見該節 |
| A8 | ✅ **2026-10-05 已實作**（規劃書 §2.5 明定「未翻譯 fallback 繁中並標示」）：Club／Team／Player／Staff／Competition／Match／新聞列表與內文／FAQ 新增 `isFallbackLocale`。其餘型別（夥伴、特約店家、課程、站台設定等）尚未加 |
| A9 | 未改（兩端須對齊，見上「快取鍵」） |
| A10 | ✅ **2026-10-05 已補**：`shared/enums.json`（產生檔，來源 `db/club-schema.sql` 的 CHECK）：`TeamDto.type`＝`first_team`／`academy`，`gender`＝`men`／`women`／`mixed`，`MatchDto.status` 五值。`TeamDto` 的 DTO 註解也寫了值域 |

### 11e. 第三批契約補強的執行層決定（2026-10-05，`backend-engineer`）

| 項目 | 決定 |
|---|---|
| **ETag／304** | `Common/ConditionalGetMiddleware.cs`。ETag＝**這次實際回應本文**的 SHA-256（強驗證），請求的 `If-None-Match` 命中（含 `W/` 前綴、多值清單、`*`）才回 304（無本文）。**每個請求仍走完整管線（含既有 `IQueryCache`）**，所以不新增快取層、不延長任何資料的有效期、不可能回舊資料；只省重複下載。🔴 **只掛白名單**（`/clubs`、`/app/layout`、`/app/config`、`/{club}/` 的 schedule、competitions、teams、players、staff、news、faqs、partners、sponsors、partner-stores、standings、venues、achievements、milestones），**不含** docs/14／docs/17「五類不得讀快取」與個人化／即時端點：商店（庫存、購物車、我的訂單）、會員一族、`/m/*` 會員卡、課程與梯次（含即時名額）、廣告、通知中心、裝置訂閱、後台——這些完全不經過中介軟體（`AppContractGapsTests` 逐一驗證不帶 ETag）。CORS 已 expose `ETag`。不改 `Cache-Control`。⚠️ 分頁聚合（賽程、名單）客戶端自行決定要不要用，伺服器每頁各有 ETag |
| **裝置識別（§9.3）** | 標頭 **`X-Device-Install-Id`**，值＝註冊裝置時的 `deviceInstallId`（網址不放：會進日誌與分享連結）。查詢參數 `deviceInstallId` 仍相容，標頭優先。有帶：驗證格式（不合 400）、用於公告條對象／通知中心／廣告頻次，且該回應 `private, no-store`；**沒帶：不拒絕**——官網與其他非 App 用戶端共用同一套公開端點不會帶，且規劃書 M2 本來就定義「沒帶裝置識別時只回全體對象內容、可邊緣快取」。其他公開內容端點接受但忽略。若要強制（拒絕沒帶的 App 端點），須先改規劃書 §9.3 說明並分離 App 與官網的端點，現不做 |
| **未翻譯標示** | `isFallbackLocale`（bool，必填）＝請求英文、而該筆英文主要欄位（名稱／標題／問題／對手）為空，回應是回退的繁中；請求繁中恆為 false。沿用既有 `IsFallbackLocale`（站台字串、搜尋）的命名 |
| **時間語意（已拍板，選項 1＋2）** | 見下方「§11f」。App 規劃書 v3.14 §3.2 已改 |

### 11f. 時間語意：已拍板選項 1＋2（2026-10-05；原調查見下表，資料未改）

| 來源 | 內容 |
|---|---|
| **App 規劃書 §3.2** | 「**資料層儲存 UTC**，顯示依裝置時區換算，並標示『賽事時間可能異動』」 |
| **主站規劃書** | 賽程單元（行 676、703）只寫「依使用者當地時區顯示、標註時間可能異動」，**沒有**「儲存 UTC」的字樣（只有 App 規劃書有） |
| **docs/12 §1／§12 第 31 點** | `datetime` 一律存 UTC（時間戳）；但 **`Match.match_on`（`date`）＋`kickoff`（`nvarchar(8)`，如 `19:00`）與 `original_*` 明定是「當地牆上時間的展示值，不是可換算時區的時間戳」**，刻意不合併成 `datetime`。docs/12 本身自洽，與 App 規劃書那句衝突 |
| **DDL** | `matches.match_on date NOT NULL`、`kickoff nvarchar(8) NULL`、`original_match_on date`、`original_kickoff nvarchar(8)`：沒有任何時區資訊，也沒有 UTC 時間戳欄位。`kickoff` 可為空（舊資料多為空，例如 2023 年藍鯨賽事） |
| **API 輸出** | `MatchDto.matchOn`（`yyyy-MM-dd`）、`kickoff`（`HH:mm` 字串）**原樣是台北當地牆上時間**。唯一做時區換算的是單場 `.ics`（`CalendarIcsRepository.ToUtc`：視為 Asia/Taipei，減 8 小時）。對照：`calendar_custom_events.starts_at`／`ends_at` 是真 UTC 時間戳（API 輸出帶 `Z`） |
| **客戶端現況** | Android 已採「Asia/Taipei 牆上時間，換算成時刻後依裝置時區顯示」（§11d），與 apps/web 一致 |

**差異**：App 規劃書那句「資料層儲存 UTC」對賽事**不成立**；賽事時間是無時區的台北牆上時間，且 `kickoff` 可為空（沒有開賽時間就無法算出時刻）。其餘時間戳欄位（`datetime2`）確實是 UTC。

**2026-10-05 已拍板：選項 1＋2，選項 3 不做。** 選項 1 已完成：App 規劃書升 **v3.14**，§3.2〈時間處理〉改為「賽事日期與開賽時間以台北當地時間存放；其餘時間戳存 UTC；顯示依裝置時區換算，API 另提供換算後的 UTC 時刻；開賽時間未定只顯示日期、不排開賽提醒、行事曆以全天事件寫入」（中英版、PDF、`docs/11`／`12`／`14` 同步；主站規劃書只寫「依使用者當地時區顯示」、沒有儲存規定，不需連動）。選項 2 已實作（`MatchDto.kickoffAt`，見 §11h）。**客戶端須知**：`kickoff` 為空時畫面標示「開賽時間未定」、不猜 00:00（兩端 `ReminderPlanner` 與 `.ics` 早已如此，驗證時對照這一句）。**以下是拍板前的調查與選項說明，保留作依據**：
1. **改規劃書說明**（建議）：App 規劃書 §3.2 改為「賽事日期與開賽時間以**台北當地時間**（`Asia/Taipei`，無夏令時間）存放；App 換算為時刻後依裝置時區顯示；其餘時間戳存 UTC」。零資料風險，與 docs/12、現有網站與兩端 App 一致。走規劃書同步鏈（客戶版文件也要同步）。
2. **在 API 補衍生欄位**（相容擴充，不動資料）：`MatchDto.kickoffAt`（ISO 8601 UTC 時刻，`matchOn`＋`kickoff` 以 +08:00 換算；`kickoff` 為空時為 null）。兩端 App 不必各自寫換算，也避免日後有人誤把牆上時間當 UTC。可與選項 1 並行。
3. **改資料**（不建議）：把賽事改存 UTC 時間戳。需 migration、後台賽程表單、官網、行事曆、`.ics` 全部連動，且 `kickoff` 為空的歷史資料無法換算；收益只是文字上對齊規劃書。

## 11e-1. Android 客戶端 Phase B 會員的執行層決定（AP-3，2026-10-05）

> 原始碼同 §11d 的 repo。**已採用後端新契約**：所有錯誤回應都有 `code`／`messageZh`／`messageEn`／`retryable`（依本文解析，不依 Content-Type）、`PlayerDto.slug`。

| 項目 | 決定 |
|---|---|
| 安全儲存 | **Tink（`tink-android`，AES-256-GCM）＋ Android Keystore 主金鑰**（`TinkSecureStore`）：更新權杖、存取權杖、會員卡 token；key 當 associated data。解密失敗（Keystore 金鑰失效）視為沒有、重新登入。**未使用 StrongBox**（需要時在 `KeyTemplates` 之外另設，尚無需求）。全部 App 資料已排除自動備份（驗證 #5 的前提） |
| 工作階段 | `SessionManager`：登入請求帶 `deviceInstallId`＋`tokenDelivery=body`，**登入前先確保裝置已註冊**（否則後端 400 `device_not_registered`）；**single-flight 續期**（Mutex，等待鎖期間別人已續期就直接取用）；401 續期後重試一次；續期回 401／400／403＝本機登出並清除所有權杖與會員卡 token，網路／5xx＝保留工作階段。🔴 **續期（及重產 token、加入俱樂部、註冊、登出等改變狀態的 POST）不自動重試**——伺服器已輪替而回應遺失時，用舊權杖重試會被判為重用而撤銷整條鏈（§4 已知取捨）；讀取與登入才依 `retryable` 重試 |
| 錯誤與重試 | 重試只看回應 `retryable`（缺少時退回「5xx 才重試」）；畫面依語系顯示 `messageZh`／`messageEn`，沒有訊息（斷網）用本機字串 |
| 會員卡 | `member_card` 表（共用 DDL）存卡面欄位；**token 只在安全儲存區**（驗證 #4）；QR＝`{官網 base}/m/{token}` 本機組出（zxing-core 只編碼、Compose Canvas 繪製）。最後同步＝**該次回應的 `Date`**＋當時單調時鐘；天數＝牆鐘差與單調差取大者；**裝置重啟後單調時鐘重置時只看牆鐘**（缺口 7 的實作決定，可被改時鐘繞過，待產品確認）；≥7 天顯示提醒，**不停用、不隱藏 QR**；狀態寫成「最後同步時的狀態」並與同步時間並陳 |
| 會籍狀態 | 每次進入／回前景重取，**不快取**；離線時會籍頁只顯示無法取得，會員卡才離線可出示（驗證 #3） |
| 首版付款 | **不建立訂單、不顯示金額**（方案 DTO 的 `fee` 不渲染）：「了解如何加入」外開官網 `/zh/member/upgrade/`；回前景重取會籍（零深連結回程）。`payment_mode` 本版未實作（沒有 App 內付款，無切換需求），Phase E 時加 |
| 年齡閘門 | 註冊要生日；未滿 18 歲須勾選「監護人同意」才可送出（**僅客戶端流程，後端沒有對應欄位**——見缺口 B2） |
| 特約店家 | 清單＋地區／類別／文字篩選、詳情、`ACTION_DIAL`、`geo:` Intent＋chooser。定位用系統 `LocationManager` 單次取位（**不依賴 Google Play 服務**），座標只在 ViewModel 記憶體、不入 SavedState／SQLite／日誌；拒絕時功能不中斷；**沒有座標的店家只進列表、排在有距離者之後**；不做 geocoding。**地圖檢視未做**：Google Maps SDK 要金鑰（綁套件名＋SHA-1、每日配額）與帳單帳戶，屬外部服務，排後；目前以系統地圖 App 開單家店 |
| 未驗證 Email | 登入回 403 `email_not_verified` → 顯示訊息並提供「重新寄送驗證信」（本機信件是寫檔假實作，驗證連結在官網完成） |
| 預覽用假後端 | `FixtureMemberRemote`（dev 版預覽資料模式）：`member@example.com`／`Passw0rd!`、`unverified@example.com`；只在記憶體 |

**Android 在 Phase B 發現的契約缺口／待配合**：

| # | 缺口 |
|---|---|
| B1 | ✅ **2026-10-05 已補**（見 §11h）：`GET /api/v1/member/draws`。原缺口：**沒有「抽獎資訊」的 App 端點**（規劃書 §3.10、§9.2「抽獎資訊 讀取」）：`openapi.json` 只有 `member/*` 與公開端點，沒有 `MemberDraw` 的個人資格布林。App 抽獎畫面因此未做 |
| B2 | ✅ **2026-10-05 已補**（見 §11h）：註冊／LINE 註冊帶 `birthOn`（必填）與 `guardianConsent`。原缺口：註冊請求沒有「監護人同意」欄位，未成年同意流程只能在客戶端擋；規劃書 §4.5 要求 App 內呈現同意流程，但後端無法留存同意紀錄 |
| B3 | ✅ **2026-10-05 已補**：`MemberCardDto.serverTime`（UTC）。原缺口：`MemberCardDto` 有 `token` 但**沒有「伺服器時間」欄位**，最後同步時間只能取回應 `Date` 標頭（CDN／代理可能改寫或缺少） |
| B4 | ✅ **2026-10-05 已定**（見 §11h）：`null`＝未確認，DDL 與後台驗證不允許 (0,0)。原缺口：**沒有店家座標狀態的公開說明**：`lat`／`lng` 可為空，客戶端把「空」當「未確認」；若後端之後改用 0,0 之類的哨兵值會被當真座標 |
| B5 | 註冊／忘記密碼／重寄驗證信都要帶 `club`（決定信件連結網域），App 取「追蹤的第一個俱樂部」；規劃書沒定義 App 註冊時該用哪個俱樂部 |
| B6 | LINE 一鍵登入（§4.2）需要 LINE Login 頻道與回呼網址（`invalid_redirect_uri` 白名單）——App 用的回呼（自訂 scheme 或 App Links）未定義 |
| B7 | 電子信件在本機是寫檔假實作：端到端的「註冊→收信→驗證→登入」無法在 App 內驗收 |

## 11g. iOS 客戶端 Phase B 會員的執行層決定（AP-3，2026-10-05）

> 原始碼同 §11c 的 repo（`tcrfc-app-ios`，僅本機 commit）。**已採用後端新契約**：錯誤回應的 `code`／`messageZh`／`messageEn`／`retryable`、`PlayerDto.slug`、`CompetitionDto`／單場賽事、`MatchDto.clubCode`／`competitionCode`、`isFallbackLocale`、`image-derivatives.json`、`enums.json`、ETag、`X-Device-Install-Id`、版面編排的 `isExternal`／`sponsorshipInquiryWebUrl`。

| 項目 | 決定 |
|---|---|
| 安全儲存 | `KeychainStore`（`AfterFirstUnlockThisDeviceOnly`）存**更新權杖**與**每張會員卡的 token**；存取權杖只在記憶體。登出、被伺服器撤銷、刪除帳號時一併清除（含 `member_card` 列與卡片牆鐘標記）。驗證 #4 有測試：整個 SQLite 全文搜尋 token 找不到 |
| 單飛續期 | `TokenVault`（actor）：並行請求與 401 只觸發**一次** refresh（離到期 30 秒內就先換）；續期回 401＝清除登入並通知畫面，網路／5xx＝保留登入。**更新權杖鏈掛 `app_devices`**：登入帶 `deviceInstallId`＋`tokenDelivery=body`，**登入前先確保裝置已註冊**，`device_not_registered` 時補註冊再試一次 |
| 🔴 重試規則 | `APIRequest.retryable`：**預設只有 GET 與 PUT 會自動重試**；POST／DELETE（續期、重產 token、加入俱樂部、註冊、改密碼、刪除帳號…）**不重試**——續期被伺服器輪替而回應遺失時，用舊權杖重試會被判為重用而撤銷整條鏈（§4 已知取捨）。登入沒有副作用，明確允許重試。重試與否另看回應的 `retryable`（`false` 的 503 不重試） |
| 錯誤文字 | 畫面依語系顯示 `messageZh`／`messageEn`（英文缺漏退回繁中）；斷網與無本文用本機字串；不顯示技術細節 |
| 會員卡 | `member_card` 存卡面欄位；QR＝`https://{ClubDto.domain}/m/{token}`，**完全在本機組出**（CoreImage，要先轉 `CGImage`，否則 SwiftUI 畫成空白）。最後同步＝該次回應的 `Date` 標頭；同步當下另記**裝置牆鐘**與**開機時間**（放 UserDefaults，非機密）供 §4 的跨重啟規則使用。狀態與同步時間並陳；逾 7 天只提醒，**不停用、不隱藏 QR** |
| 會籍狀態 | **只放記憶體、不寫快取**；每次進畫面與回前景都重取；離線時 `memberships == nil`，會員中心只說明「無法確認會籍狀態」，卡片仍可出示（驗證 #1、#3） |
| 首版付款 | 不建訂單、不顯示金額（方案 `fee` 不渲染）；「了解如何加入／升級／續會」外開官網 `/{lang}/member/upgrade/`（俱樂部有自己的網域就用它）；**回前景重取會籍**，零深連結回程。免費加入俱樂部走 `POST /{club}/member/memberships/join` |
| 註冊歸屬俱樂部 | 取**追蹤的第一個俱樂部**；全部追蹤（或沒設定）取磐石（與 Android 一致） |
| 年齡閘門 | 註冊要生日；未滿 18 歲須勾選「監護人同意」（僅客戶端流程，後端沒有欄位，見缺口 B2） |
| 特約店家 | 清單（距離排序、類別／地區／文字篩選、「約」標示粗略定位）＋ **MapKit 地圖**（只標有座標者）＋詳情（撥號 `tel:`、導航 `MKMapItem`／無座標退回店名搜尋、適用層級與升級 CTA）。定位：`WhenInUse`、單次取位、先說明再請求、拒絕時功能不中斷；座標只在記憶體；**不做 geocoding**。首頁區塊 `nearby_stores` 已實作（不在首頁彈授權） |
| 賽事系列 | 第三層晶片取自 `GET /{club}/competitions`（含尚無賽事者，選到顯示「尚未公布」空狀態）；篩選用 `competitionCode`；同代碼跨俱樂部合併為一顆 |
| 分頁聚合與 ETag | 賽程整季聚合**不送** `If-None-Match`（第一頁沒變不代表後面沒變）；單頁清單照常 304 |
| 裝置識別標頭 | 只有 `/app/*`（版面編排）帶 `X-Device-Install-Id`；公開內容端點不帶，維持可被邊緣快取 |
| 圖片 | 衍生檔命名取自 `image-derivatives.json`（測試逐筆重現契約範例）；列表 320、詳情 640／1280，不取主檔 |
| 俱樂部名稱 | 🔴 一律顯示**全名**，不自行推導簡稱（主站規劃書 §0：簡稱寫「台中磐石」、不得單獨用「磐石」）；等後端 `ClubDto.shortName`（只改 `ClubDisplay.shortName` 一處）。代價是球隊分頁標籤變長 |
| 版面編排 | 「更多」依 `moreItems`：`shop`／`charity` 帶網址才顯示；慈善為外連，開啟前明示「收受者是協會」；贊助洽詢網址取 `sponsorshipInquiryWebUrl` |
| 預覽資料 | `FixtureMemberAPI`（Debug）：任何 Email＋`Test1234` 可登入；`unverified@example.com`、`locked@example.com` 走錯誤路徑；`-TCRFC_DEBUG_SIGNED_IN YES` 直接登入 |
| 未做 | 抽獎資訊（後端沒有 App 端點，缺口 B1）、LINE 登入、球衣登記、我的報名、推播、隱私政策與條款頁、Sign in with Apple（待確認） |

**iOS 在 Phase B 發現的缺口／待配合**：B1（抽獎無端點）、B2（監護人同意無欄位）、B3（卡片無伺服器時間欄位，只能取 `Date` 標頭）、B5（註冊的 `club` 歸屬）、B6（LINE 回呼）、B7（信件寫檔，無法在 App 內驗收完整驗證流程）同 §11e-1；⚠️ **以下 B8／B9／B10 已於 2026-10-05 處理（見 §11h）**：另有 **B8**：`MembershipPlanPublicDto`／`PartnerStorePublicDto` 等沒有 `isFallbackLocale`（A8 只涵蓋部分型別），英文介面無法標示未翻譯；**B9**：`ClubDto.shortName`（見上）；**B10**：`PartnerStorePublicDto.lat`／`lng` 為空代表「座標未確認」，若後端改用 0,0 哨兵值，App 目前把 (0,0) 也當作沒有座標（已防）。

### 11e-2. AP-3 第二輪與 AP-4 Phase C（Android，2026-10-05）

| 項目 | 決定 |
|---|---|
| **App 註冊歸屬俱樂部** | 規劃書沒寫，執行層決定：**追蹤的第一個俱樂部；「全部追蹤」（含略過）取清單第一個＝磐石**（`AppContainer.primaryClub()`）。註冊、忘記密碼、重寄驗證信都帶它（決定信件連結網域）。帳號本身仍是單一 `Member`，不受影響 |
| 第三批契約已採用 | `competitions`（賽事系列晶片含**沒有賽事的系列**，選到時顯示「這項賽事的賽程尚未公布」）、`schedule/{id}`（`tcrfc://match/{id}` 找不到時改打它，俱樂部不明時依序試）、`MatchDto.clubCode`／`competitionCode`／`TeamDto.clubCode`（拿掉自行標註）、`isFallbackLocale`（英文介面下標「（Chinese only）」＝fallback 繁中並標示，涵蓋新聞、FAQ、球員、賽事）、`image-derivatives.json`（客戶端不再手抄命名規則，`ImageUrls` 讀契約並有契約範例測試）、`enums.json`（`MatchDto.status` 為 null 視為 scheduled、未知值＝UNKNOWN 不當機、非 `first_team` 視為學院）、標頭 `X-Device-Install-Id`（所有請求，取代查詢參數）、layout `moreItems.webUrl`／`isExternal`（商店、慈善外開；**慈善外開前顯示「收受者是台灣足球策略發展協會，不是俱樂部」**）與 `sponsorshipInquiryWebUrl`。賽程仍一次載整季（`from`／`to`／`competition` 篩選未用，客戶端本機篩） |
| 開賽提醒 | **本機排程通知**（`AlarmManager.setAndAllowWhileIdle`，不精確、不需精確鬧鐘權限；不需 FCM）。開賽前 2 小時、依追蹤的球隊、延賽／取消／沒有確切開賽時間不排、最近 60 天內最多 40 場（`ReminderPlanner`，純函式）；開賽時刻優先用 `MatchDto.kickoffAt`。賽程更新、追蹤改變、開關切換、重開機（`BOOT_COMPLETED`）、回前景（權限可能在系統設定被改）都會重排；點通知走 `tcrfc://match/{id}`。🔴 **設定值預設開**（規劃書 §6.2「賽事提醒」預設列為開，2026-10-05 更正，與 iOS 一致），**但沒有系統通知權限就不排**。權限三態：已允許／**尚未詢問**（從沒請求過）／被拒絕（請求過仍未允許）。引導第 3 步先說明用途，按「開啟提醒」才請求（Android 13+ 的 `POST_NOTIFICATIONS`）；按「稍後再說」或略過＝不請求，設定頁顯示「尚未詢問」，**打開開關或按「開啟通知」時才先說明再請求**；被拒絕時只顯示狀態與前往系統設定的捷徑，**不反覆請求**。**FCM 推播、靜音時段、分類開關（皆屬推播）仍未做，介面隔開** |
| 廣告版位 | `AdSlot`（`home_top`／`home_mid`／`schedule_list`／`news_list`；學院與課程畫面不放）。🔴 **判定是純函式 `ViewabilityEngine`，單元測試逐案跑 `shared/ad-viewability-cases.json`（曝光 18＋點擊 4 案全過）**；Compose 只負責 `visibleRatio`（`onGloballyPositioned` 只寫 state，200ms 迴圈取樣，`boundsInWindow` 已扣祖先裁切），行程生命週期 RESUMED 為前景；備援素材不啟動量測、圖片解碼成功才啟動；`presentationId` 每次裝載新產生；事件 `occurredAt`＝**單調時鐘推算＋最近一次伺服器時間為錨**（改裝置牆鐘無影響，測試證明）；點擊 5 秒內去重並先顯示離開提示（`tcrfc://` 直接走深連結）；**輪播每 6 秒換一則**；`sessionImpressionCap` 於 session 內計數，**達上限即停止輪播與量測**；`ads_enabled` 旗標**沒給視為開、明確為 false 才不佔位**；設定有「為什麼看到廣告」；**重新啟動時 inflight 事件一律回 pending**（單調時鐘已重置，伺服器端去重使重送安全）；所有請求都帶 `X-Device-Install-Id`（上述各項 2026-10-05 與 iOS §11i 對齊）。事件佇列（SQLite `ad_event_queue`，常數取自 `cache-policy.json`）：30 秒／50 筆／進背景送出、單批 ≤200 帶 `batchId`、inflight 60 秒回 pending、2000 筆或 48 小時 FIFO 丟棄並記丟棄數；payload 無 member_id、IP、座標、廣告識別碼（測試守著） |
| 課程報名 | 課程列表／詳情／報名表／我的報名。**梯次與名額不快取**；梯次狀態（資料庫中文字面）決定「立即報名／額滿候補／已截止」；報名是**單次 POST、不自動重試**；已登入帶 Bearer（寫入 `member_id`），**權杖被拒（401）就改匿名送**，非會員照常可報名。**自動帶入**：會員姓名／手機／Email 帶進**家長聯絡欄位**，學員姓名留空（另有「我本人報名」鈕）；不建立家長與學員的帳號關聯。🔴 **不顯示、不送 `healthDeclaration`**（可能構成《個資法》§6 特種個資，B-9 未解；`ProgramRegistrationPolicy.collectsHealthDeclaration = false`，與 iOS 一致）。**只照後端驗證**：學員姓名必填、電話與 Email 至少一項；**家長欄位不強制**，不自行擴大蒐集範圍。繳費為線下作業、App 內不收課程費；不寫入行事曆。試訓（`trials`）共用機制但本輪未接 |

**Android 在 AP-3 第二輪與 AP-4 發現的缺口**：

| # | 缺口 |
|---|---|
| C1 | ✅ **2026-10-05 已補**（見 §11j）。原缺口：`MemberRegistrationDto` 只有 `sessionId`／`trialId`，沒有課程名稱、梯次時間、地點、繳費狀態；規劃書 §3.9「我的報名」要列課程、梯次、時間、地點、繳費狀態。客戶端現在只能逐一抓課程詳情反查（N＋1 次請求），建議後端直接帶出 |
| C2 | ✅ **2026-10-05 已補**（見 §11j）。原缺口：報名與梯次的**狀態是中文字面值**（開放／額滿／候補／已結束、待確認／已確認…），`enums.json` 沒收；英文介面只能在客戶端自備對照 |
| C3 | ✅ **2026-10-05 已補**（見 §11j）。原缺口：`GET /app/ads/{slotCode}` 的 `deviceInstallId` 在 `openapi.json` 仍是查詢參數；客戶端已改送標頭 `X-Device-Install-Id`，頻次上限是否改讀標頭請後端確認 |
| C4 | ✅ **2026-10-05 已補**（見 §11j）。原缺口：沒有「預先下載當日檔期素材」的契約（§2.4）；本輪未做預載 |
| C5 | ✅ **2026-10-05 已補**（見 §11j）。原缺口：廣告素材的圖片尺寸規則：`AppAdItemDto` 只有 `imageUrl`／`imageWidth`／`imageHeight`，沒說是否也有衍生檔（客戶端不經衍生檔規則，直接取 `imageUrl`） |
| C6 | 通知中心（§3.13）與推播偏好都依賴推播傳輸，未做；本機提醒沒有「靜音時段」 |

## 11h. 第四批契約補強（2026-10-05，`backend-engineer`；Android 缺口 B1–B4、iOS B8–B10、A8、時間語意）

| 項目 | 決定與契約 |
|---|---|
| **開賽時刻 `kickoffAt`** | `MatchDto.kickoffAt`（UTC，`Z`）＝`matchOn`＋`kickoff`（台北牆上時間，UTC+8）換算的**衍生欄位**，不是儲存值；`kickoff` 為空或格式不是 `H:mm`／`HH:mm` 時為 `null`（不猜 00:00）。`matchOn`／`kickoff` 保留給「當地牆上時間」呈現。規劃書已於 v3.14 改為對應說明（§11f 選項 1） |
| **俱樂部簡稱 `ClubDto.shortName`** | 資料：`clubs_i18n.short_name`（docs/12、12c 先改，DDL、migration、種子跟進）。磐石中文「台中磐石」英文「Taichung Rock FC」；藍鯨中文「台中藍鯨」；**藍鯨英文一律 NULL**（B-5，客戶尚未指定英文全名，開發端不自挑）。請求英文且該語系沒有簡稱時回退繁中簡稱（同其他欄位規則）。後台 J4 尚無編輯簡稱的欄位（要的話再加） |
| **監護人同意（B2）** | 規格：主站規劃書「會員資料安全要求」＋App §4.5。**`POST /member/auth/register` 與 `/auth/line/complete`**：`birthOn` **必填**（缺 → 400 `birth_on_required`）；依台北當地日期算足歲，**未滿 18 歲**須帶 `guardianConsent { consented: true, guardianName, relationship: "parent"｜"legal_guardian", consentTextVersion? }`，否則 400：`guardian_consent_required`／`guardian_name_required`／`invalid_guardian_relationship`／`invalid_guardian_consent_version`（英文訊息已登記）。成年帶了也**不儲存**。同意時間用**伺服器時間**。資料：`members.guardian_consented_at`／`guardian_name`（🔒 受限個資，docs/12b §8）／`guardian_relationship`／`guardian_consent_version`，`CK_members_guardian_consent`。刪帳號清除 `guardian_name`、保留同意時間／關係／版本（同意事實）。**同意文案本身待法務（B-9），API 只記版本號、不寫法律文字**。後台 K1 會員詳情 `GET /admin/{club}/members/{id}` 新增 `guardianConsent`（姓名沒解除遮罩時遮罩、需 `member.pii.reveal`）。⚠️ **後台「現場入會」`POST /admin/{club}/members` 沒有套用閘門**（由櫃台人員當面確認，規劃書未要求）。🔴 **破壞性**：官網與 App 的註冊表單**現在必須收生日**，沒帶一律 400 |
| **抽獎資訊（B1）** | `GET /api/v1/member/draws?lang=`（會員權杖、`private, no-store`、不在 ETag 白名單）。逐俱樂部列出已鎖定名單之後的活動（`roster_locked`／`drawn`／`announced`／`closed`，草稿與作廢不列，最多 30 筆新→舊），每筆：`id`、`drawCode`、`club{code,name}`、`name`／`prizeDescription`／`rules`／`notes`（雙語＋`isFallbackLocale`）、`coverUrl`、`occasion`／`occasionLabel`、`snapshotAt`（資格基準時間）、`drawnAt`、`claimDeadlineOn`、`status`／`statusLabel`、**`isEligible`（你在該主辦俱樂部的球迷會籍於基準時間是否有效，布林）**、`announcement{slug,categoryCode}`（已公布且新聞已發布才有，連往最新消息）。🔴 **嚴格界線（規劃書 §3.10）**：**不讀 `draw_rosters`**，資格由 `memberships` 即時推得（條件同後台名單產生）；DTO 沒有序號、名單、合格人數、中獎人、備取、領獎狀態、雜湊、內部備註（測試以回應全文掃描關鍵字）。「同時具備兩隊會籍者可分別參加兩隊抽獎」那句要寫進 `rules`（內容由後台維護，API 不補寫） |
| **會員卡伺服器時間（B3）** | `MemberCardDto.serverTime`（UTC，`Z`）＝伺服器產生該回應的時刻；會員卡端點永不快取，所以就是「這張卡有效狀態被確認的時刻」。App 用它記最後同步時間，不依賴 `Date` 標頭 |
| **搜尋球員 slug** | `SearchResultItemDto.slug` 對 `type=player` 也有值（`players.slug`），可直接 `GET /{club}/players/{slug}`；教練沒有 slug |
| **店家座標（B4／B10）** | `PartnerStorePublicDto.lat`／`lng`：**`null`＝座標未確認**（地圖不顯示、不計距離）；有值必成對、在範圍內、**不是 (0,0)**——DDL `CK_partner_stores_coords`（migration 先把既有不合規的列清成 NULL）＋後台驗證（0,0、半邊、超範圍皆 400；地址定位回 (0,0) 視為沒定位到）。契約：用戶端不必再防 (0,0) 哨兵值 |
| **未翻譯標示補齊（A8／B8）** | `isFallbackLocale` 新增於：夥伴、贊助商、贊助方案、課程（列表與詳情）、特約店家、（上一批已有）Club／Team／Player／Staff／Competition／Match／新聞／FAQ、（更早已有）站台設定／搜尋結果。**會籍方案與權益對照表（`MembershipPlanPublicDto`／權益）尚未加**，B8 的這一小部分仍開著 |

### 11e-3. AP-5 Phase D 程式部分與第四批契約（Android，2026-10-05）

| 項目 | 決定 |
|---|---|
| 新聞內文 | 照官網同一份格式（`apps/web/app/utils/news-body.ts`，後端契約見 `apps/api/README.md`「新聞內文」）：null 無區塊；非 `{`／`[` 開頭或解析失敗＝純文字（空行分段）；物件認 `blocks`／`content`、單一區塊或 `{text}`；陣列逐項。區塊 `p｜paragraph｜text`、`h1｜h2｜heading`、`h3｜h4｜subheading`、`quote｜blockquote`、`list｜ul｜ol`、`image｜img`、`gallery`。圖片網址只放行 http(s) 與站內相對路徑（補官網基底；擋 `javascript:`／`data:`／`//host`）。**官網遇到不認得的 `type` 是靜默忽略；App 改為顯示「這段內容請到官方網站閱讀」並附「在官網閱讀」按鈕**（連續的只留一個），內文為空時顯示摘要與「完整內容請到官方網站閱讀」，不留白。預覽資料 `news-detail.*.preview-all-blocks` 涵蓋每種區塊（含不支援與惡意網址） |
| 離線閱讀 | 開啟過的新聞內文即快取（`news_detail`，**保留 30 天**，取自 `cache-policy.json`），啟動時刪除超過 30 天的實體；列表顯示「已儲存，可離線閱讀」，設定顯示已儲存篇數。**容量上限契約沒定義 → 執行層決定：最多保留最近 200 篇，超過刪最舊**（`AppContainer.MAX_OFFLINE_ARTICLES`）。圖片磁碟快取 150MB、LRU 來自契約（Coil）；內文圖片是否離線可看取決於是否曾載入過（盡力而為，契約沒要求） |
| 球員肖像 | 🔴 **fail-closed：目前一律不顯示任何球員或教練照片**（`PortraitPolicy.CONSENT_SIGNAL_AVAILABLE = false`）。理由見缺口 D1。顯示元件與衍生檔挑選已就緒，契約補欄位後只改這一個判斷。列表本來就是文字卡、不放佔位圖；alt 只寫「球員照片」不含姓名 |
| 球員賽季數據 | 球員詳情顯示 `GET /{club}/players/{id}/stats`（出賽、進球、助攻、黃紅牌，助攻為空顯示「–」），沒有資料不顯示該段 |
| 第四批契約 | ✅ 註冊 `birthOn` 必填（原本就必填）；未滿 18 歲帶 `guardianConsent { consented, guardianName, relationship: parent｜legal_guardian }`，**`consentTextVersion` 不帶、說明文案放標明「待法務定稿」的佔位**（B-9，不自寫條文）；✅ `ClubDto.shortName`（賽程分頁、賽事卡、新聞來源標籤；沒有時退回全名）；✅ `GET /member/draws`（抽獎資訊，**唯讀、不快取、只顯示個人 `isEligible` 布林**，逐俱樂部、附「同時具備兩隊會籍者可分別參加」一句話、結果公布連往最新消息；無報名、序號、查詢、名單、人數計數器）；✅ `MemberCardDto.serverTime` 為最後同步時間（不再用 `Date` 標頭，測試證明）；✅ `MatchDto.kickoffAt`（UTC）優先於台北牆上時間換算，開賽提醒因此改用它；✅ `isFallbackLocale` 補到夥伴、贊助、課程、特約店家的「（Chinese only）」標示 |

**Android 在 AP-5 發現的缺口**：

| # | 缺口 |
|---|---|
| D1 | ✅ **2026-10-05 已補（部分）**：`portraitConsented` 布林，見 §11j；⚠️ 「同意涵蓋範圍（App／商店頁面）」無法表達，需客戶決定（§16.2 #14）。原缺口：**契約沒有可判斷肖像同意的欄位**：資料庫有 `portrait_consent_status`（`not_consented`／`consented`／`consented_by_guardian`），API 也在未同意時把 `photoKey`／`photoUrl` 設為 null，但這個行為只寫在後端原始碼註解，**不在 `openapi.json`／`shared/`**；DTO 另分不出「已同意」與「已取得監護人同意」，也沒有「是否未成年」旗標；同意書是否涵蓋 App 與商店頁面（規劃書 §12.2）更完全沒有欄位。建議 DTO 補 `portraitConsent`（含監護人）與適用範圍，或至少在契約寫明「`photoUrl` 非 null 即已取得涵蓋 App 的同意」 |
| D2 | ✅ **2026-10-05 已補**：`shared/news-body-blocks.json`（見 §11j）。原缺口：官網對不認得的區塊類型是靜默忽略，契約沒有列出支援的 `type` 清單與「未知類型」的約定；App 目前自備清單（同官網的 12 個別名） |
| D3 | 離線閱讀的容量上限、內文圖片是否離線保存，契約沒定義（見上，執行層決定） |

### 11i. iOS AP-4 Phase C 與第四批契約的執行層決定（2026-10-05）

> 與 Android（§11e-2、§11e-3）對齊後的 iOS 做法；**與 Android 不同或規劃書有明確規定處標 ⚠️**。

| 項目 | 決定 |
|---|---|
| 開賽提醒 | 本機排程通知（`UNUserNotificationCenter` 日曆觸發，**不需 APNs、不需 Push Notifications capability**）。規則同 Android：開賽前 2 小時、依追蹤球隊、延賽／取消／已完賽／無確切開賽時間不排、最近 60 天內最多 40 場（`ReminderPlanner`，純函式，測試涵蓋）、觸發時間已過不排。開賽時刻優先用 `MatchDto.kickoffAt`（UTC），舊資料才由 `matchOn`＋`kickoff`（台北）換算。賽程更新、追蹤改變、開關切換、語系切換、授權狀態改變都會重排（`ReminderCoordinator`，debounce 0.4 秒）；點通知走 `tcrfc://match/{id}`（`userInfo` 只放深連結，內容沒有個資）。引導新增**第 3 步**：先說明用途、使用者按了才請求系統權限（§14.3）；設定 → 通知可開關，權限被拒時顯示狀態與前往系統設定的捷徑、不反覆彈窗 |
| ⚠️ 提醒預設值 | **預設開**（規劃書 §6.2「賽事提醒」預設列為開），Android 暫定預設關。差別只在「使用者按了引導『稍後』或略過」時：iOS 的開關是開、但**沒有系統權限就不會排**，設定頁顯示「尚未詢問」並在使用者打開開關時才先說明再請求。請對齊另一端，或由你決定改規劃書 |
| 不做 | 靜音時段、分類開關、每隊獨立推播開關、通知中心——都屬推播（缺口 C6）。`PushTransport` 介面已隔開（`NoopPushTransport`），之後接 APNs 只需換實作 |
| 廣告版位 | `home_top`／`home_mid`／`schedule_list`／`news_list`（首頁兩處依 `/app/layout` 的 `ad_home_top`／`ad_home_mid` 位置；學院與課程畫面不放）；固定高度 112pt、永不空白（失敗時本機備援磚、不計曝光）、`ads_enabled` 關閉時不佔位（旗標沒給視為開）。標示「廣告」（取自回應 `disclosureLabel`）。外連先顯示離開提示；`tcrfc://` 直接走深連結；設定有「為什麼看到廣告」。**輪播**每 6 秒換一則（執行層決定），每則各自 `presentationId`；`sessionImpressionCap` 於本次使用內計數，達上限停止輪播與量測 |
| 曝光量測 | `ImpressionMeter`（純函式，逐案跑 `shared/ad-viewability-cases.json`：曝光 18＋點擊 4 全過）。UIKit `AdSlotUIView`：`CADisplayLink`、**5Hz、`.common` mode**；矩形＝`convert(bounds, to: nil)` ∩ 視窗 ∩ 每個 `clipsToBounds`／`UIScrollView` 祖先，再扣分頁列；有彈窗／全螢幕畫面蓋住時保守視為不可見；`inactive`（來電橫幅、控制中心）與背景立即歸零。前景判定用 `UIApplication.applicationState` |
| 事件佇列 | SQLite `ad_event_queue`，**常數取自 `cache-policy.json` 的 `adEventQueue`**（30 秒／50 筆／進背景、單批 ≤200 帶 `batchId`、inflight 60 秒回 pending、2000 筆或 48 小時 FIFO 丟棄並記丟棄數）。重新啟動時 inflight 一律回 pending（單調時鐘已重置；伺服器端以 `presentationId`／5 秒窗口去重，重送安全）。`occurredAt`＝**單調時鐘推算＋最近一次伺服器 `Date` 為錨**（`ServerClock.eventTime`，測試證明改裝置牆鐘無影響）。payload 沒有 `member_id`、IP、座標、廣告識別碼（測試掃 JSON）。丟棄計數目前只存本機，M5 診斷回報屬後續 |
| 裝置識別標頭 | 與 Android 一致：**所有請求**都帶 `X-Device-Install-Id`（後端對不需要的端點接受但忽略）；網址不放裝置識別 |
| 課程報名 | 列表／詳情／報名表／我的報名。**梯次與名額不快取**（離線只顯示「無法取得」）；梯次狀態（資料庫中文字面）→ 立即報名／額滿候補／已截止；報名是**單次 POST、不自動重試**；已登入帶 Bearer（寫入 `member_id`，權杖被拒就改匿名送），非會員照常可報名；**會員姓名／手機／Email 帶進家長聯絡欄位，學員姓名留空**，另有「我本人報名」；不建立家長與學員的帳號關聯；繳費為線下作業；不寫入行事曆。**試訓（`trials`）本輪不接 UI**（與 Android 一致，API 與預覽資料已備好）。深連結 `tcrfc://program/{slug}` 改為 App 內課程詳情（俱樂部不明時依序試）。「我的報名」因缺口 C1 逐一抓課程詳情反查（N＋1） |
| 🔴 健康聲明 | **App 不顯示、不送 `healthDeclaration`**（`ProgramRegistrationPolicy.collectsHealthDeclaration = false`）：可能構成《個資法》§6 特種個資，B-9 未解、文案待法務；後端欄位是選填，所以不影響送出。**請 Android 對齊**（若 Android 已顯示，請先關掉或說明） |
| 家長欄位 | 後端只驗「姓名必填、電話與 Email 至少一項」；**家長欄位不強制**（規劃書沒給年齡門檻，不自行擴大蒐集範圍）。與 Android 是否一致請確認 |
| 第四批契約（iOS） | ✅ 註冊 `birthOn` 必填；未滿 18 歲帶 `guardianConsent { consented, guardianName, relationship: parent｜legal_guardian }`，`consentTextVersion` 不帶，說明文案放標明「（同意說明文案待法務定稿）」的佔位；✅ `ClubDto.shortName`（無則退回全名，`ClubDisplay.name` 一處，E-185 收尾）；✅ `GET /member/draws`（唯讀、不快取、逐俱樂部、只顯示個人 `isEligible` 布林、附「同時具備兩隊會籍者可分別參加」、結果連往最新消息）；✅ `MemberCardDto.serverTime` 為最後同步時間（缺才退回 `Date` 標頭）；✅ `MatchDto.kickoffAt`；✅ `isFallbackLocale` 補到夥伴、贊助、課程、特約店家；店家座標 null＝未確認。✅ 未翻譯標示英文一律 **「Chinese only」**（兩端統一；中文介面不會出現） |

**iOS 在 AP-4 發現的缺口**：

| # | 缺口 |
|---|---|
| E1 | `enums.json` 已列 `statusCode`（梯次、報名）與 `statusLabelZh`／`statusLabelEn`，但 **`openapi.json`／DTO 還沒有這些欄位**（`ProgramSessionDto`、`MemberRegistrationDto`、`*RegistrationSubmittedDto` 只有中文字面 `status`）。iOS 目前以「中文字面為鍵的英文對照」顯示，等 DTO 補上後改依 `statusCode` |
| E2 | `AppAdItemDto` 的廣告圖是否有衍生檔未說明（同缺口 C5）；iOS 對 `.webp` 主檔依 `image-derivatives.json` 取 640／1280 衍生檔，非 `.webp` 原樣取用 |
| E3 | 廣告「預載當日檔期素材」沒有契約（C4）；沒有 App 端判斷「這個版位屬於兒童向畫面」的欄位，目前靠 App 不在課程／學院畫面放版位 |
| E4 | 會籍方案與權益對照表尚無 `isFallbackLocale`（B8 剩餘部分） |
| E5 | `MemberRegistrationDto` 沒有繳費狀態以外的欄位也沒有課程摘要（C1）；規劃書 §3.9 要列「課程、梯次、時間、地點」，目前靠反查 |

## 11j. 第五批契約補強（2026-10-05，`backend-engineer`；Android C1–C5、D1、D2）

| 項目 | 決定與契約 |
|---|---|
| **我的報名（C1）** | `GET /member/registrations?lang=`（新增 `lang`）：每筆新增 `kind`（`session`／`trial`）、`course`（課程報名：`programSlug`、`programName`、`startOn`／`endOn`、`weeklySchedule`、`venueName`／`venueAddress`、`price`／`earlyBirdPrice`／`earlyBirdUntil`、`sessionStatusCode`／`…LabelZh`／`…LabelEn`）、`trial`（試訓：`trialOn`、`teamName`、`venueName`／`venueAddress`、`trialStatusCode`…）、`isFallbackLocale`。**繳費狀態**：報名沒有獨立付款資料表（課程報名不走線上金流），繳費就是報名狀態 `statusCode` 的 `paid`／`completed`，應繳金額看 `course.price`——**沒有發明繳費欄位** |
| **狀態穩定代碼（C2）** | 不改資料、不刪既有欄位：既有 `status`（中文字面值）保留，新增 `statusCode`／`statusLabelZh`／`statusLabelEn`，加在 `MemberRegistrationDto`、`ProgramSessionDto`、`TrialDto`、`ProgramRegistrationSubmittedDto`、`TrialRegistrationSubmittedDto`。梯次與試訓場次：`open`／`full`／`waitlist`／`ended`；報名：`pending`／`confirmed`／`paid`／`completed`／`cancelled`／`waitlisted`。代碼表在 `apps/api/Common/EnrollmentStatus.cs`，**`shared/enums.json` 由它與 DDL CHECK 產生，兩者不一致產生器失敗**；未知字面值回 `unknown`（不丟例外） |
| **廣告裝置識別（C3）** | `GET /app/ads/{slotCode}`、`/app/layout`、`/app/notifications` 的 openapi 現在記載標頭參數 `X-Device-Install-Id`（查詢參數 `deviceInstallId` 相容保留，標頭優先）；每人頻次上限依解析出的識別計算（測試：同一台裝置曝光後以標頭請求即回備援，別台不受影響） |
| **當日預載（C4）** | 規劃書 §2.4「廣告素材：預先下載當日檔期素材，檔期結束即清除」→ **`GET /app/ads/prefetch?lang=&theme=`**：每個啟用版位列出**今天（台北日）有效**的檔期素材（投放中、或已排程且今天開始；已審核未暫停），每筆 `item`（`AppAdItemDto`）＋`startsAt`／`endsAt`（UTC，**超過 `endsAt` 就清除**）＋`weight`（離線時依權重加權隨機）；另有版位 `fallback`、`rotationCap`、`sessionImpressionCap`、`validUntil`（台北當日結束，之後重取）。🔴 只是預載目錄，**不套用**每人頻次／每日上限／pacing（那些在 `/ads/{slotCode}` 即時判斷）；離線曝光須帶原始發生時間，伺服器拒收超過 24 小時的（§2.4 硬規則 1）。公開、對所有裝置相同，`public, max-age=60` |
| **廣告圖片衍生檔（C5）** | 廣告素材與備援圖都走 §4.0 管線，**一定有** 320／640／1280：`AppAdItemDto.imageVariants { url320, url640, url1280 }` 直接帶完整網址（規則同 `shared/image-derivatives.json`，該檔另新增 `coverage` 列出保證有衍生檔的 DTO 欄位）。`imageUrl` 仍是主檔網址 |
| **方案與權益未翻譯標示** | `MembershipPlanPublicDto`、`BenefitItemPublicDto`、`BenefitTablePublicDto` 新增 `isFallbackLocale`（iOS B8 結案） |
| **後台俱樂部簡稱（J4）** | `GET／PUT /admin/clubs/{id}` 的 `content.zh`／`content.en` 新增 `shortName`（最多 32 字，空白＝無簡稱）；寫入後失效公開的俱樂部清單與單筆快取。藍鯨英文簡稱仍沒有資料，**開發端不填**，後台人員之後自填。⚠️ PUT 的 `content.en` 省略會刪掉英文內容列（含英文簡稱），這是既有行為 |
| **肖像同意（D1）** | `PlayerDto`／`StaffDto` 新增 `portraitConsented`（布林）：`consented`／`consented_by_guardian` 為 true，`not_consented`（預設）為 false；**不洩漏是否未成年**。契約：**`photoUrl`／`photoKey` 非 null ⇒ `portraitConsented` 為 true**；為 false 時照片欄位一律 null。🔴 **無法保證「涵蓋 App」**：現有三態沒有「涵蓋範圍」維度，App 規劃書 §12.2、§16.2 第 14 項「同意書是否涵蓋 App 與商店頁面」是客戶尚未回覆的待決事項，**沒有自行擴充語意**；`portraitConsented` 只代表官網層級的同意已取得。要讓 App 以此顯示照片，需客戶確認同意書涵蓋範圍後，再決定是否在資料模型加範圍欄位（先改規劃書與 docs/12） |
| **新聞內文區塊清單（D2）** | `shared/news-body-blocks.json`（產生檔，`gen-news-body-blocks.mjs` 解析 `apps/web/app/utils/news-body.ts` 的 `blockOf()`）：6 種區塊 `p`（`p`／`paragraph`／`text`）、`heading`（`h1`／`h2`／`heading`＝層級 2；`h3`／`h4`／`subheading`＝層級 3）、`quote`（`quote`／`blockquote`）、`list`（`list`／`ul`／`ol`）、`image`（`image`／`img`）、`gallery`；解讀順序、欄位、**未知型別約定（一律忽略，不渲染、不報錯）**、圖片網址安全規則、「一律當文字渲染、禁止當 HTML」。後端對 `bodyJson` 不解讀區塊型別（純文字存成 `{"text":"…"}`、物件陣列原樣保存），所以**真實來源是官網解析器**；官網改動後須重跑 `gen-all.sh`，CI 會因漂移失敗 |
| **順帶修的既有 bug** | 課程詳情對「有掛夥伴」或「梯次有日期」的課程回 500（`E-171`）：藍鯨社區足球學校一直壞著 |

### 11e-4. 第五批契約的採用（Android，2026-10-05）

| 項目 | 決定 |
|---|---|
| C1 我的報名 | `GET /member/registrations?lang=`：每筆帶 `kind`（course／trial）、`course`／`trial` 摘要與 `isFallbackLocale`，**拿掉逐筆反查課程詳情（N＋1）**。顯示課程名稱、起訖與每週時段、場地；試訓顯示日期、球隊、場地；只顯示、不寫入行事曆。**繳費狀態＝報名 `statusCode` 為 `paid` 或 `completed`**（已取消與未知代碼不顯示繳費行） |
| C2 狀態代碼 | 梯次與報名一律依 `statusCode` 判斷（梯次 open／full／waitlist／ended；報名 pending／confirmed／paid／completed／cancelled／waitlisted），顯示依語系取 `statusLabelZh`／`statusLabelEn`；**未知值（含 `unknown`）容錯**：梯次不給報名入口、報名只顯示後端標籤、不判斷繳費、不當機。原本客戶端自備的中文字面對照已移除 |
| C3／C5 廣告 | 標頭已記載（所有請求帶 `X-Device-Install-Id`）；`AppAdItemDto.imageVariants`（320／640／1280）依顯示寬度挑最小夠用的檔，沒有才用 `imageUrl` |
| C4 當日預載 | `GET /app/ads/prefetch?lang=&theme=`：啟動與回前景時、距上次超過 6 小時才抓（`ads_enabled` 沒被明確關閉才做）；整份存 SQLite（`ad_creatives`），**過了 `validUntil` 整份清除、素材 `endsAt` 之後不再被選用**，圖片交給 Coil 磁碟快取預先下載。**有網路時永遠先問即時端點**（頻次與每日上限以它為準）；連不上才用預載：檔期內素材**依 `weight` 加權、不重複抽出輪播順序，最多 `rotationCap` 則**，沒有素材用該版位 `fallback`（不量測）。🔴 **離線顯示的廣告（連不上即時端點而改用預載素材或 fallback）一律不量測、不產生曝光與點擊計數事件**——App 規劃書 §2.4 硬規則 1「離線時不得計算廣告曝光」、內容表「廣告素材 離線 ❌（離線不計曝光）」，**以規劃書為準**（2026-10-05 更正：原先曾依協調者指示做成離線可暫存，已改回）。**線上時已量到、但尚未送出就斷線的事件**照佇列暫存，連線後帶**原始發生時間**（單調時鐘推算＋伺服器時間錨）回傳，超過 24 小時由伺服器拒收（佇列不因被拒而重送）。預載仍保留（載入速度、離線可顯示素材），只是離線顯示不計。實作：`AdSlotSource`（回傳 `offline` 旗標）＋`AdEventRecorder`（離線一律不入佇列），測試涵蓋「離線路徑顯示廣告後佇列沒有新增事件」「線上量到的事件斷線後仍在佇列、補送時帶原始時間」 |
| B8 | 會籍方案與權益對照表 `isFallbackLocale` 已補「（Chinese only）」標示 |
| D1 | `portraitConsented` 只代表官網層級同意、**不保證涵蓋 App**（規劃書 §16.2 第 14 項待客戶）→ `PortraitPolicy.CONSENT_SIGNAL_AVAILABLE` 維持 false，**照片繼續一律不顯示**（測試守著：DTO 有 `photoUrl` 且 `portraitConsented=true` 仍不顯示） |
| D2 | 新聞區塊的 `type` 清單、別名與標題層級改讀 `shared/news-body-blocks.json`（`NewsBody.load`，與 `ImageUrls.load` 同做法；測試對契約每個 type 驗證都被辨識、型別不分大小寫）。🔸 契約對**未知 type 的約定是「忽略」**（官網行為）；App 在其上多加一層：未知 type 顯示「這段內容請到官方網站閱讀」並附按鈕（規劃書 Phase D「不支援的區塊引導到官網，不得空白」，協調者要求），這是 App 層的決定，不改變契約解讀 |

**第五批契約（2026-10-05，iOS 採用，與 Android 對齊）**

| 項目 | 決定 |
|---|---|
| 廣告預載（C4） | `GET /app/ads/prefetch` 目錄存本機（`cache_item` 的 `ad_prefetch`），**啟動與回前景時抓、距上次超過 6 小時才抓**；有網路一律先問即時端點；`endsAt` 後清掉該素材、**`validUntil` 過後整份清除**；圖片一併預先下載到磁碟快取。離線（連不上即時端點）改用預載：依 `weight` 加權隨機、不重複、最多 `rotationCap` 則；預載也沒有就顯示本機備援磚。頻次、每日上限、pacing 不在預載裡重算 |
| 🔴 離線廣告不量測 | **規劃書規定**（App 規劃書 §2.4 硬規則 1「離線時不得計算廣告曝光」與內容表「廣告素材：離線可用 ❌（離線不計曝光）」，不是執行層決定）：**離線顯示的預載素材與備援磚一律不量測、不產生曝光與點擊事件**；線上量到、尚未送出就斷線的事件照佇列暫存，連線後帶**原始發生時間**回傳（測試：離線路徑顯示後佇列無新增、線上事件斷線後仍在佇列且時間不變） |
| 廣告圖（C5） | 取 `imageVariants`：長邊 ≥ 顯示寬度（px，含倍率）的最小衍生檔，都不夠才用 1280，**不取主檔**；沒有 `imageVariants` 才依 `image-derivatives.json` 的命名規則推導 |
| 我的報名（C1／E5） | `GET /member/registrations?lang=` 直接帶 `kind`／`course`／`trial` 摘要與 `isFallbackLocale`；**逐筆反查課程詳情（N＋1）已移除**；繳費狀態＝報名 `statusCode` 的 `paid`／`completed`，費用看 `course.price`，沒有獨立付款欄位 |
| 狀態代碼（C2／E1） | 梯次、試訓、報名依 `statusCode` 分流（CTA、狀態色），文字取 `statusLabelZh`／`statusLabelEn`（缺英文退回中文）；**「中文字面為鍵」的對照已移除**；未知值（含 `unknown`）容錯：梯次保守視為已截止、狀態照顯示伺服器給的文字 |
| 肖像（D1） | `portraitConsented` 已接進判斷（`PortraitPolicy`），但 🔴 **照片維持一律不顯示**（`appCoverageConfirmed = false`）：它只代表官網層級同意，**不保證涵蓋 App**（§16.2 第 14 項待客戶回覆）。⚠️ 這比第三批前更嚴：以前只要有 `photoUrl` 就會顯示。球員與教練都適用 |
| 新聞內文（D2） | 依 `news-body-blocks.json` 原生渲染 6 種區塊（`p`／`heading`／`quote`／`list`／`image`／`gallery`，含別名與大小寫不分、標題層級 2／3、`ol` 恆有序）；解讀順序、`blocks`／`content`／單一區塊／字串區塊、圖片網址安全規則（只放行 `http(s)` 與單一 `/` 開頭的站內相對路徑，補官網基底；`javascript:`／`data:`／`//`／`/\` 整塊丟棄）、**一律當文字渲染（`Text(verbatim:)`，不解析 HTML）**。**與 Android 一致**：未知區塊型別顯示「這段內容請到官方網站閱讀」＋「在官網閱讀」按鈕（連續的只留一個），內文為空顯示摘要與「完整內容請到官方網站閱讀」，不留白。解析函式 `NewsBodyParser` 有契約驅動的測試（逐一驗證契約列出的每個 `type` 別名都不被當成未知） |
| 離線閱讀 | 開啟過的內文即快取（`news_detail`，**30 天**取自契約），啟動時刪除超過 30 天者；**最多保留最近 200 篇**（與 Android 一致的執行層決定）；列表標示「已儲存，可離線閱讀」，設定顯示已儲存篇數 |
| 方案與權益（B8） | `isFallbackLocale` 已用於英文介面的「Chinese only」標示 |
| 預覽資料 | 預覽用的假 API 會被多個版位並行呼叫，紀錄陣列以鎖保護（E-189） |
| 🔴 冒煙測試（XCUITest） | 為什麼：E-186（QR 畫成空白）、E-189（並行呼叫閃退）與 Android 的 E-163 是同一類——**單元測試全綠、打開畫面才壞**，依全域規定 13 升級成機制。`TcrfcAppUITests`（獨立 scheme `TcrfcAppSmoke`，一條指令 `./scripts/smoke.sh`）以預覽資料啟動真的 App：引導三步 → 首頁（四個廣告版位並行載入，E-189 的條件）→ 五個分頁主畫面 → 賽事詳情 → 新聞全文（全區塊預覽）→ 會員卡（**截圖並斷言 QR 區域有深淺像素，E-186**）→ 課程詳情、特約店家、名單、夥伴、FAQ、設定 → 深連結 match／player／news／未知，**含真正的冷啟動深連結**（先 terminate、再由系統開網址叫起，走 `scene(_:willConnectTo:options:)` 的 `urlContexts`，match／news／player／未知各一案）、**App 在背景時被連結帶回前景**、引導未完成時被連結叫起（先完成引導再導向）。Android E-190（冷啟動被深連結叫起閃退）促成這幾案；iOS 實測**沒有重現**該問題，但把冷啟動連結改為**擱到分頁與導覽容器上畫面（`viewDidAppear`）且引導完成後才導向**（`pendingLink`），不依賴 UIKit 對「尚未顯示的導覽容器 push」的容忍度。`connectionOptions.userActivities`（Universal Link）走同一個 `handle(url:)`，但 Associated Domains 尚未設、無法在冒煙測試覆蓋。深連結（App 已在執行）用啟動參數 `-TCRFC_DEBUG_URL`（Debug 才有）直接走 `handle(url:)`，避開 `simctl openurl` 的系統確認框；每次啟動 `-TCRFC_DEBUG_RESET YES` 清登入狀態（Keychain 會撐過重啟）。已用「把 QR 改回 `UIImage(ciImage:)`」實測：冒煙測試會失敗。另補單元測試：預覽假 API 的並行呼叫。**AP-7 的 CI 必須跑這組測試**（連同單元測試） |

**iOS AP-4 缺口狀態**：E1（狀態代碼）✅、E2（廣告圖衍生檔）✅、E3 預載 ✅（兒童向版位判斷欄位仍無，維持靠 App 不放版位）、E4（方案 `isFallbackLocale`）✅、E5（我的報名摘要）✅。**新缺口**：F1 `portraitConsented` 不含 App 涵蓋範圍（§16.2 第 14 項）；F2 `news-body-blocks.json` 的真實來源是官網解析器，後端不驗證區塊型別，App 遇到契約外型別只能提示到官網閱讀。

**Android 冒煙測試（E-163／E-190 的防呆機制，2026-10-05）**：`app/src/androidTest/.../SmokeTest.kt`（Compose UI Test＋`ActivityScenario`，`./gradlew connectedDevDebugAndroidTest`）以 dev flavor 預覽資料在模擬器走過引導三步、五個分頁、賽事詳情、新聞全文（全區塊）、會員卡（`captureToImage` 斷言 QR 非空白）、課程、店家、設定、抽獎資訊、我的報名，首頁廣告版位在並行載入情境，以及 Intent 深連結（match／player／news／store／program／schedule／無法辨識）。**AP-7 的 Android CI 必須跑這組**（Linux runner 用模擬器動作，如 `reactivecircus/android-emulator-runner`）；iOS 端對應的是同類 UI 冒煙測試（E-186／E-189）。第一次執行就抓到冷啟動深連結閃退（E-190）。

## 12. 本檔不決定的事

- **§16.2 技術前提第 17 項：推播是否走自家直送** —— 本檔建議直送（§5），但客戶可能有既有的服務商合約
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
