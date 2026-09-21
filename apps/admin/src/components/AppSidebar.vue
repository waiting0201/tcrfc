<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { NAV_GROUPS } from '@/data/nav'

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
      unique-opened
      class="app-sidebar__menu"
      @select="handleSelect"
    >
      <template v-for="group in NAV_GROUPS" :key="group.groupLabel">
        <el-menu-item-group :title="props.collapse ? undefined : group.groupLabel">
          <template v-for="mod in group.modules" :key="mod.code">
            <el-sub-menu v-if="mod.children" :index="mod.code">
              <template #title>
                <el-icon><Folder /></el-icon>
                <span>{{ mod.label }}</span>
              </template>
              <el-menu-item v-for="child in mod.children" :key="child.path" :index="child.path">
                {{ child.label }}
              </el-menu-item>
            </el-sub-menu>
            <el-menu-item v-else :index="mod.path">
              <el-icon><Odometer /></el-icon>
              <template #title>{{ mod.label }}</template>
            </el-menu-item>
          </template>
        </el-menu-item-group>
      </template>
    </el-menu>
  </div>
</template>

<style scoped>
.app-sidebar {
  height: 100%;
  overflow-y: auto;
  overflow-x: hidden;
  background: var(--admin-bg-surface);
}

.app-sidebar__menu {
  /* 選中態改用左側 accent bar＋色階，不用大面積填色塊（docs/21 §1.3：長時間盯著一大塊高飽和藍
     比淺色底更容易視覺疲勞，一條 accent bar 加粗體文字就足夠標示「你在這裡」） */
  --el-menu-bg-color: var(--admin-bg-surface);
  --el-menu-text-color: var(--admin-text-secondary);
  --el-menu-hover-bg-color: var(--admin-bg-surface-2);
  --el-menu-hover-text-color: var(--admin-text-primary);
  --el-menu-active-color: var(--admin-primary);
  border-right: none;
  height: 100%;
}

.app-sidebar__menu:not(.el-menu--collapse) {
  width: var(--admin-sidebar-width-expanded);
}

/* 一般 hover：只變色階，不介入色相（§1.3） */
.app-sidebar__menu :deep(.el-menu-item:hover),
.app-sidebar__menu :deep(.el-sub-menu__title:hover) {
  background-color: var(--admin-bg-surface-2);
}

/* 選中（當前路由）：左側 3px accent bar ＋ surface-2 底 ＋ primary 粗體文字 */
.app-sidebar__menu :deep(.el-menu-item.is-active) {
  background-color: var(--admin-bg-surface-2);
  color: var(--admin-primary);
  font-weight: 600;
  box-shadow: inset 3px 0 0 0 var(--admin-primary);
}

.app-sidebar__menu :deep(.el-menu-item-group__title) {
  color: var(--admin-text-tertiary);
}
</style>
