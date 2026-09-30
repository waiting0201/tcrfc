<script setup lang="ts">
/**
 * 退貨與退款：案件承接前台以表單或客服信箱進來的申請，由客服在後台建立。
 * 流程：申請中 → 已核准 → 已驗收退回品 → 已退款（不需退回商品的案件核准後直接可退款），另有已駁回。
 * 🔴 「執行退款」只有系統管理員能做；LINE Pay 訂單的原路退回尚未串接（會被擋下、案件維持原狀）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import RefundCreateDialog from './parts/RefundCreateDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { listRefunds, REFUND_STATUS_OPTIONS, type RefundListItemDto } from '@/api/adminShop'
import { formatUtcDateTime } from '@/utils/formatDateTime'
import { formatMoney } from '@/utils/formatMoney'
import { refundStatusTag } from '@/utils/shopStatus'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canUpdate } = useViewUpdatePermissions('shop.refund')
const club = computed(() => activeClubId.value)

const filters = reactive({ keyword: '', status: '' })
const rows = ref<RefundListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const createOpen = ref(false)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listRefunds(club.value, { keyword: filters.keyword.trim() || undefined, status: filters.status || undefined, page: page.value, pageSize: pageSize.value })
    rows.value = result.items
    total.value = result.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '案件清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  load()
}
function clearFilter() {
  Object.assign(filters, { keyword: '', status: '' })
  applyFilter()
}
onMounted(load)
watch(club, clearFilter)
const go = (row: RefundListItemDto) => router.push(`/shop/returns/${row.id}`)
function onCreated(id: string | null) {
  if (id) router.push(`/shop/returns/${id}`)
  else load()
}
</script>

<template>
  <div class="returns">
    <PageHeader title="退貨與退款">
      <template #meta><FrontendUnitBanner module-code="S5" /></template>
    </PageHeader>

    <el-card shadow="never" class="returns__block">
      <div class="returns__row">
        <el-input v-model="filters.keyword" placeholder="搜尋訂單編號" clearable class="returns__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="案件狀態" clearable class="returns__select" @change="applyFilter">
          <el-option v-for="o in REFUND_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-button type="primary" @click="applyFilter">篩選</el-button>
        <el-button @click="clearFilter">清除</el-button>
        <span class="returns__spacer" />
        <el-button v-if="canUpdate" type="primary" @click="createOpen = true">+ 建立退貨申請</el-button>
      </div>
      <p class="returns__hint">申請中的案件排在最前面。尚未出貨的訂單請直接到「訂單」取消，不走退貨流程。退款不會寄送任何通知，需要聯繫顧客請依訂單電話處理。</p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="目前沒有符合條件的退貨案件" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="訂單編號" min-width="140" prop="orderNo" />
          <el-table-column label="案件狀態" width="130"><template #default="{ row }"><el-tag :type="refundStatusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
          <el-table-column label="退款金額" width="110"><template #default="{ row }">{{ formatMoney(row.refundAmount) }}</template></el-table-column>
          <el-table-column label="原因" min-width="160" prop="reason" />
          <el-table-column label="退回商品" width="100"><template #default="{ row }">{{ row.needsReturn ? '需要退回' : '不需退回' }}</template></el-table-column>
          <el-table-column label="付款方式" width="110" prop="paymentMethodLabel" />
          <el-table-column label="申請時間" width="150"><template #default="{ row }">{{ formatUtcDateTime(row.createdAt) }}</template></el-table-column>
          <el-table-column label="操作" width="80" fixed="right"><template #default="{ row }"><el-button size="small" text type="primary" @click="go(row)">處理</el-button></template></el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.orderNo }}</template>
          <template #meta="{ row }">
            <el-tag :type="refundStatusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag>
            <span>{{ formatMoney(row.refundAmount) }}</span><span>{{ row.needsReturn ? '需要退回' : '不需退回' }}</span><span>{{ row.reason }}</span>
          </template>
          <template #actions="{ row }"><el-button size="small" text type="primary" @click="go(row)">處理</el-button></template>
        </MobileCardList>
        <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="returns__pager" @current-change="load" @size-change="applyFilter" />
      </template>
    </el-card>

    <RefundCreateDialog v-model="createOpen" @created="onCreated" />
  </div>
</template>

<style scoped>
.returns__block { margin-bottom: 12px; }
.returns__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.returns__keyword { width: 240px; max-width: 100%; }
.returns__select { width: 150px; max-width: 100%; }
.returns__spacer { flex: 1; }
.returns__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.returns__pager { margin-top: 12px; justify-content: flex-end; }
</style>
