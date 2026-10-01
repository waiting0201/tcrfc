<script setup lang="ts">
/**
 * 「待確認申請」分頁：網頁會員送出的升級／續會申請，客服核對款項後直接帶入手動開通。
 * 姓名、Email、電話依「完整個資」權限由後端遮罩；開通成功後後端會一併結案，這裡重新讀取就會消失。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import MembershipActivateDialog from './MembershipActivateDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import {
  listMembershipApplications,
  type ActivatePrefill,
  type ApplicationStatus,
  type MembershipApplicationDto,
  type MembershipPlanListItemDto,
} from '@/api/adminMemberships'
import { errorMessage, formatMoney, type TagType } from './membershipHelpers'
import { formatDateTime } from '@/utils/dateTime'

defineProps<{ plans: MembershipPlanListItemDto[] }>()
const emit = defineEmits<{ (e: 'count', total: number): void }>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate } = useCrudPermissions('member.membership')
const club = computed(() => activeClubId.value)

// 日常中文狀態；後端有給 statusLabel 時以後端為準
const STATUS_OPTIONS: { value: ApplicationStatus; label: string; tag: TagType }[] = [
  { value: 'created', label: '待確認款項', tag: 'warning' },
  { value: 'pending_payment', label: '等待付款', tag: 'info' },
  { value: 'paid', label: '已付款', tag: 'success' },
  { value: 'activation_failed', label: '已付款但開通失敗', tag: 'danger' },
  { value: 'activated', label: '已開通', tag: 'success' },
  { value: 'expired', label: '已逾期', tag: 'info' },
  { value: 'cancelled', label: '已取消', tag: 'info' },
  { value: 'refunded', label: '已退款', tag: 'info' },
]
function statusOf(row: MembershipApplicationDto) {
  return STATUS_OPTIONS.find((s) => s.value === row.status)
}
const statusLabel = (row: MembershipApplicationDto) => row.statusLabel || statusOf(row)?.label || row.status
const statusTag = (row: MembershipApplicationDto): TagType => statusOf(row)?.tag ?? 'info'
/** 只有還沒結案的申請可以開通。 */
const canActivateRow = (row: MembershipApplicationDto) =>
  canCreate.value && ['created', 'pending_payment', 'paid', 'activation_failed'].includes(row.status)

const filters = ref<{ status: ApplicationStatus; keyword: string }>({ status: 'created', keyword: '' })
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const rows = ref<MembershipApplicationDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
let seq = 0

