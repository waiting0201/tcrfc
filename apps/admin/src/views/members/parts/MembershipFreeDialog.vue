<script setup lang="ts">
/** 建立免費（一般會員）會籍：選會員與球季，系統同時發第一張會員卡。 */
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import MemberPicker from './MemberPicker.vue'
import { activeClubId } from '@/auth/clubAccess'
import { createFreeMembership, type MembershipDetailDto } from '@/api/adminMemberships'
import { errorMessage } from './membershipHelpers'

const props = defineProps<{ modelValue: boolean; seasons: { id: string; code: string }[] }>()
const emit = defineEmits<{
  (e: 'update:modelValue', v: boolean): void
  (e: 'done', detail: MembershipDetailDto): void
}>()

const memberId = ref('')
const seasonId = ref('')
const saving = ref(false)
const error = ref<string | null>(null)

watch(
  () => props.modelValue,
  (open) => {
    if (!open) return
    memberId.value = ''
    seasonId.value = props.seasons[0]?.id ?? ''
    error.value = null
  },
)

async function submit() {
  if (!memberId.value) return void (error.value = '請先選擇會員')
  if (!seasonId.value) return void (error.value = '請選擇球季')
  saving.value = true
  error.value = null
  try {
    const detail = await createFreeMembership(activeClubId.value, { memberId: memberId.value, seasonId: seasonId.value })
    ElMessage.success('已建立一般會員會籍')
    emit('update:modelValue', false)
    emit('done', detail)
  } catch (e) {
    error.value = errorMessage(e, '建立失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="建立一般會員會籍"
    width="min(460px, 94vw)"
    :close-on-click-modal="false"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <p class="dlg__desc">一般會員不需付費。建立後系統會同時發給這位會員第一張會員卡；同一球季已有會籍的會員無法重複建立。</p>
    <el-alert v-if="error" :title="error" type="warning" show-icon class="dlg__alert" @close="error = null" />
    <el-form label-position="top" @submit.prevent="submit">
      <el-form-item label="會員" required><MemberPicker v-model="memberId" /></el-form-item>
      <el-form-item label="球季" required>
        <el-select v-model="seasonId" placeholder="選擇球季" style="width: 100%">
          <el-option v-for="s in seasons" :key="s.id" :value="s.id" :label="s.code" />
        </el-select>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button :disabled="saving" @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="saving" @click="submit">建立</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.dlg__desc { margin: 0 0 12px; font-size: 13px; line-height: 1.6; color: var(--admin-text-secondary); }
.dlg__alert { margin-bottom: 12px; }
</style>
