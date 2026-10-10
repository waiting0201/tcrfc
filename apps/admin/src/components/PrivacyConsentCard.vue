<script setup lang="ts">
/**
 * 唯讀的「隱私權同意紀錄」：顯示訪客送出報名／詢問當下同意隱私權政策的時間（台灣時間）與當時的政策版本。
 * 由系統在送出時自動記錄，後台無法修改；後台代填或舊資料沒有紀錄。
 */
import { computed } from 'vue'
import { formatDateTime } from '@/utils/dateTime'

const props = defineProps<{
  consentedAt?: string | null
  policyVersion?: string | null
}>()

const timeText = computed(() => (props.consentedAt ? formatDateTime(props.consentedAt) : ''))
const hasRecord = computed(() => !!props.consentedAt || !!props.policyVersion)
</script>

<template>
  <el-card shadow="never" header="隱私權同意紀錄" class="privacy-consent-card">
    <dl class="privacy-consent-card__list">
      <div class="privacy-consent-card__row">
        <dt>同意隱私權政策時間</dt>
        <dd>{{ timeText || '無紀錄（後台代填或舊資料）' }}</dd>
      </div>
      <div class="privacy-consent-card__row">
        <dt>同意的政策版本</dt>
        <dd>{{ policyVersion || '無紀錄（後台代填或舊資料）' }}</dd>
      </div>
    </dl>
    <p class="privacy-consent-card__hint">
      {{ hasRecord ? '這是訪客送出時系統自動記下的，無法修改。' : '訪客自行送出的資料才會有這項紀錄。' }}
    </p>
  </el-card>
</template>

<style scoped>
.privacy-consent-card { margin-top: 16px; }
.privacy-consent-card__list { margin: 0; }
.privacy-consent-card__row { margin-bottom: 10px; }
.privacy-consent-card__row dt { font-size: 13px; color: var(--admin-text-secondary); }
.privacy-consent-card__row dd { margin: 2px 0 0; }
.privacy-consent-card__hint { margin: 0; font-size: 13px; color: var(--admin-text-secondary); }
</style>
