<script setup lang="ts">
// app/components/shop/ShopOrderView.vue — 訂單內容顯示（完成頁、訂單查詢、會員中心「我的訂單」共用）
//
// 只顯示後端回傳的欄位與中文標籤，不自行推算狀態、金額或付款結果。
// 🔴 `isMasked`（訪客以「訂單編號＋Email」查到）時收件資料是後端遮罩過的值，並提示「點訂單信中的連結可看完整資料」。
// 載具號碼後端永不回傳；發票只顯示類型、狀態、發票號碼、統編（後端給的）、捐贈碼。
// 所有欄位純文字渲染（`{{ }}`），不 v-html。
import { formatPlainDate, formatTaipeiDateTime } from '#shared/utils/member'
import type { ShopOrder } from '#shared/utils/shop'

const props = defineProps<{ order: ShopOrder }>()
const { locale } = useLocale()
const o = computed(() => props.order)
</script>

<template>
  <div class="sh-order">
    <ul class="order-meta">
      <li><span>訂單編號</span><span class="mc-mono">{{ o.orderNo }}</span></li>
      <li><span>訂單狀態</span><span>{{ o.status }}</span></li>
      <li><span>付款方式</span><span>{{ o.paymentMethodLabel }}</span></li>
      <li><span>付款狀態</span><span>{{ o.paymentStatusLabel }}</span></li>
      <li v-if="o.paymentStatus === 'pending' && o.expiresAt"><span>付款期限</span><span>{{ formatTaipeiDateTime(o.expiresAt, locale) }}</span></li>
      <li><span>下單時間</span><span>{{ formatTaipeiDateTime(o.createdAt, locale) }}</span></li>
      <li v-if="o.paidAt"><span>付款時間</span><span>{{ formatTaipeiDateTime(o.paidAt, locale) }}</span></li>
      <li><span>配送方式</span><span>{{ o.deliveryMethodLabel }}</span></li>
      <li v-if="o.invoice"><span>電子發票</span><span>{{ o.invoice.typeLabel }}・{{ o.invoice.statusLabel }}<template v-if="o.invoice.invoiceNo">（{{ o.invoice.invoiceNo }}）</template></span></li>
      <li v-if="o.invoice?.taxId"><span>統一編號</span><span>{{ o.invoice.taxId }}</span></li>
      <li v-if="o.invoice?.donationCode"><span>捐贈碼</span><span>{{ o.invoice.donationCode }}</span></li>
    </ul>

    <table class="cart-table sh-order__items">
      <caption class="visually-hidden">訂購商品</caption>
      <thead>
        <tr><th scope="col">商品</th><th scope="col">數量</th><th scope="col" class="num">小計</th></tr>
      </thead>
      <tbody>
        <tr v-for="(it, i) in o.items" :key="`${it.sku}-${i}`">
          <td>
            <p class="cart-item__name">{{ it.productName }}</p>
            <p class="cart-item__variant">{{ it.variantLabel }}<template v-if="it.variantLabel"> ・ </template>{{ formatPrice(it.unitPrice) }}</p>
          </td>
          <td>×{{ it.quantity }}</td>
          <td class="num">{{ formatPrice(it.lineTotal) }}</td>
        </tr>
      </tbody>
    </table>

    <ul class="order-meta">
      <li><span>商品小計</span><span>{{ formatPrice(o.subtotal) }}</span></li>
      <li><span>運費</span><span>{{ o.shippingFee > 0 ? formatPrice(o.shippingFee) : '免運' }}</span></li>
      <li><span><strong>應付金額</strong></span><span>{{ formatPrice(o.total) }}</span></li>
    </ul>

    <template v-if="o.recipientName || o.recipientAddress || o.buyerEmail || o.recipientPhone">
      <h3 class="mc-h3" style="margin-top:2rem">收件資料</h3>
      <ul class="order-meta">
        <li v-if="o.recipientName"><span>收件人</span><span>{{ o.recipientName }}</span></li>
        <li v-if="o.recipientPhone"><span>聯絡電話</span><span>{{ o.recipientPhone }}</span></li>
        <li v-if="o.buyerEmail"><span>Email</span><span>{{ o.buyerEmail }}</span></li>
        <li v-if="o.recipientAddress"><span>地址／門市</span><span>{{ o.recipientAddress }}</span></li>
        <li v-if="o.customerNote"><span>備註</span><span>{{ o.customerNote }}</span></li>
      </ul>
      <p v-if="o.isMasked" class="mc-note mc-note--small">為保護個資，以訂單編號與 Email 查詢時，收件資料以遮罩顯示；點選訂單成立信中的查詢連結可檢視完整內容。</p>
    </template>

    <template v-if="o.shipment && (o.shipment.trackingNo || o.shipment.pickupStatusLabel || o.shipment.shippedAt)">
      <h3 class="mc-h3" style="margin-top:2rem">出貨資訊</h3>
      <ul class="order-meta">
        <li v-if="o.shipment.carrier"><span>物流</span><span>{{ o.shipment.carrier }}</span></li>
        <li v-if="o.shipment.trackingNo"><span>物流單號</span><span class="mc-mono">{{ o.shipment.trackingNo }}</span></li>
        <li v-if="o.shipment.shippedAt"><span>出貨時間</span><span>{{ formatTaipeiDateTime(o.shipment.shippedAt, locale) }}</span></li>
        <li v-if="o.shipment.pickupStatusLabel"><span>取貨狀態</span><span>{{ o.shipment.pickupStatusLabel }}</span></li>
        <li v-if="o.shipment.pickupDeadlineOn"><span>取貨期限</span><span>{{ formatPlainDate(o.shipment.pickupDeadlineOn) }}</span></li>
        <li v-if="o.shipment.deliveredAt"><span>送達時間</span><span>{{ formatTaipeiDateTime(o.shipment.deliveredAt, locale) }}</span></li>
      </ul>
    </template>
  </div>
</template>
