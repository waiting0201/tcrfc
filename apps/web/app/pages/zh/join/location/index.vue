<script setup lang="ts">
// app/pages/zh/join/location/index.vue — 由 site/src/pages/zh/join/location/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: '', unit: '10-location' })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

// BW-C1（品牌外洩全站盤點）：本頁原本 10-location 單元零俱樂部分支（既有缺口，見
// apps/web/README.md「S1-12d」節「刻意不動的範圍」），固定讀 tcrfc。改為動態帶入
// 目前 club：tcrfc 維持既有「訓練基地／主場／學院場地」三張固定卡；bw 沒有這三種
// 場地類型的既有分類（只有 `facts.venues` 這份真實的兩座場地清單，見 site-facts.ts），
// 改為逐一列出 `facts.venues`，不挪用磐石的場地分類假裝藍鯨也有「訓練基地」。
const { facts, primaryVenue } = useSiteFacts(clubKey.value)

// S1-17：嵌入地圖與導航連結改用 site-facts 的地址算出來（不再另外寫死一份 URL 編碼字串）。
// 只有主場地址有真實資料可用時才嵌入（tcrfc 有；bw 現況兩座場地皆無地址，見 site-facts.ts
// 既有註解，`v-if="facts.contact.address"` 因此對 bw 自然不渲染，不是本輪新增的判斷）。
const mapQuery = computed(() => encodeURIComponent(facts.value.contact.address ?? primaryVenue.value.nameZh))
const mapEmbedSrc = computed(() => `https://www.google.com/maps?q=${mapQuery.value}&output=embed`)
const mapNavHref = computed(() => `https://www.google.com/maps/search/?api=1&query=${mapQuery.value}`)

