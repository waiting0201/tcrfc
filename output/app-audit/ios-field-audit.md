# iOS App 畫面欄位與圖片稽核表

- 稽核對象：`mobile/ios/TcrfcApp/` 正式資料路徑（`Features/`、`Core/Network/`、`Domain/`），略過 `DevFixtures/`。
- 對照依據：App 規劃書第 3 章與 §12.2、§13；`shared/openapi.json`（`mobile/ios/Shared/` 與 `shared/` 的 openapi.json 一致）；`shared/image-derivatives.json`；`docs/19` §11c、§11g、§11i、§11k、§11t。
- 稽核日：2026-10-28。**只讀、未改任何程式**。
- **重要限制**：沒有對真實 API 實機跑過。結論只代表「程式接線上」——某欄位是否由哪個端點的哪個 DTO 欄位餵給畫面；至於真實 API 回的值對不對、圖片衍生檔實際存不存在、版面是否破圖，**本稽核無法驗證**。也沒有跑單元測試（本稽核是靜態追查，測試綠燈不能證明「規格要的欄位都接了」）。
- 狀態圖例：✅ 已接　⚠️ 部分（含「已知」＝docs/19 已記載的決定或缺口）　❌ 未接（API 契約有、規格要、畫面沒用）　🚫 契約沒有（規格要、`openapi.json` 沒有）。「寫死」＝內容資料寫在程式或字串檔裡。介面標籤（按鈕字、標題）不列。

## 0. 全域機制

| 項目 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 雙語取值 | 一律以 `lang=zh｜en` 查詢參數請求，由 API 回已選語系的單一字串（`LocalizationManager.contentLang`）。App 不自行挑 `zh`／`en` 欄位 | ✅ | `Core/Network/ContentAPI.swift` 全部端點帶 `lang`；切語系時各畫面 `.task(id: loc.language)` 重取 |
| `isFallbackLocale` | 英文介面且為真時顯示 `FallbackTag` | ⚠️ | 已接：賽事詳情、新聞列表／內文、球員詳情、夥伴贊助、FAQ、會籍方案、權益表、店家、課程、我的報名、抽獎。**未接**：賽事列表卡（`MatchCard.swift` 無 FallbackTag）、球隊名單列（`PlayerRow`、`StaffDto` 卡）、球員列表、試訓場次（`PublicTrialDto` 本來就沒有此欄位，屬 🚫） |
| 圖片衍生檔 | `ImageURL.resolve`（`Core/Images/ImageLoader.swift:54`）：把 `*Url` 主檔網址 `.webp` 推導成 `-320`／`-640`／`-1280`，**從不取主檔**；非 `.webp`（SVG／PNG 標誌）原樣用；列表取 320，詳情依螢幕倍率取 640 或 1280（`cache-policy.json` 的 `images`） | ⚠️ | 規則本身符合 §13。保留：① 契約的建議規則是「取長邊 ≥ 顯示尺寸（含倍率）的最小檔」，App 列表一律 320，首頁新聞卡寬 260pt（@2x＝520px）與詳情全寬封面 @2x（約 780px）都會用較小的檔，畫質偏軟（策略選擇，非接線錯誤）；② 推導出的衍生檔若不存在（舊圖、非上傳管線產物但副檔名剛好是 `.webp`）沒有退回主檔的機制，會顯示空狀態（見各畫面缺圖列）；③ 廣告圖走 `imageVariants`，正確 |
| 缺圖空狀態 | `RemoteImage`（`ImageLoader.swift:96`）無網址或載入失敗顯示 `placeholder` | ✅ | 各畫面 placeholder：新聞＝灰底、夥伴＝名稱文字、球員＝透明（不放假圖）。**沒有任何地方會顯示破圖圖示** |
| 內建（寫死）資產 | 兩隊隊徽（`ClubTcrfc`／`ClubBw`）、品牌色 | ✅ | 規劃書 §10.1／v3.16 明定隊徽與品牌色為 App 內建，不由 API 提供。`ClubBadge`（`UI/Components.swift:74`）依 `ClubDto.code` 對應 |
| 圖片基底網址 | `AppConfigResponse.mediaBaseUrl` → `env.imageBase` | ✅ | 相對路徑接此基底 |

---

## 1. 啟動載入（S24）與維護／更新閘門

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 雙標誌 | 寫死（內建資產） | ✅ | `Features/Launch/LaunchView.swift:14-15`；規格要求雙標誌並列 |
| 進度條（設定、俱樂部、球隊、賽程、賽事系列） | `GET /app/config`、`/clubs`、`/{club}/teams`、`/schedule`、`/competitions` | ✅ | `Domain/StartupLogic.swift`、`Domain/AppData.swift:88-144` |
| 目前離線提示 | 傳輸層錯誤判定 | ✅ | 只在連不上網路時顯示，符合 §3.0 |
| 維護中／必須更新 訊息 | `/app/config` → `AppPlatformConfig.maintenance`、`forceUpdateMessage`（雙語物件） | ✅ | `Features/Gate/GateView.swift:30-40`，缺漏時退回本機文案（設計如此） |
| 建議更新 訊息 | `recommendUpdateMessage` | ✅ | `Core/Network/ConfigGate.swift:66` → `RootTabBarController.swift:84` |
| 「新功能」`whatsNew` | `AppPlatformConfig.whatsNew` | ❌ | 全專案無引用（grep 無結果）；§3.14 設定要有「版本資訊與更新檢查」，此欄位無處顯示 |

## 2. 首次啟動引導（S23）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 俱樂部二選一（名稱） | `GET /clubs` → `ClubDto.shortName`（無則 `name`） | ✅ | `Features/Club/ClubViews.swift:64`、`Domain/Catalog.swift:44` |
| 隊徽 | 寫死（內建資產，依 `ClubDto.code`） | ✅ | 規格 §10.1 允許。若 API 出現第三個俱樂部代碼，退回簡稱首字圓圈（不會破圖） |
| 推播說明文案 | 寫死 | ✅ | 介面文案（`onboarding.push.*`） |

