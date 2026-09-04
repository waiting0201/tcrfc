#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""產生 output/ 的慈善捐款站台地圖 HTML 母檔（中英雙版，內容一對一）。

    python3 output/tools/build-sitemap.py                       # 產生兩份 HTML
    node output/tools/build-pdf.mjs sitemap-zh sitemap-en       # 再轉 PDF

內容的真實來源是 output/TCRFC_慈善捐款平台功能規劃書.md；規格異動時改本檔的
C['zh'] / C['en'] 兩份資料，**不要直接改產出的 HTML**（會被下次覆蓋）。
中英內容一對一，改一邊就要改另一邊。
"""
import io, os

OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# ── 決定性假 QR：三個定位點 + 種子雜訊，避免 PDF 依賴 JS ──────────────────
def qr_svg(px=33):
    seed = 20260904
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
    clear(px - 10, px - 10, 8, 8)
    for y in range(px - 9, px - 3):
        for x in range(px - 9, px - 3):
            edge = x in (px - 9, px - 4) or y in (px - 9, px - 4)
            core = px - 7 <= x <= px - 6 and px - 7 <= y <= px - 6
            grid[y][x] = 1 if (edge or core) else 0
    rects = ''.join(
        '<rect x="%d" y="%d" width="1" height="1"/>' % (x, y)
        for y in range(px) for x in range(px) if grid[y][x]
    )
    return ('<svg class="qr" viewBox="0 0 %d %d" role="img" aria-label="QR Code %s">'
            '<rect width="%d" height="%d" fill="#fff"/><g fill="#231916">%s</g></svg>'
            % (px, px, 'placeholder', px, px, rects))

QR = qr_svg()

CSS = """
@page{ size:A4; margin:14mm 13mm 13mm; }
*{ box-sizing:border-box; margin:0; padding:0; }
:root{
  --brand:#E0218A; --brand-aa:#D61E83; --ink:#231916;
  --text:#2E2422; --muted:#6E5F5C; --faint:#9A8B88;
  --line:#E4DBDC; --line-2:#EFE8E9; --wire:#E8E1E2; --wire-2:#D9D0D1;
  --soft:#FBE7F2; --soft-line:#F2C3DD; --ok:#1F7A5C; --ok-soft:#E3F3ED;
  --warn:#9A5B00; --warn-soft:#FBEEDC;
  --font:%(font)s;
}
html{ -webkit-print-color-adjust:exact; print-color-adjust:exact; }
body{ font-family:var(--font); color:var(--text); background:#fff;
      font-size:8.4pt; line-height:1.6; }
h1,h2,h3,h4{ color:var(--ink); line-height:1.25; }
b,strong{ font-weight:700; color:var(--ink); }

/* ── 報頭 ── */
.cover{ border-bottom:2.2px solid var(--ink); padding-bottom:5mm; margin-bottom:7mm; }
.crest{ display:flex; align-items:center; gap:2.4mm; margin-bottom:4mm; }
.crest .mk{ width:11mm; height:8mm; border-radius:1.2mm; border:.6px dashed var(--wire-2);
            color:var(--faint); font-weight:700; font-size:5.6pt; display:flex;
            align-items:center; justify-content:center; letter-spacing:.1em; flex:none; }
.crest .tx{ font-size:7.4pt; letter-spacing:.14em; color:var(--muted); text-transform:uppercase; }
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

/* ── 站台地圖 ── */
.tree{ border:.6px solid var(--line); border-radius:2mm; padding:5mm 5mm 4mm;
       display:grid; grid-template-columns:repeat(3,1fr); gap:6mm; break-inside:avoid; }
.tcol h4{ font-size:6.8pt; letter-spacing:.16em; text-transform:uppercase;
          color:var(--faint); font-weight:700; padding-bottom:1.8mm;
          border-bottom:.6px solid var(--line-2); margin-bottom:2.6mm; }
.tcol.spine h4{ color:var(--brand-aa); border-bottom-color:var(--soft-line); }
.node{ padding:2mm 0; border-bottom:.5px dotted var(--line-2); }
.node:last-child{ border-bottom:0; }
.node .nm{ font-weight:700; font-size:8.4pt; color:var(--ink);
           display:flex; align-items:center; gap:1.8mm; }
.node .nm i{ font-style:normal; width:4.4mm; height:4.4mm; border-radius:1.1mm;
             background:var(--ink); color:#fff; font-size:6.2pt; font-weight:700;
             display:flex; align-items:center; justify-content:center; flex:none; }
.tcol.spine .node .nm i{ background:var(--brand); }
.node .d{ font-size:7.4pt; color:var(--muted); margin-top:.8mm; line-height:1.55; }

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
.sh .tag.no{ background:var(--soft); border-color:var(--soft-line); color:var(--brand-aa); }
.sb{ display:grid; grid-template-columns:62mm 1fr; gap:6.5mm; align-items:start; }
.lede{ color:var(--muted); margin-bottom:2.8mm; line-height:1.68; }
ul.keyed{ list-style:none; display:flex; flex-direction:column; gap:1.8mm; }
ul.keyed li{ display:grid; grid-template-columns:4.6mm 1fr; gap:2.2mm; align-items:start; }
ul.keyed .k{ width:4.6mm; height:4.6mm; border-radius:1.2mm; background:var(--wire);
             color:var(--muted); font-size:6.2pt; font-weight:700; display:flex;
             align-items:center; justify-content:center; margin-top:.5mm; }
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
.bh{ display:flex; align-items:center; gap:1.2mm; margin-bottom:1mm; }
.kk{ width:3mm; height:3mm; border-radius:.8mm; background:var(--ink); color:#fff;
     font-size:5pt; font-weight:700; display:flex; align-items:center;
     justify-content:center; flex:none; }
