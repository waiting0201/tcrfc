#!/usr/bin/env node
// scripts/check-node-version.mjs — Node.js 版本防漂移檢查（S0-9h）
//
// 背景（docs/18-work-errors.md E-35 升級段）：四個 Node 應用的 Dockerfile 曾經共用一個
// `node:22.12-alpine`，沒有人真的「選過」這個版本，相依樹長過了它才在 `docker build` 炸開，
// 而且本機用既有 node_modules 的 `npm run build`／`npm run lint` 完全驗不到這件事
// （E-35 升級：本機驗收要跑 `docker build`）。S0-9g 已把四個 Dockerfile 統一升到
// `node:24.13.1-alpine`（docs/17-deployment.md §12），但那只解決了「這一刻」的落差；
// 沒有機制擋住的話，之後任何一次「只改一個 Dockerfile」或「CI 用了不同版本」都會重演同一種漂移。
//
// 決定（S0-9h，深döployment-engineer）：不開 `package.json` 的 `engines` + `engine-strict`
// （STATUS.md S0-9h 兩案的第一案）——那個機制只在「別人 npm install 時」才會擋，且四個專案要重複
// 設定四次、訊息又是 npm 自己的格式。改採本專案既有慣例：**寫一支檢查腳本，掛進 `npm run lint`**，
// 跟 `check-contrast.mjs`／`check-forbidden-terms.mjs`／`emit-charity-fixtures.py --check` 同一套路
// ——沒有新加入者的摩擦（npm install 時不會被擋），但 CI 會擋下漂移。
//
// 單一事實來源：repo 根目錄的 `.node-version`（純文字，一行版本號，無 "v" 前綴）。
// 選它而不是選「docs/17 §12 的文字說明」當事實來源，原因是 `.node-version` 同時是
// `actions/setup-node` 的 `node-version-file` 原生支援格式——CI 可以直接讀這個檔案決定版本，
// 不需要在 workflow YAML 裡另外複製一份數字（YAML 裡複製一份=多一個會漂移的地方）。
// 本檔驗證的是「其餘各處都跟這個檔案一致」，不是反過來規定 `.node-version` 該是多少。
//
// 涵蓋範圍：
//   1. 四個 Node 應用的 Dockerfile（apps/{web,web-charity,admin,admin-charity}/Dockerfile）
//      的每一個 `FROM node:...` 階段，版本必須等於 `.node-version` 內容 + `-alpine`。
//   2. `.github/workflows/*.yml` 裡每一個 `actions/setup-node` 步驟：
//      - 優先用法是 `node-version-file: .node-version`（直接讀同一個檔案，結構性不會漂移）；
//      - 若某步驟改用硬編碼的 `node-version:`，該值必須等於 `.node-version` 內容；
//      - 若兩者都沒有（該步驟完全沒指定版本來源），視為錯誤——那會讓 action 用它自己的預設值，
//        是本檔要擋的同一種「沒有人選過、之後悄悄漂移」的風險。
//
// 用法：node scripts/check-node-version.mjs（無參數；離開碼 0＝一致，1＝發現落差）
//
// ⚠️ 掛進 `npm run lint` 時的教訓（docs/18-work-errors.md E-34）：長期紅燈或串在可能非零離開碼
// 的指令「之後」的檢查，事實上不會被看到／不會被執行。四個 package.json 都把
// `npm run lint:node-version` 放在 `lint` 這條 `&&` 鏈的**第一個**，理由正是這裡——
// 排在第一個保證它一定會被執行，不受鏈上其他檢查是否失敗影響。

import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(SCRIPT_DIR, '..')

const NODE_VERSION_FILE = path.join(REPO_ROOT, '.node-version')

const DOCKERFILES = [
  'apps/web/Dockerfile',
  'apps/web-charity/Dockerfile',
  'apps/admin/Dockerfile',
  'apps/admin-charity/Dockerfile',
]

const WORKFLOWS_DIR = path.join(REPO_ROOT, '.github', 'workflows')

/** @type {string[]} 累積的錯誤訊息，跑完全部檢查才一次回報，不要找到第一個就停 */
const errors = []

function readNodeVersion() {
  if (!existsSync(NODE_VERSION_FILE)) {
    errors.push(`找不到單一事實來源 ${path.relative(REPO_ROOT, NODE_VERSION_FILE)}——這個檔案本身就是事實來源，不應該不存在。`)
    return null
  }
  const raw = readFileSync(NODE_VERSION_FILE, 'utf8').trim()
  if (!/^\d+\.\d+\.\d+$/.test(raw)) {
    errors.push(`${path.relative(REPO_ROOT, NODE_VERSION_FILE)} 的內容「${raw}」不是單純的 x.y.z 版本號（不要帶 "v" 前綴或其他文字）。`)
    return null
  }
  return raw
}

