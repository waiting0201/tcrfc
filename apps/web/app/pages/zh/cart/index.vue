<script setup lang="ts">
// app/pages/zh/cart/index.vue — 8.3 購物車（S3-5，接真實商店 API）
//
// 🔴 購物車內容只在瀏覽器端載入（SSR 輸出永遠是「載入中」殼，不含任何商品或個人資料，頁面 `no-store`＋`noindex`）。
// 身分：已登入 → 會員購物車（載入時順帶把訪客購物車併入）；未登入 → 訪客購物車（權杖只在 HttpOnly Cookie，見 server/utils/shop-session.ts）。
// 🔴 金額一律用後端回傳的值（小計、運費、免運門檻、促銷價），前台不自行加總；購物車不保留庫存，真正保留發生在結帳。
// 🔴 購物車不得跨俱樂部混買：BFF 固定以本容器的俱樂部呼叫後端，換站台就是另一個購物車。
import { toMemberApiError } from '#shared/utils/member'
import type { ShopCart } from '#shared/utils/shop'

definePageMeta({ nav: 'culture', unit: '8.3' })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const isBw = computed(() => config.public.club === 'bw')

useSeoMeta({
  title: computed(() => `購物車 Cart｜官方商店｜${clubAssets.value.nameZh}`),
  description: computed(() => `${clubAssets.value.shortNameZh}官方商店購物車：確認商品、規格與數量後前往結帳。`),
  robots: 'noindex, nofollow',
})

const { cart, loadCart, setQuantity, removeItem } = useShop()
// 收款主體與付款是否可用：瀏覽器端才取（避免藍鯨 SSR payload 帶出磐石字樣，見 check-club-brand-leak）
const { info, paymentAvailable } = useShopInfo({ server: false })

const loaded = ref(false)
const loadError = ref('')
const actionError = ref('')
const busy = ref<string | null>(null)

async function reload() {
  loadError.value = ''
  try {
    await loadCart()
  }
  catch (err) {
    loadError.value = toMemberApiError(err, '購物車暫時無法載入，請稍後再試。').detail
  }
  finally {
    loaded.value = true
  }
}
onMounted(reload)

async function run(variantId: string, fn: () => Promise<ShopCart>) {
  if (busy.value) return
  busy.value = variantId
  actionError.value = ''
  try {
    await fn()
  }
  catch (err) {
    actionError.value = toMemberApiError(err, '更新購物車失敗，請稍後再試一次。').detail
    // 失敗後以後端為準重新取得（例如庫存剛被買走）
    await reload()
  }
  finally {
    busy.value = null
  }
}
const changeQty = (variantId: string, qty: number) => run(variantId, () => setQuantity(variantId, Math.min(Math.max(0, Math.trunc(qty) || 0), 99)))
const remove = (variantId: string) => run(variantId, () => removeItem(variantId))

const items = computed(() => cart.value?.items ?? [])
const shippingFee = computed(() => cart.value?.shipping.fee ?? 0)
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/shop/')">官方商店</a></li>
      <li aria-current="page">購物車</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Cart</p>
    <h1>購物車<span class="en">Your Cart</span></h1>
  </div>
</section>

