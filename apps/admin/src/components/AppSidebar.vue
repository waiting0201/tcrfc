<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NAV_GROUPS } from '@/data/nav'
import type { NavGroup } from '@/types/nav'
import { authUser } from '@/auth/session'
import { useProgramPermissions } from '@/composables/useProgramPermissions'

const props = defineProps<{
  collapse: boolean
}>()

const emit = defineEmits<{
  (e: 'navigate'): void
}>()

const route = useRoute()
const router = useRouter()

/**
 * `J 系統管理` 整組只有系統管理員看得到（規劃書 §6 權限矩陣「系統」欄只有系統管理員打勾，
 * 其餘角色是「—」或「✗」）。這裡只是選單可見度，不是安全邊界——真正的把關在後端每一個
 * `system.*` 權限碼（皆為 `sysadmin_only`）與 `router/index.ts` 的第二層路由守衛。
 */
const SYSADMIN_ONLY_MODULE_CODES = new Set(['J'])

/**
 * P1／P2／P3（課程與活動）：不是每個角色都看得到，見 `useProgramPermissions` 檔頭的完整角色
 * 對照表。`customer_service_admin` 只看得到「報名」；`pr_media`／`translator` 三個都看不到
 * ——這兩個角色一旦把 P1／P2／P3 都濾掉，`children` 會變成空陣列，下面的 `.filter` 會連「課程
 * 與活動」這個父層一併拿掉，不會留下一個點進去卻沒有任何子項目的空選單。
 */
const programPermissions = useProgramPermissions()
const CHILD_VISIBILITY: Record<string, () => boolean> = {
  P1: () => programPermissions.canViewItems.value,
  P2: () => programPermissions.canViewItems.value,
  P3: () => programPermissions.canViewRegistrations.value,
}

const visibleGroups = computed<NavGroup[]>(() => {
  const isSuperAdmin = authUser.value?.isSuperAdmin ?? false
  return NAV_GROUPS.map((group) => ({
    ...group,
    modules: group.modules
      .filter((mod) => isSuperAdmin || !SYSADMIN_ONLY_MODULE_CODES.has(mod.code))
      .map((mod) => {
        if (!mod.children) return mod
        const children = mod.children.filter((child) => {
          const check = CHILD_VISIBILITY[child.code]
          return isSuperAdmin || !check || check()
        })
        return { ...mod, children }
      })
      .filter((mod) => !mod.children || mod.children.length > 0),
  })).filter((group) => group.modules.length > 0)
})

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
      <template v-for="group in visibleGroups" :key="group.groupLabel">
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
