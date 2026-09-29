#!/usr/bin/env node
/**
 * check-faq-schema.mjs — GEO-06（S1-18a）FAQPage 結構化資料的固定資料驗證。
 *
 * ## 為什麼需要這支腳本
 *
 * `apps/api` 依派工規則本輪不得啟動、`faqs` 表也還沒有真實種子問答
 * （apps/web/README.md「S1-18」節），沒有真實資料可以端到端驗證「有題目時 FAQPage
 * 真的輸出正確」。這支腳本改用固定 fixture 資料，直接對
 * `shared/utils/faq-schema.ts`（`app/composables/useFaqPageSchema.ts` 唯一依賴的
 * 純函式，見該檔檔頭「單一來源」說明）驗證三件事：
 *
 *   1. **形狀正確**：合格題目（question／answer 皆非空）都轉成
 *      `{ id, question, answer }`，可以直接餵給 `defineQuestion({ question, answer })`。
 *   2. **GEO-05 過濾正確**：question／answer 任一為 `null`，或去除 HTML 標籤後變成
 *      空字串，都不得進入輸出；同一題 id 出現兩次只保留一次（GEO-06「同一頁多個 FAQ
 *      區塊只輸出一份合併 FAQPage，不重複輸出同一題」）；全部不合格時回傳空陣列
 *      （呼叫端據此完全不輸出 FAQPage）。
 *   3. **注入防護是真的成立，不是憑印象假設**：用 `unhead/server` 匯出的
 *      **真正的** `tagToString()`（`useSchemaOrg()` 最終序列化 JSON-LD 用的同一支
 *      函式，見 shared/utils/faq-schema.ts 檔頭「`</script>` 提前結束標籤」段落的原始碼
 *      引用）組出跟正式頁面一模一樣的 `<script type="application/ld+json">…</script>`
 *      標籤字串，對含有惡意字面值（`</script>` 提前結束標籤、雙引號、反斜線、Unicode）
 *      的 fixture 資料驗證兩件事：
 *        a. 序列化後的字串裡除了標籤本身真正的收尾之外，不會再出現能被瀏覽器 HTML
 *           解析器認成「這個 `<script>` 標籤在這裡結束」的字面 `</script`；
 *        b. 把那個轉義還原（模擬瀏覽器讀 `<script>` 的 `textContent` 時看到的原始
 *           字串）之後，`JSON.parse()` 能正確還原出原始資料，一個字元都沒有失真。
 *      這裡刻意呼叫 unhead 的真實函式，不是自己重寫一份「看起來像」的正規表示式去
 *      驗證自己——那樣只是驗證自己與自己一致，驗不出框架的既有保護是否真的生效。
 *
 * ## 這支腳本刻意不做的事
 *
 * 不對「已經跑起來的前台」發 HTTP 請求（那是 `check-faq-schema-live.mjs` 的工作，
 * 見該檔檔頭）。這裡純粹是固定資料的單元測試性質，**不需要任何服務就能跑**，因此
 * 掛進 `npm run lint`（見 package.json `lint:faq-schema`）——比照 `check-match-status.mjs`
 * 等既有「純靜態、秒級跑完」的檢查（`docs/18-work-errors.md` `E-34`：會被伺服器
 * 可用性影響的檢查不能掛進 `lint`）。
 *
 * ## 用法
 *
 *   node scripts/check-faq-schema.mjs
 *
 * 離開碼：任一項斷言失敗 → `1`；全部通過 → `0`。
 */

import { tagToString } from 'unhead/server'
import { buildFaqSchemaQuestions, cleanFaqSchemaText } from '../shared/utils/faq-schema.ts'

let failures = 0

function assertEqual(actual, expected, label) {
  const a = JSON.stringify(actual)
  const e = JSON.stringify(expected)
  if (a !== e) {
    failures++
    console.error(`✗ ${label}\n    預期：${e}\n    實際：${a}`)
  } else {
    console.log(`✓ ${label}`)
  }
}

function assertTrue(condition, label) {
  if (!condition) {
    failures++
    console.error(`✗ ${label}`)
  } else {
    console.log(`✓ ${label}`)
  }
}

console.log('GEO-06 FAQPage 結構化資料 —— 固定資料驗證\n')

// ---------------------------------------------------------------------------
// ① 形狀正確 ＋ GEO-05 過濾正確
// ---------------------------------------------------------------------------
console.log('── ① buildFaqSchemaQuestions() 形狀與過濾 ─────────────')

assertEqual(
  buildFaqSchemaQuestions([
    { id: '1', question: '如何加入台中磐石球隊？', answer: '請透過線上申請表單提出試訓申請。' },
  ]),
  [{ id: '1', question: '如何加入台中磐石球隊？', answer: '請透過線上申請表單提出試訓申請。' }],
  '正常題目：完整保留',
)

assertEqual(
  buildFaqSchemaQuestions([
    { id: '1', question: null, answer: '有答案沒問題，不合格' },
    { id: '2', question: '有問題沒答案，不合格', answer: null },
    { id: '3', question: null, answer: null },
  ]),
  [],
  'GEO-05：question／answer 任一為 null 時，整題不輸出',
)

assertEqual(
  buildFaqSchemaQuestions([{ id: '1', question: '   ', answer: '答案本身正常' }]),
  [],
  'GEO-05：question 去除標籤與空白後為空字串，視同不合格',
)

