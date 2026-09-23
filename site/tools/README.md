# `site/tools/` — 前台搬遷 Nuxt 前的前置工具（S0-9d）

三支工具是 **S0-9（前台骨架改 Nuxt 4 SSR）開搬前的安全網**：先確認 80 頁的原始 HTML 對 Vue
編譯器是良構的、把站台根路徑 token 機械轉成 Nuxt 用得到的絕對路徑、再用一支回歸關卡
逐頁比對搬過去之後有沒有走樣。三支都是零相依或極少相依的 Node ESM，`site/` 有自己的
`package.json`（只鎖了 `parse5` 一個套件）。

```bash
cd site
npm install        # 只會裝 parse5，其餘兩支工具零相依
```

---

## ① `check-wellformed.mjs` — HTML 良構性掃描

**先跑這支。** 瀏覽器的 HTML5 解析器對不良巢狀（孤立結束標籤、標籤未閉合、巢狀錯誤）會依規格
「默默修好」，畫面完全正常；Vue 的 SFC 模板編譯器不會——SSR 會把內容跳脫送出、client 端直接報
`code 64` 把標籤丟掉。2026-09-20 三頁切片實測就踩到 `zh/index.html` 尾端孤立的 `</main>`，這支
工具就是把那次「靠人眼看」的檢查自動化。

```bash
node tools/check-wellformed.mjs              # 掃 site/dist，人看得懂的報告
node tools/check-wellformed.mjs --json       # 機器可讀，給 CI 用
node tools/check-wellformed.mjs <其他目錄>    # 掃指定目錄而非 site/dist（測試用）
```

**查三類問題**（實作細節與正規化規則寫在檔頭註解，這裡只列結論）：

1. **標籤未閉合／巢狀錯誤**——DOM 樹裡任何元素沒有對應的結束標籤位置（排除 void 元素與
   SVG／MathML 的自閉合寫法 `<path ... />`）。
2. **孤立／多餘的結束標籤**——原始文字裡有字面上的 `</tag>`，但解析器並沒有真的採用它去關閉
   任何元素（`zh/index.html` 那個孤立 `</main>` 就是這樣被抓到的，已收進 `docs/18` E-14 當範例）。
3. **`<template>` 內不得出現的 `<style>`／`<script>`（紀律 9，`docs/13` §6）**——找出
   `<main id="main">` 內的 `<style>`／`<script>` 子孫節點，逐頁列出，並在報告最後印**全站去重**後
   的清單（`<main>` 之外，例如 `shell.html` 自己的 `<script src=".../site.js">`，屬於未來
   layout／app.vue 範圍，只在「共用區塊」小節提示，不算違規、不影響結束碼）。

⚠️ **實作上的關鍵陷阱**（已寫進 `docs/18` E-14）：parse5 的 `onParseError` **不會**回報「孤立結束
標籤」「標籤未閉合」這類問題——HTML5 規格把它們定義為有明文規範的錯誤復原行為，不算 parse
error。本工具改用「DOM 樹上有沒有 `endTag` 位置」＋「原始文字的字面結束標籤數量 vs 樹上真的
被採用的範圍」兩個自製訊號取代，並且：
- `parse5.parse` 要帶 `scriptingEnabled: false`——預設 `true` 時規格會把 `<noscript>` 內容當純文字
  （瀏覽器有開 JS 時的行為），會把 `<noscript><p><a>...</a></p></noscript>` 誤判成一堆孤立標籤，
  但 Vue 編譯器沒有這個特例。
- SVG／MathML 是 foreign content，`<path d="..."/>` 這種自閉合寫法是真的自我關閉，不是漏寫。

**2026-09-21 對 `site/dist` 實測結果**：0 個標籤未閉合、0 個孤立結束標籤（`zh/index.html` 那處
歷史問題仍然修復有效，沒有回歸）。`<main>` 內 `<style>`／`<script>` 违规**84 筆**（67 個 `<style>` ＋
17 個 `<script>`，分布在 66 個檔案）——這是**已知、預期中**的搬遷待辦，不是新 bug，S0-9 正式搬遷時
才處理（`<style>` 移到 SFC 頂層不加 `scoped`、`<script>` 重寫為 `<script setup>`）。

