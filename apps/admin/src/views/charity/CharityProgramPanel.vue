<script setup lang="ts">
/** 慈善計畫清單（分頁）。「進行中／已結束」由計畫期間自動判斷，跟「已發布／草稿」是兩回事。 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { deleteProgram, listPrograms, type ProgramListItemDto } from '@/api/adminCharity'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('charity.content')
const club = computed(() => activeClubId.value)

const rows = ref<ProgramListItemDto[]>([])
const filters = reactive({ keyword: '', status: '' })
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const r = await listPrograms(club.value, { keyword: filters.keyword.trim() || undefined, status: filters.status || undefined, page: page.value, pageSize: pageSize.value })
    rows.value = r.items
    total.value = r.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
function apply() {
  page.value = 1
  load()
}
onMounted(load)
watch(club, () => {
  Object.assign(filters, { keyword: '', status: '' })
  apply()
})

async function handleDelete(row: ProgramListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除慈善計畫「${row.nameZh || '（未命名）'}」嗎？封面與圖集會一併刪除。`, '刪除慈善計畫', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteProgram(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

const edit = (row: ProgramListItemDto) => router.push(`/content/charity/programs/${row.id}/edit`)
const editable = (row: ProgramListItemDto) => canUpdate.value && !row.isShared
const period = (row: ProgramListItemDto) => (row.startOn || row.endOn ? `${row.startOn ?? '—'} ～ ${row.endOn ?? '持續進行'}` : '未填期間')
</script>

<template>
  <div>
    <el-card shadow="never" class="panel__block">
      <div class="panel__row">
        <el-input v-model="filters.keyword" placeholder="搜尋計畫名稱" clearable class="panel__keyword" @keyup.enter="apply" @clear="apply">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="發布狀態" clearable class="panel__select" @change="apply">
          <el-option label="已發布" value="published" /><el-option label="草稿" value="draft" />
        </el-select>
        <el-button type="primary" @click="apply">篩選</el-button>
        <span class="panel__spacer" />
        <el-button v-if="canCreate" type="primary" @click="router.push('/content/charity/programs/new')">+ 新增慈善計畫</el-button>
      </div>
      <p class="panel__hint">前台慈善計畫列表只顯示已發布的計畫，置頂的排在最前面。</p>
    </el-card>
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="封面" width="84">
            <template #default="{ row }"><img v-if="row.coverThumbUrl" :src="row.coverThumbUrl" alt="" class="panel__cover"><span v-else class="panel__muted">—</span></template>
          </el-table-column>
          <el-table-column label="計畫名稱" min-width="200">
            <template #default="{ row }">
              {{ row.nameZh || '（未命名）' }}
              <el-tag v-if="row.isPinned" size="small" class="panel__tag">置頂</el-tag>
              <el-tag v-if="row.isShared" type="info" size="small" class="panel__tag">兩隊共用</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="受贈團體" min-width="140"><template #default="{ row }">{{ row.charityNameZh || '—' }}</template></el-table-column>
          <el-table-column label="期間" min-width="180"><template #default="{ row }">{{ period(row) }}</template></el-table-column>
          <el-table-column label="進行狀況" width="100"><template #default="{ row }"><el-tag :type="row.progress === 'ongoing' ? 'success' : 'info'" size="small">{{ row.progress === 'ongoing' ? '進行中' : '已完成' }}</el-tag></template></el-table-column>
          <el-table-column label="發布" width="80"><template #default="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.status === 'published' ? '已發布' : '草稿' }}</el-tag></template></el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="edit(row)">{{ editable(row) ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          <template #meta="{ row }">
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.status === 'published' ? '已發布' : '草稿' }}</el-tag>
            <el-tag :type="row.progress === 'ongoing' ? 'success' : 'info'" size="small">{{ row.progress === 'ongoing' ? '進行中' : '已完成' }}</el-tag>
            <el-tag v-if="row.isShared" type="info" size="small">兩隊共用</el-tag>
            <span>{{ row.charityNameZh }}</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="edit(row)">{{ editable(row) ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </MobileCardList>
        <div class="panel__pagination">
          <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" @current-change="load" @size-change="apply" />
        </div>
      </template>
      <el-empty v-else description="還沒有慈善計畫" />
    </el-card>
  </div>
</template>

<style scoped>
.panel__block { margin-bottom: 12px; }
.panel__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.panel__keyword { width: 220px; max-width: 100%; }
.panel__select { width: 150px; max-width: 100%; }
.panel__spacer { flex: 1; }
.panel__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.panel__cover { width: 56px; height: 40px; object-fit: cover; border-radius: 3px; background: var(--admin-lightbox-neutral); }
.panel__muted { color: var(--admin-text-tertiary); }
.panel__tag { margin-left: 6px; }
.panel__pagination { display: flex; justify-content: flex-end; margin-top: 16px; }
@media (max-width: 767px) { .panel__pagination { justify-content: center; } }
</style>
