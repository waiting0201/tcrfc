# 23 — 後台欄位 → 前台串接稽核（2026-10-06）

> **性質**：執行層的稽核紀錄，不是規格。規格以四份規劃書為準。
> **方法**：靜態讀碼，六組並行（`qa-test-engineer`），逐欄追「後台畫面 → 後台 API 寫入 → 資料表 → 公開 API 輸出 → 前台顯示」，並反向找「前台寫死、後台改不到」。**未實機驗收**。
> **範圍**：第一次（A–G，2026-10-06）是 `apps/admin` 全部模組對 `apps/web`（主站＋藍鯨），慈善獨立平台不在範圍；**第二次（H，2026-11-13）納入慈善平台與行動 App**。金流、發票、寄信的假實作屬已知（`B-10`／`B-16`）不列。
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
- 仍待：B-5（多語系設定）、B-19（介面字串）、C-5（fallback 寫死內容，屬過渡設計）、C-6（商店固定文案）、F 類；另 `homeTeamCodes` 未接（首頁近期賽事為寫死結構）、合作學校資料來源、職員分組英文名稱待對照 `docs/06`、積分榜公開 DTO 補 `isFallbackLocale`、榮譽對話框改逐欄錯誤。

**2026-10-07 第四輪修正（殘項收尾）**：
- **B-19** ✅：規劃書 I4 定義為「按鈕、表單標籤、提示訊息、錯誤訊息」的雙語對照，不要求全站字串代號化；七類表單共用送出訊息改讀 `useUiStrings().t()`（新增 `form.error_generic`／`form.captcha_required`／`form.captcha_failed`，後台未建立時沿用原文）。
- **C-6** ✅ 檢查後不需修改：配送三方式與發票四類型為規格固定值（主站 §8.3），「主場賽事日」兩俱樂部同一條規格；收款方與發票抬頭已依 `collectingSubjectName` 動態顯示。
- **B-17**：提案下載經 `Features/Proposals` 寫入 `form_code='proposal_download'`，收件匣分頁有資料，**非重複** ✅；捐助洽詢 ✅ **使用者 2026-10-09 拍板：整個拿掉**（規劃書 v3.25 已刪 G2 分頁與 `Enquiry` 涵蓋，記錄在 `docs/15`），**已實作（2026-10-10）**：`FormCatalog`／種子／正式庫參照資料／測試／後台 `types/forms.ts` 與表單清單說明全數移除（表單 9 → 8 種），收縮遷移 `db/migrations/20261010_donation-enquiry-drop_2-contract.sql`（新版 API 上線後才套用）。
- **B-5**：規劃書 G-01 已固定繁中／英文、回退繁中，後台側欄已隱藏「多語系」→ 建議以「規格已固定、不接」結案。**C-5**：過渡設計，待客戶內容到位、後台建立第一筆後再移除寫死版。
- 零星項 ✅：首頁近期賽事接 `homeTeamCodes`；職員分組英文名登錄 `docs/06`（初稿待確認）；榮譽對話框逐欄錯誤；積分榜 `StandingRowDto.isFallbackLocale`（後端＋前台）。
- **F 類**：轉址正規化（有／無結尾斜線視為同一網址）＋儲存時迴圈偵測 ✅；賽程多隊賽事重複列與計數 ✅（`MatchesRepository`、`CalendarRepository` 的 `COUNT(DISTINCT)`）；廣告備援連結放行 `tcrfc://` ✅（App 規劃書 §2.3、§7）；球迷活動與媒體封面雙語 Alt ✅（`cover_alt`，展開遷移 `ClubCoverAltExpand`）；慈善計畫詳情夥伴／贊助商只顯示期間內者 ✅ **已拍板（2026-10-09，規劃書 v3.25 3.11 11.2），已實作（2026-10-10）**：夥伴看 `start_on`／`end_on`、贊助商看 `contract_start_on`／`contract_end_on`，與 `PartnersRepository` 公開列表共用 `Common/PublicPeriodFilter`；報名隱私同意留存 ✅ **已拍板（同日，v3.25 5.1 `Registration`／`Enquiry`，App v3.18 連動），後端已實作（2026-10-10）**：`registrations`／`enquiries` 新增 `privacy_consented_at`、`privacy_policy_version`（伺服器寫入；請求須帶 `privacyConsent: true`，課程與試訓報名沒勾 400）；版本由 `settings.legal.privacy_policy_version` 維護（後台全域設定 `privacyPolicyVersion`）。**前端已接（2026-10-10）**：`apps/web` 的 `ProgramRegistration.vue`、`TrialSchedule.vue` 請求本文送 `privacyConsent: true`（未勾先擋，並補一個 JS 防線）；後台報名／試訓名單／收件匣明細顯示唯讀 `privacyConsentedAt`（台灣時間）與 `privacyPolicyVersion`（元件 `PrivacyConsentCard.vue`），全域設定頁加版本編號欄。尚未做瀏覽器實機驗收。
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
| ✅ B-17 | 表單 | `proposal_download`、`donation_enquiry` 兩張表前台無入口，收件匣分頁永遠空（提案下載走 `Features/Proposals`，未確認是否重複） | — |
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

