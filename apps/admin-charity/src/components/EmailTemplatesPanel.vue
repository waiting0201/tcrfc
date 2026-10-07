<script setup lang="ts">
/**
 * 系統信樣板：感謝信、憑證通知、憑證開立失敗通知、退款通知（四封），每封一個頁籤、各自一份表單
 * （EmailTemplateForm：代入欄位、中英語言分頁、欄位驗證、儲存）。
 * 更新後下一封信立即套用。目前系統信一律寄出中文版（捐款單沒有記錄捐款人的語系），英文版先存起來供日後使用。
 */
import { computed, onMounted, ref } from 'vue'
import EmailTemplateForm from '@/components/EmailTemplateForm.vue'
import ErrorState from '@/components/ErrorState.vue'
import { listEmailTemplates, type EmailTemplate } from '@/api/settings'
import { AdminApiError } from '@/api/http'
import { hasPermission } from '@/auth/session'

const canManage = computed(() => hasPermission('n7.setting.manage'))
const loading = ref(false)
const loadError = ref('')
const templates = ref<EmailTemplate[]>([])
const activeCode = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    templates.value = await listEmailTemplates()
    if (!templates.value.some((t) => t.code === activeCode.value)) activeCode.value = templates.value[0]?.code ?? ''
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '讀取系統信樣板時發生問題'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <ErrorState v-if="loadError" :text="loadError" @retry="load" />
  <div v-else v-loading="loading" class="email-templates">
    <el-alert v-if="!canManage" type="info" :closable="false" show-icon title="你的角色只能檢視系統信樣板，不能修改。" class="email-templates__alert" />
    <el-tabs v-model="activeCode" tab-position="top">
      <el-tab-pane v-for="t in templates" :key="t.code" :name="t.code" :label="t.label">
        <EmailTemplateForm :template="t" :can-manage="canManage" />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.email-templates__alert {
  margin-bottom: var(--charity-admin-space-3);
}
</style>
