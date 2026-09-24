/**
 * B2「關聯（球員／球隊／賽事／課程／夥伴）」的選擇器資料來源（S1-5）。
 *
 * 🔴 這裡刻意只做「球員」與「賽事」兩種——`apps/api` 目前只有這兩種目標的唯讀清單是這個帳號
 * 打得到的：公開端點 `GET /api/v1/{club}/players`／`GET /api/v1/{club}/schedule`（不需要登入，
 * 任何角色都能查）。**球隊**雖然有 `GET /api/v1/admin/teams`，但那支端點的權限碼
 * `system.team_grant.view` 是系統管理員限定（見 `Features/AdminTeams/AdminTeamsEndpoints.cs`），
 * 寫新聞的內容編輯角色（`content_editor`／`team_competition` 等）本來就沒有這個權限碼，拿來做
 * 這裡的選擇器，一般寫新聞的帳號打開下拉選單只會得到 403，不是真的可用；**課程**與**夥伴**則是
 * 後端根本還沒有對應模組（沒有 `Features/Programs`／`Features/Partners`）。三者皆已回報，見
 * apps/admin/README.md「已知的 API 缺口」與 apps/api/README.md「S1-5」。
 *
 * 不對這三種類型改用假資料或自己兜一份清單頂著——沒有真的清單就不提供選擇器，這是任務指示
 * 「若後端缺列出這些目標的唯讀端點就停下該部分回報，不要改 apps/api」的字面意思。
 */
import { apiRequest } from './http'

interface PublicPagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

interface PublicPlayerDto {
  id: string
  teamCode: string
  shirtNo?: number | null
  position?: string | null
  name?: string | null
}

interface PublicMatchDto {
  id: string
  matchOn: string
  homeAway?: string | null
  opponent?: string | null
  competitionName?: string | null
}

export interface RelationTargetOption {
  id: string
  label: string
}

const HOME_AWAY_LABEL: Record<string, string> = { home: '主場', away: '客場' }

/**
 * 球員選擇器選項。標籤刻意不帶隊別代號（`teamCode` 是 `D1`／`U15` 這種內部代號，
 * docs/06-conventions.md §1「後台介面用語」不得出現在畫面文字），只用姓名與背號辨識。
 * 一次抓滿（`pageSize=200`，全系統目前不到 30 位球員），選擇器本身用 `filterable`
 * 讓使用者在瀏覽器端打字篩選，不需要伺服器端搜尋 API。
 */
export async function listPlayerRelationOptions(club: string): Promise<RelationTargetOption[]> {
  const page = await apiRequest<PublicPagedResult<PublicPlayerDto>>(
    `/api/v1/${club}/players?pageSize=200&lang=zh`,
  )
  return page.items.map((p) => ({
    id: p.id,
    label: `${p.name?.trim() || '（未命名球員）'}${p.shirtNo != null ? ` · 背號 ${p.shirtNo}` : ''}`,
  }))
}

/** 賽事選擇器選項。同樣一次抓滿（`pageSize=100`，目前種子資料 21 場）。 */
export async function listMatchRelationOptions(club: string): Promise<RelationTargetOption[]> {
  const page = await apiRequest<PublicPagedResult<PublicMatchDto>>(
    `/api/v1/${club}/schedule?pageSize=100&lang=zh`,
  )
  return page.items.map((m) => {
    const homeAway = m.homeAway ? HOME_AWAY_LABEL[m.homeAway] : undefined
    const opponent = m.opponent?.trim() || '對手未定'
    const suffix = [homeAway, m.competitionName?.trim()].filter(Boolean).join(' · ')
    return {
      id: m.id,
      label: `${m.matchOn} 對 ${opponent}${suffix ? `（${suffix}）` : ''}`,
    }
  })
}
