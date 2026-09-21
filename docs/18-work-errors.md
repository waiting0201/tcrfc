# 18 — 作業失誤紀錄（做錯過的事）

> **這一份收「我們真的做錯、而且下次還會再犯」的事**，不是規格，也不是待辦。
> 目的只有一個：**同樣的錯不要犯第二次。**
>
> 與另外兩份的分工——三份都不可混用：
>
> | 檔案 | 收什麼 | 一句話 |
> |---|---|---|
> | [`14-invariants.md`](14-invariants.md) | 全站不變量 | **改錯會出事** |
> | [`00-harness.md`](00-harness.md) §5 | 規格踩雷點 | **舊規格會誤導** |
> | **本檔** | 作業失誤 | **上次是怎麼做錯的、防呆放在哪** |
>
> ⚠️ **本檔不得新增規格。** 某筆失誤如果牽出規格要改，走 [`00-harness.md`](00-harness.md) **§2.5 同步鏈**，
> 本檔只留「當時為什麼會錯」。

---

## 0. 怎麼記

**什麼時候記**：只要發生「做錯又修回來」就記——不論是使用者當場指正、審查抓到、驗證腳本抓到，
還是跑完流程才發現漏了一環。**在同一次交付內補上，不要留到下次**（同 `CLAUDE.md` 全域規定第 11 條）。

**一筆五個欄位**：

| 欄位 | 要求 |
|---|---|
| 日期 | 發現的日期，`YYYY-MM-DD` |
| 錯在哪 | 具體到檔案、數量、影響範圍。「有些地方寫錯」不算 |
| **為什麼會錯** | **根因，要寫成可以被改掉的行為**（例：憑記憶寫、沒回查來源；只改了章節本體沒 grep 全文）。**不准寫「不小心」「粗心」** |
| 下次怎麼避免 | 一個做得到的動作，不是決心 |
| 防呆 | 自動檢查放在哪（腳本、lint、test）。**沒有就寫「無」**——「無」本身就是待辦訊號 |

**三條規則**：

1. **同一類錯第二次發生 → 不要再加一筆**，回頭把原本那筆**升級**：補防呆，或把結論寫進 [`14-invariants.md`](14-invariants.md)。
   記了兩次還在犯，代表記錄沒用，要的是機制。
2. **已經有自動檢查擋住的標 ✅**，沒有防呆的標 ⚠️。⚠️ 的項目才需要每次動手前掃。
3. **只記事實與根因，不記檢討情緒。** 這份是給下一個 session 讀的，不是自白書。

---

## 1. 速查

| # | 日期 | 一句話 | 防呆 |
|---|---|---|---|
| E-01 | 2026-09-03 | 品牌英文名憑印象寫成 `Taichung Rocks`，全站 28 處 | ⚠️ 無 |
| E-02 | 2026-09-10 | 改規格只改了章節本體，同一份文件他處殘留舊敘述、自相矛盾 | ⚠️ 無（靠收尾 grep） |
| E-03 | 2026-09-12<br>2026-09-18<br>2026-09-20 | 規劃書行數變了但 `docs/` 行號對照表沒重算；**重算時又用全域加法把章節編號也一起位移** | ✅ [`tools/check-linerefs.mjs`](tools/check-linerefs.mjs) |
| E-04 | 2026-09-03 | header 一條錯連結＝全站 72 頁同時壞 | ✅ `verify.mjs` 斷鏈檢查 |
| E-05 | 2026-09-03 | 檢查腳本自己抓錯 class，長期回報「待補 0 處」，真實是 28 處 | ✅ 已改抓 `pending-*` 家族 |
| E-06 | 2026-09-12 | 後台模組樹漏列廣告三項，**中英雙版一起漏** | ⚠️ 無 |
| E-07 | 2026-09-10 | 客戶版 PDF 落後於規劃書——同步鏈第 3 環沒跑完 | ⚠️ 無 |
| E-08 | 2026-09-14 | `CLAUDE.md` 長到 260 行、51% 是知識，變成第二份規格書 | ⚠️ 無（靠行數自覺） |
| E-09 | 2026-09-14 | 踩雷點編號重複（出現兩組 12–15） | ⚠️ 無 |
| E-10 | 2026-09-12<br>2026-09-18 | 代號順移後舊代號語意改變，舊文件的 `E4` 全部變成錯的；**`B` 模組的殘留半年後才被掃到** | ⚠️ 無（改代號後固定跑一次全 `docs/` 代號對樹） |
| E-11 | 2026-09-20 | **該派 agent 的工作自己做掉**（`docs/12` 四節改寫、14 張 ERD、`docs/16` 23 張表從零設計），違反全域規定第 12 條 | ⚠️ 無（已把界線寫進第 12 條） |
| E-12 | 2026-09-20 | `docs/17` §1 的 Redis healthcheck 片段寫成 exec form，`$$REDIS_PASSWORD` 不會被展開，會一直回報不健康 | ✅ 已改 `CMD-SHELL`（`docs/17` 本體與 `docker-compose.yml` 皆已修正） |
| E-13 | 2026-09-21 | **建了一個叫 `docker-compose.staging.yml` 的 override，與同一批文件裡「不建 staging 環境」直接牴觸**——功能沒錯，但檔名憑空多造出第三套環境的印象 | ✅ 已刪檔，改為 `.env` 的 `CADDYFILE`；環境數量寫進 [`14-invariants.md`](14-invariants.md) |
| E-16 | 2026-09-21 | Vue SFC 註解裡寫出完整的 `script`／`style`／`template` 字面標籤，`build` 直接壞（誤判成 async setup 衝突，繞了一圈才找到真因） | ⚠️ 無（留給 S0-9 補 lint 檢查） |
| E-17 | 2026-09-21 | `@nuxtjs/seo` 的 `nuxt-seo-utils` 子模組蓋掉元件層 `useHead` 設的 `<html lang>`，`tagPriority: 'high'` 也蓋不掉 | ✅ 改用 `nuxt.config.ts` 的 `app.head.htmlAttrs.lang` |
| E-18 | 2026-09-21 | `@nuxtjs/sitemap` 的 runtime 動態來源在「一份 build、runtime 才決定 club」的架構下沒被偵測到，`/sitemap.xml` 永遠空 | ⚠️ **尚未解決**，資料端點本身（`/api/__sitemap__/urls`）已驗證正確 |
| E-19 | 2026-09-21 | `apps/api/Tcrfc.Api.csproj` 加了 `<InvariantGlobalization>true</InvariantGlobalization>`，`Microsoft.Data.SqlClient` 一開連線就丟 `System.NotSupportedException: Globalization Invariant Mode is not supported` | ✅ 已移除該屬性，並在 csproj 留註解說明原因 |
| E-20 | 2026-09-21 | Dapper 用建構子具現化 `record` DTO 時，`DateOnly`／`DateOnly?` 屬性對到 SQL `date` 欄位一律丟 `InvalidOperationException`（要求 `DateTime` 簽章）；`ArticlesRepository` 另有一張 i18n 查詢的 SELECT 欄位數與 `record` 建構子參數數對不上，同一種例外 | ✅ 兩處已修（`PlayerRow`／`MatchRow` 改用 `DateTime`、Map() 再轉 `DateOnly`；`ArticlesRepository` 統一欄位組），⚠️ 無自動檢查，日後新增 record 對應 SQL 查詢仍要人工核對型別與欄位數 |
| E-21 | 2026-09-21 | 兩個 agent 各自判定「必然差異」以外的新差異（style 分號、v-model 顯式屬性）無害就自行放行，違反 `docs/14` 明文「發現無法歸類的差異要停下來問，不得自行放行」 | ✅ 六類差異已寫進 `site/tools/compare-dom.mjs` 正規化規則，工具報出的即是真差異，不留判斷空間 |
| E-22 | 2026-09-21 | Nuxt 元件放在子目錄（`components/content/X.vue`）時頁面仍用未加前綴的標籤名引用，**不報錯不警告，該元件整塊悄悄不 render** | ⚠️ 無 |
| E-23 | 2026-09-21 | 頁面搬遷後未重跑 `npx nuxt prepare` 就跑 `npm run lint`，link-checker 拿舊路由表比對新頁面，斷鏈數從 118 假性暴增到 415 | ⚠️ 無 |
| E-24 | 2026-09-21 | `:style="undefined"` 在 Vue SSR 仍印出 `style=""`（空字串，不是省略屬性），與一般屬性的省略行為不同 | ⚠️ 無 |
| E-25 | 2026-09-21 | 補 `match_no` 錨點 id 時，把 `haCode()`（給 `data-ha` 用的完整單字 `home`／`away`）誤套進 `fixtureId()`，id 變成 `fx-2026-09-13-away-3` 而不是 mockup 的 `fx-2026-09-13-a-3` | ✅ `compare-dom.mjs` 一定會抓到（`id` 屬性差異不在六類必然差異內），本次已用 curl 逐一核對 21 個 id 自行抓到並修正 |
| **E-26** | 2026-09-21 | 🔴 **差一步就把 158 張未成年學員照片推上公開 repo**。`.gitignore` 只寫了 `site/src/assets/img/`；S0-9 搬遷把同一批照片 rsync 到 `apps/web/public/assets/img/`（59MB），**新路徑沒有任何忽略規則**，提交前才發現 | ✅ `.gitignore` 已補上新路徑並加註「檔案換位置時規則不會自己跟過去」 |
| E-27 | 2026-09-21 | `.gitignore` 的 `apps/*/bin/`／`apps/*/obj/` 用單層萬用字元 `*`，S0-7d 新增的同目錄子專案 `apps/api/Tcrfc.Api.Tests/bin`／`obj` 多了一層，規則擋不到，`git add` 會把整個建置產物（含第三方 DLL）一起納入待提交清單，提交前用 `git add -n` 核對才發現 | ✅ 已改用 `apps/**/bin/`／`apps/**/obj/`（任意深度） |
| E-28 | 2026-09-21 | `STATUS.md` 寫「後台 15 個模組字母」，實際數規劃書 §4.0 的模組樹只有 **14** 個（`A B C P E F G H I J K L M S`）；多出來的一個疑似把獨立後台的慈善 `N` 算了進去，但規劃書明文它不在本後台之列 | ⚠️ 無 |

