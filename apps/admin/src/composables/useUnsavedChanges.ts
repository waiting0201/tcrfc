import { onBeforeUnmount, onMounted, type Ref } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { ElMessageBox } from 'element-plus'

/**
 * 離開未儲存的提醒（docs/21-admin-ui.md §3）：路由離開與瀏覽器分頁關閉兩處都要攔。
 * 畫面文字固定：「這頁還有未儲存的變更，確定要離開嗎？」，按鈕「繼續編輯」／「捨棄變更並離開」。
 */
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
    // 現代瀏覽器不再顯示自訂文字，但仍需要設定 returnValue 才會跳出瀏覽器原生確認框
    event.returnValue = ''
  }

  onMounted(() => window.addEventListener('beforeunload', handleBeforeUnload))
  onBeforeUnmount(() => window.removeEventListener('beforeunload', handleBeforeUnload))
}
