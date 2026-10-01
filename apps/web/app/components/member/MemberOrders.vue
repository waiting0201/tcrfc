<script setup lang="ts">
// app/components/member/MemberOrders.vue — 會員中心「我的訂單」（主站 §3.8 8.3「訂單查詢」：會員於會員中心查詢）
//
// 資料：`GET /api/shop/my-orders`（本人在這個俱樂部的訂單，新→舊最多 50 筆）；展開單筆用 `GET /api/shop/orders/{no}`（會員 Bearer，完整收件資料）。
// 🔴 只顯示本站台（本俱樂部）的訂單：BFF 固定以容器的俱樂部呼叫後端，磐石站看不到藍鯨站的訂單，反之亦然。
// 退換貨：規格明文「表單或客服信箱，不做專屬的線上退貨精靈」——這裡只連到政策頁與聯絡表單，沒有「申請退貨」按鈕。
// 🔴 「待付款」的訂單可在這裡繼續付款或取消；付款結果一律以後端狀態為準。
import { formatTaipeiDateTime, toMemberApiError } from '#shared/utils/member'
import type { ShopOrder, ShopOrderListItem } from '#shared/utils/shop'

const { locale, lp } = useLocale()
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
    error.value = toMemberApiError(err, '訂單暫時無法載入，請稍後再試。').detail
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
    detailError.value = toMemberApiError(err, '訂單內容暫時無法載入。').detail
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
    detailError.value = '無法取得付款連結，請稍後再試。'
  }
  catch (err) {
    detailError.value = toMemberApiError(err, '無法前往付款，請稍後再試。').detail
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
    detailError.value = toMemberApiError(err, '取消失敗，請稍後再試。').detail
  }
  finally {
    acting.value = false
  }
}
</script>

<template>
  <div class="mc-section">
    <h3 class="mc-h3">我的訂單</h3>
    <p class="mc-note">這裡是您在本站官方商店的訂單。退換貨請見<a :href="lp('/zh/shop/policy/#returns')">退換貨政策</a>，並以<a :href="lp('/zh/join/general/')">一般聯絡表單</a>提出申請。</p>

    <p v-if="error" class="mc-alert mc-alert--error" role="alert">{{ error }} <button type="button" class="mc-link" @click="load">重新載入</button></p>
    <p v-else-if="orders === null" class="mc-empty" role="status">載入中…</p>
    <p v-else-if="orders.length === 0" class="mc-empty">您還沒有訂單。<a :href="lp('/zh/shop/')">前往官方商店</a></p>

    <ul v-else class="sh-orderlist">
      <li v-for="o in orders" :key="o.orderNo" class="sh-orderrow">
        <div class="sh-orderrow__head">
          <span><span class="mc-mono">{{ o.orderNo }}</span>　<span class="mc-badge">{{ o.status }}</span></span>
          <button type="button" class="mc-link" :aria-expanded="openNo === o.orderNo" :aria-controls="`order-${o.orderNo}`" @click="toggle(o.orderNo)">{{ openNo === o.orderNo ? '收合' : '查看明細' }}</button>
        </div>
        <p class="sh-orderrow__meta">
          {{ formatTaipeiDateTime(o.createdAt, locale) }}・{{ o.firstItemName }}<template v-if="o.itemCount > 1"> 等 {{ o.itemCount }} 件</template>・{{ formatPrice(o.total) }}・{{ o.paymentStatusLabel }}
          <template v-if="o.invoiceNo">・發票 {{ o.invoiceNo }}</template><template v-if="o.trackingNo">・物流單號 {{ o.trackingNo }}</template>
        </p>
        <div v-if="openNo === o.orderNo" :id="`order-${o.orderNo}`" style="margin-top:1rem">
          <p v-if="detailLoading" class="mc-empty" role="status">載入中…</p>
          <p v-if="detailError" class="mc-alert mc-alert--error" role="alert">{{ detailError }}</p>
          <template v-if="detail">
            <ShopOrderView :order="detail" />
            <p v-if="detail.paymentStatus === 'pending' && (detail.canPay || detail.canCancel)" class="mc-actions">
              <button v-if="detail.canPay" type="button" class="btn btn--linepay" :disabled="acting || (info !== null && !paymentAvailable)" @click="pay">{{ acting ? '處理中…' : '前往 LINE Pay 付款' }}</button>
              <button v-if="detail.canCancel" type="button" class="btn btn--light" :disabled="acting" @click="cancel">取消訂單</button>
            </p>
            <p v-if="detail.paymentStatus === 'pending' && info && !paymentAvailable" class="mc-note mc-note--small">目前暫未開放線上付款。</p>
          </template>
        </div>
      </li>
    </ul>
  </div>
</template>
