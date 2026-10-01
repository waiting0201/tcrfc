// POST /api/member-auth/refresh — 以 HttpOnly Cookie 裡的更新權杖換新的存取權杖（S2-11）。
//
// 🔴 重放偵測：後端「舊更新權杖被重放＝外流，撤銷該會員全部更新權杖」。同一份 Cookie 同時送兩次
// refresh（兩個分頁、或同分頁重複觸發）第二次就是重放，會把使用者全部登出。防線分兩層：
//   1. 瀏覽器端：Web Locks（跨分頁互斥）＋分頁內 single-flight＋BroadcastChannel 同步新權杖
//      （app/composables/useMemberSession.ts）。
//   2. 本檔：同一個更新權杖「正在進行中」的請求合併成一次後端呼叫，所有等待者拿到同一份結果
//      （只合併進行中的請求，不留結果快取，避免變成「用舊權杖換新工作階段」的後門）。
// 兩層都做是因為 Web Locks 不是每個瀏覽器環境都有，本檔是最後防線。
const inflight = new Map<string, Promise<{ status: number, upstream: Awaited<ReturnType<typeof callUpstream>> }>>()

export default defineEventHandler(async (event) => {
  assertSameOrigin(event)
  const stored = readRefreshCookie(event)
  if (!stored) {
    return memberProblem(event, 401, 'no_session', '尚未登入。')
  }

  let pending = inflight.get(stored.token)
  if (!pending) {
    pending = callUpstream(event, 'POST', 'member/auth/refresh', {
      body: { refreshToken: stored.token, tokenDelivery: 'body' },
    }).then(upstream => ({ status: upstream.status, upstream }))
    inflight.set(stored.token, pending)
    pending.finally(() => inflight.delete(stored.token)).catch(() => {})
  }
  const { upstream } = await pending

  if (upstream.status === 200) {
    const session = adoptSession(event, upstream.json, stored.persistent)
    if (session) {
      setNoStore(event)
      return session
    }
    return memberProblem(event, 502, 'upstream_unavailable', '服務暫時無法使用，請稍後再試。')
  }
  // 401（過期／被撤銷）：Cookie 已無用，清掉；其他錯誤（429／502）保留 Cookie 讓使用者可以重試
  if (upstream.status === 401) clearRefreshCookie(event)
  return sendUpstream(event, upstream)
})
