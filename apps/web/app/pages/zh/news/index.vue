<script setup lang="ts">
// app/pages/zh/news/index.vue — 由 site/src/pages/zh/news/index.html 轉來（S0-9 資料驅動頁搬遷）
//
// 新聞中心首頁：焦點報導（最新 3 篇）＋ 全部新聞（分類鈕＋年月＋關鍵字篩選＋載入更多）。
// 全部新聞區塊與另外 5 個分類頁共用 NewsListBody／NewsFilterForm；分類鈕在這頁是
// <button data-tab> 篩選（不像分類頁是 <a> 連結），只有這一頁用，故不抽成獨立元件
// （site/tools/README.md 與 docs/13 §6 只要求「共用腳本」抽成元件，這裡分類鈕
// markup 全站只出現一次，沒有複製六次的問題）。
// 🔴 SSR 階段打真實 API，一次抓全部文章（pageSize=200，83 篇量級單頁載入可接受，
// 原始 mockup script 註解本來就這樣寫）。
definePageMeta({ nav: 'news', unit: '07' })

const config = useRuntimeConfig()
const club = config.public.club

// S1-13：lang 跟隨目前路由語系，見 app/pages/zh/schedule.vue 同一處的說明。
const { locale, lp } = useLocale()
const { data } = await useFetch(`/api/backend/${club}/news`, {
  query: { pageSize: 200, lang: locale.value },
})

const articles = computed(() => data.value?.items ?? [])
const totalCount = computed(() => data.value?.totalCount ?? 0)
const featured = computed(() => articles.value.slice(0, 3))
const years = computed(() => newsDistinctYears(articles.value))
const ALL_MONTHS = ['01', '02', '03', '04', '05', '06', '07', '08', '09', '10', '11', '12']

const activeCat = ref('all')
const year = ref('')
const month = ref('')
const search = ref('')

// 🔴 逐字比對 mockup：分類篩選鈕只有 8 個（缺 7.8 媒體專區，見 site/dist/zh/news/index.html
// data-tab 清單），NewsCategoryTabs（<a> 連結版）用的 NEWS_CATEGORIES 8 項全數列出才對，
// 這裡的篩選鈕刻意排除 media——是 mockup 本身的既有落差，不是這次搬遷漏做。
const filterTabCategories = computed(() => NEWS_CATEGORIES.filter((c) => c.code !== 'media'))

useSeoMeta({
  title: '最新消息 News & Stories｜台中磐石足球俱樂部',
  description: `台中磐石足球俱樂部新聞中心：俱樂部新聞、比賽報導、國際交流、營隊活動與社區公益，${totalCount.value} 篇真實報導依分類、年月與關鍵字瀏覽。`,
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li aria-current="page">新聞 News</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-news.jpg" alt="" width="1920" height="1279">
  <div class="container">
    <p class="page-hero__eyebrow">07 News &amp; Stories</p>
    <h1>最新消息<span class="en">News &amp; Stories</span></h1>
    <p class="page-hero__lede">俱樂部公告、比賽報導、國際交流、營隊活動與社區公益，統一於新聞中心發布。目前共收錄 {{ totalCount }} 篇真實報導。</p>
  </div>
</section>

<section class="band" aria-labelledby="featured-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FEATURED · 精選置頂</p>
        <h2 class="section-title" id="featured-title">焦點報導</h2>
      </div>
      <p class="section-lede">俱樂部近期最受關注的三則報導。</p>
    </div>
    <div class="featured-grid">
      <NewsCard v-for="a in featured" :key="a.slug" :article="a" />
    </div>
  </div>
</section>

<section class="band grain grain--2" aria-labelledby="all-news-title" data-news-list>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker" style="color:var(--brand)">ALL STORIES</p>
        <h2 class="section-title" id="all-news-title" style="color:#fff">全部新聞</h2>
      </div>
      <p class="section-lede" style="color:var(--muted-dark)">依分類、年月或關鍵字瀏覽全部 {{ totalCount }} 篇報導。</p>
    </div>

    <div class="news-toolbar">
      <nav class="cat-tabs" aria-label="新聞分類篩選">
        <button type="button" data-tab="all" :aria-pressed="activeCat === 'all'" @click="activeCat = 'all'">全部</button>
        <button
          v-for="cat in filterTabCategories"
          :key="cat.code"
          type="button"
          :data-tab="cat.code"
          :aria-pressed="activeCat === cat.code"
          @click="activeCat = cat.code"
        >{{ cat.label }}</button>
      </nav>
      <NewsFilterForm v-model:year="year" v-model:month="month" v-model:search="search" :years="years" :months="ALL_MONTHS" />
    </div>

    <NewsListBody :articles="articles" :active-cat="activeCat" :year="year" :month="month" :search="search" dark />
  </div>
</section>
</template>
