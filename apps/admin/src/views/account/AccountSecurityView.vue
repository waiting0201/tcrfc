<script setup lang="ts">
/**
 * 帳號安全設定：更改密碼＋兩階段驗證（J1）。
 *
 * 2026-09-30 使用者裁決：首次登入不再強制改密、也不再強制啟用兩階段驗證（正式環境亦同），
 * 這一頁只剩使用者自己從選單「帳號安全設定」進來調整的用途，沒有任何強制導向或 `forced` 參數。
 * 已啟用兩階段驗證的帳號登入時仍要輸入驗證碼（見 `LoginView.vue` 的 `totp_required` 流程）。
 */
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  beginTwoFactorSetup,
  changePassword,
  confirmTwoFactorSetup,
  disableTwoFactor,
} from '@/api/adminAuth'
import { AdminApiError } from '@/api/http'
import { authUser, patchUserFlags } from '@/auth/session'

const router = useRouter()

const user = authUser

// ── 更改密碼 ──────────────────────────────────────────────────────────────
const pwForm = ref({ currentPassword: '', newPassword: '', confirmPassword: '' })
const pwSubmitting = ref(false)
const pwError = ref('')

async function submitChangePassword() {
  pwError.value = ''
  if (pwForm.value.newPassword.length < 10) {
    pwError.value = '新密碼長度至少需要 10 個字元。'
    return
  }
  if (pwForm.value.newPassword !== pwForm.value.confirmPassword) {
    pwError.value = '兩次輸入的新密碼不一致。'
    return
  }
  pwSubmitting.value = true
  try {
    await changePassword(pwForm.value.currentPassword, pwForm.value.newPassword)
    patchUserFlags({ mustChangePassword: false })
    pwForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
    ElMessage.success('密碼已更新')
  } catch (error) {
    pwError.value = error instanceof AdminApiError ? error.message : '密碼更新失敗，請稍後再試。'
  } finally {
    pwSubmitting.value = false
  }
}

// ── 兩階段驗證 ────────────────────────────────────────────────────────────
const totpSetup = ref<{ secret: string; otpAuthUrl: string } | null>(null)
const totpCode = ref('')
const totpSubmitting = ref(false)
const totpError = ref('')
const disablePassword = ref('')
const disableSubmitting = ref(false)

async function startTotpSetup() {
  totpError.value = ''
  try {
    totpSetup.value = await beginTwoFactorSetup()
  } catch (error) {
    totpError.value = error instanceof AdminApiError ? error.message : '無法開始設定，請稍後再試。'
  }
}

async function submitTotpConfirm() {
  totpError.value = ''
  if (!totpCode.value.trim()) {
    totpError.value = '請輸入驗證碼。'
    return
  }
  totpSubmitting.value = true
  try {
    await confirmTwoFactorSetup(totpCode.value.trim())
    patchUserFlags({ twoFactorEnabled: true })
    totpSetup.value = null
    totpCode.value = ''
    ElMessage.success('已啟用兩階段驗證')
  } catch (error) {
    totpError.value = error instanceof AdminApiError ? error.message : '驗證碼不正確，請稍後再試。'
  } finally {
    totpSubmitting.value = false
  }
}

async function submitDisableTotp() {
  try {
    await ElMessageBox.confirm('停用兩階段驗證會降低帳號安全性，確定要停用嗎？', '確認停用', {
      confirmButtonText: '停用',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  disableSubmitting.value = true
  try {
    await disableTwoFactor(disablePassword.value)
    patchUserFlags({ twoFactorEnabled: false })
    disablePassword.value = ''
    ElMessage.success('已停用兩階段驗證')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '停用失敗，請稍後再試。')
  } finally {
    disableSubmitting.value = false
  }
}

function goBack() {
  router.push('/dashboard')
}
</script>

<template>
  <div class="account-security">
    <div class="account-security__inner">
      <h1 class="account-security__title">帳號安全設定</h1>

      <el-card shadow="never" header="更改密碼" class="account-security__card">
        <el-alert v-if="pwError" :title="pwError" type="error" show-icon class="account-security__error" />
        <el-form label-position="top">
          <el-form-item label="目前密碼">
            <el-input v-model="pwForm.currentPassword" type="password" show-password />
          </el-form-item>
          <el-form-item label="新密碼">
            <el-input v-model="pwForm.newPassword" type="password" show-password placeholder="至少 10 個字元" />
          </el-form-item>
          <el-form-item label="確認新密碼">
            <el-input v-model="pwForm.confirmPassword" type="password" show-password />
          </el-form-item>
          <el-button type="primary" :loading="pwSubmitting" @click="submitChangePassword">更新密碼</el-button>
        </el-form>
      </el-card>

      <el-card shadow="never" header="兩階段驗證" class="account-security__card">
        <template v-if="user?.twoFactorEnabled">
          <p class="account-security__status">目前已啟用兩階段驗證。</p>
          <el-form label-position="top">
            <el-form-item label="輸入目前密碼以停用">
              <el-input v-model="disablePassword" type="password" show-password />
            </el-form-item>
            <el-button type="danger" plain :loading="disableSubmitting" @click="submitDisableTotp">
              停用兩階段驗證
            </el-button>
          </el-form>
        </template>
        <template v-else>
          <p class="account-security__status">目前尚未啟用兩階段驗證。</p>
          <p class="account-security__hint">
            建議啟用兩階段驗證：即使密碼外流，別人沒有你手機上的驗證碼也登不進來。這不是必要設定，你可以隨時自行開啟。
          </p>
          <el-alert v-if="totpError" :title="totpError" type="error" show-icon class="account-security__error" />
          <el-button v-if="!totpSetup" type="primary" @click="startTotpSetup">開始設定</el-button>
          <template v-else>
            <p class="account-security__hint">
              請在你的驗證器 App（例如 Google Authenticator）中掃描或手動輸入以下金鑰：
            </p>
            <el-input :model-value="totpSetup.secret" readonly class="account-security__secret">
              <template #append>
                <span>設定金鑰</span>
              </template>
            </el-input>
            <p class="account-security__hint">加入後，輸入 App 上顯示的 6 位數驗證碼完成設定：</p>
            <el-form label-position="top">
              <el-form-item label="驗證碼">
                <el-input v-model="totpCode" maxlength="6" placeholder="6 位數驗證碼" />
              </el-form-item>
              <el-button type="primary" :loading="totpSubmitting" @click="submitTotpConfirm">
                確認並啟用
              </el-button>
            </el-form>
          </template>
        </template>
      </el-card>

      <el-button text @click="goBack">返回後台</el-button>
    </div>
  </div>
</template>

<style scoped>
.account-security {
  min-height: 100vh;
  background: var(--admin-bg-canvas);
  padding: var(--admin-space-6) var(--admin-space-4);
  display: flex;
  justify-content: center;
}

.account-security__inner {
  width: 480px;
  max-width: 100%;
}

.account-security__title {
  font-size: 18px;
  color: var(--admin-text-primary);
  margin: 0 0 var(--admin-space-4);
}

.account-security__error {
  margin-bottom: var(--admin-space-4);
}

.account-security__card {
  margin-bottom: var(--admin-space-4);
}

.account-security__status {
  font-size: 13px;
  color: var(--admin-text-secondary);
  margin: 0 0 var(--admin-space-3);
}

.account-security__hint {
  font-size: 13px;
  color: var(--admin-text-secondary);
  margin: var(--admin-space-3) 0 var(--admin-space-2);
}

.account-security__secret {
  font-family: monospace;
}
</style>
