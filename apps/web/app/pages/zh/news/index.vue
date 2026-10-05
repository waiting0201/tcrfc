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
definePageMeta({ nav: 'news', unit: '07', enReady: true })

const config = useRuntimeConfig()
const club = config.public.club
// BW-C1（品牌外洩全站盤點）：title／description／hero lede 原本固定寫死「台中磐石
// 足球俱樂部」，是本輪全站掃描才發現的既有缺口（本頁其餘內容——分類鈕、文章列表、
// 篩選——本來就依 `club` 動態打 API，只有這三處文案沒有跟著動）。改讀既有的
// getClubAssets()，不需要新增 club-copy.ts 文案鍵。
const clubNameZh = computed(() => getClubAssets(club).nameZh)

// S1-13：lang 跟隨目前路由語系，見 app/pages/zh/schedule.vue 同一處的說明。
const { locale, lp, isEn, tx } = useLocale()
const { data } = await useFetch(`/api/backend/${club}/news`, {
  query: { pageSize: 200, lang: locale.value },
})

const articles = computed(() => data.value?.items ?? [])
const totalCount = computed(() => data.value?.totalCount ?? 0)
// S1-17 修正：原本是 slice(0,3)，沒有真的檢查 isFeatured（規劃書 3.7「精選置頂（最多 3 則）」
// 指的是後台可指定的精選文章，不是單純取列表前 3 篇）。邏輯比照 app/pages/zh/index.vue
// 「S1-14：最新消息」同一段——精選優先，不足 3 篇時用最新發布時間補滿。
const featured = computed(() =>
  articles.value
    .slice()
    .sort((a, b) => {
      if (a.isFeatured !== b.isFeatured) return a.isFeatured ? -1 : 1
      return (b.publishedAt ?? '').localeCompare(a.publishedAt ?? '')
    })
    .slice(0, 3),
)
const years = computed(() => newsDistinctYears(articles.value))
// S1-17 新增：標籤篩選選項（規劃書 3.7「標籤篩選」）。
const tags = computed(() => newsDistinctTags(articles.value))
const ALL_MONTHS = ['01', '02', '03', '04', '05', '06', '07', '08', '09', '10', '11', '12']

const activeCat = ref('all')
const tag = ref('')
const year = ref('')
const month = ref('')
const search = ref('')

// 🔴 逐字比對 mockup：分類篩選鈕只有 8 個（缺 7.8 媒體專區，見 site/dist/zh/news/index.html
// data-tab 清單），NewsCategoryTabs（<a> 連結版）用的 NEWS_CATEGORIES 8 項全數列出才對，
// 這裡的篩選鈕刻意排除 media——是 mockup 本身的既有落差，不是這次搬遷漏做。
const filterTabCategories = computed(() => NEWS_CATEGORIES.filter((c) => c.code !== 'media'))

useSeoMeta({
  title: computed(() => isEn.value ? getNewsIndexSeoEn(totalCount.value).title : `最新消息 News & Stories｜${clubNameZh.value}`),
  description: computed(() => isEn.value ? getNewsIndexSeoEn(totalCount.value).description : `${clubNameZh.value}新聞中心：俱樂部新聞、比賽報導、國際交流、營隊活動與社區公益，${totalCount.value} 篇真實報導依分類、年月與關鍵字瀏覽。`),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('新聞 News', 'News') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-news.jpg')" width="1920" height="1279" />
  <div class="container">
    <p class="page-hero__eyebrow">07 News &amp; Stories</p>
    <h1>{{ tx('最新消息', 'News & Stories') }}<span v-if="!isEn" class="en">News &amp; Stories</span></h1>
    <p v-if="isEn" class="page-hero__lede">{{ getNewsIndexHeroEn(totalCount).lede }}</p>
    <p v-else class="page-hero__lede">俱樂部公告、比賽報導、國際交流、營隊活動與社區公益，統一於新聞中心發布。目前共收錄 {{ totalCount }} 篇真實報導。</p>
  </div>
</section>

<section class="band" aria-labelledby="featured-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">{{ tx('FEATURED · 精選置頂', 'FEATURED') }}</p>
        <h2 class="section-title" id="featured-title">{{ tx('焦點報導', 'Featured stories') }}</h2>
      </div>
      <p class="section-lede">{{ tx('俱樂部近期最受關注的三則報導。', 'The three stories getting the most attention at the club right now.') }}</p>
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
        <h2 class="section-title" id="all-news-title" style="color:#fff">{{ tx('全部新聞', 'All news') }}</h2>
      </div>
      <p class="section-lede" style="color:var(--muted-dark)">{{ isEn ? `Browse all ${totalCount} reports by category, month or keyword.` : `依分類、年月或關鍵字瀏覽全部 ${totalCount} 篇報導。` }}</p>
    </div>

    <div class="news-toolbar">
      <nav class="cat-tabs" :aria-label="tx('新聞分類篩選', 'Filter news by category')">
        <button type="button" data-tab="all" :aria-pressed="activeCat === 'all'" @click="activeCat = 'all'">{{ tx('全部', 'All') }}</button>
        <button
          v-for="cat in filterTabCategories"
          :key="cat.code"
          type="button"
          :data-tab="cat.code"
          :aria-pressed="activeCat === cat.code"
          @click="activeCat = cat.code"
        >{{ isEn ? newsCategoryTabLabelEn(cat.code) : newsCategoryTabLabel(cat.code, club) }}</button>
      </nav>
      <NewsFilterForm v-model:tag="tag" v-model:year="year" v-model:month="month" v-model:search="search" :tags="tags" :years="years" :months="ALL_MONTHS" />
    </div>

    <LocaleFallbackNotice v-if="isEn && hasFallbackLocale(data)" partial />

    <NewsListBody :articles="articles" :active-cat="activeCat" :tag="tag" :year="year" :month="month" :search="search" dark />
  </div>
</section>
</template>
