<script setup lang="ts">
/**
 * 商店設定（每個俱樂部各自一份）：運費、免運門檻、不配送地區、庫存偏低門檻、待付款保留時間，
 * 以及商店入口說明與購物政策（中英雙語）。不做重量或級距計費。
 * 🔴 收款主體是俱樂部，不是慈善捐款平台的主辦協會——畫面必須顯示系統提供的收款主體說明。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import { useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { getShopSettings, saveShopSettings, type ShopLocaleText, type ShopSettingsDto } from '@/api/adminShop'
import { formatDateTime } from '@/utils/dateTime'

const { canUpdate } = useViewUpdatePermissions('shop.setting')
const club = computed(() => activeClubId.value)

const subject = ref<ShopSettingsDto['collectingSubject'] | null>(null)
const updatedAt = ref<string | null>(null)
const form = reactive({
  shippingFee: 0,
  useThreshold: false,
  freeShippingThreshold: 0,
  excludedRegions: [] as string[],
  lowStockThreshold: 5,
  pendingTimeoutMinutes: 30,
  entryTitleZh: '', entryTitleEn: '', entryIntroZh: '', entryIntroEn: '',
  noticeZh: '', noticeEn: '', shippingZh: '', shippingEn: '', returnsZh: '', returnsEn: '', termsZh: '', termsEn: '',
})
const baseline = ref('')
const loading = ref(true)
const loadError = ref<string | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)
const isDirty = computed(() => !loading.value && JSON.stringify(form) !== baseline.value)
useUnsavedChanges(isDirty)

function apply(d: ShopSettingsDto) {
  subject.value = d.collectingSubject
  updatedAt.value = d.updatedAt ?? null
  form.shippingFee = d.shippingFee
  form.useThreshold = d.freeShippingThreshold !== null && d.freeShippingThreshold !== undefined
  form.freeShippingThreshold = d.freeShippingThreshold ?? 0
  form.excludedRegions = [...(d.excludedRegions ?? [])]
  form.lowStockThreshold = d.lowStockThreshold
  form.pendingTimeoutMinutes = d.pendingTimeoutMinutes
  form.entryTitleZh = d.entryTitle?.zh ?? ''; form.entryTitleEn = d.entryTitle?.en ?? ''
  form.entryIntroZh = d.entryIntro?.zh ?? ''; form.entryIntroEn = d.entryIntro?.en ?? ''
  form.noticeZh = d.policyNotice?.zh ?? ''; form.noticeEn = d.policyNotice?.en ?? ''
  form.shippingZh = d.policyShipping?.zh ?? ''; form.shippingEn = d.policyShipping?.en ?? ''
  form.returnsZh = d.policyReturns?.zh ?? ''; form.returnsEn = d.policyReturns?.en ?? ''
  form.termsZh = d.policyTerms?.zh ?? ''; form.termsEn = d.policyTerms?.en ?? ''
  baseline.value = JSON.stringify(form)
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    apply(await getShopSettings(club.value))
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.message : '商店設定載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, load)

const text = (zh: string, en: string): ShopLocaleText => ({ zh: nullIfBlank(zh), en: nullIfBlank(en) })

function validate(): string | null {
  if (form.shippingFee === null || form.shippingFee < 0) return '運費不能是負數'
  if (form.useThreshold && (form.freeShippingThreshold === null || form.freeShippingThreshold < 0)) return '免運門檻不能是負數'
  if (form.excludedRegions.length > 60) return '不配送地區最多 60 項'
  if (!Number.isInteger(form.pendingTimeoutMinutes) || form.pendingTimeoutMinutes < 5 || form.pendingTimeoutMinutes > 1440) return '待付款保留時間要在 5 到 1440 分鐘之間'
  if ([form.entryIntroZh, form.entryIntroEn, form.noticeZh, form.noticeEn, form.shippingZh, form.shippingEn, form.returnsZh, form.returnsEn, form.termsZh, form.termsEn].some((t) => t.length > 20000)) return '每段說明最多 20000 字'
  return null
}

async function save() {
  formError.value = validate()
  if (formError.value) return
  saving.value = true
  try {
    apply(
      await saveShopSettings(club.value, {
        shippingFee: form.shippingFee,
        freeShippingThreshold: form.useThreshold ? form.freeShippingThreshold : null,
        excludedRegions: [...new Set(form.excludedRegions.map((r) => r.trim()).filter(Boolean))],
        lowStockThreshold: form.lowStockThreshold,
        pendingTimeoutMinutes: form.pendingTimeoutMinutes,
        entryTitle: text(form.entryTitleZh, form.entryTitleEn),
        entryIntro: text(form.entryIntroZh, form.entryIntroEn),
        policyNotice: text(form.noticeZh, form.noticeEn),
        policyShipping: text(form.shippingZh, form.shippingEn),
        policyReturns: text(form.returnsZh, form.returnsEn),
        policyTerms: text(form.termsZh, form.termsEn),
      }),
    )
    ElMessage.success('已儲存商店設定')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="settings">
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <template v-else>
      <el-alert v-if="subject" type="warning" show-icon :closable="false" class="settings__block" :title="`收款主體：${subject.name}`" :description="subject.notice" />
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="settings__block" @close="formError = null" />
      <el-form label-position="top" :disabled="!canUpdate">
        <el-card shadow="never" header="運費與庫存" class="settings__block">
          <p class="settings__hint">運費設定是每個俱樂部各自一份，採單一固定運費，不做重量或級距計費。現場自取免運。</p>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="8"><el-form-item label="固定運費（元）"><el-input-number v-model="form.shippingFee" :min="0" :controls="false" style="width: 100%" /></el-form-item></el-col>
            <el-col :xs="24" :sm="8">
              <el-form-item label="免運門檻">
                <div class="settings__threshold">
                  <el-switch v-model="form.useThreshold" active-text="設定免運門檻" />
                  <el-input-number v-if="form.useThreshold" v-model="form.freeShippingThreshold" :min="0" :controls="false" />
                </div>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="8"><el-form-item label="庫存偏低門檻（預設）"><el-input-number v-model="form.lowStockThreshold" :min="0" :controls="false" style="width: 100%" /></el-form-item></el-col>
          </el-row>
          <el-form-item label="離島與不配送地區">
            <el-select v-model="form.excludedRegions" multiple filterable allow-create default-first-option :reserve-keyword="false" placeholder="輸入地區名稱後按 Enter 新增" style="width: 100%" />
          </el-form-item>
          <el-form-item label="待付款保留時間（分鐘）">
            <el-input-number v-model="form.pendingTimeoutMinutes" :min="5" :max="1440" :step="5" />
            <p class="settings__hint">超過這段時間仍未付款的訂單，可到「訂單」按「釋回逾時未付款訂單」取消並釋回庫存（目前沒有自動排程，前台結帳上線時會改為自動）。</p>
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="商店入口說明" class="settings__block">
          <BilingualShortField label="商店標題" :zh="form.entryTitleZh" :en="form.entryTitleEn" @update:zh="(v) => (form.entryTitleZh = v)" @update:en="(v) => (form.entryTitleEn = v)" />
          <BilingualTextareaField label="商店簡介" :zh="form.entryIntroZh" :en="form.entryIntroEn" :rows="4" @update:zh="(v) => (form.entryIntroZh = v)" @update:en="(v) => (form.entryIntroEn = v)" />
        </el-card>

        <el-card shadow="never" header="購物政策" class="settings__block">
          <BilingualTextareaField label="購物須知" :zh="form.noticeZh" :en="form.noticeEn" :rows="4" @update:zh="(v) => (form.noticeZh = v)" @update:en="(v) => (form.noticeEn = v)" />
          <BilingualTextareaField label="運送說明" :zh="form.shippingZh" :en="form.shippingEn" :rows="4" @update:zh="(v) => (form.shippingZh = v)" @update:en="(v) => (form.shippingEn = v)" />
          <BilingualTextareaField label="退換貨說明" :zh="form.returnsZh" :en="form.returnsEn" :rows="4" @update:zh="(v) => (form.returnsZh = v)" @update:en="(v) => (form.returnsEn = v)" />
          <BilingualTextareaField label="購物條款" :zh="form.termsZh" :en="form.termsEn" :rows="4" @update:zh="(v) => (form.termsZh = v)" @update:en="(v) => (form.termsEn = v)" />
        </el-card>
      </el-form>
      <div class="settings__foot">
        <el-button v-if="canUpdate" type="primary" :loading="saving" :disabled="!isDirty" @click="save">儲存商店設定</el-button>
        <span v-if="updatedAt" class="settings__hint">最後更新 {{ formatDateTime(updatedAt) }}</span>
      </div>
    </template>
  </div>
</template>

<style scoped>
.settings { max-width: 900px; }
.settings__block { margin-bottom: 16px; }
.settings__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.settings__threshold { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
.settings__foot { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin-bottom: 24px; }
</style>
