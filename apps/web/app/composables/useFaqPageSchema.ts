// app/composables/useFaqPageSchema.ts — GEO-06 FAQPage 結構化資料（S1-18a）
//
// 主站規劃書 §7 `GEO-06`「單元 12 與 G-12 一律輸出 FAQPage」；docs/05-i18n-seo.md §3
// 同條。沿用既有 JSON-LD 輸出機制（`useSchemaOrg`，見 app/composables/useSchemaOrgClub.ts
// 既有的 Organization／SportsTeam 兩個先例），不另起爐灶。
//
// `nuxt-schema-org`（`@nuxtjs/seo` 內建）沒有 `defineFaqPage()` 這個專用型別，只有
// `defineQuestion()`。查原始碼（node_modules/nuxt-schema-org/dist/schema.mjs
// `questionResolver.resolveRootNode`）確認機制：每個 `defineQuestion()` 節點在 resolve
// 階段會去找這一頁的 Primary WebPage 節點，只有該節點 `@type` 含 `FAQPage`（或
// `QAPage`）時，才會把這題併入它的 `mainEntity` 陣列。因此輸出 FAQPage 的正確作法是
// 「把這一頁的 WebPage 型別宣告為 FAQPage」＋「逐題呼叫 defineQuestion()」兩件事一起做。
//
// GEO-05／「同一頁多個 FAQ 區塊只輸出一份合併 FAQPage、不重複輸出同一題」的判斷邏輯
// 抽到 shared/utils/faq-schema.ts（`buildFaqSchemaQuestions`），這裡只負責接上
// `useSchemaOrg`。呼叫端（12 FAQ 首頁、4 個獨立主題頁、3 個 G-12 嵌入頁）把「這一頁
// 實際渲染出來的 FAQ 題目」整份餵進來即可，不需要自己先過濾或去重。
//
// 🔴 沒有合格題目時完全不呼叫 `useSchemaOrg`（連 `defineWebPage` 都不呼叫）——GEO-05
// 「資料不足時不輸出該型別」／「空的 FAQPage 不輸出」。
//
// ⚠️ **已知限制，記在 `docs/18-work-errors.md`（E-74），不在本檔修正**：`/zh/faq/`
// 這一條路由（僅此一條，`join-team`／`academy-admission` 等子頁的網址結尾不是
// `faq` 不受影響）即使完全不呼叫這個 composable，仍然會被 `nuxt-schema-org` 的
// `webPageResolver` 內建「依網址結尾猜頁面型別」預設邏輯自動宣告成
// `@type:["WebPage","FAQPage"]` 卻沒有 `mainEntity`——已用本機 SSR 輸出實測確認
// （`node_modules/nuxt-schema-org/dist/schema.mjs` `webPageResolver.defaults()`，
// `endPath==='faq'`），且已證實這與本檔完全無關：`/zh/about/`（`@nuxtjs/seo` 的
// canonical 網址同樣不帶結尾斜線，導致 endPath 剛好等於猜測表裡的 `about`）在
// S1-18a 之前就已經有一模一樣的 `["WebPage","AboutPage"]` 殘缺輸出，是全站既有、
// 與 GEO-06 無關的缺陷（canonical 網址生成與這套猜測表的互動），根因不在
// `apps/web` 的頁面邏輯，屬於另一次交付的範圍。試過在這裡顯式呼叫
// `defineWebPage({'@type':'WebPage'})` 覆寫，但 `nuxt-schema-org` 對同一個
// `@id` 的多次節點合併是「陣列聯集、不支援移除」（`merge()` 函式，見
// `docs/18-work-errors.md` E-74 的完整原始碼追查記錄），無法從這個層級蓋掉框架
// 自己的猜測，因此維持「不輸出」的簡單寫法，不引入沒有效果的覆寫程式碼。
import type { MaybeRefOrGetter } from 'vue'
import { buildFaqSchemaQuestions, type FaqSchemaSourceItem } from '#shared/utils/faq-schema'

export function useFaqPageSchema(items: MaybeRefOrGetter<FaqSchemaSourceItem[]>) {
  // 🔴 F1（2026-10-03）：不能用 `watchEffect(() => { if 空 return; useSchemaOrg(...) })`。
  // 呼叫端的 `useFetch` 沒有 await（FAQ 資料在 setup 當下還是空陣列），watchEffect 第一次執行時
  // 題目是空的 → 直接 return；資料到了之後 watchEffect 重跑，但那時已經不在元件 setup 的
  // 同步階段，`useSchemaOrg` 的 `useHead` 沒有作用中的 Nuxt／unhead 實體，輸出悄悄消失
  // （頁面渲染 10 題、JSON-LD 卻只有 WebSite／WebPage，無任何錯誤訊息）。
  // 改成在 setup 同步階段「一定呼叫一次」`useSchemaOrg`，輸入給 getter：unhead 在 SSR 輸出
  // head 的時候才解析 `nodes`（那時 SSR 已等完所有 useFetch），client 端則隨資料變動重算。
  // 沒有合格題目時 getter 回空陣列，GEO-05「資料不足時不輸出該型別」維持（連 `defineWebPage`
  // 都不放進去，不會影響 E-74 的型別猜測行為）。
  useSchemaOrg((() => {
    const questions = buildFaqSchemaQuestions(toValue(items))
    if (questions.length === 0) return []
    return [
      defineWebPage({ '@type': 'FAQPage' }),
      ...questions.map((q) => defineQuestion({
        question: q.question,
        answer: q.answer,
      })),
    ]
  }) as unknown as Parameters<typeof useSchemaOrg>[0])
}
