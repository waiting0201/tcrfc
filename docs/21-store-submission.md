# 21 — App Store／Google Play 建立與送審操作手冊

> **文件版本**：v1.0　**建立日期**：2026-10-27
>
> **這份是給第一次做上架的人照著點的步驟。** 規格依據是 App 規劃書 §12（個資）、§14（上架與發行）、§16.2（待確認），
> 技術背景在 [`19`](19-app-tech-stack.md) §9–§10；**本檔不是規格**，兩者衝突以規劃書為準。
> 對應 `STATUS.md` 的 **AP-9**（帳號設定、`.well-known`、內測軌道）與 **AP-6**（首次上架送審）。
>
> ⚠️ Apple 與 Google 的後台常改版，**按鈕名稱以當期畫面為準**；本檔寫的是要找的東西與要填的值。
> 🔴 **本 repo 是公開的**：Team ID 之外的任何金鑰、密碼、keystore、`.p8`、測試帳號密碼**都不得寫進本檔或任何納管檔案**。

---

## 0. 整體順序（先看這張）

```
第 1 步  事前準備（文案、素材、網址、測試帳號）            ← 不用登入任何後台
第 2 步  Apple：建 App ID、APNs 金鑰                      ← developer.apple.com
第 3 步  Apple：在 App Store Connect 建立 App              ← appstoreconnect.apple.com
第 4 步  Apple：上傳第一個建置 → TestFlight 內測
第 5 步  Google：在 Play Console 建立 App
第 6 步  Google：做上傳金鑰 → 上傳 AAB → 內部測試
第 7 步  官網：放 .well-known 兩個關聯檔、帳號刪除說明頁   ← 需要第 4、6 步產生的值
第 8 步  兩邊填完商店資料與申報 → 送審
第 9 步  通過後：發布、把商店網址填回官網
```

**第 7 步之前，主站必須已經切到 `tcrfc.tw`**（目前 `tcrfc.tw` 還是 Wix 舊站，[`17`](17-deployment.md) §10.6）。
審查人員會實際點隱私權政策與支援網址，404 會被退件；`.well-known` 也只能放在正式網域，**不能先用暫用網址送審再換**（[`17`](17-deployment.md) §10.3 第①項）。

> 🔴 **待定案：主網址是 `tcrfc.tw` 還是 `www.tcrfc.tw`**（[`08`](08-roadmap-decisions.md) 第 162 行）。
> 寫進 App 的 Universal Link 網域事後要改就得重新送審。**建議 App 兩個都登記**（`applinks:tcrfc.tw`、`applinks:www.tcrfc.tw`），
> 兩個網域都直接提供關聯檔（**不可用轉址**，Apple 不跟隨轉址）。目前 Android manifest 只登記了 `tcrfc.tw`。

---

## 1. 事前準備（不用登入後台）

### 1.1 資料清單

| # | 要準備的 | 誰提供 | 用在 |
|---|---|---|---|
| 1 | 俱樂部**法人全名**（與開發者帳號上的寫法一字不差）、地址、電話 | 俱樂部 | Copyright、聯絡資訊、（若上歐盟）交易者申報 |
| 2 | **公開聯絡 Email**（會顯示在 Google Play 商店頁） | 俱樂部 | Play 商店設定 |
| 3 | **審查聯絡窗口**：姓名、手機、Email（審查人員會直接打來或寫信） | 俱樂部 | Apple 審查資訊 |
| 4 | **App 版隱私權政策**（規劃書 §12.4：須另寫，不得直接沿用官網版） | 法務 | 兩邊必填 |
| 5 | **審查用測試帳號**：建在**正式**資料庫、Email 已驗證、**同時持有磐石與藍鯨會籍**、有會員卡 | 開發方建 | 兩邊必填 |
| 6 | **藍鯨品牌使用授權書**（PDF） | 藍鯨 | Apple 審查附件、遇申訴時用 |
| 7 | 截圖使用的照片**肖像同意清單**（逐張） | 俱樂部 | 自我檢核（規劃書 §12.2） |

### 1.2 網址（全部要在送審前能打開）

