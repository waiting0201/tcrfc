/** N2 捐款項目管理：`/api/v1/donation-platform/admin/projects`。 */
import { ADMIN_ROOT, apiRequest, apiUpload, buildQuery } from './http'

const ROOT = `${ADMIN_ROOT}/projects`

export type ProjectStatus = 'draft' | 'published'
export type InvoiceMode = 'b2c_invoice' | 'donation_receipt'

export interface ProjectListItem {
  id: string
  slug: string
  nameZh: string | null
  nameEn: string | null
  status: ProjectStatus
  sortOrder: number
  invoiceMode: InvoiceMode
  projectSharePct: number
  minAmount: number | null
  maxAmount: number | null
  coverUrl: string | null
  charityName: string | null
}

export interface ProjectDetail {
  id: string
  slug: string
  nameZh: string | null
  nameEn: string | null
  oneLinerZh: string | null
  oneLinerEn: string | null
  /** 區塊編輯器 JSON：本畫面不提供編輯，儲存時原樣送回（整筆取代，沒帶就會被清空）。 */
  descriptionZh: unknown
  descriptionEn: unknown
  fundUsageZh: string | null
  fundUsageEn: string | null
  coverAltZh: string | null
  coverAltEn: string | null
  coverUrl: string | null
  status: ProjectStatus
  sortOrder: number
  invoiceMode: InvoiceMode
  projectSharePct: number
  minAmount: number | null
  maxAmount: number | null
  amountOptions: number[]
  charityRefCode: string | null
  charityName: string | null
  charityProgramRefCode: string | null
  charityProgramName: string | null
}

export interface ProjectInput {
  nameZh: string
  nameEn: string | null
  oneLinerZh: string | null
  oneLinerEn: string | null
  descriptionZh: unknown
  descriptionEn: unknown
  fundUsageZh: string | null
  fundUsageEn: string | null
  coverAltZh: string | null
  coverAltEn: string | null
  minAmount: number | null
  maxAmount: number | null
  amountOptions: number[]
  projectSharePct?: number
  invoiceMode: InvoiceMode
  charityRefCode: string | null
  charityProgramRefCode: string | null
  sortOrder?: number
}

/**
 * 撥付對象與計畫的選單資料（官網清單的唯讀複本）。`isActive=false` 代表官網已不再公開：
 * 只會在「編輯既有項目、該項目目前選用它」時出現，畫面要標示且不得讓人重新選用。
 * `lastSyncedAt` 為 null 代表從未從官網更新過。`refCode` 是程式內部識別，不得顯示在畫面上。
 */
export interface CharityRefOptions {
  charities: { refCode: string; name: string; nameEn: string | null; isActive: boolean }[]
  programs: { refCode: string; name: string; charityRefCode: string; nameEn: string | null; isActive: boolean }[]
  lastSyncedAt: string | null
}

export interface CharityRefSyncCounts {
  created: number
  updated: number
  deactivated: number
  reactivated: number
  unchanged: number
  /** 同步後目前公開的總數。 */
  activeTotal: number
}

export interface CharityRefSyncResult {
  syncedAt: string
  charities: CharityRefSyncCounts
  programs: CharityRefSyncCounts
}

export const listProjects = (status?: string) => apiRequest<ProjectListItem[]>(`${ROOT}${buildQuery({ status })}`)
export const getProject = (id: string) => apiRequest<ProjectDetail>(`${ROOT}/${id}`)
/** 編輯既有項目時帶 `projectId`，後端會把該項目目前選用、但官網已不再公開的撥付對象／計畫一併回傳（`isActive=false`）。 */
export const getCharityRefs = (projectId?: string) => apiRequest<CharityRefOptions>(`${ROOT}/charity-refs${buildQuery({ projectId })}`)
/** 「從官網更新清單」：無請求本文。409 更新中、502 官網暫時取不到（複本完全沒變動）、503 服務未設定。 */
export const syncCharityRefs = () => apiRequest<CharityRefSyncResult>(`${ROOT}/charity-refs/sync`, { method: 'POST' })
export const createProject = (input: ProjectInput) => apiRequest<ProjectDetail>(ROOT, { method: 'POST', body: input })
export const updateProject = (id: string, input: ProjectInput) => apiRequest<ProjectDetail>(`${ROOT}/${id}`, { method: 'PUT', body: input })
export const publishProject = (id: string) => apiRequest<ProjectDetail>(`${ROOT}/${id}/publish`, { method: 'POST' })
export const unpublishProject = (id: string) => apiRequest<ProjectDetail>(`${ROOT}/${id}/unpublish`, { method: 'POST' })
export const uploadProjectCover = (id: string, file: File) => apiUpload<ProjectDetail>(`${ROOT}/${id}/cover`, file)
export const removeProjectCover = (id: string) => apiRequest<void>(`${ROOT}/${id}/cover`, { method: 'DELETE' })

/**
 * 只編輯內文三項。**省略（不放進物件）＝不變**；說明內文送空物件、其他欄位送空字串＝清空。
 * 這個端點不碰分潤、金額選項與撥付對象，所以沒有分潤授權的角色也能存。
 */
export interface ProjectContentInput {
  oneLinerZh?: string
  oneLinerEn?: string
  descriptionZh?: unknown
  descriptionEn?: unknown
  fundUsageZh?: string
  fundUsageEn?: string
}
export const updateProjectContent = (id: string, input: ProjectContentInput) =>
  apiRequest<ProjectDetail>(`${ROOT}/${id}/content`, { method: 'PUT', body: input })
