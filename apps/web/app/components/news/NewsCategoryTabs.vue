<script setup lang="ts">
// app/components/news/NewsCategoryTabs.vue — 07 單元分類導覽連結（8 頁共用：
// club／camps-events／community／international／match／academy／media／player-stories）
//
// 對應 mockup 每個分類頁重複出現的 <nav class="cat-tabs"><a>…</a></nav> 區塊
// （純連結，非 news/index 的 <button data-tab> 篩選鈕——那個不共用，見 news/index.vue）。
defineProps<{ active: string }>()

// S1-13：8 個分類頁共用這支元件，一次修好全部生效，見 news/NewsCard.vue 同理。
const { lp, isEn, tx } = useLocale()

// BW-C1（品牌外洩全站盤點）：`NEWS_CATEGORIES` 的 'academy' 項目 label 固定寫死
// 「7.3 學院新聞」，藍鯨依 docs/13-blue-whale-site.md §3 一律稱「青年隊」——本輪盤點
// 才發現，因為這個共用元件先前從未被抓到過（8 頁裡有 6 頁靠這個元件顯示分類 tab，
// 另外 2 頁——academy.vue／player-stories.vue——手刻同一份 markup，未共用元件，
// 那兩頁已改為改用本元件）。改讀單一來源 newsCategoryTabLabel()（app/utils/news.ts，
// news/index.vue 的 <button data-tab> 篩選鈕也呼叫同一支函式，不要各自算一次）。
const config = useRuntimeConfig()
function tabLabel(cat: { code: string; label: string }): string {
  // 英文版（主站 /en/）：分類名稱照 docs/06 §1.1 對照表（shared/utils/club-copy-en-sched.ts）
  if (isEn.value) return newsCategoryTabLabelEn(cat.code)
  return newsCategoryTabLabel(cat.code, config.public.club)
}
</script>

<template>
  <nav class="cat-tabs" :aria-label="tx('新聞分類', 'News categories')">
    <a
      v-for="cat in NEWS_CATEGORIES"
      :key="cat.code"
      :href="lp(`/zh/news/${cat.code}/`)"
      :aria-current="active === cat.code ? 'page' : undefined"
    >{{ tabLabel(cat) }}</a>
  </nav>
</template>
