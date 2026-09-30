<script setup lang="ts">
/**
 * 慈善與社會影響（對應前台「慈善與社會影響」：慈善計畫、事蹟紀錄、影響力數據、捐款導流）。
 * 五個分頁：公益團體、慈善計畫、事蹟紀錄、影響力數據、捐款導流設定。
 * 慈善是台中磐石的單元，台中藍鯨沒有這個前台頁面。
 */
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import CharityOrgPanel from './CharityOrgPanel.vue'
import CharityProgramPanel from './CharityProgramPanel.vue'
import CharityRecordPanel from './CharityRecordPanel.vue'
import CharityMetricPanel from './CharityMetricPanel.vue'
import CharitySettingsPanel from './CharitySettingsPanel.vue'

const route = useRoute()
const router = useRouter()
const TABS = ['programs', 'records', 'organizations', 'metrics', 'settings'] as const
type TabName = (typeof TABS)[number]
const initial = TABS.find((t) => t === route.query.tab) ?? 'programs'
const tab = ref<TabName>(initial)
watch(tab, (t) => router.replace({ query: { tab: t } }))
const setting = useViewUpdatePermissions('charity.setting')
const isBlueWhale = computed(() => activeClubId.value === 'bw')
</script>

<template>
  <div class="charity">
    <PageHeader title="慈善與社會影響">
      <template #meta><FrontendUnitBanner module-code="B5" /></template>
    </PageHeader>
    <el-alert
      v-if="isBlueWhale"
      type="info"
      show-icon
      :closable="false"
      class="charity__note"
      title="慈善與社會影響是台中磐石官網的單元，台中藍鯨官網沒有這個頁面。在藍鯨站台建立的內容不會顯示在前台。"
    />
    <el-tabs v-model="tab">
      <el-tab-pane label="慈善計畫" name="programs"><CharityProgramPanel v-if="tab === 'programs'" /></el-tab-pane>
      <el-tab-pane label="事蹟紀錄" name="records"><CharityRecordPanel v-if="tab === 'records'" /></el-tab-pane>
      <el-tab-pane label="公益團體" name="organizations"><CharityOrgPanel v-if="tab === 'organizations'" /></el-tab-pane>
      <el-tab-pane label="影響力數據" name="metrics"><CharityMetricPanel v-if="tab === 'metrics'" /></el-tab-pane>
      <el-tab-pane v-if="setting.canView.value" label="捐款導流設定" name="settings"><CharitySettingsPanel v-if="tab === 'settings'" /></el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.charity__note { margin-bottom: 12px; }
</style>
