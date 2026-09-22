<script setup lang="ts">
/**
 * 登入頁（mockup）。⛔ 後端認證尚未實作，這裡不做任何真的驗證邏輯或密碼雜湊——
 * admin_users.password_hash 是明顯的測試占位字串（見 db/seed 檔頭）。填任何帳密都會直接
 * 導向後台首頁，僅供外觀與動線展示。不放 logo：協會品牌資產未到位，識別區塊降級為純文字，
 * 不留空方框、不放假圖（比照 docs/22-charity-ui.md §2.2.1 前台的降級規則）。
 */
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { switchUser } from '@/data/session'

const router = useRouter()
const username = ref('')
const password = ref('')

function handleSubmit() {
  // mockup：不驗證帳密，一律以系統管理員身分導向後台首頁，並提示這是展示用途
  switchUser('sa@charity.local')
  router.push('/stores')
}
</script>

<template>
  <div class="login">
    <div class="login__card">
      <p class="login__brand">台灣足球策略發展協會</p>
      <h1 class="login__title">慈善捐款平台後台</h1>
      <el-alert
        type="info"
        :closable="false"
        title="這是外觀展示用的登入頁，後端認證尚未實作，輸入任何帳號密碼都能直接進入。"
        class="login__notice"
      />
      <el-form label-position="top" @submit.prevent="handleSubmit">
        <el-form-item label="帳號">
          <el-input v-model="username" placeholder="例如 sa@charity.local" />
        </el-form-item>
        <el-form-item label="密碼">
          <el-input v-model="password" type="password" show-password />
        </el-form-item>
        <el-button type="primary" native-type="submit" class="login__submit">登入</el-button>
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
