<script setup lang="ts">
// app/pages/zh/about/vision-mission.vue — 由 site/src/pages/zh/about/vision-mission/index.html 轉來（S0-9 靜態頁搬遷）
//
// 文案依俱樂部切換：磐石維持既有雙欄 Vision/Mission；藍鯨版改用 VISION_ITEMS
// 逐字節錄 content/blue-whale/club-profile.md §5「發展願景」五節（見 club-copy.ts）。
definePageMeta({ nav: "about", unit: "02" })

const { lp } = useLocale()

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
const hero = computed(() => VISION_MISSION_HERO[clubKey.value])
const items = computed(() => VISION_ITEMS[clubKey.value])

useSeoMeta({
  title: computed(() => VISION_MISSION_SEO[clubKey.value].title),
  description: computed(() => VISION_MISSION_SEO[clubKey.value].description),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">{{ hero.h1Zh }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-about.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">{{ aboutEyebrow('2.2', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band vm-band" aria-labelledby="vm-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="vm-title">{{ hero.h1Zh }}</h2>
    <div class="vm-grid">
      <div v-for="item in items" :key="item.titleZh" class="vm-col">
        <p class="kicker">{{ item.kicker }}</p>
        <h2 class="vm-col__title">{{ item.titleZh }}</h2>
        <p class="vm-col__text">{{ item.textZh }}</p>
      </div>
    </div>

    <p v-if="clubKey === 'tcrfc'" class="vm-footnote">俱樂部品牌主張與五大核心價值可先參考 <a :href="lp('/zh/about/philosophy/')">2.3 足球理念</a>。</p>
    <p v-else class="vm-footnote">俱樂部口號與培訓精神可先參考 <a :href="lp('/zh/about/philosophy/')">2.3 俱樂部口號與培訓精神</a>。</p>
  </div>
</section>
</template>

<style>
/* 【2.2 Vision & Mission】雙欄式版型 + 圖示條列骨架 */
.vm-band{ background:var(--paper); padding-block:clamp(3.5rem,6vw,6rem); }
.vm-grid{
  display:grid; gap:clamp(2rem,4vw,3.5rem);
  grid-template-columns:repeat(auto-fit,minmax(320px,1fr));
}
.vm-col__title{ font-size:var(--fs-h3); font-weight:900; color:var(--heading); margin:.4rem 0 1.25rem; }
.vm-col__text{ font-size:1rem; line-height:1.75; color:var(--text); max-width:44ch; }
.vm-footnote{ margin-top:3rem; font-size:.88rem; color:var(--muted); }
.vm-footnote a{ color:var(--brand-aa); font-weight:700; }
</style>
