import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { ALL_NAV_ITEMS } from '@/data/nav'

const AdminLayout = () => import('@/layouts/AdminLayout.vue')
const DashboardView = () => import('@/views/DashboardView.vue')
const NewsListView = () => import('@/views/news/NewsListView.vue')
const NewsEditView = () => import('@/views/news/NewsEditView.vue')
const PlaceholderView = () => import('@/views/PlaceholderView.vue')
const NotFoundView = () => import('@/views/NotFoundView.vue')

/**
 * 已經真的做出功能的路徑，優先於「還沒做」的通用佔位路由。
 * 對照 docs/21-admin-ui.md §10：外殼＋儀表板＋新聞與故事列表／編輯頁。
 */
const IMPLEMENTED_ROUTES: RouteRecordRaw[] = [
  { path: '/dashboard', name: 'dashboard', component: DashboardView, meta: { label: '儀表板', code: 'A' } },
  { path: '/content/news', name: 'news-list', component: NewsListView, meta: { label: '新聞與故事', code: 'B2' } },
  {
    path: '/content/news/new',
    name: 'news-new',
    component: NewsEditView,
    meta: { label: '新增文章', code: 'B2' },
  },
  {
    path: '/content/news/:id/edit',
    name: 'news-edit',
    component: NewsEditView,
    props: true,
    meta: { label: '編輯文章', code: 'B2' },
  },
]

const implementedPaths = new Set(IMPLEMENTED_ROUTES.map((route) => route.path))

/** 側欄裡「尚未建置」的模組，全部指到同一個 PlaceholderView，帶上模組名稱與代號 */
const PLACEHOLDER_ROUTES: RouteRecordRaw[] = ALL_NAV_ITEMS.filter(
  (item) => !item.implemented && !implementedPaths.has(item.path),
).map((item) => ({
  path: item.path,
  name: `placeholder-${item.code}`,
  component: PlaceholderView,
  meta: { label: item.label, code: item.code },
}))

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: AdminLayout,
      children: [
        { path: '', redirect: '/dashboard' },
        ...IMPLEMENTED_ROUTES,
        ...PLACEHOLDER_ROUTES,
      ],
    },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundView },
  ],
})

export default router
