<script setup lang="ts">
// app/pages/zh/charity/programs/[slug]/index.vue — 11.2 慈善計畫詳情（S2-9 新增）
//
// 規劃書 §3.11 11.2 詳情頁含：計畫緣起、受贈公益團體名稱、捐助內容、執行過程、活動圖片藝廊、相關報導；
// §3.11 內容關聯機制另有「慈善計畫可標記贊助夥伴（關聯 E1／E2）」。
// 資料來源：`GET /api/backend/{club}/charity/programs/{slug}?lang=`＋`GET .../charity/records?program={slug}`
// （同一計畫的事蹟紀錄，當作「執行過程」的逐筆佐證）。
// 🔴 `content`（計畫緣起與執行過程）是後台區塊編輯器的 JSON 字串，一律經 parseContentBlocks() 轉成純文字節點、
// 用 `{{ }}` 渲染，**不得 v-html**（見 shared/utils/content-blocks.ts）。
// 🔴 找不到（不存在／草稿）一律真 404，不回 200 空版面（比照 news/[slug]）。單元 11 對藍鯨整頁 404。
// 🔴 捐款只導流：本頁底部 CTA 讀後台 B5 設定（useCharityCta），文案點明收受者是協會；不放金額、不放表單。
import { parseContentBlocks } from '#shared/utils/content-blocks'
import type { CharityProgramDetail, ImpactRecord } from '#shared/utils/charity'
import type { PagedResponse } from '#shared/utils/api-types'
import { pickLogoUrl } from '#shared/utils/partners'

definePageMeta({ nav: 'charity', unit: '11', enReady: true })

const route = useRoute()
const config = useRuntimeConfig()
const club = config.public.club
const { lp, locale, isEn, tx } = useLocale()
const { donateLink } = await useCharityCta()

const slug = computed(() => String(route.params.slug))
const { data: program } = await useFetch<CharityProgramDetail>(() => `/api/backend/${club}/charity/programs/${slug.value}`, {
  query: { lang: locale.value },
})
if (!program.value) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}
const { data: recordsData } = await useFetch<PagedResponse<ImpactRecord>>(`/api/backend/${club}/charity/records`, {
  query: computed(() => ({ program: slug.value, lang: locale.value, pageSize: 12 })),
  key: `charity-program-records-${slug.value}-${locale.value}`,
})
const records = computed(() => recordsData.value?.items ?? [])

const blocks = computed(() => parseContentBlocks(program.value?.content))
const period = computed(() => programPeriod(program.value?.startOn ?? null, program.value?.endOn ?? null, isEn.value))
const clubName = computed(() => (isEn.value ? 'Taichung Rock FC' : getClubAssets(club).nameZh))

