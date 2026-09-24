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
| E-18 | 2026-09-21<br>2026-09-22 | `@nuxtjs/sitemap` 的 runtime 動態來源在「一份 build、runtime 才決定 club」的架構下沒被偵測到，`/sitemap.xml` 永遠空；2026-09-22 查出**真正根因不是動態來源偵測**，是模組把命中全站 `noindex` route rule 的網址整批排除 | ✅ 已改自組 XML（`server/routes/sitemap.xml.ts`），繞過該模組的內建路由 |
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
| E-29 | 2026-09-21 | `nginx-spa.conf` 的 `X-Robots-Tag` 只寫在 server 層級，被每個 location 自己的 `add_header` 蓋掉，`noindex` 沒有真的送出 | ✅ 複製進每個 location，`docker build`＋`docker run`＋`curl -I` 實測過 |
| E-30 | 2026-09-21 | `structuredClone()` 直接對 Vue `reactive()`／`ref()` 的 Proxy 呼叫丟 `DataCloneError`，頁面整片空白 | ✅ 改用 `toRaw()` + `shallowRef` |
| E-31 | 2026-09-21 | 宣稱「對比度全部用公式實測過」，27 組裡 2 組沒驗到／驗錯（含反例數字、漏驗 overlay 層） | ✅ [`apps/admin/scripts/check-contrast.mjs`](../apps/admin/scripts/check-contrast.mjs)，已掛進 `npm run lint` |
| E-32 | 2026-09-21 | 用 CDP 驗證深色 mockup 三斷點時，`/json/new` 在本機 Chrome 153 上只收 PUT，沿用舊版 GET 寫法直接 JSON parse 失敗 | ⚠️ 無 |
| E-33 | 2026-09-21 | Element Plus 沒設語系，分頁器印出 `Total 8`／`20/page` 等英文——違反 §4.0，但禁用詞掃描只看 `.vue` 的 `<template>`，掃不到元件庫自帶文案 | ✅ `check-forbidden-terms.mjs` 加驗 `main.ts` 有設 `locale` |
| E-38 | 2026-09-22 | `BlobImageStorageService` 的「容器已確保存在」旗標在呼叫 `CreateIfNotExistsAsync` **之前**就設成完成，第一次呼叫因故失敗後，旗標仍卡在「已完成」，之後每次呼叫都跳過建立、直接對不存在的容器寫入，得到的錯誤變成「容器不存在」蓋掉了真正的根因 | ✅ 改用 `SemaphoreSlim` 包住整段，`CreateIfNotExistsAsync` 成功後才設旗標 |

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
- **防呆**：✅ 見下方「2026-09-22 二次追查」——已解決，不再是本項的懸案。

#### 2026-09-22 二次追查：真正根因與修法（S0-9 收尾，非本項首次記錄者所猜的原因）

- **原記錄的推測是錯的**：上面寫的「建置階段就求值過一次、空結果被寫進 `.output` 快取」
  只是猜測，**從未實際追查 `@nuxtjs/sitemap` 的原始碼**。這次逐行讀
  `node_modules/@nuxtjs/sitemap/dist/runtime/server/sitemap/nitro.js` 的
  `buildSitemapRenderPlan()` 才找到真正原因：它會對每一筆候選網址呼叫
  `getPathRobotConfig(event, { path, skipSiteIndexable: true })`，**並額外檢查該路徑
  命中的 Nitro route rule 有沒有帶 `X-Robots-Tag` 含 `noindex` 的 `headers`**——
  有的話直接 `continue`（整筆排除），且原始碼裡**沒有任何設定能讓這筆網址被強制留下**
  （`routeRules.sitemap` 只能用來排除，不能用來強制保留）。本站 `nuxt.config.ts` 對
  `/**` 全站蓋一條 `X-Robots-Tag: noindex, nofollow`（CLAUDE.md 第 5 條，上線前必要），
  這條規則命中**每一筆**候選網址，所以 `urlset` 恆為空——不是快取或求值順序問題，
  是「這個模組的 robots 感知排除機制」與「全站 noindex 上線前」兩個各自合理的前提
  正面衝突，且衝突無法透過設定調解。
- **怎麼查證的**：先用 `dotnet run` 起 `apps/api`（讀本機 `sqlserver` 容器），
  再 `npm run build` ＋ `node .output/server/index.mjs` 起兩個 club 的 process，
  分別 curl `/sitemap.xml`（皆為空 `<urlset></urlset>`）與
  `/api/__sitemap__/urls`（資料正確），確認落差確實發生在「資料到 XML 輸出」這一段；
  再用 `npm run dev` 開 dev-mode（`import.meta.dev` 分支會印警告、且有 `/__sitemap__/debug`
  可用）交叉核對，兩種模式結果一致，排除「只在 prod 建置才發生」的可能；
  最後靜態讀模組原始碼找到 `hasRobotsDisabled` 那段判斷式，對照 `nuxt.config.ts`
  `routeRules['/**'].headers` 的內容，確認命中條件完全吻合。
  ⚠️ **沒有為了驗證而真的移除過 noindex 設定**——沙盒的自動分類器擋下了一次
  意圖「暫時移除 noindex 標頭做 A/B 對照」的指令，這個防呆是對的（CLAUDE.md 第 5 條），
  之後改用讀原始碼＋交叉核對兩種模式的方式驗證，不需要真的關掉 noindex。
- **修法**：`nuxt.config.ts` 加 `sitemap: { enabled: false }`（模組本身支援的乾淨關閉開關，
  `setup()` 開頭就 `return`，不掛任何路由／鉤子／prerender，與既有的 `ogImage: false`
  是同一種模式）；新增 `server/routes/sitemap.xml.ts` 自己組 XML 接手最終輸出，
  資料來源抽成 `server/utils/sitemap-urls.ts`（`server/api/__sitemap__/urls.ts` 改呼叫
  同一支函式，保留作為可獨立 curl 驗證的資料端點，不再是 `@nuxtjs/sitemap` 的自動探索來源）。
  自組路由一樣吃得到 `/**` 的 `X-Robots-Tag` 標頭（Nitro 的 routeRules 標頭依路徑疊加，
  不看是哪個 handler 回應），noindex 沒有被繞過。
- **實測結果（2026-09-22）**：tcrfc 站 93 筆（10 單元＋83 篇新聞）、bw 站 8 筆（單元，
  正確排除 `06`／`11`，0 篇新聞不報錯）；兩站 `/sitemap.xml` 皆送出
  `X-Robots-Tag: noindex, nofollow`；`/robots.txt` 與首頁功能不受影響；
  兩份 XML 皆通過 `xml.dom.minidom` 良構性檢查。
- **下次怎麼避免同類問題**：**任何「全站 noindex」與「SEO／sitemap 相關模組的自動化功能」
  同時存在時，先假設兩者會互相排斥，去讀該模組原始碼確認它怎麼處理 noindex 的路徑**，
  不要等模組輸出空結果才去猜原因；也不要停在第一個看起來合理的解釋（本項第一次就是
  這樣被誤判成快取問題，擱置了一天）。

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

### E-31 宣稱「對比度全部用公式實測過」，實際有兩組沒驗到／驗錯（2026-09-21，S0-12 後台深色重新設計）

- **錯在哪**：`docs/21-admin-ui.md` v2 的深色色票表寫明「所有色票的對比度都用 WCAG 相對亮度公式實測，
  不是憑感覺估」。交付後逐組重跑驗算，**27 組裡 25 組精確吻合，2 組錯的**：
  ① §4.3 用來論證「警告按鈕不能用白字」的反例數字寫 `4.06:1`，實算 **`1.72:1`**（差 2.4 倍）；
  ② §7.3 `--admin-text-tertiary` 對 `surface` 寫 `4.88:1`，實算 `5.29:1`。第二筆還牽出一個**真正的
  缺陷**：該 token 同時覆寫 `--el-text-color-placeholder`，而 placeholder 會出現在下拉選單、對話框、
  抽屜這些底色是四層裡最亮的 `overlay` 容器上，原色 `#8B92A0` 對 `overlay` 只有 **4.35:1 過不了 AA**，
  而色票表**只驗了 `surface` 與 `surface-2`，漏驗最嚴苛的那一層**。
- **為什麼會錯（根因）**：不是「不小心算錯」——是**驗算的覆蓋範圍由產出者自己挑**。當同一個人既決定
  「要驗哪幾組」又執行驗算，漏掉的組合不會被發現，因為它從來沒進過待驗清單；而「已經寫了腳本」這件事
  會製造「這份表格整體可信」的錯覺，讓抽驗的動機下降。反例數字（①）更明顯：它是用來**支持**某個決定的
  佐證，結論方向對的時候，佐證數字錯了沒有任何東西會反彈。
- **下次怎麼避免**：色票表交付後，**由產出者以外的人（或下一個步驟）把表格裡每一組前景／背景組合機械
  地重跑一次**，清單不從文件裡挑、而是從 token 定義做**笛卡兒積**（每一階文字色 × 每一層背景色），這樣
  漏驗的組合會自己浮出來。**深色主題特別要驗最亮的那一層背景**：淺色主題容器越疊越暗、文字對比只會變好，
  深色主題相反——容器越疊越亮、對比只會變差，所以 `overlay` 才是深色系統真正的門檻，不是 `surface`。
  **反例數字也要驗**，不能因為「結論方向是對的」就跳過。
- **防呆**：✅ **已補腳本**（同一次交付內完成）。修正本身：`--admin-text-tertiary` 改 `#9299A8`（四層全過）、
  反例數字改 `1.72`、規則寫進 `docs/21` §13.8。自動化：**[`apps/admin/scripts/check-contrast.mjs`](../apps/admin/scripts/check-contrast.mjs)
  已掛進 `npm run lint`**（`lint:contrast`），四種檢查：① **每階文字 × 每層背景跑笛卡兒積**——這是針對根因的那一項，
  清單不由人挑，漏掉的組合會自己浮出來 ② 四態 tag／語意色按鈕／邊框等成對色票各自的門檻（含 UI 元件的 3:1）
  ③ **把反例數字也釘住**（`1.72`／`2.78`），因為結論方向對的時候沒有東西會反彈，這正是①號錯誤混過去的原因
  ④ `docs/21` §7 的色值 vs `admin-theme.css` 實際的值，不一致就失敗（抓規格與實作脫鉤）。
  腳本用「塞回 E-31 的舊色值 `#8B92A0` 確認抓得到 overlay 那組 4.35:1、塞回錯誤的反例數字 `4.06` 確認抓得到，
  再各自復原」實測過真的有作用，不是只看它印 pass。

#### 🔴 E-31 升級（2026-09-22，S0-12d 後台改品牌配色）——**同一個根因第二次發生，所以這裡改的是機制不是記錄**

