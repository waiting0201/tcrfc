/**
 * 儀表板「待辦提醒」的前往位置與徽章數字（儀表板待辦區與頂欄鈴鐺共用同一份，不得各自複製）。
 * 內部識別（code）只用來查路由，不顯示。
 */
import type { DashboardTodo } from '@/api/adminDashboard'

const TODO_ROUTES: Record<string, string> = {
  enquiries_new: '/inquiries/inbox',
  registrations_pending: '/programs/enrollments',
  sessions_closing_soon: '/programs/sessions',
  sponsor_contracts_expiring: '/business/sponsorships',
}

/** 待辦類別的清單頁。 */
export function todoRoute(todo: DashboardTodo): string | null {
  return TODO_ROUTES[todo.code] ?? null
}

/** 能直接開到單筆的待辦（詢問、梯次），其餘開到清單頁。 */
export function todoItemRoute(todo: DashboardTodo, id: string): string | null {
  if (todo.code === 'enquiries_new') return `/inquiries/inbox/${id}/edit`
  if (todo.code === 'sessions_closing_soon') return `/programs/sessions/${id}/edit`
  return todoRoute(todo)
}

/** 各類待辦筆數總和。 */
export function sumTodoCounts(todos: readonly DashboardTodo[]): number {
  return todos.reduce((sum, t) => sum + (Number.isFinite(t.count) && t.count > 0 ? t.count : 0), 0)
}

/** 鈴鐺徽章文字：0 不顯示（空字串），超過 99 顯示 99+。 */
export function formatBadge(total: number): string {
  if (total <= 0) return ''
  return total > 99 ? '99+' : String(total)
}
