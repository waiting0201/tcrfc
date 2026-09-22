<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { activeClubId } from '@/data/activeClub'
import { checkGateStatus, type GateStatus } from '@/api/gate'
import {
  createAdminNews,
  deleteAdminNews,
  getAdminNewsById,
  listAdminNews,
  listItemDtoToArticle,
  publishAdminNews,
} from '@/api/adminNews'
import { AdminApiError } from '@/api/http'
import { NEWS_CATEGORY_LABEL, type NewsArticle, type NewsCategory } from '@/types/news'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()
const { breakpoint } = useBreakpoint()
// 手機寬度改用卡片式列表，不是把 el-table 硬擠進小螢幕橫向捲動（docs/21-admin-ui.md §8）
const isMobile = computed(() => breakpoint.value === 'mobile')
/**
 * 平板寬度（768–1023px）隱藏次要欄位（docs/21 §8）：⚠️ 這裡改用 `v-if` 讓欄位整個不渲染，
 * 不是用 CSS `class-name` + `display:none` 隱藏儲存格——那個寫法看起來對，但 `el-table` 的內部
 * 版面寬度是照每個 `<el-table-column>` 宣告的 `width` 加總算出來的，跟儲存格本身有沒有被 CSS
 * 藏起來無關；儲存格藏了，表格容器的寬度卻沒有跟著變窄，會在 `.el-table__header-wrapper`／
 * `.el-table__body-wrapper` 內部長出一條看不出理由的水平捲軸（`document.documentElement.scrollWidth`
 * 量不到，因為捲動被包在表格自己的捲動容器裡，不會外溢到整個文件）。用 CDP 在 768px 寬度實測
 * 量到 `el-table__header-wrapper` 的 `scrollWidth`（990px）遠大於 `clientWidth`（599px）才發現。
 */
const showSecondaryColumns = computed(() => breakpoint.value === 'desktop')

const filters = reactive({
  keyword: '',
  category: '' as NewsCategory | '',
  status: '' as 'draft' | 'scheduled' | 'published' | '',
})

const currentPage = ref(1)
const pageSize = ref(20)
const selectedIds = ref<string[]>([])

const articles = ref<NewsArticle[]>([])
const totalCount = ref(0)

/** 首次進入這個俱樂部的列表頁（無快取資料）：用骨架卡片，不是遮罩轉圈（docs/21 §10.1／§10.2） */
const initialLoading = ref(true)
/** 篩選／翻頁重新查詢，畫面上已有舊資料：沿用 el-table 的 v-loading 半透明遮罩 */
const refetching = ref(false)

interface ListErrorState {
  message: string
  detail?: string
}
const listError = ref<ListErrorState | null>(null)

/** 「這個模組本來就沒有資料」與「篩選查無結果」是兩種不同的空狀態（docs/21 §10.1），
 * 用一次不帶篩選條件的探測請求區分，不能只看目前這次（可能帶篩選）查詢的 totalCount。 */
const hasAnyDataEver = ref<boolean | null>(null)

const gateStatus = ref<GateStatus>('closed')
const gateChecking = ref(true)

async function probeHasAnyData(club: string) {
  try {
    const page = await listAdminNews(club, { page: 1, pageSize: 1 })
    hasAnyDataEver.value = page.totalCount > 0
  } catch {
    // 探測失敗不單獨處理——主要查詢通常會遇到同一個錯誤並顯示錯誤狀態，這裡靜默即可。
  }
}

async function fetchList() {
  const club = activeClubId.value
  refetching.value = true
  listError.value = null
  try {
    const page = await listAdminNews(club, {
      status: filters.status,
      category: filters.category,
      keyword: filters.keyword || undefined,
      page: currentPage.value,
      pageSize: pageSize.value,
    })
    articles.value = page.items.map(listItemDtoToArticle)
    totalCount.value = page.totalCount
    // 這次查詢真的撈到資料，可以直接確定「模組不是完全沒有資料」，不必等 probeHasAnyData 那次
    // 額外的請求回來——沒篩選條件時這跟 probe 的結果本來就會一致，有篩選條件時這裡先讓畫面
    // 不要誤判成「本來就沒有資料」那種空狀態文案。
    if (page.totalCount > 0) hasAnyDataEver.value = true
  } catch (error) {
    articles.value = []
    totalCount.value = 0
    if (error instanceof AdminApiError) {
      listError.value = { message: error.message, detail: error.detail }
    } else {
      listError.value = { message: '資料載入失敗，請稍後再試' }
    }
  } finally {
    refetching.value = false
    initialLoading.value = false
  }
}

