<script setup lang="ts">
// app/pages/zh/news/community.vue — 由 site/src/pages/zh/news/community/index.html 轉來（S0-9 資料驅動頁搬遷）
//
// 6 個 news 分類頁共用同一份 client 篩選行為，已抽成 NewsListBody／NewsFilterForm／
// NewsCategoryTabs 三個共用元件（docs/13-blue-whale-site.md §6，務必做成元件不要複製六次）。
// 🔴 SSR 階段打真實 API（分類已在查詢時過濾），不做 client-only 抓取。
definePageMeta({ nav: 'news', unit: '07' })

const config = useRuntimeConfig()
const club = config.public.club

// S1-13：lang 跟隨目前路由語系，見 app/pages/zh/schedule.vue 同一處的說明。
const { locale, lp } = useLocale()
const { data } = await useFetch(`/api/backend/${club}/news`, {
  query: { category: 'community', pageSize: 200, lang: locale.value },
})

const articles = computed(() => data.value?.items ?? [])
const years = computed(() => newsDistinctYears(articles.value))
const months = computed(() => newsDistinctMonths(articles.value))

const year = ref('')
const month = ref('')
const search = ref('')

useSeoMeta({
  title: "社區活動 Community｜新聞 News｜台中磐石足球俱樂部",
  description: "台中磐石社區公益與地方合作活動紀錄，共 3 篇真實報導，呼應「Community 社區共好」核心價值。",
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/news/')">新聞 News</a></li>
      <li aria-current="page">社區活動</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-news.jpg" alt="" width="1920" height="1279">
  <div class="container">
    <p class="page-hero__eyebrow">7.7 Community</p>
    <h1>社區活動<span class="en">Community</span></h1>
    <p class="page-hero__lede">公益捐贈、學校交流、地方政府合作——台中磐石在球場之外的社區共好行動。</p>
  </div>
</section>

<section class="band" aria-labelledby="cat-news-title" data-news-list>
  <div class="band-inner container">
    <h2 class="visually-hidden" id="cat-news-title">社區活動 文章列表</h2>

    <div class="news-toolbar">
      <NewsCategoryTabs active="community" />
      <NewsFilterForm v-model:year="year" v-model:month="month" v-model:search="search" :years="years" :months="months" />
    </div>

    <NewsListBody :articles="articles" active-cat="all" :year="year" :month="month" :search="search" />
  </div>
</section>
</template>

<style>
/* ============================================================
   News listing components (07 NEWS & STORIES)
   Used on: news/ hub + all 7.x category pages + 7.8 media.
   Repeats identically across 9 pages — candidate for promotion
   into shared tcrfc.css, see build report.
   ============================================================ */
.news-toolbar{ display:flex; flex-direction:column; gap:1.25rem; margin-bottom:1.75rem; }
</style>
