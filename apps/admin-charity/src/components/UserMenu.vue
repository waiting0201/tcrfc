<script setup lang="ts">
/**
 * 使用者選單。⛔ 沒有真的認證（見 src/data/session.ts 檔頭），但為了讓「不同角色看到不同
 * 畫面」（例如 §3.7.3 退款按鈕的權限判斷）可以被實際點著看，這裡做成可以切換測試身分。
 */
import { computed } from 'vue'
import { ADMIN_USERS, currentUser, switchUser } from '@/data/session'

const displayName = computed(() => currentUser.value.displayName)
const roleName = computed(() => currentUser.value.roleNameZh)

function handleCommand(username: string) {
  switchUser(username)
}
</script>

<template>
  <el-dropdown trigger="click" @command="handleCommand">
    <button type="button" class="user-menu">
      <el-avatar :size="28" class="user-menu__avatar">{{ displayName.slice(0, 1) }}</el-avatar>
      <span class="user-menu__name admin-hide-on-mobile">{{ displayName }}</span>
      <el-icon class="admin-hide-on-mobile"><ArrowDown /></el-icon>
    </button>
    <template #dropdown>
      <el-dropdown-menu>
        <div class="user-menu__current">
          <p class="user-menu__current-name">{{ displayName }}</p>
          <p class="user-menu__current-role">目前身分：{{ roleName }}</p>
        </div>
        <el-dropdown-item divided disabled>切換測試身分（僅供 mockup 展示權限差異）</el-dropdown-item>
        <el-dropdown-item v-for="u in ADMIN_USERS" :key="u.username" :command="u.username">
          {{ u.displayName }}（{{ u.roleNameZh }}）
        </el-dropdown-item>
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
