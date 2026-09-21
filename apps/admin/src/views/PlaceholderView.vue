<script setup lang="ts">
import { useRoute } from 'vue-router'

/**
 * 側欄裡尚未建置的模組共用這一頁——明確告知「這個模組還沒做」，不是死連結
 * （任務交付說明第 2 條）。頁首仍然照規則附「這裡管理的是」一列，因為就算功能沒做，
 * 這條規則描述的是「模組對應前台哪裡」這件事，跟功能做了沒無關。
 */
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'

const route = useRoute()
const label = (route.meta.label as string) ?? '此模組'
const code = (route.meta.code as string) ?? ''
</script>

<template>
  <div class="placeholder-view">
    <PageHeader :title="label">
      <template v-if="code" #meta>
        <FrontendUnitBanner :module-code="code" />
      </template>
    </PageHeader>
    <div class="placeholder-view__body">
      <el-empty description="這個模組還沒有做，之後會補上">
        <template #image>
          <el-icon :size="64" color="var(--admin-text-tertiary)"><Tools /></el-icon>
        </template>
        <p class="placeholder-view__note">
          「{{ label }}」尚未建置。本階段只實作了外殼、儀表板與新聞與故事的列表／編輯頁，
          其餘模組留待之後的階段依這份標準型逐一完成。
        </p>
      </el-empty>
    </div>
  </div>
</template>

<style scoped>
.placeholder-view__body {
  max-width: 720px;
  margin: 40px auto 0;
  text-align: center;
}

.placeholder-view__note {
  color: var(--admin-text-secondary);
  font-size: 13px;
  max-width: 440px;
  margin: 8px auto 0;
}
</style>
