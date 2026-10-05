<script setup lang="ts">
// app/pages/zh/club/index.vue — 由 site/src/pages/zh/club/index.html 轉來
// 🔴 tcrfc 版 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；
// {{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// BW-C1（品牌外洩全站盤點）：本頁原本是 S1-12d／S2-8 已記錄的既有缺口——整頁固定
// 磐石內容，只用 `isTcrfc` 隱藏兩張會 404 的單元卡（3.2／3.4），沒有真正雙俱樂部化。
// 3.2／3.4 已於 BW-C1 重開（見 shared/utils/units.ts 檔頭），本輪移除舊有的隱藏判斷，
// 改為 SEO／Hero／統計卡／單元卡描述／CTA 標題全部依俱樂部切換（getClubHubSeo 等，
// club-copy.ts），版型與 DOM 結構不變。tcrfc 分支逐字沿用改動前的既有輸出。
definePageMeta({ nav: 'club', unit: '03', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

const { facts } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => (isEn.value ? (isTcrfc.value ? getClubHubSeoEn() : getClubHubSeoEnBw(facts.value)) : getClubHubSeo(clubKey.value, facts.value)).title),
  description: computed(() => (isEn.value ? (isTcrfc.value ? getClubHubSeoEn() : getClubHubSeoEnBw(facts.value)) : getClubHubSeo(clubKey.value, facts.value)).description),
})
// 英文版：磐石用 `*_EN`，藍鯨用 `*_EN_BW`／`*EnBw`（club-copy-en-club.ts 檔頭）。
const hero = computed(() => (isEn.value ? (isTcrfc.value ? getClubHubHeroEn(facts.value) : getClubHubHeroEnBw(facts.value)) : getClubHubHero(clubKey.value, facts.value)))
const stats = computed(() => (isEn.value ? (isTcrfc.value ? getClubHubStatsEn(facts.value) : getClubHubStatsEnBw(facts.value)) : getClubHubStats(clubKey.value, facts.value)))
const opportunitiesDesc = computed(() => (isEn.value ? (isTcrfc.value ? CLUB_HUB_OPPORTUNITIES_DESC_EN : CLUB_HUB_OPPORTUNITIES_DESC_EN_BW) : getClubHubOpportunitiesDesc(clubKey.value)))
const playerStoriesDesc = computed(() => (isEn.value ? (isTcrfc.value ? CLUB_HUB_PLAYER_STORIES_DESC_EN : CLUB_HUB_PLAYER_STORIES_DESC_EN_BW) : getClubHubPlayerStoriesDesc(clubKey.value)))
const ctaTitle = computed(() => (isEn.value ? (isTcrfc.value ? CLUB_HUB_CTA_TITLE_EN : CLUB_HUB_CTA_TITLE_EN_BW) : getClubHubCtaTitle(clubKey.value)))
// 底部 CTA 第一張卡「加入球隊」——沿用首頁既有的 getHomeCtaTrio() 第一筆文案
// （已依 facts.league.nameZh 動態帶入聯賽名稱，避免本頁自己重打一份「企甲聯賽」
// 字面值，藍鯨會因此誤植磐石聯賽名稱）。
const joinPlayerCard = computed(() => (isEn.value ? (isTcrfc.value ? getClubHubJoinPlayerCardEn(facts.value) : getClubHubJoinPlayerCardEnBw(facts.value)) : getHomeCtaTrio(clubKey.value, facts.value)[0]!))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('俱樂部', 'Football Club') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/nav-club.jpg')" alt="" width="1920" height="1279">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '03 Football Club' : '03' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band grain grain--2 stats-band" aria-labelledby="club-stats-title">
  <h2 class="visually-hidden" id="club-stats-title">{{ tx('俱樂部一線隊數據', 'Club First Team statistics') }}</h2>
  <div class="band-inner container">
    <div class="stats-grid">
      <div v-for="s in stats" :key="s.labelZh" class="stat">
        <p class="stat__num">{{ s.num }}</p>
        <p class="stat__label">{{ s.labelZh }}</p>
      </div>
    </div>
  </div>
