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

definePageMeta({ nav: 'culture', unit: '8.3' })

const { lp } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))

useSeoMeta({
  title: computed(() => `訂單結果｜官方商店｜${clubAssets.value.nameZh}`),
  description: computed(() => `${clubAssets.value.shortNameZh}官方商店訂單結果：顯示訂單編號、付款結果與電子發票開立狀態，並提供訂單查詢入口。`),
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
        const e = toMemberApiError(err)
        if (e.status !== 409) throw err
        order.value = await getOrder(orderNo.value)
      }
      notice.value = order.value && order.value.paymentStatus !== 'paid' ? '您已取消這次付款，訂單未成立付款，商品庫存已釋回。' : ''
    }
    else if (transactionId.value) {
      try { order.value = await confirmOrder(orderNo.value, transactionId.value) }
      catch (err) {
        const e = toMemberApiError(err)
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
    const e = toMemberApiError(err, '訂單暫時無法載入，請稍後再試。')
    problem.value = e.status === 404
      ? '找不到這張訂單。若您是在別的瀏覽器或裝置下的單，請使用訂單成立信中的連結，或以訂單編號與 Email 查詢。'
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
    problem.value = '無法取得付款連結，請稍後再試。'
  }
  catch (err) {
    problem.value = toMemberApiError(err, '無法前往付款，請稍後再試。').detail
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
    notice.value = '訂單已取消，商品庫存已釋回。'
  }
  catch (err) {
    problem.value = toMemberApiError(err, '取消失敗，請稍後再試。').detail
  }
  finally {
    acting.value = false
  }
}
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/shop/')">官方商店</a></li>
      <li aria-current="page">訂單結果</li>
    </ol>
  </div>
</nav>

<section class="band" aria-labelledby="done-title">
  <div class="container">
    <ol class="steps">
      <li data-done>購物車</li>
      <li data-done>填寫資料</li>
      <li :data-done="paid ? '' : undefined" :aria-current="!paid && order ? 'step' : undefined">LINE Pay 付款</li>
      <li :aria-current="paid ? 'step' : undefined">完成</li>
    </ol>

    <div class="order-done">
      <template v-if="loading">
        <h1 id="done-title">訂單結果</h1>
        <p class="mc-empty" role="status">正在確認訂單狀態，請稍候…</p>
      </template>

      <template v-else-if="!orderNo">
        <h1 id="done-title">找不到訂單</h1>
        <p style="margin-top:.85rem">這個網址沒有帶訂單編號。請使用訂單成立信中的連結，或到訂單查詢輸入訂單編號與 Email。</p>
      </template>

      <template v-else>
        <template v-if="order && paid">
          <div class="order-done__check" aria-hidden="true">✓</div>
          <h1 id="done-title">付款完成，訂單已成立</h1>
          <p style="margin-top:.85rem">我們已將訂單確認信寄到您填寫的 Email。電子發票開立後會另行通知，也可以在下方查看發票狀態。</p>
        </template>
        <template v-else-if="order && pendingPay">
          <h1 id="done-title">尚未完成付款</h1>
          <p style="margin-top:.85rem">這張訂單目前是「待付款」狀態。<template v-if="order.expiresAt">請在付款期限內完成付款，逾時系統會自動取消並釋回庫存。</template></p>
        </template>
        <template v-else-if="order && (failed || expired)">
          <h1 id="done-title">{{ expired ? '付款已逾時' : '付款未成功' }}</h1>
          <p style="margin-top:.85rem">{{ expired ? '這張訂單已超過付款期限，系統已自動取消並釋回庫存。' : '付款沒有成功，這張訂單尚未成立付款。' }}請重新選購並結帳。</p>
        </template>
        <template v-else-if="order">
          <h1 id="done-title">訂單 {{ order.status }}</h1>
        </template>
        <template v-else>
          <h1 id="done-title">無法確認訂單狀態</h1>
        </template>

        <p v-if="notice" class="mc-alert mc-alert--info" role="status">{{ notice }}</p>
        <p v-if="problem" class="mc-alert mc-alert--error" role="alert">{{ problem }}</p>

        <div v-if="order" style="margin-top:1.5rem;text-align:left">
          <ShopOrderView :order="order" />
        </div>

        <p v-if="order && pendingPay && order.canPay" class="mc-actions" style="justify-content:center">
          <button class="btn btn--linepay" type="button" :disabled="acting || (info !== null && !paymentAvailable)" @click="retryPay">{{ acting ? '處理中…' : '前往 LINE Pay 付款' }}</button>
          <button v-if="order.canCancel" class="btn btn--light" type="button" :disabled="acting" @click="cancelPending">取消訂單</button>
        </p>
        <p v-if="order && pendingPay && info && !paymentAvailable" class="mc-note mc-note--small">目前暫未開放線上付款，請稍後再試。</p>
      </template>

      <p style="margin-top:2rem;display:flex;gap:1rem;flex-wrap:wrap;justify-content:center">
        <a class="btn btn--primary" :href="lp('/zh/order/lookup/')">查詢訂單</a>
        <a class="btn btn--light" :href="lp('/zh/shop/')">繼續選購</a>
      </p>
      <p class="field-hint" style="margin-top:1.5rem">會員可於<a :href="lp('/zh/member/')">會員中心</a>的「我的訂單」查看；非會員請以訂單編號與 Email 查詢，或使用訂單信中的連結。</p>
    </div>
  </div>
</section>
</template>
