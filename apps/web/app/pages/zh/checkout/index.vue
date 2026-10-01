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

definePageMeta({ nav: 'culture', unit: '8.3' })

const COLLECTING_SUBJECT_FALLBACK = '台中磐石足球俱樂部'

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const club = computed(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const clubAssets = computed(() => getClubAssets(club.value))
const isBw = computed(() => club.value === 'bw')

useSeoMeta({
  title: computed(() => `結帳 Checkout｜官方商店｜${clubAssets.value.nameZh}`),
  description: computed(() => `${clubAssets.value.shortNameZh}官方商店結帳：填寫收件資料、選擇配送方式與發票開立方式，確認後以 LINE Pay 完成付款。支援非會員結帳。`),
  robots: 'noindex, nofollow',
})

const { info, paymentAvailable } = useShopInfo()
const { cart, loadCart, checkout, payOrder, session } = useShop()
const subject = computed(() => info.value?.collectingSubjectName || COLLECTING_SUBJECT_FALLBACK)

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
  return [
    { code: 'home_delivery' as DeliveryCode, label: '宅配到府' },
    { code: 'cvs_pickup' as DeliveryCode, label: '超商取貨（僅取貨，不在門市付款）' },
    { code: 'onsite_pickup' as DeliveryCode, label: '主場賽事日或俱樂部現場自取' },
  ]
})
const INVOICE_OPTIONS: ReadonlyArray<{ code: InvoiceType, label: string }> = [
  { code: 'mobile_barcode', label: '手機條碼載具' },
  { code: 'citizen_cert', label: '自然人憑證載具' },
  { code: 'tax_id', label: '統一編號（公司報帳）' },
  { code: 'donation', label: '捐贈發票' },
]

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
    loadError.value = toMemberApiError(err, '購物車暫時無法載入，請稍後再試。').detail
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
  throw Object.assign(new Error('no_payment_url'), { status: 502, data: { detail: '無法取得付款連結，請稍後再試。' } })
}

