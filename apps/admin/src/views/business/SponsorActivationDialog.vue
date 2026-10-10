<script setup lang="ts">
/**
 * 贊助活動——新增／編輯對話框，含活動圖集（圖集是立即儲存，不需要按儲存）。
 * 對話框自己一組語言分頁與欄位錯誤（與贊助商整頁分開）。
 */
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import GalleryManager from '@/components/GalleryManager.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import {
  addActivationImage,
  createActivation,
  deleteActivationImage,
  reorderActivationImages,
  updateActivation,
  updateActivationImageAlt,
  type ActivationDto,
} from '@/api/adminSponsors'

const props = defineProps<{
  modelValue: boolean
  clubId: string
  sponsorId: string
  canUpdate: boolean
  /** 要編輯的活動；null＝新增。 */
  activation: ActivationDto | null
  /** 目前贊助商的全部活動（圖集的最新狀態由這裡取）。 */
  activations: ActivationDto[]
  /** 新增時預設的排序值。 */
  defaultSortOrder: number
  /** 重新載入活動清單（由外層負責）。 */
  reload: () => Promise<void>
}>()
const emit = defineEmits<{ (e: 'update:modelValue', value: boolean): void }>()

const formErrors = provideFormErrors()
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤。 */
const formError = ref<string | null>(null)
const form = reactive({
  id: null as string | null,
  happenedOn: '',
  sortOrder: 0,
  titleZh: '', titleEn: '', summaryZh: '', summaryEn: '',
})
const editing = computed(() => props.activations.find((a) => a.id === form.id) ?? null)

// 每次開啟時用傳入的活動重設表單
watch(
  () => props.modelValue,
  (open) => {
    if (!open) return
    formError.value = null
    formErrors.clearAll()
    const a = props.activation
    Object.assign(form, {
      id: a?.id ?? null,
      happenedOn: a?.happenedOn ?? '',
      sortOrder: a?.sortOrder ?? props.defaultSortOrder,
      titleZh: a?.zh.title ?? '', titleEn: a?.en?.title ?? '',
      summaryZh: a?.zh.resultSummary ?? '', summaryEn: a?.en?.resultSummary ?? '',
    })
  },
)

function close() {
  emit('update:modelValue', false)
}

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.titleZh.trim()) errors.titleZh = '請輸入中文活動名稱'
  return errors
}

async function save() {
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  const payload = {
    happenedOn: form.happenedOn || null,
    sortOrder: form.sortOrder,
    content: {
      zh: { title: form.titleZh.trim(), resultSummary: nullIfBlank(form.summaryZh) },
      en: enOrUndefined({ title: form.titleEn.trim(), resultSummary: nullIfBlank(form.summaryEn) as string }, 'title', 'resultSummary'),
    },
  }
  try {
    if (form.id) {
      await updateActivation(props.clubId, props.sponsorId, form.id, payload)
      await props.reload()
      ElMessage.success('已儲存')
    } else {
      const created = await createActivation(props.clubId, props.sponsorId, payload)
      await props.reload()
      // 新增後直接切到編輯狀態，讓使用者接著上傳圖集
      form.id = created.id
      ElMessage.success('已新增，可以接著加入活動圖片')
    }
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放頁首（送出失敗不關對話框）
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function galleryUpload(file: File) {
  await addActivationImage(props.clubId, props.sponsorId, form.id!, file)
  await props.reload()
}
async function galleryRemove(imageId: string) {
  await deleteActivationImage(props.clubId, props.sponsorId, form.id!, imageId)
  await props.reload()
}
async function gallerySaveAlt(imageId: string, altZh: string | null, altEn: string | null) {
  await updateActivationImageAlt(props.clubId, props.sponsorId, form.id!, imageId, altZh, altEn)
  await props.reload()
}
async function galleryReorder(ids: string[]) {
  await reorderActivationImages(props.clubId, props.sponsorId, form.id!, ids)
  await props.reload()
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="form.id ? '編輯贊助活動' : '新增贊助活動'"
    width="640px"
    class="activation-dialog"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <el-alert v-if="formError" :title="formError" type="warning" show-icon class="activation-dialog__block" @close="formError = null" />
    <el-form label-position="top" :disabled="!canUpdate">
      <LangTabsBar variant="bare">
        <BilingualShortField field="title" label="活動名稱" :zh="form.titleZh" :en="form.titleEn" required @update:zh="(v) => (form.titleZh = v)" @update:en="(v) => (form.titleEn = v)" />
        <BilingualTextareaField field="summary" label="成效摘要" :zh="form.summaryZh" :en="form.summaryEn" @update:zh="(v) => (form.summaryZh = v)" @update:en="(v) => (form.summaryEn = v)" />
      </LangTabsBar>
      <el-row :gutter="12">
        <el-col :xs="24" :sm="12"><el-form-item label="活動日期"><el-date-picker v-model="form.happenedOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
        <el-col :xs="24" :sm="12"><el-form-item label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item></el-col>
      </el-row>
    </el-form>
    <el-divider>活動圖集</el-divider>
    <p class="activation-dialog__hint">這裡的變更會立即儲存，不需要按下方的儲存</p>
    <p v-if="!form.id" class="activation-dialog__hint">請先儲存基本資料，才能管理相簿</p>
    <GalleryManager
      v-else
      :images="(editing?.images ?? []).map((i) => ({ id: i.id, thumbUrl: i.thumbUrl, imageUrl: i.imageUrl, altZh: i.altZh, altEn: i.altEn }))"
      :disabled="!canUpdate"
      :on-upload="galleryUpload"
      :on-remove="galleryRemove"
      :on-reorder="galleryReorder"
      :on-save-alt="gallerySaveAlt"
    />
    <template #footer>
      <FormErrorStatus />
      <el-button @click="close">關閉</el-button>
      <el-button v-if="canUpdate" type="primary" :loading="saving" @click="save">儲存</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.activation-dialog__block { margin-bottom: 16px; }
.activation-dialog__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
