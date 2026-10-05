<script setup lang="ts">
/**
 * P3 報名管理（對應主站規劃書 §4.4 P3；apps/api/README.md「S1-9」＋「B1」節「P3 報名進階」）。
 * 只服務課程報名（`sessionId`），試訓報名在「試訓場次」底下處理。
 *
 * 進階篩選、勾選批次改狀態、候補遞補、簽到表與候補遞補提醒入口都在這裡；匯出沿用同一組篩選。
 *
 * ⚠️ **規劃書行 1106「寄送通知信（模板化）」本輪未做**——`apps/api` 完全沒有寄信通路，
 * 這裡刻意不放一個按了沒作用的按鈕；候補遞補也不會寄信，需由承辦依電話聯繫。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminProgramSessions, type AdminSessionListItemDto } from '@/api/adminProgramSessions'
import { listAdminPrograms, type AdminProgramListItemDto } from '@/api/adminPrograms'
import {
  batchUpdateRegistrationStatus,
  downloadAdminRegistrationsCsv,
  listAdminRegistrations,
  promoteAdminRegistration,
  type AdminRegistrationListItemDto,
  type ListAdminRegistrationsParams,
} from '@/api/adminRegistrations'
import type { BatchResultDto } from '@/api/adminCommon'
import { AdminApiError } from '@/api/http'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { REGISTRATION_STATUS_ORDER, registrationStatusTagType } from '@/types/program'
import { formatDateTime } from '@/utils/dateTime'

const BATCH_LIMIT = 200

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const club = computed(() => activeClubId.value)
const { canCreateRegistrations, canProcessRegistrations, canExportRegistrations } = useProgramPermissions()

const sessions = ref<AdminSessionListItemDto[]>([])
const programs = ref<AdminProgramListItemDto[]>([])
const sessionLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const s of sessions.value) {
    map.set(s.id, `${s.programNameZh ?? '（未命名項目）'}（${s.startOn ?? '—'} ～ ${s.endOn ?? '—'}）`)
  }
  return map
})

const filters = reactive({
  sessionId: '',
  programId: '',
  status: '',
  keyword: '',
  isMember: '' as '' | 'yes' | 'no',
  range: null as [string, string] | null,
})

const registrations = ref<AdminRegistrationListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const exporting = ref(false)
const busyId = ref<string | null>(null)

const selectedIds = ref<string[]>([])
const batchStatus = ref('')
const batching = ref(false)
const batchResult = ref<{ result: BatchResultDto; status: string; names: Map<string, string> } | null>(null)

function currentParams(): ListAdminRegistrationsParams {
  return {
    sessionId: filters.sessionId || undefined,
    programId: filters.programId || undefined,
    status: filters.status || undefined,
    keyword: filters.keyword.trim() || undefined,
    isMember: filters.isMember === '' ? undefined : filters.isMember === 'yes',
    dateFrom: filters.range?.[0],
    dateTo: filters.range?.[1],
  }
}

async function loadRegistrations() {
  loading.value = true
  loadError.value = null
  selectedIds.value = []
  try {
    registrations.value = await listAdminRegistrations(club.value, currentParams())
  } catch (error) {
    registrations.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '報名清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function loadOptions() {
  const [s, p] = await Promise.all([
    listAdminProgramSessions(club.value).catch(() => [] as AdminSessionListItemDto[]),
    listAdminPrograms(club.value).catch(() => [] as AdminProgramListItemDto[]),
  ])
  sessions.value = s
  programs.value = p
}

function resetFilters() {
  filters.sessionId = ''
  filters.programId = ''
  filters.status = ''
  filters.keyword = ''
  filters.isMember = ''
  filters.range = null
}

async function bootstrap() {
  resetFilters()
  // 由候補遞補提醒等頁面帶入梯次
  if (typeof route.query.sessionId === 'string') filters.sessionId = route.query.sessionId
  await Promise.all([loadOptions(), loadRegistrations()])
}

onMounted(bootstrap)
watch(club, bootstrap)

function clearFilters() {
  resetFilters()
  loadRegistrations()
}

function handleAdd() {
  router.push('/programs/enrollments/new')
}

function handleEdit(row: AdminRegistrationListItemDto) {
  router.push(`/programs/enrollments/${row.id}/edit`)
}

function openSignIn() {
  router.push(filters.sessionId ? { path: '/programs/enrollments/sign-in', query: { sessionId: filters.sessionId } } : '/programs/enrollments/sign-in')
}

const exportOpen = ref(false)

async function handleExport(purpose: string) {
  exporting.value = true
  try {
    await downloadAdminRegistrationsCsv(club.value, currentParams(), purpose)
    exportOpen.value = false
    ElMessage.success('已匯出')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

async function handlePromote(row: AdminRegistrationListItemDto) {
  try {
    await ElMessageBox.confirm(
      `確定要遞補「${row.applicantName}」嗎？遞補後狀態改為已確認並佔用名額；系統不會自動通知，請依電話聯繫。`,
      '遞補',
      { confirmButtonText: '遞補', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  busyId.value = row.id
  try {
    await promoteAdminRegistration(club.value, row.id)
    ElMessage.success(`已遞補「${row.applicantName}」`)
    await loadRegistrations()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '遞補失敗，請稍後再試')
  } finally {
    busyId.value = null
  }
}

// ── 勾選與批次 ──────────────────────────────────────────────────────────────────

function onSelectionChange(rows: AdminRegistrationListItemDto[]) {
  selectedIds.value = rows.map((r) => r.id)
}

function isSelected(id: string): boolean {
  return selectedIds.value.includes(id)
}

function toggleSelected(id: string, checked: boolean) {
  selectedIds.value = checked ? [...selectedIds.value, id] : selectedIds.value.filter((x) => x !== id)
}

const allSelected = computed(() => registrations.value.length > 0 && selectedIds.value.length === registrations.value.length)

function toggleAll(checked: boolean) {
  selectedIds.value = checked ? registrations.value.map((r) => r.id) : []
}

async function handleBatch() {
  if (!batchStatus.value) {
    ElMessage.warning('請先選擇要改成的狀態')
    return
  }
  if (selectedIds.value.length > BATCH_LIMIT) {
    ElMessage.warning(`一次最多處理 ${BATCH_LIMIT} 筆，請縮小勾選範圍`)
    return
  }
  try {
    await ElMessageBox.confirm(`確定要把勾選的 ${selectedIds.value.length} 筆報名改為「${batchStatus.value}」嗎？`, '批次改狀態', {
      confirmButtonText: '確定',
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  batching.value = true
  try {
    const status = batchStatus.value
    // 批次後清單會重新載入，先把勾選列的姓名記下來，結果視窗才能指出哪幾筆被略過
    const names = new Map(registrations.value.map((r) => [r.id, `${r.applicantName}（${r.registrationNo}）`]))
    const result = await batchUpdateRegistrationStatus(club.value, selectedIds.value, status)
    batchResult.value = { result, status, names }
    await loadRegistrations()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '批次處理失敗，請稍後再試')
  } finally {
    batching.value = false
  }
}

function skippedName(id: string): string {
  return batchResult.value?.names.get(id) ?? '這筆報名'
}
</script>

<template>
  <div class="registration-list">
    <PageHeader title="報名管理">
      <template #meta>
        <FrontendUnitBanner module-code="P3" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="registration-list__filters">
      <div class="registration-list__filter-row">
        <el-input
          v-model="filters.keyword"
          placeholder="搜尋編號、姓名、電話、Email"
          clearable
          class="registration-list__filter-input"
          @keyup.enter="loadRegistrations"
          @clear="loadRegistrations"
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select
          v-if="programs.length > 0"
          v-model="filters.programId"
          placeholder="課程／營隊項目"
          clearable
          filterable
          class="registration-list__filter-select"
          @change="loadRegistrations"
        >
          <el-option v-for="p in programs" :key="p.id" :label="p.nameZh || '（未命名）'" :value="p.id" />
        </el-select>
        <el-select
          v-if="sessions.length > 0"
          v-model="filters.sessionId"
          placeholder="梯次"
          clearable
          filterable
          class="registration-list__filter-select registration-list__filter-select--wide"
          @change="loadRegistrations"
        >
          <el-option v-for="s in sessions" :key="s.id" :label="sessionLabelById.get(s.id)" :value="s.id" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="registration-list__filter-select registration-list__filter-select--narrow" @change="loadRegistrations">
          <el-option v-for="s in REGISTRATION_STATUS_ORDER" :key="s" :label="s" :value="s" />
        </el-select>
        <el-select v-model="filters.isMember" placeholder="是否會員" clearable class="registration-list__filter-select registration-list__filter-select--narrow" @change="loadRegistrations">
          <el-option label="是會員" value="yes" />
          <el-option label="非會員" value="no" />
        </el-select>
        <el-date-picker
          v-model="filters.range"
          type="daterange"
          value-format="YYYY-MM-DD"
          range-separator="至"
          start-placeholder="報名日起"
          end-placeholder="報名日迄"
          class="registration-list__filter-range"
          @change="loadRegistrations"
        />
        <el-button type="primary" @click="loadRegistrations">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
      </div>
      <div class="registration-list__filter-row registration-list__action-row">
        <el-button @click="router.push('/programs/enrollments/waitlist')">候補遞補提醒</el-button>
        <el-button @click="openSignIn">簽到表</el-button>
        <div class="registration-list__filter-placeholder" />
        <el-button v-if="canExportRegistrations" :loading="exporting" @click="exportOpen = true">匯出 CSV</el-button>
        <el-button v-if="canCreateRegistrations" type="primary" @click="handleAdd">+ 新增報名（後台代填）</el-button>
      </div>
      <p v-if="canExportRegistrations" class="registration-list__hint">匯出會依上方目前的篩選條件產生名單，不含健康聲明。</p>
    </el-card>

    <el-card v-if="canProcessRegistrations && selectedIds.length > 0" shadow="never" class="registration-list__batch">
      <div class="registration-list__filter-row">
        <span>已勾選 {{ selectedIds.length }} 筆，改為</span>
        <el-select v-model="batchStatus" placeholder="選擇狀態" class="registration-list__filter-select registration-list__filter-select--narrow">
          <el-option v-for="s in REGISTRATION_STATUS_ORDER" :key="s" :label="s" :value="s" />
        </el-select>
        <el-button type="primary" :loading="batching" @click="handleBatch">批次改狀態</el-button>
        <el-button @click="selectedIds = []">取消勾選</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadRegistrations">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="registrations.length > 0">
        <el-table v-if="!isMobile" :data="registrations" row-key="id" @selection-change="onSelectionChange">
          <el-table-column v-if="canProcessRegistrations" type="selection" width="44" />
          <el-table-column label="報名編號" width="180">
            <template #default="{ row }">{{ row.registrationNo }}</template>
          </el-table-column>
          <el-table-column label="課程／營隊項目" min-width="160">
            <template #default="{ row }">{{ row.programNameZh ?? '—' }}</template>
          </el-table-column>
          <el-table-column label="報名人" width="120">
            <template #default="{ row }">{{ row.applicantName }}</template>
          </el-table-column>
          <el-table-column label="聯絡方式" min-width="160">
            <template #default="{ row }">
              <div>{{ row.phone || '—' }}</div>
              <div class="registration-list__email">{{ row.email || '—' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="會員" width="80">
            <template #default="{ row }">
              <el-tag :type="row.isMember ? 'success' : 'info'" size="small">{{ row.isMember ? '是' : '否' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="狀態" width="90">
            <template #default="{ row }">
              <el-tag :type="registrationStatusTagType(row.status)" size="small">{{ row.status }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="送出時間" width="160">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="150" fixed="right">
            <template #default="{ row }">
              <el-button v-if="canProcessRegistrations && row.status === '候補'" size="small" text type="primary" :loading="busyId === row.id" @click="handlePromote(row)">遞補</el-button>
              <el-button size="small" text type="primary" @click="handleEdit(row)">{{ canProcessRegistrations ? '處理' : '檢視' }}</el-button>
            </template>
          </el-table-column>
        </el-table>
        <template v-else>
          <el-checkbox v-if="canProcessRegistrations" :model-value="allSelected" class="registration-list__select-all" @change="(v: unknown) => toggleAll(v === true)">全選目前清單</el-checkbox>
          <MobileCardList :rows="registrations" row-key="id">
            <template #title="{ row }">
              <el-checkbox v-if="canProcessRegistrations" :model-value="isSelected(row.id)" @change="(v: unknown) => toggleSelected(row.id, v === true)">{{ row.applicantName }}</el-checkbox>
              <span v-else>{{ row.applicantName }}</span>
            </template>
            <template #meta="{ row }">
              <el-tag :type="registrationStatusTagType(row.status)" size="small">{{ row.status }}</el-tag>
              <el-tag :type="row.isMember ? 'success' : 'info'" size="small">{{ row.isMember ? '會員' : '非會員' }}</el-tag>
              <span>{{ row.programNameZh ?? '—' }}</span>
              <span>{{ row.registrationNo }}</span>
              <span>電話 {{ row.phone || '—' }}</span>
              <span>{{ formatDateTime(row.createdAt) }}</span>
            </template>
            <template #actions="{ row }">
              <el-button v-if="canProcessRegistrations && row.status === '候補'" size="small" text type="primary" :loading="busyId === row.id" @click="handlePromote(row)">遞補</el-button>
              <el-button size="small" text type="primary" @click="handleEdit(row)">{{ canProcessRegistrations ? '處理' : '檢視' }}</el-button>
            </template>
          </MobileCardList>
        </template>
      </template>
      <el-empty v-else description="找不到符合條件的報名" />
    </el-card>

    <el-dialog
      :model-value="batchResult !== null"
      title="批次處理結果"
      width="460px"
      class="registration-list__result"
      @update:model-value="(v: boolean) => { if (!v) batchResult = null }"
    >
      <template v-if="batchResult">
        <p>已改為「{{ batchResult.status }}」：{{ batchResult.result.updatedCount }} 筆。</p>
        <template v-if="batchResult.result.skipped.length > 0">
          <p>略過 {{ batchResult.result.skipped.length }} 筆：</p>
          <ul class="registration-list__skipped">
            <li v-for="s in batchResult.result.skipped" :key="s.id">{{ skippedName(s.id) }}：{{ s.reason }}</li>
          </ul>
        </template>
      </template>
      <template #footer>
        <el-button type="primary" @click="batchResult = null">知道了</el-button>
      </template>
    </el-dialog>

    <ExportPurposeDialog
      v-model="exportOpen"
      title="匯出課程報名名單"
      description="名單含姓名、電話、Email、家長資料，不含健康聲明。會依上方目前的篩選條件匯出。"
      :loading="exporting"
      @confirm="handleExport"
    />
  </div>
</template>

<style scoped>
.registration-list__filters {
  margin-bottom: 12px;
}

.registration-list__batch {
  margin-bottom: 12px;
}

.registration-list__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.registration-list__action-row {
  margin-top: 10px;
}

.registration-list__filter-input {
  width: 240px;
  max-width: 100%;
}

.registration-list__filter-select {
  width: 160px;
  max-width: 100%;
}

.registration-list__filter-select--narrow {
  width: 130px;
}

.registration-list__filter-select--wide {
  width: 260px;
}

.registration-list__filter-range {
  max-width: 100%;
}

.registration-list__filter-placeholder {
  flex: 1;
}

.registration-list__hint {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.registration-list__email {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.registration-list__select-all {
  margin-bottom: 8px;
}

.registration-list__skipped {
  margin: 0;
  padding-left: 18px;
  font-size: 13px;
  line-height: 1.7;
}
</style>
