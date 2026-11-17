/**
 * 新聞內文：區塊 JSON（`PageBlockState[]`，雙語在同一份清單）⇄ 編輯器文件（TipTap／ProseMirror JSON）互轉。
 * 本檔是純函式、不引用 TipTap，測試直接載入（`scripts/test-news-body-doc.mjs`）。
 *
 * 為什麼是「每個語言一份文件、共用一份區塊清單」：儲存格式不變（`{"blocks":[…]}`，中英結構與順序相同、
 * 依索引對齊，見 `apps/api/Features/AdminNews/NewsBodyBlocks.cs`），所以編輯器的每個頂層節點對應一個區塊：
 *
 *   段落(paragraph) ↔ text    小標(heading) ↔ heading    引言(newsQuote) ↔ quote
 *   項目／編號清單(bulletList／orderedList) ↔ list  圖片(newsImage) ↔ image
 *   圖集(newsGallery) ↔ gallery  影音(newsVideo) ↔ video_embed
 *
 * - 每個頂層節點（與清單項目）帶一個穩定識別碼 `bid`＝區塊的 `localKey`（清單項目為 `itemKeys[i]`）。
 *   文字只存在於「正在編輯的那一種語言」的文件裡；另一種語言的文字留在區塊清單，靠 `bid` 找回，
 *   所以在中文分頁插入、拆開、刪除段落時，英文不會錯位。
 * - 圖片、圖集、影音的內容（檔案、替代文字、說明、影片代碼）不進文件，直接在區塊清單上就地編輯（節點畫面元件負責）。
 * - 被刪掉的區塊（含使用者按「復原」會回來的）收在 `Graveyard`，復原時用同一個 `bid` 完整還原，包含另一種語言與圖片檔案。
 */
import {
  createEmptyBlock,
  type BilingualText,
  type HeadingBlockContent,
  type ListBlockContent,
  type PageBlockState,
  type PageBlockType,
  type QuoteBlockContent,
  type TextBlockContent,
  type VideoEmbedBlockContent,
} from '@/types/pageBlocks'

export type NewsLang = 'zh' | 'en'

/** 編輯器文件節點（ProseMirror JSON 的子集）。 */
export interface DocNode {
  type: string
  attrs?: Record<string, unknown>
  content?: DocNode[]
  text?: string
}
export interface DocJSON {
  type: 'doc'
  content: DocNode[]
}

/** 區塊型別 ↔ 頂層節點名稱。 */
export const NODE_BY_BLOCK = {
  text: 'paragraph',
  heading: 'heading',
  quote: 'newsQuote',
  list: 'bulletList', // 有序清單另用 orderedList
  image: 'newsImage',
  gallery: 'newsGallery',
  video_embed: 'newsVideo',
} as const

const BLOCK_BY_NODE: Record<string, PageBlockType> = {
  paragraph: 'text',
  heading: 'heading',
  newsQuote: 'quote',
  bulletList: 'list',
  orderedList: 'list',
  newsImage: 'image',
  newsGallery: 'gallery',
  newsVideo: 'video_embed',
}

/** 帶有 `bid` 的節點型別（頂層節點；清單項目 listItem 另外處理）。 */
export const BID_NODE_TYPES = ['paragraph', 'heading', 'newsQuote', 'bulletList', 'orderedList', 'listItem', 'newsImage', 'newsGallery', 'newsVideo']

