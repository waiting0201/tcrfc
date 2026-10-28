# Android App 欄位與圖片稽核（正式資料路徑）

- 稽核日期：2026-10-28　對象：`mobile/android/app/src/main/java/tw/tcrfc/app/`（`main` 來源集；`dev` 預覽資料不在範圍）
- 方法：從 Composable → ViewModel／Repository → Retrofit（`core/Net.kt`、`member/MemberRemote.kt`）→ DTO（`shared/generated/kotlin/TcrfcApiModels.kt`），並對照 `shared/openapi.json`、`shared/image-derivatives.json`、App 規劃書第 3 章。
- **侷限**：只做程式接線判斷，**沒有對真實 API 實機跑過**，也沒有實際渲染畫面；「✅ 已接」＝程式確實從該 DTO 欄位取值顯示，不代表後端真的有回、值正確。
- 狀態：✅ 已接／⚠️ 部分（接了但有缺陷或只用一部分）／❌ 未接（API 有、規格要、畫面沒用）／🚫 契約沒有（規格要、API 契約無此欄位）。「已知」＝`docs/19` 已記載的缺口。
- 路徑縮寫：`ui/` 皆在 `tw/tcrfc/app/` 之下。

## 通用事實（先看）

| 項目 | 結論 | 位置 |
|---|---|---|
| 圖片衍生檔 | `RemoteImage` 一律經 `ImageUrls.variant()` 依 `shared/image-derivatives.json` 改成 `-320/-640/-1280`，不取主檔（除非三檔都不夠大才回主檔）。⚠️ 傳入的 `width` 被當成**像素**用，首頁新聞卡（240dp）、列表縮圖（88dp）、Logo 一律寫 320，高密度螢幕會偏糊；詳情頁有乘螢幕密度（正確） | `ui/components/Common.kt:192`、`domain/Domain.kt:303` |
| 缺圖空狀態 | `url` 空白時 `RemoteImage` 直接不繪製（不破圖、不放佔位圖） | `Common.kt:193` |
| 肖像同意 | **永遠不顯示任何球員／教練照片**：`PortraitPolicy.CONSENT_SIGNAL_AVAILABLE = false`（已知，docs/19 D1／§16.2 第 14 項）。契約的 `portraitConsented` 全 App 沒人讀 | `domain/Portrait.kt:18-21` |
| 雙語 | 所有列表／詳情端點都帶 `lang`；`isFallbackLocale` 只在部分畫面用 `FallbackNote` 標示（見各節） | `core/Net.kt` |
| 隊徽／俱樂部標誌 | **打包在 App 內的 drawable**（`club_logo_tcrfc`／`club_logo_bw`），不是 API 取得；`ClubDto` 只有 `ogImageUrl`，沒有標誌欄位 | `ui/theme/Theme.kt:160-163` |
| 俱樂部品牌色 | 寫死在 `ClubPalette`（`forClub` 只認 `"bw"`，其他一律磐石）；`TeamDto.teamColor` 未用 | `ui/theme/Theme.kt:57` |
| `MatchDetailDto` 被截斷 | `GET /{club}/schedule/{id}` 契約回 `MatchDetailDto`（含 goals／cards／lineup），Retrofit 宣告成 `Response<MatchDto>`，`ignoreUnknownKeys` 把三組資料靜默丟掉 | `core/Net.kt:154`、`core/Core.kt:8` |

---

## 1. 啟動載入（S24）、引導（S23）、強制更新／維護閘門

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 雙標誌並列 | 寫死（App 內 drawable，`listOf("tcrfc","bw")`） | ✅ | 規格明定維持雙標誌；`ui/startup/StartupScreen.kt:50`。無障礙描述為 null（裝飾性，進度條有描述） |
| 進度條（App 設定／俱樂部／球隊／賽程／賽事系列） | 啟動任務 `domain/Startup.kt`（依各端點完成度） | ✅ | 非資料欄位 |
| 引導步驟 1：俱樂部名稱 | `GET /clubs` → `ClubDto.name` | ✅ | `ui/club/ClubUi.kt:76` |
| 引導步驟 1：兩隊標誌 | 寫死 drawable（`ClubLogos.resFor`） | ✅ | 契約無標誌欄位（見通用）；`ClubUi.kt:72`。標誌無 contentDescription（隊名同列，可接受） |
| 引導步驟 1：`ClubDto.description` | `/clubs` | ❌ | 未顯示（規格只要求標誌＋隊名，列為資訊性） |
| 強制更新／建議更新文案 | 寫死（strings.xml `gate_update_*`） | ⚠️ | `AppPlatformConfig.forceUpdateMessage`、`recommendUpdateMessage`、`whatsNew` 全部沒讀（全 App grep 無引用）。docs/19 §7 為刻意本機文案，但後台填的訊息被丟棄；`ui/gate/GateScreens.kt:30-69` |
| 維護訊息 | `/app/config` → `maintenance.message`（依語系） | ✅ | `domain/Domain.kt:49`、`GateScreens.kt:49` |
| 預設目前俱樂部 | 寫死 `"tcrfc"`（`settings.currentClub == null` 時） | ✅ | 內容資料寫死但屬降級預設；`AppContainer.kt:194,298`、`ui/programs/ProgramsScreens.kt:143` 另有 `listOf("tcrfc","bw")` 備援探測 |

