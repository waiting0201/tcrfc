import { computed } from 'vue'
import { hasPermission, useIsSuperAdmin } from './useRolePermissions'

/**
 * 「檢視／新增／修改／刪除」四件套的權限判斷。權限碼命名規則是 `{前綴}.view|create|update|delete`
 * （apps/api/README.md「E1a」節權限碼矩陣），這裡只決定「要不要顯示按鈕」，**不是安全邊界**——
 * 送出後後端每支端點仍會再檢查一次（見 `useRolePermissions.ts` 檔頭）。
 */
export function useCrudPermissions(prefix: string) {
  const isSuperAdmin = useIsSuperAdmin()
  return {
    canView: computed(() => isSuperAdmin.value || hasPermission(`${prefix}.view`)),
    canCreate: computed(() => isSuperAdmin.value || hasPermission(`${prefix}.create`)),
    canUpdate: computed(() => isSuperAdmin.value || hasPermission(`${prefix}.update`)),
    canDelete: computed(() => isSuperAdmin.value || hasPermission(`${prefix}.delete`)),
  }
}

/** 只有檢視與修改兩種權限的（下載名單跟進、慈善導流設定）。 */
export function useViewUpdatePermissions(prefix: string) {
  const isSuperAdmin = useIsSuperAdmin()
  return {
    canView: computed(() => isSuperAdmin.value || hasPermission(`${prefix}.view`)),
    canUpdate: computed(() => isSuperAdmin.value || hasPermission(`${prefix}.update`)),
  }
}

/** 名單匯出（含個資的受限權限）。 */
export function useCanExportLeads() {
  const isSuperAdmin = useIsSuperAdmin()
  return computed(() => isSuperAdmin.value || hasPermission('business.lead.export'))
}
