<script setup lang="ts">
/**
 * J2 角色編輯頁：基本資料＋權限勾選矩陣。
 *
 * `role_permissions.scope_type`（`docs/12b-database-tables.md` §7.4）是比「有沒有這個權限碼」
 * 更細一層的列級限制，每一筆權限指派都要帶一個範圍值，這裡對每個勾選的權限碼提供一個小選單
 * （預設「全部」），對應不到特定情境時維持預設即可。`sysadmin_only` 的權限碼（例如 `system.*`）
 * 一律不能指派給一般角色（後端會直接拒絕整批請求，見 apps/api/README.md「執行層判斷」第 7 點），
 * 這裡直接把它們畫成禁用狀態，不讓使用者勾選後才在儲存時才發現被拒。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import {
  createAdminRole,
  getAdminRole,
  listPermissionDictionary,
  replaceRolePermissions,
  updateAdminRole,
  type AdminPermissionDto,
  type RoleScopeType,
} from '@/api/adminRoles'
import { AdminApiError } from '@/api/http'

const route = useRoute()
const router = useRouter()

const isCreate = computed(() => route.name === 'system-role-new')
const roleId = ref<string | undefined>(route.params.id as string | undefined)

/** 模組代號 → 中文模組名稱（docs/03-admin-spec.md §1 模組總覽），畫面只顯示這裡的名稱，
 * 不直接印出 `moduleCode`（後台介面一律日常中文，不得出現模組代號）。 */
const MODULE_NAME_LABEL: Record<string, string> = {
  A: '儀表板',
  B: '內容管理',
  C: '球隊管理',
  P: '課程與活動',
  E: '商業模組',
  F: '文化模組',
  G: '表單與詢問',
  H: '搜尋與 AI 能見度',
  I: '網站設定',
  J: '系統管理',
  K: '會員管理',
  L: '行事曆管理',
  M: '行動 App',
  S: '商店',
}

function moduleName(code: string): string {
  return MODULE_NAME_LABEL[code] ?? code
}

const SCOPE_TYPE_LABEL: Record<RoleScopeType, string> = {
  all: '全部',
  own_teams: '僅自己的球隊',
  academy_only: '僅學院梯隊',
  masked: '遮罩顯示',
  translate_only: '僅可翻譯',
  own_clubs: '僅自己的俱樂部',
}

const form = reactive({ code: '', nameZh: '', nameEn: '', scopeMode: 'own_clubs' as 'all_clubs' | 'own_clubs' })
const isSystem = ref(false)
const baselineJson = ref('')

const permissions = ref<AdminPermissionDto[]>([])
/** key＝權限碼，value＝這個角色是否勾選＋目前選的範圍。用 Map 存放，UI 用 computed 攤平成陣列。 */
const selection = reactive(new Map<string, RoleScopeType>())

const moduleGroups = computed(() => {
  const groups = new Map<string, AdminPermissionDto[]>()
  for (const p of permissions.value) {
    const list = groups.get(p.moduleCode) ?? []
    list.push(p)
    groups.set(p.moduleCode, list)
  }
  return Array.from(groups.entries()).map(([moduleCode, items]) => ({ moduleCode, items }))
})

function selectionSnapshot(): Record<string, RoleScopeType> {
  return Object.fromEntries(selection.entries())
}

