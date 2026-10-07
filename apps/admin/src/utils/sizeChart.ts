/**
 * 商品「尺寸對照表」的編輯狀態與後端 JSON 互轉。
 *
 * 前台（apps/web/shared/utils/shop.ts `parseSizeChart`）認得兩種形狀，後台編輯一律輸出第一種：
 *   ① `{ columns | headers: string[], rows: (string|number)[][], unit?, note?, caption? }`
 *   ② `{ rows: Record<欄名, 值>[] }` 或直接是物件陣列（欄名取第一列鍵的順序）
 * 讀進來兩種都轉成表格；認不得的舊資料回報 `recognized: false`，由畫面提示，不顯示原始內容。
 */
export interface SizeChartState {
  columns: string[]
  rows: string[][]
  unit: string
  note: string
  /** 前台認得但後台畫面不編輯的「表格標題」，原樣帶回去，避免儲存時被默默丟掉。 */
  caption: string
}

export function emptySizeChart(): SizeChartState {
  return { columns: [], rows: [], unit: '', note: '', caption: '' }
}

export function cloneSizeChart(s: SizeChartState): SizeChartState {
  return { columns: [...s.columns], rows: s.rows.map((r) => [...r]), unit: s.unit, note: s.note, caption: s.caption }
}

function cell(v: unknown): string {
  if (typeof v === 'number' || typeof v === 'string') return String(v)
  return ''
}

function text(v: unknown): string {
  return typeof v === 'string' ? v : ''
}

/** 把後端存的值轉成編輯狀態。空值視為「尚未建立」（recognized）；有值但看不懂回 `recognized: false`。 */
export function parseSizeChartForEdit(raw: unknown): { state: SizeChartState; recognized: boolean } {
  const unrecognized = { state: emptySizeChart(), recognized: false }
  if (raw === null || raw === undefined || raw === '') return { state: emptySizeChart(), recognized: true }
  let value: unknown = raw
  if (typeof value === 'string') {
    try {
      value = JSON.parse(value)
    } catch {
      return unrecognized
    }
    if (value === null) return { state: emptySizeChart(), recognized: true }
  }
  let rowsRaw: unknown
  let headers: string[] | null = null
  const state = emptySizeChart()
  if (Array.isArray(value)) {
    rowsRaw = value
  } else if (value && typeof value === 'object') {
    const o = value as Record<string, unknown>
    rowsRaw = o.rows
    const h = o.columns ?? o.headers
    if (Array.isArray(h)) headers = h.map(cell)
    state.unit = text(o.unit)
    state.note = text(o.note)
    state.caption = text(o.caption)
  } else {
    return unrecognized
  }
  if (!Array.isArray(rowsRaw)) {
    // 只有欄名沒有資料列：仍可編輯
    if (headers && rowsRaw === undefined) {
      state.columns = headers
      return { state, recognized: true }
    }
    return unrecognized
  }
  if (rowsRaw.length === 0) {
    state.columns = headers ?? []
    return { state, recognized: true }
  }
  if (rowsRaw.every((r) => Array.isArray(r))) {
    if (!headers) return unrecognized
    state.columns = headers
    state.rows = (rowsRaw as unknown[][]).map((r) => r.map(cell))
  } else if (rowsRaw.every((r) => r && typeof r === 'object' && !Array.isArray(r))) {
    const objs = rowsRaw as Record<string, unknown>[]
    const keys = headers ?? Object.keys(objs[0]!)
    state.columns = keys
    state.rows = objs.map((r) => keys.map((k) => cell(r[k])))
  } else {
    return unrecognized
  }
  // 某列比欄名多：補空欄名（儲存前會被驗證要求填寫）；比欄名少：補空白儲存格
  const width = Math.max(state.columns.length, ...state.rows.map((r) => r.length))
  while (state.columns.length < width) state.columns.push('')
  state.rows = state.rows.map((r) => (r.length < width ? [...r, ...Array<string>(width - r.length).fill('')] : r))
  return { state, recognized: true }
}

function rowBlank(r: string[]): boolean {
  return r.every((c) => !c.trim())
}

/** 整張表完全沒填（欄名、儲存格、單位、備註都空）。 */
export function sizeChartIsEmpty(s: SizeChartState): boolean {
  return s.columns.every((c) => !c.trim()) && s.rows.every(rowBlank) && !s.unit.trim() && !s.note.trim() && !s.caption.trim()
}

/** 驗證；回傳白話訊息，沒問題回 `null`。 */
export function validateSizeChart(s: SizeChartState): string | null {
  if (sizeChartIsEmpty(s)) return null
  if (s.columns.length === 0) return '請先新增欄位，並填入欄名（例如「尺寸」「胸圍」）'
  if (s.columns.some((c) => !c.trim())) return '每一欄都要填欄名，不需要的欄請刪除'
  if (new Set(s.columns.map((c) => c.trim())).size !== s.columns.length) return '欄名不可重複'
  if (s.rows.filter((r) => !rowBlank(r)).length === 0) return '請至少填入一列資料；不需要尺寸對照表就把整張表清空'
  return null
}

/** 送給後端的值；全空回 `null`（清除）。呼叫前應先通過 `validateSizeChart`。 */
export function sizeChartToPayload(s: SizeChartState): Record<string, unknown> | null {
  if (sizeChartIsEmpty(s)) return null
  const out: Record<string, unknown> = {
    columns: s.columns.map((c) => c.trim()),
    rows: s.rows.filter((r) => !rowBlank(r)).map((r) => r.map((c) => c.trim())),
  }
  if (s.unit.trim()) out.unit = s.unit.trim()
  if (s.note.trim()) out.note = s.note.trim()
  if (s.caption.trim()) out.caption = s.caption.trim()
  return out
}
