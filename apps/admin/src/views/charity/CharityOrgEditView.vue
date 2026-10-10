<script setup lang="ts">
/** 公益團體——新增／編輯。共用團體整頁唯讀。 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageAltField from '@/components/ImageAltField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import SharedContentNotice from '@/components/SharedContentNotice.vue'
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
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { createOrg, getOrg, updateOrg, type OrgDetailDto } from '@/api/adminCharity'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'charity-org-new')
const orgId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('charity.content')

const form = reactive({ websiteUrl: '', contactName: '', contactPhone: '', nameZh: '', nameEn: '', introZh: '', introEn: '', logoAltZh: '', logoAltEn: '' })
const baselineJson = ref('')
const logoFile = ref<File | null>(null)
const removeLogo = ref(false)
const logoUrl = ref<string | null>(null)
const hasLogo = ref(false)
const logoWidth = ref<number | null>(null)
const logoHeight = ref<number | null>(null)
const isShared = ref(false)
const programs = ref<OrgDetailDto['programs']>([])
const records = ref<OrgDetailDto['records']>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除標誌就清掉該欄位的錯誤
watch([logoFile, removeLogo], () => formErrors.clear('logo'))
const readOnly = computed(() => isShared.value || (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增公益團體' : `${readOnly.value ? '檢視' : '編輯'}：${form.nameZh || '（未命名）'}`))

function apply(d: OrgDetailDto) {
  form.websiteUrl = d.websiteUrl ?? ''
  form.contactName = d.contactName ?? ''
  form.contactPhone = d.contactPhone ?? ''
  form.nameZh = d.zh.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.introZh = d.zh.intro ?? ''
  form.introEn = d.en?.intro ?? ''
  logoUrl.value = d.logoUrl ?? null
  hasLogo.value = !!d.logoKey
  logoWidth.value = d.logoWidth ?? null
  logoHeight.value = d.logoHeight ?? null
  form.logoAltZh = d.zh.logoAlt ?? ''
  form.logoAltEn = d.en?.logoAlt ?? ''
  isShared.value = d.isShared
  programs.value = d.programs
  records.value = d.records
}

async function load() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && orgId.value) apply(await getOrg(activeClubId.value, orgId.value))
    logoFile.value = null
    removeLogo.value = false
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') loadState.value = 'not-found'
    else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
onMounted(load)

const isDirty = computed(() => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!logoFile.value || removeLogo.value))
useUnsavedChanges(isDirty)

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文團體名稱'
  if (form.websiteUrl.trim() && !/^https?:\/\//i.test(form.websiteUrl.trim())) errors.websiteUrl = '官網連結必須是以 http:// 或 https:// 開頭的完整網址'
  return errors
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
    const payload = {
      websiteUrl: nullIfBlank(form.websiteUrl),
      contactName: nullIfBlank(form.contactName),
      contactPhone: nullIfBlank(form.contactPhone),
      content: {
        zh: { name: form.nameZh.trim(), intro: nullIfBlank(form.introZh), logoAlt: nullIfBlank(form.logoAltZh) },
        en: enOrUndefined(
          { name: form.nameEn.trim(), intro: nullIfBlank(form.introEn) as string, logoAlt: nullIfBlank(form.logoAltEn) as string },
          'name', 'intro', 'logoAlt',
        ),
      },
      removeLogo: logoFile.value ? false : removeLogo.value,
    }
    const saved = isCreate.value
      ? await createOrg(activeClubId.value, payload, logoFile.value)
      : await updateOrg(activeClubId.value, orgId.value!, payload, logoFile.value)
    if (isCreate.value) {
      orgId.value = saved.id
      router.replace(`/content/charity/organizations/${saved.id}/edit`)
    }
    apply(saved)
    logoFile.value = null
    removeLogo.value = false
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

const back = () => router.push({ path: '/content/charity', query: { tab: 'organizations' } })
</script>

<template>
  <div class="org-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="B5" />
        <SharedContentNotice v-if="loadState === 'ready' && isShared" what="團體資料" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這個公益團體，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="org-edit__block" @close="formError = null" />
      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField field="name" label="團體名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
                  <BilingualTextareaField field="intro" label="團體簡介" :zh="form.introZh" :en="form.introEn" @update:zh="(v) => (form.introZh = v)" @update:en="(v) => (form.introEn = v)" />
                </FormSection>
                <FormSection v-if="!isCreate" title="合作紀錄">
                  <p class="org-edit__hint">這個團體受贈的計畫與事蹟（唯讀彙整）。仍有計畫或事蹟引用時，團體不能刪除。</p>
                  <p v-if="programs.length === 0 && records.length === 0" class="org-edit__hint">目前沒有合作紀錄。</p>
                  <ul v-else class="org-edit__list">
                    <li v-for="p in programs" :key="p.id">計畫：{{ p.nameZh || '（未命名）' }}（{{ p.status === 'published' ? '已發布' : '草稿' }}）</li>
                    <li v-for="r in records" :key="r.id">事蹟：{{ r.donationContentZh || '（未填捐助內容）' }}{{ r.happenedOn ? `（${r.happenedOn}）` : '' }}</li>
                  </ul>
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="websiteUrl" label="官網連結"><el-input v-model="form.websiteUrl" placeholder="https://" /></FormField>
                  <FormField field="contactName" label="聯絡人"><el-input v-model="form.contactName" maxlength="64" /></FormField>
                  <FormField field="contactPhone" label="聯絡電話"><el-input v-model="form.contactPhone" maxlength="32" /></FormField>
                </FormSection>
                <FormSection title="標誌或代表圖">
                  <FormField field="logo" label="標誌或代表圖">
                    <ImageUploader v-model:file="logoFile" v-model:remove-cover="removeLogo" variant="logo" :min-width="0" :min-height="0" :has-existing-image="hasLogo" :existing-preview-url="logoUrl" :disabled="saving || readOnly" />
                  </FormField>
                  <ImageAltField
                    v-model:zh="form.logoAltZh"
                    v-model:en="form.logoAltEn"
                    field="logoAlt"
                    :has-image="(hasLogo && !removeLogo) || logoFile !== null"
                    :width="logoWidth"
                    :height="logoHeight"
                    fallback="團體名稱"
                  />
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
.org-edit { max-width: 1200px; margin: 0 auto; }
.org-edit__block { margin-bottom: 16px; }
.org-edit__hint { margin: 0 0 8px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.org-edit__list { margin: 0; padding-left: 20px; font-size: 13px; line-height: 1.8; }
</style>
