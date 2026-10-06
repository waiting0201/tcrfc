<script setup lang="ts">
// app/components/content/PageBlocks.vue — B1 區塊編輯器內容的前台渲染（純文字型區塊，見 shared/utils/page-blocks.ts）
// 全部以 `{{ }}` 渲染，不 v-html；連結已由 `safeBlockHref` 限定站內路徑或 https。
// 影音嵌入只輸出 normalize 階段白名單產生的 nocookie 網址（YouTube／Vimeo），iframe 加 sandbox 與 referrerpolicy。
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
      <div v-else-if="b.kind === 'textImage'" class="pb-textimage" :class="{ 'pb-textimage--right': b.imagePosition === 'right', 'pb-textimage--noimg': !b.image }">
        <figure v-if="b.image" class="pb-figure">
          <img :src="b.image.src" :alt="b.image.alt" :width="b.image.width ?? undefined" :height="b.image.height ?? undefined" loading="lazy" decoding="async">
        </figure>
        <div v-if="b.paragraphs.length" class="prose">
          <p v-for="(p, pi) in b.paragraphs" :key="pi">{{ p }}</p>
        </div>
      </div>
      <ul v-else-if="b.kind === 'gallery'" class="pb-gallery">
        <li v-for="(img, gi) in b.images" :key="gi">
          <img :src="img.src" :alt="img.alt" :width="img.width ?? undefined" :height="img.height ?? undefined" loading="lazy" decoding="async">
        </li>
      </ul>
      <figure v-else-if="b.kind === 'video'" class="pb-video">
        <div class="pb-video__frame">
          <iframe
            :src="b.src"
            :title="b.caption || (b.provider === 'vimeo' ? 'Vimeo video' : 'YouTube video')"
            loading="lazy"
            allowfullscreen
            referrerpolicy="strict-origin-when-cross-origin"
            sandbox="allow-scripts allow-same-origin allow-presentation allow-popups"
            allow="fullscreen; picture-in-picture"
          />
        </div>
        <figcaption v-if="b.caption">{{ b.caption }}</figcaption>
      </figure>
      <div v-else-if="b.kind === 'faq'" class="pb-faq">
        <details v-for="(it, fi) in b.items" :key="fi" class="pb-faq__item">
          <summary>{{ it.question }}</summary>
          <p>{{ it.answer }}</p>
        </details>
      </div>
      <p v-else-if="b.kind === 'file'" class="pb-file">
        <a class="btn btn--dark btn--sm" :href="hrefOf(b.href)" download :rel="b.href.startsWith('https://') ? 'noopener' : undefined">{{ b.label }}</a>
      </p>
      <p v-else-if="b.kind === 'cta'" class="pb-cta">
        <span v-if="b.text">{{ b.text }}　</span>
        <a class="btn btn--primary btn--sm" :href="hrefOf(b.href)" :rel="b.href.startsWith('https://') ? 'noopener' : undefined">{{ b.label }}</a>
      </p>
    </template>
  </div>
</template>

<style>
.pb-textimage, .pb-gallery, .pb-video, .pb-faq, .pb-file{ margin-block:1.5rem; }
.pb-textimage{ display:grid; grid-template-columns:minmax(0,1fr) minmax(0,1fr); gap:clamp(1.25rem,3vw,2.5rem); align-items:start; }
.pb-textimage--right .pb-figure{ order:2; }
.pb-textimage--noimg{ grid-template-columns:minmax(0,1fr); }
.pb-figure{ margin:0; }
.pb-figure img, .pb-gallery img{ display:block; width:100%; height:auto; }
.pb-gallery{ list-style:none; margin:0; padding:0; display:grid; grid-template-columns:repeat(auto-fill,minmax(min(220px,100%),1fr)); gap:.75rem; }
.pb-video{ margin:0; max-width:880px; }
.pb-video__frame{ position:relative; aspect-ratio:16 / 9; background:var(--ink,#111); }
.pb-video__frame iframe{ position:absolute; inset:0; width:100%; height:100%; border:0; }
.pb-video figcaption{ margin-top:.5rem; font-size:.85rem; color:var(--muted); }
.pb-faq__item{ border-bottom:1px solid var(--rule); padding:.9rem 0; }
.pb-faq__item summary{ cursor:pointer; font-weight:700; color:var(--heading); }
.pb-faq__item p{ margin:.7rem 0 0; line-height:1.85; white-space:pre-line; overflow-wrap:anywhere; }
@media (max-width:720px){
  .pb-textimage{ grid-template-columns:minmax(0,1fr); }
  .pb-textimage--right .pb-figure{ order:0; }
}
</style>
