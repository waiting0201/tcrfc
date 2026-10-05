<script setup lang="ts">
// app/pages/zh/shop/policy/index.vue — 8.3 購物須知、運送說明、退換貨政策、交易條款（S3-5）
//
// 主站規劃書 §3.8 8.3「退換貨：前台提供退換貨政策頁與申請管道（表單或客服信箱，不做專屬的線上退貨精靈）」、
// §2 Footer「官方商店入口與購物須知／退換貨政策連結」。內容由後台 S6「商店入口與政策」維護（中／英），
// 來源 `GET /api/shop/info`；後台沒填的段落整段不顯示，全部沒填顯示「準備中」，不編造條文。
// 條文是後台人員輸入的純文字：`white-space: pre-line` 保留換行，**不 v-html**。
// 申請管道＝一般聯絡表單（10.6）；退款走後台 S5 人工審核，原路退回 LINE Pay。
import type { ShopInfo } from '#shared/utils/shop'

definePageMeta({ nav: 'culture', unit: '8.3', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => getShopClubNameEn(config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const SECTIONS = [
  { id: 'notice', title: '購物須知', titleEn: 'Shopping information', key: 'policyNotice' },
  { id: 'shipping', title: '運送說明', titleEn: 'Shipping', key: 'policyShipping' },
  { id: 'returns', title: '退換貨政策', titleEn: 'Returns policy', key: 'policyReturns' },
  { id: 'terms', title: '交易條款', titleEn: 'Terms of sale', key: 'policyTerms' },
] as const
// 只取政策四段（`transform`）：Nuxt 會把 useFetch 結果序列化進頁面 payload，整份 `info` 帶著收款主體名稱，
// 藍鯨站的這頁就會在 payload 裡出現磐石字樣——本頁沒有顯示收款主體，所以不該帶。
const { data: info } = await useFetch<Record<(typeof SECTIONS)[number]['key'], string | null> | null>('/api/shop/info', {
  query: computed(() => ({ lang: locale.value })),
  key: `shop-policy-${config.public.club}-${locale.value}`,
  default: () => null,
  transform: (i: ShopInfo): Record<(typeof SECTIONS)[number]['key'], string | null> => ({
    policyNotice: i.policyNotice, policyShipping: i.policyShipping, policyReturns: i.policyReturns, policyTerms: i.policyTerms,
  }),
})

const sections = computed(() => SECTIONS
  .map(s => ({ ...s, body: (info.value?.[s.key] ?? '').trim() }))
  .filter(s => s.body))
// 英文頁：後台沒填英文條文時後端回繁中備援（沒有旗標），偵測到漢字就在內容上方提示「部分內容只有繁體中文」
const bodyFallback = computed(() => isEn.value && shopHasCjk(...sections.value.map(s => s.body)))

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('policy', clubNameEn.value).title : `購物須知與退換貨政策｜官方商店｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? getShopSeoEn('policy', clubNameEn.value).description : `${clubAssets.value.nameZh}官方商店的購物須知、運送說明、退換貨政策與交易條款。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/shop/')">{{ tx('官方商店', 'Shop') }}</a></li>
      <li aria-current="page">{{ tx('購物須知與退換貨政策', 'Shopping information and returns policy') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Shop Policies</p>
    <h1>{{ tx('購物須知與退換貨政策', 'Shopping information and returns policy') }}<span v-if="!isEn" class="en">Shop Policies</span></h1>
  </div>
</section>

<section class="band" aria-labelledby="policy-lede">
  <div class="band-inner container">
    <LocaleFallbackNotice v-if="bodyFallback" partial />
    <h2 id="policy-lede" class="visually-hidden">{{ tx('政策內容', 'Policy details') }}</h2>
    <div v-if="sections.length" class="prose">
      <section v-for="s in sections" :id="s.id" :key="s.id" class="sh-policy">
        <h3>{{ isEn ? s.titleEn : s.title }}</h3>
        <p class="sh-narrative">{{ s.body }}</p>
      </section>
    </div>
    <p v-else class="mc-empty">{{ tx('購物須知與退換貨政策內容準備中，稍後將於本頁公布。', 'The shopping information and returns policy are being prepared and will be published on this page soon.') }}</p>

    <div class="editorial-note" style="margin-top:2.5rem">
      <h3>{{ tx('如何申請退換貨', 'How to request a return or exchange') }}</h3>
      <p v-if="isEn">Under Taiwan's Consumer Protection Act you have a seven-day cooling-off period. Please submit a request through the <a :href="lp('/zh/join/general/')">general contact form</a> and include your order number. We will check and inspect the item manually, then process the refund (returned to LINE Pay by the original route) and void the invoice or issue a discount note.</p>
      <p v-else>依《消費者保護法》享七日猶豫期。請以<a :href="lp('/zh/join/general/')">一般聯絡表單</a>提出申請並附上訂單編號，我們會人工確認、驗收後處理退款（原路退回 LINE Pay）與發票作廢或折讓。</p>
      <p style="margin-top:.75rem"><a class="btn btn--light btn--sm" :href="lp('/zh/order/lookup/')">{{ tx('查詢訂單', 'Order lookup') }}</a></p>
    </div>
  </div>
</section>
</template>
