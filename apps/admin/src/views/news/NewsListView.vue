<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { newsStore } from '@/data/newsStore'
import { NEWS_CATEGORY_LABEL, type NewsArticle, type NewsCategory } from '@/types/news'

const router = useRouter()
const { breakpoint } = useBreakpoint()
// 手機寬度改用卡片式列表，不是把 el-table 硬擠進小螢幕橫向捲動（docs/21-admin-ui.md §8）
const isMobile = computed(() => breakpoint.value === 'mobile')

// 與編輯頁共用同一份假資料（data/newsStore.ts），編輯頁存檔後回列表看得到變更
const articles = computed(() => newsStore.articles)

const filters = reactive({
  keyword: '',
  category: '' as NewsCategory | '',
  status: '' as NewsArticle['status'] | '',
})

const loading = ref(false)
const currentPage = ref(1)
const pageSize = ref(20)
const selectedIds = ref<string[]>([])

function simulateLoading() {
  loading.value = true
  window.setTimeout(() => {
    loading.value = false
  }, 400)
}

function applyFilters() {
  currentPage.value = 1
  simulateLoading()
}

function clearFilters() {
  filters.keyword = ''
  filters.category = ''
  filters.status = ''
  applyFilters()
}

const hasAnyData = computed(() => articles.value.length > 0)

const filteredArticles = computed(() => {
  return articles.value.filter((article) => {
    if (filters.keyword) {
      const kw = filters.keyword.trim()
      const inTitle = article.title.zh.includes(kw) || article.title.en.includes(kw)
      const inContent = article.content.zh.includes(kw) || article.content.en.includes(kw)
      if (!inTitle && !inContent) return false
    }
    if (filters.category && article.category !== filters.category) return false
    if (filters.status && article.status !== filters.status) return false
    return true
  })
})

const pagedArticles = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredArticles.value.slice(start, start + pageSize.value)
})

function handleSelectionChange(rows: NewsArticle[]) {
  selectedIds.value = rows.map((r) => r.id)
}

function handleAdd() {
  router.push('/content/news/new')
}

function handleEdit(row: NewsArticle) {
  router.push(`/content/news/${row.id}/edit`)
}

function handleView(row: NewsArticle) {
  if (row.status !== 'published') return
  window.open(`/zh/news/${row.urlName}/`, '_blank', 'noopener')
}

function handleDuplicate(row: NewsArticle) {
  const clone: NewsArticle = structuredClone(row)
  clone.id = `${row.id}-copy-${Date.now()}`
  clone.title = { zh: `${row.title.zh}（複製）`, en: row.title.en ? `${row.title.en} (Copy)` : '' }
  clone.status = 'draft'
  clone.statusAt = undefined
  clone.statusBy = undefined
  newsStore.articles.unshift(clone)
  ElMessage.success('已複製一份草稿')
}

async function handleDisable(row: NewsArticle) {
  try {
    await ElMessageBox.confirm(`確定要下架「${row.title.zh}」嗎？下架後前台將不再顯示。`, '確認下架', {
      confirmButtonText: '下架',
      cancelButtonText: '取消',
      type: 'warning',
    })
    row.status = 'disabled'
    row.statusAt = new Date().toISOString().slice(0, 16).replace('T', ' ')
    row.statusBy = '王小明'
    ElMessage.success('已下架')
  } catch {
    // 取消，不動作
  }
}

