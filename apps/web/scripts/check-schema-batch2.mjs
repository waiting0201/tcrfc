#!/usr/bin/env node
/**
 * check-schema-batch2.mjs — GEO-05 結構化資料第二批（S1-20：SportsEvent 場地地址／Event
 * 俱樂部活動／Course 課程）的固定資料驗證。
 *
 * ## 為什麼需要這支腳本
 *
 * `apps/api` 依派工規則本輪不得啟動，`matches`／`calendar_custom_events`／`programs`
 * 三張表現況也沒有能同時驗證「場地地址查得到」「俱樂部活動合格」「課程合格」的真實
 * 種子資料（見 apps/web/README.md「S1-20」節）。這支腳本比照
 * `check-faq-schema.mjs`（S1-18a 既有先例）的做法，直接對
 * `shared/utils/schema-batch2.ts`（`app/composables/useClubEventSchema.ts`／
 * `useCourseSchema.ts`／`app/pages/zh/schedule.vue` 的 SportsEvent 場地地址查找
 * 唯一依賴的純函式）驗證三件事：
 *
 *   1. **`venueAddressByName()`**：名稱比對正確、找不到時回傳 `null`（不臆造地址）。
 *   2. **Event（俱樂部活動）GEO-05 過濾正確**：`isClubEventSchemaEligible()`／
 *      `buildClubEventSchemaNodes()`——title／venueName 任一缺漏或起始時間無效即不合格，
 *      合格者才輸出且 `url` 錨點正確組出（對應 schedule.vue 樣板的 `:id="ce-{id}"`）。
 *   3. **Course（課程）GEO-05 過濾正確**：`isCourseSchemaEligible()`／
 *      `buildCourseSchemaNode()`——name／intro 任一缺漏即整個不輸出；`educationalLevel`
 *      只在 ageMin／ageMax 兩者皆有值時才附上（只輸出有真實資料的欄位）。
 *   4. **注入防護是真的成立**：沿用 `check-faq-schema.mjs` 的既有做法，用 `unhead/server`
 *      匯出的真正 `tagToString()` 驗證含 `</script>`／雙引號／反斜線的惡意欄位值序列化後
 *      不會提前結束標籤，且能一字不差還原。
 *
 * ## 這支腳本刻意不做的事
 *
 * 不對「已經跑起來的前台」發 HTTP 請求（那是 `check-faq-schema-live.mjs` 的既有工作範圍，
 * 本輪擴充見該檔案）。這裡純粹是固定資料的單元測試性質，不需要任何服務就能跑，掛進
 * `npm run lint`（`docs/18-work-errors.md` E-34：會被伺服器可用性影響的檢查不能掛進 lint）。
 *
 * ## 用法
 *
 *   node scripts/check-schema-batch2.mjs
 *
 * 離開碼：任一項斷言失敗 → `1`；全部通過 → `0`。
 */

import { tagToString } from 'unhead/server'
import {
  venueAddressByName,
  isClubEventSchemaEligible,
  buildClubEventSchemaNodes,
  isCourseSchemaEligible,
  buildCourseSchemaNode,
} from '../shared/utils/schema-batch2.ts'

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

console.log('GEO-05 結構化資料第二批（SportsEvent 場地地址／Event／Course）—— 固定資料驗證\n')

// ---------------------------------------------------------------------------
// ① SportsEvent 場地地址查找
// ---------------------------------------------------------------------------
console.log('── ① venueAddressByName() ─────────────')

const venues = [
  { nameZh: '西屯足球場', address: '台中市北屯區崇平路二段景谷巷 11 弄 41 號' },
  { nameZh: '台中北屯太原足球場', address: null },
]

assertEqual(venueAddressByName('西屯足球場', venues), '台中市北屯區崇平路二段景谷巷 11 弄 41 號', '名稱比對成功時回傳地址')
assertEqual(venueAddressByName('台中北屯太原足球場', venues), null, '比對成功但該場地地址本身為 null 時回傳 null（不臆造）')
assertEqual(venueAddressByName('不存在的場地', venues), null, '名稱比對不到時回傳 null，不臆造地址')
assertEqual(venueAddressByName(null, venues), null, 'venueName 本身為 null 時回傳 null')

// ---------------------------------------------------------------------------
// ② Event（俱樂部活動）GEO-05 過濾與輸出形狀
// ---------------------------------------------------------------------------
console.log('\n── ② isClubEventSchemaEligible()／buildClubEventSchemaNodes() ─────────────')

const opts = { siteUrl: 'https://tcrfc.tw/', pagePath: '/zh/schedule/' }

const eligibleEvent = {
  id: 'evt-1',
  title: '球迷見面會',
  startsAt: '2026-10-10T04:00:00.000Z',
  endsAt: '2026-10-10T06:00:00.000Z',
  venueName: '西屯足球場',
  description: '一線隊球員簽名會與拍照活動。',
  coverUrl: 'https://cdn.example.com/cover.webp',
}

