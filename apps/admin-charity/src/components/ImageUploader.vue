<script setup lang="ts">
/**
 * 圖片上傳元件（docs/22-charity-ui.md §3.6 沿用 docs/21 §9 功能規則）。
 *
 * 中性看片台（§3.6）：淺色系統下前台暖灰、後台冷灰的介面色相仍會輕微影響照片判色，
 * 所以看片台維持與整體介面色相脫鉤的中性灰（沿用 docs/21 §9.1 既有定案值，不重新驗算）。
 * - 選檔只在瀏覽器端產生預覽，不送出、不寫入（這份 mockup 沒有後端）。
 */
import { computed, ref } from 'vue'

const props = withDefaults(
  defineProps<{
    existingUrl?: string | null
    minWidth?: number
    minHeight?: number
    saving?: boolean
    saveError?: string | null
    /** 一般照片用中性灰、可能含透明通道的圖片（隊徽等）用棋盤格 */
    variant?: 'photo' | 'logo'
  }>(),
  {
    existingUrl: null,
    minWidth: 800,
    minHeight: 800,
    saving: false,
    saveError: null,
    variant: 'photo',
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
    <input ref="fileInput" type="file" accept="image/*" class="image-uploader__native-input" @change="handleFileChange">

    <div
      v-if="!hasImage"
      class="image-uploader__dropzone"
      :class="{ 'image-uploader__dropzone--drag-over': isDragOver }"
      @click="openFileDialog"
      @dragover.prevent="isDragOver = true"
      @dragleave.prevent="isDragOver = false"
      @drop.prevent="handleDrop"
    >
      <el-icon :size="32" color="var(--charity-admin-text-tertiary)"><Camera /></el-icon>
      <p class="image-uploader__hint">點擊或拖曳圖片到這裡上傳</p>
      <p class="image-uploader__note">
        支援常見的照片格式，檔案大小上限 10 MB。<br>
        系統會自動把圖片縮成適合網頁的大小，並清除照片內的位置資訊。
      </p>
    </div>

    <div
      v-else
      class="image-uploader__preview"
      :class="[`image-uploader__preview--${variant}`, { 'image-uploader__preview--error': !!saveError }]"
    >
      <img :src="previewUrl!" alt="" class="image-uploader__image">
      <button v-if="!saving" type="button" class="image-uploader__remove-btn" aria-label="移除圖片" @click="handleRemove">
        <el-icon><Close /></el-icon>
      </button>
      <div v-if="saving" class="image-uploader__saving-overlay">
        <el-icon class="is-loading" :size="24"><Loading /></el-icon>
      </div>
    </div>

    <div v-if="hasImage" class="image-uploader__meta">
      <span v-if="selectedFile">{{ selectedFile.name }}・{{ fileSizeLabel }}</span>
      <el-button size="small" text type="primary" :disabled="saving" @click="openFileDialog">更換圖片</el-button>
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
  border: 1px dashed var(--charity-admin-border-input);
  border-radius: 4px;
  padding: 32px 16px;
  text-align: center;
  cursor: pointer;
  background: var(--charity-admin-bg-page);
}

.image-uploader__dropzone--drag-over {
  border-color: var(--charity-admin-primary);
  background: var(--charity-admin-bg-surface-2);
}

.image-uploader__hint {
  margin: 8px 0 4px;
  font-size: 14px;
  color: var(--charity-admin-text-primary);
}

.image-uploader__note {
  margin: 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.6;
}

.image-uploader__preview {
  position: relative;
  width: 160px;
  height: 160px;
  border-radius: 4px;
  overflow: hidden;
  border: 1px solid var(--charity-admin-border);
}

.image-uploader__preview--photo {
  background: var(--charity-lightbox-neutral);
}

.image-uploader__preview--logo {
  background-image:
    linear-gradient(45deg, var(--charity-lightbox-checker-b) 25%, transparent 25%),
    linear-gradient(-45deg, var(--charity-lightbox-checker-b) 25%, transparent 25%),
    linear-gradient(45deg, transparent 75%, var(--charity-lightbox-checker-b) 75%),
    linear-gradient(-45deg, transparent 75%, var(--charity-lightbox-checker-b) 75%);
  background-size: 16px 16px;
  background-position: 0 0, 0 8px, 8px -8px, -8px 0;
  background-color: var(--charity-lightbox-checker-a);
}

.image-uploader__preview--error {
  border-color: var(--charity-danger-text);
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
  color: #ffffff;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.image-uploader__saving-overlay {
  position: absolute;
  inset: 0;
  background: rgba(255, 255, 255, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--charity-admin-text-primary);
}

.image-uploader__meta {
  margin-top: 8px;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.image-uploader__error {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--charity-danger-text);
}
</style>
