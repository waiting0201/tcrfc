<script setup lang="ts">
// app/pages/zh/faq/academy-admission/index.vue — 12 FAQ「學院招生」獨立主題頁（S1-18 新增）
//
// 規劃書 3.12「分類頁：各主題獨立頁面，具備獨立 SEO 設定」——本輪先建 3–4 個高頻主題
// （見 apps/web/README.md「S1-18」節取捨說明），本頁是其中之一。資料來源與 12 FAQ
// 首頁同一套 composable（useFaqCategories／useFaqList），只是帶 category 篩選單一分類。
//
// 🔴 分類名稱「學院招生」來自全站共用的 faq_categories 主檔（不分俱樂部，見
// useFaqCategories.ts 檔頭），藍鯨容器本頁會顯示同一個名稱——藍鯨規劃書 §3「04 由
// 學院改為青年隊」是內容取捨不是這張分類字典的欄位，本頁不臆自改名，維持後端真實
// 資料，此為既有資料設計的已知限制，非本輪引入的迴歸（見 apps/web/README.md
// 「S1-18」節）。
// unit '12.2'：藍鯨規劃書 §3「04 青年隊不沿用學院的招生與課程報名架構」——
// 本頁就是磐石學院招生流程與費用表，同 4.7（加入學院）的關閉理由，見
// shared/utils/units.ts 的 BLUE_WHALE_DISABLED_UNITS 說明。
definePageMeta({ nav: '', unit: '12.2' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const CATEGORY_SLUG = 'academy-admission'

const siteName = computed(() => getClubAssets(club).nameZh)
const { categories } = useFaqCategories(locale.value)
const { faqs, totalCount } = useFaqList(club, locale.value, CATEGORY_SLUG)

const categoryName = computed(
  () => categories.value.find((c) => c.slug === CATEGORY_SLUG)?.name ?? '學院招生',
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
