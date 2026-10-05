#!/usr/bin/env node
// shared/scripts/gen-image-derivatives.mjs — 把「圖片衍生檔網址命名規則」從 apps/api 的實作寫成契約，供 iOS／Android 共用，
// 兩端不必各自複製規則（Android 缺口 A5）。來源（不發明規格）：
//   - apps/api/Images/ImageUploadOptions.cs：DerivativeLongEdges、ThumbnailSize、MainLongEdge、WebPQuality
//   - apps/api/Images/ImageObjectKey.cs：主檔副檔名 .webp、衍生檔鍵 `{stem}-{suffix}.webp`
// 規劃書 §4.0 圖片上傳通則：長邊 ≤2560 的 WebP 主檔、1280／640／320 三個等比衍生檔、後台 160px 方形縮圖，不留原始檔。
// DTO 裡所有 `*Url`（photoUrl、coverUrl、logoUrl…）給的都是**主檔**網址；要小圖時依本契約從主檔網址推導衍生檔網址，
// 不需要任何額外請求。⚠️ 衍生檔只存在於「經後台上傳的圖片」；站台照片等由腳本批次上傳的圖也遵守同一規則（docs/17）。
// 用法：node gen-image-derivatives.mjs

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '../..');
const read = (rel) => readFileSync(join(repoRoot, rel), 'utf8');

const options = read('apps/api/Images/ImageUploadOptions.cs');
const key = read('apps/api/Images/ImageObjectKey.cs');

const longEdges = (options.match(/DerivativeLongEdges\s*=\s*\[([^\]]+)\]/)?.[1] ?? '')
  .split(',').map((s) => Number(s.trim())).filter(Boolean);
const num = (name) => Number(options.match(new RegExp(`${name}\\s*=\\s*(\\d+)`))?.[1]);
const thumb = num('ThumbnailSize');
const mainLongEdge = num('MainLongEdge');
const webpQuality = num('WebPQuality');
const extension = key.match(/Extension\s*=\s*"(\.[a-z0-9]+)"/)?.[1];
const thumbSuffix = key.match(/ForThumbnail\([^)]*\)\s*=>\s*ForSuffix\([^,]+,\s*"([a-z]+)"\)/)?.[1];

if (longEdges.length === 0 || !thumb || !mainLongEdge || !extension || !thumbSuffix) {
  throw new Error('解析 ImageUploadOptions.cs／ImageObjectKey.cs 失敗（格式被改動？）');
}

const derive = (url, suffix) => {
  const q = url.search(/[?#]/);
  const base = q < 0 ? url : url.slice(0, q);
  const rest = q < 0 ? '' : url.slice(q);
  return base.endsWith(extension) ? `${base.slice(0, -extension.length)}-${suffix}${extension}${rest}` : null;
};
const sample = 'https://img.example.com/images/tcrfc/players/0b7c/photo.webp';

const doc = {
  $comment:
    '自動產生，請勿手改。產生器：shared/scripts/gen-image-derivatives.mjs（docs/19 §2，缺口 A5）。來源是 apps/api/Images，不是規格；規格是主站規劃書 §4.0 圖片上傳通則。',
  mainExtension: extension,
  mainMaxLongEdgePx: mainLongEdge,
  webpQuality,
  derivatives: [
    ...longEdges.map((e) => ({ suffix: String(e), kind: 'longEdge', longEdgePx: e, note: '等比縮小，長邊為此值；原圖較小時不放大（長邊以原圖為上限）' })),
    { suffix: thumbSuffix, kind: 'squareThumbnail', sizePx: thumb, note: `${thumb}×${thumb} 置中裁切方形縮圖（後台列表縮圖；App 一般不需要）` },
  ],
  rule:
    `由主檔網址（DTO 的 *Url，副檔名 ${extension}）推導：把副檔名前面插入「-{suffix}」，查詢字串與錨點原樣保留。` +
    ` 例：${sample} → ${derive(sample, '640')}。`,
  pick: {
    note: '建議挑選：取「長邊 ≥ 顯示尺寸（px，含螢幕倍率）」的最小衍生檔；都不夠大才用主檔。',
    suffixesAscending: [...longEdges].sort((a, b) => a - b).map(String),
  },
  examples: [
    { main: sample, suffix: '320', expected: derive(sample, '320') },
    { main: sample, suffix: '640', expected: derive(sample, '640') },
    { main: sample, suffix: '1280', expected: derive(sample, '1280') },
    { main: sample, suffix: thumbSuffix, expected: derive(sample, thumbSuffix) },
    { main: `${sample}?v=3`, suffix: '320', expected: derive(`${sample}?v=3`, '320') },
  ],
  coverage: {
    note: '手動維護清單（新增圖片上傳欄位時補）：下列 DTO 欄位的圖片都走 §4.0 上傳管線，**保證有** 1280／640／320／thumb 衍生檔。',
    guaranteed: ['PlayerDto.photoUrl', 'StaffDto.photoUrl', 'ClubDto.logoUrl／logoDarkUrl', 'ArticleListItemDto.coverUrl', 'AppAdItemDto.imageUrl（另有 imageVariants 直接帶網址）', 'MemberDrawDto.coverUrl', '夥伴與贊助商的 logoDarkUrl／logoLightUrl', '特約店家 imageUrl', '課程 coverUrl', '商品圖片'],
    notGuaranteed: ['站台照片（infra/upload-site-images.sh 批次上傳，衍生檔有無與命名以該腳本為準，不保證）', '影片（videoUrl）不適用', '站外網址'],
  },
  notDerivable: '主檔網址不是以 .webp 結尾（例如站外圖片、空值）時沒有衍生檔，請直接使用原網址或沒有圖片的預設畫面。',
};

writeFileSync(join(repoRoot, 'shared/image-derivatives.json'), `${JSON.stringify(doc, null, 2)}\n`);
console.log(`image-derivatives: 長邊 ${longEdges.join('／')}＋${thumbSuffix} ${thumb}px → shared/image-derivatives.json`);