## 2. 首頁

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 區塊順序與開關 | `GET /app/layout` → `homeSections[].code`；無資料時用寫死 `DEFAULT_SECTIONS` | ✅ | `ui/home/HomeViewModel.kt:46,~110`；寫死的是降級預設 |
| 公告橫幅 | `layout.announcements[0].message` | ⚠️ | 只取第一則；`linkUrl`（可點）與 `endsAt` 沒用；`AppAnnouncementDto.id` 無作用。`HomeViewModel.kt:122`、`HomeScreen.kt:72` |
| 下一場：日期／時間／對手／場地／輪次／賽事系列／狀態／所屬球隊 | `GET /{club}/schedule` → `MatchDto.*`（見 §3 卡片） | ✅ | 沿用 `MatchCard`（`HomeScreen.kt:85`） |
| 下一場：倒數 | — | ❌ | 規格 §3.1 要「倒數」，全 App grep 無 countdown；非 API 缺口，純畫面缺項 |
| 下一場：一鍵加入行事曆／導航 | `MatchDto.venue`、`kickoffAt` | ❌ | 這兩個動作只在賽事詳情頁，首頁卡沒有（規格 §3.1 第 1 列） |
| 最新消息 3 則：封面 | `news` → `ArticleListItemDto.coverUrl`＋`coverAlt` | ⚠️ | 取 320 檔 ✅、alt ✅；`coverFocalX/Y` 沒用（固定 Crop 置中）；`HomeScreen.kt:180`。無封面時整塊略過 ✅ |
| 最新消息：標題／俱樂部名 | `ArticleListItemDto.title`、`ClubDto.shortName` | ✅ | |
| 最新消息：fallback 語系標示 | `isFallbackLocale` | ❌ | 首頁新聞卡沒有 `FallbackNote`（列表頁有） |
| 會員卡快捷 | 本機 session 狀態＋寫死字串 | ⚠️ | 規格：「已登入**且持有目前俱樂部有效會籍**才顯示卡片入口，否則顯示加入會員」。實作只看是否登入（`state.signedIn`），登入但無會籍仍顯示「會員卡」；`HomeScreen.kt:109-117` |
| 近期賽事 3 場 | `schedule` → `MatchDto` | ✅ | 同 §3 |
| 特約店家（附近 3 家）：名稱／類別／地區／適用層級 | `partner-stores` → `PartnerStorePublicDto.name/category/region/applicableTierLabel` | ✅ | `HomeScreen.kt:103`；依後台排序取 3（符合未授權規則）；已授權定位時不依距離（規格 §3.1 第 7 列「已授權定位時依距離」未做）→ 備註列 ⚠️ |
| 特約店家：已授權定位時依距離排序 | — | ⚠️ | 首頁固定後台排序（`HomeViewModel.kt:89` 註解自述） |
| 快捷入口 | `layout.quickEntries[]` → `label`＋`webUrl` | ⚠️ | 沒有 `webUrl` 的項目被濾掉（`HomeScreen.kt:131`）；`deepLink`、`icon` 沒用，只能外開網頁 |
| 贊助商 Logo 牆：Logo | `sponsors` → `SponsorDto.logoLightUrl` | ⚠️ | 只取 light 版；`logoDarkUrl` 不用（深色模式下可能看不見），`logoAlt` 沒用（alt 用名稱）；`HomeViewModel.kt:77`、`HomeScreen.kt:150`。無 Logo 只顯示名稱 ✅（不放假圖） |
| 贊助商分區名稱 | `ClubDto.name` | ✅ | 單一俱樂部時不顯示標頭 |
| 廣告版位 `home_top`／`home_mid` | `GET /app/ads/{slot}`、`/app/ads/prefetch` | ✅ | 見 §23 |
| 區塊標題／按鈕字 | 本機字串 | ✅ | 介面標籤，不算 |

## 3. 賽程與賽果（列表）

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 球隊分頁 | `GET /{club}/teams` → `TeamDto.type/name`；一線隊各一頁＋「學院」合併頁 | ✅ | 動態產生、不硬編代號（`ScheduleViewModel.kt` `ScheduleTabs.build`）；以 `type == "first_team"` 判斷一線隊（`domain/Domain.kt:90`） |
| 賽事系列晶片 | `GET /{club}/competitions` → `CompetitionDto.name` | ✅ | 含尚無賽事的系列＋空狀態說明 ✅ |
| 次要篩選（賽事類型／主客場／月份面板） | — | ❌ | 規格 §3.2 要「收在篩選面板」，畫面沒有；`CompetitionDto.compType`、`MatchDto.competitionTag` 未用 |
| 月份標頭 | 由 `matchOn`/`kickoffAt` 推算 | ✅ | |
| 日期、星期 | `MatchDto.kickoffAt`（優先）／`matchOn` | ✅ | `ui/Format.kt`；依裝置時區 ✅ |
| 開賽時間 | `kickoffAt`／`kickoff` | ✅ | 無時間顯示「時間待定」✅ |
| 對手名稱 | `MatchDto.opponent` | ⚠️ | 只用 `opponent`；`opponentName`（若為對手結構化名稱）、`opponentTeamId` 沒用；`ui/schedule/ScheduleScreen.kt:161` |
| 對手隊徽 | `MatchDto.opponentCrest`（含 url320/640/1280） | ❌ | 契約已有，全 App grep 無引用 |
| 主客場 | `MatchDto.homeAway` | ✅ | 比分依主客場換邊 ✅ |
| 比分 | `scoreHome/scoreAway` | ✅ | |
| 場地 | `MatchDto.venue` | ✅ | `TBC`／空白顯示「場地待定」✅ |
| 輪次 | `roundNo` | ✅ | `matchNo` 不用（規格未要求） |
| 賽事系列名稱＋類型 | `competitionName` | ⚠️ | 系列名稱 ✅；`competitionTag`（聯賽／盃賽類型）不顯示（規格說「並存但不重複」） |
| 狀態 | `status` | ✅ | |
| 所屬球隊名 | `TeamDto.name`＋`ClubDto.shortName` | ✅ | `domain/MatchView.kt:25`；不露代號 ✅ |
| fallback 語系標示 | `isFallbackLocale` | ✅ | `ScheduleScreen.kt:175` |
| 隊徽（俱樂部） | 寫死 drawable | ✅ | `ClubTag`，`Common.kt:122` |

