<script setup lang="ts">
// app/pages/zh/news/international.vue — 由 site/src/pages/zh/news/international/index.html 轉來（S0-9 資料驅動頁搬遷）
//
// 6 個 news 分類頁共用同一份 client 篩選行為，已抽成 NewsListBody／NewsFilterForm／
// NewsCategoryTabs 三個共用元件（docs/13-blue-whale-site.md §6，務必做成元件不要複製六次）。
// 🔴 SSR 階段打真實 API（分類已在查詢時過濾），不做 client-only 抓取。
definePageMeta({ nav: 'news', unit: '07', enReady: true, enReadyBw: true })

const config = useRuntimeConfig()
const club = config.public.club
const clubKey: 'tcrfc' | 'bw' = club === 'bw' ? 'bw' : 'tcrfc'

// S1-13：lang 跟隨目前路由語系，見 app/pages/zh/schedule.vue 同一處的說明。
const { locale, lp, isEn, tx } = useLocale()
const { data } = await useFetch(`/api/backend/${club}/news`, {
  query: { category: 'international', pageSize: 200, lang: locale.value },
})

const articles = computed(() => data.value?.items ?? [])
const years = computed(() => newsDistinctYears(articles.value))
const months = computed(() => newsDistinctMonths(articles.value))
// S1-17 新增：標籤篩選選項（規劃書 3.7「標籤篩選」）。
const tags = computed(() => newsDistinctTags(articles.value))

const tag = ref('')
const year = ref('')
const month = ref('')
const search = ref('')

useSeoMeta({
  title: computed(() => isEn.value ? getNewsCategorySeoEn('international', articles.value.length, clubKey).title : (club === 'bw' ? `國際動態 International｜新聞 News｜${getClubAssets(club).nameZh}` : "國際動態 International｜新聞 News｜台中磐石足球俱樂部")),
  description: computed(() => isEn.value ? getNewsCategorySeoEn('international', articles.value.length, clubKey).description : (club === 'bw'
    ? `${getClubAssets(club).shortNameZh}國際交流動態，共 ${articles.value.length} 篇真實報導。`
    : "台中磐石國際交流動態：海外球會合作備忘錄、球員海外受訓與轉會消息，共 12 篇真實報導。")),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/news/')">{{ tx('新聞 News', 'News') }}</a></li>
      <li aria-current="page">{{ tx('國際動態', 'International') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-news.jpg')" width="1920" height="1279" />
  <div class="container">
    <p class="page-hero__eyebrow">7.5 International</p>
    <h1>{{ tx('國際動態', 'International') }}<span v-if="!isEn" class="en">International</span></h1>
    <p v-if="isEn" class="page-hero__lede">{{ getNewsCategoryHeroEn('international', clubKey).lede }}</p>
    <p v-else-if="club !== 'bw'" class="page-hero__lede">海外球會合作、球員海外受訓與轉會——台中磐石選手放眼世界的第一手紀錄，呼應「Global Pathways 國際發展」。</p>
    <p v-else class="page-hero__lede">海外球會合作、球員海外受訓與轉會——台中藍鯨選手放眼世界的第一手紀錄。</p>
  </div>
</section>

<section class="band" aria-labelledby="cat-news-title" data-news-list>
  <div class="band-inner container">
    <h2 class="visually-hidden" id="cat-news-title">{{ tx('國際動態 文章列表', 'International articles') }}</h2>

    <div class="news-toolbar">
      <NewsCategoryTabs active="international" />
      <NewsFilterForm v-model:tag="tag" v-model:year="year" v-model:month="month" v-model:search="search" :tags="tags" :years="years" :months="months" />
    </div>

    <LocaleFallbackNotice v-if="isEn && hasFallbackLocale(data)" partial />

    <NewsListBody :articles="articles" active-cat="all" :tag="tag" :year="year" :month="month" :search="search" />
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
