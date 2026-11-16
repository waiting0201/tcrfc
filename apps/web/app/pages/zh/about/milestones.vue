<script setup lang="ts">
// app/pages/zh/about/milestones.vue — 資料驅動頁：時間軸一律來自後台 C5「里程碑」
// （`GET /api/backend/{club}/milestones`）。沒有可顯示的里程碑時顯示空狀態，不放寫死時間軸（2026-11-13 使用者拍板，
// docs/23 W-10；原因同 E-291：寫死版後台看不到也改不到）。
definePageMeta({ nav: 'about', unit: '02', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()

// 文案依俱樂部切換：hero／SEO 取自 club-copy.ts。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => (isEn.value ? getClubIdentityEnFor(clubKey.value) : getClubIdentity(clubKey.value)))
const hero = computed(() => (isEn.value ? (clubKey.value === 'bw' ? MILESTONES_HERO_EN_BW : getMilestonesHeroEn(facts.value)) : MILESTONES_HERO[clubKey.value]))
const seo = computed(() => (isEn.value ? (clubKey.value === 'bw' ? MILESTONES_SEO_EN_BW : MILESTONES_SEO_EN) : MILESTONES_SEO[clubKey.value]))
// 英文版的年份文字取自事實單一來源（成立年份），不在樣板寫死。
const { facts } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => seo.value.title),
  description: computed(() => seo.value.description),
})

const { activeYear, isPressed, isPanelHidden } = useYearChips()

