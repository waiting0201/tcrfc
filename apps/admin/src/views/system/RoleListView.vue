<script setup lang="ts">
/** J2 角色與權限列表頁。僅系統管理員可進入。 */
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { deleteAdminRole, listAdminRoles, type AdminRoleListItemDto } from '@/api/adminRoles'
import { AdminApiError } from '@/api/http'

const router = useRouter()

const roles = ref<AdminRoleListItemDto[]>([])
const loading = ref(true)
const listError = ref<string | null>(null)

async function fetchList() {
  loading.value = true
  listError.value = null
  try {
    roles.value = await listAdminRoles()
  } catch (error) {
    roles.value = []
    listError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(fetchList)

function handleAdd() {
  router.push('/system/roles/new')
}

function handleEdit(row: AdminRoleListItemDto) {
  router.push(`/system/roles/${row.id}/edit`)
}

async function handleDelete(row: AdminRoleListItemDto) {
  if (row.isSystem) {
    ElMessage.warning('系統內建角色不能刪除')
    return
  }
  if (row.assignedAccountCount > 0) {
    ElMessage.warning(`還有 ${row.assignedAccountCount} 個帳號指派這個角色，無法刪除`)
    return
  }
  try {
    await ElMessageBox.confirm(`確定要刪除角色「${row.nameZh}」嗎？`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdminRole(row.id)
    ElMessage.success('已刪除')
    await fetchList()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

const isEmpty = computed(() => !loading.value && !listError.value && roles.value.length === 0)
</script>

<template>
  <div class="role-list">
    <PageHeader title="角色與權限">
      <template #meta>
        <FrontendUnitBanner module-code="J2" />
      </template>
    </PageHeader>

    <div class="role-list__toolbar">
      <el-button type="primary" @click="handleAdd">+ 新增角色</el-button>
    </div>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>

    <el-card v-else-if="listError" shadow="never">
      <el-empty :image-size="96" :description="listError">
        <el-button type="primary" @click="fetchList">重新載入</el-button>
      </el-empty>
    </el-card>

    <el-card v-else-if="isEmpty" shadow="never">
      <el-empty description="目前還沒有任何角色">
        <el-button type="primary" @click="handleAdd">+ 新增第一個角色</el-button>
      </el-empty>
    </el-card>

    <el-card v-else shadow="never">
      <el-table :data="roles" row-key="id">
        <el-table-column label="角色名稱" min-width="160">
          <template #default="{ row }">
            {{ row.nameZh }}
            <el-tag v-if="row.isSystem" type="info" size="small" class="role-list__inline-tag">系統內建</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="資料範圍" width="140">
          <template #default="{ row }">
            {{ row.scopeMode === 'all_clubs' ? '全部俱樂部' : '授權的俱樂部' }}
          </template>
        </el-table-column>
        <el-table-column label="指派帳號數" width="120">
          <template #default="{ row }">{{ row.assignedAccountCount }}</template>
        </el-table-column>
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
            <el-button size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.role-list__toolbar {
  margin-bottom: 12px;
  display: flex;
  justify-content: flex-end;
}

.role-list__inline-tag {
  margin-left: 6px;
}
</style>
