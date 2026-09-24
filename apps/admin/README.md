# apps/admin — admin-web（官網共用後台）

台中磐石官網主站與台中藍鯨官網**共用同一個後台**的 Vue 3 SPA。

✅ **S1（2026-09-24）：接上真實登入與 J1／J2／J4／C4 賽事系列**——外殼＋儀表板＋「新聞與故事」
（B2）＋帳號（J1）＋角色與權限（J2）＋俱樂部與授權管理（J4）＋賽事系列（C4 底下的一小部分）
現在都是真的打 `apps/api` 的畫面，不再是假資料。其餘 10 個模組仍是明確的「尚未建置」佔位頁
（不是死連結）。**開發寫入閘門（`DevWriteGate`／`X-Dev-Operator-Id`）已隨後端整支移除**，
`src/api/gate.ts`／`src/api/devOperator.ts` 這兩個檔案本輪一併刪除，所有寫入端點一律走真實
登入權杖（見下方「登入與工作階段」）。

🟡 **尚未真的做的（本輪不在範圍內，回報）**：J3 稽核與備份（後端本來就已撤回稽核記錄，見
apps/api/README.md）、C4 的完整賽程賽果（本輪只做了「賽事系列」這個支援型別，見 `data/nav.ts`
該筆的註解）、其餘 10 個模組維持假資料 mockup 狀態。

規格的真實來源是規劃書與 [`docs/03-admin-spec.md`](../../docs/03-admin-spec.md)；版面與視覺規則是
[`docs/21-admin-ui.md`](../../docs/21-admin-ui.md)（v3，`visual-design-architect` 產出，
本次實作依它逐條核對）。

🔴 **深色是唯一主題，不是淺色的延伸**（2026-09-21，`S0-12c` 依 `docs/21` v2 重做）：舊的淺色＋
Element Plus 預設藍版本已整批換掉，**不做主題切換開關，不留淺色 token 分支**（`docs/21` §7.0
使用者已拍板）。版面骨架也不是換色而已——頂欄拆成系統列／頁面列兩層、側欄選中態改用 accent bar、
狀態 tag 換成深色語意底＋亮色文字、編輯頁分段卡片改用四層背景色階（tonal elevation）取代陰影、
圖片預覽區塊加了不隨主題變化的「中性看片台」。細節見下方各節與 `docs/21` §1–§9。

🔴 **配色 v3（2026-09-22）：中性深灰＋Element Plus 藍 → 品牌黑＋品牌桃紅**（依 `docs/21` §4／§5／§7／§15
重做）：四層背景色階改由品牌黑 `--ink`／`--ink-2` 推導、操作主色改用品牌桃紅系 `--brand-aa`／
`--brand-bright`／`--brand-deep`、文字與邊框階跟著暖化、危險色橘紅化（`H≈0°→14°`）、站台切換器隊徽
從純色色塊改用兩隊各自的真實隊徽圖像（`src/assets/brand/`）。**切換站台仍然不換主色**（`docs/21`
§5.1／§5.1.1：後台永遠是磐石桃紅）。中性看片台（`--admin-lightbox-*`）明文不隨這次換色調整，維持
中性冷灰。`--admin-border-input` 用的是**門檻反推值 `#8A7E75`**，不是暖化等亮度換算的 `#7A6F68`——
後者對 `overlay` 只有 2.66:1，過不了 3:1 的 UI 元件門檻，這條與 E-31 同根因，已寫進
`scripts/check-contrast.mjs` 的邊框笛卡兒積檢查（見下方「檢查腳本：對比度檢查」）。

---

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
3. **已登入但 `must_change_password` 或兩階段驗證尚未啟用** → 強制導去
   `/account/security?forced=password`／`?forced=totp`（`src/views/account/AccountSecurityView.vue`），
   同一頁同時服務「被強制」與「使用者自己從選單點進來調整」兩種情境，`forced` 查詢參數只影響
   要不要顯示提示 banner 與完成後要不要自動導回 `/dashboard`。⚠️ **這兩個前提是後端
   `AdminAccountGate` 真正擋住每一個俱樂部範圍與系統範圍端點的條件**（見 apps/api/README.md），
   前端這裡只是提前把使用者導去做完，不是安全邊界——就算跳過這一頁，後端一樣會擋。
4. **`meta.sysadminOnly` 的路由**（J1／J2／J4）非系統管理員直接改網址進入會被彈回 `/dashboard`
   並跳出「你的帳號沒有權限進入這個模組」——這也只是第二層提醒，`AppSidebar.vue` 依
   `authUser.value?.isSuperAdmin` 整組濾掉側欄項目是第一層，**真正把關永遠是後端**每個
   `system.*` 權限碼皆為 `sysadmin_only`。

