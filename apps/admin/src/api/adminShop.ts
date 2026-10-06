/**
 * S1–S6 站內商店後台端點，對照 apps/api/README.md「C1」節。
 * 訂單狀態 `orderStatus` 本身就是中文；其餘狀態是英文代碼＋`…Label`（畫面只顯示標籤）。
 * 🔴 金流（LINE Pay）與電子發票尚未串接：憑證可先存放，系統目前不會用它連線。
 */
import {
  apiRequest,
  apiUploadRequest,
  buildQuery,
  downloadExport,
  putOrder,
  type BilingualContentInput,
  type PagedResult,
} from './adminCommon'

const base = (club: string) => `/api/v1/admin/${club}/shop`

export interface BatchSkipDto {
  id: string
  reason: string
}
export interface BatchOutcomeDto {
  updatedCount: number
  skipped: BatchSkipDto[]
}

// ══════════ S1 系列 ══════════
export type ShopPublishStatus = 'draft' | 'published'

export interface CollectionListItemDto {
  id: string
  slug: string
  sortOrder: number
  status: ShopPublishStatus
  statusLabel: string
  nameZh?: string | null
  nameEn?: string | null
  productCount: number
  updatedAt: string
}
export interface CollectionDetailDto extends CollectionListItemDto {
  zh: { name: string; narrative?: string | null }
  en?: { name: string; narrative?: string | null } | null
  createdAt: string
}
export interface SaveCollectionPayload {
  slug?: string
  sortOrder?: number
  status: ShopPublishStatus
  content: BilingualContentInput<{ name: string; narrative?: string | null }>
}
export function listCollections(club: string, params: { status?: string } = {}): Promise<CollectionListItemDto[]> {
  return apiRequest<CollectionListItemDto[]>(`${base(club)}/collections${buildQuery(params)}`)
}
export function getCollection(club: string, id: string): Promise<CollectionDetailDto> {
  return apiRequest<CollectionDetailDto>(`${base(club)}/collections/${id}`)
}
export function createCollection(club: string, body: SaveCollectionPayload): Promise<CollectionDetailDto> {
  return apiRequest<CollectionDetailDto>(`${base(club)}/collections`, { method: 'POST', body })
}
export function updateCollection(club: string, id: string, body: SaveCollectionPayload): Promise<CollectionDetailDto> {
  return apiRequest<CollectionDetailDto>(`${base(club)}/collections/${id}`, { method: 'PUT', body })
}
export function deleteCollection(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/collections/${id}`, { method: 'DELETE' })
}
export function reorderCollections(club: string, ids: string[]): Promise<void> {
  return putOrder(`${base(club)}/collections/order`, ids)
}

// ══════════ S1 商品 ══════════
export type OutOfStockBehavior = 'show_unavailable' | 'hide'
export type ProductDisplayStatus = 'draft' | 'published' | 'sold_out'

export interface ProductListItemDto {
  id: string
  slug: string
  collectionId?: string | null
  collectionName?: string | null
  isNewArrival: boolean
  sortOrder: number
  status: ShopPublishStatus
  displayStatus: ProductDisplayStatus
  displayStatusLabel: string
  outOfStockBehavior: OutOfStockBehavior
  outOfStockBehaviorLabel: string
  coverThumbUrl?: string | null
  nameZh?: string | null
  nameEn?: string | null
  variantCount: number
  /** 沒有檢視規格與售價權限時為 null */
  priceMin: number | null
  priceMax: number | null
  availableTotal: number | null
  updatedAt: string
}

export interface ProductLocale {
  name: string
  narrative?: string | null
  seoTitle?: string | null
  seoDescription?: string | null
  tags?: string | null
}

export interface ProductImageDto {
  id: string
  imageKey?: string | null
  imageUrl?: string | null
  imageThumbUrl?: string | null
  width?: number | null
  height?: number | null
  sortOrder: number
}

export type VariantStatus = 'active' | 'inactive'
export interface VariantDto {
  id: string
  productId: string
  sku: string
  size?: string | null
  colour?: string | null
  label: string
  price: number
  salePrice?: number | null
  effectivePrice: number
  /** 沒有檢視成本權限時為 null */
  cost: number | null
  stockQty: number
  reservedQty: number
  availableQty: number
  status: VariantStatus
  statusLabel: string
  lowStockThreshold?: number | null
  isLowStock: boolean
  sortOrder: number
  updatedAt: string
}

export interface ProductDetailDto extends Omit<ProductListItemDto, 'priceMin' | 'priceMax' | 'availableTotal' | 'coverThumbUrl'> {
  sizeChart: unknown
  zh: ProductLocale
  en?: ProductLocale | null
  images: ProductImageDto[]
  variants: VariantDto[]
  canViewVariants: boolean
  canViewCost: boolean
  createdAt: string
}

export interface SaveProductPayload {
  slug?: string
  collectionId?: string | null
  isNewArrival: boolean
  sortOrder?: number
  status: ShopPublishStatus
  outOfStockBehavior?: OutOfStockBehavior
  sizeChart?: unknown
  content: BilingualContentInput<ProductLocale>
}

export function listProducts(
  club: string,
  params: { status?: string; collectionId?: string; keyword?: string; page?: number; pageSize?: number } = {},
): Promise<PagedResult<ProductListItemDto>> {
  return apiRequest<PagedResult<ProductListItemDto>>(`${base(club)}/products${buildQuery(params)}`)
}
export function getProduct(club: string, id: string): Promise<ProductDetailDto> {
  return apiRequest<ProductDetailDto>(`${base(club)}/products/${id}`)
}
export function createProduct(club: string, body: SaveProductPayload): Promise<ProductDetailDto> {
  return apiRequest<ProductDetailDto>(`${base(club)}/products`, { method: 'POST', body })
}
export function updateProduct(club: string, id: string, body: SaveProductPayload): Promise<ProductDetailDto> {
  return apiRequest<ProductDetailDto>(`${base(club)}/products/${id}`, { method: 'PUT', body })
}
export function deleteProduct(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/products/${id}`, { method: 'DELETE' })
}
export function reorderProducts(club: string, ids: string[]): Promise<void> {
  return putOrder(`${base(club)}/products/order`, ids)
}
export function addProductImages(club: string, id: string, files: File[]): Promise<ProductDetailDto> {
  const form = new FormData()
  for (const f of files) form.append('files', f)
  return apiUploadRequest<ProductDetailDto>(`${base(club)}/products/${id}/images`, form, { method: 'POST' })
}
export function deleteProductImage(club: string, id: string, imageId: string): Promise<void> {
  return apiRequest<void>(`${base(club)}/products/${id}/images/${imageId}`, { method: 'DELETE' })
}
export function reorderProductImages(club: string, id: string, ids: string[]): Promise<void> {
  return putOrder(`${base(club)}/products/${id}/images/order`, ids)
}

