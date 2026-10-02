<script setup lang="ts">
// app/pages/zh/about/our-story.vue — 由 site/src/pages/zh/about/our-story/index.html 轉來（S0-9 靜態頁搬遷）
//
// 文案依俱樂部切換：hero／SEO 取自 shared/utils/club-copy.ts；藍鯨版內文
// （OUR_STORY_BODY_BW）逐字節錄 content/blue-whale/club-profile.md §1，未新增文字。
definePageMeta({ nav: "about", unit: "02" })

const { lp } = useLocale()

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
// S1-12d 收尾第二輪：hero／SEO 含成立年份事實，club-copy.ts 已改為工廠函式；本頁 tcrfc
// 專屬段落原本固定讀 useSiteFacts('tcrfc')，現在 hero／SEO 兩俱樂部都要讀，改為動態帶入
// 目前 club（clubKey==='tcrfc' 時兩者等價）。
const { facts, primaryVenue } = useSiteFacts(clubKey.value)
const hero = computed(() => getOurStoryHero(clubKey.value, facts.value))

useSeoMeta({
  title: computed(() => getOurStorySeo(clubKey.value, facts.value).title),
  description: computed(() => getOurStorySeo(clubKey.value, facts.value).description),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">我們的故事</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-about.jpg')" width="1600" height="900" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ aboutEyebrow('2.1', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band story-band" aria-labelledby="story-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="story-title">我們的故事</h2>
    <div class="story-layout">
      <div v-if="clubKey === 'tcrfc'" class="prose">
        <!-- GEO-03（S1-12d）：成立年份／首季頭銜／主場為單一來源 site-facts.ts，不在此重複寫死字面值。 -->
        <p>台中磐石足球俱樂部（Taichung Rock FC）於 <strong>{{ facts.foundedYear }} 年</strong>在台中成立，同年即拿下<strong>{{ facts.foundingTitleZh }}</strong>。俱樂部主場設於{{ primaryVenue.nameZh }}，以「在地扎根．放眼世界」為品牌主張，逐步建立起一線隊、學院與課程並行的發展體系。</p>

        <h2>圖文段落</h2>
        <p>本頁版型為長文編輯，支援圖文混排與引言區塊；正式內文與圖片確認後，將依段落穿插俱樂部歷年照片。</p>
      </div>
      <div v-else class="prose">
        <p>{{ OUR_STORY_BODY_BW }}</p>
      </div>
    </div>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="story-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="story-cta-title">認識更多</h2>
    <div class="cta-grid">
      <div class="cta-card">
        <p class="cta-card__num">2.2</p>
        <p class="cta-card__title">願景與使命</p>
        <p class="cta-card__desc">了解{{ ABOUT_NAV_DESC[clubKey].visionMission }}</p>
        <a class="btn btn--primary" :href="lp('/zh/about/vision-mission/')">前往閱讀</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">2.7</p>
        <p class="cta-card__title">俱樂部歷程</p>
        <p class="cta-card__desc">{{ ABOUT_NAV_DESC[clubKey].history }}</p>
        <a class="btn btn--primary" :href="lp('/zh/about/history/')">前往閱讀</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">2.8</p>
        <p class="cta-card__title">重要里程碑</p>
        <p class="cta-card__desc">按年份檢視俱樂部的重要大事記。</p>
        <a class="btn btn--primary" :href="lp('/zh/about/milestones/')">查看時間軸</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 【2.1 Our Story】長文版型 —— 圖文佔位框沿用 .pending 視覺語彙延伸 */
.story-band{ background:var(--paper); padding-block:clamp(3.5rem,6vw,6rem); }
.story-layout{ display:flex; justify-content:center; }
.story-figure{ aspect-ratio:16/9; align-items:center; justify-content:center; text-align:center; }
</style>
