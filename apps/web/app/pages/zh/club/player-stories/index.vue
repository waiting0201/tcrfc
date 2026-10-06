<script setup lang="ts">
// app/pages/zh/club/player-stories/index.vue — 由 site/src/pages/zh/club/player-stories/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
// ⛔ 原頁 <script>（類別篩選 chips）改寫為 onMounted，行為逐字等價。
//
// S2-8（2026-09-29）：藍鯨規劃書 §3.3「一線隊」明文「沿用主站 03 的球員卡、球員頁與
// 球員故事版型」，本頁對藍鯨維持開放（版型承諾沿用），但沒有任何已核實、已取得肖像
// 同意的藍鯨球員故事案例可用——藍鯨版改為空狀態（0 案例），不得挪用磐石球員（孫恩祈／
// 山內大空／楊朝景）充數，見 club-copy.ts「03.5 球員故事」節。
definePageMeta({ nav: 'club', unit: '3.5', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

// 英文版：磐石用 `*En`，藍鯨用 `*EnBw`／`*_EN_BW`（club-copy-en-club.ts 檔頭）。
const hero = computed(() => (isEn.value ? (isTcrfc.value ? getPlayerStoriesHeroEn() : getPlayerStoriesHeroEnBw()) : getPlayerStoriesHero(clubKey.value)))
const emptyNote = computed(() => (isEn.value ? CLUB_PLAYER_STORIES_EMPTY_NOTE_EN_BW : getPlayerStoriesEmptyNote(clubKey.value)))

// 稽核 B-1：主內文改讀頁面管理（有已發布區塊用 CMS，否則維持下方寫死內容；hero／麵包屑／CTA 不變）
const cms = await useCmsPage('club/player-stories')
cms.applySeo({
  title: computed(() => (isEn.value ? (isTcrfc.value ? getPlayerStoriesSeoEn() : getPlayerStoriesSeoEnBw()) : getPlayerStoriesSeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? (isTcrfc.value ? getPlayerStoriesSeoEn() : getPlayerStoriesSeoEnBw()) : getPlayerStoriesSeo(clubKey.value)).description),
})

onMounted(() => {
  const chips = document.querySelectorAll<HTMLButtonElement>('#stories .pos-chip')
  const cards = document.querySelectorAll<HTMLElement>('#story-grid .story-card')
  chips.forEach((chip) => {
    chip.addEventListener('click', () => {
      chips.forEach((c) => c.setAttribute('aria-pressed', 'false'))
      chip.setAttribute('aria-pressed', 'true')
      const cat = chip.dataset.catFilter
      cards.forEach((card) => {
        card.hidden = !(cat === 'ALL' || card.dataset.cat === cat)
      })
    })
  })
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/club/')">{{ tx('俱樂部', 'Football Club') }}</a></li>
      <li aria-current="page">{{ tx('球員故事', 'Player Stories') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/trencin-02.jpg')" width="1920" height="1279" />
  <div class="container">
    <p class="page-hero__eyebrow">3.5 Player Stories</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<ContentCmsPageBand v-if="cms.active.value" :blocks="cms.blocks.value" :label="tx('球員故事案例', 'Player story cases')" :zh-fallback="cms.hasZhFallback.value" />
<template v-else>
<section class="band" id="stories" aria-labelledby="stories-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="stories-title">{{ tx('球員故事案例', 'Player story cases') }}</h2>

    <template v-if="isTcrfc">
      <div class="pos-filter" role="group" :aria-label="tx('依類別篩選球員故事', 'Filter player stories by category')">
        <button type="button" class="pos-chip" data-cat-filter="ALL" aria-pressed="true">{{ tx('全部 3', 'All 3') }}</button>
        <button type="button" class="pos-chip" data-cat-filter="academy" aria-pressed="false">{{ tx('學院 0', 'Academy 0') }}</button>
        <button type="button" class="pos-chip" data-cat-filter="first-team" aria-pressed="false">{{ tx('一線隊 2', 'First Team 2') }}</button>
        <button type="button" class="pos-chip" data-cat-filter="overseas" aria-pressed="false">{{ tx('海外 1', 'Overseas 1') }}</button>
        <button type="button" class="pos-chip" data-cat-filter="womens" aria-pressed="false">{{ tx('女足 0', 'Women\'s 0') }}</button>
      </div>

      <div class="story-grid" id="story-grid">
        <article class="story-card clip-card" data-cat="first-team">
          <div class="story-card__visual">
            <img class="story-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="story-card__num">6</span>
          </div>
          <div class="story-card__body">
            <span class="story-card__tag">{{ tx('一線隊 First Team', 'First Team') }}</span>
            <p class="story-card__name">孫恩祈<span class="story-card__pos">{{ tx('後衛 DF · 背號 6', 'Defender (DF) · No. 6') }}</span></p>

          </div>
        </article>

        <article class="story-card clip-card" data-cat="first-team">
          <div class="story-card__visual">
            <img class="story-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="story-card__num">44</span>
          </div>
          <div class="story-card__body">
            <span class="story-card__tag">{{ tx('一線隊 First Team', 'First Team') }}</span>
            <p class="story-card__name">山內大空<span class="story-card__pos">{{ tx('前鋒 FW · 背號 44', 'Forward (FW) · No. 44') }}</span></p>

          </div>
        </article>

        <article class="story-card clip-card" data-cat="overseas">
          <div class="story-card__visual">
            <img class="story-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="story-card__num">11</span>
          </div>
          <div class="story-card__body">
            <span class="story-card__tag">{{ tx('海外 Overseas', 'Overseas') }}</span>
            <p class="story-card__name">楊朝景<span class="story-card__pos">{{ tx('中場 MF · 背號 11 · 現效力香港九龍城', 'Midfielder (MF) · No. 11 · currently playing for Kowloon City in Hong Kong') }}</span></p>

          </div>
        </article>
      </div>

      <div class="story-empty-note">
        <p v-if="isEn">There are no player stories in the <b>Academy</b> or <b>Women's</b> categories yet. {{ CLUB_PLAYER_STORIES_BW_PREFIX_EN }}<a href="https://www.tcbw2014.com/" target="_blank" rel="noopener">{{ CLUB_PLAYER_STORIES_BW_LINK_TEXT_EN }}</a> (opens in a new tab).</p>
        <p v-else><b>學院</b>與<b>女足</b>類別目前尚無已建立的球員故事案例。台中藍鯨的球員名單與賽程請見<a href="https://www.tcbw2014.com/" target="_blank" rel="noopener">台中藍鯨女子隊官網</a>（另開新分頁）。</p>
      </div>
    </template>

    <!-- 藍鯨版：無真實、已核實的球員故事案例可顯示，維持空狀態（見 club-copy.ts 說明），
         不挪用磐石球員案例充數。 -->
    <div v-else class="story-empty-note">
      <p>{{ emptyNote }}</p>
    </div>
  </div>
</section>
</template>

<section class="band grain cta-band" id="stories-cta" aria-labelledby="stories-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="stories-cta-title">{{ tx('分享你的故事', 'Share your story') }}</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">{{ tx('認識一線隊', 'Meet the First Team') }}</p>
        <p class="cta-card__desc">{{ tx('查看完整球員名單、教練團與本季賽程。', 'See the full squad list, the coaching staff and this season\'s fixtures.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/')">{{ tx('前往一線隊', 'Go to the First Team') }}</a>
      </div>
      <!-- BW-C1：3.4 已重開（見 shared/utils/units.ts 檔頭），移除既有的 isTcrfc 隱藏。 -->
      <div class="cta-card">
        <p class="cta-card__num">3.4</p>
        <p class="cta-card__title">{{ tx('國際發展通道', 'International Pathways') }}</p>
        <p class="cta-card__desc">
          <template v-if="isEn && isTcrfc">Learn about the full route players take to Europe, Japan and Hong Kong.</template>
          <template v-else-if="isEn">{{ CLUB_PLAYER_STORIES_INTL_CARD_DESC_EN_BW }}</template>
          <template v-else-if="isTcrfc">了解球員通往歐洲、日本、香港的完整路徑。</template>
          <template v-else>了解球員旅外日本、中國的真實案例。</template>
        </p>
        <a class="btn btn--primary" :href="lp('/zh/club/international-pathways/')">{{ tx('查看國際通道', 'View International Pathways') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 3.5 球員故事 — story-card / pos-filter（與 3.1 相同結構，重複定義於本頁樣式區）
   story-card 與 player-card 為近親元件，建議之後合併收進共用 CSS */
.pos-filter{ display:flex; gap:.6rem; flex-wrap:wrap; margin:2rem 0 2.25rem; }
.pos-chip{
  min-height:42px; padding:0 1.15rem; font-weight:800; font-size:.82rem;
  border:2px solid var(--rule); color:var(--muted); background:var(--paper);
  transition:all var(--dur-fast) var(--ease);
}
.pos-chip:hover{ border-color:var(--brand-aa); color:var(--brand-deep); }
.pos-chip[aria-pressed="true"]{ background:var(--brand-aa); border-color:var(--brand-aa); color:#fff; }

.story-grid{ display:grid; grid-template-columns:repeat(auto-fit,minmax(280px,1fr)); gap:1.5rem; }
.story-card{ background:var(--paper-2); display:flex; flex-direction:column; }
.story-card[hidden]{ display:none; }
.story-card__visual{
  position:relative; aspect-ratio:16/9; background:var(--paper-3);
  display:flex; align-items:center; justify-content:center; overflow:hidden;
}
.story-card__crest{ position:absolute; inset:0; margin:auto; width:34%; height:34%; object-fit:contain; opacity:.06; }
.story-card__num{ position:relative; font-size:3rem; font-weight:900; color:var(--brand-aa); font-variant-numeric:tabular-nums; }
.story-card__body{ padding:1.25rem 1.35rem 1.5rem; display:flex; flex-direction:column; gap:.6rem; }
.story-card__tag{ font-size:.66rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--brand-aa); }
.story-card__name{ font-size:1.15rem; font-weight:900; color:var(--heading); display:flex; flex-direction:column; gap:.3rem; }
.story-card__pos{ font-size:.78rem; font-weight:600; color:var(--muted); }
.story-card .pending{ font-size:.8rem; }

.story-empty-note{
  margin-top:2rem; padding-top:1.5rem; border-top:1px solid var(--rule);
  font-size:.85rem; color:var(--muted); line-height:1.7;
}
.story-empty-note a{ color:var(--brand-aa); font-weight:700; }
</style>
