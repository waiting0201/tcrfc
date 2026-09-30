<script setup lang="ts">
/**
 * 內容編排：App 首頁區塊的開關與順序、快捷入口、「更多」分頁項目、深連結對照表、公告條。
 * 🔴 這裡只管「呈現的順序與開關」，不做 App 內的內容維護——新聞、賽事、球員、店家仍在各自的模組維護。
 * 🔴 兩隊共用 App，設定不分俱樂部。首頁九個區塊是固定的，只能開關、排序、改名稱與連結，不能新增或刪除。
 */
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import LayoutItemTab from './parts/LayoutItemTab.vue'
import DeepLinkTab from './parts/DeepLinkTab.vue'
import AnnouncementTab from './parts/AnnouncementTab.vue'
import { usePermission } from '@/composables/useCrudPermissions'

const route = useRoute()
const router = useRouter()
const canView = usePermission('app.layout.view')
type TabName = 'home_section' | 'quick_entry' | 'more_item' | 'deep_links' | 'announcements'
const TABS: TabName[] = ['home_section', 'quick_entry', 'more_item', 'deep_links', 'announcements']
const readTab = (): TabName => (TABS.includes(route.query.tab as TabName) ? (route.query.tab as TabName) : 'home_section')
const tab = ref<TabName>(readTab())
watch(tab, (t) => {
  if (readTab() !== t) router.replace({ query: { ...route.query, tab: t } })
})
const queryTab = computed(() => route.query.tab)
watch(queryTab, () => {
  if (route.name === 'app-content') tab.value = readTab()
})
</script>

<template>
  <div class="ac-view">
    <PageHeader title="內容編排">
      <template #meta><FrontendUnitBanner module-code="M2" /></template>
    </PageHeader>
    <el-alert class="ac-view__notice" type="info" show-icon :closable="false" title="兩隊共用：行動 App 是台中磐石與台中藍鯨共用的平台，這裡的設定不分俱樂部。這裡只管 App 上「呈現的順序與開關」；新聞、賽事、球員、店家的內容請到各自的模組維護。" />
    <el-empty v-if="!canView" description="你的帳號沒有檢視內容編排的權限" />
    <el-tabs v-else v-model="tab">
      <el-tab-pane label="首頁區塊" name="home_section" lazy><LayoutItemTab kind="home_section" /></el-tab-pane>
      <el-tab-pane label="快捷入口" name="quick_entry" lazy><LayoutItemTab kind="quick_entry" /></el-tab-pane>
      <el-tab-pane label="「更多」分頁項目" name="more_item" lazy><LayoutItemTab kind="more_item" /></el-tab-pane>
      <el-tab-pane label="App 內連結" name="deep_links" lazy><DeepLinkTab /></el-tab-pane>
      <el-tab-pane label="公告條" name="announcements" lazy><AnnouncementTab /></el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.ac-view { min-width: 0; }
.ac-view__notice { margin-bottom: 12px; }
</style>
