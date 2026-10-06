<script setup lang="ts">
/**
 * 編輯頁的兩欄版面（docs/21 §3）。
 *
 * - `#main`：主欄（文字欄位、雙語卡片）；`#aside`：右側欄（檔案上傳卡片）。沒有 `#aside` 時是單欄。
 * - 兩欄門檻用「容器寬度 ≥ 880px」（不是視窗寬度）：側欄展開的 1024px 視窗，內容區只剩約 780px，
 *   視窗斷點會把主欄擠到約 400px。不足時堆疊（主欄在上、側欄在下），側欄不 sticky。
 * - 底部留白給固定在畫面底部的 EditActionBar。
 * - 對話框、*Tab／*Panel 不要用這個元件。
 */
</script>

<template>
  <div class="edit-layout">
    <div class="edit-layout__grid" :class="{ 'edit-layout__grid--with-aside': $slots.aside }">
      <div class="edit-layout__main"><slot name="main" /></div>
      <aside v-if="$slots.aside" class="edit-layout__aside"><slot name="aside" /></aside>
    </div>
  </div>
</template>

<style scoped>
.edit-layout {
  container-type: inline-size;
  padding-bottom: 88px;
}

.edit-layout__grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--admin-space-4);
}

.edit-layout__main,
.edit-layout__aside {
  display: flex;
  flex-direction: column;
  gap: var(--admin-space-4);
  min-width: 0;
}

@container (min-width: 880px) {
  .edit-layout__grid {
    gap: var(--admin-space-6);
  }

  .edit-layout__grid--with-aside {
    grid-template-columns: minmax(0, 1fr) clamp(280px, 30%, 360px);
    align-items: start;
  }
}
</style>
