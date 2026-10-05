# 06 — 慣例與術語

> 執行層文件。術語表源自規劃書；設計 tokens 的實作基準是 [`../site/src/assets/css/tcrfc.css`](../site/src/assets/css/tcrfc.css) 的 `:root`。
> **品牌色的唯一真實來源是 [`../reference/TCR_logo_CMYK.ai`](../reference/TCR_logo_CMYK.ai)**，衍生資產與說明見 [`../brand/README.md`](../brand/README.md)。

---

## 1. 術語對照表

寫任何文案、命名、程式碼前先對一次，避免同一件事有三種叫法。

| 概念 | 中文 | English | 代號／識別 | 備註 |
|---|---|---|---|---|
| 俱樂部 | 台中磐石足球俱樂部 | Taichung Rock FC | TCRFC | 2024 創立。中文簡稱一律「**台中磐石**」，不單用「磐石」 |
| 品牌主張 | 在地扎根 · 放眼世界 | LOCAL ROOTS. GLOBAL PATHWAYS. | | |
| 俱樂部（全稱寫法） | — | TAICHUNG ROCK FOOTBALL CLUB | | Brand Deck 頁尾用法；一般情境一律用 `Taichung Rock FC` |
| 一線隊 | 一線隊 | First Team | **`D1`** | 對外顯示用「一線隊／First Team」，`D1` 僅作代號 |
| **合作球隊**（v3.0） | 台中藍鯨 | （待確認，B-5；英文內文暫放中文名） | **`TCBW`** | 隸屬**臺中市女子足球協會**，2014-04-12 成立。**與台中磐石是不同法人**。中文一律寫「**台中藍鯨**」，不單用「藍鯨」。英文正式全名**待藍鯨確認** |
| **藍鯨一線隊**（v3.0） | 藍鯨一線隊 | Blue Whale First Team | **`BW1`** | **不是第二個 `D1`**——`Team.code` 全站唯一，不得改複合鍵 |
| **藍鯨青年隊**（v3.0） | U15／U12 女子隊 | Youth Teams | `U15`/`U12` | ⚠️ **與磐石學院的 `U15`／`U12` 是不同球隊**，以 `club_id` 區隔 |
| 學院梯隊 | U15／U14／U12 梯隊 | Academy Teams | `U15`/`U14`/`U12` | 日後可增 U18／U10 |
| 學院 | 台中磐石足球學院 | TCRFC Academy | | |
| 女子足球 | 台中藍鯨女子隊 | Women's Football | — | ⚠️ **v3.0 改寫**：藍鯨是本系統的第二個俱樂部，**建立完整球隊資料**（`club_id` 區隔），由**獨立網域的藍鯨官網**呈現。主站 06 只剩一頁入口。**「不建球隊資料」已作廢** |
| **俱樂部（型別）**（v3.0） | 俱樂部 | Club | `Club` | **不是第六種商業對象**——它是內容主體，不計曝光、無金流、無分潤 |
| **會籍**（v3.0） | 會籍 | Membership | `Membership` | **一人每俱樂部一份**。`Member`（會員帳號）是一人一組，兩者不可混用 |
| 課程與活動 | 課程與活動 | Programs | 後台 `P1–P4` | 原編 `D1–D4`，已因撞名改號 |
| 慈善 | 慈善與社會影響 | Charity & Impact | | |
| 球迷會 | 台中磐石球迷會 | Fan Club | `fan_club` | 是會員層級，不是獨立名單 |
| 漫畫 | 台中磐石漫畫 | TCRFC Manga / Comics | | 全部免費公開 |

### 後台介面用語（規劃書 §4.0 後台設計通則，v3.7）

**後台介面說的是前台的語言。** 前台叫什麼，後台就叫什麼；介面上一律日常中文。