🔵 **驗證 `docs/13` §6 的數字**：「17 頁 `<script>` 去重後只有 11 種」「65/80 頁有 `<style>`」
**兩個數字都還正確**，但要注意口徑——`docs/13` 數的是「頁數」，本工具數的是「落在 `<main>` 內的
標籤實例數」：`<style>` 有 **67** 個實例（不是 65），因為 `src/partials/membership-benefits.html`
這個共用內容片段自己帶一個 `<style>`，被 `zh/member/`、`zh/culture/fan-club/` 兩頁各 include 一次，
每 include 一次就多算一次標籤實例，但**有 style 標籤的頁面數仍是 65**（這兩頁本來就各自也有自己的
`<style>`，只是變成一頁 2 個標籤）。去重後 `<style>` 為 **58 種不同內容**、`<script>` 為 **11 種**
（與 `docs/13` 一致）。

**自我測試**（證明工具真的會抓到問題，見下方「怎麼驗證這三支工具本身沒問題」）。

---

## ② `codemod-root.mjs` — `{{ROOT}}`／`{{>partial}}` → 絕對路徑

`site/src/pages/**/*.html` 用 `{{ROOT}}` 代表站台根路徑，`build.mjs` 依輸出頁面的巢狀深度填成
`..`／`../..` 這類相對 dot-path。Nuxt SSR 沒有「輸出檔案巢狀深度」這回事，資源路徑要用**絕對路徑**
（`/assets/img/x.png`）。這支工具做機械替換。

**實查 `build.mjs` 確認過**：`site/src/pages/` 這一層實際會出現的 token 只有兩種——
`{{ROOT}}`（1035 處，2026-09-21 實測）與 `{{>partial-name}}` 內容片段 include（2 處，皆為
`{{>membership-benefits}}`）。其餘 token（`TITLE`／`DESCRIPTION`／`HEADER`／`FOOTER`／`CONTENT` 等）
只出現在 `src/partials/shell.html` 本身，不會出現在頁面 body 裡，不是這支工具的替換對象。

```bash
# dry-run（預設，不寫檔，只印每個檔案會改幾處）
node tools/codemod-root.mjs src/pages --out /tmp/nuxt-pages-draft

# 真的寫檔
node tools/codemod-root.mjs src/pages --out /tmp/nuxt-pages-draft --write

# 自訂絕對路徑前綴（預設空字串，{{ROOT}}/x → /x）
node tools/codemod-root.mjs src/pages --out /tmp/nuxt-pages-draft --base "" --write
```

⛔ **輸入路徑、輸出路徑分開**：這支工具給「複製到 Nuxt 專案的副本」用，**不會、也拒絕**寫回
`site/src/` 或它的任何子路徑（安全防呆，寫入前會檢查輸出路徑是否落在 `site/src/` 之內，是的話
直接報錯結束，不寫檔）。

🔴 **涵蓋 CSS 註解與 `<style>`／`<script>` 內的 token**——2026-09-20 切片時就漏抄過這個。做法是
「整份檔案文字全域替換」，不是只掃 `href=`／`src=` 屬性，天生涵蓋任何位置（已用合成案例驗證，
見下方自我測試）。

**Include 展開順序比照 `build.mjs`**：先展開 `{{>partial}}`（片段內容可能自己也帶 `{{ROOT}}`），
**再**統一做 ROOT 替換，順序反了片段裡的 token 會漏轉。片段來源目錄預設 `site/src/partials`，
可用 `--partials <目錄>` 覆寫。

---

## ③ `compare-dom.mjs` — DOM 比對回歸關卡（2026-09-21 硬化版：真正的零差異關卡）

比對「Nuxt SSR 輸出」與「`site/dist` 對應頁」的 `<main id="main">` 內容，正規化後要求零差異。

🔴 **2026-09-21 定案（使用者拍板）：這支工具過去「列出所有差異讓人判斷」，78 頁幾乎頁頁有
差異，逐頁判斷「這屬不屬於已知的必然差異」等於把腳本關卡退化回目視判斷——`docs/14-invariants.md`
明訂「不得為了讓比對變綠而放寬正規化」正是在防這件事。現在的立場是：把已查證的六類必然差異
寫死在正規化規則裡，其餘一律硬性失敗（exit 非 0）。**

