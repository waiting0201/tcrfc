<script setup lang="ts">
/**
 * 單列頂欄（docs/22-charity-ui.md §3.3／§3.4）：沒有站台切換器（慈善庫沒有 club_id），
 * 全域控制只剩 hamburger／通知／使用者選單三項，56px 綽綽有餘，不做 apps/admin 那種兩列頂欄。
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import UserMenu from './UserMenu.vue'
import { RECONCILIATION_DISCREPANCIES } from '@/data/reconciliationAudit'

defineProps<{
  isMobile: boolean
}>()

const emit = defineEmits<{
  (e: 'toggle-sidebar'): void
}>()

const route = useRoute()

/** 通知數字：待處理的對帳差異筆數（真的算出來的數字，不是隨便寫的裝飾用假數字） */
const pendingCount = computed(
  () => RECONCILIATION_DISCREPANCIES.filter((d) => d.resolutionStatus === 'pending').length,
)
</script>

<template>
  <div class="app-topbar" :class="{ 'app-topbar--mobile': isMobile }">
    <button class="app-topbar__icon-btn" type="button" aria-label="切換側欄" @click="emit('toggle-sidebar')">
      <el-icon :size="20"><Fold v-if="!isMobile" /><Expand v-else /></el-icon>
    </button>

    <h1 v-if="isMobile" class="app-topbar__mobile-title">{{ route.meta.label ?? '' }}</h1>
    <span v-else class="app-topbar__brand">慈善捐款平台後台</span>

    <div class="app-topbar__spacer" />

    <router-link to="/donations?tab=reconciliation" class="app-topbar__notif" aria-label="待處理的對帳差異">
      <el-badge :value="pendingCount" :hidden="pendingCount === 0">
        <el-icon :size="20"><Bell /></el-icon>
      </el-badge>
    </router-link>
    <UserMenu />
  </div>
</template>

<style scoped>
.app-topbar {
  height: var(--charity-admin-topbar-height);
  display: flex;
  align-items: center;
  gap: var(--charity-admin-space-4);
  padding: 0 var(--charity-admin-space-4);
  background: var(--charity-admin-bg-surface);
  border-bottom: 1px solid var(--charity-admin-border);
}

.app-topbar--mobile {
  height: var(--charity-admin-topbar-mobile-height);
}

.app-topbar__icon-btn {
  border: none;
  background: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  color: var(--charity-admin-text-primary);
  padding: var(--charity-admin-space-1);
}

.app-topbar__brand {
  font-size: 14px;
  font-weight: 600;
  color: var(--charity-admin-text-secondary);
}

.app-topbar__mobile-title {
  font-size: 16px;
  font-weight: 600;
  margin: 0;
  color: var(--charity-admin-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  min-width: 0;
}

.app-topbar__spacer {
  flex: 1;
  min-width: var(--charity-admin-space-2);
}

.app-topbar__notif {
  display: inline-flex;
  cursor: pointer;
  margin-right: var(--charity-admin-space-1);
  color: var(--charity-admin-text-secondary);
}
</style>
