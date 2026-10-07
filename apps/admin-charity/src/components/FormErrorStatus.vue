<script setup lang="ts">
/**
 * EditActionBar `#status` 內放的狀態列：「有 N 處需要修正」＋「前往下一處」。
 * 沒有錯誤時不輸出任何內容（外層 aria-live 區仍在，數字出現時才會被朗讀）。
 */
import { useFormErrors } from '@/composables/useFormErrors'

const formErrors = useFormErrors()
</script>

<template>
  <span v-if="formErrors.count.value > 0" class="form-error-status">
    <span aria-hidden="true" class="form-error-status__icon">⚠</span>
    <span>有 {{ formErrors.count.value }} 處需要修正</span>
    <el-button size="small" @click="formErrors.focusFirst()">前往下一處</el-button>
  </span>
</template>

<style scoped>
.form-error-status {
  display: inline-flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  font-size: 13px;
  color: var(--charity-danger-text);
}
</style>
