# 23 — 後台欄位 → 前台串接稽核（2026-10-06）

> **性質**：執行層的稽核紀錄，不是規格。規格以四份規劃書為準。
> **方法**：靜態讀碼，六組並行（`qa-test-engineer`），逐欄追「後台畫面 → 後台 API 寫入 → 資料表 → 公開 API 輸出 → 前台顯示」，並反向找「前台寫死、後台改不到」。**未實機驗收**。
> **範圍**：`apps/admin` 全部模組對 `apps/web`（主站＋藍鯨）。慈善獨立平台（`apps/admin-charity`／`apps/web-charity`）不在範圍；金流、發票、寄信的假實作屬已知（`B-10`／`B-16`）不列。
> **追蹤**：[`STATUS.md`](../STATUS.md) `S2-21`。修掉一項就在本檔該列標 ✅ 並寫日期。

## 進度

**2026-10-06 第一輪修正**（✅ 標於各列）：A-1、A-2、A-3、A-4、A-6、A-8、A-10、A-12、B-12、B-13、B-16、B-22、C-1、C-3、E-1、E-2，以及 D 的 `contactHoursEn`。
- 使用者拍板：A-3 **鎖住欄位**（七類表單＋提案下載，規劃書 v3.19 G1／E3）、A-12 **新增賽季管理**（v3.19 C4）。B-13 依規劃書 C2「可手動輸入」補後台入口。
- 契約與行為：`apps/api/README.md`「後台欄位串接稽核的後端修正」、`apps/admin/README.md`、`apps/web/README.md`。API 測試 1498／1498；後台與前台 build、lint 通過。**全部未實機驗收**。
- 行為變更須知：公開球員只回現役（離隊球員詳情 404，數據榜仍列）；表單導向頁只收站內路徑；進球類型改代碼值域，舊自由文字在後台載入時對應，對不上歸「其他」。

**2026-10-06 第二輪修正**：A-5（P1 課程內容改用與靜態頁共用的區塊編輯器 `PageBlockListEditor`，前台以 `PageBlocks` 渲染、舊純文字相容）、A-7（`/{lang}/preview/{token}` 預覽頁）、A-9（公開行事曆展開重複規則、依球隊篩選、排除不公開類型；前台以 `occurrenceId` 為鍵、各隊分頁與月曆顯示自建活動）、A-11（`/{lang}/member-terms/` 頁；Email 與 LINE 註冊同意句皆連到此頁）、B-6（前台讀 `calendar/settings` 與活動類型名稱顏色圖示；`homeTeamCodes` 未接——首頁近期賽事為寫死結構）、B-7（訂閱連結，BFF 專屬 feed 分支保留 `text/calendar`）、B-9（P1 合作夥伴多選）。B-1 部分：`PageBlocks` 補齊 12 種區塊渲染（其餘「各靜態頁改讀 CMS」仍待做）。API 測試 1503／1503。**全部未實機驗收**——建議驗收時 `curl -I` 確認 feed 的 `Content-Type: text/calendar`。

