// POST /api/member-auth/line-callback — LINE 授權導回後，以 code＋state 完成登入或綁定（S2-11）。
// bind 模式需要存取權杖（後端會核對 state 內的會員就是目前登入的人）；login 模式不需要。
// 回 `logged_in` 時把更新權杖寫進 Cookie（LINE 登入一律視為持久工作階段：使用者主動走了一趟授權）。
export default defineEventHandler(async (event) => {
  assertSameOrigin(event)
  const raw = await readBody<Record<string, unknown>>(event).catch(() => null)
  if (!raw || typeof raw.code !== 'string' || typeof raw.state !== 'string') {
    return memberProblem(event, 400, 'invalid_body', 'LINE 授權資料不完整。')
  }
  const bearer = readBearer(event)
  const res = await callUpstream(event, 'POST', 'member/auth/line/callback', {
    headers: bearer ? { authorization: bearer } : {},
    body: { code: raw.code, state: raw.state, tokenDelivery: 'body' },
  })
  if (res.status === 200) {
    const out = res.json as { status?: string, session?: unknown } | undefined
    if (out?.status === 'logged_in') {
      const session = adoptSession(event, out.session, true)
      if (!session) return memberProblem(event, 502, 'upstream_unavailable', '服務暫時無法使用，請稍後再試。')
      setNoStore(event)
      return { status: 'logged_in', session }
    }
    // bound／signup_required 沒有權杖，原樣轉出
  }
  return sendUpstream(event, res)
})