export function newKey(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `local-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

// ───────────────────────────── 文字 ⇄ 行內節點 ─────────────────────────────

/** `\n` 是段內換行（硬換行）；匯入的新聞稿「段內換行保留」就是這個。 */
export function textToInline(text: string): DocNode[] {
  const out: DocNode[] = []
  text.split('\n').forEach((part, i) => {
    if (i > 0) out.push({ type: 'hardBreak' })
    if (part) out.push({ type: 'text', text: part })
  })
  return out
}

export function inlineToText(nodes: DocNode[] | undefined): string {
  let s = ''
  for (const n of nodes ?? []) {
    if (n.type === 'text') s += n.text ?? ''
    else if (n.type === 'hardBreak') s += '\n'
  }
  return s
}

function withContent(node: DocNode, text: string): DocNode {
  const inline = textToInline(text)
  return inline.length ? { ...node, content: inline } : node
}

// ───────────────────────────── 區塊 → 文件 ─────────────────────────────

/** 清單項目的穩定識別碼；舊資料沒有 `itemKeys` 時用 `區塊key:i序號` 湊出（決定性，同一份資料每次相同）。 */
export function listItemKeys(block: PageBlockState): string[] {
  const c = block.content as ListBlockContent
  return c.items.map((_, i) => c.itemKeys?.[i] ?? `${block.localKey}:i${i}`)
}

export function blocksToDoc(blocks: PageBlockState[], lang: NewsLang): DocJSON {
  const content: DocNode[] = []
  for (const block of blocks) {
    const bid = block.localKey
    switch (block.blockType) {
      case 'text':
        content.push(withContent({ type: 'paragraph', attrs: { bid } }, (block.content as TextBlockContent).body[lang]))
        break
      case 'heading':
        content.push(withContent({ type: 'heading', attrs: { bid, level: 2 } }, (block.content as HeadingBlockContent).text[lang]))
        break
      case 'quote': {
        const c = block.content as QuoteBlockContent
        content.push(withContent({ type: 'newsQuote', attrs: { bid, attribution: c.attribution[lang] } }, c.text[lang]))
        break
      }
      case 'list': {
        const c = block.content as ListBlockContent
        const keys = listItemKeys(block)
        const items: DocNode[] = c.items.map((item, i) => ({
          type: 'listItem',
          attrs: { bid: keys[i] },
          content: [withContent({ type: 'paragraph' }, item[lang])],
        }))
        if (items.length === 0) items.push({ type: 'listItem', attrs: { bid: `${bid}:i0` }, content: [{ type: 'paragraph' }] })
        content.push({ type: c.ordered ? 'orderedList' : 'bulletList', attrs: { bid }, content: items })
        break
      }
      case 'image':
        content.push({ type: 'newsImage', attrs: { bid } })
        break
      case 'gallery':
        content.push({ type: 'newsGallery', attrs: { bid } })
        break
      case 'video_embed':
        content.push({ type: 'newsVideo', attrs: { bid } })
        break
      default:
        // 新聞內文只有上述七種（API 只會回這七種）；其他型別不屬於這個編輯器，略過而不是壞掉整頁。
        break
    }
  }
  if (content.length === 0) content.push({ type: 'paragraph' })
  return { type: 'doc', content }
}

// ───────────────────────────── 文件 → 區塊 ─────────────────────────────

/** 文件讀出來的精簡形狀：只含轉換與比對用得到的資訊，避免被編輯器補上的預設屬性干擾比對。 */
export interface ReadNode {
  kind: PageBlockType
  bid: string | null
  text: string
  attribution: string
  ordered: boolean
  items: { bid: string | null; text: string }[]
}

export function readDoc(doc: DocJSON): ReadNode[] {
  const out: ReadNode[] = []
  for (const node of doc.content ?? []) {
    const kind = BLOCK_BY_NODE[node.type]
    if (!kind) continue
    const bid = (node.attrs?.bid as string | null | undefined) ?? null
    const read: ReadNode = { kind, bid, text: '', attribution: '', ordered: node.type === 'orderedList', items: [] }
    if (kind === 'text' || kind === 'heading' || kind === 'quote') read.text = inlineToText(node.content)
    if (kind === 'quote') read.attribution = String(node.attrs?.attribution ?? '')
    if (kind === 'list') {
      read.items = (node.content ?? []).map((li) => ({
        bid: (li.attrs?.bid as string | null | undefined) ?? null,
        text: inlineToText(li.content?.[0]?.content),
      }))
    }
    out.push(read)
  }
  return out
}

/** 兩份文件在「結構與目前語言文字」上是否相同（用來判斷另一個編輯器要不要重畫）。 */
export function docSignature(doc: DocJSON): string {
  return JSON.stringify(readDoc(doc))
}

/** 被刪掉的區塊與清單項目，使用者復原時以同一個 bid 還原（含另一種語言與已選的圖片檔案）。 */
export interface Graveyard {
  blocks: Map<string, PageBlockState>
  items: Map<string, BilingualText>
}
export function createGraveyard(): Graveyard {
  return { blocks: new Map(), items: new Map() }
}

function setLang(pair: BilingualText, lang: NewsLang, value: string): BilingualText {
  return lang === 'zh' ? { zh: value, en: pair.en } : { zh: pair.zh, en: value }
}

const TEXTUAL: PageBlockType[] = ['text', 'heading', 'quote']

function textPairOf(block: PageBlockState): BilingualText {
  const c = block.content as unknown as { body?: BilingualText; text?: BilingualText }
  return c.body ?? c.text ?? { zh: '', en: '' }
}

/**
 * 使用者在編輯器裡把節點換型別（段落⇄小標⇄引言、段落變清單）時，編輯器保留了同一個 bid：
 * 兩種語言的文字一起帶過去，不能當成新區塊（否則另一種語言的文字會不見）。其他組合視為新區塊。
 */
function convertBlock(base: PageBlockState, kind: PageBlockType): PageBlockState | undefined {
  if (!TEXTUAL.includes(base.blockType)) return undefined
  const pair = textPairOf(base)
  const fresh = { ...createEmptyBlock(kind), localKey: base.localKey }
  if (kind === 'text') return { ...fresh, content: { body: { ...pair } } satisfies TextBlockContent }
  if (kind === 'heading') return { ...fresh, content: { text: { ...pair } } satisfies HeadingBlockContent }
  if (kind === 'quote') return { ...fresh, content: { text: { ...pair }, attribution: { zh: '', en: '' } } satisfies QuoteBlockContent }
  if (kind === 'list') return { ...fresh, content: { ordered: false, items: [{ ...pair }], itemKeys: [`${base.localKey}:i0`] } satisfies ListBlockContent }
  return undefined
}

/**
 * 把「正在編輯的語言」的文件合併回區塊清單：順序、型別、該語言的文字以文件為準；
 * 另一種語言的文字、圖片等內容以 `bid` 從 `prev`（或墳場）找回。
 * 內容沒變的區塊沿用同一個物件（少一點不必要的重算）。
 */
export function docToBlocks(doc: DocJSON, lang: NewsLang, prev: PageBlockState[], grave: Graveyard): PageBlockState[] {
  const prevByKey = new Map(prev.map((b) => [b.localKey, b]))
  const used = new Set<string>()
  const out: PageBlockState[] = []

  for (const node of readDoc(doc)) {
    let base = node.bid ? prevByKey.get(node.bid) ?? grave.blocks.get(node.bid) : undefined
    if (base && used.has(base.localKey)) base = undefined
    if (base && base.blockType !== node.kind) base = convertBlock(base, node.kind)
    const restored = !!base && !!node.bid && !prevByKey.has(node.bid)
    if (!base) {
      const key = node.bid && !used.has(node.bid) ? node.bid : newKey()
      base = { ...createEmptyBlock(node.kind), localKey: key }
    }
    if (restored) grave.blocks.delete(base.localKey)
    used.add(base.localKey)

    switch (node.kind) {
      case 'text': {
        const c = base.content as TextBlockContent
        out.push(c.body[lang] === node.text ? base : { ...base, content: { body: setLang(c.body, lang, node.text) } satisfies TextBlockContent })
        break
      }
      case 'heading': {
        const c = base.content as HeadingBlockContent
        out.push(c.text[lang] === node.text ? base : { ...base, content: { text: setLang(c.text, lang, node.text) } satisfies HeadingBlockContent })
        break
      }
      case 'quote': {
        const c = base.content as QuoteBlockContent
        if (c.text[lang] === node.text && c.attribution[lang] === node.attribution) out.push(base)
        else out.push({ ...base, content: { text: setLang(c.text, lang, node.text), attribution: setLang(c.attribution, lang, node.attribution) } satisfies QuoteBlockContent })
        break
      }
      case 'list': {
        const c = base.content as ListBlockContent
        const prevKeys = listItemKeys(base)
        const byKey = new Map<string, BilingualText>(prevKeys.map((k, i) => [k, c.items[i]]))
        // 整份清單的項目識別碼都對不上、項目數卻一樣（例如圓點改編號、編輯器重建了清單）：改依索引對齊。
        const allUnknown = node.items.length > 0 && node.items.length === c.items.length
          && node.items.every((it) => !it.bid || (!byKey.has(it.bid) && !grave.items.has(it.bid)))
        const keys: string[] = []
        const items = node.items.map((it, i) => {
          const key = allUnknown ? prevKeys[i] : it.bid && !keys.includes(it.bid) ? it.bid : newKey()
          keys.push(key)
          const old = allUnknown ? c.items[i] : byKey.get(key) ?? grave.items.get(key)
          grave.items.delete(key)
          return setLang(old ?? { zh: '', en: '' }, lang, it.text)
        })
        for (const [k, v] of byKey) if (!keys.includes(k)) grave.items.set(k, v)
        out.push({ ...base, content: { ordered: node.ordered, items, itemKeys: keys } satisfies ListBlockContent })
        break
      }
      default:
        out.push(base) // 圖片、圖集、影音：內容在區塊清單上就地編輯，文件只決定位置
    }
  }

  for (const b of prev) if (!used.has(b.localKey)) grave.blocks.set(b.localKey, b)
  return out
}

// ───────────────────────────── 存檔前整理 ─────────────────────────────

/** 編輯器永遠留著一個可輸入的空段落（文章最後、圖片之間）；存檔與比對前去掉「中英都沒有字」的段落。 */
export function pruneEmptyTextBlocks(blocks: PageBlockState[]): PageBlockState[] {
  return blocks.filter((b) => {
    if (b.blockType !== 'text') return true
    const body = (b.content as TextBlockContent).body
    return body.zh.trim() !== '' || body.en.trim() !== ''
  })
}

/** 清單最後一個區塊不是段落時補一個空段落，使用者才有地方接著打字（與編輯器的 TrailingNode 一致）。 */
export function ensureTrailingText(blocks: PageBlockState[]): PageBlockState[] {
  const last = blocks[blocks.length - 1]
  if (last && last.blockType === 'text') return blocks
  return [...blocks, { ...createEmptyBlock('text'), localKey: newKey() }]
}

// ───────────────────────────── 貼上「填格子」與跨區塊刪除保護 ─────────────────────────────
//
// 為什麼需要：兩個語言的編輯器共用同一份區塊結構，`bid` 又刻意不從貼上的 HTML 解析（見 `extensions.ts`）。
// 在英文分頁貼上整篇譯文，若照預設行為，每一段都會變成「新區塊」（中文空白），原本帶中文的區塊被擠到後面；
// 全選後貼上或刪除，帶中文與圖片的區塊更會整個被刪掉。所以改成：貼上時依序把文字「填進」目前語言的空格子，
// 結構（區塊數、bid、圖片）一個都不動；英文分頁的跨區塊刪除只清空英文文字。

const TEXT_NODE_TYPES = ['paragraph', 'heading', 'newsQuote']
const LIST_NODE_TYPES = ['bulletList', 'orderedList']
const ATOM_NODE_TYPES = ['newsImage', 'newsGallery', 'newsVideo']

/** 把貼上的內容攤平成文字行：段落、小標、引言、清單項目各一行；圖片等沒有文字的節點略過；空行丟掉。 */
export function flattenPasteLines(nodes: DocNode[]): string[] {
  const lines: string[] = []
  const clean = (s: string) => s.replace(/\u00a0/g, ' ').replace(/[ \t]+\n/g, '\n').trim()
  const push = (s: string) => {
    const t = clean(s)
    if (t) lines.push(t)
  }
  const walk = (list: DocNode[]) => {
    let inlineBuf: DocNode[] = []
    const flush = () => {
      if (inlineBuf.length) for (const part of inlineToText(inlineBuf).split(/\n+/)) push(part)
      inlineBuf = []
    }
    for (const n of list) {
      if (n.type === 'text' || n.type === 'hardBreak') {
        inlineBuf.push(n)
        continue
      }
      flush()
      if (TEXT_NODE_TYPES.includes(n.type)) push(inlineToText(n.content))
      else if (n.content) walk(n.content)
    }
    flush()
  }
  walk(nodes)
  return lines
}

interface Cell {
  top: number
  item: number // -1＝頂層節點本身；否則是清單項目的索引
  text: string
}

function listCells(doc: DocJSON, startIdx: number): Cell[] {
  const cells: Cell[] = []
  doc.content.forEach((node, top) => {
    if (top < startIdx) return
    if (TEXT_NODE_TYPES.includes(node.type)) cells.push({ top, item: -1, text: inlineToText(node.content) })
    else if (LIST_NODE_TYPES.includes(node.type)) {
      (node.content ?? []).forEach((li, item) => cells.push({ top, item, text: inlineToText(li.content?.[0]?.content) }))
    }
    // 圖片、圖集、影音：不佔格
  })
  return cells
}

function withText(node: DocNode, text: string): DocNode {
  const { content: _drop, ...rest } = node
  void _drop
  const inline = textToInline(text)
  return inline.length ? { ...rest, content: inline } : rest
}

function setCell(doc: DocJSON, cell: Cell, text: string) {
  const node = doc.content[cell.top]
  if (cell.item < 0) {
    doc.content[cell.top] = withText(node, text)
    return
  }
  const items = [...(node.content ?? [])]
  const li = items[cell.item]
  items[cell.item] = { ...li, content: [withText(li.content?.[0] ?? { type: 'paragraph' }, text)] }
  doc.content[cell.top] = { ...node, content: items }
}

export interface PasteFillPlan {
  doc: DocJSON
  /** 填進既有格子的行數 */
  filled: number
  /** 格子不夠而接在後面成新段落的行數 */
  appended: number
  /** 最後動到的頂層節點索引（游標放這裡） */
  lastIdx: number
}

/**
 * 規劃貼上：從 `startIdx`（游標所在的頂層節點）起依序找格子，格子在「目前語言」為空、或落在選取範圍（`selectedIdx`，頂層節點索引）內，
 * 就填入下一行；碰到有字且未被選取的格子就停。沒填完的行接在最後一個被填的格子之後成新段落（沒有新的 bid，由編輯器補）。
 * 選取範圍內沒被填到的格子，目前語言的文字清空（使用者選了就是要換掉）；結構與 bid 不動。
 */
export function planPasteFill(source: DocJSON, startIdx: number, selectedIdx: number[], lines: string[]): PasteFillPlan {
  const doc: DocJSON = { type: 'doc', content: [...source.content] }
  const selected = new Set(selectedIdx)
  const cells = listCells(doc, startIdx)
  let li = 0
  let lastTop = -1
  for (let c = 0; c < cells.length && li < lines.length; c++) {
    const cell = cells[c]
    if (cell.text.trim() === '' || selected.has(cell.top)) {
      setCell(doc, cell, lines[li++])
      lastTop = cell.top
    } else {
      break
    }
  }
  const filled = li
  // 選取範圍內沒被填到的格子：清空目前語言的文字
  for (const cell of cells.slice(filled)) {
    if (selected.has(cell.top) && cell.text !== '') setCell(doc, cell, '')
  }
  let lastIdx = lastTop >= 0 ? lastTop : Math.min(startIdx, doc.content.length - 1)
  const rest = lines.slice(li)
  if (rest.length) {
    const extra: DocNode[] = rest.map((t) => withText({ type: 'paragraph' }, t))
    doc.content.splice(lastIdx + 1, 0, ...extra)
    lastIdx += rest.length
  }
  return { doc, filled, appended: rest.length, lastIdx }
}

/** 區塊裡某語言是否有文字（遞迴看所有 `lang` 欄位；清單、引言說明都算）。 */
export function blockHasLangText(block: PageBlockState | undefined, lang: NewsLang): boolean {
  const walk = (v: unknown): boolean => {
    if (v === null || typeof v !== 'object' || (typeof File !== 'undefined' && v instanceof File)) return false
    if (Array.isArray(v)) return v.some(walk)
    return Object.entries(v as Record<string, unknown>).some(([k, x]) => (k === lang && typeof x === 'string' && x.trim() !== '') || walk(x))
  }
  return !!block && walk(block.content)
}

/** 把指定頂層節點的文字清空（清單的每個項目、引言的出處都清）；圖片等節點不動。 */
export function clearTextInNodes(source: DocJSON, idxs: number[]): DocJSON {
  const doc: DocJSON = { type: 'doc', content: [...source.content] }
  for (const i of idxs) {
    const node = doc.content[i]
    if (!node) continue
    if (TEXT_NODE_TYPES.includes(node.type)) {
      const next = withText(node, '')
      doc.content[i] = node.type === 'newsQuote' ? { ...next, attrs: { ...next.attrs, attribution: '' } } : next
    } else if (LIST_NODE_TYPES.includes(node.type)) {
      doc.content[i] = { ...node, content: (node.content ?? []).map((li) => ({ ...li, content: [withText(li.content?.[0] ?? { type: 'paragraph' }, '')] })) }
    }
  }
  return doc
}

export interface GuardedDeletePlan {
  protect: boolean
  doc?: DocJSON
}

/**
 * 英文分頁的跨區塊刪除（Backspace／Delete／剪下）：選取範圍涵蓋多個頂層節點、且其中有帶中文的區塊或圖片／圖集／影音時，
 * 不刪結構，只把「整個被選到」的格子清空英文文字（只選到一半的頭尾節點不動，寧可少刪不可多刪）。
 * 中文分頁維持預設行為（結構以中文為主）。
 */
export function planGuardedDelete(
  source: DocJSON,
  lang: NewsLang,
  blocks: PageBlockState[],
  fromIdx: number,
  toIdx: number,
  coveredIdx: number[],
): GuardedDeletePlan {
  if (lang !== 'en' || toIdx <= fromIdx) return { protect: false }
  const byKey = new Map(blocks.map((b) => [b.localKey, b]))
  let risky = false
  for (let i = fromIdx; i <= toIdx; i++) {
    const node = source.content[i]
    if (!node) continue
    if (ATOM_NODE_TYPES.includes(node.type)) risky = true
    else if (blockHasLangText(byKey.get(String(node.attrs?.bid ?? '')), 'zh')) risky = true
    if (risky) break
  }
  if (!risky) return { protect: false }
  return { protect: true, doc: clearTextInNodes(source, coveredIdx) }
}

// ───────────────────────────── 影音網址 ─────────────────────────────

const VIDEO_ID = /^[A-Za-z0-9_-]{1,64}$/

/** 解析 YouTube／Vimeo 網址；認不得回傳 null。影片代碼規則與 API 一致（英數、底線、連字號，1–64 字）。 */
export function parseVideoUrl(input: string): { provider: 'youtube' | 'vimeo'; videoId: string } | null {
  const raw = input.trim()
  if (!raw) return null
  let url: URL
  try {
    url = new URL(/^[a-z]+:\/\//i.test(raw) ? raw : `https://${raw}`)
  } catch {
    return null
  }
  const host = url.hostname.replace(/^www\.|^m\./, '')
  const parts = url.pathname.split('/').filter(Boolean)
  let id: string | undefined
  let provider: 'youtube' | 'vimeo' | null = null
  if (host === 'youtu.be') {
    provider = 'youtube'
    id = parts[0]
  } else if (host === 'youtube.com' || host === 'youtube-nocookie.com') {
    provider = 'youtube'
    if (parts[0] === 'watch') id = url.searchParams.get('v') ?? undefined
    else if (['embed', 'shorts', 'live', 'v'].includes(parts[0])) id = parts[1]
  } else if (host === 'vimeo.com' || host === 'player.vimeo.com') {
    provider = 'vimeo'
    id = parts[0] === 'video' ? parts[1] : parts.find((p) => /^\d+$/.test(p))
  }
  if (!provider || !id || !VIDEO_ID.test(id)) return null
  return { provider, videoId: id }
}

export function videoWatchUrl(c: Pick<VideoEmbedBlockContent, 'provider' | 'videoId'>): string {
  if (!c.videoId) return ''
  return c.provider === 'vimeo' ? `https://vimeo.com/${c.videoId}` : `https://www.youtube.com/watch?v=${c.videoId}`
}