**2026-10-06 第三輪修正（B／C／D 類）**：
- **B-1／B-2**：12 個靜態頁接 CMS（`useCmsPage`＋`CmsPageBand`，無已發布內容時沿用寫死版；SEO 欄位套用），`PageBlocks` 支援 12 種區塊。刻意不接：`about/index`、`about/ecosystem`、`club/index`（導覽頁）、`about/milestones`（里程碑模組）。種子中較精簡的 5 頁改為草稿、HTML 改為區塊（`db/seed/README.md`）。
- **B-3** SEO 預設；**B-14** 首頁 Banner 文字／精選輪播／區塊排序（藍鯨核心價值種子預設關閉，文案待客戶確認）；**B-15** 新聞 seoTitle／核心價值標籤／相關球員；**B-18** 商品標籤；**B-20** 漫畫原型球員；**B-21** 圖集燈箱。
- **B-4 使用者拍板（規劃書 v3.20）**：後台不提供標誌、Favicon、品牌色設定，改由前台靜態資產與 CSS 定義；`clubs` 五個品牌欄位刪除（收縮遷移）。
- **B-8** 課程固定頁顯示後台內容、教練團、夥伴，同類多課程全列；**B-10** 學院梯隊與一線隊讀 `/teams`；**B-11** 職員依 `staffGroup` 分組（後台仍無組內排序欄位）。
- **C-2** 聯絡 Email、社群連結、各部門窗口、頁尾簡介後台可編輯（`settings` key，種子已補）；Organization JSON-LD 補 email／telephone／sameAs。**C-4** 部分（Hero 文案改讀 Banner）；**C-7** 部分（主要場地讀場地 API；合作學校表無資料來源）。
- **D** 雙語：三張新側表＋兩個側表欄位（`db/migrations/20261006_d-bilingual-gaps_*`、EF migration），後台六畫面補英文欄位，前台積分榜／提案帶 `lang`。
- 部署順序：`DBilingualGapsExpand` 可隨新版 API；`ClubBrandDropContract`、`DBilingualGapsContract` 為收縮，**新版 API 上線驗證後**經 `production-db` 核准關卡套用（建議另開 PR）。本機 `tcrfc_club` 已套用三支。API 測試 1514／1514。**全部未實機驗收**。
- 仍待：B-5（多語系設定）、B-17（捐助洽詢入口）、B-19（介面字串）、C-5（fallback 寫死內容，屬過渡設計）、C-6（商店固定文案）、F 類；另 `homeTeamCodes` 未接（首頁近期賽事為寫死結構）、合作學校資料來源、職員分組英文名稱待對照 `docs/06`、積分榜公開 DTO 補 `isFallbackLocale`、榮譽對話框改逐欄錯誤。

**2026-10-07 第四輪修正（殘項收尾）**：
- **B-19** ✅：規劃書 I4 定義為「按鈕、表單標籤、提示訊息、錯誤訊息」的雙語對照，不要求全站字串代號化；七類表單共用送出訊息改讀 `useUiStrings().t()`（新增 `form.error_generic`／`form.captcha_required`／`form.captcha_failed`，後台未建立時沿用原文）。
- **C-6** ✅ 檢查後不需修改：配送三方式與發票四類型為規格固定值（主站 §8.3），「主場賽事日」兩俱樂部同一條規格；收款方與發票抬頭已依 `collectingSubjectName` 動態顯示。
- **B-17**：提案下載經 `Features/Proposals` 寫入 `form_code='proposal_download'`，收件匣分頁有資料，**非重複** ✅；捐助洽詢 ❓ **待使用者決定**（規劃書只在 G2 分頁清單與 `Enquiry` 型別各提一次，無前台入口與欄位）。
- **B-5**：規劃書 G-01 已固定繁中／英文、回退繁中，後台側欄已隱藏「多語系」→ 建議以「規格已固定、不接」結案。**C-5**：過渡設計，待客戶內容到位、後台建立第一筆後再移除寫死版。
- 零星項 ✅：首頁近期賽事接 `homeTeamCodes`；職員分組英文名登錄 `docs/06`（初稿待確認）；榮譽對話框逐欄錯誤；積分榜 `StandingRowDto.isFallbackLocale`（後端＋前台）。
- **F 類**：轉址正規化（有／無結尾斜線視為同一網址）＋儲存時迴圈偵測 ✅；賽程多隊賽事重複列與計數 ✅（`MatchesRepository`、`CalendarRepository` 的 `COUNT(DISTINCT)`）；廣告備援連結放行 `tcrfc://` ✅（App 規劃書 §2.3、§7）；球迷活動與媒體封面雙語 Alt ✅（`cover_alt`，展開遷移 `ClubCoverAltExpand`）；慈善計畫詳情夥伴／贊助商不濾期間 ❓ **待決**（規劃書無明文；公開夥伴列表已濾期間，只有此處不一致）；報名隱私同意留存 ❓ **待決**（規劃書未要求留存）。
- 同批順帶：`ad_creatives.click_url` 改用同一套連結規則（原本 `tcrfc://` 開頭即不驗）；球員數據 `PlayerSeasonStatDto.isFallbackLocale`；後台球迷活動與媒體專區補「圖片說明」輸入、前台封面 alt 改讀 `coverAlt`。
- API 測試 1562／1562；後台與前台 lint、build 通過。**全部未實機驗收**。

