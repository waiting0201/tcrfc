<script setup lang="ts">
/**
 * I 電子報平台設定（規劃書 §4.9「外部服務連結 → EDM 平台設定」；apps/api/README.md「H 批」§5）。
 * 🔴 **金鑰只寫不讀**：後端任何回應都不含金鑰或片段，畫面只知道「是否已設定」。留空＝維持原金鑰；
 * 要換才輸入新的；要刪除勾「清除金鑰」。兩者同時給後端回 400，所以輸入新金鑰時自動取消清除勾選。
 * 設定／清除金鑰後端會寫敏感操作紀錄。每個俱樂部各一份，跟著站台切換器。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { getAdminEdmSettings, updateAdminEdmSettings, type AdminEdmSettings } from '@/api/adminSiteSettings'
import { formatDateTime } from '@/utils/dateTime'

const canUpdate = usePermission('site.edm.update')
const club = computed(() => activeClubId.value)

const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()

const form = reactive({ enabled: false, provider: '', listId: '', senderEmail: '' })
/** 新金鑰與清除意圖不屬於「已載入的表單內容」，各自獨立。 */
const newApiKey = ref('')
const clearApiKey = ref(false)
const apiKeyConfigured = ref(false)
const integrationAvailable = ref(false)
const updatedAt = ref<string | null>(null)
const baselineJson = ref('')

function apply(d: AdminEdmSettings) {
  form.enabled = d.enabled
  form.provider = d.provider ?? ''
  form.listId = d.listId ?? ''
  form.senderEmail = d.senderEmail ?? ''
  apiKeyConfigured.value = d.apiKeyConfigured
  integrationAvailable.value = d.integrationAvailable
  updatedAt.value = d.updatedAt ?? null
  newApiKey.value = ''
  clearApiKey.value = false
  baselineJson.value = JSON.stringify(form)
}

async function load() {
  loadState.value = 'loading'
  try {
    apply(await getAdminEdmSettings(club.value))
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}
onMounted(load)
watch(club, load)

watch(newApiKey, (v) => {
  if (v) clearApiKey.value = false
})
// 開關、清除勾選不一定冒泡 DOM 事件，變動時主動清掉相關欄位的錯誤
watch(() => form.enabled, () => { formErrors.clear('provider'); formErrors.clear('apiKey') })
watch(clearApiKey, () => formErrors.clear('apiKey'))

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!newApiKey.value || clearApiKey.value),
)
useUnsavedChanges(isDirty)

const willHaveKey = computed(() => !!newApiKey.value.trim() || (apiKeyConfigured.value && !clearApiKey.value))

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (form.enabled) {
    if (!form.provider.trim()) errors.provider = '啟用前請先填寫平台名稱。'
    if (!willHaveKey.value) errors.apiKey = '啟用前請先設定金鑰。'
  }
  if (newApiKey.value && (newApiKey.value.trim().length < 8 || /\s/.test(newApiKey.value.trim()))) {
    errors.apiKey = '金鑰至少 8 個字元，且不能含空白。'
  }
  return errors
}

async function handleSave() {
  if (!canUpdate.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const saved = await updateAdminEdmSettings(club.value, {
      enabled: form.enabled,
      provider: form.provider.trim() || null,
      listId: form.listId.trim() || null,
      senderEmail: form.senderEmail.trim() || null,
      apiKey: newApiKey.value.trim() || null,
      clearApiKey: clearApiKey.value,
    })
    apply(saved)
    ElMessage.success('已儲存')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="edm-settings">
    <PageHeader title="電子報平台">
      <template #meta>
        <FrontendUnitBanner module-code="I" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="edm-settings__block" @close="formError = null" />
      <el-alert v-if="!canUpdate" title="你的帳號只能檢視電子報平台設定，不能修改。" type="info" show-icon :closable="false" class="edm-settings__block" />
      <el-alert
        v-if="!integrationAvailable"
        title="尚未選定電子報服務供應商。這裡的設定會先儲存起來，等串接完成後才會真正用來同步訂閱名單與寄送。"
        type="info"
        show-icon
        :closable="false"
        class="edm-settings__block"
      />
      <el-form label-position="top" :disabled="!canUpdate">
        <EditLayout>
          <template #main>
            <el-card shadow="never">
              <FormSection title="平台設定">
                <el-form-item label="啟用電子報平台串接">
                  <el-switch v-model="form.enabled" />
                  <p class="edm-settings__hint">啟用前必須填寫平台名稱並設定金鑰。</p>
                </el-form-item>
                <FormField field="provider" label="平台名稱">
                  <el-input v-model="form.provider" maxlength="64" placeholder="例如使用的電子報服務名稱" />
                </FormField>
                <FormField field="listId" label="名單識別">
                  <el-input v-model="form.listId" maxlength="128" placeholder="電子報服務中，訂閱者要加入的名單編號或名稱" />
                </FormField>
                <FormField field="senderEmail" label="寄件者信箱">
                  <el-input v-model="form.senderEmail" maxlength="254" placeholder="name@example.com" />
                </FormField>
              </FormSection>

              <FormSection title="金鑰">
                <p class="edm-settings__status">
                  目前狀態：
                  <el-tag :type="apiKeyConfigured ? 'success' : 'info'" size="small">{{ apiKeyConfigured ? '已設定金鑰' : '尚未設定金鑰' }}</el-tag>
                </p>
                <FormField field="apiKey" :label="apiKeyConfigured ? '更換金鑰（不更換請留空）' : '設定金鑰'">
                  <el-input
                    v-model="newApiKey"
                    type="password"
                    show-password
                    autocomplete="new-password"
                    maxlength="512"
                    :placeholder="apiKeyConfigured ? '已設定，留空表示維持原金鑰' : '貼上電子報服務提供的金鑰'"
                  />
                  <p class="edm-settings__hint">基於安全，金鑰儲存後不會再顯示（連部分內容也不會）。設定與清除都會留下系統紀錄。</p>
                </FormField>
                <el-form-item v-if="apiKeyConfigured">
                  <el-checkbox v-model="clearApiKey" :disabled="!!newApiKey">清除已設定的金鑰</el-checkbox>
                </el-form-item>
              </FormSection>
            </el-card>

            <div v-if="updatedAt" class="edm-settings__hint">最近更新：{{ formatDateTime(updatedAt) }}</div>
          </template>
        </EditLayout>
      </el-form>
      <EditActionBar v-if="canUpdate">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.edm-settings { max-width: 720px; margin: 0 auto; min-width: 0; }
.edm-settings__block { margin-bottom: 16px; }
.edm-settings__hint { margin: 6px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.edm-settings__status { margin: 0 0 12px; }
</style>
