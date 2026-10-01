<script setup lang="ts">
// app/pages/zh/culture/fan-club/events/[slug].vue — 8.2 球迷會活動詳情與報名（主站 §3.8，S3-2）
// 資料：`GET {club}/fan-events/{slug}`（SSR 取匿名版；登入後由 FanEventRegistration 補抓本人的報名狀態）。
// 活動回顧圖集與關聯文章（只列已發布）在同一支端點。草稿／別隊活動後端回 404 → 轉 404 頁。
import { formatTaipeiDateTime } from '#shared/utils/member'
import type { FanEventDetail } from '#shared/utils/member'

definePageMeta({ nav: 'culture', unit: '8.2' })

const { lp, locale } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const club = config.public.club
const clubAssets = computed(() => getClubAssets(club))
const identity = computed(() => getClubIdentity(club))
const slug = String(route.params.slug ?? '')

const { data: detail, error } = await useFetch<FanEventDetail>(`/api/backend/${club}/fan-events/${encodeURIComponent(slug)}`, {
  query: { lang: locale.value },
  key: `fan-event-${club}-${slug}-${locale.value}`,
})
if (error.value || !detail.value) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}

const ev = computed(() => detail.value!.event)
const safeImg = (u: string | null | undefined) => (u && /^(https:\/\/|\/)/.test(u) ? u : null)
const paragraphs = computed(() => (detail.value?.description ?? '').split(/\n+/).map(p => p.trim()).filter(Boolean))
const images = computed(() => (detail.value?.images ?? []).filter(i => safeImg(i.imageUrl)))
const isPast = computed(() => ev.value.phase === 'past')

useSeoMeta({
  title: computed(() => `${ev.value.name}｜球迷會活動｜${clubAssets.value.nameZh}`),
  description: computed(() => `${clubAssets.value.shortNameZh}球迷會活動「${ev.value.name}」：${ev.value.startsAt ? formatTaipeiDateTime(ev.value.startsAt, locale.value) : ''}${ev.value.location ? `，${ev.value.location}` : ''}。`),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li><a :href="lp('/zh/culture/fan-club/')">球迷會</a></li>
      <li aria-current="page">{{ ev.name }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">8.2 Fan Events</p>
    <h1>{{ ev.name }}</h1>
    <p class="page-hero__lede">
      <template v-if="ev.startsAt">{{ formatTaipeiDateTime(ev.startsAt, locale) }}</template>
      <template v-if="ev.location">　{{ ev.location }}</template>
    </p>
  </div>
</section>

<section class="band" aria-labelledby="fe-detail-title">
  <div class="container">
    <h2 id="fe-detail-title" class="visually-hidden">活動資訊</h2>
    <div class="fe-detail">
      <div>
        <img v-if="safeImg(ev.coverUrl)" class="fe-detail__cover" :src="safeImg(ev.coverUrl)!" :alt="ev.name" width="1280" height="853">
        <div class="prose">
          <p v-for="(p, i) in paragraphs" :key="i">{{ p }}</p>
        </div>
      </div>
      <aside class="fe-detail__side">
        <dl class="mc-dl">
          <div v-if="ev.startsAt"><dt>活動時間</dt><dd>{{ formatTaipeiDateTime(ev.startsAt, locale) }}<template v-if="ev.endsAt"> – {{ formatTaipeiDateTime(ev.endsAt, locale) }}</template></dd></div>
          <div v-if="detail!.venueName || ev.location"><dt>地點</dt><dd>{{ detail!.venueName || ev.location }}<template v-if="detail!.venueName && ev.location && detail!.venueName !== ev.location">（{{ ev.location }}）</template></dd></div>
          <div><dt>名額</dt><dd>{{ ev.capacity === null ? '不限' : `${ev.capacity} 人${ev.spotsLeft !== null ? `（剩餘 ${ev.spotsLeft}）` : ''}` }}{{ ev.isFull ? '，已額滿可登記候補' : '' }}</dd></div>
          <div v-if="ev.isPaidMembersOnly"><dt>報名資格</dt><dd>限付費球迷會員</dd></div>
        </dl>
        <FanEventRegistration v-if="!isPast" :event="ev" :initial-my="detail!.myRegistration" />
        <p v-else class="mc-alert mc-alert--info" role="status">此活動已結束。</p>
      </aside>
    </div>

    <div v-if="images.length" class="fe-review">
      <h2 class="section-title">活動回顧</h2>
      <div class="fe-gallery">
        <figure v-for="(im, i) in images" :key="i" class="event-photo">
          <img :src="safeImg(im.imageThumbUrl || im.imageUrl)!" :alt="`${ev.name} 活動照片 ${i + 1}`" loading="lazy" :width="im.width ?? undefined" :height="im.height ?? undefined">
        </figure>
      </div>
    </div>

    <div v-if="(detail!.articles ?? []).length" class="fe-articles">
      <h2 class="section-title">相關報導</h2>
      <ul>
        <li v-for="a in detail!.articles" :key="a.slug"><a :href="lp(`/zh/news/${a.slug}/`)">{{ a.title }}</a></li>
      </ul>
    </div>

    <p style="margin-top:2.5rem;"><a class="btn btn--dark btn--sm" :href="lp('/zh/culture/fan-club/')">回到球迷會</a></p>
  </div>
</section>
</template>

<style>
.fe-detail{ display:grid; grid-template-columns:minmax(0,1.4fr) minmax(0,1fr); gap:2.5rem; align-items:start; }
@media (max-width:900px){ .fe-detail{ grid-template-columns:minmax(0,1fr); } }
.fe-detail__cover{ width:100%; height:auto; display:block; margin-bottom:1.5rem; }
.fe-detail__side{ background:var(--paper-2); border:1px solid var(--rule); padding:1.5rem; }
.fe-review, .fe-articles{ margin-top:3rem; }
.fe-articles ul{ padding-left:1.2rem; line-height:2; }
.fe-articles a{ color:var(--brand-aa); text-decoration:underline; }
</style>
