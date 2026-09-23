#!/usr/bin/env node
// S0-9d ③：DOM 比對回歸關卡（2026-09-21 硬化版）。
//
// 目的：搬頁到 Nuxt 後，SSR 輸出的 <main id="main"> 內容必須與 site/dist 對應頁
// 正規化後零差異（docs/14-invariants.md 行 128 附近：「驗收要能被腳本驗證，不是用眼睛看」）。
//
// 🔴 2026-09-21 定案（使用者拍板）：這支工具過去「列出所有差異讓人判斷」——78 頁幾乎頁頁
// 有差異，逐頁判斷「這屬不屬於已知的必然差異」等於把腳本關卡退化回目視判斷。**現在的立場是：
// 把已查證的六類必然差異寫死在下面的正規化規則裡，其餘一律硬性失敗（exit 非 0）。**
//
// ⛔⛔⛔ 這六類是封閉清單，不是範例。⛔⛔⛔
//   - 不提供 --ignore／--allow／--tolerate 之類的旁路參數，永遠不會有。
//   - 發現任何「看起來應該無害但不屬於這六類」的差異，正確做法是「停下來問」，
//     不是在這支工具裡加第七類、也不是放寬既有規則的比對範圍。
//   - 每一類規則的程式碼旁都寫死「為什麼無害」「怎麼查證的」「範圍邊界在哪」——
//     這份註解是之後判斷「能不能再加一條」的唯一依據，不是裝飾。
//
// ============================================================================
// 六類必然差異（逐條附查證方式與範圍邊界）
// ============================================================================
//
// 【類別 1】{{ROOT}} 相對路徑 → Nuxt 絕對路徑
//   為什麼無害：site/build.mjs 把 site/src/pages 裡的 {{ROOT}} token 依輸出頁面的巢狀深度
//   展開成 ../、../../ 這類相對 dot-path；Nuxt 的路由沒有「輸出檔案巢狀深度」這回事，
//   同一個資源理所當然要用絕對路徑（/assets/...）表示。兩者指向的是同一個資源，
//   不是內容差異，是兩種建置系統對「站台根目錄」的不同表示法。
//   怎麼查證：實查 site/build.mjs 與 codemod-root.mjs（site/tools/README.md ②）確認
//   {{ROOT}} 在 src/pages 這一層只出現在 href／src 屬性值（也出現在 CSS 註解／<style>／
//   <script> 內，但那些整個標籤已被類別 4 排除，不會走到這裡）。
//   範圍邊界：只處理 href／src 兩個屬性名稱；只把「非 scheme、非 protocol-relative、
//   非 # 開頭、非已經是 / 開頭」的值視為相對路徑並解析；解析用 WHATWG URL 以 Nuxt 該頁
//   的 route 目錄當 base，兩側（expected／actual）都跑同一段轉換——actual 已經是絕對路徑，
//   跑過這段轉換是恆等函式（value.startsWith('/') 直接原樣傳回），不會意外改動它。
//   外部連結（http(s)://…）、mailto:、tel:、純錨點（#foo）一律原樣比對，不受影響。
//
// 【類別 2】<div id="__nuxt"> 包裹
//   為什麼無害：tcrfc.css 的 body{} 規則只有 margin／font／overflow，沒有任何
//   `body > ` 子選擇器（已用 grep 核對 tcrfc.css 全文，見下方「查證」），多一層 <div>
//   不會改變任何視覺呈現。
//   怎麼查證：2026-09-21 實測 Nuxt SSR 輸出（node .output/server/index.mjs），
//   結構是 <body><div id="__nuxt"><div>…(header/nav/…)<main id="main">…</main>…</div></div></body>。
//   🔵 **這一類在本工具裡不需要任何正規化程式碼**——findMain() 是不限深度的遞迴搜尋
//   （walkTree 逐節點比對 tagName === 'main' && id === 'main'），不管 <main> 外面包了
//   幾層 <div>，找到的都是同一個 <main> 節點，比對範圍從 <main> 的子節點開始，
//   包裹層本來就不會進入比對樹。這裡刻意保留這段說明性註解，而不是靜默略過，
//   是因為使用者的六類清單明文列了這一條——沒寫程式碼不代表沒查證過，
//   是查證結果為「這支工具的比對範圍設計已經免疫這個差異」。
//
// 【類別 3】Nuxt 注入的 modulepreload／importmap 等標籤
//   為什麼無害：這些標籤（<link rel="modulepreload">、<script type="importmap">）
//   一律由 Nuxt 寫進 <head>，不會出現在 <body> 或 <main> 裡。
//   怎麼查證：2026-09-21 對 zh/about/ 實測輸出，'modulepreload' 與 'importmap' 兩個
//   字串只出現在 <head> 內；<main id="main"> 到 </main> 之間的子字串完全不含這兩個字串
//   （用 Node 直接切片驗證，不是用肉眼掃）。
//   🔵 跟類別 2 一樣：本工具只比對 <main> 子樹，這一類差異天生不會進入比對範圍，
//   不需要正規化程式碼，這段註解是查證紀錄，不是待辦。
//
// 【類別 4】頁面 <style>／<script> 由 <main> 內移到 SFC 頂層
//   為什麼無害：<style>／<script> 本身不是可視內容（不渲染成畫面元素），搬到 SFC 頂層
//   後內容一字不改（docs/13 §6 紀律 9），只是「印在哪個位置」的差異。
//   🔴 為什麼不能只是「容忍標籤名稱不符」：<style>／<script> 節點從 <main> 的子節點列表
//   消失，會讓它後面所有手足元素的陣列索引往前位移一格。如果比對邏輯是逐一比對
//   children[i] vs children[i]，一個節點的消失會讓後面每一個手足都被拿去跟「錯位」的
//   對象比較，產生一長串看似嚴重、其實只是位移造成的假警報（標籤名稱不符、屬性全部
//   不一樣……）。正確做法是在正規化階段——也就是建樹的當下——就把 <style>／<script>
//   節點整個排除在正規化後的子節點陣列之外（兩側都排除），讓陣列重新緊縮對齊，
//   後面的比對看到的兩棵樹本來就沒有這兩種節點，索引自然對得上。
//   怎麼查證：2026-09-21 對 zh/about/、zh/index 等頁實測，套用排除後 <main> 直屬子節點
//   數量兩側一致，且不再出現連鎖的「標籤名稱不符」；zh/schedule/（本次遷移中最大的
//   <script> 一份，251 行）實測同樣對齊。
//   範圍邊界：只排除 tagName 為 style／script 的節點本身（含其全部子孫，例如
//   <script> 內的文字內容不會被單獨比對到），不影響其他任何標籤；不對標籤名稱做
//   任何「容忍」，其餘標籤名稱不符一律照舊視為真差異。
//
// 【類別 5】Vue 對靜態 style="..." 屬性補結尾分號
//   為什麼無害：2026-09-21 實測 apps/web 目前所有頁面內的 style="..."
//   （零 :style 動態綁定，全部是靜態字串），Vue 編譯器一律解析成宣告物件再重新序列化，
//   序列化規則就是「每條宣告後面補一個分號」，除此之外不改動任何內容——不改冒號旁的
//   空白、不改宣告順序、不改屬性名稱大小寫、不新增或刪除任何一條宣告。
//   怎麼查證：直接 diff mockup 原始碼與本次 Nuxt SSR 實際輸出的 style 屬性值，
//   例如 `object-position:58% 35%` → `object-position:58% 35%;`、
//   `color:#fff` → `color:#fff;`，兩邊 102 個出現點逐一核對只有「有沒有結尾分號」
//   這一種差異模式，沒有空白、順序、內容上的其他改動。
//   🔴 為什麼不能只是把分號 strip 掉再比字串：如果做法是「兩邊都把所有分號拿掉
//   （或把連續分號／空宣告直接濾掉）再比對」，`color:#fff;;justify-content:center;`
//   這種真正畸形的值（例如複製貼上或字串拼接留下的殘留分號）分割、濾空之後會變成
//   跟正常值 `color:#fff;justify-content:center;`完全一樣的陣列，畸形值就這樣蒙混
//   過關——這正是使用者要求「不得為了讓比對變綠而放寬正規化」點名的反例。正確做法
//   是把兩側 style 值都用 `;` 切開成陣列（**不**過濾掉空字串片段），只認一種收尾
//   模式：actual 的陣列剛好比 expected 多一個「結尾的空字串」——也就是 Vue 幫沒有
//   結尾分號的來源補上一個 `;` 之後，`split(';')` 自然多出的最後一格。除此之外，
//   包含字串「中間」出現的空宣告（雙分號、連續分號），一律視為真差異，不做任何寬容。
//   範圍邊界：只套用在屬性名稱剛好是 style 的情況；只有符合上述「至多一個結尾空
//   宣告」的收尾模式才視為無差異，兩側只要有一側缺少 style 屬性、或陣列有任何
//   其他不同（含順序、含中間的空宣告、含屬性或值本身不同），一律照原樣（含
//   expected／actual 的完整原始字串）列為真差異，不做任何進一步寬容。
//
// 【類別 6】<select v-model>／<input v-model> 的 SSR 顯式印出 selected=""／value=""
//   為什麼無害（且為什麼範圍要收得很窄）：這是「行為性屬性」不是格式差異——
//   selected／value 決定表單的預設狀態，放寬過頭會蓋掉真正的預設值錯誤（例如某個
//   filter 的預設選中選項選錯、或某個欄位不該有預設值卻被塞了值）。因此只認兩種
//   狹窄到不可能是 bug 的情形：
//     (a) value="" ——Vue 對有 v-model 綁定、目前值為空字串的 <input> 會在 SSR
//         顯式印出 value=""；mockup 是純靜態 HTML，同一個 <input> 通常完全不寫
//         value 屬性（例如 zh/news/ 的 #filter-search）。兩者呈現的都是「空白輸入框」，
//         沒有視覺或行為差異。
//     (b) selected 落在該 <select> 的第一個 <option>——原生 <select> 在沒有任何
//         option 被標記 selected 時，瀏覽器行為本來就是「視覺上選中第一個」；
//         Vue 的 SSR 只是把這個瀏覽器原生行為顯式印出來（例如 NewsFilterForm.vue
//         的年份／月份篩選器，v-model 預設值等於第一個 option 的 value，於是第一個
//         <option> 被印出 selected=""），mockup 原始碼同樣沒有標記任何 option 為
//         selected（純靜態下拉選單，倚賴瀏覽器預設行為）。兩者呈現的都是「顯示第一個
//         選項」，沒有差異。
//   怎麼查證：2026-09-21 對 apps/web/app/components/news/NewsFilterForm.vue（年份／
//   月份篩選器）與 zh/schedule.vue（賽事類型／主客場篩選器）逐一核對：mockup 端
//   （site/dist 對應頁）的 <option> 一律沒有 selected、<input id="filter-search"> 一律
//   沒有 value；Nuxt 端第一個 <option> 印出 selected=""、<input> 印出 value=""。
//   🔴 範圍邊界（嚴格）：
//     - value="" 只在「expected 完全沒有這個屬性」且「actual 的值剛好是空字串」時
//       視為無差異；只要 expected 有 value（不論是不是空字串）或 actual 的值不是
//       空字串，一律真差異。這個判斷不限定標籤名稱（<input>／<option> 皆可能觸發），
//       因為判斷條件本身已經夠窄（值必須剛好是空字串），不需要再另外限制標籤。
//     - selected 只在「該 <option> 是其 <select> 父節點的第一個子節點」且「expected
//       沒有 selected、actual 的值剛好是空字串」時視為無差異。「是不是第一個子節點」
//       在比對子節點陣列時往下傳遞一個旗標，只有 index === 0 且父節點標籤是 select
//       才會打開；第二個、第三個……option 上的任何 selected 差異，或反方向
//       （expected 有 selected 但 actual 沒有）一律真差異，不套用這條規則。
//
// ============================================================================
// 站台根路徑「/」：排除在內容比對之外，改成斷言它的 HTTP 行為（不是第七類必然差異）
// ============================================================================
//
// mockup 的 site/dist/index.html 是純靜態的 client-side 導轉 stub
// （<main id="main"><script>location.replace('zh/');</script>...</main>），瀏覽器要先
// 載入並執行這段 JS 才會跳到 /zh/。Nuxt 的「/」則是 nuxt.config.ts routeRules 設定出來的
// 真正伺服器端 302（實測：curl -D - http://127.0.0.1:3011/ → HTTP 302，
// Location: /zh/）。這兩者的 <main> 天生沒有「可比對的內容」可言——若沿用一般路徑的做法
// （--actual 是 URL 時直接 fetch 該路徑），fetch 預設會 follow redirect，實際上會比對到
// 「/」的 mockup stub 對上「/zh/」的 Nuxt 真實內容，等於在比較兩個不同的頁面，不是在驗證
// 「/」這個路徑本身的行為。
//
// 🔴 為什麼這不是第七類必然差異：前面六類的性質都是「兩邊內容其實等價，只是序列化或框架
// 機制造成表面差異，可以正規化掉再比」；這裡的性質不同——「/」在兩邊根本不是同一種東西
// （一個是靜態內容頁、一個是重定向），不存在「正規化後内容相等」這回事，所以不透過
// extractMainNormalized／diffTrees 這條路徑處理，而是換一種斷言：伺服器必須回 302，
// Location 必須指向 /zh/。這符合工具檔頭「六類是封閉清單，不是範例」的精神——不是新增
// 第七類豁免規則，是承認「/」根本不屬於「內容頁比對」這個問題域。
//
// 範圍邊界：只在 --actual 是 URL（能拿到真正的 HTTP 回應）時套用這個特例；--actual 是本機
// 檔案或目錄時沒有 HTTP 語意可斷言，維持原本的 DOM 比對（例如 dist 目錄對自己做整批自我
// 測試時，兩邊本來就是同一份 stub，DOM 比對本來就會零差異，不受影響、也不需要特殊處理）。
//
// ============================================================================
// 退役頁面清單（RETIRED_ROUTES）：這一頁「還能不能拿 site/dist 當基準」（2026-09-23）
// ============================================================================
//
// 起因：S0-9f 把新聞卡片連結從 site/src 寫死的 `{{ROOT}}/zh/news/article/`（單一佔位頁，
// mockup 從來沒有逐篇文章頁）改成 Nuxt 逐篇 slug `/zh/news/<slug>/`——這是規格上正確的修正
// （mockup 本來就只是骨架，不代表最終行為），但 site/dist 這份基準沒有、也不會再更新
// （它本來就要退場，見 docs/14-invariants.md「前台改 Nuxt」整節），於是這幾頁的 href 永遠
// 對不上，而且**不是可以正規化掉的表面差異**——這是「基準本身不代表正確答案」，跟前六類
// 「兩邊其實等價、只是序列化方式不同」性質完全不同，硬套六類的作法（例如加一條 href 正規化
// 規則忽略這個特定差異）會違反 docs/14 明文的「不得為了讓比對變綠而放寬正規化」。
//
// 🔴🔴🔴 這份清單與上面的「六類必然差異」是兩套不同機制，分工如下，不要混為一談：
//   - **六類必然差異（正規化規則）**管的是「同一頁之內，哪些差異可以判定為無害」——
//     兩側都還在比對，只是比對前先把已知的序列化雜訊濾掉，過濾範圍窄到逐一寫死在程式碼裡。
//   - **這份退役清單**管的是「這一整頁還能不能拿 site/dist 當基準」——不是濾掉某個差異，
//     是承認 site/dist 對這個 route 已經不是正確答案，整頁不再進入 DOM 比對。
//   - 兩者都不提供旁路、都不能靠掰理由用嘴巴退役：六類是封閉清單「不得新增第七類」，
//     這份清單是「每一筆都要有 covered_by，缺了工具直接拒絕執行」（見下方 validateRetiredRoutes）。
//     **這份清單的存在，不代表六類清單可以比照辦理放寬——兩邊各自封閉，互不借用對方的理由。**
//
// ⛔ 退役是頁面級別的最後手段，不是「這一頁有一點差異就退役」：
//   - 只有「同一頁裡的每一筆差異，追根究底都是同一個已知、已查證、且不會再改的基準落差」
//     時才進這份清單。**任何一筆診斷不出成因、或成因跟這裡列的理由不同的差異，都不准連坐退役**
//     ——那正是這份清單存在的風險（"驗收線被一頁一頁蠶食"，STATUS.md S0-9i 原文）。
//   - 2026-09-23 逐頁核對本次退役的四頁：`/zh/news/article/`（route 在 apps/web 已整支移除，
//     是 mockup 幫全站文章共用的單一佔位頁，抓取回應必為 404）、`/zh/news/club/`、
//     `/zh/news/community/`、`/zh/news/international/`（三頁逐筆核對 diff，比對結果**只有**
//     news-card `href` 屬性不同，其餘標籤、class、文字、圖片、日期全部零差異）。
//   - 🔴 同一次查證也發現 `zh/index.html`／`zh/news/index.html`／`zh/news/match/index.html`／
//     `zh/news/camps-events/index.html` 這四頁**同樣含有** S0-9f 造成的 href 差異，
//     但**混著至少一種跟新聞連結無關的差異**（`zh/index.html` 混了導覽連結、圖片 alt／尺寸、
//     錨點 id、文案字數等一串不相關的真差異；另外三頁混了「台中磐石國際足球盃」6 篇文章
//     分類歸屬的既有落差，導致月份篩選器選項、文章篇數、卡片內容整批對不上，不是單純
//     href 差異）——**這四頁刻意不放進這份清單**，繼續留在失敗名單裡，理由與詳細差異見
//     交付報告，不在這裡重複。
//
// 🔴 每一筆的 covered_by 都是真實存在、已核對過的檢查，不是掰的名字：
//   - `apps/web` 的 ESLint 規則 `link-checker/valid-route`（由 `@nuxtjs/seo` 內建的
//     `nuxt-link-checker` 模組提供，`npm run lint:eslint` 會跑，等級是 error 不是 warning）
//     會拿 `.nuxt/link-checker/routes.json`（建置期產生，含 `/zh/news/:slug()` 這個動態路由
//     樣式）逐一核對頁面裡每一個內部連結的 href 是否指向真實存在的路由。2026-09-23 實測：
//     `npx eslint .` 對這四頁全部回 0 個 `link-checker/valid-route` error（只有無關的
//     `valid-sitemap-link` warning），證明這四頁目前產出的新聞卡片連結確實都指向真實路由，
//     且如果有人手滑把連結改回寫死的 `/zh/news/article/`（或任何不存在的路徑），這條規則
//     會炸成 error 擋下 `npm run lint`，不會悄悄放行。
//   - ⚠️ **範圍要老實承認**：`valid-route` 驗的是「連結格式指向的路由存在」，不是這四頁
//     完整 DOM 內容的逐點正確性（原本 compare-dom 在驗的是後者）。目前這是誠實的降級，
//     不是灌水——因為這四頁**目前唯一的已知差異就是 href**，covered_by 精準對應被退役的
//     那個差異本身。如果之後這四頁的其他內容（卡片版型、圖片、文字）另外壞掉，
//     `link-checker/valid-route` 不會抓到，那會是這份退役機制天生的盲區，不是這次疏漏。
//
// ⛔ 沒有、也不會有讓這份清單失效的 CLI 參數（沒有 --ignore／--allow／--skip-page／
// --retire）。要退役一個頁面，唯一的方法是改這份程式碼，走一般的 code review／PR 流程。
//
// 結構：Map<route, { why, covered_by }>。route 用 toRoutePath() 算出來的形式
// （例如 "/zh/news/article/"，含結尾斜線），跟批次／單頁模式共用同一套路由轉換。
const RETIRED_ROUTES = new Map([
  ['/zh/news/article/', {
    why:
      'site/dist 的這一頁是 mockup 幫「所有文章」共用的單一佔位頁（site/src/pages/zh/news/article/index.html），' +
      '從來不代表任何一篇真實文章的內容。S0-9f 把新聞卡片連結改成 Nuxt 逐篇 slug 路由後，' +
      'apps/web 已整支移除 /zh/news/article/ 這個 route（改為 app/pages/zh/news/[slug]/index.vue），' +
      '2026-09-23 實測 curl 這個路徑回 404——不是失誤，是刻意的路由設計，沒有任何內容可以' +
      '拿來跟 site/dist 比較，維持在一般比對流程裡只會製造一筆永遠不會消失的「抓取失敗」假警報。',
    covered_by:
      'apps/web 的 ESLint 規則 link-checker/valid-route（npm run lint:eslint，nuxt-link-checker' +
      '／@nuxtjs/seo 提供）：只要全站沒有任何連結指向 /zh/news/article/ 這個不存在的路由，' +
      '這條規則就會維持 0 error；2026-09-23 實測 npx eslint . 確認目前確實是 0 error。',
  }],
  ['/zh/news/club/', {
    why:
      '這一頁與 site/dist 的唯一差異是新聞卡片的 href（site/src/pages/zh/news/club/index.html ' +
      '11 個 <a> 全部寫死 {{ROOT}}/zh/news/article/，S0-9f 後 Nuxt 端正確輸出各自的 ' +
      '/zh/news/<slug>/）——2026-09-23 逐筆核對本頁 11 筆差異，全部是「屬性 href」，' +
      '沒有任何一筆是標籤、class、文字、圖片或日期不同。這個 href 差異不是表面序列化問題' +
      '（不屬於六類正規化的性質），是基準本身的內容已經不代表正確答案，六類清單不適用。',
    covered_by:
      '同上——apps/web 的 ESLint 規則 link-checker/valid-route（npm run lint:eslint）。' +
      '2026-09-23 實測對本頁跑 npx eslint . 回 0 個 valid-route error，證明本頁目前產出的' +
      '11 個新聞卡片連結全部指向真實存在的路由。',
  }],
  ['/zh/news/community/', {
    why:
      '同 /zh/news/club/ 的成因與查證方式：site/src/pages/zh/news/community/index.html ' +
      '3 個 <a> 全部寫死 {{ROOT}}/zh/news/article/，2026-09-23 逐筆核對本頁 3 筆差異全部是' +
      '「屬性 href」，其餘內容零差異。',
    covered_by:
      '同上——apps/web 的 ESLint 規則 link-checker/valid-route（npm run lint:eslint），' +
      '2026-09-23 實測本頁 0 個 valid-route error。',
  }],
  ['/zh/news/', {
    why:
      '新聞總覽頁，成因與 /zh/news/club/ 完全相同：site/src/pages/zh/news/index.html 的新聞卡片 ' +
      '<a> 全部寫死 {{ROOT}}/zh/news/article/，S0-9f 後 Nuxt 端正確輸出各自的 /zh/news/<slug>/。' +
      '2026-09-23 逐筆核對本頁 20 筆差異，**全部是「屬性 href」且全部是 /zh/news/article/ → ' +
      '/zh/news/<slug>/ 這一種型態，零例外**（用 JSON 輸出逐筆比對 expected/actual 驗證，' +
      '不是抽查）。\n' +
      '🔴 本頁一度被誤判為「混著台中磐石國際足球盃 6 篇文章分類歸屬的落差、不得退役」而排除在' +
      '清單外。2026-09-23 重跑查證後確認那是誤判——會混到 intcup 分類落差的是 ' +
      '/zh/news/camps-events/ 與 /zh/news/match/ 兩頁（前者「共 7 篇」對「共 1 篇」、後者月份' +
      '篩選器選項整批位移），本頁沒有。留下這段紀錄是因為「連坐退役」正是這份清單最大的風險，' +
      '而這次差點反過來發生：把一頁乾淨的、符合條件的頁面，因為鄰近頁面的問題而錯誤地留在紅燈裡。' +
      '兩個方向都要靠逐筆核對擋，不能靠印象分組。',
    covered_by:
      '同 /zh/news/club/——apps/web 的 ESLint 規則 link-checker/valid-route（npm run lint:eslint）。' +
      '2026-09-23 實測本頁 0 個 valid-route error，20 個新聞卡片連結全部指向真實存在的路由。',
  }],
  ['/zh/news/international/', {
    why:
      '同 /zh/news/club/ 的成因與查證方式：site/src/pages/zh/news/international/index.html ' +
      '12 個 <a> 全部寫死 {{ROOT}}/zh/news/article/，2026-09-23 逐筆核對本頁 12 筆差異全部是' +
      '「屬性 href」，其餘內容零差異。',
    covered_by:
      '同上——apps/web 的 ESLint 規則 link-checker/valid-route（npm run lint:eslint），' +
      '2026-09-23 實測本頁 0 個 valid-route error。',
  }],
]);