---

## 2. 逐筆

### E-01 品牌英文名憑印象寫成 `Taichung Rocks`（2026-09-03，`3dd3198`）

- **錯在哪**：全站 28 處把 `Rock` 寫成 `Rocks`／`ROCKS`，橫跨規劃書中英雙版、PDF、`docs/`、mockup、
  `site/src/` 前台骨架與 `build-pdf.mjs` 的封面頁尾。修一次要動 6 類檔案並升版 v2.1→v2.2。
- **為什麼會錯**：**品牌名稱憑語感補了複數**，沒有回查 [`reference/TCR_logo_CMYK.ai`](../reference/TCR_logo_CMYK.ai) 這個唯一來源。
  英文專有名詞的複數在中文母語者手上特別容易自動加上去。
- **下次怎麼避免**：**專有名詞（隊名、俱樂部全稱、色號、代號）一律複製貼上，不重打。**
  寫之前先開 [`14-invariants.md`](14-invariants.md) 的「名稱寫法」那段對一次。
- **防呆**：⚠️ 無。前台骨架改 Nuxt 時，`verify.mjs` 的檢查移植清單可考慮加一條**禁用字串掃描**
  （`Rocks`／`ROCKS`／`Cornerstone`）。

### E-02 改規格只改章節本體，同一份文件他處殘留舊敘述（2026-09-10，`ed4055d`）

- **錯在哪**：v3.0 已把會籍改為「一人每俱樂部一份」，但 App 規劃書 §1.1 設計前提表仍寫「一份會籍」、
  §3.7 仍寫「一份會籍同時適用兩隊」——**與同一份文件的 §3.5／§3.6 直接矛盾**，而且矛盾就寫在緊鄰的表格旁邊。
- **為什麼會錯**：改規格時**只搜了要改的章節，沒有對「舊說法的關鍵詞」全文 grep**。
  概念改名（一份會籍 → 每隊一份）不像刪功能那樣有明確的刪除目標，最容易漏。
- **下次怎麼避免**：概念改名時，**把「舊說法」列成關鍵詞清單，對八份母檔全文 grep**，
  逐一確認每個命中的是「該改的」還是「刻意保留的」。這就是 §2.5 的**收尾自檢**，它不是選配。
- **防呆**：⚠️ 無。收尾自檢目前靠人記得跑。

### E-03 規劃書行數變了但行號對照表沒重算（2026-09-12，`800490d`／`3dd3198`）

- **錯在哪**：`docs/02`、`03`、`05` 檔頭的行號對照表停留在 v2.6（1489 行）時期，
  另一次是落後 9 行的 v2.0 舊基準。**下一個 session 照著讀會讀到錯的章節。**
- **為什麼會錯**：同步鏈第 4 環寫的是「同步導航層」，執行時**只改了內容、沒重算行號**——
  行號是機器事實，改規格的人不會「感覺到」它錯了。
- **下次怎麼避免**：**任何會改動規劃書行數的變更，收尾時逐檔重算行號**，
  並更新 [`00-harness.md`](00-harness.md) §2 與各導航層檔頭的行數標示。
- **防呆**：✅ [`tools/check-linerefs.mjs`](tools/check-linerefs.mjs)（2026-09-20 補上，見本筆第三次紀錄）。

**2026-09-18 同一條再犯一次，改以「重算程序」升級本筆（不另開新號）**

- **錯在哪**：v3.9 縮圖規格的同步鏈第 4 環，用 `re.sub(r'\d+', +delta)` 位移「其他三份規劃書」那張表的最後一欄，
  **連章節編號一起加了**——`1 專案目標 61` 變成 `6 專案目標 66`，`§1.3 總則` 變成 `§6.8 總則`，三列全中。
- **為什麼會錯**：**把「這一欄都是行號」當成前提就直接全域加法**，沒有先看那一欄裡混著章節序號與 `§x.y`。
  位移量算對了（+5／+6），錯的是**套用範圍**。
- **下次怎麼避免**——行號重算固定三步，不要憑眼睛改：
  1. **先取位移量**：`git diff --unified=0 <規劃書>` 讀 `@@` 標頭，算出分段 offset（插入點不只一處時是分段的，不是單一常數）。
  2. **只對「行號欄」套用**，且**逐列確認該欄沒有混入章節序號、`§x.y`、版本號**；混著的欄位一律改用明確字串替換，不用正則。
  3. **抽查三個**：改完隨機挑三個新行號 `sed -n 'Np'` 印出來，確認印到的正是該章節標題。**這一步抓到了本次的錯。**
- **防呆**：✅ 已有腳本（見下）。三步程序仍然有效，腳本是**最後一道關**不是取代。

**2026-09-20 第三次再犯，這次把它寫成腳本（依 `CLAUDE.md` 第 13 條，不另開新號）**

- **錯在哪**：App 規劃書已是 v3.9／1753 行，[`11-mobile-app.md`](11-mobile-app.md) 檔頭仍寫「v3.5，共 1731 行」，
  §2 對照表整欄偏移 18–23 行，§3 分主題導覽甚至用著**更舊的另一套**行號、與 §2 表自相矛盾。
  寫腳本掃過才發現 **[`10-charity-donation-site.md`](10-charity-donation-site.md) 與 [`13-blue-whale-site.md`](13-blue-whale-site.md) 也整份過期**——
  慈善寫 v2.1／780 行（實際 v2.5／806 行）、藍鯨寫 v1.5／414 行（實際 v1.8／429 行），**32 筆全錯，而且沒有人發現。**
- **為什麼會錯**：**「重算行號」是靠人在同步鏈第 4 環記得做的事，而它不痛**——
  行號錯不會讓任何東西壞掉、不會報錯、不會被審查抓到，只會讓下一個 session 讀錯章節。
  前兩次的對策都是**更仔細的人工程序**，而人工程序對治不了「沒有人想起要做」。
  更關鍵的是：前兩次只檢查了「這次改到的那份」，**沒有人回頭掃過全部四份規劃書的對照表**。
- **下次怎麼避免**：不靠記得。**同步鏈第 4 環結束前跑 `node docs/tools/check-linerefs.mjs`**，非 0 就是沒做完。
- **防呆**：✅ [`tools/check-linerefs.mjs`](tools/check-linerefs.mjs)——檢查三件事：
  ① `docs/` 宣告的規劃書版本與總行數 ② 對照表每列的起始行是否真的是該章節標題 ③ 區間是否合法。
  ⚠️ **對應不到章節標題的列會列為「略過」並計數**——**略過數變多本身就是警訊**，
  代表對照表的寫法正在偏離可被機器檢查的形式，不是腳本壞了。

### E-04 header 一條錯連結＝全站 72 頁同時壞（2026-09-03，`20142c3`）

- **錯在哪**：header 的「註冊」指向從未建立的 `/zh/member/register/`，**72 頁全帶著同一條死連結**；
  掃全站後另外找到三條（夏令營／冬令營／專項訓練）。
- **為什麼會錯**：**共用區塊（header／footer／partials）的錯誤會複製到每一頁**，
  但人工檢查是「一頁一頁看」，看第一頁沒發現異狀就過了。
- **下次怎麼避免**：改共用區塊後**一律跑全站建置與連結檢查**，不要只看單頁。
- **防呆**：✅ `site/verify.mjs` 已加站內斷鏈檢查（逐頁比對 `href` 在 `dist` 是否存在）。
  ⚠️ **前台改 Nuxt 後這項檢查必須移植**，不得隨舊骨架一起丟掉（見 §2.5 第 5 環）。

