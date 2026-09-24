<script setup lang="ts">
/**
 * Hero 輪播影片上傳元件（v3.14，主站規劃書 §4.2 B3「圖／影片」，apps/api/README.md「S1-7b」）。
 *
 * 逐字比照 `ImageUploader.vue` 的既有原則（規劃書第 53 行／第 972 行「選檔不上傳、儲存才上傳……
 * 離開或取消表單不留下任何檔案」）：本元件完全不呼叫任何 API，只做三件事：
 *
 * 1. 選檔（或拖曳）→ 前端驗證格式（僅 MP4）／大小（上限 50 MB）→ 立刻顯示本機預覽
 *    （`URL.createObjectURL`，檔案只存在瀏覽器記憶體）。
 * 2. 把通過驗證的 `File` 物件透過 `update:file` 交給外層表單；外層表單按「儲存」時才跟其餘
 *    欄位、圖片檔案組成同一個 multipart 請求送出。
 * 3. 前端驗證只是防呆、不是最終把關——伺服器端仍會以 `ftyp` box 檔頭驗證容器格式與大小
 *    （見 `docs/17-deployment.md` §6），前端擋不掉的極端情況（例如檔名 `.mp4` 但內容其實是
 *    其他格式）留給後端擋下並回報中文錯誤。
 *
 * 跟 `ImageUploader` 不同的地方：影片沒有「移除」這個選項（跟圖片一樣，`banners` 沒有「不放
 * 影片」的狀態——要嘛換一支新影片，要嘛切回圖片模式讓後端清空 `video_key`），
 * 也不做 HEIC 轉檔／解析度檢查（規劃書沒有對 Hero 影片提出解析度下限的要求）。
 */
import { computed, onBeforeUnmount, ref, watch } from 'vue'

const MAX_BYTES = 50 * 1024 * 1024

const props = withDefaults(
  defineProps<{
    /** 使用者這次瀏覽階段選的新檔案（尚未上傳，只存在瀏覽器記憶體）。`v-model:file`。 */
    file: File | null
    /** 這則輪播目前（伺服器端）是否已經有影片——用來決定沒有 `file` 時要顯示拖放區還是
     * 「已上傳影片」提示。 */
    hasExistingVideo: boolean
    /** 外層表單儲存中時鎖住整個元件，避免儲存過程中使用者又換片。 */
    disabled?: boolean
  }>(),
  {
    disabled: false,
  },
)

const emit = defineEmits<{
  (e: 'update:file', file: File | null): void
}>()

const fileInput = ref<HTMLInputElement | null>(null)
const localPreviewUrl = ref<string | null>(null)
const isDragOver = ref(false)
const errorMessage = ref('')

watch(
  () => props.file,
  (file) => {
    if (localPreviewUrl.value) URL.revokeObjectURL(localPreviewUrl.value)
    localPreviewUrl.value = file ? URL.createObjectURL(file) : null
  },
  { immediate: true },
)

onBeforeUnmount(() => {
  if (localPreviewUrl.value) URL.revokeObjectURL(localPreviewUrl.value)
})

const showingExisting = computed(() => !props.file && props.hasExistingVideo)

function openFileDialog() {
  if (props.disabled) return
  fileInput.value?.click()
}

function resetErrors() {
  errorMessage.value = ''
}

/** 只認副檔名與 MIME 類型，不解析影片內容——內容驗證（`ftyp` box）留給伺服器端。 */
function looksLikeMp4(file: File): boolean {
  const nameIsMp4 = file.name.toLowerCase().endsWith('.mp4')
  const typeIsMp4 = file.type === '' || file.type === 'video/mp4'
  return nameIsMp4 && typeIsMp4
}

function handleFile(file: File) {
  resetErrors()

  if (!looksLikeMp4(file)) {
    errorMessage.value = '影片格式不支援，僅接受 MP4 格式的影片檔案，請重新選擇。'
    return
  }

  if (file.size > MAX_BYTES) {
    errorMessage.value = '影片檔案太大（上限 50 MB），請換一支較短或先壓縮過的影片。'
    return
  }

  emit('update:file', file)
}

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const selected = input.files?.[0]
  if (selected) handleFile(selected)
  input.value = ''
}