// 🔴 這是整份退役機制的重點防呆：缺 covered_by（或缺 why）就讓工具直接拒絕執行，
// exit 非 0，不是印個警告就放行。這個檢查跟目前實際要跑哪些頁面無關——不管這次
// --expected／--actual 有沒有涵蓋到退役清單裡的路由，只要清單本身有一筆資料不完整，
// 就代表清單已經失控，不應該讓任何一次比對繼續執行。
function validateRetiredRoutes(routes) {
  const problems = [];
  for (const [route, entry] of routes) {
    if (!entry || typeof entry.why !== 'string' || entry.why.trim() === '') {
      problems.push(`${route}：缺少 why（為什麼不能再用 site/dist 當基準）`);
    }
    if (!entry || typeof entry.covered_by !== 'string' || entry.covered_by.trim() === '') {
      problems.push(`${route}：缺少 covered_by（改由哪一個檢查接手驗這一頁）——退役機制不允許「先退役、驗證之後再補」`);
    }
  }
  if (problems.length) {
    console.error('退役頁面清單（RETIRED_ROUTES）設定不完整，拒絕執行：');
    for (const p of problems) console.error(`  - ${p}`);
    console.error('\n每一筆退役項目都必須同時有 why 與 covered_by，見 compare-dom.mjs 檔頭「退役頁面清單」一節。');
    process.exit(1);
  }
}