| 技術寫法（**只用於規格、程式、稽核**） | 介面寫法（**一般人看得懂**） |
|---|---|
| `slug` | 網址名稱（球員 `players.slug` 在後台稱「網址代稱」） |
| `canonical` | 正規網址 |
| `noindex` | 不讓搜尋引擎收錄 |
| `alt` | 圖片替代文字 |
| `hreflang` | 語言版本對應 |
| `SKU` | 商品規格編號 |
| `token` | 驗證碼／連結金鑰（依情境） |
| `blob`／物件儲存 | （不出現，使用者只看到「上傳圖片」） |
| `Schema`／結構化資料 | 搜尋引擎摘要資料 |
| `club_id` | （不出現，使用者看到的是站台切換器） |
| `original_match_on`／`original_kickoff` | 原定日期／原定時間（僅賽事狀態為「延賽」時顯示） |
| 模組代號 `B1`／`K4`／`S3` | **不顯示**，只用模組名稱 |
| 權限碼 `shop.order.export` | **不顯示**，改中文描述（「匯出訂單名單」） |
| 隊別代號 `D1`／`BW1` | **不顯示**，用「一線隊」「藍鯨一線隊」 |
| `SEO` | 「搜尋與分享設定」（單頁／全站設定畫面標題）；泛稱時用「搜尋最佳化」 |
| `OG`／Open Graph | 「分享圖片」「分享標題」「分享描述」「分享圖片替代文字」（依欄位情境拆開講，不出現 `OG` 這個縮寫） |
| `robots.txt` | 「搜尋引擎收錄規則」 |
| `sitemap` | 「網站地圖」 |
| `canonical`（單頁覆寫欄位） | 「正式網址」 |
| `llms.txt` | 「AI 摘要資料」（S1-12a，後台選單標題；泛稱時可稱「AI 摘要說明檔」） |
| 使用者代理／User-Agent | 「AI 服務名稱」（S1-12b，例如 `GPTBot`／`ClaudeBot` 這類技術字串，介面上不出現「使用者代理」這個詞） |
| 排除路徑（後台自訂，`crawler_extra_exclude_paths`） | 「自訂不開放的頁面路徑」 |
| 強制排除路徑（`GetMandatoryExcludePaths`，程式碼寫死） | 「系統保護的頁面（不可移除）」——說明文字講清楚是為了保護會員資料與未成年學員照片 |
| `JSON-LD` | （不出現，統稱「搜尋引擎摘要資料」，同 `Schema`） |
| `Organization`（結構化資料型別，S1-12c） | 「俱樂部基本資料」 |
| `SportsTeam`（結構化資料型別，S1-12c） | 「球隊基本資料」 |
| `Event`（結構化資料型別，S1-12c） | 「行事曆活動」 |
| `SportsEvent`（結構化資料型別，S1-12c） | 「賽程賽事」 |
| `Person`（結構化資料型別，S1-12c） | 「球員資料」 |
| `Article`（結構化資料型別，S1-12c） | 「新聞與故事文章」 |
| `Course`（結構化資料型別，S1-12c） | 「課程與活動項目」 |
| `BreadcrumbList`（結構化資料型別，S1-12c） | 「頁面路徑導覽」 |
| `FAQPage`（結構化資料型別，S1-12c） | 「常見問題」 |
| `Lead`（E3 提案下載名單，後端稱 Lead） | 「提案下載名單」（分頁標題）、「下載紀錄」（單筆）；動作寫「跟進」，狀態值為新進／處理中／已回覆／已結案／無效 |
| `Activation`（E2 贊助活動） | 「贊助活動」 |
| `is_public`／`isPublic`（B5 影響力數據） | 「公開顯示」（前台是否顯示；金額類預設不公開） |
| `Sponsor`／`Partner`／`Package`（E1／E2） | 「贊助商」／「夥伴」／「贊助方案」 |
| `CTA`（B5 導流設定） | 「按鈕文案」（如「捐款按鈕文案」「企業合作文案」） |
| 圖集／`gallery` | 「活動圖集」「其他活動圖片」（新增、移除、排序立即生效，介面要註明） |
| `Milestone`／`Achievement`（C5） | 「里程碑」／「榮譽」；`isVisible` 寫「顯示在前台時間軸」 |
| `progress`＝`ongoing`／`completed`（B5 慈善計畫） | 「進行中」／「已完成」（由期間自動判斷，與「已發布／草稿」的發布狀態是兩回事） |
| `press_release`／`brand_kit`／`hires_image`（B6） | 「新聞稿」／「品牌識別包」／「高解析圖」 |
| `is_shared`（共同列，`club_id` 為空） | 「兩隊共用」標籤；編輯頁「共用內容（唯讀）」 |
| `reveal`／遮罩（K1 會員名單、K3 球衣收件資訊） | 「顯示完整資料」／「重新遮罩」；列表與詳情預設一律「已遮罩」，顯示完整資料前要二次確認並提醒系統會記錄 |
| `pii.reveal`（權限碼） | **不顯示**；說明文字寫「檢視完整個資」（沒有此權限者，會員搜尋只比對會員編號） |
| `crossClub` | 「顯示我有權限的所有俱樂部的會員」 |
| `tier`：`registered`／`fan_club`（K1／K2） | 「一般會員」／「球迷會員」（付費）；權益表的兩欄稱「一般會員」「付費球迷會員」 |
| `effectiveStatus`（會籍） | 「會籍狀態」：有效／已到期／待確認／已取消（以**到期日**為準，人工狀態欄不直接顯示） |
| `displayStatus`：`unverified`／`suspended`／`deleted` | 「尚未驗證信箱」／「已停用」／「已刪除或已合併」 |
| `signupSource`：`web`／`line`／`admin`／`app` | 「官網註冊」／「LINE 註冊」／「現場入會」／「App 註冊」 |
| `lineBound` | 「已綁定 LINE」／「未綁定 LINE」（**不顯示** LINE 識別碼） |
| QR reissue（K1 會員卡） | 「重新產生 QR Code」（舊的立即失效，要二次確認） |
| `merge`／`duplicates`（K1） | 「合併重複帳號」（併入保留的帳號，**不可逆**，僅系統管理員）／「重複帳號比對」 |
| `purpose`（匯出必填） | 「匯出用途」（≤200 字；系統記錄匯出人員、筆數與用途）；匯出對話框固定用 `ExportPurposeDialog` |
| `size-summary`（K3） | 「尺寸備貨統計」；`ship`／`pickup` ＝「寄送」／「到場領取」；`pending`／`shipped`／`received` ＝「待處理」／「已寄出」／「已領取」 |
| `activate`／renewal（K2） | 「手動開通」「續會」（客服核對款項後開通，**不是金流**；續會＝用下一球季的方案再開通一次） |
| `expire-batch`／`dryRun`（K2） | 「球季末批次到期」／「先試算」 |
| `freeValue`／`paidValue`（K4 權益） | 「一般會員」「付費球迷會員」兩欄（如「✓」「✗」「9 折」） |
| `isShared` 特約店家（K4） | 「兩隊共用」；只有系統管理員能編輯 |
| 座標 `lat`／`lng`（K4） | 「緯度」「經度」——人工確認後儲存；旁邊的輔助按鈕寫「由地址定位」，勾選項寫「儲存時由地址定位」；定位結果 `located`＝「已依地址填入座標，請確認」、`not_found`＝「查無此地址，請手動輸入座標」、`unavailable`＝「定位服務暫時無法使用，請手動輸入座標」 |
| `promote`（P3／P4 報名） | 「遞補」（候補轉已確認，佔名額）；`waitlist` ＝「候補」；`sign-in sheet` ＝「簽到表」 |
| `syncTrials`（L3） | 「試訓是否同步到行事曆」（預設關閉；開關立即套用到該俱樂部所有試訓場次） |
| `conflict`／`tracks`（L1 進階） | 「行程衝突」（同時段同場地或同球隊）／「分軌檢視」（每支球隊一條軌道） |
| `reschedule`／`move`（L1 進階） | 「調整日期」（拖曳或按鈕）；「同時標示為延賽」為可勾選；改期不會自動通知任何人 |
| `feed`／`webcal`／`ics`（L4） | 「行事曆訂閱網址」（「複製一般網址」「複製訂閱連結」）；訂閱數一律標「估計」 |
| 賽事類型／活動類型圖示（L3） | 只能從系統預設圖示挑選，不是上傳圖片；類型是兩隊共用資料，只有系統管理員能改 |
| `isLatest`（F1 漫畫集數） | 「最新一集」——系統依已發布且發布日已到的最大集數自動判定，**不是人工勾選**；閱讀數寫「閱讀數」 |
| 藍鯨不設漫畫（F1） | 切到藍鯨站台時側欄不顯示、頁面只說明「台中藍鯨不設漫畫，這個功能只在台中磐石使用」，不呼叫端點 |
| `registered`／`waitlist`／`attended`／`cancelled`（F2 報名） | 「已報名」／「候補」／「已到場」／「已取消」；額滿自動進候補；「遞補」＝候補轉已報名 |
| `isPaidMembersOnly`（F2） | 「限付費會員報名」 |
| 新聞挑選器（`news/lookup`） | 「挑選報導」（關聯報導、贊助故事、活動回顧共用；可用標題關鍵字搜尋所有文章，不再有「最近 100 篇」限制） |
| `sku`（S1 規格） | 「規格編號」（不可含空白、全站不可重複）；規格顯示「尺寸／顏色」 |
| `status`：`draft`／`published`／`sold_out`（S1 商品） | 「下架（草稿）」／「上架」／「缺貨」——缺貨由庫存自動判定，不能手動設定；`outOfStockBehavior`＝「缺貨時的前台顯示」（顯示為缺貨／從商店隱藏） |
| `active`／`inactive`（S1 規格） | 「販售中」／「停售」 |
| `cost`（S1 成本） | 「成本」——只有持有成本權限的人看得到與修改，其餘不出現這一欄 |
| `stockQty`／`reservedQty`／`availableQty`（S2） | 「庫存量」／「已保留」／「可售量」（可售量＝庫存量－已保留）；`isLowStock`＝「需要補貨」（可售量不高於門檻） |
| `stock_in`／`damage`／`adjust`／`stocktake`（S2 異動） | 「進貨」／「報損」／「調整」／「盤點」（盤點填實際總數，差額由系統算）；`reserve`／`release`／`return_restock` ＝「下單保留」／「釋回保留」／「退貨回補」；每筆異動記數量、原因、經辦人 |
| `sellingClub`／`collectingClub`（S3） | 「販售俱樂部」／「收款主體」（藍鯨的訂單由磐石代收） |
| `settlementStatus`（S3 分帳標記） | 「分帳標記」：待結算／已結算——**人工旗標，不是狀態流程**，系統不計算應付金額、不產生結算單 |
| `home_delivery`／`cvs_pickup`／`onsite_pickup`（S3） | 「宅配」／「超商取貨」／「現場自取」；`linepay`／`onsite` ＝「LINE Pay」／「現場收款」 |
| `isManual`（S3） | 「手動建單」（現場收款、賽事日擺攤、現場補登；付款方式固定現場收款、當下扣庫存） |
| `release-expired`（S3） | 「釋回逾時未付款訂單」（立即取消逾期未付款的訂單並釋回保留庫存；系統另有定時自動處理，Production 預設每 60 秒） |
| `picking-list`／`dispatch-slips`（S4） | 「揀貨單」／「出貨單」（畫面直接列印）；`overdue`（領取）＝「逾期未領」；`arrival-notified` ＝「記錄到店通知」（只記時間，不寄通知） |
| `refund`：`requested`／`approved`／`received`／`processing`／`refunded`／`rejected`（S5） | 「申請中」／「已核准」／「已驗收退回品」／「退款處理中」／「已退款」／「已駁回」；`needsReturn` ＝「需要退回商品」；`restock` ＝「回補庫存」；「執行退款」只有系統管理員能做 |
| `integrationConnected`（S6 憑證） | 「尚未串接，取得商店號後啟用」——金流與電子發票畫面必須明講，不得做成看起來可以真的收款 |
| `sandbox`／`production`（S6） | 「測試環境」／「正式環境」；`channelId`／`channelSecret` ＝「商店識別碼」／「商店密鑰」；憑證欄位「只寫不讀」，儲存後不再顯示，留空＝沿用原本的值 |
| 發票捐贈碼（S6 `donation-codes`） | 「發票捐贈碼」——全系統共用、不分俱樂部 |
| `draw`：`draft`／`roster_locked`／`drawn`／`announced`／`closed`／`voided`（K5） | 「草稿」／「名單已鎖定」／「已抽出」／「已公布」／「已結案」／「已作廢」；系統不抽出，實體抽獎由人工進行，以序號回填 |
| `snapshotAt`／`serialNo`／`rosterHash`（K5） | 「資格基準時間」／「抽獎序號」／「名單雜湊」；名單版本歷程（作廢的舊版保留、不可刪除） |
| `winner`／`backup`／`promote`（K5） | 「中獎」／「備取」／「遞補為中獎」（同一個序號再回填一次）；名單公布後再修改必填「修改原因」 |
| `fulfilment`（K5） | 「獎品發放」：寄送／現場領取；待處理／已寄出／已領取；`overdue` ＝「逾期」（待處理且已過領獎期限） |
| `notice`（K5 蒐集告知） | 「蒐集告知確認」——未確認前不能產生名單 |
| `EDM`（G3 電子報） | 「電子報寄送平台」——官網本身不寄信，寄送由外部平台負責；串接前畫面明講「尚未串接」，「同步名單」只回報尚未串接 |
| 訂閱者 `subscribed`／`unsubscribed`（G3） | 「已訂閱」／「已退訂」；改回訂閱必填「訂閱者本人要求」的說明；刪除＝個資刪除（不留退訂紀錄） |
| `Advertiser`／`AdSlot`／`AdCampaign`（E4–E6） | 「廣告主」／「廣告版位」（App 畫面上放廣告的位置）／「投放檔期」；廣告**兩隊共用、不分俱樂部**（每頁頁首說明） |
| `guaranteed`／`traffic`（E5 目標） | 「曝光保證」／「導流」；`pacing` ＝「投放進度」（超前／正常／落後）；`weight` ＝「投放權重」 |
| 檔期狀態（E5） | 草稿／待審核／已排程／投放中／已暫停／已結束／已結案／已作廢；`pause`＝「緊急暫停」（必填原因）、`void`＝「作廢」（不可逆） |
| 素材審核（E5）`pending`／`approved`／`rejected` | 「待審核」／「已通過」／「已退回」；`impression`／`click` ＝「曝光」／「點擊」；`CTR` ＝「點擊率」；`uniqueDevices` ＝「不重複裝置數」（跨日彙總為「裝置日」，畫面要註明） |
| `contractAmount`「不公開」（E5） | 沒有合約金額檢視權限時顯示「不公開」；不顯示欄位名稱以外的權限說明 |
| `fallback`（E4 備援素材） | 「備援素材」——沒有可投放廣告時顯示、不計曝光，版位永遠不空白 |
| `deep link`／`deepLink`（M2、M3） | 「App 內連結」（`tcrfc://` 開頭，畫面可顯示這個開頭）；`announcement` ＝「公告條」；`quick_entry`／`more_item` ＝「快捷入口」／「『更多』分頁項目」 |
| 版本門檻（M1）`isMinSupported`／`isRecommended` | 「最低支援版本」（低於它強制更新、不能略過）／「建議版本」（可略過、每 7 天再提醒）；設最低支援版本要二次確認「會強制舊版更新」 |
| `maintenance`（M1） | 「維護模式」；開啟必填繁體中文訊息 |
| `edgePublish`（M1／M5） | 「已存檔，但尚未同步到靜態設定檔」——目前恆為尚未串接，不是錯誤 |
| 推播批次狀態（M3） | 草稿／待覆核／已排程／發送中／已發送／部分送出／失敗／已取消；`approve` ＝「覆核」（核可者不能是建立者本人）；`retry` ＝「重送」 |
| `sent`／`delivered`／`opened`（M3） | 「送出」（我方交給推播服務的則數）／「送達」（推播服務已接受，**不等於已到達手機**）／「開啟」（App 回報，不追蹤是誰）；畫面照後端 `statsNote` 原文 |
| 推播傳輸（APNs／FCM）未串接（M3） | 「推播傳輸尚未串接」——核可後批次停在「失敗」、不動任何裝置，串接後可「重送」；畫面不出現 APNs／FCM 字樣 |
| `estimate`（M3） | 「預估人數」（只算推播識別碼有效且已允許推播的裝置）；核可時帶入二次確認 |
| `pushToken`／`deviceInstallId`（M4） | 「推播識別碼」／「裝置識別碼」；清單一律遮罩，「查看完整識別碼」僅系統管理員、二次確認、留紀錄 |
| `pushPermission`（M4） | 「推播權限」：尚未詢問／已允許／已拒絕／暫時允許 |
| `payment_mode`（M5） | 「付款模式」：關閉付款／外開瀏覽器付款／App 內付款；**只能降級**，「App 內付款」選項在目前不是 App 內付款時停用 |
| 憑證列管（M5） | 「金鑰與憑證列管」——**只登記管理資訊，不存金鑰本身**；`rotate` ＝「記錄輪替」；`due_soon`／`overdue` ＝「即將屆期」／「已屆期」 |
| `connection-check`（M5） | 「連線檢查」；`not_configured` ＝「尚未串接」（不是故障）與 `error` ＝「異常」要分色分字 |
| `diagnostics`（M5） | 「診斷回報」：崩潰／異常退出／連線錯誤／啟動耗時／使用者回報；`crashFree` ＝「無崩潰裝置比例（近似）」 |
| `PITR`／`LTR`／`BACPAC`（J3） | 「時間點還原」／「長期備份」／「資料庫匯出檔」——畫面只用日常說法；J3 **沒有稽核查詢**，頁首明講「目前系統不保存操作稽核紀錄」 |

