<script setup lang="ts">
// app/pages/zh/culture/index.vue — 由 site/src/pages/zh/culture/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: 'culture', unit: '08', enReady: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubIdentity()／getClubAssets()。本頁只做
// 單元 hub 導覽卡的文字換名。藍鯨規劃書 v1.9 §2.1（行 136）：08 不設 8.1 漫畫，
// 8.1 卡片與所有漫畫字樣對藍鯨一律不出現（由 isUnitEnabledForClub('8.1') 決定，
// 與選單、sitemap、llms.txt 同一個開關）；8.2／8.3 比照主站，內容由後台提供。
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(config.public.club)))
const clubAssets = computed(() => getClubAssets(config.public.club))
const isTcrfc = computed(() => config.public.club !== 'bw')
const showManga = computed(() => isUnitEnabledForClub('8.1', config.public.club))

useSeoMeta({
  title: computed(() => (isEn.value ? getCultureHubSeoEn(showManga.value).title : showManga.value
    ? `${identity.value.cultureLabelZh} Culture｜漫畫、球迷會、官方商品｜${clubAssets.value.nameZh}`
    : `${identity.value.cultureLabelZh} Culture｜球迷會、官方商品｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? getCultureHubSeoEn(showManga.value).description : showManga.value
    ? `認識${clubAssets.value.nameZh}的文化單元：免費線上閱讀的${clubAssets.value.shortNameZh}漫畫、${clubAssets.value.shortNameZh}球迷會入會與福利，以及官方商品與線上商店。`
    : `認識${clubAssets.value.nameZh}的文化單元：${clubAssets.value.shortNameZh}球迷會、官方商品與線上商店，以及特約店家。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ identity.cultureLabelZh }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-culture.jpg')" width="1600" height="900" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '08 TCRFC Culture' : '08 Culture' }}</p>
    <h1>{{ identity.cultureLabelZh }}<span v-if="!isEn" class="en">Culture</span></h1>
    <p v-if="isEn && showManga" class="page-hero__lede">From the manga world and the Fan Club to official merchandise, TCRFC Culture is the most direct emotional link between Taichung Rock, its fans and the community.</p>
    <p v-else-if="isEn" class="page-hero__lede">From the Fan Club and official merchandise to partner perks, TCRFC Culture is the most direct emotional link between Taichung Rock, its fans and the community.</p>
    <p v-else-if="showManga" class="page-hero__lede">從漫畫世界觀、球迷會到官方商品，{{ identity.cultureLabelZh }}是{{ clubAssets.shortNameZh }}與球迷、社群之間最直接的情感連結。</p>
    <p v-else class="page-hero__lede">從球迷會、官方商品到特約店家，{{ identity.cultureLabelZh }}是{{ clubAssets.shortNameZh }}與球迷、社群之間最直接的情感連結。</p>
  </div>
</section>

<!-- SPEC 3.8 — 單元 landing：導覽卡連往 8.1–8.3 -->
<section class="band" aria-labelledby="culture-hub-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">EXPLORE</p>
        <h2 class="section-title" id="culture-hub-title">{{ isEn ? `${identity.cultureLabelZh} sections` : `${identity.cultureLabelZh}${showManga ? '三大單元' : '單元'}` }}</h2>
      </div>
      <p v-if="isEn" class="section-lede">Select a card to learn more about {{ showManga ? 'TCRFC Manga, the Fan Club and official merchandise' : 'the Fan Club and official merchandise' }}.</p>
      <p v-else-if="showManga" class="section-lede">點選卡片深入了解{{ clubAssets.shortNameZh }}漫畫、球迷會與官方商品。</p>
      <p v-else class="section-lede">點選卡片深入了解{{ clubAssets.shortNameZh }}球迷會與官方商品。</p>
    </div>

    <div class="unit-grid">
      <a v-if="showManga" class="unit-card" :href="lp('/zh/culture/manga/')">
        <div class="unit-card__scrim" aria-hidden="true"></div>
        <div class="unit-card__body">
          <p class="unit-card__num">8.1</p>
          <p class="unit-card__title">{{ isEn ? 'TCRFC Manga' : `${clubAssets.shortNameZh}漫畫` }}<span v-if="!isEn" class="en">{{ isTcrfc ? 'TCRFC Manga' : 'Manga' }}</span></p>
          <p class="unit-card__desc">{{ tx('世界觀企劃、角色卡牆與集數線上閱讀器——全部免費，不需登入。', 'A world-building project, a character wall and an online episode reader, all free with no login required.') }}</p>
          <span class="unit-card__link">{{ tx('開始閱讀', 'Start reading') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
        </div>
      </a>
      <a class="unit-card" :href="lp('/zh/culture/fan-club/')">
        <img v-if="isTcrfc" :src="siteImg('/assets/img/fanclub/fanclub-event-04.jpg')" alt="" loading="lazy" width="1600" height="1067">
        <div class="unit-card__scrim" aria-hidden="true"></div>
        <div class="unit-card__body">
          <p class="unit-card__num">8.2</p>
          <p class="unit-card__title">{{ isEn ? 'TCRFC Fan Club' : `${clubAssets.shortNameZh}球迷會` }}<span v-if="!isEn" class="en">Fan Club</span></p>
          <p class="unit-card__desc">{{ tx('付費會籍方案、會員權益對照與球迷活動報名、回顧。', 'Paid membership plans, a comparison of member benefits, and fan event registration and reviews.') }}</p>
          <span class="unit-card__link">{{ tx('加入球迷會', 'Join the Fan Club') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
        </div>
      </a>
      <a class="unit-card" :href="lp('/zh/culture/merchandise/')">
        <img v-if="isTcrfc" :src="siteImg('/assets/img/merch/merch-jersey-01.jpg')" alt="" loading="lazy" width="1600" height="1067">
        <div class="unit-card__scrim" aria-hidden="true"></div>
        <div class="unit-card__body">
          <p class="unit-card__num">8.3</p>
          <p class="unit-card__title">{{ tx('官方商品', 'Merchandise') }}<span v-if="!isEn" class="en">Merchandise</span></p>
          <p v-if="isEn" class="unit-card__desc">Official merchandise in three collections (Club, Academy and Fan), available in the online store.</p>
          <p v-else-if="isTcrfc" class="unit-card__desc">俱樂部、{{ identity.academyShortLabelZh }}、球迷三大系列官方商品，可於站內商店選購。</p>
          <p v-else class="unit-card__desc">官方商品與線上商店，商品內容由後台提供。</p>
          <span class="unit-card__link">{{ tx('看商品', 'View merchandise') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
        </div>
      </a>
      <a class="unit-card" :href="lp('/zh/perks/')">
        <div class="unit-card__scrim" aria-hidden="true"></div>
        <div class="unit-card__body">
          <p class="unit-card__num">8.4</p>
          <p class="unit-card__title">{{ tx('特約店家', 'Partner Perks') }}<span v-if="!isEn" class="en">Partner Perks</span></p>
          <p class="unit-card__desc">{{ tx('會員到店出示電子會員卡即享折扣，依店家標示適用一般或付費會員。', 'Members enjoy discounts by showing their digital membership card in store; each store states whether it applies to registered or paid members.') }}</p>
          <span class="unit-card__link">{{ tx('看店家清單', 'View partner stores') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
        </div>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 單元 landing 導覽卡（culture/ 與 partners/ 共用寫法，建議收進共用 CSS） */
.unit-grid{
  display:grid; grid-template-columns:repeat(auto-fit,minmax(280px,1fr)); gap:1.5rem; margin-top:2.5rem;
}
.unit-card{
  position:relative; display:flex; flex-direction:column; justify-content:flex-end;
  min-height:340px; background:var(--ink-2); color:#fff; overflow:hidden; text-decoration:none;
  clip-path:polygon(0 0,100% 0,100% calc(100% - 28px),calc(100% - 28px) 100%,0 100%);
  transition:transform var(--dur) var(--ease);
}
.unit-card:hover{ transform:translateY(-4px); }
.unit-card::after{
  content:""; position:absolute; right:0; bottom:0; width:34px; height:34px;
  background:var(--ink-trim-dk); clip-path:polygon(100% 0,100% 100%,0 100%); z-index:2;
}
.unit-card img{ position:absolute; inset:0; width:100%; height:100%; object-fit:cover; opacity:.6; transition:transform .6s var(--ease); }
.unit-card:hover img{ transform:scale(1.08); }
.unit-card__scrim{ position:absolute; inset:0; background:linear-gradient(0deg,rgba(35,25,22,.96) 0%,rgba(35,25,22,.6) 55%,rgba(35,25,22,.2) 100%); }
.unit-card__body{ position:relative; z-index:1; padding:1.85rem 1.6rem; }
.unit-card__num{ font-size:.72rem; font-weight:800; letter-spacing:.08em; color:var(--brand); text-transform:uppercase; }
.unit-card__title{ font-size:1.4rem; font-weight:900; margin-top:.5rem; letter-spacing:-.01em; }
.unit-card__title .en{ display:block; font-size:.6em; font-weight:700; letter-spacing:.1em; text-transform:uppercase; color:var(--muted-dark); margin-top:.25em; }
.unit-card__desc{ font-size:.85rem; color:var(--muted-dark); margin-top:.6rem; line-height:1.6; }
.unit-card__link{ display:inline-flex; align-items:center; gap:.4em; margin-top:1.1rem; font-weight:700; font-size:.85rem; color:#fff; }
.unit-card__link svg{ width:14px; height:14px; transition:transform var(--dur-fast) var(--ease); }
.unit-card:hover .unit-card__link svg{ transform:translateX(4px); }
</style>