| 用途 | 網址 | 狀態 |
|---|---|---|
| 隱私權政策 | `https://tcrfc.tw/zh/privacy/`（英文 `/en/privacy/`） | 頁面已有，**內容要換成 App 版** |
| 支援網址 | `https://tcrfc.tw/zh/faq/` | 已有 |
| 行銷網址 | `https://tcrfc.tw/zh/app/` | 已有 |
| **帳號刪除說明頁** | 待新增（建議放在 `/zh/app/` 頁內一節或 FAQ 一題） | 🔴 **要新做**——Google 要求公開網址；現在只有登入後的會員中心有刪除按鈕 |

### 1.3 文案（中英各一份）

| 欄位 | 字數上限 | 用在 | 草稿 |
|---|---|---|---|
| App 名稱 | 30 | 兩邊 | 台中足球／Taichung Football（規劃書 §14.1 已定案） |
| 副標 | 30 | Apple | 台中磐石 × 台中藍鯨 官方 App／Taichung Rock FC & Blue Whale |
| 簡短說明 | 80 | Google | 台中磐石與台中藍鯨官方 App：賽程、會員卡、特約店家一次掌握 |
| 宣傳文字 | 170 | Apple（可隨時改，不用送審） | 當季賽事或活動 |
| 完整說明 | 4000 | 兩邊 | 見下方寫法 |
| 關鍵字 | 100 字元（逗號分隔、不加空格） | Apple | `足球,台中,磐石,藍鯨,企甲,女足,會員卡,賽程,TCRFC,青訓,學院,特約店家` |
| 本版更新內容 | 4000 | 兩邊 | 首版寫「首次推出」即可 |

**完整說明的寫法**：
1. 第一段一定要寫明「台中磐石足球俱樂部與台中藍鯨女子足球隊的共同官方 App，由台中磐石足球俱樂部開發」（規劃書 §14.1）。
2. 接著列功能。**只寫首版真的有的功能**——推播若首版沒上，就不要寫「即時通知」，否則會被以「說明與功能不符」退件。
3. 不寫價格、不寫「在 App 內購買會籍」（首版不含 App 內付款，[`19`](19-app-tech-stack.md) §10）。

### 1.4 圖片素材規格

> **icon 與 Play 主題圖片已產出**（2026-10-27，規劃書 §14.1 雙標誌並列：白底，左磐石隊徽、右藍鯨隊徽）：`node output/tools/build-app-icons.mjs` → `output/app-icons/`，說明見 [`output/tools/README.md`](../output/tools/README.md)。iOS 與 Android 專案已放入（[`19`](19-app-tech-stack.md) §11m）。截圖仍要自己拍。

| 素材 | 規格 | 用在 | 注意 |
|---|---|---|---|
| App icon | **1024×1024** PNG，**不可有透明**、不要自己切圓角 | Apple（放進 Xcode 的 asset catalog） | 中性識別、雙標誌並列（§14.1） |
| App icon | **512×512** PNG（32 位元），≤ 1MB | Google 商店頁 | 同上 |
| 主題圖片 | **1024×500**，JPG 或 24 位元 PNG，**不可有透明** | Google（必填） | Apple 沒有這項 |
| iPhone 截圖 | **6.9 吋：1320×2868**（或 1290×2796），每語系 1–10 張 | Apple | 專案只支援 iPhone（`TARGETED_DEVICE_FAMILY = 1`），**不用做 iPad** |
| Android 手機截圖 | 2–8 張，9:16，建議 1080×1920 以上 | Google | 平板截圖可不做 |

**截圖規則**：中英各一套；含人像的照片要先過 1.1 第 7 項；會員卡畫面用測試帳號、遮罩姓名；
審查時若夥伴或球員照片仍為空，**空狀態畫面要完整、不能有破圖**（§14.3）；藍鯨內容未到位時截圖先只放磐石（§14.3）。

截圖做法：模擬器（iOS 選 iPhone 16 Pro Max 或更新的 6.9 吋機型；Android 選 Pixel 系列）跑 App，`⌘S`（iOS 模擬器）或模擬器側邊相機鈕存圖，尺寸就會對。

---

## 2. Apple：App ID 與 APNs 金鑰

網址：<https://developer.apple.com/account> → 用**俱樂部的 Apple 帳號**（Account Holder 或 Admin）登入。

### 2.1 記下 Team ID

左側 **Membership details** → 抄下 **Team ID**（10 碼英數）。Team ID 不算機密，後面 Xcode 設定與 `.well-known` 都要用。

### 2.2 建立正式版 App ID