**又犯了一次，漏在同一層。** `docs/21` v3 的邊框表宣稱「邊框對 3:1 全過（最低 3.52）」，
那個 3.52 是對 `surface` 算的；`--admin-border-input` `#7A6F68` 對最亮的 `overlay`（`#3F2D28`）
實算只有 **2.66:1，過不了 WCAG 1.4.11 的 3:1**。而 `--admin-bg-input`（`#070504`）對 `overlay` 只有
**1.57:1**——在 `el-dialog`／`el-select` 這類 `overlay` 底的容器裡，**邊框是「這裡是可以打字的欄位」的
唯一辨識線索**，它一失效欄位邊界就消失。已修正為 `#8A7E75`（五層全過，最低 3.29）。

**依 `CLAUDE.md` 第 13 條，不新增 `E-34`**：同一類錯犯第二次，要的是機制不是記錄。

**上面那個防呆為什麼沒擋住**：它的笛卡兒積只涵蓋**文字**（「每階文字 × 每層背景」），
邊框走的是**人工列舉的成對色票**（②）。也就是說，**根因只被修掉一半**——
「清單不由人挑」這個性質只套用在文字，邊框仍然是「產出者自己挑要驗哪兩組」，
於是同一個根因換一個 token 家族又發生一次。
⚠️ **教訓不是「邊框也要加進去」，而是：局部套用的機制會給出全面的信心。**
修掉一個 token 家族的挑選權，卻留著其他家族的挑選權，下一次就從沒修的那半邊漏出來。

**升級後的要求**（寫進 `docs/21` §15.2 第 6、7 點，由 `check-contrast.mjs` 實作）：

1. **邊框類 token 也跑五層背景的笛卡兒積**（門檻 3:1）。明示不需要 3:1 的 `--admin-border` 走
   **明確的豁免清單且要寫明理由**，不是默默不驗。
2. 🔴 **腳本要能回答「有沒有任何一組色票組合是它沒驗到的」**：所有前景 token × 所有背景 token
   展開成完整矩陣，逐一要求每一格落在 **「通過」／「明文豁免」／「反例釘住數字」** 三類之一，
   否則報錯。**新增一個 token 而沒安排它的驗算方式，應該讓 lint 失敗，而不是靜默不驗。**

**下次再犯就不是補防呆的問題了**——若完整矩陣仍擋不住第三次，代表問題不在覆蓋範圍而在
「交付者自我驗證」這個結構本身，屆時要改的是流程（色票表一律由產出者以外的人複驗才算交付），
不是再改腳本。

### E-32 用 CDP 驗證深色 mockup 時，`/json/new` 端點在新版 Chrome 上改成只收 PUT（2026-09-21，S0-12c 深色重做驗收）

- **錯在哪**：依專案慣例（Persistent Agent Memory 已記過的手法）用 `curl -X GET
  http://localhost:9333/json/new?about:blank` 向無頭 Chrome 開新分頁，回應不是 JSON 而是純文字
  `Using unsafe HTTP verb GET to invoke /json/new. This action supports only PUT verb.`，
  驅動腳本的 `res.json()` 直接丟 `SyntaxError: Unexpected token 'U'`。本機裝的是 Chrome 153，
  舊筆記寫這招時的版本較舊，沒有這條限制。
- **為什麼會錯**：把「以前這樣呼叫可以」當成「現在也可以」，沒有先用 `curl` 探一次端點的實際回應，
  直接假設 `/json/new` 一律吃 GET——CDP 的 HTTP endpoint 這幾年逐步收緊成只接受 PUT（防止頁面上的
  `<img src="http://localhost:9222/json/new">` 這類 CSRF 式攻擊誤觸發開分頁），是 Chrome 自己
  的安全性變更，不是本專案的問題，但沿用舊寫法就會踩到。
- **下次怎麼避免**：用 CDP HTTP endpoint（`/json/new`、`/json/close/<id>` 等會「造成動作」的端點）
  一律先用 `curl -X PUT` 試，GET 只用在單純查詢用途的端點（`/json/version`、`/json/list`）。
- **防呆**：⚠️ 無。這是本機瀏覽器版本相關的環境事實，不好寫進自動化，靠這筆記錄與「先 curl 探一次
  端點」的習慣。

### E-33 Element Plus 沒設語系，畫面上印出英文——而禁用詞掃描掃不到元件庫自帶的文案（2026-09-21，S0-12c 深色重做驗收）

- **錯在哪**：後台新聞列表頁的分頁器印著 **`Total 8`** 與 **`20/page`**，違反規劃書 §4.0「介面一律日常
  中文、不得出現英文技術詞」。`main.ts` 只寫 `app.use(ElementPlus)`、沒帶語系，元件庫自帶文案就全是英文
  預設——除了分頁器，還有表格空資料的 `No Data`、`ElMessageBox` 的 `OK`／`Cancel`、日期選擇器的月份名稱。
  **這個問題 v1 就存在**，不是深色重做引入的，是這次逐頁看截圖才發現。
- **為什麼會錯（根因）**：`check-forbidden-terms.mjs` 的**掃描範圍**是 `.vue` 檔的 `<template>` 區塊，
  隱含前提是「畫面上的字都寫在我們自己的樣板裡」。這個前提對元件庫不成立——分頁器的文案在 `node_modules`
  裡，畫面上看得到、腳本看不到。**有腳本在跑，反而讓人以為這件事已經被守住了**：這跟 E-31 是同一個形狀
  的錯，防護措施的**覆蓋範圍**與它給人的**信心**不相稱，而差距落在盲點裡，不會自己冒出來。
- **下次怎麼避免**：加了自動檢查之後，**在腳本裡明確寫下它「掃不到什麼」**，不要只寫它掃什麼——盲點沒被
  講出來，下一個人只會看到「有腳本」。凡是引入第三方 UI 元件庫，**第一件事是設語系**，不要等畫面上看到
  英文才補。驗收時至少逐頁看一次截圖：**腳本過不等於畫面對**。
- **防呆**：✅ **已補**。`main.ts` 改成 `app.use(ElementPlus, { locale: zhTw })`；
  `check-forbidden-terms.mjs` 新增一項檢查——`main.ts` 沒設 `locale` 就讓 lint 失敗，並在腳本裡用註解
  寫明「這支腳本看不到元件庫自帶文案，所以只能改成檢查語系有沒有設」。用「把 `locale` 拿掉確認腳本抓得到、
  改回來確認會過」實測過。⚠️ **殘餘風險**：這只擋得住「語系沒設」，擋不住某個元件的中文翻譯本身不合我們
  的用語規範，那仍要靠看畫面。

---

## 3. 目前沒有防呆的項目

E-01／E-02／E-06／E-07／E-08／E-09／E-10／E-11／E-22／E-23／E-24／E-28／**E-32** 都還靠人記得。
**E-03 已於 2026-09-20 補上腳本**（[`tools/check-linerefs.mjs`](tools/check-linerefs.mjs)，同步鏈第 4 環結束前必跑）；
**E-21 已於 2026-09-21 補上腳本**（六類必然差異寫進 `site/tools/compare-dom.mjs` 的正規化規則）；
**E-25 本來就受 E-21 的同一支腳本保護**（`id` 屬性差異一律視為真差異），這次是搬遷者在跑
腳本前自行用 curl 核對到的；**E-29 已用 `docker build`＋`docker run`＋`curl -I` 實測驗證**；
**E-30 已用無頭瀏覽器監聽 console 錯誤重新驗證**；**E-31 已於 2026-09-21 補上腳本**
（`apps/admin/scripts/check-contrast.mjs`，掛進 `npm run lint`）；**E-33 已於 2026-09-21 補進
`check-forbidden-terms.mjs`**（改為同時檢查 `main.ts` 有沒有設語系）。

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

---

### E-34 `lint` 長期紅燈，於是一個**真的壞掉的連結**被當成既有雜訊放過了一個月（2026-09-22，藍鯨文案架構）

- **錯在哪**：`apps/web` 的 `npm run lint` 從 S0-9 完整搬遷之後就固定印
  `✖ 535 problems (1 error, 534 warnings)`，那個 `1 error` 是
  `Link "/assets/ics/first-team-2026-27.ics" does not match any known route`。
  這條錯誤被寫進交接說明、被口耳相傳成「既有的 `.ics` 靜態檔連結**誤判**，跟本次無關」，
  連續兩輪交付都照抄這個說法，**沒有人去看它在說什麼**。
  實際去看之後發現：**它不是誤判，是真的壞掉的連結**——
  `site/src/assets/ics/first-team-2026-27.ics` 這個檔案**存在且納版控**，
  但 S0-9 搬 80 頁時**沒有把它複製到 `apps/web/public/assets/ics/`**，
  一線隊頁的「訂閱一線隊賽程 (.ics)」按鈕**真的會 404**。
- **同一天還連帶暴露第二件事**：本次新增的 `check-club-copy.mjs` 原本掛成
  `"lint": "npm run lint:eslint && npm run lint:club-copy"`。
  因為 eslint 固定 exit 1，`&&` 會短路——**新加的那道防呆從掛上去的那一刻起就不會執行**。
  如果沒有順手驗一次離開碼，它會以「已經有防呆了」的姿態存在，但一次都沒跑過。
- **為什麼會錯（根因）**：**長期紅燈的檢查會把「讀錯誤訊息」這個動作從流程裡淘汰掉。**
  一個檢查只要固定失敗、而失敗又被歸類成「已知雜訊」，它就從「告訴你哪裡壞了」退化成
  「一個固定的數字」——之後**真正的錯誤混在同一個數字裡不會被看見**，
  而且任何**串在它後面的檢查都不會執行**。
  第二層根因是**轉述取代了查證**：「那個錯是誤判」這句話沒有人驗證過來源，
  它被當成事實一路抄了兩輪，因為它讓人不必處理紅燈。
- **下次怎麼避免**：
  1. **`lint` 不准長期紅燈。** 修不掉的規則就明確關掉或加單行排除**並寫明理由**，
     不要留著讓它每次都失敗——「有一個已知的錯」和「沒有錯」對後續流程是完全不同的狀態。
  2. **把既有錯誤說成「誤判」之前要先驗證。** 至少實際去看那個檔案存不存在。
  3. **新增檢查後要驗離開碼**，不是只看它印了什麼。用「塞錯值 → 確認 `$?` 是 1 → 復原 → 確認 `$?` 是 0」，
     這個專案既有的驗證紀律本來就是這樣，這次差點漏掉的是**串接方式**而不是腳本本身。
- **防呆**：✅ **同一次交付內完成**。
  ① 漏掉的 `.ics` 已複製到 `apps/web/public/assets/ics/`，磐石站實測 `curl` 回 **200**；
  ② 那一行改為**單行** `eslint-disable-next-line link-checker/valid-route` 並附註解說明
     「這是 `public/` 的靜態下載檔不是路由，該規則只讀 `.nuxt/link-checker/routes.json`、
     只接受 `routesFile`／`rootDir` 兩個選項，`nuxt.config` 的 `linkChecker.excludeLinks` 對它無效（已實測）」——
     **刻意不關掉整條規則、也不用 `/assets/**` 這種大範圍排除**，
     否則下一次真的漏檔又會被同一個藉口蓋過去；
  ③ `lint` 改成 `lint:club-copy && lint:eslint`，並把 eslint 修到 **0 error**，`npm run lint` 離開碼現在是 **0**；
  ④ `check-club-copy.mjs` 已用「移除 `HOME_SEO.bw` → `npm run lint` 離開碼 1 → 復原 → 離開碼 0」實測過真的會擋。

