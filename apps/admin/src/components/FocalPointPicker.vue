<script setup lang="ts">
/**
 * 封面焦點選取器（規劃書 v3.28 §4.2 B2，docs/21 §3.4）。
 *
 * 在封面照片上點選或拖曳，標出「照片裡最重要的位置」；前台依不同版位裁切封面時會盡量保留這一點。
 * 數值是 0–100 的百分比（最多 2 位小數），兩者同為 `null` ＝ 沒指定，前台置中裁切。
 *
 * - 受控：`v-model:x`／`v-model:y`。換封面或移除封面時重設為 `null` 由外層（NewsEditView）負責。
 * - 旁邊兩個即時預覽框（頁首大圖比例約 16:5、列表卡片 3:2）用 `object-position` 呈現前台的實際裁切結果。
 * - 鍵盤：方向鍵微調 1%，按住 Shift 微調 5%；準星區塊是 `role="slider"`，附讀屏用的數值描述。
 * - 沒有圖片可選時（`src` 為空）只顯示說明，不顯示選取區。
 */
import { computed, ref } from 'vue'

const props = withDefaults(
  defineProps<{
    /** 要在上面選點的圖片網址（既有封面網址，或剛選檔的瀏覽器端預覽網址）。 */
    src: string | null
    disabled?: boolean
  }>(),
  { disabled: false },
)

const x = defineModel<number | null>('x', { required: true })
const y = defineModel<number | null>('y', { required: true })

const stage = ref<HTMLElement | null>(null)
const dragging = ref(false)

const isSet = computed(() => x.value !== null && y.value !== null)
const cx = computed(() => x.value ?? 50)
const cy = computed(() => y.value ?? 50)
const objectPosition = computed(() => `${cx.value}% ${cy.value}%`)

const valueText = computed(() =>
  isSet.value ? `水平 ${cx.value}%，垂直 ${cy.value}%` : '尚未指定，網站會置中裁切',
)

/** 夾在 0–100、四捨五入到 2 位小數。 */
function normalize(v: number): number {
  return Math.round(Math.min(100, Math.max(0, v)) * 100) / 100
}

function setFromPointer(event: PointerEvent) {
  const el = stage.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  if (rect.width <= 0 || rect.height <= 0) return
  x.value = normalize(((event.clientX - rect.left) / rect.width) * 100)
  y.value = normalize(((event.clientY - rect.top) / rect.height) * 100)
}

function onPointerDown(event: PointerEvent) {
  if (props.disabled) return
  dragging.value = true
  ;(event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId)
  setFromPointer(event)
}

function onPointerMove(event: PointerEvent) {
  if (dragging.value) setFromPointer(event)
}

function onPointerUp(event: PointerEvent) {
  dragging.value = false
  ;(event.currentTarget as HTMLElement).releasePointerCapture?.(event.pointerId)
}

function onKeydown(event: KeyboardEvent) {
  if (props.disabled) return
  const step = event.shiftKey ? 5 : 1
  let dx = 0
  let dy = 0
  switch (event.key) {
    case 'ArrowLeft': dx = -step; break
    case 'ArrowRight': dx = step; break
    case 'ArrowUp': dy = -step; break
    case 'ArrowDown': dy = step; break
    default: return
  }
  event.preventDefault()
  x.value = normalize(cx.value + dx)
  y.value = normalize(cy.value + dy)
}

function reset() {
  x.value = null
  y.value = null
}
</script>

<template>
  <div class="focal-picker">
    <p v-if="!src" class="focal-picker__empty">上傳封面圖片後，就能在這裡指定照片裡最重要的位置。</p>
    <template v-else>
      <p class="focal-picker__hint">點選照片中最重要的位置，網站裁切照片時會保留這一塊。也可以用方向鍵微調。</p>
      <div
        ref="stage"
        class="focal-picker__stage"
        :class="{ 'focal-picker__stage--disabled': disabled }"
        role="slider"
        tabindex="0"
        aria-label="封面焦點位置"
        aria-valuemin="0"
        aria-valuemax="100"
        :aria-valuenow="Math.round(cx)"
        :aria-valuetext="valueText"
        :aria-disabled="disabled"
        @pointerdown.prevent="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointercancel="onPointerUp"
        @keydown="onKeydown"
      >
        <img :src="src" alt="" class="focal-picker__image" draggable="false">
        <span class="focal-picker__crosshair" :class="{ 'focal-picker__crosshair--unset': !isSet }" :style="{ left: `${cx}%`, top: `${cy}%` }" aria-hidden="true" />
      </div>
      <div class="focal-picker__actions">
        <span class="focal-picker__value">{{ valueText }}</span>
        <el-button size="small" text :disabled="disabled || !isSet" @click="reset">重設為置中</el-button>
      </div>

      <div class="focal-picker__previews">
        <figure class="focal-picker__preview">
          <div class="focal-picker__frame focal-picker__frame--hero">
            <img :src="src" alt="" :style="{ objectPosition }" draggable="false">
          </div>
          <figcaption>文章頁首大圖</figcaption>
        </figure>
        <figure class="focal-picker__preview">
          <div class="focal-picker__frame focal-picker__frame--card">
            <img :src="src" alt="" :style="{ objectPosition }" draggable="false">
          </div>
          <figcaption>列表卡片</figcaption>
        </figure>
      </div>
    </template>
  </div>
</template>

<style scoped>
.focal-picker {
  width: 100%;
}

.focal-picker__empty,
.focal-picker__hint {
  margin: 0 0 8px;
  font-size: 12px;
  line-height: 1.6;
  color: var(--admin-text-tertiary);
}

/* 中性看片台（docs/21 §9.1）：底色固定、不隨主題變化 */
.focal-picker__stage {
  position: relative;
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  overflow: hidden;
  background: var(--admin-lightbox-neutral);
  cursor: crosshair;
  touch-action: none;
  user-select: none;
}

.focal-picker__stage:focus-visible {
  outline: 2px solid var(--admin-primary);
  outline-offset: 2px;
}

.focal-picker__stage--disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.focal-picker__image {
  display: block;
  width: 100%;
  height: auto;
  pointer-events: none;
}

/* 準星：白圈加深色外圈，在亮暗照片上都看得到 */
.focal-picker__crosshair {
  position: absolute;
  width: 22px;
  height: 22px;
  margin: -11px 0 0 -11px;
  border: 2px solid #fff;
  border-radius: 50%;
  box-shadow: 0 0 0 1px rgb(0 0 0 / 60%), inset 0 0 0 1px rgb(0 0 0 / 60%);
  pointer-events: none;
}

.focal-picker__crosshair::after {
  content: '';
  position: absolute;
  left: 50%;
  top: 50%;
  width: 4px;
  height: 4px;
  margin: -2px 0 0 -2px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 0 0 1px rgb(0 0 0 / 60%);
}

.focal-picker__crosshair--unset {
  opacity: 0.55;
  border-style: dashed;
}

.focal-picker__actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin: 6px 0 12px;
}

.focal-picker__value {
  font-size: 12px;
  color: var(--admin-text-secondary);
}

.focal-picker__previews {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 12px;
}

.focal-picker__preview {
  margin: 0;
  min-width: 0;
}

.focal-picker__preview figcaption {
  margin-top: 4px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.focal-picker__frame {
  width: 100%;
  overflow: hidden;
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  background: var(--admin-lightbox-neutral);
}

.focal-picker__frame--hero {
  aspect-ratio: 16 / 5;
}

.focal-picker__frame--card {
  aspect-ratio: 3 / 2;
}

.focal-picker__frame img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
  pointer-events: none;
}
</style>
