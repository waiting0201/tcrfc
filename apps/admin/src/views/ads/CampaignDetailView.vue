<script setup lang="ts">
/**
 * 投放檔期詳情：基本資料、投放進度、狀態操作（送審／核可／退回／緊急暫停／恢復／結案／作廢）、
 * 素材與審核、同版位的檔期衝突檢視。
 * 🔴 素材未通過審核的檔期不會開始投放：核可需要至少一個「已通過」的素材；恢復投放需要「已通過且未暫停」的素材。
 * 🔴 合約金額沒有檢視權限時顯示「不公開」。
 * 🔴 「已排程 → 投放中 → 已結束」由起訖時間自動推進（最多延遲約 30 秒），不用手動操作。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import CampaignFormDialog from './parts/CampaignFormDialog.vue'
import CreativeFormDialog from './parts/CreativeFormDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import {
  deleteCampaign,
  deleteCreative,
  getCampaign,
  getSlotSchedule,
  listAdSlots,
  runCampaignAction,
  runCreativeAction,
  type AdSlotDto,
  type CampaignAction,
  type CampaignDetailDto,
  type CreativeDto,
  type SlotScheduleDto,
} from '@/api/adminAds'
import { formatDateTime } from '@/utils/dateTime'
import { CAMPAIGN_ACTION_LABEL, campaignStatusTag, reviewTag } from './adLabels'

const props = defineProps<{ id: string }>()
const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canUpdate = usePermission('ad.campaign.update')
const canDelete = usePermission('ad.campaign.delete')
const canReview = usePermission('ad.campaign.review')
const canPause = usePermission('ad.campaign.pause')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const campaignId = computed(() => (route.params.id as string | undefined) ?? props.id)
const detail = ref<CampaignDetailDto | null>(null)
const slot = ref<AdSlotDto | null>(null)
const state = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadError = ref('')
const acting = ref(false)

async function load() {
  state.value = 'loading'
  try {
    detail.value = await getCampaign(campaignId.value)
    state.value = 'ready'
    const slots = await listAdSlots().catch(() => [] as AdSlotDto[])
    slot.value = slots.find((s) => s.id === detail.value!.slotId) ?? null
    loadSchedule()
  } catch (e) {
    if (e instanceof AdminApiError && e.kind === 'not-found') state.value = 'not-found'
    else {
      loadError.value = errText(e, '檔期資料載入失敗，請稍後再試')
      state.value = 'error'
    }
  }
}
onMounted(load)
watch(campaignId, load)

// ── 狀態動作 ──
const ACTION_TYPE: Record<string, 'primary' | 'success' | 'warning' | 'danger' | 'default'> = {
  submit: 'primary', approve: 'success', return: 'default', pause: 'danger', resume: 'success', close: 'default', void: 'danger',
}
const ACTION_HINT: Record<string, string> = {
  submit: '送審後檔期進入「待審核」，審核期間不能修改內容。',
  approve: '核可後檔期進入「已排程」；開始時間已過的會直接開始投放。需要至少一個已通過審核的素材。',
  return: '退回後檔期回到草稿，可以修改後重新送審。',
  resume: '恢復後會回到暫停前的狀態；需要至少一個已通過且未暫停的素材。',
  close: '結案後不能再修改，成效報表會保留。',
}
async function doAction(action: CampaignAction) {
  const d = detail.value
  if (!d) return
  let body: { reason: string } | undefined
  try {
    if (action === 'pause' || action === 'void') {
      const title = action === 'pause' ? '緊急暫停' : '作廢檔期'
      const message = action === 'pause' ? '暫停會立刻停止投放。請寫下暫停原因（必填）：' : '作廢無法復原。請寫下作廢原因（必填）：'
      const { value } = await ElMessageBox.prompt(message, title, {
        confirmButtonText: '確定', cancelButtonText: '取消', inputType: 'textarea', inputValidator: (v: string) => (!!v && v.trim().length > 0) || '請填寫原因',
        confirmButtonClass: 'el-button--danger',
      })
      body = { reason: value.trim() }
    } else {
      await ElMessageBox.confirm(ACTION_HINT[action] ?? '確定要執行嗎？', CAMPAIGN_ACTION_LABEL[action], { confirmButtonText: '確定', cancelButtonText: '取消' })
    }
  } catch {
    return
  }
  acting.value = true
  try {
    detail.value = await runCampaignAction(d.id, action, body)
    ElMessage.success('已完成')
    loadSchedule()
  } catch (e) {
    ElMessage.error(errText(e, '操作失敗，請稍後再試'))
  } finally {
    acting.value = false
  }
}
/** 有沒有權限做這個動作（後端的 availableActions 已依角色過濾，這裡是防呆）。 */
const visibleActions = computed(() => (detail.value?.availableActions ?? []).filter((a) => {
  if (a === 'approve' || a === 'return') return canReview.value
  if (a === 'pause' || a === 'resume') return canPause.value
  return canUpdate.value
}))

