import { onMounted, onUnmounted, ref } from 'vue'

export type Breakpoint = 'mobile' | 'tablet' | 'desktop'

/**
 * 斷點判斷（docs/21-admin-ui.md §8）：
 * - mobile   < 768px  → 側欄變 el-drawer
 * - tablet   768–1024px → 側欄強制收合（可展開）
 * - desktop  ≥ 1024px → 側欄常駐，可收合
 */
function classify(width: number): Breakpoint {
  if (width < 768) return 'mobile'
  if (width < 1024) return 'tablet'
  return 'desktop'
}

export function useBreakpoint() {
  const breakpoint = ref<Breakpoint>(classify(typeof window !== 'undefined' ? window.innerWidth : 1280))

  function handleResize() {
    breakpoint.value = classify(window.innerWidth)
  }

  onMounted(() => window.addEventListener('resize', handleResize))
  onUnmounted(() => window.removeEventListener('resize', handleResize))

  return { breakpoint }
}
