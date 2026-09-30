<script setup lang="ts">
/**
 * 會員名單：一律顯示遮罩後的個資。完整資料只能在會員詳情由有權限的人「顯示完整資料」。
 * 沒有檢視完整個資權限的人，關鍵字只比對會員編號（後端規則，避免搜尋變成探測個資的工具）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { listAdminSeasons, type AdminSeasonListItemDto } from '@/api/adminCompetitions'
import {
  exportMembers,
  JERSEY_STATUS_OPTIONS,
  listMembers,
  MEMBER_ACCOUNT_STATUS_OPTIONS,
  MEMBER_LOCALE_OPTIONS,
  MEMBER_SIGNUP_SOURCE_OPTIONS,
  MEMBER_TIER_OPTIONS,
  MEMBERSHIP_STATUS_OPTIONS,
  type MemberListFilter,
  type MemberListItemDto,
  type MembershipSummaryDto,
} from '@/api/adminMembers'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate } = useCrudPermissions('member.account')
const canExport = usePermission('member.export')
const canReveal = usePermission('member.pii.reveal')
const club = computed(() => activeClubId.value)

interface FilterState {
  crossClub: boolean
  keyword: string
  tier: string
  membershipStatus: string
  status: string
  signupSource: string
  lineBound: '' | 'yes' | 'no'
  range: [string, string] | null
  seasonId: string
  expiringWithinDays: number | null
  jerseyStatus: string
  locale: string
}
const emptyFilter = (): FilterState => ({
  crossClub: false, keyword: '', tier: '', membershipStatus: '', status: '', signupSource: '', lineBound: '',
  range: null, seasonId: '', expiringWithinDays: null, jerseyStatus: '', locale: '',
})
const filters = reactive<FilterState>(emptyFilter())
const showMore = ref(false)

const rows = ref<MemberListItemDto[]>([])
const seasons = ref<AdminSeasonListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

const exportOpen = ref(false)
const exporting = ref(false)

function buildFilter(): MemberListFilter {
  return {
    crossClub: filters.crossClub,
    keyword: filters.keyword.trim() || undefined,
    tier: filters.tier || undefined,
    membershipStatus: filters.membershipStatus || undefined,
    status: filters.status || undefined,
    signupSource: filters.signupSource || undefined,
    lineBound: filters.lineBound === '' ? undefined : filters.lineBound === 'yes',
    registeredFrom: filters.range?.[0],
    registeredTo: filters.range?.[1],
    seasonId: filters.seasonId || undefined,
    expiringWithinDays: filters.expiringWithinDays ?? undefined,
    jerseyStatus: filters.jerseyStatus || undefined,
    locale: filters.locale || undefined,
  }
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listMembers(club.value, buildFilter(), page.value, pageSize.value)
    rows.value = result.items
    total.value = result.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '會員名單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function loadSeasons() {
  try {
    seasons.value = await listAdminSeasons(club.value)
  } catch {
    seasons.value = []
  }
}

function applyFilter() {
  page.value = 1
  load()
}
function clearFilter() {
  Object.assign(filters, emptyFilter())
  applyFilter()
}

onMounted(() => {
  load()
  loadSeasons()
})
watch(club, () => {
  Object.assign(filters, emptyFilter())
  page.value = 1
  load()
  loadSeasons()
})

const isFiltered = computed(() => JSON.stringify(filters) !== JSON.stringify(emptyFilter()))

async function handleExport(purpose: string) {
  exporting.value = true
  try {
    await exportMembers(club.value, buildFilter(), purpose)
    exportOpen.value = false
    ElMessage.success('已匯出，檔案已下載')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

function statusType(status: string): 'success' | 'warning' | 'info' | 'danger' {
  if (status === 'active') return 'success'
  if (status === 'pending') return 'warning'
  if (status === 'cancelled') return 'danger'
  return 'info'
}
function accountType(status: string): 'success' | 'warning' | 'info' | 'danger' {
  if (status === 'active') return 'success'
  if (status === 'unverified') return 'warning'
  if (status === 'suspended') return 'danger'
  return 'info'
}
function membershipText(m: MembershipSummaryDto): string {
  return `${m.clubName}・${m.seasonCode}・${m.tierLabel}`
}
function expireHint(m: MembershipSummaryDto): string {
  if (m.effectiveStatus !== 'active' || m.daysToExpire === null) return ''
  return m.daysToExpire <= 30 ? `（${m.daysToExpire} 天後到期）` : ''
}
function goDetail(row: MemberListItemDto) {
  router.push(`/members/list/${row.id}`)
}
</script>

<template>
  <div class="member-list">
    <PageHeader title="會員名單">
      <template #meta><FrontendUnitBanner module-code="K1" /></template>
    </PageHeader>

    <el-card shadow="never" class="member-list__bar">
      <div class="member-list__row">
        <el-input v-model="filters.keyword" :placeholder="canReveal ? '搜尋會員編號、姓名、Email、電話' : '搜尋會員編號'" clearable class="member-list__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.tier" placeholder="會員層級" clearable class="member-list__select" @change="applyFilter">
          <el-option v-for="o in MEMBER_TIER_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="filters.membershipStatus" placeholder="會籍狀態" clearable class="member-list__select" @change="applyFilter">
          <el-option v-for="o in MEMBERSHIP_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="filters.status" placeholder="帳號狀態" clearable class="member-list__select" @change="applyFilter">
          <el-option v-for="o in MEMBER_ACCOUNT_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-button type="primary" @click="applyFilter">篩選</el-button>
        <el-button @click="clearFilter">清除</el-button>
        <el-button text @click="showMore = !showMore">{{ showMore ? '收合進階篩選' : '進階篩選' }}</el-button>
        <span class="member-list__spacer" />
        <el-button @click="router.push('/members/list/duplicates')">重複帳號比對</el-button>
        <el-button v-if="canExport" @click="exportOpen = true">匯出名單</el-button>
        <el-button v-if="canCreate" type="primary" @click="router.push('/members/list/new')">+ 現場建立會員</el-button>
      </div>
      <div v-if="showMore" class="member-list__row member-list__row--more">
        <el-select v-model="filters.signupSource" placeholder="註冊來源" clearable class="member-list__select" @change="applyFilter">
          <el-option v-for="o in MEMBER_SIGNUP_SOURCE_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="filters.lineBound" placeholder="LINE 綁定" clearable class="member-list__select" @change="applyFilter">
          <el-option label="已綁定 LINE" value="yes" />
          <el-option label="未綁定 LINE" value="no" />
        </el-select>
        <el-select v-model="filters.seasonId" placeholder="球季" clearable class="member-list__select" @change="applyFilter">
          <el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" />
        </el-select>
        <el-select v-model="filters.jerseyStatus" placeholder="球衣狀態" clearable class="member-list__select" @change="applyFilter">
          <el-option v-for="o in JERSEY_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="filters.locale" placeholder="語系偏好" clearable class="member-list__select" @change="applyFilter">
          <el-option v-for="o in MEMBER_LOCALE_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-input-number v-model="filters.expiringWithinDays" :min="1" :max="366" :controls="false" placeholder="幾天內到期" class="member-list__days" @change="applyFilter" />
        <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="註冊起日" end-placeholder="註冊迄日" class="member-list__range" @change="applyFilter" />
      </div>
      <div class="member-list__row member-list__row--hint">
        <el-checkbox v-model="filters.crossClub" @change="applyFilter">顯示我有權限的所有俱樂部的會員</el-checkbox>
      </div>
      <p class="member-list__hint">
        名單一律遮罩個人資料，完整資料請進入會員詳情，由有權限的人「顯示完整資料」（會留下紀錄）。
        <template v-if="!canReveal">你的帳號沒有檢視完整個資的權限，搜尋只能比對會員編號。</template>
        預設只列出在目前俱樂部有會籍的會員。
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-alert v-if="rows.some((r) => r.isMasked)" type="info" :closable="false" show-icon class="member-list__masked" title="已遮罩：姓名、Email、電話顯示的是遮罩後的內容。" />
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="會員編號" width="120" prop="memberNo" />
          <el-table-column label="姓名" min-width="110">
            <template #default="{ row }">{{ row.name || '—' }}</template>
          </el-table-column>
          <el-table-column label="Email／電話" min-width="200">
            <template #default="{ row }">
              <div class="member-list__cell">{{ row.email || '—' }}</div>
              <div class="member-list__cell member-list__muted">{{ row.phone || '—' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="會籍" min-width="240">
            <template #default="{ row }">
              <div v-if="row.memberships.length === 0" class="member-list__muted">尚無會籍</div>
              <div v-for="m in row.memberships" :key="m.membershipId" class="member-list__ms">
                <span>{{ membershipText(m) }}</span>
                <el-tag :type="statusType(m.effectiveStatus)" size="small">{{ m.effectiveStatusLabel }}</el-tag>
                <span class="member-list__muted">{{ m.endOn ? `到期 ${m.endOn}` : '' }}{{ expireHint(m) }}</span>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="帳號" width="130">
            <template #default="{ row }">
              <el-tag :type="accountType(row.displayStatus)" size="small">{{ row.displayStatusLabel }}</el-tag>
              <div class="member-list__muted">{{ row.signupSourceLabel }}{{ row.lineBound ? '・已綁定 LINE' : '' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="球衣" width="90">
            <template #default="{ row }">{{ row.jerseyStatusLabel || '—' }}</template>
          </el-table-column>
          <el-table-column label="註冊時間" width="150">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="80" fixed="right">
            <template #default="{ row }"><el-button size="small" text type="primary" @click="goDetail(row)">檢視</el-button></template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.memberNo }}・{{ row.name || '—' }}</template>
          <template #meta="{ row }">
            <el-tag :type="accountType(row.displayStatus)" size="small">{{ row.displayStatusLabel }}</el-tag>
            <span>{{ row.email || '—' }}</span>
            <span>{{ row.phone || '—' }}</span>
            <span v-for="m in row.memberships" :key="m.membershipId">{{ membershipText(m) }}（{{ m.effectiveStatusLabel }}）</span>
            <span v-if="row.memberships.length === 0">尚無會籍</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="goDetail(row)">檢視</el-button>
          </template>
        </MobileCardList>
        <div class="member-list__pager">
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
      <el-empty v-else :description="isFiltered ? '沒有符合條件的會員' : '目前還沒有會員'">
        <el-button v-if="canCreate && !isFiltered" type="primary" @click="router.push('/members/list/new')">+ 現場建立會員</el-button>
        <el-button v-if="isFiltered" @click="clearFilter">清除篩選</el-button>
      </el-empty>
    </el-card>

    <ExportPurposeDialog v-model="exportOpen" title="匯出會員名單" description="依目前的篩選條件匯出，每份會籍一列，含姓名、Email、電話（不含生日）。" :loading="exporting" @confirm="handleExport" />
  </div>
</template>

<style scoped>
.member-list__bar { margin-bottom: 12px; }
.member-list__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.member-list__row--more, .member-list__row--hint { margin-top: 8px; }
.member-list__keyword { width: 260px; max-width: 100%; }
.member-list__select { width: 150px; max-width: 100%; }
.member-list__days { width: 130px; }
.member-list__range { max-width: 100%; }
.member-list__spacer { flex: 1; }
.member-list__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.member-list__masked { margin-bottom: 12px; }
.member-list__muted { color: var(--admin-text-tertiary); font-size: 12px; }
.member-list__cell { word-break: break-all; }
.member-list__ms { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-bottom: 4px; }
.member-list__pager { display: flex; justify-content: flex-end; margin-top: 16px; overflow-x: auto; }
@media (max-width: 767px) {
  .member-list__pager { justify-content: center; }
  .member-list__keyword, .member-list__select { width: 100%; }
}
</style>
