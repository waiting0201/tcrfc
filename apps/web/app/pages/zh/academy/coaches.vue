<script setup lang="ts">
// app/pages/zh/academy/coaches.vue — 由 site/src/pages/zh/academy/coaches/index.html 轉來（S0-9 靜態頁搬遷）
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（S2-8 曾以「沒有已核實、非過期的藍鯨青年隊
// 教練名單」為由整頁 404，是誤用——藍鯨規劃書 §1.3 總則例外只有四項，不含 4.5，見
// shared/utils/units.ts 檔頭）。教練名單本身仍缺（`content/blue-whale/squad/
// coaching-staff.md` 標明「舊站教練經歷最新只到 2024，2025 賽季未更新」且無照片，
// 拿可能已過期的名單當作現在的青年隊教練公開展示風險與臆造相近），這是「此頁此
// 區塊內容缺漏」不是「整頁不存在」，改為顯示既有「收錄中」空狀態（比照 3.5 球員
// 故事），不是 404。標題改「青年隊教練團」（不用「學院」字樣，見
// check-club-brand-leak.mjs 詞表；04 對藍鯨依 docs/13 §3 一律稱「青年隊」）。
definePageMeta({ nav: "academy", unit: "4.5" })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))

// S1-12d 收尾：梯隊代碼改讀 useSiteFacts(clubKey)（後端公開端點）。
const { academyLabel } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => getYouthCoachesSeo(clubKey.value).title),
  description: computed(() => getYouthCoachesSeo(clubKey.value).description),
})
const hero = computed(() => getYouthCoachesHero(clubKey.value))
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }}</a></li>
      <li aria-current="page">{{ identity.academyShortLabelZh }}教練團</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無已核實、非過期的教練照片可用，不沿用磐石照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/academy/life-09.jpg')" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '4.5 Coaches' : '4.5' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band" aria-labelledby="coaches-list-title">
  <div class="container">
    <!-- S1-12e（GEO-07）：這裡原本直接從 H1 跳到教練卡片的 h3（人名），中間沒有 h2，
         違反「H2/H3 不跳階」。加一個視覺隱藏的 h2 補上大綱層級，比照本站既有的
         `.visually-hidden` 標題慣例（見 zh/member/index.vue「member-title」、
         zh/news/[slug]/index.vue「article-body-title」），不影響版面。 -->
    <h2 id="coaches-list-title" class="visually-hidden">教練名單</h2>

    <div v-if="isTcrfc" class="grid grid--3 person-grid">

      <article class="person-card clip-card clip-card--outlined">
        <div class="person-card__photo person-card__photo--empty">
          <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4.4 3.6-8 8-8s8 3.6 8 8"/></svg>
        </div>
        <div class="person-card__body">
          <p class="person-card__role">青訓總監</p>
          <h3 class="person-card__name">徐翊</h3>
        </div>
      </article>

      <article class="person-card clip-card clip-card--outlined">
        <div class="person-card__photo">
          <img :src="siteImg('/assets/img/academy/coach-hsu-chih-chieh.jpg')" alt="青訓教練許志傑" width="800" height="800" loading="lazy">
        </div>
        <div class="person-card__body">
          <p class="person-card__role">青訓教練</p>
          <h3 class="person-card__name">許志傑</h3>
        </div>
      </article>

      <article class="person-card clip-card clip-card--outlined">
        <div class="person-card__photo person-card__photo--empty">
          <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4.4 3.6-8 8-8s8 3.6 8 8"/></svg>
        </div>
        <div class="person-card__body">
          <p class="person-card__role">青訓教練</p>
          <h3 class="person-card__name">黃聖傑</h3>
        </div>
      </article>

    </div>
    <p v-else class="section-lede is-pending">教練名單整理中，待已核實、非過期的資料到位後將公布於本頁。</p>

    <h2 class="section-title" style="margin-top:4rem;">證照、專長與負責梯隊</h2>
    <p class="section-lede" style="margin-top:.5rem;">相關資料準備中，稍後將於本頁公布。</p>
  </div>
</section>

<section class="band grain cta-band">
  <div class="container">
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/academy/teams/')">
        <span class="cta-card__num">4.2</span>
        <span class="cta-card__title">{{ identity.academyShortLabelZh }}隊伍</span>
        <p class="cta-card__desc">查看 {{ academyLabel() }} 各梯隊</p>
      </a>
      <a class="cta-card" :href="lp('/zh/academy/curriculum/')">
        <span class="cta-card__num">4.4</span>
        <span class="cta-card__title">訓練課程與課綱</span>
        <p class="cta-card__desc">五大訓練面向與週期規劃</p>
      </a>
      <a class="cta-card" :href="lp('/zh/academy/life/')">
        <span class="cta-card__num">4.6</span>
        <span class="cta-card__title">{{ identity.academyShortLabelZh }}生活</span>
        <p class="cta-card__desc">訓練與比賽的日常紀錄</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（BW-C1 新增，沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
.is-pending{ color:var(--muted); font-style:italic; }

/* ── 4.5 專屬元件：人物卡 person-card ─────────────────
   2.4 團隊成員、3.1 一線隊教練團等頁面都需要同款「照片＋職稱＋姓名＋簡介」卡片，
   強烈建議把 .person-card 收進 tcrfc.css 成為全站共用元件。 */
.person-grid{ align-items:start; }
.person-card{ --clip-fill:var(--paper-2); }
.person-card__photo{ aspect-ratio:1/1; overflow:hidden; background:var(--paper-3); }
.person-card__photo img{ width:100%; height:100%; object-fit:cover; object-position:top center; }
.person-card__photo--empty{ display:flex; align-items:center; justify-content:center; }
.person-card__photo--empty svg{ width:34%; height:34%; fill:none; stroke:var(--rule); stroke-width:1.4; }
.person-card__body{ padding:1.5rem 1.5rem 1.75rem; }
.person-card__role{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); }
.person-card__name{ font-size:1.3rem; font-weight:900; color:var(--heading); margin-top:.25rem; }
.person-card__pending{ margin-top:1rem; font-size:.82rem; }
</style>
