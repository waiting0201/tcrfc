<script setup lang="ts">
import { computed } from 'vue'
import { getFrontendUnit } from '@/data/frontendUnits'

/**
 * 「這裡管理的是：{前台單元中文名稱} ↗」——規劃書 §4.0 前後台對照表的硬性規定，
 * 每個列表頁與編輯頁都要固定出現在同一個位置（docs/21-admin-ui.md §6）。
 */
const props = withDefaults(
  defineProps<{
    /** 模組代號，如 B2——只用來查表，不會顯示在畫面上 */
    moduleCode: string
    /** 編輯頁：這一筆資料是否已發布過。未發布時沒有正式網址可連（docs/21 §6） */
    recordPublished?: boolean
    /** 編輯頁：這一筆資料的實際前台網址（已發布時才有意義） */
    recordUrl?: string
  }>(),
  {
    recordPublished: undefined,
    recordUrl: undefined,
  },
)

const unit = computed(() => getFrontendUnit(props.moduleCode))

/** 編輯頁模式：外部傳入 recordPublished 時，代表這是單筆資料層級的連結 */
const isRecordLevel = computed(() => props.recordPublished !== undefined)

const showLink = computed(() => unit.value.linkType === 'link' || unit.value.linkType === 'multi')

const effectiveUrl = computed(() => {
  if (isRecordLevel.value) {
    return props.recordPublished ? props.recordUrl : undefined
  }
  return unit.value.url
})
</script>

<template>
  <span class="frontend-unit-banner">
    <template v-if="unit.linkType === 'none'">
      <span class="frontend-unit-banner__muted">本頁僅供後台管理使用，沒有對應的前台頁面</span>
    </template>
    <template v-else-if="unit.linkType === 'app'">
      <span class="frontend-unit-banner__muted">這裡管理的是：{{ unit.label }}</span>
    </template>
    <template v-else-if="isRecordLevel && !props.recordPublished">
      <span class="frontend-unit-banner__muted">
        這裡管理的是：{{ unit.label }}（這筆資料尚未發布，沒有正式網址可預覽）
      </span>
    </template>
    <template v-else-if="showLink && effectiveUrl">
      <a
        class="frontend-unit-banner__link"
        :href="effectiveUrl"
        target="_blank"
        rel="noopener noreferrer"
      >
        這裡管理的是：{{ unit.label }}
        <el-icon><TopRight /></el-icon>
      </a>
    </template>
    <template v-else>
      <span class="frontend-unit-banner__muted">這裡管理的是：{{ unit.label }}</span>
    </template>
  </span>
</template>

<style scoped>
.frontend-unit-banner {
  font-size: 13px;
  min-width: 0;
}

.frontend-unit-banner__link,
.frontend-unit-banner__muted {
  white-space: normal;
  word-break: break-word;
}

.frontend-unit-banner__link {
  color: var(--admin-primary);
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  gap: 2px;
}

.frontend-unit-banner__link:hover {
  text-decoration: underline;
}

.frontend-unit-banner__muted {
  color: var(--admin-text-tertiary);
}
</style>
