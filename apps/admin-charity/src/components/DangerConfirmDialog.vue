<script setup lang="ts">
/**
 * 危險操作確認對話框（docs/22-charity-ui.md §3.1 沿用 docs/21 §4.3）：
 * 「取消」用中性樣式、「確認」用危險色，且要求填寫原因／備註時才能送出——
 * 用於重新產生 slug、退款、標記對帳差異已處理、發票作廢／折讓等會寫入稽核的操作。
 */
import { ref, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    modelValue: boolean
    title: string
    /** 是否要求填寫原因／備註（這類操作通常會寫入稽核紀錄） */
    requireReason?: boolean
    reasonLabel?: string
    confirmText?: string
    auditNotice?: string
  }>(),
  {
    requireReason: true,
    reasonLabel: '原因／備註',
    confirmText: '確認',
    auditNotice: '',
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm', reason: string): void
}>()

const reason = ref('')

watch(
  () => props.modelValue,
  (visible) => {
    if (visible) reason.value = ''
  },
)

function handleConfirm() {
  if (props.requireReason && !reason.value.trim()) return
  emit('confirm', reason.value.trim())
  emit('update:modelValue', false)
}

function handleCancel() {
  emit('update:modelValue', false)
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="title"
    width="440px"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <p v-if="$slots.default" class="danger-confirm__body"><slot /></p>
    <el-form-item v-if="requireReason" :label="reasonLabel" required class="danger-confirm__reason">
      <el-input v-model="reason" type="textarea" :rows="3" />
    </el-form-item>
    <p v-if="auditNotice" class="danger-confirm__notice">
      <el-icon><WarningFilled /></el-icon>
      {{ auditNotice }}
    </p>
    <template #footer>
      <el-button @click="handleCancel">取消</el-button>
      <el-button type="danger" :disabled="requireReason && !reason.trim()" @click="handleConfirm">
        {{ confirmText }}
      </el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.danger-confirm__body {
  margin: 0 0 var(--charity-admin-space-3);
  color: var(--charity-admin-text-primary);
  font-size: 14px;
  line-height: 1.6;
}

.danger-confirm__reason {
  margin-bottom: var(--charity-admin-space-3);
}

.danger-confirm__notice {
  display: flex;
  align-items: center;
  gap: 6px;
  margin: 0;
  font-size: 13px;
  color: var(--charity-warning-text);
}
</style>