- 報名的隱私同意只勾選不留存（`privacyConsent` 未送出）——已於 2026-10-10 補上（後端留存、前端送出、後台顯示）。
- 轉址 `fromPath` 不正規化（結尾斜線），`/zh/about` 打不到 `/zh/about/`；無迴圈偵測。
- 賽程列表多隊賽事 JOIN `match_teams` 無 DISTINCT，可能重複列。
- 廣告版位「備援連結」UI 說可填 App 連結，API 只收 https（`AdminAdSlotsRepository.cs:158`）。
- 慈善計畫詳情的夥伴／贊助商不濾合作期間。
- 孤兒頁與 Schema 完整度報表以資料庫為準，但前台多數頁不從資料庫渲染，報表失真。
- 只掛在自建活動的場地不出現在 `join/location`；課程無排序欄位；`programType` 可清空導致課程不出現在任何固定頁。
- 球迷活動、媒體封面沒有替代文字欄位（§4.0 圖片欄位組要求雙語 Alt）。

## G. 確認通過的主幹（不需動）

選單、隱私／Cookie 政策、維護模式、llms.txt／robots.txt／AI 爬蟲（兩站各自一份）、轉址套用、FAQ（含嵌入槽）、球迷活動、漫畫、媒體專區下載、夥伴／贊助商／贊助方案／提案下載、慈善計畫／事蹟／指標／CTA、商品與規格、商店設定政策、會籍方案與權益、特約店家、球衣、課程梯次、試訓、報名（前台送出欄位後台都看得到）、場地、里程碑、賽程比分、積分榜、球員與職員基本欄位（肖像同意 fail-closed 正確）、App 公開設定與推播。

---

## H. 2026-11-13 第二次稽核（全平台）

> **起因**：E-357（後台存中文、讀取端認代碼）之後，使用者要求全面稽核「後台改得到、前台卻讀另一份」的同類問題。
> **方法**：靜態讀碼，五組並行（內容類、球隊賽事類、會員商務類、行動 App、慈善平台），兩個方向都掃。**未實機驗收**。
> **範圍比第一次大**：這次納入慈善獨立平台（`apps/admin-charity`／`apps/web-charity`）與行動 App（`mobile/ios`、`mobile/android`）。
> **只列第一次沒涵蓋或之後新增的**；A–G 已結案項目不重報。標 🔍 的是主 session 另外開檔抽驗過的。
> **類型**：A 讀另一份來源｜B 後台無資料時顯示後台看不到的靜態內容（E-291 類）｜C 值域不一致（E-357 類）｜D 同一事實兩個來源｜E 死欄位｜F 快取未失效。
> **修掉一項就在該列標 ✅ 並寫日期。**

### H-1 官網（`apps/web`，主站＋藍鯨）

