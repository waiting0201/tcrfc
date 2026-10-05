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
const { locale, isEn, tx } = useLocale()
const o = computed(() => props.order)
</script>

<template>
  <div class="sh-order">
    <ul class="order-meta">
      <li><span>{{ tx('訂單編號', 'Order number') }}</span><span class="mc-mono">{{ o.orderNo }}</span></li>
      <li><span>{{ tx('訂單狀態', 'Order status') }}</span><span>{{ o.status }}</span></li>
      <li><span>{{ tx('付款方式', 'Payment method') }}</span><span>{{ o.paymentMethodLabel }}</span></li>
      <li><span>{{ tx('付款狀態', 'Payment status') }}</span><span>{{ o.paymentStatusLabel }}</span></li>
      <li v-if="o.paymentStatus === 'pending' && o.expiresAt"><span>{{ tx('付款期限', 'Pay by') }}</span><span>{{ formatTaipeiDateTime(o.expiresAt, locale) }}</span></li>
      <li><span>{{ tx('下單時間', 'Ordered') }}</span><span>{{ formatTaipeiDateTime(o.createdAt, locale) }}</span></li>
      <li v-if="o.paidAt"><span>{{ tx('付款時間', 'Paid') }}</span><span>{{ formatTaipeiDateTime(o.paidAt, locale) }}</span></li>
      <li><span>{{ tx('配送方式', 'Delivery method') }}</span><span>{{ o.deliveryMethodLabel }}</span></li>
      <li v-if="o.invoice"><span>{{ tx('電子發票', 'E-invoice') }}</span><span>{{ o.invoice.typeLabel }}{{ isEn ? ' · ' : '・' }}{{ o.invoice.statusLabel }}<template v-if="o.invoice.invoiceNo">{{ isEn ? ` (${o.invoice.invoiceNo})` : `（${o.invoice.invoiceNo}）` }}</template></span></li>
      <li v-if="o.invoice?.taxId"><span>{{ tx('統一編號', 'Unified Business Number') }}</span><span>{{ o.invoice.taxId }}</span></li>
      <li v-if="o.invoice?.donationCode"><span>{{ tx('捐贈碼', 'Donation code') }}</span><span>{{ o.invoice.donationCode }}</span></li>
    </ul>

    <table class="cart-table sh-order__items">
      <caption class="visually-hidden">{{ tx('訂購商品', 'Items ordered') }}</caption>
      <thead>
        <tr><th scope="col">{{ tx('商品', 'Product') }}</th><th scope="col">{{ tx('數量', 'Quantity') }}</th><th scope="col" class="num">{{ tx('小計', 'Subtotal') }}</th></tr>
      </thead>
      <tbody>
        <tr v-for="(it, i) in o.items" :key="`${it.sku}-${i}`">
          <td>
            <p class="cart-item__name">{{ it.productName }}</p>
            <p class="cart-item__variant">{{ it.variantLabel }}<template v-if="it.variantLabel">{{ isEn ? ' · ' : ' ・ ' }}</template>{{ formatPrice(it.unitPrice) }}</p>
          </td>
          <td>×{{ it.quantity }}</td>
          <td class="num">{{ formatPrice(it.lineTotal) }}</td>
        </tr>
      </tbody>
    </table>

    <ul class="order-meta">
      <li><span>{{ tx('商品小計', 'Subtotal') }}</span><span>{{ formatPrice(o.subtotal) }}</span></li>
      <li><span>{{ tx('運費', 'Shipping') }}</span><span>{{ o.shippingFee > 0 ? formatPrice(o.shippingFee) : tx('免運', 'Free') }}</span></li>
      <li><span><strong>{{ tx('應付金額', 'Total due') }}</strong></span><span>{{ formatPrice(o.total) }}</span></li>
    </ul>

    <template v-if="o.recipientName || o.recipientAddress || o.buyerEmail || o.recipientPhone">
      <h3 class="mc-h3" style="margin-top:2rem">{{ tx('收件資料', 'Delivery details') }}</h3>
      <ul class="order-meta">
        <li v-if="o.recipientName"><span>{{ tx('收件人', 'Recipient') }}</span><span>{{ o.recipientName }}</span></li>
        <li v-if="o.recipientPhone"><span>{{ tx('聯絡電話', 'Phone') }}</span><span>{{ o.recipientPhone }}</span></li>
        <li v-if="o.buyerEmail"><span>Email</span><span>{{ o.buyerEmail }}</span></li>
        <li v-if="o.recipientAddress"><span>{{ tx('地址／門市', 'Address / store') }}</span><span>{{ o.recipientAddress }}</span></li>
        <li v-if="o.customerNote"><span>{{ tx('備註', 'Notes') }}</span><span>{{ o.customerNote }}</span></li>
      </ul>
      <p v-if="o.isMasked" class="mc-note mc-note--small">{{ tx('為保護個資，以訂單編號與 Email 查詢時，收件資料以遮罩顯示；點選訂單成立信中的查詢連結可檢視完整內容。', 'To protect your personal data, delivery details are masked when you look up an order with the order number and email. Use the lookup link in your order confirmation email to see the full details.') }}</p>
    </template>

    <template v-if="o.shipment && (o.shipment.trackingNo || o.shipment.pickupStatusLabel || o.shipment.shippedAt)">
      <h3 class="mc-h3" style="margin-top:2rem">{{ tx('出貨資訊', 'Shipping') }}</h3>
      <ul class="order-meta">
        <li v-if="o.shipment.carrier"><span>{{ tx('物流', 'Carrier') }}</span><span>{{ o.shipment.carrier }}</span></li>
        <li v-if="o.shipment.trackingNo"><span>{{ tx('物流單號', 'Tracking number') }}</span><span class="mc-mono">{{ o.shipment.trackingNo }}</span></li>
        <li v-if="o.shipment.shippedAt"><span>{{ tx('出貨時間', 'Shipped') }}</span><span>{{ formatTaipeiDateTime(o.shipment.shippedAt, locale) }}</span></li>
        <li v-if="o.shipment.pickupStatusLabel"><span>{{ tx('取貨狀態', 'Pickup status') }}</span><span>{{ o.shipment.pickupStatusLabel }}</span></li>
        <li v-if="o.shipment.pickupDeadlineOn"><span>{{ tx('取貨期限', 'Pickup deadline') }}</span><span>{{ formatPlainDate(o.shipment.pickupDeadlineOn) }}</span></li>
        <li v-if="o.shipment.deliveredAt"><span>{{ tx('送達時間', 'Delivered') }}</span><span>{{ formatTaipeiDateTime(o.shipment.deliveredAt, locale) }}</span></li>
      </ul>
    </template>
  </div>
</template>
