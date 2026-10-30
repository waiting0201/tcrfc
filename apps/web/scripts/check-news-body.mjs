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
import { parseNewsBody, safeUrl, videoEmbedUrl } from '../app/utils/news-body.ts'
import { focalObjectPosition } from '../app/utils/siteImage.ts'
import { isSessionListed, isSessionRegistrable, sessionSignupLabel, sessionSignupState, signupOpensText, statusLabel } from '../app/utils/program-session.ts'

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

// ── v3.28：key 圖片、圖集、影音、封面焦點 ──
const MEDIA = 'https://cdn.example.com/media/'
t('image 區塊以 mediaBaseUrl + key 組網址，帶 width／height／alt／caption 與 srcset', () => {
  const [b] = parseNewsBody(JSON.stringify({ blocks: [{ type: 'image', key: 'tcrfc/articles/1/body/0/image.webp', width: 1600, height: 2000, alt: '替代', caption: '說明' }] }), MEDIA)
  assert.equal(b.kind, 'image')
  assert.equal(b.src, 'https://cdn.example.com/media/tcrfc/articles/1/body/0/image.webp')
  assert.equal(b.width, 1600); assert.equal(b.height, 2000)
  assert.equal(b.alt, '替代'); assert.equal(b.caption, '說明')
  assert.match(b.srcset, /image-1024w\.webp 1024w|1024w/)
})
t('image key：無 mediaBaseUrl、含 .. 、絕對網址、開頭斜線 → 丟棄（無 src 可退）', () => {
  for (const key of ['a/b.webp', '../x.webp', 'https://evil.com/x.webp', '/abs.webp', 'javascript:x']) {
    const mb = key === 'a/b.webp' ? undefined : MEDIA
    assert.deepEqual(parseNewsBody(JSON.stringify([{ type: 'image', key }]), mb), [], key)
  }
})
t('image 舊 src／url 相容；無寬高時不帶 srcset', () => {
  const [b] = parseNewsBody('[{"type":"image","src":"https://x.com/a.webp"}]', MEDIA)
  assert.equal(b.src, 'https://x.com/a.webp'); assert.equal(b.width, null); assert.equal(b.srcset, undefined)
})
t('gallery：key 與寬高保留，壞項丟棄', () => {
  const [g] = parseNewsBody(JSON.stringify([{ type: 'gallery', images: [{ key: 'k/1.webp', width: 800, height: 600, alt: 'a' }, { key: '../x' }, { src: '/assets/z.jpg' }] }]), MEDIA)
  assert.equal(g.images.length, 2)
  assert.equal(g.images[0].src, 'https://cdn.example.com/media/k/1.webp')
  assert.equal(g.images[0].height, 600)
})
t('video：youtube／vimeo 對應 nocookie／player 網域；videoId 非 [A-Za-z0-9_-] 整塊丟棄', () => {
  const [y, v] = parseNewsBody(JSON.stringify([
    { type: 'video', provider: 'youtube', videoId: 'dQw4w9WgXcQ', caption: '片' },
    { type: 'video', provider: 'vimeo', videoId: '76979871' },
  ]))
  assert.equal(y.kind, 'video'); assert.equal(y.src, 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ'); assert.equal(y.caption, '片')
  assert.equal(v.src, 'https://player.vimeo.com/video/76979871')
  for (const id of ['a/b', 'a b', 'x"onload=1', '../x', '', 'a?b=1', '<script>']) assert.equal(videoEmbedUrl('youtube', id), null, id)
  assert.equal(videoEmbedUrl('dailymotion', 'abc'), null)
  assert.deepEqual(parseNewsBody('[{"type":"video","provider":"youtube","videoId":"a/b"}]'), [])
})
t('空文字區塊不輸出；舊 {"text"} 與純文字照舊', () => {
  assert.deepEqual(parseNewsBody('{"blocks":[{"type":"paragraph","text":"  "},{"type":"heading","text":""}]}'), [])
  assert.deepEqual(parseNewsBody('{"text":"舊"}'), [{ kind: 'p', lines: ['舊'] }])
})
t('封面焦點 object-position：缺值＝50% 50%、有值照寫、越界退 50', () => {
  assert.equal(focalObjectPosition(null, null), '50% 50%')
  assert.equal(focalObjectPosition(undefined, undefined), '50% 50%')
  assert.equal(focalObjectPosition(12.5, 80), '12.5% 80%')
  assert.equal(focalObjectPosition(0, 100), '0% 100%')
  assert.equal(focalObjectPosition(-1, 101), '50% 50%')
})

// ── 05 課程梯次狀態（SO-3：只依 statusCode，不比對時間、不看 status 字面值）──
t('statusCode 五種值：open 立即報名；full／waitlist 額滿候補；not_yet_open 停用；ended 已截止', () => {
  assert.equal(sessionSignupState({ statusCode: 'open' }), 'open')
  assert.equal(sessionSignupState({ statusCode: 'full' }), 'waitlist')
  assert.equal(sessionSignupState({ statusCode: 'waitlist' }), 'waitlist')
  assert.equal(sessionSignupState({ statusCode: 'not_yet_open' }), 'not_yet')
  assert.equal(sessionSignupState({ statusCode: 'ended' }), 'closed')
  assert.equal(sessionSignupLabel('open', 'zh'), '立即報名')
  assert.equal(sessionSignupLabel('waitlist', 'zh'), '額滿候補')
  assert.equal(sessionSignupLabel('closed', 'zh'), '已截止')
})
t('可報名／報名區列出：not_yet_open 列出但不可報名；ended 與未知都不列', () => {
  assert.equal(isSessionRegistrable({ statusCode: 'open' }), true)
  assert.equal(isSessionRegistrable({ statusCode: 'full' }), true)
  assert.equal(isSessionRegistrable({ statusCode: 'not_yet_open' }), false)
  assert.equal(isSessionRegistrable({ statusCode: 'ended' }), false)
  assert.equal(isSessionListed({ statusCode: 'not_yet_open' }), true)
  assert.equal(isSessionListed({ statusCode: 'ended' }), false)
  assert.equal(isSessionListed({ statusCode: 'brand_new' }), false)
})
t('不看 status 字面值、不比對時間：缺 statusCode＝未知＝已截止；時間欄位不影響結果', () => {
  assert.equal(sessionSignupState({ status: '開放' }), 'closed')
  assert.equal(sessionSignupState({ statusCode: 'ended', status: '開放' }), 'closed')
  assert.equal(sessionSignupState({ statusCode: 'open', signupOpensAt: '2999-01-01T00:00:00Z' }), 'open')
  assert.equal(sessionSignupState({ statusCode: 'open', signupClosesAt: '2000-01-01T00:00:00Z' }), 'open')
  assert.equal(sessionSignupState({ statusCode: 'not_yet_open', signupOpensAt: '2000-01-01T00:00:00Z' }), 'not_yet')
})
t('開放日期文字：台北時間換算、只顯示日期；中英兩語', () => {
  // 2026-11-04T16:30:00Z = 台北 11/5 00:30（UTC 日期與台北日期不同）
  assert.equal(signupOpensText('2026-11-04T16:30:00Z', 'zh'), '11 月 5 日開放報名')
  assert.equal(signupOpensText('2026-11-04T16:30:00Z', 'en'), 'Opens for registration on 5 Nov')
  // 台北 12/31 23:30 = UTC 15:30 同日
  assert.equal(signupOpensText('2026-12-31T15:30:00Z', 'zh'), '12 月 31 日開放報名')
  assert.equal(signupOpensText('2026-03-01T09:00:00Z', 'en'), 'Opens for registration on 1 Mar')
  assert.equal(signupOpensText(null), null)
  assert.equal(signupOpensText('not a date'), null)
})
t('statusLabel：後端標籤優先、本地代碼表次之、未知代碼退回字面值或代碼', () => {
  assert.equal(statusLabel({ statusCode: 'waitlisted' }, 'en'), 'Waitlisted')
  assert.equal(statusLabel({ statusCode: 'paid', statusLabelEn: 'Paid!' }, 'en'), 'Paid!')
  assert.equal(statusLabel({ statusCode: 'x', status: '新狀態' }, 'en'), '新狀態')
  assert.equal(statusLabel({ statusCode: 'x' }, 'zh'), 'x')
})
if (failures > 0) {
  console.error(`\n✗ 共 ${failures} 項斷言失敗`)
  process.exit(1)
}
console.log(`\n✓ 共 ${n} 組檢查全部通過`)
