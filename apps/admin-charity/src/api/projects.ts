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

export interface CharityRefOptions {
  charities: { refCode: string; name: string }[]
  programs: { refCode: string; name: string; charityRefCode: string }[]
}

export const listProjects = (status?: string) => apiRequest<ProjectListItem[]>(`${ROOT}${buildQuery({ status })}`)
export const getProject = (id: string) => apiRequest<ProjectDetail>(`${ROOT}/${id}`)
export const getCharityRefs = () => apiRequest<CharityRefOptions>(`${ROOT}/charity-refs`)
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
