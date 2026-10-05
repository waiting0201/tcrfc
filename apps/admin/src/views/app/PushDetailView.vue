<script setup lang="ts">
/**
 * 推播批次詳情：內容預覽（中英各一）、發送對象、送出／送達／開啟統計，以及送審、覆核、取消、重送與試送。
 * 🔴 雙人覆核：核可者不能是建立者本人。核可時要帶入你在畫面上看到的預估人數，人數在核可前有變動會被擋下，需重新確認。
 * 🔴 推播傳輸尚未串接：核可後批次停在「失敗」，不動任何裝置；串接後按「重送」，已處理的裝置不會重送。
 * 🔴 三個統計數字的意思：「送出」是我方交給推播服務的則數；「送達」是推播服務已接受且沒有回報權杖失效，不等於已到達使用者手機；
 *    「開啟」是 App 回報的次數，系統不追蹤是哪一位使用者開啟。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import {
  approvePush, cancelPush, deletePushMessage, estimatePush, getPushMessage, previewPush, retryPush, returnPush, submitPush, testSendPush,
  type PushAction, type PushDetailDto, type PushPreviewDto, type PushTestSendDto,
} from '@/api/adminApp'
import { formatDateTime } from '@/utils/dateTime'
import { PUSH_ACTION_LABEL, PUSH_NOT_CONNECTED_TEXT, audienceTierText, pushStatusTag } from './pushLabels'

const props = defineProps<{ id: string }>()
const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canCreate = usePermission('app.push.create')
const canApprove = usePermission('app.push.approve')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const messageId = computed(() => (route.params.id as string | undefined) ?? props.id)
const detail = ref<PushDetailDto | null>(null)
const preview = ref<PushPreviewDto | null>(null)
const state = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadError = ref('')
const acting = ref(false)

async function load() {
  state.value = 'loading'
  try {
    detail.value = await getPushMessage(messageId.value)
    state.value = 'ready'
    preview.value = await previewPush(messageId.value).catch(() => null)
  } catch (e) {
    if (e instanceof AdminApiError && e.kind === 'not-found') state.value = 'not-found'
    else {
      loadError.value = errText(e, '推播資料載入失敗，請稍後再試')
      state.value = 'error'
    }
  }
}
onMounted(load)
watch(messageId, load)

const CREATOR_ACTIONS: PushAction[] = ['submit', 'cancel']
const actions = computed(() => (detail.value?.availableActions ?? []).filter((a) => (CREATOR_ACTIONS.includes(a) ? canCreate.value : canApprove.value)))
const ACTION_TYPE: Record<string, 'primary' | 'success' | 'danger' | 'default'> = { submit: 'primary', approve: 'success', return: 'default', cancel: 'danger', retry: 'primary' }

async function run(action: PushAction) {
  const d = detail.value
  if (!d) return
  acting.value = true
  try {
    if (action === 'approve') {
      // 二次確認：先重新試算目前分眾的人數，讓操作者看到實際會發給多少台裝置
      const est = await estimatePush({ audienceTier: d.audienceTier, audienceClubCode: d.audienceClubCode, audienceTeamCodes: d.audienceTeamCodes })
      try {
        await ElMessageBox.confirm(
          `這則推播預估會發給 ${est.total.toLocaleString()} 台裝置。核可者不能是建立者本人。確定核可${d.scheduledAt ? `（預定 ${formatDateTime(d.scheduledAt)} 發送）` : '並立刻發送'}嗎？`,
          '覆核並發送',
          { confirmButtonText: '核可', cancelButtonText: '取消', type: 'warning' },
        )
      } catch {
        return
      }
      detail.value = await approvePush(d.id, est.total)
    } else if (action === 'return') {
      let note = ''
      try {
        const r = await ElMessageBox.prompt('請寫下退回的原因（必填），建立者會看到：', '退回修改', { confirmButtonText: '退回', cancelButtonText: '取消', inputType: 'textarea', inputValidator: (v: string) => (!!v && v.trim().length > 0) || '請填寫原因' })
        note = r.value.trim()
      } catch {
        return
      }
      detail.value = await returnPush(d.id, note)
    } else if (action === 'cancel') {
      try {
        await ElMessageBox.confirm('取消後這則推播不會再發送。確定要取消嗎？', '取消推播', { confirmButtonText: '取消推播', cancelButtonText: '先不要', confirmButtonClass: 'el-button--danger', type: 'warning' })
      } catch {
        return
      }
      detail.value = await cancelPush(d.id)
    } else if (action === 'retry') {
      detail.value = await retryPush(d.id)
    } else {
      detail.value = await submitPush(d.id)
    }
    ElMessage.success('已完成')
    preview.value = await previewPush(d.id).catch(() => null)
  } catch (e) {
    ElMessage.error(errText(e, '操作失敗，請稍後再試'))
  } finally {
    acting.value = false
  }
}

async function remove() {
  try {
    await ElMessageBox.confirm('只有草稿或已取消的推播能刪除。確定要刪除嗎？', '刪除推播', { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' })
  } catch {
    return
  }
  try {
    await deletePushMessage(messageId.value)
    ElMessage.success('已刪除')
    router.replace('/app/push')
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}

// ── 試送 ──
const testVisible = ref(false)
const testIds = ref('')
const testing = ref(false)
const testResult = ref<PushTestSendDto | null>(null)
const testError = ref<string | null>(null)
async function doTest() {
  const ids = testIds.value.split(/[\s,，]+/).filter(Boolean)
  if (ids.length < 1 || ids.length > 10) return void (testError.value = '請填 1 到 10 台測試裝置的識別碼，一行一個')
  testing.value = true
  testError.value = null
  try {
    testResult.value = await testSendPush(messageId.value, ids)
  } catch (e) {
    testError.value = errText(e, '試送失敗，請稍後再試')
  } finally {
    testing.value = false
  }
}
function openTest() {
  testIds.value = ''
  testResult.value = null
  testError.value = null
  testVisible.value = true
}
const canEditDraft = computed(() => canCreate.value && detail.value?.status === 'draft')
const canDelete = computed(() => canCreate.value && (detail.value?.status === 'draft' || detail.value?.status === 'cancelled'))
</script>

<template>
  <div class="pd">
    <PageHeader :title="detail ? `推播：${detail.content.zh.title}` : '推播'">
      <template #back><el-button text @click="router.push('/app/push')">← 回推播清單</el-button></template>
      <template #meta><FrontendUnitBanner module-code="M3" /></template>
    </PageHeader>

    <el-card v-if="state === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="state === 'not-found'" shadow="never"><el-empty description="找不到這則推播"><el-button type="primary" @click="router.push('/app/push')">回推播清單</el-button></el-empty></el-card>
    <el-card v-else-if="state === 'error'" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>

    <template v-else-if="detail">
      <el-alert v-if="detail.status === 'failed'" type="error" show-icon :closable="false" class="pd__block" title="這個批次發送失敗">
        {{ detail.failureMessage || PUSH_NOT_CONNECTED_TEXT }}
        <template v-if="canApprove">問題排除或串接完成後，可以按「重送」，已處理過的裝置不會重複收到。</template>
      </el-alert>
      <el-alert v-else-if="detail.status === 'partial'" type="warning" show-icon :closable="false" class="pd__block" title="這個批次只有部分送出">{{ detail.failureMessage }}可以按「重送」從中斷的地方續送。</el-alert>
      <el-alert v-if="detail.rejectNote && detail.status === 'draft'" type="warning" show-icon :closable="false" class="pd__block" :title="`覆核者退回原因：${detail.rejectNote}`" />

      <el-card shadow="never" class="pd__block">
        <div class="pd__head">
          <div><el-tag :type="pushStatusTag(detail.status)">{{ detail.statusLabel }}</el-tag><span class="pd__muted">&emsp;建立者 {{ detail.createdByName || '—' }}<template v-if="detail.reviewedBy">・覆核者 {{ detail.reviewedByName || '—' }}（{{ formatDateTime(detail.reviewedAt) }}）</template></span></div>
          <div class="pd__actions">
            <el-button v-for="a in actions" :key="a" :type="ACTION_TYPE[a]" :plain="a === 'cancel'" :loading="acting" @click="run(a)">{{ PUSH_ACTION_LABEL[a] }}</el-button>
            <el-button v-if="canEditDraft" @click="router.push(`/app/push/${detail.id}/edit`)">編輯</el-button>
            <el-button v-if="canCreate" @click="openTest">試送到測試裝置</el-button>
            <el-button v-if="canDelete" type="danger" plain @click="remove">刪除</el-button>
          </div>
        </div>
        <el-descriptions :column="isMobile ? 1 : 2" border class="pd__desc">
          <el-descriptions-item label="發送對象">{{ audienceTierText(detail.audienceTier) }}<template v-if="detail.audienceClubCode">・限定俱樂部</template><template v-if="detail.audienceTeamCodes.length">・追蹤指定球隊 {{ detail.audienceTeamCodes.length }} 支</template></el-descriptions-item>
          <el-descriptions-item label="預估觸及">{{ detail.audienceEstimate != null ? `${detail.audienceEstimate.toLocaleString()} 台裝置` : '尚未試算' }}</el-descriptions-item>
          <el-descriptions-item label="預定發送（台灣時間）">{{ formatDateTime(detail.scheduledAt) || '核可後立刻發送' }}</el-descriptions-item>
          <el-descriptions-item label="實際發送（台灣時間）">{{ formatDateTime(detail.sentAt) || '尚未發送' }}</el-descriptions-item>
          <el-descriptions-item label="點擊後前往">{{ detail.deepLink || '—' }}</el-descriptions-item>
        </el-descriptions>
      </el-card>

      <el-card shadow="never" class="pd__block" header="內容預覽">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12">
            <div class="pd__phone"><div class="pd__label">中文版</div><strong>{{ (preview?.zh ?? detail.content.zh).title }}</strong><p>{{ (preview?.zh ?? detail.content.zh).body }}</p><img v-if="(preview?.imageUrl ?? detail.imageUrl)" :src="(preview?.imageUrl ?? detail.imageUrl) ?? ''" :alt="detail.content.zh.imageAlt ?? ''" class="pd__img"></div>
          </el-col>
          <el-col :xs="24" :sm="12">
            <div class="pd__phone">
              <div class="pd__label">英文版<el-tag v-if="!detail.content.en" size="small" type="warning" class="pd__tag">沒有英文文案，英文裝置會收到中文</el-tag></div>
              <strong>{{ (preview?.enEffective ?? detail.content.en ?? detail.content.zh).title }}</strong><p>{{ (preview?.enEffective ?? detail.content.en ?? detail.content.zh).body }}</p>
            </div>
          </el-col>
        </el-row>
        <p v-if="preview?.audienceSummary" class="pd__hint">發送對象摘要：{{ preview.audienceSummary }}</p>
      </el-card>

      <el-card shadow="never" class="pd__block" header="發送結果">
        <div class="pd__stats">
          <div><div class="pd__num">{{ detail.sentCount.toLocaleString() }}</div><div class="pd__muted">送出</div></div>
          <div><div class="pd__num">{{ detail.deliveredCount.toLocaleString() }}</div><div class="pd__muted">送達</div></div>
          <div><div class="pd__num">{{ detail.failedCount.toLocaleString() }}</div><div class="pd__muted">失敗</div></div>
          <div><div class="pd__num">{{ detail.openedCount.toLocaleString() }}</div><div class="pd__muted">開啟</div></div>
        </div>
        <p class="pd__hint">{{ detail.statsNote }}</p>
        <el-table v-if="detail.stats.length > 0 && !isMobile" :data="detail.stats" size="small">
          <el-table-column prop="platformLabel" label="平台" width="110" />
          <el-table-column prop="localeLabel" label="語言" width="100" />
          <el-table-column prop="sent" label="送出" />
          <el-table-column prop="delivered" label="送達" />
          <el-table-column prop="opened" label="開啟" />
        </el-table>
        <ul v-else-if="detail.stats.length > 0" class="pd__list"><li v-for="(s, i) in detail.stats" :key="i">{{ s.platformLabel }}・{{ s.localeLabel }}：送出 {{ s.sent }}、送達 {{ s.delivered }}、開啟 {{ s.opened }}</li></ul>
      </el-card>
    </template>

    <el-dialog v-model="testVisible" title="試送到測試裝置" width="480px" :fullscreen="isMobile" :close-on-click-modal="false">
      <p class="pd__hint">輸入測試裝置的識別碼（一行一個，最多 10 台），只會送給這幾台。識別碼可以在「推播裝置」由系統管理員查看完整內容。推播傳輸尚未串接時，試送不會真的送出。</p>
      <el-alert v-if="testError" type="error" show-icon :closable="false" :title="testError" class="pd__block" />
      <el-input v-model="testIds" type="textarea" :rows="5" placeholder="一行一個裝置識別碼" />
      <el-alert v-if="testResult" :type="testResult.configured ? 'success' : 'warning'" show-icon :closable="false" class="pd__result" :title="testResult.message">
        送出 {{ testResult.sent }} 台、失敗 {{ testResult.failed }} 台<template v-if="testResult.unknownDevices.length">、找不到的裝置 {{ testResult.unknownDevices.length }} 台</template>。
      </el-alert>
      <template #footer>
        <el-button @click="testVisible = false">關閉</el-button>
        <el-button type="primary" :loading="testing" @click="doTest">試送</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.pd { min-width: 0; }
.pd__block { margin-bottom: 12px; }
.pd__head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; }
.pd__actions { display: flex; flex-wrap: wrap; gap: 8px; }
.pd__desc { margin-top: 12px; }
.pd__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.pd__hint { margin: 8px 0; font-size: 12px; line-height: 1.7; color: var(--admin-text-tertiary); }
.pd__phone { border: 1px solid var(--admin-border); border-radius: 10px; padding: 12px; margin-bottom: 10px; background: var(--admin-bg-surface-2); }
.pd__phone p { margin: 6px 0 0; font-size: 13px; line-height: 1.6; white-space: pre-wrap; word-break: break-word; }
.pd__label { font-size: 12px; color: var(--admin-text-tertiary); margin-bottom: 6px; }
.pd__tag { margin-left: 6px; }
.pd__img { max-width: 100%; margin-top: 8px; border-radius: 6px; }
.pd__stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(110px, 1fr)); gap: 12px; }
.pd__num { font-size: 24px; font-weight: 600; color: var(--admin-text-primary); }
.pd__list { margin: 8px 0 0; padding-left: 18px; font-size: 13px; line-height: 1.8; }
.pd__result { margin-top: 12px; }
</style>
