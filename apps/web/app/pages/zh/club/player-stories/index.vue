<script setup lang="ts">
// app/pages/zh/club/player-stories/index.vue — 由 site/src/pages/zh/club/player-stories/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
// ⛔ 原頁 <script>（類別篩選 chips）改寫為 onMounted，行為逐字等價。
//
// S2-8（2026-09-29）：藍鯨規劃書 §3.3「一線隊」明文「沿用主站 03 的球員卡、球員頁與
// 球員故事版型」，本頁對藍鯨維持開放（版型承諾沿用），但沒有任何已核實、已取得肖像
// 同意的藍鯨球員故事案例可用——藍鯨版改為空狀態（0 案例），不得挪用磐石球員（孫恩祈／
// 山內大空／楊朝景）充數，見 club-copy.ts「03.5 球員故事」節。
definePageMeta({ nav: 'club', unit: '3.5' })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

const hero = computed(() => getPlayerStoriesHero(clubKey.value))
const emptyNote = computed(() => getPlayerStoriesEmptyNote(clubKey.value))

useSeoMeta({
  title: computed(() => getPlayerStoriesSeo(clubKey.value).title),
  description: computed(() => getPlayerStoriesSeo(clubKey.value).description),
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
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/club/')">俱樂部</a></li>
      <li aria-current="page">球員故事</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg src="/assets/img/trencin-02.jpg" width="1920" height="1279" />
  <div class="container">
    <p class="page-hero__eyebrow">3.5 Player Stories</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band" id="stories" aria-labelledby="stories-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="stories-title">球員故事案例</h2>

    <template v-if="isTcrfc">
      <div class="pos-filter" role="group" aria-label="依類別篩選球員故事">
        <button type="button" class="pos-chip" data-cat-filter="ALL" aria-pressed="true">全部 3</button>
        <button type="button" class="pos-chip" data-cat-filter="academy" aria-pressed="false">學院 0</button>
        <button type="button" class="pos-chip" data-cat-filter="first-team" aria-pressed="false">一線隊 2</button>
        <button type="button" class="pos-chip" data-cat-filter="overseas" aria-pressed="false">海外 1</button>
        <button type="button" class="pos-chip" data-cat-filter="womens" aria-pressed="false">女足 0</button>
      </div>

      <div class="story-grid" id="story-grid">
        <article class="story-card clip-card" data-cat="first-team">
          <div class="story-card__visual">
            <img class="story-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="story-card__num">6</span>
          </div>
          <div class="story-card__body">
            <span class="story-card__tag">一線隊 First Team</span>
            <p class="story-card__name">孫恩祈<span class="story-card__pos">後衛 DF · 背號 6</span></p>

          </div>
        </article>

        <article class="story-card clip-card" data-cat="first-team">
          <div class="story-card__visual">
            <img class="story-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="story-card__num">44</span>
          </div>
          <div class="story-card__body">
            <span class="story-card__tag">一線隊 First Team</span>
            <p class="story-card__name">山內大空<span class="story-card__pos">前鋒 FW · 背號 44</span></p>

          </div>
        </article>

        <article class="story-card clip-card" data-cat="overseas">
          <div class="story-card__visual">
            <img class="story-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="story-card__num">11</span>
          </div>
          <div class="story-card__body">
            <span class="story-card__tag">海外 Overseas</span>
            <p class="story-card__name">楊朝景<span class="story-card__pos">中場 MF · 背號 11 · 現效力香港九龍城</span></p>

          </div>
        </article>
      </div>

      <div class="story-empty-note">
        <p><b>學院</b>與<b>女足</b>類別目前尚無已建立的球員故事案例。台中藍鯨的球員名單與賽程請見<a href="https://www.tcbw2014.com/" target="_blank" rel="noopener">台中藍鯨女子隊官網</a>（另開新分頁）。</p>
      </div>
    </template>

    <!-- 藍鯨版：無真實、已核實的球員故事案例可顯示，維持空狀態（見 club-copy.ts 說明），
         不挪用磐石球員案例充數。 -->
    <div v-else class="story-empty-note">
      <p>{{ emptyNote }}</p>
    </div>
  </div>
</section>

<section class="band grain cta-band" id="stories-cta" aria-labelledby="stories-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="stories-cta-title">分享你的故事</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">認識一線隊</p>
        <p class="cta-card__desc">查看完整球員名單、教練團與本季賽程。</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/')">前往一線隊</a>
      </div>
      <!-- BW-C1：3.4 已重開（見 shared/utils/units.ts 檔頭），移除既有的 isTcrfc 隱藏。 -->
      <div class="cta-card">
        <p class="cta-card__num">3.4</p>
        <p class="cta-card__title">國際發展通道</p>
        <p class="cta-card__desc">
          <template v-if="isTcrfc">了解球員通往歐洲、日本、香港的完整路徑。</template>
          <template v-else>了解球員旅外日本、中國的真實案例。</template>
        </p>
        <a class="btn btn--primary" :href="lp('/zh/club/international-pathways/')">查看國際通道</a>
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
