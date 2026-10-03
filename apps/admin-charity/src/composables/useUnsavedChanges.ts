import { onBeforeUnmount, onMounted, type Ref } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { ElMessageBox } from 'element-plus'

/** 離開未儲存的提醒：路由離開與瀏覽器分頁關閉兩處都要攔（比照 apps/admin 的同名 composable）。 */
export function useUnsavedChanges(isDirty: Ref<boolean>) {
  onBeforeRouteLeave(async () => {
    if (!isDirty.value) return true
    try {
      await ElMessageBox.confirm('這頁還有未儲存的變更，確定要離開嗎？', '尚未儲存', {
        confirmButtonText: '捨棄變更並離開',
        cancelButtonText: '繼續編輯',
        confirmButtonClass: 'el-button--danger',
        type: 'warning',
      })
      return true
    } catch {
      return false
    }
  })

  function handleBeforeUnload(event: BeforeUnloadEvent) {
    if (!isDirty.value) return
    event.preventDefault()
    event.returnValue = ''
  }

  onMounted(() => window.addEventListener('beforeunload', handleBeforeUnload))
  onBeforeUnmount(() => window.removeEventListener('beforeunload', handleBeforeUnload))
}
