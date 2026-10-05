<script setup lang="ts">
// app/pages/zh/join/location/index.vue — 由 site/src/pages/zh/join/location/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: '', unit: '10-location', enReady: true })

const { lp, isEn, tx } = useLocale()
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

// I5 場地管理（H 批）：後台維護的場地（名稱、地址、經緯度、交通說明、照片）。有資料時改由 API 呈現場地卡、地圖與交通說明，
// 沒有資料（API 無場地或打不到）時沿用上方 site-facts 的既有內容（過渡策略）。
const { venues } = await usePublicVenues()
const hasApiVenues = computed(() => venues.value.length > 0)
const apiPrimaryVenue = computed(() => venues.value.find((v) => v.isHome) ?? venues.value[0] ?? null)
const apiEmbedSrc = computed(() => (apiPrimaryVenue.value ? venueEmbedSrc(apiPrimaryVenue.value) : null))
const showMap = computed(() => (hasApiVenues.value ? !!apiEmbedSrc.value : !!facts.value.contact.address))
const embedSrc = computed(() => (hasApiVenues.value ? apiEmbedSrc.value : mapEmbedSrc.value))
const primaryNavHref = computed(() => (apiPrimaryVenue.value ? venueMapHref(apiPrimaryVenue.value) : null) ?? mapNavHref.value)
const primaryName = computed(() => apiPrimaryVenue.value?.name ?? (isEn.value ? (primaryVenue.value.nameEn ?? primaryVenue.value.nameZh) : primaryVenue.value.nameZh))
// 英文版：API 場地欄位回退為繁中時提示（PublicVenue 沒有 isFallbackLocale，以是否含中日文字判斷）
const venuesHaveZh = computed(() => venues.value.some((v) => /[\u3400-\u9fff]/.test(`${v.name}${v.address ?? ''}${v.directions ?? ''}`)))