> ⚠️ **這一筆與 `E-31` 的關係**：`E-31` 是「防護的**覆蓋範圍**不足卻給出全面的信心」，
> 這一筆是「防護**有跑但沒人看**，以及防護**根本沒跑**」。
> 兩者的共同點是**防護的實際效力與它給人的信心不相稱**，但可改的行為不同，所以分開記。

---

#### 🔴 `E-34` 升級（2026-09-22，同日第二次盤點）：根因不只在 `lint`，在**二手轉述取代回查來源**

**依 `CLAUDE.md` 全域規定 13，不新增 `E-39`**——同一類錯犯第二次要的是機制不是記錄。
原始 `E-34` 的第二層根因寫的是「**轉述取代了查證**」。**同一天之內，同一個根因又出現三次，
而且全部不在 `lint` 輸出裡，是在我們自己的文件裡**：

| # | 轉述說的 | 回查原始來源後的事實 |
|---|---|---|
| 1 | `STATUS.md` 說種子資料 `articles.cover_key` 全 `NULL` 是「缺口」，並引用 `docs/12d` §9 當依據 | **`docs/12d` §9 的結論相反**：明寫「✅ 不算缺，是已知的 pipeline 落差」。`generate-club-seed-sql.py` 第 472 行的註解也寫明是刻意的 |
| 2 | `docs/12c` §5 列了「7 項待裁決」，`STATUS.md` 據此把 `S0-3b`／`S0-3c` 鎖在 🔄 | **其中 6 項早已被 `S0-6a` 的 DDL 與 `S0-6c/d` 的種子資料做掉**，`clubs_i18n`／`competitions_i18n`／`impact_records_i18n` 三張側表都在 `db/club-schema.sql` 裡。§5 是**拍板前的舊稿，拍板後沒回頭改** |
| 3 | `compare-dom.mjs` 多噴 10 頁差異，交接說明寫「抽查**像是** `S0-9f` 的自然後果」 | **是，但沒有人登記**。基準 `site/dist` 的新聞卡片仍是 mockup 寫死的 `../zh/news/article/`，`S0-9f` 刻意改成逐篇 slug 卻沒把受影響頁面登記成已批准差異 |

- **根因（擴大後）**：**一份文件記下的結論，會在它的來源被改掉之後繼續被當成事實引用。**
  `E-34` 原本把這個現象綁在「`lint` 長期紅燈」這個特定載體上，於是防呆也只做在 `lint`——
  但載體從來不是重點。**只要一個結論被抄進第二個地方，它就開始獨立於來源老化**，
  而老化不會發出任何信號：`STATUS.md` 不會因為 `docs/12d` 改了就變紅，
  `docs/12c` §5 不會因為 DDL 建好了就自己劃掉。
  ⚠️ 更糟的是**這三次都不是「誰寫錯了」**——每一份文件在**寫下的當下都是對的**。
  錯的是我們沒有承認「轉述會過期」這件事，因此沒有任何一步去讓過期浮出來。
- **為什麼 `E-34` 原本的防呆擋不住**：它的三條全部針對 `lint`（不准長期紅燈／說成誤判前要驗證／新增檢查要驗離開碼）。
  第 2 條「**把既有結論說成 X 之前要先驗證**」才是可遷移的那一條，**但它被寫成 `lint` 專用**。
  這正是 `E-31` 的失效模式重演：**局部套用的機制會給出全面的信心。**
- **升級後的要求（適用全專案，不限 `lint`）**：
  1. 🔴 **狀態類敘述在被當成行動依據之前，一律回查原始來源。**
     「狀態類敘述」指 `STATUS.md` 的工作列與備註、`docs/` 裡的待辦／缺口／待裁決段落、
     交接說明裡的「已知問題」——**這些是轉述，不是來源**。規格的來源是規劃書，
     資料表的來源是 `db/*.sql`，程式行為的來源是程式碼。
  2. 🔴 **轉述與來源衝突時，以來源為準，並在同一次交付內修掉轉述。**
     發現矛盾不是「順手記一筆」，是**當場把過期的那一份改掉**——
     留著它，下一個人會再調查一次同樣的事。
  3. 🔴 **刻意偏離既有基準的交付，當下就要處理基準，不能留給下一輪去歸因。**
     `S0-9f` 這種「我們刻意修掉 mockup 的簡化」的改動，必須在同一次交付裡把
     `compare-dom.mjs` 的紅燈處理掉，**不准留著讓下一輪去猜它是不是雜訊**。
     ⛔ `compare-dom.mjs` 檔頭第 11–16 行是使用者 2026-09-21 拍板的封閉清單原則：
     **不提供 `--ignore`／`--allow`／`--tolerate` 旁路，永遠不會有**，這一條沒有變。
  4. **寫進 [`docs/14-invariants.md`](14-invariants.md)**，因為這已經是「改錯會出事」的層級，
     不再只是「做錯過的事」。

> 🔴 **第 3 點原本寫死的做法本身就是一筆未查證的轉述，已於 2026-09-23 改掉（見上）**。
> 原文寫「**做法是同步更新基準 `site/src` 並重新 build，不是把差異登記成例外**」——
> 把「處理基準」這個目標，和「更新基準」這一種手段綁死了。回查來源後發現**那個手段在本案做不到**：
> `site/src` 的 176 個新聞卡 `href` 全是**手寫**的 `{{ROOT}}/zh/news/article/`，
> 指向**同一個佔位頁**，而 `site/dist` 整站只有那一頁文章頁。
> 照原文去做，Cloudflare Pages 上的 `tcrfc-mockup` 預覽站每張新聞卡都會 404；
> 若再讓 `build.mjs` 產 113 個假文章頁，模板內容與 Nuxt 的真實內容必然不同，
> **1 頁紅燈會變成 113 頁紅燈**。
> **根因跟這一整條升級要講的事一模一樣**：寫下結論的當下沒有去看 `site/src` 實際長什麼樣，
> 只是從「基準過期了就更新基準」這個聽起來理所當然的推論直接寫成規定。
> ⚠️ **可遷移的教訓**：**紀律要規定「要達成什麼」，不要規定「用哪一招達成」。**
> 手段會因為當初沒查證的前提而失效，目標不會；把手段寫進紀律，等於把一個未驗證的假設
> 升格成不准違反的規則，下一個人照做就會做出錯的東西——或者更糟，**發現做不到卻以為是自己錯了**。
> **使用者 2026-09-23 拍板**：本案走「承認 `site/dist` 對新聞單元已退役」，
> 退役清單寫在 `compare-dom.mjs` 裡，**每一筆必須同時寫明「為什麼退役」與「改由哪個檢查接手」**，
> 缺接手者就讓工具自己報錯——**退役不是少驗一頁，是換一種驗**，這才是與封閉清單原則相容的做法
> （封閉清單管「同一頁之內哪些差異可以忽略」，退役清單管「這一頁還能不能拿 `site/dist` 當基準」，
> 兩者並存，後者永遠不會讓任何一頁的差異被靜默吞掉）。
- **下次再犯就不是補防呆的問題了**：若同一個根因出現第三類載體，代表要改的是
  **文件結構本身**（例如禁止在 `STATUS.md` 寫結論、只准寫連結指向來源），不是再加一條紀律。

> 🔴 **寫這段升級的當下就犯了同一條（2026-09-22）**：上面第 3 點的初稿寫成
> 「已在 `compare-dom.mjs` 實作為 `approved-diffs` 清單」——**那個實作不存在，而且方向與
> `compare-dom.mjs` 檔頭第 11–16 行使用者已拍板的「封閉清單、永不提供旁路」原則正面衝突**。
> 成因與上表三例完全相同：**在沒有回查來源（這裡是腳本本身）的情況下，寫下一個聽起來合理的結論。**
> 已當場改掉。留著這段是因為它是這條紀律最好的證據——**寫紀律的人在寫的當下就違反了它**，
> 足見「回查來源」不是態度問題而是流程問題，必須靠機制不是靠自覺。

> ✅ **同一天，這條紀律第一次擋下東西（2026-09-22）**：派工指示裡寫「拿掉 `docs/12` 的 `Form` 🌐 標記」，
> 依據是分析報告轉述的「DDL 選了不建 `forms_i18n`」。接手的 `system-analyst` **沒有照做，先去查 `db/club-schema.sql`**——
> `forms_i18n` **真的存在**（有 `auto_reply_body` 一欄），拿掉 🌐 會變成謊報 DDL 現況。
> 它改為保留 🌐 並加註範圍（僅指自動回覆信文案，表單顯示名稱依拍板維持寫死），並在回報裡明說自己偏離了指示。
> **這正是這條紀律要的行為**：上游的指示也是一種轉述，一樣會過期、一樣要回查。
> ⚠️ 附帶的教訓給派工的人：**指示裡不要替執行者寫死「應該看到什麼」**，
> 寫「核對後決定」比寫「拿掉它」更不容易把自己的錯誤前提傳染下去。

> 🔴 **第四個載體（2026-09-22，`S0-8` 圖片上傳共用元件）**：`backend-engineer` 交付 `S0-8` 後端時，
> 在報告的「沒做的部分與原因」明確標示了一段「**架構判斷（非規劃書明文，需要確認）**」——選擇
> 「先呼叫上傳端點拿 key、前端再把 key 塞進原本純 JSON 的建立／更新請求」兩段式做法，而不是把
> `AdminNews` 既有端點改成 multipart 單一請求，並具體寫明「若判斷結果是『一定要單一 multipart
> 請求』……應該另外排工」。**這個標示本身完全正確**——它就是「回查來源前先誠實承認沒查」的示範。
> **錯的是下一步**：派工的人把這段「待確認」原封當成既定契約往下傳給 `frontend-architect`，
> 沒有先回查規劃書第 53 行／第 972 行「選檔不上傳、儲存才上傳……離開或取消表單不留下任何檔案」，
> 前端因此依照這個未經查證的契約做成「選檔即上傳」，最終構成一筆**規格違反**（不是實作瑕疵）。
> 跟前三個載體同一個根因：**一份帶著「待確認」標記的敘述，被當成行動依據往下傳時沒有回查來源**——
> 差別只在來源不是別份文件（`docs/12d`／`docs/12c`／基準截圖），是規劃書本身。
> ⚠️ **給派工的人的教訓跟上一個附帶教訓同方向但更進一步**：下游明確標示「需要確認」時，
> **那正是回查來源的觸發點**，不是可以跳過的免責聲明——標記的目的是讓下一個讀到它的人回查，
> 不是讓風險原封轉嫁給更下游。**修正**：後端已改成單一 `multipart/form-data` 請求（`Features/AdminNews`
> 建立／更新端點），整支移除 `Features/Uploads` 的獨立上傳端點，並新增自動化測試釘住「上傳成功
> 但資料列寫入失敗時回滾、不留孤兒物件」，見 [`apps/api/README.md`](../apps/api/README.md)
> 「圖片上傳共用元件」整節。

