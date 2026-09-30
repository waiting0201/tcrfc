<script setup lang="ts">
/**
 * 金流與電子發票憑證（只有系統管理員）。
 * 🔴 尚未串接：取得 LINE Pay 與電子發票的商店號後才會啟用。憑證可以先存放，但系統目前不會用它連線，
 *    也不會收款或開立發票。
 * 🔴 只寫不讀：密鑰儲存後永遠不會再顯示、也不會回傳；識別碼只顯示遮罩後的尾碼。密鑰欄位留空＝沿用原本的密鑰。
 * 憑證屬於「收款主體俱樂部」，不論從哪個站台操作，都是同一組。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import {
  getShopCredentials,
  saveEInvoiceCredential,
  saveInvoiceRetry,
  saveLinePayCredential,
  saveShopMode,
  type CredentialEnvDto,
  type ShopCredentialsDto,
  type ShopEnvironment,
} from '@/api/adminShop'
import { formatDateTime } from '@/utils/dateTime'

const club = computed(() => activeClubId.value)
const data = ref<ShopCredentialsDto | null>(null)
const loading = ref(true)
const loadError = ref<string | null>(null)
const ENV_LABEL: Record<ShopEnvironment, string> = { sandbox: '測試環境', production: '正式環境' }
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const subjectText = computed(() => {
  const s = data.value?.collectingSubject
  if (!s) return ''
  return typeof s === 'string' ? s : [s.name, s.notice].filter(Boolean).join('：')
})

async function load() {
  loading.value = true
  loadError.value = null
  try {
    data.value = await getShopCredentials(club.value)
    Object.assign(retry, data.value.invoiceRetry)
  } catch (error) {
    data.value = null
    loadError.value = errorText(error, '憑證資料載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, load)

// ── 使用環境 ──
const modeSaving = ref(false)
async function changeMode(env: ShopEnvironment) {
  if (!data.value || env === data.value.environment) return
  try {
    await ElMessageBox.confirm(`確定要把金流環境切換為「${ENV_LABEL[env]}」嗎？`, '切換金流環境', { confirmButtonText: '確定切換', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  modeSaving.value = true
  try {
    data.value = await saveShopMode(club.value, env)
    ElMessage.success('已切換')
  } catch (error) {
    ElMessage.error(errorText(error, '切換失敗，請稍後再試'))
  } finally {
    modeSaving.value = false
  }
}

// ── LINE Pay ──
const pay = reactive({ environment: 'sandbox' as ShopEnvironment, channelId: '', channelSecret: '' })
const paySaving = ref(false)
const payError = ref<string | null>(null)
async function savePay() {
  payError.value = null
  const current = data.value?.linePay[pay.environment]
  if (!pay.channelId.trim()) return (payError.value = '請輸入商店識別碼')
  if (!current?.configured && !pay.channelSecret.trim()) return (payError.value = '第一次設定必須輸入商店密鑰')
  paySaving.value = true
  try {
    data.value = await saveLinePayCredential(club.value, { environment: pay.environment, channelId: pay.channelId.trim(), channelSecret: pay.channelSecret.trim() || undefined })
    pay.channelId = ''
    pay.channelSecret = ''
    ElMessage.success('已存放 LINE Pay 憑證（尚未串接，不會實際連線）')
  } catch (error) {
    payError.value = errorText(error, '儲存失敗，請稍後再試')
  } finally {
    paySaving.value = false
  }
}

// ── 電子發票 ──
const inv = reactive({ environment: 'sandbox' as ShopEnvironment, merchantId: '', apiKey: '', invoicePrefix: '' })
const invSaving = ref(false)
const invError = ref<string | null>(null)
async function saveInv() {
  invError.value = null
  const prefix = inv.invoicePrefix.trim().toUpperCase()
  if (!/^[A-Z]{2}$/.test(prefix)) return (invError.value = '發票字軌必須是兩位英文字母')
  invSaving.value = true
  try {
    data.value = await saveEInvoiceCredential(club.value, {
      environment: inv.environment,
      merchantId: nullIfBlank(inv.merchantId) ?? undefined,
      apiKey: nullIfBlank(inv.apiKey) ?? undefined,
      invoicePrefix: prefix,
    })
    inv.merchantId = ''
    inv.apiKey = ''
    ElMessage.success('已存放電子發票憑證（尚未串接，不會實際開立發票）')
  } catch (error) {
    invError.value = errorText(error, '儲存失敗，請稍後再試')
  } finally {
    invSaving.value = false
  }
}

// ── 重試設定 ──
const retry = reactive({ maxRetries: 3, intervalMinutes: 10 })
const retrySaving = ref(false)
async function saveRetry() {
  if (!Number.isInteger(retry.maxRetries) || retry.maxRetries < 0 || retry.maxRetries > 10) return ElMessage.warning('重試次數要在 0 到 10 之間')
  if (!Number.isInteger(retry.intervalMinutes) || retry.intervalMinutes < 1 || retry.intervalMinutes > 1440) return ElMessage.warning('重試間隔要在 1 到 1440 分鐘之間')
  retrySaving.value = true
  try {
    data.value = await saveInvoiceRetry(club.value, { maxRetries: retry.maxRetries, intervalMinutes: retry.intervalMinutes })
    ElMessage.success('已儲存')
  } catch (error) {
    ElMessage.error(errorText(error, '儲存失敗，請稍後再試'))
  } finally {
    retrySaving.value = false
  }
}

function envStatus(env?: CredentialEnvDto): string {
  if (!env || !env.configured) return '尚未設定'
  return `已設定${env.identifierMasked ? `（識別碼 ${env.identifierMasked}）` : ''}${env.rotatedAt ? `・密鑰更新於 ${formatDateTime(env.rotatedAt)}` : ''}`
}
</script>

<template>
  <div class="cred">
    <el-alert type="warning" show-icon :closable="false" class="cred__block" title="尚未串接，取得商店號後啟用" description="LINE Pay 與電子發票的商店號尚未到位。這裡可以先存放憑證，但系統目前不會用它連線：不會收款、也不會開立發票。密鑰儲存後不會再顯示，忘記了只能重新輸入。" />
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <template v-else-if="data">
      <p v-if="subjectText" class="cred__hint">憑證屬於收款主體，不論從哪個站台操作，都是同一組。{{ subjectText }}</p>

      <el-card shadow="never" header="目前使用的金流環境" class="cred__block">
        <el-radio-group :model-value="data.environment" :disabled="modeSaving" @change="(v: string | number | boolean | undefined) => changeMode(v as ShopEnvironment)">
          <el-radio-button value="sandbox">測試環境</el-radio-button>
          <el-radio-button value="production">正式環境</el-radio-button>
        </el-radio-group>
        <p class="cred__hint">連線狀態：{{ data.integrationConnected ? '已連線' : '尚未串接' }}。</p>
      </el-card>

      <el-card shadow="never" header="LINE Pay" class="cred__block">
        <ul class="cred__status">
          <li>測試環境：{{ envStatus(data.linePay.sandbox) }}</li>
          <li>正式環境：{{ envStatus(data.linePay.production) }}</li>
        </ul>
        <el-alert v-if="payError" :title="payError" type="warning" show-icon class="cred__block" @close="payError = null" />
        <el-form label-position="top" autocomplete="off">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="8"><el-form-item label="要設定的環境"><el-select v-model="pay.environment" style="width: 100%"><el-option label="測試環境" value="sandbox" /><el-option label="正式環境" value="production" /></el-select></el-form-item></el-col>
            <el-col :xs="24" :sm="8"><el-form-item label="商店識別碼"><el-input v-model="pay.channelId" autocomplete="off" /></el-form-item></el-col>
            <el-col :xs="24" :sm="8"><el-form-item label="商店密鑰（只寫不讀）"><el-input v-model="pay.channelSecret" type="password" show-password autocomplete="new-password" :placeholder="data.linePay[pay.environment]?.configured ? '留空＝沿用原本的密鑰' : '第一次設定必填'" /></el-form-item></el-col>
          </el-row>
        </el-form>
        <el-button type="primary" :loading="paySaving" @click="savePay">存放憑證</el-button>
      </el-card>

      <el-card shadow="never" header="電子發票" class="cred__block">
        <ul class="cred__status">
          <li>測試環境：{{ envStatus(data.eInvoice.sandbox) }}{{ data.eInvoice.sandbox?.invoicePrefix ? `・字軌 ${data.eInvoice.sandbox.invoicePrefix}` : '' }}</li>
          <li>正式環境：{{ envStatus(data.eInvoice.production) }}{{ data.eInvoice.production?.invoicePrefix ? `・字軌 ${data.eInvoice.production.invoicePrefix}` : '' }}</li>
        </ul>
        <el-alert v-if="invError" :title="invError" type="warning" show-icon class="cred__block" @close="invError = null" />
        <el-form label-position="top" autocomplete="off">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="6"><el-form-item label="要設定的環境"><el-select v-model="inv.environment" style="width: 100%"><el-option label="測試環境" value="sandbox" /><el-option label="正式環境" value="production" /></el-select></el-form-item></el-col>
            <el-col :xs="24" :sm="6"><el-form-item label="商店代號"><el-input v-model="inv.merchantId" autocomplete="off" placeholder="留空＝不變" /></el-form-item></el-col>
            <el-col :xs="24" :sm="6"><el-form-item label="服務金鑰（只寫不讀）"><el-input v-model="inv.apiKey" type="password" show-password autocomplete="new-password" placeholder="留空＝不變" /></el-form-item></el-col>
            <el-col :xs="24" :sm="6"><el-form-item label="發票字軌（兩位英文字母）" required><el-input v-model="inv.invoicePrefix" maxlength="2" autocomplete="off" placeholder="例如 AB" /></el-form-item></el-col>
          </el-row>
        </el-form>
        <el-button type="primary" :loading="invSaving" @click="saveInv">存放憑證</el-button>
      </el-card>

      <el-card shadow="never" header="發票開立與作廢的重試" class="cred__block">
        <el-form label-position="top">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="8"><el-form-item label="最多重試次數（0–10）"><el-input-number v-model="retry.maxRetries" :min="0" :max="10" /></el-form-item></el-col>
            <el-col :xs="24" :sm="8"><el-form-item label="重試間隔（分鐘，1–1440）"><el-input-number v-model="retry.intervalMinutes" :min="1" :max="1440" /></el-form-item></el-col>
          </el-row>
        </el-form>
        <el-button :loading="retrySaving" @click="saveRetry">儲存重試設定</el-button>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.cred { max-width: 900px; }
.cred__block { margin-bottom: 16px; }
.cred__hint { margin: 0 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.cred__status { margin: 0 0 12px; padding-left: 18px; font-size: 13px; color: var(--admin-text-secondary); line-height: 1.8; }
</style>
