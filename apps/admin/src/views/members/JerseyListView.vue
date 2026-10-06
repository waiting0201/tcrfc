<script setup lang="ts">
/**
 * 球衣發放：待處理 → 已寄出 → 已領取（可往回更正）。
 * 收件資訊依「檢視完整個資」權限遮罩；沒有這個權限的人也不能修改領用人、電話、地址。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { JERSEY_STATUS_OPTIONS, type JerseyStatusCode } from '@/api/adminMembers'
import {
  batchJerseyStatus,
  createJersey,
  exportJerseys,
  getJerseySizeSummary,
  JERSEY_DELIVERY_OPTIONS,
  JERSEY_SIZE_OPTIONS,
  listJerseys,
  lookupMemberships,
  updateJersey,
  type JerseyDeliveryMethod,
  type JerseyDto,
  type JerseySizeSummaryDto,
  type MembershipLookupDto,
  type UpdateJerseyPayload,
} from '@/api/adminJerseys'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate } = useCrudPermissions('member.jersey')
const canExport = usePermission('member.jersey.export')
const canReveal = usePermission('member.pii.reveal')
const club = computed(() => activeClubId.value)

const filters = reactive({ status: '', size: '', deliveryMethod: '', keyword: '' })
const rows = ref<JerseyDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const selectedIds = ref<string[]>([])

const summaryStatus = ref<JerseyStatusCode>('pending')
const summary = ref<JerseySizeSummaryDto[]>([])
const summaryLoading = ref(false)
const summaryError = ref<string | null>(null)
const summaryTotals = computed(() => ({
  total: summary.value.reduce((n, s) => n + s.total, 0),
  ship: summary.value.reduce((n, s) => n + s.ship, 0),
  pickup: summary.value.reduce((n, s) => n + s.pickup, 0),
}))

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listJerseys(club.value, { ...filters, keyword: filters.keyword.trim() || undefined, status: filters.status || undefined, size: filters.size || undefined, deliveryMethod: filters.deliveryMethod || undefined }, page.value, pageSize.value)
    rows.value = result.items
    total.value = result.totalCount
    selectedIds.value = selectedIds.value.filter((id) => result.items.some((r) => r.id === id))
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '球衣清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function loadSummary() {
  summaryLoading.value = true
  summaryError.value = null
  try {
    summary.value = await getJerseySizeSummary(club.value, summaryStatus.value)
  } catch (error) {
    summary.value = []
    summaryError.value = error instanceof AdminApiError ? error.message : '尺寸統計載入失敗'
  } finally {
    summaryLoading.value = false
  }
}

function reloadAll() {
  load()
  loadSummary()
}
function applyFilter() {
  page.value = 1
  selectedIds.value = []
  load()
}
function clearFilter() {
  Object.assign(filters, { status: '', size: '', deliveryMethod: '', keyword: '' })
  applyFilter()
}

onMounted(reloadAll)
watch(club, () => {
  Object.assign(filters, { status: '', size: '', deliveryMethod: '', keyword: '' })
  page.value = 1
  selectedIds.value = []
  reloadAll()
})

// ── 勾選 ──
function onSelectionChange(selection: JerseyDto[]) {
  selectedIds.value = selection.map((r) => r.id)
}
function toggleSelected(id: string, checked: boolean) {
  selectedIds.value = checked ? [...selectedIds.value, id] : selectedIds.value.filter((x) => x !== id)
}

// ── 狀態變更（單筆與批次） ──
const statusLabel = (s: JerseyStatusCode) => JERSEY_STATUS_OPTIONS.find((o) => o.value === s)?.label ?? s

async function setStatus(row: JerseyDto, status: JerseyStatusCode) {
  try {
    await updateJersey(club.value, row.id, { status })
    ElMessage.success(`已標為${statusLabel(status)}`)
    reloadAll()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '更新失敗，請稍後再試')
  }
}

const batching = ref(false)
async function handleBatch(status: JerseyStatusCode) {
  if (selectedIds.value.length === 0) return
  try {
    await ElMessageBox.confirm(`確定要把勾選的 ${selectedIds.value.length} 件球衣標為「${statusLabel(status)}」嗎？`, '批次更新狀態', {
      confirmButtonText: '確定', cancelButtonText: '取消', type: 'warning',
    })
  } catch {
    return
  }
  batching.value = true
  try {
    const result = await batchJerseyStatus(club.value, selectedIds.value, status)
    if (result.skipped.length > 0) {
      const detail = result.skipped.map((s) => s.reason).filter((v, i, a) => a.indexOf(v) === i).join('；')
      ElMessageBox.alert(`已更新 ${result.updatedCount} 件，略過 ${result.skipped.length} 件。略過原因：${detail}`, '批次更新結果', { confirmButtonText: '知道了' })
    } else {
      ElMessage.success(`已更新 ${result.updatedCount} 件`)
    }
    selectedIds.value = []
    reloadAll()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '批次更新失敗，請稍後再試')
  } finally {
    batching.value = false
  }
}

// ── 編輯 ──
const editOpen = ref(false)
const editing = ref<JerseyDto | null>(null)
const editForm = reactive({ recipientName: '', phone: '', address: '', size: '', deliveryMethod: 'ship' as JerseyDeliveryMethod, status: 'pending' as JerseyStatusCode })
const editError = ref<string | null>(null)
const editSaving = ref(false)
/** 收件資訊只有持有解除遮罩權限、且這筆回的是完整值時才能改。 */
const recipientEditable = computed(() => canReveal.value && !!editing.value && !editing.value.isMasked)

