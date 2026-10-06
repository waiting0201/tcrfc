<script setup lang="ts">
/**
 * 獎品發放（比照球衣發放）：中獎人清單、領獎方式、收件資訊、寄出與領取狀態。
 * - 「逾期」：待處理且已過領獎期限，由系統自動判定；逾期未領時請到「中獎人與備取」遞補備取者。
 * - 姓名與收件資訊依「檢視完整個資」權限遮罩；沒有這個權限的人不能修改收件資訊。
 * - 「已寄出」只適用寄送的獎品，且必須有收件人姓名、電話與地址；現場領取的請直接標為已領取。
 * - 匯出中獎人聯絡名單與獎品出貨清單需要匯出權限，並須填寫用途。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useViewUpdatePermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { formatDate } from '@/utils/dateTime'
import {
  batchFulfilmentStatus,
  exportDrawShipping,
  exportDrawWinners,
  listFulfilment,
  updateFulfilment,
  type DrawDetailDto,
  type FulfilmentRowDto,
  type FulfilmentStatus,
} from '@/api/adminDraws'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const props = defineProps<{ draw: DrawDetailDto }>()
const emit = defineEmits<{ (e: 'changed'): void }>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canUpdate } = useViewUpdatePermissions('member.draw')
const canReveal = usePermission('member.pii.reveal')
const canExport = usePermission('member.draw.export')
const club = computed(() => activeClubId.value)
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)
/** 只有已抽出／已公布的活動能改發放資料 */
const editable = computed(() => canUpdate.value && (props.draw.status === 'drawn' || props.draw.status === 'announced'))