function handleDrop(event: DragEvent) {
  isDragOver.value = false
  if (props.disabled) return
  const dropped = event.dataTransfer?.files?.[0]
  if (dropped) handleFile(dropped)
}

const fileSizeLabel = computed(() => {
  if (!props.file) return ''
  return `${(props.file.size / (1024 * 1024)).toFixed(1)} MB`
})
</script>

<template>
  <div class="video-uploader">
    <input
      ref="fileInput"
      type="file"
      accept="video/mp4,.mp4"
      class="video-uploader__native-input"
      :disabled="disabled"
      @change="handleFileChange"
    >

    <!-- 沒有本機選取的新檔案，也沒有既有影片 -->
    <div
      v-if="!file && !showingExisting"
      class="video-uploader__dropzone"
      :class="{ 'video-uploader__dropzone--drag-over': isDragOver, 'video-uploader__dropzone--disabled': disabled }"
      @click="openFileDialog"
      @dragover.prevent="!disabled && (isDragOver = true)"
      @dragleave.prevent="isDragOver = false"
      @drop.prevent="handleDrop"
    >
      <el-icon :size="32" color="var(--el-text-color-placeholder)"><VideoCamera /></el-icon>
      <p class="video-uploader__hint">點擊或拖曳影片到這裡上傳</p>
      <p class="video-uploader__note">
        僅接受 MP4 格式，檔案大小上限 50 MB。<br>
        伺服器不會轉檔，請先確認影片本身能在瀏覽器正常播放。
      </p>
    </div>

    <!-- 有本機選取的新檔案：顯示影片預覽 -->
    <div v-else-if="file" class="video-uploader__preview">
      <video :src="localPreviewUrl!" controls class="video-uploader__video" />
      <div class="video-uploader__meta">
        <span>{{ file.name }}・{{ fileSizeLabel }}</span>
        <el-button size="small" text type="primary" :disabled="disabled" @click="openFileDialog">更換影片</el-button>
      </div>
    </div>

    <!-- 沒有本機選取的新檔案，但伺服器端已有影片：本機無法直接預覽（跟 ImageUploader 的既有限制同理） -->
    <div v-else class="video-uploader__existing" @click="openFileDialog">
      <el-icon :size="28" color="var(--admin-text-tertiary)"><Document /></el-icon>
      <p class="video-uploader__hint">已上傳影片</p>
      <p class="video-uploader__note">
        目前系統還無法在後台預覽已經上傳的影片。<br>
        如需確認內容，請直接點擊更換新影片。
      </p>
    </div>

    <p v-if="errorMessage" class="video-uploader__error">{{ errorMessage }}</p>
  </div>
</template>

<style scoped>
.video-uploader__native-input {
  display: none;
}

.video-uploader__dropzone {
  border: 1px dashed var(--admin-border-input);
  border-radius: 4px;
  padding: 32px 16px;
  text-align: center;
  cursor: pointer;
  background: var(--admin-bg-input);
}

.video-uploader__dropzone--disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.video-uploader__dropzone--drag-over {
  border-color: var(--admin-primary);
  background: rgb(232 91 169 / 8%);
}

.video-uploader__existing {
  position: relative;
  border: 1px dashed var(--admin-border-input);
  border-radius: 4px;
  padding: 24px 16px;
  text-align: center;
  cursor: pointer;
  background: var(--admin-bg-input);
}

.video-uploader__hint {
  margin: 8px 0 4px;
  font-size: 14px;
  color: var(--admin-text-primary);
}

.video-uploader__note {
  margin: 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.video-uploader__preview {
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  overflow: hidden;
}

.video-uploader__video {
  display: block;
  width: 100%;
  max-height: 240px;
  background: #000;
}

.video-uploader__meta {
  margin-top: 8px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.video-uploader__error {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--admin-danger-text);
}
</style>