## 0. 總結

- 約 700 個後台欄位。**後台 → 後台 API → 資料表這兩段幾乎全通**（multipart 欄位名、`*_i18n` zh／en 寫入、圖片欄位組、`club_id` 歸屬）。
- 問題集中在兩處：**公開 API 有輸出但前台沒讀**、**前台寫死不讀 API**。
- 主站／藍鯨的 `club_id` 篩選沒有串錯；唯一例外是 `charity/commitment.vue` 寫死 `tcrfc`（C-1）。
- 標記：🔴 會產生錯誤資料或擋住使用者 ／ 🟠 後台能改、前台不變 ／ 🟡 雙語或顯示缺口 ／ 🔒 安全 ／ ❓ 需規格決定 ／ ⏸ 建議結案或暫緩（見進度段）

## A. 🔴 會出錯（資料錯、送不出、看不到該看的）

| # | 模組 | 問題 | 證據 | 修法方向 |
|---|---|---|---|---|
| ✅ A-1 | 球員數據 | 自動彙總把**烏龍球算成該球員進球** | `Features/Standings/StandingsRepository.cs:79,104`（不看 `GoalType`） | 彙總排除 own goal |
| ✅ A-2 | 球員 | 球員**狀態（離隊／外借）公開端不過濾也不輸出**，離隊球員仍出現在一線隊頁、首頁球員橫幅、梯隊頁 | `Features/Players/PlayersRepository.cs:50-66` | 公開端預設只回現役，或輸出 status |
| ✅ A-3 | 表單設計器 | 前台 7 張表單是寫死 DOM；後台**新增必填欄位、刪改欄位鍵、改下拉選項**都會讓前台送出被 400 擋下 | `apps/web/app/composables/useFormSubmit.ts:1-17`、`Features/Forms/FormsRepository.cs` 必填／欄位歸屬／select 驗證 | ❓ 決定是「鎖住欄位鍵與必填」還是「前台改讀定義渲染」 |
| ✅ A-4 | 表單設計器 | **收件通知 Email、自動回覆信從未呼叫寄信介面**（不只是假實作） | `FormsRepository.SubmitAsync` 無任何 mail 呼叫 | commit 後呼叫 `IEmailSender` |
| ✅ A-5 | 課程 P1 | 「課程內容」後台強制 JSON、前台當純文字分段印出——**照指示填會印出 JSON 原文，填純文字存不了** | 後台 `ProgramItemEditView.vue:135`；前台 `pages/zh/programs/[slug]/index.vue:60` | 兩邊對齊同一格式 |
| ✅ A-6 | 商店訂單 | 後台訂單詳情**看不到買家 Email 與發票資料**（類型、載具、統編、捐贈碼），訪客單客服無聯絡信箱 | `Features/AdminShop/AdminShopOrderDtos.cs:69`、`OrderDetailView.vue:301-306` | DTO 補欄位，載具比照遮罩；同處 `issueStatus`／`voidStatus` 直接印英文代碼要改中文 |
| ✅ A-7 | 靜態頁 | 後台**預覽連結 404**（前台無 `/preview/{token}` 路由） | `PageEditView.vue:461` | 補路由或移除連結 |
| ✅ A-8 | 新聞／FAQ | **瀏覽數永遠 0**（前台沒呼叫 `/views`，BFF 白名單也沒放行） | `news/[slug]/index.vue`、`FaqAccordion.vue`、`server/api/backend/[...path].ts:29` | 前台呼叫＋白名單 |
| ✅ A-9 | 行事曆 | 重複規則活動前台**任何地方都看不到完整場次**（列表不展開、月曆不畫自建活動）；掛在特定隊的活動跑到「俱樂部活動」分頁 | `Features/Calendar/CalendarRepository.cs:136-165`、`schedule.vue:529` | 列表展開或月曆加畫活動；分頁依 team 篩選 |
| ✅ A-10 | 首頁 Banner | 第 1 則按鈕連結 en 與 zh 同值（`/zh/...`），**英文頁使用者被帶到中文頁** | `HomeLayoutView.vue:214`、`pages/zh/index.vue:113,538` | 經 `lp()` 轉換或 en 分開存 |
| ✅ A-11 | 政策頁 | 「會員條款」後台可編、**前台沒有頁面**（註冊流程卻需要同意條款） | `usePolicy` 只被 privacy／cookies 呼叫 | 補 `member-terms` 頁 |
| ✅ A-12 | 賽季 | **沒有後台入口可開新賽季**，只能靠種子資料；賽事、積分榜、榮譽都依賴賽季 | `apps/api`、`apps/admin` 皆無 Season 寫入 | ❓ 規劃書未定義賽季管理 |