⛔ **這六類是封閉清單，不是範例，沒有、也不會有 `--ignore`／`--allow`／`--skip-page` 之類的旁路
參數。** 發現任何「看起來應該無害但不屬於這六類」的差異，正確做法是**停下來問**，不是在工具裡
加第七類、也不是放寬既有規則的比對範圍——這兩件事都會讓關卡再度形同虛設。

```bash
# 單頁：本機檔案 vs 本機檔案
node tools/compare-dom.mjs --expected dist/zh/about/index.html --actual /path/to/nuxt-output.html

# 單頁：本機檔案 vs 跑起來的 Nuxt server
node tools/compare-dom.mjs --expected dist/zh/about/index.html --actual http://localhost:3000

# 整批：dist 目錄 vs 跑起來的 Nuxt server（80 頁一次比完，這是實際收進 CI 的用法）
node tools/compare-dom.mjs --expected dist --actual http://localhost:3000

# 整批：dist 目錄 vs 另一個本機目錄（例如 nuxi generate 的 .output/public）
node tools/compare-dom.mjs --expected dist --actual .output/public

node tools/compare-dom.mjs --expected dist --actual http://localhost:3000 --json   # CI 用
```

`--actual` 用 `http://`／`https://` 開頭就當成網站根 URL 抓即時渲染的頁面；批次與單頁模式都會把
`dist/zh/about/index.html` 這種路徑轉成 route `/zh/about/`（`index.html` → `/`；單頁模式會自動找
路徑裡的 `dist/` 片段算出「相對於站台根目錄」的部分，不是只看檔名——這樣才不會把任何一個
`index.html` 都誤判成站台根目錄）。不是 URL 就當本機檔案或目錄，用**跟 `--expected` 相同的相對
路徑**直接對應。

### 六類必然差異（封閉清單，逐條附「為什麼無害」「怎麼查證」「範圍邊界」，完整版在檔頭註解）

1. **`{{ROOT}}` 相對路徑 → Nuxt 絕對路徑**——只處理 `href`／`src`，用 WHATWG URL 以該頁的
   route 目錄為 base 把相對路徑解析成絕對路徑再比對；scheme（`http:`／`mailto:`…）、
   protocol-relative、純錨點、已是絕對路徑的值原樣比對。
2. **`<div id="__nuxt">` 包裹**——🔵 **這支工具不需要為它寫任何正規化程式碼**：`findMain()` 是
   不限深度的遞迴搜尋，不管 `<main>` 外面包幾層 `<div>`，比對範圍本來就從 `<main>` 的子節點開始，
   包裹層天生不會進入比對樹。查證：2026-09-21 實測 Nuxt SSR 輸出，`<main>` 外層雖有
   `<div id="__nuxt"><div>…</div></div>` 包裹，但比對結果完全不受影響。
3. **Nuxt 注入的 `modulepreload`／`importmap`**——同樣不需要正規化程式碼，這些標籤一律在
   `<head>`，天生不會出現在 `<main>` 比對範圍內（已用實際輸出切片驗證）。
4. **頁面 `<style>`／`<script>` 由 `<main>` 內移到 SFC 頂層**——🔴 正規化做法是在建樹時把
   `<style>`／`<script>` 節點整個排除（兩側都排除），不是「容忍標籤名稱不符」：節點消失會讓
   後面所有手足元素的陣列索引位移，硬容忍標籤名稱只會產生一長串假警報、還會蓋掉真正的結構
   錯誤。排除後兩側陣列重新緊縮對齊，索引自然對得上。
5. **Vue 對靜態 `style="..."` 屬性補結尾分號**——兩側的 `style` 值都解析成「宣告陣列」（用 `;`
   切開，**不**過濾空字串片段）再比對，只容忍「actual 剛好比 expected 多一個結尾空字串」這一種
   收尾模式。🔴 刻意不做「濾掉所有空宣告再比對」，因為那樣會讓 `color:#fff;;`（雙分號，真的畸形
   的值）跟 `color:#fff;` 被誤判成一樣——查證時特別寫了合成測試案例證明這點（見下方「怎麼驗證」）。
