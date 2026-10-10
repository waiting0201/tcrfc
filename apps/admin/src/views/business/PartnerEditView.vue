<script setup lang="ts">
/** 夥伴——新增／編輯。對照 apps/api/README.md「E1a」節「E1 夥伴」。 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageAltField from '@/components/ImageAltField.vue'
import ImageSizeHint from '@/components/ImageSizeHint.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, imageIntent, nullIfBlank } from '@/api/adminCommon'
import { createPartner, getPartner, listPartnerTypes, updatePartner, type SavePartnerPayload } from '@/api/adminPartners'

const route = useRoute()
const router = useRouter()
// 🔴 必須是 computed（建立成功後 router.replace 同一元件不會重新掛載）
const isCreate = computed(() => route.name === 'partner-new')
const partnerId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('business.partner')

const form = reactive({
  partnerType: '',
  country: '',
  startOn: '',
  endOn: '',
  websiteUrl: '',
  showInFooter: false,
  showOnHome: false,
  sortOrder: 0,
  nameZh: '',
  nameEn: '',
  contentZh: '',
  contentEn: '',
  logoAltZh: '',
  logoAltEn: '',
})
const baselineJson = ref('')
const darkFile = ref<File | null>(null)
const lightFile = ref<File | null>(null)
const removeDark = ref(false)
const removeLight = ref(false)
const darkUrl = ref<string | null>(null)
const lightUrl = ref<string | null>(null)
const hasDark = ref(false)
const hasLight = ref(false)
const darkWidth = ref<number | null>(null)
const darkHeight = ref<number | null>(null)
const lightWidth = ref<number | null>(null)
const lightHeight = ref<number | null>(null)
const typeOptions = ref<string[]>([])

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除標誌就清掉該欄位的錯誤
watch([darkFile, removeDark], () => formErrors.clear('logoDark'))
watch([lightFile, removeLight], () => formErrors.clear('logoLight'))

const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增夥伴' : `編輯：${form.nameZh || '（未命名）'}`))

function applyDetail(d: Awaited<ReturnType<typeof getPartner>>) {
  form.partnerType = d.partnerType ?? ''
  form.country = d.country ?? ''
  form.startOn = d.startOn ?? ''
  form.endOn = d.endOn ?? ''
  form.websiteUrl = d.websiteUrl ?? ''
  form.showInFooter = d.showInFooter
  form.showOnHome = d.showOnHome
  form.sortOrder = d.sortOrder
  form.nameZh = d.zh.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.contentZh = d.zh.content ?? ''
  form.contentEn = d.en?.content ?? ''
  darkUrl.value = d.logoDarkUrl ?? null
  lightUrl.value = d.logoLightUrl ?? null
  hasDark.value = !!d.logoDarkKey
  hasLight.value = !!d.logoLightKey
  darkWidth.value = d.logoDarkWidth ?? null
  darkHeight.value = d.logoDarkHeight ?? null
  lightWidth.value = d.logoLightWidth ?? null
  lightHeight.value = d.logoLightHeight ?? null
  form.logoAltZh = d.zh.logoAlt ?? ''
  form.logoAltEn = d.en?.logoAlt ?? ''
}

async function load() {
  loadState.value = 'loading'
  try {
    const t = await listPartnerTypes(activeClubId.value).catch(() => null)
    typeOptions.value = t ? Array.from(new Set([...t.standardTypes, ...t.usedTypes])) : []
    if (!isCreate.value && partnerId.value) {
      applyDetail(await getPartner(activeClubId.value, partnerId.value))
    }
    darkFile.value = lightFile.value = null
    removeDark.value = removeLight.value = false
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') {
      loadState.value = 'not-found'
    } else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
onMounted(load)

const isDirty = computed(
  () =>
    loadState.value === 'ready' &&
    (JSON.stringify(form) !== baselineJson.value || !!darkFile.value || !!lightFile.value || removeDark.value || removeLight.value),
)
useUnsavedChanges(isDirty)

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文名稱'
  if (!form.partnerType.trim()) errors.partnerType = '請選擇或輸入夥伴類型'
  if (form.startOn && form.endOn && form.endOn < form.startOn) errors.endOn = '合作結束日不能早於開始日'
  if (form.websiteUrl.trim() && !/^https?:\/\//i.test(form.websiteUrl.trim())) {
    errors.websiteUrl = '官網連結必須是以 http:// 或 https:// 開頭的完整網址'
  }
  return errors
}

function buildPayload(): SavePartnerPayload {
  const dark = imageIntent(darkFile.value, removeDark.value)
  const light = imageIntent(lightFile.value, removeLight.value)
  return {
    partnerType: form.partnerType.trim(),
    country: nullIfBlank(form.country),
    startOn: form.startOn || null,
    endOn: form.endOn || null,
    websiteUrl: nullIfBlank(form.websiteUrl),
    showInFooter: form.showInFooter,
    showOnHome: form.showOnHome,
    sortOrder: form.sortOrder,
    content: {
      zh: { name: form.nameZh.trim(), content: nullIfBlank(form.contentZh), logoAlt: nullIfBlank(form.logoAltZh) },
      en: enOrUndefined(
        { name: form.nameEn.trim(), content: nullIfBlank(form.contentEn) as string, logoAlt: nullIfBlank(form.logoAltEn) as string },
        'name', 'content', 'logoAlt',
      ),
    },
    removeLogoDark: dark.remove,
    removeLogoLight: light.remove,
  }
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const files = { logoDark: darkFile.value, logoLight: lightFile.value }
    const saved = isCreate.value
      ? await createPartner(activeClubId.value, buildPayload(), files)
      : await updatePartner(activeClubId.value, partnerId.value!, buildPayload(), files)
    if (isCreate.value) {
      partnerId.value = saved.id
      router.replace(`/business/partners/${saved.id}/edit`)
    }
    applyDetail(saved)
    darkFile.value = lightFile.value = null
    removeDark.value = removeLight.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放頁首
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="partner-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="router.push('/business/partners')"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="E1" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這位夥伴，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="router.push('/business/partners')">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="partner-edit__error" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視夥伴資料，不能修改。" type="info" show-icon :closable="false" class="partner-edit__error" />
      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField field="name" label="名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
                  <BilingualTextareaField field="content" label="合作內容" :zh="form.contentZh" :en="form.contentEn" @update:zh="(v) => (form.contentZh = v)" @update:en="(v) => (form.contentEn = v)" />
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="partnerType" label="夥伴類型" required>
                    <el-select v-model="form.partnerType" filterable allow-create default-first-option placeholder="選擇或直接輸入新類型" style="width: 100%" @change="formErrors.clear('partnerType')">
                      <el-option v-for="t in typeOptions" :key="t" :label="t" :value="t" />
                    </el-select>
                  </FormField>
                  <FormField field="country" label="國家"><el-input v-model="form.country" maxlength="32" placeholder="例如 台灣、斯洛伐克" /></FormField>
                  <el-form-item label="合作開始日"><el-date-picker v-model="form.startOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('endOn')" /></el-form-item>
                  <FormField field="endOn" label="合作結束日"><el-date-picker v-model="form.endOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('endOn')" /></FormField>
                  <p class="partner-edit__hint">兩個日期都不填代表長期合作；合作結束後前台會自動不再顯示。</p>
                  <FormField field="websiteUrl" label="官網連結"><el-input v-model="form.websiteUrl" placeholder="https://" /></FormField>
                </FormSection>
                <FormSection title="深色底用標誌">
                  <FormField field="logoDark" label="放在深色背景上的標誌">
                    <ImageUploader v-model:file="darkFile" v-model:remove-cover="removeDark" variant="logo" :min-width="0" :min-height="0" :has-existing-image="hasDark" :existing-preview-url="darkUrl" :disabled="saving || readOnly" />
                  </FormField>
                  <ImageSizeHint :has-image="hasDark && !removeDark" :width="darkWidth" :height="darkHeight" />
                </FormSection>
                <FormSection title="淺色底用標誌">
                  <FormField field="logoLight" label="放在淺色背景上的標誌">
                    <ImageUploader v-model:file="lightFile" v-model:remove-cover="removeLight" variant="logo" :min-width="0" :min-height="0" :has-existing-image="hasLight" :existing-preview-url="lightUrl" :disabled="saving || readOnly" />
                  </FormField>
                  <ImageSizeHint :has-image="hasLight && !removeLight" :width="lightWidth" :height="lightHeight" />
                </FormSection>
                <FormSection title="標誌圖片說明">
                  <p class="partner-edit__hint">深色底與淺色底兩版標誌共用同一個說明。</p>
                  <ImageAltField
                    v-model:zh="form.logoAltZh"
                    v-model:en="form.logoAltEn"
                    field="logoAlt"
                    :has-image="(hasDark && !removeDark) || (hasLight && !removeLight) || darkFile !== null || lightFile !== null"
                    :show-size="false"
                    fallback="夥伴名稱"
                  />
                </FormSection>
              </el-card>
              <el-card shadow="never" header="發布設定">
                <FormSection title="前台呈現">
                  <el-form-item label="曝光位置">
                    <el-checkbox v-model="form.showOnHome">顯示在首頁夥伴標誌牆</el-checkbox>
                    <el-checkbox v-model="form.showInFooter">顯示在頁尾</el-checkbox>
                  </el-form-item>
                  <el-form-item label="排序值">
                    <el-input-number v-model="form.sortOrder" :min="0" />
                    <p class="partner-edit__hint">數字小的排前面；也可以在夥伴列表用上移、下移調整。</p>
                  </el-form-item>
                </FormSection>
              </el-card>
            </template>
          </EditLayout>
        </LangTabsBar>
      </el-form>
      <EditActionBar v-if="!readOnly">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.partner-edit { max-width: 1200px; margin: 0 auto; }
.partner-edit__error { margin-bottom: 16px; }
.partner-edit__hint { margin: 6px 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>