## 3. 首頁（S01）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 區塊順序與開關 | `GET /app/layout` → `homeSections[].code` | ✅ | `HomeView.swift:16-19`；取不到用預設順序 |
| 公告條 | `layout.announcements[].message／linkUrl` | ✅ | `HomeView.swift:70-88`，`endsAt` 由伺服器篩 |
| 下一場賽事 | `GET /{club}/schedule` → `MatchDto` | ⚠️ | 規格 §3.1 區塊 1：「目前俱樂部**一線隊**的下一場」。`HomeView.swift:100` 傳 `follow: { _ in true }`，會把學院梯隊的賽事也納入，學院賽事較早時會顯示學院隊比賽 |
| 倒數天數 | 由 `matchOn` 本機計算 | ✅ | |
| 近期賽事（3 場） | `schedule` | ✅ | 一線隊優先、其餘補滿；卡片帶隊名（`MatchCard`） |
| 最新消息（3 則）封面 | `news[].coverUrl` | ⚠️ | 取 320 衍生檔（卡寬 260pt）；有 `coverAlt` 當 accessibilityLabel；無封面時不佔位。解析度偏軟，見 §0 |
| 最新消息 標題／分類／日期／俱樂部 | `title`、`categoryName`、`publishedAt` | ✅ | `NewsCard` |
| 會員卡快捷／加入會員 | 本機卡片＋登入狀態 | ✅ | 條件用「持有目前俱樂部的卡」而非「有效會籍」，卡片存在即有會籍，接近規格 |
| 廣告 `home_top`／`home_mid` | `GET /app/ads/{slotCode}` | ✅ | 見 §20 |
| 特約店家（附近 3） | `partner-stores` | ✅ | 見 §14 |
| **快捷入口（課程報名、商店）** | `layout.quickEntries[]` ／ 首頁區塊碼 `quick_entries` | ❌ | `HomeView.swift:65` `default: EmptyView()`，註解自承「尚未實作」；`quickEntries` 整個陣列全專案沒有讀取。後端固定回傳此區塊（`AppPublicDtos.cs:54`）。（「更多」分頁另有課程與商店入口，所以使用者仍到得了，但首頁規格區塊 8 缺） |
| 贊助商 Logo 牆 | `sponsors[]`、`partners[]`（`showOnHome`） | ⚠️ | 見 §9：只用 `logoLightUrl`；未用 `logoDarkUrl`、`logoAlt`；俱樂部標題用全名 `ClubDto.name`（非簡稱，不違規） |

## 4. 賽程與賽果（S02）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 球隊分頁（全部＋各隊） | `GET /{club}/teams` → `TeamDto.name／type／code` | ✅ | 動態產生、不硬編 `D1`／`BW1`（`Domain/ScheduleLogic.swift:58`、`Catalog.swift:30`）。球隊 `heroUrl`／`teamColor`／`intro` 未用（規格未要求） |
| 賽程／賽果切換 | `status`、`matchOn` | ✅ | |
| 賽事系列晶片（含尚無賽事者） | `GET /{club}/competitions` → `CompetitionDto.code／name` | ✅ | 空狀態有（`ScheduleView.swift:38`） |
| **次要篩選（賽事類型、主客場、月份）** | `MatchDto.competitionTag／homeAway／matchOn` | ❌ | 規格 §3.2「收在篩選面板」；`ScheduleView.swift:73-99` 的 `controls` 只有三層，沒有篩選面板 |
| 月份分組＋吸頂 | `matchOn` | ✅ | |
| 日期、星期 | `matchOn` 本機換算 | ✅ | 星期字串為本機對照表（zh／en），屬格式化 |
| 開賽時間 | `MatchDto.kickoffAt`（UTC）優先，退回 `matchOn`＋`kickoff`；依裝置時區顯示；無時間顯示「時間待定」 | ✅ | `Domain/ScheduleLogic.swift:11-15` |
| 主客場 | `MatchDto.homeAway` | ⚠️ | 只用來決定隊名左右順序（`MatchCard.swift:65-68`），沒有「主／客」文字或標記；規格卡片欄位含「主客場」 |
| 對手 | `MatchDto.opponent` | ✅ | `opponentName`（原文名）未用，無妨 |
| 對手隊徽 | `MatchDto.opponentCrest`（含 320／640／1280） | ❌ | 全專案無引用。契約有、畫面沒用（規格卡片欄位未列隊徽，列為「API 有、未用」） |
| 場地／場地待定 | `MatchDto.venue`（`TBC` 或空 → 灰字「場地待定」） | ✅ | `ScheduleLogic.swift:19-22` |
| 輪次 | `roundNo` | ✅ | `matchNo` 未用 |
| 狀態 | `status`（未知值→未開賽） | ✅ | |
| 賽事系列名稱 | `competitionName` | ✅ | `MatchCard.swift:35` |
| 所屬球隊名／俱樂部標誌 | `teamCode`→`TeamDto.name`；標誌內建 | ✅ | |
| 比分（官方主客順序） | `scoreHome／scoreAway` | ✅ | 客場時換邊 |
| 未翻譯標示 | `MatchDto.isFallbackLocale` | ⚠️ | 列表卡沒顯示，只有詳情有 |
| 廣告 `schedule_list` | `/app/ads/schedule_list` | ✅ | |