### E-05 檢查腳本自己抓錯，長期回報「待補 0 處」（2026-09-03，`c9983de`）

- **錯在哪**：`verify.mjs` 的待補統計只抓 `class="pending"`，但全站實際用的是 `pending-cell`／`pending-inline`，
  **一直回報 0 處，真實是 28 處**。
- **為什麼會錯**：**把「檢查通過」當成「沒問題」**。統計類的檢查回報 0，可能是真的 0，也可能是它沒抓到東西。
- **下次怎麼避免**：**新增任何統計或檢查，先餵一個已知會命中的樣本確認它抓得到**；
  日後看到「0 處」「無問題」這種漂亮數字，先確認檢查本身還有效。
- **防呆**：✅ 已改為比對整個 `pending-*` 家族。**規則本身仍需人記得**。

### E-06 後台模組樹漏列廣告三項，中英雙版一起漏（2026-09-12，`800490d`）

- **錯在哪**：後台架構總覽的模組樹**先前中英版都漏列**廣告三個子模組。錯誤在譯本裡被忠實複製。
- **為什麼會錯**：**英文版是照中文版翻的，中文版漏了什麼，英文版就漏什麼**——
  「兩份都這樣寫」因此完全不能拿來當作正確的佐證。
- **下次怎麼避免**：清單類內容（模組樹、代號表、欄位表）**以代號序列檢查完整性**
  （`E1`–`E6` 有沒有空號），不要靠比對中英兩版。
- **防呆**：⚠️ 無。

### E-07 客戶版 PDF 落後於規劃書（2026-09-10，`ed4055d`）

- **錯在哪**：規劃書已到 v3.2，客戶版與站台地圖的 PDF 還停在 v2.0／v1.5——**會員卡仍畫成「一張卡通兩隊」**，
  正是 v3.0 已經推翻的規格。客戶手上拿到的會是錯的。
- **為什麼會錯**：同步鏈**第 1、2 環做完就收工**，第 3 環（重產 PDF）與客戶版腳本沒跑。
  客戶版是**另外兩支 Python 腳本**產生的，不在「改 md」的手感路徑上，最容易被忘記。
- **下次怎麼避免**：改規格後**逐環對照 §2.5 的七環表打勾**，尤其確認：
  ① PDF 重產　② `output/tools/` 的客戶版腳本是否也需要改。
- **防呆**：⚠️ 無。

### E-08 `CLAUDE.md` 長成第二份規格書（2026-09-14，`8df47f2`）

- **錯在哪**：`CLAUDE.md` 長到 260 行，其中「關鍵事實速查」佔 133 行（51%），
  另有兩段「只記在這裡」的專案記憶。索引層承載了知識，**長到不會被完整讀完，就失去索引的作用**。
- **為什麼會錯**：每次有新事實，**「順手寫進 CLAUDE.md」的阻力最低**——它一定會被讀到。
  單次決策都合理，累積起來違反分層。
- **下次怎麼避免**：新事實一律寫進**導航層對應檔案**；`CLAUDE.md` 只放一分鐘現況、索引、全域規定。
  **超過 150 行就是警訊**，回頭看有沒有東西該下放。
- **防呆**：⚠️ 無（靠行數自覺）。

### E-09 踩雷點編號重複（2026-09-14，`8df47f2`）

- **錯在哪**：[`00-harness.md`](00-harness.md) §5 的編號出現**兩組 12–15**，交叉引用會指到錯的項目。
- **為什麼會錯**：**分批追加清單時各自從自己那段接續編號**，沒看整份的最大號。
- **下次怎麼避免**：追加編號清單前**先看整份的最後一個編號**；
  中途插入用字母尾碼（`10a`、`34b`）而不是重編，避免動到既有引用。
- **防呆**：⚠️ 無。

### E-10 代號順移後舊代號語意改變（2026-09-12，`800490d`）

- **錯在哪**：`E5`–`E7` 順移為 `E4`–`E6` 後，**`E4` 從「商品櫥窗」變成「廣告主與版位管理」**；
  `K5` 也是回收後重新啟用為抽獎名單管理。舊文件寫的 `E4`／`K5` 全部變成錯的，共 103 處。
- **為什麼會錯**：**「刪掉一個代號」與「代號整段順移」的影響範圍差很多**——
  後者會讓既有引用**靜默指向另一個功能**，而不是壞掉，所以不會報錯。
- **下次怎麼避免**：順移代號時，**把每個被改到的代號列出「原意 → 新意」對照**，
  全文 grep 該代號逐一判讀；並在 [`14-invariants.md`](14-invariants.md) 留一條「看到舊寫法一律視為錯誤」。
- **防呆**：⚠️ 無。**注意**：這條防雷註記留在 `docs/`，**不留在規劃書**——
  規劃書只描述現行規格（§2.5「範圍縮減的寫法」）。

**2026-09-18 同一條再現一次（`B` 模組），一併升級本筆**

- **錯在哪**：`B` 模組於 v3.5 重編為 `B1` 頁面／`B2` 新聞／`B3` 首頁編排／`B4` 常見問題／`B5` 慈善／`B6` 媒體專區，
  但 [`03-admin-spec.md`](03-admin-spec.md) 的逐項清單仍寫 `B4 Banner`／`B5 FAQ`／`B6 慈善`（且 `B6` 出現兩次），
  [`02-frontend-spec.md`](02-frontend-spec.md) 兩處也指向舊代號。**同一檔的模組樹是對的、下面的清單是錯的**，六天內沒被發現。
- **為什麼會錯**：改代號時**只更新了模組樹**（那是最顯眼的地方），沒有往下掃同一檔的逐項說明。
  與 `E-02`（只改章節本體）是同一個行為模式，差別只在這次跨的是 `docs/` 不是規劃書。
- **下次怎麼避免**：**代號一改動，立刻對全部 `docs/` 跑一次「代號 → 名稱」對照**：
  `grep -rn "B[1-9]" docs/` 逐行比對模組樹，**樹與清單對不上就是錯**。這個檢查一分鐘，不要省。
- **防呆**：⚠️ 無腳本。可做：寫一支小檢查，把模組樹那行 parse 成對照表，掃 `docs/` 中所有 `代號＋名稱` 的出現並比對。

---

### E-11 該派 agent 的工作自己做掉（2026-09-20）

- **錯在哪**：全域規定第 12 條明訂「**資料表與技術文件→`system-analyst`**」，但這幾件全部自己做：
  `docs/12` 的 §4 資料表總覽、§14 檢核表，`docs/12b` 的 §6 明細與 §11 唯一鍵，
  `docs/12a` 的 14 張 ERD 重繪，以及 **`docs/16` 慈善獨立庫 23 張表的從零設計**。
  最後一項尤其明顯——那不是搬運既有內容，是設計。
- **為什麼會錯**：**把「我自己做比較快」當成可以覆蓋規定的理由**。
  推理過程是「派出去我還是得逐行核對才敢信，不見得更快」——這個判斷本身不一定錯，
  但**第 12 條沒有這個例外，而我沒有提出來讓使用者決定，是自己把例外給了自己**。
  觸發點是使用者在 B-12 說過「不要想太複雜」，我把它過度推廣成「所有工作都別派 agent」——
  **那句話針對的是單一決策的分析深度，不是派工方式。**
- **下次怎麼避免**：**第 12 條的判準改成看「產出的性質」不是「我做得動嗎」**——
  從零設計資料表、ERD、架構一律派；純粹照規劃書逐條搬運可自理。界線已寫進第 12 條。
  **覺得規定不適用時，說出來讓使用者決定，不要自己判進判出。**
- **防呆**：⚠️ 無。這一條靠的是動手前問自己「這是搬運還是設計」——**是設計就派**。

---

### E-12 `docs/17` §1 Redis healthcheck 片段是 exec form，變數不會展開（2026-09-20，S0-7a）

- **錯在哪**：`docs/17-deployment.md` §1「Redis 怎麼裝」原文的 healthcheck 寫
  `["CMD", "redis-cli", "-a", "$$REDIS_PASSWORD", "ping"]`。Compose 的 `CMD`（exec form）不經過 shell，
  `$$REDIS_PASSWORD` 只會被 compose 自己的變數轉譯規則轉成字面上的 `$REDIS_PASSWORD`，
  容器內沒有任何東西會把它換成真正的密碼——healthcheck 會一直判定不健康，但不會報出明顯錯誤，
  是那種「不會壞、只會悄悄一直紅燈」的坑，任務指示要求「直接抄」這段時才被實際套用後發現。
- **為什麼會錯**：**寫這段 compose 片段時沒有實際跑過一次**，只是憑對 Docker healthcheck 語法的印象寫。
  `CMD` vs `CMD-SHELL` 的變數展開差異不直覺，容易在紙上看起來合理。