// ============================================================================
// 六類之外：格式層級的基礎正規化（跟「搬遷差異」無關，是任何 DOM 比對工具都該有的
// 序列化雜訊過濾，維持既有實作不變）
// ============================================================================
//   - 空白摺疊：純空白文字節點整個丟棄，有內容的摺成單一空白再比對。
//   - 屬性依名稱排序後比對，標籤與屬性名稱一律轉小寫。
//   - 一律比較 parse5 解析後的 DOM 樹，不比較原始文字——自閉合寫法天生不受影響。
//   - data-v-* 屬性一律排除比對（Vue runtime 注入的識別碼），但若偵測到會在報告開頭提示
//     （可能代表搬移的 <style> 被誤加了 scoped，違反紀律 10，這是另一件事要人工確認）。
//   - 註解節點預設排除，--strict-comments 開啟嚴格比對。
//
// 用法：
//   單頁：
//     node tools/compare-dom.mjs --expected <dist 檔案路徑> --actual <本機檔案路徑或 URL>
//   整批：
//     node tools/compare-dom.mjs --expected <dist 目錄> --actual <本機目錄或網站根 URL>
//   選用：
//     --json              機器可讀輸出（給 CI 用）
//     --strict-comments   註解節點也納入比對
//     --limit N           批次模式只跑前 N 頁（除錯用）
//     --max-diffs N       單頁最多列出幾筆差異，預設 20
//
// ⛔ 沒有、也不會有 --ignore / --allow / --skip-page / --retire 之類的旁路參數。
// 退役頁面清單（RETIRED_ROUTES，見上方「退役頁面清單」一節）只能改程式碼，不接受任何
// CLI 參數控制；清單本身若有一筆缺 why 或 covered_by，工具會在做任何比對之前直接拒絕執行。
//
// 結束碼：只要有任何一頁比對出「六類以外」的差異、或任何一頁抓取失敗 → 非 0。全部零差異
// （不含已退役頁面，見下）→ 0。退役頁面清單本身資料不完整（缺 why／covered_by）→ 非 0，
// 且不會執行到任何比對。退役頁面不計入通過或失敗，但一定會在報告與 --json 輸出裡列出。

