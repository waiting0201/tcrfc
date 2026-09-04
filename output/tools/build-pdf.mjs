#!/usr/bin/env node
// output/ 交付物的 PDF 產出。
//   規劃書：Markdown → 品牌樣式 HTML → PDF（中英雙版共用樣式）
//   里程碑／站台地圖：既有的 HTML 交付物直接 → PDF（尺寸與方向由各自的 @page 決定）
//
//   node output/tools/build-pdf.mjs                        # 全部
//   node output/tools/build-pdf.mjs zh en mile-zh sitemap-zh  # 指定項目
//
// 相依：marked（output/tools/node_modules，已 gitignore；重裝跑 npm i --prefix output/tools）
// 排版：本機 Chrome headless，透過 CDP Page.printToPDF 才能設定頁首頁尾樣板。
// 色彩基準：reference/TCR_logo_CMYK.ai（品牌桃紅 #E0218A、品牌黑 #231916）。
import { readFile, writeFile, unlink } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { marked } from 'marked';

const HERE = dirname(fileURLToPath(import.meta.url));
const OUT = resolve(HERE, '..');

const CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';

const DOCS = {
  zh: {
    md: 'TCRFC_前後台功能規劃書.md',
    pdf: 'TCRFC_前後台功能規劃書.pdf',
    lang: 'zh-Hant',
    club: '台中磐石足球俱樂部',
    title: ['官方網站', '前後台功能規劃書'],
    promise: 'LOCAL ROOTS. GLOBAL PATHWAYS.　在地扎根 · 放眼世界',
    labels: { version: '文件版本', date: '建立日期' },
    runningHead: (v) => `TCRFC 官方網站前後台功能規劃書 ${v}`,
    runningFoot: '台中磐石足球俱樂部 TCRFC',
    font: '"PingFang TC","Noto Sans TC","Hiragino Sans","Helvetica Neue",Arial,sans-serif',
  },
  en: {
    md: 'TCRFC_Website_Functional_Specification_EN.md',
    pdf: 'TCRFC_Website_Functional_Specification_EN.pdf',
    lang: 'en',
    club: 'Taichung Rock FC',
    title: ['Official Website', 'Functional Specification'],
    promise: 'LOCAL ROOTS. GLOBAL PATHWAYS.',
    labels: { version: 'Document version', date: 'Date' },
    runningHead: (v) => `TCRFC Website Functional Specification ${v}`,
    runningFoot: 'Taichung Rock FC — TCRFC',
    // 英文版以拉丁字型打頭，中文詞彙再 fallback 到 PingFang
    font: '"Helvetica Neue",Helvetica,Arial,"PingFang TC","Noto Sans TC",sans-serif',
  },
  'charity-zh': {
    md: 'TCRFC_慈善捐款平台功能規劃書.md',
    pdf: 'TCRFC_慈善捐款平台功能規劃書.pdf',
    lang: 'zh-Hant',
    club: '台中磐石足球俱樂部',
    title: ['慈善捐款平台', '功能規劃書'],
    promise: 'LOCAL ROOTS. GLOBAL PATHWAYS.　在地扎根 · 放眼世界',
    labels: { version: '文件版本', date: '建立日期' },
    runningHead: (v) => `TCRFC 慈善捐款平台功能規劃書 ${v}`,
    runningFoot: '台中磐石足球俱樂部 TCRFC',
    font: '"PingFang TC","Noto Sans TC","Hiragino Sans","Helvetica Neue",Arial,sans-serif',
  },
  'charity-en': {
    md: 'TCRFC_Charity_Donation_Platform_Specification_EN.md',
    pdf: 'TCRFC_Charity_Donation_Platform_Specification_EN.pdf',
    lang: 'en',
    club: 'Taichung Rock FC',
    title: ['Charity Donation Platform', 'Functional Specification'],
    promise: 'LOCAL ROOTS. GLOBAL PATHWAYS.',
    labels: { version: 'Document version', date: 'Date' },
    runningHead: (v) => `TCRFC Charity Donation Platform Specification ${v}`,
    runningFoot: 'Taichung Rock FC — TCRFC',
    font: '"Helvetica Neue",Helvetica,Arial,"PingFang TC","Noto Sans TC",sans-serif',
  },
};

// ── Markdown ────────────────────────────────────────────────────────────────

