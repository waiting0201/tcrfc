<script setup lang="ts">
/**
 * H1 全站搜尋與分享預設（對應主站規劃書 §4.8 H「全站 SEO 預設」「robots.txt 線上編輯」
 * 「追蹤碼管理」；apps/api/README.md「S1-12」）。單一設定表單，逐俱樂部各自一份，
 * 跟著站台切換器切換（`activeClubId`），比照 `ClubEditView.vue` 的單筆設定編輯頁寫法。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import { getAdminSeoSettings, updateAdminSeoSettings, type AdminSeoSettingsDto } from '@/api/adminSeo'
import { AdminApiError } from '@/api/http'

const club = computed(() => activeClubId.value)

interface SettingsForm {
  titleTemplateZh: string
  titleTemplateEn: string
  defaultDescriptionZh: string
  defaultDescriptionEn: string
  robotsCustomRules: string
  ga4MeasurementId: string
  gtmContainerId: string
  metaPixelId: string
  lineTagId: string
  ogImageUrl: string | null
}

function emptyForm(): SettingsForm {
  return {
    titleTemplateZh: '',
    titleTemplateEn: '',
    defaultDescriptionZh: '',
    defaultDescriptionEn: '',
    robotsCustomRules: '',
    ga4MeasurementId: '',
    gtmContainerId: '',
    metaPixelId: '',
    lineTagId: '',
    ogImageUrl: null,
  }
}

const loadState = ref<'loading' | 'error' | 'ready'>('loading')
const loadErrorMessage = ref('')
const form = reactive<SettingsForm>(emptyForm())
const baselineJson = ref('')

/** 分享圖片的「這次瀏覽階段的意圖」，規則比照 `NewsEditView.vue` 的 `coverFile`／`removeCover`：
 * `ogImageFile` 非 `null` 是「選了要換的新圖」，`removeOgImage` 為真是「儲存時清空」，
 * 兩者不會同時成立，且都要等按下「儲存」才真的生效（規劃書「選檔不上傳、儲存才上傳」）。 */
const ogImageFile = ref<File | null>(null)
const removeOgImage = ref(false)

const saving = ref(false)
const formError = ref<string | null>(null)

function applyLoaded(dto: AdminSeoSettingsDto) {
  form.titleTemplateZh = dto.titleTemplateZh ?? ''
  form.titleTemplateEn = dto.titleTemplateEn ?? ''
  form.defaultDescriptionZh = dto.defaultDescriptionZh ?? ''
  form.defaultDescriptionEn = dto.defaultDescriptionEn ?? ''
  form.robotsCustomRules = dto.robotsCustomRules ?? ''
  form.ga4MeasurementId = dto.ga4MeasurementId ?? ''
  form.gtmContainerId = dto.gtmContainerId ?? ''
  form.metaPixelId = dto.metaPixelId ?? ''
  form.lineTagId = dto.lineTagId ?? ''
  form.ogImageUrl = dto.ogImageUrl ?? null
  ogImageFile.value = null
  removeOgImage.value = false
  baselineJson.value = JSON.stringify(form)
}

