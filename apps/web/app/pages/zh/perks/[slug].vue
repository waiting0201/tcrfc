<script setup lang="ts">
// app/pages/zh/perks/[slug].vue — 8.4 特約店家詳情（S2-11）
// 資料：`GET {club}/partner-stores/{slug}`；草稿、合作期間外、別隊店家後端回 404，這裡不顯示錯誤頁，
// 302 回特約店家清單（App 規劃書 §2.3：`tcrfc://store/{id}` 的回退網址「不得顯示錯誤頁」，docs/19 §2）。
// 店家的 `lat`／`lng` 是給行動 App 附近地圖用的，網頁前台只放地圖連結（`mapUrl`），不自行嵌第三方地圖。
import type { PartnerStore } from '#shared/utils/member'

definePageMeta({ nav: 'culture', unit: '08', enReady: true })

const { lp, locale, isEn, tx } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(config.public.club)))
const club = config.public.club
const slug = String(route.params.slug ?? '')

const { data: store, error } = await useFetch<PartnerStore>(`/api/backend/${club}/partner-stores/${encodeURIComponent(slug)}`, {
  query: { lang: locale.value },
  key: `partner-store-${club}-${slug}-${locale.value}`,
})
if (error.value || !store.value) {
  await navigateTo(lp('/zh/perks/'), { redirectCode: 302, replace: true })
}

const safeUrl = (u: string | null | undefined) => (u && /^https?:\/\//i.test(u) ? u : null)
const safeImg = (u: string | null | undefined) => (u && /^(https:\/\/|\/)/.test(u) ? u : null)

useSeoMeta({
  title: computed(() => (isEn.value
    ? getPerksDetailSeoEn(store.value?.name, store.value?.offerContent).title
    : `${store.value?.name ?? '特約店家'}｜特約店家｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value
    ? getPerksDetailSeoEn(store.value?.name, store.value?.offerContent).description
    : (store.value?.offerContent ?? `${store.value?.name ?? ''}是${clubAssets.value.shortNameZh}會員的特約店家。`))),
})
</script>

<template>
<nav v-if="store" class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li><a :href="lp('/zh/perks/')">{{ tx('特約店家', 'Partner Perks') }}</a></li>
      <li aria-current="page">{{ store.name }}</li>
    </ol>
  </div>
</nav>

<template v-if="store">
<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(store)" partial />
<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Partner Perks<template v-if="store.category">{{ tx('・', ' · ') }}{{ store.category }}</template></p>
    <h1>{{ store.name }}</h1>
    <p v-if="store.offerContent" class="page-hero__lede">{{ store.offerContent }}</p>
  </div>
</section>

<section class="band" aria-labelledby="store-detail-title">
  <div class="container">
    <h2 id="store-detail-title" class="visually-hidden">{{ tx('店家資訊', 'Store information') }}</h2>
    <div class="store-detail">
      <img v-if="safeImg(store.imageUrl)" :src="safeImg(store.imageUrl)!" :alt="store.name" width="960" height="640">
      <dl>
        <div><dt>{{ tx('優惠內容', 'Offer') }}</dt><dd>{{ store.offerContent || '—' }}</dd></div>
        <div><dt>{{ tx('適用層級', 'Eligible tier') }}</dt><dd>{{ store.applicableTierLabel }}</dd></div>
        <div v-if="store.category"><dt>{{ tx('類別', 'Category') }}</dt><dd>{{ store.category }}</dd></div>
        <div v-if="store.region"><dt>{{ tx('地區', 'Region') }}</dt><dd>{{ store.region }}</dd></div>
        <div v-if="store.address"><dt>{{ tx('地址', 'Address') }}</dt><dd>{{ store.address }}<template v-if="safeUrl(store.mapUrl)">{{ tx('　', ' ') }}<a :href="safeUrl(store.mapUrl)!" target="_blank" rel="noopener noreferrer">{{ tx('查看地圖', 'View map') }}</a></template></dd></div>
        <div v-if="store.phone"><dt>{{ tx('電話', 'Phone') }}</dt><dd><a :href="`tel:${store.phone.replace(/[^0-9+]/g, '')}`">{{ store.phone }}</a></dd></div>
        <div v-if="store.businessHours"><dt>{{ tx('營業時間', 'Opening hours') }}</dt><dd>{{ store.businessHours }}</dd></div>
        <div v-if="safeUrl(store.websiteUrl)"><dt>{{ tx('官方網站', 'Website') }}</dt><dd><a :href="safeUrl(store.websiteUrl)!" target="_blank" rel="noopener noreferrer">{{ store.websiteUrl }}</a></dd></div>
      </dl>
    </div>
    <p v-if="isEn" class="mc-note" style="margin-top:2rem;">
      <strong>Show your digital membership card</strong> in store to enjoy the offer; the store checks it by eye and no extra steps are needed. Offers and conditions are provided by the store, and the notice displayed in store takes precedence.
      <template v-if="store.applicableTier === 'fan_club'">This offer is for <strong>Paid Fan Club members</strong> with a valid membership only.</template>
    </p>
    <p v-else class="mc-note" style="margin-top:2rem;">
      到店<strong>出示電子會員卡</strong>即可享優惠，店家目視查驗，不需額外手續。優惠內容與適用條件由店家提供，實際以店家現場公告為準。
      <template v-if="store.applicableTier === 'fan_club'">此優惠限<strong>付費球迷會員</strong>（有效期內）使用。</template>
    </p>
    <p><a class="btn btn--dark btn--sm" :href="lp('/zh/perks/')">{{ tx('回到特約店家清單', 'Back to the partner store list') }}</a>{{ tx('　', ' ') }}<a class="btn btn--primary btn--sm" :href="lp('/zh/member/')">{{ tx('會員中心', 'Member Centre') }}</a></p>
  </div>
</section>
</template>
</template>

<style>
.store-detail a{ color:var(--brand-aa); text-decoration:underline; }
</style>
