#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""產生 output/ 的行動 App 功能說明（客戶版）HTML 母檔（中英雙版，內容一對一）。

    python3 output/tools/build-app-brief.py                          # 產生兩份 HTML
    node output/tools/build-pdf.mjs app-brief-zh app-brief-en        # 再轉 PDF

內容的真實來源是 output/TCRFC_行動App功能規劃書.md（v3.2）；規格異動時改本檔的
C['zh'] / C['en'] 兩份資料，**不要直接改產出的 HTML**（會被下次覆蓋）。
中英內容一對一，改一邊就要改另一邊。

排版沿用 build-sitemap.py 的視覺語言（A4 直式、手機示意、動線站卡片），
但這份講的是 App 五個分頁的畫面，不是網站的頁面地圖。
"""
import io, os, base64

OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.dirname(OUT)


def _crest_tcrfc():
    """TCRFC 隊徽：brand/svg/ 的向量原檔，直接內嵌。"""
    with io.open(os.path.join(ROOT, 'brand', 'svg', 'tcrfc-mark-black.svg'),
                 encoding='utf-8') as f:
        svg = f.read()
    return svg.replace('<svg ', '<svg class="lg" ', 1)


def _crest_bw():
    """台中藍鯨隊徽：目前只有點陣主檔（brand/blue-whale/README.md 記來源與限制）。"""
    with open(os.path.join(ROOT, 'brand', 'blue-whale', 'bw-crest-256.png'), 'rb') as f:
        b64 = base64.b64encode(f.read()).decode('ascii')
    return ('<img class="lg bw" alt="台中藍鯨隊徽" src="data:image/png;base64,%s">' % b64)


CREST_TCRFC, CREST_BW = _crest_tcrfc(), _crest_bw()


# ── 決定性假 QR：三個定位點 + 種子雜訊，避免 PDF 依賴 JS ──────────────────
def qr_svg(px=29, seed=20260910):
    def rnd():
        nonlocal seed
        seed = (seed * 1664525 + 1013904223) % 4294967296
        return seed / 4294967296
    grid = [[1 if rnd() > 0.52 else 0 for _ in range(px)] for _ in range(px)]

    def clear(x0, y0, w, h):
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                if 0 <= x < px and 0 <= y < px:
                    grid[y][x] = 0

    def finder(ox, oy):
        clear(ox - 1, oy - 1, 9, 9)
        for y in range(oy, oy + 7):
            for x in range(ox, ox + 7):
                if 0 <= x < px and 0 <= y < px:
                    edge = x in (ox, ox + 6) or y in (oy, oy + 6)
                    core = ox + 2 <= x <= ox + 4 and oy + 2 <= y <= oy + 4
                    grid[y][x] = 1 if (edge or core) else 0

    finder(0, 0); finder(px - 7, 0); finder(0, px - 7)
    rects = ''.join(
        '<rect x="%d" y="%d" width="1" height="1"/>' % (x, y)
        for y in range(px) for x in range(px) if grid[y][x])
    return ('<svg class="qr" viewBox="0 0 %d %d" role="img" aria-label="QR placeholder">'
            '<rect width="%d" height="%d" fill="#fff"/><g fill="#231916">%s</g></svg>'
            % (px, px, px, px, rects))


QR = qr_svg()

# ── CSS（__FONT__ 由 build() 置換；本檔不用 % 格式化，故百分比不需跳脫）──────
CSS = """
@page{ size:A4; margin:14mm 13mm 13mm; }
*{ box-sizing:border-box; margin:0; padding:0; }
:root{
  --brand:#E0218A; --brand-aa:#D61E83; --ink:#231916;
  --text:#2E2422; --muted:#6E5F5C; --faint:#9A8B88;
  --line:#E4DBDC; --line-2:#EFE8E9; --wire:#E8E1E2; --wire-2:#D9D0D1;
  --soft:#FBE7F2; --soft-line:#F2C3DD; --ok:#1F7A5C; --ok-soft:#E3F3ED;
  --warn:#9A5B00; --warn-soft:#FBEEDC;
  --font:__FONT__;
}
html{ -webkit-print-color-adjust:exact; print-color-adjust:exact; }
body{ font-family:var(--font); color:var(--text); background:#fff;
      font-size:8.4pt; line-height:1.6; }
h1,h2,h3,h4{ color:var(--ink); line-height:1.25; }
b,strong{ font-weight:700; color:var(--ink); }

/* ── 報頭 ── */
.cover{ border-bottom:2.2px solid var(--ink); padding-bottom:5mm; margin-bottom:7mm; }
.crest{ display:flex; align-items:center; gap:2.4mm; margin-bottom:4mm; }
.crest .lg{ height:9mm; width:auto; flex:none; display:block; }
.crest .lg.bw{ height:10mm; }
.crest .x{ color:var(--faint); font-size:8pt; }
.crest .tx{ font-size:7.4pt; letter-spacing:.14em; color:var(--muted); text-transform:uppercase;
            margin-left:1.4mm; }
.cover h1{ font-size:20pt; font-weight:800; letter-spacing:-.01em; margin-bottom:3mm; }
.cover h1 em{ font-style:normal; color:var(--brand-aa); }
.stand{ font-size:9.4pt; color:var(--muted); line-height:1.7; max-width:150mm; }
.stamps{ display:flex; flex-wrap:wrap; gap:1.8mm; margin-top:4.5mm; }
.stamp{ font-size:7pt; letter-spacing:.02em; border:.6px solid var(--line);
        padding:1mm 2.4mm; border-radius:6mm; color:var(--muted); }
.stamp b{ font-weight:700; }

/* ── 區段 ── */
section{ margin-top:6mm; }
section.np{ break-before:page; margin-top:0; }
.sec-head{ margin-bottom:3.4mm; break-inside:avoid; break-after:avoid; }
.eyebrow{ font-size:6.8pt; letter-spacing:.2em; text-transform:uppercase;
          color:var(--brand-aa); font-weight:700; }
.eyebrow.q{ color:var(--faint); }
.sec-head h2{ font-size:13pt; font-weight:700; margin:1.6mm 0 1.6mm; letter-spacing:-.01em; }
.sec-head p{ color:var(--muted); font-size:8.3pt; max-width:150mm; }

/* ── 分頁地圖（五欄）── */
.tree{ border:.6px solid var(--line); border-radius:2mm; padding:5mm 4.4mm 4mm;
       display:grid; grid-template-columns:repeat(5,1fr); gap:4.4mm; break-inside:avoid; }
.tcol h4{ font-size:6.8pt; letter-spacing:.14em; text-transform:uppercase;
          color:var(--faint); font-weight:700; padding-bottom:1.8mm;
          border-bottom:.6px solid var(--line-2); margin-bottom:2.6mm;
          display:flex; align-items:center; gap:1.4mm; }
.tcol h4 i{ font-style:normal; width:4.4mm; height:4.4mm; border-radius:1.1mm;
            background:var(--ink); color:#fff; font-size:6pt; font-weight:700;
            display:flex; align-items:center; justify-content:center; flex:none; }
.tcol.spine h4{ color:var(--brand-aa); border-bottom-color:var(--soft-line); }
.tcol.spine h4 i{ background:var(--brand); }
.node{ padding:1.7mm 0; border-bottom:.5px dotted var(--line-2); }
.node:last-child{ border-bottom:0; }
.node .nm{ font-weight:700; font-size:7.8pt; color:var(--ink); }
.node .d{ font-size:7pt; color:var(--muted); margin-top:.7mm; line-height:1.5; }
.node .d b{ font-size:7pt; }

/* ── 動線站 ── */
.station{ border:.6px solid var(--line); border-radius:2mm; padding:4.4mm;
          margin-top:3.4mm; break-inside:avoid; }
.sh{ display:flex; gap:2.6mm; align-items:flex-start; padding-bottom:2.6mm;
     border-bottom:.6px solid var(--line-2); margin-bottom:2.8mm; }
.sh .step{ width:8mm; height:8mm; border-radius:2mm; background:var(--soft);
           border:.6px solid var(--soft-line); color:var(--brand-aa);
           font-weight:800; font-size:8.4pt; display:flex; align-items:center;
           justify-content:center; flex:none; }
.sh h3{ font-size:11pt; font-weight:700; }
.sh .u{ font-size:8pt; color:var(--muted); margin-top:.8mm; }
.sh .tag{ margin-left:auto; align-self:center; font-size:6.8pt; letter-spacing:.06em;
          border:.6px solid var(--line); border-radius:6mm; padding:.8mm 2.2mm;
          color:var(--muted); white-space:nowrap; }
.sh .tag.ext{ background:var(--warn-soft); border-color:transparent; color:var(--warn); }
.sh .tag.key{ background:var(--soft); border-color:var(--soft-line); color:var(--brand-aa);
              font-weight:700; }
.sh .tag.ok{ background:var(--ok-soft); border-color:transparent; color:var(--ok); }
.sb{ display:grid; grid-template-columns:62mm 1fr; gap:6.5mm; align-items:start; }
.lede{ color:var(--muted); margin-bottom:2.8mm; line-height:1.68; }
ul.keyed{ list-style:none; display:flex; flex-direction:column; gap:1.8mm; }
ul.keyed li{ display:grid; grid-template-columns:4.6mm 1fr; gap:2.2mm; align-items:start; }
ul.keyed .k{ width:4.6mm; height:4.6mm; border-radius:1.2mm; background:var(--wire);
             color:var(--muted); font-size:6.2pt; font-weight:700; display:flex;
             align-items:center; justify-content:center; margin-top:.5mm; flex:none; }
ul.keyed .sub{ display:block; color:var(--faint); font-size:7.6pt; line-height:1.6; }
.admin{ margin-top:3.6mm; padding-top:2.8mm; border-top:.5px dashed var(--line);
        font-size:7.6pt; color:var(--faint); line-height:1.65; }
.admin b{ color:var(--muted); font-size:7pt; letter-spacing:.06em; }