## 5. 賽事詳情（S03）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 比分、雙方、球隊、賽事系列、輪次 | `MatchDto`（取自整季清單） | ✅ | `ScheduleView.swift:165-186` |
| 日期、星期、開賽時間 | 同上 | ✅ | |
| 延賽原定時間 | `originalMatchOn／originalKickoff` | ✅ | 只在 `postponed` 顯示 |
| 場地 | `venue` | ✅ | |
| **進球者與時間** | `MatchDetailDto.goals[]`（含 `minute`、`playerName`、`side`、`goalType`）；清單亦帶 `scorers[]` | ❌ | `HomeView`…`MatchDetailView` 完全未引用 `scorers`／`goals`。另外 `HTTPContentAPI.match()` 宣告回傳 `MatchDto`，但 openapi 該端點回 `MatchDetailDto`（`ContentAPI.swift:21`、`ContentService.swift:53`），多出的 `goals`／`cards`／`lineup` 解碼時被丟棄；且 App 只有「清單裡沒有這場」才會呼叫單場端點（`ScheduleView.swift:155-163`） |
| **卡牌** | `MatchDetailDto.cards[]` | ❌ | 同上（回應被丟棄） |
| **出賽名單** | `MatchDetailDto.lineup[]` | ❌ | 同上 |
| **賽後報導連結** | `MatchDto.matchReport { slug, title }` | ❌ | 全專案無引用；應可導向新聞內文 |
| 對手隊徽 | `opponentCrest` | ❌ | 同 §4 |
| 場地導航 | `venue`（名稱）→ Apple 地圖「以名稱搜尋」 | ⚠️ | 規格 §3.2：以 `Venue` 座標導航、無座標才退地址搜尋。API 有 `GET /{club}/venues`（`PublicVenueDto.lat／lng／address`），但 `MatchDto` 只有場地名稱字串、沒有 venueId，App 也沒呼叫 venues。實際只做名稱搜尋（`ScheduleView.swift:223-231`）。契約面：賽事與場地缺關聯鍵（部分 🚫） |
| 加入系統行事曆（單場／整季） | — | ❌ | 無 EventKit 使用；`docs/19` §11c 登記為「未做」（**已知**）。非資料欄位 |
| 「賽事時間可能異動」聲明 | 寫死（介面文案） | ✅ | 規格明定須有 |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |

## 6. 新聞列表（S06）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 封面圖 | `GET /{club}/news` → `coverUrl`，取 320 | ✅ | `NewsViews.swift:14`；缺圖不佔位、不破圖 |
| 封面 alt | `coverAlt` | ✅ | 設為 accessibilityLabel，空字串時等同未標 |
| 封面焦點 | `coverFocalX／coverFocalY` | ⚠️ | 未用；`.fill`＋置中裁切，焦點在邊緣的圖會被裁掉主體 |
| 標題、分類、日期 | `title`、`categoryName`、`publishedAt` | ✅ | |
| 所屬俱樂部標示 | 請求路徑的俱樂部 | ✅ | |
| 俱樂部範圍 | 只請求目前俱樂部（伺服器已含共同新聞） | ✅ | `NewsModels.swift:40` |
| 分類晶片 | 從**已載入**的文章推得 `categoryCode／categoryName` | ⚠️ | `NewsModels.swift:42-48`：分類只出現在已載入頁面中的；尚未捲到的分類不會有晶片，且沒有分類清單端點可用（契約面無分類清單，部分 🚫）。八分類固定，影響有限 |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |
| 「已儲存可離線閱讀」 | 本機快取判斷 | ✅ | |
| 無限捲動、下拉更新 | `page`／`totalPages` | ✅ | |
| 廣告 `news_list` | `/app/ads/news_list` | ✅ | |
| 精選 `isFeatured`、`tags` | — | ⚠️ | 未用（規格未要求） |

## 7. 新聞內文（S07）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 標題、摘要 | `ArticleDetailDto.title／summary` | ✅ | |
| 內文 | `bodyJson` → `NewsBodyParser`（段落、標題、引言、清單、圖片、圖庫、影片；未知區塊給「到官網閱讀」） | ✅ | 契約 `shared/news-body-blocks.json`；內文為空時顯示摘要＋官網連結 |
| 封面圖 | `coverUrl`，詳情取 640／1280 | ✅ | `NewsViews.swift:199`；alt＝`coverAlt` |
| 內文圖片 | 區塊內網址，走同一套衍生檔推導；alt、caption 取自區塊 | ✅ | `NewsViews.swift:308-315` |
| 分類、日期、俱樂部 | `categoryName`、`publishedAt` | ✅ | |
| 字級調整 | 本機 | ✅ | |
| 系統分享（官網網址） | `env.webURL(club, "/zh/news/{slug}")` | ⚠️ | `NewsViews.swift:156` 路徑**寫死 `/zh/`**，英文介面也分享中文版網址；API 另提供 `canonicalPath` 未使用 |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |
| 關聯內容、標籤 | `relations[]`、`tags[]` | ⚠️ | 未用（規格未要求） |
| `ogImage*`、`seo*`、`viewCount` | — | ⚠️ | 未用（App 不需要） |

## 8. 球隊名單（S04）與球員詳情（S05）

### 8.1 球隊名單

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 球隊選擇 | `GET /{club}/teams` | ✅ | |
| 球員列表（背號、姓名） | `GET /{club}/players?team=` → `shirtNo／name` | ✅ | `RosterView.swift:150-153` |
| 位置分組、組內背號排序 | `position`（GK／DF／MF／FW，其他歸「其他」） | ✅ | 位置名稱為本機對照（enum 標籤） |
| 球員照片 | `photoUrl`＋`portraitConsented` | ⚠️ **已知** | `PortraitPolicy.appCoverageConfirmed = false`（`RosterView.swift:139`）→ **目前一律不顯示任何照片**。fail-closed 方向正確（§12.2、§16.2 第 14 項客戶未回覆）。判斷式 `appCoverageConfirmed && portraitConsented ? photoUrl : nil` 邏輯正確；缺圖時是純文字卡，無假圖 |
| 球員列表只取第一頁 | `page=1&pageSize=100` | ⚠️ | `ContentService.swift:107`；超過 100 人的球隊會被截斷（目前規模不會發生） |
| 教練／團隊 姓名、職稱 | `GET /{club}/staff` → `name／title` | ✅ | |
| **證照** | `StaffDto.licence` | ❌ | 未顯示（`RosterView.swift:120-129` 只有姓名、職稱） |
| **專長（簡介）** | `StaffDto.bio` | ❌ | 未顯示 |
| **負責梯隊** | `StaffDto.teamCodes[]` | ❌ | 未顯示；且 staff 請求 `team: nil`（`ContentService.swift:115`）→ 整個俱樂部的教練列在名單底部，不隨球隊分頁變動 |
| 教練分組 | `StaffDto.staffGroup` | ❌ | 未用 |
| 教練照片 | `photoUrl`＋`portraitConsented` | ⚠️ **已知** | 與球員同一個 fail-closed 開關；**另外**教練卡本身沒有圖片版位，`PortraitPolicy.photoURL(StaffDto)`（`RosterView.swift:142`）全專案無人呼叫——即使客戶日後確認同意，教練照片也不會顯示，需補畫面 |
| 未翻譯標示 | `isFallbackLocale` | ⚠️ | 名單列與教練卡未顯示（詳情有） |

