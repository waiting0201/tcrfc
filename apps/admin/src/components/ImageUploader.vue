<script setup lang="ts">
/**
 * 圖片上傳元件（docs/21-admin-ui.md §9，規劃書 §4.0 後台圖片上傳通則）。
 *
 * - 選檔只在瀏覽器端產生預覽，不送出、不寫入；按「儲存」時交由外層表單一起送出（本元件本身
 *   不呼叫任何上傳 API——這份 mockup 完全沒有後端，「儲存」行為由 NewsEditView 統一處理）。
 * - 畫面文字刻意不列格式白名單細節（不出現 WebP／JPG／PNG 這種列舉），只寫「支援常見的照片格式」
 *   ——這是 docs/21 §9 記錄的一個代為判斷，值得請使用者確認。
 */
import { computed, ref } from 'vue'

const props = withDefaults(
  defineProps<{
    /** 已存在的圖片網址（編輯既有資料時的初始值） */
    existingUrl?: string | null
    minWidth?: number
    minHeight?: number
    saving?: boolean
    /** 伺服器端二次驗證不通過時的錯誤訊息（mock：由外層手動觸發示範用） */
    saveError?: string | null
  }>(),
  {
    existingUrl: null,
    minWidth: 1600,
    minHeight: 900,
    saving: false,
    saveError: null,
  },
)

const emit = defineEmits<{
  (e: 'update:file', file: File | null): void
}>()

const fileInput = ref<HTMLInputElement | null>(null)
const localPreviewUrl = ref<string | null>(null)
const selectedFile = ref<File | null>(null)
const resolutionError = ref('')
const sizeError = ref('')
const isDragOver = ref(false)

const previewUrl = computed(() => localPreviewUrl.value ?? props.existingUrl)
const hasImage = computed(() => Boolean(previewUrl.value))

function openFileDialog() {
  fileInput.value?.click()
}

function resetErrors() {
  resolutionError.value = ''
  sizeError.value = ''
}

function validateAndSet(file: File) {
  resetErrors()

  const MAX_BYTES = 10 * 1024 * 1024
  if (file.size > MAX_BYTES) {
    sizeError.value = '圖片檔案太大（上限 10 MB），請換一張或先壓縮'
    return
  }

  const objectUrl = URL.createObjectURL(file)
  const img = new Image()
  img.onload = () => {
    if (img.naturalWidth < props.minWidth || img.naturalHeight < props.minHeight) {
      resolutionError.value = `圖片解析度太低（至少需要 ${props.minWidth}×${props.minHeight}），請換一張品質較好的圖片`
      URL.revokeObjectURL(objectUrl)
      return
    }
    selectedFile.value = file
    localPreviewUrl.value = objectUrl
    emit('update:file', file)
  }
  img.onerror = () => {
    resolutionError.value = '這個檔案無法辨識為圖片，請重新選擇'
    URL.revokeObjectURL(objectUrl)
  }
  img.src = objectUrl
}

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) validateAndSet(file)
  input.value = ''
}

function handleDrop(event: DragEvent) {
  isDragOver.value = false
  const file = event.dataTransfer?.files?.[0]
  if (file) validateAndSet(file)
}

function handleRemove() {
  if (localPreviewUrl.value) URL.revokeObjectURL(localPreviewUrl.value)
  localPreviewUrl.value = null
  selectedFile.value = null
  resetErrors()
  emit('update:file', null)
}

const fileSizeLabel = computed(() => {
  if (!selectedFile.value) return ''
  return `${(selectedFile.value.size / (1024 * 1024)).toFixed(1)} MB`
})
</script>

<template>
  <div class="image-uploader">
    <input
      ref="fileInput"
      type="file"
      accept="image/*"
      capture="environment"
      class="image-uploader__native-input"
      @change="handleFileChange"
    >

    <div
      v-if="!hasImage"
      class="image-uploader__dropzone"
      :class="{ 'image-uploader__dropzone--drag-over': isDragOver }"
      @click="openFileDialog"
      @dragover.prevent="isDragOver = true"
      @dragleave.prevent="isDragOver = false"
      @drop.prevent="handleDrop"
    >
      <el-icon :size="32" color="var(--el-text-color-placeholder)"><Camera /></el-icon>
      <p class="image-uploader__hint">點擊或拖曳圖片到這裡上傳</p>
      <p class="image-uploader__note">
        支援常見的照片格式，檔案大小上限 10 MB。<br>
        系統會自動把圖片縮成適合網頁的大小，並清除照片內的位置資訊。
      </p>
    </div>

    <div v-else class="image-uploader__preview" :class="{ 'image-uploader__preview--error': !!saveError }">
      <img :src="previewUrl!" alt="" class="image-uploader__image">
      <button
        v-if="!saving"
        type="button"
        class="image-uploader__remove-btn"
        aria-label="移除圖片"
        @click="handleRemove"
      >
        <el-icon><Close /></el-icon>
      </button>
      <div v-if="saving" class="image-uploader__saving-overlay">
        <el-icon class="is-loading" :size="24"><Loading /></el-icon>
      </div>
    </div>

    <div v-if="hasImage" class="image-uploader__meta">
      <span v-if="selectedFile">{{ selectedFile.name }}・{{ fileSizeLabel }}</span>
      <el-button size="small" text type="primary" :disabled="saving" @click="openFileDialog">
        更換圖片
      </el-button>
    </div>

    <p v-if="resolutionError" class="image-uploader__error">{{ resolutionError }}</p>
    <p v-if="sizeError" class="image-uploader__error">{{ sizeError }}</p>
    <p v-if="saveError" class="image-uploader__error">這張圖片無法使用：{{ saveError }}，請重新選擇</p>
  </div>
</template>

<style scoped>
.image-uploader__native-input {
  display: none;
}

.image-uploader__dropzone {
  border: 1px dashed var(--el-border-color);
  border-radius: 4px;
  padding: 32px 16px;
  text-align: center;
  cursor: pointer;
  background: var(--el-fill-color-blank);
}

.image-uploader__dropzone--drag-over {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.image-uploader__hint {
  margin: 8px 0 4px;
  font-size: 14px;
}

.image-uploader__note {
  margin: 0;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}

.image-uploader__preview {
  position: relative;
  width: 160px;
  height: 160px;
  border-radius: 4px;
  overflow: hidden;
  border: 1px solid var(--el-border-color);
}

.image-uploader__preview--error {
  border-color: var(--el-color-danger);
}

.image-uploader__image {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.image-uploader__remove-btn {
  position: absolute;
  top: 4px;
  right: 4px;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  border: none;
  background: rgba(0, 0, 0, 0.55);
  color: #fff;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.image-uploader__saving-overlay {
  position: absolute;
  inset: 0;
  background: rgba(255, 255, 255, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
}

.image-uploader__meta {
  margin-top: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.image-uploader__error {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--el-color-danger);
}
</style>
