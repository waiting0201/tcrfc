<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import AppSidebar from '@/components/AppSidebar.vue'
import AppTopbar from '@/components/AppTopbar.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'

const STORAGE_KEY = 'tcrfc-charity-admin-sidebar-collapsed'

const { breakpoint } = useBreakpoint()

const desktopCollapsed = ref(localStorage.getItem(STORAGE_KEY) === '1')
watch(desktopCollapsed, (value) => {
  localStorage.setItem(STORAGE_KEY, value ? '1' : '0')
})

const tabletExpanded = ref(false)
const drawerVisible = ref(false)

const isMobile = computed(() => breakpoint.value === 'mobile')

const sidebarCollapse = computed(() => {
  if (breakpoint.value === 'desktop') return desktopCollapsed.value
  if (breakpoint.value === 'tablet') return !tabletExpanded.value
  return false
})

function toggleSidebar() {
  if (breakpoint.value === 'mobile') {
    drawerVisible.value = !drawerVisible.value
  } else if (breakpoint.value === 'tablet') {
    tabletExpanded.value = !tabletExpanded.value
  } else {
    desktopCollapsed.value = !desktopCollapsed.value
  }
}

function handleNavigate() {
  if (isMobile.value) drawerVisible.value = false
}
</script>

<template>
  <el-container class="admin-layout">
    <el-aside
      v-if="!isMobile"
      class="admin-layout__aside"
      :width="sidebarCollapse ? 'var(--charity-admin-sidebar-width-collapsed)' : 'var(--charity-admin-sidebar-width-expanded)'"
    >
      <div class="admin-layout__brand">
        <span v-if="!sidebarCollapse" class="admin-layout__brand-text">慈善後台</span>
        <button type="button" class="admin-layout__brand-toggle" aria-label="收合側欄" @click="desktopCollapsed = !desktopCollapsed">
          <el-icon :size="16"><Fold v-if="!sidebarCollapse" /><Expand v-else /></el-icon>
        </button>
      </div>
      <AppSidebar :collapse="sidebarCollapse" @navigate="handleNavigate" />
    </el-aside>

    <el-drawer
      v-else
      v-model="drawerVisible"
      direction="ltr"
      size="240px"
      :with-header="true"
      title="慈善後台"
    >
      <AppSidebar :collapse="false" @navigate="handleNavigate" />
    </el-drawer>

    <el-container class="admin-layout__body">
      <el-header class="admin-layout__header" :height="isMobile ? 'var(--charity-admin-topbar-mobile-height)' : 'var(--charity-admin-topbar-height)'">
        <AppTopbar :is-mobile="isMobile" @toggle-sidebar="toggleSidebar" />
      </el-header>
      <el-main class="admin-layout__main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.admin-layout {
  height: 100vh;
  background: var(--charity-admin-bg-page);
}

.admin-layout__aside {
  background: var(--charity-admin-bg-surface);
  border-right: 1px solid var(--charity-admin-border);
  transition: width 0.2s;
  display: flex;
  flex-direction: column;
}

.admin-layout__brand {
  height: var(--charity-admin-topbar-height);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 var(--charity-admin-space-4);
  border-bottom: 1px solid var(--charity-admin-border);
  flex-shrink: 0;
}

.admin-layout__brand-text {
  font-size: 14px;
  font-weight: 600;
  color: var(--charity-admin-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.admin-layout__brand-toggle {
  border: none;
  background: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  color: var(--charity-admin-text-secondary);
  padding: var(--charity-admin-space-1);
  margin-left: auto;
}

.admin-layout__body {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.admin-layout__header {
  padding: 0;
}

.admin-layout__main {
  background: var(--charity-admin-bg-page);
  overflow-y: auto;
  padding: var(--charity-admin-space-6);
}

@media (max-width: 767px) {
  .admin-layout__main {
    padding: var(--charity-admin-space-4);
  }
}
</style>
