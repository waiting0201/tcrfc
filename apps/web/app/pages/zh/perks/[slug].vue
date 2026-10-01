<script setup lang="ts">
// app/pages/zh/perks/[slug].vue — 8.4 特約店家詳情（S2-11）
// 資料：`GET {club}/partner-stores/{slug}`；草稿、合作期間外、別隊店家一律 404（後端已處理，這裡轉成 404 頁）。
// 店家的 `lat`／`lng` 是給行動 App 附近地圖用的，網頁前台只放地圖連結（`mapUrl`），不自行嵌第三方地圖。
import type { PartnerStore } from '#shared/utils/member'

definePageMeta({ nav: 'culture', unit: '08' })

const { lp, locale } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const identity = computed(() => getClubIdentity(config.public.club))
const club = config.public.club
const slug = String(route.params.slug ?? '')

const { data: store, error } = await useFetch<PartnerStore>(`/api/backend/${club}/partner-stores/${encodeURIComponent(slug)}`, {
  query: { lang: locale.value },
  key: `partner-store-${club}-${slug}-${locale.value}`,
})
if (error.value || !store.value) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}

const safeUrl = (u: string | null | undefined) => (u && /^https?:\/\//i.test(u) ? u : null)
const safeImg = (u: string | null | undefined) => (u && /^(https:\/\/|\/)/.test(u) ? u : null)

useSeoMeta({
  title: computed(() => `${store.value?.name ?? '特約店家'}｜特約店家｜${clubAssets.value.nameZh}`),
  description: computed(() => store.value?.offerContent ?? `${store.value?.name ?? ''}是${clubAssets.value.shortNameZh}會員的特約店家。`),
})
</script>

<template>
<nav v-if="store" class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li><a :href="lp('/zh/perks/')">特約店家</a></li>
      <li aria-current="page">{{ store.name }}</li>
    </ol>
  </div>
</nav>

<template v-if="store">
<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Partner Perks<template v-if="store.category">・{{ store.category }}</template></p>
    <h1>{{ store.name }}</h1>
    <p v-if="store.offerContent" class="page-hero__lede">{{ store.offerContent }}</p>
  </div>
</section>

<section class="band" aria-labelledby="store-detail-title">
  <div class="container">
    <h2 id="store-detail-title" class="visually-hidden">店家資訊</h2>
    <div class="store-detail">
      <img v-if="safeImg(store.imageUrl)" :src="safeImg(store.imageUrl)!" :alt="store.name" width="960" height="640">
      <dl>
        <div><dt>優惠內容</dt><dd>{{ store.offerContent || '—' }}</dd></div>
        <div><dt>適用層級</dt><dd>{{ store.applicableTierLabel }}</dd></div>
        <div v-if="store.category"><dt>類別</dt><dd>{{ store.category }}</dd></div>
        <div v-if="store.region"><dt>地區</dt><dd>{{ store.region }}</dd></div>
        <div v-if="store.address"><dt>地址</dt><dd>{{ store.address }}<template v-if="safeUrl(store.mapUrl)">　<a :href="safeUrl(store.mapUrl)!" target="_blank" rel="noopener noreferrer">查看地圖</a></template></dd></div>
        <div v-if="store.phone"><dt>電話</dt><dd><a :href="`tel:${store.phone.replace(/[^0-9+]/g, '')}`">{{ store.phone }}</a></dd></div>
        <div v-if="store.businessHours"><dt>營業時間</dt><dd>{{ store.businessHours }}</dd></div>
        <div v-if="safeUrl(store.websiteUrl)"><dt>官方網站</dt><dd><a :href="safeUrl(store.websiteUrl)!" target="_blank" rel="noopener noreferrer">{{ store.websiteUrl }}</a></dd></div>
      </dl>
    </div>
    <p class="mc-note" style="margin-top:2rem;">
      到店<strong>出示電子會員卡</strong>即可享優惠，店家目視查驗，不需額外手續。優惠內容與適用條件由店家提供，實際以店家現場公告為準。
      <template v-if="store.applicableTier === 'fan_club'">此優惠限<strong>付費球迷會員</strong>（有效期內）使用。</template>
    </p>
    <p><a class="btn btn--dark btn--sm" :href="lp('/zh/perks/')">回到特約店家清單</a>　<a class="btn btn--primary btn--sm" :href="lp('/zh/member/')">會員中心</a></p>
  </div>
</section>
</template>
</template>

<style>
.store-detail a{ color:var(--brand-aa); text-decoration:underline; }
</style>
