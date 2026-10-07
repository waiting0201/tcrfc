#!/usr/bin/env node
/**
 * 編輯頁版面規則檢查（docs/21 §3、docs/22 §3.10、apps/admin-charity/README「編輯頁共用元件」）。
 * 仿照 apps/admin/scripts/check-edit-layout.mjs，但慈善後台一開始就全面嚴格：沒有棘輪、沒有「已遷移」判定、
 * 沒有 `lang-scope: inherited` 放行。
 *
 * 用 @vue/compiler-sfc 解析 <template> AST 與 <script setup> 的 Babel AST 判斷祖先關係與指派，
 * 不用 regex 猜結構。掃 src/views/** 與 src/components/**（設定面板與表單元件也有雙語欄位）。
 *
 * 規則（所有檔案一律嚴格，沒有例外清單）：
 *   a. BilingualShortField／BilingualTextareaField／LangPane 的祖先必須有 LangTabsBar（page 或 bare）。
 *   b. BilingualShortField／BilingualTextareaField 必須有 field（或 field-zh 與 field-en 兩者）。
 *   c. 每個檔案 page 變體 LangTabsBar 最多一個；LangTabsBar 不得巢狀。
 *   d. *EditView.vue 的 ImageUploader／VideoUploader／GalleryManager／input[type=file]
 *      必須在 EditLayout 的 #aside 之下；el-dialog 內例外。
 *   e. 禁止手寫中英分頁：el-tab-pane 的 label 以「中文」或「英文」開頭。
 *   g. 使用 EditLayout 的檔案：#main 插槽內 el-card 最多 1 張、#aside 插槽內最多 2 張
 *      （不計巢狀在 el-dialog 內的卡片；卡片內分段請用 FormSection，docs/21 §3.3a）。
 *   f. 禁止 `formError.value = '字串'`／`= \`樣板字串\``（只能指派 API 錯誤訊息）；禁止出現 slugError。
 *      （`error instanceof AdminApiError ? error.message : '儲存失敗…'` 這種含後備文案的三元式不在此限。）
 *
 * 判定不看「是否已遷移」：新增或既有的 views 檔案都套用同一組規則（第 4 階段收尾，2026-10-07 起）。
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parse, babelParse, walk } from '@vue/compiler-sfc'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '..')
const SCAN_DIRS = [join(ROOT, 'src', 'views'), join(ROOT, 'src', 'components')]

const BILINGUAL = new Set(['BilingualShortField', 'BilingualTextareaField'])
const LANG_CONTENT = new Set([...BILINGUAL, 'LangPane'])
const UPLOADERS = new Set(['ImageUploader', 'VideoUploader', 'GalleryManager'])

function listVue(dir) {
  const out = []
  for (const e of readdirSync(dir)) {
    const full = join(dir, e)
    if (statSync(full).isDirectory()) out.push(...listVue(full))
    else if (e.endsWith('.vue')) out.push(full)
  }
  return out
}

const toPascal = (s) => s.replace(/(^|-)(\w)/g, (_, __, c) => c.toUpperCase())
const tagOf = (n) => toPascal(n.tag)

function attr(node, name) {
  for (const p of node.props) {
    if (p.type === 6 && p.name === name) return p
    if (p.type === 7 && p.name === 'bind' && p.arg?.content === name) return p
  }
  return null
}
const hasAttr = (node, ...names) => names.some((n) => attr(node, n))

/** 深度優先走訪 template AST，ancestors 為由外而內的元素陣列。 */
function visit(node, ancestors, fn) {
  if (node.type === 1) {
    fn(node, ancestors)
    for (const c of node.children) visit(c, [...ancestors, node], fn)
  } else if (node.children) {
    for (const c of node.children) if (typeof c === 'object') visit(c, ancestors, fn)
  }
}

function slotNameOf(node) {
  if (node.tag !== 'template') return null
  for (const p of node.props) if (p.type === 7 && p.name === 'slot') return p.arg?.content ?? 'default'
  return null
}