| # | 類型 | 嚴重度 | 問題 | 證據 | 影響 |
|---|---|---|---|---|---|
| ✅ W-1 🔍 | E／C | 高 | 賽事狀態設為延賽／取消／進行中後，行事曆頁的賽程分頁只收未開始、賽果分頁只收已結束，**這三種狀態的賽事兩個分頁都不顯示**（深連結除外） | `app/pages/zh/schedule.vue:659`（`statusOk`）；後台五值 `apps/admin/src/types/match.ts:20` | 兩站、中英<br>✅ **2026-11-13 已修**：規格未定義各狀態歸哪個分頁（規劃書 3.13 只定義標記）；採用：進行中與延賽歸賽程，取消依比賽日（今天或未來歸賽程、已過歸賽果）。`matchTabOf`（`app/utils/schedule.ts`） |
| W-2 🔍 | B | 高 | 首頁 Hero 迷你新聞卡：磐石沒有已發布新聞時改放 2 張寫死新聞（含後台不存在的賽果「3-0 銘傳大學」），沒有任何開關管得到 | `app/pages/zh/index.vue:662,677-696` | 磐石、中英 |
| W-3 🔍 | E | 高 | 表單設計器可改「題目文字」與選項英文，前台 7 張表單的標籤與選項全寫死，只讀 `captchaEnabled`／`redirectPath`。A-3 拍板鎖住欄位結構後，**題目文字成了死欄位**（只影響收件匣與通知信） | 後台 `views/forms/FormEditView.vue:469,566`；前台 `app/composables/useFormSubmit.ts:47-52` | 兩站、中英 |
| ✅ W-4 🔍 | F | 中高 | 後台報名管理（建立、改狀態、轉梯次、候補轉正）會改梯次已報名人數與額滿狀態，但 `AdminRegistrations` 整個目錄沒有快取失效；公開課程頁走 `programs` 快取 | `Features/AdminRegistrations/AdminRegistrationsRepository.cs`（無 Invalidate）；對照 `Features/Programs/ProgramsRepository.cs:377` | 兩站<br>✅ **2026-11-13 已修**：四條寫入路徑於 commit 後失效 `programs` |
| W-5 🔍 | B／D | 中高 | 藍鯨兒童足球訓練的班別、年齡、費用與守門員班資料寫死在文案檔（中英各一份），後台課程與梯次改費用不會變 | `shared/utils/club-copy.ts:1690-1696,1788-1796`；`club-copy-en-acad.ts:566-577` | 藍鯨、中英 |
| ✅ W-6 🔍 | C | 中高 | 球員「位置」後台是自由文字（提示寫「門將、中場」），英文頁只認 `GK／DF／MF／FW`，填中文時英文頁顯示中文。**E-357 同形，尚未修** | 後台 `views/teams/PlayerEditView.vue:358`；`shared/utils/club-copy-en-club.ts:133-141` | 兩站英文頁<br>✅ **2026-11-13 已修**：後台改下拉存 `GK／DF／MF／FW`；API `PlayerPosition` 寫入與讀出都正規化（認中文與英文全名，認不得的原樣保留）；官網中英標籤經 `normalizePlayerPosition`，英文頁不輸出中文 |
| W-7 🔍 | E | 中 | 頁面管理的「不列入網站地圖」開關沒作用：sitemap 的固定頁網址不看這個欄位，只有新聞文章會排除 | 後台 `views/pages/PageEditView.vue:670`；`Features/Seo/SeoRepository.cs:144-175`；`server/utils/sitemap-urls.ts:110-131` | 兩站 |
| ✅ W-8 🔍 | B | 中 | 夏令營與冬令營頁的「早鳥價」不看 `earlyBirdUntil`，過期仍顯示早鳥價且不顯示正價；同梯次在報名元件的價格可能不同 | `pages/zh/programs/summer-camp/index.vue:152-155`、`winter-camp/index.vue:124-127`；對照 `components/ProgramRegistration.vue:83` | 磐石、中英<br>✅ **2026-11-13 已修**：營隊頁與報名元件共用 `isEarlyBirdActive`，日期用台灣時區 |
| W-9 | D | 中 | 首頁「四大核心價值」說明讀前台常數，不讀 2.3 足球理念頁後台可編的 `coreValues`；同一事實三個來源 | `Features/AdminPages/PageTemplates.cs`；`shared/utils/core-values.ts`；`Features/Home/CoreValueCatalog.cs:15-18` | 磐石、中英 |
| W-10 | B | 中 | 里程碑頁：磐石後台里程碑為 0 筆或全隱藏時，回到寫死的 2024–2026 時間軸（C-5 同類，當時未列） | `pages/zh/about/milestones.vue:52-57` | 磐石、中英 |
| W-11 | D | 中 | 俱樂部數字寫死在文案檔：「28 名註冊球員」「21 場企甲例行賽」「藍鯨五度奪冠（2017…2023）」，不跟球員、賽程、榮譽資料連動 | `shared/utils/club-copy.ts:284,302,939-968,1863-1883` | 兩站 |
| W-12 | D／A | 中 | 行事曆頁聯賽名稱取自站台事實 `site.league_name`，盃賽與友誼賽的頁首副標與 .ics 標題也套同一個聯賽名 | `pages/zh/schedule.vue:761,778-779,1038` | 兩站、中英 |
| W-13 | E | 中 | 賽事系列的英文名稱 API 已依語系回傳，英文頁改顯示類型標籤（League／Cup），後台填的英文名沒用到；賽程列表卡中文也不顯示賽事名稱 | `pages/zh/index.vue:295`、`club/first-team/index.vue:354`、`schedule.vue:1341` | 兩站 |
| W-14 | D | 中 | 梯隊清單三個來源：球隊主檔、站台設定 `site.squad_codes`、行事曆設定。只改一處，另兩處不會跟著變 | `academy/teams.vue:45-50`、`index.vue:726`、`academy/coaches.vue:44-47`、`schedule.vue:340-360` | 兩站 |
| W-15 | A／E | 中 | 跨梯隊賽事（後台可複選多隊）公開端只掛在排序最前的一隊，其他參賽隊的分頁看不到 | `Features/Schedule/MatchesRepository.cs`（`SELECT TOP 1 … ORDER BY sort_order`）；`schedule.vue:657-660` | 兩站 |
| W-16 | B | 中 | 一線隊頁：後台沒設主視覺時磐石顯示寫死合影照，獎盃照永遠寫死；名單備註寫死「2026/27 賽季」（兩站兩語都顯示） | `club/first-team/index.vue:213-216,264,470-490` | 兩站 |
| W-17 | B | 中 | 藍鯨合作學校：後台沒有「教育夥伴」類型的夥伴時顯示寫死 5 所學校；前台以字面值 `'教育夥伴'` 查詢，後台類型可自由輸入 | `programs/school-community/index.vue:28-48`；`views/business/PartnerEditView.vue:240-241` | 藍鯨 |
| W-18 | B | 中 | 贊助洽詢表單「感興趣的贊助方案」9 個勾選項寫死，與後台方案清單無關（方案卡的靜態後備屬 C-5，已知） | `join/partnership/index.vue:56-59,225-240` | 兩站 |
| W-19 | B | 中 | 場地頁後台沒有場地時磐石顯示寫死的占位卡；學院隊伍頁後台沒有學院球隊時用 `squadCodes` 產生空殼分頁 | `join/location/index.vue:109-126`；`academy/teams.vue:45-50` | 磐石／兩站 |
| W-20 | E | 中 | 球員「加入日期」、教練在各隊的角色 `roleCode`、賽事系列的 `compType`／`organizer` 後台可填，公開端不輸出或前台不用（後兩項 B-11／B-12 當時只修了部分） | `Features/Players/PlayerDto.cs`、`Features/Staff/StaffDto.cs`、`views/teams/CompetitionEditView.vue:251` | 全站 |
| W-21 | D | 中低 | 賽事同時有場地主檔與場地文字，選了主檔後文字欄仍可編輯但被忽略，後台沒有提示（對手欄有提示）；公開場地清單含任何被賽事引用的場地（含客場） | `views/teams/MatchEditView.vue:496-503`；`MatchesRepository.cs:539-542`；`SiteSettingsRepository.cs:172-177` | 兩站 |
| W-22 | A | 中低 | 地圖連結用場地名稱搜尋，沒用場地主檔的座標或地址；SportsEvent JSON-LD 的地址靠名稱完全相等比對，英文頁比不到 | `utils/schedule.ts`（`venueMapUrl`）；`shared/utils/schema-batch2.ts` | 全站 |
| W-23 | B | 低中 | 球迷會頁活動回顧：後台沒有已結束活動時磐石顯示 4 張寫死照片 | `culture/fan-club/index.vue:30-31,166-181` | 磐石 |
| W-24 | C／D | 低 | 前台各表單頁自己維護「頁面選項 → 後端選項值」對照表，目前與後端一致但沒有自動檢查，改種子就會變成送出被 400 | `join/academy/index.vue:62-72`；`Features/Forms/FormsRepository.cs:331-346` | 兩站 |
| W-25 | 其他 | 低 | 行事曆空狀態文案「待客戶提供各梯隊賽程表後將更新於本頁」是內部口吻；新聞分類頁送 `pageSize: 200` 被後端靜默截成 100；新聞上下架不失效 `seo-sitemap-entries`（上限 5 分鐘）；聯賽英文簡稱無消費者（D 段已列未修）；榮譽只有一線隊頁顯示；一線隊代碼 `D1`／`BW1` 多處寫死；「我們的團隊」以寫死姓名清單排序 | `schedule.vue:792`；`news/club.vue:16`；`AdminArticlesRepository.cs:35-36`；`useSiteFacts.ts:105-109`；`shared/utils/club.ts:78-79`；`about/our-people.vue` | — |
| ✅ W-26 🔍 | F | 高 | 場地改名或改地址只失效 `site-facts`；賽程、行事曆、課程梯次三個公開快取都讀場地名稱，要等過期才更新 | `Features/AdminVenues/AdminVenuesRepository.cs:276-282`；`Schedule/MatchesRepository.cs:434`、`Calendar/CalendarRepository.cs:456`、`Programs/ProgramsRepository.cs:198-203` | 兩站、中英<br>✅ **2026-11-13 已修**：另失效 `schedule`／`calendar`／`programs` |
| ✅ W-27 🔍 | F | 中 | FAQ 分類的新增、改名、停用從不失效 `faq-categories`（`AdminFaqCategoriesRepository` 沒有任何快取呼叫） | `Features/AdminFaqs/AdminFaqCategoriesRepository.cs`；`Faqs/FaqsRepository.cs:19,31` | 兩站、中英<br>✅ **2026-11-13 已修**：失效 `faq-categories` 與各俱樂部的 `faqs`／`faq-detail`／`faq-embed` |
| ✅ W-28 🔍 | F | 中 | 課程快取也讀合作夥伴與教練姓名，但 `AdminPartners` 只失效 `partners`／`charity`、`AdminStaff` 只失效 `staff` | `AdminPartners/AdminPartnersRepository.cs:228-231`；`AdminStaff/AdminStaffRepository.cs:139,221`；`Programs/ProgramsRepository.cs:158-185` | 兩站、中英<br>✅ **2026-11-13 已修**：夥伴與教練寫入後補失效 `programs` |
| ✅ W-29 🔍 | D | 中 | 慈善事蹟紀錄列表不過濾所屬計畫的狀態，**草稿計畫的事蹟與計畫名稱會對外顯示**；同檔的影響力指標有過濾 | `Features/CharityImpact/CharityRepository.cs:198-205`（對照 `:293`） | 磐石、中英<br>✅ **2026-11-13 已修**：列表、count、年份清單與影響力彙總的事蹟件數、地區、參與團體一併過濾 |
| W-30 | B／D | 中 | 後台只改中文事實（聯賽名、主場、地址等）而沒填英文時，英文頁、英文 JSON-LD 與 `llms-en.txt` 退回前台靜態快照，不跟後台 | `app/composables/useSiteFacts.ts:87-126`；`server/routes/llms-en.txt.ts:65-70` | 兩站英文 |
| W-31 | B | 中 | 頁尾簡介、社群連結、聯絡 Email 後台清空後退回文案檔寫死值，營運人員無法讓它消失（C-2 的過渡值，沒有結案機制） | `shared/utils/club-copy.ts:208-235`；`useClubIdentity.ts` | 兩站、中英 |
| W-32 | C／B | 低 | 特約店家「適用層級」標籤：API 固定回「限尊尚層級」，藍鯨前台篩選顯示「限高階層級」；類別與地區沒有英文欄位；球衣頁說明寫死「例如尊尚會員」 | `AdminPartnerStoresRepository.cs:386-387`；`perks/index.vue:94`；`components/member/MemberJerseys.vue:87-88` | 藍鯨為主 |
| W-33 | 其他 | 低 | 比賽報導分類頁的磐石 SEO 描述寫死「共 50 篇」；球員故事空狀態寫死舊官網網址；商店篩選選單只取前 60 件商品；`llms-en.txt` 藍鯨定位句寫死「U15 and U12」；已退訂 Email 再訂閱時前台顯示成功但實際沒有重新訂閱（文件化設計，訊息會誤導） | `news/match.vue:35`；`club/player-stories/index.vue:118-119`；`shop/index.vue:70-84`；`llms-en.txt.ts:89-93`；`Newsletter/NewsletterRepository.cs:50-54` | — |

