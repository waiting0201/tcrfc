<script setup lang="ts">
/**
 * P2 梯次與場次（對應主站規劃書 §4.4 P2，行 1097–1099；apps/api/README.md「S1-9」）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminPrograms, type AdminProgramListItemDto } from '@/api/adminPrograms'
import { listAdminProgramSessions, type AdminSessionListItemDto } from '@/api/adminProgramSessions'
import { AdminApiError } from '@/api/http'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { sessionStatusTagType } from '@/types/program'

const router = useRouter()
const club = computed(() => activeClubId.value)
const { canManageItems: canManageSessions } = useProgramPermissions()

const programs = ref<AdminProgramListItemDto[]>([])
const programLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const p of programs.value) map.set(p.id, p.nameZh || p.slug)
  return map
})

const filters = reactive({ programId: '' })

const sessions = ref<AdminSessionListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadPrograms() {
  try {
    programs.value = await listAdminPrograms(club.value)
  } catch {
    programs.value = []
  }
}

async function loadSessions() {
  loading.value = true
  loadError.value = null
  try {
    sessions.value = await listAdminProgramSessions(club.value, { programId: filters.programId || undefined })
  } catch (error) {
    sessions.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '梯次清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function bootstrap() {
  filters.programId = ''
  await Promise.all([loadPrograms(), loadSessions()])
}

onMounted(bootstrap)
watch(club, bootstrap)

function applyFilters() {
  loadSessions()
}

function clearFilters() {
  filters.programId = ''
  applyFilters()
}

function handleAdd() {
  router.push('/programs/sessions/new')
}

function handleEdit(row: AdminSessionListItemDto) {
  router.push(`/programs/sessions/${row.id}/edit`)
}

function period(row: AdminSessionListItemDto): string {
  if (!row.startOn && !row.endOn) return '—'
  return `${row.startOn ?? '—'} ～ ${row.endOn ?? '—'}`
}

function priceLabel(row: AdminSessionListItemDto): string {
  if (row.price == null) return '—'
  if (row.earlyBirdPrice != null) return `$${row.price}（早鳥 $${row.earlyBirdPrice}）`
  return `$${row.price}`
}
</script>

<template>
  <div class="session-list">
    <PageHeader title="梯次與場次">
      <template #meta>
        <FrontendUnitBanner module-code="P2" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="session-list__filters">
      <div class="session-list__filter-row">
        <el-select
          v-model="filters.programId"
          placeholder="課程／營隊項目"
          clearable
          filterable
          class="session-list__filter-select"
          @change="applyFilters"
        >
          <el-option v-for="p in programs" :key="p.id" :label="p.nameZh || p.slug" :value="p.id" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>
        <div class="session-list__filter-placeholder" />
        <el-button v-if="canManageSessions" type="primary" @click="handleAdd">+ 新增梯次</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadSessions">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="sessions.length > 0" :data="sessions" row-key="id">
        <el-table-column label="所屬項目" min-width="160">
          <template #default="{ row }">{{ programLabelById.get(row.programId) ?? row.programNameZh ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="期間" width="200">
          <template #default="{ row }">{{ period(row) }}</template>
        </el-table-column>
        <el-table-column label="名額" width="110">
          <template #default="{ row }">{{ row.enrolledCount }} / {{ row.capacity ?? '不限' }}</template>
        </el-table-column>
        <el-table-column label="費用" width="150">
          <template #default="{ row }">{{ priceLabel(row) }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="90">
          <template #default="{ row }">
            <el-tag :type="sessionStatusTagType(row.status)" size="small">{{ row.status }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">{{ canManageSessions ? '編輯' : '檢視' }}</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="找不到符合條件的梯次">
        <el-button v-if="canManageSessions" type="primary" @click="handleAdd">+ 新增第一個梯次</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.session-list__filters {
  margin-bottom: 12px;
}

.session-list__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.session-list__filter-select {
  width: 220px;
  max-width: 100%;
}

.session-list__filter-placeholder {
  flex: 1;
}
</style>
