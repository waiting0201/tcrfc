<script setup lang="ts">
/**
 * 圖片上傳元件（docs/21-admin-ui.md §9，規劃書 §4.0 後台圖片上傳通則）。
 *
 * 🔴🔴🔴 S0-8 修正（2026-09-22，規劃書第 53 行／第 972 行「選檔不上傳、儲存才上傳……離開或取消
 * 表單不留下任何檔案」）：本元件**完全不呼叫任何 API**。舊版接的是後端當時的兩段式契約（選檔後
 * 立刻呼叫獨立上傳端點拿物件鍵），後端已經把該契約整支移除、改成建立／更新文章時圖片跟其餘欄位
 * 一起送出的單一 `multipart/form-data` 請求（見 `apps/api/README.md`「圖片上傳共用元件」整節、
 * `apps/admin/src/api/adminNews.ts`）。本元件現在只做三件事：
 *
 * 1. 選檔（或拖曳）→ HEIC 在瀏覽器端轉成 JPEG（`docs/17-deployment.md` §6）→ 前端驗證格式／
 *    大小／解析度 → 立刻顯示本機預覽（`URL.createObjectURL`，檔案只存在瀏覽器記憶體）。
 * 2. 把通過驗證的 `File` 物件（不是物件鍵字串——選檔階段根本還沒有物件鍵，因為根本沒有上傳）
 *    透過 `update:file` 交給外層表單；外層表單（`NewsEditView.vue`）把這個 `File` 存在自己的
 *    狀態裡，按「儲存」時才跟其餘欄位組成同一個 multipart 請求送出。
 * 3. 表達「移除既有封面圖片」的意圖（`update:removeCover`）——這是後端封面圖片三態
 *    （換新／清空／維持不變）裡「清空」那一態在畫面上的表達方式，同樣要等按「儲存」才真的生效。
 *
 * 本元件是完全受控元件（controlled component）：`file`／`removeCover` 都是 prop，不是內部
 * state，由外層表單（`form` 以外的獨立狀態，見 `NewsEditView.vue` 的 `coverFile`／`removeCover`）
 * 持有並在載入／存檔成功後重置——這樣「離開或取消表單不留下任何檔案」自然成立：使用者選了圖片
 * 但沒按儲存就離開，`File` 物件只是被瀏覽器分頁的記憶體釋放，從來沒有任何 HTTP 請求送出過。
 *
 * ⚠️ **已知限制**：既有封面圖片（`hasExistingImage` 為真、這次瀏覽階段沒有選過新檔案）沒有辦法
 * 在後台預覽——物件儲存容器目前是私有（`PublicAccessType.None`），也還沒有任何「用物件鍵換可
 * 顯示網址」的端點（見 `docs/21-admin-ui.md` §9 與 `apps/admin/README.md` 的說明）。這種情況畫面
 * 上顯示「已上傳但無法預覽」的提示區塊，不是空的上傳框（空框會讓人誤以為沒有圖片），也不是硬把
 * 物件鍵塞進 `<img src>` 假裝能顯示。等後端補上這個管道時，呼叫端把換算出來的網址傳進
 * `existingPreviewUrl` 就會自動改顯示真正的預覽圖，本元件不需要再改。
 */
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { convertHeicIfNeeded, looksUnsupportedFormat, readImageDimensions, HeicConversionError } from '@/utils/imageFile'

