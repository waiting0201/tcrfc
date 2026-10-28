# TCRFC 行動 App — 商店文案（App Store／Google Play，中英）

> **用途**：送審時直接貼進 App Store Connect 與 Google Play Console 的欄位。欄位位置與點選路徑見 [`docs/21`](../docs/21-store-submission.md) §3.3、§3.7、§8.4。
> **依據**：App 規劃書 §14.1（名稱、副標須明示雙隊共同 App 與開發者）、§14.3（只寫首版有的功能）、[`docs/21`](../docs/21-store-submission.md) §1.3。
> **版本**：2026-10-28，對應 App 首個上架版本 `1.0.0`。

---

## 0. 撰寫前提（改文案前先看）

1. **只寫首版真的有的功能**。以下功能首版**沒有**，文案一律不得提及，否則會以「說明與功能不符」（Apple 2.3、Google 中繼資料政策）退件：
   - **推播通知**（APNs／FCM 尚未串接）——首版只有「開賽提醒」，那是手機本機排程的提醒，不是推播
   - **LINE 登入**
   - **App 內付款**（首版不含，入會與續約在官網完成）
   - **搜尋**
   - **Android 版的店家地圖**（Android 只有清單；iOS 有地圖，所以共用文案只寫「依距離排序」）
2. **不寫價格**、不寫「在 App 內購買」或「在 App 內加入會員」。
3. **不寫慈善捐款**：捐款平台的主辦方是台灣足球策略發展協會，不是 App 的開發者。
4. **首版截圖與宣傳只用台中磐石的內容**（規劃書 §14.3）。說明裡寫雙隊共同 App 是事實陳述，不受影響。
5. 推播、LINE 登入等功能上線之後，要回頭更新本檔的「完整說明」與 [`TCRFC_App隱私標籤申報.md`](TCRFC_App隱私標籤申報.md)。

## 1. 共用欄位

| 欄位 | 用在 | 中文 | 英文 |
|---|---|---|---|
| App 名稱（≤30） | 兩邊 | 台中足球 | Taichung Football |
| 類別 | 兩邊 | Apple：Sports（主要）／Secondary 留空；Google：運動 | 同左 |
| 支援網址 | Apple | `https://tcrfc.tw/zh/faq/` | `https://tcrfc.tw/en/faq/` |
| 行銷網址 | Apple、Google 網站欄 | `https://tcrfc.tw/zh/app/` | `https://tcrfc.tw/en/app/` |
| 隱私權政策網址 | 兩邊 | 🔴 App 版網址待定（內容見 [`TCRFC_App隱私權政策.md`](TCRFC_App隱私權政策.md)） | 同左 |
| 版本 | 兩邊 | `1.0.0` | — |
| Copyright | Apple | `2026 〔俱樂部法人全名〕` | `2026 〔法人英文全名〕` |
| 聯絡 Email | Google（必填、公開顯示） | 〔俱樂部公開聯絡信箱〕 | 同左 |

> 〔　〕的欄位要等客戶提供，見 [`docs/21`](../docs/21-store-submission.md) §1.1 第 1、2 項。**法人全名要和開發者帳號上的寫法一字不差。**

---

## 2. 中文（繁體中文）

### 副標（Apple，≤30）

```
台中磐石 × 台中藍鯨 官方 App
```

### 簡短說明（Google，≤80）

```
台中磐石與台中藍鯨的官方 App：賽程賽果、電子會員卡、特約店家一次掌握。
```

### 宣傳文字（Apple，≤170，可隨時改、不用送審）

```
2026/27 賽季賽程已上線。追蹤台中磐石，比賽當天提醒你開球時間；電子會員卡離線也能出示。
```

### 關鍵字（Apple，≤100 字元，逗號分隔、不加空格）

```
磐石,藍鯨,企甲,女足,會員卡,賽程,賽果,TCRFC,青訓,足球學院,特約店家,球隊,球賽
```

> App 名稱裡已經有「台中」「足球」，Apple 會自動納入搜尋，關鍵字不必重複。

### 本版更新內容（兩邊）

```
首次推出。
```

### 完整說明（兩邊，≤4000）

