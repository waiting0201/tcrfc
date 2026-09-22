<script setup lang="ts">
/**
 * 發票／收據狀態（docs/22-charity-ui.md §1.8、§3.7.5）：開立狀態與作廢／折讓狀態分開判斷，
 * 但畫面上合併成一個 tag——作廢／折讓一旦發生，比「已開立」更值得使用者先看到。
 */
import { computed } from 'vue'
import SemanticTag from './SemanticTag.vue'
import type { InvoiceIssueStatus, InvoiceVoidStatus } from '@/types/fixtures'

const props = defineProps<{
  issueStatus: InvoiceIssueStatus
  voidStatus: InvoiceVoidStatus
}>()

const label = computed(() => {
  if (props.voidStatus === 'voided') return '已作廢'
  if (props.voidStatus === 'allowance') return '已折讓'
  if (props.issueStatus === 'issued') return '已開立'
  if (props.issueStatus === 'failed') return '開立失敗'
  return '待開立'
})

const variant = computed<'success' | 'danger' | 'warning' | 'info' | 'refund' | 'neutral'>(() => {
  if (props.voidStatus === 'voided') return 'danger'
  if (props.voidStatus === 'allowance') return 'refund'
  if (props.issueStatus === 'issued') return 'success'
  if (props.issueStatus === 'failed') return 'warning'
  return 'neutral'
})
</script>

<template>
  <SemanticTag :variant="variant">{{ label }}</SemanticTag>
</template>
