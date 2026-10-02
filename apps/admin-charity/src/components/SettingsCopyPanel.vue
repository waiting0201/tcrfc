<script setup lang="ts">
/**
 * 站台設定：前台文案（中英）、俱樂部官網網址、單筆金額預設範圍、徵信名單整站開關。
 * 儲存是「局部更新」：只送出有改動的欄位，沒動的欄位完全不送（後端視為不變）；
 * 文案改成空白代表清空，前台會回到預設文字。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import ErrorState from '@/components/ErrorState.vue'
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

async function save() {
  if (saving.value) return
  if (other.defaultMinAmount < 1 || other.defaultMaxAmount < other.defaultMinAmount) {
    ElMessage.warning('單筆金額的上限不能小於下限，下限至少是 1 元')
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
    ElMessage.error(error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試')
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

    <h3 class="settings-copy__title">前台文案</h3>
    <p class="settings-copy__hint">這些文字會顯示在慈善捐款網站上。英文版沒填時，前台會改顯示中文並提醒訪客「本頁尚無此語系版本」。</p>
    <el-tabs>
      <el-tab-pane label="中文">
        <el-form-item v-for="f in COPY_FIELDS" :key="f.base" :label="f.label">
          <el-input v-model="copy[`${f.base}Zh` as CopyKey]" type="textarea" :rows="f.base === 'privacyPolicy' || f.base === 'notice' ? 8 : 3" />
          <div class="settings-copy__field-hint">{{ f.hint }}</div>
        </el-form-item>
      </el-tab-pane>
      <el-tab-pane>
        <template #label>英文 <el-tag v-if="!COPY_FIELDS.every((f) => copy[`${f.base}En` as CopyKey].trim())" size="small" type="info">尚有未翻譯</el-tag></template>
        <el-form-item v-for="f in COPY_FIELDS" :key="f.base" :label="`${f.label}（英文）`">
          <el-input v-model="copy[`${f.base}En` as CopyKey]" type="textarea" :rows="f.base === 'privacyPolicy' || f.base === 'notice' ? 8 : 3" />
          <el-tag v-if="!copy[`${f.base}En` as CopyKey].trim()" size="small" type="info" class="settings-copy__untranslated">尚未翻譯</el-tag>
        </el-form-item>
      </el-tab-pane>
    </el-tabs>

    <h3 class="settings-copy__title">連結與金額</h3>
    <el-form-item label="俱樂部官網網址">
      <el-input v-model="other.clubSiteUrl" placeholder="https://" inputmode="url" />
      <div class="settings-copy__field-hint">成果回顧頁會用這個網址導回俱樂部官網；留空代表不顯示導回連結。請填完整網址（以 https:// 開頭）。</div>
    </el-form-item>
    <div class="settings-copy__pair">
      <el-form-item label="單筆金額下限（元）">
        <el-input-number v-model="other.defaultMinAmount" :min="1" :max="10000000" :step="100" style="width: 100%" />
      </el-form-item>
      <el-form-item label="單筆金額上限（元）">
        <el-input-number v-model="other.defaultMaxAmount" :min="1" :max="10000000" :step="1000" style="width: 100%" />
      </el-form-item>
    </div>
    <div class="settings-copy__field-hint settings-copy__field-hint--block">這是全站預設的單筆金額範圍；個別項目可以在項目設定裡另外指定自己的範圍。</div>

    <h3 class="settings-copy__title">捐款徵信名單</h3>
    <el-form-item>
      <el-switch v-model="other.creditListEnabled" inline-prompt active-text="開放" inactive-text="關閉" aria-label="捐款徵信名單整站開關" />
      <span class="settings-copy__switch-note">
        {{ other.creditListEnabled ? '已開放：前台公開選擇具名捐款者的姓名（不顯示金額）。' : '已關閉：前台徵信名單頁顯示「目前未開放」，網站頁尾也不再放入口。' }}
      </span>
    </el-form-item>
    <p class="settings-copy__hint">個別捐款可以到「捐款紀錄」的詳情裡設為不列入名單。</p>

    <div v-if="canManage" class="settings-copy__actions">
      <el-button type="primary" :loading="saving" :disabled="!dirty" @click="save">儲存</el-button>
      <span v-if="dirty" class="settings-copy__hint settings-copy__hint--inline">有尚未儲存的修改：{{ dirtyLabels }}</span>
    </div>
  </el-form>
</template>

<style scoped>
.settings-copy {
  max-width: 820px;
}

.settings-copy__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.settings-copy__title {
  font-size: 15px;
  margin: var(--charity-admin-space-6) 0 var(--charity-admin-space-2);
  padding-top: var(--charity-admin-space-3);
  border-top: 1px solid var(--charity-admin-border);
}

.settings-copy__title:first-of-type {
  margin-top: 0;
  padding-top: 0;
  border-top: none;
}

.settings-copy__pair {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 var(--charity-admin-space-4);
}

@media (max-width: 767px) {
  .settings-copy__pair {
    grid-template-columns: 1fr;
  }
}

.settings-copy__hint {
  margin: 0 0 var(--charity-admin-space-3);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.settings-copy__hint--inline {
  margin: 0 0 0 var(--charity-admin-space-3);
}

.settings-copy__field-hint {
  width: 100%;
  margin-top: var(--charity-admin-space-1);
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.6;
}

.settings-copy__field-hint--block {
  margin: calc(var(--charity-admin-space-2) * -1) 0 var(--charity-admin-space-3);
}

.settings-copy__untranslated {
  margin-top: var(--charity-admin-space-1);
}

.settings-copy__switch-note {
  margin-left: var(--charity-admin-space-3);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.settings-copy__actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  margin-top: var(--charity-admin-space-6);
  padding-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
}
</style>
