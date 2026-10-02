<script setup lang="ts">
/**
 * 系統信樣板：感謝信、憑證通知、憑證開立失敗通知、退款通知（四封）。
 * 內文可以插入「代入欄位」（訂單編號、金額等），寄出時系統會換成實際內容；按下欄位按鈕會插入到游標位置。
 * 更新後下一封信立即套用。英文版的主旨與本文要同時填或同時留空（同時留空代表沒有英文版）；
 * 目前系統信一律寄出中文版（捐款單沒有記錄捐款人的語系），英文版先存起來供日後使用。
 */
import { computed, nextTick, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import ErrorState from '@/components/ErrorState.vue'
import { listEmailTemplates, updateEmailTemplate, type EmailTemplate } from '@/api/settings'
import { AdminApiError } from '@/api/http'
import { hasPermission } from '@/auth/session'

interface Editable {
  template: EmailTemplate
  form: { isActive: boolean; subjectZh: string; bodyZh: string; subjectEn: string; bodyEn: string }
  base: { isActive: boolean; subjectZh: string; bodyZh: string; subjectEn: string; bodyEn: string }
  saving: boolean
}

type FieldKey = 'subjectZh' | 'bodyZh' | 'subjectEn' | 'bodyEn'

const canManage = computed(() => hasPermission('n7.setting.manage'))
const loading = ref(false)
const loadError = ref('')
const rows = ref<Editable[]>([])
const activeCode = ref('')

function toEditable(t: EmailTemplate): Editable {
  const form = {
    isActive: t.isActive,
    subjectZh: t.zh?.subject ?? '',
    bodyZh: t.zh?.body ?? '',
    subjectEn: t.en?.subject ?? '',
    bodyEn: t.en?.body ?? '',
  }
  return { template: t, form, base: { ...form }, saving: false }
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    rows.value = (await listEmailTemplates()).map(toEditable)
    if (!rows.value.some((r) => r.template.code === activeCode.value)) activeCode.value = rows.value[0]?.template.code ?? ''
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '讀取系統信樣板時發生問題'
  } finally {
    loading.value = false
  }
}

onMounted(load)

const isDirty = (r: Editable) => (Object.keys(r.form) as (keyof Editable['form'])[]).some((k) => r.form[k] !== r.base[k])

/** 英文的主旨與本文要同時填或同時空。 */
function enHalfFilled(r: Editable): boolean {
  return Boolean(r.form.subjectEn.trim()) !== Boolean(r.form.bodyEn.trim())
}

