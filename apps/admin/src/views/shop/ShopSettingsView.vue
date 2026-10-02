<script setup lang="ts">
/**
 * 商店的設定與報表（對應前台「站內商店」的入口說明與購物政策）：商店設定、金流與發票、報表、發票捐贈碼。
 * 🔴 前台商店與結帳流程已完成；正式線上付款（LINE Pay）與電子發票待取得商店號與發票服務後才啟用（目前只有介面與本機假實作）。
 * 各分頁依權限顯示：金流與發票憑證只有系統管理員。
 */
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import ShopSettingsPanel from './parts/ShopSettingsPanel.vue'
import ShopCredentialsPanel from './parts/ShopCredentialsPanel.vue'
import ShopReportPanel from './parts/ShopReportPanel.vue'
import DonationCodePanel from './parts/DonationCodePanel.vue'
import { useViewUpdatePermissions, useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'

const route = useRoute()
const router = useRouter()
const settingPerm = useViewUpdatePermissions('shop.setting')
const credentialView = usePermission('shop.credential.view')
const reportView = usePermission('shop.report.view')
const donationPerm = useCrudPermissions('shop.donation_code')

type Tab = 'settings' | 'credentials' | 'reports' | 'codes'
const available = computed<Tab[]>(() => {
  const list: Tab[] = []
  if (settingPerm.canView.value) list.push('settings')
  if (credentialView.value) list.push('credentials')
  if (reportView.value) list.push('reports')
  if (donationPerm.canView.value) list.push('codes')
  return list
})
const initial = (): Tab => {
  const q = route.query.tab as Tab
  return available.value.includes(q) ? q : available.value[0] ?? 'settings'
}
const tab = ref<Tab>(initial())
watch(tab, (t) => router.replace({ query: { tab: t } }))
</script>

<template>
  <div class="shop-settings">
    <PageHeader title="設定與報表">
      <template #meta><FrontendUnitBanner module-code="S6" /></template>
    </PageHeader>

    <el-alert class="shop-settings__banner" type="warning" show-icon :closable="false" title="前台商店與結帳流程已完成，但正式線上付款（LINE Pay）與電子發票要等取得商店號與發票服務後才會啟用。在那之前顧客可以瀏覽商品、加入購物車，但無法線上付款，也不會開立正式發票。" />

    <el-empty v-if="available.length === 0" description="你的帳號沒有這個模組任何分頁的檢視權限。" />
    <el-tabs v-else v-model="tab">
      <el-tab-pane v-if="available.includes('settings')" label="商店設定" name="settings" lazy><ShopSettingsPanel /></el-tab-pane>
      <el-tab-pane v-if="available.includes('credentials')" label="金流與發票" name="credentials" lazy><ShopCredentialsPanel /></el-tab-pane>
      <el-tab-pane v-if="available.includes('reports')" label="報表" name="reports" lazy><ShopReportPanel /></el-tab-pane>
      <el-tab-pane v-if="available.includes('codes')" label="發票捐贈碼" name="codes" lazy><DonationCodePanel /></el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.shop-settings__banner { margin-bottom: 12px; }
</style>
