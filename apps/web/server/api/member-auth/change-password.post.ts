// POST /api/member-auth/change-password — 變更密碼（S2-11）。
// 後端會撤銷全部其他裝置並替「目前這台」發新權杖，所以這裡要換掉 Cookie 裡的更新權杖。
export default defineEventHandler(async (event) => {
  assertSameOrigin(event)
  const bearer = readBearer(event)
  if (!bearer) return memberProblem(event, 401, 'login_required', '請先登入。')
  const raw = await readBody<Record<string, unknown>>(event).catch(() => null)
  if (!raw || typeof raw.newPassword !== 'string') {
    return memberProblem(event, 400, 'invalid_body', '請輸入新密碼。', 'Please enter a new password.')
  }
  const stored = readRefreshCookie(event)
  const res = await callUpstream(event, 'POST', 'member/auth/change-password', {
    headers: { authorization: bearer },
    body: {
      currentPassword: typeof raw.currentPassword === 'string' && raw.currentPassword ? raw.currentPassword : undefined,
      newPassword: raw.newPassword,
      tokenDelivery: 'body',
    },
  })
  if (res.status === 200) {
    const session = adoptSession(event, res.json, stored?.persistent ?? false)
    if (session) {
      setNoStore(event)
      return session
    }
    return memberProblem(event, 502, 'upstream_unavailable', '服務暫時無法使用，請稍後再試。')
  }
  return sendUpstream(event, res)
})
