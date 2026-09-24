<script setup lang="ts">
/**
 * B4 常見問題（對應主站規劃書 §4.2 B4，行 1021–1030；apps/api/README.md「S1-6」「S1-6 續作」
 * 「S1-7a」）。這裡管理的是前台常見問題頁與各頁的 FAQ 快捷區塊。單頁涵蓋「主題分類管理」與
 * 「題目清單」兩段——分類筆數固定在十筆上下，不需要獨立路由，比照 B3 首頁編排的單頁式做法。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { activeClubId } from '@/auth/clubAccess'
import {
  batchChangeFaqCategory,
  batchHideFaqs,
  batchShowFaqs,
  createAdminFaqCategory,
  deleteAdminFaq,
  deleteAdminFaqCategory,
  downloadAdminFaqsCsv,
  importAdminFaqsCsv,
  listAdminFaqCategories,
  listAdminFaqSearchMisses,
  listAdminFaqs,
  updateAdminFaqCategory,
  type AdminFaqCategoryListItemDto,
  type AdminFaqListItemDto,
  type AdminFaqSearchMissDto,
  type BatchOperationResultDto,
  type FaqCsvImportResultDto,
} from '@/api/adminFaq'
import { formatDateTime } from '@/utils/formatDateTime'
import { AdminApiError } from '@/api/http'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

const club = computed(() => activeClubId.value)

// ── 主題分類管理 ───────────────────────────────────────────────────────────────────

const categories = ref<AdminFaqCategoryListItemDto[]>([])
const categoriesLoading = ref(true)
const categoriesError = ref<string | null>(null)

async function loadCategories() {
  categoriesLoading.value = true
  categoriesError.value = null
  try {
    categories.value = (await listAdminFaqCategories()).sort((a, b) => a.sortOrder - b.sortOrder)
  } catch (error) {
    categories.value = []
    categoriesError.value = error instanceof AdminApiError ? error.message : '分類清單載入失敗，請稍後再試'
  } finally {
    categoriesLoading.value = false
  }
}

interface CategoryFormState {
  id: string | null
  slug: string
  sortOrder: number
  isEnabled: boolean
  nameZh: string
  nameEn: string
}

function emptyCategoryForm(): CategoryFormState {
  return { id: null, slug: '', sortOrder: categories.value.length, isEnabled: true, nameZh: '', nameEn: '' }
}

const categoryDialogVisible = ref(false)
const categoryDialogMode = ref<'create' | 'edit'>('create')
const categoryForm = reactive<CategoryFormState>(emptyCategoryForm())
const categoryFormError = ref<string | null>(null)
const categorySaving = ref(false)

function openCreateCategoryDialog() {
  categoryDialogMode.value = 'create'
  Object.assign(categoryForm, emptyCategoryForm())
  categoryFormError.value = null
  categoryDialogVisible.value = true
}

function openEditCategoryDialog(row: AdminFaqCategoryListItemDto) {
  categoryDialogMode.value = 'edit'
  Object.assign(categoryForm, {
    id: row.id,
    slug: row.slug,
    sortOrder: row.sortOrder,
    isEnabled: row.isEnabled,
    nameZh: row.nameZh ?? '',
    nameEn: row.nameEn ?? '',
  })
  categoryFormError.value = null
  categoryDialogVisible.value = true
}

async function saveCategory() {
  if (!categoryForm.slug.trim()) {
    categoryFormError.value = '請輸入網址名稱'
    return
  }
  if (!categoryForm.nameZh.trim()) {
    categoryFormError.value = '請輸入中文名稱'
    return
  }
  categorySaving.value = true
  categoryFormError.value = null
  try {
    const content = {
      zh: { name: categoryForm.nameZh },
      en: categoryForm.nameEn.trim() ? { name: categoryForm.nameEn } : undefined,
    }
    if (categoryDialogMode.value === 'create') {
      await createAdminFaqCategory({
        slug: categoryForm.slug.trim(),
        sortOrder: categoryForm.sortOrder,
        isEnabled: categoryForm.isEnabled,
        content,
      })
      ElMessage.success('已新增分類')
    } else {
      await updateAdminFaqCategory(categoryForm.id!, {
        slug: categoryForm.slug.trim(),
        sortOrder: categoryForm.sortOrder,
        isEnabled: categoryForm.isEnabled,
        content,
      })
      ElMessage.success('已儲存')
    }
    categoryDialogVisible.value = false
    await loadCategories()
  } catch (error) {
    categoryFormError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    categorySaving.value = false
  }
}

/** 啟用／停用切換：直接在表格上操作，不用另外開對話框——這是軟停用（S1-7a），
 * 隨時可以改回來，不像刪除是不可逆的動作，不需要二次確認。 */
