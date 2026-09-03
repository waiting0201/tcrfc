# 06 — 慣例與術語

> 執行層文件。術語表源自規劃書；設計 tokens 源自 [`../mockup/index.html`](../mockup/index.html)。
> **品牌色的唯一真實來源是 [`../reference/TCR_logo_CMYK.ai`](../reference/TCR_logo_CMYK.ai)**，衍生資產與說明見 [`../brand/README.md`](../brand/README.md)。

---

## 1. 術語對照表

寫任何文案、命名、程式碼前先對一次，避免同一件事有三種叫法。

| 概念 | 中文 | English | 代號／識別 | 備註 |
|---|---|---|---|---|
| 俱樂部 | 台中磐石足球俱樂部 | Taichung Cornerstone RFC | TCRFC | 2024 創立。中文簡稱一律「**台中磐石**」，不單用「磐石」 |
| 品牌主張 | 在地扎根 · 放眼世界 | LOCAL ROOTS. GLOBAL PATHWAYS. | | |
| 品牌標語 | — | TAICHUNG ROCKS FOOTBALL CLUB | | 出自 Brand Deck 頁尾，**是標語不是正式英文名**；正式名一律用 `Taichung Cornerstone RFC` |
| 一線隊 | 一線隊 | First Team | **`D1`** | 對外顯示用「一線隊／First Team」，`D1` 僅作代號 |
| 學院梯隊 | U15／U14／U12 梯隊 | Academy Teams | `U15`/`U14`/`U12` | 日後可增 U18／U10 |
| 學院 | 台中磐石足球學院 | TCRFC Academy | | |
| 女子足球 | 台中藍鯨女子隊 | Women's Football | — | **不建球隊資料**，單頁導流至 [`https://www.tcbw2014.com/`](https://www.tcbw2014.com/) |
| 課程與活動 | 課程與活動 | Programs | 後台 `P1–P4` | 原編 `D1–D4`，已因撞名改號 |
| 慈善 | 慈善與社會影響 | Charity & Impact | | |
| 球迷會 | 台中磐石球迷會 | Fan Club | `fan_club` | 是會員層級，不是獨立名單 |
| 漫畫 | 台中磐石漫畫 | TCRFC Manga / Comics | | 全部免費公開 |

### 名稱與標誌的硬性規則

1. **中文簡稱一律「台中磐石」。** 站上任何位置都不單獨出現「磐石」——`關於台中磐石`、`台中磐石文化`、`台中磐石足球學院`、`台中磐石球迷會`、`台中磐石漫畫` 皆同。
2. **標誌只用 logo 主檔萃取的三種組合**（見 [`../brand/README.md`](../brand/README.md)）：
   - `tcrfc-mark-*.svg` 隊徽
   - `tcrfc-stacked-*.svg` 隊徽＋`TCRFC`（**英文版**）
   - `tcrfc-full-*.svg` 隊徽＋`TCRFC`＋`台中磐石足球俱樂部`（**中文版**）
3. **不得在標誌旁自行排字**（含「台中磐石」文字 wordmark），**不得加註 `SINCE` 或創立年份**。
   完整中文版縮小後中文會糊掉，因此 header 需要足夠高度（桌機 112px、捲動後縮為隊徽、≤640px 直接用隊徽）。
4. `TAICHUNG ROCKS FOOTBALL CLUB` 是標語，不是正式英文名。

**五大核心價值**（同時是全站內容標籤）
`Players First 以球員為本`｜`Excellence 追求卓越`｜`Global Pathways 國際發展`｜`Community 社區共好`｜`Integrity 誠信專業`

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
| 賽事時區 | 依瀏覽者當地時區顯示，並標註「時間可能異動」 | |
| 賽事網址 | `/schedule/YYYY-MM-DD-tcrfc-vs-對手` | |
| 隊別網址 | `/schedule/{code小寫}/` | `/schedule/u15/` |
| 金額 | `NT$` + 千分位 | `NT$1,000` |
| 球員標示 | 背號 + 姓名 | `09 劉○○` |

---

## 4. 檔名慣例

**媒體資產**（沿用 [`../mockup/assets/`](../mockup/assets/) 既有規則）：
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