⚠️ **這條約束介面文字，不是資料結構。** 資料表名、欄位名與權限碼維持英文 `snake_case`。

⚠️ **`GA4`／`GTM`／`Meta Pixel`／`LINE Tag`（S1-12 追蹤碼）刻意保留原文，不翻譯**——這四個是
第三方服務的正式產品名稱，管理員要去對應服務的後台（Google Analytics／Google Tag Manager／
Meta／LINE）取得識別碼才填得進來，翻譯成中文說法反而讓人對不起來要去哪裡申請、貼的是哪一組
代碼。這跟「模組代號」「權限碼」這類**只在系統內部有意義**的技術詞不是同一回事，不適用禁用詞規則。

⚠️ **`AI 爬蟲`（S1-12a／S1-12b）刻意保留不翻譯**——這個說法一般人已經看得懂（指讀取網站內容的 AI
程式），介面上會再搭配一句白話說明（例如「像 ChatGPT、Claude 這類服務讀取網站內容的程式」），
不需要造一個更拗口的中文詞取代它。

**後台模組名稱（v3.7 更名的九個）**：首頁編排｜常見問題｜慈善與社會影響｜媒體專區｜賽程與賽果｜搜尋與 AI 能見度｜推播裝置｜App 設定與連線檢查｜商品與規格

