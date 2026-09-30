<script setup lang="ts">
/**
 * 贊助（對應前台「合作夥伴與贊助」）：分「贊助商」「贊助方案」兩個分頁。
 * 贊助商依合約狀態標示到期提醒；贊助活動與圖集在各贊助商的編輯頁內維護。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  deleteSponsor,
  deleteSponsorPackage,
  listSponsorPackages,
  listSponsors,
  reorderSponsorPackages,
  reorderSponsors,
  type SponsorContractStatus,
  type SponsorListItemDto,
  type SponsorPackageListItemDto,
} from '@/api/adminSponsors'

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const sponsorPerm = useCrudPermissions('business.sponsor')
const packagePerm = useCrudPermissions('business.sponsor_package')
const club = computed(() => activeClubId.value)

const TIERS = ['主贊助', '官方贊助', '支持夥伴']
const CONTRACT_LABEL: Record<SponsorContractStatus, string> = { none: '未設定合約', active: '合約進行中', alert: '即將到期', expired: '合約已結束' }
const CONTRACT_TAG: Record<SponsorContractStatus, 'info' | 'success' | 'warning' | 'danger'> = { none: 'info', active: 'success', alert: 'warning', expired: 'danger' }

const tab = ref<'sponsors' | 'packages'>(route.query.tab === 'packages' ? 'packages' : 'sponsors')
watch(tab, (t) => router.replace({ query: { tab: t } }))

// ── 贊助商 ──
const sponsors = ref<SponsorListItemDto[]>([])
const sponsorFilter = ref({ keyword: '', tier: '', contractStatus: '' })
const sponsorFiltered = computed(() => !!(sponsorFilter.value.keyword.trim() || sponsorFilter.value.tier || sponsorFilter.value.contractStatus))
const sponsorLoading = ref(true)
const sponsorError = ref<string | null>(null)

async function loadSponsors() {
  sponsorLoading.value = true
  sponsorError.value = null
  try {
    sponsors.value = await listSponsors(club.value, {
      keyword: sponsorFilter.value.keyword.trim() || undefined,
      tier: sponsorFilter.value.tier || undefined,
      contractStatus: sponsorFilter.value.contractStatus || undefined,
    })
  } catch (error) {
    sponsors.value = []
    sponsorError.value = error instanceof AdminApiError ? error.message : '贊助商清單載入失敗，請稍後再試'
  } finally {
    sponsorLoading.value = false
  }
}

// ── 贊助方案 ──
const packages = ref<SponsorPackageListItemDto[]>([])
const packageStatus = ref('')
const packageLoading = ref(true)
const packageError = ref<string | null>(null)

async function loadPackages() {
  packageLoading.value = true
  packageError.value = null
  try {
    packages.value = await listSponsorPackages(club.value, { status: packageStatus.value || undefined })
  } catch (error) {
    packages.value = []
    packageError.value = error instanceof AdminApiError ? error.message : '贊助方案清單載入失敗，請稍後再試'
  } finally {
    packageLoading.value = false
  }
}

function loadAll() {
  loadSponsors()
  loadPackages()
}
onMounted(loadAll)
watch(club, () => {
  sponsorFilter.value = { keyword: '', tier: '', contractStatus: '' }
  packageStatus.value = ''
  loadAll()
})

function clearSponsorFilter() {
  sponsorFilter.value = { keyword: '', tier: '', contractStatus: '' }
  loadSponsors()
}

async function moveSponsor(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= sponsors.value.length) return
  const ids = sponsors.value.map((r) => r.id)
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  try {
    await reorderSponsors(club.value, ids)
    await loadSponsors()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '調整順序失敗，請稍後再試')
  }
}

async function movePackage(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= packages.value.length) return
  const ids = packages.value.map((r) => r.id)
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  try {
    await reorderSponsorPackages(club.value, ids)
    await loadPackages()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '調整順序失敗，請稍後再試')
  }
}

async function confirmDelete(message: string, title: string): Promise<boolean> {
  try {
    await ElMessageBox.confirm(message, title, { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' })
    return true
  } catch {
    return false
  }
}

async function handleDeleteSponsor(row: SponsorListItemDto) {
  if (!(await confirmDelete(`確定要刪除贊助商「${row.nameZh || '（未命名）'}」嗎？它的贊助活動、圖集與標誌圖片會一併刪除，無法復原。`, '刪除贊助商'))) return
  try {
    await deleteSponsor(club.value, row.id)
    ElMessage.success('已刪除')
    await loadSponsors()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

async function handleDeletePackage(row: SponsorPackageListItemDto) {
  if (!(await confirmDelete(`確定要刪除贊助方案「${row.nameZh || '（未命名）'}」嗎？已勾選這個方案的贊助商會一併解除勾選。`, '刪除贊助方案'))) return
  try {
    await deleteSponsorPackage(club.value, row.id)
    ElMessage.success('已刪除')
    await loadPackages()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

function priceLabel(row: SponsorPackageListItemDto): string {
  if (row.priceMin == null && row.priceMax == null) return '未設定'
  const range = row.priceMin != null && row.priceMax != null ? `${row.priceMin.toLocaleString()} ～ ${row.priceMax.toLocaleString()}` : String(row.priceMin ?? row.priceMax)
  return row.isPricePublic ? range : `${range}（不公開）`
}
</script>

<template>
  <div class="sponsorship-list">
    <PageHeader title="贊助">
      <template #meta><FrontendUnitBanner module-code="E2" /></template>
    </PageHeader>

    <el-tabs v-model="tab">
      <el-tab-pane label="贊助商" name="sponsors">
        <el-card shadow="never" class="sponsorship-list__bar">
          <div class="sponsorship-list__bar-row">
            <el-input v-model="sponsorFilter.keyword" placeholder="搜尋贊助商名稱" clearable class="sponsorship-list__keyword" @keyup.enter="loadSponsors" @clear="loadSponsors">
              <template #prefix><el-icon><Search /></el-icon></template>
            </el-input>
            <el-select v-model="sponsorFilter.tier" placeholder="贊助等級" clearable class="sponsorship-list__select" @change="loadSponsors">
              <el-option v-for="t in TIERS" :key="t" :label="t" :value="t" />
            </el-select>
            <el-select v-model="sponsorFilter.contractStatus" placeholder="合約狀況" clearable class="sponsorship-list__select" @change="loadSponsors">
              <el-option label="進行中（含即將到期）" value="active" />
              <el-option label="即將到期" value="alert" />
              <el-option label="已結束" value="expired" />
            </el-select>
            <el-button type="primary" @click="loadSponsors">篩選</el-button>
            <el-button @click="clearSponsorFilter">清除</el-button>
            <span class="sponsorship-list__spacer" />
            <el-button v-if="sponsorPerm.canCreate.value" type="primary" @click="router.push('/business/sponsorships/sponsors/new')">+ 新增贊助商</el-button>
          </div>
          <p class="sponsorship-list__hint">前台依等級（主贊助、官方贊助、支持夥伴）分區，合約已結束的贊助商不會顯示。聯絡窗口與合約日期只在後台看得到。{{ sponsorFiltered ? '篩選中無法調整順序。' : '' }}</p>
        </el-card>
        <el-card v-if="sponsorLoading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
        <el-card v-else-if="sponsorError" shadow="never"><el-empty :description="sponsorError"><el-button type="primary" @click="loadSponsors">重新載入</el-button></el-empty></el-card>
        <el-card v-else shadow="never">
          <template v-if="sponsors.length > 0">
            <el-table v-if="!isMobile" :data="sponsors" row-key="id">
              <el-table-column label="標誌" width="84">
                <template #default="{ row }">
                  <img v-if="row.logoLightThumbUrl || row.logoDarkThumbUrl" :src="(row.logoLightThumbUrl || row.logoDarkThumbUrl)!" alt="" class="sponsorship-list__logo">
                  <span v-else class="sponsorship-list__muted">—</span>
                </template>
              </el-table-column>
              <el-table-column label="名稱" min-width="160"><template #default="{ row }">{{ row.nameZh || '（未命名）' }}</template></el-table-column>
              <el-table-column label="等級" width="100"><template #default="{ row }">{{ row.tier || '—' }}</template></el-table-column>
              <el-table-column label="合約" min-width="200">
                <template #default="{ row }">
                  <el-tag :type="CONTRACT_TAG[row.contractStatus as SponsorContractStatus]" size="small">{{ CONTRACT_LABEL[row.contractStatus as SponsorContractStatus] }}</el-tag>
                  <span class="sponsorship-list__muted"> {{ row.contractEndOn ? `至 ${row.contractEndOn}` : '' }}</span>
                </template>
              </el-table-column>
              <el-table-column label="方案／活動" width="110"><template #default="{ row }">{{ row.packageCount }}／{{ row.activationCount }}</template></el-table-column>
              <el-table-column v-if="sponsorPerm.canUpdate.value" label="順序" width="96">
                <template #default="{ $index }">
                  <el-button size="small" text :disabled="sponsorFiltered || $index === 0" aria-label="上移" @click="moveSponsor($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
                  <el-button size="small" text :disabled="sponsorFiltered || $index === sponsors.length - 1" aria-label="下移" @click="moveSponsor($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="router.push(`/business/sponsorships/sponsors/${row.id}/edit`)">{{ sponsorPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="sponsorPerm.canDelete.value" size="small" text type="danger" @click="handleDeleteSponsor(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="sponsors" row-key="id">
              <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
              <template #meta="{ row }">
                <span>{{ row.tier || '未設定等級' }}</span>
                <el-tag :type="CONTRACT_TAG[row.contractStatus as SponsorContractStatus]" size="small">{{ CONTRACT_LABEL[row.contractStatus as SponsorContractStatus] }}</el-tag>
              </template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="router.push(`/business/sponsorships/sponsors/${row.id}/edit`)">{{ sponsorPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="sponsorPerm.canDelete.value" size="small" text type="danger" @click="handleDeleteSponsor(row)">刪除</el-button>
              </template>
            </MobileCardList>
          </template>
          <el-empty v-else description="目前沒有符合條件的贊助商" />
        </el-card>
      </el-tab-pane>

      <el-tab-pane label="贊助方案" name="packages">
        <el-card shadow="never" class="sponsorship-list__bar">
          <div class="sponsorship-list__bar-row">
            <el-select v-model="packageStatus" placeholder="狀態" clearable class="sponsorship-list__select" @change="loadPackages">
              <el-option label="已發布" value="published" />
              <el-option label="草稿" value="draft" />
            </el-select>
            <span class="sponsorship-list__spacer" />
            <el-button v-if="packagePerm.canCreate.value" type="primary" @click="router.push('/business/sponsorships/packages/new')">+ 新增贊助方案</el-button>
          </div>
          <p class="sponsorship-list__hint">前台「贊助方案」頁只顯示已發布的方案；價格區間只有在方案內設為公開時才會顯示。{{ packageStatus ? '篩選中無法調整順序。' : '' }}</p>
        </el-card>
        <el-card v-if="packageLoading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
        <el-card v-else-if="packageError" shadow="never"><el-empty :description="packageError"><el-button type="primary" @click="loadPackages">重新載入</el-button></el-empty></el-card>
        <el-card v-else shadow="never">
          <template v-if="packages.length > 0">
            <el-table v-if="!isMobile" :data="packages" row-key="id">
              <el-table-column label="方案名稱" min-width="180"><template #default="{ row }">{{ row.nameZh || '（未命名）' }}</template></el-table-column>
              <el-table-column label="價格區間" min-width="180"><template #default="{ row }">{{ priceLabel(row) }}</template></el-table-column>
              <el-table-column label="狀態" width="90">
                <template #default="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.status === 'published' ? '已發布' : '草稿' }}</el-tag></template>
              </el-table-column>
              <el-table-column label="贊助商數" width="100"><template #default="{ row }">{{ row.sponsorCount }}</template></el-table-column>
              <el-table-column v-if="packagePerm.canUpdate.value" label="順序" width="96">
                <template #default="{ $index }">
                  <el-button size="small" text :disabled="!!packageStatus || $index === 0" aria-label="上移" @click="movePackage($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
                  <el-button size="small" text :disabled="!!packageStatus || $index === packages.length - 1" aria-label="下移" @click="movePackage($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="router.push(`/business/sponsorships/packages/${row.id}/edit`)">{{ packagePerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="packagePerm.canDelete.value" size="small" text type="danger" @click="handleDeletePackage(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="packages" row-key="id">
              <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
              <template #meta="{ row }">
                <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.status === 'published' ? '已發布' : '草稿' }}</el-tag>
                <span>{{ priceLabel(row) }}</span>
              </template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="router.push(`/business/sponsorships/packages/${row.id}/edit`)">{{ packagePerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="packagePerm.canDelete.value" size="small" text type="danger" @click="handleDeletePackage(row)">刪除</el-button>
              </template>
            </MobileCardList>
          </template>
          <el-empty v-else description="目前沒有贊助方案" />
        </el-card>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.sponsorship-list__bar { margin-bottom: 12px; }
.sponsorship-list__bar-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.sponsorship-list__keyword { width: 220px; max-width: 100%; }
.sponsorship-list__select { width: 170px; max-width: 100%; }
.sponsorship-list__spacer { flex: 1; }
.sponsorship-list__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.sponsorship-list__logo { width: 56px; height: 36px; object-fit: contain; background: var(--admin-lightbox-neutral); border-radius: 3px; }
.sponsorship-list__muted { color: var(--admin-text-tertiary); }
</style>
