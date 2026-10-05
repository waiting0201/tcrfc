<script setup lang="ts">
/**
 * 抽獎活動管理：名單、中獎人與備取、獎品發放、公布與結案。
 * 狀態流程：草稿 → 名單已鎖定 → 已抽出 → 已公布 → 已結案；另有「已作廢」（結案前任何時候）。
 * 按鈕依系統回報的「目前可執行的動作」顯示。名單與中獎人視同會員個資，所有匯出都須填用途並留下紀錄。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import DrawRosterPanel from './parts/DrawRosterPanel.vue'
import DrawWinnersPanel from './parts/DrawWinnersPanel.vue'
import DrawFulfilmentPanel from './parts/DrawFulfilmentPanel.vue'
import DrawAnnouncePanel from './parts/DrawAnnouncePanel.vue'
import { useViewUpdatePermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { closeDraw, eligibleCountText, getDraw, voidDraw, type DrawDetailDto } from '@/api/adminDraws'
import { formatDateTime } from '@/utils/dateTime'

const route = useRoute()
const router = useRouter()
const drawId = computed(() => route.params.id as string)
const { canView, canUpdate } = useViewUpdatePermissions('member.draw')
const canAnnounce = usePermission('member.draw.announce')
const club = computed(() => activeClubId.value)

const draw = ref<DrawDetailDto | null>(null)
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const tab = ref<'roster' | 'winners' | 'fulfilment' | 'announce'>('roster')

async function load(silent = false) {
  if (!silent) loadState.value = 'loading'
  try {
    draw.value = await getDraw(club.value, drawId.value)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') loadState.value = 'not-found'
    else if (!silent || !draw.value) {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '活動資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
onMounted(() => load())
watch(club, () => router.push('/members/lottery'))
watch(drawId, () => load())

const can = (a: string) => !!draw.value?.availableActions.includes(a as never)
function statusTag(status: string): 'success' | 'warning' | 'info' | 'danger' | undefined {
  if (status === 'announced' || status === 'closed') return 'success'
  if (status === 'drawn' || status === 'roster_locked') return 'warning'
  if (status === 'voided') return 'danger'
  return 'info'
}

const acting = ref(false)
async function doClose() {
  try {
    await ElMessageBox.confirm('結案後這個活動變成唯讀，不能再修改中獎名單與發放狀態。確定要結案嗎？', '結案', { confirmButtonText: '確定結案', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  acting.value = true
  try {
    await closeDraw(club.value, drawId.value)
    ElMessage.success('已結案')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '結案失敗，請稍後再試')
  } finally {
    acting.value = false
    await load(true)
  }
}

const voidOpen = ref(false)
const voidReason = ref('')
const voidError = ref<string | null>(null)
async function doVoid() {
  if (!voidReason.value.trim()) return (voidError.value = '作廢必須填寫原因')
  voidError.value = null
  acting.value = true
  try {
    await voidDraw(club.value, drawId.value, voidReason.value.trim())
    voidOpen.value = false
    ElMessage.success('已作廢')
  } catch (error) {
    voidError.value = error instanceof AdminApiError ? error.message : '作廢失敗，請稍後再試'
  } finally {
    acting.value = false
    await load(true)
  }
}
</script>

<template>
  <div class="draw-detail">
    <PageHeader :title="draw ? `抽獎活動：${draw.nameZh || draw.drawCode}` : '抽獎活動'">
      <template #back><el-button text @click="router.push('/members/lottery')"><el-icon><ArrowLeft /></el-icon>返回清單</el-button></template>
      <template #meta><FrontendUnitBanner module-code="K5" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready' || !draw" shadow="never">
      <el-empty :image-size="96" :description="loadState === 'not-found' ? '找不到這個抽獎活動，可能不屬於目前選擇的俱樂部。' : loadErrorMessage">
        <el-button v-if="loadState === 'error'" type="primary" @click="load()">重新載入</el-button>
        <el-button v-else type="primary" @click="router.push('/members/lottery')">返回清單</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-card shadow="never" class="draw-detail__block">
        <div class="draw-detail__head">
          <div>
            <el-tag :type="statusTag(draw.status)" size="large">{{ draw.statusLabel }}</el-tag>
            <span class="draw-detail__code">活動代碼 {{ draw.drawCode }}</span>
            <div class="draw-detail__muted">
              資格基準時間 {{ formatDateTime(draw.snapshotAt) || '未設定' }}・開獎時間 {{ formatDateTime(draw.drawnAt) || '未設定' }}<template v-if="draw.claimDeadlineOn">・領獎期限 {{ draw.claimDeadlineOn }}</template>
            </div>
            <div class="draw-detail__muted">合格 {{ eligibleCountText(draw.totalCount) }}・中獎 {{ draw.winnerCount }}・備取 {{ draw.backupCount }}・已發放 {{ draw.fulfilledCount }}</div>
          </div>
          <div class="draw-detail__actions">
            <el-button v-if="canUpdate && can('edit')" @click="router.push(`/members/lottery/${draw.id}/edit`)">編輯活動內容</el-button>
            <el-button v-if="canUpdate && can('close')" type="primary" :loading="acting" @click="doClose">結案</el-button>
            <el-button v-if="canUpdate && can('void')" type="danger" plain @click="voidReason = ''; voidError = null; voidOpen = true">作廢活動</el-button>
          </div>
        </div>
        <p v-if="draw.status === 'voided'" class="draw-detail__hint">這個活動已作廢，資料保留但不能再修改（原因記在內部備註）。</p>
      </el-card>

      <el-tabs v-model="tab">
        <el-tab-pane v-if="canView" label="名單" name="roster" lazy><DrawRosterPanel :draw="draw" @changed="load(true)" /></el-tab-pane>
        <el-tab-pane v-if="canView" label="中獎人與備取" name="winners" lazy><DrawWinnersPanel :draw="draw" @changed="load(true)" /></el-tab-pane>
        <el-tab-pane v-if="canView" label="獎品發放" name="fulfilment" lazy><DrawFulfilmentPanel :draw="draw" @changed="load(true)" /></el-tab-pane>
        <el-tab-pane v-if="canView || canAnnounce" label="公布" name="announce" lazy><DrawAnnouncePanel :draw="draw" @changed="load(true)" /></el-tab-pane>
      </el-tabs>
    </template>

    <el-dialog v-model="voidOpen" title="作廢活動" width="440px" :close-on-click-modal="false" class="draw-detail__dialog">
      <el-alert v-if="voidError" :title="voidError" type="warning" show-icon class="draw-detail__block" @close="voidError = null" />
      <p class="draw-detail__hint">作廢會保留所有資料、不能刪除，結案前任何時候都可以作廢。不辦了請用作廢，不要刪除。</p>
      <el-input v-model="voidReason" type="textarea" :rows="3" maxlength="200" show-word-limit placeholder="作廢原因（必填）" />
      <template #footer>
        <el-button @click="voidOpen = false">取消</el-button>
        <el-button type="danger" :loading="acting" @click="doVoid">確定作廢</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.draw-detail { max-width: 1040px; margin: 0 auto 32px; }
.draw-detail__block { margin-bottom: 16px; }
.draw-detail__head { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 12px; }
.draw-detail__actions { display: flex; flex-wrap: wrap; gap: 8px; }
.draw-detail__code { margin-left: 10px; font-size: 13px; color: var(--admin-text-secondary); }
.draw-detail__muted { margin-top: 4px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.draw-detail__hint { margin: 8px 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