1. **Certificates, IDs & Profiles** → 左側 **Identifiers** → 右上 **＋**
2. 選 **App IDs** → Continue → 選 **App** → Continue
3. 填：
   - **Description**：`Taichung Football`
   - **Bundle ID**：選 **Explicit**，填 `tw.tcrfc.app`
4. 下方 **Capabilities** 勾：
   - ☑ **Associated Domains**（Universal Link）
   - ☑ **Push Notifications**
   - **不要勾 Sign in with Apple**——首版沒有 LINE 登入，用不到（§16.2 第 15 項等加 LINE 登入時再處理）
5. Continue → **Register**

### 2.3 建立 APNs 金鑰（推播用）

1. 左側 **Keys** → **＋**
2. **Key Name**：`TCRFC APNs`
3. 勾 **Apple Push Notifications service (APNs)** → 旁邊 **Configure** → Environment 選 **Sandbox & Production** → Save
4. Continue → **Register**
5. 🔴 **按 Download 下載 `.p8`——只能下載這一次。** 存到密碼管理器或 NAS 的私密區，**不得放進任何 git repo**
6. 抄下 **Key ID**（10 碼）

這把金鑰交給後端做 APNs 直送（[`19`](19-app-tech-stack.md) §5），並在後台 M5 登錄輪替週期（規劃書 §12.3）。
**首版不上推播也建議現在先建好**，之後就不用再回頭跑這段。

> 簽章憑證與 Provisioning Profile **不用手動建**：用 Xcode 的自動簽章（第 4 步）或 Xcode Cloud 時會自動產生。

---

## 3. Apple：在 App Store Connect 建立 App

網址：<https://appstoreconnect.apple.com> → 同一個帳號登入。

### 3.1 先確認合約

右上帳號選單 → **Business**（舊版叫 Agreements, Tax, and Banking）→ **Free Apps** 合約是 Active 即可。
**免費 App 不用簽 Paid Apps 合約，也不用填稅務與銀行。**

### 3.2 新增 App

**Apps** → 左上 **＋** → **New App**：

| 欄位 | 填 |
|---|---|
| Platforms | ☑ iOS |
| Name | `台中足球` |
| Primary Language | Chinese (Traditional) |
| Bundle ID | 下拉選 `tw.tcrfc.app`（第 2.2 步建的；沒出現代表 2.2 沒完成） |
| SKU | `TCRFC-APP-IOS`（內部用、不公開、建了不能改） |
| User Access | Full Access |

→ **Create**。

### 3.3 App Information（左側 General → App Information）

| 欄位 | 填 |
|---|---|
| Localizable Information（右上語系切換） | 先填繁中，再按語系選單 **Add English (U.S.)** 填英文：Name `Taichung Football`、Subtitle 見 1.3 |
| Category | Primary：**Sports**；Secondary：可空或 News |
| Content Rights | 選 **Yes, it contains, shows, or accesses third-party content, and I have the necessary rights**（依據：藍鯨授權書） |
| Age Rating | 按 **Edit** 填問卷，見 3.5 |

### 3.4 App Privacy（左側 General → App Privacy）

1. **Privacy Policy URL**：`https://tcrfc.tw/zh/privacy/`（英文語系填 `/en/privacy/`）
2. Data Collection → **Get Started** → 「Do you or your third-party partners collect data from this app?」選 **Yes**
3. 依下表勾資料類型；每一項會再問三題：**用途**、**是否與使用者身分連結（Linked）**、**是否用於追蹤（Tracking）**——**Tracking 一律選 No**（§12.4）

| 資料類型 | Linked | 用途 |
|---|---|---|
| Contact Info → **Name**、**Email Address** | Yes | App Functionality |
| Contact Info → **Phone Number** | Yes | App Functionality（⚠️ 註冊表有收才勾） |
| Identifiers → **User ID**（會員編號） | Yes | App Functionality |
| Identifiers → **Device ID**（`device_install_id`、推播權杖） | Yes | App Functionality |
| Purchases → **Purchase History**（會籍、球衣、訂單紀錄） | Yes | App Functionality |
| Usage Data → **Advertising Data**（廣告曝光與點擊） | **No**（§12.1 不含 `member_id`） | Analytics、Developer's Advertising or Marketing |
| Diagnostics → **Crash Data**、**Performance Data** | ⚠️ 請工程確認上報是否帶會員 | App Functionality、Analytics |
| Other Data → **Other Data Types**（生日、監護人資料） | Yes | App Functionality |
| 課程報名的學員資料 | Yes | ⚠️ 依報名表實際欄位：含健康或緊急聯絡就另勾 Health／Sensitive Info |