**站台切換器接上真實俱樂部清單**（`src/auth/clubAccess.ts`，取代原本寫死在 `data/session.ts`／
`data/activeClub.ts` 的假資料，這兩個檔案本輪已刪除）：呼叫公開的 `GET /api/v1/clubs`
（不需要登入）列出系統裡有哪些俱樂部。🔴 **已知 API 缺口**（回報，未動手改 `apps/api`）：
規劃書要切換器「依登入者的俱樂部授權列出可切換的站台」，但後端目前沒有任何端點能讓一般帳號
查詢「我自己」被授權哪些俱樂部（`GET /accounts/{id}/club-grants` 需要 `system.club_grant.view`，
`sysadmin_only`）；`/auth/login`／`/auth/refresh` 的回應也不含 `primaryClubId` 或授權清單。
目前的處理方式：切換器列出**全部**俱樂部，選到未授權的俱樂部時，該頁會如實顯示後端回傳的 403
訊息（「你沒有被授權存取俱樂部「...」的後台資料。」）——這正是 docs/21 §5「切換器是介面便利，
不是安全邊界」的意思，不算功能性錯誤，但不是規劃書要的「只列出被授權的站台」。**建議後端補一支
`GET /api/v1/admin/auth/me`**，回傳 `displayName`／`primaryClubId`／目前有效的俱樂部授權清單／
角色代碼，屆時把 `clubAccess.ts` 改回真正過濾即可，不影響呼叫端介面。

## 專案結構

