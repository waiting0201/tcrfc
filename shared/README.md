# shared/ — App 契約目錄

> 🔵 **執行層產物，不是規格。** 設計與理由見 [`docs/19-app-tech-stack.md`](../docs/19-app-tech-stack.md) §2；要新增欄位語意，先改 App 規劃書（同步鏈）。
> 目的：把「後端與兩個 App 對同一件事的理解」做成**同一份輸入產生兩端程式碼**的機制，不靠紀律（docs/19 §11 緩解 1）。
> App 客戶端 repo（`tcrfc-app-ios`／`tcrfc-app-android`，AP-7）還沒建立；本目錄是它們將來要取用的上游。

## 內容

| 檔案 | 性質 | 說明 |
|---|---|---|
| `openapi.json` | **產生** | `apps/api` 建置期產出的 OpenAPI 3.1，已排除後台、慈善平台、伺服器內部端點並正規化 |
| `generated/swift/TcrfcApiModels.swift` | **產生** | Swift `Codable` DTO（183 個型別） |
| `generated/kotlin/TcrfcApiModels.kt` | **產生** | Kotlin `@Serializable` DTO（同上） |
| `error-codes.json` | **產生** | 從 `apps/api` 原始碼掃出的機器可讀錯誤代碼（89 個），每個附 `messageEn`／`retryable`；錯誤信封（`code`／`messageZh`／`messageEn`／`retryable`，App 規劃書 §9.5）、依狀態的通用代碼 `statusDefaults`、重試規則與殘餘缺口 |
| `image-derivatives.json` | **產生** | 圖片衍生檔網址命名規則（1280／640／320／thumb）、挑選建議與範例，來源 `apps/api/Images`（缺口 A5） |
| `enums.json` | **產生** | 封閉值域（球隊 type／gender、賽事 status、梯次／試訓／報名的穩定狀態代碼與雙語標籤），來源 `db/club-schema.sql` 的 CHECK 與 `apps/api/Common/EnrollmentStatus.cs`（缺口 A10、C2） |
| `news-body-blocks.json` | **產生** | 新聞內文 `bodyJson` 的區塊型別與別名、未知型別約定（忽略）、圖片網址安全規則；來源 `apps/web/app/utils/news-body.ts`（缺口 D2） |
| `cache-schema.sql` | 手寫 | App 本機 SQLite 的共用 DDL，`-- migration: N` 區塊連號 |
| `cache-policy.json` | 手寫 | 規劃書 §2.4 時效表逐 key，另含會員卡、事件佇列、圖片、網路常數 |
| `deeplinks.json` | 手寫 | 規劃書 §2.3 的 8 條深連結、官網回退網址、`universalLinkPaths`、解析測資 |
| `ad-viewability.md` | 手寫 | 廣告可見度量測的雙平台共同行為規格 |
| `ad-viewability-cases.json` | 手寫 | 曝光判定與點擊去重的測資（兩端單元測試逐案比對） |
| `scripts/` | 工具 | 產生器與一致性檢查 |

**產生檔不要手改**——改來源（後端程式、或產生器）再重新產生。

## 指令

```bash
./shared/scripts/gen-all.sh          # 重新產生全部產生檔，最後跑一致性檢查（需要 .NET SDK 10、Node、Python 3）
./shared/scripts/gen-openapi.sh      # 只重新產生 openapi.json（約 15 秒，不連資料庫）
node shared/scripts/gen-dto.mjs      # 只由 openapi.json 重新產生 Swift／Kotlin DTO
node shared/scripts/gen-error-codes.mjs
python3 shared/scripts/check-shared.py   # 只做一致性檢查（不產生任何檔）
```

**後端改了 API（端點、請求／回應型別、會員一族的錯誤代碼）後，必須跑 `gen-all.sh` 並把 `shared/` 的變更一起提交。**
CI 的 `shared-contract` job（[`docs/20-cicd.md`](../docs/20-cicd.md) §3）會重跑並 `git diff --exit-code`，沒提交就紅燈。

## 一致性檢查（`scripts/check-shared.py`）守什麼

- `cache-schema.sql`：migration 連號、能套用在 SQLite、**不得出現 token／座標／`member_id`／廣告識別碼欄位**；
- `cache-policy.json`：與規劃書 §2.4 的 10 列一一對應；
- `deeplinks.json`：8 列、`universalLinkPaths` 與 `webPath` 一致、解析測資通過參考解析器；
- `ad-viewability-cases.json`：用檔內的參考實作逐案驗證預期事件；
- `openapi.json`：OpenAPI 3.1、不含後台／伺服器內部路徑、`$ref` 全部可解析。

## 待決事項

完整清單在 [`docs/19`](../docs/19-app-tech-stack.md) §2「目前 `shared/` 揭露的缺口」（App 端點白名單、官網路徑落差、英文路徑、藍鯨網域、未知深連結行為、會員卡 7 天提醒跨重啟、曝光起點）。


## 肖像同意的契約（缺口 D1）

`PlayerDto`／`StaffDto` 的 `portraitConsented`（布林）：**`photoUrl`／`photoKey` 非 null ⇒ `portraitConsented` 為 true**；為 false 時照片欄位一律 null，用戶端不得自行取得或顯示該人照片。
只代表「官網層級的肖像同意已取得」——**同意的涵蓋範圍（App、商店頁面）目前沒有資料欄位**，是 App 規劃書 §16.2 第 14 項客戶待決事項，不可由這個欄位推定已涵蓋 App。
布林刻意不區分「本人同意」與「監護人同意」，以免洩漏球員是否未成年。
