<script setup lang="ts">
/** 角色與權限列表。僅系統管理員可進入。 */
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { deleteAdminRole, listAdminRoles, type AdminRoleListItem } from '@/api/adminRoles'
import { AdminApiError } from '@/api/http'
import { useBreakpoint } from '@/composables/useBreakpoint'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

const roles = ref<AdminRoleListItem[]>([])
const loading = ref(true)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    roles.value = await listAdminRoles()
  } catch (error) {
    roles.value = []
    loadError.value = error instanceof AdminApiError ? error.detail : '查詢時發生問題，請稍後再試一次'
  } finally {
    loading.value = false
  }
}

onMounted(load)

async function remove(row: AdminRoleListItem) {
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
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '刪除失敗，請稍後再試')
  }
}
</script>

<template>
  <div>
    <PageHeader title="角色與權限" frontend-unit="（無對應前台頁面，後台角色管理）">
      <template #actions>
        <el-button type="primary" @click="router.push('/system/roles/new')">新增角色</el-button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <EmptyState v-else-if="!loading && roles.length === 0" text="目前還沒有任何角色" />

    <template v-else>
      <el-table v-if="!isMobile" v-loading="loading" :data="roles" style="width: 100%">
        <el-table-column label="角色名稱" min-width="180">
          <template #default="{ row }: { row: AdminRoleListItem }">
            {{ row.nameZh }}
            <SemanticTag v-if="row.isSystem" variant="neutral" class="role-list__tag">系統內建</SemanticTag>
          </template>
        </el-table-column>
        <el-table-column label="指派帳號數" width="120" prop="assignedAccountCount" />
        <el-table-column label="操作" width="140" fixed="right">
          <template #default="{ row }: { row: AdminRoleListItem }">
            <el-button size="small" text type="primary" @click="router.push(`/system/roles/${row.id}/edit`)">編輯</el-button>
            <el-button size="small" text type="danger" @click="remove(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <MobileCardList v-else :items="roles">
        <template #default="{ item }: { item: AdminRoleListItem }">
          <div class="role-list__card-head">
            <span class="role-list__card-name">{{ item.nameZh }}</span>
            <SemanticTag v-if="item.isSystem" variant="neutral">系統內建</SemanticTag>
          </div>
          <p class="role-list__card-meta">指派帳號 {{ item.assignedAccountCount }} 個</p>
          <div class="role-list__card-actions">
            <el-button size="small" text type="primary" @click="router.push(`/system/roles/${item.id}/edit`)">編輯</el-button>
            <el-button size="small" text type="danger" @click="remove(item)">刪除</el-button>
          </div>
        </template>
      </MobileCardList>
    </template>
  </div>
</template>

<style scoped>
.role-list__tag {
  margin-left: var(--charity-admin-space-2);
}

.role-list__card-head {
  display: flex;
  justify-content: space-between;
  gap: var(--charity-admin-space-2);
}

.role-list__card-name {
  font-weight: 600;
}

.role-list__card-meta {
  margin: var(--charity-admin-space-2) 0;
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.role-list__card-actions {
  display: flex;
  gap: var(--charity-admin-space-1);
}
</style>
