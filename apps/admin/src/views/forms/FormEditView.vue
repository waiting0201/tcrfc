<script setup lang="ts">
/**
 * G1 表單設計器——編輯頁（對照 apps/api/README.md「S1-10」）。**沒有建立模式**：9 個表單是固定
 * 目錄，這裡只能編輯既有一筆的設定，以及底下動態欄位的新增／編輯／刪除。
 *
 * ⚠️ **兩個規格缺口原樣呈現，不假裝做得到**（依任務指示、apps/api/README.md「S1-10」段）：
 * ① 「檔案上傳」欄位型別目前只收文字或網址，不是真正的檔案上傳——全系統沒有通用檔案儲存服務；
 * ② 「防機器人驗證」目前只是資料庫旗標，開啟後前台會顯示驗證元件，但伺服器端不會真的檢查——
 * 全系統沒有串接任何 CAPTCHA 服務。畫面上用中文說明清楚，不放一個看起來會生效但其實不會的開關。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useFormsPermissions } from '@/composables/useFormsPermissions'
import { activeClubId } from '@/auth/clubAccess'
import {
  getAdminForm,
  updateAdminForm,
  createAdminFormField,
  updateAdminFormField,
  deleteAdminFormField,
  type AdminFormFieldDto,
} from '@/api/adminForms'
import { AdminApiError } from '@/api/http'
import { formCodeLabel, FIELD_TYPE_ORDER, FIELD_TYPE_LABEL, FIELD_TYPES_REQUIRING_OPTIONS, type FormFieldTypeCode } from '@/types/forms'

const route = useRoute()
const router = useRouter()
const { canManageForms } = useFormsPermissions()
const isReadOnly = computed(() => !canManageForms.value)

// 沿用專案既有慣例：id 用 ref 存一次性快照（這裡沒有建立模式，不會有「建立成功後改 id」的情況，
// 但同一份元件實例若直接切換到另一筆表單的網址仍可能沿用舊值——目前 UI 沒有提供這種直接跳轉的
// 連結，一律要先回列表，同 `ProgramItemEditView.vue` 等既有頁面的既知限制）。
const formId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  notifyEmails: '',
  captchaEnabled: false,
  redirectPath: '',
  autoReplyBodyZh: '',
  autoReplyBodyEn: '',
})
const baselineJson = ref('')
const formCode = ref('')
const fields = ref<AdminFormFieldDto[]>([])

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadForm() {
  loadState.value = 'loading'
  try {
    const detail = await getAdminForm(activeClubId.value, formId.value!)
    formCode.value = detail.formCode
    form.notifyEmails = detail.notifyEmails ?? ''
    form.captchaEnabled = detail.captchaEnabled
    form.redirectPath = detail.redirectPath ?? ''
    form.autoReplyBodyZh = detail.autoReplyBodyZh ?? ''
    form.autoReplyBodyEn = detail.autoReplyBodyEn ?? ''
    fields.value = [...detail.fields].sort((a, b) => a.sortOrder - b.sortOrder)
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') {
      loadState.value = 'not-found'
    } else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}

onMounted(loadForm)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => `${canManageForms.value ? '編輯' : '檢視'}表單：${formCodeLabel(formCode.value)}`)

function validateEmails(raw: string): boolean {
  if (!raw.trim()) return true
  const parts = raw.split(/[,;]/).map((s) => s.trim()).filter(Boolean)
  const pattern = /^[^@\s]+@[^@\s]+\.[^@\s]+$/
  const bad = parts.find((p) => !pattern.test(p))
  if (bad) {
    formError.value = `「${bad}」不是合法的 Email 格式，請確認收件通知欄位（可用逗號分隔多人）`
    return false
  }
  return true
}

function validate(): boolean {
  formError.value = null
  if (!validateEmails(form.notifyEmails)) return false
  if (form.redirectPath.trim()) {
    const p = form.redirectPath.trim()
    if (!p.startsWith('/') && !/^https?:\/\//i.test(p)) {
      formError.value = '送出後導向的網址要用「/」開頭的相對路徑，或完整的 http(s):// 網址'
      return false
    }
  }
  return true
}

async function handleSave() {
  if (isReadOnly.value) return
  if (!validate()) return
  saving.value = true
  formError.value = null
  try {
    await updateAdminForm(activeClubId.value, formId.value!, {
      notifyEmails: form.notifyEmails.trim() || null,
      captchaEnabled: form.captchaEnabled,
      redirectPath: form.redirectPath.trim() || null,
      autoReplyBodyZh: form.autoReplyBodyZh || null,
      autoReplyBodyEn: form.autoReplyBodyEn || null,
    })
    ElMessage.success('已儲存')
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/inquiries/builder')
}

function retryLoad() {
  loadForm()
}

// ── 動態欄位管理 ──────────────────────────────────────────────────────────────

const fieldDialogVisible = ref(false)
const fieldDialogMode = ref<'create' | 'edit'>('create')
const fieldSaving = ref(false)
const fieldDialogError = ref<string | null>(null)
const editingFieldId = ref<string | null>(null)

const fieldForm = reactive({
  fieldKey: '',
  fieldType: 'text' as FormFieldTypeCode,
  isRequired: false,
  validationRule: '',
  options: [] as string[],
  isSummary: false,
})
const newOptionText = ref('')

const fieldNeedsOptions = computed(() => FIELD_TYPES_REQUIRING_OPTIONS.has(fieldForm.fieldType))

function openCreateFieldDialog() {
  fieldDialogMode.value = 'create'
  editingFieldId.value = null
  fieldForm.fieldKey = ''
  fieldForm.fieldType = 'text'
  fieldForm.isRequired = false
  fieldForm.validationRule = ''
  fieldForm.options = []
  fieldForm.isSummary = false
  newOptionText.value = ''
  fieldDialogError.value = null
  fieldDialogVisible.value = true
}

function openEditFieldDialog(field: AdminFormFieldDto) {
  fieldDialogMode.value = 'edit'
  editingFieldId.value = field.id
  fieldForm.fieldKey = field.fieldKey
  fieldForm.fieldType = field.fieldType as FormFieldTypeCode
  fieldForm.isRequired = field.isRequired
  fieldForm.validationRule = field.validationRule ?? ''
  fieldForm.options = field.options ? [...field.options] : []
  fieldForm.isSummary = field.isSummary
  newOptionText.value = ''
  fieldDialogError.value = null
  fieldDialogVisible.value = true
}

function addOption() {
  const value = newOptionText.value.trim()
  if (!value) return
  if (fieldForm.options.includes(value)) {
    fieldDialogError.value = '這個選項已經存在了'
    return
  }
  fieldForm.options.push(value)
  newOptionText.value = ''
}

function removeOption(index: number) {
  fieldForm.options.splice(index, 1)
}

const FIELD_KEY_PATTERN = /^[a-z][a-z0-9_]{0,63}$/

function validateFieldForm(): boolean {
  fieldDialogError.value = null
  if (!FIELD_KEY_PATTERN.test(fieldForm.fieldKey)) {
    fieldDialogError.value = '欄位代碼只能是英文小寫字母開頭，接英文小寫字母、數字或底線，長度 1–64（例如 experience、cooperation_direction）'
    return false
  }
  const isDuplicate = fields.value.some(
    (f) => f.fieldKey === fieldForm.fieldKey && (fieldDialogMode.value === 'create' || f.id !== editingFieldId.value),
  )
  if (isDuplicate) {
    fieldDialogError.value = `這張表單已經有欄位代碼「${fieldForm.fieldKey}」，請換一個名稱`
    return false
  }
  if (fieldNeedsOptions.value && fieldForm.options.length === 0) {
    fieldDialogError.value = '下拉或多選欄位至少要有一個選項'
    return false
  }
  return true
}

async function submitFieldDialog() {
  if (!validateFieldForm()) return
  fieldSaving.value = true
  try {
    const payload = {
      fieldKey: fieldForm.fieldKey,
      fieldType: fieldForm.fieldType,
      isRequired: fieldForm.isRequired,
      validationRule: fieldForm.validationRule.trim() || null,
      options: fieldNeedsOptions.value ? fieldForm.options : null,
      isSummary: fieldForm.isSummary,
    }
    if (fieldDialogMode.value === 'create') {
      // 不自己算 sortOrder——後端省略時會自動接在最後一個欄位之後（見 `createAdminFormField` 註解）。
      await createAdminFormField(activeClubId.value, formId.value!, { ...payload })
    } else {
      const current = fields.value.find((f) => f.id === editingFieldId.value)
      await updateAdminFormField(activeClubId.value, formId.value!, editingFieldId.value!, {
        ...payload,
        sortOrder: current?.sortOrder ?? 0,
      })
    }
    ElMessage.success(fieldDialogMode.value === 'create' ? '已新增欄位' : '已儲存欄位')
    fieldDialogVisible.value = false
    await loadForm()
  } catch (error) {
    fieldDialogError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    fieldSaving.value = false
  }
}

async function handleDeleteField(field: AdminFormFieldDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除欄位「${field.fieldKey}」嗎？這個動作無法復原。`, '刪除欄位', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdminFormField(activeClubId.value, formId.value!, field.id)
    ElMessage.success('已刪除')
    await loadForm()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

function moveField(field: AdminFormFieldDto, direction: -1 | 1) {
  if (isReadOnly.value) return
  const index = fields.value.findIndex((f) => f.id === field.id)
  const targetIndex = index + direction
  if (index < 0 || targetIndex < 0 || targetIndex >= fields.value.length) return
  const target = fields.value[targetIndex]
  swapSortOrder(field, target)
}

/** 後端沒有批次重新排序的端點（見 apps/api/README.md「S1-10」規劃書沒寫清楚第 8 點），
 * 依建議「依序對每個異動的欄位各呼叫一次」，故意不用 `Promise.all` 併發送出。 */
