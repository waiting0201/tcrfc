// app/composables/useFaqCategories.ts — 12 FAQ 主題分類（S1-18）
//
// 對應 apps/api `GET /api/v1/faq-categories?lang=zh|en`（Features/Faqs/FaqsEndpoints.cs／
// FaqsRepository.ListCategoriesAsync）。全站共用、不分俱樂部（faq_categories 沒有
// club_id，同 article_categories 的共用主檔設計）。後端只回 is_enabled=1 的分類，
// 前台不需要再自己判斷啟用狀態。
//
// 🔴 Fail-open 但不是「臆造十個固定主題」：規劃書 3.12 雖然列出十個起始主題名稱，
// db/seed 也確實種了這十筆，但「這十個主題現在叫什麼名字、還在不在」的真實來源永遠是
// 後台——API 失敗或回傳空陣列時 categories 為空陣列，呼叫端據此隱藏主題導覽卡，
// 不要在前台另外寫死一份十筆清單當備援（那就是規格禁止的「臆造」）。
export interface FaqCategory {
  id: string
  slug: string
  sortOrder: number
  name: string | null
}

export function useFaqCategories(lang: string) {
  const { data } = useFetch<FaqCategory[]>('/api/backend/faq-categories', {
    query: { lang },
    key: `faq-categories-${lang}`,
  })
  const categories = computed(() => data.value ?? [])
  return { categories }
}
