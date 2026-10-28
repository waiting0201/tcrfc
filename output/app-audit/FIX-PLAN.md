# App 欄位與圖片稽核的修正清單（2026-10-28）

> 來源：`ios-field-audit.md`、`android-field-audit.md`。兩個平台照這份做，**判斷一致**。
> 標記：〔兩端〕〔iOS〕〔Android〕。每一項動手前先對照 App 規劃書 `output/TCRFC_行動App功能規劃書.md` 對應章節（用 grep 找，不要整份讀）；**規格沒要求的不做**，稽核表裡「規格未要求、App 未用」的欄位一律不做。

## 不在本輪範圍（不要做）

| 項目 | 原因 |
|---|---|
| 方案頁顯示建議年費 `fee` | 2026-10-28 使用者決定：**維持不顯示、規劃書不更新**（見 docs/19 §10） |
| 🚫 契約缺口（球員經歷／前東家／加入日期／關聯新聞、`MatchDto` 無 venueId、新聞分類清單端點、`SponsorDto` 無官網、試訓 `isFallbackLocale`、球衣尺寸代碼表、店家社群連結） | 要先改規劃書／API，不是 App 的事 |
| 推播傳輸、推播分類開關、Android 訂閱同步到後端、`/app/push/{id}/opened` | 等 FCM／APNs（已知） |
| LINE 登入與綁定 | 外部設定未到位（已知） |
| 健康聲明 | B-9 未解（已知） |
| Android 店家地圖檢視 | 需 Google Maps 金鑰（已知） |
| 廣告 `title`／`ctaText`／`videoUrl`／`theme` | 規格未要求 |

## A. 賽事詳情（最重要，E-342 的同類錯誤）

1. 〔兩端〕單場端點 `GET /{club}/schedule/{id}` 依契約解碼為 **`MatchDetailDto`**，不得用 `MatchDto` 吞掉欄位。iOS：詳情頁**一律**呼叫單場端點，不只在清單找不到時。
2. 〔兩端〕已結束賽事顯示：比分、**進球者（含時間）**、**卡牌**、**出賽名單（先發／替補）**、**賽後報導連結 `matchReport`**。欄位清單對照規劃書 §3.2 與主站 3.13「賽果模式追加」。沒有資料的區塊不顯示，不留空白。
3. 〔兩端〕**對手隊徽 `opponentCrest`**：賽事卡與詳情都用；沒有隊徽時沿用現行無隊徽樣式。圖片取衍生檔。
4. 〔兩端〕主／客場要有可見標示（不只是左右順序）。
5. 〔兩端〕詳情頁與賽事卡依 `isFallbackLocale` 顯示語系回退標示。
6. 〔Android〕對手名稱優先 `opponentName`，退回 `opponent`。

## B. 首頁

1. 〔iOS〕「下一場賽事」只取**目前俱樂部的一線隊**，不得把學院梯隊算進去（`HomeView.swift:100`）。
2. 〔Android〕「下一場賽事」加倒數（照 iOS 現有做法）。
3. 〔iOS〕實作**快捷入口** `layout.quickEntries`。〔Android〕快捷入口不得濾掉沒有 `webUrl` 的項目：支援 `deepLink`（App 內頁）與 `icon`。
4. 〔Android〕公告用上 `linkUrl` 與 `endsAt`（過期不顯示），多則時依規格呈現。
5. 〔Android〕會員卡捷徑只在**持有目前俱樂部有效會籍**時顯示。
6. 〔Android〕已授權定位時，附近店家依距離排序。
7. 〔兩端〕贊助商與夥伴 Logo：依淺／深色模式取 `logoLightUrl`／`logoDarkUrl`，無障礙文字用 `logoAlt`。

## C. 賽程

1. 〔兩端〕次要篩選：**賽事類型、主客場**（iOS 另含月份，Android 已有月份吸頂就不重做）。資料用 `CompetitionDto.compType`／`MatchDto.competitionTag`，對照規劃書 §3.2。
2. 〔兩端〕賽事卡顯示賽事類型 `competitionTag`（若規格要求）。

## D. 加入行事曆

1. 〔iOS〕單場賽事「加入行事曆」（EventKit，只要寫入權限；或 `EKEventEditViewController` 免權限）。規格若另要求整季與我的報名，一併做；Android 已有的範圍為準，兩端一致。

## E. 新聞

1. 〔兩端〕分享網址：用**該篇所屬俱樂部的網域**（`ClubDto.domain`，Android 已有 `ClubWeb.baseUrl()`）＋ **目前語系**＋ API 的 `canonicalPath`。不得寫死 `/zh/`。
2. 〔兩端〕封面圖用 `coverFocalX`／`coverFocalY` 決定裁切焦點。
3. 〔iOS〕分類晶片不得只來自已載入頁面——若契約沒有分類清單端點，維持現狀並在 docs/19 註明（🚫）。
4. 〔Android〕內文頁的 `relations`、`tags`、`coreValueTags`：對照規劃書，有要求才做。

## F. 球隊名單、球員、教練

