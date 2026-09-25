<script setup lang="ts">
/**
 * H2 301 轉址管理（對應主站規劃書 §4.8 H「301 轉址管理（含批次匯入）」；
 * apps/api/README.md「S1-12」）。單頁：列表＋搜尋＋分頁、新增／編輯（對話框）、刪除、
 * CSV 匯入（整批 upsert，任一列有錯整批不寫入）／匯出。版面沿用 `FaqListView.vue` 的
 * 分類管理段落（同樣是「筆數不多、欄位簡單」的小型 CRUD）與 CSV 匯入匯出段落的既有寫法。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { activeClubId } from '@/auth/clubAccess'
import {
  createAdminRedirect,
  deleteAdminRedirect,
  downloadAdminRedirectsCsv,
  importAdminRedirectsCsv,
  listAdminRedirects,
  updateAdminRedirect,
  type AdminRedirectDto,
  type RedirectCsvImportResultDto,
} from '@/api/adminSeo'
import { formatDateTime } from '@/utils/formatDateTime'
import { AdminApiError } from '@/api/http'

const club = computed(() => activeClubId.value)
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

// ── 列表 ──────────────────────────────────────────────────────────────────────────

const keyword = ref('')
const currentPage = ref(1)
const pageSize = ref(20)

const redirects = ref<AdminRedirectDto[]>([])
const totalCount = ref(0)
const listLoading = ref(true)
const listError = ref<string | null>(null)

async function loadRedirects() {
  listLoading.value = true
  listError.value = null
  try {
    const page = await listAdminRedirects(club.value, {
      keyword: keyword.value || undefined,
      page: currentPage.value,
      pageSize: pageSize.value,
    })
    redirects.value = page.items
    totalCount.value = page.totalCount
  } catch (error) {
    redirects.value = []
    totalCount.value = 0
    listError.value = error instanceof AdminApiError ? error.message : '轉址清單載入失敗，請稍後再試'
  } finally {
    listLoading.value = false
  }
}

function bootstrapForClub() {
  currentPage.value = 1
  keyword.value = ''
  loadRedirects()
}

onMounted(bootstrapForClub)
watch(club, bootstrapForClub)
watch([currentPage, pageSize], loadRedirects)

function applyKeyword() {
  currentPage.value = 1
  loadRedirects()
}

// ── 新增／編輯（對話框）──────────────────────────────────────────────────────────────

interface RedirectFormState {
  id: string | null
  fromPath: string
  toPath: string
  isActive: boolean
}

function emptyRedirectForm(): RedirectFormState {
  return { id: null, fromPath: '', toPath: '', isActive: true }
}

const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const form = reactive<RedirectFormState>(emptyRedirectForm())
const formError = ref<string | null>(null)
const saving = ref(false)

function openCreateDialog() {
  dialogMode.value = 'create'
  Object.assign(form, emptyRedirectForm())
  formError.value = null
  dialogVisible.value = true
}

function openEditDialog(row: AdminRedirectDto) {
  dialogMode.value = 'edit'
  Object.assign(form, { id: row.id, fromPath: row.fromPath, toPath: row.toPath, isActive: row.isActive })
  formError.value = null
  dialogVisible.value = true
}

async function saveRedirect() {
  formError.value = null
  if (!form.fromPath.trim()) {
    formError.value = '請輸入來源網址'
    return
  }
  if (!form.toPath.trim()) {
    formError.value = '請輸入目的網址'
    return
  }
  saving.value = true
  try {
    if (dialogMode.value === 'create') {
      await createAdminRedirect(club.value, {
        fromPath: form.fromPath.trim(),
        toPath: form.toPath.trim(),
        isActive: form.isActive,
      })
      ElMessage.success('已新增')
    } else {
      await updateAdminRedirect(club.value, form.id!, {
        toPath: form.toPath.trim(),
        isActive: form.isActive,
      })
      ElMessage.success('已儲存')
    }
    dialogVisible.value = false
    await loadRedirects()
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: AdminRedirectDto) {
  try {
    await ElMessageBox.confirm(
      `確定要刪除「${row.fromPath} → ${row.toPath}」這筆轉址嗎？這個動作無法復原。`,
      '確認刪除',
      { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await deleteAdminRedirect(club.value, row.id)
    ElMessage.success('已刪除')
    await loadRedirects()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

// ── CSV 匯入／匯出 ───────────────────────────────────────────────────────────────

const csvExporting = ref(false)
async function handleExportCsv() {
  csvExporting.value = true
  try {
    await downloadAdminRedirectsCsv(club.value)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    csvExporting.value = false
  }
}

const csvFileInput = ref<HTMLInputElement | null>(null)
const csvImporting = ref(false)
const csvImportResult = ref<RedirectCsvImportResultDto | null>(null)
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
    const result = await importAdminRedirectsCsv(club.value, file)
    csvImportResult.value = result
    csvImportDialogVisible.value = true
    if (result.errors.length === 0) {
      ElMessage.success(`已匯入 ${result.importedCount} 筆`)
      await loadRedirects()
    }
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯入失敗，請稍後再試')
  } finally {
    csvImporting.value = false
  }
}
</script>

<template>
  <div class="redirect-list">
    <PageHeader title="301 轉址管理">
      <template #meta>
        <FrontendUnitBanner module-code="H" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="redirect-list__hint-card">
      <p class="redirect-list__hint">
        當一個網址搬家或改名了，在這裡登記「舊網址」與「新網址」的對應，訪客或搜尋引擎連到舊網址時會自動轉往新網址，不會看到「找不到頁面」。
      </p>
    </el-card>

    <el-card shadow="never" class="redirect-list__filters">
      <div class="redirect-list__filter-row">
        <el-input
          v-model="keyword"
          placeholder="搜尋來源或目的網址"
          clearable
          class="redirect-list__filter-keyword"
          @keyup.enter="applyKeyword"
          @clear="applyKeyword"
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-button type="primary" @click="applyKeyword">搜尋</el-button>
      </div>
    </el-card>

    <el-card shadow="never" class="redirect-list__toolbar">
      <div class="redirect-list__toolbar-row">
        <input ref="csvFileInput" type="file" accept=".csv,text/csv" class="redirect-list__hidden-input" @change="handleCsvFileChange">
        <el-button :loading="csvImporting" @click="openCsvFileDialog">匯入 CSV</el-button>
        <el-button :loading="csvExporting" @click="handleExportCsv">匯出 CSV</el-button>
        <el-button type="primary" @click="openCreateDialog">+ 新增轉址</el-button>
      </div>
    </el-card>

    <el-card v-if="listLoading" shadow="never" class="redirect-list__table-card">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="listError" shadow="never">
      <el-empty :description="listError">
        <el-button type="primary" @click="loadRedirects">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never" class="redirect-list__table-card">
      <template v-if="redirects.length > 0">
        <el-table v-if="!isMobile" :data="redirects" row-key="id">
          <el-table-column label="來源網址" min-width="220">
            <template #default="{ row }"><code>{{ row.fromPath }}</code></template>
          </el-table-column>
          <el-table-column label="目的網址" min-width="220">
            <template #default="{ row }"><code>{{ row.toPath }}</code></template>
          </el-table-column>
          <el-table-column label="啟用" width="90">
            <template #default="{ row }">
              <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '啟用' : '停用' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="最後更新" width="160">
            <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="140" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="openEditDialog(row)">編輯</el-button>
              <el-button size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>

        <div v-else class="redirect-list__cards">
          <el-card v-for="row in redirects" :key="row.id" shadow="never" class="redirect-list__card">
            <div class="redirect-list__card-title"><code>{{ row.fromPath }}</code> → <code>{{ row.toPath }}</code></div>
            <div class="redirect-list__card-meta">
              <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '啟用' : '停用' }}</el-tag>
              <span>{{ formatDateTime(row.updatedAt) }}</span>
            </div>
            <div class="redirect-list__card-actions">
              <el-button size="small" text type="primary" @click="openEditDialog(row)">編輯</el-button>
              <el-button size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </div>
          </el-card>
        </div>
      </template>
      <el-empty v-else description="目前沒有任何轉址規則">
        <el-button type="primary" @click="openCreateDialog">+ 新增第一筆</el-button>
      </el-empty>

      <div v-if="redirects.length > 0" class="redirect-list__pagination">
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

    <el-dialog v-model="dialogVisible" :title="dialogMode === 'create' ? '新增轉址' : '編輯轉址'" width="480px">
      <el-alert v-if="formError" :title="formError" type="warning" show-icon :closable="false" class="redirect-list__form-error" />
      <el-form label-position="top">
        <el-form-item label="來源網址（舊網址）" required>
          <el-input v-model="form.fromPath" :disabled="dialogMode === 'edit'" placeholder="例如：/old-page/" />
        </el-form-item>
        <p v-if="dialogMode === 'edit'" class="redirect-list__hint">來源網址建立後不能修改，要換來源網址請刪除這筆後重新新增。</p>
        <el-form-item label="目的網址（新網址）" required>
          <el-input v-model="form.toPath" placeholder="例如：/zh/news/new-page/" />
        </el-form-item>
        <el-form-item label="啟用">
          <el-switch v-model="form.isActive" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="saveRedirect">儲存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="csvImportDialogVisible" title="CSV 匯入結果" width="600px">
      <template v-if="csvImportResult">
        <el-result
          v-if="csvImportResult.errors.length === 0"
          icon="success"
          :title="`已匯入 ${csvImportResult.importedCount} 筆`"
        />
        <template v-else>
          <el-alert
            title="整份檔案有錯誤列，本次沒有任何一列被寫入，請修正後重新上傳。"
            type="error"
            show-icon
            class="redirect-list__form-error"
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
.redirect-list__hint-card,
.redirect-list__filters,
.redirect-list__toolbar {
  margin-bottom: 12px;
}

.redirect-list__hint {
  margin: 0;
  font-size: 13px;
  color: var(--admin-text-secondary);
  line-height: 1.7;
}

.redirect-list__filter-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.redirect-list__filter-keyword {
  flex: 1;
  min-width: 220px;
}

.redirect-list__toolbar-row {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  flex-wrap: wrap;
}

.redirect-list__hidden-input {
  display: none;
}

.redirect-list__pagination {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}

.redirect-list__cards {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.redirect-list__card-title {
  font-size: 13px;
  word-break: break-all;
}

.redirect-list__card-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 8px 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.redirect-list__card-actions {
  display: flex;
  gap: 8px;
}

.redirect-list__form-error {
  margin-bottom: 12px;
}
</style>
