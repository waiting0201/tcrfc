<script setup lang="ts">
// app/components/FaqAccordion.vue — 12 FAQ 手風琴清單（S1-18）
//
// 供 FAQ 首頁（依主題分組後逐組呼叫一次）與 4 個高頻主題獨立頁面共用，避免手風琴、
// 深層連結、有用回饋三段邏輯在 5 個頁面各自重寫一份。
//
// 沿用原 mockup（site/src/pages/zh/faq/index.html）三段既有行為，只是資料來源從
// 靜態 HTML 換成 props 傳入的真實 API 資料：
//   1. 手風琴：原生 <details>/<summary>，天生支援鍵盤操作（Tab 移焦、Enter／Space
//      展開收合），不需要另外寫 ARIA 或 JS 鍵盤事件。
//   2. 單題深層連結：id="q-<slug>"（改用真實 slug，不是原 mockup 的流水號 q-101）。
//      掛載時比對 location.hash 自動展開並捲動定位；使用者手動展開時用
//      history.replaceState 同步網址，方便複製分享。
//   3. 有用回饋 👍/👎：規劃書 3.12「回饋數據回寫後台」——這是本輪唯一要求接上真實
//      寫入的互動（搜尋零結果記錄、瀏覽數遞增規格未列，本輪不做，見
//      apps/web/README.md「S1-18」節）。呼叫 POST /api/backend/{club}/faqs/{slug}/feedback，
//      失敗只 console.warn、不影響畫面（回饋是加分數據，不是關鍵路徑，比照
//      useFaqEmbed.ts 一貫的「非關鍵資料失敗不可讓頁面壞掉」原則）。按一次鎖定
//      （aria-pressed 已為 true 的按鈕不重複送出），比照原 mockup 視覺行為。
//
// 🔴 防呆：question／answer 任一為 null 的題目不渲染（過濾掉，不用「問題整理中」
// 這類替代文字頂替）——後端 FaqListItemDto 的 zh-Hant side table 理論上必填，
// 兩者皆為 null 代表資料本身不完整，寧可少顯示一題也不要顯示一題只有問題沒有答案
// （或反之）的殘缺內容。
interface FaqAccordionItem {
  id: string
  slug: string
  question: string | null
  answer: string | null
}

const props = defineProps<{
  faqs: FaqAccordionItem[]
  club: string
  emptyText?: string
}>()

// 英文版（主站 /en/）的預設空狀態文字與回饋按鈕文字；呼叫端有傳 emptyText 時仍以傳入值為準。
const { tx } = useLocale()
const emptyMessage = computed(() => props.emptyText ?? tx('本主題常見問題收錄中，稍後將於本頁公布。', 'Questions for this topic are being compiled and will be published here soon.'))

const validFaqs = computed(() => props.faqs.filter((f) => f.question && f.answer))

const pressed = reactive<Record<string, 'up' | 'down' | undefined>>({})

async function submitFeedback(faq: FaqAccordionItem, helpful: boolean) {
  if (pressed[faq.id]) return // 已回饋過，鎖定不重複送出（比照原 mockup 視覺行為）
  pressed[faq.id] = helpful ? 'up' : 'down'
  try {
    await $fetch(`/api/backend/${props.club}/faqs/${faq.slug}/feedback`, {
      method: 'POST',
      body: { helpful },
    })
  } catch (err) {
    // 回饋是加分數據不是關鍵路徑，失敗不影響畫面，只留紀錄方便事後查（同
    // useFaqEmbed.ts／useSiteFacts.ts 一貫的 fail-open 原則）。
    console.warn('[FaqAccordion] 回饋送出失敗', err)
  }
}

function onToggle(faq: FaqAccordionItem, event: Event) {
  const el = event.target as HTMLDetailsElement
  if (el.open && typeof history !== 'undefined' && history.replaceState) {
    history.replaceState(null, '', `#q-${faq.slug}`)
  }
  // A-8：展開時回報瀏覽數（每工作階段每題最多一次）
  if (el.open) reportView(props.club, 'faqs', faq.slug)
}

onMounted(() => {
  if (typeof location === 'undefined') return
  if (location.hash.indexOf('#q-') !== 0) return
  const target = document.getElementById(location.hash.slice(1))
  if (target instanceof HTMLDetailsElement) {
    target.open = true
    setTimeout(() => target.scrollIntoView({ behavior: 'smooth', block: 'center' }), 50)
  }
})
</script>

<template>
<p v-if="validFaqs.length === 0" class="faq-accordion-empty">{{ emptyMessage }}</p>
<div v-else class="accordion">
  <details
    v-for="f in validFaqs"
    :id="`q-${f.slug}`"
    :key="f.id"
    class="accordion-item"
    @toggle="onToggle(f, $event)"
  >
    <summary class="accordion-item__q">
      <span>{{ f.question }}</span>
      <span class="accordion-item__icon" aria-hidden="true"/>
    </summary>
    <div class="accordion-item__a">
      <p>{{ f.answer }}</p>
      <div class="accordion-item__feedback">
        <span>{{ tx('這則說明有幫助嗎？', 'Was this answer helpful?') }}</span>
        <button
          type="button"
          class="fb-btn"
          :aria-pressed="pressed[f.id] === 'up'"
          :aria-label="tx('有幫助', 'Helpful')"
          @click="submitFeedback(f, true)"
        >👍</button>
        <button
          type="button"
          class="fb-btn"
          :aria-pressed="pressed[f.id] === 'down'"
          :aria-label="tx('沒有幫助', 'Not helpful')"
          @click="submitFeedback(f, false)"
        >👎</button>
      </div>
    </div>
  </details>
</div>
</template>

<style>
/* 逐字沿用原 mockup（site/src/pages/zh/faq/index.html）的 accordion 系列樣式，
   原本內嵌在頁面 <style> 裡，抽成元件後移到這裡，樣式規則本身不變。 */
.faq-accordion-empty{ color:var(--muted); font-size:.92rem; padding:.5rem 0 1.5rem; }

.accordion-item{ border-bottom:1px solid var(--rule); }
.accordion-item summary::-webkit-details-marker{ display:none; }
.accordion-item__q{
  list-style:none; cursor:pointer; display:flex; align-items:center; justify-content:space-between; gap:1rem;
  padding:1.1rem 0; font-weight:700; font-size:.98rem; color:var(--heading);
}
.accordion-item__q:focus-visible{ outline:2px solid var(--brand-aa); outline-offset:2px; }
.accordion-item__icon{ flex:none; width:20px; height:20px; position:relative; }
.accordion-item__icon::before, .accordion-item__icon::after{ content:""; position:absolute; background:var(--ink); }
.accordion-item__icon::before{ top:50%; left:0; width:100%; height:2px; transform:translateY(-50%); }
.accordion-item__icon::after{ left:50%; top:0; width:2px; height:100%; transform:translateX(-50%); transition:transform .2s var(--ease); }
.accordion-item[open] .accordion-item__icon::after{ transform:translateX(-50%) scaleY(0); }
.accordion-item__a{ padding:0 0 1.5rem; color:var(--text); line-height:1.8; font-size:.92rem; }
.accordion-item__a a{ color:var(--brand-aa); text-decoration:underline; }
.accordion-item:target{ box-shadow:inset 3px 0 0 var(--brand-aa); }

.accordion-item__feedback{ display:flex; align-items:center; gap:.6rem; font-size:.8rem; color:var(--muted); margin-top:.75rem; }
.fb-btn{ min-height:32px; min-width:32px; padding:0 .4rem; font-size:1rem; }
.fb-btn[aria-pressed="true"]{ opacity:.5; }
</style>
