<script setup lang="ts">
/**
 * I 全域設定（規劃書 §4.9「全域設定」；apps/api/README.md「H 批」§5）：標誌與品牌色、三份政策頁
 * （Cookie 政策／隱私權政策／會員條款，**純文字**、空行分段，前台以文字節點輸出）、維護模式。
 *
 * - `PUT` 是 multipart：`payload` ＋ 選填檔案 `logoLight`／`logoDark`／`favicon`。非圖片欄位**整份取代**；
 *   圖片沒選檔且沒勾移除＝維持原圖。選了新檔就不送移除（兩者同時給後端回 400）。
 * - 🔴 維護模式**只是旗標與訊息**，不會自動攔截其他公開端點；前台依設定顯示維護頁。切換會留下系統紀錄。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import LangPane from '@/components/LangPane.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  getAdminGlobalSettings,
  updateAdminGlobalSettings,
  type AdminGlobalSettingsDto,
  type AdminPolicy,
} from '@/api/adminSiteSettings'
import { formatDateTime } from '@/utils/dateTime'

const canUpdate = usePermission('site.global.update')
const club = computed(() => activeClubId.value)

const POLICY_MAX = 50000
const MAINTENANCE_MAX = 500
const COLOR_RE = /^#[0-9A-Fa-f]{6}$/

const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
/** 政策與條款的摺疊面板：欄位錯誤定位時要先把對應那一則打開（FormField 的 reveal）。 */
const openPolicies = ref<string[]>([])
function revealPolicy(key: string) {
  if (!openPolicies.value.includes(key)) openPolicies.value = [...openPolicies.value, key]
}

const form = reactive({
  brandColor: '',
  brandSecondaryColor: '',
  cookieZh: '',
  cookieEn: '',
  privacyZh: '',
  privacyEn: '',
  termsZh: '',
  termsEn: '',
  maintenanceEnabled: false,
  maintenanceMessageZh: '',
  maintenanceMessageEn: '',
})
const baselineJson = ref('')
const policyUpdatedAt = reactive({ cookie: null as string | null, privacy: null as string | null, 'member-terms': null as string | null })
const maintenanceUpdatedAt = ref<string | null>(null)
/** 載入當下伺服器端的維護狀態（用來決定切換時要不要確認）。 */
const loadedMaintenance = ref(false)

// 三張圖：各自的新檔案、移除意圖、現有網址
interface ImageSlot {
  file: File | null
  remove: boolean
  url: string | null
  has: boolean
}
const logoLight = reactive<ImageSlot>({ file: null, remove: false, url: null, has: false })
const logoDark = reactive<ImageSlot>({ file: null, remove: false, url: null, has: false })
const favicon = reactive<ImageSlot>({ file: null, remove: false, url: null, has: false })
// 選檔或移除圖片就清掉該欄位的錯誤（上傳元件不會冒泡 DOM 事件）
watch(() => [logoLight.file, logoLight.remove], () => formErrors.clear('logoLight'))
watch(() => [logoDark.file, logoDark.remove], () => formErrors.clear('logoDark'))
watch(() => [favicon.file, favicon.remove], () => formErrors.clear('favicon'))

function resetSlot(slot: ImageSlot, url: string | null | undefined) {
  slot.file = null
  slot.remove = false
  slot.url = url ?? null
  slot.has = !!url
}

function policyOf(policies: AdminPolicy[], code: string): AdminPolicy | undefined {
  return policies.find((p) => p.code === code)
}