/* ── 手機示意 ── */
.phone{ border:.6px solid var(--wire-2); border-radius:4.4mm; padding:1.6mm; }
.screen{ border:.5px solid var(--line-2); border-radius:3.2mm; overflow:hidden; background:#FBF8F9; }
.sbar{ display:flex; align-items:center; gap:1.6mm; padding:1.4mm 2.4mm;
       border-bottom:.5px solid var(--line-2); font-size:5.8pt; color:var(--faint); }
.sbar .ub{ flex:1; height:1.1mm; border-radius:2mm; background:var(--wire); }
.sc{ padding:2mm; display:flex; flex-direction:column; gap:1.5mm; }
.blk{ border:.5px solid var(--wire-2); border-radius:1.5mm; padding:1.7mm; background:#fff; }
.blk.pl{ border-style:dashed; background:transparent; }
.blk.ad{ border-style:dashed; background:var(--line-2); }
.bh{ display:flex; align-items:center; gap:1.2mm; margin-bottom:1mm; }
.kk{ width:3mm; height:3mm; border-radius:.8mm; background:var(--ink); color:#fff;
     font-size:5pt; font-weight:700; display:flex; align-items:center;
     justify-content:center; flex:none; }
.bt{ font-size:7pt; font-weight:700; color:var(--ink); line-height:1.35; }
.bs{ font-size:6.3pt; color:var(--faint); line-height:1.5; }
.bars{ display:flex; flex-direction:column; gap:.8mm; margin-top:1.1mm; }
.bar{ height:.9mm; border-radius:2mm; background:var(--wire); }
.w90{ width:90%; } .w75{ width:75%; } .w60{ width:60%; } .w45{ width:45%; }
.img{ height:7.6mm; border-radius:1.2mm; margin-bottom:1.3mm;
      background:repeating-linear-gradient(135deg,var(--wire) 0 1.1mm,transparent 1.1mm 2.2mm),var(--line-2); }
.img.t{ height:10.4mm; } .img.s{ height:6mm; }
.row{ display:flex; gap:1.1mm; align-items:center; }
.chip{ border:.5px solid var(--wire-2); border-radius:1.2mm; padding:.9mm 0; flex:1;
       text-align:center; font-size:5.6pt; color:var(--muted); }
.chip.on{ background:var(--soft); border-color:var(--brand); color:var(--brand-aa); font-weight:700; }
.chip.dk{ background:var(--ink); border-color:var(--ink); color:#fff; font-weight:700; }
.fld{ border:.5px solid var(--wire-2); border-radius:1.2mm; padding:1.1mm 1.5mm;
      font-size:6.2pt; color:var(--faint); background:#fff; }
.cta{ background:var(--brand); color:#fff; border-radius:1.4mm; text-align:center;
      padding:1.5mm 1mm; font-size:6.6pt; font-weight:700; }
.cta.gh{ background:transparent; border:.6px solid var(--brand-aa); color:var(--brand-aa); }
.cta.dk{ background:var(--ink); color:#fff; }
.tiny{ font-size:5.8pt; color:var(--faint); }
.okb{ display:inline-flex; align-items:center; gap:1mm; background:var(--ok-soft);
      color:var(--ok); border-radius:6mm; padding:.8mm 2.2mm; font-size:6.2pt; font-weight:700; }
.tabbar{ display:flex; border-top:.5px solid var(--line-2); background:#fff; }
.tabbar span{ flex:1; text-align:center; font-size:5pt; color:var(--faint); padding:1.2mm 0; }
.tabbar span.on{ color:var(--brand-aa); font-weight:700; }

/* ── 賽事列 ── */
.mday{ font-size:5.8pt; font-weight:700; color:var(--faint); letter-spacing:.1em;
       padding:.6mm 0 .2mm; }
.mrow{ display:grid; grid-template-columns:7mm 1fr auto; gap:1.4mm; align-items:center;
       border:.5px solid var(--wire-2); border-radius:1.2mm; padding:1.2mm 1.4mm; background:#fff; }
.mrow .dt{ font-size:5.8pt; font-weight:700; color:var(--ink); text-align:center; line-height:1.25; }
.mrow .who{ font-size:6.2pt; font-weight:400; color:var(--muted); line-height:1.3; }
/* 我方球隊標粗以與對手區隔；整行不可都是粗體，否則 <b> 等於沒作用 */
.mrow .who b{ font-weight:800; color:var(--ink); }
.mrow .cmp{ font-size:5.4pt; color:var(--faint); margin-top:.3mm; }
.mrow .tm{ font-size:5.8pt; color:var(--muted); text-align:right; white-space:nowrap; }
.badge{ display:inline-block; font-size:4.8pt; font-weight:700; border-radius:2mm;
        padding:.2mm 1mm; margin-right:.8mm; vertical-align:.2mm; }
.badge.r{ background:var(--soft); color:var(--brand-aa); }
.badge.b{ background:#E6EEF6; color:#20537F; }

/* ── 會員卡 ── */
.card{ border:.6px solid var(--ink); border-radius:2mm; padding:2mm; background:#fff; }
.card .ch{ display:flex; align-items:center; gap:1.2mm; padding-bottom:1.4mm;
           border-bottom:.5px solid var(--line-2); margin-bottom:1.4mm; }
.card .cm{ height:4.4mm; min-width:6.6mm; border-radius:.8mm; border:.5px solid var(--ink);
           color:var(--ink); font-size:4.6pt; font-weight:800; display:flex; align-items:center;
           justify-content:center; padding:0 .8mm; flex:none; }
.card .cn{ font-size:5.4pt; letter-spacing:.1em; color:var(--faint); margin-left:auto; }
.card.bw{ border-color:#20537F; }
.card.bw .tierp{ background:#20537F; }
.cardwrap{ position:relative; }
.cardwrap .peek{ position:absolute; top:1.1mm; right:-1.1mm; bottom:1.1mm; width:2.4mm;
                 border:.6px solid var(--wire-2); border-left:0; border-radius:0 2mm 2mm 0;
                 background:#F0EAE8; }
.swipe{ display:flex; align-items:center; justify-content:center; gap:1.2mm; margin-top:1.4mm; }
.swipe .dot{ width:1.4mm; height:1.4mm; border-radius:50%; background:var(--wire-2); }
.swipe .dot.on{ background:var(--brand); }
.swipe .sl{ font-size:5.2pt; color:var(--faint); letter-spacing:.06em; }
.card .qrw{ text-align:center; }
.card .qr{ width:17mm; height:17mm; display:block; margin:0 auto 1.2mm; shape-rendering:crispEdges; }
.card .nm{ font-size:8pt; font-weight:800; color:var(--ink); }
.card .tierp{ display:inline-block; background:var(--brand); color:#fff; font-size:5.4pt;
              font-weight:700; border-radius:2mm; padding:.4mm 1.6mm; margin-top:.8mm; }
.card .meta{ font-size:5.4pt; color:var(--faint); margin-top:1.2mm; line-height:1.5;
             border-top:.5px dashed var(--line-2); padding-top:1.2mm; }

/* ── 表格 ── */
table{ border-collapse:collapse; width:100%; font-size:8.2pt; }
th,td{ padding:2.2mm 3mm; text-align:left; border-bottom:.5px solid var(--line-2);
       vertical-align:top; }
th{ font-size:6.8pt; letter-spacing:.14em; text-transform:uppercase; color:var(--faint);
    font-weight:700; background:var(--line-2); }
tr:last-child td{ border-bottom:0; }
td .n{ color:var(--brand-aa); font-weight:700; }
.tbl{ border:.6px solid var(--line); border-radius:2mm; overflow:hidden; break-inside:avoid; }
.tbl.tight th,.tbl.tight td{ padding:1.8mm 2.4mm; font-size:7.8pt; }
td.c{ text-align:center; }
.yes{ color:var(--ok); font-weight:700; }
.no{ color:var(--faint); }
.part{ color:var(--warn); font-weight:700; font-size:7pt; }

/* ── 支援畫面 ── */
.grid4{ display:grid; grid-template-columns:repeat(2,1fr); gap:4mm; }
.mini{ border:.6px solid var(--line); border-radius:2mm; padding:4mm;
       display:flex; flex-direction:column; gap:2.6mm; break-inside:avoid; }
.mini h4{ font-size:9.4pt; font-weight:700; }
.mini .phone{ width:36mm; }
.mini .sc{ padding:1.6mm; gap:1.2mm; }
.mini p{ font-size:8pt; color:var(--muted); line-height:1.65; }

/* ── 畫面總覽 ── */
.gal{ display:grid; grid-template-columns:repeat(4,1fr); gap:4.6mm 4mm; }
.gcell{ break-inside:avoid; display:flex; flex-direction:column; gap:1.6mm; }
.gcap{ display:flex; align-items:baseline; gap:1.4mm; }
.gcap .code{ font-size:6pt; font-weight:800; letter-spacing:.06em; color:#fff;
             background:var(--ink); border-radius:1mm; padding:.5mm 1.2mm; flex:none; }
.gcell.key .gcap .code{ background:var(--brand); }
.gcap .nm{ font-size:8pt; font-weight:700; color:var(--ink); }
.gnote{ font-size:6.8pt; color:var(--muted); line-height:1.55; }
.gnote b{ font-size:6.8pt; }
.gal .sc{ padding:1.5mm; gap:1.1mm; }
.gal .blk{ padding:1.3mm; }
.gal .bt{ font-size:6pt; } .gal .bs{ font-size:5.4pt; }
.gal .chip{ font-size:4.8pt; padding:.7mm 0; }
.gal .fld{ font-size:5.2pt; padding:.9mm 1.2mm; }
.gal .cta{ font-size:5.4pt; padding:1.1mm .6mm; }
.gal .tabbar span{ font-size:4.2pt; padding:1mm 0; }
.gal .card .qr{ width:12mm; height:12mm; }
/* 窄欄裡標頭放不下三個元素，讓說明字獨立成行而不是硬折 */
.gal .card .ch{ flex-wrap:wrap; }
.gal .card .cn{ margin-left:0; width:100%; font-size:4.8pt; padding-top:.6mm; }
.gal .card .nm{ font-size:6.4pt; }
.gal .mrow{ grid-template-columns:5mm 1fr auto; padding:1mm; }
.gal .mrow .who{ font-size:5.2pt; } .gal .mrow .cmp{ font-size:4.6pt; }
.gal .mrow .dt{ font-size:5.2pt; } .gal .mrow .tm{ font-size:5pt; }

/* 清單列 ── */
.lrow{ display:flex; align-items:center; gap:1.2mm; border:.5px solid var(--wire-2);
       border-radius:1.2mm; padding:1.1mm 1.4mm; background:#fff; }
.lrow .lt{ font-size:5.8pt; font-weight:700; color:var(--ink); }
.lrow .ls{ font-size:5pt; color:var(--faint); margin-top:.2mm; }
.lrow .ch{ margin-left:auto; color:var(--faint); font-size:5.6pt; flex:none; }
.pill{ margin-left:auto; width:5.4mm; height:2.8mm; border-radius:2mm; background:var(--wire);
       position:relative; flex:none; }
.pill.on{ background:var(--brand); }
.pill i{ position:absolute; top:.4mm; left:.4mm; width:2mm; height:2mm; border-radius:2mm;
         background:#fff; }
.pill.on i{ left:3mm; }
.kv{ display:grid; grid-template-columns:auto 1fr; gap:.5mm 1.4mm; font-size:5.4pt; }
.kv .k2{ color:var(--faint); white-space:nowrap; }
.kv .v2{ color:var(--ink); font-weight:700; }

/* ── 注意框 ── */
.note{ border:.6px solid var(--soft-line); background:var(--soft); border-radius:2mm;
       padding:3.4mm 4mm; margin-top:3.4mm; break-inside:avoid; }
.note h4{ font-size:9pt; font-weight:700; color:var(--brand-aa); margin-bottom:1.6mm; }
.note p{ font-size:8pt; color:var(--text); line-height:1.7; }

/* ── 頁尾 ── */
.colophon{ margin-top:7mm; padding-top:3.4mm; border-top:.6px solid var(--line);
           font-size:7pt; color:var(--faint); line-height:1.8; break-inside:avoid; }
"""

# ═══════════════════════════════════════════════════════════════════════════════
C = {}

C['zh'] = dict(
    lang='zh-Hant', chip='zh ▾',
    font='"PingFang TC","Noto Sans TC","Hiragino Sans","Helvetica Neue",Arial,sans-serif',
    doctitle='台中足球 App 功能說明（客戶版）',
    mark1='TCRFC',
    crest='台中磐石足球俱樂部　×　台中藍鯨女子隊',
    h1='一個 App，<em>兩支球隊</em>，各自一份會籍。',
    stand='這份文件把行動 App 攤開來看：使用者打開 App 之後會看到哪些畫面、每個畫面上放什麼、'
          '哪些內容由後台維護。畫面為<b>功能示意</b>，用來確認架構與流程是否正確，'
          '尚未進入視覺設計。',
    stamps=[('依據　', '行動 App 功能規劃書 v3.5'), ('分頁　', '5 個　×　畫面 23 個'),
            ('語言　', '繁中／英文雙語'), ('後台模組　', 'M1–M5　＋　E4–E6（共用官網後台）')],

    eyebrow1='App map', h2_1='五個分頁：功能切分，不是球隊切分',
    p1='底部固定五個分頁，任何功能最多點三層就到得了。<b>沒有「磐石分頁」與「藍鯨分頁」</b>——'
       '分頁照功能切，球隊是每個功能裡的一個篩選。做成兩個平行分頁的話，每個功能都要做兩次，'
       '而且只追一隊的人會看到一半空白的畫面。中間那一欄是<b>使用頻率最高的一頁</b>。',
    tabs=[
        ('1', '首頁', False, [
            ('下一場比賽', '依你追蹤的球隊決定顯示誰，帶倒數與球隊標示。'),
            ('最新消息', '橫向捲動卡片，三則。'),
            ('附近店家', '已授權定位時依距離排序。'),
            ('廣告版位', '自售版位，必須標示「廣告」。'),
            ('贊助商 Logo 牆', '<b>不計曝光、不進廣告報表</b>。')]),
        ('2', '賽程', True, [
            ('兩隊全部球隊', '磐石一線隊、<b>藍鯨一線隊</b>、U15／U14／U12，也可看「全部」。'),
            ('12 個月完整賽程', '未來一整年、聯賽與盃賽都在裡面，依月份分組。'),
            ('依賽事名稱篩選', '企甲、各項盃賽可單獨篩出。'),
            ('單場詳情', '場地導航、加入行事曆、開賽提醒。'),
            ('離線可看', '下載過就能離線查閱。')]),
        ('3', '會員', False, [
            ('電子會員卡', '<b>沒訊號也能出示</b>，<b>每份會籍一張</b>，左右滑動切換。'),
            ('會籍狀態', '<b>逐俱樂部列出</b>層級、會員編號、有效期限。'),
            ('我的報名', '課程與營隊的報名紀錄。'),
            ('球衣登記', '尺寸、領取方式、寄送狀態，<b>依會籍分別登記</b>。'),
            ('抽獎資格', '<b>逐俱樂部</b>顯示有或沒有，不做任何操作。')]),
        ('4', '新聞', False, [
            ('分類列表', '沿用官網現有八個分類，不新增。'),
            ('來源球隊標示', '標示是磐石還是藍鯨的消息。'),
            ('依追蹤過濾', '預設只顯示你追蹤的球隊，可切換全部。'),
            ('離線閱讀', '開過的文章保留 30 天。')]),
        ('5', '更多', False, [
            ('兩隊球隊名單', '先分俱樂部兩區，區內再列球隊。'),
            ('特約店家', '附近地圖、導航、撥號。'),
            ('課程報名', '表單自動帶入會員資料。'),
            ('夥伴與贊助商', '兩隊分區呈現，不混列。'),
            ('慈善／商店', '皆為<b>外部連結</b>，不在 App 內完成。'),
            ('追蹤設定・常見問題・設定', '隨時可改追蹤的球隊。')]),
    ],

    eyebrow2='User journey', h2_2='使用動線：使用者實際會看到的畫面',
    p2='以下依順序走過一次。畫面中的編號對應右側的說明條列。畫面內的隊名、對手、店名與金額皆為示意，'
       '實際內容由後台維護。',

    st00=dict(h='第一次打開 App', u='只出現一次，之後可在「更多」裡改', tag='可全部跳過',
              lede='雙隊 App 一開始就要問一件事：<b>你追誰？</b>不知道使用者追誰，首頁就沒辦法決定要顯示'
                   '哪一隊的下一場比賽。但這道問題<b>不能變成門檻</b>——所以兩步都能跳過，跳過就等於全部追蹤。',
              items=[('1', '先選語言', '預設跟隨手機語言。裝置語言不是繁中或英文時，預設繁中。'),
                     ('2', '再選追蹤對象', '可以多選、也可以直接選「全部追蹤」。<b>「全部追蹤」是明確選項，不是跳過的副作用。</b>'),
                     ('3', '最後才問推播', '先說明用途再請求權限，不在啟動時就跳系統視窗。'),
                     ('!', '不設登入牆', '整段引導在未登入狀態就能完成，偏好先存在手機裡，登入後才與帳號同步。'),
                     ('!', '追蹤 ≠ 推播', '追蹤只決定「首頁看什麼」。想在首頁看到藍鯨、又不想被藍鯨推播打擾，是合法的組合。')],
              admin='<b>重要的分寸</b><br>追蹤<b>只影響預設值，不會把其他球隊藏起來</b>——追磐石的人一樣可以在賽程分頁切去看藍鯨。'
                    '追蹤是排序與預設，不是權限。<br><br><b>後台維護</b>　M2 App 內容編排'),

    st01=dict(h='首頁', u='每次打開 App 的第一畫面', tag='依追蹤偏好排列',
              lede='首頁<b>不做「磐石區塊」與「藍鯨區塊」的上下切分</b>——照內容類型排列，不照球隊切。'
                   '照球隊切的話，只追一隊的人會看到一半空白的首頁。',
              items=[('1', '下一場比賽', '倒數、對手、時間、場地，一按加入行事曆或導航。<b>卡片一定會標是哪一隊的比賽</b>，兩隊同時出現時沒有標示就分不出來。'),
                     ('2', '廣告版位', '自售版位，必須明顯標示「廣告」。載入失敗時顯示俱樂部自家備援內容，<b>版位永不空白</b>。'),
                     ('3', '最新消息', '三則橫向捲動卡片，帶來源球隊標示。'),
                     ('4', '會員卡快捷', '已登入且<b>至少一份會籍有效</b>時顯示，並標明是哪一隊的卡；未登入顯示「加入會員」。'),
                     ('5', '附近店家', '已授權定位時依距離排序，未授權時依後台排序。'),
                     ('6', '贊助商 Logo 牆', '<b>不計曝光、不進廣告報表</b>——那是對不出來的數字。')],
              admin='<b>未設定追蹤時怎麼辦</b><br>兩隊權重相同，各取一線隊最近一場，<b>不得預設偏向任一隊</b>。'
                    '<br><br><b>後台維護</b>　M2 首頁區塊開關與排序　／　E4–E6 廣告'),

    st02=dict(h='賽程：兩隊、12 個月、所有賽事', u='這是整個 App 的第一功能', tag='使用頻率最高',
              lede='兩隊所有球隊的賽程集中在一個地方，是讓人<b>每週</b>打開 App 的主要理由——'
                   '會員卡一季用幾次，賽程每週都在看。這件事現在沒有任何一個地方做得到：'
                   '磐石的賽程在磐石官網，藍鯨的在藍鯨官網，盃賽的散在各處。',
              items=[('1', '球隊分頁', '全部／磐石一線隊／藍鯨一線隊／學院梯隊。日後要加 U18 或 U10，後台新增一支球隊即可，<b>App 不用改版重上架</b>。'),
                     ('2', '賽事名稱篩選', '企甲、各項盃賽可單獨篩出。<b>這是本次新增的一層</b>——只有「聯賽／盃賽」的粗分類，答不出「藍鯨在某項盃賽的賽程」。'),
                     ('3', '依月份分組', '月份標頭吸頂，往下滑就是完整的一整年。'),
                     ('4', '每張卡片都標球隊', '兩隊都有「一線隊」，不標就分不出這是誰的比賽。'),
                     ('5', '賽事日行程包', '加入手機行事曆（單場或整季）、場地導航、開賽前 2 小時提醒。<b>這三件事網頁做不到。</b>'),
                     ('!', '尚未公布的賽事不會被藏起來', '賽事名稱已知但賽程未定時，顯示該系列的空狀態說明——使用者需要知道「這項盃賽還沒公布」，而不是以為不存在。')],
              admin='<b>這一頁的成本要先講清楚</b><br>賽事資料<b>全部人工維護</b>，不串接任何外部系統，藍鯨的也不從藍鯨官網抓。'
                    '加入藍鯨與盃賽之後，要維護的賽事量會明顯增加——<b>這是持續性的人力，不是做一次就結束</b>。'
                    '賽程可靠度是這個 App 的核心賣點，資料一延遲就直接傷到它。'
                    '<br><br><b>後台維護</b>　C1 球隊　／　C4 賽事　／　新增「賽事系列」設定'),

    st03=dict(h='電子會員卡', u='付費會員在店家櫃檯感受最直接的一項', tag='離線可出示',
              lede='<b>每份會籍一張卡</b>——買了兩隊的人有兩張，各自帶該俱樂部的標誌與品牌色，左右滑動切換。'
                   '這樣藍鯨的合作店家看到的就是藍鯨的卡，<b>不會有「這張卡在我這裡到底能不能用」的疑問</b>。',
              items=[('1', '卡面就是那一隊的識別', '磐石的卡用磐石的標誌與桃紅，藍鯨的卡用藍鯨的。'),
                     ('2', '左右滑動切換', '只有一份會籍就只有一張卡，不會出現空白的第二張。'
                            '<b>沒買的那一隊顯示加入入口，不會被藏起來。</b>'),
                     ('3', 'QR 與會員編號', '店員目視驗卡，或掃 QR 開啟公開驗證頁，上面只顯示姓名首字、會員編號、層級、有效或已過期。'),
                     ('4', '最後同步時間', '卡面一定要標。超過 7 天沒連網會提醒——否則已過期的會員可以用飛航模式出示舊卡。'),
                     ('!', '沒訊號也能秀卡', '球場地下室、店家櫃檯常常收不到訊號。<b>這是網頁永遠做不到的一件事。</b>'),
                     ('!', '手機遺失可自行作廢', '重新產生 QR，舊的立刻失效。<b>一張卡只有一組編碼</b>，不會發出第二組。')],
              admin='<b>一張卡只講一份會籍的狀態</b><br>掃出來就是那一隊的有效或過期，'
                    '<b>不會在同一張卡上並列「磐石有效／藍鯨已到期」</b>——並列了店員反而要多判斷一層。'
                    '<br><br><b>不做掃碼核銷</b>：不記次、不出店家報表、店家不需要開帳號。'
                    '一旦做核銷，就要替每一家店開帳號、做報表、處理對帳，等於長出第二套系統。'
                    '<br><br><b>後台維護</b>　K1 會員管理（會籍狀態即卡片狀態）'),

    st04=dict(h='特約店家與附近地圖', u='會員人在外面時最有用的一頁', tag='需要定位授權',
              lede='「附近有沒有可以打折的店」——這件事網頁做不好，手機做得很好。'
                   '這是<b>付費會籍價值兌現最直接的時刻</b>，所以清單本身完全公開，未登入也看得完整。',
              items=[('1', '依距離排序', '由近至遠，顯示實際距離。<b>定位在手機上算完就丟，不上傳、不儲存。</b>'),
                     ('2', '清單／地圖切換', '地圖上以類別圖示標示。'),
                     ('3', '一鍵導航與撥號', '直接開啟手機地圖或撥號。'),
                     ('4', '適用層級標示', '「全會員適用」與「限付費會員」要明顯區隔，後者對免費會員顯示升級入口。'),
                     ('!', '拒絕定位不會讓功能中斷', '退回依地區手動篩選，並保留手動輸入地址查詢。<b>不會因為不給定位就變成一片空白。</b>')],
              admin='<b>還缺一項資料</b><br>目前店家只有地址文字、<b>沒有經緯度座標</b>，距離排序做不了。'
                    '後台會提供「由地址定位」的輔助按鈕，<b>人工確認後儲存</b>——不做每次開 App 就即時查詢，那既慢又貴，也會把使用者位置送出去。'
                    '<br><br><b>後台維護</b>　K4 特約店家（新增座標欄位）'),

    st05=dict(h='加入付費會籍', u='付款在 LINE Pay 完成，之後自動返回', tag='付款頁為站外',
              lede='付費會籍可以在 App 裡用 <b>LINE Pay</b> 直接完成，付款成功後<b>系統自動開通</b>，'
                   '不用等人工確認。權益對照表與方案比較<b>未登入就看得到</b>——它是入會轉換的關鍵，不設登入牆。',
              items=[('1', '先選俱樂部，再選方案', '<b>會籍是每個俱樂部各一份</b>，兩隊球季不同步，各自計期、各自續會。'
                            '層級維持免費與付費兩層，<b>不因為多一支球隊就多一個層級</b>。'),
                     ('2', '收款方一律是俱樂部', '買藍鯨的會籍，錢一樣進<b>台中磐石足球俱樂部</b>的帳戶、開同一張發票'
                            '（<b>代收代付</b>）。付款畫面明確寫出收款方與這份會籍是哪一隊的，不會讓人搞不清楚。'),
                     ('3', '按鈕上直接寫金額', '避免誤按。'),
                     ('→', '離開前先建立訂單', '表單內容在導向 LINE Pay 之前就保存起來，付款失敗返回時<b>不會要求重填</b>。'),
                     ('!', '重複確認不會重複扣款', '同一筆訂單重複確認不會重複入帳、重複開通或重複寄信。這是硬性要求。')],
              admin='<b>付款只用在會籍費用</b><br>課程費維持線下繳費、商品在官網商店結帳、捐款走慈善平台（且收款方是協會不是俱樂部，'
                    'App 最多提供外連並明確標示）。<br><b>兩隊之間的收入分配是線下的合約與匯款，系統不處理</b>——'
                    'App 與後台都<b>不做分潤計算、不產結算單</b>，只在付款紀錄上記下這筆是哪一隊的會籍，'
                    '事後要加總得出來即可。<br><br><b>後台維護</b>　K1 會員　／　K3 會籍方案與權益'),

    st06=dict(h='推播通知：十種，使用者全部可以關', u='系統信維持五封，不因推播而增減',
              th=['#', '通知', '推給誰', '預設'],
              rows=[('01', '賽事提醒', '開賽前 2 小時', '追蹤該隊的人', '開'),
                    ('02', '場地確認', '場地由「待定」改為確定', '追蹤該隊的人', '開'),
                    ('03', '賽事異動', '日期、時間、場地變更或延期', '追蹤該隊的人', '開'),
                    ('04', '賽果發布', '後台填入比分並發布', '追蹤該隊的人', '開'),
                    ('05', '新聞發布', '文章發布時', '訂閱該分類<b>且追蹤該俱樂部</b>的人', '<b>關</b>'),
                    ('06', '會籍到期提醒', '到期前 30 天、7 天，<b>文案指明是哪一隊</b>', '該會員', '開'),
                    ('07', '會籍開通完成', '付款開通成功', '該會員', '開'),
                    ('08', '球衣狀態異動', '狀態改為「已寄出」', '該會員', '開'),
                    ('09', '課程報名狀態', '狀態改為「已確認」或「已繳費」', '該會員', '開'),
                    ('10', '一般公告', '後台手動發送', '可指定分眾', '開')],
              foot='新聞推播<b>預設關閉</b>——文章的發布節奏若全部推播會造成打擾，由使用者自己開。'
                   '使用者可以一鍵全部關掉，但<b>會籍到期提醒仍會用系統信送到</b>：重要通知不會只有推播一個管道。'),

    eyebrow3='Other screens', h2_3='其餘四組畫面',
    p3='不在主動線上，但每一組都有它非有不可的理由。',
    minis=[('球隊與球員名單',
            '先分<b>台中磐石</b>與<b>台中藍鯨</b>兩區，各區標頭放各自的標誌，區內再列所屬球隊。'
            '球員依位置分組、組內依背號排序。<b>照片與簡介兩隊都還沒有</b>，素材到位前只顯示背號與姓名的文字卡，'
            '<b>不放假圖</b>。'),
           ('新聞與故事',
            '沿用官網現有八個分類，<b>不另外新增</b>。每則標示是磐石還是藍鯨的消息，列表預設只顯示你追蹤的球隊。'
            '開過的文章離線也能讀，保留 30 天；分享出去的是官網網址，沒裝 App 的人也點得開。'),
           ('課程報名與我的報名',
            '沿用官網既有的報名機制，<b>不是新功能，是手機介面</b>。已登入會員的姓名、手機、Email 自動帶入，'
            '手機打字痛苦，這項在 App 上的價值比網頁高一個量級。<b>非會員一樣可以報名。</b>'),
           ('夥伴與贊助商',
            '<b>兩隊的贊助商分區呈現，不會混在一起列</b>——贊助合約是各自簽的，混列等於對外宣稱一段不存在的關係。'
            '目前一份 Logo 都還沒拿到，<b>素材到位前不放示意用的假 Logo</b>。')],
    mini_screens=[
        [('chips', ['台中磐石', '台中藍鯨']), ('bt', '一線隊'), ('img', 's'),
         ('bs', 'GK　DF　MF　FW'), ('bars', ['w90', 'w60'])],
        [('chips', ['全部', '磐石', '藍鯨']), ('img', 's'), ('bt', '賽後報導'),
         ('bars', ['w90', 'w75', 'w45'])],
        [('bt', '夏令營報名'), ('fld', '姓名（已帶入）'), ('fld', '手機（已帶入）'),
         ('bs', '繳費方式：現場或匯款'), ('cta', '送出報名')],
        [('bt', '台中磐石　夥伴'), ('img', 's'), ('bt', '台中藍鯨　夥伴'), ('img', 's'),
         ('tiny', 'Logo 素材待提供')]],

    eyebrow4='Access', h2_4='誰看得到什麼',
    p4='特約店家的<b>清單是全公開的</b>，未登入也看得完整——因為它是吸引人加入會員最有效的誘因。'
       '真正的門檻在「到店出示會員卡兌換」這個動作。',
    acc_th=['功能', '未登入', '免費會員', '付費會員'],
    acc_rows=[('<b>兩隊</b>賽程、賽果、賽事詳情', 'y', 'y', 'y'),
              ('追蹤球隊、首次啟動引導', 'y', 'y', 'y'),
              ('加入行事曆、場地導航、開賽提醒', 'y', 'y', 'y'),
              ('新聞與故事、離線閱讀', 'y', 'y', 'y'),
              ('球隊與球員名單', 'y', 'y', 'y'),
              ('特約店家清單、附近地圖、導航撥號', 'y', 'y', 'y'),
              ('店家優惠<b>實際兌換</b>', 'n', 'p:部分', 'y'),
              ('課程與營隊瀏覽、報名', 'y', 'y', 'y'),
              ('課程表單自動帶入、我的報名', 'n', 'y', 'y'),
              ('會籍方案與權益對照表', 'y', 'y', 'y'),
              ('<b>電子會員卡</b>（每份會籍一張，離線可出示）', 'n', 'y', 'y'),
              ('球衣登記（<b>依會籍分別登記</b>）', 'n', 'n', 'y'),
              ('<b>抽獎資格</b>（<b>逐俱樂部</b>）', 'n', 'n', 'y'),
              ('通知中心與偏好設定', 'y', 'y', 'y')],
    acc_note_h='這張表為什麼還是只有三欄',
    acc_note='因為<b>帳號只有一組</b>，會籍才是分俱樂部的。同一個人可以只買磐石、只買藍鯨，或兩隊都買——'
             '<b>「只買一隊」是預期狀態，不是例外</b>。但層級仍然只有免費與付費兩層，'
             '<b>不會因為多一支球隊就多一種會員身分</b>。<br>'
             '表格裡的「付費會員」讀作「<b>在該俱樂部持有有效付費會籍</b>」：'
             '只買磐石的人，在藍鯨的合作店家就是免費會員，這一點介面上必須隨時講清楚，'
             '<b>不得出現只寫「會籍有效」而不說哪一隊的畫面</b>。',

    colophon='台中磐石足球俱樂部　×　台中藍鯨女子隊　·　行動 App 功能說明（客戶版）<br>'
             '依據《TCRFC 行動 App 功能規劃書》v3.5　·　五個分頁　·　中英雙語，架構預留第三語系<br>'
             '本文件的用色為說明文件用色，非 App 最終視覺。',


    # ── 全部畫面 S01–S23（gallery）──────────────────────────────────────
    # (代號, 名稱, 一句話說明, 分頁索引或 None, 內容積木)
    eyebrow6='All screens', h2_6='全部畫面：S01–S23',
    p6='規劃書列出的二十三個畫面，全部畫在這裡。<b>粉紅色代號</b>的六個是前面走過的主動線，'
       '其餘十七個一併補齊，方便逐頁核對有沒有漏掉或多做。畫面內容皆為示意，實際文案與資料由後台維護。',
    screens=[
      ('S01','首頁','依追蹤偏好決定顯示哪一隊。',0,True,[
        ('blk',[('bt','下一場比賽'),('bs','還有 2 天 04:12'),
                ('vs',('r','台中磐石','台北勒克斯',True)),
                ('bs','10/04（六）19:00'),('cta2',('行事曆','導航'))]),
        ('ad',[('bt','廣告'),('img','s')]),
        ('blk',[('bt','最新消息'),('img','s')]),
        ('blk',[('bt','我的會員卡'),('bs','球迷會員')])]),
      ('S02','賽程列表','兩隊全部球隊，12 個月完整賽程。',1,True,[
        ('chips',(['全部','磐石','藍鯨'],0)),
        ('chipsd',(['企甲','盃賽 01'],0)),
        ('bt','2026 年 10 月'),
        ('mrows',[('04','r','台中磐石','台北勒克斯','企甲聯賽',True,'19:00'),
                  ('11','b','台中藍鯨','高雄攻城獅','女甲聯賽',False,'16:30')])]),
      ('S03','賽事詳情','場地導航、加入行事曆、賽後賽果。',1,False,[
        ('img','t'),
        ('vs',('r','台中磐石','台北勒克斯',True)),
        ('kv',[('日期','10/04（六）'),('開賽','19:00'),('場地','台中足球場'),('輪次','第 7 輪')]),
        ('cta2',('加入行事曆','場地導航')),
        ('pl',[('bt','賽果'),('bs','賽後填入比分、進球者、卡牌、出賽名單與賽後報導連結')])]),
      ('S04','球隊名單','先分兩間俱樂部，再列所屬球隊。',4,False,[
        ('chips',(['台中磐石','台中藍鯨'],0)),
        ('chips',(['一線隊','U15','U14'],0)),
        ('blk',[('bt','守門員'),('bs','01　王○○'),('bs','21　李○○')]),
        ('blk',[('bt','後衛'),('bs','03　張○○'),('bs','05　陳○○')]),
        ('tiny','照片素材待提供')]),
      ('S05','球員詳情','素材到位前只顯示文字卡，不放假圖。',4,False,[
        ('img','t'),
        ('bt','09　林○○　前鋒'),
        ('kv',[('生日','1999/03/12'),('國籍','中華民國'),('慣用腳','右'),('加入','2024')]),
        ('blk',[('bt','本季數據'),('kv',[('出賽','18'),('進球','7'),('助攻','3'),('黃／紅','2 / 0')])]),
        ('pl',[('bs','相關新聞')])]),
      ('S06','新聞列表','標示來源球隊，預設依追蹤過濾。',3,False,[
        ('chips',(['全部','磐石','藍鯨'],0)),
        ('blk',[('img','s'),('bt','<span class="badge r">TCRFC</span>賽後報導'),('bs','10/04　賽事報導')]),
        ('blk',[('img','s'),('bt','<span class="badge b">BW</span>球隊動態'),('bs','10/02　球隊動態')])]),
      ('S07','新聞內文','離線可讀 30 天，分享的是官網網址。',3,False,[
        ('img','t'),('bt','標題'),('bs','2026/10/04　·　賽事報導'),
        ('bars',['w90','w75','w90','w60','w45']),
        ('cta2',('字級','分享'))]),
      ('S08','會員中心','會籍逐俱樂部列出，沒買的那一隊顯示加入入口。',2,False,[
        ('blk',[('bt','陳○○'),('bs','TCR-2026-004128')]),
        ('bt','我的會籍'),
        ('lrow',[('台中磐石　球迷會員','有效至 2027/06/30'),
                 ('台中藍鯨','尚未加入　·　立即加入')]),
        ('lrow',[('球衣登記','磐石 已寄出'),('我的報名','2 筆'),
                 ('抽獎資格','磐石 具備')]),
        ('pl',[('bs','兩隊球季不同步，續會提醒各自於到期前 30 天顯示')])]),
      ('S09','電子會員卡','每份會籍一張，左右滑動切換，沒訊號也能出示。',2,True,[
        ('card',('TCRFC','台中磐石','有效至　2027/06/30',False,True)),
        ('swipe',(['台中磐石','台中藍鯨'],0))]),
      ('S10','會籍方案與升級','先選俱樂部再選方案；未登入就看得到。',2,False,[
        ('blk',[('bt','一般會員'),('bs','免費'),('bs','· 最新消息與賽程')]),
        ('blk',[('bt','球迷會員'),('bs','NT$ 1,200 ／ 球季'),
                ('bs','· 該俱樂部的會員卡一張<br>· 球衣一件<br>· 店家折扣<br>· 抽獎資格')]),
        ('chips',(['台中磐石','台中藍鯨'],0)),
        ('cta','加入球迷會員')]),
      ('S11','付款流程','LINE Pay 完成後自動開通。',2,True,[
        ('pl',[('bs','收款方：台中磐石足球俱樂部')]),
        ('blk',[('bt','LINE Pay'),('bt','NT$ 1,200'),('ctad','確認付款'),('tiny','取消')]),
        ('okb','✓ 會籍已開通')]),
      ('S12','球衣登記','尺寸、領取方式與寄送狀態。',2,False,[
        ('bt','球衣尺寸'),
        ('chips',(['S','M','L','XL'],1)),
        ('bt','領取方式'),
        ('chips',(['寄送','到場領取'],0)),
        ('fld','收件姓名'),('fld','收件地址'),
        ('pl',[('bs','狀態：待處理 → 已寄出 → 已領取')]),
        ('cta','送出')]),
      ('S13','特約店家清單／地圖','依距離排序，定位只在手機上算。',4,True,[
        ('chips',(['清單','地圖'],0)),
        ('tiny','依距離排序'),
        ('blk',[('bt','好味小館　和平店'),('bs','餐飲 · 0.4 km · 全會員 9 折'),
                ('cta2',('導航','撥號'))]),
        ('blk',[('bt','運動家健身'),('bs','健康 · 1.2 km · 限付費會員')])]),
      ('S14','特約店家詳情','適用層級要明顯區隔。',4,False,[
        ('img','t'),('bt','好味小館　和平店'),
        ('kv',[('類別','餐飲'),('地址','台中市和平區…'),('電話','04-2xxx-xxxx'),
               ('營業','11:00–21:00')]),
        ('blk',[('bt','會員優惠'),('bs','全會員 9 折　·　付費會員 85 折')]),
        ('cta2',('導航','撥號'))]),
      ('S15','課程列表','梯次狀態自動顯示可否報名。',4,False,[
        ('chips',(['全部','兒童','營隊'],0)),
        ('blk',[('img','s'),('bt','2026 冬令營'),('bs','12/22–12/26　·　剩 6 位'),('cta','立即報名')]),
        ('blk',[('bt','週末兒童訓練'),('bs','每週六 09:00　·　額滿'),('ctag','額滿候補')])]),
      ('S16','課程報名表','已登入會員的資料自動帶入。',4,False,[
        ('bt','2026 冬令營'),
        ('fld','學員姓名'),('fld','出生年月日'),
        ('pl',[('bs','以下由會員資料自動帶入')]),
        ('fld','家長姓名（已帶入）'),('fld','手機（已帶入）'),('fld','Email（已帶入）'),
        ('bs','☐ 我已閱讀健康聲明'),
        ('cta','送出報名')]),
      ('S17','我的報名','僅限會員本人的報名紀錄。',2,False,[
        ('blk',[('bt','2026 冬令營'),('bs','12/22–12/26　·　台中足球場'),
                ('kv',[('狀態','已確認'),('繳費','待繳費')])]),
        ('blk',[('bt','週末兒童訓練'),('kv',[('狀態','已完成')])]),
        ('pl',[('bs','繳費為線下作業，不在 App 內付款')])]),
      ('S18','抽獎資訊','逐俱樂部顯示有沒有資格，不做任何操作。',2,False,[
        ('okb','✓ 台中磐石　具備資格'),
        ('bs','各俱樂部各自舉辦。該俱樂部的會籍在資格基準時間有效即自動列入，無須任何操作。'),
        ('lrow',[('台中藍鯨','未持有會籍　·　不具資格')]),
        ('pl',[('bt','活動辦法'),('bars',['w90','w75','w60'])]),
        ('kv',[('開獎','2027/01/15'),('方式','現場直播')]),
        ('tiny','不顯示序號、不做查詢、無名單頁')]),
      ('S19','夥伴與贊助商','兩隊分區呈現，不混列。',4,False,[
        ('blk',[('bt','台中磐石　贊助商'),('chips',(['　','　','　'],9))]),
        ('blk',[('bt','台中藍鯨　贊助商'),('chips',(['　','　','　'],9))]),
        ('tiny','Logo 素材待提供，不放假 Logo'),
        ('ctag','合作洽詢')]),
      ('S20','通知中心','App 內收件匣，保留 90 天，離線可讀。',2,False,[
        ('lrow',[('賽事提醒','台中磐石 19:00 開賽　·　2 小時前'),
                 ('賽果發布','台中藍鯨 2–1 勝　·　昨天'),
                 ('會籍到期提醒','剩 30 天　·　3 天前')]),
        ('pl',[('bs','保留 90 天，離線可讀')])]),
      ('S21','設定','語言、推播、定位與帳號刪除。',4,False,[
        ('chips',(['繁體中文','English'],0)),
        ('tog',[('賽事提醒',True),('賽果發布',True),('新聞發布',False),('會籍與球衣',True)]),
        ('lrow',[('免打擾時段','22:00–08:00'),('清除快取',None),('刪除帳號',None)])]),
      ('S22','FAQ','沿用官網既有問答，不另建一套。',4,False,[
        ('fld','搜尋常見問題'),
        ('lrow',[('加入球隊',None),('學院招生',None),('課程與營隊報名',None),
                 ('費用與退費',None),('球迷會與商品',None)])]),
      ('S23','追蹤設定／首次啟動引導','只出現一次，之後隨時可改。',4,True,[
        ('bt','語言'),('chips',(['繁體中文','English'],0)),
        ('bt','你想追蹤誰？'),
        ('fld','☑　台中磐石　一線隊'),('fld','☑　台中藍鯨　一線隊'),
        ('fld','☐　台中磐石　學院梯隊'),
        ('ctag','全部追蹤'),('cta','開始使用')]),
    ],
    gal_note_h='四件在畫面上看不出來、但很重要的事',
    gal_note='<b>①</b> 球員照片與簡介<b>兩隊都還沒有</b>，素材到位前只顯示背號與姓名的文字卡，不放假圖。'
             '<b>②</b> 新聞沿用官網現有八個分類、<b>不另外新增</b>，俱樂部歸屬是另一個維度，不會變成十六個分類。'
             '<b>③</b> 課程報名<b>不是新功能</b>，是官網既有機制的手機介面；非會員一樣可以報名，繳費維持線下。'
             '<b>④</b> 夥伴與贊助商目前<b>一份 Logo 都還沒拿到</b>，素材到位前不放示意用的假 Logo。',

    # 手機示意內的字串
    ph=dict(
        onb_lang='語言', onb_zh='繁體中文', onb_en='English',
        onb_q='你想追蹤誰？', onb_a='台中磐石　一線隊', onb_b='台中藍鯨　一線隊',
        onb_c='台中磐石　學院梯隊', onb_all='全部追蹤', onb_skip='先跳過',
        onb_note='之後可以隨時更改',
        home_next='下一場比賽', home_cd='還有 2 天 04:12',
        home_vs='台中磐石　vs　台北勒克斯', home_when='10/04（六）19:00　台中足球場',
        home_cal='加入行事曆', home_nav='導航',
        home_ad='廣告', home_news='最新消息', home_card='我的會員卡',
        home_store='附近特約店家', home_store1='好味小館　和平店　0.4 km',
        home_sp='贊助商', home_tabs=['首頁', '賽程', '會員', '新聞', '更多'],
        fx_all='全部', fx_r='磐石', fx_b='藍鯨', fx_ac='學院',
        fx_c1='企甲聯賽', fx_c2='盃賽 01', fx_c3='盃賽 02',
        fx_month='2026 年 10 月',
        # (日, 隊色, 我方隊名, 對手, 賽事系列, 是否主場, 時間)
        # 顯示時一律**地主隊在前**：主場寫「我方 vs 對手」，客場寫「對手 vs 我方」。
        fx_rows=[('04', 'r', '台中磐石', '台北勒克斯', '企甲聯賽', True, '19:00'),
                 ('11', 'b', '台中藍鯨', '高雄攻城獅', '女甲聯賽', False, '16:30'),
                 ('18', 'r', '台中磐石', '桃園極限', '盃賽 01', True, '18:00'),
                 ('25', 'b', '台中藍鯨', '台南天后', '盃賽 01', True, '15:00')],
        fx_home='主', fx_away='客',
        fx_seas='整季加入行事曆',
        card_no='會員編號', card_nov='TCR-2026-004128', card_nm='陳○○',
        card_tier='球迷會員', card_exp='有效至　2027/06/30',
        card_sync='最後同步　2026/09/10 14:22', card_hint='台中磐石',
        card_hint2='台中藍鯨', card_exp2='尚未加入',
        card_swipe='左右滑動切換　·　每份會籍一張卡',
        store_list='清單', store_map='地圖', store_near='依距離排序',
        store_1='好味小館　和平店', store_1d='餐飲　·　0.4 km　·　全會員 9 折',
        store_2='運動家健身　北屯店', store_2d='健康　·　1.2 km　·　限付費會員',
        store_nav='導航', store_call='撥號',
        pay_t='台中磐石　球迷會員', pay_p='NT$ 1,200 ／ 球季',
        pay_b1='該俱樂部的會員卡一張', pay_b2='球衣一件', pay_b3='特約店家折扣', pay_b4='抽獎資格',
        pay_who='收款方：台中磐石足球俱樂部',
        pay_cta='以 LINE Pay 付款 NT$ 1,200',
        pay_lp='LINE Pay', pay_lpc='台中磐石足球俱樂部', pay_lpa='NT$ 1,200',
        pay_ok='確認付款', pay_cancel='取消', pay_back='付款完成後自動返回 App',
        ok='✓ 會籍已開通',
    ),
)

C['en'] = dict(
    lang='en', chip='en ▾',
    font='"Helvetica Neue",Helvetica,Arial,"Noto Sans",sans-serif',
    doctitle='Taichung Football App — Feature Overview (Client Edition)',
    mark1='TCRFC',
    crest='Taichung Rock FC　×　Taichung Blue Whale',
    h1='One app, <em>two clubs</em>, one membership each.',
    stand='This document lays the mobile app out screen by screen: what a user sees after opening it, '
          'what sits on each screen, and which content is maintained in the admin. The screens are '
          '<b>functional sketches</b> for confirming structure and flow; visual design has not started.',
    stamps=[('Based on　', 'Mobile App Specification v3.5'), ('Tabs　', '5　×　23 screens'),
            ('Languages　', 'Chinese / English'), ('Admin modules　', 'M1–M5　＋　E4–E6 (shared admin)')],

    eyebrow1='App map', h2_1='Five tabs, split by function rather than by club',
    p1='Five fixed tabs along the bottom; nothing is more than three taps deep. '
       '<b>There is no "Rock tab" and no "Blue Whale tab"</b> — tabs are split by function, and the club '
       'is a filter inside each function. Parallel club tabs would mean building every feature twice, and '
       'anyone following one club would be looking at a half-empty screen. The middle column is '
       '<b>the most frequently opened screen</b>.',
    tabs=[
        ('1', 'Home', False, [
            ('Next match', 'Whose match is shown follows what you follow, with a countdown and a club label.'),
            ('Latest news', 'Three horizontally scrolling cards.'),
            ('Nearby stores', 'Sorted by distance where location is granted.'),
            ('Advertising slot', 'Sold by the club; must be labelled "Advertisement".'),
            ('Sponsor logo wall', '<b>No impressions counted, never in advertising reports.</b>')]),
        ('2', 'Fixtures', True, [
            ('Every squad, both clubs', 'Rock first team, <b>Blue Whale first team</b>, U15 / U14 / U12, plus an "all" view.'),
            ('12-month calendar', 'A full year ahead, league and cup alike, grouped by month.'),
            ('Filter by competition', 'The Premier League and each tournament can be filtered individually.'),
            ('Match detail', 'Venue navigation, add to calendar, kick-off reminder.'),
            ('Works offline', 'Once downloaded, consultable with no connection.')]),
        ('3', 'Member', False, [
            ('Digital membership card', '<b>Presentable with no signal</b>; <b>one card per membership</b>, swipe to switch.'),
            ('Membership status', '<b>Listed club by club</b>: tier, member number, expiry date.'),
            ('My bookings', 'Program and camp booking records.'),
            ('Jersey registration', 'Size, collection method, dispatch status, <b>registered per membership</b>.'),
            ('Prize-draw eligibility', 'Shows only whether you have it, <b>club by club</b>; nothing to operate.')]),
        ('4', 'News', False, [
            ('Category list', "The website's existing eight categories, with none added."),
            ('Club labelling', 'Marks each item as Rock or Blue Whale news.'),
            ('Filtered by follow', 'Defaults to the clubs you follow; switchable to all.'),
            ('Offline reading', 'Opened articles are kept for 30 days.')]),
        ('5', 'More', False, [
            ('Both clubs’ squads', 'Split into two club sections, each listing its own squads.'),
            ('Partner stores', 'Nearby map, navigation, calling.'),
            ('Program booking', 'Member details pre-filled into the form.'),
            ('Partners and sponsors', 'Sectioned by club, never merged.'),
            ('Charity / shop', 'Both are <b>outbound links</b>, never completed inside the app.'),
            ('Follow settings · FAQ · Settings', 'Change the squads you follow at any time.')]),
    ],

    eyebrow2='User journey', h2_2='The journey: what a user actually sees',
    p2='What follows walks through it in order. The numbers on each screen match the notes beside it. '
       'Club names, opponents, store names, and amounts are illustrative; the real content is maintained '
       'in the admin.',

    st00=dict(h='Opening the app for the first time', u='Shown once; changeable later from "More"',
              tag='Entirely skippable',
              lede='A two-club app has to ask one thing up front: <b>who do you follow?</b> Without knowing, '
                   'the home screen cannot decide whose next match to show. But the question '
                   '<b>must not become a barrier</b> — so both steps can be skipped, and skipping means '
                   'following everything.',
              items=[('1', 'Language first', "Defaults to the device language; anything other than Chinese or English defaults to Chinese."),
                     ('2', 'Then who to follow', 'Multi-select, or simply "follow everything". <b>"Follow everything" is an explicit option, not a side effect of skipping.</b>'),
                     ('3', 'Push permission last', 'The purpose is explained before the system prompt appears; no permission dialogue on launch.'),
                     ('!', 'No sign-in wall', 'The whole flow completes while signed out. Preferences are stored on the phone first and synced to the account after sign-in.'),
                     ('!', 'Following is not push', 'Following only decides what the home screen shows. Wanting Blue Whale on your home screen without Blue Whale notifications is a valid combination.')],
              admin='<b>An important limit</b><br>Following <b>changes defaults; it never hides the other club</b> — '
                    'someone following Rock can still switch to Blue Whale in the fixtures tab. '
                    'Following is ordering and defaults, not permission.<br><br><b>Maintained in</b>　M2 App composition'),

    st01=dict(h='Home', u='The first screen on every launch', tag='Ordered by what you follow',
              lede='The home screen <b>is not split into a "Rock section" above a "Blue Whale section"</b> — '
                   'it is ordered by content type, not by club. Splitting by club would leave anyone following '
                   'one club looking at a half-empty screen.',
              items=[('1', 'Next match', 'Countdown, opponent, time, venue, one tap to add to the calendar or navigate. <b>Every card carries its club</b>; with both present, an unlabelled card cannot be attributed.'),
                     ('2', 'Advertising slot', 'Club-sold, and clearly labelled. If a creative fails to load the club’s own fallback content appears, so <b>the slot is never blank</b>.'),
                     ('3', 'Latest news', 'Three horizontally scrolling cards, each labelled with its club.'),
                     ('4', 'Membership card shortcut', 'Shown when signed in with a valid membership; otherwise "Join".'),
                     ('5', 'Nearby stores', 'By distance where location is granted, otherwise in admin order.'),
                     ('6', 'Sponsor logo wall', '<b>No impressions counted, never in advertising reports</b> — that number reconciles with nothing.')],
              admin='<b>When nothing is followed yet</b><br>Both clubs carry equal weight and each first team’s next '
                    'match is shown. <b>No default bias toward either club.</b><br><br><b>Maintained in</b>　'
                    'M2 home blocks and ordering　/　E4–E6 advertising'),

    st02=dict(h='Fixtures: two clubs, 12 months, every competition',
              u="This is the app's number one feature", tag='Opened most often',
              lede='Every squad of both clubs in one place is the reason people open the app <b>week after week</b> — '
                   'a membership card gets used a few times a season; fixtures get checked every week. '
                   'Nowhere offers this today: Rock’s fixtures live on Rock’s site, Blue Whale’s on theirs, '
                   'and the cup competitions are scattered.',
              items=[('1', 'Club and squad tabs', 'All / Rock first team / Blue Whale first team / academy. Adding a U18 or U10 later means adding one squad in the admin, with <b>no app update or resubmission</b>.'),
                     ('2', 'Filter by competition', 'The Premier League and each tournament individually. <b>This layer is new</b> — a four-value league/cup category cannot answer "when does Blue Whale play in that tournament?"'),
                     ('3', 'Grouped by month', 'A sticky month header; scrolling down covers a full year.'),
                     ('4', 'Every card carries its club', 'Both clubs have a "first team"; without the label a card cannot be attributed.'),
                     ('5', 'The matchday bundle', 'Add to the phone calendar (one match or the whole season), venue navigation, a reminder two hours before kick-off. <b>A website can do none of these three.</b>'),
                     ('!', 'Unpublished fixtures are never hidden', 'Where a competition is known but its fixtures are not, an empty-state note appears for it — users need to know it has not been published, not to assume it does not exist.')],
              admin='<b>The cost of this page, stated plainly</b><br>Fixture data is <b>maintained entirely by hand</b>, '
                    'with no external integration, and Blue Whale’s is not pulled from their website either. '
                    'Adding Blue Whale and the cup competitions increases that workload noticeably — '
                    '<b>an ongoing staffing commitment, not a one-off setup</b>. Fixture reliability is this app’s '
                    'core proposition, and any delay damages it directly.<br><br><b>Maintained in</b>　'
                    'C1 squads　/　C4 fixtures　/　new competition records'),

    st03=dict(h='Digital membership card', u='What a paid member feels most directly at a counter',
              tag='Works offline',
              lede='<b>One card per membership</b> — someone holding both clubs’ memberships has two cards, each '
                   'carrying that club’s crest and colours, switched by swiping. A Blue Whale partner store therefore '
                   'sees a Blue Whale card, and <b>nobody has to wonder whether this card works here</b>.',
              items=[('1', 'The card face is that club’s identity', 'Rock’s card uses Rock’s crest and magenta; Blue Whale’s uses its own.'),
                     ('2', 'Swipe to switch', 'One membership means one card; there is never a blank second card. <b>The club not yet joined shows a join entry rather than being hidden.</b>'),
                     ('3', 'QR and member number', 'Staff check by eye, or scan the QR to open a public verification page showing only the first character of the name, member number, tier, and valid or expired.'),
                     ('4', 'Last synced time', 'Always printed on the card face, with a warning after seven days without a sync — otherwise an expired member could present a stale card in airplane mode.'),
                     ('!', 'Presentable with no signal', 'Basements at grounds and store counters routinely have none. <b>A website will never do this.</b>'),
                     ('!', 'Revocable by the member', 'Regenerating the QR invalidates the old one immediately. <b>One card has exactly one code</b>; a second is never issued.')],
              admin='<b>A card states one membership, never two</b><br>What is scanned is that club’s valid-or-expired '
                    'status. <b>The same card never lists "Rock valid / Blue Whale expired"</b> — listing both only '
                    'adds a judgement for the person at the counter.<br><br><b>No scan-to-redeem</b>: no counting, no '
                    'store reports, no store accounts. Redemption tracking would mean an account, a report, and a '
                    'reconciliation process for every store — a second system.'
                    '<br><br><b>Maintained in</b>　K1 members (membership status is card status)'),

    st04=dict(h='Partner stores and the nearby map', u='The most useful screen when a member is out',
              tag='Needs location permission',
              lede='"Is there somewhere nearby that gives me a discount?" — a website answers this badly and a phone '
                   'answers it well. This is <b>where a paid membership pays for itself</b>, so the directory itself '
                   'is fully public and complete without signing in.',
              items=[('1', 'Sorted by distance', 'Nearest first, with the actual distance shown. <b>Location is computed on the phone and discarded; never uploaded, never stored.</b>'),
                     ('2', 'List / map toggle', 'Category icons mark each store on the map.'),
                     ('3', 'One-tap navigation and calling', 'Opens the phone’s maps app or dials directly.'),
                     ('4', 'Applicable tier', '"All members" and "paid members only" must be clearly distinguished, with an upgrade route shown to free members.'),
                     ('!', 'Refusing location does not break it', 'It falls back to filtering by area, and manual address lookup remains available. <b>Declining never produces a blank screen.</b>')],
              admin='<b>One piece of data is still missing</b><br>Stores currently hold text addresses and '
                    '<b>no coordinates</b>, so distance sorting cannot be built. The admin will offer a "locate from '
                    'address" helper, <b>saved after human confirmation</b> — never a live lookup on every launch, '
                    'which is slow, costly, and sends the user’s position out.<br><br><b>Maintained in</b>　'
                    'K4 partner stores (new coordinate fields)'),

    st05=dict(h='Joining a paid membership', u='Payment completes in LINE Pay, then returns automatically',
              tag='Payment page is off-site',
              lede='A paid membership can be completed in the app with <b>LINE Pay</b>, and <b>the system activates it '
                   'automatically</b> once payment succeeds. The benefits table and plan comparison are '
                   '<b>visible without signing in</b> — they are the key to conversion and carry no sign-in wall.',
              items=[('1', 'Choose the club, then the plan', '<b>Membership is one per club</b>; the two seasons are not aligned, so each runs and renews on its own term. Two tiers remain, free and paid; <b>a second club does not add a tier</b>.'),
                     ('2', 'The collecting party is always the club', 'A Blue Whale membership is still paid into <b>Taichung Rock FC</b>’s account against the same invoice (<b>collected on its behalf</b>). The screen names both the collecting party and which club this membership is for.'),
                     ('3', 'The amount is on the button', 'So nobody taps by accident.'),
                     ('→', 'The order is created before leaving', 'Form contents are saved before handing off to LINE Pay, so a failed payment <b>never asks the user to retype anything</b>.'),
                     ('!', 'Confirming twice never charges twice', 'A repeated confirmation on the same order never double-charges, double-activates, or double-emails. This is a hard requirement.')],
              admin='<b>Payment is only ever for membership fees</b><br>Course fees stay offline, merchandise is checked '
                    'out in the website shop, and donations go through the charity platform (collected by the '
                    'Association, not the club — the app links out and says so plainly).<br>'
                    '<b>Any revenue sharing between the clubs is a contract and a bank transfer handled offline</b>; '
                    'neither the app nor the admin performs a settlement calculation or produces statements — the '
                    'payment record simply carries which club the membership is for, so it can be totalled afterwards.<br><br>'
                    '<b>Maintained in</b>　K1 members　/　K3 plans and benefits'),

    st06=dict(h='Push notifications: ten types, all switchable off',
              u='The five system emails stand, neither added to nor reduced',
              th=['#', 'Notification', 'Sent to', 'Default'],
              rows=[('01', 'Kick-off reminder', 'Two hours before kick-off', 'People following that squad', 'On'),
                    ('02', 'Venue confirmed', 'A venue changes from "to be confirmed"', 'People following that squad', 'On'),
                    ('03', 'Fixture change', 'Date, time, or venue changed; postponed', 'People following that squad', 'On'),
                    ('04', 'Result published', 'A score is entered and published', 'People following that squad', 'On'),
                    ('05', 'Article published', 'When an article is published', 'Subscribers to that category <b>who follow that club</b>', '<b>Off</b>'),
                    ('06', 'Membership expiry', '30 and 7 days before; <b>the copy names the club</b>', 'That member', 'On'),
                    ('07', 'Membership activated', 'Payment activation succeeds', 'That member', 'On'),
                    ('08', 'Jersey status', 'Status changes to "dispatched"', 'That member', 'On'),
                    ('09', 'Booking status', 'Status changes to "confirmed" or "paid"', 'That member', 'On'),
                    ('10', 'General announcement', 'Sent by hand from the admin', 'Segmentable', 'On')],
              foot='Article push <b>defaults to off</b> — pushing every article at its publishing cadence would be '
                   'intrusive, so users opt in. Everything can be switched off at once, but <b>the membership expiry '
                   'reminder still arrives by email</b>: an important message never depends on push alone.'),

    eyebrow3='Other screens', h2_3='Four more groups of screens',
    p3='Off the main journey, but each one earns its place.',
    minis=[('Squads and players',
            'Split first into <b>Taichung Rock</b> and <b>Taichung Blue Whale</b>, each section headed by its own '
            'crest, with that club’s squads beneath. Players are grouped by position and ordered by shirt number. '
            '<b>Neither club has supplied photographs or biographies</b>; until they arrive, only a text card with '
            'number and name is shown — <b>never a placeholder image</b>.'),
           ('News and stories',
            'The website’s existing eight categories, <b>with none added</b>. Each item is labelled Rock or Blue '
            'Whale, and the list defaults to the clubs you follow. Opened articles stay readable offline for 30 days; '
            'sharing sends the website URL, which opens for people without the app.'),
           ('Program booking and my bookings',
            'Reuses the website’s existing booking mechanism — <b>not a new feature, a phone interface</b>. '
            'A signed-in member’s name, mobile, and email are pre-filled; typing on a phone is painful, so this is '
            'worth an order of magnitude more here than on the web. <b>Non-members can still book.</b>'),
           ('Partners and sponsors',
            '<b>The two clubs’ sponsors appear in separate sections and are never merged</b> — the contracts are '
            'signed separately, and merging them asserts a relationship that does not exist. Not one logo has been '
            'supplied yet, and <b>no placeholder logos will be used</b>.')],
    mini_screens=[
        [('chips', ['Taichung Rock', 'Blue Whale']), ('bt', 'First Team'), ('img', 's'),
         ('bs', 'GK　DF　MF　FW'), ('bars', ['w90', 'w60'])],
        [('chips', ['All', 'Rock', 'Whale']), ('img', 's'), ('bt', 'Match report'),
         ('bars', ['w90', 'w75', 'w45'])],
        [('bt', 'Summer camp'), ('fld', 'Name (pre-filled)'), ('fld', 'Mobile (pre-filled)'),
         ('bs', 'Payment: in person or transfer'), ('cta', 'Submit booking')],
        [('bt', 'Taichung Rock　partners'), ('img', 's'), ('bt', 'Blue Whale　partners'), ('img', 's'),
         ('tiny', 'Logo assets outstanding')]],

    eyebrow4='Access', h2_4='Who can see what',
    p4='The partner-store <b>directory is entirely public</b> and complete without signing in — because it is the '
       'most effective reason anyone has to join. The real threshold is the act of showing a card in the shop.',
    acc_th=['Feature', 'Signed out', 'Free member', 'Paid member'],
    acc_rows=[('<b>Both clubs’</b> fixtures, results, match detail', 'y', 'y', 'y'),
              ('Following squads, first-run onboarding', 'y', 'y', 'y'),
              ('Add to calendar, navigation, kick-off reminders', 'y', 'y', 'y'),
              ('News and stories, offline reading', 'y', 'y', 'y'),
              ('Squads and players', 'y', 'y', 'y'),
              ('Store directory, nearby map, navigation and calling', 'y', 'y', 'y'),
              ('<b>Redeeming</b> a store offer', 'n', 'p:Some', 'y'),
              ('Browsing and booking programs', 'y', 'y', 'y'),
              ('Form pre-filling, my bookings', 'n', 'y', 'y'),
              ('Membership plans and the benefits table', 'y', 'y', 'y'),
              ('<b>Digital membership card</b> (one per membership, works offline)', 'n', 'y', 'y'),
              ('Jersey registration (<b>per membership</b>)', 'n', 'n', 'y'),
              ('<b>Prize-draw eligibility</b> (<b>club by club</b>)', 'n', 'n', 'y'),
              ('Notification centre and preferences', 'y', 'y', 'y')],
    acc_note_h='Why this table still has only three columns',
    acc_note='because <b>the account is single</b>; it is the membership that is per club. The same person may hold '
             'Rock only, Blue Whale only, or both — <b>"one club only" is the expected state, not an exception</b>. '
             'Tiers remain free and paid, and <b>a second club adds no third kind of member</b>.<br>'
             '"Paid member" in this table reads as <b>holding a valid paid membership at that club</b>: someone who '
             'bought Rock only is a free member at Blue Whale’s partner stores. The interface must say so at all '
             'times — <b>no screen may state "membership valid" without naming the club</b>.',

    colophon='Taichung Rock FC　×　Taichung Blue Whale　·　Mobile App Feature Overview (Client Edition)<br>'
             'Based on the TCRFC Mobile App Functional Specification v3.5　·　five tabs　·　'
             'Chinese and English, with the architecture ready for a third language<br>'
             'The colours here belong to this document, not to the app’s final visual design.',


    # ── All screens S01–S23 (gallery) ───────────────────────────────────
    eyebrow6='All screens', h2_6='Every screen: S01–S23',
    p6='All twenty-three screens listed in the specification are drawn here. The six with a '
       '<b>pink code</b> are the journey walked through above; the other seventeen are included so the '
       'set can be checked page by page for anything missing or surplus. All content is illustrative; '
       'the real copy and data are maintained in the admin.',
    screens=[
      ('S01','Home','Shows whichever club you follow.',0,True,[
        ('blk',[('bt','Next match'),('bs','in 2 days 04:12'),
                ('vs',('r','Taichung Rock FC','Taipei Lux',True)),
                ('bs','Sat 04 Oct 19:00'),('cta2',('Calendar','Navigate'))]),
        ('ad',[('bt','Advertisement'),('img','s')]),
        ('blk',[('bt','Latest news'),('img','s')]),
        ('blk',[('bt','My membership card'),('bs','Fan Club')])]),
      ('S02','Fixture list','Every squad of both clubs, 12 months ahead.',1,True,[
        ('chips',(['All','Rock','Whale'],0)),
        ('chipsd',(['Premier','Tournament 01'],0)),
        ('bt','October 2026'),
        ('mrows',[('04','r','Taichung Rock FC','Taipei Lux','Premier League',True,'19:00'),
                  ('11','b','Taichung Blue Whale','Kaohsiung Attack',"Women's League",False,'16:30')])]),
      ('S03','Match detail','Navigation, calendar, and the result afterwards.',1,False,[
        ('img','t'),
        ('vs',('r','Taichung Rock FC','Taipei Lux',True)),
        ('kv',[('Date','Sat 04 Oct'),('Kick-off','19:00'),('Venue','Taichung Stadium'),('Round','7')]),
        ('cta2',('Add to calendar','Navigate')),
        ('pl',[('bt','Result'),('bs','Score, scorers, cards, line-up and a link to the report, added after the match')])]),
      ('S04','Squad','Split by club first, then by squad.',4,False,[
        ('chips',(['Taichung Rock','Blue Whale'],0)),
        ('chips',(['First Team','U15','U14'],0)),
        ('blk',[('bt','Goalkeepers'),('bs','01　Wang ○○'),('bs','21　Li ○○')]),
        ('blk',[('bt','Defenders'),('bs','03　Chang ○○'),('bs','05　Chen ○○')]),
        ('tiny','Photographs outstanding')]),
      ('S05','Player detail','A text card until the assets arrive — never a fake photo.',4,False,[
        ('img','t'),
        ('bt','09　Lin ○○　Forward'),
        ('kv',[('Born','12 Mar 1999'),('Nationality','Taiwan'),('Foot','Right'),('Joined','2024')]),
        ('blk',[('bt','This season'),('kv',[('Apps','18'),('Goals','7'),('Assists','3'),('Cards','2 / 0')])]),
        ('pl',[('bs','Related news')])]),
      ('S06','News list','Labelled by club, filtered by what you follow.',3,False,[
        ('chips',(['All','Rock','Whale'],0)),
        ('blk',[('img','s'),('bt','<span class="badge r">TCRFC</span>Match report'),('bs','04 Oct　·　Match report')]),
        ('blk',[('img','s'),('bt','<span class="badge b">BW</span>Squad news'),('bs','02 Oct　·　Squad news')])]),
      ('S07','Article','Readable offline for 30 days; sharing sends the website URL.',3,False,[
        ('img','t'),('bt','Headline'),('bs','4 Oct 2026　·　Match report'),
        ('bars',['w90','w75','w90','w60','w45']),
        ('cta2',('Text size','Share'))]),
      ('S08','Member centre','Memberships listed club by club; the club not joined shows a join entry.',2,False,[
        ('blk',[('bt','Chen ○○'),('bs','TCR-2026-004128')]),
        ('bt','My memberships'),
        ('lrow',[('Taichung Rock　Fan Club','Valid to 30 Jun 2027'),
                 ('Taichung Blue Whale','Not joined　·　Join now')]),
        ('lrow',[('Jersey registration','Rock — dispatched'),('My bookings','2'),
                 ('Prize-draw eligibility','Rock — eligible')]),
        ('pl',[('bs','Seasons are not aligned; each renewal prompt appears 30 days before its own expiry')])]),
      ('S09','Digital membership card','One per membership, swipe to switch, presentable with no signal.',2,True,
        [('card',('TCRFC','Taichung Rock','Valid to　30 Jun 2027',False,True)),
         ('swipe',(['Taichung Rock','Taichung Blue Whale'],0))]),
      ('S10','Plans and upgrade','Choose the club, then the plan; visible without signing in.',2,False,[
        ('blk',[('bt','Registered member'),('bs','Free'),('bs','· News and fixtures')]),
        ('blk',[('bt','Fan Club member'),('bs','NT$1,200 / season'),
                ('bs','· One card for that club<br>· One jersey<br>· Store discounts<br>· Prize-draw eligibility')]),
        ('chips',(['Taichung Rock','Taichung Blue Whale'],0)),
        ('cta','Join the Fan Club')]),
      ('S11','Payment flow','Activated automatically once LINE Pay succeeds.',2,True,[
        ('pl',[('bs','Collected by Taichung Rock FC')]),
        ('blk',[('bt','LINE Pay'),('bt','NT$1,200'),('ctad','Confirm payment'),('tiny','Cancel')]),
        ('okb','✓ Membership activated')]),
      ('S12','Jersey registration','Size, collection method, dispatch status.',2,False,[
        ('bt','Jersey size'),
        ('chips',(['S','M','L','XL'],1)),
        ('bt','Collection'),
        ('chips',(['By post','In person'],0)),
        ('fld','Recipient name'),('fld','Delivery address'),
        ('pl',[('bs','Status: pending → dispatched → collected')]),
        ('cta','Submit')]),
      ('S13','Store list / map','Sorted by distance, computed on the phone.',4,True,[
        ('chips',(['List','Map'],0)),
        ('tiny','Sorted by distance'),
        ('blk',[('bt','Hao Wei Diner　Heping'),('bs','Food · 0.4 km · 10% off, all members'),
                ('cta2',('Navigate','Call'))]),
        ('blk',[('bt','Athlete Gym'),('bs','Health · 1.2 km · paid members only')])]),
      ('S14','Store detail','The applicable tier must be unmistakable.',4,False,[
        ('img','t'),('bt','Hao Wei Diner　Heping'),
        ('kv',[('Category','Food'),('Address','Heping, Taichung…'),('Phone','04-2xxx-xxxx'),
               ('Hours','11:00–21:00')]),
        ('blk',[('bt','Member offer'),('bs','10% off all members　·　15% off paid members')]),
        ('cta2',('Navigate','Call'))]),
      ('S15','Program list','Session status decides whether booking is open.',4,False,[
        ('chips',(['All','Children','Camps'],0)),
        ('blk',[('img','s'),('bt','2026 Winter camp'),('bs','22–26 Dec　·　6 places left'),('cta','Book now')]),
        ('blk',[('bt','Weekend training'),('bs','Saturdays 09:00　·　Full'),('ctag','Join the waiting list')])]),
      ('S16','Booking form','A signed-in member’s details are pre-filled.',4,False,[
        ('bt','2026 Winter camp'),
        ('fld','Student name'),('fld','Date of birth'),
        ('pl',[('bs','Pre-filled from your member profile')]),
        ('fld','Parent name (pre-filled)'),('fld','Mobile (pre-filled)'),('fld','Email (pre-filled)'),
        ('bs','☐ I have read the health declaration'),
        ('cta','Submit booking')]),
      ('S17','My bookings','The member’s own booking records only.',2,False,[
        ('blk',[('bt','2026 Winter camp'),('bs','22–26 Dec　·　Taichung Stadium'),
                ('kv',[('Status','Confirmed'),('Payment','Due')])]),
        ('blk',[('bt','Weekend training'),('kv',[('Status','Completed')])]),
        ('pl',[('bs','Payment is handled offline, never inside the app')])]),
      ('S18','Prize-draw information','Eligibility shown club by club; nothing to operate.',2,False,[
        ('okb','✓ Taichung Rock　eligible'),
        ('bs','Each club runs its own draw. That club\u2019s membership being valid at the cut-off enters you '
              'automatically. Nothing to do.'),
        ('lrow',[('Taichung Blue Whale','No membership　·　not eligible')]),
        ('pl',[('bt','Rules'),('bars',['w90','w75','w60'])]),
        ('kv',[('Draw','15 Jan 2027'),('Format','Live, in person')]),
        ('tiny','No serial number, no lookup, no winners list')]),
      ('S19','Partners and sponsors','Sectioned by club, never merged.',4,False,[
        ('blk',[('bt','Taichung Rock　sponsors'),('chips',(['　','　','　'],9))]),
        ('blk',[('bt','Blue Whale　sponsors'),('chips',(['　','　','　'],9))]),
        ('tiny','Logo assets outstanding; no placeholders'),
        ('ctag','Partnership enquiries')]),
      ('S20','Notification centre','An in-app inbox, kept 90 days, readable offline.',2,False,[
        ('lrow',[('Kick-off reminder','Taichung Rock 19:00　·　2 hours ago'),
                 ('Result published','Blue Whale won 2–1　·　yesterday'),
                 ('Membership expiry','30 days left　·　3 days ago')]),
        ('pl',[('bs','Kept 90 days, readable offline')])]),
      ('S21','Settings','Language, push, location, and account deletion.',4,False,[
        ('chips',(['繁體中文','English'],1)),
        ('tog',[('Kick-off reminders',True),('Results',True),('News',False),('Membership & jersey',True)]),
        ('lrow',[('Quiet hours','22:00–08:00'),('Clear cache',None),('Delete account',None)])]),
      ('S22','FAQ','Reuses the website’s existing answers; no second set.',4,False,[
        ('fld','Search the FAQ'),
        ('lrow',[('Joining the club',None),('Academy admissions',None),
                 ('Program & camp registration',None),('Fees & refunds',None),
                 ('Fan club & merchandise',None)])]),
      ('S23','Follow settings / onboarding','Shown once, changeable at any time.',4,True,[
        ('bt','Language'),('chips',(['繁體中文','English'],0)),
        ('bt','Who do you follow?'),
        ('fld','☑　Taichung Rock　First Team'),('fld','☑　Taichung Blue Whale　First Team'),
        ('fld','☐　Taichung Rock　Academy'),
        ('ctag','Follow everything'),('cta','Get started')]),
    ],
    gal_note_h='Four things the screens cannot show, but that matter',
    gal_note='<b>①</b> <b>Neither club has supplied player photographs or biographies</b>; until they arrive '
             'only a text card with number and name is shown, never a placeholder image. '
             '<b>②</b> News reuses the website’s existing eight categories and <b>adds none</b> — club is a '
             'separate dimension, so this never becomes sixteen categories. '
             '<b>③</b> Program booking is <b>not a new feature</b>, it is a phone interface onto the website’s '
             'existing mechanism; non-members can still book, and payment stays offline. '
             '<b>④</b> <b>Not one partner or sponsor logo has been supplied</b>; until they arrive, no '
             'placeholder logos will be used.',

    ph=dict(
        onb_lang='Language', onb_zh='繁體中文', onb_en='English',
        onb_q='Who do you follow?', onb_a='Taichung Rock　First Team',
        onb_b='Taichung Blue Whale　First Team',
        onb_c='Taichung Rock　Academy', onb_all='Follow everything', onb_skip='Skip for now',
        onb_note='Changeable at any time',
        home_next='Next match', home_cd='in 2 days 04:12',
        home_vs='Taichung Rock　vs　Taipei Lux', home_when='Sat 04 Oct 19:00　Taichung Stadium',
        home_cal='Add to calendar', home_nav='Navigate',
        home_ad='Advertisement', home_news='Latest news', home_card='My membership card',
        home_store='Partner stores nearby', home_store1='Hao Wei Diner　Heping　0.4 km',
        home_sp='Sponsors', home_tabs=['Home', 'Fixtures', 'Member', 'News', 'More'],
        fx_all='All', fx_r='Rock', fx_b='Whale', fx_ac='Academy',
        fx_c1='Premier League', fx_c2='Tournament 01', fx_c3='Tournament 02',
        fx_month='October 2026',
        # (day, club colour, our team, opponent, competition, is_home, time)
        # Always list the **home side first**: home reads "us vs them", away reads "them vs us".
        fx_rows=[('04', 'r', 'Taichung Rock FC', 'Taipei Lux', 'Premier League', True, '19:00'),
                 ('11', 'b', 'Taichung Blue Whale', 'Kaohsiung Attack', "Women's League", False, '16:30'),
                 ('18', 'r', 'Taichung Rock FC', 'Taoyuan Limit', 'Tournament 01', True, '18:00'),
                 ('25', 'b', 'Taichung Blue Whale', 'Tainan Queens', 'Tournament 01', True, '15:00')],
        fx_home='H', fx_away='A',
        fx_seas='Add the season to my calendar',
        card_no='Member number', card_nov='TCR-2026-004128', card_nm='Chen ○○',
        card_tier='Fan Club', card_exp='Valid to　30 Jun 2027',
        card_sync='Last synced　10 Sep 2026 14:22', card_hint='Taichung Rock',
        card_hint2='Taichung Blue Whale', card_exp2='Not joined',
        card_swipe='Swipe to switch　·　one card per membership',
        store_list='List', store_map='Map', store_near='Sorted by distance',
        store_1='Hao Wei Diner　Heping', store_1d='Food　·　0.4 km　·　10% off, all members',
        store_2='Athlete Gym　Beitun', store_2d='Health　·　1.2 km　·　paid members only',
        store_nav='Navigate', store_call='Call',
        pay_t='Taichung Rock　Fan Club', pay_p='NT$1,200 / season',
        pay_b1='One card for that club', pay_b2='One jersey', pay_b3='Partner store discounts',
        pay_b4='Prize-draw eligibility',
        pay_who='Collected by Taichung Rock FC',
        pay_cta='Pay NT$1,200 with LINE Pay',
        pay_lp='LINE Pay', pay_lpc='Taichung Rock FC', pay_lpa='NT$1,200',
        pay_ok='Confirm payment', pay_cancel='Cancel', pay_back='Returns to the app automatically',
        ok='✓ Membership activated',
    ),
)


# ═══════════════════════════════════════════════════════════════════════════════
def keyed(items):
    return '<ul class="keyed">' + ''.join(
        '<li><span class="k">%s</span><span><b>%s</b><span class="sub">%s</span></span></li>' % it
        for it in items) + '</ul>'


def station(step, d, phone, tagcls=''):
    tag = '<span class="tag %s">%s</span>' % (tagcls, d['tag']) if d.get('tag') else ''
    return ('<article class="station">\n'
            '  <div class="sh"><div class="step">%s</div><div><h3>%s</h3>'
            '<div class="u">%s</div></div>%s</div>\n'
            '  <div class="sb"><div>%s</div><div><p class="lede">%s</p>%s'
            '<div class="admin">%s</div></div></div>\n'
            '</article>') % (step, d['h'], d['u'], tag, phone, d['lede'],
                             keyed(d['items']), d['admin'])


def pairing(me, opp, is_home):
    """地主隊一律寫在前面；我方隊名標粗，避免與對手混淆。"""
    mine = '<b>%s</b>' % me
    return ('%s　vs　%s' % (mine, opp)) if is_home else ('%s　vs　%s' % (opp, mine))


def items(spec, ph):
    """把 (kind, value) 清單渲染成手機畫面內容。新增畫面時只用這些積木。"""
    out = []
    for kind, val in spec:
        if kind == 'bt':
            out.append('<div class="bt">%s</div>' % val)
        elif kind == 'bs':
            out.append('<div class="bs">%s</div>' % val)
        elif kind == 'tiny':
            out.append('<div class="tiny" style="text-align:center">%s</div>' % val)
        elif kind == 'img':
            out.append('<div class="img %s"></div>' % val)
        elif kind == 'bars':
            out.append('<div class="bars">%s</div>'
                       % ''.join('<span class="bar %s"></span>' % w for w in val))
        elif kind == 'chips':
            labels, act = val
            out.append('<div class="row">%s</div>' % ''.join(
                '<span class="chip%s">%s</span>' % (' on' if i == act else '', t)
                for i, t in enumerate(labels)))
        elif kind == 'chipsd':          # 深色（賽事系列）
            labels, act = val
            out.append('<div class="row">%s</div>' % ''.join(
                '<span class="chip%s">%s</span>' % (' dk' if i == act else '', t)
                for i, t in enumerate(labels)))
        elif kind == 'fld':
            out.append('<div class="fld">%s</div>' % val)
        elif kind == 'cta':
            out.append('<div class="cta">%s</div>' % val)
        elif kind == 'ctag':
            out.append('<div class="cta gh">%s</div>' % val)
        elif kind == 'ctad':
            out.append('<div class="cta dk">%s</div>' % val)
        elif kind == 'cta2':
            g, c2 = val
            out.append('<div class="row"><span class="cta gh" style="flex:1">%s</span>'
                       '<span class="cta" style="flex:1">%s</span></div>' % (g, c2))
        elif kind == 'okb':
            out.append('<div style="text-align:center"><span class="okb">%s</span></div>' % val)
        elif kind in ('blk', 'pl', 'ad'):
            cls = {'blk': 'blk', 'pl': 'blk pl', 'ad': 'blk ad'}[kind]
            out.append('<div class="%s">%s</div>' % (cls, items(val, ph)))
        elif kind == 'lrow':
            out.append(''.join(
                '<div class="lrow"><div><div class="lt">%s</div>%s</div>'
                '<span class="ch">›</span></div>'
                % (lt, '<div class="ls">%s</div>' % ls if ls else '')
                for lt, ls in val))
        elif kind == 'tog':
            out.append(''.join(
                '<div class="lrow"><div class="lt">%s</div>'
                '<span class="pill%s"><i></i></span></div>' % (lt, ' on' if on else '')
                for lt, on in val))
        elif kind == 'kv':
            out.append('<div class="kv">%s</div>' % ''.join(
                '<span class="k2">%s</span><span class="v2">%s</span>' % kv for kv in val))
        elif kind == 'vs':
            cls, me, opp, is_home = val
            out.append('<div class="bt"><span class="badge %s">%s</span>%s</div>'
                       % (cls, 'TCRFC' if cls == 'r' else 'BW', pairing(me, opp, is_home)))
        elif kind == 'mrows':
            out.append(''.join(
                '<div class="mrow"><div class="dt">%s</div><div><div class="who">'
                '<span class="badge %s">%s</span>%s</div><div class="cmp">%s</div></div>'
                '<div class="tm">%s<br>%s</div></div>'
                % (d, cls, 'TCRFC' if cls == 'r' else 'BW',
                   pairing(me, opp, is_home), cmp_,
                   ph['fx_home'] if is_home else ph['fx_away'], tm)
                for d, cls, me, opp, cmp_, is_home, tm in val))
        elif kind == 'card':
            # (標誌文字, 俱樂部名, 有效期, 是否藍鯨色系, 是否顯示卡背)
            mk, club, exp, bw, peek = val
            out.append(
                '<div class="cardwrap">%s<div class="card%s">'
                '<div class="ch"><span class="cm">%s</span>'
                '<span class="cn">%s</span></div>'
                '<div class="qrw">%s</div><div style="text-align:center">'
                '<div class="nm">%s</div><div class="tierp">%s</div></div>'
                '<div class="meta">%s　%s<br>%s<br><b>%s</b></div></div></div>'
                % ('<div class="peek"></div>' if peek else '', ' bw' if bw else '',
                   mk, club, QR, ph['card_nm'], ph['card_tier'],
                   ph['card_no'], ph['card_nov'], exp, ph['card_sync']))
        elif kind == 'swipe':
            labels, act = val
            dots = ''.join('<span class="dot%s"></span>' % (' on' if i == act else '')
                           for i in range(len(labels)))
            out.append('<div class="swipe"><span class="sl">‹</span>%s'
                       '<span class="sl">%s</span><span class="sl">›</span></div>'
                       % (dots, '　／　'.join(labels)))
    return ''.join(out)


def tabbar(names, on):
    return '<div class="tabbar">' + ''.join(
        '<span class="%s">%s</span>' % ('on' if i == on else '', n)
        for i, n in enumerate(names)) + '</div>'


def phone(sc, chip='', bar='', tabs=None, on=0):
    right = bar or chip
    tb = tabbar(tabs, on) if tabs else ''
    return ('<div class="phone"><div class="screen">'
            '<div class="sbar"><span class="ub"></span><span>%s</span></div>'
            '<div class="sc">%s</div>%s</div></div>' % (right, sc, tb))


def build(c):
    p, ph = c, c['ph']

    # ── 分頁地圖 ────────────────────────────────────────────────────────────
    tree = ''.join(
        '<div class="tcol%s"><h4><i>%s</i>%s</h4>%s</div>' % (
            ' spine' if spine else '', num, name,
            ''.join('<div class="node"><div class="nm">%s</div><div class="d">%s</div></div>' % n
                    for n in nodes))
        for num, name, spine, nodes in c['tabs'])

    # ── 00 首次啟動 ─────────────────────────────────────────────────────────
    sc00 = """
    <div class="blk"><div class="bh"><span class="kk">1</span><span class="bt">{onb_lang}</span></div>
      <div class="row"><span class="chip on">{onb_zh}</span><span class="chip">{onb_en}</span></div></div>
    <div class="blk"><div class="bh"><span class="kk">2</span><span class="bt">{onb_q}</span></div>
      <div class="fld" style="margin-bottom:1.1mm">☑　{onb_a}</div>
      <div class="fld" style="margin-bottom:1.1mm">☑　{onb_b}</div>
      <div class="fld" style="margin-bottom:1.1mm">☐　{onb_c}</div>
      <div class="cta gh">{onb_all}</div></div>
    <div class="blk pl"><div class="bh"><span class="kk">3</span><span class="bt">{push}</span></div>
      <div class="bs">{push_s}</div></div>
    <div class="cta">{next}</div>
    <div class="tiny" style="text-align:center">{onb_skip}　·　{onb_note}</div>
    """.format(push=('推播通知' if c['lang'] != 'en' else 'Push notifications'),
               push_s=('先說明用途，再請求權限' if c['lang'] != 'en'
                       else 'The purpose is explained before the prompt'),
               next=('開始使用' if c['lang'] != 'en' else 'Get started'), **ph)
    ph00 = phone(sc00, chip=c['chip'])

    # ── 01 首頁 ────────────────────────────────────────────────────────────
    sc01 = """
    <div class="blk"><div class="bh"><span class="kk">1</span><span class="bt">{home_next}</span></div>
      <div class="bs" style="margin-bottom:.8mm">{home_cd}</div>
      <div class="bt"><span class="badge r">TCRFC</span>{home_vs}</div>
      <div class="bs" style="margin-top:.5mm">{home_when}</div>
      <div class="row" style="margin-top:1.3mm"><span class="cta gh" style="flex:1">{home_cal}</span><span class="cta" style="flex:1">{home_nav}</span></div></div>
    <div class="blk ad"><div class="bh"><span class="kk">2</span><span class="bt">{home_ad}</span></div>
      <div class="img s" style="margin-bottom:0"></div></div>
    <div class="blk"><div class="bh"><span class="kk">3</span><span class="bt">{home_news}</span></div>
      <div class="row"><span style="flex:1"><span class="img s"></span><span class="bs"><span class="badge r">TCRFC</span></span></span><span style="flex:1"><span class="img s"></span><span class="bs"><span class="badge b">BW</span></span></span></div></div>
    <div class="blk"><div class="bh"><span class="kk">4</span><span class="bt">{home_card}</span></div>
      <div class="bs">{card_tier}　·　{card_nov}</div></div>
    <div class="blk"><div class="bh"><span class="kk">5</span><span class="bt">{home_store}</span></div>
      <div class="bs">{home_store1}</div></div>
    <div class="blk pl"><div class="bh"><span class="kk">6</span><span class="bt">{home_sp}</span></div>
      <div class="row"><span class="chip">　</span><span class="chip">　</span><span class="chip">　</span></div></div>
    """.format(**ph)
    ph01 = phone(sc01, chip=c['chip'], tabs=ph['home_tabs'], on=0)

    # ── 02 賽程 ────────────────────────────────────────────────────────────
    rows = ''.join(
        '<div class="mrow"><div class="dt">%s</div><div><div class="who">'
        '<span class="badge %s">%s</span>%s</div><div class="cmp">%s</div></div>'
        '<div class="tm">%s<br>%s</div></div>'
        % (d, cls, 'TCRFC' if cls == 'r' else 'BW',
           pairing(me, opp, is_home), cmp_,
           ph['fx_home'] if is_home else ph['fx_away'], tm)
        for d, cls, me, opp, cmp_, is_home, tm in ph['fx_rows'])
    sc02 = """
    <div class="row"><span class="chip on">{fx_all}</span><span class="chip">{fx_r}</span><span class="chip">{fx_b}</span><span class="chip">{fx_ac}</span></div>
    <div class="row"><span class="chip dk">{fx_c1}</span><span class="chip">{fx_c2}</span><span class="chip">{fx_c3}</span></div>
    <div class="mday">{fx_month}</div>
    {rows}
    <div class="cta gh">{fx_seas}</div>
    """.format(rows=rows, **ph)
    ph02 = phone(sc02, chip=c['chip'], tabs=ph['home_tabs'], on=1)

    # ── 03 會員卡 ──────────────────────────────────────────────────────────
    sc03 = """
    <div class="cardwrap"><div class="peek"></div><div class="card">
      <div class="ch"><span class="cm">{mark1}</span><span class="cn">{card_hint}</span></div>
      <div class="qrw">{qr}</div>
      <div style="text-align:center"><div class="nm">{card_nm}</div>
        <div class="tierp">{card_tier}</div></div>
      <div class="meta">{card_no}　{card_nov}<br>{card_exp}<br><b>{card_sync}</b></div>
    </div></div>
    <div class="swipe"><span class="sl">‹</span><span class="dot on"></span><span class="dot"></span>
      <span class="sl">{card_hint}　／　{card_hint2}</span><span class="sl">›</span></div>
    <div class="blk pl"><div class="bs" style="text-align:center">{card_swipe}</div></div>
    <div class="blk pl"><div class="bs" style="text-align:center">{offline}</div></div>
    """.format(mark1=c['mark1'], qr=QR,
               offline=('✈︎　' + ('沒有網路也能出示' if c['lang'] != 'en'
                                  else 'Presentable with no connection')), **ph)
    ph03 = phone(sc03, chip=c['chip'], tabs=ph['home_tabs'], on=2)

    # ── 04 特約店家 ────────────────────────────────────────────────────────
    sc04 = """
    <div class="row"><span class="chip on">{store_list}</span><span class="chip">{store_map}</span></div>
    <div class="tiny">{store_near}</div>
    <div class="blk"><div class="bh"><span class="kk">1</span><span class="bt">{store_1}</span></div>
      <div class="bs">{store_1d}</div>
      <div class="row" style="margin-top:1.2mm"><span class="cta gh" style="flex:1">{store_nav}</span><span class="cta gh" style="flex:1">{store_call}</span></div></div>
    <div class="blk"><div class="bt">{store_2}</div>
      <div class="bs">{store_2d}</div>
      <div class="cta" style="margin-top:1.2mm">{upg}</div></div>
    <div class="blk pl"><div class="bs" style="text-align:center">{more}</div></div>
    """.format(upg=('升級為付費會員' if c['lang'] != 'en' else 'Upgrade to a paid membership'),
               more=('⌖　' + ('依你的位置排序' if c['lang'] != 'en' else 'Ordered by your location')),
               **ph)
    ph04 = phone(sc04, chip=c['chip'], tabs=ph['home_tabs'], on=4)

    # ── 05 付款 ────────────────────────────────────────────────────────────
    sc05 = """
    <div class="blk"><div class="bh"><span class="kk">1</span><span class="bt">{pay_t}</span></div>
      <div class="bt" style="font-size:9pt;margin:.8mm 0">{pay_p}</div>
      <div class="bs">✓　{pay_b1}<br>✓　{pay_b2}<br>✓　{pay_b3}<br>✓　{pay_b4}</div></div>
    <div class="blk pl"><div class="bh"><span class="kk">2</span><span class="bt">{pay_who}</span></div></div>
    <div class="cta"><span class="kk" style="background:#fff;color:#E0218A">3</span>　{pay_cta}</div>
    <div class="blk" style="text-align:center;padding:2.4mm 2mm;margin-top:.6mm">
      <div class="bt">{pay_lp}</div><div class="bs" style="margin-top:1mm">{pay_lpc}</div>
      <div class="bt" style="font-size:10pt;margin:1.2mm 0">{pay_lpa}</div>
      <div class="cta dk">{pay_ok}</div><div class="tiny" style="margin-top:1.4mm">{pay_cancel}</div></div>
    <div class="blk pl" style="text-align:center"><div class="okb">{ok}</div>
      <div class="bs" style="margin-top:1mm">{pay_back}</div></div>
    """.format(**ph)
    ph05 = phone(sc05, chip='🔒 LINE Pay')

    # ── 推播表 ─────────────────────────────────────────────────────────────
    st06 = c['st06']
    push_rows = ''.join(
        '<tr><td class="n">%s</td><td><b>%s</b><div class="bs" style="margin-top:.4mm">%s</div></td>'
        '<td>%s</td><td>%s</td></tr>' % (no, name, when, who, dflt)
        for no, name, when, who, dflt in st06['rows'])

    # ── 全部畫面 S01–S23 ───────────────────────────────────────────────
    gallery = ''.join(
        '<div class="gcell%s">'
        '<div class="gcap"><span class="code">%s</span><span class="nm">%s</span></div>'
        '%s<div class="gnote">%s</div></div>'
        % (' key' if key else '', code, name,
           phone(items(spec, ph), chip=c['chip'], tabs=ph['home_tabs'], on=tab)
           if tab is not None else phone(items(spec, ph), chip=c['chip']),
           note)
        for code, name, note, tab, key, spec in c['screens'])

    # ── 權限表 ─────────────────────────────────────────────────────────────
    def cell(v):
        if v == 'y':
            return '<td class="c yes">✓</td>'
        if v == 'n':
            return '<td class="c no">✗</td>'
        return '<td class="c part">%s</td>' % v.split(':', 1)[1]

    acc_rows = ''.join(
        '<tr><td>%s</td>%s%s%s</tr>' % (feat, cell(a), cell(b), cell(d))
        for feat, a, b, d in c['acc_rows'])

    stamps = ''.join('<span class="stamp">%s<b>%s</b></span>' % s for s in c['stamps'])
    th = lambda hs: ''.join('<th>%s</th>' % h for h in hs)

    return """<!DOCTYPE html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<title>{doctitle}</title>
<style>{css}</style>
</head>
<body>

<header class="cover">
  <div class="crest">{crest_r}<div class="x">×</div>{crest_b}<div class="tx">{crest}</div></div>
  <h1>{h1}</h1>
  <p class="stand">{stand}</p>
  <div class="stamps">{stamps}</div>
</header>

<section>
  <div class="sec-head"><div class="eyebrow">{eyebrow1}</div><h2>{h2_1}</h2><p>{p1}</p></div>
  <div class="tree">{tree}</div>
</section>

<section class="np">
  <div class="sec-head"><div class="eyebrow">{eyebrow2}</div><h2>{h2_2}</h2><p>{p2}</p></div>
  {st00}
  {st01}
  {st02}
  {st03}
  {st04}
  {st05}
  <article class="station">
    <div class="sh"><div class="step">06</div><div><h3>{st06h}</h3><div class="u">{st06u}</div></div></div>
    <div class="tbl"><table><thead><tr>{st06th}</tr></thead><tbody>{st06rows}</tbody></table></div>
    <div class="admin">{st06foot}</div>
  </article>
</section>

<section class="np">
  <div class="sec-head"><div class="eyebrow">{eyebrow6}</div><h2>{h2_6}</h2><p>{p6}</p></div>
  <div class="gal">{gallery}</div>
  <div class="note"><h4>{gal_note_h}</h4><p>{gal_note}</p></div>
</section>

<section>
  <div class="sec-head"><div class="eyebrow q">{eyebrow4}</div><h2>{h2_4}</h2><p>{p4}</p></div>
  <div class="tbl tight"><table><thead><tr>{acc_th}</tr></thead><tbody>{acc_rows}</tbody></table></div>
  <div class="note"><h4>{acc_note_h}</h4><p>{acc_note}</p></div>
</section>

<div class="colophon">{colophon}</div>

</body>
</html>
""".format(
        lang=c['lang'], doctitle=c['doctitle'], css=CSS.replace('__FONT__', c['font']),
        crest_r=CREST_TCRFC, crest_b=CREST_BW, crest=c['crest'],
        h1=c['h1'], stand=c['stand'], stamps=stamps,
        eyebrow1=c['eyebrow1'], h2_1=c['h2_1'], p1=c['p1'], tree=tree,
        eyebrow2=c['eyebrow2'], h2_2=c['h2_2'], p2=c['p2'],
        st00=station('00', c['st00'], ph00),
        st01=station('01', c['st01'], ph01),
        st02=station('02', c['st02'], ph02, 'key'),
        st03=station('03', c['st03'], ph03, 'ok'),
        st04=station('04', c['st04'], ph04),
        st05=station('05', c['st05'], ph05, 'ext'),
        st06h=st06['h'], st06u=st06['u'], st06th=th(st06['th']),
        st06rows=push_rows, st06foot=st06['foot'],
        eyebrow6=c['eyebrow6'], h2_6=c['h2_6'], p6=c['p6'], gallery=gallery,
        gal_note_h=c['gal_note_h'], gal_note=c['gal_note'],
        eyebrow4=c['eyebrow4'], h2_4=c['h2_4'], p4=c['p4'],
        acc_th=th(c['acc_th']), acc_rows=acc_rows,
        acc_note_h=c['acc_note_h'], acc_note=c['acc_note'],
        colophon=c['colophon'])


files = {'zh': 'TCRFC_行動App功能說明_客戶版.html',
         'en': 'TCRFC_Mobile_App_Feature_Overview_EN.html'}
for k, fn in files.items():
    html = build(C[k])
    with io.open(os.path.join(OUT, fn), 'w', encoding='utf-8') as f:
        f.write(html)
    print('✓', fn, len(html), 'bytes')
