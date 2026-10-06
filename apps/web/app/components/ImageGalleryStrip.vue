<script setup lang="ts">
// app/components/ImageGalleryStrip.vue — 縮圖列＋點擊放大的圖集燈箱（B-21）
//
// 用於贊助活動紀錄、慈善事蹟：後台可傳多張圖，前台過去只顯示前 2～3 張。這裡全部列出縮圖，
// 點任一張以原生 `<dialog>` 放大（內建 focus trap、Esc 關閉、::backdrop），可用上一張／下一張切換。
// 不載入任何第三方腳本；只在第一次點擊後才渲染大圖（不增加首屏圖片請求）。
interface GalleryImage { imageUrl: string, thumbUrl?: string | null }
const props = defineProps<{
  images: readonly GalleryImage[]
  /** 圖集的描述（用於縮圖按鈕與大圖的替代文字，例如活動名稱）。 */
  label: string
}>()

const { tx } = useLocale()
const dialogEl = ref<HTMLDialogElement | null>(null)
const index = ref(0)
const opened = ref(false)
const total = computed(() => props.images.length)
const current = computed(() => props.images[index.value] ?? null)

function open(i: number) {
  index.value = i
  opened.value = true
  nextTick(() => dialogEl.value?.showModal())
}
function step(delta: number) {
  index.value = (index.value + delta + total.value) % total.value
}
function onDialogClick(e: MouseEvent) {
  // 點到 <dialog> 自身（即 ::backdrop 區域）視同關閉
  if (e.target === dialogEl.value) dialogEl.value?.close()
}
function onKey(e: KeyboardEvent) {
  if (e.key === 'ArrowLeft') step(-1)
  else if (e.key === 'ArrowRight') step(1)
}
</script>

<template>
  <div v-if="total" class="gallery-strip">
    <button
      v-for="(img, i) in images"
      :key="`${img.imageUrl}-${i}`"
      type="button"
      class="gallery-strip__thumb"
      :aria-label="tx(`放大檢視第 ${i + 1} 張，共 ${total} 張：${label}`, `View photo ${i + 1} of ${total}: ${label}`)"
      @click="open(i)"
    >
      <img :src="img.thumbUrl ?? img.imageUrl" alt="" loading="lazy" width="96" height="64">
    </button>
    <dialog v-if="opened" ref="dialogEl" class="gallery-dialog" :aria-label="label" @click="onDialogClick" @keydown="onKey" @close="opened = false">
      <div class="gallery-dialog__inner">
        <img v-if="current" class="gallery-dialog__img" :src="current.imageUrl" :alt="tx(`${label}（第 ${index + 1} 張，共 ${total} 張）`, `${label} (photo ${index + 1} of ${total})`)">
        <div class="gallery-dialog__bar">
          <button v-if="total > 1" type="button" class="btn btn--light btn--sm" @click="step(-1)">{{ tx('上一張', 'Previous') }}</button>
          <span class="gallery-dialog__count" role="status">{{ index + 1 }} / {{ total }}</span>
          <button v-if="total > 1" type="button" class="btn btn--light btn--sm" @click="step(1)">{{ tx('下一張', 'Next') }}</button>
          <button type="button" class="btn btn--primary btn--sm" @click="dialogEl?.close()">{{ tx('關閉', 'Close') }}</button>
        </div>
      </div>
    </dialog>
  </div>
</template>

<style>
.gallery-strip{ display:flex; flex-wrap:wrap; gap:.4rem; margin-top:.6rem; }
.gallery-strip__thumb{ padding:0; border:0; background:none; cursor:zoom-in; line-height:0; }
.gallery-strip__thumb img{ width:96px; height:64px; object-fit:cover; display:block; }
.gallery-strip__thumb:focus-visible{ outline:2px solid var(--brand-aa); outline-offset:2px; }
.gallery-dialog{ border:0; padding:0; background:var(--ink); color:#fff; max-width:min(96vw, 1100px); width:100%; max-height:92vh; }
.gallery-dialog::backdrop{ background:rgba(0,0,0,.75); }
.gallery-dialog__inner{ display:flex; flex-direction:column; max-height:92vh; }
.gallery-dialog__img{ display:block; max-width:100%; max-height:calc(92vh - 4.5rem); width:auto; height:auto; margin:0 auto; object-fit:contain; }
.gallery-dialog__bar{ display:flex; align-items:center; justify-content:center; gap:.75rem; flex-wrap:wrap; padding:.75rem 1rem; }
.gallery-dialog__count{ font-size:.85rem; font-weight:700; }
</style>
