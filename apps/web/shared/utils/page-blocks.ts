// shared/utils/page-blocks.ts — B1 頁面管理「區塊編輯器」公開內容的前台安全正規化（S3-5 順手：11.1 慈善理念）
//
// `GET /api/v1/{club}/pages/{slug}` 回 `blocks: [{ blockType, content, sortOrder }]`，`content` 的雙語欄位已由後端依請求語系
// 化簡成單一字串（`PageContentLocalizer`）。本檔只處理**純文字型**區塊，輸出成不含任何 HTML 的節點，由樣板用 `{{ }}` 渲染（**不得 v-html**）：
//   text（內文）／quote（引言）／stat_cards（數據卡）／steps（步驟條）／timeline（時間軸）／table（表格）／cta（行動呼籲按鈕）
// 圖文左右、圖片藝廊、影音嵌入、手風琴 FAQ、檔案下載需要圖片網址解析、嵌入白名單等額外處理，本輪不渲染（略過，不輸出半成品）。
// 認不得的型別、形狀不對的區塊一律略過；CTA 連結只接受站內路徑（`/` 開頭、非 `//`）或 `https://`。
export type PageBlockNode =
  | { kind: 'text', paragraphs: string[] }
  | { kind: 'quote', text: string, attribution: string | null }
  | { kind: 'stats', items: Array<{ value: string, label: string }> }
  | { kind: 'steps', items: Array<{ title: string, description: string | null }> }
  | { kind: 'timeline', items: Array<{ date: string, title: string, description: string | null }> }
  | { kind: 'table', headers: string[], rows: string[][] }
  | { kind: 'cta', text: string, label: string, href: string }

export interface RawPageBlock {
  blockType?: unknown
  content?: unknown
  sortOrder?: unknown
}

function str(v: unknown): string {
  return typeof v === 'string' ? v.trim() : ''
}

function strOrNull(v: unknown): string | null {
  const s = str(v)
  return s || null
}

function obj(v: unknown): Record<string, unknown> | null {
  return v && typeof v === 'object' && !Array.isArray(v) ? (v as Record<string, unknown>) : null
}

function items(v: unknown): Record<string, unknown>[] {
  return Array.isArray(v) ? v.map(obj).filter((o): o is Record<string, unknown> => o !== null) : []
}

/** 站內路徑或 https 連結才算安全；`javascript:`、`data:`、`//host` 一律拒絕。 */
export function safeBlockHref(raw: unknown): string | null {
  const s = str(raw)
  if (!s) return null
  if (/^\/(?!\/)[^\s]*$/.test(s)) return s
  if (/^https:\/\/[^\s]+$/i.test(s)) return s
  return null
}

export function normalizePageBlocks(blocks: readonly RawPageBlock[] | null | undefined): PageBlockNode[] {
  if (!blocks) return []
  const sorted = [...blocks].sort((a, b) => Number(a.sortOrder ?? 0) - Number(b.sortOrder ?? 0))
  const out: PageBlockNode[] = []
  for (const b of sorted) {
    const c = obj(b.content)
    if (!c) continue
    switch (b.blockType) {
      case 'text': {
        const paragraphs = str(c.body).split(/\r?\n\s*\r?\n/).map(p => p.trim()).filter(Boolean)
        if (paragraphs.length) out.push({ kind: 'text', paragraphs })
        break
      }
      case 'quote': {
        const text = str(c.text)
        if (text) out.push({ kind: 'quote', text, attribution: strOrNull(c.attribution) })
        break
      }
      case 'stat_cards': {
        const list = items(c.items).map(i => ({ value: str(i.value), label: str(i.label) })).filter(i => i.value && i.label)
        if (list.length) out.push({ kind: 'stats', items: list })
        break
      }
      case 'steps': {
        const list = items(c.items).map(i => ({ title: str(i.title), description: strOrNull(i.description) })).filter(i => i.title)
        if (list.length) out.push({ kind: 'steps', items: list })
        break
      }
      case 'timeline': {
        const list = items(c.items).map(i => ({ date: str(i.date), title: str(i.title), description: strOrNull(i.description) })).filter(i => i.date && i.title)
        if (list.length) out.push({ kind: 'timeline', items: list })
        break
      }
      case 'table': {
        const headers = Array.isArray(c.headers) ? c.headers.map(str) : []
        const rows = Array.isArray(c.rows) ? c.rows.map(r => (Array.isArray(r) ? r.map(cell => (typeof cell === 'string' || typeof cell === 'number' ? String(cell) : '')) : [])) : []
        if (headers.length && rows.length) out.push({ kind: 'table', headers, rows })
        break
      }
      case 'cta': {
        const href = safeBlockHref(c.buttonUrl)
        const label = str(c.buttonLabel)
        if (href && label) out.push({ kind: 'cta', text: str(c.text), label, href })
        break
      }
    }
  }
  return out
}
