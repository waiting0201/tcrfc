<script setup lang="ts">
// app/pages/zh/member-terms/index.vue — 會員條款（A-11，2026-10-06）
// 內容讀後台「政策與條款」的 `member-terms`（`usePolicy('member-terms')`，純文字、空行分段，以文字節點輸出，不得 v-html）。
// 🔴 不自擬條款：後台尚未填寫時只顯示「整理中」說明，不放任何預設條文（會員條款的正式文字待法務定稿，B-9）。
// 網址命名比照 `/zh/privacy/`、`/zh/cookies/`：`/zh/member-terms/`（en 孿生路由由 pages:extend 自動產生）。
definePageMeta({ nav: '', unit: 'G-07', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => (config.public.club === 'bw' ? BW_NAME_EN : 'Taichung Rock FC'))

const { policy, paragraphs } = await usePolicy('member-terms')

useSeoMeta({
  title: computed(() => (isEn.value ? `Membership Terms | ${clubNameEn.value}` : `會員條款 Membership Terms｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? `Membership terms of ${clubNameEn.value}.` : `${clubAssets.value.nameZh}會員條款：註冊與使用會員服務前請先閱讀。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('會員條款', 'Membership Terms') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Legal</p>
    <h1><template v-if="isEn">Membership Terms</template><template v-else>會員條款<span class="en">Membership Terms</span></template></h1>
    <p class="page-hero__lede">{{ tx('註冊與使用會員服務前，請先閱讀以下條款。', 'Please read these terms before registering for or using membership services.') }}</p>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && policy?.isFallbackLocale" partial />

<section class="band legal-band" aria-labelledby="member-terms-title">
  <div class="band-inner container">
    <h2 id="member-terms-title" class="visually-hidden">{{ tx('會員條款內文', 'Membership Terms') }}</h2>
    <div v-if="policy" class="prose policy-body">
      <p v-for="(para, i) in paragraphs" :key="i" class="policy-body__p">{{ para }}</p>
      <p v-if="policy.updatedAt" class="policy-body__updated">{{ tx('最後更新：', 'Last updated: ') }}{{ policy.updatedAt.slice(0, 10).replaceAll('-', '/') }}</p>
    </div>
    <p v-else class="is-pending">{{ tx('會員條款內容整理中，稍後公布。', 'The membership terms are being prepared and will be published soon.') }}</p>
  </div>
</section>
</template>

<style>
.legal-band{ padding-block:clamp(3.5rem,6vw,6rem); }
.policy-body{ max-width:78ch; }
.policy-body__p{ white-space:pre-line; overflow-wrap:anywhere; margin:0 0 1.1rem; line-height:1.85; }
.policy-body__updated{ margin-top:2rem; font-size:.82rem; color:var(--muted); }
.is-pending{ color:var(--muted); font-style:italic; }
</style>
