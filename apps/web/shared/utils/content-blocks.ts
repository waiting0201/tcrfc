// shared/utils/content-blocks.ts — 後台「區塊編輯器」JSON 字串的前台安全渲染（S2-9 慈善計畫詳情用）
//
// `GET /api/v1/{club}/charity/programs/{slug}` 的 `content`（計畫緣起與內容）是「區塊編輯器整段 JSON
// 字串」（apps/api/README.md「B5」節）。目前後台畫面（apps/admin CharityProgramEditView）實際寫入的是
// **JSON 字串化的純文字**（`JSON.stringify("…")`），日後若換成結構化區塊會是
// `{"blocks":[{"type":"paragraph","text":"…"}]}`（K5 抽獎公布稿的既有形狀，apps/api/README.md）。
//
// 🔴 一律輸出成「純文字節點陣列」，由樣板用 `{{ }}` 渲染，**不得 v-html**——內容是後台人員輸入的
// 自由文字，前台對它沒有信任基礎。認不得的區塊型別整個略過（不猜、不原樣輸出 JSON）。
export type ContentBlock =
  | { kind: 'p', text: string }
  | { kind: 'h', text: string }
  | { kind: 'ul', items: string[] }

function textToParagraphs(text: string): ContentBlock[] {
  return text
    .split(/\r?\n\s*\r?\n/)
    .map((t) => t.trim())
    .filter(Boolean)
    .map((t) => ({ kind: 'p' as const, text: t }))
}

function fromBlockArray(blocks: unknown[]): ContentBlock[] {
  const out: ContentBlock[] = []
  for (const b of blocks) {
    if (!b || typeof b !== 'object') continue
    const block = b as { type?: unknown, text?: unknown, items?: unknown }
    const type = typeof block.type === 'string' ? block.type : ''
    if ((type === 'paragraph' || type === 'text' || type === 'quote') && typeof block.text === 'string') {
      out.push(...textToParagraphs(block.text))
    }
    else if (type === 'heading' && typeof block.text === 'string' && block.text.trim()) {
      out.push({ kind: 'h', text: block.text.trim() })
    }
    else if ((type === 'list' || type === 'bullets') && Array.isArray(block.items)) {
      const items = block.items.filter((i): i is string => typeof i === 'string' && i.trim() !== '').map((i) => i.trim())
      if (items.length) out.push({ kind: 'ul', items })
    }
  }
  return out
}

export function parseContentBlocks(raw: string | null | undefined): ContentBlock[] {
  if (!raw || !raw.trim()) return []
  let parsed: unknown
  try {
    parsed = JSON.parse(raw)
  }
  catch {
    // 不是 JSON（後端只驗證合法 JSON，理論上不會發生）：當純文字處理。
    return textToParagraphs(raw)
  }
  if (typeof parsed === 'string') return textToParagraphs(parsed)
  if (Array.isArray(parsed)) return fromBlockArray(parsed)
  if (parsed && typeof parsed === 'object' && Array.isArray((parsed as { blocks?: unknown }).blocks)) {
    return fromBlockArray((parsed as { blocks: unknown[] }).blocks)
  }
  return []
}