assertEqual(
  buildFaqSchemaQuestions([
    { id: 'dup', question: '重複題目？', answer: '同一題 id 出現兩次' },
    { id: 'dup', question: '重複題目？', answer: '同一題 id 出現兩次' },
    { id: 'other', question: '另一題？', answer: '另一個答案' },
  ]),
  [
    { id: 'dup', question: '重複題目？', answer: '同一題 id 出現兩次' },
    { id: 'other', question: '另一題？', answer: '另一個答案' },
  ],
  'GEO-06：同一題 id 出現兩次只保留第一次，不重複輸出同一題',
)

assertEqual(buildFaqSchemaQuestions([]), [], 'GEO-05：輸入為空陣列時回傳空陣列（呼叫端據此完全不輸出 FAQPage）')

assertEqual(
  cleanFaqSchemaText('<p>需要<b>試訓</b>嗎？</p>&nbsp;&amp;&lt;&gt;&quot;&#39;'),
  '需要試訓嗎？ &<>"\'',
  'cleanFaqSchemaText()：標籤去除、常見實體解碼獨立驗證',
)

assertEqual(
  buildFaqSchemaQuestions([
    { id: '1', question: '<p>需要<b>試訓</b>嗎？</p>', answer: '<p>是的，需要先完成試訓。</p>&nbsp;謝謝。' },
  ]),
  [{ id: '1', question: '需要試訓嗎？', answer: '是的，需要先完成試訓。 謝謝。' }],
  'schema.org 純文字欄位：HTML 標籤去除（不留空格）、常見實體解碼、空白壓縮',
)

// ---------------------------------------------------------------------------
// ② 注入防護：用 unhead 真正的 tagToString() 序列化，而不是自己重寫一份規則
// ---------------------------------------------------------------------------
console.log('\n── ② `</script>` 提前結束標籤與 JSON 逸出（用 unhead 真實序列化函式）───')

// 🔴 這裡刻意不先經過 buildFaqSchemaQuestions()／cleanFaqSchemaText()：那支函式會把
// 「長得像標籤」的字串整段去除（`<[^>]*>`），這題的惡意字串剛好長得像標籤，會被
// 「順便」清乾淨——這樣測不出「萬一某個值沒有先經過這層清理」時，真正扛住注入的是
// 框架序列化層本身，不是巧合借用了清理副作用。這裡直接構造「已經合格、待輸出」的
// 節點（模擬 defineQuestion() 收到的原始字面值），驗證 useSchemaOrg() 最終呼叫的
// unhead 序列化本身在沒有任何前置清理的情況下仍然安全。
const rawQuestionName = '這題故意混入攻擊字串，答案裡有 </script><img src=x onerror=alert(1)>'
const rawAnswerText = '正常答案裡混入 "雙引號"、反斜線 \\、換行\n，以及提前結束標籤 </script><script>alert(1)</script> 字樣。'

const graph = {
  '@context': 'https://schema.org',
  '@graph': [
    { '@type': 'FAQPage', '@id': '#webpage' },
    { '@type': 'Question', name: rawQuestionName, acceptedAnswer: { '@type': 'Answer', text: rawAnswerText } },
  ],
}
const jsonText = JSON.stringify(graph)

// 比照 nuxt-schema-org 實際組出來的 tag 形狀（type/key/innerHTML），交給 unhead
// 真正的序列化函式——這裡不是重新發明一套「看起來像」的轉義規則。
const scriptTag = { tag: 'script', props: { type: 'application/ld+json' }, innerHTML: jsonText }
const rendered = tagToString(scriptTag)

// 斷言 a：整段輸出裡，字面 `</script`（忽略大小寫）只能出現在標籤真正的收尾，不能
// 提前出現——把最後 9 個字元（`</script>`）之外的部分抓出來單獨檢查。
const closingTag = '</script>'
assertTrue(rendered.endsWith(closingTag), '序列化輸出以真正的 </script> 收尾（否則後面斷言就沒有意義）')
const bodyBeforeRealClose = rendered.slice(0, rendered.length - closingTag.length)
assertTrue(
  !/<\/script/i.test(bodyBeforeRealClose),
  '真正收尾標籤之前，不存在任何字面 </script（unhead tagToString() 的 CLOSE_TAG_RE 已把它轉成 <\\/script）',
)

// 斷言 b：把 unhead 的轉義還原（`<\/script` → `</script`，模擬瀏覽器讀 textContent
// 看到的原始字串），JSON.parse() 必須能正確還原出一字不差的原始資料。
const openTagEnd = rendered.indexOf('>') + 1
const innerHtmlEscaped = rendered.slice(openTagEnd, rendered.length - closingTag.length)
const innerHtmlUnescaped = innerHtmlEscaped.replace(/<\\\/script/gi, '</script')
let roundTripOk = false
try {
  const parsed = JSON.parse(innerHtmlUnescaped)
  const parsedQuestion = parsed['@graph'].find((n) => n['@type'] === 'Question')
  roundTripOk = parsedQuestion?.name === rawQuestionName
    && parsedQuestion?.acceptedAnswer?.text === rawAnswerText
} catch (err) {
  console.error(`  JSON.parse 失敗：${err.message}`)
}
assertTrue(roundTripOk, 'JSON 字串逸出：還原轉義後 JSON.parse() 成功，且惡意字面值一字不差還原（雙引號／反斜線／換行／</script>）')

// ---------------------------------------------------------------------------
console.log(`\n${failures === 0 ? '✓' : '✗'} 共 ${failures} 項斷言失敗。`)
if (failures > 0) process.exit(1)
