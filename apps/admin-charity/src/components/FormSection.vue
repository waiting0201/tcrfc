<script setup lang="ts">
/**
 * 編輯頁卡片內的分段小標（docs/21 §3.3a，慈善後台沿用）。
 *
 * 編輯頁每欄只放一至兩張 `el-card`，卡片內的欄位群組用這個元件分段，不再各開一張卡片。
 * 相鄰的分段之間自動畫一條分隔線；`title` 省略時只留分隔、不顯示小標（卡片第一段常用）。
 */
defineProps<{
  /** 分段小標（畫面文字）。省略則不顯示小標。 */
  title?: string
  /** 小標下方的一行說明。 */
  hint?: string
}>()
</script>

<template>
  <section class="form-section">
    <h3 v-if="title" class="form-section__title">{{ title }}</h3>
    <p v-if="hint" class="form-section__hint">{{ hint }}</p>
    <slot />
  </section>
</template>

<style scoped>
.form-section + .form-section {
  margin-top: var(--charity-admin-space-6);
  padding-top: var(--charity-admin-space-6);
  border-top: 1px solid var(--charity-admin-border);
}

.form-section__title {
  margin: 0 0 var(--charity-admin-space-3);
  font-size: 14px;
  font-weight: 600;
  color: var(--charity-admin-text-primary);
}

.form-section__hint {
  margin: calc(var(--charity-admin-space-3) * -1 + var(--charity-admin-space-1)) 0 var(--charity-admin-space-3);
  font-size: 12px;
  line-height: 1.6;
  color: var(--charity-admin-text-tertiary);
}
</style>