### 8.2 球員詳情

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 背號、姓名、球隊名 | `PlayerDto.shirtNo／name`、`TeamDto.name` | ✅ | 規格「姓名（中英）」：API 依 `lang` 回單一語系字串，非兩者並陳（契約設計） |
| 位置、國籍、生日、身高、體重、慣用腳 | `position／nationality／birthOn／heightCm／weightKg／preferredFoot` | ✅ | `RosterView.swift:187-194`，空值不顯示 |
| 簡介 | `bio` | ✅ | |
| 賽季數據（出賽、進球、助攻、黃牌、紅牌） | `GET /{club}/players/{id}/stats` → `PlayerCareerStatDto` | ✅ | `RosterView.swift:221-262`；助攻 null 顯示「–」；`source` 未用 |
| 照片 | 同上 | ⚠️ **已知** | fail-closed 全關 |
| 照片 alt | `PlayerDto.photoAlt` 未用；`accessibilityLabel` 設為**球員姓名**（`RosterView.swift:177`） | ⚠️ | 規格 §12.2「alt 一律只描述場景，不寫個人姓名」。目前因照片全關而不生效；開關打開後會違規，且 API 已提供 `photoAlt` |
| **加入日期** | — | 🚫 | `PlayerDto` 無此欄位 |
| **前東家** | — | 🚫 | `PlayerDto` 無此欄位 |
| **經歷** | — | 🚫 | `PlayerDto` 無此欄位（只有 `bio`） |
| **關聯新聞** | — | 🚫 | 無「球員→新聞」端點；`ArticleDto.relations[]` 只能由文章端指向，反查要掃全部新聞 |

## 9. 夥伴與贊助商（S19）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 夥伴／贊助商名稱 | `GET /{club}/partners`、`/sponsors` → `name` | ✅ | |
| Logo | `logoLightUrl`（取 320；非 `.webp` 原樣） | ⚠️ | `PartnersView.swift:64-65,111,117`。**`logoDarkUrl` 從未使用**，深色模式也用淺色版 Logo；`logoAlt` 未用（accessibilityLabel＝名稱，違反「alt 描述場景」的反面不嚴重）；寬高欄位未用。缺 Logo 時退回名稱文字磚（不放假 Logo，符合 §3.11 紅牌） |
| 贊助商等級（主贊助／官方／支持） | `SponsorDto.tier`（資料庫中文字面值） | ⚠️ | 顯示為每筆旁的標籤，**沒有依等級分區**（規格「依等級分區」）；只依 `sortOrder` 排序。英文介面因 `loc.codeLabel("sponsor.tier", …)` 沒有對照鍵，回傳原中文字面，英文介面看到中文等級 |
| 夥伴類型（策略／國際／訓練／教育／品牌） | `PartnerDto.partnerType` | ⚠️ | 同上：未分區；同樣沒有英文對照 |
| 合作內容 | `content` | ⚠️ | `lineLimit(4)` 截斷（`PartnersView.swift:130`），沒有詳情頁，全文看不到 |
| 合作期間 | `PartnerDto.startOn／endOn` | ❌ | 未顯示（規格 §3.11 夥伴詳情含期間） |
| 官網連結（夥伴） | `PartnerDto.websiteUrl` | ✅ | |
| 官網連結（贊助商） | — | 🚫 | `SponsorDto` 無 `websiteUrl`（規格只要求贊助故事連結，故影響小） |
| **贊助故事連結** | `SponsorDto.stories[]`（`slug／title／summary`） | ❌ | 未顯示（規格 §3.11 Our Sponsors 含贊助故事連結） |
| 贊助活動 | `SponsorDto.activations[]`、`charityPrograms[]` | ⚠️ | 未用（規格未要求） |
| 合作洽詢導向官網 | `layout.sponsorshipInquiryWebUrl` | ✅ | `PartnersView.swift:92` |
| 「合作洽詢中」空格位 | 寫死（介面文案） | ✅ | |
| 只呈現目前俱樂部、不混列 | 請求路徑俱樂部 | ✅ | `PartnersView.swift:18` |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |

## 10. FAQ（S22）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 問題、答案 | `GET /{club}/faqs` → `question／answer` | ✅ | `FAQView.swift:91,96`；答案當純文字（若後台答案含格式，標記不會被渲染，本稽核無法確認實際資料） |
| 分類晶片 | `GET /faq-categories` → `name／slug`，對 `categorySlugs` 篩選 | ✅ | |
| 搜尋 | 本機文字比對 | ✅ | |
| 共用題目去重 | `isShared`／id | ✅ | |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |
| **課程頁／學院頁嵌入對應主題 FAQ（G-12）** | `GET /{club}/faqs/embeds/{slotCode}` | ❌ | 契約有端點；`ProgramsViews.swift` 全檔無 FAQ。規格 §3.0 G-12「沿用」 |