### H-2 慈善捐款平台（`apps/admin-charity` → `apps/web-charity`，含主站慈善頁）

| # | 類型 | 嚴重度 | 問題 | 證據 | 影響 |
|---|---|---|---|---|---|
| K-1 🔍 | E | 高 | 「感謝語樣板」後台可存、API 有輸出，**結果頁沒渲染**（標題寫死），系統信也走另一套樣板 | `admin-charity/src/components/SettingsCopyPanel.vue:28`；`CharityPlatform/Public/CharityPublicCatalog.cs:180,202`；`web-charity/app/pages/[lang]/result/[orderNo].vue:250` | 中英 |
| K-2 🔍 | D | 高 | 公益團體與計畫的唯讀複本（`charity_refs`／`charity_program_refs`）只有種子 SQL 會寫入，沒有同步或匯入入口；主站的表也沒有可對照的鍵。`docs/16` 寫的「CSV 匯入或唯讀 API 拉取」沒有實作 | `CharityPlatform/Admin/CharityProjectsAdminService.cs:348-354`；`db/seed/generate-charity-seed-sql.py:387-406`；`docs/16-charity-schema.md:509` | 中英 |
| K-3 | E | 中 | 公益團體與計畫名稱的快照只有一個欄位，英文頁永遠顯示中文且不出現語系提示 | `CharityPlatform/Data/Entities/CharityRef.cs`；`Public/CharityPublicCatalog.cs:151-153` | 英文 |
| K-4 | B | 中 | 首頁說明後台沒填時顯示寫死文案（款項用途的實質宣稱），後台看不到 | `web-charity/app/pages/[lang]/index.vue:14`；`app/utils/i18n.ts`（`home.introFallback`） | 中英 |
| K-5 | D | 中 | 退款政策兩份文字：後台可改的「捐款須知」只在 `/terms/`，項目頁 FAQ 的「不受理退款」寫死 | `app/utils/i18n.ts`（`project.faqRefundA`）；`pages/[lang]/p/[projectSlug].vue:92-95` | 中英 |
| K-6 | C | 中 | 主站後台「球迷參與文案」只驗長度；前台在文案不含協會全名時整段丟掉改用固定文案，後台提示沒講英文也有這條 | `Features/AdminCharity/AdminCharitySettingsRepository.cs:67-68`；`apps/web/app/pages/zh/charity/index.vue:12-16` | 主站、中英 |
| K-7 | E | 低中 | 「捐款須知」整段被放進標題為「關於退款」的紅框，發票規則與款項用途也被標成退款 | `web-charity/app/pages/[lang]/terms/index.vue:31-37` | 中英 |
| K-8 | D | 低中 | 協會英文名兩個答案：慈善站因未確認而寫 `the Association`，主站已寫死全名 | `web-charity/app/utils/i18n.ts`；`apps/web/shared/utils/charity.ts:15` | 英文 |
| K-9 | B／D | 低 | 信任區塊的統編與勸募許可字號寫死「洽詢中」，取得後沒有後台欄位可改；主站慈善首頁在 `donationUrl` 為空時向訪客顯示開發備註；頁尾聯絡方式沒有連結 | `web-charity/app/components/TrustSection.vue:21-34`；`apps/web/app/pages/zh/charity/index.vue:186-187`；`components/SiteFooter.vue:21` | 中英 |
| K-10 | D／E | 低 | 項目金額選項只在項目自設上下限時才驗範圍；只填英文說明沒填英文名時以中文名頂替，英文頁不出現語系提示 | `CharityProjectsAdminService.cs:226-229,397-444`；`views/projects/ProjectEditView.vue:65-66` | 中英 |

