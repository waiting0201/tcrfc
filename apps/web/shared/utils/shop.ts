// shared/utils/shop.ts — 站內商店 8.3（S3-5）共用的型別、驗證與格式化純函式
//
// 型別對應 apps/api README「F 批」的 `ShopDtos.cs`（camelCase 序列化，時間戳為 UTC 帶 Z）。
// 全部是純資料與純函式，沒有瀏覽器或 Nitro 依賴，app／server／檢查腳本（scripts/check-shop-lib.mjs）都可使用。
//
// 🔴 驗證規則逐條對照 `apps/api/Features/Shop/ShopOrderService.cs` 的 `ValidateAsync`（後端永遠是最終裁判；
// 這裡只是讓使用者在送出前就看見錯在哪）。**金額不在任何請求裡**：購物車與結帳的金額全由伺服器重算，
// 前台顯示的是後端回傳的值，不自行加總（運費、免運門檻、促銷價都以後端為準）。

// ── 型別 ──────────────────────────────────────────────────────────────────────
export interface ShopCollection {
  slug: string
  name: string | null
  narrative: string | null
  productCount: number
}

export interface ShopDeliveryMethod {
  code: string
  label: string
  baseFee: number
}

export interface ShopDonationCode {
  code: string
  orgName: string
}

export interface ShopInfo {
  entryTitle: string | null
  entryIntro: string | null
  policyNotice: string | null
  policyShipping: string | null
  policyReturns: string | null
  policyTerms: string | null
  shippingFee: number
  freeShippingThreshold: number | null
  excludedRegions: string[]
  deliveryMethods: ShopDeliveryMethod[]
  donationCodes: ShopDonationCode[]
  collectingSubjectName: string | null
  paymentAvailable: boolean
  collections: ShopCollection[]
}

export type ShopStockStatus = 'in_stock' | 'low_stock' | 'sold_out'

export interface ShopProductListItem {
  slug: string
  name: string | null
  collectionSlug: string | null
  collectionName: string | null
  tags: string[]
  isNewArrival: boolean
  imageUrl: string | null
  imageThumbUrl: string | null
  priceMin: number | null
  priceMax: number | null
  listPriceMin: number | null
  onSale: boolean
  stockStatus: ShopStockStatus
  stockStatusLabel: string
  sizes: string[]
  colours: string[]
}

export interface ShopImage {
  url: string
  thumbUrl: string
  width: number | null
  height: number | null
}

export interface ShopVariant {
  id: string
  sku: string
  size: string | null
  colour: string | null
  label: string
  listPrice: number
  price: number
  onSale: boolean
  availableQty: number
  purchasable: boolean
}

export interface ShopProductDetail {
  slug: string
  name: string | null
  narrative: string | null
  seoTitle: string | null
  seoDescription: string | null
  collectionSlug: string | null
  collectionName: string | null
  tags: string[]
  isNewArrival: boolean
  /** 尺碼表 JSON，**後端原樣給**；形狀由 `parseSizeChart` 容錯解析 */
  sizeChart: unknown
  images: ShopImage[]
  variants: ShopVariant[]
  priceMin: number | null
  priceMax: number | null
  listPriceMin: number | null
  onSale: boolean
  stockStatus: ShopStockStatus
  stockStatusLabel: string
}

export interface ShopCartItem {
  variantId: string
  productSlug: string
  productName: string | null
  variantLabel: string
  sku: string
  imageThumbUrl: string | null
  listPrice: number
  unitPrice: number
  onSale: boolean
  quantity: number
  lineTotal: number
  availableQty: number
  purchasable: boolean
  issue: 'unavailable' | 'insufficient_stock' | null
  issueMessage: string | null
}

export interface ShopCart {
  clubCode: string
  items: ShopCartItem[]
  itemCount: number
  subtotal: number
  shipping: { fee: number, freeThreshold: number | null, amountToFree: number | null }
  canCheckout: boolean
}

export interface ShopOrderItem {
  productName: string
  variantLabel: string | null
  sku: string
  unitPrice: number
  quantity: number
  lineTotal: number
}

