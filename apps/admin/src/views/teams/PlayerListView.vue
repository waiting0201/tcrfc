<script setup lang="ts">
/**
 * C2 球員（對應主站規劃書 §4.3 C2，行 1063–1069；apps/api/README.md「S1-7」「S1-7a」）。
 * 這裡管理的是一線隊／學院梯隊的球員名冊，前台對應各球隊頁的球員卡與球員頁。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { listAdminPlayers, type AdminPlayerListItemDto } from '@/api/adminPlayers'
import { AdminApiError } from '@/api/http'
import {
  PLAYER_STATUS_LABEL,
  PLAYER_STATUS_ORDER,
  PORTRAIT_CONSENT_STATUS_LABEL,
  type PlayerStatus,
  type PortraitConsentStatus,
} from '@/types/team'

const router = useRouter()
const club = computed(() => activeClubId.value)

const teams = ref<AdminTeamAdminListItemDto[]>([])
const teamLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const t of teams.value) map.set(t.id, t.nameZh || t.code)
  return map
})

const filters = reactive({
  teamId: '',
  status: '' as PlayerStatus | '',
})

const players = ref<AdminPlayerListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadTeams() {
  try {
    teams.value = await listAdminClubTeams(club.value)
  } catch {
    teams.value = []
  }
}

async function loadPlayers() {
  loading.value = true
  loadError.value = null
  try {
    players.value = await listAdminPlayers(club.value, {
      teamId: filters.teamId || undefined,
      status: filters.status || undefined,
    })
  } catch (error) {
    players.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '球員清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function bootstrap() {
  filters.teamId = ''
  filters.status = ''
  await Promise.all([loadTeams(), loadPlayers()])
}

onMounted(bootstrap)
watch(club, bootstrap)

function applyFilters() {
  loadPlayers()
}

function clearFilters() {
  filters.teamId = ''
  filters.status = ''
  applyFilters()
}

function handleAdd() {
  router.push('/teams/players/new')
}

function handleEdit(row: AdminPlayerListItemDto) {
  router.push(`/teams/players/${row.id}/edit`)
}

function portraitTagType(status: string): 'success' | 'warning' | 'info' {
  if (status === 'consented' || status === 'consented_by_guardian') return 'success'
  return 'warning'
}
</script>

<template>
  <div class="player-list">
    <PageHeader title="球員">
      <template #meta>
        <FrontendUnitBanner module-code="C2" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="player-list__filters">
      <div class="player-list__filter-row">
        <el-select v-model="filters.teamId" placeholder="球隊" clearable class="player-list__filter-select" @change="applyFilters">
          <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.id" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="player-list__filter-select" @change="applyFilters">
          <el-option v-for="s in PLAYER_STATUS_ORDER" :key="s" :label="PLAYER_STATUS_LABEL[s]" :value="s" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>
        <div class="player-list__filter-placeholder" />
        <el-button type="primary" @click="handleAdd">+ 新增球員</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadPlayers">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="players.length > 0" :data="players" row-key="id">
        <el-table-column label="背號" width="72">
          <template #default="{ row }">{{ row.shirtNo ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="姓名" min-width="140">
          <template #default="{ row }">{{ row.nameZh || '（未命名）' }}</template>
        </el-table-column>
        <el-table-column label="球隊" width="140">
          <template #default="{ row }">{{ teamLabelById.get(row.teamId) ?? row.teamCode }}</template>
        </el-table-column>
        <el-table-column label="位置" width="110">
          <template #default="{ row }">{{ row.position || '—' }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="100">
          <template #default="{ row }">{{ PLAYER_STATUS_LABEL[row.status as PlayerStatus] ?? row.status }}</template>
        </el-table-column>
        <el-table-column label="肖像同意" width="120">
          <template #default="{ row }">
            <el-tag :type="portraitTagType(row.portraitConsentStatus)" size="small">
              {{ PORTRAIT_CONSENT_STATUS_LABEL[row.portraitConsentStatus as PortraitConsentStatus] ?? row.portraitConsentStatus }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="找不到符合條件的球員">
        <el-button type="primary" @click="handleAdd">+ 新增第一位球員</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.player-list__filters {
  margin-bottom: 12px;
}

.player-list__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.player-list__filter-select {
  width: 160px;
  max-width: 100%;
}

.player-list__filter-placeholder {
  flex: 1;
}
</style>
