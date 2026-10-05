<script setup lang="ts">
// app/pages/zh/shop/[slug]/index.vue — 8.3 商品詳情（S3-5，接真實商店 API；取代原本兩張寫死的示意商品頁）
//
// 資料：`GET /api/shop/products/{slug}`（SSR；草稿、別隊商品由後端回 404 → 這裡真 404，不回 200 空版面）。
// 規格選擇（尺寸／顏色對應 SKU）、可售量、促銷價都以後端為準；「加入購物車」「直接結帳」只在瀏覽器端呼叫 BFF。
// 🔴 不得快取（庫存即時）；內容欄位（商品敘事、尺碼表、政策）一律純文字渲染，**不 v-html**。
// 🔴 藍鯨商品頁明示「您購買的是台中藍鯨的商品，款項由 {收款主體} 代收」（藍鯨規劃書 §5.2）；收款主體名稱只用 API 的
// `collectingSubjectName`，本頁沒有任何固定的磐石字樣（動態路由，不在 collect-routes 的檢查範圍，所以更不能留備援字串）。
// 🔴 Product／Offer 結構化資料（S3-5a）見 `useProductSchema`：無名稱或沒有任何正價格規格就不輸出。
import { toMemberApiError } from '#shared/utils/member'
import type { ShopProductDetail, ShopVariant } from '#shared/utils/shop'

definePageMeta({ nav: 'culture', unit: '8.3', enReady: true, enReadyBw: true })

const route = useRoute()
const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const clubAssets = computed(() => getClubAssets(clubKey.value))
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(clubKey.value)))
const isBw = computed(() => clubKey.value === 'bw')
const clubNameEn = computed(() => getShopClubNameEn(clubKey.value))
const cultureLabel = computed(() => (isEn.value ? getShopCultureLabelEn(clubKey.value) : identity.value.cultureLabelZh))

const slug = computed(() => String(route.params.slug))
const { data: product } = await useFetch<ShopProductDetail | null>(() => `/api/shop/products/${slug.value}`, {
  query: computed(() => ({ lang: locale.value })),
  default: () => null,
})
if (!product.value) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}
const { info, paymentAvailable } = useShopInfo()
const { addToCart } = useShop()

// ── 規格選擇 ──
const variants = computed<ShopVariant[]>(() => product.value?.variants ?? [])
const sizeOptions = computed(() => [...new Set(variants.value.map(v => v.size).filter((s): s is string => !!s))])
const colourOptions = computed(() => [...new Set(variants.value.map(v => v.colour).filter((c): c is string => !!c))])
const hasSize = computed(() => sizeOptions.value.length > 0)
const hasColour = computed(() => colourOptions.value.length > 0)
// 兩個維度都沒有（單一規格商品，或規格只有自訂標籤）：直接列出全部規格讓使用者挑
const listOnly = computed(() => !hasSize.value && !hasColour.value && variants.value.length > 1)

const pickedSize = ref<string | null>(null)
const pickedColour = ref<string | null>(null)
const pickedVariantId = ref<string | null>(null)

// 只有一個規格就預選（SSR 也成立，不閃一下）
if (variants.value.length === 1) {
  const only = variants.value[0]!
  pickedVariantId.value = only.id
  pickedSize.value = only.size
  pickedColour.value = only.colour
}

const selected = computed<ShopVariant | null>(() => {
  if (listOnly.value || variants.value.length === 1) return variants.value.find(v => v.id === pickedVariantId.value) ?? null
  const needSize = hasSize.value
  const needColour = hasColour.value
  if ((needSize && !pickedSize.value) || (needColour && !pickedColour.value)) return null
  return variants.value.find(v => (!needSize || v.size === pickedSize.value) && (!needColour || v.colour === pickedColour.value)) ?? null
})