useSeoMeta({
  title: computed(() => (isEn.value
    ? `${program.value?.name ?? 'Charity program'} | Charity Programs | ${clubName.value}`
    : `${program.value?.name ?? '慈善計畫'}｜慈善計畫 Charity Programs｜${clubName.value}`)),
  description: computed(() => {
    const p = program.value
    if (!p) return ''
    if (isEn.value) {
      return [p.targetAudience ? `Audience: ${p.targetAudience}` : '', p.donationContent ? `Donated: ${p.donationContent}` : '', p.charity?.name ? `Beneficiary charity: ${p.charity.name}` : '']
        .filter(Boolean).join('; ').slice(0, 160) || `${clubName.value} charity program "${p.name ?? ''}"`
    }
    return [p.targetAudience ? `對象：${p.targetAudience}` : '', p.donationContent ? `捐助內容：${p.donationContent}` : '', p.charity?.name ? `受贈公益團體：${p.charity.name}` : '']
      .filter(Boolean).join('；').slice(0, 160) || `${clubName.value}慈善計畫「${p.name ?? ''}」`
  }),
  ogImage: computed(() => program.value?.coverUrl ?? undefined),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/charity/')">{{ tx('慈善與社會影響', 'Charity & Impact') }}</a></li>
      <li><a :href="lp('/zh/charity/programs/')">{{ tx('慈善計畫', 'Charity Programs') }}</a></li>
      <li aria-current="page">{{ program?.name }}</li>
    </ol>
  </div>
</nav>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale([program, records])" partial />

<section :class="['page-hero', { 'page-hero--media': program?.coverUrl }]">
  <img v-if="program?.coverUrl" class="page-hero__bg" :src="program.coverUrl" alt="" width="1600" height="1000">
  <div class="container">
    <p class="page-hero__eyebrow">11.2 Charity Programs</p>
    <h1>{{ program?.name }}</h1>
    <p class="page-hero__lede">
      <span :class="['status-chip', program?.progress === 'completed' ? 'status-chip--done' : 'status-chip--ongoing']">{{ progressLabel(program?.progress ?? '', isEn) }}</span>
      <template v-if="period"> {{ period }}</template>
    </p>
  </div>
</section>

<section class="band" aria-labelledby="program-origin-title">
  <div class="band-inner container program-detail">
    <article class="prose">
      <h2 id="program-origin-title">{{ tx('計畫緣起與執行過程', 'Background and implementation') }}</h2>
      <template v-if="blocks.length">
        <template v-for="(b, i) in blocks" :key="i">
          <h3 v-if="b.kind === 'h'">{{ b.text }}</h3>
          <ul v-else-if="b.kind === 'ul'"><li v-for="(it, j) in b.items" :key="j">{{ it }}</li></ul>
          <p v-else class="program-detail__para">{{ b.text }}</p>
        </template>
      </template>
      <p v-else class="is-pending">{{ tx('計畫緣起與執行過程整理中，稍後將於本頁公布。', 'The background and implementation of this program are being compiled and will be published here soon.') }}</p>

      <h2 v-if="program?.donationContent">{{ tx('捐助內容', 'Donation details') }}</h2>
      <p v-if="program?.donationContent" class="program-detail__para">{{ program.donationContent }}</p>
    </article>

    <aside class="program-detail__aside" aria-labelledby="program-facts-title">
      <h2 id="program-facts-title" class="program-detail__aside-title">{{ tx('計畫資訊', 'Program details') }}</h2>
      <dl class="program-facts">
        <div v-if="program?.targetAudience"><dt>{{ tx('對象', 'Audience') }}</dt><dd>{{ program.targetAudience }}</dd></div>
        <div v-if="period"><dt>{{ tx('期間', 'Period') }}</dt><dd>{{ period }}</dd></div>
        <div><dt>{{ tx('狀態', 'Status') }}</dt><dd>{{ progressLabel(program?.progress ?? '', isEn) }}</dd></div>
      </dl>

      <template v-if="program?.charity">
        <h3 class="program-detail__aside-sub">{{ tx('受贈公益團體', 'Beneficiary charity') }}</h3>
        <div class="charity-org">
          <img v-if="program.charity.logoUrl" :src="program.charity.logoUrl" :alt="`${program.charity.name ?? ''} Logo`" loading="lazy" width="96" height="96">
          <div>
            <p class="charity-org__name">{{ program.charity.name }}</p>
            <p v-if="program.charity.intro" class="charity-org__intro">{{ program.charity.intro }}</p>
            <p v-if="safeExternalUrl(program.charity.websiteUrl)" class="charity-org__intro"><a :href="safeExternalUrl(program.charity.websiteUrl) ?? undefined" target="_blank" rel="noopener noreferrer">{{ tx('團體網站（另開新分頁）', 'Charity website (opens in a new tab)') }}</a></p>
          </div>
        </div>
      </template>
    </aside>
  </div>
</section>

<section v-if="program?.images.length" class="band" aria-labelledby="program-gallery-title">
  <div class="container">
    <h2 id="program-gallery-title" class="section-title">{{ tx('活動圖片藝廊', 'Activity photo gallery') }}</h2>
    <ul class="program-gallery">
      <li v-for="(img, i) in program.images" :key="i">
        <a :href="img.imageUrl" target="_blank" rel="noopener noreferrer" :aria-label="isEn ? `${program.name ?? ''} activity photo ${i + 1} (opens in a new tab)` : `${program.name ?? ''} 活動圖片 ${i + 1}（另開新分頁）`">
          <img :src="img.thumbUrl ?? img.imageUrl" alt="" loading="lazy" width="480" height="320">
        </a>
      </li>
    </ul>
  </div>
</section>

<section v-if="records.length" class="band" aria-labelledby="program-records-title">
  <div class="container">
    <h2 id="program-records-title" class="section-title">{{ tx('執行紀錄', 'Implementation records') }}</h2>
    <ol class="program-records">
      <li v-for="r in records" :key="r.id" class="program-records__item">
        <p class="program-records__date">{{ slashDate(r.happenedOn) ?? tx('日期未標', 'Undated') }}<span v-if="r.location"> · {{ r.location }}</span></p>
        <p class="program-records__title">{{ r.charityName }}</p>
        <p v-if="r.donationContent" class="program-records__desc">{{ tx('捐助內容：', 'Donated: ') }}{{ r.donationContent }}</p>
        <p v-if="r.briefDescription" class="program-records__desc">{{ r.briefDescription }}</p>
      </li>
    </ol>
    <p style="margin-top:1.5rem"><a class="btn btn--dark btn--sm" :href="lp('/zh/charity/impact-stories/')">{{ tx('查看全部慈善事蹟', 'View all impact stories') }}</a></p>
  </div>
</section>

<section v-if="program?.partners.length || program?.sponsors.length" class="band sponsor-band" aria-labelledby="program-sponsors-title">
  <div class="container">
    <h2 id="program-sponsors-title" class="section-title">{{ tx('共同參與的夥伴與贊助商', 'Partners and sponsors taking part') }}</h2>
    <div class="sponsor-grid">
      <PartnerLogoTile v-for="p in program.partners" :key="`p-${p.slug}`" :name="p.name" :logo-url="pickLogoUrl(p)" :href="lp('/zh/partners/our-partners/')" />
      <PartnerLogoTile v-for="s in program.sponsors" :key="`s-${s.slug}`" :name="s.name" :logo-url="pickLogoUrl(s)" :href="lp('/zh/partners/our-sponsors/')" />
    </div>
  </div>
</section>

<section v-if="program?.articles.length" class="band" aria-labelledby="program-articles-title">
  <div class="container">
    <h2 id="program-articles-title" class="section-title">{{ tx('相關報導', 'Related news') }}</h2>
    <ul class="program-articles">
      <li v-for="a in program.articles" :key="a.slug"><a :href="lp(`/zh/news/${a.slug}/`)">{{ a.title }}</a></li>
    </ul>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="program-cta-title">
  <div class="band-inner container">
    <h2 class="section-title" id="program-cta-title">{{ tx('一起參與', 'Get involved') }}</h2>
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/charity/programs/')">
        <span class="cta-card__num">11.2</span>
        <span class="cta-card__title">{{ tx('所有慈善計畫', 'All charity programs') }}</span>
        <p class="cta-card__desc">{{ tx('回到計畫列表', 'Back to the program list') }}</p>
      </a>
      <a class="cta-card" :href="donateLink.href" :target="donateLink.external ? '_blank' : undefined" :rel="donateLink.external ? 'noopener noreferrer' : undefined">
        <span class="cta-card__num">01</span>
        <span class="cta-card__title">{{ tx('球迷捐款', 'Fan donations') }}</span>
        <p class="cta-card__desc"><template v-if="isEn">Fan donations are handled by the charity donation platform of the {{ CHARITY_RECIPIENT_EN }}, not donated to {{ clubName }}.</template><template v-else>球迷捐款由{{ CHARITY_RECIPIENT }}的慈善捐款平台承接，不是捐給{{ clubName }}</template></p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
.program-detail{ display:grid; grid-template-columns:minmax(0,1fr) 320px; gap:3rem; align-items:start; }
.program-detail__para{ white-space:pre-line; }
.program-detail__aside{ background:var(--paper-2); border:1px solid var(--rule); padding:1.5rem; }
.program-detail__aside-title{ font-size:1rem; font-weight:800; margin-bottom:1rem; }
.program-detail__aside-sub{ font-size:.85rem; font-weight:800; margin:1.5rem 0 .75rem; color:var(--muted); }
.program-facts{ display:flex; flex-direction:column; gap:.5rem; }
.program-facts div{ display:flex; gap:.6rem; font-size:.88rem; }
.program-facts dt{ font-weight:700; color:var(--muted); flex:none; min-width:3em; }
.charity-org{ display:flex; gap:1rem; align-items:flex-start; }
.charity-org img{ width:64px; height:64px; object-fit:contain; background:var(--paper); border:1px solid var(--rule); flex:none; }
.charity-org__name{ font-weight:800; color:var(--heading); }
.charity-org__intro{ font-size:.82rem; line-height:1.6; color:var(--muted); margin-top:.3rem; }
.charity-org__intro a{ color:var(--brand-aa); text-decoration:underline; }
.program-gallery{ list-style:none; padding:0; margin:1.5rem 0 0; display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:1rem; }
.program-gallery img{ width:100%; aspect-ratio:3/2; object-fit:cover; display:block; }
.program-records{ list-style:none; padding:0; margin:1.5rem 0 0; display:flex; flex-direction:column; gap:1.25rem; max-width:720px; }
.program-records__item{ border-left:3px solid var(--brand-aa); padding-left:1rem; }
.program-records__date{ font-size:.8rem; font-weight:800; color:var(--muted); }
.program-records__title{ font-weight:800; color:var(--heading); margin-top:.2rem; }
.program-records__desc{ font-size:.88rem; line-height:1.7; color:var(--text); margin-top:.2rem; white-space:pre-line; }
.program-articles{ margin-top:1rem; display:flex; flex-direction:column; gap:.6rem; }
.program-articles a{ color:var(--brand-aa); text-decoration:underline; font-weight:700; }
.is-pending{ color:var(--muted); font-style:italic; }
.status-chip{ font-size:.65rem; font-weight:800; letter-spacing:.05em; padding:.3rem .6rem; text-transform:uppercase; }
.status-chip--ongoing{ background:rgba(224,33,138,.1); color:var(--brand-aa); }
.status-chip--done{ background:var(--paper-2); color:var(--muted); border:1px solid var(--rule); }
@media (max-width:900px){ .program-detail{ grid-template-columns:1fr; } }
</style>
