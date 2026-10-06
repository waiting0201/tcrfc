/** 選單編輯用的樹節點，以及它與後端形狀之間的轉換與前端驗證（規則與 apps/api/README.md「H 批」§5 一致）。 */
import type { AdminMenuItem, UpsertMenuItem } from '@/api/adminSiteSettings'

export interface MenuNode {
  /** 僅供畫面 key 用，不送後端。 */
  key: string
  /** 後端既有項目的 id；新增的沒有。有 id 沿用、沒有新增、不在請求裡的既有項目會被刪除。 */
  id: string | null
  labelZh: string
  labelEn: string
  url: string
  isExternal: boolean
  children: MenuNode[]
}

export const MENU_MAX_DEPTH = 3
export const MENU_MAX_ITEMS = 100

let seq = 0
export function newMenuKey(): string {
  seq += 1
  return `n${seq}`
}

export function fromDto(items: AdminMenuItem[]): MenuNode[] {
  return items.map((it) => ({
    key: newMenuKey(),
    id: it.id,
    labelZh: it.labelZh ?? '',
    labelEn: it.labelEn ?? '',
    url: it.url ?? '',
    isExternal: it.isExternal,
    children: fromDto(it.children ?? []),
  }))
}

export function toRequest(nodes: MenuNode[]): UpsertMenuItem[] {
  return nodes.map((n) => ({
    id: n.id,
    labelZh: n.labelZh.trim(),
    labelEn: n.labelEn.trim() || null,
    url: n.url.trim() || null,
    isExternal: n.isExternal,
    children: toRequest(n.children),
  }))
}

export function countNodes(nodes: MenuNode[]): number {
  return nodes.reduce((sum, n) => sum + 1 + countNodes(n.children), 0)
}

export interface TreeProblems {
  /** 不屬於特定項目的問題（例如項目總數過多）；沒有為 null。 */
  general: string | null
  /** 項目 key（畫面用）→ 該項目的問題（日常中文，每個項目只回第一個）。 */
  byKey: Record<string, string>
}

/** 一次檢查整棵樹，回傳所有有問題的項目；沒有問題時 general 為 null、byKey 為空。 */
export function validateTreeAll(nodes: MenuNode[]): TreeProblems {
  const result: TreeProblems = { general: null, byKey: {} }
  if (countNodes(nodes) > MENU_MAX_ITEMS) result.general = `每個選單最多 ${MENU_MAX_ITEMS} 個項目。`
  const walk = (list: MenuNode[], depth: number) => {
    for (const n of list) {
      const problem = nodeProblem(n, depth)
      if (problem) result.byKey[n.key] = problem
      walk(n.children, depth + 1)
    }
  }
  walk(nodes, 1)
  return result
}

function nodeProblem(n: MenuNode, depth: number): string | null {
  const name = n.labelZh.trim()
  if (!name) return '請填寫這個項目的中文名稱。'
  const url = n.url.trim()
  if (n.children.length === 0 && !url) return `「${name}」沒有子項目，必須填寫連結。`
  if (url) {
    if (n.isExternal) {
      if (!/^https?:\/\/\S+$/i.test(url)) return `「${name}」是外部連結，請填寫完整的 http:// 或 https:// 網址。`
    } else if (!url.startsWith('/') || url.startsWith('//') || /\s/.test(url) || /^[a-z][a-z0-9+.-]*:/i.test(url)) {
      return `「${name}」是站內連結，請以 / 開頭、不含空白與網域；若要連到其他網站請勾選「外部連結」。`
    }
  }
  if (depth >= MENU_MAX_DEPTH && n.children.length > 0) return '選單最多 3 層。'
  return null
}
