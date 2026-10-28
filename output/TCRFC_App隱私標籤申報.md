# TCRFC 行動 App — 隱私標籤申報（Apple App Privacy／Google 資料安全性）

> **用途**：送審時照表填寫 App Store Connect 的 **App Privacy** 與 Google Play Console 的**資料安全性**問卷。點選路徑見 [`docs/21`](../docs/21-store-submission.md) §3.4、§8.3。
> **依據**：App 規劃書 §12.1（個資判定）、§12.4（據實填報、可宣告不追蹤）；每一列都對照過**程式實際送出的欄位**（`shared/openapi.json` 與兩個 App 的送出端），依據寫在「程式依據」欄。
> **版本**：2026-10-28，對應首個上架版本 `1.0.0`（**不含推播傳輸、LINE 登入、App 內付款**；**含診斷上報**，2026-10-28 補上）。

---

## 0. 總原則

| 項目 | 結論 | 理由 |
|---|---|---|
| 是否蒐集資料 | **是** | 會員註冊、報名、球衣登記、裝置註冊、廣告事件都會送到伺服器 |
| **追蹤（Tracking）** | **一律「否」** | 不用廣告識別碼（IDFA／AAID）、不接聯播網、不與其他公司的資料合併、不做跨 App 追蹤（規劃書 §12.4、§7.8） |
| **定位** | **不申報** | 距離在手機上計算，座標**不上傳、不儲存**（§12.1）。兩家商店都只把「送出裝置」的資料算成蒐集 |
| 分享給第三方 | **否** | 雲端主機、寄信、推播（之後接上時）都是代為處理的服務供應商，依兩家商店的定義不算「分享」 |
| 傳輸加密 | **是** | 全程 HTTPS，不接受降級（§12.3） |
| 使用者可要求刪除 | **是** | App 內：iOS「會員 → 帳號 → 刪除帳號」、Android「會員 → 會員資料與帳號 → 刪除帳號」（§3.14）；Google 另需公開的刪除說明網址，見 [`docs/21`](../docs/21-store-submission.md) §1.2（🔴 頁面尚未做） |

---

## 1. Apple — App Privacy

**Privacy Policy URL**：App 版隱私權政策的網址（🔴 待定，內容見 [`TCRFC_App隱私權政策.md`](TCRFC_App隱私權政策.md)）
「Do you or your third-party partners collect data from this app?」→ **Yes**

每一項的「Used for tracking?」**全部選 No**。

| Apple 資料類型 | Linked to user | 用途（Purposes） | 程式依據 |
|---|---|---|---|
| Contact Info → **Name** | Yes | App Functionality | 註冊 `name`；課程／試訓報名 `applicantName`、`guardianName`；球衣登記 `recipientName` |
| Contact Info → **Email Address** | Yes | App Functionality | 註冊、課程／試訓報名的 `email` |
| Contact Info → **Phone Number** | Yes | App Functionality | 註冊 `phone`（選填）；報名 `phone`、`guardianPhone`；球衣登記 `phone` |
| Contact Info → **Physical Address** | Yes | App Functionality | 球衣登記選宅配時的 `address` |
| Identifiers → **User ID** | Yes | App Functionality | 會員帳號與會籍編號 |
| Identifiers → **Device ID** | Yes | App Functionality | `deviceInstallId`（App 自己產生，不是廣告識別碼），用於裝置註冊、登入裝置管理、廣告事件去重 |
| Purchases → **Purchase History** | Yes | App Functionality | 會籍狀態與期間、球衣登記、我的報名（付款在官網完成，App 只讀取紀錄） |
| Usage Data → **Advertising Data** | **Yes** | Analytics、Developer's Advertising or Marketing | 自售廣告的曝光與點擊事件（`/app/ads/events`），見下方說明 |
| Other Data → **Other Data Types** | Yes | App Functionality | 生日 `birthOn`（年齡閘門）、監護人同意資料、報名備註 `note` |
| Diagnostics → **Crash Data** | **Yes** | App Functionality、Analytics | `/app/diagnostics` 的 `crash`／`abnormal_exit`：例外類別與堆疊（不含例外訊息文字），帶 `deviceInstallId` |
| Diagnostics → **Performance Data** | **Yes** | App Functionality、Analytics | `startup_time`：冷啟動毫秒數 |
| Diagnostics → **Other Diagnostic Data** | **Yes** | App Functionality、Analytics | `api_error`：伺服器 5xx 與連線失敗的方法、路徑樣板、狀態碼、次數（不含網址參數與查詢字串） |

**不要勾的類型**（含理由）：

| 類型 | 理由 |
|---|---|
| Location（Precise／Coarse） | 只在手機上算距離，不上傳 |
| Health & Fitness／Sensitive Info | App **不送** `healthDeclaration`（兩端皆寫死為空，B-9 未解）。後端雖有此欄位，App 沒有蒐集 |
| Financial Info／Payment Info | 首版不含 App 內付款 |
| Contacts、Photos or Videos、Audio、User Content、Browsing History、Search History | 都沒有蒐集 |

