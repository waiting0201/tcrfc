<script setup lang="ts">
/**
 * 推播：推播批次清單與自動推播規則。
 * 🔴 雙人覆核：公關／媒體建立並送審，必須由「另一位」系統管理員覆核（核可者不能是建立者本人，系統管理員自己建立的也一樣）。
 * 🔴 推播不能成為「中獎只在最新消息公布」承諾的後門：內文出現中獎、得獎等字樣，或連結指向抽獎公布文章，系統會擋下。
 * 🔴 推播傳輸尚未串接：核可後批次會停在「失敗」，串接後可以重送。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import PushRulesTab from './parts/PushRulesTab.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { dispatchDuePush, listPushMessages, type PushListItemDto } from '@/api/adminApp'
import { formatDateTime } from '@/utils/dateTime'
import { PUSH_NOT_CONNECTED_TEXT, PUSH_STATUS_OPTIONS, pushStatusTag } from './pushLabels'

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canView = usePermission('app.push.view')
const canCreate = usePermission('app.push.create')
const canApprove = usePermission('app.push.approve')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

type TabName = 'messages' | 'rules'
const readTab = (): TabName => (route.query.tab === 'rules' ? 'rules' : 'messages')
const tab = ref<TabName>(readTab())
watch(tab, (t) => {
  if (readTab() !== t) router.replace({ query: { ...route.query, tab: t } })
})
const queryTab = computed(() => route.query.tab)
watch(queryTab, () => {
  if (route.name === 'app-push') tab.value = readTab()
})

const status = ref('')
const rows = ref<PushListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
async function load() {
  if (!canView.value) return void (loading.value = false)
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listPushMessages(status.value || undefined)
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '推播清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

const dispatching = ref(false)
async function dispatchDue() {
  dispatching.value = true
  try {
    const r = await dispatchDuePush()
    ElMessage.success(r.dispatched > 0 ? `已處理 ${r.dispatched} 個到時間的批次` : '目前沒有到時間的批次')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '執行失敗，請稍後再試'))
  } finally {
    dispatching.value = false
  }
}
const go = (row: PushListItemDto) => router.push(`/app/push/${row.id}`)
</script>

<template>
  <div class="push">
    <PageHeader title="推播">
      <template #meta><FrontendUnitBanner module-code="M3" /></template>
    </PageHeader>
    <el-alert class="push__block" type="warning" show-icon :closable="false" title="推播傳輸尚未串接">{{ PUSH_NOT_CONNECTED_TEXT }}</el-alert>
    <el-alert class="push__block" type="info" show-icon :closable="false" title="兩隊共用 App，推播不分俱樂部。分眾只用會籍層級、追蹤球隊與俱樂部歸屬，不做行為追蹤。會員條款完成推播蒐集告知前，不能啟用推播。" />

    <el-empty v-if="!canView" description="你的帳號沒有檢視推播的權限" />
    <el-tabs v-else v-model="tab">
      <el-tab-pane label="推播批次" name="messages" lazy>
        <el-card shadow="never" class="push__block">
          <div class="push__row">
            <el-select v-model="status" placeholder="全部狀態" clearable class="push__select" @change="load"><el-option v-for="o in PUSH_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" /></el-select>
            <el-button @click="load">重新整理</el-button>
            <span class="push__spacer" />
            <el-button v-if="canApprove" :loading="dispatching" @click="dispatchDue">立即發送已到時間的批次</el-button>
            <el-button v-if="canCreate" type="primary" @click="router.push('/app/push/new')">+ 新增推播</el-button>
          </div>
        </el-card>
        <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
        <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
        <el-card v-else shadow="never">
          <el-empty v-if="rows.length === 0" description="目前沒有推播批次（最多列出最近 200 筆）"><el-button v-if="canCreate" type="primary" @click="router.push('/app/push/new')">+ 新增第一則推播</el-button></el-empty>
          <template v-else>
            <el-table v-if="!isMobile" :data="rows" row-key="id" @row-click="go">
              <el-table-column label="標題" min-width="220"><template #default="{ row }"><el-link type="primary" :underline="false">{{ row.titleZh || '（未填標題）' }}</el-link><div class="push__muted">建立者 {{ row.createdBy || '—' }}</div></template></el-table-column>
              <el-table-column label="狀態" width="110"><template #default="{ row }"><el-tag :type="pushStatusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
              <el-table-column label="預定發送（台灣時間）" width="170"><template #default="{ row }">{{ formatDateTime(row.scheduledAt) || '核可後立刻' }}</template></el-table-column>
              <el-table-column label="實際發送" width="150"><template #default="{ row }">{{ formatDateTime(row.sentAt) || '—' }}</template></el-table-column>
              <el-table-column label="送出／送達／開啟" width="170"><template #default="{ row }">{{ row.sentCount }} ／ {{ row.deliveredCount }} ／ {{ row.openedCount }}</template></el-table-column>
            </el-table>
            <MobileCardList v-else :rows="rows" row-key="id">
              <template #title="{ row }">{{ row.titleZh || '（未填標題）' }}</template>
              <template #meta="{ row }"><el-tag :type="pushStatusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag><span>預定 {{ formatDateTime(row.scheduledAt) || '核可後立刻' }}</span><span>送出 {{ row.sentCount }}</span></template>
              <template #actions="{ row }"><el-button size="small" text type="primary" @click="go(row)">查看</el-button></template>
            </MobileCardList>
          </template>
        </el-card>
      </el-tab-pane>
      <el-tab-pane label="自動推播規則" name="rules" lazy><PushRulesTab /></el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.push { min-width: 0; }
.push__block { margin-bottom: 12px; }
.push__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.push__select { width: 150px; max-width: 100%; }
.push__spacer { flex: 1; }
.push__muted { font-size: 12px; color: var(--admin-text-tertiary); }
:deep(.el-table__row) { cursor: pointer; }
</style>
