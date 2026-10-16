<script setup lang="ts">
/**
 * 新聞內文編輯器裡「引言」節點的畫面：引言文字直接在編輯器裡打，來源（選填）在下方一個欄位，
 * 兩者都依目前編輯的語言存放（中文分頁寫中文來源、英文分頁寫英文來源）。
 */
import { nodeViewProps, NodeViewContent, NodeViewWrapper } from '@tiptap/vue-3'

const props = defineProps(nodeViewProps)

function onInput(value: string | number) {
  props.updateAttributes({ attribution: String(value) })
}
</script>

<template>
  <NodeViewWrapper class="news-quote">
    <NodeViewContent as="blockquote" class="news-quote__text" />
    <div class="news-quote__source" contenteditable="false">
      <label>
        <span>引言來源（選填）</span>
        <el-input :model-value="String(node.attrs.attribution ?? '')" :disabled="!editor.isEditable" placeholder="例如：教練 王小明" @update:model-value="onInput" />
      </label>
    </div>
  </NodeViewWrapper>
</template>

<style scoped>
.news-quote {
  margin: 12px 0;
  padding: 4px 0 8px 16px;
  border-left: 4px solid var(--admin-primary);
}

.news-quote__text {
  margin: 0;
  color: var(--admin-text-primary);
  font-style: italic;
}

.news-quote__source {
  margin-top: 8px;
  max-width: 420px;
}

.news-quote__source label {
  display: block;
  font-size: 12px;
  color: var(--admin-text-secondary);
}

.news-quote__source span {
  display: block;
  margin-bottom: 4px;
}
</style>
