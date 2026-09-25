<script setup lang="ts">
// app/components/news/NewsCategoryTabs.vue — 07 單元分類導覽連結（8 頁共用：
// club／camps-events／community／international／match／academy／media／player-stories）
//
// 對應 mockup 每個分類頁重複出現的 <nav class="cat-tabs"><a>…</a></nav> 區塊
// （純連結，非 news/index 的 <button data-tab> 篩選鈕——那個不共用，見 news/index.vue）。
defineProps<{ active: string }>()

// S1-13：8 個分類頁共用這支元件，一次修好全部生效，見 news/NewsCard.vue 同理。
const { lp } = useLocale()
</script>

<template>
  <nav class="cat-tabs" aria-label="新聞分類">
    <a
      v-for="cat in NEWS_CATEGORIES"
      :key="cat.code"
      :href="lp(`/zh/news/${cat.code}/`)"
      :aria-current="active === cat.code ? 'page' : undefined"
    >{{ cat.label }}</a>
  </nav>
</template>
