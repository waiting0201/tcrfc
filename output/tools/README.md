# output/tools — 交付物 PDF 產出

`output/` 的十二份 PDF 都由這裡的腳本產生，**不要手動改 PDF**：改來源檔（`.md` / `.html`）再重跑。

> **PDF 與兩組產生的 HTML 不納版控**（見 `.gitignore`）——它們是產生物，母檔才是真實來源。
> 剛 clone 下來的專案沒有這些檔案，跑一次下面的指令就會全部出現。

```bash
npm i --prefix output/tools        # 首次或換機時安裝相依（只有 marked）
node output/tools/build-pdf.mjs    # 產出全部十份
```

只產指定項目：

```bash
node output/tools/build-pdf.mjs zh          # TCRFC_前後台功能規劃書.pdf
node output/tools/build-pdf.mjs en          # TCRFC_Website_Functional_Specification_EN.pdf
node output/tools/build-pdf.mjs mile-zh     # TCRFC_開發里程碑_Milestone.pdf
node output/tools/build-pdf.mjs mile-en     # TCRFC_開發里程碑（英文）
node output/tools/build-pdf.mjs charity-zh # TCRFC_慈善捐款平台功能規劃書.pdf
node output/tools/build-pdf.mjs charity-en # TCRFC_Charity_Donation_Platform_Specification_EN.pdf
node output/tools/build-pdf.mjs app-zh     # TCRFC_行動App功能規劃書.pdf
node output/tools/build-pdf.mjs app-en     # TCRFC_Mobile_App_Specification_EN.pdf
node output/tools/build-pdf.mjs brief-zh    # TCRFC_官網功能說明_客戶版.pdf
node output/tools/build-pdf.mjs bw-brief-zh # TCRFC_台中藍鯨官網功能說明_客戶版.pdf
node output/tools/build-pdf.mjs app-brief-zh  # TCRFC_行動App功能說明_客戶版.pdf（來源是 HTML，見下）
node output/tools/build-pdf.mjs app-brief-en  # TCRFC_Mobile_App_Feature_Overview_EN.pdf（同上）
node output/tools/build-pdf.mjs sitemap-zh # TCRFC_慈善捐款站台地圖.pdf
node output/tools/build-pdf.mjs sitemap-en # TCRFC_Charity_Donation_Sitemap_EN.pdf
```

## 兩份版面式文件的 HTML 母檔是產生的，不要手改

**慈善捐款站台地圖**與**行動 App 功能說明（客戶版）**都是版面式文件——不是 Markdown 轉排版，
而是由 Python 腳本直接產出 A4 直式 HTML（含手機示意、動線站卡片、表格），再轉 PDF：

```bash
python3 output/tools/build-sitemap.py                        # 慈善站台地圖中英兩份 HTML
node output/tools/build-pdf.mjs sitemap-zh sitemap-en        # 再轉 PDF

python3 output/tools/build-app-brief.py                      # App 客戶版中英兩份 HTML
node output/tools/build-pdf.mjs app-brief-zh app-brief-en    # 再轉 PDF
```

兩支腳本的結構相同：規格異動時改腳本裡的 `C['zh']` / `C['en']` 兩份資料，**改一邊就要改另一邊**；
直接改產出的 HTML 會在下次執行時被覆蓋。QR 是編譯期產生的示意圖案，不是真的可掃描碼。

> **`build-app-brief.py` 的內容真實來源是 [`../TCRFC_行動App功能規劃書.md`](../TCRFC_行動App功能規劃書.md)（v3.4）**，
> 與慈善站台地圖對規劃書的關係一致：腳本裡的文案是**為客戶改寫過的濃縮版**，不是規格本身。
> 規格異動時先改規劃書，再回頭同步腳本。
>
> **客戶版沒有 `.md` 母檔**——它不是 Markdown 轉排版，內容直接寫在本腳本的 `C['zh']` / `C['en']` 裡。
> （v1.4 時代曾有兩份 `.md` 客戶版，v2.0 改版面式文件後已於 2026-09-10 刪除。）

## 兩種來源

| 項目 | 來源 | 排版方式 |
|---|---|---|
| `zh` / `en` 規劃書 | `output/*.md` | Markdown → 品牌樣式 HTML → A4 直式，含封面、頁首頁尾與頁碼 |
| `charity-zh` / `charity-en` 慈善站規劃書 | `output/*.md` | 同上 |
| `app-zh` / `app-en` 行動 App 規劃書 | `output/*.md` | 同上 |
| `bw-zh` / `bw-en` 台中藍鯨官網規劃書 | `output/*.md` | 同上 |
| **`brief-zh` / `brief-en` 官網功能說明（客戶版）** | `output/*.md` | 同上。**純文字摘要，不畫示意圖** |
| **`bw-brief-zh` / `bw-brief-en` 藍鯨官網功能說明（客戶版）** | `output/*.md` | 同上 |
| `app-brief-zh` / `app-brief-en` 行動 App 功能說明（客戶版） | `build-app-brief.py` → `output/*.html` | HTML 直接列印，A4 直式多頁 |
| `mile-zh` / `mile-en` 里程碑 | `output/*.html` | 既有 HTML 交付物直接列印，A4 橫式單頁 |
| `sitemap-zh` / `sitemap-en` 站台地圖 | `build-sitemap.py` → `output/*.html` | HTML 直接列印，A4 直式多頁 |

方向與紙張由各項目的 `opts`（餵給 `Page.printToPDF`）與檔內 `@page` 共同決定。

## 規劃書排版規則

- 封面由 H1 後面的 `> **文件版本** / **建立日期**` 自動帶入，正文從第一個 `##` 開始
- **每個 `##` 一級章節換頁**（第一個「目錄」除外）
- 目錄錨點採 GitHub 規則（轉小寫、去標點、空白轉 `-`、CJK 保留），
  所以 md 裡的 `[專案目標與範圍](#1-專案目標與範圍)` 才連得到 `## 1. 專案目標與範圍`
- 色彩取自 [`../../reference/TCR_logo_CMYK.ai`](../../reference/TCR_logo_CMYK.ai)：桃紅 `#E0218A`、品牌黑 `#231916`
- 慈善捐款站台地圖沿用同一組用色（**說明文件的用色，不是慈善平台的最終視覺**）；但**協會標誌尚未提供，一律以虛線方框佔位，不得放上 TCRFC 標誌、也不得自行為協會造標**

## 相依

- **marked**（唯一 npm 相依，`node_modules/` 已 gitignore）
- **本機 Chrome**（路徑寫在腳本頂端的 `CHROME`）。
  CLI 的 `--print-to-pdf` 不能設定頁首頁尾樣板，所以走 DevTools Protocol；
  WebSocket 用 Node 內建的，不需要 puppeteer。

## 注意

改完 `.md` 若章節行數有變動，記得同步 [`../../docs/00-harness.md`](../../docs/00-harness.md) 的規劃書行號對照表。
