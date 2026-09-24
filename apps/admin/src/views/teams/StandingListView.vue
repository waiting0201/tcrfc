<script setup lang="ts">
/**
 * C4「積分榜」——列表頁。對照 apps/api/README.md「S1-8」。規劃書原文「積分榜：手動維護表格或
 * 匯入 CSV」——這裡就是那張表格：依賽季檢視、逐列新增／編輯／刪除，或整季 CSV 替換匯入。
 *
 * ⚠️ 這個模組沒有球隊列級授權（`standings` 沒有 `team_id` 欄位，見 `apps/api/adminStandings.ts`
 * 檔頭說明），受球隊範圍限制的帳號（例如學院管理者）目前完全沒有這組權限碼，打這個頁面的任何
 * 寫入端點一律 403「沒有權限」。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminSeasons, type AdminSeasonListItemDto } from '@/api/adminCompetitions'
import {
  createAdminStanding,
  deleteAdminStanding,
  importAdminStandingsCsv,
  listAdminStandings,
  updateAdminStanding,
  type AdminStandingListItemDto,
  type StandingCsvImportResultDto,
} from '@/api/adminStandings'
import { AdminApiError } from '@/api/http'

const club = computed(() => activeClubId.value)

const seasons = ref<AdminSeasonListItemDto[]>([])
const seasonId = ref('')
const seasonCode = computed(() => seasons.value.find((s) => s.id === seasonId.value)?.code ?? '')

const standings = ref<AdminStandingListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadSeasons() {
  try {
    seasons.value = await listAdminSeasons(club.value)
    if (seasons.value.length > 0 && !seasonId.value) {
      seasonId.value = seasons.value[0].id
    }
  } catch {
    seasons.value = []
  }
}

async function loadStandings() {
  if (!seasonId.value) {
    standings.value = []
    loading.value = false
    return
  }
  loading.value = true
  loadError.value = null
  try {
    standings.value = await listAdminStandings(club.value, seasonId.value)
  } catch (error) {
    standings.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '積分榜載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function bootstrap() {
  seasonId.value = ''
  await loadSeasons()
  await loadStandings()
}

onMounted(bootstrap)
watch(club, bootstrap)
watch(seasonId, loadStandings)

const isEmpty = computed(() => !loading.value && !loadError.value && standings.value.length === 0)

// ── 新增／編輯（單列，對話框）─────────────────────────────────────────────────────────

interface StandingFormState {
  id: string | null
  teamName: string
  rank: number | null
  played: number | null
  points: number | null
}

function emptyForm(): StandingFormState {
  return { id: null, teamName: '', rank: null, played: null, points: null }
}

const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const form = reactive<StandingFormState>(emptyForm())
const formError = ref<string | null>(null)
const saving = ref(false)

function openCreateDialog() {
  if (!seasonId.value) {
    ElMessage.warning('請先選擇賽季')
    return
  }
  dialogMode.value = 'create'
  Object.assign(form, emptyForm())
  formError.value = null
  dialogVisible.value = true
}

function openEditDialog(row: AdminStandingListItemDto) {
  dialogMode.value = 'edit'
  Object.assign(form, {
    id: row.id,
    teamName: row.teamName,
    rank: row.rank ?? null,
    played: row.played ?? null,
    points: row.points ?? null,
  })
  formError.value = null
  dialogVisible.value = true
}

async function saveForm() {
  if (!form.teamName.trim()) {
    formError.value = '請輸入球隊名稱'
    return
  }
  saving.value = true
  formError.value = null
  try {
    const payload = {
      seasonId: seasonId.value,
      teamName: form.teamName.trim(),
      rank: form.rank,
      played: form.played,
      points: form.points,
    }
    if (dialogMode.value === 'create') {
      await createAdminStanding(club.value, payload)
      ElMessage.success('已新增')
    } else {
      await updateAdminStanding(club.value, form.id!, payload)
      ElMessage.success('已儲存')
    }
    dialogVisible.value = false
    await loadStandings()
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      formError.value = error.message
    } else {
      formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
    }
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: AdminStandingListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.teamName}」這一列積分榜資料嗎？這個動作無法復原。`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdminStanding(club.value, row.id)
    ElMessage.success('已刪除')
    await loadStandings()
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      await ElMessageBox.alert(error.message, '沒有權限刪除', { confirmButtonText: '我知道了' })
    } else {
      ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
    }
  }
}

// ── CSV 匯入（整季替換：匯入前會先刪除該賽季既有全部資料，再整批寫入新內容）───────────────────

const csvFileInput = ref<HTMLInputElement | null>(null)
const csvImporting = ref(false)
const csvImportResult = ref<StandingCsvImportResultDto | null>(null)
const csvImportDialogVisible = ref(false)

function openCsvFileDialog() {
  csvFileInput.value?.click()
}

async function handleCsvFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  try {
    await ElMessageBox.confirm(
      '匯入會先完全刪除這份 CSV 檔案內賽季代碼所屬賽季的全部既有積分榜資料，再整批寫入檔案內容，這個動作無法復原。確定要匯入嗎？',
      '確認匯入（整季替換）',
      { confirmButtonText: '匯入並取代', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }

  csvImporting.value = true
  try {
    const result = await importAdminStandingsCsv(club.value, file)
    csvImportResult.value = result
    csvImportDialogVisible.value = true
    if (result.errors.length === 0) {
      ElMessage.success(`已匯入 ${result.replacedCount} 筆（原本 ${result.deletedCount} 筆既有資料已被清除）`)
      await loadSeasons()
      await loadStandings()
    }
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯入失敗，請稍後再試')
  } finally {
    csvImporting.value = false
  }
}
</script>

<template>
  <div class="standing-list">
    <PageHeader title="積分榜">
      <template #meta>
        <FrontendUnitBanner module-code="C4" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="standing-list__filters">
      <div class="standing-list__filter-row">
        <el-select v-model="seasonId" placeholder="賽季" filterable class="standing-list__filter-select">
          <el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" />
        </el-select>
        <div class="standing-list__filter-placeholder" />
        <input ref="csvFileInput" type="file" accept=".csv,text/csv" class="standing-list__hidden-input" @change="handleCsvFileChange">
        <el-button :loading="csvImporting" @click="openCsvFileDialog">匯入 CSV（整季替換）</el-button>
        <el-button type="primary" @click="openCreateDialog">+ 新增一列</el-button>
      </div>
      <p class="standing-list__hint">
        CSV 匯入是「整季替換」：匯入會先清除檔案內賽季代碼所屬賽季的全部既有資料，再整批寫入檔案內容，不是逐列更新。
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadStandings">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="!isEmpty" :data="standings" row-key="id">
        <el-table-column label="名次" width="90">
          <template #default="{ row }">{{ row.rank ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="球隊名稱" min-width="160">
          <template #default="{ row }">{{ row.teamName }}</template>
        </el-table-column>
        <el-table-column label="出賽場次" width="110">
          <template #default="{ row }">{{ row.played ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="積分" width="90">
          <template #default="{ row }">{{ row.points ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="130" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="openEditDialog(row)">編輯</el-button>
            <el-button size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else :description="seasonId ? '這個賽季還沒有任何積分榜資料' : '請先選擇賽季，或先到「賽事系列」建立賽季'">
        <el-button v-if="seasonId" type="primary" @click="openCreateDialog">+ 新增第一列</el-button>
      </el-empty>
    </el-card>

    <el-dialog v-model="dialogVisible" :title="dialogMode === 'create' ? '新增積分榜列' : '編輯積分榜列'" width="480px">
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="standing-list__form-error" @close="formError = null" />
      <el-form label-position="top">
        <el-form-item label="賽季">
          <el-input :model-value="seasonCode" disabled />
        </el-form-item>
        <el-form-item label="球隊名稱" required>
          <el-input v-model="form.teamName" placeholder="例如：台中磐石，或聯賽其他球隊名稱" />
        </el-form-item>
        <el-form-item label="名次（選填）">
          <el-input-number v-model="form.rank" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="出賽場次（選填）">
          <el-input-number v-model="form.played" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="積分（選填）">
          <el-input-number v-model="form.points" :min="0" style="width: 100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="saveForm">儲存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="csvImportDialogVisible" title="CSV 匯入結果" width="640px">
      <template v-if="csvImportResult">
        <el-result
          v-if="csvImportResult.errors.length === 0"
          icon="success"
          :title="`已匯入 ${csvImportResult.replacedCount} 筆`"
          :sub-title="`原本 ${csvImportResult.deletedCount} 筆既有資料已被清除並換成新內容`"
        />
        <template v-else>
          <el-alert
            title="整份檔案有錯誤列，本次沒有任何資料被異動，請修正後重新上傳。"
            type="error"
            show-icon
            class="standing-list__form-error"
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
.standing-list__filters {
  margin-bottom: 12px;
}

.standing-list__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.standing-list__filter-select {
  width: 200px;
  max-width: 100%;
}

.standing-list__filter-placeholder {
  flex: 1;
}

.standing-list__hidden-input {
  display: none;
}

.standing-list__hint {
  margin: 12px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.standing-list__form-error {
  margin-bottom: 12px;
}
</style>
