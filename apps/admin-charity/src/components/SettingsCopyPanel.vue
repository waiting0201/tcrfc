<script setup lang="ts">
/**
 * 站台設定：前台文案（中英）、俱樂部官網網址、單筆金額預設範圍、徵信名單整站開關。
 * 儲存是「局部更新」：只送出有改動的欄位，沒動的欄位完全不送（後端視為不變）；
 * 文案改成空白代表清空，前台會回到預設文字。
 * 版面依 docs/21 §3.3a 與 docs/22 §3.10：面板放在頁籤內，所以單欄一張卡、卡內分段，語言分頁用 bare 版
 * （頁籤內容區是 overflow:hidden，sticky 會失效），操作列用 inline 版。欄位鍵與請求屬性名一致。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import ErrorState from '@/components/ErrorState.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { getSettings, updateSettings, type SiteSettings, type SiteSettingsInput } from '@/api/settings'
import { AdminApiError } from '@/api/http'
import { hasPermission } from '@/auth/session'

const canManage = computed(() => hasPermission('n7.setting.manage'))

const COPY_FIELDS = [
  { base: 'homeIntro', label: '首頁說明', hint: '顯示在首頁標題下方的一段介紹。' },
  { base: 'thankYouTemplate', label: '感謝語樣板', hint: '付款完成後結果頁上的感謝文字。' },
  { base: 'notice', label: '捐款須知', hint: '捐款須知頁的內容，包含不受理退款的說明、發票規則與款項用途說明。' },
  { base: 'privacyPolicy', label: '隱私權政策', hint: '隱私權政策頁的內容，須涵蓋捐款人個資與發票資料。前台不會自行補任何法律文字。' },
] as const

type CopyKey =
  | 'homeIntroZh' | 'homeIntroEn' | 'thankYouTemplateZh' | 'thankYouTemplateEn'
  | 'noticeZh' | 'noticeEn' | 'privacyPolicyZh' | 'privacyPolicyEn'

const formErrors = provideFormErrors()
/** 只放沒有欄位歸屬的錯誤（API 回來的訊息）；前端驗證一律進 formErrors。 */
const formError = ref<string | null>(null)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)

const copy = reactive<Record<CopyKey, string>>({
  homeIntroZh: '', homeIntroEn: '', thankYouTemplateZh: '', thankYouTemplateEn: '',
  noticeZh: '', noticeEn: '', privacyPolicyZh: '', privacyPolicyEn: '',
})
const other = reactive({ clubSiteUrl: '', defaultMinAmount: 100, defaultMaxAmount: 100000, creditListEnabled: true })
const base = reactive<{ copy: Record<CopyKey, string>; clubSiteUrl: string; defaultMinAmount: number; defaultMaxAmount: number; creditListEnabled: boolean }>({
  copy: { ...copy },
  clubSiteUrl: '',
  defaultMinAmount: 100,
  defaultMaxAmount: 100000,
  creditListEnabled: true,
})

function fill(s: SiteSettings) {
  for (const key of Object.keys(copy) as CopyKey[]) copy[key] = s[key] ?? ''
  other.clubSiteUrl = s.clubSiteUrl ?? ''
  other.defaultMinAmount = s.defaultMinAmount
  other.defaultMaxAmount = s.defaultMaxAmount
  other.creditListEnabled = s.creditListEnabled
  base.copy = { ...copy }
  base.clubSiteUrl = other.clubSiteUrl.trim()
  base.defaultMinAmount = s.defaultMinAmount
  base.defaultMaxAmount = s.defaultMaxAmount
  base.creditListEnabled = s.creditListEnabled
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    fill(await getSettings())
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '讀取設定時發生問題'
  } finally {
    loading.value = false
  }
}

onMounted(load)

const LABELS: Record<string, string> = {
  homeIntro: '首頁說明', thankYouTemplate: '感謝語樣板', notice: '捐款須知', privacyPolicy: '隱私權政策',
}

function buildChanges(): SiteSettingsInput {
  const out: SiteSettingsInput = {}
  for (const key of Object.keys(copy) as CopyKey[]) {
    if (copy[key] !== base.copy[key]) out[key] = copy[key]
  }
  if (other.clubSiteUrl.trim() !== base.clubSiteUrl) out.clubSiteUrl = other.clubSiteUrl.trim()
  if (other.defaultMinAmount !== base.defaultMinAmount) out.defaultMinAmount = other.defaultMinAmount
  if (other.defaultMaxAmount !== base.defaultMaxAmount) out.defaultMaxAmount = other.defaultMaxAmount
  if (other.creditListEnabled !== base.creditListEnabled) out.creditListEnabled = other.creditListEnabled
  return out
}

const dirty = computed(() => Object.keys(buildChanges()).length > 0)

/** 讓人知道「哪些項目還沒存」，中英文版算同一項。 */
const dirtyLabels = computed(() => {
  const labels = Object.keys(buildChanges()).map((k) => {
    if (k === 'clubSiteUrl') return '俱樂部官網網址'
    if (k === 'creditListEnabled') return '徵信名單開關'
    if (k === 'defaultMinAmount' || k === 'defaultMaxAmount') return '單筆金額範圍'
    return LABELS[k.replace(/(Zh|En)$/, '')] ?? k
  })
  return [...new Set(labels)].join('、')
})

