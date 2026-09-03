# output/tools — 交付物 PDF 產出

`output/` 的四份 PDF 都由這支腳本產生，**不要手動改 PDF**：改來源檔（`.md` / `.html`）再重跑。

```bash
npm i --prefix output/tools        # 首次或換機時安裝相依（只有 marked）
node output/tools/build-pdf.mjs    # 產出全部四份
```

只產指定項目：

```bash
node output/tools/build-pdf.mjs zh          # TCRFC_前後台功能規劃書.pdf
node output/tools/build-pdf.mjs en          # TCRFC_Website_Functional_Specification_EN.pdf
node output/tools/build-pdf.mjs mile-zh     # TCRFC_開發里程碑_Milestone.pdf
node output/tools/build-pdf.mjs mile-en     # TCRFC_Development_Milestones_EN.pdf
```

## 兩種來源

| 項目 | 來源 | 排版方式 |
|---|---|---|
| `zh` / `en` 規劃書 | `output/*.md` | Markdown → 品牌樣式 HTML → A4 直式，含封面、頁首頁尾與頁碼 |
| `mile-zh` / `mile-en` 里程碑 | `output/*.html` | 既有 HTML 交付物直接列印，A4 橫式單頁，尺寸由檔內 `@page` 決定 |

## 規劃書排版規則

- 封面由 H1 後面的 `> **文件版本** / **建立日期**` 自動帶入，正文從第一個 `##` 開始
- **每個 `##` 一級章節換頁**（第一個「目錄」除外）
- 目錄錨點採 GitHub 規則（轉小寫、去標點、空白轉 `-`、CJK 保留），
  所以 md 裡的 `[專案目標與範圍](#1-專案目標與範圍)` 才連得到 `## 1. 專案目標與範圍`
- 色彩取自 [`../../reference/TCR_logo_CMYK.ai`](../../reference/TCR_logo_CMYK.ai)：桃紅 `#E0218A`、品牌黑 `#231916`

## 相依

- **marked**（唯一 npm 相依，`node_modules/` 已 gitignore）
- **本機 Chrome**（路徑寫在腳本頂端的 `CHROME`）。
  CLI 的 `--print-to-pdf` 不能設定頁首頁尾樣板，所以走 DevTools Protocol；
  WebSocket 用 Node 內建的，不需要 puppeteer。

## 注意

改完 `.md` 若章節行數有變動，記得同步 [`../../docs/00-harness.md`](../../docs/00-harness.md) 的規劃書行號對照表。
