<script setup lang="ts">
/**
 * 投放檔期的新增／編輯對話框（清單頁新增、詳情頁編輯共用）。
 * 🔴 已排程／投放中／已暫停的檔期只能改名稱、權重與兩個上限；改廣告主、版位、期間、目標要「作廢後重建」。
 * 🔴 合約金額：沒有「編輯合約金額」權限的人看不到這一欄，也不會送出；有權限的人編輯時要帶回原本的金額，否則會被清空。
 * 🔴 時間一律是台灣時間：選擇器顯示台灣時間，送出時換算成帶時區的 UTC。
 */
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import {
  createCampaign,
  listAdSlots,
  listAdvertisers,
  updateCampaign,
  type AdSlotDto,
  type AdvertiserDto,
  type CampaignDetailDto,
  type CampaignGoalType,
} from '@/api/adminAds'
import { pickerDateToUtc, utcToPickerDate } from '@/utils/dateTime'

const props = defineProps<{ modelValue: boolean; campaign: CampaignDetailDto | null }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: boolean): void; (e: 'saved', c: CampaignDetailDto): void }>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canEditAmount = usePermission('ad.contract.update')
const canViewAmount = usePermission('ad.contract.view')

const advertisers = ref<AdvertiserDto[]>([])
const slots = ref<AdSlotDto[]>([])
const optionError = ref<string | null>(null)

const form = reactive({
  advertiserId: '', slotId: '', name: '', startsAt: null as Date | null, endsAt: null as Date | null, weight: 1,
  dailyImpressionCap: null as number | null, perDeviceDailyCap: null as number | null,
  goalType: 'traffic' as CampaignGoalType, goalImpressions: null as number | null,
  contractAmount: null as number | null, isAmountHidden: false,
})
const saving = ref(false)
const formError = ref<string | null>(null)

const isEdit = computed(() => !!props.campaign)
/** 只有草稿能改全部欄位；已排程／投放中／已暫停只能改名稱、權重與上限。 */
const structureLocked = computed(() => !!props.campaign && props.campaign.status !== 'draft')
const fullyLocked = computed(() => !!props.campaign && !['draft', 'scheduled', 'running', 'paused'].includes(props.campaign.status))

watch(
  () => props.modelValue,
  async (open) => {
    if (!open) return
    formError.value = null
    optionError.value = null
    const c = props.campaign
    Object.assign(form, {
      advertiserId: c?.advertiserId ?? '', slotId: c?.slotId ?? '', name: c?.name ?? '',
      startsAt: utcToPickerDate(c?.startsAt), endsAt: utcToPickerDate(c?.endsAt), weight: c?.weight ?? 1,
      dailyImpressionCap: c?.dailyImpressionCap ?? null, perDeviceDailyCap: c?.perDeviceDailyCap ?? null,
      goalType: c?.goalType ?? 'traffic', goalImpressions: c?.goalImpressions ?? null,
      contractAmount: c?.contractAmount ?? null, isAmountHidden: c?.isAmountHidden ?? false,
    })
    try {
      const [a, s] = await Promise.all([listAdvertisers(), listAdSlots()])
      advertisers.value = a
      slots.value = s
    } catch (e) {
      optionError.value = e instanceof AdminApiError ? e.message : '廣告主與版位清單載入失敗'
    }
  },
)

const selectableAdvertisers = computed(() => advertisers.value.filter((a) => a.status !== 'ended' || a.id === props.campaign?.advertiserId))
const selectableSlots = computed(() => slots.value.filter((s) => s.isActive || s.id === props.campaign?.slotId))

