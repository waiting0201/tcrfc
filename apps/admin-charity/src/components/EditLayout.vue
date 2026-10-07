<script setup lang="ts">
/**
 * 編輯頁的兩欄版面（docs/21 §3.3，慈善後台沿用）。
 *
 * - `#main`：主欄（恰好一張卡片：文字欄位與太寬的元件）；`#aside`：右側欄（「基本設定」＋「發布設定」，
 *   所有圖片上傳在這裡）。沒有 `#aside` 時是單欄。
 * - 兩欄門檻用「容器寬度 ≥ 880px」（不是視窗寬度）：側欄展開的 1024px 視窗，內容區只剩約 780px，
 *   視窗斷點會把主欄擠到約 400px。不足時堆疊（主欄在上、側欄在下），側欄不 sticky。
 * - 底部不另留白：EditActionBar 是 sticky 在捲動容器底部（慈善後台的側欄寬度會隨收合改變，
 *   不能像 apps/admin 那樣用 fixed 並寫死側欄寬度）。
 * - 對話框、*Panel 不要用這個元件。
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
  padding-bottom: var(--charity-admin-space-4);
}

.edit-layout__grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--charity-admin-space-4);
}

.edit-layout__main,
.edit-layout__aside {
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-4);
  min-width: 0;
}

@container (min-width: 880px) {
  .edit-layout__grid {
    gap: var(--charity-admin-space-6);
  }

  .edit-layout__grid--with-aside {
    grid-template-columns: minmax(0, 1fr) clamp(280px, 30%, 360px);
    align-items: start;
  }
}
</style>
