<script setup lang="ts">
/** 帳號列表。僅系統管理員可進入（路由 `sysadminOnly` 守衛＋後端端點皆限系統管理員）。 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import {
  listAdminAccounts,
  resetAdminAccountPassword,
  resetAdminAccountTwoFactor,
  setAdminAccountStatus,
  type AdminAccountListItem,
} from '@/api/adminAccounts'
import { listAdminRoles } from '@/api/adminRoles'
import { AdminApiError } from '@/api/http'
import { formatTaipei } from '@/utils/format'
import { useBreakpoint } from '@/composables/useBreakpoint'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

const filters = reactive({ keyword: '', status: '' as 'active' | 'disabled' | '' })
const page = ref(1)
const pageSize = 20
const items = ref<AdminAccountListItem[]>([])
const totalCount = ref(0)
const loading = ref(true)
const loadError = ref('')
/** 角色識別名稱 → 中文名稱；列表只顯示中文名稱，不顯示識別名稱。 */
const roleNames = ref<Record<string, string>>({})

function roleLabel(code: string): string {
  return roleNames.value[code] ?? '未命名角色'
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const result = await listAdminAccounts({
      status: filters.status,
      keyword: filters.keyword.trim() || undefined,
      page: page.value,
      pageSize,
    })
    items.value = result.items
    totalCount.value = result.totalCount
  } catch (error) {
    items.value = []
    totalCount.value = 0
    loadError.value = error instanceof AdminApiError ? error.detail : '查詢時發生問題，請稍後再試一次'
  } finally {
    loading.value = false
  }
}

async function loadRoleNames() {
  try {
    const roles = await listAdminRoles()
    roleNames.value = Object.fromEntries(roles.map((r) => [r.code, r.nameZh]))
  } catch {
    // 角色名稱讀不到時，角色欄退回「未命名角色」，不擋住列表。
  }
}

function search() {
  page.value = 1
  void load()
}

watch(page, () => { void load() })
onMounted(() => {
  void load()
  void loadRoleNames()
})

async function toggleStatus(row: AdminAccountListItem) {
  const next = row.status === 'active' ? 'disabled' : 'active'
  const label = next === 'disabled' ? '停用' : '啟用'
  try {
    await ElMessageBox.confirm(`確定要${label}帳號「${row.username}」嗎？`, `確認${label}`, {
      confirmButtonText: label,
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await setAdminAccountStatus(row.id, next)
    ElMessage.success(`已${label}`)
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : `${label}失敗，請稍後再試`)
  }
}

async function resetPassword(row: AdminAccountListItem) {
  let newPassword = ''
  try {
    const result = await ElMessageBox.prompt('請輸入新密碼（至少 6 個字元），設定後請透過站外管道轉交給使用者。', '重設密碼', {
      confirmButtonText: '重設',
      cancelButtonText: '取消',
      inputType: 'password',
      inputValidator: (value: string) => (value && value.length >= 6) || '密碼長度至少需要 6 個字元',
    })
    newPassword = result.value
  } catch {
    return
  }
  try {
    await resetAdminAccountPassword(row.id, newPassword)
    ElMessage.success('密碼已重設，該帳號目前所有登入工作階段已被強制登出，下次登入需要重新設定密碼')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '重設失敗，請稍後再試')
  }
}

