#!/usr/bin/env node
// scripts/check-compose-env.mjs — docker-compose.yml 的 NUXT_PUBLIC_* 覆寫（含 SPA 後台執行期位址）防漂移檢查
//
// 背景（docs/18-work-errors.md E-112 升級段）：`nuxt.config.ts` 的 `runtimeConfig.public` 與 `site.name`
// 裡寫的是「本機開發預設值」（'tcrfc'、'prelaunch'、'TCRFC'、staging 網址……），正式環境靠 compose 的
// `environment:` 以 NUXT_PUBLIC_* 覆寫。漏帶不會報錯，只會悄悄用預設值——E-112 第一次是網址鍵
// （藍鯨連結導往 bw-stg.tcrfc.tw），第二次是 nuxt-bw 沒帶 SITE_NAME（藍鯨站 <title>／og:site_name／
// JSON-LD 全顯示 TCRFC，docs/13 §6 紀律 11a）。同一類錯犯兩次，所以升級成機制。
//
// 檢查（無外部相依，純文字解析，與 check-node-version.mjs 同風格）：
//   1. 自動推導：從 nuxt.config.ts 的 `runtimeConfig.public` 取出「值是非空字串字面量」的鍵（＝寫死的
//      開發預設值），加上 `site.name`，要求每個對應的 nuxt 服務在 compose 都帶 NUXT_PUBLIC_<SNAKE>。
//      新增這類鍵時不用改本檔——compose 沒跟著加，CI 就會紅。
//   2. 明確清單（REQUIRED_EXTRA）：預設值是空字串／布林、或由 nuxt-site-config 另外讀取而不在
//      runtimeConfig.public 裡的鍵（SITE_URL、MEDIA_BASE_URL、CHARITY 的 SITE_ENV 等）。
//      ➜ 新增「預設值為空或非字串、但正式環境必須覆寫」的鍵時，要手動加進 REQUIRED_EXTRA。
//   3. 值斷言（EXPECTED_VALUES）：nuxt-bw 的 CLUB=bw、SITE_NAME=台中藍鯨；nuxt-tcrfc 的 CLUB=tcrfc、
//      SITE_NAME=TCRFC。
//      ENV_ONLY：不在 runtimeConfig.public、由程式直接讀 process.env 的 NUXT_PUBLIC_*，只供反向檢查放行。
//   4. 反向：compose 帶了 NUXT_PUBLIC_* 但 nuxt.config.ts 沒有對應鍵（打錯字會被 Nuxt 靜默忽略）。
//
//   5. SPA 後台（admin-web、admin-charity）：API 位址是「執行期注入」（容器啟動時由 docker-entrypoint.d
//      產生 /config.js），compose 必須帶 ADMIN_API_BASE_URL，且值要指向 API_DOMAIN；漏帶時映像檔裡
//      沒有任何預設可用，畫面會顯示「未設定 API 位址」（E-112 升級段第三次：舊版退回寫死的 127.0.0.1）。
//      同時要求 Dockerfile 有複製 entrypoint 腳本、nginx-spa.conf 有 /config.js 的 no-store 設定。
//
// 用法：node scripts/check-compose-env.mjs（在 repo 任何位置執行皆可）。失敗結束碼 1。
// CI：.github/workflows/ci.yml 的 compose-env job（改 docker-compose.yml 或兩份 nuxt.config.ts 時必跑）。

import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const read = (p) => readFileSync(resolve(ROOT, p), 'utf8')

// 每個 nuxt 應用：設定檔、對應 compose 服務、額外必帶、免帶（附理由）、值斷言
const APPS = [
  {
    config: 'apps/web/nuxt.config.ts',
    services: ['nuxt-tcrfc', 'nuxt-bw'],
    hasSiteName: true, // nuxt.config.ts 有 site.name
    REQUIRED_EXTRA: ['SITE_URL', 'MEDIA_BASE_URL', 'API_BASE'],
    // 不在 runtimeConfig.public、由程式直接讀 process.env.NUXT_PUBLIC_*（server/utils/backend-api.ts）
    ENV_ONLY: ['API_BASE'],
    // 藍鯨站沒有「女足入口頁外連藍鯨站」這個功能，不需要覆寫
    EXEMPT: { 'nuxt-bw': ['BLUE_WHALE_SITE_URL'] },
    EXPECTED_VALUES: {
      'nuxt-tcrfc': { CLUB: 'tcrfc', SITE_NAME: 'TCRFC' },
      'nuxt-bw': { CLUB: 'bw', SITE_NAME: '台中藍鯨' },
    },
  },
  {
    config: 'apps/web-charity/nuxt.config.ts',
    services: ['nuxt-charity'],
    hasSiteName: false,
    // 慈善站的 apiBase 有寫死的 127.0.0.1 預設值，由自動推導涵蓋；SITE_URL 由 nuxt-site-config 讀
    // （慈善站目前沒有任何程式讀 SITE_ENV，compose 帶了只是對稱，故不強制、僅允許存在）
    REQUIRED_EXTRA: ['SITE_URL'],
    ENV_ONLY: ['SITE_ENV'],
    EXEMPT: {},
    EXPECTED_VALUES: {},
  },
]

