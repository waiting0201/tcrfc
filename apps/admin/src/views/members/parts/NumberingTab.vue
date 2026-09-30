<script setup lang="ts">
/** 「會員編號規則」分頁：前綴與位數，只影響後台新建的會員。 */
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { getMemberSettings, updateMemberSettings } from '@/api/adminMemberships'
import { errorMessage } from './membershipHelpers'

const { canView, canUpdate } = useViewUpdatePermissions('member.setting')
const club = computed(() => activeClubId.value)

const prefix = ref('')
const digits = ref(6)
const preview = ref('')
const loading = ref(true)
const loadError = ref<string | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const s = await getMemberSettings(club.value)
    prefix.value = s.memberNoPrefix ?? ''
    digits.value = s.memberNoDigits
    preview.value = s.nextMemberNoPreview ?? ''
  } catch (error) {
    loadError.value = errorMessage(error, '設定載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, load)

async function save() {
  formError.value = null
  const p = prefix.value.trim()
  if (!/^[A-Za-z0-9]{0,8}$/.test(p)) return void (formError.value = '前綴只能是英文字母或數字，最多 8 個字')
  if (!Number.isInteger(digits.value) || digits.value < 4 || digits.value > 10) return void (formError.value = '流水號位數必須在 4 到 10 之間')
  saving.value = true
  try {
    await updateMemberSettings(club.value, { memberNoPrefix: p, memberNoDigits: digits.value })
    ElMessage.success('已儲存')
    await load()
  } catch (error) {
    formError.value = errorMessage(error, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-card shadow="never" class="num-tab">
    <el-skeleton v-if="loading" :rows="3" animated />
    <el-empty v-else-if="loadError" :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    <template v-else>
      <el-alert type="info" :closable="false" show-icon class="num-tab__alert">
        這個規則只影響之後在後台新建的會員；已經存在的會員編號不會變動。兩個俱樂部各有一份設定，但會員編號在全站不會重複。
      </el-alert>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="num-tab__alert" @close="formError = null" />
      <el-form label-position="top" :disabled="!canUpdate || !canView" @submit.prevent="save">
        <el-form-item label="編號前綴（英文字母或數字，最多 8 字，可留空）">
          <el-input v-model="prefix" maxlength="8" placeholder="例如 M" />
        </el-form-item>
        <el-form-item label="流水號位數（4 到 10 位）">
          <el-input-number v-model="digits" :min="4" :max="10" :precision="0" />
        </el-form-item>
        <el-form-item label="下一個會員編號預覽">
          <el-input :model-value="preview || '—'" disabled />
        </el-form-item>
      </el-form>
      <el-button v-if="canUpdate" type="primary" :loading="saving" @click="save">儲存</el-button>
      <p v-else class="num-tab__hint">你的帳號只能檢視這項設定，不能修改。</p>
    </template>
  </el-card>
</template>

<style scoped>
.num-tab { max-width: 520px; }
.num-tab__alert { margin-bottom: 14px; }
.num-tab__hint { font-size: 12px; color: var(--admin-text-tertiary); }
</style>