6. **`<select v-model>`／`<input v-model>` 的 SSR 顯式印出 `selected=""`／`value=""`**——範圍
   嚴格限制：只有「`value` 剛好是空字串且 expected 完全沒有這個屬性」，或「`selected` 落在該
   `<select>` 的第一個 `<option>` 且 actual 印出空字串」這兩種情形視為無差異。其他任何
   `selected`／`value` 差異（含反方向：expected 有、actual 沒有）一律是真差異。這是行為性
   屬性不是格式差異，範圍故意收得很窄，避免蓋掉真正的預設值錯誤。

### 站台根路徑 `/`：排除在內容比對之外，改斷言 HTTP 行為（2026-09-21，不是第七類必然差異）

mockup 的 `site/dist/index.html` 是 client-side 導轉 stub（`<main>` 內只有
`<script>location.replace('zh/');</script>`），Nuxt 的 `/` 則是 `nuxt.config.ts` routeRules
設定出來的真正伺服器端 302（實測 `curl -D - http://127.0.0.1:3011/` → `HTTP 302`、
`Location: /zh/`）。兩者的 `<main>` 天生沒有可比對的「內容」——若沿用一般路徑做法
（`--actual` 是 URL 時直接 fetch 該路徑），fetch 預設會 follow redirect，等於在比較「/」
的 mockup stub 與「/zh/」的 Nuxt 真實內容，比較的其實是兩個不同頁面。

🔴 **這不是第七類必然差異**：前六類的性質是「兩邊內容其實等價，序列化或框架機制造成表面
差異，正規化後可以比」；`/` 的性質不同——兩邊根本不是同一種東西（一個是內容頁、一個是
重定向），不存在「正規化後內容相等」這回事。工具改為換一種斷言：`--actual` 是 URL 時，
遇到站台根路徑一律不進 DOM 比對，改用 `redirect: 'manual'` 直接檢查回應本身，必須是
**3xx 狀態碼且 `Location` 指向 `/zh/`**，不符合就算這頁失敗（訊息會印在 `structuralError`）。
`--actual` 是本機檔案或目錄（沒有 HTTP 回應可看）時維持原本的 DOM 比對（例如 `dist` 對自己
做整批自我測試，兩邊本來就是同一份 stub，DOM 比對本來就零差異，不受影響）。程式碼與更完整
的說明見 `compare-dom.mjs` 檔頭「站台根路徑『/』」一節。

以上六類之外，工具仍保留基礎的序列化雜訊過濾（跟「搬遷差異」無關，任何 DOM 比對工具都該有）：
空白摺疊、屬性依名稱排序比對（標籤／屬性名稱轉小寫）、只比對 parse5 解析後的樹（自閉合寫法
天生等價）、`data-v-*` 排除（但偵測到會在報告開頭提示，可能代表搬移的 `<style>` 被誤加了
`scoped`，這是另一件事要人工確認）、註解節點預設排除（`--strict-comments` 開啟嚴格比對）。

差異會印成可讀清單，每筆有**路徑**（例如 `main[0] > section[2] > div[1] > a[0]`）、**差異種類**
（標籤名稱／屬性／子節點數量／文字內容）、**expected 與 actual 的實際值**，不是只回 true/false。
結束碼：有任何一頁比對出「六類以外」的差異、或任何一頁抓取失敗 → 非 0，全部零差異 → 0（CI 關卡
的語意）。報告最後一行會同時印通過與失敗頁數（例如「共 5/80 頁有差異（75 頁乾淨）」），批次跑完
不需要另外數。

**驗證方式**：`site/dist` 對自己（80 頁全過）＋ 人工在複本裡製造文字差異、`href` 差異（驗證類別 1
的路徑解析）、整頁重新排版三種情形，證明「內容真的不同」會被抓到、「純排版不同」不會被誤判；
另外用三個小型合成頁面驗證六類正規化本身：(a) 六類差異同時出現的「乾淨」actual 版本必須零差異，
(b) 同一批位置改成雙分號畸形 style、`selected` 落在第二個 option、`value` 塞了非空字串三種「真
差異」，必須全部被抓到、一個都不能被六類規則誤放行。2026-09-21 對 `apps/web` 實跑全站（見
`docs/18-work-errors.md` 與交付報告）：**當時**80 頁中 75 頁 `<main>` 零差異，其餘 5 頁的差異
全部可追溯到兩個已知、待其他工作處理的資料問題（`Match` 缺 `match_no` 欄位、「台中磐石國際
足球盃」6 篇文章的分類歸屬待客戶確認），以及一個需要人工決策的範圍問題（站台根路徑 `/`
在 Nuxt 是真正的伺服器端 302 導轉，不是像 mockup 一樣的可比對內容頁）。

