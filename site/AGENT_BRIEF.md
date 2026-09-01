# TCRFC 網站頁面建置規範

給負責建頁面的 agent。**動工前整份讀完。**

---

## 0. 最重要的一條：不准杜撰

這是要交給真實客戶的商業網站。**任何你不知道的事實，一律不准編。**

具體禁止：
- ❌ 編造球員生涯簡介、身高體重、出生地、前東家、進球數
- ❌ 編造教練資歷、證照、年資
- ❌ 編造課程費用、名額、開課日期、報名截止日
- ❌ 編造贊助商名稱、合作夥伴、金額
- ❌ 編造獎項全名、賽事成績、統計數字
- ❌ 編造引言、球員／教練說的話
- ❌ 用 Lorem ipsum 或「這裡是介紹文字」這種佔位廢話

**正確做法**：內容缺的地方放 `.pending` 標記，並寫出資料要去哪裡拿。

```html
<div class="pending">
  球員簡介待補 —— 來源 <code>TCRFC_資料收件夾/03_FOOTBALL_CLUB.../02_球員名單/9_劉建緯/</code>
  （目前資料夾為空，且文稿為 Google Docs 捷徑，尚未取回）
</div>
```

可以寫的「非事實」文案：**版型結構性的引導文字**，例如區塊標題、欄位標籤、按鈕文字、
表格表頭、篩選器選項、無障礙標籤。這些屬於介面文案，不是客戶內容。

---

## 1. 你在做什麼

台中磐石足球俱樂部（TCRFC）官方網站，靜態 HTML，延續已完成的視覺 mockup。

- **品牌主張**：LOCAL ROOTS. GLOBAL PATHWAYS.｜在地扎根 · 放眼世界
- **規格唯一真實來源**：`output/TCRFC_前後台功能規劃書.md`（v1.8）
- **頁面規格摘要**：`docs/02-frontend-spec.md`（有對應規劃書行號，需要細節時回讀原文）
- **視覺基準**：`mockup/index.html`（1327 行，design tokens 已抽到共用 CSS）

---

## 2. 檔案怎麼放

頁面原始檔放 `site/src/pages/zh/<路徑>/index.html`，建置後輸出到 `site/dist/`。

```
site/
├── build.mjs                 建置腳本（零相依，勿改）
├── src/
│   ├── partials/             shell / header / footer（共用，勿改）
│   ├── assets/css/tcrfc.css  全站樣式（勿改；要加樣式時見 §6）
│   ├── assets/js/site.js     全站 JS
│   ├── assets/img/           圖片
│   ├── data/*.json           真實資料，見 §4
│   └── pages/zh/…            ← 你寫這裡
└── dist/                     ← 建置產物，別手改
```

建置：`node site/build.mjs`。**寫完一定要跑一次確認沒錯誤。**

---

## 3. 頁面骨架

每個頁面檔開頭必須有 `<!--meta … -->` JSON 區塊，接著只寫 `<main>` 裡面的內容
（`<html>` / `<head>` / header / footer 由 shell 自動包）。

```html
<!--meta
{
  "title": "我們的故事 Our Story｜關於磐石｜台中磐石足球俱樂部",
  "description": "一句話說明這頁在講什麼，60–150 字，給搜尋引擎看。",
  "nav": "about",
  "unit": "2.1"
}
-->
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="{{ROOT}}/zh/">首頁</a></li>
      <li><a href="{{ROOT}}/zh/about/">關於磐石</a></li>
      <li aria-current="page">我們的故事</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="{{ROOT}}/assets/img/nav-about.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">2.1 About TCRFC</p>
    <h1>我們的故事<span class="en">Our Story</span></h1>
    <p class="page-hero__lede">（這裡可寫結構性導言；若需引用客戶原文則放 .pending）</p>
  </div>
</section>

<section class="band">
  <div class="container">
    …
  </div>
</section>
```

`{{ROOT}}` 是站根相對路徑，建置時自動代換。**所有站內連結與資產路徑都要用它。**

`nav` 欄位可用值（決定導覽列高亮）：
`home` `about` `club` `academy` `programs` `womens` `schedule` `news` `culture` `partners` `charity`

---

## 4. 有哪些真實資料可以用

`site/src/data/` 底下是我從客戶收件夾抽出來的**真實**資料，請直接讀取套用：

