<script setup lang="ts">
// app/pages/zh/faq/join-team/index.vue — 12 FAQ「加入球隊」獨立主題頁（S1-18 新增）
//
// 規劃書 3.12「分類頁：各主題獨立頁面，具備獨立 SEO 設定」——本輪先建 3–4 個高頻主題
// （見 apps/web/README.md「S1-18」節取捨說明），本頁是其中之一。資料來源與 12 FAQ
// 首頁同一套 composable（useFaqCategories／useFaqList），只是帶 category 篩選單一分類。
definePageMeta({ nav: '', unit: '12' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const CATEGORY_SLUG = 'join-team'

const siteName = computed(() => getClubAssets(club).nameZh)
const { categories } = useFaqCategories(locale.value)
const { faqs, totalCount } = useFaqList(club, locale.value, CATEGORY_SLUG)

const categoryName = computed(
  () => categories.value.find((c) => c.slug === CATEGORY_SLUG)?.name ?? '加入球隊',
)

useSeoMeta({
  title: () => `${categoryName.value}常見問題｜12 FAQ｜${siteName.value}`,
  description: () => `${siteName.value}「${categoryName.value}」主題常見問題，共 ${totalCount.value} 題。`,
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/faq/')">常見問題</a></li>
      <li aria-current="page">{{ categoryName }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">12</span>
  <div class="container">
    <p class="page-hero__eyebrow">12 FAQ</p>
    <h1>{{ categoryName }}<span class="en">FAQ</span></h1>
    <p class="page-hero__lede">「{{ categoryName }}」主題的常見問題整理，共 {{ totalCount }} 題。</p>
  </div>
</section>

<section class="band">
  <div class="band-inner container">
    <FaqAccordion :faqs="faqs" :club="club" />

    <div class="faq-fallback-cta">
      <p>沒有找到你要的答案？</p>
      <a class="btn btn--primary" :href="lp('/zh/join/general/')">聯絡我們</a>
      <a class="faq-back-link" :href="lp('/zh/faq/')">查看全部常見問題 →</a>
    </div>
  </div>
</section>
</template>

<style>
.faq-fallback-cta{
  text-align:center; padding:3rem 1.5rem; background:var(--paper-2); border:1px solid var(--rule); margin-top:2rem;
}
.faq-fallback-cta p{ font-weight:700; color:var(--heading); margin-bottom:1rem; }
.faq-back-link{ display:block; margin-top:1rem; font-size:.9rem; color:var(--brand-aa); font-weight:700; }
</style>
