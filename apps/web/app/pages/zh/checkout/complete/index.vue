<script setup lang="ts">
// app/pages/zh/checkout/complete/index.vue — 8.3 付款導回頁／訂單完成頁（S3-5，接真實商店 API）
//
// 這一頁同時是 LINE Pay 的兩個導回目的地（網址約定見 shared/utils/shop.ts `checkoutReturnUrl`、README「S3-5」節、API 缺口一）：
//   `?orderNo=…&transactionId=…`  付款完成導回 → 呼叫 `confirm`（伺服器向金流方確認，**不信用戶端**）
//   `?orderNo=…&cancel=1`         使用者在 LINE Pay 按取消 → 呼叫 `cancel` 釋回庫存
//   `?orderNo=…`（其餘）            單純查看訂單狀態（例如付款失敗後回來、或結帳頁建立訂單後請款失敗）
// 🔴 「付款成功」只依後端回傳的 `paymentStatus === 'paid'` 顯示，絕不由網址參數推斷（網址參數誰都能偽造）。
// 🔴 重新整理本頁會再次呼叫 confirm：後端保證重複／並行確認冪等（庫存只扣一次、發票只開一張、信只寄一封）。
// 🔴 訪客以 HttpOnly Cookie 裡的訂單權杖操作（由 BFF 帶標頭）；Cookie 不在（換了瀏覽器）→ 找不到訂單，引導到訂單查詢。
// 訂單內容只在瀏覽器端載入（SSR 是殼）；`noindex`；頁面與 API 皆 `no-store`。
import { toMemberApiError } from '#shared/utils/member'
import type { ShopOrder } from '#shared/utils/shop'

