<script setup lang="ts">
/** 球季末批次到期：先試算件數，確認後才真正執行。 */
import { ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { activeClubId } from '@/auth/clubAccess'
import { expireBatch } from '@/api/adminMemberships'
import { errorMessage, todayString } from './membershipHelpers'

const props = defineProps<{ modelValue: boolean; seasons: { id: string; code: string }[] }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: boolean): void; (e: 'done'): void }>()

const asOf = ref(todayString())
const seasonId = ref('')
const trialCount = ref<number | null>(null)
const busy = ref(false)
const error = ref<string | null>(null)

watch(
  () => props.modelValue,
  (open) => {
    if (!open) return
    asOf.value = todayString()
    seasonId.value = ''
    trialCount.value = null
    error.value = null
  },
)
// 條件一改，先前的試算就不作數
watch([asOf, seasonId], () => (trialCount.value = null))

function body(dryRun: boolean) {
  return { asOf: asOf.value || undefined, seasonId: seasonId.value || undefined, dryRun }
}

async function trial() {
  busy.value = true
  error.value = null
  try {
    trialCount.value = (await expireBatch(activeClubId.value, body(true))).count
  } catch (e) {
    error.value = errorMessage(e, '試算失敗，請稍後再試')
  } finally {
    busy.value = false
  }
}

async function execute() {
  try {
    await ElMessageBox.confirm(
      `將把 ${trialCount.value} 份到期日早於 ${asOf.value} 的有效會籍標為「已到期」，確定要執行嗎？`,
      '確認批次到期',
      { confirmButtonText: '確認執行', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  busy.value = true
  error.value = null
  try {
    const r = await expireBatch(activeClubId.value, body(false))
    ElMessage.success(`已將 ${r.count} 份會籍標為已到期`)
    emit('update:modelValue', false)
    emit('done')
  } catch (e) {
    error.value = errorMessage(e, '執行失敗，請稍後再試')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="球季末批次到期"
    width="min(480px, 94vw)"
    :close-on-click-modal="false"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <p class="dlg__desc">把「到期日早於基準日」的有效會籍一次標為已到期，只處理目前這個俱樂部。請先試算件數，再確認執行。</p>
    <el-alert v-if="error" :title="error" type="warning" show-icon class="dlg__alert" @close="error = null" />
    <el-form label-position="top">
      <el-form-item label="基準日（預設今天）">
        <el-date-picker v-model="asOf" type="date" value-format="YYYY-MM-DD" :clearable="false" style="width: 100%" />
      </el-form-item>
      <el-form-item label="限定球季（不選＝全部球季）">
        <el-select v-model="seasonId" clearable placeholder="全部球季" style="width: 100%">
          <el-option v-for="s in seasons" :key="s.id" :value="s.id" :label="s.code" />
        </el-select>
      </el-form-item>
    </el-form>
    <el-alert v-if="trialCount !== null" :type="trialCount > 0 ? 'warning' : 'success'" :closable="false" show-icon>
      試算結果：共 {{ trialCount }} 份會籍會被標為已到期。
    </el-alert>
    <template #footer>
      <el-button :disabled="busy" @click="emit('update:modelValue', false)">取消</el-button>
      <el-button :loading="busy" @click="trial">試算件數</el-button>
      <el-button type="danger" :disabled="busy || !trialCount" @click="execute">確認執行</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.dlg__desc { margin: 0 0 12px; font-size: 13px; line-height: 1.6; color: var(--admin-text-secondary); }
.dlg__alert { margin-bottom: 12px; }
</style>