const props = withDefaults(
  defineProps<{
    /** 使用者這次瀏覽階段選的新檔案（尚未上傳，只存在瀏覽器記憶體）。`v-model:file`。 */
    file: File | null
    /** 使用者是否要求「儲存時清空封面圖片」（後端封面圖片三態的「清空」那一態）。`v-model:remove-cover`。 */
    removeCover: boolean
    /** 這篇資料目前（伺服器端）是否已經有封面圖片——用來決定沒有 `file` 時要顯示拖放區還是
     * 「已上傳但無法預覽」卡片，不受 `removeCover` 影響（`removeCover` 只是「意圖」，儲存前
     * 伺服器端的既有圖片並沒有真的被動到）。 */
    hasExistingImage: boolean
    /**
     * 如果呼叫端有辦法把既有封面圖片換成一個可以直接顯示的網址就傳進來（目前系統還沒有這個
     * 管道，見上方檔頭「已知限制」），沒有就留 `null`，元件會改顯示「已上傳但無法預覽」。
     */
    existingPreviewUrl?: string | null
    minWidth?: number
    minHeight?: number
    /** 外層表單儲存中時鎖住整個元件，避免儲存過程中使用者又換圖。 */
    disabled?: boolean
    /**
     * 中性看片台的底色（docs/21-admin-ui.md §9.1）：一般照片用中性灰、
     * 可能含透明通道的圖片（隊徽、去背標誌）用棋盤格，讓透明邊緣清楚可辨。
     */
    variant?: 'photo' | 'logo'
  }>(),
  {
    existingPreviewUrl: null,
    minWidth: 1600,
    minHeight: 900,
    disabled: false,
    variant: 'photo',
  },
)

const emit = defineEmits<{
  (e: 'update:file', file: File | null): void
  (e: 'update:removeCover', value: boolean): void
}>()

const fileInput = ref<HTMLInputElement | null>(null)
const localPreviewUrl = ref<string | null>(null)
const isDragOver = ref(false)
/** HEIC 轉檔／解析度讀取這段短暫的非同步處理期間——不是上傳中，本元件不再呼叫任何網路請求。 */
const isBusy = ref(false)
/** 選檔／轉檔／解析度檢查失敗時顯示，內容是前端寫死的文案（本元件已經不呼叫 API，不會再有
 * 後端 ProblemDetails 需要顯示）。 */
const errorMessage = ref('')

// 受控元件：本機預覽網址由 `props.file` 推導，不是元件自己的選檔結果，這樣外層表單把
// `file` 重置為 `null`（載入新資料、存檔成功後）時，預覽會自動跟著清空。
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

/** 這次瀏覽階段沒有選新檔案、伺服器端目前有封面圖片、也沒有標記要清空——顯示既有圖片的狀態
 * （不管看不看得到預覽）。`removeCover` 為真時視同「目前要顯示的是空的」，即使伺服器端的
 * 既有圖片其實還沒被動到（要等按下儲存）。 */
const showingExisting = computed(() => !props.file && props.hasExistingImage && !props.removeCover)

/** 本機預覽優先；沒有本機預覽但呼叫端有給既有圖片的可顯示網址時才用它（見檔頭「已知限制」）。 */
const previewUrl = computed(() => {
  if (localPreviewUrl.value) return localPreviewUrl.value
  return showingExisting.value ? props.existingPreviewUrl ?? null : null
})
const hasPreviewableImage = computed(() => Boolean(previewUrl.value))
/** 有既有圖片，但這個瀏覽階段沒有選過新檔案、呼叫端也沒給可顯示網址——已經有圖片，只是顯示不出來。 */
const hasUnpreviewableExisting = computed(() => showingExisting.value && !hasPreviewableImage.value)

function openFileDialog() {
  if (props.disabled || isBusy.value) return
  fileInput.value?.click()
}

function resetErrors() {
  errorMessage.value = ''
}