const filters = reactive({ status: '', claimMethod: '' })
const rows = ref<FulfilmentRowDto[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const selected = ref<number[]>([])

async function load() {
  if (props.draw.winnerCount + props.draw.backupCount === 0) {
    rows.value = []
    total.value = 0
    return
  }
  loading.value = true
  loadError.value = null
  try {
    const result = await listFulfilment(club.value, props.draw.id, { status: filters.status || undefined, claimMethod: filters.claimMethod || undefined, page: page.value, pageSize: pageSize.value })
    rows.value = result.items
    total.value = result.totalCount
    selected.value = selected.value.filter((s) => result.items.some((r) => r.serialNo === s))
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = errorText(error, '發放清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  selected.value = []
  load()
}
onMounted(load)
watch(() => [props.draw.id, props.draw.winnerCount, props.draw.backupCount, props.draw.status], applyFilter)

function onSelectionChange(sel: FulfilmentRowDto[]) {
  selected.value = sel.map((r) => r.serialNo)
}
function toggleSelected(serialNo: number, checked: boolean) {
  selected.value = checked ? [...selected.value, serialNo] : selected.value.filter((x) => x !== serialNo)
}
function statusTag(status: string): 'success' | 'warning' | 'danger' | 'info' {
  if (status === 'claimed') return 'success'
  if (status === 'shipped') return 'warning'
  if (status === 'overdue') return 'danger'
  return 'info'
}
const statusLabel = (s: FulfilmentStatus) => ({ pending: '待處理', shipped: '已寄出', claimed: '已領取' })[s]

// ── 單筆編輯 ──
const editOpen = ref(false)
const editRow = ref<FulfilmentRowDto | null>(null)
const editSaving = ref(false)
const editError = ref<string | null>(null)
const editForm = reactive({ claimMethod: '' as string, recipientName: '', recipientPhone: '', recipientAddress: '', status: 'pending' as FulfilmentStatus, note: '' })
const recipientEditable = computed(() => canReveal.value && !!editRow.value && !editRow.value.isMasked)
function openEdit(row: FulfilmentRowDto) {
  editRow.value = row
  Object.assign(editForm, { claimMethod: row.claimMethod ?? '', recipientName: row.recipientName ?? '', recipientPhone: row.recipientPhone ?? '', recipientAddress: row.recipientAddress ?? '', status: row.fulfilmentStatus, note: row.note ?? '' })
  editError.value = null
  formErrors.clearAll()
  editOpen.value = true
}
async function saveEdit() {
  const row = editRow.value
  if (!row) return
  editError.value = null
  formErrors.clearAll()
  const body: Parameters<typeof updateFulfilment>[3] = {}
  if (editForm.claimMethod && editForm.claimMethod !== (row.claimMethod ?? '')) body.claimMethod = editForm.claimMethod
  if (recipientEditable.value) {
    if (editForm.recipientName.trim() !== (row.recipientName ?? '')) body.recipientName = editForm.recipientName.trim()
    if (editForm.recipientPhone.trim() !== (row.recipientPhone ?? '')) body.recipientPhone = editForm.recipientPhone.trim()
    if (editForm.recipientAddress.trim() !== (row.recipientAddress ?? '')) body.recipientAddress = editForm.recipientAddress.trim()
  }
  if (editForm.status !== row.fulfilmentStatus) body.status = editForm.status
  if (editForm.note.trim() !== (row.note ?? '')) body.note = editForm.note.trim()
  if (Object.keys(body).length === 0) {
    editOpen.value = false
    return
  }
  const method = body.claimMethod ?? row.claimMethod
  if (body.status === 'shipped' && method !== 'ship') return (editError.value = '只有「寄送」的獎品能標為已寄出，現場領取請標為已領取')
  editSaving.value = true
  try {
    await updateFulfilment(club.value, props.draw.id, row.serialNo, body)
    editOpen.value = false
    ElMessage.success('已儲存')
    await load()
    emit('changed')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    editError.value = errorText(error, '儲存失敗，請稍後再試')
  } finally {
    editSaving.value = false
  }
}

// ── 批次狀態 ──
const batching = ref(false)
async function batch(status: FulfilmentStatus) {
  if (selected.value.length === 0) return
  try {
    await ElMessageBox.confirm(`確定要把勾選的 ${selected.value.length} 位標為「${statusLabel(status)}」嗎？`, '批次更新發放狀態', { confirmButtonText: '確定', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  batching.value = true
  try {
    const result = await batchFulfilmentStatus(club.value, props.draw.id, { serialNos: selected.value, status })
    if (result.skipped.length > 0) {
      const detail = [...new Set(result.skipped.map((s) => s.reason))].join('；')
      ElMessageBox.alert(`已更新 ${result.updatedCount} 位，略過 ${result.skipped.length} 位。略過原因：${detail}`, '批次更新結果', { confirmButtonText: '知道了' })
    } else {
      ElMessage.success(`已更新 ${result.updatedCount} 位`)
    }
    selected.value = []
    await load()
    emit('changed')
  } catch (error) {
    ElMessage.error(errorText(error, '批次更新失敗，請稍後再試'))
  } finally {
    batching.value = false
  }
}

// ── 匯出 ──
const exportKind = ref<'winners' | 'shipping'>('winners')
const exportOpen = ref(false)
const exporting = ref(false)
async function doExport(purpose: string) {
  exporting.value = true
  try {
    if (exportKind.value === 'winners') await exportDrawWinners(club.value, props.draw.id, purpose)
    else await exportDrawShipping(club.value, props.draw.id, purpose)
    exportOpen.value = false
    ElMessage.success('已匯出，檔案已下載')
  } catch (error) {
    ElMessage.error(errorText(error, '匯出失敗，請稍後再試'))
  } finally {
    exporting.value = false
  }
}
</script>

<template>
  <div class="fulfil">
    <el-alert v-if="draw.winnerCount + draw.backupCount === 0" type="info" show-icon :closable="false" title="還沒有中獎人，回填中獎人後才會出現在這裡。" />
    <template v-else>
      <div class="fulfil__row">
        <el-select v-model="filters.status" placeholder="發放狀態" clearable class="fulfil__select" @change="applyFilter">
          <el-option label="待處理" value="pending" /><el-option label="已寄出" value="shipped" /><el-option label="已領取" value="claimed" />
        </el-select>
        <el-select v-model="filters.claimMethod" placeholder="領獎方式" clearable class="fulfil__select" @change="applyFilter">
          <el-option label="寄送" value="ship" /><el-option label="現場領取" value="pickup" />
        </el-select>
        <el-button type="primary" @click="applyFilter">篩選</el-button>
        <span class="fulfil__spacer" />
        <template v-if="canExport">
          <el-button @click="exportKind = 'winners'; exportOpen = true">匯出中獎人聯絡名單</el-button>
          <el-button @click="exportKind = 'shipping'; exportOpen = true">匯出獎品出貨清單</el-button>
        </template>
      </div>
      <div v-if="editable" class="fulfil__row fulfil__row--batch">
        <span class="fulfil__hint">已勾選 {{ selected.length }} 位</span>
        <el-button size="small" :disabled="selected.length === 0" :loading="batching" @click="batch('shipped')">標為已寄出</el-button>
        <el-button size="small" :disabled="selected.length === 0" :loading="batching" @click="batch('claimed')">標為已領取</el-button>
        <el-button size="small" :disabled="selected.length === 0" :loading="batching" @click="batch('pending')">回復待處理</el-button>
      </div>
      <p class="fulfil__hint">「逾期」＝待處理且已過領獎期限，逾期未領請到「中獎人與備取」遞補。{{ canUpdate && !editable ? '活動要在「已抽出」或「已公布」狀態才能修改發放資料。' : '' }}<template v-if="!canReveal">你的帳號沒有檢視完整個資的權限，姓名與收件資訊顯示為遮罩，也不能修改收件資訊。</template></p>

      <el-skeleton v-if="loading" :rows="4" animated />
      <el-empty v-else-if="loadError" :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
      <el-empty v-else-if="rows.length === 0" description="沒有符合條件的中獎人" :image-size="64" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="serialNo" @selection-change="onSelectionChange">
          <el-table-column v-if="editable" type="selection" width="44" />
          <el-table-column label="序號" width="70" prop="serialNo" />
          <el-table-column label="中獎人" min-width="130"><template #default="{ row }"><div>{{ row.memberName || '—' }}<el-tag v-if="row.isBackup" type="warning" size="small" class="fulfil__tag">備取</el-tag></div><div class="fulfil__hint">{{ row.memberNo }}</div></template></el-table-column>
          <el-table-column label="獎項" min-width="130"><template #default="{ row }">{{ row.prizeName || '—' }}</template></el-table-column>
          <el-table-column label="領獎方式" min-width="200">
            <template #default="{ row }">
              <div>{{ row.claimMethodLabel || '未指定' }}</div>
              <div v-if="row.claimMethod === 'ship'" class="fulfil__hint">{{ row.recipientName || '—' }} {{ row.recipientPhone || '' }}<br>{{ row.recipientAddress || '' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="發放狀態" width="120">
            <template #default="{ row }">
              <el-tag :type="statusTag(row.effectiveStatus)" size="small">{{ row.effectiveStatusLabel }}</el-tag>
              <div v-if="row.shippedAt" class="fulfil__hint">寄出 {{ formatDate(row.shippedAt) }}</div>
              <div v-if="row.claimedAt" class="fulfil__hint">領取 {{ formatDate(row.claimedAt) }}</div>
            </template>
          </el-table-column>
          <el-table-column v-if="editable" label="操作" width="80" fixed="right"><template #default="{ row }"><el-button size="small" text type="primary" @click="openEdit(row)">編輯</el-button></template></el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="serialNo">
          <template #title="{ row }">
            <el-checkbox v-if="editable" :model-value="selected.includes(row.serialNo)" @change="(v: unknown) => toggleSelected(row.serialNo, v === true)" />
            序號 {{ row.serialNo }}・{{ row.memberName || '—' }}
          </template>
          <template #meta="{ row }">
            <el-tag :type="statusTag(row.effectiveStatus)" size="small">{{ row.effectiveStatusLabel }}</el-tag>
            <el-tag v-if="row.isBackup" type="warning" size="small">備取</el-tag>
            <span>{{ row.prizeName || '—' }}</span><span>{{ row.claimMethodLabel || '未指定領獎方式' }}</span>
          </template>
          <template #actions="{ row }"><el-button v-if="editable" size="small" text type="primary" @click="openEdit(row)">編輯</el-button></template>
        </MobileCardList>
        <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="fulfil__pager" @current-change="load" @size-change="applyFilter" />
      </template>
    </template>

    <ExportPurposeDialog v-model="exportOpen" :title="exportKind === 'winners' ? '匯出中獎人聯絡名單' : '匯出獎品出貨清單'" :description="exportKind === 'winners' ? '只包含已回填的中獎人，含姓名、手機、Email 與收件資訊。' : '含中獎人、獎項、領獎方式、收件資訊與發放狀態（逾期會標示）。'" :loading="exporting" @confirm="doExport" />

    <el-dialog v-model="editOpen" title="編輯獎品發放" width="480px" :close-on-click-modal="false" class="fulfil__dialog">
      <el-alert v-if="editError" :title="editError" type="warning" show-icon class="fulfil__block" @close="editError = null" />
      <p v-if="editRow" class="fulfil__hint">序號 {{ editRow.serialNo }}・{{ editRow.memberName || '—' }}・{{ editRow.prizeName || '—' }}</p>
      <el-form label-position="top">
        <FormField field="claimMethod" label="領獎方式"><el-radio-group v-model="editForm.claimMethod"><el-radio-button value="ship">寄送</el-radio-button><el-radio-button value="pickup">現場領取</el-radio-button></el-radio-group></FormField>
        <template v-if="editForm.claimMethod === 'ship'">
          <p v-if="!recipientEditable" class="fulfil__hint">你的帳號沒有檢視完整個資的權限（或這筆是遮罩值），不能修改收件資訊。</p>
          <FormField field="recipientName" label="收件人姓名"><el-input v-model="editForm.recipientName" :disabled="!recipientEditable" maxlength="64" /></FormField>
          <FormField field="recipientPhone" label="收件人電話"><el-input v-model="editForm.recipientPhone" :disabled="!recipientEditable" maxlength="32" /></FormField>
          <FormField field="recipientAddress" label="收件地址"><el-input v-model="editForm.recipientAddress" :disabled="!recipientEditable" maxlength="200" /></FormField>
        </template>
        <FormField field="status" label="發放狀態"><el-radio-group v-model="editForm.status"><el-radio-button value="pending">待處理</el-radio-button><el-radio-button value="shipped">已寄出</el-radio-button><el-radio-button value="claimed">已領取</el-radio-button></el-radio-group></FormField>
        <el-form-item label="備註（清空即清除）"><el-input v-model="editForm.note" type="textarea" :rows="2" maxlength="200" show-word-limit /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editOpen = false">取消</el-button>
        <el-button type="primary" :loading="editSaving" @click="saveEdit">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.fulfil__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 8px; }
.fulfil__select { width: 140px; max-width: 100%; }
.fulfil__spacer { flex: 1; }
.fulfil__hint { margin: 4px 0 8px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.fulfil__tag { margin-left: 6px; }
.fulfil__block { margin-bottom: 12px; }
.fulfil__pager { margin-top: 12px; justify-content: flex-end; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