- **下次怎麼避免**：**compose／Dockerfile 裡任何帶變數展開的指令片段，寫完至少過一次語法檢查
  或最小可行測試**（哪怕只是 `docker compose config` 或手動跑一次那行指令），不要只憑印象過稿。
- **防呆**：✅ 已修正 `docs/17` 本體與本次新增的 `docker-compose.yml`／`docker-compose.dev.yml` 一併改用
  `CMD-SHELL`。無自動掃描這類「未經測試的指令片段」，仍要靠動手時多驗一步。

---

### E-13 新檔名憑空造出第三套環境（2026-09-21，S0-7a 後續）

- **錯在哪**：`docs/17` §10 規劃「正式網址到位前的暫時設定」時，產出了
  `docker-compose.staging.yml` ＋ `deploy/Caddyfile.staging` ＋ `STAGING_BASIC_AUTH_*` 三組東西。
  **功能本身沒有錯**（同一台 VM、同一批容器，只是 proxy 多掛一道 Basic Auth），
  但同一批交付裡的 [`20-cicd.md`](20-cicd.md) §1 明文寫著「**不建持久 staging 環境**」——
  兩份文件擺在一起看，讀者第一眼看到的是一個名為 staging 的 compose 檔，
  結論會是「原來還是有 staging」。使用者一句「正式機不會有 staging，就是本機開發跟正式 VM 而已」
  當場點破。當時甚至已經察覺到要在 §10 開頭寫一整段「這不是另一套基礎設施」來澄清——
  **需要寫一段話去澄清一個命名，就是命名本身錯了**。
- **為什麼會錯**：**用「這是 Docker Compose 的慣例做法」取代了「這個專案只有幾套環境」的事實核對**。
  `docker-compose.<env>.yml` 是業界通用寫法，套上去很順手，於是沒有回頭問一句
  「本專案到底有幾套環境？這個檔名會讓人以為有幾套？」——也沒有翻開同一批交付的 `docs/20` §1
  對照。這是 `E-02`（改一處、他處殘留矛盾）的變形：這次矛盾的不是殘留的舊敘述，而是**新造的檔名**。
- **下次怎麼避免**：**新增任何 `docker-compose.*.yml`、`Dockerfile.*`、`.env.*`、`*.config.*`
  這類「用字尾命名環境」的檔案前，先回答「本專案有幾套環境、分別叫什麼」**。答案在
  [`14-invariants.md`](14-invariants.md)。如果新檔名不在那份清單裡，**不是加檔案，是改用既有
  環境的參數**（本例：`.env` 的 `CADDYFILE` 變數）。**階段不是環境**——同一套環境的不同階段，
  用值切換，不用檔案切換。
- **防呆**：✅ 「只有兩套環境」已寫進 [`14-invariants.md`](14-invariants.md)，動手前必掃。
  ⚠️ 無自動檢查；`docs/17` §10.8 保留一列刪除記錄，避免日後有人「補回」這個檔案。

### E-14 誤信 parse5 `onParseError` 會抓到孤立結束標籤／標籤未閉合（2026-09-21，S0-9d）

- **錯在哪**：寫 `site/tools/check-wellformed.mjs`（HTML 良構性掃描）第一版時，設計是「用 parse5 解析、
  靠 `onParseError` callback 回報孤立結束標籤與標籤未閉合」。實測發現**完全不會 fire**——連
  `<main>...</main></main>`（就是 2026-09-20 切片踩到的那個孤立 `</main>` 回歸案例）這種明確案例都沒有錯誤回報。
  原因是 HTML5 規格把「body 範圍內的孤立結束標籤」「標籤被隱性關閉」定義為**有明文規範的錯誤復原行為**，
  不算 spec 定義的 parse error——`onParseError` 只回報少數真正的 parse error（例如缺 `<!DOCTYPE>`、
  `<head>`／`<template>` 範圍內結束標籤不成對），這與「瀏覽器會默默修好，但 Vue 編譯器會失敗」這一類
  問題幾乎不重疊。
- **為什麼會錯**：**把「parse5 是忠實的 HTML5 解析器」直接等同於「parse5 會回報所有跟規格寫法不同的地方」**，
  沒有在動手前用最小案例先驗證這個 API 真的會 fire。
- **下次怎麼避免**：**用任何 library 的「錯誤回報」機制之前，先寫 2–3 個已知會觸發的最小案例跑一次確認
  callback 真的被呼叫**，不要只看 API 文件的參數說明就假設行為。本例最後改用「解析後的 DOM 樹裡
  每個元素有沒有 `sourceCodeLocation.endTag`」＋「原始文字裡的字面結束標籤數量 vs 樹上真的被採用的
  結束標籤範圍」兩個自製訊號取代，兩者都先用合成案例驗證會 fire 才套到真實的 80 頁。
- **防呆**：✅ 已寫進 `site/tools/check-wellformed.mjs` 檔頭註解（「偵測方法」段），並留 3 個自我測試案例
  （孤立結束標籤／標籤未閉合／乾淨檔案）可重跑驗證，見 `site/tools/README.md`。⚠️ 沒有 CI 自動跑這些
  自我測試，仍要靠人在改這支工具時手動重跑。

### E-15 種子腳本的「冪等判斷」用錯了父列，partial failure 後永遠補不回子列（2026-09-21，S0-6c）

- **錯在哪**：`db/seed/generate-club-seed-sql.py` 第一版種子腳本，`locales` 語系主檔忘了先種，
  導致 `clubs_i18n`（FK 指向 `locales(code)`）第一次執行時因 FK 違反而整批失敗。但**同一個 GO 批次裡
  先執行的 `INSERT INTO clubs` 已經照 autocommit 語意成功寫入**（沒有交易包住）。修好 `locales` 之後
  重跑，腳本的冪等判斷是「`SELECT id FROM clubs WHERE code = 'tcrfc'`，查到就跳過整個區塊」——
  查到了（上次失敗前已經插入），於是**連同本來就該重跑的 `clubs_i18n` 兩筆一起被跳過**，
  台中磐石的中英文名稱因此在資料庫裡憑空消失、且無論重跑幾次都不會自己補回來。
- **為什麼會錯**：**冪等判斷用來代表「這個區塊做完了」的那一列，跟這個區塊實際要保證存在的東西
  不是同一組**——`clubs` 那一列存在，不代表 `clubs_i18n` 的兩列也存在；partial failure 就是專門
  製造這種「父列有、子列沒有」的中間狀態的场景，而冪等檢查只看了父列。
- **下次怎麼避免**：**「IF NOT EXISTS 才 INSERT」這種冪等寫法，必須讓被檢查的存在性和被保護的寫入
  範圍完全對齊**；只要一個區塊會寫多張表，就要嘛把檢查條件擴大到涵蓋所有子列，要嘛把整個區塊包在
  一個交易裡——**寧可整組回滾重來，不要留下半殘狀態讓「重跑」變得不可靠**。
- **防呆**：✅ 已修正 `db/seed/generate-club-seed-sql.py` 的 `block()`：任何符合
  「`IF ... IS NULL BEGIN ... END`」防呆插入樣式的區塊，自動包一層 `BEGIN TRANSACTION`／
  `COMMIT TRANSACTION`，並在檔頭加一句 session 層級的 `SET XACT_ABORT ON;`（跨 `GO` 批次仍然有效，
  不像變數會被清空）——任何一句 `INSERT` 失敗就整組回滾，下次重跑會照冪等邏輯正確地重新嘗試整個區塊，
  不會再卡在「父列有、子列永遠補不回來」的狀態。已用**乾淨重建整個 `tcrfc_club_dev`＋連續套用種子腳本
  兩次**驗證：兩次執行皆零錯誤、第二次執行資料筆數不變（真的冪等）。

### E-16 Vue SFC 註解裡寫出完整的 `script`／`style`／`template` 字面標籤，build 直接壞（2026-09-21，S0-9a）

- **錯在哪**：`apps/web/app/pages/index.vue` 第一版在 `<script setup>` 的註解裡，為了說明原始
  mockup 的行為，逐字寫了 `// 原始頁面用 <script>location.replace('zh/');</script> 做純前端轉址`。
  `npm run build` 直接報 `[@vue/compiler-sfc] <script> and <script setup> must have the same
  language type`。一開始誤判是「`<script setup>` 頂層用了 `await navigateTo()` 讓 setup 變成
  async，跟 Nuxt 抽取 `definePageMeta` 產生的第二個 script 區塊衝突」，改成具名 middleware
  避開 top-level await後**錯誤訊息一字不變**，才發現真正原因是那行註解——Vue SFC 的區塊切分
  是對**原始檔案文字**做標籤比對，不是先做 JS 語法分析再看「這在註解裡」；註解裡完整寫出
  `<script>...</script>` 這種成對標籤，會被解析成第二個沒有 `lang="ts"` 的 `<script>` 區塊。
  全檔案掃過一輪後，另外在 `app.vue`／`SiteHeader.vue`／`pages/zh/index.vue` 的註解裡也抓到
  4 處類似的字面 `<style>`／`<script setup>`／`<template>` 提及，一併修掉。
