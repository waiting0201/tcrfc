<script setup lang="ts">
/**
 * C4「賽季」管理（apps/api/README.md「後台欄位串接稽核的後端修正」A-12）。
 *
 * 賽季是整個俱樂部共用的設定（賽程、賽事系列、積分榜、榮譽、球員數據、會籍都掛在賽季底下），
 * 欄位只有代碼與起訖日、沒有雙語內容，所以用列表頁＋對話框，不另做整頁編輯頁（docs/21 §3 的分界點）。
 *
 * - 「當季」＝今天（台北時間）落在起訖日之內，只是畫面標示，不是後端欄位。
 * - 被任何資料使用的賽季不能刪：按鈕停用、旁邊顯示使用情形（後端 `usage`）；
 *   後端若仍回 409（例如剛好在這之前被別人新增了賽事），訊息原樣顯示。
 * - 寫入需要整個俱樂部的球隊授權（學院限定或個別球隊帳號會被後端 403），畫面不預先判斷，
 *   403 訊息直接顯示。
 * - 欄位錯誤鍵 `code`／`startOn`／`endOn`（`startOn` 也用於「期間與別的賽季重疊」的 409）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import FormField from '@/components/FormField.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import {
  createAdminSeason,
  deleteAdminSeason,
  listSeasonsForManage,
  updateAdminSeason,
  type AdminSeasonDto,
} from '@/api/adminSeasons'
import { AdminApiError } from '@/api/http'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { taipeiToday } from '@/utils/dateTime'

const club = computed(() => activeClubId.value)
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canView, canCreate, canUpdate, canDelete } = useCrudPermissions('team.match')

const seasons = ref<AdminSeasonDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    seasons.value = await listSeasonsForManage(club.value)
  } catch (error) {
    seasons.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '賽季清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, load)

const today = computed(() => taipeiToday())
const isCurrent = (s: AdminSeasonDto) => s.startOn <= today.value && today.value <= s.endOn
/** 起日新的在上面。 */
const sorted = computed(() => [...seasons.value].sort((a, b) => b.startOn.localeCompare(a.startOn)))

const usageText = (s: AdminSeasonDto) => s.usage.map((u) => `${u.label} ${u.count} 筆`).join('、')
const usageTotal = (s: AdminSeasonDto) => s.usage.reduce((sum, u) => sum + u.count, 0)

// ── 新增／編輯對話框 ───────────────────────────────────────────────────────────────────
const dialogVisible = ref(false)
const editingId = ref<string | null>(null)
const saving = ref(false)
const dialogError = ref<string | null>(null)
const form = reactive({ code: '', startOn: '', endOn: '' })
const formErrors = provideFormErrors()

function openDialog(row?: AdminSeasonDto) {
  editingId.value = row?.id ?? null
  form.code = row?.code ?? ''
  form.startOn = row?.startOn ?? ''
  form.endOn = row?.endOn ?? ''
  dialogError.value = null
  formErrors.clearAll()
  dialogVisible.value = true
}

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  const code = form.code.trim()
  if (!code) errors.code = '請輸入賽季代碼'
  else if (code.length > 16) errors.code = '賽季代碼最多 16 個字'
  else if (!/^[A-Za-z0-9/_-]+$/.test(code)) errors.code = '賽季代碼只能用英數字與 / - _，例如 2026/27'
  if (!form.startOn) errors.startOn = '請選擇開始日期'
  if (!form.endOn) errors.endOn = '請選擇結束日期'
  else if (form.startOn && form.endOn <= form.startOn) errors.endOn = '結束日期必須晚於開始日期'
  return errors
}

