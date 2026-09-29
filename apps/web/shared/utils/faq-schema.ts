// shared/utils/faq-schema.ts — GEO-06 FAQPage 結構化資料的純函式（S1-18a）
//
// 抽成不依賴 Vue／Nuxt runtime 的純函式，理由：
//   1. 讓 app/composables/useFaqPageSchema.ts 與各頁面（12 FAQ 首頁、4 個獨立主題頁、
//      3 個 G-12 嵌入頁）共用同一份「哪些題目合格、文字怎麼清理」判斷，不重寫第二份
//      （比照 useSchemaOrgClub.ts 檔頭 E-39「單一來源」的既有原則）。
//   2. 讓 scripts/check-faq-schema.mjs 能在不啟動 apps/api、不需要 Nuxt runtime的
//      情況下，直接對固定 fixture 資料驗證輸出形狀與注入防護（任務指示要求「另外用
//      單元測試或固定資料驗證有資料時的輸出」，`apps/api` 未啟動、`faqs` 表也還沒有
//      真實種子問答，見 apps/web/README.md「S1-18」節）。
//
// 🔴 內容規範本身不在這裡：問題要不要寫成完整句子、答案首句是不是結論，是後台編輯與
// 資料本身的責任（主站規劃書 §7 GEO-06、任務指示原文「這是內容規範，前台只負責輸出，
// 不得改寫內容」）。這裡只做「輸出前的技術處理」——去除 HTML 標籤與多餘空白，讓
// schema.org 的 Question.name／acceptedAnswer.text 是乾淨的純文字：
//   - apps/api FaqListItemDto.question／answer 目前是純文字欄位（string｜null），
//     app/components/FaqAccordion.vue 也是用 `{{ }}` 文字插值顯示 answer，不是
//     `v-html`——本來就沒有把答案當 HTML 渲染。這裡的清理是防禦性的：萬一資料裡混入
//     HTML 標籤（例如編輯不慎貼上富文本編輯器的殘留標籤），仍然只留下乾淨文字，讓
//     GEO-04「事實雙重呈現、明文與結構化資料一致」在這個情境下不因為「一邊有標籤、
//     一邊清過」而互相矛盾。
//
// 🔴 `</script>` 提前結束標籤的注入防護不是這裡做的：JSON-LD 最終由
// nuxt-schema-org／unhead 統一序列化（`useSchemaOrg()` → `useHead({ script: [...] })`），
// unhead 的 `tagToString()`（node_modules/unhead/dist/shared/unhead.*.mjs）對每個
// `<script>` 標籤的 innerHTML 一律把字面 `</script` 取代成 `<\/script`（`CLOSE_TAG_RE`），
// 這是全站既有 JSON-LD（Organization／SportsTeam／Article／SportsEvent）共用的框架層
// 保護，不是本檔新增的機制，也不需要在這裡重做一次。JSON 字串本身的跳脫（雙引號、
// 反斜線、控制字元）則交給 `JSON.stringify()`（同樣是框架內部序列化時做的事）。
// 這裡的 `cleanFaqSchemaText()` 純粹是「schema.org 純文字欄位」的資料清理，跟上述
// 兩層防護是不同層次，缺一不影響另一個成立。

export interface FaqSchemaSourceItem {
  id: string
  question: string | null
  answer: string | null
}

export interface FaqSchemaQuestionNode {
  id: string
  question: string
  answer: string
}

/** 去除 HTML 標籤、解碼常見 HTML 實體、壓縮空白——用於 schema.org 純文字欄位。
 * 不是完整的 HTML-to-text 轉換器（沒有處理巢狀語意，例如 <li> 前綴符號），本站 FAQ
 * 答案目前是純文字欄位，這裡只防「萬一混入標籤」，見上方檔頭說明。 */
export function cleanFaqSchemaText(raw: string): string {
  return raw
    .replace(/<[^>]*>/g, '')
    .replace(/&nbsp;/gi, ' ')
    .replace(/&amp;/gi, '&')
    .replace(/&lt;/gi, '<')
    .replace(/&gt;/gi, '>')
    .replace(/&quot;/gi, '"')
    .replace(/&#0?39;/gi, '\'')
    .replace(/\s+/g, ' ')
    .trim()
}

/** 由「這一頁上可能來自多個 FAQ 區塊（首頁多個分類、或單一分類）的題目」算出這一頁
 * 該輸出的 FAQPage `mainEntity` 節點清單（GEO-05／GEO-06）：
 *   - `question`／`answer` 任一為 `null`，或去除標籤與空白後變成空字串 → 不合格，
 *     不輸出這題（比照 FaqAccordion.vue `validFaqs` 的既有防呆，這裡是第二個消費點，
 *     兩處判斷條件必須一致——都是「question 與 answer 兩者皆有實質內容」）。
 *   - 同一題 `id` 出現兩次（例如呼叫端不慎把同一題放進兩個區塊）只保留第一次出現，
 *     不重複輸出同一題（任務指示明文要求）。
 *   - 全部不合格或輸入為空陣列 → 回傳空陣列，呼叫端看到空陣列就不能呼叫
 *     `useSchemaOrg`（GEO-05「資料不足時不輸出該型別」／「空的 FAQPage 不輸出」）。 */
export function buildFaqSchemaQuestions(items: readonly FaqSchemaSourceItem[]): FaqSchemaQuestionNode[] {
  const seen = new Set<string>()
  const result: FaqSchemaQuestionNode[] = []
  for (const item of items) {
    if (!item.question || !item.answer) continue
    if (seen.has(item.id)) continue
    const question = cleanFaqSchemaText(item.question)
    const answer = cleanFaqSchemaText(item.answer)
    if (!question || !answer) continue
    seen.add(item.id)
    result.push({ id: item.id, question, answer })
  }
  return result
}