---

> 🟢 **同一天第五次，這次踩在「防護根本沒跑」那一支上（2026-09-22，`S0-8` 收尾複驗）**：
> 為了複驗 `backend-engineer` 報的「99/99 全綠」，在 **`apps/api/` 目錄**下跑 `dotnet test`——
> **離開碼 0、輸出只有 `Determining projects to restore...`、一個測試都沒有執行。**
> 根因：`Tcrfc.Api.csproj` 明確 `<Compile Remove>` 排除了測試子專案（`S0-7d` 的刻意設計，為了讓
> `docker build` 不受影響），所以在那一層跑 `dotnet test` 找到的是**主專案**，沒有測試可跑，
> 於是**回報成功**。⚠️ **這比紅燈危險得多**：紅燈至少會叫人去看，
> 「綠燈 ＋ 零測試」給的是完整的信心而背後什麼都沒驗。
> 正確跑法是**指向測試專案**：`dotnet test apps/api/Tcrfc.Api.Tests/Tcrfc.Api.Tests.csproj`，
> 並且**要設 `CLUB_SQL_CONNECTION_STRING`**（設對之後實測 **99/99、Duration 29s**，數字屬實）。
> ✅ **CI 沒有踩到**（`ci.yml` 第 201 行本來就指向 `.csproj`，已核對），這是**人工複驗**才會踩的坑。
> 🔴 **可改的行為**：驗測試結果時，**離開碼 0 不算通過，要看到通過筆數**。
> 「`Passed! - Failed: 0, Passed: 99`」才是通過，`exit 0` 不是。
> 這條跟 `E-34` 原文第 3 點（新增檢查後要驗離開碼）是**一體兩面**——
> 離開碼能抓到「跑了但失敗」，抓不到「根本沒跑」，**兩者都要看**。

---

### E-35 前端專案的驗收只跑 `npm run build`，`docker build` 到交付後才發現是壞的（2026-09-22，慈善捐款前台）

- **錯在哪**：慈善前台 `apps/web-charity/` 交付時 `npm run build`、`npm run lint`、逐頁 `curl`、
  三斷點實測**全部通過**，但 `docker build apps/web-charity` **直接失敗**：

  ```
  [sync-fixtures] 找不到來源檔案：/db/seed/charity-fixtures.json
  ERROR: process "/bin/sh -c npm run build" did not complete successfully: exit code: 1
  ```

  原因是我把兩個前端共用的假資料放在 repo 根目錄的 `db/seed/charity-fixtures.json`，
  而 [`20-cicd.md`](20-cicd.md) §3 的既有慣例是**以 app 目錄為 build context**
  （`docker build -t x apps/admin` 已實測過）——那個檔案落在 build context 之外，容器內讀不到。
  **本機跑得動是因為本機看得到整個 repo；容器看不到。**
- **為什麼會錯（根因）**：兩層。
  1. **新增跨專案共用的檔案時，沒有先確認它在不在各專案的 Docker build context 內。**
     「跨專案共用」在 monorepo 的檔案系統裡是理所當然的事，在容器裡卻是預設做不到的事——
     這個落差不會在任何本機指令裡浮現。
  2. 🔴 **更要命的是驗收清單的形狀**：我派工時給前台的驗收是「`npm run build` ／ `lint` ／ `curl` ／
     三斷點」，**沒有 `docker build`**；給後台的驗收**有**寫 `docker build`。同一輪、同一類專案、
     兩份清單不一致，於是缺的那一邊就漏掉了。**驗收清單是人逐次手寫的，就會逐次不一樣。**
- **下次怎麼避免**：
  1. **前端專案的驗收一律包含 `docker build`**，不是只有 `npm run build`。
     這兩件事驗的是不同的東西：前者驗「程式碼對不對」，後者驗「**這個專案能不能被部署**」。
     Dockerfile 建不起來的交付物等於沒有交付。
  2. **新增任何跨專案共用的檔案時，先問「它在 build context 裡嗎」。** 若不在，要嘛改 context，
     要嘛在各專案內放一份由腳本產生、且有同步檢查的副本。
  3. **不要每次手寫驗收清單。** 同一類專案的驗收項目應該是一份固定清單，逐項勾，不是憑印象列。
- **防呆**：✅ **同一次交付內完成**。
  ① `db/seed/emit-charity-fixtures.py` 改為同時輸出三份內容完全一致的檔案
     （`db/seed/` 正本 ＋ 兩個 app 目錄內的副本，都納版控），`--check` 一次驗三份，
     任一份過期就 exit 1，已掛進兩個前端的 `npm run lint`；
  ② `apps/web-charity` 與 `apps/admin-charity` 都已 `docker build` ＋ `docker run` ＋ `curl -I` 實測，
     兩者的 `X-Robots-Tag: noindex` 皆正確送出（`E-29` 無回歸）。

> 🔵 **為什麼選「各 app 放副本」而不是「把 build context 改成 repo root」**：
> 後者要改兩支 Dockerfile、要加根目錄 `.dockerignore` 擋掉 456MB 的收件夾與 `reference/`，
> 並推翻 `20-cicd.md` 已記錄且已實測的慣例。副本最怕的是漂移，而漂移由 `--check` 擋住；
> 改 context 要動的面則大得多。**這是執行層取捨，不是規格。**

#### 🔴 E-35 升級（2026-09-22，`S0-9e` 收尾時發現）——**同一個根因第二次發生，改的是機制不是記錄**

**又犯了一次，而且是我自己犯的。** `BW-0e` 那一輪我在 `apps/web/package.json` 加了 `vue-tsc` 這個
devDependency，只跑了 `npm run lint`／`npm run build` 就交付——**本機已有 `node_modules`，
那兩個指令根本不會驗證鎖檔一致性**。結果是 **`docker build -f apps/web/Dockerfile apps/web` 直接失敗**，
`apps/web`（主站前台）**處於不可部署狀態**，隔了三個交付才被發現。

**診斷過程本身也值得記**，因為前兩個推論都是錯的：

| 推論 | 查證結果 |
|---|---|
| ① 鎖檔壞了 → 重跑 `npm install` | ❌ `git diff` 顯示鎖檔**一個字都沒變**，問題不在這 |
| ② 本機 npm 11.8 與容器 npm 10.9 解析不同 | ❌ 方向對但不是根因——用容器的 npm 重產鎖檔後 `npm ci` **仍然失敗** |
| ③ **`node:22.12` 太舊** | ✅ 相依樹裡多個套件要求 `^20.19.0 \|\| ^22.13.0 \|\| >=24`，**22.12.0 不滿足** |

⚠️ **`npm ci` 的錯誤訊息（「Missing: eslint@9.39.5 from lock file」）把人指向鎖檔，
而真正的原因是 Node 版本**——這種「錯誤訊息指錯方向」的情況，唯一的解法是**逐個推論都去查證**，
不要在第一個看起來合理的解釋停下來。

**為什麼 `E-35` 的教訓沒有擋住這次**：`E-35` 寫的是「前端專案的驗收一律包含 `docker build`」，
**但那是寫給「做前端專案的人」的**，而我當時做的事在我自己看來是「加一支 lint 腳本」——
⚠️ **我沒有把「動了 `package.json`」認成「動了前端專案的可部署性」。**

**升級後的要求**（比原本那條更難迴避）：
1. 🔴 **動到任何 `package.json` 或 `package-lock.json`，就要跑該專案的 `docker build`。**
   不是「做前端功能時」——**是「碰到相依宣告時」**。這個觸發條件是機械的，不需要判斷「這算不算前端工作」。
2. **`npm run build` 不能代替 `docker build`**：前者用本機既有的 `node_modules`，
   後者跑 `npm ci` 從零裝。**兩者驗的是不同的東西**，而且只有後者代表「這個東西能被部署」。
3. **基底映像檔的版本要是被檢視過的決定，不是沿用下來的預設值。**
   `node:22.12-alpine` 用在 4 個專案，但 `docs/17`／`docs/20` **完全沒有記錄它是怎麼選的**——
   沒有人選過它，它只是當時複製貼上的值，然後相依樹長過了它。

**防呆**：🔴 **尚未完成**，`apps/web` 目前仍不可部署（見 `STATUS.md`）。
要做的是把基底映像檔升到滿足相依需求的版本，**而那是 4 個 Dockerfile 的一致性決定**，
屬於部署層（`CLAUDE.md` 第 12 條 → `deployment-engineer`），不在本輪範圍內草率動手。

---

### E-36 改了種子資料，沒有跑相依專案的測試——兩個斷言默默過期（2026-09-22，`BW-0g` 造成、`S0-7e` 才發現）

- **錯在哪**：`BW-0g`（藍鯨舊站資料匯入）把 28 名藍鯨球員灌進 `tcrfc_club_dev`。
  但 `apps/api/Tcrfc.Api.Tests` 裡有兩個斷言寫的是「**藍鯨球員數應為 0**」
  （`ClubScopingTests` 與 `CacheBehaviorTests` 各一個），它們是在藍鯨還沒有任何資料時寫的。
  **`BW-0g` 那一輪完全沒有跑 `dotnet test`**，所以沒人發現這兩個斷言已經不成立——
  一直到隔了兩個交付、`S0-7e` 做寫入端點時才撞出來。
- **為什麼會錯（根因）**：**`db/seed/` 是跨專案的共用真實來源，但它的驗收只看自己。**
  種子那一輪的驗收清單是「產得出 SQL、套用成功、冪等、筆數對得上、主站庫未受影響」——
  全部都是**對資料庫**的檢查，沒有一項是「**有誰依賴這份資料**」。
  而 `apps/api` 的整合測試是直接打本機資料庫的，種子一改它們就可能失效。
  ⚠️ 更一般的形狀：**改動的影響範圍比改動所在的目錄大**，而驗收清單是照目錄寫的。
- **下次怎麼避免**：
  1. **改 `db/seed/` 或 `db/*.sql` 之後，要跑 `apps/api` 的測試**——那是目前唯一直接打真實資料庫的專案。
  2. 更一般地：**動共用的東西之前先問「誰依賴它」**，而不是只問「我改的這個目錄裡有什麼」。
     目前已知的共用真實來源有三處：`db/seed/`（資料庫 ＋ `apps/api` 測試）、
     `db/seed/charity-fixtures.json`（兩個慈善前端）、`site/src/assets/css/tcrfc.css`（主站與藍鯨）。
  3. **斷言不要寫成「應為 0」這種依賴「目前剛好沒資料」的形狀。** 已改為驗證
     **「兩俱樂部的球員 id 集合互不重疊」**——那是俱樂部範圍隔離真正要保證的性質，
     不隨資料多寡變動。⚠️ 這比補跑測試更根本：**原本的斷言就算當時有跑，也只是剛好會過。**
