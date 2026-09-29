<script setup lang="ts">
// app/pages/zh/news/player-stories.vue — 由 site/src/pages/zh/news/player-stories/index.html 轉來（S0-9 靜態頁搬遷）
//
// S1-17：接上真實 API（GET /api/v1/{club}/news?category=player-stories）。🔴 種子資料的
// NEWS_CATEGORY_MAP（db/seed/generate-club-seed-sql.py）沒有把任何一筆舊站新聞歸類到
// `player-stories`，這個分類目前必然是 0 篇——跟 mockup 原本的「空狀態頁面」文案一致，
// 差別只在於現在是「真的查詢後發現 0 篇」，不是寫死的假設。沒有 NewsFilterForm（比照
// app/components/news/NewsFilterForm.vue 檔頭既有慣例：資料量太少不需要年月／關鍵字篩選）。
definePageMeta({ nav: "news", unit: "07" })

const config = useRuntimeConfig()
const club = config.public.club
const { locale, lp } = useLocale()
const { data } = await useFetch(`/api/backend/${club}/news`, {
  query: { category: 'player-stories', pageSize: 200, lang: locale.value },
})
const articles = computed(() => data.value?.items ?? [])

// BW-C1（品牌外洩全站盤點）：title／description 原本固定寫死「台中磐石足球俱樂部」，
// 分類導覽改用共用元件 NewsCategoryTabs（原本手刻複製一份，含寫死的「7.3 學院新聞」，
// 該元件本輪已改為依俱樂部動態組字，見 app/components/news/NewsCategoryTabs.vue）。
useSeoMeta({
  title: computed(() => `球員故事 Player Stories｜新聞 News｜${getClubAssets(club).nameZh}`),
  description: computed(() => `${getClubAssets(club).shortNameZh}球員故事與人物專訪，內容尚待客戶提供，目前為空狀態頁面。`),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/news/')">新聞 News</a></li>
      <li aria-current="page">球員故事</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-news.jpg" alt="" width="1920" height="1279">
  <div class="container">
    <p class="page-hero__eyebrow">7.4 Player Stories</p>
    <h1>球員故事<span class="en">Player Stories</span></h1>
    <p class="page-hero__lede">球員的成長歷程與心路歷程專訪，內容陸續建置中。</p>
  </div>
</section>

<section class="band" aria-labelledby="cat-news-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="cat-news-title">球員故事 文章列表</h2>

    <div class="news-toolbar">
      <NewsCategoryTabs active="player-stories" />
    </div>

    <div v-if="articles.length > 0" class="news-list-grid">
      <NewsCard v-for="a in articles" :key="a.slug" :article="a" />
    </div>
    <div v-else class="news-empty">
      <p style="margin-bottom:1rem">本分類目前尚無已發布之文章。</p>
    </div>
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
