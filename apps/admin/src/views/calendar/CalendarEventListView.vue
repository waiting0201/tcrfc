<script setup lang="ts">
/**
 * `L2` 自建事件——列表頁（對應主站規劃書 §4.12 L2，行 1378–1381；apps/api/README.md「S1-11」）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import {
  deleteAdminCalendarCustomEvent,
  listAdminCalendarCustomEvents,
  type AdminCalendarCustomEventListItemDto,
} from '@/api/adminCalendar'
import { AdminApiError } from '@/api/http'
import { useCalendarPermissions } from '@/composables/useCalendarPermissions'
import { formatDateTime } from '@/utils/formatDateTime'
import { CALENDAR_CLUB_TEAM_VALUE, repeatRuleLabel } from '@/types/calendar'

const router = useRouter()
const club = computed(() => activeClubId.value)
const { canManageCustomEvents } = useCalendarPermissions()

const filters = reactive({ team: '' })

const teams = ref<AdminTeamAdminListItemDto[]>([])
const teamLabelByCode = computed(() => {
  const map = new Map<string, string>()
  for (const t of teams.value) map.set(t.code, t.nameZh || t.code)
  return map
})

function teamCodesLabel(codes: string[]): string {
  if (codes.length === 0) return '俱樂部活動'
  return codes.map((code) => teamLabelByCode.value.get(code) ?? code).join('、')
}

const events = ref<AdminCalendarCustomEventListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadTeams() {
  try {
    teams.value = await listAdminClubTeams(club.value)
  } catch {
    teams.value = []
  }
}

async function loadEvents() {
  loading.value = true
  loadError.value = null
  try {
    events.value = await listAdminCalendarCustomEvents(club.value, { team: filters.team || undefined })
  } catch (error) {
    events.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '自建事件清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function bootstrap() {
  filters.team = ''
  await Promise.all([loadTeams(), loadEvents()])
}

onMounted(bootstrap)
watch(club, bootstrap)
watch(() => filters.team, loadEvents)

function clearFilters() {
  filters.team = ''
}

function handleAdd() {
  router.push('/calendar/events/new')
}

function handleEdit(row: AdminCalendarCustomEventListItemDto) {
  router.push(`/calendar/events/${row.id}/edit`)
}

async function handleDelete(row: AdminCalendarCustomEventListItemDto) {
  try {
    await ElMessageBox.confirm(
      `確定要刪除「${row.titleZh || '（未命名活動）'}」這筆自建事件嗎？這個動作無法復原。`,
      '確認刪除',
      { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await deleteAdminCalendarCustomEvent(club.value, row.id)
    ElMessage.success('已刪除')
    await loadEvents()
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      await ElMessageBox.alert(error.message, '沒有權限刪除', { confirmButtonText: '我知道了' })
    } else {
      ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
    }
  }
}

const isEmpty = computed(() => !loading.value && !loadError.value && events.value.length === 0)
</script>

<template>
  <div class="calendar-event-list">
    <PageHeader title="自建事件">
      <template #meta>
        <FrontendUnitBanner module-code="L2" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="calendar-event-list__filters">
      <div class="calendar-event-list__filter-row">
        <el-select v-model="filters.team" placeholder="球隊" clearable filterable class="calendar-event-list__filter-select">
          <el-option label="俱樂部活動" :value="CALENDAR_CLUB_TEAM_VALUE" />
          <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.code" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>
        <div class="calendar-event-list__filter-placeholder" />
        <el-button v-if="canManageCustomEvents" type="primary" @click="handleAdd">+ 新增自建事件</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadEvents">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="!isEmpty" :data="events" row-key="id">
        <el-table-column label="標題" min-width="180">
          <template #default="{ row }">{{ row.titleZh || '（未命名活動）' }}</template>
        </el-table-column>
        <el-table-column label="起訖時間" width="170">
          <template #default="{ row }">
            <div>{{ formatDateTime(row.startsAt) }}</div>
            <div v-if="row.endsAt" class="calendar-event-list__muted">～ {{ formatDateTime(row.endsAt) }}</div>
            <div v-if="row.isAllDay" class="calendar-event-list__muted">全天</div>
          </template>
        </el-table-column>
        <el-table-column label="重複規則" width="100">
          <template #default="{ row }">{{ repeatRuleLabel(row.repeatRule) }}</template>
        </el-table-column>
        <el-table-column label="分類" width="110">
          <template #default="{ row }">{{ row.eventTypeCode || '—' }}</template>
        </el-table-column>
        <el-table-column label="球隊" min-width="140">
          <template #default="{ row }">{{ teamCodesLabel(row.teamCodes) }}</template>
        </el-table-column>
        <el-table-column label="公開狀態" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isPublic ? 'success' : 'info'" size="small">{{ row.isPublic ? '公開' : '不公開' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="更新時間" width="150">
          <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="130" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">{{ canManageCustomEvents ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canManageCustomEvents" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="找不到符合條件的自建事件">
        <el-button v-if="canManageCustomEvents" type="primary" @click="handleAdd">+ 新增第一筆自建事件</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.calendar-event-list__filters {
  margin-bottom: 12px;
}

.calendar-event-list__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.calendar-event-list__filter-select {
  width: 160px;
  max-width: 100%;
}

.calendar-event-list__filter-placeholder {
  flex: 1;
}

.calendar-event-list__muted {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}
</style>