assertTrue(isClubEventSchemaEligible(eligibleEvent), '合格活動：title／venueName／startsAt 皆齊全')
assertTrue(!isClubEventSchemaEligible({ ...eligibleEvent, title: null }), 'GEO-05：title 缺漏不合格')
assertTrue(!isClubEventSchemaEligible({ ...eligibleEvent, title: '   ' }), 'GEO-05：title 去除空白後為空字串視同不合格')
assertTrue(!isClubEventSchemaEligible({ ...eligibleEvent, venueName: null }), 'GEO-05：venueName 缺漏不合格（location 必填）')
assertTrue(!isClubEventSchemaEligible({ ...eligibleEvent, startsAt: '不是日期' }), 'GEO-05：startsAt 不是合法日期字串時不合格')

assertEqual(
  buildClubEventSchemaNodes([eligibleEvent], opts),
  [{
    name: '球迷見面會',
    startDate: '2026-10-10T04:00:00.000Z',
    location: { '@type': 'Place', name: '西屯足球場' },
    eventAttendanceMode: 'https://schema.org/OfflineEventAttendanceMode',
    url: 'https://tcrfc.tw/zh/schedule/#ce-evt-1',
    endDate: '2026-10-10T06:00:00.000Z',
    description: '一線隊球員簽名會與拍照活動。',
    image: 'https://cdn.example.com/cover.webp',
  }],
  '合格活動輸出完整欄位，url 錨點對應 schedule.vue 樣板的 :id="ce-{id}"',
)

assertEqual(
  buildClubEventSchemaNodes([eligibleEvent, { ...eligibleEvent, id: 'evt-2', venueName: null }], opts).length,
  1,
  'GEO-05：逐筆判斷，只有不合格的那一筆不輸出，其餘合格的仍要輸出',
)

{
  // 重複活動：同 id、不同發生日（occurrenceId）→ 每個發生日一個 Event 節點，url 錨點各自唯一
  const recurring = [
    { ...eligibleEvent, occurrenceId: 'evt-1:202610101200' },
    { ...eligibleEvent, startsAt: '2026-10-17T04:00:00.000Z', occurrenceId: 'evt-1:202610171200' },
  ]
  const nodes = buildClubEventSchemaNodes(recurring, opts)
  assertEqual(nodes.length, 2, '重複活動：每個發生日各輸出一個 Event 節點（schema.org／Google 對重複活動的慣用寫法）')
  assertEqual(new Set(nodes.map((n) => n.url)).size, 2, '重複活動：url 錨點以 occurrenceId 區分，兩個節點不會撞成同一個')
}

assertEqual(buildClubEventSchemaNodes([], opts), [], '輸入為空陣列時回傳空陣列（呼叫端據此完全不呼叫 useSchemaOrg）')

const minimalEvent = {
  id: 'evt-3', title: '社區足球日', startsAt: '2026-11-01T01:00:00.000Z',
  endsAt: null, venueName: '豐原體育場', description: null, coverUrl: null,
}
assertEqual(
  buildClubEventSchemaNodes([minimalEvent], opts),
  [{
    name: '社區足球日',
    startDate: '2026-11-01T01:00:00.000Z',
    location: { '@type': 'Place', name: '豐原體育場' },
    eventAttendanceMode: 'https://schema.org/OfflineEventAttendanceMode',
    url: 'https://tcrfc.tw/zh/schedule/#ce-evt-3',
    description: null,
    image: null,
  }],
  '無真實說明／封面圖時明確填 null（不是省略鍵）——防止 nuxt-schema-org 的 eventResolver '
    + 'inheritMeta 拿全站預設 SEO 說明／OG 圖頂替；endDate 因無 endsAt 而整個省略',
)

// ---------------------------------------------------------------------------
// ③ Course（課程）GEO-05 過濾與輸出形狀
// ---------------------------------------------------------------------------
console.log('\n── ③ isCourseSchemaEligible()／buildCourseSchemaNode() ─────────────')

const courseOpts = { providerName: '台中磐石足球俱樂部', siteUrl: 'https://tcrfc.tw/' }

assertTrue(isCourseSchemaEligible({ name: '兒童足球訓練', intro: '依年齡與能力分級規劃課程。', ageMin: 6, ageMax: 12 }), '合格課程：name／intro 皆非空')
assertTrue(!isCourseSchemaEligible(null), 'program 本身為 null 時不合格')
assertTrue(!isCourseSchemaEligible({ name: null, intro: '有說明沒名稱，不合格', ageMin: null, ageMax: null }), 'GEO-05：name 缺漏不合格')
assertTrue(!isCourseSchemaEligible({ name: '有名稱沒說明，不合格', intro: null, ageMin: null, ageMax: null }), 'GEO-05：description（intro）缺漏不合格')