**同日稍晚（`match_no` 場次編號同步鏈收尾，`backend-engineer`）**：`Match.match_no` 補齊、
`/` 改為上面的 302 斷言後重跑，**80 頁中 77 頁 `<main>` 零差異**（`zh/schedule` 與 `/` 兩頁
由本次解決）；剩下 3 頁（`zh/news`／`zh/news/match`／`zh/news/camps-events`）全部是前述
intcup 分類問題，沒有出現任何六類以外的新差異。

### 2026-09-23：`S0-9f` 讓新聞卡片連結變成逐篇 slug 後，退化成 67/80——新增「退役頁面清單」機制

**起因**：`S0-9f` 把新聞卡片連結從 mockup 寫死的 `{{ROOT}}/zh/news/article/`（單一佔位頁，
mockup 從來沒有逐篇文章頁）改成 Nuxt 逐篇 `/zh/news/<slug>/`——**這是規格上正確的修正**，
但 `site/dist` 這份比對基準沒有、也不會再跟著更新（它本來就要退場，見
[`docs/14-invariants.md`](../../docs/14-invariants.md)「前台改 Nuxt」整節），於是全站比對從
77/80 退成更差。`STATUS.md` `S0-9i` 轉述的「67/80」與「10 頁」是派工時的估計數字，
**2026-09-23 實際重跑（`apps/web` build ＋ `node .output/server/index.mjs` ＋ `apps/api`
連本機 `tcrfc_club_dev`）拿到的真實結果是 80 頁中 13 頁失敗**，逐頁人工核對後分成三組：

1. **純粹是 `S0-9f` 的自然後果、且整頁只有這一種差異（5 頁）**：`zh/news/index.html`、
   `zh/news/article/`（route 在 apps/web 已整支移除，抓取回 404）、`zh/news/club/`、
   `zh/news/community/`、`zh/news/international/`（逐筆核對，**差異 100% 是 news-card 的
   `href` 屬性**，其餘標籤／class／文字／圖片／日期零差異）。
   🔴 **`zh/news/index.html` 一開始被錯誤地歸到第 2 組**（理由是「新聞單元那批都混著 intcup
   分類落差」），2026-09-23 用 `--json` 輸出逐筆比對 expected／actual 後確認它的
   **20 筆差異零例外全是 `/zh/news/article/` → `/zh/news/<slug>/`**，已補進清單。
   **這是一次分組推論取代逐筆核對造成的誤判**（記為 [`docs/18`](../../docs/18-work-errors.md) `E-40`）——
   方向與「連坐退役」相反卻同源，而且更難發現：**紅燈多一頁不會有人來查**。
2. **同時混著 `S0-9f` 的 href 差異與另一個既有、無關的問題（2 頁）**：
   `zh/news/match/index.html`、`zh/news/camps-events/index.html`——這 2 頁除了 href 之外，
   還混著「台中磐石國際足球盃」6 篇文章分類歸屬（`camps-events` vs `match`）的既有落差
   （上面 2026-09-21 的紀錄就已經點名這是「待客戶確認」的問題，`STATUS.md` `B-15`），
   導致月份篩選器選項、文章篇數、卡片內容整批對不上，**不是單純 href 差異，不得退役**。
3. **跟新聞連結完全無關的真差異（6 頁）**：`zh/index.html`（**混合**：5 個新聞卡片是
   `S0-9f` 的 href 差異，但同一頁還有導覽連結錯誤、圖片 `alt` 缺失、圖片尺寸不對、
   錨點 `id` 缺失、文案字數不同——這些都跟新聞連結無關）、`zh/about/ecosystem/index.html`、
   `zh/about/index.html`、`zh/about/milestones/index.html`、`zh/club/first-team/index.html`、
   `zh/join/index.html`——**這 6 頁全部與 `S0-9f` 無關，是其他真 bug，不得退役也不屬於
   這次任務的修復範圍**（詳細差異內容見對應的交付報告，這裡不重複列出，避免文件跟程式碼
   兩處各寫一份、之後對不上）。

