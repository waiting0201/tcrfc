<script setup lang="ts">
/**
 * 頂欄（docs/21-admin-ui.md §1.2／§8.2）。
 *
 * 桌面／平板：這是「系統列」——40px，surface-2 色階，放 hamburger／收合鈕、站台切換器、通知、
 * 使用者選單，全部是跨模組、任何頁面都一樣的全域控制。頁面標題與「這裡管理的是」不在這裡，
 * 是各頁面自己的 PageHeader（頁面列，見 PageHeader.vue）。
 *
 * 手機：系統列與頁面列合併成單一 56px 列（§8.2），站台切換器移進抽屜頂部（見 AdminLayout.vue），
 * 這裡改顯示目前頁面標題（直接讀 route.meta.label——router/index.ts 的每一筆路由都帶這個欄位，
 * 不需要另外建一個跨元件的狀態store）。
 */
import { useRoute } from 'vue-router'
import UserMenu from './UserMenu.vue'
import SiteSwitcher from './SiteSwitcher.vue'

defineProps<{
  /** 手機寬度：顯示 hamburger 開關 drawer；桌面／平板：顯示收合切換鈕 */
  isMobile: boolean
}>()

const emit = defineEmits<{
  (e: 'toggle-sidebar'): void
}>()

const route = useRoute()
</script>

<template>
  <div class="app-topbar" :class="{ 'app-topbar--mobile': isMobile }">
    <button class="app-topbar__icon-btn" type="button" aria-label="切換側欄" @click="emit('toggle-sidebar')">
      <el-icon :size="20"><Fold v-if="!isMobile" /><Expand v-else /></el-icon>
    </button>

    <template v-if="isMobile">
      <h1 class="app-topbar__mobile-title">{{ route.meta.label ?? '' }}</h1>
    </template>
    <template v-else>
      <SiteSwitcher />
    </template>

    <div class="app-topbar__spacer" />

    <el-badge :value="3" class="app-topbar__notif">
      <el-icon :size="20"><Bell /></el-icon>
    </el-badge>
    <UserMenu />
  </div>
</template>

<style scoped>
.app-topbar {
  height: var(--admin-topbar-system-height);
  display: flex;
  align-items: center;
  gap: var(--admin-space-4);
  padding: 0 var(--admin-space-4);
  background: var(--admin-bg-surface-2);
  border-bottom: 1px solid var(--admin-border);
}

.app-topbar--mobile {
  height: var(--admin-topbar-mobile-height);
}

.app-topbar__icon-btn {
  border: none;
  background: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  color: var(--admin-text-primary);
  padding: var(--admin-space-1);
}

.app-topbar__mobile-title {
  font-size: 16px;
  font-weight: 600;
  margin: 0;
  color: var(--admin-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  min-width: 0;
}

.app-topbar__spacer {
  flex: 1;
  min-width: var(--admin-space-2);
}

.app-topbar__notif {
  display: inline-flex;
  cursor: pointer;
  margin-right: var(--admin-space-1);
  color: var(--admin-text-secondary);
}
</style>
