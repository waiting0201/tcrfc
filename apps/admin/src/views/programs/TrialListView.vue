<script setup lang="ts">
/**
 * 試訓場次列表（對應前台試訓資訊；apps/api/README.md「B1」節「P4 試訓場次」）。
 * 名單、簽到表、代填都從這裡的按鈕進入。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { deleteTrial, listTrials, TRIAL_STATUS_ORDER, type TrialListItemDto } from '@/api/adminTrials'
import { sessionStatusTagType } from '@/types/program'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const club = computed(() => activeClubId.value)
const { canCreate, canUpdate, canDelete } = useCrudPermissions('program.trial')
const canViewRegistrations = usePermission('program.trial_registration.view')

const teams = ref<AdminTeamAdminListItemDto[]>([])
const rows = ref<TrialListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const filters = reactive({ teamId: '', status: '', range: null as [string, string] | null })

async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listTrials(club.value, {
      teamId: filters.teamId || undefined,
      status: filters.status || undefined,
      from: filters.range?.[0],
      to: filters.range?.[1],
    })
  } catch (error) {
    rows.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '試訓場次載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function loadTeams() {
  try {
    teams.value = await listAdminClubTeams(club.value)
  } catch {
    teams.value = []
  }
}

function resetFilters() {
  filters.teamId = ''
  filters.status = ''
  filters.range = null
}

onMounted(() => {
  load()
  loadTeams()
})
watch(club, () => {
  resetFilters()
  load()
  loadTeams()
})

function clearFilters() {
  resetFilters()
  load()
}

function teamText(row: TrialListItemDto): string {
  return row.teamName || '俱樂部整體'
}

function quotaText(row: TrialListItemDto): string {
  return `${row.enrolledCount} / ${row.capacity ?? '不限'}`
}

async function handleDelete(row: TrialListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除 ${row.trialOn} 的試訓場次嗎？刪除後無法復原。`, '刪除試訓場次', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteTrial(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    if (error instanceof AdminApiError && error.status === 409) {
      ElMessageBox.alert(`${error.message} 這場已經有人報名，請改成把狀態設為「已結束」。`, '無法刪除', { confirmButtonText: '知道了' })
    } else {
      ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
    }
  }
}

const go = (path: string) => router.push(path)
</script>

<template>
  <div class="trial-list">
    <PageHeader title="試訓場次">
      <template #meta>
        <FrontendUnitBanner module-code="P4" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="trial-list__bar">
      <div class="trial-list__bar-row">
        <el-select v-model="filters.teamId" placeholder="球隊" clearable filterable class="trial-list__select" @change="load">
          <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || '（未命名）'" :value="t.id" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="trial-list__select" @change="load">
          <el-option v-for="s in TRIAL_STATUS_ORDER" :key="s" :label="s" :value="s" />
        </el-select>
        <el-date-picker
          v-model="filters.range"
          type="daterange"
          value-format="YYYY-MM-DD"
          range-separator="至"
          start-placeholder="試訓日起"
          end-placeholder="試訓日迄"
          class="trial-list__range"
          @change="load"
        />
        <el-button @click="clearFilters">清除</el-button>
        <span class="trial-list__spacer" />
        <el-button v-if="canCreate" type="primary" @click="go('/programs/trials/new')">+ 新增試訓場次</el-button>
      </div>
      <p class="trial-list__hint">報名達名額時系統會自動把狀態改為「額滿」；之後有人取消不會自動轉回，請人工決定要不要重新開放。</p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="試訓日" width="120" prop="trialOn" />
          <el-table-column label="球隊" min-width="130">
            <template #default="{ row }">{{ teamText(row) }}</template>
          </el-table-column>
          <el-table-column label="場地" min-width="130">
            <template #default="{ row }">{{ row.venueName || '—' }}</template>
          </el-table-column>
          <el-table-column label="已報名／名額" width="120">
            <template #default="{ row }">{{ quotaText(row) }}</template>
          </el-table-column>
          <el-table-column label="候補" width="70" prop="waitlistCount" />
          <el-table-column label="報名截止" width="120">
            <template #default="{ row }">{{ row.deadlineOn || '—' }}</template>
          </el-table-column>
          <el-table-column label="狀態" width="90">
            <template #default="{ row }">
              <el-tag :type="sessionStatusTagType(row.status)" size="small">{{ row.status }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="前台報名" width="100">
            <template #default="{ row }">
              <el-tag :type="row.isSignupOpen ? 'success' : 'info'" size="small">{{ row.isSignupOpen ? '開放中' : '未開放' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="290" fixed="right">
            <template #default="{ row }">
              <el-button v-if="canViewRegistrations" size="small" text type="primary" @click="go(`/programs/trials/${row.id}/registrations`)">報名名單</el-button>
              <el-button v-if="canViewRegistrations" size="small" text type="primary" @click="go(`/programs/trials/${row.id}/sign-in`)">簽到表</el-button>
              <el-button size="small" text type="primary" @click="go(`/programs/trials/${row.id}/edit`)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.trialOn }} {{ teamText(row) }}</template>
          <template #meta="{ row }">
            <el-tag :type="sessionStatusTagType(row.status)" size="small">{{ row.status }}</el-tag>
            <el-tag :type="row.isSignupOpen ? 'success' : 'info'" size="small">{{ row.isSignupOpen ? '開放報名中' : '未開放報名' }}</el-tag>
            <span>已報名 {{ quotaText(row) }}</span>
            <span>候補 {{ row.waitlistCount }} 人</span>
            <span>截止 {{ row.deadlineOn || '—' }}</span>
            <span>場地 {{ row.venueName || '—' }}</span>
          </template>
          <template #actions="{ row }">
            <el-button v-if="canViewRegistrations" size="small" text type="primary" @click="go(`/programs/trials/${row.id}/registrations`)">報名名單</el-button>
            <el-button v-if="canViewRegistrations" size="small" text type="primary" @click="go(`/programs/trials/${row.id}/sign-in`)">簽到表</el-button>
            <el-button size="small" text type="primary" @click="go(`/programs/trials/${row.id}/edit`)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </MobileCardList>
      </template>
      <el-empty v-else description="目前沒有符合條件的試訓場次">
        <el-button v-if="canCreate" type="primary" @click="go('/programs/trials/new')">+ 新增試訓場次</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.trial-list__bar { margin-bottom: 12px; }
.trial-list__bar-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.trial-list__select { width: 160px; max-width: 100%; }
.trial-list__range { max-width: 100%; }
.trial-list__spacer { flex: 1; }
.trial-list__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>
