<script setup lang="ts">
// app/pages/zh/order/lookup/index.vue — 8.3 訂單查詢（S3-5，接真實商店 API）
//
// 兩種查法（主站規劃書 §3.8 8.3「訂單查詢」）：
//   1. 訂單編號 ＋ 下單時填寫的 Email（任何人）：只拿到**遮罩**的收件資料；
//   2. 訂單成立信的連結 `?token=…`（30 天有效）：完整內容，並可繼續付款／取消。權杖查詢成功後 BFF 把它記進 HttpOnly Cookie
//      （JS 讀不到），本頁立刻把網址上的 token 拿掉（`history.replaceState`），避免留在瀏覽紀錄與分享連結裡。
// 🔴 找不到、Email 不符、權杖過期後端一律回同一個 404——前台也只顯示同一句話，不洩漏訂單是否存在。
// 🔴 防枚舉限流（每 IP 5 分鐘 30 次）觸發的 429 顯示後端說明。
// 會員走會員中心「我的訂單」；已登入時本頁頂端提示並連過去。
import { toMemberApiError } from '#shared/utils/member'
import type { ShopOrder } from '#shared/utils/shop'

definePageMeta({ nav: 'culture', unit: '8.3', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => getShopClubNameEn(config.public.club === 'bw' ? 'bw' : 'tcrfc'))

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('lookup', clubNameEn.value).title : `訂單查詢 Order Lookup｜官方商店｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? getShopSeoEn('lookup', clubNameEn.value).description : `以訂單編號與 Email 查詢${clubAssets.value.shortNameZh}官方商店的訂單狀態、物流單號與電子發票；會員可直接於會員中心的「我的訂單」查看。`)),
  robots: 'noindex, nofollow',
})

const { lookupOrder, payOrder, cancelOrder, session } = useShop()
const { info, paymentAvailable } = useShopInfo({ server: false })

const orderNo = ref('')
const email = ref('')
const errors = ref<{ orderNo?: string, email?: string }>({})
const submitting = ref(false)
const problem = ref('')
const order = ref<ShopOrder | null>(null)
const notice = ref('')
const acting = ref(false)

const NOT_FOUND = computed(() => tx('找不到符合的訂單，請確認訂單編號與下單時填寫的 Email。', 'We could not find a matching order. Please check your order number and the email you used when ordering.'))

async function run(body: { orderNo: string, email: string } | { token: string }) {
  submitting.value = true
  problem.value = ''
  notice.value = ''
  order.value = null
  try {
    order.value = await lookupOrder(body)
  }
  catch (err) {
    const e = toMemberApiError(err, NOT_FOUND.value, isEn.value)
    problem.value = e.status === 404 || e.status === 400 ? (e.status === 404 ? NOT_FOUND.value : e.detail) : e.detail
  }
  finally {
    submitting.value = false
  }
}

async function onSubmit() {
  errors.value = {}
  const no = orderNo.value.trim()
  const mail = email.value.trim()
  if (!no) errors.value.orderNo = tx('請填寫訂單編號', 'Please enter your order number')
  if (!mail || !EMAIL_SHAPE.test(mail)) errors.value.email = tx('請填寫有效的 Email', 'Please enter a valid email address')
  if (Object.keys(errors.value).length) return
  await run({ orderNo: no, email: mail })
}

onMounted(async () => {
  void session.restore()
  const token = typeof route.query.token === 'string' ? route.query.token.trim() : ''
  if (token && token.length <= 128) {
    await run({ token })
    // 不論成功與否，都不讓權杖繼續留在網址列
    history.replaceState(null, '', route.path)
  }
})

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

/** 只有拿到完整權限（訂單信連結或會員本人）的畫面才操作付款／取消；遮罩查詢只能看。 */
const canAct = computed(() => !!order.value && !order.value.isMasked && order.value.paymentStatus === 'pending')
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/shop/')">{{ tx('官方商店', 'Shop') }}</a></li>
      <li aria-current="page">{{ tx('訂單查詢', 'Order lookup') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Order Lookup</p>
    <h1>{{ tx('訂單查詢', 'Order lookup') }}<span v-if="!isEn" class="en">Order Lookup</span></h1>
    <p class="page-hero__lede">{{ tx('沒有註冊會員也能查詢：輸入訂單編號與下單時填寫的 Email 即可。', 'You do not need to be a member: just enter your order number and the email you used when ordering.') }}</p>
  </div>
</section>

<!-- SPEC 3.8 §8.3 — 非會員以訂單編號 + Email 查詢；會員走會員中心「我的訂單」 -->
<section class="band form-band" aria-labelledby="lookup-title">
  <div class="container">
    <div class="form-layout form-layout--narrow">
      <p v-if="session.isLoggedIn.value" class="mc-alert mc-alert--info" role="note"><template v-if="isEn">You are signed in. You can go straight to <a :href="lp('/zh/member/#orders')">My Orders in the Member Centre</a> to see all your orders.</template><template v-else>您已登入，可以直接到<a :href="lp('/zh/member/#orders')">會員中心的「我的訂單」</a>查看全部訂單。</template></p>

      <form class="tcrfc-form" novalidate @submit.prevent="onSubmit">
        <h2 id="lookup-title" class="section-title">{{ tx('查詢我的訂單', 'Look up my order') }}</h2>
        <div class="form-grid" style="margin-top:1.5rem">
          <div class="form-field form-field--full">
            <label for="ol-no">{{ tx('訂單編號', 'Order number') }}<span class="req" aria-hidden="true">*</span></label>
            <input id="ol-no" v-model="orderNo" type="text" autocomplete="off" autocapitalize="characters" maxlength="32" placeholder="TR-20261001-ABC123" :aria-invalid="!!errors.orderNo" aria-describedby="ol-no-error">
            <p v-if="errors.orderNo" id="ol-no-error" class="field-error is-shown" role="alert">{{ errors.orderNo }}</p>
          </div>
          <div class="form-field form-field--full">
            <label for="ol-email">{{ tx('下單時填寫的 Email', 'Email used when ordering') }}<span class="req" aria-hidden="true">*</span></label>
            <input id="ol-email" v-model="email" type="email" autocomplete="email" maxlength="255" :aria-invalid="!!errors.email" aria-describedby="ol-email-error">
            <p v-if="errors.email" id="ol-email-error" class="field-error is-shown" role="alert">{{ errors.email }}</p>
          </div>
        </div>
        <div class="form-submit-note">
          <button class="btn btn--primary" type="submit" :disabled="submitting">{{ submitting ? tx('查詢中…', 'Searching…') : tx('查詢訂單', 'Look up order') }}</button>
          <p class="field-hint" style="margin-top:.85rem">{{ tx('查得的內容包含品項、金額、付款狀態、出貨狀態與物流單號、電子發票號碼，以及退換貨申請方式。', 'Results include the items, amounts, payment status, shipping status and tracking number, e-invoice number, and how to request a return or exchange.') }}</p>
        </div>
      </form>

      <p v-if="problem" class="mc-alert mc-alert--error" role="alert">{{ problem }}</p>
      <p v-if="notice" class="mc-alert mc-alert--info" role="status">{{ notice }}</p>

      <div v-if="order" style="margin-top:2rem" aria-live="polite">
        <h2 class="mc-h3">{{ tx('查詢結果', 'Result') }}</h2>
        <ShopOrderView :order="order" />
        <p v-if="canAct" class="mc-actions">
          <button v-if="order.canPay" class="btn btn--linepay" type="button" :disabled="acting || (info !== null && !paymentAvailable)" @click="retryPay">{{ acting ? tx('處理中…', 'Processing…') : tx('前往 LINE Pay 付款', 'Go to LINE Pay to pay') }}</button>
          <button v-if="order.canCancel" class="btn btn--light" type="button" :disabled="acting" @click="cancelPending">{{ tx('取消訂單', 'Cancel order') }}</button>
        </p>
        <p v-if="canAct && info && !paymentAvailable" class="mc-note mc-note--small">{{ tx('目前暫未開放線上付款，請稍後再試。', 'Online payment is not available yet. Please try again later.') }}</p>
        <p v-else-if="order.isMasked && order.paymentStatus === 'pending'" class="mc-note mc-note--small">{{ tx('此訂單尚未付款。若要繼續付款，請使用訂單成立信中的連結進入本頁。', 'This order has not been paid. To continue paying, please open this page from the link in your order confirmation email.') }}</p>
      </div>

      <div class="editorial-note" style="margin-top:2.5rem">
        <h3>{{ tx('已經是會員？', 'Already a member?') }}</h3>
        <p v-if="isEn">Sign in and open My Orders in the <a :href="lp('/zh/member/')">Member Centre</a> to see all your orders without entering each number.</p>
        <p v-else>登入後於<a :href="lp('/zh/member/')">會員中心</a>的「我的訂單」可直接看到全部訂單，不需要逐筆輸入編號。</p>
        <h3 style="margin-top:1.5rem">{{ tx('要退貨或換貨？', 'Need to return or exchange an item?') }}</h3>
        <p v-if="isEn">Under Taiwan's Consumer Protection Act you have a seven-day cooling-off period. Please submit a request through the <a :href="lp('/zh/join/general/')">general contact form</a>. We will check it manually, then process the refund and void the invoice or issue a discount note. See <a :href="lp('/zh/shop/policy/#returns')">Shopping information and returns policy</a>.</p>
        <p v-else>依消費者保護法享七日猶豫期。請以<a :href="lp('/zh/join/general/')">一般聯絡表單</a>提出申請，我們會人工確認後處理退款與發票作廢或折讓。詳見<a :href="lp('/zh/shop/policy/#returns')">購物須知與退換貨政策</a>。</p>
      </div>
    </div>
  </div>
</section>
</template>
