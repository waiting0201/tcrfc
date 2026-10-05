<script setup lang="ts">
// app/pages/zh/faq/index.vue — 12 FAQ 首頁（S1-18，改為資料驅動）
//
// 原檔（S0-9）由 site/src/pages/zh/faq/index.html 逐段轉來，十個主題全是
// 「本分類問題整理中」的靜態占位。S1-18 起改讀 apps/api 真實資料
// （useFaqCategories／useFaqList，見兩支 composable 檔頭），沒有真實內容的
// 主題維持「收錄中」空狀態——不是拿掉，是換成「資料驅動的空狀態」。
//
// 🔴 一題可能同時掛在多個分類（FaqItem.categorySlugs 是陣列，後端刻意支援
// 多對多標記，見 FaqsRepository.ListAsync 檔頭），但本頁單題深層連結
// `#q-<slug>` 必須全站唯一（規劃書「方便客服直接傳送單題連結」的前提）。
// 因此每題只指派給「排序最前的一個分類」陳列一次（見下方 faqsByCategory），
// 不是每個分類都重複顯示——多分類標記的用途是讓 G-12 嵌入區塊／未來搜尋能
// 從多個主題撈到同一題，不是「首頁要多處重複顯示」。
definePageMeta({ nav: '', unit: '12', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club

const siteName = computed(() => (isEn.value ? (club === 'bw' ? BW_NAME_EN : CLUB_NAME_EN) : getClubAssets(club).nameZh))

const { categories } = useFaqCategories(locale.value)
const { faqs } = useFaqList(club, locale.value)

// 🔴 S1-18 藍鯨取捨：`faq_categories` 無 club_id、兩俱樂部共用同一份分類主檔
// （見 useFaqCategories.ts 檔頭），但「學院招生」「課程與營隊報名」兩個分類的
// 問答內容分別對應磐石學院招生架構與磐石 05 課程報名架構——藍鯨規劃書 §3 明文
// 「04 青年隊不沿用招生與課程報名架構」，同 4.7／5.1／5.2 的關閉理由，見
// shared/utils/units.ts 的 isFaqCategoryEnabledForClub／BLUE_WHALE_DISABLED_UNITS
// 說明。其餘八個分類本輪對照規劃書後沒有找到明文排除依據，維持開放（取捨依據
// 列在本輪交付報告，不在此自行擴大關閉範圍）。
const visibleCategories = computed(() =>
  categories.value.filter((cat) => isFaqCategoryEnabledForClub(cat.slug, club)),
)

// 每題只指派給第一個相符的「可見」分類（分類已依 sortOrder 排序），確保單題深層
// 連結全頁唯一，見上方檔頭說明。被關閉分類的題目不併入其他分類顯示——整個分類
// 對該俱樂部視同不存在，同獨立主題頁 404 的關閉方式一致。
const faqsByCategory = computed(() => {
  const map = new Map<string, typeof faqs.value>()
  const assigned = new Set<string>()
  for (const cat of visibleCategories.value) {
    const items = faqs.value.filter((f) => !assigned.has(f.id) && f.categorySlugs.includes(cat.slug))
    items.forEach((f) => assigned.add(f.id))
    map.set(cat.slug, items)
  }
  return map
})

// 顯示用總題數：只算「可見分類」裡的題目，不是後端回傳的全站原始總數——
// 對藍鯨而言，被關閉分類（學院招生／課程與營隊報名）底下的題目不該被算進
// 「目前共收錄 N 題」，否則畫面上看不到卻計入數字裡會顯得矛盾。
const totalCategorizedCount = computed(() =>
  [...faqsByCategory.value.values()].reduce((sum, items) => sum + items.length, 0),
)

useSeoMeta({
  title: () => (isEn.value ? `FAQ | ${siteName.value}` : `常見問題 FAQ｜${siteName.value}`),
  description: () => {
    // 🔴 主題名稱一律由 visibleCategories 動態組出，不寫死十個分類的中文名稱——
    // 寫死會在藍鯨容器印出「學院招生」等已關閉分類的名稱，觸發
    // check-club-brand-leak.mjs 的「學院」禁詞命中（見 apps/web/README.md「S1-18」節）。
    const topicNames = visibleCategories.value.map((cat) => cat.name).filter(Boolean).join(isEn.value ? ', ' : '、')
    if (isEn.value) {
      const topicPartEn = topicNames ? `, covering topics such as ${topicNames}` : ''
      return `Frequently asked questions about ${siteName.value}, organised by topic${topicPartEn}. ${totalCategorizedCount.value} ${totalCategorizedCount.value === 1 ? 'question is' : 'questions are'} currently included.`
    }
    const topicPart = topicNames ? `，涵蓋${topicNames}等主題` : ''
    return `${siteName.value}常見問題集，依主題分類整理${topicPart}，目前共收錄 ${totalCategorizedCount.value} 題。`
  },
})

// 先建 3–4 個高頻主題的獨立頁面（規劃書順序前四項：加入球隊／學院招生／課程與營隊
// 報名／費用與退費），其餘六個主題本輪仍只在本頁彙整呈現，見 apps/web/README.md
// 「S1-18」節。
const DEDICATED_PAGE_SLUGS: Record<string, string> = {
  'join-team': '/zh/faq/join-team/',
  'academy-admission': '/zh/faq/academy-admission/',
  'programs-camps': '/zh/faq/programs-camps/',
  'fees-refunds': '/zh/faq/fees-refunds/',
}

function dedicatedPageFor(slug: string): string | undefined {
  return DEDICATED_PAGE_SLUGS[slug]
}

const search = ref('')
const keyword = computed(() => search.value.trim().toLowerCase())

function matchesKeyword(f: { question: string | null; answer: string | null }): boolean {
  if (!keyword.value) return true
  const text = `${f.question ?? ''} ${f.answer ?? ''}`.toLowerCase()
  return text.includes(keyword.value)
}

const visibleByCategory = computed(() => {
  const map = new Map<string, typeof faqs.value>()
  for (const [slug, items] of faqsByCategory.value) {
    map.set(slug, keyword.value ? items.filter(matchesKeyword) : items)
  }
  return map
})

const totalVisible = computed(() =>
  [...visibleByCategory.value.values()].reduce((sum, items) => sum + items.length, 0),
)
const isSearching = computed(() => keyword.value.length > 0)
const noResult = computed(() => isSearching.value && totalVisible.value === 0)

// GEO-06（S1-18a）：本頁把多個分類區塊各自的題目攤平成一份清單餵給
// useFaqPageSchema()，只輸出一份合併的 FAQPage（任務指示明文要求），不對每個
// `<FaqAccordion>` 各自呼叫一次。用 faqsByCategory（未套用搜尋關鍵字篩選的完整
// 可見清單）而不是 visibleByCategory：搜尋框是 client-side 互動篩選，SSR 輸出的
// JSON-LD 應該反映「這一頁完整收錄的題目」，不是使用者當下打的關鍵字結果（且
// SSR 階段 search 恆為空字串，兩者在初始渲染時本來就相同）。faqsByCategory 本身
// 已經是「每題只指派給第一個可見分類」的去重結果（見上方檔頭說明），
// buildFaqSchemaQuestions() 的 id 去重是第二層防呆，不是本頁需要仰賴的機制。
useFaqPageSchema(computed(() => [...faqsByCategory.value.values()].flat()))

// 英文版：後端 `?lang=en` 逐欄位回退繁中，FAQ 回應沒有旗標，偵測漢字決定是否提示「部分內容只有繁體中文」。
const hasZhContent = computed(() => isEn.value && faqs.value.some((f) => /[\u3400-\u9fff]/.test(`${f.question ?? ''}${f.answer ?? ''}`)))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('常見問題', 'FAQ') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">12</span>
  <div class="container">
    <p class="page-hero__eyebrow">12 FAQ</p>
    <h1><template v-if="isEn">FAQ</template><template v-else>常見問題<span class="en">FAQ</span></template></h1>
    <p class="page-hero__lede">{{ isEn ? `Frequently asked questions organised by topic. Search by keyword, or share a link to a single question with anyone who needs it. ${totalCategorizedCount} ${totalCategorizedCount === 1 ? 'question is' : 'questions are'} currently included.` : `依主題分類整理的常見問題，可直接搜尋關鍵字，或分享單題連結給需要的人。目前共收錄 ${totalCategorizedCount} 題。` }}</p>
  </div>
</section>

<LocaleFallbackNotice v-if="hasZhContent" partial />

<section class="band faq-band" aria-labelledby="faq-title">
  <div class="band-inner container">
    <h2 id="faq-title" class="visually-hidden">{{ tx('常見問題搜尋與分類', 'FAQ search and topics') }}</h2>

    <div class="faq-search">
      <label class="visually-hidden" for="faq-search-input">{{ tx('搜尋常見問題', 'Search the FAQ') }}</label>
      <input
        id="faq-search-input"
        v-model="search"
        type="search"
        :placeholder="tx('輸入關鍵字搜尋問題與答案…', 'Search questions and answers by keyword…')"
        autocomplete="off"
      >
    </div>

    <p v-if="noResult" class="faq-no-result">
      <template v-if="isEn">No matching questions found. Please feel free to <a :href="lp('/zh/join/general/')">contact us</a> and we will reply to your question as soon as we can.</template>
      <template v-else>
      沒有找到符合的問題。歡迎直接
      <a :href="lp('/zh/join/general/')">聯絡我們</a>，我們會盡快回覆你的問題。
      </template>
    </p>

    <nav v-if="!isSearching" class="faq-topics" :aria-label="tx('常見問題主題', 'FAQ topics')">
      <a v-for="cat in visibleCategories" :key="cat.id" class="faq-topic-card" :href="`#topic-${cat.slug}`">{{ cat.name }}</a>
    </nav>

    <div class="faq-categories">
      <section
        v-for="cat in visibleCategories"
        v-show="!isSearching || (visibleByCategory.get(cat.slug)?.length ?? 0) > 0"
        :id="`topic-${cat.slug}`"
        :key="cat.id"
        class="faq-category"
      >
        <div class="faq-category__head">
          <h2 class="faq-category__title">{{ cat.name }}</h2>
          <a v-if="dedicatedPageFor(cat.slug)" class="faq-category__more" :href="lp(dedicatedPageFor(cat.slug) ?? '/zh/faq/')">{{ tx('獨立主題頁 →', 'Topic page →') }}</a>
        </div>
        <FaqAccordion :faqs="visibleByCategory.get(cat.slug) ?? []" :club="club" />
      </section>
    </div>

    <div class="faq-fallback-cta">
      <p>{{ tx('沒有找到你要的答案？', 'Could not find the answer you need?') }}</p>
      <a class="btn btn--primary" :href="lp('/zh/join/general/')">{{ tx('聯絡我們 10.7', 'Contact Us 10.7') }}</a>
    </div>
  </div>
</section>
</template>

<style>
.faq-band{ padding-block:clamp(3.5rem,6vw,6rem); }

.faq-search{ max-width:520px; margin-bottom:2rem; }
.faq-search input{
  width:100%; border:2px solid var(--rule); background:var(--paper);
  padding:.9rem 1.1rem; font-size:1rem; font-family:inherit; color:var(--text);
}
.faq-search input:focus{ border-color:var(--brand-aa); outline:none; }

.faq-no-result{
  padding:1.25rem 1.5rem; background:var(--paper-2); border-left:4px solid var(--brand-deep);
  font-size:.92rem; margin-bottom:2rem;
}
.faq-no-result a{ color:var(--brand-aa); text-decoration:underline; font-weight:700; }

.faq-topics{ display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); gap:.75rem; margin-bottom:3rem; }
.faq-topic-card{
  padding:1rem 1.1rem; background:var(--paper-2); border:1px solid var(--rule);
  font-weight:700; font-size:.88rem; color:var(--heading); text-align:center;
  transition:background var(--dur-fast) var(--ease), color var(--dur-fast) var(--ease);
}
.faq-topic-card:hover, .faq-topic-card:focus-visible{ background:var(--brand-aa); color:#fff; }

.faq-category{ margin-bottom:3rem; scroll-margin-top:6rem; }
.faq-category__head{ display:flex; align-items:baseline; justify-content:space-between; gap:1rem; padding-bottom:.75rem; border-bottom:2px solid var(--rule); margin-bottom:.5rem; flex-wrap:wrap; }
.faq-category__title{ font-size:1.15rem; font-weight:900; color:var(--heading); margin:0; }
.faq-category__more{ font-size:.85rem; font-weight:700; color:var(--brand-aa); white-space:nowrap; }

.faq-fallback-cta{
  text-align:center; padding:3rem 1.5rem; background:var(--paper-2); border:1px solid var(--rule); margin-top:2rem;
}
.faq-fallback-cta p{ font-weight:700; color:var(--heading); margin-bottom:1rem; }
</style>