function openEdit(row: JerseyDto) {
  editing.value = row
  Object.assign(editForm, {
    recipientName: row.recipientName ?? '', phone: row.phone ?? '', address: row.address ?? '',
    size: row.size ?? '', deliveryMethod: row.deliveryMethod, status: row.status,
  })
  editError.value = null
  formErrors.clearAll()
  editOpen.value = true
}

async function handleEditSave() {
  const row = editing.value
  if (!row || !canUpdate.value) return
  const payload: UpdateJerseyPayload = {}
  if (recipientEditable.value) {
    if (editForm.recipientName.trim() !== (row.recipientName ?? '')) payload.recipientName = editForm.recipientName.trim()
    if (editForm.phone.trim() !== (row.phone ?? '')) payload.phone = editForm.phone.trim()
    if (editForm.address.trim() !== (row.address ?? '')) payload.address = editForm.address.trim()
  }
  if (editForm.size.trim().toUpperCase() !== (row.size ?? '').toUpperCase()) payload.size = editForm.size.trim()
  if (editForm.deliveryMethod !== row.deliveryMethod) payload.deliveryMethod = editForm.deliveryMethod
  if (editForm.status !== row.status) payload.status = editForm.status
  if (Object.keys(payload).length === 0) {
    editOpen.value = false
    return
  }
  editSaving.value = true
  editError.value = null
  formErrors.clearAll()
  try {
    await updateJersey(club.value, row.id, payload)
    editOpen.value = false
    ElMessage.success('已儲存')
    reloadAll()
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    editError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    editSaving.value = false
  }
}

// ── 代填 ──
const createOpen = ref(false)
const createForm = reactive({ membershipId: '', recipientName: '', phone: '', size: '', deliveryMethod: 'ship' as JerseyDeliveryMethod, address: '' })
const createError = ref<string | null>(null)
const createSaving = ref(false)
const memberOptions = ref<MembershipLookupDto[]>([])
const searching = ref(false)

function openCreate() {
  Object.assign(createForm, { membershipId: '', recipientName: '', phone: '', size: '', deliveryMethod: 'ship', address: '' })
  createError.value = null
  formErrors.clearAll()
  memberOptions.value = []
  createOpen.value = true
}

async function searchMemberships(keyword: string) {
  const k = keyword.trim()
  if (!k) {
    memberOptions.value = []
    return
  }
  searching.value = true
  try {
    memberOptions.value = (await lookupMemberships(club.value, k)).items
  } catch {
    memberOptions.value = []
  } finally {
    searching.value = false
  }
}

async function handleCreate() {
  createError.value = null
  formErrors.clearAll()
  if (!createForm.membershipId) return (createError.value = '請先搜尋並選擇會籍')
  if (!createForm.recipientName.trim()) return (createError.value = '請輸入領用人姓名')
  if (!createForm.size.trim()) return (createError.value = '請選擇或輸入尺寸')
  if (createForm.deliveryMethod === 'ship' && (!createForm.phone.trim() || !createForm.address.trim())) {
    return (createError.value = '寄送的球衣必須填寫電話與地址')
  }
  createSaving.value = true
  try {
    await createJersey(club.value, {
      membershipId: createForm.membershipId,
      recipientName: createForm.recipientName.trim(),
      phone: nullIfBlank(createForm.phone),
      size: createForm.size.trim(),
      deliveryMethod: createForm.deliveryMethod,
      address: nullIfBlank(createForm.address),
    })
    createOpen.value = false
    ElMessage.success('已新增球衣發放')
    reloadAll()
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    createError.value = error instanceof AdminApiError ? error.message : '新增失敗，請稍後再試'
  } finally {
    createSaving.value = false
  }
}