async function handleDelete(row: NewsArticle) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.title.zh}」嗎？這個動作無法復原。`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
    newsStore.articles = newsStore.articles.filter((a) => a.id !== row.id)
    ElMessage.success('已刪除')
  } catch {
    // 取消，不動作
  }
}

async function handleBatchPublish() {
  newsStore.articles.forEach((a) => {
    if (selectedIds.value.includes(a.id)) {
      a.status = 'published'
      a.statusAt = new Date().toISOString().slice(0, 16).replace('T', ' ')
    }
  })
  ElMessage.success(`已發布 ${selectedIds.value.length} 筆`)
  selectedIds.value = []
}

async function handleBatchDisable() {
  newsStore.articles.forEach((a) => {
    if (selectedIds.value.includes(a.id)) {
      a.status = 'disabled'
      a.statusAt = new Date().toISOString().slice(0, 16).replace('T', ' ')
      a.statusBy = '王小明'
    }
  })
  ElMessage.success(`已下架 ${selectedIds.value.length} 筆`)
  selectedIds.value = []
}

function handleBatchExport() {
  ElMessage.success(`已匯出 ${selectedIds.value.length} 筆（此操作會記入稽核日誌）`)
}

async function handleBatchDelete() {
  try {
    await ElMessageBox.confirm(`確定要刪除選取的 ${selectedIds.value.length} 筆嗎？這個動作無法復原。`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
    newsStore.articles = newsStore.articles.filter((a) => !selectedIds.value.includes(a.id))
    ElMessage.success('已刪除')
    selectedIds.value = []
  } catch {
    // 取消，不動作
  }
}
</script>

<template>
  <div class="news-list">
    <PageHeader title="新聞與故事">
      <template #meta>
        <FrontendUnitBanner module-code="B2" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="news-list__filters">
      <div class="news-list__filter-row">
        <el-input
          v-model="filters.keyword"
          placeholder="搜尋標題或內文"
          clearable
          class="news-list__filter-keyword"
          @keyup.enter="applyFilters"
          @clear="applyFilters"
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.category" placeholder="分類" clearable class="news-list__filter-select">
          <el-option
            v-for="(label, value) in NEWS_CATEGORY_LABEL"
            :key="value"
            :label="label"
            :value="value"
          />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="news-list__filter-select">
          <el-option label="草稿" value="draft" />
          <el-option label="排程發布" value="scheduled" />
          <el-option label="已發布" value="published" />
          <el-option label="已停用" value="disabled" />
        </el-select>
        <el-button type="primary" @click="applyFilters">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
      </div>
    </el-card>

    <el-card shadow="never" class="news-list__toolbar">
      <div class="news-list__toolbar-row">
        <div v-if="selectedIds.length > 0" class="news-list__batch-actions">
          <span class="news-list__batch-count">已選取 {{ selectedIds.length }} 筆</span>
          <el-button size="small" @click="handleBatchPublish">批次發布</el-button>
          <el-button size="small" @click="handleBatchDisable">批次下架</el-button>
          <el-button size="small" @click="handleBatchExport">匯出</el-button>
          <el-button size="small" type="danger" plain @click="handleBatchDelete">刪除</el-button>
        </div>
        <div v-else class="news-list__batch-actions-placeholder" />
        <el-button type="primary" @click="handleAdd">+ 新增文章</el-button>
      </div>
    </el-card>

    <el-card v-if="!hasAnyData" shadow="never">
      <el-empty description="目前還沒有任何新聞與故事">
        <el-button type="primary" @click="handleAdd">+ 新增第一篇文章</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-card v-loading="loading" shadow="never" class="news-list__table-card">
        <template v-if="pagedArticles.length > 0">
          <!-- 桌面／平板：el-table，平板寬度下用 CSS 隱藏次要欄位（docs/21 §8） -->
          <el-table
            v-if="!isMobile"
            :data="pagedArticles"
            row-key="id"
            @selection-change="handleSelectionChange"
          >
            <el-table-column type="selection" width="44" />
            <el-table-column label="封面" width="96">
              <template #default="{ row }">
                <div class="news-list__thumb">
                  <el-image
                    v-if="row.coverImageUrl"
                    :src="row.coverImageUrl"
                    :preview-src-list="[row.coverImageUrl]"
                    fit="cover"
                    class="news-list__thumb-img"
                  />
                  <div v-else class="news-list__thumb-placeholder">
                    <el-icon><Picture /></el-icon>
                  </div>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="標題" min-width="240">
              <template #default="{ row }">
                <span>{{ row.title.zh || '（未命名）' }}</span>
                <el-tag v-if="row.isSharedContent" type="info" size="small" class="news-list__shared-tag">
                  共用內容
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="分類" width="100" class-name="news-list__col-secondary">
              <template #default="{ row }">{{ NEWS_CATEGORY_LABEL[row.category as NewsCategory] }}</template>
            </el-table-column>
            <el-table-column label="狀態" width="140">
              <template #default="{ row }">
                <StatusTag :status="row.status" :status-at="row.statusAt" :status-by="row.statusBy" />
              </template>
            </el-table-column>
            <el-table-column label="發布時間" width="160" class-name="news-list__col-secondary">
              <template #default="{ row }">{{ row.statusAt ?? '—' }}</template>
            </el-table-column>
            <el-table-column label="操作" width="220" fixed="right">
              <template #default="{ row }">
                <el-button size="small" text :disabled="row.status !== 'published'" @click="handleView(row)">
                  檢視
                </el-button>
                <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
                <el-dropdown trigger="click">
                  <el-button size="small" text>
                    更多<el-icon class="el-icon--right"><ArrowDown /></el-icon>
                  </el-button>
                  <template #dropdown>
                    <el-dropdown-menu>
                      <el-dropdown-item @click="handleDuplicate(row)">複製</el-dropdown-item>
                      <el-dropdown-item v-if="row.status === 'published'" @click="handleDisable(row)">
                        下架
                      </el-dropdown-item>
                      <el-dropdown-item divided @click="handleDelete(row)">
                        <span class="news-list__danger-item">刪除</span>
                      </el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
              </template>
            </el-table-column>
          </el-table>

          <!-- 手機：卡片式列表，每筆一張 el-card，只留關鍵欄位（docs/21 §8） -->
          <div v-else class="news-list__cards">
            <el-card v-for="row in pagedArticles" :key="row.id" shadow="never" class="news-list__card">
              <div class="news-list__card-main">
                <div class="news-list__thumb">
                  <el-image
                    v-if="row.coverImageUrl"
                    :src="row.coverImageUrl"
                    :preview-src-list="[row.coverImageUrl]"
                    fit="cover"
                    class="news-list__thumb-img"
                  />
                  <div v-else class="news-list__thumb-placeholder">
                    <el-icon><Picture /></el-icon>
                  </div>
                </div>
                <div class="news-list__card-body">
                  <div class="news-list__card-title">{{ row.title.zh || '（未命名）' }}</div>
                  <div class="news-list__card-meta">
                    <span>{{ NEWS_CATEGORY_LABEL[row.category as NewsCategory] }}</span>
                    <StatusTag :status="row.status" :status-at="row.statusAt" :status-by="row.statusBy" />
                  </div>
                </div>
              </div>
              <div class="news-list__card-actions">
                <el-button size="small" text :disabled="row.status !== 'published'" @click="handleView(row)">
                  檢視
                </el-button>
                <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
                <el-dropdown trigger="click">
                  <el-button size="small" text>
                    更多<el-icon class="el-icon--right"><ArrowDown /></el-icon>
                  </el-button>
                  <template #dropdown>
                    <el-dropdown-menu>
                      <el-dropdown-item @click="handleDuplicate(row)">複製</el-dropdown-item>
                      <el-dropdown-item v-if="row.status === 'published'" @click="handleDisable(row)">
                        下架
                      </el-dropdown-item>
                      <el-dropdown-item divided @click="handleDelete(row)">
                        <span class="news-list__danger-item">刪除</span>
                      </el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
              </div>
            </el-card>
          </div>
        </template>

        <el-empty v-else description="找不到符合條件的資料">
          <el-button @click="clearFilters">清除篩選條件</el-button>
        </el-empty>

        <div v-if="pagedArticles.length > 0" class="news-list__pagination">
          <el-pagination
            v-model:current-page="currentPage"
            v-model:page-size="pageSize"
            :total="filteredArticles.length"
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
.news-list__filters,
.news-list__toolbar {
  margin-bottom: 12px;
}

.news-list__filter-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.news-list__filter-keyword {
  width: 240px;
  max-width: 100%;
}

.news-list__filter-select {
  width: 140px;
  max-width: 100%;
}

.news-list__toolbar-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
}

.news-list__batch-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.news-list__batch-actions-placeholder {
  flex: 1;
}

.news-list__batch-count {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

/* 表格列縮圖固定 48×48px（docs/21 §2.3：160px 是伺服器端衍生檔規格，不是表格顯示尺寸） */
.news-list__thumb {
  width: 48px;
  height: 48px;
}

.news-list__thumb-img {
  width: 100%;
  height: 100%;
  border-radius: 4px;
  /* 中性看片台（§9.1）：縮圖背後固定鋪中性灰，不隨主題色改變，避免透明區域誤判成純色 */
  background: var(--admin-lightbox-neutral);
}

.news-list__thumb-placeholder {
  width: 100%;
  height: 100%;
  background: var(--admin-bg-surface-2);
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--admin-text-tertiary);
}

.news-list__shared-tag {
  margin-left: 6px;
}

.news-list__danger-item {
  color: var(--el-color-danger);
}

.news-list__pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

@media (max-width: 767px) {
  .news-list__pagination {
    justify-content: center;
  }

  /* 手機卡片式列表的縮圖比桌面表格略大（docs/21 §2.3） */
  .news-list__thumb {
    width: 56px;
    height: 56px;
  }
}

/* 平板寬度：維持 el-table 但隱藏次要欄位（docs/21-admin-ui.md §8），桌面寬度照常顯示 */
@media (max-width: 1023px) {
  :deep(.news-list__col-secondary) {
    display: none;
  }
}

.news-list__cards {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.news-list__card {
  --el-card-padding: 12px;
}

.news-list__card-main {
  display: flex;
  gap: 12px;
}

.news-list__card-body {
  flex: 1;
  min-width: 0;
}

.news-list__card-title {
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 6px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.news-list__card-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.news-list__card-actions {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--el-border-color-lighter);
  display: flex;
  gap: 4px;
}
</style>