// ── 規格 ──
export interface SaveVariantPayload {
  sku: string
  size?: string | null
  colour?: string | null
  price: number
  salePrice?: number | null
  cost?: number | null
  clearCost?: boolean
  status?: VariantStatus
  lowStockThreshold?: number | null
  sortOrder?: number
  /** 只有新增有效，記成一筆進貨 */
  initialStock?: number
}
const variants = (club: string, productId: string) => `${base(club)}/products/${productId}/variants`
export function listVariants(club: string, productId: string): Promise<VariantDto[]> {
  return apiRequest<VariantDto[]>(variants(club, productId))
}
export function createVariant(club: string, productId: string, body: SaveVariantPayload): Promise<VariantDto> {
  return apiRequest<VariantDto>(variants(club, productId), { method: 'POST', body })
}
export function updateVariant(club: string, productId: string, id: string, body: SaveVariantPayload): Promise<VariantDto> {
  return apiRequest<VariantDto>(`${variants(club, productId)}/${id}`, { method: 'PUT', body })
}
export function deleteVariant(club: string, productId: string, id: string): Promise<void> {
  return apiRequest<void>(`${variants(club, productId)}/${id}`, { method: 'DELETE' })
}
export function reorderVariants(club: string, productId: string, ids: string[]): Promise<void> {
  return putOrder(`${variants(club, productId)}/order`, ids)
}

