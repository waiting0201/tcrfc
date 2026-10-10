<script setup lang="ts">
// app/pages/zh/charity/programs/index.vue — 11.2 慈善計畫列表（S2-9 起接上真實 API）
//
// 由 charity/programs.vue 搬為 programs/index.vue：新增 `programs/[slug]/index.vue`（計畫詳情）後，
// 同名的 programs.vue 會被 Nuxt 視為巢狀路由的父層（要求 <NuxtPage/>），所以改成目錄形式
// （比照 news/index.vue＋news/[slug]/index.vue 既有做法）。URL 不變：`/zh/charity/programs/`。
//
// 資料來源：`GET /api/backend/{club}/charity/programs?lang=&page=&pageSize=`（後台 B5，只回已發布；排序＝
// 置頂優先、排序值、開始日新到舊）。`progress`（進行中／已完成）由期間推導，不是發布狀態。
// 分頁用 `?page=N` 的一般連結（SSR 可爬、不依賴 JS），每頁 12 筆。
// 🔴 單元 11 對藍鯨整頁 404（藍鯨規劃書 §2.1），本頁只有磐石會被存取。
// 🔴 空狀態：沒有已發布計畫（含 API 打不到）維持既有「尚無已公開的慈善計畫」說明，不放假資料。
import type { CharityProgramListItem } from '#shared/utils/charity'
import type { PagedResponse } from '#shared/utils/api-types'

definePageMeta({ nav: 'charity', unit: '11', enReady: true })

const { lp, locale, isEn, tx } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const club = config.public.club
const { donateLink } = await useCharityCta()

const PAGE_SIZE = 12
const page = computed(() => {
  const n = Number(Array.isArray(route.query.page) ? route.query.page[0] : route.query.page)
  return Number.isInteger(n) && n >= 1 ? n : 1
})

const { data } = await useFetch<PagedResponse<CharityProgramListItem>>(`/api/backend/${club}/charity/programs`, {
  query: computed(() => ({ lang: locale.value, page: page.value, pageSize: PAGE_SIZE })),
  key: `charity-programs-${club}-${locale.value}`,
  watch: [page],
})
const programs = computed(() => data.value?.items ?? [])
const totalPages = computed(() => data.value?.totalPages ?? 0)

function pageHref(n: number): string {
  return n <= 1 ? route.path : `${route.path}?page=${n}`
}

