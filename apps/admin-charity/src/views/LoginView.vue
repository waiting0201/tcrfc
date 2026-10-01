<script setup lang="ts">
/**
 * 登入頁。慈善後台是獨立的帳號體系（與主站後台不相通）。
 * 流程：帳號＋密碼 → 若該帳號啟用了兩階段驗證，後端回「需要驗證碼」，這裡再多顯示一個驗證碼欄位，
 * 用同一組帳密與驗證碼重新送出。兩階段驗證是選用的（不強制）。不放 logo：協會品牌資產尚未提供。
 */
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { loadProfile, login } from '@/api/auth'

const router = useRouter()
const route = useRoute()
const username = ref('')
const password = ref('')
const totpCode = ref('')
const needsTotp = ref(false)
const submitting = ref(false)
const errorText = ref('')

async function handleSubmit() {
  if (submitting.value) return
  errorText.value = ''
  if (!username.value.trim() || !password.value) {
    errorText.value = '請輸入帳號與密碼。'
    return
  }
  submitting.value = true
  try {
    const outcome = await login(username.value.trim(), password.value, needsTotp.value ? totpCode.value.trim() : undefined)
    if (outcome.kind === 'totp-required') {
      needsTotp.value = true
      errorText.value = outcome.message
      return
    }
    if (outcome.kind !== 'success') {
      errorText.value = outcome.message
      if (needsTotp.value) totpCode.value = ''
      return
    }
    try { await loadProfile() } catch { /* 個人檔案讀不到不擋登入，之後的請求會再提示 */ }
    const redirect = typeof route.query.redirect === 'string' && route.query.redirect.startsWith('/') ? route.query.redirect : '/'
    await router.replace(redirect)
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="login">
    <div class="login__card">
      <p class="login__brand">台灣足球策略發展協會</p>
      <h1 class="login__title">慈善捐款平台後台</h1>
      <el-alert v-if="errorText" type="error" :closable="false" :title="errorText" class="login__notice" show-icon />
      <el-form label-position="top" @submit.prevent="handleSubmit">
        <el-form-item label="帳號">
          <el-input v-model="username" autocomplete="username" :disabled="needsTotp" />
        </el-form-item>
        <el-form-item label="密碼">
          <el-input v-model="password" type="password" show-password autocomplete="current-password" :disabled="needsTotp" />
        </el-form-item>
        <el-form-item v-if="needsTotp" label="兩階段驗證碼">
          <el-input v-model="totpCode" inputmode="numeric" maxlength="6" autocomplete="one-time-code" placeholder="6 位數字" />
        </el-form-item>
        <el-button type="primary" native-type="submit" class="login__submit" :loading="submitting">登入</el-button>
      </el-form>
    </div>
  </div>
</template>

<style scoped>
.login {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--charity-admin-bg-page);
  padding: var(--charity-admin-space-4);
}

.login__card {
  width: 100%;
  max-width: 360px;
  background: var(--charity-admin-bg-surface);
  border: 1px solid var(--charity-admin-border);
  border-radius: 6px;
  padding: var(--charity-admin-space-7);
}

.login__brand {
  margin: 0 0 4px;
  font-size: 13px;
  color: var(--charity-admin-text-tertiary);
}

.login__title {
  margin: 0 0 var(--charity-admin-space-5);
  font-size: 20px;
  color: var(--charity-admin-text-primary);
}

.login__notice {
  margin-bottom: var(--charity-admin-space-4);
}

.login__submit {
  width: 100%;
}
</style>
