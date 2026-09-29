// app/composables/useFaqEmbed.ts — G-12 常見問題快捷區塊（S1-15 新增）
//
// 對應 apps/api `GET /api/v1/{club}/faqs/embeds/{slotCode}`（Features/Faqs/FaqsEndpoints.cs），
// 讀取後台「額外」指定在某個掛載點出現的題目（S1-7a）。四個固定掛載點代碼定義在
// db/seed/generate-club-seed-sql.py 的 FAQ_EMBED_SLOTS：academy_admission（4.7）、
// program_detail（5.x 各課程）、trials（3.3）、sponsorship（9.4）——目前只有前兩個被
// 消費（academy/join.vue、programs/childrens-training｜summer-camp），其餘兩個掛載點
// 屬於尚未開工的單元（3.2–3.5 球員發展機會、09 夥伴與贊助），不在本輪範圍。
//
// 🔴 Fail-open 但不是「全部顯示」：API 失敗或回傳空陣列時 faqs 為空陣列，呼叫端據此
// 隱藏整個「常見問題」子區塊、只保留「查看全部常見問題」連結——這裡跟 useHomeSections()
// 的 fail-open（找不到就當作啟用）不同，因為 FAQ 題目是要顯示的實際內容，不是一個
// 開關；API 沒有回傳題目，畫面上就不該無中生有列出題目。
export interface FaqEmbedItem {
  id: string
  slug: string
  isShared: boolean
  sortOrder: number
  question: string | null
  answer: string | null
  categorySlugs: string[]
}

export function useFaqEmbed(club: string, slotCode: string, lang: string) {
  const { data } = useFetch<FaqEmbedItem[]>(`/api/backend/${club}/faqs/embeds/${slotCode}`, {
    query: { lang },
    key: `faq-embed-${club}-${slotCode}-${lang}`,
  })
  const faqs = computed(() => data.value ?? [])
  return { faqs }
}
