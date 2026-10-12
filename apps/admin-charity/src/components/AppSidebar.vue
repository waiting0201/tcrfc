<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NAV_ITEMS, isNavItemVisible } from '@/data/nav'

const props = defineProps<{
  collapse: boolean
}>()

const emit = defineEmits<{
  (e: 'navigate'): void
}>()

const route = useRoute()
const router = useRouter()

// 依各模組檢視端點的權限碼決定要不要顯示；子頁（如 /stores/new）的選單高亮落在所屬模組。
const visibleItems = computed(() => NAV_ITEMS.filter(isNavItemVisible))
const activePath = computed(() => NAV_ITEMS.find((item) => route.path === item.path || route.path.startsWith(`${item.path}/`))?.path ?? route.path)

function handleSelect(path: string) {
  if (route.path !== path) router.push(path)
  emit('navigate')
}
</script>

<template>
  <div class="app-sidebar">
    <el-menu
      :default-active="activePath"
      :collapse="props.collapse"
      :collapse-transition="false"
      class="app-sidebar__menu"
      @select="handleSelect"
    >
      <el-menu-item v-for="item in visibleItems" :key="item.code" :index="item.path">
        <el-icon><Folder /></el-icon>
        <template #title>{{ item.label }}</template>
      </el-menu-item>
    </el-menu>
  </div>
</template>

<style scoped>
.app-sidebar {
  height: 100%;
  overflow-y: auto;
  overflow-x: hidden;
  background: var(--charity-admin-bg-surface);
}

.app-sidebar__menu {
  /* 選中態＝淡綠底 primary-tint ＋ 左側 3px 球場綠 bar ＋ 粗體，三重訊號；hover 用 surface-2，
     兩者可區分（docs/22 §3.3／§3.4）。不用實心綠色塊。 */
  --el-menu-bg-color: var(--charity-admin-bg-surface);
  --el-menu-text-color: var(--charity-admin-text-secondary);
  --el-menu-hover-bg-color: var(--charity-admin-bg-surface-2);
  --el-menu-hover-text-color: var(--charity-admin-text-primary);
  --el-menu-active-color: var(--charity-admin-text-primary);
  border-right: none;
  height: 100%;
}

.app-sidebar__menu:not(.el-menu--collapse) {
  width: var(--charity-admin-sidebar-width-expanded);
}

.app-sidebar__menu :deep(.el-menu-item.is-active) {
  background-color: var(--charity-admin-primary-tint);
  color: var(--charity-admin-text-primary);
  font-weight: 600;
  box-shadow: inset 3px 0 0 0 var(--charity-admin-primary);
}
</style>
