// scripts/dev-fixture-api.mjs — 驗證用假 API（不是測試、不納入 lint；**未對真 API 驗**時的替身）。
// 用法：FIXTURE_MODE=full|empty PORT=5399 node scripts/dev-fixture-api.mjs，另一個 shell 以
//   NUXT_PUBLIC_CLUB=bw NUXT_PUBLIC_SITE_ENV=production NUXT_API_INTERNAL_BASE=http://127.0.0.1:5399 node .output/server/index.mjs
// full＝賽事／球員／球隊／俱樂部皆 schemaEligible=true；empty＝一律無資料，用來驗『資料不足不輸出』與『資料齊全才輸出』兩個分支（E-159）。
// 假 API（僅供 apps/web 實機驗證，未對真 API 驗）。環境變數 FIXTURE_MODE=full|empty
import http from 'node:http'
const MODE = process.env.FIXTURE_MODE || 'full'
const M1 = '11111111-1111-4111-8111-111111111111'
const P1 = '22222222-2222-4222-8222-222222222222'
const json = (res, code, body) => { res.writeHead(code, { 'content-type': 'application/json' }); res.end(JSON.stringify(body)) }
const match = (club) => ({ id: M1, seasonCode: '2025', teamCode: club === 'bw' ? 'BW1' : 'D1', matchOn: '2025-06-01', kickoff: '14:00', homeAway: 'home', opponent: '測試對手', venue: '測試球場', competitionTag: 'league', competitionName: '木蘭聯賽', status: 'played', scoreHome: 1, scoreAway: 0, roundNo: 3, matchNo: 12, originalMatchOn: null, originalKickoff: null, schemaEligible: MODE === 'full' })
// 註冊年齡閘門（對照 apps/api `MemberAgeGate`）：生日必填→台北當地足歲→未滿 18 歲驗 guardianConsent。
const problem = (res, code, detail, en) => { json(res, 400, { title: '輸入內容有誤', status: 400, detail, code, messageZh: detail, messageEn: en ?? 'The request could not be completed.', retryable: false }); return true }
const taipeiToday = () => new Date(Date.now() + 8 * 3600_000).toISOString().slice(0, 10)
function ageGate(res, b) {
  if (!b.birthOn) return problem(res, 'birth_on_required', '請填寫生日（註冊需要確認年齡）。')
  const [by, bm, bd] = b.birthOn.split('-').map(Number); const [ty, tm, td] = taipeiToday().split('-').map(Number)
  const age = ty - by - ((tm < bm || (tm === bm && td < bd)) ? 1 : 0)
  if (age >= 18) return null
  const g = b.guardianConsent
  if (!g || g.consented !== true) return problem(res, 'guardian_consent_required', '未滿 18 歲須經監護人同意才能註冊，請由監護人完成同意。')
  if (!g.guardianName || !String(g.guardianName).trim() || String(g.guardianName).length > 64) return problem(res, 'guardian_name_required', '請填寫監護人姓名（64 字以內）。')
  if (!['parent', 'legal_guardian'].includes(g.relationship)) return problem(res, 'invalid_guardian_relationship', '請選擇監護人與你的關係（父母或法定監護人）。')
  if (g.consentTextVersion && String(g.consentTextVersion).length > 32) return problem(res, 'invalid_guardian_consent_version', '同意文案版本不正確。')
  return null
}
http.createServer((req, res) => {
  // C-6 第二輪：公開表單送出的 400（驗證英文版錯誤訊息直通：BFF `clientErrorFrom` 要把 messageEn 帶給瀏覽器）。
  if (req.method === 'POST' && /\/forms\/[^/]+\/submissions$/.test(req.url)) {
    req.resume(); req.on('end', () => json(res, 400, { title: '輸入內容有誤', status: 400, detail: '缺少必填欄位：姓名', code: 'missing_required_field', messageZh: '缺少必填欄位：姓名', messageEn: 'A required field is missing.', retryable: false }))
    return
  }
  if (req.method === 'POST' && /\/member\/auth\/(register|line\/complete)$/.test(req.url)) {
    let raw = ''; req.on('data', (c) => { raw += c }); req.on('end', () => {
      let b = {}; try { b = JSON.parse(raw) } catch { /* 空本文 */ }
      console.log('register body', JSON.stringify(b))
      if (ageGate(res, b)) return
      json(res, 200, { memberNo: 'M900999', emailSent: true })
    })
    return
  }
  if (req.method === 'POST' && /\/(programs\/sessions|trials)\/[^/]+\/registrations$/.test(req.url)) {
    req.resume(); req.on('end', () => {
      json(res, 200, { registrationNo: 'REG0001', status: '候補', statusCode: 'waitlisted', statusLabelZh: '候補', statusLabelEn: 'Waitlisted' })
    })
    return
  }
  const u = new URL(req.url, 'http://x')
  const m = u.pathname.match(/^\/api\/v1\/(?:(tcrfc|bw)\/)?(.*)$/)
  if (!m) return json(res, 404, {})
  const club = m[1] || 'bw'; const rest = m[2]
  if (rest === 'calendar/events') return json(res, 200, { items: MODE === 'full' ? [{ id: 'e1', sourceType: 'custom', startsAt: '2025-06-20T10:00:00Z', endsAt: null, isAllDay: false, title: '球迷見面會', venueName: '示範球場', eventTypeCode: 'fan', description: '示範活動', ctaUrl: null, coverUrl: null }] : [], page: 1, pageSize: 100, totalCount: 1, totalPages: 1 })
  if (rest === 'schedule') return json(res, 200, { items: MODE === 'full' ? [match(club), { ...match(club), id: '11111111-1111-4111-8111-111111111112', matchOn: '2025-07-06', matchNo: 13, opponent: '測試對手二' }, { ...match(club), id: '11111111-1111-4111-8111-111111111113', matchOn: '2025-08-03', matchNo: 14, opponent: '測試對手三' }] : [], page: 1, pageSize: 200, totalCount: 1, totalPages: 1 })
  if (rest === 'players') return json(res, 200, { items: MODE === 'full' ? [{ id: P1, slug: 'test-player', teamCode: club === 'bw' ? 'BW1' : 'D1', shirtNo: 9, position: 'FW', name: '測試球員', schemaEligible: true, photoUrl: null }] : [], page: 1, pageSize: 100, totalCount: 1, totalPages: 1 })
  if (rest.startsWith('players/') && !rest.endsWith('/stats')) {
    const key = decodeURIComponent(rest.slice('players/'.length)).toLowerCase()
    return MODE === 'full' && (key === 'test-player' || key === P1) ? json(res, 200, { id: P1, slug: 'test-player', teamCode: club === 'bw' ? 'BW1' : 'D1', shirtNo: 9, position: 'FW', name: '測試球員', schemaEligible: true, photoUrl: null }) : json(res, 404, { title: 'Not Found', status: 404, code: 'not_found', messageZh: '找不到球員', messageEn: 'Player not found', retryable: false })
  }
  if (rest.startsWith('players/') && rest.endsWith('/stats')) return json(res, 200, { playerId: P1, seasons: [] })
  if (rest === 'teams') return json(res, 200, MODE === 'full' ? [{ code: 'BW1', name: '台中藍鯨一線隊', logoUrl: 'https://example.test/logo.png', schemaEligible: true }, { code: 'BW-U15', name: 'U15', logoUrl: null, schemaEligible: false }] : [])
  if (rest === 'programs/demo-camp') return json(res, 200, { id: '33333333-3333-4333-8333-333333333333', slug: 'demo-camp', audience: '國小學童', ageMin: 7, ageMax: 12, coverUrl: null, name: '示範營隊', intro: MODE === 'full' ? '示範營隊的簡介' : null, content: '第一段\n\n第二段', staff: [{ id: 's1', name: '教練甲' }], partners: [], sessions: [{ id: 'x1', startOn: '2099-07-01', endOn: '2099-07-05', weeklySchedule: '{"mon":"18:00-19:30","wed":"18:00-19:30"}', capacity: 20, enrolledCount: 5, price: 3000, earlyBirdPrice: null, earlyBirdUntil: null, signupOpensAt: null, signupClosesAt: null, status: '開放', statusCode: 'open', statusLabelZh: '開放', statusLabelEn: 'Open', venueName: '示範球場', venueAddress: null }] })
  if (rest === 'seo/crawler-settings') return json(res, 200, { userAgents: [{ userAgent: 'GPTBot', allowed: true }, { userAgent: 'ClaudeBot', allowed: true }], excludePaths: ['/zh/member/', '/en/member/', '/zh/academy/teams/', '/en/academy/teams/', '/zh/academy/life/', '/en/academy/life/', '/m/', '/assets/img/academy/', '/assets/img/programs/'] })
  if (rest === 'seo/settings') return json(res, 200, { robotsCustomRules: null })
  if (rest === 'seo/redirects') return json(res, 200, [])
  if (rest === 'seo/sitemap-entries') return json(res, 200, [])
  if (rest === 'seo/llms-content') return json(res, 200, MODE === 'full' ? { positioningZh: '台中藍鯨女子足球隊官方網站（fixture）', positioningEn: null, factsSummaryZh: '成立 2014-04-12（fixture）' } : {})
  if (rest === 'clubs/bw' || rest === 'clubs/tcrfc' || u.pathname.endsWith('/clubs/bw')) return json(res, 200, { name: '台中藍鯨', domain: 'bw.example.tw', logoUrl: 'https://example.test/logo.png', schemaEligible: MODE === 'full' })
  return json(res, 404, { title: 'Not Found', status: 404 })
}).listen(Number(process.env.PORT || 5399), '127.0.0.1', () => console.log('fixture up'))
