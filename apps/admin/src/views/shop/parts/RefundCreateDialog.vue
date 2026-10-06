<script setup lang="ts">
/**
 * 建立退貨／退款申請（前台以表單或客服信箱收到的申請，由客服在後台建立案件）。
 * 支援部分退款：勾選要退的品項與數量，退款金額預設為所退品項小計，要連運費一起退請自行改金額。
 * 只有已付款且「已出貨／已完成」的訂單可以申請；尚未出貨的請直接到訂單「取消訂單」。
 */
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { createRefund, getOrder, listOrders, type OrderDetailDto, type OrderListItemDto } from '@/api/adminShop'
import { formatMoney } from '@/utils/formatMoney'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const props = defineProps<{ modelValue: boolean; orderId?: string }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: boolean): void; (e: 'created', refundId: string | null): void }>()

const formErrors = provideFormErrors()
const club = computed(() => activeClubId.value)
const order = ref<OrderDetailDto | null>(null)
const loading = ref(false)
const saving = ref(false)
const error = ref<string | null>(null)
const options = ref<OrderListItemDto[]>([])
const searching = ref(false)
const pickedOrderId = ref('')
const qty = reactive<Record<string, number>>({})
const form = reactive({ reason: '', needsReturn: true, overrideAmount: false, refundAmount: 0 })

async function loadOrder(id: string) {
  loading.value = true
  error.value = null
  try {
    order.value = await getOrder(club.value, id)
    for (const key of Object.keys(qty)) delete qty[key]
    for (const item of order.value.items) qty[item.id] = 0
  } catch (e) {
    order.value = null
    error.value = e instanceof AdminApiError ? e.message : '訂單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

watch(
  () => props.modelValue,
  (open) => {
    if (!open) return
    Object.assign(form, { reason: '', needsReturn: true, overrideAmount: false, refundAmount: 0 })
    error.value = null
    formErrors.clearAll()
    options.value = []
    pickedOrderId.value = ''
    order.value = null
    if (props.orderId) loadOrder(props.orderId)
  },
)

async function searchOrders(keyword: string) {
  const k = keyword.trim()
  if (!k) {
    options.value = []
    return
  }
  searching.value = true
  try {
    options.value = (await listOrders(club.value, { keyword: k }, 1, 10)).items.filter((o) => o.orderStatus === '已出貨' || o.orderStatus === '已完成')
  } catch {
    options.value = []
  } finally {
    searching.value = false
  }
}

const refundable = (item: OrderDetailDto['items'][number]) => item.quantity - item.refundedQuantity
const itemsSubtotal = computed(() => (order.value?.items ?? []).reduce((sum, i) => sum + (qty[i.id] ?? 0) * i.unitPrice, 0))
const finalAmount = computed(() => (form.overrideAmount ? form.refundAmount : itemsSubtotal.value))

async function submit() {
  const o = order.value
  if (!o) return
  error.value = null
  formErrors.clearAll()
  const items = o.items.filter((i) => (qty[i.id] ?? 0) > 0).map((i) => ({ orderItemId: i.id, quantity: qty[i.id] }))
  // 欄位鍵與後端一致（items／reason／refundAmount），同一錯誤不論前後端都標在同一格
  const errors: Record<string, string> = {}
  if (items.length === 0) errors.items = '請至少選擇一個要退的品項與數量'
  else if (o.items.some((i) => (qty[i.id] ?? 0) > refundable(i))) errors.items = '退回數量不可超過尚可退的數量'
  if (!form.reason.trim()) errors.reason = '請填寫退貨原因'
  if (form.overrideAmount && (form.refundAmount === null || form.refundAmount <= 0)) errors.refundAmount = '退款金額必須大於 0'
  if (formErrors.replaceAll(errors)) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const created = await createRefund(club.value, {
      orderId: o.id,
      reason: form.reason.trim(),
      items,
      refundAmount: form.overrideAmount ? form.refundAmount : null,
      needsReturn: form.needsReturn,
    })
    ElMessage.success('已建立退貨案件，訂單進入「退貨處理中」')
    emit('update:modelValue', false)
    emit('created', created?.id ?? null)
  } catch (e) {
    if (e instanceof AdminApiError && formErrors.applyApiError(e)) return
    error.value = e instanceof AdminApiError ? e.message : '建立失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-dialog :model-value="modelValue" title="建立退貨申請" width="640px" :close-on-click-modal="false" class="refund-create" @update:model-value="(v: boolean) => emit('update:modelValue', v)">
    <el-alert v-if="error" :title="error" type="warning" show-icon class="refund-create__block" @close="error = null" />
    <el-form label-position="top">
      <el-form-item v-if="!orderId" label="選擇訂單（已出貨或已完成）">
        <el-select v-model="pickedOrderId" filterable remote clearable :remote-method="searchOrders" :loading="searching" placeholder="輸入訂單編號搜尋" style="width: 100%" @change="(id: string) => id && loadOrder(id)">
          <el-option v-for="o in options" :key="o.id" :label="`${o.orderNo} ${formatMoney(o.total)} ${o.orderStatus}`" :value="o.id" />
        </el-select>
      </el-form-item>
    </el-form>
    <el-skeleton v-if="loading" :rows="4" animated />
    <template v-else-if="order">
      <p class="refund-create__title">訂單 {{ order.orderNo }}（{{ order.orderStatus }}，總額 {{ formatMoney(order.total) }}）</p>
      <FormField field="items" style="margin-bottom: 0">
        <div class="refund-create__items">
          <div v-for="item in order.items" :key="item.id" class="refund-create__item">
            <div class="refund-create__item-main">
              <strong>{{ item.productName }}</strong>
              <span class="refund-create__muted">{{ item.variantLabel }}・單價 {{ formatMoney(item.unitPrice) }}・購買 {{ item.quantity }}・已退 {{ item.refundedQuantity }}</span>
            </div>
            <el-input-number v-model="qty[item.id]" :min="0" :max="refundable(item)" :disabled="refundable(item) <= 0" size="small" />
          </div>
        </div>
      </FormField>
      <el-form label-position="top">
        <FormField field="reason" label="退貨原因" required><el-input v-model="form.reason" type="textarea" :rows="2" maxlength="200" show-word-limit /></FormField>
        <el-form-item label="是否需要退回商品"><el-switch v-model="form.needsReturn" active-text="需要退回並驗收" inactive-text="不需要退回（核准後直接可退款）" /></el-form-item>
        <FormField field="refundAmount" label="退款金額">
          <div class="refund-create__amount">
            <span>所退品項小計 {{ formatMoney(itemsSubtotal) }}</span>
            <el-checkbox v-model="form.overrideAmount">手動指定金額（例如連運費一起退）</el-checkbox>
            <el-input-number v-if="form.overrideAmount" v-model="form.refundAmount" :min="0" :controls="false" />
          </div>
          <p class="refund-create__muted">不可超過這張訂單尚可退的金額。本次退款金額：{{ formatMoney(finalAmount) }}</p>
        </FormField>
      </el-form>
    </template>
    <template #footer>
      <el-button @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="saving" :disabled="!order" @click="submit">建立案件</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.refund-create__block { margin-bottom: 12px; }
.refund-create__title { margin: 0 0 8px; font-size: 14px; }
.refund-create__items { display: flex; flex-direction: column; gap: 6px; margin-bottom: 12px; }
.refund-create__item { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 8px 10px; border: 1px solid var(--admin-border); border-radius: 4px; flex-wrap: wrap; }
.refund-create__item-main { display: flex; flex-direction: column; min-width: 0; flex: 1; }
.refund-create__muted { font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.refund-create__amount { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
