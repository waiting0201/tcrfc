<script setup lang="ts">
/**
 * 球迷會活動（對應前台「台中磐石文化」的球迷會活動與報名）。
 * 名額由系統計算：已報名與已到場佔名額，額滿後新報名自動進候補。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { deleteFanEvent, listFanEvents, type FanEventListItemDto } from '@/api/adminFanEvents'
import { formatDateTime } from '@/utils/dateTime'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('culture.fan_event')
const club = computed(() => activeClubId.value)

const emptyFilter = () => ({ keyword: '', status: '', range: null as [string, string] | null })
const filters = reactive(emptyFilter())
const rows = ref<FanEventListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listFanEvents(club.value, {
      keyword: filters.keyword.trim() || undefined,
      status: filters.status || undefined,
      from: filters.range?.[0],
      to: filters.range?.[1],
    })
  } catch (error) {
    rows.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '活動清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, () => {
  Object.assign(filters, emptyFilter())
  load()
})
function clearFilters() {
  Object.assign(filters, emptyFilter())
  load()
}

async function handleDelete(row: FanEventListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除活動「${row.nameZh || '（未命名）'}」嗎？封面與活動圖集會一併刪除，無法復原。已有報名紀錄的活動不能刪除，請改為草稿。`, '刪除活動', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteFanEvent(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

const capacityText = (row: FanEventListItemDto) =>
  `${row.registeredCount} / ${row.capacity ?? '不限'}${row.waitlistCount > 0 ? `，候補 ${row.waitlistCount}` : ''}`
const go = (row: FanEventListItemDto) => router.push(`/culture/fan-events/${row.id}/edit`)
</script>

<template>
  <div class="fan-events">
    <PageHeader title="球迷會活動">
      <template #meta><FrontendUnitBanner module-code="F2" /></template>
    </PageHeader>

    <el-card shadow="never" class="fan-events__bar">
      <div class="fan-events__row">
        <el-input v-model="filters.keyword" placeholder="搜尋活動名稱" clearable class="fan-events__keyword" @keyup.enter="load" @clear="load">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="fan-events__select" @change="load">
          <el-option label="已發布" value="published" />
          <el-option label="草稿" value="draft" />
        </el-select>
        <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="開始日" end-placeholder="結束日" class="fan-events__range" @change="load" />
        <el-button type="primary" @click="load">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
        <span class="fan-events__spacer" />
        <el-button v-if="canCreate" type="primary" @click="router.push('/culture/fan-events/new')">+ 新增活動</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="目前沒有符合條件的活動">
        <el-button v-if="canCreate" type="primary" @click="router.push('/culture/fan-events/new')">+ 新增第一場活動</el-button>
      </el-empty>
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="封面" width="84">
            <template #default="{ row }">
              <img v-if="row.coverThumbUrl" :src="row.coverThumbUrl" alt="" class="fan-events__thumb">
              <span v-else class="fan-events__muted">—</span>
            </template>
          </el-table-column>
          <el-table-column label="活動名稱" min-width="180">
            <template #default="{ row }">
              <div>{{ row.nameZh || '（未命名）' }}</div>
              <el-tag v-if="row.isPaidMembersOnly" size="small" type="warning">限付費會員</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="活動時間" min-width="150"><template #default="{ row }">{{ formatDateTime(row.startsAt) || '尚未設定' }}</template></el-table-column>
          <el-table-column label="報名（已報名／名額）" min-width="150"><template #default="{ row }">{{ capacityText(row) }}</template></el-table-column>
          <el-table-column label="報名狀況" width="100">
            <template #default="{ row }">
              <el-tag v-if="row.status === 'published'" :type="row.isRegistrationOpen ? 'success' : 'info'" size="small">{{ row.isRegistrationOpen ? '開放報名' : '未開放' }}</el-tag>
              <span v-else class="fan-events__muted">—</span>
            </template>
          </el-table-column>
          <el-table-column label="狀態" width="90">
            <template #default="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag></template>
          </el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="go(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          <template #meta="{ row }">
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag>
            <el-tag v-if="row.isPaidMembersOnly" size="small" type="warning">限付費會員</el-tag>
            <span>{{ formatDateTime(row.startsAt) || '尚未設定時間' }}</span>
            <span>報名 {{ capacityText(row) }}</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="go(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </MobileCardList>
      </template>
    </el-card>
  </div>
</template>

<style scoped>
.fan-events__bar { margin-bottom: 12px; }
.fan-events__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.fan-events__keyword { width: 220px; max-width: 100%; }
.fan-events__select { width: 130px; max-width: 100%; }
.fan-events__range { max-width: 100%; }
.fan-events__spacer { flex: 1; }
.fan-events__thumb { width: 64px; height: 44px; object-fit: cover; border-radius: 3px; background: var(--admin-lightbox-neutral); }
.fan-events__muted { color: var(--admin-text-tertiary); }
</style>
