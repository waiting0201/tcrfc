<script setup lang="ts"></script>

<template>
  <div class="not-found">
    <el-result icon="warning" title="找不到這個頁面" sub-title="請確認網址是否正確">
      <template #extra>
        <el-button type="primary" @click="$router.push('/dashboard')">回到儀表板</el-button>
      </template>
    </el-result>
  </div>
</template>

<style scoped>
.not-found {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
}
</style>