---

### 名稱與標誌的硬性規則

1. **中文簡稱一律「台中磐石」。** 站上任何位置都不單獨出現「磐石」——`關於台中磐石`、`台中磐石文化`、`台中磐石足球學院`、`台中磐石球迷會`、`台中磐石漫畫` 皆同。
1b. **藍鯨的中文一律寫「台中藍鯨」**，不單用「藍鯨」（比照磐石的規則）。**藍鯨的標誌、品牌色與英文正式名稱須由藍鯨提供**——**不得自行造標、不得從網頁截圖描摹、不得自行排字**；未到位前對應區塊不顯示，**不得放假圖或空 Logo 格**。
1c. **行動 App 對外名稱是中性的「台中足球 / Taichung Football」**（兩隊共同品牌），**但官網與所有主站文件仍是 TCRFC**——這兩者不衝突，是刻意的分工。
2. **標誌只用 logo 主檔萃取的三種組合**（見 [`../brand/README.md`](../brand/README.md)）：
   - `tcrfc-mark-*.svg` 隊徽
   - `tcrfc-stacked-*.svg` 隊徽＋`TCRFC`（**英文版**）
   - `tcrfc-full-*.svg` 隊徽＋`TCRFC`＋`台中磐石足球俱樂部`（**中文版**）