1. 〔兩端〕教練：顯示證照 `licence`、專長 `bio`、負責梯隊 `teamCodes`（轉成隊名）、`staffGroup` 分組；教練依目前選的球隊分頁變動。
2. 〔iOS〕教練卡補圖片版位，接 `PortraitPolicy.photoURL(StaffDto)`（照片仍照 fail-closed，現在不會顯示，但開關打開就要能出來）。
3. 〔兩端〕球員／教練照片的 alt **不得寫姓名**（規劃書 §12.2），用 API 的 `photoAlt`，沒有就只描述場景或留空讓讀屏讀卡片文字。
4. 〔兩端〕球員與教練清單要能取完全部（不得只取第一頁 100 筆）。
5. 〔Android〕`TeamDto.intro`／`heroUrl`／`heroAlt`：規格要求就顯示。
6. 〔Android〕生日格式化（依語系），慣用腳 `preferredFoot` 轉成本機化文字（iOS 已有對照表，照它）。
7. 〔兩端〕名單列、教練卡、球員詳情依 `isFallbackLocale` 顯示回退標示。
8. 〔Android〕賽季數據取不到時的處理與 iOS 一致（iOS：整段不顯示）。

## G. 夥伴與贊助

1. 〔兩端〕贊助商**依等級分區**、夥伴**依類型分區**；等級與類型的顯示文字要雙語。先查 `shared/enums.json` 是否有對照；沒有就是契約缺口，在 docs/19 註明並暫用 API 字面值。
2. 〔兩端〕夥伴合作期間 `startOn`／`endOn`；贊助故事連結 `stories[]`。
3. 〔iOS〕合作內容被 `lineLimit(4)` 截斷又沒有詳情頁：要能看全文。
4. 〔Android〕夥伴的 `country`、`charityPrograms`、贊助商 `activations`：規格要求才做。

## H. 課程與試訓

1. 〔兩端〕課程列表與詳情的**封面圖** `coverUrl`／`coverAlt`（衍生檔、缺圖不破圖、課程畫面不得有學員可辨識照片的問題由內容端控管，App 照常顯示 API 給的圖）。
2. 〔兩端〕課程詳情：類型與對象（轉本機化文字）、年齡、教練 `staff[]`、合作夥伴 `partners[]`（Logo）。
3. 〔兩端〕梯次：早鳥價與截止、報名開放／截止時間（App 不顯示金額的取捨**只適用會籍方案**；課程價格照規格顯示，若規格要求）。
4. 〔兩端〕梯次與試訓的場地座標 `venueLat`／`venueLng`：有座標就用座標導航。
5. 〔兩端〕課程詳情嵌入主題 FAQ：`GET /{club}/faqs/embeds/{slot}`（G-12）。
6. 〔Android〕隱私權政策改由 `GET /{club}/policies/{code}` 取得，不寫死路徑。

## I. 抽獎、球衣、會員

1. 〔兩端〕抽獎：開獎時間 `drawnAt`、資格基準時間 `snapshotAt`、封面圖 `coverUrl`。
2. 〔iOS〕抽獎與球衣登記依**目前俱樂部**過濾（v3.24，照會員中心的 `MemberScope`）。
3. 〔Android〕球衣列表顯示電話（遮罩與否照規格）。
4. 〔Android〕會籍列顯示 `planName`、`cardQuota`、`jerseyQuota`。
5. 〔Android〕會員卡 `validUntil` 格式化、用 `isValid`；QR 網址用**該卡所屬俱樂部的網域**。
6. 〔iOS〕帳號頁顯示生日 `birthOn`、語系偏好 `locale`（規格要求可改就做可改）。〔Android〕語系偏好同。

## J. 特約店家

1. 〔Android〕店家詳情與列表的**圖片** `imageUrl`（含寬高與 `imageAlt`）、`mapUrl`、回退標示。
2. 〔兩端〕篩選選項改用 `GET /{club}/partner-stores/filters`，不由已載入清單推得。
3. 〔iOS〕詳情圖片用 `imageAlt`，不得 `accessibilityHidden`；地圖標記依類別用不同圖示。

## K. 通知

1. 〔Android〕通知中心顯示 `imageUrl`。

## L. 設定（送審相關，優先）

1. 〔兩端〕設定頁加**隱私權政策、服務條款、聯絡我們**三個入口（規劃書 §3.14）。政策內容由 `GET /{club}/policies/{code}` 取得並在 App 內顯示；聯絡我們開官網聯絡頁。
2. 〔兩端〕設定頁集中說明定位權限用途並可前往系統設定。

## M. 版本閘門

1. 〔兩端〕`forceUpdateMessage`、`recommendUpdateMessage` 有值時顯示後台的訊息，沒有才用本機文案；`whatsNew` 依規格在更新後顯示一次。

## N. 圖片挑檔

1. 〔兩端〕依**實際像素寬**（版位寬 × 螢幕密度）挑 320／640／1280 衍生檔，不得把 320 當點數用。
2. 〔Android〕廣告衍生檔為 null 時退回 `imageUrl` 的可用衍生檔；仍無則不顯示。

## O. 「更多」選單

1. 〔兩端〕後台 M2 對內建項目的 `label`／`icon`／順序：規格（§8.2 M2）要求後台可控就照做；規格只讓後台控外部連結就維持現狀。

## P. 搜尋

1. 〔兩端〕全站搜尋（`GET /{club}/search`），結果**只列目前俱樂部**（規劃書 §3.0）。入口位置與結果分類照規格。
