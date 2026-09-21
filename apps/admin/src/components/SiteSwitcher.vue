<script setup lang="ts">
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { CURRENT_USER } from '@/data/session'

/**
 * 站台切換器（docs/21-admin-ui.md §5）。
 * 只授權一個俱樂部的帳號不顯示下拉箭頭，改成純文字標籤——用不可互動的靜態標籤而不是隱藏整塊區域，
 * 讓使用者仍然隨時看得到「我現在在哪個站台底下」。
 *
 * ⚠️ 切換站台只換這裡的隊徽圖示與名稱文字，不整個換 Element Plus 主色（docs/21 §5／§7：
 * 「切換器是介面便利，不是安全邊界」）。
 */
const clubs = CURRENT_USER.authorizedClubs
const activeClubId = ref(clubs[0]?.id)

const activeClub = computed(() => clubs.find((c) => c.id === activeClubId.value) ?? clubs[0])
const canSwitch = computed(() => clubs.length > 1)

function handleCommand(clubId: string) {
  if (clubId === activeClubId.value) return
  activeClubId.value = clubId
  const club = clubs.find((c) => c.id === clubId)
  ElMessage.success(`已切換至${club?.name}的管理畫面`)
}
</script>

<template>
  <el-dropdown v-if="canSwitch" trigger="click" @command="handleCommand">
    <span class="site-switcher">
      <span class="site-switcher__mark" :style="{ backgroundColor: activeClub?.markColor }" />
      <span class="site-switcher__name">{{ activeClub?.name }}</span>
      <el-icon class="site-switcher__arrow"><ArrowDown /></el-icon>
    </span>
    <template #dropdown>
      <el-dropdown-menu>
        <el-dropdown-item
          v-for="club in clubs"
          :key="club.id"
          :command="club.id"
        >
          <el-icon v-if="club.id === activeClubId"><Check /></el-icon>
          <span v-else class="site-switcher__check-placeholder" />
          {{ club.name }}
        </el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
  <span v-else class="site-switcher site-switcher--static">
    <span class="site-switcher__mark" :style="{ backgroundColor: activeClub?.markColor }" />
    <span class="site-switcher__name">{{ activeClub?.name }}</span>
  </span>
</template>

<style scoped>
.site-switcher {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  cursor: pointer;
  color: var(--el-text-color-primary);
  font-size: 13px;
  background: var(--el-fill-color-blank);
}

.site-switcher--static {
  cursor: default;
}

.site-switcher__mark {
  width: 16px;
  height: 16px;
  border-radius: 3px;
  flex-shrink: 0;
}

.site-switcher__arrow {
  font-size: 12px;
  color: var(--el-text-color-placeholder);
}

.site-switcher__check-placeholder {
  display: inline-block;
  width: 14px;
}

.site-switcher__name {
  white-space: nowrap;
}
</style>
