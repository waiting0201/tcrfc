#!/usr/bin/env node
/**
 * check-page-blocks.mjs — 區塊 JSON 前台正規化（shared/utils/page-blocks.ts）的純函式驗證（A-5／A-7，2026-10-06）。
 * 驗證：雙語物件依語系化簡（en 空白退 zh）、parseBlocksJson 的向下相容、五種新區塊的白名單與消毒
 * （影音只認 YouTube／Vimeo 並輸出 nocookie、圖片網址只認 https／站內路徑／mediaBaseUrl+key、檔案連結消毒）。
 * 用法：node scripts/check-page-blocks.mjs　離開碼：任一斷言失敗 → 1。
 */
import assert from 'node:assert/strict'
import { normalizePageBlocks, parseBlocksJson, videoEmbedSrc } from '../shared/utils/page-blocks.ts'

let n = 0
let failures = 0
const t = (name, fn) => {
  try { fn(); n++; console.log('✓', name) }
  catch (e) { failures++; console.error(`✗ ${name}\n    ${e.message}`) }
}
const BASE = 'https://acct.blob.core.windows.net/images'

t('parseBlocksJson：純文字／[快訊] 開頭文字／空 → null', () => {
  assert.equal(parseBlocksJson('第一段\n\n第二段'), null)
  assert.equal(parseBlocksJson('[快訊] 今晚開賽'), null)
  assert.equal(parseBlocksJson(null), null)
  assert.equal(parseBlocksJson('{"weekday":1}'), null)
})
t('parseBlocksJson：陣列與 {blocks} 兩種外形', () => {
  assert.equal(parseBlocksJson('[{"blockType":"text","content":{}}]').length, 1)
  assert.equal(parseBlocksJson('{"blocks":[{"blockType":"text","content":{}}]}').length, 1)
})
t('雙語物件化簡：zh／en／en 空白退 zh', () => {
  const raw = [{ blockType: 'text', content: { body: { zh: '中文內文', en: 'English body' } } }]
  assert.deepEqual(normalizePageBlocks(raw, { locale: 'en' })[0].paragraphs, ['English body'])
  assert.deepEqual(normalizePageBlocks(raw, { locale: 'zh' })[0].paragraphs, ['中文內文'])
  const raw2 = [{ blockType: 'text', content: { body: { zh: '只有中文', en: null } } }]
  assert.deepEqual(normalizePageBlocks(raw2, { locale: 'en' })[0].paragraphs, ['只有中文'])
})
t('已化簡的字串（靜態頁後端輸出）原樣可用', () => {
  assert.equal(normalizePageBlocks([{ blockType: 'quote', content: { text: '已化簡' } }])[0].text, '已化簡')
})
t('未知型別略過；blockType 缺時認 type', () => {
  assert.deepEqual(normalizePageBlocks([{ blockType: 'script', content: { x: 1 } }]), [])
  assert.equal(normalizePageBlocks([{ type: 'quote', content: { text: 'q' } }]).length, 1)
})
t('video：YouTube／Vimeo → nocookie；其他來源與壞代碼拒絕', () => {
  assert.equal(videoEmbedSrc('youtube', 'dQw4w9WgXcQ'), 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ')
  assert.equal(videoEmbedSrc('vimeo', '123456789'), 'https://player.vimeo.com/video/123456789?dnt=1')
  assert.equal(videoEmbedSrc('youtube', 'x"><script>'), null)
  assert.equal(videoEmbedSrc('youtube', 'https://evil.example/x'), null)
  assert.equal(videoEmbedSrc('dailymotion', 'abcdefgh'), null)
  assert.equal(videoEmbedSrc('vimeo', 'abc'), null)
})
t('text_image：key + mediaBaseUrl；沒有基底則圖略過但文字保留；alt 依語系', () => {
  const raw = [{ blockType: 'text_image', content: { body: { zh: '文', en: null }, imagePosition: 'right', image: { key: 'pages/a.webp', width: 10, height: 5, altZh: '中替', altEn: 'en alt' } } }]
  const withBase = normalizePageBlocks(raw, { locale: 'en', mediaBaseUrl: BASE })[0]
  assert.equal(withBase.imagePosition, 'right')
  assert.deepEqual(withBase.image, { src: `${BASE}/pages/a.webp`, alt: 'en alt', width: 10, height: 5 })
  const noBase = normalizePageBlocks(raw)[0]
  assert.equal(noBase.image, null)
  assert.deepEqual(noBase.paragraphs, ['文'])
})
t('圖片網址消毒：javascript:／data:／..／絕對 key 一律拒絕', () => {
  const mk = (img) => normalizePageBlocks([{ blockType: 'gallery', content: { images: [img] } }], { mediaBaseUrl: BASE })
  assert.deepEqual(mk({ url: 'javascript:alert(1)', altZh: 'x' }), [])
  assert.deepEqual(mk({ key: '../secret.webp', altZh: 'x' }), [])
  assert.deepEqual(mk({ key: 'https://evil/x.webp', altZh: 'x' }), [])
  assert.deepEqual(mk({ key: '/abs.webp', altZh: 'x' }), [])
  assert.equal(mk({ url: 'https://cdn.example/a.webp', altZh: 'x' })[0].images[0].src, 'https://cdn.example/a.webp')
})
t('accordion_faq／file_download', () => {
  const faq = normalizePageBlocks([{ blockType: 'accordion_faq', content: { items: [{ question: { zh: 'Q', en: 'Q-en' }, answer: { zh: 'A', en: '' } }, { question: 'only', answer: '' }] } }], { locale: 'en' })[0]
  assert.deepEqual(faq.items, [{ question: 'Q-en', answer: 'A' }])
  assert.equal(normalizePageBlocks([{ blockType: 'file_download', content: { label: 'x', fileUrl: 'javascript:alert(1)' } }], { mediaBaseUrl: BASE }).length, 0)
  assert.equal(normalizePageBlocks([{ blockType: 'file_download', content: { label: 'x', fileUrl: 'https://a.example/f.pdf' } }])[0].href, 'https://a.example/f.pdf')
})

t('text 含簡單 HTML（種子頁）：標籤不得原樣輸出，段落以結尾標籤分隔；純文字不動', () => {
  const html = normalizePageBlocks([{ blockType: 'text', content: { body: '<h2>願景</h2><p>從台中出發 &amp; 放眼世界。</p>' } }])[0]
  assert.deepEqual(html.paragraphs, ['願景', '從台中出發 & 放眼世界。'])
  const li = normalizePageBlocks([{ blockType: 'text', content: { body: '<ul><li>甲</li><li>乙</li></ul>' } }])[0]
  assert.deepEqual(li.paragraphs, ['甲', '乙'])
  assert.deepEqual(normalizePageBlocks([{ blockType: 'text', content: { body: '1 < 2 且 3 > 2' } }])[0].paragraphs, ['1 < 2 且 3 > 2'])
})

console.log(`\n${n} 項通過，${failures} 項失敗`)
process.exit(failures ? 1 : 0)