useSeoMeta({
  title: computed(() => `場地位置與地圖 Location & Map｜加入與聯絡｜${getClubAssets(clubKey.value).nameZh}`),
  description: computed(() => (isTcrfc.value
    ? '台中磐石足球俱樂部訓練基地、主場與學院場地的位置、地圖與交通指引。'
    : '台中藍鯨主場的位置、地圖與交通指引。')),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/join/')">加入與聯絡</a></li>
      <li aria-current="page">場地位置與地圖</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10</span>
  <div class="container">
    <p class="page-hero__eyebrow">Location &amp; Map</p>
    <h1>場地位置與地圖<span class="en">Location &amp; Map</span></h1>
    <p v-if="isTcrfc" class="page-hero__lede">台中磐石的訓練基地、主場與學院場地分布於台中北屯一帶，以下整理各場地的位置、地圖與交通指引。</p>
    <p v-else class="page-hero__lede">台中藍鯨的比賽與訓練場地分布於台中北屯一帶，以下整理各場地的位置、地圖與交通指引。</p>
  </div>
</section>

<section class="band" aria-labelledby="venues-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">場地一覽</p>
        <h2 class="section-title" id="venues-title">{{ isTcrfc ? '訓練基地・主場・學院場地' : '比賽與訓練場地' }}</h2>
      </div>
    </div>

    <div v-if="isTcrfc" class="grid grid--3">
      <article class="venue-card">
        <p class="venue-card__label">訓練基地</p>
        <h3 class="venue-card__name">Training Base</h3>
        <p class="venue-card__addr">地址資訊準備中，稍後將於本頁公布。</p>
      </article>

      <!-- GEO-03（S1-12d）：主場名稱／地址為單一來源 site-facts.ts，不在此重複寫死字面值。 -->
      <article class="venue-card">
        <p class="venue-card__label">主場</p>
        <h3 class="venue-card__name">{{ primaryVenue.nameZh }}<span class="en">{{ primaryVenue.nameEn }}</span></h3>
        <p class="venue-card__addr">{{ facts.contact.address }}</p>
      </article>

      <article class="venue-card">
        <p class="venue-card__label">學院場地</p>
        <h3 class="venue-card__name">Academy Ground</h3>
        <p class="venue-card__addr">地址資訊準備中，稍後將於本頁公布。</p>
      </article>
    </div>
    <!-- bw：沒有「訓練基地／學院場地」這種既有場地分類，逐一列出 facts.venues 這份真實
         的場地清單（現況兩座，皆無地址，詳見 site-facts.ts），不挪用磐石的場地分類。 -->
    <div v-else class="grid grid--3">
      <article v-for="v in facts.venues" :key="v.nameZh" class="venue-card">
        <p class="venue-card__label">{{ v.isHomeGround ? '主場' : '訓練場地' }}</p>
        <h3 class="venue-card__name">{{ v.nameZh }}<span v-if="v.nameEn" class="en">{{ v.nameEn }}</span></h3>
        <p class="venue-card__addr">{{ v.address ?? '地址資訊準備中，稍後將於本頁公布。' }}</p>
      </article>
    </div>
  </div>
</section>

<section class="band grain" aria-labelledby="map-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">MAP</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">嵌入地圖</p>
        <h2 class="section-title" id="map-title" style="color:#fff">Google Map</h2>
      </div>
      <p class="section-lede on-dark">正式上線時於此區塊嵌入各場地的 Google Map（可依所選場地切換）。</p>
    </div>

    <div class="map-embed">
      <iframe
        v-if="facts.contact.address"
        :src="mapEmbedSrc"
        :title="`Google Map：${getClubAssets(clubKey).shortNameZh}主場位置`"
        loading="lazy"
        referrerpolicy="no-referrer-when-downgrade"
        allowfullscreen
      />
      <p v-else>地圖嵌入位置準備中，可先參考上方各場地地址資訊。</p>
    </div>
  </div>
</section>

<section class="band" aria-labelledby="directions-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">交通指引</p>
        <h2 class="section-title" id="directions-title">怎麼到場地</h2>
      </div>
    </div>
    <div class="grid grid--3">
      <div class="direction-card">
        <p class="direction-card__mode">開車</p>
        <p>詳細路線指引準備中，稍後將於本頁公布。</p>
      </div>
      <div class="direction-card">
        <p class="direction-card__mode">大眾運輸</p>
        <p>詳細路線指引準備中，稍後將於本頁公布。</p>
      </div>
      <div class="direction-card">
        <p class="direction-card__mode">導航連結</p>
        <a :href="mapNavHref" target="_blank" rel="noopener">開啟 Google 導航（{{ primaryVenue.nameZh }}）<span class="visually-hidden">（新分頁開啟）</span></a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 僅本頁使用：場地卡、地圖佔位、交通指引卡 */
.venue-card{
  padding:2rem clamp(1.25rem,3vw,1.75rem); background:var(--paper-2); border:1px solid var(--rule);
}
.venue-card__label{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.6rem; }
.venue-card__name{ font-size:1.3rem; font-weight:900; letter-spacing:-.01em; color:var(--heading); margin-bottom:1rem; }
.venue-card__name .en{ display:block; font-size:.6em; font-weight:700; text-transform:uppercase; letter-spacing:.06em; color:var(--muted); margin-top:.3rem; }
.venue-card__addr{ font-size:.88rem; color:var(--muted); line-height:1.6; }

.map-embed{
  width:100%; aspect-ratio:16/7; min-height:220px; background:var(--ink-2); border:1px solid rgba(255,255,255,.08);
  display:flex; align-items:center; justify-content:center; padding:clamp(1.25rem,3vw,2rem); text-align:center;
}
.map-embed p{ color:var(--muted-dark); font-size:.9rem; max-width:56ch; min-width:0; }
.map-embed iframe{ width:100%; height:100%; border:0; }

.direction-card{ padding:1.75rem; border:1px solid var(--rule); background:var(--paper); }
.direction-card__mode{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.9rem; }
.direction-card p{ font-size:.85rem; color:var(--muted); }
.direction-card a{ font-size:.85rem; font-weight:700; color:var(--brand-aa); }
</style>
