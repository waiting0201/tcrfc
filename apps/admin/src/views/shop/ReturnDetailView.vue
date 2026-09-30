<script setup lang="ts">
/**
 * 退貨案件詳情：審核（核准／駁回）、收貨驗收、執行退款。
 * - 執行退款只有系統管理員能做；並行送出只有一個會成功，不會重複退款。
 * - LINE Pay 訂單的原路退回尚未串接：執行會被擋下，案件維持原狀、不會留下已退款的痕跡；現場收款的訂單以人工退款登錄經辦人。
 * - 已開立的發票會同步登記作廢（全額）或折讓（部分）；電子發票服務尚未串接時，請依畫面說明到發票服務端手動處理。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { approveRefund, executeRefund, getRefund, receiveRefund, rejectRefund, type RefundDetailDto } from '@/api/adminShop'
import { formatUtcDateTime } from '@/utils/formatDateTime'
import { formatMoney } from '@/utils/formatMoney'
import { orderStatusTag, refundStatusTag } from '@/utils/shopStatus'

const route = useRoute()
const router = useRouter()
const refundId = computed(() => route.params.id as string)
const canUpdate = usePermission('shop.refund.update')
const canExecute = usePermission('shop.refund.execute')
const club = computed(() => activeClubId.value)

const refund = ref<RefundDetailDto | null>(null)
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const acting = ref(false)
const invoiceAction = ref<string | null>(null)

async function load() {
  loadState.value = 'loading'
  try {
    refund.value = await getRefund(club.value, refundId.value)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') loadState.value = 'not-found'
    else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '案件載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
onMounted(load)
watch(club, () => router.push('/shop/returns'))
watch(refundId, load)

const has = (action: string) => !!refund.value?.availableActions.includes(action as never)

/** 動作完成或失敗後一律重新讀取案件（各動作回應形狀契約未逐一寫明，不依賴回應內容）。 */
async function act(fn: () => Promise<unknown>, success: string): Promise<boolean> {
  acting.value = true
  try {
    await fn()
    ElMessage.success(success)
    return true
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '操作失敗，請稍後再試')
    return false
  } finally {
    acting.value = false
    await load()
  }
}

