<script setup lang="ts">
/**
 * 「兩隊共用」內容的唯讀說明（後端規則：共同列 `club_id` 為空，編輯／刪除一律拒絕）。
 * 放在編輯頁頁頭的 meta 區，讓使用者在填表前就知道為什麼整張表單鎖住。
 */
withDefaults(defineProps<{ what?: string }>(), { what: '內容' })
</script>

<template>
  <span class="shared-notice">
    <el-tag type="info" size="small">共用內容（唯讀）</el-tag>
    這筆{{ what }}是台中磐石與台中藍鯨共用的，你只能檢視；如需修改請聯繫系統管理員。
  </span>
</template>

<style scoped>
.shared-notice {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
  font-size: 13px;
  color: var(--admin-text-secondary);
}
</style>
