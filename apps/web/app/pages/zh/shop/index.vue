<script setup lang="ts">
// app/pages/zh/shop/index.vue — 8.3 官方商店：商品列表（S3-5，接真實商店 API）
//
// 資料：`GET /api/shop/products`（篩選、排序、分頁全由後端做，網址 query 就是篩選狀態，`<form method="get">`
// 無 JS 也能用）＋ `GET /api/shop/info`（入口介紹、運費規則、收款主體、`paymentAvailable`）。
// 🔴 不得快取：BFF 與頁面 routeRules 皆 `no-store`（庫存狀態每次直接查庫）。
// 🔴 本頁（與 checkout）是 `check-club-brand-leak.mjs` 的例外頁：藍鯨站購物須知必須明示「款項由台中磐石足球俱樂部代收」
// （藍鯨規劃書 §5.2、主站規劃書 §1.3），固定備援文案只放在這兩頁，其他商店頁一律只用 API 的 `collectingSubjectName`。
import type { PagedResponse } from '#shared/utils/api-types'
import type { ShopProductListItem } from '#shared/utils/shop'

definePageMeta({ nav: 'culture', unit: '8.3', enReady: true, enReadyBw: true })

const COLLECTING_SUBJECT_FALLBACK = '台中磐石足球俱樂部'

const { lp, locale, isEn, tx } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const clubAssets = computed(() => getClubAssets(clubKey.value))
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(clubKey.value)))
const isBw = computed(() => clubKey.value === 'bw')
// 英文版俱樂部名稱與「文化」麵包屑（藍鯨用 BW_NAME_EN，磐石用 CLUB_NAME_EN）
const clubNameEn = computed(() => getShopClubNameEn(clubKey.value))
const cultureLabel = computed(() => (isEn.value ? getShopCultureLabelEn(clubKey.value) : identity.value.cultureLabelZh))

const { info, paymentAvailable } = useShopInfo()
const collectingSubject = computed(() => info.value?.collectingSubjectName || (isEn.value ? CLUB_NAME_EN : COLLECTING_SUBJECT_FALLBACK))
// 英文頁：後台入口文案／配送名稱若英文欄位是空的，後端會回繁中備援，這時在內容上方提示
const infoFallback = computed(() => isEn.value && shopHasCjk(info.value?.entryTitle, info.value?.entryIntro, info.value?.collectingSubjectName, ...(info.value?.deliveryMethods ?? []).map(d => d.label)))

// ── 篩選狀態（來自網址 query；形狀與 BFF 白名單一致，不合法的值視為沒有）──
function one(v: unknown): string {
  return typeof v === 'string' ? v : ''
}
const SORTS = ['newest', 'price_asc', 'price_desc']
const filters = computed(() => {
  const q = route.query
  const sort = one(q.sort)
  const intOf = (v: unknown) => (/^\d{1,9}$/.test(one(v)) ? one(v) : '')
  return {
    collection: /^[a-z0-9][a-z0-9-]{0,127}$/i.test(one(q.collection)) ? one(q.collection) : '',
    // B-18：標籤篩選（BFF 放行 `?tag=`）。形狀與 BFF 的 FILTER_TEXT 相同，不合法視為沒有，避免整頁被 400 擋掉。
    tag: /^[\p{L}\p{N} _\-./()（）+]{1,60}$/u.test(one(q.tag)) ? one(q.tag) : '',
    size: one(q.size).slice(0, 60),
    colour: one(q.colour).slice(0, 60),
    sort: SORTS.includes(sort) ? sort : '',
    minPrice: intOf(q.minPrice),
    maxPrice: intOf(q.maxPrice),
    isNew: one(q.isNew) === 'true' ? 'true' : '',
  }
})
const page = computed(() => (/^\d{1,4}$/.test(one(route.query.page)) ? Math.max(1, Number(one(route.query.page))) : 1))
const hasFilters = computed(() => Object.values(filters.value).some(Boolean))

const listQuery = computed(() => {
  const out: Record<string, string | number> = { page: page.value, pageSize: 24, lang: locale.value }
  for (const [k, v] of Object.entries(filters.value)) if (v) out[k] = v
  return out
})
const { data: list, status } = await useFetch<PagedResponse<ShopProductListItem> | null>('/api/shop/products', {
  query: listQuery,
  default: () => null,
})
// 篩選選單的尺寸／顏色來源：不帶篩選的第一頁（最多 60 件）。有篩選時另外取一份，沒有篩選時直接重用 list。
const { data: allList } = await useFetch<PagedResponse<ShopProductListItem> | null>('/api/shop/products', {
  query: { pageSize: 60, lang: locale.value },
  key: `shop-filter-source-${clubKey.value}-${locale.value}`,
  default: () => null,
})

