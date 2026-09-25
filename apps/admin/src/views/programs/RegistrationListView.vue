<script setup lang="ts">
/**
 * P3 報名管理（對應主站規劃書 §4.4 P3，行 1101–1108；apps/api/README.md「S1-9」）。
 * 只服務課程報名（`sessionId`），不含 P4 試訓（`S2-4`）。
 *
 * ⚠️ **規劃書行 1106「寄送通知信（模板化）」本輪未做**——`apps/api` 完全沒有寄信通路，
 * `EmailLog.type` 值域也沒有課程通知（見 apps/api/README.md「S1-9」「規劃書沒寫清楚」第 1 點）。
 * 這裡刻意不放一個按了沒作用的按鈕，也不自行做出寄信功能，依任務指示原樣回報。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminProgramSessions, type AdminSessionListItemDto } from '@/api/adminProgramSessions'
import {
  listAdminRegistrations,
  downloadAdminRegistrationsCsv,
  type AdminRegistrationListItemDto,
} from '@/api/adminRegistrations'
import { AdminApiError } from '@/api/http'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { REGISTRATION_STATUS_ORDER, registrationStatusTagType } from '@/types/program'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()
const club = computed(() => activeClubId.value)
const { canCreateRegistrations, canProcessRegistrations, canExportRegistrations } = useProgramPermissions()

const sessions = ref<AdminSessionListItemDto[]>([])
const sessionLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const s of sessions.value) {
    map.set(s.id, `${s.programNameZh ?? '（未命名項目）'}（${s.startOn ?? '—'} ～ ${s.endOn ?? '—'}）`)
  }
  return map
})

const filters = reactive({ sessionId: '', status: '' })

const registrations = ref<AdminRegistrationListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const exporting = ref(false)

async function loadSessions() {
  try {
    sessions.value = await listAdminProgramSessions(club.value)
  } catch {
    sessions.value = []
  }
}

async function loadRegistrations() {
  loading.value = true
  loadError.value = null
  try {
    registrations.value = await listAdminRegistrations(club.value, {
      sessionId: filters.sessionId || undefined,
      status: filters.status || undefined,
    })
  } catch (error) {
    registrations.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '報名清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function bootstrap() {
  filters.sessionId = ''
  filters.status = ''
  await Promise.all([loadSessions(), loadRegistrations()])
}

onMounted(bootstrap)
watch(club, bootstrap)

function applyFilters() {
  loadRegistrations()
}

function clearFilters() {
  filters.sessionId = ''
  filters.status = ''
  applyFilters()
}

function handleAdd() {
  router.push('/programs/enrollments/new')
}

function handleEdit(row: AdminRegistrationListItemDto) {
  router.push(`/programs/enrollments/${row.id}/edit`)
}

async function handleExport() {
  exporting.value = true
  try {
    await downloadAdminRegistrationsCsv(club.value, {
      sessionId: filters.sessionId || undefined,
      status: filters.status || undefined,
    })
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
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
        <el-select
          v-model="filters.sessionId"
          placeholder="梯次"
          clearable
          filterable
          class="registration-list__filter-select registration-list__filter-select--wide"
          @change="applyFilters"
        >
          <el-option v-for="s in sessions" :key="s.id" :label="sessionLabelById.get(s.id)" :value="s.id" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="registration-list__filter-select" @change="applyFilters">
          <el-option v-for="s in REGISTRATION_STATUS_ORDER" :key="s" :label="s" :value="s" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>
        <div class="registration-list__filter-placeholder" />
        <el-button v-if="canExportRegistrations" :loading="exporting" @click="handleExport">匯出 CSV</el-button>
        <el-button v-if="canCreateRegistrations" type="primary" @click="handleAdd">+ 新增報名（後台代填）</el-button>
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
      <el-table v-if="registrations.length > 0" :data="registrations" row-key="id">
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
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">{{ canProcessRegistrations ? '處理' : '檢視' }}</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="找不到符合條件的報名" />
    </el-card>
  </div>
</template>

<style scoped>
.registration-list__filters {
  margin-bottom: 12px;
}

.registration-list__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.registration-list__filter-select {
  width: 160px;
  max-width: 100%;
}

.registration-list__filter-select--wide {
  width: 260px;
}

.registration-list__filter-placeholder {
  flex: 1;
}

.registration-list__email {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}
</style>