/** 一次檢查全部（欄位鍵 → 訊息），不要遇到第一個就停。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  const url = other.clubSiteUrl.trim()
  if (url && !/^https?:\/\//i.test(url)) errors.clubSiteUrl = '請填完整網址（以 https:// 開頭）'
  if (!(other.defaultMinAmount >= 1)) errors.defaultMinAmount = '單筆金額下限至少是 1 元'
  if (!(other.defaultMaxAmount >= other.defaultMinAmount)) errors.defaultMaxAmount = '單筆金額的上限不能小於下限'
  return errors
}

async function save() {
  if (saving.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  const changes = buildChanges()
  if (Object.keys(changes).length === 0) {
    ElMessage.info('沒有任何變更')
    return
  }
  saving.value = true
  try {
    fill(await updateSettings(changes))
    ElMessage.success('已儲存站台設定')
  } catch (error) {
    // 後端有標欄位就標到欄位；對不到（或沒有欄位資訊）才退回頁首提示
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

onBeforeRouteLeave(async () => {
  if (!dirty.value || saving.value) return true
  try {
    await ElMessageBox.confirm('站台設定有尚未儲存的修改，確定要離開嗎？', '離開前確認', {
      confirmButtonText: '離開',
      cancelButtonText: '留在這裡',
      type: 'warning',
    })
    return true
  } catch {
    return false
  }
})
</script>

<template>
  <ErrorState v-if="loadError" :text="loadError" @retry="load" />
  <el-form v-else v-loading="loading" label-position="top" class="settings-copy" :disabled="!canManage" @submit.prevent>
    <el-alert v-if="!canManage" type="info" :closable="false" show-icon title="你的角色只能檢視站台設定，不能修改。" class="settings-copy__alert" />
    <el-alert v-if="formError" :title="formError" type="warning" show-icon class="settings-copy__alert" @close="formError = null" />

    <LangTabsBar variant="bare">
      <el-card shadow="never">
        <FormSection title="前台文案" hint="這些文字會顯示在慈善捐款網站上。英文版沒填時，前台會改顯示中文並提醒訪客「本頁尚無此語系版本」。">
          <BilingualTextareaField
            v-for="f in COPY_FIELDS"
            :key="f.base"
            :field="f.base"
            :label="f.label"
            :hint="f.hint"
            :rows="f.base === 'privacyPolicy' || f.base === 'notice' ? 8 : 3"
            :zh="copy[`${f.base}Zh` as CopyKey]"
            :en="copy[`${f.base}En` as CopyKey]"
            @update:zh="copy[`${f.base}Zh` as CopyKey] = $event"
            @update:en="copy[`${f.base}En` as CopyKey] = $event"
          />
        </FormSection>

        <FormSection title="連結與金額">
          <FormField field="clubSiteUrl" label="俱樂部官網網址">
            <el-input v-model="other.clubSiteUrl" placeholder="https://" inputmode="url" />
            <div class="settings-copy__field-hint">成果回顧頁會用這個網址導回俱樂部官網；留空代表不顯示導回連結。請填完整網址（以 https:// 開頭）。</div>
          </FormField>
          <div class="settings-copy__pair">
            <FormField field="defaultMinAmount" label="單筆金額下限（元）">
              <el-input-number v-model="other.defaultMinAmount" :min="1" :max="10000000" :step="100" style="width: 100%" />
            </FormField>
            <FormField field="defaultMaxAmount" label="單筆金額上限（元）">
              <el-input-number v-model="other.defaultMaxAmount" :min="1" :max="10000000" :step="1000" style="width: 100%" />
            </FormField>
          </div>
          <div class="settings-copy__field-hint settings-copy__field-hint--block">這是全站預設的單筆金額範圍；個別項目可以在項目設定裡另外指定自己的範圍。</div>
        </FormSection>

        <FormSection title="捐款徵信名單" hint="個別捐款可以到「捐款紀錄」的詳情裡設為不列入名單。">
          <FormField field="creditListEnabled">
            <el-switch v-model="other.creditListEnabled" inline-prompt active-text="開放" inactive-text="關閉" aria-label="捐款徵信名單整站開關" />
            <span class="settings-copy__switch-note">
              {{ other.creditListEnabled ? '已開放：前台公開選擇具名捐款者的姓名（不顯示金額）。' : '已關閉：前台徵信名單頁顯示「目前未開放」，網站頁尾也不再放入口。' }}
            </span>
          </FormField>
        </FormSection>
      </el-card>
    </LangTabsBar>

    <EditActionBar v-if="canManage" variant="inline">
      <template #status>
        <FormErrorStatus />
        <span v-if="dirty && formErrors.count.value === 0" class="settings-copy__dirty">有尚未儲存的修改：{{ dirtyLabels }}</span>
      </template>
      <el-button type="primary" :loading="saving" :disabled="!dirty" @click="save">儲存</el-button>
    </EditActionBar>
  </el-form>
</template>

<style scoped>
.settings-copy__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.settings-copy__pair {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 0 var(--charity-admin-space-4);
}

@media (max-width: 767px) {
  .settings-copy__pair {
    grid-template-columns: minmax(0, 1fr);
  }
}

.settings-copy__field-hint {
  width: 100%;
  margin-top: var(--charity-admin-space-1);
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.6;
}

.settings-copy__field-hint--block {
  margin: 0;
}

.settings-copy__switch-note {
  margin-left: var(--charity-admin-space-3);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.settings-copy__dirty {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}
</style>
