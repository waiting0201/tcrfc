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

definePageMeta({ nav: 'culture', unit: '8.3' })

const COLLECTING_SUBJECT_FALLBACK = '台中磐石足球俱樂部'

const { lp, locale } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const clubAssets = computed(() => getClubAssets(clubKey.value))
const identity = computed(() => getClubIdentity(clubKey.value))
const isBw = computed(() => clubKey.value === 'bw')

const { info, paymentAvailable } = useShopInfo()
const collectingSubject = computed(() => info.value?.collectingSubjectName || COLLECTING_SUBJECT_FALLBACK)

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
  title: computed(() => `官方商店 Shop｜${clubAssets.value.nameZh}`),
  description: computed(() => `${clubAssets.value.nameZh}官方商店。以 LINE Pay 付款、自動開立電子發票，可宅配、超商取貨或現場自取。`),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li><a :href="lp('/zh/culture/merchandise/')">官方商品</a></li>
      <li aria-current="page">官方商店</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">8.3 Official Shop</p>
    <h1>{{ info?.entryTitle || '官方商店' }}<span class="en">Shop</span></h1>
    <p class="page-hero__lede">{{ info?.entryIntro || '選好尺寸與顏色加入購物車，以 LINE Pay 付款，系統自動開立電子發票。' }}</p>
  </div>
</section>

<section v-if="isBw || (info && !paymentAvailable)" class="band band--tight">
  <div class="container">
    <p v-if="isBw" class="mc-alert mc-alert--info" role="note">
      您購買的是{{ clubAssets.nameZh }}的商品，<strong>款項由{{ collectingSubject }}代收</strong>，發票抬頭亦為{{ collectingSubject }}。
    </p>
    <p v-if="info && !paymentAvailable" class="mc-alert mc-alert--info" role="status">
      目前暫未開放線上付款。您可以先瀏覽商品並加入購物車，開放付款後再結帳。
    </p>
  </div>
</section>

<!-- SPEC 3.8 §8.3 — 商品列表：分類、價格、尺寸、顏色篩選與排序 -->
<section class="band band--tight" aria-labelledby="filters-title">
  <div class="container">
    <h2 id="filters-title" class="visually-hidden">商品篩選</h2>
    <form class="filter-row" method="get" :action="route.path">
      <div class="filter-field">
        <label for="f-collection">系列</label>
        <select id="f-collection" name="collection">
          <option value="" :selected="!filters.collection">全部商品</option>
          <option v-for="c in collectionOptions" :key="c.slug" :value="c.slug" :selected="filters.collection === c.slug">{{ c.name || c.slug }}</option>
        </select>
      </div>
      <div class="filter-field">
        <label for="f-size">尺寸</label>
        <select id="f-size" name="size">
          <option value="" :selected="!filters.size">不限</option>
          <option v-for="s in sizeOptions" :key="s" :value="s" :selected="filters.size === s">{{ s }}</option>
        </select>
      </div>
      <div class="filter-field">
        <label for="f-colour">顏色</label>
        <select id="f-colour" name="colour">
          <option value="" :selected="!filters.colour">不限</option>
          <option v-for="c in colourOptions" :key="c" :value="c" :selected="filters.colour === c">{{ c }}</option>
        </select>
      </div>
      <div class="filter-field sh-price-field">
        <label for="f-min">價格（NT$）</label>
        <span class="sh-price-range">
          <input id="f-min" type="number" name="minPrice" inputmode="numeric" min="0" step="1" placeholder="最低" :value="filters.minPrice" aria-label="最低價格">
          <span aria-hidden="true">–</span>
          <input id="f-max" type="number" name="maxPrice" inputmode="numeric" min="0" step="1" placeholder="最高" :value="filters.maxPrice" aria-label="最高價格">
        </span>
      </div>
      <div class="filter-field">
        <label for="f-sort">排序</label>
        <select id="f-sort" name="sort">
          <option value="" :selected="!filters.sort">預設排序</option>
          <option value="newest" :selected="filters.sort === 'newest'">最新上架</option>
          <option value="price_asc" :selected="filters.sort === 'price_asc'">價格由低到高</option>
          <option value="price_desc" :selected="filters.sort === 'price_desc'">價格由高到低</option>
        </select>
      </div>
      <div class="filter-field">
        <span class="sh-check">
          <input id="f-new" type="checkbox" name="isNew" value="true" :checked="filters.isNew === 'true'">
          <label for="f-new">只看新上市</label>
        </span>
      </div>
      <div class="filter-field">
        <button class="btn btn--primary btn--sm" type="submit">套用篩選</button>
      </div>
      <div v-if="hasFilters" class="filter-field">
        <a class="sh-clear" :href="route.path">清除篩選</a>
      </div>
    </form>
    <p v-if="list" class="result-count" role="status">共 {{ totalCount }} 件商品</p>
  </div>
</section>