.bt{ font-size:7pt; font-weight:700; color:var(--ink); line-height:1.35; }
.bs{ font-size:6.3pt; color:var(--faint); line-height:1.5; }
.bars{ display:flex; flex-direction:column; gap:.8mm; margin-top:1.1mm; }
.bar{ height:.9mm; border-radius:2mm; background:var(--wire); }
.w90{ width:90%%; } .w75{ width:75%%; } .w60{ width:60%%; } .w45{ width:45%%; }
.img{ height:7.6mm; border-radius:1.2mm; margin-bottom:1.3mm;
      background:repeating-linear-gradient(135deg,var(--wire) 0 1.1mm,transparent 1.1mm 2.2mm),var(--line-2); }
.img.t{ height:10.4mm; } .img.s{ height:6mm; }
.row{ display:flex; gap:1.1mm; align-items:center; }
.chip{ border:.5px solid var(--wire-2); border-radius:1.2mm; padding:.9mm 0; flex:1;
       text-align:center; font-size:5.8pt; color:var(--muted); }
.chip.on{ background:var(--soft); border-color:var(--brand); color:var(--brand-aa); font-weight:700; }
.fld{ border:.5px solid var(--wire-2); border-radius:1.2mm; padding:1.1mm 1.5mm;
      font-size:6.2pt; color:var(--faint); background:#fff; }
.cta{ background:var(--brand); color:#fff; border-radius:1.4mm; text-align:center;
      padding:1.5mm 1mm; font-size:6.6pt; font-weight:700; }
.cta.gh{ background:transparent; border:.6px solid var(--brand-aa); color:var(--brand-aa); }
.cta.dk{ background:var(--ink); color:#fff; }
.tiny{ font-size:5.8pt; color:var(--faint); }
.okb{ display:inline-flex; align-items:center; gap:1mm; background:var(--ok-soft);
      color:var(--ok); border-radius:6mm; padding:.8mm 2.2mm; font-size:6.2pt; font-weight:700; }
.attrib{ background:var(--soft); border:.5px solid var(--soft-line); color:var(--brand-aa);
         border-radius:1.2mm; padding:1mm 1.5mm; font-size:5.8pt; font-weight:700; text-align:center; }
.flinks{ display:flex; gap:1.4mm; justify-content:center; padding-top:.6mm;
         font-size:5.6pt; color:var(--faint); align-items:center; }

/* ── 桌卡 ── */
.poster{ background:#fff; border:.6px solid var(--wire-2); border-radius:2mm;
         padding:4.5mm 4mm; text-align:center; color:var(--ink); }
.poster .pm{ width:16mm; height:8mm; border-radius:1.2mm; border:.6px dashed #D9D0D1;
             color:#9A8B88; font-weight:700; font-size:5.6pt; display:flex; align-items:center;
             justify-content:center; margin:0 auto 2.6mm; letter-spacing:.1em; }
.poster .pt{ font-size:9pt; font-weight:800; line-height:1.4; }
.poster .ps{ font-size:6.6pt; color:var(--muted); margin-top:1.2mm; }
.poster .qr{ width:30mm; height:30mm; display:block; margin:3mm auto 2.2mm; shape-rendering:crispEdges; }
.poster .pn{ font-size:7pt; font-weight:700; border-top:.5px solid var(--line);
             padding-top:2mm; margin-top:.4mm; }
.poster .pf{ font-size:5.6pt; color:var(--faint); margin-top:1mm; letter-spacing:.04em; }
.cap{ text-align:center; font-size:6.4pt; color:var(--faint); margin-top:2.4mm; }

/* ── 表格 ── */
table{ border-collapse:collapse; width:100%%; font-size:8.2pt; }
th,td{ padding:2.2mm 3mm; text-align:left; border-bottom:.5px solid var(--line-2);
       vertical-align:top; }
th{ font-size:6.8pt; letter-spacing:.14em; text-transform:uppercase; color:var(--faint);
    font-weight:700; background:var(--line-2); }
tr:last-child td{ border-bottom:0; }
td .n{ color:var(--brand-aa); font-weight:700; }
.tbl{ border:.6px solid var(--line); border-radius:2mm; overflow:hidden; break-inside:avoid; }

/* ── 支援頁面 ── */
.grid4{ display:grid; grid-template-columns:repeat(2,1fr); gap:4mm; }
.mini{ border:.6px solid var(--line); border-radius:2mm; padding:4mm;
       display:flex; flex-direction:column; gap:2.6mm; break-inside:avoid; }
.mini h4{ font-size:9.4pt; font-weight:700; }
.mini .phone{ width:36mm; }
.mini .sc{ padding:1.6mm; gap:1.2mm; }
.mini p{ font-size:8pt; color:var(--muted); line-height:1.65; }

/* ── 頁尾 ── */
.colophon{ margin-top:7mm; padding-top:3.4mm; border-top:.6px solid var(--line);
           font-size:7pt; color:var(--faint); line-height:1.8; break-inside:avoid; }
"""

# ─────────────────────────────────────────────────────────────────────────────
C = {}

C['zh'] = dict(
    lang='zh-Hant', chip='zh ▾',
    font='"PingFang TC","Noto Sans TC","Hiragino Sans","Helvetica Neue",Arial,sans-serif',
    doctitle='TCRFC 慈善捐款站台地圖',
    crest='台灣足球策略發展協會　主辦', logomark='標誌待提供',
    h1='掃一次碼，<em>八個頁面</em>就決定了這筆捐款。',
    stand='這份文件把慈善捐款平台的每一頁攤開來看：客人在店裡掃到 QR Code 之後，會經過哪些畫面、'
          '每個畫面上放什麼、哪些欄位由後台維護。畫面為<b>功能示意</b>，用來確認架構與流程是否正確，'
          '尚未進入視覺設計。',
    stamps=[('依據　','慈善捐款平台功能規劃書 v1.5'),('前台頁面　','8 頁　×　中英雙語'),
            ('系統信　','4 封'),('後台模組　','N1–N7（共用官網後台）')],
    eyebrow1='Sitemap', h2_1='站台地圖：三種頁面，只有一條主線',
    p1='八個頁面分成三組。中間那一組是<b>捐款主幹</b>——客人真正會走完的路；左右兩組是入口與支援頁面。'
       '每一頁都有繁體中文與英文兩個版本。',
    tcols=[
      ('入口', False, [
        ('A','實體 QR Code','主要入口。店內桌卡或貼紙，每家合作店家一組專屬 QR Code，捐款會歸屬到這家店。'),
        ('B','一般入口','沒有店家歸屬的直接入口，例如從台中磐石官網「慈善與社會影響」頁點進來。')]),
      ('捐款主幹', True, [
        ('1','掃碼落地頁','顯示店家名稱＋捐款項目卡片牆。'),
        ('2','項目詳情頁','項目說明＋款項用途，<b>捐款表單在頁面最下方</b>。'),
        ('3','LINE Pay 付款','跳出本站，於 LINE Pay 完成付款後自動返回。'),
        ('4','結果頁','成功／未完成／處理中三種狀態。')]),
      ('支援頁面', False, [
        ('C','捐款徵信','具名捐款人名單，只有姓名、沒有金額。'),
        ('D','成果回顧','摘要慈善計畫成果，導回台中磐石官網的完整紀錄。'),
        ('E','隱私權政策','獨立網域須自有一份，涵蓋捐款人個資與發票資料。'),
        ('F','捐款須知','發票規則、款項用途說明。')])],
    eyebrow2='Donor journey', h2_2='捐款動線：客人實際會看到的畫面',
    p2='以下依順序走過一次。畫面中的編號對應右側的說明條列。畫面內的店名、項目名稱與金額皆為示意，'
       '實際內容由後台維護。',
    poster=dict(pt='吃一碗麵，<br>幫一個孩子留在球場上', ps='掃碼支持協會的公益計畫',
                pn='好味小館　和平店', pf='台灣足球策略發展協會', cap='桌卡示意（店名為範例，標誌待協會提供）'),
    st00=dict(h='實體入口：店內 QR Code', u='每家合作店家一組專屬 QR Code', tag='印刷品，非網頁',
      lede='每家合作店家拿到的是<b>專屬於自己的網址</b>，因此這家店帶進來的捐款可以被算出來、'
           '回饋金也才算得出來。後台可產出 PNG、SVG 與含店名的印刷版 PDF。',
      items=[('✓','網址不可由店家編號推算','避免有人猜出其他店家的網址；網址上也不會帶金額或分潤比例，不能被竄改。'),
             ('✓','容錯等級 Q（25%）、碼區至少 3×3 公分','印刷品常有油污與局部遮擋，留足容錯與四周留白。'),
             ('!','標誌等協會提供，現在一律留佔位','不得沿用台中磐石的標誌，也不得自行為協會排字造標。桌卡上的方框就是佔位。'),
             ('!','重新產生網址會讓舊 QR 立刻失效','後台會明確警示並記錄操作者；已印出去的桌卡需一併回收。')],
      admin='<b>後台維護</b>　N1 店家管理與 QR Code'),
    st01=dict(h='掃碼落地頁', u='客人掃碼後看到的第一頁', tag='100% 手機情境',
      lede='客人掃碼後看到的第一頁。最重要的一件事是<b>先讓客人認出「這是我正在吃飯的那家店」</b>，'
           '再往下選項目——店名放在最上面就是為了這個。',
      items=[('1','店家識別','店名必填，Logo 或照片選填。店家沒放 Logo 時降級為純文字的店名區塊，不會留一個空框。'),
             ('2','平台說明','一段話講清楚這個平台在做什麼、錢會怎麼用。後台可編輯。'),
             ('3','項目卡片牆','封面圖、項目名稱、一句話說明、「了解並捐款」按鈕。<b>不顯示募款進度</b>。'),
             ('4','信任區塊','協會簡介、立案字號、成立日期、會址與統編，並連回台中磐石官網的公益成果紀錄。'),
             ('5','頁尾','隱私權政策、捐款須知、聯絡方式。')],
      admin='<b>兩個行為要注意</b><br>店家若已停止合作，這個網址<b>仍然打得開</b>，只是不顯示店家區塊，'
            '等同一般入口——已印出去的桌卡不會變成死連結。<br>首屏不依賴大圖，確保行動網路下快速開啟。'
            '<br><br><b>後台維護</b>　N1 店家　／　N2 捐款項目　／　N7 站台文案'),
    st02=dict(h='項目詳情頁', u='捐款表單在這一頁的最下方', tag='最長的一頁',
      lede='這一頁刻意<b>把捐款表單放在最下方</b>：先把項目說清楚、把錢的用途講明白，客人讀完了再捐。'
           '順序本身就是設計的一部分。<b>頁面上沒有募款進度</b>——捐款人看到的是項目在做什麼，不是還差多少。',
      items=[('1','封面與標題','項目名稱與一句話訴求。'),
             ('2','項目說明','後台以區塊編輯器排版，支援圖文、引言、清單。'),
             ('3','款項用途','明列這筆錢會用在哪裡。'),
             ('4','關聯的慈善計畫','選填。連向官網對應的計畫頁，讓捐款人看得到過往成果。'),
             ('5','捐款表單','金額選項卡由後台逐項目設定，可設單筆最低與最高金額。姓名與 Email 必填，'
                          'Email 即時驗證格式。按鈕上直接寫出實際金額，避免誤按。'),
             ('6','常見問題','發票、款項用途。')],
      admin='<b>店家歸屬怎麼一路帶到底</b><br>從落地頁點進來時，網址會帶上店家代碼，同時寫進一個 24 小時的'
            '短期 Cookie。頁面中段那條粉紅色提示「您正透過好味小館捐款」，就是讓客人知道歸屬關係，但不喧賓奪主。'
            '<br>若真的抓不到店家，<b>捐款照樣成立</b>，只是歸類為「無店家歸屬」、店家回饋金為 0，'
            '絕不會中斷捐款流程。<br><br><b>後台維護</b>　N2 捐款項目（金額級距、發票模式）'),
    st03=dict(h='LINE Pay 付款', u='站外頁面，由 LINE Pay 提供', tag='離開本站',
      lede='這一頁不是我們做的，畫面由 LINE Pay 提供。我們負責的是<b>離開前與返回後</b>這兩件事。',
      items=[('→','離開前先建立捐款單','表單內容在導向 LINE Pay 之前就保存起來。'),
             ('←','付款失敗返回時不得要求重填','客人按了「重試」是沿用原本那張捐款單，不會重複建單。'),
             ('!','同一筆單重複確認不會重複扣款','重複確認不會重複入帳、重複開發票或重複寄信。這是硬性要求。'),
             ('!','「已扣款但確認失敗」不會被靜默丟掉','會進到後台的異常佇列並主動通知，由人工處理。')],
      admin='<b>後台維護</b>　N3 捐款紀錄（含異常佇列）'),
    st04=dict(h='結果頁', u='付款完成後返回本站的頁面', tag='不被搜尋引擎收錄',
      lede='三種狀態共用同一個網址：<b>成功</b>、<b>未完成</b>、以及款項尚在確認中的<b>處理中</b>。',
      items=[('1','成功','感謝訊息、捐款單號、金額、項目、發票或收據的開立狀態、分享按鈕、返回入口。'),
             ('2','發票狀態寫在畫面上','不必讓捐款人自己去信箱找。Email 一律遮罩，頁面不顯示完整個資。'),
             ('3','未完成','說明可能原因、提供重試按鈕與聯絡方式。'),
             ('!','處理中不能無限轉圈','自動輪詢，但逾時上限與逾時後的文案要事先定義；文案不得讓人誤以為失敗而重複付款。')],
      admin='<b>後台維護</b>　N3 捐款紀錄　／　N5 發票與收據管理'),
    st05=dict(h='系統信：四封，不多也不少', u='寄送紀錄一律寫入官網既有的寄信紀錄',
      th=['#','信件','寄給誰','什麼時候寄'],
      rows=[('01','捐款感謝信','含單號、金額、項目與款項用途','捐款人','付款成功後立即'),
            ('02','電子發票／捐贈收據通知','含發票號碼或收據編號與檢視連結','捐款人','開立成功後'),
            ('03','開立失敗通知','提醒後台人員人工補開','協會','發票開立失敗時'),
            ('04','退款通知','','捐款人','後台完成退款後')]),
    eyebrow3='Supporting pages', h2_3='其餘四頁', p3='不在主動線上，但每一頁都有它非有不可的理由。',
    minis=[('捐款徵信','只列出選擇「具名」的捐款人姓名。<b>不顯示金額、不顯示 Email、不顯示店家</b>，'
                     '可依項目與期間篩選。後台可整站關閉，也可逐筆隱藏。'),
           ('成果回顧','摘要呈現關聯的慈善計畫成果，並導回台中磐石官網「慈善事蹟」的完整紀錄。'
                     '<b>成果內容的主檔在俱樂部官網，不在這裡重複維護。</b>'),
           ('隱私權政策','獨立網域必須自有一份，不能只連回官網。內容需涵蓋<b>捐款人個資與發票資料</b>的'
                     '蒐集目的與保存期限。捐款表單的同意勾選會連到這一頁。'),
           ('捐款須知','發票規則與款項用途說明。捐款前的常見疑問集中在這裡，項目詳情頁的常見問題會連過來。')],
    mini_screens=[
      [('row',[('全部項目',True),('2026',False)]),('bs','陳○○<br>林○○<br>王○○<br>好味小館員工'),
       ('tiny','僅列具名捐款人')],
      [('img','s'),('bt','2025 台中磐石盃'),('bars',['w90','w60']),('cta','看台中磐石官網紀錄')],
      [('bt','個人資料蒐集告知'),('bars',['w90','w75','w90','w45'])],
      [('bt','發票規則'),('bars',['w75']),('bt','款項用途'),('bars',['w90'])]],
    colophon='台灣足球策略發展協會　·　慈善捐款平台<br>'
             '依據《慈善捐款平台功能規劃書》v1.5　·　前台 8 頁　·　中英雙語，架構預留第三語系<br>'
             '<b>協會標誌尚未提供，文件中以虛線方框佔位。</b>本文件的用色為說明文件用色，非平台最終視覺。',
    # 手機畫面文案
    p01=dict(store='好味小館　和平店', thanks='感謝好味小館與協會一起做公益',
             about='關於這個平台', pick='選擇想支持的項目',
             pj1='學童足球鞋支援計畫', pj1s='讓孩子有一雙合腳的球鞋',
             pj2='偏鄉球隊交通補助', cta='了解並捐款',
             trust='關於協會', trusts='台內團字第 1150283692 號、114/10/08 成立、會址、統編（待補）',
             foot='隱私權　捐款須知　聯絡我們'),
    p02=dict(title='學童足球鞋支援計畫', sub='讓孩子有一雙合腳的球鞋',
             desc='項目說明', use='款項用途', link='關聯的慈善計畫', links='→ 連向官網的計畫頁',
             attrib='您正透過「好味小館」捐款', form='捐款表單', other='其他金額',
             name='姓名（必填）', mail='Email（必填）', named='具名', anon='匿名',
             inv='發票／收據欄位', consent='☐ 我已閱讀並同意個資使用說明',
             cta='以 LINE Pay 捐款 NT$ 300', faq='常見問題', faqs='發票、款項用途'),
    p03=dict(brand='LINE Pay', club='台灣足球策略發展協會', amt='NT$ 300',
             cta='確認付款', cancel='取消', note='付款完成後自動返回捐款平台'),
    p04=dict(ok='✓ 捐款成功', thanks='謝謝你，陳○○', body='你的心意會用在「學童足球鞋支援計畫」',
             detail='捐款明細', lines='單號　D26090400412<br>金額　NT$ 300<br>項目　學童足球鞋支援計畫',
             inv='電子發票', invs='開立後將寄至 ch****@gmail.com',
             share='分享', back='回到捐款首頁'),
)

C['en'] = dict(
    lang='en', chip='en ▾',
    font='"Helvetica Neue",Helvetica,Arial,"PingFang TC","Noto Sans TC",sans-serif',
    doctitle='TCRFC Charity Donation Sitemap',
    crest='Organised by 台灣足球策略發展協會', logomark='LOGO TBC',
    h1='One scan, <em>eight pages</em>, and the donation is made.',
    stand='This document lays out every page of the charity donation platform: what a customer sees after '
          'scanning the QR code in a venue, what sits on each screen, and which fields the admin maintains. '
          'The screens are <b>functional wireframes</b> for confirming structure and flow. Visual design has '
          'not started.',
    stamps=[('Based on　','Charity Donation Platform Specification v1.5'),
            ('Public pages　','8　×　Chinese and English'),
            ('System emails　','4'),('Admin modules　','N1–N7 (shared club admin)')],
    eyebrow1='Sitemap', h2_1='Sitemap: three groups of pages, one main line',
    p1='The eight pages fall into three groups. The middle group is the <b>donation line</b> — the route a '
       'customer actually walks. The other two are entry points and supporting pages. Every page exists in '
       'Traditional Chinese and English.',
    tcols=[
      ('Entry', False, [
        ('A','Printed QR code','The main entry. A table card or sticker in the venue; every partner venue '
         'gets its own QR code, and donations are attributed to that venue.'),
        ('B','General entry','A direct entry with no venue attached — for example from the Taichung Rock FC '
         'site’s charity section.')]),
      ('Donation line', True, [
        ('1','Scan landing page','The venue name plus a wall of project cards.'),
        ('2','Project page','Description and use of funds, with the <b>donation form at the very bottom</b>.'),
        ('3','LINE Pay','Leaves the site; returns automatically once payment is complete.'),
        ('4','Result page','Three states: complete, not completed, still processing.')]),
      ('Supporting', False, [
        ('C','Donor roll','Named donors only — names, never amounts.'),
        ('D','Impact','A summary of delivered charity work, linking back to the full record on the Taichung Rock FC site.'),
        ('E','Privacy policy','A separate domain needs its own, covering donor data and invoice data.'),
        ('F','Donation terms','Invoice rules and how funds are used.')])],
    eyebrow2='Donor journey', h2_2='The donation journey, screen by screen',
    p2='What follows is one pass through, in order. The numbers on each screen match the notes beside it. '
       'Venue names, project names and amounts are illustrative; the real content is maintained in the admin.',
    poster=dict(pt='One bowl of noodles<br>keeps a child on the pitch',
                ps='Scan to support the Association’s charity work',
                pn='Haowei Diner — Heping', pf='台灣足球策略發展協會',
                cap='Table card, illustrative (venue name is an example; logo to be supplied)'),
    st00=dict(h='Physical entry: the QR code in the venue', u='Every partner venue gets its own QR code',
      tag='print, not a web page',
      lede='Every partner venue gets a <b>URL of its own</b>, which is what makes the donations it brings in '
           'measurable and its rebate calculable. The admin produces PNG, SVG and a print-ready PDF carrying '
           'the venue name.',
      items=[('✓','The URL cannot be derived from a venue number',
              'So nobody can guess another venue’s address. The URL also carries no amount or share '
              'percentage, so there is nothing worth tampering with.'),
             ('✓','Error correction level Q (25%), code area at least 3×3 cm',
              'Printed material picks up grease and partial cover; leave the correction and the quiet zone.'),
             ('!','The logo is a placeholder until the Association supplies one',
              'Do not reuse the Taichung Rock FC lockups, and do not typeset one for the Association. The box on the card is the placeholder.'),
             ('!','Reissuing a URL kills the old QR code immediately',
              'The admin warns explicitly and records who did it; cards already printed have to be collected.')],
      admin='<b>Admin</b>　N1 Venues and QR codes'),
    st01=dict(h='Scan landing page', u='The first page after scanning', tag='100% mobile',
      lede='The first thing a customer sees. What matters most is that they <b>recognise the place they are '
           'sitting in</b> before choosing a project — which is why the venue name sits at the top.',
      items=[('1','Venue identity','The name is required; a logo or photo is optional. With no logo it falls '
              'back to a plain text name block rather than leaving an empty frame.'),
             ('2','About the platform','A short passage on what this platform does and where the money goes. '
              'Editable in the admin.'),
             ('3','Project card wall','Cover image, project name, one-line description, and a "read more and '
              'donate" button. <b>No fundraising progress.</b>'),
             ('4','Trust block','The Association\u2019s introduction, its registration number, date of '
              'establishment, address and tax ID, and a link back to the charity record on the TCRFC site.'),
             ('5','Footer','Privacy policy, donation terms, contact.')],
      admin='<b>Two behaviours to note</b><br>If the partnership has ended, the URL <b>still opens</b>; it '
            'simply drops the venue block and behaves as the general entry — cards already printed never '
            'become dead links.<br>The first screen must not depend on a large image, so it opens fast on '
            'mobile data.<br><br><b>Admin</b>　N1 Venues　/　N2 Projects　/　N7 Site copy'),
    st02=dict(h='Project page', u='The donation form sits at the bottom of this page', tag='the longest page',
      lede='This page deliberately <b>puts the donation form last</b>: explain the project, be explicit about '
           'where the money goes, and let the reader decide at the end. The order is part of the design. '
           '<b>There is no fundraising progress</b> — the donor sees what the project does, not how far short '
           'it is.',
      items=[('1','Cover and title','Project name and a one-line appeal.'),
             ('2','Project description','Laid out in the admin with a block editor: rich text, images, pull '
              'quotes, lists.'),
             ('3','Use of funds','An explicit statement of where the money goes.'),
             ('4','Linked charity programme','Optional. Links to the matching programme page on the club site '
              'so donors can see past results.'),
             ('5','Donation form','Amount chips are set per project in the admin, along with a minimum and '
              'maximum per donation. Name and email are required, with the email format validated as it is '
              'typed. The button states the actual amount, so nobody taps it by accident.'),
             ('6','FAQ','Invoices, use of funds.')],
      admin='<b>How venue attribution survives the journey</b><br>Arriving from the landing page, the URL '
            'carries the venue code and it is also written to a short-lived 24-hour cookie. The pink line '
            'mid-page — "you are donating via Haowei Diner" — tells the donor about the attribution without '
            'overshadowing the project.<br>If the venue genuinely cannot be resolved, <b>the donation still '
            'goes through</b>: it is recorded as having no venue, the venue rebate is zero, and the flow is '
            'never interrupted.<br><br><b>Admin</b>　N2 Projects (amount chips, invoice mode)'),
    st03=dict(h='LINE Pay', u='An external page, provided by LINE Pay', tag='leaves the site',
      lede='This page is not ours; LINE Pay provides it. What we own is <b>what happens before we leave and '
           'after we come back</b>.',
      items=[('→','The donation record is created before leaving','The form contents are saved before the '
              'redirect to LINE Pay.'),
             ('←','A failed payment must never force a retype','"Try again" reuses the original donation '
              'record rather than creating a second one.'),
             ('!','Confirming the same order twice never charges twice','No double posting, no duplicate '
              'invoice, no duplicate email. This is a hard requirement.'),
             ('!','"Charged but confirmation failed" is never dropped silently','It goes into the admin '
              'exception queue and raises an alert for someone to handle.')],
      admin='<b>Admin</b>　N3 Donation records (including the exception queue)'),
    st04=dict(h='Result page', u='Where the donor lands on returning from payment', tag='not indexed',
      lede='Three states share one address: <b>complete</b>, <b>not completed</b>, and <b>processing</b> while '
           'the payment is still being confirmed.',
      items=[('1','Complete','Thanks, order number, amount, project, the status of the invoice or receipt, a '
              'share button and a way back.'),
             ('2','Invoice status is on the page','So the donor does not have to go hunting in their inbox. '
              'The email address is masked; no full personal data is displayed.'),
             ('3','Not completed','Likely reasons, a retry button, and contact details.'),
             ('!','Processing must not spin forever','It polls automatically, but the timeout and the copy '
              'after it must be defined in advance — and the copy must never read as failure, or people pay '
              'twice.')],
      admin='<b>Admin</b>　N3 Donation records　/　N5 Invoices and receipts'),
    st05=dict(h='System emails: four, no more', u='Every send is written to the club’s existing email log',
      th=['#','Email','To','When'],
      rows=[('01','Donation thank-you','Order number, amount, project and use of funds','Donor',
             'Immediately after payment'),
            ('02','E-invoice or receipt notice','Invoice or receipt number and a link to view it','Donor',
             'Once issued'),
            ('03','Issuing failure notice','Prompts staff to issue it manually','The Association',
             'When issuing fails'),
            ('04','Refund notice','','Donor','After a refund is completed in the admin')]),
    eyebrow3='Supporting pages', h2_3='The other four pages',
    p3='Off the main line, but each of them earns its place.',
    minis=[('Donor roll','Lists only donors who chose to be named. <b>No amounts, no email addresses, no '
                        'venues.</b> Filterable by project and period. The admin can switch the whole page '
                        'off, or hide individual entries.'),
           ('Impact','A summary of the linked charity programmes, leading back to the full record on the club '
                     'site. <b>The master content lives on the club site and is not maintained twice.</b>'),
           ('Privacy policy','A separate domain must carry its own — a link back to the club site is not '
                     'enough. It has to cover the purpose and retention period for <b>donor data and invoice '
                     'data</b>. The consent checkbox on the donation form links here.'),
           ('Donation terms','Invoice rules and how funds are used. The questions people have before donating '
                     'live here, and the project page FAQ links across.')],
    mini_screens=[
      [('row',[('All projects',True),('2026',False)]),('bs','Ms Chen<br>Mr Lin<br>Ms Wang<br>Haowei Diner staff'),
       ('tiny','Named donors only')],
      [('img','s'),('bt','2025 Taichung Rock Cup'),('bars',['w90','w60']),('cta','Full record on the TCRFC site')],
      [('bt','Personal data notice'),('bars',['w90','w75','w90','w45'])],
      [('bt','Invoice rules'),('bars',['w75']),('bt','Use of funds'),('bars',['w90'])]],
    colophon='台灣足球策略發展協會　·　Charity Donation Platform<br>'
             'Based on the Charity Donation Platform Functional Specification v1.5　·　8 public pages　·　'
             'Chinese and English, with room for a third language<br>'
             '<b>The Association’s logo has not been supplied; a dashed box stands in for it.</b> The colours here belong to this document, not to the platform’s final design.',
    p01=dict(store='Haowei Diner — Heping', thanks='Thank you, Haowei Diner, for supporting the Association’s charity work',
             about='About this platform', pick='Choose a project to support',
             pj1='Football boots for schoolchildren', pj1s='So every child has boots that fit',
             pj2='Travel support for rural teams', cta='Read more and donate',
             trust='About the Association', trusts='Reg. 台內團字第 1150283692 號, established 2025-10-08, address, tax ID (pending)',
             foot='Privacy　Terms　Contact'),
    p02=dict(title='Football boots for schoolchildren', sub='So every child has boots that fit',
             desc='Project description', use='Use of funds', link='Linked charity programme',
             links='→ links to the club site', attrib='You are donating via Haowei Diner',
             form='Donation form', other='Other amount', name='Name (required)', mail='Email (required)',
             named='Named', anon='Anonymous', inv='Invoice / receipt fields',
             consent='☐ I have read and accept the privacy notice',
             cta='Donate NT$300 with LINE Pay', faq='FAQ', faqs='Invoices, use of funds'),
    p03=dict(brand='LINE Pay', club='台灣足球策略發展協會', amt='NT$ 300',
             cta='Confirm payment', cancel='Cancel', note='Returns to the donation site automatically'),
    p04=dict(ok='✓ Donation complete', thanks='Thank you, Ms Chen',
             body='Your gift goes to "Football boots for schoolchildren"',
             detail='Donation details',
             lines='Order　D26090400412<br>Amount　NT$ 300<br>Project　Football boots for schoolchildren',
             inv='E-invoice', invs='Will be sent to ch****@gmail.com once issued',
             share='Share', back='Back to donations'),
)


def keyed(items):
    return '<ul class="keyed">' + ''.join(
        '<li><span class="k">%s</span><span><b>%s</b><span class="sub">%s</span></span></li>' % it
        for it in items) + '</ul>'


def station(step, d, phone, tagcls=''):
    tag = '<span class="tag %s">%s</span>' % (tagcls, d['tag']) if d.get('tag') else ''
    return """<article class="station">
  <div class="sh"><div class="step">%s</div><div><h3>%s</h3><div class="u">%s</div></div>%s</div>
  <div class="sb"><div>%s</div><div><p class="lede">%s</p>%s<div class="admin">%s</div></div></div>
</article>""" % (step, d['h'], d['u'], tag, phone, d['lede'], keyed(d['items']), d['admin'])


def build(c):
    p01, p02, p03, p04 = c['p01'], c['p02'], c['p03'], c['p04']

    ph01 = """<div class="phone"><div class="screen">
  <div class="sbar"><span class="ub"></span><span>%s</span></div>
  <div class="sc">
    <div class="blk"><div class="bh"><span class="kk">1</span><span class="bt">%s</span></div>
      <div class="bs">%s</div></div>
    <div class="blk pl"><div class="bh"><span class="kk">2</span><span class="bt">%s</span></div>
      <div class="bars"><span class="bar w90"></span><span class="bar w75"></span></div></div>
    <div class="blk"><div class="bh"><span class="kk">3</span><span class="bt">%s</span></div>
      <div class="img"></div><div class="bt">%s</div><div class="bs">%s</div>
      <div class="cta" style="margin-top:1.3mm">%s</div></div>
    <div class="blk"><div class="img s"></div><div class="bt">%s</div>
      <div class="cta gh" style="margin-top:1.3mm">%s</div></div>
    <div class="blk pl"><div class="bh"><span class="kk">4</span><span class="bt">%s</span></div>
      <div class="bs">%s</div></div>
    <div class="flinks"><span class="kk">5</span><span>%s</span></div>
  </div></div></div>""" % (c['chip'], p01['store'], p01['thanks'], p01['about'], p01['pick'],
                           p01['pj1'], p01['pj1s'], p01['cta'], p01['pj2'], p01['cta'],
                           p01['trust'], p01['trusts'], p01['foot'])

    ph02 = """<div class="phone"><div class="screen">
  <div class="sbar"><span class="ub"></span><span>%s</span></div>
  <div class="sc">
    <div><div class="img t"></div><div class="bh"><span class="kk">1</span><span class="bt">%s</span></div>
      <div class="bs">%s</div></div>
    <div class="blk pl"><div class="bh"><span class="kk">2</span><span class="bt">%s</span></div>
      <div class="bars"><span class="bar w90"></span><span class="bar w75"></span><span class="bar w90"></span><span class="bar w45"></span></div></div>
    <div class="blk pl"><div class="bh"><span class="kk">3</span><span class="bt">%s</span></div>
      <div class="bars"><span class="bar w75"></span><span class="bar w60"></span></div></div>
    <div class="blk pl"><div class="bh"><span class="kk">4</span><span class="bt">%s</span></div>
      <div class="bs">%s</div></div>
    <div class="attrib">%s</div>
    <div class="blk"><div class="bh"><span class="kk">5</span><span class="bt">%s</span></div>
      <div class="row" style="margin-bottom:1.1mm"><span class="chip">100</span><span class="chip on">300</span><span class="chip">500</span><span class="chip">1000</span></div>
      <div class="fld" style="margin-bottom:1.1mm">%s</div>
      <div class="fld" style="margin-bottom:1.1mm">%s</div>
      <div class="fld" style="margin-bottom:1.1mm">%s</div>
      <div class="row" style="margin-bottom:1.1mm"><span class="chip on">%s</span><span class="chip">%s</span></div>
      <div class="fld" style="margin-bottom:1.1mm">%s</div>
      <div class="bs" style="margin-bottom:1.3mm">%s</div>
      <div class="cta">%s</div></div>
    <div class="blk pl"><div class="bh"><span class="kk">6</span><span class="bt">%s</span></div>
      <div class="bs">%s</div></div>
  </div></div></div>""" % (c['chip'], p02['title'], p02['sub'], p02['desc'], p02['use'], p02['link'],
                           p02['links'], p02['attrib'], p02['form'], p02['other'], p02['name'],
                           p02['mail'], p02['named'], p02['anon'], p02['inv'], p02['consent'],
                           p02['cta'], p02['faq'], p02['faqs'])

    ph03 = """<div class="phone"><div class="screen">
  <div class="sbar"><span class="ub"></span><span>%s</span></div>
  <div class="sc" style="padding:3.2mm 2.2mm">
    <div class="blk" style="text-align:center;padding:3.2mm 2mm">
      <div class="bt">%s</div><div class="bs" style="margin-top:1.6mm">%s</div>
      <div class="bt" style="font-size:11pt;margin:1.6mm 0">%s</div>
      <div class="cta dk">%s</div><div class="tiny" style="margin-top:1.8mm">%s</div></div>
    <div class="blk pl" style="text-align:center"><div class="bs">%s</div></div>
  </div></div></div>""" % ('🔒 LINE Pay', p03['brand'], p03['club'], p03['amt'],
                           p03['cta'], p03['cancel'], p03['note'])

    ph04 = """<div class="phone"><div class="screen">
  <div class="sbar"><span class="ub"></span><span>%s</span></div>
  <div class="sc">
    <div class="blk" style="text-align:center"><div class="okb">%s</div>
      <div class="bt" style="margin-top:1.8mm">%s</div>
      <div class="bs" style="margin-top:.8mm">%s</div></div>
    <div class="blk"><div class="bh"><span class="kk">1</span><span class="bt">%s</span></div>
      <div class="bs">%s</div></div>
    <div class="blk"><div class="bh"><span class="kk">2</span><span class="bt">%s</span></div>
      <div class="bs">%s</div></div>
    <div class="row"><div class="cta gh" style="flex:1">%s</div><div class="cta" style="flex:1">%s</div></div>
  </div></div></div>""" % (c['chip'], p04['ok'], p04['thanks'], p04['body'], p04['detail'],
                           p04['lines'], p04['inv'], p04['invs'], p04['share'], p04['back'])

    poster = """<div><div class="poster"><div class="pm">%s</div>
  <div class="pt">%s</div><div class="ps">%s</div>%s
  <div class="pn">%s</div><div class="pf">%s</div></div>
  <p class="cap">%s</p></div>""" % (c['logomark'], c['poster']['pt'], c['poster']['ps'], QR,
                                    c['poster']['pn'], c['poster']['pf'], c['poster']['cap'])

    tree = ''.join(
        '<div class="tcol%s"><h4>%s</h4>%s</div>' % (
            ' spine' if spine else '', head,
            ''.join('<div class="node"><div class="nm"><i>%s</i>%s</div><div class="d">%s</div></div>'
                    % n for n in nodes))
        for head, spine, nodes in c['tcols'])

    mail_rows = ''.join(
        '<tr><td class="n">%s</td><td><b>%s</b>%s</td><td>%s</td><td>%s</td></tr>' % (
            r[0], r[1], ('<div class="bs" style="margin-top:.4mm">%s</div>' % r[2]) if r[2] else '',
            r[3], r[4])
        for r in c['st05']['rows'])

    def mini_screen(blocks):
        out = []
        for kind, val in blocks:
            if kind == 'row':
                out.append('<div class="row">' + ''.join(
                    '<span class="chip%s">%s</span>' % (' on' if on else '', t) for t, on in val) + '</div>')
            elif kind == 'bs':
                out.append('<div class="blk pl"><div class="bs">%s</div></div>' % val)
            elif kind == 'tiny':
                out.append('<div class="tiny" style="text-align:center">%s</div>' % val)
            elif kind == 'img':
                out.append('<div class="img %s"></div>' % val)
            elif kind == 'bt':
                out.append('<div class="bt">%s</div>' % val)
            elif kind == 'bars':
                out.append('<div class="bars">' + ''.join('<span class="bar %s"></span>' % w for w in val) + '</div>')
            elif kind == 'cta':
                out.append('<div class="cta gh">%s</div>' % val)
        return '<div class="phone"><div class="screen"><div class="sbar"><span class="ub"></span></div>' \
               '<div class="sc">%s</div></div></div>' % ''.join(out)

    minis = ''.join(
        '<div class="mini"><h4>%s</h4>%s<p>%s</p></div>' % (t, mini_screen(s), body)
        for (t, body), s in zip(c['minis'], c['mini_screens']))

    stamps = ''.join('<span class="stamp">%s<b>%s</b></span>' % s for s in c['stamps'])

    return """<!DOCTYPE html>
<html lang="%(lang)s">
<head>
<meta charset="utf-8">
<title>%(doctitle)s</title>
<style>%(css)s</style>
</head>
<body>

<header class="cover">
  <div class="crest"><div class="mk">%(logomark)s</div><div class="tx">%(crest)s</div></div>
  <h1>%(h1)s</h1>
  <p class="stand">%(stand)s</p>
  <div class="stamps">%(stamps)s</div>
</header>

<section>
  <div class="sec-head"><div class="eyebrow">%(eyebrow1)s</div><h2>%(h2_1)s</h2><p>%(p1)s</p></div>
  <div class="tree">%(tree)s</div>
</section>

<section class="np">
  <div class="sec-head"><div class="eyebrow">%(eyebrow2)s</div><h2>%(h2_2)s</h2><p>%(p2)s</p></div>
  %(st00)s
  %(st01)s
  %(st02)s
  %(st03)s
  %(st04)s
  <article class="station">
    <div class="sh"><div class="step">05</div><div><h3>%(st05h)s</h3><div class="u">%(st05u)s</div></div></div>
    <div class="tbl"><table><thead><tr>%(mailhead)s</tr></thead><tbody>%(mailrows)s</tbody></table></div>
  </article>
</section>

<section>
  <div class="sec-head"><div class="eyebrow q">%(eyebrow3)s</div><h2>%(h2_3)s</h2><p>%(p3)s</p></div>
  <div class="grid4">%(minis)s</div>
</section>

<div class="colophon">%(colophon)s</div>

</body>
</html>
""" % dict(
        lang=c['lang'], doctitle=c['doctitle'], css=CSS % {'font': c['font']},
        crest=c['crest'], logomark=c['logomark'], h1=c['h1'], stand=c['stand'], stamps=stamps,
        eyebrow1=c['eyebrow1'], h2_1=c['h2_1'], p1=c['p1'], tree=tree,
        eyebrow2=c['eyebrow2'], h2_2=c['h2_2'], p2=c['p2'],
        st00=station('00', c['st00'], poster, 'ext'),
        st01=station('01', c['st01'], ph01),
        st02=station('02', c['st02'], ph02),
        st03=station('03', c['st03'], ph03, 'ext'),
        st04=station('04', c['st04'], ph04, 'no'),
        st05h=c['st05']['h'], st05u=c['st05']['u'],
        mailhead=''.join('<th>%s</th>' % h for h in c['st05']['th']), mailrows=mail_rows,
        eyebrow3=c['eyebrow3'], h2_3=c['h2_3'], p3=c['p3'], minis=minis,
        colophon=c['colophon'])


files = {'zh': 'TCRFC_慈善捐款站台地圖.html', 'en': 'TCRFC_Charity_Donation_Sitemap_EN.html'}
for k, fn in files.items():
    html = build(C[k])
    with io.open(os.path.join(OUT, fn), 'w', encoding='utf-8') as f:
        f.write(html)
    print('✓', fn, len(html), 'bytes')