**當下只有第 1 組（5 頁）進了 `compare-dom.mjs` 的 `RETIRED_ROUTES`**，第 2、3 組（合計 8 頁）
維持在失敗清單裡——退役機制的目的是「基準本身不代表正確答案時換一種驗法」，
不是「跟這次改動有關的差異都算了」。

### 🔵 同日稍晚：第 3 組那 6 頁**全部修掉了**，`zh/index.html` 隨後也符合退役條件

第 3 組（`zh/index.html`／`zh/about/ecosystem/`／`zh/about/`／`zh/about/milestones/`／
`zh/club/first-team/`／`zh/join/`）登記為 `STATUS.md` 的 `S0-9k` 之後立刻派工修完。
**成因是同一件事**：這幾頁為了跟藍鯨共用而從靜態 HTML 改成資料驅動（`shared/utils/club-copy.ts`
的 `ClubText<T>`），但**磐石那一份的值沒有照 mockup 原值填**——`PillarCopy` 介面少了
`id`／`imgAlt`／`imgWidth`／`imgHeight` 四個欄位，首頁 12 筆差異有 11 筆出自這一個原因。
⚠️ **這不是「一模一樣」與「兩站共用」互相衝突**：`ClubText` 的 `tcrfc` 那一份本來就該放
mockup 的逐字原值，機制沒錯，是填錯了。

修完之後 `zh/index.html` 只剩 5 筆新聞卡 href 差異、**逐筆核對零例外**，與第 1 組同一個成因。
**使用者 2026-09-23 裁決：退役，但不准沿用其他 5 頁的 `covered_by`**——
首頁退役等於整頁 DOM 不再比對，而 `link-checker/valid-route` 只驗得了連結，
首頁的 hero、四大支柱、CTA 三卡從此沒有任何自動驗收。
因此**先寫出接手的檢查才退役**：新增 `apps/web/scripts/check-homepage-fidelity.mjs`，
從 `site/src/pages/zh/index.html`（選 `src` 不選 `dist`：前者納管、是真實來源，後者只是產物）
解析出 hero 兩個 CTA 與四大支柱卡片的 `id`／`href`／`alt`／`width`／`height` 共 23 個欄位，
逐一要求 `HOME_PILLARS.tcrfc`／`HOME_HERO.tcrfc` 對得上；
**讀不到 mockup 時 fail-loud（exit 1）**，不會安靜跳過——`site/` 退場時這支腳本要一起處理，
檔頭寫明了這個相依。

**目前狀態：`passed=72 / failed=2 / retired=6`**，紅燈只剩第 2 組（`B-15`，等客戶確認
intcup 6 篇文章的分類歸屬）。

**退役頁面清單（`RETIRED_ROUTES`）的設計**，完整規則與逐條理由在
[`compare-dom.mjs`](compare-dom.mjs) 檔頭「退役頁面清單」一節，這裡只列摘要：

- 這是**跟六類必然差異完全不同的機制**，不要混為一談：六類管的是「同一頁之內，哪些差異
  可以正規化掉」（兩側仍在比對）；退役清單管的是「這一整頁還能不能拿 `site/dist` 當基準」
  （整頁不再進入 DOM 比對）。兩者都是封閉清單、都沒有 CLI 旁路，但退役清單多一條硬性規定：
  **每一筆都必須同時有 `why`（為什麼不能再用 site/dist 當基準）與 `covered_by`（改由哪一個
  檢查接手驗這一頁）**，缺一個工具就直接拒絕執行（`process.exit(1)`，在做任何比對之前）。