## 4. 賽事詳情

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 日期／時間／場地／賽事系列／輪次／主客場／狀態 | `GET /{club}/schedule/{id}`（或整季快取）→ `MatchDto.*` | ✅ | `ui/schedule/MatchDetailScreen.kt:139-158` |
| 延期前原日期時間 | `originalMatchOn/originalKickoff` | ✅ | 僅 postponed |
| 比分 | `scoreHome/Away` | ✅ | |
| 進球者與時間 | `MatchDetailDto.goals[]`（minute/playerName/goalType/side）；`MatchDto.scorers[]` | ❌ | 兩者 API 都有；`Net.kt:154` 型別為 `MatchDto` 丟掉 `goals`，而 `MatchDto.scorers` 本身也沒被任何畫面讀 |
| 卡牌 | `MatchDetailDto.cards[]` | ❌ | 同上，被型別截斷 |
| 出賽名單 | `MatchDetailDto.lineup[]` | ❌ | 同上；`MatchLineupDto` 全 App 無引用 |
| 賽後報導連結 | `MatchDto.matchReport`（slug/title） | ❌ | 契約有、畫面沒有 |
| 對手隊徽 | `opponentCrest` | ❌ | 同 §3 |
| 加入行事曆 | `kickoffAt`／`matchOn`＋`venue` | ✅ | 無時間 → 全天事件 ✅；`MatchDetailScreen.kt:~160` |
| 場地導航 | `MatchDto.venue`（地址字串搜尋） | 🚫 | 規格要「以 Venue 座標開啟導航，無座標退回地址」；`MatchDto` 沒有 venue 座標／venueId（`GET /{club}/venues` 存在但 Retrofit 沒接，MatchDto 也無外鍵）；現況只能用場地名稱字串搜尋，`MatchDetailScreen.kt:168` |
| 「賽事時間可能異動」聲明 | 本機字串 | ✅ | 規格要求的固定文字 |
| fallback 語系標示 | `isFallbackLocale` | ❌ | 詳情頁沒有 `FallbackNote` |
| 場次提醒設定 | 本機 | — | 非欄位 |

## 5. 新聞列表

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 封面圖 | `ArticleListItemDto.coverUrl`＋`coverAlt` | ✅ | 88dp 縮圖用 320 檔；`ui/news/NewsScreens.kt:148`；無圖不佔位 ✅ |
| 標題 | `title` | ✅ | |
| 分類 | `categoryName`（chips 來自已載入項目的 `categoryCode/Name`） | ✅ | `NewsViewModel.kt` ~49；分類 chips 僅由已載入頁推得，未載入頁的分類不會出現 |
| 日期 | `publishedAt` | ✅ | |
| 俱樂部標示 | `ClubDto.shortName` | ✅ | |
| fallback 語系標示 | `isFallbackLocale` | ✅ | |
| 已存離線標示 | 本機快取 | ✅ | |
| `summary`、`isFeatured`、`tags`、`isShared` | 列表 DTO | ❌ | 列表不顯示摘要／精選標記／標籤（規格列表欄位僅封面、標題、分類、日期，故屬資訊性） |
| 廣告版位 `news_list` | `/app/ads` | ✅ | |

## 6. 新聞內文

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 封面圖 | `ArticleDetailDto.coverUrl`＋`coverAlt` | ✅ | 依螢幕像素挑檔 ✅；`NewsScreens.kt:171` |
| 標題／分類／日期／俱樂部 | `title/categoryName/publishedAt` | ✅ | |
| 內文 | `bodyJson`（解析 p/h/quote/list/image/gallery/video；未知型別→引導官網） | ✅ | `domain/NewsBody.kt`；圖片只有 key 時用 `/app/config.mediaBaseUrl` 組網址；內文圖 alt／caption ✅ |
| 內文空白 | `summary`＋「到官網閱讀」 | ✅ | `NewsScreens.kt:209` |
| 分享（官網網址） | `canonicalPath`＋`env.webBaseUrl` | ⚠️ | 用 App 設定的 `webBaseUrl`，不是該篇所屬俱樂部的 `ClubDto.domain`；藍鯨文章分享出去可能指向磐石網域（`NewsScreens.kt:188-195`；對照 `ClubWeb.baseUrl()` 已有現成函式但新聞沒用） |
| 字級調整 | 本機 | ✅ | |
| fallback 語系標示 | `isFallbackLocale` | ✅ | |
| 關聯內容（`relations`）、標籤（`tags`、`coreValueTags`） | 詳情 DTO | ❌ | 未顯示；規格 §3.3 球員詳情要「關聯新聞」，反向也沒有 |
| `seoTitle/ogImage*/isNoindex/schemaEligible` | — | — | App 不做 SEO，正確略過 |