// ══════════ S2 庫存 ══════════
export interface InventoryItemDto {
  variantId: string
  productId: string
  productName: string
  productStatus: string
  sku: string
  label: string
  status: VariantStatus
  statusLabel: string
  stockQty: number
  reservedQty: number
  availableQty: number
  lowStockThreshold?: number | null
  isLowStock: boolean
}
export interface InventoryMovementDto {
  id: string
  variantId: string
  sku: string
  productName: string
  movementType: string
  movementTypeLabel: string
  quantity: number
  stockAfter: number
  reservedAfter: number
  reason?: string | null
  orderId?: string | null
  orderNo?: string | null
  handledByName?: string | null
  occurredAt: string
}
export type InventoryAdjustType = 'stock_in' | 'damage' | 'adjust' | 'stocktake'
export function listInventory(
  club: string,
  params: { keyword?: string; productId?: string; lowStockOnly?: boolean; status?: string; page?: number; pageSize?: number } = {},
): Promise<PagedResult<InventoryItemDto>> {
  return apiRequest<PagedResult<InventoryItemDto>>(`${base(club)}/inventory${buildQuery(params)}`)
}
export function listInventoryMovements(
  club: string,
  params: { variantId?: string; type?: string; from?: string; to?: string; orderId?: string; page?: number; pageSize?: number } = {},
): Promise<PagedResult<InventoryMovementDto>> {
  return apiRequest<PagedResult<InventoryMovementDto>>(`${base(club)}/inventory/movements${buildQuery(params)}`)
}
export function createInventoryMovement(
  club: string,
  body: { variantId: string; type: InventoryAdjustType; quantity: number; reason?: string },
): Promise<{ movement: InventoryMovementDto; item: InventoryItemDto }> {
  return apiRequest(`${base(club)}/inventory/movements`, { method: 'POST', body })
}

// ══════════ S3 訂單 ══════════
export type OrderStatus = '待付款' | '已付款' | '備貨中' | '已出貨' | '已完成' | '已取消' | '退貨處理中' | '已退款'
export const ORDER_STATUS_OPTIONS: OrderStatus[] = ['待付款', '已付款', '備貨中', '已出貨', '已完成', '已取消', '退貨處理中', '已退款']
export const PAYMENT_STATUS_OPTIONS = [
  { value: 'pending', label: '待付款' },
  { value: 'paid', label: '已付款' },
  { value: 'failed', label: '付款失敗' },
  { value: 'expired', label: '已逾時' },
  { value: 'refunded', label: '已退款' },
]
export const DELIVERY_METHOD_OPTIONS = [
  { value: 'home_delivery', label: '宅配' },
  { value: 'cvs_pickup', label: '超商取貨' },
  { value: 'onsite_pickup', label: '現場自取' },
]
export const PAYMENT_METHOD_OPTIONS = [
  { value: 'linepay', label: 'LINE Pay' },
  { value: 'onsite', label: '現場收款' },
]
export const SETTLEMENT_STATUS_OPTIONS = [
  { value: 'pending', label: '待結算' },
  { value: 'settled', label: '已結算' },
]

export interface OrderFilter {
  paymentStatus?: string
  orderStatus?: string
  deliveryMethod?: string
  paymentMethod?: string
  settlementStatus?: string
  isMember?: boolean
  from?: string
  to?: string
  keyword?: string
}

export interface OrderListItemDto {
  id: string
  orderNo: string
  createdAt: string
  paidAt?: string | null
  subtotal: number
  shippingFee: number
  total: number
  paymentStatus: string
  paymentStatusLabel: string
  paymentMethod: string
  paymentMethodLabel: string
  orderStatus: OrderStatus
  deliveryMethod: string
  deliveryMethodLabel: string
  shipmentStatusLabel: string
  isMember: boolean
  isManual: boolean
  sellingClubId: string
  sellingClubCode: string
  sellingClubName: string
  settlementStatus: string
  settlementStatusLabel: string
  recipientName?: string | null
  itemCount: number
  isMasked: boolean
}

