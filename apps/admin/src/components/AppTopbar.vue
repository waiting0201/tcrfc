<script setup lang="ts">
import SiteSwitcher from './SiteSwitcher.vue'
import UserMenu from './UserMenu.vue'

defineProps<{
  /** 手機寬度：顯示 hamburger 開關 drawer；桌面／平板：顯示收合切換鈕 */
  isMobile: boolean
}>()

const emit = defineEmits<{
  (e: 'toggle-sidebar'): void
}>()
</script>

<template>
  <div class="app-topbar">
    <button class="app-topbar__icon-btn" type="button" aria-label="切換側欄" @click="emit('toggle-sidebar')">
      <el-icon :size="20"><Fold v-if="!isMobile" /><Expand v-else /></el-icon>
    </button>
    <SiteSwitcher />
    <div class="app-topbar__spacer" />
    <el-badge :value="3" class="app-topbar__notif admin-hide-on-mobile">
      <el-icon :size="20"><Bell /></el-icon>
    </el-badge>
    <UserMenu />
  </div>
</template>

<style scoped>
.app-topbar {
  height: 56px;
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 0 16px;
  background: #fff;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.app-topbar__icon-btn {
  border: none;
  background: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  color: var(--el-text-color-primary);
  padding: 4px;
}

.app-topbar__spacer {
  flex: 1;
}

.app-topbar__notif {
  display: inline-flex;
  cursor: pointer;
  margin-right: 4px;
}
</style>