// ── 核准 ──
const approveOpen = ref(false)
const approveNote = ref('')
async function doApprove() {
  if (await act(() => approveRefund(club.value, refundId.value, approveNote.value.trim() || undefined), '已核准')) approveOpen.value = false
}
// ── 駁回 ──
const rejectOpen = ref(false)
const rejectNote = ref('')
const rejectError = ref<string | null>(null)
async function doReject() {
  if (!rejectNote.value.trim()) return (rejectError.value = '駁回必須填寫理由')
  rejectError.value = null
  if (await act(() => rejectRefund(club.value, refundId.value, rejectNote.value.trim()), '已駁回')) rejectOpen.value = false
}
// ── 收貨驗收 ──
const receiveOpen = ref(false)
const receiveForm = ref({ restock: true, note: '' })
async function doReceive() {
  if (await act(() => receiveRefund(club.value, refundId.value, { restock: receiveForm.value.restock, note: receiveForm.value.note.trim() || undefined }), receiveForm.value.restock ? '已驗收，商品已回補庫存' : '已驗收（商品不回補庫存）')) receiveOpen.value = false
}
// ── 執行退款 ──
async function doExecute() {
  const r = refund.value
  if (!r) return
  try {
    await ElMessageBox.confirm(
      `即將執行退款 ${formatMoney(r.refundAmount)}（訂單 ${r.orderNo}）。這個動作無法復原，請確認金額與案件內容無誤。`,
      '執行退款',
      { confirmButtonText: '確定執行退款', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  invoiceAction.value = null
  acting.value = true
  try {
    const result = await executeRefund(club.value, refundId.value)
    invoiceAction.value = result?.invoiceAction ?? null
    ElMessage.success('退款已完成')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '退款執行失敗，請稍後再試')
  } finally {
    acting.value = false
    await load()
  }
}

const steps = computed(() => {
  const r = refund.value
  if (!r) return { list: [] as string[], active: 0 }
  const list = r.needsReturn ? ['申請中', '已核准', '已驗收退回品', '已退款'] : ['申請中', '已核准', '已退款']
  const order: Record<string, number> = r.needsReturn
    ? { requested: 0, approved: 1, received: 2, processing: 3, refunded: 4 }
    : { requested: 0, approved: 1, processing: 2, refunded: 3 }
  return { list, active: order[r.status] ?? 0 }
})
</script>

<template>
  <div class="refund-detail">
    <PageHeader :title="refund ? `退貨案件 ${refund.orderNo}` : '退貨案件'">
      <template #back><el-button text @click="router.push('/shop/returns')"><el-icon><ArrowLeft /></el-icon>返回案件清單</el-button></template>
      <template #meta><FrontendUnitBanner module-code="S5" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading' && !refund" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="!refund" shadow="never">
      <el-empty :image-size="96" :description="loadState === 'not-found' ? '找不到這個案件，可能不屬於目前選擇的俱樂部。' : loadErrorMessage">
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="router.push('/shop/returns')">返回案件清單</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="invoiceAction" :title="invoiceAction" type="warning" show-icon class="refund-detail__block" @close="invoiceAction = null" />

      <el-card shadow="never" class="refund-detail__block">
        <div class="refund-detail__head">
          <div>
            <el-tag :type="refundStatusTag(refund.status)" size="large">{{ refund.statusLabel }}</el-tag>
            <strong class="refund-detail__amount">{{ formatMoney(refund.refundAmount) }}</strong>
            <div class="refund-detail__muted">申請 {{ formatUtcDateTime(refund.createdAt) }}・{{ refund.needsReturn ? '需要退回商品' : '不需退回商品' }}</div>
          </div>
          <div class="refund-detail__actions">
            <template v-if="canUpdate">
              <el-button v-if="has('approve')" type="primary" :loading="acting" @click="approveNote = ''; approveOpen = true">核准</el-button>
              <el-button v-if="has('reject')" type="danger" plain @click="rejectNote = ''; rejectError = null; rejectOpen = true">駁回</el-button>
              <el-button v-if="has('receive')" type="primary" @click="receiveForm = { restock: true, note: '' }; receiveOpen = true">收貨驗收</el-button>
            </template>
            <el-button v-if="has('execute') && canExecute" type="danger" :loading="acting" @click="doExecute">執行退款</el-button>
          </div>
        </div>
        <p v-if="has('execute') && !canExecute" class="refund-detail__hint">這個案件已可以退款，但「執行退款」只有系統管理員能做。</p>
        <p v-if="has('execute')" class="refund-detail__hint">LINE Pay 訂單的原路退回尚未串接，執行時會被擋下、案件維持原狀；現場收款的訂單以人工退款登錄經辦人。</p>
        <el-steps v-if="refund.status !== 'rejected'" :active="steps.active" finish-status="success" align-center class="refund-detail__steps">
          <el-step v-for="s in steps.list" :key="s" :title="s" />
        </el-steps>
        <p v-else class="refund-detail__hint">這個案件已駁回：{{ refund.reviewNote || '（未填理由）' }}</p>
      </el-card>

      <el-row :gutter="16">
        <el-col :xs="24" :md="12">
          <el-card shadow="never" header="案件資訊" class="refund-detail__block">
            <dl class="refund-detail__dl">
              <div><dt>訂單</dt><dd><el-button text type="primary" class="refund-detail__link" @click="router.push(`/shop/orders/${refund.orderId}`)">{{ refund.orderNo }}</el-button><el-tag :type="orderStatusTag(refund.orderStatus)" size="small">{{ refund.orderStatus }}</el-tag></dd></div>
              <div><dt>訂單總額</dt><dd>{{ formatMoney(refund.orderTotal) }}（已退累計 {{ formatMoney(refund.orderRefundedTotal) }}）</dd></div>
              <div><dt>付款方式</dt><dd>{{ refund.paymentMethodLabel }}</dd></div>
              <div><dt>退貨原因</dt><dd>{{ refund.reason }}</dd></div>
              <div v-if="refund.reviewNote"><dt>審核意見</dt><dd>{{ refund.reviewNote }}</dd></div>
              <div v-if="refund.approvedByName"><dt>核准人</dt><dd>{{ refund.approvedByName }}</dd></div>
            </dl>
          </el-card>
        </el-col>
        <el-col :xs="24" :md="12">
          <el-card shadow="never" header="處理紀錄" class="refund-detail__block">
            <dl class="refund-detail__dl">
              <div><dt>收貨驗收</dt><dd>{{ refund.receivedAt ? `${formatUtcDateTime(refund.receivedAt)}（${refund.receivedByName || '—'}）` : refund.needsReturn ? '尚未驗收' : '不需退回' }}</dd></div>
              <div><dt>退款方式</dt><dd>{{ refund.refundMethodLabel || '—' }}</dd></div>
              <div><dt>退款時間</dt><dd>{{ refund.refundedAt ? `${formatUtcDateTime(refund.refundedAt)}（${refund.refundedByName || '—'}）` : '尚未退款' }}</dd></div>
              <div v-if="refund.refundReference"><dt>退款序號</dt><dd>{{ refund.refundReference }}</dd></div>
            </dl>
          </el-card>
        </el-col>
      </el-row>

      <el-card shadow="never" header="退貨品項" class="refund-detail__block">
        <el-table :data="refund.items" row-key="orderItemId">
          <el-table-column label="商品" min-width="180"><template #default="{ row }"><div>{{ row.productName }}</div><div class="refund-detail__muted">{{ row.variantLabel }}・{{ row.sku }}</div></template></el-table-column>
          <el-table-column label="單價" width="110"><template #default="{ row }">{{ formatMoney(row.unitPrice) }}</template></el-table-column>
          <el-table-column label="退貨數量" width="90" prop="quantity" />
        </el-table>
      </el-card>
    </template>

    <el-dialog v-model="approveOpen" title="核准案件" width="440px" :close-on-click-modal="false" class="refund-detail__dialog">
      <p class="refund-detail__hint">{{ refund?.needsReturn ? '核准後請等顧客退回商品，收到後到「收貨驗收」確認。' : '這個案件不需退回商品，核准後就可以由系統管理員執行退款。' }}</p>
      <el-input v-model="approveNote" type="textarea" :rows="3" maxlength="200" show-word-limit placeholder="審核意見（選填）" />
      <template #footer><el-button @click="approveOpen = false">取消</el-button><el-button type="primary" :loading="acting" @click="doApprove">確定核准</el-button></template>
    </el-dialog>
    <el-dialog v-model="rejectOpen" title="駁回案件" width="440px" :close-on-click-modal="false" class="refund-detail__dialog">
      <el-alert v-if="rejectError" :title="rejectError" type="warning" show-icon class="refund-detail__block" @close="rejectError = null" />
      <p class="refund-detail__hint">駁回後訂單會回到「已完成」或「已出貨」（沒有其他處理中案件時）。</p>
      <el-input v-model="rejectNote" type="textarea" :rows="3" maxlength="200" show-word-limit placeholder="駁回理由（必填）" />
      <template #footer><el-button @click="rejectOpen = false">取消</el-button><el-button type="danger" :loading="acting" @click="doReject">確定駁回</el-button></template>
    </el-dialog>
    <el-dialog v-model="receiveOpen" title="收貨驗收" width="460px" :close-on-click-modal="false" class="refund-detail__dialog">
      <el-form label-position="top">
        <el-form-item label="退回商品的處理"><el-switch v-model="receiveForm.restock" active-text="可再販售，回補庫存" inactive-text="損毀無法再賣，不回補庫存" /></el-form-item>
        <el-form-item label="驗收備註（選填）"><el-input v-model="receiveForm.note" type="textarea" :rows="2" maxlength="200" show-word-limit /></el-form-item>
      </el-form>
      <template #footer><el-button @click="receiveOpen = false">取消</el-button><el-button type="primary" :loading="acting" @click="doReceive">確認已收到</el-button></template>
    </el-dialog>
  </div>
</template>

<style scoped>
.refund-detail { max-width: 1000px; margin: 0 auto 32px; }
.refund-detail__block { margin-bottom: 16px; }
.refund-detail__head { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 12px; }
.refund-detail__actions { display: flex; flex-wrap: wrap; gap: 8px; }
.refund-detail__amount { margin-left: 12px; font-size: 18px; }
.refund-detail__muted { margin-top: 4px; font-size: 12px; color: var(--admin-text-tertiary); }
.refund-detail__hint { margin: 8px 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.refund-detail__steps { margin-top: 16px; }
.refund-detail__dl { margin: 0; display: flex; flex-direction: column; gap: 6px; font-size: 14px; }
.refund-detail__dl > div { display: flex; gap: 8px; min-width: 0; align-items: baseline; }
.refund-detail__dl dt { width: 84px; flex-shrink: 0; color: var(--admin-text-secondary); }
.refund-detail__dl dd { margin: 0; min-width: 0; word-break: break-word; }
.refund-detail__link { padding: 0; margin-right: 8px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