export interface OrderItemDto {
  id: string
  variantId?: string | null
  productName: string
  variantLabel: string
  sku: string
  unitPrice: number
  quantity: number
  lineTotal: number
  refundedQuantity: number
}
export interface ShipmentDto {
  id: string
  carrier?: string | null
  trackingNo?: string | null
  storeBranchCode?: string | null
  shippedAt?: string | null
  deliveredAt?: string | null
  pickupStatus?: 'waiting' | 'picked_up' | 'overdue' | null
  pickupStatusLabel?: string | null
  pickupDeadlineOn?: string | null
  arrivalNotifiedAt?: string | null
}
export type OrderAction = 'prepare' | 'ship' | 'complete' | 'cancel' | 'request_refund'
export interface OrderDetailDto {
  id: string
  orderNo: string
  createdAt: string
  paidAt?: string | null
  completedAt?: string | null
  cancelledAt?: string | null
  cancelReason?: string | null
  subtotal: number
  shippingFee: number
  total: number
  linepayTransactionId?: string | null
  paymentStatus: string
  paymentStatusLabel: string
  paymentMethod: string
  paymentMethodLabel: string
  orderStatus: OrderStatus
  deliveryMethod: string
  deliveryMethodLabel: string
  shipmentStatusLabel: string
  isMember: boolean
  memberId?: string | null
  memberNo?: string | null
  isManual: boolean
  sellingClubId: string
  sellingClubCode: string
  sellingClubName: string
  collectingClubId?: string | null
  collectingClubName?: string | null
  recipientName?: string | null
  recipientPhone?: string | null
  recipientAddress?: string | null
  customerNote?: string | null
  internalNote?: string | null
  settlementStatus: string
  settlementStatusLabel: string
  settledOn?: string | null
  settlementNote?: string | null
  items: OrderItemDto[]
  shipment?: ShipmentDto | null
  /** 買家 Email（客服聯絡用）；沒有檢視完整個資權限時是遮罩值，照原樣顯示。 */
  buyerEmail?: string | null
  invoice?: OrderInvoiceDto | null
  refunds: { id: string; status: string; statusLabel: string; refundAmount: number; reason: string; createdAt: string }[]
  availableActions: OrderAction[]
  isMasked: boolean
  canReveal: boolean
  updatedAt: string
}

/** 訂單發票摘要（對照 `AdminOrderInvoiceDto`）；`*Label` 是日常中文，畫面一律顯示 Label。 */
export interface OrderInvoiceDto {
  invoiceNo?: string | null
  issuedAt?: string | null
  issueStatus?: string | null
  issueStatusLabel?: string | null
  voidStatus?: string | null
  voidStatusLabel?: string | null
  /** 開立方式代碼（mobile_barcode／citizen_cert／tax_id／donation），畫面不顯示，只顯示 `typeLabel`。 */
  type?: string | null
  typeLabel?: string | null
  /** 載具號碼：視同個資，無權限時為遮罩值。 */
  carrierId?: string | null
  taxId?: string | null
  donationCode?: string | null
}

export interface CreateOrderPayload {
  items: { variantId: string; quantity: number }[]
  deliveryMethod: string
  recipientName?: string | null
  recipientPhone?: string | null
  recipientAddress?: string | null
  memberId?: string | null
  customerNote?: string | null
  internalNote?: string | null
  shippingFee?: number | null
  completeImmediately?: boolean
}

const orders = (club: string) => `${base(club)}/orders`
export function listOrders(club: string, filter: OrderFilter, page: number, pageSize: number): Promise<PagedResult<OrderListItemDto>> {
  return apiRequest<PagedResult<OrderListItemDto>>(`${orders(club)}${buildQuery({ ...filter, page, pageSize })}`)
}
export function getOrder(club: string, id: string): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}`)
}
export function createOrder(club: string, body: CreateOrderPayload): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(orders(club), { method: 'POST', body })
}
export function saveOrderNote(club: string, id: string, internalNote: string): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}/notes`, { method: 'PUT', body: { internalNote } })
}
export function saveOrderSettlement(club: string, id: string, body: { status: string; settledOn?: string | null; note?: string | null }): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}/settlement`, { method: 'PUT', body })
}
export function batchOrderSettlement(club: string, body: { ids: string[]; status: string; settledOn?: string | null; note?: string | null }): Promise<BatchOutcomeDto> {
  return apiRequest<BatchOutcomeDto>(`${orders(club)}/batch/settlement`, { method: 'POST', body })
}
export function releaseExpiredOrders(club: string): Promise<{ expiredCount: number; timeoutMinutes: number }> {
  return apiRequest(`${orders(club)}/release-expired`, { method: 'POST' })
}
export function prepareOrder(club: string, id: string): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}/prepare`, { method: 'POST' })
}
export function cancelOrder(club: string, id: string, reason: string): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}/cancel`, { method: 'POST', body: { reason } })
}
export interface ShipOrderPayload {
  carrier?: string | null
  trackingNo?: string | null
  storeBranchCode?: string | null
  pickupDeadlineOn?: string | null
}
export function shipOrder(club: string, id: string, body: ShipOrderPayload): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}/ship`, { method: 'POST', body })
}
export function updateShipment(club: string, id: string, body: ShipOrderPayload): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}/shipment`, { method: 'PUT', body })
}
export function markArrivalNotified(club: string, id: string, pickupDeadlineOn?: string | null): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}/arrival-notified`, { method: 'POST', body: { pickupDeadlineOn } })
}
export function completeOrder(club: string, id: string): Promise<OrderDetailDto> {
  return apiRequest<OrderDetailDto>(`${orders(club)}/${id}/complete`, { method: 'POST' })
}
export function exportOrders(club: string, filter: OrderFilter, purpose: string): Promise<void> {
  return downloadExport(`${orders(club)}/export${buildQuery({ ...filter, purpose })}`, '訂單.csv')
}

