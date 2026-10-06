<script setup lang="ts">
/**
 * 選單樹的遞迴編輯清單（I 選單管理用）。直接就地修改傳入的陣列（外層持有的 reactive 樹），
 * 排序用「上移／下移」按鈕（鍵盤與觸控都能操作），不做拖曳。最多 3 層，由 `depth` 控制是否還能加子項目。
 */
import { computed } from 'vue'
import type { MenuNode } from './menuNode'

const props = defineProps<{
  nodes: MenuNode[]
  depth: number
  maxDepth: number
  disabled?: boolean
  newKey: () => string
  /** 項目 key → 該項目的欄位錯誤（由外層驗證後傳入）。 */
  errors?: Record<string, string>
}>()
const emit = defineEmits<{ (e: 'edit', key: string): void }>()

const canAddChild = computed(() => props.depth < props.maxDepth)
/** 刻意就地修改外層持有的 reactive 陣列（遞迴編輯器，不逐層 emit）；用 computed 別名避開 props 變異規則的誤判。 */
const list = computed(() => props.nodes)

function move(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= list.value.length) return
  const [item] = list.value.splice(index, 1)
  list.value.splice(target, 0, item)
}

function remove(index: number) {
  list.value.splice(index, 1)
}

function addChild(node: MenuNode) {
  node.children.push({ key: props.newKey(), id: null, labelZh: '', labelEn: '', url: '', isExternal: false, children: [] })
}

function urlHint(node: MenuNode): string {
  if (node.children.length > 0) return '有子項目時，連結可以留空（只當分類標題）'
  return node.isExternal ? '完整網址，例如 https://example.com/' : '站內路徑，以 / 開頭，例如 /about/'
}
</script>

<template>
  <ul class="menu-nodes" :class="{ 'menu-nodes--nested': depth > 1 }">
    <li v-for="(node, index) in nodes" :key="node.key" class="menu-node">
      <div class="menu-node__card" :class="{ 'menu-node__card--error': errors?.[node.key] }" :data-node-key="node.key">
        <div class="menu-node__fields">
          <el-input v-model="node.labelZh" :aria-invalid="errors?.[node.key] ? 'true' : undefined" :disabled="disabled" maxlength="100" placeholder="中文名稱（必填）" aria-label="中文名稱" @input="emit('edit', node.key)" />
          <el-input v-model="node.labelEn" :aria-invalid="errors?.[node.key] ? 'true' : undefined" :disabled="disabled" maxlength="100" placeholder="英文名稱（留空時英文版顯示中文）" aria-label="英文名稱" @input="emit('edit', node.key)" />
          <el-input v-model="node.url" :aria-invalid="errors?.[node.key] ? 'true' : undefined" :disabled="disabled" maxlength="500" :placeholder="urlHint(node)" aria-label="連結" @input="emit('edit', node.key)" />
          <el-checkbox v-model="node.isExternal" :disabled="disabled" @change="emit('edit', node.key)">外部連結（另開網站）</el-checkbox>
        </div>
        <p v-if="errors?.[node.key]" class="menu-node__error" role="alert"><span aria-hidden="true">⚠ </span>{{ errors[node.key] }}</p>
        <div v-if="!disabled" class="menu-node__actions">
          <el-button size="small" :disabled="index === 0" aria-label="上移" @click="move(index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
          <el-button size="small" :disabled="index === nodes.length - 1" aria-label="下移" @click="move(index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
          <el-button v-if="canAddChild" size="small" @click="addChild(node)">+ 子項目</el-button>
          <el-button size="small" type="danger" plain @click="remove(index)">刪除</el-button>
        </div>
      </div>
      <MenuNodeList
        v-if="node.children.length > 0"
        :nodes="node.children"
        :depth="depth + 1"
        :max-depth="maxDepth"
        :disabled="disabled"
        :new-key="newKey"
        :errors="errors"
        @edit="(key: string) => emit('edit', key)"
      />
    </li>
  </ul>
</template>

<style scoped>
.menu-nodes { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; min-width: 0; }
.menu-nodes--nested { margin: 8px 0 0 20px; padding-left: 12px; border-left: 2px solid var(--admin-border); }
.menu-node { min-width: 0; }
.menu-node__card { border: 1px solid var(--admin-border); border-radius: 6px; padding: 10px; display: flex; flex-direction: column; gap: 8px; background: var(--admin-bg-surface); }
.menu-node__fields { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; align-items: center; min-width: 0; }
.menu-node__card--error { border: 2px solid var(--admin-danger-text); }
.menu-node__error { margin: 0; font-size: 13px; line-height: 1.5; color: var(--admin-danger-text); }
.menu-node__actions { display: flex; flex-wrap: wrap; gap: 6px; }
.menu-node__actions .el-button { margin-left: 0; }
</style>
