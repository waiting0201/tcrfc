// POST /api/member-auth/login — Email＋密碼登入（S2-11）。
// 以 tokenDelivery=body 呼叫後端，把更新權杖寫進本站 HttpOnly Cookie，只回存取權杖給瀏覽器。
// 設計說明見 server/utils/member-session.ts 檔頭。
export default defineEventHandler(async (event) => {
  assertSameOrigin(event)
  const raw = await readBody<Record<string, unknown>>(event).catch(() => null)
  if (!raw || typeof raw.email !== 'string' || typeof raw.password !== 'string') {
    return memberProblem(event, 400, 'invalid_body', '請輸入 Email 與密碼。')
  }
  const rememberMe = raw.rememberMe === true
  const res = await callUpstream(event, 'POST', 'member/auth/login', {
    body: { email: raw.email, password: raw.password, rememberMe, tokenDelivery: 'body' },
  })
  if (res.status === 200) {
    const session = adoptSession(event, res.json, rememberMe)
    if (session) {
      setNoStore(event)
      return session
    }
    return memberProblem(event, 502, 'upstream_unavailable', '服務暫時無法使用，請稍後再試。')
  }
  return sendUpstream(event, res)
})
