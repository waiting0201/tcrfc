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
definePageMeta({ nav: '', unit: '12' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club

const siteName = computed(() => getClubAssets(club).nameZh)

const { categories } = useFaqCategories(locale.value)
const { faqs, totalCount } = useFaqList(club, locale.value)

useSeoMeta({
  title: () => `常見問題 FAQ｜${siteName.value}`,
  description: () =>
    `${siteName.value}常見問題集，依主題分類整理，涵蓋加入球隊、學院招生、課程與營隊報名、費用與退費、試訓、國際發展、女子足球、球迷會與商品、合作與贊助等主題，目前共收錄 ${totalCount.value} 題。`,
})

// 每題只指派給第一個相符的分類（分類已依 sortOrder 排序），確保單題深層連結
// 全頁唯一，見上方檔頭說明。
const faqsByCategory = computed(() => {
  const map = new Map<string, typeof faqs.value>()
  const assigned = new Set<string>()
  for (const cat of categories.value) {
    const items = faqs.value.filter((f) => !assigned.has(f.id) && f.categorySlugs.includes(cat.slug))
    items.forEach((f) => assigned.add(f.id))
    map.set(cat.slug, items)
  }
  return map
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
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li aria-current="page">常見問題</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">12</span>
  <div class="container">
    <p class="page-hero__eyebrow">12 FAQ</p>
    <h1>常見問題<span class="en">FAQ</span></h1>
    <p class="page-hero__lede">依主題分類整理的常見問題，可直接搜尋關鍵字，或分享單題連結給需要的人。目前共收錄 {{ totalCount }} 題。</p>
  </div>
</section>

<section class="band faq-band" aria-labelledby="faq-title">
  <div class="band-inner container">
    <h2 id="faq-title" class="visually-hidden">常見問題搜尋與分類</h2>

    <div class="faq-search">
      <label class="visually-hidden" for="faq-search-input">搜尋常見問題</label>
      <input
        id="faq-search-input"
        v-model="search"
        type="search"
        placeholder="輸入關鍵字搜尋問題與答案…"
        autocomplete="off"
      >
    </div>

    <p v-if="noResult" class="faq-no-result">
      沒有找到符合的問題。歡迎直接
      <a :href="lp('/zh/join/general/')">聯絡我們</a>，我們會盡快回覆你的問題。
    </p>

    <nav v-if="!isSearching" class="faq-topics" aria-label="常見問題主題">
      <a v-for="cat in categories" :key="cat.id" class="faq-topic-card" :href="`#topic-${cat.slug}`">{{ cat.name }}</a>
    </nav>

    <div class="faq-categories">
      <section
        v-for="cat in categories"
        v-show="!isSearching || (visibleByCategory.get(cat.slug)?.length ?? 0) > 0"
        :id="`topic-${cat.slug}`"
        :key="cat.id"
        class="faq-category"
      >
        <div class="faq-category__head">
          <h2 class="faq-category__title">{{ cat.name }}</h2>
          <a v-if="dedicatedPageFor(cat.slug)" class="faq-category__more" :href="lp(dedicatedPageFor(cat.slug) ?? '/zh/faq/')">獨立主題頁 →</a>
        </div>
        <FaqAccordion :faqs="visibleByCategory.get(cat.slug) ?? []" :club="club" />
      </section>
    </div>

    <div class="faq-fallback-cta">
      <p>沒有找到你要的答案？</p>
      <a class="btn btn--primary" :href="lp('/zh/join/general/')">聯絡我們 10.7</a>
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