## 7. 球隊名單

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 球隊選擇 chips | `TeamDto.name` | ✅ | `ui/roster/RosterScreens.kt`；只列目前俱樂部 |
| 球隊簡介／封面 | `TeamDto.intro/heroUrl/heroAlt` | ❌ | 全 App 無引用 |
| 位置分組 | `PlayerDto.position`（GK/DF/MF/FW） | ✅ | 組內依背號 |
| 背號 | `shirtNo` | ✅ | `RosterScreens.kt:171-` |
| 姓名 | `PlayerDto.name` | ✅ | 規格「姓名（中英）」：依 `lang` 取單一語系，未同時顯示中英 → 備註 |
| 球員照片 | `photoUrl`／`photoAlt` | ❌ | **有意不顯示**（肖像 fail-closed，已知）；列表本為文字卡，符合 §3.3 不放佔位圖 |
| fallback 語系標示 | `isFallbackLocale` | ✅ | `RosterScreens.kt:172` |
| 教練與團隊：姓名／職稱／證照 | `StaffDto.name/title/licence` | ✅ | `RosterScreens.kt:149-150` |
| 教練：專長／簡介 | `StaffDto.bio` | ❌ | 未顯示（規格「專長」） |
| 教練：負責梯隊 | `StaffDto.teamCodes` | ⚠️ | 只用來過濾，沒有顯示對應梯隊名 |
| 教練照片 | `StaffDto.photoUrl` | ❌ | 有意不顯示（同上） |
| 教練 fallback 語系標示 | `StaffDto.isFallbackLocale` | ❌ | 沒有 `FallbackNote` |

## 8. 球員詳情與賽季數據

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 背號／姓名／所屬隊 | `PlayerDto.shirtNo/name`、`TeamDto.name` | ✅ | `RosterScreens.kt` 詳情區 |
| 位置 | `position` | ✅ | |
| 生日 | `birthOn` | ⚠️ | 直接顯示原始 `yyyy-MM-dd`，沒依語系格式化 |
| 身高／體重 | `heightCm/weightKg` | ✅ | 單位字串寫死 `cm`／`kg` |
| 國籍 | `nationality` | ✅ | 原值顯示 |
| 慣用腳 | `preferredFoot` | ⚠️ | 原值直接顯示（若為 `right/left` 代碼則不會本地化），`RosterScreens.kt:212` |
| 簡介 | `bio` | ✅ | 空白略過 |
| 照片 | `photoUrl` | ❌ | 有意不顯示（fail-closed，已知）；`portraitConsented` 未被讀 |
| 賽季數據：出賽／進球／助攻／黃牌／紅牌 | `GET /{club}/players/{id}/stats` → `PlayerCareerStatDto` | ✅ | `RosterScreens.kt:219-225`；表頭為本機字串；`source` 未顯示；取不到時整段靜默不顯示（無錯誤提示） |
| 加入日期、前東家、經歷 | — | 🚫 | `PlayerDto` 無這三項（規格 §3.3） |
| 關聯新聞 | — | 🚫 | `PlayerDto` 無關聯欄位（新聞 `relations` 也未接） |
| fallback 語系標示 | `isFallbackLocale` | ❌ | 詳情頁沒有 `FallbackNote`（簡介可能是回退繁中） |

## 9. 夥伴與贊助商

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 俱樂部分區標題 | `ClubDto.name` | ✅ | `ui/partners/PartnersScreen.kt` |
| 贊助商名稱 | `SponsorDto.name` | ✅ | |
| 贊助商等級 | `SponsorDto.tier` | ⚠️ | 規格要「依等級分區（主贊助／官方／支持）」。實作是平鋪列表，且 `tier` **原值**（代碼）直接當副標顯示，沒有分區也沒有本地化標籤；`PartnersScreen.kt:117` |
| 贊助商 Logo | `logoLightUrl` | ⚠️ | 320 檔 ✅；只取 light、不取 dark；alt 用名稱而非 `logoAlt`；`PartnersScreen.kt:145` |
| 贊助商內容 | `content` | ✅ | 點開展開 |
| 贊助故事連結 | `SponsorDto.stories[]` | ❌ | 規格 §3.11 要；未用 |
| 贊助活動 | `activations[]` | ❌ | 未用 |
| 贊助商官網 | — | 🚫 | `SponsorDto` 無 `websiteUrl`（`PartnerDto` 有） |
| 夥伴名稱／內容／官網 | `PartnerDto.name/content/websiteUrl` | ✅ | |
| 夥伴類型 | `partnerType` | ⚠️ | 規格要依類型分區（策略／國際／訓練／教育／品牌）；實作平鋪且顯示原始代碼 |
| 夥伴合作期間 | `startOn/endOn` | ❌ | 規格 §3.11「合作內容、期間」；未用 |
| 夥伴國家、慈善專案 | `country`、`charityPrograms[]` | ❌ | 未用（資訊性） |
| 夥伴 Logo | `logoLightUrl` | ⚠️ | 同贊助商（無 dark、無 `logoAlt`） |
| 無素材時 | 本機字串 `partners_pending`＝「合作洽詢中。」 | ✅ | 不放假 Logo |
| 贊助洽詢按鈕 | `layout.sponsorshipInquiryWebUrl` | ✅ | `PartnersScreen.kt:129` |
| fallback 語系標示 | `isFallbackLocale` | ✅ | |

