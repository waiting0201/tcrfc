#!/usr/bin/env node
// TCRFC 靜態站建置：把 src/pages 的頁面內容包進共用外殼，輸出純靜態 HTML 到 dist/。
// 零相依。輸出等同手寫 HTML，可直接部署 Cloudflare Pages。
import { readFile, writeFile, mkdir, readdir, cp, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join, dirname, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = dirname(fileURLToPath(import.meta.url));
const SRC = join(ROOT, 'src');
const DIST = join(ROOT, 'dist');

const read = (p) => readFile(p, 'utf8');

async function walk(dir, out = []) {
  for (const e of await readdir(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) await walk(p, out);
    else if (e.name.endsWith('.html')) out.push(p);
  }
  return out;
}

// 從頁面檔頭抽出 <!--meta { ... } --> 區塊
function parseMeta(src) {
  const m = src.match(/^\s*<!--meta\s*([\s\S]*?)-->/);
  if (!m) throw new Error('頁面缺少 <!--meta ... --> 區塊');
  return { meta: JSON.parse(m[1]), body: src.slice(m[0].length).trim() };
}

// 依輸出深度計算回到站根的相對路徑，讓 dist/ 可在任何子路徑下開啟
function rootPrefix(outRel) {
  const depth = outRel.split(sep).length - 1;
  return depth === 0 ? '.' : Array(depth).fill('..').join('/');
}

function fill(tpl, vars) {
  return tpl.replace(/\{\{(\w+)\}\}/g, (_, k) => (k in vars ? vars[k] : ''));
}

const build = async () => {
  const shell = await read(join(SRC, 'partials/shell.html'));
  const header = await read(join(SRC, 'partials/header.html'));
  const footer = await read(join(SRC, 'partials/footer.html'));

  const pagesDir = join(SRC, 'pages');
  const files = await walk(pagesDir);
  let n = 0;

  for (const file of files) {
    const rel = relative(pagesDir, file);
    const { meta, body } = parseMeta(await read(file));
    const root = rootPrefix(rel);

    const vars = {
      ROOT: root,
      TITLE: meta.title,
      DESCRIPTION: meta.description ?? '',
      NAV: meta.nav ?? '',
      UNIT: meta.unit ?? '',
      LANG: meta.lang ?? 'zh-Hant',
      CANONICAL: meta.canonical ?? '',
      BODYCLASS: meta.bodyClass ?? '',
      SCHEMA: meta.schema ? `<script type="application/ld+json">${JSON.stringify(meta.schema)}</script>` : '',
      HEADER: fill(header, { ROOT: root, NAV: meta.nav ?? '' }),
      FOOTER: fill(footer, { ROOT: root }),
      CONTENT: fill(body, { ROOT: root }),
    };

    // 導覽列目前分頁標記
    vars.HEADER = vars.HEADER.replace(
      new RegExp(`(<a [^>]*data-nav="${meta.nav}")`, 'g'),
      '$1 aria-current="page"'
    );

    const out = join(DIST, rel);
    await mkdir(dirname(out), { recursive: true });
    await writeFile(out, fill(shell, vars), 'utf8');
    n++;
  }

  // 靜態資產原樣複製
  if (existsSync(join(SRC, 'assets'))) {
    await cp(join(SRC, 'assets'), join(DIST, 'assets'), { recursive: true });
  }
  for (const f of ['_headers', '_redirects', 'robots.txt']) {
    if (existsSync(join(SRC, f))) await cp(join(SRC, f), join(DIST, f));
  }

  console.log(`✓ 產出 ${n} 個頁面 → ${relative(process.cwd(), DIST)}`);
};

build().catch((e) => {
  console.error('建置失敗：', e.message);
  process.exit(1);
});