useSeoMeta({
  title: computed(() => (isEn.value ? 'Location & Map | Join / Contact | Taichung Rock FC' : `場地位置與地圖 Location & Map｜加入與聯絡｜${getClubAssets(clubKey.value).nameZh}`)),
  description: computed(() => (isEn.value
    ? 'Locations, maps and directions for the Taichung Rock FC training base, home ground and Academy venues.'
    : isTcrfc.value
    ? '台中磐石足球俱樂部訓練基地、主場與學院場地的位置、地圖與交通指引。'
    : '台中藍鯨主場的位置、地圖與交通指引。')),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/join/')">{{ tx('加入與聯絡', 'Join / Contact') }}</a></li>
      <li aria-current="page">{{ tx('場地位置與地圖', 'Location & Map') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10</span>
  <div class="container">
    <p class="page-hero__eyebrow">Location &amp; Map</p>
    <h1><template v-if="isEn">Location &amp; Map</template><template v-else>{{ tx('場地位置與地圖', 'Location & Map') }}<span class="en">Location &amp; Map</span></template></h1>
    <p v-if="isTcrfc" class="page-hero__lede"><template v-if="isEn">The Taichung Rock FC training base, home ground and Academy venues are located in and around Beitun, Taichung. Below you will find each venue's location, map and directions.</template><template v-else>台中磐石的訓練基地、主場與學院場地分布於台中北屯一帶，以下整理各場地的位置、地圖與交通指引。</template></p>
    <p v-else class="page-hero__lede">台中藍鯨的比賽與訓練場地分布於台中北屯一帶，以下整理各場地的位置、地圖與交通指引。</p>
  </div>
</section>

<section class="band" aria-labelledby="venues-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">{{ tx('場地一覽', 'Venues') }}</p>
        <h2 class="section-title" id="venues-title">{{ isEn ? 'Training base, home ground and Academy venues' : isTcrfc ? '訓練基地・主場・學院場地' : '比賽與訓練場地' }}</h2>
      </div>
    </div>
    <LocaleFallbackNotice v-if="isEn && venuesHaveZh" partial />

    <div v-if="hasApiVenues" class="grid grid--3">
      <article v-for="v in venues" :key="v.id" class="venue-card">
        <img v-if="v.photoUrl" class="venue-card__photo" :src="v.photoUrl" :alt="v.photoAlt ?? ''" :width="v.photoWidth ?? undefined" :height="v.photoHeight ?? undefined" loading="lazy">
        <p class="venue-card__label">{{ v.isHome ? tx('主場', 'Home ground') : tx('場地', 'Venue') }}</p>
        <h3 class="venue-card__name">{{ v.name }}</h3>
        <p class="venue-card__addr">{{ v.address ?? tx('地址資訊準備中，稍後將於本頁公布。', 'Address details are being prepared and will be published here soon.') }}</p>
      </article>
    </div>
    <div v-else-if="isTcrfc" class="grid grid--3">
      <article class="venue-card">
        <p class="venue-card__label">{{ tx('訓練基地', 'Training base') }}</p>
        <h3 class="venue-card__name">Training Base</h3>
        <p class="venue-card__addr">{{ tx('地址資訊準備中，稍後將於本頁公布。', 'Address details are being prepared and will be published here soon.') }}</p>
      </article>

      <!-- GEO-03（S1-12d）：主場名稱／地址為單一來源 site-facts.ts，不在此重複寫死字面值。 -->
      <article class="venue-card">
        <p class="venue-card__label">{{ tx('主場', 'Home ground') }}</p>
        <h3 class="venue-card__name"><template v-if="isEn">{{ primaryVenue.nameEn ?? primaryVenue.nameZh }}</template><template v-else>{{ primaryVenue.nameZh }}<span class="en">{{ primaryVenue.nameEn }}</span></template></h3>
        <p class="venue-card__addr">{{ facts.contact.address }}</p>
      </article>

      <article class="venue-card">
        <p class="venue-card__label">{{ tx('學院場地', 'Academy venue') }}</p>
        <h3 class="venue-card__name">Academy Ground</h3>
        <p class="venue-card__addr">{{ tx('地址資訊準備中，稍後將於本頁公布。', 'Address details are being prepared and will be published here soon.') }}</p>
      </article>
    </div>
    <!-- bw：沒有「訓練基地／學院場地」這種既有場地分類，逐一列出 facts.venues 這份真實
         的場地清單（現況兩座，皆無地址，詳見 site-facts.ts），不挪用磐石的場地分類。 -->
    <div v-else class="grid grid--3">
      <article v-for="v in facts.venues" :key="v.nameZh" class="venue-card">
        <p class="venue-card__label">{{ v.isHomeGround ? '主場' : '訓練場地' }}</p>
        <h3 class="venue-card__name">{{ v.nameZh }}<span v-if="v.nameEn" class="en">{{ v.nameEn }}</span></h3>
        <p class="venue-card__addr">{{ v.address ?? tx('地址資訊準備中，稍後將於本頁公布。', 'Address details are being prepared and will be published here soon.') }}</p>
      </article>
    </div>
  </div>
</section>

<section class="band grain" aria-labelledby="map-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">MAP</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">{{ tx('嵌入地圖', 'Embedded map') }}</p>
        <h2 class="section-title" id="map-title" style="color:#fff">Google Map</h2>
      </div>
      <p class="section-lede on-dark">{{ tx('正式上線時於此區塊嵌入各場地的 Google Map（可依所選場地切換）。', 'At launch, a Google Map for each venue will be embedded here (switchable by venue).') }}</p>
    </div>

    <div class="map-embed">
      <iframe
        v-if="showMap"
        :src="embedSrc ?? undefined"
        :title="isEn ? 'Google Map: home ground location' : `Google Map：${getClubAssets(clubKey).shortNameZh}主場位置`"
        loading="lazy"
        referrerpolicy="no-referrer-when-downgrade"
        allowfullscreen
      />
      <p v-else>{{ tx('地圖嵌入位置準備中，可先參考上方各場地地址資訊。', 'The embedded map is being prepared. In the meantime, please refer to the venue addresses above.') }}</p>
    </div>
  </div>
</section>

<section class="band" aria-labelledby="directions-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">{{ tx('交通指引', 'Directions') }}</p>
        <h2 class="section-title" id="directions-title">{{ tx('怎麼到場地', 'How to get there') }}</h2>
      </div>
    </div>
    <div v-if="hasApiVenues" class="grid grid--3">
      <div v-for="v in venues" :key="v.id" class="direction-card">
        <p class="direction-card__mode">{{ v.name }}</p>
        <template v-if="splitParagraphs(v.directions).length > 0">
          <p v-for="(para, i) in splitParagraphs(v.directions)" :key="i" class="direction-card__text">{{ para }}</p>
        </template>
        <p v-else>{{ tx('詳細路線指引準備中，稍後將於本頁公布。', 'Detailed directions are being prepared and will be published here soon.') }}</p>
        <a v-if="venueMapHref(v)" :href="venueMapHref(v)!" target="_blank" rel="noopener">{{ tx('開啟 Google 導航', 'Open Google Maps navigation') }}<span class="visually-hidden">{{ tx('（新分頁開啟）', '(opens in a new tab)') }}</span></a>
      </div>
    </div>
    <div v-else class="grid grid--3">
      <div class="direction-card">
        <p class="direction-card__mode">{{ tx('開車', 'By car') }}</p>
        <p>{{ tx('詳細路線指引準備中，稍後將於本頁公布。', 'Detailed directions are being prepared and will be published here soon.') }}</p>
      </div>
      <div class="direction-card">
        <p class="direction-card__mode">{{ tx('大眾運輸', 'Public transport') }}</p>
        <p>{{ tx('詳細路線指引準備中，稍後將於本頁公布。', 'Detailed directions are being prepared and will be published here soon.') }}</p>
      </div>
      <div class="direction-card">
        <p class="direction-card__mode">{{ tx('導航連結', 'Navigation link') }}</p>
        <a :href="primaryNavHref" target="_blank" rel="noopener">{{ tx('開啟 Google 導航', 'Open Google Maps navigation') }}{{ isEn ? ` (${primaryName})` : `（${primaryName}）` }}<span class="visually-hidden">{{ tx('（新分頁開啟）', '(opens in a new tab)') }}</span></a>
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
.direction-card__text{ white-space:pre-line; overflow-wrap:anywhere; margin-bottom:.6rem; }
.venue-card__photo{ display:block; width:100%; height:auto; aspect-ratio:16/9; object-fit:cover; margin-bottom:1rem; }
.direction-card a{ font-size:.85rem; font-weight:700; color:var(--brand-aa); }
</style>
