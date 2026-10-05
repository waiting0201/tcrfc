<script setup lang="ts">
/** 連線檢查：資料庫、推播傳輸、靜態設定檔同步、憑證屆期、最低支援版本、未處理崩潰。「尚未串接」與「異常」分開顯示。 */
import { onMounted, ref } from 'vue'
import { AdminApiError } from '@/api/http'
import { getConnectionCheck, type ConnectionCheckDto } from '@/api/adminApp'
import { formatDateTime } from '@/utils/dateTime'

const data = ref<ConnectionCheckDto | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
async function load() {
  loading.value = true
  error.value = null
  try {
    data.value = await getConnectionCheck()
  } catch (e) {
    data.value = null
    error.value = e instanceof AdminApiError ? e.message : '連線檢查失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
const TAG: Record<string, 'success' | 'warning' | 'info' | 'danger'> = { ok: 'success', warning: 'warning', not_configured: 'info', error: 'danger' }
/** 檢查項目的白話名稱與說明（以項目 key 對應；後端文字含服務原名，畫面改用日常說法）。 */
const ITEM_TEXT: Record<string, { label: string; ok?: string; notConfigured?: string }> = {
  push_transport: {
    label: '手機推播服務（Apple 與 Google）',
    ok: '已設定推播服務。',
    notConfigured: '尚未串接：推播所需的 Apple 與 Google 金鑰尚未建立，推播批次會停在「失敗」並保留，串接後可重送。',
  },
  edge_config: {
    label: '備援設定來源',
    ok: '已串接備援設定檔，存檔時會同步。',
    notConfigured: '尚未串接備援設定檔；主伺服器掛掉時，App 讀不到維護與強制更新公告。',
  },
}
const itemLabel = (i: { key: string; label: string }) => ITEM_TEXT[i.key]?.label ?? i.label
const itemMessage = (i: { key: string; status: string; message: string }) =>
  (i.status === 'ok' ? ITEM_TEXT[i.key]?.ok : i.status === 'not_configured' ? ITEM_TEXT[i.key]?.notConfigured : undefined) ?? i.message
const STATUS_TEXT: Record<string, string> = { ok: '正常', warning: '注意', not_configured: '尚未串接', error: '異常' }
</script>

<template>
  <div>
    <el-card shadow="never" class="ct__block">
      <div class="ct__row">
        <span class="ct__muted">「尚未串接」代表這個服務還沒建置完成，不是故障；「異常」才需要立刻處理。檢查不會真的送出任何推播。</span>
        <span class="ct__spacer" />
        <el-button :loading="loading" type="primary" @click="load">重新檢查</el-button>
      </div>
    </el-card>
    <el-card v-if="loading && !data" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="error" shadow="never"><el-empty :description="error"><el-button type="primary" @click="load">重新檢查</el-button></el-empty></el-card>
    <el-card v-else-if="data" shadow="never">
      <p class="ct__muted">檢查時間：{{ formatDateTime(data.checkedAt) }}（台灣時間）</p>
      <ul class="ct__list">
        <li v-for="i in data.items" :key="i.key">
          <el-tag :type="TAG[i.status]" size="small" class="ct__tag">{{ STATUS_TEXT[i.status] ?? i.statusLabel }}</el-tag>
          <div><strong>{{ itemLabel(i) }}</strong><div class="ct__muted">{{ itemMessage(i) }}</div></div>
        </li>
      </ul>
    </el-card>
  </div>
</template>

<style scoped>
.ct__block { margin-bottom: 12px; }
.ct__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.ct__spacer { flex: 1; }
.ct__muted { font-size: 12px; line-height: 1.7; color: var(--admin-text-tertiary); }
.ct__list { list-style: none; margin: 8px 0 0; padding: 0; display: flex; flex-direction: column; gap: 14px; }
.ct__list li { display: flex; gap: 12px; align-items: flex-start; }
.ct__tag { flex: none; min-width: 64px; justify-content: center; }
</style>