function analyze(file) {
  const source = readFileSync(file, 'utf-8')
  const rel = relative(ROOT, file).split(sep).join('/')
  const { descriptor } = parse(source, { filename: file })
  const info = { rel, errors: [] }
  const err = (line, msg) => info.errors.push({ line, msg })
  const isEditView = rel.endsWith('EditView.vue')

  const ast = descriptor.template?.ast
  if (ast) {
    let pageBars = 0
    const cardCount = {}
    visit(ast, [], (node, anc) => {
      const tag = tagOf(node)
      const line = node.loc.start.line

      if (tag === 'LangTabsBar') {
        const v = attr(node, 'variant')
        const isBare = v?.type === 6 && v.value?.content === 'bare'
        if (!isBare) {
          pageBars++
          if (pageBars === 2) err(line, '同一檔案出現第二個 page 變體的 LangTabsBar（每頁恰好一個；對話框請用 variant="bare"）')
        }
        if (anc.some((a) => tagOf(a) === 'LangTabsBar')) err(line, 'LangTabsBar 不得巢狀')
      }

      if (LANG_CONTENT.has(tag) && !anc.some((a) => tagOf(a) === 'LangTabsBar')) {
        err(line, `<${tag}> 的祖先沒有 LangTabsBar（整頁加 page 版；對話框與頁籤內的面板加 variant="bare"，每個元件自己一組，不靠祖先放行）`)
      }

      if (BILINGUAL.has(tag)) {
        const ok = hasAttr(node, 'field') || (hasAttr(node, 'field-zh', 'fieldZh') && hasAttr(node, 'field-en', 'fieldEn'))
        if (!ok) err(line, `<${tag}> 缺少 field（或 field-zh 與 field-en），欄位錯誤無法定位到這個欄位`)
      }

      if (tag === 'ElTabPane') {
        const l = attr(node, 'label')
        const text = l?.type === 6 ? l.value?.content?.trim() : ''
        if (/^(中文|英文)/.test(text ?? '')) err(line, `手寫中英分頁 <el-tab-pane label="${text}">，請改用 LangTabsBar`)
      }

      if (tag === 'ElCard' && !anc.some((a) => tagOf(a) === 'ElDialog')) {
        for (const slot of ['main', 'aside']) {
          const i = anc.findIndex((a, k) => slotNameOf(a) === slot && anc.slice(0, k).some((x) => tagOf(x) === 'EditLayout'))
          if (i < 0) continue
          // 只計屬於這個 EditLayout 插槽的卡片（中間沒有別的 EditLayout 或 el-card 包住）
          cardCount[slot] = (cardCount[slot] ?? 0) + 1
          const limit = slot === 'main' ? 1 : 2
          if (cardCount[slot] === limit + 1) {
            err(line, `<EditLayout> 的 #${slot} 內 el-card 超過 ${limit} 張（主欄一張、側欄「基本設定」與「發布設定」兩張；其餘分組請改用 <FormSection title="…">，見 docs/21-admin-ui.md §3.3a）`)
          }
        }
      }

      if (isEditView) {
        const isFileInput =
          tag === 'Input' && attr(node, 'type')?.type === 6 && attr(node, 'type').value?.content === 'file'
        if (UPLOADERS.has(tag) || isFileInput) {
          const inDialog = anc.some((a) => tagOf(a) === 'ElDialog')
          if (!inDialog) {
            const inAside = anc.some(
              (a, i) => slotNameOf(a) === 'aside' && anc.slice(0, i).some((x) => tagOf(x) === 'EditLayout'),
            )
            if (!inAside) err(line, `<${isFileInput ? 'input type="file"' : tag}> 須放在 <EditLayout> 的 #aside 之下（對話框內除外）`)
          }
        }
      }
    })
  }

  // 規則 f：script AST
  const script = descriptor.scriptSetup ?? descriptor.script
  if (script) {
    const offset = script.loc.start.line - 1
    try {
      const prog = babelParse(script.content, { sourceType: 'module', plugins: ['typescript'] })
      walk(prog, {
        enter(n) {
          if (
            n.type === 'AssignmentExpression' &&
            n.operator === '=' &&
            n.left.type === 'MemberExpression' &&
            n.left.object.type === 'Identifier' &&
            n.left.object.name === 'formError' &&
            n.left.property.type === 'Identifier' &&
            n.left.property.name === 'value' &&
            (n.right.type === 'StringLiteral' || n.right.type === 'TemplateLiteral')
          ) {
            err(n.loc.start.line + offset, '禁止 formError.value = 字串字面量（驗證請填進 formErrors，formError 只放 API 錯誤）')
          }
        },
      })
    } catch (e) {
      err(script.loc.start.line, `script 解析失敗：${e.message}`)
    }
  }
  source.split('\n').forEach((l, i) => {
    if (/slugError/.test(l)) err(i + 1, '禁止 slugError（網址名稱錯誤改用 formErrors 的 slug 鍵）')
  })
  return info
}

function main() {
  const infos = SCAN_DIRS.flatMap(listVue).map(analyze)
  let failed = false

  for (const i of infos) {
    if (i.errors.length === 0) continue
    failed = true
    console.error(`\n✗ ${i.rel}`)
    for (const e of i.errors.sort((a, b) => a.line - b.line)) console.error(`  - ${i.rel}:${e.line} ${e.msg}`)
  }

  if (failed) {
    console.error('\n編輯頁版面規則見 docs/21-admin-ui.md §3 與 docs/22-charity-ui.md §3.10。\n')
    process.exit(1)
  }
  console.log(`✓ 編輯頁版面規則檢查通過（掃描 ${infos.length} 個 views／components 檔案）`)
}

main()
