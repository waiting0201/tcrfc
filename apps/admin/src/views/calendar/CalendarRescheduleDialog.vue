<script setup lang="ts">
/**
 * 行事曆改期對話框（L1 進階：拖曳改期與「調整日期」按鈕共用）。
 *
 * - 賽事：只改日期（可順手改開賽時間），可勾「同時標示為延賽」；不勾＝單純更正日期。
 * - 自建活動：整份時間依「天數差」平移；重複活動改的是**整個系列的開始時間**（例外日期不跟著位移）。
 * - 新時段與其他行程衝突時後端回 409、什麼都沒寫入，這裡把衝突清單列出來，使用者按「仍要改期」才會帶確認旗標重送。
 * - 系統目前沒有寄信通路，改期不會自動通知任何人（畫面明講，避免使用者以為已通知）。
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  conflictsFromError,
  moveAdminCustomEvent,
  rescheduleAdminMatch,
  type AdminCalendarConflictDto,
  type AdminCalendarEventDto,
} from '@/api/adminCalendar'
import { formatDateTime } from '@/utils/formatDateTime'

const props = defineProps<{
  modelValue: boolean
  event: AdminCalendarEventDto | null
  /** 拖曳放下的目標日期；沒有（按按鈕開啟）就由使用者自己選。 */
  initialDay?: string | null
  /** 把隊別代碼換成中文名稱（畫面不顯示代碼）。 */
  teamLabel: (code: string) => string
}>()
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'done'): void
}>()

const isMatch = computed(() => props.event?.sourceType === 'match')
const currentDay = computed(() => props.event?.startsAt.slice(0, 10) ?? '')

const newDay = ref('')
const kickoff = ref<string | null>(null)
const kickoffTouched = ref(false)
const markAsPostponed = ref(false)
const submitting = ref(false)
const errorMessage = ref<string | null>(null)
const conflicts = ref<AdminCalendarConflictDto[] | null>(null)

watch(
  () => [props.modelValue, props.event, props.initialDay] as const,
  ([open]) => {
    if (!open || !props.event) return
    newDay.value = props.initialDay ?? currentDay.value
    kickoff.value = props.event.kickoff ?? null
    kickoffTouched.value = false
    markAsPostponed.value = false
    errorMessage.value = null
    conflicts.value = null
  },
)

const unchanged = computed(() => !newDay.value || (newDay.value === currentDay.value && !kickoffTouched.value))

/** 後端的時間戳是 UTC；少數回應沒帶時區字尾，一律當 UTC 解讀。 */
function parseUtc(value: string): Date {
  return new Date(/(Z|[+-]\d\d:?\d\d)$/.test(value) ? value : `${value}Z`)
}

function daysBetween(fromDay: string, toDay: string): number {
  return Math.round((Date.parse(`${toDay}T00:00:00Z`) - Date.parse(`${fromDay}T00:00:00Z`)) / 86400000)
}

function shiftIso(value: string, days: number): string {
  return new Date(parseUtc(value).getTime() + days * 86400000).toISOString()
}

async function submit(acknowledge: boolean) {
  const event = props.event
  if (!event || unchanged.value) return
  submitting.value = true
  errorMessage.value = null
  try {
    const club = activeClubId.value
    if (event.sourceType === 'match') {
      await rescheduleAdminMatch(club, event.sourceId, {
        matchOn: newDay.value,
        kickoff: kickoffTouched.value ? (kickoff.value ?? '') : undefined,
        markAsPostponed: markAsPostponed.value,
        acknowledgeConflicts: acknowledge,
      })
    } else {
      const delta = daysBetween(currentDay.value, newDay.value)
      await moveAdminCustomEvent(club, event.sourceId, {
        startsAt: shiftIso(event.startsAt, delta),
        endsAt: event.endsAt ? shiftIso(event.endsAt, delta) : null,
        isAllDay: event.isAllDay,
        acknowledgeConflicts: acknowledge,
      })
    }
    ElMessage.success('已調整日期')
    emit('update:modelValue', false)
    emit('done')
  } catch (error) {
    const found = conflictsFromError(error)
    if (found) {
      conflicts.value = found
    } else {
      conflicts.value = null
      errorMessage.value = error instanceof AdminApiError ? error.message : '調整失敗，請稍後再試'
    }
  } finally {
    submitting.value = false
  }
}