- **為什麼會錯**：**把「這段文字在 JS 註解裡」當成解析器也看得懂的語境**，沒意識到 SFC 的
  區塊切分發生在 JS 語法分析**之前**、是純文字層級的標籤掃描。這與 `docs/18` 既有的
  「Vue 模板裡的原始 script／style 會被逃逸」是同一族的坑（Persistent Agent Memory 裡也記過
  `<template>` 內的版本），但這次是**在 `<script>` 區塊的註解裡**踩到，範圍更大——連
  「純粹用文字描述某段程式碼長什麼樣子」的註解都算數。
- **下次怎麼避免**：**.vue 檔的任何地方（含註解、含字串常值）都不要寫出完整的
  `<script`／`<style`／`<template` 加對應 `>` 的字面文字**；要描述這些標籤時一律用中文詞彙代替
  （「script 標籤」「樣板區塊」），或至少拆開成不構成合法標籤開頭的片段。動手搬遷別的
  mockup 頁面、註解裡要引用原始 HTML 片段時，先掃一次有沒有這三個字首。
- **防呆**：⚠️ 目前無。可以加一條 CI 檢查（grep `.vue` 檔案是否含有裸的
  `<script>`／`<style>`／`<template>` 完整標籤字樣落在 `<script setup>` 或註解裡），
  留給 S0-9 把 `verify.mjs` 六項檢查移植為 lint／test 時一併補上。

### E-17 `@nuxtjs/seo` 的 `nuxt-seo-utils` 子模組會蓋掉元件層 `useHead` 設的 `<html lang>`（2026-09-21，S0-9a）

- **錯在哪**：`app/app.vue` 一開始在動態 `useHead(() => ({ htmlAttrs: { lang: 'zh-Hant', ... } }))`
  裡設 `lang`，實測輸出永遠是 `lang="en"`。追進 `node_modules/nuxt-seo-utils/dist/runtime/app/logic/
  applyDefaults.js` 才發現它自己也對 `htmlAttrs.lang` 呼叫一次 `useHead`（依
  `site.defaultLocale`／`currentLocale` 解析，預設回退 `'en'`），且原始碼註解明講
  `tagPriority: 'low'`「give nuxt.config values higher priority」——但實測 `htmlAttrs`／
  `bodyAttrs` 的合併是**依註冊順序、同一個 key 後蓋前**，不像一般 `<meta>` 標籤走
  `tagPriority` 去重比大小；即使把自己那次 `useHead` 呼叫也加上 `tagPriority: 'high'` 一樣
  蓋不掉。也試過在 `nuxt.config.ts` 設 `site.defaultLocale: 'zh-Hant'`，但沒能定位到它實際
  怎麼沒被讀到（`nuxt-site-config` 用 stack／priority 機制解析，沒有再往下追）。
- **為什麼會錯**：**看到 `tagPriority: 'low'` 的原始碼註解就假設整個 head 合併系統都遵守
  同一套優先權規則**，沒有針對 `htmlAttrs`／`bodyAttrs` 這種「整個物件按 key 合併」的特例
  另外驗證。
- **下次怎麼避免**：**`<html>`／`<body>` 的屬性合併，改用 `nuxt.config.ts` 的
  `app.head.htmlAttrs`／`app.head.bodyAttrs` 設定，不要指望元件層 `useHead` 的
  `tagPriority` 能贏過模組自己註冊的預設值**——這是 `nuxt-seo-utils` 自己的原始碼註解
  真正指的「nuxt.config values」，親測有效。全站固定不隨 club 變動的屬性（本例是
  `lang`，兩個俱樂部都是 `zh-Hant`）放這裡；只有真的隨 club 變動的屬性（`data-club`）
  才留在 `app.vue` 的動態 `useHead`。
- **防呆**：✅ 已改為 `nuxt.config.ts` 的 `app.head.htmlAttrs.lang = 'zh-Hant'`，
  `apps/web/app/app.vue` 只保留 `data-club`，並在兩處都留了逐字說明理由的註解，
  下一個接手的人不會想「應該用 useHead 設就好」重踩一次。

### E-18 `@nuxtjs/sitemap` 的 runtime 動態來源在單一 build／多容器情境下沒被偵測到（2026-09-21，S0-9a，⚠️ 尚未解決）

- **錯在哪**：`nuxt.config.ts` 先後試過 `sitemap.urls`（行內函式）與
  `sitemap.sources: ['/__sitemap__/urls']`（獨立 server route），`npm run build` 都印
  `[@nuxtjs/sitemap] No dynamic sources detected`，實際請求 `/sitemap.xml` 永遠是空的
  `<urlset></urlset>`——即使直接呼叫那支來源 route（`curl /__sitemap__/urls` 或改名為
  `server/api/__sitemap__/urls.ts` 走官方零設定慣例）都能正確回傳依 `isUnitEnabledForClub`
  過濾過的網址清單。研判：這個模組會在**建置階段**先求值一次來源（此時還沒有真正在跑的
  HTTP server 可以回應 request-scoped 的 `NUXT_PUBLIC_CLUB`），把空結果寫進 `.output` 的
  靜態資產快取，之後每個 request 都回放那份快取，不會因為 runtime 環境變數不同而重新求值。
- **為什麼會錯**：本專案「一份 build、兩個容器各帶不同 `NUXT_PUBLIC_CLUB`」是刻意的架構決定
  （`docs/13` §6 紀律 7、8 的同一個精神延伸到 club），但 `@nuxtjs/sitemap` 預設的效能優化
  假設是「同一份 build 的 sitemap 內容不會因為 runtime 環境變數而不同」，兩者互相衝突，
  沒有在選型時就查證這個模組的動態來源判定邏輯是否真的支援 runtime-env-driven 的網址清單。
- **下次怎麼避免**：之後若要在同一份 build、runtime 才決定內容的情境下用這個模組的自動
  sitemap.xml 產生功能，要先查 `nuxt-seo` 官方文件的 zero-runtime／dynamic sources 章節
  確認求值時機，或乾脆放棄用模組產生 `/sitemap.xml`，改寫一支 Nitro server route 直接組
  XML（來源仍是同一個 `getEnabledSiteUnits(club)`，不必依賴模組猜對）。
- **防呆**：⚠️ 無，本項尚未解決。**單元開關呼叫點 3（sitemap）目前只有
  `server/api/__sitemap__/urls.ts` 這個資料端點本身正確**（已用 curl 驗證 tcrfc／bw 兩邊
  過濾結果正確），`/sitemap.xml` 的最終輸出還是空的。留給 S0-9 完整搬遷、真的需要
  sitemap.xml 生效時處理，處理前 `/sitemap.xml` 不得被視為已完成。

### E-19 `apps/api` 開了 `InvariantGlobalization`，`Microsoft.Data.SqlClient` 連線直接炸（2026-09-21，S0-10 API 骨架）

- **錯在哪**：`Tcrfc.Api.csproj` 建專案時順手加了 `<InvariantGlobalization>true</InvariantGlobalization>`
  （想法是體積小、啟動快，常見於不碰在地化的後端服務範本）。本機起 API 打 `/readyz`，
  `SqlConnection.OpenAsync()` 在 `TryOpen` 階段直接丟 `System.NotSupportedException:
  Globalization Invariant Mode is not supported`——`Microsoft.Data.SqlClient` 的連線與定序處理
  依賴完整 ICU，開了不變全球化模式連一條連線都開不了，不是「某些在地化功能不能用」這種可接受的降級。
- **為什麼會錯**：**套用了「後端 API 常見最佳化範本」，沒有針對本專案實際用的套件
  （`Microsoft.Data.SqlClient`）查證相容性**——`InvariantGlobalization` 對純 HTTP／JSON 服務通常安全，
  但只要相依鏈裡有需要定序或編碼轉換的資料庫驅動，就是地雷。沒有先問「這個旗標對我實際引入的
  NuGet 套件有沒有已知不相容」，就把它當成無風險的效能選項加了進去。
- **下次怎麼避免**：**加 `InvariantGlobalization`（或任何 trimming／AOT 類體積優化旗標）之前，
  先確認專案的資料庫驅動與其他相依套件是否明文支援不變全球化模式**——`Microsoft.Data.SqlClient`
  的文件與已知 issue 都有記載這條限制。不確定就先不開，等有實際的啟動時間／映像檔體積數字
  證明有必要，再回頭針對性驗證。
