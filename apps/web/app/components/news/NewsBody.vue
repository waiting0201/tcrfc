<script setup lang="ts">
// app/components/news/NewsBody.vue — 新聞詳情頁內文（ArticleDetailDto.bodyJson）
//
// 解析規則與 XSS 防線在 app/utils/news-body.ts：這裡只用 Vue 文字插值渲染，
// 🔴 不得改用 v-html；圖片網址已由 safeUrl() 限定 http(s)／站內相對路徑。
const props = defineProps<{ bodyJson: string | null | undefined }>()
const blocks = computed(() => parseNewsBody(props.bodyJson))
</script>

<template>
<div v-if="blocks.length" class="news-body">
  <template v-for="(b, i) in blocks" :key="i">
    <p v-if="b.kind === 'p'"><template v-for="(line, j) in b.lines" :key="j"><br v-if="j > 0">{{ line }}</template></p>
    <h3 v-else-if="b.kind === 'heading' && b.level === 2">{{ b.text }}</h3>
    <h4 v-else-if="b.kind === 'heading'">{{ b.text }}</h4>
    <blockquote v-else-if="b.kind === 'quote'">
      <p>{{ b.text }}</p>
      <footer v-if="b.cite">{{ b.cite }}</footer>
    </blockquote>
    <component :is="b.ordered ? 'ol' : 'ul'" v-else-if="b.kind === 'list'">
      <li v-for="(item, j) in b.items" :key="j">{{ item }}</li>
    </component>
    <figure v-else-if="b.kind === 'image'" class="news-body__figure">
      <img :src="b.src" :alt="b.alt" loading="lazy">
      <figcaption v-if="b.caption">{{ b.caption }}</figcaption>
    </figure>
    <div v-else-if="b.kind === 'gallery'" class="article-gallery">
      <figure v-for="(img, j) in b.images" :key="j">
        <img :src="img.src" :alt="img.alt" loading="lazy">
        <figcaption v-if="img.caption">{{ img.caption }}</figcaption>
      </figure>
    </div>
  </template>
</div>
</template>

<style>
.news-body{ margin-bottom:2rem; }
.news-body p{ margin:0 0 1.1rem; line-height:1.9; }
.news-body h3, .news-body h4{ margin:1.6rem 0 .7rem; }
.news-body blockquote{ margin:1.4rem 0; padding:.2rem 1.1rem; border-left:4px solid var(--brand); color:var(--muted-dark); }
.news-body blockquote footer{ font-size:.82rem; color:var(--muted); }
.news-body ul, .news-body ol{ margin:0 0 1.1rem 1.4rem; line-height:1.9; }
.news-body__figure{ margin:1.4rem 0; }
.news-body__figure img{ max-width:100%; height:auto; display:block; }
.news-body figcaption{ font-size:.78rem; color:var(--muted); margin-top:.4rem; }
</style>
