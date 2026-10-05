/**
 * 全後台數字輸入框的「超界提示」（B-13）。
 *
 * Element Plus 的 `el-input-number` 在使用者輸入超出 min／max 的數字後，會在失焦時**靜默**改成上限或下限，
 * 使用者不知道自己輸入的值被改了。這裡用文件層的事件監聽統一處理，不必逐一改 94 個輸入框：
 *   1. 輸入時記下使用者實際打的文字（`input` 事件）。
 *   2. 失焦後（讓元件先完成它自己的修正）比對畫面上的值；不一樣就跳出提示說明改成了什麼。
 * 之後新增的數字輸入框自動適用，不需要任何額外寫法。
 */
import { ElMessage } from 'element-plus'

const typed = new WeakMap<HTMLInputElement, string>()

function isNumberInput(target: EventTarget | null): target is HTMLInputElement {
  return target instanceof HTMLInputElement && !!target.closest('.el-input-number')
}

export function installInputNumberGuard(): void {
  document.addEventListener(
    'input',
    (e) => {
      if (isNumberInput(e.target)) typed.set(e.target, e.target.value)
    },
    true,
  )
  document.addEventListener(
    'focusout',
    (e) => {
      const el = e.target
      if (!isNumberInput(el)) return
      const raw = typed.get(el)
      typed.delete(el)
      if (raw === undefined || raw.trim() === '' || raw === '-') return
      // 等元件完成自己的失焦修正
      setTimeout(() => {
        const rawNum = Number(raw)
        const finalNum = Number(el.value)
        if (!Number.isFinite(rawNum) || el.value.trim() === '' || !Number.isFinite(finalNum) || rawNum === finalNum) return
        // el-input-number 的輸入框帶 aria-valuemin／aria-valuemax，依實際原因提示（小數超界不是「格式不符」）
        const max = Number(el.getAttribute('aria-valuemax'))
        const min = Number(el.getAttribute('aria-valuemin'))
        const hasMax = el.hasAttribute('aria-valuemax') && Number.isFinite(max)
        const hasMin = el.hasAttribute('aria-valuemin') && Number.isFinite(min)
        let why: string
        if (hasMax && rawNum > max) why = `超過上限（最大 ${max}）`
        else if (hasMin && rawNum < min) why = `低於下限（最小 ${min}）`
        else why = '超過允許的小數位數，已四捨五入'
        ElMessage.warning({ message: `您輸入的 ${raw} ${why}，已自動調整為 ${finalNum}，請確認是否正確。`, duration: 5000, grouping: true })
      }, 0)
    },
    true,
  )
}
