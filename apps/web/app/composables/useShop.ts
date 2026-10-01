// app/composables/useShop.ts — 站內商店 8.3（S3-5）瀏覽器端的 API 存取點
//
// 所有請求都走同源 BFF `/api/shop/…`（`server/api/shop/[...path].ts`）：
//   - 訪客購物車權杖與訂單權杖只存在伺服器設定的 HttpOnly Cookie，這裡完全碰不到、也不需要碰；
//   - 會員：帶 15 分鐘存取權杖（`useMemberSession().authedFetch`，401 會 refresh 一次再重試）；
//   - 會員登入狀態未確認時先 `restore()` 一次（購物車、結帳、訂單頁一律在瀏覽器端才呼叫，SSR 只吐殼）。
// 🔴 這支檔案**不快取任何商店資料**：購物車、庫存、訂單狀態每次都向後端取（docs/14 不得讀快取的五類）。
import type { ShopCart, ShopOrder, ShopOrderListItem } from '#shared/utils/shop'
import { toMemberApiError } from '#shared/utils/member'

type FetchOpts = Parameters<typeof $fetch>[1]

function isLoginRequired(err: unknown): boolean {
  return toMemberApiError(err).code === 'login_required'
}

/** 頁首購物車圖示的件數（讀 BFF 寫的非 HttpOnly 提示 Cookie；只在瀏覽器端有值）。 */
export function useCartBadge() {
  const config = useRuntimeConfig()
  const count = useState<number>('shop-cart-count', () => 0)
  onMounted(() => {
    const club = config.public.club === 'bw' ? 'bw' : 'tcrfc'
    const m = document.cookie.match(new RegExp(`(?:^|; )tcrfc-shop-n-${club}=(\\d{1,4})(?:;|$)`))
    count.value = m ? Number(m[1]) : 0
  })
  return count
}

export function useShop() {
  const { locale } = useLocale()
  const session = useMemberSession()
  const cart = useState<ShopCart | null>('shop-cart', () => null)
  const cartCount = useState<number>('shop-cart-count', () => 0)

  /** 帶會員權杖（若已登入）呼叫商店 BFF；會員權杖換不到時退回訪客身分。 */
  async function call<T>(path: string, options: FetchOpts = {}): Promise<T> {
    if (!import.meta.client) throw new Error('useShop().call 只能在瀏覽器端使用')
    const url = `/api/shop/${path}`
    const merged = { ...options, query: { lang: locale.value, ...(options?.query as Record<string, unknown> | undefined) } }
    if (!session.restored.value) await session.restore()
    if (session.isLoggedIn.value) {
      try {
        return await session.authedFetch<T>(url, merged as Record<string, unknown>)
      }
      catch (err) {
        if (!isLoginRequired(err)) throw err
        // 登入已過期：繼續以訪客身分
      }
    }
    const raw = $fetch as unknown as (u: string, o: Record<string, unknown>) => Promise<T>
    return await raw(url, merged as Record<string, unknown>)
  }

  function setCart(next: ShopCart): ShopCart {
    cart.value = next
    cartCount.value = next.itemCount
    return next
  }

  /** 讀取購物車；已登入時改呼叫 merge（訪客購物車並入會員購物車，重複呼叫安全；沒有訪客購物車時 BFF 直接回會員購物車）。 */
  async function loadCart(): Promise<ShopCart> {
    if (!session.restored.value) await session.restore()
    const next = session.isLoggedIn.value
      ? await call<ShopCart>('cart/merge', { method: 'POST' }).catch(async (err) => {
        // 會員權杖剛好失效：退回訪客讀取
        if (isLoginRequired(err)) return await call<ShopCart>('cart')
        throw err
      })
      : await call<ShopCart>('cart')
    return setCart(next)
  }

  async function addToCart(variantId: string, quantity: number): Promise<ShopCart> {
    return setCart(await call<ShopCart>('cart/items', { method: 'POST', body: { variantId, quantity } }))
  }

  async function setQuantity(variantId: string, quantity: number): Promise<ShopCart> {
    return setCart(await call<ShopCart>(`cart/items/${variantId}`, { method: 'PUT', body: { quantity } }))
  }

  async function removeItem(variantId: string): Promise<ShopCart> {
    return setCart(await call<ShopCart>(`cart/items/${variantId}`, { method: 'DELETE' }))
  }

  async function checkout(body: Record<string, unknown>, idempotencyKey: string): Promise<ShopOrder> {
    const order = await call<ShopOrder>('checkout', { method: 'POST', body, headers: { 'idempotency-key': idempotencyKey } })
    cart.value = null
    cartCount.value = 0
    return order
  }

  const orderPath = (orderNo: string) => `orders/${encodeURIComponent(orderNo)}`
  const getOrder = (orderNo: string) => call<ShopOrder>(orderPath(orderNo))
  const payOrder = (orderNo: string) => call<ShopOrder>(`${orderPath(orderNo)}/pay`, { method: 'POST' })
  const confirmOrder = (orderNo: string, transactionId: string) =>
    call<ShopOrder>(`${orderPath(orderNo)}/confirm`, { method: 'POST', body: { transactionId } })
  const cancelOrder = (orderNo: string) => call<ShopOrder>(`${orderPath(orderNo)}/cancel`, { method: 'POST' })
  const lookupOrder = (body: { orderNo: string, email: string } | { token: string }) =>
    call<ShopOrder>('orders/lookup', { method: 'POST', body })
  const myOrders = () => call<ShopOrderListItem[]>('my-orders')

  return {
    cart, cartCount, session, call,
    loadCart, addToCart, setQuantity, removeItem,
    checkout, getOrder, payOrder, confirmOrder, cancelOrder, lookupOrder, myOrders,
  }
}
