import { ref } from 'vue'
import { listAdminWritableTeams, type AdminTeamAdminListItemDto, type AdminWritableTeamDto, type AdminWritableTeamModule } from '@/api/adminTeams'

/**
 * 「我能寫哪些球隊」（`GET /api/v1/admin/{club}/teams/writable`，S1-8 續作新增）的前端共用邏輯。
 * 給 C2 球員（`module: 'player'`）、C3 教練與團隊成員（`module: 'staff'`）、C4 賽程與賽果
 * （`module: 'match'`）三個模組的「所屬球隊／負責梯隊／參賽球隊」選單共用，理由見
 * apps/admin/README.md「已知的 API 缺口彙整」第 14 點與這支端點的契約說明。
 *
 * 這支端點回傳的清單**已經是收斂過的可寫球隊**，畫面上新增資料時直接把選項綁這份清單即可。
 * 但編輯既有資料時，既有關聯的球隊可能不在這份清單內（例如學院管理者打開一線隊的球員／教練／
 * 賽事資料）——這裡不能讓下拉選單悄悄把它排除掉，否則使用者存檔時容易在不知情下把這筆關聯換掉
 * 或整個漏掉。`buildOptions()` 因此把「可寫球隊」與「這筆資料既有但不可寫的球隊」合併成一份
 * 選項清單，後者標示為 `disabled: true` 並附上正確名稱（從俱樂部全部球隊清單查回來，因為它不會
 * 出現在可寫清單裡），畫面再依 `disabled` 呈現鎖定樣式與原因說明。
 */
export function useWritableTeamScope(module: AdminWritableTeamModule) {
  const writableTeams = ref<AdminWritableTeamDto[]>([])

  async function loadWritableTeams(club: string): Promise<void> {
    writableTeams.value = await listAdminWritableTeams(club, module)
  }

  function isWritable(teamId: string): boolean {
    return writableTeams.value.some((t) => t.id === teamId)
  }

  /** 找不到可寫球隊本身的名稱時，回退用 `allTeams`（該俱樂部全部球隊）查——可寫清單本身也帶
   * 名稱，這裡優先用可寫清單的（跟後端同一份資料），查不到才代表是鎖定的既有關聯。 */
  function teamLabel(teamId: string, allTeams: AdminTeamAdminListItemDto[]): string {
    const writable = writableTeams.value.find((t) => t.id === teamId)
    if (writable) return writable.nameZh || writable.code
    const found = allTeams.find((t) => t.id === teamId)
    return found ? found.nameZh || found.code : teamId
  }

  interface TeamSelectOption {
    id: string
    label: string
    disabled: boolean
  }

  function buildOptions(allTeams: AdminTeamAdminListItemDto[], referencedIds: string[]): TeamSelectOption[] {
    const options: TeamSelectOption[] = writableTeams.value.map((t) => ({ id: t.id, label: t.nameZh || t.code, disabled: false }))
    const seen = new Set(options.map((o) => o.id))
    for (const id of referencedIds) {
      if (!id || seen.has(id)) continue
      seen.add(id)
      options.push({ id, label: teamLabel(id, allTeams), disabled: true })
    }
    return options
  }

  function outOfScopeIds(referencedIds: string[]): string[] {
    return referencedIds.filter((id) => !!id && !isWritable(id))
  }

  return { writableTeams, loadWritableTeams, isWritable, teamLabel, buildOptions, outOfScopeIds }
}