## 11. 會員中心（S08）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 姓名、會員編號 | `GET /member/me` → `name／memberNo`（離線退回卡片） | ✅ | |
| 俱樂部、層級、方案、球季、有效期限、狀態 | `GET /member/memberships` → `MyMembershipDto.club.name／tierLabel／planName／seasonCode／endOn／statusLabel` | ✅ | `MemberCenterView.swift:107-129`；只顯示目前俱樂部 |
| 起始日 | `startOn` | ⚠️ | 未顯示（規格欄位清單未要求） |
| 待處理訂單 | `pendingOrder`、`openOrders[]` | ✅ | |
| 續會提示（到期前 30 天） | `renewalDue`、`endOn` | ✅ | |
| 另一隊也有會籍提示＋切換 | `memberships[]` 其他俱樂部 | ✅ | |
| 沒有會籍的加入入口 | `joinableClubs[]` | ✅ | |
| 僅註冊帳號說明 | 寫死（介面文案） | ✅ | |
| 帳號資料：姓名、手機、Email | `MemberProfileDto.name／phone／email` | ✅ | `AccountView.swift:20-25` |
| **生日** | `MemberProfileDto.birthOn` | ❌ | 帳號頁未顯示（儲存時原樣回傳，`AccountView.swift:58`）；規格 §3.5「會員資料」含生日 |
| **語系偏好** | `MemberProfileDto.locale` | ❌ | 未顯示、未同步；App 語系只存本機（UserDefaults） |
| 登入方式（LINE 綁定狀態） | `lineBound／hasPassword` | ⚠️ | 只用 `hasPassword` 決定刪除流程；LINE 登入／綁定 UI 未做（**已知**，§11k 不做 LINE 登入） |

## 12. 電子會員卡（S09）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 所屬俱樂部標誌 | 內建資產（依 `MemberCardDto.club.code`） | ✅ | |
| 會員編號、姓名、層級、有效期限、狀態 | `GET /member/memberships` → `cards[]`：`memberNo／holderName／tierLabel／validUntil／statusLabel`，存本機 | ✅ | `MemberCardsView.swift:70-80` |
| QR Code | 本機組 `https://{ClubDto.domain}/m/{token}` | ✅ | token 在 Keychain；網域為空或 `.invalid` 時顯示「QR 無法使用」而非錯誤 QR |
| 最後同步時間 | `MemberCardDto.serverTime` | ✅ | |
| 7 天提醒、token 重產 | 本機規則；`POST /member/cards/{id}/regenerate` | ✅ | |
| 品牌色 | 內建（依俱樂部） | ✅ | |

## 13. 加入付費會籍：方案與權益（S10）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 方案名稱、層級 | `GET /{club}/membership/plans` → `name／tierLabel` | ✅ | |
| 球季期間 | `startsOn／endsOn` | ✅ | |
| `card_quota` 發卡數、`jersey_quota` 球衣件數 | `cardQuota／jerseyQuota` | ✅ | |
| 商店會員折扣比例 | `shopDiscountPercent` | ✅ | 0 則不顯示 |
| 說明、期中加入規則 | `benefitNote／midSeasonRule` | ✅ | |
| **建議年費** | `fee` | ⚠️ **已知** | 刻意不渲染（`MembershipPlansView.swift:25` 註解；`docs/19` §11g「首版付款：不顯示金額」）。與規格 §3.7「方案比較含建議年費」不一致，屬使用者已知的首版取捨（IAP 風險），待決定是否改規格或開放顯示 |
| 權益對照表（分組、各方案欄、值、說明） | `GET /{club}/membership/benefits` → `groups[].items[].values[]`、`plans[]` | ✅ | 純文字、欄位由 API 決定、不硬編權益（符合「不得硬編任何權益文字」）；螢幕閱讀器標籤已組好 |
| 涵蓋範圍說明（每俱樂部獨立計期、收款方為台中磐石足球俱樂部） | 寫死（`plans.scope.body`，`Localizable.strings:218`） | ✅ | 規格要求的聲明；**收款方名稱寫死在文案裡**（見寫死清單）。未登入可看 ✅ |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |

## 14. 特約店家：清單、詳情、地圖（S13／S14）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 名稱、類別、地區、優惠內容、適用層級標籤 | `GET /{club}/partner-stores` → `name／category／region／offerContent／applicableTierLabel` | ✅ | `StoresView.swift:98-121` |
| 距離排序、距離顯示 | 裝置座標＋`lat／lng`，裝置端計算 | ✅ | 座標不上傳 |
| 俱樂部範圍（含兩隊共同） | 目前俱樂部清單＋共同店家 | ✅ | `StoresLogic.swift:142` |
| 類別／地區篩選選項 | 從**已載入店家**推得 | ⚠️ | `StoresLogic.swift:84-85`；契約有 `GET /{club}/partner-stores/filters`，`ContentAPI.swift:99` 宣告了卻**全專案無呼叫**（回應整個丟棄）。目前行為：選項只含有店家的類別，結果等價，但端點白接 |
| 文字搜尋 | 本機比對名稱、地址、地區 | ✅ | |
| 地圖檢視 | `lat／lng`（MapKit） | ✅ | 無座標者只進清單 |
| 地圖類別圖示 | 規格「以類別圖示標示」 | ⚠️ | 全用同一種標記，標題＋副標（類別文字）（`StoresView.swift:158-165`） |
| 詳情照片／Logo | `imageUrl`（640／1280） | ✅ | 缺圖不顯示區塊 |
| 詳情照片 alt | `imageAlt` | ⚠️ | 未用，且 `accessibilityHidden(true)`（`StoresView.swift:200`） |
| 地址、營業時間 | `address／businessHours` | ✅ | |
| 電話（一鍵撥號） | `phone` | ✅ | |
| 一鍵導航 | `lat／lng`，無座標退回「店名＋地址」文字搜尋 | ✅ | |
| 地圖連結 | `mapUrl` | ❌ | 未用（導航自行處理，功能等價） |
| 官網／社群連結 | `websiteUrl` | ✅ | 社群連結 🚫（DTO 只有一個網址欄位） |
| 適用層級＋升級 CTA | `applicableTier`、本機會籍層級 | ✅ | 尊尚判斷用字面 `"premium"`（`StoresView.swift:195`）；契約 `enums` 值域＝`standard／premium`，吻合 |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |

