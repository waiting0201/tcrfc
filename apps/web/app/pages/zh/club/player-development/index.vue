<script setup lang="ts">
// app/pages/zh/club/player-development/index.vue — 由 site/src/pages/zh/club/player-development/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（S2-8 曾以「沒有具名『系統』框架」為由整頁
// 404，是誤用——藍鯨規劃書 §1.3 總則例外只有四項，不含 3.2，見 shared/utils/units.ts
// 檔頭）。八大主題本身是通用足球培訓詞彙，兩俱樂部共用；每個模組詳細內容本來就是
// 「準備中」佔位文字（磐石版也是）。改為讀 getPlayerDevelopmentSeo()／
// getPlayerDevelopmentHero()，藍鯨版避免使用「系統」這個暗示已建制機構框架的用詞。
definePageMeta({ nav: 'club', unit: '3.2', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

// 稽核 B-1：主內文改讀頁面管理（有已發布區塊用 CMS，否則維持下方寫死內容；hero／麵包屑／CTA 不變）
const cms = await useCmsPage('club/player-development')
cms.applySeo({
  title: computed(() => (isEn.value ? (isTcrfc.value ? getPlayerDevelopmentSeoEn() : getPlayerDevelopmentSeoEnBw()) : getPlayerDevelopmentSeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? (isTcrfc.value ? getPlayerDevelopmentSeoEn() : getPlayerDevelopmentSeoEnBw()) : getPlayerDevelopmentSeo(clubKey.value)).description),
})
// 英文版：磐石用 `*En`，藍鯨用 `*EnBw`／`*_EN_BW`（club-copy-en-club.ts 檔頭）。
const hero = computed(() => (isEn.value ? (isTcrfc.value ? getPlayerDevelopmentHeroEn() : getPlayerDevelopmentHeroEnBw()) : getPlayerDevelopmentHero(clubKey.value)))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/club/')">{{ tx('俱樂部', 'Football Club') }}</a></li>
      <li aria-current="page">{{ isEn ? hero.h1Zh : `球員發展${clubKey === 'bw' ? '重點' : '系統'}` }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無已核實可用的訓練場景照片可用，不沿用磐石照片頂替（比照 academy/pathway.vue 既有做法） -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/trencin-05.jpg')" alt="" width="1920" height="1279">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ clubKey === 'bw' ? '3.2' : '3.2 Player Development' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<ContentCmsPageBand v-if="cms.active.value" :blocks="cms.blocks.value" :label="tx('球員發展系統', 'Player Development')" :zh-fallback="cms.hasZhFallback.value" />
<template v-else>
<section class="band" id="modules" aria-labelledby="modules-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="modules-title">{{ tx('八大發展模組', 'The eight development modules') }}</h2>

    <div class="module-grid">
      <details class="module-card">
        <summary>
          <span class="module-card__num">01</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><circle cx="12" cy="12" r="3"/><path d="M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M5.6 18.4l2.1-2.1M16.3 7.7l2.1-2.1"/></svg>
          <span class="module-card__title">{{ tx('技術戰術分析', 'Technical & Tactical Analysis') }}<span v-if="!isEn" class="en">Technical &amp; Tactical Analysis</span></span>
          <svg class="module-card__chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
        </summary>
        <div class="module-card__detail">
          <p>{{ tx('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.') }}</p>
        </div>
      </details>

      <details class="module-card">
        <summary>
          <span class="module-card__num">02</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M6 20V10M12 20V4M18 20v-7"/></svg>
          <span class="module-card__title">{{ tx('體能訓練', 'Physical Fitness Training') }}<span v-if="!isEn" class="en">Physical Fitness Training</span></span>
          <svg class="module-card__chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
        </summary>
        <div class="module-card__detail">
          <p>{{ tx('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.') }}</p>
        </div>
      </details>

      <details class="module-card">
        <summary>
          <span class="module-card__num">03</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><circle cx="12" cy="12" r="8"/><path d="M12 8v4l3 2"/></svg>
          <span class="module-card__title">{{ tx('比賽判讀', 'Match Reading') }}<span v-if="!isEn" class="en">Match Reading</span></span>
          <svg class="module-card__chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
        </summary>
        <div class="module-card__detail">
          <p>{{ tx('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.') }}</p>
        </div>
      </details>

      <details class="module-card">
        <summary>
          <span class="module-card__num">04</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M12 21s-7-4.35-9-9a5 5 0 0 1 9-3 5 5 0 0 1 9 3c-2 4.65-9 9-9 9z"/></svg>
          <span class="module-card__title">{{ tx('心理韌性', 'Mental Resilience') }}<span v-if="!isEn" class="en">Mental Resilience</span></span>
          <svg class="module-card__chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
        </summary>
        <div class="module-card__detail">
          <p>{{ tx('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.') }}</p>
        </div>
      </details>

      <details class="module-card">
        <summary>
          <span class="module-card__num">05</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><rect x="3" y="5" width="18" height="14" rx="1"/><path d="M10 9l5 3-5 3z"/></svg>
          <span class="module-card__title">{{ tx('影片分析', 'Video Analysis') }}<span v-if="!isEn" class="en">Video Analysis</span></span>
          <svg class="module-card__chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
        </summary>
        <div class="module-card__detail">
          <p>{{ tx('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.') }}</p>
        </div>
      </details>

      <details class="module-card">
        <summary>
          <span class="module-card__num">06</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M4 19V6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v13"/><path d="M4 19h16M9 10h6"/></svg>
          <span class="module-card__title">{{ tx('IDP 個人發展計畫', 'Individual Development Plan') }}<span v-if="!isEn" class="en">Individual Development Plan</span></span>
          <svg class="module-card__chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
        </summary>
        <div class="module-card__detail">
          <p>{{ tx('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.') }}</p>
        </div>
      </details>

      <details class="module-card">
        <summary>
          <span class="module-card__num">07</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M12 2a5 5 0 0 0-5 5c0 3 2 4 2 7h6c0-3 2-4 2-7a5 5 0 0 0-5-5z"/><path d="M9 21h6M10 18h4"/></svg>
          <span class="module-card__title">{{ tx('營養與生活', 'Nutrition & Lifestyle') }}<span v-if="!isEn" class="en">Nutrition &amp; Lifestyle</span></span>
          <svg class="module-card__chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
        </summary>
        <div class="module-card__detail">
          <p>{{ tx('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.') }}</p>
        </div>
      </details>

      <details class="module-card">
        <summary>
          <span class="module-card__num">08</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M12 3L2 8l10 5 10-5-10-5z"/><path d="M6 10.5V16c0 1.5 3 3 6 3s6-1.5 6-3v-5.5"/></svg>
          <span class="module-card__title">{{ tx('教育與語言', 'Education & Language') }}<span v-if="!isEn" class="en">Education &amp; Language</span></span>
          <svg class="module-card__chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
        </summary>
        <div class="module-card__detail">
          <p>{{ tx('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.') }}</p>
        </div>
      </details>

      <div class="module-card module-card--summary">
        <p class="module-card__summary-kicker">WHY IT MATTERS</p>
        <p class="module-card__summary-title">{{ isEn ? (isTcrfc ? 'Eight modules, one complete system' : CLUB_PLAYER_DEVELOPMENT_PAGE_EN_BW.summaryTitle) : (isTcrfc ? '八大模組，一套完整體系' : '八大面向，持續培育選手') }}</p>
        <p class="module-card__summary-desc">
          <template v-if="isEn && !isTcrfc">{{ CLUB_PLAYER_DEVELOPMENT_PAGE_EN_BW.summaryDesc }}</template>
          <template v-else-if="isEn">The eight modules together form the development framework for First Team and age-group players, connecting Academy training with International Pathways as the core support system for players heading to the professional stage.</template>
          <template v-else-if="isTcrfc">八大模組共同構成一線隊與各梯隊球員的養成框架，銜接學院訓練與國際發展通道，是選手邁向職業舞台的核心支持系統。</template>
          <template v-else>八大面向共同支持一線隊與青年隊球員的成長，銜接青年隊訓練與國際發展通道，協助選手持續進步。</template>
        </p>
        <a class="module-card__summary-link" :href="lp('/zh/club/international-pathways/')">{{ tx('查看國際發展通道 →', 'View International Pathways →') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<section class="band grain cta-band" id="pd-cta" aria-labelledby="pd-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="pd-cta-title">{{ isEn ? (isTcrfc ? 'Join Player Development' : CLUB_PLAYER_DEVELOPMENT_PAGE_EN_BW.ctaHeading) : (isTcrfc ? '加入球員發展系統' : '加入球員培育重點') }}</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">{{ tx('認識一線隊', 'Meet the First Team') }}</p>
        <p class="cta-card__desc">{{ isEn ? (isTcrfc ? 'See the First Team squad and season performances that Player Development supports.' : CLUB_PLAYER_DEVELOPMENT_PAGE_EN_BW.firstTeamDesc) : (isTcrfc ? '看看球員發展系統支持的一線隊陣容與賽季表現。' : '看看一線隊陣容與賽季表現。') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/')">{{ tx('前往一線隊', 'Go to the First Team') }}</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">10.1</p>
        <p class="cta-card__title">{{ tx('加入球隊', 'Join the Squad') }}</p>
        <p class="cta-card__desc">{{ isEn && !isTcrfc ? CLUB_PLAYER_DEVELOPMENT_PAGE_EN_BW.joinDesc : tx('想成為體系內的一員？填寫報名表，開始你的旅程。', 'Want to be part of the system? Fill in the registration form and start your journey.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/player/')">{{ tx('填寫報名表', 'Fill in the registration form') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（BW-C1 新增，沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }

/* 3.2 球員發展系統 — 九宮格模組卡片（<details> 展開）
   與 3.1 player-card、3.5 story-card 為同系列淺底卡片元件，建議未來一併收進共用 CSS */
.module-grid{
  display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:1.25rem;
}
.module-card{
  background:var(--paper-2); border:1px solid var(--rule);
}
.module-card summary{
  list-style:none; cursor:pointer; padding:1.5rem 1.5rem;
  display:grid; grid-template-columns:auto auto 1fr auto; align-items:center; gap:.9rem;
}
.module-card summary::-webkit-details-marker{ display:none; }
.module-card__num{ font-size:.72rem; font-weight:800; color:var(--brand-aa); letter-spacing:.06em; }
.module-card summary svg:not(.module-card__chev){ width:24px; height:24px; color:var(--brand-deep); flex:none; }
.module-card__title{ font-size:1rem; font-weight:800; color:var(--heading); line-height:1.4; }
.module-card__title .en{ display:block; font-size:.7rem; font-weight:700; color:var(--muted); text-transform:uppercase; letter-spacing:.04em; margin-top:.2rem; }
.module-card__chev{ width:18px; height:18px; color:var(--muted); transition:transform var(--dur-fast) var(--ease); flex:none; }
.module-card[open] .module-card__chev{ transform:rotate(180deg); }
.module-card__detail{ padding:0 1.5rem 1.5rem; }

.module-card--summary{
  background:var(--ink); color:#fff; padding:1.75rem 1.6rem; display:flex; flex-direction:column; gap:.6rem; justify-content:center;
}
.module-card__summary-kicker{ font-size:.68rem; font-weight:800; letter-spacing:.1em; text-transform:uppercase; color:var(--brand-bright); }
.module-card__summary-title{ font-size:1.15rem; font-weight:900; letter-spacing:-.01em; }
.module-card__summary-desc{ font-size:.82rem; color:var(--muted-dark); line-height:1.6; }
.module-card__summary-link{ font-size:.82rem; font-weight:700; color:var(--brand-bright); margin-top:.3rem; }

@media (max-width:900px){ .module-grid{ grid-template-columns:repeat(2,minmax(0,1fr)); } }
@media (max-width:600px){
  .module-grid{ grid-template-columns:1fr; }
  .module-card summary{ grid-template-columns:auto 1fr auto; }
  .module-card__num{ display:none; }
}
</style>