> **為什麼廣告資料是「Linked = Yes」**：廣告事件本身不含 `member_id`（規劃書 §12.1），但會帶 `deviceInstallId`，後端也原樣寫進 `ad_events.device_install_id`（`apps/api/Features/AppPublic/AdEventIngestService.cs`）。同一個識別碼在 `app_devices` 裡對應到登入的會員，**技術上可以連回本人**，依 Apple 的定義就屬於 Linked。
> 要改成「No」，必須先在寫入 `ad_events` 前把識別碼換成無法回推的值（例如加鹽雜湊），並確認去重邏輯仍然成立——這是程式變更，改完才能改申報。**改之前不得填 No。**

填完右上 **Publish**。

---

## 2. Google Play — 資料安全性

### 2.1 總題

| 題目 | 答 |
|---|---|
| 應用程式是否會蒐集或分享任何必要的使用者資料類型？ | **是** |
| 是否所有使用者資料在傳輸時都經過加密？ | **是** |
| 是否提供使用者要求刪除資料的方式？ | **是**——填帳號刪除說明頁網址（🔴 尚未建立，見 [`docs/21`](../docs/21-store-submission.md) §1.2） |

### 2.2 資料類型

每一項的「是否分享」**全部選否**；「是否經過暫時處理」**全部選否**。

| Google 類別 → 類型 | 蒐集 | 必要／選填 | 用途 | 程式依據 |
|---|---|---|---|---|
| 個人資訊 → **姓名** | 是 | 必要 | 應用程式功能、帳戶管理 | 註冊、報名、球衣登記 |
| 個人資訊 → **電子郵件地址** | 是 | 必要 | 應用程式功能、帳戶管理 | 註冊、報名 |
| 個人資訊 → **使用者 ID** | 是 | 必要 | 應用程式功能、帳戶管理 | 會員帳號 |
| 個人資訊 → **電話號碼** | 是 | 選填 | 應用程式功能 | 註冊（選填）、報名、球衣登記 |
| 個人資訊 → **地址** | 是 | 選填 | 應用程式功能 | 球衣宅配 |
| 個人資訊 → **其他資訊** | 是 | 必要 | 應用程式功能、帳戶管理 | 生日（年齡閘門）、監護人同意、報名備註 |
| 財務資訊 → **購買記錄** | 是 | 選填 | 應用程式功能 | 會籍、球衣登記、我的報名 |
| 應用程式活動 → **其他使用者產生的動作**（廣告曝光與點擊） | 是 | 必要 | 數據分析、廣告或行銷 | `/app/ads/events` |
| 裝置或其他 ID | 是 | 必要 | 應用程式功能、數據分析 | `deviceInstallId` |
| 應用程式資訊與效能 → **當機記錄** | 是 | 必要 | 數據分析、應用程式功能 | `crash`／`abnormal_exit` |
| 應用程式資訊與效能 → **診斷資訊** | 是 | 必要 | 數據分析、應用程式功能 | `startup_time`、`api_error` |

> Google 的「應用程式互動」指的是使用者在 App 內的操作紀錄；廣告事件只記錄**廣告被看到或被點**，放在「其他使用者產生的動作」較貼切。若審查方認定應歸「應用程式互動」，改勾該項即可，用途不變。

**不勾的類別**：位置、健康與健身、相片和影片、音訊、檔案和文件、日曆、聯絡人、訊息、網路瀏覽記錄（理由同 Apple 一節）。

### 2.3 相關申報（應用程式內容頁）

| 項目 | 答 | 理由 |
|---|---|---|
| 廣告 | **是，含有廣告** | 自售廣告版位也算 |
| 廣告 ID | **否** | 送出前用 Merged Manifest 確認沒有 `com.google.android.gms.permission.AD_ID`（[`docs/21`](../docs/21-store-submission.md) §8.2） |

---

## 3. 何時要回頭改這份申報

以下任一件事發生，**送審那個版本之前**就要更新申報（Apple 改完要重新 Publish，Google 改完要重新送審）：

| 觸發 | 要加的申報 |
|---|---|
| 接上推播（APNs／FCM） | 推播權杖併入 Device ID（已申報，用途不變）；**會員條款須先增列告知**（規劃書 §12.1 第 1 項） |
| 報名開始送 `healthDeclaration` | Apple：Health & Fitness 或 Sensitive Info；Google：健康與健身。**前提是 B-9 解決、法務文案到位** |
| 加入 LINE 登入 | Identifiers → User ID 的用途加上帳戶管理；並確認是否須同時提供 Sign in with Apple（§12.4） |
| 加入 App 內付款（Phase E） | Purchase History 用途不變；付款資料由 LINE Pay 處理、App 不存卡號，仍不申報 Payment Info，但要重新核對 |
| 廣告事件改為不可回推的識別碼 | Apple Advertising Data 的 Linked 可改為 No |
