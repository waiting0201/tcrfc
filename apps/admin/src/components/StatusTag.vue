<script setup lang="ts">
import { computed } from 'vue'
import type { ContentStatus } from '@/types/common'

/** 狀態四態的 el-tag 呈現（docs/21-admin-ui.md §4）。不是每個模組都有全部四態，但有這個語意的都共用這顆元件。 */
const props = defineProps<{
  status: ContentStatus
  /** 已發布：發布時間；排程發布：預計發布時間；已停用：下架時間 */
  statusAt?: string
  statusBy?: string
}>()

const STATUS_LABEL: Record<ContentStatus, string> = {
  draft: '草稿',
  scheduled: '排程發布',
  published: '已發布',
  disabled: '已停用',
}

const tagType = computed<'info' | 'warning' | 'success' | undefined>(() => {
  switch (props.status) {
    case 'draft':
      return 'info'
    case 'scheduled':
      return 'warning'
    case 'published':
      return 'success'
    default:
      return undefined
  }
})

const tooltip = computed(() => {
  if (props.status === 'scheduled' && props.statusAt) return `將於 ${props.statusAt} 發布`
  if (props.status === 'published' && props.statusAt) return `於 ${props.statusAt} 發布`
  if (props.status === 'disabled' && props.statusAt) {
    return props.statusBy ? `於 ${props.statusAt} 由 ${props.statusBy} 下架` : `於 ${props.statusAt} 下架`
  }
  return ''
})
</script>

<template>
  <el-tooltip v-if="tooltip" :content="tooltip" placement="top">
    <el-tag
      :type="tagType"
      :class="{ 'admin-status-tag--disabled': status === 'disabled' }"
      size="small"
    >
      {{ STATUS_LABEL[status] }}
    </el-tag>
  </el-tooltip>
  <el-tag
    v-else
    :type="tagType"
    :class="{ 'admin-status-tag--disabled': status === 'disabled' }"
    size="small"
  >
    {{ STATUS_LABEL[status] }}
  </el-tag>
</template>