### H-3 行動 App（`mobile/ios`、`mobile/android`）

| # | 類型 | 嚴重度 | 問題 | 證據 | 影響 |
|---|---|---|---|---|---|
| ✅ M-1 🔍 | A | 高 | API 對商店入口回相對網址 `/zh/shop/`，Android `openWeb` 只放行 http／https，**首頁與「更多」的商店入口點了沒反應**。iOS 有把相對路徑接上俱樂部網域 | `Features/AppPublic/AppLayoutReader.cs:37`；Android `ui/Intents.kt:14-18`；iOS `App/AppContainer.swift:22` | Android、兩隊<br>✅ **2026-11-13 已修**：Android 新增統一解析入口 `ClubWeb.resolve`，相對路徑接目前俱樂部網域 |
| ✅ M-2 🔍 | A／C | 高 | 種子的課程深連結是樣板 `tcrfc://program/{slug}`，Android 解析器把 `{slug}` 當真值，**課程快捷入口開出「找不到課程」**。iOS 有擋含 `{` 的樣板 | `db/prod/club-content-seed.sql:9932`；Android `core/DeepLinks.kt:112-130`；iOS `Domain/QuickEntries.swift:9-14` | Android、兩隊<br>✅ **2026-11-13 已修**：Android 快捷入口與 iOS 同規則；解析器拒收含大括號的參數 |
| M-3 | A／C | 中 | 後台推播「點擊後前往」、公告條連結的選單給的是樣板（`tcrfc://match/{id}` 等），API 只驗 `tcrfc://` 開頭；iOS 只有快捷入口擋樣板，公告條與通知中心沒擋，Android 全都沒擋，點了開出不存在的詳情頁 | `apps/admin/src/views/app/PushEditView.vue:202-204`；`AdminAppLayoutRepository.cs:269-286`；iOS `Features/Home/HomeView.swift:77`、`Notifications/NotificationCenterView.swift:63`；Android `domain/HomeLogic.kt:42-46` | 兩平台<br>🔄 **2026-11-13 App 端已擋**：兩平台解析器拒收含大括號的參數，iOS 公告條連結不可用時只顯示文字。**後台選單仍給樣板、API 仍不驗**，未修 |
| M-4 🔍 | E | 中 | 功能開關 `map_enabled`、`biometric_unlock_enabled`、`payment_mode` 後台可設，兩個 App 都不讀（只讀 `ads_enabled`）；`payment_mode` 是三態字串但 `/app/config` 只輸出布林 | `views/app/parts/FlagTab.vue:33-38`；`Features/AppPublic/AppConfigComposer.cs:34-44` | 兩平台 |
| M-5 🔍 | E | 高 | 首頁區塊的「名稱」與「點擊後前往」後台可改（頁面說明也這樣寫），**兩平台都只取區塊代碼**，標題用字串資源、「查看全部」寫死跳分頁 | `views/app/parts/LayoutItemTab.vue:146,195`；`AppLayoutReader.cs:40-46`；iOS `Features/Home/HomeView.swift:16,152,190`；Android `ui/home/HomeScreen.kt:101-120` | 兩平台 |
| M-6 | E | 中 | 廣告素材的標題、按鈕文案、影片後台可填，兩平台不顯示（`docs/19` 已列延後，但後台欄位仍開著） | `views/ads/parts/CreativeFormDialog.vue:100-107`；Android `AdSlot.kt`；iOS `AdSlot.swift:235` | 兩平台 |
| ✅ M-7 | D | 中 | Android「查看方案」永遠開主站網域，沒套目前俱樂部的網域；iOS 有 | Android `ui/AppRoot.kt:276`；iOS `App/AppContainer.swift:38` | Android、藍鯨<br>✅ **2026-11-13 已修**：「查看方案」改走同一個解析入口 |
| M-8 | B | 低 | 後台把首頁九個區塊全關，兩平台都退回預設全開（API 只回啟用項目，空清單與載入失敗分不出來） | iOS `HomeView.swift:15-18`；Android `ui/home/HomeViewModel.kt:136`；`AppLayoutReader.cs:34` | 兩平台 |
| M-9 | A／D | 低 | 後台可新增廣告版位代碼但 App 只認四個；後台「深連結對照」的 `web_url`／`requiresLogin` App 不讀；快捷入口圖示詞彙兩平台不一致（後台是自由文字）；「更多」的種子標籤與 App 資源名稱不同；特約店家類別與地區沒有英文欄位 | `adminAds.ts:55-60`；`AppLayoutReader.cs`；Android `HomeScreen.kt`（`QuickEntryIcons`）；iOS `QuickEntries.swift:25-36` | 兩平台 |
| ✅ M-10 🔍 | F | 高 | iOS 的 App 排版（首頁區塊、公告條、快捷入口、更多項目）只在冷啟動與切語系時重抓，回前景與下拉都不抓。**後台新增或下架公告條，iOS 要殺掉重開才看得到** | iOS `App/AppContainer.swift:204,230`（`refreshLayout` 僅有的兩個呼叫端）；Android `ui/home/HomeViewModel.kt:115-117`（下拉會重抓） | iOS<br>✅ **2026-11-13 已修**：回前景與首頁下拉重抓，以 ETag 條件式請求，失敗保留現有排版 |
| ✅ M-11 | C | 中 | 球員「位置」後台自由文字，兩個 App 只認 `GK／DF／MF／FW`：填「門將」被歸到「其他」群組，Android 詳情頁還把它顯示成「其他」（與 `W-6` 同源） | `views/teams/PlayerEditView.vue:357-358`；iOS `Features/More/RosterView.swift:25-32`；Android `domain/Domain.kt:276-283`、`ui/roster/RosterScreens.kt:290` | 兩平台<br>✅ **2026-11-13 已修**：由 API 讀出正規化解決，兩個 App 未改 |
| M-12 | F | 中 | Android 的俱樂部、球隊、賽程、排版資料每個行程只收集一次，回前景不依時效重抓；單場賽事詳情用 6 小時時效且無強制重抓，進行中或剛結束的比賽看不到更新。iOS 回前景會更新、詳情每次打網路 | Android `data/AppData.kt:37-47`、`ui/AppRoot.kt:114`、`data/Repositories.kt:121-122` | Android |
| M-13 | D／F | 中 | `shared/cache-policy.json` 沒列的資料（方案、權益、夥伴、贊助、FAQ、賽事系列）兩平台行為不同：iOS 每次重新驗證，Android 每個行程只驗一次，註解卻寫「與 iOS 對齊」 | iOS `Domain/ContentRepository.swift:31-34`；Android `core/Cache.kt:136-157`、`core/Policy.kt:60-64` | 兩平台不一致 |
| ✅ M-14 🔍 | D | 中 | Android 早鳥截止後仍顯示早鳥價（`earlyBirdActive` 寫好了但沒人呼叫）；iOS 有判斷。與官網 `W-8` 同類 | Android `ui/programs/ProgramsScreens.kt:226-231`、`domain/Programs.kt:68`；iOS `Domain/ProgramsDisplay.swift:9-14` | Android<br>✅ **2026-11-13 已修**：接上 `earlyBirdActive`，日期用台灣時區 |
| M-15 | D | 低 | 兩平台歸類不一致：取消賽事 iOS 留在賽程、Android 歸賽果；「下一場」iOS 不排除延賽；未知狀態處理不同；進球類型 `other`、未知梯次狀態、未知慣用腳代碼的顯示不同；政策頁只有 iOS 可離線。Android 字串寫死「限尊尚層級會員」 | iOS `Domain/ScheduleLogic.swift:125-183`；Android `domain/Domain.kt:236-253`、`res/values/strings.xml:218` | 兩平台 |

