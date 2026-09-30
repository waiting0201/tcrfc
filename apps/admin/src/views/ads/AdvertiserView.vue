<script setup lang="ts">
/**
 * 廣告主與版位（行動 App 的廣告）。同頁兩個分頁：廣告主、版位；目前分頁記在網址的 `?tab=`。
 * 🔴 廣告不分俱樂部：App 是台中磐石與台中藍鯨共用的平台，這裡的資料兩隊共用、不受站台切換影響。
 */
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import AdvertiserTab from './parts/AdvertiserTab.vue'
import SlotTab from './parts/SlotTab.vue'
import { usePermission } from '@/composables/useCrudPermissions'

type TabName = 'advertisers' | 'slots'
const route = useRoute()
const router = useRouter()
const canViewAdvertisers = usePermission('ad.advertiser.view')
const canViewSlots = usePermission('ad.slot.view')

const readTab = (): TabName => (route.query.tab === 'slots' ? 'slots' : 'advertisers')
const tab = ref<TabName>(readTab())
watch(tab, (t) => {
  if (readTab() !== t) router.replace({ query: { ...route.query, tab: t } })
})
const queryTab = computed(() => route.query.tab)
watch(queryTab, () => {
  if (route.name === 'ad-advertisers') tab.value = readTab()
})
</script>

<template>
  <div class="ad-view">
    <PageHeader title="廣告主與版位">
      <template #meta><FrontendUnitBanner module-code="E4" /></template>
    </PageHeader>
    <el-alert class="ad-view__notice" type="info" show-icon :closable="false" title="兩隊共用：行動 App 是台中磐石與台中藍鯨共用的平台，廣告主與版位不分俱樂部，切換站台不會改變這裡的內容。" />
    <el-empty v-if="!canViewAdvertisers && !canViewSlots" description="你的帳號沒有檢視廣告主與版位的權限" />
    <el-tabs v-else v-model="tab">
      <el-tab-pane v-if="canViewAdvertisers" label="廣告主" name="advertisers" lazy><AdvertiserTab /></el-tab-pane>
      <el-tab-pane v-if="canViewSlots" label="廣告版位" name="slots" lazy><SlotTab /></el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.ad-view { min-width: 0; }
.ad-view__notice { margin-bottom: 12px; }
</style>
