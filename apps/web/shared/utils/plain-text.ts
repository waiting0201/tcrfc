// shared/utils/plain-text.ts — 純文字顯示與搜尋高亮的小工具（H 批，2026-10-02）。
//
// 🔴 這兩個函式的存在理由是「不得 v-html」：後台政策頁（Cookie／隱私權／會員條款）與全站搜尋的
// 標題、摘錄都是**純文字**，前台一律切成文字片段、由樣板用文字節點輸出（`{{ }}`，Vue 會自動跳脫），
// 高亮用 `<mark>` 包「文字片段」而不是把標記字串塞回 HTML。即使內容含 `<script>` 或 `<b>`，
// 也只會被當成一般字元顯示出來。

/** 純文字依空白行分段；段內的單一換行保留（樣板以 `white-space: pre-line` 呈現）。 */
export function splitParagraphs(text: string | null | undefined): string[] {
  if (!text) return []
  return text
    .replace(/\r\n?/g, '\n')
    .split(/\n[ \t　]*\n+/)
    .map((p) => p.trim())
    .filter((p) => p.length > 0)
}

export interface HighlightSegment {
  text: string
  hit: boolean
}

/**
 * 依搜尋回傳的 `tokens` 把文字切成「命中／未命中」片段。比對不分大小寫，也容忍全形字元
 * （伺服器端的 tokens 已正規化成半形小寫）。找不到任何命中時回傳單一未命中片段。
 */
export function highlightSegments(text: string | null | undefined, tokens: readonly string[]): HighlightSegment[] {
  const source = text ?? ''
  if (!source) return []
  const needles = tokens.map((t) => t.trim().toLowerCase()).filter((t) => t.length > 0)
  if (needles.length === 0) return [{ text: source, hit: false }]

  // NFKC 把全形英數轉半形，但某些字元會改變長度；長度不一致時退回單純小寫，避免索引錯位。
  const folded = source.normalize('NFKC').toLowerCase()
  const haystack = folded.length === source.length ? folded : source.toLowerCase()
  if (haystack.length !== source.length) return [{ text: source, hit: false }]

  const ranges: Array<[number, number]> = []
  for (const needle of needles) {
    let from = 0
    while (from <= haystack.length - needle.length) {
      const at = haystack.indexOf(needle, from)
      if (at < 0) break
      ranges.push([at, at + needle.length])
      from = at + needle.length
    }
  }
  if (ranges.length === 0) return [{ text: source, hit: false }]

  ranges.sort((a, b) => a[0] - b[0])
  const merged: Array<[number, number]> = []
  for (const r of ranges) {
    const last = merged[merged.length - 1]
    if (last && r[0] <= last[1]) last[1] = Math.max(last[1], r[1])
    else merged.push([r[0], r[1]])
  }

  const out: HighlightSegment[] = []
  let cursor = 0
  for (const [start, end] of merged) {
    if (start > cursor) out.push({ text: source.slice(cursor, start), hit: false })
    out.push({ text: source.slice(start, end), hit: true })
    cursor = end
  }
  if (cursor < source.length) out.push({ text: source.slice(cursor), hit: false })
  return out
}