**不要勾 Location**：定位只在手機上算距離、不上傳（§12.1），Apple 的定義是「送出手機才算蒐集」。

4. 全部填完 → 右上 **Publish**。

### 3.5 年齡分級問卷

- 暴力、成人、恐怖、藥物、醫療、賭博等：**全部 None／No**
- **Advertising**（若有此題）：Yes
- **User-generated content／messaging**：No（會員之間不能互動）
- **Unrestricted web access**：No（只外開特定官網頁面）
- **抽獎**（若有競賽或抽獎題）：照實勾，並在審查備註說明（見 3.8）
- **Made for Kids**：🔴 **待客戶確認**（§16.2 第 11 項）。建議選 **No**：選 Yes 會進兒童類別，對廣告、外部連結、資料蒐集的限制很嚴，而 App 有會籍、定位與廣告

### 3.6 Pricing and Availability（左側 Monetization）

| 欄位 | 填 |
|---|---|
| Price | **Free**（USD 0） |
| Availability | 🔴 **待客戶決定上架地區**。選了歐盟國家就要到 **Business → Digital Services Act** 申報交易者身分，俱樂部地址電話會公開顯示 |

### 3.7 版本頁（左側 iOS App → 1.0 Prepare for Submission）

| 欄位 | 填 |
|---|---|
| Previews and Screenshots | 選 **iPhone 6.9" Display** 拖入截圖（中、英語系各一次） |
| Promotional Text／Description／Keywords | 見 1.3 |
| Support URL | `https://tcrfc.tw/zh/faq/` |
| Marketing URL | `https://tcrfc.tw/zh/app/` |
| Version | `1.0.0` |
| Copyright | `2026 ` ＋ 法人全名 |
| Build | 第 4 步上傳後在這裡選 |
| App Review Information | 見 3.8 |
| Version Release | 建議選 **Manually release this version**（通過後自己按發布，比較好控時程） |

### 3.8 App Review Information（同一頁下方）

| 欄位 | 填 |
|---|---|
| Sign-In Information | ☑ Sign-in required，填 1.1 第 5 項的帳號密碼（**只填在後台，不寫進任何檔案**） |
| Contact Information | 1.1 第 3 項 |
| Notes | 用英文寫，五點（範本如下） |
| Attachment | 藍鯨品牌使用授權書 PDF |

```
1. Memberships provide real-world benefits (stadium member card, jerseys, partner store discounts).
   The app shows no prices and takes no payment; "Learn how to join" opens our website in the browser
   (Guideline 3.1.3(e), goods and services consumed outside the app).
2. The prize draw screen is read-only information for members. Entry is free. Apple is not a sponsor.
3. Location is used only on-device to sort nearby partner stores; it is never uploaded or stored.
4. Account deletion: Settings > Account > Delete account.
5. The app is jointly branded by Taichung Rock FC and Taichung Blue Whale; the brand licence is attached.
```

---

## 4. Apple：上傳第一個建置 → TestFlight

> 首次建議**在自己的 Mac 用 Xcode 手動上傳**，流程最短。Xcode Cloud 自動化屬 AP-7，等第一次手動跑通再做。

### 4.1 專案設定（工程，一次性）

`mobile/ios` 用 XcodeGen 產生專案，**設定要改在 `project.yml`**，不要只在 Xcode 裡改（下次重產會被蓋掉）：

1. `project.yml` 的 target `settings` 加 `DEVELOPMENT_TEAM: <Team ID>`、`CODE_SIGN_STYLE: Automatic`
2. 加 entitlements（Associated Domains `applinks:tcrfc.tw`〔與 `applinks:www.tcrfc.tw`，見 §0 待定案〕；推播上線時再加 `aps-environment`）
3. 🔴 **加 `PrivacyInfo.xcprivacy`**（隱私清單）：程式用到 `UserDefaults` 等須申報理由的 API，缺檔上傳後會收到 ITMS-91053 信並被退件
4. 確認 1024 App icon 已放進 asset catalog
5. 重產專案：`cd mobile/ios && xcodegen`

### 4.2 封存與上傳

