#!/usr/bin/env node
/**
 * EditView 路由狀態一次性求值檢查（防呆，對應 docs/18-work-errors.md 的 MatchEditView 回報項）。
 *
 * 背景：`MatchEditView.vue` 曾經把 `isCreate` 寫成一次性求值的
 * `const isCreate = route.name === 'match-new'`。「建立／編輯共用同一個元件」的頁面，建立成功
 * 後會呼叫 `router.replace()` 換成編輯頁的網址——Vue Router 對同一個元件實例的路由切換預設**不會
 * 重新掛載**（component reuse），`<script setup>` 頂層程式碼只在掛載當下跑一次，於是這個 `const`
 * 布林值永遠停在建立當下算出來的值。使用者「建立 → 立刻再存一次」時，畫面誤判成仍在建立模式，
 * 重複呼叫建立 API，產生第二筆重複資料而不是更新剛剛那筆。
 *
 * 這支腳本抓的正是這個特徵：**EditView 檔案裡，`<script setup>` 頂層（不在任何函式內）有一個
 * `const`／`let` 直接把 `route.name` 或 `route.params` 的求值結果指派給變數，而且沒有包
 * `computed(...)`**。這種寫法一旦被讀取的地方當成「畫面狀態」使用（判斷模式、決定要不要顯示
 * 某個區塊……），就會踩到跟 `MatchEditView.vue` 一樣的陷阱。
 *
 * 已知且刻意放行的例外（寧可少抓也不要誤報，同 `check-forbidden-terms.mjs` 的既有原則）：
 *
 *   1. 包在 `computed(() => ...)` 裡的——這正是修法本身，`CompetitionEditView.vue`／
 *      `MatchEditView.vue`／本輪修正的六支檔案都是這種寫法。
 *   2. 包在 `ref(...)` 裡的一次性 id 快照，例如
 *      `const playerId = ref<string | undefined>(route.params.id as string | undefined)`——
 *      這是這個專案已審查過的既有慣例：建立成功後由 `handleSave()` 手動
 *      `playerId.value = created.id` 更新，不依賴路由參數變化自動反應，見各 EditView 檔案裡
 *      「必須是 computed 不能是一次性求值的 const」那則註解的上下文。`ref(...)` 本身就是可以在
 *      之後被賦值改變的容器，跟凍結的 `const` 布林值不是同一種風險。
 *   3. `route.params` 的一次性 `const`，只要它在接下來幾行**只被當成 `ref(...)` 的初始值使用**
 *      （例如 `const paramId = route.params.id as string | undefined` 接著
 *      `const currentId = ref<string | undefined>(paramId)`）——這跟例外 2 是同一種模式，只是
 *      中間多了一個變數，`NewsEditView.vue`／`PageEditView.vue` 是這種寫法。
 *
 * 涵蓋不到的邊界（已知限制，不是誤判）：
 *   - 只掃「頂層、單行完成的 const／let 指派」。`<script setup>` 頂層敘述在這個專案的既有風格裡
 *     一律頂格寫（不縮排），函式內部的程式碼一律有縮排——這支腳本用「有沒有縮排」當作「是不是頂層」
 *     的判準。函式內部直接寫 `route.name === 'x'`（例如在 `handleSave()` 裡即時判斷）不是這個問題
 *     的目標：函式每次呼叫都會重新讀一次當下的 `route.name`，不會凍結，不必檢查。
 *   - 如果之後有人寫成跨行的 `computed(\n  () => route.name === 'x'\n)`，這支腳本目前只看第一行
 *     有沒有 `computed(`，抓不到「`computed(` 開頭但没在同一行看到 route.」的情況——但這個專案
 *     目前所有 `computed` 用法都是單行，暫不處理這個假設情境。
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, extname } from 'node:path'
import { fileURLToPath } from 'node:url'

const SRC_DIR = join(fileURLToPath(new URL('.', import.meta.url)), '..', 'src')

function listEditViewFiles(dir) {
  const results = []
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry)
    const stat = statSync(full)
    if (stat.isDirectory()) {
      results.push(...listEditViewFiles(full))
    } else if (extname(entry) === '.vue' && entry.endsWith('EditView.vue')) {
      results.push(full)
    }
  }
  return results
}

function extractScriptBlock(source) {
  const match = source.match(/<script[^>]*>([\s\S]*?)<\/script>/)
  return match ? match[1] : ''
}

// 頂層 const／let 指派：`^const IDENT = RHS` 或 `^let IDENT = RHS`，完全不縮排。
const TOPLEVEL_ASSIGN_RE = /^(?:const|let)\s+([A-Za-z_$][\w$]*)\s*(?::[^=]+)?=\s*(.+)$/

function isSafeWrapper(rhs) {
  return /^computed\s*\(/.test(rhs) || /^ref\s*[<(]/.test(rhs)
}

function referencesRoute(rhs) {
  return /\broute\.(name|params)\b/.test(rhs)
}

function isRouteName(rhs) {
  return /\broute\.name\b/.test(rhs)
}

/** 例外 3：這一行是 `const IDENT = route.params...`，接下來幾行有沒有把 IDENT 當成
 * `ref(...)` 的初始值？只有這種「純粹拿去餵 ref」的情況才放行——如果 IDENT 還被用在別的地方
 * （例如直接拿去做判斷），下面的呼叫端就不會符合「只有一次出現在 ref(...) 裡」，仍然會被抓到，
 * 因為那種用法才是真正的風險：一次性快照被當成活的路由狀態使用。 */
