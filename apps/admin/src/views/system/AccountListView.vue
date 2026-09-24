<script setup lang="ts">
/**
 * J1 帳號管理列表頁。僅系統管理員可進入（`router/index.ts` 的 `sysadminOnly` 守衛 ＋
 * 後端 `system.account.*` 皆為 `sysadmin_only`，見 apps/api/README.md）。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  listAdminAccounts,
  resetAdminAccountPassword,
  resetAdminAccountTwoFactor,
  setAdminAccountStatus,
  type AdminAccountListItemDto,
} from '@/api/adminAccounts'
import { AdminApiError } from '@/api/http'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()

const filters = reactive({ keyword: '', status: '' as 'active' | 'disabled' | '' })
const currentPage = ref(1)
const pageSize = ref(20)

const accounts = ref<AdminAccountListItemDto[]>([])
const totalCount = ref(0)
const loading = ref(true)
const listError = ref<string | null>(null)

async function fetchList() {
  loading.value = true
  listError.value = null
  try {
    const page = await listAdminAccounts({
      status: filters.status,
      keyword: filters.keyword || undefined,
      page: currentPage.value,
      pageSize: pageSize.value,
    })
    accounts.value = page.items
    totalCount.value = page.totalCount
  } catch (error) {
    accounts.value = []
    totalCount.value = 0
    listError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(fetchList)

function applyFilters() {
  currentPage.value = 1
  fetchList()
}

function clearFilters() {
  filters.keyword = ''
  filters.status = ''
  applyFilters()
}

function handleAdd() {
  router.push('/system/accounts/new')
}

function handleEdit(row: AdminAccountListItemDto) {
  router.push(`/system/accounts/${row.id}/edit`)
}

async function toggleStatus(row: AdminAccountListItemDto) {
  const next = row.status === 'active' ? 'disabled' : 'active'
  const actionLabel = next === 'disabled' ? '停用' : '啟用'
  try {
    await ElMessageBox.confirm(`確定要${actionLabel}帳號「${row.username}」嗎？`, `確認${actionLabel}`, {
      confirmButtonText: actionLabel,
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await setAdminAccountStatus(row.id, next)
    ElMessage.success(`已${actionLabel}`)
    await fetchList()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : `${actionLabel}失敗，請稍後再試`)
  }
}

async function handleResetPassword(row: AdminAccountListItemDto) {
  let newPassword = ''
  try {
    const result = await ElMessageBox.prompt('請輸入新密碼（至少 10 個字元），設定後請透過站外管道轉交給使用者。', '重設密碼', {
      confirmButtonText: '重設',
      cancelButtonText: '取消',
      inputType: 'password',
      inputValidator: (value: string) => (value && value.length >= 10) || '密碼長度至少需要 10 個字元',
    })
    newPassword = result.value
  } catch {
    return
  }
  try {
    await resetAdminAccountPassword(row.id, newPassword)
    ElMessage.success('密碼已重設，該帳號目前所有登入工作階段已被強制登出，下次登入需要重新設定密碼')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '重設失敗，請稍後再試')
  }
}

async function handleResetTotp(row: AdminAccountListItemDto) {
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
    await fetchList()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '重設失敗，請稍後再試')
  }
}

const isEmpty = computed(() => !loading.value && !listError.value && accounts.value.length === 0)
</script>

<template>
  <div class="account-list">
    <PageHeader title="帳號">
      <template #meta>
        <FrontendUnitBanner module-code="J1" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="account-list__filters">
      <div class="account-list__filter-row">
        <el-input
          v-model="filters.keyword"
          placeholder="搜尋帳號或姓名"
          clearable
          class="account-list__filter-keyword"
          @keyup.enter="applyFilters"
          @clear="applyFilters"
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="account-list__filter-select">
          <el-option label="啟用中" value="active" />
          <el-option label="已停用" value="disabled" />
        </el-select>
        <el-button type="primary" @click="applyFilters">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
        <div class="account-list__spacer" />
        <el-button type="primary" @click="handleAdd">+ 新增帳號</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>

    <el-card v-else-if="listError" shadow="never">
      <el-empty :image-size="96" :description="listError">
        <el-button type="primary" @click="fetchList">重新載入</el-button>
      </el-empty>
    </el-card>

    <el-card v-else-if="isEmpty" shadow="never">
      <el-empty description="目前還沒有任何帳號">
        <el-button type="primary" @click="handleAdd">+ 新增第一個帳號</el-button>
      </el-empty>
    </el-card>

    <el-card v-else shadow="never">
      <el-table :data="accounts" row-key="id">
        <el-table-column label="帳號" min-width="140">
          <template #default="{ row }">{{ row.username }}</template>
        </el-table-column>
        <el-table-column label="姓名" min-width="120">
          <template #default="{ row }">{{ row.displayName }}</template>
        </el-table-column>
        <el-table-column label="角色" min-width="160">
          <template #default="{ row }">
            <el-tag v-if="row.isSuperAdmin" type="danger" size="small">系統管理員</el-tag>
            <el-tag v-for="code in row.roleCodes" :key="code" size="small" class="account-list__inline-tag">
              {{ code }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="狀態" width="100">
          <template #default="{ row }">
            <el-tag :type="row.status === 'active' ? 'success' : 'info'" size="small">
              {{ row.status === 'active' ? '啟用中' : '已停用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="安全設定" width="160">
          <template #default="{ row }">
            <el-tag v-if="row.mustChangePassword" type="warning" size="small" class="account-list__inline-tag">
              待改密
            </el-tag>
            <el-tag v-if="!row.twoFactorEnabled" type="warning" size="small" class="account-list__inline-tag">
              未啟用兩階段驗證
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="最後登入" width="160">
          <template #default="{ row }">{{ row.lastLoginAt ? formatDateTime(row.lastLoginAt) : '尚未登入' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
            <el-dropdown trigger="click">
              <el-button size="small" text>
                更多<el-icon class="el-icon--right"><ArrowDown /></el-icon>
              </el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item @click="toggleStatus(row)">
                    {{ row.status === 'active' ? '停用帳號' : '啟用帳號' }}
                  </el-dropdown-item>
                  <el-dropdown-item @click="handleResetPassword(row)">重設密碼</el-dropdown-item>
                  <el-dropdown-item @click="handleResetTotp(row)">重設兩階段驗證</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </template>
        </el-table-column>
      </el-table>

      <div class="account-list__pagination">
        <el-pagination
          v-model:current-page="currentPage"
          v-model:page-size="pageSize"
          :total="totalCount"
          :page-sizes="[10, 20, 50, 100]"
          layout="total, sizes, prev, pager, next"
          @current-change="fetchList"
          @size-change="fetchList"
        />
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.account-list__filters {
  margin-bottom: 12px;
}

.account-list__filter-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.account-list__filter-keyword {
  width: 220px;
  max-width: 100%;
}

.account-list__filter-select {
  width: 140px;
  max-width: 100%;
}

.account-list__spacer {
  flex: 1;
}

.account-list__inline-tag {
  margin-left: 4px;
}

.account-list__pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