### H-3a 修正進度

**2026-11-13 第一批（不涉及規格）**：`W-1`、`W-4`、`W-8`、`W-26`、`W-27`、`W-28`、`W-29`、`M-1`、`M-2`、`M-7`、`M-10`、`M-14` 已修，`M-3` 的 App 端已擋。
- 測試：API 新增 3 項（`AuditH1CacheInvalidationTests`、`AuditH1CharityRecordsDraftProgramTests`），排除慈善平台的全套 1420 項通過、失敗 0（跑在新建的 `tcrfc_club_test`，未碰 `tcrfc_club`；慈善平台測試未跑）；官網 `scripts/check-schedule-tab.mjs` 9 項通過、lint 0 錯誤；iOS 單元測試 349 項通過；Android JVM 單元測試 313 項通過。
- **全部未實機驗收**：官網未對兩站實看；iOS 未對真 API 驗 `/app/layout` 的 304（帶裝置識別的回應是 `private, no-store`，是否仍帶 ETag 待確認）；Android 未跑模擬器。
- 修正時發現、未處理：Android `lintDevDebug` 有 5 個既有的 `StateFlowValueCalledInComposition` 錯誤（`AdSlot.kt:65`、`AuthScreens.kt:60,151`、`PolicyScreen.kt:85,86`，皆非本批改動的行）；公告條只有一個連結欄位、沒有官網備援網址；兩隊共同（`club_id` 為空）的教練或夥伴寫入時只失效呼叫端俱樂部的快取；延賽賽事匯出行事曆不標示原定時間。

