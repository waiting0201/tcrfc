#!/usr/bin/env node
/**
 * check-news-body.mjs — 新聞內文（ArticleDetailDto.bodyJson）解析與消毒、05 課程梯次狀態判斷的純函式驗證（2026-10-02）。
 *
 * 比照 `check-shop-lib.mjs`：不啟動任何服務，直接對 `app/utils/*.ts` 做單元測試，掛進 `npm run lint`。
 * 驗證的是：① 後台目前送出的純文字（含 HTML 字元）一律當文字、段落以空行分隔；② 區塊 JSON 只認白名單型別，
 * 圖片網址只放行 http(s)／站內路徑（`javascript:`／`data:`／`//host` 丟棄）；③ 梯次狀態是**中文字面值**
 * （開放／額滿／候補／已結束，不是 open／waitlist），報名窗口外與已結束不可報名（E-130）。
 *
 * 用法：node scripts/check-news-body.mjs　　離開碼：任一斷言失敗 → 1；全部通過 → 0。
 */
import assert from 'node:assert/strict'
import { parseNewsBody, safeUrl } from '../app/utils/news-body.ts'
import { isSessionRegistrable, sessionSignupState, statusLabel } from '../app/utils/program-session.ts'

let n = 0
let failures = 0
const t = (name, fn) => {
  try { fn(); n++; console.log('✓', name) }
  catch (e) { failures++; console.error(`✗ ${name}\n    ${e.message}`) }
}


t('null/空白 → []', () => { assert.deepEqual(parseNewsBody(null), []); assert.deepEqual(parseNewsBody('  \n '), []) })
t('純文字（ArticleBodyJsonColumnTests 的樣本）：段落、單行斷行、HTML 字元原樣為文字', () => {
  const b = parseNewsBody("第一段 <b>& 'quote'</b> + https://example.com/a?x=1&y=2\n\n第二段")
  assert.equal(b.length, 2)
  assert.deepEqual(b[0].lines, ["第一段 <b>& 'quote'</b> + https://example.com/a?x=1&y=2"])
  assert.deepEqual(b[1].lines, ['第二段'])
})
t('段內單換行保留為多行', () => { assert.deepEqual(parseNewsBody('a\nb').map((x) => x.lines), [['a', 'b']]) })
t('CRLF 與多個空行', () => { assert.equal(parseNewsBody('a\r\n\r\n\r\n\r\nb').length, 2) })
t('區塊 JSON（測試樣本 {"blocks":[{"type":"p","text":"區塊"}]}）', () => {
  assert.deepEqual(parseNewsBody('{"blocks":[{"type":"p","text":"區塊"}]}'), [{ kind: 'p', lines: ['區塊'] }])
})
t('以 [ 開頭但不是 JSON 的純文字 → 純文字', () => {
  const b = parseNewsBody('[快訊] 今晚開賽')
  assert.deepEqual(b, [{ kind: 'p', lines: ['[快訊] 今晚開賽'] }])
})
t('{"text":"…"} 原樣 → 純文字', () => { assert.deepEqual(parseNewsBody('{"text":"你好"}'), [{ kind: 'p', lines: ['你好'] }]) })
t('標題/引言/清單/圖片/圖集', () => {
  const b = parseNewsBody(JSON.stringify([
    { type: 'h2', text: '大標' }, { type: 'h3', text: '小標' },
    { type: 'quote', text: '話', cite: '某人' },
    { type: 'ul', items: ['a', '', 'b'] }, { type: 'ol', items: ['x'] },
    { type: 'image', url: 'https://cdn.example.com/a.jpg', alt: '圖', caption: '說明' },
    { type: 'gallery', images: [{ src: '/assets/x.jpg' }, { src: 'javascript:alert(1)' }] },
  ]))
  assert.deepEqual(b.map((x) => x.kind), ['heading', 'heading', 'quote', 'list', 'list', 'image', 'gallery'])
  assert.equal(b[3].items.length, 2)
  assert.equal(b[6].images.length, 1)
})
t('XSS：危險網址一律丟棄', () => {
  for (const u of ['javascript:alert(1)', 'data:text/html;base64,AAA', '//evil.com/x.png', '/\\evil.com', 'vbscript:x', ' JaVaScRiPt:alert(1)']) assert.equal(safeUrl(u), null, u)
  assert.equal(safeUrl('https://a.com/x'), 'https://a.com/x')
  assert.equal(safeUrl('/assets/a.png'), '/assets/a.png')
  assert.deepEqual(parseNewsBody('[{"type":"image","url":"javascript:alert(1)"}]'), [])
})
t('script 標籤只是文字（元件以文字插值輸出）', () => {
  const b = parseNewsBody('<script>alert(1)</script>')
  assert.deepEqual(b, [{ kind: 'p', lines: ['<script>alert(1)</script>'] }])
})
t('未知區塊型別忽略、非物件項忽略', () => { assert.deepEqual(parseNewsBody('[{"type":"video","url":"https://x"}, 5, null]'), []) })