// ══════════ S4 出貨 ══════════
export interface ShipmentListItemDto {
  orderId: string
  orderNo: string
  orderStatus: OrderStatus
  deliveryMethod: string
  deliveryMethodLabel: string
  createdAt: string
  recipientName?: string | null
  itemCount: number
  shipment?: ShipmentDto | null
  isMasked: boolean
}
export interface PickingListDto {
  generatedAt: string
  orderCount: number
  totalQuantity: number
  lines: { sku: string; productName: string; variantLabel: string; quantity: number; orderCount: number }[]
}
export interface DispatchSlipDto {
  orderId: string
  orderNo: string
  deliveryMethod: string
  deliveryMethodLabel: string
  recipientName?: string | null
  recipientPhone?: string | null
  recipientAddress?: string | null
  storeBranchCode?: string | null
  customerNote?: string | null
  items: { sku: string; productName: string; variantLabel: string; quantity: number }[]
  isMasked: boolean
}
const shipments = (club: string) => `${base(club)}/shipments`
function idsQuery(name: string, ids: string[]): string {
  return ids.map((id) => `${name}=${encodeURIComponent(id)}`).join('&')
}
export function listShipments(
  club: string,
  params: { orderStatus?: string; deliveryMethod?: string; pickupStatus?: string; keyword?: string; page?: number; pageSize?: number },
): Promise<PagedResult<ShipmentListItemDto>> {
  return apiRequest<PagedResult<ShipmentListItemDto>>(`${shipments(club)}${buildQuery(params)}`)
}
export function getPickingList(club: string, orderIds: string[]): Promise<PickingListDto> {
  const query = orderIds.length > 0 ? `?${idsQuery('orderIds', orderIds)}` : ''
  return apiRequest<PickingListDto>(`${shipments(club)}/picking-list${query}`)
}
export function getDispatchSlips(club: string, orderIds: string[]): Promise<DispatchSlipDto[]> {
  return apiRequest<DispatchSlipDto[]>(`${shipments(club)}/dispatch-slips?${idsQuery('orderIds', orderIds)}`)
}
export function batchShip(club: string, body: { ids: string[]; carrier?: string | null }): Promise<BatchOutcomeDto> {
  return apiRequest<BatchOutcomeDto>(`${shipments(club)}/batch/ship`, { method: 'POST', body })
}
export interface ShipmentImportResultDto {
  updatedCount: number
  skipped: { row: number; orderNo?: string | null; reason: string }[]
}
export function importShipments(club: string, file: File): Promise<ShipmentImportResultDto> {
  return apiUploadRequest<ShipmentImportResultDto>(`${shipments(club)}/import`, fileForm(file), { method: 'POST' })
}
function fileForm(file: File): FormData {
  const form = new FormData()
  form.append('file', file)
  return form
}

