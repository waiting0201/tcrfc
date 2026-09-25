<script setup lang="ts">
/**
 * P1 課程／營隊項目（對應主站規劃書 §4.4 P1，行 1093–1095；apps/api/README.md「S1-9」）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminPrograms, type AdminProgramListItemDto } from '@/api/adminPrograms'
import { AdminApiError } from '@/api/http'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { PROGRAM_TYPE_LABEL, PROGRAM_TYPE_ORDER, PROGRAM_STATUS_LABEL, type ProgramType } from '@/types/program'

const router = useRouter()
const club = computed(() => activeClubId.value)
const { canManageItems } = useProgramPermissions()

const filters = reactive({ programType: '' as ProgramType | '' })

const programs = ref<AdminProgramListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadPrograms() {
  loading.value = true
  loadError.value = null
  try {
    programs.value = await listAdminPrograms(club.value, { programType: filters.programType || undefined })
  } catch (error) {
    programs.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '課程／營隊項目載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(loadPrograms)
watch(club, () => {
  filters.programType = ''
  loadPrograms()
})

function clearFilters() {
  filters.programType = ''
  loadPrograms()
}

function handleAdd() {
  router.push('/programs/items/new')
}

function handleEdit(row: AdminProgramListItemDto) {
  router.push(`/programs/items/${row.id}/edit`)
}
</script>

<template>
  <div class="program-item-list">
    <PageHeader title="課程／營隊項目">
      <template #meta>
        <FrontendUnitBanner module-code="P1" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="program-item-list__filters">
      <div class="program-item-list__filter-row">
        <el-select v-model="filters.programType" placeholder="類型" clearable class="program-item-list__filter-select" @change="loadPrograms">
          <el-option v-for="t in PROGRAM_TYPE_ORDER" :key="t" :label="PROGRAM_TYPE_LABEL[t]" :value="t" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>
        <div class="program-item-list__filter-placeholder" />
        <el-button v-if="canManageItems" type="primary" @click="handleAdd">+ 新增項目</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadPrograms">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="programs.length > 0" :data="programs" row-key="id">
        <el-table-column label="名稱" min-width="180">
          <template #default="{ row }">{{ row.nameZh || '（未命名）' }}</template>
        </el-table-column>
        <el-table-column label="類型" width="110">
          <template #default="{ row }">
            {{ row.programType ? PROGRAM_TYPE_LABEL[row.programType as ProgramType] ?? row.programType : '—' }}
          </template>
        </el-table-column>
        <el-table-column label="適合對象" width="140">
          <template #default="{ row }">{{ row.audience || '—' }}</template>
        </el-table-column>
        <el-table-column label="年齡範圍" width="110">
          <template #default="{ row }">
            <span v-if="row.ageMin == null && row.ageMax == null">—</span>
            <span v-else>{{ row.ageMin ?? '不限' }} – {{ row.ageMax ?? '不限' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="梯次數" width="90">
          <template #default="{ row }">{{ row.sessionCount }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="100">
          <template #default="{ row }">
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">
              {{ PROGRAM_STATUS_LABEL[row.status as 'draft' | 'published'] ?? row.status }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">{{ canManageItems ? '編輯' : '檢視' }}</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="找不到符合條件的課程／營隊項目">
        <el-button v-if="canManageItems" type="primary" @click="handleAdd">+ 新增第一個項目</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.program-item-list__filters {
  margin-bottom: 12px;
}

.program-item-list__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.program-item-list__filter-select {
  width: 160px;
  max-width: 100%;
}

.program-item-list__filter-placeholder {
  flex: 1;
}
</style>