3. **不得在標誌旁自行排字**（含「台中磐石」文字 wordmark），**不得加註 `SINCE` 或創立年份**。
4. **前台各處用哪一種**：
   - **header 只放隊徽**，不放任何文字（俱樂部名稱由 `.brand-lockup` 的 `aria-label` 提供給輔助技術）
   - **footer 放完整中文版**（`tcrfc-full-white.svg`）
   - 完整中文版縮小後中文會糊掉，任何情境用它都要留足高度（footer 目前 120px）
5. **英文名一律 `Taichung Rock FC`**（全稱 `TAICHUNG ROCK FOOTBALL CLUB`）。舊稿的 `Taichung Cornerstone RFC` 已全數汰換，看到一律視為錯誤。

**五大核心價值**（同時是全站內容標籤）
`Players First 以球員為本`｜`Excellence 追求卓越`｜`Global Pathways 國際發展`｜`Community 社區共好`｜`Integrity 誠信專業`

---

### 1.1 英文用詞對照表（2026-10-05，主站英文版 C-6／S2-13）

> **英文版所有文案（樣板、`club-copy.ts`、種子的 `en` 欄位）一律照本表。** 來源優先序：規劃書英文版（`output/*_EN.md`）→ 本檔 §1 與
> [`14-invariants.md`](14-invariants.md) 既有寫法 → 本表由開發端擬定的初稿（標「初稿」）。**規劃書與 docs 都沒有、又屬專有名詞者標「待客戶確認」，
> 譯文採保守寫法（描述性或用代稱），不自創看似官方的名稱。** 範圍只有主站（`tcrfc`）；藍鯨英文全名卡 B-5，**藍鯨站不適用本表**。
> 寫法：句首以外用 sentence case；標題用 Title Case；`TCRFC` 保留大寫；運動項目稱 football（不寫 soccer）。

