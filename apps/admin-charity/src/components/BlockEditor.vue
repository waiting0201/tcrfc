<script setup lang="ts">
/**
 * 說明內文的區塊編輯器（規劃書 §3.2「區塊編輯器排版，支援圖文、引言、清單」）。
 * 這一版支援段落、小標題、引言、清單，可新增、調整順序與刪除。
 * 規劃書提到的「圖文」（內文中插入圖片）本版沒有做：後端儲存的區塊格式目前沒有圖片區塊的定義，
 * 前台也沒有對應的渲染，貿然加上去會產生前台看不到的內容——列為待決，見 README。
 */
import { BLOCK_TYPE_LABELS, KNOWN_BLOCK_TYPES, newBlock, type BlockType, type EditorBlock } from '@/utils/blocks'

const blocks = defineModel<EditorBlock[]>({ required: true })

defineProps<{ label: string }>()

function add(type: BlockType) {
  blocks.value = [...blocks.value, newBlock(type)]
}

function remove(index: number) {
  blocks.value = blocks.value.filter((_, i) => i !== index)
}

function move(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= blocks.value.length) return
  const copy = [...blocks.value]
  const [item] = copy.splice(index, 1)
  copy.splice(target, 0, item as EditorBlock)
  blocks.value = copy
}

function isKnown(type: string): type is BlockType {
  return (KNOWN_BLOCK_TYPES as string[]).includes(type)
}
</script>

<template>
  <div class="block-editor" role="group" :aria-label="label">
    <p v-if="blocks.length === 0" class="block-editor__empty">還沒有內容。按下面的按鈕新增第一個區塊。</p>

    <div v-for="(block, index) in blocks" :key="block.uid" class="block-editor__block">
      <div class="block-editor__bar">
        <el-select v-model="block.type" size="small" class="block-editor__type" :aria-label="`第 ${index + 1} 個區塊的版型`">
          <el-option v-for="t in KNOWN_BLOCK_TYPES" :key="t" :value="t" :label="BLOCK_TYPE_LABELS[t]" />
          <el-option v-if="!isKnown(block.type)" :value="block.type" label="其他版型（保留原樣）" disabled />
        </el-select>
        <span class="block-editor__spacer" />
        <el-button size="small" text :disabled="index === 0" aria-label="上移" @click="move(index, -1)">
          <el-icon><ArrowUp /></el-icon>
        </el-button>
        <el-button size="small" text :disabled="index === blocks.length - 1" aria-label="下移" @click="move(index, 1)">
          <el-icon><ArrowDown /></el-icon>
        </el-button>
        <el-button size="small" text type="danger" aria-label="刪除這個區塊" @click="remove(index)">
          <el-icon><Delete /></el-icon>
        </el-button>
      </div>
      <el-input
        v-if="block.type === 'list'"
        v-model="block.itemsText"
        type="textarea"
        :rows="4"
        placeholder="一行一個項目"
        :aria-label="`第 ${index + 1} 個區塊的清單內容`"
      />
      <el-input
        v-else
        v-model="block.text"
        type="textarea"
        :rows="block.type === 'heading' ? 1 : 4"
        :aria-label="`第 ${index + 1} 個區塊的內容`"
      />
    </div>

    <div class="block-editor__add">
      <el-button size="small" @click="add('paragraph')">新增段落</el-button>
      <el-button size="small" @click="add('heading')">新增小標題</el-button>
      <el-button size="small" @click="add('quote')">新增引言</el-button>
      <el-button size="small" @click="add('list')">新增清單</el-button>
    </div>
  </div>
</template>

<style scoped>
.block-editor {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-3);
}

.block-editor__empty {
  margin: 0;
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.block-editor__block {
  border: 1px solid var(--charity-admin-border);
  border-radius: 6px;
  padding: var(--charity-admin-space-2) var(--charity-admin-space-3) var(--charity-admin-space-3);
  background: var(--charity-admin-bg-surface);
}

.block-editor__bar {
  display: flex;
  align-items: center;
  margin-bottom: var(--charity-admin-space-2);
}

.block-editor__type {
  width: 190px;
  max-width: 60%;
}

.block-editor__spacer {
  flex: 1;
}

.block-editor__add {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
}
</style>
