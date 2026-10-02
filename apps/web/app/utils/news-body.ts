// app/utils/news-body.ts — 新聞內文（ArticleDetailDto.bodyJson）→ 可安全渲染的區塊清單
//
// 後端契約（apps/api/README.md「新聞內文 articles_i18n.body」、`ArticleBodyJsonColumnTests`）：
//   - 後台目前送純文字；後端存成 {"text":"…"}，公開單篇 `bodyJson` 讀回「還原後的原文字」
//     （所以前台拿到的通常就是一段純文字，段落以空行分隔、段內換行是單一 \n）。
//   - 物件／陣列原樣保存、原樣回傳（為區塊編輯器保留）；後台尚無區塊編輯器，這裡只認
//     {"blocks":[…]}／[…] 這兩種外形與下列區塊型別，其餘一律忽略（不猜測、不發明格式）。
//
// 🔴 XSS：本檔只輸出「資料」，不輸出 HTML。元件一律用 Vue 文字插值渲染（自動跳脫），
// 禁止 v-html。唯一會變成屬性值的是圖片網址，必須通過 safeUrl()（只放行 https:／http:／
// 站內相對路徑 `/`，擋掉 javascript:／data:／protocol-relative `//` 等）。

export type NewsBodyBlock =
  | { kind: 'p', lines: string[] }
  | { kind: 'heading', level: 2 | 3, text: string }
  | { kind: 'quote', text: string, cite: string | null }
  | { kind: 'list', ordered: boolean, items: string[] }
  | { kind: 'image', src: string, alt: string, caption: string | null }
  | { kind: 'gallery', images: { src: string, alt: string, caption: string | null }[] }

/** 只放行 http(s) 絕對網址與站內相對路徑；其餘（javascript:／data:／vbscript:／`//host`）回 null。 */
export function safeUrl(raw: unknown): string | null {
  if (typeof raw !== 'string') return null
  const v = raw.trim()
  if (!v) return null
  if (/^https?:\/\//i.test(v)) return v
  if (v.startsWith('/') && !v.startsWith('//') && !v.startsWith('/\\')) return v
  return null
}

function str(v: unknown): string {
  return typeof v === 'string' ? v : ''
}

/** 純文字 → 段落：空行分段，段內單一換行保留為斷行。 */
export function plainTextToBlocks(text: string): NewsBodyBlock[] {
  return text
    .replace(/\r\n?/g, '\n')
    .split(/\n{2,}/)
    .map((para) => para.split('\n').map((l) => l.trimEnd()))
    .filter((lines) => lines.some((l) => l.trim() !== ''))
    .map((lines) => ({ kind: 'p', lines }) as NewsBodyBlock)
}

function imageOf(o: Record<string, unknown>): { src: string, alt: string, caption: string | null } | null {
  const src = safeUrl(o.src ?? o.url)
  if (!src) return null
  const caption = str(o.caption).trim()
  return { src, alt: str(o.alt).trim() || caption, caption: caption || null }
}

function blockOf(raw: unknown): NewsBodyBlock[] {
  if (typeof raw === 'string') return plainTextToBlocks(raw)
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return []
  const o = raw as Record<string, unknown>
  const type = str(o.type).toLowerCase()
  const text = str(o.text)
  switch (type) {
    case 'p':
    case 'paragraph':
    case 'text':
      return plainTextToBlocks(text)
    case 'h1':
    case 'h2':
    case 'heading':
      return text.trim() ? [{ kind: 'heading', level: 2, text: text.trim() }] : []
    case 'h3':
    case 'h4':
    case 'subheading':
      return text.trim() ? [{ kind: 'heading', level: 3, text: text.trim() }] : []
    case 'quote':
    case 'blockquote':
      return text.trim() ? [{ kind: 'quote', text: text.trim(), cite: str(o.cite ?? o.source).trim() || null }] : []
    case 'list':
    case 'ul':
    case 'ol': {
      const items = (Array.isArray(o.items) ? o.items : []).map((i) => str(i).trim()).filter(Boolean)
      return items.length ? [{ kind: 'list', ordered: type === 'ol' || o.ordered === true, items }] : []
    }
    case 'image':
    case 'img': {
      const img = imageOf(o)
      return img ? [{ kind: 'image', ...img }] : []
    }
    case 'gallery': {
      const images = (Array.isArray(o.images) ? o.images : [])
        .map((i) => (i && typeof i === 'object' ? imageOf(i as Record<string, unknown>) : null))
        .filter((i): i is NonNullable<typeof i> => i !== null)
      return images.length ? [{ kind: 'gallery', images }] : []
    }
    default:
      // 沒有 type 但有 text（例如 {"text":"…"} 原樣出現）→ 當純文字；其他未知區塊忽略。
      return !type && text ? plainTextToBlocks(text) : []
  }
}

/**
 * bodyJson（字串或 null）→ 區塊清單。
 * - null／空白 → []（呼叫端不顯示內文區塊）
 * - 不是以 `{`／`[` 開頭，或 JSON.parse 失敗 → 整段當純文字（後台目前的主要情況）
 * - 物件：認 `blocks`／`content` 陣列，或單一區塊／{text}；陣列：逐項當區塊
 */
export function parseNewsBody(bodyJson: string | null | undefined): NewsBodyBlock[] {
  if (bodyJson == null) return []
  const trimmed = bodyJson.trim()
  if (!trimmed) return []
  if (trimmed[0] !== '{' && trimmed[0] !== '[') return plainTextToBlocks(bodyJson)
  let parsed: unknown
  try {
    parsed = JSON.parse(trimmed)
  } catch {
    return plainTextToBlocks(bodyJson)
  }
  if (Array.isArray(parsed)) return parsed.flatMap(blockOf)
  if (parsed && typeof parsed === 'object') {
    const o = parsed as Record<string, unknown>
    const list = Array.isArray(o.blocks) ? o.blocks : Array.isArray(o.content) ? o.content : null
    if (list) return list.flatMap(blockOf)
    return blockOf(o)
  }
  return []
}
