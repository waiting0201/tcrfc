<script setup lang="ts">
/**
 * 帳號安全設定：只有更改密碼（J1）。
 *
 * 2026-09-30 使用者裁決：首次登入不再強制改密、也不再強制啟用兩階段驗證（正式環境亦同）；
 * 同日再裁決：兩階段驗證的啟用／停用入口從介面隱藏，這一頁不再出現任何 2FA 區塊。
 * 系統能力保留：已啟用兩階段驗證的帳號登入時仍要輸入驗證碼（見 `LoginView.vue` 的 `totp_required`）。
 * `@/api/adminAuth` 的 `beginTwoFactorSetup`／`confirmTwoFactorSetup`／`disableTwoFactor` 刻意保留、
 * 不在介面引用，日後開放時直接接回即可。
 */
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { changePassword } from '@/api/adminAuth'
import { AdminApiError } from '@/api/http'
import { patchUserFlags } from '@/auth/session'

const router = useRouter()

// ── 更改密碼 ──────────────────────────────────────────────────────────────
const pwForm = ref({ currentPassword: '', newPassword: '', confirmPassword: '' })
const pwSubmitting = ref(false)
const pwError = ref('')

async function submitChangePassword() {
  pwError.value = ''
  if (pwForm.value.newPassword.length < 9) {
    pwError.value = '新密碼長度至少需要 9 個字元。'
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
            <el-input v-model="pwForm.newPassword" type="password" show-password placeholder="至少 9 個字元" />
          </el-form-item>
          <el-form-item label="確認新密碼">
            <el-input v-model="pwForm.confirmPassword" type="password" show-password />
          </el-form-item>
          <el-button type="primary" :loading="pwSubmitting" @click="submitChangePassword">更新密碼</el-button>
        </el-form>
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



</style>
