<script setup lang="ts">
/** N1 店家管理與 QR Code（docs/22-charity-ui.md §3.7.1）：接真 API，列表由伺服器分頁與篩選。 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import DangerConfirmDialog from '@/components/DangerConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import StoreImportDialog from '@/components/StoreImportDialog.vue'
import {
  downloadAllStoreQr,
  downloadStoreQr,
  fetchStoreQrPreview,
  listStores,
  regenerateStoreSlug,
  type StoreListItem,
} from '@/api/stores'
import { AdminApiError } from '@/api/http'
import { formatDate, formatMoney } from '@/utils/format'
import { hasPermission } from '@/auth/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度的次要欄位用 v-if 整欄不渲染（el-table 欄位總寬不看 CSS 隱藏，見 README「響應式」）。
const isDesktop = computed(() => breakpoint.value === 'desktop')

const canManage = computed(() => hasPermission('n1.donation_store.manage'))
const canExport = computed(() => hasPermission('n1.donation_store.export'))
const canQr = computed(() => canManage.value || canExport.value)

const items = ref<StoreListItem[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 20
const keyword = ref('')
const status = ref('')
const loading = ref(false)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const result = await listStores({ keyword: keyword.value.trim(), status: status.value, page: page.value, pageSize })
    items.value = result.items
    totalCount.value = result.totalCount
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '查詢時發生問題，請稍後再試一次'
  } finally {
    loading.value = false
  }
}

function search() {
  page.value = 1
  void load()
}

watch(page, () => { void load() })
onMounted(load)

const importVisible = ref(false)

// ── QR Code ───────────────────────────────────────────────────────────
const qrStore = ref<StoreListItem | null>(null)
const qrPreviewUrl = ref<string | null>(null)
const qrError = ref('')
const qrBusy = ref(false)
const qrVisible = computed({
  get: () => qrStore.value !== null,
  set: (v: boolean) => { if (!v) closeQr() },
})

async function openQr(store: StoreListItem) {
  qrStore.value = store
  qrError.value = ''
  qrPreviewUrl.value = null
  try {
    qrPreviewUrl.value = await fetchStoreQrPreview(store.id)
  } catch (error) {
    qrError.value = error instanceof AdminApiError ? error.detail : '無法產生 QR Code'
  }
}

function closeQr() {
  if (qrPreviewUrl.value) URL.revokeObjectURL(qrPreviewUrl.value)
  qrPreviewUrl.value = null
  qrStore.value = null
}

onBeforeUnmount(() => { if (qrPreviewUrl.value) URL.revokeObjectURL(qrPreviewUrl.value) })

async function downloadQr(format: 'png' | 'svg') {
  if (!qrStore.value) return
  qrBusy.value = true
  try {
    await downloadStoreQr(qrStore.value.id, format, `${qrStore.value.nameZh ?? 'store'}-${qrStore.value.slug}`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '下載失敗')
  } finally {
    qrBusy.value = false
  }
}

const batchBusy = ref(false)
async function downloadBatch(format: 'png' | 'svg') {
  batchBusy.value = true
  try {
    await downloadAllStoreQr(format)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '下載失敗')
  } finally {
    batchBusy.value = false
  }
}

// ── 重新產生網址名稱 ─────────────────────────────────────────────────
const regenerateVisible = ref(false)
const regenerateTarget = ref<StoreListItem | null>(null)

function openRegenerate(store: StoreListItem) {
  regenerateTarget.value = store
  regenerateVisible.value = true
}

async function confirmRegenerate() {
  const target = regenerateTarget.value
  if (!target) return
  try {
    await regenerateStoreSlug(target.id)
    ElMessage.success(`已為「${target.nameZh}」重新產生網址名稱，舊的 QR Code 已失效，請重新下載列印`)
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '重新產生失敗')
  }
}

function statusLabel(s: string) {
  return s === 'active' ? '合作中' : '已停止'
}
</script>

<template>
  <div>
    <PageHeader title="店家管理與 QR Code" frontend-unit="掃碼落地頁">
      <template #actions>
        <el-dropdown v-if="canExport" trigger="click" @command="downloadBatch">
          <el-button :loading="batchBusy">全部合作中店家的 QR 打包下載</el-button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="png">PNG 圖檔（zip）</el-dropdown-item>
              <el-dropdown-item command="svg">SVG 向量檔（zip）</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
        <el-button v-if="canManage" @click="importVisible = true">批次匯入</el-button>
        <el-button v-if="canManage" type="primary" @click="router.push('/stores/new')">新增店家</el-button>
      </template>
    </PageHeader>

    <div class="store-list__filters">
      <el-input v-model="keyword" placeholder="搜尋店名" clearable style="max-width: 240px" @keyup.enter="search" @clear="search" />
      <el-select v-model="status" placeholder="合作狀態" clearable style="width: 140px" @change="search">
        <el-option value="active" label="合作中" />
        <el-option value="inactive" label="已停止" />
      </el-select>
      <el-button @click="search">查詢</el-button>
    </div>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <EmptyState v-else-if="!loading && items.length === 0" text="目前沒有符合條件的店家" />

    <template v-else>
      <el-table v-if="!isMobile" v-loading="loading" :data="items" style="width: 100%">
        <el-table-column label="店名" min-width="180">
          <template #default="{ row }: { row: StoreListItem }">
            <div>{{ row.nameZh }}</div>
            <div class="store-list__en">{{ row.nameEn }}</div>
          </template>
        </el-table-column>
        <el-table-column v-if="isDesktop" prop="category" label="類別" width="90" />
        <el-table-column v-if="isDesktop" label="合作起訖" width="200">
          <template #default="{ row }: { row: StoreListItem }">{{ formatDate(row.startOn) }} ～ {{ row.endOn ? formatDate(row.endOn) : '持續中' }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="90">
          <template #default="{ row }: { row: StoreListItem }">
            <SemanticTag :variant="row.status === 'active' ? 'success' : 'neutral'">{{ statusLabel(row.status) }}</SemanticTag>
          </template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="累計捐款" width="170">
          <template #default="{ row }: { row: StoreListItem }">{{ row.paidCount }} 筆・{{ formatMoney(row.paidTotal) }}</template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="應付回饋金" width="120">
          <template #default="{ row }: { row: StoreListItem }">{{ formatMoney(row.storeShareAccrued) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="230" fixed="right">
          <template #default="{ row }: { row: StoreListItem }">
            <el-button v-if="canManage" size="small" text type="primary" @click="router.push(`/stores/${row.id}/edit`)">編輯</el-button>
            <el-button v-if="canQr" size="small" text type="primary" @click="openQr(row)">產生 QR</el-button>
            <el-button v-if="canManage" size="small" text @click="openRegenerate(row)">重新產生網址</el-button>
          </template>
        </el-table-column>
      </el-table>

      <MobileCardList v-else :items="items">
        <template #default="{ item }: { item: StoreListItem }">
          <div class="store-list__card-head">
            <div>
              <div class="store-list__card-name">{{ item.nameZh }}</div>
              <div class="store-list__en">{{ item.nameEn }}</div>
            </div>
            <SemanticTag :variant="item.status === 'active' ? 'success' : 'neutral'">{{ statusLabel(item.status) }}</SemanticTag>
          </div>
          <p class="store-list__card-meta">
            {{ item.category }}・累計 {{ item.paidCount }} 筆・{{ formatMoney(item.paidTotal) }}・應付回饋金 {{ formatMoney(item.storeShareAccrued) }}
          </p>
          <div class="store-list__card-actions">
            <el-button v-if="canManage" size="small" text type="primary" @click="router.push(`/stores/${item.id}/edit`)">編輯</el-button>
            <el-button v-if="canQr" size="small" text type="primary" @click="openQr(item)">產生 QR</el-button>
            <el-button v-if="canManage" size="small" text @click="openRegenerate(item)">重新產生網址</el-button>
          </div>
        </template>
      </MobileCardList>

      <el-pagination
        v-if="totalCount > pageSize"
        v-model:current-page="page"
        class="store-list__pager"
        layout="prev, pager, next"
        :page-size="pageSize"
        :total="totalCount"
      />
    </template>

    <el-dialog v-model="qrVisible" :title="`${qrStore?.nameZh ?? ''} — QR Code`" width="420px">
      <div class="store-list__qr-preview">
        <img v-if="qrPreviewUrl" :src="qrPreviewUrl" alt="店家 QR Code 預覽" class="store-list__qr-img">
        <p v-else-if="qrError" class="store-list__qr-error">{{ qrError }}</p>
        <el-icon v-else class="is-loading" :size="24"><Loading /></el-icon>
      </div>
      <p class="store-list__qr-note">
        掃描後會前往這家店的捐款落地頁，網址不含金額或分潤參數。<br>
        印刷時請確認 QR Code 邊長至少 3 公分；要放大列印請下載 SVG 向量檔。
      </p>
      <div class="store-list__qr-actions">
        <el-button :loading="qrBusy" :disabled="!qrPreviewUrl" @click="downloadQr('png')">下載 PNG</el-button>
        <el-button :loading="qrBusy" :disabled="!qrPreviewUrl" @click="downloadQr('svg')">下載 SVG</el-button>
        <el-button disabled>含店名的印刷版 PDF（尚未提供）</el-button>
      </div>
      <p class="store-list__qr-note">印刷版 PDF 需要協會標誌與中文字型，兩者到位後才能提供，目前請先下載 PNG 或 SVG。</p>
      <template #footer>
        <el-button type="primary" @click="closeQr">關閉</el-button>
      </template>
    </el-dialog>

    <DangerConfirmDialog
      v-model="regenerateVisible"
      title="重新產生網址名稱"
      :require-reason="false"
      confirm-text="確認重新產生"
      audit-notice="這個操作會記錄操作者。"
      @confirm="confirmRegenerate"
    >
      舊的 QR Code 將立即失效（掃到舊碼的人會被視為沒有店家歸屬），需要重新列印。確定要為「{{ regenerateTarget?.nameZh }}」重新產生網址名稱嗎？
    </DangerConfirmDialog>

    <StoreImportDialog v-model="importVisible" @imported="load" />
  </div>
</template>

<style scoped>
.store-list__filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  margin-bottom: var(--charity-admin-space-4);
}

.store-list__en {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.store-list__pager {
  margin-top: var(--charity-admin-space-4);
  justify-content: center;
}

.store-list__qr-preview {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 120px;
  background: #ffffff;
  border: 1px solid var(--charity-admin-border);
  border-radius: 4px;
}

.store-list__qr-img {
  width: 100%;
  max-width: 300px;
  height: auto;
  image-rendering: pixelated;
}

.store-list__qr-error {
  color: var(--charity-danger-text);
  font-size: 13px;
  padding: var(--charity-admin-space-3);
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
