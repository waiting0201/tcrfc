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
    // F5（2026-10-03）：匿名訪客進 /member／/cart／/checkout 等頁會先試著還原登入，這是「正常的
    // 沒登入」不是錯誤，回 401 會讓 console 每頁一條紅字。改回 204（沒有內容＝沒有工作階段），
    // 瀏覽器端（useMemberSession.refresh）把空回應視同「確定沒登入」。更新權杖 Cookie 是
    // HttpOnly，瀏覽器端看不到它存不存在，所以由這裡回答；回應不含任何資訊（有沒有 Cookie
    // 本來就是請求方自己帶來的），安全性不變。Cookie 存在但後端判定無效的 401 維持原樣。
    setResponseStatus(event, 204)
    setNoStore(event)
    return null
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
