<script setup lang="ts">
/**
 * 匯出含個資名單前的「用途」對話框（規劃書 §4.0 個資通則：匯出要留下用途）。
 * 用途必填、至多 200 字；確認後由外層執行匯出並在完成後關閉（`loading` 期間鎖住按鈕）。
 * 後端每次匯出都會記下「誰、匯出什麼、幾筆、用途」，這裡的文字要讓使用者知道這件事。
 */
import { ref, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    modelValue: boolean
    /** 例如「匯出會員名單」 */
    title: string
    /** 補充說明，例如「名單含姓名、Email、電話」 */
    description?: string
    loading?: boolean
  }>(),
  { description: '', loading: false },
)
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm', purpose: string): void
}>()

const purpose = ref('')
const error = ref<string | null>(null)

watch(
  () => props.modelValue,
  (open) => {
    if (open) {
      purpose.value = ''
      error.value = null
    }
  },
)

function handleConfirm() {
  const value = purpose.value.trim()
  if (!value) {
    error.value = '請填寫匯出用途，例如「球季末續會聯繫」'
    return
  }
  error.value = null
  emit('confirm', value)
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="title"
    width="460px"
    class="export-purpose-dialog"
    :close-on-click-modal="false"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <p v-if="description" class="export-purpose-dialog__desc">{{ description }}</p>
    <p class="export-purpose-dialog__desc">名單含個人資料，請只用在必要的用途。系統會記錄這次匯出的人員、筆數與用途。</p>
    <el-form label-position="top" @submit.prevent="handleConfirm">
      <el-form-item label="匯出用途" required :error="error ?? undefined">
        <el-input v-model="purpose" type="textarea" :rows="3" maxlength="200" show-word-limit placeholder="請簡述為什麼要匯出這份名單" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button :disabled="loading" @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="loading" @click="handleConfirm">確認匯出</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.export-purpose-dialog__desc {
  margin: 0 0 10px;
  font-size: 13px;
  line-height: 1.6;
  color: var(--admin-text-secondary);
}
</style>