<section class="band band--tight" aria-labelledby="products-title">
  <div class="container">
    <h2 id="products-title" class="visually-hidden">商品列表</h2>

    <p v-if="loadFailed" class="mc-alert mc-alert--error" role="alert">商品資料暫時無法載入，請稍後再試。</p>

    <div v-else-if="items.length" class="product-grid">
      <article v-for="p in items" :key="p.slug" class="product-card">
        <a class="product-card__media" :href="lp(`/zh/shop/${p.slug}/`)" :aria-label="p.name || p.slug">
          <span v-if="p.stockStatus === 'sold_out'" class="product-card__flag product-card__flag--muted">缺貨</span>
          <span v-else-if="p.onSale" class="product-card__flag">優惠</span>
          <span v-else-if="p.isNewArrival" class="product-card__flag">新上市</span>
          <img v-if="p.imageThumbUrl || p.imageUrl" :src="(p.imageThumbUrl || p.imageUrl) ?? ''" :alt="p.name || ''" loading="lazy" width="640" height="640">
        </a>
        <div class="product-card__body">
          <p class="product-card__name"><a :href="lp(`/zh/shop/${p.slug}/`)">{{ p.name || p.slug }}</a></p>
          <p v-if="p.sizes.length || p.colours.length" class="product-card__opts">
            <template v-if="p.sizes.length">尺寸 {{ p.sizes.join('／') }}</template>
            <template v-if="p.sizes.length && p.colours.length">・</template>
            <template v-if="p.colours.length">{{ p.colours.length }} 色可選</template>
          </p>
          <div class="product-card__foot">
            <template v-if="p.priceMin !== null">
              <span :class="['price', { 'price--sale': p.onSale }]">{{ formatPriceRange(p.priceMin, p.priceMax) }}</span>
              <span v-if="p.onSale && p.listPriceMin" class="price__was">{{ formatPrice(p.listPriceMin) }}</span>
            </template>
            <span v-else class="price__was">暫無販售</span>
          </div>
          <a class="btn btn--primary btn--sm btn--block" :href="lp(`/zh/shop/${p.slug}/`)" style="margin-top:1rem">{{ p.stockStatus === 'sold_out' ? '查看商品' : '選購' }}</a>
        </div>
      </article>
    </div>

    <div v-else class="editorial-note">
      <template v-if="hasFilters">
        <p>沒有符合條件的商品，請調整篩選條件。</p>
        <p style="margin-top:.75rem"><a class="sh-clear" :href="route.path">清除篩選</a></p>
      </template>
      <p v-else>商品準備中，敬請期待。</p>
    </div>

    <nav v-if="totalPages > 1" class="sh-pager" aria-label="商品分頁">
      <a v-if="page > 1" class="btn btn--light btn--sm" :href="pageHref(page - 1)" rel="prev">上一頁</a>
      <span class="sh-pager__now" aria-current="page">第 {{ page }} ／ {{ totalPages }} 頁</span>
      <a v-if="page < totalPages" class="btn btn--light btn--sm" :href="pageHref(page + 1)" rel="next">下一頁</a>
    </nav>
  </div>
</section>

<section class="band grain" aria-labelledby="shop-info-title">
  <div class="band-inner container">
    <p class="kicker kicker--on-dark">SHOP INFO</p>
    <h2 id="shop-info-title" class="section-title" style="color:#fff">購物須知</h2>
    <div class="grid grid--3" style="margin-top:2rem">
      <div class="clip-card clip-card--on-dark">
        <h3>付款方式</h3>
        <p>結帳一律以 <strong>LINE Pay</strong> 付款，收款方為{{ collectingSubject }}。本站不經手也不儲存信用卡資料。</p>
      </div>
      <div class="clip-card clip-card--on-dark">
        <h3>電子發票</h3>
        <p>結帳時開立電子發票，可選擇手機條碼載具、自然人憑證載具、統一編號或捐贈碼；退貨時同步作廢或折讓。</p>
      </div>
      <div class="clip-card clip-card--on-dark">
        <h3>配送方式</h3>
        <p v-if="info && info.deliveryMethods.length">
          {{ info.deliveryMethods.map(d => d.label).join('、') }}。
          <template v-if="info.shippingFee > 0">運費 {{ formatPrice(info.shippingFee) }}<template v-if="info.freeShippingThreshold">，單筆滿 {{ formatPrice(info.freeShippingThreshold) }} 免運</template>；現場自取免運費。</template>
          <template v-else>運費全面免收。</template>
        </p>
        <p v-else>宅配到府、超商取貨（僅取貨，不在門市付款）、主場賽事日或俱樂部現場自取。</p>
      </div>
    </div>
    <p style="margin-top:2rem;display:flex;gap:1rem;flex-wrap:wrap">
      <a class="btn btn--light" :href="lp('/zh/order/lookup/')">查詢訂單</a>
      <a class="btn btn--light" :href="lp('/zh/shop/policy/')">購物須知與退換貨政策</a>
    </p>
  </div>
</section>
</template>
