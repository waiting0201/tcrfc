<script setup lang="ts">
/**
 * 抽獎名單管理（對應前台會員中心；兩隊各自舉辦、不合辦）。
 * 🔴 系統不抽出：實體抽獎在現場或直播由人工進行，系統只負責「鎖定合格名單、配發序號」與「以序號回填中獎人」。
 * 🔴 舉辦前必須先確認「蒐集告知」：會員條款須已增列抽獎資格與遮罩公布的說明，未確認前不能產生名單。
 * 名單與中獎人視同會員個資，匯出須填用途並留下紀錄；不寄送任何中獎通知。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { deleteDraw, DRAW_STATUS_OPTIONS, eligibleCountText, getDrawNotice, listDraws, saveDrawNotice, type DrawListItemDto } from '@/api/adminDraws'
import { formatDateTime } from '@/utils/dateTime'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canView, canUpdate } = useViewUpdatePermissions('member.draw')
const club = computed(() => activeClubId.value)
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

// ── 蒐集告知 ──
const notice = ref<{ confirmed: boolean; confirmedAt?: string | null } | null>(null)
const noticeError = ref<string | null>(null)
const noticeSaving = ref(false)
async function loadNotice() {
  noticeError.value = null
  try {
    notice.value = await getDrawNotice(club.value)
  } catch (error) {
    notice.value = null
    noticeError.value = errorText(error, '蒐集告知狀態載入失敗')
  }
}
async function toggleNotice(confirmed: boolean) {
  try {
    await ElMessageBox.confirm(
      confirmed
        ? '請確認會員條款已增列：「會籍有效期間將自動列入球迷會員抽獎合格名單；中獎時，姓名將以遮罩方式於最新消息公布」。確認後才能產生抽獎名單。'
        : '取消確認後，這個俱樂部就不能再產生新的抽獎名單（已產生的不受影響）。',
      confirmed ? '確認蒐集告知' : '取消蒐集告知確認',
      { confirmButtonText: '確定', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  noticeSaving.value = true
  try {
    notice.value = await saveDrawNotice(club.value, confirmed)
    ElMessage.success('已更新')
  } catch (error) {
    ElMessage.error(errorText(error, '更新失敗，請稍後再試'))
  } finally {
    noticeSaving.value = false
  }
}

// ── 清單 ──
const filters = reactive({ keyword: '', status: '' })
const rows = ref<DrawListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
async function load() {
  if (!canView.value) {
    loading.value = false
    return
  }
  loading.value = true
  loadError.value = null
  try {
    const result = await listDraws(club.value, { keyword: filters.keyword.trim() || undefined, status: filters.status || undefined, page: page.value, pageSize: pageSize.value })
    rows.value = result.items
    total.value = result.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = errorText(error, '抽獎活動清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  load()
}
function clearFilter() {
  Object.assign(filters, { keyword: '', status: '' })
  applyFilter()
}
onMounted(() => {
  load()
  loadNotice()
})
watch(club, () => {
  Object.assign(filters, { keyword: '', status: '' })
  page.value = 1
  load()
  loadNotice()
})

function statusTag(status: string): 'success' | 'warning' | 'info' | 'danger' | undefined {
  if (status === 'announced' || status === 'closed') return 'success'
  if (status === 'drawn' || status === 'roster_locked') return 'warning'
  if (status === 'voided') return 'danger'
  return 'info'
}
async function removeDraw(row: DrawListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除活動「${row.nameZh || row.drawCode}」嗎？只有還沒產生過名單的草稿能刪除，不辦了請改用「作廢」。`, '刪除抽獎活動', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteDraw(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(errorText(error, '刪除失敗，請稍後再試'))
  }
}
const go = (row: DrawListItemDto) => router.push(`/members/lottery/${row.id}`)
</script>

<template>
  <div class="draws">
    <PageHeader title="抽獎名單管理">
      <template #meta><FrontendUnitBanner module-code="K5" /></template>
    </PageHeader>

    <el-alert class="draws__block" type="info" show-icon :closable="false" title="系統不負責抽出：實體抽獎在現場或直播由人工進行，這裡只鎖定合格名單（配發抽獎序號），並以序號回填中獎人。不會寄送中獎通知，需要聯繫中獎人請由客服電話處理。" />

    <el-card v-if="canView" shadow="never" class="draws__block">
      <div class="draws__notice">
        <div>
          <strong>蒐集告知確認</strong>
          <el-tag v-if="notice" :type="notice.confirmed ? 'success' : 'warning'" size="small" class="draws__tag">{{ notice.confirmed ? '已確認' : '尚未確認' }}</el-tag>
          <p class="draws__hint">會員條款必須已增列「會籍有效期間將自動列入球迷會員抽獎合格名單；中獎時，姓名將以遮罩方式於最新消息公布」（每個俱樂部各自確認）。<strong>尚未確認前，不能產生抽獎名單。</strong>活動辦法也要寫明：同時具備兩隊會籍的人可以分別參加兩隊的抽獎。</p>
          <p v-if="noticeError" class="draws__hint">{{ noticeError }}</p>
        </div>
        <el-button v-if="canUpdate && notice" :type="notice.confirmed ? 'default' : 'primary'" :loading="noticeSaving" @click="toggleNotice(!notice.confirmed)">{{ notice.confirmed ? '取消確認' : '我已確認條款' }}</el-button>
      </div>
    </el-card>

    <el-card v-if="!canView" shadow="never">
      <el-empty description="你的帳號只能為抽獎活動產生公布稿，沒有檢視抽獎活動清單的權限。需要處理時請請有權限的同事開啟活動頁面。" />
    </el-card>

    <el-card v-if="canView" shadow="never" class="draws__block">
      <div class="draws__row">
        <el-input v-model="filters.keyword" placeholder="搜尋活動名稱或活動代碼" clearable class="draws__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="活動狀態" clearable class="draws__select" @change="applyFilter">
          <el-option v-for="o in DRAW_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-button type="primary" @click="applyFilter">篩選</el-button>
        <el-button @click="clearFilter">清除</el-button>
        <span class="draws__spacer" />
        <el-button v-if="canUpdate" type="primary" @click="router.push('/members/lottery/new')">+ 新增抽獎活動</el-button>
      </div>
    </el-card>

    <el-card v-if="canView && loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="canView && loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else-if="canView" shadow="never">
      <el-empty v-if="rows.length === 0" description="目前沒有符合條件的抽獎活動">
        <el-button v-if="canUpdate" type="primary" @click="router.push('/members/lottery/new')">+ 新增第一個抽獎活動</el-button>
      </el-empty>
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="活動" min-width="200">
            <template #default="{ row }"><div>{{ row.nameZh || '（未命名）' }}</div><div class="draws__muted">活動代碼 {{ row.drawCode }}</div></template>
          </el-table-column>
          <el-table-column label="狀態" width="110"><template #default="{ row }"><el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
          <el-table-column label="資格基準時間" width="150"><template #default="{ row }">{{ formatDateTime(row.snapshotAt) || '未設定' }}</template></el-table-column>
          <el-table-column label="合格人數" width="100"><template #default="{ row }">{{ eligibleCountText(row.totalCount) }}</template></el-table-column>
          <el-table-column label="中獎／備取／已發放" width="150"><template #default="{ row }">{{ row.winnerCount }} ／ {{ row.backupCount }} ／ {{ row.fulfilledCount }}</template></el-table-column>
          <el-table-column label="公布" width="110"><template #default="{ row }">{{ row.announcementStatusLabel || '—' }}</template></el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="go(row)">管理</el-button>
              <el-button v-if="canUpdate && row.status === 'draft' && row.rosterVersion === 0" size="small" text type="danger" @click="removeDraw(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          <template #meta="{ row }">
            <el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag>
            <span>合格 {{ eligibleCountText(row.totalCount) }}</span><span>中獎 {{ row.winnerCount }}・備取 {{ row.backupCount }}</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="go(row)">管理</el-button>
            <el-button v-if="canUpdate && row.status === 'draft' && row.rosterVersion === 0" size="small" text type="danger" @click="removeDraw(row)">刪除</el-button>
          </template>
        </MobileCardList>
        <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="draws__pager" @current-change="load" @size-change="applyFilter" />
      </template>
    </el-card>
  </div>
</template>

<style scoped>
.draws__block { margin-bottom: 12px; }
.draws__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.draws__keyword { width: 240px; max-width: 100%; }
.draws__select { width: 150px; max-width: 100%; }
.draws__spacer { flex: 1; }
.draws__notice { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 12px; }
.draws__tag { margin-left: 8px; }
.draws__hint { margin: 6px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.draws__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.draws__pager { margin-top: 12px; justify-content: flex-end; }
</style>
