<script setup lang="ts">
/**
 * 事蹟紀錄——新增／編輯。三項必填：公益團體、捐助內容、活動圖片（主圖）。
 * 其他活動圖片可多張，新增後在編輯頁補充。共用事蹟整頁唯讀。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import GalleryManager from '@/components/GalleryManager.vue'
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
import {
  addRecordImage,
  createRecord,
  deleteRecordImage,
  getRecord,
  listOrgs,
  listPrograms,
  reorderRecordImages,
  updateRecord,
  type GalleryImageDto,
  type OrgListItemDto,
  type ProgramListItemDto,
  type RecordDetailDto,
} from '@/api/adminCharity'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'charity-record-new')
const recordId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('charity.content')
const club = computed(() => activeClubId.value)

const form = reactive({
  charityId: '', programId: '', happenedOn: '', sortOrder: 0, isPinned: false,
  donationZh: '', donationEn: '', locationZh: '', locationEn: '', briefZh: '', briefEn: '',
})
const baselineJson = ref('')
const imageFile = ref<File | null>(null)
const removeFlag = ref(false)
const imageUrl = ref<string | null>(null)
const hasImage = ref(false)
const gallery = ref<GalleryImageDto[]>([])
const isShared = ref(false)
const orgs = ref<OrgListItemDto[]>([])
const programs = ref<ProgramListItemDto[]>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除主圖就清掉該欄位的錯誤
watch([imageFile, removeFlag], () => formErrors.clear('image'))
const readOnly = computed(() => isShared.value || (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增事蹟紀錄' : `${readOnly.value ? '檢視' : '編輯'}事蹟紀錄`))

function apply(d: RecordDetailDto) {
  form.charityId = d.charityId
  form.programId = d.charityProgramId ?? ''
  form.happenedOn = d.happenedOn ?? ''
  form.sortOrder = d.sortOrder
  form.isPinned = d.isPinned
  form.donationZh = d.zh.donationContent ?? ''
  form.donationEn = d.en?.donationContent ?? ''
  form.locationZh = d.zh.location ?? ''
  form.locationEn = d.en?.location ?? ''
  form.briefZh = d.zh.briefDescription ?? ''
  form.briefEn = d.en?.briefDescription ?? ''
  imageUrl.value = d.imageUrl ?? null
  hasImage.value = !!d.imageKey
  gallery.value = d.images
  isShared.value = d.isShared
}

async function load() {
  loadState.value = 'loading'
  try {
    const [o, p] = await Promise.all([
      listOrgs(club.value).catch(() => [] as OrgListItemDto[]),
      listPrograms(club.value, { pageSize: 100 }).then((r) => r.items).catch(() => [] as ProgramListItemDto[]),
    ])
    orgs.value = o
    programs.value = p
    if (!isCreate.value && recordId.value) apply(await getRecord(club.value, recordId.value))
    imageFile.value = null
    removeFlag.value = false
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

const isDirty = computed(() => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!imageFile.value || removeFlag.value))
useUnsavedChanges(isDirty)

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.charityId) errors.charityId = '請選擇公益團體'
  if (!form.donationZh.trim()) errors.donationZh = '請填寫中文捐助內容，例如「足球 50 顆、訓練背心 100 件」'
  if (isCreate.value && !imageFile.value) errors.image = '事蹟紀錄必須上傳活動圖片'
  else if (!isCreate.value && removeFlag.value && !imageFile.value) errors.image = '主圖不能移除，請選擇新的圖片來更換'
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
      charityId: form.charityId,
      charityProgramId: form.programId || null,
      happenedOn: form.happenedOn || null,
      sortOrder: form.sortOrder,
      isPinned: form.isPinned,
      content: {
        zh: { donationContent: form.donationZh.trim(), location: nullIfBlank(form.locationZh), briefDescription: nullIfBlank(form.briefZh) },
        en: enOrUndefined(
          { donationContent: form.donationEn.trim(), location: nullIfBlank(form.locationEn) as string, briefDescription: nullIfBlank(form.briefEn) as string },
          'donationContent', 'location', 'briefDescription',
        ),
      },
    }
    const saved = isCreate.value
      ? await createRecord(club.value, payload, imageFile.value!)
      : await updateRecord(club.value, recordId.value!, payload, imageFile.value)
    if (isCreate.value) {
      recordId.value = saved.id
      router.replace(`/content/charity/records/${saved.id}/edit`)
    }
    apply(saved)
    imageFile.value = null
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

async function galleryUpload(file: File) {
  gallery.value = (await addRecordImage(club.value, recordId.value!, file)).images
}
async function galleryRemove(id: string) {
  await deleteRecordImage(club.value, recordId.value!, id)
  gallery.value = gallery.value.filter((g) => g.id !== id)
}
async function galleryReorder(ids: string[]) {
  gallery.value = (await reorderRecordImages(club.value, recordId.value!, ids)).images
}

const back = () => router.push({ path: '/content/charity', query: { tab: 'records' } })
</script>

<template>
  <div class="record-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="B5" />
        <SharedContentNotice v-if="loadState === 'ready' && isShared" what="事蹟紀錄" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這筆事蹟紀錄，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="record-edit__block" @close="formError = null" />
      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualTextareaField field="donation" label="捐助內容" :zh="form.donationZh" :en="form.donationEn" required :rows="2" placeholder="例如：足球 50 顆、訓練背心 100 件" @update:zh="(v) => (form.donationZh = v)" @update:en="(v) => (form.donationEn = v)" />
                  <BilingualShortField field="location" label="地點" :zh="form.locationZh" :en="form.locationEn" @update:zh="(v) => (form.locationZh = v)" @update:en="(v) => (form.locationEn = v)" />
                  <BilingualTextareaField field="brief" label="簡述" :zh="form.briefZh" :en="form.briefEn" @update:zh="(v) => (form.briefZh = v)" @update:en="(v) => (form.briefEn = v)" />
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="charityId" label="公益團體" required>
                    <el-select v-model="form.charityId" filterable placeholder="請選擇受贈的公益團體" style="width: 100%" @change="formErrors.clear('charityId')">
                      <el-option v-for="o in orgs" :key="o.id" :label="o.nameZh || '（未命名）'" :value="o.id" />
                    </el-select>
                  </FormField>
                  <FormField field="programId" label="所屬計畫（選填）">
                    <el-select v-model="form.programId" clearable filterable placeholder="不屬於任何計畫" style="width: 100%"><el-option v-for="p in programs" :key="p.id" :label="p.nameZh || '（未命名）'" :value="p.id" /></el-select>
                  </FormField>
                  <el-form-item label="日期"><el-date-picker v-model="form.happenedOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
                </FormSection>
                <FormSection title="活動圖片（主圖，必填）">
                  <FormField field="image" label="活動圖片（主圖）" required>
                    <ImageUploader v-model:file="imageFile" v-model:remove-cover="removeFlag" :has-existing-image="hasImage" :existing-preview-url="imageUrl" :disabled="saving || readOnly" />
                  </FormField>
                  <p class="record-edit__hint">主圖不能移除，只能更換。</p>
                </FormSection>
                <FormSection title="其他活動圖片">
                  <p class="record-edit__hint">這裡的變更會立即儲存，不需要按下方的儲存</p>
                  <p v-if="isCreate" class="record-edit__hint">請先儲存基本資料，才能管理相簿</p>
                  <GalleryManager v-else :images="gallery" :disabled="readOnly" :on-upload="galleryUpload" :on-remove="galleryRemove" :on-reorder="galleryReorder" />
                </FormSection>
              </el-card>
              <el-card shadow="never" header="發布設定">
                <FormSection>
                  <el-form-item label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item>
                  <el-form-item label="置頂"><el-switch v-model="form.isPinned" active-text="排在最前面" /></el-form-item>
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
.record-edit { max-width: 1200px; margin: 0 auto; }
.record-edit__block { margin-bottom: 16px; }
.record-edit__hint { margin: 6px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>
