<script setup lang="ts">
/**
 * C1 球隊（對應主站規劃書 §4.3 C1，行 1053–1062；apps/api/README.md「S1-7」）。
 * 這裡管理的是一線隊／學院梯隊的球隊主檔，前台對應一線隊與學院梯隊各頁。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { AdminApiError } from '@/api/http'
import { TEAM_GENDER_LABEL, TEAM_TYPE_LABEL, type TeamGender, type TeamType } from '@/types/team'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()
const club = computed(() => activeClubId.value)

const teams = ref<AdminTeamAdminListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadTeams() {
  loading.value = true
  loadError.value = null
  try {
    teams.value = (await listAdminClubTeams(club.value)).sort((a, b) => a.sortOrder - b.sortOrder)
  } catch (error) {
    teams.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '球隊清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(loadTeams)
watch(club, loadTeams)

function handleAdd() {
  router.push('/teams/clubs/new')
}

function handleEdit(row: AdminTeamAdminListItemDto) {
  router.push(`/teams/clubs/${row.id}/edit`)
}
</script>

<template>
  <div class="team-list">
    <PageHeader title="球隊">
      <template #meta>
        <FrontendUnitBanner module-code="C1" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="team-list__toolbar">
      <div class="team-list__toolbar-row">
        <el-button type="primary" @click="handleAdd">+ 新增球隊</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="5" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadTeams">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="teams.length > 0" :data="teams" row-key="id">
        <el-table-column label="排序" width="72">
          <template #default="{ row }">{{ row.sortOrder }}</template>
        </el-table-column>
        <el-table-column label="隊別代號" width="120">
          <template #default="{ row }">{{ row.code }}</template>
        </el-table-column>
        <el-table-column label="名稱" min-width="180">
          <template #default="{ row }">{{ row.nameZh || '（未命名）' }}</template>
        </el-table-column>
        <el-table-column label="類型" width="110">
          <template #default="{ row }">{{ TEAM_TYPE_LABEL[row.type as TeamType] ?? row.type }}</template>
        </el-table-column>
        <el-table-column label="性別" width="100">
          <template #default="{ row }">{{ TEAM_GENDER_LABEL[row.gender as TeamGender] ?? row.gender }}</template>
        </el-table-column>
        <el-table-column label="更新時間" width="160">
          <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="目前還沒有任何球隊">
        <el-button type="primary" @click="handleAdd">+ 新增第一支球隊</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.team-list__toolbar {
  margin-bottom: 12px;
}

.team-list__toolbar-row {
  display: flex;
  justify-content: flex-end;
}
</style>