async function save() {
  dialogError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const payload = { code: form.code.trim(), startOn: form.startOn, endOn: form.endOn }
    if (editingId.value) await updateAdminSeason(club.value, editingId.value, payload)
    else await createAdminSeason(club.value, payload)
    dialogVisible.value = false
    ElMessage.success('已儲存')
    await load()
  } catch (error) {
    // 送出失敗不關對話框：欄位錯誤標到欄位，對不到欄位的才顯示在對話框上方
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    dialogError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function remove(row: AdminSeasonDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除賽季「${row.code}」嗎？這個動作無法復原。`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdminSeason(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    const message = error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試'
    // 409（仍被使用）訊息會列出被誰引用，用對話框完整顯示；其餘用輕量提示
    const conflict = error instanceof AdminApiError && error.status === 409
    if (conflict || (error instanceof AdminApiError && error.kind === 'forbidden')) {
      await ElMessageBox.alert(message, '無法刪除', { confirmButtonText: '我知道了' })
      if (conflict) await load()
    } else {
      ElMessage.error(message)
    }
  }
}

const isEmpty = computed(() => !loading.value && !loadError.value && seasons.value.length === 0)
</script>

<template>
  <div class="season-list">
    <PageHeader title="賽季">
      <template #meta>
        <FrontendUnitBanner module-code="C4" />
      </template>
    </PageHeader>

    <el-alert
      title="賽季是整個俱樂部共用的設定：賽程、賽事系列、積分榜、榮譽與球員數據都掛在賽季底下。同一俱樂部的賽季期間不能重疊，已經有資料使用的賽季不能刪除。"
      type="info"
      show-icon
      :closable="false"
      class="season-list__hint"
    />

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :image-size="96" :description="loadError">
        <el-button type="primary" @click="load">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else-if="!canView" shadow="never"><el-empty description="你的帳號沒有查看賽季的權限" /></el-card>

    <template v-else>
      <div v-if="canCreate" class="season-list__toolbar">
        <el-button type="primary" @click="openDialog()">+ 新增賽季</el-button>
      </div>

      <el-card v-if="isEmpty" shadow="never">
        <el-empty description="目前還沒有任何賽季">
          <el-button v-if="canCreate" type="primary" @click="openDialog()">+ 新增第一個賽季</el-button>
        </el-empty>
      </el-card>

      <el-card v-else-if="!isMobile" shadow="never">
        <el-table :data="sorted" row-key="id">
          <el-table-column label="賽季代碼" prop="code" min-width="120" />
          <el-table-column label="開始日期" prop="startOn" width="130" />
          <el-table-column label="結束日期" prop="endOn" width="130" />
          <el-table-column label="目前賽季" width="100">
            <template #default="{ row }">
              <el-tag v-if="isCurrent(row)" type="success" size="small">當季</el-tag>
              <span v-else>—</span>
            </template>
          </el-table-column>
          <el-table-column label="使用中" min-width="200">
            <template #default="{ row }">
              <template v-if="row.inUse">
                <span>{{ usageTotal(row) }} 筆</span>
                <span class="season-list__muted">（{{ usageText(row) }}）</span>
              </template>
              <span v-else class="season-list__muted">沒有資料使用</span>
            </template>
          </el-table-column>
          <el-table-column v-if="canUpdate || canDelete" label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button v-if="canUpdate" size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
              <el-tooltip
                v-if="canDelete"
                :disabled="!row.inUse"
                :content="`這個賽季仍有資料使用，不能刪除：${usageText(row)}`"
                placement="top"
              >
                <span>
                  <el-button size="small" text type="danger" :disabled="row.inUse" @click="remove(row)">刪除</el-button>
                </span>
              </el-tooltip>
            </template>
          </el-table-column>
        </el-table>
      </el-card>

      <MobileCardList v-else :rows="sorted" row-key="id">
        <template #title="{ row }">
          {{ row.code }}
          <el-tag v-if="isCurrent(row)" type="success" size="small">當季</el-tag>
        </template>
        <template #meta="{ row }">
          <div>{{ row.startOn }} ～ {{ row.endOn }}</div>
          <div class="season-list__muted">{{ row.inUse ? `使用中：${usageText(row)}` : '沒有資料使用' }}</div>
        </template>
        <template #actions="{ row }">
          <el-button v-if="canUpdate" size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
          <el-button v-if="canDelete" size="small" text type="danger" :disabled="row.inUse" @click="remove(row)">刪除</el-button>
          <p v-if="canDelete && row.inUse" class="season-list__muted">仍有資料使用，不能刪除</p>
        </template>
      </MobileCardList>
    </template>

    <el-dialog v-model="dialogVisible" :title="editingId ? '編輯賽季' : '新增賽季'" width="480px" :close-on-click-modal="false">
      <el-alert v-if="dialogError" :title="dialogError" type="warning" show-icon class="season-list__dialog-error" @close="dialogError = null" />
      <el-form label-position="top" @submit.prevent="save">
        <FormField field="code" label="賽季代碼" required>
          <el-input v-model="form.code" maxlength="16" placeholder="英數字與 / - _，例如 2026/27" />
        </FormField>
        <FormField field="startOn" label="開始日期" required>
          <el-date-picker
            v-model="form.startOn"
            type="date"
            value-format="YYYY-MM-DD"
            placeholder="選擇日期"
            style="width: 100%"
            @change="formErrors.clear('startOn')"
          />
        </FormField>
        <FormField field="endOn" label="結束日期" required>
          <el-date-picker
            v-model="form.endOn"
            type="date"
            value-format="YYYY-MM-DD"
            placeholder="選擇日期，須晚於開始日期"
            style="width: 100%"
            @change="formErrors.clear('endOn')"
          />
        </FormField>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.season-list__hint {
  margin-bottom: 12px;
}

.season-list__toolbar {
  margin-bottom: 12px;
  display: flex;
  justify-content: flex-end;
}

.season-list__muted {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.season-list__dialog-error {
  margin-bottom: 12px;
}
</style>
