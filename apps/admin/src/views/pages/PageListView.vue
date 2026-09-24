<script setup lang="ts">
/**
 * B1 頁面管理列表。對照規劃書 §4.2 B1：「對應所有靜態頁（2.x、3.2~3.4、4.x、5.x、
 * 06 藍鯨官網入口頁、9.3、11.1 等）」——這裡管理的是網站的多個靜態頁面，不是單一前台頁面，
 * 所以 `FrontendUnitBanner` 用的是 `linkType: 'multi'`（見 `@/data/frontendUnits.ts`）。
 * 版面沿用既有 `NewsListView.vue` 的列表頁標準型（篩選列／表格／分頁／空狀態）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { activeClubId } from '@/auth/clubAccess'
import { deleteAdminPage, listAdminPages, type AdminPageListItemDto } from '@/api/adminPages'
import { AdminApiError } from '@/api/http'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

const filters = reactive({
  keyword: '',
  status: '' as 'draft' | 'scheduled' | 'published' | '',
})

const currentPage = ref(1)
const pageSize = ref(20)

const pages = ref<AdminPageListItemDto[]>([])
const totalCount = ref(0)
const initialLoading = ref(true)
const refetching = ref(false)

interface ListErrorState { message: string; detail?: string }
const listError = ref<ListErrorState | null>(null)
const hasAnyDataEver = ref<boolean | null>(null)

async function probeHasAnyData(club: string) {
  try {
    const result = await listAdminPages(club, { page: 1, pageSize: 1 })
    hasAnyDataEver.value = result.totalCount > 0
  } catch {
    // 探測失敗不單獨處理——主要查詢通常會遇到同一個錯誤並顯示錯誤狀態。
  }
}

async function fetchList() {
  const club = activeClubId.value
  refetching.value = true
  listError.value = null
  try {
    const result = await listAdminPages(club, {
      status: filters.status,
      keyword: filters.keyword || undefined,
      page: currentPage.value,
      pageSize: pageSize.value,
    })
    pages.value = result.items
    totalCount.value = result.totalCount
    if (result.totalCount > 0) hasAnyDataEver.value = true
  } catch (error) {
    pages.value = []
    totalCount.value = 0
    listError.value = error instanceof AdminApiError ? { message: error.message, detail: error.detail } : { message: '資料載入失敗，請稍後再試' }
  } finally {
    refetching.value = false
    initialLoading.value = false
  }
}

async function bootstrapForClub() {
  initialLoading.value = true
  hasAnyDataEver.value = null
  listError.value = null
  const club = activeClubId.value
  await Promise.all([fetchList(), probeHasAnyData(club)])
}

onMounted(bootstrapForClub)
watch(activeClubId, bootstrapForClub)
watch([currentPage, pageSize], () => fetchList())

function applyFilters() {
  currentPage.value = 1
  fetchList()
}

function clearFilters() {
  filters.keyword = ''
  filters.status = ''
  applyFilters()
}

const hasAnyData = computed(() => hasAnyDataEver.value === true)
const isFilteredEmpty = computed(() => !refetching.value && !listError.value && totalCount.value === 0 && hasAnyDataEver.value === true)

function handleAdd() {
  router.push('/content/pages/new')
}

function handleEdit(row: AdminPageListItemDto) {
  router.push(`/content/pages/${row.id}/edit`)
}

function handleView(row: AdminPageListItemDto) {
  if (row.status !== 'published') return
  window.open(`/zh/${row.slug.replace(/^\/+|\/+$/g, '')}/`, '_blank', 'noopener')
}

async function handleDelete(row: AdminPageListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除網址名稱「${row.slug}」的頁面嗎？這個動作無法復原。`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdminPage(activeClubId.value, row.id, row.updatedAt)
    ElMessage.success('已刪除')
    await fetchList()
    await probeHasAnyData(activeClubId.value)
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'concurrency-conflict') {
      ElMessage.warning('這個頁面在你確認的同時已被其他人變更或刪除，清單已重新整理')
      await fetchList()
      return
    }
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}
</script>

<template>
  <div class="page-list">
    <PageHeader title="頁面管理">
      <template #meta>
        <FrontendUnitBanner module-code="B1" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="page-list__filters">
      <div class="page-list__filter-row">
        <el-input
          v-model="filters.keyword"
          placeholder="搜尋網址名稱或 SEO 標題"
          clearable
          class="page-list__filter-keyword"
          @keyup.enter="applyFilters"
          @clear="applyFilters"
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="page-list__filter-select">
          <el-option label="草稿" value="draft" />
          <el-option label="排程發布" value="scheduled" />
          <el-option label="已發布" value="published" />
        </el-select>
        <el-button type="primary" @click="applyFilters">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
      </div>
    </el-card>

    <el-card shadow="never" class="page-list__toolbar">
      <div class="page-list__toolbar-row">
        <el-button type="primary" @click="handleAdd">+ 新增頁面</el-button>
      </div>
    </el-card>

    <el-card v-if="initialLoading" shadow="never" class="page-list__table-card">
      <el-skeleton :rows="6" animated />
    </el-card>

    <el-card v-else-if="listError" shadow="never">
      <el-empty :image-size="96">
        <template #image>
          <el-icon :size="48" color="var(--admin-text-tertiary)"><WarningFilled /></el-icon>
        </template>
        <template #description>
          <p class="page-list__error-text">{{ listError.message }}</p>
          <p v-if="listError.detail" class="page-list__error-detail">{{ listError.detail }}</p>
        </template>
        <el-button type="primary" @click="fetchList">重新載入</el-button>
      </el-empty>
    </el-card>

    <el-card v-else-if="!hasAnyData && !isFilteredEmpty" shadow="never">
      <el-empty description="目前還沒有任何頁面">
        <el-button type="primary" @click="handleAdd">+ 新增第一個頁面</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-card v-loading="refetching" shadow="never" class="page-list__table-card">
        <template v-if="pages.length > 0">
          <el-table v-if="!isMobile" :data="pages" row-key="id">
            <el-table-column label="網址名稱" min-width="200">
              <template #default="{ row }">
                <code>{{ row.slug }}</code>
              </template>
            </el-table-column>
            <el-table-column label="SEO 標題" min-width="180">
              <template #default="{ row }">{{ row.seoTitleZh || '（尚未設定）' }}</template>
            </el-table-column>
            <el-table-column label="狀態" width="120">
              <template #default="{ row }">
                <StatusTag :status="row.status" :status-at="row.publishedAt ?? undefined" />
              </template>
            </el-table-column>
            <el-table-column label="更新時間" width="160">
              <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="180" fixed="right">
              <template #default="{ row }">
                <el-button size="small" text :disabled="row.status !== 'published'" @click="handleView(row)">檢視</el-button>
                <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
                <el-button size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
              </template>
            </el-table-column>
          </el-table>

          <div v-else class="page-list__cards">
            <el-card v-for="row in pages" :key="row.id" shadow="never" class="page-list__card">
              <div class="page-list__card-title">
                <code>{{ row.slug }}</code>
              </div>
              <div class="page-list__card-meta">
                <span>{{ row.seoTitleZh || '（尚未設定 SEO 標題）' }}</span>
                <StatusTag :status="row.status" :status-at="row.publishedAt ?? undefined" />
              </div>
              <div class="page-list__card-actions">
                <el-button size="small" text :disabled="row.status !== 'published'" @click="handleView(row)">檢視</el-button>
                <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
                <el-button size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
              </div>
            </el-card>
          </div>
        </template>

        <el-empty v-else description="找不到符合條件的資料">
          <el-button @click="clearFilters">清除篩選條件</el-button>
        </el-empty>

        <div v-if="pages.length > 0" class="page-list__pagination">
          <el-pagination
            v-model:current-page="currentPage"
            v-model:page-size="pageSize"
            :total="totalCount"
            :page-sizes="[10, 20, 50, 100]"
            layout="total, sizes, prev, pager, next"
            :size="isMobile ? 'small' : 'default'"
          />
        </div>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.page-list__filters,
.page-list__toolbar {
  margin-bottom: 12px;
}

.page-list__filter-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.page-list__filter-keyword {
  width: 280px;
  max-width: 100%;
}

.page-list__filter-select {
  width: 140px;
  max-width: 100%;
}

.page-list__toolbar-row {
  display: flex;
  justify-content: flex-end;
}

.page-list__error-text {
  font-size: 14px;
  line-height: 1.7;
}

.page-list__error-detail {
  margin-top: 4px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.page-list__pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

@media (max-width: 767px) {
  .page-list__pagination {
    justify-content: center;
  }
}

.page-list__cards {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.page-list__card {
  --el-card-padding: 12px;
}

.page-list__card-title {
  margin-bottom: 6px;
}

.page-list__card-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  flex-wrap: wrap;
}

.page-list__card-actions {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--el-border-color-lighter);
  display: flex;
  gap: 4px;
}
</style>