- **防呆**：🟡 **部分**。兩個斷言已改成不依賴資料筆數的形狀（`S0-7e` 交付內完成，48/48 全綠）。
  ⛔ **但「改種子要跑 API 測試」目前沒有自動化**——CI 還沒建（`docs/20-cicd.md` 是計畫不是實作），
  等 CI 建起來時，`db/**` 的改動要觸發 `apps/api` 的測試，**這條要記得加進去**。

> ⚠️ **與 `E-35` 的關係**：兩者都是「驗收清單的範圍比改動的影響範圍小」。
> `E-35` 是同一類專案的兩份手寫清單不一致；這一筆是清單照目錄寫、而影響跨目錄。
> **共同的可改行為是：驗收清單不要憑當次印象列。**

---

### E-37 三斷點驗證只量 `document` 層級的 `scrollWidth`，量不到捲動容器**內部**的溢出（2026-09-22，發現時已是第二次）

- **錯在哪**：本專案兩個後台的響應式驗證用的都是
  `document.documentElement.scrollWidth === innerWidth`，三斷點全綠就算過。
  但 **`el-table` 自己是一個捲動容器**——它的欄位總寬是照 `el-table-column` 的數量與 `width`
  參數加總算出來的，**不看 CSS 有沒有把儲存格藏起來**。用 `display: none`／`class-name` 隱藏次要欄位時，
  儲存格消失但表格總寬不變，於是溢出被**包在表格自己的捲動容器裡**，
  `document.documentElement.scrollWidth` **量不到**。
  結果是：**自動化驗證全綠，畫面卻是壞的。**
  `apps/admin` 在 768px 實測 `el-table__header-wrapper` 內部 `scrollWidth 990 vs clientWidth 599`，
  溢出 391px——而 `S0-12c` 當初宣稱「三斷點 scrollWidth 均等於 viewport」是**真的**，只是量錯了東西。
- **這是第二次**：`apps/admin-charity` 交付時（`CH-3a`）也撞到同一個形狀，當時記在
  [`22-charity-ui.md`](22-charity-ui.md) §6 的實作提醒裡，**沒有升級成 `docs/18` 的條目、
  也沒有回頭改驗證方法**——所以 `apps/admin` 用同一套不足的檢查，繼續全綠了一輪。
- **為什麼會錯（根因）**：**驗證方法量的是「頁面有沒有橫向捲軸」，而要保證的性質是
  「畫面上沒有東西被切掉」。** 這兩件事在只有一層捲動容器時等價，一旦出現巢狀捲動容器
  （表格、`el-drawer`、`overflow: auto` 的卡片）就不等價了。
  ⚠️ 更一般的形狀：**代理指標（proxy metric）在多數情況下等於真正要保證的性質，
  於是沒有人回頭檢查它什麼時候會脫鉤。** 脫鉤的時候它不會報錯，它會**通過**。
- **下次怎麼避免**：
  1. **三斷點檢查不能只量 `document.documentElement`**，要**逐一掃描所有捲動容器**：
     對每個元素比對 `scrollWidth > clientWidth`，把違規元素的選擇器印出來。
  2. `el-table` 的響應式隱藏欄位**一律用 `v-if` 整欄不渲染**，⛔ 不得用 CSS 隱藏儲存格。
  3. **把「這個檢查會漏掉什麼」寫進檢查本身的註解**——這一條與 `E-31` 升級後的要求同一個精神：
     防護要能說出自己的覆蓋範圍，不然它給出的信心會超過它實際驗到的東西。
- **防呆**：🟡 **部分**。`apps/admin`（本輪）與 `apps/admin-charity`（`CH-3a`）的表格都已改為 `v-if`，
  兩邊都用「逐元素掃描捲動容器」的方式重驗過。
  ⛔ **但那個掃描目前是一次性的臨時腳本，沒有留在專案裡**——
  下一個做響應式的人若沿用舊的 `document` 層級寫法，同一個洞會第三次出現。
  **要留下來的是那支掃描腳本本身**，等 CI 建起來時一併納入（`docs/20-cicd.md`）。

> ⚠️ 與 `E-31`／`E-34`／`E-35`／`E-36` 同屬一族：**防護的實際效力與它給人的信心不相稱。**
> 這一筆特別的地方在於它**不是覆蓋範圍不足，是量錯了東西**——
> 檢查本身跑得好好的，只是它保證的性質不是我們以為的那個。

### E-38 「只做一次」的完成旗標在動作失敗時也被設成完成，真正的錯誤被下一次呼叫的誤導性錯誤蓋掉（2026-09-22，S0-8 圖片上傳共用元件）

- **錯在哪**：`apps/api/Images/BlobImageStorageService.cs` 的 `EnsureContainerAsync`（容器不存在時
  自動建立一次，避免每次上傳都打一次「容器是否存在」的 API）原本這樣寫：

  ```csharp
  private async Task EnsureContainerAsync(CancellationToken cancellationToken)
  {
      if (Interlocked.CompareExchange(ref _containerEnsured, 1, 0) == 0)
      {
          await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
      }
  }
  ```

  `Interlocked.CompareExchange` 在呼叫 `CreateIfNotExistsAsync` **之前**就把旗標設成「已確保」。
  本機對 Azurite 實測時，第一次上傳因為 `Azure.Storage.Blobs` SDK 版本比 Azurite 認得的 API 版本新
  而失敗（`400 The API version ... is not supported by Azurite`，見
  [`apps/api/README.md`](../apps/api/README.md)「本機開發：Azurite」），但旗標**已經被設成已完成**。
  第二次呼叫因此完全跳過容器建立、直接對一個從未真正建立成功的容器寫入，得到的錯誤變成
  `404 The specified container does not exist`——**跟一開始那個真正的 API 版本不相容問題完全無關**，
  排查時完全被誤導，多花時間才想到要去查旗標邏輯本身，而不是繼續在 Azurite 版本相容性上打轉。
- **為什麼會錯（根因）**：**把「這個動作只做一次」的旗標，設在「動作真正成功」之前，而不是之後。**
  這種寫法在動作**一定會成功**（或失敗了也無所謂）時沒有問題，但只要動作可能失敗、而失敗後
  還想要「下次重試」，旗標就必須綁定在「成功」這個結果上，不能綁在「呼叫過」這件事上。
  `Interlocked.CompareExchange` 本身沒有錯，錯的是拿它保護一段**中間會 `await` 且可能失敗**的動作——
  它只能安全地保護「同步、不會失敗」或「失敗了也不影響正確性」的一次性初始化。
- **下次怎麼避免**：寫任何「只做一次」的快取／初始化旗標時，先問一句：**這個動作可能失敗嗎？
  失敗之後我希望下次呼叫重試，還是永遠放棄？** 只要答案是「希望重試」，旗標就必須在動作的
  `await` 完成、確認成功之後才設定，中間要嘛用 `SemaphoreSlim`（本次的修法）包住整段等待其他
  呼叫端排隊，要嘛接受多個呼叫端同時各自嘗試一次（如果動作本身是 idempotent，像
  `CreateIfNotExistsAsync` 這種情況）。
- **防呆**：⚠️ 無自動化檢查（這類「旗標設定時機」的邏輯錯誤很難用靜態分析攔截）。已修正為
  `SemaphoreSlim` 包住整段、旗標在 `CreateIfNotExistsAsync` 成功之後才設定，並在程式碼註解裡
  寫明這個修法要解決的問題形狀，供下一個寫類似旗標的人對照。

### E-39 同一個 enum 在同一支功能裡有兩份手寫對照表，其中一份從來沒被真實資料打中過（2026-09-23，`S0-9j`）

- **錯在哪**：[`apps/web/app/utils/schedule.ts`](../apps/web/app/utils/schedule.ts) 的 `mapMatchStatus()`
  用 switch 比對 `'finished'`，但 `matches.status` 的真實值是 `'played'`。結果是**藍鯨 21 場已完成賽事
  在畫面上全部顯示成「未開始」**；更隱蔽的第二個症狀是「賽果」分頁的篩選用同一支函式，
  **藍鯨的賽果分頁從頭到尾是空的**，一個月內沒有人提過。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：同一組 `matches.status` enum 在同一個頁面功能裡
  **存在兩份各自手寫的對照表**——`mapMatchStatus()`（給畫面 status-pill）與 `schedule.vue` 的
  `EVENT_STATUS_MAP`（給 `SportsEvent` JSON-LD）。兩份表的鍵型別都只是 `string`，
  **沒有任何機制要求它們一致，也沒有任何機制要求它們涵蓋資料庫真的會出現的值**；
  `matches.status` 在 `db/club-schema.sql` 是 `nvarchar(16)` 且**無 CHECK 約束**，DB 這一層也不提供保證。
  於是「寫錯一個字串」這件事在整條鏈上**沒有任何一處會失敗**。
  🔴 **第二層根因**：2026-09-22 寫 JSON-LD 的那一輪交付**已經在行內註解裡寫明發現了這個落差**
  （「兩者對不上……回報但不在此修」），卻選擇留著。**把已知的壞掉寫進註解，不等於處理了它**——
  註解不會讓任何檢查變紅，下一個人看到的仍然是一個「正常」的頁面。
  這與 `E-34` 系譜同源（**記錄取代了處理**），但可改的行為不同，所以另立一筆。
- **下次怎麼避免**：
  1. **同一組 enum 在同一個功能裡只准有一份對照表。** 需要多種輸出格式（顯示文字、CSS class、
     schema.org 型別）就在**同一筆資料**上多開欄位，不要為了新用途另寫一份。
  2. **對照表的鍵必須對得上真實寫入路徑寫進去的值**，而且這件事要由腳本驗，不是靠記憶。
  3. **發現落差當下就處理，或明確排一列工作。** 只寫進行內註解不算。
- **防呆**：✅ 同一次交付內完成。[`apps/web/scripts/check-match-status.mjs`](../apps/web/scripts/check-match-status.mjs)，
  掛進 `npm run lint` 且**排在 `lint:eslint` 之前**（`E-34` 的 `&&` 短路教訓）。三道檢查：
  ① 單一來源 `MATCH_STATUS_MAP` 的結構完整、兩支 export 都真的讀它；
  ② `app/`／`shared/` 底下不准出現第二份對照表或手刻 `schema.org/Event*` 字面值；
  ③ **跨載體核對**——解析 `db/seed/generate-club-seed-sql.py` 的 `INSERT INTO matches` 語句、
  依欄位位置取出 `status` 的真實字面值，逐一要求 `MATCH_STATUS_MAP` 有對應鍵；
  另掃 `db/`／`apps/api/` 是否有**未登記**的 `INSERT/UPDATE matches` 來源。

