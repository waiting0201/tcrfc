<!-- lang-scope: inherited -->
<script setup lang="ts">
/**
 * 新聞內文：所見即所得的文書編輯器（TipTap），編輯區使用整個可用寬度。
 *
 * 資料與儲存格式都沒變：`v-model` 仍是 `PageBlockState[]`（雙語在同一份清單），存檔照舊走
 * `serializeBlocksForSubmit` 與 multipart。這裡只是把「一塊一塊加區塊」換成一份連續的文件：
 * 中文、英文各有一個編輯器，兩份文件共用同一份區塊結構（轉換規則與理由見 `@/utils/newsBodyDoc`）：
 * 在其中一種語言插入／拆開／刪除段落，另一種語言的編輯器同步長出／少掉對應的空段落，英文不會錯位。
 *
 * 工具列只提供區塊格式存得下的能力：段落、小標、引言、項目／編號清單、圖片、圖集、影音、復原與重做。
 * 圖片「選檔不上傳、儲存才上傳」：檔案只在記憶體與預覽網址，按儲存才跟其他欄位一起送出。
 */
import { computed, onBeforeUnmount, ref, shallowRef, toRaw, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Editor, EditorContent } from '@tiptap/vue-3'
import FormField from '@/components/FormField.vue'
import LangPane from '@/components/LangPane.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { useLangScope } from '@/composables/useLangScope'
import { createEmptyBlock, NEWS_BLOCK_TYPE_LABEL, type PageBlockState, type PageBlockType } from '@/types/pageBlocks'
import {
  blocksToDoc,
  createGraveyard,
  docSignature,
  docToBlocks,
  ensureTrailingText,
  newKey,
  parseVideoUrl,
  type DocJSON,
  type NewsLang,
} from '@/utils/newsBodyDoc'
import { createNewsBodyExtensions, type NewsBodyStorage } from './extensions'
import NewsAtomView from './NewsAtomView.vue'
import NewsQuoteView from './NewsQuoteView.vue'

const props = withDefaults(defineProps<{ modelValue: PageBlockState[]; imageMinWidth?: number; disabled?: boolean }>(), {
  imageMinWidth: 800,
  disabled: false,
})
const emit = defineEmits<{ (e: 'update:modelValue', value: PageBlockState[]): void }>()

const scope = useLangScope()
const formErrors = useFormErrors()
const current = computed<NewsLang>(() => scope?.current.value ?? 'zh')
const LANG_LABEL: Record<NewsLang, string> = { zh: '中文', en: '英文' }

let grave = createGraveyard()
/** 目前最新的區塊清單（prop 要等父層重繪才會更新，連續兩次更新之間以這份為準）。 */
let latest: PageBlockState[] = toRaw(props.modelValue)
/** 本元件自己送出的最後一份清單；prop 變成別的陣列＝父層重新載入資料，需要重建編輯器。 */
let lastOut: PageBlockState[] | null = null
let syncing = false
const autoOpen = new Set<string>()
/** 編輯器交易時遞增，讓工具列的「目前格式」「可否復原」跟著重算。 */
const tick = ref(0)
const editors = shallowRef<Record<NewsLang, Editor> | null>(null)

function createEditor(lang: NewsLang, initial: PageBlockState[]): Editor {
  const storage: NewsBodyStorage = {
    lang,
    getBlocks: () => props.modelValue,
    isVisible: () => current.value === lang,
    imageMinWidth: props.imageMinWidth,
    consumeAutoOpen: (bid) => autoOpen.delete(bid),
  }
  return new Editor({
    extensions: createNewsBodyExtensions({ atom: NewsAtomView, quote: NewsQuoteView }, storage),
    content: blocksToDoc(initial, lang),
    editable: !props.disabled,
    editorProps: {
      attributes: { role: 'textbox', 'aria-multiline': 'true', 'aria-label': `新聞內文（${LANG_LABEL[lang]}）`, class: 'news-body__prose' },
    },
    onUpdate: () => handleUpdate(lang),
    onTransaction: () => { tick.value++ },
  })
}

