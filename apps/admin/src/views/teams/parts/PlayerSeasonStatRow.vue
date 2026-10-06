<script setup lang="ts">
/**
 * 球員賽季數據的單一賽季列（`PlayerSeasonStatsPanel` 的子元件）。
 *
 * 每一列自己提供一份欄位錯誤（`provideFormErrors`），因為五個欄位鍵（出賽、進球……）在每個賽季都一樣，
 * 共用頁面那一份會互相標錯列。儲存／清除都是立即送出，不跟球員頁的「儲存」連動。
 */
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import {
  clearAdminPlayerSeasonStat,
  setAdminPlayerSeasonStat,
  type AdminPlayerSeasonStatDto,
  type PlayerSeasonStatValues,
} from '@/api/adminPlayers'
import { AdminApiError } from '@/api/http'

const props = defineProps<{
  club: string
  playerId: string
  stat: AdminPlayerSeasonStatDto
  canEdit: boolean
}>()
const emit = defineEmits<{ changed: [] }>()

const SOURCE_LABEL: Record<string, string> = { manual: '手動', auto: '自動彙總', none: '無資料' }
const SOURCE_TAG: Record<string, 'success' | 'info' | 'warning'> = { manual: 'warning', auto: 'success', none: 'info' }

const FIELDS = [
  { key: 'appearances', label: '出賽' },
  { key: 'goals', label: '進球' },
  { key: 'assists', label: '助攻' },
  { key: 'yellowCards', label: '黃牌' },
  { key: 'redCards', label: '紅牌' },
] as const
type FieldKey = (typeof FIELDS)[number]['key']

const formErrors = provideFormErrors()
const form = reactive<Record<FieldKey, number>>({ appearances: 0, goals: 0, assists: 0, yellowCards: 0, redCards: 0 })
const baseline = ref('')
const saving = ref(false)
const clearing = ref(false)
const rowError = ref<string | null>(null)

/** 編輯起點：有手動值用手動值，否則用自動值（助攻沒有自動來源，從 0 起），都沒有就是 0。 */
function reset() {
  const src: PlayerSeasonStatValues | null | undefined = props.stat.manual ?? props.stat.auto
  form.appearances = src?.appearances ?? 0
  form.goals = src?.goals ?? 0
  form.assists = src?.assists ?? 0
  form.yellowCards = src?.yellowCards ?? 0
  form.redCards = src?.redCards ?? 0
  baseline.value = JSON.stringify(form)
  formErrors.clearAll()
  rowError.value = null
}
watch(() => props.stat, reset, { immediate: true })

const dirty = computed(() => JSON.stringify(form) !== baseline.value)
const valuesText = (v: PlayerSeasonStatValues | null | undefined) =>
  v
    ? `出賽 ${v.appearances}、進球 ${v.goals}、助攻 ${v.assists ?? '—'}、黃牌 ${v.yellowCards}、紅牌 ${v.redCards}`
    : '沒有資料'

function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  for (const f of FIELDS) {
    const v = form[f.key]
    if (!Number.isInteger(v) || v < 0 || v > 9999) errors[f.key] = `${f.label}請輸入 0 到 9999 的整數`
  }
  return errors
}

async function save() {
  rowError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    await setAdminPlayerSeasonStat(props.club, props.playerId, props.stat.seasonId, { ...form })
    ElMessage.success(`已將 ${props.stat.seasonCode} 的數據儲存為手動輸入`)
    emit('changed')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    rowError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function clearManual() {
  try {
    await ElMessageBox.confirm(
      `清除後，${props.stat.seasonCode} 的數據會改回由已結束的賽事自動彙總（目前自動值：${valuesText(props.stat.auto)}）。確定要清除手動輸入的數據嗎？`,
      '清除手動值',
      { confirmButtonText: '清除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  rowError.value = null
  clearing.value = true
  try {
    await clearAdminPlayerSeasonStat(props.club, props.playerId, props.stat.seasonId)
    ElMessage.success('已改回自動彙總')
    emit('changed')
  } catch (error) {
    rowError.value = error instanceof AdminApiError ? error.message : '清除失敗，請稍後再試'
  } finally {
    clearing.value = false
  }
}
</script>

<template>
  <div class="stat-row">
    <div class="stat-row__head">
      <strong>{{ stat.seasonCode }}</strong>
      <span class="stat-row__period">{{ stat.startOn }} ～ {{ stat.endOn }}</span>
      <el-tag :type="SOURCE_TAG[stat.source] ?? 'info'" size="small">{{ SOURCE_LABEL[stat.source] ?? stat.source }}</el-tag>
    </div>

    <p class="stat-row__auto">自動彙總：{{ valuesText(stat.auto) }}</p>
    <p v-if="stat.source === 'manual' && stat.manual" class="stat-row__auto">目前手動值：{{ valuesText(stat.manual) }}（前台以手動值為準）</p>

    <el-alert v-if="rowError" :title="rowError" type="warning" show-icon class="stat-row__error" @close="rowError = null" />

    <div class="stat-row__fields">
      <FormField v-for="f in FIELDS" :key="f.key" :field="f.key" :label="f.label">
        <el-input-number
          v-model="form[f.key]"
          :min="0"
          :max="9999"
          :disabled="!canEdit"
          controls-position="right"
          class="stat-row__num"
          @change="formErrors.clear(f.key)"
        />
      </FormField>
    </div>

    <div v-if="canEdit" class="stat-row__actions">
      <el-button type="primary" size="small" :loading="saving" :disabled="stat.source === 'manual' && !dirty" @click="save">
        儲存為手動值
      </el-button>
      <el-button v-if="stat.source === 'manual'" size="small" :loading="clearing" @click="clearManual">
        清除手動值，改回自動彙總
      </el-button>
    </div>
  </div>
</template>

<style scoped>
.stat-row {
  padding: 12px 0;
  border-top: 1px solid var(--admin-border);
}

.stat-row:first-child {
  border-top: 0;
  padding-top: 0;
}

.stat-row__head {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 4px;
}

.stat-row__period,
.stat-row__auto {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.stat-row__auto {
  margin: 2px 0;
}

.stat-row__error {
  margin: 8px 0;
}

.stat-row__fields {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 0 12px;
  margin-top: 8px;
}

.stat-row__num {
  width: 100%;
}

.stat-row__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
</style>
