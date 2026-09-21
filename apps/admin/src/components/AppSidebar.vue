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
}

.app-sidebar__menu {
  border-right: none;
  height: 100%;
}

.app-sidebar__menu:not(.el-menu--collapse) {
  width: var(--admin-sidebar-width-expanded);
}
</style>