1. Xcode 開 `mobile/ios/TcrfcApp.xcodeproj`
2. 上方裝置選單選 **Any iOS Device (arm64)**
3. 選單 **Product → Archive**（Scheme 要是 Release 設定）
4. 完成後自動開 **Organizer** → 選剛才的封存 → **Distribute App** → **App Store Connect** → **Upload** → 一路 Next（自動簽章）→ Upload
5. 等 10–30 分鐘，App Store Connect 會寄信通知處理完成。加密申報不會再問（`Info.plist` 已設 `ITSAppUsesNonExemptEncryption = false`）

### 4.3 TestFlight 內測

1. App Store Connect → 你的 App → 上方 **TestFlight**
2. 左側 **Internal Testing** → **＋** 建群組（例：`TCRFC 內部`）→ 加入團隊成員（須先在 **Users and Access** 加為使用者）
3. 把建置加進群組 → 成員手機安裝 **TestFlight** App 就收得到

**內部測試不用審查**；外部測試（給非團隊成員）要過一次簡易審查。

---

## 5. Google：在 Play Console 建立 App

網址：<https://play.google.com/console> → 用**俱樂部的組織帳號**登入。

1. 右上 **建立應用程式**（Create app）
2. 填：

| 欄位 | 填 |
|---|---|
| 應用程式名稱 | `台中足球` |
| 預設語言 | 中文（台灣）– zh-TW |
| 應用程式或遊戲 | 應用程式 |
| 免費或付費 | **免費**（🔴 選了免費之後**永遠不能改成付費**） |
| 聲明 | 勾「開發人員計畫政策」與「美國出口法規」 |

3. **建立應用程式**。進入後左側 **資訊主頁**（Dashboard）會列出「設定應用程式」的待辦清單，第 8.2 節逐項處理。

> 套件名稱 `tw.tcrfc.app` **不是在這裡填**，是第一次上傳 AAB 時從檔案讀出，**一經上傳永久綁定這個 App**。

---

## 6. Google：上傳金鑰 → AAB → 內部測試

### 6.1 產生上傳金鑰（一次性）

在自己電腦執行（會問密碼與名稱，名稱填俱樂部即可）：

```bash
keytool -genkeypair -v -keystore tcrfc-upload.jks \
  -keyalg RSA -keysize 2048 -validity 10000 -alias upload
```

🔴 `tcrfc-upload.jks` 與兩組密碼存密碼管理器＋NAS 私密區，**不得放進 git**（含 `mobile/android` 的私有 repo，[`19`](19-app-tech-stack.md) §9：密鑰一律走 CI secrets）。
弄丟可以向 Google 申請重設上傳金鑰，但要等審核。

### 6.2 專案設定（工程，一次性）

`mobile/android/app/build.gradle.kts` 目前**沒有 `signingConfig`**。要加一組 release 簽章，**從環境變數或 `local.properties`（已不納管）讀** keystore 路徑與密碼，不得把密碼寫進 `build.gradle.kts`。

### 6.3 打包

```bash
cd mobile/android && ./gradlew bundleRelease
# 產物：app/build/outputs/bundle/release/app-release.aab
```

`app/build/outputs/mapping/release/mapping.txt` 一併歸檔（[`19`](19-app-tech-stack.md) §9：`{平台}-{版本}-{建置號}`）。

### 6.4 上傳到內部測試

1. Play Console → 左側 **測試 → 內部測試** → **建立新版本**
2. 第一次會問 **Play 應用程式簽署**：選 **使用 Google 產生的金鑰**（建議，預設）
3. 把 `app-release.aab` 拖進去
4. 版本名稱會自動帶入；版本資訊填「首次內測」
5. **下一步 → 儲存並發布**
6. 同頁 **測試人員** 分頁 → 建立 Email 清單 → 複製「加入測試」連結給測試者

組織帳號**不受**「12 位測試人員、連續 14 天封閉測試」的規定（[`19`](19-app-tech-stack.md) §9）。

### 6.5 抄下簽署指紋（第 7 步要用）

左側 **測試與發布 → 設定 → 應用程式完整性**（App integrity）→ **應用程式簽署** 分頁：

- **應用程式簽署金鑰憑證**的 **SHA-256** → 給 `assetlinks.json`
- 同一區的 **SHA-1** → 給 Firebase（推播）與 Google Maps 金鑰限制

🔴 **要抄「應用程式簽署金鑰」那組，不是「上傳金鑰」那組**——使用者裝到的 App 是 Google 重新簽過的，抄錯 App Links 會驗證失敗、連結全部開到瀏覽器。

