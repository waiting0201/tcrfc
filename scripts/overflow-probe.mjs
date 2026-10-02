#!/usr/bin/env node
/**
 * 響應式「有東西被切掉」探針（docs/18-work-errors.md E-37 的防呆）。
 *
 * 為什麼需要它：`document.documentElement.scrollWidth === innerWidth` 只能證明「頁面沒有橫向捲軸」，
 * 但我們真正要保證的是「畫面上沒有東西被切掉」。兩者在出現巢狀捲動容器（表格）或浮層（對話框、抽屜，
 * `position: fixed` 且外層 `overflow: auto`）時就脫鉤：溢出被包在容器裡，document 層級的數字照樣全綠。
 * E-37 第一、二次是表格，第三次（2026-10-02）是 `el-dialog` 內嵌寬度 640px 在 390px 螢幕被切掉。
 *
 * 做法：連到一個已經開著 `--remote-debugging-port` 的 Chrome，對「目前這一頁」掃描所有元素：
 *   ① 捲動／裁切容器：overflow-x 不是 visible，且 scrollWidth > clientWidth（內容被捲走或切掉）
 *   ② 超出視窗右緣的元素（排除已被某個祖先容器裁切者，那種由 ① 負責）
 * 有任何一筆就以結束碼 1 結束，並印出元素選擇器與數字。
 *
 * 用法（頁面導航、登入、開對話框都由你自己做；本腳本只量測當下畫面）：
 *   1. 開 Chrome：--remote-debugging-port=9222，並把視窗／裝置尺寸設成要驗的寬度（建議 390、820、1440 各跑一次）
 *   2. 把畫面操作到要驗的狀態（例如開著某個對話框）
 *   3. node scripts/overflow-probe.mjs [port]
 *
 * ⚠️ 覆蓋範圍：只量「橫向」；只量目前可見的狀態（沒打開的對話框、沒切過去的頁籤量不到）；
 * 刻意有橫向捲動的元素（例如給使用者橫向滑動的區塊）會被當成違規，需要的話用 data-overflow-ok 屬性標記豁免。
 * Node 22+ 內建 WebSocket，不需要任何相依套件。
 */

const PROBE = `(() => {
  const vw = window.innerWidth
  const out = []
  const sel = (el) => {
    const parts = []
    for (let n = el; n && n.nodeType === 1 && parts.length < 4; n = n.parentElement) {
      let s = n.tagName.toLowerCase()
      if (n.id) { parts.unshift(s + '#' + n.id); break }
      const cls = [...n.classList].slice(0, 2).join('.')
      if (cls) s += '.' + cls
      parts.unshift(s)
    }
    return parts.join(' > ')
  }
  const clipsX = (cs) => cs.overflowX !== 'visible'
  for (const el of document.querySelectorAll('body *')) {
    // 豁免：明確標記的區塊；以及 Element Plus 頁籤列的內建捲動（頁籤太多時兩側會出現箭頭，這是元件設計行為，
    // 不是內容被切掉）。表格 el-table 的捲動則「不」豁免——手機寬度請改卡片（E-37）。
    if (el.closest('[data-overflow-ok]') || el.closest('.el-tabs__nav-wrap')) continue
    const cs = getComputedStyle(el)
    if (cs.display === 'none' || cs.visibility === 'hidden') continue
    const r = el.getBoundingClientRect()
    if (r.width === 0 && r.height === 0) continue
    if (clipsX(cs) && el.clientWidth > 0 && el.scrollWidth > el.clientWidth + 1) {
      // 順便找出是哪些子孫撐出去的（右緣超過容器右緣），省得人工二分法。
      const edge = r.left + el.clientWidth
      const culprits = [...el.querySelectorAll('*')]
        .filter((c) => { const cr = c.getBoundingClientRect(); return cr.width > 0 && cr.right > edge + 1 })
        .sort((a, b) => b.getBoundingClientRect().right - a.getBoundingClientRect().right)
        .slice(0, 3)
        .map((c) => sel(c) + ' (右緣 ' + Math.round(c.getBoundingClientRect().right) + ')')
      out.push({ kind: '內容被捲走或切掉', el: sel(el), scrollWidth: el.scrollWidth, clientWidth: el.clientWidth, culprits })
      continue
    }
    if (r.right > vw + 1) {
      let clipped = false
      for (let a = el.parentElement; a && a !== document.body; a = a.parentElement) {
        if (clipsX(getComputedStyle(a)) && a.getBoundingClientRect().right <= vw + 1) { clipped = true; break }
      }
      if (!clipped) out.push({ kind: '超出視窗右緣', el: sel(el), right: Math.round(r.right), viewport: vw })
    }
  }
  return { viewport: vw, documentScrollWidth: document.documentElement.scrollWidth, findings: out.slice(0, 40), total: out.length }
})()`

const port = Number(process.argv[2] ?? 9222)
const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()
const target = list.find((t) => t.type === 'page')
if (!target) {
  console.error(`連不上 Chrome（port ${port}）或沒有開著的分頁`)
  process.exit(2)
}
const ws = new WebSocket(target.webSocketDebuggerUrl)
await new Promise((resolve) => ws.addEventListener('open', resolve, { once: true }))
const result = await new Promise((resolve, reject) => {
  ws.addEventListener('message', (e) => {
    const m = JSON.parse(e.data)
    if (m.id === 1) (m.error ? reject(new Error(m.error.message)) : resolve(m.result))
  })
  ws.send(JSON.stringify({ id: 1, method: 'Runtime.evaluate', params: { expression: PROBE, returnByValue: true } }))
})
ws.close()
const v = result.result.value
console.log(`分頁：${target.url}\n視窗寬 ${v.viewport}px；document.scrollWidth ${v.documentScrollWidth}px（這個數字全綠不代表沒問題，見檔頭說明）`)
if (v.total === 0) {
  console.log('✓ 沒有發現被切掉或超出視窗的元素')
} else {
  console.error(`✗ 發現 ${v.total} 處（最多列 40 筆）`)
  for (const f of v.findings) console.error(`  - [${f.kind}] ${f.el}  ${JSON.stringify({ ...f, kind: undefined, el: undefined })}`)
  process.exit(1)
}
