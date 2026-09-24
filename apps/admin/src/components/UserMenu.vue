<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { authUser } from '@/auth/session'
import { logout } from '@/api/adminAuth'
import { resetClubAccess } from '@/auth/clubAccess'

const router = useRouter()

// `displayName` 由 `GET /auth/me` 補上（見 `@/auth/clubAccess`），登入完成的當下還沒有這筆資料時，
// `@/auth/session` 的 `setSession()` 已經先用帳號字串頂著，這裡不需要再自己 fallback 一次。
const displayName = computed(() => authUser.value?.displayName ?? '')
const initial = computed(() => displayName.value.slice(0, 1).toUpperCase())

async function handleCommand(command: string) {
  if (command === 'security') {
    router.push('/account/security')
    return
  }
  if (command === 'logout') {
    await logout()
    resetClubAccess()
    ElMessage.success('已登出')
    router.push('/login')
  }
}
</script>

<template>
  <el-dropdown trigger="click" @command="handleCommand">
    <span class="user-menu">
      <el-avatar :size="28">{{ initial }}</el-avatar>
      <span class="user-menu__name admin-hide-on-mobile">{{ displayName }}</span>
      <el-icon><ArrowDown /></el-icon>
    </span>
    <template #dropdown>
      <el-dropdown-menu>
        <el-dropdown-item command="security">帳號安全設定</el-dropdown-item>
        <el-dropdown-item command="logout" divided>登出</el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>

<style scoped>
.user-menu {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  font-size: 13px;
  color: var(--el-text-color-primary);
}
</style>
