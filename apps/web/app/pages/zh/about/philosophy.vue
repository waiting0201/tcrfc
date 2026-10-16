<script setup lang="ts">
// app/pages/zh/about/philosophy.vue — 由 site/src/pages/zh/about/philosophy/index.html 轉來（S0-9 靜態頁搬遷）
//
// 文案依俱樂部切換：磐石＝足球理念＋四大核心價值（v3.29）；藍鯨版換成 PHILOSOPHY_QUOTES_BW
// （逐字節錄 content/blue-whale/club-profile.md §3 隊徽理念、口號、培訓精神）——
// 兩者是不同的敘事框架，不強行套用磐石的四大核心價值結構到藍鯨身上（不得自行創作藍鯨沒說過的話）。
definePageMeta({ nav: "about", unit: "02", enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => (isEn.value ? getClubIdentityEnFor(clubKey.value) : getClubIdentity(clubKey.value)))
const hero = computed(() => (isEn.value ? (clubKey.value === 'bw' ? PHILOSOPHY_HERO_EN_BW : PHILOSOPHY_HERO_EN) : PHILOSOPHY_HERO[clubKey.value]))
const seo = computed(() => (isEn.value ? (clubKey.value === 'bw' ? PHILOSOPHY_SEO_EN_BW : PHILOSOPHY_SEO_EN) : PHILOSOPHY_SEO[clubKey.value]))

const quotes = computed(() => (isEn.value ? PHILOSOPHY_QUOTES_EN_BW : PHILOSOPHY_QUOTES_BW))

// 磐石：足球理念本文＋四大核心價值，皆為客戶 2.3 原文（shared/utils/core-values.ts；英文 club-copy-en-core.ts）
const coreValues = computed(() => buildCoreValueViews(null))
const philosophyParagraphs = computed(() => splitParagraphs(isEn.value ? PHILOSOPHY_BODY_EN : PHILOSOPHY_BODY_TCRFC))

// 稽核 B-1：主內文改讀頁面管理（有已發布區塊用 CMS，否則維持下方寫死內容；hero／麵包屑／CTA 不變）
const cms = await useCmsPage('about/philosophy')
cms.applySeo({ title: () => seo.value.title, description: () => seo.value.description })
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">{{ hero.h1Zh }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-about.jpg')" width="1600" height="900" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isEn ? aboutEyebrowEn('2.3', clubKey) : aboutEyebrow('2.3', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<!-- 藍鯨版：隊徽理念、俱樂部口號、培訓精神——逐字節錄舊站原文（見 club-copy.ts PHILOSOPHY_QUOTES_BW）。 -->
<ContentCmsPageBand v-if="cms.active.value" :blocks="cms.blocks.value" :label="tx('足球理念', 'Our Philosophy')" :zh-fallback="cms.hasZhFallback.value" />
<template v-else>
<section v-if="clubKey === 'bw'" class="band vm-band" aria-labelledby="bw-philosophy-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="bw-philosophy-title">{{ tx('俱樂部口號與培訓精神', 'Club Slogan and Training Spirit') }}</h2>
    <div class="prose">
      <h2>{{ tx('隊徽理念', 'Crest Concept') }}</h2>
      <p>{{ quotes.crestZh }}</p>
      <h2>{{ tx('俱樂部口號', 'Club Slogan') }}</h2>
      <p style="white-space:pre-line">{{ quotes.sloganZh }}</p>
      <h2>{{ tx('培訓精神', 'Training Spirit') }}</h2>
      <p style="white-space:pre-line">{{ quotes.spiritZh }}</p>
    </div>
  </div>
</section>

<!-- 四大核心價值展開（v3.29，客戶 2.3 原文）—— 磐石專屬框架，不套用到藍鯨 -->
<section v-if="clubKey === 'tcrfc'" class="band values-band" id="values" aria-labelledby="values-expand-title">
  <span class="ghost-num ghost-num--light" aria-hidden="true">04</span>
  <div class="band-inner container">
    <div class="prose philosophy-intro">
      <h2 class="visually-hidden">{{ tx('足球理念', 'Our Philosophy') }}</h2>
      <p v-for="(para, i) in philosophyParagraphs" :key="i" class="philosophy-intro__p">{{ para }}</p>
    </div>
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FOUR CORE VALUES</p>
        <h2 class="section-title" id="values-expand-title">{{ tx('四大核心價值', 'Four Core Values') }}</h2>
      </div>
      <p class="section-lede">{{ tx('同時作為全站內容標籤，貫穿俱樂部各項訓練規劃與對外溝通。', 'They also serve as content tags across the site, running through all of the club\'s training plans and external communications.') }}</p>
    </div>
    <div class="values-grid values-grid--2">
      <div v-for="v in coreValues" :key="v.code" class="value-card">
        <p class="value-card__num">{{ v.num }}</p>
        <p class="value-card__en">{{ v.nameEn }}</p>
        <p v-if="!isEn" class="value-card__zh">{{ v.nameZh }}</p>
        <p v-for="(para, i) in (isEn ? splitParagraphs(CORE_VALUE_DESCRIPTIONS_EN[v.code] ?? '') : v.paragraphs)" :key="i" class="value-card__desc">{{ para }}</p>
      </div>
    </div>
  </div>
</section>
</template>
</template>

<style>
.philosophy-intro{ margin-bottom:3rem; }
.philosophy-intro__p{ white-space:pre-line; }
.values-grid--2 .value-card__desc{ white-space:pre-line; }
</style>
