// POST /api/member-auth/logout-all — 登出全部裝置（需存取權杖）並清除本站 Cookie（S2-11）。
export default defineEventHandler(async (event) => {
  assertSameOrigin(event)
  const bearer = readBearer(event)
  if (!bearer) return memberProblem(event, 401, 'login_required', '請先登入。')
  const res = await callUpstream(event, 'POST', 'member/auth/logout-all', { headers: { authorization: bearer } })
  if (res.status === 204 || res.status === 401) clearRefreshCookie(event)
  return sendUpstream(event, res)
})