function build() {
  const initial = ensureTrailingText(toRaw(props.modelValue))
  latest = initial
  lastOut = initial
  grave = createGraveyard()
  editors.value = { zh: createEditor('zh', initial), en: createEditor('en', initial) }
  // 父層的清單沒有結尾空段落時補上（存檔與比對前會去掉空段落，不算變更）
  if (initial !== toRaw(props.modelValue)) emit('update:modelValue', initial)
}

function destroy() {
  if (!editors.value) return
  editors.value.zh.destroy()
  editors.value.en.destroy()
  editors.value = null
}

build()
onBeforeUnmount(destroy)

watch(() => props.modelValue, (value) => {
  if (toRaw(value) === lastOut) return
  destroy()
  build()
})
watch(() => props.disabled, (disabled) => {
  editors.value?.zh.setEditable(!disabled)
  editors.value?.en.setEditable(!disabled)
})

const TEXT_LIKE: PageBlockType[] = ['text', 'heading', 'quote', 'list']

function handleUpdate(lang: NewsLang) {
  if (syncing || !editors.value) return
  const prev = latest
  const next = docToBlocks(editors.value[lang].getJSON() as DocJSON, lang, prev, grave)
  latest = next
  lastOut = next
  emit('update:modelValue', next)

  // 區塊索引變了就清掉所有內文錯誤（下次儲存會重新檢查）；沒變則只清文字類區塊的錯誤（圖片等由欄位自己清）
  const sameOrder = next.length === prev.length && next.every((b, i) => b.localKey === prev[i].localKey)
  for (const key of Object.keys(formErrors.errors)) {
    const m = /^blocks\[(\d+)\]/.exec(key)
    if (!m) continue
    const i = Number(m[1])
    if (!sameOrder || (TEXT_LIKE.includes(next[i]?.blockType) && next[i] !== prev[i])) formErrors.clear(key)
  }

  // 另一種語言的編輯器：結構或文字不同才重畫（不進復原紀錄、不觸發它自己的更新事件）
  const otherLang: NewsLang = lang === 'zh' ? 'en' : 'zh'
  const other = editors.value[otherLang]
  const target = blocksToDoc(next, otherLang)
  if (docSignature(other.getJSON() as DocJSON) !== docSignature(target)) {
    syncing = true
    try {
      other.chain().setMeta('addToHistory', false).setContent(target, { emitUpdate: false }).run()
    } finally {
      syncing = false
    }
  }
}

// ───────────────────────────── 工具列 ─────────────────────────────

const active = computed(() => editors.value?.[current.value] ?? null)
const editable = computed(() => !props.disabled)
/** 游標在清單裡時，段落／小標／引言會把項目拆出清單，另一種語言的對應項目文字會跟著錯位；要離開清單請再按一次清單按鈕。 */
const inList = computed(() => isOn('bulletList') || isOn('orderedList'))

function isOn(name: string, attrs?: Record<string, unknown>): boolean {
  void tick.value
  return active.value?.isActive(name, attrs) ?? false
}
const canUndo = computed(() => (void tick.value, active.value?.can().undo() ?? false))
const canRedo = computed(() => (void tick.value, active.value?.can().redo() ?? false))

function run(fn: (chain: ReturnType<Editor['chain']>) => ReturnType<Editor['chain']>) {
  if (!active.value) return
  fn(active.value.chain().focus()).run()
}
const setParagraph = () => run((c) => c.setParagraph())
const toggleHeading = () => run((c) => c.toggleHeading({ level: 2 }))
const toggleQuote = () => run((c) => (isOn('newsQuote') ? c.setParagraph() : c.setNode('newsQuote')))
const toggleBullet = () => run((c) => c.toggleBulletList())
const toggleOrdered = () => run((c) => c.toggleOrderedList())
const undo = () => run((c) => c.undo())
const redo = () => run((c) => c.redo())

type AtomName = 'newsImage' | 'newsGallery' | 'newsVideo'
const BLOCK_OF: Record<AtomName, PageBlockType> = { newsImage: 'image', newsGallery: 'gallery', newsVideo: 'video_embed' }