async function save() {
  if (fullyLocked.value) return
  const name = form.name.trim()
  if (!name) return void (formError.value = '請輸入檔期名稱')
  if (!form.advertiserId) return void (formError.value = '請選擇廣告主')
  if (!form.slotId) return void (formError.value = '請選擇廣告版位')
  const startsAt = pickerDateToUtc(form.startsAt)
  const endsAt = pickerDateToUtc(form.endsAt)
  if (!startsAt || !endsAt) return void (formError.value = '請選擇開始與結束時間')
  if (form.endsAt!.getTime() <= form.startsAt!.getTime()) return void (formError.value = '結束時間必須晚於開始時間')
  if (!Number.isInteger(form.weight) || form.weight < 1 || form.weight > 100) return void (formError.value = '權重請填 1 到 100')
  if (form.goalType === 'guaranteed' && (!form.goalImpressions || form.goalImpressions < 1)) {
    return void (formError.value = '曝光保證型的檔期必須填寫目標曝光數')
  }
  saving.value = true
  formError.value = null
  const payload = {
    advertiserId: form.advertiserId,
    slotId: form.slotId,
    name,
    startsAt,
    endsAt,
    weight: form.weight,
    dailyImpressionCap: form.dailyImpressionCap,
    perDeviceDailyCap: form.perDeviceDailyCap,
    goalType: form.goalType,
    goalImpressions: form.goalImpressions,
    // 沒有編輯金額權限就一律不送（送非空值會被拒絕）；有權限時照表單送，空白會清空金額
    ...(canEditAmount.value ? { contractAmount: form.contractAmount, isAmountHidden: form.isAmountHidden } : {}),
  }
  try {
    const saved = props.campaign ? await updateCampaign(props.campaign.id, payload) : await createCampaign(payload)
    ElMessage.success('已儲存')
    emit('saved', saved)
    emit('update:modelValue', false)
  } catch (e) {
    formError.value = e instanceof AdminApiError ? e.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-dialog :model-value="modelValue" :title="isEdit ? '編輯投放檔期' : '新增投放檔期'" width="680px" :fullscreen="isMobile" :close-on-click-modal="false" @update:model-value="(v: boolean) => emit('update:modelValue', v)">
    <el-alert v-if="optionError" type="warning" show-icon :closable="false" :title="optionError" class="cf__block" />
    <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="cf__block" />
    <el-alert v-if="fullyLocked" type="info" show-icon :closable="false" title="這個檔期已結束、結案或作廢，不能再修改" class="cf__block" />
    <el-alert v-else-if="structureLocked" type="info" show-icon :closable="false" title="這個檔期已送審通過或正在投放，只能修改名稱、權重與曝光上限；要改廣告主、版位、期間或目標，請先作廢再重新建立。" class="cf__block" />
    <el-form label-position="top" :disabled="fullyLocked">
      <el-form-item label="檔期名稱" required><el-input v-model="form.name" maxlength="100" /></el-form-item>
      <el-row :gutter="12">
        <el-col :xs="24" :sm="12">
          <el-form-item label="廣告主" required>
            <el-select v-model="form.advertiserId" filterable :disabled="structureLocked" placeholder="選擇廣告主" style="width: 100%">
              <el-option v-for="a in selectableAdvertisers" :key="a.id" :label="a.nameZh || '（未命名）'" :value="a.id" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="廣告版位" required>
            <el-select v-model="form.slotId" filterable :disabled="structureLocked" placeholder="選擇版位" style="width: 100%">
              <el-option v-for="s in selectableSlots" :key="s.id" :label="s.nameZh || s.slotCode" :value="s.id" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12"><el-form-item label="開始時間（台灣時間）" required><el-date-picker v-model="form.startsAt" type="datetime" :disabled="structureLocked" style="width: 100%" /></el-form-item></el-col>
        <el-col :xs="24" :sm="12"><el-form-item label="結束時間（台灣時間）" required><el-date-picker v-model="form.endsAt" type="datetime" :disabled="structureLocked" style="width: 100%" /></el-form-item></el-col>
        <el-col :xs="24" :sm="8"><el-form-item label="投放權重（1–100）"><el-input-number v-model="form.weight" :min="1" :max="100" controls-position="right" style="width: 100%" /></el-form-item></el-col>
        <el-col :xs="24" :sm="8"><el-form-item label="每日曝光上限（選填）"><el-input-number v-model="form.dailyImpressionCap" :min="1" controls-position="right" style="width: 100%" /></el-form-item></el-col>
        <el-col :xs="24" :sm="8"><el-form-item label="每人每日曝光上限（選填）"><el-input-number v-model="form.perDeviceDailyCap" :min="1" controls-position="right" style="width: 100%" /></el-form-item></el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="投放目標">
            <el-radio-group v-model="form.goalType" :disabled="structureLocked">
              <el-radio value="traffic">導流</el-radio>
              <el-radio value="guaranteed">曝光保證</el-radio>
            </el-radio-group>
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12"><el-form-item :label="form.goalType === 'guaranteed' ? '目標曝光數（必填）' : '目標曝光數（選填）'"><el-input-number v-model="form.goalImpressions" :min="1" :disabled="structureLocked" controls-position="right" style="width: 100%" /></el-form-item></el-col>
      </el-row>
      <template v-if="canViewAmount || canEditAmount">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><el-form-item label="合約金額（元）"><el-input-number v-model="form.contractAmount" :min="0" :disabled="!canEditAmount || structureLocked" controls-position="right" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="金額標示"><el-switch v-model="form.isAmountHidden" :disabled="!canEditAmount" active-text="標示為不公開" /></el-form-item></el-col>
        </el-row>
      </template>
    </el-form>
    <p class="cf__hint">「曝光保證」會依剩餘量平均分配到剩餘天數，今天的份額用完就先停；達到目標曝光數即停止。同一版位同時段有多個檔期時，依權重比例輪流出現。</p>
    <template #footer>
      <el-button @click="emit('update:modelValue', false)">取消</el-button>
      <el-button v-if="!fullyLocked" type="primary" :loading="saving" @click="save">儲存</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.cf__block { margin-bottom: 12px; }
.cf__hint { margin: 4px 0 0; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>