async function loadSettings() {
  loadState.value = 'loading'
  try {
    const dto = await getAdminSeoSettings(club.value)
    applyLoaded(dto)
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(loadSettings)
watch(club, loadSettings)

const isDirty = computed(() =>
  loadState.value === 'ready'
  && (JSON.stringify(form) !== baselineJson.value || ogImageFile.value !== null || removeOgImage.value),
)
useUnsavedChanges(isDirty)

function validate(): boolean {
  formError.value = null
  if (!form.titleTemplateZh.trim()) {
    formError.value = '請輸入標題樣板（中文）'
    return false
  }
  if (!form.defaultDescriptionZh.trim()) {
    formError.value = '請輸入預設描述（中文）'
    return false
  }
  return true
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  try {
    const saved = await updateAdminSeoSettings(
      club.value,
      {
        titleTemplateZh: form.titleTemplateZh || null,
        titleTemplateEn: form.titleTemplateEn || null,
        defaultDescriptionZh: form.defaultDescriptionZh || null,
        defaultDescriptionEn: form.defaultDescriptionEn || null,
        robotsCustomRules: form.robotsCustomRules || null,
        ga4MeasurementId: form.ga4MeasurementId || null,
        gtmContainerId: form.gtmContainerId || null,
        metaPixelId: form.metaPixelId || null,
        lineTagId: form.lineTagId || null,
        removeOgImage: removeOgImage.value,
      },
      ogImageFile.value,
    )
    applyLoaded(saved)
    ElMessage.success('已儲存')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="seo-settings">
    <PageHeader title="全站搜尋與分享設定">
      <template #meta>
        <FrontendUnitBanner module-code="H" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>

    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage">
        <el-button type="primary" @click="loadSettings">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="seo-settings__form-error"
        @close="formError = null"
      />

      <el-card shadow="never" header="標題與描述" class="seo-settings__section">
        <p class="seo-settings__hint">
          這裡設定的是全站的預設值：任何一頁自己沒有另外設定搜尋與分享標題／描述時，就會使用這裡的樣板與描述。
          網站名稱本身在「俱樂部與授權管理」設定，這裡不重複維護。
        </p>
        <el-form label-position="top">
          <BilingualShortField
            label="標題樣板"
            :zh="form.titleTemplateZh"
            :en="form.titleTemplateEn"
            required
            placeholder="例如：{標題}｜台中磐石足球俱樂部"
            @update:zh="(v) => (form.titleTemplateZh = v)"
            @update:en="(v) => (form.titleTemplateEn = v)"
          />
          <p class="seo-settings__hint">「{標題}」會被換成每一頁自己的標題，其餘文字（例如網站名稱）原樣顯示。</p>
          <BilingualTextareaField
            label="預設描述"
            :zh="form.defaultDescriptionZh"
            :en="form.defaultDescriptionEn"
            required
            :rows="3"
            placeholder="建議 80–120 字，描述整個網站"
            @update:zh="(v) => (form.defaultDescriptionZh = v)"
            @update:en="(v) => (form.defaultDescriptionEn = v)"
          />
        </el-form>
      </el-card>

      <el-card shadow="never" header="全站預設分享圖片" class="seo-settings__section">
        <p class="seo-settings__hint">
          任何一頁自己沒有另外設定分享圖片時，社群分享（例如 Facebook、LINE）預覽會使用這張圖片。
        </p>
        <el-form-item label="分享圖片">
          <ImageUploader
            v-model:file="ogImageFile"
            v-model:remove-cover="removeOgImage"
            :has-existing-image="!!form.ogImageUrl"
            :existing-preview-url="form.ogImageUrl"
            :disabled="saving"
          />
        </el-form-item>
      </el-card>

      <el-card shadow="never" header="搜尋引擎收錄規則" class="seo-settings__section">
        <el-alert type="warning" :closable="false" show-icon class="seo-settings__alert">
          目前網站尚未正式上線，這裡的設定要等正式上線後才會生效——上線前系統一律回覆「禁止所有搜尋引擎收錄」，不論這裡填了什麼。
        </el-alert>
        <el-form label-position="top">
          <el-form-item label="額外規則（進階，選填）">
            <el-input
              v-model="form.robotsCustomRules"
              type="textarea"
              :rows="4"
              placeholder="選填，正式上線後會附加在系統自動產生的規則之後，需要熟悉 robots.txt 語法才建議填寫"
            />
          </el-form-item>
        </el-form>
      </el-card>

      <el-card shadow="never" header="追蹤碼" class="seo-settings__section">
        <p class="seo-settings__hint">填寫後會自動注入到前台所有頁面，留白表示不啟用該項追蹤。</p>
        <el-form label-position="top">
          <el-form-item label="GA4 評估 ID">
            <el-input v-model="form.ga4MeasurementId" placeholder="例如：G-XXXXXXXXXX" />
          </el-form-item>
          <el-form-item label="GTM 容器 ID">
            <el-input v-model="form.gtmContainerId" placeholder="例如：GTM-XXXXXXX" />
          </el-form-item>
          <el-form-item label="Meta Pixel ID">
            <el-input v-model="form.metaPixelId" placeholder="選填" />
          </el-form-item>
          <el-form-item label="LINE Tag ID">
            <el-input v-model="form.lineTagId" placeholder="選填" />
          </el-form-item>
        </el-form>
      </el-card>

      <div class="seo-settings__actions">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.seo-settings {
  max-width: 780px;
  margin: 0 auto;
}

.seo-settings__section {
  margin-bottom: 12px;
}

.seo-settings__form-error {
  margin-bottom: 12px;
}

.seo-settings__hint {
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin: 4px 0 12px;
}

.seo-settings__alert {
  margin-bottom: 12px;
}

.seo-settings__actions {
  margin-top: 16px;
}
</style>