// ══════════ S5 退貨與退款 ══════════
export const REFUND_STATUS_OPTIONS = [
  { value: 'requested', label: '申請中' },
  { value: 'approved', label: '已核准' },
  { value: 'received', label: '已驗收退回品' },
  { value: 'processing', label: '退款處理中' },
  { value: 'refunded', label: '已退款' },
  { value: 'rejected', label: '已駁回' },
]
export interface RefundListItemDto {
  id: string
  orderId: string
  orderNo: string
  status: string
  statusLabel: string
  refundAmount: number
  reason: string
  needsReturn: boolean
  paymentMethod: string
  paymentMethodLabel: string
  refundMethod?: string | null
  refundMethodLabel?: string | null
  createdAt: string
  updatedAt: string
}
export type RefundAction = 'approve' | 'reject' | 'receive' | 'execute'
export interface RefundDetailDto extends RefundListItemDto {
  orderStatus: OrderStatus
  orderTotal: number
  orderRefundedTotal: number
  reviewNote?: string | null
  approvedByName?: string | null
  receivedAt?: string | null
  receivedByName?: string | null
  refundReference?: string | null
  refundedAt?: string | null
  refundedByName?: string | null
  items: { orderItemId: string; productName: string; variantLabel: string; sku: string; quantity: number; unitPrice: number }[]
  availableActions: RefundAction[]
}
const refunds = (club: string) => `${base(club)}/refunds`
export function listRefunds(club: string, params: { status?: string; keyword?: string; page?: number; pageSize?: number }): Promise<PagedResult<RefundListItemDto>> {
  return apiRequest<PagedResult<RefundListItemDto>>(`${refunds(club)}${buildQuery(params)}`)
}
export function getRefund(club: string, id: string): Promise<RefundDetailDto> {
  return apiRequest<RefundDetailDto>(`${refunds(club)}/${id}`)
}
export function createRefund(
  club: string,
  body: { orderId: string; reason: string; items: { orderItemId: string; quantity: number }[]; refundAmount?: number | null; needsReturn?: boolean },
): Promise<RefundDetailDto> {
  return apiRequest<RefundDetailDto>(refunds(club), { method: 'POST', body })
}
export function approveRefund(club: string, id: string, note?: string): Promise<RefundDetailDto> {
  return apiRequest<RefundDetailDto>(`${refunds(club)}/${id}/approve`, { method: 'POST', body: { note } })
}
export function rejectRefund(club: string, id: string, note: string): Promise<RefundDetailDto> {
  return apiRequest<RefundDetailDto>(`${refunds(club)}/${id}/reject`, { method: 'POST', body: { note } })
}
export function receiveRefund(club: string, id: string, body: { restock: boolean; note?: string }): Promise<RefundDetailDto> {
  return apiRequest<RefundDetailDto>(`${refunds(club)}/${id}/receive`, { method: 'POST', body })
}
export function executeRefund(club: string, id: string, note?: string): Promise<{ refund: RefundDetailDto; invoiceAction?: string | null }> {
  return apiRequest(`${refunds(club)}/${id}/execute`, { method: 'POST', body: { note } })
}

// ══════════ S6 設定、憑證、報表、發票捐贈碼 ══════════
export interface ShopLocaleText {
  zh?: string | null
  en?: string | null
}
export interface ShopSettingsDto {
  collectingSubject: { clubId: string; name: string; notice: string }
  shippingFee: number
  freeShippingThreshold?: number | null
  excludedRegions: string[]
  lowStockThreshold: number
  pendingTimeoutMinutes: number
  entryTitle: ShopLocaleText
  entryIntro: ShopLocaleText
  policyNotice: ShopLocaleText
  policyShipping: ShopLocaleText
  policyReturns: ShopLocaleText
  policyTerms: ShopLocaleText
  updatedAt?: string | null
}
export type SaveShopSettingsPayload = Omit<ShopSettingsDto, 'collectingSubject' | 'updatedAt'>
export function getShopSettings(club: string): Promise<ShopSettingsDto> {
  return apiRequest<ShopSettingsDto>(`${base(club)}/settings`)
}
export function saveShopSettings(club: string, body: SaveShopSettingsPayload): Promise<ShopSettingsDto> {
  return apiRequest<ShopSettingsDto>(`${base(club)}/settings`, { method: 'PUT', body })
}

