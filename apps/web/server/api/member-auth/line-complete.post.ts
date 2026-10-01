// POST /api/member-auth/line-complete — LINE 首次登入、補 Email 完成註冊（S2-11）。
// 後端回新的 MemberSession，更新權杖寫進 Cookie（同 login）。
export default defineEventHandler(async (event) => {
  assertSameOrigin(event)
  const raw = await readBody<Record<string, unknown>>(event).catch(() => null)
  if (!raw || typeof raw.ticket !== 'string' || typeof raw.email !== 'string' || typeof raw.club !== 'string') {
    return memberProblem(event, 400, 'invalid_body', '請填寫 Email。')
  }
  const str = (v: unknown) => (typeof v === 'string' && v.trim() ? v.trim() : undefined)
  const res = await callUpstream(event, 'POST', 'member/auth/line/complete', {
    body: {
      club: raw.club,
      ticket: raw.ticket,
      email: raw.email,
      name: str(raw.name),
      phone: str(raw.phone),
      birthOn: str(raw.birthOn),
      lang: raw.lang === 'en' ? 'en' : 'zh',
      tokenDelivery: 'body',
    },
  })
  if (res.status === 200) {
    const session = adoptSession(event, res.json, true)
    if (session) {
      setNoStore(event)
      return session
    }
    return memberProblem(event, 502, 'upstream_unavailable', '服務暫時無法使用，請稍後再試。')
  }
  return sendUpstream(event, res)
})
