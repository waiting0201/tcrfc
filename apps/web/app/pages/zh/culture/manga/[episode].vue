<script setup lang="ts">
// app/pages/zh/culture/manga/[episode].vue — 8.1 漫畫線上閱讀器（主站 §3.8，S3-2）
//
// 單集閱讀頁：分頁／捲動雙模式、上下集導覽、行動裝置手勢（左右滑動換頁）、鍵盤左右鍵。
// **全部免費、不需登入、沒有付費牆**。藍鯨不設 8.1：`unit: '8.1'` 讓 unit-gate 對藍鯨容器直接 404（後端亦回 403）。
// 草稿與未到發布日的集數後端回 404 → 這裡轉成 404 頁。閱讀數在客戶端載入後送一次（`POST …/views`，固定 204，
// 失敗靜默忽略——不影響閱讀）。
import type { ComicEpisodeDetail } from '#shared/utils/member'

definePageMeta({ nav: 'culture', unit: '8.1' })

const { lp, locale } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const club = config.public.club
const identity = computed(() => getClubIdentity(club))

const episodeNo = Number.parseInt(String(route.params.episode ?? ''), 10)
if (!Number.isInteger(episodeNo) || episodeNo < 1) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}

const { data: ep, error } = await useFetch<ComicEpisodeDetail>(`/api/backend/${club}/comic/episodes/${episodeNo}`, {
  query: { lang: locale.value },
  key: `comic-ep-${club}-${episodeNo}-${locale.value}`,
})
if (error.value || !ep.value) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}

useSeoMeta({
  title: computed(() => `第 ${episodeNo} 集　${ep.value?.title ?? ''}｜台中磐石漫畫｜台中磐石足球俱樂部`),
  description: computed(() => `台中磐石漫畫第 ${episodeNo} 集《${ep.value?.title ?? ''}》線上閱讀，免費、不需登入。`),
})

const pages = computed(() => (ep.value?.pages ?? []).filter(p => /^(https:\/\/|\/)/.test(p.imageUrl)))
const mode = ref<'paginated' | 'scroll'>('paginated')
const index = ref(0)
const total = computed(() => pages.value.length)
const current = computed(() => pages.value[index.value])

function go(delta: number) {
  const next = index.value + delta
  if (next < 0 || next >= total.value) return
  index.value = next
  // 預載下一頁，翻頁不閃
  const upcoming = pages.value[next + 1]
  if (upcoming && import.meta.client) new Image().src = upcoming.imageUrl
}
function onKey(e: KeyboardEvent) {
  if (mode.value !== 'paginated') return
  if (e.key === 'ArrowRight') { e.preventDefault(); go(1) }
  else if (e.key === 'ArrowLeft') { e.preventDefault(); go(-1) }
}

// 行動裝置手勢：橫向滑動超過 50px 且橫向位移大於縱向才換頁，不干擾上下捲動
let touchX = 0
let touchY = 0
function onTouchStart(e: TouchEvent) {
  touchX = e.changedTouches[0]?.clientX ?? 0
  touchY = e.changedTouches[0]?.clientY ?? 0
}
function onTouchEnd(e: TouchEvent) {
  if (mode.value !== 'paginated') return
  const dx = (e.changedTouches[0]?.clientX ?? 0) - touchX
  const dy = (e.changedTouches[0]?.clientY ?? 0) - touchY
  if (Math.abs(dx) < 50 || Math.abs(dx) < Math.abs(dy)) return
  go(dx < 0 ? 1 : -1)
}

onMounted(() => {
  // 閱讀數：載入後送一次，失敗靜默
  $fetch(`/api/backend/${club}/comic/episodes/${episodeNo}/views`, { method: 'POST', body: {} }).catch(() => {})
  if (pages.value[1]) new Image().src = pages.value[1].imageUrl
})
</script>

<template>
<nav v-if="ep" class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li><a :href="lp('/zh/culture/manga/')">台中磐石漫畫</a></li>
      <li aria-current="page">第 {{ ep.episodeNo }} 集</li>
    </ol>
  </div>
</nav>

<template v-if="ep">
<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">8.1 TCRFC Manga / Comics</p>
    <h1>第 {{ ep.episodeNo }} 集　{{ ep.title }}</h1>
    <p class="page-hero__lede"><template v-if="ep.publishedOn">發布日 {{ formatPlainDate(ep.publishedOn) }}　</template>免費閱讀，不需登入。</p>
  </div>
</section>

