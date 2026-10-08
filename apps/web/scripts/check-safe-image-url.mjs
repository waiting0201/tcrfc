#!/usr/bin/env node
/**
 * check-safe-image-url.mjs — app/utils/safe-image-url.ts 的純函式驗證（E-301，2026-10-08）。
 * 釘住：正式環境只收 https 與站內路徑；開發模式才多收 http；`//host`、javascript:、data: 一律拒絕。
 * 並檢查 app/ 內不得再有頁面自己複製 `/^(https:\/\/|\/)/` 這類正規式。
 * 用法：node scripts/check-safe-image-url.mjs　　離開碼：任一失敗 → 1。
 */
import assert from 'node:assert/strict'
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { checkImageUrl } from '../app/utils/safe-image-url.ts'

let failures = 0
const t = (name, fn) => {
  try { fn(); console.log('✓', name) }
  catch (e) { failures++; console.error(`✗ ${name}\n    ${e.message}`) }
}

t('正式環境（allowHttp=false）：https 與站內路徑放行', () => {
  assert.equal(checkImageUrl('https://a.com/x.png'), 'https://a.com/x.png')
  assert.equal(checkImageUrl('/assets/img/a.jpg'), '/assets/img/a.jpg')
})
t('正式環境：http 拒絕（行為不變）', () => {
  assert.equal(checkImageUrl('http://127.0.0.1:10000/images/a.png'), null)
})
t('開發環境（allowHttp=true）：http 放行', () => {
  assert.equal(checkImageUrl('http://127.0.0.1:10000/images/a.png', true), 'http://127.0.0.1:10000/images/a.png')
})
t('兩種環境都拒絕：協定相對、javascript:、data:、vbscript:、空值、非字串', () => {
  for (const allow of [false, true]) {
    for (const u of ['//evil.com/x.png', '/\\evil.com', 'javascript:alert(1)', ' JaVaScRiPt:alert(1)', 'data:image/png;base64,AAA', 'vbscript:x', 'ftp://a.com/x', 'a.png', '', '   ', null, undefined, 42]) {
      assert.equal(checkImageUrl(u, allow), null, `${String(u)} allow=${allow}`)
    }
  }
})

// 頁面不得再複製自己的檢查
const walk = (d) => readdirSync(d).flatMap(f => { const p = join(d, f); return statSync(p).isDirectory() ? walk(p) : [p] })
const dup = /\^\(https:\\\/\\\/\|\\\/\)/
t('app/ 內沒有複製的 /^(https:\\/\\/|\\/)/ 檢查', () => {
  const bad = walk(new URL('../app', import.meta.url).pathname)
    .filter(f => /\.(vue|ts)$/.test(f) && dup.test(readFileSync(f, 'utf8')))
  assert.deepEqual(bad, [])
})

if (failures) { console.error(`\n${failures} 項失敗`); process.exit(1) }
console.log('\ncheck-safe-image-url：全部通過')
