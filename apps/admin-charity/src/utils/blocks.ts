/**
 * 項目說明內文的區塊格式（後端原樣存取、前台 `BlockContent.vue` 渲染）：
 * `{ "blocks": [ { "type": "paragraph|heading|quote", "text": "…" }, { "type": "list", "items": ["…"] } ] }`。
 * 前台另外容忍純字串。這裡的編輯器只認這四種版型；遇到不認得的版型時**原樣保留**（不改 type、不丟掉多餘欄位），
 * 避免一次儲存就把別人放進去的內容洗掉。
 */
export type BlockType = 'paragraph' | 'heading' | 'quote' | 'list'

export interface EditorBlock {
  /** 編輯器內部用的穩定鍵，不會送給後端。 */
  uid: number
  type: string
  text: string
  /** 清單項目，一行一項。 */
  itemsText: string
  /** 版型以外原本就有的欄位，原樣帶回去。 */
  extra: Record<string, unknown>
}

export const BLOCK_TYPE_LABELS: Record<BlockType, string> = {
  paragraph: '段落',
  heading: '小標題',
  quote: '引言',
  list: '清單',
}

export const KNOWN_BLOCK_TYPES = Object.keys(BLOCK_TYPE_LABELS) as BlockType[]

let uidSeed = 1
export const nextUid = () => uidSeed++

function str(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

export function parseBlocks(raw: unknown): EditorBlock[] {
  if (typeof raw === 'string') {
    return raw.trim() ? [{ uid: nextUid(), type: 'paragraph', text: raw, itemsText: '', extra: {} }] : []
  }
  const list = (raw as { blocks?: unknown } | null)?.blocks
  if (!Array.isArray(list)) return []
  const result: EditorBlock[] = []
  for (const entry of list as Array<Record<string, unknown>>) {
    if (entry === null || typeof entry !== 'object') continue
    const { type, text, items, ...extra } = entry
    result.push({
      uid: nextUid(),
      type: str(type) || 'paragraph',
      text: str(text),
      itemsText: Array.isArray(items) ? (items as unknown[]).map(str).join('\n') : '',
      extra,
    })
  }
  return result
}

export function newBlock(type: BlockType = 'paragraph'): EditorBlock {
  return { uid: nextUid(), type, text: '', itemsText: '', extra: {} }
}

/** 轉回要送給後端的形狀；沒有任何有內容的區塊時回傳空物件（後端以空物件代表「清空」）。 */
export function serializeBlocks(blocks: EditorBlock[]): Record<string, unknown> {
  const out: Record<string, unknown>[] = []
  for (const b of blocks) {
    if (b.type === 'list') {
      const items = b.itemsText.split('\n').map((s) => s.trim()).filter(Boolean)
      if (items.length > 0) out.push({ ...b.extra, type: 'list', items })
    } else if (b.text.trim()) {
      out.push({ ...b.extra, type: b.type, text: b.text.trim() })
    }
  }
  return out.length > 0 ? { blocks: out } : {}
}

/** 比對用的標準化字串：原始內容與編輯後內容經過同一條路徑再比較，才不會被空白或欄位順序騙到。 */
export function blocksKey(blocks: EditorBlock[]): string {
  return JSON.stringify(serializeBlocks(blocks))
}
