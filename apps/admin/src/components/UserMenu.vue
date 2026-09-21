<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { CURRENT_USER } from '@/data/session'

function handleCommand(command: string) {
  if (command === 'logout') {
    ElMessage.info('已登出（mock，未串接真正的登入系統）')
  }
}
</script>

<template>
  <el-dropdown trigger="click" @command="handleCommand">
    <span class="user-menu">
      <el-avatar :size="28">{{ CURRENT_USER.name.slice(0, 1) }}</el-avatar>
      <span class="user-menu__name admin-hide-on-mobile">{{ CURRENT_USER.name }}</span>
      <el-icon><ArrowDown /></el-icon>
    </span>
    <template #dropdown>
      <el-dropdown-menu>
        <el-dropdown-item command="profile">個人設定</el-dropdown-item>
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
