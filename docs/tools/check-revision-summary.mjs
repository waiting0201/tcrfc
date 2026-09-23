#!/usr/bin/env node
// docs/tools/check-revision-summary.mjs — 修訂摘要不得寫出被取代的舊值（2026-09-23，E-41 的防呆）
//
// 依據：docs/00-harness.md §2.5「範圍縮減的寫法」，客戶 2026-09-12 指示——
// **規劃書與客戶版只寫「現在要做什麼」，不留「曾經決定不做什麼」的記錄**，
// 且「這條比『整段刪除』更嚴：連『原本是 X 改為 Y』這類註記本身都不留」。
//
// 🔴 這條規定在 2026-09-23 之前**完全沒有任何自動檢查**，於是 v3.13 的修訂摘要
// 就寫成了「『延期』改為『延賽』」——舊用語因此出現在**客戶看得到的交付物**裡，
// 正是這條指示要避免的。記為 docs/18-work-errors.md 的 E-41。
//
// ⛔ 本檢查**只掃修訂摘要區塊**（`> **vX.Y 修訂摘要` / `revision summary` 起，到第一行非 `> ` 為止），
// 不掃規格本體——本體裡的「移除 EXIF」是功能描述、「發票作廢」是資料狀態值，
// §2.5 明文允許（`✅ 保留：…『已作廢』／『已取消』這類資料狀態值`）。
//
// ⛔ 比對的形狀**刻意收得很窄**：只認「把被取代的舊值連同新值一起寫出來」這一種，
// 也就是規定第 165 行點名的那一種。刻意**不做**關鍵字掃描（「移除」「作廢」「退役」）——
// 2026-09-23 實測那樣做會在八份母檔產生 7 筆誤判、0 筆真陽性，
// 而**一個誤判率高的檢查會被加上白名單，然後變成擺設**（docs/18 的 E-34）。
//
// 允許的寫法（規定 ✅ 的那一類，本檢查不會攔）：
//   ✅ 「九個子模組更名以呼應前台：首頁編排、常見問題、…」——只列新名稱，不寫舊名稱
//   ✅ 「§7 的 GEO 是條列需求 GEO-01–GEO-09」——描述現況
//   ❌ 「『延期』改為『延賽』」「原 E5–E7 改為 E4–E6」「§7 的 GEO 由『補充建議』改為…」
//
// 用法：node docs/tools/check-revision-summary.mjs        （非 0 就是有違規）

import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const OUTPUT_DIR = 'output';
const BLOCK_START = /^> \*\*v[\d.]+ (修訂摘要|revision summary)/;

// 「把舊值寫出來」的四種形狀。每一種都要求**舊值被引號或反引號包住**——
// 沒有被引起來的「改為」多半是在描述設計（「長邊改為 2560px」），不是在記錄被取代的舊值。
const BANNED = [
  { re: /「[^」]{1,24}」\s*(?:改為|改成|改名為|更名為)/, why: '把舊用語連同新用語一起寫出來' },
  { re: /原\s*[`「][^`」]{1,24}[`」]\s*(?:改為|改成)/, why: '「原 X 改為 Y」' },
  { re: /由\s*[`「][^`」]{1,24}[`」]\s*(?:改為|改成)/, why: '「由 X 改為 Y」' },
  { re: /renamed\s+["“][^"”]{1,36}["”]\s+to\s+/i, why: 'renamed "X" to "Y"' },
];

const files = readdirSync(OUTPUT_DIR).filter((f) => f.startsWith('TCRFC_') && f.endsWith('.md'));
const problems = [];

for (const file of files) {
  const lines = readFileSync(join(OUTPUT_DIR, file), 'utf8').split('\n');
  let inBlock = false;
  lines.forEach((line, i) => {
    if (BLOCK_START.test(line)) inBlock = true;
    else if (!line.startsWith('> ')) inBlock = false;
    if (!inBlock) return;
    for (const { re, why } of BANNED) {
      if (re.test(line)) {
        problems.push({ file, line: i + 1, why, text: line.trim() });
        break;
      }
    }
  });
}

if (problems.length === 0) {
  console.log(`✅ 修訂摘要乾淨（掃了 ${files.length} 份母檔，未發現寫出被取代舊值的段落）`);
  process.exit(0);
}

console.error(`✗ 修訂摘要有 ${problems.length} 處寫出了被取代的舊值：\n`);
for (const p of problems) {
  console.error(`  ${p.file}:${p.line}  ——  ${p.why}`);
  console.error(`    ${p.text}\n`);
}
console.error('依 docs/00-harness.md §2.5（客戶 2026-09-12 指示），規劃書與客戶版只寫「現在是什麼」，');
console.error('不留「原本是 X 改為 Y」這類註記——舊值不應該出現在客戶看得到的交付物裡。');
console.error('改法：把句子改寫成描述現況，例如「『延期』改為『延賽』」→「賽事狀態的中文用語定為「延賽」」。');
process.exit(1);
