<script setup lang="ts" generic="T">
/**
 * 手機寬度的卡片式列表外殼（docs/22-charity-ui.md §3.1 沿用 docs/21 §8：手機不把 el-table
 * 硬擠進小螢幕橫向捲動，改成每筆一張卡片）。只提供卡片容器與間距，卡片內容由呼叫端的
 * default slot 決定——比照 apps/admin `NewsListView.vue` 的既有做法抽出來的共用外殼，
 * 減少每個列表頁各自重寫一次卡片版面的樣板碼。泛型 `T` 讓呼叫端的 slot 拿到正確型別的 `item`。
 */
defineProps<{
  items: T[]
}>()
</script>

<template>
  <div class="mobile-card-list">
    <el-card v-for="(item, index) in items" :key="index" shadow="never" class="mobile-card-list__card">
      <slot :item="item" :index="index" />
    </el-card>
  </div>
</template>

<style scoped>
.mobile-card-list {
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-3);
}

.mobile-card-list__card {
  --el-card-padding: 12px;
}
</style>
