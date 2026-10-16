<script setup lang="ts">
/**
 * 新聞內文編輯器裡「圖片／圖集／影音」節點的畫面（TipTap node view）。
 *
 * 內容不在編輯器文件裡，直接在區塊清單（`PageBlockState`）上就地編輯：
 * - 圖片、圖集重用 `PageBlockEditor` 的圖片欄位（選檔不上傳、儲存才上傳；寬度下限、HEIC 轉檔、替代文字必填都沿用）。
 * - 影音是一個「影片網址」欄位（YouTube／Vimeo），解析成 provider＋videoId 存回區塊。
 * - 存檔前的驗證錯誤用 `FormField` 標在這個節點上（鍵 `blocks[區塊索引]`，與其他區塊相同）。
 * 只在「目前顯示的語言」那個編輯器渲染，避免同一個區塊的欄位出現兩份。
 */
import { computed, nextTick, ref, watch } from 'vue'
import { ElMessageBox } from 'element-plus'
import { nodeViewProps, NodeViewWrapper } from '@tiptap/vue-3'
import BilingualShortField from '@/components/BilingualShortField.vue'
import FormField from '@/components/FormField.vue'
import PageBlockEditor from '@/components/pageBlocks/PageBlockEditor.vue'
import type { VideoEmbedBlockContent } from '@/types/pageBlocks'
import { parseVideoUrl, videoWatchUrl } from '@/utils/newsBodyDoc'
import type { NewsBodyStorage } from './extensions'

const props = defineProps(nodeViewProps)

const storage = computed(() => (props.editor.storage as unknown as { newsBody: NewsBodyStorage }).newsBody)
const bid = computed(() => props.node.attrs.bid as string | null)
const index = computed(() => (bid.value ? storage.value.getBlocks().findIndex((b) => b.localKey === bid.value) : -1))
const block = computed(() => (index.value >= 0 ? storage.value.getBlocks()[index.value] : null))
const visible = computed(() => storage.value.isVisible())

const TITLE: Record<string, string> = { image: '圖片', gallery: '圖集', video_embed: '影音' }
const title = computed(() => TITLE[block.value?.blockType ?? ''] ?? '')
const video = computed(() => (block.value?.blockType === 'video_embed' ? (block.value.content as VideoEmbedBlockContent) : null))

// 影片網址：輸入框顯示由區塊還原的網址；只有解析成功才寫回區塊，失敗時就地提示、不動原本的值。
const urlText = ref('')
const urlError = ref('')
watch(video, (v) => { if (v) urlText.value = videoWatchUrl(v) }, { immediate: true })
function commitUrl() {
  if (!video.value) return
  const raw = urlText.value.trim()
  if (!raw) { video.value.videoId = ''; urlError.value = ''; return }
  const parsed = parseVideoUrl(raw)
  if (!parsed) {
    urlError.value = '認不得這個網址，請貼上 YouTube 或 Vimeo 的影片網址。'
    return
  }
  urlError.value = ''
  video.value.provider = parsed.provider
  video.value.videoId = parsed.videoId
  urlText.value = videoWatchUrl(parsed)
}

// 剛插入的圖片／圖集：自動開啟選檔視窗一次（插入動作本身就是使用者的點擊）。
const root = ref<{ $el: HTMLElement } | null>(null)
watch(
  [visible, block],
  async ([isVisible, b]) => {
    if (!isVisible || !b || !bid.value || b.blockType === 'video_embed') return
    if (!storage.value.consumeAutoOpen(bid.value)) return
    await nextTick()
    root.value?.$el.querySelector<HTMLInputElement>('input[type="file"]')?.click()
  },
  { immediate: true },
)

async function remove() {
  try {
    await ElMessageBox.confirm(`確定要刪除這個${title.value}嗎？（按「復原」可以找回）`, '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  props.deleteNode()
}
</script>

<template>
  <NodeViewWrapper ref="root" class="news-atom" :class="{ 'news-atom--selected': selected }" contenteditable="false">
    <template v-if="visible && block">
      <div class="news-atom__bar">
        <span class="news-atom__title">{{ title }}</span>
        <el-button size="small" text type="danger" :disabled="!editor.isEditable" @click="remove">刪除{{ title }}</el-button>
      </div>
      <FormField :field="`blocks[${index}]`">
        <div class="news-atom__body">
          <template v-if="video">
            <el-form-item label="影片網址" required>
              <el-input
                v-model="urlText"
                :disabled="!editor.isEditable"
                placeholder="貼上 YouTube 或 Vimeo 的影片網址"
                @change="commitUrl"
                @keydown.enter.prevent="commitUrl"
              />
              <p v-if="urlError" class="news-atom__error" role="alert">{{ urlError }}</p>
            </el-form-item>
            <BilingualShortField
              :field="`blocks[${index}].caption`"
              label="說明文字"
              :zh="video.caption.zh"
              :en="video.caption.en"
              placeholder="選填"
              @update:zh="(v: string) => (video!.caption.zh = v)"
              @update:en="(v: string) => (video!.caption.en = v)"
            />
          </template>
          <PageBlockEditor
            v-else
            :block-type="block.blockType"
            :content="block.content"
            :block-index="index"
            :image-min-width="storage.imageMinWidth"
            :image-min-height="0"
          />
        </div>
      </FormField>
    </template>
    <p v-else-if="!visible" class="news-atom__hidden">{{ node.type.name === 'newsGallery' ? '圖集' : node.type.name === 'newsVideo' ? '影音' : '圖片' }}</p>
  </NodeViewWrapper>
</template>

<style scoped>
.news-atom {
  margin: 12px 0;
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  background: var(--admin-bg-surface-2);
  padding: 12px 16px 16px;
}

.news-atom--selected {
  outline: 2px solid var(--admin-border-focus);
  outline-offset: 1px;
}

.news-atom__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--admin-border);
}

.news-atom__title {
  font-weight: 500;
  color: var(--admin-text-primary);
}

.news-atom__error {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--admin-danger-text);
}

.news-atom__hidden {
  margin: 0;
  color: var(--admin-text-tertiary);
}
</style>
