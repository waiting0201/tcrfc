<script setup lang="ts">
/** 會籍詳情：付款紀錄、會員卡、手動調整（必填原因）、新增副卡、停用單卡。 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  addMembershipCard,
  adjustMembership,
  getMembership,
  revokeMembershipCard,
  type AdjustMembershipPayload,
  type MembershipCardDto,
  type MembershipDetailDto,
  type MembershipStatus,
  type MembershipTier,
} from '@/api/adminMemberships'
import { clubNameOf, effectiveStatusLabel, errorMessage, expiryText, formatMoney, STATUS_TAG, tierLabel } from './parts/membershipHelpers'

const props = defineProps<{ id: string }>()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canUpdate } = useCrudPermissions('member.membership')
const club = computed(() => activeClubId.value)

const detail = ref<MembershipDetailDto | null>(null)
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')

async function load() {
  loadState.value = 'loading'
  try {
    detail.value = await getMembership(club.value, props.id)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') {
      loadState.value = 'not-found'
    } else {
      loadErrorMessage.value = errorMessage(error, '會籍載入失敗，請稍後再試')
      loadState.value = 'error'
    }
  }
}
onMounted(load)
watch(() => props.id, load)
// 切換俱樂部後，這筆會籍就不屬於目前俱樂部了，回到清單
watch(club, () => router.replace({ path: '/members/plans', query: { tab: 'memberships' } }))

const m = computed(() => detail.value?.membership)
const activeCards = computed(() => (detail.value?.cards ?? []).filter((c) => !c.revokedAt))
const cardsFull = computed(() => !!detail.value && activeCards.value.length >= detail.value.cardQuota)
const backToList = () => router.push({ path: '/members/plans', query: { tab: 'memberships' } })

// ── 調整 ──
const adjustOpen = ref(false)
const adjust = ref({ tier: 'registered' as MembershipTier, status: 'active' as MembershipStatus, startOn: '', endOn: '', reason: '' })
const adjustError = ref<string | null>(null)
const busy = ref(false)

function openAdjust() {
  const x = m.value
  if (!x) return
  adjust.value = { tier: x.tier, status: x.status, startOn: x.startOn ?? '', endOn: x.endOn ?? '', reason: '' }
  adjustError.value = null
  adjustOpen.value = true
}
async function submitAdjust() {
  const x = m.value
  if (!x) return
  const a = adjust.value
  if (!a.reason.trim()) return void (adjustError.value = '請填寫調整原因')
  if (a.startOn && a.endOn && a.endOn < a.startOn) return void (adjustError.value = '結束日不能早於開始日')
  const payload: AdjustMembershipPayload = { reason: a.reason.trim() }
  if (a.tier !== x.tier) payload.tier = a.tier
  if (a.status !== x.status) payload.status = a.status
  if (a.startOn && a.startOn !== (x.startOn ?? '')) payload.startOn = a.startOn
  if (a.endOn && a.endOn !== (x.endOn ?? '')) payload.endOn = a.endOn
  if (!payload.tier && !payload.status && !payload.startOn && !payload.endOn) return void (adjustError.value = '請至少修改層級、狀態或起訖日其中一項')
  busy.value = true
  adjustError.value = null
  try {
    detail.value = await adjustMembership(club.value, props.id, payload)
    adjustOpen.value = false
    ElMessage.success('已調整會籍')
  } catch (error) {
    adjustError.value = errorMessage(error, '調整失敗，請稍後再試')
  } finally {
    busy.value = false
  }
}

// ── 副卡 ──
const cardOpen = ref(false)
const holderName = ref('')
const cardError = ref<string | null>(null)
function openAddCard() {
  holderName.value = ''
  cardError.value = null
  cardOpen.value = true
}
async function submitCard() {
  if (!holderName.value.trim()) return void (cardError.value = '請輸入持卡人姓名')
  busy.value = true
  cardError.value = null
  try {
    detail.value = await addMembershipCard(club.value, props.id, holderName.value.trim())
    cardOpen.value = false
    ElMessage.success('已新增會員卡')
  } catch (error) {
    cardError.value = errorMessage(error, '新增失敗，請稍後再試')
  } finally {
    busy.value = false
  }
}
async function revoke(card: MembershipCardDto) {
  try {
    await ElMessageBox.confirm(`確定要停用「${card.holderName || '這張卡'}」嗎？停用後這張卡不能再用來驗證。`, '停用會員卡', {
      confirmButtonText: '停用',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    detail.value = await revokeMembershipCard(club.value, props.id, card.id)
    ElMessage.success('已停用')
  } catch (error) {
    ElMessage.error(errorMessage(error, '停用失敗，請稍後再試'))
  }
}
const fmtTime = (iso?: string | null) => (iso ? new Date(iso).toLocaleString('zh-TW', { hour12: false }) : '—')
</script>

<template>
  <div class="ms-detail">
    <PageHeader :title="m ? `會籍：${m.memberNo || ''} ${m.seasonCode}` : '會籍詳情'">
      <template #back>
        <el-button text @click="backToList"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="K2" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready' || !detail || !m" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這筆會籍，可能不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="backToList">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-card shadow="never" class="ms-detail__section">
        <template #header>
          <div class="ms-detail__head">
            <span>會籍資料</span>
            <el-button v-if="canUpdate" size="small" @click="openAdjust">調整會籍</el-button>
          </div>
        </template>
        <el-descriptions :column="isMobile ? 1 : 2" border>
          <el-descriptions-item label="會員編號">{{ m.memberNo || '—' }}</el-descriptions-item>
          <el-descriptions-item label="姓名（已遮罩）">{{ m.memberName || '—' }}</el-descriptions-item>
          <el-descriptions-item label="會員層級">{{ tierLabel(m) }}</el-descriptions-item>
          <el-descriptions-item label="目前狀態">
            <el-tag :type="STATUS_TAG[m.effectiveStatus]" size="small">{{ effectiveStatusLabel(m) }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="球季">{{ m.seasonCode }}</el-descriptions-item>
          <el-descriptions-item label="方案">{{ m.planName || '—' }}</el-descriptions-item>
          <el-descriptions-item label="會籍期間">{{ m.startOn || '—' }} ～ {{ m.endOn || '—' }}</el-descriptions-item>
          <el-descriptions-item label="到期提醒">{{ expiryText(m.daysToExpire) || '—' }}</el-descriptions-item>
          <el-descriptions-item label="已付總額">{{ formatMoney(m.paidTotal) }}</el-descriptions-item>
          <el-descriptions-item label="方案額度">會員卡 {{ detail.cardQuota }} 張、球衣 {{ detail.jerseyQuota }} 件</el-descriptions-item>
          <el-descriptions-item label="最近一次調整原因" :span="isMobile ? 1 : 2">
            {{ detail.lastAdjustReason || '尚未調整過' }}
            <span v-if="detail.lastAdjustedAt" class="ms-detail__muted">（{{ fmtTime(detail.lastAdjustedAt) }}）</span>
          </el-descriptions-item>
        </el-descriptions>
        <p class="ms-detail__hint">「有效」與否以到期日為準；系統只保留最近一次的調整原因。</p>
      </el-card>

      <el-card shadow="never" class="ms-detail__section">
        <template #header>
          <div class="ms-detail__head">
            <span>會員卡（使用中 {{ activeCards.length }}／上限 {{ detail.cardQuota }}）</span>
            <el-button v-if="canUpdate" size="small" :disabled="cardsFull" @click="openAddCard">新增副卡</el-button>
          </div>
        </template>
        <template v-if="detail.cards.length > 0">
          <el-table v-if="!isMobile" :data="detail.cards" row-key="id">
            <el-table-column label="持卡人（已遮罩）" min-width="140"><template #default="{ row }">{{ row.holderName || '—' }}</template></el-table-column>
            <el-table-column label="狀態" width="100">
              <template #default="{ row }"><el-tag :type="row.revokedAt ? 'info' : 'success'" size="small">{{ row.statusLabel || (row.revokedAt ? '已停用' : '使用中') }}</el-tag></template>
            </el-table-column>
            <el-table-column label="發卡時間" min-width="160"><template #default="{ row }">{{ fmtTime(row.issuedAt) }}</template></el-table-column>
            <el-table-column label="停用時間" min-width="160"><template #default="{ row }">{{ fmtTime(row.revokedAt) }}</template></el-table-column>
            <el-table-column v-if="canUpdate" label="操作" width="90">
              <template #default="{ row }"><el-button v-if="!row.revokedAt" size="small" text type="danger" @click="revoke(row)">停用</el-button></template>
            </el-table-column>
          </el-table>
          <MobileCardList v-else :rows="detail.cards" row-key="id">
            <template #title="{ row }">{{ row.holderName || '—' }}</template>
            <template #meta="{ row }">
              <el-tag :type="row.revokedAt ? 'info' : 'success'" size="small">{{ row.statusLabel || (row.revokedAt ? '已停用' : '使用中') }}</el-tag>
              <span>發卡：{{ fmtTime(row.issuedAt) }}</span>
            </template>
            <template #actions="{ row }"><el-button v-if="canUpdate && !row.revokedAt" size="small" text type="danger" @click="revoke(row)">停用</el-button></template>
          </MobileCardList>
        </template>
        <el-empty v-else description="這份會籍還沒有會員卡" :image-size="64" />
        <p v-if="cardsFull" class="ms-detail__hint">已達方案的會員卡數上限，無法再新增副卡。</p>
      </el-card>

      <el-card shadow="never" class="ms-detail__section">
        <template #header>付款紀錄</template>
        <template v-if="detail.payments.length > 0">
          <el-table v-if="!isMobile" :data="detail.payments" row-key="id">
            <el-table-column label="付款日" width="110"><template #default="{ row }">{{ row.paidOn }}</template></el-table-column>
            <el-table-column label="方案" min-width="120"><template #default="{ row }">{{ row.planName || '—' }}</template></el-table-column>
            <el-table-column label="方式" width="100"><template #default="{ row }">{{ row.methodLabel || row.method }}</template></el-table-column>
            <el-table-column label="金額" width="100"><template #default="{ row }">{{ formatMoney(row.amount) }}</template></el-table-column>
            <el-table-column label="收款主體" width="110"><template #default="{ row }">{{ clubNameOf(row.collectingClubCode) }}</template></el-table-column>
            <el-table-column label="受益俱樂部" width="110"><template #default="{ row }">{{ clubNameOf(row.beneficiaryClubCode) }}</template></el-table-column>
            <el-table-column label="經手人" width="100"><template #default="{ row }">{{ row.handledByName || '—' }}</template></el-table-column>
            <el-table-column label="備註" min-width="120"><template #default="{ row }">{{ row.note || '—' }}</template></el-table-column>
          </el-table>
          <MobileCardList v-else :rows="detail.payments" row-key="id">
            <template #title="{ row }">{{ row.paidOn }}｜{{ formatMoney(row.amount) }}</template>
            <template #meta="{ row }">
              <span>{{ row.planName || '' }}</span>
              <span>{{ row.methodLabel || row.method }}</span>
              <span>收款：{{ clubNameOf(row.collectingClubCode) }}</span>
              <span v-if="row.note">{{ row.note }}</span>
            </template>
            <template #actions><span /></template>
          </MobileCardList>
        </template>
        <el-empty v-else description="沒有付款紀錄（例如一般會員會籍）" :image-size="64" />
      </el-card>
    </template>

    <el-dialog v-model="adjustOpen" title="調整會籍" width="min(500px, 94vw)" :close-on-click-modal="false">
      <el-alert type="info" :closable="false" show-icon class="ms-detail__alert">
        調整是客服的人工更正。系統只保留最近一次調整原因；改成「已取消」時，這份會籍的會員卡會一併停用。
      </el-alert>
      <el-alert v-if="adjustError" :title="adjustError" type="warning" show-icon class="ms-detail__alert" @close="adjustError = null" />
      <el-form label-position="top">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12">
            <el-form-item label="會員層級">
              <el-select v-model="adjust.tier" style="width: 100%"><el-option label="一般會員" value="registered" /><el-option label="付費球迷會員" value="fan_club" /></el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="會籍狀態">
              <el-select v-model="adjust.status" style="width: 100%">
                <el-option label="待確認" value="pending" /><el-option label="有效" value="active" /><el-option label="已到期" value="expired" /><el-option label="已取消" value="cancelled" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="開始日"><el-date-picker v-model="adjust.startOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="結束日"><el-date-picker v-model="adjust.endOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
        </el-row>
        <el-form-item label="調整原因（必填）" required>
          <el-input v-model="adjust.reason" type="textarea" :rows="3" maxlength="200" show-word-limit placeholder="例如：會員來電更正，原登記層級有誤" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="busy" @click="adjustOpen = false">取消</el-button>
        <el-button type="primary" :loading="busy" @click="submitAdjust">確認調整</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="cardOpen" title="新增副卡" width="min(420px, 94vw)" :close-on-click-modal="false">
      <el-alert v-if="cardError" :title="cardError" type="warning" show-icon class="ms-detail__alert" @close="cardError = null" />
      <el-form label-position="top" @submit.prevent="submitCard">
        <el-form-item label="持卡人姓名" required><el-input v-model="holderName" maxlength="50" placeholder="家庭方案的其他成員姓名" /></el-form-item>
      </el-form>
      <p class="ms-detail__hint">會員卡數量不能超過方案的上限。</p>
      <template #footer>
        <el-button :disabled="busy" @click="cardOpen = false">取消</el-button>
        <el-button type="primary" :loading="busy" @click="submitCard">新增</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.ms-detail { max-width: 980px; margin: 0 auto 40px; min-width: 0; }
.ms-detail__section { margin-bottom: 16px; }
.ms-detail__head { display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-wrap: wrap; }
.ms-detail__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.ms-detail__muted { color: var(--admin-text-tertiary); font-size: 12px; }
.ms-detail__alert { margin-bottom: 12px; }
</style>
