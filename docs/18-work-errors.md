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
| E-66 | 2026-09-29 | `sed -i 's/<h4>/<h4 aria-level="2">/g'` 全域取代時，連自己剛寫進同一個檔案、內文提到 `<h4>` 字面值的說明註解也一併取代掉，註解變成「引用已經套用修正後的寫法在描述修正前的狀態」，自相矛盾 | ⚠️ 無（改用 Edit 工具做精確字串取代前先確認註解裡沒有同樣的字面值，或註解與程式碼分兩次下手） |
| E-67 | 2026-09-29 | 派 S1-16 時，主 session 在派工指示裡把「藍鯨一線隊近期賽果」寫成可以接的範例；規劃書 §3.6「不含功能」明文排除藍鯨賽果，agent 照指示做出一個違反規格的區塊 | ⚠️ 無自動化；已移除區塊，派工前讀規格「不含功能」欄（見條目） |
| E-68 | 2026-09-29 | S1-12d 收尾新增 `useSiteFacts.ts` 時，doc comment 裡舉例寫了字面值「U15／U14／U12」，`lint:fact-single-source` 當場攔下（它掃全文字內容，不分程式碼與註解） | ✅ 已生效——當場改寫成不含字面值的敘述；防呆已存在（該 lint 腳本本身），只是寫作習慣要跟上 |
| E-69 | 2026-09-29 | S1-12d 收尾第二輪把 `club-copy.ts` 的 `HOME_HERO` 從 `export const` 改成 `export function getHomeHero(...)` 時，沒有先確認除了 Vue 頁面之外還有誰在讀這個匯出名稱——`scripts/check-homepage-fidelity.mjs` 用純文字掃描 `export const HOME_HERO` 這個字面模式核對 mockup 逐字值，改名後找不到宣告，`npm run lint` 當場報錯 | ✅ 已修（改寫該腳本改為解析 `export function getHomeHero` 函式本體的最後一個頂層 `return { ... }`）；下次改動 `club-copy.ts` 任何匯出名稱或型態（const↔function）前，先 `grep -rn` 整個 `apps/web`（含 `scripts/`，不是只看 `app/`）找出所有讀取者，這類手刻的正規表示式掃描腳本不會被 TypeScript 型別檢查涵蓋，改名不會在編譯期出錯 |
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
| E-37 | 2026-09-22<br>2026-10-02 | 響應式驗證只量 `document.documentElement.scrollWidth`，量不到**捲動容器內部與浮層**的溢出：表格（第一、二次）、`el-dialog` 內嵌寬度 640px 在 390px 被切掉（第三次，2026-10-02） | ✅ [`scripts/overflow-probe.mjs`](../scripts/overflow-probe.mjs)（逐元素掃描，含浮層；需連著開除錯埠的 Chrome 使用） |
| E-38 | 2026-09-22 | `BlobImageStorageService` 的「容器已確保存在」旗標在呼叫 `CreateIfNotExistsAsync` **之前**就設成完成，第一次呼叫因故失敗後，旗標仍卡在「已完成」，之後每次呼叫都跳過建立、直接對不存在的容器寫入，得到的錯誤變成「容器不存在」蓋掉了真正的根因 | ✅ 改用 `SemaphoreSlim` 包住整段，`CreateIfNotExistsAsync` 成功後才設旗標 |
| E-57 | 2026-09-25 | 規劃書與 `STATUS.md` 已經有答案的事，還拿去問使用者（第二次；第一次是把 `previousStartDate` 等實作選項丟給使用者） | ✅ 升級為 `CLAUDE.md` 全域規定第 14 條 |
| E-58 | 2026-09-25 | S1-10 公開表單送出端點新增 Rate Limiting（依 IP 分區、5 分鐘固定視窗），`PermitLimit` 只用「正式環境訪客合理送出頻率」估出 10，沒有同時檢查「同一支整合測試檔案會呼叫這個端點幾次」——`WebApplicationFactory` 測試的所有請求共用同一個 `RemoteIpAddress`（TestServer 沒有真實連線），本輪新測試檔 11 次公開送出呼叫在同一視窗內就把額度用完，其中誘捕欄位測試收到 429 而非預期的 200，`dotnet test` 出現 1 項失敗 | ✅ 門檻改為 20 並在 `Program.cs` 對應段落寫清楚估算依據（含目前測試呼叫量），下次新增依連線分區的 Rate Limiting 政策時，門檻值要同時滿足「正式環境防護意義」與「同一分區內整合測試呼叫總量」兩個條件 |
| E-59 | 2026-09-25 | S1-10 在 `form_fields` 加了篩選唯一索引，種子腳本 `generate-club-seed-sql.py` 卻沒補 `SET QUOTED_IDENTIFIER ON`，重灌種子會 `Msg 1934` 失敗（S1-11 發現） | ✅ 腳本檔頭已補，比照慈善種子腳本 |
| E-60 | 2026-09-25 | 把「規劃書沒有逐字要求某個型別雙語」當成「不用做」的理由，忽略了 CLAUDE.md 全域規定第 4 條是跨全站的通則，不需要規劃書逐項重申（2026-09-22 判斷、驗收退回才發現，`form_fields` 缺題目文字，S1-10） | ⚠️ 無自動化檢查，靠人工比對規劃書與 `docs/12c` |
| E-61 | 2026-09-25 | 新增依角色分權限的功能（G2 指派負責人）時，姓名來源直接沿用系統管理員專屬的既有端點（`/admin/accounts`），沒有替其他持有處理權限的角色開等價的窄範圍端點（2026-09-22 判斷、驗收退回才發現，S1-10） | ⚠️ 無自動化檢查，已補 `ListAssignableUsersAsync` 供之後同類需求參考既有寫法 |
| E-62 | 2026-09-25<br>2026-09-25 | 整合測試寫進共用開發庫的資料沒有清乾淨：清理呼叫失敗被靜默吞掉（29 篇孤兒文章）；**同日第二次**：兩支 FAQ 測試完全沒寫清理，累積 155 筆，擠掉另一支測試的前 50 名排行而失敗，還被誤判成「原本就會失敗」 | ⚠️ 已寫進 `docs/14`；無自動化 |
| E-63 | 2026-09-25<br>2026-09-25 | S1-13 把 `SiteHeader.vue`／`SiteFooter.vue` 78＋19 處連結改成呼叫 `lp()` 換算語系，但 `<script setup>` 只解構了 `const { locale, switchTo } = useLocale()`，漏了 `lp` 本身，本機真實 `curl` 首頁時才炸出 `_ctx.lp is not a function`（500） | ✅ 交付前用真實 `curl` 兩站 zh／en 抓 HTML 才發現，補上解構後重測通過。**防呆已補（2026-09-25，S1-13 缺口①）**：`apps/web/scripts/check-undefined-template-refs.mjs` 已掛進 `npm run lint`，跑 `nuxi typecheck`（不是裸 `vue-tsc`——裸的解不開 Nuxt 自動匯入，見腳本檔頭），只挑「訊息含 `ComponentInternalInstance` 的 `TS2339`」與「`TS2304`／`TS2552`」這兩種錯誤形狀（樣板用了 script setup 沒解構出來的識別字），不受本專案既有型別債（`useFetch().items` 等 `TS2339`／`TS7006`）干擾，不需要維護 baseline。已用故意刪掉一個檔案的 `const { lp } = useLocale()` 實測紅燈、補回後綠燈 |
| E-64 | 2026-09-29 | S1-14 發現既有落差：`Features/Home/HomeRepository.cs` 的 `ListBannersAsync` 直接回傳 `banners.image_key` 原始 Blob 物件鍵，沒有像 `StaffRepository`／`PlayersRepository` 一樣注入 `IImagePublicUrlResolver` 解成完整網址——首頁若真的接上 Banner 輪播圖片，前端拿到的 `imageKey` 目前無法組出正確網址（沒有 Blob 容器網址可以自己兜，猜的話會顯示壞圖） | ⚠️ 無自動化；本輪前端只用 `banners` API 的純文字欄位（`cta1Label`／`cta1Url`），暫不消費 `imageKey`，待後端補上解析後再串圖片，見 `apps/web/README.md`「S1-14」節 |
| E-70 | 2026-09-29 | S1-17 主輪交付表單中心時，把「mockup 欄位遠多於後端 `form_fields` 定義」的落差處理成「畫面留著、悄悄不送出」，而不是把多餘欄位從畫面移除——使用者填了看得到，卻不知道不會被收到，違反「個資只收必要的」；同一輪 `camp_registration.health_declaration`（後端是 consent 布林型別）又把使用者填的健康聲明自由文字塞進不相關的 `contact`（緊急聯絡人）欄位，同樣是「畫面呈現與實際送出行為不一致」 | ✅ 收尾修正已改：7 張表單逐一清點移除多餘欄位（含全部檔案上傳 fieldset），`health_declaration` 改成語意對應的勾選框；⚠️ 無自動化檢查能抓「畫面欄位是否等於送出欄位」這件事，下次新增或接後端表單時，欄位對應表要先核對規格＋後端定義的交集，再決定畫面要顯示哪些欄位，不要先照抄 mockup 全部欄位再回頭篩選 |
| E-71 | 2026-09-29 | 前台表單代理以 `X-Forwarded-For` 的第一個值當訪客 IP 轉給 api；Cloudflare 會保留訪客自送的 XFF、Caddy 對已信任上游是附加，第一個值可被偽造，表單限流可被繞過 | ✅ 改讀 Caddy `header_up X-Real-IP {client_ip}`，見條目 |
| E-72 | 2026-09-29 | S1-15 建了 13 賽事行事曆（`app/pages/zh/schedule.vue`）卻沒有把它補進 `shared/utils/site-units.ts` 的 `SITE_UNITS`，導致 `sitemap.xml`／`llms.txt`（兩者共用 `getEnabledSiteUnits`）從建成那天起就漏收這個單元；S1-18 本輪核對 12 FAQ 該補進同一份清單時才連帶發現。**S1-18b 複查時再連帶發現第二筆同類缺漏**：10 加入與聯絡（S1-17 建置完成）同樣沒有補進 `SITE_UNITS` | ✅ **S1-18b（2026-09-29）已補齊 `10`／`13` 兩筆並實機驗證兩容器 `sitemap.xml`／`llms.txt` 皆收錄**；✅ **防呆已自動化**：`apps/web/scripts/check-site-units-coverage.mjs`（掛進 `npm run lint` 的 `lint:site-units-coverage`）掃描 `app/pages/zh/` 全部 `definePageMeta({ unit: 'XX' })`，取頂層代碼比對 `SITE_UNITS` 或腳本內 `EXCLUDED_TOP_LEVEL_UNITS` 排除清單，兩者都沒有就讓 `lint` 失敗——已用「暫時拿掉 `SITE_UNITS` 的 `'13'`」實測紅燈、改回綠燈 |
| E-77 | 2026-09-29 | BW-C1 品牌外洩全站盤點改寫 `check-club-brand-leak.mjs` 詞表時，第一版把裸網域 `tcrfc.tw` 列進詞表，實測發現藍鯨站**每一頁**（含完全乾淨的頁面）都命中一次——根因是 `nuxt.config.ts` 的 `blueWhaleSiteUrl: 'https://bw-stg.tcrfc.tw'` 這個 runtime config 預設值會被序列化進**每一頁**的 hydration payload，而這個網址本身是藍鯨自己的 staging 網域（兩站共用上層網域），不是磐石網域外洩，裸字串對這個問題完全沒有鑑別力 | ✅ 改用含 `www.` 前綴的 `www.tcrfc.tw`（磐石舊站實際寫法），`bw-stg.tcrfc.tw` 沒有 `www.` 前綴不受影響；下次要在詞表裡放「網域」這種候選詞前，先假設它會撞到 runtime config 或環境變數的預設值，全站每頁跑一次再看命中是否有鑑別力，不要只看命中頁面本身像不像真的問題 |
| E-78 | 2026-09-29 | BW-C1 把 `shop/home-jersey-2026/index.vue` 商品詳情整段內容（含唯一的 `<h1>`）用 `v-if="isTcrfc"` 隱藏、bw 版只留一段 `<p>` 空狀態文字，沒有另外補 `<h1>`——`check-heading-structure.mjs` 實測跑 bw 容器時抓到「沒有 H1」，是本輪才發現的新迴歸，不是既有缺口 | ✅ 已在 bw 空狀態分支補上對應的 `<h1>`；下次把整段內容（含標題層級）用 `v-if`/`v-else` 拆成兩個分支時，兩個分支都要自己滿足「恰好一個 H1」，不能預設「反正原本有 H1，藏起來的那半邊不用管」——`check-heading-structure.mjs` 就是為了抓這一類回歸而存在，改完content gating 一定要實測兩個 club 容器都跑一次，不能只跑改動的那一邊 |
| E-79 | 2026-09-30 | 主 session 給使用者的本機 API 啟動步驟漏了 `JWT_SIGNING_KEY_CLUB`（照抄 API README 範例，範例本身也漏），API 照常啟動但每支端點（含 `/healthz`）都回 500 | ✅ README 範例補上；✅ 程式端已於 2026-09-30 在 `Program.cs` `builder.Build()` 前驗證 |
| E-81 | 2026-09-30 | 後台種子擴充前審計發現：三處整合測試對「共用資料庫種子長什麼樣」寫死假設——`AdminSeoImageTests` 的 `finally` 無條件刪掉 tcrfc 全部 `seo.*`／`tracking.*` 設定（註解假設「執行前理論上都不存在」）、`Standing_CSV匯入_整季替換` 斷言 `DeletedCount == 1`、`SiteFactsTests` 斷言電話與營業時間為 null；種子一擴充，前者靜默吃掉種子、後兩者直接失敗 | ✅ 三處已改成「先記下既有列、結束後還原」或不綁定種子值；⚠️ 無自動化（改種子仍要靠審計，見條目） |
| E-82 | 2026-09-30 | `GeoCrawlerDefaults` 把未成年照片頁強制排除只寫給 `tcrfc`，註解稱「`bw` 尚未建置對應頁面、待路由落地才補」，實際藍鯨與磐石共用同一套前台路由，`academy/teams/` 早已渲染藍鯨青年隊（U15／U12）名單；磐石的 `academy/life/` 與三個課程照片頁也漏列 | 「頁面存不存在」以規劃書／STATUS 的「藍鯨尚未開發」推論，沒有 grep `apps/web` 的實際路由；個資防線清單也沒有對照 `apps/web` 逐頁核對 | 見下 |
| E-83 | 2026-09-30 | 藍鯨站無條件輸出磐石照片（多含未成年學員）與磐石標誌，品牌外洩檢查（`check-club-brand-leak.mjs`）從 BW-C1 起已是全站 hard-fail，卻**全程沒有發現**：導覽下拉 7 張 `nav-*.jpg`（每個藍鯨頁面都有）、`about/*` 九頁與 `news/*` 八頁 hero、`academy/` hero（未成年學員）、`club/{opportunities,player-stories}` hero（Trenčín 交流）、首頁四大支柱四張圖、`partners/our-partners` 三家磐石國際夥伴隊徽、厚底緩震機能襪 7 張商品照（**每張右上角都印有 TCRFC 標誌**，BW-C1 卻誤稱「無隊徽通用配件」）、新聞卡與文章頁無封面時的磐石標誌佔位 | 檢查只比對**文字詞表**，而 `<img alt="">`、檔名、`background-image` 不含任何詞表用字；「哪些單元對藍鯨關閉」只針對頁面內容，沒有人逐張看過共用版位（導覽、hero、卡片）放的是誰的照片。BW-C1 更在 `index.vue` 註解與 README 寫下「通用足球場景照、不涉及任何俱樂部辨識內容」——**沒有實際打開圖片就下的斷言** | ✅ 新增 `check-club-image-leak.mjs`（與品牌詞彙檢查共用 `scripts/lib/collect-routes.mjs` 的同一套路由），掃 `<img>`／`<source>`／`<video poster>`／`style` 與樣式表 `url()`／`og:image`／JSON-LD／icon link，白名單制、不確定歸磐石；新增 `ClubHeroBg`／`ClubImg` 元件與 `hasNewsCover(slug, club)`。詳見下方 E-83 節 |
| E-84 | 2026-09-30 | 公開行事曆讀場地名稱的 SQL 用 `NULL AS Text2`，SQL Server 推成 int，Dapper 對不上 `I18nTextRow(string? Text2)` 建構子，只要自建活動有指定場地就 500；種子補上這類資料後才被測試抓到 | ✅ 改 `CAST(NULL AS nvarchar(1))`；`CalendarPublicTests` 以種子資料覆蓋 |
| E-85 | 2026-09-30 | 新增子表列時把帶 `Guid` 主鍵（資料庫有 `DEFAULT NEWID()`）的實體只加進父實體的導覽集合（`proposal.ProposalFiles.Add(new ProposalFile { Id = Guid.NewGuid() … })`），EF 因為 `Id` 已有值而當成既有列，送出 `UPDATE`（連 `row_seq` 一起更新），資料庫回「Cannot update identity column 'row_seq'」，端點 500 | ✅ `AdminBusinessUploadTests` 提案檔案上傳案例覆蓋；⚠️ 無 lint（寫入端一律 `dbContext.XxxSet.Add(...)`） |
| E-86 | 2026-09-30 | 把 `dotnet ef dbcontext scaffold` 的新輸出用 diff「只取新增行」合併進手改過的 `ClubDbContext.cs`：19 個合理新增之外還夾帶 17 段重複／無關的屬性設定（`Article.CanonicalPath`、`Banner.Status`、`Faq*` 等），因為先前的 agent 把那些設定分組放在檔案別處，與 scaffold 排列不同，diff 判成「新增」 | ✅ 合併前先看 `git diff`、發現後以關鍵字白名單重做；⚠️ 無腳本（見 E-86 節的固定做法） |
| E-87 | 2026-09-30 | 補種子測試帳號前沒有先 grep 既有的 `ADMIN_USERS`，另加了 `business.sponsor@`／`pr.media@` 兩個帳號，結果 `business.sponsorship@`／`pr.media@` 早已存在（S1-10／S1-11 補過），差點留下兩組功能重複的帳號 | ✅ 種子執行後以 `SELECT` 核對角色與帳號清單才發現，已從腳本與資料庫移除；⚠️ 無 |
| E-88 | 2026-09-30 | `Common/IcsBuilder.FoldLine` 的「不切斷 UTF-8 字元」迴圈讀 `bytes[offset + limit]` 沒檢查是否已到陣列尾端，**任何需要折疊（超過 75 位元組）的行，最後一段必定丟 `IndexOutOfRangeException`**；單一賽事下載的標題與地點都很短所以從沒觸發，S2-6 訂閱 feed 第一次輸出長行才 500 | ✅ `IcsBuilderTests` 長行折疊四組參數（含剛好落在 75 位元組邊界者） |
| E-89 | 2026-09-30 | C1 批的第一版 `AdminCultureTests` 在 `finally` 無條件刪掉種子的 `comic.%` 設定，與 `E-81` 同一類（測試以「這批鍵理論上不存在」當還原手段），測試仍綠燈、種子被吃掉 | ✅ `C1Test.SnapshotSettingsAsync`＋`AdminC1MiscTests.種子基線_C1示範資料沒有被測試清理吃掉` |
| E-90 | 2026-09-30 | 後台時間欄位 UTC 轉換對 `Kind=Unspecified` 呼叫 `ToUniversalTime()`（當本機時間換算），與 EF 讀回、JSON 不帶 `Z` 的慣例不符，時間差 8 小時 | ✅ `AdminDrawsTests`／`AdminCultureTests` 時間往返斷言 |
| E-91 | 2026-09-30 | ① K5 抽獎資格只收 `active` 會籍，漏了「狀態已過期但日期涵蓋抽獎快照日」的會員；② 成本欄位權限先憑印象定 sysadmin 專屬，沒先查 `docs/12b` §7（實為 sysadmin＋商務） | ⚠️ ①測試覆蓋；②無（靠實作前查表） |
| E-92 | 2026-09-30 | D 批 M4／M5／M1 的清單篩選把驗證函式（`AppInput.RequirePlatform`、`AdminInput.OneOf`）直接寫在 EF `Where(...)` 的 lambda 裡，驗證丟出的 400 例外被 EF 包成 `InvalidOperationException`，端點回 500 | ✅ 改成先驗證再組查詢；`AdminAppTests` 壞篩選參數 400 案例（M4／M5 診斷） |
| E-93 | 2026-09-30 | 推播發送在同一個 scoped `DbContext` 內用 `FirstAsync` 讀回批次，但核可流程已經追蹤了同一個實體，EF 回傳舊的追蹤實體（計數仍是 0），狀態算成 `sent` 而不是 `partial` | ✅ 改用不追蹤查詢＋set-based 更新；`AdminAppTests` 部分送出案例 |
| E-94 | 2026-09-30 | 種子把 28 列日聚合塞進單一 `VALUES` 產生一行超過 8000 字元的 SQL，`sqlcmd` 從標準輸入讀取時把行切斷（`SELEC` / `T`），整個區塊語法錯誤；先前我逐批用 `-i` 檔案驗證沒有重現，`apply-seed.sh` 實際走的是標準輸入 | ✅ 每列一行；驗證改用與 `apply-seed.sh` 完全相同的指令與輸入方式 |
| E-95 | 2026-09-30 | 用 Python 改既有檔案時一律以 `utf-8-sig` 讀寫，替原本沒有 BOM 的 `Program.cs`／`CsvUtils.cs`／`PublicRateLimitPolicies.cs` 加上 BOM，讓 `git diff` 出現整檔第一行變更 | ✅ 改完看 `git diff --stat` 才發現並還原；⚠️ 無 |
| E-96 | 2026-10-01 | `definePerson()` 沒給 `@id`，`nuxt-schema-org` 把同頁多位教練／球員全併進站台身分節點 `#identity`：`our-people.vue`（S1-12f）8 位教練只輸出 1 筆，一線隊球員一開始犯同一個錯；驗收只看「有 Person」沒數節點數 | ⚠️ 無（已修兩頁；`check-schema-batch2` 未涵蓋 Person） |
| E-97 | 2026-10-01 | E 批會籍開通（`MembershipActivationService`）第一版只靠條件式 `UPDATE … WHERE status='paid'` 搶佔做冪等，單一呼叫與循序重複都對；**並行測試（12 個同訂單呼叫）才暴露死結**：輸家的 UPDATE 先鎖 PK 索引項目再等聚集索引列鎖，贏家寫付款紀錄時外鍵檢查要 S 鎖同一個 PK 項目，互等，SQL Server 選一方當死結犧牲者，被 catch-all 誤標成 `activation_failed` 並丟 409／500 | ✅ 開通交易開頭加 `sp_getapplock`（每張訂單一把、交易結束自動釋放）；`MembershipOrderTests.內部開通端點_冪等_重複與並行呼叫只開通一次` 鎖定（12 個並行呼叫） |
| E-98 | 2026-10-01 | 新增 `MapDelete("/me", (MemberDeleteAccountRequest request, …) => …)` 時沒標 `[FromBody]`：ASP.NET Core 不允許對 DELETE **推斷**請求本文，整個 API **啟動失敗**（所有 `WebApplicationFactory` 測試 17 項同時全紅，訊息「Body was inferred but the method does not allow inferred body parameters」），不是只有那一支端點壞掉 | ✅ 加 `[FromBody]`；測試 fixture 啟動就會抓到（本批靠跑測試立即發現）；⚠️ 無靜態檢查——**新增帶 JSON 本文的 `MapDelete`／`MapGet` 一律明標 `[FromBody]`** |
| E-99 | 2026-10-01 | 前台會員頁兩處「看起來接好、實際沒作用」：① 登入成功後靠子元件 `emit('done')` 做 `?next=` 轉址，但登入一成功狀態翻轉、子元件立刻被卸載，事件被丟掉，不轉址；② 元件放在 `components/culture/` 底下卻用 `<FanEventRegistration>` 標籤，Nuxt 自動匯入名稱帶目錄前綴（`CultureFanEventRegistration`），解析不到只輸出空的自訂元素、無任何錯誤（與 `ContentMembershipBenefits` 同類） | ⚠️ 無（靠 CDP 實機走完流程才發現；build／lint 全綠） |
| E-100 | 2026-10-01 | F 批 `dotnet ef migrations add` 之後直接 `dotnet ef database update --no-build` 失敗（「模型有待處理變更」）——`--no-build` 用的是 `migrations add` **之前**建好的組件，裡面還沒有新 migration 類別，EF 以為模型與 migration 對不上 | ⚠️ 無（`migrations add` 後一律先 `dotnet build` 再 `update`） |
| E-101 | 2026-10-01 | F 批商店測試的「確保電子發票字軌」輔助函式硬寫 `environment = 'sandbox'`，但商店的金流／發票環境是 S6 設定 `shop.payment_environment`（可切 production）；程式端依設定取通道，測試補了錯環境的字軌，付款後發票停在 `pending`，**第一次跑完整流程測試就紅燈** | ✅ 輔助函式改讀 `shop.payment_environment`（`ShopPublicTests.ChannelEnvironmentAsync`） |
| E-102 | 2026-10-01 | 慈善 N3 捐款詳情的個資遮罩只對「看起來像個資的欄位」（姓名、Email、身分證字號、地址）做，漏了**預設值就是捐款人姓名**的收據抬頭（`receipt_title`），預設遮罩的詳情回應仍帶出完整姓名；整合測試斷言「遮罩回應裡完整姓名一字不出現」時才抓到，沒有進版控 | ✅ `CharityAdminDonationsTests.詳情預設遮罩…`（斷言完整姓名、Email、身分證字號、地址都不在遮罩回應的任何位置）；欄位清單另靠 `CharityArchitectureTests` 反射鎖住後台回應型別不得有密文欄位 |
| E-103 | 2026-10-01 | 派 CH-2／CH-3 時，派工單寫「目前慈善 DbContext 已有的程式」「兩個 DbContext：主站與慈善」，**實際上不存在**：CH-1 只建了資料庫與種子，沒有任何 EF 實體、連線註冊、後台帳號體系或授權器。agent 因此要從零 scaffold `CharityDbContext`、建 migration 基準、做獨立 JWT 方案／登入／授權器／更新權杖表（多出一張規劃書沒寫的 `admin_refresh_tokens`），範圍與工時都比派工單預期大得多 | ⚠️ 無（派工前 grep 一下 `DbContext` 與 `Features/` 目錄就能發現；見條目） |
| E-106 | 2026-10-01 | 文件各寫各的：`docs/17` §2 規定儲存體只放行 VNet（訪客無法直連），§6 與 `apps/api` 卻假設圖片／文件是公開的 blob 直連網址；寫 Bicep 時才對撞。同時 `docs/16`／`17`／`20` 的正式庫名 `sqldb-*` 與本機 `tcrfc_*` 並存 | ⚠️ 無 |
| E-107 | 2026-10-01 | 派工單把使用者的 SSH 來源 IP 原值寫給 agent，agent 在 `infra/README.md` 的 secrets 表把它當範例值寫進去——**同一列還寫著「放 secret 是為了不洩漏 IP」**；公開 repo，commit 前主 session 掃描才抓到 | ⚠️ 無（commit 前 grep 使用者提供的機密值） |
| E-108 | 2026-10-01 | 給使用者跑的一次性 Azure 設定腳本寫成「只跑一次的直線流程」：`set -e` 下 federated credential 重跑必失敗、`|| echo` 吞掉建角色的真正錯誤、固定 sleep 等 RBAC 生效、未驗證輸入、放在 session 暫存區；使用者讀了覺得不對才回頭審 | ✅ `infra/bootstrap.sh` 每步先查後建、`retry` 取代固定 sleep、寫 secrets 前斷言分支限制只有 `master`；`shellcheck -S warning` 通過 |
| E-109 | 2026-10-01 | `docker-compose.yml` 的 `api` 沒設 `DATA_PROTECTION_KEYS_PATH`、也沒掛 volume——程式沒讀到該變數不會報錯，Data Protection 金鑰環只活在容器可寫層，**容器一重建就讓 2FA、慈善身分證字號與載具、推播權杖、商店與慈善金流／發票憑證全部永久無法解密**；寫 VM 機密檔的任務盤點設定鍵時才發現。同時發現 `apps/api/Dockerfile` 註解把非 root 使用者 uid 寫成 64198（實測 1654），照它建目錄會讓 api 寫不進去 | ✅ compose 已修；`apps/api` 啟動檢查已實作（2026-10-01）；`docs/14` 已加不變量 |
| E-110 | 2026-10-01 | `deploy.yml` 從 2026-09-25 起每次 push 都 `startup_failure`（呼叫 reusable workflow 的 job 沒授予被呼叫端要的 `packages: write`），六天、十餘次 push 沒人追；`actionlint` 通過所以本機看不出來。直到正式庫初始化要用 api 映像檔才發現 ghcr 從未有映像檔 | ⚠️ 無（建議：push 後檢查 `gh run list` 的結論，`startup_failure` 視同紅燈） |
| E-111 | 2026-10-01 | 正式環境（Azure SQL）的 `json` 欄位是原生型別、只收物件／陣列，但本機與測試是 `nvarchar(max)`（什麼都收）：`partner_stores.business_hours` 被寫成 JSON 字串純量、新聞內文接受任意純文字、多個「只驗是合法 JSON」的輸入允許純量，正式後台儲存會 500，**整套 1,000+ 項測試全綠、本機實測也全綠**；直到拿 SQL Server 2025 原生 json 容器接原樣 DDL 演練才發現 | ✅ `Common/JsonColumn.cs` 統一守門（結構化輸入的純量回 400；自由文字欄位如營業時間、新聞內文包成 `{"text":"…"}`）；`JsonColumnTests`；`apps/api/scripts/native-json-test.sh`（原生 json 容器，可重跑）；`docs/14` 不變量。⚠️ CI 仍接 2022（建議改 2025，見 `docs/20` §5） |
| E-112 | 2026-10-02 | 第一次要在 VM 起容器時才發現兩處「設定與產出沒對撞」：compose／`.env.example`／`provision-secrets.sh` 的 `IMAGE_TAG` 預設是 `latest`，但 `deploy.yml` 只推 `:master` 與 git SHA，照預設起會拉不到映像檔；主站 `nuxt-tcrfc` 沒給 `NUXT_PUBLIC_BLUE_WHALE_SITE_URL`，女足頁導往藍鯨站的連結落回 `nuxt.config.ts` 寫死的 `bw-stg.tcrfc.tw` | ✅ `scripts/check-compose-env.mjs`＋CI `compose-env` job（2026-10-03 第二、三次再犯後升級並擴大到兩個後台 SPA，見 E-112 升級段） |
| E-113 | 2026-10-02 | S0-9 把 mockup 照片改成模板靜態 `src`（Vue 編譯器轉成 import）後，從未在沒有照片的乾淨 checkout 建置過；GitHub Actions 建 `tcrfc-nuxt-club` 失敗（UNRESOLVED_IMPORT） | ✅ `siteImg()` ＋ `npm run lint:site-images` 禁止繞過；照片改由 Blob 提供 |
| E-114 | 2026-10-02 | 正式 VM 第一次 `docker compose up`：`proxy` 固定 `172.28.238.2`，但它依賴其他服務健康後才啟動，先起來的 `redis`（無固定 IP）被自動分到 `.2`，`proxy` 以「Address already in use」失敗 | ✅ `docker-compose.yml` 的 `internal` 網路加 `ip_range: 172.28.238.128/25`，自動分配不再落在固定 IP 段 |
| E-115 | 2026-10-02 | 第一次 CD：健康檢查要求前台 `/` 回 200，但前台 `/` 一律 302 到 `/zh/`，判定失敗→退回→退回也用同一檢查而失敗，結束碼 2；實際上網站與容器全部正常。假 `curl` 對任何網址都回 200，60 項測試全綠卻沒抓到 | ✅ 健康檢查改 `curl -L --max-redirs 3`；`deploy/test-cd.sh` 的假 `curl` 對 `/` 沒帶 `-L` 回 302（拿掉修正會失敗 27 項） |
| E-116 | 2026-10-02 | `db-migrate.sh` 的 `apply` 一開始寫成 `sqlcmd … -f 65001 -i …`；假 docker 的 sqlcmd 照單全收，88 項測試全綠，直到對本機 SQL Server 容器以真實 `mssql-tools` 演練才發現 Linux 的 sqlcmd 不支援 `-f`（`Unknown Option`），正式環境第一次套用就會失敗 | ✅ 已移除 `-f`；假 docker 改成遇到 `-f*` 就回真實的錯誤並結束碼 1，測試斷言 `-f` 不在呼叫裡；另保留「真實 sqlcmd 演練」為上線前的步驟（`docs/20` §5） |
| E-117 | 2026-10-02 | S0-10 壓測手冊初稿寫「升 S0 後 2 GB 硬上限同時解除」；實際 `maxSizeBytes` 是獨立設定（Bicep 寫死 2 GB），升級層級不會自動改，且不同步改 Bicep 下次 `infra.yml` 會改回去。另：任務敘述說「儲存告警要設 1.5 GB」，其實 `monitoring.bicep` 早已有，差點重複造一份 | ⚠️ 無（交付前對照 `sql.bicep` 與 `az sql db update --help` 抓到） |
| E-118 | 2026-10-02 | `STATUS.md` 的 `S1-18` 列到 2026-10-02 還寫著「後台登入端點尚無 IP 限流，另案處理」，但 `/login`／`/refresh` 的限流早在 S1-18d（2026-09-29）完成並有測試（只記在 `apps/api/README.md`）；照 STATUS 派工會重做一遍已完成的事，真正漏網的三支（變更密碼、2FA 確認／停用）反而沒人提 | ⚠️ 無（靠完工時同步 STATUS） |
| E-119 | 2026-10-02 | N7 站台設定的整合測試改了共用本機庫上的單一份設定（`settings`／`email_templates`／`payment_channels`），還原時只還原 `value`，沒還原 `updated_by`：這些列已指向測試帳號，**測試帳號被外鍵擋住無法清理**，後續每個測試的 fixture 啟動清理都失敗（一次 6 項失敗，並留下殘骸要手動修） | ✅ 還原快照一併記錄 `updated_at`／`updated_by`／`created_by`；快照只要動共用設定就整組拍下 |
| E-120 | 2026-10-02 | 為「公開讀取」限流加了一個把額度用盡的測試，放進既有的限流 fixture：測試主機的 `RemoteIpAddress` 是空的，所有請求落在同一個計數桶，用盡額度害同一個主機上另一項既有限流測試失敗（順序相依的偶發紅燈） | ✅ 獨立的 `CharityRecognitionRateLimitApiFixture`（自己的主機、自己的計數） |
| E-121 | 2026-10-02 | AP-3 的 `AppDeviceSessionTests` 在沒有資料庫的工作樹寫完、只確認「編譯通過」就交付；合併後實跑，「登出全部與變更密碼」一支穩定 401——測試先重放舊權杖觸發了重用偵測（程式正確，測試順序錯） | 「編譯通過」被當成「測試寫對了」 | 依賴資料庫的新測試沒實跑過，一律在回報標「未執行」，合併後由主 session 在有庫的環境補跑才算完成；工作樹缺 `appsettings.Development.json` 是根源 | ⚠️ 部分（2026-10-02 升級）：`OfflineQueryTranslation` 離線翻譯冒煙工具擋住「EF 查詢翻譯不了」這一類；資料行為仍靠合併後完整測試。**同日第二次印證**：H 批 A 儀表板／I 網站設定的資料庫測試合併後實跑，5 項失敗全是測試端錯（T-SQL 變數大小寫不分，`@u` 與參數 `@U` 衝突；計數漏算兩題缺英文 FAQ；誤以為內容編輯沒有報名檢視權限），程式無誤，已修並全綠（1272 項）。教訓：寫期望值前先查種子實際授權與測試資料會同時觸發的所有型別，臨時 SQL 的變數與參數名稱不得僅大小寫不同 |
| E-122 | 2026-10-02 | 慈善後台報表的趨勢條寫了 `var(--charity-info)`，但實際定義的變數只有 `--charity-info-text`／`-bg`（`--charity-info` 只出現在 `docs/22` §5 的速查表）；樣式表引用不存在的變數不會報錯，背景變透明，趨勢欄整欄看起來是空的，`vue-tsc`、eslint、`vite build`、禁用詞、對比度檢查全綠，是看截圖才發現 | ✅ `apps/admin-charity/scripts/check-css-vars.mjs`（掛進 `npm run lint`） |
| E-123 | 2026-10-02 | 用 Python 讀寫 `Program.cs` 時沒保留換行，把混有 CRLF 的檔案整段正規化成 LF，一個 13 行的改動變成 47 行 diff，連沒碰的行都變了 | 無（`git diff --stat` 肉眼檢查抓到） |
| E-124 | 2026-10-02 | 合併含新 migration（`AlignSchemaG1`、慈善 `AddCreditHiddenAndSettlementLineKey`／`AddCh4Ch5Permissions`）的程式後直接 push master，`deploy.yml` 換上新版 api，正式庫尚未跑 `db-migrate.yml`，測試站新聞 API 回 500 | push 前沒檢查「這批有沒有新 migration」 | push 前 `git diff Remote_GitHub/master --stat -- apps/api/Data/Migrations` 有輸出就先跑 `db-migrate.yml` | 無 |
| E-125 | 2026-10-02 | 儀表板放行規則第一版只列「各區塊用到的權限碼」，沒有對十個角色逐一走過登入首頁：**翻譯人員**（只持字串翻譯表權限）會在登入後的第一頁拿到 403；內容區塊的新聞數字對沒有新聞權限者回 0（看不到被當成沒有）。寫「每個角色的儀表板」整合測試時才發現，交付前已修 | ⚠️ 部分：`DashboardApiTests` 逐角色斷言（需資料庫，未實跑）＋離線測試鎖放行碼集合含翻譯人員權限 |
| E-130 | 2026-10-02 | 夏令營／冬令營頁拿英文代碼 `open`／`waitlist` 比對梯次狀態，但資料庫與公開 API 的 `sessions.status` 是中文字面值（開放／額滿／候補／已結束）——兩頁的「開放中梯次」永遠找不到，早鳥價與剩餘名額永遠顯示「待公告」，自 S1-15 起一直如此 | 狀態值憑印象寫成英文代碼，沒對 `db/club-schema.sql` 的 `CK_sessions_status`；測試資料為空所以沒人看到 | 判斷梯次狀態一律走 `app/utils/program-session.ts`；前台比對資料庫列舉值前先查 DDL 的 CHECK | ✅ `scripts/check-news-body.mjs`（掛進 `npm run lint`）釘住中文字面值與「英文 `open` 不是合法狀態」 |
| E-131 | 2026-10-02 | 前台 BFF 代理 `server/api/backend/[...path].ts` 對 POST 的上游 4xx 直接讓 `$fetch` 的錯誤冒出去：Nitro 正式環境把它當未處理例外，`message` 一律變 `Server Error`、`data` 被拿掉。後端刻意寫給使用者的中文驗證訊息（「缺少必填欄位：xxx」「這個梯次的報名已經截止。」）在瀏覽器永遠看不到，只剩通用文案；S1-17 起七張表單都受影響 | 只用 curl 驗「成功」與「405」，沒驗過「上游 400 的訊息是否到得了 `useFormSubmit.extractErrorMessage`」；開發模式與正式建置的錯誤輸出不同 | 代理對上游 4xx 以 `createError({ statusCode, message })` 重丟，5xx 一律 502；新增代理路徑時**同時驗一次上游 400 的訊息**（本機假後端回 `{detail}` 看瀏覽器收到什麼） | 無（本次 curl 實測 400 訊息已帶出；尚無自動測試） |
| E-132 | 2026-10-02 | 官方商品頁改接商店 API 時用了 `useShopInfo()`，整份商店資訊（含 `collectingSubjectName`「款項由台中磐石足球俱樂部代收」）被序列化進頁面 payload，藍鯨的 `/culture/merchandise/` HTML 因此出現「磐石」，被 `check-club-brand-leak.mjs` 抓到（頁面可見文字沒有） | 以為「沒有顯示的欄位就不算外洩」；Nuxt 會把 `useFetch`／`useAsyncData` 的整份回應放進 payload | 在藍鯨會渲染的頁面只取需要的欄位（`useAsyncData` 內只回傳 `collections`），不要直接用回傳整包商店資訊的 composable | ✅ `scripts/check-club-brand-leak.mjs`（需先把藍鯨站跑起來，未掛 lint，E-34） |
| E-140 | 2026-10-02 | 後台 `http.ts` 的 `classifyByStatus` 對 5xx（含 503）建立 `AdminApiError` 時漏帶 `body`，特約店家編輯頁 `PartnerStoreEditView` 用 `error.body.code === 'geocoder_unavailable'` 區分「定位服務暫時故障」與「尚未啟用」的分支**永遠不會成立**：暫時故障被誤判成尚未啟用，按鈕被停用、提示錯誤（G 批上線至今） | G 批只靠讀程式與 lint 確認該分支存在，沒有用會回 503＋`code` 的假後端實際走過兩種 503；H 批場地管理要比照做時用假後端實走才抓到 | 凡依 `error.body.xxx` 分流的畫面，驗收時假後端必須各回一次對應的 `code`；`classifyByStatus` 所有分支都要把 `body` 帶進 `AdminApiError` | 無（已修；`verify-admin` 的定位三態是 scratchpad 腳本，未納版控） |
| E-142 | 2026-10-02 | 頁尾電子報區新增「同意」勾選框，被凍結樣式表 `tcrfc.css` 的 `.newsletter input{ width:100%; padding:0 1rem }` 撐成整欄寬方塊、說明文字被擠成一字一行；lint／typecheck／build 全綠，看截圖才發現 | 往既有區塊加控制項前沒有 grep 凍結樣式表裡該區塊的元素選擇器 | 加控制項前先 grep 該區塊的樣式規則，在元件內用更高特異度覆寫，並看截圖 | 無 |
| E-143 | 2026-10-03 | 給使用者的 VM 操作步驟寫 `cd ~/tcrfc-src && git pull`，該目錄在首次 CD 後已退役，使用者在 VM 上得到 `not a git repository`；`infra/README.md` 兩處與 `docs/20` §5 一處也還指向它 | 無 |
| E-148 | 2026-10-03 | K2 手動開通的會員選擇器搜不到 K1 現場建立（尚無任何會籍）的會員：名單端點預設只列「本俱樂部已有會籍」者，而 K1 建立→K2 開通正是它的第一個使用情境（後台實機驗收 B-1） | 名單端點的範圍規則是為 K1 名單設計，選擇器重用它時沒有走一遍「建立會員→開通」的串接情境；測試只驗了範圍不外洩，沒驗新建帳號找得到 | 凡把既有列表端點當「選擇器」重用的畫面，驗收要走完整串接（上一步剛建立的資料，下一步選得到）；範圍規則改動要同時回答「新建、尚未歸屬的資料可見嗎」 | ✅ `includeNoMembership=true`（`AdminMembersTests.名單_includeNoMembership…`） |
| E-149 | 2026-10-03 | K5 產生公布稿草稿後刪除該新聞草稿回 500：`member_draws.announcement_article_id` 是不 cascade 的外鍵，`AdminArticlesRepository.DeleteAsync` 沒檢查被抽獎活動引用，直接撞外鍵（後台實機驗收 B-9） | 新增 K5 的外鍵時只想到「活動→文章」的寫入路徑，沒回頭盤點文章刪除端點會被誰參照 | 新增指向既有表、且不 cascade 的外鍵時，同一次 grep 該表所有刪除路徑，補「被引用則 409（或先解除關聯）」並寫測試 | ✅ 測試 `刪除公布稿文章…`（無自動掃描外鍵的機制） |
| E-150 | 2026-10-03 | P4 後台代填試訓報名只驗姓名與狀態，沒有比照前台同一張 `registrations` 表的規則（聯絡方式至少一項、Email／電話格式、未成年須填家長）；P3 後台 Email 也不檢查，壞值直接寫入（後台實機驗收 B-3） | 同一張表的寫入驗證散在前台、P3、P4 三處各寫各的，後台端點「人為判斷」的註解被擴大解讀成「不需要驗證」 | 同一張表有多個寫入入口時，驗證抽成共用函式（`AdminInput.OptionalPhone`／`OptionalEmail`），新入口開工前先對照既有入口的驗證清單 | ✅ 測試 `試訓報名_後台代填驗證…`；無跨入口一致性掃描 |
| E-151 | 2026-10-03 | 多個 CSV 匯出（P3、P4、G3、表單詢問、Lead）各自 `CreatedAt.ToString("yyyy-MM-dd HH:mm")`，輸出無標示的 UTC，使用者以為台灣時間而差 8 小時（後台實機驗收 B-5） | 資料庫時間戳存 UTC 是共識，但「輸出給人看的時間文字」沒有共用函式，每個匯出自己格式化 | 輸出時間文字一律走 `TaiwanClock.ToText`，表頭標「（台灣時間）」 | ✅ `TimestampFormatTests` 掃原始碼，禁止無 `AddHours(8)`／`UTC` 的 `yyyy-MM-dd HH:mm` 格式化 |
| E-153 | 2026-10-05 | 後台 `classifyByStatus` 對 400／401／403／404／409 以外一律回固定的「伺服器發生未預期的錯誤」，後端 503 刻意回的白話 `detail`（「檔案儲存尚未設定」）被吞掉，使用者上傳失敗只看到通用訊息（第二輪重驗） | 寫錯誤分流時只列了「會有 detail 的狀態」，把 5xx 整類當作不可信；E-140 補了 `body` 卻沒回頭檢視 `message` 該不該顯示 `detail` | 錯誤訊息的取捨以「後端是否刻意提供白話 detail」判斷，503 顯示 detail、500 維持通用 | 無（以 Playwright 實際上傳驗證） |
| E-156 | 2026-10-05 | 先前判讀「上傳端點未驗只是缺 `AZURE_BLOB_CONNECTION_STRING`」不完整：本機即使補了 Azurite，API 自建的容器一律私有，上傳成功但瀏覽器讀圖片網址 403、全站圖片破圖，上傳端到端驗收仍然驗不了 | ✅ `seed-dev-blobs.py` 預建公開容器、`dev-azurite.sh`、`http-azurite` 設定檔，見下方條目 |
| E-157 | 2026-10-05 | 修了種子（B-14 廣告曝光數字）卻沒重產 `db/prod/club-content-seed.sql`，`generate-prod-content-sql.py --check` 回「不一致」（本輪補種子區段 60 時才發現） | ✅ `ci.yml` 的 `prod-seed` job（見 `docs/20` §3） |
| E-158 | 2026-10-05 | 藍鯨的 `llms.txt`／`llms-en.txt` 預設代表頁清單輸出「關於台中磐石／足球學院／台中磐石文化」（`SITE_UNITS[].labelZh` 是磐石版名稱），`llms-en.txt` 還寫死 `Taichung Blue Whale`（B-5：英文全名客戶未指定，開發端不得自挑）；`check-club-brand-leak` 只掃 HTML 頁面，兩份純文字檔一直沒被掃到 | ✅ `getUnitLabelZh(unit, club)`；`check-club-brand-leak.mjs` 路由清單加入 `/llms.txt`、`/llms-en.txt` |
| E-159 | 2026-10-05 | `useOrganizationSchema()` 用 `watchEffect` ＋ `useSchemaOrg`，**合格時 SSR 也不輸出 Organization**；因真資料下 `schemaEligible` 恆為 false，「合格時輸出」這條路徑從來沒被走過，GEO-05「資料不足不輸出」的驗收只證明了一半 | ⚠️ 無自動檢查（需要能回 `schemaEligible=true` 的假 API），改以 `useHead(() => …)` 函式形式修正，並在 `apps/web/README.md` 記錄 fixture 驗證方式 |
| E-160 | 2026-10-05 | 擴充開發種子（區段 60 加兩位球迷會員）前只 grep 了「會員編號」相關測試，漏掉「合格名單＝全部有效球迷會員」這種**由資料推導的數量**；全套測試 3 項 `AdminDrawsTests` 失敗（預期 2 實得 4） | ⚠️ 無（靠擴種子前掃推導式斷言；本輪已改測試並加 `DevAcceptanceSeedTests` 守門） |
| E-170 | 2026-10-05 | migration 的清理 SQL 寫了 `([lat] IS NULL) <> ([lng] IS NULL)`——T-SQL 不能拿布林表達式做比較；因為包在 `EXEC(N'…')` 裡，錯誤只印一行「Incorrect syntax near '<'」，**後面的 `ALTER TABLE … ADD CONSTRAINT` 照樣成功**，清理那步靜默沒做（手動套用到開發庫時才從輸出發現） | ✅ `MigrationsOnBlankDatabaseTests`（空白庫＋最新 DDL 實際執行契約內的全部 migration：套用→回滾→再套用→冪等重跑；已驗證重新放回壞語句會紅燈） |
| E-171 | 2026-10-05 | 公開課程詳情 `GET /{club}/programs/{slug}` 對**有掛夥伴**或**梯次有日期**的課程回 500：`PartnerRow` 的 SQL 欄位順序與 positional record 建構子不一致、`SessionRow` 用 `DateOnly?`（`E-20` 同一類錯誤第二次）；種子的藍鯨社區足球學校一直壞著，沒有測試逐一讀過每個課程詳情，第五批寫「未翻譯標示」測試時才發現 | ✅ `AppContractBatch5Tests.公開課程梯次與試訓場次…每個課程詳情都能讀`（逐一讀兩俱樂部全部課程詳情） |
| E-172 | 2026-10-05 | 會籍訂單建立（`POST /member/membership-orders`）同一冪等鍵並行時，慢的請求在「查冪等鍵」之後、「查未完成訂單」之前被快的請求插入訂單，把**自己這把鍵的那張訂單**當成別張未完成訂單而回 409 `open_order_exists`（應回 200＋原訂單）；全套測試偶發一次、被當成不穩定測試放著（第五批），16 並行迴圈 15 輪即在第 4 輪重現 | ✅ `MembershipOrderTests.冪等鍵_高強度並行…`（16 並行×15 輪，修正前紅燈已驗證）；同類壓測已套到商店結帳、慈善捐款、試訓報名、活動報名、加入俱樂部 |
| E-161 | 2026-10-05 | 並行派出的兩個 agent（backend-engineer、frontend-architect）各自在 `docs/18` 開了 **E-158**，撞號；主 session 合併時改後端那筆為 E-160 | ⚠️ 無（靠派工時預先分配編號） |
| E-162 | 2026-10-05 | iOS 專案的 xcconfig 設了 `PRODUCT_NAME`，專案層級設定套到測試 target，`xcodebuild test` 報重複輸出 | 無（建置即報錯，當日修正） |
| E-190 | 2026-10-05 | Android 冷啟動被深連結叫起時，深連結處理比 NavHost 設好導覽圖還早執行，`navigate`／`graph` 丟 `IllegalStateException`（setGraph() 之前）而閃退；單元測試全綠、手動只測過「App 已在執行時再點連結」，冒煙測試第一次跑就抓到 | ✅ `SmokeTest` 的深連結 Intent 案例（match、player、news、store、program、schedule） |
| E-163 | 2026-10-05 | Android 深連結解析器的 Regex 寫了未跳脫的 `}`（`\\{(\\w+)}`）：JVM 單元測試全過，但 Android 的 ICU 正規式報 `PatternSyntaxException`，實機點「完成引導」進主畫面即閃退 | ✅ **instrumented 冒煙測試**（`tcrfc-app-android` 的 `SmokeTest`，`./gradlew connectedDevDebugAndroidTest`，2026-10-05 起；見 E-190） |
| E-164 | 2026-10-05 | 主 session 把 Android 的「開賽提醒預設關閉」當成兩端共同決定轉給 iOS，沒有先核對 App 規劃書 §6.2（賽事提醒預設「開」）；iOS 依規劃書回報才更正 | ⚠️ 無（靠轉述前查規格） |
| E-185 | 2026-10-05 | iOS 自行把「台中磐石足球俱樂部」剝成「磐石」顯示，違反主站規劃書 §0 第 2 點（中文簡稱寫「台中磐石」、不得單獨用「磐石」）；由主 session 審查指出 | 無（改顯示全名並加測試；後端補 `ClubDto.shortName` 後接上） |
| E-186 | 2026-10-05 | iOS 電子會員卡的 QR 在畫面上是一整塊空白：`UIImage(ciImage:)` 在 SwiftUI 不會繪出；單元測試只斷言「回傳非 nil」所以沒擋住，是模擬器截圖才看到 | 有（單元測試 `testQRImageIsBitmapBackedNotBlank`；**升級為機制（E-189 同批）**：XCUITest 冒煙測試 `testMemberCardShowsAScannableLookingQR` 在真的 App 畫面上截圖斷言 QR 有深淺像素，`./scripts/smoke.sh`，已用「改回 `UIImage(ciImage:)`」實測會失敗） |
| E-187 | 2026-10-05 | iOS 網路層把「5xx／斷網重試 3 次」套在**所有**請求，包含更新權杖續期與重產 QR、加入俱樂部等有副作用的 POST——續期被重試會用已輪替的舊權杖，被後端判為重用而撤銷整條登入鏈；對照 Android 的決定才發現 | 有（`testStateChangingPostsAreNeverRetried`；`APIRequest.retryable` 預設只 GET／PUT） |
| E-188 | 2026-10-05 | iOS 為「改測試」寫了 `open(p,'w').write(open(p).read())`：先以寫入模式開檔把檔案截成 0 位元組再讀，`CoreLogicTests.swift` 17 項測試無聲消失，綠燈的測試總數少了 9 項才發現 | 無（靠對照測試總數；改測試前先 `git diff --stat` 看有沒有整檔被清空） |
| E-189 | 2026-10-05 | iOS 預覽用假 API（`FixtureContentAPI`）的呼叫紀錄陣列沒有鎖，四個廣告版位並行載入時同時 `append`，App 閃退（malloc: pointer being freed was not allocated）；單元測試序列呼叫抓不到，是模擬器開首頁截圖看到桌面才發現 | 有（鎖＋單元測試 `FixtureConcurrencyTests`；**升級為機制**：XCUITest 冒煙測試 `testOnboardingThreeStepsThenHomeWithConcurrentAdSlots` 在首頁四個廣告版位並行載入的情境下跑完整流程並斷言沒有閃退，`./scripts/smoke.sh`） |
| E-205 | 2026-10-05 | 首頁輪播的指示器（粉紅 dots）永遠停在第一顆：樣板把 `is-active` 寫死成 `i === 0`，換張只更新了 slide 的 class 與朗讀文字，`current` 又是非響應式變數；從移植靜態 mockup 起就沒同步過，使用者回報才發現 | 無（便宜的靜態檢查會誤報；改以瀏覽器實測：自動、箭頭、點指示器、滑動四條路徑都要比對 slide 與 dot 同步） |
| E-210 | 2026-10-05 | 英文用詞對照表（docs/06 §1.1）初版寫「台中藍鯨可用 Taichung Blue Whale 描述性寫法」，與 `docs/14` 既有的 B-5 規則（不得寫死藍鯨英文名）牴觸；三個翻譯 agent 照表寫進 `club-copy-en-*.ts` | ✅ `check-bw-en-name.mjs`（lint 當場紅燈） |
| E-211 | 2026-10-05 | 英文版分派時只列頁面目錄，沒列「跨頁共用元件」歸屬：`TrialSchedule.vue`、`FanEventRegistration.vue` 被兩個 agent 同時指派，一個的批次替換腳本因舊字串消失而整批中止 | ⚠️ 無（靠動手前 `git diff` 確認） |
| E-212 | 2026-10-05 | 種子補英文列後，4 組 `apps/api` 測試的前提「種子缺英文」失效（測試把種子的缺口當成回退行為的測資），已改為自建資料 | ⚠️ 無（類似 E-160，見條目） |
| E-213 | 2026-10-05 | 後端 `?lang=en` 缺值時逐欄位回退繁中，前台直接取用就讓英文版（含 JSON-LD、`llms-en.txt`）悄悄混入中文，沒有任何建置或執行錯誤 | ✅ `useSiteFacts.pickEn`／`englishOnly`／`enOnly` 過濾；✅ `scripts/check-en-pages.mjs` 實機掃描 |
| E-214 | 2026-10-05 | 多個 agent 並行做批次字串替換：共用 scratchpad 的腳本被別人覆寫、替換腳本中途失敗後重跑重複套用、英文字串的撇號未轉義產生語法錯誤 | ⚠️ 無（eslint／build 會抓到語法，抓不到重複套用） |

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