async function onSubmit() {
  if (submitting.value) return
  formError.value = ''
  consentError.value = false
  errors.value = validateCheckout(form, isMember.value)
  if (!agree.value) consentError.value = true
  if (Object.keys(errors.value).length > 0 || consentError.value) {
    await nextTick()
    document.querySelector<HTMLElement>('.field-error.is-shown')?.closest('.form-field, .consent-block')?.querySelector<HTMLElement>('input,select,textarea')?.focus()
    return
  }
  if (!paymentAvailable.value) {
    formError.value = '目前暫未開放線上付款，暫時無法送出訂單。'
    return
  }
  if (!createdOrder.value && (!cart.value?.canCheckout || items.value.length === 0)) {
    formError.value = '購物車內有商品目前無法購買，請先回購物車調整。'
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
    const e = toMemberApiError(err, '送出訂單失敗，請稍後再試一次。')
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
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/shop/')">官方商店</a></li>
      <li><a :href="lp('/zh/cart/')">購物車</a></li>
      <li aria-current="page">結帳</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Checkout</p>
    <h1>結帳<span class="en">Checkout</span></h1>
  </div>
</section>

<!-- SPEC 3.8 §8.3 — 結帳：收件資料 → 配送方式 → 發票開立方式 → 確認 → 轉 LINE Pay。支援非會員結帳 -->
<section class="band form-band" aria-labelledby="checkout-title">
  <div class="container">
    <h2 id="checkout-title" class="visually-hidden">結帳流程</h2>

    <ol class="steps">
      <li data-done>購物車</li>
      <li aria-current="step">填寫資料</li>
      <li>LINE Pay 付款</li>
      <li>完成</li>
    </ol>

    <p v-if="isBw" class="mc-alert mc-alert--info" role="note">
      您購買的是{{ clubAssets.nameZh }}的商品，<strong>款項由{{ subject }}代收，發票抬頭亦為{{ subject }}</strong>。
    </p>
    <p v-if="info && !paymentAvailable" class="mc-alert mc-alert--error" role="alert">
      <strong>目前暫未開放線上付款。</strong>您可以先確認商品與填寫資料，但暫時無法送出訂單；開放付款後會於本站公告。
    </p>
    <p v-else-if="!info" class="mc-alert mc-alert--error" role="alert">商店資訊暫時無法載入，暫時無法結帳，請稍後再試。</p>

    <p v-if="!loaded" class="mc-empty" role="status">載入中…</p>
    <p v-else-if="loadError" class="mc-alert mc-alert--error" role="alert">{{ loadError }}</p>
    <div v-else-if="items.length === 0 && !createdOrder" class="editorial-note">
      <p>您的購物車是空的，沒有可以結帳的商品。</p>
      <p style="margin-top:.75rem"><a class="btn btn--primary btn--sm" :href="lp('/zh/shop/')">前往選購</a></p>
    </div>

    <div v-else class="shop-layout">
      <div>
        <form class="tcrfc-form" novalidate @submit.prevent="onSubmit">
          <fieldset :disabled="submitting">
            <legend>訂購人</legend>
            <p v-if="!isMember" class="field-hint">已是會員？<a :href="lp('/zh/member/?next=' + encodeURIComponent(lp('/zh/checkout/')))">登入</a>後可自動帶入資料並於「我的訂單」查詢。<strong>未註冊也可以直接結帳</strong>，填 Email 即可。</p>
            <p v-else class="field-hint">已登入會員：資料已自動帶入，可直接修改；留空的欄位會以帳號資料補上。</p>
            <div class="form-grid">
              <div class="form-field">
                <label for="co-name">姓名<span class="req" aria-hidden="true">*</span></label>
                <input id="co-name" v-model="form.recipientName" type="text" autocomplete="name" maxlength="64" :aria-invalid="!!errors.recipientName" aria-describedby="co-name-error">
                <p v-if="errors.recipientName" id="co-name-error" class="field-error is-shown" role="alert">{{ errors.recipientName }}</p>
              </div>
              <div class="form-field">
                <label for="co-email">Email<span class="req" aria-hidden="true">*</span></label>
                <input id="co-email" v-model="form.email" type="email" autocomplete="email" maxlength="255" :aria-invalid="!!errors.email" aria-describedby="co-email-error">
                <p v-if="errors.email" id="co-email-error" class="field-error is-shown" role="alert">{{ errors.email }}</p>
                <p class="field-hint">訂單確認信與查詢連結會寄到這個信箱。</p>
              </div>
              <div class="form-field">
                <label for="co-phone">手機<span class="req" aria-hidden="true">*</span></label>
                <input id="co-phone" v-model="form.recipientPhone" type="tel" autocomplete="tel" maxlength="32" :aria-invalid="!!errors.recipientPhone" aria-describedby="co-phone-error">
                <p v-if="errors.recipientPhone" id="co-phone-error" class="field-error is-shown" role="alert">{{ errors.recipientPhone }}</p>
              </div>
            </div>
          </fieldset>

          <fieldset :disabled="submitting">
            <legend>配送方式</legend>
            <div class="checkbox-group" role="radiogroup" aria-label="配送方式">
              <label v-for="d in deliveryOptions" :key="d.code" class="checkbox-field">
                <input v-model="form.deliveryMethod" type="radio" name="delivery" :value="d.code"><span>{{ d.label }}</span>
              </label>
            </div>
            <p v-if="info" class="field-hint">
              <template v-if="info.shippingFee > 0">運費 {{ formatPrice(info.shippingFee) }}<template v-if="info.freeShippingThreshold">，單筆滿 {{ formatPrice(info.freeShippingThreshold) }} 免運</template>；現場自取免運費。</template>
              <template v-else>運費全面免收。</template>
              <template v-if="info.excludedRegions.length">　目前不配送：{{ info.excludedRegions.join('、') }}。</template>
            </p>

            <div class="form-grid" style="margin-top:1.25rem">
              <div v-if="form.deliveryMethod === 'home_delivery'" class="form-field form-field--full">
                <label for="co-addr">收件地址<span class="req" aria-hidden="true">*</span></label>
                <input id="co-addr" v-model="form.recipientAddress" type="text" autocomplete="shipping street-address" maxlength="500" placeholder="例：臺中市西屯區…" :aria-invalid="!!errors.recipientAddress" aria-describedby="co-addr-error">
                <p v-if="errors.recipientAddress" id="co-addr-error" class="field-error is-shown" role="alert">{{ errors.recipientAddress }}</p>
              </div>
              <div v-else-if="form.deliveryMethod === 'cvs_pickup'" class="form-field form-field--full">
                <label for="co-store">取貨門市<span class="req" aria-hidden="true">*</span></label>
                <input id="co-store" v-model="form.pickupStore" type="text" maxlength="200" placeholder="門市名稱或代碼，例：7-ELEVEN 台中某某門市" :aria-invalid="!!errors.pickupStore" aria-describedby="co-store-error">
                <p v-if="errors.pickupStore" id="co-store-error" class="field-error is-shown" role="alert">{{ errors.pickupStore }}</p>
                <p class="field-hint">超商取貨僅取貨，不在門市付款。</p>
              </div>
              <p v-else class="field-hint form-field--full">現場自取不需填寫地址，我們會於備貨完成後以 Email 通知取貨方式。</p>
              <div class="form-field form-field--full">
                <label for="co-note">訂單備註（選填）</label>
                <textarea id="co-note" v-model="form.customerNote" rows="3" maxlength="500" :aria-invalid="!!errors.customerNote" />
                <p v-if="errors.customerNote" class="field-error is-shown" role="alert">{{ errors.customerNote }}</p>
              </div>
            </div>
          </fieldset>

          <fieldset :disabled="submitting">
            <legend>電子發票</legend>
            <div class="checkbox-group" role="radiogroup" aria-label="發票開立方式">
              <label v-for="o in INVOICE_OPTIONS" :key="o.code" class="checkbox-field">
                <input v-model="form.invoiceType" type="radio" name="invoice" :value="o.code"><span>{{ o.label }}</span>
              </label>
            </div>
            <div class="form-grid" style="margin-top:1.25rem">
              <div v-if="form.invoiceType === 'mobile_barcode' || form.invoiceType === 'citizen_cert'" class="form-field">
                <label for="co-carrier">{{ form.invoiceType === 'mobile_barcode' ? '手機條碼' : '自然人憑證條碼' }}<span class="req" aria-hidden="true">*</span></label>
                <input id="co-carrier" v-model="form.carrierId" type="text" autocomplete="off" autocapitalize="characters" spellcheck="false" :placeholder="form.invoiceType === 'mobile_barcode' ? '/ABC+123' : 'AB12345678901234'" :aria-invalid="!!errors.carrierId" aria-describedby="co-carrier-error">
                <p v-if="errors.carrierId" id="co-carrier-error" class="field-error is-shown" role="alert">{{ errors.carrierId }}</p>
              </div>
              <div v-else-if="form.invoiceType === 'tax_id'" class="form-field">
                <label for="co-taxid">統一編號<span class="req" aria-hidden="true">*</span></label>
                <input id="co-taxid" v-model="form.taxId" type="text" inputmode="numeric" maxlength="8" autocomplete="off" :aria-invalid="!!errors.taxId" aria-describedby="co-taxid-error">
                <p v-if="errors.taxId" id="co-taxid-error" class="field-error is-shown" role="alert">{{ errors.taxId }}</p>
              </div>
              <div v-else class="form-field form-field--full">
                <label for="co-donate">捐贈對象<span class="req" aria-hidden="true">*</span></label>
                <select id="co-donate" v-model="form.donationCode" :aria-invalid="!!errors.donationCode" aria-describedby="co-donate-error">
                  <option value="">請選擇要捐贈的團體</option>
                  <option v-for="d in info?.donationCodes ?? []" :key="d.code" :value="d.code">{{ d.orgName }}（{{ d.code }}）</option>
                </select>
                <p v-if="errors.donationCode" id="co-donate-error" class="field-error is-shown" role="alert">{{ errors.donationCode }}</p>
              </div>
            </div>
            <p class="field-hint">發票抬頭為<strong>{{ subject }}</strong>，付款完成後以 Email 通知。退貨時同步作廢或折讓。</p>
          </fieldset>

          <fieldset :disabled="submitting">
            <legend>條款</legend>
            <div class="consent-block">
              <label class="checkbox-field">
                <input v-model="agree" type="checkbox" :aria-invalid="consentError" aria-describedby="co-agree-error">
                <span>我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>與<a :href="lp('/zh/shop/policy/#terms')">交易條款</a>，並瞭解七日猶豫期的<a :href="lp('/zh/shop/policy/#returns')">退換貨規定</a>。</span>
              </label>
              <p v-if="consentError" id="co-agree-error" class="field-error is-shown" role="alert">請勾選同意後再送出。</p>
            </div>
          </fieldset>

          <p v-if="formError" class="mc-alert mc-alert--error" role="alert">{{ formError }}</p>
          <p v-if="createdOrder" class="mc-alert mc-alert--info" role="status">
            訂單 <span class="mc-mono">{{ createdOrder.orderNo }}</span> 已建立，尚未完成付款。請按下方按鈕重新前往 LINE Pay；
            也可以稍後到<a :href="lp('/zh/order/lookup/')">訂單查詢</a>繼續付款（訂單保留至 {{ formatTaipeiDateTime(createdOrder.expiresAt, locale) || '付款期限內' }}）。
          </p>

          <div class="form-submit-note">
            <button class="btn btn--linepay" type="submit" :disabled="submitting || !paymentAvailable || (!createdOrder && !cart?.canCheckout)">
              {{ submitting ? '處理中…' : createdOrder ? '重新前往 LINE Pay 付款' : '以 LINE Pay 付款' }}
            </button>
            <p class="field-hint" style="margin-top:.85rem">點擊後將建立訂單並轉往 LINE Pay 完成付款，本站不會取得也不會儲存您的信用卡資料。付款成功後自動返回本站並開立電子發票。</p>
          </div>
        </form>
      </div>

      <aside class="shop-layout__aside">
        <div class="summary">
          <h2>訂單摘要</h2>
          <p v-for="it in items" :key="it.variantId" class="summary__line">
            <span>{{ it.productName || it.productSlug }}｜{{ it.variantLabel }} ×{{ it.quantity }}</span><strong>{{ formatPrice(it.lineTotal) }}</strong>
          </p>
          <p class="summary__line"><span>商品小計</span><strong>{{ formatPrice(cart?.subtotal) }}</strong></p>
          <p class="summary__line"><span>運費</span><strong>{{ shippingFee > 0 ? formatPrice(shippingFee) : '免運' }}</strong></p>
          <p class="summary__line summary__total"><span>應付金額</span><span>{{ formatPrice(total) }}</span></p>
          <p class="summary__note">實際金額以送出訂單時後端計算為準。收款方為{{ subject }}。慈善捐款由台灣足球策略發展協會於獨立平台收受，與本商店分開結帳。</p>
          <p class="summary__note"><a :href="lp('/zh/cart/')">← 回購物車修改</a></p>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>