export interface ShopOrderInvoice {
  type: InvoiceType
  typeLabel: string
  status: string
  statusLabel: string
  invoiceNo: string | null
  issuedAt: string | null
  taxId: string | null
  donationCode: string | null
}

export interface ShopOrderShipment {
  carrier: string | null
  trackingNo: string | null
  shippedAt: string | null
  deliveredAt: string | null
  pickupStatus: string | null
  pickupStatusLabel: string | null
  pickupDeadlineOn: string | null
}

export interface ShopOrder {
  orderNo: string
  clubCode: string
  status: string
  paymentStatus: string
  paymentStatusLabel: string
  paymentMethod: string
  paymentMethodLabel: string
  deliveryMethod: string
  deliveryMethodLabel: string
  subtotal: number
  shippingFee: number
  total: number
  items: ShopOrderItem[]
  recipientName: string | null
  recipientPhone: string | null
  recipientAddress: string | null
  buyerEmail: string | null
  customerNote: string | null
  isMasked: boolean
  invoice: ShopOrderInvoice | null
  shipment: ShopOrderShipment | null
  paymentUrl: string | null
  expiresAt: string | null
  canPay: boolean
  canCancel: boolean
  createdAt: string
  paidAt: string | null
}

export interface ShopOrderListItem {
  orderNo: string
  status: string
  paymentStatusLabel: string
  total: number
  itemCount: number
  firstItemName: string | null
  invoiceNo: string | null
  trackingNo: string | null
  createdAt: string
}

export type InvoiceType = 'mobile_barcode' | 'citizen_cert' | 'tax_id' | 'donation'
export type DeliveryCode = 'home_delivery' | 'cvs_pickup' | 'onsite_pickup'

export interface CheckoutForm {
  email: string
  recipientName: string
  recipientPhone: string
  deliveryMethod: DeliveryCode
  recipientAddress: string
  pickupStore: string
  customerNote: string
  invoiceType: InvoiceType
  carrierId: string
  taxId: string
  donationCode: string
}

// ── 格式化 ────────────────────────────────────────────────────────────────────
/** `1200` → `NT$1,200`。新台幣沒有小數；非有限數字回 `—`（不輸出 `NT$NaN`）。 */
export function formatPrice(amount: number | null | undefined): string {
  if (typeof amount !== 'number' || !Number.isFinite(amount)) return '—'
  return `NT$${Math.round(amount).toLocaleString('en-US')}`
}

/** 商品卡／詳情的價格區間：單價 `NT$120`，區間 `NT$120–NT$180`；沒有販售中規格回 `null`（呼叫端顯示「暫無販售」）。 */
export function formatPriceRange(min: number | null | undefined, max: number | null | undefined): string | null {
  if (typeof min !== 'number') return null
  if (typeof max !== 'number' || max === min) return formatPrice(min)
  return `${formatPrice(min)}–${formatPrice(max)}`
}

