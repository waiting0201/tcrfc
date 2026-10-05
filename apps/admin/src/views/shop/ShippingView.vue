<script setup lang="ts">
/**
 * 出貨與物流：出貨清單、揀貨單、出貨單列印、批次出貨、物流單號批次回填。
 * 預設列出「已付款、備貨中、已出貨」的訂單；待領取且已過領取期限的算「逾期」（讀取時換算）。
 * 本系統不串接物流商，也不會寄送任何出貨通知。收件人依「檢視完整個資」權限遮罩；
 * 取得完整出貨單資料的每一次動作都會留下紀錄。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useViewUpdatePermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  batchShip,
  DELIVERY_METHOD_OPTIONS,
  getDispatchSlips,
  getPickingList,
  importShipments,
  listShipments,
  type DispatchSlipDto,
  type PickingListDto,
  type ShipmentImportResultDto,
  type ShipmentListItemDto,
} from '@/api/adminShop'
import { formatDateTime } from '@/utils/dateTime'
import { orderStatusTag } from '@/utils/shopStatus'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canUpdate } = useViewUpdatePermissions('shop.shipment')
const canReveal = usePermission('shop.order.reveal')
const club = computed(() => activeClubId.value)
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

type Mode = 'list' | 'picking' | 'slips'
const mode = ref<Mode>('list')

const emptyFilter = () => ({ keyword: '', orderStatus: '', deliveryMethod: '', pickupStatus: '' })
const filters = reactive(emptyFilter())
const rows = ref<ShipmentListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const selectedIds = ref<string[]>([])

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listShipments(club.value, {
      keyword: filters.keyword.trim() || undefined,
      orderStatus: filters.orderStatus || undefined,
      deliveryMethod: filters.deliveryMethod || undefined,
      pickupStatus: filters.pickupStatus || undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    rows.value = result.items
    total.value = result.totalCount
    selectedIds.value = selectedIds.value.filter((id) => result.items.some((r) => r.orderId === id))
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = errorText(error, '出貨清單載入失敗，請稍後再試')
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
  mode.value = 'list'
  page.value = 1
  selectedIds.value = []
  load()
})
function onSelectionChange(selection: ShipmentListItemDto[]) {
  selectedIds.value = selection.map((r) => r.orderId)
}
function toggleSelected(id: string, checked: boolean) {
  selectedIds.value = checked ? [...selectedIds.value, id] : selectedIds.value.filter((x) => x !== id)
}

// ── 揀貨單 ──
const picking = ref<PickingListDto | null>(null)
const printing = ref(false)
async function openPicking(all: boolean) {
  if (!all && selectedIds.value.length === 0) return
  if (!all && selectedIds.value.length > 200) return ElMessage.warning('揀貨單一次最多 200 張訂單')
  printing.value = true
  try {
    picking.value = await getPickingList(club.value, all ? [] : selectedIds.value)
    mode.value = 'picking'
  } catch (error) {
    ElMessage.error(errorText(error, '揀貨單載入失敗，請稍後再試'))
  } finally {
    printing.value = false
  }
}

// ── 出貨單 ──
const slips = ref<DispatchSlipDto[]>([])
async function openSlips() {
  if (selectedIds.value.length === 0) return
  if (selectedIds.value.length > 100) return ElMessage.warning('出貨單一次最多 100 張')
  printing.value = true
  try {
    slips.value = await getDispatchSlips(club.value, selectedIds.value)
    mode.value = 'slips'
  } catch (error) {
    ElMessage.error(errorText(error, '出貨單載入失敗，請稍後再試'))
  } finally {
    printing.value = false
  }
}
const slipsMasked = computed(() => slips.value.some((s) => s.isMasked))
function backToList() {
  mode.value = 'list'
}

// ── 批次出貨 ──
const batchOpen = ref(false)
const batchCarrier = ref('')
const batching = ref(false)
function resultAlert(title: string, updated: number, skipped: string[]) {
  if (skipped.length > 0) {
    const detail = [...new Set(skipped)].join('；')
    ElMessageBox.alert(`已更新 ${updated} 筆，略過 ${skipped.length} 筆。略過原因：${detail}`, title, { confirmButtonText: '知道了' })
  } else {
    ElMessage.success(`已更新 ${updated} 筆`)
  }
}
async function doBatchShip() {
  batching.value = true
  try {
    const result = await batchShip(club.value, { ids: selectedIds.value, carrier: batchCarrier.value.trim() || null })
    batchOpen.value = false
    resultAlert('批次出貨結果', result.updatedCount, result.skipped.map((s) => s.reason))
    selectedIds.value = []
    await load()
  } catch (error) {
    ElMessage.error(errorText(error, '批次出貨失敗，請稍後再試'))
  } finally {
    batching.value = false
  }
}

// ── 物流單號檔案匯入 ──
const importOpen = ref(false)
const importFile = ref<File | null>(null)
const importing = ref(false)
const importError = ref<string | null>(null)
const importResult = ref<ShipmentImportResultDto | null>(null)
function openImport() {
  importFile.value = null
  importError.value = null
  importResult.value = null
  importOpen.value = true
}
function onFilePicked(event: Event) {
  const el = event.target as HTMLInputElement
  importFile.value = el.files?.[0] ?? null
  el.value = ''
  importError.value = null
}
async function doImport() {
  const file = importFile.value
  if (!file) return (importError.value = '請先選擇 CSV 檔案')
  if (file.size > 2 * 1024 * 1024) return (importError.value = '檔案不可超過 2 MB')
  importing.value = true
  importError.value = null
  try {
    importResult.value = await importShipments(club.value, file)
    await load()
  } catch (error) {
    importError.value = errorText(error, '匯入失敗，請稍後再試')
  } finally {
    importing.value = false
  }
}

function pickupTag(status?: string | null): 'success' | 'warning' | 'danger' | 'info' {
  if (status === 'picked_up') return 'success'
  if (status === 'overdue') return 'danger'
  if (status === 'waiting') return 'warning'
  return 'info'
}
const printNow = () => window.print()
</script>

<template>
  <div class="shipping">
    <PageHeader title="出貨與物流">
      <template #meta><FrontendUnitBanner module-code="S4" /></template>
    </PageHeader>

    <!-- 揀貨單：可直接列印 -->
    <section v-if="mode === 'picking' && picking" class="ship-print">
      <div class="no-print shipping__bar">
        <el-button @click="backToList"><el-icon><ArrowLeft /></el-icon>返回出貨清單</el-button>
        <el-button type="primary" @click="printNow">列印揀貨單</el-button>
      </div>
      <div class="ship-print__sheet">
        <h2 class="ship-print__title">揀貨單</h2>
        <p class="ship-print__meta">共 {{ picking.orderCount }} 張訂單、{{ picking.totalQuantity }} 件商品・產生時間 {{ formatDateTime(picking.generatedAt) }}（只含已付款與備貨中的訂單，依商品規格編號加總）</p>
        <p v-if="picking.lines.length === 0" class="ship-print__meta">目前沒有需要揀貨的品項。</p>
        <div v-else class="ship-print__scroll">
          <table class="ship-print__table">
            <thead><tr><th>商品規格編號</th><th>商品</th><th>尺寸／顏色</th><th class="ship-print__num">數量</th><th class="ship-print__num">訂單數</th><th class="ship-print__check">已揀</th></tr></thead>
            <tbody>
              <tr v-for="l in picking.lines" :key="l.sku"><td>{{ l.sku }}</td><td>{{ l.productName }}</td><td>{{ l.variantLabel || '—' }}</td><td class="ship-print__num">{{ l.quantity }}</td><td class="ship-print__num">{{ l.orderCount }}</td><td class="ship-print__check" /></tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <!-- 出貨單：一張訂單一頁 -->
    <section v-else-if="mode === 'slips'" class="ship-print">
      <div class="no-print shipping__bar">
        <el-button @click="backToList"><el-icon><ArrowLeft /></el-icon>返回出貨清單</el-button>
        <el-button type="primary" @click="printNow">列印出貨單（{{ slips.length }} 張）</el-button>
      </div>
      <el-alert v-if="slipsMasked" class="no-print shipping__block" type="warning" show-icon :closable="false" title="你的帳號沒有檢視完整個資的權限，收件人資料是遮罩後的值，這樣的出貨單無法用來寄件。" />
      <div v-for="s in slips" :key="s.orderId" class="ship-print__sheet ship-print__slip">
        <h2 class="ship-print__title">出貨單 {{ s.orderNo }}</h2>
        <dl class="ship-print__dl">
          <div><dt>配送方式</dt><dd>{{ s.deliveryMethodLabel }}</dd></div>
          <div><dt>收件人</dt><dd>{{ s.recipientName || '—' }}</dd></div>
          <div><dt>電話</dt><dd>{{ s.recipientPhone || '—' }}</dd></div>
          <div v-if="s.deliveryMethod === 'home_delivery'"><dt>地址</dt><dd>{{ s.recipientAddress || '—' }}</dd></div>
          <div v-if="s.storeBranchCode"><dt>門市代碼</dt><dd>{{ s.storeBranchCode }}</dd></div>
          <div v-if="s.customerNote"><dt>顧客備註</dt><dd>{{ s.customerNote }}</dd></div>
        </dl>
        <div class="ship-print__scroll">
          <table class="ship-print__table">
            <thead><tr><th>商品規格編號</th><th>商品</th><th>尺寸／顏色</th><th class="ship-print__num">數量</th></tr></thead>
            <tbody><tr v-for="(i, idx) in s.items" :key="idx"><td>{{ i.sku }}</td><td>{{ i.productName }}</td><td>{{ i.variantLabel || '—' }}</td><td class="ship-print__num">{{ i.quantity }}</td></tr></tbody>
          </table>
        </div>
      </div>
    </section>

    <template v-else>
      <el-card shadow="never" class="shipping__block">
        <div class="shipping__row">
          <el-input v-model="filters.keyword" :placeholder="canReveal ? '搜尋訂單編號或收件人' : '搜尋訂單編號'" clearable class="shipping__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
            <template #prefix><el-icon><Search /></el-icon></template>
          </el-input>
          <el-select v-model="filters.orderStatus" placeholder="訂單狀態" clearable class="shipping__select" @change="applyFilter">
            <el-option label="已付款" value="已付款" /><el-option label="備貨中" value="備貨中" /><el-option label="已出貨" value="已出貨" />
          </el-select>
          <el-select v-model="filters.deliveryMethod" placeholder="配送方式" clearable class="shipping__select" @change="applyFilter">
            <el-option v-for="o in DELIVERY_METHOD_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
          <el-select v-model="filters.pickupStatus" placeholder="領取狀態" clearable class="shipping__select" @change="applyFilter">
            <el-option label="待領取" value="waiting" /><el-option label="已領取" value="picked_up" /><el-option label="逾期未領" value="overdue" />
          </el-select>
          <el-button type="primary" @click="applyFilter">篩選</el-button>
          <el-button @click="clearFilter">清除</el-button>
        </div>
        <div class="shipping__row shipping__row--batch">
          <span class="shipping__muted">已勾選 {{ selectedIds.length }} 張</span>
          <el-button size="small" :loading="printing" @click="openPicking(true)">列印全部待出貨的揀貨單</el-button>
          <el-button size="small" :disabled="selectedIds.length === 0" :loading="printing" @click="openPicking(false)">勾選訂單的揀貨單</el-button>
          <el-button size="small" :disabled="selectedIds.length === 0" :loading="printing" @click="openSlips">列印出貨單</el-button>
          <template v-if="canUpdate">
            <el-button size="small" :disabled="selectedIds.length === 0" @click="batchCarrier = ''; batchOpen = true">批次標記已出貨</el-button>
            <el-button size="small" @click="openImport">匯入物流單號檔案</el-button>
          </template>
        </div>
        <p class="shipping__hint">超商取貨缺門市代碼、狀態不對的訂單在批次出貨時會被略過並列出原因。系統不串接物流商，也不會寄送出貨通知。<template v-if="!canReveal">你的帳號沒有檢視完整個資的權限，收件人顯示為遮罩。</template></p>
      </el-card>

      <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
      <el-card v-else-if="loadError" shadow="never">
        <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
      </el-card>
      <el-card v-else shadow="never">
        <el-empty v-if="rows.length === 0" description="目前沒有需要出貨或追蹤的訂單" />
        <template v-else>
          <el-table v-if="!isMobile" :data="rows" row-key="orderId" @selection-change="onSelectionChange">
            <el-table-column type="selection" width="44" />
            <el-table-column label="訂單編號" min-width="140"><template #default="{ row }"><el-button text type="primary" class="shipping__no" @click="router.push(`/shop/orders/${row.orderId}`)">{{ row.orderNo }}</el-button></template></el-table-column>
            <el-table-column label="訂單狀態" width="100"><template #default="{ row }"><el-tag :type="orderStatusTag(row.orderStatus)" size="small">{{ row.orderStatus }}</el-tag></template></el-table-column>
            <el-table-column label="配送" width="100" prop="deliveryMethodLabel" />
            <el-table-column label="收件人" min-width="110"><template #default="{ row }">{{ row.recipientName || '—' }}<span class="shipping__muted">・{{ row.itemCount }} 件</span></template></el-table-column>
            <el-table-column label="物流" min-width="170">
              <template #default="{ row }">
                <div v-if="row.shipment">{{ row.shipment.carrier || '—' }} {{ row.shipment.trackingNo || '尚未回填單號' }}</div>
                <div v-else class="shipping__muted">尚未出貨</div>
                <div v-if="row.shipment?.storeBranchCode" class="shipping__muted">門市代碼 {{ row.shipment.storeBranchCode }}</div>
              </template>
            </el-table-column>
            <el-table-column label="領取狀態" width="140">
              <template #default="{ row }">
                <template v-if="row.shipment?.pickupStatusLabel">
                  <el-tag :type="pickupTag(row.shipment.pickupStatus)" size="small">{{ row.shipment.pickupStatusLabel }}</el-tag>
                  <div v-if="row.shipment.pickupDeadlineOn" class="shipping__muted">期限 {{ row.shipment.pickupDeadlineOn }}</div>
                </template>
                <span v-else class="shipping__muted">—</span>
              </template>
            </el-table-column>
            <el-table-column label="下單時間" width="150"><template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template></el-table-column>
          </el-table>
          <MobileCardList v-else :rows="rows" row-key="orderId">
            <template #title="{ row }">
              <el-checkbox :model-value="selectedIds.includes(row.orderId)" @change="(v: unknown) => toggleSelected(row.orderId, v === true)" />
              {{ row.orderNo }}
            </template>
            <template #meta="{ row }">
              <el-tag :type="orderStatusTag(row.orderStatus)" size="small">{{ row.orderStatus }}</el-tag>
              <span>{{ row.deliveryMethodLabel }}</span><span>{{ row.recipientName || '—' }}</span>
              <span v-if="row.shipment?.trackingNo">單號 {{ row.shipment.trackingNo }}</span>
              <el-tag v-if="row.shipment?.pickupStatusLabel" :type="pickupTag(row.shipment.pickupStatus)" size="small">{{ row.shipment.pickupStatusLabel }}</el-tag>
            </template>
            <template #actions="{ row }"><el-button size="small" text type="primary" @click="router.push(`/shop/orders/${row.orderId}`)">查看訂單</el-button></template>
          </MobileCardList>
          <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="shipping__pager" @current-change="load" @size-change="applyFilter" />
        </template>
      </el-card>
    </template>

    <el-dialog v-model="batchOpen" title="批次標記已出貨" width="440px" :close-on-click-modal="false" class="shipping__dialog">
      <p class="shipping__hint">將勾選的 {{ selectedIds.length }} 張訂單標為已出貨。超商取貨缺門市代碼、狀態不對的會被略過。物流單號可稍後用「匯入物流單號檔案」回填。</p>
      <el-form label-position="top"><el-form-item label="物流商（選填）"><el-input v-model="batchCarrier" maxlength="64" placeholder="例如 黑貓宅急便" /></el-form-item></el-form>
      <template #footer>
        <el-button @click="batchOpen = false">取消</el-button>
        <el-button type="primary" :loading="batching" @click="doBatchShip">確定出貨</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="importOpen" title="匯入物流單號檔案" width="560px" :close-on-click-modal="false" class="shipping__dialog">
      <p class="shipping__hint">
        上傳 CSV 檔（不超過 2 MB、2000 列）。第一列是表頭，順序不拘：「訂單編號」「物流單號」必填，「物流商」「門市代碼」選填。
        已付款或備貨中的訂單會一併標為已出貨；已出貨或已完成的只更新物流資料；其餘略過。
      </p>
      <el-alert v-if="importError" :title="importError" type="warning" show-icon class="shipping__block" @close="importError = null" />
      <div class="shipping__file">
        <label class="shipping__file-btn">選擇檔案<input type="file" accept=".csv,text/csv" class="shipping__file-input" @change="onFilePicked"></label>
        <span class="shipping__muted">{{ importFile ? importFile.name : '尚未選擇檔案' }}</span>
      </div>
      <template v-if="importResult">
        <el-alert class="shipping__block" type="success" show-icon :closable="false" :title="`已更新 ${importResult.updatedCount} 筆，略過 ${importResult.skipped.length} 筆`" />
        <el-table v-if="importResult.skipped.length > 0" :data="importResult.skipped" size="small" max-height="240">
          <el-table-column label="列" width="60" prop="row" />
          <el-table-column label="訂單編號" min-width="120"><template #default="{ row }">{{ row.orderNo || '—' }}</template></el-table-column>
          <el-table-column label="略過原因" min-width="180" prop="reason" />
        </el-table>
      </template>
      <template #footer>
        <el-button @click="importOpen = false">關閉</el-button>
        <el-button type="primary" :loading="importing" :disabled="!importFile" @click="doImport">開始匯入</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.shipping__block { margin-bottom: 12px; }
.shipping__bar { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 12px; }
.shipping__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.shipping__row--batch { margin-top: 10px; }
.shipping__keyword { width: 240px; max-width: 100%; }
.shipping__select { width: 140px; max-width: 100%; }
.shipping__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.shipping__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.shipping__no { padding: 0; }
.shipping__pager { margin-top: 12px; justify-content: flex-end; }
.shipping__file { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; margin-bottom: 12px; }
.shipping__file-btn { display: inline-block; padding: 6px 14px; border: 1px solid var(--admin-border-input); border-radius: 4px; cursor: pointer; font-size: 13px; }
.shipping__file-input { display: none; }
.ship-print__sheet { background: var(--admin-bg-surface); border: 1px solid var(--admin-border); border-radius: 6px; padding: 16px; margin-bottom: 16px; min-width: 0; }
.ship-print__title { margin: 0 0 8px; font-size: 18px; }
.ship-print__meta { margin: 0 0 12px; font-size: 13px; color: var(--admin-text-secondary); line-height: 1.6; }
.ship-print__scroll { overflow-x: auto; }
.ship-print__table { width: 100%; min-width: 480px; border-collapse: collapse; font-size: 14px; }
.ship-print__table th, .ship-print__table td { border: 1px solid var(--admin-border); padding: 8px 10px; text-align: left; }
.ship-print__num { text-align: right !important; width: 80px; }
.ship-print__check { width: 64px; }
.ship-print__dl { margin: 0 0 12px; display: flex; flex-direction: column; gap: 4px; font-size: 14px; }
.ship-print__dl > div { display: flex; gap: 8px; }
.ship-print__dl dt { width: 80px; flex-shrink: 0; color: var(--admin-text-secondary); }
.ship-print__dl dd { margin: 0; word-break: break-word; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>

<style>
@media print {
  .admin-layout__aside,
  .admin-layout__header,
  .page-header,
  .no-print,
  .el-drawer,
  .el-overlay {
    display: none !important;
  }
  html, body { background: #fff !important; height: auto !important; }
  .admin-layout { display: block !important; height: auto !important; background: #fff !important; }
  .admin-layout__body, .admin-layout__main { display: block !important; height: auto !important; overflow: visible !important; padding: 0 !important; background: #fff !important; }
  .ship-print__sheet { border: none !important; padding: 0 !important; background: #fff !important; color: #000 !important; }
  .ship-print__sheet * { color: #000 !important; }
  .ship-print__scroll { overflow: visible !important; }
  .ship-print__table { min-width: 0 !important; }
  .ship-print__table th, .ship-print__table td { border-color: #000 !important; }
  .ship-print__slip { break-after: page; page-break-after: always; }
  .ship-print__check { height: 32px; }
}
</style>