assertEqual(
  buildCourseSchemaNode({ name: '兒童足球訓練', intro: '依年齡與能力分級規劃課程。', ageMin: 6, ageMax: 12 }, courseOpts),
  {
    name: '兒童足球訓練',
    description: '依年齡與能力分級規劃課程。',
    provider: { name: '台中磐石足球俱樂部', url: 'https://tcrfc.tw' },
    educationalLevel: '6–12 歲',
  },
  '合格課程輸出完整欄位，educationalLevel 由 ageMin／ageMax 組出',
)

assertEqual(
  buildCourseSchemaNode({ name: '夏令營', intro: '密集足球訓練營隊。', ageMin: null, ageMax: null }, courseOpts),
  {
    name: '夏令營',
    description: '密集足球訓練營隊。',
    provider: { name: '台中磐石足球俱樂部', url: 'https://tcrfc.tw' },
  },
  '只輸出有真實資料的欄位：ageMin／ageMax 任一缺漏時不附上 educationalLevel（不臆造年齡範圍）',
)

assertEqual(buildCourseSchemaNode(null, courseOpts), null, '資料不足（program 為 null）時回傳 null，呼叫端不輸出該型別')
assertEqual(
  buildCourseSchemaNode({ name: '<p>課程</p>', intro: '<b>說明</b>&nbsp;文字', ageMin: null, ageMax: null }, courseOpts).name,
  '課程',
  'name／description 沿用既有 cleanFaqSchemaText() 去除 HTML 標籤（防禦性處理，理由同 FAQPage）',
)

// ---------------------------------------------------------------------------
// ④ 注入防護：用 unhead 真正的 tagToString() 序列化（比照 check-faq-schema.mjs 既有做法）
// ---------------------------------------------------------------------------
console.log('\n── ④ `</script>` 提前結束標籤與 JSON 逸出（用 unhead 真實序列化函式）───')

// 🔴 這裡刻意不透過 buildClubEventSchemaNodes()：那支函式內部會用 cleanFaqSchemaText()
// 去除「長得像標籤」的字串，會把這題的惡意字串「順便」清乾淨（理由同 check-faq-schema.mjs
// 同一段既有註解）——這樣測不出「萬一某個值沒有先經過清理層」時，真正扛住注入的是
// useSchemaOrg() 最終呼叫的框架序列化本身，不是巧合借用了清理副作用。這裡直接構造
// 「已經合格、待輸出」的原始節點（模擬 defineEvent() 收到未清理的原始字面值）。
const rawEventName = '這題故意混入攻擊字串 </script><img src=x onerror=alert(1)>'
const rawEventDescription = '答案裡有 "雙引號"、反斜線 \\、換行\n，以及提前結束標籤 </script><script>alert(1)</script> 字樣。'
const maliciousNode = { name: rawEventName, description: rawEventDescription, startDate: '2026-12-01T00:00:00.000Z', location: { '@type': 'Place', name: '正常場地' } }

const graph = { '@context': 'https://schema.org', '@graph': [{ '@type': 'Event', ...maliciousNode }] }
const jsonText = JSON.stringify(graph)
const scriptTag = { tag: 'script', props: { type: 'application/ld+json' }, innerHTML: jsonText }
const rendered = tagToString(scriptTag)

const closingTag = '</script>'
assertTrue(rendered.endsWith(closingTag), '序列化輸出以真正的 </script> 收尾（否則後面斷言就沒有意義）')
const bodyBeforeRealClose = rendered.slice(0, rendered.length - closingTag.length)
assertTrue(
  !/<\/script/i.test(bodyBeforeRealClose),
  '真正收尾標籤之前，不存在任何字面 </script（unhead tagToString() 的 CLOSE_TAG_RE 已把它轉成 <\\/script）',
)

const openTagEnd = rendered.indexOf('>') + 1
const innerHtmlEscaped = rendered.slice(openTagEnd, rendered.length - closingTag.length)
const innerHtmlUnescaped = innerHtmlEscaped.replace(/<\\\/script/gi, '</script')
let roundTripOk = false
try {
  const parsed = JSON.parse(innerHtmlUnescaped)
  const parsedEvent = parsed['@graph'][0]
  roundTripOk = parsedEvent.name === rawEventName && parsedEvent.description === rawEventDescription
} catch (err) {
  console.error(`  JSON.parse 失敗：${err.message}`)
}
assertTrue(roundTripOk, 'JSON 字串逸出：還原轉義後 JSON.parse() 成功，且惡意字面值一字不差還原')

// ---------------------------------------------------------------------------
console.log(`\n${failures === 0 ? '✓' : '✗'} 共 ${failures} 項斷言失敗。`)
if (failures > 0) process.exit(1)