```
「台中足球」是台中磐石足球俱樂部與台中藍鯨女子足球隊的共同官方 App，由台中磐石足球俱樂部開發。

第一次開啟時選擇你要追蹤的俱樂部，之後整個 App 只顯示這一隊的賽程、新聞與會員資訊；隨時可以在頁面上方切換。

【賽程與賽果】
・一次看未來 12 個月的賽程，可依賽事與球隊篩選
・賽事詳情：開球時間、場地與比分
・開賽提醒：比賽當天提醒你追蹤的球隊開球時間，可設定靜音時段

【會員】
・註冊會員、管理個人資料與登入裝置
・電子會員卡：每份會籍一張，以 QR Code 出示，沒有網路也能使用
・查看會籍方案與權益；入會與續約請至俱樂部官網辦理
・會員抽獎資訊、會員球衣登記

【特約店家】
・依距離排序附近的特約店家，查看店家資訊、一鍵撥號或導航
・定位只在手機上計算距離，不會上傳

【課程與試訓】
・瀏覽課程梯次與試訓資訊並線上報名
・在「我的報名」查看報名狀態

【新聞與球隊】
・俱樂部最新消息，看過的文章可離線閱讀
・球隊名單與球員賽季數據
・合作夥伴與贊助商、常見問題

【中英雙語】
介面依手機語系顯示繁體中文或英文。

隱私權政策：〔App 版隱私權政策網址〕
問題與回饋：https://tcrfc.tw/zh/faq/
```

---

## 3. English (U.S.)

### Subtitle (Apple, ≤30)

```
Taichung Rock FC & Blue Whale
```

### Short description (Google, ≤80)

```
Official app of Taichung Rock FC & Blue Whale: fixtures, member card, partners.
```

### Promotional text (Apple, ≤170)

```
The 2026/27 fixtures are live. Follow Taichung Rock FC and get a kick-off reminder on match day. Your digital member card works even offline.
```

### Keywords (Apple, ≤100 characters, comma-separated, no spaces)

```
soccer,Taiwan,Rock FC,Blue Whale,TCRFC,TFPL,women,fixtures,results,member card,academy,club
```

### What's New (both stores)

```
First release.
```

### Full description (both stores, ≤4000)

```
Taichung Football is the joint official app of Taichung Rock Football Club and Taichung Blue Whale Women's Football Club, developed by Taichung Rock Football Club.

Choose the club you follow when you first open the app. From then on, the whole app shows that club's fixtures, news and membership only, and you can switch clubs at any time from the top of the screen.

FIXTURES & RESULTS
• Fixtures for the next 12 months, filtered by competition and team
• Match details: kick-off time, venue and score
• Kick-off reminders on match day for the teams you follow, with optional quiet hours

MEMBERSHIP
• Sign up, manage your profile and signed-in devices
• Digital member card: one card per membership, shown as a QR code, available offline
• Membership plans and benefits; joining and renewing are done on the club website
• Member prize draw information and member jersey registration

PARTNER STORES
• Nearby partner stores sorted by distance, with store details, one-tap call and directions
• Your location is used only on your phone to calculate distance and is never uploaded

PROGRAMS & TRIALS
• Browse program sessions and trials and register online
• Track your registrations in "My registrations"

NEWS & TEAM
• Club news, with articles you have read available offline
• Squad lists and player season stats
• Partners and sponsors, FAQ

BILINGUAL
The app follows your phone's language and is available in Traditional Chinese and English.

Privacy policy: [App privacy policy URL]
Help & feedback: https://tcrfc.tw/en/faq/
```

---

## 4. 送審前自檢

- [ ] 〔　〕欄位都已填入客戶提供的內容
- [ ] 完整說明裡每一項功能，在**送審的那個建置**裡都點得到（特別是開賽提醒、特約店家、課程報名）
- [ ] 首版若已接上推播，補寫推播功能並同步更新隱私標籤；沒接就保持現狀
- [ ] 隱私權政策網址打開的是 **App 版**內容（規劃書 §12.4），不是官網版
- [ ] 截圖（`output/app-screenshots/`）裡出現的畫面，說明都有提到；說明提到的功能，不必每項都有截圖
