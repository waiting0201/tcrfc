/**
 * B2「關聯（球員／球隊／賽事／課程／夥伴）」的選擇器資料來源（S1-5，S1-7 續作補上「球隊」）。
 *
 * 🔴 這裡現在做「球員」「球隊」「賽事」三種——`apps/api` 目前只有這三種目標的唯讀清單是這個帳號
 * 打得到的：公開端點 `GET /api/v1/{club}/players`／`GET /api/v1/{club}/schedule`（不需要登入，
 * 任何角色都能查）；**球隊**自 S1-7 起改接 `GET /api/v1/admin/{club}/teams`（權限碼
 * `team.team.view`，C1 俱樂部範圍端點）——這**不是**先前回報的那支系統管理員限定端點
 * （`GET /api/v1/admin/teams`，`system.team_grant.view`，那支是 J4 球隊授權下拉選單用的
 * 全域端點，兩者恰好同名資源但完全不同）。主站規劃書 §6「球隊／賽事」欄矩陣把
 * `team.team.view` 授予「內容編輯」等唯讀角色（見 apps/api/README.md「S1-7」「角色授予」表），
 * 寫新聞的 `content_editor` 帳號因此打得到這支端點，不會得到 403，任務指示要求的查證已完成。
 * **課程**與**夥伴**則是後端根本還沒有對應模組（沒有 `Features/Programs`／`Features/Partners`）。
 * 兩者已回報，見 apps/admin/README.md「已知的 API 缺口」與 apps/api/README.md「S1-5」。
 *
 * 不對這兩種類型改用假資料或自己兜一份清單頂著——沒有真的清單就不提供選擇器，這是任務指示
 * 「若後端缺列出這些目標的唯讀端點就停下該部分回報，不要改 apps/api」的字面意思。
 */
import { apiRequest } from './http'
import { listAdminClubTeams } from './adminTeams'

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

/**
 * 球隊選擇器選項（S1-7 續作）。標籤用中文名稱，缺中文名稱時退而求其次顯示隊別代號——
 * 隊別代號（`D1`／`BW1`）本身是內部識別鍵，docs/06 §1「後台介面用語」不希望它單獨作為
 * 主要顯示文字，但這裡是「找不到名稱時的最後手段」，不是常態，比照既有球員選擇器同一種
 * 退而求其次寫法（`p.name?.trim() || '（未命名球員）'`）。一次抓滿（這個俱樂部自己的球隊，
 * 遠低於需要分頁的量），選擇器用 `filterable` 讓使用者自行打字篩選。
 */
export async function listTeamRelationOptions(club: string): Promise<RelationTargetOption[]> {
  const teams = await listAdminClubTeams(club)
  return teams.map((t) => ({
    id: t.id,
    label: t.nameZh?.trim() || t.code,
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