async function save(r: Editable) {
  if (r.saving) return
  if (!r.form.subjectZh.trim() || !r.form.bodyZh.trim()) {
    ElMessage.warning('中文版的主旨與本文都要填寫')
    return
  }
  if (enHalfFilled(r)) {
    ElMessage.warning('英文版的主旨與本文要同時填寫，或同時留空')
    return
  }
  r.saving = true
  try {
    const updated = await updateEmailTemplate(r.template.code, {
      isActive: r.form.isActive,
      subjectZh: r.form.subjectZh.trim(),
      bodyZh: r.form.bodyZh,
      subjectEn: r.form.subjectEn.trim(),
      bodyEn: r.form.bodyEn,
    })
    const fresh = toEditable(updated)
    r.template = fresh.template
    r.form = fresh.form
    r.base = fresh.base
    ElMessage.success(`已儲存「${updated.label}」，下一封信起套用`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試')
  } finally {
    r.saving = false
  }
}

// ── 在游標位置插入代入欄位 ─────────────────────────────────────────────
// 按按鈕時輸入框會先失去焦點，所以在 blur 當下記住是哪一格、游標在哪。
let lastField: { el: HTMLInputElement | HTMLTextAreaElement; row: Editable; key: FieldKey } | null = null

function remember(event: Event, row: Editable, key: FieldKey) {
  lastField = { el: event.target as HTMLInputElement | HTMLTextAreaElement, row, key }
}

async function insertToken(row: Editable, token: string) {
  const target = lastField && lastField.row === row ? lastField : null
  const key: FieldKey = target?.key ?? 'bodyZh'
  const current = row.form[key]
  const start = target?.el.selectionStart ?? current.length
  const end = target?.el.selectionEnd ?? current.length
  row.form[key] = current.slice(0, start) + token + current.slice(end)
  await nextTick()
  if (target?.el) {
    target.el.focus()
    const caret = start + token.length
    target.el.setSelectionRange(caret, caret)
  }
}
</script>

<template>
  <ErrorState v-if="loadError" :text="loadError" @retry="load" />
  <div v-else v-loading="loading" class="email-templates">
    <el-alert v-if="!canManage" type="info" :closable="false" show-icon title="你的角色只能檢視系統信樣板，不能修改。" class="email-templates__alert" />
    <el-tabs v-model="activeCode" tab-position="top">
      <el-tab-pane v-for="r in rows" :key="r.template.code" :name="r.template.code" :label="r.template.label">
        <el-form label-position="top" :disabled="!canManage" class="email-templates__form" @submit.prevent>
          <el-form-item>
            <el-switch v-model="r.form.isActive" inline-prompt active-text="寄送" inactive-text="停寄" :aria-label="`是否寄送「${r.template.label}」`" />
            <span class="email-templates__note">{{ r.form.isActive ? '捐款流程走到這一步時會寄出這封信。' : '已停止寄送這封信。' }}</span>
          </el-form-item>

          <div v-if="r.template.tokens.length > 0" class="email-templates__tokens">
            <span class="email-templates__tokens-label">插入欄位（寄出時自動換成實際內容）：</span>
            <el-button v-for="tk in r.template.tokens" :key="tk.token" size="small" :disabled="!canManage" @mousedown.prevent @click="insertToken(r, tk.token)">
              {{ tk.description || tk.token }}
            </el-button>
          </div>

          <h4 class="email-templates__lang">中文版</h4>
          <el-form-item label="主旨" required>
            <el-input v-model="r.form.subjectZh" maxlength="200" @blur="remember($event, r, 'subjectZh')" />
          </el-form-item>
          <el-form-item label="本文" required>
            <el-input v-model="r.form.bodyZh" type="textarea" :rows="10" @blur="remember($event, r, 'bodyZh')" />
          </el-form-item>

          <h4 class="email-templates__lang">
            英文版
            <el-tag v-if="!r.form.subjectEn.trim() && !r.form.bodyEn.trim()" size="small" type="info">尚未翻譯</el-tag>
          </h4>
          <el-form-item label="主旨（英文）">
            <el-input v-model="r.form.subjectEn" maxlength="200" @blur="remember($event, r, 'subjectEn')" />
          </el-form-item>
          <el-form-item label="本文（英文）">
            <el-input v-model="r.form.bodyEn" type="textarea" :rows="10" @blur="remember($event, r, 'bodyEn')" />
          </el-form-item>
          <p v-if="enHalfFilled(r)" class="email-templates__warn">英文版的主旨與本文要同時填寫，或同時留空。</p>
          <p class="email-templates__note">目前系統信一律寄出中文版，英文版先存起來，等系統能判斷捐款人語言後才會使用。</p>

          <div v-if="canManage" class="email-templates__actions">
            <el-button type="primary" :loading="r.saving" :disabled="!isDirty(r)" @click="save(r)">儲存這封信</el-button>
          </div>
        </el-form>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.email-templates__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.email-templates__form {
  max-width: 760px;
}

.email-templates__note {
  margin: 0 0 0 var(--charity-admin-space-3);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

p.email-templates__note {
  margin: 0 0 var(--charity-admin-space-3);
}

.email-templates__warn {
  margin: 0 0 var(--charity-admin-space-3);
  font-size: 13px;
  color: var(--charity-danger-text);
}

.email-templates__tokens {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--charity-admin-space-2);
  margin-bottom: var(--charity-admin-space-3);
}

.email-templates__tokens-label {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.email-templates__lang {
  font-size: 14px;
  margin: var(--charity-admin-space-4) 0 var(--charity-admin-space-2);
}

.email-templates__actions {
  margin-top: var(--charity-admin-space-4);
  padding-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
}
</style>
