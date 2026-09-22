<script setup lang="ts">
import { computed } from 'vue'
import type { ContentStatus } from '@/types/common'
import { formatDateTime } from '@/utils/formatDateTime'

/**
 * 狀態四態的呈現（docs/21-admin-ui.md §4.2）。深色語意底＋亮色語意文字，不是 Element Plus
 * 內建 el-tag 的淺色 type（那是為淺色底設計的，直接套進深色頁面會變成突兀的高亮方塊）。
 * 色票用 admin-theme.css 定義的 .admin-status-tag--* 這組 class，不透過 el-tag 的 type prop。
 */
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

const statusClass = computed(() => `admin-status-tag admin-status-tag--${props.status}`)

const tooltip = computed(() => {
  const at = formatDateTime(props.statusAt)
  if (props.status === 'scheduled' && at) return `將於 ${at} 發布`
  if (props.status === 'published' && at) return `於 ${at} 發布`
  if (props.status === 'disabled' && at) {
    return props.statusBy ? `於 ${at} 由 ${props.statusBy} 下架` : `於 ${at} 下架`
  }
  return ''
})
</script>

<template>
  <el-tooltip v-if="tooltip" :content="tooltip" placement="top">
    <el-tag :class="statusClass" disable-transitions size="small">
      {{ STATUS_LABEL[status] }}
    </el-tag>
  </el-tooltip>
  <el-tag v-else :class="statusClass" disable-transitions size="small">
    {{ STATUS_LABEL[status] }}
  </el-tag>
</template>