## B. 🟠 後台能改、前台不變

| # | 模組 | 沒生效的欄位／功能 | 證據 |
|---|---|---|---|
| ✅ B-1 | 靜態頁 B1 | **全站只有 `charity/commitment` 一頁讀 CMS**；about／club／join／faq 等種子頁前台都是寫死。12 種區塊只渲染 7 種（缺 `text_image`、`gallery`、`video_embed`、`accordion_faq`、`file_download`）；SEO 只讀 title／description | `shared/utils/page-blocks.ts:57-88`；`PageBlockTypes.cs` |
| ✅ B-2 | 9.3 成為合作夥伴 | 規劃書對照表歸頁面管理，前台整頁寫死 | `pages/zh/partners/become-a-partner/index.vue`；規劃書 L965 |
| ✅ B-3 | 全站 SEO 預設 | 標題樣板、預設描述、預設 OG 圖前台完全沒讀（`app.vue` 只取追蹤碼） | `app/app.vue:27,88` |
| ✅ B-4 | 全域設定品牌 | Logo 淺／深、Favicon、主色、輔助色只有維護頁用到 | `server/middleware/maintenance.ts:50-61`；❓ `docs/14` 規定品牌色唯一來源是 `tcrfc.css`，可能應改標「僅維護頁用」 |
| ⏸ B-5 | 多語系管理 | 啟用語系、備援規則、日期數字格式前台沒讀 | `shared/utils/site-settings.ts:28-30` 無呼叫者 |
| ✅ B-6 | 行事曆 L3 | 前台顯示設定（預設檢視／範圍／球隊、`homeTeamCodes`）、隊別名稱顏色排序公開；活動類型名稱顏色圖示——前台全沒讀 | `schedule.vue:175,187,221-229`；`/calendar/settings` 無呼叫者 |
| ✅ B-7 | 行事曆訂閱 | `feed.ics` 已實作，前台文案寫「尚未上線」且無連結 | `schedule.vue:904-911,1113` |
| ✅ B-8 | 課程固定頁 5.1–5.5 | 只用梯次與 Schema；內容、教練團、合作夥伴、封面、適合對象都不顯示（夏令營「教練團」區塊只有標題）；同類型多個課程只取第一個的梯次；課程總覽卡片來自 `club-copy.ts`，沒有連到動態詳情頁 | `programs/summer-camp/index.vue:107-130` 等 |
| ✅ B-9 | 課程合作夥伴 | 後台**沒有選擇介面**（說明文字仍寫「E1 尚未開發」），後端與前台都已支援 | `ProgramItemEditView.vue:312-318` |
| ✅ B-10 | 球隊 C1 | 簡介、年齡層、代表色、性別、主視覺、排序前台都沒用；學院梯隊分頁寫死 | `academy/teams.vue:43`、`first-team/index.vue:41` |
| ✅ B-11 | 職員 | 後台分組 `staffGroup` 前台沒讀；前台用寫死的姓名排序表與職稱覆寫，**新職員排最後且無法調**；各隊角色 `role_code` 不輸出 | `about/our-people.vue`；`Features/Staff/StaffRepository.cs:118-122` |
| ✅ B-12 | 賽事系列 C3 | `compType`、`organizer` 無前台出口；**草稿賽事名稱仍出現在賽程** | `Features/Schedule/MatchesRepository.cs`（`LoadCompetitionNamesAsync` 不濾 status） |
| ✅ B-13 | 球員手動數據 | 公開端讀 `player_season_stats`（手動優先），但**沒有任何寫入入口** | `StandingsRepository.cs:109-115`；❓ 規劃書只寫「自動彙總」（L1760），可能應移除 manual 分支 |
| ✅ B-14 | 首頁 | Banner 標題、副標、按鈕二不顯示（Hero 文案寫死）；「精選輪播」與區塊「排序」不生效；藍鯨開「核心價值」區塊無效 | `pages/zh/index.vue:29,101-113,720`；`useHomeSections.ts` 檔頭 |
| ✅ B-15 | 新聞 | `seoTitle` 沒用（title／ogTitle 都用標題）；核心價值標籤、球員關聯前台不顯示 | `news/[slug]/index.vue:170-185` |
| ✅ B-16 | 表單 | 「送出後導向頁」公開 DTO 刻意不輸出、前台也不處理 | `Features/Forms/FormDtos.cs:29` |
| 🔄 B-17 | 表單 | `proposal_download`、`donation_enquiry` 兩張表前台無入口，收件匣分頁永遠空（提案下載走 `Features/Proposals`，未確認是否重複） | — |
| ✅ B-18 | 商品 | 標籤可編、API 支援 `?tag=`，前台不顯示也不篩 | `pages/zh/shop/index.vue` |
| ✅ B-19 | 介面字串 | `ui-strings` 只用在頁尾電子報 5 個鍵 | `useUiStrings.ts:1-10` |
| ✅ B-20 | 漫畫角色 | 關聯球員不顯示（頁面文案卻說可關聯原型球員） | `culture/manga/index.vue:106-116` |
| ✅ B-21 | 圖集截斷 | 贊助活動只顯示 3 張、慈善事蹟只顯示 2 張；媒體專區只有高解析圖庫類顯示封面 | `our-sponsors/index.vue`、`charity/impact-stories.vue`、`media.vue:210` |
| ✅ B-22 | 快取 | 改後台要等 TTL（預設 300 秒）才生效：`AdminSeo`／`AdminSiteFacts`／`AdminForms` 不失效；改球隊代碼名稱不失效 players／staff／schedule／honors 快取 | `AdminTeamsRepository.cs:215,285` |

