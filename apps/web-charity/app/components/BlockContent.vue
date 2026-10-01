<script setup lang="ts">
// BlockContent.vue — 項目說明內文（區塊編輯器輸出的 JSON，API 原樣回傳）的唯讀渲染。
//
// 🔴 一律以純文字插值渲染，不使用 v-html：內文由後台人員編輯、經 API 回傳，仍不信任其中的 HTML。
// 目前已知的形狀是 `{ "blocks": [{ "type": "paragraph", "text": "…" }] }`（種子與後台一致），
// 另外容忍 heading／quote／list（`items` 字串陣列）；遇到不認得的區塊類型時，只要區塊帶有 `text` 就當成段落顯示，
// 都沒有就略過——不為了「顯示全部」而猜測未知的區塊格式。
interface Block {
  kind: 'paragraph' | 'heading' | 'quote' | 'list'
  text: string
  items: string[]
}

const props = defineProps<{ content: unknown }>()

function asString(value: unknown): string {
  return typeof value === 'string' ? value.trim() : ''
}

const blocks = computed<Block[]>(() => {
  const root = props.content
  if (typeof root === 'string') {
    return root.trim() ? [{ kind: 'paragraph', text: root.trim(), items: [] }] : []
  }
  const raw = (root as { blocks?: unknown } | null)?.blocks
  if (!Array.isArray(raw)) return []

  const result: Block[] = []
  for (const entry of raw as Array<Record<string, unknown>>) {
    const type = asString(entry?.type)
    const text = asString(entry?.text)
    if (type === 'list' && Array.isArray(entry.items)) {
      const items = (entry.items as unknown[]).map(asString).filter(Boolean)
      if (items.length > 0) result.push({ kind: 'list', text: '', items })
    } else if (type === 'heading' && text) {
      result.push({ kind: 'heading', text, items: [] })
    } else if (type === 'quote' && text) {
      result.push({ kind: 'quote', text, items: [] })
    } else if (text) {
      result.push({ kind: 'paragraph', text, items: [] })
    }
  }
  return result
})
</script>

<template>
  <div v-if="blocks.length > 0" class="block-content">
    <template v-for="(block, index) in blocks" :key="index">
      <h3 v-if="block.kind === 'heading'">{{ block.text }}</h3>
      <blockquote v-else-if="block.kind === 'quote'">{{ block.text }}</blockquote>
      <ul v-else-if="block.kind === 'list'">
        <li v-for="(item, i) in block.items" :key="i">{{ item }}</li>
      </ul>
      <p v-else>{{ block.text }}</p>
    </template>
  </div>
</template>