function conflictTeams(item: AdminCalendarConflictDto): string {
  return item.sharedTeamCodes.map((c) => props.teamLabel(c)).join('、')
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="isMatch ? '調整賽事日期' : '調整活動日期'"
    width="520px"
    :close-on-click-modal="false"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <template v-if="event">
      <p class="reschedule__title">{{ event.title }}</p>
      <p class="reschedule__meta">目前：{{ formatDateTime(event.startsAt) }}<span v-if="event.isAllDay">（全天）</span></p>

      <el-form label-position="top">
        <el-form-item label="改到哪一天" required>
          <el-date-picker v-model="newDay" type="date" value-format="YYYY-MM-DD" :clearable="false" style="width: 100%" />
        </el-form-item>
        <el-form-item v-if="isMatch" label="開賽時間（不改就不用動）">
          <el-time-picker
            v-model="kickoff"
            format="HH:mm"
            value-format="HH:mm"
            placeholder="沒有固定開賽時間"
            style="width: 100%"
            @change="kickoffTouched = true"
          />
        </el-form-item>
        <el-form-item v-if="isMatch">
          <el-checkbox v-model="markAsPostponed">同時標示為延賽（並記下原定日期與時間）</el-checkbox>
          <p class="reschedule__hint">沒勾選只會更正日期，賽事狀態不變；改期已開賽、已結束或已取消的賽事不被允許。</p>
        </el-form-item>
        <p v-else class="reschedule__hint">如果這是重複活動，改的是整個系列的開始時間；已排除的例外日期不會跟著移動。</p>
      </el-form>

      <p class="reschedule__hint">系統目前不會自動通知球員或家長，改期後請自行聯繫相關人員。</p>

      <el-alert v-if="errorMessage" :title="errorMessage" type="error" show-icon :closable="false" class="reschedule__alert" />

      <el-alert
        v-if="conflicts"
        title="新的時段與其他行程衝突，目前還沒有修改任何資料"
        type="warning"
        show-icon
        :closable="false"
        class="reschedule__alert"
      >
        <ul class="reschedule__conflicts">
          <li v-for="(c, i) in conflicts" :key="i">
            {{ c.description }}
            <span v-if="c.venueName">（場地：{{ c.venueName }}）</span>
            <span v-if="c.sharedTeamCodes.length">（球隊：{{ conflictTeams(c) }}）</span>
          </li>
        </ul>
        <p class="reschedule__hint">確認沒問題可以按「仍要改期」。</p>
      </el-alert>
    </template>
    <template #footer>
      <el-button :disabled="submitting" @click="emit('update:modelValue', false)">取消</el-button>
      <el-button v-if="conflicts" type="warning" :loading="submitting" @click="submit(true)">仍要改期</el-button>
      <el-button v-else type="primary" :loading="submitting" :disabled="unchanged" @click="submit(false)">確認改期</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.reschedule__title {
  margin: 0 0 4px;
  font-weight: 600;
  word-break: break-word;
}

.reschedule__meta {
  margin: 0 0 12px;
  font-size: 13px;
  color: var(--admin-text-secondary);
}

.reschedule__hint {
  margin: 4px 0 8px;
  font-size: 12px;
  line-height: 1.6;
  color: var(--admin-text-tertiary);
}

.reschedule__alert {
  margin-top: 12px;
}

.reschedule__conflicts {
  margin: 6px 0;
  padding-left: 18px;
  font-size: 13px;
  line-height: 1.7;
}
</style>
