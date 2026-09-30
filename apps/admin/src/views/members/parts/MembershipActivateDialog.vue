<script setup lang="ts">
/**
 * 手動開通／續會：客服核對款項後在後台補開通，不是金流。
 * 續會＝用下一球季的方案再開通一次。
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import MemberPicker from './MemberPicker.vue'
import { activeClubId, availableClubs } from '@/auth/clubAccess'
import { activateMembership, type MembershipDetailDto, type MembershipPlanListItemDto } from '@/api/adminMemberships'
import { nullIfBlank } from '@/api/adminCommon'
import { errorMessage, formatMoney, todayString } from './membershipHelpers'
import { pickerDateToDateOnly, taipeiToday } from '@/utils/dateTime'

const props = defineProps<{ modelValue: boolean; plans: MembershipPlanListItemDto[] }>()
const emit = defineEmits<{
  (e: 'update:modelValue', v: boolean): void
  (e: 'done', detail: MembershipDetailDto): void
}>()

const form = ref({
  memberId: '',
  planId: '',
  paymentMethod: 'onsite' as 'linepay' | 'onsite',
  amount: 0,
  paidOn: todayString(),
  note: '',
  startOn: '',
  endOn: '',
})
const saving = ref(false)
const error = ref<string | null>(null)

const publishedPlans = computed(() => props.plans.filter((p) => p.status === 'published'))
const clubName = computed(() => availableClubs.value.find((c) => c.code === activeClubId.value)?.name ?? activeClubId.value)
const selectedPlan = computed(() => props.plans.find((p) => p.id === form.value.planId))

watch(
  () => props.modelValue,
  (open) => {
    if (!open) return
    form.value = { memberId: '', planId: '', paymentMethod: 'onsite', amount: 0, paidOn: todayString(), note: '', startOn: '', endOn: '' }
    error.value = null
  },
)

function onPlanChange() {
  if (selectedPlan.value) form.value.amount = selectedPlan.value.fee
}

function disabledFuture(date: Date): boolean {
  return pickerDateToDateOnly(date) > taipeiToday()
}

function validate(): string | null {
  const f = form.value
  if (!f.memberId) return '請先選擇會員'
  if (!f.planId) return '請選擇方案'
  if (f.amount === null || f.amount < 0) return '請輸入實收金額（可為 0）'
  if (!f.paidOn) return '請選擇付款日'
  if (f.paidOn > todayString()) return '付款日不能晚於今天'
  if (f.startOn && f.endOn && f.endOn < f.startOn) return '結束日不能早於開始日'
  return null
}

async function submit() {
  const problem = validate()
  if (problem) {
    error.value = problem
    return
  }
  saving.value = true
  error.value = null
  try {
    const f = form.value
    const detail = await activateMembership(activeClubId.value, {
      memberId: f.memberId,
      planId: f.planId,
      paymentMethod: f.paymentMethod,
      amount: f.amount,
      paidOn: f.paidOn,
      note: nullIfBlank(f.note),
      startOn: f.startOn || null,
      endOn: f.endOn || null,
    })
    ElMessage.success('已開通會籍')
    emit('update:modelValue', false)
    emit('done', detail)
  } catch (e) {
    error.value = errorMessage(e, '開通失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="手動開通／續會"
    width="min(560px, 94vw)"
    :close-on-click-modal="false"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <el-alert type="info" :closable="false" show-icon class="dlg__alert">
      這是客服核對款項後的手動開通，系統不會向會員收款。續會請選擇「下一球季」的方案再開通一次。
      補登已結束球季的會籍時，必須填寫開始日與結束日。
    </el-alert>
    <el-alert v-if="error" :title="error" type="warning" show-icon class="dlg__alert" @close="error = null" />
    <el-form label-position="top" @submit.prevent="submit">
      <el-form-item label="會員" required>
        <MemberPicker v-model="form.memberId" />
      </el-form-item>
      <el-form-item label="方案（僅列出已上架的方案）" required>
        <el-select v-model="form.planId" placeholder="選擇方案" style="width: 100%" @change="onPlanChange">
          <el-option v-for="p in publishedPlans" :key="p.id" :value="p.id" :label="`${p.seasonCode}｜${p.nameZh || p.code}（${formatMoney(p.fee)}）`" />
        </el-select>
      </el-form-item>
      <el-row :gutter="12">
        <el-col :xs="24" :sm="12">
          <el-form-item label="付款方式" required>
            <el-radio-group v-model="form.paymentMethod">
              <el-radio value="onsite">現場付款</el-radio>
              <el-radio value="linepay">LINE Pay（已付款）</el-radio>
            </el-radio-group>
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="實收金額（元）" required>
            <el-input-number v-model="form.amount" :min="0" :precision="0" style="width: 100%" />
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="付款日（不可晚於今天）" required>
            <el-date-picker v-model="form.paidOn" type="date" value-format="YYYY-MM-DD" :disabled-date="disabledFuture" style="width: 100%" />
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="受益俱樂部">
            <el-input :model-value="clubName" disabled />
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="會籍開始日（可空）">
            <el-date-picker v-model="form.startOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="會籍結束日（可空）">
            <el-date-picker v-model="form.endOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
          </el-form-item>
        </el-col>
      </el-row>
      <p class="dlg__hint">開始日、結束日留空時，依方案設定的期間開通。</p>
      <el-form-item label="備註">
        <el-input v-model="form.note" type="textarea" :rows="2" maxlength="200" show-word-limit placeholder="例如：現場刷卡、收據編號" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button :disabled="saving" @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="saving" @click="submit">確認開通</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.dlg__alert { margin-bottom: 12px; }
.dlg__hint { margin: 0 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>
