<script setup lang="ts">
/**
 * 單一封系統信樣板的編輯表單（EmailTemplatesPanel 每個頁籤一份）。
 * 內文可以插入「代入欄位」（訂單編號、金額等），寄出時系統會換成實際內容；按下欄位按鈕會插入到游標位置。
 * 英文版的主旨與本文要同時填或同時留空（同時留空代表沒有英文版）。
 *
 * 獨立成元件的原因：每封信要有自己的一組欄位錯誤與語言分頁（欄位鍵 subjectZh／bodyZh… 在每封信裡都一樣，
 * 放在同一個頁面層會互相覆蓋）。版面規則見 docs/22 §3.10：面板內單欄一張卡、語言分頁用 bare 版、操作列用 inline 版。
 */
import { computed, nextTick, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { updateEmailTemplate, type EmailTemplate } from '@/api/settings'
import { AdminApiError } from '@/api/http'

type FieldKey = 'subjectZh' | 'bodyZh' | 'subjectEn' | 'bodyEn'
interface FormState { isActive: boolean; subjectZh: string; bodyZh: string; subjectEn: string; bodyEn: string }

const props = defineProps<{ template: EmailTemplate; canManage: boolean }>()

const formErrors = provideFormErrors()
/** 只放沒有欄位歸屬的錯誤（API 回來的訊息）；前端驗證一律進 formErrors。 */
const formError = ref<string | null>(null)
const saving = ref(false)
const root = ref<HTMLElement | null>(null)

const current = ref<EmailTemplate>(props.template)

function toState(t: EmailTemplate): FormState {
  return { isActive: t.isActive, subjectZh: t.zh?.subject ?? '', bodyZh: t.zh?.body ?? '', subjectEn: t.en?.subject ?? '', bodyEn: t.en?.body ?? '' }
}

const form = reactive<FormState>(toState(props.template))
const base = ref<FormState>(toState(props.template))

const dirty = computed(() => (Object.keys(form) as (keyof FormState)[]).some((k) => form[k] !== base.value[k]))

/** 一次檢查全部（欄位鍵 → 訊息），不要遇到第一個就停。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.subjectZh.trim()) errors.subjectZh = '請填寫中文版主旨'
  if (!form.bodyZh.trim()) errors.bodyZh = '請填寫中文版本文'
  // 英文的主旨與本文要同時填或同時空：標在「空的那一邊」
  const hasSubjectEn = !!form.subjectEn.trim()
  const hasBodyEn = !!form.bodyEn.trim()
  if (hasSubjectEn && !hasBodyEn) errors.bodyEn = '英文版的主旨與本文要同時填寫，或同時留空'
  if (!hasSubjectEn && hasBodyEn) errors.subjectEn = '英文版的主旨與本文要同時填寫，或同時留空'
  return errors
}

async function save() {
  if (saving.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const updated = await updateEmailTemplate(current.value.code, {
      isActive: form.isActive,
      subjectZh: form.subjectZh.trim(),
      bodyZh: form.bodyZh,
      subjectEn: form.subjectEn.trim(),
      bodyEn: form.bodyEn,
    })
    current.value = updated
    Object.assign(form, toState(updated))
    base.value = toState(updated)
    ElMessage.success(`已儲存「${updated.label}」，下一封信起套用`)
  } catch (error) {
    // 後端有標欄位就標到欄位；對不到（或沒有欄位資訊）才退回頁首提示
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

// ── 在游標位置插入代入欄位 ─────────────────────────────────────────────
// 按按鈕時輸入框不能失焦（按鈕 mousedown.prevent），所以在 focusin 當下記住是哪一格，
// 欄位鍵從外層 FormField 的 data-field 讀（雙語欄位的鍵就是 subjectZh／bodyZh…）。
const KEYS: FieldKey[] = ['subjectZh', 'bodyZh', 'subjectEn', 'bodyEn']
let lastField: { el: HTMLInputElement | HTMLTextAreaElement; key: FieldKey } | null = null

function remember(event: FocusEvent) {
  const el = event.target as HTMLElement
  if (!(el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement)) return
  const key = el.closest('[data-field]')?.getAttribute('data-field') as FieldKey | null
  if (key && KEYS.includes(key)) lastField = { el, key }
}

/** 沒有記住的游標位置（或那一格目前被語言分頁藏起來）時，改插到目前顯示語言的本文。 */
function visibleBodyKey(): FieldKey {
  for (const key of ['bodyZh', 'bodyEn'] as const) {
    const wrapper = root.value?.querySelector<HTMLElement>(`[data-field="${key}"]`)
    if (wrapper && wrapper.offsetParent !== null) return key
  }
  return 'bodyZh'
}

async function insertToken(token: string) {
  const target = lastField && lastField.el.isConnected && lastField.el.offsetParent !== null ? lastField : null
  const key: FieldKey = target?.key ?? visibleBodyKey()
  const text = form[key]
  const start = target?.el.selectionStart ?? text.length
  const end = target?.el.selectionEnd ?? text.length
  form[key] = text.slice(0, start) + token + text.slice(end)
  formErrors.clear(key)
  await nextTick()
  if (target?.el) {
    target.el.focus()
    const caret = start + token.length
    target.el.setSelectionRange(caret, caret)
  }
}
</script>

<template>
  <el-form label-position="top" :disabled="!canManage" class="email-template-form" @submit.prevent>
    <el-alert v-if="formError" :title="formError" type="warning" show-icon class="email-template-form__alert" @close="formError = null" />

    <div ref="root" @focusin="remember">
      <LangTabsBar variant="bare" :label="current.label">
        <el-card shadow="never">
          <FormSection title="寄送設定">
            <FormField field="isActive">
              <el-switch v-model="form.isActive" inline-prompt active-text="寄送" inactive-text="停寄" :aria-label="`是否寄送「${current.label}」`" />
              <span class="email-template-form__note">{{ form.isActive ? '捐款流程走到這一步時會寄出這封信。' : '已停止寄送這封信。' }}</span>
            </FormField>
          </FormSection>

          <FormSection title="信件內容">
            <div v-if="current.tokens.length > 0" class="email-template-form__tokens">
              <span class="email-template-form__tokens-label">插入欄位（寄出時自動換成實際內容）：</span>
              <el-button v-for="tk in current.tokens" :key="tk.token" size="small" :disabled="!canManage" @mousedown.prevent @click="insertToken(tk.token)">
                {{ tk.description || tk.token }}
              </el-button>
            </div>
            <BilingualShortField field="subject" label="主旨" required :maxlength="200" :zh="form.subjectZh" :en="form.subjectEn" @update:zh="form.subjectZh = $event" @update:en="form.subjectEn = $event" />
            <BilingualTextareaField field="body" label="本文" required :rows="10" :zh="form.bodyZh" :en="form.bodyEn" @update:zh="form.bodyZh = $event" @update:en="form.bodyEn = $event" />
            <p class="email-template-form__note email-template-form__note--block">目前系統信一律寄出中文版，英文版先存起來，等系統能判斷捐款人語言後才會使用。英文版的主旨與本文要同時填寫，或同時留空。</p>
          </FormSection>
        </el-card>
      </LangTabsBar>
    </div>

    <EditActionBar v-if="canManage" variant="inline">
      <template #status><FormErrorStatus /></template>
      <el-button type="primary" :loading="saving" :disabled="!dirty" @click="save">儲存這封信</el-button>
    </EditActionBar>
  </el-form>
</template>

<style scoped>
.email-template-form__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.email-template-form__note {
  margin: 0 0 0 var(--charity-admin-space-3);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.email-template-form__note--block {
  margin: 0;
}

.email-template-form__tokens {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--charity-admin-space-2);
  margin-bottom: var(--charity-admin-space-3);
}

.email-template-form__tokens-label {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}
</style>
