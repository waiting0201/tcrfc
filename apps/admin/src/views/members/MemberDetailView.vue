<script setup lang="ts">
/**
 * 會員詳情。預設遮罩；有「檢視完整個資」權限的人可二次確認後顯示完整資料（會留下紀錄）。
 * 顯示完整資料的狀態只存在這個元件的記憶體，離開頁面即失效，不寫入網址或瀏覽器儲存。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { formatDateTime } from '@/utils/dateTime'
import {
  getMember,
  reissueMemberCard,
  updateMemberNote,
  updateMemberStatus,
  type MemberCardDto,
  type MemberDetailDto,
} from '@/api/adminMembers'

const props = defineProps<{ id: string }>()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const columns = computed(() => (breakpoint.value === 'mobile' ? 1 : 2))
const { canUpdate } = useCrudPermissions('member.account')
const club = computed(() => activeClubId.value)

const member = ref<MemberDetailDto | null>(null)
const revealed = ref(false)
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const revealing = ref(false)

const noteDraft = ref('')
const savingNote = ref(false)
const acting = ref(false)

async function load(reveal = false) {
  loadState.value = member.value ? 'ready' : 'loading'
  try {
    const detail = await getMember(club.value, props.id, reveal)
    member.value = detail
    revealed.value = reveal && !detail.isMasked
    noteDraft.value = detail.internalNote ?? ''
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') {
      loadState.value = 'not-found'
    } else if (member.value) {
      ElMessage.error(error instanceof AdminApiError ? error.message : '重新載入失敗，請稍後再試')
      loadState.value = 'ready'
    } else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '會員資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}

onMounted(() => load(false))
watch(club, () => {
  member.value = null
  revealed.value = false
  load(false)
})
watch(
  () => props.id,
  () => {
    member.value = null
    revealed.value = false
    load(false)
  },
)

async function handleReveal() {
  try {
    await ElMessageBox.confirm(
      '即將顯示這位會員的完整個人資料（姓名、Email、電話、生日）。這個動作會被系統記錄，請只在處理會員事務時使用。',
      '顯示完整資料',
      { confirmButtonText: '確定顯示', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  revealing.value = true
  try {
    await load(true)
  } finally {
    revealing.value = false
  }
}

async function handleMask() {
  revealing.value = true
  try {
    await load(false)
  } finally {
    revealing.value = false
  }
}

const isSuspended = computed(() => member.value?.status === 'suspended')
const isDeleted = computed(() => member.value?.displayStatus === 'deleted')

async function handleToggleStatus() {
  const m = member.value
  if (!m) return
  const target = isSuspended.value ? 'active' : 'suspended'
  let reason: string | undefined
  try {
    if (target === 'suspended') {
      const { value } = await ElMessageBox.prompt('停用後會員無法登入。請簡述停用原因（選填）。', '停用帳號', {
        confirmButtonText: '停用', cancelButtonText: '取消', inputPlaceholder: '停用原因', inputType: 'textarea', confirmButtonClass: 'el-button--danger',
      })
      reason = (value ?? '').trim() || undefined
    } else {
      await ElMessageBox.confirm('確定要重新啟用這個帳號嗎？', '啟用帳號', { confirmButtonText: '啟用', cancelButtonText: '取消', type: 'warning' })
    }
  } catch {
    return
  }
  acting.value = true
  try {
    await updateMemberStatus(club.value, m.id, target, reason)
    ElMessage.success(target === 'suspended' ? '已停用' : '已啟用')
    await load(revealed.value)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '操作失敗，請稍後再試')
  } finally {
    acting.value = false
  }
}

async function handleSaveNote() {
  const m = member.value
  if (!m) return
  savingNote.value = true
  try {
    await updateMemberNote(club.value, m.id, noteDraft.value)
    m.internalNote = noteDraft.value.trim() || null
    noteDraft.value = m.internalNote ?? ''
    ElMessage.success('備註已儲存')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試')
  } finally {
    savingNote.value = false
  }
}

const noteDirty = computed(() => (noteDraft.value.trim() || null) !== (member.value?.internalNote ?? null))

async function handleReissue(card: MemberCardDto) {
  const m = member.value
  if (!m) return
  try {
    await ElMessageBox.confirm(
      '這張會員卡會換成一組新的 QR Code，舊的 QR Code 會立即失效（已存在會員手機或列印品上的會無法再使用）。確定要重新產生嗎？',
      '重新產生會員卡 QR Code',
      { confirmButtonText: '重新產生', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  acting.value = true
  try {
    await reissueMemberCard(club.value, m.id, card.id)
    ElMessage.success('已重新產生，舊的 QR Code 已失效')
    await load(revealed.value)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '重新產生失敗，請稍後再試')
  } finally {
    acting.value = false
  }
}

function statusType(status: string): 'success' | 'warning' | 'info' | 'danger' {
  if (status === 'active') return 'success'
  if (status === 'pending') return 'warning'
  if (status === 'cancelled') return 'danger'
  return 'info'
}
function accountType(status: string): 'success' | 'warning' | 'info' | 'danger' {
  if (status === 'active') return 'success'
  if (status === 'unverified') return 'warning'
  if (status === 'suspended') return 'danger'
  return 'info'
}
function money(n: number): string {
  return `NT$ ${n.toLocaleString('zh-TW')}`
}
</script>

<template>
  <div class="member-detail">
    <PageHeader :title="member ? `會員：${member.memberNo}` : '會員詳情'">
      <template #back>
        <el-button text @click="router.push('/members/list')"><el-icon><ArrowLeft /></el-icon>返回名單</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="K1" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready' || !member" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這位會員，可能不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load(false)">重新載入</el-button>
        <el-button v-else type="primary" @click="router.push('/members/list')">返回名單</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="member.isMasked" type="info" show-icon :closable="false" class="member-detail__block" title="已遮罩：目前顯示的是遮罩後的個人資料。">
        <template v-if="member.canReveal">
          <el-button type="primary" size="small" :loading="revealing" @click="handleReveal">顯示完整資料</el-button>
        </template>
        <template v-else>你的帳號沒有檢視完整個資的權限。</template>
      </el-alert>
      <el-alert v-else type="warning" show-icon :closable="false" class="member-detail__block" title="目前顯示完整個資，這次檢視已被記錄。離開此頁後會自動回到遮罩狀態。">
        <el-button size="small" :loading="revealing" @click="handleMask">重新遮罩</el-button>
      </el-alert>

      <el-card shadow="never" header="帳號資料" class="member-detail__block">
        <el-descriptions :column="columns" border class="member-detail__desc">
          <el-descriptions-item label="會員編號">{{ member.memberNo }}</el-descriptions-item>
          <el-descriptions-item label="帳號狀態">
            <el-tag :type="accountType(member.displayStatus)" size="small">{{ member.displayStatusLabel }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="姓名">{{ member.name || '—' }}</el-descriptions-item>
          <el-descriptions-item label="生日">{{ member.birthOn || '—' }}</el-descriptions-item>
          <el-descriptions-item label="Email">{{ member.email || '—' }}</el-descriptions-item>
          <el-descriptions-item label="電話">{{ member.phone || '—' }}</el-descriptions-item>
          <el-descriptions-item label="註冊來源">{{ member.signupSourceLabel }}</el-descriptions-item>
          <el-descriptions-item label="LINE">{{ member.lineBound ? '已綁定 LINE' : '未綁定 LINE' }}</el-descriptions-item>
          <el-descriptions-item label="語系偏好">{{ member.localeLabel }}</el-descriptions-item>
          <el-descriptions-item label="信箱驗證">{{ member.emailVerifiedAt ? formatDateTime(member.emailVerifiedAt) : '尚未驗證' }}</el-descriptions-item>
          <el-descriptions-item label="註冊時間">{{ formatDateTime(member.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="最近登入">{{ member.lastLoginAt ? formatDateTime(member.lastLoginAt) : '尚未登入' }}</el-descriptions-item>
          <el-descriptions-item v-if="member.mergedIntoMemberNo" label="已合併至" :span="2">{{ member.mergedIntoMemberNo }}</el-descriptions-item>
        </el-descriptions>
        <div v-if="canUpdate && !isDeleted" class="member-detail__actions">
          <el-button :type="isSuspended ? 'primary' : 'danger'" plain :loading="acting" @click="handleToggleStatus">{{ isSuspended ? '啟用帳號' : '停用帳號' }}</el-button>
        </div>
      </el-card>

      <el-card shadow="never" header="會籍" class="member-detail__block">
        <el-empty v-if="member.memberships.length === 0" description="這位會員在你有權限的俱樂部沒有會籍" :image-size="64" />
        <div v-for="ms in member.memberships" :key="ms.membership.membershipId" class="member-detail__ms">
          <div class="member-detail__ms-head">
            <router-link :to="`/members/plans/memberships/${ms.membership.membershipId}`" class="member-detail__link">
              {{ ms.membership.clubName }}・{{ ms.membership.seasonCode }}・{{ ms.membership.tierLabel }}
            </router-link>
            <el-tag :type="statusType(ms.membership.effectiveStatus)" size="small">{{ ms.membership.effectiveStatusLabel }}</el-tag>
            <span class="member-detail__muted">
              {{ ms.membership.planName || '未指定方案' }}・{{ ms.membership.startOn || '—' }} ～ {{ ms.membership.endOn || '—' }}
              <template v-if="ms.membership.effectiveStatus === 'active' && ms.membership.daysToExpire !== null && ms.membership.daysToExpire <= 30">（{{ ms.membership.daysToExpire }} 天後到期）</template>
            </span>
          </div>
          <p v-if="ms.lastAdjustReason" class="member-detail__muted">最近一次調整原因：{{ ms.lastAdjustReason }}（{{ formatDateTime(ms.lastAdjustedAt) }}）</p>

          <div class="member-detail__sub">付款紀錄</div>
          <div v-if="ms.payments.length === 0" class="member-detail__muted">沒有付款紀錄</div>
          <div v-else class="member-detail__scroll">
            <el-table :data="ms.payments" size="small" row-key="id">
              <el-table-column label="付款日" width="110" prop="paidOn" />
              <el-table-column label="方案" min-width="120"><template #default="{ row }">{{ row.planName || '—' }}</template></el-table-column>
              <el-table-column label="方式" width="90" prop="methodLabel" />
              <el-table-column label="金額" width="110"><template #default="{ row }">{{ money(row.amount) }}</template></el-table-column>
              <el-table-column label="經手人" width="100"><template #default="{ row }">{{ row.handledByName || '—' }}</template></el-table-column>
              <el-table-column label="備註" min-width="120"><template #default="{ row }">{{ row.note || '—' }}</template></el-table-column>
            </el-table>
          </div>

          <div class="member-detail__sub">會員卡</div>
          <div v-if="ms.cards.length === 0" class="member-detail__muted">沒有會員卡</div>
          <div v-for="card in ms.cards" :key="card.id" class="member-detail__card">
            <span>{{ card.holderName || '—' }}</span>
            <el-tag :type="card.revokedAt ? 'info' : 'success'" size="small">{{ card.statusLabel }}</el-tag>
            <span class="member-detail__muted">發卡 {{ formatDateTime(card.issuedAt) }}・已重產 {{ card.reissueCount }} 次</span>
            <el-button v-if="canUpdate && !card.revokedAt" size="small" text type="danger" :disabled="acting" @click="handleReissue(card)">重新產生 QR Code</el-button>
          </div>
        </div>
      </el-card>

      <el-card shadow="never" header="球衣" class="member-detail__block">
        <div v-if="member.jerseyIssues.length === 0" class="member-detail__muted">沒有球衣發放紀錄</div>
        <div v-else class="member-detail__scroll">
          <el-table :data="member.jerseyIssues" size="small" row-key="id">
            <el-table-column label="領用人" min-width="100"><template #default="{ row }">{{ row.recipientName || '—' }}</template></el-table-column>
            <el-table-column label="尺寸" width="80"><template #default="{ row }">{{ row.size || '未填' }}</template></el-table-column>
            <el-table-column label="領取方式" width="100" prop="deliveryMethodLabel" />
            <el-table-column label="狀態" width="90" prop="statusLabel" />
            <el-table-column label="寄出日" width="110"><template #default="{ row }">{{ row.shippedOn || '—' }}</template></el-table-column>
            <el-table-column label="領取日" width="110"><template #default="{ row }">{{ row.receivedOn || '—' }}</template></el-table-column>
          </el-table>
        </div>
      </el-card>

      <el-card shadow="never" header="內部備註" class="member-detail__block">
        <el-input v-model="noteDraft" type="textarea" :rows="3" maxlength="2000" show-word-limit :disabled="!canUpdate" placeholder="只有後台人員看得到，會員看不到" />
        <div v-if="canUpdate" class="member-detail__actions">
          <el-button type="primary" :disabled="!noteDirty" :loading="savingNote" @click="handleSaveNote">儲存備註</el-button>
        </div>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.member-detail { max-width: 980px; margin: 0 auto 40px; }
.member-detail__block { margin-bottom: 16px; }
.member-detail__actions { margin-top: 12px; display: flex; gap: 8px; }
.member-detail__muted { color: var(--admin-text-tertiary); font-size: 12px; line-height: 1.6; }
.member-detail__ms { padding-bottom: 16px; margin-bottom: 16px; border-bottom: 1px solid var(--el-border-color-lighter); }
.member-detail__ms:last-child { border-bottom: 0; margin-bottom: 0; padding-bottom: 0; }
.member-detail__ms-head { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.member-detail__link { font-weight: 500; color: var(--el-color-primary); text-decoration: none; }
.member-detail__sub { margin: 12px 0 6px; font-size: 13px; font-weight: 500; }
.member-detail__card { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 4px 0; }
.member-detail__scroll { overflow-x: auto; }
.member-detail__desc { word-break: break-all; }
</style>
