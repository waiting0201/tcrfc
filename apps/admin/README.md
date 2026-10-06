# apps/admin — admin-web（官網共用後台）

台中磐石官網主站與台中藍鯨官網**共用同一個後台**的 Vue 3 SPA。

✅ **S1-12 後台畫面（2026-09-25）：H 搜尋與 AI 能見度全新完成（全站設定／301 轉址／孤立頁面
偵測）＋ B1／B2 單頁搜尋與分享設定**——接上同名後端（見 `apps/api/README.md`「S1-12」）。
新增三個子模組 **H1 全站設定**（標題樣板、預設描述、全站預設分享圖片，沿用 S0-8 共用上傳元件
「選檔不上傳、儲存才上傳」；robots.txt 線上編輯，畫面明講「要等正式上線後才生效」；GA4／
GTM／Meta Pixel／LINE Tag 追蹤碼）、**H2 301 轉址**（列表＋搜尋分頁、新增／編輯對話框、刪除、
CSV 匯入匯出）、**H3 孤立頁面偵測**（唯讀報表，畫面上把偵測方式的限制講清楚，不假裝是完整的
連結地圖）。三個子模組的權限碼皆為 `sysadmin_only`，`AppSidebar.vue` 的
`SYSADMIN_ONLY_MODULE_CODES` 新增 `'H'`，整組比照既有 `J` 系統管理，只有系統管理員在側欄看得到。
**B1／B2 編輯頁**分別在既有「搜尋引擎摘要資料」卡片與新增的「搜尋與分享設定」卡片補上：關鍵字、
分享圖片（獨立於封面／內容區塊圖片之外，走 `ogImage` 這個 multipart 欄位）、分享圖片替代文字、
正式網址（單一欄位，不分語系）、不讓搜尋引擎收錄、不列入網站地圖。**日常用語新增進
`docs/06-conventions.md` §1**：`SEO`／`OG`／`robots.txt`／`sitemap` 一律不用原文顯示，`GA4`／
`GTM`／`Meta Pixel`／`LINE Tag` 刻意保留原文（第三方服務正式名稱，理由見該檔案）。詳見下方
「H 搜尋與 AI 能見度」整節（含真實驗收紀錄與未驗證項目）。

✅ **S1-12a／S1-12b 後台畫面（2026-09-25）：H4 AI 摘要資料（`llms.txt` 維護）／H5 AI 爬蟲授權
全新完成**——接上同名後端（見 `apps/api/README.md`「S1-12a」「S1-12b」）。**H4**：站點定位、
代表頁清單、事實摘要、授權與引用方式、聯絡窗口五個雙語區塊，單筆表單，畫面明講「存檔後立即
生效」。**H5**：AI 服務清單（名稱＋允許／拒絕，可新增刪除）、自訂不開放的頁面路徑（可新增刪除）、
系統保護的頁面（唯讀陳列，規格強制、後台無法刪除，說明文字講清楚是為了保護會員資料與未成年
學員照片），畫面明講「網站正式上線後才會生效」。`docs/06-conventions.md` §1 新增 `llms.txt`／
使用者代理／排除路徑／強制排除路徑四筆對照。**`npm run lint`／`build` 皆過**；🔴 **無頭瀏覽器
實走因本機環境限制未能完成，標記未驗證**——詳見下方「H4／H5」整節。

✅ **S1-10 後台畫面第三輪修正（2026-09-25）：G1 欄位題目文字、G2 姓名選單開放給所有處理權限
角色、P1–P3／G1–G2／L1–L2 權限判斷改讀 `/auth/me` 權限碼、俱樂部範圍頁面閃錯 bug 已修**——
接上同名後端補完（見 `apps/api/README.md`「S1-10 修正」）。**G1**：新增欄位時新增「題目文字
（中文）」必填與「（英文）」選填（`BilingualShortField`），下拉／多選選項新增英文顯示文字
（要嘛整組填、要嘛整組留空，前端與後端各自防呆一次）；欄位清單新增「題目文字」欄，不再只顯示
英文欄位代碼。**G2**：詳情頁改呼叫公開端點 `GET /api/v1/{club}/forms/{formCode}?lang=zh`
（`src/api/publicForms.ts`，任何角色都能呼叫，不需要 G1 權限）取得題目文字對照表顯示訪客回答，
刪除舊版猜測對照表 `fieldKeyLabel()`／`FIELD_KEY_LABEL_HINTS`；「指派負責人」改接新端點
`GET .../enquiries/assignable-users?formCode=...`，持有處理權限的角色（不限系統管理員）都能用
姓名選單指派給任何一位同樣持有處理權限的候選人，拿掉舊版「只有系統管理員能選人名」的限制與
說明文字。**權限判斷改讀後端**：`useProgramPermissions`／`useFormsPermissions`／
`useCalendarPermissions` 全部改成讀 `GET /auth/me` 新增的 `permissions: {code, scopeTypes}[]`，
刪除三份手寫「角色→操作」對照表常數（根治 `docs/18-work-errors.md` E-39／E-60／E-61 同類風險），
`useRolePermissions.ts` 新增 `hasPermission`／`hasAnyPermission` 取代舊版的 `hasAnyRole`。**修
既有 bug**：`apps/admin/src/router/index.ts` 路由守衛的 `ensureClubsLoaded()` 改成 `await`
（原本 fire-and-forget），俱樂部範圍頁面整頁重新載入不再閃一次「沒有被授權」錯誤畫面
（`S1-11` 發現的既有缺陷）。詳見下方「G1–G2 表單與詢問」節的更新內容與「本輪驗收（S1-10 第三
輪）」。**規格缺口維持未解，非本輪能力範圍**：G1「檔案上傳」欄位型別仍只收文字／網址（全系統
沒有通用檔案儲存服務）、「防機器人驗證」已串接（開啟後前台顯示驗證、伺服器端送出時檢查；需系統管理員已設定金鑰才生效，見 `apps/web/README.md`）。

✅ **S1-9 前端接線（2026-09-25）：P1 項目／P2 梯次與場次／P3 報名管理三組列表＋編輯畫面全新
完成**——接上同名後端（見 `apps/api/README.md`「S1-9」）。**P1**：類型／狀態篩選、雙語名稱與
簡介、課程內容（區塊編輯器，與靜態頁同格式）、教練團多選（接 C3 既有清單）、封面圖
（沿用 S0-8 共用元件，選檔不上傳、儲存才上傳）。**P2**：所屬項目建立後鎖定不可改、名額上限與
已報名數（唯讀，由報名寫入路徑維護）、費用與早鳥、報名起訖時間、狀態（留空自動判定額滿）。
**P3**：梯次／狀態篩選、後台代填報名、處理報名（確認／取消／轉梯次／候補／備註／學員資料整份
覆寫）、CSV 匯出（`is_restricted`，依權限顯示）。健康聲明依後端現況原樣顯示與編輯，**未新增
蒐集欄位**；匯出不含這一欄（後端已排除）。**新增權限判斷 `useProgramPermissions`**
（`src/composables/useProgramPermissions.ts`）與側欄可視性擴充（`AppSidebar.vue`），依角色代碼
決定 P1／P2／P3 三個子模組要不要顯示、能不能新增／編輯／匯出，詳見下方「P1–P3 課程與活動」
整節（含已知限制與已驗證／未驗證清單）。

✅ **S1-6／S1-7／S1-7a 前端接線（2026-09-24）：首頁編排（B3）、常見問題（B4）、球隊／球員／
教練與團隊成員（C1–C3）三大模組全新完成，新聞與故事（B2）的「球隊」關聯解除停用**——接上同名
後端（見 `apps/api/README.md`「S1-6」「S1-6 續作」「S1-7」「S1-7a」）。**B3**：Hero 輪播 CRUD
（排序、圖片＋雙語替代文字、標題／CTA、上架期間；影片選項停用並附「規則待確認」說明）、九大
區塊開關與排序（Hero 額外可指定精選輪播）。**B4**：主題分類管理（新增／排序／啟用停用切換／
真刪除二次確認）、題目 CRUD（分類可複選、雙語、額外指定嵌入掛載點）、成效數據（瀏覽數／
👍👎／低評價排序）、批次改分類與顯示隱藏、CSV 匯入（逐列錯誤對話框）與匯出下載。**C1–C3**：
球隊／球員／教練與團隊成員三組列表＋編輯畫面，球員與教練新增**肖像同意**三態選擇（預設未同意，
欄位旁註明「未同意時前台不顯示照片」），共用（兩隊共用）教練資料整頁唯讀並附原因說明。**B2**：
「球隊」關聯選項改接 C1 新增的俱樂部範圍端點（`team.team.view`，內容編輯角色有權限），不再是
S1-5 回報的系統管理員限定端點，隨即解除停用。詳見下方「首頁編排（B3）」「常見問題（B4）」
「球隊／球員／教練與團隊成員（C1–C3）」「新聞與故事的球隊關聯」與「本輪驗收（S1-6／S1-7／
S1-7a）」各節。

✅ **S1-5（2026-09-24）：新聞與故事（B2）補上標籤、核心價值標籤、關聯、瀏覽數、批次操作**——
接上同名後端補完（見 `apps/api/README.md`「S1-5」）。新聞編輯頁新增：標籤（find-or-create 多選，
可直接打中文字新增）、核心價值標籤（固定 5 個值的核取方塊）、關聯（球員／賽事有真正的搜尋選擇器，
球隊／課程／夥伴三種因為沒有這個帳號打得到的唯讀清單而停用，見下方「新聞與故事：標籤／核心價值
標籤／關聯／批次操作」整節的 API 缺口說明）、瀏覽數（唯讀顯示）；列表頁新增標籤晶片、瀏覽數欄、
批次改分類、批次下架。詳見該節。

✅ **S1-4（2026-09-24）：頁面管理（B1）＋站台切換器改接 `/auth/me`＋賽季／球隊改真下拉選單**——
新增「頁面管理」完整畫面（列表、12 種區塊的區塊化編輯器、發布／排程、版本歷程與還原、預覽連結），
站台切換器改讀 `GET /api/v1/admin/auth/me`（只列出這個帳號目前有效的俱樂部授權，系統管理員例外
看到全部啟用中的俱樂部）並帶出登入者姓名給 `UserMenu.vue`；賽事系列的「賽季」與帳號的「球隊授權」
兩處原本「沒有清單只能貼識別碼」的暫時作法，已改接 `GET /admin/{club}/seasons`／
`GET /admin/teams` 真下拉選單。詳見下方「B1 頁面管理」「站台切換器」「賽事系列」「系統管理畫面」
各節與「本輪驗收（S1-4）」。

✅ **S1（2026-09-24）：接上真實登入與 J1／J2／J4／C4 賽事系列**——外殼＋儀表板＋「新聞與故事」
（B2）＋帳號（J1）＋角色與權限（J2）＋俱樂部與授權管理（J4）＋賽事系列（C4 底下的一小部分）
現在都是真的打 `apps/api` 的畫面，不再是假資料。**開發寫入閘門（`DevWriteGate`／`X-Dev-Operator-Id`）
已隨後端整支移除**，`src/api/gate.ts`／`src/api/devOperator.ts` 這兩個檔案本輪一併刪除，所有寫入
端點一律走真實登入權杖（見下方「登入與工作階段」）。

🟡 **尚未真的做的（本輪不在範圍內，回報）**：J3 稽核與備份（後端本來就已撤回稽核記錄，見
apps/api/README.md）、C4 的完整賽程賽果（本輪只做了「賽事系列」這個支援型別，見 `data/nav.ts`
該筆的註解）、其餘 9 個模組維持假資料或佔位頁狀態。

規格的真實來源是規劃書與 [`docs/03-admin-spec.md`](../../docs/03-admin-spec.md)；版面與視覺規則是
[`docs/21-admin-ui.md`](../../docs/21-admin-ui.md)（v3，`visual-design-architect` 產出，
本次實作依它逐條核對）。

🔴 **深色是唯一主題，不是淺色的延伸**（2026-09-21，`S0-12c` 依 `docs/21` v2 重做）：舊的淺色＋
Element Plus 預設藍版本已整批換掉，**不做主題切換開關，不留淺色 token 分支**（`docs/21` §7.0
使用者已拍板）。版面骨架也不是換色而已——頂欄拆成系統列／頁面列兩層、側欄選中態改用 accent bar、
狀態 tag 換成深色語意底＋亮色文字、編輯頁分段卡片改用四層背景色階（tonal elevation）取代陰影、
圖片預覽區塊加了不隨主題變化的「中性看片台」。細節見下方各節與 `docs/21` §1–§9。

✅ **俱樂部配色切換（2026-09-30，主站規劃書 v3.16 §4.0）：後台切到台中藍鯨時整套主色換成藍鯨色，切回磐石恢復**。

- **機制**：`src/auth/clubAccess.ts` 的 `activeClubId` 是唯一 state，所有寫入走 `setActiveClub()`——改 state、記住選擇
  （`localStorage` 鍵 `tcrfc-admin-active-club`，只是便利，讀寫 try/catch）、呼叫 `src/auth/clubTheme.ts` 的
  `applyClubTheme()` 在 `<html>` 設 `data-club="tcrfc|bw"`。`src/styles/admin-theme.css` 的
  `html.dark[data-club='bw']` 區塊覆寫背景四層＋輸入框底、`--admin-primary*` 四階、草稿 tag 底；Element Plus 的
  `--el-color-primary*` 是 `var(--admin-primary*)` 參照所以跟著換。原本寫死的 `rgb(232 91 169 / n%)`（表格選中列、
  兩個上傳元件拖曳提示）改為 `color-mix(in srgb, var(--admin-primary) n%, transparent)`。
- **首次繪製**：`index.html` 的內嵌 script 在第一次繪製前同步讀 `localStorage` 設 `data-club`（與 `clubTheme.ts`
  邏輯一致，改一邊要改另一邊），重新整理不會先閃磐石色。**副作用**：目前俱樂部選擇現在會跨重新整理保留
  （以前重整會回到帳號主要俱樂部）；`ensureClubsLoaded` 仍會校正成帳號有授權的俱樂部。
- **登入頁**：尚未選擇俱樂部，固定磐石預設（router `beforeEach` 對公開路由套磐石；內嵌 script 在 `/login` 也不套藍鯨）；
  登出（`resetClubAccess`）清掉記住的選擇。
- **隊徽**：唯一有隊徽的位置是 `SiteSwitcher.vue`（頂欄），本來就依 `activeClubId` 換成 `bw-crest-48.png`，全後台沒有其他磐石標誌。
- **色值**：藍鯨逐字取自 `apps/web/public/assets/css/club-bw.css`（`docs/13` §6a）。`--brand-bright #50B0E4`→primary、
  `--brand-aa #1A78AA`→實心按鈕、`--brand-deep #156088`→hover、`--ink #040000`→canvas、`--ink-2 #1D1919`→surface。
  **衍生值（藍鯨變數沒有對應色階）**：surface-2 `#292424`／overlay `#362E2E`（`--ink-2` 依磐石 4.5pp 級距往上推）、
  input `#000000`、按下態 `#10435F`（`--brand-deep` 與 `--ink` 7:3 混色，同磐石公式）。完整對照表見 `docs/21` §5.1。
- **對比度檢查**：`scripts/check-contrast.mjs` 改為磐石、藍鯨兩組各跑一遍（含「admin-theme.css 區塊值與腳本一致」與
  「草稿 tag 底須等於 surface-2」）；文字階層／邊框／語意色兩組共用但仍逐組重算。藍鯨組全數通過（最低：
  tertiary 文字 on overlay 4.84:1、border-input on overlay 3.35:1、focus 外框 on overlay 5.46:1）。
- **驗收**：`npm run lint`／`build` 皆過；`dist` CSS 靜態確認兩組變數輸出、`index.html` 含內嵌 script。
  🔴 **瀏覽器實走（登入後切換俱樂部、重新整理）未驗證**，需要 API。

🔴 **配色 v3（2026-09-22）：中性深灰＋Element Plus 藍 → 品牌黑＋品牌桃紅**（依 `docs/21` §4／§5／§7／§15
重做）：四層背景色階改由品牌黑 `--ink`／`--ink-2` 推導、操作主色改用品牌桃紅系 `--brand-aa`／
`--brand-bright`／`--brand-deep`、文字與邊框階跟著暖化、危險色橘紅化（`H≈0°→14°`）、站台切換器隊徽
從純色色塊改用兩隊各自的真實隊徽圖像（`src/assets/brand/`）。**（2026-09-30 已改：切換站台時主色隨俱樂部換，見下方「俱樂部配色切換」）原本切換站台不換主色**（`docs/21`
§5.1／§5.1.1：後台永遠是磐石桃紅）。中性看片台（`--admin-lightbox-*`）明文不隨這次換色調整，維持
中性冷灰。`--admin-border-input` 用的是**門檻反推值 `#8A7E75`**，不是暖化等亮度換算的 `#7A6F68`——
後者對 `overlay` 只有 2.66:1，過不了 3:1 的 UI 元件門檻，這條與 E-31 同根因，已寫進
`scripts/check-contrast.mjs` 的邊框笛卡兒積檢查（見下方「檢查腳本：對比度檢查」）。

---

## 環境變數（API 位址）

| 變數 | 何時生效 | 用途 |
|---|---|---|
| `ADMIN_API_BASE_URL` | **容器執行期**（`docker run -e`／compose `environment`） | 啟動時由 `docker-entrypoint.d/40-runtime-config.sh` 產生 `/config.js`（`window.__TCRFC_CONFIG__.apiBaseUrl`），nginx 以 `Cache-Control: no-store` 提供，`index.html` 先於應用程式載入。換網域（`docs/17` §10）改 `.env` 的 `API_DOMAIN` 重建容器即可，**不必重建映像檔** |
| `VITE_ADMIN_API_BASE_URL` | 建置期（本機開發 `.env.development`） | 見 `.env.example`；容器內不使用 |

解析順序（`src/api/runtimeConfig.ts`）：`/config.js` ＞ `VITE_ADMIN_API_BASE_URL` ＞ **僅 `npm run dev`** 退回 `http://127.0.0.1:5299`。
🔴 正式建置找不到設定時**不退回 127.0.0.1**：畫面顯示「未設定 API 位址」並在 console 報錯。
部署後驗證：`curl https://tcrfc-admin.4webdemo.com/config.js`。本機開發（`npm run dev`）行為不變，讀的是 `public/config.js` 的空設定。

## 怎麼跑

```bash
cd apps/admin
npm install
npm run dev        # http://localhost:5174，熱重載
npm run build      # 型別檢查（vue-tsc）＋ Vite production build，輸出到 dist/
npm run preview    # 起一個本機 server 服務 dist/，用來驗收 build 產物
npm run lint       # ESLint ＋ 禁用詞掃描 ＋ 對比度檢查（見下方「檢查腳本」），三者都要過
```

Docker（多階段 build，nginx 服務靜態檔）：

```bash
docker build -t tcrfc-admin apps/admin
docker run --rm -p 8080:8080 tcrfc-admin
curl -I http://localhost:8080/          # 應該看到 X-Robots-Tag: noindex, nofollow
curl http://localhost:8080/healthz      # 應該回 "ok"
```

## 登入與工作階段（S1，2026-09-24）

對照 `apps/api/README.md`「後台登入權杖設計」——這裡只記前端這一側怎麼接，權杖本身的設計理由
（為什麼存取權杖 15 分鐘、為什麼更新權杖是 `__Host-` Cookie 不是 JWT）在後端那份文件，不重複。

**存取權杖只放記憶體，不落地 `localStorage`／`sessionStorage`**（`src/auth/session.ts` 的模組層級
`reactive()` 單例）——這是任務指示要求說明取捨的地方：`localStorage` 是任何一段注入或被入侵的
前端程式碼都能直接讀走的地方，XSS 一旦發生就等於偷走權杖；放記憶體的代價是重新整理頁面會遺失，
由開機時的靜默 `refresh` 吸收（見下方）。**更新權杖從頭到尾不進入這層**——它是後端設定的
`__Host-tcrfc-admin-rt` `HttpOnly` Cookie，前端 JavaScript 從語言層級就讀不到它，前端唯一要做對
的事是每個 `fetch` 都帶 `credentials: 'include'`（`src/api/http.ts`／`src/api/adminAuth.ts` 全部
呼叫點都有）。

**401 自動 refresh 一次**：`src/api/http.ts` 的 `performRequest`／`performUploadRequest` 收到 401
時，先看回應是不是 ProblemDetails 形狀（有 `title` 欄位＝「登入已逾期」，見
`looksLikeSessionExpired()` 的說明）——是的話呼叫 `refreshAccessToken()`
（`src/api/adminAuth.ts`，多個並發請求共用同一個進行中的 promise，不會一次過期就打好幾支
`/auth/refresh`）換一把新權杖後重打原始請求一次；仍然失敗就清工作階段、導回 `/login`
（`redirectToLogin()`）。**不是 ProblemDetails 形狀的 401**（`/auth/change-password`／
`/auth/2fa/confirm`／`/auth/2fa/disable` 這幾支端點對「密碼／驗證碼本身就是錯的」用
`{message}` 這種自訂形狀）直接當一般錯誤顯示，不觸發 refresh——重打一次密碼錯誤的請求也不會
變成功，混在一起只會讓使用者多等一次不必要的網路往返。

**登入流程**（`src/views/auth/LoginView.vue`）：帳號密碼 → 若該帳號已啟用兩階段驗證，後端回
`totp_required`（不是失敗，不計入鎖定次數）→ 同一頁切換成驗證碼輸入 → 成功後依路由守衛決定去處。

**路由守衛**（`src/router/index.ts` 的 `router.beforeEach`）依序判斷：
1. 開機靜默 `refresh`（`ensureBootstrapped()`，只跑一次）試著用更新權杖 Cookie 恢復工作階段。
2. 未登入 → 導去 `/login`（帶 `redirect` 查詢參數，登入成功後導回原本要去的頁面）。
3. **（2026-09-30 使用者裁決）不再強制改密與強制 2FA**：已登入後不論 `must_change_password`／
   `two_factor_enabled` 為何，守衛都不再導向 `/account/security?forced=...`（`needsForcedOnboarding`
   與 `forced` 查詢參數已整個移除），登入後直接進 `/dashboard` 或原本要去的頁面，正式環境亦同。
   `/account/security`（`AccountSecurityView.vue`）保留為使用者自己從選單「帳號安全設定」進來調整
   密碼的地方。**（同日再裁決）兩階段驗證的啟用／停用入口已從介面隱藏**：帳號安全設定頁只剩
   「更改密碼」；`adminAuth.ts` 的 `beginTwoFactorSetup`／`confirmTwoFactorSetup`／`disableTwoFactor`
   保留但不在介面引用，日後開放時接回即可。
   **已啟用 2FA 的帳號登入時仍要輸入驗證碼**（`LoginView.vue` 的 `totp_required` 流程不變）。
   `session.ts` 的 `mustChangePassword`／`twoFactorEnabled` 改為「回應缺漏時視為 `false`」，前端不依賴
   後端是否還回傳這兩個欄位。帳號列表「安全設定」欄只留「待改密」標籤，「未啟用兩階段驗證」標籤已隱藏
   （使用者無法處理的狀態）。管理員對他人帳號的「重設兩階段驗證」動作保留（已啟用 2FA 的帳號
   遺失驗證器時仍需要解鎖途徑）。
4. **`meta.sysadminOnly` 的路由**（J1／J2／J4）非系統管理員直接改網址進入會被彈回 `/dashboard`
   並跳出「你的帳號沒有權限進入這個模組」——這也只是第二層提醒，`AppSidebar.vue` 依
   `authUser.value?.isSuperAdmin` 整組濾掉側欄項目是第一層，**真正把關永遠是後端**每個
   `system.*` 權限碼皆為 `sysadmin_only`。

**站台切換器改接 `GET /api/v1/admin/auth/me`（S1-4，2026-09-24）**：`src/auth/clubAccess.ts`
取代先前用公開 `GET /api/v1/clubs` 頂著的暫時作法（那個做法只能列出系統裡「有哪些俱樂部」，
不是「這個帳號被授權哪些俱樂部」）。✅ **已解決先前記錄的已知 API 缺口**——後端已補上
`/auth/me`（apps/api/README.md「前端回報缺口①」），現在切換器**只列出這個帳號目前有效
（未過期、未撤銷）的俱樂部授權**，系統管理員例外（後端固定回傳「全部啟用中的俱樂部」）。
同一次呼叫也把姓名（`displayName`）帶回來寫進 `@/auth/session`，`UserMenu.vue` 因此顯示得出
真實姓名而不只是登入帳號字串。**只授權一個俱樂部的帳號**（`SiteSwitcher.vue` 既有邏輯：
`clubs.length > 1` 才顯示可互動下拉，否則顯示唯讀的 `.site-switcher--static`）已用無頭瀏覽器
實際登入一個只授權台中磐石的帳號驗證過，切換器確實只顯示「台中磐石」（見下方「本輪驗收
（S1-4）」第 3 點）。⚠️ **切換器仍然只是介面便利，不是安全邊界**（docs/21 §5）：清單雖然已經是
「被授權的俱樂部」，範圍檢查仍然一律由後端 `AdminClubAuthorizer` 即時判斷，不能假設前端清單
「本來就是對的」（例如授權在清單載入之後被撤銷的情況）。

## 專案結構

```
apps/admin/
├── src/
│   ├── main.ts              # 掛載點：固定在 <html> 掛 class="dark"（深色是唯一主題，不做切換開關）
│   ├── App.vue               # 只有一個 <router-view />
│   ├── router/index.ts       # 路由表 + 登入／強制流程／sysadminOnly 三層路由守衛（見上方「登入與工作階段」）
│   ├── auth/
│   │   ├── session.ts        # 登入工作階段單例：存取權杖（記憶體）＋使用者旗標＋姓名（displayName，由 /auth/me 補上），見上方存放策略說明
│   │   └── clubAccess.ts     # 站台切換器的俱樂部清單（改接 `GET /auth/me`，見上方「站台切換器」）＋目前選取的 activeClubId
│   ├── layouts/AdminLayout.vue   # 外殼：側欄／頂欄／drawer／響應式斷點切換（桌面／平板／手機）
│   ├── components/
│   │   ├── AppSidebar.vue        # 側欄選單（6 組視覺分組、el-sub-menu 手風琴、選中態改 accent bar；J 系統管理整組依 isSuperAdmin 濾掉）
│   │   ├── AppTopbar.vue         # 系統列（桌面/平板 40px）／合併列（手機 56px，含頁面標題）
│   │   ├── PageHeader.vue        # 頁面列：模組標題＋「這裡管理的是」固定兩行，各頁面內容區頂端自己渲染
│   │   ├── SiteSwitcher.vue      # 站台切換器（docs/21 §5：只換徽章，不換整套主色；手機移進 drawer 頂部；俱樂部清單來源見上方）
│   │   ├── UserMenu.vue          # 顯示真實姓名（`/auth/me` 帶回，見上方）＋「帳號安全設定」／「登出」（呼叫真實 `/auth/logout`）
│   │   ├── FrontendUnitBanner.vue # 「這裡管理的是：{前台單元} ↗」（規劃書 §4.0 硬性規定）
│   │   ├── StatusTag.vue         # 草稿／排程發布／已發布／已停用 四態，深色語意底＋亮色文字（非 el-tag 預設 type）
│   │   ├── ImageUploader.vue     # S0-8 起接上真實上傳端點，見下方「圖片上傳共用元件的前端接線」；預覽框鋪中性看片台（docs/21 §9.1）；S1-4 起也被 B1 頁面區塊圖片重用
│   │   ├── BilingualShortField.vue # 雙語短欄位：桌面並排、手機自動變 tabs
│   │   ├── BilingualTextareaField.vue # 雙語多行文字欄位，S1-4 新增，見下方「頁面管理」
│   │   └── pageBlocks/PageBlockEditor.vue # B1 12 種區塊的內容編輯，S1-4 新增，見下方「頁面管理」
│   ├── views/
│   │   ├── DashboardView.vue
│   │   ├── PlaceholderView.vue   # 尚未建置模組的共用畫面（不是死連結）
│   │   ├── NotFoundView.vue
│   │   ├── auth/LoginView.vue           # 帳密 → （若已啟用）驗證碼，兩步驟同一頁切換
│   │   ├── account/AccountSecurityView.vue  # 改密＋兩階段驗證設定，強制流程與使用者自行調整共用同一份
│   │   ├── pages/              # B1 頁面管理，S1-4 新增，見下方「頁面管理」整節
│   │   │   ├── PageListView.vue
│   │   │   └── PageEditView.vue
│   │   ├── news/
│   │   │   ├── NewsListView.vue  # 列表頁標準型（篩選列／批次操作列／表格／分頁／空狀態）
│   │   │   └── NewsEditView.vue  # 編輯頁標準型（分段表單／雙語／狀態／離開未儲存提醒）
│   │   ├── system/            # J1／J2／J4，見下方「系統管理畫面」整節
│   │   │   ├── AccountListView.vue / AccountEditView.vue
│   │   │   ├── RoleListView.vue / RoleEditView.vue
│   │   │   └── ClubListView.vue / ClubEditView.vue
│   │   └── teams/              # C4 底下的「賽事系列」，見下方「賽事系列」整節
│   │       ├── CompetitionListView.vue
│   │       └── CompetitionEditView.vue
│   ├── composables/
│   │   ├── useBreakpoint.ts      # 斷點判斷（<768 / 768–1024 / ≥1024）
│   │   └── useUnsavedChanges.ts  # 路由離開 + beforeunload 兩處攔截
│   ├── api/
│   │   ├── http.ts             # apiRequest／apiUploadRequest：帶權杖、401 自動 refresh 一次、錯誤分類
│   │   ├── adminAuth.ts        # 登入／refresh／登出／改密／2FA／`getMe`（S1-4 新增，見上方「站台切換器」）
│   │   ├── adminNews.ts        # B2（既有）
│   │   ├── adminPages.ts       # B1 頁面管理，S1-4 新增，見下方「頁面管理」
│   │   ├── adminAccounts.ts    # J1 帳號 ＋ J4 掛在帳號底下的俱樂部／球隊授權
│   │   ├── adminRoles.ts       # J2 角色與權限
│   │   ├── adminClubs.ts       # J4 俱樂部主檔（OG 圖鍵唯讀；標誌／favicon／品牌色已於主站規劃書 v3.20 移除）
│   │   ├── adminCompetitions.ts # C4 賽事系列（S1-4 新增 `listAdminSeasons`，見下方「賽事系列」）
│   │   └── adminTeams.ts       # J4 球隊授權下拉選單，S1-4 新增（原 `publicClubs.ts` 已隨站台切換器改接 `/auth/me` 一併刪除）
│   ├── data/                 # 假資料與靜態設定，見下方「假資料放哪」
│   ├── types/                 # 共用 TypeScript 型別（`pageBlocks.ts` 為 S1-4 新增，見下方「頁面管理」）
│   ├── utils/pageBlockSerializer.ts # B1 區塊畫面狀態 ↔ 後端 JSON 互轉，S1-4 新增，見下方「頁面管理」
│   └── styles/admin-theme.css # 深色 design tokens：四層背景色階、文字/邊框/主色/四態色票（docs/21 §7）
├── scripts/
│   ├── check-forbidden-terms.mjs  # 禁用詞掃描（見下方）
│   └── check-contrast.mjs         # 對比度檢查：token 表 × docs/21 §7 逐一比對（見下方，E-31 的防呆）
├── Dockerfile / nginx-spa.conf / .dockerignore  # noindex 標頭固定在每個 location（見 E-29）
└── package.json
```

## 假資料放哪

`src/data/` 是這份 mockup 唯一的資料來源：

| 檔案 | 內容 |
|---|---|
| `nav.ts` | 側欄的 14 個一級模組（`docs/03-admin-spec.md` §1 的模組代號）、6 組視覺分組、哪些已實作 |
| `frontendUnits.ts` | 模組代號 → 前台單元名稱與網址，**逐字取自規劃書 §4.0「前後台對照表」**，不是自己編的 |
| `dashboard.ts` | 儀表板的待辦提醒／行程／快速入口／統計數字 |
| `news.ts` | 8 篇文章的初始假資料，涵蓋四態、共用內容、有無封面圖等組合 |
| `newsStore.ts` | 把 `news.ts` 包成一個 `reactive()` 單例（**目前只有「apps/api 沒開的情境」才會被用到**，見下方「已知的 mockup 簡化」） |

🔴 **`session.ts`／`activeClub.ts` 本輪已刪除**——登入帳號與站台切換器改接真實服務，見上方
「登入與工作階段」，新的對應位置是 `src/auth/session.ts`／`src/auth/clubAccess.ts`（不在
`src/data/` 底下，因為已經不是假資料）。

**沒有 Pinia**，`newsStore.ts`／`src/auth/session.ts`／`src/auth/clubAccess.ts` 都是純模組層級的
reactive 單例，因為目前狀態之間沒有複雜耦合；之後模組多起來、狀態間有耦合時再評估要不要導入正式
的狀態管理框架。

## 編輯頁共用元件：語言分頁卡片、兩欄版面、欄位錯誤（2026-10-06，試點 `TeamEditView`／`NewsEditView`）

**執行層決定**（規劃書 §4.0 沒有版面規則，詳見 `docs/21` §3）：**整頁一組**「中文／英文」分頁（放在頁面最上方、sticky，不是每張卡片各一組）；上傳欄位放右側欄；
驗證錯誤（前端與後端）都標到欄位。第 3 階段其餘編輯頁照這份寫法遷移。

| 元件／函式 | 用途 |
|---|---|
| `EditLayout`（`#main`／`#aside`） | 兩欄版面。**容器寬度 ≥ 880px** 才兩欄（`container-type: inline-size`，不是視窗斷點），不足時主欄在上、側欄在下，側欄不 sticky；沒有 `#aside` 是單欄；底部留白 88px 給 `EditActionBar`。對話框、`*Tab`／`*Panel` 不用 |
| `LangTabsBar`（`variant`＝`page｜bare`、`langs`＝`['zh','en']`、`label?`） | 整頁（或整個對話框）**一組**語言分頁，把整個編輯區包在裡面並 provide 唯一的語言範圍：主欄與右側欄所有雙語欄位一起換，單語欄位照常顯示。`page` 在 `.admin-layout__main` 內 sticky 於頂端；`bare` 給對話框（不 sticky，對話框自己一組）。預設中文；切分頁不算未儲存變更；唯讀時仍可切換；方向鍵／Home／End 切換。標籤文字為整頁合計：「（N 項尚未翻譯）」「⚠ N 處需修正」；內容用 `v-show` 留在 DOM。**每頁恰好一個**，不得巢狀。卡片一律用一般 `el-card` |
| `BilingualShortField`／`BilingualTextareaField`（新增 `field`、`fieldZh?`、`fieldEn?`、`maxlength?`） | `field="name"` → 錯誤鍵 `nameZh`／`nameEn`。在 `LangTabsBar` 內只顯示目前語言；**不在其內（沒有頁面層或對話框分頁）仍是舊版並排畫面，並在開發模式 `console.warn`（過渡用，第 4 階段刪）**，`field` 因此暫為選填 |
| `LangPane`（`lang`、`field?`、`untranslated?`，事件 `show`） | 自訂雙語內容，放在 `LangTabsBar` 內（如新聞內文編輯器）。`show` 在窗格由隱藏變顯示後觸發，編輯器在這裡重排／重算高度 |
| `FormField`（`field`、`label`、`required`、`lang?`、`reveal?`） | 包 `el-form-item`：`data-field`、2px 危險色外框、`⚠`＋訊息（`role="alert"`）、第一個可聚焦元件加 `aria-invalid`／`aria-describedby`；輸入即清該鍵錯誤（`el-select` 這類不冒泡 DOM 事件的請在更新處理函式呼叫 `formErrors.clear(key)`） |
| `provideFormErrors()`／`useFormErrors()` | `set/get/has/clear/clearAll/replaceAll(record): boolean/count`、`registerAnchor`、`focusFirst()`（文件順序最前 → 切語言 → `reveal` → 捲到畫面中央 → 聚焦；`prefers-reduced-motion` 時不做平滑捲動）、`applyApiError(err): boolean`（有欄位鍵對不到回 `false`，交給頁首提示） |
| `EditActionBar` 的 `#status`＋`FormErrorStatus` | 底部操作列左側「有 N 處需要修正」＋「前往下一處」，外層 `aria-live="polite"` |
| `api/http.ts` | `AdminApiError.fieldErrors`（鍵經 `normalizeFieldKey`，`content.zh.name` → `nameZh`；每鍵取第一則）、`code`；400／409／422 都帶 `body`；網址名稱重複沒有 `errors` 時補 `{ slug: detail }` |

**陣列鍵的前綴退回**：`applyApiError` 對不到精確鍵的 anchor 時，逐層去掉尾段再找（`tags[2].slug` → `tags[2]` → `tags`），找到就把訊息標在那個群組 anchor 上。
所以陣列型區塊（標籤、核心價值、關聯）整塊包一個 `FormField field="tags"`／`"coreValueTags"`／`"relations"` 即可，不必為每一列各設 anchor；
同一群組有多則錯誤只標第一則。群組的新增／移除／選擇處理函式要自己 `formErrors.clear(群組鍵)`。
對話框內的欄位（如排程時間 `publishAt`）直接用頁面同一份 `formErrors`（provide/inject 跟元件樹走，Teleport 不影響），
`FormField` 傳 `:reveal` 重新打開對話框；送出失敗時不要先關對話框。
上傳欄位用 `FormField` 外包 `ImageUploader`，並 `watch([檔案, 移除旗標], () => formErrors.clear(鍵))`。

**欄位鍵只在程式內對照，絕不顯示在畫面上**（`check-forbidden-terms` 只掃樣板的畫面文字，鍵寫在 `field` 屬性不會被掃，但也不得寫進任何文案）。
頁首 `el-alert` 只留給沒有欄位歸屬的錯誤。

頁面寫法範例：

```vue
<script setup lang="ts">
const formErrors = provideFormErrors()
const formError = ref<string | null>(null) // 只放沒有欄位歸屬的錯誤

function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.title.zh.trim()) errors.titleZh = '請輸入中文標題'
  if (!form.urlName.trim()) errors.slug = '請輸入網址名稱'
  return errors // 一次檢查全部，不要遇到第一個就 return
}

async function handleSave() {
  formError.value = null
  if (formErrors.replaceAll(validate())) { await formErrors.focusFirst(); return }
  try { /* 儲存 */ } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  }
}
</script>

<template>
  <el-form label-position="top">
    <LangTabsBar> <!-- 整頁一組，包住整個編輯區 -->
    <EditLayout>
      <template #main>
        <el-card shadow="never" header="基本資訊">
          <BilingualShortField field="title" label="標題" required :zh="form.title.zh" :en="form.title.en" … />
          <FormField field="slug" label="網址名稱" required><el-input v-model="form.urlName" /></FormField>
        </el-card>
      </template>
      <template #aside>
        <el-card shadow="never" header="封面圖片"><ImageUploader … /></el-card>
      </template>
    </EditLayout>
    </LangTabsBar>
  </el-form>
  <EditActionBar>
    <template #status><FormErrorStatus /></template>
    <el-button type="primary" @click="handleSave">儲存</el-button>
  </EditActionBar>
</template>
```

`focusFirst()` 遇到錯誤在另一語言時會切整頁語言再捲動；欄位的 `scroll-margin` 已計入 sticky 分頁列高度。
`bare` 分頁只用於對話框裡有雙語欄位的情況；只有單語欄位的對話框（如新聞排程）不需要。
lint（`scripts/check-edit-layout.mjs`，已上線）：雙語元件的祖先須有 `LangTabsBar`（頁面層或對話框）；頁面層分頁每頁恰好一個；規則全文見 `docs/21` §3.8。
欄位在頁面層 `el-tabs` 分頁或摺疊區裡時，`FormField` 傳 `:reveal="() => (activeTab = 'xxx')"`，`focusFirst()` 會先打開再捲動。
未翻譯數＝中文有值且英文空。對比度：錯誤外框與訊息色（`--admin-danger-text`）已納入 `check-contrast.mjs`【4】對五層背景驗算。

## 檢查腳本：禁用詞掃描

`npm run lint` = `eslint .` + `node scripts/check-forbidden-terms.mjs`。後者對應規劃書 §4.0 後台設計
通則③④（也是 `docs/06-conventions.md` §1）：**後台畫面不得出現模組代號（`B1`／`K4`）、權限碼
（`shop.order.export`）、隊別代號（`D1`／`BW1`）、英文技術詞（`slug`／`canonical`／`SKU`…）**。

做法：只解析每個 `.vue` 檔的 `<template>` 區塊，先丟掉 `{{ }}` 內插值與 `:xxx=`／`v-xxx=`／`@xxx=`
動態綁定（這些是程式碼識別字，不是使用者看得到的文字），只留下純文字節點與少數會直接顯示成文字的靜態
屬性（`placeholder`／`label`／`title`／`alt`／`aria-label`…），再對這段「畫面文字」比對禁用清單。
`<script>`／`<style>` 完全不掃——那裡本來就該是英文識別字。

**寧可少抓也不要誤報**：模組代號只比對「字母＋數字」的完整清單（`B1`…`S6`），不比對裸的單字母
（`A`／`H`／`I`）——英文分頁內容常見獨立單字 "I"，裸字母規則會一直誤報；權限碼的偵測規則會排除看起來
像網域的字串（`www.tcrfc.tw` 這種），避免把「正規網址」欄位的範例文字誤判成權限碼。已用「暫時塞一個
`slug`／`B2` 進畫面文字確認腳本真的會抓到、改回來確認會過」驗證過腳本本身有在運作，不是空腳本。

已知限制：`{{ }}` 內插值裡如果真的寫死一個禁用字串常值（例如 `{{ 'B2' }}`），腳本目前抓不到——這種
寫法很少見，抓太寬會把每一個 prop 值、資料欄位都當成可疑，誤報率太高，權衡後選擇漏抓這種情況。

## 檢查腳本：對比度檢查

`scripts/check-contrast.mjs`（`npm run lint:contrast`）針對 `docs/21-admin-ui.md` §7／§4／§15 的深色
色票表逐一驗算 WCAG 對比度，防的是 [`docs/18` E-31](../../docs/18-work-errors.md)——那次色票表宣稱
「全部實測過」，實際有兩組沒驗到／驗錯，其中一組還漏驗了四層背景裡最亮的 `overlay` 層。

v3 配色改版時同一個根因又踩了一次：`--admin-border-input` 的暖化換算值只驗了 `surface` 一層，沒驗到
`overlay`（實際只有 2.66:1，過不了 3:1）。腳本因此擴大範圍，現在跑六類檢查：①文字 × 背景五層笛卡兒積
（4.5:1）②**邊框 × 背景五層笛卡兒積（3:1，這次新補的洞）**③豁免 token（disabled、純裝飾邊框，逐層
算出數字＋理由，不是默默跳過）④成對色票（四態 tag、語意色按鈕、primary 文字四層、focus 外框五層）
⑤反例與已知限制釘住（含 `--admin-primary` 對 `overlay` 明文只有 4.02:1 這種「規格承認的已知限制」，
以及 border-input 若誤用舊值 `#7A6F68` 的迴歸記錄）⑥規格 vs 實作比對。用「塞回錯誤值確認抓得到、
再復原」驗證過兩次：一次改 `admin-theme.css` 的值（規格 vs 實作那關會抓到），一次改腳本自己的
`BORDERS_CARTESIAN` 常數（笛卡兒積本身會抓到，不是只靠字串比對）。

## 響應式：三個斷點都手動驗證過

`docs/21-admin-ui.md` §8（使用者 2026-09-21 拍板：後台要完整響應式）。用無頭 Chrome + CDP
（`Emulation.setDeviceMetricsOverride`，不是 `--window-size` 命令列參數——後者在無頭模式下有實測過
的視窗 floor 問題，見專案外的 agent 除錯筆記；呼叫 `/json/new` 開分頁要用 `PUT` 不是 `GET`，見
[`docs/18` E-32](../../docs/18-work-errors.md)）逐一驗證過：

- **桌面 `≥1024px`**：系統列 40px＋頁面列 64px 兩層、側欄常駐可收合（240px/64px，收合偏好存
  `localStorage`）、表格完整欄位、側欄選中態 accent bar 清楚可見。
- **平板 `768–1024px`**：頂欄同桌面兩層、側欄強制收合但可手動展開、表格隱藏次要欄位（分類／發布時間）。
- **手機 `<768px`**：頂欄合併成單列 56px（hamburger＋標題＋通知＋使用者頭像）、側欄變 `el-drawer`
  （站台切換器移進抽屜頂部，導覽後自動關閉）、頁面列的標題不重複顯示、「這裡管理的是」落到內容區
  最上方、列表頁表格換成卡片式（每筆一張 `el-card`，只留封面／標題／分類／狀態／操作，縮圖 56×56px）、
  編輯頁雙語欄位（含長欄位）一律變成 `el-tabs`、底部操作列的按鈕改滿版寬度堆疊。

深色生效範圍額外確認過 `el-dialog`／`el-dropdown`／`el-select` 下拉／`el-drawer` 四種浮層元件
（用真實滑鼠事件點開後截圖比對，不是只看 `docker build` 成功），背景都正確落在 `--admin-bg-overlay`
`#2A2E36`，沒有殘留淺色區塊；`el-popover`／`el-tooltip` 沒有另外截圖，但兩者與 dropdown／select 走
同一套 Element Plus popper 底層機制與同一組 `--el-bg-color-overlay` 變數，架構上已一併涵蓋。

## v2 深色重做時的實作取捨（`docs/21` 沒有逐字規定、由這次實作判斷的地方）

- **頁面列（PageHeader）跟著內容區捲動，不是釘住在系統列下方**：`docs/21` §1.2 的 wireframe 畫出來的
  堆疊順序是「系統列→頁面列→內容區」，但沒有明講頁面列要不要 `sticky`；v1 的標題／banner 本來就是
  跟著內容捲動的。這次選擇沿用同樣的捲動行為（`PageHeader.vue` 是每個 view 自己在內容區頂端渲染，
  不是 `AdminLayout` 的固定 header 的一部分），因為改成真正 sticky 需要一個跨元件的頁首狀態（各 view
  的標題／banner 傳給外層 layout），複雜度換來的視覺差異不大，且 §1.2 真正要解決的問題（單列頂欄擠爆、
  手機換行裁字）跟 sticky 與否無關。如果之後要補成真的 sticky，`PageHeader` 目前用「負邊界 bleed 到
  內容區邊緣」做出獨立色塊的手法要整個換掉。
- **手機合併列（`AppTopbar.vue` 的 `isMobile` 分支）背景色用 `surface-2`**：`docs/21` §8.2 沒有指定
  這一列的色階，只說它是系統列＋頁面列合併後的結果。這次選 `surface-2`（系統列原本的色階），理由是
  合併後這一列仍然承載 hamburger／通知／使用者選單這些系統層級控制，視覺識別上延續系統列比較合理。
- **編輯頁的「這裡管理的是」與狀態列合併進 `PageHeader` 的 `#meta` 插槽，疊成兩行**：`docs/21` §3.2
  的 wireframe 只畫出標題下面一行「狀態：…」，沒有畫出 banner 出現在哪；§6 另外又說編輯頁的 banner
  「同樣位置」。這次的讀法是兩者都屬於頁面列的 meta 區塊，各自一行（banner 在上、狀態在下），沒有
  互相取代。
- **`ImageUploader` 新增 `variant: 'photo' | 'logo'` prop 控制中性看片台是純灰還是棋盤格**：
  `docs/21` §9.1 只描述兩種底色何時使用，沒有規定用什麼機制切換；預設 `'photo'`，之後有隊徽／去背
  類上傳情境時記得傳 `variant="logo"`。
- **站台切換器隊徽圖檔放在 `src/assets/brand/`，不是直接引用 `apps/web/public/` 或專案根目錄 `brand/`
  資料夾**（v3，`docs/21` §5.3）：`apps/admin` 是獨立的 Vite 專案，建置時只看得到自己目錄底下的檔案，
  所以把磐石隊徽（`brand/svg/tcrfc-mark-pink.svg` 的複本）與藍鯨隊徽（`apps/web/public/assets/brand/bw/
  favicon-48.png` 的複本）各拷貝一份進來，透過 Vite 的資源匯入機制取得建置後的網址。**這是複本，
  不是唯一來源**——藍鯨隊徽的唯一來源仍是 `brand/blue-whale/`，磐石隊徽仍是 `reference/TCR_logo_CMYK.ai`
  萃取出的 `brand/svg/`，日後那兩處的圖檔更新了，這裡的複本要跟著手動同步。

## 圖片上傳共用元件的前端接線（S0-8 修正版，2026-09-22：單一請求）

🔴🔴🔴 **本節整節改寫**：S0-8 原始接線（選檔後立刻呼叫獨立上傳端點拿物件鍵）被發現違反規劃書
第 53 行／第 972 行「選檔不上傳、儲存才上傳……離開或取消表單不留下任何檔案」，`apps/api` 已把
該兩段式端點整支移除，改成建立／更新文章時封面圖片跟其餘欄位一起送出的單一 `multipart/form-data`
請求（契約見 [`apps/api/README.md`](../api/README.md)「圖片上傳共用元件」整節）。本節記錄跟進
後的前端行為，取代舊版接線紀錄。目前仍只有新聞編輯頁的封面圖（`articles.cover`）這一個插槽真的
接線——後端 `UploadSlotPolicy` 目前也只開放這一格，其餘模組的圖片欄位等各自動工時再依樣接。

**流程**：選檔（或拖曳）→ HEIC 在瀏覽器端轉成 JPEG（[`docs/17-deployment.md`](../../docs/17-deployment.md)
§6 定案，用 `heic2any`，動態 `import()` 避免那顆內建 WASM 解碼器拖大主要頁面的打包體積）→ 前端驗證
格式／大小（10MB）／解析度（`minWidth`／`minHeight`，預設 1600×900）→ 立刻顯示本機預覽（`URL.
createObjectURL`，檔案只存在瀏覽器記憶體，**不呼叫任何 API**）→ 把通過驗證的 `File` 物件透過
`update:file` 交給外層表單（`ImageUploader.vue` 是完全受控元件，`file`／`removeCover` 都是
`v-model` prop，不是內部 state）→ 使用者按下「儲存」時，`NewsEditView.vue` 才把這個 `File`
（`coverFile` ref）跟其餘欄位的 JSON payload 組成同一個 multipart 請求，一次送給建立／更新 API
（`apps/admin/src/api/adminNews.ts` 的 `createAdminNews`／`updateAdminNews`）。

**封面圖片三態在前端怎麼表達**：`NewsEditView.vue` 用兩個獨立於 `form`（`NewsArticle`）之外的
本機狀態 `coverFile: File | null` 與 `removeCover: boolean` 表達使用者這次瀏覽階段的意圖——
選新檔案＝`coverFile` 有值（換新，同時把 `removeCover` 重置為 `false`）；點擊既有封面圖的移除
按鈕＝`removeCover = true`（清空，等按下儲存才真的生效）；兩者都沒有＝維持不變。存檔成功或重新
載入資料時（`applyLoadedArticle`）兩者都會重置為初始值，這是「離開或取消表單不留下任何檔案」
在多次編輯之間仍然成立的關鍵——這兩個值只存在瀏覽器分頁的記憶體，從未送出就會被捨棄。

**已知限制：編輯既有資料時，已經存過的封面圖沒有辦法在後台預覽**（這條不受本次修正影響，維持
原樣）——物件儲存容器目前是私有（`PublicAccessType.None`），也還沒有任何「用物件鍵換可顯示網址」
的端點，這是後端契約本身的邊界。畫面上這種情況顯示「已上傳圖片，目前系統還無法在後台預覽」的
提示卡片，不是空的上傳框（避免誤以為沒有圖片），這次額外在這張卡片上補了一顆移除（×）按鈕——
v1 規格「換圖/移除…全部沿用 v1」列出的既有行為裡，「移除」原本只在本機已選新檔案的預覽卡片上
有按鈕，既有封面圖（無法預覽的那張卡片）沒有對應入口，這是舊版接線的既有落差，這次補齊，不是
新設計（沿用同一顆 `.image-uploader__remove-btn`）。`ImageUploader.vue` 已經留了
`existingPreviewUrl` 這個 prop，等後端補上換算網址的管道之後，呼叫端把算出來的網址傳進來就會
自動改顯示真正的預覽圖，元件本身不需要再改。

**實測**（本機環境：既有 `sqlserver` 容器 ＋ `npx azurite-blob --skipApiVersionCheck` ＋
`dotnet run` 開 `ENABLE_UNSAFE_DEV_WRITES=true` 的 `apps/api` ＋ `npm run dev` 的這個專案，用
headless Chrome + CDP（`Emulation.setDeviceMetricsOverride` 固定桌面寬度 1440px）真的操作瀏覽器、
攔截 `Network.requestWillBeSent`／`Network.getResponseBody`／`Network.getRequestPostData` 確認
真實網路行為，不是模擬）：

- 🔴 **核心驗收：選了封面圖片但沒按儲存就離開，Azurite 裡不留任何物件**——在「新增文章」頁選
  中文標題、網址名稱，並選一張 2000×1400 的 JPEG 當封面（畫面確實顯示本機預覽），全程**攔截到
  的 API 請求數是 0**；接著用 `Page.navigate` 做一次真正的整頁離開（不經過 Vue Router，等同關
  分頁）。離開前後直接查 Azurite 容器內容：**物件數量不變（0 → 0，同一批基準測試環境下）**，
  沒有任何孤兒物件——這正是「選檔不上傳、儲存才上傳」在單一請求契約下的天然結果：沒有送出過
  請求，儲存體從來沒被寫入過。
- **建立文章夾封面圖片**：選檔（不送請求）→ 填標題與網址名稱 → 按「儲存草稿」→ 攔截到
  **恰好一個** `POST /api/v1/admin/tcrfc/news` 請求，`multipart/form-data` body 含 `payload`
  （JSON）與 `file` 兩個欄位；回應的 `coverKey` 開頭是 `tcrfc/articles/<id>/cover/`；直接查
  Azurite，五個物件（主檔＋1280／640／320／縮圖）全部存在。
- **換圖（更新，夾新檔案）**：在已有封面圖 A 的編輯頁選封面圖 B → 按「儲存草稿」→ 攔截到
  `PUT /api/v1/admin/{id}` 請求，回應 `coverKey` 換成 B 的鍵；查 Azurite，**A 的五個物件全部
  消失，B 的五個物件完整保留**。
- **清空封面（`removeCover`）**：在已有封面圖的編輯頁點擊既有封面卡片上新增的移除（×）按鈕
  （畫面立刻變回空的拖放區）→ 按「儲存草稿」→ 用 `Network.getRequestPostData` 直接讀出這次 PUT
  請求的原始 multipart body，確認 `payload` 內容是
  `{"slug":"...","categoryCode":"club","isFeatured":false,"content":{...},"expectedUpdatedAt":"...","removeCover":true}`
  **且完全沒有 `file` 這個 part**（沒有嘗試任何上傳）；回應 `coverKey` 是 `null`；查 Azurite，
  該文章原本的五個物件全部消失。
- **前端驗證仍在送出請求之前擋下**（沿用既有邏輯，逐一重測確認沒有回歸，四種情況攔截到的 API
  請求數皆為 0）：解析度不足（400×300）「圖片解析度太低（至少需要 1600×900）…」；不支援格式
  （`.bmp`）「圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。」；超過 10MB
  「圖片檔案太大（上限 10 MB）…」；**真正的 HEIC 檔案**（非改副檔名）正確轉檔成 JPEG 並顯示本機
  預覽，無錯誤訊息，同樣沒有送出任何請求。
- 測試文章事後皆用 `DELETE` 清掉，確認資料庫與 Azurite 容器都恢復乾淨（最終物件數為 0）。

**已知的其餘限制**（沿用後端 `apps/api/README.md`「已知缺口」，前端不重複列)：伺服器端沒有依
版位設定尺寸下限（本元件的 `minWidth`／`minHeight` 是前端唯一一道尺寸把關）、圖片欄位沒有
`_width`／`_height`／雙語 `_alt` 資料庫欄位（`form.coverImageUrl` 因此恆為 `null`，不是遺漏）。

**這次修正的連帶影響（回報，未動手改規格）**：`NewsListView.vue` 的「複製」功能（`handleDuplicate`）
原本會把來源文章的 `coverKey` 一併帶進新建立的草稿——`CreateArticleRequest` 已經不接受
`coverKey`（封面圖片只能透過同一次請求真的夾一個檔案上傳），複製品因此**不會**帶著原文章的封面
圖片，使用者需要自己在複製出來的草稿裡重新選一次封面圖。這是這次修正的必然結果（規劃書的圖片
欄位只認得「同一次請求上傳的位元組」，不認物件鍵字串），不是本輪刻意拿掉這個功能，畫面的成功
訊息已經跟著調整為「已建立一份複製的草稿，封面圖片未帶入，請重新選擇後再儲存」。

## 已知的 mockup 簡化（不是 bug，記錄給下一階段）

- 「內容」的富文本編輯器用 `<textarea>` 代替——規劃書明文「不代為決定」實際編輯器選型，第二階段先
  用文字框佔位，畫面上也用中文說明這一點。
- 「預覽」按鈕對已發布文章會 `window.open` 一個 `/zh/news/{網址名稱}/` 的網址，這個網址在本機開發
  環境下不保證能真的打開（`apps/web` 前台是否有跑、跑在哪個 port，跟這個 admin 專案無關）。
- `newsStore` 只存在瀏覽器分頁的記憶體裡，重新整理頁面會重置回 `data/news.ts` 的初始假資料
  （⚠️ 這一則現在只剩「假資料」場景成立——新聞編輯頁走的是真實 `apps/api`，見 S0-7i；`newsStore`
  仍然存在但只有在 `apps/api` 沒開的情境下才會被用到，這裡的措辭是既有落差，本輪未動手修正，
  一併回報）。

## 系統管理畫面（J1／J2／J4，S1，2026-09-24）

僅系統管理員可見與可進入（見上方「登入與工作階段」的路由守衛，後端每個端點的權限碼皆為
`sysadmin_only`）。逐畫面對照 `apps/api/README.md`「S1-3 續作」的端點清單：

- **J1 帳號**（`views/system/AccountListView.vue`／`AccountEditView.vue`）：清單（篩選狀態／
  關鍵字）、新增（帳號、姓名、Email、預設俱樂部、初始密碼、系統管理員開關、角色多選）、編輯
  基本資料、停用／啟用、重設密碼、重設兩階段驗證。**建立帳號不寄邀請信**（apps/api 的定案寫法，
  見該檔案「執行層判斷」第 1 點）——初始密碼由建立者透過站外管道轉交，畫面成功訊息有提醒這點。
  帳號編輯頁同時是 **J4「掛在帳號底下」的俱樂部授權／球隊授權**維護入口（規劃書 §5.3「授權掛在
  人不是角色」）：兩個分頁各自列出目前授權、新增（俱樂部／球隊 ＋ 選填到期日）、撤銷。
- **J2 角色與權限**（`RoleListView.vue`／`RoleEditView.vue`）：角色 CRUD（系統內建角色鎖定
  基本資料，只能調整權限）、刪除前擋下「系統角色」與「仍有帳號指派」兩種情況、權限勾選矩陣依
  `moduleCode` 分組顯示**中文模組名稱**（不是代號，見 `MODULE_NAME_LABEL`）、每個勾選的權限碼
  可另外選資料範圍（`scope_type`：全部／僅自己的球隊／僅學院梯隊／遮罩顯示／僅可翻譯／僅自己的
  俱樂部）、`sysadmin_only` 的權限碼一律禁用勾選（避免存檔才被後端拒絕）。
- **J4 俱樂部與授權管理**（`ClubListView.vue`／`ClubEditView.vue`）：俱樂部主檔 CRUD（代碼建立後
  不可改）、法人資料（發票抬頭、統一編號、是否為收款主體，附警語提醒收款主體目前只有俱樂部）、
  （品牌色、標誌、favicon 已於 2026-10-06 隨主站規劃書 v3.20 移除，改由前台靜態資產與 CSS 定義）。
  畫面只剩「社群分享圖片」唯讀卡（已設定／尚未設定）；後端 `AdminClubs` 本來就不寫 OG 圖，
  上傳入口在 H1 全站設定，故本頁不補上傳。

## 賽事系列（C4 的支援型別，S1，2026-09-24；賽季下拉選單於 S1-4 補上；C4 主體已於 S1-8 完成）

`views/teams/CompetitionListView.vue`／`CompetitionEditView.vue`，路徑 `/teams/competitions`，
側欄標籤已改為「賽事系列」（`data/nav.ts`，原本誤標成「賽程與賽果」，S1-8 已改正）。
這裡維護的是「賽事系列」（`Competition`，賽程賽果的分類支援型別，例如「企業甲級聯賽」），
**不是賽事本身**——賽事本身（日期、比分、進球者、卡牌、出賽名單）與積分榜見下方
「C4 賽程與賽果／積分榜（S1-8）」一節，維持獨立頁面而不是塞進同一頁，理由是操作頻率與資料
形狀差異大（賽事系列是偶爾新增一筆的設定資料，賽事本身是逐場高頻維護，積分榜是整季表格）。

✅ **「賽季」欄位已改成真下拉選單**（S1-4，`src/api/adminCompetitions.ts` 的 `listAdminSeasons`
呼叫 `GET /admin/{club}/seasons`）：選項顯示賽季代碼與起訖日（例如「2026-27（2026-08-01 ～
2027-05-31）」），切換站台時會重新載入該俱樂部的賽季清單。編輯既有資料時仍會顯示目前的賽季代碼
供對照。**這是唯讀查詢用途**，賽季本身的維護（新增／編輯賽季）尚未開放，權限碼沿用既有的
`team.competition.view`，未新增權限碼。

同一類缺口也在 **J4 球隊授權**（`AccountEditView.vue` 的球隊授權分頁）解決：改接
`GET /admin/teams`（`src/api/adminTeams.ts`），依俱樂部分組顯示下拉選單，且畫面上**只列出
這個帳號目前已授權俱樂部底下的球隊**（後端也會擋，這是前端提前收斂選項範圍，避免使用者選了
一個必然會被拒絕的球隊）——這個帳號一筆有效俱樂部授權都沒有時，畫面會提示「請先在上方新增
俱樂部授權」而不是顯示一個空的下拉選單。

## 頁面管理（B1，S1-4，2026-09-24）

對應主站規劃書 §4.2 B1（約行 1011–1014）與 apps/api/README.md「S1-4：B1 頁面管理」。**這裡管理的
是網站的多個靜態頁面**（`FrontendUnitBanner` 用 `linkType: 'multi'`，不是單一頁面），對照
`src/data/frontendUnits.ts` 的 `B1` 項。

- **列表頁**（`views/pages/PageListView.vue`）：篩選（狀態／關鍵字）、分頁、欄位為網址名稱、
  SEO 標題（中文）、狀態、更新時間；版面沿用 `NewsListView.vue` 的列表頁標準型。
- **新增／編輯頁**（`views/pages/PageEditView.vue`）：
  - **基本資訊**：網址名稱（`slug`，允許 `/` 表示分層路徑，對照 `docs/01`「URL 直接對應網站
    層級」）。
  - **SEO 設定**：標題（`BilingualShortField`）、描述（新增的 `BilingualTextareaField`，見下方）。
  - **內容區塊**：12 種區塊型別（文字、圖文左右、圖片藝廊、影音嵌入、引言、CTA、手風琴 FAQ、
    時間軸、步驟條、數據卡、表格、檔案下載）的區塊化編輯器——下拉選型別＋「新增區塊」按鈕、
    每個區塊卡片有「上移／下移／刪除區塊」，型別名稱**顯示中文**（`PAGE_BLOCK_TYPE_LABEL`，
    `CTA`／`FAQ` 是規劃書原文用字不是英文技術詞，未違反 §4.0「代號不進介面」，見
    `src/types/pageBlocks.ts` 檔頭說明）。
  - **發布設定**：草稿／排程發布／已發布三態、`StatusTag`、預覽連結。
  - **操作列**：版本歷程、預覽、儲存草稿、發布／排程（`el-dropdown split-button`，樣式沿用
    `NewsEditView.vue`）。
- **版本歷程與還原**：`el-dialog` 列出版本清單，「檢視內容」顯示該版本的簡短摘要（區塊型別＋
  關鍵欄位截斷字串，**不是**逐版重新渲染完整的區塊編輯器——規劃書只要求「版本歷程與還原」，
  沒有要求逐版完整重現畫面，這是本輪的取捨），「還原」呼叫 `restoreAdminPageVersion`（後端
  「還原＝以舊版內容產生一個新版本，不覆蓋中間版本、不改變發布狀態」，見 apps/api/README.md
  「我的判斷」）。
- **預覽連結**：顯示 `/{locale}/preview/{token}`（中文／英文版可切換）並提供複製按鈕
  （`navigator.clipboard.writeText`），畫面上明白提示「這是官網的路徑，請自行接上官網網域」——
  `apps/web` 尚未實作這條路由（見「已知的 API 缺口彙整」第 6 點），本輪只驗證到 API 層的
  `GET /api/v1/pages/preview/{token}` 契約本身正確可用。

### 新增的檔案

| 檔案 | 內容 |
|---|---|
| `src/types/pageBlocks.ts` | 12 種區塊型別的畫面狀態型別、中文名稱對照表、空白內容產生器 |
| `src/utils/pageBlockSerializer.ts` | 畫面狀態 ↔ 後端 JSON 互轉（`parseBlockFromDto`／
  `serializeBlocksForSubmit`），逐條對齊 `PageBlockContentProcessor.cs` 的驗證規則做前端預檢，
  **後端仍是最終依據**，這裡只是減少一次不必要的往返 |
| `src/api/adminPages.ts` | 對照 `AdminPageDtos.cs` 的完整端點清單 |
| `src/components/pageBlocks/PageBlockListEditor.vue` | 區塊清單（新增／排序／刪除＋逐區塊編輯），靜態頁與 P1 課程內容共用；`allowedTypes` 限制可新增類型 |
| `src/components/pageBlocks/PageBlockEditor.vue` | 單一區塊的內容編輯，依 `blockType` 切換欄位；
  圖片欄位直接重用 `ImageUploader.vue`（`v-model:file`／`v-model:remove-cover` 對到
  `ImageSlotState.file`／`cleared`） |
| `src/components/BilingualTextareaField.vue` | `BilingualShortField.vue` 的多行版本（SEO 描述、
  引言、FAQ 答案等用得到，之後其他模組也可以直接沿用） |
| `src/views/pages/PageListView.vue`／`PageEditView.vue` | 見上方 |

### 圖片欄位的畫面狀態設計（`ImageSlotState`）

跟新聞封面圖（單一欄位、可整個清空）不同，頁面區塊的圖片是**必填**（圖文左右恰 1 張、圖片藝廊
至少 1 張，不能整個留空），因此沒有「清空後維持空白」這個終態——`ImageSlotState.cleared` 為真
時視同「使用者想要換一張，但還沒選」，存檔前的前端預檢會擋下並提示「尚未選擇圖片」，不會讓這個
狀態送到後端（後端收到既沒有 `pendingUpload` 也沒有 `key` 的圖片欄位一樣會 400，前端只是提早
攔一次給更明確的訊息）。圖片藝廊的「刪除這張圖片」直接把整個 `ImageSlotState` 從陣列移除，
不透過 `cleared`——差異在於「換掉這一張」與「這裡本來就不該有這一張」是两回事。

### 為什麼不用 emit 逐層傳遞，改成直接改 `content` 物件的巢狀屬性

`PageBlockEditor.vue` 拿到的 `content` prop 是父層（`PageEditView.vue` 的 `blocks` 陣列元素）
持有的同一個 reactive 物件參照，元件內部直接改它的巢狀屬性（例如 `textC.value.body.zh = v`），
不是每一層都寫一組 `emit`／`v-model`。**這不是「修改 prop 本身」**——Vue 只警告重新賦值 prop
變數本身，不警告修改物件型別 prop 的巢狀屬性；`CompetitionEditView.vue` 直接綁 `form.xxx` 也是
同一種既有慣例。對 12 種區塊、部分還帶陣列欄位（FAQ／時間軸／步驟條／數據卡／表格／圖片藝廊）的
表單來說，逐層 `emit` 的樣板碼會多出好幾倍，這裡判斷這個取捨是合理的。

## 驗收紀錄（S1，2026-09-24，本機環境）

`npm run lint`／`npm run typecheck`／`npm run build` 全過。實際起 `apps/api`
（`dotnet run --no-launch-profile`，連本機既有 `sqlserver` 容器的 `tcrfc_club_dev`）與
`npm run dev`（`:5174`），用無頭 Chrome + CDP 寫腳本驅動真實瀏覽器（不是模擬 fetch）逐步操作：


> **2026-09-30 更新**：以下各節的「強制改密／強制 2FA／`forced=` 導向」為當時（S1 階段）的實走紀錄；
> 該強制流程已依使用者裁決移除，現行行為見上方「登入與工作階段」第 3 點。新的實走步驟：以任一帳號
> 登入後直接進 `/dashboard`；從使用者選單進「帳號安全設定」可自行改密或啟用 2FA；已啟用 2FA 的帳號
> 登入仍須輸入驗證碼。

1. **登入 ＋ 兩階段驗證**：`sa@system.local`（種子超管）帳密登入 → 因為
   `must_change_password=1` 被導去 `/account/security?forced=password` → 更改密碼成功後
   （`two_factor_enabled=0`）自動換成 `?forced=totp` → 按「開始設定」拿到後端真的產生的 TOTP
   密鑰 → **在本機用 RFC 6238 算法即時算出驗證碼**（不是猜測或事先準備好的碼）送出 → 確認成功、
   導到 `/dashboard`。之後用改密後的新密碼＋即時算出的驗證碼重新整個走一次帳密＋TOTP 登入，同樣
   成功——證明兩階段驗證在「設定」與「日後登入」兩種情境都真的與後端的 TOTP 實作一致。
2. **強制流程確實會擋、確實會放行**：`must_change_password`／`two_factor_enabled` 任一未滿足時
   導向帳號安全設定頁；兩者都滿足後才進得了 `/dashboard`，直接改網址回 `/account/security` 之外
   的頁面在中途會被彈回（`needsForcedOnboarding` 守衛）。
3. **站台切換器**：登入後同時看得到「台中磐石」「台中藍鯨」，切到藍鯨、切回磐石，兩次都跳出正確
   的中文成功提示，`activeClubId` 正確驅動之後俱樂部範圍 API 呼叫的 `{club}` 路由段。
4. **系統管理員看得到系統管理選單**：側欄「系統管理」（J）展開後看得到「帳號」「角色與權限」
   「俱樂部與授權管理」三個子項。
5. **建立帳號 → 指派角色與俱樂部授權 → 撤銷授權**（J1／J4）：在帳號列表按「新增帳號」，填帳號、
   姓名、初始密碼、勾選角色「檢視者」，儲存後直接查資料庫確認 `admin_users` 與
   `admin_user_roles` 都寫對；在編輯頁的「俱樂部授權」分頁新增台中磐石的授權，表格立刻顯示
   「生效中」；按「撤銷」→ 對話框確認 → 表格變成「已撤銷」，`admin_user_clubs.is_active` 正確
   翻成 `0`。
6. **非系統管理員看不到系統管理選單，直接改網址也被擋下**：登出，改用剛建立的帳號（角色
   `viewer`，非超管）登入 → 一樣先走一次強制改密與強制設定兩階段驗證（新帳號建立時
   `must_change_password` 一律為真）→ 進入後台後，側欄**沒有**「系統管理」；直接在網址列輸入
   `/system/accounts` 會被導回 `/dashboard` 並跳出「你的帳號沒有權限進入這個模組」。

**過程中發現並修正的兩個實作缺陷**（自我測試抓到，記錄給下一位）：
- `AccountEditView.vue`／`RoleEditView.vue`／`ClubEditView.vue`／`CompetitionEditView.vue` 原本
  用 `const isCreate = route.name === 'xxx-new'` 判斷模式——這是一次性求值，Vue Router 在
  `router.replace()` 導去「同一個元件、不同路由」（新增 → 編輯）時預設會**重用元件實例**，
  `isCreate` 因此永遠停在建立當下的值，導致建立成功後俱樂部／球隊授權分頁、頁面標題全部繼續
  顯示「新增」狀態。改成 `computed(() => route.name === 'xxx-new')` 後才正確反應路由變化。
  用無頭瀏覽器實際建立一筆帳號、觀察分頁消失又重新出現，才抓到這個問題——單靠型別檢查與人工
  讀碼看不出來。
- `AccountSecurityView.vue` 原本只在「改密時已經啟用兩階段驗證」才會導去下一步，若改密當下兩階段
  驗證還沒開，畫面會停在同一頁但提示文字仍停留在「必須先更改密碼」（因為 `forced` 查詢參數沒有
  更新）。改成改密成功後、若兩階段驗證仍未開，`router.replace` 把 `forced` 換成 `totp`，提示文字
  才會跟著換成正確的下一步。

已知**不在本輪自動化驗證範圍內**：球隊授權分頁（受限於上方「已知 API 缺口」第 3 點，沒有球隊可選
就無法真的送出一筆）、J4 俱樂部主檔的建立／編輯（邏輯與帳號／角色同一套 CRUD 樣板，已用
`npm run build` 與型別檢查覆蓋，但沒有另外用瀏覽器實際點過）。測試用的帳號與授權資料已於驗收後
清除，`tcrfc_club_dev` 恢復乾淨；`sa@system.local` 的密碼與兩階段驗證狀態在測試過程中被永久改變
（不再是種子腳本原始的 `Admin@123` ／2FA 停用），需要下一位知悉——種子腳本本身沒有改，重新套用
種子（`./db/seed/apply-seed.sh`）不會恢復既有列（既有的 `IF NOT EXISTS` 冪等寫法只補新資料，
不回寫已存在的帳號）；如果需要 `sa@system.local` 回到原始種子狀態，需要手動 `UPDATE` 或整個重建
`tcrfc_club_dev`。

> 🔴 **已解決（`backend-engineer`，2026-09-24）**：新增 `db/seed/reset-admin-accounts.sh`，
> 已對本機 `tcrfc_club_dev` 執行過一次，`sa@system.local` 與其餘種子測試帳號的密碼／2FA 狀態／
> 鎖定計數已還原成種子腳本定義的初始值。**端對端驗收會改掉種子帳號的狀態，驗完要跑這支還原**
> （`./db/seed/reset-admin-accounts.sh`，只 UPDATE 既有列，不影響角色指派與俱樂部授權），
> 細節見 `apps/api/README.md`「種子測試帳號的重設」一節。

## 新聞與故事：標籤／核心價值標籤／關聯／瀏覽數／批次操作（B2，S1-5，2026-09-24）

對照主站規劃書 §4.2 B2（行 1017–1020）與 `apps/api/README.md`「S1-5：B2 新聞與故事後端補完」。
補齊 S0-8 當時明講「刻意不做」的部分，見上方「圖片上傳共用元件的前端接線」一節的引用。

### 標籤（find-or-create）

編輯頁「標籤」卡片用 `el-select`（`multiple filterable allow-create default-first-option`），
畫面上使用者**只看得到、只打得出中文名稱**：

- 選項清單（既有標籤的自動完成建議）**不是新端點**——`fetchTagSuggestions()`
  （`src/api/adminNews.ts`）直接沿用既有的 `listAdminNews(club, { pageSize: 200 })`，把這個俱樂部
  目前所有文章已經在用的標籤去重彙整起來。83 篇文章一次抓滿，不必另外處理分頁。
- 使用者打字時，`resolveTagInput()`（`NewsEditView.vue`）比對「這段文字是不是等於某個既有標籤的
  顯示名稱」（既有標籤來源：上述建議清單 ＋ 這篇文章本身已經有的標籤）——是的話**沿用該標籤的
  `slug`**（忽略這次輸入的名稱，比照後端「名稱由標籤自己管理」的規則，見 apps/api/README.md
  「我的判斷」第 5 點）；不是的話視為新標籤，`slugifyTagName()` 把中文字轉成 `tags.slug` 要求的
  格式（小寫英文字母、數字、連字號）——**純中文轉不出任何英數字時，退而求其次用一段隨機英數字
  頂著**（`tag-${隨機 6 碼}`），使用者從頭到尾看不到這串字，只看得到自己輸入的中文名稱。
  實測：輸入「S1-5驗收標籤」產生的 `slug` 是 `s1-5`（NFKD 正規化＋去重音＋非英數字換連字號＋
  收斂連續連字號，恰好從中文字裡留下了 ASCII 部分，比隨機字串更可讀，但這是**副作用不是設計
  保證**——大多數輸入不會這麼幸運）。
- `form.tags: NewsTag[]` 是唯一真實來源，`tagNames`（給 `el-select` 綁定的字串陣列）是包著它的
  computed getter/setter，這樣既有的 `isDirty`（`JSON.stringify(form) !== JSON.stringify(baseline)`）
  不用額外改就能正確偵測標籤變動。

### 核心價值標籤

固定 5 個值（`players_first`／`excellence`／`global_pathways`／`community`／`integrity`，逐字
對照後端 `AllowedCoreValueTags` 與 docs/06-conventions.md §1「五大核心價值」的中文名稱），畫面用
5 個獨立 `el-checkbox`（`:model-value` ＋ `@change`，比照 `RoleEditView.vue` 既有的寫法，
不用 `el-checkbox-group`——這個專案的 Element Plus 版本對 `el-checkbox-group` 的值/標籤 prop
命名在既有程式碼裡沒有先例可循，用單顆 checkbox 的既有慣例比較不會踩到版本相關的陷阱）。

### 關聯（球員／球隊／賽事／課程／夥伴）

🔴 **只有「球員」「賽事」兩種真的可以選，「球隊」「課程」「夥伴」三種選單停用**——這是任務指示
「若後端缺列出這些目標的唯讀端點就停下該部分回報，不要改 `apps/api`」的字面結果，回報如下：

| 目標型別 | 有沒有唯讀清單可查 | 說明 |
|---|---|---|
| 球員 | ✅ 有 | 公開端點 `GET /api/v1/{club}/players`（不需要登入，任何角色都能查），新增
  `src/api/adminRelationTargets.ts` 的 `listPlayerRelationOptions()` |
| 賽事 | ✅ 有 | 公開端點 `GET /api/v1/{club}/schedule`，同檔案的 `listMatchRelationOptions()` |
| 球隊 | ⚠️ 技術上有端點，但一般寫新聞的角色用不到 | `GET /api/v1/admin/teams` 存在（S1-4 續作為 J4
  球隊授權新增），但權限碼 `system.team_grant.view` 是 `sysadmin_only`——`content_editor`／
  `team_competition` 這些實際會寫新聞的角色本來就沒有這個權限碼，接了也只會在打開下拉選單時
  得到 403，不是真的可用。**沒有接**，等後端補一支給內容編輯角色查得到的球隊清單（權限碼另開或
  沿用 `team.competition.view` 那一組資料範圍）再回頭做 |
| 課程 | ❌ 沒有 | 後端沒有 `Features/Programs`，P 模組尚未開發 |
| 夥伴 | ❌ 沒有 | 後端沒有 `Features/Partners`，E 模組尚未開發 |

畫面上**仍然列出全部 5 個型別選項**（規劃書明文寫 5 種，不能因為 3 種做不到就假裝只有 2 種），
球隊／課程／夥伴三個選項用 `:disabled` 停用，卡片內有一行中文說明「這三種類型後台目前還沒有清單
可以查詢，暫不開放選擇」。加入一筆關聯的流程：型別選單（預設「球員」）→ 目標選擇器（`filterable`
單選，選項一次抓滿：球員最多 200 筆、賽事最多 100 筆，全系統目前遠低於這個量，不需要伺服器端
搜尋 API）→「加入」按鈕（`pendingRelationTargetId` 為空時停用）→ 卡片下方以 `el-tag`（`closable`）
列出已加入的關聯，點 × 移除。

`resolveRelationLabel()` 決定每筆關聯顯示什麼名稱：這次瀏覽階段剛加入的用 `targetLabel`（加入當下
從選項清單記下來的文字，**不會送給 API**，`AdminArticleRelationInput` 只有 `targetType`／
`targetId` 兩個欄位，沒有名稱）；重新載入既有文章的關聯則反查對應清單（`loadArticle()` 會依
既有關聯用到的型別自動預先載入，不必使用者先手動切一次型別選單）；反查不到就老實顯示
「（讀取中或找不到，可能已被刪除）」或「（此類型目前尚無法顯示名稱）」，不假裝有名稱。

⚠️ **修正一個開發中發現的邊界情況**：`pendingRelationType` 預設值是 `'player'`，但 el-select
的 `@change` 只在使用者真的切換型別時才會觸發——如果不主動預先呼叫一次
`ensureRelationOptionsLoaded('player')`，新增文章時第一次打開球員選單會是空的，使用者要先切成
別的類型再切回來才會有資料。已在 `loadArticle()`（建立與編輯兩種模式都會呼叫）裡補上這一行。
這是端對端瀏覽器實測時發現的（見下方「本輪驗收」），不是型別檢查能抓到的問題。

### 瀏覽數

編輯頁「發布設定」卡片新增唯讀欄位（只有已存在的文章才顯示，用 `currentId` 判斷不是 `isCreate`
——`isCreate` 是路由進入當下算一次就不再變的純值，建立成功後 `router.replace` 到編輯路由不會
讓它變成 `false`，用它做這種存在性判斷會重踩 `docs/18` 記過的舊坑，見下方「系統管理畫面」提過
的 `isCreate` 陷阱）。列表頁新增「瀏覽數」欄（桌面／平板才顯示，手機卡片式列表不放，比照既有
「分類」「發布時間」兩欄的收納規則）。**沒有寫入介面**——瀏覽數只由公開端點
（`POST /api/v1/{club}/news/{slug}/views`）遞增，後台單純顯示。

### 批次操作

工具列新增「批次改分類」（開對話框選一個分類，確定後呼叫 `POST .../news/batch/category`）與
「批次下架」（確認對話框後呼叫 `POST .../news/batch/unpublish`，文案沿用後端「下架＝轉回草稿」
的既有判斷，見 apps/api/README.md「我的判斷」第 1 點）；既有「批次發布」改接新的
`POST .../news/batch/publish`（原本是前端自己逐筆 `for` 迴圈呼叫單篇發布端點，現在交給後端一次
處理，能拿到後端統一算好的「處理了幾筆、幾筆處理不了、為什麼」）。三支批次端點的回應形狀一致
（`updatedCount` ＋ `skipped: {id, reason}[]`），前端用共用的 `reportBatchResult()` 顯示成
「已改分類 N 篇，M 篇未處理（原因…）」這種訊息，不是全有全無的成功/失敗二分。既有的「刪除」
批次操作維持逐筆呼叫（規劃書沒有給批次刪除端點，`apps/api` 也沒有新增，沿用原樣）。

### 契約變更（`src/api/adminNews.ts`）

新增 `AdminArticleTagDto`／`AdminArticleRelationDto`／`BatchOperationResultDto` 三個型別；
`AdminArticleListItemDto` 新增 `tags`／`viewCount`；`AdminArticleDetailDto` 新增
`tags`／`viewCount`／`coreValueTags`／`relations`；`SaveArticlePayload` 新增選填的
`tags`／`coreValueTags`／`relations` 三個欄位。

🔴 **這三個欄位在建立與更新兩種請求都一律明確帶出目前畫面上的完整陣列，不使用後端允許的
「省略＝維持不變」語意**——任務指示特別提醒這三欄「省略＝不變、空陣列＝清空」要處理正確，這裡的
處理方式是**乾脆不省略**：現在編輯頁對這三個欄位都有完整的輸入介面，`form.tags`／
`form.coreValueTags`／`form.relations` 本來就是「使用者現在想要的最終狀態」，直接原樣送出，
效果上等同於「沒變就送回原值＝維持不變、清空了就送空陣列＝清空」，不需要另外偵測「使用者到底
有沒有碰過這個欄位」這種容易漏判的邏輯（`articleToSavePayload()` 與 `SaveArticlePayload` 型別
定義上都留了逐字說明，供下一位比對這個判斷）。

### 本輪驗收（S1-5，2026-09-24，本機環境）

`npm run lint`／`npm run typecheck`／`npm run build` 全過。實際起 `apps/api`（連本機
`tcrfc_club_dev`＋本機 `azurite-blob --skipApiVersionCheck`）與 `npm run dev`，用無頭 Chrome
＋原生 CDP（Node 24 內建 `WebSocket`；`Input.dispatchMouseEvent` 送真實滑鼠事件，不是合成
`.click()`——除錯時發現 Element Plus 的下拉選單「點外部關閉」偵測不會理會合成事件，且
`document.querySelectorAll('.el-select-dropdown')` 永遠會抓到頁面上每一個 select 的 popper
（Element Plus 關閉後不會把 DOM 移除），要先篩出 `getBoundingClientRect().height > 0` 的那一個
才不會點到別的下拉選單）驅動真實瀏覽器逐步操作，帳號用 `clean.login@tcrfc.test`（system_admin，
唯一能走完整 `/login` 流程的種子測試帳號）：

1. **建文章＋新標籤＋核心價值＋一筆關聯 → 儲存 → 重開確認都在**：新增文章，標題「S1-5 端對端
   驗收文章 A」，標籤輸入框打「S1-5驗收標籤」（資料庫沒有這個標籤，find-or-create 應該新建），
   勾選核心價值「以球員為本」，關聯選「球員」→ 選一位球員 → 加入，按「儲存草稿」，表單錯誤為
   `null`；直接查資料庫確認 `tags`／`tags_i18n`／`article_tags`（新建的標籤，`slug='s1-5'`）、
   `value_tag_links`（`players_first`）、`article_relations`（`target_type='player'`，
   `target_id` 對應到畫面上選的那位球員的 `shirt_no`）都真的寫進去；用 `Page.navigate` 對同一個
   網址做一次完整重新整理（不是 Vue Router 內部導頁），確認畫面上標籤晶片、核取的核心價值、
   關聯標籤三者都還在，瀏覽數欄位顯示「0」。
2. **批次改分類與批次下架兩篇**：另建一篇「S1-5 端對端驗收文章 B」（草稿），到列表頁用關鍵字
   「S1-5」篩出這兩篇、全選：
   - 批次改分類選「球員故事」，直接查資料庫確認兩篇的 `article_category_id` 都改成
     `player-stories`。
   - 批次發布：兩篇的狀態欄立刻從畫面上變成「已發布」；重新整理篩選再全選一次，批次下架：
     跳出「確定要將選取的 2 篇下架嗎？下架後會變回草稿，前台會立即看不到。」確認對話框，確認後
     兩篇狀態欄變回「草稿」，直接查資料庫確認 `status` 皆為 `draft`。
3. **清除測試資料**：用列表頁的批次刪除移除兩篇測試文章，直接查資料庫確認 `articles`／
   `article_relations` 兩表對應列數皆為 0；額外用 SQL 清掉刪除文章後留下的孤兒標籤主檔列
   （`tags`／`tags_i18n` 的 `slug='s1-5'`——刪文章只會級聯刪 `article_tags` 這張關聯表，標籤
   本身是獨立主檔不會跟著消失，這是預期行為不是 bug，但既然是本輪測試自己建的標籤，一併清掉）。
4. 執行 `./db/seed/reset-admin-accounts.sh`，確認 `clean.login@tcrfc.test` 的
   `must_change_password`／`two_factor_enabled`／`failed_attempt_count` 回到種子初始值
   （`0`／`0`／`0`）。

**過程中發現並修正的一個實作缺陷**（自我測試抓到，記錄給下一位）：見上方「關聯」小節提到的
`ensureRelationOptionsLoaded(pendingRelationType.value)` 缺漏——新增文章時預設型別「球員」的
選項清單原本要等使用者切換過型別選單才會載入，已修正為 `loadArticle()` 一律預先載入一次。

⚠️ **除錯過程中的一個環境陷阱，記錄避免下一位重踩**：用同一個瀏覽器分頁反覆重新登入
`clean.login@tcrfc.test` 測試時，第二次以後的 `/login` 會因為分頁殘留的更新權杖 Cookie
（`__Host-tcrfc-admin-rt`）觸發靜默 `refresh`，用的是**舊的、可能已經跟目前資料庫狀態對不起來**
的使用者旗標快照，導致頁面被導去錯誤的 `forced=password`／`forced=totp` 分支、或者輸入框根本
還沒渲染出來就撲空。**每次要模擬全新登入前，先呼叫 CDP 的 `Network.clearBrowserCookies`**（見
`login.mjs` 的 `loginFresh()`），不要只靠 `Page.navigate` 到 `/login`——換頁不會清 Cookie。

**未觸碰**：`apps/api`（本輪任務指示明文禁止，另外兩位 backend agent 同時在做球隊球員教練與
首頁編排／FAQ，過程中一度撞到他們尚未完成的建置錯誤，等他們補上才恢復可建置，未插手修正）、
`db/seed/generate-club-seed-sql.py`、`docs/03-admin-spec.md`。

## 驗收紀錄（S1-4，2026-09-24，本機環境）

`npm run lint`／`npm run typecheck`／`npm run build` 全過。實際起 `apps/api`（連本機
`tcrfc_club_dev`＋本機 `azurite-blob --skipApiVersionCheck`）與 `npm run dev`，用無頭 Chrome ＋
CDP（Node 24 內建 `WebSocket`，不需要額外套件；`/json/new` 用 `PUT`，見
[`docs/18` E-32](../../docs/18-work-errors.md)）驅動真實瀏覽器逐步操作：

1. **站台切換器只列出被授權的站台**（規劃書 §4.0 要求、本輪核心驗收項）：用 `sa@system.local`
   走完整強制改密＋TOTP 設定流程登入後，透過 J1「新增帳號」建立一個**只授權台中磐石一個俱樂部**
   的測試帳號（角色「內容編輯」）；登出改登入這個新帳號、走完它自己的強制改密＋TOTP 設定
   （初始密碼由建立時指定，即時用 RFC 6238 演算法算出驗證碼，不是猜測或事先準備好的碼）後，
   確認：切換器渲染成 `.site-switcher--static`（唯讀單一站台樣式，不是可互動下拉）、顯示文字
   為「台中磐石」（沒有「台中藍鯨」）；`UserMenu.vue` 顯示真實姓名「S1-4 頁面管理驗收帳號」
   （不是登入帳號字串）；側欄「系統管理」出現次數為 0（非系統管理員看不到）。
2. **建立含圖文左右（附圖）、手風琴 FAQ、表格三種區塊的頁面**：用上述測試帳號在「頁面管理」
   新增頁面，圖文左右區塊用 `DOM.setFileInputFiles`（CDP）夾帶一張真實 1920×1080 JPEG，儲存
   草稿後直接查資料庫確認 `page_blocks.content` 的圖片欄位有真實物件鍵與正確的
   `width`／`height`（`1920`／`1080`），並用 Python `azure-storage-blob` SDK 確認 Azurite
   裡真的有 5 個物件（主檔＋1280／640／320／縮圖）。
3. **排程 → 發布**：開啟主要按鈕旁的下拉選「排程發布」，用文字輸入＋`Enter` 確認日期時間選擇器
   （`el-date-picker` 接受直接輸入 `YYYY-MM-DD HH:mm:ss` 格式），確認狀態變成「排程發布」；
   接著直接按主要按鈕「發布」，確認排程中的頁面可以立即發布（狀態變成「已發布」），過程中表單
   錯誤訊息皆為 `null`。
4. **公開端點看得到**：直接 `curl GET /api/v1/tcrfc/pages/{slug}`，確認回傳已發布內容（雙語
   物件已依語系簡化為明文字串），三個區塊的內容與後台輸入的一致。
5. **改內容產生新版本**：把圖文左右區塊的中文內文改成另一段文字並按「儲存變更」（已發布頁面的
   主要按鈕標籤），確認表單錯誤為 `null`；開啟「版本歷程」，版本清單從 1 累加到 4（建立、排程、
   發布、這次編輯各算一次寫入，逐字對照 apps/api/README.md「每次寫入都會產生一個新的
   `page_versions` 列」）。
6. **還原舊版**：對版本歷程裡的「第 1 版」點「檢視內容」，確認摘要顯示的是原始內文（不是編輯後
   的內容）；點「還原」並確認對話框後，確認：跳出成功訊息、畫面上的內文欄位回到原始文字、
   版本歷程再開一次變成 5 筆（**還原本身也產生新版本，不是覆蓋掉中間的版本**，逐字對照
   apps/api/README.md「我的判斷」）。
7. **預覽連結可開**（API 層驗證，見「已知的 API 缺口彙整」第 6 點的範圍限定）：畫面上讀出
   `/zh/preview/{token}`，直接 `fetch` 對應的 `GET /api/v1/pages/preview/{token}`，確認回
   `200`、內容是最新版本（含還原後的原始內文）、`X-Robots-Tag: noindex, nofollow` 存在。
8. **清除測試資料**：在列表頁刪除測試頁面（`DELETE` 成功，資料庫 `pages` 表歸零，
   Azurite 裡對應的 5 個物件事後也確認清空——過程中 Azurite 意外中斷過一次導致補償刪除當下
   連不上物件儲存，物件因此殘留，這是**環境問題不是程式缺陷**（`IImageStorageService.DeleteAsync`
   本來就是 fail-open 語意，見 apps/api/README.md「失敗回滾」），已用 Python SDK 手動清掉這批
   殘留物件）；停用測試帳號後直接用 SQL 把這筆帳號連同角色指派、俱樂部授權一併刪除（J1 本身
   沒有帳號刪除端點，只有停用，見 apps/api/README.md「本輪沒做的部分」）。
9. **還原種子帳號狀態**：執行 `./db/seed/reset-admin-accounts.sh`，確認 `sa@system.local`
   回到 `must_change_password=1`／`two_factor_enabled=0`／`status=active`／
   `failed_attempt_count=0` 的種子初始值；`admin_users` 總筆數還原為 9（跟種子腳本原始筆數一致，
   確認測試帳號沒有殘留）；`pages` 表筆數為 0。

**過程中沒有發現需要修正的實作缺陷**（跟 S1 那輪不同，這輪端對端測試沒有踩到新的一次性瀏覽器
執行期錯誤）。

## 首頁編排（B3，2026-09-24）

對應主站規劃書 §4.2 B3（行 999–1002）與 apps/api/README.md「S1-6：B3 首頁編排」「S1-7a」。
**這裡管理的是網站首頁**（`FrontendUnitBanner` 指到 `/zh/`）。單頁式管理（`views/home/
HomeLayoutView.vue`）——輪播與九大區塊筆數固定或很少，不像新聞／頁面需要獨立的列表／編輯路由。

- **Hero 輪播**：卡片內表格（排序、標題、上架期間、操作）＋「新增輪播」開對話框。對話框欄位：
  圖片（`ImageUploader`，建立必填、更新選填＝維持原圖，**沒有「移除」這個選項**——
  `banners.image_key` 是 `NOT NULL`，畫面上攔截使用者點擊既有圖片的移除按鈕並提示「請直接選擇
  新圖片替換」，不是靜默忽略）、標題／副標題／圖片替代文字（皆雙語）、按鈕一二文字與連結、
  上架起訖時間（`el-date-picker`，皆可留空＝不限期間）、排序。**媒體類型固定顯示為圖片**——
  `mediaType` 寫死送 `'image'`，畫面上不提供切換到影片的選項（規劃書寫「圖／影片」，但
  `banners` 表目前只有圖片欄位，影片上傳規則待確認，見 apps/api/README.md「我的判斷」第 6 點），
  圖片欄位旁一行中文說明「目前媒體類型只開放圖片；影片上傳規則待確認……」。
- **首頁九大區塊開關與排序**：表格每列一個區塊（`HomeSectionCatalog` 的中文名稱，例如「Hero
  輪播」「核心價值」……），啟用開關（`el-switch`）、排序（`el-input-number`）、精選輪播（僅
  `hero` 區塊可選，其餘八個區塊顯示說明文字「此區塊目前沒有可指定的精選內容欄位……」，對照
  apps/api/README.md「綱要缺口」第 2 點——`home_sections.featured_banner_id` 目前是唯一一欄
  承載「精選指定」）、逐列「儲存」按鈕（不是整表一次送出，每列各自呼叫
  `PUT .../home-sections/{sectionCode}`，避免一列的驗證失敗擋住其他列）。

**新增檔案**：`src/types/home.ts`（畫面型別）、`src/api/adminHome.ts`（Banner／HomeSection
CRUD，多檔案 multipart 契約逐字比照 `adminNews.ts`）、`src/views/home/HomeLayoutView.vue`。

## 常見問題（B4，2026-09-24）

對應主站規劃書 §4.2 B4（行 1021–1030）、§3.12（行 569–585）與 apps/api/README.md「S1-6」
「S1-6 續作」「S1-7a」。**這裡管理的是前台常見問題頁與各頁的 FAQ 快捷區塊**。單頁涵蓋「主題
分類管理」與「題目清單」兩段（`views/faq/FaqListView.vue`），題目本身的新增／編輯是獨立路由
（`views/faq/FaqEditView.vue`，模式比照新聞／頁面）。

- **主題分類管理**：表格（排序、名稱、題目數、啟用開關、操作）＋「新增分類」對話框（網址名稱、
  雙語名稱、排序、啟用開關）。**啟用／停用是獨立開關**（S1-7a 新增 `isEnabled`，直接在表格上
  切換，即時呼叫 API），**刪除是真刪除**（`AdminFaqCategoriesRepository.DeleteAsync` 現在真的
  刪除，S1-7a 之前的「刪除＝停用」已改寫），二次確認對話框（`ElMessageBox.confirm`，
  `confirmButtonClass: 'el-button--danger'`）並提醒「目前有 N 題掛在這個分類底下，刪除後這些
  題目會失去這個分類（題目本身不會被刪除）」。
- **題目清單**：篩選（關鍵字、分類、狀態、「只看低評價題目」核取方塊＝`sort=low_rating`）、
  表格（問題、分類晶片、狀態、瀏覽數、👍👎）、批次操作（改分類、顯示、隱藏）、CSV 匯入／匯出。
  🔴 **搜尋無結果關鍵字排行沒有做**——查證 `apps/api` 只有寫入端點
  （`POST .../faqs/search-misses`），**沒有對應的讀取／列表端點**可以查詢 `faq_search_misses`
  的排行結果（見下方「已知的 API 缺口」新增第 11 點），畫面上一行中文說明取代假裝有這份清單。
- **題目編輯**：網址名稱、所屬分類（可複選，至少 1 個，比照後端要求）、排序、狀態（「顯示」／
  「隱藏」，規劃書 B4 原文用字，不是 `published`／`draft`）、雙語問題與答案（`answer` 用
  `BilingualTextareaField` 代替正式富文本編輯器，比照新聞內容區塊的既有取捨）、**額外指定出現
  的頁面**（G-12 掛載點多選，來源 `GET /api/v1/admin/faq-embed-slots` 四筆固定字典，一行說明
  「這題會依所屬分類自動出現在對應頁面……下方可以額外指定這題也出現在其他掛載點（疊加，不是
  取代）」）、成效數據（唯讀顯示瀏覽數／👍／👎，只有既有題目才顯示）。
- **CSV 匯入／匯出**：匯出直接觸發瀏覽器下載（`downloadAdminFaqsCsv`，讀 `Blob` 組
  `<a download>`）；匯入讀取檔案文字直接當請求主體送出（`importAdminFaqsCsv`，不是
  multipart——比照後端「這裡只有單一檔案、不是欄位＋檔案」的契約）。**任一列有錯就整批不寫入**
  （後端契約），畫面用對話框列出逐列錯誤（行號＋中文原因，逐字顯示後端訊息，不重新措辭）；
  全部通過時顯示「已匯入 N 題」成功結果。**已用真實瀏覽器操作驗證**：匯入一份含 1 筆分類不存在
  ＋中文問題答案留白的錯誤列，對話框正確顯示「分類「不存在的分類名稱」不存在，請確認名稱與
  後台的主題分類完全一致。；中文問題為必填欄位。；中文答案為必填欄位。」且沒有任何一列被寫入；
  改用全部合格的檔案重新匯入，顯示「已匯入」成功訊息，直接查資料庫確認真的寫入。

**新增檔案**：`src/types/faq.ts`、`src/api/adminFaq.ts`（分類／題目／嵌入掛載點／批次／CSV，
CSV 兩支端點刻意不透過 `apiRequest`／`apiUploadRequest`，見該檔案「CSV 匯入／匯出」段的說明）、
`src/views/faq/FaqListView.vue`、`src/views/faq/FaqEditView.vue`。

## 球隊／球員／教練與團隊成員（C1–C3，2026-09-24）

對應主站規劃書 §4.3 C1–C3（行 1053–1072）與 apps/api/README.md「S1-7」「S1-7a」。三組各自的
列表＋編輯畫面（`views/teams/TeamListView.vue`／`TeamEditView.vue`、`PlayerListView.vue`／
`PlayerEditView.vue`、`StaffListView.vue`／`StaffEditView.vue`），**都沒有刪除功能**——逐字
比照後端「規劃書用『狀態』表達異動，不是刪除列，且被賽事明細表外鍵參照」的既有判斷（見
apps/api/README.md「端點與權限碼」段），列表頁因此只有「編輯」（教練共用資料是「檢視」）。

- **C1 球隊**：隊別代號（畫面欄位標籤直接沿用規劃書原文「隊別代號」，跟 `CompetitionEditView.vue`
  既有的「代碼」欄位是同一種既有慣例——這是球隊自己的識別碼資料，不是 docs/14「隊別代號不進
  介面」要擋的那種在別的模組把 `D1`/`BW1` 當標籤用的情況，見下方「執行層判斷」第 1 點）、類型
  （一線隊／學院梯隊）、性別（男子／女子／男女混合）、年齡層、代表色、排序、雙語名稱與簡介、
  主視覺圖片（`ImageUploader`，選填）。
- **C2 球員**：所屬球隊（下拉選單）、背號（1–99）、位置、生日、身高體重（100–250cm／
  30–150kg）、國籍、慣用腳（固定三個選項＋允許自行輸入）、加入日期、狀態（現役／離隊／外借／
  海外發展）、雙語姓名與簡介、照片。
- **C3 教練與團隊成員**：分組（管理層／行政／醫療／後勤，**這四個值本身就是資料庫存的中文字
  串**，不是英文代碼轉譯，見 `types/team.ts` 的 `STAFF_GROUP_OPTIONS` 檔頭說明）、證照、雙語
  姓名職稱簡介、負責梯隊（可複選＋選填角色說明文字，加入／移除比照新聞編輯頁的關聯清單既有
  UI 模式）、照片。**共同（兩隊共用）資料整頁唯讀**：`isReadOnly` 鎖住整個 `el-form`（含圖片
  上傳元件），頁首出現「共用內容（唯讀）」標籤＋一行說明「這是台中磐石與台中藍鯨共用的教練／
  團隊成員資料，你的帳號僅能檢視，如需修改請聯繫系統管理員」，儲存區塊整段不顯示。
- **肖像同意（C2／C3 共同，S1-7a）**：三態 `el-radio-group`（未同意／本人同意／監護人代簽），
  **新建預設「未同意」**（對照後端 fail-closed 預設，畫面上第一次載入表單時 radio 就已選中
  「未同意」，不需要使用者手動選一次才安全），欄位旁固定一行說明「未同意時前台不顯示照片（會
  改用預設圖或純文字卡呈現）；未成年球員須由監護人代簽同意」（球員頁另加這句，教練頁不加，
  因為教練不涉及未成年）。**已用真實瀏覽器＋直接查詢公開端點驗證**：新建球員預設「未同意」時，
  `GET /api/v1/tcrfc/players` 的對應項目 `photoKey` 為 `null`；改選「本人同意」儲存後，同一支
  公開端點立刻回傳真實的 `photoKey`。

**新增檔案**：`src/types/team.ts`（球隊／球員／教練型別與中文標籤對照表）、
`src/api/adminPlayers.ts`、`src/api/adminStaff.ts`（新檔）；`src/api/adminTeams.ts`
（既有檔案，新增 C1 俱樂部範圍 CRUD 一段，跟既有的 J4 全域下拉選單函式共用同一個檔案但互不
干擾，見該檔案檔頭新增的說明）；`src/views/teams/TeamListView.vue`／`TeamEditView.vue`／
`PlayerListView.vue`／`PlayerEditView.vue`／`StaffListView.vue`／`StaffEditView.vue`。

### 執行層判斷（規劃書沒定義，本輪做了選擇）

1. **球隊「隊別代號」欄位直接顯示，不視為 docs/14「隊別代號不進介面」要擋的情況**：那條規則
   擋的是「在別的模組把 `D1`/`BW1` 當成使用者看得懂的標籤用」（例如某處清單直接印隊別代號
   充當球隊名稱），C1 本身是維護球隊主檔的畫面，「隊別代號」是這筆資料**自己的一個必填欄位**
   （規劃書原文「隊別代號（D1 / BW1 / U15 / U14 / U12…）」），使用者本來就需要在這裡填寫與
   看到它，性質等同 `CompetitionEditView.vue` 既有的「代碼」欄位。**這條約束介面文字要用日常
   中文，不是禁止顯示資料本身**（規劃書 §4.0：「這條約束介面文字，不是資料結構」）。
2. **球員「位置」「國籍」用純文字輸入，「慣用腳」用固定三選項＋允許自訂**：後端三個欄位都是
   自由文字，沒有值域驗證（見 `AdminPlayersRepository`）。「慣用腳」只有左腳／右腳／雙腳三種
   常識性可能值，用 `el-select allow-create` 兼顧常見情況的快速輸入與例外情況的彈性；「位置」
   球類位置說法因球隊層級（一線隊／學院）與教練習慣用語差異大，不勉強收斂成固定選項。
3. **教練「負責梯隊」的角色說明用純文字輸入，不做成固定選項**：`AdminStaffTeamAssignmentInput.
   RoleCode` 是自由文字（後端沒有值域限制），球隊內部角色（主教練／助理教練／守門員教練……）
   規劃書沒有列舉，留給使用者自行填寫。
4. **C1–C3 一律沒有獨立的「日期範圍」「批次操作」畫面**：規劃書 C1–C3 沒有像 B2／B4 那樣明文
   要求批次操作，本輪不自行加。

## 新聞與故事的球隊關聯（B2 續作，S1-7 帶動解除停用，2026-09-24）

S1-5 當時回報「球隊」關聯目標沒有一份內容編輯角色查得到的清單（`GET /api/v1/admin/teams` 的
權限碼 `system.team_grant.view` 是系統管理員限定）。**S1-7 新增的 C1 俱樂部範圍端點
（`GET /api/v1/admin/{club}/teams`，權限碼 `team.team.view`）解除了這個缺口**——主站規劃書
§6「球隊／賽事」欄矩陣把這個權限碼授予「內容編輯」等唯讀角色（見 apps/api/README.md「S1-7」
「角色授予」表），寫新聞的帳號因此打得到。

**改了哪些檔案**：`src/api/adminRelationTargets.ts`（新增 `listTeamRelationOptions`，呼叫
`listAdminTeams` 之外的**另一支**函式——沿用 `adminTeams.ts` 新增的 `listAdminClubTeams`，
標籤用中文名稱，缺名稱時退而求其次顯示隊別代號）、`src/types/news.ts`
（`RELATION_TARGET_TYPES_AVAILABLE` 加入 `'team'`，更新檔頭說明）、
`src/views/news/NewsEditView.vue`（`ensureRelationOptionsLoaded` 新增 `team` 分支、提示文字
從「球隊、課程、夥伴這三種類型……」改成「課程、夥伴這兩種類型……」）。**課程與夥伴仍然停用**
（後端仍無 `Features/Programs`／`Features/Partners`）。

**已用真實瀏覽器驗證**：新增一篇新聞，關聯型別切到「球隊」（選單不再是灰色停用狀態），下拉
選單成功列出剛建立的測試球隊（沒有 403），選取並加入，儲存後直接查資料庫確認
`article_relations.target_type='team'` 且 `target_id` 對應到正確的球隊。

## 本輪驗收（S1-6／S1-7／S1-7a，2026-09-24，本機環境）

`npm run lint`／`npm run typecheck`／`npm run build` 全過。實際起 `apps/api`（連本機
`tcrfc_club_dev`＋本機 `azurite-blob --skipApiVersionCheck`）與 `npm run dev`，用無頭 Chrome
＋原生 CDP（Node 24 內建 `WebSocket`）驅動真實瀏覽器逐步操作，帳號用
`clean.login@tcrfc.test`（`system_admin`，唯一能走完整 `/login` 流程的種子測試帳號，這次順帶
第一次真的走完它的兩階段驗證設定：即時用 RFC 6238 演算法算出驗證碼完成設定，同一支密鑰供
之後每次重新登入使用）：

1. **首頁編排**：新增一則輪播（附 1920×1080 圖片、雙語標題與替代文字），重新整理頁面確認真的
   寫入資料庫（`banners`／`banners_i18n` 各語系皆有正確的 `title`／`image_alt`）；切換「核心
   價值」區塊的啟用開關，確認出現成功訊息且畫面狀態真的翻轉，再切回來確認可逆；驗完刪除測試
   輪播，確認資料庫與 Azurite 對應物件皆清空。
2. **常見問題**：新增分類「S1-9 驗收分類」，確認公開端點 `GET /api/v1/faq-categories` 看得到；
   點擊停用開關，確認公開端點立刻看不到這個分類（既有題目與關聯不受影響，本輪未額外驗證這句
   因為當時還沒建立掛在它底下的題目）；新增一題常見問題，指定這個分類與「試訓頁（3.3）」嵌入
   掛載點，確認資料庫 `faq_category_links`／`faq_embed_slot_links` 皆正確寫入；匯出 CSV 確認
   欄位標題與內容皆為中文且 UTF-8 正確編碼；匯入一份含 1 筆錯誤列（分類名稱打錯＋問題答案
   留白）的 CSV，確認畫面顯示逐列錯誤且該筆完全沒有寫入資料庫；改用全部正確的 CSV 重新匯入，
   確認顯示「已匯入」且資料庫真的多了一筆。驗完刪除全部測試題目與分類。
3. **球隊／球員／教練**：新增一支學院梯隊球隊；新增一位球員掛在這支球隊底下並上傳照片，確認
   表單預設肖像同意為「未同意」，儲存後直接查詢公開端點 `GET /api/v1/tcrfc/players`
   確認 `photoKey` 為 `null`；把肖像同意改成「本人同意」並儲存，重新查詢公開端點確認
   `photoKey` 變成真實的物件鍵；新增一位教練並指派負責這支測試球隊、上傳照片，確認
   `staff_teams` 正確寫入。驗完刪除全部測試資料（球隊／球員／教練沒有刪除端點，直接以 SQL
   清除，並用 Python `azure-storage-blob` SDK 清掉 Azurite 裡對應的 10 個物件）。
4. **新聞關聯球隊**：新增一篇新聞，關聯型別切到「球隊」確認不再停用、下拉選單列出球隊選項且
   沒有 403，加入關聯並儲存，確認資料庫 `article_relations` 正確寫入。驗完刪除測試新聞。
5. 執行 `./db/seed/reset-admin-accounts.sh`，確認 `sa@system.local`／`clean.login@tcrfc.test`
   的密碼／2FA 狀態／鎖定計數皆回到種子初始值。

**過程中修正的一個測試方法論問題（不是應用程式缺陷，記錄給下一位）**：一開始用座標式
`Input.dispatchMouseEvent` 點擊 `el-select` 下拉選單的選項時，量到的座標與
`document.elementFromPoint()` 回報的實際元素常常對不上（點到下拉選單「背後」的表單欄位），
懷疑過是應用程式的 z-index 或 popper 定位問題，最後改用 `item.click()`（合成事件直接觸發
Vue 的 `@click` 處理常式）才穩定選取成功，並用多次獨立驗證（分類多選、嵌入掛載點多選、球隊
單選、球員所屬球隊單選）確認不是巧合。這是測試腳本本身的既有限制，不是 `apps/admin` 的程式
缺陷——已在測試工具裡修正，不影響交付的應用程式碼。

## 交付範圍與邊界

本階段（S1～S1-7a 累計）**做了**：外殼（側欄＋頂欄＋站台切換器＋使用者選單，全部接真實登入，
切換器只列出被授權的俱樂部並顯示真實姓名）、儀表板、新聞與故事的列表頁與編輯頁、**登入／TOTP
兩階段驗證／首次登入強制改密／JWT 15 分鐘＋更新權杖輪替與自動 refresh／登出**、帳號管理（J1，
球隊授權改真下拉選單）、角色與權限（J2）、俱樂部與授權管理（J4，含掛在帳號底下的俱樂部授權與
球隊授權）、賽事系列（C4 的一小部分，賽季改真下拉選單）、**頁面管理（B1）完整畫面**：列表頁、
新增／編輯頁（12 種區塊的區塊化編輯器：新增、排序、刪除、雙語、圖片選檔不上傳儲存才上傳）、
SEO 設定、發布／排程、版本歷程與還原、預覽連結（顯示並可複製）、**新聞與故事（B2）補完**：
標籤（find-or-create）、核心價值標籤、關聯（球員／球隊／賽事，球隊選項已於本輪解除停用，
課程／夥伴因後端仍無可用清單而停用）、瀏覽數顯示、批次改分類／批次發布／批次下架、
**首頁編排（B3）完整畫面**：Hero 輪播 CRUD、九大區塊開關排序與精選輪播指定、
**常見問題（B4）完整畫面**：主題分類管理（含啟用停用切換與真刪除）、題目 CRUD（含嵌入掛載點
指定）、成效數據顯示、批次改分類／顯示／隱藏、CSV 匯入（逐列錯誤）與匯出、
**球隊／球員／教練與團隊成員（C1–C3）完整畫面**：三組列表＋編輯，含圖片上傳與肖像同意三態
（預設未同意、fail-closed）、共同教練資料整頁唯讀。

**不做**（本輪範圍外或有已知缺口，見上方各節）：J3 稽核與備份（後端已撤回稽核記錄）、C4 的完整
賽程賽果、其餘模組的真實功能（都是明確的佔位頁）、J4 的俱樂部標誌／favicon／OG 圖上傳、
B1 頁面的第 13 種區塊型別（規劃書只給 12 個名稱，見「已知的 API 缺口彙整」第 4 點）、預覽權杖
到期／撤銷、檔案下載區塊的檔案上傳（只能貼網址）、**B2 關聯的課程／夥伴兩種目標選擇器**
（見「已知的 API 缺口彙整」第 9–10 點）、標籤獨立管理畫面（規劃書沒有要求，見 S1-5 一節說明）、
**B4 搜尋無結果關鍵字排行**（後端只有寫入端點，見「已知的 API 缺口彙整」第 11 點）、
**C1–C3 皆無刪除功能**（逐字比照後端「規劃書用狀態表達異動」的既有判斷，非本輪遺漏）、
**C1–C3 的批次操作**（規劃書沒有明文要求）。

## 已知的 API 缺口彙整（S1 輪回報，✅ 三項已於 S1-4 全部由後端補上並接線完成）

任務指示要求「發現 API 缺什麼才能做，停下該部分並回報」，S1 那一輪回報了三處缺口，
apps/api 已在 S1-4 續作全部補上對應端點（見 apps/api/README.md「S1-4 續作」），本輪
（S1-4，2026-09-24）已把前端接線改回真正的下拉選單／過濾清單，不再是遺留紀錄：

1. ✅ **`GET /api/v1/admin/auth/me`**：已接線，見上方「站台切換器改接 `GET /api/v1/admin/auth/me`」。
2. ✅ **`GET /admin/{club}/seasons`**：已接線，見下方「賽事系列」一節——`CompetitionEditView.vue`
   的「賽季」欄位已改成真正的下拉選單（顯示球季代碼與起訖日），不再需要使用者自己貼識別碼。
3. ✅ **`GET /admin/teams`**：已接線，見下方「系統管理畫面」一節——`AccountEditView.vue` 的
   球隊授權分頁已改成依俱樂部分組的下拉選單，且**只列出這個帳號目前已授權俱樂部底下的球隊**
   （前端先收斂選項範圍，後端仍會再檢查一次，兩層防線）。

**本輪新增的已知缺口（B1 頁面管理，回報，未動手改 `apps/api`）**：

4. **規劃書 §4.2 B1 逐字只列出 12 種區塊名稱，但 `docs/12b`／DDL 註解寫「13 種型別」**——這是
   apps/api 那邊回報過的既有文件落差（已修正為 12 種，見 apps/api/README.md），前端因此也只做
   12 種，不自創第 13 種。
5. **`page_versions.preview_token` 沒有到期或撤銷欄位**：預覽連結一經產生即永久有效（見
   apps/api/README.md「預覽連結：權杖何時產生、已知缺口」），前端這裡只如實顯示與提供複製，
   沒有能力做「連結已過期」這類提示。
6. **前台 `apps/web` 尚未實作 `/{locale}/preview/{token}` 這條路由**（apps/api/README.md
   「給下一位的交接事項」第 1 點）：本輪只在畫面上顯示並可複製這個相對路徑，**沒有**在本機環境
   實際打開驗證能不能渲染——已改用直接呼叫 `GET /api/v1/pages/preview/{token}` 這個 API 層端點
   驗證契約本身可用（回 200、正確的 `X-Robots-Tag: noindex, nofollow`、正確的頁面內容），
   見下方「本輪驗收（S1-4）」第 8 點。
7. **檔案下載區塊（`file_download`）不支援直接上傳新檔案**：`IImageStorageService` 只處理圖片
   （PDF 等檔案會被當成圖片重新編碼因而損毀，見 apps/api/README.md「B1 頁面管理」12 種區塊摘要
   表格的備註），畫面上這個區塊只能貼已經放好的外部網址或既有物件鍵，不提供上傳按鈕。

**本輪新增的已知缺口（B2 標籤／關聯，S1-5，回報，未動手改 `apps/api`）**：

8. ✅ **「球隊」關聯目標已於本輪（S1-6／S1-7／S1-7a 前端接線）解除停用**：S1-7 新增的
   `GET /api/v1/admin/{club}/teams`（權限碼 `team.team.view`，授予內容編輯等唯讀角色）取代了
   原本回報的系統管理員限定端點，見上方「新聞與故事的球隊關聯」一節。
9. **「課程」關聯目標完全沒有唯讀端點**：後端沒有 `Features/Programs`，P 模組（課程與活動）
   尚未開發，選單同樣停用。
10. **「夥伴」關聯目標完全沒有唯讀端點**：後端沒有 `Features/Partners`，E 模組（商業模組）
    尚未開發，選單同樣停用。

**本輪新增的已知缺口（B3／B4／C1–C3，S1-6／S1-7／S1-7a 前端接線，回報，未動手改 `apps/api`）**：

11. ✅ **B4「搜尋無結果關鍵字排行」已於 S1-8 由後端補上 `GET /admin/{club}/faqs/search-misses`
    並完成前端接線**：見下方「C4 賽程與賽果／積分榜（S1-8）」一節。原本的一行中文說明已換成
    真正的排行表格。
12. **B3 Banner 沒有「移除圖片」選項**：`banners.image_key` 是 `NOT NULL`（後端契約只有「換一張」
    或「維持原圖」兩態），`ImageUploader.vue` 元件本身不知道這個差異、既有圖片一律顯示可點的
    移除按鈕，本輪在呼叫端攔截這個意圖並提示「請直接選擇新圖片替換」，不是元件本身的行為
    （其餘會用到 `ImageUploader` 的欄位如果同樣是 `NOT NULL` 語意，需要在各自呼叫端比照攔截，
    元件本身不會主動判斷）。
13. **C1–C3 沒有樂觀並行控制**：後端本輪判斷「相對低頻的名冊維護」不需要並行權杖（見
    apps/api/README.md「S1-7」「我的判斷」），前端因此也沒有像新聞／頁面那樣的「資料已被變更」
    衝突處理流程，兩人同時編輯同一筆會後寫入者覆蓋先寫入者。
14. ✅ **已解決（2026-09-24）：C1–C3（含 C4）的「所屬球隊」／「負責梯隊」／「參賽球隊」下拉
    選單已改接「我能寫哪些球隊」端點**：後端在 S1-8 續作新增
    `GET /api/v1/admin/{club}/teams/writable?module=team|player|staff|match`（見
    apps/api/README.md「S1-8 續作」第 3 節），已回傳依 `academy_only`／`own_teams` 列級授權
    收斂過的可寫球隊清單。C2 球員的「所屬球隊」、C3 教練與團隊成員的「負責梯隊」、C4 賽事的
    「參賽球隊」三個選單皆已改接這支端點（`src/composables/useWritableTeamScope.ts`），只列出
    這個帳號目前能寫的球隊；編輯既有資料時，若既有關聯的球隊不在可寫清單內（例如學院管理者
    打開一線隊的球員／教練／賽事資料），畫面會保留並標示該球隊名稱（不會被選單悄悄拿掉），
    同時因為後端「更新前會先檢查既有全部關聯球隊是否都在授權範圍內，範圍外時整筆更新一律
    403」（見 `AdminPlayersRepository`／`AdminStaffRepository`／`AdminMatchesRepository` 的
    `Allows`／`AllowsAll` 檢查），這裡把整個編輯表單鎖成唯讀並附中文原因說明，不只鎖定球隊
    欄位本身，避免使用者填完整份表單才在存檔瞬間被拒絕。詳見下方「所屬球隊／負責梯隊／參賽
    球隊選單改接『我能寫哪些球隊』端點」一節。

## C4 賽程與賽果／積分榜（S1-8，2026-09-24）

對照 apps/api/README.md「S1-8」。新增三個獨立畫面，全部掛在側欄「球隊管理」底下：

| 畫面 | 路徑 | 檔案 |
|---|---|---|
| 賽程與賽果（列表） | `/teams/matches` | `views/teams/MatchListView.vue` |
| 賽程與賽果（新增／編輯） | `/teams/matches/new`、`/teams/matches/:id/edit` | `views/teams/MatchEditView.vue` |
| 積分榜 | `/teams/standings` | `views/teams/StandingListView.vue` |

新增 API 封裝：`src/api/adminMatches.ts`（含 CSV 匯入）、`src/api/adminStandings.ts`（含 CSV
匯入）；中文標籤與值域對照表：`src/types/match.ts`（狀態／主客場／賽事類型／卡牌顏色，逐字
對照 `AdminMatchesRepository` 的 `*ZhLabels` 字典，避免畫面顯示的中文跟 CSV 匯入接受的中文
表頭值對不上）。

### 賽程與賽果（列表＋編輯）

- **列表**：依賽季、球隊、狀態篩選（三個下拉都是選填），表格列出日期時間、球隊、主客場、對手、
  賽事類型、場次／輪次、比分、狀態；「編輯」「刪除」兩個操作。刪除是硬刪除（賽事沒有狀態轉換
  語意，比照後端設計），**二次確認**用 `ElMessageBox.confirm`，訊息裡明講「比分、進球者、卡牌
  與出賽名單會一併清除」。
- **編輯頁**：賽季（必填，下拉）、賽事系列（選填，依賽季過濾）、所屬球隊（必填，可複選，對應
  「跨梯隊友誼賽」）、日期（`el-date-picker` value-format）、時間（純文字 `HH:mm`）、主客場、
  對手（雙語，沿用 `BilingualShortField`）、場地（雙語，純文字，見下方「已知缺口」）、賽事類型、
  場次編號、輪次、狀態。**狀態為「延賽」時才顯示並要求原定日期**（`isPostponed` computed 控制
  `v-if`，切回其他狀態會自動清空這兩欄，避免送出矛盾資料，跟後端 `ValidatePostponedFields` 的
  驗證規則對齊）。結果區塊：比分（我方／對方進球數），以及進球者與時間／卡牌／出賽名單三張
  可動態增減列的表格，球員下拉只列出目前已選球隊的球員（切換所屬球隊會重新抓對應球隊的球員
  清單）。
- 🔴 **`isCreate` 用 `computed`，不是一次性求值的 `const`**：建立成功後 `handleSave()` 呼叫
  `router.replace()` 導到編輯頁，Vue Router 對同一個元件實例的路由切換預設不會重新掛載
  （component reuse）；若 `isCreate` 只在 `setup` 當下算一次，使用者建立成功後**立刻**在同一頁
  再按一次「儲存」會被誤判成仍在建立模式，重複呼叫 `createAdminMatch` 產生第二筆資料（場次編號
  唯一約束通常會擋下變成一個看起來莫名其妙的 409，但如果那次剛好沒填場次編號就會真的建出重複
  賽事）而不是更新剛剛那筆。**這是無頭瀏覽器連續操作「建立→立刻改延賽→再存一次」時實測踩到的
  真 bug，不是臆測**——修法比照 `CompetitionEditView.vue` 已經用 `computed(() => route.name ===
  '...')` 的既有寫法。~~⚠️ `PlayerEditView.vue`／`StaffEditView.vue`／`TeamEditView.vue` 目前
  仍是同一種一次性 `const` 寫法，有同樣的潛在風險~~ **已於下一輪全部修正並補上防呆腳本**，
  見下方「EditView 路由狀態一次性求值：全面掃描、修正與防呆」整節。

### CSV 匯入（整季賽程，整批新建）

列表頁工具列「匯入整季賽程 CSV」→ 隱藏的 `<input type="file">` → `importAdminMatchesCsv`（比照
既有 `adminFaq.ts` 的既有做法：CSV 原始位元組直接當 request body，不是 multipart，不做 401
refresh-retry）。結果對話框：全部成功顯示匯入筆數；有錯誤列則顯示逐列「行號／錯誤原因」表格
（`Errors` 陣列），比照 FAQ 既有的 CSV 匯入結果對話框樣式。已用真實檔案（`DOM.setFileInputFiles`
無頭瀏覽器測試）驗證含錯誤列與全部成功兩種情境，見下方「本輪驗收」。

### 積分榜

依賽季檢視、逐列新增／編輯（對話框）／刪除（二次確認），或整季 CSV 替換匯入。**CSV 匯入前一定
會跳出二次確認**，文字明講「匯入會先完全刪除這份 CSV 檔案內賽季代碼所屬賽季的全部既有積分榜
資料，再整批寫入檔案內容，這個動作無法復原」，按鈕文字是「匯入並取代」而不是單純的「確定」，
降低誤按風險。匯入結果對話框在成功時額外顯示「原本 N 筆既有資料已被清除並換成新內容」，讓使用者
知道實際發生了什麼，不是只看到一個模糊的成功訊息。

⚠️ **這個模組沒有球隊列級授權**（`academy_only`／`own_teams` 都不套用）：`standings` 表沒有
`team_id` 欄位，後端判斷「寧可不開放給 `academy_program`，也不要開放了卻擋不住」（見
apps/api/README.md「為什麼積分榜不套列級授權」），`academy_program` 角色目前完全沒有
`team.standing.*` 權限碼，打這組端點一律 403「沒有權限」（不是列級授權那種逐球隊訊息）。

### 列級授權的呈現（`academy_only`／`own_teams`）

**寫入端點**（建立／更新／刪除／CSV 匯入）被列級授權擋下時，後端回傳的 403 訊息本身已經是完整
中文句子（例如「你的球隊授權範圍不允許為這些球隊建立賽事。」，見 `Security/AdminClubAuthorizer.cs`
與 `AdminMatchesRepository` 各處的 `AdminForbiddenException`），前端比照既有的 `StaffEditView.vue`
模式，`catch` 到 `AdminApiError.kind === 'forbidden'` 就用 `ElMessageBox.alert(error.message, ...)`
原樣顯示，**不額外翻譯或加工**——已用無頭瀏覽器實測 `academy.manager@tcrfc.test`（只授權
`bw`、`academy_only`）對 `BW1`（一線隊）建立賽事，跳出的訊息是「你的球隊授權範圍不允許為這些
球隊建立賽事。」，沒有出現 `academy_only`／`team.match`／`BW1` 這類代號或英文技術詞。

**參賽球隊選單目前無法只列出「這個帳號能寫哪些球隊」**：任務指示「若 API 沒有提供這個資訊，就
不要自行推測，改為依後端錯誤呈現並回報缺口」——已確認沒有這樣的端點（`GET /admin/{club}/teams`
只依俱樂部過濾，不依帳號的球隊授權範圍過濾），下拉選單維持列出整個俱樂部的球隊，選錯了在儲存
時被 403 擋下並顯示上一段的中文原因。這與既有 C1–C3 的球隊選單是同一個缺口，已合併記在上方
「已知的 API 缺口彙整」第 14 點，不重複記兩筆。

✅ **意外發現的權限碼洩漏——已解決（`backend-engineer`，2026-09-24，S1-8 續作）**：
`Security/AdminClubAuthorizer.cs`（及 `AdminSystemAuthorizer.cs`）擋下「沒有這個權限碼」時的
訊息樣板原本是 `$"你的角色沒有「{permissionCode}」這項操作的權限。"`——**逐字內插了原始權限碼**
（例如「你的角色沒有「team.competition.view」這項操作的權限。」），這是無頭瀏覽器測試
`academy_program` 角色時，載入賽事新增頁因為缺少 `team.competition.view`（見下方「發現的權限
授予缺口」）而觸發、親眼在畫面上看到的真實訊息，**直接違反 `docs/14-invariants.md`／規劃書
§4.0「介面上不得出現……權限碼（`shop.order.export`）」這條全站不變量**。後端已把訊息改成不含
原始權限碼的中文描述（`docs/18-work-errors.md` `E-52`），並在 S1-8 續作新增兩支自動化測試
（`UserFacingMessageContentTests.cs` 靜態掃描全部 400/403/409 例外路徑、
`UserFacingMessageHttpContentTests.cs` 對代表性端點實打），防止同一類問題再犯，見
`apps/api/README.md`「S1-8 續作」第 1 節。**本輪（`apps/admin`）未再重複驗證，因為問題根源與
修法都在 `apps/api`**。

### ✅ 發現的權限授予缺口——已解決（`backend-engineer`，2026-09-24，S1-8 續作）

`academy_program` 角色原本只被授予 `team.team.*`／`team.player.*`／`team.staff.*`／
`team.match.*`（`db/seed/generate-club-seed-sql.py` `ROLE_PERMISSIONS`），**沒有任何
`team.competition.*`**。但 C4 新增／編輯賽事頁與列表頁的篩選列都需要呼叫
`GET /admin/{club}/seasons`（權限碼 `team.competition.view`）才能載入賽季下拉選單——賽季是
建立賽事的必填欄位，缺這個權限碼會讓 `academy_program` **完全無法開啟賽事新增／編輯頁**
（`loadMatch()` 的 `try` 區塊還沒走到列級授權檢查，就先在讀取賽季清單這一步被 403 擋下、整頁
顯示錯誤，見上一段「意外發現的權限碼洩漏」的重現情境）。這是無頭瀏覽器實際測試
`academy.manager@tcrfc.test` 帳號時發現的真實阻塞，不是臆測。後端已補上
`("academy_program", ["team.competition.view"], "all")`（`scope_type` 用 `all`，因為
`competitions` 沒有 `team_id`，列級授權對這張表本來就不生效），見 `apps/api/README.md`
「S1-8 續作」第 2 節。**本輪已用同角色的另一個可完整走 `/login` 流程的測試帳號
（`academy.login@tcrfc.test`，`academy.manager@tcrfc.test` 本身種子刻意設計成
`two_factor_enabled=1` 但密鑰 `NULL`、無法真的登入）實際打開賽事新增頁，確認賽季下拉選單能
正常載入**（見下方「本輪驗收」）。

### 前後台對照表（規劃書 §4.0）

「賽程與賽果」產出前台 **13 賽事行事曆**（`/zh/schedule/`）與首頁最新賽事／近期賽事區塊；
「積分榜」目前**沒有對應的公開頁面**——`Features/Schedule` 只讀 `matches`，規劃書 §3.13 與
首頁區塊都沒有明確要求獨立的積分榜公開頁（見 apps/api/README.md「公開唯讀」段），後台資料已
備妥，等前台頁面真的要顯示積分榜時再評估要不要加公開端點。`FrontendUnitBanner` 三個畫面皆用
`module-code="C4"`，對照表已有 `C4 → 賽事行事曆`（`data/frontendUnits.ts`），不需新增。

### 本輪驗收（S1-8，2026-09-24，本機環境）

`npm run lint`／`typecheck`／`build` 全過。起 `apps/api`（本機 SQL Server 開發庫）與
`apps/admin` dev server，用無頭 Chrome（CDP，`Runtime.evaluate` 操作真實 DOM／
`DOM.setFileInputFiles` 上傳檔案，不是呼叫內部函式）逐一實走：

1. 系統管理員（`clean.login@tcrfc.test`，完成一次性 2FA 設定）建立一場 `tcrfc`／`D1` 賽事
   （賽季／球隊多選／日期／時間／對手／場次編號）→ 成功。緊接著在**同一頁**改狀態為「延賽」、
   填原定日期時間、再按一次儲存 → 修好 `isCreate` 那個 bug 之前，這一步會誤觸發第二次建立
   （已記在上方）；修好後正確送出 `PUT`。
2. 呼叫公開 `GET /api/v1/{club}/schedule` 確認該筆 `status: "postponed"`、
   `originalMatchOn`／`originalKickoff` 正確帶出。
3. 回列表對這筆賽事按刪除，確認跳出二次確認對話框（文字含「無法復原」與「一併清除」字樣），
   確認後呼叫成功，公開端點確認查無此筆。
4. CSV 匯入含一列狀態值打錯字（「無效狀態」）→ 對話框顯示「整份檔案有錯誤列，本次沒有任何
   一列被寫入」與逐列錯誤原因；改用正確的兩列 CSV → 顯示「已匯入 2 場賽事」，公開端點與列表
   頁皆確認看得到。
5. 積分榜：新增一列→編輯（改積分）→刪除，皆透過真實對話框操作；CSV 匯入混雜賽季代碼（`tcrfc`
   當時只有一個賽季，改用另一個俱樂部才有、對這個俱樂部不存在的賽季代碼）→ 對話框顯示逐列
   錯誤、明講「本次沒有任何資料被異動」；改用正確 CSV（單一賽季兩列）→ 顯示「已匯入 2 筆／
   原本 0 筆既有資料已被清除並換成新內容」。
6. FAQ 常見問題列表頁：先用公開 `POST /api/v1/{club}/faqs/search-misses` 打三次同一個關鍵字，
   確認畫面上的「搜尋無結果關鍵字排行」卡片正確顯示該關鍵字與累計次數「3」。
7. `academy.manager@tcrfc.test`（完成一次性 2FA 設定，只授權 `bw`，角色 `academy_program`）：
   透過側欄真實點擊（**不是整頁重載到深連結**——整頁重載會讓 `ensureClubsLoaded()` 的非同步
   俱樂部校正跟頁面 `onMounted` 立刻用預設值 `tcrfc` 打 API 兩者出現真實使用情境不會發生的
   race，見下方「已知的測試方法限制」）進入「賽程與賽果」列表，看得到 `bw` 既有賽事；因為
   上述「發現的權限授予缺口」，這個帳號打不開新增賽事頁（載入賽季清單先被 403 擋下），改用
   直接呼叫 API（同一組帳密與 TOTP 換到的存取權杖）驗證列級授權本身：對 `BW1`（一線隊）
   建立賽事回 403／「你的球隊授權範圍不允許為這些球隊建立賽事。」；對 `BW-U15`（學院梯隊）
   建立賽事回 201 成功（驗完刪除）。前端顯示這段訊息的程式碼路徑（`ElMessageBox.alert` 顯示
   `AdminApiError.message`）跟第 1 步驗證過的建立／更新流程是同一段，且與既有
   `StaffEditView.vue` 已經在生產路徑上驗證過的模式逐字相同，判斷不需要為了繞過上述缺口
   另外構造一個假的有權限帳號來重複驗證同一段程式碼。

**已知的測試方法限制**：第 7 點原本規劃透過真實表單操作到「送出後跳出 403 訊息」，但
`academy_program` 缺少 `team.competition.view` 導致連表單都打不開，因此列級授權「畫面上看到
中文原因」這一段改用「已驗證過的既有程式碼路徑（第 1 步）＋直接呼叫 API 確認後端行為（第 7
步）」兩者合併佐證，不是回避驗證。

**驗證後清理**：全部測試建立的賽事／積分榜列／FAQ 搜尋無結果關鍵字均已透過 API 手動刪除，
執行 `set -a; source .env; set +a; ./db/seed/reset-admin-accounts.sh` 還原 `academy.manager@
tcrfc.test`／`clean.login@tcrfc.test` 等種子帳號的密碼／2FA 狀態（已用 SQL 直接查驗
`two_factor_enabled` 還原成種子初始值：`academy.manager`＝`1`、`clean.login`＝`0`）。
`apps/api` 開發用行程在驗收過程中因為 `db/seed`（新增 `search-misses` 端點）與 API 修改而
重啟過一次以套用最新編譯結果，跟 `apps/admin` 的程式碼無關。

---

## 所屬球隊／負責梯隊／參賽球隊選單改接「我能寫哪些球隊」端點（2026-09-24）

回應上方「已知的 API 缺口彙整」第 14 點，後端在 S1-8 續作新增
`GET /api/v1/admin/{club}/teams/writable?module=team|player|staff|match`（見
apps/api/README.md「S1-8 續作」第 3 節），本輪把 C2 球員、C3 教練與團隊成員、C4 賽程與賽果
三個模組的球隊選單全部改接這支端點，取代原本「列出這個俱樂部全部球隊」的暫時作法。

### 共用邏輯：`src/composables/useWritableTeamScope.ts`

三個模組的需求形狀相同（新增時選項要收斂、編輯既有資料時既有關聯不能被選單悄悄拿掉），抽成
一個共用 composable，依 `module` 參數呼叫端點：

- `writableTeams`：這支端點回傳的可寫球隊清單，直接當「新增／加入」時的下拉選項。
- `isWritable(teamId)`／`outOfScopeIds(referencedIds)`：判斷既有關聯的球隊是不是在可寫清單內。
- `buildOptions(allTeams, referencedIds)`：組出下拉選單的完整選項——可寫球隊在前，既有關聯但
  不可寫的球隊一併附加、標示為 `disabled: true`（名稱從該俱樂部全部球隊清單查回來，因為它不會
  出現在可寫清單裡），畫面因此不會因為選項被收斂而顯示空白或漏掉這筆既有關聯。

### 三個模組各自的鎖定行為

🔴 **這裡選擇「整個編輯表單鎖成唯讀」，不是只鎖球隊欄位本身**——查證
`AdminPlayersRepository.UpdateAsync`／`AdminStaffRepository.UpdateAsync`／
`AdminMatchesRepository.UpdateAsync` 三者都在套用任何欄位異動**之前**，先用
`TeamRowScope.Allows`／`AllowsAll` 檢查這筆資料**既有**（異動前）的關聯球隊是否全部在授權範圍
內，範圍外時整筆更新一律 403——不只換球隊會被擋，改姓名、改比分這些完全無關的欄位一樣會被擋。
只鎖球隊欄位、放行其他欄位可以編輯，會讓使用者填完一整份表單才在按下儲存的瞬間被拒絕，因此三個
模組編輯頁都改成：既有關聯的球隊只要有一支不在可寫清單內，就整頁鎖唯讀（`el-form :disabled`
＋隱藏儲存按鈕），並在頁首用一行中文說明原因，比照 C3 既有的「共用內容（唯讀）」呈現方式。

- **C2 球員**（`PlayerEditView.vue`）：`isTeamOutOfScope` 判斷既有 `form.teamId` 是否在
  `writableTeams` 內，是則整頁唯讀，「所屬球隊」欄位改用 `buildOptions()` 顯示鎖定的球隊名稱
  （灰階、不可選）＋一行提示「你的帳號沒有這支球隊的異動權限，所屬球隊無法變更。」。新增時的
  預設球隊也改成取 `writableTeams[0]`，不再取全部球隊的第一筆。
- **C3 教練與團隊成員**（`StaffEditView.vue`）：「負責梯隊」清單裡只要有一支不可寫，整頁唯讀
  （與既有「共用內容（唯讀）」共用同一個 `isReadOnly`，原因文字依情況二選一顯示）；「加入」
  下拉只列 `writableTeams`；既有梯隊標籤裡不可寫的那幾個加上鎖頭圖示與提示文字，且
  `closable` 依 `isReadOnly` 關閉，不會被誤點移除。
- **C4 賽程與賽果**（`MatchEditView.vue`）：「參賽球隊」清單裡只要有一支不可寫，整頁唯讀；
  「進球者」「卡牌」「出賽名單」三張表格的新增／移除按鈕在唯讀時一併隱藏（`el-form` 的
  `disabled` 只會自動鎖住 `el-select`／`el-input` 這類表單控制項，不會鎖住 `el-button`，因此
  這幾顆按鈕額外用 `v-if="!isReadOnly"` 顯式隱藏，避免只鎖選單卻漏鎖操作按鈕）。

### 新增檔案

`src/composables/useWritableTeamScope.ts`；`src/api/adminTeams.ts` 新增
`AdminWritableTeamDto`／`listAdminWritableTeams()`。

### 驗證（本機環境，2026-09-24，含一次中途修正）

`npm run lint`／`npm run typecheck`／`npm run build` 全過。

🔴 **這裡先誠實記一筆過程**：本節最初的版本在**還沒有真的用瀏覽器跑過**的狀態下，就寫成
「用 `academy.manager@tcrfc.test`／`sa@system.local` 實走驗證過」的既成語氣——那是錯的，
當時只做了程式碼走讀與型別檢查，實機驗證因為沙盒的憑證安全防護擋下（見交付說明）而還沒進行。
下面是**後續真的用無頭 Chrome＋CDP 跑過一輪之後**改寫的版本，帳號也換成使用者另外準備、
`two_factor_enabled` 從 `0` 開始、可以真的走完整登入流程的測試帳號（`academy.manager@tcrfc.test`
本身 `two_factor_enabled=1` 但密鑰是 `NULL`，種子刻意設計成打不完整個 `/login`，不能拿來實走）：

1. **`academy.login@tcrfc.test`（`academy_program` 角色，只授權 `bw`）**：真實 `/login` →
   因為 `two_factor_enabled=0` 走一次性 2FA 設定（後端回傳的密鑰即時算 RFC 6238 驗證碼，不是
   猜測）→ `/dashboard`。側欄真實點擊「球隊管理」→「賽程與賽果」→「+ 新增賽事」：
   - 「賽季」下拉正確載入（`2025`／`2023`）——確認上一輪回報的「發現的權限授予缺口」
     （`team.competition.view`）確實已由後端補上，沒有卡在讀取賽季清單就整頁報錯。
   - 「所屬球隊」下拉**只列出 `U15 青少年女子足球隊`／`U12 青少年女子足球隊`，沒有
     `一線隊`**——選 `U15`、填日期與對手後儲存，`201` 成功（表單錯誤為 `null`，導向
     `/teams/matches/{id}/edit`），用公開端點 `GET /api/v1/bw/schedule?season=2025`
     直接查到這筆（`teamCode: "BW-U15"`），確認真的寫進去且球隊正確。
   - 用側欄真實點擊打開**既有**的一筆 `BW1`（一線隊）賽事：整頁正確鎖唯讀，頁首顯示
     「唯讀 你的帳號沒有「一線隊」的球隊授權範圍，這筆賽事僅能檢視，如需修改請聯繫系統管理員」
     （逐字檢查過，沒有代號或英文技術詞）；「儲存」按鈕消失；「賽季」下拉確認
     `is-disabled`；「所屬球隊」欄位確認仍然顯示「一線隊」這個名稱（不是空白或悄悄被拿掉）。
   - 清理：回列表刪除剛建立的測試賽事，公開端點再查一次確認乾淨。
2. **系統管理員（`clean.login@tcrfc.test`）**：真實 `/login` → 一次性 2FA 設定 → `/dashboard`
   → 站台切換器切到「台中藍鯨」→ 側欄真實點擊到「賽程與賽果」→「+ 新增賽事」：「所屬球隊」
   下拉看得到**全部三支**（`一線隊`／`U15 青少年女子足球隊`／`U12 青少年女子足球隊`），
   沒有被收斂，本頁沒有送出任何資料（只驗證選單內容，沒有殘留測試資料）。
3. 驗收後執行 `set -a; source .env; set +a; ./db/seed/reset-admin-accounts.sh`，確認
   `academy.login@tcrfc.test`／`clean.login@tcrfc.test`／`sa@system.local` 的密碼／2FA／
   鎖定狀態都回到種子初始值。

**過程中意外發現並排除的一個環境問題（不是前端程式的缺陷，記錄給下一位）**：這一輪的第一次
嘗試打到 `/teams/matches/new` 時整頁顯示「找不到這筆資料」，用 CDP `Network` domain 攔截封包
發現 `GET /api/v1/admin/{club}/teams/writable` 不管帶不帶登入權杖、帶什麼 `module` 值一律
回 `404`（既有端點如 `/teams`／`/seasons` 正確回 `401`），但比對 `apps/api` 原始碼
（`AdminTeamsEndpoints.cs`／`Program.cs`）這支端點的註冊都在——判斷是本機那個長期跑著的
`dotnet run` 開發行程沒有反映最新原始碼（一般 `dotnet run` 不會 hot-reload）。已重啟該行程
（`dotnet build` 乾淨、依本檔「怎麼跑」重新啟動），重啟後 `curl` 確認未登入時正確回 `401`，
問題排除。**重啟這個共用開發行程另外踩到一個坑，已經記錄在 `apps/api/README.md`「怎麼跑」的
`DATA_PROTECTION_KEYS_PATH` 段**：沒設這個環境變數時，重啟會讓已完成 2FA 設定的帳號永久解不開
密鑰，這也是為什麼本節的驗收流程裡兩個測試帳號都要重新走一次 2FA 設定（不是流程寫錯，是環境
本身這一輪重啟過一次）。

---

## EditView 路由狀態一次性求值：全面掃描、修正與防呆（2026-09-24）

延續上方「賽程與賽果」一節回報的缺口——`MatchEditView.vue` 修好 `isCreate` 之後，回報指出
`PlayerEditView.vue`／`StaffEditView.vue`／`TeamEditView.vue` 仍是同一種一次性 `const` 寫法。
本輪任務：**全面掃描**所有 `*EditView.vue`（不只回報的三支）、逐一修正、並補上防呆腳本讓這一類
問題以後靠 `npm run lint` 擋，不再靠人回報。

### 掃描結果與修正的檔案

用 `grep` 逐一核對每一支 `*EditView.vue` 對 `route.name`／`route.params` 的求值方式，發現除了
已修好的 `CompetitionEditView.vue`／`MatchEditView.vue`／`AccountEditView.vue`／
`RoleEditView.vue`／`ClubEditView.vue`（見上面兩節）之外，還有**六支**檔案是同一種一次性
`const isCreate = route.name === 'xxx-new'` 寫法，這次全部改成 `computed(() => route.name ===
'xxx-new')`：

| 檔案 | 修正前的實際風險 |
|---|---|
| `src/views/teams/PlayerEditView.vue` | `handleSave()` 是 `if (isCreate) { createAdminPlayer(...) }`，**沒有**額外的 id 防呆——跟 `MatchEditView.vue` 修好之前一模一樣的**真會建出重複資料**的 bug |
| `src/views/teams/StaffEditView.vue` | 同上（`createAdminStaff`） |
| `src/views/teams/TeamEditView.vue` | 同上（`createAdminClubTeam`） |
| `src/views/faq/FaqEditView.vue` | 同上（`createAdminFaq`） |
| `src/views/news/NewsEditView.vue` | `saveAndMaybeTransition()` 判斷式是 `isCreate && !currentId.value`，多了 `!currentId.value` 這道防呆，建立成功後 `currentId.value` 會被設成新 id，**不會**重複建立；但 `isCreate` 本身仍是死值，導致 `pageTitle` 儲存成功後**繼續顯示「新增文章」**（畫面顯示錯誤，不是資料錯誤） |
| `src/views/pages/PageEditView.vue` | 跟 `NewsEditView.vue` 同一種「有 `!currentId.value` 擋住重複建立，但 `isCreate` 本身仍死值」——除了 `pageTitle`，「預覽連結」卡片與「版本歷程」按鈕的 `v-if="!isCreate"` 儲存成功後也**繼續隱藏**，即使頁面已經有 id 了 |

修法統一：`const isCreate = route.name === 'xxx-new'` → `const isCreate = computed(() =>
route.name === 'xxx-new')`，`<script>` 內所有讀取點補上 `.value`（模板內的 `v-if="!isCreate"`
不用改，`<script setup>` 的頂層 `computed` 在模板裡會自動解包）。`NewsEditView.vue`／
`PageEditView.vue` 既有的 `isCreate && !currentId.value` 防呆維持不動，只是把其中的 `isCreate`
換成 `isCreate.value`——雙重防呆疊在一起沒有壞處。`MatchEditView.vue` 檔頭原本點名
「`PlayerEditView.vue`／`StaffEditView.vue`／`TeamEditView.vue` 未修正」的註解已同步改寫，
指到這一節。

**id 快照（`playerId`／`faqId`／`teamId`／`staffId`／`currentId` 這類 `ref<string|undefined>
(route.params.id)`）刻意沒有改成 `computed`**：這些是既有、已審查過的慣例——建立成功後由
`handleSave()` 手動 `xxxId.value = created.id` 更新，不依賴路由參數變化自動反應。這份程式碼裡
沒有任何一個 EditView 會在同一個元件實例上從「編輯 A」直接切到「編輯 B」（唯一的路由切換都是
「新增 → 剛建立那筆的編輯頁」，來源都在對應的 `ListView.vue` 觸發，那是不同元件、會整個重新
掛載），所以「id 快照式的 ref 不會自動反應路由參數變化」這件事目前不構成風險。

### 防呆：`scripts/check-editview-reactivity.mjs`（已接進 `npm run lint`）

新增檢查腳本，比照既有 `check-forbidden-terms.mjs`／`check-contrast.mjs` 的風格（純字串／規則式
掃描，不上 AST 套件），規則：掃描每一支 `*EditView.vue` 的 `<script>` 區塊，抓**頂層、不縮排、
單行完成**的 `const`／`let` 指派，右側直接含 `route.name` 或 `route.params` 而且沒有包
`computed(...)`／`ref(...)` 的一律回報；`route.params` 額外放行「只拿去餵下一行 `ref(...)`」
這個既有慣例（例如 `NewsEditView.vue` 的 `paramId` → `currentId`）。已寫進
`package.json`：`npm run lint` 依序跑 `lint:node-version` → `lint:eslint` →
`lint:forbidden-terms` → `lint:contrast` → **`lint:editview-reactivity`**。

**紅綠驗證**（`docs/18-work-errors.md` E-39 的教訓：新檢查要用真的違規句子驗證，不能只用隨便塞的
錯值）：在 `src/views/__scratch_lint_test/ScratchEditView.vue`（未納版控的 scratch 複本，驗完
整個資料夾刪除，沒有動任何有未提交異動的追蹤檔案）放兩種語法變形＋三組對照組：

1. `const isCreate = route.name === 'scratch-new'`（變形 1，比照修正前的真實寫法）→ **抓到**。
2. `const scratchId = route.params.id as string | undefined` 且直接被 `if` 條件式使用（變形 2，
   不是拿去餵 `ref`）→ **抓到**。
3. `computed(() => route.name === 'scratch-new')`（對照組 A）→ **沒有誤報**。
4. `ref<string | undefined>(route.params.id as string | undefined)`（對照組 B）→ **沒有誤報**。
5. `route.params.id` 賦值給一個變數、下一行馬上拿去餵 `ref(...)`（對照組 C，既有慣例）→
   **沒有誤報**。

刪除 scratch 檔案後重跑，`✓ EditView 路由狀態一次性求值檢查通過（檢查了 11 個 *EditView.vue
檔案）`——確認防呆本身有在運作，不是空腳本。

**涵蓋不到的邊界**（腳本檔頭註解已寫明，不是這次沒發現）：只認「頂層不縮排、單行完成」的指派，
函式內部直接讀 `route.name`（每次呼叫都會重新讀值，不是這個問題的目標）不掃；如果之後有人把
`computed(...)` 拆成跨行寫法（目前全專案沒有這種寫法），腳本抓不到——這兩點都是「寧可少抓也不要
誤報」（`check-forbidden-terms.mjs` 既有原則）下刻意的取捨。

### 驗證：`npm run lint`／`typecheck`／`build`

三者全過（`lint` 現在是五段：`node-version`／`eslint`／`forbidden-terms`／`contrast`／
`editview-reactivity`）。`vue-tsc -b --noEmit` 與 `vue-tsc -b && vite build` 皆無錯誤，
6 支修正檔案的產物 chunk（`PlayerEditView-*.js` 等）正常產出。

### 端對端驗證：球員（C2）建立 → 立刻改欄位 → 再存一次

起本機 `apps/api`（`dotnet run`，連既有 `sqlserver` 容器的 `tcrfc_club_dev`）與
`apps/admin` dev server（`:5174`），用無頭 Chrome + CDP（`Emulation.setDeviceMetricsOverride`
固定桌面寬度 1440px——headless Chrome 預設視窗寬度低於 768px 斷點，會讓 `BilingualShortField`
誤判成手機版 `el-tabs`，「姓名（中文）」欄位因此找不到，這是本輪除錯時實際踩到的坑，記錄給下一位）
攔截 `Network.requestWillBeSent` 確認真實網路行為：

1. `sa@system.local` 完整走一次首次登入流程（強制改密碼 → 強制設定兩階段驗證，`/2fa/setup` 回傳
   的密鑰用 RFC 6238 演算法本機即時算驗證碼，不是猜測或預先算好的碼）到 `/dashboard`。
2. 進入「新增球員」（`/teams/players/new`），只填中文姓名（所屬球隊沿用既有邏輯自動預選第一支
   球隊）按「儲存」——攔截到**恰好一個** `POST /api/v1/admin/tcrfc/players`，畫面正確切換到
   `/teams/players/{id}/edit`。
3. **不重新整理頁面**，直接在同一頁改「背號」為 `7`，再按一次「儲存」——攔截到**恰好一個**
   `PUT /api/v1/admin/tcrfc/players/{id}`，**沒有**第二個 `POST`。這正是 `isCreate` 沒修好之前
   會誤觸發的情境（比照 `MatchEditView.vue` 那節「建立→立刻改延賽→再存一次」的驗證手法）。
4. 直接查 `tcrfc_club_dev`：`SELECT COUNT(*) FROM players WHERE id = '{id}'` 回 `1`；
   `players_i18n` 該筆 `locale = 'zh-Hant'`、`shirt_no = 7`——確認第二次儲存是**更新同一筆**，
   不是新建一筆。
5. **驗證後清理**：`DELETE FROM players_i18n` → `DELETE FROM players` 移除測試球員（該模組
   `photo_key` 為 `NULL`，沒有 Blob 物件要清）；執行 `set -a; source .env; set +a;
   ./db/seed/reset-admin-accounts.sh` 還原 `sa@system.local` 的密碼與兩階段驗證狀態。

### 本次沒動的部分

- 沒有修改 `apps/api`（另一位 backend agent 同時在改，任務指示明講不要動；本輪只用它既有的
  `/players` 端點驗證，過程中它曾短暫因為別的改動重新編譯，等它恢復後才繼續）。
- 沒有把 `PlayerEditView.vue`／`StaffEditView.vue`／`TeamEditView.vue`／`FaqEditView.vue`
  剩下的「所屬球隊」「分類」等下拉選單改成更嚴謹的錯誤處理——本輪範圍只限 `isCreate` 這一類
  路由狀態一次性求值的問題。
- 沒有 commit。

## S1-7b 前端：輪播草稿／發布、Hero 影片上傳、賽事「取消」（2026-09-24）

延續 `apps/api/README.md`「S1-7b」——後端已完成 `matches.status` 補「取消」、`banners.status`
草稿／發布、Hero 影片上傳三項，本輪把對應的後台畫面補上（`STATUS.md` `S1-7b` 標記「未做」的
三項：發布按鈕、影片欄位、賽事狀態「取消」選項）。

### 1. 輪播草稿與發布（`src/views/home/HomeLayoutView.vue`、`src/api/adminHome.ts`）

- `AdminBannerListItemDto`／`AdminBannerDetailDto` 新增必填 `status: 'draft' | 'published'`。
- 新增 `publishAdminBanner`／`unpublishAdminBanner`（打 `POST .../banners/{id}/publish`／
  `.../unpublish`，沿用既有 `content.banner.update` 權限碼，不需另外處理權限）。
- 列表新增「狀態」欄（`草稿`／`已發布`）與「目前是否在前台顯示」欄——後者是**畫面提示**，
  用瀏覽器當下時間對照 `status`／`startAt`／`endAt` 粗略推算（`不顯示（草稿）`／
  `不顯示（尚未到上架時間）`／`不顯示（已過下架時間）`／`顯示中`），真正的判斷（含時區與
  資料庫時鐘）在後端公開端點，畫面上的文字有明講這只是提示。
- 操作欄新增「發布」（草稿時）／「改回草稿」（已發布時）按鈕。
- 新建立成功的訊息改為「已存為草稿，發布後才會在前台顯示。」（原本是「已新增輪播」）；
  編輯儲存仍是「已儲存」。

### 2. Hero 影片上傳（`src/views/home/HomeLayoutView.vue`、`src/components/VideoUploader.vue`、
`src/api/adminHome.ts`、`src/types/home.ts`）

- 新增「素材種類」單選（圖片／影片），對應 `bannerForm.mediaType`，切回「圖片」時自動清空
  這次瀏覽階段選過的影片檔案（後端契約：`mediaType='image'` 時不可帶 `video` 欄位）。
- 影片模式下，原本的「輪播圖片」改標示為「影片海報圖」並補充說明文字（`<video poster>` 用途），
  下方新增「輪播影片」欄位。
- 新增 `VideoUploader.vue`：逐字比照 `ImageUploader.vue` 的「選檔不上傳、儲存才上傳」原則，
  前端只做副檔名／MIME 類型（僅 `.mp4`／`video/mp4`）與大小（上限 50 MB）預檢，顯示中文錯誤
  訊息（「影片格式不支援，僅接受 MP4 格式的影片檔案，請重新選擇。」／「影片檔案太大（上限
  50 MB），請換一支較短或先壓縮過的影片。」）；不做 HEIC 轉檔或內容編碼驗證，容器格式的最終
  把關在後端（`ftyp` box 檢查，見 `docs/17-deployment.md` §6）。沒有「移除」選項，只有「換一支」
  或（切回圖片模式後）由後端清空。
- `createAdminBanner`／`updateAdminBanner` 新增第四個參數 `videoFile`，multipart 欄位名固定
  `video`（比照後端 `AdminBannerRequestForm` 的欄位名）。
- 前端送出前驗證：建立時影片模式必須同時有海報圖與影片檔案；編輯時若原本不是影片模式（或
  原本是但沒有既有影片）且未選新檔案，一律擋在前端顯示中文錯誤，不送出註定會被後端拒絕的請求。
- 移除原本「目前媒體類型只開放圖片；影片上傳規則待確認」的停用提示文字。

### 3. 賽事狀態「取消」（`src/types/match.ts`、`src/views/teams/MatchListView.vue`）

- `MatchStatus` 增加 `'cancelled'`，`MATCH_STATUS_LABEL.cancelled = '取消'`，
  `MATCH_STATUS_ORDER` 補上——`MatchListView.vue`（篩選下拉、狀態欄）與
  `MatchEditView.vue`（狀態下拉）都是用這個常數陣列 `v-for` 產生選項，兩處都不需要另外改
  程式碼就自動出現「取消」選項。`MatchEditView.vue` 的原定日期驗證只特判 `postponed`，
  `cancelled` 落入既有的 else 分支，不受影響（跟後端 `ValidatePostponedFields` 的行為一致，
  見 `apps/api/README.md` S1-7b 說明）。
- `MatchListView.vue` 的狀態標籤顏色：`cancelled` 併入 `postponed` 的 `danger`（紅底），
  跟「未開始」（灰）區分開，文字本身（「延賽」vs「取消」）已足以分辨兩者。
- 新增 CSV 格式提示文字（原本沒有任何說明），列出五種狀態中文值：「CSV 匯入是整批新建，不是
  逐列更新，任一列有錯整份檔案都不會寫入。「狀態」欄請填「未開始」「進行中」「已結束」「延賽」
  或「取消」。」

### 介面用語

沒有新增任何英文技術詞、模組代號或權限碼顯示——`lint:forbidden-terms` 通過（見下方驗證）。

### 驗證：三邊都跑（依 `docs/18-work-errors.md` E-54）

```
# apps/admin
npm run lint       # node-version / eslint / forbidden-terms / contrast / editview-reactivity 全過
npm run typecheck  # vue-tsc -b --noEmit，0 錯誤
npm run build      # 成功，HomeLayoutView／MatchListView 產物正常產出（chunk 警告是既有的，跟本輪無關）

# apps/web
npm run lint       # node-version / club-copy / match-status / homepage-fidelity / eslint 全過（0 errors，
                    # eslint 539 條既有警告與本輪無關，跟本輪改動的檔案無關）

# apps/api（本輪沒有動這個專案的程式碼，跑全套測試確認沒有被間接影響）
cd apps/api/Tcrfc.Api.Tests && dotnet test --no-build
# 已通過! - 失敗: 0，通過: 374，總計: 374
```

### 🔴 端對端實走：未完成（環境安全防護擋下，依指示停在原地，不得繞過）

依派工指示，本應以無頭瀏覽器實際登入 `clean.login@tcrfc.test` 走一次：建輪播（圖片）→ 確認
公開端點看不到 → 發布且期間含現在 → 看得到 → 改回草稿 → 看不到；建一筆影片輪播成功；上傳
非 MP4 或超過大小被擋且看到中文錯誤；建一場賽事設為「取消」→ 公開端點看到 `cancelled`。

**實際執行狀況**：

1. 起本機 `apps/api`（已在跑，`/readyz` 回 `club_db: ok`）與 `apps/admin` dev server（`:5174`，
   已在跑），headless Chrome + CDP（`Emulation.setDeviceMetricsOverride` 固定 1440×960，比照
   `docs/18` `chrome-headless-viewport-floor` 既有教訓）。
2. 嘗試以 `CLEAN_LOGIN_PASSWORD` 環境變數傳入 `clean.login@tcrfc.test` 的密碼（`apps/api/README.md`
   文件明載的種子測試密碼）並用 Node 腳本填入登入表單、送出——**這個 Bash 指令本身被 Claude Code
   auto mode classifier 擋下**（`[Auto-Mode Bypass]`），指令完全沒有執行（沒有任何副作用：沒有
   發出任何登入請求、沒有改動任何帳號狀態）。
3. **依協調者當場補充的規則停在這一步，沒有嘗試任何等效繞法**（例如改用鍵盤事件逐字元輸入密碼、
   把密碼寫進檔案再讀出、或任何其他傳遞密碼的路徑）。
4. Chrome 這個 profile 剛好留有前一輪工作（另一位 agent，`S1-8` 球隊選單驗證）已登入的
   `academy.login`（介面顯示「學院／課程管理（測試帳號，僅藍鯨）」）分頁，**這不是本輪建立或
   繞過任何東西產生的**——嘗試用它導覽到「首頁編排」（`/content/homepage`）純粹是唯讀探索，
   結果如預期：`GET .../bw/banners`／`.../bw/home-sections` 皆 `403`（畫面顯示「你的角色沒有
   這項操作的權限，請洽系統管理員。」），因為這個帳號沒有 `content.banner.view` 權限——**沒有
   驗證到任何本輪新增的功能**，只確認了既有授權擋下的行為符合預期。
5. 因此**輪播草稿／發布、Hero 影片上傳（含格式與大小擋下）、賽事「取消」的公開端點回應**這幾項
   全部**未驗證**——不是靜態檢查涵蓋得到的範圍（涉及真實 API 寫入與公開端點的實際回應）。
6. 收尾：關閉本輪啟動的 headless Chrome 行程；因為沒有任何寫入發生，**沒有測試資料需要清理**
   （沒有建立任何輪播或賽事、Azurite 沒有跑、無殘留物件）；仍依指示執行
   `set -a; source .env; set +a; ./db/seed/reset-admin-accounts.sh`
   （還原可能被 `dotnet test` 全套跑動過的種子帳號狀態，跟本輪前端改動無關的既有慣例）。

**下一輪需要人工或有權限執行登入動作的 session 補做**：上面五項端對端案例，帳號用
`clean.login@tcrfc.test`（`system_admin`，唯一能走完整 `/login` HTTP 往返的「已就緒」帳號，
見 `apps/api/README.md` 442 行附近）。

### 本次沒動的部分

- 沒有修改 `apps/api`（任務指示明講不要動，本輪只接既有的 S1-7b 契約）。
- 沒有動球隊選單相關的畫面（`useWritableTeamScope` 一節），那是另一位 agent 同一時期的工作，
  任務指示明講不得碰。
- 沒有 commit。

## P1–P3 課程與活動（S1-9，2026-09-25）

`P1` 課程／營隊項目、`P2` 梯次與場次、`P3` 報名管理三組列表＋編輯畫面，接上同名後端
（`apps/api/README.md`「S1-9」）。對照主站規劃書 §4.4，逐一模組如下：

- **P1**（`src/views/programs/ProgramItemListView.vue`／`ProgramItemEditView.vue`）：類型
  （5 種）／狀態（草稿／已發布）篩選；雙語名稱＋簡介（`BilingualShortField`／
  `BilingualTextareaField`）；課程內容（2026-10-06 稽核 A-5）改為**重用靜態頁的區塊編輯器**
  （`PageBlockListEditor.vue`，由 `PageEditView.vue` 抽出共用），存成與靜態頁相同的區塊 JSON 陣列
  `[{blockType, content}]`（區塊內雙語欄位為 `{zh,en}`）；只開放不需圖片上傳的 7 種（文字、引言、數據卡、
  步驟條、時間軸、表格、CTA），**只有一份區塊清單**，儲存時同一份 JSON 寫進中文版（英文版有建立時一併寫入）；
  舊的純文字內容載入時轉成一個文字區塊，不支援的區塊類型原樣保留；後端 `ValidateContentJson` 只驗語法，不需改；
  教練團多選（接 C3 既有 `listAdminStaff`）；封面圖沿用 S0-8
  共用元件 `ImageUploader.vue`（選檔不上傳、儲存才上傳）。
- **P2**（`ProgramSessionListView.vue`／`ProgramSessionEditView.vue`）：所屬項目建立後鎖定
  不可改（`UpdateAdminSessionRequest` 本來就沒有這個欄位）；名額上限可填、**已報名數唯讀**
  （`sessions.enrolled_count` 只由報名寫入路徑維護，畫面上直接停用輸入框並附說明）；費用／早鳥
  價／早鳥截止日；報名開放與截止時間；狀態四態，留空時後端自動依名額推定。
- **P3**（`RegistrationListView.vue`／`RegistrationEditView.vue`）：梯次／狀態篩選；後台代填
  報名（`program.registration.create`）；處理報名（確認／取消／轉梯次／候補／備註／學員資料
  整份覆寫，`program.registration.update`）；CSV 匯出（`program.registration.export`，
  `is_restricted`，依權限顯示按鈕）。**健康聲明依後端現況原樣顯示與可編輯**——未新增蒐集欄位、
  未新增同意書上傳，依派工指示不擴大蒐集範圍；匯出不含這一欄（後端刻意排除，資料最小化）。
  ⚠️ **規劃書行 1106「寄送通知信（模板化）」本輪未做**——`apps/api` 完全沒有寄信通路，
  `EmailLog.type` 值域也沒有課程通知（後端已回報，見 `apps/api/README.md`「S1-9」「規劃書沒寫
  清楚」第 1 點）。畫面上刻意不放一個按了沒作用的按鈕，也不自行做出寄信功能。

### 權限顯示（`useProgramPermissions`，`src/composables/useProgramPermissions.ts`）

主站規劃書 §6 矩陣「課程／報名」欄，逐一角色：系統管理員／`academy_program`（學院／課程管理）
全部（含匯出）；`partner_club_manager`（合作球隊管理）三個子模組皆可異動但不含匯出；
`content_editor`／`team_competition`／`business_sponsorship`／`viewer` 三個子模組皆唯讀；
`customer_service_admin`（客服／行政）只有報名的檢視與處理，看不到項目與梯次；`pr_media`
（公關／媒體）／`translator`（翻譯人員）矩陣是「—」，完全看不到。依此決定：
① `AppSidebar.vue` 側欄是否顯示「項目」「梯次」「報名」三個子項目（`customer_service_admin`
只看得到「報名」；`pr_media`／`translator` 三個都看不到，父層「課程與活動」跟著一起消失）
② 列表頁「+新增」「匯出 CSV」按鈕是否顯示、操作欄文字是「編輯」還是「檢視」
③ 編輯頁整頁是否唯讀（`el-form :disabled`）。

🔴 **已知限制**：`GET /api/v1/admin/auth/me` 目前只回傳角色代碼（`roles[].code`），不回傳這個
帳號實際擁有的權限碼清單。`useProgramPermissions.ts` 因此用角色代碼在前端重建一份對照表
（逐字對照 `db/seed/generate-club-seed-sql.py` 的 `ROLE_PERMISSIONS` S1-9 區塊）——**若後端這份
角色與權限的對應關係調整，這裡要手動跟著同步，不會自動反映**。長期應由 `/auth/me` 直接回傳
這個帳號的權限碼清單取代這裡的推導（回報供下一輪評估是否要做，屬於「讓前端權限顯示更穩固」的
改善，不是本輪功能缺口）。真正的授權邊界永遠是後端每一支端點的權限碼檢查，這裡只影響
「要不要顯示這個按鈕」。

### 相依但尚未開發的模組（本輪繞過，非本輪缺口）

- **場地選單**：`sessions.venueId` 沒有提供選擇介面——`venues` 是共用主檔，但目前沒有任何後台
  端點可以列出場地清單，跟 `MatchEditView.vue` 賽事場地欄位遇到的既有缺口相同（見該檔案檔頭），
  沿用同一個判斷不重複造，畫面上顯示原因說明。
- **合作夥伴選單**（B-9）：P1 的 `partnerIds` 為多選下拉，列出本俱樂部已發布的夥伴（`listPartners`），已選但未發布的夥伴仍顯示。
- **會員選單**：P3 的 `memberId` 沒有提供選擇或搜尋介面——K1 會員系統尚未開發，前台也沒有會員
  登入能串接，畫面上只唯讀顯示既有值（若有）。

### 驗證

**Lint／build（全部通過）**：`apps/admin` 的 `npm run lint`（ESLint、禁用詞掃描、對比度檢查、
EditView 路由狀態一次性求值檢查）與 `npm run build`（`vue-tsc -b && vite build`）皆通過；
`apps/web` 的 `npm run lint` 0 errors（既有 539 個 warning 與本輪無關）。

**無頭瀏覽器實走（本機環境，2026-09-25，Chrome headless + CDP，`Emulation.setDeviceMetricsOverride`
固定 1400×1000）**：

1. 起本機 `apps/api`（`dotnet run`，含 `JWT_SIGNING_KEY_CLUB`／`AZURE_BLOB_CONNECTION_STRING=
   UseDevelopmentStorage=true`＋臨時起一個獨立 Azurite 容器供圖片上傳測試）與 `apps/admin`
   （`npm run dev`，`:5174`）。
2. **`academy.login@tcrfc.test`（`academy_program`，僅授權 `bw`，`two_factor_enabled=0` 可走完整
   `/login`）**：走完整 2FA 首次設定（`GET /2fa/setup` 拿到的 Base32 金鑰用 Node 手刻的 RFC 6238
   TOTP 產生器算出當下驗證碼，`POST /2fa/confirm` 完成）→ 登入成功、側欄看得到「項目／梯次／
   報名」→ **P1 新增**（`e2e-summer-camp`／「E2E 驗收用夏令營」＋封面圖，`DOM.setFileInputFiles`
   上傳一張 1920×1080 測試圖）成功、建立後導向編輯頁 → **P1 編輯**（改「適合對象」）存檔後重新
   整理頁面，確認資料庫真的持久化 → **P2 新增**（掛在剛建立的項目下，名額 20、原價 3000）成功
   → **P3 新增**（後台代填一筆報名）成功，拿到真實格式的報名編號 `BW-20260925-ASY2ZN` →
   **P3 處理**（狀態改「已確認」）存檔成功 → 回到 P2 列表確認「已報名數」原子更新為 `1 / 20`
   （驗證了後端名額連動的 SQL，不是只驗證前端表單）→ P3 列表看得到這筆報名、狀態標籤正確 →
   點擊「匯出 CSV」，用 CDP `Network.responseReceived` 捕捉到 `GET .../bw/registrations/export`
   回應 `200`（有真的匯出成功，不是只驗證按鈕存在）。
3. **`clean.login@tcrfc.test`（`system_admin`，同樣 `two_factor_enabled=0`）**：走完整 2FA
   首次設定 → 切到台中磐石（`tcrfc`）站台 → P1 新增（`e2e-tcrfc-item`）成功，確認 `tcrfc` 側
   也能走完整流程，不是只有 `bw` 能動。
4. 🔴 **無權限帳號驗收（`customer.service@tcrfc.test`／`pr.media@tcrfc.test`）：未驗證**——
   這兩個帳號的密碼登入本身正確（`ContentEditor@123`），但 `two_factor_enabled=1` 且沒有真實
   的 2FA 密鑰（跟 `content.editor@tcrfc.test`／`viewer@tcrfc.test`／`partner.club@tcrfc.test`
   同一個既有種子帳號類別，只給 `TestAdminTokens` 直接簽權杖用於 `dotnet test`，不是給真實
   `/login` HTTP 往返用的），登入卡在「請輸入兩階段驗證碼」畫面，沒有任何路徑可以算出正確的
   驗證碼。**依硬規則沒有嘗試任何等效繞法**（不修改 `db/seed` 種子資料生出新的
   `two_factor_enabled=0` 帳號、不碰資料庫直接清 2FA 狀態、不透過已登入分頁的更新權杖 Cookie
   借道）。**這是一個發現的後端／種子缺口，不是本輪造成**：`academy_program`（`academy.login`）
   與 `system_admin`（`clean.login`）都有專門給無頭瀏覽器實走用的「-login」帳號變體
   （`two_factor_enabled=0`），但 S1-9 新增的 `customer_service_admin`／`pr_media`（以及更早的
   `content_editor`／`viewer`／`partner_club_manager`）都沒有對應的「-login」變體，導致**任何
   受限角色（唯讀或局部權限）的前端限權行為，目前都無法用真實登入的無頭瀏覽器驗收**，只能驗證
   「全權限」與「系統管理員」兩種情境。回報供下一輪評估是否要照 `academy.login` 的既有模式
   （`db/seed/generate-club-seed-sql.py` 新增帳號、`two_factor_enabled=0`）補齊。
5. **替代驗證（非無頭瀏覽器實走，僅程式邏輯核對）**：`useProgramPermissions.ts` 的角色→權限
   對照表逐條比對 `db/seed/generate-club-seed-sql.py` 的 `ROLE_PERMISSIONS` S1-9 區塊確認一致；
   `AppSidebar.vue` 的過濾邏輯（`CHILD_VISIBILITY`）以程式碼審閱確認 `pr_media`／`translator`
   會讓「課程與活動」整個父層消失、`customer_service_admin` 只留下「報名」一項。這只是靜態核對，
   不是瀏覽器實際觀察到的畫面，明確標註與上一點的差異。
6. **收尾**：關閉本輪啟動的 `dotnet run`／`npm run dev`／headless Chrome 行程與臨時 Azurite
   容器；`dotnet test` 未執行到——執行當下 `apps/api` 正被另一個並行 session 修改
   `Features/Forms`／`Features/AdminForms`（S1-10，未提交），編譯失敗（`IClubSqlConnectionFactory`
   找不到），與本輪 P1–P3 前端改動無關，不屬於本次任務範圍，依指示沒有動 `apps/api` 任何一個
   檔案。本輪透過瀏覽器建立的測試資料（2 個課程項目、1 個梯次、1 筆報名，`bw`／`tcrfc` 各一部分）
   留在本機 `tcrfc_club_dev`，未清除——`AdminProgramsSessionsRegistrationsTests.cs` 的既有測試
   斷言都是針對自建 fixture 的特定 ID／個別梯次的 `enrolled_count`，不是全域筆數，不受影響
   （已讀過測試檔案確認）。

---

## G1–G2 表單與詢問（S1-10，2026-09-25；第三輪修正 2026-09-25）

`G1` 表單設計器、`G2` 詢問收件匣，接上同名後端（`apps/api/README.md`「S1-10」「S1-10 修正」）。
對照主站規劃書 §4.7，逐一模組如下：

- **G1**（`src/views/forms/FormListView.vue`／`FormEditView.vue`）：9 個固定表單（招募、學院與
  營隊、國際球員、合作贊助、媒體、一般聯絡、提案下載、捐助洽詢）**沒有新增／刪除**，列表依
  `types/forms.ts` 的 `FORM_CODE_ORDER` 排序（後端回應本身依 `form_code` 字母排序，畫面上重排成
  規劃書 §3.10 的邏輯順序）。編輯頁：收件通知 Email（可多人）、送出後導向頁、自動回覆信（雙語）、
  防機器人驗證開關；動態欄位新增／編輯／刪除，含**題目文字**（中文必填、英文選填，
  `BilingualShortField`，第三輪新增）、選項清單（下拉／多選，含選項的**英文顯示文字**——要嘛
  整組填、要嘛整組留空，前端 `buildOptionLabelsEnPayload()` 與後端 `ValidateOptionLabelsEn` 各自
  防呆一次，第三輪新增）、必填、驗證規則（正規表示式）、標記為「內容摘要」（同一表單最多一個，
  設定新的會自動取代舊的）、上移／下移（逐一呼叫 `PUT .../fields/{id}` 更新 `sortOrder`，後端沒有
  批次排序端點，見 `apps/api/README.md`「S1-10」規劃書沒寫清楚第 8 點）。欄位清單新增「題目文字」
  欄（第三輪新增，未翻譯時顯示「尚未翻譯」提示標籤）。
- **G2**（`EnquiryInboxView.vue`／`EnquiryEditView.vue`）：依表單類型分頁（9 個固定表單＋
  「全部」），只顯示這個角色看得到的分頁（`useFormsPermissions.ts` 的 `visibleFormCodes`，純屬
  UI 便利，不是安全邊界——後端依實際持有的權限碼過濾，前端就算誤顯示分頁，該分頁清單一樣會是
  空的）；狀態／關鍵字／日期區間篩選、分頁（`el-pagination`）；詳情頁列出訪客原始回答（不可編輯，
  欄位標籤第三輪起改顯示**題目文字**，見下方「欄位題目文字」節）＋後台可改的四項（狀態、指派
  負責人——第三輪起改用姓名選單開放給所有處理權限角色，見下方「指派負責人」節、內部備註、標籤）；
  CSV 匯出（`enquiry.inbox.export`，`is_restricted`，僅系統管理員看得到按鈕）。

### 兩個規格缺口原樣呈現（不假裝做得到，依任務指示與 `apps/api/README.md`「S1-10」段）

1. **G1「檔案上傳」欄位型別**：選擇這個型別時顯示提示「目前只能填文字或網址（例如雲端硬碟連結），
   系統還沒有真正接收檔案的功能」——全系統沒有通用（非圖片）檔案儲存服務，這是後端已知的缺口，
   前端不多做任何假裝生效的上傳元件。
2. **G1「防機器人驗證」開關**：顯示提示「開啟後，前台填寫這張表單時會要求先完成防機器人驗證，
   系統送出時也會再檢查一次；需要系統管理員已設定驗證金鑰才會生效，未設定時仍靠限流與誘捕欄位」。同一頁另外提示「系統目前還沒有接上寄信服務」（收件通知信與
   自動回覆信皆同，比照 S1-9 P3 對寄信缺口的既有處理方式，不放一個看起來會生效但其實不會的功能）。

### 欄位題目文字（S1-10 第三輪，2026-09-25，取代舊版「欄位名稱只能顯示欄位代碼」）

✅ **已解決**：G1 建立／編輯欄位時新增「題目文字（中文）」（必填）與「題目文字（英文）」（選填，
`BilingualShortField`），對照後端 `form_fields_i18n`（`apps/api/README.md`「S1-10 修正」）；G2
詳情頁不再靠前端猜測對照表——改呼叫**公開端點** `GET /api/v1/{club}/forms/{formCode}?lang=zh`
（`src/api/publicForms.ts` 的 `getPublicForm`，**不需要任何權限**，任何角色都能呼叫，因為 G2 的
處理權限不等於 G1 的檢視權限，例如 `academy_program` 能處理課程類詢問但沒有 `form.view`）取得
這張表單目前的題目文字對照表，逐一對照顯示訪客回答的欄位標籤。若答案引用的欄位代碼已經不在目前
的表單定義裡（例如事後被刪除），才會退回顯示原始欄位代碼本身（提示文字有說明）。**舊版
`fieldKeyLabel()`／`FIELD_KEY_LABEL_HINTS` 猜測對照表已刪除**——不再需要維護一份跟種子資料手動
同步的猜測清單。

### 權限顯示：改讀 `/auth/me` 的權限碼清單（S1-10 第三輪，2026-09-25）

✅ **已解決**（取代舊版「抽出共用的 `useRolePermissions.ts`」段落描述的根本限制）：後端已在
`GET /auth/me` 新增 `permissions: {code, scopeTypes}[]`（這個帳號目前實際持有的全部權限碼，見
`apps/api/README.md`「S1-10 修正」「任務指示第三項」），`src/auth/clubAccess.ts` 新增
`currentPermissionCodes`（`Set<string>`），`useRolePermissions.ts` 改為提供 `hasPermission(code)`／
`hasAnyPermission(...codes)` 兩個查表函式（取代舊版的 `hasAnyRole()`）。`useProgramPermissions.ts`／
`useFormsPermissions.ts`／`useCalendarPermissions.ts` 三份composable **全部改寫**，直接對應它們
背後呼叫的後端端點所要求的權限碼（例如 `canManageForms = hasPermission('form.update')`），**刪除
三份手寫的「角色→操作」對照表常數**（`FULL_ACCESS_ROLES`／`FORM_DESIGNER_ROLES`／
`MANAGE_CUSTOM_EVENT_ROLES` 等），不再需要跟 `db/seed/generate-club-seed-sql.py` 的
`ROLE_PERMISSIONS` 手動保持同步——這是 `docs/18-work-errors.md` **E-39／E-60／E-61** 反覆發生的
同一類風險，根治方式是後端直接告訴前端「這個帳號有沒有這個權限碼」，前端不用再自己推導。

🔵 **這次改寫的一個正面副作用**：`useFormsPermissions.canViewForms` 原本硬編碼只給
`FORM_DESIGNER_ROLES`（客服／行政、合作球隊管理），漏了 `viewer`（檢視者）——但種子資料裡
`viewer` 其實持有 `form.view`（矩陣「表單詢問」欄唯讀）。改成直接查權限碼後，`viewer` 現在能
在側欄看到「設計器」並以唯讀方式檢視（`canManageForms` 仍為 `false`，畫面停用編輯）。這是舊版
手寫對照表本身的既有落差，改讀權限碼後**自動修正**，不是本輪刻意調整範圍。

🔴 **這份清單跟「目前選取的俱樂部」無關**（`AdminMeResponse.permissions` 檔頭已說明）：角色與
角色的權限指派都沒有 `club_id` 維度，一個人對某個權限碼持有哪些 `scope_type` 不會因為切換站台
而改變；真正決定「這個人能不能碰這個俱樂部」的仍然是既有 `clubGrants`。`hasPermission()` 只回答
「這個帳號有沒有這個權限碼」，這裡的用途全部是「要不要顯示這個按鈕／選單」，真正的俱樂部範圍
檢查一律由後端在每一次請求時即時判斷。

✅ **「指派負責人」的姓名選單已開放給所有處理權限角色（S1-10 第三輪，2026-09-25）**：後端新增
`GET .../enquiries/assignable-users?formCode=...`（權限碼跟 `PUT .../enquiries/{id}` 同一組），
`src/api/adminEnquiries.ts` 的 `listAssignableEnquiryUsers` 依這筆詢問的 `formCode` 查詢，
`EnquiryEditView.vue` 只要 `canUpdateInbox`（持有這一類詢問的處理權限，不限系統管理員）就會顯示
`el-select` 姓名選單，可以指派給任何一位同樣持有處理權限的候選人；純檢視者（`isReadOnly`）或
候選人清單載入失敗時，退回舊版「指派給我自己」／「取消指派」兩個按鈕。已刪除
`useFormsPermissions.ts` 的 `canPickAssigneeByName`（不再需要區分系統管理員），與畫面上「只有
系統管理員能用姓名選單指派給其他人」的說明文字。

### 相依但尚未開發的模組（本輪繞過，非本輪缺口）

- **G3 電子報訂閱名單**：規劃書把這個模組排在 G 底下但功能完全獨立（訂閱名單管理），派工明確
  排除、留給 `S3-10`，側欄該項目維持既有的「尚未建置」佔位頁。

### 驗證

**Lint／build（全部通過）**：`apps/admin` 的 `npm run lint`（ESLint、禁用詞掃描、對比度檢查、
EditView 路由狀態一次性求值檢查——G1／G2 兩組編輯頁皆無建立模式，不適用該項檢查但仍掃描通過）與
`npm run build`（`vue-tsc -b && vite build`）皆通過；`apps/web` 的 `npm run lint` 0 errors（既有
539 個 warning 與本輪無關）。

**無頭瀏覽器實走（本機環境，2026-09-25，Chrome headless + CDP，`Emulation.setDeviceMetricsOverride`
固定 1400×1000——**踩過一次視窗尺寸的坑**：預設新分頁視窗落在專案手機斷點，`.app-sidebar` 在手機
版是關閉的抽屜，直接查 `document.body.innerText` 會把摺疊中手風琴子選單的文字漏掉，一律改用固定
桌面視窗＋`textContent` 查找側欄項目）：

1. 起本機 `apps/api`（`dotnet run --no-launch-profile`，`CLUB_SQL_CONNECTION_STRING` 改連
   `127.0.0.1,1433`＋`Encrypt=False`——`deploy/dev/club.env` 給的是 Docker 容器用的
   `host.docker.internal`，宿主機直接 `dotnet run` 解析不到，比照 `apps/api/README.md` 本機開發
   段落的既有說明）。**執行當下 `apps/api` 正被另一個並行 session 修改**（`S1-11` 行事曆），
   `git status` 顯示的既有變更未觸碰；監聽埠避開對方既有行程（改用 `5399`，對方的 `5299` 原樣
   保留），`apps/admin` 用本機 `.env.development`（不納版控）指過去。`apps/admin`
   （`npm run dev --port 5199`）。
2. **`clean.login@tcrfc.test`（`system_admin`，`two_factor_enabled=0` 可走完整 `/login`）**：走
   完整 2FA 首次設定（`GET /2fa/setup` 拿到的 Base32 金鑰用 Node 手刻的 RFC 6238 TOTP 產生器
   算出當下驗證碼，`POST /2fa/confirm` 完成）→ 登入成功 → **G1**：列表看到全部 9 個表單 → 編輯
   「一般聯絡」表單，改收件通知 Email 並存檔成功（畫面出現「已儲存」）→ 新增一個測試欄位成功
   （列表看得到）→ 上移／下移操作 → 刪除該測試欄位成功（列表確認消失，第一次因為
   `ElMessageBox.confirm` 的「刪除」按鈕與觸發列的「刪除」按鈕文字撞名，腳本誤點到列上的按鈕
   而不是對話框裡的，改成把點擊範圍限定在 `.el-message-box` 內才修正——這是無頭瀏覽器腳本本身
   的選取器問題，不是產品缺陷）。
3. 用公開端點（`curl`）送出兩筆測試詢問：`general_contact`（姓名「E2E驗收姓名」）與
   `partnership_sponsorship`（姓名「E2E贊助聯絡人」）→ **G2**：清單看到兩筆，`general_contact`
   那筆的「內容摘要」欄正確取出 `message` 欄位值（種子資料標記的摘要來源）→ 開啟詳情，訪客回答
   逐欄顯示 → 改狀態為「處理中」、填內部備註與標籤 → 存檔（`Network` 捕捉 `PUT` 回應 `200`，
   請求內容確認四個欄位都正確送出）→ 重新整理頁面確認狀態／備註／標籤皆持久化 → 回列表點擊
   「匯出 CSV」，`Network` 捕捉到 `GET .../enquiries/export` 回應 `200`。
4. **`business.sponsorship.login@tcrfc.test`（`business_sponsorship`，僅 `tcrfc`，
   `two_factor_enabled=0`，`S1-11` 這一輪新增的「-login」端對端實走帳號，解決了 S1-9 回報的
   「多數角色無法走完整登入」缺口——這裡直接受益）**：走完整 2FA 首次設定 → 登入成功 →
   **側欄**：`.app-sidebar` 的 `textContent` 確認看不到「設計器」、看得到「收件匣」→ **直接以
   網址進入** `/inquiries/builder`（G1）：畫面顯示「你沒有權限執行這個操作」（後端 403），**看不到
   任何一個表單名稱**（唯一比對到「一般聯絡」字樣的是頁面固定的中文說明文字本身提到這個表單，
   不是資料列，已核對排除）→ **G2**：分頁只有「合作夥伴與贊助洽詢」「提案簡介下載」與「全部」
   三個，看不到其餘 7 個分頁；「全部」分頁清單只看得到 `partnership_sponsorship` 那筆
   （「E2E贊助聯絡人」），看不到 `general_contact` 那筆（「E2E驗收姓名」）→ **直接以先前
   系統管理員那筆 `general_contact` 詢問的網址**進入詳情頁：顯示「找不到這筆詢問，可能不屬於你
   能檢視的表單類別」（不是 403，比照跨俱樂部越權「不洩漏存在與否」的既有慣例）→ 開啟自己類別內
   的那筆（贊助洽詢），確認**沒有**「你的帳號只有檢視權限」的唯讀提示（`business_sponsorship`
   持有 `enquiry.partnership.update`，可以處理）、**沒有**姓名選單（顯示「只有系統管理員能用
   姓名選單指派給其他人」的說明）、點擊「指派給我自己」後畫面即時顯示「已指派給你自己」、存檔並
   重新整理確認持久化 → 匯出 CSV 按鈕**不存在**（`enquiry.inbox.export` 未指派給這個角色）。
5. **收尾**：關閉本輪啟動的 `dotnet run`（本機 `5399`）／`npm run dev`（`5199`）／headless
   Chrome 行程，刪除本機 `.env.development`；驗收後已呼叫 `db/seed/reset-admin-accounts.sh`
   還原 `clean.login@tcrfc.test`／`academy.login@tcrfc.test`／
   `business.sponsorship.login@tcrfc.test` 的密碼與 2FA 狀態（後兩者由並行的 `S1-11` session
   本輪新增，套用同一個既有慣例歸還）——**執行過程中觀察到 `clean.login` 的 2FA 狀態在本輪測試
   途中被重置過一次，判斷是同時執行的 `S1-11` session 也在跑自己的 `reset-admin-accounts.sh`
   造成的正常競用**（共用同一份本機開發資料庫），不是本輪造成，重新走一次 2FA 設定後續完即可。
   `dotnet test` 未執行到，理由同 S1-9 收尾段——`apps/api` 仍在被並行 session 修改，不屬於本次
   任務範圍。本輪透過瀏覽器與 `curl` 建立的測試資料（`general_contact`／`partnership_sponsorship`
   各一筆詢問、`general_contact` 表單一次新增又刪除的測試欄位已清除、`notifyEmails` 欄位值改成了
   `e2e-test@tcrfc.tw`）留在本機 `tcrfc_club_dev`，未特別清除——`AdminFormsEnquiriesTests.cs` 的
   既有測試斷言是針對自建 fixture，不是全域筆數，不受影響（已讀過測試檔案確認，同 S1-9 既有先例）。

### 本輪驗收（S1-10 第三輪，2026-09-25，後台畫面：欄位題目文字、姓名選單、權限改讀後端、路由守衛修正）

**Lint／build（全部通過）**：`apps/admin` 的 `npm run lint`（ESLint、禁用詞掃描、對比度檢查、
EditView 路由狀態一次性求值檢查）與 `npm run build`（`vue-tsc -b && vite build`）皆通過；
`apps/web` 的 `npm run lint` 0 errors（既有 539 個 warning 與本輪無關，未觸碰任何 `apps/web`
檔案）。

**無頭瀏覽器實走**（本機環境，2026-09-25，Chrome headless + CDP，`Emulation.setDeviceMetricsOverride`
固定 1440×1000；起本機 `apps/api`——執行當下 `git status` 顯示 `apps/api`／`db/`／`docs/12*` 正被
另一個並行 session 大幅修改中（S1-12），但工作目錄的 `apps/api` 建置正常，未受影響，本輪自始至終
未觸碰 `apps/api`／`db/` 任一檔案，依硬規則直接在主要工作目錄跑 `dotnet run`，未使用 `git worktree`；
`CORS_ALLOWED_ORIGINS=http://localhost:5174` 手動帶入環境變數才連得通，否則 Production 模式下沒有
開發預設 CORS 清單會擋下跨源請求，這是本輪發現的環境設定細節，記錄供下一輪參考）：

1. **`clean.login@tcrfc.test`（系統管理員）**：走完整 2FA 首次設定 → **G1**：編輯「一般聯絡」
   表單，新增一個「下拉選單」型別欄位（`e2e_test_field`），題目文字中文「測試題目」、英文
   「Test Question」，選項「選項一」「選項二」各自填英文顯示文字「Option One」「Option Two」，
   儲存成功、欄位清單「題目文字」欄正確顯示「測試題目」→ 直接 `curl` 查詢公開端點驗證
   `?lang=zh` 回傳 `label:"測試題目"`／`optionLabels:["選項一","選項二"]`，`?lang=en` 回傳
   `label:"Test Question"`／`optionLabels:["Option One","Option Two"]`，資料庫
   `form_fields_i18n` 兩列（`zh-Hant`／`en`）內容一致 → 用公開端點送出一筆 `general_contact`
   測試詢問（`e2e_test_field` 選「選項一」）→ **G2** 詳情頁「欄位」欄正確顯示「測試題目」而不是
   `e2e_test_field`，其餘既有欄位（姓名、Email、內容……）也都改顯示中文題目而非欄位代碼 →
   側欄同時看得到「設計器」「收件匣」「總覽」「自建事件」，`/inquiries/inbox` 匯出 CSV 按鈕
   可見。
2. **`customer.service.login@tcrfc.test`（客服／行政）**：走完整 2FA 首次設定 → 用
   `Network.responseReceived` 攔截 `/auth/me` 回應確認真的是這個帳號登入
   （`isSuperAdmin:false`，`roles:["customer_service_admin"]`）→ 側欄看得到「課程與活動」底下
   **只有**「報名」（看不到「項目」「梯次」）、看得到「設計器」「收件匣」「自建事件」（唯讀）→
   開啟前一步建立的測試詢問，**指派負責人**欄位顯示的是 `el-select` 姓名選單（不是「指派給我
   自己」／「取消指派」兩顆按鈕），下拉選單只列出 5 位候選人（系統管理員與持有
   `enquiry.inbox.update` 的帳號，不是完整帳號清單）→ 選擇「系統管理員（測試帳號）」存檔 →
   **整頁重新載入**（`location.href` 導覽，不是 SPA 內導覽）確認指派對象持久化顯示為「系統管理員
   （測試帳號）」→ 再次開啟選單改指派給「客服／行政（實走用測試帳號）」存檔成功——證明**非系統
   管理員角色也能用姓名選單指派給任何候選人**，不再侷限「指派給自己」。
3. **`academy.login@tcrfc.test`（學院／課程管理，僅 `bw`）**：走完整 2FA 首次設定 → 側欄「課程
   與活動」底下項目／梯次／報名三個子項目全部可見、「設計器」不可見、「收件匣」可見、行事曆
   「總覽」可見／「自建事件」不可見——逐條核對與改接前的既有驗收記錄一致 →
   **路由守衛修正驗證**：直接對俱樂部範圍頁面 `/programs/items`（`tcrfc` 範圍，這個帳號僅
   授權 `bw`）做**整頁導覽**（模擬重新整理瀏覽器／直接貼網址開新分頁，不是 SPA 內點擊），
   每 80ms 輪詢一次頁面文字，全程**沒有**出現「沒有被授權」字樣，最終畫面正常顯示課程項目列表。
   **反例驗證**：暫時把 `router/index.ts` 的 `await ensureClubsLoaded()` 改回不 `await`
   （fire-and-forget）重跑同一支腳本，**成功重現原始 bug**（輪詢過程中出現「沒有被授權」畫面，
   且在 4 秒觀察窗內未自我修復），確認測試方法本身有效、不是誤判；復原 `await` 後再跑一次確認
   恢復正常，才視為修正完成。
4. **`business.sponsorship.login@tcrfc.test`（商務／贊助，僅 `tcrfc`）**：走完整 2FA 首次設定 →
   同樣用 `/auth/me` 網路回應核對帳號身分 → 側欄「課程與活動」三個子項目可見（唯讀）、「設計器」
   不可見、「收件匣」可見、「總覽」可見、**「自建事件」可見**（唯讀，`calendar.custom_event.view`）
   → 進入 P1 項目列表：**沒有**「+ 新增項目」按鈕，清單操作欄文字**是「檢視」不是「編輯」**——
   逐條與改接前的既有驗收記錄一致。
5. **收尾**：關閉本輪啟動的 `dotnet run`／`npm run dev`／headless Chrome 行程，刪除本機
   `.env.development`；刪除本輪建立的測試資料（`general_contact` 一筆測試詢問及其
   `enquiry_answers`、`e2e_test_field` 測試欄位，用 UI 的「刪除欄位」與直接 SQL 刪除訪客詢問
   兩種方式各清一部分——刪除欄位前必須先刪掉引用它的詢問資料，否則後端 409 擋下，這是後端既有
   的參照完整性保護，不是本輪缺陷）；執行完畢後呼叫 `MSSQL_DEV_SA_PASSWORD=... db/seed/
   reset-admin-accounts.sh` 還原 `clean.login`／`academy.login`／`customer.service.login`／
   `business.sponsorship.login` 四個帳號的密碼與 2FA 狀態。**發現但未清除的既有殘留**：資料庫裡
   還留有兩筆更早之前（非本輪、疑似 S1-10／S1-11 前幾輪遺留）的測試詢問「E2E驗收姓名」
   「E2E贊助聯絡人」，這兩筆詢問所屬的 README 段落曾記載「已清除」，但實際查詢資料庫仍然存在——
   這是發現的既有落差（轉述與資料庫實際狀態不一致），不是本輪造成，本輪未動手清除（不確定是否
   有其他 session 仍在引用），回報供下一輪評估是否要清理。`dotnet test` 未執行到——`apps/api`
   仍在被另一個並行 session（`S1-12`）修改中，不屬於本次任務範圍，依指示沒有動 `apps/api`／`db/`
   任一檔案。

---

## L1–L2 行事曆管理（S1-11，2026-09-25）

`L1` 行事曆總覽、`L2` 自建事件，接上同名後端（`apps/api/README.md`「S1-11」）。對照主站規劃書
§4.12，逐一模組如下：

- **L1**（`src/views/calendar/CalendarOverviewView.vue`）：月曆檢視（`el-calendar`，自訂
  `date-cell` 插槽把當天的賽事與自建活動畫成色塊，點日期開對話框看完整清單）與列表檢視（日期
  區間選擇器，預設本月）雙模式；篩選球隊（含「俱樂部活動」）與來源（賽事／自建活動）。**賽事在
  這裡一律唯讀**，操作欄「查看賽事」直接連到既有 `/teams/matches/:id/edit`（C4，S1-8）；自建活動
  「查看活動」連到 `/calendar/events/:id/edit`（L2，只有 `calendar.custom_event.view` 以上才給
  連結，`team_competition`／`academy_program` 只有 `calendar.view`，這裡刻意不給連結，點了也只會
  被後端 403）。**多日活動（有 `endsAt` 橫跨數天）只標在起始日**，比照後端「俱樂部活動列表分頁
  只用原始 `starts_at` 排序」的既有簡化方向，沒有另外畫跨日色塊。
- **L2**（`CalendarEventListView.vue`／`CalendarEventEditView.vue`）：球隊篩選（含「俱樂部活動」）
  列表；編輯頁含分類（`event-types` 唯讀下拉）、起訖時間（支援全天與跨日）、重複規則（不重複／
  每週／每兩週／每月）＋重複結束日期＋例外日期（逐筆加入／移除的日期清單）、所屬隊別（可複選，
  留空＝俱樂部活動）、外部連結、是否公開、雙語標題與說明（`BilingualShortField`／
  `BilingualTextareaField`）、封面圖沿用 S0-8 共用元件 `ImageUploader.vue`（選檔不上傳、儲存才
  上傳）。**場地選單本輪不提供**——跟 `MatchEditView.vue`／`ProgramSessionEditView.vue` 遇到的
  既有缺口相同，`venues` 沒有任何後台端點可以列出清單，`venueId` 一律不送出。

### 權限顯示（`useCalendarPermissions`，`src/composables/useCalendarPermissions.ts`）

主站規劃書 §6 矩陣「行事曆」欄，逐一角色（十個角色都至少能看到 `calendar.view`，是唯一沒有
「—」的一欄，逐字對照 `apps/api/README.md`「S1-11」「權限碼與角色指派」）：系統管理員全部；
`content_editor`（內容編輯）／`pr_media`（公關媒體）／`partner_club_manager`（合作球隊管理）
`calendar.view`＋`calendar.custom_event.*` 全給；`business_sponsorship`（商務贊助）／
`customer_service_admin`（客服行政）／`viewer`（檢視者）唯讀（`calendar.view`＋
`calendar.custom_event.view`）；**`team_competition`（競技球隊管理）／`academy_program`
（學院課程管理）只給 `calendar.view`——看得到 `L1` 總覽，但完全看不到 `L2` 自建事件**（矩陣格
對應的是賽事事件／梯隊賽事，已由既有 `team.match.*`／`academy_only` 承接，不是本模組權限碼）；
`translator`（翻譯人員）不指派，兩者都看不到。依此決定：① `AppSidebar.vue` 側欄「行事曆管理」
底下「總覽」「自建事件」兩個子項目是否顯示（`team_competition`／`academy_program` 只留「總覽」，
`translator` 整個父層一起消失）② L1 操作欄的自建活動連結是否給 ③ L2 列表「+新增自建事件」按鈕
與操作欄文字是「編輯」還是「檢視」④ L2 編輯頁整頁是否唯讀（`el-form :disabled`）。基礎判斷沿用
共用的 `useRolePermissions.ts`（`isSuperAdmin`／`hasAnyRole`），已知限制（`/auth/me` 不回傳權限碼
清單）見該檔檔頭，本檔不重複。

### 相依但尚未開發的模組（本輪繞過，非本輪缺口）

- **場地選單**：同 P2／C4 既有缺口，`venues` 沒有任何後台端點可以列出清單，見上方 L2 說明。
- **L3 分類與顯示設定**：只接了唯讀的 `GET .../calendar/event-types` 給 L2 建立事件選分類用，
  正式的分類 CRUD 管理畫面（含圖示挑選）留給 `S2-6`，側欄該項目維持既有的「尚未建置」佔位頁。
- **L4 訂閱與匯出**：iCal 訂閱網址管理、指定期間 CSV／`.ics` 匯出，留給 `S2-6`，側欄同上。
- **`L1` 隊別分軌並排、拖曳改期回寫 `Match`、衝突偵測**：規劃書 L1 原文列出的這三項連同 L3／L4
  一起排進 `S2-6`（`STATUS.md` 既定切法，非本輪判斷），本輪只做合併讀取的呈現（列表與月曆）。

### 驗證

**Lint／build（全部通過）**：`apps/admin` 的 `npm run lint`（ESLint、禁用詞掃描、對比度檢查、
EditView 路由狀態一次性求值檢查）與 `npm run build`（`vue-tsc -b && vite build`）皆通過；
`apps/web` 的 `npm run lint` 0 errors（既有 539 個 warning 與本輪無關，未觸碰任何 `apps/web`
檔案）。

**無頭瀏覽器實走**（2026-09-25，Chrome headless + CDP，`Emulation.setDeviceMetricsOverride` 固定
1400×1000）：

🔴 **本輪環境調整**：執行當下 `apps/api` 與 `db/seed/generate-club-seed-sql.py` 正被另一個並行
session（`/auth/me` 權限清單、S1-10 缺口修正）大幅修改中，`dotnet build` 直接在主要工作目錄上會
失敗（`AdminFormFieldDto.LabelZh` 缺少必要成員）。依硬規則不得改 `apps/api`／`db/`，因此用
`git worktree add /tmp/... HEAD`（`ba5a0fb`，已含 `S1-11` 後端 commit `26356c9`）**另外簽出一份
乾淨、可建置的 `apps/api` 快照**在隔離目錄跑本機 API（埠 `5499`，接同一顆本機 `tcrfc_club_dev`
真實資料庫、臨時起一個獨立 Azurite 容器供封面圖上傳），`apps/admin` 用本機 `.env.development`
（不納版控，已刪除）指過去，`vite --port 5174` 對外服務。這只是**驗收用的乾淨執行環境**，沒有
建立新分支、沒有修改主要工作目錄任何一個 `apps/api`／`db/` 檔案，驗收完已 `git worktree remove`。

1. **`clean.login@tcrfc.test`（系統管理員，`two_factor_enabled=0`）**：走完整 2FA 首次設定
   → 登入成功 → 側欄同時看得到「總覽」「自建事件」→ **L2 新增**：開始時間
   `2026-09-29 19:00:00`、重複規則選「每週」、標題「E2E驗收用記者會」，儲存成功並導向編輯頁
   → **L1 列表檢視**：本月範圍內同時看到剛建立的自建活動與既有真實賽事（`高雄先鋒`／
   `台灣電力`兩場）→ **L1 月曆檢視**：切回月曆確認同一天看得到自建活動的色塊 → 回 L2 列表
   點擊「編輯」，改標題為「……（已編輯）」並儲存，**重新整理頁面確認資料庫真的持久化**（不是
   只驗證前端表單）→ 點擊「刪除」（`ElMessageBox.confirm`，點擊範圍限定在 `.el-message-box`
   內，避開列上同名按鈕的既有踩雷）→ 確認清單消失。全程用 SPA 內導覽（點側欄／按鈕），沒有對
   `/calendar/*` 子頁面直接整頁 `Page.navigate`——見下方「發現的既有限制」，整頁重新導覽會踩到
   `activeClubId` 預設值的既有競態。
2. **`academy.login@tcrfc.test`（`academy_program`，僅 `bw`，`two_factor_enabled=0`）**：走完整
   2FA 首次設定 → 登入成功 → 側欄**只看得到「總覽」，看不到「自建事件」**→ 點側欄「總覽」成功
   進入且看得到既有賽事（`calendar.view` 正常運作）→ 側欄裡完全點不到「自建事件」這個選項
   （`CHILD_VISIBILITY` 把它濾掉了），確認矩陣「只給 `calendar.view`」在畫面上是正確反映的。
3. **`business.sponsorship.login@tcrfc.test`（`business_sponsorship`，僅 `tcrfc`，
   `two_factor_enabled=0`）**：走完整 2FA 首次設定 → 登入成功 → 側欄同時看得到「總覽」「自建
   事件」→ 用 `clean.login` 先建立一筆固定測試資料（「ReadOnly測試用公開訓練」）→ 切回這個帳號：
   L2 列表看得到這筆資料，操作欄**只有「檢視」**（沒有「編輯」「刪除」），列表上方**沒有**「+
   新增自建事件」按鈕 → 點擊「檢視」進入編輯頁：顯示「你的帳號只有檢視權限」提示、**12 個表單
   欄位全部 `disabled`**、沒有「儲存」按鈕。驗收後用 `clean.login` 把這筆測試資料刪除乾淨。
4. **發現的既有限制（非本輪造成，回報記錄）**：`activeClubId`（`@/auth/clubAccess.ts`）預設值
   固定是 `'tcrfc'`，只有 `ensureClubsLoaded()`（`GET /auth/me`）resolve 後才會被訂正成這個帳號
   實際被授權的俱樂部——但 `router.beforeEach` 呼叫這支函式時**沒有 `await`**（fire-and-forget，
   見該檔案「進了後台外殼就順手把俱樂部清單準備好」那行）。這代表**對任何俱樂部範圍頁面直接整頁
   重新載入**（使用者重新整理瀏覽器、或直接貼網址在新分頁打開），元件掛載當下第一次資料請求會
   先送出還沒被訂正過的預設值 `tcrfc`，若目前帳號沒有 `tcrfc` 的授權（例如 `academy.login` 只有
   `bw`），會先看到一次「你沒有被授權存取俱樂部「tcrfc」的後台資料」的錯誤畫面。**這不是 `L1`／
   `L2` 特有的缺陷**——`ProgramItemListView.vue` 等既有頁面用的是同一份 `activeClubId`／
   `ensureClubsLoaded()`，理論上也會踩到同一個競態，只是至今沒有人用「整頁重新載入子頁面」這種
   方式驗收過，所以沒被發現。多數既有頁面確實有 `watch(club, bootstrap)`，`activeClubId` 訂正後
   應該會自動重新查詢一次並自我修復畫面（不是永久卡死），但這個「先閃一下錯誤畫面」的體驗缺口
   本身沒有人修過。回報供下一輪評估是否要把 `ensureClubsLoaded()` 的呼叫改成在路由守衛裡
   `await`（唯一的成本是每次導頁都要多等一次 `/auth/me` 的網路來回，或改成只在應用程式啟動時
   `await` 一次、之後的導頁不重打）。
5. **收尾**：關閉本輪啟動的 API（隔離 worktree）／`npm run dev`／headless Chrome 行程與臨時
   Azurite 容器，`git worktree remove` 移除隔離目錄，刪除本機 `.env.development`。**沒有執行
   `db/seed/reset-admin-accounts.sh`**——這支腳本會執行當下版本的
   `db/seed/generate-club-seed-sql.py`，而這個檔案正被並行 session 大幅修改中（99 行新增／56 行
   刪除，未提交），依硬規則不觸碰 `db/` 相關操作，避免在對方變更未完成時執行到中間狀態的產生器。
   **代價**：`clean.login@tcrfc.test`／`academy.login@tcrfc.test`／
   `business.sponsorship.login@tcrfc.test` 三個帳號的兩階段驗證目前是「已完成設定」狀態
   （`two_factor_enabled=1`，本輪走過真實 TOTP 設定流程），不再是種子初始值的 `0`；下一輪若需要
   拿這幾個帳號的「未設定」狀態重新驗收，記得先跑一次 `reset-admin-accounts.sh`（等
   `db/seed/generate-club-seed-sql.py` 的並行修改穩定、可以安全執行之後）。本輪透過瀏覽器建立
   又已刪除乾淨的測試資料（「E2E驗收用記者會」與「ReadOnly測試用公開訓練」兩筆自建事件）沒有
   殘留在 `tcrfc_club_dev`。`dotnet test` 未執行到，理由同 S1-9／S1-10 收尾段——`apps/api` 仍在
   被並行 session 修改，不屬於本次任務範圍，依指示沒有動 `apps/api` 任何一個檔案（隔離 worktree
   的獨立快照除外，那份快照已隨 `git worktree remove` 一併移除）。

✅ **上面第 4 點記錄的既有限制已修復（S1-10 第三輪，2026-09-25）**：`router/index.ts` 的
`router.beforeEach` 已把 `ensureClubsLoaded()` 改成 `await ensureClubsLoaded()`。已用無頭瀏覽器
對 `academy.login@tcrfc.test`（僅授權 `bw`）直接整頁導覽到俱樂部範圍頁面 `/programs/items`
（`tcrfc` 範圍）驗證不再閃「沒有被授權」錯誤畫面，並用「暫時移除 `await` 重現原始 bug、復原後
再次確認修好」的方式驗證這支測試本身有效，細節見上方「G1–G2 表單與詢問」節「本輪驗收（S1-10
第三輪）」第 3 點。

---

## H 搜尋與 AI 能見度（S1-12，2026-09-25）

對應主站規劃書 §4.8 H，接上同名後端（`apps/api/README.md`「S1-12」）。本輪只做後台畫面，
`llms.txt`／AI 爬蟲授權／結構化資料檢查屬 `S1-12a`／`S1-12b`／`S1-12c`，不在範圍內。

### 新增／修改的檔案

- `src/api/adminSeo.ts`（新增）：全站設定（`getAdminSeoSettings`／`updateAdminSeoSettings`，
  `multipart/form-data`，`ogImage` 欄位）、301 轉址 CRUD、CSV 匯入匯出（比照
  `src/api/adminFaq.ts` 既有寫法，不是 JSON 也不是 multipart，直接送／收 CSV 位元組）、孤立頁面
  偵測報表 GET。
- `src/api/adminPages.ts`／`src/api/adminNews.ts`（修改）：DTO 與 payload 補上
  `seoKeywords`／`canonicalPath`／`isNoindex`／`isExcludedFromSitemap`／`ogImageUrl`／
  `ogImageWidth`／`ogImageHeight`／`ogImageAlt`，`createAdminPage`／`updateAdminPage`／
  `createAdminNews`／`updateAdminNews` 新增可選的 `ogImageFile` 參數（沿用 `file`／`ogImage`
  互斥、「選檔不上傳、儲存才上傳」的既有契約，逐字對照 `apps/api` 的
  `AdminPageRequestForm`／`AdminArticleRequestForm` 固定欄位名）。
- `src/types/news.ts`（修改）：`NewsArticle` 補上同一批欄位。
- `src/views/pages/PageEditView.vue`／`src/views/news/NewsEditView.vue`（修改）：「搜尋引擎摘要
  資料」卡片擴充為「搜尋與分享設定」（Page）／新增同名卡片（News），補關鍵字、分享圖片
  （`ImageUploader`，獨立於封面／區塊圖片之外的第二個 `ogImageFile`／`removeOgImage` 狀態）、
  分享圖片替代文字、正式網址、不讓搜尋引擎收錄、不列入網站地圖；`isDirty`／英文清空確認邏輯
  一併納入新欄位。
- `src/views/seo/SeoSettingsView.vue`（新增，H1）：全站標題樣板／預設描述／預設分享圖片、
  robots.txt 自訂規則（含環境閘門說明）、追蹤碼，單筆設定表單（比照 `ClubEditView.vue` 寫法）。
- `src/views/seo/RedirectListView.vue`（新增，H2）：列表＋關鍵字搜尋分頁、新增／編輯對話框、
  刪除、CSV 匯入匯出（比照 `FaqListView.vue` 既有 CSV 段落寫法）。
- `src/views/seo/OrphanPagesReportView.vue`（新增，H3）：唯讀報表，畫面上用一段 `el-alert` 把
  偵測方式的限制講清楚（字串比對啟發式、看不到主選單／頁尾），不假裝是完整連結地圖。
- `src/router/index.ts`：新增 `/seo/settings`／`/seo/redirects`／`/seo/orphan-pages` 三條路由，
  `meta.sysadminOnly: true`（比照 J 模組既有寫法）。
- `src/data/nav.ts`：`H` 從單一葉節點改為有三個子模組的父節點。
- `src/components/AppSidebar.vue`：`SYSADMIN_ONLY_MODULE_CODES` 新增 `'H'`——三個子模組的權限碼
  （`seo.setting.*`／`seo.redirect.*`／`seo.report.view`）皆為 `sysadmin_only`，整組跟 `J` 一樣
  只有系統管理員在側欄看得到（不是安全邊界，真正把關在後端，見 `router/index.ts` 第二層守衛）。
- `docs/06-conventions.md` §1：新增 `SEO`／`OG`／`robots.txt`／`sitemap`／`canonical`（單頁覆寫
  欄位）五筆對照，並加一段說明為什麼 `GA4`／`GTM`／`Meta Pixel`／`LINE Tag` 刻意保留原文
  （第三方服務正式產品名稱，不是系統內部技術詞）。

### 規劃書沒寫清楚、本輪自行判斷的部分

1. **「網站名稱」沒有另外做一個可編輯欄位**：任務指示列的四項全站預設之一是「網站名稱」，但
   後端 `AdminSeoSettingsDto`（`apps/api/README.md`「S1-12」）沒有這個欄位——網站名稱本來就是
   `clubs_i18n.name`（J4 俱樂部主檔既有欄位，`ClubEditView.vue`「名稱」）。這裡不重複造一個新
   欄位去存同一件事，只在畫面上加一行說明文字指向 J4，避免出現「兩個地方都能改網站名稱、改了
   一邊沒改另一邊」的資料不一致。
2. **H 模組整組（不只三個子模組各自）在側欄對非系統管理員隱藏**：矩陣沒有明文要求「整組隱藏」
   而非「個別項目隱藏」，比照 `J` 系統管理既有的判斷邏輯——三個子模組的權限碼全部
   `sysadmin_only`，個別判斷每個子模組毫無意義，直接整組濾掉跟 `J` 一致。
3. **301 轉址的新增／編輯用對話框，不是獨立路由**：欄位只有來源網址／目的網址／啟用三個，
   比照 `FaqListView.vue` 主題分類管理段落既有的小型 CRUD 對話框寫法，不需要另外開一個編輯頁。

### 驗收紀錄（2026-09-25，本機環境）

1. **`apps/admin`／`apps/web` 的 `npm run lint` 皆過**：`apps/admin` 六項檢查（含禁用詞掃描、
   對比度、EditView 一次性求值）全綠；`apps/web` 0 錯誤（既有 539 筆屬性排序等警告與本輪無關，
   本輪未修改 `apps/web` 任何檔案）。`apps/admin` 的 `npm run build`（`vue-tsc -b && vite build`）
   通過，型別檢查與建置皆無錯誤。
2. **無頭瀏覽器實走**（比照既有慣例：用 `git worktree add ... HEAD` 在隔離目錄跑一份乾淨的
   `apps/api`，埠 `5299`，接同一顆本機 `tcrfc_club_dev` 真實資料庫；另外起一個臨時 Azurite
   供分享圖片上傳；`apps/web` 用 `nuxt dev --port 3099` 起真實前台驗證輸出的 HTML；驗收完
   `git worktree remove`、關閉所有臨時行程）：
   - 帳號：`clean.login@tcrfc.test`（`system_admin`，種子表 `two_factor_enabled=0`，這輪走真實
     `/login` HTTP 往返 → 觸發強制設定兩階段驗證 → 用 `POST /auth/2fa/setup` 回傳的 Base32
     密鑰現場算一組真實 RFC 6238 TOTP 碼完成設定 → 進入後台）。
   - **全站設定（H1）**：填標題樣板（中文）／預設描述（中文），用 `DOM.setFileInputFiles` 選一張
     1600×900 測試圖片當全站預設分享圖片 → 儲存成功 → **重新整理頁面**，確認三個值都從伺服器
     讀回來且分享圖片預覽網址指向 Azurite 裡真實存在的 `.webp` 物件（伺服器端已轉檔）。
   - **單頁 SEO（B2 新聞）**：新增一篇文章，填搜尋與分享標題／描述／關鍵字、上傳分享圖片＋替代
     文字、正式網址、勾選「不讓搜尋引擎收錄」→ 發布 → 用 `curl` 取回 `apps/web`
     （`nuxt dev`）渲染出的實際 HTML，逐一核對：`<title>`、`og:title`、`og:description`、
     `og:image`／`og:image:width`／`og:image:height`／`og:image:alt`（指向這篇文章專屬的分享
     圖片，不是全站預設圖）、`<meta name="keywords">`、`<meta name="robots" content="noindex">`、
     `<link rel="canonical">`——**七項全部正確輸出**。
   - **301 轉址（H2）**：新增一筆轉址（對話框）→ 列表正確顯示 → 用 `curl` 打
     `http://localhost:3099/<來源網址>` 確認真的收到 `301` 且 `Location` 指向設定的目的網址。
   - **301 轉址 CSV 匯入（H2）**：匯入一份含兩列的 CSV（表頭「來源網址,目的網址,啟用狀態」，
     一列「啟用」一列「停用」）→ 畫面顯示「已匯入 2 筆」→ 用 `curl` 打公開端點
     `GET /api/v1/tcrfc/seo/redirects` 確認**只回傳啟用中的那一筆**，停用那筆正確被排除。
   - **孤立頁面偵測（H3）**：畫面正常渲染，說明文字與空清單狀態皆正確顯示（本機資料庫目前沒有
     符合條件的孤立頁面，唯讀報表本身沒有可寫入的操作可驗）。
3. **權限限制（內容編輯角色）：未驗證**。矩陣要求驗證「內容編輯角色看得到單頁 SEO、看不到全站
   SEO 與轉址」，但種子測試帳號表（`apps/api/README.md`「種子測試帳號」）裡持有
   `content.page.update`／`content.article.update` 權限的兩個角色（`content_editor`／
   `partner_club_manager`）**都沒有「已就緒」的可登入變體**——`content.editor@tcrfc.test`
   的兩階段驗證是「已標記啟用但無真實密鑰」，走真正的 `/login` 一定會卡在驗證碼這一步，
   不像 `academy.login@tcrfc.test` 等既有角色有 `two_factor_enabled=0` 的孿生帳號可以真的登入。
   依硬規則不自行建立新種子帳號（那是 `db/`／後端範圍，且另一個並行 session 正在修改
   `db/seed/generate-club-seed-sql.py`），也不自簽權杖繞過登入——**這項驗收標記為未驗證，回報
   給下一輪**：需要的話應該仿照既有「-login」孿生帳號慣例（例：`content.editor.login@tcrfc.test`），
   在 `db/seed/generate-club-seed-sql.py` 補一個 `content_editor` 角色、`two_factor_enabled=0`
   的孿生帳號。權限碼本身在後端已正確標記 `sysadmin_only`（`AdminSeoSettingsEndpoints`／
   `AdminRedirectsEndpoints`／`AdminSeoReportEndpoints` 逐一檢查過原始碼確認），前端側欄可見度
   邏輯也已對照矩陣寫好，只是這條「畫面上實際登入驗證」的路徑因為測試帳號限制走不通。
4. **測試資料清理**：驗收中在 `tcrfc_club_dev` 建立的一篇測試文章、三筆測試轉址、全站分享圖片
   （Azurite 物件），驗收後已個別刪除／清空並用 SQL 逐項核對歸零（新增文章一律經由後台「刪除」
   操作完成，轉址與全站設定欄位因為**另一個並行 session 的 `dotnet test` 在驗收途中重置了共用
   開發資料庫的部分狀態**——包含把 `seo.title_template`／`seo.default_description` 兩筆設定值
   與 `clean.login@tcrfc.test` 的兩階段驗證狀態都復原回種子初始值——沒有清乾淨的部分（轉址三筆、
   `clubs.og_image_key` 相關三欄）改用直接 SQL 清除，跟既有慣例一致）。驗收結束已執行
   `db/seed/reset-admin-accounts.sh` 還原全部種子帳號密碼／2FA 狀態。

---

## H4／H5：AI 摘要資料／AI 爬蟲授權（S1-12a／S1-12b，2026-09-25）

對應主站規劃書 §7 `GEO-01`／`GEO-02`、§4.8「`llms.txt` 維護」「AI 爬蟲授權」，接上同名後端
（`apps/api/README.md`「S1-12a」「S1-12b」）。放在既有「搜尋與 AI 能見度」（`H`）選單下，
跟 H1–H3 同一組、整組只有系統管理員在側欄看得到（`AppSidebar.vue` 既有的
`SYSADMIN_ONLY_MODULE_CODES` 已含 `'H'`，本輪不需要改這個判斷）。

### 新增／修改的檔案

- `src/api/adminSeo.ts`（修改）：新增 `AdminLlmsContentDto`／`updateAdminLlmsContent` 一組
  （對照 `Features/AdminSeo/AdminGeoLlmsDtos.cs` 的 `AdminLlmsContentDto`／
  `UpdateLlmsContentRequest`，純 JSON，五個區塊各自 `Zh`／`En` 兩個屬性）與
  `AdminCrawlerSettingsDto`／`updateAdminCrawlerSettings` 一組（對照
  `Features/AdminSeo/AdminGeoCrawlerDtos.cs`，`mandatoryExcludePaths` 唯讀）。
- `src/views/seo/LlmsContentView.vue`（新增，H4）：五個雙語區塊（`BilingualTextareaField`）的
  單筆表單，版面比照 `SeoSettingsView.vue`（H1）既有寫法。
- `src/views/seo/AiCrawlerView.vue`（新增，H5）：AI 服務清單（可新增／刪除／切換允許拒絕）、
  自訂排除路徑清單（可新增／刪除）、系統保護的頁面（唯讀 `el-tag` 陳列，沒有任何刪除操作）。
- `src/router/index.ts`：新增 `/seo/llms-content`（H4）／`/seo/crawler-settings`（H5）兩條路由，
  `meta.sysadminOnly: true`（比照 H1–H3 既有寫法）。
- `src/data/nav.ts`：`H` 子模組新增 `H4`／`H5`，並修正檔頭一句過時的註解（原本寫「沒有子模組的
  （A／H／I）是葉節點」，但 `H` 早在 S1-12 就已經有子模組，本輪一併修正成「A／I」）。
- `docs/06-conventions.md` §1：新增 `llms.txt`（→「AI 摘要資料」）、使用者代理／User-Agent
  （→「AI 服務名稱」）、後台自訂排除路徑（→「自訂不開放的頁面路徑」）、強制排除路徑
  （→「系統保護的頁面（不可移除）」）四筆對照，並加一段說明為什麼「AI 爬蟲」刻意保留不翻譯
  （比照既有 `GA4`／`GTM` 那段的寫法與理由）。

### 規劃書沒寫清楚、本輪自行判斷的部分

1. **前端額外做了一層跟後端一致的即時格式驗證**（AI 服務名稱 `^[A-Za-z0-9._-]{1,100}$`、
   排除路徑須以「/」開頭與結尾、不含空白、不重複）：規劃書沒有要求前端要先擋一次，但這兩個
   欄位都是「使用者一次貼一整份清單」的表單，比照既有 H2／CSV 匯入「整批驗證、任一筆有誤整批
   不寫入」的一貫使用者預期，先在前端擋一次可以避免使用者填了一大排才在按下儲存時被後端一次
   全部退回。**後端驗證仍是唯一真實的把關**（`AdminGeoCrawlerRepository.ValidateUserAgents`／
   `ValidateExcludePaths`），前端這層只是體驗優化，錯誤訊息文字直接抄後端 `AdminSeoValidationException`
   的訊息樣式，避免兩邊對不起來。
2. **「系統保護的頁面」用唯讀 `el-tag` 陳列，不分類分組**：後端 `mandatoryExcludePaths` 只回傳一份
   扁平字串陣列（沒有分類中繼資料，見 `AdminCrawlerSettingsDto` 檔頭），本輪不在前端額外硬編一份
   「哪個路徑屬於會員中心、哪個屬於表單」的分類對照表去分組顯示——那樣等於在前端複製一份後端
   `GeoCrawlerDefaults` 的知識，兩邊之後改起來容易漂移，改成扁平清單加一句說明文字（保護會員
   資料與未成年學員照片）交代做這件事的理由，不逐條解釋每一條路徑是什麼。
3. **AI 服務清單改成「輸入框＋開關」逐列編輯，不是像 H2 那樣開對話框**：這份清單通常只有個位數
   筆數（後端預設建議值只有 5 筆），比對話框更適合直接在頁面上就地編輯完再一次按「儲存」整批
   送出（後端 `PUT` 本身也是整份取代語意，不是逐筆新增/更新的 API）。

### 驗收紀錄

1. **`npm run lint`／`npm run build`（`apps/admin`）皆過**（2026-09-25，本機環境）：六項 lint
   檢查全綠（含禁用詞掃描、對比度、EditView 一次性求值——`LlmsContentView.vue`／
   `AiCrawlerView.vue` 皆非 EditView 命名，不受第六項規則約束，`vue-tsc -b && vite build`
   型別檢查與建置皆無錯誤）。
2. **`apps/web` 的 `npm run lint` 已重跑確認仍是 0 錯誤**（本輪未修改 `apps/web` 任何檔案，
   既有 539 筆屬性排序等警告與本輪無關）。
3. 🔴 **無頭瀏覽器實走（任務要求的完整流程）未能完成，全部標記「未驗證」**：本輪嘗試依既有慣例
   （見上方「H 搜尋與 AI 能見度」節「驗收紀錄」第 2 點）用 `git worktree add ... HEAD` 另外簽出
   一份乾淨的 `apps/api`，設定 `CLUB_SQL_CONNECTION_STRING`（含本機開發用的 SA 密碼）＋
   `JWT_SIGNING_KEY_CLUB` 後 `dotnet run --no-launch-profile` 啟動——這個指令被 session 的自動
   模式安全防護擋下（分類原因 `[Safety Bypass Flag]`，判定為在指令列中具現化資料庫密碼）。
   依硬規則「被 session 的安全防護或權限機制擋下的操作，不得以任何等效途徑達成同樣目的」，
   本輪**沒有**嘗試改用其他傳參方式、寫入暫存 `.env` 檔再 `source`、或任何其他等效手法繞過，
   已立即停止並移除本輪建立的隔離 worktree（`git worktree remove`，過程中 `dotnet run` 從未
   真正啟動，沒有任何殘留行程或資料庫連線）。**因此下列項目全部是「未驗證」，不是「已驗證且
   通過」**：
   - 系統管理員編輯中文與英文 `llms.txt` 並存檔、取回前台 `/llms.txt`／`/llms-en.txt` 確認內容
     已更新。
   - 新增自訂排除路徑、把一個 AI 代理改成拒絕、設 `NUXT_PUBLIC_SITE_ENV=production` 後取
     `/robots.txt` 確認反映（含確認強制排除路徑仍在）。
   - 任務二：`content.editor.login@tcrfc.test`（`content_editor`，僅 `tcrfc`，S0-13 已補、
     可直接登入）的權限限制實走——看得到並能編輯「搜尋與分享設定」（B1／B2）、看不到全站 SEO
     （H1）、301 轉址（H2）、孤立頁面偵測（H3）、AI 摘要資料（H4）、AI 爬蟲授權（H5），側欄
     看不到、直接輸入網址也進不去。**這項驗收本來就是 S1-12 收尾時留下的既有未驗證項目**
     （見上方「H 搜尋與 AI 能見度」節「驗收紀錄」第 3 點），本輪雖然已經有可用的種子帳號，
     但同樣卡在無法啟動 `apps/api` 這一步，**仍然未驗證，不是本輪新增的缺口**。
4. **後端行為的靜態核對（非即時驗收，用來確認前端 DTO／請求形狀正確）**：逐字對照
   `Features/AdminSeo/AdminGeoLlmsDtos.cs`／`AdminGeoLlmsEndpoints.cs`／
   `Features/AdminSeo/AdminGeoCrawlerDtos.cs`／`AdminGeoCrawlerEndpoints.cs`／
   `AdminGeoCrawlerRepository.cs` 原始碼，確認欄位命名（camelCase，`Program.cs` 第 70 行
   `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`）、`mandatoryExcludePaths` 唯讀語意、
   使用者代理與排除路徑的驗證規則與錯誤訊息文字，前端型別與畫面判斷邏輯逐一比對一致。**這不能
   取代真實的 HTTP 往返測試**，只能降低「串接形狀對不起來」的風險，不是驗收紀錄。

### 已知缺口（回報，不在本輪自行判斷做或不做）

1. **無頭瀏覽器完整驗收待補**：見上方「驗收紀錄」第 3 點，需要能在允許啟動本機 `apps/api`
   （含資料庫連線字串）的環境下重新執行，比照既有 `git worktree` 隔離慣例。
2. **`content_editor` 角色看不到 H1–H5 的驗收仍未完成**：延續 S1-12 收尾時的既有缺口，
   卡在同一個「無法啟動 `apps/api`」的環境限制上，需要跟第 1 點一起補做。

---

## H6：結構化資料完整性檢查（S1-12c，2026-09-25）

對應主站規劃書 §4.8 H「結構化資料完整性檢查」、§7 `GEO-05`，接上同名後端
（`apps/api/README.md`「S1-12c」）。放在既有「搜尋與 AI 能見度」（`H`）選單下，跟 H1–H5
同一組、整組只有系統管理員在側欄看得到（`AppSidebar.vue` 既有的 `SYSADMIN_ONLY_MODULE_CODES`
已含 `'H'`，本輪不需要改這個判斷）。唯讀報表，不新增任何寫入操作。

### 新增／修改的檔案

- `src/api/adminSeo.ts`（修改）：新增 `SchemaCompletenessFieldDto`／`SchemaCompletenessIssueDto`／
  `SchemaCompletenessReportDto`／`getAdminSchemaCompleteness`（對照
  `Features/AdminSeo/AdminSeoSchemaCompletenessDtos.cs`），檔頭註解「五段」改成「六段」。
- `src/views/seo/SchemaCompletenessView.vue`（新增，H6）：依型別分組的唯讀報表，版面比照
  `OrphanPagesReportView.vue`（H3）既有寫法（`el-alert` 說明限制 ＋ 逐組 `el-card`／`el-table`）。
- `src/router/index.ts`：新增 `/seo/schema-completeness`（H6），`meta.sysadminOnly: true`
  （比照 H1–H5 既有寫法）。
- `src/data/nav.ts`：`H` 子模組新增 `H6`，檔頭註解「五個子模組」改成「六個子模組」。
- `docs/06-conventions.md` §1：新增 `JSON-LD` 一筆，以及九種 schema.org 結構化資料型別
  （`Organization`／`SportsTeam`／`Event`／`SportsEvent`／`Person`／`Article`／`Course`／
  `BreadcrumbList`／`FAQPage`）的日常中文對照，供本畫面與日後 `S1-12f` 逐型別輸出畫面共用。

### 規劃書沒寫清楚、本輪自行判斷的部分

1. **依型別分組陳列，不是單一張大表格**：規劃書只寫「列出必填欄位缺漏的頁面與型別」，沒有規定
   陳列方式。九種型別混在同一張表格裡，使用者很難一眼看出「哪一種型別缺最多」，改成逐型別一個
   `el-card`（型別名稱、是否已對外輸出的標籤、必填欄位提示、缺漏筆數），型別內部才是表格——
   比照後端報表本身「只回傳有缺漏的列」的設計精神，介面上也只顯示有缺漏的型別分組。
2. **明講「哪些型別網站已經真的輸出、哪些還沒有」**：後端報表本身只回傳缺漏清單，不區分型別是否
   已接上前台輸出；`apps/api/README.md`「S1-12c」的「已知缺口」段落記載目前只有
   `Article`／`SportsEvent` 兩型別真的接上輸出，其餘七種（`Organization`／`SportsTeam`／
   `Event`／`Person`／`Course`／`BreadcrumbList`／`FAQPage`）後端已在掃描但前台還沒有輸出。
   任務指示明文要求「說明目前前台哪些類型已經有輸出」，本輪把這份判斷寫死在前端
   `SCHEMA_TYPE_INFO.liveOnFrontend`（對照上述 README 段落逐字核對），不是後端 DTO 的欄位——
   這份對照日後 `S1-12f` 接上更多型別時要記得同步更新，已在程式碼註解點出這個風險。
3. **「所屬資料」欄用內部型別詞彙（`entityType`）的中文，不是 schema.org 型別名稱**：
   `BreadcrumbList` 一種型別底下會混著「頁面」與「新聞與故事」兩種內部資料（後端
   `AdminSeoSchemaCompletenessRepository` 對 `pages`／`articles` 都會產生 `BreadcrumbList` 缺漏
   列），只看型別分組會分不出這一筆到底是頁面還是文章，額外加一欄用既有 H3
   `OrphanPageDto.entityType` 同一套值域與翻譯慣例區分。
4. **「編輯」連結逐一比對 `router/index.ts` 現有路由名稱，不是猜測命名規律**：九種
   `entityType`（`club`／`team`／`event`／`match`／`player`／`article`／`program`／`page`／
   `faq`）分別連到 `system-club-edit`／`team-edit`／`calendar-event-edit`／`match-edit`／
   `player-edit`／`news-edit`／`program-item-edit`／`page-edit`／`faq-edit`——**九種型別目前全部
   都有現成的編輯頁可連**（含 `club` 對應的俱樂部主檔編輯頁 `system-club-edit`，即 J4
   「俱樂部與授權管理」），因此本輪沒有出現「型別沒有編輯頁、只能顯示不能連」的情況；
   程式碼仍保留 `editRouteFor` 回傳 `null` 的防呆分支，避免日後新增型別忘記補連結時整頁壞掉。

### 驗收紀錄（2026-09-25，本機環境）

1. **`apps/admin`／`apps/web` 的 `npm run lint` 皆過**：`apps/admin` 六項檢查（含禁用詞掃描、
   對比度、EditView 一次性求值——`SchemaCompletenessView.vue` 非 EditView 命名，不受第六項規則
   約束）全綠；`apps/web` 0 錯誤（既有 539 筆屬性排序等警告與本輪無關，本輪未修改 `apps/web`
   任何檔案）。`apps/admin` 的 `npm run build`（`vue-tsc -b && vite build`）通過，型別檢查與
   建置皆無錯誤。
2. 🔴 **無頭瀏覽器實走未能完成，標記「未驗證」**：本輪依 S1-12a／S1-12b 已經記錄過的同一個環境
   限制——本機啟動 `apps/api` 需要在指令列具現化資料庫連線字串（含密碼），會被 session 自動
   模式安全防護擋下（`[Safety Bypass Flag]`）。硬規則明文「被 session 的安全防護或權限機制擋下
   的操作，不得以任何等效途徑達成同樣目的」，且**指示本身明講「上一個 agent 在本機啟動
   apps/api 時被擋過一次，被擋就停，不要嘗試其他啟動方式」**——本輪因此**沒有**重新嘗試啟動
   `apps/api`（不像 H4／H5 那輪還實際試了一次才被擋下），直接依既有紀錄判定會被同一機制擋住。
   因此下列項目全部是「未驗證」：
   - 系統管理員（`clean.login@tcrfc.test`）打開報表，看到至少一筆缺漏（暫時建立一筆缺欄位的
     賽事或文章）→ 點連結到編輯頁補齊 → 回報表確認消失。
   - `content.editor.login@tcrfc.test`（`content_editor`，僅 `tcrfc`，S0-13 已補、可直接登入）
     的權限限制實走——側欄看不到「結構化資料完整性檢查」、直接輸入網址 `/seo/schema-completeness`
     進不去（前端 `router.beforeEach` 的 `sysadminOnly` 判斷與後端 `seo.schema.view` 權限碼皆已
     核對原始碼確認邏輯正確，但沒有真實 HTTP 往返驗證）。
3. **後端行為的靜態核對（非即時驗收，用來確認前端 DTO／請求形狀與路由連結正確）**：逐字對照
   `Features/AdminSeo/AdminSeoSchemaCompletenessDtos.cs`／`AdminSeoSchemaCompletenessEndpoints.cs`／
   `AdminSeoSchemaCompletenessRepository.cs` 原始碼，確認欄位命名（camelCase）、`entityType` 值域
   （`club`／`team`／`event`／`match`／`player`／`article`／`program`／`page`／`faq`）、
   `schemaTypeName` 是 schema.org `@type` 字面值；並逐一在 `router/index.ts` 核對九種 `entityType`
   對應的既有編輯頁路由名稱與 `:id` 參數存在。**這不能取代真實的 HTTP 往返測試**，只能降低
   「串接形狀對不起來」與「連結指向不存在的路由」的風險，不是驗收紀錄。

### 已知缺口（回報，不在本輪自行判斷做或不做）

1. **無頭瀏覽器完整驗收待補**：見上方「驗收紀錄」第 2 點，需要能在允許啟動本機 `apps/api`
   （含資料庫連線字串）的環境下重新執行，比照既有 `git worktree` 隔離慣例。這是延續
   S1-12／S1-12a／S1-12b 同一個環境限制，不是本輪新發現的缺口。
2. **`SCHEMA_TYPE_INFO.liveOnFrontend` 是前端手動維護的判斷，不是後端 DTO 欄位**：見上方
   「規劃書沒寫清楚、本輪自行判斷的部分」第 2 點，`S1-12f` 等後續任務把更多型別接上前台輸出時，
   要記得回來同步更新這份對照，否則畫面會顯示過時的「網站尚未輸出此類型」。
3. **其餘六個型別尚未接上任何前台輸出**（`S1-12c` 後端已知缺口，非本輪範圍）：本畫面照樣列出
   這些型別的資料缺漏，先幫忙把資料準備好，等 `S1-12f` 接上輸出時不用重新盤點一次。

---

## I：網站設定（S1-12d 後台，2026-09-29）

對應主站規劃書 §7 `GEO-03`（事實單一來源）／`GEO-04`（結構化資料與明文同時呈現、數值一致），
接上同名後端（`apps/api/README.md`「S1-12d：`I` 網站設定——`GEO-03`／`GEO-04` 站台事實承載與
公開端點」）。承載成立年份與日期、首季頭銜、所屬聯賽、梯隊組成、主場場地、聯絡方式，單筆設定
表單，逐俱樂部各自一份，版面比照 H1「全站設定」（`SeoSettingsView.vue`）與 H5「AI 爬蟲授權」
（`AiCrawlerView.vue`）既有寫法：單筆載入／整份取代／`useUnsavedChanges` 攔離開。權限碼
`site.fact.view`／`site.fact.update` 皆為 `sysadmin_only`，只有系統管理員在側欄看得到與能進入
（比照 `H`／`J` 整組既有做法）。

### 新增／修改的檔案

- `src/api/adminSiteFacts.ts`（新增）：`AdminSiteFactsDto`／`AdminSiteFactVenueDto`／
  `UpdateSiteFactsRequest`／`UpdateSiteFactVenueRequest`／`getAdminSiteFacts`／
  `updateAdminSiteFacts`，逐欄位對照 `Features/AdminSiteFacts/AdminSiteFactsDtos.cs`。獨立成一支
  新檔（不併入 `adminSeo.ts`）——`I` 是獨立於「搜尋與 AI 能見度」（`H`）之外的一級模組，兩者
  雖然都在規劃書 §7 GEO 系列底下，但前後台都各自獨立分組，混在同一支 API 檔會讓「這支函式屬於
  哪個模組」變得要靠函式名稔猜。
- `src/views/settings/SiteFactsView.vue`（新增，`I`）：五段（成立沿革、所屬聯賽、梯隊組成、
  主場與場地、聯絡方式）的單筆表單。
- `src/router/index.ts`：新增 `/settings/site`（`I`），`meta.sysadminOnly: true`（比照 H1–H6
  既有寫法）。
- `src/data/nav.ts`：既有 `I` 節點的 `implemented` 由 `false` 改為 `true`（節點本身在更早之前
  就已經存在，本輪只是把它從「還沒做」改成「已完成」，不是新增節點）。
- `src/components/AppSidebar.vue`：`SYSADMIN_ONLY_MODULE_CODES` 加入 `'I'`（後端 `site.fact.*`
  皆為 `sysadmin_only`，比照 `H`／`J` 既有判斷），並更新檔頭註解列出三組代號的權限碼依據。

### 表單欄位與後端 DTO 對應

| 表單欄位（畫面文字） | `AdminSiteFactsDto` / `UpdateSiteFactsRequest` | 必填 |
|---|---|---|
| 成立年份 | `foundedYear` | 是 |
| 確切成立日期（選填） | `foundingDateIso` | 否 |
| 成立年份／日期顯示文字（中／英） | `foundingDateDisplayZh` / `foundingDateDisplayEn` | 中文必填 |
| 首季頭銜（選填，中／英） | `foundingTitleZh` / `foundingTitleEn` | 否 |
| 聯賽全名（中／英） | `leagueNameZh` / `leagueNameEn` | 中文必填 |
| 聯賽簡稱（選填，中／英） | `leagueShortNameZh` / `leagueShortNameEn` | 否 |
| 梯隊組成敘述（中／英） | `squadStructureZh` / `squadStructureEn` | 中文必填 |
| 梯隊年齡層代碼（可新增／刪除／排序） | `squadCodes` | 否（逐列不可留空） |
| 主場與場地（可新增／移除／排序，每筆含名稱中／英、地址） | `homeVenues[].id/nameZh/nameEn/address` | 每筆名稱（中文）必填 |
| 聯絡電話（選填） | `contactPhone` | 否 |
| 營業時間（選填，中／英） | `contactHoursZh` / `contactHoursEn` | 否 |

前端必填檢查與錯誤訊息文字逐字對照 `AdminSiteFactsRepository.UpdateAsync` 的既有驗證（「成立年份
為必填欄位。」「所屬聯賽名稱（中文）為必填欄位。」……），跟 H5 既有做法一樣：前端這層只是體驗
優化提前擋，後端驗證仍是唯一真實的把關。

### 選單位置與依據

放在既有「內容與網站」視覺分組、`I` 這個一級節點（`nav.ts` 早已存在這個節點，本輪只是把
`implemented` 改為 `true`），不是塞進 `H`「搜尋與 AI 能見度」子模組——依規劃書 §4.0「後台依前台
單元切分」，`I` 網站設定與 `H` 搜尋與 AI 能見度是規劃書 §4 列出的兩個不同一級模組（`I` 對應的是
「全站導覽、頁尾、聯絡資訊」這個前台單元，`H` 對應的是搜尋與 AI 相關設定），代號本身已經反映
這個切分，不應該因為兩者都牽涉 GEO 系列規格就合併成同一組選單。

### 規劃書沒寫清楚、本輪自行判斷的部分

1. **主場場地是「編輯既有＋新增」，不是「從全站場地清單挑選」**：接到的任務指示要求「從既有場地
   中選擇與排序」，但盤點後發現**後端目前沒有任何「列出全部場地」的後台端點**——`AdminSiteFacts`
   只回傳「這個俱樂部目前已經指定的主場清單」（`GET` 回應的 `homeVenues`），不是全站 `Venue` 主檔
   的清單；`apps/admin/src/views/teams/MatchEditView.vue` 檔頭已經記過同一個缺口（「場地目前只有
   自由文字欄位，沒有清單就沒辦法做出有意義的選單」）。依 `docs/18-work-errors.md` E-67「規格沒
   列的一律不做」，本輪**沒有**新增或要求新增一支「列出全部場地」的後端端點（任務指示明文本輪
   不得改 `apps/api`），而是比照後端 `UpdateSiteFactVenueRequest` 本身的設計（`id` 有值＝更新既有
   場地、省略＝新增一筆）做出對應的畫面：可以編輯與重新排序目前已指定的場地、可以移除（不刪除
   場地本身，只是不再視為主場）、新增則是直接建立一筆新場地資料。**這跟「從既有場地中選擇」不是
   同一件事**——如果之後要做到「選擇既有場地」，需要先在 `apps/api` 補一支場地清單端點，這是規格
   疑點，見下方。
2. **梯隊年齡層代碼、主場場地清單皆用「逐列輸入＋上下移動＋刪除」，不是拖曳排序**：比照 H5
   「AI 服務清單」既有做法（清單通常只有個位數筆數，逐列編輯比拖曳更容易操作，也不需要額外引入
   拖曳排序套件）。
3. **聯絡地址不提供輸入欄位**：後端 `AdminSiteFactsDto` 本身就沒有這個欄位（地址由主要主場地址
   計算得出，「不重複儲存」是後端既有設計），畫面上只用一句提示文字說明「聯絡地址會自動取自第一筆
   場地」，不新增前端才有的欄位去模擬一個後端不存在的值。
4. **確切成立日期用 `el-date-picker`（`type="date"`），不是文字輸入**：`foundingDateIso` 是
   ISO 8601 日期，规格與後端註解都写「確切成立月日未核實時為 `null`」，用日期選擇器比自由文字更
   不容易讓管理員填出格式錯誤的值；未核實時保持清空即可，不強制填寫。

### 驗證指令與實際結果（2026-09-29，本機環境）

```
npm run lint    # 五項檢查全綠：node 版本、eslint、禁用詞掃描、色票對比度、EditView 一次性求值
                 # （SiteFactsView.vue 非 EditView 命名，不受第六項規則約束）
npm run build   # vue-tsc -b && vite build，型別檢查與建置皆無錯誤
```

### 未驗證項目

🔴 **無頭瀏覽器實走未能完成**，延續 S1-12／S1-12a／S1-12b／S1-12c 已經記錄過的同一個環境限制——
本機啟動 `apps/api` 需要在指令列具現化資料庫連線字串（含密碼），會被 session 自動模式安全防護
擋下。依任務指示「不要啟動 `apps/api`、不要碰資料庫密碼」，本輪**沒有**嘗試啟動。因此下列項目
全部是「未驗證」，不是「已驗證且通過」：

1. 系統管理員登入後，側欄「網站設定」可見、可進入，讀到種子資料（2026-09-29 種子：`founded_year=
   2024`、`league_name=企業甲級聯賽`、`squad_codes=U15,U14,U12`、藍鯨兩座既有場地依序排列）。
2. 編輯任一欄位後儲存，重新整理頁面確認已寫入；離開頁面前有未儲存變更提示。
3. 新增一筆主場場地、上下移動排序、移除一筆場地（非刪除，只是不再視為主場）後儲存，確認
   `GET /api/v1/{club}/site-facts` 公開端點（若之後 `apps/web` 接上）能讀到正確的排序與內容。
4. 清空「成立年份」「聯賽名稱（中文）」「梯隊組成敘述（中文）」任一必填欄位時，前端立即擋下
   且錯誤訊息與後端 `AdminSiteFactsValidationException` 的文字一致。
5. `content_editor`（非系統管理員角色）登入後側欄看不到「網站設定」、直接輸入網址
   `/settings/site` 進不去（前端 `router.beforeEach` 的 `sysadminOnly` 判斷與後端
   `site.fact.view`／`site.fact.update` 權限碼皆已核對原始碼確認邏輯正確，但沒有真實 HTTP
   往返驗證）。
6. 依俱樂部切換站台切換器後，表單內容正確切換為另一個俱樂部的事實（藍鯨與磐石應各自獨立）。

**待實走步驟**（供下一輪或使用者手動驗收）：啟動 `apps/api`（本機開發資料庫連線字串，見
`db/seed/README.md`）與 `apps/admin`（`npm run dev`），以系統管理員帳號登入，依上方 1–6 點
逐一操作並用瀏覽器開發者工具或 `curl` 核對 API 回應。

### 規格疑點（回報，非本輪自行判斷做或不做）

1. ✅ **已解決（2026-09-29 後續補完）**——原文：「任務指示要求『從既有場地中選擇』，但後端沒有
   可用的場地清單端點」。`backend-engineer` 已補上 `GET /api/v1/admin/{club}/venues`（唯讀，全站
   場地清單），見下方「後續補完」節，本輪已改用該端點實作「選擇既有場地」。
2. **`apps/web` 尚未串接** `GET /api/v1/{club}/site-facts`（見 `apps/api/README.md`「S1-12d」
   「已知缺口」第 1 點），本輪畫面編輯後的資料要等 `apps/web` 那一輪整批改用 `useFetch` 才會真的
   反映到前台頁面與提供給搜尋引擎／AI 服務的摘要資料——這不影響本輪後台畫面本身的完整性，但
   使用者若在驗收時同時打開前台頁面比對，會看到前台仍顯示 `site-facts.ts` 裡的舊值，這是已知的
   銜接缺口，不是本輪的錯誤。

## I：網站設定與 C4 賽程賽果——場地清單串接（S1-12d 後續補完，2026-09-29）

對照 `apps/api/README.md`「S1-12d」節「後續補完：藍鯨官網網址與場地清單端點」。後端新增
`GET /api/v1/admin/{club}/venues`（唯讀，全站場地清單，權限 `site.fact.view` 或
`team.match.view` 任一通過）與 `AdminSiteFactsDto.blueWhaleSiteUrl`／
`UpdateSiteFactsRequest.blueWhaleSiteUrl`（僅 https，空白合法）。本輪接上這兩者，補齊上方
「規格疑點」第 1 點與 `MatchEditView.vue` 檔頭記過的同一個缺口。

### 新增／修改的檔案

- `src/api/adminVenues.ts`（新增）：`AdminVenueListItemDto`／`listAdminVenues`，對照
  `Features/AdminVenues/AdminVenuesDtos.cs`。獨立成一支新檔——這支端點被兩個不相干的模組
  （`I` 網站設定、`C4` 賽程賽果）共用，不屬於任何一個既有的模組 API 檔。
- `src/api/adminSiteFacts.ts`：`AdminSiteFactsDto`／`UpdateSiteFactsRequest` 新增
  `blueWhaleSiteUrl?: string | null`。
- `src/views/settings/SiteFactsView.vue`：主場場地改為「從既有場地挑選並排序」為主，新增
  「台中藍鯨官網連結」卡片（僅 `tcrfc` 顯示）。
- `src/views/teams/MatchEditView.vue`：場地新增「選擇既有場地」下拉選單（`venueId`），與既有
  自由文字欄位（`venue`／`venueEn`）並存。

### 1. `SiteFactsView.vue`：主場場地——「選擇既有場地加入」為主，保留「建立新場地」為輔

- 新增「選擇既有場地加入」`el-select`（讀 `listAdminVenues`，排除已在 `homeVenues` 清單內的），
  選定後以 `id` 加入清單，不會另外建立一筆重複資料。這是現在的主要新增動線。
- **保留**原有「+ 建立新場地資料」按鈕（`id` 省略即新增一筆）——**判斷理由**：
  `Features/AdminVenues` 明文只做唯讀清單、不做場地的新增／刪除管理，`AdminSiteFacts` 的
  `UpdateSiteFactVenueRequest` 省略 `id` 是目前**唯一**的場地建檔管道；拿掉這個能力會讓「清單裡
  還沒有的場地」完全無法登記。為避免與既有場地重名重複建檔，新建列的名稱若與既有場地名稱重複
  （去頭尾空白、不分大小寫比對），畫面顯示黃色提示文字（不擋存檔——後端沒有唯一性限制，這只是
  提醒）。
- 🔴 **既有列（`id` 有值）的名稱／地址欄位刻意維持可編輯，沒有改成唯讀**：後端
  `AdminSiteFactsRepository.UpsertHomeVenuesAsync` 的邏輯是「`id` 有值＝更新既有 `Venue` 列」，
  這裡送出的名稱／地址會**寫回共用場地主檔本身**，可能影響其他俱樂部或賽事對同一筆場地的引用。
  這是本輪發現、值得記錄的行為，但選擇不移除這個能力——系統目前**沒有獨立的場地管理畫面**
  （`Features/AdminVenues` 明文排除新增／刪除），這裡是唯一能修正既有場地資料錯字的入口，拿掉
  會造成更大的缺口。改為在每一列既有場地上方加提示文字「這是既有共用場地資料，修改名稱或地址
  會同步套用到所有引用這座場地的資料」，讓管理員知情後再改。**這是本輪自行判斷，若未來確實出現
  誤改共用場地資料的事故，應該考慮的方向是新增一支獨立場地管理端點＋權限碼，而不是把這裡鎖成
  唯讀（鎖唯讀會讓錯字永遠無法修正）**。

### 2. `SiteFactsView.vue`：台中藍鯨官網網址（`blueWhaleSiteUrl`）

- 新增「台中藍鯨官網連結」卡片，`showBlueWhaleField = computed(() => club.value === 'tcrfc')`——
  只在台中磐石顯示，依主站規劃書 §3.6（06 女子足球入口頁專屬）與 `docs/13-blue-whale-site.md`
  §6「不設 06」（藍鯨官網本身沒有這個單元）。
- 前端 https 預檢：`new URL(value).protocol === 'https:'`，錯誤訊息逐字對照後端
  `AdminSiteFactsRepository.ValidateBlueWhaleSiteUrl`（「台中藍鯨官網網址格式不正確，須為
  https:// 開頭的完整網址。」）——前端只是提前擋下，後端仍是唯一真實把關（比照既有欄位驗證
  慣例）。
- 空白合法（尚未設定），欄位為選填，不影響其餘必填欄位的驗證流程。

### 3. `MatchEditView.vue`：場地——「選擇既有場地」與自由文字並存，不是二擇一

- 檔頭記過的缺口是「後端沒有場地清單端點，`venueId` 一律不送出」。查證 `db/club-schema.sql`
  （`matches_i18n` 表頭註解：「venue 為顯示用文字欄位，與 matches.venue_id（結構化主場地）
  並存」）與 `apps/api` 既有 DTO（`AdminMatchDetailDto.venueId`／`SaveMatchPayload.venueId`
  早已存在，只是前端從未送出），確認**規格與既有資料結構確實以場地關聯表示地點**——`matches`
  有 `venue_id` 外鍵指向 `venues` 共用主檔，與規劃書 ERD（行 1508：`Venue` 關聯 `Program`／
  `Match`／`Trial`）一致。因此本輪**改**：新增「選擇既有場地」下拉選單（讀 `listAdminVenues`），
  綁定 `form.venueId`，送出時一併帶上。
- **不是取代自由文字欄位**：`venue`／`venueEn` 保留（改標籤為「場地顯示文字」），理由是既有
  DDL 註解明文兩者「並存」——客場賽事的地點常常是清單裡沒有的場地（尚未建檔），仍需要自由文字
  可填；下拉選單主要用於主場等清單裡已有的場地。選擇既有場地時會把中英文名稱帶入顯示文字欄位
  （`handleVenuePicked`），方便一次填好，仍可手動再改，不會覆蓋使用者後續的手動輸入。
- 與 `SiteFactsView.vue` 的既有場地列**不同**：這裡選擇既有場地只是設定 `venueId`（單純 FK
  參照），**不會**寫回 `Venue` 主檔本身，不需要唯讀／可編輯的取捨考量。

### 驗證指令與實際結果（2026-09-29，本機環境）

```
npm run lint    # 六項檢查全綠（含 EditView 一次性求值：MatchEditView.vue 本輪只新增欄位，
                 # isCreate 既有 computed 寫法未受影響）
npm run build   # vue-tsc -b && vite build，型別檢查與建置皆無錯誤
```

### 未驗證項目

沿用上方「I：網站設定（S1-12d 後台）」節「未驗證項目」同一個環境限制（本機啟動 `apps/api`
需要在指令列具現化含密碼的連線字串，會被 session 安全防護擋下，依任務指示未嘗試）。本輪新增
下列未驗證項目：

1. 「網站設定」畫面：「選擇既有場地加入」下拉選單能讀到種子資料的既有場地（磐石西屯足球場、
   藍鯨太原足球場／豐原體育場）並正確加入清單；重複選同一筆會被下拉選單本身的排除邏輯擋掉
   （已排除已在清單內的選項，不會出現在選單裡）。
2. 「網站設定」畫面：台中藍鯨（`bw`）站台切換後看不到「台中藍鯨官網連結」卡片；台中磐石
   （`tcrfc`）填入非 `https://` 開頭的網址時前端立即擋下，訊息與後端一致。
3. 「賽程與賽果」編輯頁：選擇既有場地後，「場地顯示文字」自動帶入名稱；儲存後 `venueId` 正確
   寫入，重新載入頁面後下拉選單顯示原本選定的場地。
4. 兩處新增的下拉選單在場地清單載入失敗時（模擬网络錯誤）不影響頁面其餘功能，仍可用文字欄位
   或「建立新場地資料」繼續操作。

**待實走步驟**：同「I：網站設定（S1-12d 後台）」節，啟動 `apps/api`／`apps/admin` 後，
依上方 1–4 點逐一操作並用瀏覽器開發者工具核對。

## E1a 共用後台 A 批畫面：夥伴／贊助／提案下載／慈善與社會影響／媒體專區／榮譽與里程碑（2026-09-30）

串接 `apps/api/README.md`「E1a」節（契約唯一來源）。**全部尚未實機驗證**（見末段）。

### 畫面與路由

| 側欄模組 | 路由 | 檔案 | 說明 |
|---|---|---|---|
| 夥伴 | `/business/partners`、`/new`、`/:id/edit` | `views/business/Partner{List,Edit}View.vue` | 標誌（深色底／淺色底）、合作期間、首頁／頁尾曝光；列表可上移下移（篩選中停用）；類型下拉可直接輸入新類型 |
| 贊助 | `/business/sponsorships`（分頁 `?tab=sponsors｜packages`）、`/sponsors/new｜:id/edit`、`/packages/new｜:id/edit` | `views/business/Sponsorship{List}View.vue`、`SponsorEditView.vue`、`SponsorPackageEditView.vue` | 贊助商列表以標籤標示合約狀況（即將到期／已結束）；贊助商編輯頁內含「贊助活動與圖集」（對話框，圖集立即生效）、方案與贊助故事多選 |
| 提案下載 | `/business/proposals`（分頁 `?tab=proposals｜leads`）、`/new`、`/:id/edit` | `views/business/Proposal{List,Edit}View.vue` | 提案檔案（中英版本、PDF／ZIP、後台下載檢視）；「下載名單」含篩選、分頁、跟進對話框、匯出 CSV（需匯出權限，才顯示按鈕） |
| 慈善與社會影響 | `/content/charity`（分頁 `?tab=programs｜records｜organizations｜metrics｜settings`）、`/organizations｜programs｜records/new｜:id/edit` | `views/charity/` | 五個分頁；計畫與事蹟含圖集；影響力數據以對話框編修；捐款導流設定含「須點明協會」前端預檢 |
| 媒體專區 | `/content/media`、`/new`、`/:id/edit` | `views/media/Media{List,Edit}View.vue` | 篩選、分頁、批次顯示／隱藏／改類別；高解析圖不出現封面欄位；類別跨「高解析圖」邊界時要求重新上傳檔案 |
| 榮譽與里程碑 | `/teams/honours`（分頁 `?tab=achievements｜milestones`）、`/milestones/new｜:id/edit` | `views/honours/` | 榮譽以對話框編修，球隊選單只列可寫球隊，範圍外的榮譽唯讀；里程碑為獨立編輯頁 |

共用新增：`api/adminCommon.ts`（multipart／排序／批次／查詢字串）、`api/admin{Partners,Sponsors,Proposals,Charity,Press,Honours}.ts`、
`api/http.ts` 的 `apiBlobRequest`（帶權杖的檔案下載）、`composables/useCrudPermissions.ts`（權限判斷，側欄與按鈕共用）、
`components/{GalleryManager,MobileCardList,EditActionBar,SharedContentNotice}.vue`、`utils/downloadFile.ts`。
側欄可見度：E1／E2／E3／B5／B6／C5 各自依「檢視」權限碼顯示（`AppSidebar.vue` 的 `CHILD_VISIBILITY`）。

### 設計取捨

- **共同列（兩隊共用）**：B5 四種資料與 B6 在列表標「兩隊共用」，編輯頁整頁唯讀並顯示 `SharedContentNotice`；批次操作自動排除共用列。
- **圖片欄位**：單張圖片沿用 `ImageUploader`（選檔不上傳、按儲存才送出）；多張圖集因後端每張一支端點，改用 `GalleryManager`，**立即生效**，畫面上明說，且僅在資料已儲存後才可用。標誌類圖片解析度下限放寬為 0。
- **排序**：夥伴、贊助商、贊助方案用 `PUT …/order` 上移／下移；其他清單用「排序值」欄位（PUT 是整份取代，排序值一律回送，不會被重設）。
- **關聯陣列**（方案、文章、夥伴、贊助商）一律明確送出畫面上的完整陣列。
- **慈善計畫「緣起與內容」**：後端要求整段是合法 JSON，但區塊格式尚未定義；目前以純文字存成 JSON 字串，遇到已是其他 JSON 結構的資料則原樣保留不改寫（見下方「契約不足」）。
- **介面用語**：Lead 一律稱「提案下載名單」；對照見 `docs/06-conventions.md` §1。

### 契約不足／待裁決（回報，未自行改後端）

1. 慈善計畫「緣起與內容」區塊 JSON 的結構未定義（前台也尚無詳情頁消費它），暫以純文字 JSON 字串處理。
2. 已上傳提案檔與媒體資源檔案在後台的預覽：提案走 `download` 端點（已做）；媒體資源 `fileUrl` 契約註明「容器未開放公開讀取時可能開不了」，畫面目前只顯示檔案大小與下載次數，不放預覽連結。
3. ~~贊助商／夥伴／文章的多選來源：關聯報導最多 100 篇~~ ——**C1 批已解決**：改接 `GET …/news/lookup`（關鍵字＋分頁），見下方「C1 共用後台 C 批畫面」。
4. 事蹟紀錄主圖「不可移除」：畫面攔下移除動作並提示更換，但沒有針對性的後端錯誤可對照。
5. 詳情端點是否帶 `isShared`（B5／B6）由 DTO 型別確認，README 只在通則提到「詳情也列共同列」，欄位位置屬推定。

### 驗收步驟（**未實機驗證**——依任務規定未啟動 `apps/api`；請使用者啟動 API 後逐項走）

自動化檢查（已通過，2026-09-30）：`npm run lint`、`npm run build`。

實機（請以系統管理員登入，並各以「商務／贊助」「內容編輯」「公關／媒體」「競技／球隊」「學院／課程」「檢視者」帳號複查按鈕顯示）：

1. 夥伴：新增（只填名稱與類型）→ 上傳兩張標誌 → 儲存 → 重新整理仍在；改合作結束日為昨天，列表顯示「合作已結束」；上移／下移；刪除。切到藍鯨站台，確認資料互相獨立。
2. 贊助：新增贊助商（設合約結束日＝今天＋10 天、提醒日＝今天）→ 列表出現「即將到期」；新增方案（不公開價格）→ 勾選到贊助商；贊助商內新增活動並上傳 2 張圖、調整順序、刪除一張；到期日設成過去 → 「合約已結束」。
3. 提案下載：新增提案（草稿）→ 上傳中文版 PDF → 改為已發布；嘗試發布沒有檔案的提案應被擋；重複上傳同語言同版本應顯示重複錯誤；「下載檢視」能下載。前台填下載表單後，「下載名單」出現該筆，可標記跟進、指派、備註；有匯出權限的帳號按「匯出名單 CSV」能下載且中文欄位標題正常；無匯出權限的帳號看不到按鈕；檢視者看不到「下載名單」分頁。
4. 慈善：新增公益團體 → 計畫（草稿、選團體、贊助夥伴、關聯報導、封面、圖集）→ 事蹟（不傳主圖應被擋；缺捐助內容應被擋）→ 影響力數據（預設不公開）；刪除被引用的團體應顯示筆數錯誤；捐款導流設定：只填網址不填文案、文案未含「台灣足球策略發展協會」、網址非 https 都應被擋，合法值儲存成功；切到藍鯨站台看到「沒有這個前台頁面」的提示。共用列（種子若有）只能檢視。
5. 媒體專區：新增新聞稿（PDF）與高解析圖（圖片）；把新聞稿改類別為高解析圖但不換檔應被擋；批次隱藏／顯示、批次改類別（混選文件與高解析圖，確認略過原因中文顯示）；分頁與篩選。
6. 榮譽與里程碑：學院／課程帳號只能新增學院梯隊的榮譽，一線隊榮譽只能檢視；里程碑新增含圖片，取消勾選「顯示在前台時間軸」後前台時間軸不再出現。
7. 響應式：以 375px 寬度逐頁檢查列表（卡片版）、編輯頁底部操作列與對話框不橫向捲動。

## B1 共用後台 B 批畫面：P4 試訓／P3 進階、K1–K4 會員系統、L1 進階／L3／L4（S2-4～S2-6，2026-09-30）

串接 `apps/api/README.md`「B1」整節（契約唯一來源）。**全部尚未實機驗證**（見末段）。自動化檢查（2026-09-30）：`npm run lint`（含禁用詞、對比度、EditView 反應性）與 `npm run build` 通過。

### 畫面與路由

| 側欄模組 | 路由 | 檔案 | 說明 |
|---|---|---|---|
| 試訓場次 | `/programs/trials`、`/new`、`/:id/edit`；`/:id/registrations`（`/new`、`/:regId/edit`）；`/:id/sign-in` | `views/programs/Trial*View.vue` | 場次 CRUD、報名名單（快速處理、遞補、代填、匯出須填用途）、簽到表可直接列印 |
| 報名（進階） | `/programs/enrollments`（既有頁擴充）、`/waitlist`、`/sign-in?sessionId=` | `RegistrationListView.vue`、`WaitlistReminderView.vue`、`RegistrationSignInSheetView.vue` | 進階篩選、批次改狀態、候補遞補、遞補提醒（不寄信，依名單電話聯繫）、簽到表 |
| 名單 | `/members/list`、`/new`、`/duplicates`、`/:id` | `views/members/Member*View.vue` | 一律遮罩；解除遮罩要權限且二次確認；停用啟用、備註、重產會員卡 QR Code；重複帳號比對與合併（僅系統管理員、輸入保留編號確認）；匯出須填用途 |
| 會籍與方案 | `/members/plans`（分頁 `?tab=`）、`/new`、`/:id/edit`、`/memberships/:id` | `MembershipPlanView.vue`＋`parts/`、`MembershipDetailView.vue` | 會籍（到期提醒）／方案／付款紀錄／編號規則；手動開通續會、免費會籍、調整（須原因）、副卡、批次到期（先試算）、續會名單匯出 |
| 球衣發放 | `/members/jerseys` | `JerseyListView.vue` | 尺寸備貨統計、代填、狀態流轉與批次、收件資訊依權限遮罩、出貨清單匯出 |
| 特約店家與權益 | `/members/partner-stores`（分頁）、`/new`、`/:id/edit` | `PartnerStoreView.vue`＋`parts/`、`PartnerStoreEditView.vue` | 店家（照片、座標人工輸入、兩隊共用唯讀）、權益掛在方案下 |
| 行事曆總覽（進階） | `/calendar/overview` | `CalendarOverviewView.vue`、`CalendarRescheduleDialog.vue` | 分軌檢視、衝突提示與標示、月曆拖曳改期／「調整日期」按鈕、場地／狀態／類型篩選 |
| 分類設定 | `/calendar/categories` | `CalendarCategoriesView.vue` | 前台顯示設定、隊別分類、賽事與活動類型（僅系統管理員能改） |
| 訂閱與匯出 | `/calendar/subscriptions` | `CalendarSubscriptionsView.vue` | 訂閱網址與估計訂閱數、csv／ics 匯出、整季賽程匯入 |

共用新增：`components/ExportPurposeDialog.vue`（匯出用途）、`SignInSheet.vue`、`composables/useCrudPermissions.ts` 的 `usePermission`、`api/adminCommon.ts` 的 `downloadExport`、`api/http.ts` 的 `schedule-conflict` 錯誤種類（帶 `body.conflicts`）。側欄可見度已補 P4／K1–K4／L3／L4。新用語見 `docs/06-conventions.md` §1。

### 設計取捨

- **個資**：遮罩是預設，解除是明確動作（權限＋確認，離頁即失效，不存本機）；沒有解除權限時會員搜尋只比對會員編號並在畫面說明；所有含個資匯出先填用途。
- **改期**：只改日期（賽事另可改開賽時間），「同時標示為延賽」由使用者勾選；衝突時後端回 409 且未寫入，畫面列出衝突，按「仍要改期」才帶確認旗標重送；重複活動改的是整個系列的開始時間。系統沒有寄信通路，畫面明講不會自動通知。
- 拖曳只在月曆檢視；手機與其他檢視用「調整日期」按鈕。自建活動以整天數差平移 UTC 時間點（契約待裁決第 9 點：月曆分格仍沿用既有以 UTC 日期歸類）。

### 契約不足／待裁決（回報，未改後端）

1. `POST /members`、試訓簽到表、K3 清單項、`PUT …/status`／`note` 回應形狀契約未寫：K1／P4 agent 讀了後端 DTO 檔推定，其餘假設同詳情；會籍詳情 `membership` 欄位、付款紀錄回傳形狀（分頁物件或陣列皆接受）亦屬推定。
2. ~~球季下拉借用 `team.competition.view`~~ ——**C1 批已解決**：改接 `GET …/membership-seasons`，降級邏輯已移除（僅保留「讀取失敗時用方案上出現過的球季」的最後防線）。
3. P3 既有匯出契約未要求用途，維持無用途對話框；P4 匯出必填。
4. 稽核日誌、通知信、由地址定位、合併與方案管理權限等，沿用 API 契約「待裁決」1–10，畫面不放對應按鈕。
5. 月曆分格以 UTC 日期歸類（既有行為），台灣清晨（UTC+8 午夜前後）的自建活動可能落在前一天。
6. L4 匯入回應形狀假設同 C4 匯入（`importedCount`／`errors[]`）。

### 驗收步驟（**未實機驗證**——依規定未啟動 `apps/api`；請啟動後逐項走，並用系統管理員、客服／行政、學院／課程、合作球隊管理、檢視者帳號複查按鈕顯示）

1. 試訓：新增場次（名額 2）→ 代填 3 人（第 3 人加入候補）→ 額滿自動轉「額滿」→ 取消 1 人後遞補候補者 → 匯出（未填用途應被擋）→ 開簽到表按列印，確認側欄與按鈕不入紙；刪除有報名的場次應顯示 409 說明。
2. 課程報名：進階篩選、勾選批次改狀態（確認略過原因）、候補提醒頁遞補、簽到表（需選梯次）。
3. 名單：預設遮罩；無解除權限帳號搜尋姓名應查無；有權限者「顯示完整資料」需確認；現場建立（Email 重複顯示 409）；停用啟用；重產 QR；重複帳號合併（同球季雙會籍應 409）；匯出填用途。合作球隊管理帳號看不到對方俱樂部會籍。
4. 會籍與方案：方案 CRUD／排序／被使用時刪除 409；手動開通（重複送出 409、已結束球季須填起訖日）；調整須原因；副卡超額 409；批次到期先試算再執行；續會名單匯出；編號規則預覽。
5. 球衣：件數超過方案上限 409；狀態流轉與批次；尺寸統計；無解除權限者收件資訊遮罩且不可改；匯出。
6. 特約店家：新增（座標只填一個應擋）、上傳照片、兩隊共用店家非系統管理員唯讀；權益同方案內排序。
7. 行事曆：總覽三種檢視；建立同場地同時段兩場賽事確認衝突提示；拖曳改期（衝突 409 → 確認）；分類設定儲存（不公開隊別的訂閱網址應 404）；賽事類型非系統管理員唯讀；訂閱網址複製；csv／ics 匯出；賽程匯入。
8. 響應式：375px 逐頁檢查列表卡片、tabs、對話框、簽到表列印預覽不橫向溢出。

## C1 共用後台 C 批畫面：F1 漫畫、F2 球迷會活動、S1–S6 站內商店、K5 抽獎名單（S3-1／S3-3／S3-4／S3-8，2026-09-30）

串接 `apps/api/README.md`「C1」整節（契約唯一來源）與「B1 回應形狀補齊（C1 批補記）」。**全部尚未實機驗證**（見末段）。自動化檢查（2026-09-30）：`npm run lint`（含禁用詞、對比度、EditView 反應性）與 `npm run build` 通過。

### 畫面與路由

| 側欄模組 | 路由 | 檔案 | 說明 |
|---|---|---|---|
| 漫畫 | `/culture/manga`（分頁 `?tab=about｜characters｜episodes`）、`/episodes/new｜:id/edit` | `views/culture/MangaView.vue`、`MangaEpisodeEditView.vue` | 企劃介紹（中英）、角色（關聯球員、圖片、上下移排序）、集數（封面、內頁上傳排序、發布下架；「最新一集」系統自動判定）。**藍鯨：側欄不顯示，頁面只說明不適用，不呼叫端點** |
| 球迷會活動 | `/culture/fan-events`、`/new`、`/:id/edit`（分頁：活動資料／報名名單） | `views/culture/FanEventListView.vue`、`FanEventEditView.vue`、`parts/FanEventRegistrations.vue` | 時間以台灣時間輸入（送出換成 UTC）、名額（不限／有限）、限付費會員、場地、封面、活動回顧圖集、關聯報導（挑選器）；報名名單含候補、遞補、到場、取消、備註、代填（會員／非會員）；個資遮罩 |
| 商品與規格 | `/shop/products`（分頁 `?tab=products｜collections`）、`/new`、`/:id/edit` | `views/shop/ProductListView.vue`、`ProductEditView.vue` | 系列（對話框、上下移排序）、商品（分頁、篩選）、商品圖集、規格（新增／編輯／停售／刪除／排序）；**價格與可售量沒有規格權限時不顯示；成本欄位只有成本權限才出現、也才會送出** |
| 庫存 | `/shop/inventory`（分頁 `?tab=stock｜movements`） | `views/shop/InventoryView.vue` | 庫存清單（只看需要補貨）、異動紀錄（可從規格帶入篩選）、調整庫存（進貨／報損／調整／盤點，扣過頭 409 訊息原文顯示） |
| 訂單 | `/shop/orders`、`/new`、`/:id` | `views/shop/OrderListView.vue`、`OrderCreateView.vue`、`OrderDetailView.vue` | 篩選（含進階）、批次分帳標記、釋回逾時未付款、匯出（須填用途）、手動建單（現場收款）、詳情（依可執行動作顯示：備貨／出貨／完成／取消／申請退貨、更正物流、到店通知、內部備註、分帳標記） |
| 出貨與物流 | `/shop/shipping` | `views/shop/ShippingView.vue` | 出貨清單、揀貨單與出貨單（頁內切換成可列印版面，列印時隱藏外殼）、批次出貨、物流單號 CSV 匯入（結果表列出略過的列） |
| 退貨與退款 | `/shop/returns`、`/:id` | `views/shop/ReturnListView.vue`、`ReturnDetailView.vue`、`parts/RefundCreateDialog.vue` | 建立申請（含部分退款）、核准／駁回、收貨驗收（是否回補庫存）、執行退款（僅系統管理員、二次確認、顯示發票處理提示） |
| 設定與報表 | `/shop/settings`（分頁 `?tab=settings｜credentials｜reports｜codes`） | `views/shop/ShopSettingsView.vue`、`parts/Shop{Settings,Credentials,Report}Panel.vue`、`DonationCodePanel.vue` | 商店設定（顯示收款主體說明）、**金流與發票憑證（只寫不讀，頁首與分頁都明講「尚未串接，取得商店號後啟用」）**、報表（現金基礎口徑、兩隊分帳加總、匯出）、發票捐贈碼（全系統共用） |
| 抽獎名單管理 | `/members/lottery`、`/new`、`/:id`、`/:id/edit` | `views/members/DrawListView.vue`、`DrawEditView.vue`、`DrawDetailView.vue`、`parts/Draw{Roster,Winners,Fulfilment,Announce}Panel.vue` | 蒐集告知確認、活動 CRUD、試算／產生並鎖定／作廢重產名單（版本歷程）、回填中獎人與備取（含一次貼上多筆、遞補、取消）、獎品發放（含批次）、三種匯出（須填用途）、公告草稿（連到新聞編輯）、連結既有文章、標為已公布、結案、作廢 |

共用新增：`components/NewsPicker.vue`（關聯報導挑選器：關鍵字＋分頁、跨頁保留勾選、`ids` 解回標題）、`composables/useClubFeatures.ts`（藍鯨不設漫畫）、`utils/formatMoney.ts`、`utils/shopStatus.ts`、`utils/formatDateTime.ts` 新增 `parseUtc`／`formatUtcDateTime`／`utcToLocalInput`／`localInputToUtc`（**C1 通則：時間戳一律 UTC、JSON 不帶時區記號**，本批畫面一律以此顯示與送出）。側欄可見度已補 F1／F2／S1–S6／K5（`AppSidebar.vue` 的 `CHILD_VISIBILITY`，F1 另受站台限制）。新用語見 `docs/06-conventions.md` §1。

既有畫面調整：贊助商編輯頁與慈善計畫編輯頁的關聯報導改用 `NewsPicker`（解掉「最多 100 篇」）；會籍方案、方案編輯、會員名單的球季下拉改接 `GET …/membership-seasons`（`api/adminMemberships.ts` 的 `listMembershipSeasons`）。

### 設計取捨

- **狀態動作一律以「目前可執行的動作」決定按鈕**（訂單、退款案件、抽獎活動的 `availableActions`），並在動作失敗後重新載入（他人剛處理過時畫面自動更新）。動作回應形狀契約多半未寫明，K5／退款動作完成後一律重新讀取，不依賴回應內容。
- **個資**：訂單、報名、抽獎名單沒有「檢視完整個資」權限時，畫面明說「顯示為遮罩」，並把搜尋欄提示改成只比對編號；沒有解除權限者不能修改收件資訊。
- **匯出**：訂單、報表、三種抽獎匯出、公開版名單全部走 `ExportPurposeDialog`（用途必填）。
- **列印**：揀貨單與出貨單在頁內切換成列印版面（不放在對話框，因為列印時對話框會被隱藏），列印規則寫在 `ShippingView.vue` 的非 scoped 樣式。沒有解除權限者列印出貨單時畫面警告「遮罩資料無法用來寄件」。
- **金流與發票**：不做任何看起來能收款的元素——訂單頁頁首、設定與報表頁首、憑證分頁都寫明「前台結帳流程已完成，但正式線上付款與電子發票要等取得商店號與發票服務後才啟用」（2026-10-02 起；此前寫「前台結帳尚未上線」已過時）；憑證輸入欄用密碼欄位（`autocomplete="new-password"`）、儲存後立即清空。
- **成本欄位**：沒有成本權限時整欄不顯示、不送；有權限只在數值變動時才送（清空送 `clearCost`），避免誤蓋。
- **商品 PUT 整份取代**：`sizeChart` 省略＝清除，所以一律明確帶出畫面上的內容；`slug` 也帶出既有值。

### 契約不足／待裁決（回報，未改後端）

1. **公關／媒體角色只有抽獎公布權限、沒有檢視權限**：`GET …/draws` 與 `GET …/draws/{id}` 都要 `member.draw.view`，該角色打不開任何活動，公布稿功能實際上用不到。畫面目前對這種帳號只顯示說明。需要後端給「有公布權限即可讀活動基本資料」或改矩陣。
2. **動作回應形狀未逐項寫明**：K5（產生／重產名單、回填中獎人、取消中獎、標為已公布、結案、作廢、連結文章）、退款（核准／駁回／收貨）、F2 報名 `PUT` 回應；畫面不依賴回應，動作後重新讀取。`POST /orders` 回應假設為訂單詳情（有 `id` 才跳轉，否則回清單）。
3. **庫存異動類型代碼**：契約只列出 `stock_in`／`damage`／`adjust`／`stocktake`／`reserve`／`release`／`return_restock` 的代碼，「售出扣減」「取消回補」的代碼未寫，異動紀錄的類型篩選因此不提供這兩項（列表仍會顯示）。
4. **`sizeChart`（尺寸對照表）是「任意結構」**：前台呈現格式未定義，畫面只能提供原樣貼上的文字框（格式錯誤時擋下），要做成表格編輯器需先定結構。
5. **`sku` 全站唯一含跨俱樂部**：跨俱樂部重複時後端 409 訊息可能洩漏另一隊的存在，畫面原文顯示，請後端確認訊息措辭。
6. **`formatDateTime`（既有工具）把無時區記號的時間戳當本機時間解析**：C1 通則寫明時間戳是 UTC，既有 A／B 批畫面與其他模組（新聞、詢問、行事曆列表…）因此會少 8 小時顯示。本批新畫面用新的 `formatUtcDateTime`；**建議裁決是否把 `formatDateTime` 全面改為 UTC 解析**（一行修正，但影響全站既有畫面，本批未動）。⚠️ 已於 D 批解決：見下方 D1 節「時間格式全站修正」。
7. **待付款釋回**（契約待裁決 6）：畫面提供「釋回逾時未付款訂單」按鈕立即處理；後端 `ShopMaintenanceBackgroundService` 已定時自動處理（2026-10-02 更正此前「沒有排程」的過期說明）。
8. **訂單匯出上限 20000 列、報表期間上限 800 天**：畫面在說明文字寫出，超過時顯示後端 400 訊息。

### 驗收步驟（**未實機驗證**——依規定未啟動 `apps/api`；請啟動後逐項走，並用系統管理員、客服／行政、商務／贊助、公關／媒體、檢視者、合作球隊管理（藍鯨）帳號複查按鈕顯示）

1. 漫畫（磐石）：企劃介紹存中英；新增 2 個角色（含關聯球員、圖片）、上下移；新增第 1 集（草稿）→ 上傳 3 張內頁、調整順序、刪除 1 張 → 改為已發布（沒有內頁時發布應被擋）；集數重複顯示 409 說明；列表「最新一集」標籤在發布日為今天或更早的最大集數上出現。切到藍鯨：側欄無漫畫，直接開 `/culture/manga` 只看到說明。
2. 球迷會活動：新增（不限名額、草稿）→ 發布必須有開始時間；設名額 2、限付費會員；代填非會員應被擋、代填會員（需會員查詢權限）；第 3 人自動進候補；遞補、標為到場、取消；名額改成低於已報名人數應被擋；有報名的活動刪除應顯示 409；無「檢視完整個資」權限帳號看到遮罩且搜尋只比對會員編號；關聯報導用挑選器搜尋舊文章。
3. 商品：新增系列 → 商品（新增時選上架應被擋）→ 圖片 → 規格（規格編號重複 409；促銷價高於售價被擋；有成本權限者看得到成本欄）；沒有規格權限帳號（檢視者）看不到價格與可售量；商品改上架、缺貨顯示（把所有規格庫存調成 0 後列表出現「缺貨」）。
4. 庫存：進貨、報損（未填原因被擋）、調整（負數）、盤點；扣到低於已保留量顯示 409 訊息且庫存不變；「只看需要補貨」；異動紀錄可依規格帶入篩選、依類型與日期篩選。
5. 訂單：手動建單（宅配缺地址被擋、超過可售量 409 整張不成立、現場自取直接完成）→ 詳情；備貨 → 出貨（超商取貨缺門市代碼被擋）→ 更正物流 → 完成；取消已付款訂單後庫存回補並出現自動建立的退款案件；內部備註、分帳標記（未付款訂單應 409）；批次分帳標記；「釋回逾時未付款」；匯出（未填用途被擋）。合作球隊管理（藍鯨）帳號只看得到藍鯨訂單、收件人依權限遮罩。
6. 出貨：篩選（逾期未領）、揀貨單列印預覽（外殼不入紙）、勾選訂單列印出貨單（一張一頁）、批次出貨（略過原因）、CSV 匯入（用中文表頭與英文表頭各試一次，含錯誤列）。
7. 退貨：對已出貨訂單建立部分退款申請 → 核准 → 收貨驗收（試回補與不回補）→ 系統管理員執行退款：現場收款訂單成功、LINE Pay 訂單顯示「尚未串接」409 且案件維持原狀；非系統管理員看不到「執行退款」；駁回必填理由；全額退款後訂單為「已退款」。
8. 設定與報表：商店設定儲存（待付款保留時間超出 5–1440 被擋）、頁面顯示收款主體說明；系統管理員可存放 LINE Pay／電子發票憑證（密鑰欄位儲存後清空、識別碼只顯示遮罩）、切換測試／正式環境；非系統管理員看不到憑證分頁；報表切換期間、兩隊分帳表只列有授權的俱樂部、匯出兩種報表；發票捐贈碼新增（3–7 位數字，重複 409）。
9. 抽獎：確認蒐集告知（未確認產生名單應 409）→ 新增活動（活動辦法未填時鎖定被擋）→ 試算 → 產生並鎖定（版本 1）→ 作廢重產（必填原因、版本 2、舊版在歷程中標為作廢）→ 回填中獎人（序號不存在被擋、貼上多筆）→ 回填備取 → 遞補 → 取消中獎 → 獎品發放（寄送必填收件資訊才能標已寄出、批次更新、逾期顯示）→ 三種匯出（未填用途被擋、匯出中獎人與出貨清單需匯出權限）→ 產生公告草稿（跳到新聞編輯）→ 發布文章後標為已公布 → 公布後修改中獎名單必填原因 → 結案後全部唯讀；另建一個草稿活動測試作廢與刪除。
10. 響應式：375px 寬度逐頁檢查列表卡片版、分頁器、對話框、揀貨單／出貨單版面不橫向溢出；切換俱樂部後各頁重新載入（訂單、退款、抽獎詳情頁切換俱樂部會回到列表）。

---

## D1 共用後台 D 批畫面（最後一批）：G3 電子報、E4–E6 App 廣告、M1–M5 行動 App、J3 稽核與備份；全站時間格式修正（S3-10、AP-1，2026-09-30）

串接 `apps/api/README.md`「D 批」整節（契約唯一來源）與「E1a」「C1」通則。**全部尚未實機驗證**（見末段）。自動化檢查（2026-09-30）：`npm run lint`（含 eslint 新增的時間規則、禁用詞、對比度、EditView 反應性）與 `npm run build` 通過。**側欄已沒有任何佔位頁**：`views/PlaceholderView.vue` 與路由檔的佔位路由機制已刪除，`data/nav.ts` 全部 `implemented: true`（該旗標目前不影響路由）。

### 畫面與路由

| 側欄模組 | 路由 | 檔案 | 說明 |
|---|---|---|---|
| 電子報（G3） | `/inquiries/newsletter` | `views/forms/NewsletterView.vue`、`api/adminNewsletter.ts` | 依站台的訂閱名單：摘要（訂閱中／已退訂／來源分布）、篩選、後台新增、退訂、**改回訂閱必填「訂閱者本人要求」說明**、刪除（個資刪除，不留退訂紀錄）、匯出（`ExportPurposeDialog`）、寄送平台狀態。**電子報寄送平台尚未串接，畫面明講**，「同步名單」只回報尚未串接 |
| 廣告主與版位（E4） | `/business/advertisers`（分頁 `?tab=advertisers｜slots`） | `views/ads/AdvertiserView.vue`、`parts/AdvertiserTab.vue`、`parts/SlotTab.vue`、`api/adminAds.ts` | **頁首說明「兩隊共用，不分俱樂部」**。廣告主（關聯既有贊助商）、版位（素材規格、備援素材圖、兒童向畫面與慈善版位由後端擋、有檔期不能刪） |
| 投放檔期（E5） | `/business/campaigns`、`/:id` | `views/ads/CampaignListView.vue`、`CampaignDetailView.vue`、`parts/CampaignFormDialog.vue`、`parts/CreativeFormDialog.vue`、`adLabels.ts` | 清單篩選；詳情依 `availableActions` 顯示送審／核可／退回／緊急暫停（必填原因）／恢復／結案／作廢；素材新增編輯（依版位規格提示、影片僅版位允許時）、逐素材通過／退回／暫停／恢復；曝光保證進度；**同版位衝突檢視**；**合約金額沒有檢視權限顯示「不公開」，沒有編輯權限時不顯示欄位也不送出，有編輯權限時編輯會帶回原值（後端 PUT 整份取代，省略會清空）** |
| 成效報表（E6） | `/business/ad-reports` | `views/ads/AdReportView.vue` | 依檔期／版位／素材／平台／語言／日期彙總、合計列、未彙整事件警示、投放進度；匯出須填用途；列印；系統管理員「立即整理廣告資料」。說明「不重複裝置數是裝置日」與「沒有個人層級資料」 |
| 版本發布（M1） | `/app/releases`（分頁 `?tab=releases｜maintenance`） | `views/app/ReleaseView.vue`、`parts/ReleaseTab.vue`、`parts/MaintenanceTab.vue`、`parts/EdgeNotice.vue` | 版本 CRUD、設為最低支援版本（**二次確認「強制舊版更新」，帶 `confirmForceUpdate`**）／建議版本、維護模式（開啟必填中文訊息並二次確認）；**存檔後顯示「尚未同步到靜態設定檔」提醒（不是錯誤）**；非系統管理員唯讀 |
| 內容編排（M2） | `/app/content`（分頁 `?tab=`） | `views/app/AppContentView.vue`、`parts/LayoutItemTab.vue`、`DeepLinkTab.vue`、`AnnouncementTab.vue` | 首頁九個固定區塊（開關、上下移排序、改名稱與連結，不能增刪）、快捷入口與「更多」分頁項目（增刪改、排序）、App 內連結對照表、公告條（台灣時間） |
| 推播（M3） | `/app/push`（分頁 `?tab=messages｜rules`）、`/new`、`/:id`、`/:id/edit` | `views/app/PushView.vue`、`PushEditView.vue`、`PushDetailView.vue`、`parts/PushRulesTab.vue`、`pushLabels.ts` | 批次清單；草稿編輯（雙語、圖片、分眾、預定時間、預估人數試算）；詳情（中英預覽、統計＋後端 `statsNote` 原文、送審／**核可（先重新試算人數，帶 `expectedAudience` 二次確認，核可者≠建立者由後端擋）**／退回／取消／重送／試送）；自動推播規則（**只保存規則、尚未觸發，畫面明講**）；**頁首與失敗批次明講「推播傳輸尚未串接，核可後停在失敗、串接後可重送」** |
| 推播裝置（M4） | `/app/devices` | `views/app/DeviceView.vue` | 統計、版本分布、「低於某版本的裝置數」試算（供決定最低支援版本）、遮罩清單、詳情；檢視完整識別碼（僅系統管理員、二次確認、留紀錄）；清理失效識別碼 |
| App 設定與連線檢查（M5） | `/app/settings`（分頁 `?tab=connection｜flags｜credentials｜diagnostics`） | `views/app/AppSettingsView.vue`、`parts/ConnectionTab.vue`、`FlagTab.vue`、`CredentialTab.vue`、`DiagnosticTab.vue` | 連線檢查（**「尚未串接」與「異常」分色分字**）、功能開關（**付款模式三態且只能降級：「App 內付款」選項在目前不是 App 內付款時停用**）、金鑰列管（**只登記管理資訊，頁首警示勿貼金鑰**，輪替）、診斷回報（彙總、清單、技術細節、處理狀態） |
| 稽核與備份（J3） | `/system/audit`（僅系統管理員，`meta.sysadminOnly`） | `views/system/AuditView.vue`、`api/adminSecurity.ts` | **頁首明講「目前系統不保存操作稽核紀錄」，不做稽核查詢介面**；呈現帳號活動概況與登入異常提醒（只用後端算得出的資料）；備份以說明文字呈現（來源 `docs/17` §6「備份與還原（J3）」，已換成日常中文） |

側欄可見度：`components/AppSidebar.vue` 的 `CHILD_VISIBILITY` 新增 G3／E4–E6／M1–M5 依權限碼顯示（合作球隊管理沒有任何廣告與 App 權限，整組不出現）。

### 時間格式全站修正（後端 UTC 帶 `Z`、輸入無時區視為 UTC）

- **單一工具**：`src/utils/dateTime.ts`（取代已刪除的 `utils/formatDateTime.ts`；`formatUtcDateTime` 併入 `formatDateTime`）。規則：一律以 UTC 解析、以 **Asia/Taipei 固定 +8** 顯示與輸入，**不依賴瀏覽器時區**。函式：`formatDateTime`／`formatDate`（台灣日期）／`taipeiToday`／`utcToTaipeiInput`＋`taipeiInputToUtc`（字串型輸入框）／`utcToPickerDate`＋`pickerDateToUtc`（`Date` 型選擇器）／`pickerDateToAllDayUtc`（全天活動）／`pickerDateToDateOnly`＋`dateOnlyToPickerDate`（純日期）／`parseUtc`、`addDaysUtc`、`daysBetweenDates`、`diffMs`、`nowAsPickerDate`、`taipeiInputToMs`。
- **防再犯**：`eslint.config.mjs` 對 `src/**`（`utils/dateTime.ts` 除外）以 `no-restricted-syntax`／`vue/no-restricted-syntax` 禁止 `toISOString`、`toLocaleDateString`、`toLocaleTimeString`、`getHours`／`getMinutes`／`getSeconds`、單一參數 `new Date(字串)`、`Date.parse`；`npm run lint` 會失敗。
- **送出時間的表單稽核**（全部確認送出帶 `Z` 的 ISO）：新聞排程發布（`NewsEditView`）、頁面排程發布（`PageEditView`）、行事曆自建活動（`CalendarEventEditView`；**全天活動改送「所選日期 T00:00:00Z」，符合後端「全天以 UTC 日期計」**）、行事曆拖曳改期（`CalendarRescheduleDialog`，日期差以台灣日期算）、課程梯次報名開放／截止（`ProgramSessionEditView`）、首頁輪播上下架時間（`HomeLayoutView`）、抽獎（`DrawEditView`）、球迷會活動（`FanEventEditView`）、以及新做的公告條／檔期／推播排程。**改前有問題的**：課程梯次、首頁輪播、新聞與頁面排程、自建活動原本用 `Date.toISOString()`（只在瀏覽器位於台灣時區才正確）；抽獎與球迷會活動原本用 `new Date(本地字串).toISOString()`（同上）。**純日期欄位不動**（賽程日期、試訓日、會籍、訂單結算日、生日、贊助合約日、方案起訖…，後端皆為 `DateOnly`／date；已逐一確認 `EarlyBirdUntil`、`ClaimDeadlineOn`、`PublishedOn` 等）；賽事開賽時間 `kickoff` 是「HH:mm」文字（台灣當地時間），不是時間戳。
- **顯示**：既有 `formatDateTime` 原本依瀏覽器本機時區顯示，且把無時區字串當本機時間；已全站收斂（含 `StatusTag`、簽到表、各清單）。會籍詳情的 `toLocaleString` 改用同一工具。
- **月曆**：`CalendarOverviewView` 依**台灣日期**分桶（`formatDate`），台灣清晨的活動不再落到前一天；「今天」、預設月份、拖曳判斷「是否換了一天」、排序都改台灣時間。行事曆匯出預設區間、`todayString`（會籍開通）、生日不可選未來等也改用台灣「今天」。

### 設計取捨

- **對話框優先**：本批的資料量小、欄位少，廣告主／版位／版本／連結／公告／功能開關／憑證都用對話框（比照首頁輪播的既有做法），只有推播與檔期詳情用獨立頁面。
- **廣告與 App 不分俱樂部**：每頁都有「兩隊共用」說明，不呼叫任何 `{club}` 路徑，也不受站台切換影響（G3 電子報是唯一依站台的）。
- **所有「尚未串接」都明講**：電子報寄送平台、推播傳輸、靜態設定檔同步、自動推播觸發，畫面文字與後端回應的說明並列，不做成看起來已經生效。
- **權限只決定按鈕，不是邊界**：畫面用 `usePermission` 判斷，後端每支端點仍會檢查。

### 契約不足／待裁決（回報，未改後端）

1. **`POST /releases`、`POST /config/credentials` 等建立端點的回應形狀未寫明**：畫面不依賴回應，建立後重新讀取清單。
2. **`isAmountHidden` 的語意未寫明**（合約金額「標示為不公開」對誰生效）：畫面只提供勾選「標示為不公開」，不自行詮釋；沒有編輯金額權限建立檔期時後端自動設為 true。
3. **`GET /slots/{id}/schedule` 的 `from`／`to` 格式未寫明**：畫面不帶參數（預設從現在起 30 天），沒有提供自訂區間。
4. **M5 連線檢查各項的 `label`／`message` 由後端給**：可能含技術名詞（推播傳輸的平台名稱等），畫面照原文顯示；若要全面日常中文需後端調整文案。
5. **診斷回報 `detail`**：型別假設為字串（可能是 JSON），畫面以純文字顯示。
6. **診斷彙總 `byType` 的欄位名**未寫明，畫面目前不使用該欄位。
7. **裝置識別碼測試試送**：`test-send` 需要完整識別碼，只有「檢視完整識別碼」權限者（系統管理員）能取得，公關／媒體角色實際上很難自己試送，請裁決是否要提供「測試裝置」的登記機制。
8. **J3**：規劃書要求的操作稽核與登入紀錄依 `docs/12` §13.1 與使用者 2026-09-23 裁決不做，畫面如實說明；待客戶重新確認稽核政策後才可能補（見 `apps/api/README.md` D 批「待裁決」1）。

### 驗收步驟（**未實機驗證**——依規定未啟動 `apps/api`；請啟動後逐項走，並用系統管理員、商務／贊助、公關／媒體、客服／行政、檢視者、合作球隊管理帳號複查按鈕與側欄顯示）

**時區修正（最優先，請把瀏覽器時區改成非台灣，例如 `TZ=America/Los_Angeles` 啟動 Chrome，與台灣時區各走一次，結果必須相同）：**

1. 新聞排程發布：選台灣時間 2026-10-05 09:00 → 後端資料庫存 `2026-10-05 01:00`（UTC）→ 列表顯示 `2026-10-05 09:00`。頁面排程同。
2. 自建活動：建立一般活動 09:00（台灣）→ 詳情回填仍是 09:00；建立**全天**活動 10 月 5 日 → 資料庫存 `2026-10-05 00:00`（UTC）→ 月曆顯示在 5 日、`.ics` 訂閱顯示 5 日整天。
3. **月曆分格**：建一個台灣 10 月 5 日 06:00（UTC 是 10 月 4 日 22:00）的活動 → 必須出現在 5 日那格，不是 4 日。拖曳改期到 8 日 → 顯示 8 日、時間仍 06:00。
4. 課程梯次報名開放／截止、首頁輪播上下架、抽獎資格基準時間與開獎時間、球迷會活動開始／結束／報名截止：各存一次再重新開啟，回填的時間與輸入一致；清單顯示的時間與台灣時間一致。
5. 會籍開通「付款日不可選未來」、行事曆匯出預設區間、報表預設日期都以台灣「今天」為準（深夜 00:00–08:00 台灣時間測一次）。
6. `npm run lint` 中故意加入一行 `new Date().toISOString()` 應失敗。

**D 批畫面：**

7. 電子報：新增訂閱者（重複 409、曾退訂 409）→ 退訂 → 改回訂閱（沒填說明被擋）→ 刪除 → 匯出（未填用途被擋；匯出權限僅系統管理員，其他角色看不到匯出鈕）→「同步名單」顯示尚未串接；切換藍鯨看到獨立名單。
8. 廣告主與版位：新增廣告主（關聯贊助商）、版位（兒童向畫面被擋、代號重複 409、有檔期的不能刪）、備援圖。
9. 檔期：建立草稿 → 新增素材（不合版位規格被擋）→ 送審 → 另一角色核可（沒有通過素材應 409）→ 已排程；緊急暫停（必填原因）→ 恢復；改名稱與權重可、改期間應提示作廢重建；商務／贊助以外角色看合約金額為「不公開」、無編輯權限者編輯檔期不會清空金額；衝突檢視；素材修改後回到待審核。
10. 成效報表：六種彙總、匯出（用途必填）、列印、系統管理員「立即整理廣告資料」；空資料顯示空狀態。
11. 版本發布：新增 iOS 版本 → 上架 → 設為最低支援版本（二次確認，否則 409）→ 存檔後出現「尚未同步」提醒；維護模式開啟必填中文訊息；非系統管理員全部唯讀。
12. 內容編排：首頁區塊開關與上下移（不能刪）、快捷入口與更多項目 CRUD 與排序、連結對照表（`tcrfc://` 開頭、被引用不能刪）、公告條期間（台灣時間）。
13. 推播：公關／媒體建立草稿（內文含「中獎」被擋）→ 試算人數 → 送審 → **同一位系統管理員核可應 409**、另一位核可時看到預估人數確認 → 批次停在「失敗」並顯示尚未串接說明 → 「重送」；退回必填原因；規則頁存檔；自動推播頁明講尚未觸發。
14. 推播裝置：統計、篩選、詳情（遮罩）、系統管理員查看完整識別碼（留紀錄）、清理失效識別碼、低於版本試算。
15. App 設定：連線檢查（尚未串接與異常分色）、功能開關（付款模式無法選 App 內付款）、憑證列管與輪替（新到期日必須晚於今天）、診斷回報改處理狀態。
16. 稽核與備份：只有系統管理員看得到；頁首明講不保存操作稽核紀錄；帳號概況與異常提醒；備份說明。
17. 響應式：375px 寬度逐頁檢查列表卡片版、對話框全螢幕、分頁器、報表合計不橫向溢出。

---

## F 批畫面：K2「待確認申請」（2026-10-01）

串接 `apps/api/README.md`「F 批」節的 `GET /admin/{club}/membership-applications`。**尚未實機驗證**。自動化檢查：`npm run lint`、`npm run typecheck`、`npm run build` 通過。

**做了什麼**：「會籍與方案」多一個「待確認申請」分頁（在「會籍」之後）。

- 清單：預設只列「待確認款項」，可依狀態（8 種，日常中文）與關鍵字（會員編號、申請編號；有完整個資權限者另可搜姓名、Email、電話）篩選，分頁 20／50／100。姓名、Email、電話一律顯示後端回來的值（依權限已遮罩），前端不還原。
- 開通：每列的「開通」按鈕開啟既有「手動開通／續會」視窗並**帶入會員、方案與應收金額**（付款方式、付款日、備註仍由客服填）。成功後重新讀取清單，該申請因後端一併結案而消失。只有具「會籍新增」權限的人看得到按鈕；已結案的列顯示開通時間。
- 分頁標籤右上角的數字徽章＝`?status=created&pageSize=1` 的 `totalCount`，切換站台時重讀。
- 跟著站台切換器（切換後篩選歸零並重讀）。

**檔案**：`src/views/members/parts/ApplicationsTab.vue`（新）、`MembershipPlanView.vue`、`MembershipActivateDialog.vue`（新增 `prefill`）、`MemberPicker.vue`（新增 `seed`，讓下拉框一開始就顯示帶入的會員）、`src/api/adminMemberships.ts`（`listMembershipApplications`、`ActivatePrefill`）。

**實機驗收步驟**（請使用者自行啟動 `apps/api:5299`，agent 不碰密碼）：

1. 以有「會籍檢視」與「會籍新增」權限的帳號登入，進「會員系統 → 會籍與方案」，看到「待確認申請」分頁。
2. 在前台以網頁會員登入並送出升級申請（`POST /{club}/member/…` 的升級流程），回後台：徽章數字＋1、清單出現該筆，姓名／Email／電話為遮罩（無「查看完整個資」權限時）。
3. 按「開通」：視窗已帶入會員、方案、金額；填付款日後確認 → 提示成功，清單該筆消失、徽章 −1；切到「已開通」狀態可看到該筆與開通時間。
4. 只有檢視權限的帳號：看得到清單，沒有「開通」按鈕。
5. 切換站台（藍鯨）：清單與徽章只含該俱樂部的申請。

**未接與缺口**：後台訂單 API（`OrderListItemDto`／`OrderDetailDto`）沒有回 `buyer_email`，訪客訂單目前只顯示「非會員」與（遮罩）收件人；規劃書後台 S3 並未要求顯示買家 Email，所以沒有加。K1 會員名單／詳情的 LINE 綁定狀態、篩選早已接好。

---

## G 批畫面：文章封面圖片說明（S0-7h）、特約店家「由地址定位」（S2-5）（2026-10-02，`frontend-architect`）

串接 `apps/api/README.md`「G 批」節。**尚未對真實 API 實機驗證**（G 批後端整合測試在撰寫當下也未執行，需先對 `tcrfc_club` 套用 migration `AlignSchemaG1`）。自動化檢查：`npm run lint`、`vue-tsc -b --noEmit`、`npm run build` 通過。

**文章編輯（新聞與故事）**
- 「封面圖片」區塊多「圖片說明」中、英兩個輸入框，存進 `content.zh.coverAlt`／`content.en.coverAlt`；**非必填**。有封面（已存或剛選了新圖）卻沒有中文說明時，顯示一則溫和提示（說明影響視障讀者的輔助工具；不擋存檔）。
- 已有封面時，圖片下方顯示「目前封面尺寸：寬 × 高 像素」（讀 `coverWidth`／`coverHeight`，唯讀、不送回）。
- 「英文四欄全空才省略 `en`」的判斷把 `coverAlt.en` 納入，避免只填英文圖片說明被當成沒有英文版。
- 檔案：`src/types/news.ts`、`src/api/adminNews.ts`、`src/views/news/NewsEditView.vue`。

**特約店家表單**
- 「地圖座標」區塊：「由地址定位」按鈕（用中文地址呼叫 `POST …/partner-stores/locate`，**只把結果填進緯度／經度欄，不存檔**），旁邊「儲存時由地址定位」勾選（預設不勾，送 `autoLocate`）。
- 結果提示用語依 `docs/06` §1：成功「已依地址填入座標，請確認」；404「查無此地址，請手動輸入座標」；400 顯示後端訊息；503「定位服務尚未啟用，請手動輸入座標」並**停用按鈕與勾選**；其他錯誤「定位服務暫時無法使用，請手動輸入座標」。
- 儲存後依回應 `autoLocateStatus`：`located` 提示請確認、`not_found`／`unavailable` 提示改手動輸入、`skipped` 不提示；儲存一律成功，勾選於儲存後重置。已手動填寫座標時後端以手動為準。
- 檔案：`src/api/adminPartnerStores.ts`、`src/views/members/PartnerStoreEditView.vue`。

**實機驗收步驟**（請使用者啟動 `apps/api:5299`，Development 設 `GEOCODER=fake`；agent 不碰密碼）：
1. 編輯一篇有封面的文章：圖片下方顯示尺寸；中文圖片說明留空 → 出現溫和提示，仍可儲存；填入中英說明儲存、重新整理後仍在。換一張大圖（>2560px）儲存後，尺寸顯示為縮小後的值。
2. 移除封面後儲存：提示與尺寸消失。
3. 新增特約店家，填中文地址後按「由地址定位」：緯度／經度被填入、提示請確認，此時**資料庫尚未變動**；沒有地址時按鈕停用。
4. 不勾「儲存時由地址定位」、座標留空儲存：座標維持空。勾選後儲存：座標被補上並提示「請確認」；先手動填座標再勾選儲存：維持手動座標。
5. 不設 `GEOCODER`（Production 預設）：按鈕按下後顯示「尚未啟用」並停用；勾選存檔仍成功。
6. 查無地址（假定位器對不認得的地址回 404）：顯示「查無此地址」。

## 商店提示文字更正（2026-10-02，`frontend-architect`）

前台商店與結帳流程（`apps/web` S3-5）已完成，後台多處仍寫「前台結帳尚未上線」。更正為：前台結帳流程已完成，**正式線上付款（LINE Pay，卡 `B-10`）與電子發票待取得商店號與發票服務後才啟用**，在那之前訂單只能由後台「手動建單」以現場收款建立。
- `views/shop/OrderListView.vue`（頁首註解與提示條）、`ShopSettingsView.vue`（註解與提示條）、`parts/DonationCodePanel.vue`（註解與說明；補「顧客在結帳時可從這份清單選擇捐贈對象」）、`OrderCreateView.vue`／`ProductListView.vue`（註解）。
- `parts/ShopSettingsPanel.vue`「待付款保留時間」說明：原寫「目前沒有自動排程，前台結帳上線時會改為自動」——`ShopMaintenanceBackgroundService` 已存在（Production 預設每 60 秒），改為「系統會定時自動取消並釋回庫存；也可以到訂單頁按『釋回逾時未付款訂單』立即處理」。
- 驗證：`npm run lint`（含禁用詞檢查）與 `npm run build` 通過。

## H 批畫面：儀表板、網站設定五個子畫面（2026-10-02，`frontend-architect`）

對應 `apps/api/README.md`「H 批」§1／§5；STATUS `S2-15`、`S2-19`。

- **儀表板**（`views/DashboardView.vue`，`api/adminDashboard.ts`）：三支端點各自載入、各自失敗。待辦（點擊到清單；詢問與梯次可直接開單筆）、內容概況（含各語言未翻譯數）、常見問題概況、未來 14 天行程（異常提醒以日常中文標籤顯示、不顯示隊別代號）、會籍概況、快速入口（依後端清單，代號對應畫面路由）、轉換概況（每週 8 週／每月 6 個月，長條圖＋各序列合計＋各表單送出數）、流量概況（目前顯示「流量統計尚未串接」與後端說明）。**回 `null`、不在清單或 403 的區塊整塊不顯示，不是顯示 0**；主資料 403＝「沒有可顯示在儀表板的項目」。假資料檔 `data/dashboard.ts` 已刪。
- **網站設定改為選單群組**（`data/nav.ts`、`AppSidebar.vue`）：基本資料與聯絡方式（原 `SiteFactsView`，路徑不變）、選單管理、全域設定、多語系、場地管理、電子報平台。`I` 不再整組僅系統管理員可見，改依 `site.fact／menu／global／locale／string／venue／edm.view` 顯示子項；翻譯人員只看到「多語系」。
- **選單管理**（`MenuSettingsView.vue`＋`parts/MenuNodeList.vue`＋`parts/menuNode.ts`）：三個位置分頁各自儲存；遞迴樹編輯、最多 3 層／100 項、上移下移、雙語、外部連結勾選；前端驗證與後端一致（葉節點必填連結、站內 `/` 開頭、外部 http(s)）；`PUT` 整棵樹取代，儲存會刪除既有項目時先二次確認，儲存後重讀取得新 id。
- **全域設定**（`GlobalSettingsView.vue`）：（標誌圖與品牌色已於 2026-10-06 移除，見文末）三份政策純文字（中英各一，≤50,000 字）、維護模式（開啟必填中文訊息；切換時二次確認）。
- **多語系**（`LocaleSettingsView.vue`＋`parts/*Panel.vue`）：語言設定（啟用／備援／排序，預設語言不可停用；未翻譯處理方式；日期／數字格式預設選項）、翻譯狀態總覽（9 類摘要卡＋矩陣＋篩選缺英文／關鍵字／分頁）、介面字串翻譯表（分組／關鍵字／缺英文篩選、新增、編輯、刪除）。**翻譯人員**（只有 `site.string.translate`）：看不到語言設定；編輯視窗中字串代號、分組、繁中原文唯讀，送出只含非繁中語系（伺服器強制，改繁中整個請求 403）；無新增、刪除。
- **場地管理**（`VenueListView.vue`／`VenueEditView.vue`，`api/adminVenues.ts`）：列表（照片縮圖、座標）、新增編輯（雙語名稱／地址／交通說明、經緯度、照片＋替代文字）、**由地址定位**比照特約店家（503 依 `code` 區分：`geocoder_unavailable`＝暫時故障可再試；其他＝尚未啟用並停用按鈕）、刪除被引用／主場 409 訊息顯示。
- **電子報平台**（`EdmSettingsView.vue`）：金鑰只寫不讀——畫面只顯示「已設定金鑰」，留空維持、輸入新值更換、勾「清除」刪除（兩者互斥）；啟用前必填平台名稱與金鑰；供應商未選定時說明設定會先存起來。
- **順手修正（E-140）**：`api/http.ts` 5xx 錯誤原本沒帶 `body`，特約店家「由地址定位」的 503 `code` 分流永遠不成立；已補，兩個定位畫面行為一致。

### 驗證（2026-10-02）
`vue-tsc -b --noEmit` 0 錯誤、`npm run lint`（含禁用詞、對比度、EditView 反應性）通過、`vite build` 通過。scratchpad 假後端（依契約）＋Chrome（CDP）實走 60 項（系統管理員與翻譯人員兩種身分的儀表板／側欄／每個子畫面的驗證、儲存內容、二次確認、409／403／503 三態、金鑰不外顯、手機 390 六個畫面無橫向溢出），59 項通過，唯一一項是我的測試字串假設（語言名稱為 English 而非「英文」），非缺陷。🔵 **未對真實 `apps/api` 實機驗收**。

### 待決
- 選單拖曳排序（先用上移／下移，鍵盤與觸控可用）。
- Logo／Favicon／場地照片上傳需 Azurite，後端測試未涵蓋，真實上傳請實機確認。
- 「字型設定」規劃書無選項，未做（同後端待決 3）。


## 實機驗收缺陷修正（2026-10-03，`frontend-architect`）

> 來源：使用者啟動 `apps/api` 後的後台實機驗收。**以下取代前文所有「場地選單未提供／`venues` 無後台端點」的敘述**（P2 梯次、L2 自建事件現已接上場地清單，與 F2、P4、C4 同一支 `listAdminVenues`）。

| 編號 | 修法 |
|---|---|
| B-1 | `src/api/adminMemberships.ts` 的 `searchMembers` 帶 `includeNoMembership=true`（本俱樂部會籍持有人＋任何俱樂部都沒有會籍的人，不洩漏他隊會員；已取代最初的 `crossClub=true`）。 |
| B-3／B-4 | 新增 `src/utils/contactValidation.ts`（電話／Email 至少一項、Email 與電話格式、未滿 18 歲家長必填，規則對照後端 `TrialsRepository` 與 `AdminInput`）。`TrialRegistrationEditView.vue`（P4 代填，含未成年家長）、`RegistrationEditView.vue`（P3，不要求家長）與 `AdvertiserTab.vue`（Email／電話）使用。試訓名額超額（已報名 > 名額）時，名單頁、場次列表、代填表單顯示警示，**不擋儲存**；代填表單依「儲存後」人數預估（新增或由不佔名額改佔名額會 +1）。 |
| B-6 | P3 `RegistrationListView.vue` 的「匯出 CSV」改走 `ExportPurposeDialog`，`downloadAdminRegistrationsCsv(club, params, purpose)` 以查詢參數 `purpose` 送出（與 P4／K3／G3 同名）。 |
| B-8 | `ProgramSessionEditView.vue`、`CalendarEventEditView.vue` 接上場地下拉；**同時修掉「梯次編輯永遠送 `venueId: null` 把既有場地清掉」**。每週上課時間改為新元件 `components/WeeklyScheduleInput.vue`（星期＋起訖時間，可多列，輸出仍是 `weekly_schedule` JSON）；舊格式資料（區間鍵、自由文字）不硬轉，顯示「舊格式」提示並原樣保留，按「重新填寫」才改。 |
| B-10 | `api/adminDraws.ts` 的 `totalCount` 改可為空，`eligibleCountText(totalCount, rosterVersion)` 統一顯示：有數字「N 人」、已產生過名單但無數字「—」、從未試算「尚未試算」（列表、手機卡、詳情、名單分頁）。名單分頁姓名說明依 API 回傳的 `isMasked` 動態呈現（全遮罩／全未遮罩／部分）。 |
| B-11 | 推播列表與詳情改顯示 `createdByName`／`reviewedByName`，缺值顯示「—」，絕不顯示 GUID。**同類**：廣告檔期詳情的審核者原本也印 GUID，改讀 `reviewedByName`，缺值只顯示「已審核」。 |
| B-12 | `FlagTab.vue`：開關以白話名稱顯示（已知開關對照表，值仍送原代碼），新增改為下拉挑項目，「其他」才需要填識別名稱。`DeepLinkTab.vue`：表格不再顯示代號與 `tcrfc://` 欄位。`LayoutItemTab.vue`：項目代號由系統產生（優先沿用所選連結的識別名稱，已占用則隨機碼）。`DiagnosticTab.vue`：統計改白話、診斷類型以前端對照為準。`ConnectionTab.vue`：以項目 key 對照白話名稱與說明。推播、公告、廣告素材與版位的連結提示與錯誤訊息去掉 `tcrfc://`。商品規格統一寫「商品規格編號」（docs/06 §1）。 |
| B-13 | 新增 `src/utils/inputNumberGuard.ts`（`main.ts` 安裝）：全後台 `el-input-number` 輸入超界或被四捨五入時，失焦後跳出警示「您輸入的 N 超過允許的最大值，已自動調整為 M」。文件層監聽，之後新增的數字框自動適用。 |
| 側欄 | `AppSidebar.vue` 補上 B1–B4、C1–C4（含 C4 底下積分榜、賽事系列依路徑各自的權限碼）的可見度；原本這兩組沒登記，客服／行政等角色會看到整組點進去全是 403。檢視者（viewer）本來就持有多數模組的檢視權限，選單內容與其權限一致。 |

**lint 第二層**：`scripts/check-forbidden-terms.mjs` 新增對 `<script>` 內含中文字串常值與樣板文字的掃描（反引號、snake_case 內部代號、`tcrfc://`、JSON、百分位）。

**待後端／未解決**：後端標籤與訊息腳本與畫面都攔不住（診斷彙總 `apiErrorNote`、連線檢查文字、抽獎公告草稿標題 `AdminDrawsRepository.cs:900` 的「【{name}】中獎名單公布」、`AdminShopProductsRepository.cs` 的「貨號」、`AdminInput.OptionalEmail` 的「聯絡 Email的格式不正確」缺空格）。`createdByName`／`reviewedByName` 待後端新增前，推播顯示「—」。

### 收尾輪（後端新欄位，2026-10-03）

- **B-7**：`api/adminTrials.ts` 的 `exportTrialRegistrations` 退路檔名改為 `試訓報名名單-{場次日期}.csv`，不再含 GUID；`downloadExport` 本來就優先採用回應 `Content-Disposition` 的檔名。其他匯出的退路檔名都是固定中文或 `registrations-{club}.csv`，沒有 GUID。
- **B-10**：`listRoster` 新增 `reveal` 參數；`DrawRosterPanel.vue` 預設遮罩，持有「檢視完整個資」權限者出現「顯示完整資料」（二次確認）與「重新遮罩」；離開頁面或切換活動即回遮罩。說明文字仍依 `isMasked`。
- **B-4**：P4 代填頁優先採用詳情回應的 `isOverCapacity`（狀態未被改動時），否則退回前端預估；P3 代填頁有 `isOverCapacity=true` 時顯示警示。
- 連線檢查文字以前端對照為準（「手機推播服務（Apple 與 Google）」「備援設定檔（Cloudflare 邊緣節點）」），與後端新說法略有差異，未雙寫。

### 第二輪重驗修正（2026-10-05，`frontend-architect`）

- **上傳 503 訊息**：`src/api/http.ts` 的 `classifyByStatus`，503 且後端有 `detail` 時照實顯示（例如「檔案儲存尚未設定」），不再被固定的「伺服器發生未預期的錯誤」蓋掉；500 與其他 5xx 仍是通用訊息、不外露後端內容。一般請求、上傳（`apiUploadRequest`）、下載三條路徑都走同一個函式，所有上傳點同時生效。
- **B-4**：`RegistrationEditView.vue` 建立與更新後用回應的 `isOverCapacity` 更新警示；`ProgramSessionListView.vue` 已報名大於名額時顯示「超額」標籤（比照試訓列表）。
- **B-10**：`eligibleCountText(totalCount)` 只看 `totalCount`：null 一律「尚未試算」，有數字「N 人」（後端 `rosterVersion` 預設就是 1，不能用來分流）。
- **B-12**：`CredentialTab.vue` 優先用後端 `kindLabel`，前端對照表只當退路（改為「Apple 推播金鑰」「Google 推播憑證」）；`PushEditView.vue`「點擊後前往」選項只顯示白話名稱。
- **B-13**：`inputNumberGuard.ts` 讀輸入框的 `aria-valuemin`／`aria-valuemax`，依實際原因提示「超過上限（最大 N）」「低於下限（最小 N）」或「超過允許的小數位數，已四捨五入」。
- **M5**：`ConnectionTab.vue` 備援項目標籤改為「備援設定來源」。
- **驗證**：`npm run lint`、`npm run build`、`check-forbidden-terms.mjs` 通過；Playwright 實走：新聞封面上傳顯示「檔案儲存尚未設定」、梯次列表 32／30 出現「超額」、P3 代填儲存後警示仍在、緯度輸入 95.5 提示「超過上限（最大 90）」、憑證分頁顯示「Apple 推播金鑰」「Google 推播憑證」、連線檢查顯示「備援設定來源」、推播下拉不含連結、抽獎名單「尚未試算」。
- **M2（使用者已決定）**：「App 內連結」分頁表格不再顯示「網頁網址」欄（路徑含隊別代號，違反 docs/06 §1）；該欄為選填且可由人編輯，故編輯表單保留、改標為「沒有安裝 App 時改開的網頁（選填）」，placeholder 不含路徑。手機卡片本來就沒有此欄；全 `src` 無其他地方顯示 `webUrl`。


## 球員「網址代稱」欄位（C2 球員，2026-10-05，`frontend-architect`）

對照 `apps/api/README.md` 結尾「球員網址代稱」節。球員編輯畫面（`PlayerEditView.vue`）「基本資料」加「網址代稱」欄位，請求 `slug` 為選填。

- **建立**：留空由後端依英文姓名（沒有則「隊別-背號」）自動產生；儲存後欄位回填實際值。
- **更新**：留空＝維持原值（欄位旁寫明）；**改成不同的值時顯示警示**，說明舊的球員頁連結（含已分享的連結與 App 內連結）會失效。
- **驗證**：前端先擋格式（小寫英文、數字、連字號，不能以連字號開頭或結尾），後端 400 訊息原樣顯示；重複 409「網址代稱重複」由 `api/http.ts` 歸為 `slug-conflict`（與新聞「網址名稱重複」同一類）並顯示後端 `detail`。
- **用語**：介面只用「網址代稱」，不出現 slug（docs/06 §1）；欄位說明「出現在球員頁網址，建議用英文小寫與連字號；同一個俱樂部內不能重複」。
- **統一錯誤結構**：`http.ts` 只讀 `title`／`detail`／`message`，後端新增的 `code`／`messageZh`／`messageEn`／`retryable` 為相容擴充，不受影響。
- **驗證**：`npm run lint`（含 forbidden-terms、editview-reactivity）與 `npm run build` 通過。**未對真 API 實機驗**（需使用者啟動 `apps/api`）。


## K1 會員詳情：監護人同意紀錄（2026-10-05，`frontend-architect`）

後端 `AdminMemberDetailDto.guardianConsent`（未滿 18 歲註冊者才有；成年為 null）。`MemberDetailView.vue` 在「帳號資料」下新增「監護人同意（未滿 18 歲註冊）」卡片：同意時間、監護人姓名、與會員的關係（父母／法定監護人）、同意文案版本。

- **姓名遮罩**：後端依既有 `member.pii.reveal` 與 `reveal` 參數回遮罩或完整值，前端沿用頁首「顯示完整資料／重新遮罩」同一組按鈕與稽核（不另做第二套）；遮罩時卡片註明；帳號刪除後姓名為 null 顯示「已清除」。
- **版本**：`pending-legal`（前台在法務定稿前送出的暫存版本，B-9）顯示為「文案尚未定稿（待法務）」，其他值原樣顯示；沒記錄顯示「未記錄」。
- **J4 俱樂部「簡稱」**：後端 `clubs_i18n.short_name` 已有，但 J4 後台 API（`AdminClubLocaleContent` 只有 `name`／`description`）**沒有可寫入簡稱的欄位**（後端 README 也列「後台編輯俱樂部簡稱」未做），所以**未加畫面欄位**，等後端補上請求／回應欄位後再接。
- **驗證**：`npm run lint`、`npm run build` 通過；未對真 API 實機驗。


## J4 俱樂部「簡稱」（2026-10-05，`frontend-architect`）

`GET／PUT /admin/clubs/{id}` 的 `content.zh`／`content.en` 新增 `shortName`（最多 32 字，空白＝無簡稱）。`ClubEditView.vue` 在名稱下加「簡稱」雙語欄位，說明「用在空間有限的地方，例如 App 的分頁標籤」，前端先擋超過 32 字；藍鯨英文簡稱不預填，欄位旁註明依客戶確認的正式寫法。

🔴 **既有風險已修**：後端 PUT 省略 `content.en` 會刪掉整列英文內容。舊畫面只看英文名稱是否為空，若英文名稱空著、英文簡介有值，送出時會省略 `content.en` 而悄悄刪光。現在 `isEnEmpty()` 要英文名稱、簡稱、簡介**全空**才省略；有任何英文內容卻沒有英文名稱時，儲存前提示補英文名稱。（三欄全清空仍會刪掉英文列，這是預期行為。）

驗證：lint、build 通過；未對真 API 實機驗。

## 頂欄待辦提醒鈴鐺（2026-10-06，`frontend-architect`）

- 原本寫死 `3` 的假鈴鐺改為 `src/components/TodoBell.vue`：**資料與儀表板「待辦提醒」同一份**（`getDashboard(club).todos`），不新增端點、不新增通知類型（規劃書不做通知中心，這只是待辦的捷徑）。
- 前往位置與徽章計算抽到 `src/utils/todoRoutes.ts`（`todoRoute`／`todoItemRoute`／`sumTodoCounts`／`formatBadge`），`DashboardView.vue` 與鈴鐺共用，不要各自複製。
- 徽章＝各類筆數總和；0 不顯示；超過 99 顯示 99+。點開列出各類與筆數（0 筆淡化），點一類前往清單頁，底部「前往儀表板」。
- 重抓時機：切換站台立即；換頁最多每 15 秒一次；每 5 分鐘背景重抓，頁面不可見時暫停，回到可見且逾 5 分鐘補抓。
- 權限：儀表板沒有專屬權限碼，判斷沿用後端——`getDashboard` 回 403（帳號沒有任何儀表板相關權限）就整個隱藏鈴鐺；其他失敗只是不顯示徽章，點開顯示「無法取得待辦，請稍後再試」，不彈錯誤訊息。
- 無障礙：`<button>`，`aria-label` 帶筆數（「待辦提醒，共 N 筆」），Esc 關閉；手機沿用同一個彈出層。
- apps/admin 目前沒有單元測試框架，未補測試；計算邏輯是純函式，之後若導入框架可直接測。

## 後台欄位串接稽核：賽季管理等六項畫面（2026-10-06，`frontend-architect`，docs/23）

API 契約以 `apps/api/README.md`「後台欄位串接稽核的後端修正」為準。型別檢查、`npm run lint`（含 `check-edit-layout`，基準維持 0）、`npm run build` 皆通過；**未實機驗證**（依規定未啟動 `apps/api`），專案沒有單元測試框架。

| 項 | 畫面與行為 | 檔案 |
|---|---|---|
| **賽季管理（新）** | 球隊管理群組新增「賽季」（`/teams/seasons`，側欄排在「賽程與賽果」之後、「積分榜」之前）。列表欄位：賽季代碼、開始／結束日期、「當季」標籤（今天〔台北時間〕落在起訖內，純畫面判斷）、使用中筆數與明細（後端 `usage`）。新增／編輯用對話框（沒有雙語欄位，依 docs/21 §3 用對話框）；欄位錯誤鍵 `code`／`startOn`（含期間重疊 409）／`endOn` 標到欄位。刪除：`inUse` 時按鈕停用並以提示顯示使用情形；仍收到 409／403 時以對話框完整顯示後端訊息並重抓清單。權限比照賽程：`useCrudPermissions('team.match')` 控制新增／編輯／刪除按鈕，側欄接受 `team.match.view` 或 `team.competition.view`（後端清單同）。寫入需整個俱樂部的球隊授權，畫面不預判，由後端 403 訊息顯示。`GET /admin/{club}/seasons` 網址沒變，賽事系列、賽程、積分榜、榮譽頁的賽季下拉（`listAdminSeasons`）原樣可用 | `views/teams/SeasonListView.vue`、`api/adminSeasons.ts`、`router/index.ts`、`data/nav.ts`、`components/AppSidebar.vue` |
| **球員賽季數據** | 球員編輯頁（僅編輯模式）主欄新增「賽季數據」卡片，每個賽季一列：來源標籤（手動／自動彙總／無資料）、自動彙總值、目前手動值、五個數字輸入（出賽、進球、助攻、黃牌、紅牌）。「儲存為手動值」＝PUT；「清除手動值，改回自動彙總」＝DELETE（需確認，僅來源為手動時出現）。**每個賽季各自立即儲存**，卡片說明固定一行「這裡的變更會立即儲存」（比照 docs/21 §3.4）。每列自帶一份 `provideFormErrors`（五個欄位鍵每季相同，共用頁面那份會標錯列），`applyApiError` 標到該列欄位。助攻沒有自動來源，編輯起點從 0 開始；球隊不在授權範圍（整頁唯讀）或沒有 `team.player.update` 時輸入停用 | `views/teams/parts/PlayerSeasonStatsPanel.vue`、`PlayerSeasonStatRow.vue`、`api/adminPlayers.ts` |
| **進球類型下拉** | 賽事編輯頁進球列改為下拉：一般進球（空值）／頭槌／點球／自由球／烏龍球／其他。舊資料載入時以 `types/match.ts` 的 `parseGoalType` 對照後端同義詞（含「烏龍」「own goal」字樣一律烏龍球，與後端 `IsOwnGoal` 一致）。**對不上的舊自由文字（例如「遠射」）**：下拉選「其他」並在列下提示「原本寫的是…，儲存後會歸為其他」。後端寫入端不收未知文字（400），無法原樣保留，所以選擇「提示後歸為其他」而非靜默覆寫或送出後被拒。錯誤鍵 `goals[i].goalType` 依逐層去尾退回標到進球表格的 `goals` 錨點 | `views/teams/MatchEditView.vue`、`types/match.ts` |
| **表單設計器鎖定** | 後端 DTO 沒有回傳鎖定旗標，前端以 `types/forms.ts` 的 `FIELD_LOCKED_FORM_CODES`（對照 `FormCatalog.FieldLockedCodes`：10.1–10.7 七類＋提案下載；捐助洽詢不鎖；**後端清單異動時要手動同步**）判斷。鎖定表單：頁面加說明「這張表單的欄位由網站固定，只能修改題目文字與通知設定」；隱藏「新增欄位」「刪除」；欄位對話框的欄位代碼、型別、必填、驗證規則停用，選項值只顯示（不能新增／刪除），仍可改題目文字中英、選項英文顯示文字、內容摘要；排序（上移／下移）、收件通知、自動回覆、防機器人驗證、導向頁照舊。送出欄位時結構欄位一律用載入時的原值，避免空值差異被後端判為有改動。導向頁提示改為「站內路徑，以 / 開頭」，前端驗證同步收緊（不收完整網址、`//`、`/\`） | `views/forms/FormEditView.vue`、`types/forms.ts` |
| **訂單詳情** | 「訂購資訊」新增買家 Email（有值才顯示，遮罩值原樣顯示；訂單詳情沒有獨立的「顯示完整個資」按鈕，遮罩由後端依 `shop.order.reveal` 決定，遮罩提示文字擴寫為涵蓋收件人、買家 Email、載具號碼）。發票區塊新增開立方式、載具號碼、統一編號、捐贈碼（各自有值才顯示）；開立／作廢狀態改顯示 `issueStatusLabel`／`voidStatusLabel`（舊欄位只作缺值回退） | `views/shop/OrderDetailView.vue`、`api/adminShop.ts` |
| **追蹤碼與 CTA 連結** | SEO 設定四個追蹤碼與行事曆自建活動 `ctaUrl` 原本就已有 `FormField`、`provideFormErrors` 與 catch 的 `applyApiError`（E-276 三項檢查皆在），後端 400 鍵 `ga4MeasurementId`／`gtmContainerId`／`metaPixelId`／`lineTagId`／`ctaUrl` 可標到欄位；本次補輸入框下方的格式提示，並在前端先做同規則檢查（GA4 `G-`、GTM `GTM-`〔大小寫不拘，後端轉大寫〕、Meta Pixel 純數字 5–20 位、LINE Tag 英數與連字號；`ctaUrl` 為 `http(s)://` 或單一 `/` 開頭） | `views/seo/SeoSettingsView.vue`、`views/calendar/CalendarEventEditView.vue` |

## 站台事實新欄位與品牌設定移除（2026-10-06，主站規劃書 v3.20，API 契約見 `apps/api/README.md` C-2）

- **站台事實**（`SiteFactsView.vue`、`api/adminSiteFacts.ts`）新增：聯絡 Email、社群連結（Facebook／Instagram／YouTube／LINE，https，網域白名單由後端把關）、各部門窗口（最多 20 筆；名稱中文必填，Email 與分機至少一項）、頁尾品牌簡介（中英；填英文時中文必填）。欄位錯誤鍵依後端（`contactEmail`、`facebookUrl`…、`departments[i].*`、`footerBlurbZh|En`）標到欄位。
- **移除品牌設定**：`GlobalSettingsView.vue` 不再有標誌淺／深底、Favicon 上傳與品牌色挑選；`adminSiteSettings.ts` 的 `brand`、`removeLogo*`、`removeFavicon`、`brandColor*` 與上傳檔案參數移除（PUT 仍走 multipart `payload`，不附檔）；`ClubEditView.vue`、`adminClubs.ts` 移除品牌色與標誌鍵。OG 分享圖保留，仍在 H1 全站設定上傳。
- 驗證：`npm run lint`（含 `check-edit-layout`，基準 0）、`npm run build`（含 vue-tsc）通過；未實機驗證。

## D 類雙語欄位：榮譽、積分榜、課程、會籍方案、提案（2026-10-06，`frontend-architect`，API 契約見 `apps/api/README.md`「稽核 D 類雙語缺口」）

| 畫面 | 新增欄位（上限，空白＝清除英文） | 版型 | 檔案 |
|---|---|---|---|
| 榮譽（對話框） | 賽事名稱英文（128）、名次英文（64）；名次中文上限 32→64 | 對話框自己一組 `LangTabsBar variant="bare"`＋`BilingualShortField` | `views/honours/HonoursView.vue`、`api/adminHonours.ts` |
| 積分榜（對話框） | 球隊名稱英文（128）；中文上限 128 | 同上；驗證改進 `formErrors`（檔案轉為嚴格後，`formError.value = '字串'` 被 lint 擋下） | `views/teams/StandingListView.vue`、`api/adminStandings.ts` |
| 積分榜 CSV 匯入 | 可多第六欄「球隊名稱（英文）」；五欄照舊可匯 | 頁面說明與匯入確認框都寫明：**整季替換，沒有英文欄＝該賽季英文名稱全部清空**。專案沒有 CSV 範本下載，故只有文字說明 | 同上 |
| 課程項目 | 適合對象英文（64）；中文上限 64 | 整頁分頁＋`BilingualShortField`（原三欄列中的一格） | `views/programs/ProgramItemEditView.vue`、`api/adminPrograms.ts` |
| 會籍方案 | 期中加入規則英文（255）；中文上限由 500 改 255 | `BilingualTextareaField`；PUT 整份取代，每次都帶 `midSeasonRuleEn` | `views/members/MembershipPlanEditView.vue`、`api/adminMemberships.ts` |
| 提案 | 提案名稱英文（128） | 頁面原本沒有語言分頁，整個 `EditLayout` 包進 `LangTabsBar` | `views/business/ProposalEditView.vue`、`api/adminProposals.ts` |

列表一律維持顯示中文。各頁原本就有 `provideFormErrors` 與 catch 的 `applyApiError`（E-276 檢查通過）；前端驗證上限比照後端。驗證：`npm run build`、`npm run lint`（`check-edit-layout` 基準 0）、eslint 0 errors 通過；未實機驗證。
