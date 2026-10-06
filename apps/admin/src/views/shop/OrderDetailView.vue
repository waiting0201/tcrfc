<script setup lang="ts">
/**
 * 訂單詳情：狀態動作（備貨、出貨、完成、取消、申請退貨）、物流、備註與分帳標記。
 * 按鈕依系統回報的「目前可執行的動作」顯示；每個狀態動作都是帶前置狀態的單次更新，
 * 狀態不對（例如另一位同事剛處理過）會被擋下並顯示目前狀態，不會重複扣庫存或重複退款。
 * 收件人視同會員個資：有「檢視完整個資」權限的角色直接看到完整值，其餘遮罩。
 * 🔴 電子發票尚未串接，這裡不會開立發票；顯示的發票資訊只是已登錄的紀錄。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import RefundCreateDialog from './parts/RefundCreateDialog.vue'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import {
  cancelOrder,
  completeOrder,
  getOrder,
  markArrivalNotified,
  prepareOrder,
  saveOrderNote,
  saveOrderSettlement,
  shipOrder,
  updateShipment,
  type OrderDetailDto,
  type ShipOrderPayload,
} from '@/api/adminShop'
import { formatDateTime } from '@/utils/dateTime'
import { formatMoney } from '@/utils/formatMoney'
import { orderStatusTag, refundStatusTag } from '@/utils/shopStatus'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const route = useRoute()
const router = useRouter()
const orderId = computed(() => route.params.id as string)
const canUpdateOrder = usePermission('shop.order.update')
const canUpdateShipment = usePermission('shop.shipment.update')
const canRequestRefund = usePermission('shop.refund.update')
const canViewRefund = usePermission('shop.refund.view')
const club = computed(() => activeClubId.value)

const order = ref<OrderDetailDto | null>(null)
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const acting = ref(false)

async function load() {
  loadState.value = 'loading'
  try {
    setOrder(await getOrder(club.value, orderId.value))
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') loadState.value = 'not-found'
    else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '訂單載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
function setOrder(o: OrderDetailDto) {
  order.value = o
  noteText.value = o.internalNote ?? ''
  Object.assign(settle, { status: o.settlementStatus === 'settled' ? 'settled' : 'pending', settledOn: o.settledOn ?? '', note: o.settlementNote ?? '' })
}
onMounted(load)
watch(club, () => router.push('/shop/orders'))
watch(orderId, load)

const has = (action: string) => !!order.value?.availableActions.includes(action as never)

/** 執行狀態動作：成功後以回應（最新訂單詳情）更新畫面；失敗顯示後端說明後重新載入。 */
async function act(fn: () => Promise<OrderDetailDto>, success: string) {
  acting.value = true
  try {
    setOrder(await fn())
    ElMessage.success(success)
    return true
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return false
    ElMessage.error(error instanceof AdminApiError ? error.message : '操作失敗，請稍後再試')
    await load()
    return false
  } finally {
    acting.value = false
  }
}