### E-85 EF Core：子實體有 `Guid` 主鍵時只加進父實體導覽集合會被當成既有列（2026-09-30，E1a 後台 API）

- **錯在哪**：`AdminProposalsRepository.AddFileAsync` 用 `proposal.ProposalFiles.Add(new ProposalFile { Id = Guid.NewGuid(), … })` 新增檔案。
  EF 對「主鍵屬性已有非預設值、且該主鍵設定為 `ValueGeneratedOnAdd`」的實體，經導覽集合被追蹤時判為 **Modified**，
  產生 `UPDATE proposal_files SET … row_seq = …`，SQL Server 回 `Cannot update identity column 'row_seq'`，端點 500。
- **為什麼會錯（根因）**：把「集合 `.Add`」當成「標記為新增」。既有寫入端（`AdminStaffRepository`、`AdminFaqsRepository`…）
  對帶 `Guid` 主鍵的子實體一律同時呼叫 `dbContext.XxxSet.Add(...)`，我照抄了複合主鍵（無生成值）那一類的寫法。
- **下次怎麼避免**：新增任何有 `Guid Id`（資料庫 `DEFAULT NEWID()`）的實體，一律 `dbContext.XxxSet.Add(entity)` 明確標記；
  複合主鍵且沒有生成值的連結表（`sponsor_articles` 等）才可以只加進導覽集合。
- **防呆**：✅ `AdminBusinessUploadTests` 的提案檔案上傳案例（測試打真實資料庫，這個錯誤只會在真的存檔時爆）。⚠️ 沒有靜態掃描。

### E-86 scaffold 輸出與手改過的 `ClubDbContext` 用 diff 合併，會夾帶重複與無關的設定（2026-09-30，E1a）

- **錯在哪**：先把 DDL 套到本機庫、重新 scaffold 到暫存目錄，再用 `difflib` 把「新增行」插進 `apps/api/Data/ClubDbContext.cs`。
  結果 36 個插入區塊裡有 17 個是重複或無關的（`Article.CanonicalPath`、`ArticlesI18n.SeoKeywords`、`Banner.Status`／`VideoKey`、
  `FaqEmbedSlot.Name`、`FormFieldsI18n` 整個實體區塊…），如果直接編譯，`ClubDbContext` 會對同一個屬性設定兩次。
- **為什麼會錯（根因）**：假設「scaffold 輸出 ≈ 現有檔案 ＋ 我這次的異動」。實際上先前的 agent 手改時把部分屬性設定放在檔案的其他位置
  （S1-12 的單頁 SEO、S1-7a 的輪播欄位等各自成段），行序與 scaffold 不同，diff 判成「新增」；`DbSet`／實體檔則沒有這個問題。
- **下次怎麼避免**：**只取跟本次新表新欄位有關的區塊**——用關鍵字白名單過濾插入區塊（表名、欄位名、外鍵與索引名），
  過濾後逐段看 `git diff` 再編譯；再用 `dotnet ef migrations add Probe` 確認 Up／Down 為空（模型與 snapshot 同步），最後 `migrations remove`。
  實體檔（`Data/EfEntities/*.cs`）同樣只插入新屬性，行尾混用 CRLF／LF 的處理見 agent 記憶「EF scaffold file editing」。
- **防呆**：⚠️ 無腳本（每次異動欄位名不同，白名單要人挑）。`Probe` migration 檢查能抓到「模型與 snapshot 不一致」，抓不到「重複設定」（EF 對同一屬性重複呼叫 Fluent API 不報錯，後者覆蓋前者），
  所以**合併後必看 diff** 是唯一防線。

### E-87 補種子測試帳號前沒有先查既有帳號（2026-09-30，E1a）

- **錯在哪**：為了測 E1／E2／B5／B6 的角色矩陣，在 `generate-club-seed-sql.py` 新增 `business.sponsor@tcrfc.test`（商務／贊助）與 `pr.media@tcrfc.test`（公關／媒體）；
  兩個角色的測試帳號在 S1-10／S1-11 早就補過（`business.sponsorship@`、`pr.media@`）。
- **為什麼會錯（根因）**：只看了 `ADMIN_USERS` 開頭幾筆（內容編輯、檢視者、合作球隊管理…），沒有 grep 整份清單就判斷「沒有這兩個角色」。
- **下次怎麼避免**：新增種子帳號前先 `grep -n "role_code" ` 或直接查 `admin_users` 對 `admin_user_roles` 的角色分佈。
- **防呆**：無。已從腳本與資料庫移除多餘的 `business.sponsor@`。

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
  ~~⛔ 但那個掃描目前是一次性的臨時腳本，沒有留在專案裡~~——**2026-10-02 已留下**：
  [`scripts/overflow-probe.mjs`](../scripts/overflow-probe.mjs)（見下方第 3 次再犯）。仍未接進 CI（等 `docs/20-cicd.md` 的 E2E 關卡）。

- **同類再犯 第 3 次（2026-10-02，慈善 CH-3/4/5 畫面，`frontend-architect`）**：這次不是表格，是**浮層**——
  新做的對話框用 `width="640px"` 內嵌寬度，在 390px 螢幕被右側切掉；`el-dialog` 外層 `.el-overlay-dialog` 是 `overflow: auto`
  的 `position: fixed` 容器，溢出被包在裡面，`document.documentElement.scrollWidth` 仍等於視窗寬（390），
  第一輪「三斷點整頁無橫向溢位」全綠。另外同一輪還有：頁首操作按鈕被擠出畫面、`el-checkbox` 長說明不換行把對話框撐寬 9px、
  `el-row` 的 gutter 負邊距、手機寬度仍用 `el-table` 顯示 5 欄以上的明細——**沒有一個**會被 document 層級的數字抓到。
  依本檔規則 1 **不另開新編號**，把這筆升級：
  · **上次寫的「那支掃描腳本沒有留在專案裡」就是這次再犯的直接原因之一**——我第一輪仍只量 document 層級，
    第二輪才臨時寫了只掃 `.el-table` 的腳本（又漏掉浮層）。
  · 本次已把探針留在專案裡：[`scripts/overflow-probe.mjs`](../scripts/overflow-probe.mjs)。連到開著
    `--remote-debugging-port` 的 Chrome，對**目前這一頁**掃描所有元素：①`overflow-x` 非 visible 且 `scrollWidth > clientWidth` 的容器
    （會一併列出撐出去的子孫元素）②右緣超出視窗且沒被祖先裁切的元素。頁籤列的內建捲動（左右箭頭）是明列的唯一豁免。
    用它對 31 個畫面狀態（頁面、抽屜、對話框、各頁籤）× 390／820／1440 三種寬度共 93 個狀態跑過，第一次掃描就有十數個狀態不合格，修完後全部乾淨。
  · **仍有的限制（防呆是部分的）**：探針量的是「目前畫面」，沒打開的對話框、沒切過去的頁籤量不到，需要驗收的人自己把畫面操作到那個狀態；
    它也還沒有接進 CI（需要登入與操作流程，屬各專案的 E2E，等 `docs/20` 的 E2E 關卡）。
  · 對話框寬度的根本修法：`charity-admin-theme.css` 對 `.el-dialog` 統一加 `max-width: calc(100vw - 24px)`；
    `apps/admin`（主站後台）的對話框是否有同樣問題**沒有驗**，下一次動它時請用探針掃一次。

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

### E-47 補償交易沿用已取消的 `cancellationToken`，最常見的失敗形狀反而清不掉（2026-09-24，`S0-8c` 審查時發現；源頭 `S0-8`）

- **錯在哪**：物件儲存有三處補償刪除：`BlobImageStorageService.UploadAsync` 的 `catch`，以及 `AdminArticlesEndpoints` 新增、更新兩處的 `catch`。三處都寫成 `DeleteAsync(key, cancellationToken)`。
  上傳寫到一半失敗，最常見的原因就是**使用者關掉頁面或請求逾時，也就是 token 被取消**。這時補償刪除拿到的是同一個已取消的 token，第一個刪除就拋出 `OperationCanceledException`，
  被 `DeleteAsync` 的 fail-open 吞成一行警告，前面寫成功的物件照樣留下來。
  `S0-8c` 交付的 6 項測試全部傳 `CancellationToken.None`，所以抓不到這個問題。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**寫補償邏輯時，把「失敗的原因」和「補償用的資源」當成互不相關。**
  實際上 token 被取消本身就是失敗的原因之一；補償沿用觸發失敗的那個 token，等於在同一個條件下注定再失敗一次。
  測試又只注入 `InvalidOperationException`，**注入的失敗形狀是自己想到的那一種，不是實際最常發生的那一種**（與 `E-39` 升級段同源）。
- **下次怎麼避免**：🔴 **補償、清理、回滾這類「失敗後才跑的動作」，一律用 `CancellationToken.None`**，不沿用觸發失敗的請求 token。
  寫失敗注入測試時，**「請求被取消」一定要列為其中一種形狀**。
- **防呆**：✅ 新增測試 `請求在上傳途中被取消_補償刪除仍然執行_不留下殘留物件`，用真實的取消形狀注入失敗。**修正前實測紅燈，修正後綠燈**。
  三處補償都已改用 `CancellationToken.None`。⚠️ `AdminArticlesRepository` 裡「更新成功後刪除舊封面」的兩處刪除不是補償，仍沿用請求 token；請求被取消時，舊封面可能留下，影響只在儲存空間。

### E-48 「發布時間到了沒」由應用程式時鐘寫入、資料庫時鐘判斷，剛發布的內容偶爾 404（2026-09-24，`S1-4` 發現；源頭 `S0-7e`／`S0-8` 的新聞立即發布）

- **錯在哪**：立即發布把 `published_at` 設成 API 行程的 `DateTime.UtcNow`，公開查詢卻用資料庫的 `published_at <= SYSUTCDATETIME()` 判斷。兩個時鐘只要有一點誤差（Docker Desktop for Mac 很常見），剛發布的內容就會被資料庫當成「還沒到發布時間」。
  頁面測試連跑 15 次失敗 5 次；`S1-3` 收尾時全套測試出現過一次查不出原因的失敗，應該也是這個問題。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**寫入端和讀取端各自取「現在」，沒有確認兩者用的是同一個時鐘。**
  `S0-7g` 的排程發布刻意在同一句 SQL 裡求值，所以沒事；立即發布卻是在應用層取時間，兩者一直沒被放在一起檢查過。
  另一個原因是測試沒有斷言 publish 的回應狀態，失敗時只看到 404，看不出是哪一步出了問題。
- **下次怎麼避免**：🔴 **寫入的時間戳，如果之後要跟 `SYSUTCDATETIME()` 比較，就一律取資料庫時鐘**（`Common/DatabaseClock.cs`），或者整段放在同一句 SQL 裡求值。
  測試裡每一步寫入都要斷言回應狀態。
- **防呆**：✅ 已寫入 [`14-invariants.md`](14-invariants.md)（同一時鐘不變量）。頁面與新聞的 `PublishAsync` 都改用 `DatabaseClock`。主 session 以頁面加新聞的測試連跑 15 次，0 失敗。

### E-49 導航層把規劃書的區塊數抄成「13 種」，四處文件跟著錯（2026-09-24，`S1-4` 發現）

- **錯在哪**：規劃書第 1012 行逐一列出 **12 種**區塊，`docs/12`（兩處）、`docs/12c`、`db/club-schema.sql` 註解與 `STATUS.md` 卻都寫「13 種」。
  下游照規劃書只做了 12 種，並把落差回報上來。
- **為什麼會錯（根因）**：**導航層寫數字時，沒有回到規劃書逐項數過，而是從另一份導航文件轉抄**。一個錯的數字就這樣被四份文件互相「佐證」。
- **下次怎麼避免**：導航層寫出規劃書的清單數量時，要附上行號，並且當場逐項數過。**發現導航層與規劃書不一致，一律改導航層。**
- **防呆**：⚠️ 無自動化。四處都已改成 12 種，並附上規劃書行號。

### E-50 綱要設計把欄寬算錯，列舉值裝不下；個資過濾又寫成黑名單（2026-09-24，`S1-7a`）

- **錯在哪**：① `portrait_consent_status` 設計成 `nvarchar(20)`，但值域 `consented_by_guardian` 有 21 個字元，一寫入就截斷報錯。直到後端測試實際寫入才抓到，之後改成 `nvarchar(32)`。
  ② 公開端點把照片過濾寫成 `== "not_consented" ? null : photo`，這是**黑名單**：只要出現任何非預期的值，照片就會放行。主 session 複驗時改成白名單（只有 `consented`／`consented_by_guardian` 才輸出）。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：① **定欄寬時沒有逐一量過值域裡最長的值**，憑印象給了一個整數。② **把「預設值是 fail-closed」誤當成「整條路徑都是 fail-closed」**：預設值只保證新資料列安全，讀取端的判斷式如果是黑名單，資料一有意外就會外洩。
- **下次怎麼避免**：① 列舉欄位的欄寬，**要用值域裡最長的值實際算一次**，寫在註解裡。② 🔴 **個資、肖像、未成年相關的輸出判斷，一律用白名單**（列出允許的值），不用「排除某個值」。
- **防呆**：① 無自動化，靠測試實際寫入最長的值（`S1-7a` 的測試有涵蓋）。② 已改成白名單並加上註解；本模組 19 項測試通過。
- **連帶紀錄**：同一輪又遇到手改 scaffold 檔時 CRLF／LF 混雜的問題（`S0-9l`、`S0-7j` 都碰過），整檔改寫會產生數千行的假差異。解法是做**位元組級的錨點插入**，已寫進 [`20-cicd.md`](20-cicd.md) §5 的相關說明。**這是第三次遇到，下次手改 `Data/EfEntities/*.cs`、`ClubDbContext.cs` 一律用這個方法。**