- **防呆**：✅ 已移除該屬性並在 `Tcrfc.Api.csproj` 留下說明性註解；本次已用本機
  `dotnet run` 實測連線成功作為回歸依據（見本次任務回報）。⚠️ 無自動化測試防止有人日後重新加回去，
  仍要靠 code review 與這筆記錄。

### E-20 Dapper 的 record 建構子具現化對型別與欄位數要求比預期嚴格（2026-09-21，S0-10 API 骨架）

- **錯在哪**：兩個獨立但同根源的失敗。① `PlayersRepository.PlayerRow`／`MatchesRepository.MatchRow`
  把 `birth_on`／`match_on`（SQL `date` 型別）對應的屬性宣告成 `DateOnly?`／`DateOnly`，
  `GET /api/v1/{club}/players`／`.../schedule` 一律 500，例外訊息是
  `A parameterless default constructor or one matching signature (... System.DateTime BirthOn ...)
  is required`。② `ArticlesRepository.GetBySlugAsync` 內的 `i18nSql` 只 SELECT 5 欄
  （`locale, title, summary, seo_title, seo_description`），但拿去具現化的
  `ArticleI18nRow` 建構子有 6 個參數（多一個 `ArticleId`，是給列表查詢那個批次版本用的）——
  同一種例外，但根因是「兩個查詢共用了一個只有一邊欄位對得上的型別」。
- **為什麼會錯**：① **假設 Dapper 對 C# 的 `DateOnly`（.NET 6 引入）有「跟屬性型別走」的自動轉換，
  沒有意識到它的 record 具現化路徑是照 `IDataReader.GetFieldType()` 回報的實際 CLR 型別去比對建構子
  參數型別**——而 ADO.NET／`Microsoft.Data.SqlClient` 對 SQL `date` 欄位回報的一律是 `System.DateTime`
  （TDS 協定沒有原生 date-only 型別），兩者對不上，record 沒有 parameterless 建構子可以退而求其次。
  ② **偷懶共用一個 DTO 型別給兩個「欄位子集不同」的查詢**，圖省事沒有各自宣告精確對應 SELECT 清單的型別。
- **下次怎麼避免**：**Dapper ＋ record 的組合，任何對應 SQL `date`／`datetime` 欄位的屬性一律先宣告
  成 `DateTime`／`DateTime?`，需要 `DateOnly` 語意時在應用層（Map 函式）用 `DateOnly.FromDateTime()`
  轉，不要指望 Dapper 幫忙轉型別。且每個 `QueryAsync<TRow>` 呼叫前，**手動逐一核對 SELECT 的欄位順序
  與數量跟 `TRow` 建構子參數是否完全一致**——record 只有一個建構子時，Dapper 沒有其他退路，
  型別或數量錯一個就整支查詢炸掉，不會是「漏掉的欄位是 null」這種溫和的失敗模式。
- **防呆**：✅ 已修正兩處（`PlayerRow`／`MatchRow` 改 `DateTime`，`ArticlesRepository` 拆出
  `ArticleDetailI18nRow` 精確對應單篇查詢的 7 欄）；已用本機資料庫實際跑過全部端點驗證修復
  （見本次任務回報的 curl 紀錄）。⚠️ 無自動化測試防止同類錯誤再發生，仍要靠這筆記錄與
  「先跑過一次再交付」的紀律（同 E-12 的教訓）。

---

### E-21 發現「必然差異」以外的新差異時自行放行（2026-09-21，S0-9 前台搬遷比對）

- **錯在哪**：Vue SSR 對靜態 `style="..."` 屬性一律補結尾分號、對 `<select>`／`<input>` 的
  `v-model` 顯式印出 `selected=""`／`value=""`——這兩類差異都不在 [`14-invariants.md`](14-invariants.md)
  原本封閉的四類「必然差異」清單裡。兩個 agent 分別遇到時，各自判定「這是必然差異、不影響視覺」
  就直接放行比對關卡，**沒有停下來問**——而 `docs/14` 當時已經明文寫著「發現無法歸類的差異要停下來問，
  不得自行放行」。事後 2026-09-21 由使用者拍板確認這兩類確實無害，正式列入第五、六類，
  但**流程本身是錯的**：判斷結果對，不代表繞過使用者這一步是對的。
- **為什麼會錯**：**把「我自己判斷得出這是無害的」當成「不用問」的理由**——但那條規則的存在
  本來就是為了防止這種情況：判斷正確與否事後才能驗證，**規則設計上就是不該讓執行者自己兼任仲裁者**。
  與 `E-11`（該派 agent 的工作自己做掉）是同一種行為模式的變形（自己把例外給了自己），
  但這次的領域不同（驗收關卡放行，不是工作派遣），**不算同一筆重複**。
- **下次怎麼避免**：驗收關卡（尤其是「一模一樣」這種以腳本判定通過與否的關卡）發現任何不在既有
  封閉清單裡的差異，**一律先停下來回報使用者**，不論當下多有把握這是無害的——把「這是不是必然差異」
  的判定權限收斂在使用者這一層，不是判斷力問題，是誰有權下這個結論的問題。
- **防呆**：✅ 六類差異已直接寫進 [`site/tools/compare-dom.mjs`](../site/tools/compare-dom.mjs)
  的正規化規則，工具會自動排除——**因此往後工具報出的任何差異都是真差異**，不再留給人
  「這個算不算必然差異」的判斷空間。

### E-22 Nuxt 元件放子目錄時標籤名要加目錄前綴，寫錯不報錯也不警告（2026-09-21，S0-9 前台搬遷）

- **錯在哪**：`components/content/MembershipBenefits.vue` 這種放在 `components/` 子目錄的元件，
  Nuxt 的 auto-import 會把它註冊成 `<ContentMembershipBenefits />`；頁面裡若仍沿用檔名寫
  `<MembershipBenefits />`，Nuxt **不認得這個標籤，也不報錯、不警告**，那一整塊內容在 render 時
  悄悄消失。今天的 `MembershipBenefits` 就是這樣被漏用，靠 compare-dom 抓到「一整段 DOM 消失」
  才發現——**目視完全看不出來**，頁面看起來只是少了一塊，很容易被誤判成「內容本來就沒有」。
- **為什麼會錯**：套用了「元件放在 `components/` 頂層時標籤名就是檔名」的直覺，沒有意識到
  **子目錄名會被拼進元件標籤名**是 Nuxt auto-import 的既定慣例，而且這個慣例寫錯時是**靜默失敗**
  不是報錯失敗。
- **下次怎麼避免**：元件放進 `components/` 的任何子目錄，落筆引用前先確認實際標籤名是
  「子目錄名 + 檔名」拼接後的 PascalCase（`components/content/X.vue` → `<ContentX />`），
  不要憑檔名本身猜。
- **防呆**：⚠️ 無。留給 S0-9 補 lint／test 時可考慮加一條掃描：`components/` 底下每個子目錄元件，
  檢查有沒有頁面用「未加前綴」的標籤名引用它。

### E-23 跑 `npm run lint` 前要先跑 `npx nuxt prepare`（2026-09-21，S0-9 前台搬遷）

- **錯在哪**：`.nuxt/link-checker/routes.json` 是建置期產生的路由表快取。頁面搬遷後若沒有重新跑
  `nuxt prepare`，link-checker 仍拿著搬遷前的舊路由表去比對新增頁面裡的連結，會把「目的頁其實
  已經搬過去、只是路由表還沒更新」的連結全部誤判成斷鏈。今天回報的斷鏈數字一度從 118 暴增到 415，
  重跑 `nuxt prepare` 後才降回真實的 65。
- **為什麼會錯**：把 `npm run lint` 當成一個獨立、不需要前置動作的指令，**沒有意識到它依賴的路由表
  是另一個獨立建置步驟的產物**，兩者之間沒有自動連動，改了路由不會自動觸發路由表重算。
- **下次怎麼避免**：任何一輪頁面搬遷或路由變動後，**跑 lint 前固定先跑一次 `npx nuxt prepare`**，
  當成 lint 的必要前置步驟，不是可省的動作。
- **防呆**：⚠️ 無。可考慮把 `nuxt prepare` 併進 `package.json` 的 lint script（前置或直接串接），
  留給 S0-9 補 lint／test 腳本時一併處理。

### E-24 `:style="undefined"` 在 Vue SSR 仍印出 `style=""`，不是省略屬性（2026-09-21，S0-9 前台搬遷）

- **錯在哪**：預期 `:style="condition ? {...} : undefined"` 在 `condition` 為 false 時，Vue SSR
  會完全不輸出 `style` 屬性（等同 mockup 原本沒有這個屬性），但實測 Vue SSR 對 `:style="undefined"`
  仍會印出空字串的 `style=""`——這跟一般屬性「值是 `undefined`／`null` 就省略」的行為不一致，
  導致 compare-dom 抓到「多了一個空 `style` 屬性」的差異。
