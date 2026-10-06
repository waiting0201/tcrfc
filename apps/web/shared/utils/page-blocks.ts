// shared/utils/page-blocks.ts — B1 頁面管理「區塊編輯器」公開內容的前台安全正規化（S3-5 順手：11.1 慈善理念）
//
// `GET /api/v1/{club}/pages/{slug}` 回 `blocks: [{ blockType, content, sortOrder }]`，`content` 的雙語欄位已由後端依請求語系
// 化簡成單一字串（`PageContentLocalizer`）。本檔只處理**純文字型**區塊，輸出成不含任何 HTML 的節點，由樣板用 `{{ }}` 渲染（**不得 v-html**）：
//   text（內文）／quote（引言）／stat_cards（數據卡）／steps（步驟條）／timeline（時間軸）／table（表格）／cta（行動呼籲按鈕）
// A-5（2026-10-06）補齊其餘五種：text_image（圖文左右）／gallery（圖片藝廊）／video_embed（影音嵌入，只認 YouTube／Vimeo，
// 輸出 nocookie 網域的嵌入網址，不接受任意 iframe 網址）／accordion_faq（手風琴 FAQ）／file_download（檔案下載）。
// 圖片欄位組是 `{ key, width, height, altZh, altEn }`（公開 API 不解析成網址）：有 `url`（https 或站內路徑）就用，否則以
// `mediaBaseUrl + '/' + key` 組出（Azure Blob `images` 容器的公開網址，同 `siteImage.ts` 的基底）；兩者都沒有就略過該圖。
// 認不得的型別、形狀不對的區塊一律略過；CTA 連結只接受站內路徑（`/` 開頭、非 `//`）或 `https://`。
export type PageBlockNode =
  | { kind: 'text', paragraphs: string[] }
  | { kind: 'quote', text: string, attribution: string | null }
  | { kind: 'stats', items: Array<{ value: string, label: string }> }
  | { kind: 'steps', items: Array<{ title: string, description: string | null }> }
  | { kind: 'timeline', items: Array<{ date: string, title: string, description: string | null }> }
  | { kind: 'table', headers: string[], rows: string[][] }
  | { kind: 'cta', text: string, label: string, href: string }
  | { kind: 'textImage', paragraphs: string[], imagePosition: 'left' | 'right', image: PageBlockImage | null }
  | { kind: 'gallery', images: PageBlockImage[] }
  | { kind: 'video', provider: 'youtube' | 'vimeo', src: string, caption: string | null }
  | { kind: 'faq', items: Array<{ question: string, answer: string }> }
  | { kind: 'file', label: string, href: string }

export interface PageBlockImage {
  src: string
  alt: string
  width: number | null
  height: number | null
}

export interface NormalizeBlocksOptions {
  /** 內容語系；內容裡殘留的 `{zh,en}` 雙語物件依此化簡（en 空白回退 zh）。預設 zh。 */
  locale?: 'zh' | 'en'
  /** 圖片 key → 網址的基底（`runtimeConfig.public.mediaBaseUrl`）。未提供時只認區塊自帶的 `url`。 */
  mediaBaseUrl?: string | null
}