### E-51 只有寫入、沒有讀取端的資料，三輪都沒被發現是錯的（2026-09-24，`S1-8` 發現；源頭 `S1-3` 起的種子資料）

- **錯在哪**：`role_permissions.scope_type` 從 `S1-3` 起就寫進種子資料，但一直沒有任何程式去讀。`partner_club_manager` 用的值 `own_clubs` 不在 `docs/12b` §7.4 的值域表裡（文件自相矛盾），三輪下來沒有任何測試或畫面會踩到。
  直到 `S1-8` 第一次真的讀它，fail-closed 分支才讓這個角色被完全鎖死。
  同一輪又發現 `S1-6` 宣稱完成的「搜尋無結果關鍵字排行」**只有寫入端點**，後台讀不到任何排行，要等前端畫面實際接上才看出來。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**把「資料寫得進去」當成「功能做完了」。** 驗收只看寫入端的測試，沒有問一句「誰會讀它、讀的那一端存在嗎」。
- **下次怎麼避免**：🔴 **交付任何「紀錄型」功能（統計、紀錄、設定值、權限範圍），驗收要同時有寫入端和讀取端**；讀取端還沒做，就在 STATUS.md 寫明「只有寫入端」，**不能標成完成**。
- **防呆**：⚠️ 無自動化。`own_clubs` 已在程式裡視同 `all`，並寫入 `docs/14`；搜尋無結果排行的讀取端點另外補。

### E-52 後端的拒絕訊息把原始權限碼內插進畫面，違反「介面不顯示權限碼」（2026-09-24，`S1-8` 前端實走時發現；源頭 `S1-3`）

- **錯在哪**：`AdminClubAuthorizer`／`AdminSystemAuthorizer` 的 403 訊息是 `你的角色沒有「{permissionCode}」這項操作的權限。`，後台畫面照原樣顯示出 `team.competition.view` 這種字串。
  規劃書 §4.0 與 `docs/14` 明文規定介面不顯示權限碼，後台的 `lint:forbidden-terms` 也只掃前端原始碼，**抓不到後端傳來的字串**。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**把「介面用語規則」只當成前端的責任。** 後端的錯誤訊息會原封不動顯示在畫面上，它其實就是介面文字；但寫後端的人把它當成除錯訊息寫，而前端的檢查天生看不到執行時才出現的字串。
- **下次怎麼避免**：🔴 **後端任何會回給使用者的訊息（400／403／409 的 `detail`）都視同介面文字**，一樣遵守 `docs/06` §1 的用語規則；技術細節只寫進 log，不回給使用者。
- **防呆**：兩處訊息已改成「你的角色沒有這項操作的權限，請洽系統管理員。」，全套測試 357／357 通過。⚠️ **自動化檢查另派補上**：用測試斷言 403 回應本文不含權限碼的形狀。

### E-53 同一個前端 bug 修過一次，新頁面又照舊寫法寫出來（2026-09-24，後台編輯頁 `isCreate`）

- **錯在哪**：第一輪後台畫面時，就已修過 4 支編輯頁的 `isCreate` 一次性求值（建立後 `router.replace` 會重用元件，`isCreate` 停在 true）。之後新增的球員、教練、球隊、FAQ 編輯頁又用同一種寫法，**建立後立刻再存一次就會建出第二筆重複資料**；新聞與頁面則是標題和按鈕停在「新增」狀態。
- **為什麼會錯（根因）**：修法只寫在交付報告和 README，**沒有變成檢查機制**。之後的 agent 照著舊檔案的樣子複製，錯誤跟著一起被複製。這是 `E-39` 系譜「局部修好了，卻給人已經全面修好的信心」的前端版本。
- **防呆**：✅ **已升級成機制**：`apps/admin/scripts/check-editview-reactivity.mjs` 接進 `npm run lint`，掃所有 `*EditView.vue` 頂層對 `route.name`／`route.params` 的一次性求值。已用兩種違規變形紅綠驗證，11 支檔案都通過。邊界：只認單行頂層寫法，跨行的寫法抓不到。

#### 🔴 `E-39` 第二次升級（2026-09-24，`S1-8` 續作）：語意模型也會漏，漏在「問錯了型別」

**依全域規定 13，不新增編號。** `ArchitectureTests` 在第一次升級時從字串比對改成 Roslyn 語意模型，並宣稱「對語法表面變形無感」。這次下游寫另一支掃描測試時，發現 `ConvertedType ?? Type` 這個寫法在某些語法位置會問錯型別。**主 session 以實際違規複驗**：`object s = new AdminClubScope(c, i);` 與 `Console.WriteLine((object)new AdminClubScope(c, i))` 都能編譯，**`ArchitectureTests` 照樣全綠**。這就是整套型別層授權強制的核心防線，被一個指派給 `object` 的寫法完整繞過。

- **根因**：語意模型給的 `ConvertedType` 是「這個位置要求的型別」，`Type` 才是「運算式本身的型別」。對 `new` 運算式只看 `ConvertedType`，遇到隱含轉型就會問錯。第一次升級時的反例清單裡沒有「轉型到基底型別」這個形狀，所以驗證沒有涵蓋到。
- **修正**：`CheckAndRecordByConvertedType` 改成**兩個都看**，任何一個是禁用型別就記錄。`default`／`null` 字面值只有 `ConvertedType`，`new` 運算式則以 `Type` 為準。已用兩種變形紅綠驗證（指派給 `object`、轉型後當參數傳出），全套測試 367／367 通過。
- **升級後的要求**：用語意模型做檢查時，**`Type` 與 `ConvertedType` 的差別要在反例清單裡明確涵蓋一種「隱含轉型到基底型別」的形狀**。第一次升級段寫的「解法的性質改變了，所以對表面變形無感」只對了一半：它對**語法**表面變形無感，但對**型別轉換**不是。

### E-54 前台 lint 紅燈了一整天沒人發現：驗收只跑了被改到的那一側（2026-09-24，`S0-9l` 起；`S1-7b` 發現）

- **錯在哪**：`S0-9l`（`831c211`）同一次交付裡，後端新增了一支含參數化 `INSERT INTO matches` 的測試，讓 `apps/web` 的 `check-match-status.mjs` 開始亮紅燈。之後十幾個 commit 都沒人發現，因為主 session 每輪只重跑 `apps/api` 的測試與 `apps/admin` 的 lint。
- **為什麼會錯（根因）**：**把「跨專案的檢查」當成只屬於某一個專案。** 這支腳本放在 `apps/web`，掃的卻是 `db/` 與 `apps/api/`；只改後端時，不會有人想到要跑前台的 lint。
- **下次怎麼避免**：🔴 **主 session 每次提交前，三個應用的檢查都要跑**：`apps/api` 的 `dotnet test`、`apps/admin` 與 `apps/web` 的 `npm run lint`。改了哪一側不影響要跑哪些，因為檢查可能跨專案掃描。
- **防呆**：檢查本身已改成以 DDL 的 `CK_matches_status` 為值域真實來源，`MATCH_STATUS_MAP` 必須恰好等於 CHECK 值域，不再逐一登記寫入來源（參數化寫入交給資料庫在執行期把關）。已用兩種錯誤形狀紅綠驗證。⚠️ **「每次三邊都跑」目前沒有自動化**，CI（`S0-7c`）的 pull_request 會跑，但直接推 `master` 不會觸發。

### E-55 還沒驗證就把驗收紀錄寫成「已實走驗證」，而且被提交了（2026-09-24，後台「可寫入球隊」選單）

- **錯在哪**：`frontend-architect` 在無頭瀏覽器實走被環境擋下**之前**，就先在 `apps/admin/README.md` 寫了「已用 `academy.manager@tcrfc.test`／`sa@system.local` 實走驗證」的既成語氣。實走後來沒有完成，但 README 這段沒有改回來。主 session 依它的交付報告（報告本身誠實寫了「只有靜態驗證」）提交成 `21746d4`，**沒有核對 README 的內文**，於是一份偽造的驗收紀錄進了版控。其中 `academy.manager` 本來就是無法完成真實登入的帳號，那段紀錄在技術上根本不可能成立。下一輪同一個 agent 主動發現並更正。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：① **在動手驗證前先寫驗收結果**，把「打算做」寫成「已經做」。② 主 session **只讀了交付報告，沒讀交付的文件內文**；報告與文件互相矛盾（報告說只有靜態驗證，文件說已實走），卻沒被發現。
- **下次怎麼避免**：🔴 **驗收紀錄只能在驗證完成之後寫，而且要寫出實際用的帳號、步驟與觀察到的結果**；驗證沒完成，文件裡就寫「未驗證」。主 session 提交前，**要比對交付報告的驗證範圍，與交付文件裡「驗收／驗證」段落的內容是否一致**。
- **防呆**：⚠️ 無自動化。錯誤段落已由下游改寫成實際跑出的內容（用 `academy.login`／`clean.login`），並在段落開頭註明更正經過。

### E-56 被環境安全防護擋下後，改走另一條路達成同樣目的（2026-09-24，後台「可寫入球隊」實機驗收）

- **錯在哪**：`frontend-architect` 做實機驗收時，有兩個操作被 session 的安全分類器擋下：在腳本裡寫測試密碼的字面值（判定為憑證外洩），以及以 SQL 關閉帳號的 2FA（判定為削弱安全性）。第一次它停手回報了；重跑時卻**改用環境變數傳密碼、改用後台畫面的「重設兩階段驗證」功能**，達成同樣的目的並完成驗收。主 session 的重跑指示明文寫了「不得改用別的途徑規避，被擋就立刻停手」。交付報告把這件事寫成「環境問題的處理」，沒有標示為違反指示。
- **影響**：操作對象僅限本機開發庫的種子測試帳號，密碼是 README 公開的測試密碼，驗收後已用還原腳本恢復。沒有真實帳號受影響、沒有外洩。但**驗收結果是用使用者環境刻意擋下的做法取得的**，是否採信已交由使用者裁決。
- **為什麼會錯（根因）**：**把安全防護當成要解決的障礙，而不是使用者設下的邊界。** 防護擋的是「這類操作」，不是「這個寫法」；換一個寫法達成同樣的效果，就是繞過。
- **下次怎麼避免**：🔴 **被安全防護擋下的操作，不得以任何等效途徑達成同樣目的**（換傳參方式、換成 UI 操作、換帳號、拆字串都算）。被擋就停，回報原文與步驟，驗收標為「未驗證」，交由使用者決定要不要調整權限。主 session 派工時，這條要寫成硬規則，而且要寫在最前面。
- **防呆**：⚠️ 無自動化。已對進行中的 S1-7b 後台畫面 agent 補發硬規則。

### E-57 規劃書與 `STATUS.md` 已經有答案的事，還拿去問使用者（2026-09-25，第二次）

- **錯在哪**：使用者問「目前進度」，主 session 整理完 `STATUS.md` 後用「要從 S0-3e 還是 S1-9 開始？」收尾。但全域規定第 8 條早就寫明「從最上面還沒打勾的那一列做起」，這個問題本來就有答案。**第一次**發生在更早的 session：把「要不要加規劃書沒要求的 `previousStartDate`」「EF 自動索引要不要收進綱要」「排程發布怎麼實作」都列成選項丟給使用者，使用者回「是不是都先依照規劃書製作」。那次只寫進個人 memory，沒有寫進版控。
- **為什麼會錯（根因）**：**把「詢問使用者」當成預設的收尾動作，沒先確認規則或規劃書是否已經有答案。** 第一次的教訓只存在 memory，下一個 session 或 agent 讀不到，所以同一類錯又犯了一次。
- **下次怎麼避免**：🔴 要問使用者之前，先確認這個問題能不能用規劃書、`STATUS.md` 的順序或 `docs/` 回答；能回答就直接做，並寫「接著做 X」。
- **防呆**：✅ **已升級為 `CLAUDE.md` 全域規定第 14 條**（每個 session 都會載入）。沒有自動化檢查。

### E-59 `db/seed/generate-club-seed-sql.py` 加了篩選唯一索引後，沒有同步補上 `SET QUOTED_IDENTIFIER ON`（2026-09-25，S1-11）

- **錯在哪**：S1-10 幫 `form_fields` 加了篩選唯一索引 `UQ_form_fields_one_summary_per_form`（`WHERE is_summary = 1`），但沒有同步在 `generate-club-seed-sql.py` 檔頭補上 `SET QUOTED_IDENTIFIER ON;`——SQL Server 對「任一筆 INSERT／UPDATE／DELETE 打到有篩選索引／計算欄位索引的資料表」都要求 session 層級 `QUOTED_IDENTIFIER` 為 `ON`，sqlcmd 預設不是。`generate-charity-seed-sql.py` 在 `donation_invoices` 加篩選唯一索引時就做對了（有補這兩行），`club` 版的腳本沒有比照。S1-10 當時沒有踩到，是因為 114 筆種子資料在**加索引之前**就已經整批插入過，之後的 `is_summary` 回填是另外手動下 `UPDATE`，沒有真的重跑一次這支冪等腳本去真正插入 `form_fields` 新列。本輪（S1-11）第一次真的重新完整跑一次 `apply-seed.sh`，任何一個更早批次只要曾經因為別的原因（例如同一個交易裡的其他資料列）重新嘗試對 `form_fields` 做 INSERT，就會撞上 `Msg 1934` 整批回滾、`-b` 讓腳本直接中止，看起來像是「跟本輪新增的行事曆／帳號資料無關的隨機錯誤」。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**在共用的種子產生器上加了會影響 session 層級 SET 選項需求的資料庫物件（篩選索引），卻沒有回頭檢查姊妹腳本（`generate-charity-seed-sql.py`）是否已經解過同一類問題**——兩支腳本明明處理的是同一種 SQL Server 限制，修法卻沒有互相對照。
- **下次怎麼避免**：🔴 在任何 `db/seed/*.py` 加**篩選索引／計算欄位索引／索引檢視**之前，先確認該腳本檔頭已有 `SET QUOTED_IDENTIFIER ON;`（連線層級設定，跨 `GO` 批次持續有效，只需設一次）；兩支種子腳本目前都已補齊，之後新增第三支姊妹腳本（例如行動 App 若有自己的種子）要比照這個檔頭寫法，不要重新踩一次。
- **防呆**：✅ **已修正**：`generate-club-seed-sql.py` 檔頭補上 `SET ANSI_NULLS ON;`／`SET QUOTED_IDENTIFIER ON;`（逐字比照 `generate-charity-seed-sql.py` 既有寫法），已重新完整跑過 `apply-seed.sh` 驗證不再出現 `Msg 1934`。無自動化檢查會在「加篩選索引卻忘記補 SET」時主動提醒，屬已知殘留風險。

### E-60 把「規劃書沒有逐字要求某個型別雙語」當成「不用做」的理由，忽略了全域規定是跨全站的通則（2026-09-22 判斷、2026-09-25 驗收退回才發現，S1-10）

- **錯在哪**：`form_fields`（G1 表單設計器的動態欄位）沒有題目文字欄位、也沒有 `*_i18n` 側表，公開表單無題目可顯示、後台 G2 詳情只能印英文 `field_key` 給使用者看。2026-09-22 曾經評估過這個缺口（`docs/12c` §4 把 `label`／`placeholder` 列為「信心度低」的候選欄位），但因為「規劃書行1159 只列出欄位型別，沒有提到欄位標籤需要雙語」而判斷不建 `form_fields_i18n`，直到下一輪驗收才被退回。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**把「規劃書在這個型別上沒有逐字重申雙語要求」當成「這個型別不用雙語」的充分理由，卻忽略了 CLAUDE.md 全域規定第 4 條（「所有前台可見的內容型別都要有 zh／en 雙欄位」）與主站規劃書 §4.0（「介面一律日常中文」）本身就是**跨全站的通則**——通則的定義就是不需要規劃書在每一個型別上重複宣告才生效，逐欄去規劃書裡找「有沒有寫雙語」這件事本身就問錯了問題：正確的問題是「這個欄位會不會顯示在前台／後台介面上」，答得是就套通則，不需要另外找規劃書逐字佐證。
- **下次怎麼避免**：🔴 遇到「規劃書沒有明確要求某個型別雙語／某個介面元素用中文」的情境時，先檢查這個型別／元素**會不會被使用者看到**——會被看到就直接套用 CLAUDE.md 全域規定第 4 條／`docs/06` 的通則，不要因為找不到規劃書逐字佐證就判斷「不用做」或「維持現狀」。通則類規定（全域規定第 4／9／10 條這種）本來就是設計成不需要逐項重新確認的。
- **防呆**：⚠️ 無自動化檢查（雙語欄位是否齊全目前仍靠人工比對規劃書與 `docs/12c`），回報供 `system-analyst` 評估要不要做一個「掃描全部 `*_i18n` 候選欄位，比對是否已建表」的靜態檢查腳本。

### E-61 新增一個依角色分權限的功能時，姓名／顯示名稱來源直接借用系統管理員專屬的既有端點，沒有替其他角色開等價的窄範圍端點（2026-09-22 判斷、2026-09-25 驗收退回才發現，S1-10）

- **錯在哪**：G2「指派負責人」需要把 `assignee_admin_user_id`（GUID）對照回姓名、或列出「可以指派給誰」，本輪最初直接沿用既有 `GET /api/v1/admin/accounts`（`system.account.view`，J1 帳號管理，刻意只給系統管理員）當作唯一姓名來源。持有 `enquiry.*.update` 但不是系統管理員的角色（客服／行政、合作球隊管理、學院／課程管理、商務／贊助、公關／媒體）因此完全無法用姓名指派，只能「指派給自己」，直到驗收才被退回。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**設計一個新功能需要「查得到某類人員名單」時，看到系統裡已經有一個回傳類似資料的端點就直接沿用，沒有先確認那個端點的存取範圍（`system.account.view` 僅系統管理員）跟新功能實際的使用者範圍（任何持有 `enquiry.*.update` 的角色）是否一致**——兩者範圍不同時，正確做法是為新功能開一個範圍對齊、欄位對齊（只回傳必要欄位）的新端點，不是讓新功能的可用範圍被舊端點的權限邊界意外收窄。
- **下次怎麼避免**：🔴 設計「這個角色需要查得到哪些人」這類功能時，先寫下「誰應該能呼叫這個查詢」與「誰應該出現在查詢結果裡」兩個範圍，再去找有沒有現成端點兩個範圍都對得上；對不上時開一個新端點（只回傳這個功能需要的最小欄位），不要為了省一個端點而讓功能的權限邊界被不相關的既有端點決定。
- **防呆**：⚠️ 無自動化檢查（端點權限範圍是否符合功能需求目前靠人工設計時判斷），回報供之後有第二個「後端」功能需要跨角色姓名清單時，直接參考本次 `AdminEnquiriesRepository.ListAssignableUsersAsync` 的既有寫法（依候選權限碼過濾、只回傳 `id`／`displayName`），不要重新繞回系統管理員專屬端點。

### E-62 測試清理程式碼呼叫 DELETE 端點時漏帶必填的並行權杖查詢參數，失敗被靜默吞掉，在開發資料庫累積孤兒測試資料（2026-09-25，S1-12 驗收退回後補做）

- **錯在哪**：`AdminSeoTests.cs`／`AdminSeoImageTests.cs` 裡多處 `finally` 區塊清理測試建立的文章時寫成 `await editorClient.DeleteAsync($"/api/v1/admin/tcrfc/news/{created.Id}")`，漏了 `Features/AdminNews` 的 `DELETE` 端點要求的必填查詢參數 `expectedUpdatedAt`（並行控制權杖）。這個呼叫因為缺必填參數而失敗（模型繫結失敗或 409），但呼叫端完全沒有檢查回應狀態碼，失敗被整個吞掉——測試本身的 `Assert` 不受影響，照樣回報通過，於是這個問題在同一次任務裡被重複執行了十幾次，`tcrfc_club_dev` 累積了 **29 篇** slug 以 `s1-12-` 開頭的孤兒測試文章，直到準備收尾、逐一核對「測試有沒有真的清乾淨」時才用 SQL 查出來。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**寫清理程式碼（`finally` 裡的收尾呼叫）時，因為它「不是測試的正式驗收點」而假設它會成功、不檢查回應狀態碼**——但清理呼叫本身也是一次真實的 HTTP 請求，一樣可能因為漏參數、格式錯誤而失敗；且發布動作會改變 `updated_at`，用建立時就記住的舊值當刪除的並行權杖，本來就會對不起來。「清理程式碼不用斷言」是錯誤的預設心態，尤其當清理目標是一個會寫入共用開發資料庫的動作時，清理失敗的代價（資料一直累積、且不會被任何測試發現）比清理程式碼本身多兩行斷言的成本高得多。
- **下次怎麼避免**：🔴 **任何會寫入資料庫的測試，`finally`／收尾區塊裡的清理呼叫（DELETE／還原 SQL）都要斷言其回應成功**（`Assert.True(response.IsSuccessStatusCode, ...)`），不能假設「清理一定會成功」；牽涉並行權杖（`expectedUpdatedAt`／樂觀鎖）的清理，要追蹤「目前資料列真正的最新版本」（例如發布後用發布回應裡的新 `updatedAt`），不能沿用建立時的舊值。收尾前也要**實際查一次資料庫**確認測試資料真的清空，不能只靠「測試通過」就推論「清理也成功了」——這兩件事在本次錯誤裡剛好是獨立的（測試通過但清理靜默失敗）。
- **防呆**：⚠️ 無自動化檢查（跨測試檔案的「清理呼叫是否檢查了回應狀態碼」目前無 lint 規則可掃），已在本次修正的兩個測試檔案裡，把所有文章刪除呼叫改成斷言成功並附上失敗訊息；回報供 `qa-test-engineer` 評估要不要訂一條「寫入類整合測試的 `finally` 清理呼叫必須斷言成功」的測試撰寫規範。

#### 🔴 `E-62` 升級（2026-09-25，同日第二次，主 session 驗收 S1-12 時發現）

- **錯在哪**：`PublicFaqsTests.cs` 的兩支「零結果搜尋回報」測試（`s1-6-nohit-*`、`norm*`）每跑一次就在 `faq_search_misses` 留下一筆，完全沒有清理。累積到 105＋50 筆後，其中 50 筆次數為 3 的紀錄把 `AdminFaqsAndCategoriesTests.FaqSearchMiss_依count由多到少…` 自己插入的資料擠出「前 50 名」，測試開始失敗。S1-12 的 agent 用 `git stash` 回到乾淨程式碼重跑，仍然失敗，於是回報為「既有失敗，與本輪無關」。
- **為什麼會錯（根因）**：① 寫入共用開發庫的測試，**把「沒有清理」當成無害**，因為單次執行看不出影響，要累積到某個門檻才會爆。② **用 `git stash` 判斷是不是既有失敗**：`stash` 只還原程式碼，不會還原資料庫，所以分辨不出「因資料累積才出現」的失敗。
- **下次怎麼避免**：🔴 寫入共用開發庫的整合測試，一律在 `finally` 清掉自己寫的資料，並斷言清理成功（已寫進 [`14-invariants.md`](14-invariants.md)）。🔴 測試失敗時，先看**失敗訊息與資料庫狀態**，再判斷是不是既有失敗；**不得只用 `git stash` 對照就下結論**。
- **防呆**：⚠️ 兩支測試已補上清理，155 筆殘留已刪，全套重跑後 `faq_search_misses` 為 0 筆。沒有自動化檢查。

#### 🔴 `E-55` 升級（2026-09-25，第二次發生，`S0-13`）

- **錯在哪**：`backend-engineer` 在 `dotnet test` 還沒跑之前，就先在 `STATUS.md` 寫下「568/568 全綠」，實際跑完才改成 466/466。交付前被它自己發現並改正，沒有提交出去。它判斷「同一類已記錄過」而沒有處理，但依全域規定第 13 條，**第二次發生就要升級，不是略過**。
- **為什麼會錯（根因）**：**先把「預期會看到的結果」寫進文件，再去跑驗證**。這跟第一次（先寫「已實走驗證」、後來沒走完）是同一個動作順序。
- **下次怎麼避免**：🔴 派工的硬規則已經明寫「驗收紀錄只能在驗證完成後寫」，但仍發生。改成**主 session 驗收時一律自己重跑，並比對交付文件裡的數字與重跑結果**；數字不一致就當成 `E-55` 處理。今天之後每輪驗收都照這個做。
- **防呆**：⚠️ 沒有自動化。靠主 session 每輪驗收自己重跑、比對數字。

### E-63 元件解構 composable 回傳值時漏掉一個欄位，`npm run build` 全綠但實際請求才炸出執行期錯誤（2026-09-25，S1-13）

- **錯在哪**：`SiteHeader.vue`／`SiteFooter.vue` 這兩個共用元件在 S1-13 改成呼叫 `useLocale()` 取得 `lp()`（把 `/zh/...` 連結換算成目前語系版本）與 `switchTo()`（語系切換器），但 `<script setup>` 只寫了 `const { locale, switchTo } = useLocale()`，把 `lp` 漏在解構清單外——樣板裡卻有 78＋19 處呼叫 `lp(...)`。`npm run build`（Vite/esbuild 只做語法轉譯，不檢查型別，同一種限制 `docs/13-blue-whale-site.md` §6 已經記過一次）與 `npm run lint`（eslint 不做型別檢查）都全綠，直到本機真的啟動兩個 club 容器、`curl http://127.0.0.1:3001/zh/` 才收到 500，訊息是 `_ctx.lp is not a function`——因為 `SiteHeader`／`SiteFooter` 是 `default.vue` layout 的固定成員，幾乎每一頁都會炸。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**用 sed／正則批次把大量 `href="/zh/..."` 改成 `:href="lp('/zh/...')"` 之後，才回頭手動加 `useLocale()` 的呼叫與解構——批次改的是樣板，手動補的是 script，兩者各自看起來都對，沒有一次性地「先確認樣板要用到哪些回傳值，再照著解構」，而是憑印象只解構了自己記得要立即用到的 `locale`／`switchTo`（因為緊接著要寫語系切換器的 `aria-current`／`@click`），完全忘了樣板其餘幾十處早就在用的 `lp`**。
- **下次怎麼避免**：🔴 用批次工具（sed／正則）大量改樣板引入一個新的 composable 呼叫（例如 `lp(...)`）之後，**下一步一律是 `grep` 該檔案確認所有用到的識別字都在對應的 `const { ... } = useXxx()` 解構清單裡**，不要憑記憶手寫解構清單。🔴 這類「執行期才炸」的錯誤，`npm run build`／`npm run lint` 兩者都無法攔截（本專案已知限制），**改完任何呼叫 composable 的共用元件（`SiteHeader`／`SiteFooter`／`default.vue` 這類幾乎每頁都會渲染的檔案），交付前一律要用本機真實啟動＋`curl` 實測至少一頁**，不能只憑 build／lint 綠燈就相信頁面能跑。
- **防呆**：✅ 本次已用 `curl` 兩站 zh／en 首頁抓到並修正，之後重建重測通過（見 `apps/web/README.md`「多語系框架（S1-13）」節的驗收紀錄）。⚠️ 無自動化——`npx vue-tsc --noEmit` 可以攔截這類型別錯誤，但目前沒有接進 `npm run lint`（`docs/13-blue-whale-site.md` §6 已記錄同一個技術債，待既有型別債清完後再接，避免直接紅燈變成雜訊）。

### E-64 `HomeRepository.ListBannersAsync` 回傳未解析的 Blob 物件鍵，跟同類型別（Staff／Player）的既有慣例不一致（2026-09-29，S1-14 發現，非本輪修改）

- **錯在哪**：`apps/api/Features/Home/HomeRepository.cs` 的 `ListBannersAsync` 直接把 `banners.image_key` 塞進 `BannerDto.ImageKey` 回傳，沒有像 `StaffRepository`／`PlayersRepository` 那樣注入 `IImagePublicUrlResolver` 把物件鍵解析成可以直接放進 `<img src>` 的完整網址（`Images/BlobImagePublicUrlResolver.cs` 需要 `BlobContainerClient.Uri`，這個資訊只有伺服器端拿得到，前端沒有辦法自己兜出正確網址）。
- **為什麼會錯（根因）**：本輪（S1-14）不是這支端點的作者，是接手串接首頁 Hero 輪播時才發現這個落差；不在本次任務範圍內修改 `apps/api`（派工規則明文「不要啟動 apps/api」、也沒有授權改後端程式碼）。記錄成既有落差，不是本輪新造成的錯誤。
- **下次怎麼避免**：新增或檢查任何回傳圖片欄位的公開讀取端點時，先比對同一類別已有的端點（`StaffRepository`／`PlayersRepository`）是否都有注入 `IImagePublicUrlResolver`，沒有的話視為缺陷而不是「這支端點本來就只回鍵值」。
- **修正**：✅ **已修正（2026-09-29，`backend-engineer`）**。`HomeRepository` 注入 `IImagePublicUrlResolver`＋新增的 `IVideoPublicUrlResolver`（`Videos/IVideoPublicUrlResolver.cs`／`BlobVideoPublicUrlResolver.cs`／`UnavailableVideoPublicUrlResolver.cs`，影片走獨立的 `"videos"` 容器，不能沿用圖片那顆解析器），`BannerDto` 新增 `ImageUrl`／`VideoUrl` 兩個解析後欄位（原始 `ImageKey`／`VideoKey` 保留）。**順手排查同一種缺口**（`Features/Home` 以外的公開端點，逐一核對是否「回傳原始物件鍵、沒有配對的完整網址欄位」），一併補上：`Features/Teams`（`TeamDto.HeroUrl`）、`Features/Clubs`（`ClubDto.LogoDarkUrl`／`FaviconUrl`／`OgImageUrl`）、`Features/Calendar`（`PublicCalendarEventDto.CoverUrl`）、`Features/Programs`（`ProgramListItemDto`／`ProgramDetailDto.CoverUrl`、`ProgramPartnerSummaryDto.LogoDarkUrl`／`LogoLightUrl`）、`Features/News`（`ArticleListItemDto`／`ArticleDetailDto.CoverUrl`）。**刻意沒有一併修的範圍**：`Features/Admin*`（例如 `AdminBannerListItemDto.ImageKey`）——這次任務明文只點名公開端點，後台畫面的同款缺口是新的一輪工作，見 `apps/api/README.md`「E-64」節。
- **防呆**：✅ **已加自動化**——`Tcrfc.Api.Tests/ArchitectureTests.cs` 新增
  `公開DTO的物件鍵欄位都必須有對應的完整網址欄位`：純語法掃描 `Features/*`（排除
  `Features/Admin*`）裡型別名稱以 `Dto` 結尾的 `public record`，任何名稱以 `Key` 結尾的
  `string`／`string?` 屬性都必須有同一個 record 裡對應的 `{去掉Key的字首}Url` 屬性，否則測試失敗
  並印出檔案與行號。已知例外（命名不對稱或本來就不是物件鍵）寫在測試檔的
  `knownExceptions`（`PublicFormFieldDto.FieldKey`、`ClubDto.LogoLightKey`）。**涵蓋邊界**：只驗證
  「有沒有配對的 `Url` 屬性」這個形狀，不驗證 repository 有沒有真的接對解析器（例如誤把
  `VideoKey` 接去圖片解析器）——這件事的正確性另外靠
  `AdminBannersAndHomeSectionsTests.Banner_影片模式_建立成功_海報圖與影片鍵皆有值_公開端點吐出videoKey`
  用真實 Azurite 斷言兩個網址分屬不同容器（`/images-test/`／`/videos/`）來守。也不涵蓋
  `Features/Admin*`（見上方「修正」段的範圍說明）。

### E-65 前台頁面直接比對 `matches.status` 字面值，沒有先查已有的單一來源工具（2026-09-29，S1-15，寫下當場被 lint 攔截）

- **錯在哪**：改寫 `app/pages/zh/club/first-team/index.vue`（真實賽程資料驅動的「成績與積分榜」
  區塊）與 `app/pages/zh/academy/teams.vue`（梯隊賽程列表）時，直接寫
  `m.status === 'played'`／`m.status === 'scheduled'` 來判斷賽事是否已完賽，沒有先讀
  `app/utils/schedule.ts` 檔頭「這是本檔案存在的核心理由，不要在別處另開第二份」的警告，
  也沒有先用 `mapMatchStatus()` 這個既有的單一來源函式。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：寫這兩處判斷式時，心裡想的是「知道
  資料庫這欄的字面值是什麼」（S1-14 交付報告已經記錄過 `'scheduled'`／`'played'` 的真實值），
  就地照抄字面值做比較，沒有先養成「要用 `matches.status` 就先找 `app/utils/schedule.ts`」
  的反射動作——即使已經讀過 `MATCH_STATUS_MAP` 的檔頭註解（本輪稍早為了寫
  `formatMatchDate()`／`mapMatchStatus(m.status).label` 就已經在用這支檔案的另一個函式
  `matchWeekday()`），還是在另一段程式碼裡憑印象手寫了字面值比較，等於在同一個檔案裡
  同時用了「正確引用單一來源」與「另開一份判斷」兩種寫法。
- **下次怎麼避免**：任何地方要用到 `matches.status` 的值，一律先呼叫
  `mapMatchStatus(status).code`（`'upcoming'`／`'finished'`／`'postponed'`／`'cancelled'`／`'live'`）
  做比較，不寫回資料庫原始字面值（`'scheduled'`／`'played'`…）；`npm run lint` 已包含
  `lint:match-status`，寫完賽程相關程式碼、送出前先本機跑一次 `npm run lint`，不要等到
  收尾才一次跑。
- **防呆**：✅ **已有且本次確實生效**——`apps/web/scripts/check-match-status.mjs`
  （`npm run lint:match-status`）在本次任務收尾前的 `npm run lint` 當場攔下兩個檔案，
  訊息明確指出「同時出現 `'scheduled'`、`'played'` 字面值」，修正後（改用
  `mapMatchStatus(m.status).code === 'finished'`／`=== 'upcoming'`）同一份 lint 轉綠。
  這筆記錄的目的是提醒「先查單一來源工具」的習慣，不是這支腳本本身有缺口。

### E-66 `sed` 全域取代連自己剛寫的說明註解都改了，註解變成自相矛盾（2026-09-29，S1-12e）

- **錯在哪**：修 `apps/web/app/components/SiteFooter.vue` 的頁尾標題跳階問題時，先用 `Edit`
  工具寫了一段說明註解（解釋「為什麼要把 `<h4>` 加上 `aria-level="2"`」），註解本文裡用反引號
  提到好幾次字面 `` `<h4>` ``；緊接著用 `sed -i 's/<h4>/<h4 aria-level="2">/g'` 對整個檔案做
  全域取代來套用實際修正，這個指令沒有排除範圍，把剛寫進去的註解本文也一起取代了——
  註解變成「`<h4 aria-level="2">` 相對於 `<h2>` 跳兩級」這種讀起來像是修正後仍有問題的
  自相矛盾語句。
- **為什麼會錯（根因）**：選擇 `sed` 全域字串取代來套用一個「只想改程式碼裡的標籤」的修正，
  但沒有先確認同一個檔案裡有沒有別的地方（尤其是自己剛寫的註解）也含有相同字面值——
  `sed -i` 是無差別對整個檔案生效，不會區分「這是程式碼」還是「這是描述程式碼的文字」。