async function bootstrapForClub() {
  gateChecking.value = true
  initialLoading.value = true
  hasAnyDataEver.value = null
  listError.value = null
  selectedIds.value = []
  const club = activeClubId.value
  const status = await checkGateStatus(club)
  gateStatus.value = status
  gateChecking.value = false
  if (status !== 'open') {
    initialLoading.value = false
    return
  }
  await Promise.all([fetchList(), probeHasAnyData(club)])
}

onMounted(bootstrapForClub)
watch(activeClubId, bootstrapForClub)

async function retryFromGate() {
  await bootstrapForClub()
}

function applyFilters() {
  currentPage.value = 1
  fetchList()
}

function clearFilters() {
  filters.keyword = ''
  filters.category = ''
  filters.status = ''
  applyFilters()
}

watch([currentPage, pageSize], () => {
  if (gateStatus.value === 'open') fetchList()
})

const hasAnyData = computed(() => hasAnyDataEver.value === true)
const isFilteredEmpty = computed(
  () => !refetching.value && !listError.value && totalCount.value === 0 && hasAnyDataEver.value === true,
)

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

async function handleDuplicate(row: NewsArticle) {
  const club = activeClubId.value
  try {
    const detail = await getAdminNewsById(club, row.id)
    const created = await createAdminNews(club, {
      slug: `${detail.slug}-copy-${Date.now()}`,
      categoryCode: detail.categoryCode,
      coverKey: detail.coverKey,
      isFeatured: false, // 複製品刻意不繼承置頂精選，避免立刻撞到「逐俱樂部限 3」的上限
      content: {
        zh: detail.zh,
        en: detail.en,
      },
    })
    ElMessage.success('已建立一份複製的草稿，請修改網址名稱後再儲存')
    router.push(`/content/news/${created.id}/edit`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '複製失敗，請稍後再試')
  }
}