**2026-11-13 第二批（值域收斂，比照 E-357 慣用腳）**：`W-6`、`M-11` 已修。值域 `GK／DF／MF／FW` 寫進 `docs/12a` 與 `docs/14`，未動規劃書；依規劃書不加位置分組或篩選。正式內容種子的 28 位球員位置原本就是代碼，無需遷移。官網賽程頁的中文標籤由「守門員」改為「門將」（與後台一致）；首頁球員橫幅與新聞相關球員原本直接印代碼，改為顯示標籤。測試：API 相關 83 項通過，官網 `check-player-position.mjs` 6 項通過，後台與官網 lint、build 通過。**未實機驗收。**

### H-4 涵蓋範圍與仍未看的部分

**兩輪合計已涵蓋**：`apps/admin` 全部模組對 `apps/web`、慈善平台、行動 App 兩平台；後台寫入端對公開快取的失效已全面對照（缺口為 `W-4`、`W-26`–`W-28` 與新聞對 `seo-sitemap-entries`）。

**確認沒有問題的重點**：商店庫存、購物車、訂單狀態沒有讀快取；App 會員卡與會籍狀態每次直打 API、不經 HTTP 快取；球迷活動的報名時窗與名額由 API 計算；搜尋直連資料庫且狀態過濾與公開列表一致；App 賽程取整季全狀態，延賽與取消不會消失（官網的 `W-1` 在 App 沒有）；新聞區塊型別兩平台全認得；App 字串資源沒有寫死的價格、電話、網址、場地。

**仍未逐欄看的**：會員中心的訂單狀態標籤與停權會員的卡片判定；商店結帳頁與訂單頁；`club-copy*.ts` 4393 行的敘述性事實（只掃了數字與聯絡方式）；App 的抽獎、球衣登記狀態值域、試訓場次顯示欄位、英文字串資源；iOS 圖片載入器在後台換圖後是否看得到新圖；慈善後台的捐款、發票、結算、報表、對帳畫面。
