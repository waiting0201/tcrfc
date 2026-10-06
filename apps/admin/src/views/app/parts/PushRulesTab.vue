<script setup lang="ts">
/**
 * 自動推播規則。⚠️ 目前只保存規則：實際觸發（掃描賽事與會籍到期、產生推播批次）屬 App 開發階段，
 * 這裡的開關與時間設定要等到觸發程式完成後才會生效。新聞發布的自動推播預設關閉；帶抽獎標籤的文章一律不自動推播。
 */
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { getPushRules, savePushRules, type PushRulesDto } from '@/api/adminApp'

const canEdit = usePermission('app.push.approve')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const rules = ref<PushRulesDto | null>(null)
const loading = ref(true)
const loadError = ref<string | null>(null)
const saving = ref(false)
const formErrors = provideFormErrors()
const form = reactive({ matchReminderHours: 2, expiryDays: '30,7', toggles: {} as Record<string, boolean> })
function apply(r: PushRulesDto) {
  rules.value = r
  form.matchReminderHours = r.matchReminderHours
  form.expiryDays = r.membershipExpiryDays.join(',')
  form.toggles = Object.fromEntries(r.toggles.map((t) => [t.key, t.enabled]))
}
async function load() {
  loading.value = true
  loadError.value = null
  try {
    apply(await getPushRules())
  } catch (e) {
    rules.value = null
    loadError.value = errText(e, '規則載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

async function save() {
  const errors: Record<string, string> = {}
  const days = form.expiryDays.split(/[,，\s]+/).filter(Boolean).map(Number)
  if (days.length < 1 || days.length > 5 || days.some((d) => !Number.isInteger(d) || d < 1 || d > 365) || new Set(days).size !== days.length) {
    errors.membershipExpiryDays = '到期提醒天數請填 1 到 5 個不重複的整數（1–365），用逗號分隔，例如 30,7'
  }
  if (!Number.isInteger(form.matchReminderHours) || form.matchReminderHours < 1 || form.matchReminderHours > 72) {
    errors.matchReminderHours = '賽前提醒小時數請填 1 到 72'
  }
  if (formErrors.replaceAll(errors)) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    apply(await savePushRules({ matchReminderHours: form.matchReminderHours, membershipExpiryDays: days.sort((a, b) => b - a), toggles: form.toggles }))
    ElMessage.success('已儲存')
  } catch (e) {
    if (e instanceof AdminApiError && formErrors.applyApiError(e)) return
    ElMessage.error(errText(e, '儲存失敗，請稍後再試'))
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div>
    <el-alert class="pr__block" type="warning" show-icon :closable="false" title="目前只保存規則，還沒有實際觸發" description="賽前提醒、會籍到期提醒、新聞發布等自動推播的「觸發程式」要等 App 開發階段才會完成；在那之前，這裡的開關與時間設定不會讓系統自動送出任何推播。" />
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else-if="rules" shadow="never">
      <el-alert v-if="!canEdit" type="info" show-icon :closable="false" title="自動推播規則只有系統管理員能修改，你的帳號只能檢視。" class="pr__block" />
      <el-form label-position="top" :disabled="!canEdit">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><FormField field="matchReminderHours" label="賽前幾小時提醒（1–72）"><el-input-number v-model="form.matchReminderHours" :min="1" :max="72" controls-position="right" style="width: 100%" /></FormField></el-col>
          <el-col :xs="24" :sm="12"><FormField field="membershipExpiryDays" label="會籍到期前幾天提醒（最多 5 個，用逗號分隔）"><el-input v-model="form.expiryDays" placeholder="30,7" /></FormField></el-col>
        </el-row>
        <el-form-item label="自動推播項目">
          <div class="pr__toggles"><div v-for="t in rules.toggles" :key="t.key" class="pr__toggle"><el-switch v-model="form.toggles[t.key]" /><span>{{ t.label }}</span></div></div>
        </el-form-item>
      </el-form>
      <p class="pr__hint">新聞發布的自動推播預設關閉；帶「球迷會員抽獎」標籤的文章一律不會自動推播，避免推播成為繞過「中獎只在最新消息公布」的後門。</p>
      <el-button v-if="canEdit" type="primary" :loading="saving" @click="save">儲存規則</el-button>
    </el-card>
  </div>
</template>

<style scoped>
.pr__block { margin-bottom: 12px; }
.pr__toggles { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 10px 16px; width: 100%; }
.pr__toggle { display: flex; align-items: center; gap: 8px; font-size: 13px; }
.pr__hint { margin: 0 0 12px; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>
