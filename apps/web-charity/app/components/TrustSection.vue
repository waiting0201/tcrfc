<script setup lang="ts">
// TrustSection.vue — 信任區塊（docs/22-charity-ui.md §2.2：摺疊而非平鋪，預設收合）。
// 立案字號等事實數字來自規劃書 §2.2（台內團字第 1150283692 號、114/10/08 成立、
// 臺中市潭子區勝利三街 95 號），統編與勸募許可字號依規劃書明文「待補」，不得杜撰填上任何數字。
import { useLang } from '../composables/useLang'

const { tr, tt } = useLang()
</script>

<template>
  <details class="disclosure">
    <summary>{{ tt(tr.trust.heading) }}</summary>
    <div class="disclosure-body">
      <dl>
        <dt>{{ tr.trust.registrationNo }}</dt>
        <dd>台內團字第 1150283692 號</dd>

        <dt>{{ tr.trust.establishedOn }}</dt>
        <dd>2025-10-08</dd>

        <dt>{{ tr.trust.address }}</dt>
        <dd>臺中市潭子區勝利三街 95 號</dd>

        <dt>{{ tr.trust.taxId }}</dt>
        <dd>{{ tr.trust.taxIdPending }}</dd>

        <dt>{{ tr.trust.fundraisingPermit }}</dt>
        <dd>{{ tr.trust.fundraisingPermitPending }}</dd>
      </dl>
    </div>
  </details>
</template>
