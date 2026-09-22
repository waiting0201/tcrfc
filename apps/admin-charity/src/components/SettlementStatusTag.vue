<script setup lang="ts">
/**
 * 結算單三態（docs/22-charity-ui.md §3.7.4）：獨立於捐款單六態的另一組狀態機，
 * 刻意不共用同一組 tag 色票判斷邏輯，避免使用者把「捐款已完成」跟「回饋金已付款」搞混。
 */
import { computed } from 'vue'
import SemanticTag from './SemanticTag.vue'
import type { SettlementStatus } from '@/types/fixtures'

const props = defineProps<{ status: SettlementStatus }>()

const LABEL: Record<SettlementStatus, string> = {
  pending: '待結算',
  settled: '已結算',
  paid: '已付款',
}

const VARIANT: Record<SettlementStatus, 'success' | 'info' | 'neutral'> = {
  pending: 'neutral',
  settled: 'info',
  paid: 'success',
}

const label = computed(() => LABEL[props.status])
const variant = computed(() => VARIANT[props.status])
</script>

<template>
  <SemanticTag :variant="variant">{{ label }}</SemanticTag>
</template>