- 新聞單元那 5 筆的 `covered_by` 都是同一個真實存在、已核對過的檢查：`apps/web` 的 ESLint 規則
  `link-checker/valid-route`（`npm run lint:eslint`，由 `@nuxtjs/seo` 內建的
  `nuxt-link-checker` 模組提供，等級是 **error**）。2026-09-23 實測 `npx eslint .` 對這
  5 頁全部回 0 個 `valid-route` error，證明目前產出的新聞卡片連結確實都指向真實路由；
  如果之後有人手滑把連結改回寫死的 `/zh/news/article/`，這條規則會炸成 error 擋下 `lint`。
  ⚠️ **範圍要老實承認**：`valid-route` 驗的是「連結格式指向的路由存在」，不是這 5 頁完整
  DOM 內容的逐點正確性——這是誠實的降級，因為這 5 頁**目前唯一的已知差異就是 href**，
  `covered_by` 精準對應被退役的那個差異本身；如果之後這幾頁的其他內容（卡片版型、圖片、
  文字）另外壞掉，`link-checker/valid-route` 不會抓到，這是退役機制天生的盲區。
- 🔴 **`/zh/`（首頁）那一筆的 `covered_by` 是兩項合起來**，刻意跟上面 5 筆不同：
  ① `link-checker/valid-route`（涵蓋新聞卡 href）② `check-homepage-fidelity.mjs`（涵蓋 hero
  CTA 與四大支柱的 `id`／`href`／`alt`／寬高）。**範圍一樣要老實承認**：兩項合起來仍不是整頁
  逐點核對，CTA 三卡、贊助商牆、商店帶、賽事帶都沒有自動驗收。
  ⚠️ 刻意**不**把涵蓋範圍做大——解析愈多，腳本對 mockup 的 HTML 結構就愈脆弱，
  而 mockup 本來就要退場；釘住的是**這次真的回歸過的那批值**，不是「能釘多少釘多少」。
- **報告輸出**：通過／失敗／退役三個數字分開印（`--json` 輸出新增 `retired` 欄位，
  `results` 陣列裡退役項目帶 `retired: true`／`route`／`why`／`coveredBy`）。退役頁逐頁列出
  route 與 `covered_by`；退役頁數不為 0 時，結尾摘要一定會印「N 頁已退出 site/dist
  基準」，不會只印「全部通過」讓退役偽裝成乾淨。退役不計入通過或失敗，**結束碼只看
  真正比對過的頁面**（`failed.length`），跟以前一樣。

**2026-09-23 實測結果**（`apps/web` build ＋ 本機跑 ＋ `apps/api` 連 `tcrfc_club_dev`）：

```
比對 74 頁，另有 6 頁已退出 site/dist 基準
共 2/74 頁有差異（72 頁乾淨）
🔵 另有 6 頁已退出 site/dist 基準（不計入上面的通過或失敗數字）
```

**驗證方式**：① 暫時清空 `RETIRED_ROUTES` 重跑，結果與退役機制加入前完全一致（80 頁中
13 頁失敗，逐頁 route 清單相同）——證明六類正規化與既有比對邏輯未受影響。② 暫時清空某一筆
的 `covered_by` 重跑，工具在做任何比對之前就印錯誤訊息並 `exit 1`；復原後重跑回到
`exit 1`（因為仍有 8 頁真差異未解決，這是預期行為，不是退役機制的問題）。

**尚未做、留給後續**：只剩第 2 組 2 頁（`zh/news/camps-events/`、`zh/news/match/`）是紅燈，
要等 intcup 6 篇文章的分類歸屬由客戶確認（`STATUS.md` `B-15`）。
⛔ **這 2 頁不能比照首頁退役**——它們不是「基準本身不代表正確答案」，
是「這批文章該歸哪一類還沒定案」，**紅燈是正確狀態**，定案之前不該讓它變綠。

🔴 **另有一個涵蓋落差要知道**（`STATUS.md` `S0-9m`、`docs/18` `E-42`）：
`docs/14-invariants.md` 的不變量寫的是「**body** 的 DOM 結構、class 名稱、元素順序與文字內容
一律不動」，但這支工具**只比對 `<main>`**——**頁首與頁尾在不變量範圍內、卻在工具範圍外**。
2026-09-23 已經真的漏掉一個（`ClubAssets.nameZh` 誤用讓頁首頁尾每一頁都印錯文案，
被抓到純粹是因為同一個誤用剛好也命中了 `<main>` 裡的兩處）。**改 layout 層的東西時，
這道關卡不會替你把關。**

---

## 搬頁時的正確順序

