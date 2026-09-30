<script setup lang="ts">
/**
 * 特約店家與權益：同頁兩個分頁——特約店家、會員權益（權益對照表）。
 * 目前分頁記在網址的 `?tab=`。
 */
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import StoresTab from './parts/StoresTab.vue'
import BenefitsTab from './parts/BenefitsTab.vue'
import { usePermission } from '@/composables/useCrudPermissions'

type TabName = 'stores' | 'benefits'
const route = useRoute()
const router = useRouter()
const canViewStores = usePermission('member.store.view')
const canViewBenefits = usePermission('member.benefit.view')

const readTab = (): TabName => (route.query.tab === 'benefits' ? 'benefits' : 'stores')
const tab = ref<TabName>(readTab())
watch(tab, (t) => {
  if (readTab() !== t) router.replace({ query: { ...route.query, tab: t } })
})
const stop = computed(() => route.query.tab)
watch(stop, () => {
  if (route.name === 'partner-store-list') tab.value = readTab()
})
</script>

<template>
  <div class="store-view">
    <PageHeader title="特約店家與權益">
      <template #meta><FrontendUnitBanner module-code="K4" /></template>
    </PageHeader>
    <el-tabs v-model="tab" class="store-view__tabs">
      <el-tab-pane v-if="canViewStores" label="特約店家" name="stores" lazy><StoresTab /></el-tab-pane>
      <el-tab-pane v-if="canViewBenefits" label="會員權益" name="benefits" lazy><BenefitsTab /></el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.store-view { min-width: 0; }
.store-view__tabs { min-width: 0; }
</style>
