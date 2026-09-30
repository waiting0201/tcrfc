<script setup lang="ts">
/**
 * 庫存：清單與異動紀錄。庫存量與已保留量只透過「庫存異動」改動，每一筆都留下數量、原因與經辦人。
 * 可售量＝庫存量－已保留量。並行下單由系統在交易內加鎖，不會超賣；調整到低於已保留量會被擋下。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  createInventoryMovement,
  listInventory,
  listInventoryMovements,
  type InventoryAdjustType,
  type InventoryItemDto,
  type InventoryMovementDto,
} from '@/api/adminShop'
import { formatDateTime } from '@/utils/dateTime'

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canUpdate } = useViewUpdatePermissions('shop.inventory')
const club = computed(() => activeClubId.value)

const tab = ref<'stock' | 'movements'>(route.query.tab === 'movements' ? 'movements' : 'stock')
watch(tab, (t) => router.replace({ query: { ...route.query, tab: t } }))
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

// ══ 庫存清單 ══
const filters = reactive({ keyword: '', lowStockOnly: false, status: '' })
const rows = ref<InventoryItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listInventory(club.value, {
      keyword: filters.keyword.trim() || undefined,
      lowStockOnly: filters.lowStockOnly || undefined,
      status: filters.status || undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    rows.value = result.items
    total.value = result.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = errorText(error, '庫存清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  load()
}
function clearFilter() {
  Object.assign(filters, { keyword: '', lowStockOnly: false, status: '' })
  applyFilter()
}

// ══ 異動紀錄 ══
const mvFilters = reactive({ type: '', range: null as [string, string] | null, variantId: '', variantLabel: '' })
const movements = ref<InventoryMovementDto[]>([])
const mvLoading = ref(false)
const mvError = ref<string | null>(null)
const mvPage = ref(1)
const mvPageSize = ref(20)
const mvTotal = ref(0)
// 只列契約已明確寫出代碼的類型（售出扣減、取消回補的代碼契約未寫，列表仍會顯示，只是不提供篩選）
const MOVEMENT_TYPES = [
  { value: 'stock_in', label: '進貨' },
  { value: 'stocktake', label: '盤點' },
  { value: 'damage', label: '報損' },
  { value: 'adjust', label: '調整' },
  { value: 'reserve', label: '下單保留' },
  { value: 'release', label: '釋回保留' },
  { value: 'return_restock', label: '退貨回補' },
]

async function loadMovements() {
  mvLoading.value = true
  mvError.value = null
  try {
    const result = await listInventoryMovements(club.value, {
      variantId: mvFilters.variantId || undefined,
      type: mvFilters.type || undefined,
      from: mvFilters.range?.[0],
      to: mvFilters.range?.[1],
      page: mvPage.value,
      pageSize: mvPageSize.value,
    })
    movements.value = result.items
    mvTotal.value = result.totalCount
  } catch (error) {
    movements.value = []
    mvTotal.value = 0
    mvError.value = errorText(error, '異動紀錄載入失敗，請稍後再試')
  } finally {
    mvLoading.value = false
  }
}
function applyMvFilter() {
  mvPage.value = 1
  loadMovements()
}
function clearMvFilter() {
  Object.assign(mvFilters, { type: '', range: null, variantId: '', variantLabel: '' })
  applyMvFilter()
}
function viewMovements(row: InventoryItemDto) {
  Object.assign(mvFilters, { type: '', range: null, variantId: row.variantId, variantLabel: `${row.productName} ${row.label || row.sku}` })
  tab.value = 'movements'
  applyMvFilter()
}

onMounted(() => {
  load()
  loadMovements()
})
watch(club, () => {
  Object.assign(filters, { keyword: '', lowStockOnly: false, status: '' })
  Object.assign(mvFilters, { type: '', range: null, variantId: '', variantLabel: '' })
  page.value = 1
  mvPage.value = 1
  load()
  loadMovements()
})

// ══ 調整庫存 ══
const adjOpen = ref(false)
const adjSaving = ref(false)
const adjError = ref<string | null>(null)
const adjTarget = ref<InventoryItemDto | null>(null)
const adjForm = reactive({ type: 'stock_in' as InventoryAdjustType, quantity: 1 as number | null, reason: '' })
const ADJ_TYPES: { value: InventoryAdjustType; label: string; help: string }[] = [
  { value: 'stock_in', label: '進貨', help: '增加庫存，數量填正數。' },
  { value: 'damage', label: '報損', help: '減少庫存，數量填正數，原因必填。' },
  { value: 'adjust', label: '調整', help: '數量可填正數（增加）或負數（減少），不能是 0，原因必填。' },
  { value: 'stocktake', label: '盤點', help: '數量填「實際盤點到的庫存總數」，系統會自動算出差額，原因預設「盤點」。' },
]
const adjHelp = computed(() => ADJ_TYPES.find((t) => t.value === adjForm.type)?.help ?? '')

function openAdjust(row: InventoryItemDto) {
  adjTarget.value = row
  Object.assign(adjForm, { type: 'stock_in', quantity: 1, reason: '' })
  adjError.value = null
  adjOpen.value = true
}
function onTypeChange() {
  adjForm.quantity = adjForm.type === 'stocktake' ? adjTarget.value?.stockQty ?? 0 : 1
}
async function saveAdjust() {
  const target = adjTarget.value
  if (!target) return
  adjError.value = null
  const q = adjForm.quantity
  if (q === null || !Number.isInteger(q)) return (adjError.value = '請輸入整數數量')
  if ((adjForm.type === 'stock_in' || adjForm.type === 'damage') && q <= 0) return (adjError.value = '數量必須大於 0')
  if (adjForm.type === 'adjust' && q === 0) return (adjError.value = '調整數量不能是 0')
  if (adjForm.type === 'stocktake' && q < 0) return (adjError.value = '盤點數量不能是負數')
  if ((adjForm.type === 'damage' || adjForm.type === 'adjust') && !adjForm.reason.trim()) return (adjError.value = '報損與調整必須填寫原因')
  adjSaving.value = true
  try {
    await createInventoryMovement(club.value, { variantId: target.variantId, type: adjForm.type, quantity: q, reason: adjForm.reason.trim() || undefined })
    adjOpen.value = false
    ElMessage.success('已記錄庫存異動')
    await Promise.all([load(), loadMovements()])
  } catch (error) {
    adjError.value = errorText(error, '調整失敗，請稍後再試')
  } finally {
    adjSaving.value = false
  }
}

const signed = (n: number) => (n > 0 ? `+${n}` : String(n))
</script>

<template>
  <div class="inventory">
    <PageHeader title="庫存">
      <template #meta><FrontendUnitBanner module-code="S2" /></template>
    </PageHeader>

    <el-tabs v-model="tab">
      <el-tab-pane label="庫存清單" name="stock">
        <el-card shadow="never" class="inventory__bar">
          <div class="inventory__row">
            <el-input v-model="filters.keyword" placeholder="搜尋商品名稱或規格編號" clearable class="inventory__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
              <template #prefix><el-icon><Search /></el-icon></template>
            </el-input>
            <el-select v-model="filters.status" placeholder="販售狀態" clearable class="inventory__select" @change="applyFilter">
              <el-option label="販售中" value="active" />
              <el-option label="停售" value="inactive" />
            </el-select>
            <el-switch v-model="filters.lowStockOnly" active-text="只看需要補貨的" @change="applyFilter" />
            <el-button type="primary" @click="applyFilter">篩選</el-button>
            <el-button @click="clearFilter">清除</el-button>
          </div>
          <p class="inventory__hint">依可售量由少到多排列。「需要補貨」＝販售中且可售量不高於門檻（規格自己的門檻，沒有就用商店設定，預設 5）。庫存量只能用「調整庫存」修改。</p>
        </el-card>
        <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
        <el-card v-else-if="loadError" shadow="never">
          <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
        </el-card>
        <el-card v-else shadow="never">
          <el-empty v-if="rows.length === 0" :description="filters.lowStockOnly ? '目前沒有需要補貨的規格' : '目前沒有符合條件的規格'" />
          <template v-else>
            <el-table v-if="!isMobile" :data="rows" row-key="variantId">
              <el-table-column label="商品" min-width="180"><template #default="{ row }">{{ row.productName }}</template></el-table-column>
              <el-table-column label="規格編號" min-width="120" prop="sku" />
              <el-table-column label="尺寸／顏色" min-width="100"><template #default="{ row }">{{ row.label || '—' }}</template></el-table-column>
              <el-table-column label="庫存量" width="90" prop="stockQty" />
              <el-table-column label="已保留" width="90" prop="reservedQty" />
              <el-table-column label="可售量" width="110">
                <template #default="{ row }">
                  <strong :class="{ 'inventory__low': row.isLowStock }">{{ row.availableQty }}</strong>
                  <el-tag v-if="row.isLowStock" type="warning" size="small" class="inventory__tag">補貨</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="販售" width="80"><template #default="{ row }"><el-tag :type="row.status === 'active' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
              <el-table-column label="操作" width="170" fixed="right">
                <template #default="{ row }">
                  <el-button v-if="canUpdate" size="small" text type="primary" @click="openAdjust(row)">調整庫存</el-button>
                  <el-button size="small" text @click="viewMovements(row)">異動紀錄</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="rows" row-key="variantId">
              <template #title="{ row }">{{ row.productName }}・{{ row.label || row.sku }}</template>
              <template #meta="{ row }">
                <span>庫存 {{ row.stockQty }}</span><span>保留 {{ row.reservedQty }}</span>
                <span>可售 <strong :class="{ 'inventory__low': row.isLowStock }">{{ row.availableQty }}</strong></span>
                <el-tag v-if="row.isLowStock" type="warning" size="small">補貨</el-tag>
              </template>
              <template #actions="{ row }">
                <el-button v-if="canUpdate" size="small" text type="primary" @click="openAdjust(row)">調整庫存</el-button>
                <el-button size="small" text @click="viewMovements(row)">異動紀錄</el-button>
              </template>
            </MobileCardList>
            <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="inventory__pager" @current-change="load" @size-change="applyFilter" />
          </template>
        </el-card>
      </el-tab-pane>

      <el-tab-pane label="異動紀錄" name="movements" lazy>
        <el-card shadow="never" class="inventory__bar">
          <div class="inventory__row">
            <el-tag v-if="mvFilters.variantId" closable @close="mvFilters.variantId = ''; mvFilters.variantLabel = ''; applyMvFilter()">{{ mvFilters.variantLabel }}</el-tag>
            <el-select v-model="mvFilters.type" placeholder="異動類型" clearable class="inventory__select" @change="applyMvFilter">
              <el-option v-for="t in MOVEMENT_TYPES" :key="t.value" :label="t.label" :value="t.value" />
            </el-select>
            <el-date-picker v-model="mvFilters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="開始日" end-placeholder="結束日" class="inventory__range" @change="applyMvFilter" />
            <el-button type="primary" @click="applyMvFilter">篩選</el-button>
            <el-button @click="clearMvFilter">清除</el-button>
          </div>
          <p class="inventory__hint">由新到舊。「下單保留」「釋回保留」改變的是已保留量，其餘改變的是庫存量；數字有正負號。</p>
        </el-card>
        <el-card v-if="mvLoading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
        <el-card v-else-if="mvError" shadow="never">
          <el-empty :description="mvError"><el-button type="primary" @click="loadMovements">重新載入</el-button></el-empty>
        </el-card>
        <el-card v-else shadow="never">
          <el-empty v-if="movements.length === 0" description="沒有符合條件的異動紀錄" />
          <template v-else>
            <el-table v-if="!isMobile" :data="movements" row-key="id">
              <el-table-column label="時間" width="150"><template #default="{ row }">{{ formatDateTime(row.occurredAt) }}</template></el-table-column>
              <el-table-column label="商品／規格" min-width="180"><template #default="{ row }"><div>{{ row.productName }}</div><div class="inventory__muted">{{ row.sku }}</div></template></el-table-column>
              <el-table-column label="類型" width="100" prop="movementTypeLabel" />
              <el-table-column label="數量" width="80"><template #default="{ row }"><strong>{{ signed(row.quantity) }}</strong></template></el-table-column>
              <el-table-column label="異動後（庫存／保留）" width="160"><template #default="{ row }">{{ row.stockAfter }} ／ {{ row.reservedAfter }}</template></el-table-column>
              <el-table-column label="原因" min-width="120"><template #default="{ row }">{{ row.reason || '—' }}</template></el-table-column>
              <el-table-column label="訂單" width="130">
                <template #default="{ row }"><el-button v-if="row.orderId" size="small" text type="primary" @click="router.push(`/shop/orders/${row.orderId}`)">{{ row.orderNo }}</el-button><span v-else class="inventory__muted">—</span></template>
              </el-table-column>
              <el-table-column label="經辦人" width="100"><template #default="{ row }">{{ row.handledByName || '系統' }}</template></el-table-column>
            </el-table>
            <MobileCardList v-else :rows="movements" row-key="id">
              <template #title="{ row }">{{ row.productName }}・{{ row.movementTypeLabel }} {{ signed(row.quantity) }}</template>
              <template #meta="{ row }">
                <span>{{ formatDateTime(row.occurredAt) }}</span><span>{{ row.sku }}</span>
                <span>異動後 {{ row.stockAfter }} ／ {{ row.reservedAfter }}</span>
                <span v-if="row.reason">{{ row.reason }}</span><span>{{ row.handledByName || '系統' }}</span>
              </template>
              <template #actions="{ row }"><el-button v-if="row.orderId" size="small" text type="primary" @click="router.push(`/shop/orders/${row.orderId}`)">查看訂單 {{ row.orderNo }}</el-button></template>
            </MobileCardList>
            <el-pagination v-model:current-page="mvPage" v-model:page-size="mvPageSize" :total="mvTotal" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="inventory__pager" @current-change="loadMovements" @size-change="applyMvFilter" />
          </template>
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="adjOpen" title="調整庫存" width="480px" :close-on-click-modal="false" class="inventory__dialog">
      <p v-if="adjTarget" class="inventory__target">{{ adjTarget.productName }}・{{ adjTarget.label || adjTarget.sku }}<br><span class="inventory__muted">目前庫存 {{ adjTarget.stockQty }}、已保留 {{ adjTarget.reservedQty }}、可售 {{ adjTarget.availableQty }}</span></p>
      <el-alert v-if="adjError" :title="adjError" type="warning" show-icon class="inventory__block" @close="adjError = null" />
      <el-form label-position="top">
        <el-form-item label="異動類型">
          <el-radio-group v-model="adjForm.type" @change="onTypeChange">
            <el-radio-button v-for="t in ADJ_TYPES" :key="t.value" :value="t.value">{{ t.label }}</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <p class="inventory__hint">{{ adjHelp }}</p>
        <el-form-item :label="adjForm.type === 'stocktake' ? '實際盤點的庫存總數' : '數量'"><el-input-number v-model="adjForm.quantity" :controls="false" style="width: 100%" /></el-form-item>
        <el-form-item label="原因" :required="adjForm.type === 'damage' || adjForm.type === 'adjust'"><el-input v-model="adjForm.reason" type="textarea" :rows="2" maxlength="200" show-word-limit /></el-form-item>
      </el-form>
      <p class="inventory__hint">扣到低於已保留量或變成負數會被擋下，庫存不變、也不留紀錄。</p>
      <template #footer>
        <el-button @click="adjOpen = false">取消</el-button>
        <el-button type="primary" :loading="adjSaving" @click="saveAdjust">確認調整</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.inventory__bar { margin-bottom: 12px; }
.inventory__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.inventory__keyword { width: 260px; max-width: 100%; }
.inventory__select { width: 150px; max-width: 100%; }
.inventory__range { max-width: 100%; }
.inventory__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.inventory__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.inventory__tag { margin-left: 6px; }
.inventory__low { color: var(--admin-warning-text); }
.inventory__pager { margin-top: 12px; justify-content: flex-end; }
.inventory__block { margin-bottom: 12px; }
.inventory__target { margin: 0 0 12px; font-size: 14px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