```
apps/admin/
├── src/
│   ├── main.ts              # 掛載點：固定在 <html> 掛 class="dark"（深色是唯一主題，不做切換開關）
│   ├── App.vue               # 只有一個 <router-view />
│   ├── router/index.ts       # 路由表 + 登入／強制流程／sysadminOnly 三層路由守衛（見上方「登入與工作階段」）
│   ├── auth/
│   │   ├── session.ts        # 登入工作階段單例：存取權杖（記憶體）＋使用者旗標，見上方存放策略說明
│   │   └── clubAccess.ts     # 站台切換器的俱樂部清單＋目前選取的 activeClubId（原 data/session.ts／data/activeClub.ts 已刪除）
│   ├── layouts/AdminLayout.vue   # 外殼：側欄／頂欄／drawer／響應式斷點切換（桌面／平板／手機）
│   ├── components/
│   │   ├── AppSidebar.vue        # 側欄選單（6 組視覺分組、el-sub-menu 手風琴、選中態改 accent bar；J 系統管理整組依 isSuperAdmin 濾掉）
│   │   ├── AppTopbar.vue         # 系統列（桌面/平板 40px）／合併列（手機 56px，含頁面標題）
│   │   ├── PageHeader.vue        # 頁面列：模組標題＋「這裡管理的是」固定兩行，各頁面內容區頂端自己渲染
│   │   ├── SiteSwitcher.vue      # 站台切換器（docs/21 §5：只換徽章，不換整套主色；手機移進 drawer 頂部；俱樂部清單來源見上方）
│   │   ├── UserMenu.vue          # 顯示真實登入帳號＋「帳號安全設定」／「登出」（呼叫真實 `/auth/logout`）
│   │   ├── FrontendUnitBanner.vue # 「這裡管理的是：{前台單元} ↗」（規劃書 §4.0 硬性規定）
│   │   ├── StatusTag.vue         # 草稿／排程發布／已發布／已停用 四態，深色語意底＋亮色文字（非 el-tag 預設 type）
│   │   ├── ImageUploader.vue     # S0-8 起接上真實上傳端點，見下方「圖片上傳共用元件的前端接線」；預覽框鋪中性看片台（docs/21 §9.1）
│   │   └── BilingualShortField.vue # 雙語短欄位：桌面並排、手機自動變 tabs
│   ├── views/
│   │   ├── DashboardView.vue
│   │   ├── PlaceholderView.vue   # 尚未建置模組的共用畫面（不是死連結）
│   │   ├── NotFoundView.vue
│   │   ├── auth/LoginView.vue           # 帳密 → （若已啟用）驗證碼，兩步驟同一頁切換
│   │   ├── account/AccountSecurityView.vue  # 改密＋兩階段驗證設定，強制流程與使用者自行調整共用同一份
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
│   │   ├── adminAuth.ts        # 登入／refresh／登出／改密／2FA 三支
│   │   ├── adminNews.ts        # B2（既有）
│   │   ├── adminAccounts.ts    # J1 帳號 ＋ J4 掛在帳號底下的俱樂部／球隊授權
│   │   ├── adminRoles.ts       # J2 角色與權限
│   │   ├── adminClubs.ts       # J4 俱樂部主檔（標誌／favicon／OG 圖唯讀，見該檔案檔頭）
│   │   ├── adminCompetitions.ts # C4 賽事系列
│   │   └── publicClubs.ts      # 公開 `GET /api/v1/clubs`，站台切換器用
│   ├── data/                 # 假資料與靜態設定（session.ts／activeClub.ts 已刪除，見上方），見下方「假資料放哪」
│   ├── types/                 # 共用 TypeScript 型別
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
  品牌色。🔴 **標誌／favicon／OG 圖三組欄位本輪唯讀**（另一位 backend agent 同時在改圖片上傳，
  任務指示要求不要動它，見 `src/api/adminClubs.ts` 檔頭）——畫面只顯示「已設定」／「尚未設定」，
  不提供上傳，之後應比照新聞封面圖片的 multipart 契約補上。

## 賽事系列（C4 的一小部分，S1，2026-09-24）

`views/teams/CompetitionListView.vue`／`CompetitionEditView.vue`，路徑掛在側欄既有的
「C4 賽程與賽果」底下（`data/nav.ts` 該筆已標成 `implemented: true`，附一行註解說明範圍）。
⚠️ **這不是完整的 C4**——規劃書 C4 涵蓋賽季、賽事名稱、日期時間、主客場、對手、比分、出賽名單等
完整賽程賽果（`docs/03-admin-spec.md` C4 全部條文），本輪只做了「賽事系列」（`Competition`，
賽程賽果的分類支援型別，例如「企業甲級聯賽」）這一小塊，畫面上有明顯提示「本階段僅開放維護
賽事系列……實際的賽程日期、比分與出賽名單尚未開放」，完整功能留給之後的 `S1-8`。

🔴 **已知 API 缺口（回報，未動手改 `apps/api`）**：建立賽事系列需要指定所屬「賽季」
（`Season`），但後端**沒有任何端點可以列出俱樂部有哪些賽季**——`Season` 目前只在種子腳本裡建立，
沒有對應的維護或列表端點。畫面上因此用一個「賽季識別碼」文字輸入框（GUID 格式驗證）當權宜作法，
並在畫面與程式註解都清楚標示這是暫時的。編輯既有資料時會顯示目前的賽季代碼供對照，不需要使用者
自己記。**建議後端補一支賽季清單端點（甚至一併補上賽季維護端點）**，屆時把這裡換成下拉選單即可。

同一類缺口也出現在 **J4 球隊授權**（`AccountEditView.vue` 的球隊授權分頁）：後端沒有任何端點能
列出「有哪些球隊」，同樣只能先用文字輸入球隊識別碼，待 `C1 球隊` 或專門的清單端點做出來後再補上
下拉選單。

## 驗收紀錄（S1，2026-09-24，本機環境）

`npm run lint`／`npm run typecheck`／`npm run build` 全過。實際起 `apps/api`
（`dotnet run --no-launch-profile`，連本機既有 `sqlserver` 容器的 `tcrfc_club_dev`）與
`npm run dev`（`:5174`），用無頭 Chrome + CDP 寫腳本驅動真實瀏覽器（不是模擬 fetch）逐步操作：

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

## 交付範圍與邊界

本階段**做了**：外殼（側欄＋頂欄＋站台切換器＋使用者選單，全部接真實登入）、儀表板、新聞與故事的
列表頁與編輯頁（改走真實登入權杖）、**登入／TOTP 兩階段驗證／首次登入強制改密／JWT 15 分鐘＋
更新權杖輪替與自動 refresh／登出**、帳號管理（J1）、角色與權限（J2）、俱樂部與授權管理（J4，
含掛在帳號底下的俱樂部授權與球隊授權）、賽事系列（C4 的一小部分）。

**不做**（本輪範圍外或有已知缺口，見上方各節）：J3 稽核與備份（後端已撤回稽核記錄）、C4 的完整
賽程賽果、其餘 9 個模組的真實功能（都是明確的佔位頁）、J4 的俱樂部標誌／favicon／OG 圖上傳、
賽季與球隊的清單／維護端點（回報的 API 缺口）、站台切換器「只列出被授權的俱樂部」（回報的 API
缺口，目前列出全部俱樂部，未授權時由後端 403 擋下）。

## 已知的 API 缺口彙整（本輪回報，未動手改 `apps/api`）

任務指示要求「發現 API 缺什麼才能做，停下該部分並回報」，三處彙整在這裡，個別細節見上面各自的
小節：

1. **沒有 `GET /api/v1/admin/auth/me`（或等效端點）**：一般帳號無法查詢「我自己」的
   `displayName`／`primaryClubId`／有效俱樂部授權／角色代碼。影響：站台切換器只能列出全部俱樂部
   而非「被授權的站台」；`UserMenu.vue` 只能顯示 `username`（登入帳號字串），顯示不出姓名。
2. **沒有任何端點可以列出俱樂部的「賽季」（`Season`）**。影響：建立賽事系列時的賽季欄位只能讓
   使用者自己貼識別碼，不是下拉選單。
3. **沒有任何端點可以列出「球隊」**（`C1 球隊` 本身也還沒有維護端點）。影響：J4 球隊授權的球隊
   欄位同樣只能貼識別碼。

三者都不影響已完成功能的正確性（後端仍然是最終的授權與資料範圍把關），純粹是「畫面沒有更好的
輸入方式可用」的可用性缺口。
