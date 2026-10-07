/**
 * FormField 內部用：把預設插槽裡每個「有 `onUpdate:xxx` 事件」的元件（el-select、el-date-picker、el-switch、
 * el-input-number、el-radio-group、ImageUploader……）的更新事件旁聽一份，使用者一改就清掉該欄位的錯誤。
 *
 * 為什麼不用 DOM 事件：el-select／el-date-picker 選項是 teleport 到 body 的、選完不會冒泡 input／change，
 * 光靠 el-form-item 上的 @input／@change 抓不到（apps/admin 的 FormField 只能請各頁自己呼叫 clear，
 * 修正下拉與日期錯誤不會即時清除就是這個缺口）。這裡直接從元件的更新事件接，不需要各頁處理。
 *
 * 只處理預設插槽內「直接」可見的 vnode（含 Fragment、v-if／v-for 展開、一般元素的子節點），
 * 不會往別的元件的插槽內鑽。
 */
import { cloneVNode, Comment, Fragment, isVNode, Text, type FunctionalComponent, type VNode, type VNodeArrayChildren } from 'vue'

function tap(node: VNode, onChange: () => void): VNode {
  if (node.type === Comment || node.type === Text) return node
  const props = node.props
  let extra: Record<string, () => void> | null = null
  if (props) {
    for (const key of Object.keys(props)) {
      if (key.startsWith('onUpdate:')) (extra ??= {})[key] = onChange
    }
  }
  const children = node.children
  let nextChildren: VNodeArrayChildren | null = null
  // Fragment 與一般 HTML 元素（type 是字串）的陣列子節點才遞迴；元件的子節點是插槽，不碰。
  if (Array.isArray(children) && (node.type === Fragment || typeof node.type === 'string')) {
    nextChildren = children.map((c) => (isVNode(c) ? tap(c, onChange) : Array.isArray(c) ? tapAll(c, onChange) : c))
  }
  if (!extra && !nextChildren) return node
  const cloned = extra ? cloneVNode(node, extra) : cloneVNode(node)
  if (nextChildren) cloned.children = nextChildren
  return cloned
}

function tapAll(nodes: VNodeArrayChildren, onChange: () => void): VNodeArrayChildren {
  return nodes.map((n) => (isVNode(n) ? tap(n, onChange) : Array.isArray(n) ? tapAll(n, onChange) : n))
}

export const FormFieldChangeTap: FunctionalComponent<{ handler: () => void }> = (props, { slots }) => {
  const nodes = slots.default?.() ?? []
  return tapAll(nodes, props.handler) as VNode[]
}
FormFieldChangeTap.props = ['handler']