| 中文 | English | 來源／狀態 |
|---|---|---|
| 台中磐石足球俱樂部／台中磐石 | Taichung Rock FC（全稱 TAICHUNG ROCK FOOTBALL CLUB，僅頁尾／品牌用） | 規劃書 §1.3、本檔 §1 |
| 俱樂部（泛稱）／本俱樂部 | the club | 初稿 |
| 在地扎根 · 放眼世界 | LOCAL ROOTS. GLOBAL PATHWAYS. | 規劃書 |
| 台中磐石足球學院／學院 | TCRFC Academy／the Academy | 規劃書 §3.4 |
| 一線隊 | First Team | 規劃書 |
| U15／U14／U12 梯隊 | U15／U14／U12 squads（泛稱 age-group teams） | 規劃書 §3.4（Our Teams） |
| 台中藍鯨（出現在主站英文內文時） | **英文句內放中文名**，一律取 `BW_NAME_EN_PENDING`（`club-copy.ts`，值為「台中藍鯨」） | 本檔 §1；**正式英文全名待藍鯨確認（B-5），開發端不得自挑**，`check-bw-en-name.mjs` 擋寫死的 `Taichung Blue Whale`。本表初版（2026-10-05）曾寫「可用 Taichung Blue Whale 作描述性寫法」，與 B-5 牴觸，已更正（E-210） |
| 臺中市女子足球協會 | Taichung Women's Football Association | **待客戶確認**（初稿，直譯） |
| 台灣足球策略發展協會 | Taiwan Football Strategic Development Association | **待客戶確認**（初稿，直譯；慈善平台的收款主體） |
| 企業甲級足球聯賽（企甲） | 保守寫法：「the league」「the top-tier corporate league」；已寫死於 `site-facts.ts` 的 `Enterprise Premier League` 沿用；規劃書英文版縮寫為 TFPL | **正式英文名待客戶確認**；譯文優先用 `facts.league.nameEn`，不得自行拼出 Taiwan Football Premier League 全名 |
| 全國乙級聯賽冠軍 | National Second Division champions | **待客戶確認**（直譯，已用於事實列） |
| 台灣木蘭足球聯賽 | Taiwan Mulan Football League | 規劃書英文版 |
| 西屯足球場 | Xitun Football Field | `site-facts.ts`、種子既有 |
| 賽事行事曆／賽程 | Schedule／Fixtures；賽果 Results；積分榜 Standings（League table） | 規劃書 §3.13 |
| 主場／客場 | Home／Away | 規劃書 |
| 延賽／取消 | Postponed／Cancelled | 初稿 |
| 單元名稱（導覽） | About TCRFC／Football Club／Academy／Programs／Women's Football／News & Stories／Culture／Partners & Sponsors／Join / Contact／Charity & Impact／FAQ／Schedule／Shop／Member Centre | 規劃書 §2、§2.2 |
| 球員發展系統／球員發展機會／國際發展通道／球員故事 | Player Development／Player Opportunities／International Pathways／Player Stories | 規劃書 §3.3 |
| 學院總覽／梯隊／發展路徑／訓練與課程／教練團／學院生活／加入學院 | Academy Overview／Our Teams／Academy Pathway／Training & Curriculum／Coaches／Academy Life／Join the Academy | 規劃書 §3.4 |
| 兒童足球訓練／夏令營／冬令營／專項訓練／校園與社區 | Children's Training／Summer Camp／Winter Camp／Specialist Training／School & Community | 規劃書 §3.5 |
| 我們的夥伴／我們的贊助商／成為夥伴／贊助機會 | Our Partners／Our Sponsors／Become a Partner／Sponsorship Opportunities | 規劃書 §3.9 |
| 贊助簡報下載 | Sponsorship Deck | 規劃書 |
| 加入球隊／學院與兒童訓練報名／營隊報名／國際球員洽詢／合作與贊助洽詢／媒體洽詢／一般聯絡 | Join as a Player／Academy & Children's Training／Camp Registration／International Player Enquiries／Partnership & Sponsorship／Media Enquiries／General Contact | 規劃書 §3.10 |
| 漫畫／球迷會／官方商品與線上商店／特約店家 | Manga／Fan Club／Merchandise & Online Store／Partner Perks | 規劃書 §3.8 |
| 會員中心／一般會員／付費球迷會員／會籍／會員卡 | Member Centre／Registered member／Paid Fan Club member／Membership／Membership card | 規劃書英文版 |
| 購物車／結帳／訂單查詢 | Cart／Checkout／Order lookup | 規劃書 §2 |
| 慈善與社會影響 | Charity & Impact | 規劃書 §3.11 |
| 常見問題 | FAQ | 規劃書 |
| 新聞／新聞稿／媒體專區 | News／Press release／Press & Media | 規劃書 |
| 里程碑／榮譽 | Milestones／Honours（英式拼寫，與規劃書英文版一致） | 規劃書英文版 |
| 五大核心價值 | Players First／Excellence／Global Pathways／Community／Integrity | 本檔 §4 |
| 教練／總教練／助理教練／守門員教練／體能教練／領隊／球探 | Coach／Head Coach／Assistant Coach／Goalkeeper Coach／Fitness Coach／Team Manager／Scout | 初稿；**已有 `staff_i18n` en 職稱時以資料為準** |
| 位置：門將／後衛／中場／前鋒 | Goalkeeper／Defender／Midfielder／Forward | 初稿 |
| 隱私權政策／Cookie 政策／會員條款／監護人同意 | Privacy Policy／Cookie Policy／Membership Terms／Guardian Consent | **內文不翻，維持「pending legal review」佔位**（英文版翻譯規則見 [`05-i18n-seo.md`](05-i18n-seo.md) §1） |

**翻譯過程新增、尚待客戶確認的寫法（2026-10-05，開發端初稿，非官方名稱）**：
賽程場地——台北田徑場 Taipei Athletics Stadium、台南市立足球場 Tainan Municipal Football Stadium、楠梓足球場 Nanzih Football Field、汐止綜合運動場 Xizhi Sports Complex、輔仁大學足球場 Fu Jen Catholic University Football Field（地名拼音＋通用場地詞）；
賽事狀態——Upcoming／Live／Finished／Postponed／Cancelled（`MATCH_STATUS_MAP.labelEn`）；
職稱——青訓教練 Youth Development Coach、青訓總監 Director of Youth Development、顧問 Advisor／Technical Adviser；部門——競技部 Football Department、學院部 Academy Department、課程部 Programs Department、商務部 Partnerships Department；
活動／盃賽——台中磐石足球節 Taichung Rock FC Football Festival、台中磐石盃 Taichung Rock FC Cup、總統盃 President's Cup；
慈善子頁——慈善理念 Our Commitment、慈善計畫 Charity Programs、慈善事蹟 Impact Stories、影響力數據 Our Impact；
商店與會員——統一編號 Unified Business Number、手機條碼載具 Mobile barcode carrier、入會球衣 Welcome jersey、超商取貨 Convenience-store pickup；
新聞標題（第二輪）——預備隊 Taichung Rock FC Reserves、台中磐石國際足球盃 Taichung Rock FC International Cup、維羅納 Hellas Verona、臺中市政府運動局 Taichung City Government Sports Bureau、東京農業大學 Tokyo University of Agriculture、港超聯 Hong Kong Premier League；**標題內沒有英文來源的球隊／人名／學校（銘傳大學、陽信北競、南市台鋼、大同足球、台電／台灣電力、台中Futuro、新北航源、桃園國際、高雄先鋒、潭秀非營利幼兒園、南投縣雙龍國小、龜記茗品，及陳曉明、周宇杰、廖奕盛、王義友、孫恩祈、梁顥騰、高冠宇、楊朝景等）原樣保留中文，待客戶補英文名**；
聯賽縮寫——規劃書英文版用 TFPL 指企業甲級聯賽，**但全名未出現，不得自行拼寫**。單元導覽短標籤「新聞」用 News（單元全名為 News & Stories）。