<section id="reader" class="band grain grain--2" aria-labelledby="reader-title">
  <div class="band-inner container">
    <h2 id="reader-title" class="visually-hidden">線上閱讀器</h2>

    <div id="manga-reader" class="reader" :class="{ 'is-scroll': mode === 'scroll' }">
      <div class="reader__toolbar">
        <div class="reader__modes" role="group" aria-label="切換閱讀模式">
          <button type="button" class="reader__mode-btn" :aria-pressed="mode === 'paginated'" @click="mode = 'paginated'">分頁模式</button>
          <button type="button" class="reader__mode-btn" :aria-pressed="mode === 'scroll'" @click="mode = 'scroll'">捲動模式</button>
        </div>
        <div class="reader__nav">
          <a v-if="ep.previousEpisodeNo" class="reader__nav-link" :href="lp(`/zh/culture/manga/${ep.previousEpisodeNo}/`)">← 上一集</a>
          <button v-else type="button" class="reader__nav-btn" disabled aria-label="沒有上一集">← 上一集</button>
          <a v-if="ep.nextEpisodeNo" class="reader__nav-link" :href="lp(`/zh/culture/manga/${ep.nextEpisodeNo}/`)">下一集 →</a>
          <button v-else type="button" class="reader__nav-btn" disabled aria-label="沒有下一集">下一集 →</button>
        </div>
      </div>

      <div v-if="total === 0" class="reader__frame" aria-live="polite">
        <p>這一集還沒有可閱讀的頁面</p>
      </div>

      <!-- 分頁模式：一次一頁，左右鍵／按鈕／左右滑動換頁 -->
      <div
        v-else-if="mode === 'paginated'"
        class="reader__frame"
        tabindex="0"
        role="group"
        aria-label="漫畫頁面，可用左右方向鍵換頁"
        @keydown="onKey"
        @touchstart.passive="onTouchStart"
        @touchend.passive="onTouchEnd"
      >
        <img
          v-if="current"
          :key="current.pageNo"
          class="reader__page"
          :src="current.imageUrl"
          :alt="`第 ${ep.episodeNo} 集 第 ${current.pageNo} 頁`"
          :width="current.width ?? undefined"
          :height="current.height ?? undefined"
        >
        <div class="reader__nav">
          <button type="button" class="reader__nav-btn" :disabled="index === 0" @click="go(-1)">← 上一頁</button>
          <span class="reader__counter" aria-live="polite">第 {{ index + 1 }} / {{ total }} 頁</span>
          <button type="button" class="reader__nav-btn" :disabled="index >= total - 1" @click="go(1)">下一頁 →</button>
        </div>
      </div>

      <!-- 捲動模式：由上而下連續閱讀，第一頁之後延遲載入 -->
      <div v-else class="reader__frame" role="group" aria-label="漫畫頁面，上下捲動閱讀">
        <img
          v-for="(p, i) in pages"
          :key="p.pageNo"
          class="reader__page"
          :src="p.imageUrl"
          :alt="`第 ${ep.episodeNo} 集 第 ${p.pageNo} 頁`"
          :width="p.width ?? undefined"
          :height="p.height ?? undefined"
          :loading="i === 0 ? 'eager' : 'lazy'"
        >
      </div>
    </div>

    <p v-if="total > 0 && (mode === 'scroll' || index >= total - 1)" class="mc-note" style="color:var(--muted-dark);margin-top:1.5rem;">
      本集完。
      <a v-if="ep.nextEpisodeNo" :href="lp(`/zh/culture/manga/${ep.nextEpisodeNo}/`)" style="color:#fff;text-decoration:underline;">閱讀下一集 →</a>
      <template v-else>目前已是最新一集，下一集敬請期待。</template>
    </p>
  </div>
</section>

<section class="band" aria-labelledby="manga-back-title">
  <div class="container">
    <h2 id="manga-back-title" class="visually-hidden">更多內容</h2>
    <p><a class="btn btn--dark" :href="lp('/zh/culture/manga/')">回到漫畫首頁</a>　<a class="btn btn--primary" :href="lp('/zh/culture/fan-club/')">加入球迷會</a></p>
  </div>
</section>
</template>
</template>

<style>
/* 線上閱讀器：建議收進共用 CSS（其他集數頁與角色詳情頁都會用到） */
.reader{ background:var(--ink); border:1px solid rgba(255,255,255,.1); }
.reader__toolbar{
  display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:1rem;
  padding:1.1rem 1.25rem; border-bottom:1px solid rgba(255,255,255,.1);
}
.reader__modes{ display:flex; gap:.5rem; }
.reader__mode-btn{
  min-height:40px; padding:0 1rem; font-size:.8rem; font-weight:800;
  background:transparent; border:2px solid rgba(255,255,255,.18); color:var(--muted-dark);
  transition:all var(--dur-fast) var(--ease);
}
.reader__mode-btn[aria-pressed="true"]{ background:var(--brand-aa); border-color:var(--brand-aa); color:#fff; }
.reader__nav{ display:flex; gap:.5rem; }
.reader__nav-btn{
  min-height:40px; padding:0 1rem; font-size:.8rem; font-weight:700;
  background:transparent; border:1px solid rgba(255,255,255,.18); color:var(--muted-dark);
}
.reader__nav-btn:disabled{ opacity:.4; cursor:not-allowed; }
.reader__frame{
  min-height:360px; display:flex; flex-direction:column; align-items:center; justify-content:center; gap:.6rem;
  padding:2.5rem 1.5rem; text-align:center; color:var(--muted-dark);
}
.reader__frame p:first-child{ font-size:1.05rem; font-weight:800; color:#fff; }
.reader__frame-note{ font-size:.82rem; max-width:44ch; line-height:1.7; }
.reader.is-scroll .reader__frame{ min-height:520px; }

.reader__frame{ position:relative; }
.reader__frame:focus-visible{ outline:2px solid var(--brand); outline-offset:-2px; }
.reader__frame .reader__nav{ margin-top:1rem; align-items:center; }
</style>
