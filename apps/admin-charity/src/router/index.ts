import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const AdminLayout = () => import('@/layouts/AdminLayout.vue')
const LoginView = () => import('@/views/LoginView.vue')
const NotFoundView = () => import('@/views/NotFoundView.vue')

const StoreListView = () => import('@/views/stores/StoreListView.vue')
const StoreEditView = () => import('@/views/stores/StoreEditView.vue')
const ProjectListView = () => import('@/views/projects/ProjectListView.vue')
const ProjectEditView = () => import('@/views/projects/ProjectEditView.vue')
const DonationListView = () => import('@/views/donations/DonationListView.vue')
const SettlementView = () => import('@/views/settlements/SettlementView.vue')
const InvoiceListView = () => import('@/views/invoices/InvoiceListView.vue')
const ReportView = () => import('@/views/reports/ReportView.vue')
const SiteSettingView = () => import('@/views/settings/SiteSettingView.vue')

const routes: RouteRecordRaw[] = [
  { path: '/login', name: 'login', component: LoginView, meta: { label: '登入' } },
  {
    path: '/',
    component: AdminLayout,
    children: [
      { path: '', redirect: '/stores' },
      { path: 'stores', name: 'stores', component: StoreListView, meta: { label: '店家與 QR Code', code: 'N1' } },
      { path: 'stores/new', name: 'store-new', component: StoreEditView, meta: { label: '新增店家', code: 'N1' } },
      {
        path: 'stores/:storeKey/edit',
        name: 'store-edit',
        component: StoreEditView,
        props: true,
        meta: { label: '編輯店家', code: 'N1' },
      },
      { path: 'projects', name: 'projects', component: ProjectListView, meta: { label: '捐款項目管理', code: 'N2' } },
      {
        path: 'projects/new',
        name: 'project-new',
        component: ProjectEditView,
        meta: { label: '新增捐款項目', code: 'N2' },
      },
      {
        path: 'projects/:projectKey/edit',
        name: 'project-edit',
        component: ProjectEditView,
        props: true,
        meta: { label: '編輯捐款項目', code: 'N2' },
      },
      { path: 'donations', name: 'donations', component: DonationListView, meta: { label: '捐款紀錄', code: 'N3' } },
      { path: 'settlements', name: 'settlements', component: SettlementView, meta: { label: '回饋金結算', code: 'N4' } },
      { path: 'invoices', name: 'invoices', component: InvoiceListView, meta: { label: '發票與收據管理', code: 'N5' } },
      { path: 'reports', name: 'reports', component: ReportView, meta: { label: '捐款報表', code: 'N6' } },
      { path: 'settings', name: 'settings', component: SiteSettingView, meta: { label: '站台設定', code: 'N7' } },
    ],
  },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundView },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

export default router