## 10. FAQ

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 分類 chips | `GET /faq-categories` → `FaqCategoryDto.name/slug/sortOrder` | ✅ | `ui/faq/FaqScreen.kt` |
| 問題／答案 | `FaqListItemDto.question/answer` | ✅ | 點開展開；答案純文字 |
| 搜尋 | 本機對已載入清單比對 | ✅ | `/{club}/search` 端點未用（規格 G-02 全 App 搜尋含新聞、球員、店家：整個 App 沒有搜尋畫面）→ 見彙總 |
| fallback 語系標示 | `isFallbackLocale` | ✅ | |
| 課程頁／學院頁嵌入對應主題 FAQ（G-12） | `GET /{club}/faqs/embeds/{slotCode}` | ❌ | 契約有端點，Retrofit 未宣告；課程詳情無 FAQ 區 |
| 有幫助回饋／瀏覽數 | `POST faqs/{slug}/feedback|views` | ❌ | 未用（規格第 3 章未列為 App 功能，資訊性） |

## 11. 會員中心

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 姓名／會員編號 | 登入 session `member.name/memberNo` | ✅ | `ui/member/MemberScreen.kt:144-145` |
| Email 未驗證提示 | `emailVerified` | ✅ | |
| 會籍列：俱樂部／層級／狀態 | `GET /member/memberships` → `MyMembershipDto.club.name/tierLabel/statusLabel` | ✅ | `MemberScreen.kt:211-213`；只顯示目前俱樂部（`MemberClubView.of`） |
| 球季／有效期限 | `seasonCode/startOn/endOn` | ✅ | `MemberScreen.kt:214-217` |
| 會籍列的會員編號 | — | ⚠️ | 規格「俱樂部、層級、會員編號、有效期限、狀態」；會員編號只在頁首顯示一次（每份會籍的卡片上有），會籍列本身不帶 |
| 方案名稱、發卡數、球衣件數 | `planName/cardQuota/jerseyQuota` | ❌ | `MembershipRow` 未顯示 |
| 續會提示條 | `renewalDue`＋`endOn` | ✅ | 指明俱樂部 ✅ |
| 另一隊也有會籍提示＋切換 | `MemberClubView.otherClubs` | ✅ | |
| 待確認訂單 | `openOrders[]`（planName/statusLabel/seasonCode/orderNo）、`pendingOrder` | ✅ | |
| 可加入俱樂部入口 | `joinableClubs[]` | ✅ | |
| 會員資料入口／球衣／抽獎／方案選單 | 本機 | ✅ | |
| 首頁／會員中心對「僅註冊帳號」提示 | 本機字串 | ✅ | |

## 12. 電子會員卡

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 所屬俱樂部標誌 | 寫死 drawable（磐石加白底圓盤） | ✅ | 契約無標誌欄位；`ui/member/CardsScreen.kt:129-134`，圖片 `contentDescription = null`（俱樂部名稱同列顯示） |
| 俱樂部名稱 | `ClubDto.name`（以卡片 `club.code` 反查）；DTO 本身也有 `MemberCardDto.club.name` | ✅ | `CardsScreen.kt:99` |
| 品牌色 | 寫死 `ClubPalette` | ✅ | |
| 姓名 | `holderName` | ✅ | |
| 會員編號 | `memberNo` | ✅ | |
| 層級 | `tierLabel` | ✅ | |
| 有效期限 | `validUntil` | ⚠️ | 原始日期字串，未格式化；`CardsScreen.kt:141` |
| 狀態 | `statusLabel` | ✅ | 與「最後同步」並陳 ✅；`isValid` 布林未用 |
| QR Code | `token` → 本機組 `{webBase}/m/{token}` | ⚠️ | `webBase` 取 `env.webBaseUrl`（單一網域），不是該卡所屬俱樂部的 `ClubDto.domain`。規格 §3.6 稱「與官網完全一致」，藍鯨卡可能 QR 指向磐石網域（`CardsScreen.kt:146`、`member/Cards.kt:60`）；是否有害取決於兩站驗證頁是否共用 |
| 最後同步時間 | 卡片自帶 `serverTime` | ✅ | `member/Cards.kt:119-120` |
| 超過 7 天提醒 | 本機計算 | ✅ | |
| 重產 token | `POST /member/cards/{id}/regenerate` | ✅ | |
| `reissueCount/issuedAt` | — | ❌ | 未用（規格未要求） |

