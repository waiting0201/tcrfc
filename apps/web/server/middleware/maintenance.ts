// server/middleware/maintenance.ts — G-10 維護頁（I3 全域設定「維護模式」，H 批，2026-10-02）
//
// 後端的維護模式**只是旗標與訊息**（apps/api/README.md「H 批」§5 全域設定）：`GET /api/v1/{club}/site-settings`
// 的 `maintenance.enabled`／`message`。「攔住其他頁面、改顯示維護頁」由前台負責，就是這支 Nitro 中介層。
//
// 行為：
//   - 維護中時，**頁面請求**一律回 `503 Service Unavailable`＋簡單的品牌化維護頁（訊息依路徑語系，`Retry-After`、`no-store`、`noindex`），
//     不渲染 Nuxt 應用程式、不打任何其他資料端點。關閉維護模式後最多 15 秒（本機快取）恢復。
//   - 🔴 **不能被維護頁擋掉的路徑（放行）**——查過規劃書，**沒有任何一條寫「維護時某頁必須照常」**，下列是執行層的保守決定
//     （擋了會造成現場傷害或讓基礎設施誤判，列為待決事項讓使用者確認）：
//       · `/m/{token}`：電子會員卡公開驗證頁，店家現場查驗會籍用（主站規劃書 §3.14），維護時店家無法驗證會讓會員在櫃台被拒絕。
//       · `/{lang}/checkout/complete`：付款後的導回頁，金流已經扣款，擋掉會讓顧客看不到訂單結果。
//       · `/api/*`（含 BFF 代理）、`/_nuxt/*`、`/healthz`、`/robots.txt`、`/sitemap.xml`、`/llms*.txt`、`/__sitemap__*`
//         與所有帶副檔名的靜態資源：基礎設施探測、爬蟲規則與前端資源不屬於「頁面」。
//   - fail-open：打不到 apps/api、或回應格式不對，一律視為「沒有維護」（網站本身此時也無資料可顯示，不應再多擋一層）。
//     取得過一次成功的值之後，API 暫時失敗就沿用最後一次的結果。
//   - 只攔 GET／HEAD 的頁面請求；其他方法（表單送出走 `/api/*`）本來就在放行名單內。
import type { H3Event } from 'h3'

interface MaintenanceState {
  enabled: boolean
  message: string | null
  logoUrl: string | null
  brandColor: string | null
}

const CACHE_TTL_MS = 15_000
const FETCH_TIMEOUT_MS = 2_000
const cache = new Map<string, { at: number, value: MaintenanceState | null }>()

const EXEMPT_PREFIXES = ['/api/', '/_nuxt/', '/__nuxt', '/_ipx/', '/assets/', '/__sitemap__', '/m/']
const EXEMPT_EXACT = new Set(['/healthz', '/robots.txt', '/sitemap.xml', '/llms.txt', '/llms-en.txt', '/favicon.ico'])
const CHECKOUT_COMPLETE = /^\/(zh|en)\/checkout\/complete(\/|$)/
const HAS_EXTENSION = /\.[a-z0-9]{2,6}$/i

export function isMaintenanceExempt(path: string): boolean {
  if (EXEMPT_EXACT.has(path)) return true
  if (EXEMPT_PREFIXES.some((p) => path.startsWith(p))) return true
  if (CHECKOUT_COMPLETE.test(path)) return true
  return HAS_EXTENSION.test(path)
}

async function loadState(club: string, lang: 'zh' | 'en'): Promise<MaintenanceState | null> {
  const key = `${club}:${lang}`
  const hit = cache.get(key)
  const now = Date.now()
  if (hit && now - hit.at < CACHE_TTL_MS) return hit.value
  try {
    const dto = await $fetch<{
      maintenance?: { enabled?: boolean, message?: string | null }
      brand?: { logoLightUrl?: string | null, brandColor?: string | null }
    }>(`/api/v1/${club}/site-settings`, {
      baseURL: backendApiBase(),
      query: { lang },
      timeout: FETCH_TIMEOUT_MS,
    })
    const value: MaintenanceState = {
      enabled: dto?.maintenance?.enabled === true,
      message: dto?.maintenance?.message ?? null,
      logoUrl: dto?.brand?.logoLightUrl ?? null,
      brandColor: dto?.brand?.brandColor ?? null,
    }
    cache.set(key, { at: now, value })
    return value
  }
  catch {
    // 失敗：沿用最後一次成功的值（可能是 null＝沒有維護），並縮短下次重試間隔，避免每個請求都等逾時
    cache.set(key, { at: now - CACHE_TTL_MS + 3_000, value: hit?.value ?? null })
    return hit?.value ?? null
  }
}

