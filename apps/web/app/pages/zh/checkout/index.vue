<script setup lang="ts">
// app/pages/zh/checkout/index.vue — 8.3 結帳（S3-5，接真實商店 API）
//
// 流程：收件資料 → 配送方式 → 發票開立方式 → 確認送出（建立訂單）→ 請款 → 轉往 LINE Pay。支援非會員結帳（填 Email 即可）；
// 會員結帳自動帶入帳號資料（可修改，留空則由伺服器用帳號資料補）。
//
// 🔴 冪等：每一次「結帳嘗試」一把 `Idempotency-Key`，存 sessionStorage 直到成功；連點、重送、網路重試都用同一把鍵，
// 後端保證只成立一張訂單（並行同鍵也只成立一張）。送出按鈕在請求進行中停用。
// 🔴 請求本文沒有任何金額欄位——金額（小計、運費、免運門檻、促銷價）一律後端重算，右側摘要顯示的是後端回傳的值。
// 🔴 `shop/info.paymentAvailable === false`（LINE Pay 尚未串接，STATUS B-10）：明示「暫未開放線上付款」並**停用送出**，
// 不建立訂單（建了也付不了，只會把庫存占住 30 分鐘），更不得假裝付款成功。判斷是保守的：取不到 `info` 一律視為不可付款。
// 🔴 本頁是 `check-club-brand-leak.mjs` 例外頁：藍鯨站必須明示「發票抬頭與收款方是台中磐石足球俱樂部」（藍鯨規劃書 §5.2），
// 備援固定文案只放在本頁與 shop/index.vue。
// 🔴 載具號碼只經 BFF 送後端（後端加密保存、永不回傳）；本頁不把它寫進 localStorage／sessionStorage／網址。
import { toMemberApiError } from '#shared/utils/member'
import type { CheckoutErrors, CheckoutForm, DeliveryCode, InvoiceType, ShopOrder } from '#shared/utils/shop'

definePageMeta({ nav: 'culture', unit: '8.3', enReady: true })

