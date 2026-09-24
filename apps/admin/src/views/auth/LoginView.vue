<script setup lang="ts">
/**
 * 登入頁（規劃書 §4.10 J1、apps/api/README.md「後台登入權杖設計」）：
 * 帳號密碼 → （若該帳號已啟用兩階段驗證）驗證碼 → 成功後由路由守衛（`router/index.ts`）
 * 決定接下來要不要先導去強制改密／強制設定兩階段驗證，這裡不重複判斷。
 *
 * 密碼已通過但尚未提供驗證碼時，後端回應是 `totp_required`（不是登入失敗），畫面在同一頁
 * 切換成「請輸入驗證碼」步驟，沿用同一組帳號密碼再送一次；驗證碼錯誤才會真的計入鎖定次數。
 */
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { login } from '@/api/adminAuth'

const route = useRoute()
const router = useRouter()

const step = ref<'credentials' | 'totp'>('credentials')
const form = reactive({ username: '', password: '', totpCode: '' })
const submitting = ref(false)
const errorMessage = ref('')

async function submitCredentials() {
  errorMessage.value = ''
  if (!form.username.trim() || !form.password) {
    errorMessage.value = '請輸入帳號與密碼。'
    return
  }
  await attemptLogin()
}

async function submitTotp() {
  errorMessage.value = ''
  if (!form.totpCode.trim()) {
    errorMessage.value = '請輸入驗證碼。'
    return
  }
  await attemptLogin()
}

async function attemptLogin() {
  submitting.value = true
  try {
    const result = await login(form.username.trim(), form.password, form.totpCode || undefined)
    switch (result.kind) {
      case 'success': {
        const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'
        router.push(redirect)
        break
      }
      case 'totp-required':
        step.value = 'totp'
        break
      case 'locked':
      case 'disabled':
      case 'invalid-credentials':
      case 'network':
        errorMessage.value = result.message
        // 驗證碼錯誤時只清驗證碼，讓使用者不用重打帳密；帳密本身錯誤時退回第一步重新輸入。
        if (step.value === 'totp' && result.kind === 'invalid-credentials') {
          form.totpCode = ''
        } else {
          step.value = 'credentials'
        }
        break
    }
  } finally {
    submitting.value = false
  }
}

function backToCredentials() {
  step.value = 'credentials'
  form.totpCode = ''
  errorMessage.value = ''
}
</script>

<template>
  <div class="login-page">
    <div class="login-card">
      <div class="login-card__brand">台中磐石後台管理系統</div>

      <el-form v-if="step === 'credentials'" label-position="top" @submit.prevent="submitCredentials">
        <el-alert v-if="errorMessage" :title="errorMessage" type="error" show-icon class="login-card__error" />
        <el-form-item label="帳號">
          <el-input v-model="form.username" placeholder="請輸入帳號" autofocus @keyup.enter="submitCredentials" />
        </el-form-item>
        <el-form-item label="密碼">
          <el-input
            v-model="form.password"
            type="password"
            show-password
            placeholder="請輸入密碼"
            @keyup.enter="submitCredentials"
          />
        </el-form-item>
        <el-button type="primary" class="login-card__submit" :loading="submitting" @click="submitCredentials">
          登入
        </el-button>
      </el-form>

      <el-form v-else label-position="top" @submit.prevent="submitTotp">
        <el-alert v-if="errorMessage" :title="errorMessage" type="error" show-icon class="login-card__error" />
        <p class="login-card__hint">請開啟你的驗證器 App，輸入目前顯示的 6 位數驗證碼。</p>
        <el-form-item label="驗證碼">
          <el-input
            v-model="form.totpCode"
            placeholder="6 位數驗證碼"
            maxlength="6"
            autofocus
            @keyup.enter="submitTotp"
          />
        </el-form-item>
        <el-button type="primary" class="login-card__submit" :loading="submitting" @click="submitTotp">
          驗證並登入
        </el-button>
        <el-button text class="login-card__back" @click="backToCredentials">重新輸入帳號密碼</el-button>
      </el-form>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--admin-bg-canvas);
  padding: var(--admin-space-4);
}

.login-card {
  width: 360px;
  max-width: 100%;
  background: var(--admin-bg-surface);
  border: 1px solid var(--admin-border);
  border-radius: 8px;
  padding: var(--admin-space-6);
}

.login-card__brand {
  font-size: 16px;
  font-weight: 600;
  color: var(--admin-text-primary);
  margin-bottom: var(--admin-space-5);
  text-align: center;
}

.login-card__error {
  margin-bottom: var(--admin-space-4);
}

.login-card__hint {
  font-size: 13px;
  color: var(--admin-text-secondary);
  margin: 0 0 var(--admin-space-3);
}

.login-card__submit {
  width: 100%;
}

.login-card__back {
  width: 100%;
  margin-top: var(--admin-space-2);
}
</style>