async function load() {
  const mine = ++seq
  loading.value = true
  loadError.value = null
  try {
    const res = await listMembershipApplications(club.value, {
      status: filters.value.status,
      keyword: filters.value.keyword.trim() || undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    if (mine !== seq) return
    rows.value = res.items
    total.value = res.totalCount
    // 分頁標籤上的數字：只有「待確認款項」且沒有關鍵字時才代表真正的待辦數
    if (filters.value.status === 'created' && !filters.value.keyword.trim()) emit('count', res.totalCount)
  } catch (error) {
    if (mine !== seq) return
    rows.value = []
    total.value = 0
    loadError.value = errorMessage(error, '待確認申請載入失敗，請稍後再試')
  } finally {
    if (mine === seq) loading.value = false
  }
}
function search() {
  page.value = 1
  load()
}
function clearFilters() {
  filters.value = { status: 'created', keyword: '' }
  search()
}

onMounted(load)
watch(club, () => {
  filters.value = { status: 'created', keyword: '' }
  page.value = 1
  load()
})

// ── 開通 ──
const activateOpen = ref(false)
const prefill = ref<ActivatePrefill | null>(null)
function openActivate(row: MembershipApplicationDto) {
  prefill.value = {
    member: { id: row.memberId, memberNo: row.memberNo ?? '', name: row.memberName ?? '', email: row.memberEmail, phone: row.memberPhone },
    planId: row.planId,
    amount: row.amount,
    orderNo: row.orderNo,
  }
  activateOpen.value = true
}
function onActivated() {
  ElMessage.success('申請已結案，會籍已開通')
  load()
}
</script>

<template>
  <div class="ap-tab">
    <el-card shadow="never" class="ap-tab__bar">
      <div class="ap-tab__row">
        <el-input v-model="filters.keyword" placeholder="搜尋會員編號或申請編號" clearable class="ap-tab__keyword" @keyup.enter="search" @clear="search">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="申請狀態" class="ap-tab__select" @change="search">
          <el-option v-for="s in STATUS_OPTIONS" :key="s.value" :label="s.label" :value="s.value" />
        </el-select>
        <el-button type="primary" @click="search">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
      </div>
      <p class="ap-tab__hint">
        這裡是網頁會員在官網送出的升級／續會申請。請先確認款項已收到，再按「開通」；開通後申請會自動結案並從此清單消失。
        姓名、Email、電話依你的權限遮罩顯示。「已付款但開通失敗」的申請需要客服人工處理。
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="orderNo">
          <el-table-column label="申請時間" width="150"><template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template></el-table-column>
          <el-table-column label="會員" min-width="190">
            <template #default="{ row }">
              <div>{{ row.memberNo || '—' }}｜{{ row.memberName || '—' }}</div>
              <div class="ap-tab__muted">{{ row.memberEmail || '' }} {{ row.memberPhone || '' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="申請方案" min-width="150">
            <template #default="{ row }">{{ row.seasonCode ? `${row.seasonCode}｜` : '' }}{{ row.planName || row.planCode || '—' }}</template>
          </el-table-column>
          <el-table-column label="應收金額" width="110"><template #default="{ row }">{{ formatMoney(row.amount) }}</template></el-table-column>
          <el-table-column label="狀態" width="140">
            <template #default="{ row }">
              <el-tag :type="statusTag(row)" size="small">{{ statusLabel(row) }}</el-tag>
              <div v-if="row.expiresAt && row.status !== 'activated'" class="ap-tab__muted">保留至 {{ formatDateTime(row.expiresAt) }}</div>
            </template>
          </el-table-column>
          <el-table-column label="申請編號" min-width="150" prop="orderNo" />
          <el-table-column label="操作" width="100" fixed="right">
            <template #default="{ row }">
              <el-button v-if="canActivateRow(row)" size="small" type="primary" @click="openActivate(row)">開通</el-button>
              <span v-else-if="row.activatedAt" class="ap-tab__muted">{{ formatDateTime(row.activatedAt) }} 開通</span>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="orderNo">
          <template #title="{ row }">{{ row.memberNo || '—' }}｜{{ row.memberName || '—' }}</template>
          <template #meta="{ row }">
            <el-tag :type="statusTag(row)" size="small">{{ statusLabel(row) }}</el-tag>
            <span>{{ row.planName || row.planCode || '' }}</span>
            <span>{{ formatMoney(row.amount) }}</span>
            <span>{{ formatDateTime(row.createdAt) }}</span>
          </template>
          <template #actions="{ row }">
            <el-button v-if="canActivateRow(row)" size="small" type="primary" @click="openActivate(row)">開通</el-button>
          </template>
        </MobileCardList>
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          class="ap-tab__pager"
          background
          :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'"
          :page-sizes="[20, 50, 100]"
          :total="total"
          @current-change="load"
          @size-change="search"
        />
      </template>
      <el-empty v-else :description="filters.status === 'created' && !filters.keyword.trim() ? '目前沒有待確認的申請' : '沒有符合條件的申請'" />
    </el-card>

    <MembershipActivateDialog v-model="activateOpen" :plans="plans" :prefill="prefill" @done="onActivated" />
  </div>
</template>

<style scoped>
.ap-tab { min-width: 0; }
.ap-tab__bar { margin-bottom: 12px; }
.ap-tab__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.ap-tab__keyword { width: 220px; max-width: 100%; }
.ap-tab__select { width: 170px; max-width: 100%; }
.ap-tab__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.ap-tab__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.ap-tab__pager { margin-top: 12px; justify-content: flex-end; flex-wrap: wrap; }
</style>
