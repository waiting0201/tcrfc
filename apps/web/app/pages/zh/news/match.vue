<script setup lang="ts">
// app/pages/zh/news/match.vue — 由 site/src/pages/zh/news/match/index.html 轉來（S0-9 資料驅動頁搬遷）
//
// 6 個 news 分類頁共用同一份 client 篩選行為，已抽成 NewsListBody／NewsFilterForm／
// NewsCategoryTabs 三個共用元件（docs/13-blue-whale-site.md §6，務必做成元件不要複製六次）。
// 🔴 SSR 階段打真實 API（分類已在查詢時過濾），不做 client-only 抓取。
definePageMeta({ nav: 'news', unit: '07', enReady: true, enReadyBw: true })

const config = useRuntimeConfig()
const club = config.public.club
const clubKey: 'tcrfc' | 'bw' = club === 'bw' ? 'bw' : 'tcrfc'
const { facts } = useSiteFacts(clubKey)

// S1-13：lang 跟隨目前路由語系，見 app/pages/zh/schedule.vue 同一處的說明。
const { locale, lp, isEn, tx } = useLocale()
const { data } = await useFetch(`/api/backend/${club}/news`, {
  query: { category: 'match', pageSize: 200, lang: locale.value },
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
  title: computed(() => isEn.value ? getNewsCategorySeoEn('match', articles.value.length, clubKey).title : (club === 'bw' ? `比賽報導 Match Reports｜新聞 News｜${getClubAssets(club).nameZh}` : "比賽報導 Match Reports｜新聞 News｜台中磐石足球俱樂部")),
  description: computed(() => isEn.value ? getNewsCategorySeoEn('match', articles.value.length, clubKey).description : (club === 'bw'
    ? `${getClubAssets(club).shortNameZh}各級隊伍完整比賽報導，共 ${articles.value.length} 篇真實報導。`
    : "台中磐石各級隊伍完整比賽報導，含企甲聯賽、乙級聯賽、總統盃與熱身賽戰報，共 50 篇真實賽後報導。")),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/news/')">{{ tx('新聞 News', 'News') }}</a></li>
      <li aria-current="page">{{ tx('比賽報導', 'Match Reports') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-news.jpg')" width="1920" height="1279" />
  <div class="container">
    <p class="page-hero__eyebrow">7.2 Match Reports</p>
    <h1>{{ tx('比賽報導', 'Match Reports') }}<span v-if="!isEn" class="en">Match Reports</span></h1>
    <p v-if="isEn" class="page-hero__lede">{{ getNewsCategoryHeroEn('match', clubKey).lede }}</p>
    <p v-else class="page-hero__lede">{{ getMatchReportsLedeZh(club, facts) }}</p>
  </div>
</section>

<section class="band" aria-labelledby="cat-news-title" data-news-list>
  <div class="band-inner container">
    <h2 class="visually-hidden" id="cat-news-title">{{ tx('比賽報導 文章列表', 'Match Reports articles') }}</h2>

    <div class="news-toolbar">
      <NewsCategoryTabs active="match" />
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
