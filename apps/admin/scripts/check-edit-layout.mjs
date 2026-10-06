#!/usr/bin/env node
/**
 * 編輯頁版面規則檢查（docs/21 §3、apps/admin/README「編輯頁共用元件」）。
 *
 * 用 @vue/compiler-sfc 解析 <template> AST 與 <script setup> 的 Babel AST 判斷祖先關係與指派，
 * 不用 regex 猜結構。只掃 src/views/**。
 *
 * 規則（對「已遷移」檔案嚴格）：
 *   a. BilingualShortField／BilingualTextareaField／LangPane 的祖先必須有 LangTabsBar（page 或 bare）。
 *      子元件檔（由父頁的 LangTabsBar 包住）可在檔頭加 `<!-- lang-scope: inherited -->` 放行。
 *   b. BilingualShortField／BilingualTextareaField 必須有 field（或 field-zh 與 field-en 兩者）。
 *   c. 每個檔案 page 變體 LangTabsBar 最多一個；LangTabsBar 不得巢狀。
 *   d. *EditView.vue 的 ImageUploader／VideoUploader／GalleryManager／input[type=file]
 *      必須在 EditLayout 的 #aside 之下；el-dialog 內例外。
 *   e. 禁止手寫中英分頁：el-tab-pane 的 label 以「中文」或「英文」開頭。
 *   f. 禁止 `formError.value = '字串'`／`= \`樣板字串\``（只能指派 API 錯誤訊息）；禁止出現 slugError。
 *      （`error instanceof AdminApiError ? error.message : '儲存失敗…'` 這種含後備文案的三元式不在此限。）
 *
 * 「已遷移」的判定看檔案內容、不用共享清單（6 批遷移同時進行，清單會互相衝突）：
 *   檔案 import 了 LangTabsBar 或 EditLayout 即視為已遷移。
 *
 * 棘輪：未遷移、未標 inherited、卻仍用雙語元件的檔案數只能減少。
 *   基準值 UNMIGRATED_BASELINE 寫在下面；遷移完成的人**把它調低**（超過基準即失敗，低於基準會提示調低）。
 *   第 4 階段改為全面嚴格：基準歸 0，並把「已遷移」判定拿掉，所有 views 檔案一律套用 a–f。
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parse, babelParse, walk } from '@vue/compiler-sfc'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '..')
const VIEWS_DIR = join(ROOT, 'src', 'views')

/** 棘輪基準：未遷移但用了雙語元件的檔案數（2026-10-06 第 2 階段起算）。只能調低。 */
const UNMIGRATED_BASELINE = 0

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
  const imports = /\bimport\b[^\n]*\b(LangTabsBar|EditLayout)\b/.test(source)
  const inherited = /<!--\s*lang-scope:\s*inherited\s*-->/.test(source)
  const info = { rel, migrated: imports, inherited, usesBilingual: false, errors: [] }
  const err = (line, msg) => info.errors.push({ line, msg })
  const isEditView = rel.endsWith('EditView.vue')

  const ast = descriptor.template?.ast
  if (ast) {
    let pageBars = 0
    visit(ast, [], (node, anc) => {
      const tag = tagOf(node)
      const line = node.loc.start.line
      if (LANG_CONTENT.has(tag)) info.usesBilingual = true

      if (tag === 'LangTabsBar') {
        const v = attr(node, 'variant')
        const isBare = v?.type === 6 && v.value?.content === 'bare'
        if (!isBare) {
          pageBars++
          if (pageBars === 2) err(line, '同一檔案出現第二個 page 變體的 LangTabsBar（每頁恰好一個；對話框請用 variant="bare"）')
        }
        if (anc.some((a) => tagOf(a) === 'LangTabsBar')) err(line, 'LangTabsBar 不得巢狀')
      }

      if (LANG_CONTENT.has(tag) && !inherited && !anc.some((a) => tagOf(a) === 'LangTabsBar')) {
        err(line, `<${tag}> 的祖先沒有 LangTabsBar（整頁加 page 版、對話框加 variant="bare"；子元件檔可於檔頭加 <!-- lang-scope: inherited -->）`)
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
  const infos = listVue(VIEWS_DIR).map(analyze)
  let failed = false
  const migrated = infos.filter((i) => i.migrated)
  const strict = infos.filter((i) => i.migrated || i.inherited)

  for (const i of strict) {
    if (i.errors.length === 0) continue
    failed = true
    console.error(`\n✗ ${i.rel}`)
    for (const e of i.errors.sort((a, b) => a.line - b.line)) console.error(`  - ${i.rel}:${e.line} ${e.msg}`)
  }

  const pending = infos.filter((i) => !i.migrated && !i.inherited && i.usesBilingual)
  if (pending.length > UNMIGRATED_BASELINE) {
    failed = true
    console.error(`\n✗ 未遷移卻使用雙語元件的檔案增加了：${pending.length} > 基準 ${UNMIGRATED_BASELINE}（新頁面請直接用 LangTabsBar＋EditLayout）`)
    for (const p of pending) console.error(`  - ${p.rel}`)
  }

  if (failed) {
    console.error('\n編輯頁版面規則見 docs/21-admin-ui.md §3 與 apps/admin/README.md「編輯頁共用元件」。\n')
    process.exit(1)
  }
  console.log(
    `✓ 編輯頁版面規則檢查通過（已遷移 ${migrated.length} 個、標 inherited ${infos.filter((i) => i.inherited).length} 個；` +
      `未遷移仍用雙語元件 ${pending.length} 個，基準 ${UNMIGRATED_BASELINE}）`,
  )
  if (pending.length < UNMIGRATED_BASELINE) {
    console.log(`  提示：未遷移數已低於基準，請把 UNMIGRATED_BASELINE 調低為 ${pending.length}。`)
  }
}

main()
