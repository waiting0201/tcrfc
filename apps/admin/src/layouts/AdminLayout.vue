<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import AppSidebar from '@/components/AppSidebar.vue'
import AppTopbar from '@/components/AppTopbar.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'

const STORAGE_KEY = 'tcrfc-admin-sidebar-collapsed'

const { breakpoint } = useBreakpoint()

/** 桌面態的收合偏好，跨頁面／重新整理保留（docs/21-admin-ui.md §1：純前端便利設定，不必連後端） */
const desktopCollapsed = ref(localStorage.getItem(STORAGE_KEY) === '1')
watch(desktopCollapsed, (value) => {
  localStorage.setItem(STORAGE_KEY, value ? '1' : '0')
})

/** 平板態：強制收合，但使用者這個工作階段內可以手動展開（docs/21 §8） */
const tabletExpanded = ref(false)

/** 手機態：側欄變 el-drawer */
const drawerVisible = ref(false)

const isMobile = computed(() => breakpoint.value === 'mobile')

const sidebarCollapse = computed(() => {
  if (breakpoint.value === 'desktop') return desktopCollapsed.value
  if (breakpoint.value === 'tablet') return !tabletExpanded.value
  return false // mobile 用 drawer，drawer 內部一律展開態
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
      :width="sidebarCollapse ? 'var(--admin-sidebar-width-collapsed)' : 'var(--admin-sidebar-width-expanded)'"
    >
      <AppSidebar :collapse="sidebarCollapse" @navigate="handleNavigate" />
    </el-aside>

    <el-drawer
      v-else
      v-model="drawerVisible"
      direction="ltr"
      size="240px"
      :with-header="true"
      title="TCRFC 後台"
    >
      <AppSidebar :collapse="false" @navigate="handleNavigate" />
    </el-drawer>

    <el-container class="admin-layout__body">
      <el-header class="admin-layout__header" height="56px">
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
}

.admin-layout__aside {
  background: #fff;
  border-right: 1px solid var(--el-border-color-lighter);
  transition: width 0.2s;
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
  background: #f5f7fa;
  overflow-y: auto;
  padding: 16px;
}

@media (max-width: 767px) {
  .admin-layout__main {
    padding: 12px;
  }
}
</style>
