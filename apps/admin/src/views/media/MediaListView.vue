<script setup lang="ts">
/** 媒體專區（對應前台「媒體專區」：新聞稿、品牌識別包、高解析圖）。可批次顯示／隱藏、批次改類別。 */
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
import { formatFileSize } from '@/utils/downloadFile'
import type { BatchResultDto } from '@/api/adminCommon'
import {
  batchChangePressType,
  batchHidePress,
  batchShowPress,
  deletePress,
  listPress,
  type PressListItemDto,
  type PressResourceType,
} from '@/api/adminPress'
import { PRESS_TYPE_LABEL, PRESS_TYPE_ORDER } from './pressTypes'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('content.press')
const club = computed(() => activeClubId.value)

const rows = ref<PressListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const filters = reactive({ keyword: '', resourceType: '', status: '' })
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const selected = ref<PressListItemDto[]>([])
// 共用列不能被批次修改，批次只算自己俱樂部的
const selectedIds = computed(() => selected.value.filter((r) => !r.isShared).map((r) => r.id))

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listPress(club.value, {
      keyword: filters.keyword.trim() || undefined,
      resourceType: filters.resourceType || undefined,
      status: filters.status || undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    rows.value = result.items
    total.value = result.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

function applyFilters() {
  page.value = 1
  load()
}
function clearFilters() {
  Object.assign(filters, { keyword: '', resourceType: '', status: '' })
  applyFilters()
}

onMounted(load)
watch(club, clearFilters)

function reportBatch(result: BatchResultDto, action: string) {
  if (result.skipped.length > 0) {
    ElMessage.warning(`${action}了 ${result.updatedCount} 筆，${result.skipped.length} 筆沒有處理：${result.skipped.map((s) => s.reason).join('；')}`)
  } else {
    ElMessage.success(`已${action} ${result.updatedCount} 筆`)
  }
}

async function runBatch(fn: () => Promise<BatchResultDto>, action: string) {
  try {
    reportBatch(await fn(), action)
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : `${action}失敗，請稍後再試`)
  }
}

const typeDialog = ref(false)
const batchType = ref<PressResourceType | ''>('')
async function confirmType() {
  if (!batchType.value) return
  typeDialog.value = false
  await runBatch(() => batchChangePressType(club.value, selectedIds.value, batchType.value as PressResourceType), '改類別')
  batchType.value = ''
}

async function handleDelete(row: PressListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.titleZh || '（未命名）'}」嗎？檔案與封面會一併刪除，無法復原。`, '刪除媒體資源', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deletePress(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

const goEdit = (row: PressListItemDto) => router.push(`/content/media/${row.id}/edit`)
</script>

<template>
  <div class="media-list">
    <PageHeader title="媒體專區">
      <template #meta><FrontendUnitBanner module-code="B6" /></template>
    </PageHeader>

    <el-card shadow="never" class="media-list__block">
      <div class="media-list__row">
        <el-input v-model="filters.keyword" placeholder="搜尋標題" clearable class="media-list__keyword" @keyup.enter="applyFilters" @clear="applyFilters">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.resourceType" placeholder="類別" clearable class="media-list__select" @change="applyFilters">
          <el-option v-for="t in PRESS_TYPE_ORDER" :key="t" :label="PRESS_TYPE_LABEL[t]" :value="t" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="media-list__select" @change="applyFilters">
          <el-option label="顯示" value="published" />
          <el-option label="隱藏" value="draft" />
        </el-select>
        <el-button type="primary" @click="applyFilters">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
        <span class="media-list__spacer" />
        <el-button v-if="canCreate" type="primary" @click="router.push('/content/media/new')">+ 新增資源</el-button>
      </div>
      <div v-if="canUpdate && selectedIds.length > 0" class="media-list__row media-list__batch">
        <span class="media-list__count">已選取 {{ selectedIds.length }} 筆</span>
        <el-button size="small" @click="runBatch(() => batchShowPress(club, selectedIds), '設為顯示')">批次顯示</el-button>
        <el-button size="small" @click="runBatch(() => batchHidePress(club, selectedIds), '設為隱藏')">批次隱藏</el-button>
        <el-button size="small" @click="typeDialog = true">批次改類別</el-button>
      </div>
      <p class="media-list__hint">前台媒體專區只顯示「顯示」狀態的資源。標示「兩隊共用」的資源是台中磐石與台中藍鯨共用的，只能檢視。</p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id" @selection-change="(v: PressListItemDto[]) => (selected = v)">
          <el-table-column v-if="canUpdate" type="selection" width="44" :selectable="(row: PressListItemDto) => !row.isShared" />
          <el-table-column label="封面" width="84">
            <template #default="{ row }">
              <img v-if="row.coverThumbUrl" :src="row.coverThumbUrl" alt="" class="media-list__cover">
              <span v-else class="media-list__muted">—</span>
            </template>
          </el-table-column>
          <el-table-column label="標題" min-width="200">
            <template #default="{ row }">
              {{ row.titleZh || '（未命名）' }}
              <el-tag v-if="row.isShared" type="info" size="small" class="media-list__tag">兩隊共用</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="類別" width="110"><template #default="{ row }">{{ PRESS_TYPE_LABEL[row.resourceType as PressResourceType] }}</template></el-table-column>
          <el-table-column label="狀態" width="80"><template #default="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.status === 'published' ? '顯示' : '隱藏' }}</el-tag></template></el-table-column>
          <el-table-column label="發布日期" width="110"><template #default="{ row }">{{ row.publishedOn || '—' }}</template></el-table-column>
          <el-table-column label="檔案大小" width="100"><template #default="{ row }">{{ formatFileSize(row.fileBytes) }}</template></el-table-column>
          <el-table-column label="下載次數" width="90"><template #default="{ row }">{{ row.downloadCount }}</template></el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="goEdit(row)">{{ canUpdate && !row.isShared ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.titleZh || '（未命名）' }}</template>
          <template #meta="{ row }">
            <span>{{ PRESS_TYPE_LABEL[row.resourceType as PressResourceType] }}</span>
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.status === 'published' ? '顯示' : '隱藏' }}</el-tag>
            <el-tag v-if="row.isShared" type="info" size="small">兩隊共用</el-tag>
            <span>下載 {{ row.downloadCount }} 次</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="goEdit(row)">{{ canUpdate && !row.isShared ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </MobileCardList>
        <div class="media-list__pagination">
          <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" @current-change="load" @size-change="applyFilters" />
        </div>
      </template>
      <el-empty v-else description="目前沒有符合條件的資源">
        <el-button v-if="canCreate" type="primary" @click="router.push('/content/media/new')">+ 新增第一筆</el-button>
      </el-empty>
    </el-card>

    <el-dialog v-model="typeDialog" title="批次改類別" width="420px">
      <p class="media-list__hint">新聞稿與品牌識別包之間可以互改；「高解析圖」跟其他類別檔案格式不相容，這類項目會被略過。</p>
      <el-select v-model="batchType" placeholder="請選擇新的類別" style="width: 100%">
        <el-option v-for="t in PRESS_TYPE_ORDER" :key="t" :label="PRESS_TYPE_LABEL[t]" :value="t" />
      </el-select>
      <template #footer>
        <el-button @click="typeDialog = false">取消</el-button>
        <el-button type="primary" :disabled="!batchType" @click="confirmType">確定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.media-list__block { margin-bottom: 12px; }
.media-list__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.media-list__batch { margin-top: 10px; }
.media-list__keyword { width: 220px; max-width: 100%; }
.media-list__select { width: 150px; max-width: 100%; }
.media-list__spacer { flex: 1; }
.media-list__count { font-size: 13px; color: var(--el-text-color-secondary); }
.media-list__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.media-list__cover { width: 56px; height: 40px; object-fit: cover; border-radius: 3px; background: var(--admin-lightbox-neutral); }
.media-list__muted { color: var(--admin-text-tertiary); }
.media-list__tag { margin-left: 6px; }
.media-list__pagination { display: flex; justify-content: flex-end; margin-top: 16px; }
@media (max-width: 767px) { .media-list__pagination { justify-content: center; } }
</style>