function isParamsSeedForRef(lines, startIndex, ident) {
  const windowLines = lines.slice(startIndex + 1, startIndex + 6)
  const seedPattern = new RegExp(`=\\s*ref\\s*(?:<[^>]*>)?\\s*\\(\\s*${ident}\\b`)
  return windowLines.some((line) => seedPattern.test(line))
}

function scanScript(scriptText) {
  const lines = scriptText.split('\n')
  const findings = []

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const m = line.match(TOPLEVEL_ASSIGN_RE)
    if (!m) continue

    const [, ident, rhsRaw] = m
    const rhs = rhsRaw.trim()

    if (isSafeWrapper(rhs)) continue
    if (!referencesRoute(rhs)) continue

    if (!isRouteName(rhs) && isParamsSeedForRef(lines, i, ident)) continue

    findings.push({ line: i + 1, text: line.trim(), ident })
  }

  return findings
}

function main() {
  const files = listEditViewFiles(SRC_DIR)
  let hasError = false
  let checkedCount = 0

  for (const file of files) {
    const source = readFileSync(file, 'utf-8')
    const script = extractScriptBlock(source)
    if (!script) continue
    checkedCount++

    const findings = scanScript(script)
    if (findings.length > 0) {
      hasError = true
      console.error(`\n✗ ${file}`)
      for (const finding of findings) {
        console.error(`  - 第 ${finding.line} 行：\`${finding.text}\``)
      }
    }
  }

  if (hasError) {
    console.error(
      '\n偵測到 EditView 頂層對 route.name／route.params 的一次性 const／let 求值——建立／編輯共用' +
        '同一個元件時，建立成功後 router.replace() 不會重新掛載元件，這種寫法會凍結在建立當下的值，' +
        '「建立→立刻再存一次」會誤判成仍在建立模式，重複建立第二筆資料。改成 computed()（比照 ' +
        'CompetitionEditView.vue／MatchEditView.vue），或如果是 id 快照就包成 ref(...) 並在建立成功' +
        '後手動指派新 id（見 docs/18-work-errors.md）。\n',
    )
    process.exit(1)
  }

  console.log(`✓ EditView 路由狀態一次性求值檢查通過（檢查了 ${checkedCount} 個 *EditView.vue 檔案）`)
}

main()
