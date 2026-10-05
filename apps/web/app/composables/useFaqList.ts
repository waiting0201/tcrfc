// app/composables/useFaqList.ts — 12 FAQ 常見問題列表（S1-18）
//
// 對應 apps/api `GET /api/v1/{club}/faqs?category=&keyword=&lang=&page=&pageSize=`
// （Features/Faqs/FaqsEndpoints.cs／FaqsRepository.ListAsync）。FAQ 首頁一次抓全站
// 已發布題目（pageSize 200，比照 news／schedule 既有「單頁全載」慣例：83 篇新聞、
// 21 場賽事都是這樣一次抓完，FAQ 題數量級只會更少），前端依 categorySlugs 分組
// 陳列成十個主題區塊；分類頁改帶 category 篩選單一分類。
//
// 🔴 Fail-open 但不是「全部顯示」：同 useFaqEmbed.ts 檔頭說明——API 失敗或回傳空清單
// 時 faqs 為空陣列，呼叫端據此顯示既有「收錄中」空狀態，不臆造題目內容。
export interface FaqItem {
  id: string
  slug: string
  isShared: boolean
  sortOrder: number
  question: string | null
  answer: string | null
  categorySlugs: string[]
}

interface FaqListResponse {
  items: FaqItem[]
  page: number
  pageSize: number
  totalCount: number
}

export function useFaqList(club: string, lang: string, category?: string) {
  const { data } = useFetch<FaqListResponse>(`/api/backend/${club}/faqs`, {
    query: { lang, pageSize: 200, category },
    key: `faq-list-${club}-${category ?? 'all'}-${lang}`,
    // F2（2026-10-03）：所屬分類「全部」對本站已關閉的題目（例如藍鯨的學院招生）不進 payload，
    // 理由同 useFaqCategories。沒有分類標記的題目照舊保留。
    transform: (res) => ({
      ...res,
      items: res.items.filter((f) => f.categorySlugs.length === 0 || f.categorySlugs.some((s) => isFaqCategoryEnabledForClub(s, club))),
    }),
  })
  const faqs = computed(() => data.value?.items ?? [])
  const totalCount = computed(() => data.value?.totalCount ?? 0)
  return { faqs, totalCount }
}
