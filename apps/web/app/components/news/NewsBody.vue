<script setup lang="ts">
// app/components/news/NewsBody.vue — 新聞詳情頁內文（ArticleDetailDto.bodyJson）
//
// 解析規則與 XSS 防線在 app/utils/news-body.ts：這裡只用 Vue 文字插值渲染，
// 🔴 不得改用 v-html；圖片網址已由 safeUrl() 限定 http(s)／站內相對路徑。
const props = defineProps<{ bodyJson: string | null | undefined }>()
const mediaBaseUrl = useRuntimeConfig().public.mediaBaseUrl as string | undefined
const blocks = computed(() => parseNewsBody(props.bodyJson, mediaBaseUrl))
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
      <!-- 照原比例、不裁切：帶 width／height 讓瀏覽器預留版面，CSS 以 height:auto 維持比例 -->
      <img
        :src="b.src"
        :srcset="b.srcset"
        :sizes="b.srcset ? '(max-width: 900px) 100vw, 760px' : undefined"
        :alt="b.alt"
        :width="b.width ?? undefined"
        :height="b.height ?? undefined"
        loading="lazy"
        decoding="async"
      >
      <figcaption v-if="b.caption">{{ b.caption }}</figcaption>
    </figure>
    <div v-else-if="b.kind === 'gallery'" class="news-body__gallery">
      <figure v-for="(img, j) in b.images" :key="j">
        <img
          :src="img.src"
          :srcset="img.srcset"
          :sizes="img.srcset ? '(min-width: 1024px) 260px, (min-width: 640px) 45vw, 100vw' : undefined"
          :alt="img.alt"
          :width="img.width ?? undefined"
          :height="img.height ?? undefined"
          loading="lazy"
          decoding="async"
        >
        <figcaption v-if="img.caption">{{ img.caption }}</figcaption>
      </figure>
    </div>
    <figure v-else-if="b.kind === 'video'" class="news-body__video">
      <div class="news-body__video-frame">
        <iframe
          :src="b.src"
          :title="b.caption || (b.provider === 'vimeo' ? 'Vimeo video' : 'YouTube video')"
          loading="lazy"
          allow="fullscreen; picture-in-picture"
          allowfullscreen
          referrerpolicy="strict-origin-when-cross-origin"
          sandbox="allow-scripts allow-same-origin allow-presentation allow-popups"
        />
      </div>
      <figcaption v-if="b.caption">{{ b.caption }}</figcaption>
    </figure>
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

<style scoped>
/* 圖集：照原比例的 CSS columns 瀑布流（≥1024px 3 欄、≥640px 2 欄、手機 1 欄），不裁切 */
.news-body__gallery{ columns:1; column-gap:1rem; margin:1.4rem 0; }
.news-body__gallery figure{ break-inside:avoid; margin:0 0 1rem; }
.news-body__gallery img{ display:block; width:100%; height:auto; }
@media (min-width:640px){ .news-body__gallery{ columns:2; } }
@media (min-width:1024px){ .news-body__gallery{ columns:3; } }
.news-body__video{ margin:1.4rem 0; }
.news-body__video-frame{ position:relative; aspect-ratio:16 / 9; background:#000; }
.news-body__video-frame iframe{ position:absolute; inset:0; width:100%; height:100%; border:0; }
</style>