// `GET /api/backend/{club}/milestones?lang=`（後台 C5「里程碑」，只列「顯示於時間軸」者、日期由舊到新）。
// 兩俱樂部共用同一套時間軸版型。後台資料沒有「分類標籤」欄位，所以不顯示標籤行，不臆造分類。
interface MilestoneDto {
  id: string
  happenedOn: string
  title: string | null
  description: string | null
  imageUrl: string | null
  imageAlt: string | null
  imageWidth: number | null
  imageHeight: number | null
}
const { data: milestoneData } = await useFetch<MilestoneDto[]>(`/api/backend/${config.public.club}/milestones`, {
  query: { lang: locale.value },
  key: `milestones-${config.public.club}-${locale.value}`,
})
const apiMilestones = computed(() => (milestoneData.value ?? []).filter((m) => m.title))
const milestoneYears = computed(() => {
  const groups = new Map<string, MilestoneDto[]>()
  for (const m of apiMilestones.value) {
    const y = m.happenedOn.slice(0, 4)
    groups.set(y, [...(groups.get(y) ?? []), m])
  }
  return [...groups.entries()].sort((a, b) => a[0].localeCompare(b[0])).map(([year, items]) => ({ year, items }))
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">{{ tx('重要里程碑', 'Key Milestones') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-about.jpg')" width="1600" height="900" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isEn ? aboutEyebrowEn('2.8', clubKey) : aboutEyebrow('2.8', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section v-if="apiMilestones.length" class="band milestones-band" aria-labelledby="milestones-title-api">
  <div class="band-inner container">
    <h2 id="milestones-title-api" class="visually-hidden">{{ tx('重要里程碑時間軸', 'Key milestones timeline') }}</h2>

    <div class="year-filter" role="group" :aria-label="tx('選擇年份', 'Select a year')">
      <button class="year-chip" type="button" data-year="all" :aria-pressed="isPressed('all')" @click="activeYear = 'all'">{{ tx('全部', 'All') }}</button>
      <button v-for="y in milestoneYears" :key="y.year" class="year-chip" type="button" :data-year="y.year" :aria-pressed="isPressed(y.year)" @click="activeYear = y.year">{{ y.year }}</button>
    </div>

    <div class="timeline">
      <section v-for="y in milestoneYears" :id="`milestones-${y.year}`" :key="y.year" class="timeline-year" :data-year-panel="y.year" :hidden="isPanelHidden(y.year)">
        <h3 class="timeline-year__anchor">{{ y.year }}</h3>
        <ol class="timeline-list">
          <li v-for="m in y.items" :key="m.id" :class="['timeline-item', { 'timeline-item--no-media': !m.imageUrl }]">
            <p class="timeline-item__date">{{ m.happenedOn }}</p>
            <div v-if="m.imageUrl" class="timeline-item__media"><img :src="m.imageUrl" :alt="m.imageAlt ?? ''" loading="lazy" :width="m.imageWidth ?? 640" :height="m.imageHeight ?? 427"></div>
            <div class="timeline-item__body">
              <h4 class="timeline-item__title">{{ m.title }}</h4>
              <p v-if="m.description" class="timeline-item__desc">{{ m.description }}</p>
            </div>
          </li>
        </ol>
      </section>
    </div>
  </div>
</section>

<section v-else class="band milestones-band" aria-labelledby="milestones-title-empty">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="milestones-title-empty">{{ tx('重要里程碑', 'Key Milestones') }}</h2>
    <div class="news-empty">
      <p>{{ tx('重要里程碑整理中，稍後將於本頁公布。', 'Key milestones are being compiled and will be published on this page soon.') }}</p>
    </div>
  </div>
</section>
</template>

<style>
/* 【2.8 Key Milestones】時間軸元件 + 年份篩選 —— 全站若有其他時間軸需求（例如榮譽時間軸 3.1），建議收進共用 CSS/JS */
.milestones-band{ background:var(--paper); padding-block:clamp(3.5rem,6vw,6rem); }

.year-filter{ display:flex; gap:.6rem; flex-wrap:wrap; margin-bottom:3rem; }
.year-chip{
  min-height:44px; padding:0 1.3rem; font-weight:800; font-size:.88rem;
  border:2px solid var(--rule); color:var(--muted);
  transition:background var(--dur-fast) var(--ease), color var(--dur-fast) var(--ease), border-color var(--dur-fast) var(--ease);
}
.year-chip:hover{ border-color:var(--brand-aa); color:var(--brand-aa); }
.year-chip[aria-pressed="true"]{ background:var(--brand-aa); border-color:var(--brand-aa); color:#fff; }

.timeline{ position:relative; max-width:800px; }
.timeline-year{ margin-bottom:3.5rem; }
.timeline-year:last-child{ margin-bottom:0; }
.timeline-year__anchor{
  font-size:2.4rem; font-weight:900; color:var(--brand-aa); letter-spacing:-.02em;
  margin-bottom:1.5rem; scroll-margin-top:110px;
}

.timeline-list{ position:relative; list-style:none; margin:0; padding:0; }
.timeline-list::before{
  content:""; position:absolute; left:7px; top:.4rem; bottom:.4rem; width:2px; background:var(--rule);
}
.timeline-item{
  position:relative; padding-left:2.5rem; padding-bottom:2rem;
  display:grid; grid-template-columns:auto 1fr; gap:0 1.25rem; align-items:start;
}
.timeline-item:last-child{ padding-bottom:0; }
.timeline-item::before{
  content:""; position:absolute; left:0; top:.35rem; width:16px; height:16px; border-radius:50%;
  background:var(--paper); border:3px solid var(--brand-aa);
}
.timeline-item__date{
  grid-column:1/-1; font-size:.82rem; font-weight:800; color:var(--muted); letter-spacing:.03em;
  display:flex; align-items:center; flex-wrap:wrap; gap:.4rem; margin-bottom:.6rem;
}
.timeline-item__media{ width:140px; flex:none; aspect-ratio:3/2; overflow:hidden; }
.timeline-item__media img{ width:100%; height:100%; object-fit:cover; }
.timeline-item__body{ min-width:0; }
.timeline-item__tag{ font-size:.68rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.35rem; }
.timeline-item__title{ font-size:1rem; font-weight:800; color:var(--heading); line-height:1.5; }

@media (max-width:520px){
  .timeline-item{ grid-template-columns:1fr; }
  .timeline-item__media{ width:100%; aspect-ratio:16/9; margin-bottom:.75rem; }
}
.timeline-item__desc{ font-size:.88rem; line-height:1.7; color:var(--text); white-space:pre-line; }
</style>
