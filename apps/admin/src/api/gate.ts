/**
 * 寫入端點開發模式開關的前端偵測（apps/api/README.md「寫入端點開發模式開關」）。
 *
 * 這組端點在 `ASPNETCORE_ENVIRONMENT=Development` 且 `ENABLE_UNSAFE_DEV_WRITES=true` 同時成立
 * 才會被註冊，關閉時整條路由不存在，回應是**沒有 body 的 404**——跟「這篇文章真的找不到」
 * 長得一模一樣（都是 404），差別只在有沒有 JSON body。用後台新聞清單端點（不帶資料 id，
 * 一定不會落到「文章找不到」這個分支）探一次，就能可靠分辨兩種情況，讓列表頁與編輯頁
 * 共用同一個判斷結果，不必各自猜。
 */
import { apiRequest, AdminApiError } from './http'
import type { AdminArticlePage } from './adminNews'

export type GateStatus = 'open' | 'closed' | 'unreachable'

// 每個俱樂部代碼各快取一份，避免列表頁與編輯頁在同一次瀏覽中重複探測。
const cache = new Map<string, GateStatus>()

export async function checkGateStatus(club: string, options?: { force?: boolean }): Promise<GateStatus> {
  if (!options?.force) {
    const cached = cache.get(club)
    if (cached) return cached
  }

  try {
    await apiRequest<AdminArticlePage>(`/api/v1/admin/${club}/news?page=1&pageSize=1`, {
      ambiguousNotFoundIsRoute: true,
    })
    cache.set(club, 'open')
    return 'open'
  } catch (error) {
    if (error instanceof AdminApiError) {
      if (error.kind === 'gate-closed-or-unreachable') {
        cache.set(club, 'closed')
        return 'closed'
      }
      if (error.kind === 'network') {
        // 不快取：伺服器可能只是還沒啟動完成，下次重試應該重新探測。
        return 'unreachable'
      }
      // 其他錯誤（例如俱樂部代碼真的不存在）代表路由確實有掛上去，開關是開的。
      cache.set(club, 'open')
      return 'open'
    }
    return 'unreachable'
  }
}

export function invalidateGateCache(club: string): void {
  cache.delete(club)
}
