# output/tools — 交付物 PDF 產出

`output/` 的八份 PDF 都由這裡的腳本產生，**不要手動改 PDF**：改來源檔（`.md` / `.html`）再重跑。

```bash
npm i --prefix output/tools        # 首次或換機時安裝相依（只有 marked）
node output/tools/build-pdf.mjs    # 產出全部八份
```

只產指定項目：

```bash
node output/tools/build-pdf.mjs zh          # TCRFC_前後台功能規劃書.pdf
node output/tools/build-pdf.mjs en          # TCRFC_Website_Functional_Specification_EN.pdf
node output/tools/build-pdf.mjs mile-zh     # TCRFC_開發里程碑_Milestone.pdf
node output/tools/build-pdf.mjs mile-en     # TCRFC_開發里程碑（英文）
node output/tools/build-pdf.mjs charity-zh # TCRFC_慈善捐款平台功能規劃書.pdf
node output/tools/build-pdf.mjs charity-en # TCRFC_Charity_Donation_Platform_Specification_EN.pdf
node output/tools/build-pdf.mjs sitemap-zh # TCRFC_慈善捐款站台地圖.pdf
node output/tools/build-pdf.mjs sitemap-en # TCRFC_Charity_Donation_Sitemap_EN.pdf
```

## 站台地圖的 HTML 母檔是產生的，不要手改

慈善捐款站台地圖的兩份 HTML 由 [`build-sitemap.py`](build-sitemap.py) 產出，中英內容一對一：

```bash
python3 output/tools/build-sitemap.py                    # 產生兩份 HTML
node output/tools/build-pdf.mjs sitemap-zh sitemap-en    # 再轉 PDF
```

規格異動時改 `build-sitemap.py` 裡的 `C['zh']` / `C['en']` 兩份資料，**改一邊就要改另一邊**；
直接改產出的 HTML 會在下次執行時被覆蓋。桌卡上的 QR 是編譯期產生的示意圖案，不是真的可掃描碼。

## 兩種來源

| 項目 | 來源 | 排版方式 |
|---|---|---|
| `zh` / `en` 規劃書 | `output/*.md` | Markdown → 品牌樣式 HTML → A4 直式，含封面、頁首頁尾與頁碼 |
| `charity-zh` / `charity-en` 慈善站規劃書 | `output/*.md` | 同上 |
| `mile-zh` / `mile-en` 里程碑 | `output/*.html` | 既有 HTML 交付物直接列印，A4 橫式單頁 |
| `sitemap-zh` / `sitemap-en` 站台地圖 | `build-sitemap.py` → `output/*.html` | HTML 直接列印，A4 直式多頁 |

方向與紙張由各項目的 `opts`（餵給 `Page.printToPDF`）與檔內 `@page` 共同決定。

## 規劃書排版規則

- 封面由 H1 後面的 `> **文件版本** / **建立日期**` 自動帶入，正文從第一個 `##` 開始
- **每個 `##` 一級章節換頁**（第一個「目錄」除外）
- 目錄錨點採 GitHub 規則（轉小寫、去標點、空白轉 `-`、CJK 保留），
  所以 md 裡的 `[專案目標與範圍](#1-專案目標與範圍)` 才連得到 `## 1. 專案目標與範圍`
- 色彩取自 [`../../reference/TCR_logo_CMYK.ai`](../../reference/TCR_logo_CMYK.ai)：桃紅 `#E0218A`、品牌黑 `#231916`
- **例外**：慈善捐款站台地圖的主體是台灣足球策略發展協會，**不得沿用 TCRFC 品牌色與標誌**，協會品牌資產到位前一律以中性色與佔位方框呈現

## 相依

- **marked**（唯一 npm 相依，`node_modules/` 已 gitignore）
- **本機 Chrome**（路徑寫在腳本頂端的 `CHROME`）。
  CLI 的 `--print-to-pdf` 不能設定頁首頁尾樣板，所以走 DevTools Protocol；
  WebSocket 用 Node 內建的，不需要 puppeteer。

## 注意

改完 `.md` 若章節行數有變動，記得同步 [`../../docs/00-harness.md`](../../docs/00-harness.md) 的規劃書行號對照表。
