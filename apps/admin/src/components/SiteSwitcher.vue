<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { activeClubId, availableClubs, ensureClubsLoaded } from '@/auth/clubAccess'

/**
 * 站台切換器（docs/21-admin-ui.md §5）。
 * 只授權一個俱樂部的帳號不顯示下拉箭頭，改成純文字標籤——用不可互動的靜態標籤而不是隱藏整塊區域，
 * 讓使用者仍然隨時看得到「我現在在哪個站台底下」。
 *
 * ⚠️ 切換站台只換這裡的隊徽圖示與名稱文字，不整個換 Element Plus 主色（docs/21 §5／§7：
 * 「切換器是介面便利，不是安全邊界」）。
 *
 * 🔴 俱樂部清單來源已改為真實登入後的 `@/auth/clubAccess`（本輪從固定假資料改接真實服務，
 * 見該檔案上方的完整說明與已知 API 缺口：目前沒有「查詢目前帳號被授權哪些俱樂部」的自助端點，
 * 這裡列出的是系統裡「有哪些俱樂部」，不是「這個帳號被授權哪些俱樂部」——選到未授權的俱樂部會在
 * 該頁看到後端如實回傳的 403 訊息，不會誤導成功）。
 */
onMounted(() => {
  ensureClubsLoaded()
})

const clubs = availableClubs
const activeClub = computed(() => clubs.value.find((c) => c.code === activeClubId.value) ?? clubs.value[0])
const canSwitch = computed(() => clubs.value.length > 1)

function handleCommand(clubCode: string) {
  if (clubCode === activeClubId.value) return
  activeClubId.value = clubCode
  const club = clubs.value.find((c) => c.code === clubCode)
  ElMessage.success(`已切換至${club?.name}的管理畫面`)
}
</script>

<template>
  <el-dropdown v-if="canSwitch" trigger="click" @command="handleCommand">
    <span class="site-switcher">
      <img class="site-switcher__mark" :src="activeClub?.crestUrl" :alt="`${activeClub?.name}隊徽`">
      <span class="site-switcher__name">{{ activeClub?.name }}</span>
      <el-icon class="site-switcher__arrow"><ArrowDown /></el-icon>
    </span>
    <template #dropdown>
      <el-dropdown-menu>
        <el-dropdown-item
          v-for="club in clubs"
          :key="club.code"
          :command="club.code"
        >
          <el-icon v-if="club.code === activeClubId"><Check /></el-icon>
          <span v-else class="site-switcher__check-placeholder" />
          <img class="site-switcher__mark site-switcher__mark--menu-item" :src="club.crestUrl" :alt="`${club.name}隊徽`">
          {{ club.name }}
        </el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
  <span v-else-if="activeClub" class="site-switcher site-switcher--static">
    <img class="site-switcher__mark" :src="activeClub.crestUrl" :alt="`${activeClub.name}隊徽`">
    <span class="site-switcher__name">{{ activeClub.name }}</span>
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
  flex-shrink: 0;
  object-fit: contain;
}

.site-switcher__mark--menu-item {
  margin-right: 4px;
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
