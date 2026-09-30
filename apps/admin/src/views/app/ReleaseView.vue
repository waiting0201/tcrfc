<script setup lang="ts">
/**
 * 版本發布：App 各平台的上架版本、「最低支援版本」與「建議版本」，以及維護模式。
 * 🔴 兩隊共用 App，設定不分俱樂部。
 * 🔴 寫入僅系統管理員：誤設最低支援版本會讓全體舊版使用者無法使用 App，所以其他角色只能檢視。
 * 🔴 這些設定存檔後要同步到靜態設定檔，App 才能在伺服器全部掛掉時仍讀到「維護中」；同步目前尚未串接（存檔本身仍會成功）。
 */
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import ReleaseTab from './parts/ReleaseTab.vue'
import MaintenanceTab from './parts/MaintenanceTab.vue'
import { usePermission } from '@/composables/useCrudPermissions'

const route = useRoute()
const router = useRouter()
const canView = usePermission('app.release.view')
type TabName = 'releases' | 'maintenance'
const readTab = (): TabName => (route.query.tab === 'maintenance' ? 'maintenance' : 'releases')
const tab = ref<TabName>(readTab())
watch(tab, (t) => {
  if (readTab() !== t) router.replace({ query: { ...route.query, tab: t } })
})
const queryTab = computed(() => route.query.tab)
watch(queryTab, () => {
  if (route.name === 'app-releases') tab.value = readTab()
})
</script>

<template>
  <div class="rel-view">
    <PageHeader title="版本發布">
      <template #meta><FrontendUnitBanner module-code="M1" /></template>
    </PageHeader>
    <el-alert class="rel-view__notice" type="info" show-icon :closable="false" title="兩隊共用：行動 App 是台中磐石與台中藍鯨共用的平台，版本與維護設定不分俱樂部。" />
    <el-empty v-if="!canView" description="你的帳號沒有檢視版本發布的權限" />
    <el-tabs v-else v-model="tab">
      <el-tab-pane label="上架版本" name="releases" lazy><ReleaseTab /></el-tab-pane>
      <el-tab-pane label="維護模式" name="maintenance" lazy><MaintenanceTab /></el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.rel-view { min-width: 0; }
.rel-view__notice { margin-bottom: 12px; }
</style>