// ── 匯出 ──
const exportOpen = ref(false)
const exporting = ref(false)
async function handleExport(purpose: string) {
  exporting.value = true
  try {
    await exportJerseys(club.value, filters.status, purpose)
    exportOpen.value = false
    ElMessage.success('已匯出，檔案已下載')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

function statusTag(status: string): 'success' | 'warning' | 'info' {
  if (status === 'received') return 'success'
  if (status === 'shipped') return 'warning'
  return 'info'
}
const canMarkShipped = (row: JerseyDto) => row.deliveryMethod === 'ship' && row.status === 'pending'
</script>

<template>
  <div class="jersey">
    <PageHeader title="球衣發放">
      <template #meta><FrontendUnitBanner module-code="K3" /></template>
    </PageHeader>

    <el-card shadow="never" class="jersey__block">
      <template #header>
        <div class="jersey__summary-head">
          <span>尺寸備貨統計</span>
          <el-radio-group v-model="summaryStatus" size="small" @change="loadSummary">
            <el-radio-button v-for="o in JERSEY_STATUS_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</el-radio-button>
          </el-radio-group>
        </div>
      </template>
      <el-skeleton v-if="summaryLoading" :rows="2" animated />
      <el-alert v-else-if="summaryError" :title="summaryError" type="warning" show-icon :closable="false" />
      <el-empty v-else-if="summary.length === 0" :image-size="48" description="這個狀態下沒有球衣" />
      <div v-else class="jersey__summary">
        <div v-for="s in summary" :key="s.sizeLabel" class="jersey__size" :class="{ 'jersey__size--empty': s.size === null }">
          <div class="jersey__size-name">{{ s.sizeLabel }}</div>
          <div class="jersey__size-total">{{ s.total }}</div>
          <div class="jersey__size-sub">寄送 {{ s.ship }}・自取 {{ s.pickup }}</div>
        </div>
        <div class="jersey__size jersey__size--sum">
          <div class="jersey__size-name">合計</div>
          <div class="jersey__size-total">{{ summaryTotals.total }}</div>
          <div class="jersey__size-sub">寄送 {{ summaryTotals.ship }}・自取 {{ summaryTotals.pickup }}</div>
        </div>
      </div>
    </el-card>

    <el-card shadow="never" class="jersey__block">
      <div class="jersey__row">
        <el-input v-model="filters.keyword" placeholder="搜尋會員編號或領用人" clearable class="jersey__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="jersey__select" @change="applyFilter">
          <el-option v-for="o in JERSEY_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="filters.size" placeholder="尺寸" clearable class="jersey__select" @change="applyFilter">
          <el-option v-for="s in JERSEY_SIZE_OPTIONS" :key="s" :label="s" :value="s" />
        </el-select>
        <el-select v-model="filters.deliveryMethod" placeholder="領取方式" clearable class="jersey__select" @change="applyFilter">
          <el-option v-for="o in JERSEY_DELIVERY_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-button type="primary" @click="applyFilter">篩選</el-button>
        <el-button @click="clearFilter">清除</el-button>
        <span class="jersey__spacer" />
        <el-button v-if="canExport" @click="exportOpen = true">匯出出貨清單</el-button>
        <el-button v-if="canCreate" type="primary" @click="openCreate">+ 代填球衣</el-button>
      </div>
      <div v-if="canUpdate" class="jersey__row jersey__row--batch">
        <span class="jersey__muted">已勾選 {{ selectedIds.length }} 件</span>
        <el-button size="small" :disabled="selectedIds.length === 0" :loading="batching" @click="handleBatch('shipped')">標為已寄出</el-button>
        <el-button size="small" :disabled="selectedIds.length === 0" :loading="batching" @click="handleBatch('received')">標為已領取</el-button>
        <el-button size="small" :disabled="selectedIds.length === 0" :loading="batching" @click="handleBatch('pending')">回復待處理</el-button>
      </div>
      <p class="jersey__hint">
        「已寄出」只適用寄送的球衣，到場領取的請直接標為已領取。狀態變更會即時反映在會員中心。
        <template v-if="!canReveal">你的帳號沒有檢視完整個資的權限，收件資訊顯示為遮罩，也不能修改領用人、電話與地址。</template>
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id" @selection-change="onSelectionChange">
          <el-table-column v-if="canUpdate" type="selection" width="44" />
          <el-table-column label="會員編號" width="110"><template #default="{ row }">{{ row.memberNo || '—' }}</template></el-table-column>
          <el-table-column label="領用人／電話" min-width="150">
            <template #default="{ row }">
              <div>{{ row.recipientName || '—' }}</div>
              <div class="jersey__muted">{{ row.phone || '—' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="尺寸" width="80"><template #default="{ row }">{{ row.size || '未填' }}</template></el-table-column>
          <el-table-column label="領取方式" min-width="180">
            <template #default="{ row }">
              <div>{{ row.deliveryMethodLabel }}</div>
              <div v-if="row.deliveryMethod === 'ship'" class="jersey__muted jersey__addr">{{ row.address || '—' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="狀態" width="130">
            <template #default="{ row }">
              <el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag>
              <div v-if="row.shippedOn" class="jersey__muted">寄出 {{ row.shippedOn }}</div>
              <div v-if="row.receivedOn" class="jersey__muted">領取 {{ row.receivedOn }}</div>
            </template>
          </el-table-column>
          <el-table-column v-if="canUpdate" label="操作" width="200" fixed="right">
            <template #default="{ row }">
              <el-button v-if="canMarkShipped(row)" size="small" text type="primary" @click="setStatus(row, 'shipped')">已寄出</el-button>
              <el-button v-if="row.status !== 'received'" size="small" text type="primary" @click="setStatus(row, 'received')">已領取</el-button>
              <el-button size="small" text type="primary" @click="openEdit(row)">編輯</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">
            <el-checkbox v-if="canUpdate" :model-value="selectedIds.includes(row.id)" @change="(v: unknown) => toggleSelected(row.id, v === true)" />
            {{ row.memberNo || '—' }}・{{ row.recipientName || '—' }}
          </template>
          <template #meta="{ row }">
            <el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag>
            <span>尺寸 {{ row.size || '未填' }}</span>
            <span>{{ row.deliveryMethodLabel }}</span>
            <span>{{ row.phone || '—' }}</span>
            <span v-if="row.deliveryMethod === 'ship'">{{ row.address || '—' }}</span>
          </template>
          <template #actions="{ row }">
            <template v-if="canUpdate">
              <el-button v-if="canMarkShipped(row)" size="small" text type="primary" @click="setStatus(row, 'shipped')">已寄出</el-button>
              <el-button v-if="row.status !== 'received'" size="small" text type="primary" @click="setStatus(row, 'received')">已領取</el-button>
              <el-button size="small" text type="primary" @click="openEdit(row)">編輯</el-button>
            </template>
          </template>
        </MobileCardList>
        <div class="jersey__pager">
          <el-pagination
            v-model:current-page="page"
            v-model:page-size="pageSize"
            :total="total"
            :page-sizes="[20, 50, 100]"
            :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'"
            :pager-count="isMobile ? 5 : 7"
            background
            @current-change="load"
            @size-change="applyFilter"
          />
        </div>
      </template>
      <el-empty v-else description="目前沒有符合條件的球衣發放" />
    </el-card>

    <el-dialog v-model="editOpen" title="編輯球衣發放" width="480px" :close-on-click-modal="false">
      <el-alert v-if="editError" :title="editError" type="warning" show-icon class="jersey__gap" @close="editError = null" />
      <el-alert v-if="!recipientEditable" type="info" show-icon :closable="false" class="jersey__gap" title="收件資訊為遮罩或你沒有檢視完整個資的權限，領用人、電話、地址無法修改。" />
      <el-form label-position="top">
        <FormField field="recipientName" label="領用人"><el-input v-model="editForm.recipientName" :disabled="!recipientEditable" maxlength="64" /></FormField>
        <FormField field="phone" label="電話"><el-input v-model="editForm.phone" :disabled="!recipientEditable" maxlength="32" /></FormField>
        <FormField field="address" label="地址"><el-input v-model="editForm.address" :disabled="!recipientEditable" maxlength="255" /></FormField>
        <FormField field="size" label="尺寸">
          <el-select v-model="editForm.size" filterable allow-create default-first-option placeholder="選擇或輸入尺寸" style="width: 100%">
            <el-option v-for="s in JERSEY_SIZE_OPTIONS" :key="s" :label="s" :value="s" />
          </el-select>
        </FormField>
        <FormField field="deliveryMethod" label="領取方式">
          <el-radio-group v-model="editForm.deliveryMethod">
            <el-radio v-for="o in JERSEY_DELIVERY_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</el-radio>
          </el-radio-group>
        </FormField>
        <FormField field="status" label="狀態">
          <el-select v-model="editForm.status" style="width: 100%">
            <el-option v-for="o in JERSEY_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" :disabled="o.value === 'shipped' && editForm.deliveryMethod === 'pickup'" />
          </el-select>
          <p class="jersey__hint">到場領取的球衣請直接標為已領取；改回待處理會清掉寄出與領取日期。</p>
        </FormField>
      </el-form>
      <template #footer>
        <el-button :disabled="editSaving" @click="editOpen = false">取消</el-button>
        <el-button type="primary" :loading="editSaving" @click="handleEditSave">儲存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="createOpen" title="代填球衣" width="520px" :close-on-click-modal="false">
      <el-alert v-if="createError" :title="createError" type="warning" show-icon class="jersey__gap" @close="createError = null" />
      <el-form label-position="top">
        <FormField field="membershipId" label="會籍" required>
          <el-select v-model="createForm.membershipId" filterable remote reserve-keyword :remote-method="searchMemberships" :loading="searching" placeholder="輸入會員編號搜尋會籍" style="width: 100%">
            <el-option v-for="m in memberOptions" :key="m.membershipId" :value="m.membershipId" :label="`${m.memberNo}・${m.memberName || '—'}・${m.seasonCode}・${m.planName || '未指定方案'}（${m.effectiveStatusLabel}）`" />
          </el-select>
          <p class="jersey__hint">球衣件數不能超過方案的球衣份數，免費會籍不含球衣。</p>
        </FormField>
        <FormField field="recipientName" label="領用人" required><el-input v-model="createForm.recipientName" maxlength="64" /></FormField>
        <FormField field="size" label="尺寸" required>
          <el-select v-model="createForm.size" filterable allow-create default-first-option placeholder="選擇或輸入尺寸" style="width: 100%">
            <el-option v-for="s in JERSEY_SIZE_OPTIONS" :key="s" :label="s" :value="s" />
          </el-select>
        </FormField>
        <FormField field="deliveryMethod" label="領取方式" required>
          <el-radio-group v-model="createForm.deliveryMethod">
            <el-radio v-for="o in JERSEY_DELIVERY_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</el-radio>
          </el-radio-group>
        </FormField>
        <FormField field="phone" :label="createForm.deliveryMethod === 'ship' ? '電話（寄送必填）' : '電話'"><el-input v-model="createForm.phone" maxlength="32" /></FormField>
        <FormField v-if="createForm.deliveryMethod === 'ship'" field="address" label="地址（寄送必填）"><el-input v-model="createForm.address" maxlength="255" /></FormField>
      </el-form>
      <template #footer>
        <el-button :disabled="createSaving" @click="createOpen = false">取消</el-button>
        <el-button type="primary" :loading="createSaving" @click="handleCreate">新增</el-button>
      </template>
    </el-dialog>

    <ExportPurposeDialog v-model="exportOpen" title="匯出球衣出貨清單" :description="`匯出${filters.status ? statusLabel(filters.status as JerseyStatusCode) : '待處理'}的球衣，含完整收件資訊。`" :loading="exporting" @confirm="handleExport" />
  </div>
</template>

<style scoped>
.jersey__block { margin-bottom: 12px; }
.jersey__summary-head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; }
.jersey__summary { display: flex; flex-wrap: wrap; gap: 8px; }
.jersey__size { min-width: 92px; padding: 8px 12px; border: 1px solid var(--el-border-color-lighter); border-radius: 6px; text-align: center; }
.jersey__size--empty { border-style: dashed; }
.jersey__size--sum { background: var(--el-fill-color-light); }
.jersey__size-name { font-size: 12px; color: var(--admin-text-secondary); }
.jersey__size-total { font-size: 22px; font-weight: 600; line-height: 1.3; }
.jersey__size-sub { font-size: 11px; color: var(--admin-text-tertiary); }
.jersey__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.jersey__row--batch { margin-top: 10px; }
.jersey__keyword { width: 240px; max-width: 100%; }
.jersey__select { width: 140px; max-width: 100%; }
.jersey__spacer { flex: 1; }
.jersey__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.jersey__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.jersey__addr { word-break: break-all; }
.jersey__gap { margin-bottom: 12px; }
.jersey__pager { display: flex; justify-content: flex-end; margin-top: 16px; overflow-x: auto; }
@media (max-width: 767px) {
  .jersey__pager { justify-content: center; }
  .jersey__keyword, .jersey__select { width: 100%; }
}
</style>
