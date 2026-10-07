/**
 * 慈善計畫「緣起與內容」的讀寫轉換。
 *
 * 後端要求這個欄位是合法 JSON；前台（apps/web/shared/utils/content-blocks.ts `parseContentBlocks`）認得：
 * 純字串（`JSON.stringify("…")`）、`[{ type, text|items }]`、`{ blocks: [...] }`。
 * 後台畫面以「純文字（空行分段）」編輯，寫回時存成 JSON 字串。
 * 注意：前台**不**渲染 `[{ blockType, content }]`（頁面區塊編輯器的格式），所以這個欄位不能改用區塊編輯器。
 *
 * 舊資料若是區塊格式：讀成純文字顯示（標題、清單項目各成一段）；使用者沒改文字就原樣送回，
 * 改了才改存純文字——兩種情況都不會把修改默默丟掉，也不會顯示原始 JSON。
 */
export interface ContentField {
  /** 文字框顯示與編輯的純文字。 */
  text: string
  /** 後端原值（只用來「沒改就原樣送回」）。 */
  raw: string | null
  /** 原值是區塊格式（標題、清單等排版，改存純文字會消失）。 */
  structured: boolean
  /** 載入時轉出的文字，用來判斷使用者有沒有改。 */
  initialText: string
}

function str(v: unknown): string {
  return typeof v === 'string' ? v.trim() : ''
}

function blockToText(b: unknown, lang: 'zh' | 'en'): string {
  if (!b || typeof b !== 'object') return ''
  const o = b as Record<string, unknown>
  if (Array.isArray(o.items)) return o.items.map(str).filter(Boolean).join('\n')
  if (typeof o.text === 'string') return o.text.trim()
  // 頁面區塊格式 { blockType: 'text', content: { body: { zh, en } } }
  const body = (o.content as { body?: unknown } | undefined)?.body
  if (typeof body === 'string') return body.trim()
  if (body && typeof body === 'object') {
    const bo = body as Record<string, unknown>
    return str(bo[lang]) || str(bo[lang === 'zh' ? 'en' : 'zh'])
  }
  return ''
}

export function decodeCharityContent(raw: string | null | undefined, lang: 'zh' | 'en'): ContentField {
  const empty: ContentField = { text: '', raw: raw ?? null, structured: false, initialText: '' }
  if (!raw || !raw.trim()) return { ...empty, raw: null }
  let parsed: unknown
  try {
    parsed = JSON.parse(raw)
  } catch {
    return { text: raw, raw, structured: false, initialText: raw }
  }
  if (typeof parsed === 'string') return { text: parsed, raw, structured: false, initialText: parsed }
  const list = Array.isArray(parsed) ? parsed : Array.isArray((parsed as { blocks?: unknown })?.blocks) ? (parsed as { blocks: unknown[] }).blocks : null
  const text = list ? list.map((b) => blockToText(b, lang)).filter(Boolean).join('\n\n') : ''
  return { text, raw, structured: true, initialText: text }
}

/** 送給後端的值：沒改文字就原樣送回；改了存成 JSON 字串；清空存 null。 */
export function encodeCharityContent(f: ContentField): string | null {
  if (f.structured && f.text === f.initialText) return f.raw
  const t = f.text.trim()
  return t ? JSON.stringify(t) : null
}