/** 在游標所在區塊的後面插入（游標在空段落上就取代那個空段落）。 */
function insertAtom(name: AtomName, seed?: PageBlockState['content']) {
  const ed = active.value
  if (!ed) return
  const bid = newKey()
  const type = BLOCK_OF[name]
  const empty = createEmptyBlock(type)
  // 先放進墳場：編輯器更新時依 bid 取回，圖片／影音的初始內容就跟著進區塊清單
  grave.blocks.set(bid, { ...empty, localKey: bid, content: seed ?? empty.content })
  if (name !== 'newsVideo') autoOpen.add(bid)

  const { selection } = ed.state
  const $to = selection.$to
  let target: number | { from: number; to: number }
  if ($to.depth >= 1) {
    const top = $to.node(1)
    const from = $to.before(1)
    const to = $to.after(1)
    target = top.type.name === 'paragraph' && top.content.size === 0 ? { from, to } : to
  } else {
    target = selection.to
  }
  ed.chain().focus().insertContentAt(target, { type: name, attrs: { bid } }).run()
}

async function insertVideo() {
  let url: string
  try {
    const result = await ElMessageBox.prompt('請貼上 YouTube 或 Vimeo 的影片網址。', '插入影音', {
      confirmButtonText: '插入',
      cancelButtonText: '取消',
      inputPlaceholder: 'https://www.youtube.com/watch?v=…',
      inputValidator: (v: string) => (parseVideoUrl(v) ? true : '認不得這個網址，請貼上 YouTube 或 Vimeo 的影片網址。'),
    })
    url = result.value
  } catch {
    return
  }
  const parsed = parseVideoUrl(url)
  if (!parsed) {
    ElMessage.error('認不得這個網址，請貼上 YouTube 或 Vimeo 的影片網址。')
    return
  }
  insertAtom('newsVideo', { provider: parsed.provider, videoId: parsed.videoId, caption: { zh: '', en: '' } })
}

// ───────────────────────────── 存檔前的錯誤（文字類區塊） ─────────────────────────────

/** 圖片、圖集、影音的錯誤標在各自的節點上；段落、小標、引言、清單沒有欄位可標，列在編輯區下方並可跳回該處。 */
const textErrors = computed(() => {
  const list: { index: number; field: string; label: string }[] = []
  props.modelValue.forEach((block, index) => {
    const field = `blocks[${index}]`
    if (!TEXT_LIKE.includes(block.blockType) || !formErrors.has(field)) return
    list.push({ index, field, label: `第 ${index + 1} 個區塊（${NEWS_BLOCK_TYPE_LABEL[block.blockType] ?? ''}）` })
  })
  return list
})

function jumpTo(index: number) {
  const ed = active.value
  const bid = props.modelValue[index]?.localKey
  if (!ed || !bid) return
  let pos = -1
  ed.state.doc.forEach((node, offset) => {
    if (node.attrs.bid === bid) pos = offset
  })
  if (pos < 0) return
  ed.chain().focus().setTextSelection(pos + 1).scrollIntoView().run()
}

function hasLangText(block: PageBlockState, lang: NewsLang): boolean {
  const walk = (v: unknown): boolean => {
    if (v === null || typeof v !== 'object' || v instanceof File) return false
    if (Array.isArray(v)) return v.some(walk)
    return Object.entries(v as Record<string, unknown>).some(([k, x]) => (k === lang && typeof x === 'string' && x.trim() !== '') || walk(x))
  }
  return walk(block.content)
}
const zhHasText = computed(() => props.modelValue.some((b) => hasLangText(b, 'zh')))
const enHasText = computed(() => props.modelValue.some((b) => hasLangText(b, 'en')))
</script>

