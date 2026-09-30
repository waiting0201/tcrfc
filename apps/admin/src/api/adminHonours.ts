/**
 * C5 榮譽與里程碑後台端點，對照 apps/api/README.md「E1a」節「C5」。
 */
import { apiRequest, apiUploadRequest, buildMultipart, buildQuery, type BilingualContentInput } from './adminCommon'

// ───────────── 榮譽 ─────────────

export interface AchievementDto {
  id: string
  seasonId: string
  seasonCode: string
  teamId: string
  teamCode: string
  teamNameZh?: string | null
  year?: number | null
  competitionName?: string | null
  placing?: string | null
  updatedAt: string
}

export interface SaveAchievementPayload {
  seasonId: string
  teamId: string
  year?: number | null
  competitionName: string
  placing: string
}

const achievementBase = (club: string) => `/api/v1/admin/${club}/achievements`

export function listAchievements(club: string, params: { teamId?: string; seasonId?: string; year?: number } = {}): Promise<AchievementDto[]> {
  return apiRequest<AchievementDto[]>(`${achievementBase(club)}${buildQuery(params)}`)
}

export function createAchievement(club: string, payload: SaveAchievementPayload): Promise<AchievementDto> {
  return apiRequest<AchievementDto>(achievementBase(club), { method: 'POST', body: payload })
}

export function updateAchievement(club: string, id: string, payload: SaveAchievementPayload): Promise<AchievementDto> {
  return apiRequest<AchievementDto>(`${achievementBase(club)}/${id}`, { method: 'PUT', body: payload })
}

export function deleteAchievement(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${achievementBase(club)}/${id}`, { method: 'DELETE' })
}

// ───────────── 里程碑 ─────────────

export interface MilestoneLocaleContent {
  title: string
  description?: string | null
  imageAlt?: string | null
}

export interface MilestoneDto {
  id: string
  happenedOn: string
  sortOrder: number
  isVisible: boolean
  imageKey?: string | null
  imageUrl?: string | null
  imageThumbUrl?: string | null
  imageWidth?: number | null
  imageHeight?: number | null
  zh: MilestoneLocaleContent
  en?: MilestoneLocaleContent | null
  updatedAt: string
}

export interface SaveMilestonePayload {
  happenedOn: string
  sortOrder: number
  isVisible: boolean
  content: BilingualContentInput<MilestoneLocaleContent>
  removeImage?: boolean
}

const milestoneBase = (club: string) => `/api/v1/admin/${club}/milestones`

export function listMilestones(club: string): Promise<MilestoneDto[]> {
  return apiRequest<MilestoneDto[]>(milestoneBase(club))
}

export function getMilestone(club: string, id: string): Promise<MilestoneDto> {
  return apiRequest<MilestoneDto>(`${milestoneBase(club)}/${id}`)
}

export function createMilestone(club: string, payload: SaveMilestonePayload, image: File | null): Promise<MilestoneDto> {
  return apiUploadRequest<MilestoneDto>(milestoneBase(club), buildMultipart(payload, { image }), { method: 'POST' })
}

export function updateMilestone(club: string, id: string, payload: SaveMilestonePayload, image: File | null): Promise<MilestoneDto> {
  return apiUploadRequest<MilestoneDto>(`${milestoneBase(club)}/${id}`, buildMultipart(payload, { image }), { method: 'PUT' })
}

export function deleteMilestone(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${milestoneBase(club)}/${id}`, { method: 'DELETE' })
}