import { readFile, readdir, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join, dirname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as parse5 from 'parse5';

const ROOT = dirname(fileURLToPath(import.meta.url));
const SITE_ROOT = dirname(ROOT);

function parseArgs(argv) {
  const out = { json: false, strictComments: false, maxDiffs: 20 };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--expected') out.expected = argv[++i];
    else if (a === '--actual') out.actual = argv[++i];
    else if (a === '--json') out.json = true;
    else if (a === '--strict-comments') out.strictComments = true;
    else if (a === '--limit') out.limit = Number(argv[++i]);
    else if (a === '--max-diffs') out.maxDiffs = Number(argv[++i]);
  }
  return out;
}

async function walkHtml(dir, out = []) {
  for (const e of await readdir(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) await walkHtml(p, out);
    else if (e.name.endsWith('.html')) out.push(p);
  }
  return out;
}

function walkTree(node, cb) {
  cb(node);
  if (node.childNodes) for (const c of node.childNodes) walkTree(c, cb);
  if (node.content) walkTree(node.content, cb);
}

function findMain(doc) {
  let found = null;
  walkTree(doc, (n) => {
    if (found) return;
    if (n.tagName === 'main' && (n.attrs || []).some((a) => a.name === 'id' && a.value === 'main')) found = n;
  });
  return found;
}