/** 某個尺寸（在目前已選顏色下）／某個顏色（在目前已選尺寸下）是否還有可購買的規格——不可購買的選項呈現為停用。 */
function optionBuyable(kind: 'size' | 'colour', value: string): boolean {
  return variants.value.some((v) => {
    if (!variantBuyable(v)) return false
    if (kind === 'size') return v.size === value && (!pickedColour.value || v.colour === pickedColour.value)
    return v.colour === value && (!pickedSize.value || v.size === pickedSize.value)
  })
}
function pickSize(s: string) { pickedSize.value = s; quantity.value = 1; result.value = null }
function pickColour(c: string) { pickedColour.value = c; quantity.value = 1; result.value = null }
function pickVariant(v: ShopVariant) { pickedVariantId.value = v.id; quantity.value = 1; result.value = null }

// ── 價格顯示：選定規格 → 該規格價格；未選 → 價格區間 ──
const priceNow = computed(() => (selected.value ? formatPrice(selected.value.price) : formatPriceRange(product.value?.priceMin, product.value?.priceMax)))
const onSaleNow = computed(() => (selected.value ? selected.value.onSale : product.value?.onSale === true))
const listPriceNow = computed(() => {
  if (selected.value) return selected.value.onSale ? formatPrice(selected.value.listPrice) : null
  return product.value?.onSale && product.value.listPriceMin ? formatPrice(product.value.listPriceMin) : null
})
const stockNow = computed(() => {
  const v = selected.value
  if (!v) return product.value?.stockStatusLabel ?? ''
  if (!variantBuyable(v)) return tx('此規格已售完', 'This option is sold out')
  return v.availableQty < 5 ? tx(`此規格僅剩 ${v.availableQty} 件`, `Only ${v.availableQty} left in this option`) : tx('尚有庫存', 'In stock')
})
const soldOutAll = computed(() => product.value?.stockStatus === 'sold_out')

// ── 數量與加入購物車 ──
const quantity = ref(1)
const maxQty = computed(() => Math.max(1, Math.min(selected.value?.availableQty ?? 10, 10)))
function setQty(n: number) {
  quantity.value = Math.min(Math.max(1, Math.trunc(Number.isFinite(n) ? n : 1)), maxQty.value)
}

const busy = ref<'cart' | 'checkout' | null>(null)
const result = ref<{ ok: boolean, message: string } | null>(null)
const canBuy = computed(() => !!selected.value && variantBuyable(selected.value) && busy.value === null)

async function submit(goCheckout: boolean) {
  const v = selected.value
  if (!v) {
    result.value = isEn.value
      ? { ok: false, message: `Please choose your ${[hasSize.value ? 'size' : '', hasColour.value ? 'colour' : ''].filter(Boolean).join(' and ') || 'option'} first.` }
      : { ok: false, message: `請先選擇${[hasSize.value ? '尺寸' : '', hasColour.value ? '顏色' : ''].filter(Boolean).join('與') || '規格'}。` }
    return
  }
  busy.value = goCheckout ? 'checkout' : 'cart'
  result.value = null
  try {
    await addToCart(v.id, quantity.value)
    if (goCheckout) {
      await navigateTo(lp('/zh/checkout/'))
      return
    }
    result.value = { ok: true, message: tx(`已將「${v.label}」×${quantity.value} 加入購物車。`, `Added "${v.label}" × ${quantity.value} to your cart.`) }
  }
  catch (err) {
    result.value = { ok: false, message: toMemberApiError(err, tx('加入購物車失敗，請稍後再試一次。', 'We could not add this item to your cart. Please try again.'), isEn.value).detail }
  }
  finally {
    busy.value = null
  }
}

// ── 圖集 ──
const images = computed(() => product.value?.images ?? [])
const imageIdx = ref(0)
const mainImage = computed(() => images.value[imageIdx.value] ?? null)

// ── 尺碼表（後端原樣給 JSON，容錯解析，認不得就不顯示）──
const sizeChart = computed(() => parseSizeChart(product.value?.sizeChart, isEn.value))

