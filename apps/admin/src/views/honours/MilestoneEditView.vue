<script setup lang="ts">
/** 里程碑——新增／編輯（前台「關於」頁的時間軸）。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
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
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { createMilestone, getMilestone, updateMilestone, type MilestoneDto } from '@/api/adminHonours'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'milestone-new')
const milestoneId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('team.milestone')

const form = reactive({
  happenedOn: '', sortOrder: 0, isVisible: true,
  titleZh: '', titleEn: '', descZh: '', descEn: '', altZh: '', altEn: '',
})
const baselineJson = ref('')
const imageFile = ref<File | null>(null)
const removeImage = ref(false)
const imageUrl = ref<string | null>(null)
const hasImage = ref(false)
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增里程碑' : `編輯：${form.titleZh || '（未命名）'}`))

function apply(d: MilestoneDto) {
  form.happenedOn = d.happenedOn
  form.sortOrder = d.sortOrder
  form.isVisible = d.isVisible
  form.titleZh = d.zh.title ?? ''
  form.titleEn = d.en?.title ?? ''
  form.descZh = d.zh.description ?? ''
  form.descEn = d.en?.description ?? ''
  form.altZh = d.zh.imageAlt ?? ''
  form.altEn = d.en?.imageAlt ?? ''
  imageUrl.value = d.imageUrl ?? null
  hasImage.value = !!d.imageKey
}

async function load() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && milestoneId.value) apply(await getMilestone(activeClubId.value, milestoneId.value))
    imageFile.value = null
    removeImage.value = false
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

const isDirty = computed(() => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!imageFile.value || removeImage.value))
useUnsavedChanges(isDirty)

/** 一次檢查全部必填，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.titleZh.trim()) errors.titleZh = '請輸入中文標題'
  if (!form.happenedOn) errors.happenedOn = '請選擇日期'
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
      happenedOn: form.happenedOn,
      sortOrder: form.sortOrder,
      isVisible: form.isVisible,
      content: {
        zh: { title: form.titleZh.trim(), description: nullIfBlank(form.descZh), imageAlt: nullIfBlank(form.altZh) },
        en: enOrUndefined({ title: form.titleEn.trim(), description: nullIfBlank(form.descEn) as string, imageAlt: nullIfBlank(form.altEn) as string }, 'title', 'description', 'imageAlt'),
      },
      removeImage: imageFile.value ? false : removeImage.value,
    }
    const saved = isCreate.value
      ? await createMilestone(activeClubId.value, payload, imageFile.value)
      : await updateMilestone(activeClubId.value, milestoneId.value!, payload, imageFile.value)
    if (isCreate.value) {
      milestoneId.value = saved.id
      router.replace(`/teams/honours/milestones/${saved.id}/edit`)
    }
    apply(saved)
    imageFile.value = null
    removeImage.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

const back = () => router.push({ path: '/teams/honours', query: { tab: 'milestones' } })
</script>

<template>
  <div class="milestone-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="C5" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這個里程碑，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="milestone-edit__block" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視里程碑，不能修改。" type="info" show-icon :closable="false" class="milestone-edit__block" />
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
                  <FormField field="happenedOn" label="日期" required><el-date-picker v-model="form.happenedOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('happenedOn')" /></FormField>
                </FormSection>
                <FormSection title="圖片（選填）">
                  <ImageUploader v-model:file="imageFile" v-model:remove-cover="removeImage" :min-width="0" :min-height="0" :has-existing-image="hasImage" :existing-preview-url="imageUrl" :disabled="saving || readOnly" />
                  <BilingualShortField field="alt" label="圖片替代文字" :zh="form.altZh" :en="form.altEn" placeholder="用一句話描述圖片內容，供視障者與搜尋引擎閱讀" @update:zh="(v) => (form.altZh = v)" @update:en="(v) => (form.altEn = v)" />
                </FormSection>
              </el-card>
              <el-card shadow="never" header="發布設定">
                <FormSection>
                  <el-form-item label="前台顯示"><el-switch v-model="form.isVisible" active-text="顯示在前台時間軸" inactive-text="隱藏" /></el-form-item>
                  <el-form-item label="排序值（同一天時的先後）"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item>
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
.milestone-edit { max-width: 1200px; margin: 0 auto 88px; }
.milestone-edit__block { margin-bottom: 16px; }
</style>