## 13. 會籍方案與權益對照

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 方案名稱／層級 | `MembershipPlanPublicDto.name/tierLabel` | ✅ | `ui/member/PlansScreen.kt:102-103` |
| 球季期間 | `seasonCode/startsOn/endsOn` | ✅ | |
| 發卡數／球衣件數 | `cardQuota/jerseyQuota` | ✅ | |
| 商店折扣 | `shopDiscountPercent` | ✅ | |
| 方案備註／期中規則 | `benefitNote/midSeasonRule` | ✅ | |
| **建議年費** | `fee`（方案）／`BenefitPlanColumnDto.fee` | ❌ | 規格 §3.7「建議年費」；**有意不顯示**（`PlansScreen.kt` 檔頭註解＋docs/19 §10 保守版），但 API 有、規格要，列為偏離規格 |
| 權益對照表（欄＝方案、列＝權益、組＝groupLabel） | `GET /{club}/membership/benefits` → `BenefitTablePublicDto` | ✅ | 全由 API 組，無硬編權益文字 ✅（`PlansScreen.kt:119-130`） |
| 「購買的是哪一隊會籍」說明 | — | ⚠️ | 規格：畫面須寫明購買對象即目前俱樂部；畫面只用 `s.club` 上色，沒有顯示俱樂部名稱 |
| 收款方說明 | 寫死字串 `plans_payee_note`（含「台中磐石足球俱樂部」） | ✅ | 內容文字寫死（規格明定收款方一律磐石） |
| fallback 語系標示 | `isFallbackLocale` | ✅ | |

## 14. 特約店家清單

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 名稱／類別／地區 | `PartnerStorePublicDto.name/category/region` | ✅ | `ui/stores/StoresScreens.kt:208-210` |
| 適用層級 | `applicableTierLabel` | ✅ | |
| 距離 | 本機 Haversine（`lat/lng`＋裝置定位） | ✅ | 座標不上傳不儲存 ✅ |
| 篩選 chips（類別／地區） | 由已載入店家推得 | ⚠️ | 契約有 `GET /{club}/partner-stores/filters`（`PartnerStoreFiltersDto`），沒用；選項僅由現有清單推得（功能等價但未用官方選項與排序） |
| 地圖檢視（清單／地圖切換、類別圖示） | — | ❌ | 規格 §3.8 要；畫面只有清單 |
| 縮圖／Logo | `imageUrl/imageAlt` | ❌ | 列表不顯示 |
| 一鍵導航／撥號 | 詳情頁（見 §15） | — | |
| fallback 語系標示 | `isFallbackLocale` | ✅ | |

## 15. 特約店家詳情

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 名稱／類別／地區／適用層級 | `PartnerStorePublicDto.*` | ✅ | `StoresScreens.kt:237-240` |
| 優惠內容 | `offerContent` | ✅ | |
| 地址／電話／營業時間 | `address/phone/businessHours` | ✅ | |
| **照片／Logo** | `imageUrl/imageWidth/imageHeight/imageAlt` | ❌ | 規格 §3.8 詳情要「名稱、照片／Logo」；DTO 有、畫面完全沒用 |
| 地圖連結 | `mapUrl` | ❌ | 未用（導航改用 `lat/lng` 或地址，功能上有替代） |
| 官網或社群連結 | `websiteUrl` | ✅ | |
| 一鍵導航 | `lat/lng`，否則地址 | ✅ | |
| 一鍵撥號 | `phone` → ACTION_DIAL | ✅ | |
| 升級 CTA（限尊尚） | `applicableTier` ＋ 會籍查詢 | ✅ | |
| fallback 語系標示 | `isFallbackLocale` | ❌ | 詳情沒有 `FallbackNote` |

## 16. 課程與試訓

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 列表：俱樂部名／課程名 | `ClubDto.name`／`ProgramListItemDto.name` | ✅ | `ui/programs/ProgramsScreens.kt:124-125` |
| 列表：課程類型／對象 | `programType`、`audience` | ⚠️ | 原值直接顯示（若後端回代碼則不會本地化）；`ProgramsScreens.kt:128` |
| 列表：年齡 | `ageMin/ageMax` | ✅ | |
| 列表：簡介（2 行） | `intro` | ✅ | |
| 列表：開放報名標示 | `hasOpenSession` | ✅ | |
| **列表／詳情：封面圖** | `coverUrl/coverAlt/coverKey` | ❌ | 列表與詳情都不顯示封面 |
| 詳情：名稱／簡介／內文 | `ProgramDetailDto.name/intro/content` | ✅ | 內文純文字（若含 HTML/Markdown 會原樣出現，待實機） |
| 詳情：年齡／對象 | `ageMin/ageMax/audience` | ❌ | 詳情頁不顯示（列表有） |
| 詳情：教練／講師 | `staff[]`（`ProgramStaffSummaryDto.name`） | ❌ | 未用 |
| 詳情：合作夥伴 Logo | `partners[]`（logo/websiteUrl） | ❌ | 未用 |
| 詳情：嵌入主題 FAQ（G-12） | `faqs/embeds/{slot}` | ❌ | 見 §10 |
| 梯次：起訖／每週時段／場地／地址 | `ProgramSessionDto.startOn/endOn/weeklySchedule/venueName/venueAddress` | ✅ | `ProgramsScreens.kt:181-183` |
| 梯次：狀態／剩餘名額／價格 | `statusLabelZh/En`、`capacity-enrolledCount`、`price` | ✅ | 狀態文字依語系 ✅ |
| 梯次：早鳥價／早鳥截止 | `earlyBirdPrice/earlyBirdUntil` | ❌ | 未顯示（DTO 有） |
| 梯次：報名開放／截止時間 | `signupOpensAt/signupClosesAt` | ❌ | 未顯示 |
| 梯次：場地座標導航 | `venueLat/venueLng` | ❌ | 未用 |
| 報名表（姓名／生日／家長／電話／Email／備註／隱私同意） | 本機輸入＋會員資料帶入 | ✅ | 健康聲明刻意不蒐集（`ProgramRegistrationPolicy`，B-9） |
| 隱私權政策連結 | `ClubDto.domain` ＋ 寫死路徑 `/zh|en/privacy/` | ⚠️ | 路徑寫死；契約有 `GET /{club}/policies/{code}`，沒用；`ProgramsScreens.kt:274` |
| 隱私同意文字 | 寫死字串 `programs_privacy_consent` | ✅ | 同意版本由後端留存 |
| 試訓：日期／球隊／對象／場地／截止／名額／狀態 | `PublicTrialDto.trialOn/teamName/audience/venueName/venueAddress/deadlineOn/capacity/enrolledCount/statusLabel*` | ✅ | `ui/programs/TrialsScreens.kt:66-77` |
| 試訓：場地座標 | `venueLat/venueLng` | ❌ | 未用 |
| 課程／試訓畫面不放廣告 | — | ✅ | 符合 §3.12 |

