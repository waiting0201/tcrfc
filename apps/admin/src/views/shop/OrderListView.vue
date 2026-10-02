<script setup lang="ts">
/**
 * 訂單（對應前台會員中心的「我的訂單」）。訂單狀態：待付款 → 已付款 → 備貨中 → 已出貨 → 已完成，
 * 另有已取消、退貨處理中、已退款。收件人視同會員個資：沒有「檢視完整個資」權限時一律遮罩，
 * 且關鍵字只比對訂單編號。藍鯨的訂單由磐石代收，只在藍鯨站台看得到。
 * 🔴 前台結帳流程已完成，但正式線上付款待取得 LINE Pay 商店號：在那之前訂單只能由後台「現場收款」手動建立（賽事日擺攤、現場補登）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  batchOrderSettlement,
  DELIVERY_METHOD_OPTIONS,
  exportOrders,
  listOrders,
  ORDER_STATUS_OPTIONS,
  PAYMENT_METHOD_OPTIONS,
  PAYMENT_STATUS_OPTIONS,
  releaseExpiredOrders,
  SETTLEMENT_STATUS_OPTIONS,
  type OrderFilter,
  type OrderListItemDto,
} from '@/api/adminShop'
import { formatDateTime } from '@/utils/dateTime'
import { formatMoney } from '@/utils/formatMoney'
import { orderStatusTag } from '@/utils/shopStatus'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate } = useCrudPermissions('shop.order')
const canReveal = usePermission('shop.order.reveal')
const canExport = usePermission('shop.order.export')
const club = computed(() => activeClubId.value)

interface FilterState {
  keyword: string
  orderStatus: string
  paymentStatus: string
  deliveryMethod: string
  paymentMethod: string
  settlementStatus: string
  member: '' | 'yes' | 'no'
  range: [string, string] | null
}
const emptyFilter = (): FilterState => ({ keyword: '', orderStatus: '', paymentStatus: '', deliveryMethod: '', paymentMethod: '', settlementStatus: '', member: '', range: null })
const filters = reactive<FilterState>(emptyFilter())
const showMore = ref(false)

const rows = ref<OrderListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const selectedIds = ref<string[]>([])

function buildFilter(): OrderFilter {
  return {
    keyword: filters.keyword.trim() || undefined,
    orderStatus: filters.orderStatus || undefined,
    paymentStatus: filters.paymentStatus || undefined,
    deliveryMethod: filters.deliveryMethod || undefined,
    paymentMethod: filters.paymentMethod || undefined,
    settlementStatus: filters.settlementStatus || undefined,
    isMember: filters.member === '' ? undefined : filters.member === 'yes',
    from: filters.range?.[0],
    to: filters.range?.[1],
  }
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listOrders(club.value, buildFilter(), page.value, pageSize.value)
    rows.value = result.items
    total.value = result.totalCount
    selectedIds.value = selectedIds.value.filter((id) => result.items.some((r) => r.id === id))
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '訂單清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  selectedIds.value = []
  load()
}
function clearFilter() {
  Object.assign(filters, emptyFilter())
  applyFilter()
}
onMounted(load)
watch(club, () => {
  Object.assign(filters, emptyFilter())
  page.value = 1
  selectedIds.value = []
  load()
})
const isFiltered = computed(() => JSON.stringify(filters) !== JSON.stringify(emptyFilter()))

function onSelectionChange(selection: OrderListItemDto[]) {
  selectedIds.value = selection.map((r) => r.id)
}
function toggleSelected(id: string, checked: boolean) {
  selectedIds.value = checked ? [...selectedIds.value, id] : selectedIds.value.filter((x) => x !== id)
}

// ── 批次分帳標記 ──
const settleOpen = ref(false)
const settleSaving = ref(false)
const settleError = ref<string | null>(null)
const settleForm = reactive({ status: 'settled', settledOn: '', note: '' })
function openSettle() {
  Object.assign(settleForm, { status: 'settled', settledOn: '', note: '' })
  settleError.value = null
  settleOpen.value = true
}
async function saveSettle() {
  settleSaving.value = true
  settleError.value = null
  try {
    const result = await batchOrderSettlement(club.value, {
      ids: selectedIds.value,
      status: settleForm.status,
      settledOn: settleForm.status === 'settled' ? settleForm.settledOn || null : null,
      note: settleForm.note.trim() || null,
    })
    settleOpen.value = false
    if (result.skipped.length > 0) {
      const detail = [...new Set(result.skipped.map((s) => s.reason))].join('；')
      ElMessageBox.alert(`已標記 ${result.updatedCount} 張，略過 ${result.skipped.length} 張。略過原因：${detail}`, '批次標記結果', { confirmButtonText: '知道了' })
    } else {
      ElMessage.success(`已標記 ${result.updatedCount} 張`)
    }
    selectedIds.value = []
    await load()
  } catch (error) {
    settleError.value = error instanceof AdminApiError ? error.message : '批次標記失敗，請稍後再試'
  } finally {
    settleSaving.value = false
  }
}

// ── 釋回逾時未付款 ──
const releasing = ref(false)
async function releaseExpired() {
  try {
    await ElMessageBox.confirm('系統會把超過付款期限仍未付款的訂單取消，並把保留的庫存釋回。確定要現在執行嗎？', '釋回逾時未付款訂單', {
      confirmButtonText: '執行', cancelButtonText: '取消', type: 'warning',
    })
  } catch {
    return
  }
  releasing.value = true
  try {
    const result = await releaseExpiredOrders(club.value)
    ElMessage.success(result.expiredCount > 0 ? `已釋回 ${result.expiredCount} 張逾時訂單（付款期限 ${result.timeoutMinutes} 分鐘）` : `沒有逾時的訂單（付款期限 ${result.timeoutMinutes} 分鐘）`)
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '執行失敗，請稍後再試')
  } finally {
    releasing.value = false
  }
}

// ── 匯出 ──
const exportOpen = ref(false)
const exporting = ref(false)
async function handleExport(purpose: string) {
  exporting.value = true
  try {
    await exportOrders(club.value, buildFilter(), purpose)
    exportOpen.value = false
    ElMessage.success('已匯出，檔案已下載')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

const go = (row: OrderListItemDto) => router.push(`/shop/orders/${row.id}`)
</script>

<template>
  <div class="orders">
    <PageHeader title="訂單">
      <template #meta><FrontendUnitBanner module-code="S3" /></template>
    </PageHeader>

    <el-alert class="orders__block" type="info" show-icon :closable="false" title="前台結帳流程已完成，但正式線上付款與電子發票要等取得商店號後才會啟用，所以目前訂單只能由後台「手動建單」以現場收款建立。" />

    <el-card shadow="never" class="orders__block">
      <div class="orders__row">
        <el-input v-model="filters.keyword" :placeholder="canReveal ? '搜尋訂單編號或收件人' : '搜尋訂單編號'" clearable class="orders__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.orderStatus" placeholder="訂單狀態" clearable class="orders__select" @change="applyFilter">
          <el-option v-for="s in ORDER_STATUS_OPTIONS" :key="s" :label="s" :value="s" />
        </el-select>
        <el-select v-model="filters.paymentStatus" placeholder="付款狀態" clearable class="orders__select" @change="applyFilter">
          <el-option v-for="o in PAYMENT_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-button type="primary" @click="applyFilter">篩選</el-button>
        <el-button @click="clearFilter">清除</el-button>
        <el-button text type="primary" @click="showMore = !showMore">{{ showMore ? '收合進階篩選' : '進階篩選' }}</el-button>
        <span class="orders__spacer" />
        <el-button v-if="canUpdate" :loading="releasing" @click="releaseExpired">釋回逾時未付款訂單</el-button>
        <el-button v-if="canExport" @click="exportOpen = true">匯出訂單</el-button>
        <el-button v-if="canCreate" type="primary" @click="router.push('/shop/orders/new')">+ 手動建單</el-button>
      </div>
      <div v-if="showMore" class="orders__row orders__row--more">
        <el-select v-model="filters.deliveryMethod" placeholder="配送方式" clearable class="orders__select" @change="applyFilter">
          <el-option v-for="o in DELIVERY_METHOD_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="filters.paymentMethod" placeholder="付款方式" clearable class="orders__select" @change="applyFilter">
          <el-option v-for="o in PAYMENT_METHOD_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="filters.settlementStatus" placeholder="分帳標記" clearable class="orders__select" @change="applyFilter">
          <el-option v-for="o in SETTLEMENT_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="filters.member" placeholder="買家身分" clearable class="orders__select" @change="applyFilter">
          <el-option label="會員" value="yes" /><el-option label="非會員" value="no" />
        </el-select>
        <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="下單起日" end-placeholder="下單迄日" class="orders__range" @change="applyFilter" />
      </div>
      <div v-if="canUpdate" class="orders__row orders__row--batch">
        <span class="orders__muted">已勾選 {{ selectedIds.length }} 張</span>
        <el-button size="small" :disabled="selectedIds.length === 0" @click="openSettle">批次標記分帳</el-button>
      </div>
      <p class="orders__hint">
        訂單編號的前綴代表販售的俱樂部。「分帳標記」是給兩隊線下分帳用的人工旗標，系統不計算應付金額。
        <template v-if="!canReveal">你的帳號沒有檢視完整個資的權限，收件人顯示為遮罩，關鍵字只比對訂單編號。</template>
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" :description="isFiltered ? '沒有符合條件的訂單' : '目前還沒有訂單'" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id" @selection-change="onSelectionChange">
          <el-table-column v-if="canUpdate" type="selection" width="44" />
          <el-table-column label="訂單編號" min-width="140">
            <template #default="{ row }">
              <el-button text type="primary" class="orders__no" @click="go(row)">{{ row.orderNo }}</el-button>
              <el-tag v-if="row.isManual" size="small" type="info">手動建單</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="下單時間" width="150"><template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template></el-table-column>
          <el-table-column label="買家／收件人" min-width="130">
            <template #default="{ row }"><div>{{ row.recipientName || '—' }}</div><div class="orders__muted">{{ row.isMember ? '會員' : '非會員' }}・{{ row.itemCount }} 件</div></template>
          </el-table-column>
          <el-table-column label="金額" width="110"><template #default="{ row }">{{ formatMoney(row.total) }}</template></el-table-column>
          <el-table-column label="付款" width="120"><template #default="{ row }"><div>{{ row.paymentStatusLabel }}</div><div class="orders__muted">{{ row.paymentMethodLabel }}</div></template></el-table-column>
          <el-table-column label="訂單狀態" width="110"><template #default="{ row }"><el-tag :type="orderStatusTag(row.orderStatus)" size="small">{{ row.orderStatus }}</el-tag></template></el-table-column>
          <el-table-column label="配送" width="120"><template #default="{ row }"><div>{{ row.deliveryMethodLabel }}</div><div class="orders__muted">{{ row.shipmentStatusLabel }}</div></template></el-table-column>
          <el-table-column label="分帳" width="90"><template #default="{ row }"><el-tag :type="row.settlementStatus === 'settled' ? 'success' : 'info'" size="small">{{ row.settlementStatusLabel }}</el-tag></template></el-table-column>
          <el-table-column label="操作" width="80" fixed="right"><template #default="{ row }"><el-button size="small" text type="primary" @click="go(row)">查看</el-button></template></el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">
            <el-checkbox v-if="canUpdate" :model-value="selectedIds.includes(row.id)" @change="(v: unknown) => toggleSelected(row.id, v === true)" />
            {{ row.orderNo }}
          </template>
          <template #meta="{ row }">
            <el-tag :type="orderStatusTag(row.orderStatus)" size="small">{{ row.orderStatus }}</el-tag>
            <span>{{ formatMoney(row.total) }}</span>
            <span>{{ row.paymentMethodLabel }}・{{ row.paymentStatusLabel }}</span>
            <span>{{ row.deliveryMethodLabel }}</span>
            <span>{{ row.recipientName || '—' }}</span>
            <span>{{ formatDateTime(row.createdAt) }}</span>
          </template>
          <template #actions="{ row }"><el-button size="small" text type="primary" @click="go(row)">查看</el-button></template>
        </MobileCardList>
        <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="orders__pager" @current-change="load" @size-change="applyFilter" />
      </template>
    </el-card>

    <ExportPurposeDialog v-model="exportOpen" title="匯出訂單" description="匯出的訂單含完整收件人資料，範圍為目前的篩選條件（最多 20000 筆）。" :loading="exporting" @confirm="handleExport" />

    <el-dialog v-model="settleOpen" title="批次標記分帳" width="440px" :close-on-click-modal="false" class="orders__dialog">
      <el-alert v-if="settleError" :title="settleError" type="warning" show-icon class="orders__block" @close="settleError = null" />
      <p class="orders__hint">這只是給兩隊線下分帳用的人工標記，系統不計算應付金額、也不產生結算單。尚未付款的訂單會被略過。</p>
      <el-form label-position="top">
        <el-form-item label="標記為">
          <el-radio-group v-model="settleForm.status"><el-radio-button value="settled">已結算</el-radio-button><el-radio-button value="pending">待結算</el-radio-button></el-radio-group>
        </el-form-item>
        <el-form-item v-if="settleForm.status === 'settled'" label="結算日（沒填就用今天）"><el-date-picker v-model="settleForm.settledOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
        <el-form-item label="備註（選填）"><el-input v-model="settleForm.note" type="textarea" :rows="2" maxlength="200" show-word-limit /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="settleOpen = false">取消</el-button>
        <el-button type="primary" :loading="settleSaving" @click="saveSettle">確定標記 {{ selectedIds.length }} 張</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.orders__block { margin-bottom: 12px; }
.orders__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.orders__row--more, .orders__row--batch { margin-top: 10px; }
.orders__keyword { width: 240px; max-width: 100%; }
.orders__select { width: 140px; max-width: 100%; }
.orders__range { max-width: 100%; }
.orders__spacer { flex: 1; }
.orders__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.orders__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.orders__no { padding: 0; }
.orders__pager { margin-top: 12px; justify-content: flex-end; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