useSeoMeta({
  title: computed(() => (isEn.value ? 'Charity Programs | Charity & Impact | Taichung Rock FC' : '慈善計畫 Charity Programs｜慈善與社會影響｜台中磐石足球俱樂部')),
  description: computed(() => (isEn.value
    ? 'The charity programs of Taichung Rock FC, with details of each: background, beneficiary charity, what was donated, how it was carried out and a photo gallery.'
    : '台中磐石足球俱樂部的慈善計畫列表與詳情：計畫緣起、受贈公益團體、捐助內容、執行過程與活動圖片藝廊。')),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/charity/')">{{ tx('慈善與社會影響', 'Charity & Impact') }}</a></li>
      <li aria-current="page">{{ tx('慈善計畫', 'Charity Programs') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true" style="left:var(--edge);bottom:-1.8rem;">11.2</span>
  <div class="container">
    <p class="page-hero__eyebrow">11.2 Charity Programs</p>
    <h1><template v-if="isEn">Charity Programs</template><template v-else>{{ tx('慈善計畫', 'Charity Programs') }}<span class="en">Charity Programs</span></template></h1>
    <p class="page-hero__lede">{{ tx('俱樂部正在進行與已完成的公益計畫，包含計畫緣起、受贈公益團體與捐助內容。', 'Charity programs the club is currently running or has completed, including each program\'s background, beneficiary charity and what was donated.') }}</p>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(data)" partial />

<section class="band programs-band" aria-labelledby="programs-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="programs-title">{{ tx('慈善計畫列表', 'Charity program list') }}</h2>

    <div v-if="programs.length" class="program-grid">
      <a v-for="p in programs" :key="p.id" class="program-card" :href="lp(`/zh/charity/programs/${p.slug}/`)">
        <div class="program-card__cover">
          <img v-if="p.coverUrl" :src="p.coverUrl" :alt="imgAlt(p.coverAlt, p.name)" loading="lazy" v-bind="imgAttrs(p.coverWidth, p.coverHeight)">
        </div>
        <div class="program-card__body">
          <div class="program-card__meta-row">
            <span :class="['status-chip', p.progress === 'completed' ? 'status-chip--done' : 'status-chip--ongoing']">{{ progressLabel(p.progress, isEn) }}</span>
          </div>
          <h3 class="program-card__name">{{ p.name }}</h3>
          <dl class="program-card__facts">
            <div v-if="p.targetAudience"><dt>{{ tx('對象', 'Audience') }}</dt><dd>{{ p.targetAudience }}</dd></div>
            <div v-if="programPeriod(p.startOn, p.endOn)"><dt>{{ tx('期間', 'Period') }}</dt><dd>{{ programPeriod(p.startOn, p.endOn, isEn) }}</dd></div>
            <div v-if="p.charityName"><dt>{{ tx('受贈團體', 'Beneficiary') }}</dt><dd>{{ p.charityName }}</dd></div>
          </dl>
        </div>
      </a>
    </div>

    <nav v-if="totalPages > 1" class="pager" :aria-label="tx('慈善計畫分頁', 'Charity programs pagination')">
      <a v-if="page > 1" class="btn btn--dark btn--sm" :href="pageHref(page - 1)" rel="prev">{{ tx('上一頁', 'Previous') }}</a>
      <span class="pager__info"><template v-if="isEn">Page {{ page }} of {{ totalPages }}</template><template v-else>第 {{ page }} 頁／共 {{ totalPages }} 頁</template></span>
      <a v-if="page < totalPages" class="btn btn--dark btn--sm" :href="pageHref(page + 1)" rel="next">{{ tx('下一頁', 'Next') }}</a>
    </nav>

    <div v-if="!programs.length" class="empty-state">
      <p class="empty-state__title">{{ tx('尚無已公開的慈善計畫', 'No charity programs published yet') }}</p>
      <p class="empty-state__desc">{{ tx('俱樂部的公益投入持續進行中，個別計畫的緣起、受贈團體與捐助內容確認後將於此公開。歡迎企業洽談長期公益合作方案。', 'The club\'s charity work is ongoing. The background, beneficiary and donation details of each program will be published here once confirmed. Companies are welcome to discuss long-term charity partnerships.') }}</p>
      <a class="btn btn--dark btn--sm" :href="lp('/zh/partners/opportunities/')">{{ tx('洽談企業合作', 'Discuss corporate partnerships') }}</a>
    </div>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="programs-cta-title">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;color:rgba(255,255,255,.06);">11.2</span>
  <div class="band-inner container">
    <h2 class="section-title" id="programs-cta-title">{{ tx('相關內容', 'Related content') }}</h2>
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/charity/impact-stories/')">
        <span class="cta-card__num">11.3</span>
        <span class="cta-card__title">{{ tx('慈善事蹟', 'Impact Stories') }}</span>
        <p class="cta-card__desc">{{ tx('已落地的公益行動時間軸', 'A timeline of the charity actions we have carried out') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/news/community/')">
        <span class="cta-card__num">7.7</span>
        <span class="cta-card__title">{{ tx('社區活動新聞', 'Community news') }}</span>
        <p class="cta-card__desc">{{ tx('與慈善計畫相關的即時報導', 'Timely reports related to our charity programs') }}</p>
      </a>
      <a class="cta-card" :href="donateLink.href" :target="donateLink.external ? '_blank' : undefined" :rel="donateLink.external ? 'noopener noreferrer' : undefined">
        <span class="cta-card__num">01</span>
        <span class="cta-card__title">{{ tx('支持特定計畫', 'Support a specific program') }}</span>
        <p class="cta-card__desc"><template v-if="isEn">Fan donations are handled by the charity donation platform of the {{ CHARITY_RECIPIENT_EN }}. You can choose which item to support when you donate.</template><template v-else>球迷捐款由{{ CHARITY_RECIPIENT }}的慈善捐款平台承接，捐款時可指定支持的項目</template></p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* ── 11.2 專屬元件：program-card 計畫卡片、status-chip（S2-9 起實際使用，卡片整張是連結） ── */
.programs-band{ padding-block:clamp(3.5rem,6vw,6rem); }

.program-grid{ display:grid; grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); gap:1.5rem; }
.program-card{ background:var(--paper); border:1px solid var(--rule); display:flex; flex-direction:column; text-decoration:none; color:inherit; transition:border-color var(--dur-fast) var(--ease); }
.program-card:hover{ border-color:var(--brand-aa); }
.program-card__cover{ aspect-ratio:16/10; background:var(--paper-2); overflow:hidden; }
.program-card__cover img{ width:100%; height:100%; object-fit:cover; display:block; }
.program-card__body{ padding:1.25rem 1.35rem 1.5rem; display:flex; flex-direction:column; gap:.6rem; }
.program-card__meta-row{ display:flex; gap:.5rem; }
.status-chip{ font-size:.65rem; font-weight:800; letter-spacing:.05em; padding:.3rem .6rem; text-transform:uppercase; }
.status-chip--ongoing{ background:rgba(224,33,138,.1); color:var(--brand-aa); }
.status-chip--done{ background:var(--paper-2); color:var(--muted); border:1px solid var(--rule); }
.program-card__name{ font-size:1.05rem; font-weight:800; color:var(--heading); }
.program-card__facts{ display:flex; flex-direction:column; gap:.3rem; }
.program-card__facts div{ display:flex; gap:.5rem; font-size:.82rem; }
.program-card__facts dt{ font-weight:700; color:var(--muted); flex:none; }
.program-card__facts dd{ color:var(--text); }

.pager{ display:flex; align-items:center; justify-content:center; gap:1rem; margin-top:2.5rem; flex-wrap:wrap; }
.pager__info{ font-size:.85rem; color:var(--muted); }

/* ── 空狀態（取代已移除的「待補」提示框）── 其他頁若也需要同款空狀態，建議收進共用 tcrfc.css。 */
.empty-state{
  max-width:56ch; padding:2.5rem 2rem; background:var(--paper-2); border:1px dashed var(--rule);
  display:flex; flex-direction:column; gap:1rem; align-items:flex-start;
}
.empty-state__title{ font-size:1.05rem; font-weight:800; color:var(--heading); }
.empty-state__desc{ font-size:.9rem; line-height:1.7; color:var(--muted); }
</style>