> 🔴 **這筆防呆的第一版是不合格的，記在這裡因為失敗的形狀比成功的更值得留**：
> 第一版只做了①②，**不驗字面值**。驗收時用「把 `MATCH_STATUS_MAP` 的 `played` 鍵改回 `finished`」
> ——也就是**一字不差地重現本筆要防的那個 bug**——去測，結果 **exit 0，沒抓到**。
> 一支叫 `check-match-status`、掛在 `lint` 裡、印著「✓ 檢查通過」的腳本，**擋不住它命名的那個 bug**。
> 這正是 `E-31`／`E-34` 升級段反覆點名的失效模式：**局部套用的機制會給出全面的信心**。
> ⚠️ **可遷移的教訓——新增防呆的驗收條件，是「用那個錯誤本身當反例」，不是「隨便塞個錯值看它會不會叫」。**
> 塞任意錯值只證明腳本會失敗，不證明它涵蓋了要防的那件事。
> 附帶：「需要先定義正式拼法才能驗」這個當時的理由也不成立——**要驗的不是「拼法對不對」（那確實是規格層），
> 是「程式的表有沒有涵蓋到真實寫入路徑會寫進去的值」，後者現在就能從程式碼讀出來**，不需要任何新規格。

### E-40 把一頁**符合條件**的頁面因為鄰近頁面的問題而錯誤地排除（2026-09-23，`S0-9i`）

- **錯在哪**：`S0-9i` 建退役清單時，`/zh/news/` 被歸類成「混著台中磐石國際足球盃 6 篇文章分類歸屬的
  既有落差，不是單純 href 差異」而**刻意排除在清單外**。實際逐筆核對 `--json` 輸出後，
  這一頁的 **20 筆差異 100% 是 `/zh/news/article/` → `/zh/news/<slug>/`，零例外**。
  真正混到 intcup 分類落差的是 `/zh/news/camps-events/`（「共 7 篇」對「共 1 篇」）與
  `/zh/news/match/`（月份篩選器選項整批位移）兩頁，`/zh/news/` 沒有。
- **為什麼會錯（根因）**：整個退役機制的防線都架在**「不准連坐退役」**這個方向上——
  檔頭寫了、清單設計寫了、派工指示也特別交代「不要預設全部都是」。
  **但同一個成因（用印象分組取代逐筆核對）會往兩個方向出錯，而只有一個方向被防著。**
  「這幾頁看起來都是新聞單元、那批頁面有 intcup 問題」是一次**分組推論**，
  它讓一頁乾淨的頁面被錯留在紅燈裡——後果比連坐退役不明顯，所以更不會被發現：
  **紅燈多一頁沒有人會來查，那一頁就會一直紅著，然後變成「已知雜訊」**，回到 `E-34` 的起點。
- **下次怎麼避免**：**分類的依據只能是逐筆核對的結果，不能是「這一批看起來像」。**
  工具已經吐得出 `--json`，就用它把每一筆差異的 expected／actual 跑過一次程式化判斷，
  不要用肉眼掃摘要。⚠️ **判斷「該不該放寬」時要雙向問**：既問「有沒有不該放進來的」，
  也問「有沒有該放進來卻被擋在外面的」。
- **防呆**：⚠️ 無自動化檢查（這是判斷品質問題，不是可以靜態分析的東西）。
  已寫進 [`14-invariants.md`](14-invariants.md) 的退役機制段落，與「不得連坐退役」並列成對，
  讓下一個人看到清單時同時看到兩個方向的失效模式。

### E-41 修訂摘要寫成「『延期』改為『延賽』」，把被取代的舊用語留在客戶看得到的交付物裡（2026-09-23，`v3.13` 同步鏈）

- **錯在哪**：跑 `v3.13` 同步鏈時，主站與 App 規劃書的修訂摘要寫成
  「**「延期」改為「延賽」**」，英文版同樣寫成 `"延期" is renamed to "延賽"`。
  這直接違反 [`00-harness.md`](00-harness.md) §2.5 的明文——客戶 2026-09-12 指示：
  **「連『原本是 X 改為 Y』這類註記本身都不留」**。四份母檔都是**客戶交付物**，
  結果被淘汰的舊用語反而因為這句話被印進了 PDF。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**這條規定從訂下到現在，沒有任何一處會因為違反它而失敗。**
  §2.5 的第 4 環寫著「收尾必跑 `check-linerefs.mjs`」——那是行號的檢查，
  而「範圍縮減的寫法」這一整節**只有文字，沒有對應的腳本**。
  一條只靠讀者記得的規定，在一份 200 行的流程文件裡，等於**只對讀到那一段而且當下想起來的人生效**。
  ⚠️ 更要命的是這條規定的失效**沒有任何症狀**：文件照樣產得出來、PDF 照樣漂亮、
  `check-linerefs` 照樣 exit 0——**唯一會發現的人是客戶**。
- **下次怎麼避免**：**流程文件裡凡是「不准寫成 X」的規定，要嘛配一支腳本，要嘛承認它只是建議。**
  兩者都可以，但不要留一條沒有執法的禁令——它會給人「這件事有在管」的錯覺（`E-31` 的形狀）。
- **防呆**：✅ 同一次交付內完成。新增 [`docs/tools/check-revision-summary.mjs`](tools/check-revision-summary.mjs)，
  已登記進 §2.5 第 4 環的「收尾必跑」。設計上的兩個取捨都寫在腳本檔頭：
  ① **只掃修訂摘要區塊**，不掃規格本體（本體的「移除 EXIF」是功能描述、「發票作廢」是 §2.5 明文允許的資料狀態值）；
  ② **只認「把舊值連同新值一起寫出來」這一種形狀**，刻意不做關鍵字掃描——
  實測關鍵字版在八份母檔產生 **7 筆誤判、0 筆真陽性**，而**誤判率高的檢查會被加上白名單、然後變成擺設**（`E-34`）。
  驗收用**本次的違規句子本身**當反例（`E-39` 的教訓：不是隨便塞個錯值看它會不會叫）：exit 1 → 復原 → exit 0。
  🔵 順帶掃出一筆**既有**違規並一併清掉：`v3.6` 修訂摘要的「§7 的 GEO **由「補充建議」改為**條列需求」。

> ⚠️ **這一筆與 `E-39` 的共同點**：兩者都是「**規定／意圖只存在於文字裡，沒有任何機制會因為違反它而失敗**」。
> `E-39` 是程式的兩份對照表沒人強制一致，這一筆是流程文件的禁令沒有執法。
> **可遷移的判準**：寫下一條「一律要／一律不准」的規定時，順手問一句——
> **違反它的那一刻，有什麼東西會變紅？** 答不出來，就代表寫下的是期望不是規定。

### E-42 一個欄位身兼「全稱」與「簡稱」兩種語境，頁首頁尾每一頁都印錯而關卡看不到（2026-09-23，`S0-9k`）

- **錯在哪**：`apps/web/shared/utils/club.ts` 的 `ClubAssets.nameZh`，**註解寫「中文簡稱」、實際值塞的是全稱**
  （`台中磐石足球俱樂部`）。同一個欄位同時被「該用全稱」的地方（頁尾標誌 `alt`、隊徽 `alt`、SEO 說明、麵包屑
  `aria-label`）與「該用簡稱」的地方（首頁「加入台中磐石」、關於頁「認識台中磐石」、頁首導覽、頁尾兩處）引用——
  **兩種語境共用一個值，必定有一邊是錯的**。命中 4 個檔案 7 處，其中 3 處在 `SiteHeader.vue`／`SiteFooter.vue`，
  也就是**每一頁**都印著「參與台中磐石足球俱樂部」。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：多俱樂部化把寫死的字串改成資料驅動時，
  只確認了**「這個欄位有沒有兩家的值」**，沒有確認**「引用這個欄位的每一處，各自要的是哪一種寫法」**。
  `check-club-copy.mjs` 正好也只驗前者——**它驗的是「有沒有填」，不是「填得對不對」**，
  於是 lint 全綠給出了「俱樂部文案這件事有在管」的信心。
  🔴 欄位名稱與註解其實**寫對了意圖**（簡稱），錯在賦值時沒照著註解走——
  **一個沒有被任何檢查讀到的註解，跟沒寫一樣。**
- **下次怎麼避免**：新增 `ClubText`／`ClubAssets` 欄位前，**先把所有引用點各自期望的 mockup 逐字值列出來、分組，
  再決定要開幾個欄位**。「一個俱樂部名稱只需要一種寫法」是會出錯的假設。
- **防呆**：✅ **比對範圍已於同日擴大到整個 `<body>`**（`S0-9m`），頁首頁尾從此進入關卡；首頁另由 `check-homepage-fidelity.mjs` 釘住。
  ⚠️ **但範圍擴大不等於這類錯誤不會再發生**——見下方升級段。

> 🔴 **這一筆真正的教訓不在欄位設計，在「它為什麼會被發現」**：
> `compare-dom.mjs` 之所以抓到，是因為同一個欄位誤用**剛好也命中了 `<main>` 裡的兩處**。
> **如果這個誤用只發生在頁首頁尾，這道關卡不會有任何反應**——
> `docs/14` 的不變量寫的是「**body** 的 DOM 結構、class 名稱、元素順序與文字內容一律不動」，
> 而工具只比對 `<main>`。**規定的範圍與執法的範圍差了一圈，而差的那一圈是「每一頁都有」的區塊。**
> 這是 `E-31`（局部套用的機制給出全面的信心）在**驗收工具**這個載體上的又一次現形：
> 不是工具做錯了，是**沒有人把「工具驗到哪」與「規定管到哪」對照過一次**。
> ✅ 已登記為 `STATUS.md` 的 `S0-9m`（把比對範圍擴到 header ＋ footer），並寫進 `docs/14` 那一條不變量本身——
> **寫在不變量旁邊而不是只寫在這裡，是因為下一個人是在「動手改東西前掃一次 `docs/14`」時需要知道這件事**，
> 而不是在事後檢討時。
> ⚠️ **可遷移的判準（跟 `E-41` 同一組）**：寫下一條「一律要／一律不准」的規定之後，
> 除了問「違反它的那一刻有什麼東西會變紅」，還要再問一句——**那個會變紅的東西，涵蓋範圍跟規定一樣大嗎？**


#### 🔴 `E-42` 升級（2026-09-23，同日第二、三次現形）：**擴大驗收範圍不是這一類的防呆**

**依 `CLAUDE.md` 全域規定 13，不新增 `E-43`**——同一類錯犯第二次要的是機制不是記錄。同一個根因當天又出現兩次：

| # | 形狀 | 在哪 |
|---|---|---|
| 2 | **欄位組錯句子**：`加入{{ identity.academyLabelZh }}` → 「加入足球學院」，mockup 是「加入學院」。`academyLabelZh`（足球學院）單獨當標籤是對的，被組進「加入…」就錯了 | `SiteFooter.vue`，全站 73 頁 |
| 3 | **欄位根本沒被引用**：`SiteHeader.vue` 學院 mega 選單的主標籤正確用 `identity.academyLabelZh`，**底下 7 個子項目與 1 個 CTA 全部字面寫死「學院」**，藍鯨站實測照樣印出來 | `SiteHeader.vue` ＋ 至少 9 個 `academy`／`join` 頁面 |