## 17. 我的報名

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 課程名／試訓 | `GET /member/registrations` → `course.programName`／`kind` | ✅ | `ProgramsScreens.kt:321` |
| 學員姓名 | `applicantName` | ✅ | |
| 梯次時間／地點 | `course.startOn/endOn/weeklySchedule/venueName/venueAddress` | ✅ | 不寫入行事曆 ✅ |
| 試訓日期／球隊／場地 | `trial.trialOn/teamName/venueName` | ✅ | `trial.venueAddress` 未顯示 |
| 報名狀態／編號 | `statusLabelZh/En`、`registrationNo` | ✅ | |
| 繳費狀態 | 由 `statusCode`（paid/completed）推導 | ⚠️ | 契約無獨立繳費欄位，為推導（已知，程式註解）；`course.price/earlyBirdPrice` 沒顯示金額 |
| 梯次狀態（`sessionStatusLabel*`）、報名時間 `createdAt` | — | ❌ | 未顯示 |
| 單筆匯出到裝置行事曆 | — | ❌ | 規格 §3.9 「可提供單筆匯出」為「可」，列資訊性 |
| fallback 語系標示 | `isFallbackLocale` | ✅ | |

## 18. 抽獎資訊

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 俱樂部／名稱 | `GET /member/draws` → `club.name`、`name`（缺則 `drawCode`） | ✅ | `ui/member/DrawsScreen.kt:60-63` |
| 狀態 | `statusLabel` | ✅ | |
| 個人資格布林 | `isEligible` | ✅ | 無人數計數器 ✅ |
| 獎品／辦法／注意事項 | `prizeDescription/rules/notes` | ✅ | 「兩隊可分別參加」那句依賴後端 `rules` 內容 |
| 開獎場合 | `occasionLabel` | ✅ | |
| **開獎時間** | `drawnAt` | ❌ | 規格 §3.10 第 3 項「開獎時間與開獎場合」；只顯示場合與 `claimDeadlineOn`，`drawnAt`、`snapshotAt`（資格基準時間）未用 |
| 領獎期限 | `claimDeadlineOn` | ✅ | |
| 結果公布連結 | `announcement.slug/categoryCode` | ✅ | |
| 封面圖 | `coverUrl/coverAlt` | ❌ | DTO 有，畫面無 |
| 場合分類 `occasion` | — | — | 已用 `occasionLabel` |
| fallback 語系標示 | `isFallbackLocale` | ✅ | |

## 19. 球衣登記

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 俱樂部／球季／已用／配額 | `GET /member/jerseys` → `clubName/seasonCode/used/quota` | ✅ | `ui/member/JerseysScreen.kt:~107` |
| 領用人／尺寸 | `recipientName/size` | ✅ | |
| 領取方式／地址 | `deliveryMethodLabel/address` | ✅ | |
| 狀態＋寄出／領取日 | `statusLabel/shippedOn/receivedOn` | ✅ | |
| 電話 | `MemberJerseyDto.phone` | ⚠️ | 列表不顯示（編輯表單有） |
| 可否登記／可否編輯 | `canRegister/editable` | ✅ | |
| 尺寸選項 | 自由輸入 | ✅ | 後端無固定清單（程式註解） |

## 20. 個人資料與登入／註冊

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| Email／姓名／手機／生日 | `GET /member/me` → `MemberProfileDto` | ✅ | `ui/member/ProfileScreen.kt:~71-75` |
| 語系偏好 | `MemberProfileDto.locale` | ⚠️ | 不顯示也不可改；更新時固定送 App 當前語系 |
| 註冊來源、LINE 綁定狀態 | `signupSourceLabel`、`lineBound` | ❌ | 未顯示；LINE 登入／解除綁定（`DELETE /member/me/line`）整個沒做（`AuthScreens.kt:102` 註解：外部設定未到位，已知） |
| 改密碼／登出／全部登出／刪除帳號 | 本機操作＋端點 | ✅ | 刪除帳號 App 內完成 ✅ |
| 已登入裝置列表 | `GET /member/devices` | ❌ | Retrofit 未宣告（規格第 3 章未列，資訊性） |

