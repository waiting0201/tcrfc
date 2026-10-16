<!-- lang-scope: inherited -->
<script setup lang="ts">
/**
 * 區塊清單編輯（新增／上移／下移／刪除＋每個區塊的內容編輯），由「靜態頁」與「課程內容」共用。
 * 原本寫在 `PageEditView.vue` 內，抽出後兩邊同一份行為。
 *
 * - `v-model` 是區塊陣列（`PageBlockState[]`），子元件 `PageBlockEditor` 直接就地修改巢狀內容。
 * - `allowedTypes`：可新增的區塊類型。課程內容沒有圖片上傳通道，所以不開放需要圖片的類型。
 * - 欄位錯誤鍵一律是 `blocks[i]`／`blocks[i].xxx`，新增、刪除、排序後索引會錯位，
 *   這裡把舊錯誤一律清掉（下次儲存會重新檢查）。
 */
import { ref } from 'vue'
import { ElMessageBox } from 'element-plus'
import FormField from '@/components/FormField.vue'
import PageBlockEditor from '@/components/pageBlocks/PageBlockEditor.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { createEmptyBlock, PAGE_BLOCK_TYPE_LABEL, PAGE_BLOCK_TYPES, type PageBlockState, type PageBlockType } from '@/types/pageBlocks'

const props = withDefaults(
  defineProps<{
    allowedTypes?: readonly PageBlockType[]
    emptyHint?: string
    /** 覆寫區塊類型的中文名稱（新聞內文用「段落」「圖集」「影音」）；沒列到的類型沿用 B1 名稱。 */
    typeLabels?: Partial<Record<PageBlockType, string>>
    /** 圖片欄位解析度下限，原樣傳給每個區塊編輯器；不傳＝沿用上傳元件預設。 */
    imageMinWidth?: number
    imageMinHeight?: number
    /** 清單底部額外顯示成一鍵按鈕的類型（新聞內文用圖片、圖集），讓常用的「插入圖片」不必先找下拉選單。 */
    quickAddTypes?: readonly PageBlockType[]
    /** 區塊之間顯示「在這裡插入」。預設關閉，頁面管理（B1）行為不變。 */
    allowInsert?: boolean
  }>(),
  {
    allowedTypes: () => PAGE_BLOCK_TYPES,
    emptyHint: '目前還沒有任何內容區塊，從下方選一種類型開始新增。',
    typeLabels: () => ({}),
    imageMinWidth: undefined,
    imageMinHeight: undefined,
    quickAddTypes: () => [],
    allowInsert: false,
  },
)

const blocks = defineModel<PageBlockState[]>({ required: true })
const formErrors = useFormErrors()

const addBlockType = ref<PageBlockType>(props.allowedTypes[0] ?? 'text')

function blockTypeLabel(blockType: string): string {
  return props.typeLabels[blockType as PageBlockType] ?? PAGE_BLOCK_TYPE_LABEL[blockType as PageBlockType] ?? blockType
}

function clearBlockErrors() {
  for (const key of Object.keys(formErrors.errors)) {
    if (key.startsWith('blocks')) formErrors.clear(key)
  }
}

/** 在指定位置插入新區塊；省略位置＝加在最後。 */
function addBlock(type: PageBlockType = addBlockType.value, at: number = blocks.value.length) {
  blocks.value.splice(at, 0, createEmptyBlock(type))
  clearBlockErrors()
}

function moveBlock(index: number, delta: number) {
  const target = index + delta
  if (target < 0 || target >= blocks.value.length) return
  const [item] = blocks.value.splice(index, 1)
  blocks.value.splice(target, 0, item)
  clearBlockErrors()
}

async function removeBlock(index: number) {
  try {
    await ElMessageBox.confirm('確定要刪除這個區塊嗎？', '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  blocks.value.splice(index, 1)
  clearBlockErrors()
}
</script>

<template>
  <div>
    <p v-if="blocks.length === 0" class="block-list__hint">{{ emptyHint }}</p>

    <template v-for="(block, index) in blocks" :key="block.localKey">
    <div v-if="allowInsert && index > 0" class="block-list__insert">
      <el-dropdown trigger="click" @command="(type: PageBlockType) => addBlock(type, index)">
        <el-button size="small" text type="primary">+ 在這裡插入</el-button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item v-for="type in allowedTypes" :key="type" :command="type">{{ blockTypeLabel(type) }}</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>
    <div class="block-list__block">
      <div class="block-list__toolbar">
        <span class="block-list__type">{{ blockTypeLabel(block.blockType) }}</span>
        <div class="block-list__actions">
          <el-button size="small" text :disabled="index === 0" @click="moveBlock(index, -1)">上移</el-button>
          <el-button size="small" text :disabled="index === blocks.length - 1" @click="moveBlock(index, 1)">下移</el-button>
          <el-button size="small" text type="danger" @click="removeBlock(index)">刪除區塊</el-button>
        </div>
      </div>
      <FormField :field="`blocks[${index}]`">
        <PageBlockEditor
          :block-type="block.blockType"
          :content="block.content"
          :block-index="index"
          :image-min-width="imageMinWidth"
          :image-min-height="imageMinHeight"
        />
      </FormField>
    </div>
    </template>

    <div class="block-list__add">
      <el-select v-model="addBlockType" style="width: 200px">
        <el-option v-for="type in allowedTypes" :key="type" :label="blockTypeLabel(type)" :value="type" />
      </el-select>
      <el-button type="primary" plain @click="addBlock()">+ 新增區塊</el-button>
      <el-button v-for="type in quickAddTypes" :key="type" plain @click="addBlock(type)">+ {{ blockTypeLabel(type) }}</el-button>
    </div>
  </div>
</template>

<style scoped>
.block-list__hint {
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.block-list__block {
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  padding: 16px;
  margin-bottom: 16px;
}

.block-list__toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--admin-border);
}

.block-list__type {
  font-weight: 500;
  color: var(--admin-text-primary);
}

.block-list__insert {
  display: flex;
  justify-content: center;
  margin: -8px 0 8px;
}

.block-list__add {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
</style>
