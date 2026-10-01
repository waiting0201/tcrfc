<script setup lang="ts">
// app/components/content/PageBlocks.vue — B1 區塊編輯器內容的前台渲染（純文字型區塊，見 shared/utils/page-blocks.ts）
// 全部以 `{{ }}` 渲染，不 v-html；連結已由 `safeBlockHref` 限定站內路徑或 https。
import type { PageBlockNode } from '#shared/utils/page-blocks'

defineProps<{ blocks: PageBlockNode[] }>()
const { lp } = useLocale()
const hrefOf = (h: string) => (h.startsWith('/zh/') || h.startsWith('/en/') ? lp(h) : h)
</script>

<template>
  <div class="pb">
    <template v-for="(b, i) in blocks" :key="i">
      <div v-if="b.kind === 'text'" class="prose">
        <p v-for="(p, pi) in b.paragraphs" :key="pi">{{ p }}</p>
      </div>
      <blockquote v-else-if="b.kind === 'quote'" class="pb-quote">
        <p>{{ b.text }}</p>
        <footer v-if="b.attribution">— {{ b.attribution }}</footer>
      </blockquote>
      <div v-else-if="b.kind === 'stats'" class="values-grid pb-stats">
        <div v-for="(it, si) in b.items" :key="si" class="value-card">
          <p class="value-card__en">{{ it.value }}</p>
          <p class="value-card__desc">{{ it.label }}</p>
        </div>
      </div>
      <ol v-else-if="b.kind === 'steps'" class="pb-list">
        <li v-for="(it, si) in b.items" :key="si"><strong>{{ it.title }}</strong><span v-if="it.description">　{{ it.description }}</span></li>
      </ol>
      <ol v-else-if="b.kind === 'timeline'" class="timeline">
        <li v-for="(it, ti) in b.items" :key="ti" class="timeline-item">
          <p class="timeline-item__year">{{ it.date }}</p>
          <p class="timeline-item__title">{{ it.title }}</p>
          <p v-if="it.description" class="timeline-item__desc">{{ it.description }}</p>
        </li>
      </ol>
      <div v-else-if="b.kind === 'table'" class="sh-table-wrap">
        <table class="sh-stats-table">
          <thead><tr><th v-for="(h, hi) in b.headers" :key="hi" scope="col">{{ h }}</th></tr></thead>
          <tbody><tr v-for="(r, ri) in b.rows" :key="ri"><td v-for="(c, ci) in r" :key="ci">{{ c }}</td></tr></tbody>
        </table>
      </div>
      <p v-else-if="b.kind === 'cta'" class="pb-cta">
        <span v-if="b.text">{{ b.text }}　</span>
        <a class="btn btn--primary btn--sm" :href="hrefOf(b.href)" :rel="b.href.startsWith('https://') ? 'noopener' : undefined">{{ b.label }}</a>
      </p>
    </template>
  </div>
</template>
