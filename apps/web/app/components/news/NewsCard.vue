<script setup lang="ts">
// app/components/news/NewsCard.vue — 新聞卡片（07 單元共用，S0-9 資料驅動頁搬遷）
//
// 對應 site/src/pages/zh/news/*/index.html 裡重複出現的
// `<a class="news-card clip-card" ...>` 區塊（焦點報導／全部新聞／分類列表共用同一種卡片，
// 首頁 zh/index.vue 的 news-card--feature/--sml/--wide 變體不在此範圍，那是另一個頁面的區塊）。
// DOM 結構、class 一律比照 mockup 不動；data-cat/data-year/data-month/data-title
// 四個屬性原本由 build.mjs 從 JSON 算好烤進 HTML，這裡改成從 API 回傳的
// ArticleListItemDto 即時算，值域與算法逐一對應 app/utils/news.ts。
//
// 🔴 S0-9e：href 原本寫死 /zh/news/article/（83 篇卡片全部連到同一篇範本文章），
// 改為依 article.slug 動態組出逐篇網址 /zh/news/{slug}/，對應新增的動態路由
// app/pages/zh/news/[slug]/index.vue。
interface NewsCardArticle {
  slug: string
  categoryCode: string
  categoryName: string | null
  title: string | null
  publishedAt: string | null
}

const props = withDefaults(defineProps<{ article: NewsCardArticle; hidden?: boolean }>(), { hidden: false })

const cover = computed(() => hasNewsCover(props.article.slug))
</script>

<template>
  <a
    class="news-card clip-card"
    :href="`/zh/news/${article.slug}/`"
    :hidden="hidden || undefined"
    :data-cat="article.categoryCode"
    :data-year="newsYearAttr(article.publishedAt)"
    :data-month="newsMonthAttr(article.publishedAt)"
    :data-title="newsTitleAttr(article.title)"
  >
    <div :class="['news-card__media', { 'news-card__media--noimg': !cover }]">
      <span class="news-card__tag">{{ article.categoryName }}</span>
      <img v-if="cover" :src="newsCoverSrc(article.slug)" alt="" loading="lazy" width="1600" height="1067">
      <img v-else class="news-card__media-mark" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" loading="lazy" width="64" height="67">
    </div>
    <div class="news-card__body">
      <p class="news-card__meta"><time :datetime="newsIsoDate(article.publishedAt)">{{ newsSlashDate(article.publishedAt) }}</time></p>
      <p class="news-card__title">{{ article.title }}</p>
    </div>
  </a>
</template>
