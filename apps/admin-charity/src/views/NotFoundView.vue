<script setup lang="ts">
import { useRouter } from 'vue-router'
const router = useRouter()
</script>

<template>
  <div class="not-found">
    <h1>找不到這個頁面</h1>
    <p>網址可能已經變更，或這個頁面尚未建立。</p>
    <el-button type="primary" @click="router.push('/stores')">回到後台首頁</el-button>
  </div>
</template>

<style scoped>
.not-found {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
  background: var(--charity-admin-bg-page);
  color: var(--charity-admin-text-primary);
  font-family: var(--charity-admin-font-family);
}
</style>