async function resetTotp(row: AdminAccountListItem) {
  try {
    await ElMessageBox.confirm(
      `確定要重設帳號「${row.username}」的兩階段驗證設定嗎？重設後，該帳號下次登入時需要重新設定兩階段驗證。`,
      '確認重設',
      { confirmButtonText: '重設', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await resetAdminAccountTwoFactor(row.id)
    ElMessage.success('已重設兩階段驗證設定')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '重設失敗，請稍後再試')
  }
}

function onCommand(command: string, row: AdminAccountListItem) {
  if (command === 'status') void toggleStatus(row)
  else if (command === 'password') void resetPassword(row)
  else if (command === 'totp') void resetTotp(row)
}
</script>

<template>
  <div>
    <PageHeader title="帳號" frontend-unit="（無對應前台頁面，後台帳號管理）">
      <template #actions>
        <el-button type="primary" @click="router.push('/system/accounts/new')">新增帳號</el-button>
      </template>
    </PageHeader>

    <div class="account-list__filters">
      <el-input v-model="filters.keyword" placeholder="搜尋帳號或姓名" clearable style="max-width: 240px" @keyup.enter="search" @clear="search" />
      <el-select v-model="filters.status" placeholder="狀態" clearable style="width: 140px" @change="search">
        <el-option value="active" label="啟用中" />
        <el-option value="disabled" label="已停用" />
      </el-select>
      <el-button @click="search">查詢</el-button>
    </div>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <EmptyState v-else-if="!loading && items.length === 0" text="目前沒有符合條件的帳號" />

    <template v-else>
      <el-table v-if="!isMobile" v-loading="loading" :data="items" style="width: 100%">
        <el-table-column label="帳號" min-width="140" prop="username" />
        <el-table-column label="姓名" min-width="120" prop="displayName" />
        <el-table-column label="角色" min-width="180">
          <template #default="{ row }: { row: AdminAccountListItem }">
            <SemanticTag v-if="row.isSuperAdmin" variant="danger">系統管理員</SemanticTag>
            <SemanticTag v-for="code in row.roleCodes" :key="code" variant="neutral" class="account-list__tag">{{ roleLabel(code) }}</SemanticTag>
          </template>
        </el-table-column>
        <el-table-column label="狀態" width="100">
          <template #default="{ row }: { row: AdminAccountListItem }">
            <SemanticTag :variant="row.status === 'active' ? 'success' : 'neutral'">{{ row.status === 'active' ? '啟用中' : '已停用' }}</SemanticTag>
            <SemanticTag v-if="row.mustChangePassword" variant="warning" class="account-list__tag">待改密碼</SemanticTag>
          </template>
        </el-table-column>
        <el-table-column label="最後登入" width="170">
          <template #default="{ row }: { row: AdminAccountListItem }">{{ row.lastLoginAt ? formatTaipei(row.lastLoginAt) : '尚未登入' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }: { row: AdminAccountListItem }">
            <el-button size="small" text type="primary" @click="router.push(`/system/accounts/${row.id}/edit`)">編輯</el-button>
            <el-dropdown trigger="click" @command="(c: string) => onCommand(c, row)">
              <el-button size="small" text>更多<el-icon class="el-icon--right"><ArrowDown /></el-icon></el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="status">{{ row.status === 'active' ? '停用帳號' : '啟用帳號' }}</el-dropdown-item>
                  <el-dropdown-item command="password">重設密碼</el-dropdown-item>
                  <el-dropdown-item command="totp">重設兩階段驗證</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </template>
        </el-table-column>
      </el-table>

      <MobileCardList v-else :items="items">
        <template #default="{ item }: { item: AdminAccountListItem }">
          <div class="account-list__card-head">
            <div>
              <div class="account-list__card-name">{{ item.displayName }}</div>
              <div class="account-list__sub">{{ item.username }}</div>
            </div>
            <SemanticTag :variant="item.status === 'active' ? 'success' : 'neutral'">{{ item.status === 'active' ? '啟用中' : '已停用' }}</SemanticTag>
          </div>
          <p class="account-list__card-meta">
            {{ item.isSuperAdmin ? '系統管理員' : (item.roleCodes.map(roleLabel).join('、') || '尚未指派角色') }}・{{ item.lastLoginAt ? formatTaipei(item.lastLoginAt) : '尚未登入' }}
          </p>
          <div class="account-list__card-actions">
            <el-button size="small" text type="primary" @click="router.push(`/system/accounts/${item.id}/edit`)">編輯</el-button>
            <el-button size="small" text @click="toggleStatus(item)">{{ item.status === 'active' ? '停用帳號' : '啟用帳號' }}</el-button>
            <el-button size="small" text @click="resetPassword(item)">重設密碼</el-button>
            <el-button size="small" text @click="resetTotp(item)">重設兩階段驗證</el-button>
          </div>
        </template>
      </MobileCardList>

      <el-pagination
        v-if="totalCount > pageSize"
        v-model:current-page="page"
        class="account-list__pager"
        layout="prev, pager, next"
        :page-size="pageSize"
        :total="totalCount"
      />
    </template>
  </div>
</template>

<style scoped>
.account-list__filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  margin-bottom: var(--charity-admin-space-4);
}

.account-list__tag {
  margin-left: var(--charity-admin-space-1);
}

.account-list__sub {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.account-list__pager {
  margin-top: var(--charity-admin-space-4);
  justify-content: center;
}

.account-list__card-head {
  display: flex;
  justify-content: space-between;
  gap: var(--charity-admin-space-2);
}

.account-list__card-name {
  font-weight: 600;
}

.account-list__card-meta {
  margin: var(--charity-admin-space-2) 0;
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.account-list__card-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-1);
}
</style>
