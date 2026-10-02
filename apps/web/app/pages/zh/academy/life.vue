<script setup lang="ts">
// app/pages/zh/academy/life.vue — 由 site/src/pages/zh/academy/life/index.html 轉來（S0-9 靜態頁搬遷）
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（S2-8 曾以「13 張磐石學員未成年真實照片不能
// 挪用」為由整頁 404，是誤用——藍鯨規劃書 §1.3 總則例外只有四項，不含 4.6，見
// shared/utils/units.ts 檔頭）。照片本身仍不能挪用（未成年真實影像，藍鯨沒有對應、
// 已核實肖像同意的青年隊照片可用），這是「此頁此區塊內容缺漏」不是「整頁不存在」，
// 圖片藝廊區塊對藍鯨改為顯示既有「收錄中」空狀態（比照 3.5 球員故事），不放任何
// 照片，不是 404。標題改「青年隊生活」（不用「學院」字樣，見
// check-club-brand-leak.mjs 詞表）。
definePageMeta({ nav: "academy", unit: "4.6" })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))

// S1-12d 收尾：梯隊代碼改讀 useSiteFacts(clubKey)（後端公開端點）。
const { academyLabel } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => getYouthLifeSeo(clubKey.value).title),
  description: computed(() => getYouthLifeSeo(clubKey.value).description),
})
const hero = computed(() => getYouthLifeHero(clubKey.value))
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }}</a></li>
      <li aria-current="page">{{ identity.academyShortLabelZh }}生活</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無已核實、已取得肖像同意的照片可用，不沿用磐石學員照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/academy/life-07.jpg')" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '4.6 Academy Life' : '4.6' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">Photo Gallery</p>
        <h2 class="section-title">訓練日常剪影</h2>
      </div>
      <p v-if="isTcrfc" class="section-lede">下列照片皆為學員未成年，說明僅描述訓練場景，不標示個人姓名。</p>
      <p v-else class="section-lede is-pending">訓練與比賽影像整理中，待肖像同意到位後將陸續公布於本頁。</p>
    </div>

    <div v-if="isTcrfc" class="gallery-grid">
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-01.jpg')" alt="夜間訓練場上，學員盤球突破，一旁有對手球員防守" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-02.jpg')" alt="訓練場上，學員全力衝刺進行折返跑練習" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-03.jpg')" alt="夜間降雨中，學員帶球奔跑" width="1067" height="1600" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-04.jpg')" alt="一對一防守練習中，兩名學員貼身盯防" width="1067" height="1600" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-05.jpg')" alt="雨中訓練，學員帶球奔跑，隊友於後方跟進" width="1600" height="1066" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-06.jpg')" alt="教練在一旁指導，學員練習盤球" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-07.jpg')" alt="學員在球門前控球，臉上帶著笑容" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-08.jpg')" alt="學員在觀眾席前盤球練習" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-09.jpg')" alt="教練在球門邊指導學員進行對抗練習" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-10.jpg')" alt="學員在角錐旁練習控球" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-11.jpg')" alt="學員帶球推進，隊友伸手上前逼搶" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-12.jpg')" alt="教練在球門邊向排成一列的學員講解訓練內容" width="1600" height="1067" loading="lazy"></figure>
      <figure class="gallery-item"><img :src="siteImg('/assets/img/academy/life-13.jpg')" alt="兩名學員在夜間訓練中爭搶皮球" width="1600" height="1067" loading="lazy"></figure>
    </div>
  </div>
</section>

<section class="band">
  <div class="container">
    <h2 class="section-title">影音</h2>
    <p class="section-lede" style="margin-top:.5rem;">影音內容準備中，稍後將於本頁公布。</p>
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
      <a class="cta-card" :href="lp('/zh/academy/coaches/')">
        <span class="cta-card__num">4.5</span>
        <span class="cta-card__title">{{ identity.academyShortLabelZh }}教練團</span>
        <p class="cta-card__desc">認識帶領各梯隊的教練</p>
      </a>
      <!-- 4.7 加入學院對藍鯨已整頁關閉（既有 units.ts），不連結一個會 404 的頁面。 -->
      <a v-if="isTcrfc" class="cta-card" :href="lp('/zh/academy/join/')">
        <span class="cta-card__num">4.7</span>
        <span class="cta-card__title">加入學院</span>
        <p class="cta-card__desc">招生對象與遴選流程</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（BW-C1 新增，沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
.is-pending{ color:var(--muted); font-style:italic; }

/* ── 4.6 專屬元件：圖片藝廊 ───────────────────────────
   7.x 新聞／8.x 台中磐石文化等單元的圖片牆也會用到，建議把 .gallery-grid 收進 tcrfc.css 共用。 */
.gallery-grid{
  display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:.6rem; margin-top:2.25rem;
}
.gallery-item{ aspect-ratio:4/3; overflow:hidden; background:var(--paper-2); }
.gallery-item img{ width:100%; height:100%; object-fit:cover; transition:transform var(--dur) var(--ease); }
.gallery-item:hover img{ transform:scale(1.06); }
</style>