## 21. 通知中心

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 標題／內文 | `GET /app/notifications` → `title/body`（`lang`） | ✅ | `ui/notifications/NotificationsScreen.kt:73-74` |
| 時間 | `sentAt` | ✅ | |
| 深連結 | `deepLink` | ✅ | |
| 圖片 | `imageUrl` | ❌ | 解析進 `NotificationItem.imageUrl` 後畫面沒用（`domain/Notifications.kt:14`） |
| 90 天保留／離線可讀 | 後端＋本機 90 天過濾 | ✅ | |
| 標記已開啟 | `POST /app/push/{id}/opened` | ❌ | 未宣告（FCM 未接，已知） |

## 22. 通知偏好與追蹤設定

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 隊別／俱樂部主題清單 | `clubs`＋`teams`（動態產生） | ✅ | `ui/onboarding/FollowScreens.kt:100-109` |
| 訂閱狀態 | 本機 `container.follow` | ⚠️ | 只存本機；`GET/PUT /app/devices/{id}/subscriptions`（`AppSubscriptionDto`）Retrofit 未宣告，後端不知道訂閱（與 FCM 未接同屬已知） |
| 分類開關（賽事提醒／賽果／新聞／會籍與球衣） | — | ❌ | 規格 §3.13 要四類各自開關；設定頁只有「開賽提醒」單一開關（本機通知） |
| 靜音時段 | 本機設定 | ✅ | |
| 系統權限被拒說明＋前往設定 | 本機 | ✅ | |

## 23. 設定、更多、俱樂部切換、廣告版位

| 欄位／圖片 | 來源 | 狀態 | 備註 |
|---|---|---|---|
| 語系／快取／版本資訊／更新檢查 | 本機＋`/app/config` | ✅ | `ui/settings/SettingsScreen.kt` |
| 隱私政策、服務條款、聯絡我們 | — | ❌ | 規格 §3.14 要；設定頁無此三項，全 App 也沒有入口（契約有 `GET /{club}/policies/{code}`，未接）。只有報名表有隱私權政策連結 |
| 定位權限說明 | 本機字串（店家頁） | ⚠️ | 設定頁沒有集中說明入口 |
| 「為什麼看到廣告」 | 寫死字串 `settings_why_ads_body` | ✅ | |
| 更多選單項目 | 本機 `rows` 寫死 7 項＋`layout.moreItems`（僅外開項） | ⚠️ | 原生項目的顯示／順序**不受後台 M2 控制**（`nativeCodes` 寫死，`MoreScreen.kt:34,49`）；外開項（商店、慈善）用 `label/webUrl` ✅；慈善只在磐石出現（`CurrentClub.ROCK` 寫死 `"tcrfc"`） |
| 慈善收受者聲明 | 寫死字串（含「台灣足球策略發展協會」） | ✅ | 規格明定須明示 |
| 俱樂部切換鈕 | 寫死 drawable＋`ClubDto.shortName` | ✅ | |
| 廣告：圖片 | `AppAdItemDto.imageVariants`（320/640/1280）→ 無則 `imageUrl` | ⚠️ | 依視窗寬度挑檔 ✅、alt（`altText ?: title`）✅；但挑到的衍生檔若為 null 不會退回 `imageUrl`（`domain/AdPrefetch.kt:49-51`）；`videoUrl/ctaText` 未用 |
| 廣告標示「廣告」 | 回應 `disclosureLabel` | ✅ | |
| 廣告備援 | 回應 fallback 項；無資料時本機字串 `app_name_short`（「台中足球」） | ⚠️ | 規格「備援素材（俱樂部自家內容）」；API 有 fallback 時用 API，否則是純文字，不是素材圖 |

---

## 彙總（以表格列計）

| 狀態 | 列數 |
|---|---|
| ✅ 已接 | 153 |
| ⚠️ 部分 | 31 |
| ❌ 未接 | 53 |
| 🚫 契約沒有 | 4 |

（另有「—」的非欄位／資訊性列未計。）

### 🚫 契約缺口
1. 賽事場地座標／venueId（`MatchDto` 無；導航只能用場地字串）— §4
2. 球員加入日期、前東家、經歷 — §8
3. 球員關聯新聞 — §8
4. `SponsorDto` 無官網連結 — §9

### 寫死的內容資料（非介面標籤）
- 俱樂部標誌：`club_logo_tcrfc`／`club_logo_bw` drawable（`ui/theme/Theme.kt:160`）
- 俱樂部品牌色與代號判斷：`ClubPalette.forClub`（`Theme.kt:57`）、`CurrentClub.ROCK/BLUE_WHALE`（`domain/CurrentClub.kt:12-13`）
- 預設目前俱樂部 `"tcrfc"`（`AppContainer.kt:194,298`）、課程詳情備援探測 `listOf("tcrfc","bw")`（`ProgramsScreens.kt:143`）、啟動畫面雙標誌（`StartupScreen.kt:50`）
- 首頁區塊預設順序（`HomeViewModel.kt:46`，API 有資料時被覆蓋）
- 更多選單的原生項目與 `nativeCodes`（`MoreScreen.kt:34,49`）
- 字串資源中的內容性文字：收款方「台中磐石足球俱樂部」（`plans_payee_note`）、慈善收受者「台灣足球策略發展協會」（`more_charity_notice`）、隱私權政策路徑 `/zh|en/privacy/`（`ProgramsScreens.kt:274`）、廣告備援文字「台中足球」（`app_name_short`）
