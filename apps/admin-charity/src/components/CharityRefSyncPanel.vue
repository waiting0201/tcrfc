<script setup lang="ts">
/**
 * N2「從官網更新清單」：撥付對象（公益團體）與公益計畫的選單資料來自官網，這裡讓管理員手動更新。
 * 只有具項目管理授權者看得到。按下後進行中不可重複點；成功以日常中文摘要結果（公益團體與公益計畫分開講）。
 * 409（更新中）／502（官網暫時取不到）／503（服務未設定）直接顯示後端的中文訊息；
 * 502 另外明說「現有清單沒有被改動」（後端整筆回復，複本一個欄位都不動）。
 * 本元件只負責觸發與顯示結果；清單資料由上層重新載入（收到 `synced` 事件）。
 */
import { computed, ref } from 'vue'
import { syncCharityRefs, type CharityRefSyncCounts, type CharityRefSyncResult } from '@/api/projects'
import { AdminApiError } from '@/api/http'
import { hasPermission } from '@/auth/session'
import { formatTaipei } from '@/utils/format'

const props = defineProps<{
  /** 上次更新時間（UTC ISO）；null＝從未更新過；undefined＝尚未取得（不顯示時間行）。 */
  lastSyncedAt: string | null | undefined
}>()
const emit = defineEmits<{ synced: [result: CharityRefSyncResult] }>()

const canSync = computed(() => hasPermission('n2.donation_project.manage'))
const syncing = ref(false)
const result = ref<CharityRefSyncResult | null>(null)
const errorText = ref('')

const lastSyncedText = computed(() => {
  if (props.lastSyncedAt === undefined) return ''
  if (props.lastSyncedAt === null) return '尚未從官網更新過清單'
  return `清單上次更新：${formatTaipei(props.lastSyncedAt)}（台灣時間）`
})

function describe(label: string, c: CharityRefSyncCounts): string {
  const parts: string[] = []
  if (c.created) parts.push(`新增 ${c.created} 筆`)
  if (c.updated) parts.push(`更新 ${c.updated} 筆`)
  if (c.deactivated) parts.push(`停用 ${c.deactivated} 筆（官網已不再公開）`)
  if (c.reactivated) parts.push(`恢復 ${c.reactivated} 筆`)
  const changes = parts.length > 0 ? parts.join('、') : '沒有變動'
  return `${label}：${changes}，目前共 ${c.activeTotal} 筆可選用`
}

const summaryLines = computed(() => (result.value ? [describe('公益團體', result.value.charities), describe('公益計畫', result.value.programs)] : []))

async function run() {
  if (syncing.value) return
  syncing.value = true
  errorText.value = ''
  result.value = null
  try {
    const r = await syncCharityRefs()
    result.value = r
    emit('synced', r)
  } catch (error) {
    if (error instanceof AdminApiError) {
      // 502：後端整筆回復，把「沒有被改動」說清楚，避免管理員以為清單被清空。
      errorText.value = error.status === 502 && !error.detail.includes('沒有') ? `${error.detail}現有清單沒有被改動，稍後再試一次即可。` : error.detail
    } else {
      errorText.value = '更新清單時發生問題，現有清單沒有被改動，請稍後再試一次。'
    }
  } finally {
    syncing.value = false
  }
}
</script>

<template>
  <div v-if="canSync" class="ref-sync">
    <div class="ref-sync__row">
      <el-button :loading="syncing" :disabled="syncing" @click="run">{{ syncing ? '更新中…' : '從官網更新清單' }}</el-button>
      <span v-if="lastSyncedText" class="ref-sync__time">{{ lastSyncedText }}</span>
    </div>
    <el-alert v-if="result" type="success" show-icon :closable="true" title="已從官網更新清單" class="ref-sync__alert" @close="result = null">
      <p v-for="line in summaryLines" :key="line" class="ref-sync__line">{{ line }}</p>
    </el-alert>
    <el-alert v-if="errorText" type="warning" show-icon :closable="true" :title="errorText" class="ref-sync__alert" @close="errorText = ''" />
  </div>
</template>

<style scoped>
.ref-sync__row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--charity-admin-space-2) var(--charity-admin-space-3);
}

.ref-sync__time {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.ref-sync__alert {
  margin-top: var(--charity-admin-space-2);
}

.ref-sync__line {
  margin: 0;
  line-height: 1.6;
}
</style>
