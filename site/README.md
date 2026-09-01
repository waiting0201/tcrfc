# TCRFC 官方網站

靜態 HTML，延續 [`../mockup/`](../mockup/) 的視覺系統。無框架、零 npm 相依。

## 指令

```bash
node site/build.mjs      # 建置 → site/dist/
npx wrangler pages deploy site/dist --project-name tcrfc-mockup --branch main
```

## 結構

| 路徑 | 說明 |
|---|---|
| `build.mjs` | 建置腳本。把 `src/pages/` 的頁面包進共用外殼，輸出純靜態 HTML |
| `src/partials/` | `shell.html`（頁面外殼）／`header.html`（含 Mega Menu）／`footer.html` |
| `src/assets/css/tcrfc.css` | 全站樣式。前 675 行由 mockup 抽出，其後為各頁面共用元件 |
| `src/assets/js/site.js` | Sticky header、行動選單、Mega Menu |
| `src/data/*.json` | 由客戶收件夾抽出的真實資料，見下表 |
| `src/pages/zh/**` | 頁面內容（只寫 `<main>` 內部） |
| `dist/` | 建置產物。**不要手改** |
| `AGENT_BRIEF.md` | 建頁面時的規範（含「不准杜撰」原則） |

## 頁面寫法

每個頁面檔開頭要有 `<!--meta {...} -->`，其餘只寫 `<main>` 內容。
站內連結與資產路徑一律用 `{{ROOT}}` 前綴，建置時代換成正確的相對路徑。

## 資料檔

| 檔案 | 內容 | 完整度 |
|---|---|---|
| `schedule.json` | 2026/27 企甲 21 場賽程 | ✅ 完整 |
| `news.json` | 83 篇新聞的日期／標題／分類／封面圖 | 🟡 **正文全缺** |
| `players.json` | 一線隊 28 名球員的背號／位置／姓名 | 🟡 缺照片與簡介 |
| `staff.json` `coaches-d1.json` `coaches-academy.json` | 團隊與教練名單 | 🟡 缺照片與簡介 |

## 內容現況

客戶文稿為 **Google Docs 捷徑檔**，本機讀不到內容（見
[`../docs/09-intake-inventory.md`](../docs/09-intake-inventory.md) §2）。
凡是缺內容的地方，頁面上都放了 `.pending` 標記並註明來源路徑。

**上線前必須把所有 `.pending` 清空**：

```bash
grep -rn 'class="pending"' site/src/pages/ | wc -l
```

## ⚠️ 肖像權：上線前的阻斷條件

`src/assets/img/` 內含**從客戶收件夾複製的真實照片**，其中
`programs/`（22 張）與 `academy/`（14 張）**含未成年學員**。

目前這是可接受的，因為：全站 `noindex`、尚未部署、供內部與客戶審稿用。

**但上線前必須逐項確認肖像權授權**（CLAUDE.md §7、規劃書 §8）：

- [ ] 未成年學員照片取得監護人同意
- [ ] 球員照片使用授權
- [ ] 夥伴 Logo 使用授權
- [ ] 移除未獲授權者，並回填替代素材

未完成前**不得移除 `noindex`**。`alt` 文字目前一律只描述場景、不寫個人姓名，
補內容時請維持這個原則。

## 待辦

- [ ] 取回 113 篇 Google Docs 文稿，填入各頁 `.pending`
- [ ] 英文版 `/en/`（規劃書列為 Phase 2，內容產出方式待客戶確認）
- [ ] 表單接後端（目前 `action` 留空）
- [ ] Turnstile sitekey 設定
- [ ] Shopify 商店網址（8.3 商品卡導流目標，客戶尚未提供）
- [ ] 上線時移除 `src/_headers` 的 `X-Robots-Tag` 與 `robots.txt` 的 `Disallow`
