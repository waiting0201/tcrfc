<script setup lang="ts">
// app/components/content/CmsPageBand.vue — 靜態頁「主內文」改由頁面管理（B1）區塊渲染時的外框（稽核 B-1／B-2）
// 取代頁面原本寫死的主內文 band；hero／麵包屑／CTA 由各頁自己保留。`useCmsPage()` 的 `active`／`blocks`／`hasZhFallback` 直接餵進來。
import type { PageBlockNode } from '#shared/utils/page-blocks'

defineProps<{ blocks: PageBlockNode[], label: string, zhFallback?: boolean }>()
</script>

<template>
  <LocaleFallbackNotice v-if="zhFallback" partial />
  <section class="band cms-band" :aria-label="label">
    <div class="band-inner container">
      <ContentPageBlocks :blocks="blocks" />
    </div>
  </section>
</template>

<style>
.cms-band{ padding-block:clamp(3.5rem,6vw,6rem); }
</style>
