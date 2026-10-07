<script setup lang="ts">
/**
 * 球員編輯頁的「賽季數據」區（apps/api/README.md B-13，規劃書 C2「可手動輸入或由賽事自動彙總」）。
 *
 * 與球員頁其他欄位不同，這裡**每個賽季各自立即儲存**，不等頁面下方的「儲存」，卡片標題下固定說明
 * （比照 docs/21 §3.4 `GalleryManager` 的規則）。球員尚未建立時不顯示（沒有 id）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { getAdminPlayerSeasonStats, type AdminPlayerSeasonStatDto } from '@/api/adminPlayers'
import { AdminApiError } from '@/api/http'
import { usePermission } from '@/composables/useCrudPermissions'
import FormSection from '@/components/FormSection.vue'
import PlayerSeasonStatRow from './PlayerSeasonStatRow.vue'

const props = defineProps<{
  club: string
  playerId: string
  /** 球員頁整頁唯讀（球隊不在授權範圍）時為 true。 */
  readonly?: boolean
}>()

const canUpdate = usePermission('team.player.update')
const canEdit = computed(() => canUpdate.value && !props.readonly)

const items = ref<AdminPlayerSeasonStatDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    items.value = (await getAdminPlayerSeasonStats(props.club, props.playerId)).items
  } catch (error) {
    items.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '賽季數據載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(() => [props.club, props.playerId], load)
</script>

<template>
  <FormSection title="賽季數據" class="stats-panel">
    <p class="stats-panel__note">
      每個賽季可以手動輸入出賽、進球、助攻與牌數；沒有手動輸入時，前台會用已結束的賽事自動彙總（烏龍球不算進球，助攻沒有自動來源）。
      這裡的變更會立即儲存，不需要按下方的儲存。
    </p>
    <el-skeleton v-if="loading" :rows="3" animated />
    <el-empty v-else-if="loadError" :image-size="64" :description="loadError">
      <el-button type="primary" size="small" @click="load">重新載入</el-button>
    </el-empty>
    <el-empty v-else-if="items.length === 0" :image-size="64" description="這個俱樂部還沒有任何賽季，請先到「賽季」新增" />
    <template v-else>
      <PlayerSeasonStatRow
        v-for="stat in items"
        :key="stat.seasonId"
        :club="club"
        :player-id="playerId"
        :stat="stat"
        :can-edit="canEdit"
        @changed="load"
      />
    </template>
  </FormSection>
</template>

<style scoped>
.stats-panel__note {
  margin: 0 0 12px;
  font-size: 12px;
  line-height: 1.6;
  color: var(--admin-text-tertiary);
}
</style>
