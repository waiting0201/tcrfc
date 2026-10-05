<script setup lang="ts">
// app/pages/zh/culture/merchandise/index.vue — 8.3 官方商品（櫥窗頁）
//
// 2026-10-02：商品卡改接站內商店 API（`GET /api/shop/products`＋`GET /api/shop/info` 的系列清單，
// 與 /zh/shop/ 同一份資料），原本寫死的兩款磐石商品（主場球衣、機能襪，含尺碼表與價格）、
// 「Academy／Fan Collection 開發中」兩張空卡與「前端流程骨架」旗標已移除。商品、價格、尺寸、
// 庫存全由後台商店模組（S1）維護；某系列沒有商品就不出現，整間店沒有商品顯示空狀態。
// 藍鯨走同一個元件，只看自己俱樂部的商品（API 依站台範圍回傳），不再有磐石專屬分支。
import type { PagedResponse } from '#shared/utils/api-types'
import type { ShopCollection, ShopInfo, ShopProductListItem } from '#shared/utils/shop'

definePageMeta({ nav: 'culture', unit: '8.3', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const isTcrfc = computed(() => config.public.club !== 'bw')
// BW-C1（品牌外洩全站盤點）：頁首大圖是磐石主場球衣實拍，藍鯨用純色佔位。「舊官網選購」過渡期
// 文案保留 www.tcrfc.tw 網域字面值，只對磐石輸出（check-club-brand-leak.mjs 檢查藍鯨站輸出）。
const clubAssets = computed(() => getClubAssets(config.public.club))
const identity = computed(() => (isEn.value ? (config.public.club === 'bw' ? getClubIdentityEnBw() : getClubIdentityEn()) : getClubIdentity(config.public.club)))

// 系列清單（名稱、說明、排序）取自 `GET /api/shop/info`；只留 `collections`，其餘欄位（含 `collectingSubjectName`
// 「款項由台中磐石足球俱樂部代收」）不進頁面 payload——藍鯨站該字串只允許出現在 /shop/ 與 /checkout/
// （check-club-brand-leak.mjs 例外清單），本頁不得因為共用 API 回應而把它序列化進藍鯨的 HTML。
const { data: shopCollections } = await useAsyncData(
  `merch-collections-${config.public.club}-${locale.value}`,
  async () => {
    const info = await $fetch<ShopInfo | null>('/api/shop/info', { query: { lang: locale.value } }).catch(() => null)
    return info?.collections ?? []
  },
  { default: () => [] as ShopCollection[] },
)
const { data: productList } = await useFetch<PagedResponse<ShopProductListItem> | null>('/api/shop/products', {
  query: { pageSize: 60, lang: locale.value },
  key: `merch-products-${config.public.club}-${locale.value}`,
  default: () => null,
})
const products = computed(() => productList.value?.items ?? [])
/** 依後台系列排序分組；沒有商品的系列不顯示；未歸入任何系列的商品放最後的「其他商品」。 */
const groups = computed(() => {
  const list = products.value
  const out: { slug: string, name: string, narrative: string | null, items: ShopProductListItem[] }[] = []
  for (const c of shopCollections.value ?? []) {
    const items = list.filter((p) => p.collectionSlug === c.slug)
    if (items.length) out.push({ slug: c.slug, name: c.name || c.slug, narrative: c.narrative, items })
  }
  const known = new Set(out.flatMap((g) => g.items.map((p) => p.slug)))
  const rest = list.filter((p) => !known.has(p.slug))
  if (rest.length) out.push({ slug: 'other', name: out.length ? tx('其他商品', 'Other products') : tx('官方商品', 'Merchandise'), narrative: null, items: rest })
  return out
})
const hasProducts = computed(() => products.value.length > 0)

useSeoMeta({
  title: computed(() => (isEn.value ? (isTcrfc.value ? getMerchandiseSeoEn(hasProducts.value) : getMerchandiseSeoEnBw(hasProducts.value)).title : `官方商品 Merchandise｜${identity.value.cultureLabelZh}｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? (isTcrfc.value ? getMerchandiseSeoEn(hasProducts.value) : getMerchandiseSeoEnBw(hasProducts.value)).description : hasProducts.value
    ? `${clubAssets.value.nameZh}官方商品：依系列瀏覽，於本站官方商店選尺寸與顏色、以 LINE Pay 付款並開立電子發票。`
    : `${clubAssets.value.nameZh}官方商品。商品內容由後台提供，目前尚無可顯示的商品。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li aria-current="page">{{ tx('官方商品', 'Merchandise') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/merch/merch-jersey-01.jpg')" :alt="tx('球員身著台中磐石桃紅色主場球衣，胸前印有 Joma、TCRFC 隊徽與 San Pellegrino 贊助字樣', 'A player wearing the Taichung Rock pink home jersey, with Joma, the TCRFC crest and San Pellegrino sponsor lettering on the chest')" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">8.3 Merchandise</p>
    <h1>{{ tx('官方商品', 'Merchandise') }}<span v-if="!isEn" class="en">Merchandise</span></h1>
    <p v-if="isEn && hasProducts" class="page-hero__lede">Browse official merchandise by collection. To buy, go to the <a :href="lp('/zh/shop/')" style="color:inherit;text-decoration:underline">official store</a>: choose a size and colour, add to your cart, pay with LINE Pay and receive an e-invoice.</p>
    <p v-else-if="isEn" class="page-hero__lede">Official merchandise information is provided through the back office, and there is nothing to display at the moment.</p>
    <p v-else-if="hasProducts" class="page-hero__lede">官方商品依系列瀏覽。要選購請前往<a :href="lp('/zh/shop/')" style="color:inherit;text-decoration:underline">官方商店</a>：選尺寸與顏色、加入購物車，以 LINE Pay 付款並開立電子發票。</p>
    <p v-else class="page-hero__lede">{{ clubAssets.shortNameZh }}官方商品資料由後台提供，目前尚無可顯示的內容。</p>
  </div>
</section>

<!-- SPEC 3.8 §8.3 — 依系列呈現官方商品（資料：站內商店 API）；沒有商品顯示空狀態 -->
<section v-for="g in groups" :id="`collection-${g.slug}`" :key="g.slug" class="band" :aria-labelledby="`collection-${g.slug}-title`">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">COLLECTION</p>
        <h2 :id="`collection-${g.slug}-title`" class="section-title">{{ g.name }}</h2>
      </div>
      <p v-if="g.narrative" class="section-lede">{{ g.narrative }}</p>
    </div>
    <div class="product-grid">
      <article v-for="p in g.items" :key="p.slug" class="product-card">
        <a class="product-card__media" :href="lp(`/zh/shop/${p.slug}/`)" :aria-label="p.name || p.slug">
          <span v-if="p.stockStatus === 'sold_out'" class="product-card__flag product-card__flag--muted">{{ tx('缺貨', 'Sold out') }}</span>
          <span v-else-if="p.onSale" class="product-card__flag">{{ tx('優惠', 'On sale') }}</span>
          <span v-else-if="p.isNewArrival" class="product-card__flag">{{ tx('新上市', 'New') }}</span>
          <img v-if="p.imageThumbUrl || p.imageUrl" :src="(p.imageThumbUrl || p.imageUrl) ?? ''" :alt="p.name || ''" loading="lazy" width="640" height="640">
        </a>
        <div class="product-card__body">
          <p class="product-card__name"><a :href="lp(`/zh/shop/${p.slug}/`)">{{ p.name || p.slug }}</a></p>
          <div class="product-card__foot">
            <template v-if="p.priceMin !== null">
              <span :class="['price', { 'price--sale': p.onSale }]">{{ formatPriceRange(p.priceMin, p.priceMax) }}</span>
              <span v-if="p.onSale && p.listPriceMin" class="price__was">{{ formatPrice(p.listPriceMin) }}</span>
            </template>
            <span v-else class="price__was">{{ tx('暫無販售', 'Not currently available') }}</span>
          </div>
          <a class="btn btn--primary btn--sm btn--block" :href="lp(`/zh/shop/${p.slug}/`)" style="margin-top:1rem">{{ p.stockStatus === 'sold_out' ? tx('查看商品', 'View product') : tx('選購', 'Buy') }}</a>
        </div>
      </article>
    </div>
  </div>
</section>

<section v-if="!hasProducts" id="club-collection" class="band" aria-labelledby="club-collection-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">MERCHANDISE</p>
        <h2 id="club-collection-title" class="section-title">{{ tx('官方商品', 'Merchandise') }}</h2>
      </div>
    </div>
    <p class="is-pending">{{ tx('商品內容由後台提供，目前尚無可顯示的商品。', 'Product content is provided through the back office, and there are no products to display yet.') }}</p>
  </div>
</section>

<section v-if="hasProducts" class="band grain grain--2 store-band" aria-labelledby="store-cta-title">
  <div class="band-inner container">
    <div class="store-band__grid" style="grid-template-columns:1fr">
      <div>
        <p class="kicker kicker--on-dark">ONLINE STORE</p>
        <h2 class="section-title" id="store-cta-title">{{ tx('前往官方商店', 'Go to the official store') }}</h2>
        <p v-if="isEn">Choose a size and colour, add to your cart, pay with <strong>LINE Pay</strong> and receive an automatic <strong>e-invoice</strong>. You can choose home delivery, convenience-store pickup or in-person pickup. Orders can be looked up in the Member Centre, and non-members can look up an order with the order number and email.</p>
        <p v-else>選尺寸與顏色、加入購物車，以 <strong>LINE Pay</strong> 付款並自動開立<strong>電子發票</strong>，可宅配、超商取貨或現場自取；訂單於會員中心查詢，未註冊者以訂單編號與 Email 查詢。</p>
        <p style="margin:1.25rem 0"><a class="btn btn--primary" :href="lp('/zh/shop/')">{{ tx('前往官方商店', 'Go to the official store') }}</a></p>
        <p v-if="isEn && isTcrfc" class="store-band__fine">Until the official launch, you can still buy from the shop on the old website <a href="https://www.tcrfc.tw" target="_blank" rel="noopener" style="color:inherit">www.tcrfc.tw</a>; once the online store goes live, the old shop will stop selling.</p>
        <p v-else-if="isTcrfc" class="store-band__fine">正式上線前，仍可透過舊官網 <a href="https://www.tcrfc.tw" target="_blank" rel="noopener" style="color:inherit">www.tcrfc.tw</a> 的商店選購；站內商店上線後舊商店將停售。</p>
      </div>
    </div>
  </div>
</section>
</template>

<style>
.is-pending{ color:var(--muted); font-style:italic; }
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
</style>