// relPath（例如 "zh/about/index.html" 或單檔模式下含 dist/ 前綴的完整字串）→ Nuxt route
// 路徑（例如 "/zh/about/"）。批次模式與單檔模式、--actual 是 URL 時的 fetch 目標、
// 以及類別 1 的相對路徑解析 base，三個用途共用同一份轉換，避免各自寫一份而互相兜不攏。
function toRoutePath(relPath) {
  const norm = relPath.split(/[\\/]+/).filter(Boolean).join('/');
  if (norm === 'index.html' || norm === '') return '/';
  return '/' + norm.replace(/index\.html$/, '');
}

// 類別 1：{{ROOT}} 相對路徑 → Nuxt 絕對路徑。只處理 href／src，只把「看起來是相對路徑」
// 的值解析成絕對路徑；scheme（http:、https:、mailto:、tel: ...）、protocol-relative
// （//…）、純錨點（#…）、已經是絕對路徑（/…）一律原樣傳回。
const SCHEME_OR_ABSOLUTE_RE = /^(?:[a-z][a-z0-9+.-]*:|\/\/|#)/i;
function resolveHrefValue(value, routeDir) {
  if (value == null || value === '') return value;
  if (SCHEME_OR_ABSOLUTE_RE.test(value)) return value;
  if (value.startsWith('/')) return value; // 已是絕對路徑（actual 端一律如此，恆等）
  try {
    const base = 'http://tcrfc-compare-dom.invalid' + routeDir;
    const u = new URL(value, base);
    return u.pathname + u.search + u.hash;
  } catch {
    return value; // 解析失敗就原樣比對，讓差異照實浮現，不要吞掉
  }
}

// 類別 5：style="..." 解析成宣告陣列。回傳 null 代表這個屬性根本不存在（跟「有 style
// 但是空字串」不同，呼叫端要能分辨）。
//
// 🔴 刻意不過濾掉空字串片段（雙分號、結尾分號造成的空宣告）——過濾掉的話，
// "color:#fff;;justify-content:center;" 這種真的畸形值分割後會變成跟正常值一樣的
// ["color:#fff","justify-content:center"]，等於用「濾掉空宣告」的方式悄悄放行了
// 畸形值，剛好是檔頭類別 5 說明裡明確要求不能發生的事。空宣告要不要視為無差異，
// 交給下面的 styleDeclarationsEqual() 依「只有結尾多一個空宣告」這個唯一模式判斷。
function parseStyleDeclarations(value) {
  if (value == null) return null;
  return value.split(';').map((d) => {
    const t = d.trim();
    if (t === '') return '';
    const idx = t.indexOf(':');
    if (idx === -1) return t; // 沒有冒號的畸形片段整段保留，讓它在陣列比對時顯出差異
    const prop = t.slice(0, idx).trim().toLowerCase();
    const val = t.slice(idx + 1).trim();
    return `${prop}:${val}`;
  });
}
// 只容忍一種模式：actual 的宣告陣列剛好比 expected 多一個「結尾的空字串」——也就是
// Vue 幫沒有結尾分號的來源補上一個 ; 之後，split(';') 自然多出的最後一格。除此之外
// （包含字串中間出現的空宣告，例如雙分號）一律判定為不同，不做任何寬容。
function styleDeclarationsEqual(a, b) {
  if (a === null || b === null) return a === b;
  if (a.length === b.length) return a.every((d, i) => d === b[i]);
  if (b.length === a.length + 1 && b[b.length - 1] === '') return a.every((d, i) => d === b[i]);
  if (a.length === b.length + 1 && a[a.length - 1] === '') return b.every((d, i) => d === a[i]);
  return false;
}

// 把 parse5 的 DOM 樹正規化成拿來做深度比對用的簡化結構。
// opts.routeDir：這一頁的 Nuxt route 目錄（例如 "/zh/about/"），類別 1 的 base。
function normalize(node, opts, foundDataV) {
  const kind = node.nodeName;
  if (kind === '#text') {
    const collapsed = node.value.replace(/\s+/g, ' ').trim();
    return collapsed ? { type: 'text', value: collapsed } : null;
  }
  if (kind === '#comment') {
    if (!opts.strictComments) return null;
    return { type: 'comment', value: node.data.trim() };
  }
  if (!node.tagName) return null; // document / doctype 等節點，比對範圍用不到

  // 類別 4：<style>／<script> 整個節點（含子孫）從正規化後的樹裡排除，讓兩側的手足
  // 索引重新緊縮對齊。見檔頭「類別 4」說明——這裡不是「容忍標籤名稱不符」，是連節點
  // 本身都不進入比對樹。
  const tagLower = node.tagName.toLowerCase();
  if (tagLower === 'style' || tagLower === 'script') return null;

  const attrs = (node.attrs || [])
    .filter((a) => {
      if (/^data-v-/i.test(a.name)) {
        foundDataV.count++;
        return false;
      }
      return true;
    })
    .map((a) => {
      const name = a.name.toLowerCase();
      const value = name === 'href' || name === 'src' ? resolveHrefValue(a.value, opts.routeDir) : a.value;
      return [name, value];
    })
    .sort((a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0));
  const children = (node.childNodes || [])
    .map((c) => normalize(c, opts, foundDataV))
    .filter(Boolean);
  return { type: 'element', tag: tagLower, attrs, children };
}

function extractMainNormalized(html, opts, foundDataV) {
  const doc = parse5.parse(html, { sourceCodeLocationInfo: false, scriptingEnabled: false });
  const mainNode = findMain(doc);
  if (mainNode) return normalize(mainNode, opts, foundDataV);
  return null; // 呼叫端決定 fallback
}

function pathLabel(path) {
  return path.length ? path.join(' > ') : '(根)';
}

// 深度比對兩棵正規化後的樹，回傳差異清單。
// ctx.allowSelectedDefault：類別 6(b) 的旗標，只有「<select> 的第一個子節點」在遞迴時
// 會是 true，其餘一律 false——見檔頭類別 6 的範圍邊界說明。
function diffTrees(expected, actual, path, diffs, maxDiffs, ctx = { allowSelectedDefault: false }) {
  if (diffs.length >= maxDiffs) return;
  if (!expected && !actual) return;
  if (!expected || !actual) {
    diffs.push({ path: pathLabel(path), kind: '節點存在與否', expected: expected ? summarize(expected) : '（不存在）', actual: actual ? summarize(actual) : '（不存在）' });
    return;
  }
  if (expected.type !== actual.type) {
    diffs.push({ path: pathLabel(path), kind: '節點類型', expected: expected.type, actual: actual.type });
    return;
  }
  if (expected.type === 'text' || expected.type === 'comment') {
    if (expected.value !== actual.value) {
      diffs.push({ path: pathLabel(path), kind: `${expected.type === 'text' ? '文字內容' : '註解內容'}`, expected: expected.value, actual: actual.value });
    }
    return;
  }
  // element
  if (expected.tag !== actual.tag) {
    diffs.push({ path: pathLabel(path), kind: '標籤名稱', expected: expected.tag, actual: actual.tag });
    return; // 標籤都不同了，往下比子節點沒有意義
  }
  const curPath = [...path, `${expected.tag}[${(path._siblingIndex ?? 0)}]`];
  // 屬性比對
  const eAttrs = new Map(expected.attrs);
  const aAttrs = new Map(actual.attrs);
  const allNames = new Set([...eAttrs.keys(), ...aAttrs.keys()]);
  for (const name of allNames) {
    if (diffs.length >= maxDiffs) break;
    const ev = eAttrs.get(name);
    const av = aAttrs.get(name);

    // 類別 5：style 屬性解析成宣告陣列再比對，不是字串比對。
    if (name === 'style') {
      const eDecls = parseStyleDeclarations(ev);
      const aDecls = parseStyleDeclarations(av);
      if (!styleDeclarationsEqual(eDecls, aDecls)) {
        diffs.push({
          path: pathLabel(curPath),
          kind: '屬性 style（宣告內容不同，非結尾分號差異）',
          expected: ev !== undefined ? ev : '（沒有 style）',
          actual: av !== undefined ? av : '（沒有 style）',
        });
      }
      continue;
    }

    // 類別 6(a)：value="" 是 Vue 對 v-model 空值 input 的顯式輸出，expected 完全沒有
    // 這個屬性、actual 剛好是空字串時視為無差異。
    if (name === 'value' && ev === undefined && av === '') continue;

    // 類別 6(b)：selected 落在 <select> 第一個 <option> 且 actual 印出空字串時視為無差異。
    if (name === 'selected' && ctx.allowSelectedDefault && ev === undefined && av === '') continue;

    if (ev === undefined) {
      diffs.push({ path: pathLabel(curPath), kind: '屬性', expected: `（沒有 ${name}）`, actual: `${name}="${av}"` });
    } else if (av === undefined) {
      diffs.push({ path: pathLabel(curPath), kind: '屬性', expected: `${name}="${ev}"`, actual: `（沒有 ${name}）` });
    } else if (ev !== av) {
      diffs.push({ path: pathLabel(curPath), kind: `屬性 ${name}`, expected: ev, actual: av });
    }
  }
  // 子節點比對
  const n = Math.max(expected.children.length, actual.children.length);
  if (expected.children.length !== actual.children.length) {
    diffs.push({
      path: pathLabel(curPath),
      kind: '子節點數量',
      expected: `${expected.children.length} 個：${expected.children.map(summarize).join('、') || '（無）'}`,
      actual: `${actual.children.length} 個：${actual.children.map(summarize).join('、') || '（無）'}`,
    });
  }
  for (let i = 0; i < n && diffs.length < maxDiffs; i++) {
    const childPath = Object.assign([...curPath], { _siblingIndex: i });
    const childCtx = { allowSelectedDefault: expected.tag === 'select' && i === 0 };
    diffTrees(expected.children[i] ?? null, actual.children[i] ?? null, childPath, diffs, maxDiffs, childCtx);
  }
}

function summarize(node) {
  if (!node) return '（無）';
  if (node.type === 'text') return `文字「${node.value.slice(0, 20)}${node.value.length > 20 ? '…' : ''}」`;
  if (node.type === 'comment') return `註解「${node.value.slice(0, 20)}」`;
  return `<${node.tag}>`;
}

async function readExpected(file) {
  return readFile(file, 'utf8');
}

async function readActual(actualSpec, relPath, isUrl) {
  if (isUrl) {
    const url = actualSpec.replace(/\/$/, '') + toRoutePath(relPath);
    const res = await fetch(url);
    if (!res.ok) throw new Error(`HTTP ${res.status} ${url}`);
    return await res.text();
  }
  // 檔案或目錄：與 expected 用同一個相對路徑對應（例如另一份靜態輸出目錄，或
  // 未來 Nuxt generate 產出的 .output/public，兩者都保留 "zh/about/index.html" 這種
  // 檔案路徑結構，不是 URL route，所以這裡刻意不套用 toRoutePath()）。
  const stats = await stat(actualSpec);
  const target = stats.isDirectory() ? join(actualSpec, relPath) : actualSpec;
  return readFile(target, 'utf8');
}

// 站台根路徑「/」的特例斷言（見檔頭說明）：不比對內容，改斷言 302 且 Location 指向 /zh/。
// redirect: 'manual' 是關鍵——Node 的 fetch()（undici）在這個模式下會把重定向回應本身
// 原樣交回來（status 真的是 302、headers 真的帶 Location），不會像瀏覽器 fetch 一樣把
// 狀態蓋成不透明的 0，也不會（用預設的 'follow'）直接幫你追過去，兩者都會讓這裡斷言不到
// 真正的重定向行為（已用 curl -D - 與 node --eval 實測核對 127.0.0.1:3011 確認）。
async function checkRootRedirect(actualSpec, relPath) {
  const url = actualSpec.replace(/\/$/, '') + '/';
  let res;
  try {
    res = await fetch(url, { redirect: 'manual' });
  } catch (e) {
    return { relPath, ok: false, fetchError: e.message, diffs: [] };
  }
  const location = res.headers.get('location');
  const isRedirect = res.status >= 300 && res.status < 400;
  const locationOk = !!location && new URL(location, url).pathname === '/zh/';
  if (isRedirect && locationOk) {
    return { relPath, ok: true, diffs: [], rootRedirectCheck: true };
  }
  return {
    relPath,
    ok: false,
    diffs: [],
    rootRedirectCheck: true,
    structuralError:
      `根路徑「/」預期回 302 且 Location 指向 /zh/，實際 status=${res.status}` +
      ` location=${location ?? '(無)'}`,
  };
}

async function compareOne(expectedFile, actualSpec, relPath, isUrl, opts) {
  const routePath = toRoutePath(relPath);
  if (routePath === '/' && isUrl) {
    return await checkRootRedirect(actualSpec, relPath);
  }

  const expectedHtml = await readExpected(expectedFile);
  let actualHtml;
  try {
    actualHtml = await readActual(actualSpec, relPath, isUrl);
  } catch (e) {
    return { relPath, ok: false, fetchError: e.message, diffs: [] };
  }

  const pageOpts = { ...opts, routeDir: routePath };
  const foundDataV = { count: 0 };
  const expectedNorm = extractMainNormalized(expectedHtml, pageOpts, foundDataV);
  const actualNorm = extractMainNormalized(actualHtml, pageOpts, foundDataV);

  if (!expectedNorm) return { relPath, ok: false, structuralError: 'expected 找不到 <main id="main">', diffs: [] };
  if (!actualNorm) return { relPath, ok: false, structuralError: 'actual 找不到 <main id="main">', diffs: [] };

  const diffs = [];
  diffTrees(expectedNorm, actualNorm, [], diffs, opts.maxDiffs);
  return { relPath, ok: diffs.length === 0, diffs, dataVCount: foundDataV.count };
}

// 單檔模式下，把使用者傳入的 --expected 路徑轉成「相對於 dist 根目錄」的 relPath，
// 跟批次模式（relative(expectedDir, file)）用同一套語意，這樣 toRoutePath() 才會算出
// 正確的 route（例如 "dist/zh/about/index.html" → "zh/about/index.html" → "/zh/about/"，
// 不會被誤判成站台根目錄 "/"）。找不到 dist/ 片段就退而求其次，直接用去掉開頭 ./ 的
// 原始字串——使用者若指定的是其他目錄結構，仍需自行確保傳入的是「相對於站台根目錄」的路徑。
function singleFileRelPath(rawExpectedArg, expectedAbsPath) {
  const segments = expectedAbsPath.split(sep);
  const distIdx = segments.lastIndexOf('dist');
  if (distIdx >= 0) return segments.slice(distIdx + 1).join('/');
  return rawExpectedArg.replace(/^\.\/+/, '');
}

async function main() {
  // 🔴 退役清單的結構性驗證放在最前面，不管這次要比對哪些頁面都先做——
  // 清單本身資料不完整就直接拒絕執行，見 validateRetiredRoutes() 與檔頭「退役頁面清單」一節。
  validateRetiredRoutes(RETIRED_ROUTES);

  const opts = parseArgs(process.argv.slice(2));
  if (!opts.expected || !opts.actual) {
    console.error('用法：node tools/compare-dom.mjs --expected <路徑> --actual <路徑或URL> [--json] [--limit N] [--max-diffs N] [--strict-comments]');
    process.exit(1);
  }
  const expectedPath = resolve(process.cwd(), opts.expected);
  if (!existsSync(expectedPath)) {
    console.error(`--expected 路徑不存在：${expectedPath}`);
    process.exit(1);
  }
  const isUrl = /^https?:\/\//.test(opts.actual);
  const actualSpec = isUrl ? opts.actual : resolve(process.cwd(), opts.actual);

  const expectedStat = await stat(expectedPath);
  let pairs;
  if (expectedStat.isDirectory()) {
    let files = await walkHtml(expectedPath);
    files.sort();
    if (opts.limit) files = files.slice(0, opts.limit);
    pairs = files.map((f) => ({ file: f, relPath: relative(expectedPath, f) }));
  } else {
    pairs = [{ file: expectedPath, relPath: singleFileRelPath(opts.expected, expectedPath) }];
  }

  const results = [];
  for (const { file, relPath } of pairs) {
    // 退役頁面：不進 compareOne()，不對 site/dist 做任何內容比對——這一頁的「正確答案」
    // 不再是 site/dist，比較它只會製造假警報。見檔頭「退役頁面清單」一節與 RETIRED_ROUTES。
    const route = toRoutePath(relPath);
    const retiredEntry = RETIRED_ROUTES.get(route);
    if (retiredEntry) {
      results.push({ relPath, route, retired: true, why: retiredEntry.why, coveredBy: retiredEntry.covered_by });
      continue;
    }
    results.push(await compareOne(file, actualSpec, relPath, isUrl, opts));
  }

  // 🔴 三個數字分開算：retired 不計入 passed 或 failed，避免「全部通過」的訊息
  // 悄悄蓋過「其實有頁面被退役、換了一種驗法」這件事。
  const retired = results.filter((r) => r.retired);
  const compared = results.filter((r) => !r.retired);
  const failed = compared.filter((r) => !r.ok);
  const passed = compared.length - failed.length;

  if (opts.json) {
    console.log(JSON.stringify({ total: results.length, passed, failed: failed.length, retired: retired.length, results }, null, 2));
    process.exit(failed.length ? 1 : 0);
  }

  console.log(`比對 ${compared.length} 頁（expected：${relative(process.cwd(), expectedPath) || '.'}／actual：${isUrl ? actualSpec : relative(process.cwd(), actualSpec) || '.'}）` + (retired.length ? `，另有 ${retired.length} 頁已退出 site/dist 基準（見下方清單，不計入本次比對）` : '') + '\n');

  if (retired.length) {
    console.log(`🔵 ${retired.length} 頁已退出 site/dist 基準（不計入通過或失敗，改由各自的 covered_by 檢查接手）：`);
    for (const r of retired) {
      console.log(`  - ${r.route}`);
      console.log(`      why        : ${r.why}`);
      console.log(`      covered_by : ${r.coveredBy}`);
    }
    console.log('');
  }

  const totalDataV = compared.reduce((n, r) => n + (r.dataVCount || 0), 0);
  if (totalDataV) {
    console.log(`⚠️ 共偵測到 ${totalDataV} 個 data-v-* 屬性（已排除在比對之外）——請另外確認搬移的 <style> 沒有被加上 scoped（紀律 10，docs/13 §6）\n`);
  }

  for (const r of compared) {
    if (r.ok) continue;
    console.log(`  ✗ ${r.relPath}`);
    if (r.fetchError) {
      console.log(`      抓取失敗：${r.fetchError}`);
      continue;
    }
    if (r.structuralError) {
      console.log(`      ${r.structuralError}`);
      continue;
    }
    for (const d of r.diffs) {
      console.log(`      [${d.kind}] ${d.path}`);
      console.log(`        expected: ${d.expected}`);
      console.log(`        actual  : ${d.actual}`);
    }
  }

  if (failed.length) {
    console.log(`\n共 ${failed.length}/${compared.length} 頁有差異（${passed} 頁乾淨）`);
  } else {
    console.log(`✓ 列入比對的 ${compared.length} 頁 <main> 內容零差異`);
  }
  // 🔴 requirement 4：退役頁數不為 0 時，結尾摘要必須明講，不能只印「全部通過」，
  // 否則退役會偽裝成乾淨——即使 compared 全過，也要讓人一眼看到「這不是真的 80/80」。
  if (retired.length) {
    console.log(`🔵 另有 ${retired.length} 頁已退出 site/dist 基準（不計入上面的通過或失敗數字，清單見上方）`);
  }

  process.exit(failed.length ? 1 : 0);
}

main().catch((e) => {
  console.error('比對失敗：', e.stack ?? e.message);
  process.exit(1);
});