function apply(d: AdminGlobalSettingsDto) {
  form.brandColor = d.brand.brandColor ?? ''
  form.brandSecondaryColor = d.brand.brandSecondaryColor ?? ''
  const cookie = policyOf(d.policies, 'cookie')
  const privacy = policyOf(d.policies, 'privacy')
  const terms = policyOf(d.policies, 'member-terms')
  form.cookieZh = cookie?.bodyZh ?? ''
  form.cookieEn = cookie?.bodyEn ?? ''
  form.privacyZh = privacy?.bodyZh ?? ''
  form.privacyEn = privacy?.bodyEn ?? ''
  form.termsZh = terms?.bodyZh ?? ''
  form.termsEn = terms?.bodyEn ?? ''
  policyUpdatedAt.cookie = cookie?.updatedAt ?? null
  policyUpdatedAt.privacy = privacy?.updatedAt ?? null
  policyUpdatedAt['member-terms'] = terms?.updatedAt ?? null
  form.maintenanceEnabled = d.maintenance.enabled
  form.maintenanceMessageZh = d.maintenance.messageZh ?? ''
  form.maintenanceMessageEn = d.maintenance.messageEn ?? ''
  maintenanceUpdatedAt.value = d.maintenance.updatedAt ?? null
  loadedMaintenance.value = d.maintenance.enabled
  resetSlot(logoLight, d.brand.logoLightUrl)
  resetSlot(logoDark, d.brand.logoDarkUrl)
  resetSlot(favicon, d.brand.faviconUrl)
  baselineJson.value = JSON.stringify(form)
}

async function load() {
  loadState.value = 'loading'
  try {
    apply(await getAdminGlobalSettings(club.value))
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}
onMounted(load)
watch(club, load)

const isDirty = computed(
  () =>
    loadState.value === 'ready' &&
    (JSON.stringify(form) !== baselineJson.value ||
      [logoLight, logoDark, favicon].some((s) => !!s.file || s.remove)),
)
useUnsavedChanges(isDirty)

/** 色彩挑選器清除時會送出 null，統一當成空字串。 */
const str = (v: unknown): string => (typeof v === 'string' ? v : '')

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (str(form.brandColor).trim() && !COLOR_RE.test(str(form.brandColor).trim())) {
    errors.brandColor = '主要品牌色格式不正確，請填寫像 #1A3C6E 這樣以 # 開頭的六碼色碼，或留空。'
  }
  if (str(form.brandSecondaryColor).trim() && !COLOR_RE.test(str(form.brandSecondaryColor).trim())) {
    errors.brandSecondaryColor = '輔助品牌色格式不正確，請填寫像 #1A3C6E 這樣以 # 開頭的六碼色碼，或留空。'
  }
  for (const p of POLICY_BLOCKS) {
    if (form[p.zh].length > POLICY_MAX) errors[p.zh] = `${p.title}（中文）最多 ${POLICY_MAX.toLocaleString()} 字。`
    if (form[p.en].length > POLICY_MAX) errors[p.en] = `${p.title}（英文）最多 ${POLICY_MAX.toLocaleString()} 字。`
  }
  if (form.maintenanceEnabled && !form.maintenanceMessageZh.trim()) {
    errors.maintenanceMessageZh = '開啟維護模式前，請先填寫繁體中文的維護訊息，訪客才知道發生什麼事。'
  }
  return errors
}

function slotFile(slot: ImageSlot): File | null {
  return slot.file
}
function slotRemove(slot: ImageSlot): boolean {
  return !slot.file && slot.remove
}

