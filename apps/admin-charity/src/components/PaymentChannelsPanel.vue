<script setup lang="ts">
/**
 * 金流與發票通道（僅系統管理員）：LINE Pay 與電子發票加值中心各有「測試」與「正式」兩套憑證，系統同一時間只使用其中一套。
 * 🔴 憑證只寫不讀：送出後由伺服器加密保存，之後任何畫面與端點都不會再顯示它，這裡只看得到「已設定／尚未設定」。
 * 🔴 切換到正式環境之後，系統會真的向捐款人收款、開立真實的發票，所以要二次確認。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import ErrorState from '@/components/ErrorState.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import { listPaymentChannels, setPaymentCredential, switchPaymentEnvironment, type PaymentChannel, type PaymentEnvironment } from '@/api/settings'
import { AdminApiError } from '@/api/http'
import { formatTaipei } from '@/utils/format'

const loading = ref(false)
const loadError = ref('')
const channels = ref<PaymentChannel[]>([])

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    channels.value = await listPaymentChannels()
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '讀取金流設定時發生問題'
  } finally {
    loading.value = false
  }
}

onMounted(load)

const envLabel = (e: PaymentEnvironment) => (e === 'production' ? '正式環境' : '測試環境')

// ── 更換憑證 ───────────────────────────────────────────────────────────
const credVisible = ref(false)
const credTarget = ref<{ channel: PaymentChannel; environment: PaymentEnvironment } | null>(null)
const credForm = reactive({ credential: '', invoicePrefix: '' })
const credBusy = ref(false)
const credError = ref('')
const needsPrefix = computed(() => credTarget.value?.channel.channelType === 'einvoice')

function openCredential(channel: PaymentChannel, environment: PaymentEnvironment) {
  credTarget.value = { channel, environment }
  credForm.credential = ''
  credForm.invoicePrefix = channel.environments.find((e) => e.environment === environment)?.invoicePrefix ?? ''
  credError.value = ''
  credVisible.value = true
}

async function saveCredential() {
  const target = credTarget.value
  if (!target || credBusy.value) return
  if (!credForm.credential.trim()) {
    credError.value = '請貼上新的憑證內容'
    return
  }
  credBusy.value = true
  credError.value = ''
  try {
    await setPaymentCredential(target.channel.channelType, {
      environment: target.environment,
      credential: credForm.credential,
      invoicePrefix: needsPrefix.value ? credForm.invoicePrefix.trim() || undefined : undefined,
    })
    credForm.credential = '' // 不在畫面上留著金鑰
    credVisible.value = false
    ElMessage.success(`已更換「${target.channel.label}」${envLabel(target.environment)}的憑證`)
    await load()
  } catch (error) {
    credError.value = error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試'
  } finally {
    credBusy.value = false
  }
}

// ── 切換環境 ───────────────────────────────────────────────────────────
const CONFIRM_PHRASE = '切換到正式環境'
const switchVisible = ref(false)
const switchTarget = ref<{ channel: PaymentChannel; to: PaymentEnvironment } | null>(null)
const switchTyped = ref('')
const switchBusy = ref(false)
const switchError = ref('')
const toProduction = computed(() => switchTarget.value?.to === 'production')
const switchReady = computed(() => !toProduction.value || switchTyped.value.trim() === CONFIRM_PHRASE)

function openSwitch(channel: PaymentChannel) {
  switchTarget.value = { channel, to: channel.activeEnvironment === 'production' ? 'sandbox' : 'production' }
  switchTyped.value = ''
  switchError.value = ''
  switchVisible.value = true
}

async function doSwitch() {
  const t = switchTarget.value
  if (!t || switchBusy.value || !switchReady.value) return
  switchBusy.value = true
  switchError.value = ''
  try {
    await switchPaymentEnvironment(t.channel.channelType, t.to)
    switchVisible.value = false
    ElMessage.success(`「${t.channel.label}」已切換到${envLabel(t.to)}`)
    await load()
  } catch (error) {
    // 例如「正式環境還沒設定憑證」（409）：把後端的原因原文顯示出來。
    switchError.value = error instanceof AdminApiError ? error.detail : '切換失敗，請稍後再試'
  } finally {
    switchBusy.value = false
  }
}
</script>

<template>
  <ErrorState v-if="loadError" :text="loadError" @retry="load" />
  <div v-else v-loading="loading" class="channels">
    <el-alert
      type="warning"
      :closable="false"
      show-icon
      title="這裡的憑證只能寫入、不能讀取：送出後由伺服器加密保存，之後不會再顯示。切換到正式環境後，系統會真的向捐款人收款並開立真實的發票。"
      class="channels__alert"
    />
    <el-card v-for="c in channels" :key="c.channelType" shadow="never" class="channels__card">
      <template #header>
        <div class="channels__head">
          <strong>{{ c.label }}</strong>
          <SemanticTag :variant="c.activeEnvironment === 'production' ? 'danger' : 'info'">目前使用：{{ envLabel(c.activeEnvironment) }}</SemanticTag>
        </div>
      </template>
      <ul class="channels__envs">
        <li v-for="row in c.environments" :key="row.environment" class="channels__env">
          <div class="channels__env-head">
            <strong>{{ row.label || envLabel(row.environment) }}</strong>
            <el-tag v-if="row.environment === c.activeEnvironment" size="small" type="success">使用中</el-tag>
          </div>
          <dl class="channels__facts">
            <div>
              <dt>憑證</dt>
              <dd>
                <span v-if="row.hasCredential" class="channels__mask" aria-label="已設定">••••••••（已設定）</span>
                <SemanticTag v-else variant="warning">尚未設定</SemanticTag>
              </dd>
            </div>
            <div v-if="c.channelType === 'einvoice'">
              <dt>發票字軌</dt>
              <dd>{{ row.invoicePrefix ?? '—' }}</dd>
            </div>
            <div>
              <dt>上次更換</dt>
              <dd>{{ formatTaipei(row.rotatedAt) }}</dd>
            </div>
          </dl>
          <el-button size="small" @click="openCredential(c, row.environment)">更換憑證</el-button>
        </li>
      </ul>
      <div class="channels__switch">
        <el-button :type="c.activeEnvironment === 'production' ? 'default' : 'danger'" plain @click="openSwitch(c)">
          {{ c.activeEnvironment === 'production' ? '改回測試環境' : '切換到正式環境' }}
        </el-button>
      </div>
    </el-card>

    <el-dialog v-model="credVisible" :title="credTarget ? `更換${envLabel(credTarget.environment)}憑證` : '更換憑證'" width="480px" destroy-on-close>
      <p class="channels__hint">貼上新的憑證內容後送出。送出後這段內容不會再出現在任何畫面，如果貼錯了，請再更換一次。</p>
      <el-form label-position="top" autocomplete="off" @submit.prevent>
        <el-form-item label="新的憑證內容" required>
          <el-input v-model="credForm.credential" type="password" autocomplete="new-password" placeholder="貼上憑證內容" />
        </el-form-item>
        <el-form-item v-if="needsPrefix" label="發票字軌（選填）">
          <el-input v-model="credForm.invoicePrefix" maxlength="16" placeholder="不填代表維持原本的字軌" />
          <div class="channels__hint channels__hint--field">字軌是協會自己的，不能與俱樂部共用；切換到正式環境前，正式環境一定要設定字軌。</div>
        </el-form-item>
      </el-form>
      <el-alert v-if="credError" type="error" :closable="false" show-icon :title="credError" />
      <template #footer>
        <el-button @click="credVisible = false">取消</el-button>
        <el-button type="primary" :loading="credBusy" @click="saveCredential">儲存憑證</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="switchVisible" :title="toProduction ? '切換到正式環境' : '改回測試環境'" width="480px" destroy-on-close>
      <template v-if="switchTarget">
        <el-alert
          v-if="toProduction"
          type="error"
          :closable="false"
          show-icon
          title="切換之後，捐款人付的是真的錢，開出的是真的發票或收據。"
        />
        <p class="channels__hint">
          {{ toProduction
            ? `「${switchTarget.channel.label}」目前使用測試環境。切換前請確認正式環境的憑證已經設定好（沒有設定的話系統會拒絕切換），而且測試環境已經完整測過。`
            : `「${switchTarget.channel.label}」會改用測試環境，之後的捐款不會真的收款。` }}
        </p>
        <el-form-item v-if="toProduction" :label="`請輸入「${CONFIRM_PHRASE}」以確認`">
          <el-input v-model="switchTyped" :placeholder="CONFIRM_PHRASE" autocomplete="off" />
        </el-form-item>
        <p class="channels__notice">這個操作會記入操作紀錄。</p>
        <el-alert v-if="switchError" type="error" :closable="false" show-icon :title="switchError" />
      </template>
      <template #footer>
        <el-button @click="switchVisible = false">取消</el-button>
        <el-button type="danger" :loading="switchBusy" :disabled="!switchReady" @click="doSwitch">確認切換</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.channels {
  max-width: 820px;
}

.channels__alert {
  margin-bottom: var(--charity-admin-space-4);
}

.channels__card {
  margin-bottom: var(--charity-admin-space-4);
}

.channels__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--charity-admin-space-2);
}

.channels__envs {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-3);
}

.channels__env {
  border: 1px solid var(--charity-admin-border);
  border-radius: 6px;
  padding: var(--charity-admin-space-3);
}

.channels__env-head {
  display: flex;
  align-items: center;
  gap: var(--charity-admin-space-2);
  margin-bottom: var(--charity-admin-space-2);
}

.channels__facts {
  margin: 0 0 var(--charity-admin-space-3);
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: var(--charity-admin-space-2);
  font-size: 13px;
}

.channels__facts dt {
  color: var(--charity-admin-text-tertiary);
}

.channels__facts dd {
  margin: 0;
}

.channels__mask {
  letter-spacing: 1px;
}

.channels__switch {
  margin-top: var(--charity-admin-space-3);
}

.channels__hint {
  margin: var(--charity-admin-space-3) 0;
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.channels__hint--field {
  margin: var(--charity-admin-space-1) 0 0;
  font-size: 12px;
}

.channels__notice {
  margin: 0 0 var(--charity-admin-space-3);
  font-size: 13px;
  color: var(--charity-warning-text);
}
</style>