async function toggleCategoryEnabled(row: AdminFaqCategoryListItemDto, next: boolean) {
  const previous = row.isEnabled
  row.isEnabled = next
  try {
    await updateAdminFaqCategory(row.id, {
      slug: row.slug,
      sortOrder: row.sortOrder,
      isEnabled: next,
      content: { zh: { name: row.nameZh }, en: row.nameEn ? { name: row.nameEn } : undefined },
    })
    ElMessage.success(next ? '已啟用' : '已停用（前台主題導覽會立即消失，既有題目與關聯不受影響）')
  } catch (error) {
    row.isEnabled = previous
    ElMessage.error(error instanceof AdminApiError ? error.message : '更新失敗，請稍後再試')
  }
}

/** 🔴 真刪除，不可逆（S1-7a 起「停用」已經有獨立開關，刪除不再是「停用」的替代品）。 */
async function handleDeleteCategory(row: AdminFaqCategoryListItemDto) {
  try {
    await ElMessageBox.confirm(
      `確定要刪除分類「${row.nameZh || row.slug}」嗎？這個動作無法復原。`
      + (row.faqCount > 0 ? `目前有 ${row.faqCount} 題掛在這個分類底下，刪除後這些題目會失去這個分類（題目本身不會被刪除）。` : ''),
      '確認刪除',
      { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await deleteAdminFaqCategory(row.id)
    ElMessage.success('已刪除')
    await loadCategories()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

// ── 題目清單 ──────────────────────────────────────────────────────────────────────

const filters = reactive({
  keyword: '',
  categoryId: '',
  status: '' as 'draft' | 'published' | '',
  lowRatingOnly: false,
})

const currentPage = ref(1)
const pageSize = ref(20)
const selectedIds = ref<string[]>([])

const faqs = ref<AdminFaqListItemDto[]>([])
const totalCount = ref(0)
const faqsLoading = ref(true)
const faqsError = ref<string | null>(null)

async function loadFaqs() {
  faqsLoading.value = true
  faqsError.value = null
  try {
    const page = await listAdminFaqs(club.value, {
      status: filters.status,
      categoryId: filters.categoryId || undefined,
      keyword: filters.keyword || undefined,
      sort: filters.lowRatingOnly ? 'low_rating' : '',
      page: currentPage.value,
      pageSize: pageSize.value,
    })
    faqs.value = page.items
    totalCount.value = page.totalCount
  } catch (error) {
    faqs.value = []
    totalCount.value = 0
    faqsError.value = error instanceof AdminApiError ? error.message : '題目清單載入失敗，請稍後再試'
  } finally {
    faqsLoading.value = false
  }
}

// ── 搜尋無結果關鍵字排行 ─────────────────────────────────────────────────────────────
// 🔴 `count` 是這個關鍵字有史以來累計被搜尋不到的總次數，`searchMissDays` 只篩「最後一次被
// 搜尋到是否落在這個天數內」，不是「近 N 天的次數」——文案與欄位命名要避免混淆（見
// apps/api/README.md「S1-8」對 `AdminFaqSearchMissDto.Count` 的說明）。

const searchMissDays = ref(30)
const searchMisses = ref<AdminFaqSearchMissDto[]>([])
const searchMissesLoading = ref(true)
const searchMissesError = ref<string | null>(null)

async function loadSearchMisses() {
  searchMissesLoading.value = true
  searchMissesError.value = null
  try {
    searchMisses.value = await listAdminFaqSearchMisses(club.value, searchMissDays.value)
  } catch (error) {
    searchMisses.value = []
    searchMissesError.value = error instanceof AdminApiError ? error.message : '搜尋無結果關鍵字排行載入失敗，請稍後再試'
  } finally {
    searchMissesLoading.value = false
  }
}

async function bootstrapForClub() {
  currentPage.value = 1
  selectedIds.value = []
  await Promise.all([loadCategories(), loadFaqs(), loadSearchMisses()])
}

onMounted(bootstrapForClub)
watch(club, bootstrapForClub)

function applyFilters() {
  currentPage.value = 1
  loadFaqs()
}

function clearFilters() {
  filters.keyword = ''
  filters.categoryId = ''
  filters.status = ''
  filters.lowRatingOnly = false
  applyFilters()
}

watch([currentPage, pageSize], () => loadFaqs())

function handleSelectionChange(rows: AdminFaqListItemDto[]) {
  selectedIds.value = rows.map((r) => r.id)
}

function handleAdd() {
  router.push('/content/faq/new')
}

function handleEdit(row: AdminFaqListItemDto) {
  router.push(`/content/faq/${row.id}/edit`)
}

async function handleDelete(row: AdminFaqListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.questionZh || '（未命名）'}」嗎？這個動作無法復原。`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdminFaq(club.value, row.id)
    ElMessage.success('已刪除')
    await loadFaqs()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

function reportBatchResult(result: BatchOperationResultDto, verbDone: string) {
  if (result.skipped.length === 0) {
    ElMessage.success(`${verbDone} ${result.updatedCount} 題`)
    return
  }
  const reasons = [...new Set(result.skipped.map((s) => s.reason))].join('；')
  ElMessage.warning(`${verbDone} ${result.updatedCount} 題，${result.skipped.length} 題未處理（${reasons}）`)
}

const batchCategoryDialogVisible = ref(false)
const batchCategoryIds = ref<string[]>([])

function openBatchCategoryDialog() {
  batchCategoryIds.value = []
  batchCategoryDialogVisible.value = true
}

async function confirmBatchCategory() {
  if (batchCategoryIds.value.length === 0) {
    ElMessage.warning('請至少選擇一個分類')
    return
  }
  try {
    const result = await batchChangeFaqCategory(club.value, selectedIds.value, batchCategoryIds.value)
    reportBatchResult(result, '已改分類')
    batchCategoryDialogVisible.value = false
    selectedIds.value = []
    await loadFaqs()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '批次改分類失敗，請稍後再試')
  }
}

async function handleBatchShow() {
  try {
    const result = await batchShowFaqs(club.value, selectedIds.value)
    reportBatchResult(result, '已顯示')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '批次顯示失敗，請稍後再試')
  }
  selectedIds.value = []
  await loadFaqs()
}

async function handleBatchHide() {
  try {
    const result = await batchHideFaqs(club.value, selectedIds.value)
    reportBatchResult(result, '已隱藏')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '批次隱藏失敗，請稍後再試')
  }
  selectedIds.value = []
  await loadFaqs()
}

// ── CSV 匯入／匯出 ───────────────────────────────────────────────────────────────

const csvExporting = ref(false)
async function handleExportCsv() {
  csvExporting.value = true
  try {
    await downloadAdminFaqsCsv(club.value)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    csvExporting.value = false
  }
}

const csvFileInput = ref<HTMLInputElement | null>(null)
const csvImporting = ref(false)
const csvImportResult = ref<FaqCsvImportResultDto | null>(null)
const csvImportDialogVisible = ref(false)

function openCsvFileDialog() {
  csvFileInput.value?.click()
}

async function handleCsvFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  csvImporting.value = true
  try {
    const result = await importAdminFaqsCsv(club.value, file)
    csvImportResult.value = result
    csvImportDialogVisible.value = true
    if (result.errors.length === 0) {
      ElMessage.success(`已匯入 ${result.importedCount} 題`)
      await loadFaqs()
    }
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯入失敗，請稍後再試')
  } finally {
    csvImporting.value = false
  }
}
</script>

<template>
  <div class="faq-list">
    <PageHeader title="常見問題">
      <template #meta>
        <FrontendUnitBanner module-code="B4" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="faq-list__section">
      <template #header>
        <div class="faq-list__card-header">
          <span>主題分類管理</span>
          <el-button type="primary" size="small" @click="openCreateCategoryDialog">+ 新增分類</el-button>
        </div>
      </template>
      <el-skeleton v-if="categoriesLoading" :rows="3" animated />
      <el-empty v-else-if="categoriesError" :description="categoriesError">
        <el-button type="primary" @click="loadCategories">重新載入</el-button>
      </el-empty>
      <el-table v-else :data="categories" row-key="id">
        <el-table-column label="排序" width="72">
          <template #default="{ row }">{{ row.sortOrder }}</template>
        </el-table-column>
        <el-table-column label="名稱" min-width="160">
          <template #default="{ row }">{{ row.nameZh || row.slug }}</template>
        </el-table-column>
        <el-table-column label="題目數" width="90">
          <template #default="{ row }">{{ row.faqCount }}</template>
        </el-table-column>
        <el-table-column label="啟用" width="90">
          <template #default="{ row }">
            <el-switch :model-value="row.isEnabled" @change="(v: boolean) => toggleCategoryEnabled(row, v)" />
          </template>
        </el-table-column>
        <el-table-column label="操作" width="140" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="openEditCategoryDialog(row)">編輯</el-button>
            <el-button size="small" text type="danger" @click="handleDeleteCategory(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-card shadow="never" class="faq-list__filters">
      <div class="faq-list__filter-row">
        <el-input
          v-model="filters.keyword"
          placeholder="搜尋中文問題"
          clearable
          class="faq-list__filter-keyword"
          @keyup.enter="applyFilters"
          @clear="applyFilters"
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.categoryId" placeholder="分類" clearable class="faq-list__filter-select">
          <el-option v-for="c in categories" :key="c.id" :label="c.nameZh || c.slug" :value="c.id" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="faq-list__filter-select">
          <el-option label="顯示" value="published" />
          <el-option label="隱藏" value="draft" />
        </el-select>
        <el-checkbox v-model="filters.lowRatingOnly">只看低評價題目</el-checkbox>
        <el-button type="primary" @click="applyFilters">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
      </div>
    </el-card>

    <el-card shadow="never" class="faq-list__section">
      <template #header>
        <div class="faq-list__card-header">
          <span>搜尋無結果關鍵字排行</span>
          <el-select v-model="searchMissDays" size="small" style="width: 200px" @change="loadSearchMisses">
            <el-option label="最近 7 天內有人搜尋過" :value="7" />
            <el-option label="最近 30 天內有人搜尋過" :value="30" />
            <el-option label="最近 90 天內有人搜尋過" :value="90" />
          </el-select>
        </div>
      </template>
      <p class="faq-list__hint">
        列出訪客在前台搜尋常見問題、但找不到結果的關鍵字，累計搜尋次數是這個關鍵字有史以來被搜尋不到的總次數，不是這個天數範圍內的次數；篩選只決定「最後一次被搜尋到是否還在這個天數內」。可用來判斷有沒有應該新增的題目。
      </p>
      <el-skeleton v-if="searchMissesLoading" :rows="3" animated />
      <el-empty v-else-if="searchMissesError" :description="searchMissesError">
        <el-button type="primary" @click="loadSearchMisses">重新載入</el-button>
      </el-empty>
      <el-empty v-else-if="searchMisses.length === 0" description="這段期間沒有搜尋不到結果的關鍵字" />
      <el-table v-else :data="searchMisses" row-key="keyword" max-height="320">
        <el-table-column label="關鍵字" min-width="160">
          <template #default="{ row }">{{ row.keyword }}</template>
        </el-table-column>
        <el-table-column label="累計搜尋次數" width="140">
          <template #default="{ row }">{{ row.count }}</template>
        </el-table-column>
        <el-table-column label="最後搜尋時間" width="180">
          <template #default="{ row }">{{ formatDateTime(row.lastSearchedAt) }}</template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-card shadow="never" class="faq-list__toolbar">
      <div class="faq-list__toolbar-row">
        <div v-if="selectedIds.length > 0" class="faq-list__batch-actions">
          <span class="faq-list__batch-count">已選取 {{ selectedIds.length }} 筆</span>
          <el-button size="small" @click="openBatchCategoryDialog">批次改分類</el-button>
          <el-button size="small" @click="handleBatchShow">批次顯示</el-button>
          <el-button size="small" @click="handleBatchHide">批次隱藏</el-button>
        </div>
        <div v-else class="faq-list__batch-actions-placeholder" />
        <div class="faq-list__toolbar-actions">
          <input ref="csvFileInput" type="file" accept=".csv,text/csv" class="faq-list__hidden-input" @change="handleCsvFileChange">
          <el-button :loading="csvImporting" @click="openCsvFileDialog">匯入 CSV</el-button>
          <el-button :loading="csvExporting" @click="handleExportCsv">匯出 CSV</el-button>
          <el-button type="primary" @click="handleAdd">+ 新增題目</el-button>
        </div>
      </div>
    </el-card>

    <el-card v-if="faqsLoading" shadow="never" class="faq-list__table-card">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="faqsError" shadow="never">
      <el-empty :description="faqsError">
        <el-button type="primary" @click="loadFaqs">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never" class="faq-list__table-card">
      <template v-if="faqs.length > 0">
        <el-table v-if="!isMobile" :data="faqs" row-key="id" @selection-change="handleSelectionChange">
          <el-table-column type="selection" width="44" />
          <el-table-column label="問題" min-width="220">
            <template #default="{ row }">
              <span>{{ row.questionZh || '（未命名）' }}</span>
              <el-tag v-if="row.isShared" type="info" size="small" class="faq-list__inline-tag">共用內容</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="分類" min-width="160">
            <template #default="{ row }">
              <el-tag v-for="c in row.categories" :key="c.id" size="small" class="faq-list__inline-tag">
                {{ c.nameZh || c.slug }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="狀態" width="90">
            <template #default="{ row }">
              <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">
                {{ row.status === 'published' ? '顯示' : '隱藏' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="瀏覽數" width="90">
            <template #default="{ row }">{{ row.viewCount.toLocaleString('zh-Hant') }}</template>
          </el-table-column>
          <el-table-column label="評價" width="120">
            <template #default="{ row }">
              <span>👍 {{ row.helpfulCount }}  👎 {{ row.unhelpfulCount }}</span>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="140" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
              <el-button v-if="!row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>

        <div v-else class="faq-list__cards">
          <el-card v-for="row in faqs" :key="row.id" shadow="never" class="faq-list__card">
            <div class="faq-list__card-title">{{ row.questionZh || '（未命名）' }}</div>
            <div class="faq-list__card-meta">
              <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">
                {{ row.status === 'published' ? '顯示' : '隱藏' }}
              </el-tag>
              <span>👍 {{ row.helpfulCount }}  👎 {{ row.unhelpfulCount }}</span>
            </div>
            <div class="faq-list__card-actions">
              <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
              <el-button v-if="!row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </div>
          </el-card>
        </div>
      </template>
      <el-empty v-else description="找不到符合條件的題目">
        <el-button type="primary" @click="handleAdd">+ 新增第一題</el-button>
      </el-empty>

      <div v-if="faqs.length > 0" class="faq-list__pagination">
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

    <el-dialog
      v-model="categoryDialogVisible"
      :title="categoryDialogMode === 'create' ? '新增分類' : '編輯分類'"
      width="480px"
    >
      <el-alert
        v-if="categoryFormError"
        :title="categoryFormError"
        type="warning"
        show-icon
        class="faq-list__form-error"
        @close="categoryFormError = null"
      />
      <el-form label-position="top">
        <el-form-item label="網址名稱" required>
          <el-input v-model="categoryForm.slug" placeholder="例如 join-team" />
        </el-form-item>
        <BilingualShortField
          label="名稱"
          :zh="categoryForm.nameZh"
          :en="categoryForm.nameEn"
          required
          @update:zh="(v) => (categoryForm.nameZh = v)"
          @update:en="(v) => (categoryForm.nameEn = v)"
        />
        <el-form-item label="排序">
          <el-input-number v-model="categoryForm.sortOrder" :min="0" />
        </el-form-item>
        <el-form-item label="啟用">
          <el-switch v-model="categoryForm.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="categoryDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="categorySaving" @click="saveCategory">儲存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="batchCategoryDialogVisible" title="批次改分類" width="420px">
      <p class="faq-list__hint">整批取代成同一組分類（不是新增或移除既有分類）。</p>
      <el-select v-model="batchCategoryIds" multiple placeholder="請選擇分類" style="width: 100%">
        <el-option v-for="c in categories" :key="c.id" :label="c.nameZh || c.slug" :value="c.id" />
      </el-select>
      <template #footer>
        <el-button @click="batchCategoryDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmBatchCategory">確定</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="csvImportDialogVisible" title="CSV 匯入結果" width="600px">
      <template v-if="csvImportResult">
        <el-result
          v-if="csvImportResult.errors.length === 0"
          icon="success"
          :title="`已匯入 ${csvImportResult.importedCount} 題`"
        />
        <template v-else>
          <el-alert
            title="整份檔案有錯誤列，本次沒有任何一列被寫入，請修正後重新上傳。"
            type="error"
            show-icon
            class="faq-list__form-error"
          />
          <el-table :data="csvImportResult.errors" max-height="360">
            <el-table-column label="行號" width="80">
              <template #default="{ row }">{{ row.rowNumber }}</template>
            </el-table-column>
            <el-table-column label="錯誤原因">
              <template #default="{ row }">{{ row.reason }}</template>
            </el-table-column>
          </el-table>
        </template>
      </template>
      <template #footer>
        <el-button type="primary" @click="csvImportDialogVisible = false">關閉</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.faq-list__section,
.faq-list__filters,
.faq-list__toolbar {
  margin-bottom: 12px;
}

.faq-list__card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.faq-list__filter-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.faq-list__filter-keyword {
  width: 220px;
  max-width: 100%;
}

.faq-list__filter-select {
  width: 160px;
  max-width: 100%;
}

.faq-list__hint {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.faq-list__toolbar-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
}

.faq-list__batch-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.faq-list__batch-actions-placeholder {
  flex: 1;
}

.faq-list__batch-count {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.faq-list__toolbar-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.faq-list__hidden-input {
  display: none;
}

.faq-list__inline-tag {
  margin-right: 4px;
}

.faq-list__form-error {
  margin-bottom: 12px;
}

.faq-list__pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

.faq-list__cards {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.faq-list__card {
  --el-card-padding: 12px;
}

.faq-list__card-title {
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 6px;
}

.faq-list__card-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
}

.faq-list__card-actions {
  display: flex;
  gap: 4px;
  border-top: 1px solid var(--el-border-color-lighter);
  padding-top: 8px;
}

@media (max-width: 767px) {
  .faq-list__pagination {
    justify-content: center;
  }
}
</style>