const SIZE_ORDER = ['XS', 'S', 'M', 'L', 'XL', '2XL', '3XL', '4XL']
function sortSizes(a: string, b: string): number {
  const ia = SIZE_ORDER.indexOf(a.toUpperCase())
  const ib = SIZE_ORDER.indexOf(b.toUpperCase())
  if (ia !== -1 && ib !== -1) return ia - ib
  if (ia !== -1) return -1
  if (ib !== -1) return 1
  return a.localeCompare(b, 'zh-Hant')
}
const sizeOptions = computed(() => [...new Set((allList.value?.items ?? []).flatMap(p => p.sizes))].sort(sortSizes))
const tagOptions = computed(() => [...new Set((allList.value?.items ?? []).flatMap(p => p.tags ?? []).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'zh-Hant')))
const colourOptions = computed(() => [...new Set((allList.value?.items ?? []).flatMap(p => p.colours))])
const collectionOptions = computed(() => (info.value?.collections ?? []).filter(c => c.productCount > 0))

const items = computed(() => list.value?.items ?? [])
const totalCount = computed(() => list.value?.totalCount ?? 0)
const totalPages = computed(() => list.value?.totalPages ?? 1)
const loadFailed = computed(() => list.value === null && status.value !== 'pending')

function pageHref(n: number): string {
  const params = new URLSearchParams()
  for (const [k, v] of Object.entries(filters.value)) if (v) params.set(k, v)
  if (n > 1) params.set('page', String(n))
  const qs = params.toString()
  return `${route.path}${qs ? `?${qs}` : ''}`
}

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('shop', clubNameEn.value).title : `官方商店 Shop｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? getShopSeoEn('shop', clubNameEn.value).description : `${clubAssets.value.nameZh}官方商店。以 LINE Pay 付款、自動開立電子發票，可宅配、超商取貨或現場自取。`)),
})
</script>

<template>
<LocaleFallbackNotice v-if="infoFallback" partial />
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/culture/')">{{ cultureLabel }}</a></li>
      <li><a :href="lp('/zh/culture/merchandise/')">{{ tx('官方商品', 'Merchandise') }}</a></li>
      <li aria-current="page">{{ tx('官方商店', 'Shop') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">8.3 Official Shop</p>
    <h1>{{ info?.entryTitle || tx('官方商店', 'Shop') }}<span v-if="!isEn" class="en">Shop</span></h1>
    <p class="page-hero__lede">{{ info?.entryIntro || tx('選好尺寸與顏色加入購物車，以 LINE Pay 付款，系統自動開立電子發票。', 'Choose your size and colour, add to your cart and pay with LINE Pay. An e-invoice is issued automatically.') }}</p>
  </div>
</section>

<section v-if="isBw || (info && !paymentAvailable)" class="band band--tight sh-notice-band">
  <div class="container">
    <p v-if="isBw && isEn" class="mc-alert mc-alert--info" role="note">
      You are buying {{ clubNameEn }} merchandise. <strong>Payment is collected by {{ collectingSubject }}</strong>, and the invoice is also issued in the name of {{ collectingSubject }}.
    </p>
    <p v-else-if="isBw" class="mc-alert mc-alert--info" role="note">
      您購買的是{{ clubAssets.nameZh }}的商品，<strong>款項由{{ collectingSubject }}代收</strong>，發票抬頭亦為{{ collectingSubject }}。
    </p>
    <p v-if="info && !paymentAvailable" class="mc-alert mc-alert--info" role="status">
      {{ tx('目前暫未開放線上付款。您可以先瀏覽商品並加入購物車，開放付款後再結帳。', 'Online payment is not available yet. You can browse and add items to your cart now, and check out once payment opens.') }}
    </p>
  </div>
</section>

<!-- SPEC 3.8 §8.3 — 商品列表：分類、價格、尺寸、顏色篩選與排序 -->
<section class="band band--tight sh-filter-band" aria-labelledby="filters-title">
  <div class="container">
    <h2 id="filters-title" class="visually-hidden">{{ tx('商品篩選', 'Filter products') }}</h2>
    <form class="filter-row" method="get" :action="route.path">
      <div class="filter-field">
        <label for="f-collection">{{ tx('系列', 'Collection') }}</label>
        <select id="f-collection" name="collection">
          <option value="" :selected="!filters.collection">{{ tx('全部商品', 'All products') }}</option>
          <option v-for="c in collectionOptions" :key="c.slug" :value="c.slug" :selected="filters.collection === c.slug">{{ c.name || c.slug }}</option>
        </select>
      </div>
      <div v-if="tagOptions.length || filters.tag" class="filter-field">
        <label for="f-tag">{{ tx('標籤', 'Tag') }}</label>
        <select id="f-tag" name="tag">
          <option value="" :selected="!filters.tag">{{ tx('不限', 'Any') }}</option>
          <option v-if="filters.tag && !tagOptions.includes(filters.tag)" :value="filters.tag" selected>{{ filters.tag }}</option>
          <option v-for="t in tagOptions" :key="t" :value="t" :selected="filters.tag === t">{{ t }}</option>
        </select>
      </div>
      <div class="filter-field">
        <label for="f-size">{{ tx('尺寸', 'Size') }}</label>
        <select id="f-size" name="size">
          <option value="" :selected="!filters.size">{{ tx('不限', 'Any') }}</option>
          <option v-for="s in sizeOptions" :key="s" :value="s" :selected="filters.size === s">{{ s }}</option>
        </select>
      </div>
      <div class="filter-field">
        <label for="f-colour">{{ tx('顏色', 'Colour') }}</label>
        <select id="f-colour" name="colour">
          <option value="" :selected="!filters.colour">{{ tx('不限', 'Any') }}</option>
          <option v-for="c in colourOptions" :key="c" :value="c" :selected="filters.colour === c">{{ c }}</option>
        </select>
      </div>
      <div class="filter-field sh-price-field">
        <label for="f-min">{{ tx('價格（NT$）', 'Price (NT$)') }}</label>
        <span class="sh-price-range">
          <input id="f-min" type="number" name="minPrice" inputmode="numeric" min="0" step="1" :placeholder="tx('最低', 'Min')" :value="filters.minPrice" :aria-label="tx('最低價格', 'Minimum price')">
          <span aria-hidden="true">–</span>
          <input id="f-max" type="number" name="maxPrice" inputmode="numeric" min="0" step="1" :placeholder="tx('最高', 'Max')" :value="filters.maxPrice" :aria-label="tx('最高價格', 'Maximum price')">
        </span>
      </div>
      <div class="filter-field">
        <label for="f-sort">{{ tx('排序', 'Sort by') }}</label>
        <select id="f-sort" name="sort">
          <option value="" :selected="!filters.sort">{{ tx('預設排序', 'Default order') }}</option>
          <option value="newest" :selected="filters.sort === 'newest'">{{ tx('最新上架', 'Newest') }}</option>
          <option value="price_asc" :selected="filters.sort === 'price_asc'">{{ tx('價格由低到高', 'Price: low to high') }}</option>
          <option value="price_desc" :selected="filters.sort === 'price_desc'">{{ tx('價格由高到低', 'Price: high to low') }}</option>
        </select>
      </div>
      <div class="filter-field">
        <span class="sh-check">
          <input id="f-new" type="checkbox" name="isNew" value="true" :checked="filters.isNew === 'true'">
          <label for="f-new">{{ tx('只看新上市', 'New arrivals only') }}</label>
        </span>
      </div>
      <div class="filter-field">
        <button class="btn btn--primary btn--sm" type="submit">{{ tx('套用篩選', 'Apply filters') }}</button>
      </div>
      <div v-if="hasFilters" class="filter-field">
        <a class="sh-clear" :href="route.path">{{ tx('清除篩選', 'Clear filters') }}</a>
      </div>
    </form>
    <p v-if="list" class="result-count" role="status">{{ isEn ? `${totalCount} ${totalCount === 1 ? 'product' : 'products'}` : `共 ${totalCount} 件商品` }}</p>
  </div>
</section>

<section class="band band--tight sh-products-band" aria-labelledby="products-title">
  <div class="container">
    <h2 id="products-title" class="visually-hidden">{{ tx('商品列表', 'Products') }}</h2>

    <p v-if="loadFailed" class="mc-alert mc-alert--error" role="alert">{{ tx('商品資料暫時無法載入，請稍後再試。', 'Products could not be loaded right now. Please try again later.') }}</p>

    <div v-else-if="items.length" class="product-grid">
      <article v-for="p in items" :key="p.slug" class="product-card">
        <a class="product-card__media" :href="lp(`/zh/shop/${p.slug}/`)" :aria-label="p.name || p.slug">
          <span v-if="p.stockStatus === 'sold_out'" class="product-card__flag product-card__flag--muted">{{ tx('缺貨', 'Sold out') }}</span>
          <span v-else-if="p.onSale" class="product-card__flag">{{ tx('優惠', 'Sale') }}</span>
          <span v-else-if="p.isNewArrival" class="product-card__flag">{{ tx('新上市', 'New') }}</span>
          <img v-if="p.imageThumbUrl || p.imageUrl" :src="(p.imageThumbUrl || p.imageUrl) ?? ''" :alt="p.name || ''" loading="lazy" width="640" height="640">
        </a>
        <div class="product-card__body">
          <p class="product-card__name"><a :href="lp(`/zh/shop/${p.slug}/`)">{{ p.name || p.slug }}</a></p>
          <p v-if="p.sizes.length || p.colours.length" class="product-card__opts">
            <template v-if="p.sizes.length">{{ tx('尺寸', 'Sizes') }} {{ p.sizes.join(isEn ? ' / ' : '／') }}</template>
            <template v-if="p.sizes.length && p.colours.length">{{ isEn ? ' · ' : '・' }}</template>
            <template v-if="p.colours.length">{{ isEn ? `${p.colours.length} ${p.colours.length === 1 ? 'colour' : 'colours'} available` : `${p.colours.length} 色可選` }}</template>
          </p>
          <p v-if="p.tags?.length" class="product-card__opts product-card__tags">
            <a v-for="t in p.tags" :key="t" class="sh-tag" :href="`${route.path}?tag=${encodeURIComponent(t)}`" :aria-label="tx(`篩選標籤：${t}`, `Filter by tag: ${t}`)">#{{ t }}</a>
          </p>
          <div class="product-card__foot">
            <template v-if="p.priceMin !== null">
              <span :class="['price', { 'price--sale': p.onSale }]">{{ formatPriceRange(p.priceMin, p.priceMax) }}</span>
              <span v-if="p.onSale && p.listPriceMin" class="price__was">{{ formatPrice(p.listPriceMin) }}</span>
            </template>
            <span v-else class="price__was">{{ tx('暫無販售', 'Not available') }}</span>
          </div>
          <a class="btn btn--primary btn--sm btn--block" :href="lp(`/zh/shop/${p.slug}/`)" style="margin-top:1rem">{{ p.stockStatus === 'sold_out' ? tx('查看商品', 'View product') : tx('選購', 'Shop now') }}</a>
        </div>
      </article>
    </div>

    <div v-else class="editorial-note">
      <template v-if="hasFilters">
        <p>{{ tx('沒有符合條件的商品，請調整篩選條件。', 'No products match your filters. Please adjust them and try again.') }}</p>
        <p style="margin-top:.75rem"><a class="sh-clear" :href="route.path">{{ tx('清除篩選', 'Clear filters') }}</a></p>
      </template>
      <p v-else>{{ tx('商品準備中，敬請期待。', 'Products are coming soon.') }}</p>
    </div>

    <nav v-if="totalPages > 1" class="sh-pager" :aria-label="tx('商品分頁', 'Product pages')">
      <a v-if="page > 1" class="btn btn--light btn--sm" :href="pageHref(page - 1)" rel="prev">{{ tx('上一頁', 'Previous') }}</a>
      <span class="sh-pager__now" aria-current="page">{{ isEn ? `Page ${page} of ${totalPages}` : `第 ${page} ／ ${totalPages} 頁` }}</span>
      <a v-if="page < totalPages" class="btn btn--light btn--sm" :href="pageHref(page + 1)" rel="next">{{ tx('下一頁', 'Next') }}</a>
    </nav>
  </div>
</section>

<section class="band grain shop-info-band" aria-labelledby="shop-info-title">
  <div class="band-inner container">
    <p class="kicker kicker--on-dark">SHOP INFO</p>
    <h2 id="shop-info-title" class="section-title" style="color:#fff">{{ tx('購物須知', 'Shopping information') }}</h2>
    <div class="grid grid--3" style="margin-top:2rem">
      <div class="clip-card clip-card--on-dark">
        <h3>{{ tx('付款方式', 'Payment') }}</h3>
        <p v-if="isEn">All orders are paid with <strong>LINE Pay</strong>, and payment is collected by {{ collectingSubject }}. This website does not handle or store credit card details.</p>
        <p v-else>結帳一律以 <strong>LINE Pay</strong> 付款，收款方為{{ collectingSubject }}。本站不經手也不儲存信用卡資料。</p>
      </div>
      <div class="clip-card clip-card--on-dark">
        <h3>{{ tx('電子發票', 'E-invoice') }}</h3>
        <p>{{ tx('結帳時開立電子發票，可選擇手機條碼載具、自然人憑證載具、統一編號或捐贈碼；退貨時同步作廢或折讓。', 'An e-invoice is issued at checkout. You can choose a mobile barcode carrier, a Citizen Digital Certificate carrier, a Unified Business Number or a donation code. If you return an item, the invoice is voided or a discount note is issued.') }}</p>
      </div>
      <div class="clip-card clip-card--on-dark">
        <h3>{{ tx('配送方式', 'Delivery') }}</h3>
        <p v-if="info && info.deliveryMethods.length">
          {{ info.deliveryMethods.map(d => d.label).join(isEn ? ', ' : '、') }}{{ isEn ? '.' : '。' }}
          <template v-if="isEn">
            <template v-if="info.shippingFee > 0">Shipping is {{ formatPrice(info.shippingFee) }}<template v-if="info.freeShippingThreshold">, free on orders of {{ formatPrice(info.freeShippingThreshold) }} or more</template>; on-site pickup has no shipping fee.</template>
            <template v-else>Shipping is free.</template>
          </template>
          <template v-else>
            <template v-if="info.shippingFee > 0">運費 {{ formatPrice(info.shippingFee) }}<template v-if="info.freeShippingThreshold">，單筆滿 {{ formatPrice(info.freeShippingThreshold) }} 免運</template>；現場自取免運費。</template>
            <template v-else>運費全面免收。</template>
          </template>
        </p>
        <p v-else>{{ tx('宅配到府、超商取貨（僅取貨，不在門市付款）、主場賽事日或俱樂部現場自取。', 'Home delivery, convenience-store pickup (pickup only, no payment at the store), or pickup on home match days or at the club.') }}</p>
      </div>
    </div>
    <p style="margin-top:2rem;display:flex;gap:1rem;flex-wrap:wrap">
      <a class="btn btn--light" :href="lp('/zh/order/lookup/')">{{ tx('查詢訂單', 'Order lookup') }}</a>
      <a class="btn btn--light" :href="lp('/zh/shop/policy/')">{{ tx('購物須知與退換貨政策', 'Shopping information and returns policy') }}</a>
    </p>
  </div>
</section>
</template>

<style>
/* B-18：商品標籤（只用 design tokens，不動 tcrfc.css） */
.product-card__tags{ display:flex; flex-wrap:wrap; gap:.25rem .6rem; }
.sh-tag{ font-size:.78rem; font-weight:700; color:var(--brand-aa); text-decoration:none; }
.sh-tag:hover, .sh-tag:focus-visible{ text-decoration:underline; }
/* 提示／篩選／商品列表三段連續：各自的 .band--tight 上下內距疊加後，篩選列上下空白過大（桌機約 50px／120px）。
   篩選列與上下文屬同一個操作區，收成上 1.5rem、下接商品列表 2rem。 */
.sh-notice-band{ padding-bottom:0; }
.sh-filter-band{ padding-block:1.5rem 0; }
.sh-filter-band .result-count{ margin-top:1rem; margin-bottom:0; }
.sh-products-band{ padding-top:2rem; }
/* 購物須知（深底）：.grain 只給深色底、不設文字色，卡片也沒有底色與內距——
   文字沿用頁面預設深色字會疊在深底上看不見。比照 .cta-band／.cta-card 的深底寫法，只用 design tokens。 */
.shop-info-band{ color:#fff; }
.shop-info-band .clip-card{ background:var(--ink-2); border:1px solid rgba(255,255,255,.08); padding:2rem 1.75rem 2.25rem; }
.shop-info-band .clip-card h3{ color:#fff; font-size:1.15rem; font-weight:700; margin:0 0 .75rem; }
.shop-info-band .clip-card p{ color:var(--muted-dark); line-height:1.7; margin:0; }
.shop-info-band .clip-card strong{ color:#fff; }
</style>