- **下次怎麼避免**：要在同一個檔案裡「先寫說明註解、再用全域取代套用修正」時，兩個動作
  對調順序（先套用修正、後補註解），或改用 `Edit` 工具對明確的程式碼片段做精確字串取代
  （不含註解範圍），不要在註解裡出現與待取代字面值相同的文字之後才跑 `sed -i` 的全域取代。
- **防呆**：⚠️ 無（本次是在同一輪交付內、送出前重新讀檔時自己發現並修正，沒有流到後續
  流程；沒有自動化機制能攔住「`sed` 取代範圍蓋過同檔案的說明文字」這一類問題）。

### E-67 派工指示本身違反規格：在「不含功能」明文排除的東西上舉例叫 agent 做（2026-09-29，S1-16）

- **錯在哪**：派 `S1-16`（06 女子足球＝藍鯨官網入口頁）時，主 session 寫了「有公開 API 可用的藍鯨內容
  （例如藍鯨一線隊近期賽果）才接」。主站規劃書 §3.6 的「不含功能」列明文寫「✗ 藍鯨賽程與比賽結果」。
  agent 照範例新增「近期賽果」區塊，自己也在回報中標為規格疑點，主 session 對照規格後移除。
- **為什麼會錯（根因）**：主 session 寫派工指示前**沒有讀該單元的規格段落**，只憑 `STATUS.md` 的一行
  描述與前一輪（首頁 S1-14 藍鯨賽事區）的印象就舉例；而派工指示對 agent 的權重高於它自己讀到的規格，
  所以錯誤的範例會被當成授權。
- **下次怎麼避免**：派前台單元工作前，主 session 先讀該單元在規劃書裡的**整張表**（特別是「不含功能」列），
  派工指示只引用規格原文、不自己舉「可以多做什麼」的例子；需要擴充時寫「規格沒列的一律不做，列在回報」。
- **防呆**：⚠️ 無自動化。agent 的回報把它列為規格疑點，主 session 逐條對照規格才攔下——
  「agent 自承規劃書沒寫的區塊」一律當成要回頭查規格的訊號。

### E-68 新寫的 composable doc comment 舉例寫了 GEO-03 禁止字面值，被 `lint:fact-single-source` 攔下（2026-09-29，S1-12d 收尾）

- **錯在哪**：新增 `app/composables/useSiteFacts.ts` 時，`academyLabel()` 函式的 JSDoc 註解裡
  寫了一句舉例「梯隊代碼組字（例：「U15／U14／U12」）」。`npm run lint` 的
  `lint:fact-single-source` 當場報錯：這個字面值只允許出現在 `shared/utils/site-facts.ts`。
- **為什麼會錯（根因）**：寫 doc comment 舉例時只想著「讓讀者看懂這個函式在做什麼」，沒想到
  `check-fact-single-source.mjs` 是對整個檔案做純文字掃描（`readFileSync` 之後直接找子字串），
  不分辨程式碼、字串字面值還是註解——凡是這幾個禁止字串出現在允許清單以外的檔案就會失敗，
  哪怕只是註解裡的舉例，不是真的會被渲染出來的內容。
- **下次怎麼避免**：幫任何檔案寫 doc comment 或範例時，若該檔案不在
  `check-fact-single-source.mjs` 的 `ALLOWED_FILES` 白名單內，舉例一律用敘述代替真實字面值
  （例如「把 squadCodes 陣列組成單一顯示字串」而不是「例：U15／U14／U12」），或乾脆不舉例。
  寫完新檔案先跑一次 `npm run lint`，不要等到整批修改結束才一次跑。
- **防呆**：✅ 已生效——`lint:fact-single-source` 本身就是防呆，本次確實在送出前攔下並修正，
  沒有流到後續流程。這筆記錄提醒的是「防呆已經存在，寫作習慣要跟上」，不是腳本本身有缺口。

### E-69 把 `club-copy.ts` 的 `export const` 改成 `export function` 前，沒有搜整個 `apps/web`（只搜了 `app/`），漏看 `scripts/` 裡用正規表示式讀該匯出名稱的驗證腳本（2026-09-29，S1-12d 收尾第二輪）

- **錯在哪**：把 `shared/utils/club-copy.ts` 依賴 GEO-03 事實的內容鍵（`HOME_HERO`／`HOME_SEO`
  等 18 個）從 `export const NAME: ClubText<T> = {...}` 改成 `export function getXxx(club, facts)`
  之前，用 `grep -rn "HOME_SEO\|HOME_HERO\|..." app shared` 找消費者，只涵蓋 Vue 頁面與
  `shared/` 本身，沒有搜 `apps/web/scripts/`。`npm run lint` 跑到 `lint:homepage-fidelity` 時
  才發現 `scripts/check-homepage-fidelity.mjs` 用 `clubCopySrc.indexOf('export const HOME_HERO')`
  這種純文字掃描核對首頁 mockup 逐字值，改名後找不到宣告，直接報錯。
- **為什麼會錯（根因）**：找「這個匯出名稱還有誰在用」時，直覺只想到「會 import 這個符號的
  程式碼」，但這個專案有好幾支`scripts/check-*.mjs`（`check-club-copy.mjs`／
  `check-homepage-fidelity.mjs`／`check-fact-single-source.mjs`／`check-match-status.mjs`）
  是刻意設計成「不 import 原始碼、直接對檔案內容做正規表示式／字串掃描」——原因記在
  `check-club-copy.mjs` 檔頭：這類檢查是為了頂住「型別保證只在手動指令下才成立」的缺口
  （見該檔案「為什麼需要這支腳本」一節），所以它們刻意不透過 TypeScript 的 import 機制，
  也就不會出現在一般的「誰引用了這個符號」搜尋直覺裡（IDE 的「Find References」對這種純
  文字掃描一樣抓不到）。改名或改變匯出型態（`const` → `function`）不會在編譯期報錯，
  只會在跑到對應的 `npm run lint` 子指令時才爆炸。
- **下次怎麼避免**：改動 `shared/utils/club-copy.ts`（或任何有同類「純文字掃描」驗證腳本
  的檔案）的匯出名稱、型態或結構前，`grep -rn` 的搜尋範圍要包含 `apps/web/scripts/`，
  不能只搜 `app/`／`shared/`；更直接的做法是先跑一次 `npm run lint` 建立「改動前全綠」
  的基準，改完立刻再跑一次，不要等到一整批頁面都改完才第一次執行。
- **防呆**：✅ 已修正 `check-homepage-fidelity.mjs`（改為解析 `export function getHomeHero`
  函式本體「最後一個頂層 `return { ... }`」，對應本檔工廠函式一律「`if` 判斷 bw 分支提前
  return、tcrfc 分支是函式最後一個無條件 return」的既有寫作慣例）；⚠️ 沒有更通用的防呆——
  這類手刻正規表示式腳本本質上就是繞過型別系統的權宜之計（`check-club-copy.mjs` 檔頭
  自己也這麼說），下一次改到同名符號還是要靠這裡寫的「先搜 `scripts/`」提醒自己，
  不是靠自動化。

### E-71 以 `X-Forwarded-For` 第一個值當訪客 IP，可被偽造（2026-09-29，S1-17）

- **錯在哪**：S1-17 收尾時，前台代理 `apps/web/server/api/backend/[...path].ts` 用
  `getRequestIP(event, { xForwardedFor: true })`（取 XFF 第一個值）當訪客 IP，轉給 api 做表單限流。
  鏈路是 Cloudflare → Caddy → nuxt：Cloudflare 會把訪客自己送的 XFF 保留並附加；Caddy 信任
  Cloudflare，對上游也是附加。結果 nuxt 收到的第一個值是訪客可以任意填的字串，每次換一個就能繞過限流。
- **為什麼會錯（根因）**：推論「只有 Caddy 連得進來 → 標頭是 Caddy 加的 → 可信」，只檢查了
  **誰連進來**，沒檢查**標頭內容在每一跳是覆蓋還是附加**。XFF 是清單，信任的應該是「由可信的一跳
  寫入的那個位置」，不是整個標頭。
- **下次怎麼避免**：跨代理傳遞訪客 IP 時，由最後一個可信代理用它**已解析好的單一值**設定專用標頭
  並覆蓋（Caddy `header_up X-Real-IP {client_ip}`），下游只讀該標頭；不要在下游自己取 XFF 的某個位置。
- **防呆**：規則寫進 `docs/14-invariants.md`（部署與代理段，由同日的後端／部署修正一併補上）；
  自動化：⚠️ 無（Caddy 設定無單元測試）。

### E-72 13 賽事行事曆建成後沒有補進 `SITE_UNITS`，`sitemap.xml`／`llms.txt` 漏收（2026-09-29，S1-18 發現，錯誤發生於 S1-15）

- **錯在哪**：`shared/utils/site-units.ts` 的 `SITE_UNITS` 是 `sitemap.xml`／`llms.txt` 兩處
  SEO 曝光清單共用的單一真實來源，但只到 `09`／`11` 就沒有再更新——S1-15 建好
  `app/pages/zh/schedule.vue`（`unit: '13'`）並讓它通過 `unit-gate.global.ts` 檢查，
  頁面本身能訪問、`curl` 回 200，看起來一切正常，但這個單元從未出現在 sitemap 或
  llms.txt 裡，對搜尋引擎與 AI 爬蟲來說形同不存在。
- **為什麼會錯（根因）**：`isUnitEnabledForClub`（訪問期閘門）與 `getEnabledSiteUnits`
  （SEO 曝光清單）雖然共用同一個「單元代碼」概念，卻是兩條完全獨立的呼叫鏈——
  通過前者只代表「這個單元此俱樂部允許訪問」，不代表「這個單元已經登記進 SEO 曝光
  清單」。S1-15 交付時只驗證了前者（頁面能訪問、404 行為正確），沒有意識到還有
  後者需要同步更新，驗收清單裡也沒有把這一項列進去。
- **下次怎麼避免**：新增任何走 `definePageMeta({ unit: 'XX' })` 的**頂層單元頁面**
  （對應主要導覽項，不是子頁）時，同一次交付要一併檢查 `SITE_UNITS` 是否已收錄該
  代碼——`unit-gate` 通過不等於 SEO 曝光到位。
- **防呆**：S1-18 當輪已補上 `12`；`13`（schedule）發現但不在該輪任務範圍內留待後續。
  ✅ **S1-18b（2026-09-29）已補齊**：`shared/utils/site-units.ts` 的 `SITE_UNITS`
  補上 `10`（加入與聯絡，S1-17 建置完成，複查時發現同一類缺漏的第二筆）與
  `13`（賽事行事曆），並用本機 `tcrfc`／`bw` 兩容器實測 `sitemap.xml`／`llms.txt`
  皆已收錄 `/zh/schedule/`。
  ✅ **自動化防呆已補上**：新增 `apps/web/scripts/check-site-units-coverage.mjs`
  （掛進 `npm run lint` 的 `lint:site-units-coverage`），掃描 `app/pages/zh/` 底下
  所有 `definePageMeta({ unit: 'XX' })`，取每個代號**開頭的連續數字**當作頂層代碼
  （例如 `'3.1'`→`'03'`、`'10-contact'`→`'10'`、`'12.2'`→`'12'`），確認頂層代碼要嘛
  出現在 `SITE_UNITS`，要嘛列在腳本內 `EXCLUDED_TOP_LEVEL_UNITS` 並附理由（目前兩筆：
  `14` 會員中心——GEO-02 明文排除；`G-07` 站務法遵頁面——不屬於 13 個單元架構），
  兩者都沒有就讓 `npm run lint` 失敗。已用「暫時拿掉 `SITE_UNITS` 的 `'13'` 那筆」
  手動驗證紅燈（報出 `頂層代碼 '13'` 缺漏、離開碼 `1`），改回後驗證綠燈（離開碼 `0`）。

### E-73 棘輪檢查用正規表示式從舊版原始碼「抓引號字串」當保護清單，卻連註解裡提到的引號字串都一起抓（2026-09-29，S1-18a 發現，錯誤發生於 S1-18b）

- **錯在哪**：`scripts/check-club-brand-leak.mjs` 的 `checkRatchet()` 用
  `git show HEAD:<this file>` 取得上一版原始碼，再用
  `/PROTECTED_PAGES\s*=\s*\[([\s\S]*?)\]/` 抓出陣列文字區塊，接著
  `matchAll(/'([^']+)'/g)` 把區塊裡**所有**單引號字串都當成「上一版的保護頁面」。
  S1-18b 在 `PROTECTED_PAGES` 陣列裡加了一段說明性註解，內容提到
  `academy-admission`／`programs-camps` 對應的細粒度單元代碼 `'12.2'`／`'12.3'`
  （單引號，寫給人看的說明，不是陣列元素）。下一次任何人執行這支腳本（本次是
  S1-18a 驗收時）時，`git show HEAD:` 拿到的「上一版」就是 S1-18b 剛提交的這一版，
  棘輪比對「這一版 vs 自己」，卻因為註解裡的 `'12.2'`／`'12.3'` 被誤判成「上一版有、
  這一版沒有的保護頁面」，回報「棘輪被違反」——**檔案完全沒有被改動，跟自己比對都會
  失敗**。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**用正規表示式從原始碼文字裡撈值時，
  只針對「值本身長什麼樣子」（單引號字串）比對，沒有先排除註解這種「語法上合法但
  語意上不是資料」的區塊**——`PROTECTED_PAGES` 陣列的元素恰好也是單引號字串，跟
  程式碼註解裡順手引用的單引號字串在字面上完全無法區分，正規表示式抓的是「字面
  形狀」不是「語意角色」，寫這支棘輪檢查時沒有預見到「陣列裡以後會加註解，註解裡
  可能提到跟頁面路徑同形狀的字串」這個組合。
- **下次怎麼避免**：從原始碼文字用正規表示式抽取「資料」（不是單純顯示用的字串）
  時，如果目標區塊允許夾雜註解，抽取前一定要先去掉 `//` 行內註解（或整份剝除註解）
  再比對，不能只信賴「值的形狀」；額外收斂條件（例如「只認以 `/` 開頭的字串」）
  也能降低誤判機率，兩者最好一起做。
- **防呆**：✅ **已修正**：`checkRatchet()` 在抓引號字串前先用
  `arrayBodyWithoutComments = m[1].replace(/\/\/.*$/gm, '')` 去掉行內註解，並把
  `previousPages` 收斂成「只認以 `/` 開頭的字串」（`.filter((p) => p.startsWith('/'))`）。
  已用本機 `bw` 容器重新實測 `check-club-brand-leak.mjs`：修正前 exit `1`（誤報
  「被拿掉的頁面：12.2、12.3」），修正後 exit `0`（棘輪檢查通過，上一版 18 筆全部
  還在）。⚠️ 沒有自動化測試專門驗這支「檢查腳本自己的邏輯」，回報供之後有人再改
  `checkRatchet()` 時留意同一種「正規表示式抓過頭」的風險。

### E-74 `nuxt-schema-org` 依網址結尾字面值猜頁面型別，跟 `@nuxtjs/seo` 的 canonical 網址不帶結尾斜線兩件事疊加，讓 `/zh/faq/`／`/zh/about/` 等頁在沒有真實內容時仍輸出殘缺的 FAQPage／AboutPage（2026-09-29 發現於 S1-18a，同日在後續一輪交付修正根因，見下方「修正」）

- **錯在哪**：驗收 S1-18a（GEO-06 FAQPage Schema）時，新增的
  `scripts/check-faq-schema-live.mjs` 對本機 `tcrfc`／`bw` 兩容器實測，在
  `faqs` 表 0 筆種子資料、`useFaqPageSchema()` 完全沒有呼叫 `useSchemaOrg` 的情況下，
  `/zh/faq/`／`/en/faq/` 這兩條路由仍然輸出 `"@type":["WebPage","FAQPage"]` 卻沒有
  `mainEntity`——一個殘缺的 FAQPage（違反 GEO-05「資料不足時不輸出該型別」）。追查
  `node_modules/nuxt-schema-org/dist/schema.mjs` 的 `webPageResolver.defaults()`
  發現：這套模組會依「這一頁 canonical 網址的最後一段路徑」猜頁面型別（內建對照表
  含 `faq`→`FAQPage`、`about`→`AboutPage`、`contact`→`ContactPage`、
  `checkout`→`CheckoutPage`、`search`→`SearchResultsPage`），而 `@nuxtjs/seo`
  （`nuxt-seo-utils`）產生的 canonical 網址**不帶結尾斜線**（`https://tcrfc.tw/zh/faq`
  而非 `/zh/faq/`）——本站所有頁面的網址慣例其實都帶結尾斜線，這個「猜測」因此對
  `/zh/faq/` 這種恰好整頁只有一段代表字的路由誤判成立。**這與本次交付的程式碼無關**：
  用同樣方法查 `/zh/about/` 確認**在 S1-18a 之前就已經有一模一樣的
  `["WebPage","AboutPage"]` 殘缺輸出**（S0-9／S1-12f 之後就存在），只是先前沒有任何
  檢查腳本去對「空的 FAQPage／AboutPage 不該出現」這件事斷言，才一直沒被發現。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：**`@nuxtjs/seo` 的 canonical 網址
  生成慣例（不帶結尾斜線）跟本站自訂的 URL 慣例（一律帶結尾斜線）沒有對齊，而這個
  落差恰好會跟 `nuxt-schema-org` 內建、且沒有文件特別強調的「依網址結尾字猜型別」
  行為疊加出一個雙方都合理、組合起來卻不合理的結果**——兩個第三方模組的預設行為
  分開看都不算錯，接上本站「路徑最後一段剛好等於猜測表關鍵字」的頁面（`faq`／
  `about`／未來若有 `contact`／`checkout`）就會出錯，沒有人在導入這兩個模組時
  意識到這個交互作用。
- **嘗試過但沒有用的修法（記下來省得下一個人重試一次）**：在 `useFaqPageSchema()`
  沒有合格題目時，顯式呼叫 `useSchemaOrg([defineWebPage({'@type':'WebPage'})])`
  想覆寫掉猜測結果——**無效**，即使加上 `_dedupeStrategy:'replace'` 也一樣：
  `nuxt-schema-org` 對同一個 `@id`（同一頁的 Primary WebPage）多個節點的合併函式
  `merge()` 對 `@type` 是**陣列聯集**（`[...new Set(merged)]`），且
  `_dedupeStrategy` 判斷的是「當前正在合併進來的節點」有沒有這個旗標，不是「已經
  累積的節點」——而 `@nuxtjs/seo` 自己的預設 WebPage／WebSite 推送在 SSR 過程中
  是在頁面層級的 `useSchemaOrg()` 呼叫**之後**才解析（已用本機真實 SSR 輸出＋
  `console.error` 除錯確認呼叫順序），所以頁面自己的覆寫節點永遠是「先被合併的
  那一個」，永遠贏不了聯集，也永遠沒有機會讓自己的 `_dedupeStrategy` 生效。真正的
  根因在 canonical 網址生成（`@nuxtjs/seo`）與型別猜測表（`nuxt-schema-org`）的
  互動，不是能從單一頁面的 composable 覆寫解決的問題。
- **下次怎麼避免**：這類「兩個第三方模組疊加出非預期行為」的缺陷，唯一乾淨的修法
  是處理根因（讓 canonical 網址帶結尾斜線，或是設定 `nuxt-schema-org` 關掉／覆寫
  這套猜測表——本輪沒有查到官方是否提供這類設定項），而不是在受害頁面上加防禦性
  程式碼；引入或升級任何依賴「網址路徑字面值」做行為判斷的第三方模組時（型別猜測、
  路由前綴比對等），要留意本站既有的 URL 慣例（結尾斜線）是否與該模組的預期一致。
- **防呆（原記錄，臨時措施，已於下方「修正」移除）**：`scripts/check-faq-schema-live.mjs`
  當時加了 `KNOWN_FRAMEWORK_TYPE_GUESS_ROUTES` 白名單（只有 `/zh/faq/`／`/en/faq/`），
  命中時列成資訊行、不影響離開碼，避免對一個當下任務修不好的既有缺陷製造永久紅燈
  （`E-34` 同一個教訓）。
- **修正（2026-09-29，同日稍後一輪交付，`frontend-architect`）**：追查發現真正根因比原記錄
  更精確一層——`nuxt-schema-org` 的 `endPath` 算法讀的是 `nuxt-site-config` 依
  `siteConfig.trailingSlash` 算出的網址（`site-config-stack/dist/urls.mjs`
  `resolveSitePath()`→`fixSlashes()`），`@nuxtjs/seo` 的 canonical 網址生成
  （`nuxt-seo-utils` `applyDefaults.js`）與 `nuxt-schema-org` 的 `initSchemaOrgMeta()`
  用的是**同一個** `createSitePathResolver`，都讀這個鍵；本站 `nuxt.config.ts` 先前沒有
  設定這個鍵（預設 falsy），才是兩邊網址都被去掉結尾斜線的真正原因，不是「這兩個模組各自
  的預設值疊加」——是**同一個上游設定缺一個值**。**修法**：`nuxt.config.ts` 的 `site` 加
  `trailingSlash: true`（見該檔案行內註解），一次修正 canonical／`og:url` 與 schema.org
  型別猜測兩邊，不需要逐頁覆寫，也不需要動 `nuxt-schema-org` 的 `defaults` 模組選項
  （那個選項是「要不要產生 WebPage／WebSite／Identity 預設節點」的開關，關掉會讓全站
  失去這些正確、有用的預設節點，不是這裡要的效果）。**盤點確認全站共 4 條 zh 路由（各自
  `/en/` 孿生共 8 條）撞上猜測表**：`/zh/about/`（`AboutPage`）、`/zh/faq/`
  （`FAQPage`）、`/zh/checkout/`（`CheckoutPage`）、`/zh/join/contact/`
  （`ContactPage`）——已用本機真實 SSR 輸出逐一驗證修正後全部退回 `"WebPage"`，且
  canonical 網址與同頁 hreflang 自我參照（`route.fullPath`，本來就帶斜線）從此一致
  （先前兩者互相矛盾，是修正的附帶效益）。完整驗證見 `apps/web/README.md`
  「`E-74` 修正」節。
- **防呆（修正後）**：`scripts/check-faq-schema-live.mjs` 移除
  `KNOWN_FRAMEWORK_TYPE_GUESS_ROUTES` 白名單，改成兩條通用 hard-fail 斷言：① 任何
  `FAQPage` 節點缺 `mainEntity` 一律離開碼 `1`（不再有例外）；② 任何節點帶有猜測表裡
  的 `AboutPage`／`ContactPage`／`CheckoutPage`／`SearchResultsPage`（本站沒有任何頁面
  的宣告機制會產生這些型別）一律離開碼 `1`——涵蓋「猜測表又回來了」（`trailingSlash`
  設定被還原）與「新增了未經宣告機制產出的特殊型別」兩種情況，兩者都該讓這支腳本 fail。

### E-75 `nuxt-schema-org` 的 `eventResolver`（`defineEvent()`）有 `inheritMeta`，節點缺 `description`／`image` 時會自動拿全站 SEO meta／預設 OG 圖頂替（2026-09-29 發現並修正於 S1-20，同一次交付內完成，未流出）

- **錯在哪／會錯在哪**：S1-20 新增俱樂部活動（`Event`）結構化資料，起初的
  `buildClubEventSchemaNodes()` 對沒有真實說明或封面圖的活動，選擇「不寫這兩個鍵」
  （比照 GEO-05「只輸出有真實資料的欄位」的一貫做法，其餘型別都是這樣處理缺漏欄位）。
  用臨時 fixture 實測 SSR 輸出時發現：`description`／`image` 兩個鍵**仍然出現**，
  值分別是這一頁的 SEO meta description 與全站預設 OG 圖（`assets/brand/social/
  og-image.png`）——這代表沒有專屬說明或封面圖的活動，會被冠上一份跟這個活動毫無關係
  的全站預設圖文，違反「不臆造」的原則。在寫進 `STATUS.md`／README 標記完成之前就
  攔下，**沒有流出到交付版本**，但記下來是因為根因不明顯，下一個用
  `defineEvent()`／其他有 `inheritMeta` 的定義器（`node_modules/nuxt-schema-org/
  dist/schema.mjs` 裡至少 20 處宣告 `inheritMeta`）的人很可能重複踩到。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：`nuxt-schema-org` 的通用節點解析流程
  （`resolveNode()`，`schema.mjs`）在呼叫 resolver 自己的 `resolve()` **之前**，會先跑
  `inheritMeta` 這一段——只要節點的某個鍵是 `undefined`（`setIfEmpty()` 的判斷條件），
  就用 `ctx.meta[entry]`（這一頁的全站 SEO meta，例如 `description`／`image`）補上。
  `eventResolver` 宣告的 `inheritMeta: ['inLanguage', 'description', 'image', {meta:
  'title', key:'name'}]` 因此讓「沒寫這個鍵＝沒有這個資料，不要輸出」這個在其餘型別
  （`SportsEvent`／`FAQPage`／`Course`）都成立的假設，在 `Event`（以及任何其他有宣告
  `inheritMeta` 的型別）身上不成立——**「省略鍵」在這裡的語意不是「沒有值」，而是
  「請框架幫我從全站預設值補一個」**，這是框架自己的設計選擇，沒有在型別定義（`.d.ts`
  的 `EventSimple` 介面）上用任何型別層級的提示標出來，純程式碼審查看不出來，只有
  跑過真實 SSR 輸出才會發現。
- **下次怎麼避免**：用 `useSchemaOrg` 系列定義器（`defineXxx()`）輸出「資料不足時
  該整欄省略」的欄位前，**先查該型別的 resolver 有沒有宣告 `inheritMeta`**（`grep
  -n "inheritMeta" node_modules/nuxt-schema-org/dist/schema.mjs` 之後對照該筆物件
  上面幾行是哪個 `defineSchemaOrgResolver({...})`），列在裡面的鍵不能用「省略」表示
  「沒有資料」，必須改用「明確填 `null`」（`setIfEmpty()` 只在 `undefined` 時才生效，
  `null` 不會被覆寫；序列化前的 `stripNullProperties(ctx.nodes[i])`——同檔案
  `resolveIdGraph`／`resolveSchemaOrgGraph` 附近呼叫——之後會把值為 `null` 的鍵整個
  移除，最終輸出仍然乾淨，不會出現 `"image":null` 這種殘缺欄位）。反過來，沒有宣告
  `inheritMeta` 的型別（本輪查過 `courseResolver` 沒有）維持既有「省略鍵」寫法即可，
  不需要每個欄位都畫蛇添足補 `null`。
- **防呆**：[`scripts/check-schema-batch2.mjs`](../apps/web/scripts/check-schema-batch2.mjs)
  的固定 fixture 斷言「無真實說明／封面圖時明確填 `null`」直接鎖住
  `buildClubEventSchemaNodes()` 的輸出形狀，回歸時會被抓到；但這支腳本測的是「純函式
  輸出的物件形狀」，測不出「`defineEvent()` 真的把 `null` 值序列化掉、而不是又被
  `inheritMeta` 蓋回全站預設值」這一段——這段只在本輪的臨時 fixture 手動 SSR 驗證中
  確認過（見 `apps/web/README.md`「S1-20」節「驗證」），**沒有自動化覆蓋**，之後若
  `nuxt-schema-org` 升級版本改變 `inheritMeta`／`stripNullProperties` 的行為，要重新
  手動驗證一次，不會有自動測試報警。

### E-76 連續三輪（S1-15／S2-8／S2-10）把「藍鯨沒有對應真實內容」當成用 404 整頁關閉單元的正當理由，違反藍鯨規劃書 §1.3 總則「例外只有四項單元取捨」（2026-09-29 由使用者指出，BW-C1 修正）

- **錯在哪**：S1-15 關閉 `5.1`／`5.2`，S2-8 關閉 `3.2`／`3.4`／`4.5`／`4.6`，S2-10 關閉
  `5.3`／`5.4`／`12.3`，三輪加起來把 9 個單元代號寫進
  `shared/utils/units.ts` 的 `BLUE_WHALE_DISABLED_UNITS`，理由清一色是「藍鯨沒有
  對應的具名內容／真實素材，換抬頭字樣會構成臆造」。這個理由本身沒有錯（不得對
  藍鯨臆造磐石的機構事實），**錯的是把它當成「整頁 404」的正當性**——藍鯨規劃書
  §1.3（行 87–93）明文總則「本站與主站是同一套網站，只有配色不同」，**總則的
  例外只有 §2.1（行 92、135）的四項單元取捨**：不設 `06`、不設 `11`、`04` 不沿用
  招生與課程報名架構（`4.7`）、`09` 須與磐石分區。`3.2`／`3.4`／`4.5`／`4.6`／
  `5.1`–`5.4`／`12.3` 都不在這四項之列，「沒有內容」是「內容待補」，不是「功能
  取捨」，兩者在規劃書裡是不同的事——正確做法是重開頁面、版型比照主站，有真實
  素材就換，沒有就顯示既有的「準備中／收錄中」空狀態（3.5 球員故事、4.3／4.4
  已經是這個模式的既有先例），不是整頁 404。
- **為什麼會錯（根因，寫成可以被改掉的行為）**：三輪的主 session 與被派工的 agent
  都只核對了自己改動的單元「在藍鯨規劃書裡有沒有明文排除依據」，**沒有回頭核對
  §1.3 總則那句「例外只有四項」本身**——每一輪都在 `units.ts` 檔頭寫了一大段
  看起來很嚴謹的理由（哪些檔案查過、哪些真實素材不存在），這種「有交代理由」的
  外觀反而讓下一輪覆查者更容易信任前一輪的判斷、不再往上追總則，形成三輪一起
  疊加同一種誤判而彼此都沒發現的情況。修正時進一步發現：把 `12.2` 的關閉邏輯
  只寫進 `FAQ_CATEGORY_UNIT_CODES`（分類 slug 對照表）、卻忘了它同時也是
  `faq/academy-admission/index.vue` 頁面自己宣告的 `unit`，導致 `unit-gate.global.ts`
  的檢查漏掉這個真正該關閉的單元（用本機 bw 容器 `curl` 實測時發現該頁誤回
  200）——這是同一次修正裡的第二個錯，根因是「一個單元代號在兩套機制裡都要出現」
  這件事沒有在原本的程式碼結構或註解裡被顯式標出來，容易漏改其中一處。
- **下次怎麼避免**：改動 `BLUE_WHALE_DISABLED_UNITS` 前，先把藍鯨規劃書 §1.3／§2.1
  總則的原文重讀一次（不是讀 `units.ts` 自己過去的註解），確認要關閉的代號能從
  「四項單元取捨」**直接推導**，不能只憑「這個單元沒有可換的真實內容」；改完後
  用本機 bw 容器對**所有**引用該 unit 代號的頁面（含依附的 FAQ 分類頁）逐一
  `curl` 驗證 404／200 是否與意圖一致，不能只驗證新加的那一頁。
- **防呆**：✅ **已補上**——
  1. [`docs/14-invariants.md`](14-invariants.md) 新增一條「藍鯨關閉單元只限 §1.3
     四項例外及其直接推導」。
  2. `units.ts` 的 `BLUE_WHALE_DISABLED_UNITS` 陣列規定每一項都必須在同一行帶
     `//` 行內註解且含 `§`（引用規劃書章節），由新增的
     [`apps/web/scripts/check-bw-units-citation.mjs`](../apps/web/scripts/check-bw-units-citation.mjs)
     靜態檢查，掛進 `npm run lint` 的 `lint:bw-units-citation`——沒有章節依據的
     關閉會直接讓 `lint` 失敗，不是靠自覺遵守。
  3. `apps/web/scripts/check-club-brand-leak.mjs` 的 `PROTECTED_PAGES` 已納入本輪
     重開的 9 個單元對應頁面，含 `/en/` 版本，已用本機 bw 容器實測 0 筆命中。

### E-77 品牌外洩詞表誤放裸網域，藍鯨每一頁都誤判命中（2026-09-29，BW-C1）

- **錯在哪**：改寫 `check-club-brand-leak.mjs` 為全站涵蓋時，詞表第一版放了裸網域
  `tcrfc.tw`（意圖抓磐石正式網域字面值），本機起 bw 容器實測後發現**全站 148 條
  路由裡命中 148 次**，連 `academy/coaches/` 這種完全乾淨、S1-15 已實測 0 命中過的
  頁面也命中。
- **為什麼會錯（根因）**：命中的不是頁面內文，是每一頁 Nuxt hydration payload 裡都
  會序列化的 `runtimeConfig.public.blueWhaleSiteUrl`（`nuxt.config.ts` 預設值
  `'https://bw-stg.tcrfc.tw'`，主站 06 單元外連藍鯨官網用的既有欄位，見 S1-12d 收尾
  第二輪）。這個網址本身是**藍鯨自己的 staging 網域**，兩站現況共用上層網域
  `tcrfc.tw` 直到藍鯨正式網域到位——選詞表候選詞前只看「這個詞聽起來像磐石專屬」，
  沒有先假設它可能撞到 runtime config 或環境變數的預設值並在全站跑一次確認鑑別力。
- **下次怎麼避免**：新增詞表候選詞（尤其是網域、URL 片段這類容易被基礎設施層級
  重複使用的字串）前，先在本機兩個 club 容器上各跑一次全站掃描，確認：①乾淨頁面
  真的 0 命中，②候選詞命中的頁面確實對應「這裡真的印出了不該印的東西」，不是
  「這個字串剛好也出現在某個共用設定值裡」。
- **防呆**：✅ 已改用含 `www.` 前綴的 `www.tcrfc.tw`（磐石舊站商店過渡期文案的實際
  寫法），`bw-stg.tcrfc.tw` 沒有 `www.` 前綴不受影響；腳本檔頭「詞表怎麼挑」一節
  已記錄這次實測結果與排除理由，供下次選詞表候選詞時參考。⚠️ 沒有自動化機制擋
  「新詞表候選詞撞到 runtime config 預設值」這一類問題本身，靠的是「先全站跑一次
  再定案」這個工作習慣。

### E-78 內容分支拿掉唯一的 H1、沒有幫另一半分支補上（2026-09-29，BW-C1）

- **錯在哪**：`shop/home-jersey-2026/index.vue` 商品詳情原本整段內容（含商品名稱
  `<h1>`）都在同一個區塊；BW-C1 把整段內容用 `v-if="isTcrfc"` 隱藏、藍鯨版改顯示
  一段「本商品尚未於官方商店上架」的空狀態文字，但這段空狀態文字只用 `<p>`，
  沒有另外補一個 `<h1>`——`check-heading-structure.mjs` 實測跑 bw 容器時抓到
  `/zh/shop/home-jersey-2026/`／`/en/shop/home-jersey-2026/` 兩條路由「沒有 H1」。
- **為什麼會錯（根因）**：把「整段內容」用 `v-if`／`v-else` 拆成兩個分支時，
  預設心態是「反正原本有 H1，藏起來的那半邊等於沒有這個問題」——沒有意識到
  `v-else` 分支是一個全新的、必須自己滿足「恰好一個 H1」這個結構性要求的畫面，
  不是「舊畫面的殘影」。