## 15. 課程與營隊（S15／S16）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 課程名稱、簡介 | `GET /{club}/programs` → `name／intro` | ✅ | `ProgramsViews.swift:117-127` |
| 年齡範圍、有開放梯次 | `ageMin／ageMax／hasOpenSession` | ✅ | 年齡需兩者皆非空才顯示 |
| **課程封面圖** | `ProgramListItemDto.coverUrl／coverAlt`、`ProgramDetailDto.cover*` | ❌ | 列表與詳情都沒有封面（全專案 `coverUrl` 只有新聞用）。學院／課程畫面有未成年學員，需先過肖像審核，但畫面沒有版位 |
| 類型、對象 | `programType／audience` | ❌ | 未顯示 |
| 詳情內容 | `ProgramDetailDto.content` | ✅ | 純文字 |
| **教練** | `ProgramDetailDto.staff[]` | ❌ | 未顯示 |
| **合作夥伴** | `ProgramDetailDto.partners[]`（含 Logo 網址） | ❌ | 未顯示 |
| 梯次：起訖、每週時段、場地、名額、費用、狀態 | `sessions[]`：`startOn／endOn／weeklySchedule／venueName／venueAddress／capacity／enrolledCount／price／statusCode＋statusLabelZh｜En` | ✅ | 狀態文字取伺服器標籤，CTA 依 `statusCode` |
| 早鳥價與截止 | `earlyBirdPrice／earlyBirdUntil` | ❌ | 未顯示；只顯示 `price`，早鳥期間內會顯示原價 |
| 報名開放／截止時間 | `signupOpensAt／signupClosesAt` | ❌ | 未用 |
| 場地座標 | `venueLat／venueLng` | ❌ | 未用（規格未要求） |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |
| 線下繳費聲明 | 寫死（介面文案） | ✅ | |
| 學院／課程畫面不置廣告 | — | ✅ | 無廣告版位 |

## 16. 試訓場次與報名表

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 日期、球隊、對象、場地、名額、報名截止、狀態 | `GET /{club}/trials` → `PublicTrialDto` | ✅ | `TrialsViews.swift:72-94`；CTA 依 `statusCode`＋`isSignupOpen／acceptsWaitlist` |
| 場地座標 | `venueLat／venueLng` | ❌ | 未用（`docs/19` Q-8 已知） |
| 未翻譯標示 | — | 🚫 | `PublicTrialDto` 沒有 `isFallbackLocale` |
| 報名表：學員、生日、家長、聯絡、備註、隱私同意 | 表單送出 `Submit*RegistrationRequest` | ✅ | 會員資料自動帶入家長聯絡欄位 ✅ |
| 健康聲明 | `healthDeclaration` | ⚠️ **已知** | 刻意不蒐集（`ProgramRegistrationPolicy.collectsHealthDeclaration = false`，STATUS B-9） |

## 17. 我的報名

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 課程名稱／試訓＋球隊 | `GET /member/registrations` → `course.programName`／`trial.teamName` | ✅ | `ProgramsViews.swift:359-362` |
| 學員、報名編號 | `applicantName／registrationNo` | ✅ | |
| 梯次時間、每週時段、地點 | `course.startOn／endOn／weeklySchedule／venueName／venueAddress` | ✅ | |
| 費用 | `course.price` | ✅ | `earlyBirdPrice` 未用 |
| 繳費狀態 | `statusLabelZh／En`（`statusCode`：paid／completed…） | ✅ | 課程無獨立付款資料，繳費狀態＝報名狀態 |
| 試訓日期、場地 | `trial.trialOn／venueName／venueAddress` | ✅ | |
| 單筆匯出至裝置行事曆 | — | ❌ | 規格 §3.9 邊界 2「可提供」；EventKit 未做（**已知**，非必備） |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |

## 18. 抽獎資訊（S18）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 個人資格 | `GET /member/draws` → `isEligible` | ✅ | 無人數計數、無序號（符合界線） |
| 活動名稱、獎品、辦法、注意事項 | `name／prizeDescription／rules／notes` | ✅ | |
| 主辦俱樂部 | `club.name` | ✅ | |
| 開獎場合、資格基準時間、開獎時間、領獎期限、狀態 | `occasionLabel／snapshotAt／drawnAt／claimDeadlineOn／statusLabel` | ✅ | |
| 結果連往最新消息 | `announcement { slug, categoryCode }` | ✅ | |
| **只顯示目前俱樂部** | — | ⚠️ | 規格 §3.10（v3.0／v3.24）：顯示目前俱樂部的抽獎、切換俱樂部看另一場。`DrawsView.swift:30` 直接列出 API 回的全部抽獎，**沒套 `MemberScope`**（會員中心／卡片都有套）；與 v3.24「一次只看一個俱樂部」不一致。同樣情形：球衣登記（`JerseysView.swift:76`）列出全部會籍的分組 |
| 抽獎封面圖 | `coverUrl／coverWidth／coverHeight／coverAlt` | ❌ | 全專案未用 |
| 雙會籍聲明 | 寫死（`draws.both_clubs_note`） | ✅ | 規格要求的固定一句話 |
| 未翻譯標示 | `isFallbackLocale` | ✅ | |

## 19. 球衣登記

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 俱樂部、球季、已登記／配額、可否登記 | `GET /member/jerseys` → `clubName／seasonCode／used／quota／canRegister` | ✅ | |
| 領用人、尺寸、領取方式、地址、電話、狀態、寄出／領取日、可否修改 | `items[]` | ✅ | `JerseysView.swift:114-135` |
| 尺寸選項 | — | 🚫 | 契約沒有尺寸清單（`docs/19` 已記），改自由輸入 |
| 狀態代碼表 | — | 🚫 | `enums.json` 無球衣狀態值域（Q-7 已知），以 `status == "pending"` 判色 |
| 範圍 | 全部會籍 | ⚠️ | 見 §18，未依目前俱樂部過濾 |

## 20. 廣告版位（首頁、賽程、新聞）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 廣告圖 | `AppAdItemDto.imageVariants`（320／640／1280）→ 取最小夠用者；退回 `imageUrl` 經衍生檔推導 | ✅ | `ImageLoader.swift:43-50` |
| 「廣告」標示 | `AppAdResponse.disclosureLabel` | ✅ | 圖片失敗的本機備援磚不顯示標示（也不計曝光） |
| 替代文字 | `altText` 退回 `title` | ✅ | |
| 點擊目標 | `clickUrl`（外連先顯示離開提示、`tcrfc://` 走深連結） | ✅ | |
| `ctaText`、`title`、`videoUrl`、`theme` 參數 | — | ⚠️ | 未顯示（`title`／`ctaText` 不渲染；請求 `theme: nil`）；規格未要求 |
| 離線預載 | `GET /app/ads/prefetch` | ✅ | 只展示、不計曝光 |
| 「為什麼看到廣告」 | 寫死（`ads.why.body`） | ✅ | 規格要求的說明頁 |