### 6.6 Firebase 與 Maps（做推播、店家地圖時才需要）

- **Firebase**：<https://console.firebase.google.com> 用俱樂部的 Google 帳號 → 新增專案 → 新增 Android 應用程式，套件 `tw.tcrfc.app`、填 6.5 的 SHA-1 → 下載 `google-services.json`（同樣不進公開 repo）
- **Google Maps**：Google Cloud Console → API 和服務 → 憑證 → 建 API 金鑰 → 限制為 Android 應用程式，填套件名稱＋SHA-1

---

## 7. 官網配合（需要第 2.1 與 6.5 的值）

| 檔案 | 位置 | 內容來源 |
|---|---|---|
| `apple-app-site-association` | `https://tcrfc.tw/.well-known/apple-app-site-association`（**無副檔名、`Content-Type: application/json`、不可轉址**） | 由 `shared/deeplinks.json` 的 `universalLinkPaths` 產生，appID 為 `<Team ID>.tw.tcrfc.app` |
| `assetlinks.json` | `https://tcrfc.tw/.well-known/assetlinks.json` | `package_name: tw.tcrfc.app`、`sha256_cert_fingerprints` 填 6.5 的 SHA-256 |
| 帳號刪除說明頁 | 見 1.2 | 寫 App 與官網兩條刪除步驟，以及保留範圍（規劃書 §4.6） |

`www.tcrfc.tw` 若也登記（§0 待定案），兩個檔案也要在 `www` 直接提供一份。

驗證：

```bash
curl -sI https://tcrfc.tw/.well-known/apple-app-site-association   # 要 200、不可 301/302
curl -s  https://tcrfc.tw/.well-known/assetlinks.json
```

Android 另可用 Play Console → **深層連結**（Deep links）頁面看驗證結果。

---

## 8. 填完商店資料與申報 → 送審

### 8.1 Apple

第 3 節全部填完、3.7 選好建置後 → 版本頁右上 **Add for Review** → **Submit to App Review**。
狀態會從 Waiting for Review → In Review → Pending Developer Release（選手動發布時）。通常 1–3 天。

### 8.2 Google：資訊主頁的申報清單

左側 **監控與改善 → 政策 → 應用程式內容**（或資訊主頁的待辦），逐項：

| 項目 | 填 |
|---|---|
| **隱私權政策** | `https://tcrfc.tw/zh/privacy/` |
| **應用程式存取權** | 選「應用程式的全部或部分功能受到限制」→ 新增指示：名稱「會員功能」、帳號密碼（1.1 第 5 項）、說明「登入後於『會員』分頁可看會員卡」 |
| **廣告** | **是，我的應用程式含有廣告**（自售廣告也算） |
| **內容分級** | 填 Email → 類別選 **所有其他應用程式類型** → 暴力、性、髒話、毒品：否；使用者互動或分享內容：否；分享使用者位置：否；購買數位商品：否；賭博：否 → 儲存 → 提交 |
| **目標對象與內容** | 🔴 **待客戶確認**（§16.2 第 11 項）。勾到 13 歲以下就適用「闔家適用」政策；建議只勾 **18 歲以上**（App 對象是球迷與家長，未成年由監護人同意，§4.5）。「是否會吸引兒童」選否 |
| **新聞應用程式** | 否 |
| **新冠肺炎相關** | 否 |
| **資料安全性** | 見 8.3 |
| **政府應用程式** | 否 |
| **金融功能** | 不提供任何金融功能 |
| **健康** | 不提供健康功能 |
| **廣告 ID** | **否**。上傳前用 Android Studio 開 `AndroidManifest.xml` → 下方 **Merged Manifest** 分頁確認沒有 `com.google.android.gms.permission.AD_ID`（Firebase Analytics 會自動加，FCM 不會） |

### 8.3 Google：資料安全性問卷

| 題目 | 答 |
|---|---|
| 是否蒐集或分享必要的使用者資料類型 | 是 |
| 傳輸中的資料是否都經過加密 | 是 |
| 使用者可否要求刪除資料 | 是 → 填帳號刪除說明頁網址（1.2） |
| 個人資訊 | ☑ 姓名、電子郵件地址、使用者 ID、（有收才勾）電話號碼、其他資訊（生日、監護人） → 已蒐集、未分享、必要、用途「應用程式功能」「帳戶管理」 |
| 財務資訊 | ☑ 購買記錄 → 應用程式功能 |
| 應用程式活動 | ☑ 應用程式互動（廣告曝光與點擊）→ 用途「數據分析」「廣告或行銷」 |
| 應用程式資訊與效能 | ☑ 當機記錄、診斷資訊 → 數據分析 |
| 裝置或其他 ID | ☑ → 應用程式功能 |
| 位置 | **不勾**（只在手機上計算，不上傳） |

