/**
 * 新聞內文編輯器（TipTap）的擴充：只開放區塊格式（`@/types/pageBlocks`）真的存得下的能力。
 *
 * - 沒有粗體、斜體、連結、程式碼、分隔線、引用區塊、巢狀清單、多層標題：區塊格式沒有對應欄位，
 *   提供了存檔就會默默消失，所以編輯器一律不給（貼上時也會被濾掉）。
 * - 圖片、圖集、影音、引言是自訂節點；圖片／圖集／影音的內容不在文件裡，節點畫面元件直接編輯區塊清單
 *   （見 `utils/newsBodyDoc.ts` 檔頭）。
 */
import { Extension, Node } from '@tiptap/core'
import { ListItem } from '@tiptap/extension-list'
import { Plugin, PluginKey, Selection, type EditorState } from '@tiptap/pm/state'
import type { EditorView } from '@tiptap/pm/view'
import { Mapping } from '@tiptap/pm/transform'
import { VueNodeViewRenderer } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import type { Component } from 'vue'
import {
  BID_NODE_TYPES,
  flattenPasteLines,
  newKey,
  planGuardedDelete,
  planPasteFill,
  type DocJSON,
  type DocNode,
  type NewsLang,
} from '@/utils/newsBodyDoc'
import type { PageBlockState } from '@/types/pageBlocks'

/** 編輯器 `storage.newsBody`：節點畫面元件透過它取得區塊清單與目前狀態（不用 provide/inject，避免跨 Teleport 取不到）。 */
export interface NewsBodyStorage {
  lang: NewsLang
  getBlocks: () => PageBlockState[]
  /** 這個語言的編輯器目前是否顯示中（隱藏中的編輯器不渲染重的節點畫面，避免同一個區塊的欄位出現兩份）。 */
  isVisible: () => boolean
  imageMinWidth: number
  /** 剛插入的圖片／圖集區塊：節點畫面元件掛載時自動開啟選檔視窗一次。 */
  consumeAutoOpen: (bid: string) => boolean
}

const bidKey = new PluginKey('newsBodyBid')

/**
 * 給頂層節點與清單項目一個唯一、穩定的 `bid`：
 * - 新節點（Enter 拆段、貼上、轉換節點型別）沒有 bid 或與既有重複 → 補新的；
 * - 轉換節點型別（段落改小標、圓點改編號）若只是 bid 掉了，從交易前同位置的舊節點找回來，區塊才不會被當成新的。
 */
const BlockId = Extension.create({
  name: 'newsBodyBlockId',
  addGlobalAttributes() {
    return [
      {
        types: BID_NODE_TYPES,
        attributes: {
          // 不輸出到 HTML、不從 HTML 解析：複製貼上出來的是新節點，不會與原節點搶同一個區塊。
          bid: { default: null, rendered: false, keepOnSplit: false, parseHTML: () => null },
        },
      },
    ]
  },
  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: bidKey,
        appendTransaction(transactions, oldState, newState) {
          if (!transactions.some((t) => t.docChanged)) return null
          const used = new Set<string>()
          const todo: { pos: number; attrs: Record<string, unknown> }[] = []
          newState.doc.descendants((node, pos, parent) => {
            const isTarget = BID_NODE_TYPES.includes(node.type.name)
              && (parent?.type.name === 'doc' || node.type.name === 'listItem')
            if (!isTarget) return true
            const bid = node.attrs.bid as string | null
            if (bid && !used.has(bid)) used.add(bid)
            else todo.push({ pos, attrs: node.attrs })
            return true
          })
          if (todo.length === 0) return null

          // 交易前後的位置對應：把新節點的起點倒推回舊文件，找同位置的舊節點
          const inverse = new Mapping()
          for (let i = transactions.length - 1; i >= 0; i--) {
            const maps = transactions[i].mapping.maps
            for (let j = maps.length - 1; j >= 0; j--) inverse.appendMap(maps[j].invert())
          }
          const tr = newState.tr
          for (const { pos, attrs } of todo) {
            let id: string | null = null
            const oldPos = inverse.map(pos, 1)
            const old = oldPos >= 0 && oldPos <= oldState.doc.content.size ? oldState.doc.nodeAt(oldPos) : null
            const oldBid = old?.attrs.bid as string | null | undefined
            if (oldBid && !used.has(oldBid)) id = oldBid
            if (!id) id = newKey()
            used.add(id)
            tr.setNodeMarkup(pos, undefined, { ...attrs, bid: id })
          }
          tr.setMeta('addToHistory', false)
          return tr
        },
      }),
    ]
  },
})


// ───────────────────────────── 貼上填格子／英文分頁刪除保護 ─────────────────────────────
// 規劃邏輯在 `utils/newsBodyDoc.ts`（純函式、有測試）；這裡只負責把編輯器選取範圍換成索引，再把結果套回編輯器。

