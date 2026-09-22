<script setup lang="ts">
/** 捐款單六態（docs/22-charity-ui.md §1.8：一律帶文字標籤，色相只是輔助辨識） */
import { computed } from 'vue'
import SemanticTag from './SemanticTag.vue'
import type { DonationStatus } from '@/types/fixtures'

const props = defineProps<{ status: DonationStatus }>()

const LABEL: Record<DonationStatus, string> = {
  created: '已建立',
  pending: '處理中',
  paid: '已完成',
  failed: '付款失敗',
  expired: '已逾時',
  refunded: '已退款',
}

const VARIANT: Record<DonationStatus, 'success' | 'danger' | 'warning' | 'info' | 'refund' | 'neutral'> = {
  created: 'neutral',
  pending: 'info',
  paid: 'success',
  failed: 'danger',
  expired: 'warning',
  refunded: 'refund',
}

const label = computed(() => LABEL[props.status])
const variant = computed(() => VARIANT[props.status])
</script>

<template>
  <SemanticTag :variant="variant">{{ label }}</SemanticTag>
</template>
