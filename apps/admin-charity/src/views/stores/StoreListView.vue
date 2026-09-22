<script setup lang="ts">
/** N1 店家管理與 QR Code（docs/22-charity-ui.md §3.7.1） */
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import DangerConfirmDialog from '@/components/DangerConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { STORES, type Store } from '@/data/fixtures'
import { formatDate, formatMoney } from '@/utils/format'
import { hasPermission } from '@/data/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度（768–1023px）表格仍是 el-table，但欄位總寬度會超出可視寬度——CSS display:none
// 隱藏儲存格不會讓 el-table 縮小欄位總寬（它的版面計算是照 el-table-column 的數量與 width
// 參數加總，不看 CSS 有沒有把某個儲存格藏起來），所以次要欄位要用 v-if 整欄不渲染，
// 不能只用 CSS 隱藏（這是本次驗收在平板寬度實測抓到的問題，不是理論假設）。
const isDesktop = computed(() => breakpoint.value === 'desktop')

const qrDialogStore = ref<Store | null>(null)
const qrDialogVisible = computed({
  get: () => qrDialogStore.value !== null,
  set: (v: boolean) => {
    if (!v) qrDialogStore.value = null
  },
})
const regenerateDialogVisible = ref(false)
const regenerateTarget = ref<Store | null>(null)

function openQr(store: Store) {
  qrDialogStore.value = store
}

function openRegenerate(store: Store) {
  regenerateTarget.value = store
  regenerateDialogVisible.value = true
}

function confirmRegenerate() {
  ElMessage.success(`已為「${regenerateTarget.value?.nameZh}」重新產生網址名稱（mockup，未實際變更資料）`)
  regenerateTarget.value = null
}

function downloadPlaceholder(kind: string) {
  ElMessage.info(`mockup 尚未接產檔服務，正式環境會下載${kind}`)
}
</script>

<template>
  <div>
    <PageHeader title="店家管理與 QR Code" frontend-unit="掃碼落地頁">
      <template #actions>
        <el-button v-if="hasPermission('n1.donation_store.manage')" type="primary" @click="router.push('/stores/new')">
          新增店家
        </el-button>
      </template>
    </PageHeader>

    <EmptyState v-if="STORES.length === 0" text="目前沒有店家" />

    <el-table v-else-if="!isMobile" :data="STORES" style="width: 100%">
      <el-table-column label="店名" min-width="180">
        <template #default="{ row }: { row: Store }">
          <div>{{ row.nameZh }}</div>
          <div class="store-list__en">{{ row.nameEn }}</div>
        </template>
      </el-table-column>
      <el-table-column v-if="isDesktop" prop="category" label="類別" width="90" />
      <el-table-column v-if="isDesktop" label="合作起訖" width="200">
        <template #default="{ row }: { row: Store }">{{ formatDate(row.startOn) }} ～ {{ row.endOn ? formatDate(row.endOn) : '持續中' }}</template>
      </el-table-column>
      <el-table-column label="狀態" width="90">
        <template #default="{ row }: { row: Store }">
          <SemanticTag :variant="row.status === 'active' ? 'success' : 'neutral'">
            {{ row.status === 'active' ? '合作中' : '已停止' }}
          </SemanticTag>
        </template>
      </el-table-column>
      <el-table-column v-if="isDesktop" label="累計捐款" width="150">
        <template #default="{ row }: { row: Store }">{{ row.donationCount }} 筆・{{ formatMoney(row.donationTotal) }}</template>
      </el-table-column>
      <el-table-column v-if="isDesktop" label="應付回饋金" width="120">
        <template #default="{ row }: { row: Store }">{{ formatMoney(row.payableTotal) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="220" fixed="right">
        <template #default="{ row }: { row: Store }">
          <el-button size="small" text type="primary" @click="router.push(`/stores/${row.key}/edit`)">編輯</el-button>
          <el-button size="small" text type="primary" @click="openQr(row)">產生 QR</el-button>
          <el-button size="small" text @click="openRegenerate(row)">重新產生網址</el-button>
        </template>
      </el-table-column>
    </el-table>

    <MobileCardList v-else :items="STORES">
      <template #default="{ item }: { item: Store }">
        <div class="store-list__card-head">
          <div>
            <div class="store-list__card-name">{{ item.nameZh }}</div>
            <div class="store-list__en">{{ item.nameEn }}</div>
          </div>
          <SemanticTag :variant="item.status === 'active' ? 'success' : 'neutral'">
            {{ item.status === 'active' ? '合作中' : '已停止' }}
          </SemanticTag>
        </div>
        <p class="store-list__card-meta">
          {{ item.category }}・累計 {{ item.donationCount }} 筆・{{ formatMoney(item.donationTotal) }}・應付回饋金 {{ formatMoney(item.payableTotal) }}
        </p>
        <div class="store-list__card-actions">
          <el-button size="small" text type="primary" @click="router.push(`/stores/${item.key}/edit`)">編輯</el-button>
          <el-button size="small" text type="primary" @click="openQr(item)">產生 QR</el-button>
          <el-button size="small" text @click="openRegenerate(item)">重新產生網址</el-button>
        </div>
      </template>
    </MobileCardList>

    <el-dialog v-model="qrDialogVisible" :title="`${qrDialogStore?.nameZh ?? ''} — QR Code`" width="420px">
      <p class="store-list__qr-note">
        店家網址名稱：<code>{{ qrDialogStore?.slug }}</code><br>
        印刷版預覽含協會標誌留白位版位（協會品牌資產尚未提供，不放假圖）。
      </p>
      <div class="store-list__qr-actions">
        <el-button @click="downloadPlaceholder('PNG')">下載 PNG</el-button>
        <el-button @click="downloadPlaceholder('SVG')">下載 SVG</el-button>
        <el-button @click="downloadPlaceholder('含店名的印刷版 PDF')">下載印刷版 PDF</el-button>
      </div>
      <template #footer>
        <el-button type="primary" @click="qrDialogStore = null">關閉</el-button>
      </template>
    </el-dialog>

    <DangerConfirmDialog
      v-model="regenerateDialogVisible"
      title="重新產生網址名稱"
      :require-reason="false"
      confirm-text="確認重新產生"
      @confirm="confirmRegenerate"
    >
      舊的 QR Code 將立即失效，需要重新列印。確定要為「{{ regenerateTarget?.nameZh }}」重新產生網址名稱嗎？
    </DangerConfirmDialog>
  </div>
</template>

<style scoped>
.store-list__en {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.store-list__qr-note {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.store-list__qr-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  margin-top: var(--charity-admin-space-3);
}

.store-list__card-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: var(--charity-admin-space-2);
}

.store-list__card-name {
  font-weight: 600;
  color: var(--charity-admin-text-primary);
}

.store-list__card-meta {
  margin: var(--charity-admin-space-2) 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.store-list__card-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-1);
  padding-top: var(--charity-admin-space-2);
  border-top: 1px solid var(--charity-admin-border);
}
</style>