const COLLECTING_SUBJECT_FALLBACK = '台中磐石足球俱樂部'

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const club = computed(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const clubAssets = computed(() => getClubAssets(club.value))
const isBw = computed(() => club.value === 'bw')

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('checkout', CLUB_NAME_EN).title : `結帳 Checkout｜官方商店｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? getShopSeoEn('checkout', CLUB_NAME_EN).description : `${clubAssets.value.shortNameZh}官方商店結帳：填寫收件資料、選擇配送方式與發票開立方式，確認後以 LINE Pay 完成付款。支援非會員結帳。`)),
  robots: 'noindex, nofollow',
})

const { info, paymentAvailable } = useShopInfo()
const { cart, loadCart, checkout, payOrder, session } = useShop()
const subject = computed(() => info.value?.collectingSubjectName || (isEn.value ? CLUB_NAME_EN : COLLECTING_SUBJECT_FALLBACK))
// 英文頁：後台配送方式名稱若沒有英文，後端回繁中備援（沒有旗標），偵測到漢字就提示
const infoFallback = computed(() => isEn.value && shopHasCjk(...(info.value?.deliveryMethods ?? []).map(d => d.label), ...(info.value?.donationCodes ?? []).map(d => d.orgName)))

const form = reactive<CheckoutForm>({
  email: '',
  recipientName: '',
  recipientPhone: '',
  deliveryMethod: 'home_delivery',
  recipientAddress: '',
  pickupStore: '',
  customerNote: '',
  invoiceType: 'mobile_barcode',
  carrierId: '',
  taxId: '',
  donationCode: '',
})
const agree = ref(false)
const errors = ref<CheckoutErrors>({})
const consentError = ref(false)
const formError = ref('')
const submitting = ref(false)
const loaded = ref(false)
const loadError = ref('')
/** 訂單已建立但請款／導向失敗時保留，讓使用者能重試付款而不是重複下單。 */
const createdOrder = ref<ShopOrder | null>(null)

const isMember = computed(() => session.isLoggedIn.value)
const deliveryOptions = computed(() => {
  const fromApi = info.value?.deliveryMethods
  if (fromApi?.length) return fromApi.map(d => ({ code: d.code as DeliveryCode, label: d.label }))
  if (isEn.value) {
    return (['home_delivery', 'cvs_pickup', 'onsite_pickup'] as DeliveryCode[]).map(code => ({ code, label: SHOP_DELIVERY_FALLBACK_EN[code] }))
  }
  return [
    { code: 'home_delivery' as DeliveryCode, label: '宅配到府' },
    { code: 'cvs_pickup' as DeliveryCode, label: '超商取貨（僅取貨，不在門市付款）' },
    { code: 'onsite_pickup' as DeliveryCode, label: '主場賽事日或俱樂部現場自取' },
  ]
})
const INVOICE_OPTIONS_ZH: ReadonlyArray<{ code: InvoiceType, label: string }> = [
  { code: 'mobile_barcode', label: '手機條碼載具' },
  { code: 'citizen_cert', label: '自然人憑證載具' },
  { code: 'tax_id', label: '統一編號（公司報帳）' },
  { code: 'donation', label: '捐贈發票' },
]
const INVOICE_OPTIONS = computed(() => INVOICE_OPTIONS_ZH.map(o => ({ code: o.code, label: isEn.value ? SHOP_INVOICE_LABEL_EN[o.code] : o.label })))

const items = computed(() => cart.value?.items ?? [])
const shippingFee = computed(() => (form.deliveryMethod === 'onsite_pickup' ? 0 : (cart.value?.shipping.fee ?? 0)))
const total = computed(() => (cart.value?.subtotal ?? 0) + shippingFee.value)

const KEY_STORAGE = computed(() => `tcrfc-checkout-key-${club.value}`)
function currentKey(): string {
  try {
    const existing = sessionStorage.getItem(KEY_STORAGE.value)
    if (existing && /^[A-Za-z0-9_\-:.]{8,64}$/.test(existing)) return existing
    const fresh = newIdempotencyKey()
    sessionStorage.setItem(KEY_STORAGE.value, fresh)
    return fresh
  }
  catch {
    // sessionStorage 不可用（私密視窗等）：退回記憶體裡的一把，同一頁內重送仍是同一把
    memoryKey ||= newIdempotencyKey()
    return memoryKey
  }
}
let memoryKey = ''
function rotateKey() {
  memoryKey = ''
  try { sessionStorage.removeItem(KEY_STORAGE.value) }
  catch { /* 忽略 */ }
}

onMounted(async () => {
  try {
    await loadCart()
    if (session.isLoggedIn.value) {
      // 會員自動帶入帳號資料（可修改；留空送出則由伺服器補）
      try {
        const me = await session.authedFetch<{ name?: string, email?: string, phone?: string | null }>('/api/backend/member/me', { query: { lang: locale.value } })
        form.recipientName ||= me.name ?? ''
        form.email ||= me.email ?? ''
        form.recipientPhone ||= me.phone ?? ''
      }
      catch { /* 帶入失敗不影響結帳：留空由伺服器補 */ }
    }
  }
  catch (err) {
    loadError.value = toMemberApiError(err, tx('購物車暫時無法載入，請稍後再試。', 'Your cart could not be loaded right now. Please try again later.'), isEn.value).detail
  }
  finally {
    loaded.value = true
  }
})

async function startPayment(order: ShopOrder) {
  const paid = await payOrder(order.orderNo)
  if (paid.paymentUrl && /^https:\/\//i.test(paid.paymentUrl)) {
    window.location.assign(paid.paymentUrl)
    return
  }
  throw Object.assign(new Error('no_payment_url'), { status: 502, data: { detail: '無法取得付款連結，請稍後再試。', messageEn: 'We could not get the payment link. Please try again later.' } })
}

async function onSubmit() {
  if (submitting.value) return
  formError.value = ''
  consentError.value = false
  errors.value = validateCheckout(form, isMember.value, isEn.value)
  if (!agree.value) consentError.value = true
  if (Object.keys(errors.value).length > 0 || consentError.value) {
    await nextTick()
    document.querySelector<HTMLElement>('.field-error.is-shown')?.closest('.form-field, .consent-block')?.querySelector<HTMLElement>('input,select,textarea')?.focus()
    return
  }
  if (!paymentAvailable.value) {
    formError.value = tx('目前暫未開放線上付款，暫時無法送出訂單。', 'Online payment is not available yet, so orders cannot be submitted for now.')
    return
  }
  if (!createdOrder.value && (!cart.value?.canCheckout || items.value.length === 0)) {
    formError.value = tx('購物車內有商品目前無法購買，請先回購物車調整。', 'Some items in your cart cannot be purchased right now. Please go back to your cart and adjust them.')
    return
  }

  submitting.value = true
  try {
    let order = createdOrder.value
    if (!order) {
      order = await checkout(buildCheckoutBody(form, locale.value), currentKey())
      createdOrder.value = order
      rotateKey()
    }
    await startPayment(order)
  }
  catch (err) {
    const e = toMemberApiError(err, tx('送出訂單失敗，請稍後再試一次。', 'We could not submit your order. Please try again.'), isEn.value)
    formError.value = e.detail
    if (e.code === 'idempotency_key_reused') rotateKey()
    if (['insufficient_stock', 'item_unavailable', 'cart_empty', 'quantity_limit'].includes(e.code)) {
      // 以後端為準更新購物車（庫存剛被買走、商品下架）
      await loadCart().catch(() => {})
    }
  }
  finally {
    submitting.value = false
  }
}
</script>

<template>
<LocaleFallbackNotice v-if="infoFallback" partial />
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/shop/')">{{ tx('官方商店', 'Shop') }}</a></li>
      <li><a :href="lp('/zh/cart/')">{{ tx('購物車', 'Cart') }}</a></li>
      <li aria-current="page">{{ tx('結帳', 'Checkout') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Checkout</p>
    <h1>{{ tx('結帳', 'Checkout') }}<span v-if="!isEn" class="en">Checkout</span></h1>
  </div>
</section>

<!-- SPEC 3.8 §8.3 — 結帳：收件資料 → 配送方式 → 發票開立方式 → 確認 → 轉 LINE Pay。支援非會員結帳 -->
<section class="band form-band" aria-labelledby="checkout-title">
  <div class="container">
    <h2 id="checkout-title" class="visually-hidden">{{ tx('結帳流程', 'Checkout') }}</h2>

    <ol class="steps">
      <li data-done>{{ tx('購物車', 'Cart') }}</li>
      <li aria-current="step">{{ tx('填寫資料', 'Your details') }}</li>
      <li>{{ tx('LINE Pay 付款', 'Pay with LINE Pay') }}</li>
      <li>{{ tx('完成', 'Done') }}</li>
    </ol>

    <p v-if="isBw" class="mc-alert mc-alert--info" role="note">
      您購買的是{{ clubAssets.nameZh }}的商品，<strong>款項由{{ subject }}代收，發票抬頭亦為{{ subject }}</strong>。
    </p>
    <p v-if="info && !paymentAvailable" class="mc-alert mc-alert--error" role="alert">
      <template v-if="isEn"><strong>Online payment is not available yet.</strong> You can review your items and fill in your details, but orders cannot be submitted for now. We will announce on this website when payment opens.</template><template v-else><strong>目前暫未開放線上付款。</strong>您可以先確認商品與填寫資料，但暫時無法送出訂單；開放付款後會於本站公告。</template>
    </p>
    <p v-else-if="!info" class="mc-alert mc-alert--error" role="alert">{{ tx('商店資訊暫時無法載入，暫時無法結帳，請稍後再試。', 'Shop information could not be loaded, so checkout is unavailable for now. Please try again later.') }}</p>

    <p v-if="!loaded" class="mc-empty" role="status">{{ tx('載入中…', 'Loading…') }}</p>
    <p v-else-if="loadError" class="mc-alert mc-alert--error" role="alert">{{ loadError }}</p>
    <div v-else-if="items.length === 0 && !createdOrder" class="editorial-note">
      <p>{{ tx('您的購物車是空的，沒有可以結帳的商品。', 'Your cart is empty, so there is nothing to check out.') }}</p>
      <p style="margin-top:.75rem"><a class="btn btn--primary btn--sm" :href="lp('/zh/shop/')">{{ tx('前往選購', 'Go to the shop') }}</a></p>
    </div>

    <div v-else class="shop-layout">
      <div>
        <form class="tcrfc-form" novalidate @submit.prevent="onSubmit">
          <fieldset :disabled="submitting">
            <legend>{{ tx('訂購人', 'Your details') }}</legend>
            <p v-if="!isMember" class="field-hint"><template v-if="isEn">Already a member? <a :href="lp('/zh/member/?next=' + encodeURIComponent(lp('/zh/checkout/')))">Sign in</a> to fill in your details automatically and find your order under My Orders. <strong>You can also check out without registering</strong>, using just your email.</template><template v-else>已是會員？<a :href="lp('/zh/member/?next=' + encodeURIComponent(lp('/zh/checkout/')))">登入</a>後可自動帶入資料並於「我的訂單」查詢。<strong>未註冊也可以直接結帳</strong>，填 Email 即可。</template></p>
            <p v-else class="field-hint">{{ tx('已登入會員：資料已自動帶入，可直接修改；留空的欄位會以帳號資料補上。', 'Signed-in member: your details have been filled in and you can edit them. Any fields left blank will be completed from your account.') }}</p>
            <div class="form-grid">
              <div class="form-field">
                <label for="co-name">{{ tx('姓名', 'Name') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="co-name" v-model="form.recipientName" type="text" autocomplete="name" maxlength="64" :aria-invalid="!!errors.recipientName" aria-describedby="co-name-error">
                <p v-if="errors.recipientName" id="co-name-error" class="field-error is-shown" role="alert">{{ errors.recipientName }}</p>
              </div>
              <div class="form-field">
                <label for="co-email">Email<span class="req" aria-hidden="true">*</span></label>
                <input id="co-email" v-model="form.email" type="email" autocomplete="email" maxlength="255" :aria-invalid="!!errors.email" aria-describedby="co-email-error">
                <p v-if="errors.email" id="co-email-error" class="field-error is-shown" role="alert">{{ errors.email }}</p>
                <p class="field-hint">{{ tx('訂單確認信與查詢連結會寄到這個信箱。', 'Your order confirmation and lookup link will be sent to this email address.') }}</p>
              </div>
              <div class="form-field">
                <label for="co-phone">{{ tx('手機', 'Mobile phone') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="co-phone" v-model="form.recipientPhone" type="tel" autocomplete="tel" maxlength="32" :aria-invalid="!!errors.recipientPhone" aria-describedby="co-phone-error">
                <p v-if="errors.recipientPhone" id="co-phone-error" class="field-error is-shown" role="alert">{{ errors.recipientPhone }}</p>
              </div>
            </div>
          </fieldset>

          <fieldset :disabled="submitting">
            <legend>{{ tx('配送方式', 'Delivery method') }}</legend>
            <div class="checkbox-group" role="radiogroup" :aria-label="tx('配送方式', 'Delivery method')">
              <label v-for="d in deliveryOptions" :key="d.code" class="checkbox-field">
                <input v-model="form.deliveryMethod" type="radio" name="delivery" :value="d.code"><span>{{ d.label }}</span>
              </label>
            </div>
            <p v-if="info" class="field-hint">
              <template v-if="isEn">
                <template v-if="info.shippingFee > 0">Shipping is {{ formatPrice(info.shippingFee) }}<template v-if="info.freeShippingThreshold">, free on orders of {{ formatPrice(info.freeShippingThreshold) }} or more</template>; on-site pickup has no shipping fee.</template>
                <template v-else>Shipping is free.</template>
                <template v-if="info.excludedRegions.length"> We do not currently deliver to: {{ info.excludedRegions.join(', ') }}.</template>
              </template>
              <template v-else>
                <template v-if="info.shippingFee > 0">運費 {{ formatPrice(info.shippingFee) }}<template v-if="info.freeShippingThreshold">，單筆滿 {{ formatPrice(info.freeShippingThreshold) }} 免運</template>；現場自取免運費。</template>
                <template v-else>運費全面免收。</template>
                <template v-if="info.excludedRegions.length">　目前不配送：{{ info.excludedRegions.join('、') }}。</template>
              </template>
            </p>

            <div class="form-grid" style="margin-top:1.25rem">
              <div v-if="form.deliveryMethod === 'home_delivery'" class="form-field form-field--full">
                <label for="co-addr">{{ tx('收件地址', 'Delivery address') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="co-addr" v-model="form.recipientAddress" type="text" autocomplete="shipping street-address" maxlength="500" :placeholder="tx('例：臺中市西屯區…', 'e.g. Xitun District, Taichung City…')" :aria-invalid="!!errors.recipientAddress" aria-describedby="co-addr-error">
                <p v-if="errors.recipientAddress" id="co-addr-error" class="field-error is-shown" role="alert">{{ errors.recipientAddress }}</p>
              </div>
              <div v-else-if="form.deliveryMethod === 'cvs_pickup'" class="form-field form-field--full">
                <label for="co-store">{{ tx('取貨門市', 'Pickup store') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="co-store" v-model="form.pickupStore" type="text" maxlength="200" :placeholder="tx('門市名稱或代碼，例：7-ELEVEN 台中某某門市', 'Store name or code, e.g. a 7-ELEVEN store in Taichung')" :aria-invalid="!!errors.pickupStore" aria-describedby="co-store-error">
                <p v-if="errors.pickupStore" id="co-store-error" class="field-error is-shown" role="alert">{{ errors.pickupStore }}</p>
                <p class="field-hint">{{ tx('超商取貨僅取貨，不在門市付款。', 'Convenience-store pickup is for collection only. You do not pay at the store.') }}</p>
              </div>
              <p v-else class="field-hint form-field--full">{{ tx('現場自取不需填寫地址，我們會於備貨完成後以 Email 通知取貨方式。', 'No address is needed for on-site pickup. We will email you the pickup details once your order is ready.') }}</p>
              <div class="form-field form-field--full">
                <label for="co-note">{{ tx('訂單備註（選填）', 'Order notes (optional)') }}</label>
                <textarea id="co-note" v-model="form.customerNote" rows="3" maxlength="500" :aria-invalid="!!errors.customerNote" />
                <p v-if="errors.customerNote" class="field-error is-shown" role="alert">{{ errors.customerNote }}</p>
              </div>
            </div>
          </fieldset>

          <fieldset :disabled="submitting">
            <legend>{{ tx('電子發票', 'E-invoice') }}</legend>
            <div class="checkbox-group" role="radiogroup" :aria-label="tx('發票開立方式', 'Invoice type')">
              <label v-for="o in INVOICE_OPTIONS" :key="o.code" class="checkbox-field">
                <input v-model="form.invoiceType" type="radio" name="invoice" :value="o.code"><span>{{ o.label }}</span>
              </label>
            </div>
            <div class="form-grid" style="margin-top:1.25rem">
              <div v-if="form.invoiceType === 'mobile_barcode' || form.invoiceType === 'citizen_cert'" class="form-field">
                <label for="co-carrier">{{ form.invoiceType === 'mobile_barcode' ? tx('手機條碼', 'Mobile barcode') : tx('自然人憑證條碼', 'Citizen Digital Certificate barcode') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="co-carrier" v-model="form.carrierId" type="text" autocomplete="off" autocapitalize="characters" spellcheck="false" :placeholder="form.invoiceType === 'mobile_barcode' ? '/ABC+123' : 'AB12345678901234'" :aria-invalid="!!errors.carrierId" aria-describedby="co-carrier-error">
                <p v-if="errors.carrierId" id="co-carrier-error" class="field-error is-shown" role="alert">{{ errors.carrierId }}</p>
              </div>
              <div v-else-if="form.invoiceType === 'tax_id'" class="form-field">
                <label for="co-taxid">{{ tx('統一編號', 'Unified Business Number') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="co-taxid" v-model="form.taxId" type="text" inputmode="numeric" maxlength="8" autocomplete="off" :aria-invalid="!!errors.taxId" aria-describedby="co-taxid-error">
                <p v-if="errors.taxId" id="co-taxid-error" class="field-error is-shown" role="alert">{{ errors.taxId }}</p>
              </div>
              <div v-else class="form-field form-field--full">
                <label for="co-donate">{{ tx('捐贈對象', 'Donate to') }}<span class="req" aria-hidden="true">*</span></label>
                <select id="co-donate" v-model="form.donationCode" :aria-invalid="!!errors.donationCode" aria-describedby="co-donate-error">
                  <option value="">{{ tx('請選擇要捐贈的團體', 'Choose an organisation to donate to') }}</option>
                  <option v-for="d in info?.donationCodes ?? []" :key="d.code" :value="d.code">{{ d.orgName }}（{{ d.code }}）</option>
                </select>
                <p v-if="errors.donationCode" id="co-donate-error" class="field-error is-shown" role="alert">{{ errors.donationCode }}</p>
              </div>
            </div>
            <p class="field-hint"><template v-if="isEn">The invoice is issued to <strong>{{ subject }}</strong> and you will be notified by email once payment is complete. If you return an item, the invoice is voided or a discount note is issued.</template><template v-else>發票抬頭為<strong>{{ subject }}</strong>，付款完成後以 Email 通知。退貨時同步作廢或折讓。</template></p>
          </fieldset>

          <fieldset :disabled="submitting">
            <legend>{{ tx('條款', 'Terms') }}</legend>
            <div class="consent-block">
              <label class="checkbox-field">
                <input v-model="agree" type="checkbox" :aria-invalid="consentError" aria-describedby="co-agree-error">
                <span v-if="isEn">I have read and agree to the <a :href="lp('/zh/privacy/')">Privacy Policy</a> and the <a :href="lp('/zh/shop/policy/#terms')">terms of sale</a>, and I understand the <a :href="lp('/zh/shop/policy/#returns')">returns rules</a> for the seven-day cooling-off period.</span>
                <span v-else>我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>與<a :href="lp('/zh/shop/policy/#terms')">交易條款</a>，並瞭解七日猶豫期的<a :href="lp('/zh/shop/policy/#returns')">退換貨規定</a>。</span>
              </label>
              <p v-if="consentError" id="co-agree-error" class="field-error is-shown" role="alert">{{ tx('請勾選同意後再送出。', 'Please tick the box to agree before submitting.') }}</p>
            </div>
          </fieldset>

          <p v-if="formError" class="mc-alert mc-alert--error" role="alert">{{ formError }}</p>
          <p v-if="createdOrder" class="mc-alert mc-alert--info" role="status">
            <template v-if="isEn">Order <span class="mc-mono">{{ createdOrder.orderNo }}</span> has been created but payment is not complete. Press the button below to go to LINE Pay again, or continue paying later from <a :href="lp('/zh/order/lookup/')">Order lookup</a> (your order is held until {{ formatTaipeiDateTime(createdOrder.expiresAt, locale) || 'the payment deadline' }}).</template>
            <template v-else>訂單 <span class="mc-mono">{{ createdOrder.orderNo }}</span> 已建立，尚未完成付款。請按下方按鈕重新前往 LINE Pay；
            也可以稍後到<a :href="lp('/zh/order/lookup/')">訂單查詢</a>繼續付款（訂單保留至 {{ formatTaipeiDateTime(createdOrder.expiresAt, locale) || '付款期限內' }}）。</template>
          </p>

          <div class="form-submit-note">
            <button class="btn btn--linepay" type="submit" :disabled="submitting || !paymentAvailable || (!createdOrder && !cart?.canCheckout)">
              {{ submitting ? tx('處理中…', 'Processing…') : createdOrder ? tx('重新前往 LINE Pay 付款', 'Go to LINE Pay again') : tx('以 LINE Pay 付款', 'Pay with LINE Pay') }}
            </button>
            <p class="field-hint" style="margin-top:.85rem">{{ tx('點擊後將建立訂單並轉往 LINE Pay 完成付款，本站不會取得也不會儲存您的信用卡資料。付款成功後自動返回本站並開立電子發票。', 'Selecting this creates your order and takes you to LINE Pay to complete payment. This website does not receive or store your credit card details. After a successful payment you return to this website and an e-invoice is issued.') }}</p>
          </div>
        </form>
      </div>

      <aside class="shop-layout__aside">
        <div class="summary">
          <h2>{{ tx('訂單摘要', 'Order summary') }}</h2>
          <p v-for="it in items" :key="it.variantId" class="summary__line">
            <span>{{ it.productName || it.productSlug }}｜{{ it.variantLabel }} ×{{ it.quantity }}</span><strong>{{ formatPrice(it.lineTotal) }}</strong>
          </p>
          <p class="summary__line"><span>{{ tx('商品小計', 'Subtotal') }}</span><strong>{{ formatPrice(cart?.subtotal) }}</strong></p>
          <p class="summary__line"><span>{{ tx('運費', 'Shipping') }}</span><strong>{{ shippingFee > 0 ? formatPrice(shippingFee) : tx('免運', 'Free') }}</strong></p>
          <p class="summary__line summary__total"><span>{{ tx('應付金額', 'Total due') }}</span><span>{{ formatPrice(total) }}</span></p>
          <p v-if="isEn" class="summary__note">The final amount is calculated by our system when you submit your order. Payment is collected by {{ subject }}. Charity donations are received by the Taiwan Football Strategic Development Association on a separate platform and are checked out separately from this shop.</p>
          <p v-else class="summary__note">實際金額以送出訂單時後端計算為準。收款方為{{ subject }}。慈善捐款由台灣足球策略發展協會於獨立平台收受，與本商店分開結帳。</p>
          <p class="summary__note"><a :href="lp('/zh/cart/')">{{ tx('← 回購物車修改', '← Back to cart to edit') }}</a></p>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>
