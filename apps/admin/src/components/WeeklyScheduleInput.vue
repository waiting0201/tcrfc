<script setup lang="ts">
/**
 * 每週上課時間輸入：星期＋起訖時間，可新增多列（同一天可有多個時段）。
 * 對外仍是 `sessions.weekly_schedule` 的 JSON 文字（`{"mon":"18:00-19:30","wed":["09:00-10:00","16:00-17:00"]}`），
 * 前台 `formatWeeklySchedule` 讀的就是這個形狀；使用者不需要知道 JSON。
 *
 * 舊資料若不是這個形狀（區間鍵、自由文字等），不硬轉換——顯示「舊格式」提示並原樣保留，
 * 要改請按「重新填寫」。
 */
import { computed, ref, watch } from 'vue'

const model = defineModel<string>({ default: '' })
defineProps<{ disabled?: boolean }>()

const DAYS = [
  { key: 'mon', label: '週一' },
  { key: 'tue', label: '週二' },
  { key: 'wed', label: '週三' },
  { key: 'thu', label: '週四' },
  { key: 'fri', label: '週五' },
  { key: 'sat', label: '週六' },
  { key: 'sun', label: '週日' },
] as const

interface Row { day: string; range: [string, string] | null }

const TIME_RANGE = /^(\d{2}):(\d{2})-(\d{2}):(\d{2})$/

/** 解析既有 JSON；回傳 null 代表不是可結構化編輯的形狀。 */
function parse(raw: string): Row[] | null {
  if (!raw.trim()) return []
  let obj: unknown
  try { obj = JSON.parse(raw) } catch { return null }
  if (obj === null) return []
  if (typeof obj !== 'object' || Array.isArray(obj)) return null
  const rows: Row[] = []
  for (const [k, v] of Object.entries(obj as Record<string, unknown>)) {
    if (!DAYS.some((d) => d.key === k)) return null
    for (const slot of Array.isArray(v) ? v : [v]) {
      const m = typeof slot === 'string' ? TIME_RANGE.exec(slot.trim()) : null
      if (!m) return null
      rows.push({ day: k, range: [`${m[1]}:${m[2]}`, `${m[3]}:${m[4]}`] })
    }
  }
  return rows
}

function serialize(rows: Row[]): string {
  const out: Record<string, string | string[]> = {}
  for (const r of rows) {
    if (!r.day || !r.range) continue
    const slot = `${r.range[0]}-${r.range[1]}`
    const cur = out[r.day]
    out[r.day] = cur === undefined ? slot : Array.isArray(cur) ? [...cur, slot] : [cur, slot]
  }
  return Object.keys(out).length ? JSON.stringify(out) : ''
}

const initial = parse(model.value)
const legacy = ref(initial === null)
const rows = ref<Row[]>(initial ?? [])

// 外部（例如載入詳情後）改了值才重新解析；自己 serialize 出去的值不回灌，避免游標與列順序被打亂
let lastEmitted = model.value
watch(model, (v) => {
  if (v === lastEmitted) return
  lastEmitted = v
  const p = parse(v)
  legacy.value = p === null
  rows.value = p ?? []
})
watch(rows, (r) => {
  if (legacy.value) return
  lastEmitted = serialize(r)
  model.value = lastEmitted
}, { deep: true })

function addRow() { rows.value.push({ day: '', range: null }) }
function removeRow(i: number) { rows.value.splice(i, 1) }
function reset() { legacy.value = false; rows.value = []; lastEmitted = ''; model.value = '' }

const legacyText = computed(() => model.value)

/** 供外層送出前檢查：每一列都要選星期與起訖時間，且結束晚於開始。 */
function validate(): string | null {
  if (legacy.value) return null
  for (const [i, r] of rows.value.entries()) {
    if (!r.day || !r.range) return `上課時間表第 ${i + 1} 列請選擇星期與起訖時間，或移除這一列`
    if (r.range[1] <= r.range[0]) return `上課時間表第 ${i + 1} 列的結束時間要晚於開始時間`
  }
  return null
}
defineExpose({ validate })
</script>

<template>
  <div class="weekly">
    <el-alert
      v-if="legacy"
      type="info"
      show-icon
      :closable="false"
      title="這個梯次的上課時間是舊格式，系統會原樣保留；若要改成星期與時間的填法，請按「重新填寫」。"
    >
      <p class="weekly__legacy">{{ legacyText }}</p>
      <el-button size="small" :disabled="disabled" @click="reset">重新填寫</el-button>
    </el-alert>
    <template v-else>
      <div v-for="(r, i) in rows" :key="i" class="weekly__row">
        <el-select v-model="r.day" placeholder="星期" :disabled="disabled" class="weekly__day">
          <el-option v-for="d in DAYS" :key="d.key" :label="d.label" :value="d.key" />
        </el-select>
        <el-time-picker
          v-model="r.range"
          is-range
          format="HH:mm"
          value-format="HH:mm"
          range-separator="至"
          start-placeholder="開始時間"
          end-placeholder="結束時間"
          :disabled="disabled"
          class="weekly__time"
        />
        <el-button v-if="!disabled" text type="danger" @click="removeRow(i)">移除</el-button>
      </div>
      <p v-if="rows.length === 0" class="weekly__empty">尚未設定每週上課時間（選填）。</p>
      <el-button v-if="!disabled" size="small" @click="addRow">＋ 新增上課時段</el-button>
      <p class="weekly__hint">例如「週一 18:00 至 19:30」；同一天有兩個時段就新增兩列。前台會顯示成「週一 18:00–19:30」。</p>
    </template>
  </div>
</template>

<style scoped>
.weekly { width: 100%; min-width: 0; }
.weekly__row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 8px; }
.weekly__day { width: 110px; }
.weekly__time { flex: 1; min-width: 220px; max-width: 320px; }
.weekly__empty, .weekly__hint { margin: 4px 0 8px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.weekly__legacy { margin: 6px 0; font-family: ui-monospace, monospace; font-size: 12px; word-break: break-all; }
</style>