const errors = []
const fail = (m) => errors.push(m)

// ── compose：services.<name>.environment → { KEY: value }（行式解析，足以應付本檔格式）
function parseComposeEnv(text) {
  const out = {}
  let inServices = false
  let svc = null
  let inEnv = false
  for (const raw of text.split('\n')) {
    if (/^\S/.test(raw)) { inServices = /^services:\s*$/.test(raw); svc = null; inEnv = false; continue }
    if (!inServices || /^\s*(#.*)?$/.test(raw)) continue
    let m
    if ((m = raw.match(/^ {2}([A-Za-z0-9_-]+):\s*$/))) { svc = m[1]; out[svc] = {}; inEnv = false; continue }
    if (!svc) continue
    if ((m = raw.match(/^ {4}([A-Za-z_]+):/))) { inEnv = m[1] === 'environment'; continue }
    if (inEnv && (m = raw.match(/^ {6}([A-Z0-9_]+):\s*(.*)$/))) {
      let v = m[2].replace(/\s+#.*$/, '').trim() // 去行尾註解（值內不會有「空白＋#」）
      v = v.replace(/^(['"])(.*)\1$/, '$2')
      out[svc][m[1]] = v
    }
  }
  return out
}

// ── nuxt.config.ts：runtimeConfig.public 的第一層鍵與其字串字面量（沒有字串則 null）
function parsePublicKeys(text) {
  const lines = text.split('\n')
  const start = lines.findIndex((l) => /^\s*public:\s*\{/.test(l))
  if (start < 0) { fail('找不到 runtimeConfig.public 區塊（本檔的解析假設已失效，請更新 check-compose-env.mjs）'); return {} }
  const keys = {}
  let depth = 0
  for (let i = start; i < lines.length; i++) {
    const code = lines[i].replace(/^\s*\/\/.*$/, '') // 只去整行註解（不能去行內 //，會吃掉 https://）
    if (i > start && depth === 1) {
      const m = code.match(/^\s*([A-Za-z0-9_]+):\s*(.*?),?\s*$/)
      if (m) {
        const s = m[2].match(/^(['"])(.*)\1$/)
        keys[m[1]] = s ? s[2] : null
      }
    }
    for (const ch of code) { if (ch === '{') depth++; else if (ch === '}') depth-- }
    if (depth === 0) break
  }
  return keys
}

// ── SPA 後台：執行期注入的 API 位址
const SPA_APPS = [
  // frontend：後台連到前台的網址（「這裡管理的是：… ↗」、預覽前台）。後台與前台不同網域，
  // 漏帶時連結不顯示；早期版本寫相對路徑，點下去連回後台自己（E-300）。
  {
    service: 'admin-web',
    dir: 'apps/admin',
    frontend: { ADMIN_WEB_BASE_URL: 'TCRFC_DOMAIN', ADMIN_BW_WEB_BASE_URL: 'BW_DOMAIN' },
  },
  { service: 'admin-charity', dir: 'apps/admin-charity' },
]
const composeText = read('docker-compose.yml')
const compose0 = parseComposeEnv(composeText)
for (const { service, dir, frontend = {} } of SPA_APPS) {
  const env = compose0[service]
  if (!env) { fail(`docker-compose.yml 找不到服務 ${service}`); continue }
  if (!('ADMIN_API_BASE_URL' in env)) {
    fail(`${service} 缺 ADMIN_API_BASE_URL：SPA 的 API 位址是執行期注入，漏帶畫面會顯示「未設定 API 位址」（E-112 升級段）`)
  } else if (!/\$\{API_DOMAIN\b/.test(env.ADMIN_API_BASE_URL)) {
    fail(`${service} 的 ADMIN_API_BASE_URL 必須指向 \${API_DOMAIN}，目前是「${env.ADMIN_API_BASE_URL}」`)
  }
  for (const [k, domain] of Object.entries(frontend)) {
    if (!(k in env)) fail(`${service} 缺 ${k}：後台連到前台的網址，漏帶時「這裡管理的是：… ↗」不會出現連結`)
    else if (!new RegExp(`\\$\\{${domain}\\b`).test(env[k])) fail(`${service} 的 ${k} 必須指向 \${${domain}}，目前是「${env[k]}」`)
  }
  for (const k of Object.keys(env)) {
    if (k.startsWith('VITE_')) fail(`${service} 的 ${k}：VITE_* 是建置期變數，容器執行期設了不會生效，請改用 ADMIN_API_BASE_URL`)
  }
  if (!/docker-entrypoint\.d\/40-runtime-config\.sh/.test(read(dir + '/Dockerfile'))) {
    fail(`${dir}/Dockerfile 沒有複製 docker-entrypoint.d/40-runtime-config.sh（沒有它就不會產生 /config.js）`)
  }
  const nginx = read(dir + '/nginx-spa.conf')
  if (!/location = \/config\.js[\s\S]*?no-store/.test(nginx)) {
    fail(`${dir}/nginx-spa.conf 缺 location = /config.js（需 alias 到 /tmp/config.js 並設 Cache-Control: no-store）`)
  }
  if (!/<script src="\/config\.js"><\/script>/.test(read(dir + '/index.html'))) {
    fail(`${dir}/index.html 沒有載入 /config.js`)
  }
}

const toSnake = (k) => k.replace(/([A-Z])/g, '_$1').toUpperCase()
const compose = parseComposeEnv(read('docker-compose.yml'))

for (const app of APPS) {
  const pub = parsePublicKeys(read(app.config))
  if (!Object.keys(pub).length) continue

  // 1. 自動推導：寫死非空字串預設值的鍵
  const derived = Object.entries(pub).filter(([, v]) => v).map(([k]) => toSnake(k))
  if (app.hasSiteName) derived.push('SITE_NAME')
  const required = [...new Set([...derived, ...app.REQUIRED_EXTRA])]

  const known = new Set([...Object.keys(pub).map(toSnake), 'SITE_URL', 'SITE_NAME', ...app.ENV_ONLY])

  for (const svc of app.services) {
    const env = compose[svc]
    if (!env) { fail(`docker-compose.yml 找不到服務 ${svc}（${app.config} 的對應服務）`); continue }
    const exempt = new Set(app.EXEMPT[svc] ?? [])
    for (const k of required) {
      if (exempt.has(k)) continue
      if (!('NUXT_PUBLIC_' + k in env)) {
        fail(`${svc} 缺 NUXT_PUBLIC_${k}：${app.config} 內該鍵是寫死的開發預設值，正式環境漏帶會悄悄沿用（E-112、docs/13 §6 紀律 11a）`)
      }
    }
    for (const [k, want] of Object.entries(app.EXPECTED_VALUES[svc] ?? {})) {
      const got = env['NUXT_PUBLIC_' + k]
      if (got !== want) fail(`${svc} 的 NUXT_PUBLIC_${k} 必須是「${want}」，目前是「${got ?? '(未設)'}」`)
    }
    // 4. 反向：打錯字的鍵
    for (const k of Object.keys(env)) {
      if (k.startsWith('NUXT_PUBLIC_') && !known.has(k.slice('NUXT_PUBLIC_'.length))) {
        fail(`${svc} 的 ${k} 在 ${app.config} 找不到對應鍵，Nuxt 會靜默忽略（打錯字？）`)
      }
    }
  }
}

if (errors.length) {
  console.error('✗ docker-compose.yml 的 NUXT_PUBLIC_* 覆寫檢查失敗：')
  for (const e of errors) console.error('  - ' + e)
  process.exit(1)
}
console.log('✓ docker-compose.yml 的 NUXT_PUBLIC_* 覆寫齊全（' + APPS.flatMap((a) => a.services).join('、') + '）；SPA 後台執行期位址齊全（' + SPA_APPS.map((a) => a.service).join('、') + '）')