const editVisible = ref(false)
const canEdit = computed(() => canUpdate.value && !!detail.value && ['draft', 'scheduled', 'running', 'paused'].includes(detail.value.status))
async function removeCampaign() {
  try {
    await ElMessageBox.confirm('只有草稿能刪除。確定要刪除這個檔期嗎？不辦了的檔期請改用「作廢」。', '刪除檔期', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteCampaign(campaignId.value)
    ElMessage.success('已刪除')
    router.replace('/business/campaigns')
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}

// ── 素材 ──
const creativeVisible = ref(false)
const editingCreative = ref<CreativeDto | null>(null)
function openCreative(c: CreativeDto | null) {
  editingCreative.value = c
  creativeVisible.value = true
}
async function creativeAction(c: CreativeDto, action: 'approve' | 'reject' | 'pause' | 'resume') {
  let body: { reason: string } | undefined
  try {
    if (action === 'reject') {
      const { value } = await ElMessageBox.prompt('請寫下退回原因（必填），會顯示給素材提供者：', '退回素材', {
        confirmButtonText: '退回', cancelButtonText: '取消', inputType: 'textarea', inputValidator: (v: string) => (!!v && v.trim().length > 0) || '請填寫原因',
      })
      body = { reason: value.trim() }
    } else if (action === 'pause') {
      const { value } = await ElMessageBox.prompt('暫停後這個素材會立刻停止出現。請寫下原因（必填）：', '暫停素材', {
        confirmButtonText: '暫停', cancelButtonText: '取消', inputType: 'textarea', inputValidator: (v: string) => (!!v && v.trim().length > 0) || '請填寫原因',
        confirmButtonClass: 'el-button--danger',
      })
      body = { reason: value.trim() }
    }
  } catch {
    return
  }
  try {
    await runCreativeAction(c.id, action, body)
    ElMessage.success('已完成')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '操作失敗，請稍後再試'))
  }
}
async function removeCreative(c: CreativeDto) {
  try {
    await ElMessageBox.confirm('只有草稿或待審核的檔期能刪除素材；之後只能暫停（成效要留著對帳）。確定要刪除嗎？', '刪除素材', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteCreative(c.id)
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}
const canEditCreatives = computed(() => canUpdate.value && !!detail.value && !['ended', 'closed', 'voided'].includes(detail.value.status))
const canDeleteCreatives = computed(() => canUpdate.value && !!detail.value && ['draft', 'pending_review'].includes(detail.value.status))

// ── 衝突檢視 ──
const schedule = ref<SlotScheduleDto | null>(null)
const scheduleError = ref<string | null>(null)
async function loadSchedule() {
  const d = detail.value
  if (!d) return
  scheduleError.value = null
  try {
    schedule.value = await getSlotSchedule(d.slotId)
  } catch (e) {
    schedule.value = null
    scheduleError.value = errText(e, '衝突檢視載入失敗')
  }
}

const pacingTag = (s: string) => (s === 'behind' ? 'danger' : s === 'ahead' ? 'success' : 'primary')
const progressPercent = computed(() => {
  const p = detail.value?.pacing
  return p && p.goalImpressions > 0 ? Math.min(100, Math.round((p.delivered / p.goalImpressions) * 100)) : 0
})
</script>

<template>
  <div class="cd">
    <PageHeader :title="detail ? `檔期：${detail.name}` : '投放檔期'">
      <template #back><el-button text @click="router.push('/business/campaigns')">← 回檔期清單</el-button></template>
      <template #meta><FrontendUnitBanner module-code="E5" /></template>
    </PageHeader>

    <el-card v-if="state === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="state === 'not-found'" shadow="never"><el-empty description="找不到這個檔期"><el-button type="primary" @click="router.push('/business/campaigns')">回檔期清單</el-button></el-empty></el-card>
    <el-card v-else-if="state === 'error'" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>

    <template v-else-if="detail">
      <el-card shadow="never" class="cd__block">
        <div class="cd__head">
          <div>
            <el-tag :type="campaignStatusTag(detail.status)">{{ detail.statusLabel }}</el-tag>
            <span class="cd__muted">&emsp;{{ detail.advertiserName }}・{{ detail.slotName || detail.slotCode }}</span>
          </div>
          <div class="cd__actions">
            <el-button v-for="a in visibleActions" :key="a" :type="ACTION_TYPE[a]" :plain="ACTION_TYPE[a] === 'danger'" :loading="acting" @click="doAction(a)">{{ CAMPAIGN_ACTION_LABEL[a] }}</el-button>
            <el-button v-if="canEdit" @click="editVisible = true">編輯</el-button>
            <el-button v-if="canDelete && detail.status === 'draft'" type="danger" plain @click="removeCampaign">刪除</el-button>
          </div>
        </div>
        <el-alert v-if="detail.pauseReason" class="cd__alert" type="warning" show-icon :closable="false" :title="`暫停或作廢原因：${detail.pauseReason}`" />
        <el-descriptions :column="isMobile ? 1 : 2" border class="cd__desc">
          <el-descriptions-item label="投放期間（台灣時間）">{{ formatDateTime(detail.startsAt) }} ～ {{ formatDateTime(detail.endsAt) }}</el-descriptions-item>
          <el-descriptions-item label="投放權重">{{ detail.weight }}</el-descriptions-item>
          <el-descriptions-item label="投放目標">{{ detail.goalTypeLabel }}<template v-if="detail.goalImpressions">（目標曝光 {{ detail.goalImpressions.toLocaleString() }} 次）</template></el-descriptions-item>
          <el-descriptions-item label="累計曝光／今日">{{ detail.deliveredTotal.toLocaleString() }} 次／{{ detail.deliveredToday.toLocaleString() }} 次</el-descriptions-item>
          <el-descriptions-item label="每日曝光上限">{{ detail.dailyImpressionCap ? `${detail.dailyImpressionCap.toLocaleString()} 次` : '不限' }}</el-descriptions-item>
          <el-descriptions-item label="每人每日曝光上限">{{ detail.perDeviceDailyCap ? `${detail.perDeviceDailyCap} 次` : '不限' }}</el-descriptions-item>
          <el-descriptions-item label="合約金額">{{ detail.contractAmountLabel }}<el-tag v-if="detail.isAmountHidden" size="small" type="info" class="cd__tag">標示為不公開</el-tag></el-descriptions-item>
          <el-descriptions-item label="審核">{{ detail.reviewedBy ? `${detail.reviewedByName || '已審核'}（${formatDateTime(detail.reviewedAt)}）` : '尚未審核' }}</el-descriptions-item>
        </el-descriptions>
      </el-card>

      <el-card v-if="detail.pacing" shadow="never" class="cd__block" header="曝光保證的投放進度">
        <el-progress :percentage="progressPercent" :status="detail.pacing.status === 'behind' ? 'exception' : undefined" />
        <p class="cd__p">
          已曝光 {{ detail.pacing.delivered.toLocaleString() }} 次，依進度到現在應該達到 {{ detail.pacing.expectedByNow.toLocaleString() }} 次，每日目標約 {{ detail.pacing.dailyTarget.toLocaleString() }} 次。
          <el-tag :type="pacingTag(detail.pacing.status)" size="small">{{ detail.pacing.statusLabel }}</el-tag>
        </p>
      </el-card>

      <el-card shadow="never" class="cd__block">
        <template #header>
          <div class="cd__card-head"><span>廣告素材（{{ detail.creatives.length }}）</span><el-button v-if="canEditCreatives" size="small" type="primary" @click="openCreative(null)">+ 新增素材</el-button></div>
        </template>
        <p class="cd__hint">素材要通過審核才會投放；素材內容被修改後會回到待審核。緊急狀況可以單獨暫停某個素材。</p>
        <el-empty v-if="detail.creatives.length === 0" description="還沒有素材，送審前至少要有一個" :image-size="64" />
        <template v-else>
          <el-table v-if="!isMobile" :data="detail.creatives" row-key="id">
            <el-table-column label="素材" min-width="240">
              <template #default="{ row }">
                <div class="cd__creative">
                  <img v-if="row.imageThumbUrl || row.imageUrl" :src="row.imageThumbUrl || row.imageUrl" :alt="row.altText" class="cd__thumb">
                  <div>{{ row.title || row.altText }}<div class="cd__muted">{{ row.locale === 'zh' ? '中文版' : '英文版' }}<template v-if="row.variantTag">・{{ row.variantTag }} 版</template><template v-if="row.videoKey">・含影片</template></div></div>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="前往連結" min-width="160"><template #default="{ row }"><span class="cd__break">{{ row.clickUrl || '—' }}</span></template></el-table-column>
            <el-table-column label="審核" width="150">
              <template #default="{ row }">
                <el-tag :type="reviewTag(row.reviewStatus)" size="small">{{ row.reviewStatusLabel || row.reviewStatus }}</el-tag>
                <el-tag v-if="row.isPaused" type="danger" size="small" class="cd__tag">已暫停</el-tag>
                <div v-if="row.rejectReason" class="cd__muted">{{ row.rejectReason }}</div>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="260" fixed="right">
              <template #default="{ row }">
                <template v-if="canReview && row.reviewStatus === 'pending'">
                  <el-button size="small" text type="success" @click="creativeAction(row, 'approve')">通過</el-button>
                  <el-button size="small" text type="danger" @click="creativeAction(row, 'reject')">退回</el-button>
                </template>
                <template v-if="canPause">
                  <el-button v-if="!row.isPaused" size="small" text type="warning" @click="creativeAction(row, 'pause')">暫停</el-button>
                  <el-button v-else size="small" text type="primary" @click="creativeAction(row, 'resume')">恢復</el-button>
                </template>
                <el-button v-if="canEditCreatives" size="small" text type="primary" @click="openCreative(row)">編輯</el-button>
                <el-button v-if="canDeleteCreatives" size="small" text type="danger" @click="removeCreative(row)">刪除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <MobileCardList v-else :rows="detail.creatives" row-key="id">
            <template #title="{ row }">{{ row.title || row.altText }}</template>
            <template #meta="{ row }">
              <el-tag :type="reviewTag(row.reviewStatus)" size="small">{{ row.reviewStatusLabel || row.reviewStatus }}</el-tag>
              <el-tag v-if="row.isPaused" type="danger" size="small">已暫停</el-tag>
              <span>{{ row.locale === 'zh' ? '中文版' : '英文版' }}</span><span v-if="row.rejectReason">{{ row.rejectReason }}</span>
            </template>
            <template #actions="{ row }">
              <template v-if="canReview && row.reviewStatus === 'pending'">
                <el-button size="small" text type="success" @click="creativeAction(row, 'approve')">通過</el-button>
                <el-button size="small" text type="danger" @click="creativeAction(row, 'reject')">退回</el-button>
              </template>
              <template v-if="canPause">
                <el-button v-if="!row.isPaused" size="small" text type="warning" @click="creativeAction(row, 'pause')">暫停</el-button>
                <el-button v-else size="small" text type="primary" @click="creativeAction(row, 'resume')">恢復</el-button>
              </template>
              <el-button v-if="canEditCreatives" size="small" text type="primary" @click="openCreative(row)">編輯</el-button>
              <el-button v-if="canDeleteCreatives" size="small" text type="danger" @click="removeCreative(row)">刪除</el-button>
            </template>
          </MobileCardList>
        </template>
      </el-card>

      <el-card shadow="never" class="cd__block" header="同版位的檔期衝突檢視">
        <p class="cd__hint">列出這個版位「從現在起 30 天內」待審核、已排程、投放中與已暫停的檔期。同時段檔期超過版位的輪播張數上限只是提醒，不會擋存檔。</p>
        <el-alert v-if="scheduleError" type="warning" show-icon :closable="false" :title="scheduleError" />
        <template v-else-if="schedule">
          <el-alert v-if="schedule.exceedsRotationCap" type="warning" show-icon :closable="false" class="cd__alert" :title="`期間內最多同時有 ${schedule.maxConcurrent} 個檔期，超過這個版位同時輪播 ${schedule.rotationCap} 張的上限，部分檔期會分不到曝光。`" />
          <p v-else class="cd__p">期間內最多同時有 {{ schedule.maxConcurrent }} 個檔期（版位可同時輪播 {{ schedule.rotationCap }} 張）。</p>
          <el-empty v-if="schedule.items.length === 0" description="這段期間沒有其他檔期" :image-size="64" />
          <el-table v-else-if="!isMobile" :data="schedule.items" row-key="campaignId">
            <el-table-column label="檔期" min-width="200"><template #default="{ row }"><el-link :underline="false" type="primary" @click="router.push(`/business/campaigns/${row.campaignId}`)">{{ row.name }}</el-link><div class="cd__muted">{{ row.advertiserName }}</div></template></el-table-column>
            <el-table-column label="期間（台灣時間）" min-width="220"><template #default="{ row }">{{ formatDateTime(row.startsAt) }} ～ {{ formatDateTime(row.endsAt) }}</template></el-table-column>
            <el-table-column label="狀態" width="100" prop="statusLabel" />
            <el-table-column label="權重" width="80" prop="weight" />
            <el-table-column label="約佔曝光" width="100"><template #default="{ row }">{{ row.weightSharePercent }}%</template></el-table-column>
          </el-table>
          <MobileCardList v-else :rows="schedule.items" row-key="campaignId">
            <template #title="{ row }">{{ row.name }}</template>
            <template #meta="{ row }"><span>{{ row.statusLabel }}</span><span>權重 {{ row.weight }}（約 {{ row.weightSharePercent }}%）</span><span>{{ formatDateTime(row.startsAt) }} ～ {{ formatDateTime(row.endsAt) }}</span></template>
            <template #actions="{ row }"><el-button size="small" text type="primary" @click="router.push(`/business/campaigns/${row.campaignId}`)">查看</el-button></template>
          </MobileCardList>
        </template>
      </el-card>
    </template>

    <CampaignFormDialog v-model="editVisible" :campaign="detail" @saved="load" />
    <CreativeFormDialog v-model="creativeVisible" :campaign-id="campaignId" :creative="editingCreative" :ad-slot="slot" @saved="load" />
  </div>
</template>

<style scoped>
.cd { min-width: 0; }
.cd__block { margin-bottom: 12px; }
.cd__head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; }
.cd__actions { display: flex; flex-wrap: wrap; gap: 8px; }
.cd__alert { margin: 10px 0; }
.cd__desc { margin-top: 12px; }
.cd__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.cd__hint { margin: 0 0 10px; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.cd__p { margin: 8px 0 0; font-size: 13px; line-height: 1.7; }
.cd__tag { margin-left: 6px; }
.cd__card-head { display: flex; align-items: center; justify-content: space-between; }
.cd__creative { display: flex; align-items: center; gap: 10px; }
.cd__thumb { width: 96px; height: 54px; object-fit: cover; border-radius: 4px; border: 1px solid var(--admin-border); flex: none; }
.cd__break { word-break: break-all; }
</style>