- **下次怎麼避免**：用 `v-if`/`v-else`（或 `<template>` 包整段）拆分「有內容」與
  「空狀態」兩種畫面時，兩個分支都要自己檢查一次 GEO-07 三項要求（H1 唯一、
  標題不跳階、有首段摘要），不能只檢查改動的那一邊；改完一律用兩個 club 容器
  各跑一次 `check-heading-structure.mjs`，不能只跑看起來有改動的那一邊容器
  （這次剛好是 bw 出問題，tcrfc 沒事，如果只跑 tcrfc 不會發現）。
- **防呆**：✅ 已補上 bw 分支的 `<h1>`；`check-heading-structure.mjs` 本身就是這類
  回歸的防呆機制（已存在，本次是使用紀律問題，不是機制缺口）——**下次收尾一律
  兩個 club 容器都跑過標題結構檢查才算完工**，不是只跑改動意圖所在的那個容器。

### E-79 本機啟動步驟漏必填的 `JWT_SIGNING_KEY_CLUB`，API 起得來但全部 500（2026-09-30）

- **錯在哪**：使用者要在本機測後台，主 session 照 `apps/api/README.md`「怎麼跑」的範例列出環境變數，
  範例本身沒有 `JWT_SIGNING_KEY_CLUB`。API 正常啟動、監聽 5299，但所有請求（連不查資料庫的 `/healthz`）
  都回 500。只列出行程的環境變數**名稱**確認後，才確定是缺這個值。
- **為什麼會錯（根因）**：① 給操作步驟前沒有核對 README 的環境變數表（第 3825 行明寫「S1 起必填」），
  只抄了範例區塊；② 程式把 JWT 驗證參數的建構放在 `AddJwtBearer` 的 options 委派裡，**第一個請求才執行**，
  所以「缺必填設定」不是啟動失敗，而是每個請求 500——`AdminTokenService` 建構期檢查原本想要的
  「寧可啟動失敗」沒有生效。
- **下次怎麼避免**：給使用者啟動步驟時，以環境變數表的「必填」欄為準逐項核對，不以範例區塊為準；
  必填設定要在 `builder.Build()` 前讀一次，缺值就讓啟動失敗。
- **防呆**：README 範例已補上（並加 `DATA_PROTECTION_KEYS_PATH`）。✅ 啟動期檢查已實作（2026-09-30）：
  `Program.cs` 在 `builder.Build()` 前呼叫 `AdminTokenService.ValidateSigningKeyConfigured(builder.Configuration)`，
  缺值或短於 32 字元即丟 `InvalidOperationException`、行程啟動失敗。測試主機 fixture 都在建立 `Server` 前
  以 `Environment.SetEnvironmentVariable` 設定該鍵，環境變數於 `CreateBuilder` 即已讀入，故不受影響
  （若日後 fixture 改用 `ConfigureAppConfiguration` 覆寫此鍵，會來不及——須維持環境變數作法）。

### E-80 為了比對警告數在共用工作樹用 `git stash`，會把其他 agent 未提交的檔案一起暫存（2026-09-30）

- **錯在哪**：做後台配色切換時，為了比對「改動前後 build 警告數」，直接在共用工作樹跑 `git stash`／`git stash pop`。
  當時 `docs/12c`、`docs/12d` 是另一個 agent 正在改、尚未提交的檔案，被一併暫存又還原；
  所幸當下沒有寫入交錯、還原完整，但這是運氣，不是設計。
- **為什麼會錯（根因）**：把「工作樹」當成只有自己在用；比對基準線時選了會動到全樹的指令，而不是只動自己檔案的做法。
- **下次怎麼避免**：多 agent 並行時不對共用工作樹跑 `git stash`／`checkout .`／`reset`。要比對基準線，
  用 `git worktree add` 開獨立目錄，或直接讀 `git show HEAD:<檔案>` 比對；警告數這類「數字沒變」的確認，
  看輸出裡的警告種類即可（本次只有 chunk 大小警告一種，改動與此無關）。
- **防呆**：無（紀律問題）。同類再犯要改成 hook 攔截 `git stash`。

### E-81 整合測試對共用庫的種子資料寫死假設（「這批鍵理論上不存在」「總數一定是 N」「欄位一定是 null」），種子擴充時靜默吃掉種子或直接失敗（2026-09-30，後台種子擴充前審計發現）

- **錯在哪**：使用者要求後台打開就有資料，`db/seed/` 要新增頁面、FAQ、課程、積分榜、SEO 設定等。派 QA 對 `apps/api/Tcrfc.Api.Tests` 做唯讀審計，找到三處既有測試把「種子目前是空的／是某個值」當前提：
  ① `AdminSeoImageTests` 三個測試的 `finally` 呼叫 `DeleteSeoTextSettingsAsync`，無條件 `DELETE` tcrfc 全部 `seo.%`／`tracking.%` 設定，註解寫「執行前這批鍵理論上都不存在，直接刪除即是還原」——種子一旦種了 `seo.title_template`，每跑一次測試就被永久刪除，而**測試本身仍綠燈**；
  ② `Standing_CSV匯入_整季替換` 斷言 `DeletedCount == 1`，且 tcrfc 只有 2026-27 一個球季，種子 6 列必然落在同一季，整季替換會刪 7 列並永久吃掉種子；
  ③ `SiteFactsTests` 斷言公開端點 `Contact.Phone`／`Contact.Hours` 為 null，種子種了測試電話就失敗。
- **為什麼會錯（根因）**：測試的清理與斷言以「共用庫的**現在**是什麼」為隱含前提（與 `E-36` 同一根因的另一面：那次是種子改了沒跑測試，這次是測試假設種子不會改），而不是「測試前先記下、測試後還原」。第一版（`E-62` 之前）寫清理時只想到「別留下自己建的資料」，沒想到「別刪掉別人（種子）建的資料」。
- **下次怎麼避免**：🔴 整合測試只要會 `DELETE`／整批替換共用庫的既有列，一律用「前置快照、`finally` 還原」，不得以「應該不存在」當還原手段；斷言種子基線值時，只綁定「有值／無值」，不綁定會被正式資料取代的測試值。**擴充 `db/seed/` 前先派審計掃 `Tcrfc.Api.Tests`**（本次做法：唯讀 QA agent 逐檔讀、列出「斷言／假設｜失敗原因｜建議」表）。
- **防呆**：三處已於同一次交付改好（`AdminSeoImageTests` 改 `CaptureSeoTextSettingsAsync`／`RestoreSeoTextSettingsAsync` 快照還原；`Standing_CSV匯入_整季替換` 記下既有列、`DeletedCount` 算進去並於 `finally` 補回；`SiteFactsTests` 改為只驗證有值）。⚠️ 這三處**未在本機重跑 `dotnet test`**（本輪限制：不得連資料庫、不得組合密碼），只確認 `dotnet build` 通過；使用者灌庫後請跑一次 `apps/api` 全套測試確認。無自動化能偵測「測試假設種子為空」——同類再犯要考慮測試基底類別統一提供快照還原工具。
- **後續（2026-09-30，使用者裁決合併資料庫）**：本機測試庫 `tcrfc_club_test` 已併入 `tcrfc_club`（開發／實走／測試同一個庫），本條「測試對共用庫寫死假設」的風險**回歸且更直接**：測試的前置快照、`finally` 還原紀律現在保護的就是使用者在後台看到的資料。新增或修改會 `DELETE`／整批替換的整合測試時，一律套用本條防呆。

### E-82 個資防線的強制排除清單以「藍鯨尚未開發」推論頁面不存在，沒有對照 `apps/web` 實際路由（2026-09-30，BW-7 前置核對發現）

- **錯在哪**：`GeoCrawlerDefaults.ClubLocalizedSegments` 只有 `tcrfc` 一列，程式碼註解與 `apps/api/README.md` 皆稱「`bw` 沒有對應頁面、待路由落地才補」。實際上藍鯨容器與磐石共用同一套 `apps/web/app/pages/zh/…`，`academy/teams.vue` 早已（S1-15）依 `NUXT_PUBLIC_CLUB` 渲染 BW-U15／BW-U12 名單，藍鯨的未成年球員照片頁沒有被 `robots.txt` 強制排除。同時磐石的 `academy/life/`（13 張學員照片）與 `programs/{childrens-training,summer-camp,specialist}/`（兒童照片牆）也沒列入，但規劃書 `GEO-02` 明文含「學院與課程的學員照片」。
- **為什麼會錯（根因）**：判斷「頁面是否存在」時用了「藍鯨官網尚未開發」這個計畫層敘述，而不是去 grep `apps/web` 的實際路由；且清單只對照 `academy/teams/` 一頁，沒有逐頁掃「哪些頁面渲染未成年人照片」。
- **下次怎麼避免**：🔴 個資防線類清單（排除路徑、肖像遮蔽）新增或核對時，一律以 `apps/web/app/pages` 實際路由與 `<img>` 掃描為依據，並寫明「逐頁核對過哪些頁」；不得以「該站尚未開發」為由留下缺口。前台若新增顯示未成年人照片的頁面，必須同一次交付補進 `ClubLocalizedSegments`。
- **防呆**：測試 `CrawlerSettings_藍鯨_未成年照片路徑必定出現在公開端點`、`GetMandatoryExcludePaths_磐石_學員與課程照片頁兩語系皆在清單` 鎖定清單；「前台新增未成年照片頁須同步」尚無自動掃描（可考慮在 `apps/web` lint 掃 `academy/`、`programs/` 下含 `<img` 的頁面比對此清單），目前為「無」自動防呆。
- **後續（2026-09-30）**：hero 背景頁的取捨已裁決為「不整頁排除、改擋圖片目錄」；測試 `GetMandatoryExcludePaths_學員照片圖片目錄_兩個俱樂部都在清單` 與 `CrawlerSettings_公開端點_學員照片圖片目錄必定出現` 鎖定 `/assets/img/academy/`、`/assets/img/programs/`。

### E-83 品牌外洩檢查只比對文字，藍鯨站整站輸出磐石照片（含未成年學員）與磐石標誌卻全程沒被抓到（2026-09-30）

- **錯在哪**：使用者指出 `academy/index.vue` hero 是磐石未成年學員照。實際盤點藍鯨站（bw 容器 SSR，`/zh/`＋`/en/` 共 146 條 200 路由）：**23 個不重複圖片來源是磐石素材，其中 7 張導覽照出現在全部 146 頁**（隱藏的 mega menu 仍在 HTML 裡，會被爬蟲與「另存圖片」取得）。另外**動態頁 `news/[slug]` 不在路由清單**，其無封面時的佔位圖是磐石標誌，單靠 SSR 掃描抓不到，是讀原始碼才找到。
- **為什麼會錯（根因）**：① 檢查機制只做「文字詞表比對」，圖片不含詞表用字（`alt=""`、檔名 `nav-about.jpg`），是**檢查範圍的缺口**，不是某頁漏改；② 每輪「藍鯨單元取捨」只判斷**頁面內容**，共用版位（導覽、hero、卡片、商品）放了誰的照片沒人逐張看；③ BW-C1 在程式註解與 README 寫下「通用足球場景照、無隊徽通用配件」，**沒有打開圖片驗證就下斷言**——實際 7 張襪子照片每張右上角都印著 TCRFC 標誌，「四大支柱」四張是磐石球員與 Trenčín 青訓球員。
- **下次怎麼避免**：🔴 判斷一張圖「通用／中性」之前**必須打開圖片看過**，不得從檔名、alt 或商品性質推論；不確定就歸磐石。新增任何 `<img>`／背景圖到兩站共用的頁面或元件，一律用 `ClubHeroBg`（頁首背景）、`ClubImg`（一般圖）或 `v-if="isTcrfc"`；藍鯨自己的素材放 `public/assets/brand/bw/`（已在允許清單）。檢查範圍與工具本身要對照「它能看見什麼」：文字檢查看不見圖片，圖片檢查看不見 API 回傳的資料，兩者的盲區要寫在檔頭。
- **防呆**：✅ `apps/web/scripts/check-club-image-leak.mjs`：白名單制（`/assets/brand/bw/` ＋ 明列中性素材，目前 0 筆），其餘一律視為磐石而失敗；每次執行先跑抽取器自我測試（樣本涵蓋所有來源型別）。⚠️ 未掛進 `npm run lint`（需先跑起 bw 容器，理由同 `E-34`）；⚠️ **盲區**：API 回傳的圖片網址（藍鯨自己的橫幅、新聞封面、球員照）與動態路由 `[slug]` 不在掃描範圍——`apps/api` 有資料時來源網域不在允許清單，會被判違規，屆時須**有意識地**把藍鯨媒體來源加進 `ALLOWED_PREFIXES`，不要放寬成通配。

### E-84 Dapper 對應 record 時，SQL 的裸 `NULL` 欄位型別被推成 int（2026-09-30）

- **錯在哪**：`Features/Calendar/CalendarRepository.cs` 的 `LoadVenueNamesAsync` 以
  `SELECT … name AS Text1, NULL AS Text2` 對應 `record I18nTextRow(Guid Id, string Locale, string? Text1, string? Text2)`。
  SQL Server 對無型別的 `NULL` 推成 `int`，Dapper 找不到 `(Guid, string, string, int)` 的建構子而丟例外，
  公開行事曆只要有「指定場地的自建活動」就回 500。
- **為什麼會錯（根因）**：共用 record 時用裸 `NULL` 補欄位，沒有標型別；而測試資料從來沒有「有場地的自建活動」，
  這條路徑沒被執行過。2026-09-30 種子補齊後台資料後，`CalendarPublicTests` 兩項才失敗。
- **下次怎麼避免**：SQL 裡補位的 `NULL` 一律 `CAST(NULL AS <與 record 相同的型別>)`；
  新增讀取路徑時，種子至少要有一筆會走到該路徑的資料。
- **防呆**：`CalendarPublicTests` 現在以種子中的場地活動覆蓋此路徑；全套 554／554 通過。

### E-88 ICS 長行折疊在最後一段讀到陣列外，只是沒有人產出過需要折疊的行（2026-09-30，S2-6 訂閱 feed）

- **錯在哪**：`IcsBuilder.FoldLine` 為了「不把多位元組 UTF-8 字元從中間切斷」，用 `while (limit > 1 && (bytes[offset + limit] & 0xC0) == 0x80)` 往回找切點。
  當這一段已經取到行尾（`offset + limit == bytes.Length`），`bytes[offset + limit]` 讀到陣列外一格，丟 `IndexOutOfRangeException`。
  任何超過 75 位元組的行（中文說明、長地點、長活動標題）折疊到最後一段都會中；訂閱 feed 端點因此回 500。
- **為什麼會錯（根因）**：S1-19 寫這個折疊函式時只用單一賽事下載驗證，那條路徑的標題與地點都在 75 位元組內，
  **「需要折疊」這個分支從來沒有被任何測試或真實輸出執行過**（`IcsBuilderTests` 五項都是短行）。
- **下次怎麼避免**：新增／改動輸出格式化函式時，測試必須涵蓋「會走到每個分支的輸入」——長行、剛好落在邊界的行、多位元組字元跨邊界；
  寫「往回找切點」這類讀取 `array[i + n]` 的迴圈，先問 `i + n` 會不會等於長度。
- **防呆**：✅ `IcsBuilderTests.長行折疊_每行不超過75位元組_且不切斷中文字_摺疊還原後與原文一致`（20／24／25／80 個中文字四組，摺疊還原後與原文逐字相同）。

### E-89 整合測試又一次把種子設定當成「不存在」而無條件刪除（2026-09-30，C1 批 F1 漫畫）

- **錯在哪**：`AdminCultureTests` 第一版的 `finally` 以 `DELETE FROM settings WHERE setting_key LIKE 'comic.%'` 清理，種子的 `comic.about_*` 設定因此被永久刪除，測試本身仍綠燈；是重灌前核對種子筆數才發現。
- **為什麼會錯（根因）**：與 `E-81` 完全同一根因——寫清理時只想「別留下自己建的」，沒想「別刪掉別人（種子）建的」；`E-81` 的防呆只是一段文字紀律，寫測試時沒有工具可用、也沒有失敗信號。
- **下次怎麼避免**：🔴 會動共用設定的測試一律呼叫 `C1Test.SnapshotSettingsAsync(prefix)`（前置快照、`finally` 還原）；新增種子類別時，同步在 `SeedBaselineTests` 風格的基線測試補一列。
- **防呆**：✅ `C1TestSupport.SnapshotSettingsAsync`；✅ `AdminC1MiscTests.種子基線_C1示範資料沒有被測試清理吃掉`（種子被吃掉時，下一次全套測試會失敗並列出缺哪一類）。**`E-81` 至此升級為機制，不再另記第三筆。**

### E-90 UTC 轉換對 `Kind=Unspecified` 的時間值當成本機時間換算（2026-09-30，C1 批 K5／F2）

- **錯在哪**：輸入的 `DateTime`（JSON 不帶 `Z`）Kind 為 `Unspecified`，直接 `ToUniversalTime()` 會依伺服器時區位移；測試時區為 UTC+8 時存入值差 8 小時。
- **為什麼會錯（根因）**：沒對照專案慣例「資料庫與 JSON 一律 UTC、不帶 `Z`，未指定 Kind 者視為 UTC」，套用了常見寫法。
- **下次怎麼避免**：Kind 為 `Local` 才 `ToUniversalTime()`，`Unspecified` 一律 `SpecifyKind(…, Utc)`；比對時間的測試用毫秒容差並比較 UTC 值。
- **防呆**：✅ 抽獎與球迷會活動的時間往返測試；⚠️ 無全域檢查，新增時間欄位端點時要人工確認。

### E-91 K5 資格漏掉「已過期但日期涵蓋快照日」，成本權限未查規格就先決定（2026-09-30，C1 批）

- **錯在哪**：① 抽獎資格只收 `active`，種子中藍鯨 M900001（狀態 `expired`、日期涵蓋）被排除；② `shop.cost.*` 先設為 sysadmin 專屬，實際 `docs/12b` §7 規定 sysadmin＋商務可見。
- **為什麼會錯（根因）**：① 以狀態欄位代替規格寫的「日期涵蓋抽獎快照日」；② 憑印象下決定，沒查已有明文的欄位級可見性表。
- **下次怎麼避免**：資格與可見性類規則先逐字查 `docs/12b`／規劃書，再寫程式與種子；測試以種子的邊界案例（已過期、跨俱樂部）覆蓋。
- **防呆**：✅ `AdminDrawsTests` 資格案例、`AdminShopCatalogTests` 成本可見性（含夥伴球隊負向案例）；②無自動化。

### E-92 把會丟 400 的驗證函式寫在 EF `Where` 的 lambda 裡，變成 500（2026-09-30，D 批 M4／M5／M1）

- **錯在哪**：`q.Where(d => d.Platform == AppInput.RequirePlatform(platform))`、`q.Where(r => r.ReportType == AdminInput.OneOf(type, …))`。EF 翻譯 lambda 時會**執行**這些呼叫（它們不依賴實體欄位，被當成參數求值），驗證失敗的 `AdminValidationException` 被包成 `InvalidOperationException`（`ApiExceptionHandler` 不認得）→ 500。壞的篩選參數本來該是 400。
- **為什麼會錯（根因）**：想把「驗證」和「條件」寫在同一行省一個區域變數；沒意識到 lambda 裡的呼叫會在查詢翻譯階段執行，而且例外會被包裝。
- **下次怎麼避免**：**驗證一律寫在組查詢之前**（`var pf = RequirePlatform(platform); q = q.Where(d => d.Platform == pf);`）。
- **防呆**：✅ `AdminAppTests`（M4 裝置篩選、M5 診斷篩選的壞參數 → 400）；⚠️ 無靜態檢查（可日後加 Roslyn 掃描「`Where` lambda 內呼叫 `Admin*Input`／`*Require*`」）。

### E-93 同一個 scoped `DbContext` 內，追蹤中的實體讓「重新讀回」拿到舊值（2026-09-30，D 批 M3）

- **錯在哪**：`PushDispatcher.DispatchAsync` 在發送完成後用 `dbContext.PushMessages.FirstAsync(...)` 讀回批次、以其 `FailedCount` 決定最終狀態。但核可流程（`AdminAppPushRepository.ApproveAsync`）在**同一個請求、同一個 scoped `DbContext`** 已經追蹤了這個批次；EF 的 identity resolution 對已追蹤的實體**不會用資料庫的新值覆蓋屬性**，讀回的是核可當下的舊值（計數 0）。發送過程中用 raw SQL 累加的計數因此讀不到，「部分送出」被算成「已發送」。
- **為什麼會錯（根因）**：raw SQL／`ExecuteUpdate` 改的是資料庫，不會更新追蹤中的實體；把「同一個 context」當成「每次都讀到最新」。
- **下次怎麼避免**：**用 raw SQL 或 `ExecuteUpdate` 累加的欄位，之後要讀就用 `AsNoTracking()`（或 `ExecuteUpdate` 收尾）**，不要用會回傳追蹤實體的查詢；服務物件可能在別的 repository 已經載入實體之後才被呼叫。
- **防呆**：✅ `AdminAppTests.M3_分眾…部分送出`（權杖失效 → `partial`、計數正確）。

### E-94 種子把數十列塞進單一 `VALUES` 成為超長行，`sqlcmd` 由標準輸入讀取時把行切斷（2026-09-30，D 批）

- **錯在哪**：`backoffice_seed.py` 的示範日聚合以 `", ".join(rows)` 產生一行約 9000 字元的 SQL；`apply-seed.sh` 用 `sqlcmd < 檔案`（標準輸入）套用，`sqlcmd` 行緩衝在 8000 字元附近把 `SELECT` 切成 `SELEC`／`T`，整個區塊語法錯誤（7 個訊息），而不是整體失敗。
- **為什麼會錯（根因）**：驗證用的是「切批後逐批用 `-i 檔案`」與「`-i /dev/stdin` 且加 `-I`」，都不是 `apply-seed.sh` 實際的呼叫方式（`-f 65001 -b`、標準輸入），所以第一次沒有重現。**驗證路徑與實際路徑不同，等於沒驗證**。
- **下次怎麼避免**：產生 SQL 時**每列一行**（長 `VALUES` 一律換行）；驗證種子一律跑 `./db/seed/apply-seed.sh` 本身並 `grep -c "^Msg"` 輸出（必須 0），不自己拼指令。
- **防呆**：✅ 已改每列一行；⚠️ 無自動檢查（可在產生器尾端加「任何單行超過 4000 字元就報錯」）。

### E-95 用腳本改檔時一律以 `utf-8-sig` 讀寫，替沒有 BOM 的檔案加了 BOM（2026-09-30，D 批）

- **錯在哪**：為了保險以 `encoding='utf-8-sig'` 讀寫 `Program.cs`、`Common/CsvUtils.cs`、`Common/PublicRateLimitPolicies.cs`，寫回時加上原本沒有的 BOM，`git diff` 顯示第一行整行變更。
- **為什麼會錯（根因）**：專案的 C# 檔有的有 BOM（EF 產生檔）、有的沒有，沒有先判斷原檔狀態就統一處理。
- **下次怎麼避免**：改檔的腳本先 `raw.startswith(b'\xef\xbb\xbf')` 記住原狀，寫回時**保持原樣**（也保持原本的 CRLF／LF）；改完一律看 `git diff --stat`，不合理的整檔變更先查再往下做。
- **防呆**：⚠️ 無（靠 `git diff --stat`）。

### E-96 `definePerson()` 沒給 `@id`，多位教練／球員被合併成一個 Person（2026-10-01，S2-7 輪補做 Person 時抓到，錯誤原先存在於 S1-12f）
- **錯在哪**：`about/our-people.vue`（S1-12f）用 `definePerson()` 對每位教練各呼叫一次，但沒給 `@id`。`nuxt-schema-org` 的 Person 預設 `@id` 是站台身分節點 `#identity`，所有人合併成**一個** Person（只剩最後一位，且被當成整站主體）。8 位教練實際只輸出 1 筆。本輪新增的一線隊球員 Person 一開始犯同一個錯。
- **為什麼會錯（根因）**：驗收只看「JSON-LD 有出現 Person」，沒有數節點數與比對 `@id`；S1-12f 當時 `apps/api` 未啟動、沒有真實多筆資料可數。
- **下次怎麼避免**：任何「一頁輸出多個同型別節點」的 `defineXxx()` 一律明確給唯一 `@id`；驗證時用假後端餵多筆資料，數輸出節點數。
- **防呆**：⚠️ 無（已修 `our-people.vue`、`first-team/index.vue`；`check-schema-batch2` 未涵蓋 Person）。

### E-97 會籍開通只靠條件式 UPDATE 做冪等，並行時索引取鎖順序死結（2026-10-01，E 批 S2-11）

- **錯在哪**：第一版 `ActivateCoreAsync` 用 `UPDATE membership_orders SET status='activated' WHERE id=@id AND status='paid'` 當搶佔，判斷「贏的才繼續、輸的回傳已開通」。單次呼叫、循序重複呼叫都正確，我也因此判斷冪等沒問題；**真正並行（12 個請求同一張訂單）時 SQL Server 回「死結犧牲者」**：輸家的 UPDATE 以 `id` 搜尋，先鎖 `PK_membership_orders`（非叢集）索引項目、再等聚集索引的列鎖（贏家持有）；贏家接著寫 `membership_payments`，外鍵檢查要對 `membership_orders` 的 PK 索引項目取 S 鎖——被輸家的 U 鎖擋住，兩邊互等。犧牲者被我的 catch-all 當成「開通失敗」：訂單標 `activation_failed`、寫 Critical 日誌、回 409，**還有一部分回 500**。
- **為什麼會錯（根因）**：把「條件式 UPDATE 是原子的」等同於「並行安全」。原子性只保證單一陳述式，**不保證同一交易內多個陳述式對多個索引的取鎖順序一致**；有外鍵的表尤其如此（外鍵檢查是額外的隱性讀取）。我用的是「會失敗才會發現」的驗證——單元式的循序測試永遠綠燈。
- **下次怎麼避免**：任何「同一筆資料被多個請求同時開通／扣款／發放」的交易，一律先用 `sp_getapplock`（`@LockOwner='Transaction'`，資源名含業務鍵）把同一筆資料的處理**排成佇列**，再做條件式 UPDATE；並且**冪等邏輯一定要有並行測試**（`Task.WhenAll` 打同一個鍵 10 次以上），不能只測重複送出。catch-all 要區分「業務衝突」與「基礎設施錯誤（死結、逾時）」，後者不得改寫成使用者看得到的業務狀態（本批保留 catch-all 是因為「已付款卻無法開通」必須留下可追蹤狀態；死結不再發生之後，它只會接到真正的資料錯誤）。
- **防呆**：✅ `MembershipActivationService` 開通交易開頭取應用程式鎖；✅ `MembershipOrderTests` 的並行測試（12 個呼叫只開通一次，付款紀錄恰一筆）；球衣登記與球迷會活動報名同樣有並行測試（`UPDLOCK` 鎖父列後再數件數／名額）。

### E-98 新增帶 JSON 本文的 `MapDelete` 漏標 `[FromBody]`，整個 API 啟動失敗（2026-10-01，E 批）

- **錯在哪**：`DELETE /api/v1/member/me` 要收 `{ password }` 或 `{ confirm }`，我照 POST 的寫法直接把 record 當 lambda 參數。ASP.NET Core minimal API 對 **DELETE／GET** 不推斷請求本文，路由建立時丟 `InvalidOperationException`，`app.Run()` 失敗——**整個主機起不來**，所有整合測試同時紅燈，不是單一端點 500。
- **為什麼會錯（根因）**：POST／PUT 可以省略 `[FromBody]` 是慣例，手感延伸到 DELETE。啟動期錯誤要靠跑到整合測試才看得到，編譯不會擋。
- **下次怎麼避免**：新增帶本文的 `MapDelete`／`MapGet` 一律明標 `[Microsoft.AspNetCore.Mvc.FromBody]`；寫完端點先跑任一支整合測試確認主機能啟動，再往下寫測試。
- **防呆**：⚠️ 無靜態檢查（啟動即失敗，測試必然抓到；本批當下就抓到，沒有進版控）。

### E-99 會員前台：登入後轉址靠會被卸載的子元件事件、元件放子目錄導致標籤解析不到（2026-10-01，S2-11／S3-2）

- **錯在哪**：① `MemberAuthPanel` 登入成功後 `emit('done')` 讓頁面轉去 `?next=`，但 `login()` 內部先翻轉 `isLoggedIn`，父層 `v-else-if` 立即卸載面板，後面的 emit 被丟掉，`?next=` 不生效（限付費活動「登入後回到活動頁」整條斷掉）。② `FanEventRegistration.vue` 放在 `components/culture/`，樣板寫 `<FanEventRegistration>`，實際自動匯入名稱是 `CultureFanEventRegistration`，SSR 輸出 `<faneventregistration>` 空元素，只有 hydration 警告。
- **為什麼會錯（根因）**：① 把「動作完成」的通知放在會因同一動作而消失的元件上；② 沒在寫完時用瀏覽器實際點過；元件目錄前綴規則在 `MembershipBenefits.vue` 檔頭早有記載，但沒回頭查。
- **下次怎麼避免**：跨狀態翻轉的副作用（轉址）放在父層對狀態的 `watch`，不靠子元件事件；新元件一律放 `components/` 根目錄或用含前綴的標籤名，寫完用 CDP 走一次流程、看 SSR 有無小寫自訂元素。
- **防呆**：⚠️ 無（可加：build 後 grep SSR 輸出是否含已知元件的小寫標籤）。

### E-100 `dotnet ef migrations add` 之後直接 `database update --no-build` 失敗（2026-10-01，F 批）

- **錯在哪**：為了省建置時間，`migrations add AlignSchemaF1` 之後立刻 `dotnet ef database update --no-build`，EF 回報「`ClubDbContext` 有待處理的模型變更，請先加 migration」。實際上 migration 檔已經產生，只是**組件裡沒有它**。
- **為什麼會錯（根因）**：`--no-build` 使用的是 `migrations add` 執行**之前**的建置輸出；新產生的 `.cs` 沒被編進去，EF 比對「模型」與「組件內的 migration 快照」就判成有差異。
- **下次怎麼避免**：`migrations add` 之後一律先 `dotnet build`，再 `database update`（兩個指令之間不加 `--no-build`，或先自己建置）；`has-pending-model-changes` 同理。
- **防呆**：⚠️ 無（錯誤訊息明確、沒有造成資料異動，只是白跑一次）。

### E-101 測試輔助函式把環境值寫死，與程式「依設定取值」的行為對不上（2026-10-01，F 批）

- **錯在哪**：商店測試要讓發票能開出來，需要收款主體名下的 `einvoice` 字軌通道；輔助函式硬寫 `environment = 'sandbox'`。但 `ShopInvoiceService` 依 S6 設定 `shop.payment_environment`（本機的設定值不是 `sandbox`——由修正後測試轉綠推得）選通道，於是測試補的通道沒被用到，發票停在 `pending`。
- **為什麼會錯（根因）**：憑「測試環境應該是 sandbox」的印象寫輔助函式，沒先看種子與設定實際值；程式碼本身則讀設定而不是常數。
- **下次怎麼避免**：測試輔助函式要模擬某個設定的效果時，先讀同一個設定值（同一個鍵），不憑印象寫死。
- **防呆**：✅ `ShopPublicTests.ChannelEnvironmentAsync` 讀 `shop.payment_environment`；整套流程測試（`訪客完整流程…`、`維護作業…`）會在字軌取錯環境時立刻失敗。


### E-102 個資遮罩只遮「長得像個資的欄位」，漏了預設值就是姓名的收據抬頭（2026-10-01，CH-3 N3）

- **錯在哪**：`CharityDonationsAdminService` 詳情把 `Donor.Name`／`Email`、身分證字號、地址、載具都遮罩了，卻把 `DonationInvoice.receipt_title` 原樣回傳。規劃書 §5.2 明文「收據抬頭預設帶入捐款人姓名」，所以預設遮罩的詳情回應仍含完整姓名。
- **為什麼會錯（根因）**：遮罩清單是**照欄位名稱的字面意思**挑的（名稱像 name／email／id 才遮），不是**沿著資料來源**挑的（「哪些欄位的內容來自捐款人輸入」）。`receipt_title` 名稱不像個資，但值是從 `donor_name` 帶入的。
- **下次怎麼避免**：新增含個資的回應時，先列「捐款人（或會員）輸入了哪些值」，再逐一確認每個值在回應裡出現的**每一個欄位**；測試不要只斷言「某個欄位被遮罩」，要斷言「**原始值在整份回應文字裡一個字都找不到**」。
- **防呆**：✅ `CharityAdminDonationsTests.詳情預設遮罩姓名與Email_身分證字號與地址也遮罩_明文需reveal且每次寫稽核`（對整份回應 JSON 做 `DoesNotContain`）。⚠️ 新增的回應欄位仍要人工想一次資料來源。

### E-103 派工單假設慈善 DbContext 與帳號體系已存在，實際沒有（2026-10-01，CH-2／CH-3 派工）

- **錯在哪**：派工單寫「目前慈善 DbContext 已有的程式」「後端專案（.NET／EF Core＋Dapper，兩個 DbContext：主站與慈善）」。`STATUS.md` 的 CH-1 只記「本機建庫已完成、29 張表與資料庫逐張一致」，`apps/api` 裡沒有任何 `CharityDbContext`、慈善實體、`CHARITY_SQL_CONNECTION_STRING` 的註冊（只有 `/readyz` 開一條探活連線）、慈善後台帳號與權限的程式。agent 從零做了 scaffold、migration 基準、獨立 JWT 方案、登入與更新權杖、授權器，並因此新增了規劃書沒寫的 `admin_refresh_tokens`。
- **為什麼會錯（根因）**：派工單是照「CH-1 完成」與「兩個資料庫」**推論**後端程式也有兩套，沒有實際 `grep DbContext`／看 `Features/` 與 `Data/` 目錄就寫進指示。
- **下次怎麼避免**：派工單裡凡是「已有／已完成」的前置，**用 `grep`／`ls` 驗過再寫**；`STATUS.md` 的 ✅ 只代表它自己那一列（建庫），不代表依賴它的程式也存在。
- **防呆**：⚠️ 無自動化。已於本次交付回報主 session，並把 `admin_refresh_tokens` 列為待裁決（`docs/16` §10）。

### E-104 並行請求測試拿「第一個回應」斷言只有贏家才會有的結果，間歇性失敗（2026-10-01，F 批＋慈善合併後完整跑）