- **為什麼會錯**：把 `:style` 的 `undefined` 處理直接類比成其他一般屬性（如 `:title="undefined"`
  會省略）的行為，**沒有另外查證 `:style`／`:class` 這兩個物件型綁定的省略規則是否相同**。
- **下次怎麼避免**：需要「條件成立才輸出 style」時改用 `v-bind="condition ? {style:'...'} : {}"`
  的物件展開寫法，不要依賴 `:style="undefined"` 會被省略。
- **防呆**：⚠️ 無，靠這筆記錄與 code review 時留意 `:style` 的條件式寫法。

---

### E-25 賽事卡片錨點 id 誤用 `data-ha` 的編碼函式，`home`／`away` 全字混進本該是單一字母的 id（2026-09-21，S0-9 場次編號補齊）

- **錯在哪**：`app/utils/schedule.ts` 的 `fixtureId()` 補 `match_no` 時直接呼叫既有的 `haCode()`
  （回傳完整單字 `home`／`away`，給 `data-ha` 屬性用），讓錨點 id 變成
  `fx-2026-09-13-away-3`；mockup 實際是 `fx-2026-09-13-a-3`——id 裡的主客場是單一字母
  `a`／`h`，跟 `data-ha` 的完整單字是兩套獨立的編碼，不是同一個值的兩種寫法。
- **為什麼會錯**：看到「這頁已經有一個『主客場轉字串』的函式」就直接重用，**沒有先把 mockup
  的 21 個 id 逐一列出來跟自己要組出來的字串比對**，等寫完程式碼才用 curl 抓渲染結果比對才發現
  對不上——任務指示本來就明講「先去 `site/dist/.../schedule/index.html` 逐一核對 mockup 實際
  的 21 個 id 長什麼樣，不要憑描述猜格式」，但「查過 id 格式」跟「查過『同一頁裡是不是已經有
  一個看起來很像、但語意不同的編碼函式』」是兩件事，只做了前者。
- **下次怎麼避免**：組字串型的識別碼（id、slug、key）如果打算重用既有函式，要先確認那個函式
  的**呼叫端**（誰在用、用在哪個屬性）是不是同一個語意，不能只看函式名稱「看起來合用」。
- **防呆**：✅ `compare-dom.mjs` 一定會抓到——`id` 屬性差異不在六類必然差異清單內，一律回報成
  真差異；本次是在跑 `compare-dom.mjs` 之前先用 `curl | grep 'id="fx-'` 逐一核對才自己抓到。

---

### E-26 客戶照片換了路徑，`.gitignore` 沒跟著換——差一步推上公開 repo（2026-09-21，S0-9 搬遷）

- **錯在哪**：`.gitignore` 有一條「客戶照片：從收件夾轉檔的衍生物，**含未成年學員肖像**，
  授權未逐項確認前不納入有遠端的版控」，指向 `site/src/assets/img/`。S0-9 搬遷時因為
  Nuxt 的 `<img src="/assets/...">` 會被 Vite 編譯成 import、指到不存在的檔案就是 build-time
  硬錯誤，搬遷者用 `rsync` 把整份 `site/src/assets/{img,brand}` 複製進
  `apps/web/public/assets/`（159 檔／59MB）。**新路徑不在任何忽略規則裡**，`git status`
  把 199 個檔案列為待加入；若照常 commit＋push，這批照片就會進入 **公開的** GitHub repo。
  提交前做例行檢查才攔下來。
- **為什麼會錯**：忽略規則寫的是**路徑**，但它要保護的是**內容**（CLAUDE.md 第 7 條的個資與
  肖像權）。搬檔案的人關心的是「build 過不過」，不會意識到自己同時把一條安全規則的作用範圍
  繞開了——**規則綁在舊路徑上，檔案一搬，保護就自動失效，而且沒有任何東西會出聲**。
  搬遷任務的指示裡也沒有一條「複製任何客戶素材前先確認忽略規則涵蓋目的地」。
- **下次怎麼避免**：複製或移動**任何**客戶素材（照片、名單、證書、收件夾內容）到新位置時，
  **先跑 `git check-ignore -v <新路徑>` 確認涵蓋，再動手**。派工給 agent 時，凡是會搬動素材的
  任務都要在指示裡明寫這一條。
- **防呆**：✅ `.gitignore` 已補上 `apps/web/public/assets/img/`，並在註解裡寫明「上面那條規則
  寫的是舊路徑，檔案換位置時規則不會自己跟過去」，讓下一個讀到的人知道這裡有過一次事故。
  ⚠️ **仍無自動化**——沒有東西會在「新增大量圖檔」時主動示警。**這是目前最值得寫成 pre-commit
  掛鉤的一條**：掃描待提交檔案裡有沒有 `*.jpg`／`*.png` 落在 `assets/img` 這類路徑下。

---

### E-27 `.gitignore` 的 bin／obj 忽略規則是單層萬用字元，同目錄子專案的建置產物擋不到（2026-09-21，S0-7d）

- **錯在哪**：`.gitignore` 原本寫 `apps/*/bin/`／`apps/*/obj/`——`*` 在 gitignore 語法裡不跨越 `/`，
  只能匹配 `apps/<一層>/bin/`（例如 `apps/api/bin/`）。S0-7d 在 `apps/api/` 底下新增了**同目錄的
  獨立子專案** `apps/api/Tcrfc.Api.Tests/`（沒有 `.sln` 把兩者的建置範圍切開，見本檔案 E-19 之後
  同一個 session 的另一個坑），它的建置產物落在 `apps/api/Tcrfc.Api.Tests/bin/`，多了一層路徑，
  舊規則完全擋不到。`git status` 一度把整包 xunit／`Microsoft.Data.SqlClient` 等第三方 DLL
  （478 個檔案）列為待加入，提交前用 `git add -n apps/api/Tcrfc.Api.Tests/` 核對才發現。
- **為什麼會錯**：寫 `.gitignore` 規則時心裡想的是「`apps/` 底下每個應用程式一層」這個當時成立的
  目錄結構假設，沒有把「新專案可能巢狀在既有應用程式目錄底下」這個之後才出現的情況算進去——
  跟 E-26 是同一個根因家族：**規則綁定在特定的目錄深度假設上，結構一變，規則就悄悄失效**，
  而且沒有任何東西會出聲提醒。
- **下次怎麼避免**：`.gitignore` 裡涉及建置產物（`bin/`／`obj/`／`dist/`／`.nuxt/` 這類）的規則，
  預設一律用 `**`（任意深度）而不是 `*`（單一層），除非有明確理由需要限制層數。新增任何巢狀專案
  （測試專案、子模組）之後，跑一次 `git add -n <新專案目錄>` 確認沒有 `bin/`／`obj/` 被列進去。
- **防呆**：✅ 已改用 `apps/**/bin/`／`apps/**/obj/`，並用 `git check-ignore -v` 對兩層深度
  （`apps/api/bin/...`、`apps/api/Tcrfc.Api.Tests/bin/...`）都驗證過涵蓋。⚠️ 仍無自動化——
  跟 E-26 一樣，這類「規則涵蓋範圍隨結構改變而失效」的錯，目前都靠人手動跑 `git add -n` 核對，
  沒有 pre-commit 掛鉤主動示警。

---

### E-28 後台模組數量寫成 15，實際是 14——把獨立後台的慈善模組算了進來（2026-09-21，後台介面版面決策）

- **錯在哪**：[`STATUS.md`](../STATUS.md)「要做的是這五個」表格寫「共用後台 Admin：一個入口＋站台切換器，
  **15 個模組字母**」。實際數規劃書 §4.0 與 [`03-admin-spec.md`](03-admin-spec.md) §1 的模組樹，
  一級模組是 `A B C P E F G H I J K L M S` 共 **14 個**。多出來的那一個最可能是慈善捐款平台的 `N`
  ——但規劃書 §4.0 明文「**慈善捐款平台是獨立後台與獨立資料庫，不在本後台的模組之列**」，
  而 `STATUS.md` 同一張表裡慈善捐款平台本來就另外列為第 4 個平台，等於同一個模組被算了兩次。
  由 `visual-design-architect` 在做後台版面決策、實際逐一點名模組時發現。