// GitHub 風格 anchor：轉小寫、去標點、空白轉連字號。CJK 字元原樣保留，
// 目錄裡的 `#1-專案目標與範圍` 才連得到 `## 1. 專案目標與範圍`。
const slug = (s) =>
  s
    .toLowerCase()
    .replace(/<[^>]*>/g, '')
    .replace(/[^\p{L}\p{N}\s-]/gu, '')
    .trim()
    .replace(/\s/g, '-');

function renderBody(md) {
  const renderer = new marked.Renderer();
  renderer.heading = function ({ tokens, depth }) {
    const text = this.parser.parseInline(tokens);
    return `<h${depth} id="${slug(this.parser.parseInline(tokens, this.parser.textRenderer))}">${text}</h${depth}>\n`;
  };
  return marked.parse(md, { renderer, gfm: true, breaks: false });
}

// H1 與其後的中繼資料 blockquote 由封面吸收；正文從第一個 `##` 開始。
function split(md) {
  const body = md.slice(md.search(/^## /m));
  const head = md.slice(0, md.search(/^## /m));
  const version = head.match(/\*\*(?:文件版本|Document version)\*\*[：:]\s*(v[\d.]+)/)?.[1] ?? '';
  const date = head.match(/\*\*(?:建立日期|Date)\*\*[：:]\s*([0-9-]+)/)?.[1] ?? '';
  // 中繼資料 blockquote 仍要出現在正文第一頁（目錄之前）
  const quotes = head.slice(head.indexOf('\n>')).replace(/^\s*---\s*$/gm, '').trim();
  return { quotes, body, version, date };
}

// ── HTML ────────────────────────────────────────────────────────────────────

const CSS = (font) => `
:root{
  --font:${font};
  --brand:#E0218A; --brand-aa:#D61E83; --ink:#231916;
  --text:#333333; --muted:#666666; --rule:#E0E0E0; --paper-2:#F5F5F5;
  --link:#1A4FD6;
}
@page{ size:A4; margin:16mm 15mm 15mm; }
*{ box-sizing:border-box; }
html{ -webkit-print-color-adjust:exact; print-color-adjust:exact; }
body{
  margin:0; color:var(--text); background:#fff;
  font-family:var(--font);
  font-size:10.2pt; line-height:1.75; letter-spacing:.005em;
}

/* 封面 */
.cover{
  height:245mm; display:flex; flex-direction:column;
  align-items:center; justify-content:center; text-align:center;
  page-break-after:always;
}
.cover .mark{ font-size:34pt; font-weight:800; letter-spacing:.06em; color:var(--brand); }
.cover .club{ margin-top:5mm; font-size:14pt; letter-spacing:.42em; color:var(--ink); text-indent:.42em; }
.cover .rule{ width:42mm; height:2.2px; background:var(--brand); margin:9mm 0 16mm; }
.cover h1{ margin:0; font-size:21pt; font-weight:800; line-height:1.55; color:var(--ink); letter-spacing:.01em; }
.cover .promise{ margin-top:7mm; font-size:10.5pt; letter-spacing:.06em; color:var(--muted); }
.cover .meta{ margin-top:24mm; font-size:9.5pt; color:var(--muted); line-height:2; }
.cover .meta b{ font-weight:600; color:var(--text); margin-right:3mm; }

/* 標題 */
h2{
  font-size:15.5pt; font-weight:800; color:var(--ink); letter-spacing:.01em;
  margin:0 0 7mm; padding:1mm 0 1mm 5mm; border-left:4px solid var(--brand);
  page-break-before:always; page-break-after:avoid;
}
h2:first-of-type{ page-break-before:avoid; }
h3{
  font-size:12.4pt; font-weight:800; color:var(--ink); margin:9mm 0 3.5mm;
  padding-bottom:2mm; border-bottom:1px solid var(--rule); page-break-after:avoid;
}
h4{ font-size:11pt; font-weight:800; color:var(--ink); margin:6.5mm 0 2.5mm; page-break-after:avoid; }
h5,h6{ font-size:10.4pt; font-weight:700; color:var(--ink); margin:5mm 0 2mm; page-break-after:avoid; }

p{ margin:0 0 3.2mm; }
strong{ font-weight:700; color:var(--ink); }
a{ color:var(--link); text-decoration:none; }
hr{ border:0; border-top:1px solid var(--rule); margin:7mm 0; }

ul,ol{ margin:0 0 3.5mm; padding-left:6.5mm; }
li{ margin-bottom:1.4mm; }
li>ul,li>ol{ margin-top:1.4mm; }

/* 表格 */
table{
  width:100%; border-collapse:collapse; margin:3mm 0 5mm;
  font-size:9.1pt; line-height:1.6; page-break-inside:auto;
}
thead{ display:table-header-group; }
tr{ page-break-inside:avoid; }
th,td{ border:1px solid var(--rule); padding:2mm 2.6mm; text-align:left; vertical-align:top; }
th{ background:var(--paper-2); font-weight:700; color:var(--ink); }

/* 引言區塊 */
blockquote{
  margin:3.5mm 0 5mm; padding:3mm 4mm; background:var(--paper-2);
  border-left:3px solid var(--brand); page-break-inside:avoid;
}
blockquote p{ margin:0 0 1.8mm; font-size:9.5pt; }
blockquote p:last-child{ margin-bottom:0; }
blockquote ol,blockquote ul{ margin-bottom:0; font-size:9.5pt; }

/* 程式碼 */
code{
  font-family:"SF Mono",Menlo,Consolas,monospace; font-size:8.6pt;
  background:var(--paper-2); padding:.3mm 1mm; border-radius:1mm; color:var(--ink);
}
pre{
  background:var(--paper-2); border:1px solid var(--rule); padding:3.5mm 4mm;
  margin:3mm 0 5mm; overflow:hidden; page-break-inside:avoid;
}
pre code{ background:none; padding:0; font-size:8.4pt; line-height:1.62; white-space:pre-wrap; }
`;

const HEADER_TPL = (text) => `
<div style="width:100%;padding:0 15mm;font-family:-apple-system,'PingFang TC','Helvetica Neue',Arial,sans-serif;
            font-size:7pt;color:#9A9A9A;letter-spacing:.02em;">${text}</div>`;

const FOOTER_TPL = (text) => `
<div style="width:100%;padding:0 15mm;font-family:-apple-system,'PingFang TC','Helvetica Neue',Arial,sans-serif;
            font-size:7pt;color:#9A9A9A;letter-spacing:.02em;display:flex;justify-content:space-between;">
  <span>${text}</span><span><span class="pageNumber"></span> / <span class="totalPages"></span></span>
</div>`;

function page(doc, { quotes, body, version, date }) {
  return `<!DOCTYPE html>
<html lang="${doc.lang}"><head><meta charset="utf-8">
<title>${doc.runningHead(version)}</title>
<style>${CSS(doc.font)}</style></head>
<body>
<section class="cover">
  <div class="mark">TCRFC</div>
  <div class="club">${doc.club}</div>
  <div class="rule"></div>
  <h1>${doc.title.join('<br>')}</h1>
  <div class="promise">${doc.promise}</div>
  <div class="meta">
    <div><b>${doc.labels.version}</b>${version}</div>
    <div><b>${doc.labels.date}</b>${date}</div>
  </div>
</section>
${renderBody(quotes)}
<hr>
${renderBody(body)}
</body></html>`;
}

// 既有的 HTML 交付物，只需照 @page 印出來，不套規劃書樣式也不加頁首頁尾。
// opts 直接餵給 Page.printToPDF：里程碑是 A4 橫式單頁，站台地圖是 A4 直式多頁。
const HTML_DOCS = {
  'mile-zh': { html: 'TCRFC_開發里程碑_Milestone.html', pdf: 'TCRFC_開發里程碑_Milestone.pdf',
    opts: { preferCSSPageSize: true, landscape: true } },
  'mile-en': { html: 'TCRFC_Development_Milestones_EN.html', pdf: 'TCRFC_Development_Milestones_EN.pdf',
    opts: { preferCSSPageSize: true, landscape: true } },
  'sitemap-zh': { html: 'TCRFC_慈善捐款站台地圖.html', pdf: 'TCRFC_慈善捐款站台地圖.pdf',
    opts: { preferCSSPageSize: true } },
  'sitemap-en': { html: 'TCRFC_Charity_Donation_Sitemap_EN.html', pdf: 'TCRFC_Charity_Donation_Sitemap_EN.pdf',
    opts: { preferCSSPageSize: true } },
};

// ── Chrome / CDP ────────────────────────────────────────────────────────────

// CLI 的 --print-to-pdf 無法帶自訂頁首頁尾，所以直接走 DevTools Protocol。
// Node 24 內建 WebSocket，不需要 puppeteer。
function startChrome() {
  return new Promise((ok, fail) => {
    const proc = spawn(CHROME, [
      '--headless', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
      '--remote-debugging-port=0', '--user-data-dir=' + join(tmpdir(), 'tcrfc-pdf-profile'),
      'about:blank',
    ]);
    let buf = '';
    const to = setTimeout(() => fail(new Error('Chrome 啟動逾時')), 30000);
    proc.stderr.on('data', (d) => {
      buf += d;
      const m = buf.match(/DevTools listening on (ws:\/\/\S+)/);
      if (m) { clearTimeout(to); ok({ proc, ws: m[1] }); }
    });
    proc.on('error', fail);
  });
}

function cdp(url) {
  const sock = new WebSocket(url);
  let id = 0;
  const pending = new Map();
  const events = new Map();
  sock.addEventListener('message', (e) => {
    const msg = JSON.parse(e.data);
    if (msg.id && pending.has(msg.id)) {
      const { ok, fail } = pending.get(msg.id); pending.delete(msg.id);
      msg.error ? fail(new Error(msg.error.message)) : ok(msg.result);
    } else if (msg.method && events.has(msg.method)) {
      events.get(msg.method)(); events.delete(msg.method);
    }
  });
  const ready = new Promise((ok) => sock.addEventListener('open', ok));
  return {
    ready,
    send: (method, params = {}, sessionId) =>
      new Promise((ok, fail) => {
        const n = ++id;
        pending.set(n, { ok, fail });
        sock.send(JSON.stringify({ id: n, method, params, sessionId }));
      }),
    once: (method) => new Promise((ok) => events.set(method, ok)),
    close: () => sock.close(),
  };
}

async function toPdf(client, fileUrl, opts) {
  const { targetId } = await client.send('Target.createTarget', { url: 'about:blank' });
  const { sessionId } = await client.send('Target.attachToTarget', { targetId, flatten: true });
  await client.send('Page.enable', {}, sessionId);
  const loaded = client.once('Page.loadEventFired');
  await client.send('Page.navigate', { url: fileUrl }, sessionId);
  await loaded;
  const { data } = await client.send('Page.printToPDF',
    { printBackground: true, ...opts }, sessionId);
  await client.send('Target.closeTarget', { targetId });
  return Buffer.from(data, 'base64');
}

// ── main ────────────────────────────────────────────────────────────────────

const all = { ...DOCS, ...HTML_DOCS };
const jobs = process.argv.slice(2).length ? process.argv.slice(2) : Object.keys(all);
for (const k of jobs)
  if (!all[k]) { console.error(`未知的項目：${k}（可用 ${Object.keys(all).join(' / ')}）`); process.exit(1); }

const { proc, ws } = await startChrome();
const client = cdp(ws);
await client.ready;

try {
  for (const k of jobs) {
    const doc = all[k];
    let pdf, note;
    if (HTML_DOCS[k]) {
      pdf = await toPdf(client, pathToFileURL(join(OUT, doc.html)).href, doc.opts);
    } else {
      const parsed = split(await readFile(join(OUT, doc.md), 'utf8'));
      const tmp = join(tmpdir(), `tcrfc-build-${k}.html`);
      await writeFile(tmp, page(doc, parsed), 'utf8');
      pdf = await toPdf(client, pathToFileURL(tmp).href, {
        displayHeaderFooter: true,
        headerTemplate: HEADER_TPL(doc.runningHead(parsed.version)),
        footerTemplate: FOOTER_TPL(doc.runningFoot),
        paperWidth: 8.27, paperHeight: 11.69,
        marginTop: 0.63, marginBottom: 0.59, marginLeft: 0.59, marginRight: 0.59,
      });
      await unlink(tmp);
      note = parsed.version;
    }
    await writeFile(join(OUT, doc.pdf), pdf);
    console.log(`✓ ${doc.pdf}　${note ?? ''}　${(pdf.length / 1024 / 1024).toFixed(1)} MB`);
  }
} finally {
  client.close();
  proc.kill();
}
