<script setup lang="ts">
// app/pages/zh/programs/index.vue — 由 site/src/pages/zh/programs/index.html 轉來
// 🔴 tcrfc 版 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；
// {{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// BW-C1（品牌外洩全站盤點）：本頁原本整頁固定磐石內容（單元名稱、五張導覽卡描述、
// 照片、六步驟線上報名流程、底部 CTA 全部沒有俱樂部分支）。藍鯨規劃書 §2.1：05
// 單元名稱是「PROGRAMS 推廣活動」，不是磐石的「課程與活動」；§3.5（行 201）「05
// 沿用主站 05 的活動版型；是否開放線上報名與收費，待確認」——藍鯨 5.1–5.4 各頁
// 現況一律現場個人報名，不接站內線上報名／金流流程，故「線上報名流程」六步驟區塊
// 對藍鯨隱藏，改顯示如實的報名說明（getProgramsHubEnrolNoteBw()）。五張導覽卡描述
// 與底部 CTA 改讀 club-copy.ts 的 getProgramsHubCards()／getProgramsHubCtaCards()，
// 沒有藍鯨自己照片的卡片改用既有的漸層佔位，不挪用磐石照片。
definePageMeta({ nav: 'programs', unit: '05', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

useSeoMeta({
  title: computed(() => (isEn.value ? getProgramsHubSeoEn(clubKey.value) : getProgramsHubSeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? getProgramsHubSeoEn(clubKey.value) : getProgramsHubSeo(clubKey.value)).description),
})
// C-6／S2-13：主站 /en/ 讀 shared/utils/club-copy-en-acad.ts（英文版文案）。
const hero = computed(() => (isEn.value ? getProgramsHubHeroEn(clubKey.value) : getProgramsHubHero(clubKey.value)))
const intro = computed(() => (isEn.value ? getProgramsHubIntroEn(clubKey.value) : getProgramsHubIntro(clubKey.value)))
const cards = computed(() => (isEn.value ? getProgramsHubCardsEn(clubKey.value) : getProgramsHubCards(clubKey.value)))
const enrolNoteBw = computed(() => (isEn.value ? getProgramsHubEnrolNoteEnBw() : getProgramsHubEnrolNoteBw()))
const ctaCards = computed(() => (isEn.value ? getProgramsHubCtaCardsEn(clubKey.value) : getProgramsHubCtaCards(clubKey.value)))
const flowSteps = computed(() => (isEn.value ? ENROL_FLOW_STEPS_TCRFC_EN : ENROL_FLOW_STEPS_TCRFC))

/** 導覽卡照片路徑——只有 tcrfc 既有卡片沿用原圖，藍鯨版一律 `hasPhoto: false`
 * （見 getProgramsHubCards 檔頭說明），不需要另外維護一份藍鯨照片路徑對照表。 */
const CARD_PHOTOS: Record<string, string> = {
  '5.1': siteImg('/assets/img/programs/childrens-03.jpg'),
  '5.2': siteImg('/assets/img/programs/summer-camp-05.jpg'),
  '5.4': siteImg('/assets/img/programs/specialist-06.jpg'),
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx(isTcrfc ? '課程與活動' : '推廣活動', 'Programs') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/nav-programs.jpg')" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '05 Programs' : '05' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band unit-intro">
  <div class="container">
    <div class="prose">
      <p>{{ intro }}</p>
    </div>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="card-nav-grid">
      <a v-for="c in cards" :key="c.num" class="nav-card clip-card" :href="lp(c.href)">
        <div v-if="c.hasPhoto" class="nav-card__media">
          <img :src="CARD_PHOTOS[c.num]" alt="" loading="lazy" width="1600" height="1067">
        </div>
        <div v-else class="nav-card__media nav-card__media--empty" aria-hidden="true">
          <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 2v20M2 12h20M4.9 4.9l14.2 14.2M19.1 4.9L4.9 19.1"/></svg>
        </div>
        <div class="nav-card__body">
          <p class="nav-card__num">{{ c.num }}</p>
          <p class="nav-card__title">{{ c.titleZh }}<span v-if="!isEn" class="en">{{ c.titleEn }}</span></p>
          <p class="nav-card__desc">{{ c.descZh }}</p>
          <span class="nav-card__link">{{ tx('查看詳情', 'View details') }}
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
          </span>
        </div>
      </a>
    </div>
  </div>
</section>

<!-- 報名流程：tcrfc 為介面結構視覺（非客戶內容）；藍鯨不接這套站內線上流程，
     改顯示如實的現場報名說明，見本頁檔頭說明。 -->
<section v-if="isTcrfc" class="band grain" aria-labelledby="enroll-flow-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">05</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">HOW TO ENROL</p>
        <h2 id="enroll-flow-title" class="section-title">{{ tx('線上報名流程', 'Online Registration Process') }}</h2>
      </div>
    </div>
    <p class="section-lede">{{ tx('所有課程與營隊皆透過同一套線上流程報名，站內不接受金流付款。', 'All programs and camps use the same online registration process. Payment is not taken on this site.') }}</p>

    <ol class="flow-steps">
      <li v-for="(step, i) in flowSteps" :key="i"><h3>{{ step.titleZh }}</h3><p>{{ step.descZh }}</p></li>
    </ol>
  </div>
</section>
<section v-else class="band grain" aria-labelledby="enroll-flow-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">05</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">HOW TO JOIN</p>
        <h2 id="enroll-flow-title" class="section-title">{{ tx('如何報名', 'How to Register') }}</h2>
      </div>
    </div>
    <p class="section-lede">{{ enrolNoteBw }}</p>
  </div>
</section>

<section class="band cta-band grain grain--2" aria-labelledby="programs-cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">JOIN</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">GET STARTED</p>
        <h2 id="programs-cta-title" class="section-title">{{ tx('準備好開始了嗎？', 'Ready to Get Started?') }}</h2>
      </div>
    </div>
    <div class="cta-grid">
      <div v-for="c in ctaCards" :key="c.titleZh" class="cta-card clip-card clip-card--on-dark">
        <p class="cta-card__num">{{ c.num }}</p>
        <p class="cta-card__title">{{ c.titleZh }}</p>
        <p class="cta-card__desc">{{ c.descZh }}</p>
        <a class="btn btn--light btn--sm" :href="lp(c.href)">{{ c.ctaLabelZh }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }

/* PROGRAMS 單元共用元件（landing + 5.1–5.5 共用；三頁以上重複使用，建議收進 tcrfc.css） */
.unit-intro{ padding-block:clamp(3rem,6vw,4.5rem); }

.card-nav-grid{ display:grid; gap:1.5rem; grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); }
.nav-card{ position:relative; background:var(--paper); display:flex; flex-direction:column; }
.nav-card__media{ aspect-ratio:4/3; overflow:hidden; background:var(--ink-2); }
.nav-card__media img{ width:100%; height:100%; object-fit:cover; transition:transform .5s var(--ease); }
.nav-card:hover .nav-card__media img{ transform:scale(1.06); }
.nav-card__media--empty{ display:flex; align-items:center; justify-content:center; color:var(--muted-dark); }
.nav-card__body{ padding:1.25rem 1.35rem 1.5rem; display:flex; flex-direction:column; gap:.5rem; flex:1; }
.nav-card__num{ font-size:.72rem; font-weight:800; color:var(--brand-aa); letter-spacing:.08em; }
.nav-card__title{ font-size:1.15rem; font-weight:900; color:var(--heading); }
.nav-card__title .en{ display:block; font-size:.68rem; font-weight:700; letter-spacing:.1em; color:var(--muted); text-transform:uppercase; margin-top:.2rem; }
.nav-card__desc{ font-size:.85rem; color:var(--muted); line-height:1.6; flex:1; }
.nav-card__link{ font-size:.8rem; font-weight:700; color:var(--brand-aa); display:inline-flex; gap:.35em; align-items:center; }
.nav-card__link svg{ transition:transform var(--dur-fast,.2s) var(--ease); }
.nav-card:hover .nav-card__link svg{ transform:translateX(4px); }

.flow-steps{ display:grid; gap:1.25rem; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); counter-reset:step; margin-top:2rem; list-style:none; padding:0; }
.flow-steps li{ background:rgba(255,255,255,.04); border:1px solid rgba(255,255,255,.12); padding:1.5rem 1.25rem; position:relative; }
.flow-steps li::before{ counter-increment:step; content:counter(step); font-size:2.2rem; font-weight:900; color:var(--brand); display:block; margin-bottom:.6rem; line-height:1; }
.flow-steps h3{ font-size:.9rem; font-weight:800; color:#fff; margin-bottom:.4rem; }
.flow-steps p{ font-size:.78rem; color:var(--muted-dark); line-height:1.6; }
</style>