<!-- SPEC 3.8 §8.3 — 購物車：修改數量、刪除、小計與運費試算 -->
<section class="band" aria-labelledby="cart-title">
  <div class="container">
    <h2 id="cart-title" class="visually-hidden">購物車內容</h2>

    <p v-if="!loaded" class="mc-empty" role="status">購物車載入中…</p>
    <p v-else-if="loadError" class="mc-alert mc-alert--error" role="alert">{{ loadError }} <button type="button" class="mc-link" @click="reload">重新載入</button></p>

    <template v-else-if="items.length === 0">
      <div class="editorial-note">
        <p>您的購物車是空的。</p>
        <p style="margin-top:.75rem"><a class="btn btn--primary btn--sm" :href="lp('/zh/shop/')">前往選購</a></p>
      </div>
      <p class="mc-note" style="margin-top:1.5rem">已有帳號？<a :href="lp('/zh/member/')">登入會員</a>，登入後會自動併入您先前加入的商品。</p>
    </template>

    <div v-else class="shop-layout">
      <div>
        <p v-if="actionError" class="mc-alert mc-alert--error" role="alert">{{ actionError }}</p>
        <table class="cart-table">
          <caption class="visually-hidden">購物車商品清單</caption>
          <thead>
            <tr>
              <th scope="col">商品</th>
              <th scope="col">數量</th>
              <th scope="col" class="num">小計</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="it in items" :key="it.variantId">
              <td>
                <div class="cart-item">
                  <img v-if="it.imageThumbUrl" :src="it.imageThumbUrl" :alt="it.productName ?? ''" loading="lazy" width="200" height="200">
                  <div>
                    <p class="cart-item__name"><a :href="lp(`/zh/shop/${it.productSlug}/`)">{{ it.productName || it.productSlug }}</a></p>
                    <p class="cart-item__variant">
                      {{ it.variantLabel }} ・ {{ formatPrice(it.unitPrice) }}<template v-if="it.onSale">（原價 {{ formatPrice(it.listPrice) }}）</template>
                    </p>
                    <p v-if="it.issueMessage" class="sh-issue" role="alert">{{ it.issueMessage }}</p>
                    <button class="cart-item__remove" type="button" :disabled="busy === it.variantId" @click="remove(it.variantId)">移除</button>
                  </div>
                </div>
              </td>
              <td>
                <div class="qty">
                  <button type="button" aria-label="減少數量" :disabled="busy !== null || it.quantity <= 1" @click="changeQty(it.variantId, it.quantity - 1)">−</button>
                  <input
                    type="number" :value="it.quantity" min="1" max="99" inputmode="numeric" :aria-label="`${it.productName ?? ''} ${it.variantLabel} 數量`"
                    :disabled="busy !== null" @change="changeQty(it.variantId, Number(($event.target as HTMLInputElement).value))"
                  >
                  <button type="button" aria-label="增加數量" :disabled="busy !== null || it.quantity >= 99" @click="changeQty(it.variantId, it.quantity + 1)">＋</button>
                </div>
              </td>
              <td class="num">{{ formatPrice(it.lineTotal) }}</td>
            </tr>
          </tbody>
        </table>

        <p style="margin-top:1.75rem"><a class="btn btn--light btn--sm" :href="lp('/zh/shop/')">← 繼續選購</a></p>
      </div>

      <aside class="shop-layout__aside">
        <div class="summary">
          <h2>金額摘要</h2>
          <p class="summary__line"><span>商品小計</span><strong>{{ formatPrice(cart?.subtotal) }}</strong></p>
          <p class="summary__line"><span>運費（宅配／超商取貨）</span><strong>{{ shippingFee > 0 ? formatPrice(shippingFee) : '免運' }}</strong></p>
          <p v-if="cart?.shipping.amountToFree" class="summary__note" style="margin-top:0">再購買 {{ formatPrice(cart.shipping.amountToFree) }} 即可免運。</p>
          <p class="summary__line summary__total"><span>預估合計</span><span>{{ formatPrice((cart?.subtotal ?? 0) + shippingFee) }}</span></p>
          <p class="summary__note">現場自取免運費；實際運費於結帳時依配送方式確認。</p>
          <p v-if="cart && !cart.canCheckout" class="mc-alert mc-alert--error" role="alert">購物車內有商品目前無法購買，請先移除或調整數量後再結帳。</p>
          <p v-if="info && !paymentAvailable" class="mc-alert mc-alert--info" role="status">目前暫未開放線上付款，開放後即可結帳。</p>
          <a v-if="cart?.canCheckout" class="btn btn--primary btn--block" :href="lp('/zh/checkout/')">前往結帳</a>
          <button v-else class="btn btn--primary btn--block" type="button" disabled>前往結帳</button>
          <p v-if="isBw && info?.collectingSubjectName" class="summary__note">您購買的是{{ clubAssets.nameZh }}的商品，款項由{{ info.collectingSubjectName }}代收。</p>
          <p class="summary__note">結帳時以 LINE Pay 付款並開立電子發票。</p>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>