## C. 🟠 前台寫死、後台改不到（反向）

| # | 內容 | 位置 |
|---|---|---|
| ✅ C-1 | `charity/commitment.vue` 讀 CMS 的網址寫死 `tcrfc`，藍鯨讀不到 | `pages/zh/charity/commitment.vue:24` |
| ✅ C-2 | 社群連結（FB／IG／YT）、聯絡 Email、頁尾品牌簡介；Organization Schema 缺電話／`sameAs`／Email | `shared/utils/club-copy.ts:183-210`、`useSchemaOrgClub.ts:96-108` |
| ✅ C-3 | 聯絡頁電話、營業時間、地址對藍鯨整塊 `v-if="isTcrfc"`，藍鯨後台填了也不顯示 | `join/contact/index.vue:72,85,92` |
| ✅ C-4 | 首頁 Hero 文案、`ecosystem_nav`、`bottom_cta`、球員橫幅（無開關）、核心價值說明與圖示 | `pages/zh/index.vue`、`shared/utils/core-values.ts` |
| ⏸ C-5 | Fallback 寫死內容（後台建第一筆就整批消失）：三個國際夥伴隊徽、9 張贊助方案卡、慈善事蹟 3 筆與影響力數字 | `partners/our-partners`、`partners/opportunities:141-207`、`charity/impact-stories.vue`、`our-impact.vue` |
| ✅ C-6 | 商店：配送方式名稱說明（藍鯨也顯示「主場賽事日」）、發票類型清單、電子發票說明段 | `ShopCatalogRepository.cs:298-310`、`checkout/index.vue:81`、`shop/index.vue:258` |
| ✅ C-7 | school-community 合作學校表（藍鯨常數、磐石空白）、兒童訓練主要場地 | `programs/school-community`、`childrens-training/index.vue:183` |