async function handleSave() {
  if (!canUpdate.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  // 切換維護模式是會立刻影響訪客的動作，二次確認
  if (form.maintenanceEnabled !== loadedMaintenance.value) {
    try {
      await ElMessageBox.confirm(
        form.maintenanceEnabled
          ? '開啟後，訪客會看到維護頁而不是網站內容（會員卡驗證頁等不能中斷的頁面除外）。確定要開啟維護模式嗎？'
          : '確定要關閉維護模式，讓網站恢復正常嗎？',
        form.maintenanceEnabled ? '開啟維護模式' : '關閉維護模式',
        { confirmButtonText: '確定', cancelButtonText: '取消', type: 'warning' },
      )
    } catch {
      return
    }
  }
  saving.value = true
  formError.value = null
  try {
    const saved = await updateAdminGlobalSettings(
      club.value,
      {
        brandColor: str(form.brandColor).trim() || null,
        brandSecondaryColor: str(form.brandSecondaryColor).trim() || null,
        removeLogoLight: slotRemove(logoLight),
        removeLogoDark: slotRemove(logoDark),
        removeFavicon: slotRemove(favicon),
        cookiePolicy: { bodyZh: form.cookieZh, bodyEn: form.cookieEn },
        privacyPolicy: { bodyZh: form.privacyZh, bodyEn: form.privacyEn },
        memberTerms: { bodyZh: form.termsZh, bodyEn: form.termsEn },
        maintenanceEnabled: form.maintenanceEnabled,
        maintenanceMessageZh: form.maintenanceMessageZh,
        maintenanceMessageEn: form.maintenanceMessageEn,
      },
      { logoLight: slotFile(logoLight), logoDark: slotFile(logoDark), favicon: slotFile(favicon) },
    )
    apply(saved)
    ElMessage.success('已儲存')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

const POLICY_BLOCKS = [
  { key: 'cookie', title: 'Cookie 政策', zh: 'cookieZh', en: 'cookieEn' },
  { key: 'privacy', title: '隱私權政策', zh: 'privacyZh', en: 'privacyEn' },
  { key: 'member-terms', title: '會員條款', zh: 'termsZh', en: 'termsEn' },
] as const
</script>

<template>
  <div class="global-settings">
    <PageHeader title="全域設定">
      <template #meta>
        <FrontendUnitBanner module-code="I" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="global-settings__block" @close="formError = null" />
      <el-alert v-if="!canUpdate" title="你的帳號只能檢視全域設定，不能修改。" type="info" show-icon :closable="false" class="global-settings__block" />

      <el-form label-position="top" :disabled="!canUpdate">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never" header="品牌色">
                <el-row :gutter="16">
                  <el-col :xs="24" :sm="12">
                    <FormField field="brandColor" label="主要品牌色">
                      <div class="global-settings__color">
                        <el-color-picker v-model="form.brandColor" :predefine="[]" aria-label="主要品牌色挑選器" @change="formErrors.clear('brandColor')" />
                        <el-input v-model="form.brandColor" maxlength="7" placeholder="#1A3C6E，留空使用預設" clearable />
                      </div>
                    </FormField>
                  </el-col>
                  <el-col :xs="24" :sm="12">
                    <FormField field="brandSecondaryColor" label="輔助品牌色">
                      <div class="global-settings__color">
                        <el-color-picker v-model="form.brandSecondaryColor" aria-label="輔助品牌色挑選器" @change="formErrors.clear('brandSecondaryColor')" />
                        <el-input v-model="form.brandSecondaryColor" maxlength="7" placeholder="#RRGGBB，留空使用預設" clearable />
                      </div>
                    </FormField>
                  </el-col>
                </el-row>
                <p class="global-settings__hint">請確認色碼與背景的對比足夠，文字才讀得清楚。</p>
              </el-card>

              <el-card shadow="never" header="政策與條款">
                <p class="global-settings__hint">內容是純文字，用空白行分段，不支援粗體、連結等格式。每則最多 {{ POLICY_MAX.toLocaleString() }} 字；英文留空時，英文版前台會顯示中文內容。</p>
                <el-collapse v-model="openPolicies">
                  <el-collapse-item v-for="p in POLICY_BLOCKS" :key="p.key" :name="p.key">
                    <template #title>
                      {{ p.title }}
                      <el-tag v-if="!form[p.zh].trim()" size="small" type="info" class="global-settings__tag">尚未填寫</el-tag>
                      <el-tag v-else-if="!form[p.en].trim()" size="small" type="warning" class="global-settings__tag">英文尚未填寫</el-tag>
                    </template>
                    <LangPane lang="zh" :field="p.zh">
                      <FormField :field="p.zh" :label="`${p.title}（中文）`" lang="zh" :reveal="() => revealPolicy(p.key)">
                        <el-input v-model="form[p.zh]" type="textarea" :rows="10" :maxlength="POLICY_MAX" show-word-limit />
                      </FormField>
                    </LangPane>
                    <LangPane lang="en" :field="p.en" :untranslated="!!form[p.zh].trim() && !form[p.en].trim()">
                      <FormField :field="p.en" :label="`${p.title}（英文）`" lang="en" :reveal="() => revealPolicy(p.key)">
                        <el-input v-model="form[p.en]" type="textarea" :rows="10" :maxlength="POLICY_MAX" show-word-limit />
                      </FormField>
                    </LangPane>
                    <div v-if="policyUpdatedAt[p.key]" class="global-settings__hint">最近更新：{{ formatDateTime(policyUpdatedAt[p.key]) }}</div>
                  </el-collapse-item>
                </el-collapse>
              </el-card>

              <el-card shadow="never" header="維護模式">
                <el-alert
                  title="開啟後，前台會改顯示維護頁與下方的訊息。切換維護模式會留下系統紀錄。會員卡驗證頁等不能中斷的頁面不受影響。"
                  type="warning"
                  show-icon
                  :closable="false"
                  class="global-settings__block"
                />
                <el-form-item label="啟用維護模式">
                  <el-switch v-model="form.maintenanceEnabled" @change="formErrors.clear('maintenanceMessageZh')" />
                  <el-tag v-if="loadedMaintenance" type="danger" size="small" class="global-settings__tag">目前正在維護中</el-tag>
                </el-form-item>
                <BilingualTextareaField
                  field="maintenanceMessage"
                  label="維護訊息"
                  :zh="form.maintenanceMessageZh"
                  :en="form.maintenanceMessageEn"
                  :maxlength="MAINTENANCE_MAX"
                  placeholder="例如：網站維護中，預計今晚 22:00 恢復，造成不便敬請見諒。"
                  @update:zh="(v) => (form.maintenanceMessageZh = v)"
                  @update:en="(v) => (form.maintenanceMessageEn = v)"
                />
                <div v-if="maintenanceUpdatedAt" class="global-settings__hint">最近更新：{{ formatDateTime(maintenanceUpdatedAt) }}</div>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="標誌與網站圖示">
                <p class="global-settings__hint">此處維護目前選取俱樂部的標誌。選好檔案後要按「儲存」才會上傳。</p>
                <FormField field="logoLight" label="標誌（淺色背景用）">
                  <ImageUploader v-model:file="logoLight.file" v-model:remove-cover="logoLight.remove" variant="logo" :min-width="0" :min-height="0" :has-existing-image="logoLight.has" :existing-preview-url="logoLight.url" :disabled="saving || !canUpdate" />
                </FormField>
                <FormField field="logoDark" label="標誌（深色背景用）">
                  <ImageUploader v-model:file="logoDark.file" v-model:remove-cover="logoDark.remove" variant="logo" :min-width="0" :min-height="0" :has-existing-image="logoDark.has" :existing-preview-url="logoDark.url" :disabled="saving || !canUpdate" />
                </FormField>
                <FormField field="favicon" label="瀏覽器分頁小圖示">
                  <ImageUploader v-model:file="favicon.file" v-model:remove-cover="favicon.remove" variant="logo" :min-width="0" :min-height="0" :has-existing-image="favicon.has" :existing-preview-url="favicon.url" :disabled="saving || !canUpdate" />
                </FormField>
              </el-card>
            </template>
          </EditLayout>
        </LangTabsBar>
      </el-form>
      <EditActionBar v-if="canUpdate">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.global-settings { max-width: 1200px; margin: 0 auto; min-width: 0; }
.global-settings__block { margin-bottom: 16px; }
.global-settings__hint { margin: 0 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.global-settings__color { display: flex; align-items: center; gap: 12px; width: 100%; }
.global-settings__tag { margin-left: 8px; }
</style>