- **為什麼會錯**：模組字母的數量是**人工維護的彙總數字**，而它的來源（模組樹）改過好幾次——
  `D1–D4` 改編為 `P1–P4`、`N` 移出成獨立後台、`S` 商店在 v2.6 新增。
  **每次改的是樹，沒有人回頭重數那個數字**，而數字寫在另一份檔案裡，不會因為樹改了就出錯或報警。
  跟 [E-03](#e-03) 是同一個根因家族：**衍生數字與它的來源分處兩份檔案，來源變動時衍生值悄悄過期**。
- **下次怎麼避免**：文件裡要寫「共 N 個」這種彙總數字時，**在同一句話裡把 N 個是哪些一併列出**
  （本次已改成「14 個模組字母（`A B C P E F G H I J K L M S`；慈善的 `N` 是獨立後台不算在內）」）。
  列出來之後，數字與清單對不上是**肉眼就看得到的矛盾**，不需要跨檔案核對才發現。
- **防呆**：⚠️ 無自動化。已把清單與數字寫在一起降低再犯機率，但沒有任何腳本會在模組樹變動時
  重新核對這個數字。

---

### E-29 `nginx-spa.conf` 的 `X-Robots-Tag` 只寫在 server 層級，被每個 location 自己的 add_header 蓋掉，`noindex` 沒有真的送出（2026-09-21，S0-12 後台外殼）

- **錯在哪**：`apps/admin/nginx-spa.conf`（S0-7a 階段建立的骨架檔案）把
  `add_header X-Robots-Tag "noindex, nofollow" always;` 放在 `server {}` 區塊最外層，
  但 `location ~* \.(js|css|...)$` 與 `location /` 兩個實際會回應內容的 location 各自都有自己的
  `add_header Cache-Control ...`。nginx 的 `add_header` 繼承規則是「子層級只要宣告了任何一個
  `add_header`，就完全不繼承上層的整組 `add_header`」，不是逐條疊加。本次用 `docker build` 建出映像檔、
  `docker run` 起容器、`curl -I` 實測首頁與一個真實的 `/assets/*.js`，**兩者的回應都沒有
  `X-Robots-Tag`**，等於 CLAUDE.md 全域規定第 5 條「`noindex` 不要拿掉」在這個容器裡從建置完成的
  第一天就沒有生效，一直沒被發現是因為先前的階段只做到「這個 Dockerfile 待 `apps/admin` 建立後才能
  build」，沒有人實際 `docker run` 過去 `curl -I` 驗證過標頭。
- **為什麼會錯**：寫這份 nginx 設定時，心裡的模型是「`add_header` 像 CSS 一樣會逐層疊加」，
  但 nginx 官方文件明講的行為是「同層級沒有自己的 `add_header` 才會繼承上層，一旦自己宣告了，
  上層整組作廢」，跟直覺不符，而且**這種不生效不會有任何錯誤或警告**——伺服器照常回 200，
  頁面內容完全正常，只有標頭悄悄不見，光看畫面或看 `curl` 的 body 完全看不出來，一定要專門
  `curl -I` 看標頭或用瀏覽器開發者工具的 Network 分頁核對才抓得到。
- **下次怎麼避免**：任何 nginx 設定只要在 server 層級與 location 層級都用了 `add_header`，
  就把 server 層級那個假設當作「不會生效」，直接把需要的標頭複製到每一個有自己 `add_header` 的
  location 裡；寫完新的 nginx 設定，**建置映像檔、實際 `docker run` 起來、對每一種會回應內容的路徑各
  跑一次 `curl -I` 核對關鍵標頭**（這裡是 `X-Robots-Tag`；其他專案可能是 CSP、CORS 等），
  不能只驗證 `docker build` 成功或頁面內容正確就視為過關。
- **防呆**：✅ 已修正——`X-Robots-Tag` 複製進兩個 location 各自的 `add_header` 清單，並用
  `docker build` + `docker run` + `curl -I` 對首頁與一個真實的 hash 檔名 JS 資源都驗證過標頭存在。
  ⚠️ 仍無自動化：這個修正只覆蓋了現在的兩個 location，之後如果再新增 location（例如給某個路徑另開
  快取規則），一樣要記得複製 `X-Robots-Tag` 進去，沒有 CI 檢查會主動提醒。

---

### E-30 `structuredClone()` 直接對 Vue `reactive()`／`ref()` 包出來的 Proxy 呼叫會丟 `DataCloneError`（2026-09-21，S0-12 新聞編輯頁）

- **錯在哪**：`NewsEditView.vue` 一開始寫
  `const baseline = ref<NewsArticle>(structuredClone(existing ?? createEmptyArticle()))`，
  `existing` 是從共用的 `newsStore`（一個 `reactive()` 陣列）裡 `find` 出來的項目——Vue 對
  `reactive()` 物件的陣列做屬性存取時，回傳的元素本身就是被包過的 reactive Proxy。瀏覽器原生
  `structuredClone()` 無法複製 Proxy，執行到這行直接在瀏覽器主控台丟出
  `DataCloneError: Failed to execute 'structuredClone' on 'Window'`，導致整個 `setup()`
  中斷、頁面渲染不出任何內容（表現為「新增文章」頁一片空白、`h1` 都抓不到），但 `npm run build`／
  `vue-tsc` 型別檢查與 ESLint **完全不會發現這個問題**——這是純執行期的瀏覽器 API 行為，只有
  實際在瀏覽器（或無頭瀏覽器）打開頁面才會踩到。是用 CDP 起無頭 Chrome、監聽
  `Runtime.consoleAPICalled`／`Runtime.exceptionThrown` 事件才抓到的，單純看 `curl` 或截圖
  （頁面回 200、DOM 存在但是空的）不會直接顯示原因。
- **為什麼會錯**：寫的時候把 `structuredClone` 當成「深拷貝任何 JS 物件」的萬用工具，忽略了
  Vue 3 的 `reactive()`／把物件放進 `ref()` 都會回傳 Proxy 包裝過的值，而 Proxy 不在
  `structuredClone` 支援的可複製型別清單內（一般物件、陣列、Map、Date 等可以，Proxy exotic
  object 不行）。这是「兩個各自成立的假設疊在一起才會爆」的典型：單獨看 `structuredClone` 沒問題、
  單獨看 Vue reactivity 也沒問題，但「拿 reactive 來源的資料去 structuredClone」這個組合會爆，
  而且爆的時間點是執行期，型別系統看不出來（`NewsArticle` 型別本身沒有變，TypeScript 不知道
  一個值在執行期被 Proxy 包過）。
- **下次怎麼避免**：任何時候要 `structuredClone()` 一個「可能來自 Vue `reactive()`／`ref()`
  來源」的值之前，先用 `toRaw()`（`import { toRaw } from 'vue'`）拿回原始物件；`ref(obj)`
  本身也會把 `obj` 深層轉成 reactive，所以拿來存「快照、之後要拿去跟目前狀態比對」用途的 ref，
  改用 `shallowRef()`——它只追蹤 `.value` 的重新賦值，不會把賦進去的物件本身也變成 Proxy。
  這份 mockup 沒有後端、資料完全在前端記憶體流轉（`newsStore`），這個模式之後其他模組的編輯頁
  只要也是「從共用 store 讀一筆、複製一份到表單本地狀態」就會重複踩到，寫其他模組編輯頁時要
  记得比照這個修法。
- **防呆**：✅ 已修正（`toRaw()` + `shallowRef`），並用無頭瀏覽器監聽 console 錯誤事件重新驗證
  過新增與編輯兩種模式都不再拋出例外、頁面正常渲染。⚠️ 無自動化：目前沒有測試框架（如 Vitest +
  Testing Library）跑過這個元件的掛載測試，之後若要補測試，「掛載 `NewsEditView` 並斷言沒有
  `console.error`」會是最低成本能攔住這整類錯誤的一條測試。

---

## 3. 目前沒有防呆的項目

E-01／E-02／E-06／E-07／E-08／E-09／E-10／E-11／E-22／E-23／E-24 都還靠人記得。
**E-03 已於 2026-09-20 補上腳本**（[`tools/check-linerefs.mjs`](tools/check-linerefs.mjs)，同步鏈第 4 環結束前必跑）；
**E-21 已於 2026-09-21 補上腳本**（六類必然差異寫進 `site/tools/compare-dom.mjs` 的正規化規則）；
**E-25 本來就受 E-21 的同一支腳本保護**（`id` 屬性差異一律視為真差異），這次是搬遷者在跑
腳本前自行用 curl 核對到的。

🔴 **E-26 沒有自動化防呆，而它的後果是個資外洩不是程式出錯**——最值得優先補的是 pre-commit 掛鉤：待提交檔案若有圖檔落在 `assets/img` 類路徑下就擋下來。

**下一條最值得寫成腳本的是 E-01 的禁用字串掃描**：`Rocks`／`ROCKS`／`Cornerstone`／`D1 課程`／`E4 商品櫥窗`／`MediaAsset`。

兩者都適合在前台改 Nuxt 時，**與 `verify.mjs` 的六項檢查一起移植成專案的 lint／test**
（見 [`00-harness.md`](00-harness.md) §2.5 第 5 環）。

---

| 相關文件 | 用途 |
|---|---|
| [`00-harness.md`](00-harness.md) | §2.5 同步鏈（七環＋收尾自檢）、§5 規格踩雷點 |
| [`14-invariants.md`](14-invariants.md) | 全站不變量，動手前掃一次 |
| [`../STATUS.md`](../STATUS.md) | 進度追蹤 |
