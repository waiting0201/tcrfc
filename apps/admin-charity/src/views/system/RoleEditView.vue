<script setup lang="ts">
/**
 * 角色新增／編輯：基本資料＋權限勾選。僅系統管理員可進入。
 *
 * 每個勾選的權限都帶一個「範圍」（全部／遮罩顯示／僅可翻譯，慈善庫實際用到的三種）。
 * 「僅系統管理員」的權限不能指派給一般角色（後端會整批拒絕），這裡直接畫成禁用，
 * 不讓使用者勾了才在儲存時被拒。權限分組只顯示中文模組名稱，不顯示代號。
 * 版面依 docs/21 §3.3a 與 docs/22 §3.10：整頁一組語言分頁（角色名稱中英）、主欄一張卡
 * （名稱＋太寬的權限勾選）、右側欄「基本設定」（角色代碼）。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import ErrorState from '@/components/ErrorState.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import {
  createAdminRole,
  getAdminRole,
  listPermissionDictionary,
  replaceRolePermissions,
  updateAdminRole,
  type AdminPermission,
  type RoleScopeType,
} from '@/api/adminRoles'
import { AdminApiError } from '@/api/http'

const route = useRoute()
const router = useRouter()

const isCreate = computed(() => route.name === 'system-role-new')
const roleId = ref<string | undefined>(route.params.id as string | undefined)

/** 子模組代號 → 中文名稱（與側欄同名）。畫面只顯示名稱；對不到的一律歸「其他」，不印代號。 */
const MODULE_NAME: Record<string, string> = {
  N1: '店家與 QR Code',
  N2: '捐款項目管理',
  N3: '捐款紀錄',
  N4: '回饋金結算',
  N5: '發票與收據管理',
  N6: '捐款報表',
  N7: '站台設定',
}

const SCOPE_LABEL: Record<RoleScopeType, string> = {
  all: '全部',
  masked: '遮罩顯示',
  translate_only: '僅可翻譯',
}

const form = reactive({ code: '', nameZh: '', nameEn: '' })
const isSystem = ref(false)
const baselineJson = ref('')
const permissions = ref<AdminPermission[]>([])
/** key＝權限碼，value＝目前選的範圍；有 key 就是已勾選。 */
const selection = reactive(new Map<string, RoleScopeType>())

const moduleGroups = computed(() => {
  const groups = new Map<string, { name: string; items: AdminPermission[] }>()
  for (const p of permissions.value) {
    const name = (p.submoduleCode && MODULE_NAME[p.submoduleCode]) || '其他'
    const group = groups.get(name) ?? { name, items: [] }
    group.items.push(p)
    groups.set(name, group)
  }
  return [...groups.values()]
})

function snapshot(): string {
  return JSON.stringify({ form: { ...form }, selection: Object.fromEntries(selection.entries()) })
}

const formErrors = provideFormErrors()
const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 只放沒有欄位歸屬的錯誤（API 回來的訊息）；前端驗證一律進 formErrors。 */
const formError = ref<string | null>(null)