- **根因（維持不變）**：**一個欄位被當成「一個俱樂部名稱只需要一種寫法」來用。**
  第 2 次是同一個值被塞進兩種句型，第 3 次是連塞都沒塞。
- 🔴 **這次真正學到的是「什麼不是防呆」**：
  `E-42` 原本把防呆寄託在「把比對範圍擴大到 `<body>`」。範圍確實擴大了（`S0-9m`），也確實當場抓到第 2 次現形。
  **但同一天的獨立審查證明了它的天花板**：第 3 次現形（藍鯨選單）**就算範圍已經擴到整個 body 也抓不到**，
  因為 `compare-dom.mjs` 比對的是**「Nuxt 輸出 vs 磐石的靜態 mockup」**，不是「兩家俱樂部之間該不該一致」——
  **藍鯨沒有 mockup，這個維度上它沒有任何基準。**
  ⚠️ **可遷移的教訓**：**「擴大驗收範圍」與「讓錯誤不可能發生」是兩件事。**
  驗收範圍再怎麼擴，驗的都是「跟基準像不像」；欄位語意用對了沒，基準本身回答不了。
  真正堵住這一類的是**把「一個欄位只能代表一種語意」做進型別與資料結構**
  （這次新增 `academyJoinLabelZh` 就是這麼做的），不是任何一種擴大範圍的做法。
- **下次怎麼避免（維持並強化）**：新增 `ClubText`／`ClubAssets` 欄位前，
  **先把所有引用點各自期望的 mockup 逐字值列出來、分組，再決定要開幾個欄位**；
  並且**同時檢查有沒有該引用卻沒引用的地方**——第 3 次現形就是漏在這一半。
- **防呆**：🟡 **部分，且已知邊界**。
  ① 範圍擴大（`S0-9m`）涵蓋「磐石站對得上 mockup」這個維度；
  ② `check-club-copy.mjs` 涵蓋「兩家都有填值」，**不涵蓋「填得對不對」也不涵蓋「該填的地方有沒有去填」**；
  ③ **跨俱樂部一致性目前完全沒有自動檢查**——已登記為 `STATUS.md` 的 `S0-9n`，
  審查提出的可行做法是「拿磐石專屬詞彙表在 `NUXT_PUBLIC_CLUB=bw` 的 SSR 輸出裡整批 grep，命中即錯」，
  誤判風險在詞表本身而不在比對邏輯（比對的是已渲染的最終文字，不是猜程式碼意圖）。

> 🔵 **這次流程上做對的一件事，值得留著**：`S0-9m` 改的是**驗收關卡本身**，而實作者為了讓關卡通過**同時改了應用程式**
> （`default.vue` 改 Vue fragment）。這種情境下「交付者自我驗證」特別站不住腳，因此交付後**由產出者以外的角色做了一次獨立審查**——
> 正是 `E-31` 結尾寫的「若擋不住第三次，要改的是流程」。
> **那次審查的產出不是「確認這次修對了」，而是當場找到第 3 次現形**（上表第 3 列），
> 以及指出拆包裹條件「本來可以更窄卻沒有」（已收窄並實測巢狀 `id="teleports"` 現在會被報出來）。
> ⚠️ **判準**：當一次交付同時改了「被驗的東西」與「驗它的東西」，獨立複核不是加分項，是必要條件。


> 🔴 **`E-42` 第四次現形，以及它終於被一個「對的形狀」的機制接住（2026-09-23，`S0-9n`）**
>
> **依 `CLAUDE.md` 全域規定 13，仍不新增編號。** 第 4 次的形狀是**「欄位存在，但該引用的地方沒去引用」**：
> `SiteHeader.vue` 的學院 mega 選單**主標籤正確用了 `identity.academyLabelZh`**（藍鯨顯示「青年隊」），
> 但底下 8 個子項目與 CTA 字面寫死「學院」——**原始碼審查、型別、lint 全都連不上**，
> 因為程式碼本身沒有任何錯：一個字串常數不會因為「它應該是變數」而編譯失敗。
>
> ✅ **這次補的機制之所以是對的形狀，在於它換了一個觀測點**：
> [`check-club-brand-leak.mjs`](../apps/web/scripts/check-club-brand-leak.mjs) 不看原始碼，
> **看的是藍鯨站真的渲染出來的 SSR 輸出**——「藍鯨頁面上不該出現磐石專屬詞彙」是一個
> **確定性的事實**，不需要推測程式碼意圖，所以誤判率的風險落在**詞表**而不在比對邏輯。
> ⚠️ **可遷移的判準**：**當一類錯誤的特徵是「程式碼本身沒有錯」時，再嚴格的靜態檢查都接不住它，
> 要換的是觀測點，不是檢查的嚴格度。**
>
> 🔴 **但同一次交付裡，`E-42` 的根因又差點當場第五次現形**：實作者為了修這 8 處，
> 複用了 `academyJoinLabelZh`——那個欄位的 JSDoc 逐字寫著「SiteFooter『加入＿＿』複合句**專用**」，
> 卻被拿去承載「＿＿總覽／隊伍／發展路徑／教練團／生活／新聞」六種非 join 句型。
> **替換內容是對的，錯的是欄位名稱與文件不再描述它承載的東西**——跟 `E-42` 原始那一筆
> （`nameZh` 註解寫「簡稱」實際存全稱）是**同一個形狀**。已當場退回改名為 `academyShortLabelZh`，
> 並釐清分界：`academyLabelZh` 是**全名**（足球學院／青年隊）、`academyShortLabelZh` 是**短名**（學院／青年隊），
> **差別是全名／短名，不是 join／非 join**。
> ⚠️ **這件事本身就是證據**：我們在同一天早些時候才把 `E-42` 升級成「一個欄位只能代表一種語意」，
> 幾個小時後就在同一個欄位家族上差點重演。**寫下紀律不會讓人遵守紀律**——
> 擋下它的是**交付後的逐行複核**，不是那條紀律本身。這與 `E-31` 結尾、`S0-9m` 的獨立審查指向同一件事：
> **這一類根因目前唯一有效的防線是「產出者以外的人看過」**，不是任何一條寫在文件裡的原則。

### E-43 用 `/healthz` 去確認資料庫連線，白跑兩輪比對（2026-09-23，`S0-9i`／`S0-9k` 驗證期間）

- **錯在哪**：本機起 `apps/api` 驗證前台頁面時，用 `curl /healthz` 回 `{"status":"ok"}` 就認定
  「API 好了」。實際上連線字串是從 `deploy/dev/club.env` 取來的——那份是給 **Docker 容器**用的
  （`Server=host.docker.internal`），宿主機上 `dotnet run` 根本連不到資料庫。
  結果 `compare-dom.mjs` 多噴 `zh/schedule/` 與 `zh/about/our-people/` 兩頁紅燈
  （頁面渲染成「共 0 場」、篩選器整組空白），**一度被當成真的搬遷失真去追**，白跑兩輪。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**拿 liveness 探針當 readiness 探針用。**
  `/healthz` 的設計本來就只回報「行程活著」，**它不碰資料庫**——這不是 API 的 bug，是我選錯端點。
  真正該用的是 `/readyz`（會實際開連線查 `SELECT 1`），或直接打一個會查資料的端點。
  🔴 更深一層：**我要驗的是「資料拿得到嗎」，卻去問了一個不回答這個問題的東西，
  然後把它的「ok」當成我要的那個答案。**
- **下次怎麼避免**：**驗證某件事之前，先問「我打算看的這個訊號，涵蓋範圍跟我要確認的事一樣大嗎」。**
  要確認資料拿得到，就打一個**真的會回資料**的端點並看回應內容，不要只看狀態碼或 `ok` 字樣：
  ```bash
  curl -s http://127.0.0.1:5299/api/v1/tcrfc/schedule | head -c 200   # 要看到真的賽程 JSON
  ```
- **防呆**：✅ 已寫進 [`apps/api/README.md`](../apps/api/README.md) 的本機跑法一節
  （含「`deploy/dev/club.env` 是給 Docker 用的、直接 `dotnet run` 要改 `127.0.0.1` 並補 `Encrypt=False`」
  這個連帶的坑，以及可直接抄的一行版取密碼指令）。⚠️ **沒有自動化檢查**——
  這是「人選錯觀測點」的問題，不是程式可以攔截的。

> ⚠️ **這一筆跟 `E-42` 升級段的判準是同一條，只是換了一個載體**：
> 那裡問的是「**那個會變紅的東西，涵蓋範圍跟規定一樣大嗎？**」，
> 這裡問的是「**那個回 ok 的東西，涵蓋範圍跟我要確認的事一樣大嗎？**」。
> 前者關於防呆設計，後者關於臨場驗證——**同一個思考習慣的兩種用法**。

### E-44 派工要求做 `J3` 稽核，卻沒先查 `docs/12` §13.1 記著的「委託方指示不建日誌表」（2026-09-23，`S1-3` 登入與權限）

- **錯在哪**：派 `backend-engineer` 做後台登入與權限時，我把規劃書 §4.10 的 `J3 稽核與備份` 整條寫進任務範圍，
  下游因此新增了 `admin_login_logs` 與 `admin_audit_logs` 兩張表、寫進 `db/club-schema.sql` 與本機庫。
  但 [`12-database-schema.md`](12-database-schema.md) **§13.1 逐字記著一條委託方指示**：
  「本次資料庫設計不含 log」，明定**不建** `AuditLog`／`LoginLog`／`ExportLog`／`OperationLog`，
  並列出 **8 處與規劃書衝突的條文**與**八項直接後果**。**我要求做的，正是客戶明文說不要的那件事。**
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**我把「規劃書寫了」當成「這件事該做」。**
  規劃書是規格的唯一真實來源沒錯，但 §13.1 記的是**客戶對範圍的指示**——
  它不改規劃書的條文，而是**宣告某一段條文本期不實作**，並把落差與後果留在導航層。
  🔴 這種「規格有、但被刻意不做」的東西**只存在於 `docs/`，規劃書裡查不到**
  （這正是 `15-out-of-scope-record.md` 與 §13.1 存在的理由）。
  我照 `00-harness.md` 的行號對照表去讀了 §4.10，**卻沒有反向問一句「這一段有沒有被登記為不做」**。
- **下次怎麼避免**：🔴 **把規劃書某一節寫進任務範圍之前，先查那一節有沒有被登記成「本期不做」。**
  要查的兩個地方是 [`15-out-of-scope-record.md`](15-out-of-scope-record.md) 與
  **`docs/12` §13（資料庫的刻意落差）**。⚠️ **「規劃書寫了」不等於「現在要做」**——
  兩者之間隔著一層客戶的範圍指示，而那層只記在導航層。
- **防呆**：⚠️ **無自動化檢查。** 已在本筆寫明要反查的兩個位置。
  🔵 **這次真正擋下它的是下游**：`backend-engineer` 在報告裡明確把這個衝突標為「需要你裁決的政策衝突」，
  而不是默默照做或默默不做。**那正是 `E-34` 升級段第 1 點要的行為**——
  上游的指示也是一種轉述，一樣會過期、一樣要回查。

