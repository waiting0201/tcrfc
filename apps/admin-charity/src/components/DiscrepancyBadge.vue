<script setup lang="ts">
/**
 * 對帳差異三種類型（docs/22-charity-ui.md §1.8／§3.8）：符號＋文字雙重標示，
 * 不是三種顏色的色塊——列印成黑白或色弱使用者仍能靠符號＋文字分辨。
 */
import { computed } from 'vue'
import type { DiscrepancyType } from '@/data/reconciliationAudit'

const props = defineProps<{ type: DiscrepancyType }>()

const CONFIG: Record<DiscrepancyType, { symbol: string; label: string; variant: 'warning' | 'info' | 'danger' }> = {
  site_only: { symbol: '▲', label: '本站有金流無', variant: 'warning' },
  gateway_only: { symbol: '▽', label: '金流有本站無', variant: 'info' },
  amount_mismatch: { symbol: '≠', label: '金額不符', variant: 'danger' },
}

const config = computed(() => CONFIG[props.type])
</script>

<template>
  <span class="discrepancy-badge" :class="`discrepancy-badge--${config.variant}`">
    <span class="discrepancy-badge__symbol" aria-hidden="true">{{ config.symbol }}</span>
    {{ config.label }}
  </span>
</template>

<style scoped>
.discrepancy-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 4px;
}

.discrepancy-badge__symbol {
  font-size: 14px;
}

.discrepancy-badge--warning {
  background: var(--charity-warning-bg);
  color: var(--charity-warning-text);
}

.discrepancy-badge--info {
  background: var(--charity-info-bg);
  color: var(--charity-info-text);
}

.discrepancy-badge--danger {
  background: var(--charity-danger-bg);
  color: var(--charity-danger-text);
}
</style>
