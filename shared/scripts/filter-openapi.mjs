#!/usr/bin/env node
// shared/scripts/filter-openapi.mjs
//
// 把 .NET 建置期產出的完整 OpenAPI 文件收斂成「App 可以呼叫的那一份」並正規化：
//   1. 排除後台管理端點、慈善獨立平台端點、伺服器內部端點（見下方 EXCLUDED_PREFIXES）；
//   2. 只留下被保留端點用到的 components.schemas（沿 $ref 取閉包）；
//   3. 路徑與 schema 依字母排序、固定 2 空白縮排、結尾換行——讓 git diff 只反映真的 API 變動。
//
// 用法：node filter-openapi.mjs <輸入 json> <輸出 json>
// 規則本身是執行層決定（docs/19 §2），不是規格；App 端點清單的真實來源仍是 App 規劃書 §9.2。

import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

/** 不給 App 的路徑前綴（任一前綴開頭就整條排除）。 */
export const EXCLUDED_PREFIXES = [
  '/api/v1/admin', // 後台管理（官網後台專用）
  '/api/v1/donation-platform', // 慈善捐款平台（獨立後台與獨立資料庫，App 不呼叫）
  '/api/membership/activate', // 伺服器內部、憑證保護，不對外（App 規劃書 §9.3）
];

const HTTP_METHODS = ['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace'];

function collectRefs(node, out) {
  if (Array.isArray(node)) {
    for (const x of node) collectRefs(x, out);
  } else if (node && typeof node === 'object') {
    for (const [k, v] of Object.entries(node)) {
      if (k === '$ref' && typeof v === 'string' && v.startsWith('#/components/schemas/')) {
        out.add(v.slice('#/components/schemas/'.length));
      } else {
        collectRefs(v, out);
      }
    }
  }
}

function sortKeys(obj) {
  return Object.fromEntries(Object.keys(obj).sort().map((k) => [k, obj[k]]));
}

export function filterOpenApi(doc) {
  const paths = {};
  for (const p of Object.keys(doc.paths ?? {}).sort()) {
    if (EXCLUDED_PREFIXES.some((x) => p === x || p.startsWith(`${x}/`))) continue;
    paths[p] = doc.paths[p];
  }

  const allSchemas = doc.components?.schemas ?? {};
  const needed = new Set();
  collectRefs(paths, needed);
  // 閉包：被保留 schema 再引用的 schema 也要留
  const queue = [...needed];
  while (queue.length > 0) {
    const name = queue.pop();
    const schema = allSchemas[name];
    if (!schema) throw new Error(`OpenAPI 引用了不存在的 schema：${name}`);
    const inner = new Set();
    collectRefs(schema, inner);
    for (const n of inner) {
      if (!needed.has(n)) {
        needed.add(n);
        queue.push(n);
      }
    }
  }
  const schemas = {};
  for (const n of [...needed].sort()) schemas[n] = allSchemas[n];

  const components = { ...(doc.components ?? {}), schemas };
  const out = { ...doc, paths, components: sortKeys(components) };
  // 沒被引用的 tag 清單也一併清掉，避免文件殘留後台標籤
  if (Array.isArray(out.tags)) {
    const used = new Set();
    for (const item of Object.values(paths)) {
      for (const m of HTTP_METHODS) for (const t of item[m]?.tags ?? []) used.add(t);
    }
    out.tags = out.tags.filter((t) => used.has(t.name));
    if (out.tags.length === 0) delete out.tags;
  }
  return out;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const [input, output] = process.argv.slice(2);
  if (!input || !output) {
    console.error('用法：node filter-openapi.mjs <輸入 json> <輸出 json>');
    process.exit(2);
  }
  const doc = JSON.parse(readFileSync(input, 'utf8'));
  const filtered = filterOpenApi(doc);
  writeFileSync(output, `${JSON.stringify(filtered, null, 2)}\n`);
  console.log(
    `openapi: ${Object.keys(filtered.paths).length} 條路徑、${Object.keys(filtered.components.schemas).length} 個 schema → ${output}`,
  );
}
