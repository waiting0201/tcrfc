<script setup lang="ts">
/** 「會籍」分頁：會籍清單（預設只看 30 天內到期＝到期提醒）、手動開通／續會、批次到期、匯出續會名單。 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import MembershipActivateDialog from './MembershipActivateDialog.vue'
import MembershipFreeDialog from './MembershipFreeDialog.vue'
import MembershipExpireBatchDialog from './MembershipExpireBatchDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { listMemberships, downloadRenewalExport, type MembershipDetailDto, type MembershipListItemDto, type MembershipPlanListItemDto, type RenewalExportKind } from '@/api/adminMemberships'
import { effectiveStatusLabel, errorMessage, expiryText, formatMoney, STATUS_TAG, tierLabel } from './membershipHelpers'

const props = defineProps<{ plans: MembershipPlanListItemDto[]; seasons: { id: string; code: string }[] }>()

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate } = useCrudPermissions('member.membership')
const canExport = usePermission('member.export')
const canViewMember = usePermission('member.account.view')
const club = computed(() => activeClubId.value)

const EXPIRING_OPTIONS = [7, 14, 30, 60, 90]
function emptyFilters() {
  return { keyword: '', tier: '', status: '', seasonId: '', planId: '', expiring: 30 as number | null }
}
const filters = ref(emptyFilters())
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const rows = ref<MembershipListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

const filteredPlans = computed(() =>
  filters.value.seasonId ? props.plans.filter((p) => p.seasonId === filters.value.seasonId) : props.plans,
)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const f = filters.value
    const res = await listMemberships(club.value, {
      keyword: f.keyword.trim() || undefined,
      tier: f.tier || undefined,
      status: f.status || undefined,
      seasonId: f.seasonId || undefined,
      planId: f.planId || undefined,
      expiringWithinDays: f.expiring ?? undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    rows.value = res.items
    total.value = res.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = errorMessage(error, '會籍清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}

function search() {
  page.value = 1
  load()
}
function clearFilters() {
  filters.value = emptyFilters()
  search()
}
function onSeasonChange() {
  if (filters.value.planId && !filteredPlans.value.some((p) => p.id === filters.value.planId)) filters.value.planId = ''
  search()
}

onMounted(load)
watch(club, () => {
  filters.value = emptyFilters()
  page.value = 1
  load()
})

function openDetail(row: MembershipListItemDto) {
  router.push(`/members/plans/memberships/${row.membershipId}`)
}

// ── 對話框 ──
const activateOpen = ref(false)
const freeOpen = ref(false)
const expireOpen = ref(false)
function onCreated(detail: MembershipDetailDto) {
  router.push(`/members/plans/memberships/${detail.membership.membershipId}`)
}

// ── 匯出續會名單：先選範圍，再填用途 ──
const exportChoiceOpen = ref(false)
const exportPurposeOpen = ref(false)
const exportBusy = ref(false)
const exportChoice = ref<{ kind: RenewalExportKind; days: number; seasonId: string }>({ kind: 'expiring', days: 30, seasonId: '' })

function openExport() {
  exportChoice.value = { kind: 'expiring', days: filters.value.expiring ?? 30, seasonId: filters.value.seasonId }
  exportChoiceOpen.value = true
}
function exportNext() {
  exportChoiceOpen.value = false
  exportPurposeOpen.value = true
}
async function doExport(purpose: string) {
  exportBusy.value = true
  try {
    const c = exportChoice.value
    await downloadRenewalExport(club.value, { kind: c.kind, days: c.days, seasonId: c.seasonId || undefined, purpose })
    exportPurposeOpen.value = false
    ElMessage.success('已匯出續會名單')
  } catch (error) {
    ElMessage.error(errorMessage(error, '匯出失敗，請稍後再試'))
  } finally {
    exportBusy.value = false
  }
}
</script>

<template>
  <div class="ms-tab">
    <el-card shadow="never" class="ms-tab__bar">
      <div class="ms-tab__row">
        <el-input v-model="filters.keyword" placeholder="搜尋會員編號" clearable class="ms-tab__keyword" @keyup.enter="search" @clear="search">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.expiring" placeholder="到期時間" clearable class="ms-tab__select" @change="search">
          <el-option v-for="d in EXPIRING_OPTIONS" :key="d" :label="`${d} 天內到期`" :value="d" />
        </el-select>
        <el-select v-model="filters.tier" placeholder="會員層級" clearable class="ms-tab__select" @change="search">
          <el-option label="一般會員" value="registered" />
          <el-option label="付費球迷會員" value="fan_club" />
        </el-select>
        <el-select v-model="filters.status" placeholder="會籍狀態" clearable class="ms-tab__select" @change="search">
          <el-option label="待確認" value="pending" />
          <el-option label="有效" value="active" />
          <el-option label="已到期" value="expired" />
          <el-option label="已取消" value="cancelled" />
        </el-select>
        <el-select v-model="filters.seasonId" placeholder="球季" clearable class="ms-tab__select" @change="onSeasonChange">
          <el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" />
        </el-select>
        <el-select v-model="filters.planId" placeholder="方案" clearable class="ms-tab__select" @change="search">
          <el-option v-for="p in filteredPlans" :key="p.id" :label="`${p.seasonCode}｜${p.nameZh || p.code}`" :value="p.id" />
        </el-select>
        <el-button type="primary" @click="search">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
      </div>
      <div class="ms-tab__row ms-tab__actions">
        <el-button v-if="canCreate" type="primary" @click="activateOpen = true">手動開通／續會</el-button>
        <el-button v-if="canCreate" @click="freeOpen = true">建立一般會員會籍</el-button>
        <el-button v-if="canUpdate" @click="expireOpen = true">球季末批次到期</el-button>
        <el-button v-if="canExport" @click="openExport">匯出續會名單</el-button>
      </div>
      <p class="ms-tab__hint">
        預設只列出 30 天內到期的會籍，方便客服提醒續會；「有效」與否以到期日為準，到期日已過就算已到期。
        姓名為遮罩顯示（個資已遮罩），要查看完整資料請到會員資料頁。
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="membershipId">
          <el-table-column label="會員編號" width="120"><template #default="{ row }">{{ row.memberNo || '—' }}</template></el-table-column>
          <el-table-column label="姓名（已遮罩）" min-width="120"><template #default="{ row }">{{ row.memberName || '—' }}</template></el-table-column>
          <el-table-column label="層級" width="120"><template #default="{ row }">{{ tierLabel(row) }}</template></el-table-column>
          <el-table-column label="狀態" width="90">
            <template #default="{ row }"><el-tag :type="STATUS_TAG[row.effectiveStatus as keyof typeof STATUS_TAG]" size="small">{{ effectiveStatusLabel(row) }}</el-tag></template>
          </el-table-column>
          <el-table-column label="到期日" min-width="150">
            <template #default="{ row }">
              {{ row.endOn || '—' }}
              <div v-if="expiryText(row.daysToExpire)" class="ms-tab__muted">{{ expiryText(row.daysToExpire) }}</div>
            </template>
          </el-table-column>
          <el-table-column label="球季" width="90" prop="seasonCode" />
          <el-table-column label="方案" min-width="130"><template #default="{ row }">{{ row.planName || '—' }}</template></el-table-column>
          <el-table-column label="卡數" width="70" prop="cardCount" />
          <el-table-column label="已付總額" width="110"><template #default="{ row }">{{ formatMoney(row.paidTotal) }}</template></el-table-column>
          <el-table-column label="操作" width="160" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="openDetail(row)">會籍詳情</el-button>
              <el-button v-if="canViewMember" size="small" text @click="router.push(`/members/list/${row.memberId}`)">會員</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="membershipId">
          <template #title="{ row }">{{ row.memberNo || '—' }}｜{{ row.memberName || '—' }}</template>
          <template #meta="{ row }">
            <el-tag :type="STATUS_TAG[row.effectiveStatus as keyof typeof STATUS_TAG]" size="small">{{ effectiveStatusLabel(row) }}</el-tag>
            <span>{{ tierLabel(row) }}</span>
            <span>{{ row.seasonCode }}</span>
            <span>{{ row.endOn || '—' }} {{ expiryText(row.daysToExpire) }}</span>
            <span>{{ row.planName || '' }}</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="openDetail(row)">會籍詳情</el-button>
            <el-button v-if="canViewMember" size="small" text @click="router.push(`/members/list/${row.memberId}`)">會員</el-button>
          </template>
        </MobileCardList>
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          class="ms-tab__pager"
          background
          :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'"
          :page-sizes="[20, 50, 100]"
          :total="total"
          @current-change="load"
          @size-change="search"
        />
      </template>
      <el-empty v-else description="目前沒有符合條件的會籍" />
    </el-card>

    <MembershipActivateDialog v-model="activateOpen" :plans="plans" @done="onCreated" />
    <MembershipFreeDialog v-model="freeOpen" :seasons="seasons" @done="onCreated" />
    <MembershipExpireBatchDialog v-model="expireOpen" :seasons="seasons" @done="load" />

    <el-dialog v-model="exportChoiceOpen" title="匯出續會名單" width="min(420px, 94vw)" :close-on-click-modal="false">
      <el-form label-position="top">
        <el-form-item label="名單種類">
          <el-radio-group v-model="exportChoice.kind">
            <el-radio value="expiring">即將到期</el-radio>
            <el-radio value="expired">已到期尚未續會</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item v-if="exportChoice.kind === 'expiring'" label="幾天內到期">
          <el-input-number v-model="exportChoice.days" :min="1" :max="366" />
        </el-form-item>
        <el-form-item label="球季（不選＝全部）">
          <el-select v-model="exportChoice.seasonId" clearable placeholder="全部球季" style="width: 100%">
            <el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="exportChoiceOpen = false">取消</el-button>
        <el-button type="primary" @click="exportNext">下一步：填寫用途</el-button>
      </template>
    </el-dialog>
    <ExportPurposeDialog
      v-model="exportPurposeOpen"
      title="匯出續會名單"
      description="名單含姓名、Email、電話，用於聯繫會員續會。"
      :loading="exportBusy"
      @confirm="doExport"
    />
  </div>
</template>

<style scoped>
.ms-tab { min-width: 0; }
.ms-tab__bar { margin-bottom: 12px; }
.ms-tab__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.ms-tab__actions { margin-top: 10px; }
.ms-tab__keyword { width: 180px; max-width: 100%; }
.ms-tab__select { width: 150px; max-width: 100%; }
.ms-tab__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.ms-tab__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.ms-tab__pager { margin-top: 12px; justify-content: flex-end; flex-wrap: wrap; }
</style>