// ── 05 課程梯次狀態 ──
const now = new Date('2026-10-02T00:00:00Z')
t('梯次狀態是中文字面值：開放／額滿／候補可報名，已結束不可', () => {
  assert.equal(sessionSignupState({ status: '開放' }, now), 'open')
  assert.equal(sessionSignupState({ status: '額滿' }, now), 'waitlist')
  assert.equal(sessionSignupState({ status: '候補' }, now), 'waitlist')
  assert.equal(sessionSignupState({ status: '已結束' }, now), 'closed')
  assert.equal(isSessionRegistrable({ status: '開放' }, now), true)
  assert.equal(isSessionRegistrable({ status: '已結束' }, now), false)
})
t('字面值欄位放英文 open 不是合法的「開放」（舊 bug）；未知狀態保守視為已截止', () => {
  assert.equal(sessionSignupState({ status: 'open' }, now), 'closed')
})
t('statusCode 為準：open／full／waitlist／ended，未知代碼容錯為不可報名；statusCode 優先於字面值', () => {
  assert.equal(sessionSignupState({ statusCode: 'open' }, now), 'open')
  assert.equal(sessionSignupState({ statusCode: 'full' }, now), 'waitlist')
  assert.equal(sessionSignupState({ statusCode: 'waitlist' }, now), 'waitlist')
  assert.equal(sessionSignupState({ statusCode: 'ended' }, now), 'closed')
  assert.equal(sessionSignupState({ statusCode: 'brand_new' }, now), 'closed')
  assert.equal(sessionSignupState({ statusCode: 'ended', status: '開放' }, now), 'closed')
})
t('statusLabel：後端標籤優先、本地代碼表次之、未知代碼退回字面值或代碼', () => {
  assert.equal(statusLabel({ statusCode: 'waitlisted' }, 'en'), 'Waitlisted')
  assert.equal(statusLabel({ statusCode: 'paid', statusLabelEn: 'Paid!' }, 'en'), 'Paid!')
  assert.equal(statusLabel({ statusCode: 'x', status: '新狀態' }, 'en'), '新狀態')
  assert.equal(statusLabel({ statusCode: 'x' }, 'zh'), 'x')
})
t('報名窗口：尚未開放、已截止都不可報名', () => {
  assert.equal(sessionSignupState({ status: '開放', signupOpensAt: '2026-11-01T00:00:00Z' }, now), 'not_yet')
  assert.equal(isSessionRegistrable({ status: '開放', signupOpensAt: '2026-11-01T00:00:00Z' }, now), false)
  assert.equal(sessionSignupState({ status: '開放', signupClosesAt: '2026-09-30T00:00:00Z' }, now), 'closed')
  assert.equal(isSessionRegistrable({ status: '開放', signupOpensAt: '2026-09-01T00:00:00Z', signupClosesAt: '2026-10-31T00:00:00Z' }, now), true)
})

if (failures > 0) {
  console.error(`\n✗ 共 ${failures} 項斷言失敗`)
  process.exit(1)
}
console.log(`\n✓ 共 ${n} 組檢查全部通過`)