⚠️ 人名、地名：球員與教練英文名一律取資料庫（`players_i18n`／`staff_i18n`）已有值；沒有的**不得自行音譯**，顯示中文原名。
地址只翻成通用的英文地址格式不做，維持中文原文＋必要時加 Taichung 城市名。

---

## 2. 設計 tokens

取自 mockup 的 `:root`。實作網站時**直接沿用**，不要另起一套。

### 色彩

**兩個品牌基準色**直接來自 logo 主檔，不得自行調整：

| | 印刷 | 網頁 |
|---|---|---|
| 品牌桃紅 | PANTONE 225 C／`C5 M90 Y0 K0` | `#E0218A` |
| 品牌黑 | `C0 M0 Y0 K100` | `#231916`（**暖調近黑，不是深藍**） |

其餘 token 皆由上述兩色推導：

| Token | 值 | 用途 |
|---|---|---|
| `--brand` | `#E0218A` | 品牌桃紅正色：色塊填色、深底文字、**大型**標題（≥18.66px bold 或 ≥24px） |
| `--brand-aa` | `#D61E83` | **AA 安全版 4.80:1** — 白底小字，或承載白色小字的實心底 |
| `--brand-bright` | `#E85BA9` | hover 提亮（深底上 5.32:1） |
| `--brand-deep` | `#AE186B` | 實心按鈕 hover（白字 6.68:1） |
| `--ink` / `--ink-2` | `#231916` / `#31231F` | 深色底 |
| `--ink-trim` / `--ink-trim-dk` | `#231916` / `#4C3630` | 切角三角（淺底／深底） |
| `--paper` / `--paper-2` / `--paper-3` | `#FFFFFF` / `#F5F5F5` / `#F8F7F6` | 淺色面 |
| `--heading` / `--text` / `--muted` | `#231916` / `#333333` / `#666666` | 文字階層 |
| `--muted-dark` | `#BFB4AF` | 深底上的次要文字（8.48:1） |
| `--rule` / `--ghost` | `#E0E0E0` / `#CCCCCC` | 分隔線與裝飾數字 |

> ⚠️ **`--brand` 對白底只有 4.42:1**，通過大字 AA 但未達小字的 4.5:1。
> 兩種情境一律改用 `--brand-aa`：①白底粉色小字 ②粉底白色小字（按鈕、標籤、徽章）。
> 兩色肉眼幾乎無差別，但這是無障礙 AA 的硬性要求（規劃書 §8）。

> 舊 token `--brand-ink`／`--navy-trim`／`--navy-trim-dk` 已分別更名為 `--brand-aa`／`--ink-trim`／`--ink-trim-dk`，見 [`08-roadmap-decisions.md`](08-roadmap-decisions.md) §2。


### 台中藍鯨的色彩（藍鯨規劃書 §8.1 定案）

**一律取自隊徽，不自行配色。** 主色與深色是隊徽上的實際用色，其餘四個由主色依對比度需求推導。
隊徽主檔與取樣方法見 [`../brand/blue-whale/README.md`](../brand/blue-whale/README.md)。

| Token | 值 | 來源與用途 |
|---|---|---|
| `--brand` | `#2196D5` | **隊徽藍**（佔隊徽藍色像素 92.8%）。對白 3.29:1，**只能用於大字與色塊** |
| `--brand-aa` | `#1A78AA` | 主色降亮度 20%，**對白 4.87:1** — 白底小字、承載白色小字的實心底 |
| `--brand-bright` | `#50B0E4` | 主色提亮 25%，深底 hover（對 `--ink` 8.63:1） |
| `--brand-deep` | `#156088` | 主色降亮度 36%，實心按鈕 hover（白字 6.86:1） |
| `--ink` | `#040000` | **隊徽黑**（鯨魚與字體的實際用色） |
| `--ink-2` | `#1D1919` | `--ink` 提亮一階，深底層次 |
| `--ink-trim` | `#040000` | 淺底切角三角，同 `--ink` |

`shell.html` 的 `theme-color` 一併改為 `#2196D5`。

> ⚠️ **藍鯨主色對白只有 3.29:1，比磐石的 4.42:1 差更多。** 小字誤用主色在藍鯨站會是更明顯的無障礙缺陷，
> `verify.mjs` 複製到 `site-bw/` 時要把硬編碼色碼清單一起換掉。
>
> 🔴 **印刷色票（PANTONE／CMYK）仍未提供**——上表是自隊徽點陣主檔取樣的**網頁色值**，**印刷品不得依此發包**。