/** 選取範圍涉及的頂層節點索引，以及「內容整個被選到」的節點。 */
function selectionInfo(state: EditorState) {
  const { doc, selection } = state
  const fromIdx = doc.resolve(selection.from).index(0)
  const toIdx = selection.empty ? fromIdx : doc.resolve(Math.max(selection.from, selection.to - 1)).index(0)
  const covered: number[] = []
  if (!selection.empty) {
    doc.forEach((node, offset, idx) => {
      if (idx < fromIdx || idx > toIdx) return
      let n = node
      let s = offset
      let e = offset + node.nodeSize
      while (!n.isTextblock && !n.isLeaf && n.firstChild) {
        s += 1
        e -= 1
        n = n.firstChild
      }
      if (n.isTextblock) {
        s += 1
        e -= 1
      }
      if (selection.from <= s && selection.to >= e) covered.push(idx)
    })
  }
  return { fromIdx, toIdx, covered }
}

/** 用新文件整份取代內容（結構與 bid 不變），游標放在指定頂層節點的結尾。 */
function replaceDoc(view: EditorView, next: DocJSON, cursorIdx: number): boolean {
  try {
    const nodes = view.state.schema.nodeFromJSON(next)
    const tr = view.state.tr.replaceWith(0, view.state.doc.content.size, nodes.content)
    let pos = 0
    tr.doc.forEach((node, offset, idx) => {
      if (idx <= cursorIdx) pos = offset + node.nodeSize
    })
    tr.setSelection(Selection.near(tr.doc.resolve(Math.max(0, pos - 1)), -1)).scrollIntoView()
    view.dispatch(tr)
    return true
  } catch {
    return false
  }
}

function guardedDelete(view: EditorView, storage: NewsBodyStorage) {
  if (storage.lang !== 'en' || view.state.selection.empty) return null
  const { fromIdx, toIdx, covered } = selectionInfo(view.state)
  const plan = planGuardedDelete(view.state.doc.toJSON() as DocJSON, storage.lang, storage.getBlocks(), fromIdx, toIdx, covered)
  return plan.protect && plan.doc ? { doc: plan.doc, fromIdx } : null
}

function createPasteAndDeleteGuard(storage: NewsBodyStorage) {
  return Extension.create({
    name: 'newsBodyPasteGuard',
    addProseMirrorPlugins() {
      return [
        new Plugin({
          key: new PluginKey('newsBodyPasteGuard'),
          props: {
            handlePaste(view, _event, slice) {
              if (!view.editable) return false
              const lines = flattenPasteLines((slice.content.toJSON() ?? []) as DocNode[])
              const { fromIdx, covered } = selectionInfo(view.state)
              // 單段貼上維持預設；但選取跨多個區塊時，預設會把整批區塊刪掉，一律走填格子
              if (lines.length < 2 && !(lines.length === 1 && covered.length >= 2)) return false
              const plan = planPasteFill(view.state.doc.toJSON() as DocJSON, fromIdx, covered, lines)
              return replaceDoc(view, plan.doc, plan.lastIdx)
            },
            handleKeyDown(view, event) {
              if (event.key !== 'Backspace' && event.key !== 'Delete') return false
              const guard = guardedDelete(view, storage)
              if (!guard) return false
              replaceDoc(view, guard.doc, guard.fromIdx)
              return true
            },
            handleDOMEvents: {
              cut(view, event) {
                const guard = guardedDelete(view, storage)
                if (!guard) return false
                const data = (event as ClipboardEvent).clipboardData
                if (data) {
                  const { dom, text } = view.serializeForClipboard(view.state.selection.content())
                  data.setData('text/html', dom.innerHTML)
                  data.setData('text/plain', text)
                }
                event.preventDefault()
                replaceDoc(view, guard.doc, guard.fromIdx)
                return true
              },
            },
          },
        }),
      ]
    },
  })
}

function atomNode(name: string, view: Component) {
  return Node.create({
    name,
    group: 'block',
    atom: true,
    selectable: true,
    draggable: false,
    // 不從貼上的 HTML 建立：圖片／影音一定從工具列插入，才有對應的區塊內容。
    parseHTML: () => [],
    renderHTML: () => ['div', { 'data-news-block': name }],
    addNodeView: () => VueNodeViewRenderer(view),
  })
}

export function createNewsBodyExtensions(views: { atom: Component; quote: Component }, storage: NewsBodyStorage) {
  const NewsQuote = Node.create({
    name: 'newsQuote',
    group: 'block',
    content: 'inline*',
    defining: true,
    addAttributes() {
      return { attribution: { default: '' } }
    },
    parseHTML: () => [{ tag: 'blockquote' }],
    renderHTML: () => ['blockquote', 0],
    addNodeView: () => VueNodeViewRenderer(views.quote),
  })

  const Storage = Extension.create({
    name: 'newsBody',
    addStorage: () => storage,
  })

  return [
    StarterKit.configure({
      heading: { levels: [2] },
      bold: false,
      italic: false,
      strike: false,
      underline: false,
      code: false,
      codeBlock: false,
      blockquote: false,
      horizontalRule: false,
      link: false,
      listItem: false,
    }),
    // 清單項目只放一個段落：不開巢狀清單（區塊格式的清單是單層）。
    ListItem.extend({ content: 'paragraph' }),
    BlockId,
    createPasteAndDeleteGuard(storage),
    NewsQuote,
    atomNode('newsImage', views.atom),
    atomNode('newsGallery', views.atom),
    atomNode('newsVideo', views.atom),
    Storage,
  ]
}
