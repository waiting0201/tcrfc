<script setup lang="ts">
/**
 * 廣告素材的新增／編輯對話框。
 * 🔴 素材內容被修改（文案、點擊目的地、換圖、換影片）後一律回到「待審」，沒有改動的儲存不會退回待審。
 * 🔴 圖片上傳時就會檢查版位規格（最小尺寸、長寬比、檔案大小），不合會被擋下；影片素材的圖片是海報。
 */
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import ImageUploader from '@/components/ImageUploader.vue'
import VideoUploader from '@/components/VideoUploader.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { createCreative, updateCreative, type AdSlotDto, type CreativeDto } from '@/api/adminAds'

const props = defineProps<{ modelValue: boolean; campaignId: string; creative: CreativeDto | null; adSlot: AdSlotDto | null }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: boolean): void; (e: 'saved'): void }>()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

const form = reactive({ locale: 'zh' as 'zh' | 'en', altText: '', title: '', ctaText: '', clickUrl: '', theme: 'both', variantTag: '' })
const image = ref<File | null>(null)
const video = ref<File | null>(null)
const removeVideo = ref(false)
const saving = ref(false)
const error = ref<string | null>(null)

watch(
  () => props.modelValue,
  (open) => {
    if (!open) return
    const c = props.creative
    error.value = null
    image.value = null
    video.value = null
    removeVideo.value = false
    Object.assign(form, {
      locale: c?.locale ?? 'zh', altText: c?.altText ?? '', title: c?.title ?? '', ctaText: c?.ctaText ?? '',
      clickUrl: c?.clickUrl ?? '', theme: c?.theme ?? 'both', variantTag: c?.variantTag ?? '',
    })
  },
)

async function save() {
  if (!form.altText.trim()) return void (error.value = '請填寫圖片替代文字（給看不到圖片的人閱讀）')
  if (!props.creative && !image.value) return void (error.value = '請選擇素材圖片')
  const url = form.clickUrl.trim()
  if (url && !/^(tcrfc:\/\/|https?:\/\/)/i.test(url)) return void (error.value = '點擊後前往的連結格式不正確：請填網址（https:// 開頭）或 App 內頁面連結')
  saving.value = true
  error.value = null
  const payload = {
    locale: form.locale,
    altText: form.altText.trim(),
    title: nullIfBlank(form.title),
    ctaText: nullIfBlank(form.ctaText),
    clickUrl: nullIfBlank(form.clickUrl),
    theme: form.theme,
    variantTag: form.variantTag || null,
    removeVideo: video.value ? undefined : removeVideo.value || undefined,
  }
  try {
    if (props.creative) await updateCreative(props.creative.id, payload, image.value, video.value)
    else await createCreative(props.campaignId, payload, image.value, video.value)
    ElMessage.success('已儲存')
    emit('saved')
    emit('update:modelValue', false)
  } catch (e) {
    error.value = e instanceof AdminApiError ? e.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-dialog :model-value="modelValue" :title="creative ? '編輯素材' : '新增素材'" width="640px" :fullscreen="isMobile" :close-on-click-modal="false" @update:model-value="(v: boolean) => emit('update:modelValue', v)">
    <el-alert v-if="error" type="error" show-icon :closable="false" :title="error" class="cr__block" />
    <el-alert v-if="creative" type="warning" show-icon :closable="false" title="修改內容（文案、連結、圖片、影片）後，這個素材會回到「待審核」，需重新審核才會投放。" class="cr__block" />
    <p v-if="adSlot" class="cr__hint">版位規格：{{ adSlot.aspectRatio ? `長寬比 ${adSlot.aspectRatio}` : '長寬比不限' }}<template v-if="adSlot.minWidth && adSlot.minHeight">・至少 {{ adSlot.minWidth }}×{{ adSlot.minHeight }} 像素</template><template v-if="adSlot.maxFileKb">・檔案 {{ adSlot.maxFileKb }} KB 以內</template>。</p>
    <el-form label-position="top">
      <el-form-item label="語言版本">
        <el-radio-group v-model="form.locale"><el-radio value="zh">中文</el-radio><el-radio value="en">英文</el-radio></el-radio-group>
        <div class="cr__hint">依語言分別上傳；App 使用英文但沒有英文素材時，會改顯示中文素材。</div>
      </el-form-item>
      <el-form-item :label="adSlot?.allowVideo ? '素材圖片（影片素材的海報圖）' : '素材圖片'" :required="!creative">
        <ImageUploader v-model:file="image" :remove-cover="false" :has-existing-image="!!creative?.imageKey" :existing-preview-url="creative?.imageThumbUrl ?? creative?.imageUrl ?? null" :min-width="adSlot?.minWidth ?? 1" :min-height="adSlot?.minHeight ?? 1" :disabled="saving" />
      </el-form-item>
      <el-form-item v-if="adSlot?.allowVideo" label="素材影片（選填）">
        <VideoUploader v-model:file="video" :has-existing-video="!!creative?.videoKey && !removeVideo" :disabled="saving" />
        <el-checkbox v-if="creative?.videoKey && !video" v-model="removeVideo">儲存時移除既有影片</el-checkbox>
      </el-form-item>
      <el-form-item label="圖片替代文字" required><el-input v-model="form.altText" maxlength="200" show-word-limit /></el-form-item>
      <el-row :gutter="12">
        <el-col :xs="24" :sm="12"><el-form-item label="標題（選填）"><el-input v-model="form.title" maxlength="100" /></el-form-item></el-col>
        <el-col :xs="24" :sm="12"><el-form-item label="按鈕文案（選填）"><el-input v-model="form.ctaText" maxlength="30" /></el-form-item></el-col>
      </el-row>
      <el-form-item label="點擊後前往（選填）"><el-input v-model="form.clickUrl" placeholder="網址（https:// 開頭）或 App 內頁面連結" /></el-form-item>
      <el-row :gutter="12">
        <el-col :xs="24" :sm="12">
          <el-form-item label="適用的畫面色調">
            <el-select v-model="form.theme" style="width: 100%"><el-option label="淺色與深色都適用" value="both" /><el-option label="只適用淺色" value="light" /><el-option label="只適用深色" value="dark" /></el-select>
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="A／B 比較標記（選填）">
            <el-select v-model="form.variantTag" clearable placeholder="不做比較" style="width: 100%"><el-option label="A 版" value="A" /><el-option label="B 版" value="B" /></el-select>
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>
    <template #footer>
      <el-button @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.cr__block { margin-bottom: 12px; }
.cr__hint { margin: 4px 0 8px; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>
