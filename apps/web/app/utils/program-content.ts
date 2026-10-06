// app/utils/program-content.ts — 05 課程詳情（`GET /{club}/programs/{slug}`）的型別與內容解析（B-8，2026-10-06）
//
// 動態詳情頁 `programs/[slug]` 與五個固定頁 5.1–5.5 共用同一份：
//   - `content` 是 P1「課程內容」區塊編輯器存的區塊 JSON：`parseBlocksJson` 解得開就走 `PageBlocks`，
//     解不開的舊資料退回純文字（空行分段）。**一律不使用 v-html**。
//   - 是區塊 JSON（即使一個區塊都渲染不出來）就不能退回純文字，否則會把 JSON 原文印在頁面上。
import type { ProgramSessionLike } from '~/utils/program-session'
import type { PageBlockNode } from '#shared/utils/page-blocks'

export interface ProgramPartnerView {
  id: string
  slug: string
  name: string | null
  websiteUrl: string | null
  logoDarkUrl?: string | null
  logoLightUrl?: string | null
}

export interface ProgramSessionView extends ProgramSessionLike {
  id: string
  startOn: string | null
  endOn: string | null
  weeklySchedule: string | null
  capacity: number | null
  enrolledCount: number
  price: number | null
  earlyBirdPrice: number | null
  earlyBirdUntil: string | null
  venueName: string | null
  venueAddress: string | null
}

export interface ProgramDetailView {
  id: string
  slug: string
  isFallbackLocale?: boolean
  audience: string | null
  ageMin: number | null
  ageMax: number | null
  coverUrl: string | null
  name: string | null
  intro: string | null
  content: string | null
  staff: Array<{ id: string, name: string | null }>
  partners: ProgramPartnerView[]
  sessions: ProgramSessionView[]
}

export interface ProgramContentView {
  blocks: PageBlockNode[]
  paragraphs: string[]
}

export function buildProgramContent(content: string | null | undefined, locale: 'zh' | 'en', mediaBaseUrl: string): ProgramContentView {
  const raw = parseBlocksJson(content)
  const blocks = raw ? normalizePageBlocks(raw, { locale, mediaBaseUrl }) : []
  const paragraphs = raw ? [] : (content ?? '').split(/\n{2,}/).map((p) => p.trim()).filter(Boolean)
  return { blocks, paragraphs }
}

export function programAgeText(p: { ageMin: number | null, ageMax: number | null }, isEn: boolean): string | null {
  if (p.ageMin == null) return null
  if (isEn) return p.ageMax != null ? `${p.ageMin}–${p.ageMax} years` : `${p.ageMin} years and over`
  return p.ageMax != null ? `${p.ageMin}–${p.ageMax} 歲` : `${p.ageMin} 歲以上`
}

/** 多個課程的教練團／合作夥伴合併去重（同一人／同一單位掛在多個課程只列一次）。 */
export function uniqueById<T extends { id: string }>(lists: ReadonlyArray<ReadonlyArray<T>>): T[] {
  const seen = new Set<string>()
  const out: T[] = []
  for (const list of lists) for (const x of list) if (!seen.has(x.id)) { seen.add(x.id); out.push(x) }
  return out
}

export function safeHttps(url: string | null | undefined): string | null {
  return url && /^https:\/\//i.test(url) ? url : null
}