async function swapSortOrder(a: AdminFormFieldDto, b: AdminFormFieldDto) {
  try {
    await updateAdminFormField(activeClubId.value, formId.value!, a.id, {
      fieldKey: a.fieldKey,
      fieldType: a.fieldType,
      isRequired: a.isRequired,
      validationRule: a.validationRule ?? null,
      options: a.options ?? null,
      isSummary: a.isSummary,
      sortOrder: b.sortOrder,
    })
    await updateAdminFormField(activeClubId.value, formId.value!, b.id, {
      fieldKey: b.fieldKey,
      fieldType: b.fieldType,
      isRequired: b.isRequired,
      validationRule: b.validationRule ?? null,
      options: b.options ?? null,
      isSummary: b.isSummary,
      sortOrder: a.sortOrder,
    })
    await loadForm()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '調整排序失敗，請稍後再試')
  }
}
</script>

<template>
  <div class="form-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="G1" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這個表單，可能不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState !== 'not-found'" type="primary" @click="retryLoad">重新載入</el-button>
        <el-button v-else type="primary" @click="handleBack">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="form-edit__form-error"
        @close="formError = null"
      />
      <el-alert
        v-if="isReadOnly"
        title="你的帳號只有檢視權限，沒有這個模組的編輯權限"
        type="info"
        show-icon
        :closable="false"
        class="form-edit__form-error"
      />

      <el-form label-position="top" :disabled="isReadOnly">
        <el-card shadow="never" header="表單設定" class="form-edit__section">
          <el-form-item label="收件通知 Email（可多人，以逗號分隔）">
            <el-input v-model="form.notifyEmails" placeholder="例如 academy@tcrfc.tw, office@tcrfc.tw" />
          </el-form-item>
          <el-form-item label="送出後導向頁（選填，留空維持在原頁顯示送出成功）">
            <el-input v-model="form.redirectPath" placeholder="例如 /zh/thank-you/ 或完整網址" />
          </el-form-item>
          <el-form-item label="防機器人驗證">
            <el-switch v-model="form.captchaEnabled" />
            <p class="form-edit__hint">
              開啟後前台會顯示防機器人驗證元件，但系統目前尚未串接驗證服務，送出時**不會真的檢查**是否為機器人——目前只靠送出頻率限制與隱藏誘捕欄位防護，等日後取得驗證服務的憑證才會真正生效。
            </p>
          </el-form-item>
          <BilingualTextareaField
            label="自動回覆信內容"
            :zh="form.autoReplyBodyZh"
            :en="form.autoReplyBodyEn"
            :rows="4"
            @update:zh="(v) => (form.autoReplyBodyZh = v)"
            @update:en="(v) => (form.autoReplyBodyEn = v)"
          />
          <p class="form-edit__hint form-edit__hint--warning">
            系統目前還沒有接上寄信服務：收件通知信與這裡設定的自動回覆信內容都只是設定值，訪客送出表單後**不會真的收到信**，後台這裡看到的送出紀錄不受影響。
          </p>
        </el-card>
      </el-form>

      <el-card shadow="never" header="表單欄位" class="form-edit__section">
        <div class="form-edit__fields-toolbar">
          <p class="form-edit__hint">
            「姓名」「聯絡方式」兩個慣用欄位代碼（<code>name</code>／<code>contact</code>）會被收件匣拿來顯示對應欄位，改名或刪除會讓收件匣那兩欄顯示空白。
          </p>
          <el-button v-if="canManageForms" type="primary" @click="openCreateFieldDialog">+ 新增欄位</el-button>
        </div>

        <el-table v-if="fields.length > 0" :data="fields" row-key="id">
          <el-table-column label="欄位代碼" min-width="160">
            <template #default="{ row }">{{ row.fieldKey }}</template>
          </el-table-column>
          <el-table-column label="型別" width="110">
            <template #default="{ row }">{{ FIELD_TYPE_LABEL[row.fieldType as FormFieldTypeCode] ?? row.fieldType }}</template>
          </el-table-column>
          <el-table-column label="必填" width="70">
            <template #default="{ row }">
              <el-tag v-if="row.isRequired" size="small" type="warning">必填</el-tag>
              <span v-else>—</span>
            </template>
          </el-table-column>
          <el-table-column label="選項" min-width="160">
            <template #default="{ row }">{{ row.options && row.options.length > 0 ? row.options.join('、') : '—' }}</template>
          </el-table-column>
          <el-table-column label="內容摘要來源" width="110">
            <template #default="{ row }">
              <el-tag v-if="row.isSummary" size="small" type="success">是</el-tag>
              <span v-else>—</span>
            </template>
          </el-table-column>
          <el-table-column v-if="canManageForms" label="操作" width="180" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text @click="moveField(row, -1)">上移</el-button>
              <el-button size="small" text @click="moveField(row, 1)">下移</el-button>
              <el-button size="small" text type="primary" @click="openEditFieldDialog(row)">編輯</el-button>
              <el-button size="small" text type="danger" @click="handleDeleteField(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <el-empty v-else description="這張表單目前沒有任何欄位">
          <el-button v-if="canManageForms" type="primary" @click="openCreateFieldDialog">+ 新增第一個欄位</el-button>
        </el-empty>
      </el-card>

      <div v-if="!isReadOnly" class="form-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存表單設定</el-button>
      </div>
    </template>

    <el-dialog
      v-model="fieldDialogVisible"
      :title="fieldDialogMode === 'create' ? '新增欄位' : '編輯欄位'"
      width="520px"
      :close-on-click-modal="false"
    >
      <el-alert v-if="fieldDialogError" :title="fieldDialogError" type="warning" show-icon class="form-edit__dialog-error" />
      <el-form label-position="top">
        <el-form-item label="欄位代碼（英文小寫，例如 experience）" required>
          <el-input v-model="fieldForm.fieldKey" placeholder="英文小寫字母開頭，可含數字與底線" />
        </el-form-item>
        <el-form-item label="欄位型別" required>
          <el-select v-model="fieldForm.fieldType" style="width: 100%">
            <el-option v-for="t in FIELD_TYPE_ORDER" :key="t" :label="FIELD_TYPE_LABEL[t]" :value="t" />
          </el-select>
          <p v-if="fieldForm.fieldType === 'file'" class="form-edit__hint form-edit__hint--warning">
            「檔案上傳」目前只能填文字或網址（例如雲端硬碟連結），系統還沒有真正接收檔案的功能。
          </p>
        </el-form-item>
        <el-form-item label="是否必填">
          <el-switch v-model="fieldForm.isRequired" />
        </el-form-item>
        <el-form-item v-if="fieldNeedsOptions" label="選項清單（下拉／多選必填，至少一項）">
          <div class="form-edit__options">
            <el-tag v-for="(opt, index) in fieldForm.options" :key="opt" closable class="form-edit__option-tag" @close="removeOption(index)">
              {{ opt }}
            </el-tag>
          </div>
          <div class="form-edit__option-add">
            <el-input v-model="newOptionText" placeholder="輸入選項內容後按新增" @keyup.enter="addOption" />
            <el-button @click="addOption">新增選項</el-button>
          </div>
        </el-form-item>
        <el-form-item v-if="!['select', 'multiselect', 'consent', 'file'].includes(fieldForm.fieldType)" label="驗證規則（選填，正規表示式）">
          <el-input v-model="fieldForm.validationRule" placeholder="例如電話格式，留空表示不額外驗證格式" />
        </el-form-item>
        <el-form-item label="標記為內容摘要">
          <el-switch v-model="fieldForm.isSummary" />
          <p class="form-edit__hint">
            收件匣清單的「內容摘要」欄會取這個欄位的值。同一張表單最多一個欄位可以標記，設定新的會自動取代舊的。
          </p>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="fieldDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="fieldSaving" @click="submitFieldDialog">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.form-edit {
  max-width: 860px;
  margin: 0 auto 88px;
}

.form-edit__form-error {
  margin-bottom: 16px;
}

.form-edit__section {
  margin-bottom: 16px;
}

.form-edit__hint {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.form-edit__hint--warning {
  color: var(--el-color-warning);
}

.form-edit__fields-toolbar {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.form-edit__options {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 8px;
}

.form-edit__option-add {
  display: flex;
  gap: 8px;
}

.form-edit__dialog-error {
  margin-bottom: 12px;
}

.form-edit__action-bar {
  position: fixed;
  bottom: 0;
  left: var(--admin-sidebar-width-expanded);
  right: 0;
  background: var(--admin-bg-surface-2);
  border-top: 1px solid var(--admin-border);
  padding: 12px 24px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  z-index: 10;
}

@media (max-width: 1023px) {
  .form-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .form-edit__action-bar {
    justify-content: stretch;
  }

  .form-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>
