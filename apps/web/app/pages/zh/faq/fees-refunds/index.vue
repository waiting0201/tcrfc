<script setup lang="ts">
// app/pages/zh/faq/fees-refunds/index.vue — 12 FAQ「費用與退費」獨立主題頁（S1-18 新增）
//
// 規劃書 3.12「分類頁：各主題獨立頁面，具備獨立 SEO 設定」——本輪先建 3–4 個高頻主題
// （見 apps/web/README.md「S1-18」節取捨說明），本頁是其中之一。資料來源與 12 FAQ
// 首頁同一套 composable（useFaqCategories／useFaqList），只是帶 category 篩選單一分類。
definePageMeta({ nav: '', unit: '12', enReady: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const CATEGORY_SLUG = 'fees-refunds'

const siteName = computed(() => (isEn.value ? CLUB_NAME_EN : getClubAssets(club).nameZh))
const { categories } = useFaqCategories(locale.value)
const { faqs, totalCount } = useFaqList(club, locale.value, CATEGORY_SLUG)

const categoryName = computed(
  () => categories.value.find((c) => c.slug === CATEGORY_SLUG)?.name ?? tx('費用與退費', 'Fees & Refunds'),
)

useSeoMeta({
  title: () => (isEn.value ? `${categoryName.value} FAQ | 12 FAQ | ${siteName.value}` : `${categoryName.value}常見問題｜12 FAQ｜${siteName.value}`),
  description: () => (isEn.value ? `Frequently asked questions on “${categoryName.value}” from ${siteName.value}: ${totalCount.value} ${totalCount.value === 1 ? 'question' : 'questions'} in total.` : `${siteName.value}「${categoryName.value}」主題常見問題，共 ${totalCount.value} 題。`),
})

// GEO-06（S1-18a）：本頁只有一個 FAQ 區塊（單一分類），直接把 useFaqList() 撈回來的
// 完整清單餵給 useFaqPageSchema()，不合格題目（question／answer 任一為 null）由
// buildFaqSchemaQuestions() 過濾，不需要在這裡先篩一次。
useFaqPageSchema(faqs)

// 英文版：後端 `?lang=en` 逐欄位回退繁中，FAQ 回應沒有旗標，偵測漢字決定是否提示「部分內容只有繁體中文」。
const hasZhContent = computed(() => isEn.value && faqs.value.some((f) => /[\u3400-\u9fff]/.test(`${f.question ?? ''}${f.answer ?? ''}`)))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/faq/')">{{ tx('常見問題', 'FAQ') }}</a></li>
      <li aria-current="page">{{ categoryName }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">12</span>
  <div class="container">
    <p class="page-hero__eyebrow">12 FAQ</p>
    <h1>{{ categoryName }}<span class="en">FAQ</span></h1>
    <p class="page-hero__lede">{{ isEn ? `Frequently asked questions on “${categoryName}”: ${totalCount} ${totalCount === 1 ? 'question' : 'questions'} in total.` : `「${categoryName}」主題的常見問題整理，共 ${totalCount} 題。` }}</p>
  </div>
</section>

<LocaleFallbackNotice v-if="hasZhContent" partial />

<section class="band">
  <div class="band-inner container">
    <FaqAccordion :faqs="faqs" :club="club" />

    <div class="faq-fallback-cta">
      <p>{{ tx('沒有找到你要的答案？', 'Could not find the answer you need?') }}</p>
      <a class="btn btn--primary" :href="lp('/zh/join/general/')">{{ tx('聯絡我們', 'Contact Us') }}</a>
      <a class="faq-back-link" :href="lp('/zh/faq/')">{{ tx('查看全部常見問題 →', 'View all FAQs →') }}</a>
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
