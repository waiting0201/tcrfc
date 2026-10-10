<script setup lang="ts">
/**
 * 圖片說明欄（規劃書 §4.0 圖片欄位組：物件鍵＋寬＋高＋雙語替代文字）。
 *
 * 放在 ImageUploader 的旁邊（同一個 FormSection、同一個 LangTabsBar 內），提供：
 * 1. 目前圖片尺寸提示（有圖且後端回了寬高才顯示）；
 * 2. 中英「圖片說明」雙語欄（欄位鍵 `{field}Zh`／`{field}En`，對應後端 `{名稱}AltZh`／`{名稱}AltEn` 驗證鍵）；
 * 3. 有圖片卻沒有中文說明時的溫和提示（只提示、不擋存檔，規劃書未要求必填）。
 *
 * 比照新聞編輯頁封面的既有做法，抽成共用元件，各編輯頁不再各寫一份。
 * `hasImage`：已存圖片、或剛選了新圖，且沒有勾選移除。
 */
import { computed } from 'vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageSizeHint from '@/components/ImageSizeHint.vue'

const props = withDefaults(
  defineProps<{
    /** 欄位鍵基底，例如 `heroAlt`（錯誤鍵 `heroAltZh`／`heroAltEn`）。 */
    field: string
    zh: string
    en: string
    hasImage: boolean
    width?: number | null
    height?: number | null
    /** 沒有說明時前台會改念的文字，用在提示句，例如「球員姓名」。 */
    fallback?: string
    label?: string
    /** 一欄說明對多張圖時（如深淺兩版標誌）由外層各自顯示尺寸，這裡關掉。 */
    showSize?: boolean
  }>(),
  { width: null, height: null, fallback: '標題', label: '圖片說明', showSize: true },
)

const emit = defineEmits<{
  (e: 'update:zh', value: string): void
  (e: 'update:en', value: string): void
}>()

const zhMissing = computed(() => props.hasImage && !props.zh.trim())
</script>

<template>
  <ImageSizeHint v-if="showSize" :has-image="hasImage" :width="width" :height="height" />
  <BilingualShortField
    :field="field"
    :label="label"
    :zh="zh"
    :en="en"
    :maxlength="200"
    placeholder="選填，用一句話描述圖片內容，供視障讀者的輔助工具朗讀"
    @update:zh="(v) => emit('update:zh', v)"
    @update:en="(v) => emit('update:en', v)"
  />
  <el-alert
    v-if="zhMissing"
    type="info"
    :closable="false"
    show-icon
    :title="`建議補上中文圖片說明：沒有說明時，視障讀者的輔助工具只會念出${fallback}，無法得知圖片內容。（不影響儲存）`"
  />
</template>

