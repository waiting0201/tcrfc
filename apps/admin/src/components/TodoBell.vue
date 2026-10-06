<script setup lang="ts">
/**
 * 頂欄「待辦提醒」鈴鐺（docs/21-admin-ui.md §1.2）。
 *
 * 這不是通知中心：資料與儀表板「待辦提醒」同一份（`getDashboard(club).todos`），只是放在每一頁都看得到的
 * 捷徑；沒有新的通知類型、沒有新端點。前往位置與儀表板共用 `@/utils/todoRoutes`。
 *
 * - 徽章＝各類筆數總和；0 不顯示數字；超過 99 顯示 99+。
 * - 依目前站台取資料；切換站台立即重抓，換頁最多每 15 秒重抓一次，另每 5 分鐘背景重抓（頁面不可見時暫停）。
 * - 403（帳號沒有儀表板任何權限）→ 整個鈴鐺隱藏；其他失敗 → 不顯示徽章，點開只顯示簡短說明，不跳錯誤彈窗。
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { getDashboard, type DashboardTodo } from '@/api/adminDashboard'
import { formatBadge, sumTodoCounts, todoRoute } from '@/utils/todoRoutes'

const router = useRouter()
const route = useRoute()

const REFRESH_MS = 5 * 60 * 1000
const ROUTE_THROTTLE_MS = 15 * 1000

const todos = ref<DashboardTodo[]>([])
const state = ref<'loading' | 'ready' | 'forbidden' | 'error'>('loading')
const open = ref(false)

let seq = 0
let lastLoadedAt = 0
let timer: ReturnType<typeof setInterval> | null = null

async function load() {
  const mySeq = ++seq
  const requestedClub = activeClubId.value
  lastLoadedAt = Date.now()
  try {
    const data = await getDashboard(requestedClub)
    if (mySeq !== seq) return
    todos.value = data.todos ?? []
    state.value = 'ready'
  } catch (error) {
    if (mySeq !== seq) return
    todos.value = []
    state.value = error instanceof AdminApiError && error.kind === 'forbidden' ? 'forbidden' : 'error'
  }
}

function startTimer() {
  stopTimer()
  timer = setInterval(() => {
    if (document.visibilityState === 'visible') void load()
  }, REFRESH_MS)
}

function stopTimer() {
  if (timer) clearInterval(timer)
  timer = null
}

function onVisibility() {
  // 回到可見時，若已超過一個週期沒更新就補抓一次。
  if (document.visibilityState === 'visible' && Date.now() - lastLoadedAt >= REFRESH_MS) void load()
}

onMounted(() => {
  void load()
  startTimer()
  document.addEventListener('visibilitychange', onVisibility)
})

onBeforeUnmount(() => {
  stopTimer()
  document.removeEventListener('visibilitychange', onVisibility)
})

watch(activeClubId, () => void load())
watch(
  () => route.path,
  () => {
    if (Date.now() - lastLoadedAt >= ROUTE_THROTTLE_MS) void load()
  },
)

const total = computed(() => sumTodoCounts(todos.value))
const badgeText = computed(() => (state.value === 'ready' ? formatBadge(total.value) : ''))
const ariaLabel = computed(() =>
  state.value === 'ready' && total.value > 0 ? `待辦提醒，共 ${total.value} 筆` : '待辦提醒',
)

function go(path: string | null) {
  open.value = false
  if (path) void router.push(path)
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') open.value = false
}
</script>

<template>
  <el-popover
    v-if="state !== 'forbidden'"
    v-model:visible="open"
    trigger="click"
    placement="bottom-end"
    :width="300"
    popper-class="todo-bell__popper"
    :show-arrow="false"
  >
    <template #reference>
      <button
        class="todo-bell"
        type="button"
        :aria-label="ariaLabel"
        aria-haspopup="true"
        :aria-expanded="open"
        @keydown="onKeydown"
      >
        <el-icon :size="20"><Bell /></el-icon>
        <span v-if="badgeText" class="todo-bell__badge" aria-hidden="true">{{ badgeText }}</span>
      </button>
    </template>

    <div class="todo-bell__panel" role="region" aria-label="待辦提醒" @keydown="onKeydown">
      <p class="todo-bell__title">待辦提醒</p>
      <p v-if="state === 'loading'" class="todo-bell__note">載入中…</p>
      <p v-else-if="state === 'error'" class="todo-bell__note">無法取得待辦，請稍後再試</p>
      <p v-else-if="todos.length === 0" class="todo-bell__note">目前沒有可顯示的待辦項目</p>
      <ul v-else class="todo-bell__list">
        <li v-for="todo in todos" :key="todo.code">
          <button
            type="button"
            class="todo-bell__row"
            :class="{ 'todo-bell__row--zero': todo.count === 0 }"
            @click="go(todoRoute(todo))"
          >
            <span class="todo-bell__label">{{ todo.label }}</span>
            <span class="todo-bell__count">{{ todo.count }} 筆</span>
          </button>
        </li>
      </ul>
      <button type="button" class="todo-bell__footer" @click="go('/dashboard')">前往儀表板</button>
    </div>
  </el-popover>
</template>

<style scoped>
.todo-bell {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 32px;
  min-height: 32px;
  margin-right: var(--admin-space-1);
  padding: var(--admin-space-1);
  border: none;
  background: none;
  cursor: pointer;
  color: var(--admin-text-secondary);
}

.todo-bell:hover {
  color: var(--admin-text-primary);
}

.todo-bell__badge {
  position: absolute;
  top: 0;
  right: -2px;
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  border-radius: 8px;
  background: var(--el-color-danger, #f56c6c);
  color: #fff;
  font-size: 11px;
  line-height: 16px;
  text-align: center;
  box-sizing: border-box;
}

.todo-bell__panel {
  display: flex;
  flex-direction: column;
  gap: var(--admin-space-2);
}

.todo-bell__title {
  margin: 0;
  font-weight: 600;
  color: var(--admin-text-primary);
}

.todo-bell__note {
  margin: 0;
  font-size: 13px;
  color: var(--admin-text-secondary);
}

.todo-bell__list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.todo-bell__row,
.todo-bell__footer {
  width: 100%;
  min-height: 40px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--admin-space-2);
  padding: 0 var(--admin-space-2);
  border: none;
  border-radius: 4px;
  background: none;
  cursor: pointer;
  font: inherit;
  color: var(--admin-text-primary);
  text-align: left;
}

.todo-bell__row:hover,
.todo-bell__footer:hover {
  background: var(--admin-bg-surface-2);
}

.todo-bell__row--zero {
  color: var(--admin-text-secondary);
}

.todo-bell__count {
  flex-shrink: 0;
  font-variant-numeric: tabular-nums;
}

.todo-bell__footer {
  justify-content: center;
  border-top: 1px solid var(--admin-border);
  border-radius: 0;
  color: var(--el-color-primary);
}
</style>