推播服務（FCM、APNs）是**服務供應商**，依 Google 定義**不算「分享」**。

### 8.4 Google：商店資訊與設定

- **拓展使用者 → 商店資訊 → 主要商店資訊**：名稱、簡短說明、完整說明、icon 512、主題圖片 1024×500、手機截圖；右上 **管理翻譯 → 新增 English (United States)** 再填一份英文
- **商店設定**：應用程式類別 **運動**；聯絡資訊填 Email（必填）、網站 `https://tcrfc.tw/zh/app/`
- **正式版 → 國家／地區**：🔴 待客戶決定（同 3.6；上歐盟要到 **設定 → 開發人員帳戶 → 帳戶詳細資料** 申報交易者）

### 8.5 Google：送審

1. **正式版** → **建立新版本** → **從資料庫新增**選內部測試那個 AAB（或直接升級內部測試的版本）
2. 下一步 → **儲存** → 左側 **發布總覽** → **傳送 N 項變更以供審查**
3. 建議先在 **發布總覽** 開啟 **代管發布**（Managed publishing）：審查通過後由你按發布，比較好控時程
4. **新 App 的首次審查可能要 7 天以上**，預留時間

---

## 9. 通過後

1. Apple：版本頁 **Release This Version**；Google：發布總覽 **發布**
2. 抄下商店網址：
   - Apple：App Store Connect → App Information → **Apple ID**（數字）→ `https://apps.apple.com/tw/app/id<Apple ID>`
   - Google：`https://play.google.com/store/apps/details?id=tw.tcrfc.app`
3. 填進官網 `.env` 的 `NUXT_PUBLIC_APP_STORE_URL`、`NUXT_PUBLIC_PLAY_STORE_URL` → 重新部署，`/app/` 頁就會從「即將上線」變成下載按鈕
4. `STATUS.md` 的 AP-6、AP-9 打勾

---

## 10. 常見退件與處理

| 退件理由（審查準則） | 常見原因 | 處理 |
|---|---|---|
| **2.1** App 完整性 | 審查時 API 掛掉、測試帳號登不進、畫面空白或破圖 | 送審前用測試帳號在正式環境走一遍 |
| **2.3** 中繼資料 | 說明寫了沒有的功能、截圖不是 App 實際畫面 | 依 1.3 只寫首版功能 |
| **3.1.1** 應使用 IAP | 審查人員認為會籍是數位內容 | 回覆 3.8 第 1 點、強調實體權益；最壞情況見 [`19`](19-app-tech-stack.md) §10 的 fallback |
| **5.1.1** 資料蒐集與刪除 | 找不到刪帳號、隱私標籤與實際不符 | 確認 App 內路徑；隱私標籤與 3.4 表對照 |
| **5.3** 抽獎 | 沒寫明 Apple 非贊助者 | App 抽獎畫面加註並在備註說明 |
| Google 政策違規 | 資料安全性與實際不符、廣告未申報 | 改申報後重新送審 |

**回覆方式**：Apple 在 App Store Connect 該版本的 **App Review** 訊息串回覆（不用重傳建置，除非要改程式）；Google 在 **政策狀態** 頁看原因，修正後重新送審。

---

## 11. 尚待客戶決定（規劃書未寫，會影響商店上看得到的東西）

| # | 事項 | 影響的步驟 | 建議 |
|---|---|---|---|
| 1 | **目標年齡層**（§16.2 第 11 項） | 3.5、8.2 | 不選兒童類別 |
| 2 | **上架地區** | 3.6、8.4 | 含歐盟就要公開地址電話 |
| 3 | **主網址 `tcrfc.tw` 或 `www.tcrfc.tw`**（[`08`](08-roadmap-decisions.md) 第 162 行） | 4.1、7 | App 兩個都登記 |
| 4 | **截圖是否使用真人照片**（§16.2 第 13、14 項） | 1.4 | 同意確認前只用無人像畫面 |
