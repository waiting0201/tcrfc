<script setup lang="ts">
/**
 * 媒體專區——新增／編輯。檔案規則：新聞稿與品牌識別包是 PDF 或壓縮檔（≤50 MB）；高解析圖是圖片，
 * 系統會自動產生縮圖，不需要另外上傳封面。共用資源整頁唯讀。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
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
import { enOrUndefined, imageIntent, nullIfBlank } from '@/api/adminCommon'
import { formatFileSize } from '@/utils/downloadFile'
import { createPress, getPress, updatePress, type PressDetailDto, type PressResourceType, type SavePressPayload } from '@/api/adminPress'
import { PRESS_TYPE_LABEL, PRESS_TYPE_ORDER } from './pressTypes'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'media-new')
const pressId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('content.press')

const form = reactive({
  resourceType: 'press_release' as PressResourceType,
  status: 'draft' as 'draft' | 'published',
  publishedOn: '',
  sortOrder: 0,
  titleZh: '', titleEn: '', descZh: '', descEn: '', coverAltZh: '', coverAltEn: '',
})
const baselineJson = ref('')
const originalType = ref<PressResourceType | null>(null)
const resourceFile = ref<File | null>(null)
const coverFile = ref<File | null>(null)
const removeCover = ref(false)
const coverUrl = ref<string | null>(null)
const hasCover = ref(false)
const fileBytes = ref<number | null>(null)
const fileUrl = ref<string | null>(null)
const downloadCount = ref(0)
const isShared = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除封面就清掉該欄位的錯誤
watch(resourceFile, () => formErrors.clear('file'))
watch([coverFile, removeCover], () => formErrors.clear('cover'))
const readOnly = computed(() => isShared.value || (isCreate.value ? !canCreate.value : !canUpdate.value))
const isImageType = computed(() => form.resourceType === 'hires_image')
const pageTitle = computed(() => (isCreate.value ? '新增媒體資源' : `${readOnly.value ? '檢視' : '編輯'}：${form.titleZh || '（未命名）'}`))
const fileAccept = computed(() => (isImageType.value ? 'image/jpeg,image/png,image/webp' : 'application/pdf,application/zip,.pdf,.zip'))
// 類別在「高解析圖」與其他之間切換，後端要求同時重新上傳檔案
const crossesImageBoundary = computed(
  () => !isCreate.value && originalType.value !== null && (originalType.value === 'hires_image') !== isImageType.value,
)

function apply(d: PressDetailDto) {
  form.resourceType = d.resourceType
  form.status = d.status
  form.publishedOn = d.publishedOn ?? ''
  form.sortOrder = d.sortOrder
  form.titleZh = d.zh.title ?? ''
  form.titleEn = d.en?.title ?? ''
  form.descZh = d.zh.description ?? ''
  form.descEn = d.en?.description ?? ''
  form.coverAltZh = d.zh.coverAlt ?? ''
  form.coverAltEn = d.en?.coverAlt ?? ''
  originalType.value = d.resourceType
  coverUrl.value = d.coverThumbUrl ?? d.coverUrl ?? null
  hasCover.value = !!d.coverKey
  fileBytes.value = d.fileBytes ?? null
  fileUrl.value = d.fileUrl ?? null
  downloadCount.value = d.downloadCount
  isShared.value = d.isShared
}

async function load() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && pressId.value) apply(await getPress(activeClubId.value, pressId.value))
    resourceFile.value = coverFile.value = null
    removeCover.value = false
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

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!resourceFile.value || !!coverFile.value || removeCover.value),
)
useUnsavedChanges(isDirty)

function pickFile(event: Event) {
  const el = event.target as HTMLInputElement
  const f = el.files?.[0]
  el.value = ''
  if (!f) return
  if (isImageType.value) {
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(f.type)) return void ElMessage.warning('高解析圖只接受 JPG、PNG 或 WebP 圖片')
    if (f.size > 10 * 1024 * 1024) return void ElMessage.warning('圖片超過 10 MB，請先壓縮')
  } else if (f.size > 50 * 1024 * 1024) {
    return void ElMessage.warning('檔案超過 50 MB，請壓縮後再上傳')
  }
  resourceFile.value = f
}

/** 一次檢查全部必填，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.titleZh.trim()) errors.titleZh = '請輸入中文標題'
  if (isCreate.value && !resourceFile.value) errors.file = '請選擇要上傳的檔案'
  else if (crossesImageBoundary.value && !resourceFile.value) errors.file = '在「高解析圖」與其他類別之間切換時，必須重新上傳檔案'
  return errors
}

function buildPayload(): SavePressPayload {
  return {
    resourceType: form.resourceType,
    status: form.status,
    publishedOn: form.publishedOn || null,
    sortOrder: form.sortOrder,
    content: {
      zh: { title: form.titleZh.trim(), description: nullIfBlank(form.descZh), coverAlt: nullIfBlank(form.coverAltZh) },
      en: enOrUndefined({ title: form.titleEn.trim(), description: nullIfBlank(form.descEn) as string, coverAlt: nullIfBlank(form.coverAltEn) as string }, 'title', 'description', 'coverAlt'),
    },
    removeCover: imageIntent(isImageType.value ? null : coverFile.value, removeCover.value).remove,
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
    // 高解析圖不可傳封面，避免帶到舊選擇
    const cover = isImageType.value ? null : coverFile.value
    const saved = isCreate.value
      ? await createPress(activeClubId.value, buildPayload(), resourceFile.value!, cover)
      : await updatePress(activeClubId.value, pressId.value!, buildPayload(), resourceFile.value, cover)
    if (isCreate.value) {
      pressId.value = saved.id
      router.replace(`/content/media/${saved.id}/edit`)
    }
    apply(saved)
    resourceFile.value = coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      await ElMessageBox.alert(error.message, '沒有編輯權限', { confirmButtonText: '我知道了' })
    } else if (error instanceof AdminApiError && formErrors.applyApiError(error)) {
      return
    } else {
      formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
    }
  } finally {
    saving.value = false
  }
}

const back = () => router.push('/content/media')
</script>

<template>
  <div class="media-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="B6" />
        <SharedContentNotice v-if="loadState === 'ready' && isShared" what="資源" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這筆資源，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="media-edit__block" @close="formError = null" />
      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField field="title" label="標題" :zh="form.titleZh" :en="form.titleEn" required @update:zh="(v) => (form.titleZh = v)" @update:en="(v) => (form.titleEn = v)" />
                  <BilingualTextareaField field="desc" label="說明" :zh="form.descZh" :en="form.descEn" @update:zh="(v) => (form.descZh = v)" @update:en="(v) => (form.descEn = v)" />
                </FormSection>
              </el-card>
            </template>
            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="resourceType" label="類別" required>
                    <el-select v-model="form.resourceType" style="width: 100%" @change="formErrors.clear('resourceType')"><el-option v-for="t in PRESS_TYPE_ORDER" :key="t" :label="PRESS_TYPE_LABEL[t]" :value="t" /></el-select>
                  </FormField>
                  <el-form-item label="發布日期"><el-date-picker v-model="form.publishedOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
                </FormSection>
                <FormSection title="檔案">
                  <el-alert v-if="crossesImageBoundary" type="warning" show-icon :closable="false" class="media-edit__block" title="你更改了類別，請重新上傳符合新類別的檔案。" />
                  <p v-if="!isCreate" class="media-edit__hint">目前檔案：{{ formatFileSize(fileBytes) }}・累計下載 {{ downloadCount }} 次。不重新上傳就維持原檔。</p>
                  <p class="media-edit__hint">{{ isImageType ? '高解析圖請上傳圖片（JPG、PNG 或 WebP，10 MB 以內），系統會自動產生縮圖。' : '新聞稿與品牌識別包請上傳 PDF 或壓縮檔（ZIP），50 MB 以內。' }}</p>
                  <input ref="fileInput" type="file" :accept="fileAccept" class="media-edit__input" @change="pickFile">
                  <FormField field="file" label="檔案">
                    <div>
                      <el-button :disabled="readOnly" @click="fileInput?.click()">{{ resourceFile ? '重新選擇檔案' : isCreate ? '選擇檔案' : '更換檔案' }}</el-button>
                      <span v-if="resourceFile" class="media-edit__picked">{{ resourceFile.name }}（{{ formatFileSize(resourceFile.size) }}，儲存時才會上傳）</span>
                    </div>
                  </FormField>
                </FormSection>
                <FormSection v-if="!isImageType" title="封面圖片（選填）">
                  <FormField field="cover" label="封面圖片">
                    <ImageUploader v-model:file="coverFile" v-model:remove-cover="removeCover" :min-width="0" :min-height="0" :has-existing-image="hasCover" :existing-preview-url="coverUrl" :disabled="saving || readOnly" />
                  </FormField>
                  <BilingualShortField
                    field="coverAlt"
                    label="圖片說明"
                    :zh="form.coverAltZh"
                    :en="form.coverAltEn"
                    :maxlength="200"
                    placeholder="選填，用一句話描述圖片內容，供視障讀者的輔助工具朗讀"
                    @update:zh="(v) => (form.coverAltZh = v)"
                    @update:en="(v) => (form.coverAltEn = v)"
                  />
                </FormSection>
              </el-card>

              <el-card shadow="never" header="發布設定">
                <FormSection>
                  <FormField field="status" label="狀態"><el-radio-group v-model="form.status"><el-radio value="draft">隱藏</el-radio><el-radio value="published">顯示</el-radio></el-radio-group></FormField>
                  <el-form-item label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item>
                  <p class="media-edit__hint">設為「顯示」且沒填發布日期時，系統會自動填入當天。</p>
                </FormSection>
              </el-card>
            </template>
          </EditLayout>
        </LangTabsBar>
      </el-form>
      <EditActionBar v-if="!readOnly"><template #status><FormErrorStatus /></template><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.media-edit { max-width: 1200px; margin: 0 auto 88px; }
.media-edit__block { margin-bottom: 16px; }
.media-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.media-edit__input { display: none; }
.media-edit__picked { margin-left: 8px; font-size: 12px; color: var(--admin-text-secondary); word-break: break-all; }
</style>