## 21. 通知中心（S20）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 標題、內文、時間 | `GET /app/notifications` → `title／body／sentAt` | ✅ | 90 天保留、離線可讀 |
| 縮圖 | `imageUrl`（取 320） | ✅ | |
| 深連結 | `deepLink`（需 `DeepLinkParser` 可解析才可點） | ✅ | |
| 分類開關（賽事提醒／賽果／新聞／會籍與球衣）、隊別訂閱的推播 | — | ⚠️ **已知** | `docs/19` Q-5：無後端欄位、APNs 未做；隊別訂閱以 `PUT /app/devices/{id}/subscriptions` 送出但 `isPushEnabled` 一律 false |
| 靜音時段 | 本機設定 | ✅ | 後端無欄位（Q-1 已知），只約束本機開賽提醒 |

## 22. 設定與帳號（S21）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 語系、清除快取、離線新聞數量、版本資訊 | 本機 | ✅ | |
| 刪除帳號、改密碼、登出、登出全部裝置 | `DELETE /member/me`、`/member/auth/*` | ✅ | 在「帳號」頁而非設定頁；設定頁沒有直接入口（會員分頁 → 帳號），規格 §3.14 把它列在設定裡 |
| **隱私政策、服務條款** | `GET /{club}/policies/{code}`（`PublicPolicyDto.title／body／updatedAt`） | ❌ | 契約有端點；`SettingsView.swift` 無連結，全專案未呼叫 `policies`。`SettingsView.swift:3-5` 註解寫「文件不存在」，但 `docs/19` §11t 的 commit 已新增 App 版隱私權政策；報名表有連到**官網**隱私頁（`env.resolveWeb("/{lang}/privacy/")`），App 設定頁沒有 |
| **聯絡我們** | `GET /{club}/site-settings` 等 | ❌ | 設定頁無；而球衣頁寫「請聯繫客服」（`jerseys.locked`、`jerseys.form.footer`）卻沒有任何聯絡方式 |
| 定位權限說明 | 寫死／系統 | ⚠️ | 說明在特約店家頁（首次使用時），設定頁無集中說明（規格 §3.14 列「定位權限說明」） |
| 更新檢查／新功能 | `whatsNew` | ❌ | 見 §1 |
| 「為什麼看到廣告」 | 寫死 | ✅ | |

## 23. 更多分頁

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 項目與順序 | `layout.moreItems[].code／label／webUrl` | ✅ | `MoreView.swift:32-56`；不認得的代碼略過 |
| 商店、慈善外連 | `webUrl`（相對路徑接官網網域；慈善為絕對網址，開前顯示收受者為協會） | ✅ | 慈善只在磐石顯示（程式比對 `clubCode == "tcrfc"` 寫死，`MoreView.swift:17`） |
| 標籤文字 | 程式內 `routes` 對照（本機字串），只有商店、慈善用 API `label` | ⚠️ | `layout.moreItems[].label` 與 `icon` 對內建路由項目不採用（後台 M2 改標籤不會反映到 App） |
| `layout.deepLinks[]` | — | ⚠️ | 未用（解析器讀 `shared/deeplinks.json`），無影響 |

---

## 彙總

狀態數量（§1–§23 共 222 列，不含 §0 全域機制）：✅ 144、⚠️ 37、❌ 33、🚫 8。

### ❌ 未接（契約有、規格要、畫面沒用）

1. 首頁快捷入口：`HomeView.swift:65`（`quickEntries`／`quick_entries` 未實作）
2. 賽程次要篩選面板（賽事類型、主客場、月份）：`ScheduleView.swift:73-99`
3. 賽事詳情進球者／卡牌／出賽名單：`ScheduleView.swift:165-204`；`ContentAPI.swift:21`、`ContentService.swift:53`（`goals`／`cards`／`lineup`／`scorers` 被丟棄）
4. 賽後報導連結 `matchReport`：全專案無引用
5. 對手隊徽 `opponentCrest`：`MatchCard.swift`、`ScheduleView.swift` 無引用
6. 加入系統行事曆（單場／整季／我的報名單筆）：無 EventKit（已知）
7. 教練證照 `licence`、專長 `bio`、負責梯隊 `teamCodes`、`staffGroup`：`RosterView.swift:120-129`
8. 夥伴合作期間 `startOn／endOn`：`PartnersView.swift:123-137`
9. 贊助故事連結 `stories[]`：`PartnersView.swift:110-112`
10. 課程封面圖、類型／對象、教練 `staff[]`、合作夥伴 `partners[]`：`ProgramsViews.swift:117-127,183-213`
11. 課程早鳥價／截止、報名開放與截止時間：`ProgramsViews.swift:240`
12. 課程／試訓場地座標：未用（試訓為已知 Q-8）
13. 課程頁嵌入 FAQ（G-12）`/faqs/embeds/{slotCode}`：無呼叫
14. 抽獎封面圖 `coverUrl`：`DrawsView.swift`
15. 會員資料：生日 `birthOn`、語系偏好 `locale`：`AccountView.swift:19-25`
16. 設定頁：隱私政策／服務條款（`/policies/{code}`）、聯絡我們：`SettingsView.swift`
17. `AppPlatformConfig.whatsNew`：無引用
18. 店家 `mapUrl`：未用（功能等價，低優先）
19. `partner-stores/filters` 端點：宣告未呼叫（`ContentAPI.swift:99`）

### ⚠️ 部分／偏離