definePageMeta({ nav: 'culture', unit: '8.3', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => getShopClubNameEn(config.public.club === 'bw' ? 'bw' : 'tcrfc'))

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('complete', clubNameEn.value).title : `訂單結果｜官方商店｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? getShopSeoEn('complete', clubNameEn.value).description : `${clubAssets.value.shortNameZh}官方商店訂單結果：顯示訂單編號、付款結果與電子發票開立狀態，並提供訂單查詢入口。`)),
  robots: 'noindex, nofollow',
})

const { getOrder, confirmOrder, cancelOrder, payOrder } = useShop()
const { info, paymentAvailable } = useShopInfo({ server: false })

const orderNo = computed(() => {
  const v = route.query.orderNo
  return typeof v === 'string' && /^[A-Za-z0-9_-]{4,64}$/.test(v) ? v : ''
})
const transactionId = computed(() => {
  const v = route.query.transactionId
  return typeof v === 'string' && /^[A-Za-z0-9_.:-]{1,64}$/.test(v) ? v : ''
})
const cancelled = computed(() => route.query.cancel === '1' || route.query.cancel === 'true')

const loading = ref(true)
const order = ref<ShopOrder | null>(null)
const problem = ref('')
const notice = ref('')
const acting = ref(false)

const paid = computed(() => order.value?.paymentStatus === 'paid')
const failed = computed(() => order.value?.paymentStatus === 'failed')
const expired = computed(() => order.value?.paymentStatus === 'expired')
const pendingPay = computed(() => order.value?.paymentStatus === 'pending')

async function load() {
  loading.value = true
  problem.value = ''
  notice.value = ''
  if (!orderNo.value) {
    loading.value = false
    return
  }
  try {
    if (cancelled.value) {
      // 使用者在 LINE Pay 取消：釋回庫存。已取消／已付款時後端回 409，不是錯誤，照樣顯示目前狀態。
      try { order.value = await cancelOrder(orderNo.value) }
      catch (err) {
        const e = toMemberApiError(err, undefined, isEn.value)
        if (e.status !== 409) throw err
        order.value = await getOrder(orderNo.value)
      }
      notice.value = order.value && order.value.paymentStatus !== 'paid' ? tx('您已取消這次付款，訂單未成立付款，商品庫存已釋回。', 'You cancelled this payment. The order has not been paid and the stock has been released.') : ''
    }
    else if (transactionId.value) {
      try { order.value = await confirmOrder(orderNo.value, transactionId.value) }
      catch (err) {
        const e = toMemberApiError(err, undefined, isEn.value)
        // 確認失敗：顯示後端說明，並盡量取得訂單目前狀態
        problem.value = e.detail
        order.value = await getOrder(orderNo.value).catch(() => null)
      }
    }
    else {
      order.value = await getOrder(orderNo.value)
    }
  }
  catch (err) {
    const e = toMemberApiError(err, tx('訂單暫時無法載入，請稍後再試。', 'Your order could not be loaded right now. Please try again later.'), isEn.value)
    problem.value = e.status === 404
      ? tx('找不到這張訂單。若您是在別的瀏覽器或裝置下的單，請使用訂單成立信中的連結，或以訂單編號與 Email 查詢。', 'We could not find this order. If you placed it in another browser or on another device, please use the link in your order confirmation email, or look it up with your order number and email.')
      : e.detail
  }
  finally {
    loading.value = false
  }
}
onMounted(load)

async function retryPay() {
  if (!order.value || acting.value) return
  acting.value = true
  problem.value = ''
  try {
    const next = await payOrder(order.value.orderNo)
    if (next.paymentUrl && /^https:\/\//i.test(next.paymentUrl)) {
      window.location.assign(next.paymentUrl)
      return
    }
    problem.value = tx('無法取得付款連結，請稍後再試。', 'We could not get the payment link. Please try again later.')
  }
  catch (err) {
    problem.value = toMemberApiError(err, tx('無法前往付款，請稍後再試。', 'We could not take you to payment. Please try again later.'), isEn.value).detail
  }
  finally {
    acting.value = false
  }
}

async function cancelPending() {
  if (!order.value || acting.value) return
  acting.value = true
  problem.value = ''
  try {
    order.value = await cancelOrder(order.value.orderNo)
    notice.value = tx('訂單已取消，商品庫存已釋回。', 'Your order has been cancelled and the stock has been released.')
  }
  catch (err) {
    problem.value = toMemberApiError(err, tx('取消失敗，請稍後再試。', 'We could not cancel the order. Please try again later.'), isEn.value).detail
  }
  finally {
    acting.value = false
  }
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/shop/')">{{ tx('官方商店', 'Shop') }}</a></li>
      <li aria-current="page">{{ tx('訂單結果', 'Order result') }}</li>
    </ol>
  </div>
</nav>

<section class="band" aria-labelledby="done-title">
  <div class="container">
    <ol class="steps">
      <li data-done>{{ tx('購物車', 'Cart') }}</li>
      <li data-done>{{ tx('填寫資料', 'Your details') }}</li>
      <li :data-done="paid ? '' : undefined" :aria-current="!paid && order ? 'step' : undefined">{{ tx('LINE Pay 付款', 'Pay with LINE Pay') }}</li>
      <li :aria-current="paid ? 'step' : undefined">{{ tx('完成', 'Done') }}</li>
    </ol>

    <div class="order-done">
      <template v-if="loading">
        <h1 id="done-title">{{ tx('訂單結果', 'Order result') }}</h1>
        <p class="mc-empty" role="status">{{ tx('正在確認訂單狀態，請稍候…', 'Checking your order status, please wait…') }}</p>
      </template>

      <template v-else-if="!orderNo">
        <h1 id="done-title">{{ tx('找不到訂單', 'Order not found') }}</h1>
        <p style="margin-top:.85rem">{{ tx('這個網址沒有帶訂單編號。請使用訂單成立信中的連結，或到訂單查詢輸入訂單編號與 Email。', 'This address does not include an order number. Please use the link in your order confirmation email, or enter your order number and email in Order lookup.') }}</p>
      </template>

      <template v-else>
        <template v-if="order && paid">
          <div class="order-done__check" aria-hidden="true">✓</div>
          <h1 id="done-title">{{ tx('付款完成，訂單已成立', 'Payment complete. Your order is confirmed') }}</h1>
          <p style="margin-top:.85rem">{{ tx('我們已將訂單確認信寄到您填寫的 Email。電子發票開立後會另行通知，也可以在下方查看發票狀態。', 'We have sent an order confirmation to the email address you entered. You will be notified separately once your e-invoice is issued, and you can also check the invoice status below.') }}</p>
        </template>
        <template v-else-if="order && pendingPay">
          <h1 id="done-title">{{ tx('尚未完成付款', 'Payment not complete') }}</h1>
          <p style="margin-top:.85rem">{{ tx('這張訂單目前是「待付款」狀態。', 'This order is currently awaiting payment.') }}<template v-if="order.expiresAt">{{ tx('請在付款期限內完成付款，逾時系統會自動取消並釋回庫存。', ' Please pay before the deadline, otherwise the order is cancelled automatically and the stock is released.') }}</template></p>
        </template>
        <template v-else-if="order && (failed || expired)">
          <h1 id="done-title">{{ expired ? tx('付款已逾時', 'Payment expired') : tx('付款未成功', 'Payment unsuccessful') }}</h1>
          <p style="margin-top:.85rem">{{ expired ? tx('這張訂單已超過付款期限，系統已自動取消並釋回庫存。', 'This order passed its payment deadline and was cancelled automatically. The stock has been released.') : tx('付款沒有成功，這張訂單尚未成立付款。', 'The payment was not successful, so this order has not been paid.') }}{{ tx('請重新選購並結帳。', ' Please shop again and check out.') }}</p>
        </template>
        <template v-else-if="order">
          <h1 id="done-title">{{ tx('訂單 ', 'Order: ') }}{{ order.status }}</h1>
        </template>
        <template v-else>
          <h1 id="done-title">{{ tx('無法確認訂單狀態', 'We could not confirm your order status') }}</h1>
        </template>

        <p v-if="notice" class="mc-alert mc-alert--info" role="status">{{ notice }}</p>
        <p v-if="problem" class="mc-alert mc-alert--error" role="alert">{{ problem }}</p>

        <div v-if="order" style="margin-top:1.5rem;text-align:left">
          <ShopOrderView :order="order" />
        </div>

        <p v-if="order && pendingPay && order.canPay" class="mc-actions" style="justify-content:center">
          <button class="btn btn--linepay" type="button" :disabled="acting || (info !== null && !paymentAvailable)" @click="retryPay">{{ acting ? tx('處理中…', 'Processing…') : tx('前往 LINE Pay 付款', 'Go to LINE Pay to pay') }}</button>
          <button v-if="order.canCancel" class="btn btn--light" type="button" :disabled="acting" @click="cancelPending">{{ tx('取消訂單', 'Cancel order') }}</button>
        </p>
        <p v-if="order && pendingPay && info && !paymentAvailable" class="mc-note mc-note--small">{{ tx('目前暫未開放線上付款，請稍後再試。', 'Online payment is not available yet. Please try again later.') }}</p>
      </template>

      <p style="margin-top:2rem;display:flex;gap:1rem;flex-wrap:wrap;justify-content:center">
        <a class="btn btn--primary" :href="lp('/zh/order/lookup/')">{{ tx('查詢訂單', 'Order lookup') }}</a>
        <a class="btn btn--light" :href="lp('/zh/shop/')">{{ tx('繼續選購', 'Continue shopping') }}</a>
      </p>
      <p class="field-hint" style="margin-top:1.5rem"><template v-if="isEn">Members can view their orders under My Orders in the <a :href="lp('/zh/member/')">Member Centre</a>. Non-members can look up an order with the order number and email, or use the link in the order email.</template><template v-else>會員可於<a :href="lp('/zh/member/')">會員中心</a>的「我的訂單」查看；非會員請以訂單編號與 Email 查詢，或使用訂單信中的連結。</template></p>
    </div>
  </div>
</section>
</template>
