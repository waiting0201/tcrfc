<script setup lang="ts">
/**
 * 圖集管理（贊助活動、慈善計畫、事蹟紀錄的多張圖片）。
 *
 * 跟單張圖片欄位（`ImageUploader`，選檔後等按「儲存」才上傳）不同：圖集的後端契約是**每張圖
 * 一支獨立端點**（一次一張、立刻寫入），所以這裡的「新增圖片」「移除」「調整順序」都是**立刻生效**，
 * 不受外層表單的「儲存」按鈕影響。畫面文字要講清楚這件事，避免使用者以為要再按儲存。
 * 元件本身不呼叫 API，由外層透過 `onUpload`／`onRemove`／`onReorder` 三個函式接手（回傳 Promise），
 * 外層負責在成功後更新 `images`。
 */
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { AdminApiError } from '@/api/http'

export interface GalleryItem {
  id: string
  thumbUrl?: string | null
  imageUrl?: string | null
}

const props = withDefaults(
  defineProps<{
    images: GalleryItem[]
    disabled?: boolean
    onUpload: (file: File) => Promise<void>
    onRemove: (id: string) => Promise<void>
    onReorder: (ids: string[]) => Promise<void>
  }>(),
  { disabled: false },
)

const input = ref<HTMLInputElement | null>(null)
const busy = ref(false)

const MAX_BYTES = 10 * 1024 * 1024
const ALLOWED = ['image/jpeg', 'image/png', 'image/webp']

function fail(error: unknown, fallback: string) {
  ElMessage.error(error instanceof AdminApiError ? error.message : fallback)
}

async function handleFiles(event: Event) {
  const el = event.target as HTMLInputElement
  const files = Array.from(el.files ?? [])
  el.value = ''
  if (files.length === 0) return
  busy.value = true
  try {
    for (const file of files) {
      if (!ALLOWED.includes(file.type)) {
        ElMessage.warning(`「${file.name}」格式不支援，請使用 JPG、PNG 或 WebP 圖片`)
        continue
      }
      if (file.size > MAX_BYTES) {
        ElMessage.warning(`「${file.name}」超過 10 MB，請先壓縮`)
        continue
      }
      try {
        await props.onUpload(file)
      } catch (error) {
        fail(error, `「${file.name}」上傳失敗，請稍後再試`)
      }
    }
  } finally {
    busy.value = false
  }
}

async function handleRemove(item: GalleryItem) {
  try {
    await ElMessageBox.confirm('確定要移除這張圖片嗎？移除後立即生效。', '移除圖片', {
      confirmButtonText: '移除',
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  busy.value = true
  try {
    await props.onRemove(item.id)
  } catch (error) {
    fail(error, '移除失敗，請稍後再試')
  } finally {
    busy.value = false
  }
}

async function move(index: number, delta: -1 | 1) {
  const ids = props.images.map((i) => i.id)
  const target = index + delta
  if (target < 0 || target >= ids.length) return
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  busy.value = true
  try {
    await props.onReorder(ids)
  } catch (error) {
    fail(error, '調整順序失敗，請稍後再試')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="gallery">
    <p class="gallery__hint">圖片新增、移除與順序調整會立即生效，不需要再按「儲存」。單張上限 10 MB，可一次選多張。</p>
    <div v-if="images.length > 0" class="gallery__grid">
      <div v-for="(item, index) in images" :key="item.id" class="gallery__item">
        <img v-if="item.thumbUrl || item.imageUrl" :src="(item.thumbUrl || item.imageUrl)!" alt="" class="gallery__img">
        <div v-else class="gallery__img gallery__img--empty">無法預覽</div>
        <div v-if="!disabled" class="gallery__tools">
          <el-button size="small" text :disabled="busy || index === 0" aria-label="往前移" @click="move(index, -1)">
            <el-icon><ArrowLeft /></el-icon>
          </el-button>
          <el-button size="small" text :disabled="busy || index === images.length - 1" aria-label="往後移" @click="move(index, 1)">
            <el-icon><ArrowRight /></el-icon>
          </el-button>
          <el-button size="small" text type="danger" :disabled="busy" aria-label="移除圖片" @click="handleRemove(item)">
            <el-icon><Delete /></el-icon>
          </el-button>
        </div>
      </div>
    </div>
    <p v-else class="gallery__empty">目前還沒有圖片。</p>
    <input ref="input" type="file" multiple accept="image/jpeg,image/png,image/webp" class="gallery__input" @change="handleFiles">
    <el-button v-if="!disabled" :loading="busy" @click="input?.click()">+ 新增圖片</el-button>
  </div>
</template>

<style scoped>
.gallery__hint {
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.gallery__empty {
  margin: 0 0 8px;
  font-size: 13px;
  color: var(--admin-text-tertiary);
}

.gallery__input {
  display: none;
}

.gallery__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 10px;
  margin-bottom: 10px;
}

.gallery__item {
  min-width: 0;
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  overflow: hidden;
  background: var(--admin-bg-surface);
}

.gallery__img {
  display: block;
  width: 100%;
  aspect-ratio: 4 / 3;
  object-fit: cover;
  background: var(--admin-lightbox-neutral);
}

.gallery__img--empty {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.gallery__tools {
  display: flex;
  justify-content: space-between;
  padding: 2px 4px;
}
</style>
