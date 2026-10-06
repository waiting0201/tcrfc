<script setup lang="ts">
/**
 * 發票捐贈碼：結帳時顧客可以選擇把電子發票捐贈給哪個團體。
 * 🔴 這份清單全系統共用、不分俱樂部（切換站台看到的是同一份）。前台結帳已有「捐贈發票」選項，電子發票正式開立待取得發票服務後啟用。
 */
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { AdminApiError } from '@/api/http'
import { createDonationCode, deleteDonationCode, listDonationCodes, updateDonationCode, type DonationCodeDto } from '@/api/adminShop'
import { computed } from 'vue'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('shop.donation_code')
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const rows = ref<DonationCodeDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listDonationCodes()
  } catch (error) {
    rows.value = []
    loadError.value = errorText(error, '捐贈碼清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

const open = ref(false)
const saving = ref(false)
/** 對話框頂部提示：只放沒有對到欄位的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
const form = reactive({ id: null as string | null, code: '', orgName: '', isActive: true, sortOrder: 0 })
function openDialog(row: DonationCodeDto | null) {
  formError.value = null
  formErrors.clearAll()
  Object.assign(form, row ? { id: row.id, code: row.code, orgName: row.orgName, isActive: row.isActive, sortOrder: row.sortOrder } : { id: null, code: '', orgName: '', isActive: true, sortOrder: rows.value.length })
  open.value = true
}
async function save() {
  formError.value = null
  const errors: Record<string, string> = {}
  if (!/^\d{3,7}$/.test(form.code.trim())) errors.code = '捐贈碼必須是 3 到 7 位數字'
  if (!form.orgName.trim()) errors.orgName = '請輸入受贈團體名稱'
  if (formErrors.replaceAll(errors)) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  const body = { code: form.code.trim(), orgName: form.orgName.trim(), isActive: form.isActive, sortOrder: form.sortOrder }
  try {
    if (form.id) await updateDonationCode(form.id, body)
    else await createDonationCode(body)
    open.value = false
    ElMessage.success('已儲存')
    await load()
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放對話框頂部
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = errorText(error, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
async function remove(row: DonationCodeDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除捐贈碼 ${row.code}（${row.orgName}）嗎？`, '刪除捐贈碼', { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteDonationCode(row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(errorText(error, '刪除失敗，請稍後再試'))
  }
}
</script>

<template>
  <div class="codes">
    <div class="codes__bar">
      <span class="codes__hint">這份清單全系統共用，不分俱樂部。顧客在結帳時可從這份清單選擇捐贈對象；電子發票正式開立要等取得發票服務後才會啟用。</span>
      <span class="codes__spacer" />
      <el-button v-if="canCreate" type="primary" @click="openDialog(null)">+ 新增捐贈碼</el-button>
    </div>
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="4" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="還沒有捐贈碼" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="捐贈碼" width="120" prop="code" />
          <el-table-column label="受贈團體" min-width="200" prop="orgName" />
          <el-table-column label="狀態" width="90"><template #default="{ row }"><el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '啟用' : '停用' }}</el-tag></template></el-table-column>
          <el-table-column label="排序值" width="90" prop="sortOrder" />
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="openDialog(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete" size="small" text type="danger" @click="remove(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.code }} {{ row.orgName }}</template>
          <template #meta="{ row }"><el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '啟用' : '停用' }}</el-tag></template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="openDialog(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" @click="remove(row)">刪除</el-button>
          </template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="open" :title="form.id ? '編輯捐贈碼' : '新增捐贈碼'" width="440px" :close-on-click-modal="false" class="codes__dialog">
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="codes__block" @close="formError = null" />
      <el-form label-position="top" :disabled="form.id ? !canUpdate : !canCreate">
        <FormField field="code" label="捐贈碼（3 到 7 位數字）" required><el-input v-model="form.code" maxlength="7" /></FormField>
        <FormField field="orgName" label="受贈團體名稱" required><el-input v-model="form.orgName" maxlength="128" /></FormField>
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><el-form-item label="啟用"><el-switch v-model="form.isActive" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><FormField field="sortOrder" label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></FormField></el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="open = false">關閉</el-button>
        <el-button v-if="form.id ? canUpdate : canCreate" type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.codes__bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 12px; }
.codes__spacer { flex: 1; }
.codes__hint { font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.codes__block { margin-bottom: 12px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