async function handleDelete(row: NewsArticle) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.title.zh || '（未命名）'}」嗎？這個動作無法復原。`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return // 取消，不動作
  }
  try {
    await deleteAdminNews(activeClubId.value, row.id, row.updatedAt)
    ElMessage.success('已刪除')
    await fetchList()
    await probeHasAnyData(activeClubId.value)
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'concurrency-conflict') {
      ElMessage.warning('這篇文章在你確認的同時已被其他人變更或刪除，清單已重新整理')
      await fetchList()
      return
    }
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

async function handleBatchPublish() {
  const targets = articles.value.filter((a) => selectedIds.value.includes(a.id))
  let succeeded = 0
  let failed = 0
  for (const target of targets) {
    try {
      await publishAdminNews(activeClubId.value, target.id, target.updatedAt)
      succeeded += 1
    } catch {
      failed += 1
    }
  }
  if (failed === 0) {
    ElMessage.success(`已發布 ${succeeded} 篇`)
  } else {
    ElMessage.warning(`已發布 ${succeeded} 篇，${failed} 篇失敗（可能已被他人變更，或是共用內容）`)
  }
  selectedIds.value = []
  await fetchList()
}

async function handleBatchDelete() {
  const targets = articles.value.filter((a) => selectedIds.value.includes(a.id))
  try {
    await ElMessageBox.confirm(`確定要刪除選取的 ${targets.length} 筆嗎？這個動作無法復原。`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  let succeeded = 0
  let failed = 0
  for (const target of targets) {
    try {
      await deleteAdminNews(activeClubId.value, target.id, target.updatedAt)
      succeeded += 1
    } catch {
      failed += 1
    }
  }
  if (failed === 0) {
    ElMessage.success(`已刪除 ${succeeded} 筆`)
  } else {
    ElMessage.warning(`已刪除 ${succeeded} 筆，${failed} 筆失敗（可能已被他人異動，或是共用內容）`)
  }
  selectedIds.value = []
  await fetchList()
  await probeHasAnyData(activeClubId.value)
}
</script>

<template>
  <div class="news-list">
    <PageHeader title="新聞與故事">
      <template #meta>
        <FrontendUnitBanner module-code="B2" />
      </template>
    </PageHeader>

    <!-- 開發環境寫入功能尚未開啟，或完全連不上後台服務：整頁替換成明確說明，不留空白畫面（docs/21 §10、任務指示第 2 點） -->
    <el-card v-if="!gateChecking && gateStatus !== 'open'" shadow="never">
      <el-empty :image-size="96">
        <template #image>
          <el-icon :size="48" color="var(--admin-text-tertiary)"><WarningFilled /></el-icon>
        </template>
        <template #description>
          <p v-if="gateStatus === 'closed'" class="news-list__gate-text">
            目前開發環境尚未開啟後台寫入功能，暫時無法檢視或編輯新聞與故事。<br>
            請洽負責後端開發的同仁確認開發環境設定後再試一次。
          </p>
          <p v-else class="news-list__gate-text">
            無法連線到後台服務，請確認服務是否已啟動、網路是否正常後再試一次。
          </p>
        </template>
        <el-button type="primary" @click="retryFromGate">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-card shadow="never" class="news-list__filters">
        <div class="news-list__filter-row">
          <el-input
            v-model="filters.keyword"
            placeholder="搜尋中文標題"
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
            <el-button size="small" type="danger" plain @click="handleBatchDelete">刪除</el-button>
          </div>
          <div v-else class="news-list__batch-actions-placeholder" />
          <el-button type="primary" @click="handleAdd">+ 新增文章</el-button>
        </div>
      </el-card>

      <!-- 首次載入：骨架卡片，讓使用者提前看到大致版面結構（docs/21 §10.2） -->
      <el-card v-if="initialLoading" shadow="never" class="news-list__table-card">
        <el-skeleton :rows="6" animated />
      </el-card>

      <!-- 查詢失敗：不用 danger 紅色，這是系統性問題不是使用者操作後果（docs/21 §10.3） -->
      <el-card v-else-if="listError" shadow="never">
        <el-empty :image-size="96">
          <template #image>
            <el-icon :size="48" color="var(--admin-text-tertiary)"><WarningFilled /></el-icon>
          </template>
          <template #description>
            <p class="news-list__gate-text">{{ listError.message }}</p>
            <p v-if="listError.detail" class="news-list__error-detail">{{ listError.detail }}</p>
          </template>
          <el-button type="primary" @click="fetchList">重新載入</el-button>
        </el-empty>
      </el-card>

      <el-card v-else-if="!hasAnyData && !isFilteredEmpty" shadow="never">
        <el-empty description="目前還沒有任何新聞與故事">
          <el-button type="primary" @click="handleAdd">+ 新增第一篇文章</el-button>
        </el-empty>
      </el-card>

      <template v-else>
        <el-card v-loading="refetching" shadow="never" class="news-list__table-card">
          <template v-if="articles.length > 0">
            <!-- 桌面／平板：el-table，平板寬度下用 CSS 隱藏次要欄位（docs/21 §8） -->
            <el-table
              v-if="!isMobile"
              :data="articles"
              row-key="id"
              @selection-change="handleSelectionChange"
            >
              <el-table-column type="selection" width="44" />
              <el-table-column label="封面" width="72">
                <template #default>
                  <div class="news-list__thumb">
                    <div class="news-list__thumb-placeholder">
                      <el-icon><Picture /></el-icon>
                    </div>
                  </div>
                </template>
              </el-table-column>
              <el-table-column label="標題" min-width="180">
                <template #default="{ row }">
                  <span>{{ row.title.zh || '（未命名）' }}</span>
                  <el-tag v-if="row.isSharedContent" type="info" size="small" class="news-list__inline-tag">
                    共用內容
                  </el-tag>
                  <el-tag v-if="row.isFeatured" type="warning" size="small" class="news-list__inline-tag">
                    置頂精選
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column v-if="showSecondaryColumns" label="分類" width="110">
                <template #default="{ row }">{{ NEWS_CATEGORY_LABEL[row.category as NewsCategory] }}</template>
              </el-table-column>
              <el-table-column label="狀態" width="120">
                <template #default="{ row }">
                  <StatusTag :status="row.status" :status-at="row.statusAt" />
                </template>
              </el-table-column>
              <el-table-column v-if="showSecondaryColumns" label="發布時間" width="160">
                <template #default="{ row }">{{ row.statusAt ? formatDateTime(row.statusAt) : '—' }}</template>
              </el-table-column>
              <el-table-column label="操作" width="180" fixed="right">
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
                        <el-dropdown-item v-if="!row.isSharedContent" divided @click="handleDelete(row)">
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
              <el-card v-for="row in articles" :key="row.id" shadow="never" class="news-list__card">
                <div class="news-list__card-main">
                  <div class="news-list__thumb">
                    <div class="news-list__thumb-placeholder">
                      <el-icon><Picture /></el-icon>
                    </div>
                  </div>
                  <div class="news-list__card-body">
                    <div class="news-list__card-title">{{ row.title.zh || '（未命名）' }}</div>
                    <div class="news-list__card-meta">
                      <span>{{ NEWS_CATEGORY_LABEL[row.category as NewsCategory] }}</span>
                      <StatusTag :status="row.status" :status-at="row.statusAt" />
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
                        <el-dropdown-item v-if="!row.isSharedContent" divided @click="handleDelete(row)">
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

          <div v-if="articles.length > 0" class="news-list__pagination">
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

.news-list__gate-text {
  font-size: 14px;
  line-height: 1.7;
}

.news-list__error-detail {
  margin-top: 4px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

/* 表格列縮圖固定 48×48px（docs/21 §2.3：160px 是伺服器端衍生檔規格，不是表格顯示尺寸） */
.news-list__thumb {
  width: 48px;
  height: 48px;
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

.news-list__inline-tag {
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
