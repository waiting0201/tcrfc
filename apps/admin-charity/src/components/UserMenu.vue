<script setup lang="ts">
/** 使用者選單：顯示登入者姓名與角色、登出。 */
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { authUser } from '@/auth/session'
import { logout } from '@/api/auth'

const router = useRouter()
const displayName = computed(() => authUser.value?.displayName || '使用者')
const roleName = computed(() => (authUser.value?.roleNames.length ? authUser.value.roleNames.join('、') : authUser.value?.isSuperAdmin ? '系統管理員' : ''))

async function handleLogout() {
  await logout()
  await router.replace({ name: 'login' })
}
</script>

<template>
  <el-dropdown trigger="click" @command="handleLogout">
    <button type="button" class="user-menu">
      <el-avatar :size="28" class="user-menu__avatar">{{ displayName.slice(0, 1) }}</el-avatar>
      <span class="user-menu__name admin-hide-on-mobile">{{ displayName }}</span>
      <el-icon class="admin-hide-on-mobile"><ArrowDown /></el-icon>
    </button>
    <template #dropdown>
      <el-dropdown-menu>
        <div class="user-menu__current">
          <p class="user-menu__current-name">{{ displayName }}</p>
          <p v-if="roleName" class="user-menu__current-role">{{ roleName }}</p>
        </div>
        <el-dropdown-item divided command="logout">登出</el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>

<style scoped>
.user-menu {
  display: flex;
  align-items: center;
  gap: var(--charity-admin-space-2);
  border: none;
  background: none;
  cursor: pointer;
  padding: var(--charity-admin-space-1) var(--charity-admin-space-2);
  border-radius: 4px;
  color: var(--charity-admin-text-primary);
}

.user-menu:hover {
  background: var(--charity-admin-bg-surface-2);
}

.user-menu__avatar {
  background: var(--charity-admin-primary);
  color: #ffffff;
  font-size: 13px;
}

.user-menu__name {
  font-size: 14px;
  max-width: 96px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-menu__current {
  padding: var(--charity-admin-space-2) var(--charity-admin-space-4);
}

.user-menu__current-name {
  margin: 0;
  font-weight: 600;
  color: var(--charity-admin-text-primary);
}

.user-menu__current-role {
  margin: 2px 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}
</style>