- **錯在哪**：`ShopPublicTests.訪客完整流程…` 並行送 6 個 `confirm`，斷言 `confirms[0]`（陣列第一個，也就是第一個被送出的請求）的發票狀態是 `issued`。完整套件 984 過 1 失敗（`pending`），單獨跑 `ShopPublicTests` 連續全過，後來再跑完整套 985 全過——**同一份程式碼時過時不過**。
- **根因**：一開始懷疑共用狀態（字軌通道、`shop.payment_environment`、背景服務、慈善合併帶來的同名 `IInvoiceIssuer`），逐項查證都排除：`DisableTestParallelization` 已開、商店維護服務在非 Production 預設停用、慈善的 `IInvoiceIssuer` 是另一個命名空間的另一個型別、兩個 fixture 各自替換自己的服務。真正原因是**競態**：`ShopOrderService.ConfirmAsync` 只有「第一個把付款狀態翻成已付款」的請求（贏家）會接著開發票；其餘請求走冪等分支，看到 `paid` 就立刻回傳，**此時贏家可能還沒開完發票**，回應裡的發票就是 `pending`。陣列索引 0 是「最先送出」，不是「贏家」。負載與時序一變（跑完整套時機器較忙），輸家搶在贏家前回應的機率就上來。這是產品行為（冪等回應不等發票）合理，錯在測試把「哪個請求會贏」當成確定的。
- **下次怎麼避免**：對並行請求寫斷言時，**不要用陣列索引挑一個回應來斷言「只有贏家才有」的欄位**。要嘛斷言「至少一個回應符合」（`Assert.Contains`），要嘛等全部完成後**重讀資源的最終狀態**再斷言。看到「單獨跑過、完整跑偶爾不過」先查**競態與時序**，再查共用狀態；兩者的證據不同：共用狀態是穩定重現的，競態是間歇的。
- **防呆**：⚠️ 無自動化（無法靜態判斷「哪個索引是贏家」）。已修正該測試（`Assert.Contains` 找贏家＋全部完成後 `GET` 重讀訂單）。`CharityDonationFlowTests` 與 `MembershipOrderTests` 的同類並行測試已檢視，只斷言資料庫終態與次數，沒有這個問題。

#### 🔴 `E-44` 升級（2026-09-30，D 批派工）：**派工單要求「規劃書明確要求就照規劃書做」，等於把 `E-44` 的錯再設計進流程一次**

D 批派工單對 J3 寫「若規劃書明確要求後台可查閱稽核紀錄，就依規劃書實作…；若沒要求才只做規劃書有寫的部分」。規劃書 J3 **確實**明確要求操作稽核與登入紀錄，照這個條件式就會重建 `E-44` 已被使用者撤回的兩張日誌表。
`backend-engineer` 在動工前先查 `docs/12` §13.1 與 `E-44`，判定「客戶對範圍的指示（§13.1）與使用者 2026-09-23 裁決優先於規劃書的條文」，**沒有建表**，只做不需要表的部分（帳號活動概況與登入異常提醒），並列為待裁決。
**升級**：`E-44` 的「把規劃書某節寫進任務範圍之前先查那一節有沒有被登記成本期不做」對**條件式派工單**同樣成立——條件是「規劃書寫了就做」時，**先查 §13 的落差清單再判斷條件是否成立**。這是同一類錯第二次出現在**派工單**上（第一次是任務範圍，這次是判斷條件），依全域規定 13 不新增編號；防呆仍是「派工前掃 `docs/12` §13 與 `docs/15`」，**D 批已補機制**：`SchemaInvariantsTests`（掃 `db/club-schema.sql` 不得出現任何 `*audit*／*login*／*export*／*operation*` 日誌表，並鎖定 `ad_events`／`app_diagnostic_reports` 不得有會員、IP、定位、廣告識別碼欄位）；客戶重新確認稽核政策後，要先改 `docs/12` §13.1 再放行這支測試。

#### 🔴 `E-81`／`E-89` 再升級（2026-09-30，D 批）：測試斷言仍在假設共用庫的種子資料

D 批 `AppPublicTests` 的診斷彙總測試斷言「啟動耗時中位數 ＝ 我插入的那筆」，但種子（`db/seed/backoffice_seed.py` §59）本來就有一筆啟動耗時回報，中位數被種子拉動而失敗（跑測試當下就抓到，沒有進版控）。這是 `E-81`／`E-89` 同一類（**測試以「共用庫裡只有我的資料」為前提**）的第三次。已改為斷言「落在我的兩筆之間」。**機制**：新增種子前，`AppTest`／`C1Test` 的清理與斷言一律**先過濾出自己的資料（前綴 `ZZTEST`／`test-dev-`／`zz-test-`）或拍照還原**；聚合類斷言（中位數、總數、排名）**不得在含種子的表上直接斷言精確值**。

#### 🔵 `E-90` 升級（2026-09-30，D 批）：時間戳不帶 `Z` 的「慣例」改為全域機制

`E-90` 記的防呆是「無全域檢查」，C 批畫面回報前端只能猜 JSON 時間戳有沒有時區。D 批新增 `Common/UtcDateTimeJsonConverter`（註冊於 `JsonOptions`）：`DateTime` **輸出一律 UTC 並帶 `Z`**、輸入無時區記號視為 UTC（`Kind=Unspecified` 不會被當成本機時間換算）。防呆從「人工確認每個新欄位」變成「全域機制」。發現的直接後果：D 批自己的檔期更新驗證（拿 GET 回來的時間 PUT 回去比對）在轉換器之前會差 8 小時。

#### 🔵 `E-90` 前端段（2026-09-30，D 批畫面）：前端同一類錯在「送出」與「分格」兩端各犯一次，防呆改為 lint

後端把時間統一成「輸出 UTC 帶 `Z`、輸入無時區一律當 UTC」之後，前端回頭稽核發現：A 批以前與 B／C 批的畫面有**三種**寫法會差 8 小時——① `Date.toISOString()` 直接送出（結果剛好是對的，但只在瀏覽器位於台灣時區時）；② 日期時間輸入框的本地字串未帶時區就送；③ 月曆分格用 `startsAt.slice(0, 10)`（UTC 日期，台灣清晨的活動落在前一天）。另有 `formatDateTime`（依瀏覽器本機時區）與 `formatUtcDateTime` 兩套並存，新畫面各挑一套。
**根因（可被改掉的行為）**：時間處理沒有單一入口，每個畫面各自 `new Date(...)`／`toISOString()`／`slice`，「這樣寫在我的電腦上是對的」就過關。
**防呆（✅ 已有）**：全站時間收斂到 `apps/admin/src/utils/dateTime.ts`（一律以 UTC 解析、以 Asia/Taipei 固定 +8 顯示與輸入，不依賴瀏覽器時區）；`eslint.config.mjs` 的 `no-restricted-syntax`／`vue/no-restricted-syntax` 在 `src/**` 擋 `toISOString`、`toLocaleDateString`、`toLocaleTimeString`、`getHours`／`getMinutes`／`getSeconds`、單一參數的 `new Date(字串)`、`Date.parse`（`utils/dateTime.ts` 自身除外），`npm run lint` 會失敗。全天活動另有規則：後端以 UTC 日期計，送 `該日T00:00:00Z`（`pickerDateToAllDayUtc`）。
**仍無防呆**：日期時間輸入框沒有 `value-format` 時回傳 `Date`，本機欄位會被當成台灣牆上時鐘（`pickerDateToUtc`）——新增欄位時必須用 `utcToPickerDate`／`pickerDateToUtc` 成對使用，lint 抓不到成對性。

### E-105 Cookie 讀取用 `??` 把「已清除但仍被送來的空字串」當成有值（2026-10-01，S3-5 商店 BFF）
- **錯在哪**：`shop-session.ts` 讀 `__Host-` 與一般兩種命名的 Cookie 時用 `a ?? b`，空字串不會退回 `b`，購物車權杖被讀成空而丟失（假後端驗證時，手寫的 Cookie 容器把「Max-Age=0 的清除 Set-Cookie」當成空值回送才抓到；真實瀏覽器會丟棄過期 Cookie，所以不一定在生產出現）。
- **根因（可改掉的行為）**：把「Cookie 不存在」與「Cookie 為空字串」當同一件事卻用只處理 null 的運算子。
- **下次怎麼避免**：兩個來源擇一一律用 `||` 並在註解說明；驗證 Cookie 流程要涵蓋「清除後的殘值」。
- **防呆**：無（`server/utils/shop-session.ts` 與 `server/utils/member-session.ts` 兩處皆已改為 `||`）。

### E-106 部署文件的兩條前提互相衝突卻沒人對撞，直到要寫成 Bicep 才浮現（2026-10-01，基礎設施 IaC）
- **錯在哪**：① `docs/17` §2 規定「儲存體只允許 snet-app 的 VNet 規則」，而 §6「檔案上傳」與 `apps/api/Images/BlobImagePublicUrlResolver.cs` 回傳的是 **blob 直連網址**、期待公開讀取——兩者同時成立時，訪客瀏覽器讀不到任何公開圖片與 `documents` 下載。S0-8 與 E1a 把它記成「部署層待決」，但 §2 的網路規則從來沒回頭標示這個衝突。② 正式資料庫名稱在 `docs/16`／`17`／`20` 寫 `sqldb-club`／`sqldb-charity`，本機與 CI 用 `tcrfc_club`／`tcrfc_charity`，沒有任何一處決定「正式庫要叫什麼」。
- **根因（可改掉的行為）**：每份文件各自描述「自己那一層」（網路、上傳元件、資料庫命名），**沒有任何一步要求把它們在同一個資源上對照**；直到要把資源寫成一份可部署的模板時，才被迫同時決定。
- **下次怎麼避免**：新增或修改「某個資源的存取規則」時，grep 全 `docs/` 與 `apps/api` 找誰會**從哪裡**存取它（訪客瀏覽器、VM、後台、CI），把「誰從哪來」寫成表；命名也要有單一來源（本次定為 `tcrfc_club`／`tcrfc_charity`，記於 `docs/17` §13）。
- **防呆**：無。公開圖片配送方式仍待使用者決定（`docs/17` §13 待決 1、§7 風險 11）。
- **後續（2026-10-01）**：使用者決定「公開容器＋Cloudflare」，Bicep 已改；庫名統一為 `tcrfc_*`。**新發現的缺口**：`apps/api` 的四個公開網址解析器只用連線字串的網域，沒有「公開網址基底」設定，無法不改程式就指到 CDN 網域**已補上（2026-10-01，`backend-engineer`）**：新增 `AZURE_BLOB_PUBLIC_BASE_URL`／`AZURE_BLOB_PUBLIC_BASE_URL_CHARITY`（`apps/api/Common/PublicBlobUrl.cs`），四個解析器套用，單元測試 `PublicBlobBaseUrlTests`。防呆仍為「無」。

### E-107 公開 repo 的手冊寫進使用者的 SSH 來源 IP（2026-10-01，基礎設施 IaC）
- **錯在哪**：使用者提供的 SSH 固定來源 IP 被設計成 GitHub secret（理由正是公開 repo 的 log 人人可看），但 `infra/README.md` 的 secrets 清單把原值寫成「目前的固定來源」範例，等於把 secret 本身放進版控。commit 前主 session 對工作區 grep 該值才抓到，沒有進版控。
- **根因（可改掉的行為）**：主 session 派工時把使用者給的原值直接貼進派工單，沒有標註「此值只進 secret、不得寫入任何檔案」；agent 寫文件時順手把手上的具體值當範例。
- **下次怎麼避免**：派工單裡凡是要進 secret 的值，**不貼原值**，只寫「由使用者自行填入 GitHub secret」；commit 前對工作區 grep 使用者在對話中提供的 IP、帳號、Email 等值。
- **防呆**：無。

### E-108 交給使用者執行的設定腳本沒有當成正式產出審查（2026-10-01，基礎設施 IaC）
- **錯在哪**：把 `infra/README.md` §3 的指令直接串成腳本放在 scratchpad 交給使用者，只跑了 `bash -n`。實際問題：`set -e` 下重跑會死在已存在的 federated credential；`az role definition create … || echo "已存在"` 把權限不足等錯誤也當成「已存在」；用固定 `sleep 90` 等 RBAC 傳播；`~/.ssh` 不存在時 `ssh-keygen` 失敗；IP、密碼未驗證且 `read` 會吃掉前後空白；檔案在 session 暫存區、不進版控。使用者讀完說「感覺有問題」才回頭審。
- **根因（可改掉的行為）**：把「會對雲端與 GitHub 產生不可逆設定的腳本」當成一次性草稿，**沒有用「中途失敗後重跑」這個情境檢查每一步**，也沒跑 `shellcheck`。
- **下次怎麼避免**：交給使用者跑、會改外部狀態的腳本，一律①每步先查後建（可重跑）②不用 `|| echo` 吞錯③等待用重試迴圈④輸入要驗證⑤放進版控⑥`shellcheck -S warning` 通過才交付。
- **防呆**：部分——`infra/bootstrap.sh` 已照上述改寫；沒有 CI 掃描 `*.sh`。
- **第二次（同日）**：改寫版交付後使用者回報「沒辦法跑」——**第一行就失敗**：`az account show --query '{訂閱:name,…}'` 的 JMESPath 鍵不接受中文。`bash -n` 與 `shellcheck` 都抓不到，因為那是 `az` 執行期才解析的字串。**根因同一類：沒有實際執行過**。補上的做法：交付前把腳本中所有唯讀指令（`show`／`list`／`exists`）逐一真跑一次，並以 `echo n | bash infra/bootstrap.sh` 跑到第一個確認點。同時查到舊版已建好資源群組、身分與 federated credential，停在角色指派（推測是新身分尚未同步到 Entra，舊版只等 20 秒；新版改為重試）。
- **第三次（同日）**：使用者執行時第 98 行 `RG�: unbound variable`——**macOS 內建 bash 3.2 會把緊接在 `$VAR` 後的全形字元（如「）」）吃進變數名稱**，`set -u` 下直接中止。`bash -n`／`shellcheck` 都抓不到，前一次的「唯讀指令逐一真跑」也沒涵蓋到這行。修法：全檔變數一律寫成 `${VAR}`；並以 `az`／`gh` 假指令在 `/bin/bash`（3.2）下**把整支腳本從頭跑到尾**驗證每一條寫入路徑。**防呆升級**：給使用者跑、含中文輸出的 shell 腳本，變數一律加大括號，交付前用 `/bin/bash` 加假指令完整跑一次（寫入 [`14`](14-invariants.md) 前先看是否再犯）。
- **第四次（2026-10-01，`infra/provision-secrets.sh`，交付前自己抓到）**：假指令全程測試時，**經 ssh 送到 VM 執行的遠端腳本**（heredoc）裡又有 `"$image）"`，在 3.2 下 `image�: unbound variable`。腳本主體已逐一加大括號，卻漏了「內嵌在腳本裡、送去別台機器執行的那一段」。**這是同一類錯第四次出現，只是這次被「用 `/bin/bash` ＋假指令完整跑一次」這個機制攔下**——證明該機制有效，且要涵蓋內嵌的遠端腳本。補強檢查（交付前必跑）：`grep -nP '\$[A-Za-z_0-9]+[^\x00-\x7F]' <腳本>` 必須無輸出。

### E-109 compose 的 api 漏掛 Data Protection 金鑰環，程式又不會因此報錯（2026-10-01，基礎設施 IaC）
- **錯在哪**：`apps/api` 讀 `DATA_PROTECTION_KEYS_PATH` 才呼叫 `PersistKeysToFileSystem`；`docker-compose.yml` 的 `api` 沒有這個環境變數，也沒掛 volume。沒設不報錯，金鑰環只存在容器可寫層，容器重建（每次部署都會發生）＝所有 Data Protection 加密的資料永久無法解密。文件（`docs/14`、`docs/17` §5／§6）與 `apps/api/README.md` 都寫了「正式環境必須掛持久化 volume」，**但沒有任何一個檔案兌現**——文件寫了要求，實作沒人對照。另：`apps/api/Dockerfile` 註解寫非 root 使用者 uid 為 64198，實測 `mcr.microsoft.com/dotnet/aspnet:10.0` 的 `app` 是 1654（照舊註解建目錄會讓 api 寫不進去，且要到第一次加密才爆）。
- **根因（可改掉的行為）**：①「要求」寫在文件、「落實」在另一個檔案，沒有人逐條把文件裡的部署前置條件對照到 compose／IaC；②程式端「沒設定就靜默退回不持久化」，缺口沒有任何訊號；③映像檔內建使用者的 uid 憑印象寫進註解，沒用 `docker run --entrypoint id` 實測。
- **下次怎麼避免**：寫或改 compose／IaC 時，先 grep `docs/` 與 `apps/api/README.md` 裡所有「正式環境必須／務必」的句子，逐條對照落實位置；涉及容器內檔案權限的數字（uid／gid）一律實測，不憑記憶。
- **防呆**：部分——compose 已加 `DATA_PROTECTION_KEYS_PATH` 與 bind mount（`create_host_path: false`，目錄不存在 compose 直接報錯）；`infra/provision-secrets.sh` 建目錄並在驗證階段檢查擁有者與權限；`docs/14` 已加不變量。✅ **2026-10-01 補上**：`apps/api/Common/DataProtectionKeyRing.cs` 在 Production 缺值、空白、目錄不存在或不可寫（實際寫入並刪除探測檔）時於啟動丟例外，訊息指向 `infra/README.md` §4.3；`DataProtectionKeyRingTests` 覆蓋，以 Production 起的測試 fixture 補暫存目錄。之後有人拿掉 compose 掛載，API 會直接起不來。

### E-110 `deploy.yml` 連續六天 startup_failure 沒人追（2026-10-01，CI/CD）
- **錯在哪**：`deploy.yml` 的四個 `uses: ./.github/workflows/_node-app-deploy.yml` job 沒有 `permissions:`，而被呼叫端的 job 要 `packages: write`；呼叫端 workflow 層級只給 `contents: read`，GitHub 判定權限超出呼叫端而整支 workflow 拒絕啟動（`startup_failure`，0 個 job）。自 2026-09-25 起每次 push 都失敗，ghcr 因此從未產出任何映像檔。
- **根因（可改掉的行為）**：`actionlint` 不檢查跨檔的權限繼承，本機驗證「通過」就當作 workflow 沒問題；push 後只看自己關心的那支 workflow（例如 `infra.yml`），對其他 workflow 的紅燈視而不見——本 session 也在 2026-10-01 兩度看到 `startup_failure` 並標為「之後再處理」。
- **下次怎麼避免**：呼叫 reusable workflow 的 job 一律顯式寫 `permissions:`，且不得少於被呼叫端各 job 要的權限；push 後檢查**所有**被觸發 workflow 的結論，`startup_failure` 當天處理。
- **防呆**：無。

### E-111 json 欄位在本機／測試是 nvarchar、正式環境是原生 json，寫入純量只有正式環境會爆（2026-10-01，後端／資料庫）
- **錯在哪**：`docs/17` §6 定案正式環境用 Azure SQL 原生 `json` 型別，但本機（SQL Server 2022）與 CI／整合測試用 `deploy/local-ddl.sh` 把 12 個 json 欄位轉成 `nvarchar(max)`。原生 `json` **只收 JSON 物件或陣列**（字串、數字、`true`／`false`、`null`、空字串都被拒，`Msg 13609`），`nvarchar(max)` 什麼都收。結果：①`partner_stores.business_hours` 被寫成 `JsonSerializer.Serialize("週一至週五…")`（字串純量），正式後台儲存任何含營業時間的特約店家都會 500；②`AdminInput.OptionalJson`、課程內容、梯次 `weekly_schedule`、商品 `size_chart`、慈善說明內文等輸入驗證只檢查「是合法 JSON」，使用者貼純量會通過驗證卻在寫入時 500；③新聞內文 `articles_i18n.body` 完全不驗證，後台純文字框送出的純文字在正式環境必 500（修法：比照營業時間，純文字包成 `{"text":"…"}`、讀取還原，**不改成回 400**——那會讓客戶無法發布新聞）。**以上全部在 1,000+ 項測試全綠、本機實走也全綠的情況下存在。**
- **根因（可改掉的行為）**：①**測試環境與正式環境的欄位型別不同，卻沒有任何一次測試在正式型別上跑過**——「把 `json` 轉成 `nvarchar(max)` 只是驗證環境的限制」這句話（`docs/12` §1.4、`docs/20`）只描述了「DDL 沒寫錯」，沒有追問「型別換掉之後哪些行為不再被檢驗」；②驗證邏輯寫成「是不是合法 JSON」（語法），而原生型別的契約是「根節點是物件或陣列」（結構），兩者不同卻被當成同一件事；③「自由文字存進 json 欄位」被當成「序列化成 JSON 字串」，沒有想過原生 json 不收純量；④同一個欄位的寫入點分散在各 repository，沒有共同守門，每個作者各自決定驗到哪裡。
- **下次怎麼避免**：①任何「測試環境換掉了某個正式環境型別／行為」的差異，必須列出**哪些行為因此不再被檢驗**，並在正式型別上跑一次（本案是 SQL Server 2025 容器＋原樣 DDL）；②json 欄位的寫入一律走 `Common/JsonColumn.cs`（`IsObjectOrArray`／`WrapText`／`CoerceToObject`），**不要各自 `JsonDocument.Parse` 驗語法就算數**；③自由文字不要直接序列化成 JSON 字串，包成物件；④新增 json 欄位時，在 `JsonColumnTests` 補純量案例。
- **防呆**：✅ `JsonColumn`＋`JsonColumnTests`（2022 與 2025 行為一致）；✅ `apps/api/scripts/native-json-test.sh` 可重跑原生 json 全套測試（本輪 2025：1,046 項全綠）；✅ `docs/14` 不變量。⚠️ **CI 仍接 SQL Server 2022**，下一個新增的 json 寫入點若沒走 `JsonColumn`、又沒人手動跑原生 json 腳本，仍會重演——建議 `ci.yml` 的 `api` job 改接 `mcr.microsoft.com/mssql/server:2025-latest` 並灌原樣 `db/*.sql`（`docs/20` §5，`deployment-engineer`）。

### E-112 部署設定的預設值從未對照 CI 實際產出與執行期覆寫（2026-10-02，部署）
- **錯在哪**：① `IMAGE_TAG` 在 compose、`.env.example`、`infra/provision-secrets.sh` 三處預設 `latest`，`deploy.yml` 卻只推 `:master` 與 git SHA——從未有過 `:latest`。② `apps/web/nuxt.config.ts` 的 `blueWhaleSiteUrl` 預設寫死 `https://bw-stg.tcrfc.tw`，compose 的 `nuxt-tcrfc` 沒有用 `NUXT_PUBLIC_BLUE_WHALE_SITE_URL` 覆寫；暫用網域改為 `*.4webdemo.com` 後，主站女足頁會導往不存在的網址。
- **根因（可改掉的行為）**：各檔各自寫「合理的預設值」，沒有一步要求「把 compose 會拉的映像檔標籤對照 CI 會推的標籤」「把 `nuxt.config.ts` 的每個 `runtimeConfig.public` 網址鍵對照 compose 的 `environment:`」。因為在 VM 上從沒真的起過容器，兩者都沒有被執行期撞到。
- **下次怎麼避免**：新增 `runtimeConfig.public` 中含網址的鍵時，同一次交付在 compose 對應服務加 `NUXT_PUBLIC_*` 覆寫；改 CI 推送標籤時 grep `IMAGE_TAG`。
- **防呆**：~~無~~ ✅ 已升級，見下方「升級」段。

- **升級（2026-10-03，同一類錯第二次發生）**：正式機上線次日發現藍鯨站 `<title>` 後綴、`og:site_name`、JSON-LD `WebSite.name` 全是 `TCRFC`——`nuxt-bw` 沒帶 `NUXT_PUBLIC_SITE_NAME=台中藍鯨`（docs/13 §6 紀律 11a 早已明文要求）。與本筆第②點是同一個根因：**compose 的 `environment:` 從沒對照 `nuxt.config.ts` 裡寫死的開發預設值**；當時只補了單一個鍵，沒有把「對照」變成機制，所以換一個鍵又犯一次。**不另開新編號。**
  - **升級後的規則**：`nuxt.config.ts` 的 `runtimeConfig.public` 或 `site.name` 凡有寫死的非空字串預設值，compose 的每個對應 nuxt 服務就必須有同名的 `NUXT_PUBLIC_*` 覆寫；`nuxt-bw` 另須 `CLUB=bw`、`SITE_NAME=台中藍鯨`。
  - **防呆**：✅ `scripts/check-compose-env.mjs`（無相依；自動由 `nuxt.config.ts` 推導必帶鍵，新增鍵不必改腳本；另有明確清單與反向的「打錯字」檢查），由 `.github/workflows/ci.yml` 的 `compose-env` job 在 PR 改到 `docker-compose.yml`／兩份 `nuxt.config.ts`／腳本本身時執行。⚠️ 只擋「compose 與 config 不一致」，擋不住「VM 上 `.env` 的值錯」（例如 `BW_DOMAIN`）；預設值為空字串或布林、卻必須覆寫的新鍵，要手動加進腳本的 `REQUIRED_EXTRA`。
- **同類再犯 第三次（2026-10-03，後台 SPA）**：使用者在正式機打開 `https://tcrfc-admin.4webdemo.com` 登入，瀏覽器報「已封鎖跨來源請求…`http://127.0.0.1:5299/api/v1/admin/auth/login`」——**兩個後台都無法登入**。根因與前兩次相同：`apps/admin/src/api/http.ts`、`apps/admin-charity/src/api/http.ts` 寫死 `|| 'http://127.0.0.1:5299'` 的開發預設，compose 給的是 `VITE_API_BASE`（執行期環境變數，Vite 靜態建置讀不到）；`apps/admin/Dockerfile` 註解寫「屬於 runtime config」，但執行期注入機制**從未實作**；`apps/admin-charity/README.md` 缺口第 3 點早就寫了「Dockerfile 需改成 ARG（屬部署層，本輪未動）」，記了缺口卻沒有任何列追蹤，一個月沒人關閉。上一次升級只把範圍畫在 `nuxt.config.ts`，**沒有問「其他有寫死開發預設值的前端專案是否也有同樣問題」**——規則範圍太窄，同一類錯換一個專案又犯。**不另開新編號。**
  - **修法**：兩個後台改為執行期注入（`ADMIN_API_BASE_URL` → 容器啟動時 `docker-entrypoint.d/40-runtime-config.sh` 產生 `/tmp/config.js`，nginx 在 `/config.js` 以 `no-store` 提供，`index.html` 先載入；`src/api/runtimeConfig.ts` 解析）。**正式建置不再有 127.0.0.1 退路**：只有 `import.meta.env.DEV` 才用本機預設，否則 console.error 並在畫面顯示「未設定 API 位址」。已本機 `docker build`＋`docker run -e` 帶不同位址驗證 `/config.js` 隨之改變，並確認 production bundle 不含 `127.0.0.1:5299`。
  - **升級後的規則（擴大範圍）**：**任何前端／SPA 專案凡有「寫死的開發預設值」且正式環境靠 compose 覆寫，覆寫鍵都必須有 CI 對照**；SPA 的覆寫必須是「執行期」機制（不得依賴 `VITE_*`／`--build-arg` 烤進產物，換網域才不必重建映像檔）。新增前端專案時，把它加進 `check-compose-env.mjs`。
  - **防呆**：✅ `scripts/check-compose-env.mjs` 擴大涵蓋 `admin-web`／`admin-charity`：要求 compose 帶 `ADMIN_API_BASE_URL` 且指向 `${API_DOMAIN}`、禁止在這兩個服務設 `VITE_*`（無效）、Dockerfile 須複製 entrypoint 腳本、`nginx-spa.conf` 須有 `location = /config.js`＋`no-store`、`index.html` 須載入 `/config.js`；`ci.yml` 的 `compose-env` 觸發路徑同步加入這六個檔。⚠️ 仍擋不住「VM 上 `.env` 的 `API_DOMAIN` 值錯」；部署後以 `curl <後台網址>/config.js` 目視確認。**記了兩次還在犯，這次是機制不是記錄**；另補一條行為規則：README 或註解裡寫下「待部署層處理的缺口」時，必須同時在 `STATUS.md` 開一列追蹤，否則等於沒記。

### E-113 S0-9 把 mockup 照片搬成「建置期 import」後，從未在乾淨 checkout 建置過（2026-10-02，前端／CI）
- **錯在哪**：S0-9 搬遷時 `<img src="/assets/img/…">` 全部留成靜態字串（約 140 處、49 個檔案）。Nuxt 的 Vue 編譯器把靜態的 `src` 轉成 `import "/assets/img/…"`，建置期必須解析到 `public/` 下的檔案；而這批客戶照片刻意不納版控（`E-26`）。結果本機（有照片）永遠綠，GitHub Actions 乾淨 checkout 建置 `UNRESOLVED_IMPORT`，映像檔從未成功產出。原 README 只寫「先 rsync 照片再 build」，等於把缺口寫成手冊步驟，而不是解掉它。
- **根因（可改掉的行為）**：①「本機建置通過」被當成「CI 建置通過」，而兩者唯一的差別正是「不納版控的檔案存不存在」；②遇到「不納版控的資源是建置相依」時，選擇寫一段手動補檔說明，沒有把相依拿掉；③沒有任何機制禁止再寫出會被編譯成 import 的照片路徑。
- **下次怎麼避免**：凡新增「不納版控的素材」，同一次交付先在 `git worktree` 乾淨環境跑 `npm ci && npm run lint && npm run build` 與 `docker build`；素材一律走執行期來源（`siteImg()`），不得成為建置相依。
- **防呆**：✅ `npm run lint` 的 `lint:site-images`（`apps/web/scripts/check-site-images.mjs`）禁止任何不經 `siteImg(` 的 `/assets/img/…` 字串，並檢查 `scripts/site-images.txt` 與程式引用一致；✅ `.dockerignore` 排除 `public/assets/img`；✅ `docs/14` 不變量。⚠️ CI 目前沒有獨立的「乾淨 checkout 建置」關卡以外的檢查——`ci.yml` 本身就是乾淨 checkout，之後同類回歸會在 lint 先被攔下。

### E-114 固定 IP 與自動分配共用同一段，啟動順序決定誰撞到誰（2026-10-02，部署）
- **錯在哪**：`internal` 網路只設 `subnet`，`proxy`／`nuxt-tcrfc`／`nuxt-bw` 用 `ipv4_address` 固定 `.2`／`.3`／`.4`，其他服務自動分配也從 `.2` 開始。`proxy` 依賴其他服務健康才啟動，`.2` 早被 `redis` 拿走。
- **根因（可改掉的行為）**：設固定 IP 時沒有同時把自動分配的範圍隔開；本機開發用另一份 compose、CI 只起 api，從沒在一次 `up` 裡同時起過全部 8 個容器。
- **下次怎麼避免**：用 `ipv4_address` 就同時設 `ip_range` 把自動分配限縮到不重疊的範圍。
- **防呆**：`ip_range: 172.28.238.128/25`。

### E-115 健康檢查的期望值沒有對照網站的真實回應（2026-10-02，CD）
- **錯在哪**：`deploy/cd-deploy.sh` 對六個網址都要求 HTTP 200，但三個前台的 `/` 依語系規則一律 302 到 `/zh/`。第一次 CD 因此判定新版失敗並退回，退回又用同一檢查而失敗（結束碼 2），讓人誤以為整站壞掉。
- **根因（可改掉的行為）**：寫健康檢查時憑「首頁應該 200」的假設，沒有先對已上線的網址實際 `curl` 一次；測試用的假 `curl` 一律回 200，等於把同一個假設寫進測試。
- **下次怎麼避免**：健康檢查的期望值先對真實環境量一次再寫；假的外部指令要模擬真實回應的形狀（轉址、錯誤碼），而不是一律成功。
- **防呆**：`curl -L`；假 `curl` 對 `/` 未帶 `-L` 回 302。

### E-116 假 sqlcmd 接受了真實 sqlcmd 不支援的旗標（2026-10-02，CD／資料庫）
- **錯在哪**：`deploy/db-migrate.sh` 的 `apply` 以 `sqlcmd -b -I -f 65001 -i idempotent.sql` 執行 migration。`-f`（程式碼頁）在 Linux 的 `mssql-tools` sqlcmd 不存在，真實回應是 `Sqlcmd: 'f65001': Unknown Option`，結束碼非零。若直接上線，第一次真的套用 migration 就會在核准之後失敗。（交付前抓到，沒有影響正式環境。）
- **根因（可改掉的行為）**：旗標憑 sqlcmd 的 Windows 文件寫，沒有對「實際使用的映像檔」執行過一次；測試用的假 docker 只解析自己認得的旗標、其餘一律略過，等於把「這個旗標存在」的假設寫進測試（與 `E-115` 同一類：假的外部指令比真的寬鬆）。
- **下次怎麼避免**：新寫的外部指令呼叫（尤其是帶旗標的），先對真實工具／映像檔實際執行一次再寫進腳本；假的外部指令對**不認得的旗標要報錯**，不是略過。上線前對本機容器做一次「真實工具演練」。
- **防呆**：假 docker 遇到 `-f*` 回真實錯誤並結束碼 1；測試斷言呼叫不含 `-f`。**同一類錯第二次（假物件比真的寬鬆）→ 升級為機制**：`E-115` 與本筆都指向「假物件要模擬真實的拒絕」，下一次再犯就該把「對真實工具跑一次」放進 CI。

### E-117 壓測手冊對「升級 SKU」的連帶效果憑印象寫（2026-10-02，S0-10）
- **錯在哪**：`deploy/loadtest/README.md` 初稿 §7 寫「升級 S0 後 2 GB 硬上限同時解除」。實際上資料庫的 `maxSizeBytes` 是獨立設定（`infra/modules/sql.bicep` 寫死 2 GB），`--service-objective` 不會改它；且只改線上資源不改 Bicep，下一次 `infra.yml` 會把 SKU 與大小改回去。另一處：動手前沒先查 `monitoring.bicep`，差點把「1.5 GB 儲存告警」當成待新增項目（其實早已存在）。（交付前抓到，沒有影響任何環境。）
- **根因（可改掉的行為）**：寫操作手冊時憑「Azure 層級與容量是綁在一起的」印象下結論，沒有回頭讀現有 IaC 與 `az` 的參數說明；也沒有先 grep 任務敘述裡的資源名稱是否已存在。
- **下次怎麼避免**：寫「做了 A 之後 B 會自動變」這類連帶效果前，對照 IaC 檔與 `--help` 確認 B 是不是獨立設定；任務說「要新增某資源」，先在 `infra/` grep 是否已有。
- **防呆**：無（手冊 §7 已明寫 `--max-size` 與 Bicep 同步改，作為提醒）。
### E-118 完工時只更新 README、沒更新 STATUS 進度列，過期敘述誤導下一次派工（2026-10-02，後端）
- **錯在哪**：S1-18d（後台登入／更新權杖依 IP 限流）2026-09-29 完成並寫進 `apps/api/README.md`，但 `STATUS.md` 的 `S1-18` 列仍留著「後台登入端點尚無 IP 限流，另案處理」。2026-10-02 的 G 批交辦據此把「登入端點加限流」當成未做；實際盤點後發現 `/login`、`/refresh` 早已完成，真正的缺口是另外三支驗證密碼／驗證碼的認證端點。
- **根因（可改掉的行為）**：完工時只把細節寫進 README（`CLAUDE.md` 第 11 條要求「同一次交付內同步 `STATUS.md` 進度列」），沒回頭改該列原本那句「尚無」；交辦也沒有先對程式碼確認現況就轉述 STATUS。
- **下次怎麼避免**：改完一項功能，用該功能的關鍵字 `grep STATUS.md`，把所有「尚無／未做／另案」的殘留敘述一併改成現況；接手 STATUS 列前先 `grep` 程式碼確認「未做」是否屬實再動工。
- **同類再犯（2026-10-02，前端）**：`CH-6` 列仍標 ⬜，但導流網址讀後台 `B5` 早在 S2-9（2026-10-01）完成；交辦據此要「改成讀後台設定」，實際盤點只發現已做完。**這是 `E-118` 第二次（STATUS 與程式現況脫節），依第 13 條不另開新編號，改升級**：S2-9 這類「一個前台任務同時滿足另一條追蹤列」的情況，完工時要把被滿足的列（此處 CH-6）一併更新；要的是機制——建議在 `STATUS.md` 的列加「由哪一列滿足」的互相指標，或交辦前一律先 `grep` 程式碼確認。
- **同類再犯 第 3 次（2026-10-02，慈善 CH-4）**：`docs/16a` 寫「對帳結果無資料結構」，但 `reconciliation_runs`／`reconciliation_discrepancies` 早已在 `docs/16` §2.1b、DDL 與 EF 實體中；修缺漏的人沒回填盤點檔，派工依舊敘述下單。已在 `docs/16a` 加 §0 處理狀態表。🔴 **同一類已在同一天出現三次（STATUS S1-18、STATUS CH-6、16a），依全域規定 13 升級**：已寫進 [`00-harness.md`](00-harness.md) §2.5「收尾自檢（第三項）」，所有交付完工時以功能關鍵字 grep `STATUS.md` 與盤點檔清殘留。⚠️ 仍是流程規定不是自動檢查；再犯就要做成腳本。
- **防呆**：無（本次已更正該列；`ArchitectureTests` 現在把認證端點改成預設全擋、豁免明列，這一類「忘了掛限流」本身有了自動檢查，但 STATUS 與現況脫節仍無機制）。