// ── 收款主體（只用 API，不留固定字樣）、運費說明 ──
const collectingSubject = computed(() => info.value?.collectingSubjectName ?? null)
const shippingNote = computed(() => {
  const i = info.value
  if (!i) return null
  if (isEn.value) {
    if (i.shippingFee <= 0) return 'Shipping is free.'
    return `Shipping is ${formatPrice(i.shippingFee)}${i.freeShippingThreshold ? `, free on orders of ${formatPrice(i.freeShippingThreshold)} or more` : ''}; on-site pickup has no shipping fee.`
  }
  if (i.shippingFee <= 0) return '運費全面免收。'
  return `運費 ${formatPrice(i.shippingFee)}${i.freeShippingThreshold ? `，單筆滿 ${formatPrice(i.freeShippingThreshold)} 免運` : ''}；現場自取免運費。`
})

const siteConfig = useSiteConfig()
const pageUrl = computed(() => `${(siteConfig.url ?? '').replace(/\/$/, '')}${route.path}`)
useProductSchema(product, { brandName: computed(() => (isEn.value ? clubNameEn.value : clubAssets.value.shortNameZh)), pageUrl, sellerName: collectingSubject })

useSeoMeta({
  title: computed(() => product.value?.seoTitle
    || (isEn.value ? getShopProductSeoEn(clubNameEn.value, product.value?.name).title : `${product.value?.name ?? '商品'}｜官方商店｜${clubAssets.value.nameZh}`)),
  description: computed(() => product.value?.seoDescription
    || (isEn.value ? getShopProductSeoEn(clubNameEn.value, product.value?.name).description : `${clubAssets.value.nameZh}官方商店「${product.value?.name ?? ''}」。以 LINE Pay 付款並開立電子發票。`)),
  ogImage: computed(() => images.value[0]?.url ?? undefined),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/culture/')">{{ cultureLabel }}</a></li>
      <li><a :href="lp('/zh/shop/')">{{ tx('官方商店', 'Shop') }}</a></li>
      <li aria-current="page">{{ product?.name }}</li>
    </ol>
  </div>
</nav>

<!-- SPEC 3.8 §8.3 — 商品詳情：圖集、規格選擇（尺寸／顏色＝SKU）、尺碼表、原價與促銷價、庫存、運送與退換貨說明、加入購物車 -->
<section class="band band--tight">
  <div class="container">
    <p v-if="isBw && collectingSubject && isEn" class="mc-alert mc-alert--info" role="note">
      You are buying {{ clubNameEn }} merchandise. <strong>Payment is collected by {{ collectingSubject }}</strong>, and the invoice is also issued in the name of {{ collectingSubject }}.
    </p>
    <p v-else-if="isBw && collectingSubject" class="mc-alert mc-alert--info" role="note">
      您購買的是{{ clubAssets.nameZh }}的商品，<strong>款項由{{ collectingSubject }}代收</strong>，發票抬頭亦為{{ collectingSubject }}。
    </p>
    <p v-if="info && !paymentAvailable" class="mc-alert mc-alert--info" role="status">
      {{ tx('目前暫未開放線上付款。您可以先加入購物車，開放付款後再結帳。', 'Online payment is not available yet. You can add items to your cart now, and check out once payment opens.') }}
    </p>

    <div class="product-detail">
      <div class="product-gallery">
        <div class="product-gallery__main">
          <img v-if="mainImage" :src="mainImage.url" :alt="product?.name ?? ''" :width="mainImage.width ?? 1280" :height="mainImage.height ?? 1280">
        </div>
        <ul v-if="images.length > 1" class="product-gallery__thumbs">
          <li v-for="(img, i) in images" :key="img.url">
            <button type="button" class="sh-thumb" :aria-label="tx(`檢視第 ${i + 1} 張圖片`, `View image ${i + 1}`)" :aria-pressed="i === imageIdx" @click="imageIdx = i">
              <img :src="img.thumbUrl" alt="" loading="lazy" width="72" height="72">
            </button>
          </li>
        </ul>
        <p v-if="!images.length" class="mc-note mc-note--small">{{ tx('此商品尚未上傳圖片。', 'No images have been uploaded for this product yet.') }}</p>
      </div>

      <div class="product-info">
        <h1 class="product-info__title">{{ product?.name }}</h1>
        <div class="product-info__price">
          <template v-if="priceNow">
            <span :class="['price', { 'price--sale': onSaleNow }]">{{ priceNow }}</span>
            <span v-if="listPriceNow" class="price__was">{{ listPriceNow }}</span>
          </template>
          <span v-else class="price__was">{{ tx('暫無販售', 'Not available') }}</span>
          <span v-if="product?.isNewArrival" class="badge">{{ tx('新上市', 'New') }}</span>
        </div>
        <p class="product-info__stock" role="status">{{ soldOutAll ? tx('已售完', 'Sold out') : stockNow }}</p>
        <p v-if="product?.collectionName" class="mc-note mc-note--small">{{ tx('系列：', 'Collection: ') }}{{ product.collectionName }}</p>

        <hr>

        <template v-if="listOnly">
          <div class="variant-group">
            <div class="variant-group__label"><span>{{ tx('規格', 'Option') }}<span class="req" aria-hidden="true">*</span></span></div>
            <ul class="variant-opts" role="group" :aria-label="tx('選擇規格', 'Choose an option')">
              <li v-for="v in variants" :key="v.id">
                <button class="variant-opt" type="button" :aria-pressed="pickedVariantId === v.id" :disabled="!variantBuyable(v)" @click="pickVariant(v)">{{ v.label }}</button>
              </li>
            </ul>
          </div>
        </template>
        <template v-else>
          <div v-if="hasColour" class="variant-group">
            <div class="variant-group__label"><span>{{ tx('顏色', 'Colour') }}<span class="req" aria-hidden="true">*</span></span></div>
            <ul class="variant-opts" role="group" :aria-label="tx('選擇顏色', 'Choose a colour')">
              <li v-for="c in colourOptions" :key="c">
                <button class="variant-opt" type="button" :aria-pressed="pickedColour === c" :disabled="!optionBuyable('colour', c)" @click="pickColour(c)">{{ c }}</button>
              </li>
            </ul>
          </div>
          <div v-if="hasSize" class="variant-group">
            <div class="variant-group__label">
              <span>{{ tx('尺寸', 'Size') }}<span class="req" aria-hidden="true">*</span></span>
              <a v-if="sizeChart" href="#size-chart">{{ tx('尺碼表', 'Size chart') }}</a>
            </div>
            <ul class="variant-opts" role="group" :aria-label="tx('選擇尺寸', 'Choose a size')">
              <li v-for="s in sizeOptions" :key="s">
                <button class="variant-opt" type="button" :aria-pressed="pickedSize === s" :disabled="!optionBuyable('size', s)" @click="pickSize(s)">{{ s }}</button>
              </li>
            </ul>
          </div>
        </template>

        <div class="variant-group">
          <div class="variant-group__label"><span>{{ tx('數量', 'Quantity') }}</span></div>
          <div class="qty">
            <button type="button" :aria-label="tx('減少數量', 'Decrease quantity')" :disabled="quantity <= 1" @click="setQty(quantity - 1)">−</button>
            <input type="number" :value="quantity" min="1" :max="maxQty" inputmode="numeric" :aria-label="tx('數量', 'Quantity')" @change="setQty(Number(($event.target as HTMLInputElement).value))">
            <button type="button" :aria-label="tx('增加數量', 'Increase quantity')" :disabled="quantity >= maxQty" @click="setQty(quantity + 1)">＋</button>
          </div>
        </div>

        <div class="buy-row">
          <button class="btn btn--primary" type="button" :disabled="!canBuy" @click="submit(false)">
            {{ busy === 'cart' ? tx('加入中…', 'Adding…') : soldOutAll ? tx('已售完', 'Sold out') : tx('加入購物車', 'Add to cart') }}
          </button>
          <button class="btn btn--dark" type="button" :disabled="!canBuy" @click="submit(true)">
            {{ busy === 'checkout' ? tx('處理中…', 'Processing…') : tx('直接結帳', 'Buy now') }}
          </button>
        </div>

        <p v-if="result" :class="['mc-alert', result.ok ? 'mc-alert--ok' : 'mc-alert--error']" :role="result.ok ? 'status' : 'alert'">
          {{ result.message }}
          <template v-if="result.ok"> <a :href="lp('/zh/cart/')">{{ tx('前往購物車', 'Go to cart') }}</a></template>
        </p>

        <ul class="info-list">
          <template v-if="isEn">
            <li><strong>Payment</strong><span>LINE Pay<template v-if="collectingSubject"> (collected by {{ collectingSubject }})</template>. An e-invoice is issued at checkout.</span></li>
            <li><strong>Delivery</strong><span>Home delivery, convenience-store pickup (pickup only), or pickup on home match days and at the club. <template v-if="shippingNote">{{ shippingNote }}</template></span></li>
            <li><strong>Returns</strong><span>Under Taiwan's Consumer Protection Act you have a seven-day cooling-off period. See <a :href="lp('/zh/shop/policy/#returns')">Shopping information and returns policy</a>.</span></li>
          </template>
          <template v-else>
            <li><strong>付款</strong><span>LINE Pay<template v-if="collectingSubject">（收款方：{{ collectingSubject }}）</template>，結帳時開立電子發票。</span></li>
            <li><strong>配送</strong><span>宅配到府／超商取貨（僅取貨）／主場賽事日與俱樂部現場自取。<template v-if="shippingNote">{{ shippingNote }}</template></span></li>
            <li><strong>退換貨</strong><span>依消費者保護法享七日猶豫期，詳見<a :href="lp('/zh/shop/policy/#returns')">購物須知與退換貨政策</a>。</span></li>
          </template>
        </ul>
      </div>
    </div>
  </div>
</section>

<section v-if="product?.narrative" class="band band--tight" aria-labelledby="product-story-title">
  <div class="container">
    <h2 id="product-story-title" class="section-title">{{ tx('商品介紹', 'About this product') }}</h2>
    <p class="sh-narrative">{{ product.narrative }}</p>
  </div>
</section>

<section v-if="sizeChart" id="size-chart" class="band band--tight" aria-labelledby="size-chart-title">
  <div class="container">
    <h2 id="size-chart-title" class="section-title">{{ tx('尺碼表', 'Size chart') }}</h2>
    <div class="table-scroll" style="margin-top:1.5rem">
      <table class="size-table">
        <caption class="visually-hidden">{{ sizeChart.caption || tx('尺碼表', 'Size chart') }}</caption>
        <thead>
          <tr><th v-for="(h, i) in sizeChart.headers" :key="i" scope="col">{{ h }}</th></tr>
        </thead>
        <tbody>
          <tr v-for="(row, ri) in sizeChart.rows" :key="ri">
            <template v-for="(c, ci) in row" :key="ci">
              <th v-if="ci === 0" scope="row">{{ c }}</th>
              <td v-else>{{ c }}</td>
            </template>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-if="sizeChart.note" class="field-hint" style="margin-top:.75rem">{{ sizeChart.note }}</p>
  </div>
</section>
</template>

<style>
.table-scroll{ overflow-x:auto; }
.size-table{ width:100%; min-width:420px; border-collapse:collapse; font-size:.82rem; }
.size-table th, .size-table td{ padding:.55rem .75rem; border:1px solid var(--rule); text-align:center; }
.size-table thead th{ background:var(--paper-2); font-weight:800; color:var(--heading); }
.size-table th[scope="row"]{ font-weight:800; background:var(--paper-2); }
</style>
