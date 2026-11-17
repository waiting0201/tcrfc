#!/usr/bin/env node
/**
 * 新聞內文「區塊 JSON ⇄ 編輯器文件」轉換器的單元測試（`src/utils/newsBodyDoc.ts`）。
 * 與 test-news-blocks.mjs 同一套做法：用 Vite SSR 載入器載入 TypeScript，以 node:assert 驗證，不新增測試框架。
 * 真實資料取自 `db/migrations/20261015_news-body-import.sql` 的 83 篇匯入內文（全部來回比對）。
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const SQL = fileURLToPath(new URL('../../../db/migrations/20261015_news-body-import.sql', import.meta.url))
const server = await createServer({ root: ROOT, configFile: false, optimizeDeps: { noDiscovery: true, include: [] }, logLevel: 'error', appType: 'custom', server: { middlewareMode: true }, resolve: { alias: { '@': `${ROOT}src` } } })

let failed = 0
async function test(name, fn) {
  try {
    await fn()
    console.log(`  ok  ${name}`)
  } catch (error) {
    failed++
    console.error(`  FAIL  ${name}\n${error.stack}`)
  }
}

try {
  const D = await server.ssrLoadModule('/src/utils/newsBodyDoc.ts')

  const bi = (zh, en = '') => ({ zh, en })
  const slot = (n, altZh = '球員', altEn = 'Player') => ({ existingKey: `club/articles/1/body/${n}`, existingPreviewUrl: 't', existingWidth: 1600, existingHeight: 1200, file: null, cleared: false, altZh, altEn })
  const block = (blockType, content, localKey) => ({ localKey, blockType, content })
  const text = (zh, en, k) => block('text', { body: bi(zh, en) }, k)
  const clone = (v) => structuredClone(v)

  /** 一篇涵蓋七種區塊、含段內換行與英文的文章。 */
  const sample = () => [
    text('第一段\n含段內換行\n\n與連續空行', 'Para 1\nline 2', 'k1'),
    block('heading', { text: bi('小標', 'Heading') }, 'k2'),
    block('quote', { text: bi('引言內文\n第二行', 'Quote'), attribution: bi('某人', 'Someone') }, 'k3'),
    block('list', { ordered: true, items: [bi('甲', 'A'), bi('乙', 'B'), bi('丙', '')] }, 'k4'),
    block('list', { ordered: false, items: [bi('一'), bi('二')] }, 'k4b'),
    block('image', { image: slot(1), caption: bi('說明', 'Caption') }, 'k5'),
    block('gallery', { images: [slot(2), slot(3)] }, 'k6'),
    block('video_embed', { provider: 'youtube', videoId: 'abc_123-Z', caption: bi('影片', 'Video') }, 'k7'),
    text('結尾', 'End', 'k8'),
  ]
  /** 來回後 list 會補上 itemKeys（畫面專用欄位）；比對時只看送後端會用到的內容。 */
  const stripKeys = (blocks) => clone(blocks).map((b) => { if (b.content && b.content.itemKeys) delete b.content.itemKeys; return b })
  const roundtrip = (blocks, lang) => D.docToBlocks(D.blocksToDoc(blocks, lang), lang, blocks, D.createGraveyard())

  await test('七種區塊來回轉換無損（中、英各一次）', () => {
    for (const lang of ['zh', 'en']) {
      const blocks = sample()
      assert.deepEqual(stripKeys(roundtrip(blocks, lang)), stripKeys(blocks), lang)
    }
  })

  await test('中文編輯不會動到英文：改中文文字後英文原樣', () => {
    const blocks = sample()
    const doc = D.blocksToDoc(blocks, 'zh')
    doc.content[0].content = [{ type: 'text', text: '全新的第一段' }]
    const next = D.docToBlocks(doc, 'zh', blocks, D.createGraveyard())
    assert.equal(next[0].content.body.zh, '全新的第一段')
    assert.equal(next[0].content.body.en, 'Para 1\nline 2')
    assert.equal(next[1], blocks[1])
  })

  await test('圖片、圖集、影音區塊沿用同一物件（檔案與替代文字不被複製）', () => {
    const blocks = sample()
    const file = { name: 'a.jpg', size: 1 }
    blocks[5].content.image.file = file
    const next = roundtrip(blocks, 'en')
    assert.equal(next[5], blocks[5])
    assert.equal(next[5].content.image.file, file)
    assert.equal(next[6], blocks[6])
    assert.equal(next[7], blocks[7])
  })

  await test('在中文第一段後拆出新段落：英文各段不錯位，新段落英文為空', () => {
    const blocks = sample()
    const doc = D.blocksToDoc(blocks, 'zh')
    doc.content.splice(1, 0, { type: 'paragraph', attrs: { bid: 'new1' }, content: [{ type: 'text', text: '新插入的中文段' }] })
    const next = D.docToBlocks(doc, 'zh', blocks, D.createGraveyard())
    assert.equal(next.length, blocks.length + 1)
    assert.equal(next[1].localKey, 'new1')
    assert.deepEqual(next[1].content.body, { zh: '新插入的中文段', en: '' })
    assert.equal(next[2].content.text.en, 'Heading')
    // 英文文件跟著長出一個空段落，位置對得上
    const en = D.readDoc(D.blocksToDoc(next, 'en'))
    assert.deepEqual([en[0].text, en[1].text, en[2].text], ['Para 1\nline 2', '', 'Heading'])
  })

  await test('刪除區塊進墳場、復原用同一個 bid 完整還原（含英文與圖片檔）', () => {
    const blocks = sample()
    blocks[5].content.image.file = { name: 'x.jpg' }
    const grave = D.createGraveyard()
    const doc = D.blocksToDoc(blocks, 'zh')
    const removed = doc.content.splice(5, 1)
    const afterDelete = D.docToBlocks(doc, 'zh', blocks, grave)
    assert.equal(afterDelete.length, blocks.length - 1)
    assert.ok(grave.blocks.has('k5'))
    const undo = clone(doc)
    undo.content.splice(5, 0, removed[0])
    const afterUndo = D.docToBlocks(undo, 'zh', afterDelete, grave)
    assert.equal(afterUndo[5], blocks[5])
    assert.equal(grave.blocks.size, 0)
  })

  await test('清單：在中間插入項目，英文跟著對齊；刪除後復原帶回英文', () => {
    const blocks = [sample()[3], text('', '', 'tail')]
    const grave = D.createGraveyard()
    const doc = D.blocksToDoc(blocks, 'zh')
    doc.content[0].content.splice(1, 0, { type: 'listItem', attrs: { bid: 'item-new' }, content: [{ type: 'paragraph', content: [{ type: 'text', text: '插入' }] }] })
    const next = D.docToBlocks(doc, 'zh', blocks, grave)
    assert.deepEqual(next[0].content.items.map((i) => [i.zh, i.en]), [['甲', 'A'], ['插入', ''], ['乙', 'B'], ['丙', '']])
    const del = clone(doc)
    const [gone] = del.content[0].content.splice(2, 1)
    const afterDel = D.docToBlocks(del, 'zh', next, grave)
    assert.deepEqual(afterDel[0].content.items.map((i) => i.zh), ['甲', '插入', '丙'])
    const undo = clone(del)
    undo.content[0].content.splice(2, 0, gone)
    const back = D.docToBlocks(undo, 'zh', afterDel, grave)
    assert.deepEqual(back[0].content.items.map((i) => [i.zh, i.en]), [['甲', 'A'], ['插入', ''], ['乙', 'B'], ['丙', '']])
  })

  await test('清單改圓點／編號：項目識別碼被編輯器重建時依索引對齊，英文不丟', () => {
    const blocks = [sample()[3], text('', '', 'tail')]
    const doc = D.blocksToDoc(blocks, 'zh')
    doc.content[0] = { type: 'bulletList', attrs: { bid: 'k4' }, content: doc.content[0].content.map((li) => ({ ...li, attrs: { bid: null } })) }
    const next = D.docToBlocks(doc, 'zh', blocks, D.createGraveyard())
    assert.equal(next[0].content.ordered, false)
    assert.deepEqual(next[0].content.items.map((i) => i.en), ['A', 'B', ''])
  })

  await test('段落、小標、引言互相轉換：兩種語言的文字一起帶過去，不當成新區塊', () => {
    const blocks = sample()
    const doc = D.blocksToDoc(blocks, 'zh')
    doc.content[0] = { type: 'heading', attrs: { bid: 'k1', level: 2 }, content: [{ type: 'text', text: '改成小標' }] }
    doc.content[1] = { type: 'paragraph', attrs: { bid: 'k2' }, content: [{ type: 'text', text: '小標變段落' }] }
    const next = D.docToBlocks(doc, 'zh', blocks, D.createGraveyard())
    assert.equal(next[0].blockType, 'heading')
    assert.deepEqual(next[0].content.text, { zh: '改成小標', en: 'Para 1\nline 2' })
    assert.equal(next[0].localKey, 'k1')
    assert.equal(next[1].blockType, 'text')
    assert.deepEqual(next[1].content.body, { zh: '小標變段落', en: 'Heading' })
  })

  await test('段落轉成清單時英文留在第一項；型別完全不相干（圖片）則視為新區塊', () => {
    const blocks = sample()
    const doc = D.blocksToDoc(blocks, 'zh')
    doc.content[0] = { type: 'bulletList', attrs: { bid: 'k1' }, content: [{ type: 'listItem', attrs: { bid: null }, content: [{ type: 'paragraph', content: [{ type: 'text', text: '第一項' }] }] }] }
    doc.content[5] = { type: 'paragraph', attrs: { bid: 'k5' }, content: [{ type: 'text', text: '取代圖片' }] }
    const next = D.docToBlocks(doc, 'zh', blocks, D.createGraveyard())
    assert.deepEqual(next[0].content.items, [{ zh: '第一項', en: 'Para 1\nline 2' }])
    assert.equal(next[5].blockType, 'text')
    assert.deepEqual(next[5].content.body, { zh: '取代圖片', en: '' })
  })

  await test('引言的來源（attribution）依語言保存', () => {
    const blocks = sample()
    const doc = D.blocksToDoc(blocks, 'en')
    assert.equal(doc.content[2].attrs.attribution, 'Someone')
    doc.content[2].attrs.attribution = 'Another'
    const next = D.docToBlocks(doc, 'en', blocks, D.createGraveyard())
    assert.deepEqual(next[2].content.attribution, { zh: '某人', en: 'Another' })
  })

  await test('空文章：產生一個可輸入的空段落；整理後不留空段落', () => {
    assert.deepEqual(D.blocksToDoc([], 'zh').content, [{ type: 'paragraph' }])
    const withTrailing = D.ensureTrailingText([block('image', { image: slot(1), caption: bi('') }, 'i')])
    assert.equal(withTrailing.length, 2)
    assert.equal(withTrailing[1].blockType, 'text')
    assert.equal(D.ensureTrailingText(withTrailing), withTrailing)
    assert.equal(D.pruneEmptyTextBlocks(withTrailing).length, 1)
    // 英文有字的段落不能被當成空段落丟掉
    assert.equal(D.pruneEmptyTextBlocks([text('', 'only en', 'e')]).length, 1)
    assert.equal(D.pruneEmptyTextBlocks([text('  ', '', 'e')]).length, 0)
  })

  await test('文件簽章：只有目前語言文字不同才算不同', () => {
    const blocks = sample()
    assert.equal(D.docSignature(D.blocksToDoc(blocks, 'zh')), D.docSignature(D.blocksToDoc(clone(blocks), 'zh')))
    const changed = clone(blocks)
    changed[0].content.body.en = '改了英文'
    assert.equal(D.docSignature(D.blocksToDoc(blocks, 'zh')), D.docSignature(D.blocksToDoc(changed, 'zh')))
    assert.notEqual(D.docSignature(D.blocksToDoc(blocks, 'en')), D.docSignature(D.blocksToDoc(changed, 'en')))
  })

  await test('影音網址解析：YouTube／Vimeo 各種寫法，認不得回傳 null', () => {
    const yt = (id) => ({ provider: 'youtube', videoId: id })
    assert.deepEqual(D.parseVideoUrl('https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=3'), yt('dQw4w9WgXcQ'))
    assert.deepEqual(D.parseVideoUrl('https://youtu.be/dQw4w9WgXcQ?si=x'), yt('dQw4w9WgXcQ'))
    assert.deepEqual(D.parseVideoUrl('youtube.com/shorts/abc_DEF-123'), yt('abc_DEF-123'))
    assert.deepEqual(D.parseVideoUrl('https://m.youtube.com/embed/dQw4w9WgXcQ'), yt('dQw4w9WgXcQ'))
    assert.deepEqual(D.parseVideoUrl('https://vimeo.com/76979871'), { provider: 'vimeo', videoId: '76979871' })
    assert.deepEqual(D.parseVideoUrl('https://player.vimeo.com/video/76979871?h=1'), { provider: 'vimeo', videoId: '76979871' })
    for (const bad of ['', 'hello', 'https://example.com/watch?v=abc', 'https://www.youtube.com/watch', 'https://vimeo.com/channels/staffpicks']) {
      assert.equal(D.parseVideoUrl(bad), null, bad)
    }
    assert.equal(D.videoWatchUrl({ provider: 'vimeo', videoId: '1' }), 'https://vimeo.com/1')
  })

  // ── 真實資料：83 篇匯入內文（儲存格式 → 後台讀取形狀 → 編輯器 → 區塊） ──
  /** 對照 `NewsBodyBlocks.MergeBlock`：儲存格式的單語區塊 → 後台雙語區塊（en 另帶一份才有英文）。 */
  const merge = (z, e) => {
    const B = (a, b) => ({ zh: a ?? '', en: b ?? '' })
    switch (z.type) {
      case 'heading': return block('heading', { text: B(z.text, e?.text) })
      case 'quote': return block('quote', { text: B(z.text, e?.text), attribution: B(z.cite, e?.cite) })
      case 'list': return block('list', { ordered: !!z.ordered, items: z.items.map((t, i) => B(t, e?.items?.[i])) })
      default: return block('text', { body: B(z.text, e?.text) })
    }
  }
  const sql = readFileSync(SQL, 'utf8')
  const articles = [...sql.matchAll(/SET body = N'([\s\S]*?)' FROM articles_i18n/g)].map((m) => JSON.parse(m[1].replaceAll("''", "'")))

  await test('匯入的 83 篇新聞內文全部來回無損，並涵蓋小標、清單與段內換行', () => {
    assert.equal(articles.length, 83)
    const seen = { heading: 0, list: 0, newline: 0, long: 0, paragraphs: 0 }
    articles.forEach((a, n) => {
      const zhBlocks = a.blocks.map((z, i) => ({ ...merge(z, undefined), localKey: `a${n}-${i}` }))
      // 英文用同結構的模擬譯文，確認雙語都能無損
      const bilingual = a.blocks.map((z, i) => ({ ...merge(z, z.text !== undefined ? { text: `EN ${z.text}` } : { items: z.items.map((t) => `EN ${t}`) }), localKey: `a${n}-${i}` }))
      for (const blocks of [zhBlocks, bilingual]) {
        for (const lang of ['zh', 'en']) {
          assert.deepEqual(stripKeys(roundtrip(blocks, lang)), stripKeys(blocks), `第 ${n} 篇 ${lang}`)
        }
      }
      for (const b of zhBlocks) {
        if (b.blockType === 'heading') seen.heading++
        if (b.blockType === 'list') seen.list++
        if (b.blockType === 'text') {
          seen.paragraphs++
          if (b.content.body.zh.includes('\n')) seen.newline++
          if (b.content.body.zh.length > 300) seen.long++
        }
      }
    })
    console.log(`        段落 ${seen.paragraphs}、小標 ${seen.heading}、清單 ${seen.list}、含段內換行 ${seen.newline}、長段落 ${seen.long}`)
    assert.ok(seen.paragraphs > 0)
  })

  // ───────── 貼上填格子／英文分頁刪除保護（整篇貼上譯文的翻譯流程） ─────────
  /** 中文 8 段、第 4 段後夾一張圖，英文全空。 */
  const zhOnly = () => {
    const out = []
    for (let i = 1; i <= 8; i++) {
      out.push(text(`中文第${i}段`, '', `p${i}`))
      if (i === 4) out.push(block('image', { image: slot(1), caption: bi('說明', '') }, 'img'))
    }
    return out
  }
  const enLines = Array.from({ length: 8 }, (_, i) => `English paragraph ${i + 1}`)
  /** 套用規劃後走完「英文編輯器 → 區塊清單」，回傳新區塊。 */
  const applyEn = (blocks, plan) => D.docToBlocks(plan.doc, 'en', blocks, D.createGraveyard())
  const keysOf = (blocks) => blocks.map((b) => b.localKey)

  await test('flattenPasteLines：段落、小標、清單項目各一行，圖片與空行略過，裸文字依換行拆開', () => {
    const lines = D.flattenPasteLines([
      { type: 'paragraph', content: [{ type: 'text', text: ' A ' }] },
      { type: 'paragraph' },
      { type: 'heading', attrs: { level: 2 }, content: [{ type: 'text', text: 'H' }] },
      { type: 'newsImage' },
      { type: 'bulletList', content: [
        { type: 'listItem', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'x' }] }] },
        { type: 'listItem', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'y' }] }] },
      ] },
    ])
    assert.deepEqual(lines, ['A', 'H', 'x', 'y'])
    assert.deepEqual(D.flattenPasteLines([{ type: 'text', text: 'one\ntwo\n\nthree' }]), ['one', 'two', 'three'])
  })

  await test('(a) 游標在第 1 段貼 8 段：依序填入英文、跳過圖片、區塊數與 bid 不變', () => {
    const blocks = zhOnly()
    const plan = D.planPasteFill(D.blocksToDoc(blocks, 'en'), 0, [], enLines)
    assert.equal(plan.filled, 8)
    assert.equal(plan.appended, 0)
    const next = applyEn(blocks, plan)
    assert.deepEqual(keysOf(next), keysOf(blocks))
    const texts = next.filter((b) => b.blockType === 'text')
    texts.forEach((b, i) => {
      assert.equal(b.content.body.en, enLines[i])
      assert.equal(b.content.body.zh, `中文第${i + 1}段`)
    })
    assert.equal(next[4], blocks[4]) // 圖片沿用同一物件
  })

  await test('(b) 英文全選後貼上：不刪任何區塊，格子被換成新文字', () => {
    const blocks = zhOnly().map((b) => (b.blockType === 'text' ? { ...b, content: { body: bi(b.content.body.zh, '舊英文') } } : b))
    const doc = D.blocksToDoc(blocks, 'en')
    const all = doc.content.map((_, i) => i)
    const plan = D.planPasteFill(doc, 0, all, enLines)
    const next = applyEn(blocks, plan)
    assert.deepEqual(keysOf(next), keysOf(blocks))
    assert.equal(next.filter((b) => b.blockType === 'text').map((b) => b.content.body.en).join('|'), enLines.join('|'))
    assert.equal(next.filter((b) => b.blockType === 'text').every((b) => b.content.body.zh.startsWith('中文第')), true)
    // 貼的段數少於格子：選取範圍內多的格子英文清空、結構不動
    const fewer = applyEn(blocks, D.planPasteFill(doc, 0, all, enLines.slice(0, 3)))
    assert.deepEqual(keysOf(fewer), keysOf(blocks))
    assert.deepEqual(fewer.filter((b) => b.blockType === 'text').map((b) => b.content.body.en), [...enLines.slice(0, 3), '', '', '', '', ''])
  })

  await test('(c) 貼的段數多於空格子：多的接在最後一個被填的格子後面成新段落，原區塊 bid 不變', () => {
    const blocks = [text('中一', '', 'a'), text('中二', '', 'b'), block('image', { image: slot(1), caption: bi('', '') }, 'img')]
    const lines = ['E1', 'E2', 'E3', 'E4']
    const plan = D.planPasteFill(D.blocksToDoc(blocks, 'en'), 0, [], lines)
    assert.equal(plan.filled, 2)
    assert.equal(plan.appended, 2)
    const next = applyEn(blocks, plan)
    assert.equal(next.length, 5)
    assert.deepEqual(keysOf(next).filter((k) => k === 'a' || k === 'b' || k === 'img'), ['a', 'b', 'img'])
    assert.deepEqual(next.map((b) => b.blockType), ['text', 'text', 'text', 'text', 'image'])
    assert.deepEqual(next.map((b) => b.content.body?.en ?? null), ['E1', 'E2', 'E3', 'E4', null])
    assert.deepEqual(next.slice(2, 4).map((b) => b.content.body.zh), ['', ''])
  })

  await test('貼上遇到已有英文且未被選取的格子就停，其餘接在後面；清單項目也算格子', () => {
    const blocks = [
      text('甲', '', 'a'),
      text('乙', 'Already', 'b'),
      block('list', { ordered: false, items: [bi('一', ''), bi('二', '')], itemKeys: ['i1', 'i2'] }, 'l'),
    ]
    const stop = D.planPasteFill(D.blocksToDoc(blocks, 'en'), 0, [], ['X', 'Y', 'Z'])
    assert.equal(stop.filled, 1)
    assert.equal(stop.appended, 2)
    const stopped = applyEn(blocks, stop)
    assert.deepEqual(stopped.slice(0, 4).map((b) => b.content.body.en), ['X', 'Y', 'Z', 'Already'])
    assert.equal(stopped[3].localKey, 'b') // 既有英文的區塊 bid 不變，只是被新段落擠後
    const li = D.planPasteFill(D.blocksToDoc(blocks, 'en'), 2, [], ['X', 'Y', 'Z'])
    const next = applyEn(blocks, li)
    assert.deepEqual(next[2].content.items.map((i) => i.en), ['X', 'Y'])
    assert.deepEqual(next[2].content.items.map((i) => i.zh), ['一', '二'])
    assert.equal(next[3].content.body.en, 'Z')
  })

  await test('中文分頁同一規則：貼進空的中文格子也不新增區塊、不動英文', () => {
    const blocks = [text('', 'E1', 'a'), text('', 'E2', 'b')]
    const plan = D.planPasteFill(D.blocksToDoc(blocks, 'zh'), 0, [], ['一', '二'])
    const next = D.docToBlocks(plan.doc, 'zh', blocks, D.createGraveyard())
    assert.deepEqual(keysOf(next), ['a', 'b'])
    assert.deepEqual(next.map((b) => [b.content.body.zh, b.content.body.en]), [['一', 'E1'], ['二', 'E2']])
  })

  await test('(d) 英文分頁全選刪除：中文與圖片都還在、英文清空；中文分頁不保護', () => {
    const blocks = zhOnly().map((b) => (b.blockType === 'text' ? { ...b, content: { body: bi(b.content.body.zh, 'old') } } : b))
    const doc = D.blocksToDoc(blocks, 'en')
    const idx = doc.content.map((_, i) => i)
    const plan = D.planGuardedDelete(doc, 'en', blocks, 0, idx.length - 1, idx)
    assert.equal(plan.protect, true)
    const next = applyEn(blocks, { doc: plan.doc })
    assert.deepEqual(keysOf(next), keysOf(blocks))
    assert.equal(next.filter((b) => b.blockType === 'text').every((b) => b.content.body.en === '' && b.content.body.zh.startsWith('中文第')), true)
    assert.equal(next[4], blocks[4])
    assert.equal(D.planGuardedDelete(D.blocksToDoc(blocks, 'zh'), 'zh', blocks, 0, idx.length - 1, idx).protect, false)
    // 沒有中文、也沒有圖片的純英文區塊：照預設刪除
    const enOnly = [text('', 'a', 'a'), text('', 'b', 'b')]
    assert.equal(D.planGuardedDelete(D.blocksToDoc(enOnly, 'en'), 'en', enOnly, 0, 1, [0, 1]).protect, false)
    // 只選到單一區塊：不保護
    assert.equal(D.planGuardedDelete(doc, 'en', blocks, 2, 2, [2]).protect, false)
  })
} finally {
  await server.close()
}

if (failed > 0) {
  console.error(`\n${failed} 項失敗`)
  process.exit(1)
}
console.log('\n全部通過')