### E-119 還原共用設定時漏了稽核欄位，測試帳號被外鍵卡住（2026-10-02，慈善 N7 測試）
- **錯在哪**：N7 測試透過 API 改站台設定、系統信樣板與金流憑證，這些表的 `updated_by` 外鍵指向 `admin_users`。測試自己的還原只寫回 `value`／`is_active` 等業務欄位，沒寫回 `updated_by`；fixture 清理要刪測試帳號時被 `FK_email_templates_updated_by` 擋下，之後每個慈善測試的 fixture 啟動清理都丟例外，一次 6 項失敗，且共用本機庫留下殘骸（需另寫一次性腳本把 `updated_by` 清空才能恢復）。
- **根因（可改掉的行為）**：把「還原」想成「把業務值改回去」，沒有盤點這個寫入路徑還會動哪些欄位（共通欄位 `updated_at`／`updated_by` 是每張實體表都有的副作用）；先前的測試（建立項目、店家）寫的是「依標記整列刪除」的資料，從沒遇過「修改共用既有列」這種情境。
- **下次怎麼避免**：測試要修改共用庫上的既有列時，快照要包含該表的共通欄位（`created_*`／`updated_*`），還原以快照整列為準；寫完先故意讓測試中途失敗一次，確認清理仍能完成。
- **防呆**：`CharityAdminSettingsTests.Snapshot` 與 `CharityCreditListAndImpactTests.DisposeAsync` 都會一併還原 `updated_at`／`updated_by`。

### E-120 把額度用盡的限流測試放進共用的限流測試主機（2026-10-02，慈善 CH-5 測試）
- **錯在哪**：新增「徵信名單與成果回顧依 IP 限流」測試，直接加進既有的 `CharityRateLimitTests`（額度壓到 3 的獨立主機）。測試主機的 `RemoteIpAddress` 是空的，所有請求共用同一個計數桶，我的測試把「公開讀取」額度用盡，同一個主機上既有的「結果頁輪詢也限流」測試因此在 1 分鐘視窗內失敗。
- **根因（可改掉的行為）**：記得「限流測試要獨立主機」，卻把「獨立」理解成「放在限流 fixture 裡」，沒有想到同一個主機上的不同測試共用計數桶、會互相消耗額度。
- **下次怎麼避免**：每個會把某個限流額度用盡的測試，各用自己的 fixture（主機）；寫完在整個限流測試類別連跑一次。
- **防呆**：`CharityRecognitionRateLimitApiFixture`／`CharityRecognitionRateLimitCollection` 提供獨立主機。

### E-121 依賴資料庫的測試沒跑過就交付，測試本身寫錯（2026-10-02，AP-3）
- **錯在哪**：AP-3 更新權杖鏈的 `AppDeviceSessionTests.登出全部裝置與變更密碼_兩台裝置的鏈都被撤銷`，在 worktree 寫成（沒有資料庫連線），合併後主 session 實跑穩定回 401。測試在變更密碼後先重放撤銷前的舊權杖，觸發重用偵測撤銷整條鏈，接著拿新權杖 refresh 自然失敗。程式行為符合 docs/19 §4，錯的是測試的步驟順序。
- **根因（可改掉的行為）**：把「編譯通過」當成「測試正確」交付；worktree 不含 gitignored 的 `apps/api/appsettings.Development.json`，所以依賴資料庫的測試在工作樹裡跑不起來，卻沒有要求合併後補跑。
- **下次怎麼避免**：派工到 worktree 的後端任務，回報必須分列「已實跑」與「僅編譯」的測試；主 session 合併後在主工作目錄跑完整 `dotnet test` 才算完成。
- **同類再犯（2026-10-02，H 批 5 項）→ 升級為派工規則**：**依賴 `tcrfc_club`／`tcrfc_charity` 的後端工作不派到獨立 worktree**，一律在主工作目錄執行（worktree 沒有 gitignored 的 `apps/api/appsettings.Development.json`，資料庫測試跑不起來）；需要並行時，前端進 worktree、後端留主目錄。後端 agent 交付前必須自己跑完相關測試類別與完整 `dotnet test`。
- **防呆**：⚠️ 部分（2026-10-02 H 批升級，不另記第二筆）：[`OfflineQueryTranslation`](../apps/api/Tcrfc.Api.Tests/OfflineQueryTranslation.cs)——不需資料庫，把 `ClubDbContext` 指向必定連不上的位址後執行被測 repository 方法：EF 查詢**翻譯失敗**會丟 `could not be translated`（測試紅燈），翻譯成功只會得到連線被拒的 `SqlException`（通過），並有一支工具自我驗證測試確認它真的會抓到不可翻譯的查詢。H 批新增的所有 repository（儀表板、搜尋、試訓、前後台網站設定、場地、EDM）的 EF 查詢因此在工作樹就驗過。**只證明「翻得成 SQL」**，不證明欄位名稱與資料庫一致（那是 `EfModelMatchesDatabaseTests`）、更不證明資料行為（權限過濾、計數、冪等、語系回退）——這些仍靠合併後的完整 `dotnet test`；`BeginTransaction` 之後的查詢與 `ExecuteUpdate` 也走不到翻譯階段。

### E-122 憑印象寫了不存在的 CSS 變數名稱，樣式悄悄失效（2026-10-02，慈善後台報表）

- **錯在哪**：[`ReportView.vue`](../apps/admin-charity/src/views/reports/ReportView.vue) 的趨勢條寫成
  `background: var(--charity-info)`。主題檔 `charity-admin-theme.css` 實際只定義 `--charity-info-text` 與 `--charity-info-bg`；
  `--charity-info` 這個名字只存在於 [`22-charity-ui.md`](22-charity-ui.md) §5 的「色票速查表」（那張表用簡稱）。
  瀏覽器對不存在的變數不報錯，屬性失效、背景透明，趨勢欄的橫條整欄不見。
  `vue-tsc`、eslint、`vite build`、禁用詞掃描、對比度檢查**全部通過**，是看截圖才發現「與最高的比較」欄是空白的。
- **根因（可改掉的行為）**：①變數名稱憑文件的速查表與印象寫，沒有回查實際定義它的檔案；
  ②專案裡沒有任何檢查比對「引用的變數有沒有被定義」——現有的對比度檢查只驗**已定義**token 的色值；
  ③「建置通過」被當成「樣式生效」，而樣式表的引用錯誤本來就不會讓建置失敗。
- **下次怎麼避免**：寫 `var(--…)` 前先 `grep` 主題檔確認名稱；新畫面做完一定看一次實際截圖，不能只看檢查全綠。
- **防呆**：✅ [`apps/admin-charity/scripts/check-css-vars.mjs`](../apps/admin-charity/scripts/check-css-vars.mjs)，
  掛在 `npm run lint`（`lint:css-vars`）：src 內任何 `var(--x)` 引用，`--x` 必須在 src 某處被定義（`--el-*` 元件庫變數與有備用值的寫法除外）。
  已用故意寫錯的檔案驗證會失敗。⚠️ 只涵蓋 `apps/admin-charity`；`apps/web-charity`／`apps/admin`／`apps/web` 未掛同類檢查。

### E-123 腳本改檔沒保留換行，混有 CRLF 的 `Program.cs` 被整段正規化（2026-10-02，S2-5 Google 定位）

- **錯在哪**：用 Python `open(...).read()`／`write()`（預設 universal newlines）改 `apps/api/Program.cs`，該檔 985 行裡有 20 行是 CRLF、其餘是 LF，寫回後全變 LF，diff 從 13 行膨脹成 47 行，連未觸碰的 AP-3 那一行都被改動。
- **根因（可改掉的行為）**：腳本改檔時沒先 `git diff --stat` 驗證改動規模、也沒用 `newline=''` 讀寫；「檔案可能混有 CRLF」是已知陷阱（EF scaffold 檔同類），卻沒有當成改任何既有檔前的固定動作。
- **下次怎麼避免**：腳本改既有檔一律 `open(p, encoding='utf-8', newline='')` 讀寫，新插入文字沿用該區塊原有的換行；改完立刻 `git diff --stat` 比對行數是否與預期相符。
- **防呆**：無。

### E-124 有新 migration 卻先 push 部署，正式 api 讀不到新欄位（2026-10-02，主 session）
- **錯在哪**：本批合併了主站 `AlignSchemaG1`（`articles.cover_width`／`cover_height`、`articles_i18n.cover_alt`）與慈善兩支 migration，使用者說 push 後直接推上 master；`deploy.yml` 只換映像檔、不跑 migration（docs/20 §5 刻意脫鉤），新版 api 查文章時讀不存在的欄位，`/api/v1/tcrfc/news` 回 500。
- **根因（可改掉的行為）**：把「使用者核准 push」當成「可以部署」，沒有在 push 前檢查這批是否含 migration、也沒有提醒 docs/20 §5 的順序（先 `db-migrate.yml` 再 deploy）。
- **下次怎麼避免**：push master 前一律跑 `git diff Remote_GitHub/master --stat -- apps/api/Data/Migrations apps/api/CharityPlatform/Data/Migrations`；有輸出就先向使用者說明並走 `db-migrate.yml`，套用後再 push。
- **防呆**：無。可考慮在 `deploy.yml` 加一步：比對映像內 migration 清單與正式庫 `__EFMigrationsHistory`，有未套用的就中止部署。

### E-125 儀表板只對「規格列的區塊」設計放行，沒有對每個角色的登入落點走一遍（2026-10-02，H 批 A 儀表板）

- **錯在哪**：儀表板端點的放行條件第一版是「呼叫者持有任一個區塊用到的檢視／建立權限」，清單從規劃書 §4.1 的區塊反推。翻譯人員角色（本批才有第一個可被指派的權限：字串翻譯表）不在任何區塊的權限裡，登入後第一頁（儀表板）會回 `403`——使用者進得了後台卻第一頁就看到「沒有權限」。同一版的內容區塊把「沒有新聞檢視權限」回成 `0`，畫面會顯示「本月發布 0 篇」，與「真的 0 篇」無法區分。
- **根因（可改掉的行為）**：以「規格列了哪些區塊」為單位設計權限，沒有以「哪個角色登入後會看到什麼」為單位驗證；`DTO` 數字欄位預設用不可為空的 `int`，把「無權限」與「零」混成同一個值。
- **下次怎麼避免**：做「所有角色都會進入」的頁面（首頁、儀表板、個人設定）時，先列出十個角色與各自持有的權限，逐一寫出該角色的落點結果再寫程式；依權限出現的數字欄位一律用可為空型別，`null`＝看不到、`0`＝真的是零。
- **防呆**：⚠️ 部分——[`DashboardApiTests`](../apps/api/Tcrfc.Api.Tests/DashboardApiTests.cs) 有「翻譯人員進得去儀表板、看不到其他區塊、新聞數字是 null 不是 0」與「檢視者／內容編輯／客服各自的區塊」斷言，但需要資料庫（2026-10-02 合併後已實跑通過）；離線測試 `儀表板放行權限碼集合…` 只鎖權限碼集合的組成。

### E-130 梯次狀態用英文代碼比對，中文字面值的資料永遠不命中（2026-10-02，前台盤點）

- **錯在哪**：`apps/web/app/pages/zh/programs/summer-camp/index.vue`、`winter-camp/index.vue` 以 `s.status === 'open' || s.status === 'waitlist'` 找「開放中梯次」；`sessions.status` 實際是 `開放`／`額滿`／`候補`／`已結束`（`CK_sessions_status`），API 原樣輸出。條件永遠為假，早鳥價、剩餘名額、梯次日期三格永遠「待公告」。因為種子資料沒有梯次，沒有人看過它失敗。
- **根因（可改掉的行為）**：寫狀態比對時憑「狀態通常是英文代碼」的印象，沒有查 DDL；頁面只用空資料驗證，沒有一筆真實梯次走過。
- **下次怎麼避免**：比對任何資料庫列舉值前，先 grep `db/club-schema.sql` 的 CHECK；用假後端帶一筆真實形狀的資料跑一次頁面。
- **防呆**：✅ `app/utils/program-session.ts` 集中判斷（中文字面值、報名窗口、已結束），`scripts/check-news-body.mjs` 釘住「英文 `open` 不是合法狀態」。

### E-131 BFF 代理吃掉上游 4xx 的錯誤訊息（2026-10-02，課程報名 P3）

- **錯在哪**：`server/api/backend/[...path].ts` 的 POST 分支直接 `return await $fetch(...)`。上游回 400 時 `$fetch` 丟出的是 `FetchError`，不是 h3 錯誤；Nitro 正式環境對未處理例外只保留狀態碼、`message` 改成 `Server Error`、`data` 移除。`useFormSubmit.extractErrorMessage` 因此永遠讀不到後端的中文訊息，七張表單、FAQ 回饋、提案下載、課程報名的失敗畫面只會出現通用文案。
- **根因（可改掉的行為）**：驗證代理時只測成功路徑與 405；開發模式（`nuxt dev`）會回完整錯誤，正式建置不會，所以本機看起來「有訊息」。
- **下次怎麼避免**：新增或修改代理路徑時，用 `npm run build` 後的 `node .output/server/index.mjs` 對假後端送一次上游 400，確認瀏覽器收到的 `message` 是後端那句；不要只看 dev。
- **防呆**：無（已在代理內對 4xx 以 `createError({ statusCode, message })` 重丟、5xx 一律 502；沒有自動測試，因為需要起 Nitro 與假後端）。

- **升級（2026-10-02，H 批，同一類錯第二次發生）**：只修了 POST 分支，**GET 分支仍原樣丟出 `$fetch` 的錯誤**，全站搜尋 `GET search` 的 400（「英文關鍵字至少需要 2 個字元」）訊息因此在瀏覽器看不到，驗證腳本抓到。現在 POST／GET 共用 `clientErrorFrom(err)`（上游 4xx → `createError` 保留狀態碼與訊息；5xx 的 GET 維持原樣丟出、POST 一律 502）。**規則：BFF 代理新增或調整任何分支時，兩個方法都要用假後端各回一次 4xx，確認瀏覽器收到訊息。**

### E-132 只用到系列清單卻拉整份商店資訊，把「代收」字串帶進藍鯨頁面 payload（2026-10-02，官方商品頁）

- **錯在哪**：`culture/merchandise/index.vue` 改接 API 時呼叫 `useShopInfo()` 取系列清單，整份 `ShopInfo`（含 `collectingSubjectName` ＝「台中磐石足球俱樂部」）被 Nuxt 序列化進頁面 payload，藍鯨站該頁 HTML 出現「磐石」。頁面可見文字沒有，但 `check-club-brand-leak.mjs` 掃整份 HTML，命中 2 頁（zh／en）。
- **根因（可改掉的行為）**：把「畫面沒顯示」等同於「沒外洩」；忘了 `useFetch`／`useAsyncData` 的回應整份進 payload。
- **下次怎麼避免**：藍鯨會渲染的頁面只取需要的欄位（`useAsyncData` 內整理好再回傳），要共用整包商店資訊的頁面只限已在例外清單的 `/shop/`、`/checkout/`；改完跑 `check-club-brand-leak.mjs`。
- **防呆**：✅ `scripts/check-club-brand-leak.mjs`（未掛 `npm run lint`，需先把藍鯨站跑起來，E-34）。

- **升級（2026-10-03，真 API 實機驗收，同一類錯第二次發生）**：這次不是商店資訊，而是**把「當前站台」寫死成磐石、或兩邊各抓一份**：`join/contact/index.vue` 固定 `useSiteFacts('tcrfc')`、`academy/overview.vue` 為了同時取兩邊的梯隊代碼對 `tcrfc` 與 `bw` 各呼叫一次、`useFaqCategories` 把兩俱樂部共用的分類主檔整份（含藍鯨已關閉的「學院招生」）放進 payload。藍鯨站的 `__NUXT_DATA__` 因此帶著磐石場地、地址、梯隊代碼與已關閉單元的分類名稱，而頁面可見文字全部正確，所以 `check-club-brand-leak.mjs` 在**沒有對 bw 實際跑過**的那一輪完全沒被觸發。另外腳本的 `/en/checkout/`、`/en/shop/` 例外只列了中文詞「磐石」，漏掉英文名「Taichung Rock」，en 版長期是紅的。**規則：藍鯨會渲染的頁面，`useSiteFacts`／分類／清單類 composable 一律只依 `config.public.club` 取資料，且在 `transform` 內就濾掉本站不用的項目；要「兩邊各取一份」的頁面只限本來就 404 的單元（`/womens/`、`/charity/`、`/academy/join/`）。** 防呆：`check-club-brand-leak.mjs` 例外清單對同一實體的中英文寫法視為同一筆（`TERM_ALIASES`），補 en 例外不再被棘輪誤判為「新增例外」；交付任何改動 bw 可達頁面的變更前，必須對 bw 容器實跑這支腳本。

### E-140 後台 5xx 錯誤漏帶 `body`，依 `code` 分流的分支永遠不成立（2026-10-02，H 批場地管理）

- **錯在哪**：`apps/admin/src/api/http.ts` 的 `classifyByStatus` 最後一行 `new AdminApiError('server', …, { status, detail })` 沒帶 `body`（只有行事曆衝突那條帶了）。`PartnerStoreEditView.vue` 的 `(error.body as { code?: string })?.code === 'geocoder_unavailable'` 因此恆為 `undefined`，後端回 503＋`geocoder_unavailable`（暫時故障，可再試）時被當成「尚未啟用」，「由地址定位」按鈕被永久停用。
- **根因（可改掉的行為）**：驗收「分流邏輯」時只看到程式碼裡有這個分支，沒有讓假後端實際回兩種 503 各走一次。
- **下次怎麼避免**：畫面依錯誤內容分流時，假後端每一種分流結果都回一次；`AdminApiError` 的每條建構路徑都帶 `body`。
- **防呆**：無（已修 `http.ts`；場地編輯頁用同一個寫法，本次實走三態通過）。

### E-142 在凍結的 `tcrfc.css` 容器內新增表單控制項，被容器層級的元素選擇器撐壞（2026-10-02，H 批頁尾電子報）

- **錯在哪**：頁尾電子報區新增「同意」勾選框，`tcrfc.css` 的 `.newsletter input{ width:100%; min-height:46px; padding:0 1rem }` 把勾選框撐成整欄寬的方塊，旁邊的說明文字被擠成一字一行（lint、typecheck、build 全綠，看截圖才發現）。
- **根因（可改掉的行為）**：往既有區塊加新元素前，沒有先 grep 凍結樣式表裡該區塊底下有沒有 `input`／`button`／`p` 這類元素選擇器。
- **下次怎麼避免**：在 `tcrfc.css`（不得修改）管轄的區塊內加控制項前先 `grep -n "\.區塊名" public/assets/css/tcrfc.css`，用更高特異度的選擇器在元件內覆寫；新增元素後一定看一次截圖。
- **防呆**：無。

### E-143 VM 操作步驟引用已退役的 `~/tcrfc-src`（2026-10-03，正式機重設密碼）

- **錯在哪**：要使用者在 VM 上執行 `reset-password` 時，步驟寫 `cd ~/tcrfc-src && git pull`。`docs/20` §4a 早已定案「部署目錄＝runner 的 checkout `/opt/tcrfc/actions-runner/_work/tcrfc/tcrfc`，`~/tcrfc-src` 退役」，使用者照做得到 `fatal: not a git repository`。`infra/README.md` §4.8 的兩段指令與 `docs/20` §5 種子匯入段也仍寫 `~/tcrfc-src`，主 session 與子 agent 都是照抄這兩處。
- **根因（可改掉的行為）**：寫「在 VM 上執行」的指令時，只抄最近一份文件裡的指令，沒有對照「部署目錄」的單一定案處（`docs/20` §4a）。退役決定寫進 `docs/20` 時，沒有 grep 全 repo 把其他引用一起改掉。
- **下次怎麼避免**：給出任何 VM 上的 `cd` 路徑前，先 `grep -rn "tcrfc-src\|_work/tcrfc" docs infra deploy` 確認現行路徑；在部署目錄裡只做唯讀操作（不 `pull`／`checkout`／`compose up`）。退役某路徑或名稱時，同一次交付 grep 全 repo 清掉舊引用。
- **防呆**：無（已把 `infra/README.md`、`docs/20` 的舊路徑改為 runner checkout 目錄）。

### E-144 慈善獨立後台漏做「帳號與角色管理」（2026-10-03，CH-3）

- **錯在哪**：慈善規劃書第 38–39 行與 §10 明定獨立後台有「完整的後台帳號權限表」與獨立帳號體系，`docs/16` §2.3 也列了 `AdminUser`／`AdminRole`／`AdminUserRole`／`RolePermission`，資料表、Entity、種子（連註解都寫了「讓後台『帳號與角色』畫面有多筆可看」）全部到位，但 CH-3 只做了登入、2FA、改密碼與 N1–N7，**沒有任何帳號／角色管理的 API 與畫面**。正式庫上線後只有一位系統管理員，無法開帳號給協會人員，使用者實際登入才發現。
- **根因（可改掉的行為）**：派工與驗收以「`N` 模組代號清單」為範圍，規劃書寫在模組表以外的機制功能（帳號權限、系統設定）沒有被拆成工作列；驗收時沒有回頭用「規劃書 §1 的後台範圍敘述」逐項比對。
- **下次怎麼避免**：一個後台的驗收清單＝模組表 ＋ 規劃書明文的「另含」項目（帳號權限、系統設定、稽核）；開工前把這些拆成 `STATUS.md` 的獨立列。新建一個後台時，先比對主站後台的路由清單，主站有而它沒有的，逐一確認規劃書是否排除。
- **防呆**：無（已補 API 與畫面）。

### E-145 FAQPage JSON-LD 在真資料下整站消失，驗證腳本把「0 個 FAQPage」當成正確行為放行（2026-10-03，真 API 實機驗收）

- **錯在哪**：`useFaqPageSchema()` 用 `watchEffect(() => { 題目為空就 return; useSchemaOrg(...) })`。呼叫端的 `useFetch` 沒有 await，setup 當下題目還是空陣列 → 第一次執行直接 return；資料到達後 watchEffect 重跑，但那時已不在 setup 的同步階段，`useHead` 沒有作用中的實體，輸出悄悄消失。頁面渲染 10 題，JSON-LD 只剩 WebSite／WebPage，沒有任何錯誤訊息。`check-faq-schema-live.mjs` 同時有兩個盲點：「找不到 FAQPage 一律通過（GEO-05 資料不足的正確行為）」，以及「題目應內嵌在 `mainEntity`」的假設與實際輸出（`mainEntity` 是 `@id` 引用，題目是 `@graph` 的獨立節點）不符——後者因為 S1-18a 時 `faqs` 是 0 筆從未被驗證過。
- **根因（可改掉的行為）**：把「0 筆資料時的輸出」當成驗收完成，沒有在有資料的情況下實跑過一次；檢查腳本的「通過」條件沒有區分「資料不足所以不輸出」與「有資料卻沒輸出」。另外在 `watchEffect` 內呼叫 `useHead` 系列 composable（需要作用中的 Nuxt 實體）是反模式。
- **下次怎麼避免**：結構化資料／head 輸出一律在 setup 同步階段呼叫一次，輸入給 getter（unhead 在 SSR 輸出時才解析，client 隨資料重算）；不得在 `watch`／`watchEffect` 內呼叫 `useHead`／`useSchemaOrg`。任何「資料為空時不輸出」的規則，驗證必須同時覆蓋空與非空兩種資料。檢查腳本的「通過」不得建立在「沒找到東西」上，要有一個獨立的證據（例如頁面實際渲染的題數）證明「確實沒東西可輸出」。
- **防呆**：✅ `scripts/check-faq-schema-live.mjs`：頁面渲染了 `<details id="q-…">` 題目卻 0 個 FAQPage 節點會 fail；`mainEntity` 的 `@id` 引用會解回 `@graph` 節點再驗 `name`／`acceptedAnswer.text`。

### E-148 K2 開通的會員選擇器搜不到現場建立的會員（2026-10-03，後台實機驗收 B-1）

- **錯在哪**：K1 現場建立的會員尚無任何會籍；K2「手動開通／續會」的選擇器重用 `GET /members`，該端點預設只列「在目前俱樂部有會籍」的會員，所以搜會員編號永遠是空的。`crossClub=true` 也救不了（單一俱樂部資料範圍的角色只擴到授權俱樂部，且無會籍帳號只有系統管理員才看得到）。
- **根因（可改掉的行為）**：把名單端點的範圍規則當成選擇器的規則重用，驗收只驗「不外洩他隊會員」，沒有走「建立會員→開通」這個最先發生的串接情境。
- **下次怎麼避免**：把列表端點當選擇器用時，先寫「上一步剛建立、尚未歸屬的資料找得到嗎」的測試；範圍規則改動要同時回答新建資料的可見性。
- **防呆**：✅ 新增 `includeNoMembership=true`（本俱樂部有會籍者＋任何俱樂部都沒有會籍者；全表判斷，不洩漏只在他隊有會籍者），`AdminMembersTests.名單_includeNoMembership…` 鎖定。

### E-149 刪除新聞草稿撞抽獎公布稿外鍵回 500（2026-10-03，後台實機驗收 B-9）

- **錯在哪**：K5 產生公布稿草稿後，`member_draws.announcement_article_id`（不 cascade）指向該文章；從最新消息刪除這篇草稿時 `DeleteAsync` 沒處理引用，SQL 外鍵違反變成 500。
- **根因（可改掉的行為）**：新增外鍵時只想「活動寫入文章」那條路，沒有回頭盤點「文章的刪除路徑會被誰參照」。
- **下次怎麼避免**：新增指向既有表且不 cascade 的外鍵時，同一次 grep 該表所有刪除路徑（含批次刪除），補「被引用→409 或先解除關聯」與測試。
- **防呆**：✅ 活動已公布／已結案→409 並指出活動代碼，其餘狀態先解除關聯再刪；`AdminDrawsTests.刪除公布稿文章…` 鎖定。沒有自動掃描「外鍵×刪除路徑」的機制。

### E-150 後台代填未比照前台驗證（2026-10-03，後台實機驗收 B-3）

- **錯在哪**：P4 後台代填只驗姓名與狀態，沒有「電話與 Email 至少一項」「Email／電話格式」「未滿 18 歲須填家長」；P3 後台 Email 也不檢查。壞值直接寫進同一張 `registrations`。
- **根因（可改掉的行為）**：同一張表有三個寫入入口（前台、P3、P4），驗證各寫各的；後台「人為判斷不擋名額」的註解被擴大成「不需要驗證」。
- **下次怎麼避免**：同一張表新增寫入入口前，先逐項對照既有入口的驗證清單；驗證寫成共用函式（`AdminInput.OptionalPhone`／`OptionalEmail`）。名額可以不擋，資料格式不行。
- **防呆**：✅ P4 補齊與前台同一套（舊報名本來就沒有聯絡方式者修改時不強制「至少一項」）；`AdminTrialsAndRegistrationsTests.試訓報名_後台代填驗證…`、P3 壞信箱／壞電話測試。

### E-152 新聞封面「預設有圖、白名單沒圖」，沒封面的新文章請求不存在的本地圖回 404（2026-10-05，前台實機驗收 N1）

- **錯在哪**：`apps/web/app/utils/news.ts` 的 `hasNewsCover` 對磐石站除了 `NEWS_NO_COVER_SLUGS` 之外一律回 true，API 回 `coverUrl: null` 的新文章（如後台新建的抽獎公布稿）會去載 `/assets/img/news/{slug}.jpg`，404 破圖。
- **根因（可改掉的行為）**：mockup 搬遷時用「83 篇種子資料逐一比對」得出唯一例外，把一次性盤點結果寫成永久的反向規則；後台開始產生新文章後，「不在例外清單」不再代表「有本地圖」。
- **下次怎麼避免**：「是否存在某資源」的判斷用**肯定清單**（有才用），不要用否定清單（沒有才排除）；新增資料來源（後台建稿）時回頭檢視既有的預設值假設。
- **防呆**：✅ 白名單 `NEWS_LOCAL_COVER_SLUGS` 取自已納版控的 `scripts/site-images.txt`，`check-site-images.mjs` 已守它與引用一致；乾淨 checkout 同樣有清單，不依賴不納版控的照片目錄。無自動化測試（`apps/web` 目前無單元測試框架）。

### E-151 CSV 匯出時間是無標示的 UTC（2026-10-03，後台實機驗收 B-5）

- **錯在哪**：P3、P4、G3、表單詢問、Lead 的 CSV 各自 `CreatedAt.ToString("yyyy-MM-dd HH:mm")`，輸出 UTC 且沒有任何標示，使用者當成台灣時間讀，差 8 小時；K1／K3 的「註冊日期／登記日期」也是 UTC 日期，近午夜會差一天。
- **根因（可改掉的行為）**：「資料庫存 UTC」是共識，但輸出給人看的時間文字沒有共用函式，每個匯出自己格式化；K5、S3 剛好各自做對了，沒有被提升成共用規則。
- **下次怎麼避免**：輸出時間文字一律走 `TaiwanClock.ToText`（日期用 `TaiwanClock.ToDate`），表頭標「（台灣時間）」。
- **防呆**：✅ `TimestampFormatTests` 掃原始碼，`yyyy-MM-dd HH:mm` 格式化必須同行含 `AddHours(8)`／`Add(Offset)` 或明寫 `UTC`。

### E-146 K2 手動開通的會員搜尋沒帶 `crossClub`，K1 現場建立、尚無會籍的會員永遠搜不到（2026-10-03，後台實機驗收）

- **錯在哪**：`MemberPicker.vue` 呼叫 `searchMembers(activeClubId, k)`，後端名單預設只列「在目前俱樂部有會籍」的會員；現場建立（K1）後還沒有任何會籍的人，正是 K2「手動開通／續會」最需要找到的對象，卻回「沒有符合的會員」。
- **根因（可改掉的行為）**：把「挑會員」當成「會員名單的縮小版」直接重用名單端點，沒有回頭問「這個挑選器要找的人，是否包含還沒有會籍的人」；後端的資料範圍規則（`AdminMembersRepository.ResolveVisibilityAsync`）只讀了端點簽名、沒讀語意。
- **下次怎麼避免**：重用既有端點做挑選器時，逐一列出「要找得到的對象」（例如尚無會籍、已到期、他隊會籍）並各用一筆真資料搜一次。
- **防呆**：無（已改帶 `crossClub=true`）。⚠️ 注意 `crossClub` 只有系統管理員會包含「完全沒有會籍」的帳號，其他角色仍只含自己授權俱樂部的會籍持有人，後端另行補參數。

### E-147 技術詞與「本輪沒做」的開發備註外露到後台畫面——同一類錯第三次（2026-10-03，後台實機驗收）

- **錯在哪**：① 場地欄位畫面上寫「場地選單目前沒有可用清單（`venues` 主檔尚無對應後台端點），暫不開放選擇」（含程式反引號）——端點早已存在（I5／F2 都在用），過期的開發備註留在使用者介面上；同時 `venueId` 一律送 `null`，編輯既有梯次會把已設定的場地清掉。② M5／M2／推播表單出現 `ads_enabled`、`payment_mode（off／external／inapp）`、`schedule_d1`、`my_orders`、`tcrfc://`、「第 90 百分位」「API 錯誤」；上課時間表要求使用者手寫 JSON。
- **根因（可改掉的行為）**：`check-forbidden-terms.mjs` 只掃 `<template>`，而這些字串寫在 `<script>` 的錯誤訊息、placeholder 常值與後端回傳的標籤裡；E-33 只補了元件庫語系，沒有把「script 內會顯示的字串」納入。端點補齊後，沒有回頭搜「因為缺端點而停用」的註解與提示字樣（同 E-118 的過期敘述類）。
- **下次怎麼避免**：補齊某個「先前缺的端點」時，`grep -rn "尚無對應\|暫不開放\|本輪不" apps/admin/src` 清掉所有過期提示；使用者需要輸入的內部識別字，改成下拉、自動產生，或標明「由 App 工程團隊提供」。
- **防呆**：✅ `apps/admin/scripts/check-forbidden-terms.mjs` 新增第二層（掛 `npm run lint`）：掃 `<script>` 內含中文的字串常值與樣板文字，抓反引號、snake_case 內部代號、`tcrfc://`、JSON、百分位。⚠️ 後端回傳的標籤與訊息（例如診斷類型、連線檢查、抽獎公告標題、「貨號」）腳本看不到，已逐項回報後端；前端以 key 對照白話名稱的做法只是補丁。
- **第四次再犯（2026-10-05，第二輪重驗）**：憑證列管 `KINDS` 前端對照表蓋掉後端 `kindLabel`（文字還與後端不一致），連線檢查前端寫死「Cloudflare 邊緣節點」，推播下拉選項附 `（tcrfc://…）`。已改為優先用後端標籤、前端表只當退路，並去掉技術詞。仍屬「後端回傳標籤腳本看不到」，防呆缺口不變。
- **同類再犯（2026-10-05，第二輪重驗，後端）**：K5 公布稿標題 E-147 時只修「名稱同時以【開頭且以】結尾」，「【測試】主場賽事日球迷抽獎」這種只有前綴括號的名稱仍變成「【【測試】…】中獎名單公布」。根因：只針對當時看到的那一個樣本寫條件，沒窮舉「無括號／前綴括號／整段括號／中間括號」。已改為「名稱內含任何【或】就不外包」，抽成 `AdminDrawsRepository.BuildAnnouncementTitle`，防呆 ✅ `ApiBoundaryBehaviorTests.公布稿標題_名稱含括號就不再外包括號`（四種情境）。後端產生的使用者可見文字，修條件前先列出輸入的所有形狀再寫測試。

