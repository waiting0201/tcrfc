import { computed, reactive, ref } from 'vue'
import { listPublicClubs } from '@/api/publicClubs'
import tcrfcCrest from '@/assets/brand/tcrfc-mark-pink.svg'
import bwCrest from '@/assets/brand/bw-crest-48.png'

/**
 * 站台切換器可切換的俱樂部清單。
 *
 * 🔴 **已知 API 缺口（本輪回報，未動手改 `apps/api`）**：規劃書要求切換器「依登入者的俱樂部授權
 * 列出可切換的站台」（主站規劃書 §5.4／§6），但 `apps/api` 目前**沒有任何端點能讓一般帳號查詢
 * 自己的俱樂部授權**——`GET /accounts/{id}/club-grants` 需要 `system.club_grant.view`
 * （`sysadmin_only`），非系統管理員帳號打自己的 id 一樣會被 403 擋下；`/auth/login`／
 * `/auth/refresh` 的回應也不含 `primaryClubId` 或授權清單（只有 `username`／`isSuperAdmin`／
 * `mustChangePassword`／`twoFactorEnabled`，見 `Features/AdminAuth/AdminAuthDtos.cs`）。
 *
 * 目前的處理方式：一律用公開的 `GET /api/v1/clubs`（不需要登入）列出系統裡「有哪些俱樂部」，
 * 讓切換器可以動作；**這不等於「這個帳號真的被授權存取」**——真正的範圍檢查一律由後端
 * `AdminClubAuthorizer` 在每一次俱樂部範圍請求時即時判斷（docs/21-admin-ui.md §5：「切換器是
 * 介面便利，不是安全邊界」本來就是這個意思），選到未授權的俱樂部時，該頁會如實顯示後端回傳的
 * 403 訊息（「你沒有被授權存取俱樂部「...」的後台資料。」），不會誤導使用者以為操作成功。
 * **建議後端補一支 `GET /api/v1/admin/auth/me`**，回傳 `displayName`／`primaryClubId`／
 * 目前有效的俱樂部授權清單／角色代碼，屆時把這裡改回真正的「只列出被授權的俱樂部」即可，
 * 不影響呼叫端（`availableClubs`／`activeClubId` 的介面不需要變）。
 */
export interface ClubOption {
  code: string
  name: string
  crestUrl: string
}

const FALLBACK_CREST: Record<string, string> = {
  tcrfc: tcrfcCrest,
  bw: bwCrest,
}

function resolveCrest(code: string): string {
  return FALLBACK_CREST[code] ?? tcrfcCrest
}

interface ClubAccessState {
  clubs: ClubOption[]
  loaded: boolean
  loading: boolean
}

const state = reactive<ClubAccessState>({ clubs: [], loaded: false, loading: false })

export const availableClubs = computed(() => state.clubs)

const internalActiveClubId = ref('tcrfc')

/** 目前站台切換器選到的俱樂部代碼，模組層級單例、跨元件共用（沿用改版前 `data/activeClub.ts`
 * 的既有設計理由：後台各種俱樂部範圍端點都要知道「現在選的是哪一隊」）。 */
export const activeClubId = computed({
  get: () => internalActiveClubId.value,
  set: (value: string) => {
    internalActiveClubId.value = value
  },
})

export async function ensureClubsLoaded(force = false): Promise<void> {
  if (state.loading) return
  if (state.loaded && !force) return
  state.loading = true
  try {
    const clubs = await listPublicClubs()
    state.clubs = clubs.map((c) => ({ code: c.code, name: c.name, crestUrl: resolveCrest(c.code) }))
    state.loaded = true
    if (!clubs.some((c) => c.code === internalActiveClubId.value) && clubs.length > 0) {
      internalActiveClubId.value = clubs[0].code
    }
  } catch {
    // 讀不到俱樂部清單就先留空——各頁面既有的「連不上後台服務」錯誤畫面會處理接下來的 API 呼叫失敗，
    // 這裡不重複跳錯誤訊息。
  } finally {
    state.loading = false
  }
}

export function resetClubAccess(): void {
  state.clubs = []
  state.loaded = false
  internalActiveClubId.value = 'tcrfc'
}
