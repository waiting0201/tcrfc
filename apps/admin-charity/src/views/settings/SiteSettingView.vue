<script setup lang="ts">
/**
 * 站台設定（docs/22-charity-ui.md §3.7.7、§3.9）：接真 API。
 * 頁籤依權限顯示：站台文案與系統信樣板（有「站台設定」權限者）、金流與發票通道、操作紀錄（僅系統管理員）。
 */
import { computed, ref } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import SettingsCopyPanel from '@/components/SettingsCopyPanel.vue'
import EmailTemplatesPanel from '@/components/EmailTemplatesPanel.vue'
import PaymentChannelsPanel from '@/components/PaymentChannelsPanel.vue'
import AuditLogPanel from '@/components/AuditLogPanel.vue'
import { hasPermission } from '@/auth/session'

const canSettings = computed(() => hasPermission('n7.setting.view'))
const canChannels = computed(() => hasPermission('n7.payment_channel.manage'))
const canAudit = computed(() => hasPermission('n7.audit_log.view'))

const firstTab = canSettings.value ? 'copy' : canChannels.value ? 'channels' : 'audit'
const activeTab = ref(firstTab)
</script>

<template>
  <div>
    <PageHeader title="站台設定" frontend-unit="站台文案、系統信、徵信名單" />

    <el-tabs v-model="activeTab">
      <el-tab-pane v-if="canSettings" label="站台文案與金額" name="copy">
        <SettingsCopyPanel />
      </el-tab-pane>
      <el-tab-pane v-if="canSettings" label="系統信樣板" name="mail" lazy>
        <EmailTemplatesPanel />
      </el-tab-pane>
      <el-tab-pane v-if="canChannels" label="金流與發票" name="channels" lazy>
        <PaymentChannelsPanel />
      </el-tab-pane>
      <el-tab-pane v-if="canAudit" label="操作紀錄" name="audit" lazy>
        <AuditLogPanel />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>
