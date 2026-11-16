#!/usr/bin/env node
/** G1 選項中文顯示文字的儲存判斷（`src/utils/formOptionLabels.ts`）單元測試，做法同 test-match-result.mjs。 */
import assert from 'node:assert/strict'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const server = await createServer({ root: ROOT, configFile: false, optimizeDeps: { noDiscovery: true, include: [] }, logLevel: 'error', appType: 'custom', server: { middlewareMode: true }, resolve: { alias: { '@': `${ROOT}src` } } })

let failed = 0
function test(name, fn) {
  try {
    fn()
    console.log(`  ok  ${name}`)
  } catch (error) {
    failed++
    console.error(`  FAIL  ${name}\n${error.stack}`)
  }
}

try {
  const M = await server.ssrLoadModule('/src/utils/formOptionLabels.ts')
  const options = ['小學', '國中', '高中']

  test('原本 null、沒動過 → 不送（undefined），不把送出值複製進去', () =>
    assert.equal(M.buildOptionLabelsZhPayload(options, null, M.initialOptionLabelsZh(options, null)), undefined))
  test('原本 null、改了一項 → 送整組', () =>
    assert.deepEqual(M.buildOptionLabelsZhPayload(options, null, ['小學', '國中生', '高中']), ['小學', '國中生', '高中']))
  test('原本有設定、全部改回與送出值相同 → 送 [] 清除', () =>
    assert.deepEqual(M.buildOptionLabelsZhPayload(options, ['A', 'B', 'C'], options), []))
  test('原本有設定、沒動過 → 不送', () =>
    assert.equal(M.buildOptionLabelsZhPayload(options, ['A', 'B', 'C'], ['A', 'B', 'C']), undefined))
  test('原本有設定、改了一項 → 送整組', () =>
    assert.deepEqual(M.buildOptionLabelsZhPayload(options, ['A', 'B', 'C'], ['A', 'B', 'D']), ['A', 'B', 'D']))
  test('空白視為用送出值：改一項、清空一項 → 送整組且不含空字串', () =>
    assert.deepEqual(M.buildOptionLabelsZhPayload(options, null, ['小學生', '  ', '高中']), ['小學生', '國中', '高中']))
  test('空白＋原本 null → 不送', () =>
    assert.equal(M.buildOptionLabelsZhPayload(options, null, ['', '', '']), undefined))
  test('選項被刪減（筆數變了）且原本有設定 → 送新的整組', () =>
    assert.deepEqual(M.buildOptionLabelsZhPayload(['小學', '國中'], ['A', 'B', 'C'], ['A', 'B']), ['A', 'B']))
  test('沒有選項 → 不送', () => assert.equal(M.buildOptionLabelsZhPayload([], null, []), undefined))
  test('前後空白會被修剪', () =>
    assert.deepEqual(M.buildOptionLabelsZhPayload(options, null, [' 小學生 ', '國中', '高中']), ['小學生', '國中', '高中']))
  test('初始值：null → 送出值；有設定 → 設定值；筆數對不上 → 送出值', () => {
    assert.deepEqual(M.initialOptionLabelsZh(options, null), options)
    assert.deepEqual(M.initialOptionLabelsZh(options, ['A', 'B', 'C']), ['A', 'B', 'C'])
    assert.deepEqual(M.initialOptionLabelsZh(options, ['A']), options)
  })
} finally {
  await server.close()
}
if (failed > 0) {
  console.error(`\n${failed} 項失敗`)
  process.exit(1)
}
else console.log('全部通過')
