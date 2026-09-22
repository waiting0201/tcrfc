<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { NAV_ITEMS } from '@/data/nav'

const props = defineProps<{
  collapse: boolean
}>()

const emit = defineEmits<{
  (e: 'navigate'): void
}>()

const route = useRoute()
const router = useRouter()

function handleSelect(path: string) {
  if (route.path !== path) router.push(path)
  emit('navigate')
}
</script>

<template>
  <div class="app-sidebar">
    <el-menu
      :default-active="route.path"
      :collapse="props.collapse"
      :collapse-transition="false"
      class="app-sidebar__menu"
      @select="handleSelect"
    >
      <el-menu-item v-for="item in NAV_ITEMS" :key="item.code" :index="item.path">
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
  /* 選中態用左側 accent bar＋色階，不用大面積填色塊（docs/22 §3.3 沿用 docs/21 §1.3 的理由：
     長時間停留在同一模組時，大面積填色比一條 accent bar 更容易造成視覺疲勞，與飽和度高低無關） */
  --el-menu-bg-color: var(--charity-admin-bg-surface);
  --el-menu-text-color: var(--charity-admin-text-secondary);
  --el-menu-hover-bg-color: var(--charity-admin-bg-surface-2);
  --el-menu-hover-text-color: var(--charity-admin-text-primary);
  --el-menu-active-color: var(--charity-admin-primary);
  border-right: none;
  height: 100%;
}

.app-sidebar__menu:not(.el-menu--collapse) {
  width: var(--charity-admin-sidebar-width-expanded);
}

.app-sidebar__menu :deep(.el-menu-item.is-active) {
  background-color: var(--charity-admin-bg-surface-2);
  color: var(--charity-admin-primary);
  font-weight: 600;
  box-shadow: inset 3px 0 0 0 var(--charity-admin-primary);
}
</style>