async function handleFile(rawFile: File) {
  resetErrors()

  const MAX_BYTES = 10 * 1024 * 1024

  let file: File
  isBusy.value = true
  try {
    file = await convertHeicIfNeeded(rawFile)
  } catch (cause) {
    errorMessage.value = cause instanceof HeicConversionError
      ? '這個 HEIC 圖片檔案無法自動轉換成一般格式，請改用手機的「共享／轉存成 JPG」功能處理後再上傳，或換一張圖片。'
      : '圖片轉檔時發生未預期的錯誤，請換一張圖片再試一次。'
    isBusy.value = false
    return
  }

  if (looksUnsupportedFormat(file)) {
    errorMessage.value = '圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。'
    isBusy.value = false
    return
  }

  if (file.size > MAX_BYTES) {
    errorMessage.value = '圖片檔案太大（上限 10 MB），請換一張或先壓縮'
    isBusy.value = false
    return
  }

  let dimensions: { width: number; height: number }
  try {
    dimensions = await readImageDimensions(file)
  } catch {
    errorMessage.value = '這個檔案無法辨識為圖片，請重新選擇'
    isBusy.value = false
    return
  }

  if (dimensions.width < props.minWidth || dimensions.height < props.minHeight) {
    errorMessage.value = `圖片解析度太低（至少需要 ${props.minWidth}×${props.minHeight}），請換一張品質較好的圖片`
    isBusy.value = false
    return
  }

  // 通過前端檢查：交給外層表單，本元件不碰任何 API。選新檔案代表「取消清空封面」的意圖
  // （後端契約：`file` 與 `removeCover` 互斥，換新圖優先）。
  isBusy.value = false
  emit('update:file', file)
  emit('update:removeCover', false)
}

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const selected = input.files?.[0]
  if (selected) handleFile(selected)
  input.value = ''
}

function handleDrop(event: DragEvent) {
  isDragOver.value = false
  if (props.disabled || isBusy.value) return
  const dropped = event.dataTransfer?.files?.[0]
  if (dropped) handleFile(dropped)
}

/** 移除目前顯示的圖片。正在預覽的是「這次選的新檔案」就只是取消這次選擇（回到原本的狀態，
 * 不影響伺服器端既有圖片）；正在顯示的是「既有圖片」（不管看不看得到）才真的標記
 * `removeCover`，等按下儲存才會真的清空。 */
function handleRemove() {
  resetErrors()
  if (props.file) {
    emit('update:file', null)
    return
  }
  if (props.hasExistingImage) {
    emit('update:removeCover', true)
  }
}

const fileSizeLabel = computed(() => {
  if (!props.file) return ''
  return `${(props.file.size / (1024 * 1024)).toFixed(1)} MB`
})
</script>

<template>
  <div class="image-uploader">
    <input
      ref="fileInput"
      type="file"
      accept="image/*,.heic,.heif"
      capture="environment"
      class="image-uploader__native-input"
      :disabled="disabled || isBusy"
      @change="handleFileChange"
    >

    <!-- 完全沒有圖片：拖放區（含「已標記清空既有圖片」的情況——使用者看到的就是儲存後的結果） -->
    <div
      v-if="!hasPreviewableImage && !hasUnpreviewableExisting"
      class="image-uploader__dropzone"
      :class="{ 'image-uploader__dropzone--drag-over': isDragOver, 'image-uploader__dropzone--disabled': disabled || isBusy }"
      @click="openFileDialog"
      @dragover.prevent="!disabled && !isBusy && (isDragOver = true)"
      @dragleave.prevent="isDragOver = false"
      @drop.prevent="handleDrop"
    >
      <el-icon :size="32" color="var(--el-text-color-placeholder)">
        <Loading v-if="isBusy" class="is-loading" />
        <Camera v-else />
      </el-icon>
      <p class="image-uploader__hint">{{ isBusy ? '圖片處理中…' : '點擊或拖曳圖片到這裡上傳' }}</p>
      <p class="image-uploader__note">
        支援常見的照片格式，檔案大小上限 10 MB。<br>
        系統會自動把圖片縮成適合網頁的大小，並清除照片內的位置資訊。
      </p>
    </div>

    <!-- 既有圖片，但這個瀏覽階段沒有可顯示的網址：見檔頭「已知限制」 -->
    <div
      v-else-if="hasUnpreviewableExisting"
      class="image-uploader__existing"
      @click="openFileDialog"
    >
      <el-icon :size="28" color="var(--admin-text-tertiary)"><Document /></el-icon>
      <p class="image-uploader__hint">已上傳圖片</p>
      <p class="image-uploader__note">
        目前系統還無法在後台預覽已經上傳的圖片。<br>
        如需確認內容，請直接點擊更換新圖片。
      </p>
      <button
        v-if="!disabled"
        type="button"
        class="image-uploader__remove-btn"
        aria-label="移除封面圖片"
        @click.stop="handleRemove"
      >
        <el-icon><Close /></el-icon>
      </button>
    </div>

    <!-- 有可顯示的預覽（這次選的新檔案） -->
    <div
      v-else
      class="image-uploader__preview"
      :class="[`image-uploader__preview--${variant}`, { 'image-uploader__preview--error': !!errorMessage }]"
    >
      <img :src="previewUrl!" alt="" class="image-uploader__image">
      <button
        v-if="!disabled"
        type="button"
        class="image-uploader__remove-btn"
        aria-label="移除圖片"
        @click="handleRemove"
      >
        <el-icon><Close /></el-icon>
      </button>
    </div>

    <div v-if="hasPreviewableImage || hasUnpreviewableExisting" class="image-uploader__meta">
      <span v-if="file">{{ file.name }}・{{ fileSizeLabel }}</span>
      <el-button size="small" text type="primary" :disabled="disabled || isBusy" @click="openFileDialog">
        更換圖片
      </el-button>
    </div>

    <p v-if="errorMessage" class="image-uploader__error">{{ errorMessage }}</p>
  </div>