// ── 檢核（對照 ShopOrderService.ValidateAsync）─────────────────────────────────
export const EMAIL_SHAPE = /^[^@\s]+@[^@\s]+\.[^@\s]+$/
export const PHONE_SHAPE = /^[0-9+\-() #]{6,32}$/
export const MOBILE_BARCODE_SHAPE = /^\/[0-9A-Z.+-]{7}$/
export const CITIZEN_CERT_SHAPE = /^[A-Z]{2}\d{14}$/
export const TAX_ID_SHAPE = /^\d{8}$/

/** 統一編號檢核碼（與後端 `ShopOrderService.IsValidTaxId` 同一演算法：權重 1,2,1,2,1,2,4,1，總和為 10 的倍數；第 7 碼為 7 時總和或總和＋1 為 10 的倍數皆可）。 */
export function isValidTaxId(id: string): boolean {
  if (!TAX_ID_SHAPE.test(id)) return false
  const weights = [1, 2, 1, 2, 1, 2, 4, 1]
  let sum = 0
  for (let i = 0; i < 8; i++) {
    const p = Number(id[i]) * weights[i]!
    sum += Math.floor(p / 10) + (p % 10)
  }
  return sum % 10 === 0 || (id[6] === '7' && (sum + 1) % 10 === 0)
}

export type CheckoutErrors = Partial<Record<'email' | 'recipientName' | 'recipientPhone' | 'recipientAddress' | 'pickupStore' | 'carrierId' | 'taxId' | 'donationCode' | 'customerNote', string>>

/**
 * 結帳表單檢核。`memberFilled`＝已登入會員：姓名／Email／電話可由伺服器用帳號資料補，所以不強制（與後端一致），
 * 但**有填就要符合格式**。回傳空物件代表可以送出。
 */
export function validateCheckout(form: CheckoutForm, memberFilled = false): CheckoutErrors {
  const errors: CheckoutErrors = {}
  const email = form.email.trim()
  if (!email ? !memberFilled : (email.length > 255 || !EMAIL_SHAPE.test(email))) errors.email = '請填寫正確的 Email（訂單成立信會寄到這裡）。'

  const name = form.recipientName.trim()
  if (!name ? !memberFilled : name.length > 64) errors.recipientName = '請填寫收件人姓名（最多 64 個字）。'

  const phone = form.recipientPhone.trim()
  if (!phone ? !memberFilled : !PHONE_SHAPE.test(phone)) errors.recipientPhone = '請填寫正確的聯絡電話。'

  if (form.deliveryMethod === 'home_delivery') {
    const address = form.recipientAddress.trim()
    if (!address || address.length > 500) errors.recipientAddress = '宅配請填寫完整收件地址（最多 500 個字）。'
  }
  else if (form.deliveryMethod === 'cvs_pickup') {
    const store = form.pickupStore.trim()
    if (!store || store.length > 200) errors.pickupStore = '超商取貨請填寫取貨門市名稱或代碼。'
  }

  if (form.customerNote.trim().length > 500) errors.customerNote = '備註最多 500 個字。'

  switch (form.invoiceType) {
    case 'mobile_barcode':
      if (!MOBILE_BARCODE_SHAPE.test(form.carrierId.trim().toUpperCase())) errors.carrierId = '手機條碼載具格式不正確（斜線開頭共 8 碼，例如 /ABC+123）。'
      break
    case 'citizen_cert':
      if (!CITIZEN_CERT_SHAPE.test(form.carrierId.trim().toUpperCase())) errors.carrierId = '自然人憑證載具格式不正確（2 個英文字母加 14 位數字）。'
      break
    case 'tax_id':
      if (!isValidTaxId(form.taxId.trim())) errors.taxId = '統一編號不正確，請確認 8 位數字。'
      break
    case 'donation':
      if (!form.donationCode.trim()) errors.donationCode = '請從清單中選擇要捐贈的團體。'
      break
  }
  return errors
}

/** 組成結帳請求本文。**沒有任何金額欄位**；空字串欄位不送（會員由伺服器補帳號資料）。 */
export function buildCheckoutBody(form: CheckoutForm, lang: 'zh' | 'en'): Record<string, unknown> {
  const trim = (s: string) => s.trim()
  const body: Record<string, unknown> = {
    deliveryMethod: form.deliveryMethod,
    lang,
    invoice: buildInvoice(form),
  }
  if (trim(form.email)) body.email = trim(form.email)
  if (trim(form.recipientName)) body.recipientName = trim(form.recipientName)
  if (trim(form.recipientPhone)) body.recipientPhone = trim(form.recipientPhone)
  if (form.deliveryMethod === 'home_delivery') body.recipientAddress = trim(form.recipientAddress)
  if (form.deliveryMethod === 'cvs_pickup') body.pickupStore = trim(form.pickupStore)
  if (trim(form.customerNote)) body.customerNote = trim(form.customerNote)
  return body
}

function buildInvoice(form: CheckoutForm): Record<string, string> {
  switch (form.invoiceType) {
    case 'mobile_barcode':
    case 'citizen_cert':
      return { type: form.invoiceType, carrierId: form.carrierId.trim().toUpperCase() }
    case 'tax_id':
      return { type: 'tax_id', taxId: form.taxId.trim() }
    default:
      return { type: 'donation', donationCode: form.donationCode.trim() }
  }
}

/** 冪等鍵：8–64 字元、`[A-Za-z0-9_\-:.]`（後端 `IdempotencyKeyShape`）。 */
export function newIdempotencyKey(): string {
  const uuid = typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}-${Math.random().toString(36).slice(2, 12)}`
  return `co-${uuid}`
}

// ── 規格選擇 ──────────────────────────────────────────────────────────────────
/** 規格是否可加入購物車（後端 `purchasable` 為準；另外要求可售量 > 0，避免畫面出現「可購買但 0 件」的矛盾）。 */
export function variantBuyable(v: ShopVariant): boolean {
  return v.purchasable && v.availableQty > 0
}

// ── 尺碼表（後端原樣給 JSON，形狀不固定；容錯解析成表格）──────────────────────────────
export interface SizeChartTable {
  caption: string | null
  headers: string[]
  rows: string[][]
  note: string | null
}

function cell(v: unknown): string {
  if (v === null || v === undefined) return ''
  if (typeof v === 'number' || typeof v === 'string') return String(v)
  return ''
}

/**
 * 認得兩種形狀，其他一律回 `null`（不猜、不輸出原始 JSON）：
 *   1. `{ columns|headers: string[], rows: (string|number)[][] , unit?, note?, caption? }`
 *   2. `{ rows: Record<string, string|number>[] }` 或直接陣列 `Record<string, string|number>[]`——欄名取第一列的鍵順序
 */
export function parseSizeChart(raw: unknown): SizeChartTable | null {
  if (!raw) return null
  let value: unknown = raw
  if (typeof value === 'string') {
    try { value = JSON.parse(value) }
    catch { return null }
  }
  let rowsRaw: unknown
  let headers: string[] | null = null
  let caption: string | null = null
  let note: string | null = null
  if (Array.isArray(value)) {
    rowsRaw = value
  }
  else if (value && typeof value === 'object') {
    const o = value as Record<string, unknown>
    rowsRaw = o.rows
    const h = o.columns ?? o.headers
    if (Array.isArray(h)) headers = h.map(cell)
    if (typeof o.caption === 'string' && o.caption.trim()) caption = o.caption.trim()
    if (typeof o.note === 'string' && o.note.trim()) note = o.note.trim()
    else if (typeof o.unit === 'string' && o.unit.trim()) note = `單位：${o.unit.trim()}`
  }
  if (!Array.isArray(rowsRaw) || rowsRaw.length === 0) return null

  const rows: string[][] = []
  if (rowsRaw.every(r => Array.isArray(r))) {
    for (const r of rowsRaw as unknown[][]) rows.push(r.map(cell))
    if (!headers) return null
  }
  else if (rowsRaw.every(r => r && typeof r === 'object' && !Array.isArray(r))) {
    const objs = rowsRaw as Record<string, unknown>[]
    const keys = headers ?? Object.keys(objs[0]!)
    if (!headers) headers = keys
    for (const r of objs) rows.push(keys.map(k => cell(r[k])))
  }
  else {
    return null
  }
  if (!headers || headers.length === 0 || rows.length === 0) return null
  return { caption, headers, rows, note }
}

// ── 購物車與結帳返回頁 ────────────────────────────────────────────────────────
/**
 * 付款頁導回本站的網址形狀（前後端約定，見 apps/web/README.md「S3-5」節、API 缺口一）：
 *   確認：`{站台}/{lang}/checkout/complete/?orderNo={訂單編號}&transactionId={LINE Pay 帶回}`
 *   取消：`{站台}/{lang}/checkout/complete/?orderNo={訂單編號}&cancel=1`
 */
export const CHECKOUT_RETURN_PATH = '/zh/checkout/complete/'

export function checkoutReturnUrl(origin: string, lang: 'zh' | 'en', orderNo: string, kind: 'confirm' | 'cancel'): string {
  const base = `${origin}/${lang}/checkout/complete/?orderNo=${encodeURIComponent(orderNo)}`
  return kind === 'cancel' ? `${base}&cancel=1` : base
}

/** 付款狀態代碼（後端 `paymentStatus`）：`pending`／`paid`／`failed`／`expired`／`refunded`。 */
export function isPaidStatus(paymentStatus: string): boolean {
  return paymentStatus === 'paid'
}