async function doPrepare() {
  await act(() => prepareOrder(club.value, orderId.value), '已標為備貨中')
}
async function doComplete() {
  try {
    await ElMessageBox.confirm(order.value?.deliveryMethod === 'home_delivery' ? '確定要把這張訂單標為已完成嗎？' : '確定顧客已領取，並把訂單標為已完成嗎？', '完成訂單', { confirmButtonText: '確定', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  await act(() => completeOrder(club.value, orderId.value), '訂單已完成')
}

// ── 取消 ──
const cancelOpen = ref(false)
const cancelReason = ref('')
const cancelError = ref<string | null>(null)
async function doCancel() {
  if (!cancelReason.value.trim()) return (cancelError.value = '請填寫取消原因')
  cancelError.value = null
  const ok = await act(() => cancelOrder(club.value, orderId.value, cancelReason.value.trim()), '訂單已取消')
  if (ok) cancelOpen.value = false
}

// ── 出貨／更正物流 ──
const shipOpen = ref(false)
const shipMode = ref<'ship' | 'edit'>('ship')
const shipForm = reactive({ carrier: '', trackingNo: '', storeBranchCode: '', pickupDeadlineOn: '' })
const shipError = ref<string | null>(null)
function openShip(mode: 'ship' | 'edit') {
  shipMode.value = mode
  const s = order.value?.shipment
  Object.assign(shipForm, {
    carrier: mode === 'edit' ? s?.carrier ?? '' : '',
    trackingNo: mode === 'edit' ? s?.trackingNo ?? '' : '',
    storeBranchCode: mode === 'edit' ? s?.storeBranchCode ?? '' : '',
    pickupDeadlineOn: mode === 'edit' ? s?.pickupDeadlineOn ?? '' : '',
  })
  shipError.value = null
  shipOpen.value = true
}
async function saveShip() {
  const o = order.value
  if (!o) return
  if (o.deliveryMethod === 'cvs_pickup' && !shipForm.storeBranchCode.trim()) return (shipError.value = '超商取貨必須填寫門市代碼')
  shipError.value = null
  const body: ShipOrderPayload = {
    carrier: nullIfBlank(shipForm.carrier),
    trackingNo: nullIfBlank(shipForm.trackingNo),
    storeBranchCode: nullIfBlank(shipForm.storeBranchCode),
    pickupDeadlineOn: shipForm.pickupDeadlineOn || null,
  }
  const ok = await act(
    () => (shipMode.value === 'ship' ? shipOrder(club.value, orderId.value, body) : updateShipment(club.value, orderId.value, body)),
    shipMode.value === 'ship' ? (o.deliveryMethod === 'onsite_pickup' ? '已備妥待領' : '已標為已出貨') : '物流資料已更新',
  )
  if (ok) shipOpen.value = false
}
async function doArrival() {
  await act(() => markArrivalNotified(club.value, orderId.value, order.value?.shipment?.pickupDeadlineOn ?? null), '已記錄到店通知時間')
}

// ── 備註 ──
const noteText = ref('')
const noteSaving = ref(false)
async function saveNote() {
  noteSaving.value = true
  try {
    setOrder(await saveOrderNote(club.value, orderId.value, noteText.value.trim()))
    ElMessage.success('備註已儲存')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試')
  } finally {
    noteSaving.value = false
  }
}

// ── 分帳標記 ──
const settle = reactive({ status: 'pending', settledOn: '', note: '' })
const settleSaving = ref(false)
async function saveSettle() {
  settleSaving.value = true
  try {
    setOrder(await saveOrderSettlement(club.value, orderId.value, { status: settle.status, settledOn: settle.status === 'settled' ? settle.settledOn || null : null, note: nullIfBlank(settle.note) }))
    ElMessage.success('分帳標記已儲存')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試')
  } finally {
    settleSaving.value = false
  }
}

// ── 申請退貨 ──
const refundOpen = ref(false)
function onRefundCreated(id: string | null) {
  if (id) router.push(`/shop/returns/${id}`)
  else load()
}
</script>

<template>
  <div class="order-detail">
    <PageHeader :title="order ? `訂單 ${order.orderNo}` : '訂單'">
      <template #back><el-button text @click="router.push('/shop/orders')"><el-icon><ArrowLeft /></el-icon>返回訂單</el-button></template>
      <template #meta><FrontendUnitBanner module-code="S3" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready' || !order" shadow="never">
      <el-empty :image-size="96" :description="loadState === 'not-found' ? '找不到這張訂單，可能不屬於目前選擇的俱樂部。' : loadErrorMessage">
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="router.push('/shop/orders')">返回訂單</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-card shadow="never" class="order-detail__block">
        <div class="order-detail__head">
          <div>
            <el-tag :type="orderStatusTag(order.orderStatus)" size="large">{{ order.orderStatus }}</el-tag>
            <el-tag v-if="order.isManual" type="info" class="order-detail__tag">手動建單</el-tag>
            <div class="order-detail__muted">下單 {{ formatDateTime(order.createdAt) }}<template v-if="order.paidAt">・付款 {{ formatDateTime(order.paidAt) }}</template><template v-if="order.completedAt">・完成 {{ formatDateTime(order.completedAt) }}</template></div>
            <div v-if="order.cancelledAt" class="order-detail__muted">取消 {{ formatDateTime(order.cancelledAt) }}：{{ order.cancelReason || '（未填原因）' }}</div>
          </div>
          <div class="order-detail__actions">
            <el-button v-if="has('prepare') && canUpdateOrder" :loading="acting" @click="doPrepare">標為備貨中</el-button>
            <el-button v-if="has('ship') && canUpdateShipment" type="primary" @click="openShip('ship')">{{ order.deliveryMethod === 'onsite_pickup' ? '備妥待領' : '標為已出貨' }}</el-button>
            <el-button v-if="has('complete') && canUpdateShipment" type="success" :loading="acting" @click="doComplete">標為已完成</el-button>
            <el-button v-if="has('request_refund') && canRequestRefund" @click="refundOpen = true">申請退貨退款</el-button>
            <el-button v-if="has('cancel') && canUpdateOrder" type="danger" plain @click="cancelReason = ''; cancelError = null; cancelOpen = true">取消訂單</el-button>
          </div>
        </div>
        <p v-if="order.orderStatus === '已出貨'" class="order-detail__hint">已出貨的訂單不能直接取消，如需退貨請用「申請退貨退款」。</p>
      </el-card>

      <el-row :gutter="16">
        <el-col :xs="24" :md="12">
          <el-card shadow="never" header="訂購資訊" class="order-detail__block">
            <dl class="order-detail__dl">
              <div><dt>販售俱樂部</dt><dd>{{ order.sellingClubName }}</dd></div>
              <div v-if="order.collectingClubName"><dt>收款主體</dt><dd>{{ order.collectingClubName }}</dd></div>
              <div><dt>買家</dt><dd>{{ order.isMember ? `會員 ${order.memberNo || ''}` : '非會員' }}</dd></div>
              <div v-if="order.buyerEmail"><dt>買家 Email</dt><dd>{{ order.buyerEmail }}</dd></div>
              <div><dt>付款方式</dt><dd>{{ order.paymentMethodLabel }}</dd></div>
              <div><dt>付款狀態</dt><dd>{{ order.paymentStatusLabel }}</dd></div>
              <div v-if="order.linepayTransactionId"><dt>金流交易編號</dt><dd>{{ order.linepayTransactionId }}</dd></div>
              <div><dt>配送方式</dt><dd>{{ order.deliveryMethodLabel }}（{{ order.shipmentStatusLabel }}）</dd></div>
            </dl>
          </el-card>
        </el-col>
        <el-col :xs="24" :md="12">
          <el-card shadow="never" class="order-detail__block">
            <template #header>
              <span>收件人</span>
              <el-tag v-if="order.isMasked" size="small" type="info" class="order-detail__tag">已遮罩</el-tag>
            </template>
            <dl class="order-detail__dl">
              <div><dt>姓名</dt><dd>{{ order.recipientName || '—' }}</dd></div>
              <div><dt>電話</dt><dd>{{ order.recipientPhone || '—' }}</dd></div>
              <div><dt>地址</dt><dd>{{ order.recipientAddress || '—' }}</dd></div>
              <div><dt>顧客備註</dt><dd>{{ order.customerNote || '—' }}</dd></div>
            </dl>
            <p v-if="order.isMasked" class="order-detail__hint">你的帳號沒有檢視完整個資的權限，收件人、買家 Email 與發票載具號碼顯示為遮罩。</p>
          </el-card>
        </el-col>
      </el-row>

      <el-card shadow="never" header="訂購品項" class="order-detail__block">
        <el-table :data="order.items" row-key="id">
          <el-table-column label="商品" min-width="180"><template #default="{ row }"><div>{{ row.productName }}</div><div class="order-detail__muted">{{ row.variantLabel }}・{{ row.sku }}</div></template></el-table-column>
          <el-table-column label="單價" width="100"><template #default="{ row }">{{ formatMoney(row.unitPrice) }}</template></el-table-column>
          <el-table-column label="數量" width="70" prop="quantity" />
          <el-table-column label="小計" width="110"><template #default="{ row }">{{ formatMoney(row.lineTotal) }}</template></el-table-column>
          <el-table-column label="已退" width="70"><template #default="{ row }">{{ row.refundedQuantity || '—' }}</template></el-table-column>
        </el-table>
        <div class="order-detail__sum">
          <div>商品小計 {{ formatMoney(order.subtotal) }}</div>
          <div>運費 {{ formatMoney(order.shippingFee) }}</div>
          <div><strong>訂單總額 {{ formatMoney(order.total) }}</strong></div>
        </div>
        <p class="order-detail__hint">品項是下單當下的複本，之後商品改名或改價不會影響這張訂單。</p>
      </el-card>

      <el-card shadow="never" class="order-detail__block">
        <template #header>
          <div class="order-detail__head-row">
            <span>物流</span>
            <el-button v-if="order.shipment && canUpdateShipment" size="small" @click="openShip('edit')">更正物流資料</el-button>
          </div>
        </template>
        <p v-if="!order.shipment" class="order-detail__hint">尚未出貨。</p>
        <dl v-else class="order-detail__dl">
          <div><dt>物流商</dt><dd>{{ order.shipment.carrier || '—' }}</dd></div>
          <div><dt>物流單號</dt><dd>{{ order.shipment.trackingNo || '尚未回填' }}</dd></div>
          <div v-if="order.shipment.storeBranchCode"><dt>門市代碼</dt><dd>{{ order.shipment.storeBranchCode }}</dd></div>
          <div><dt>出貨時間</dt><dd>{{ formatDateTime(order.shipment.shippedAt) || '—' }}</dd></div>
          <div v-if="order.shipment.pickupStatusLabel"><dt>領取狀態</dt><dd>{{ order.shipment.pickupStatusLabel }}<template v-if="order.shipment.pickupDeadlineOn">（領取期限 {{ order.shipment.pickupDeadlineOn }}）</template></dd></div>
          <div v-if="order.shipment.arrivalNotifiedAt"><dt>到店通知</dt><dd>{{ formatDateTime(order.shipment.arrivalNotifiedAt) }}</dd></div>
        </dl>
        <template v-if="order.shipment && order.deliveryMethod === 'cvs_pickup' && order.orderStatus === '已出貨' && canUpdateShipment">
          <el-button size="small" :loading="acting" :disabled="!!order.shipment.arrivalNotifiedAt" @click="doArrival">記錄到店通知</el-button>
          <p class="order-detail__hint">只會記錄時間並進入待領取，本系統不會寄任何通知給顧客。</p>
        </template>
      </el-card>

      <el-row :gutter="16">
        <el-col :xs="24" :md="12">
          <el-card shadow="never" header="發票" class="order-detail__block">
            <p v-if="!order.invoice" class="order-detail__hint">尚無發票紀錄。電子發票尚未串接（取得商店號後啟用），目前不會自動開立。</p>
            <dl v-else class="order-detail__dl">
              <div><dt>發票號碼</dt><dd>{{ order.invoice.invoiceNo || '—' }}</dd></div>
              <div><dt>開立時間</dt><dd>{{ formatDateTime(order.invoice.issuedAt) || '—' }}</dd></div>
              <div><dt>開立狀態</dt><dd>{{ order.invoice.issueStatusLabel || order.invoice.issueStatus || '—' }}</dd></div>
              <div><dt>作廢狀態</dt><dd>{{ order.invoice.voidStatusLabel || order.invoice.voidStatus || '—' }}</dd></div>
              <div v-if="order.invoice.typeLabel"><dt>開立方式</dt><dd>{{ order.invoice.typeLabel }}</dd></div>
              <div v-if="order.invoice.carrierId"><dt>載具號碼</dt><dd>{{ order.invoice.carrierId }}</dd></div>
              <div v-if="order.invoice.taxId"><dt>統一編號</dt><dd>{{ order.invoice.taxId }}</dd></div>
              <div v-if="order.invoice.donationCode"><dt>捐贈碼</dt><dd>{{ order.invoice.donationCode }}</dd></div>
            </dl>
          </el-card>
        </el-col>
        <el-col :xs="24" :md="12">
          <el-card shadow="never" header="退貨與退款案件" class="order-detail__block">
            <p v-if="order.refunds.length === 0" class="order-detail__hint">這張訂單沒有退貨或退款案件。</p>
            <div v-else class="order-detail__refunds">
              <div v-for="r in order.refunds" :key="r.id" class="order-detail__refund">
                <div>
                  <el-tag :type="refundStatusTag(r.status)" size="small">{{ r.statusLabel }}</el-tag>
                  <strong class="order-detail__refund-amount">{{ formatMoney(r.refundAmount) }}</strong>
                  <div class="order-detail__muted">{{ r.reason }}・{{ formatDateTime(r.createdAt) }}</div>
                </div>
                <el-button v-if="canViewRefund" size="small" text type="primary" @click="router.push(`/shop/returns/${r.id}`)">查看</el-button>
              </div>
            </div>
          </el-card>
        </el-col>
      </el-row>

      <el-card shadow="never" header="內部備註" class="order-detail__block">
        <el-input v-model="noteText" type="textarea" :rows="3" maxlength="500" show-word-limit :disabled="!canUpdateOrder" placeholder="只有後台看得到，清空並儲存即清除" />
        <el-button v-if="canUpdateOrder" type="primary" :loading="noteSaving" class="order-detail__save" @click="saveNote">儲存備註</el-button>
      </el-card>

      <el-card shadow="never" header="兩隊分帳標記" class="order-detail__block">
        <p class="order-detail__hint">藍鯨的訂單由磐石代收。這只是給兩隊線下分帳用的人工旗標（不是狀態流程），系統不計算應付金額、不產生結算單；尚未付款的訂單不能標記。</p>
        <el-form label-position="top" :disabled="!canUpdateOrder">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="8"><el-form-item label="標記"><el-radio-group v-model="settle.status"><el-radio-button value="pending">待結算</el-radio-button><el-radio-button value="settled">已結算</el-radio-button></el-radio-group></el-form-item></el-col>
            <el-col :xs="24" :sm="8"><el-form-item v-if="settle.status === 'settled'" label="結算日（沒填就用今天）"><el-date-picker v-model="settle.settledOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
            <el-col :xs="24" :sm="8"><el-form-item label="備註"><el-input v-model="settle.note" maxlength="200" /></el-form-item></el-col>
          </el-row>
        </el-form>
        <el-button v-if="canUpdateOrder" :loading="settleSaving" @click="saveSettle">儲存分帳標記</el-button>
      </el-card>
    </template>

    <el-dialog v-model="cancelOpen" title="取消訂單" width="460px" :close-on-click-modal="false" class="order-detail__dialog">
      <el-alert v-if="cancelError" :title="cancelError" type="warning" show-icon class="order-detail__block" @close="cancelError = null" />
      <p class="order-detail__hint">待付款的訂單會釋回保留的庫存；已付款或備貨中的訂單會回補庫存，並自動建立一張「已核准、不需退回商品」的全額退款案件，等系統管理員執行退款。</p>
      <el-form label-position="top"><el-form-item label="取消原因" required><el-input v-model="cancelReason" type="textarea" :rows="3" maxlength="200" show-word-limit /></el-form-item></el-form>
      <template #footer>
        <el-button @click="cancelOpen = false">先不要</el-button>
        <el-button type="danger" :loading="acting" @click="doCancel">確定取消訂單</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="shipOpen" :title="shipMode === 'ship' ? (order?.deliveryMethod === 'onsite_pickup' ? '備妥待領' : '標為已出貨') : '更正物流資料'" width="480px" :close-on-click-modal="false" class="order-detail__dialog">
      <el-alert v-if="shipError" :title="shipError" type="warning" show-icon class="order-detail__block" @close="shipError = null" />
      <el-form label-position="top">
        <template v-if="order?.deliveryMethod !== 'onsite_pickup'">
          <FormField field="carrier" label="物流商"><el-input v-model="shipForm.carrier" maxlength="64" placeholder="例如 黑貓宅急便" /></FormField>
          <FormField field="trackingNo" label="物流單號"><el-input v-model="shipForm.trackingNo" maxlength="64" placeholder="可以先出貨、之後再回填" /></FormField>
        </template>
        <FormField v-if="order?.deliveryMethod === 'cvs_pickup'" field="storeBranchCode" label="門市代碼" required><el-input v-model="shipForm.storeBranchCode" maxlength="32" /></FormField>
        <el-form-item v-if="order?.deliveryMethod === 'onsite_pickup'" label="領取期限（選填）"><el-date-picker v-model="shipForm.pickupDeadlineOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
      </el-form>
      <p v-if="shipMode === 'edit'" class="order-detail__hint">更正會整份取代物流商、物流單號、門市代碼與領取期限四個欄位。</p>
      <template #footer>
        <el-button @click="shipOpen = false">取消</el-button>
        <el-button type="primary" :loading="acting" @click="saveShip">確定</el-button>
      </template>
    </el-dialog>

    <RefundCreateDialog v-model="refundOpen" :order-id="orderId" @created="onRefundCreated" />
  </div>
</template>

<style scoped>
.order-detail { max-width: 1040px; margin: 0 auto 32px; }
.order-detail__block { margin-bottom: 16px; }
.order-detail__head { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 12px; }
.order-detail__head-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.order-detail__actions { display: flex; flex-wrap: wrap; gap: 8px; }
.order-detail__tag { margin-left: 8px; }
.order-detail__muted { margin-top: 4px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.order-detail__hint { margin: 6px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.order-detail__dl { margin: 0; display: flex; flex-direction: column; gap: 6px; font-size: 14px; }
.order-detail__dl > div { display: flex; gap: 8px; min-width: 0; }
.order-detail__dl dt { width: 90px; flex-shrink: 0; color: var(--admin-text-secondary); }
.order-detail__dl dd { margin: 0; min-width: 0; word-break: break-word; }
.order-detail__sum { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; margin-top: 12px; font-size: 14px; }
.order-detail__refunds { display: flex; flex-direction: column; gap: 8px; }
.order-detail__refund { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 8px 10px; border: 1px solid var(--admin-border); border-radius: 4px; }
.order-detail__refund-amount { margin-left: 8px; }
.order-detail__save { margin-top: 10px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
