#!/usr/bin/env node
/**
 * 新聞內文區塊與封面焦點的單元測試（規劃書 v3.28 §4.2 B2）。
 * 後台沒有測試框架，這裡用 Vite 的 SSR 載入器（沿用專案的 `@` 別名）直接載入 TypeScript 模組，
 * 以 node:assert 驗證，不新增任何相依套件。執行：`npm run test`。
 */
import assert from 'node:assert/strict'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
// adminNews 會連帶載入 http.ts → router（需要瀏覽器的 window）；測試只驗純函式，改用空殼取代。
const stubHttp = {
  name: 'stub-http',
  enforce: 'pre',
  resolveId: (id) => (id === './http' || id === '@/api/http' ? '\0stub-http' : null),
  load: (id) => (id === '\0stub-http' ? 'export const apiRequest = () => {}; export const apiUploadRequest = () => {}' : null),
}
const server = await createServer({ root: ROOT, configFile: false, plugins: [stubHttp], optimizeDeps: { noDiscovery: true, include: [] }, logLevel: 'error', appType: 'custom', server: { middlewareMode: true }, resolve: { alias: { '@': `${ROOT}src` } } })

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
  const types = await server.ssrLoadModule('/src/types/pageBlocks.ts')
  const ser = await server.ssrLoadModule('/src/utils/pageBlockSerializer.ts')
  const api = await server.ssrLoadModule('/src/api/adminNews.ts')

  const bi = (zh, en = '') => ({ zh, en })
  const img = { key: 'club/articles/1/body/4/image', width: 1600, height: 2000, altZh: '球員', altEn: 'Player', url: 'u', thumbUrl: 't' }

  /** 契約 B1 的七種內文區塊（API 回傳形狀），圖片為既有圖。 */
  const dtoBlocks = [
    { blockType: 'text', content: { body: bi('第一段\n\n第二段', 'Para') } },
    { blockType: 'heading', content: { text: bi('小標', 'Heading') } },
    { blockType: 'quote', content: { text: bi('引言'), attribution: bi('某人') } },
    { blockType: 'list', content: { ordered: true, items: [bi('甲', 'A'), bi('乙')] } },
    { blockType: 'image', content: { image: img, caption: bi('說明', 'Caption') } },
    { blockType: 'gallery', content: { images: [img, { ...img, key: 'k2' }] } },
    { blockType: 'video_embed', content: { provider: 'youtube', videoId: 'abc_123-Z', caption: bi('') } },
  ]
  const dto = { bodyBlocks: dtoBlocks }

  await test('detailDtoToBodyBlocks 還原七種區塊並保留順序', () => {
    const blocks = api.detailDtoToBodyBlocks(dto)
    assert.deepEqual(blocks.map((b) => b.blockType), ['text', 'heading', 'quote', 'list', 'image', 'gallery', 'video_embed'])
    assert.equal(blocks[3].content.ordered, true)
    assert.equal(blocks[4].content.image.existingKey, img.key)
    assert.equal(blocks[4].content.image.existingPreviewUrl, 't')
  })

  await test('往返：還原再序列化，內容與圖片鍵不變、不產生待上傳檔案', () => {
    const blocks = api.detailDtoToBodyBlocks(dto)
    const { blocks: out, files } = ser.serializeBlocksForSubmit(blocks)
    assert.equal(Object.keys(files).length, 0)
    assert.deepEqual(out[1].content.text, { zh: '小標', en: 'Heading' })
    assert.deepEqual(out[3].content, { ordered: true, items: [{ zh: '甲', en: 'A' }, { zh: '乙', en: null }] })
    assert.equal(out[4].content.image.key, img.key)
    assert.equal(out[4].content.image.width, 1600)
    assert.equal(out[5].content.images.length, 2)
    assert.equal(out[6].content.caption, undefined)
    assert.equal(out[0].blockType, 'text')
  })

  await test('新選的圖片以 file:{區塊索引}:{路徑} 命名（image／images:{i}）', () => {
    const blocks = api.detailDtoToBodyBlocks(dto)
    const f1 = new File(['a'], 'a.jpg')
    const f2 = new File(['b'], 'b.jpg')
    blocks[4].content.image.file = f1
    blocks[5].content.images[1].file = f2
    const { blocks: out, files } = ser.serializeBlocksForSubmit(blocks)
    assert.deepEqual(Object.keys(files).sort(), ['file:4:image', 'file:5:images:1'])
    assert.equal(out[4].content.image.pendingUpload, true)
    assert.equal(out[5].content.images[1].pendingUpload, true)
    assert.equal(out[5].content.images[0].key, img.key)
  })

  await test('驗證：小標空白、清單無項目、圖片未選都會擋下', () => {
    const empty = (t) => [types.createEmptyBlock(t)]
    assert.throws(() => ser.serializeBlocksForSubmit(empty('heading')), ser.PageBlockValidationError)
    assert.throws(() => ser.serializeBlocksForSubmit(empty('image')), ser.PageBlockValidationError)
    const list = types.createEmptyBlock('list')
    list.content.items = []
    assert.throws(() => ser.serializeBlocksForSubmit([list]), ser.PageBlockValidationError)
  })

  await test('B1 頁面管理的區塊型別清單不含新聞專用型別', () => {
    for (const t of ['heading', 'list', 'image']) assert.ok(!types.PAGE_BLOCK_TYPES.includes(t))
    assert.equal(types.PAGE_BLOCK_TYPES.length, 12)
    assert.deepEqual([...types.NEWS_BODY_BLOCK_TYPES], ['text', 'heading', 'quote', 'list', 'image', 'gallery', 'video_embed'])
  })

  await test('blocksHaveEnglish：只在有英文內容時為真', () => {
    const zhOnly = api.detailDtoToBodyBlocks({ bodyBlocks: [{ blockType: 'text', content: { body: bi('只有中文') } }] })
    assert.equal(ser.blocksHaveEnglish(zhOnly), false)
    assert.equal(ser.blocksHaveEnglish(api.detailDtoToBodyBlocks(dto)), true)
  })

  await test('契約的 type 欄位也能還原；沒有 bodyBlocks 視為空清單', () => {
    const blocks = api.detailDtoToBodyBlocks({ bodyBlocks: [{ type: 'heading', content: { text: bi('X') } }] })
    assert.equal(blocks[0].blockType, 'heading')
    assert.deepEqual(api.detailDtoToBodyBlocks({}), [])
  })

  const article = (focal) => ({
    id: '', title: bi('t'), urlName: 's', category: 'club', coverUrl: null, coverThumbUrl: null, coverKey: null,
    coverWidth: null, coverHeight: null, coverAlt: bi(''), coverFocalX: focal?.[0] ?? null, coverFocalY: focal?.[1] ?? null,
    isFeatured: false, status: 'draft', isSharedContent: false, updatedAt: '', content: bi(''), summary: bi('摘要'),
    seoTitle: bi(''), seoDescription: bi(''), seoKeywords: bi(''), canonicalPath: '', isNoindex: false,
    isExcludedFromSitemap: false, ogImageUrl: null, ogImageWidth: null, ogImageHeight: null, ogImageAlt: bi(''),
    tags: [], coreValueTags: [], relations: [], viewCount: 0,
  })

  await test('焦點：payload 帶 coverFocalX/Y；重設為 null 時兩者同為 null', () => {
    const p1 = api.articleToSavePayload(article([33.33, 66.67]))
    assert.equal(p1.coverFocalX, 33.33)
    assert.equal(p1.coverFocalY, 66.67)
    const p2 = api.articleToSavePayload(article(null))
    assert.equal(p2.coverFocalX, null)
    assert.equal(p2.coverFocalY, null)
  })

  await test('英文：標題等皆空但內文區塊有英文時仍保留 en 語系', () => {
    assert.equal(api.articleToSavePayload(article(null)).content.en, undefined)
    assert.ok(api.articleToSavePayload(article(null), true).content.en)
    assert.equal(api.articleToSavePayload(article(null), true).content.zh.body, null)
  })

  await test('buildArticleFormData 附上內文圖片欄位', () => {
    const f = new File(['x'], 'x.jpg')
    const form = api.buildArticleFormData({ slug: 's' }, null, null, { 'file:2:image': f })
    assert.ok(form.get('payload'))
    assert.ok(form.get('file:2:image'))
    assert.equal(form.get('file'), null)
  })
} finally {
  await server.close()
}

if (failed > 0) {
  console.error(`\n${failed} 項測試失敗`)
  process.exit(1)
}
console.log('\n全部通過')