| 檔案 | 內容 | 備註 |
|---|---|---|
| `players.json` | 一線隊 28 名球員：背號、位置、中文名、英文名 | `photo`/`bio_zh` 皆為 `null` —— 客戶尚未提供 |
| `staff.json` | 團隊成員 8 人：職稱、姓名 | `bio_zh` 為 `null` |
| `coaches-d1.json` | 一線隊教練團 4 人 | 同上 |
| `coaches-academy.json` | 學院教練 3 人 | 同上 |
| `news.json` | 83 篇新聞：日期、標題、分類、封面圖路徑 | `body_zh` 為 `null` —— 正文尚未取回 |
| `schedule.json` | 2026/27 企甲 21 場賽程：日期、時間、主客、對手、場地 | **完整可用** |

其他確定的事實：

- 成立 **2024** 年；**2024 全國乙級聯賽冠軍**
- 隊別代號 `D1`（＝ First Team 一線隊，全站僅一支）／`U15`／`U14`／`U12`
  → **對外顯示一律寫 `First Team / 一線隊`，不要寫 D1**
- 主場：**西屯足球場**
- 地址：台中市北屯區崇平路二段景谷巷 11 弄 41 號
- 社群：IG `@tcr_fc_2024`、FB `TCRFC2024`、YouTube `@TCRFC-2024`
- 女足官網：`https://www.tcbw2014.com/`（台中藍鯨，**新分頁開啟**）
- 五大核心價值：`Players First 以球員為本`｜`Excellence 追求卓越`｜`Global Pathways 國際發展`｜`Community 社區共好`｜`Integrity 誠信專業`

**不做的事**（規劃書明列）：站內電商／金流／購物車（導向 Shopify）、票務、女足名單與賽程。

---

## 5. 視覺語彙

共用 CSS 已載入，直接用既有 class，**不要另立一套**。三個標誌性手法（延續 mockup）：

1. **Programme pagination** —— `<span class="ghost-num" aria-hidden="true">02</span>`
   超大幽靈數字標記區塊，像賽事秩序冊在分頁。
2. **Kit-trim cut** —— 卡片切角 + 背後墨色三角。**直角為主，不要圓角、不要漸層。**
3. **Floodlight ink** —— 深色區塊用單一平塗墨色（`--ink` / `--ink-2`），無材質。

可用 class（詳見 `site/src/assets/css/tcrfc.css`）：
`container` `band` `band.grain` `ghost-num` `btn btn--primary/--dark/--light/--sm/--block`
（按鈕**一律實心**，沒有外框版——外框無法跟隨切角 clip-path）
`page-hero` `page-hero--media` `page-hero__eyebrow/__lede/__bg` `breadcrumb` `prose`
`grid grid--2/--3/--4` `pending` `visually-hidden`

**顏色只准用 CSS 變數**：`--brand`(#E0218A) `--brand-aa`(小字用) `--ink` `--paper` `--text` `--muted` `--rule`。
不要寫死色碼。

---

## 6. 需要新樣式時

先看 `tcrfc.css` 有沒有現成的。真的需要新元件，**加在你自己頁面的
`<style>` 區塊裡並註明用途**，不要改共用 CSS（會撞其他 agent）。
若某元件三頁以上都要用，在回報裡說明，我統一收進共用 CSS。

---

## 7. 品質要求

- **無障礙**：語意標籤、`alt`（裝飾圖 `alt=""`）、表單 `<label>`、可見焦點、對比達 AA
  （小字用 `--brand-aa`，不要用 `--brand`）
- **響應式**：手機優先，不得橫向捲動；表格與寬內容包 `overflow-x:auto`
- **圖片**：一律加 `width`/`height`，首屏以外加 `loading="lazy"`
- **SEO**：每頁唯一 `<h1>`；標題階層不跳級；`meta.description` 要寫實質內容
- **繁體中文**，用詞照 `docs/06-conventions.md`
- 中英混排時英文用 `<span class="en">`

---

## 8. 交件前自檢

1. `node site/build.mjs` 跑過沒錯誤
2. `grep -rn '{{' site/dist/` 沒有殘留未代換的 token
3. 每頁只有一個 `<h1>`
4. **再檢查一次沒有杜撰的事實**——這是最容易出事的地方
5. 回報：做了哪些頁、放了哪些 `.pending`、有沒有需要收進共用 CSS 的元件