export type ShopEnvironment = 'sandbox' | 'production'
export interface CredentialEnvDto {
  configured: boolean
  identifierMasked?: string | null
  invoicePrefix?: string | null
  rotatedAt?: string | null
}
export interface ShopCredentialsDto {
  collectingSubject: { clubId?: string; name?: string; notice?: string } | string
  environment: ShopEnvironment
  linePay: Record<ShopEnvironment, CredentialEnvDto>
  eInvoice: Record<ShopEnvironment, CredentialEnvDto>
  invoiceRetry: { maxRetries: number; intervalMinutes: number }
  integrationConnected: boolean
}
const credentials = (club: string) => `${base(club)}/credentials`
export function getShopCredentials(club: string): Promise<ShopCredentialsDto> {
  return apiRequest<ShopCredentialsDto>(credentials(club))
}
export function saveLinePayCredential(club: string, body: { environment: ShopEnvironment; channelId: string; channelSecret?: string }): Promise<ShopCredentialsDto> {
  return apiRequest<ShopCredentialsDto>(`${credentials(club)}/linepay`, { method: 'PUT', body })
}
export function saveEInvoiceCredential(
  club: string,
  body: { environment: ShopEnvironment; merchantId?: string; apiKey?: string; invoicePrefix: string },
): Promise<ShopCredentialsDto> {
  return apiRequest<ShopCredentialsDto>(`${credentials(club)}/einvoice`, { method: 'PUT', body })
}
export function saveShopMode(club: string, environment: ShopEnvironment): Promise<ShopCredentialsDto> {
  return apiRequest<ShopCredentialsDto>(`${credentials(club)}/mode`, { method: 'PUT', body: { environment } })
}
export function saveInvoiceRetry(club: string, body: { maxRetries: number; intervalMinutes: number }): Promise<ShopCredentialsDto> {
  return apiRequest<ShopCredentialsDto>(`${credentials(club)}/invoice-retry`, { method: 'PUT', body })
}

export interface ReportSummaryDto {
  from: string
  to: string
  grossRevenue: number
  refundedAmount: number
  netRevenue: number
  orderCount: number
  averageOrderValue: number
  returnOrderCount: number
  returnRatePercent: number
  topSkus: { sku: string; productName: string; variantLabel: string; quantity: number; revenue: number }[]
  lowStockCount: number
  outOfStockCount: number
  totalAvailableQuantity: number
}
export interface ReportBySellingClubDto {
  sellingClubId: string
  sellingClubCode: string
  sellingClubName: string
  orderCount: number
  grossRevenue: number
  refundedAmount: number
  netRevenue: number
  settledAmount: number
  pendingSettlementAmount: number
}
export function getReportSummary(club: string, from?: string, to?: string): Promise<ReportSummaryDto> {
  return apiRequest<ReportSummaryDto>(`${base(club)}/reports/summary${buildQuery({ from, to })}`)
}
export function getReportBySellingClub(club: string, from?: string, to?: string): Promise<ReportBySellingClubDto[]> {
  return apiRequest<ReportBySellingClubDto[]>(`${base(club)}/reports/by-selling-club${buildQuery({ from, to })}`)
}
export function exportReport(club: string, kind: 'summary' | 'by-selling-club', from: string | undefined, to: string | undefined, purpose: string): Promise<void> {
  return downloadExport(`${base(club)}/reports/export${buildQuery({ kind, from, to, purpose })}`, '商店報表.csv')
}

export interface DonationCodeDto {
  id: string
  code: string
  orgName: string
  isActive: boolean
  sortOrder: number
  updatedAt: string
}
export interface SaveDonationCodePayload {
  code: string
  orgName: string
  isActive?: boolean
  sortOrder?: number
}
const DONATION = '/api/v1/admin/shop/donation-codes'
export function listDonationCodes(): Promise<DonationCodeDto[]> {
  return apiRequest<DonationCodeDto[]>(DONATION)
}
export function createDonationCode(body: SaveDonationCodePayload): Promise<DonationCodeDto> {
  return apiRequest<DonationCodeDto>(DONATION, { method: 'POST', body })
}
export function updateDonationCode(id: string, body: SaveDonationCodePayload): Promise<DonationCodeDto> {
  return apiRequest<DonationCodeDto>(`${DONATION}/${id}`, { method: 'PUT', body })
}
export function deleteDonationCode(id: string): Promise<void> {
  return apiRequest<void>(`${DONATION}/${id}`, { method: 'DELETE' })
}
