// POST /api/member-auth/logout — 登出目前這台裝置：撤銷更新權杖並清除 Cookie（S2-11）。
// 後端撤銷失敗（API 沒起來）也一律清掉本站 Cookie——使用者按了登出，這一端必須確實登出。
export default defineEventHandler(async (event) => {
  assertSameOrigin(event)
  const stored = readRefreshCookie(event)
  clearRefreshCookie(event)
  setNoStore(event)
  if (stored) {
    await callUpstream(event, 'POST', 'member/auth/logout', {
      body: { refreshToken: stored.token, tokenDelivery: 'body' },
    })
  }
  setResponseStatus(event, 204)
  return null
})