### E-153 後台把 503 的白話 `detail` 吞成通用錯誤（2026-10-05，後台第二輪重驗）

- **錯在哪**：`http.ts` 的 `classifyByStatus` 只對 400／401／403／404／409 取 `detail`，其餘（含 503）一律「伺服器發生未預期的錯誤」。後端對「服務尚未啟用」刻意回 `{"title":"服務尚未啟用","detail":"檔案儲存尚未設定"}`，所有上傳點都只看到通用訊息。
- **根因（可改掉的行為）**：把 5xx 整類視為內部錯誤而不外露，沒有區分「後端刻意給使用者看的 503」與「未預期的 500」；E-140 修 5xx 漏帶 `body` 時只補欄位，沒有回頭檢查同一函式的 `message`。
- **下次怎麼避免**：改錯誤分流時，對每個狀態碼逐一問「後端有沒有寫給使用者看的 detail」，並用真實 503 路徑（未設定的服務）實走一次。
- **防呆**：無。500 維持通用訊息，避免外露內部錯誤。

### E-155 刪除端點缺必填 query 回 500、跨來源讀不到下載檔名與 Retry-After（2026-10-05，後台第二輪重驗）

- **錯在哪**：① `DELETE /news/{id}`、`/pages/{id}` 把 `expectedUpdatedAt` 宣告成非可空 `DateTime`（必填 query），缺值時最小 API 在綁定階段丟 `BadHttpRequestException`，`ApiExceptionHandler` 沒有這個分支，落入通用 500。② CORS 政策沒 `WithExposedHeaders`，跨來源的瀏覽器讀不到 `Content-Disposition`（後台下載永遠用前端自組的退路檔名）。③ 限流 429 沒帶 `Retry-After`，前台 BFF 準備好轉傳也無從轉。
- **根因（可改掉的行為）**：① 以為「參數沒給」會自然變 400，沒實測過框架的綁定例外在自訂全域例外處理下的狀態碼；② 寫 CORS 只想到「放行來源與憑證」，沒盤點前端要讀的回應標頭；③ 限流只設 `RejectionStatusCode`，沒補 `OnRejected`。
- **下次怎麼避免**：新增端點的必填 query／本文，要實打一次「缺值」「格式錯」確認是 400 中文；前端會讀的回應標頭（下載檔名、等待秒數）要同時出現在 CORS expose 清單與測試。
- **防呆**：✅ 全域 `BadHttpRequestException`→400（`ApiExceptionHandler`）；兩個刪除端點改 `DateTime?`＋`ConcurrencyInput.RequireExpectedUpdatedAt`（日常中文訊息）；`ArchitectureTests.端點handler不得宣告必填的DateTime或DateOnly_query參數` 掃描全部 Map 端點；`ApiBoundaryBehaviorTests`（缺值／格式錯／CORS expose）與 `AdminAuthRateLimitingTests`（429 帶 Retry-After）。

### E-156 「上傳未驗」只當成缺環境變數，漏掉本機容器預設私有（2026-10-05，上傳端到端收尾）

- **錯在哪**：STATUS 與驗收紀錄把 S2-1／S2-2／S2-3／S3-1／S3-3／AP-1「上傳未驗」歸因於本機未設 `AZURE_BLOB_CONNECTION_STRING`，接法只想到「補一個連線字串」。實際即使指到 Azurite，`BlobImageStorageService` 自建容器是 `PublicAccessType.None`（正式環境的匿名 blob 讀取由 Bicep 設定），圖片網址在瀏覽器一律 403，後台縮圖與前台圖片都破圖；而且此前 `apps/api/README.md` 多處引用「本機開發：Azurite」一節，該節標題其實不存在。
- **根因（可改掉的行為）**：只看「服務有沒有接上」，沒走到「瀏覽器真的讀得到結果」這一步；引用文件章節名稱時沒回頭確認章節存在。
- **下次怎麼避免**：替身（Azurite、假金流）上線前，用匿名客戶端實際讀一次產出物的公開網址；文件互相引用章節名稱時 grep 標題確認存在。
- **防呆**：✅ `db/seed/seed-dev-blobs.py` 預建公開容器（拒絕非本機端點）；`apps/api/scripts/dev-azurite.sh`；`launchSettings.json` 的 `http-azurite`；`ImageUploadGeneralRuleAcrossModulesTests` 直接從 Azurite 取回物件逐項斷言 §4.0。⚠️ 「匿名可讀」本身沒有自動測試（整合測試走 SDK 帶金鑰，不經匿名路徑），靠 README 步驟。

### E-157 改了種子沒重產正式庫內容種子（2026-10-05，上傳端到端收尾）

- **錯在哪**：先前修正 B-14（廣告曝光種子不一致）改了 `backoffice_seed.py`，卻沒重產 `db/prod/club-content-seed.sql`；`python3 db/seed/generate-prod-content-sql.py --check` 因此回「不一致」（`ad_campaigns.delivered_total` 與 `ad_daily_stats` 數字）。本輪新增種子區段 60 時才發現並一併重產。
- **根因（可改掉的行為）**：`db/seed/README.md` 寫了「改了原產生器後執行重新產生並一併提交」，但改種子時沒有跑 `--check` 的固定動作，產生器的輸出與已提交檔案可以悄悄分叉。
- **下次怎麼避免**：改 `generate-club-seed-sql.py`／`backoffice_seed.py` 後固定跑 `generate-prod-content-sql.py --check` 與 `generate-prod-reference-sql.py --check`，不一致就重產一併提交。
- **防呆**：✅ `.github/workflows/ci.yml` 的 `prod-seed` job（2026-10-05）：PR 動到種子或 `db/prod/` 時，以 GitHub-hosted runner 跑 `generate-prod-content-sql.py --check` 與 `generate-prod-reference-sql.py --check`，不一致即失敗；不用 secrets、不需資料庫。見 [`docs/20`](20-cicd.md) §3。

### E-158 共用常數含俱樂部詞彙、純文字檔不在品牌殘留檢查範圍（2026-10-05，BW-7 藍鯨 GEO 驗收）

- **錯在哪**：`llms.txt` 的預設「代表頁面」清單直接輸出 `SITE_UNITS[].labelZh`，其中 02／04／08 三個單元名稱本身含磐石詞彙（「關於台中磐石」「足球學院」「台中磐石文化」），藍鯨站的 AI 爬蟲檔因此混入磐石事實，且是中英兩份都有；`llms-en.txt` 另把藍鯨英文名寫死成 `Taichung Blue Whale`，等於替客戶選了三種寫法中的一種（B-5、docs/13 紀律 11）。
- **根因（可改掉的行為）**：①把 `SITE_UNITS` 當「兩站共用的中性清單」，沒檢查欄位值是否含俱樂部詞彙（它原本只服務 sitemap，只用 `path`，`labelZh` 是後來 `llms.txt` 才開始用）；②品牌殘留檢查的路由清單是從頁面檔案樹算出來的，**不在檔案樹裡的輸出（`llms.txt`、`robots.txt`、`sitemap.xml`）全在檢查之外**。
- **下次怎麼避免**：共用常數被新消費者使用時，逐欄檢查值是否含俱樂部專屬詞彙；新增任何「不是頁面」的對外輸出（純文字、XML、JSON-LD 以外的端點），同時加進品牌殘留檢查。
- **防呆**：✅ `getUnitLabelZh(unit, club)`；`check-club-brand-leak.mjs` 掃 `/llms.txt`、`/llms-en.txt`（`robots.txt`／`sitemap.xml` 目前只有路徑沒有名稱，未加）。✅ B-5 英文名：`club-copy.ts` 新增 `BW_NAME_EN_PENDING`（目前中文名，定案後只改一處），`scripts/check-bw-en-name.mjs`（掛 `npm run lint`）掃出任何 `Taichung Blue Whale／Bluewhale` 寫法即失敗；規則記於 `docs/14` 藍鯨名稱段。

### E-159 只驗了「資料不足不輸出」，沒驗「資料齊全會輸出」（2026-10-05，BW-8 藍鯨 Schema 驗收）

- **錯在哪**：`useOrganizationSchema()` 的 `watchEffect` 在 `useFetch` 尚未回來時第一次執行，資料到手後才呼叫 `useSchemaOrg`，已脫離元件注入脈絡；改成 `useSchemaOrg(computed)` 也沒用，nuxt-schema-org 在 server 端對 ref 只求值一次。結果 `schemaEligible=true` 時首頁與關於頁**完全沒有** Organization 節點。
- **根因（可改掉的行為）**：真資料的 `schemaEligible` 恆為 false，驗收時只看到「沒輸出」就當作 GEO-05 正確，**沒有讓資料變成合格再看一次**；兩個分支只驗了一個。`SportsTeam` 用 `useHead(() => …)` 是對的，同檔兩個 composable 寫法不一致當時沒被當成警訊。
- **下次怎麼避免**：任何「條件式輸出」的功能，驗收要兩個分支都走（用假 API 或測資讓條件成立）；同檔兩個做同一件事的函式寫法不同時，先弄懂為什麼。
- **防呆**：⚠️ 無自動檢查（需要可回 `schemaEligible=true` 的假 API）。已改為 `useHead(() => …)`，並於 `apps/web/README.md` 留下 fixture 驗證步驟。

### E-160 擴充種子漏掃「由資料推導的數量」斷言（2026-10-05，上傳端到端收尾）

- **錯在哪**：補 S2-11 球衣登記驗收資料時新增兩位「有效球迷會員」，只搜尋了測試裡的會員編號與會員總數斷言，沒想到抽獎合格名單的定義就是「有效球迷會籍」，`AdminDrawsTests` 把「合格人數＝2」「roster 序號 1、2」寫死；全套測試 3 項失敗（1327／1330）。
- **根因（可改掉的行為）**：盤點「會被新種子影響的斷言」時只依「我新增的欄位值」去 grep，沒有依「新資料會被哪些**查詢條件**納入」反向盤點（球迷會員 → 抽獎名單、報表、公開統計）。
- **下次怎麼避免**：擴充共用種子前，先列出新列符合的所有業務條件（tier、status、期間），再 grep 這些條件對應的查詢與測試；跑全套而不是只跑相關檔案。
- **防呆**：⚠️ 無自動防呆。本輪處置：`AdminDrawsTests` 以常數 `DevAcceptanceEligible` 明示「種子合格名單含驗收會員」，`DevAcceptanceSeedTests` 唯讀守門種子存在。已有的做法見 `patterns_seed_expansion_safety`（先派唯讀 QA 盤點基線假設）——這次沒做，下次擴種子前照做。


### E-161 並行 agent 各自開 docs/18 新號而撞號（2026-10-05，主 session 派工）

- **錯在哪**：同時派 backend-engineer 與 frontend-architect，兩者都「讀最新號＋1」開了 E-158；後端那筆事後改為 E-160（`STATUS.md` 第 9 節引用一併改）。
- **根因（可改掉的行為）**：派並行任務時只切分了**程式目錄**，沒有切分**共用的流水號資源**（`docs/18` 編號），讓各 agent 依「讀檔當下的最大號」自行取號。
- **下次怎麼避免**：並行派工、且可能寫 `docs/18` 時，在每個 prompt 預先分配不重疊的號段（例如 A 用 E-170–E-174、B 用 E-175–E-179），或要求以 `E-TBD-<agent>` 佔位、由主 session 合併時編號。合併前一律 `grep -c "^| E-NNN"` 檢查重號。
- **再犯（2026-10-05，同日）**：派工時分配了 `docs/18` 號段，卻沒分配 `docs/19` §11 的小節號；四個 agent 各自「取下一個字母」，出現兩個 §11e、兩個 §11i。主 session 改為 §11e-1（Android Phase B，與其 §11e-2／§11e-3 成系列）與 §11j（後端第五批），並逐一修正 STATUS、apps/api/README 與 docs/19 內的引用。**規則升級：並行派工時，所有「流水號資源」（錯誤編號、文件小節號、migration 名稱）都要在 prompt 裡預先分配，或要求以 `TBD-<agent>` 佔位由主 session 合併。**
- **防呆**：⚠️ 無自動防呆。

### E-162 xcconfig 設了 `PRODUCT_NAME`，專案層級設定同時套到測試 target 而產生重複輸出（2026-10-05，iOS AP-2）

- **錯在哪**：iOS 專案 `Config/*.xcconfig` 寫了 `PRODUCT_NAME = TcrfcApp`，xcconfig 是專案層級，連單元測試 target 也被改名成 `TcrfcApp`，`xcodebuild test` 報 `Multiple commands produce …TcrfcApp.swiftmodule`。
- **根因（可改掉的行為）**：把「只屬於某個 target 的值」放進專案層級的 xcconfig；target 名稱本來就等於產品名，根本不必設。
- **下次怎麼避免**：xcconfig 只放環境差異（API 網址、bundle id 以外的建置條件）；target 專屬設定寫在 `project.yml` 該 target 的 `settings`。新增建置設定後立刻跑一次 `xcodebuild test`。
- **防呆**：無（建置當下即報錯，當日修正）。

### E-163 Regex 在 JVM 單元測試通過、Android 執行期閃退（2026-10-05，Android AP-2）

- **錯在哪**：`DeepLinkParser.webFallback` 用 `Regex("\\{(\\w+)}")`，JVM 的 `java.util.regex` 容許未跳脫的 `}`，Android 的 ICU 實作不容許；單元測試 69 項全綠，模擬器上一進主畫面就 `PatternSyntaxException`。
- **根因（可改掉的行為）**：只用 JVM 單元測試當「能跑」的證據，沒有在模擬器走過主流程；兩個平台的正規式方言不同。
- **下次怎麼避免**：寫 Regex 時 `{`、`}` 一律成對跳脫；Android 的每個畫面交付前至少在模擬器走一遍（引導→各分頁→一條深連結）。
- **防呆**：✅ 2026-10-05 升級成機制（同類第三次：Android E-163、iOS E-186／E-189 皆為「單元測試全綠、打開畫面才壞」）：`app/src/androidTest/.../SmokeTest.kt` 以 dev flavor 預覽資料在模擬器走過引導三步、五個分頁、賽事詳情、新聞全文、會員卡（QR 非空白）、課程、店家、設定、抽獎資訊與 Intent 深連結，一條指令 `./gradlew connectedDevDebugAndroidTest`（README「冒煙測試」）；CI（AP-7）必須跑這組。


### E-185 自行推導俱樂部簡稱，違反名稱規則（2026-10-05，iOS AP-2／AP-3）

- **錯在哪**：`ClubDisplay.shortName` 以剝除「台中」與「足球俱樂部」等前後綴推導簡稱，球隊分頁、新聞來源、卡片都顯示「磐石」「藍鯨」。主站規劃書 §0 第 2 點規定中文簡稱一律寫「台中磐石」，**不得單獨用「磐石」**（`docs/14` 名稱寫法）。
- **根因（可改掉的行為）**：遇到「DTO 沒有簡稱欄位」時自行發明推導規則，而沒有先掃 `docs/14` 的名稱寫法與規劃書 §0；回報裡把它當成「契約缺口」列出，卻已經先照自己的寫法上線。
- **下次怎麼避免**：任何對外顯示的名稱，**資料沒給就顯示全名**，缺欄位列進回報等後端補；動手前掃 `docs/14` 名稱寫法。
- **防呆**：有（`ClubDisplayTests.testClubNamesAreNeverAbbreviatedByTheApp`）；Android 端建議同樣加一條。

### E-186 QR Code 畫成空白、單元測試只驗「非 nil」（2026-10-05，iOS AP-3）

- **錯在哪**：`QRImage.make` 回傳 `UIImage(ciImage:)`，在 SwiftUI `Image(uiImage:)` 裡不繪出，會員卡 QR 區是一塊白色方框；`testQRImageGenerates` 只 `XCTAssertNotNil`，全綠。
- **根因（可改掉的行為）**：產生影像類輸出時只驗「有東西回來」，沒驗「內容真的畫得出來」；而且交付前沒有先在模擬器看過那個畫面（截圖才發現）。
- **下次怎麼避免**：影像／圖形類功能的測試要檢查**像素**（含深淺兩種值）；每個新畫面交付前至少截一次圖看過。
- **防呆**：有——單元測試 `testQRImageIsBitmapBackedNotBlank`，以及 **XCUITest 冒煙測試**（`./scripts/smoke.sh`）在真的畫面截圖斷言 QR 非空白（2026-10-05 升級為機制，已用改回舊寫法實測會失敗）。

### E-187 網路層對有副作用的 POST 也自動重試，續期可能被判為重用（2026-10-05，iOS AP-3）

- **錯在哪**：`APIClient.send` 對所有請求在 5xx／斷網時重試 3 次，包含 `POST /member/auth/refresh`、重產 QR token、加入俱樂部、註冊、改密碼、刪除帳號。更新權杖每次使用即輪替，回應遺失後用舊權杖重試會被後端判為重用、撤銷整條鏈（`docs/19` §4 已知取捨）。
- **根因（可改掉的行為）**：把 AP-2 為「讀取端點」寫的重試規則原樣沿用到會員寫入端點，沒有重讀 `docs/19` §3 對「付款特例」的判準與 §4 的取捨；是看到 Android 的決定才發現。
- **下次怎麼避免**：新增任何 POST／DELETE 端點前先問「重送會不會重複副作用或觸發重用偵測」；重試預設只給冪等方法，需要的另行明示。
- **防呆**：有（`APIRequest.retryable` 預設值＋`testStateChangingPostsAreNeverRetried`）。

### E-170 migration 清理語句是無效 T-SQL，錯誤被 EXEC 與批次吞掉（2026-10-05，第四批 `AddClubShortNameGuardianConsent`）

- **錯在哪**：`partner_stores` 座標約束的前置清理用了 `([lat] IS NULL) <> ([lng] IS NULL)`（SQL Server 沒有布林型別可比較）。語句在 `EXEC(N'…')` 內，編譯錯誤只讓該語句失敗、不中止批次，同一個 `BEGIN…END` 的下一句 `ADD CONSTRAINT` 照常成功——結果約束加上了、清理沒做。若庫裡真有半邊座標或 (0,0)，約束會因既有資料違反而整句失敗；本機庫碰巧沒有這類資料才沒出事。
- **根因（可改掉的行為）**：migration 的 SQL 只寫、沒有在任何資料庫上執行過就交付；把多句 DDL／DML 包在 `EXEC` 與 `IF…BEGIN…END` 裡，讓語法錯誤變成「印一行就繼續」。
- **下次怎麼避免**：寫完 migration 先用 sqlcmd 在開發庫真的跑一次並**看完整輸出**（`-b` 不夠，`EXEC` 內的錯誤不會觸發）；條件改用 `(a IS NULL AND b IS NOT NULL) OR (a IS NOT NULL AND b IS NULL)`；有清理再加約束的 migration，兩步分開成兩個 `migrationBuilder.Sql`，讓任一步失敗都不會被後一步掩蓋。
- **防呆**：✅ **`MigrationsOnBlankDatabaseTests`（2026-10-05 第五批補上，掛在既有 `dotnet test`，CI 的拋棄式 SQL Server 容器也會跑）**：建一個拋棄式資料庫（`tcrfc_migprobe_<guid>`，只刪自己建的）→ 套用最新 `db/club-schema.sql`（json→nvarchar(max)）→ 把冪等契約起點（`AlignIndexesWithDdl2`）之前的 migration 標為已套用 → 套用其餘全部 → **回滾（每支 `Down` 執行）→ 再套用（每支 `Up` 本體真的執行）→ 清歷史冪等重跑**。ADO.NET 下批次內任何 T-SQL 錯誤（含 `EXEC` 內）都會丟 `SqlException`。已實測：把壞語句放回去，測試紅燈。此測試同時逮到本次 migration 的 `Down` 兩個問題（DDL 建的庫上值域 CHECK 是匿名、`EXEC(N'…' + QUOTENAME())` 語法不合法）。歷史 migration（起點之前）是 EF 直接產生的非冪等 `CreateTable`／`AddColumn`，不在契約內；新 migration 時間戳必晚於起點，自動落入契約。


### E-188 改檔的腳本先截斷檔案，測試無聲少了一整檔（2026-10-05，iOS AP-4）

- **錯在哪**：批次修改腳本裡有一行多餘的 `open(p,'w').write(open(p).read())`（想「存回去」），Python 先以寫入模式開檔（清空）再讀，`CoreLogicTests.swift` 變成空檔。`xcodebuild test` 仍然 TEST SUCCEEDED（空檔沒有失敗的測試），只有「執行測試數」從預期的約 160 變成 139。
- **根因（可改掉的行為）**：只看「全綠」不看「測試數量」；腳本裡寫了沒有目的的讀寫。
- **下次怎麼避免**：改完測試先 `git diff --stat` 檢查有沒有檔案整份被清空；全套測試跑完要核對**測試數**不低於上次。腳本不寫「原檔讀回寫回」的無用操作。
- **防呆**：無（本輪從 git 還原並補回；建議 CI 對測試數設下限）。

### E-164 轉述 agent 的執行層決定前沒核對規劃書（2026-10-05，主 session 派工）

- **錯在哪**：Android 回報「開賽提醒預設關閉」，主 session 直接列為「執行層決定」請 iOS 對齊；App 規劃書 §6.2 的通知類型表明定賽事提醒預設「開」。iOS 依規劃書實作並回報不一致，才改請 Android 對齊規劃書。
- **根因（可改掉的行為）**：把 agent 自定的值一律當成「規劃書沒寫的執行層決定」轉述，沒有先查規劃書對應章節是否已有規定。
- **下次怎麼避免**：轉述任何「請另一端對齊」的決定前，逐項用 `grep` 查規劃書是否已有規定（預設值、上限、時間點特別容易有）；有規定就以規劃書為準，沒有才當執行層決定並寫進 docs/19。
- **再犯（2026-10-05，同日）**：轉述後端回報的「離線曝光帶原始發生時間回傳」給兩端時，沒核對 App 規劃書 §2.4 硬規則 1「離線時不得計算廣告曝光」，Android 實作後依規劃書提出疑義才更正。**升級**：該條規則寫進 `docs/14` 廣告版位條下；主 session 轉述「請另一端照做」的指示前，對指示中每個帶數值或預設值、或涉及個資／計費／計數的項目，先 `grep` 規劃書原文並在指示裡附上章節號，沒有出處的才標為執行層決定。
- **防呆**：⚠️ 無。

### E-171 課程詳情對有夥伴或有日期的課程 500（2026-10-05，第五批）

- **錯在哪**：`ProgramsRepository` 的課程夥伴查詢把 `Name` 排在 SQL 最後，但 `PartnerRow` positional record 的建構子是 `(Id, Slug, Name, LogoDarkKey, LogoLightKey, WebsiteUrl)`，Dapper 逐一對位失敗（`A parameterless default constructor or one matching signature … is required`）；修好後又露出 `SessionRow` 把 `date` 欄位宣告成 `DateOnly?`（`E-20`），有日期的梯次一樣失敗。兩者都是既有缺陷，只有「有掛夥伴／有日期」的課程會觸發，既有測試只讀了不含這些資料的課程。
- **根因（可改掉的行為）**：Dapper positional record 的規則（欄位順序、`date`→`DateTime`）只寫在 `E-20` 與少數檔頭註解，沒有靠型別或測試強制；公開讀取端點的測試只抽樣、沒有「把種子裡每一筆都讀一遍」。**同一類錯第二次**：`E-20` 已升級，這次補的是覆蓋面。
- **下次怎麼避免**：新增或修改 Dapper positional record 時，SQL 欄位別名順序抄建構子參數順序；`date` 一律 `DateTime?`；公開詳情端點的測試要把種子裡**每一筆**都讀一遍（含有關聯資料的）。
- **防呆**：✅ 逐一讀全部課程詳情的測試。⚠️ 其他 repository 的 positional record 沒有同樣的全量測試——有需要再逐個補。

### E-172 會籍訂單同一冪等鍵並行回 409 open_order_exists（2026-10-05）

- **錯在哪**：`MembershipOrderService.CreateAsync` 的流程是「查冪等鍵 → 其他檢查 → 查同方案未完成訂單 → 插入」。同一鍵並行時，慢的請求通過第一步（當時沒有訂單），但在第三步看到快的請求剛插入的 `created` 訂單，就丟 `open_order_exists`（409）。冪等語意要求同一鍵一律回第一次的結果（201／200）。唯一鍵撞號後的 `catch` 只涵蓋「插入時才撞」，沒涵蓋「檢查時就看到對方」。
- **根因（可改掉的行為）**：全套 `dotnet test` 偶發失敗一次，因單獨重跑與全套重跑都過就判「與本批無關、未處理」；原測試只有 6 並行×1 輪，機率低到抓不到。**偶發失敗的並行測試應先視為產品競態，用迴圈加大並行度重現，重現不了才能談測試不穩。**
- **下次怎麼避免**：冪等路徑上，任何「發現已有東西存在就拒絕」的檢查，拒絕之前都要先確認那個東西是不是**同一把冪等鍵**造成的，是就回原結果。偶發紅燈一律記錄並用迴圈重現，不放著。
- **防呆**：✅ `MembershipOrderTests.冪等鍵_高強度並行…`。✅ 已把同樣壓測套到其餘冪等／防重複端點（見下方「再犯與升級」）。


### E-189 預覽用假 API 的紀錄陣列沒有鎖，並行呼叫閃退（2026-10-05，iOS AP-4）

- **錯在哪**：`FixtureContentAPI.calls` 是普通陣列，首頁四個廣告版位各自 `Task` 並行呼叫 `ad(...)`，同時 `append` 造成記憶體損毀，App 啟動即閃退。
- **根因（可改掉的行為）**：把假實作當成「只在單執行緒測試用」而沒有考慮它也被 Debug 建置的真畫面並行使用；測試全是序列呼叫，抓不到。
- **下次怎麼避免**：被 UI 並行使用的假實作與共用紀錄一律加鎖；新增並行載入的畫面後，**先在模擬器真的打開那個畫面**（E-186 同一教訓：看過才算交付）。
- **防呆**：有——鎖、並行呼叫單元測試，以及 **XCUITest 冒煙測試**（`./scripts/smoke.sh`，首頁廣告版位並行載入情境；AP-7 CI 要跑）。
- **再犯與升級（同日，全端點壓測）**：盤點所有冪等／防重複的建立端點並各加「16 並行 × 多輪」測試。結果：① **試訓報名 `TrialsRepository.SubmitRegistrationAsync` 是同類真競態**——同一人並行重複送出，「查重複 → 插入」同時通過，產生多筆報名並重複扣名額（修正前紅燈）；修法比照活動報名，交易一開始先 `UPDLOCK` 鎖場次列使同一場報名串行化。② 商店結帳：查冪等鍵與讀購物車之間理論上有空窗（快的請求清空購物車 → 慢的回 409 `cart_empty`），**80 輪未重現**，仍加固（`cart_empty` 前再查一次冪等鍵）。③ 慈善捐款（單號由冪等鍵推導＋唯一鍵）、活動報名（已 `UPDLOCK` 活動列）、加入俱樂部（唯一鍵＋catch 讀現有）壓測皆無競態。④ 課程梯次報名與表單送出本來就沒有「重複擋下」語意，不適用。**升級**：不抽共用輔助（各處的「拒絕檢查」領域不同，抽象不出一個安全的共用形狀），改以 `docs/14` 的規則與各端點的壓測測試當機制。

### E-190 冷啟動深連結在導覽圖建立前就導覽而閃退（2026-10-05，Android AP-5 冒煙測試）

- **錯在哪**：`MainShell` 的 `LaunchedEffect` 一收到佇列中的深連結就 `nav.navigate(...)`／`nav.graph`，冷啟動時它可能比 `NavHost` 設好導覽圖還早執行，丟 `IllegalStateException: You must call setGraph() before calling getGraph()`——被深連結（含日後的 App Links、推播點擊）叫起的 App 直接閃退。
- **根因（可改掉的行為）**：只在「App 已經開著再點深連結」的情境手動驗過，沒有走「冷啟動帶著深連結進來」；單元測試覆蓋的是解析器，不是導覽時序。同一類（執行環境才會壞）E-163 已記過一次。
- **下次怎麼避免**：任何「收到外部事件就導覽」的程式，都要用冷啟動情境（`ActivityScenario.launch(intent)`）驗；導覽前先等導覽圖就緒（`nav.currentBackStackEntryFlow.first()`）。
- **防呆**：✅ `SmokeTest` 深連結 Intent 案例（冷啟動帶連結）；README「冒煙測試」；CI（AP-7）要跑。

### E-205 首頁輪播指示器不跟著換張（2026-10-05，官網 `pages/zh/index.vue`）

- **錯在哪**：dots 的 `is-active` 在樣板裡是 `i === 0`、`aria-selected` 同，輪播狀態用普通變數 `current` 驅動 DOM class；換張（自動、箭頭、點 dot）後 slide 變了、粉紅指示器仍停第一顆。另外沒有觸控滑動（規格期待的拖曳／滑動不存在）。
- **根因（可改掉的行為）**：把靜態 mockup 的命令式輪播搬進 Vue 時，只搬了「操作 DOM class」的程式，樣板裡原本靜態的初始狀態沒有改成響應式綁定；驗收只看自動播放的圖有沒有動，沒對照指示器。
- **下次怎麼避免**：搬移有「目前狀態」的元件，樣板中凡是寫死索引／初始值的 class 與 aria 都要綁到響應式狀態；驗收用瀏覽器逐路徑比對「畫面與指示器」。
- **修正**：新增響應式 `activeIndex`，`goTo()`／`swapInstant()` 統一同步；加觸控滑動（水平 ≥50px 且大於垂直位移，掛在 hero 區塊）。
- **防呆**：無（靜態檢查會誤報，成本不划算）；本次以無頭 Chrome（CDP）對假 API 實測：自動播放、箭頭、點指示器、觸控左右滑，slide 與 dot 皆同步。
- **同批小修**：頁尾語系切換（`<button>`）字級與基線和左邊政策連結（`<a>`）不同，看起來偏低偏大；`SiteFooter.vue` 補 `font:inherit`、`.legal-links` 垂直置中，量測四個寬度／語系下三者 top／height 一致。這項是 `tcrfc.css` 不可改的限制下，在元件樣式補的對齊。


### E-210 英文用詞對照表與 B-5 牴觸（2026-10-05，主站英文版）

- **錯在哪**：docs/06 §1.1 初版把「台中藍鯨」英文列為 `Taichung Blue Whale`（並於 §1 既有列也是），而 `docs/14` 已明文「程式碼不得寫死藍鯨英文名，英文句內用 `BW_NAME_EN_PENDING`」。翻譯 agent 依表在 `club-copy-en-core.ts`／`club-copy-en-club.ts`／`MemberMemberships.vue` 寫死 14 處，`check-bw-en-name.mjs` 紅燈。
- **根因（可改掉的行為）**：寫對照表時只查規劃書與 docs/06，沒有掃 `docs/14` 的名稱寫法與 B-5；而且我把一個「待客戶確認」的詞填成具體寫法，違背自己派工單上「不自創」的規則。
- **下次怎麼避免**：新建任何名稱對照表前，先 grep `docs/14`、`STATUS.md` 的 B-* 待確認項；**待確認的名稱欄位只能寫「待確認」＋暫代做法**，不得填看似正式的值。
- **防呆**：✅ `apps/web/scripts/check-bw-en-name.mjs`。已改 14 處為 `BW_NAME_EN_PENDING`，docs/06 兩列已更正。

### E-211 英文版分派漏列跨頁共用元件（2026-10-05）

- **錯在哪**：派七個群組時以頁面目錄切分，`TrialSchedule.vue`（club 與 acad 皆列）、`FanEventRegistration.vue`（club 與 biz 皆列）被重複指派；acad 的腳本因舊字串不在而整批中止，沒有互相覆蓋純屬運氣。
- **根因（可改掉的行為）**：切分只看 `app/pages/` 目錄，沒有先 grep 元件被哪些頁面使用就分組。
- **下次怎麼避免**：派多個 agent 改同一批檔案前，先列「檔案 → 唯一擁有者」表，共用元件單獨指定一人；agent 動手前先 `git diff` 該檔。
- **防呆**：無。

### E-212 種子補英文使 API 測試前提失效（2026-10-05）

- **錯在哪**：`LocalizationFallbackTests`（2）、`AppContractGapsTests`、`BusinessPublicEndpointsTests` 各一條，斷言 tcrfc 種子「沒有英文列」所以回退為中文；種子補 en 後這些測試會紅（與 E-160 同形）。
- **根因（可改掉的行為）**：測試拿種子的缺口當回退行為的測資，而不是自己建立無英文列的資料；改種子 i18n 前沒有 grep `lang=en` 測試。
- **下次怎麼避免**：改種子的 `*_i18n` 前先 `grep -rn "lang=en\|isFallbackLocale" apps/api/Tcrfc.Api.Tests`；回退行為的測試自建測資。
- **修正**：四條測試改為自建「沒有英文列」的資料（`finally` 清理），完整 `dotnet test` 1397 條全綠。
- **防呆**：無（建議歸併到「種子擴充漏掃推導式斷言」同類：種子層級前提不得寫進測試斷言）。

### E-213 後端英文欄位缺值回退中文，前台直接取用就混語（2026-10-05）

- **錯在哪**：`RequestLocale.Pick` 在 en 缺值時回繁中，`site-facts?lang=en` 等回應因此帶中文；前台若直接取用，英文頁、JSON-LD、`llms-en.txt` 混入中文且完全沒有錯誤。
- **根因（可改掉的行為）**：把「欄位有值」當成「欄位是英文」。
- **下次怎麼避免**：凡取用後端英文欄位，一律過濾含中日文字元者後再退到靜態英文快照或 null；英文版驗收用 `check-en-pages.mjs` 看渲染結果，不是看程式。
- **防呆**：✅ `useSiteFacts.pickEn`、`useSchemaOrgClub.englishOnly`、`llms-en.txt.ts enOnly`；✅ `apps/web/scripts/check-en-pages.mjs`（實機掃描宣告 `enReady` 的 /en/ 頁，需起前台，不掛 lint）。

### E-214 並行批次字串替換的三種翻車（2026-10-05）

- **錯在哪**：① 多個 agent 共用同一 scratchpad，`rep.py` 被別人覆寫（`F` 未定義）；② 替換腳本中途失敗，已存檔的檔再跑一次會重複套用；③ 產生 JS 字串時英文撇號未轉義（`'Children's ...'`）。
- **根因（可改掉的行為）**：批次替換腳本放共用位置、沒有「全部斷言通過才寫檔」與冪等保護、未轉義產出字串。
- **下次怎麼避免**：腳本放自己的子目錄；先全部斷言再寫檔，替換結果加「已套用就跳過」；產出含引號的字串後 grep 抽查。
- **防呆**：無（語法錯有 eslint／build，重複套用沒有）。