### 字型與尺度
- 字體：`Montserrat`（英數）+ `Noto Sans TC`（中文），fallback `PingFang TC`
- 容器寬 `1280px`；邊距 `clamp(1.25rem, 4vw, 3.5rem)`
- 字級全部用 `clamp()` 流體縮放：`--fs-hero` / `--fs-h2` / `--fs-h3` / `--fs-stat` / `--fs-ghost`
- 動態：`--dur .32s`、`--ease cubic-bezier(.2,.7,.2,1)`

### 三個標誌性視覺手法（沿用自 mockup，勿隨意替換）
1. **Programme pagination** — 每個區塊帶超大幽靈數字，像賽事節目單的頁碼
2. **Kit-trim cut** — 卡片切角，後方襯品牌黑三角
3. **Floodlight ink** — 深色帶用單一平塗品牌黑（`#231916` / `#31231F`），**無漸層、無材質**

> 形狀語言為**硬邊直角**（rect ≫ roundRect），零漸層——這是品牌簡報的既定調性。

---

## 3. 格式規範

| 項目 | 格式 | 範例 |
|---|---|---|
| 日期 | `YYYY-MM-DD` | `2026-03-15` |
| 時間 | 24 小時制 `HH:MM` | `19:00` |
| 賽事時區 | 依瀏覽者當地時區顯示，並標註「時間可能異動」（**前台**規則） | |
| **後台時間戳** | 後端輸出 UTC 帶 `Z`、輸入不帶時區一律當 UTC；**後台畫面一律以台灣時間（Asia/Taipei，UTC+8）顯示與輸入**，送出必帶時區；全站只走 `apps/admin/src/utils/dateTime.ts`（eslint 擋其他寫法，見 `docs/18` `E-90` 前端段）。純日期 `YYYY-MM-DD` 不做時區轉換；全天活動送「該日 T00:00:00Z」 | `2026-10-05 09:00`（台灣時間） |
| 賽事網址 | `/schedule/YYYY-MM-DD-tcrfc-vs-對手` | |
| 隊別網址 | `/schedule/{code小寫}/` | `/schedule/u15/` |
| 金額 | `NT$` + 千分位 | `NT$1,000` |
| 球員標示 | 背號 + 姓名 | `09 劉○○` |

**行動 App 專用格式**（v1.0 新增）：

| 項目 | 格式 | 範例 |
|---|---|---|
| 廣告版位代號 | `{畫面}_{位置}`，小寫蛇形 | `home_top`、`schedule_list` |
| 深連結 scheme | `tcrfc://{資源}/{識別}` | `tcrfc://schedule/d1`、`tcrfc://news/{slug}` |
| App 版本號 | 語意化 `主.次.修`，建置號另計 | `1.4.2 (231)` |
| 裝置識別碼 | 安裝時產生的隨機值，**不使用廣告識別碼** | `device_install_id` |
| 廣告事件時間 | 記錄**發生時間**非上傳時間；超過 24 小時伺服器端拒收 | |
| **功能開關代號** | `{模組}_{功能}`，小寫蛇形 | `ads_enabled`、`payment_mode` |
| **語系代碼** | API 與 `AppDevice.lang` 一律 `zh`／`en`；裝置的 `zh-Hant-TW` 映射為 `zh`（⚠️ 這同時服務 [`17-deployment.md`](17-deployment.md) §4「Redis key 必須含 `locale`」——語系值必須是有限集合，否則 key 爆炸） | `zh`、`en` |
| **建置號** | CI run number，兩平台各自單調遞增，**不參與版本比較** | `231` |

---

## 4. 檔名慣例

**App 建置產物歸檔**（見 [`19-app-tech-stack.md`](19-app-tech-stack.md) §9）：`{平台}-{版本}-{建置號}.dSYM.zip`／`{平台}-{版本}-{建置號}-mapping.txt`，例 `ios-1.4.2-231.dSYM.zip`。

**媒體資產**（規則沿用自已退役的單頁 mockup，現行檔案見 [`../site/src/assets/img/`](../site/src/assets/img/)）：
```
{型別}-{編號}-{識別}.{副檔名}
player-09-liu.jpg    coach-ezoe.jpg    match-01.jpg
news-trencin.jpg     partners/04-joma.png    home-hero.jpg
```

**客戶收件資料夾**（見 [`07-content-pipeline.md`](07-content-pipeline.md)）：
分類資訊由**路徑**承載，客戶不需改檔名。需建立子資料夾的地方遵循：
```
球員：{背號}_{姓名}          → 09_劉大明
新聞：{YYYY-MM-DD}_{標題}    → 2026-05-20_台中磐石作客特倫欽
慈善：{YYYY-MM-DD}_{團體名}  → 2026-04-02_台中家扶中心
夥伴／贊助：{公司名}
```

**品牌資產**：不套用上述規則，一律沿用 [`../brand/`](../brand/) 既有檔名（`tcrfc-{組合}-{色}.svg`、`favicon.*`、`icon-*.png`、`og-image.png`）。
標誌檔案**不得手工修改**，`.ai` 更新時整批重產。

**專案文件**：`docs/{兩位數序號}-{kebab-case}.md`

---

## 5. 文案撰寫規則

| 區塊 | 建議長度 |
|---|---|
| 頁面主標題 | 20 字內 |
| 導言／副標 | 60 字內 |
| 內文段落 | 3–5 段，每段可配 1 圖 |
| SEO 描述 | 80–120 字 |
| 卡片摘要 | 40–60 字 |

- 中英夾雜時，英文詞前後留半形空格
- 頁面標題慣例為「中文 + English」並列（如 `Our Story 我們的故事`），沿用規劃書寫法
- 每個靜態頁底部都應有一個明確 CTA（G-05 元件）
- 涉及球員、學員的敘述避免承諾性語句（如「保證入選」「必定升學」）
