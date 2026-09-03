# TCRFC 品牌資產

全部由 [`../reference/TCR_logo_CMYK.ai`](../reference/TCR_logo_CMYK.ai)（Illustrator 28.3，2024-07-11，文字已轉外框）**直接向量萃取**，未經重繪或描邊。
`.ai` 是唯一色彩與造型真實來源；本資料夾的檔案若與 `.ai` 衝突，以 `.ai` 為準並回頭重產。

---

## 1. 品牌色

| 用途 | 印刷 | 網頁 | 說明 |
|---|---|---|---|
| 品牌桃紅 | **PANTONE 225 C**／`C5 M90 Y0 K0` | `#E0218A` | logo 主檔中唯一的彩色 |
| 品牌黑 | `C0 M0 Y0 K100` | `#231916` | 暖調近黑，**不是**深藍 |
| 反白 | `C0 M0 Y0 K0` | `#FFFFFF` | 深底版本用 |

`.ai` 色票面板另存有 50 餘個 Illustrator 預設色庫色票，**未使用於畫面**，不屬於品牌色。

網頁完整 token 表（含 hover、AA 安全版、深淺階層）見 [`../docs/06-conventions.md`](../docs/06-conventions.md) §2，
實作基準在 [`../mockup/index.html`](../mockup/index.html) 的 `:root`。

### 無障礙注意事項

`#E0218A` 對白底是 **4.42:1** — 通過大字 AA（≥18.66px bold 或 ≥24px），但**未達小字 AA 的 4.5:1**。

小字情境（白底粉字、或粉底白字）一律改用 `--brand-aa` `#D61E83`（4.80:1）。
肉眼幾乎分辨不出差異，但可通過 WCAG AA。

---

## 2. 檔案清單

### `svg/` — 向量標誌

三種組合各四個檔：

| 檔名 | 內容 | viewBox | 比例 |
|---|---|---|---|
| `tcrfc-mark.svg` | 隊徽（飛鳥） | `0 0 426.94 445.07` | 0.959 |
| `tcrfc-stacked.svg` | 隊徽 + `TCRFC` | `0 0 554.8 609.15` | 0.911 |
| `tcrfc-full.svg` | 隊徽 + `TCRFC` + 台中磐石足球俱樂部 | `0 0 554.8 687.68` | 0.807 |

- 無後綴版本 = `fill="currentColor"`，**只在 inline SVG 或 `<use>` 情境下**才會跟著 CSS `color` 變色
- `-pink` / `-black` / `-white` 後綴 = 固定色，供 `<img src>`、`background-image`、email、第三方平台使用

> ⚠️ `<img src="tcrfc-mark.svg">` **不會**繼承 `currentColor`（外部 SVG 文件是獨立 context，會 fallback 成黑色）。用 `<img>` 就選固定色版本。

**標誌不是正方形**（隊徽 0.959，其餘更窄）。設定 `width`/`height` 請照比例，勿硬填相同數值，否則會變形。

### `icons/` — 網站圖示

粉底 `#E0218A` + 反白隊徽的實心方塊（硬邊直角，符合品牌造型語言）。

| 檔名 | 尺寸 | 用途 |
|---|---|---|
| `favicon.svg` | 64 viewBox | 現代瀏覽器首選，任意解析度銳利 |
| `favicon.ico` | 16／32／48 三解析度 | 舊版瀏覽器與書籤列 fallback |
| `favicon-16/32/48/96.png` | — | 組 `.ico` 的來源，亦可單獨引用 |
| `apple-touch-icon.png` | 180×180 | iOS 加入主畫面 |
| `icon-192.png`／`icon-512.png` | — | PWA manifest |
| `icon-maskable-192.png`／`icon-maskable-512.png` | — | Android maskable，隊徽已收在中央 80% 安全區內 |

> **16px 的已知限制**：隊徽三道翅膀線之間的留白在 16px 會糊成色塊。32px 以上清楚。
> 現代瀏覽器優先取 `favicon.svg` 或 32px，實務影響小；若要根治需請設計方提供 **16px 簡化版隊徽**（本專案不自行改動標誌造型）。

### `png/` — 透明底點陣

`tcrfc-mark-pink-512.png`、`tcrfc-mark-white-512.png`、`tcrfc-full-black-2048.png`、`tcrfc-full-white-2048.png`
供簡報、Word、社群工具等不吃 SVG 的場合使用。

### `social/` — 社群分享圖

`og-image.png`（1200×630）：品牌黑底 + 反白完整標誌 + 雙語主張，底部粉色色帶。
供 `og:image` / `twitter:image` 預設值使用；各單元日後可另做專屬圖覆蓋。

---

## 3. 引用範例

### `<head>`

```html
<link rel="icon" href="/favicon.ico" sizes="16x16 32x32 48x48">
<link rel="icon" href="/favicon.svg" type="image/svg+xml">
<link rel="apple-touch-icon" href="/apple-touch-icon.png">
<link rel="manifest" href="/site.webmanifest">
<meta name="theme-color" content="#E0218A">

<meta property="og:image" content="https://www.tcrfc.tw/og-image.png">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
```

### `site.webmanifest`

```json
{
  "name": "台中磐石足球俱樂部",
  "short_name": "TCRFC",
  "theme_color": "#E0218A",
  "background_color": "#231916",
  "display": "standalone",
  "icons": [
    { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/icons/icon-maskable-192.png", "sizes": "192x192", "type": "image/png", "purpose": "maskable" },
    { "src": "/icons/icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

`name` 的英文版請用 `Taichung Rock FC`（見 [`../docs/06-conventions.md`](../docs/06-conventions.md) §1 術語表）。

### 頁面內

```html
<!-- 淺底 -->
<img src="/brand/svg/tcrfc-mark-pink.svg" alt="台中磐石足球俱樂部隊徽" width="38" height="40">

<!-- 深底 -->
<img src="/brand/svg/tcrfc-full-white.svg" alt="台中磐石足球俱樂部" width="248" height="307">

<!-- 需要跟著主題變色：inline SVG -->
<span style="color:var(--brand)"><!-- 貼上 tcrfc-mark.svg 內容 --></span>
```

---

## 4. 重新產生

原始萃取流程（`.ai` → PDF 解析 → 路徑分群 → SVG → Chrome headless 光柵化）記錄於本檔。
若 `.ai` 更新，重跑流程即可；**不要**手工修改 `svg/` 內的路徑資料。