<template>
  <div class="news-body">
    <div class="news-body__toolbar" role="toolbar" aria-label="內文格式">
      <div class="news-body__group" role="group" aria-label="文字格式">
        <el-button size="small" :type="isOn('paragraph') ? 'primary' : ''" :aria-pressed="isOn('paragraph')" :disabled="!editable || inList" @click="setParagraph">段落</el-button>
        <el-button size="small" :type="isOn('heading') ? 'primary' : ''" :aria-pressed="isOn('heading')" :disabled="!editable || inList" @click="toggleHeading">小標</el-button>
        <el-button size="small" :type="isOn('newsQuote') ? 'primary' : ''" :aria-pressed="isOn('newsQuote')" :disabled="!editable || inList" @click="toggleQuote">引言</el-button>
      </div>
      <div class="news-body__group" role="group" aria-label="清單">
        <el-button size="small" :type="isOn('bulletList') ? 'primary' : ''" :aria-pressed="isOn('bulletList')" :disabled="!editable" @click="toggleBullet">項目清單</el-button>
        <el-button size="small" :type="isOn('orderedList') ? 'primary' : ''" :aria-pressed="isOn('orderedList')" :disabled="!editable" @click="toggleOrdered">編號清單</el-button>
      </div>
      <div class="news-body__group" role="group" aria-label="插入">
        <el-button size="small" :disabled="!editable" @click="insertAtom('newsImage')">插入圖片</el-button>
        <el-button size="small" :disabled="!editable" @click="insertAtom('newsGallery')">插入圖集</el-button>
        <el-button size="small" :disabled="!editable" @click="insertVideo">插入影音</el-button>
      </div>
      <div class="news-body__group" role="group" aria-label="復原與重做">
        <el-button size="small" :disabled="!editable || !canUndo" @click="undo">復原</el-button>
        <el-button size="small" :disabled="!editable || !canRedo" @click="redo">重做</el-button>
      </div>
    </div>

    <LangPane lang="zh">
      <EditorContent v-if="editors" :editor="editors.zh" class="news-body__editor" />
    </LangPane>
    <LangPane lang="en" :untranslated="zhHasText && !enHasText">
      <EditorContent v-if="editors" :editor="editors.en" class="news-body__editor" />
    </LangPane>

    <ul v-if="textErrors.length" class="news-body__errors">
      <li v-for="e in textErrors" :key="e.field">
        <FormField :field="e.field">
          <el-button size="small" type="danger" plain @click="jumpTo(e.index)">回到{{ e.label }}</el-button>
        </FormField>
      </li>
    </ul>
    <p class="news-body__hint">中文與英文共用同一份段落結構：在一種語言新增或刪除段落、圖片，另一種語言會跟著增減；文字則各寫各的。</p>
  </div>
</template>

<style scoped>
.news-body {
  width: 100%;
  min-width: 0;
}

.news-body__toolbar {
  position: sticky;
  top: 49px; /* 語言分頁列（LangTabsBar）的高度，捲動時工具列接在它下面 */
  z-index: 8;
  display: flex;
  flex-wrap: wrap;
  gap: 8px 16px;
  padding: 8px 12px;
  background: var(--admin-bg-surface);
  border: 1px solid var(--admin-border-input);
  border-bottom: 0;
  border-radius: 4px 4px 0 0;
}

.news-body__group {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.news-body__group :deep(.el-button + .el-button) {
  margin-left: 0;
}

.news-body__editor {
  width: 100%;
}

.news-body__editor :deep(.news-body__prose) {
  min-height: 480px;
  padding: 16px 20px;
  background: var(--admin-bg-input);
  color: var(--admin-text-primary);
  border: 1px solid var(--admin-border-input);
  border-radius: 0 0 4px 4px;
  line-height: 1.8;
  outline: none;
  overflow-wrap: anywhere;
}

.news-body__editor :deep(.news-body__prose:focus-visible) {
  border-color: var(--admin-border-focus);
  box-shadow: 0 0 0 1px var(--admin-border-focus);
}

.news-body__editor :deep(.news-body__prose p) {
  margin: 0 0 12px;
}

.news-body__editor :deep(.news-body__prose h2) {
  margin: 20px 0 10px;
  font-size: 20px;
  line-height: 1.4;
}

.news-body__editor :deep(.news-body__prose ul),
.news-body__editor :deep(.news-body__prose ol) {
  margin: 0 0 12px;
  padding-left: 28px;
}

.news-body__editor :deep(.news-body__prose li > p) {
  margin: 0;
}

.news-body__errors {
  margin: 8px 0 0;
  padding: 0;
  list-style: none;
}

.news-body__hint {
  margin: 8px 0 0;
  font-size: 12px;
  line-height: 1.6;
  color: var(--admin-text-tertiary);
}
</style>