> ⚠️ **這一筆的連帶問題（同一次交付內已處理）**：新增那兩張表時，
> **DDL 先改、`docs/12` 沒動**，違反 `CLAUDE.md` 目錄地圖明文的
> 「**綱要的真實來源是 `docs/12`，改綱要要先改文件再改 DDL**」。
> 因為使用者裁決撤回那兩張表，撤完之後 DDL 會自動回到與文件一致，不必倒著補文件；
> **但下次新增表一定要先改文件**。
> 🔵 順帶記一個**數字上的錯**：我用 `grep -c "\[Fact\]\|\[Theory\]"` 數測試數量得到「70 個測試」寫進派工單，
> 實際基準是 **99 個測試案例**——`[Theory]` 一個屬性會展開成多個案例，**我數的是屬性不是案例**。
> 下游回查後指出來。**用 grep 數東西時，要先確認「我數的單位」跟「我要講的單位」是不是同一個。**

#### 🔴 `E-39` 升級（2026-09-23，`S1-3` 授權型別層強制）：**「用那個錯誤本身當反例」不夠——那個錯誤往往不只一種形狀**

**依 `CLAUDE.md` 全域規定 13，不新增編號。** `E-39` 原本的教訓是
「**新增防呆的驗收條件，是用那個錯誤本身當反例，不是隨便塞個錯值看它會不會叫**」。
這條是對的，但這次暴露了它的下一層。

**這次發生什麼**：後台授權改成型別層強制時，`internal` 建構子擋不住同組件內的直接建構
（C# 的 `internal` 是**組件**範圍不是命名空間範圍），交付者誠實標出這件事，並補了一支
`ArchitectureTests` 掃原始碼當 CI 層防線，宣稱「已用真實違規形狀驗證真的抓得到」。

**主 session 複驗，一驗就穿了兩次**：

| 注入的寫法 | 能編譯 | 那支測試 |
|---|---|---|
| `new Tcrfc.Api.Security.AdminClubScope(default, default)`（完整命名空間） | ✅ | ❌ 沒抓到 |
| `=> default;`（`readonly struct` 零值，**完全不經建構子**） | ✅ | ❌ 沒抓到 |

成因是檢查比對固定字串 `new {typeName}(`：完整命名空間讓 `new ` 後面接的不是型別名，`IndexOf` 直接 -1；
而 `default` 更根本——**C# 的 `readonly struct` 永遠可以取零值，不管建構子是什麼存取層級**，
任何「掃 `new`」的做法天生抓不到。
⚠️ **同一個洞也在 `S0-7b` 的原始 `ClubScope` 上**，不是這次造成的。

- **根因（`E-39` 之外的新增部分）**：**交付者驗證的是「我想到的那個形狀」，而不是「這一類的形狀空間」。**
  他的驗證過程完全誠實、也真的跑過——但**反例清單是自己列的，而列清單的人正是實作的人**，
  於是漏掉的恰好是他沒想到的那幾種。這不是態度問題：
  **一個人想不到的變形，他自己再怎麼認真驗也驗不出來。**
- **升級後的要求**：
  1. 🔴 **新增防呆時，反例至少要有兩種以上語法變形**，並在交付說明裡**明確回答一句：
     「這份反例清單是怎麼確認涵蓋夠廣的？」** 答案若是「我想到這些」，就照實寫——
     **照實寫不丟臉，包裝成已窮盡才會出事。**
  2. 🔴 **基於「掃字串」的檢查，預設視為可繞過**，除非能說明為什麼該語法空間是封閉的。
     可行時優先用**語意層**工具（此例是 Roslyn 分析），而不是逐行比對。
  3. 🔴 **防呆的驗收，交由實作者以外的人再穿一次。** 這一次擋下來的正是這個動作——
     與 `E-31` 結尾、`E-42` 升級段、`S0-9m` 的獨立審查指向同一件事：
     **這一類問題目前唯一有效的防線是「產出者以外的人看過」**，不是任何一條寫在文件裡的原則。
- **防呆**：✅ **同一次交付內完成，而且換了解法的性質**。
  ① **`default` 那個洞從型別本身解掉**：`ClubScope`／`AdminClubScope` 由 `readonly struct` 改為 `sealed class`——
  `struct` 的 `default` 依語言規範**保證不呼叫任何建構子**，存取層級再嚴都沒用，**那不是掃描邏輯不夠好的問題**；
  改 class 之後偽造值是 `null`，一用就 `NullReferenceException`，不是悄悄拿到一個看起來合法的零值。
  ② **掃描改用 Roslyn 語意模型**（不是字串比對）：比對編譯器解析後的型別符號，
  完整命名空間、using 別名、逐字識別碼、目標型別 `new()` 等表面變形一次收斂，另偵測三個反射繞過 API。
  ③ **主 session 用三種形狀複穿**：完整命名空間 ✅ 抓到、`default` ✅ 抓到、`GetUninitializedObject` ✅ 抓到並標出行號。
  🔵 **交付者對「涵蓋夠廣嗎」的回答值得留著當範本**：
  「**不是窮舉出來的，我不包裝成窮舉**。信心來源是解法的性質改變了——第一版比對文字，涵蓋面等於我想到的字串樣式；
  第二版比對型別符號，所以對語法表面變形無感。」並主動劃出仍然做不到的邊界（`unsafe` 指標轉型、`Marshal.PtrToStructure`、手刻 IL）。

> ⚠️ **另記一筆交付者主動回報的操作失誤，方向對、值得留**：
> 驗證「編譯會不會失敗」時把違規程式碼直接寫進**有未提交異動的追蹤檔案**，
> 事後用 `git checkout -- <file>` 復原——那會還原到**最後一次 commit**，
> 於是整輪未提交的工作被打回原始（連已刪除的 `DevWriteGate` 都回來了）。
> 已自行發現並重建，且主 session 複核過重建結果乾淨（零死碼殘留、7/7 端點都授權）。
> **正確做法**：「暫時破壞某個檔案來驗證檢查抓不抓得到」一律用**獨立的 scratch 檔案**，
> 不要對有未提交異動的追蹤檔案動 `git checkout`。

### E-45 `dotnet ef migrations add` 產生三個檔，只 commit 了兩個，之後兩次改綱要都沒有 migration（2026-09-24，`S0-9l` 發現；源頭 `d5ec4ec`／`e67ef26`）

- **錯在哪**：`S0-7f` 建 EF Core 基準 migration（`InitialBaseline`）時，只把 `.cs` 與 `.Designer.cs` 放進版控，
  **`ClubDbContextModelSnapshot.cs` 從來沒有 commit**。少了 snapshot，下一次 `dotnet ef migrations add`
  會拿**空模型**當比較基準，產出一個把整個綱要重建一遍的 migration（`S0-9l` 實測 **145 個 `CreateTable`**，已刪除）。
  接著 `e67ef26`（`AdminRefreshToken`）與 `S0-9l`（`matches.original_match_on`／`original_kickoff`）兩次改綱要，
  都是手改 scaffold 檔、對本機庫直接 `ALTER TABLE`，**沒有任何 migration**。
  [`20-cicd.md`](20-cicd.md) §5 定的正式庫變更路徑是「每次改動都是一個新 migration」——**照現況，這條路徑走不通**。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**交付時只驗「程式能編譯、測試會過」，沒驗「下一個人照流程操作還走得通」。**
  測試接的是從 `db/club-schema.sql` 建出來的資料庫，不經過 migration，所以缺 snapshot 不會讓任何測試變紅；
  而 `e67ef26` 的交付者遇到同一個坑時，選擇繞過（手改＋`ALTER TABLE`）卻沒有回報，
  **繞過一次沒記下來，第二個人就只能再繞一次。**
- **下次怎麼避免**：① 🔴 **碰到 `apps/api/Data/Migrations/` 的交付，驗收時要實際跑一次 `dotnet ef migrations add <暫名>`，確認產出的是空 migration 再刪掉**——這才是在驗證「基準成立」，只看 git 裡有沒有檔案不算。
  ② **改綱要時，除了 `db/*.sql` 與 scaffold 檔，也要產生對應的 migration**；做不到就在交付報告寫明原因，並在 `STATUS.md` 開一列。
- **防呆**：✅ **同一次交付內完成（`S0-7j`，2026-09-24）**。CI 的 `api` job 加了兩道檢查：① 有 `Migrations/*.Designer.cs` 就必須有 `ClubDbContextModelSnapshot.cs`；② `dotnet ef migrations has-pending-model-changes`，模型改了卻沒有 migration 就紅燈。兩道都用真實的錯誤形狀驗過：刪掉 snapshot、改模型不補 migration，都會紅燈。主 session 另外把 snapshot 移走再跑一次，確認第 ② 道也抓得到。新增 migration 的驗收程序寫進了 [`20-cicd.md`](20-cicd.md) §5。
  🔵 **這次擋下來的是下游**：`backend-engineer` 發現產出 145 張表時沒有 commit，而是刪掉、改用與先例一致的做法完成任務，並把根因回報上來。

### E-46 `dotnet ef migrations remove --force` 以為只刪檔案，實際對本機庫執行了 `Down()`（2026-09-24，`S0-7k`）

- **錯在哪**：`S0-7k` 要重新產生兩支還沒 commit 的 migration，派工單寫「remove 後依序重新 add」。
  對已套用的 migration，`remove` 預設會拒絕，下游因此加了 `--force`。**但 `--force` 不只刪檔案，還會先對連線中的資料庫執行那支 migration 的 `Down()`**，
  結果 `tcrfc_club_dev.matches` 的 `original_match_on`／`original_kickoff` 兩欄被刪掉。
  下游當場發現，用 `database update` 重新套用，把兩欄補回來了（migration ID 因此改為 `20260924014130`）。
  另一支 `AddAdminRefreshTokens` 如果照同樣方式處理，就等於 `DROP TABLE admin_refresh_tokens`，那張表裡已經有真實資料。下游改成手改檔案，所以沒有發生。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**我派工時只寫了「重新產生 migration」這個目標，沒有寫「過程中不准碰資料庫結構」這條邊界**，
  而 `remove` 會不會動到資料庫，要看那支 migration 是否已經套用。**我在派工單上告訴下游「本機庫已套用這兩筆」，卻又叫它 remove，這兩句本來就互相衝突。**
- **下次怎麼避免**：🔴 **已套用到任何資料庫的 migration，一律不用 `remove`／`remove --force` 處理。** 要改內容，就手改檔案或另加一支新 migration。
  派工只要會碰到 migration，就寫明「**不得對任何資料庫執行 DDL，除非本單明文要求**」。
- **防呆**：⚠️ **無自動化。** 這個坑已寫進 [`20-cicd.md`](20-cicd.md) §5。主 session 事後複驗：`dotnet test` 135／135 通過，`has-pending-model-changes` 回報模型與 snapshot 一致。