1. **先跑 `check-wellformed.mjs`**，把 `<main>` 內的 `<style>`／`<script>` 清單印出來對照著搬
   （紀律 9：`<style>` 移到 SFC 頂層、不加 `scoped`；`<script>` 重寫成 `<script setup>`）。
   看到任何「標籤未閉合」「孤立結束標籤」先回頭修 `site/src/pages/`、重跑
   `node build.mjs && node verify.mjs` 確認六項檢查仍全過，**這一步沒過不要往下走**。
2. 把要搬的頁面用 `codemod-root.mjs`（dry-run 先看摘要，確認無誤再 `--write`）轉成
   `{{ROOT}}`／`{{>partial}}` 都展開、絕對路徑的版本，複製到 Nuxt 專案側，照紀律 9／10 動手改成
   SFC（`<template>` 只留搬移後乾淨的 HTML、`<style>` 搬到頂層不加 `scoped`、`<script>` 改寫）。
3. 用 `compare-dom.mjs` 比對 Nuxt 輸出（本機 `nuxt dev`／`http://localhost:3000` 皆可）與
   `site/dist` 對應頁，**`<main>` 內零差異才算搬完**。差異報告會直接告訴你是哪個節點、哪個屬性、
   還是子節點數量對不上。
4. 全部頁面搬完後，`compare-dom.mjs --expected dist --actual <Nuxt 網址> --json` 整批跑一次收進
   CI，之後任何改動誤觸頁面內容都會被擋下來。

---

## 怎麼驗證這三支工具本身沒問題（2026-09-21 實測記錄）

**① `check-wellformed.mjs`**——三個合成案例，證明工具真的會抓到問題、乾淨檔案不會被誤判：
- 複製一頁、人工塞回歷史上真的發生過的孤立 `</main>`（`<main>...</main></main>`）→ 正確抓到
  `[孤立結束標籤] </main>`，`exit=1`。
- 複製一頁、拔掉一個 `</div>` → 正確抓到 `[標籤未閉合] <div>`，`exit=1`。
- 全新的乾淨頁面（無 `<style>`／`<script>`）→ `✓ 沒有發現良構性問題`，`exit=0`。

**② `codemod-root.mjs`**——合成一個頁面，`{{ROOT}}` 分別出現在 CSS 註解、`<style>` 的
`url(...)`、`<script>` 的字串字面值、`<a href>` 四種位置 → dry-run 與 `--write` 都正確回報
4 處替換，輸出檔逐一確認全部換成絕對路徑、無殘留 token。另外實測對 `--out` 指到
`site/src`／`site/src/pages` 會被安全防呆擋下，`git status` 確認 `site/src` 全程未被動到。

**③ `compare-dom.mjs`**——
- `site/dist` 對自己（80 頁）：全部零差異，`exit=0`。
- 複製一頁，改一段 `<main>` 內的文字與一個 `href` 屬性 → 正確印出兩筆差異（含路徑、expected／
  actual），`exit=1`。
- 整批複製 `dist/`，其中一頁**整頁重新排版**（打散所有換行縮排、內容不變）、另一頁**真的改了
  一個字**：80 頁比對結果為「79 頁過、1 頁差異」，差異精準指到被改的那一頁與那個節點——證明空白
  正規化確實濾掉了排版雜訊，同時真正的內容差異不會被一起濾掉。
- 🔴 **2026-09-21 硬化版追加驗證**（六類正規化規則本身）：三個小型合成頁面（見上方「怎麼驗證」）
  分別驗證「六類差異同時出現時必須零差異」與「雙分號畸形 style／selected 落在非首個
  option／value 塞非空字串三種真差異必須全部被抓到」，兩邊都通過。並對 `apps/web` 實際
  build（`npm run build`）＋ 本機跑（`node .output/server/index.mjs`）＋ `apps/api` 連本機
  `mssql-dev`（含種子資料）做過一次全站 80 頁實跑：75 頁零差異，其餘 5 頁全部可歸因於
  `Match.match_no` 缺欄位、6 篇文章分類歸屬待確認、`/` 的伺服器端 302 導轉不是內容頁三個
  已知、非本工具職責的原因（細節見交付報告）。這次實跑同時抓到並促成修掉四個真差異（見
  交付報告「修掉的真差異清單」），證明關卡收緊後確實能分辨「該過的頁面」與「真的有問題的
  頁面」，不是只會全部放行或全部擋下。