</section>

<section class="band unit-nav-band" id="unit-links" aria-labelledby="unit-links-title">
  <span class="ghost-num ghost-num--light" aria-hidden="true">03</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">IN THIS SECTION</p>
        <h2 class="section-title" id="unit-links-title">{{ tx('本單元內容', 'In this section') }}</h2>
      </div>
      <p class="section-lede">{{ tx('從球隊本身、養成系統，到通往職業與海外的每一步。', 'From the team itself and its development system to every step towards the professional game and overseas.') }}</p>
    </div>

    <div class="unit-links">
      <a class="unit-link-card clip-card" :href="lp('/zh/club/first-team/')">
        <p class="unit-link-card__num">3.1</p>
        <p v-if="!isEn" class="unit-link-card__en">First Team</p>
        <p class="unit-link-card__zh">{{ tx('一線隊', 'First Team') }}</p>
        <p class="unit-link-card__desc">{{ tx('球隊介紹、球員名單與位置篩選、教練團、賽程表（含 .ics 訂閱）、成績與積分榜、榮譽時間軸。', 'Team introduction, squad list, coaching staff, fixtures (with .ics subscription), results and league table, and the honours timeline.') }}</p>
        <span class="unit-link-card__cta">{{ tx('查看一線隊', 'View the First Team') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="unit-link-card clip-card" :href="lp('/zh/club/player-development/')">
        <p class="unit-link-card__num">3.2</p>
        <p v-if="!isEn" class="unit-link-card__en">Player Development</p>
        <p class="unit-link-card__zh">{{ tx('球員發展系統', 'Player Development') }}</p>
        <p class="unit-link-card__desc">{{ tx('技術戰術、體能、比賽判讀、心理韌性、影片分析、IDP、營養生活、教育語言，八大面向完整說明。', 'Technical and tactical work, fitness, match reading, mental resilience, video analysis, IDP, nutrition and lifestyle, and education and language: all eight areas explained.') }}</p>
        <span class="unit-link-card__cta">{{ tx('認識發展系統', 'Explore Player Development') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="unit-link-card clip-card" :href="lp('/zh/club/opportunities/')">
        <p class="unit-link-card__num">3.3</p>
        <p v-if="!isEn" class="unit-link-card__en">Player Opportunities</p>
        <p class="unit-link-card__zh">{{ tx('球員機會', 'Player Opportunities') }}</p>
        <p class="unit-link-card__desc">{{ opportunitiesDesc }}</p>
        <span class="unit-link-card__cta">{{ tx('查看機會', 'View opportunities') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="unit-link-card clip-card" :href="lp('/zh/club/international-pathways/')">
        <p class="unit-link-card__num">3.4</p>
        <p v-if="!isEn" class="unit-link-card__en">International Pathways</p>
        <p class="unit-link-card__zh">{{ tx('國際發展通道', 'International Pathways') }}</p>
        <p class="unit-link-card__desc">
          <template v-if="isEn && isTcrfc">The full route from local football to clubs abroad, regional information for Europe, Japan and Hong Kong, partner clubs, and trial scouting channels.</template>
          <template v-else-if="isEn">{{ CLUB_HUB_INTL_DESC_EN_BW }}</template>
          <template v-else-if="isTcrfc">在地到海外的完整路徑、歐洲／日本／香港分區、合作俱樂部與試訓球探管道。</template>
          <template v-else>在地到海外的真實旅外案例，日本／中國分區與海外媒合諮詢管道。</template>
        </p>
        <span class="unit-link-card__cta">{{ tx('了解國際通道', 'Explore International Pathways') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="unit-link-card clip-card" :href="lp('/zh/club/player-stories/')">
        <p class="unit-link-card__num">3.5</p>
        <p v-if="!isEn" class="unit-link-card__en">Player Stories</p>
        <p class="unit-link-card__zh">{{ tx('球員故事', 'Player Stories') }}</p>
        <p class="unit-link-card__desc">{{ playerStoriesDesc }}</p>
        <span class="unit-link-card__cta">{{ tx('閱讀球員故事', 'Read Player Stories') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
    </div>
  </div>
</section>

<section class="band grain cta-band" id="club-cta" aria-labelledby="club-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="club-cta-title">{{ ctaTitle }}</h2>
    <div class="cta-grid">
      <div class="cta-card">
        <p class="cta-card__num">10.1</p>
        <p class="cta-card__title">{{ joinPlayerCard.titleZh }}</p>
        <p class="cta-card__desc">{{ joinPlayerCard.descZh }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/player/')">{{ tx('填寫報名表', 'Fill in the registration form') }}</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">3.3</p>
        <p class="cta-card__title">{{ tx('查看試訓資訊', 'Trial information') }}</p>
        <p class="cta-card__desc">{{ tx('試訓場次日期、地點、對象與名額，一次掌握球員機會頁面。', 'Trial dates, venues, who each session is for and the places available, all on the Player Opportunities page.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/club/opportunities/')">{{ tx('前往球員機會', 'Go to Player Opportunities') }}</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">3.4</p>
        <p class="cta-card__title">{{ tx('海外發展諮詢', 'Overseas development enquiries') }}</p>
        <p class="cta-card__desc">
          <template v-if="isEn && isTcrfc">Want to know about partnership channels in Europe, Japan and Hong Kong? The International Pathways page explains the full route.</template>
          <template v-else-if="isEn">{{ CLUB_HUB_INTL_CTA_DESC_EN_BW }}</template>
          <template v-else-if="isTcrfc">想了解歐洲、日本、香港的合作管道？國際發展通道頁面說明完整路徑。</template>
          <template v-else>想了解日本、中國的旅外案例？國際發展通道頁面說明完整路徑。</template>
        </p>
        <a class="btn btn--primary" :href="lp('/zh/club/international-pathways/')">{{ tx('了解國際通道', 'Explore International Pathways') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }

/* 03 FOOTBALL CLUB landing — 單元導覽卡片
   若其他單元 landing（About / Academy / Programs...）也採同一版型，建議收進共用 CSS */
.unit-nav-band{ padding-block:clamp(4rem,7vw,6.5rem); }
.unit-nav-band .ghost-num{ right:var(--edge); top:0; }
.unit-links{
  display:grid; grid-template-columns:repeat(auto-fit,minmax(260px,1fr));
  gap:1.5rem; margin-top:2.5rem;
}
.unit-link-card{
  background:var(--paper); border:1px solid var(--rule);
  padding:2rem 1.85rem 2.25rem; display:flex; flex-direction:column; gap:.6rem;
  transition:transform var(--dur-fast) var(--ease), border-color var(--dur-fast) var(--ease);
}
.unit-link-card:hover{ transform:translateY(-4px); border-color:var(--brand-aa); }
.unit-link-card__num{ font-size:.72rem; font-weight:800; letter-spacing:.08em; color:var(--brand-aa); }
.unit-link-card__en{ font-size:.72rem; font-weight:800; letter-spacing:.1em; text-transform:uppercase; color:var(--muted); }
.unit-link-card__zh{ font-size:1.35rem; font-weight:900; letter-spacing:-.01em; color:var(--heading); }
.unit-link-card__desc{ font-size:.85rem; line-height:1.65; color:var(--muted); flex:1; }
.unit-link-card__cta{
  font-size:.82rem; font-weight:700; color:var(--brand-aa);
  display:inline-flex; align-items:center; gap:.4em; margin-top:.4rem;
}
.unit-link-card__cta svg{ width:14px; height:14px; transition:transform var(--dur-fast) var(--ease); }
.unit-link-card:hover .unit-link-card__cta svg{ transform:translateX(4px); }
</style>
