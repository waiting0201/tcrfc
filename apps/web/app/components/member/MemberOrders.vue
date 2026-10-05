<script setup lang="ts">
// app/components/member/MemberOrders.vue — 會員中心「我的訂單」（主站 §3.8 8.3「訂單查詢」：會員於會員中心查詢）
//
// 資料：`GET /api/shop/my-orders`（本人在這個俱樂部的訂單，新→舊最多 50 筆）；展開單筆用 `GET /api/shop/orders/{no}`（會員 Bearer，完整收件資料）。
// 🔴 只顯示本站台（本俱樂部）的訂單：BFF 固定以容器的俱樂部呼叫後端，磐石站看不到藍鯨站的訂單，反之亦然。
// 退換貨：規格明文「表單或客服信箱，不做專屬的線上退貨精靈」——這裡只連到政策頁與聯絡表單，沒有「申請退貨」按鈕。
// 🔴 「待付款」的訂單可在這裡繼續付款或取消；付款結果一律以後端狀態為準。
import { formatTaipeiDateTime, toMemberApiError } from '#shared/utils/member'
import type { ShopOrder, ShopOrderListItem } from '#shared/utils/shop'

const { locale, lp, isEn, tx } = useLocale()
const { myOrders, getOrder, payOrder, cancelOrder } = useShop()
const { info, paymentAvailable } = useShopInfo({ server: false })

const orders = ref<ShopOrderListItem[] | null>(null)
const error = ref('')
const openNo = ref<string | null>(null)
const detail = ref<ShopOrder | null>(null)
const detailLoading = ref(false)
const detailError = ref('')
const acting = ref(false)

async function load() {
  error.value = ''
  try {
    orders.value = await myOrders()
  }
  catch (err) {
    error.value = toMemberApiError(err, tx('訂單暫時無法載入，請稍後再試。', 'Your orders could not be loaded right now. Please try again later.'), isEn.value).detail
  }
}
onMounted(load)

async function toggle(orderNo: string) {
  if (openNo.value === orderNo) {
    openNo.value = null
    detail.value = null
    return
  }
  openNo.value = orderNo
  detail.value = null
  detailError.value = ''
  detailLoading.value = true
  try {
    detail.value = await getOrder(orderNo)
  }
  catch (err) {
    detailError.value = toMemberApiError(err, tx('訂單內容暫時無法載入。', 'The order details could not be loaded right now.'), isEn.value).detail
  }
  finally {
    detailLoading.value = false
  }
}

async function pay() {
  if (!detail.value || acting.value) return
  acting.value = true
  detailError.value = ''
  try {
    const next = await payOrder(detail.value.orderNo)
    if (next.paymentUrl && /^https:\/\//i.test(next.paymentUrl)) {
      window.location.assign(next.paymentUrl)
      return
    }
    detailError.value = tx('無法取得付款連結，請稍後再試。', 'We could not get the payment link. Please try again later.')
  }
  catch (err) {
    detailError.value = toMemberApiError(err, tx('無法前往付款，請稍後再試。', 'We could not take you to payment. Please try again later.'), isEn.value).detail
  }
  finally {
    acting.value = false
  }
}

async function cancel() {
  if (!detail.value || acting.value) return
  acting.value = true
  detailError.value = ''
  try {
    detail.value = await cancelOrder(detail.value.orderNo)
    await load()
  }
  catch (err) {
    detailError.value = toMemberApiError(err, tx('取消失敗，請稍後再試。', 'We could not cancel the order. Please try again later.'), isEn.value).detail
  }
  finally {
    acting.value = false
  }
}
</script>

<template>
  <div class="mc-section">
    <h3 class="mc-h3">{{ tx('我的訂單', 'My orders') }}</h3>
    <p v-if="isEn" class="mc-note">These are your orders from this website's shop. For returns and exchanges, see the <a :href="lp('/zh/shop/policy/#returns')">returns policy</a> and submit a request through the <a :href="lp('/zh/join/general/')">general contact form</a>.</p>
    <p v-else class="mc-note">這裡是您在本站官方商店的訂單。退換貨請見<a :href="lp('/zh/shop/policy/#returns')">退換貨政策</a>，並以<a :href="lp('/zh/join/general/')">一般聯絡表單</a>提出申請。</p>

    <p v-if="error" class="mc-alert mc-alert--error" role="alert">{{ error }} <button type="button" class="mc-link" @click="load">{{ tx('重新載入', 'Reload') }}</button></p>
    <p v-else-if="orders === null" class="mc-empty" role="status">{{ tx('載入中…', 'Loading…') }}</p>
    <p v-else-if="orders.length === 0" class="mc-empty">{{ tx('您還沒有訂單。', 'You do not have any orders yet. ') }}<a :href="lp('/zh/shop/')">{{ tx('前往官方商店', 'Go to the shop') }}</a></p>

    <ul v-else class="sh-orderlist">
      <li v-for="o in orders" :key="o.orderNo" class="sh-orderrow">
        <div class="sh-orderrow__head">
          <span><span class="mc-mono">{{ o.orderNo }}</span>{{ isEn ? ' ' : '　' }}<span class="mc-badge">{{ o.status }}</span></span>
          <button type="button" class="mc-link" :aria-expanded="openNo === o.orderNo" :aria-controls="`order-${o.orderNo}`" @click="toggle(o.orderNo)">{{ openNo === o.orderNo ? tx('收合', 'Hide details') : tx('查看明細', 'View details') }}</button>
        </div>
        <p class="sh-orderrow__meta">
          {{ formatTaipeiDateTime(o.createdAt, locale) }}{{ isEn ? ' · ' : '・' }}{{ o.firstItemName }}<template v-if="o.itemCount > 1">{{ isEn ? ` and ${o.itemCount - 1} more` : ` 等 ${o.itemCount} 件` }}</template>{{ isEn ? ' · ' : '・' }}{{ formatPrice(o.total) }}{{ isEn ? ' · ' : '・' }}{{ o.paymentStatusLabel }}
          <template v-if="o.invoiceNo">{{ isEn ? ' · Invoice ' : '・發票 ' }}{{ o.invoiceNo }}</template><template v-if="o.trackingNo">{{ isEn ? ' · Tracking number ' : '・物流單號 ' }}{{ o.trackingNo }}</template>
        </p>
        <div v-if="openNo === o.orderNo" :id="`order-${o.orderNo}`" style="margin-top:1rem">
          <p v-if="detailLoading" class="mc-empty" role="status">{{ tx('載入中…', 'Loading…') }}</p>
          <p v-if="detailError" class="mc-alert mc-alert--error" role="alert">{{ detailError }}</p>
          <template v-if="detail">
            <ShopOrderView :order="detail" />
            <p v-if="detail.paymentStatus === 'pending' && (detail.canPay || detail.canCancel)" class="mc-actions">
              <button v-if="detail.canPay" type="button" class="btn btn--linepay" :disabled="acting || (info !== null && !paymentAvailable)" @click="pay">{{ acting ? tx('處理中…', 'Processing…') : tx('前往 LINE Pay 付款', 'Go to LINE Pay to pay') }}</button>
              <button v-if="detail.canCancel" type="button" class="btn btn--light" :disabled="acting" @click="cancel">{{ tx('取消訂單', 'Cancel order') }}</button>
            </p>
            <p v-if="detail.paymentStatus === 'pending' && info && !paymentAvailable" class="mc-note mc-note--small">{{ tx('目前暫未開放線上付款。', 'Online payment is not available yet.') }}</p>
          </template>
        </div>
      </li>
    </ul>
  </div>
</template>