function checkDockerfiles(expectedVersion) {
  const expectedTag = `node:${expectedVersion}-alpine`
  for (const rel of DOCKERFILES) {
    const abs = path.join(REPO_ROOT, rel)
    if (!existsSync(abs)) {
      errors.push(`${rel} 不存在，無法檢查版本（若這個應用已改名或移除，請同步更新本腳本的 DOCKERFILES 清單）。`)
      continue
    }
    const lines = readFileSync(abs, 'utf8').split(/\r?\n/)
    let foundAny = false
    lines.forEach((line, idx) => {
      const m = line.match(/^\s*FROM\s+node:(\S+)/i)
      if (!m) return
      foundAny = true
      const actualTag = `node:${m[1]}`
      if (actualTag !== expectedTag) {
        errors.push(
          `${rel}:${idx + 1} 用的是 \`${actualTag}\`，與 .node-version 訂的 \`${expectedTag}\` 不一致。`,
        )
      }
    })
    if (!foundAny) {
      errors.push(`${rel} 沒有任何 \`FROM node:...\` 階段——若這個應用已改成非 Node 基底映像檔，請同步從 DOCKERFILES 清單移除。`)
    }
  }
}

/**
 * 從 workflow YAML 純文字裡切出每一個 `actions/setup-node` 所在的 step 區塊。
 * 用縮排判斷 step 邊界（每個 step 是一行 `- ...` 開頭，區塊延續到下一個同縮排或更淺縮排的
 * `- ` 開頭行為止），不引入 YAML parser 依賴——跟本專案既有檢查腳本（check-contrast.mjs 等）
 * 同樣走「輕量文字比對」風格。
 */
function findSetupNodeSteps(yamlText) {
  const lines = yamlText.split(/\r?\n/)
  const steps = []
  for (let i = 0; i < lines.length; i++) {
    if (!/actions\/setup-node@/.test(lines[i])) continue

    let start = i
    while (start > 0 && !/^\s*-\s/.test(lines[start])) start--
    const stepIndent = lines[start].match(/^\s*/)[0].length

    let end = lines.length
    for (let j = start + 1; j < lines.length; j++) {
      const m = lines[j].match(/^(\s*)-\s/)
      if (m && m[1].length <= stepIndent) {
        end = j
        break
      }
    }
    steps.push({ line: start + 1, text: lines.slice(start, end).join('\n') })
  }
  return steps
}

function checkWorkflows(expectedVersion) {
  if (!existsSync(WORKFLOWS_DIR)) {
    // .github/workflows 還沒建立時不算錯誤（例如尚未寫 workflow 的階段），純粹沒有東西可檢查。
    return
  }
  const files = readdirSync(WORKFLOWS_DIR).filter((f) => /\.ya?ml$/.test(f))
  for (const file of files) {
    const rel = path.join('.github', 'workflows', file)
    const abs = path.join(WORKFLOWS_DIR, file)
    const text = readFileSync(abs, 'utf8')
    const steps = findSetupNodeSteps(text)
    for (const step of steps) {
      const fileMatch = step.text.match(/^\s*node-version-file:\s*['"]?([^\s'"]+)['"]?/m)
      const explicitVersion = step.text.match(/^\s*node-version:\s*['"]?([^\s'"]+)['"]?/m)

      if (fileMatch) {
        // node-version-file 的路徑一律應該指回 repo 根目錄的 .node-version（唯一事實來源）。
        const referencedFromRoot = path.resolve(REPO_ROOT, fileMatch[1])
        if (referencedFromRoot !== NODE_VERSION_FILE) {
          errors.push(
            `${rel}:${step.line} 的 setup-node 用 node-version-file 指到「${fileMatch[1]}」，不是 repo 根目錄的 .node-version——單一事實來源被分岔了。`,
          )
        }
        continue
      }

      if (explicitVersion) {
        const v = explicitVersion[1].replace(/^v/, '')
        if (v !== expectedVersion) {
          errors.push(
            `${rel}:${step.line} 的 setup-node 硬編碼 node-version: ${explicitVersion[1]}，與 .node-version 的 ${expectedVersion} 不一致（建議改用 node-version-file: .node-version，就不會有第二個數字要對齊）。`,
          )
        }
        continue
      }

      errors.push(
        `${rel}:${step.line} 的 actions/setup-node 沒有指定 node-version 或 node-version-file，會用 action 自己的預設值——這是沒有人選過、之後會悄悄漂移的版本來源，請加上 node-version-file: .node-version。`,
      )
    }
  }
}

function main() {
  const expectedVersion = readNodeVersion()
  if (expectedVersion) {
    checkDockerfiles(expectedVersion)
    checkWorkflows(expectedVersion)
  }

  if (errors.length > 0) {
    console.error('✖ Node.js 版本防漂移檢查失敗（單一事實來源：.node-version）：\n')
    for (const e of errors) console.error(`  - ${e}`)
    console.error(`\n共 ${errors.length} 處不一致。修法：讓下面這些地方的版本都等於 .node-version 目前的內容，或改 .node-version 並同步更新其餘各處。`)
    process.exit(1)
  }

  console.log(`✓ Node.js 版本一致（.node-version = ${expectedVersion}），四個 Dockerfile 與 CI workflow 均對齊。`)
}

main()