1. 首頁下一場賽事含學院梯隊，規格是一線隊：`HomeView.swift:100`
2. 賽事卡主客場無標示：`MatchCard.swift:65-68`
3. 賽事列表卡／名單列未顯示 `isFallbackLocale`
4. 賽事場地導航用名稱搜尋，未用 `venues` 座標：`ScheduleView.swift:223-231`
5. 新聞封面焦點 `coverFocalX/Y` 未用：`NewsViews.swift:14,199`
6. 新聞分類晶片只含已載入頁面的分類：`NewsModels.swift:42-48`
7. 新聞分享網址寫死 `/zh/`：`NewsViews.swift:156`
8. 球員／教練照片全關（已知，fail-closed）：`RosterView.swift:139`；教練卡無圖片版位，`PortraitPolicy.photoURL(StaffDto)` 無人呼叫
9. 球員照片 alt 設為姓名、未用 `photoAlt`：`RosterView.swift:177`
10. 球員列表／教練只取第一頁 100 筆：`ContentService.swift:107,115`；教練不隨球隊分頁：`RosterView.swift:120`
11. Logo 未用 `logoDarkUrl`／`logoAlt`：`PartnersView.swift:64-65,111,117`
12. 贊助等級／夥伴類型未分區，英文介面顯示中文字面：`PartnersView.swift:111,117`
13. 夥伴合作內容 `lineLimit(4)` 無詳情頁：`PartnersView.swift:130`
14. 建議年費 `fee` 不顯示（已知決定，與規格 §3.7 不一致）：`MembershipPlansView.swift:73-87`
15. 抽獎、球衣登記未依目前俱樂部過濾：`DrawsView.swift:30`、`JerseysView.swift:76`
16. 地圖標記無類別圖示：`StoresView.swift:158-165`
17. 店家詳情圖片 `imageAlt` 未用、`accessibilityHidden`：`StoresView.swift:200`
18. 列表圖片一律 320、首頁卡／詳情封面偏小檔（畫質偏軟）：`ImageLoader.swift:59-62`
19. 廣告 `title`／`ctaText`／`theme` 未用（規格未要求）
20. 推播分類開關與隊別推播（已知，Q-5／Q-1）
21. 定位權限說明未集中於設定
22. 健康聲明不蒐集（已知，B-9）
23. 更多分頁標籤取本機字串，後台 `label`／`icon` 不影響：`MoreView.swift:20-39`
24. 帳號頁未顯示 LINE 綁定狀態；LINE 登入未做（已知）

### 🚫 契約沒有（規格要、`openapi.json` 沒有）

1. 球員：加入日期、前東家、經歷、關聯新聞（`PlayerDto` 無，規格 §3.3 球員詳情）
2. 賽事↔場地關聯鍵（`MatchDto` 只有場地名稱字串，沒有 `venueId`，無法用 `venues` 座標導航）
3. 新聞分類清單端點（只能從文章推得）
4. 贊助商官網連結（`SponsorDto` 無 `websiteUrl`；規格只要求故事連結，影響低）
5. 試訓場次 `isFallbackLocale`（`PublicTrialDto` 無）
6. 球衣尺寸清單與球衣狀態代碼表（已知 Q-7）
7. 店家社群連結（僅一個 `websiteUrl`；規格寫「官網或社群連結」，影響低）

### 寫死的內容資料

- 俱樂部標誌與品牌色：內建資產與色票（規格 §10.1 允許）。
- `MoreView.swift:17` `showsCharity`：以字串 `"tcrfc"` 判斷哪一隊顯示慈善入口（俱樂部代碼寫死，非內容文字）。
- `Components.swift:69` `ClubAssets`：俱樂部代碼→圖檔名對照寫死。
- `StoresView.swift:195`／`MembershipPlansView.swift:77`：層級字面 `"premium"`。
- 位置名稱（守門員、後衛…）、慣用腳：`Localizable.strings:97-104` 本機對照（有英文版）。贊助等級、夥伴類型**沒有**對照（顯示 API 原字面）。
- 賽事狀態、梯次狀態標籤：本機對照；梯次／報名／試訓狀態文字取伺服器標籤（正確）。
- 法務／說明文案寫死在字串檔，需注意與規劃書同步：`plans.scope.body`（含收款方「台中磐石足球俱樂部」，:218）、`draws.both_clubs_note`（:370）、`ads.why.body`（:281）、`ads.fallback`＝「台中足球」（:277）、`member.registered_only.*`（:204）、`member.intro.*`（:82-83）、`account.delete.consequences`（:262）、`more.charity.notice.body`（:273，含協會名稱）、`programs.offline_payment_note`、`trials.offline_note`、`jerseys.form.footer`、`inbox.retention`（90 天）、`settings.offline_articles.footer`（30 天／200 篇）、隱私同意文案 `programs.form.privacy_consent`／`trials.form.privacy_consent`（文案待法務，Q-4 已知）。
- 星期縮寫、月份名稱：`MatchFormatting.swift:7-19`（格式化）。
- `RootTabBarController.swift:158` 的 `tcrfc-sample-3`、`demo-cafe`、`summer-camp`：位於 `#if DEBUG` 截圖輔助，不影響 Release。

## 規格以外、API 有回但 App 未用（不列入統計）

`TeamDto.heroUrl／teamColor／intro／ageBand`；`ClubDto.description／ogImage*`；`ArticleDto.tags／relations／isFeatured／viewCount／ogImage*`；`MatchDto.matchNo／competitionTag／opponentName`；`PlayerCareerStatDto.source`；`MemberCardDto.issuedAt／reissueCount`；`ProgramSessionDto.signupOpensAt／signupClosesAt`；`AppLayoutResponse.deepLinks`；全站搜尋 `/{club}/search`（G-02，App 只有 FAQ 與店家各自的本機搜尋，**全站搜尋未做**，docs/19 §11d 已記「未做」）；`POST /faqs/{slug}/feedback`、`views`；`/standings`、`/achievements`、`/milestones`（規格未要求）。

## 無法驗證的事

- 真實 API 回的資料（欄位是否為空、圖片衍生檔是否存在、`coverUrl` 是否皆為 `.webp`、`tier` 字面值）。
- 畫面實際渲染（版面截斷、深色模式、動態字級）。
- 單元測試／冒煙測試未執行；測試結果不改變上述接線判斷。
