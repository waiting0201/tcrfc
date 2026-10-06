// app/utils/report-view.ts — 瀏覽數回報（A-8）：新聞單篇／FAQ 展開共用。
//
// 只在瀏覽器端呼叫；同一個工作階段（sessionStorage）每個 key 最多送一次，重新整理或反覆展開不重複計數。
// 失敗一律靜默（瀏覽數是加分數據，不得影響閱讀）。後端對應
// `POST /api/v1/{club}/news/{slug}/views`、`POST /api/v1/{club}/faqs/{slug}/views`（固定 204）。
// BFF 白名單見 server/api/backend/[...path].ts 的 VIEW_COUNT_PATH。
export function reportView(club: string, kind: 'news' | 'faqs', slug: string): void {
  if (!import.meta.client || !slug) return
  const key = `tcrfc:view:${club}:${kind}:${slug}`
  try {
    if (sessionStorage.getItem(key)) return
    sessionStorage.setItem(key, '1')
  }
  catch {
    // sessionStorage 不可用（隱私模式等）：照送一次，寧可偶爾多算也不要讓功能壞掉
  }
  $fetch(`/api/backend/${club}/${kind}/${encodeURIComponent(slug)}/views`, { method: 'POST', body: {} }).catch(() => {})
}
