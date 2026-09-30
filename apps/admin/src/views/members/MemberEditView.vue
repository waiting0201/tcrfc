<script setup lang="ts">
/** 現場建立會員（只有建立，沒有編輯模式）。對照 apps/api/README.md「K1 會員名單」的 `POST /members`。 */
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { createMember, MEMBER_LOCALE_OPTIONS, type MemberLocale } from '@/api/adminMembers'
import { pickerDateToDateOnly, taipeiToday } from '@/utils/dateTime'

const route = useRoute()
const router = useRouter()
// 這頁只有建立；仍用 computed 判斷，避免路由被改成編輯路徑時停在舊狀態
const isCreate = computed(() => route.name === 'member-new')
const { canCreate } = useCrudPermissions('member.account')

const form = reactive({ name: '', email: '', phone: '', birthOn: '', locale: 'zh-Hant' as MemberLocale, internalNote: '' })
const initialJson = JSON.stringify(form)
const saving = ref(false)
const saved = ref(false)
const formError = ref<string | null>(null)

const isDirty = computed(() => !saved.value && JSON.stringify(form) !== initialJson)
useUnsavedChanges(isDirty)

function validate(): boolean {
  formError.value = null
  if (!form.name.trim()) return (formError.value = '請輸入姓名'), false
  const email = form.email.trim()
  if (!email) return (formError.value = '請輸入 Email'), false
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return (formError.value = 'Email 格式不正確'), false
  return true
}

async function handleSave() {
  if (!canCreate.value || !isCreate.value || !validate()) return
  saving.value = true
  formError.value = null
  try {
    const created = await createMember(activeClubId.value, {
      name: form.name.trim(),
      email: form.email.trim(),
      phone: nullIfBlank(form.phone),
      birthOn: form.birthOn || null,
      locale: form.locale,
      internalNote: nullIfBlank(form.internalNote),
    })
    saved.value = true
    ElMessage.success(`已建立會員 ${created.memberNo}`)
    router.replace(`/members/list/${created.id}`)
  } catch (error) {
    if (error instanceof AdminApiError && error.status === 409) {
      formError.value = error.message || '這個 Email 已經有會員使用了，請改用其他 Email，或到名單搜尋既有帳號。'
    } else {
      formError.value = error instanceof AdminApiError ? error.message : '建立失敗，請稍後再試'
    }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="member-edit">
    <PageHeader title="現場建立會員">
      <template #back>
        <el-button text @click="router.push('/members/list')"><el-icon><ArrowLeft /></el-icon>返回名單</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="K1" /></template>
    </PageHeader>

    <el-alert v-if="!canCreate" title="你的帳號沒有建立會員的權限。" type="info" show-icon :closable="false" class="member-edit__block" />
    <el-alert v-if="formError" :title="formError" type="warning" show-icon class="member-edit__block" @close="formError = null" />
    <el-alert
      type="info"
      show-icon
      :closable="false"
      class="member-edit__block"
      title="現場入會不設定密碼，Email 視為尚未驗證。會員日後請用「忘記密碼」自行設定密碼；會員編號由系統依編號規則自動產生。"
    />

    <el-form label-position="top" :disabled="!canCreate" @submit.prevent="handleSave">
      <el-card shadow="never" header="會員資料" class="member-edit__block">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12">
            <el-form-item label="姓名" required><el-input v-model="form.name" maxlength="64" /></el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="Email" required><el-input v-model="form.email" maxlength="255" inputmode="email" /></el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="電話"><el-input v-model="form.phone" maxlength="32" inputmode="tel" /></el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="生日"><el-date-picker v-model="form.birthOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" :disabled-date="(d: Date) => pickerDateToDateOnly(d) > taipeiToday()" /></el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="語系偏好">
              <el-select v-model="form.locale" style="width: 100%">
                <el-option v-for="o in MEMBER_LOCALE_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="內部備註（會員看不到）">
          <el-input v-model="form.internalNote" type="textarea" :rows="3" maxlength="2000" show-word-limit />
        </el-form-item>
      </el-card>
    </el-form>
    <EditActionBar v-if="canCreate"><el-button type="primary" :loading="saving" @click="handleSave">建立會員</el-button></EditActionBar>
  </div>
</template>

<style scoped>
.member-edit { max-width: 780px; margin: 0 auto 88px; }
.member-edit__block { margin-bottom: 16px; }
</style>
