<script setup lang="ts">
// app/pages/zh/about/philosophy.vue — 由 site/src/pages/zh/about/philosophy/index.html 轉來（S0-9 靜態頁搬遷）
//
// 文案依俱樂部切換：磐石維持既有五大核心價值；藍鯨版換成 PHILOSOPHY_QUOTES_BW
// （逐字節錄 content/blue-whale/club-profile.md §3 隊徽理念、口號、培訓精神）——
// 兩者是不同的敘事框架，不強行套用磐石的五大核心價值結構到藍鯨身上（不得自行創作藍鯨沒說過的話）。
definePageMeta({ nav: "about", unit: "02" })

const { lp } = useLocale()

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
const hero = computed(() => PHILOSOPHY_HERO[clubKey.value])

useSeoMeta({
  title: computed(() => PHILOSOPHY_SEO[clubKey.value].title),
  description: computed(() => PHILOSOPHY_SEO[clubKey.value].description),
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
    <p class="page-hero__eyebrow">{{ aboutEyebrow('2.3', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<!-- 藍鯨版：隊徽理念、俱樂部口號、培訓精神——逐字節錄舊站原文（見 club-copy.ts PHILOSOPHY_QUOTES_BW）。 -->
<section v-if="clubKey === 'bw'" class="band vm-band" aria-labelledby="bw-philosophy-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="bw-philosophy-title">俱樂部口號與培訓精神</h2>
    <div class="prose">
      <h2>隊徽理念</h2>
      <p>{{ PHILOSOPHY_QUOTES_BW.crestZh }}</p>
      <h2>俱樂部口號</h2>
      <p style="white-space:pre-line">{{ PHILOSOPHY_QUOTES_BW.sloganZh }}</p>
      <h2>培訓精神</h2>
      <p style="white-space:pre-line">{{ PHILOSOPHY_QUOTES_BW.spiritZh }}</p>
    </div>
  </div>
</section>

<!-- 五大核心價值展開 —— 內容沿用首頁既有版本，維持全站文案一致（磐石專屬框架，不套用到藍鯨） -->
<section v-if="clubKey === 'tcrfc'" class="band values-band" id="values" aria-labelledby="values-expand-title">
  <span class="ghost-num ghost-num--light" aria-hidden="true">05</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FIVE CORE VALUES</p>
        <h2 class="section-title" id="values-expand-title">五大核心價值</h2>
      </div>
      <p class="section-lede">同時作為全站內容標籤，貫穿俱樂部各項訓練規劃與對外溝通。</p>
    </div>
    <div class="values-grid">
      <div class="value-card">
        <p class="value-card__num">01</p>
        <p class="value-card__en">Players First</p>
        <p class="value-card__zh">以球員為本</p>
        <p class="value-card__desc">所有訓練規劃與資源配置，皆以球員的長期發展與福祉為核心考量。</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">02</p>
        <p class="value-card__en">Excellence</p>
        <p class="value-card__zh">追求卓越</p>
        <p class="value-card__desc">建立專業化訓練與教練體系，協助選手邁向職業舞台所需的實力與態度。</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">03</p>
        <p class="value-card__en">Global Pathways</p>
        <p class="value-card__zh">國際發展</p>
        <p class="value-card__desc">從台中出發、放眼世界，透過海外交流建立選手與職業舞台接軌的路徑。</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">04</p>
        <p class="value-card__en">Community</p>
        <p class="value-card__zh">社區共好</p>
        <p class="value-card__desc">紮根台中在地，成為社區認同與榮耀的來源，與球迷共同成長。</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">05</p>
        <p class="value-card__en">Integrity</p>
        <p class="value-card__zh">誠信專業</p>
        <p class="value-card__desc">以誠信治理與專業制度，支撐俱樂部長期穩健發展。</p>
      </div>
    </div>
  </div>
</section>
</template>