</template>

<style scoped>
.image-uploader__native-input {
  display: none;
}

.image-uploader__dropzone {
  border: 1px dashed var(--admin-border-input);
  border-radius: 4px;
  padding: 32px 16px;
  text-align: center;
  cursor: pointer;
  background: var(--admin-bg-input);
}

.image-uploader__dropzone--disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.image-uploader__dropzone--drag-over {
  border-color: var(--admin-primary);
  /* rgb(232 91 169 / 8%) = --admin-primary #E85BA9 的 rgb 等效值。這是拖曳提示疊色，
     不是 §9.1 的中性看片台（那是選圖後的預覽底色，兩者是不同區塊），整批換色時要跟著換 */
  background: rgb(232 91 169 / 8%);
}

.image-uploader__existing {
  position: relative;
  border: 1px dashed var(--admin-border-input);
  border-radius: 4px;
  padding: 24px 16px;
  text-align: center;
  cursor: pointer;
  background: var(--admin-bg-input);
}

.image-uploader__hint {
  margin: 8px 0 4px;
  font-size: 14px;
  color: var(--admin-text-primary);
}

.image-uploader__note {
  margin: 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

/* 中性看片台（docs/21-admin-ui.md §9.1）：底色固定，不隨頁面主題變化，讓照片判色與去背邊緣
   永遠有一個恆定的參考面——與 §7 的四層背景色階脫鉤，就算之後真的加了淺色模式也不受影響 */
.image-uploader__preview {
  position: relative;
  width: 160px;
  height: 160px;
  border-radius: 4px;
  overflow: hidden;
  border: 1px solid var(--admin-border);
}

.image-uploader__preview--photo {
  background: var(--admin-lightbox-neutral);
}

.image-uploader__preview--logo {
  background-image:
    linear-gradient(45deg, var(--admin-lightbox-checker-b) 25%, transparent 25%),
    linear-gradient(-45deg, var(--admin-lightbox-checker-b) 25%, transparent 25%),
    linear-gradient(45deg, transparent 75%, var(--admin-lightbox-checker-b) 75%),
    linear-gradient(-45deg, transparent 75%, var(--admin-lightbox-checker-b) 75%);
  background-size: 16px 16px;
  background-position:
    0 0,
    0 8px,
    8px -8px,
    -8px 0;
  background-color: var(--admin-lightbox-checker-a);
}

.image-uploader__preview--error {
  border-color: var(--admin-danger-text);
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

.image-uploader__meta {
  margin-top: 8px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.image-uploader__error {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--admin-danger-text);
}
</style>