export interface RawPageBlock {
  blockType?: unknown
  /** 後台課程內容等處可能以 `type` 命名；`blockType` 優先。 */
  type?: unknown
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


/** 後端 `PageContentLocalizer` 的前台版：`{zh, en?}` 雙語物件（只有這兩個鍵、值為字串或 null）化簡為字串，其餘遞迴。 */
export function localizeBlockContent(node: unknown, locale: 'zh' | 'en'): unknown {
  if (Array.isArray(node)) return node.map(n => localizeBlockContent(n, locale))
  const o = obj(node)
  if (!o) return node
  const keys = Object.keys(o)
  if ('zh' in o && keys.every(k => k === 'zh' || k === 'en') && keys.every(k => o[k] === null || typeof o[k] === 'string')) {
    const zh = typeof o.zh === 'string' ? o.zh : ''
    const en = typeof o.en === 'string' ? o.en : ''
    return locale === 'en' && en.trim() ? en : zh
  }
  const out: Record<string, unknown> = {}
  for (const k of keys) out[k] = localizeBlockContent(o[k], locale)
  return out
}

/**
 * 把「字串型 JSON」解析成區塊清單：支援 `[{blockType,content,sortOrder}, …]` 與 `{ blocks: [...] }` 兩種外形。
 * 不是 JSON（含以 `[` 開頭的純文字，如「[快訊] …」）、或解析後不是上述外形 → 回 `null`，呼叫端當純文字處理（向下相容）。
 */
export function parseBlocksJson(text: string | null | undefined): RawPageBlock[] | null {
  const t = (text ?? '').trim()
  if (!t || (t[0] !== '[' && t[0] !== '{')) return null
  let parsed: unknown
  try { parsed = JSON.parse(t) }
  catch { return null }
  const list = Array.isArray(parsed) ? parsed : (obj(parsed) && Array.isArray(obj(parsed)!.blocks) ? (obj(parsed)!.blocks as unknown[]) : null)
  if (!list) return null
  const blocks = list.map(obj).filter((o): o is Record<string, unknown> => o !== null && (typeof o.blockType === 'string' || typeof o.type === 'string'))
  // 外形對、但沒有任何「看起來像區塊」的元素（例如週期時段表 JSON）→ 不是區塊內容
  return blocks.length ? (blocks as RawPageBlock[]) : null
}

function num(v: unknown): number | null {
  return typeof v === 'number' && Number.isFinite(v) && v > 0 ? v : null
}

/** 圖片欄位組 → 可顯示的圖。`url`（https 或站內路徑）優先，其次 `mediaBaseUrl/key`（key 不得含 `..`、不得為絕對網址）。 */
function imageOf(v: unknown, locale: 'zh' | 'en', mediaBaseUrl: string | null | undefined): PageBlockImage | null {
  const o = obj(v)
  if (!o) return null
  let src = safeBlockHref(o.url)
  if (!src) {
    const key = str(o.key)
    const base = (mediaBaseUrl ?? '').trim().replace(/\/+$/, '')
    if (key && base && /^https?:\/\//i.test(base) && !/^[a-z][a-z0-9+.-]*:/i.test(key) && !key.startsWith('/') && !key.includes('..')) {
      src = `${base}/${key}`
    }
  }
  if (!src) return null
  const alt = locale === 'en' ? (str(o.altEn) || str(o.altZh)) : (str(o.altZh) || str(o.altEn))
  return { src, alt: alt || str(o.alt), width: num(o.width), height: num(o.height) }
}

/** 影音嵌入：只認 YouTube／Vimeo，輸出 nocookie／dnt 網域；代碼格式不合一律拒絕（不接受任意網址）。 */
export function videoEmbedSrc(provider: unknown, videoId: unknown): string | null {
  const id = str(videoId)
  if (provider === 'youtube' && /^[A-Za-z0-9_-]{6,32}$/.test(id)) return `https://www.youtube-nocookie.com/embed/${id}`
  if (provider === 'vimeo' && /^\d{4,15}$/.test(id)) return `https://player.vimeo.com/video/${id}?dnt=1`
  return null
}

/** 檔案連結：站內路徑或 https；其餘「看起來是物件鍵」的值（無協定、無 `..`）在有 mediaBaseUrl 時接到其後。 */
function fileHref(v: unknown, mediaBaseUrl: string | null | undefined): string | null {
  const safe = safeBlockHref(v)
  if (safe) return safe
  const key = str(v)
  const base = (mediaBaseUrl ?? '').trim().replace(/\/+$/, '')
  if (key && base && /^https?:\/\//i.test(base) && !/^[a-z][a-z0-9+.-]*:/i.test(key) && !key.startsWith('/') && !key.includes('..') && !/\s/.test(key)) return `${base}/${key}`
  return null
}

/**
 * 區塊 `text.body` 若夾帶簡單 HTML（種子頁與富文本貼上常見的 `<h2>…</h2><p>…</p>`），前台不 v-html，
 * 也不可把標籤原樣印出：區塊結尾標籤換成段落分隔、其餘標籤去掉、解開基本實體。純文字原樣通過。
 */
export function plainTextOf(body: string): string {
  if (!/<\/?(p|h[1-6]|ul|ol|li|br|strong|em|b|i|a|div|span|blockquote)\b[^>]*>/i.test(body)) return body
  return body
    .replace(/<\s*br\s*\/?>/gi, '\n')
    .replace(/<\/(p|h[1-6]|ul|ol|li|div|blockquote)\s*>/gi, '\n\n')
    .replace(/<[^>]*>/g, '')
    .replace(/&nbsp;/g, ' ').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&')
    .replace(/\n{3,}/g, '\n\n')
    .trim()
}

export function normalizePageBlocks(blocks: readonly RawPageBlock[] | null | undefined, options: NormalizeBlocksOptions = {}): PageBlockNode[] {
  if (!blocks) return []
  const locale = options.locale ?? 'zh'
  const mediaBase = options.mediaBaseUrl
  const sorted = [...blocks].sort((a, b) => Number(a.sortOrder ?? 0) - Number(b.sortOrder ?? 0))
  const out: PageBlockNode[] = []
  for (const b of sorted) {
    const c = obj(localizeBlockContent(b.content, locale))
    if (!c) continue
    switch (b.blockType ?? b.type) {
      case 'text': {
        const paragraphs = plainTextOf(str(c.body)).split(/\r?\n\s*\r?\n/).map(p => p.trim()).filter(Boolean)
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
      case 'text_image': {
        const paragraphs = str(c.body).split(/\r?\n\s*\r?\n/).map(p => p.trim()).filter(Boolean)
        const image = imageOf(c.image, locale, mediaBase)
        if (paragraphs.length || image) out.push({ kind: 'textImage', paragraphs, imagePosition: c.imagePosition === 'right' ? 'right' : 'left', image })
        break
      }
      case 'gallery': {
        const images = (Array.isArray(c.images) ? c.images : []).map(i => imageOf(i, locale, mediaBase)).filter((i): i is PageBlockImage => i !== null)
        if (images.length) out.push({ kind: 'gallery', images })
        break
      }
      case 'video_embed': {
        const src = videoEmbedSrc(c.provider, c.videoId)
        if (src) out.push({ kind: 'video', provider: c.provider === 'vimeo' ? 'vimeo' : 'youtube', src, caption: strOrNull(c.caption) })
        break
      }
      case 'accordion_faq': {
        const list = items(c.items).map(i => ({ question: str(i.question), answer: str(i.answer) })).filter(i => i.question && i.answer)
        if (list.length) out.push({ kind: 'faq', items: list })
        break
      }
      case 'file_download': {
        const href = fileHref(c.fileUrl, mediaBase)
        const label = str(c.label)
        if (href && label) out.push({ kind: 'file', label, href })
        break
      }
    }
  }
  return out
}
