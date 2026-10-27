// 產出行動 App 上架用圖示到 output/app-icons/。
// 依 App 規劃書 §14.1「中性識別，雙標誌並列」：白底，左磐石隊徽（brand/svg/tcrfc-mark-pink.svg）、
// 右藍鯨隊徽（brand/blue-whale/bw-crest.png 點陣主檔，§16.2 第 1 項：足以產出 1024 App icon）。
// 兩個標誌都不改色、不改造型、不加字；藍鯨隊徽本身是全彩白底盾徽，所以底色用白，不用任一隊的品牌色。
//   node output/tools/build-app-icons.mjs
// 以 Chrome headless 截圖合成，再用 Pillow 去掉 iOS 圖示與 Play 主題圖片不允許的透明通道。

import { mkdir, writeFile, unlink } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { tmpdir } from 'node:os';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..', '..');
const OUT = join(ROOT, 'output', 'app-icons');
const CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const WHITE = '#FFFFFF';
const TCRFC = pathToFileURL(join(ROOT, 'brand', 'svg', 'tcrfc-mark-pink.svg')).href;
const BW = pathToFileURL(join(ROOT, 'brand', 'blue-whale', 'bw-crest.png')).href;

// logo：每個標誌的高度佔畫布高度的比例；gap：兩標誌間距佔畫布寬度的比例。
// 滿版圖示（iOS、Play）兩標誌合寬約佔 84%；iOS 會切圓角、Play 會套圓角遮罩，角落不放東西即可。
// Android 自適應前景是 108dp 畫布、可能被裁成直徑 66dp 的圓，兩標誌須整組收進圓內，故縮到 26%。
const JOBS = [
  { file: 'ios/AppIcon-1024.png', w: 1024, h: 1024, logo: 0.40, gap: 0.04, bg: WHITE, opaque: true },
  { file: 'play/play-icon-512.png', w: 512, h: 512, logo: 0.40, gap: 0.04, bg: WHITE, opaque: true },
  { file: 'play/feature-graphic-1024x500.png', w: 1024, h: 500, logo: 0.60, gap: 0.08, bg: WHITE, opaque: true },
  ...[['mdpi', 108], ['hdpi', 162], ['xhdpi', 216], ['xxhdpi', 324], ['xxxhdpi', 432]].map(([d, px]) => ({
    file: `android/mipmap-${d}/ic_launcher_foreground.png`, w: px, h: px, logo: 0.26, gap: 0.03, bg: 'transparent',
  })),
];

const html = (j) => `<!DOCTYPE html><html><head><style>
html,body{margin:0;width:${j.w}px;height:${j.h}px;background:${j.bg};overflow:hidden}
body{display:flex;align-items:center;justify-content:center;gap:${Math.round(j.w * j.gap)}px}
img{height:${Math.round(j.h * j.logo)}px;width:auto;display:block}
</style></head><body><img src="${TCRFC}"><img src="${BW}"></body></html>`;

for (const j of JOBS) {
  const out = join(OUT, j.file);
  await mkdir(dirname(out), { recursive: true });
  const tmp = join(tmpdir(), `tcrfc-icon-${process.pid}.html`);
  await writeFile(tmp, html(j), 'utf8');
  execFileSync(CHROME, [
    '--headless=new', '--disable-gpu', '--hide-scrollbars', '--force-device-scale-factor=1',
    '--default-background-color=00000000', `--window-size=${j.w},${j.h}`, `--screenshot=${out}`,
    pathToFileURL(tmp).href,
  ], { stdio: 'ignore' });
  await unlink(tmp);
  const mode = j.opaque ? 'RGB' : 'RGBA';
  execFileSync('python3', ['-I', '-c',
    `import sys;from PIL import Image;im=Image.open(sys.argv[1]);assert im.size==(${j.w},${j.h}),im.size;im.convert('${mode}').save(sys.argv[1])`,
    out]);
  console.log(`✓ ${j.file}　${j.w}×${j.h} ${mode}`);
}