async function load() {
  loadState.value = 'loading'
  try {
    permissions.value = await listPermissionDictionary()
    if (!isCreate.value && roleId.value) {
      const detail = await getAdminRole(roleId.value)
      form.code = detail.code
      form.nameZh = detail.nameZh
      form.nameEn = detail.nameEn ?? ''
      isSystem.value = detail.isSystem
      selection.clear()
      for (const a of detail.permissions) selection.set(a.permissionCode, a.scopeType)
    }
    baselineJson.value = snapshot()
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.detail : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(load)

const isDirty = computed(() => loadState.value === 'ready' && snapshot() !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增角色' : `編輯角色：${form.nameZh}`))

function toggle(permission: AdminPermission, checked: boolean) {
  if (permission.sysadminOnly) return
  if (checked) selection.set(permission.code, selection.get(permission.code) ?? 'all')
  else selection.delete(permission.code)
}

function setScope(code: string, scope: RoleScopeType) {
  if (selection.has(code)) selection.set(code, scope)
}

/** 一次檢查全部（欄位鍵 → 訊息），不要遇到第一個就停。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  const code = form.code.trim()
  if (isCreate.value) {
    if (!code) errors.code = '請輸入角色代碼'
    else if (!/^[a-z0-9_]+$/.test(code)) errors.code = '角色代碼只能使用小寫英文字母、數字與底線（_）'
  }
  if (!isSystem.value && !form.nameZh.trim()) errors.nameZh = '請輸入角色名稱'
  return errors
}

async function save() {
  if (saving.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    let id = roleId.value
    if (isCreate.value) {
      const created = await createAdminRole({ code: form.code.trim(), nameZh: form.nameZh.trim(), nameEn: form.nameEn.trim() || null })
      id = created.id
      roleId.value = id
    } else if (!isSystem.value) {
      await updateAdminRole(id!, { nameZh: form.nameZh.trim(), nameEn: form.nameEn.trim() || null })
    }
    await replaceRolePermissions(
      id!,
      [...selection.entries()].map(([permissionCode, scopeType]) => ({ permissionCode, scopeType })),
    )
    ElMessage.success('已儲存')
    baselineJson.value = snapshot()
    if (isCreate.value) await router.replace(`/system/roles/${id}/edit`)
  } catch (error) {
    // 後端有標欄位就標到欄位；對不到（或沒有欄位資訊）才退回頁首提示
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div>
    <PageHeader :title="pageTitle" frontend-unit="（無對應前台頁面，後台角色管理）">
      <template #back>
        <el-button text @click="router.push('/system/roles')">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <ErrorState v-else-if="loadState === 'error'" :text="loadErrorMessage" @retry="load" />

    <el-form v-else label-position="top" @submit.prevent>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="role-edit__block" @close="formError = null" />
      <el-alert v-if="isSystem" title="這是系統內建角色，基本資料無法修改，僅能調整權限。" type="info" show-icon :closable="false" class="role-edit__block" />

      <LangTabsBar>
        <EditLayout>
          <template #main>
            <el-card shadow="never">
              <FormSection>
                <BilingualShortField
                  field="name"
                  label="角色名稱"
                  required
                  :maxlength="50"
                  :disabled="isSystem"
                  :zh="form.nameZh"
                  :en="form.nameEn"
                  @update:zh="form.nameZh = $event"
                  @update:en="form.nameEn = $event"
                />
              </FormSection>

              <FormSection title="權限">
                <FormField field="permissions">
                  <div class="role-edit__permissions">
                    <div v-for="group in moduleGroups" :key="group.name" class="role-edit__group">
                      <h4 class="role-edit__group-title">{{ group.name }}</h4>
                      <div v-for="permission in group.items" :key="permission.code" class="role-edit__row">
                        <el-checkbox
                          :model-value="selection.has(permission.code)"
                          :disabled="permission.sysadminOnly"
                          @change="(val: unknown) => toggle(permission, val === true)"
                        >
                          {{ permission.nameZh }}
                          <SemanticTag v-if="permission.sysadminOnly" variant="neutral" class="role-edit__tag">僅系統管理員</SemanticTag>
                        </el-checkbox>
                        <el-select
                          v-if="selection.has(permission.code)"
                          :model-value="selection.get(permission.code)"
                          size="small"
                          class="role-edit__scope"
                          @update:model-value="(v: RoleScopeType) => setScope(permission.code, v)"
                        >
                          <el-option v-for="(label, value) in SCOPE_LABEL" :key="value" :label="label" :value="value" />
                        </el-select>
                      </div>
                    </div>
                  </div>
                </FormField>
              </FormSection>
            </el-card>
          </template>

          <template #aside>
            <el-card shadow="never" header="基本設定">
              <FormSection>
                <FormField field="code" label="角色代碼" required>
                  <el-input v-model="form.code" :disabled="!isCreate" :maxlength="64" placeholder="建立後不可修改" />
                </FormField>
              </FormSection>
            </el-card>
          </template>
        </EditLayout>
      </LangTabsBar>

      <EditActionBar>
        <template #status><FormErrorStatus /></template>
        <el-button @click="router.push('/system/roles')">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
      </EditActionBar>
    </el-form>
  </div>
</template>

<style scoped>
.role-edit__block {
  margin-bottom: var(--charity-admin-space-3);
}

.role-edit__permissions {
  width: 100%;
}

.role-edit__group {
  margin-bottom: var(--charity-admin-space-4);
}

.role-edit__group-title {
  margin: 0 0 var(--charity-admin-space-2);
  font-size: 13px;
  color: var(--charity-admin-text-tertiary);
}

.role-edit__row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--charity-admin-space-2);
  padding: var(--charity-admin-space-1) 0;
}

.role-edit__tag {
  margin-left: var(--charity-admin-space-2);
}

.role-edit__scope {
  width: 140px;
}
</style>