const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadData() {
  loadState.value = 'loading'
  try {
    permissions.value = await listPermissionDictionary()
    if (!isCreate.value && roleId.value) {
      const detail = await getAdminRole(roleId.value)
      form.code = detail.code
      form.nameZh = detail.nameZh
      form.nameEn = detail.nameEn ?? ''
      form.scopeMode = detail.scopeMode
      isSystem.value = detail.isSystem
      selection.clear()
      for (const assignment of detail.permissions) {
        selection.set(assignment.permissionCode, assignment.scopeType)
      }
    }
    baselineJson.value = JSON.stringify({ form: { ...form }, selection: selectionSnapshot() })
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(loadData)

const isDirty = computed(
  () => loadState.value === 'ready'
    && JSON.stringify({ form: { ...form }, selection: selectionSnapshot() }) !== baselineJson.value,
)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增角色' : `編輯角色：${form.nameZh}`))

function isChecked(code: string): boolean {
  return selection.has(code)
}

function toggle(permission: AdminPermissionDto, checked: boolean) {
  if (permission.sysadminOnly) return
  if (checked) selection.set(permission.code, selection.get(permission.code) ?? 'all')
  else selection.delete(permission.code)
}

function setScope(code: string, scope: RoleScopeType) {
  if (selection.has(code)) selection.set(code, scope)
}

function validate(): boolean {
  formError.value = null
  if (isCreate.value && !form.code.trim()) {
    formError.value = '請輸入角色代碼'
    return false
  }
  if (!form.nameZh.trim()) {
    formError.value = '請輸入角色名稱'
    return false
  }
  return true
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  try {
    let id = roleId.value
    if (isCreate.value) {
      const created = await createAdminRole({
        code: form.code.trim(),
        nameZh: form.nameZh.trim(),
        nameEn: form.nameEn || null,
        scopeMode: form.scopeMode,
      })
      id = created.id
      roleId.value = id
    } else {
      await updateAdminRole(id!, {
        nameZh: form.nameZh.trim(),
        nameEn: form.nameEn || null,
        scopeMode: form.scopeMode,
      })
    }

    const permissionInputs = Array.from(selection.entries()).map(([permissionCode, scopeType]) => ({
      permissionCode,
      scopeType,
    }))
    await replaceRolePermissions(id!, permissionInputs)

    ElMessage.success('已儲存')
    if (isCreate.value) {
      router.replace(`/system/roles/${id}/edit`)
    }
    baselineJson.value = JSON.stringify({ form: { ...form }, selection: selectionSnapshot() })
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/system/roles')
}
</script>

<template>
  <div class="role-edit">
    <PageHeader :title="pageTitle">
      <template #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="J2" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>

    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage">
        <el-button type="primary" @click="loadData">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="role-edit__form-error"
        @close="formError = null"
      />

      <el-card shadow="never" header="基本資料" class="role-edit__section">
        <el-form label-position="top" :disabled="isSystem">
          <el-form-item label="角色代碼" required>
            <el-input v-model="form.code" :disabled="!isCreate" placeholder="建立後不可修改" />
          </el-form-item>
          <el-form-item label="角色名稱（中文）" required>
            <el-input v-model="form.nameZh" />
          </el-form-item>
          <el-form-item label="角色名稱（英文）">
            <el-input v-model="form.nameEn" placeholder="選填" />
          </el-form-item>
          <el-form-item label="資料範圍">
            <el-radio-group v-model="form.scopeMode">
              <el-radio value="all_clubs">全部俱樂部</el-radio>
              <el-radio value="own_clubs">僅授權的俱樂部</el-radio>
            </el-radio-group>
          </el-form-item>
        </el-form>
        <el-alert v-if="isSystem" title="這是系統內建角色，基本資料無法修改，僅能調整權限。" type="info" show-icon :closable="false" />
      </el-card>

      <el-card shadow="never" header="權限" class="role-edit__section">
        <div v-for="group in moduleGroups" :key="group.moduleCode" class="role-edit__module-group">
          <h3 class="role-edit__module-title">{{ moduleName(group.moduleCode) }}</h3>
          <div v-for="permission in group.items" :key="permission.code" class="role-edit__permission-row">
            <el-checkbox
              :model-value="isChecked(permission.code)"
              :disabled="permission.sysadminOnly"
              @change="(val: boolean) => toggle(permission, val)"
            >
              {{ permission.nameZh }}
              <el-tag v-if="permission.sysadminOnly" type="info" size="small" class="role-edit__inline-tag">
                僅系統管理員
              </el-tag>
            </el-checkbox>
            <el-select
              v-if="isChecked(permission.code)"
              :model-value="selection.get(permission.code)"
              size="small"
              class="role-edit__scope-select"
              @update:model-value="(v: RoleScopeType) => setScope(permission.code, v)"
            >
              <el-option v-for="(label, value) in SCOPE_TYPE_LABEL" :key="value" :label="label" :value="value" />
            </el-select>
          </div>
        </div>
      </el-card>

      <div class="role-edit__actions">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.role-edit__section {
  margin-bottom: 12px;
}

.role-edit__form-error {
  margin-bottom: 12px;
}

.role-edit__module-group {
  margin-bottom: 16px;
}

.role-edit__module-title {
  font-size: 13px;
  color: var(--admin-text-tertiary);
  margin: 0 0 8px;
}

.role-edit__permission-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 0;
  flex-wrap: wrap;
}

.role-edit__inline-tag {
  margin-left: 6px;
}

.role-edit__scope-select {
  width: 160px;
}

.role-edit__actions {
  margin-top: 16px;
}
</style>