function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', '\'': '&#39;' })[c]!)
}

/** 只接受 #RGB／#RRGGBB，避免把任意字串塞進 style。 */
function safeColor(value: string | null, fallback: string): string {
  return value && /^#[0-9a-f]{3}([0-9a-f]{3})?$/i.test(value) ? value : fallback
}

/** 只接受 http(s) 或站內絕對路徑的圖片網址。 */
function safeImageUrl(value: string | null): string | null {
  return value && /^(https?:\/\/|\/)[^\s"'<>]+$/i.test(value) ? value : null
}

export function renderMaintenancePage(opts: { lang: 'zh' | 'en', clubName: string, message: string | null, logoUrl: string | null, brandColor: string | null }): string {
  const zh = opts.lang === 'zh'
  const title = zh ? '網站維護中' : 'Under maintenance'
  const fallback = zh ? '網站維護中，請稍後再回來看看。造成不便，敬請見諒。' : 'We are currently performing maintenance. Please check back soon. Thank you for your patience.'
  const message = opts.message?.trim() || fallback
  const color = safeColor(opts.brandColor, '#222222')
  const logo = safeImageUrl(opts.logoUrl)
  const paragraphs = message.replace(/\r\n?/g, '\n').split(/\n{2,}/).map((p) => `<p>${escapeHtml(p.trim()).replace(/\n/g, '<br>')}</p>`).join('')
  return `<!doctype html>
<html lang="${zh ? 'zh-Hant' : 'en'}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex, nofollow">
<title>${escapeHtml(title)}｜${escapeHtml(opts.clubName)}</title>
<style>
body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#fff;color:#231916;font-family:system-ui,-apple-system,"Noto Sans TC","PingFang TC","Microsoft JhengHei",sans-serif;line-height:1.7}
main{width:min(100% - 3rem,34rem);padding:3rem 0;text-align:center}
img{display:block;margin:0 auto 2rem;max-width:12rem;max-height:6rem;width:auto;height:auto}
h1{margin:0 0 1rem;font-size:1.6rem;border-bottom:4px solid ${color};display:inline-block;padding-bottom:.4rem}
p{margin:.8rem 0;overflow-wrap:anywhere}
small{display:block;margin-top:2rem;color:#666}
</style>
</head>
<body>
<main>
${logo ? `<img src="${escapeHtml(logo)}" alt="${escapeHtml(opts.clubName)}">` : ''}
<h1>${escapeHtml(title)}</h1>
${paragraphs}
<small>${escapeHtml(opts.clubName)}</small>
</main>
</body>
</html>`
}

export default defineEventHandler(async (event: H3Event) => {
  if (event.method !== 'GET' && event.method !== 'HEAD') return
  const path = event.path.split('?')[0] ?? '/'
  if (isMaintenanceExempt(path)) return

  const club = useRuntimeConfig(event).public.club
  const lang: 'zh' | 'en' = path === '/en' || path.startsWith('/en/') ? 'en' : 'zh'
  const state = await loadState(club, lang)
  if (!state?.enabled) return

  setResponseStatus(event, 503, 'Service Unavailable')
  setHeaders(event, {
    'Content-Type': 'text/html; charset=utf-8',
    'Cache-Control': 'no-store',
    'Retry-After': '3600',
    'X-Robots-Tag': 'noindex, nofollow',
  })
  return renderMaintenancePage({
    lang,
    clubName: getClubAssets(club).nameZh,
    message: state.message,
    logoUrl: state.logoUrl,
    brandColor: state.brandColor,
  })
})
