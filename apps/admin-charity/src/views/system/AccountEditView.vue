<script setup lang="ts">
/** 帳號新增／編輯。僅系統管理員可進入。慈善後台沒有俱樂部維度，所以只有基本資料與角色。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import ErrorState from '@/components/ErrorState.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import {
  createAdminAccount,
  getAdminAccount,
  resetAdminAccountPassword,
  resetAdminAccountTwoFactor,
  setAdminAccountStatus,
  updateAdminAccount,
} from '@/api/adminAccounts'
import { listAdminRoles, type AdminRoleListItem } from '@/api/adminRoles'
import { AdminApiError } from '@/api/http'

const route = useRoute()
const router = useRouter()

const isCreate = computed(() => route.name === 'system-account-new')
const accountId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  username: '',
  displayName: '',
  email: '',
  isSuperAdmin: false,
  roleCodes: [] as string[],
  initialPassword: '',
})
const baselineJson = ref(JSON.stringify(form))
const accountStatus = ref<'active' | 'disabled'>('active')
const roles = ref<AdminRoleListItem[]>([])

const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function load() {
  loadState.value = 'loading'
  try {
    roles.value = await listAdminRoles()
    if (!isCreate.value && accountId.value) {
      const detail = await getAdminAccount(accountId.value)
      form.username = detail.username
      form.displayName = detail.displayName
      form.email = detail.email ?? ''
      form.isSuperAdmin = detail.isSuperAdmin
      form.roleCodes = [...detail.roleCodes]
      accountStatus.value = detail.status
    }
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.detail : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(load)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增帳號' : `編輯帳號：${form.username}`))

function validate(): boolean {
  formError.value = null
  if (isCreate.value && !form.username.trim()) {
    formError.value = '請輸入帳號'
    return false
  }
  if (!form.displayName.trim()) {
    formError.value = '請輸入姓名'
    return false
  }
  if (isCreate.value && form.initialPassword.length < 9) {
    formError.value = '初始密碼長度至少需要 9 個字元'
    return false
  }
  return true
}

async function save() {
  if (!validate()) return
  saving.value = true
  try {
    if (isCreate.value) {
      const created = await createAdminAccount({
        username: form.username.trim(),
        displayName: form.displayName.trim(),
        email: form.email.trim() || null,
        initialPassword: form.initialPassword,
        isSuperAdmin: form.isSuperAdmin,
        roleCodes: form.roleCodes,
      })
      ElMessage.success('已建立帳號，請透過站外管道把初始密碼轉交給使用者')
      form.initialPassword = ''
      baselineJson.value = JSON.stringify(form)
      await router.replace(`/system/accounts/${created.id}/edit`)
      accountId.value = created.id
      await load()
    } else {
      await updateAdminAccount(accountId.value!, {
        displayName: form.displayName.trim(),
        email: form.email.trim() || null,
        isSuperAdmin: form.isSuperAdmin,
        roleCodes: form.roleCodes,
      })
      ElMessage.success('已儲存')
      baselineJson.value = JSON.stringify(form)
    }
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function toggleStatus() {
  const next = accountStatus.value === 'active' ? 'disabled' : 'active'
  const label = next === 'disabled' ? '停用' : '啟用'
  try {
    await ElMessageBox.confirm(`確定要${label}這個帳號嗎？`, `確認${label}`, {
      confirmButtonText: label,
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await setAdminAccountStatus(accountId.value!, next)
    accountStatus.value = next
    ElMessage.success(`已${label}`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : `${label}失敗，請稍後再試`)
  }
}

async function resetPassword() {
  let newPassword = ''
  try {
    const result = await ElMessageBox.prompt('請輸入新密碼（至少 9 個字元），設定後請透過站外管道轉交給使用者。', '重設密碼', {
      confirmButtonText: '重設',
      cancelButtonText: '取消',
      inputType: 'password',
      inputValidator: (value: string) => (value && value.length >= 9) || '密碼長度至少需要 9 個字元',
    })
    newPassword = result.value
  } catch {
    return
  }
  try {
    await resetAdminAccountPassword(accountId.value!, newPassword)
    ElMessage.success('密碼已重設，該帳號目前所有登入工作階段已被強制登出')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '重設失敗，請稍後再試')
  }
}

async function resetTotp() {
  try {
    await ElMessageBox.confirm('確定要重設這個帳號的兩階段驗證設定嗎？重設後下次登入需要重新設定。', '確認重設', {
      confirmButtonText: '重設',
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await resetAdminAccountTwoFactor(accountId.value!)
    ElMessage.success('已重設兩階段驗證設定')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '重設失敗，請稍後再試')
  }
}
</script>

<template>
  <div>
    <PageHeader :title="pageTitle" frontend-unit="（無對應前台頁面，後台帳號管理）">
      <template #back>
        <el-button text @click="router.push('/system/accounts')">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <ErrorState v-else-if="loadState === 'error'" :text="loadErrorMessage" @retry="load" />

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="account-edit__error" @close="formError = null" />

      <el-card shadow="never" header="基本資料">
        <el-form label-position="top" class="account-edit__form">
          <el-form-item label="帳號" required>
            <el-input v-model="form.username" :disabled="!isCreate" placeholder="登入用帳號，可用中文，不含空白，最多 64 字；建立後不可修改" />
          </el-form-item>
          <el-form-item label="姓名" required>
            <el-input v-model="form.displayName" />
          </el-form-item>
          <el-form-item label="電子郵件">
            <el-input v-model="form.email" placeholder="選填" />
          </el-form-item>
          <el-form-item v-if="isCreate" label="初始密碼" required>
            <el-input v-model="form.initialPassword" type="password" show-password placeholder="至少 9 個字元，建立後請透過站外管道轉交" />
          </el-form-item>
          <el-form-item label="系統管理員">
            <el-switch v-model="form.isSuperAdmin" />
            <span class="account-edit__hint">系統管理員可使用全部功能，不受角色權限限制</span>
          </el-form-item>
          <el-form-item label="角色">
            <el-select v-model="form.roleCodes" multiple placeholder="請選擇角色" style="width: 100%">
              <el-option v-for="role in roles" :key="role.code" :label="role.nameZh" :value="role.code" />
            </el-select>
          </el-form-item>
        </el-form>
        <div class="account-edit__actions">
          <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
          <template v-if="!isCreate">
            <el-button @click="toggleStatus">{{ accountStatus === 'active' ? '停用帳號' : '啟用帳號' }}</el-button>
            <el-button @click="resetPassword">重設密碼</el-button>
            <el-button @click="resetTotp">重設兩階段驗證</el-button>
          </template>
        </div>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.account-edit__error {
  margin-bottom: var(--charity-admin-space-3);
}

.account-edit__form {
  max-width: 560px;
}

.account-edit__hint {
  margin-left: var(--charity-admin-space-2);
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.account-edit__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  margin-top: var(--charity-admin-space-4);
}
</style>