## D. 🟡 雙語缺口（`/en/` 顯示中文；違反全域規定 4）

榮譽 `competitionName`／`placing`、積分榜 `teamName`、課程 `audience`、會籍方案 `midSeasonRule`、提案 `title`、SiteFacts `contactHoursEn`（前台只取 zh）、聯賽英文簡稱（前台型別無欄位）。
另：多個後台 API **英文名稱留空時把其他英文欄位一併丟棄、不報錯**（`AdminPartners`、`AdminSponsors*`、`AdminCharity*`、`AdminImpactMetrics` 的 `SetI18n`）——應改為 400。

## E. 🔒 安全

| # | 問題 | 證據 |
|---|---|---|
| ✅ E-1 | 追蹤碼（GA4／GTM／Pixel／LINE Tag）不驗格式，前台直接拼進 `innerHTML` 腳本 | `AdminSeoSettingsRepository.cs:90-93`、`app/app.vue:33-52` |
| ✅ E-2 | 行事曆自建活動 `ctaUrl` 不驗協定，前台直接放 `:href`（可填 `javascript:`） | `AdminCalendarCustomEventsRepository.cs:130,197`、`schedule.vue:1057` |

## F. ❓ 其他待確認

- 報名的隱私同意只勾選不留存（`privacyConsent` 未送出）——若需留同意紀錄即為缺口。
- 轉址 `fromPath` 不正規化（結尾斜線），`/zh/about` 打不到 `/zh/about/`；無迴圈偵測。
- 賽程列表多隊賽事 JOIN `match_teams` 無 DISTINCT，可能重複列。
- 廣告版位「備援連結」UI 說可填 App 連結，API 只收 https（`AdminAdSlotsRepository.cs:158`）。
- 慈善計畫詳情的夥伴／贊助商不濾合作期間。
- 孤兒頁與 Schema 完整度報表以資料庫為準，但前台多數頁不從資料庫渲染，報表失真。
- 只掛在自建活動的場地不出現在 `join/location`；課程無排序欄位；`programType` 可清空導致課程不出現在任何固定頁。
- 球迷活動、媒體封面沒有替代文字欄位（§4.0 圖片欄位組要求雙語 Alt）。

## G. 確認通過的主幹（不需動）

選單、隱私／Cookie 政策、維護模式、llms.txt／robots.txt／AI 爬蟲（兩站各自一份）、轉址套用、FAQ（含嵌入槽）、球迷活動、漫畫、媒體專區下載、夥伴／贊助商／贊助方案／提案下載、慈善計畫／事蹟／指標／CTA、商品與規格、商店設定政策、會籍方案與權益、特約店家、球衣、課程梯次、試訓、報名（前台送出欄位後台都看得到）、場地、里程碑、賽程比分、積分榜、球員與職員基本欄位（肖像同意 fail-closed 正確）、App 公開設定與推播。
