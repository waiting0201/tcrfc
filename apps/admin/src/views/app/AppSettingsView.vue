<script setup lang="ts">
/**
 * App 設定與連線檢查：連線檢查、功能開關、金鑰與憑證列管、診斷回報。
 * 🔴 兩隊共用 App，不分俱樂部。
 * 🔴 憑證列管「只登記管理資訊，絕不存放金鑰本身」。
 * 🔴 「尚未串接」與「異常」分開顯示：尚未串接是還沒建置，不是故障。連線檢查不會真的送出任何推播。
 */
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import ConnectionTab from './parts/ConnectionTab.vue'
import FlagTab from './parts/FlagTab.vue'
import CredentialTab from './parts/CredentialTab.vue'
import DiagnosticTab from './parts/DiagnosticTab.vue'
import { usePermission } from '@/composables/useCrudPermissions'

const route = useRoute()
const router = useRouter()
const canConfig = usePermission('app.config.view')
const canCredential = usePermission('app.credential.view')
const canDiagnostic = usePermission('app.diagnostic.view')
type TabName = 'connection' | 'flags' | 'credentials' | 'diagnostics'
const TABS: TabName[] = ['connection', 'flags', 'credentials', 'diagnostics']
const readTab = (): TabName => (TABS.includes(route.query.tab as TabName) ? (route.query.tab as TabName) : canConfig.value ? 'connection' : canDiagnostic.value ? 'diagnostics' : 'credentials')
const tab = ref<TabName>(readTab())
watch(tab, (t) => {
  if (readTab() !== t) router.replace({ query: { ...route.query, tab: t } })
})
const queryTab = computed(() => route.query.tab)
watch(queryTab, () => {
  if (route.name === 'app-settings') tab.value = readTab()
})
</script>

<template>
  <div class="as-view">
    <PageHeader title="App 設定與連線檢查">
      <template #meta><FrontendUnitBanner module-code="M5" /></template>
    </PageHeader>
    <el-alert class="as-view__notice" type="info" show-icon :closable="false" title="兩隊共用：行動 App 是台中磐石與台中藍鯨共用的平台，這裡的設定不分俱樂部。" />
    <el-empty v-if="!canConfig && !canCredential && !canDiagnostic" description="你的帳號沒有檢視 App 設定的權限" />
    <el-tabs v-else v-model="tab">
      <el-tab-pane v-if="canConfig" label="連線檢查" name="connection" lazy><ConnectionTab /></el-tab-pane>
      <el-tab-pane v-if="canConfig" label="功能開關" name="flags" lazy><FlagTab /></el-tab-pane>
      <el-tab-pane v-if="canCredential" label="金鑰與憑證列管" name="credentials" lazy><CredentialTab /></el-tab-pane>
      <el-tab-pane v-if="canDiagnostic" label="診斷回報" name="diagnostics" lazy><DiagnosticTab /></el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.as-view { min-width: 0; }
.as-view__notice { margin-bottom: 12px; }
</style>
